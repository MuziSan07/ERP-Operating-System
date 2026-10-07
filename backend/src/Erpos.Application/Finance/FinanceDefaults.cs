using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Starter chart of accounts (IFRS for SMEs statement layout), sales tax rates and default posting accounts.
/// All editable; tax rates must be checked against current FBR / provincial notifications.
/// </summary>
public static class FinanceDefaults
{
    private record Seed(string Code, string Name, AccountType Type, AccountSubType Sub, string? Parent, bool Group = false, bool System = false);

    private static readonly Seed[] Chart =
    [
        new("1000", "Assets", AccountType.Asset, AccountSubType.Group, null, true),
        new("1100", "Current assets", AccountType.Asset, AccountSubType.Group, "1000", true),
        new("1110", "Cash in hand", AccountType.Asset, AccountSubType.Cash, "1100", System: true),
        new("1120", "Bank - main account", AccountType.Asset, AccountSubType.Bank, "1100", System: true),
        new("1200", "Trade receivables", AccountType.Asset, AccountSubType.Receivable, "1100", System: true),
        new("1210", "Advances to employees", AccountType.Asset, AccountSubType.OtherCurrentAsset, "1100"),
        new("1220", "Input sales tax recoverable", AccountType.Asset, AccountSubType.TaxReceivable, "1100", System: true),
        new("1230", "Advance income tax / tax deducted at source", AccountType.Asset, AccountSubType.TaxReceivable, "1100"),
        new("1300", "Inventories", AccountType.Asset, AccountSubType.Inventory, "1100", System: true),
        new("1400", "Prepayments and deposits", AccountType.Asset, AccountSubType.Prepayment, "1100"),
        new("1500", "Non-current assets", AccountType.Asset, AccountSubType.Group, "1000", true),
        new("1510", "Property, plant and equipment", AccountType.Asset, AccountSubType.FixedAsset, "1500"),
        new("1520", "Accumulated depreciation", AccountType.Asset, AccountSubType.AccumulatedDepreciation, "1500"),
        new("1530", "Intangible assets", AccountType.Asset, AccountSubType.IntangibleAsset, "1500"),

        new("2000", "Liabilities", AccountType.Liability, AccountSubType.Group, null, true),
        new("2100", "Current liabilities", AccountType.Liability, AccountSubType.Group, "2000", true),
        new("2110", "Trade payables", AccountType.Liability, AccountSubType.Payable, "2100", System: true),
        new("2120", "Accrued expenses", AccountType.Liability, AccountSubType.AccruedLiability, "2100"),
        new("2130", "Salaries payable", AccountType.Liability, AccountSubType.PayrollLiability, "2100", System: true),
        new("2140", "Income tax withheld on salaries", AccountType.Liability, AccountSubType.TaxPayable, "2100", System: true),
        new("2150", "EOBI payable", AccountType.Liability, AccountSubType.PayrollLiability, "2100", System: true),
        new("2160", "Provident fund payable", AccountType.Liability, AccountSubType.PayrollLiability, "2100", System: true),
        new("2170", "Social security payable", AccountType.Liability, AccountSubType.PayrollLiability, "2100", System: true),
        new("2175", "Other payroll deductions payable", AccountType.Liability, AccountSubType.PayrollLiability, "2100", System: true),
        new("2180", "Output sales tax payable (FBR)", AccountType.Liability, AccountSubType.TaxPayable, "2100", System: true),
        new("2185", "Income tax withheld from suppliers payable", AccountType.Liability, AccountSubType.TaxPayable, "2100", System: true),
        new("2190", "Provincial sales tax on services payable", AccountType.Liability, AccountSubType.TaxPayable, "2100", System: true),
        new("2125", "Goods received not invoiced", AccountType.Liability, AccountSubType.AccruedLiability, "2100", System: true),
        new("2210", "COD collections payable to shippers", AccountType.Liability, AccountSubType.OtherCurrentLiability, "2100", System: true),
        new("2220", "Restricted grants and donations received in advance", AccountType.Liability, AccountSubType.DeferredIncome, "2100", System: true),
        new("2230", "Zakat received, not yet disbursed", AccountType.Liability, AccountSubType.DeferredIncome, "2100", System: true),
        new("2200", "Advances from customers / deferred income", AccountType.Liability, AccountSubType.DeferredIncome, "2100"),
        new("2500", "Non-current liabilities", AccountType.Liability, AccountSubType.Group, "2000", true),
        new("2510", "Long-term borrowings", AccountType.Liability, AccountSubType.NonCurrentLiability, "2500"),

        new("3000", "Equity", AccountType.Equity, AccountSubType.Group, null, true),
        new("3100", "Share capital / owner's equity", AccountType.Equity, AccountSubType.Capital, "3000"),
        new("3200", "Retained earnings", AccountType.Equity, AccountSubType.RetainedEarnings, "3000", System: true),
        new("3300", "Restricted funds", AccountType.Equity, AccountSubType.Reserves, "3000"),

        new("4000", "Income", AccountType.Income, AccountSubType.Group, null, true),
        new("4100", "Revenue from services", AccountType.Income, AccountSubType.Revenue, "4000"),
        new("4110", "Sale of goods", AccountType.Income, AccountSubType.Revenue, "4000"),
        new("4120", "Freight and transport revenue", AccountType.Income, AccountSubType.Revenue, "4000", System: true),
        new("4300", "Grants and donations", AccountType.Income, AccountSubType.Revenue, "4000"),
        new("4310", "Restricted income released (grants and Zakat spent)", AccountType.Income, AccountSubType.Revenue, "4000", System: true),
        new("4900", "Other income", AccountType.Income, AccountSubType.OtherIncome, "4000"),
        new("4920", "Gain / (loss) on disposal of fixed assets", AccountType.Income, AccountSubType.OtherIncome, "4000", System: true),
        new("4910", "Exchange gain / (loss)", AccountType.Income, AccountSubType.OtherIncome, "4000", System: true),

        new("5000", "Cost of sales", AccountType.Expense, AccountSubType.Group, null, true),
        new("5100", "Cost of goods and services sold", AccountType.Expense, AccountSubType.CostOfSales, "5000", System: true),
        new("5110", "Purchase price variance", AccountType.Expense, AccountSubType.CostOfSales, "5000", System: true),
        new("5120", "Inventory adjustments and write-offs", AccountType.Expense, AccountSubType.CostOfSales, "5000", System: true),
        new("5130", "Vehicle running costs (fuel, tolls, allowances)", AccountType.Expense, AccountSubType.CostOfSales, "5000", System: true),
        new("5140", "Hired vehicle charges", AccountType.Expense, AccountSubType.CostOfSales, "5000", System: true),

        new("6000", "Operating expenses", AccountType.Expense, AccountSubType.Group, null, true),
        new("6100", "Salaries and wages", AccountType.Expense, AccountSubType.PayrollExpense, "6000", System: true),
        new("6110", "EOBI contribution", AccountType.Expense, AccountSubType.PayrollExpense, "6000", System: true),
        new("6120", "Provident fund contribution", AccountType.Expense, AccountSubType.PayrollExpense, "6000", System: true),
        new("6130", "Social security contribution", AccountType.Expense, AccountSubType.PayrollExpense, "6000", System: true),
        new("6200", "Rent", AccountType.Expense, AccountSubType.OperatingExpense, "6000"),
        new("6300", "Utilities", AccountType.Expense, AccountSubType.OperatingExpense, "6000"),
        new("6400", "Travel and conveyance", AccountType.Expense, AccountSubType.OperatingExpense, "6000"),
        new("6450", "Vehicle repairs and maintenance", AccountType.Expense, AccountSubType.OperatingExpense, "6000", System: true),
        new("6500", "Office and administration", AccountType.Expense, AccountSubType.OperatingExpense, "6000"),
        new("6600", "Legal and professional fees", AccountType.Expense, AccountSubType.OperatingExpense, "6000"),
        new("6700", "Depreciation and amortisation", AccountType.Expense, AccountSubType.Depreciation, "6000"),
        new("6800", "Bank charges", AccountType.Expense, AccountSubType.FinanceCost, "6000"),
        new("6950", "Beneficiary assistance", AccountType.Expense, AccountSubType.OperatingExpense, "6000", System: true),
        new("6960", "Program activities and supplies", AccountType.Expense, AccountSubType.OperatingExpense, "6000"),
        new("6900", "Income tax expense", AccountType.Expense, AccountSubType.TaxExpense, "6000"),
    ];

