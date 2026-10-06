using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Payroll;

/// <summary>
/// Starter HR/payroll data for a Pakistani organization. Everything here is editable per organization —
/// verify figures against the current Finance Act / FBR and provincial notifications before running payroll.
/// </summary>
public static class HrDefaults
{
    /// <summary>Federal minimum wage announced in the 2026-27 budget. Provinces notify their own.</summary>
    public const decimal MinimumWage = 40_700m;

    public static async Task EnsureAsync(IAppDbContext db, Guid tenantId, CancellationToken ct)
    {
        if (!await db.HrSettings.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId, ct))
            db.HrSettings.Add(new HrSettings
            {
                TenantId = tenantId, WeeklyOffDays = "Sunday", MinimumWage = MinimumWage,
                EobiEnabled = true, EobiEmployeeRate = 0.01m, EobiEmployerRate = 0.05m,
                ProvidentFundEnabled = false, ProvidentFundEmployeeRate = 0.0833m, ProvidentFundEmployerRate = 0.0833m,
                SocialSecurityEnabled = false, SocialSecurityEmployerRate = 0.06m, SocialSecurityWageCeiling = MinimumWage
            });

        if (!await db.LeaveTypes.IgnoreQueryFilters().AnyAsync(t => t.TenantId == tenantId, ct))
            db.LeaveTypes.AddRange(
                new LeaveType { TenantId = tenantId, Code = "AL", Name = "Annual Leave", DaysPerYear = 14 },
                new LeaveType { TenantId = tenantId, Code = "CL", Name = "Casual Leave", DaysPerYear = 10 },
                new LeaveType { TenantId = tenantId, Code = "SL", Name = "Sick Leave", DaysPerYear = 8 },
                new LeaveType { TenantId = tenantId, Code = "ML", Name = "Maternity Leave", DaysPerYear = 90, AllowHalfDay = false, OnlyForGender = Gender.Female },
                new LeaveType { TenantId = tenantId, Code = "PL", Name = "Paternity Leave", DaysPerYear = 30, AllowHalfDay = false, OnlyForGender = Gender.Male },
                new LeaveType { TenantId = tenantId, Code = "UL", Name = "Unpaid Leave", DaysPerYear = 0, IsPaid = false });

        if (!await db.LeaveApprovalSteps.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId, ct))
            db.LeaveApprovalSteps.AddRange(
                new LeaveApprovalStep { TenantId = tenantId, StepOrder = 1, Name = "Line manager", ApproverType = ApproverType.LineManager },
                new LeaveApprovalStep { TenantId = tenantId, StepOrder = 2, Name = "HR", ApproverType = ApproverType.HrPermission });

        if (!await db.PayComponents.IgnoreQueryFilters().AnyAsync(c => c.TenantId == tenantId, ct))
            db.PayComponents.AddRange(
                new PayComponent { TenantId = tenantId, Code = "BASIC", Name = "Basic Salary", Kind = PayComponentKind.Earning, IsBasic = true, SortOrder = 1 },
                new PayComponent { TenantId = tenantId, Code = "HRA", Name = "House Rent Allowance", Kind = PayComponentKind.Earning, SortOrder = 2 },
                // Clause 139, Part I, 2nd Schedule: medical allowance exempt up to 10% of basic.
                new PayComponent { TenantId = tenantId, Code = "MEDICAL", Name = "Medical Allowance", Kind = PayComponentKind.Earning, ExemptUpToFractionOfBasic = 0.10m, SortOrder = 3 },
                new PayComponent { TenantId = tenantId, Code = "UTIL", Name = "Utilities Allowance", Kind = PayComponentKind.Earning, SortOrder = 4 },
                new PayComponent { TenantId = tenantId, Code = "CONV", Name = "Conveyance Allowance", Kind = PayComponentKind.Earning, SortOrder = 5 },
                new PayComponent { TenantId = tenantId, Code = "OTHER", Name = "Other Allowance", Kind = PayComponentKind.Earning, SortOrder = 6 },
                new PayComponent { TenantId = tenantId, Code = "LOAN", Name = "Loan / Advance Recovery", Kind = PayComponentKind.Deduction, IsTaxable = false, SortOrder = 20 },
                new PayComponent { TenantId = tenantId, Code = "OTHDED", Name = "Other Deduction", Kind = PayComponentKind.Deduction, IsTaxable = false, SortOrder = 21 });

        var years = await db.TaxYears.IgnoreQueryFilters().Where(y => y.TenantId == tenantId && !y.IsDeleted)
            .Select(y => y.Year).ToListAsync(ct);
        foreach (var table in TaxTables(tenantId).Where(t => !years.Contains(t.Year)))
            db.TaxYears.Add(table);
    }

    /// <summary>Common split of a gross salary: Basic 2/3, with house rent, medical (10% of basic) and utilities.</summary>
    public static IReadOnlyDictionary<string, decimal> SplitGross(decimal gross)
    {
        var basic = Math.Round(gross / 1.5m, 0);
        var medical = Math.Round(basic * 0.10m, 0);
        var utilities = Math.Round(gross * 0.0333m, 0);
        var hra = gross - basic - medical - utilities;
        return new Dictionary<string, decimal> { ["BASIC"] = basic, ["HRA"] = hra, ["MEDICAL"] = medical, ["UTIL"] = utilities };
    }

    private static IEnumerable<TaxYear> TaxTables(Guid tenantId)
    {
        TaxYear Make(int year, string notes, decimal? surchargeAt, decimal surcharge, params (decimal from, decimal? to, decimal fixedTax, decimal rate)[] slabs)
        {
            var t = new TaxYear { TenantId = tenantId, Year = year, Notes = notes, SurchargeThreshold = surchargeAt, SurchargeRate = surcharge };
            foreach (var s in slabs)
                t.Slabs.Add(new TaxSlab { TaxYearId = t.Id, From = s.from, To = s.to, FixedTax = s.fixedTax, Rate = s.rate });
            return t;
        }

        yield return Make(2026, "Finance Act 2025 (July 2025 – June 2026). 9% surcharge on tax above Rs 10m income.", 10_000_000m, 0.09m,
            (0, 600_000, 0, 0),
            (600_000, 1_200_000, 0, 0.01m),
            (1_200_000, 2_200_000, 6_000, 0.11m),
            (2_200_000, 3_200_000, 116_000, 0.23m),
            (3_200_000, 4_100_000, 346_000, 0.30m),
            (4_100_000, null, 616_000, 0.35m));

        yield return Make(2027, "Finance Act 2026 (July 2026 – June 2027). Surcharge abolished. Verify against the FBR notification.", null, 0m,
            (0, 600_000, 0, 0),
            (600_000, 1_200_000, 0, 0.01m),
            (1_200_000, 2_200_000, 6_000, 0.11m),
            (2_200_000, 3_200_000, 116_000, 0.20m),
            (3_200_000, 4_100_000, 316_000, 0.25m),
            (4_100_000, 5_600_000, 541_000, 0.29m),
            (5_600_000, 7_000_000, 976_000, 0.32m),
            (7_000_000, null, 1_424_000, 0.35m));
    }
}
