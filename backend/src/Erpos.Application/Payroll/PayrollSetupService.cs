using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Hr;
using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Payroll;

/// <summary>Organization settings, salary heads, FBR tax tables and employee salary revisions.</summary>
public class PayrollSetupService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    // ---------------- Settings ----------------

    public async Task<HrSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var s = await SettingsAsync(ct);
        return ToDto(s);
    }

    public async Task<HrSettingsDto> SaveCalendarAsync(List<string> weeklyOffDays, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.HrSettingsManage, ct);
        var days = WorkCalendar.ParseWeeklyOffs(string.Join(',', weeklyOffDays));
        if (days.Count > 3) throw new ValidationException("At most three weekly off days.");
        var s = await SettingsAsync(ct);
        s.WeeklyOffDays = string.Join(',', days.OrderBy(d => d));
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<HrSettingsDto> SavePayrollSettingsAsync(HrSettingsDto req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.PayrollSettingsManage, ct);
        decimal Rate(decimal v, string name) => v is < 0 or > 1 ? throw new ValidationException($"{name} must be a fraction between 0 and 1 (e.g. 0.05 for 5%).") : v;
        if (req.MinimumWage <= 0) throw new ValidationException("Minimum wage must be positive.");

        var s = await SettingsAsync(ct);
        s.MinimumWage = req.MinimumWage;
        s.EobiEnabled = req.EobiEnabled;
        s.EobiEmployeeRate = Rate(req.EobiEmployeeRate, "EOBI employee rate");
        s.EobiEmployerRate = Rate(req.EobiEmployerRate, "EOBI employer rate");
        s.ProvidentFundEnabled = req.ProvidentFundEnabled;
        s.ProvidentFundEmployeeRate = Rate(req.ProvidentFundEmployeeRate, "PF employee rate");
        s.ProvidentFundEmployerRate = Rate(req.ProvidentFundEmployerRate, "PF employer rate");
        s.SocialSecurityEnabled = req.SocialSecurityEnabled;
        s.SocialSecurityEmployerRate = Rate(req.SocialSecurityEmployerRate, "Social security rate");
        s.SocialSecurityWageCeiling = req.SocialSecurityWageCeiling;
        s.PayrollRequiresSecondApprover = req.PayrollRequiresSecondApprover;
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    private async Task<HrSettings> SettingsAsync(CancellationToken ct)
    {
        var s = await db.HrSettings.FirstOrDefaultAsync(ct);
        if (s != null) return s;
        await HrDefaults.EnsureAsync(db, currentUser.TenantId!.Value, ct);
        await db.SaveChangesAsync(ct);
        return await db.HrSettings.FirstAsync(ct);
    }

    private static HrSettingsDto ToDto(HrSettings s) => new(
        WorkCalendar.ParseWeeklyOffs(s.WeeklyOffDays).Select(d => d.ToString()).ToList(), s.MinimumWage, s.EobiEnabled,
        s.EobiEmployeeRate, s.EobiEmployerRate, s.ProvidentFundEnabled, s.ProvidentFundEmployeeRate, s.ProvidentFundEmployerRate,
        s.SocialSecurityEnabled, s.SocialSecurityEmployerRate, s.SocialSecurityWageCeiling, s.PayrollRequiresSecondApprover);

    // ---------------- Pay components ----------------

    public Task<List<PayComponentDto>> ListComponentsAsync(CancellationToken ct) =>
        db.PayComponents.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new PayComponentDto(c.Id, c.Code, c.Name, c.Kind, c.IsTaxable, c.IsBasic, c.ExemptUpToFractionOfBasic, c.SortOrder, c.IsActive))
            .ToListAsync(ct);

    public async Task<PayComponentDto> SaveComponentAsync(Guid? id, SavePayComponentRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.PayrollSettingsManage, ct);
        var c = id == null ? null : await db.PayComponents.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Pay component");
        if (c == null)
        {
            c = new PayComponent { TenantId = currentUser.TenantId!.Value };
            db.PayComponents.Add(c);
        }
        var code = Guard.Code(req.Code);
        if (await db.PayComponents.AnyAsync(x => x.Code == code && x.Id != c.Id, ct)) throw new ValidationException($"Code '{code}' already exists.");
        if (req.IsBasic && await db.PayComponents.AnyAsync(x => x.IsBasic && x.Id != c.Id, ct))
            throw new ValidationException("Only one component can be the Basic Salary.");
        if (req.IsBasic && req.Kind != Domain.Enums.PayComponentKind.Earning) throw new ValidationException("Basic Salary must be an earning.");

        c.Code = code;
        c.Name = Guard.Required(req.Name, "Name", 100);
        c.Kind = req.Kind;
        c.IsTaxable = req.IsTaxable;
        c.IsBasic = req.IsBasic;
        c.ExemptUpToFractionOfBasic = req.ExemptUpToFractionOfBasic;
        c.SortOrder = req.SortOrder;
        c.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return new PayComponentDto(c.Id, c.Code, c.Name, c.Kind, c.IsTaxable, c.IsBasic, c.ExemptUpToFractionOfBasic, c.SortOrder, c.IsActive);
    }

    // ---------------- Tax tables ----------------

    public async Task<List<TaxYearDto>> ListTaxYearsAsync(CancellationToken ct) =>
        (await db.TaxYears.Include(t => t.Slabs).OrderByDescending(t => t.Year).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<TaxYearDto> SaveTaxYearAsync(SaveTaxYearRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.PayrollSettingsManage, ct);
        if (req.Year is < 2020 or > 2100) throw new ValidationException("Invalid tax year.");
        var slabs = req.Slabs.OrderBy(s => s.From).ToList();
        if (slabs.Count == 0 || slabs[0].From != 0) throw new ValidationException("Slabs must start at 0.");
        for (var i = 0; i < slabs.Count; i++)
        {
            if (slabs[i].Rate is < 0 or > 1) throw new ValidationException("Rates are fractions between 0 and 1 (e.g. 0.11 for 11%).");
            var expectedTo = i + 1 < slabs.Count ? slabs[i + 1].From : (decimal?)null;
            if (slabs[i].To != expectedTo) throw new ValidationException("Each slab must end where the next one starts; only the last slab is open-ended.");
        }

        var table = await db.TaxYears.Include(t => t.Slabs).FirstOrDefaultAsync(t => t.Year == req.Year, ct);
        if (table == null)
        {
            table = new TaxYear { TenantId = currentUser.TenantId!.Value, Year = req.Year };
            db.TaxYears.Add(table);
        }
        else
        {
            if (await db.Payslips.AnyAsync(p => p.PayrollRun!.Status == Domain.Enums.PayrollRunStatus.Posted &&
                                                ((p.Year == req.Year - 1 && p.Month >= 7) || (p.Year == req.Year && p.Month <= 6)), ct))
                throw new ValidationException("Payroll has already been posted in this tax year. Changes would not match posted payslips.");
            db.TaxSlabs.RemoveRange(table.Slabs);
            table.Slabs.Clear();
        }

        table.SurchargeThreshold = req.SurchargeThreshold;
        table.SurchargeRate = req.SurchargeRate;
        table.Notes = req.Notes;
        foreach (var s in slabs)
            table.Slabs.Add(new TaxSlab { TaxYearId = table.Id, From = s.From, To = s.To, FixedTax = s.FixedTax, Rate = s.Rate });
        await db.SaveChangesAsync(ct);
        return ToDto(table);
    }

    public async Task<TaxPreviewDto> PreviewTaxAsync(int taxYear, decimal monthlyTaxable, CancellationToken ct)
    {
        var table = await db.TaxYears.Include(t => t.Slabs).FirstOrDefaultAsync(t => t.Year == taxYear, ct)
                    ?? throw new NotFoundException($"Tax table for {taxYear}");
        var annualIncome = monthlyTaxable * 12;
        var annualTax = PakistanTax.AnnualTax(annualIncome, table);
        return new TaxPreviewDto(taxYear, annualIncome, annualTax, Math.Round(annualTax / 12, 0),
            annualIncome == 0 ? 0 : Math.Round(annualTax / annualIncome, 4));
    }

    private static TaxYearDto ToDto(TaxYear t) => new(t.Id, t.Year, t.SurchargeThreshold, t.SurchargeRate, t.Notes,
        t.Slabs.OrderBy(s => s.From).Select(s => new TaxSlabDto(s.From, s.To, s.FixedTax, s.Rate)).ToList());

    // ---------------- Employee salaries ----------------

    /// <summary>Salary history, newest first. Visible to payroll staff and to the employee themself.</summary>
    public async Task<List<SalaryDto>> SalaryHistoryAsync(Guid employeeId, CancellationToken ct)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct) ?? throw new NotFoundException("Employee");
        if (emp.UserId != currentUser.UserId) await access.EnsureAsync(Permissions.SalaryView, emp.EntityId, ct);

        var rows = await db.EmployeeSalaries.Where(s => s.EmployeeId == employeeId).OrderByDescending(s => s.EffectiveFrom)
            .Include(s => s.Lines).ThenInclude(l => l.PayComponent).ToListAsync(ct);
        return rows.Select(s =>
        {
            var lines = s.Lines.OrderBy(l => l.PayComponent!.SortOrder)
                .Select(l => new SalaryLineDto(l.PayComponentId, l.PayComponent!.Code, l.PayComponent.Name, l.PayComponent.Kind, l.Amount)).ToList();
            return new SalaryDto(s.Id, s.EffectiveFrom, s.Remarks,
                lines.Where(l => l.Kind == Domain.Enums.PayComponentKind.Earning).Sum(l => l.Amount),
                lines.Where(l => l.Kind == Domain.Enums.PayComponentKind.Deduction).Sum(l => l.Amount), lines);
        }).ToList();
    }

    public async Task<List<SalaryDto>> SaveSalaryAsync(Guid employeeId, SaveSalaryRequest req, CancellationToken ct)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct) ?? throw new NotFoundException("Employee");
        var existing = await db.EmployeeSalaries.Include(s => s.Lines).FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.EffectiveFrom == req.EffectiveFrom, ct);
        await access.EnsureAsync(existing == null ? Permissions.SalaryCreate : Permissions.SalaryEdit, emp.EntityId, ct);

        var componentIds = req.Lines.Select(l => l.PayComponentId).ToList();
        var components = await db.PayComponents.Where(c => componentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        if (components.Count != componentIds.Distinct().Count()) throw new ValidationException("Unknown or duplicate pay component.");
        if (req.Lines.Any(l => l.Amount < 0)) throw new ValidationException("Amounts cannot be negative.");
        if (!req.Lines.Any(l => components[l.PayComponentId].IsBasic && l.Amount > 0)) throw new ValidationException("Basic salary is required.");

        var locked = await db.Payslips.AnyAsync(p => p.EmployeeId == employeeId && p.PayrollRun!.Status == Domain.Enums.PayrollRunStatus.Posted &&
                                                     (p.Year > req.EffectiveFrom.Year || (p.Year == req.EffectiveFrom.Year && p.Month >= req.EffectiveFrom.Month)), ct);
        if (locked) throw new ValidationException("Payroll is already posted for that period. Use a later effective date.");

        if (existing == null)
        {
            existing = new EmployeeSalary { TenantId = emp.TenantId, EntityId = emp.EntityId, EmployeeId = emp.Id, EffectiveFrom = req.EffectiveFrom };
            db.EmployeeSalaries.Add(existing);
        }
        else
        {
            db.EmployeeSalaryLines.RemoveRange(existing.Lines);
            existing.Lines.Clear();
        }
        existing.Remarks = req.Remarks;
        foreach (var l in req.Lines.Where(l => l.Amount > 0))
            existing.Lines.Add(new EmployeeSalaryLine { EmployeeSalaryId = existing.Id, PayComponentId = l.PayComponentId, Amount = Math.Round(l.Amount, 2) });
        await db.SaveChangesAsync(ct);
        return await SalaryHistoryAsync(employeeId, ct);
    }

    public async Task<List<SalaryLineDto>> SplitGrossAsync(decimal gross, CancellationToken ct)
    {
        if (gross <= 0) throw new ValidationException("Gross must be positive.");
        var components = await db.PayComponents.ToDictionaryAsync(c => c.Code, ct);
        return HrDefaults.SplitGross(gross).Where(kv => components.ContainsKey(kv.Key))
            .Select(kv => new SalaryLineDto(components[kv.Key].Id, kv.Key, components[kv.Key].Name, components[kv.Key].Kind, kv.Value)).ToList();
    }
}
