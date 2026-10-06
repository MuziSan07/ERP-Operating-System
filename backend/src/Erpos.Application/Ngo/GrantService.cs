using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Ngo;

/// <summary>
/// Grants: Proposal (budget and tranches drafted) → Active (approved; reporting schedule generated; tranches received into
/// deferred income; spending charged to budget lines) → Closed (all reports submitted; unspent money refunded, overspend
/// absorbed by unrestricted funds). Each grant has its own restricted fund.
/// </summary>
public class GrantService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, FundService funds)
{
    public async Task<List<GrantListItem>> ListAsync(GrantStatus? status, Guid? donorId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.GrantsView, ct);
        var q = db.Grants.Where(g => visible.Contains(g.EntityId));
        if (status != null) q = q.Where(g => g.Status == status);
        if (donorId != null) q = q.Where(g => g.DonorId == donorId);
        return await ListItemsAsync(q, ct);
    }

    internal async Task<List<GrantListItem>> ListItemsAsync(IQueryable<Grant> q, CancellationToken ct)
    {
        var grants = await q.Include(g => g.Donor).ThenInclude(d => d!.Contact).Include(g => g.BudgetLines).Include(g => g.Tranches).Include(g => g.Reports)
            .OrderByDescending(g => g.StartDate).Take(300).ToListAsync(ct);
        var ids = grants.Select(g => g.Id).ToList();
        var spent = await db.FundExpenses.Where(e => e.GrantId != null && ids.Contains(e.GrantId.Value)).GroupBy(e => e.GrantId)
            .Select(g => new { g.Key, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        return grants.Select(g =>
        {
            var amount = g.BudgetLines.Sum(l => l.Amount);
            var received = g.Tranches.Where(t => t.ReceivedAmount != null).ToList();
            var rate = received.Sum(t => t.ReceivedAmount!.Value) is > 0 and var ra ? received.Sum(t => t.ReceivedBase!.Value) / ra : g.AgreementRate;
            var spentBase = spent.FirstOrDefault(x => x.Key == g.Id)?.Total ?? 0;
            return new GrantListItem(g.Id, g.Number, g.Title, g.Donor!.Contact!.Name, g.Currency, amount, g.StartDate, g.EndDate, g.Status,
                received.Sum(t => t.ReceivedBase!.Value), spentBase, NgoCommon.Percent(spentBase / rate, amount), NgoCommon.TimeElapsed(g.StartDate, g.EndDate),
                g.Reports.Where(r => r.SubmittedOn == null).OrderBy(r => r.DueDate).Select(r => (DateOnly?)r.DueDate).FirstOrDefault());
        }).ToList();
    }

    public async Task<GrantDto> GetAsync(Guid id, CancellationToken ct)
    {
        var g = await db.Grants.Include(x => x.Entity).Include(x => x.Donor).ThenInclude(d => d!.Contact).Include(x => x.Program).Include(x => x.Fund)
                    .Include(x => x.BudgetLines).Include(x => x.Tranches).Include(x => x.Reports).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Grant");
        await access.EnsureAsync(Permissions.GrantsView, g.EntityId, ct);
        var today = NgoCommon.Today;
        var rate = await NgoCommon.AverageRateAsync(db, g, ct);
        var byLine = await db.FundExpenses.Where(e => e.GrantId == g.Id && e.BudgetLineId != null).GroupBy(e => e.BudgetLineId)
            .Select(x => new { x.Key, Total = x.Sum(e => e.Amount) }).ToListAsync(ct);
        var accountIds = g.BudgetLines.Where(l => l.ExpenseAccountId != null).Select(l => l.ExpenseAccountId!.Value).ToList();
        var accounts = await db.Accounts.Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => $"{a.Code} {a.Name}", ct);
        var lines = g.BudgetLines.OrderBy(l => l.SortOrder).Select(l =>
        {
            var actualBase = byLine.FirstOrDefault(x => x.Key == l.Id)?.Total ?? 0;
            var actual = Math.Round(actualBase / rate, 2);
            return new BudgetLineDto(l.Id, l.Code, l.Description, l.Category, l.Amount, l.ExpenseAccountId, l.ExpenseAccountId is { } a ? accounts.GetValueOrDefault(a) : null,
                actualBase, actual, l.Amount - actual, NgoCommon.Percent(actual, l.Amount), actual > l.Amount);
        }).ToList();
        var amount = g.BudgetLines.Sum(l => l.Amount);
        var receivedAmount = g.Tranches.Sum(t => t.ReceivedAmount ?? 0);
        var receivedBase = g.Tranches.Sum(t => t.ReceivedBase ?? 0);
        var spentBase = await db.FundExpenses.Where(e => e.GrantId == g.Id).SumAsync(e => e.Amount, ct);
        var spent = Math.Round(spentBase / rate, 2);
        var burn = NgoCommon.Percent(spent, amount);
        var elapsed = NgoCommon.TimeElapsed(g.StartDate, g.EndDate);

        var warnings = new List<string>();
        var trancheTotal = g.Tranches.Sum(t => t.Amount);
        if (g.Status is GrantStatus.Proposal or GrantStatus.Active && trancheTotal != amount)
            warnings.Add($"Tranches add up to {trancheTotal:N0} but the budget is {amount:N0} {g.Currency}.");
        foreach (var t in g.Tranches.Where(t => t.ReceivedDate == null && t.DueDate < today && g.Status == GrantStatus.Active))
            warnings.Add($"Tranche {t.Sequence} ({t.Amount:N0} {g.Currency}) was due {t.DueDate:dd MMM yyyy} and hasn't been received.");
        foreach (var r in g.Reports.Where(r => r.SubmittedOn == null && r.DueDate < today))
            warnings.Add($"{r.Title} was due {r.DueDate:dd MMM yyyy}.");
        foreach (var l in lines.Where(l => l.OverBudget))
            warnings.Add($"Budget line {l.Code} is over budget ({l.Actual:N0} of {l.Amount:N0}).");
        if (g.Status == GrantStatus.Active && elapsed - burn > 25)
            warnings.Add($"Spending is behind schedule: {burn:0.#}% spent with {elapsed:0.#}% of the grant period gone.");
        if (g.Status == GrantStatus.Active && spentBase > receivedBase)
            warnings.Add($"Spending is {spentBase - receivedBase:N0} ahead of money received (pre-financed from other funds).");
        if (g.Status == GrantStatus.Active && g.EndDate >= today && g.EndDate <= today.AddDays(60))
            warnings.Add($"The grant ends on {g.EndDate:dd MMM yyyy}.");

        var expenses = await funds.ExpenseItemsAsync(db.FundExpenses.Where(e => e.GrantId == g.Id).OrderByDescending(e => e.Date).ThenByDescending(e => e.Number), ct);
        return new GrantDto(g.Id, g.Number, g.EntityId, g.Entity!.Name, g.Title, g.DonorId, g.Donor!.Contact!.Name, g.AgreementRef, g.ProgramId, g.Program?.Name,
            g.FundId, g.Fund!.Code, g.Currency, g.AgreementRate, g.StartDate, g.EndDate, g.Status, g.ReportingFrequency, g.FlexibilityPercent, g.Notes,
            amount, receivedAmount, receivedBase, spentBase, spent, Math.Round(rate, 4), receivedBase - spentBase, burn, elapsed, lines,
            g.Tranches.OrderBy(t => t.Sequence).Select(t => new TrancheDto(t.Id, t.Sequence, t.DueDate, t.Amount, t.Condition, t.ReceivedDate, t.ReceivedAmount,
                t.ReceivedBase, t.ReceivedDate == null && t.DueDate < today)).ToList(),
            g.Reports.OrderBy(r => r.DueDate).Select(r => new GrantReportDto(r.Id, r.Title, r.PeriodStart, r.PeriodEnd, r.DueDate, r.SubmittedOn, r.Notes,
                r.SubmittedOn == null && r.DueDate < today)).ToList(),
            expenses, warnings);
    }

    public async Task<GrantDto> SaveAsync(Guid? id, SaveGrantRequest req, CancellationToken ct)
    {
        var settings = await ledger.SettingsAsync(ct);
        Grant g;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.GrantsCreate, req.EntityId, ct);
            g = new Grant { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Number = await ledger.NextNumberAsync("GRT", req.StartDate, ct) };
            db.Grants.Add(g);
        }
        else
        {
            g = await db.Grants.Include(x => x.BudgetLines).Include(x => x.Tranches).Include(x => x.Fund).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Grant");
            await access.EnsureAsync(Permissions.GrantsEdit, g.EntityId, ct);
            if (g.Status is not (GrantStatus.Proposal or GrantStatus.Active)) throw new ValidationException($"A {g.Status.ToString().ToLower()} grant can't be edited.");
            if (req.EntityId != g.EntityId)
            {
                if (g.Status != GrantStatus.Proposal) throw new ValidationException("An active grant can't move to another entity.");
                await access.EnsureAsync(Permissions.GrantsEdit, req.EntityId, ct);
                g.EntityId = req.EntityId;
            }
        }
        var donor = await db.Donors.Include(d => d.Contact).FirstOrDefaultAsync(d => d.Id == req.DonorId, ct) ?? throw new NotFoundException("Donor");
        if (req.EndDate <= req.StartDate) throw new ValidationException("The end date must be after the start date.");
        var currency = string.IsNullOrWhiteSpace(req.Currency) ? settings.BaseCurrency : req.Currency.Trim().ToUpperInvariant();
        if (currency.Length != 3) throw new ValidationException("Use a 3-letter currency code.");
        var rate = currency == settings.BaseCurrency ? 1 : req.AgreementRate ?? 0;
        if (rate <= 0) throw new ValidationException($"Enter the {currency} → {settings.BaseCurrency} rate in the agreement.");
        if (g.Status == GrantStatus.Active && (currency != g.Currency || req.StartDate != g.StartDate))
            throw new ValidationException("The currency and start date of an active grant can't change.");
        if (req.FlexibilityPercent is < 0 or > 100) throw new ValidationException("Flexibility must be between 0 and 100%.");
        if (req.BudgetLines.Count == 0) throw new ValidationException("Add the budget lines.");
        if (req.Tranches.Count == 0) throw new ValidationException("Add at least one tranche (instalment).");
        if (req.BudgetLines.Any(l => l.Amount <= 0) || req.Tranches.Any(t => t.Amount <= 0)) throw new ValidationException("Budget and tranche amounts must be positive.");
        var codes = req.BudgetLines.Select(l => Guard.Required(l.Code, "Budget line code", 20).ToUpperInvariant()).ToList();
        if (codes.Distinct().Count() != codes.Count) throw new ValidationException("Budget line codes must be unique.");
        var accountIds = req.BudgetLines.Where(l => l.ExpenseAccountId != null).Select(l => l.ExpenseAccountId!.Value).Distinct().ToList();
        if (await db.Accounts.CountAsync(a => accountIds.Contains(a.Id) && a.Type == AccountType.Expense && !a.IsGroup, ct) != accountIds.Count)
            throw new ValidationException("Budget lines must point to expense accounts.");

        g.Title = Guard.Required(req.Title, "Title", 200);
        g.DonorId = donor.Id;
        g.AgreementRef = req.AgreementRef;
        g.ProgramId = req.ProgramId;
        g.Currency = currency;
        g.AgreementRate = rate;
        g.StartDate = req.StartDate;
        g.EndDate = req.EndDate;
        g.ReportingFrequency = req.ReportingFrequency;
        g.FlexibilityPercent = req.FlexibilityPercent;
        g.Notes = req.Notes;

        // Budget lines: an active grant can't drop a line that has spending, or cut it below what was spent.
        var spentByLine = id == null ? new Dictionary<Guid, decimal>() : await db.FundExpenses.Where(e => e.GrantId == g.Id && e.BudgetLineId != null).GroupBy(e => e.BudgetLineId!.Value)
            .Select(x => new { x.Key, Total = x.Sum(e => e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var avg = id == null ? rate : await NgoCommon.AverageRateAsync(db, g, ct);
        foreach (var gone in g.BudgetLines.Where(l => req.BudgetLines.All(r => r.Id != l.Id)).ToList())
        {
            if (spentByLine.ContainsKey(gone.Id)) throw new ValidationException($"Budget line {gone.Code} has spending and can't be removed.");
            g.BudgetLines.Remove(gone);
        }
        for (var i = 0; i < req.BudgetLines.Count; i++)
        {
            var r = req.BudgetLines[i];
            var line = g.BudgetLines.FirstOrDefault(l => l.Id == r.Id);
            if (line == null) { line = new GrantBudgetLine { GrantId = g.Id }; g.BudgetLines.Add(line); }
            if (spentByLine.TryGetValue(line.Id, out var used) && r.Amount < Math.Round(used / avg, 2))
                throw new ValidationException($"Budget line {codes[i]} has already spent {used / avg:N0} {currency}.");
            line.Code = codes[i];
            line.Description = Guard.Required(r.Description, "Budget line description", 300);
            line.Category = r.Category;
            line.Amount = LedgerService.Round(r.Amount);
            line.ExpenseAccountId = r.ExpenseAccountId;
            line.SortOrder = i;
        }

        // Tranches: received ones are locked.
        foreach (var gone in g.Tranches.Where(t => req.Tranches.All(r => r.Id != t.Id)).ToList())
        {
            if (gone.ReceivedDate != null) throw new ValidationException($"Tranche {gone.Sequence} has been received and can't be removed.");
            g.Tranches.Remove(gone);
        }
        var seq = 1;
        foreach (var r in req.Tranches.OrderBy(t => t.DueDate))
        {
            var t = g.Tranches.FirstOrDefault(x => x.Id == r.Id);
            if (t == null) { t = new GrantTranche { GrantId = g.Id }; g.Tranches.Add(t); }
            if (t.ReceivedDate != null && (t.Amount != r.Amount || t.DueDate != r.DueDate)) throw new ValidationException($"Tranche {t.Sequence} has been received and can't change.");
            t.Sequence = seq++;
            t.DueDate = r.DueDate;
            t.Amount = LedgerService.Round(r.Amount);
            t.Condition = r.Condition;
        }

        var fundName = $"{g.Title} — {donor.Contact!.Name}";
        if (fundName.Length > 150) fundName = fundName[..150];
        if (g.Fund == null)
        {
            var fund = new Fund { TenantId = g.TenantId, Code = g.Number, Name = fundName, Kind = FundKind.Restricted, GrantId = g.Id, Purpose = $"Grant {g.Number}" };
            db.Funds.Add(fund);
            g.FundId = fund.Id;
        }
        else g.Fund.Name = fundName;
        await db.SaveChangesAsync(ct);
        return await GetAsync(g.Id, ct);
    }

    /// <summary>Approves a proposal: the tranches must add up to the budget; the reporting schedule is generated.</summary>
    public async Task<GrantDto> ActivateAsync(Guid id, CancellationToken ct)
    {
        var g = await db.Grants.Include(x => x.BudgetLines).Include(x => x.Tranches).Include(x => x.Reports).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Grant");
        await access.EnsureAsync(Permissions.GrantsApprove, g.EntityId, ct);
        if (g.Status != GrantStatus.Proposal) throw new ValidationException("Only proposals can be approved.");
        var budget = g.BudgetLines.Sum(l => l.Amount);
        var tranches = g.Tranches.Sum(t => t.Amount);
        if (budget != tranches) throw new ValidationException($"Tranches ({tranches:N0}) must add up to the budget ({budget:N0} {g.Currency}).");
        foreach (var r in Schedule(g)) g.Reports.Add(r);
        g.Status = GrantStatus.Active;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Periodic donor reports due 30 days after each period; the last one is the final report, due 60 days after the end.</summary>
    internal static List<GrantReport> Schedule(Grant g)
    {
        var months = g.ReportingFrequency switch
        {
            ReportingFrequency.Monthly => 1, ReportingFrequency.Quarterly => 3, ReportingFrequency.SemiAnnual => 6, ReportingFrequency.Annual => 12, _ => 0
        };
        var list = new List<GrantReport>();
        if (months > 0)
        {
            var start = g.StartDate;
            var n = 1;
            while (start <= g.EndDate)
            {
                var end = start.AddMonths(months).AddDays(-1);
                if (end >= g.EndDate) break;
                list.Add(new GrantReport { GrantId = g.Id, Title = $"{g.ReportingFrequency} report {n++}", PeriodStart = start, PeriodEnd = end, DueDate = end.AddDays(30) });
                start = end.AddDays(1);
            }
        }
        list.Add(new GrantReport { GrantId = g.Id, Title = "Final report", PeriodStart = g.StartDate, PeriodEnd = g.EndDate, DueDate = g.EndDate.AddDays(60) });
        return list;
    }

    public async Task<GrantDto> ReceiveTrancheAsync(Guid id, ReceiveTrancheRequest req, CancellationToken ct)
    {
        var g = await db.Grants.Include(x => x.Tranches).Include(x => x.Donor).Include(x => x.Fund).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Grant");
        await access.EnsureAsync(Permissions.GrantsEdit, g.EntityId, ct);
        if (g.Status != GrantStatus.Active) throw new ValidationException("Approve the grant before receiving money against it.");
        var t = g.Tranches.FirstOrDefault(x => x.Id == req.TrancheId) ?? throw new NotFoundException("Tranche");
        if (t.ReceivedDate != null) throw new ValidationException($"Tranche {t.Sequence} was already received.");
        if (req.Amount <= 0) throw new ValidationException("Amount must be positive.");
        var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.BankAccountId, ct) ?? throw new NotFoundException("Account");
        if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Receive into a bank account.");
        var settings = await ledger.SettingsAsync(ct);
        var rate = g.Currency == settings.BaseCurrency ? 1 : req.Rate ?? await ledger.RateAsync(g.Currency, req.Date, ct);
        if (rate <= 0) throw new ValidationException($"Enter the {g.Currency} rate.");
        var amount = LedgerService.Round(req.Amount);
        var text = $"{g.Number} tranche {t.Sequence} received — {g.Title}";
        var entry = await ledger.BuildAndPostAsync(g.EntityId, req.Date, text, g.Currency, rate, JournalSource.Ngo, g.Id,
            [new(bank.Id, amount, 0, null, text, g.Donor!.ContactId), new(await NgoCommon.AccountAsync(db, "2220", ct), 0, amount, null, text, g.Donor.ContactId)],
            g.Number, ct);
        t.ReceivedDate = req.Date;
        t.ReceivedAmount = amount;
        t.ReceivedBase = entry.Lines.Sum(l => l.BaseDebit);
        t.BankAccountId = bank.Id;
        t.JournalEntryId = entry.Id;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<GrantDto> SubmitReportAsync(Guid id, Guid reportId, SubmitReportRequest req, CancellationToken ct)
    {
        var g = await db.Grants.Include(x => x.Reports).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Grant");
        await access.EnsureAsync(Permissions.GrantsEdit, g.EntityId, ct);
        var r = g.Reports.FirstOrDefault(x => x.Id == reportId) ?? throw new NotFoundException("Report");
        r.SubmittedOn = req.SubmittedOn ?? NgoCommon.Today;
        r.Notes = req.Notes;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Closes an active grant: unspent money is refunded to the donor; overspend is taken back out of restricted income.</summary>
    public async Task<GrantDto> CloseAsync(Guid id, CloseGrantRequest req, CancellationToken ct)
    {
        var g = await db.Grants.Include(x => x.Reports).Include(x => x.Tranches).Include(x => x.Fund).Include(x => x.Donor).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Grant");
        await access.EnsureAsync(Permissions.GrantsApprove, g.EntityId, ct);
        if (g.Status != GrantStatus.Active) throw new ValidationException("Only active grants can be closed.");
        var pending = g.Reports.Count(r => r.SubmittedOn == null);
        if (pending > 0) throw new ValidationException($"Submit the remaining donor reports first ({pending} outstanding).");
        var date = req.Date ?? NgoCommon.Today;
        var settings = await ledger.SettingsAsync(ct);
        var received = g.Tranches.Sum(t => t.ReceivedBase ?? 0);
        var spent = await db.FundExpenses.Where(e => e.GrantId == g.Id).SumAsync(e => e.Amount, ct);
        var unspent = LedgerService.Round(received - spent);
        var deferred = await NgoCommon.AccountAsync(db, "2220", ct);
        if (unspent > 0)
        {
            if (req.RefundFromAccountId is not { } bank) throw new ValidationException($"{unspent:N0} is unspent; choose the bank account to refund the donor from.");
            var text = $"{g.Number} closed — unspent balance refunded to donor";
            await ledger.BuildAndPostAsync(g.EntityId, date, text, settings.BaseCurrency, 1, JournalSource.Ngo, g.Id,
                [new(deferred, unspent, 0, null, text, g.Donor!.ContactId), new(bank, 0, unspent, null, text, g.Donor.ContactId)], g.Number, ct);
        }
        else if (unspent < 0)
        {
            var text = $"{g.Number} closed — overspend not funded by the donor, borne by unrestricted funds";
            await ledger.BuildAndPostAsync(g.EntityId, date, text, settings.BaseCurrency, 1, JournalSource.Ngo, g.Id,
                [new(await NgoCommon.AccountAsync(db, "4310", ct), -unspent, 0, null, text), new(deferred, 0, -unspent, null, text)], g.Number, ct);
        }
        g.Status = GrantStatus.Closed;
        g.Fund!.IsActive = false;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<GrantDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var g = await db.Grants.Include(x => x.Fund).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Grant");
        await access.EnsureAsync(Permissions.GrantsEdit, g.EntityId, ct);
        if (g.Status != GrantStatus.Proposal) throw new ValidationException("Only proposals can be cancelled; close an active grant instead.");
        g.Status = GrantStatus.Cancelled;
        g.Fund!.IsActive = false;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }
}