    /// <summary>
    /// Section 153 withholding defaults (Income Tax Ordinance 2001, rates for active taxpayers as in recent Finance Acts).
    /// Rates change with each budget: organizations review and edit them; non-ATL suppliers are charged twice the rate.
    /// </summary>
    internal static readonly (string Code, string Name, string Section, decimal Rate)[] WhtDefaults =
    [
        ("WHT-GOODS-CO", "Supply of goods — company", "153(1)(a)", 0.05m),
        ("WHT-GOODS-OTH", "Supply of goods — individual / AOP", "153(1)(a)", 0.055m),
        ("WHT-SVC-CO", "Services — company", "153(1)(b)", 0.09m),
        ("WHT-SVC-OTH", "Services — individual / AOP", "153(1)(b)", 0.11m),
        ("WHT-CON-CO", "Execution of contracts — company", "153(1)(c)", 0.07m),
        ("WHT-CON-OTH", "Execution of contracts — individual / AOP", "153(1)(c)", 0.075m),
    ];

    /// <summary>Starter asset classes (straight line, typical useful lives); organizations adjust them to their policy.</summary>
    internal static readonly (string Code, string Name, int LifeMonths)[] AssetCategoryDefaults =
    [
        ("BLDG", "Buildings", 300), ("PLANT", "Plant & machinery", 120), ("FURN", "Furniture & fixtures", 120),
        ("VEH", "Vehicles", 60), ("IT", "Computers & IT equipment", 36), ("OFFICE", "Office equipment", 60),
    ];

