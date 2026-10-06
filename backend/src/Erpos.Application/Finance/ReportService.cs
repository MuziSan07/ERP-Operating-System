using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Financial statements in base currency for an entity and its sub-entities (limited to those the user may report on).
/// No closing entries are needed: profit of earlier fiscal years is rolled into retained earnings on the fly.
/// </summary>
public class ReportService(IAppDbContext db, IAccessService access, LedgerService ledger)
{
    private record Bal(Guid AccountId, decimal Debit, decimal Credit);

    /// <summary>Entities covered: the chosen entity's subtree (default: everything visible) ∩ where reports are allowed.</summary>
    private async Task<HashSet<Guid>> ScopeAsync(Guid? entityId, CancellationToken ct)
    {
        var allowed = await access.EntitiesWithAsync(Permissions.ReportsView, ct);
        if (allowed.Count == 0) throw new ForbiddenException("Missing permission 'finance.reports.view'.");
        if (entityId == null) return allowed;
        if (!allowed.Contains(entityId.Value)) throw new ForbiddenException("Missing permission 'finance.reports.view' for this entity.");
        var path = await db.Entities.Where(e => e.Id == entityId).Select(e => e.Path).FirstAsync(ct);
        var subtree = await db.Entities.Where(e => e.Path.StartsWith(path)).Select(e => e.Id).ToListAsync(ct);
        return subtree.Where(allowed.Contains).ToHashSet();
    }

    private IQueryable<JournalLine> Posted(HashSet<Guid> scope) =>
        db.JournalLines.Where(l => scope.Contains(l.EntityId) &&
                                   (l.JournalEntry!.Status == JournalStatus.Posted || l.JournalEntry.Status == JournalStatus.Reversed));

    private async Task<List<Bal>> BalancesAsync(HashSet<Guid> scope, DateOnly? from, DateOnly to, CancellationToken ct)
    {
        var q = Posted(scope).Where(l => l.JournalEntry!.Date <= to);
        if (from != null) q = q.Where(l => l.JournalEntry!.Date >= from);
        return (await q.GroupBy(l => l.AccountId).Select(g => new { g.Key, D = g.Sum(l => l.BaseDebit), C = g.Sum(l => l.BaseCredit) }).ToListAsync(ct))
            .Select(x => new Bal(x.Key, x.D, x.C)).ToList();
    }

    private async Task<DateOnly> FiscalYearStartAsync(DateOnly d, CancellationToken ct)
    {
        var m = (await ledger.SettingsAsync(ct)).FiscalYearStartMonth;
        return new DateOnly(d.Month >= m ? d.Year : d.Year - 1, m, 1);
    }

    private static bool IsPnl(AccountType t) => t is AccountType.Income or AccountType.Expense;

    public async Task<TrialBalanceDto> TrialBalanceAsync(Guid? entityId, DateOnly asOf, CancellationToken ct)
    {
        var scope = await ScopeAsync(entityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var fyStart = await FiscalYearStartAsync(asOf, ct);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, ct);
        var all = await BalancesAsync(scope, null, asOf, ct);
        var prior = await BalancesAsync(scope, null, fyStart.AddDays(-1), ct);

        var rows = new List<TrialBalanceRow>();
        foreach (var b in all)
        {
            var a = accounts[b.AccountId];
            var (d, c) = (b.Debit, b.Credit);
            if (IsPnl(a.Type)) { var p = prior.FirstOrDefault(x => x.AccountId == a.Id); if (p != null) { d -= p.Debit; c -= p.Credit; } }
            var net = d - c;
            if (net != 0) rows.Add(new TrialBalanceRow(a.Id, a.Code, a.Name, a.Type, net > 0 ? net : 0, net < 0 ? -net : 0));
        }
        // Earlier years' profit closes into retained earnings.
        var priorProfit = prior.Where(b => IsPnl(accounts[b.AccountId].Type)).Sum(b => b.Credit - b.Debit);
        if (priorProfit != 0 && settings.RetainedEarningsAccountId is { } re)
        {
            var existing = rows.FirstOrDefault(r => r.AccountId == re);
            var net = (existing == null ? 0 : existing.Credit - existing.Debit) + priorProfit;
            rows.RemoveAll(r => r.AccountId == re);
            var a = accounts[re];
            rows.Add(new TrialBalanceRow(re, a.Code, a.Name, a.Type, net < 0 ? -net : 0, net > 0 ? net : 0));
        }
        rows = rows.OrderBy(r => r.Code).ToList();
        return new TrialBalanceDto(asOf, settings.BaseCurrency, rows, rows.Sum(r => r.Debit), rows.Sum(r => r.Credit));
    }

