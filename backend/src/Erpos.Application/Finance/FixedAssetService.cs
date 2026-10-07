using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Fixed asset register: acquisition (already in the books, paid now, or on a vendor bill), monthly depreciation runs
/// (straight line or reducing balance, full-month convention, missed months caught up, never below salvage value),
/// disposal with gain or loss, and the movement schedule for the financial statements.
/// </summary>
public class FixedAssetService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents)
{
    private static int Period(int year, int month) => year * 100 + month;
    private static int Period(DateOnly d) => d.Year * 100 + d.Month;
    private static DateOnly MonthEnd(int period) => new DateOnly(period / 100, period % 100, 1).AddMonths(1).AddDays(-1);
    private static int Next(int period) => period % 100 == 12 ? (period / 100 + 1) * 100 + 1 : period + 1;
    private static string? Label(int period) => period == 0 ? null : new DateOnly(period / 100, period % 100, 1).ToString("MMM yyyy");

    private async Task<Guid> AccountAsync(string code, CancellationToken ct) =>
        await db.Accounts.Where(a => a.Code == code && !a.IsGroup).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw new ValidationException($"Account {code} is missing from the chart of accounts.");

    // ---------------- Categories ----------------

    public async Task<List<AssetCategoryDto>> CategoriesAsync(CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.AssetsView, ct);
        return await db.AssetCategories.OrderBy(c => c.Name)
            .Select(c => new AssetCategoryDto(c.Id, c.Code, c.Name, c.Method, c.UsefulLifeMonths, c.ReducingRate, c.AssetAccountId, c.AccumulatedAccountId,
                c.ExpenseAccountId, c.IsActive, db.FixedAssets.Count(a => a.CategoryId == c.Id && a.Status != AssetStatus.Disposed))).ToListAsync(ct);
    }

    public async Task<AssetCategoryDto> SaveCategoryAsync(Guid? id, SaveAssetCategoryRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.FinanceSettingsManage, ct);
        var c = id == null ? null : await db.AssetCategories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Category");
        if (c == null) { c = new AssetCategory { TenantId = currentUser.TenantId!.Value }; db.AssetCategories.Add(c); }
        var code = Guard.Code(req.Code);
        if (await db.AssetCategories.AnyAsync(x => x.Code == code && x.Id != c.Id, ct)) throw new ValidationException($"Category {code} exists.");
        Validate(req.Method, req.UsefulLifeMonths, req.ReducingRate);
        c.Code = code;
        c.Name = Guard.Required(req.Name, "Name", 100);
        c.Method = req.Method;
        c.UsefulLifeMonths = req.UsefulLifeMonths;
        c.ReducingRate = req.ReducingRate;
        c.AssetAccountId = req.AssetAccountId ?? await AccountAsync("1510", ct);
        c.AccumulatedAccountId = req.AccumulatedAccountId ?? await AccountAsync("1520", ct);
        c.ExpenseAccountId = req.ExpenseAccountId ?? await AccountAsync("6700", ct);
        c.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await CategoriesAsync(ct)).First(x => x.Id == c.Id);
    }

    private static void Validate(DepreciationMethod method, int life, decimal rate)
    {
        if (method == DepreciationMethod.StraightLine && life is < 1 or > 1200) throw new ValidationException("Useful life must be between 1 and 1,200 months.");
        if (method == DepreciationMethod.ReducingBalance && rate is <= 0 or >= 1) throw new ValidationException("Enter the reducing-balance rate as a fraction, e.g. 0.15.");
    }

    // ---------------- Assets ----------------

    public async Task<List<AssetListItem>> ListAsync(AssetStatus? status, Guid? categoryId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.AssetsView, ct);
        if (visible.Count == 0) throw new ForbiddenException();
        var q = db.FixedAssets.Where(a => visible.Contains(a.EntityId));
        if (status != null) q = q.Where(a => a.Status == status);
        if (categoryId != null) q = q.Where(a => a.CategoryId == categoryId);
        var rows = await q.OrderBy(a => a.Code).Select(a => new { a.Id, a.Code, a.Name, Category = a.Category!.Name, Entity = a.Entity!.Name, a.AcquisitionDate, a.Cost,
            a.AccumulatedDepreciation, a.Status, a.Location, a.DepreciatedThrough }).ToListAsync(ct);
        return rows.Select(a => new AssetListItem(a.Id, a.Code, a.Name, a.Category, a.Entity, a.AcquisitionDate, a.Cost, a.AccumulatedDepreciation,
            a.Status == AssetStatus.Disposed ? 0 : a.Cost - a.AccumulatedDepreciation, a.Status, a.Location, Label(a.DepreciatedThrough))).ToList();
    }

    public async Task<AssetDto> GetAsync(Guid id, CancellationToken ct)
    {
        var a = await db.FixedAssets.Include(x => x.Entity).Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Asset");
        await access.EnsureAsync(Permissions.AssetsView, a.EntityId, ct);
        var runs = await db.DepreciationRuns.Include(r => r.Lines).Where(r => r.Lines.Any(l => l.AssetId == id)).ToListAsync(ct);
        var history = runs.SelectMany(r => r.Lines.Where(l => l.AssetId == id).Select(l => new AssetDepreciationRow(r.Year, r.Month, l.Months, l.Amount)))
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ToList();
        return new AssetDto(a.Id, a.Code, a.Name, a.EntityId, a.Entity!.Name, a.CategoryId, a.Category!.Name, a.SerialNo, a.Location, a.AcquisitionDate,
            a.DepreciationStart, a.Cost, a.SalvageValue, a.Method, a.UsefulLifeMonths, a.ReducingRate, a.AccumulatedDepreciation,
            a.Status == AssetStatus.Disposed ? 0 : a.Cost - a.AccumulatedDepreciation, a.Status == AssetStatus.Active ? MonthlyCharge(a, a.AccumulatedDepreciation) : 0,
            Label(a.DepreciatedThrough), a.Status, a.DisposalDate, a.DisposalProceeds, a.Notes, history);
    }

    /// <summary>One month's charge given the depreciation accumulated so far; never takes book value below salvage.</summary>
    internal static decimal MonthlyCharge(FixedAsset a, decimal accumulated)
    {
        var depreciable = a.Cost - a.SalvageValue - accumulated;
        if (depreciable <= 0) return 0;
        var charge = a.Method == DepreciationMethod.StraightLine
            ? (a.Cost - a.SalvageValue) / a.UsefulLifeMonths
            : (a.Cost - accumulated) * a.ReducingRate / 12;
        return LedgerService.Round(Math.Min(charge, depreciable));
    }

    public async Task<AssetDto> RegisterAsync(RegisterAssetRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.AssetsCreate, req.EntityId, ct);
        var cat = await db.AssetCategories.FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct) ?? throw new NotFoundException("Category");
        if (req.Cost <= 0) throw new ValidationException("Enter the cost.");
        if (req.SalvageValue < 0 || req.SalvageValue >= req.Cost) throw new ValidationException("Salvage value must be less than the cost.");
        var method = req.Method ?? cat.Method;
        var life = req.UsefulLifeMonths ?? cat.UsefulLifeMonths;
        var rate = req.ReducingRate ?? cat.ReducingRate;
        Validate(method, life, rate);
        var start = req.DepreciationStart ?? req.AcquisitionDate;
        start = new DateOnly(start.Year, start.Month, 1);
        if (start < new DateOnly(req.AcquisitionDate.Year, req.AcquisitionDate.Month, 1)) throw new ValidationException("Depreciation can't start before the asset was acquired.");

        var a = new FixedAsset
        {
            TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Code = $"FA-{await db.NextSequenceAsync("FA", ct):D5}",
            Name = Guard.Required(req.Name, "Name", 150), CategoryId = cat.Id, SerialNo = req.SerialNo, Location = req.Location, AcquisitionDate = req.AcquisitionDate,
            DepreciationStart = start, Cost = LedgerService.Round(req.Cost), SalvageValue = LedgerService.Round(req.SalvageValue), Method = method,
            UsefulLifeMonths = life, ReducingRate = rate, Notes = req.Notes
        };
        var settings = await ledger.SettingsAsync(ct);
        var text = $"{a.Code} {a.Name} acquired";
        switch (req.Acquisition)
        {
            case AssetAcquisition.AlreadyInBooks:
                // Taken on from existing records: cost and any depreciation to date are already in the ledger.
                if (req.OpeningAccumulatedDepreciation < 0 || req.OpeningAccumulatedDepreciation > a.Cost - a.SalvageValue)
                    throw new ValidationException("Opening depreciation must be between zero and cost less salvage value.");
                a.AccumulatedDepreciation = LedgerService.Round(req.OpeningAccumulatedDepreciation);
                if (a.AccumulatedDepreciation > 0)
                {
                    var through = req.DepreciatedThrough ?? throw new ValidationException("Say up to which month the opening depreciation was charged.");
                    if (Period(through) < Period(start)) throw new ValidationException("Opening depreciation can't end before depreciation started.");
                    a.DepreciatedThrough = Period(through);
                }
                break;
            case AssetAcquisition.PaidNow:
                if (req.PaidFromAccountId is not { } bankId) throw new ValidationException("Choose the account it was paid from.");
                var bank = await db.Accounts.FirstOrDefaultAsync(x => x.Id == bankId, ct) ?? throw new NotFoundException("Account");
                if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Pay from a bank or cash account.");
                var entry = await ledger.BuildAndPostAsync(req.EntityId, req.AcquisitionDate, text, settings.BaseCurrency, 1, JournalSource.Manual, a.Id,
                    [new(cat.AssetAccountId, a.Cost, 0, null, text), new(bank.Id, 0, a.Cost, null, text)], a.Code, ct);
                a.AcquisitionJournalId = entry.Id;
                break;
            case AssetAcquisition.OnCredit:
                if (req.VendorId is not { } vendorId || !await db.Contacts.AnyAsync(c => c.Id == vendorId && c.IsVendor, ct)) throw new ValidationException("Choose the vendor.");
                var bill = await documents.CreateAsync(DocumentKind.Bill, new SaveDocumentRequest(req.EntityId, vendorId, req.AcquisitionDate, null, a.Code, text,
                    settings.BaseCurrency, 1, [new DocumentLineInput($"{a.Name}{(a.SerialNo == null ? "" : $" (S/N {a.SerialNo})")}", cat.AssetAccountId, 1, a.Cost, null)]), ct, system: true);
                await documents.ApproveAsync(bill.Id, ct, system: true);
                a.AcquisitionBillId = bill.Id;
                break;
        }
        if (a.Cost - a.SalvageValue - a.AccumulatedDepreciation <= 0) a.Status = AssetStatus.FullyDepreciated;
        db.FixedAssets.Add(a);
        await db.SaveChangesAsync(ct);
        return await GetAsync(a.Id, ct);
    }

    public async Task<AssetDto> UpdateAsync(Guid id, UpdateAssetRequest req, CancellationToken ct)
    {
        var a = await db.FixedAssets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Asset");
        await access.EnsureAsync(Permissions.AssetsEdit, a.EntityId, ct);
        a.Name = Guard.Required(req.Name, "Name", 150);
        a.SerialNo = req.SerialNo;
        a.Location = req.Location;
        a.Notes = req.Notes;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---------------- Depreciation ----------------

    /// <summary>Charges every active asset of the entity up to the given month (catching up missed months) in one journal.</summary>
    public async Task<DepreciationRunDto> RunAsync(RunDepreciationRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.AssetsDepreciate, req.EntityId, ct);
        if (req.Month is < 1 or > 12) throw new ValidationException("Invalid month.");
        var period = Period(req.Year, req.Month);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        if (period > Period(today)) throw new ValidationException("Depreciation can't be run for a future month.");
        await db.LockAsync<BusinessEntity>(req.EntityId, ct);
        if (await db.DepreciationRuns.AnyAsync(r => r.EntityId == req.EntityId && r.Year == req.Year && r.Month == req.Month, ct))
            throw new ValidationException($"Depreciation for {Label(period)} has already been posted.");

        var assets = await db.FixedAssets.Include(a => a.Category)
            .Where(a => a.EntityId == req.EntityId && a.Status == AssetStatus.Active && a.DepreciatedThrough < period).ToListAsync(ct);
        var run = new DepreciationRun { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Year = req.Year, Month = req.Month };
        foreach (var a in assets)
        {
            var from = a.DepreciatedThrough == 0 ? Period(a.DepreciationStart) : Next(a.DepreciatedThrough);
            if (from > period) continue;
            decimal total = 0;
            var months = 0;
            for (var p = from; p <= period; p = Next(p))
            {
                var charge = MonthlyCharge(a, a.AccumulatedDepreciation + total);
                if (charge <= 0) break;
                total += charge;
                months++;
            }
            if (total <= 0) { a.Status = AssetStatus.FullyDepreciated; continue; }
            a.AccumulatedDepreciation += total;
            a.DepreciatedThrough = period;
            if (a.Cost - a.SalvageValue - a.AccumulatedDepreciation <= 0) a.Status = AssetStatus.FullyDepreciated;
            run.Lines.Add(new DepreciationLine { RunId = run.Id, AssetId = a.Id, Months = months, Amount = total });
        }
        if (run.Lines.Count == 0) throw new ValidationException($"No asset has depreciation due for {Label(period)}.");
        run.Total = run.Lines.Sum(l => l.Amount);

        var settings = await ledger.SettingsAsync(ct);
        var text = $"Depreciation for {Label(period)}";
        var byAsset = assets.ToDictionary(a => a.Id);
        var lines = run.Lines.GroupBy(l => (byAsset[l.AssetId].Category!.ExpenseAccountId, byAsset[l.AssetId].Category!.AccumulatedAccountId))
            .SelectMany(g => new[]
            {
                new LineInput(g.Key.ExpenseAccountId, g.Sum(l => l.Amount), 0, null, text),
                new LineInput(g.Key.AccumulatedAccountId, 0, g.Sum(l => l.Amount), null, text)
            }).ToList();
        var entry = await ledger.BuildAndPostAsync(req.EntityId, req.PostingDate ?? MonthEnd(period), text, settings.BaseCurrency, 1, JournalSource.Manual, run.Id,
            lines, $"DEP-{period}", ct);
        run.JournalEntryId = entry.Id;
        db.DepreciationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return await RunDtoAsync(run.Id, ct);
    }

    public async Task<List<DepreciationRunDto>> RunsAsync(CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.AssetsView, ct);
        var ids = await db.DepreciationRuns.Where(r => visible.Contains(r.EntityId)).OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => r.Id).Take(60).ToListAsync(ct);
        var list = new List<DepreciationRunDto>();
        foreach (var id in ids) list.Add(await RunDtoAsync(id, ct));
        return list;
    }

    private async Task<DepreciationRunDto> RunDtoAsync(Guid id, CancellationToken ct)
    {
        var r = await db.DepreciationRuns.Include(x => x.Lines).FirstAsync(x => x.Id == id, ct);
        var assetIds = r.Lines.Select(l => l.AssetId).ToList();
        var assets = await db.FixedAssets.Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => (a.Code, a.Name), ct);
        var number = await db.JournalEntries.Where(j => j.Id == r.JournalEntryId).Select(j => j.Number).FirstOrDefaultAsync(ct);
        return new DepreciationRunDto(r.Id, r.Year, r.Month, r.Total, r.Lines.Count, number,
            r.Lines.Select(l => new DepreciationRunLine(l.AssetId, assets[l.AssetId].Code, assets[l.AssetId].Name, l.Months, l.Amount)).OrderBy(l => l.AssetCode).ToList());
    }

    // ---------------- Disposal ----------------

    /// <summary>Sells or scraps an asset: removes cost and accumulated depreciation; the difference to the proceeds is a gain or loss.</summary>
    public async Task<AssetDto> DisposeAsync(Guid id, DisposeAssetRequest req, CancellationToken ct)
    {
        var a = await db.FixedAssets.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Asset");
        await access.EnsureAsync(Permissions.AssetsEdit, a.EntityId, ct);
        if (a.Status == AssetStatus.Disposed) throw new ValidationException("Already disposed.");
        if (req.Date < a.AcquisitionDate) throw new ValidationException("Disposal can't be before acquisition.");
        if (req.Proceeds < 0) throw new ValidationException("Proceeds can't be negative.");
        // Depreciation must be up to date: charged through the month before disposal (or the disposal month itself).
        var due = MonthlyCharge(a, a.AccumulatedDepreciation) > 0 && Period(a.DepreciationStart) < Period(req.Date)
                  && a.DepreciatedThrough < Period(req.Date.AddMonths(-1));
        if (a.Status == AssetStatus.Active && due)
            throw new ValidationException($"Run depreciation up to {Label(Period(req.Date.AddMonths(-1)))} before disposing (charged through {Label(a.DepreciatedThrough) ?? "never"}).");
        if (req.Proceeds > 0 && req.ReceivedIntoAccountId == null) throw new ValidationException("Choose the account the sale proceeds went into.");

        var settings = await ledger.SettingsAsync(ct);
        var proceeds = LedgerService.Round(req.Proceeds);
        var gain = proceeds - (a.Cost - a.AccumulatedDepreciation);
        var text = $"{a.Code} {a.Name} disposed{(string.IsNullOrWhiteSpace(req.Reason) ? "" : $" — {req.Reason}")}";
        var lines = new List<LineInput> { new(a.Category!.AssetAccountId, 0, a.Cost, null, text) };
        if (a.AccumulatedDepreciation > 0) lines.Add(new LineInput(a.Category.AccumulatedAccountId, a.AccumulatedDepreciation, 0, null, text));
        if (proceeds > 0) lines.Add(new LineInput(req.ReceivedIntoAccountId!.Value, proceeds, 0, null, text));
        var gainAccount = await AccountAsync("4920", ct);
        if (gain > 0) lines.Add(new LineInput(gainAccount, 0, gain, null, "Gain on disposal"));
        else if (gain < 0) lines.Add(new LineInput(gainAccount, -gain, 0, null, "Loss on disposal"));
        var entry = await ledger.BuildAndPostAsync(a.EntityId, req.Date, text, settings.BaseCurrency, 1, JournalSource.Manual, a.Id, lines, a.Code, ct);
        a.Status = AssetStatus.Disposed;
        a.DisposalDate = req.Date;
        a.DisposalProceeds = proceeds;
        a.DisposalJournalId = entry.Id;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---------------- Schedule ----------------

    /// <summary>Movement schedule (IAS 16 note): cost and depreciation brought forward, additions, charge, disposals, carried forward.</summary>
    public async Task<AssetScheduleDto> ScheduleAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.AssetsView, ct);
        if (visible.Count == 0) throw new ForbiddenException();
        var assets = await db.FixedAssets.Where(a => visible.Contains(a.EntityId) && a.AcquisitionDate <= to)
            .Select(a => new { a.Id, Category = a.Category!.Name, a.AcquisitionDate, a.Cost, a.DisposalDate, a.Status, Opening = a.DepreciatedThrough }).ToListAsync(ct);
        var ids = assets.Select(a => a.Id).ToList();
        var fromP = Period(from);
        var toP = Period(to);
        var runs = await db.DepreciationRuns.Include(r => r.Lines).Where(r => visible.Contains(r.EntityId)).ToListAsync(ct);
        var allRuns = runs.SelectMany(r => r.Lines.Select(l => new { l.AssetId, P = r.Year * 100 + r.Month, l.Amount })).Where(x => ids.Contains(x.AssetId)).ToList();
        var charges = allRuns.Where(x => x.P <= toP).ToList();
        // Depreciation taken on from earlier records (not in any run) counts as brought forward.
        var openingTaken = await db.FixedAssets.Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.AccumulatedDepreciation }).ToDictionaryAsync(a => a.Id, a => a.AccumulatedDepreciation, ct);
        var rows = assets.GroupBy(a => a.Category).OrderBy(g => g.Key).Select(g =>
        {
            decimal oc = 0, add = 0, disp = 0, od = 0, chg = 0, dd = 0;
            foreach (var a in g)
            {
                var takenOn = openingTaken[a.Id] - allRuns.Where(x => x.AssetId == a.Id).Sum(x => x.Amount);
                var before = takenOn + charges.Where(c => c.AssetId == a.Id && c.P < fromP).Sum(c => c.Amount);
                var during = charges.Where(c => c.AssetId == a.Id && c.P >= fromP).Sum(c => c.Amount);
                var disposedBefore = a.DisposalDate is { } d0 && d0 < from;
                if (disposedBefore) continue;
                if (a.AcquisitionDate < from) { oc += a.Cost; od += before; } else add += a.Cost;
                chg += during;
                if (a.DisposalDate is { } d && d <= to) { disp += a.Cost; dd += before + during; }
            }
            return new AssetScheduleRow(g.Key, oc, add, disp, oc + add - disp, od, chg, dd, od + chg - dd, oc + add - disp - (od + chg - dd));
        }).ToList();
        var total = new AssetScheduleRow("Total", rows.Sum(r => r.OpeningCost), rows.Sum(r => r.Additions), rows.Sum(r => r.Disposals), rows.Sum(r => r.ClosingCost),
            rows.Sum(r => r.OpeningDepreciation), rows.Sum(r => r.Charge), rows.Sum(r => r.DepreciationOnDisposals), rows.Sum(r => r.ClosingDepreciation),
            rows.Sum(r => r.ClosingBookValue));
        return new AssetScheduleDto(from, to, rows, total);
    }
}
