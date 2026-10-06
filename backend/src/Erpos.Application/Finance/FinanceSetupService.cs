using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>Chart of accounts, settings, exchange rates, tax rates and contacts (all organization-wide).</summary>
public class FinanceSetupService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger)
{
    // ---------------- Accounts ----------------

    /// <summary>Anyone working in finance needs the account list for pickers.</summary>
    public async Task<List<AccountDto>> AccountsAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        if (!mine.ByEntity.Values.Any(s => s.Any(p => p.StartsWith("finance.")))) throw new ForbiddenException();
        return await db.Accounts.OrderBy(a => a.Code).Select(a => new AccountDto(a.Id, a.Code, a.Name, a.Type, a.SubType, a.ParentId,
            a.IsGroup, a.IsSystem, a.Currency, a.Description, a.IsActive)).ToListAsync(ct);
    }

    public async Task<AccountDto> SaveAccountAsync(Guid? id, SaveAccountRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(id == null ? Permissions.AccountsCreate : Permissions.AccountsEdit, ct);
        var a = id == null ? null : await db.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Account");
        var used = a != null && await db.JournalLines.AnyAsync(l => l.AccountId == a.Id, ct);
        if (a == null)
        {
            a = new Account { TenantId = currentUser.TenantId!.Value };
            db.Accounts.Add(a);
        }

        var code = Guard.Required(req.Code, "Code", 20);
        if (await db.Accounts.AnyAsync(x => x.Code == code && x.Id != a.Id, ct)) throw new ValidationException($"Account code {code} already exists.");
        if (req.ParentId != null)
        {
            var parent = await db.Accounts.FirstOrDefaultAsync(x => x.Id == req.ParentId, ct) ?? throw new NotFoundException("Parent account");
            if (!parent.IsGroup) throw new ValidationException("The parent must be a group account.");
            if (parent.Type != req.Type) throw new ValidationException("An account must have the same type as its parent group.");
            if (parent.Id == a.Id) throw new ValidationException("An account can't be its own parent.");
        }
        if (used && (req.Type != a.Type || req.IsGroup)) throw new ValidationException("This account has postings; its type can't change and it can't become a group.");
        if (a.IsSystem && !req.IsActive) throw new ValidationException("System accounts can't be deactivated.");
        if (req.Currency != null && req.SubType is not (AccountSubType.Bank or AccountSubType.Cash))
            throw new ValidationException("Only bank and cash accounts can have their own currency.");

        a.Code = code;
        a.Name = Guard.Required(req.Name, "Name", 150);
        a.Type = req.Type;
        a.SubType = req.IsGroup ? AccountSubType.Group : req.SubType;
        a.ParentId = req.ParentId;
        a.IsGroup = req.IsGroup;
        a.Currency = string.IsNullOrWhiteSpace(req.Currency) ? null : req.Currency.Trim().ToUpperInvariant();
        a.Description = req.Description;
        a.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return new AccountDto(a.Id, a.Code, a.Name, a.Type, a.SubType, a.ParentId, a.IsGroup, a.IsSystem, a.Currency, a.Description, a.IsActive);
    }

    // ---------------- Settings ----------------

    public async Task<FinanceSettingsDto> GetSettingsAsync(CancellationToken ct) => ToDto(await ledger.SettingsAsync(ct));

    public async Task<FinanceSettingsDto> SaveSettingsAsync(FinanceSettingsDto req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.FinanceSettingsManage, ct);
        var s = await ledger.SettingsAsync(ct);
        if (req.FiscalYearStartMonth is < 1 or > 12) throw new ValidationException("Fiscal year start month must be 1–12.");
        var baseCurrency = Guard.Required(req.BaseCurrency, "Base currency", 3).ToUpperInvariant();
        if (baseCurrency != s.BaseCurrency && await db.JournalEntries.AnyAsync(ct))
            throw new ValidationException("The base currency can't change once anything has been posted.");

        var ids = new[] { req.ReceivableAccountId, req.PayableAccountId, req.RetainedEarningsAccountId, req.ExchangeGainLossAccountId,
            req.SalaryExpenseAccountId, req.SalaryPayableAccountId, req.SalaryTaxPayableAccountId, req.EobiExpenseAccountId,
            req.EobiPayableAccountId, req.PfExpenseAccountId, req.PfPayableAccountId, req.SocialSecurityExpenseAccountId,
            req.SocialSecurityPayableAccountId, req.OtherPayrollDeductionsAccountId, req.DefaultInventoryAccountId,
            req.DefaultConsumptionAccountId, req.GrniAccountId, req.PriceVarianceAccountId, req.InventoryAdjustmentAccountId }.Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        if (await db.Accounts.CountAsync(a => ids.Contains(a.Id) && !a.IsGroup, ct) != ids.Count)
            throw new ValidationException("Default accounts must be existing posting (non-group) accounts.");

        s.BaseCurrency = baseCurrency;
        s.FiscalYearStartMonth = req.FiscalYearStartMonth;
        s.LockedThrough = req.LockedThrough;
        s.Ntn = req.Ntn;
        s.Strn = req.Strn;
        s.ReceivableAccountId = req.ReceivableAccountId;
        s.PayableAccountId = req.PayableAccountId;
        s.RetainedEarningsAccountId = req.RetainedEarningsAccountId;
        s.ExchangeGainLossAccountId = req.ExchangeGainLossAccountId;
        s.SalaryExpenseAccountId = req.SalaryExpenseAccountId;
        s.SalaryPayableAccountId = req.SalaryPayableAccountId;
        s.SalaryTaxPayableAccountId = req.SalaryTaxPayableAccountId;
        s.EobiExpenseAccountId = req.EobiExpenseAccountId;
        s.EobiPayableAccountId = req.EobiPayableAccountId;
        s.PfExpenseAccountId = req.PfExpenseAccountId;
        s.PfPayableAccountId = req.PfPayableAccountId;
        s.SocialSecurityExpenseAccountId = req.SocialSecurityExpenseAccountId;
        s.SocialSecurityPayableAccountId = req.SocialSecurityPayableAccountId;
        s.OtherPayrollDeductionsAccountId = req.OtherPayrollDeductionsAccountId;
        s.DefaultInventoryAccountId = req.DefaultInventoryAccountId;
        s.DefaultConsumptionAccountId = req.DefaultConsumptionAccountId;
        s.GrniAccountId = req.GrniAccountId;
        s.PriceVarianceAccountId = req.PriceVarianceAccountId;
        s.InventoryAdjustmentAccountId = req.InventoryAdjustmentAccountId;
        if (req.PriceTolerance is < 0 or > 1) throw new ValidationException("Price tolerance is a fraction between 0 and 1.");
        s.PriceTolerance = req.PriceTolerance;
        if (req.CustomerAdvanceAccountId != null) s.CustomerAdvanceAccountId = req.CustomerAdvanceAccountId;
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    private static FinanceSettingsDto ToDto(FinanceSettings s) => new(s.BaseCurrency, s.FiscalYearStartMonth, s.LockedThrough, s.Ntn, s.Strn,
        s.ReceivableAccountId, s.PayableAccountId, s.RetainedEarningsAccountId, s.ExchangeGainLossAccountId, s.SalaryExpenseAccountId,
        s.SalaryPayableAccountId, s.SalaryTaxPayableAccountId, s.EobiExpenseAccountId, s.EobiPayableAccountId, s.PfExpenseAccountId,
        s.PfPayableAccountId, s.SocialSecurityExpenseAccountId, s.SocialSecurityPayableAccountId, s.OtherPayrollDeductionsAccountId,
        s.DefaultInventoryAccountId, s.DefaultConsumptionAccountId, s.GrniAccountId, s.PriceVarianceAccountId, s.InventoryAdjustmentAccountId,
        s.PriceTolerance, s.CustomerAdvanceAccountId);

    // ---------------- Exchange rates ----------------

    public Task<List<ExchangeRateDto>> RatesAsync(string? currency, CancellationToken ct)
    {
        var q = db.ExchangeRates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(currency)) q = q.Where(r => r.Currency == currency.ToUpper());
        return q.OrderByDescending(r => r.Date).ThenBy(r => r.Currency).Take(500)
            .Select(r => new ExchangeRateDto(r.Id, r.Currency, r.Date, r.Rate)).ToListAsync(ct);
    }

    public async Task<ExchangeRateDto> SaveRateAsync(SaveExchangeRateRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.FinanceSettingsManage, ct);
        var settings = await ledger.SettingsAsync(ct);
        var cur = Guard.Required(req.Currency, "Currency", 3).ToUpperInvariant();
        if (cur == settings.BaseCurrency) throw new ValidationException("The base currency always has rate 1.");
        if (req.Rate <= 0) throw new ValidationException("Rate must be positive.");
        var r = await db.ExchangeRates.FirstOrDefaultAsync(x => x.Currency == cur && x.Date == req.Date, ct);
        if (r == null)
        {
            r = new ExchangeRate { TenantId = currentUser.TenantId!.Value, Currency = cur, Date = req.Date };
            db.ExchangeRates.Add(r);
        }
        r.Rate = req.Rate;
        await db.SaveChangesAsync(ct);
        return new ExchangeRateDto(r.Id, r.Currency, r.Date, r.Rate);
    }

    // ---------------- Tax rates ----------------

    public Task<List<TaxRateDto>> TaxRatesAsync(CancellationToken ct) =>
        db.TaxRates.OrderBy(t => t.Code).Select(t => new TaxRateDto(t.Id, t.Code, t.Name, t.Rate, t.Authority, t.OutputAccountId, t.InputAccountId, t.IsActive))
            .ToListAsync(ct);

    public async Task<TaxRateDto> SaveTaxRateAsync(Guid? id, SaveTaxRateRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.FinanceSettingsManage, ct);
        var t = id == null ? null : await db.TaxRates.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Tax rate");
        if (t != null && t.Rate != req.Rate && await db.FinanceDocumentLines.AnyAsync(l => l.TaxRateId == t.Id, ct))
            throw new ValidationException("This rate is used on documents. Create a new tax rate for the new percentage.");
        if (t == null)
        {
            t = new TaxRate { TenantId = currentUser.TenantId!.Value };
            db.TaxRates.Add(t);
        }
        var code = Guard.Code(req.Code);
        if (await db.TaxRates.AnyAsync(x => x.Code == code && x.Id != t.Id, ct)) throw new ValidationException($"Tax code {code} already exists.");
        if (req.Rate is < 0 or > 1) throw new ValidationException("Rate is a fraction between 0 and 1 (0.18 = 18%).");
        if (req.OutputAccountId == null || req.InputAccountId == null) throw new ValidationException("Choose the output (payable) and input (recoverable) tax accounts.");

        t.Code = code;
        t.Name = Guard.Required(req.Name, "Name", 100);
        t.Rate = req.Rate;
        t.Authority = req.Authority;
        t.OutputAccountId = req.OutputAccountId;
        t.InputAccountId = req.InputAccountId;
        t.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return new TaxRateDto(t.Id, t.Code, t.Name, t.Rate, t.Authority, t.OutputAccountId, t.InputAccountId, t.IsActive);
    }

    // ---------------- Contacts ----------------

    public async Task<List<ContactDto>> ContactsAsync(bool? customers, bool? vendors, string? search, CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        if (!mine.ByEntity.Values.Any(s => s.Any(p => p.StartsWith("finance.")))) throw new ForbiddenException();
        var q = db.Contacts.AsQueryable();
        if (customers == true) q = q.Where(c => c.IsCustomer);
        if (vendors == true) q = q.Where(c => c.IsVendor);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(s) || c.Code.ToLower().Contains(s) || (c.Ntn != null && c.Ntn.Contains(s)));
        }
        var open = new[] { DocumentStatus.Open, DocumentStatus.PartiallyPaid };
        return await q.OrderBy(c => c.Name).Select(c => new ContactDto(c.Id, c.Code, c.Name, c.IsCustomer, c.IsVendor, c.Email, c.Phone,
            c.Address, c.City, c.Country, c.Ntn, c.Strn, c.Cnic, c.Currency, c.PaymentTermsDays, c.IsActive,
            db.FinanceDocuments.Where(d => d.ContactId == c.Id && d.Kind == DocumentKind.Invoice && open.Contains(d.Status)).Sum(d => d.BaseTotal - d.BasePaid),
            db.FinanceDocuments.Where(d => d.ContactId == c.Id && d.Kind == DocumentKind.Bill && open.Contains(d.Status)).Sum(d => d.BaseTotal - d.BasePaid),
            c.DefaultWhtRateId, c.NotOnActiveTaxpayerList))
            .ToListAsync(ct);
    }

    public async Task<ContactDto> SaveContactAsync(Guid? id, SaveContactRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(id == null ? Permissions.ContactsCreate : Permissions.ContactsEdit, ct);
        var c = id == null ? null : await db.Contacts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Contact");
        if (c == null)
        {
            c = new Contact { TenantId = currentUser.TenantId!.Value };
            db.Contacts.Add(c);
        }
        var code = Guard.Code(req.Code);
        if (await db.Contacts.AnyAsync(x => x.Code == code && x.Id != c.Id, ct)) throw new ValidationException($"Contact code {code} already exists.");
        if (!req.IsCustomer && !req.IsVendor) throw new ValidationException("A contact must be a customer, a vendor or both.");

        c.Code = code;
        c.Name = Guard.Required(req.Name, "Name");
        c.IsCustomer = req.IsCustomer;
        c.IsVendor = req.IsVendor;
        c.Email = req.Email;
        c.Phone = req.Phone;
        c.Address = req.Address;
        c.City = req.City;
        c.Country = req.Country;
        c.Ntn = req.Ntn;
        c.Strn = req.Strn;
        c.Cnic = req.Cnic;
        c.Currency = string.IsNullOrWhiteSpace(req.Currency) ? null : req.Currency.Trim().ToUpperInvariant();
        c.PaymentTermsDays = Math.Clamp(req.PaymentTermsDays, 0, 365);
        c.IsActive = req.IsActive;
        if (req.DefaultWhtRateId is { } wr && !await db.WithholdingTaxRates.AnyAsync(r => r.Id == wr, ct)) throw new NotFoundException("Withholding rate");
        c.DefaultWhtRateId = req.DefaultWhtRateId;
        c.NotOnActiveTaxpayerList = req.NotOnActiveTaxpayerList;
        await db.SaveChangesAsync(ct);
        return (await ContactsAsync(null, null, c.Code, ct)).First(x => x.Id == c.Id);
    }
}
