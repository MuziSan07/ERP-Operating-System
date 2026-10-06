using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class DepartmentConfig : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("hr_departments");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.HeadEmployee).WithMany().HasForeignKey(x => x.HeadEmployeeId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.EntityId, x.Code });
    }
}

public class DesignationConfig : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> b)
    {
        b.ToTable("hr_designations");
        b.Property(x => x.Title).HasMaxLength(100).IsRequired();
        b.Property(x => x.Grade).HasMaxLength(20);
        b.HasIndex(x => new { x.TenantId, x.Title });
    }
}

public class EmployeeConfig : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("hr_employees");
        b.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
        foreach (var p in new[] { nameof(Employee.FatherName), nameof(Employee.City), nameof(Employee.EmergencyContactName), nameof(Employee.BankName), nameof(Employee.BankAccountTitle) })
            b.Property(p).HasMaxLength(150);
        b.Property(x => x.Cnic).HasMaxLength(15);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(50);
        b.Property(x => x.Iban).HasMaxLength(34);
        b.Property(x => x.Ntn).HasMaxLength(20);
        b.Property(x => x.EobiNumber).HasMaxLength(30);

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Designation).WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.TenantId, x.EmployeeCode });
        b.HasIndex(x => new { x.TenantId, x.EntityId });
    }
}

public class HolidayConfig : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("hr_holidays");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Date });
    }
}

public class AttendanceConfig : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.ToTable("hr_attendance");
        b.Property(x => x.Remarks).HasMaxLength(300);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Date });
    }
}

public class LeaveTypeConfig : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> b)
    {
        b.ToTable("hr_leave_types");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.DaysPerYear).HasPrecision(6, 1);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class LeaveStepConfig : IEntityTypeConfiguration<LeaveApprovalStep>
{
    public void Configure(EntityTypeBuilder<LeaveApprovalStep> b)
    {
        b.ToTable("hr_leave_workflow");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.StepOrder });
    }
}

public class LeaveRequestConfig : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("hr_leave_requests");
        b.Property(x => x.Days).HasPrecision(6, 1);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Approvals).WithOne().HasForeignKey(a => a.LeaveRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmployeeId, x.FromDate });
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class LeaveApprovalConfig : IEntityTypeConfiguration<LeaveApproval>
{
    public void Configure(EntityTypeBuilder<LeaveApproval> b)
    {
        b.ToTable("hr_leave_approvals");
        b.Property(x => x.StepName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.HasIndex(x => x.ApproverUserId);
    }
}

public class HrSettingsConfig : IEntityTypeConfiguration<HrSettings>
{
    public void Configure(EntityTypeBuilder<HrSettings> b)
    {
        b.ToTable("hr_settings");
        b.Property(x => x.WeeklyOffDays).HasMaxLength(100);
        foreach (var p in new[] { nameof(HrSettings.EobiEmployeeRate), nameof(HrSettings.EobiEmployerRate), nameof(HrSettings.ProvidentFundEmployeeRate),
                     nameof(HrSettings.ProvidentFundEmployerRate), nameof(HrSettings.SocialSecurityEmployerRate) })
            b.Property(p).HasPrecision(9, 4);
        b.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class PayComponentConfig : IEntityTypeConfiguration<PayComponent>
{
    public void Configure(EntityTypeBuilder<PayComponent> b)
    {
        b.ToTable("pay_components");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.ExemptUpToFractionOfBasic).HasPrecision(9, 4);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class EmployeeSalaryConfig : IEntityTypeConfiguration<EmployeeSalary>
{
    public void Configure(EntityTypeBuilder<EmployeeSalary> b)
    {
        b.ToTable("pay_employee_salaries");
        b.Property(x => x.Remarks).HasMaxLength(300);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.EmployeeSalaryId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
    }
}

public class EmployeeSalaryLineConfig : IEntityTypeConfiguration<EmployeeSalaryLine>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryLine> b)
    {
        b.ToTable("pay_employee_salary_lines");
        b.HasOne(x => x.PayComponent).WithMany().HasForeignKey(x => x.PayComponentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TaxYearConfig : IEntityTypeConfiguration<TaxYear>
{
    public void Configure(EntityTypeBuilder<TaxYear> b)
    {
        b.ToTable("pay_tax_years");
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.SurchargeRate).HasPrecision(9, 4);
        b.HasMany(x => x.Slabs).WithOne().HasForeignKey(s => s.TaxYearId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Year });
    }
}

public class TaxSlabConfig : IEntityTypeConfiguration<TaxSlab>
{
    public void Configure(EntityTypeBuilder<TaxSlab> b)
    {
        b.ToTable("pay_tax_slabs");
        b.Property(x => x.Rate).HasPrecision(9, 4);
    }
}

public class PayrollRunConfig : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> b)
    {
        b.ToTable("pay_runs");
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.Warnings).HasColumnType("text");
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payslips).WithOne(p => p.PayrollRun).HasForeignKey(p => p.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Adjustments).WithOne().HasForeignKey(a => a.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Year, x.Month });
    }
}

public class PayrollAdjustmentConfig : IEntityTypeConfiguration<PayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<PayrollAdjustment> b)
    {
        b.ToTable("pay_adjustments");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
    }
}

public class PayslipConfig : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> b)
    {
        b.ToTable("pay_payslips");
        b.Property(x => x.EmployeeCode).HasMaxLength(50);
        b.Property(x => x.EmployeeName).HasMaxLength(200);
        foreach (var p in new[] { nameof(Payslip.Designation), nameof(Payslip.Department), nameof(Payslip.EntityName), nameof(Payslip.BankName) })
            b.Property(p).HasMaxLength(200);
        b.Property(x => x.Cnic).HasMaxLength(15);
        b.Property(x => x.Iban).HasMaxLength(34);
        b.Property(x => x.UnpaidDays).HasPrecision(6, 1);
        b.Property(x => x.PayableDays).HasPrecision(6, 1);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PayslipId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmployeeId, x.Year, x.Month });
    }
}

public class PayslipLineConfig : IEntityTypeConfiguration<PayslipLine>
{
    public void Configure(EntityTypeBuilder<PayslipLine> b)
    {
        b.ToTable("pay_payslip_lines");
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(150);
    }
}