    public async Task<FinancialStatementDto> ProfitAndLossAsync(Guid? entityId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var scope = await ScopeAsync(entityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, ct);
        var bal = (await BalancesAsync(scope, from, to, ct)).Where(b => IsPnl(accounts[b.AccountId].Type)).ToList();

        StatementSection Section(string title, Func<Account, bool> pick, bool income)
        {
            var lines = bal.Where(b => pick(accounts[b.AccountId]))
                .Select(b => (a: accounts[b.AccountId], amt: income ? b.Credit - b.Debit : b.Debit - b.Credit))
                .Where(x => x.amt != 0).OrderBy(x => x.a.Code)
                .Select(x => new StatementLine(x.a.Code, x.a.Name, x.amt)).ToList();
            return new StatementSection(title, lines, lines.Sum(l => l.Amount));
        }

        var revenue = Section("Revenue", a => a.SubType == AccountSubType.Revenue, true);
        var cos = Section("Cost of sales", a => a.SubType == AccountSubType.CostOfSales, false);
        var other = Section("Other income", a => a.Type == AccountType.Income && a.SubType != AccountSubType.Revenue, true);
        var opex = Section("Operating expenses", a => a.Type == AccountType.Expense && a.SubType is not (AccountSubType.CostOfSales or AccountSubType.FinanceCost or AccountSubType.TaxExpense), false);
        var finance = Section("Finance costs", a => a.SubType == AccountSubType.FinanceCost, false);
        var tax = Section("Income tax expense", a => a.SubType == AccountSubType.TaxExpense, false);
        var gross = new StatementSection("Gross profit", [], revenue.Total - cos.Total);
        var beforeTax = new StatementSection("Profit before tax", [], gross.Total + other.Total - opex.Total - finance.Total);
        var profit = beforeTax.Total - tax.Total;

        return new FinancialStatementDto("Statement of profit or loss", from, to, settings.BaseCurrency,
            [revenue, cos, gross, other, opex, finance, beforeTax, tax], profit, profit >= 0 ? "Profit for the period" : "Loss for the period");
    }

    public async Task<FinancialStatementDto> BalanceSheetAsync(Guid? entityId, DateOnly asOf, CancellationToken ct)
    {
        var scope = await ScopeAsync(entityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var fyStart = await FiscalYearStartAsync(asOf, ct);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, ct);
        var all = await BalancesAsync(scope, null, asOf, ct);
        var current = await BalancesAsync(scope, fyStart, asOf, ct);

        decimal Profit(IEnumerable<Bal> b) => b.Where(x => IsPnl(accounts[x.AccountId].Type)).Sum(x => x.Credit - x.Debit);
        var currentProfit = Profit(current);
        var retainedFromPnl = Profit(all) - currentProfit;

        StatementSection Section(string title, Func<Account, bool> pick, bool debitNature)
        {
            var lines = all.Where(b => pick(accounts[b.AccountId]))
                .Select(b => (a: accounts[b.AccountId], amt: debitNature ? b.Debit - b.Credit : b.Credit - b.Debit))
                .Where(x => x.amt != 0).OrderBy(x => x.a.Code).Select(x => new StatementLine(x.a.Code, x.a.Name, x.amt)).ToList();
            return new StatementSection(title, lines, lines.Sum(l => l.Amount));
        }
        bool NonCurrentAsset(Account a) => a.SubType is AccountSubType.FixedAsset or AccountSubType.AccumulatedDepreciation or AccountSubType.IntangibleAsset or AccountSubType.OtherNonCurrentAsset;

        var nca = Section("Non-current assets", a => a.Type == AccountType.Asset && NonCurrentAsset(a), true);
        var ca = Section("Current assets", a => a.Type == AccountType.Asset && !NonCurrentAsset(a), true);
        var ncl = Section("Non-current liabilities", a => a.Type == AccountType.Liability && a.SubType == AccountSubType.NonCurrentLiability, false);
        var cl = Section("Current liabilities", a => a.Type == AccountType.Liability && a.SubType != AccountSubType.NonCurrentLiability, false);
        var eqBase = Section("Equity", a => a.Type == AccountType.Equity, false);
        var eqLines = eqBase.Lines.ToList();
        if (retainedFromPnl != 0) eqLines.Add(new StatementLine("", "Retained earnings brought forward (prior years' results)", retainedFromPnl));
        eqLines.Add(new StatementLine("", "Profit / (loss) for the current year", currentProfit));
        var equity = new StatementSection("Equity", eqLines, eqLines.Sum(l => l.Amount));

        var totalAssets = nca.Total + ca.Total;
        var totalEqLiab = equity.Total + ncl.Total + cl.Total;
        return new FinancialStatementDto("Statement of financial position", fyStart, asOf, settings.BaseCurrency,
            [nca, ca, new StatementSection("Total assets", [], totalAssets), equity, ncl, cl, new StatementSection("Total equity and liabilities", [], totalEqLiab)],
            totalAssets - totalEqLiab, "Difference (should be zero)");
    }

