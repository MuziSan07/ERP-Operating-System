using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Ngo;

public class NgoReportService(IAppDbContext db, IAccessService access, LedgerService ledger, FundService funds, GrantService grants)
{
    public async Task<NgoDashboardDto> DashboardAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        var visible = mine.ByEntity.Where(kv => kv.Value.Any(p => p.StartsWith("ngo."))).Select(kv => kv.Key).ToHashSet();
        if (visible.Count == 0) throw new ForbiddenException();
        var today = NgoCommon.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var (fyStart, _) = await FiscalYearAsync(today, ct);

        var monthGifts = await db.Donations.Where(d => visible.Contains(d.EntityId) && d.Date >= monthStart && d.Date <= today)
            .Select(d => new { d.DonorId, d.Amount }).ToListAsync(ct);
        var active = await grants.ListItemsAsync(db.Grants.Where(g => visible.Contains(g.EntityId) && g.Status == GrantStatus.Active), ct);
        var activeIds = active.Select(g => g.Id).ToList();
        var rates = new Dictionary<Guid, decimal>();
        foreach (var g in await db.Grants.Where(x => activeIds.Contains(x.Id)).ToListAsync(ct)) rates[g.Id] = await NgoCommon.AverageRateAsync(db, g, ct);

        var due = new List<DueItem>();
        var horizon = today.AddDays(30);
        var reports = await db.GrantReports.Join(db.Grants, r => r.GrantId, g => g.Id, (r, g) => new { r, g })
            .Where(x => activeIds.Contains(x.g.Id) && x.r.SubmittedOn == null && x.r.DueDate <= horizon).ToListAsync(ct);
        due.AddRange(reports.Select(x => new DueItem("Report", x.g.Id, x.g.Number, $"{x.r.Title} — {x.g.Title}", x.r.DueDate, null, x.r.DueDate < today)));
        var tranches = await db.GrantTranches.Join(db.Grants, t => t.GrantId, g => g.Id, (t, g) => new { t, g })
            .Where(x => activeIds.Contains(x.g.Id) && x.t.ReceivedDate == null && x.t.DueDate <= horizon).ToListAsync(ct);
        due.AddRange(tranches.Select(x => new DueItem("Tranche", x.g.Id, x.g.Number, $"Tranche {x.t.Sequence} ({x.g.Currency}) — {x.g.Title}", x.t.DueDate, x.t.Amount, x.t.DueDate < today)));
        due.AddRange(active.Where(g => g.EndDate >= today && g.EndDate <= today.AddDays(60))
            .Select(g => new DueItem("Grant ends", g.Id, g.Number, g.Title, g.EndDate, null, false)));

