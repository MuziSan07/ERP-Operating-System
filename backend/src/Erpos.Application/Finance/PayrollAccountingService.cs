using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Accrual journal for a posted payroll run, split by the entity of each payslip:
///   Dr Salaries expense (gross)              Cr Income tax withheld
///   Dr EOBI / PF / social security expense   Cr EOBI / PF / social security payable (employee + employer)
///                                            Cr Other payroll deductions (loan recovery…)
///                                            Cr Salaries payable (net pay)
/// Paying salaries later: Dr Salaries payable / Cr Bank.
/// </summary>
public class PayrollAccountingService(IAppDbContext db, IAccessService access, LedgerService ledger)
{
    /// <summary>Called when a run is posted. Skipped when the run's entity doesn't use the Finance module.</summary>
    public async Task PostRunAsync(PayrollRun run, CancellationToken ct)
    {
        if (!await db.EntityModules.AnyAsync(m => m.EntityId == run.EntityId && m.ModuleCode == Modules.Finance && m.IsEnabled, ct)) return;
        var s = await ledger.SettingsAsync(ct);
        Guid Req(Guid? id, string name) => id ?? throw new ValidationException($"Set the '{name}' account in Finance settings before posting payroll.");

        var slips = await db.Payslips.Where(p => p.PayrollRunId == run.Id).Include(p => p.Lines).ToListAsync(ct);
        var lines = new List<LineInput>();
        void Dr(Guid account, decimal amount, Guid entity, string text) { if (amount > 0) lines.Add(new(account, amount, 0, entity, text)); else if (amount < 0) lines.Add(new(account, 0, -amount, entity, text)); }
        void Cr(Guid account, decimal amount, Guid entity, string text) => Dr(account, -amount, entity, text);

        foreach (var g in slips.GroupBy(p => p.EntityId))
        {
            decimal Sum(params string[] codes) => g.SelectMany(p => p.Lines).Where(l => codes.Contains(l.Code)).Sum(l => l.Amount);
            var other = g.SelectMany(p => p.Lines)
                .Where(l => l.Kind == PayComponentKind.Deduction && !l.IsEmployerContribution && l.Code is not ("TAX" or "EOBI" or "PF")).Sum(l => l.Amount);

            Dr(Req(s.SalaryExpenseAccountId, "Salary expense"), g.Sum(p => p.GrossEarnings), g.Key, "Gross salaries");
            Dr(Req(s.EobiExpenseAccountId, "EOBI expense"), Sum("EOBI_ER"), g.Key, "EOBI employer share");
            Dr(Req(s.PfExpenseAccountId, "PF expense"), Sum("PF_ER"), g.Key, "PF employer share");
            Dr(Req(s.SocialSecurityExpenseAccountId, "Social security expense"), Sum("SS_ER"), g.Key, "Social security");
            Cr(Req(s.SalaryTaxPayableAccountId, "Salary tax payable"), g.Sum(p => p.IncomeTax), g.Key, "Income tax withheld (s.149)");
            Cr(Req(s.EobiPayableAccountId, "EOBI payable"), Sum("EOBI", "EOBI_ER"), g.Key, "EOBI payable");
            Cr(Req(s.PfPayableAccountId, "PF payable"), Sum("PF", "PF_ER"), g.Key, "Provident fund payable");
            Cr(Req(s.SocialSecurityPayableAccountId, "Social security payable"), Sum("SS_ER"), g.Key, "Social security payable");
            Cr(Req(s.OtherPayrollDeductionsAccountId, "Other payroll deductions"), other, g.Key, "Other deductions");
            Cr(Req(s.SalaryPayableAccountId, "Salaries payable"), g.Sum(p => p.NetPay), g.Key, "Net salaries payable");
        }
        if (lines.Count < 2) return;

        var date = new DateOnly(run.Year, run.Month, 1).AddMonths(1).AddDays(-1);
        var entry = await ledger.BuildAndPostAsync(run.EntityId, date, $"Payroll {date:MMMM yyyy}", s.BaseCurrency, 1, JournalSource.Payroll,
            run.Id, lines, $"PAYROLL-{run.Year}-{run.Month:D2}", ct);
        run.JournalEntryId = entry.Id;
    }

    /// <summary>Records the bank transfer of a posted run's net salaries.</summary>
    public async Task<Guid> PaySalariesAsync(Guid runId, PaySalariesRequest req, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException("Payroll run");
        await access.EnsureAsync(Permissions.PaymentsCreate, run.EntityId, ct);
        if (run.Status != PayrollRunStatus.Posted || run.JournalEntryId == null) throw new ValidationException("Post the payroll (with Finance enabled) first.");
        if (run.PaymentJournalEntryId != null) throw new ValidationException("Salaries for this run are already marked as paid.");

        var s = await ledger.SettingsAsync(ct);
        var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.BankAccountId, ct) ?? throw new NotFoundException("Bank account");
        if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Choose a bank or cash account.");
        if (bank.Currency != null && bank.Currency != s.BaseCurrency) throw new ValidationException($"Pay salaries from a {s.BaseCurrency} account.");

        var lines = new List<LineInput>();
        foreach (var g in (await db.Payslips.Where(p => p.PayrollRunId == runId).ToListAsync(ct)).GroupBy(p => p.EntityId))
        {
            var net = g.Sum(p => p.NetPay);
            if (net <= 0) continue;
            lines.Add(new(s.SalaryPayableAccountId!.Value, net, 0, g.Key, "Net salaries paid"));
            lines.Add(new(bank.Id, 0, net, g.Key, "Salary transfer"));
        }
        var entry = await ledger.BuildAndPostAsync(run.EntityId, req.Date, $"Salary payment {run.Month:D2}/{run.Year}", s.BaseCurrency, 1,
            JournalSource.Payroll, run.Id, lines, $"SALPAY-{run.Year}-{run.Month:D2}", ct);
        run.PaymentJournalEntryId = entry.Id;
        await db.SaveChangesAsync(ct);
        return entry.Id;
    }
}
