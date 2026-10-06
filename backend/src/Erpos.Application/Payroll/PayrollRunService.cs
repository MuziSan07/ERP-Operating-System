using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Hr;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Payroll;

/// <summary>
/// Monthly payroll: Draft (calculate, adjust, recalculate) → Approved (locks attendance/leave) → Posted (final).
///
/// Per employee:
///  • Unpaid days = days before joining / after leaving + Absent (1) / Half day (0.5) + approved unpaid leave.
///    Days without an attendance record are paid.
///  • Earnings are prorated by payable days / calendar days; fixed deductions (loan recovery) are not.
///  • Income tax: section 149 withholding = (annual tax on projected income − tax already withheld this tax year)
///    ÷ months remaining, using the organization's FBR table for that tax year.
///  • EOBI on the minimum wage; PF and social security when enabled for the organization and the employee.
/// </summary>
public class PayrollRunService(IAppDbContext db, IAccessService access, ICurrentUser currentUser,
    Finance.PayrollAccountingService accounting)
{
    public async Task<List<PayrollRunDto>> ListAsync(int? year, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.PayrollView, ct);
        var q = db.PayrollRuns.Where(r => visible.Contains(r.EntityId));
        if (year != null) q = q.Where(r => r.Year == year);
        return await Project(q.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ThenBy(r => r.Entity!.Name)).ToListAsync(ct);
    }

    public async Task<PayrollRunDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var run = await Project(db.PayrollRuns.Where(r => r.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Payroll run");
        await access.EnsureAsync(Permissions.PayrollView, run.EntityId, ct);
        var payslips = await PayslipQuery(db.Payslips.Where(p => p.PayrollRunId == id).OrderBy(p => p.EmployeeCode), ct);
        var adjustments = await db.PayrollAdjustments.Where(a => a.PayrollRunId == id)
            .Select(a => new PayrollAdjustmentDto(a.Id, a.EmployeeId, a.Name, a.Kind, a.Amount, a.IsTaxable)).ToListAsync(ct);
        return new PayrollRunDetailDto(run, payslips, adjustments);
    }

    public async Task<PayrollRunDetailDto> CreateAsync(CreatePayrollRunRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.PayrollCreate, req.EntityId, ct);
        if (req.Month is < 1 or > 12 || req.Year is < 2020 or > 2100) throw new ValidationException("Invalid payroll month.");
        if (new DateOnly(req.Year, req.Month, 1) > DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1))
            throw new ValidationException("Payroll can't be run more than a month ahead.");
        if (await db.PayrollRuns.AnyAsync(r => r.EntityId == req.EntityId && r.Year == req.Year && r.Month == req.Month &&
                                               r.Status != PayrollRunStatus.Cancelled, ct))
            throw new ValidationException("A payroll run already exists for this entity and month. Recalculate or cancel it.");

        var run = new PayrollRun
        {
            TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Year = req.Year, Month = req.Month,
            IncludeSubEntities = req.IncludeSubEntities, Notes = req.Notes
        };
        db.PayrollRuns.Add(run);
        await CalculateAsync(run, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(run.Id, ct);
    }

    public async Task<PayrollRunDetailDto> RecalculateAsync(Guid id, CancellationToken ct)
    {
        var run = await LoadDraftAsync(id, ct);
        await CalculateAsync(run, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayrollRunDetailDto> AddAdjustmentAsync(Guid id, AddAdjustmentRequest req, CancellationToken ct)
    {
        var run = await LoadDraftAsync(id, ct);
        if (!await db.Payslips.AnyAsync(p => p.PayrollRunId == id && p.EmployeeId == req.EmployeeId, ct))
            throw new ValidationException("That employee is not part of this payroll run.");
        if (req.Amount <= 0) throw new ValidationException("Amount must be positive.");
        db.PayrollAdjustments.Add(new PayrollAdjustment
        {
            PayrollRunId = id, EmployeeId = req.EmployeeId, Name = Guard.Required(req.Name, "Name", 100), Kind = req.Kind,
            Amount = Math.Round(req.Amount, 2), IsTaxable = req.Kind == PayComponentKind.Earning && req.IsTaxable
        });
        await db.SaveChangesAsync(ct);
        await CalculateAsync(run, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayrollRunDetailDto> RemoveAdjustmentAsync(Guid id, Guid adjustmentId, CancellationToken ct)
    {
        var run = await LoadDraftAsync(id, ct);
        var adj = await db.PayrollAdjustments.FirstOrDefaultAsync(a => a.Id == adjustmentId && a.PayrollRunId == id, ct)
                  ?? throw new NotFoundException("Adjustment");
        db.PayrollAdjustments.Remove(adj);
        await db.SaveChangesAsync(ct);
        await CalculateAsync(run, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayrollRunDetailDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Payroll run");
        await access.EnsureAsync(Permissions.PayrollApprove, run.EntityId, ct);
        if (run.Status != PayrollRunStatus.Draft) throw new ValidationException("Only draft runs can be approved.");
        var settings = await db.HrSettings.FirstOrDefaultAsync(ct);
        if (settings?.PayrollRequiresSecondApprover == true && run.CreatedBy == currentUser.UserId && !currentUser.IsSuperAdmin)
            throw new ForbiddenException("A different person must approve the payroll run they prepared.");
        if (!await db.Payslips.AnyAsync(p => p.PayrollRunId == id, ct)) throw new ValidationException("This run has no payslips.");

        run.Status = PayrollRunStatus.Approved;
        run.ApprovedBy = currentUser.UserId;
        run.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayrollRunDetailDto> PostAsync(Guid id, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Payroll run");
        await access.EnsureAsync(Permissions.PayrollPost, run.EntityId, ct);
        if (run.Status != PayrollRunStatus.Approved) throw new ValidationException("Approve the run before posting it.");
        run.Status = PayrollRunStatus.Posted;
        run.PostedBy = currentUser.UserId;
        run.PostedAt = DateTime.UtcNow;
        // Accrue salaries in the ledger in the same save, so payroll and books never disagree.
        await accounting.PostRunAsync(run, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayrollRunDetailDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Payroll run");
        await access.EnsureAsync(Permissions.PayrollApprove, run.EntityId, ct);
        if (run.Status is PayrollRunStatus.Posted or PayrollRunStatus.Cancelled)
            throw new ValidationException("Posted or cancelled runs can't be cancelled.");
        run.Status = PayrollRunStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---------------- Payslips ----------------

    public async Task<PayslipDto> PayslipAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Payslips.Where(x => x.Id == id)
            .Select(x => new { x.EntityId, x.Employee!.UserId, x.PayrollRun!.Status }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Payslip");
        var own = p.UserId == currentUser.UserId;
        if (!own || p.Status != PayrollRunStatus.Posted) await access.EnsureAsync(Permissions.PayslipsView, p.EntityId, ct);
        return (await PayslipQuery(db.Payslips.Where(x => x.Id == id), ct)).First();
    }

    /// <summary>The signed-in employee's posted payslips.</summary>
    public Task<List<PayslipDto>> MyPayslipsAsync(CancellationToken ct) =>
        PayslipQuery(db.Payslips.Where(p => p.Employee!.UserId == currentUser.UserId && p.PayrollRun!.Status == PayrollRunStatus.Posted)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month), ct);

    // ---------------- Calculation ----------------

    private async Task CalculateAsync(PayrollRun run, CancellationToken ct)
    {
        var from = new DateOnly(run.Year, run.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var daysInMonth = to.Day;
        var warnings = new List<string>();

        var settings = await db.HrSettings.FirstOrDefaultAsync(ct) ?? throw new ValidationException("HR settings are missing.");
        var taxYear = PakistanTax.TaxYearOf(run.Year, run.Month);
        var table = await db.TaxYears.Include(t => t.Slabs).FirstOrDefaultAsync(t => t.Year == taxYear, ct)
                    ?? throw new ValidationException($"Add the FBR tax table for tax year {taxYear} (July {taxYear - 1} – June {taxYear}) first.");

        var root = await db.Entities.Where(e => e.Id == run.EntityId).Select(e => new { e.Path, e.Name }).FirstAsync(ct);
        var employees = await db.Employees
            .Where(e => e.JoinDate <= to && (e.ExitDate == null || e.ExitDate >= from))
            .Where(e => run.IncludeSubEntities ? e.Entity!.Path.StartsWith(root.Path) : e.EntityId == run.EntityId)
            .Select(e => new
            {
                Emp = e, e.User!.FullName, EntityName = e.Entity!.Name, EntityPath = e.Entity.Path,
                Department = e.Department == null ? null : e.Department.Name,
                Designation = e.Designation == null ? null : e.Designation.Title
            })
            .OrderBy(e => e.Emp.EmployeeCode).ToListAsync(ct);

        // Someone already paid for this month in another run (e.g. a sub-entity's own run) is left out.
        var ids = employees.Select(e => e.Emp.Id).ToList();
        var elsewhere = (await db.Payslips.Where(p => p.Year == run.Year && p.Month == run.Month && p.PayrollRunId != run.Id &&
                                                      p.PayrollRun!.Status != PayrollRunStatus.Cancelled && ids.Contains(p.EmployeeId))
            .Select(p => p.EmployeeId).ToListAsync(ct)).ToHashSet();

        var salaries = (await db.EmployeeSalaries.Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= to)
                .Include(s => s.Lines).ThenInclude(l => l.PayComponent).ToListAsync(ct))
            .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First());

        var attendance = (await db.AttendanceRecords.Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to)
                .Select(a => new { a.EmployeeId, a.Date, a.Status }).ToListAsync(ct))
            .ToLookup(a => a.EmployeeId);
        var unpaidLeave = (await db.LeaveRequests.Where(l => ids.Contains(l.EmployeeId) && l.Status == LeaveStatus.Approved &&
                                                           !l.LeaveType!.IsPaid && l.FromDate <= to && l.ToDate >= from)
                .Select(l => new { l.EmployeeId, l.FromDate, l.ToDate, l.IsHalfDay }).ToListAsync(ct))
            .ToLookup(l => l.EmployeeId);
        var calendar = await WorkCalendar.LoadAsync(db, from, to, ct);
        var adjustments = (await db.PayrollAdjustments.Where(a => a.PayrollRunId == run.Id).ToListAsync(ct)).ToLookup(a => a.EmployeeId);

        // Taxable income and tax already withheld earlier in the same tax year (approved/posted runs only).
        var taxYearStart = new DateOnly(taxYear - 1, 7, 1);
        var prior = (await db.Payslips.Where(p => ids.Contains(p.EmployeeId) && p.PayrollRunId != run.Id &&
                                                 (p.PayrollRun!.Status == PayrollRunStatus.Approved || p.PayrollRun.Status == PayrollRunStatus.Posted) &&
                                                 ((p.Year == taxYearStart.Year && p.Month >= 7) || (p.Year == taxYear && p.Month <= 6)))
                .Select(p => new { p.EmployeeId, p.Year, p.Month, p.TaxableIncome, p.IncomeTax }).ToListAsync(ct))
            .Where(p => new DateOnly(p.Year, p.Month, 1) < from)
            .GroupBy(p => p.EmployeeId)
            .ToDictionary(g => g.Key, g => (Taxable: g.Sum(x => x.TaxableIncome), Tax: g.Sum(x => x.IncomeTax)));

        // Replace previous calculation.
        db.Payslips.RemoveRange(await db.Payslips.Where(p => p.PayrollRunId == run.Id).ToListAsync(ct));

        decimal totalGross = 0, totalDeductions = 0, totalNet = 0, totalEmployer = 0;
        foreach (var row in employees)
        {
            var emp = row.Emp;
            if (elsewhere.Contains(emp.Id)) { warnings.Add($"{emp.EmployeeCode} {row.FullName}: already in another payroll run for this month."); continue; }
            if (!salaries.TryGetValue(emp.Id, out var salary)) { warnings.Add($"{emp.EmployeeCode} {row.FullName}: no salary defined — skipped."); continue; }

            // ---- unpaid days (max weight per date so nothing is counted twice) ----
            var unpaid = new Dictionary<DateOnly, decimal>();
            void Mark(DateOnly d, decimal w) => unpaid[d] = Math.Max(unpaid.GetValueOrDefault(d), w);
            for (var d = from; d <= to; d = d.AddDays(1))
                if (d < emp.JoinDate || (emp.ExitDate != null && d > emp.ExitDate)) Mark(d, 1);
            foreach (var a in attendance[emp.Id])
                if (a.Status == AttendanceStatus.Absent) Mark(a.Date, 1);
                else if (a.Status == AttendanceStatus.HalfDay) Mark(a.Date, 0.5m);
            foreach (var l in unpaidLeave[emp.Id])
                foreach (var d in calendar.WorkingDays(l.FromDate < from ? from : l.FromDate, l.ToDate > to ? to : l.ToDate, row.EntityPath))
                    Mark(d, l.IsHalfDay ? 0.5m : 1);

            var unpaidDays = unpaid.Values.Sum();
            var payable = daysInMonth - unpaidDays;
            var factor = payable / daysInMonth;

            var slip = new Payslip
            {
                TenantId = emp.TenantId, EntityId = emp.EntityId, PayrollRunId = run.Id, EmployeeId = emp.Id, Year = run.Year, Month = run.Month,
                EmployeeCode = emp.EmployeeCode, EmployeeName = row.FullName, Designation = row.Designation, Department = row.Department,
                EntityName = row.EntityName, Cnic = emp.Cnic, Iban = emp.Iban, BankName = emp.BankName,
                DaysInMonth = daysInMonth, UnpaidDays = unpaidDays, PayableDays = payable
            };
            var order = 0;
            void Line(string code, string name, PayComponentKind kind, decimal amount, bool employer = false)
            {
                if (amount == 0) return;
                slip.Lines.Add(new PayslipLine { PayslipId = slip.Id, Code = code, Name = name, Kind = kind, Amount = amount, IsEmployerContribution = employer, SortOrder = order++ });
            }

            // ---- earnings ----
            decimal gross = 0, taxable = 0, basic = 0, exemptBase = 0;
            var exemptCaps = new List<(decimal Amount, decimal Fraction)>();
            foreach (var l in salary.Lines.Where(l => l.PayComponent!.Kind == PayComponentKind.Earning).OrderBy(l => l.PayComponent!.SortOrder))
            {
                var c = l.PayComponent!;
                var amount = Math.Round(l.Amount * factor, 0);
                slip.MonthlyGross += l.Amount;
                Line(c.Code, c.Name, PayComponentKind.Earning, amount);
                gross += amount;
                if (c.IsBasic) basic += amount;
                if (!c.IsTaxable) continue;
                taxable += amount;
                if (c.ExemptUpToFractionOfBasic is { } f) exemptCaps.Add((amount, f));
            }
            foreach (var (amount, fraction) in exemptCaps) exemptBase += Math.Min(amount, Math.Round(basic * fraction, 0));
            taxable -= exemptBase;

            foreach (var a in adjustments[emp.Id].Where(a => a.Kind == PayComponentKind.Earning))
            {
                Line("ADJ", a.Name, PayComponentKind.Earning, a.Amount);
                gross += a.Amount;
                if (a.IsTaxable) taxable += a.Amount;
            }

            // ---- statutory deductions ----
            var (priorTaxable, priorTax) = prior.GetValueOrDefault(emp.Id);
            var (tax, projected, annualTax) = PakistanTax.MonthlyWithholding(run.Month, taxable, priorTaxable, priorTax, table);
            Line("TAX", "Income Tax", PayComponentKind.Deduction, tax);
            decimal deductions = tax, employerCost = 0;

            if (settings.EobiEnabled && emp.EobiMember && payable > 0)
            {
                var eobiEmp = Math.Round(settings.MinimumWage * settings.EobiEmployeeRate, 0);
                var eobiEr = Math.Round(settings.MinimumWage * settings.EobiEmployerRate, 0);
                Line("EOBI", "EOBI (employee 1%)", PayComponentKind.Deduction, eobiEmp);
                Line("EOBI_ER", "EOBI (employer)", PayComponentKind.Deduction, eobiEr, employer: true);
                deductions += eobiEmp;
                employerCost += eobiEr;
            }
            if (settings.ProvidentFundEnabled && emp.ProvidentFundMember)
            {
                var pf = Math.Round(basic * settings.ProvidentFundEmployeeRate, 0);
                var pfEr = Math.Round(basic * settings.ProvidentFundEmployerRate, 0);
                Line("PF", "Provident Fund (employee)", PayComponentKind.Deduction, pf);
                Line("PF_ER", "Provident Fund (employer)", PayComponentKind.Deduction, pfEr, employer: true);
                deductions += pf;
                employerCost += pfEr;
            }
            if (settings.SocialSecurityEnabled && emp.SocialSecurityMember && slip.MonthlyGross <= settings.SocialSecurityWageCeiling)
            {
                var ss = Math.Round(gross * settings.SocialSecurityEmployerRate, 0);
                Line("SS_ER", "Social Security (employer)", PayComponentKind.Deduction, ss, employer: true);
                employerCost += ss;
            }

            // ---- other deductions (fixed salary deductions + one-off adjustments) ----
            foreach (var l in salary.Lines.Where(l => l.PayComponent!.Kind == PayComponentKind.Deduction))
            {
                Line(l.PayComponent!.Code, l.PayComponent.Name, PayComponentKind.Deduction, l.Amount);
                deductions += l.Amount;
            }
            foreach (var a in adjustments[emp.Id].Where(a => a.Kind == PayComponentKind.Deduction))
            {
                Line("ADJ", a.Name, PayComponentKind.Deduction, a.Amount);
                deductions += a.Amount;
            }

            slip.GrossEarnings = gross;
            slip.TaxableIncome = taxable;
            slip.IncomeTax = tax;
            slip.TotalDeductions = deductions;
            slip.NetPay = gross - deductions;
            slip.EmployerContributions = employerCost;
            slip.ProjectedAnnualTaxable = projected;
            slip.ProjectedAnnualTax = annualTax;
            if (slip.NetPay < 0) warnings.Add($"{emp.EmployeeCode} {row.FullName}: deductions exceed earnings (net {slip.NetPay:N0}).");
            if (string.IsNullOrEmpty(emp.Iban)) warnings.Add($"{emp.EmployeeCode} {row.FullName}: no IBAN for bank transfer.");

            db.Payslips.Add(slip);
            totalGross += gross;
            totalDeductions += deductions;
            totalNet += slip.NetPay;
            totalEmployer += employerCost;
        }

        if (employees.Count == 0) warnings.Add("No employees were active in this entity during the month.");
        run.TotalGross = totalGross;
        run.TotalDeductions = totalDeductions;
        run.TotalNet = totalNet;
        run.TotalEmployerContributions = totalEmployer;
        run.Warnings = warnings.Count == 0 ? null : string.Join('\n', warnings);
    }

    // ---------------- helpers ----------------

    private async Task<PayrollRun> LoadDraftAsync(Guid id, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Payroll run");
        await access.EnsureAsync(Permissions.PayrollCreate, run.EntityId, ct);
        if (run.Status != PayrollRunStatus.Draft) throw new ValidationException("Only draft runs can be changed.");
        return run;
    }

    private IQueryable<PayrollRunDto> Project(IQueryable<PayrollRun> q) => q.Select(r => new PayrollRunDto(
        r.Id, r.EntityId, r.Entity!.Name, r.Year, r.Month, r.IncludeSubEntities, r.Status, r.Notes,
        db.Payslips.Count(p => p.PayrollRunId == r.Id), r.TotalGross, r.TotalDeductions, r.TotalNet, r.TotalEmployerContributions,
        db.Users.Where(u => u.Id == r.CreatedBy).Select(u => u.FullName).FirstOrDefault(), r.CreatedAt,
        db.Users.Where(u => u.Id == r.ApprovedBy).Select(u => u.FullName).FirstOrDefault(), r.ApprovedAt,
        db.Users.Where(u => u.Id == r.PostedBy).Select(u => u.FullName).FirstOrDefault(), r.PostedAt,
        r.JournalEntryId, r.PaymentJournalEntryId,
        r.Warnings == null ? new List<string>() : r.Warnings.Split('\n', StringSplitOptions.None).ToList()));

    private static async Task<List<PayslipDto>> PayslipQuery(IQueryable<Payslip> q, CancellationToken ct)
    {
        var rows = await q.Include(p => p.Lines).Include(p => p.PayrollRun).ToListAsync(ct);
        return rows.Select(p => new PayslipDto(p.Id, p.PayrollRunId, p.PayrollRun!.Status, p.EmployeeId, p.Year, p.Month,
            p.EmployeeCode, p.EmployeeName, p.Designation, p.Department, p.EntityName, p.Cnic, p.Iban, p.BankName,
            p.DaysInMonth, p.UnpaidDays, p.PayableDays, p.MonthlyGross, p.GrossEarnings, p.TaxableIncome, p.IncomeTax,
            p.TotalDeductions, p.NetPay, p.EmployerContributions, p.ProjectedAnnualTaxable, p.ProjectedAnnualTax,
            p.Lines.OrderBy(l => l.SortOrder).Select(l => new PayslipLineDto(l.Code, l.Name, l.Kind, l.Amount, l.IsEmployerContribution)).ToList()))
            .ToList();
    }
}