        var assisted = await db.Assistance.Join(db.Beneficiaries, a => a.BeneficiaryId, b => b.Id, (a, b) => new { a, b })
            .Where(x => visible.Contains(x.b.EntityId) && x.a.Date >= monthStart && x.a.Date <= today).Select(x => x.a.BeneficiaryId).Distinct().CountAsync(ct);
        var functional = await FunctionalAsync(visible, fyStart, today, ct);
        var fundList = mine.ByEntity.Values.Any(s => s.Contains(Permissions.FundsView)) ? await funds.FundsAsync(ct) : [];
        return new NgoDashboardDto(monthGifts.Sum(d => d.Amount), monthGifts.Select(d => d.DonorId).Distinct().Count(), active.Count,
            active.Sum(g => Math.Round(g.Amount * rates.GetValueOrDefault(g.Id, 1), 2)), active.Sum(g => g.ReceivedBase), active.Sum(g => g.SpentBase),
            await db.Beneficiaries.CountAsync(b => visible.Contains(b.EntityId) && b.IsActive, ct), assisted, functional.ProgramRatio,
            fundList.Where(f => f.IsActive).ToList(), active, due.OrderBy(d => d.DueDate).ToList());
    }

    /// <summary>Statement of functional expenses: spending by program and by function (program / management &amp; general / fundraising).</summary>
    public async Task<FunctionalExpensesDto> FunctionalExpensesAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.NgoReports, ct);
        if (visible.Count == 0) throw new ForbiddenException();
        var today = NgoCommon.Today;
        var (fyStart, _) = await FiscalYearAsync(today, ct);
        return await FunctionalAsync(visible, from ?? fyStart, to ?? today, ct);
    }

    private async Task<FunctionalExpensesDto> FunctionalAsync(HashSet<Guid> visible, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var grouped = await db.FundExpenses.Where(e => visible.Contains(e.EntityId) && e.Date >= from && e.Date <= to)
            .GroupBy(e => new { e.ProgramId, e.Function }).Select(g => new { g.Key.ProgramId, g.Key.Function, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        var names = await db.NgoPrograms.ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var rows = grouped.Select(r => new { Program = r.ProgramId is { } id ? names.GetValueOrDefault(id) : null, r.Function, r.Total }).ToList();
        var list = rows.GroupBy(r => r.Program ?? "Not program-specific").OrderBy(g => g.Key == "Not program-specific").ThenBy(g => g.Key).Select(g =>
        {
            decimal Of(FunctionalCategory f) => g.Where(r => r.Function == f).Sum(r => r.Total);
            return new FunctionalRow(g.Key, Of(FunctionalCategory.Program), Of(FunctionalCategory.ManagementGeneral), Of(FunctionalCategory.Fundraising), g.Sum(r => r.Total));
        }).ToList();
        var program = list.Sum(r => r.ProgramCost);
        var total = list.Sum(r => r.Total);
        return new FunctionalExpensesDto(from, to, list, program, list.Sum(r => r.ManagementGeneral), list.Sum(r => r.Fundraising), total, NgoCommon.Percent(program, total));
    }

    public async Task<List<DonorSummaryRow>> DonorSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.NgoReports, ct);
        if (visible.Count == 0) throw new ForbiddenException();
        var today = NgoCommon.Today;
        var (fyStart, _) = await FiscalYearAsync(today, ct);
        var start = from ?? fyStart;
        var end = to ?? today;
        var gifts = await db.Donations.Where(d => visible.Contains(d.EntityId) && d.Date >= start && d.Date <= end).GroupBy(d => d.DonorId)
            .Select(g => new { g.Key, Total = g.Sum(d => d.Amount), Count = g.Count() }).ToListAsync(ct);
        var grantRows = await db.Grants.Where(g => visible.Contains(g.EntityId) && (g.Status == GrantStatus.Active || g.Status == GrantStatus.Closed))
            .Select(g => new { g.DonorId, Amount = g.BudgetLines.Sum(l => l.Amount), g.AgreementRate, Received = g.Tranches.Sum(t => t.ReceivedBase ?? 0) })
            .ToListAsync(ct);
        var donorIds = gifts.Select(g => g.Key).Concat(grantRows.Select(g => g.DonorId)).Distinct().ToList();
        var donors = await db.Donors.Include(d => d.Contact).Where(d => donorIds.Contains(d.Id)).ToListAsync(ct);
        return donors.Select(d =>
        {
            var gi = gifts.FirstOrDefault(x => x.Key == d.Id);
            var gr = grantRows.Where(x => x.DonorId == d.Id).ToList();
            return new DonorSummaryRow(d.Id, d.Contact!.Name, d.Type, gi?.Total ?? 0, gi?.Count ?? 0,
                gr.Sum(x => Math.Round(x.Amount * x.AgreementRate, 2)), gr.Sum(x => x.Received));
        }).OrderByDescending(r => r.Donations + r.GrantsReceived).ToList();
    }

    private async Task<(DateOnly Start, DateOnly End)> FiscalYearAsync(DateOnly date, CancellationToken ct)
    {
        var settings = await ledger.SettingsAsync(ct);
        var m = Math.Max(1, settings.FiscalYearStartMonth);
        var start = new DateOnly(date.Month >= m ? date.Year : date.Year - 1, m, 1);
        return (start, start.AddYears(1).AddDays(-1));
    }
}
