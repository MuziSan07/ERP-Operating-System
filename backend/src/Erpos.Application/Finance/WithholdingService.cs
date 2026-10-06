using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Withholding tax on supplier payments: rates, the register of deductions (for the monthly withholding statement),
/// deposits into the treasury (CPR) and per-supplier deduction certificates. Deduction itself happens in PaymentService.
/// </summary>
public class WithholdingService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger)
{
    // ---------------- Rates ----------------

    public async Task<List<WhtRateDto>> RatesAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        if (!mine.ByEntity.Values.Any(s => s.Any(p => p.StartsWith("finance.")))) throw new ForbiddenException();
        return await db.WithholdingTaxRates.OrderBy(r => r.Section).ThenBy(r => r.Code)
            .Select(r => new WhtRateDto(r.Id, r.Code, r.Name, r.Section, r.Rate, r.PayableAccountId,
                db.Accounts.Where(a => a.Id == r.PayableAccountId).Select(a => a.Code + " " + a.Name).First(), r.IsActive)).ToListAsync(ct);
    }

    public async Task<WhtRateDto> SaveRateAsync(Guid? id, SaveWhtRateRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.FinanceSettingsManage, ct);
        var r = id == null ? null : await db.WithholdingTaxRates.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Withholding rate");
        if (r == null) { r = new WithholdingTaxRate { TenantId = currentUser.TenantId!.Value }; db.WithholdingTaxRates.Add(r); }
        var code = Guard.Code(req.Code);
        if (await db.WithholdingTaxRates.AnyAsync(x => x.Code == code && x.Id != r.Id, ct)) throw new ValidationException($"Rate {code} exists.");
        if (req.Rate is <= 0 or >= 1) throw new ValidationException("Enter the rate as a fraction, e.g. 0.09 for 9%.");
        var payable = req.PayableAccountId ?? await db.Accounts.Where(a => a.Code == "2185").Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct)
                      ?? throw new ValidationException("Choose the liability account the withheld tax is held in.");
        if (!await db.Accounts.AnyAsync(a => a.Id == payable && a.Type == AccountType.Liability && !a.IsGroup, ct))
            throw new ValidationException("Withheld tax must be held in a liability account.");
        r.Code = code;
        r.Name = Guard.Required(req.Name, "Name", 150);
        r.Section = Guard.Required(req.Section, "Section", 30);
        r.Rate = req.Rate;
        r.PayableAccountId = payable;
        r.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await RatesAsync(ct)).First(x => x.Id == r.Id);
    }

    // ---------------- Deductions ----------------

    private IQueryable<Payment> Deductions(HashSet<Guid> entities, DateOnly from, DateOnly to) =>
        db.Payments.Where(p => entities.Contains(p.EntityId) && !p.IsVoid && p.WithholdingTax > 0 && p.Date >= from && p.Date <= to);

    private async Task<List<WhtDeductionDto>> ItemsAsync(IQueryable<Payment> q, CancellationToken ct) =>
        await q.OrderBy(p => p.Date).ThenBy(p => p.Number)
            .Select(p => new WhtDeductionDto(p.Id, p.Number, p.Date, p.ContactId, p.Contact!.Name, p.Contact.Ntn, p.Contact.Cnic, p.Contact.NotOnActiveTaxpayerList,
                db.WithholdingTaxRates.Where(r => r.Id == p.WithholdingTaxRateId).Select(r => r.Section).FirstOrDefault() ?? "", p.WithholdingTaxRateApplied,
                p.WithholdingTaxBase, p.WithholdingTax,
                db.WhtDeposits.Where(d => d.Id == p.WhtDepositId).Select(d => d.CprNumber).FirstOrDefault(),
                db.WhtDeposits.Where(d => d.Id == p.WhtDepositId).Select(d => (DateOnly?)d.Date).FirstOrDefault())).ToListAsync(ct);

    public async Task<WhtSummaryDto> DeductionsAsync(DateOnly from, DateOnly to, Guid? vendorId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ReportsView, ct);
        visible.UnionWith(await access.EntitiesWithAsync(Permissions.PaymentsView, ct));
        if (visible.Count == 0) throw new ForbiddenException();
        var q = Deductions(visible, from, to);
        if (vendorId != null) q = q.Where(p => p.ContactId == vendorId);
        var items = await ItemsAsync(q, ct);
        var deposited = items.Where(i => i.CprNumber != null).Sum(i => i.Tax);
        return new WhtSummaryDto(from, to, items, items.Sum(i => i.TaxBase), items.Sum(i => i.Tax), deposited, items.Sum(i => i.Tax) - deposited);
    }

    // ---------------- Deposits ----------------

    public async Task<List<WhtDepositDto>> DepositsAsync(CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.PaymentsView, ct);
        return await db.WhtDeposits.Where(d => visible.Contains(d.EntityId)).OrderByDescending(d => d.Year).ThenByDescending(d => d.Month)
            .Select(d => new WhtDepositDto(d.Id, d.Year, d.Month, d.Date, d.Amount, d.CprNumber, db.Payments.Count(p => p.WhtDepositId == d.Id))).ToListAsync(ct);
    }

    /// <summary>Pays a month's undeposited withholding into the treasury: Dr tax payable / Cr bank, tagged with the CPR number.</summary>
    public async Task<WhtDepositDto> DepositAsync(DepositWhtRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.PaymentsCreate, req.EntityId, ct);
        if (req.Month is < 1 or > 12) throw new ValidationException("Invalid month.");
        var cpr = Guard.Required(req.CprNumber, "CPR number", 50);
        var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.BankAccountId, ct) ?? throw new NotFoundException("Bank account");
        if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Pay from a bank account.");
        await db.LockAsync<BusinessEntity>(req.EntityId, ct); // two simultaneous deposits can't both take the same deductions
        var from = new DateOnly(req.Year, req.Month, 1);
        var payments = await Deductions([req.EntityId], from, from.AddMonths(1).AddDays(-1)).Where(p => p.WhtDepositId == null).ToListAsync(ct);
        if (payments.Count == 0) throw new ValidationException($"No undeposited withholding tax for {from:MMMM yyyy}.");

        var rateIds = payments.Select(p => p.WithholdingTaxRateId!.Value).Distinct().ToList();
        var accounts = await db.WithholdingTaxRates.IgnoreQueryFilters().Where(r => rateIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.PayableAccountId, ct);
        var total = payments.Sum(p => p.WithholdingTax);
        var settings = await ledger.SettingsAsync(ct);
        var text = $"Income tax withheld for {from:MMMM yyyy} deposited — CPR {cpr}";
        var lines = payments.GroupBy(p => accounts[p.WithholdingTaxRateId!.Value])
            .Select(g => new LineInput(g.Key, g.Sum(p => p.WithholdingTax), 0, null, text)).ToList();
        lines.Add(new LineInput(bank.Id, 0, total, null, text));
        var deposit = new WhtDeposit
        {
            TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Year = req.Year, Month = req.Month, Date = req.Date, Amount = total,
            CprNumber = cpr, BankAccountId = bank.Id
        };
        var entry = await ledger.BuildAndPostAsync(req.EntityId, req.Date, text, settings.BaseCurrency, 1, JournalSource.Payment, deposit.Id, lines, cpr, ct);
        deposit.JournalEntryId = entry.Id;
        foreach (var p in payments) p.WhtDepositId = deposit.Id;
        db.WhtDeposits.Add(deposit);
        await db.SaveChangesAsync(ct);
        return new WhtDepositDto(deposit.Id, deposit.Year, deposit.Month, deposit.Date, deposit.Amount, deposit.CprNumber, payments.Count);
    }

    // ---------------- Certificates ----------------

    /// <summary>Certificate of tax deducted for one supplier over a period (what the supplier claims against their own tax).</summary>
    public async Task<WhtCertificateDto> CertificateAsync(Guid vendorId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var summary = await DeductionsAsync(from, to, vendorId, ct);
        var vendor = await db.Contacts.FirstOrDefaultAsync(c => c.Id == vendorId, ct) ?? throw new NotFoundException("Supplier");
        var root = await db.Entities.Where(e => e.ParentId == null).Select(e => new { e.Name, e.TaxNumber }).FirstOrDefaultAsync(ct);
        return new WhtCertificateDto(root?.Name ?? "", root?.TaxNumber, vendor.Name, vendor.Ntn, vendor.Cnic, from, to, summary.Deductions,
            summary.TotalBase, summary.TotalTax);
    }
}