    private static async Task EnsureAssetCategoriesAsync(IAppDbContext db, Guid tenantId, Guid asset, Guid accumulated, Guid expense, CancellationToken ct)
    {
        if (await db.AssetCategories.IgnoreQueryFilters().AnyAsync(c => c.TenantId == tenantId, ct)) return;
        foreach (var (code, name, life) in AssetCategoryDefaults)
            db.AssetCategories.Add(new AssetCategory { TenantId = tenantId, Code = code, Name = name, Method = DepreciationMethod.StraightLine, UsefulLifeMonths = life,
                AssetAccountId = asset, AccumulatedAccountId = accumulated, ExpenseAccountId = expense });
    }

    private static async Task EnsureWhtRatesAsync(IAppDbContext db, Guid tenantId, Guid payableAccountId, CancellationToken ct)
    {
        var have = (await db.WithholdingTaxRates.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).Select(r => r.Code).ToListAsync(ct)).ToHashSet();
        foreach (var (code, name, section, rate) in WhtDefaults.Where(d => !have.Contains(d.Code)))
            db.WithholdingTaxRates.Add(new WithholdingTaxRate { TenantId = tenantId, Code = code, Name = name, Section = section, Rate = rate, PayableAccountId = payableAccountId });
    }

    public static async Task EnsureAsync(IAppDbContext db, Guid tenantId, CancellationToken ct, string? baseCurrency = null)
    {
        if (await db.FinanceSettings.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId, ct)) return;

        baseCurrency ??= await db.Entities.IgnoreQueryFilters().Where(e => e.TenantId == tenantId && e.ParentId == null)
            .Select(e => e.Currency).FirstOrDefaultAsync(ct) ?? "PKR";

        var byCode = new Dictionary<string, Account>();
        foreach (var s in Chart)
        {
            var a = new Account
            {
                TenantId = tenantId, Code = s.Code, Name = s.Name, Type = s.Type, SubType = s.Sub, IsGroup = s.Group,
                IsSystem = s.System, ParentId = s.Parent == null ? null : byCode[s.Parent].Id
            };
            byCode[s.Code] = a;
            db.Accounts.Add(a);
        }

        db.TaxRates.AddRange(
            new TaxRate { TenantId = tenantId, Code = "GST18", Name = "GST 18% (goods)", Rate = 0.18m, Authority = "FBR", OutputAccountId = byCode["2180"].Id, InputAccountId = byCode["1220"].Id },
            new TaxRate { TenantId = tenantId, Code = "PST-PB", Name = "Punjab sales tax on services 16%", Rate = 0.16m, Authority = "PRA", OutputAccountId = byCode["2190"].Id, InputAccountId = byCode["1220"].Id },
            new TaxRate { TenantId = tenantId, Code = "PST-SD", Name = "Sindh sales tax on services 15%", Rate = 0.15m, Authority = "SRB", OutputAccountId = byCode["2190"].Id, InputAccountId = byCode["1220"].Id },
            new TaxRate { TenantId = tenantId, Code = "PST-KP", Name = "KP sales tax on services 15%", Rate = 0.15m, Authority = "KPRA", OutputAccountId = byCode["2190"].Id, InputAccountId = byCode["1220"].Id },
            new TaxRate { TenantId = tenantId, Code = "PST-ICT", Name = "Islamabad sales tax on services 16%", Rate = 0.16m, Authority = "FBR (ICT)", OutputAccountId = byCode["2190"].Id, InputAccountId = byCode["1220"].Id },
            new TaxRate { TenantId = tenantId, Code = "EXEMPT", Name = "Exempt / zero-rated", Rate = 0m, Authority = null, OutputAccountId = byCode["2180"].Id, InputAccountId = byCode["1220"].Id });

        db.FinanceSettings.Add(new FinanceSettings
        {
            TenantId = tenantId, BaseCurrency = baseCurrency, FiscalYearStartMonth = 7,
            ReceivableAccountId = byCode["1200"].Id, PayableAccountId = byCode["2110"].Id,
            RetainedEarningsAccountId = byCode["3200"].Id, ExchangeGainLossAccountId = byCode["4910"].Id,
            SalaryExpenseAccountId = byCode["6100"].Id, SalaryPayableAccountId = byCode["2130"].Id,
            SalaryTaxPayableAccountId = byCode["2140"].Id, EobiExpenseAccountId = byCode["6110"].Id,
            EobiPayableAccountId = byCode["2150"].Id, PfExpenseAccountId = byCode["6120"].Id, PfPayableAccountId = byCode["2160"].Id,
            SocialSecurityExpenseAccountId = byCode["6130"].Id, SocialSecurityPayableAccountId = byCode["2170"].Id,
            OtherPayrollDeductionsAccountId = byCode["2175"].Id,
            DefaultInventoryAccountId = byCode["1300"].Id, DefaultConsumptionAccountId = byCode["5100"].Id,
            GrniAccountId = byCode["2125"].Id, PriceVarianceAccountId = byCode["5110"].Id, InventoryAdjustmentAccountId = byCode["5120"].Id,
            CustomerAdvanceAccountId = byCode["2200"].Id
        });
        await EnsureWhtRatesAsync(db, tenantId, byCode["2185"].Id, ct);
        await EnsureAssetCategoriesAsync(db, tenantId, byCode["1510"].Id, byCode["1520"].Id, byCode["6700"].Id, ct);
    }

    /// <summary>Adds accounts introduced by later phases to organizations created before them. Caller saves.</summary>
    public static async Task UpgradeAsync(IAppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var settings = await db.FinanceSettings.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings == null) return;
        var accounts = await db.Accounts.IgnoreQueryFilters().Where(a => a.TenantId == tenantId && !a.IsDeleted).ToListAsync(ct);

        Account Ensure(string code)
        {
            var existing = accounts.FirstOrDefault(a => a.Code == code);
            if (existing != null) return existing;
            var seed = Chart.First(c => c.Code == code);
            var a = new Account
            {
                TenantId = tenantId, Code = seed.Code, Name = seed.Name, Type = seed.Type, SubType = seed.Sub, IsSystem = seed.System,
                ParentId = accounts.FirstOrDefault(p => p.Code == seed.Parent && p.IsGroup)?.Id
            };
            accounts.Add(a);
            db.Accounts.Add(a);
            return a;
        }
        settings.DefaultInventoryAccountId ??= Ensure("1300").Id;
        settings.DefaultConsumptionAccountId ??= Ensure("5100").Id;
        settings.GrniAccountId ??= Ensure("2125").Id;
        settings.CustomerAdvanceAccountId ??= Ensure("2200").Id;
        settings.PriceVarianceAccountId ??= Ensure("5110").Id;
        settings.InventoryAdjustmentAccountId ??= Ensure("5120").Id;
        // Accounts used by industry modules (looked up by code).
        foreach (var code in new[] { "4120", "5130", "5140", "6450", "2210", "2220", "2230", "3300", "4300", "4310", "6950", "6960" }) Ensure(code);
        await EnsureWhtRatesAsync(db, tenantId, Ensure("2185").Id, ct);
        Ensure("4920");
        await EnsureAssetCategoriesAsync(db, tenantId, Ensure("1510").Id, Ensure("1520").Id, Ensure("6700").Id, ct);
    }
}