    public async Task<GeneralLedgerDto> GeneralLedgerAsync(Guid accountId, Guid? entityId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var scope = await ScopeAsync(entityId, ct);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct) ?? throw new NotFoundException("Account");
        var debitNature = account.Type is AccountType.Asset or AccountType.Expense;
        var lines = Posted(scope).Where(l => l.AccountId == accountId);

        var openingFrom = IsPnl(account.Type) ? await FiscalYearStartAsync(from, ct) : DateOnly.MinValue;
        var op = await lines.Where(l => l.JournalEntry!.Date < from && l.JournalEntry.Date >= openingFrom)
            .GroupBy(l => 1).Select(g => new { D = g.Sum(l => l.BaseDebit), C = g.Sum(l => l.BaseCredit) }).FirstOrDefaultAsync(ct);
        var opening = op == null ? 0 : debitNature ? op.D - op.C : op.C - op.D;

        var rows = await lines.Where(l => l.JournalEntry!.Date >= from && l.JournalEntry.Date <= to)
            .OrderBy(l => l.JournalEntry!.Date).ThenBy(l => l.JournalEntry!.Number)
            .Select(l => new
            {
                l.JournalEntry!.Date, l.JournalEntry.Number, l.JournalEntryId, Description = l.Description ?? l.JournalEntry.Description,
                Entity = db.Entities.Where(e => e.Id == l.EntityId).Select(e => e.Name).FirstOrDefault(),
                Contact = l.ContactId == null ? null : db.Contacts.Where(c => c.Id == l.ContactId).Select(c => c.Name).FirstOrDefault(),
                l.BaseDebit, l.BaseCredit
            }).ToListAsync(ct);

