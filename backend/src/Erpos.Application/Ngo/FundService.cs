using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Ngo;

/// <summary>
/// Fund accounting. Receipts into restricted and Zakat funds wait in deferred income (2220 / 2230) and are released to
/// income (4310) as the money is spent; unrestricted donations are income (4300) at once; endowments are capital (3300).
/// </summary>
public class FundService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents)
{
    // ---------------- Funds ----------------

    public async Task<List<FundDto>> FundsAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        if (!mine.ByEntity.Values.Any(s => s.Any(p => p.StartsWith("ngo.")))) throw new ForbiddenException();
        var funds = await db.Funds.OrderBy(f => f.Kind).ThenBy(f => f.Code).ToListAsync(ct);
        var balances = await BalancesAsync(ct);
        var grantNumbers = await db.Grants.Select(g => new { g.Id, g.Number }).ToDictionaryAsync(g => g.Id, g => g.Number, ct);
        return funds.Select(f =>
        {
            var (received, spent) = balances.GetValueOrDefault(f.Id);
            return new FundDto(f.Id, f.Code, f.Name, f.Kind, f.Purpose, f.GrantId, f.GrantId is { } g ? grantNumbers.GetValueOrDefault(g) : null, f.IsActive,
                received, spent, received - spent);
        }).ToList();
    }

    /// <summary>Received (donations + grant tranches, base currency) and spent per fund.</summary>
    internal async Task<Dictionary<Guid, (decimal Received, decimal Spent)>> BalancesAsync(CancellationToken ct)
    {
        var donations = await db.Donations.GroupBy(d => d.FundId).Select(g => new { g.Key, Total = g.Sum(d => d.Amount) }).ToListAsync(ct);
        var tranches = await db.GrantTranches.Join(db.Grants, t => t.GrantId, g => g.Id, (t, g) => new { t, g.FundId }).Where(x => x.t.ReceivedBase != null)
            .GroupBy(x => x.FundId).Select(g => new { g.Key, Total = g.Sum(x => x.t.ReceivedBase!.Value) }).ToListAsync(ct);
        var spent = await db.FundExpenses.GroupBy(e => e.FundId).Select(g => new { g.Key, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        return donations.Select(x => x.Key).Concat(tranches.Select(x => x.Key)).Concat(spent.Select(x => x.Key)).Distinct()
            .ToDictionary(id => id, id => (
                (donations.FirstOrDefault(x => x.Key == id)?.Total ?? 0) + (tranches.FirstOrDefault(x => x.Key == id)?.Total ?? 0),
                spent.FirstOrDefault(x => x.Key == id)?.Total ?? 0));
    }

    private async Task<decimal> AvailableAsync(Guid fundId, CancellationToken ct) =>
        (await BalancesAsync(ct)).TryGetValue(fundId, out var b) ? b.Received - b.Spent : 0;

    public async Task<FundDto> SaveFundAsync(Guid? id, SaveFundRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.FundsManage, ct);
        var f = id == null ? null : await db.Funds.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Fund");
        if (f == null) { f = new Fund { TenantId = currentUser.TenantId!.Value }; db.Funds.Add(f); }
        else
        {
            if (f.GrantId != null) throw new ValidationException("A grant's fund is managed from the grant.");
            if (f.Kind != req.Kind && (await db.Donations.AnyAsync(d => d.FundId == f.Id, ct) || await db.FundExpenses.AnyAsync(e => e.FundId == f.Id, ct)))
                throw new ValidationException("The fund already has transactions; its kind can't change.");
        }
        var code = Guard.Code(req.Code);
        if (await db.Funds.AnyAsync(x => x.Code == code && x.Id != f.Id, ct)) throw new ValidationException($"Fund {code} exists.");
        f.Code = code;
        f.Name = Guard.Required(req.Name, "Name", 150);
        f.Kind = req.Kind;
        f.Purpose = req.Purpose;
        f.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await FundsAsync(ct)).First(x => x.Id == f.Id);
    }

    // ---------------- Donations ----------------

    public async Task<PagedResult<DonationListItem>> DonationsAsync(Guid? donorId, Guid? fundId, DateOnly? from, DateOnly? to, string? search, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.DonationsView, ct);
        var q = db.Donations.Where(d => visible.Contains(d.EntityId));
        if (donorId != null) q = q.Where(d => d.DonorId == donorId);
        if (fundId != null) q = q.Where(d => d.FundId == fundId);
        if (from != null) q = q.Where(d => d.Date >= from);
        if (to != null) q = q.Where(d => d.Date <= to);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(d => d.Number.Contains(search) || d.Donor!.Contact!.Name.Contains(search) || (d.Reference != null && d.Reference.Contains(search)));
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(d => d.Date).ThenByDescending(d => d.Number).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
            .Select(d => new DonationListItem(d.Id, d.Number, d.Date, d.Donor!.Contact!.Name, d.Fund!.Code, d.Fund.Kind, d.Amount, d.Method, d.Reference)).ToListAsync(ct);
        return new PagedResult<DonationListItem>(items, total, page, pageSize);
    }

    public async Task<DonationDto> GetDonationAsync(Guid id, CancellationToken ct)
    {
        var d = await db.Donations.Include(x => x.Donor).ThenInclude(x => x!.Contact).Include(x => x.Fund).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Donation");
        await access.EnsureAsync(Permissions.DonationsView, d.EntityId, ct);
        var entity = await db.Entities.FirstAsync(e => e.Id == d.EntityId, ct);
        var org = await db.Entities.Where(e => e.ParentId == null).Select(e => e.Name).FirstOrDefaultAsync(ct) ?? entity.Name;
        var bank = await db.Accounts.Where(a => a.Id == d.BankAccountId).Select(a => a.Name).FirstAsync(ct);
        var program = d.ProgramId == null ? null : await db.NgoPrograms.Where(p => p.Id == d.ProgramId).Select(p => p.Name).FirstOrDefaultAsync(ct);
        var settings = await ledger.SettingsAsync(ct);
        var c = d.Donor!.Contact!;
        return new DonationDto(d.Id, d.Number, d.EntityId, entity.Name, d.Date, d.DonorId, c.Name, c.Cnic, c.Ntn, c.Address, d.FundId, d.Fund!.Name, d.Fund.Kind,
            d.Amount, NgoCommon.InWords(d.Amount, settings.BaseCurrency), d.Method, d.Reference, bank, program, d.Notes, org);
    }

    public async Task<DonationDto> RecordDonationAsync(RecordDonationRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.DonationsCreate, req.EntityId, ct);
        if (req.Amount <= 0) throw new ValidationException("Amount must be positive.");
        var donor = await db.Donors.Include(x => x.Contact).FirstOrDefaultAsync(x => x.Id == req.DonorId, ct) ?? throw new NotFoundException("Donor");
        var fund = await db.Funds.FirstOrDefaultAsync(x => x.Id == req.FundId, ct) ?? throw new NotFoundException("Fund");
        if (!fund.IsActive) throw new ValidationException($"{fund.Code} is closed.");
        if (fund.GrantId != null) throw new ValidationException("Grant money is recorded by receiving a tranche on the grant.");
        var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.BankAccountId, ct) ?? throw new NotFoundException("Account");
        if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Receive into a bank or cash account.");
        if (req.Method == DonationMethod.Cash && bank.SubType != AccountSubType.Cash) throw new ValidationException("Cash donations go into a cash account.");

        var settings = await ledger.SettingsAsync(ct);
        var credit = await NgoCommon.AccountAsync(db, NgoCommon.DeferredAccount(fund.Kind) ?? (fund.Kind == FundKind.Endowment ? "3300" : "4300"), ct);
        var d = new Donation
        {
            TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Number = await ledger.NextNumberAsync("DON", req.Date, ct), Date = req.Date,
            DonorId = donor.Id, FundId = fund.Id, Amount = LedgerService.Round(req.Amount), Method = req.Method, Reference = req.Reference,
            BankAccountId = bank.Id, ProgramId = req.ProgramId, Notes = req.Notes
        };
        var text = $"Donation {d.Number} — {donor.Contact!.Name} ({fund.Code})";
        var entry = await ledger.BuildAndPostAsync(req.EntityId, req.Date, text, settings.BaseCurrency, 1, JournalSource.Ngo, d.Id,
            [new(bank.Id, d.Amount, 0, null, text, donor.ContactId), new(credit, 0, d.Amount, null, text, donor.ContactId)], d.Number, ct);
        d.JournalEntryId = entry.Id;
        db.Donations.Add(d);
        await db.SaveChangesAsync(ct);
        return await GetDonationAsync(d.Id, ct);
    }

    // ---------------- Spending ----------------

    public async Task<List<FundExpenseDto>> ExpensesAsync(Guid? fundId, Guid? grantId, Guid? programId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.FundExpensesView, ct);
        var grantVisible = await access.EntitiesWithAsync(Permissions.GrantsView, ct);
        var q = db.FundExpenses.Where(e => visible.Contains(e.EntityId) || (e.GrantId != null && grantVisible.Contains(e.EntityId)));
        if (fundId != null) q = q.Where(e => e.FundId == fundId);
        if (grantId != null) q = q.Where(e => e.GrantId == grantId);
        if (programId != null) q = q.Where(e => e.ProgramId == programId);
        return await ExpenseItemsAsync(q.OrderByDescending(e => e.Date).ThenByDescending(e => e.Number).Take(500), ct);
    }

    internal async Task<List<FundExpenseDto>> ExpenseItemsAsync(IQueryable<FundExpense> q, CancellationToken ct) =>
        await q.Select(e => new FundExpenseDto(e.Id, e.Number, e.Date, e.Fund!.Code,
            db.Grants.Where(g => g.Id == e.GrantId).Select(g => g.Number).FirstOrDefault(),
            db.GrantBudgetLines.Where(l => l.Id == e.BudgetLineId).Select(l => l.Code).FirstOrDefault(),
            e.Program == null ? null : e.Program.Name, e.Function,
            db.Accounts.Where(a => a.Id == e.AccountId).Select(a => a.Code + " " + a.Name).First(), e.Description, e.Amount,
            e.AllocationOnly ? "Allocation" : e.VendorId != null ? "Vendor bill" : "Paid", e.BillId, e.AssistanceId)).ToListAsync(ct);

    public async Task<FundExpenseDto> ChargeAsync(ChargeExpenseRequest req, CancellationToken ct)
    {
        var e = await ChargeInternalAsync(req, null, ct);
        await db.SaveChangesAsync(ct);
        return (await ExpenseItemsAsync(db.FundExpenses.Where(x => x.Id == e.Id), ct)).First();
    }

    /// <summary>
    /// Validates and posts a fund expense (caller saves). Grants: active, within the grant period, on a budget line and
    /// within the line's flexibility. Restricted/Zakat funds can't go below zero; Zakat only reaches eligible beneficiaries.
    /// </summary>
    internal async Task<FundExpense> ChargeInternalAsync(ChargeExpenseRequest req, Beneficiary? beneficiary, CancellationToken ct)
    {
        Grant? grant = null;
        GrantBudgetLine? line = null;
        Fund fund;
        Guid entityId;
        if (req.GrantId is { } gid)
        {
            grant = await db.Grants.Include(g => g.BudgetLines).Include(g => g.Fund).FirstOrDefaultAsync(g => g.Id == gid, ct) ?? throw new NotFoundException("Grant");
            fund = grant.Fund!;
            entityId = grant.EntityId;
        }
        else
        {
            fund = await db.Funds.FirstOrDefaultAsync(f => f.Id == req.FundId, ct) ?? throw new ValidationException("Choose the fund to charge.");
            entityId = req.EntityId ?? beneficiary?.EntityId ?? throw new ValidationException("Choose the entity the cost belongs to.");
        }
        await access.EnsureAsync(Permissions.FundExpensesCreate, entityId, ct);

        if (req.Amount <= 0) throw new ValidationException("Amount must be positive.");
        var amount = LedgerService.Round(req.Amount);
        if (grant != null)
        {
            if (grant.Status != GrantStatus.Active) throw new ValidationException($"{grant.Number} is not active.");
            if (req.Date < grant.StartDate || req.Date > grant.EndDate)
                throw new ValidationException($"{grant.Number} only covers costs from {grant.StartDate:dd MMM yyyy} to {grant.EndDate:dd MMM yyyy}.");
            line = grant.BudgetLines.FirstOrDefault(l => l.Id == req.BudgetLineId) ?? throw new ValidationException("Choose the grant budget line to charge.");
            var onLine = await db.FundExpenses.Where(x => x.BudgetLineId == line.Id).SumAsync(x => x.Amount, ct);
            var rate = await NgoCommon.AverageRateAsync(db, grant, ct);
            var after = (onLine + amount) / rate;
            var limit = line.Amount * (1 + grant.FlexibilityPercent / 100m);
            if (after > limit + 0.005m)
                throw new ValidationException($"Budget line {line.Code} would reach {after:N0} {grant.Currency} against a budget of {line.Amount:N0} — beyond the " +
                                              $"{grant.FlexibilityPercent:0.#}% flexibility. Re-budget the grant (with donor approval) first.");
        }
        else if (fund.GrantId != null) throw new ValidationException("Charge grant spending to the grant and one of its budget lines.");
        if (!fund.IsActive) throw new ValidationException($"{fund.Code} is closed.");
        if (fund.Kind == FundKind.Endowment) throw new ValidationException("Endowment capital can't be spent.");
        if (fund.Kind == FundKind.Zakat)
        {
            if (beneficiary == null) throw new ValidationException("Zakat can only be paid out as assistance to Zakat-eligible beneficiaries.");
            if (!beneficiary.ZakatEligible) throw new ValidationException($"{beneficiary.FullName} is not verified as Zakat-eligible.");
        }
        if (grant == null && fund.Kind is FundKind.Restricted or FundKind.Zakat)
        {
            var available = await AvailableAsync(fund.Id, ct);
            if (amount > available) throw new ValidationException($"Only {available:N0} is left in {fund.Code}.");
        }

        var accountId = req.AccountId ?? line?.ExpenseAccountId ?? throw new ValidationException("Choose the expense account.");
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct) ?? throw new NotFoundException("Account");
        if (account.Type != AccountType.Expense || account.IsGroup) throw new ValidationException("Charge an expense account.");
        var modes = (req.PaidFromAccountId != null ? 1 : 0) + (req.VendorId != null ? 1 : 0) + (req.AllocationOnly ? 1 : 0);
        if (modes != 1) throw new ValidationException("Choose one: paid from cash/bank, owed to a vendor, or an allocation of a cost already booked.");
        var description = Guard.Required(req.Description, "Description", 300);

        var settings = await ledger.SettingsAsync(ct);
        var e = new FundExpense
        {
            TenantId = currentUser.TenantId!.Value, EntityId = entityId, Number = await ledger.NextNumberAsync("FEX", req.Date, ct), Date = req.Date,
            FundId = fund.Id, GrantId = grant?.Id, BudgetLineId = line?.Id, ProgramId = req.ProgramId ?? beneficiary?.ProgramId ?? grant?.ProgramId,
            Function = req.Function, AccountId = account.Id, Description = description, Amount = amount, PaidFromAccountId = req.PaidFromAccountId,
            VendorId = req.VendorId, AllocationOnly = req.AllocationOnly
        };
        var text = $"{e.Number} {fund.Code}{(line != null ? "/" + line.Code : "")} — {description}";
        if (req.PaidFromAccountId is { } bankId)
        {
            var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == bankId, ct) ?? throw new NotFoundException("Account");
            if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Pay from a bank or cash account.");
            var entry = await ledger.BuildAndPostAsync(entityId, req.Date, text, settings.BaseCurrency, 1, JournalSource.Ngo, e.Id,
                [new(account.Id, amount, 0, null, text), new(bank.Id, 0, amount, null, text)], e.Number, ct);
            e.JournalEntryId = entry.Id;
        }
        else if (req.VendorId is { } vendorId)
        {
            if (!await db.Contacts.AnyAsync(c => c.Id == vendorId && c.IsVendor, ct)) throw new ValidationException("Choose a vendor.");
            var bill = await documents.CreateAsync(DocumentKind.Bill, new SaveDocumentRequest(entityId, vendorId, req.Date, null, e.Number, text,
                settings.BaseCurrency, 1, [new DocumentLineInput(description, account.Id, 1, amount, null)]), ct, system: true);
            await documents.ApproveAsync(bill.Id, ct, system: true);
            e.BillId = bill.Id;
        }

        if (NgoCommon.DeferredAccount(fund.Kind) is { } deferred)
        {
            var release = await ledger.BuildAndPostAsync(entityId, req.Date, $"Restricted income released — {text}", settings.BaseCurrency, 1, JournalSource.Ngo, e.Id,
                [new(await NgoCommon.AccountAsync(db, deferred, ct), amount, 0, null, text), new(await NgoCommon.AccountAsync(db, "4310", ct), 0, amount, null, text)], e.Number, ct);
            e.ReleaseJournalId = release.Id;
        }
        db.FundExpenses.Add(e);
        return e;
    }
}
