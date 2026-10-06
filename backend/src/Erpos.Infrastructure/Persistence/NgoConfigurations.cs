using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class DonorConfig : IEntityTypeConfiguration<Donor>
{
    public void Configure(EntityTypeBuilder<Donor> b)
    {
        b.ToTable("ngo_donors");
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ContactId).IsUnique();
    }
}

public class FundConfig : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> b)
    {
        b.ToTable("ngo_funds");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Purpose).HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.Code });
        b.HasIndex(x => x.GrantId);
    }
}

public class NgoProgramConfig : IEntityTypeConfiguration<NgoProgram>
{
    public void Configure(EntityTypeBuilder<NgoProgram> b)
    {
        b.ToTable("ngo_programs");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Sector).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class GrantConfig : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> b)
    {
        b.ToTable("ngo_grants");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.AgreementRef).HasMaxLength(100);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.AgreementRate).HasPrecision(18, 6);
        b.Property(x => x.FlexibilityPercent).HasPrecision(6, 2);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Donor).WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Fund).WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.BudgetLines).WithOne().HasForeignKey(l => l.GrantId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Tranches).WithOne().HasForeignKey(t => t.GrantId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Reports).WithOne().HasForeignKey(r => r.GrantId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Number });
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class GrantBudgetLineConfig : IEntityTypeConfiguration<GrantBudgetLine>
{
    public void Configure(EntityTypeBuilder<GrantBudgetLine> b)
    {
        b.ToTable("ngo_grant_budget_lines");
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
    }
}

public class GrantTrancheConfig : IEntityTypeConfiguration<GrantTranche>
{
    public void Configure(EntityTypeBuilder<GrantTranche> b)
    {
        // Concurrency guard: a second simultaneous change to these fails with 409 instead of double-posting.
        b.Property(x => x.ReceivedDate).IsConcurrencyToken();
        b.ToTable("ngo_grant_tranches");
        b.Property(x => x.Condition).HasMaxLength(300);
    }
}

public class GrantReportConfig : IEntityTypeConfiguration<GrantReport>
{
    public void Configure(EntityTypeBuilder<GrantReport> b)
    {
        b.ToTable("ngo_grant_reports");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
    }
}

public class DonationConfig : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> b)
    {
        b.ToTable("ngo_donations");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Donor).WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Fund).WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Number });
        b.HasIndex(x => new { x.TenantId, x.Date });
    }
}

public class FundExpenseConfig : IEntityTypeConfiguration<FundExpense>
{
    public void Configure(EntityTypeBuilder<FundExpense> b)
    {
        b.ToTable("ngo_fund_expenses");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.HasOne(x => x.Fund).WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.GrantId);
        b.HasIndex(x => x.BudgetLineId);
        b.HasIndex(x => new { x.TenantId, x.Date });
    }
}

public class BeneficiaryConfig : IEntityTypeConfiguration<Beneficiary>
{
    public void Configure(EntityTypeBuilder<Beneficiary> b)
    {
        b.ToTable("ngo_beneficiaries");
        b.Property(x => x.RegistrationNo).HasMaxLength(20);
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Cnic).HasMaxLength(15);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.District).HasMaxLength(100);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Vulnerabilities).HasMaxLength(500);
        b.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Cnic });
        b.HasIndex(x => new { x.TenantId, x.RegistrationNo });
    }
}

public class AssistanceConfig : IEntityTypeConfiguration<Assistance>
{
    public void Configure(EntityTypeBuilder<Assistance> b)
    {
        b.ToTable("ngo_assistance");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.HasOne(x => x.Beneficiary).WithMany().HasForeignKey(x => x.BeneficiaryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BeneficiaryId, x.Date });
    }
}