        var running = opening;
        var result = rows.Select(r =>
        {
            running += debitNature ? r.BaseDebit - r.BaseCredit : r.BaseCredit - r.BaseDebit;
            return new LedgerRow(r.Date, r.Number, r.JournalEntryId, r.Description, r.Entity, r.Contact, r.BaseDebit, r.BaseCredit, running);
        }).ToList();
        return new GeneralLedgerDto(account.Id, account.Code, account.Name, from, to, opening, result, running);
    }

    public async Task<AgingDto> AgingAsync(DocumentKind kind, Guid? entityId, DateOnly asOf, CancellationToken ct)
    {
        var scope = await ScopeAsync(entityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var docs = await db.FinanceDocuments
            .Where(d => d.Kind == kind && scope.Contains(d.EntityId) && d.Date <= asOf && (d.Status == DocumentStatus.Open || d.Status == DocumentStatus.PartiallyPaid))
            .Select(d => new { d.ContactId, ContactName = d.Contact!.Name, d.DueDate, Outstanding = d.BaseTotal - d.BasePaid }).ToListAsync(ct);

        var rows = docs.GroupBy(d => new { d.ContactId, d.ContactName }).Select(g =>
        {
            decimal Bucket(int min, int max) => g.Where(d => { var days = asOf.DayNumber - d.DueDate.DayNumber; return days >= min && days <= max; }).Sum(d => d.Outstanding);
            return new AgingRow(g.Key.ContactId, g.Key.ContactName, Bucket(int.MinValue, 0), Bucket(1, 30), Bucket(31, 60), Bucket(61, 90),
                Bucket(91, int.MaxValue), g.Sum(d => d.Outstanding));
        }).OrderByDescending(r => r.Total).ToList();
        var totals = new AgingRow(Guid.Empty, "Total", rows.Sum(r => r.Current), rows.Sum(r => r.Days1To30), rows.Sum(r => r.Days31To60),
            rows.Sum(r => r.Days61To90), rows.Sum(r => r.Over90), rows.Sum(r => r.Total));
        return new AgingDto(kind, asOf, settings.BaseCurrency, rows, totals);
    }

    /// <summary>Sales tax return summary from approved invoices (output) and bills (input), in base currency.</summary>
    public async Task<SalesTaxReportDto> SalesTaxAsync(Guid? entityId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var scope = await ScopeAsync(entityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var counted = new[] { DocumentStatus.Open, DocumentStatus.PartiallyPaid, DocumentStatus.Paid };
        var lines = await db.FinanceDocumentLines
            .Join(db.FinanceDocuments, l => l.DocumentId, d => d.Id, (l, d) => new { l, d })
            .Where(x => x.l.TaxRateId != null && scope.Contains(x.d.EntityId) && counted.Contains(x.d.Status) && x.d.Date >= from && x.d.Date <= to)
            .Select(x => new { TaxRateId = x.l.TaxRateId!.Value, x.d.Kind, Value = x.l.Amount * x.d.ExchangeRate, Tax = x.l.TaxAmount * x.d.ExchangeRate })
            .ToListAsync(ct);
        var rates = await db.TaxRates.ToDictionaryAsync(t => t.Id, ct);

        var rows = lines.GroupBy(x => x.TaxRateId).Select(g =>
        {
            var t = rates[g.Key];
            var sales = g.Where(x => x.Kind == DocumentKind.Invoice).ToList();
            var purchases = g.Where(x => x.Kind == DocumentKind.Bill).ToList();
            var outTax = LedgerService.Round(sales.Sum(x => x.Tax));
            var inTax = LedgerService.Round(purchases.Sum(x => x.Tax));
            return new SalesTaxRow(t.Id, t.Code, t.Name, t.Authority, t.Rate, LedgerService.Round(sales.Sum(x => x.Value)), outTax,
                LedgerService.Round(purchases.Sum(x => x.Value)), inTax, outTax - inTax);
        }).OrderBy(r => r.Authority).ThenBy(r => r.Code).ToList();
        return new SalesTaxReportDto(from, to, settings.BaseCurrency, rows, rows.Sum(r => r.OutputTax), rows.Sum(r => r.InputTax), rows.Sum(r => r.Net));
    }

    public async Task<FinanceDashboardDto> DashboardAsync(Guid? entityId, CancellationToken ct)
    {
        var scope = await ScopeAsync(entityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fyStart = await FiscalYearStartAsync(today, ct);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, ct);
        var all = await BalancesAsync(scope, null, today, ct);
        var year = await BalancesAsync(scope, fyStart, today, ct);

        decimal Net(IEnumerable<Bal> b, Func<Account, bool> pick) => b.Where(x => pick(accounts[x.AccountId])).Sum(x => x.Debit - x.Credit);
        var open = new[] { DocumentStatus.Open, DocumentStatus.PartiallyPaid };
        var overdue = await db.FinanceDocuments.Where(d => d.Kind == DocumentKind.Invoice && scope.Contains(d.EntityId) && open.Contains(d.Status) && d.DueDate < today)
            .SumAsync(d => d.BaseTotal - d.BasePaid, ct);

        var monthly = await Posted(scope).Where(l => l.JournalEntry!.Date >= fyStart && l.JournalEntry.Date <= today &&
                                                     (l.Account!.Type == AccountType.Income || l.Account.Type == AccountType.Expense))
            // Group by the date column itself: DateOnly members can't be translated through the value converter.
            .GroupBy(l => new { l.JournalEntry!.Date, l.Account!.Type })
            .Select(g => new { g.Key.Date, g.Key.Type, D = g.Sum(l => l.BaseDebit), C = g.Sum(l => l.BaseCredit) })
            .ToListAsync(ct);
        var points = new List<MonthlyPoint>();
        for (var m = fyStart; m <= today; m = m.AddMonths(1))
        {
            var rows = monthly.Where(x => x.Date.Year == m.Year && x.Date.Month == m.Month).ToList();
            points.Add(new MonthlyPoint(m.Year, m.Month, rows.Where(x => x.Type == AccountType.Income).Sum(x => x.C - x.D),
                rows.Where(x => x.Type == AccountType.Expense).Sum(x => x.D - x.C)));
        }

        var income = -Net(year, a => a.Type == AccountType.Income);
        var expenses = Net(year, a => a.Type == AccountType.Expense);
        return new FinanceDashboardDto(settings.BaseCurrency, Net(all, a => a.SubType is AccountSubType.Bank or AccountSubType.Cash),
            Net(all, a => a.SubType == AccountSubType.Receivable), -Net(all, a => a.SubType == AccountSubType.Payable), overdue,
            income, expenses, income - expenses, points);
    }
}
