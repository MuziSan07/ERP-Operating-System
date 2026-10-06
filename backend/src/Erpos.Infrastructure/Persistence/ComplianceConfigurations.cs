using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class WithholdingTaxRateConfig : IEntityTypeConfiguration<WithholdingTaxRate>
{
    public void Configure(EntityTypeBuilder<WithholdingTaxRate> b)
    {
        b.ToTable("fin_wht_rates");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Section).HasMaxLength(30).IsRequired();
        b.Property(x => x.Rate).HasPrecision(9, 6);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class WhtDepositConfig : IEntityTypeConfiguration<WhtDeposit>
{
    public void Configure(EntityTypeBuilder<WhtDeposit> b)
    {
        b.ToTable("fin_wht_deposits");
        b.Property(x => x.CprNumber).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.EntityId, x.Year, x.Month });
    }
}

public class BankReconciliationConfig : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> b)
    {
        b.ToTable("fin_bank_reconciliations");
        b.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ReconciliationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.BankAccountId, x.Status });
    }
}

public class BankStatementLineConfig : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> b)
    {
        b.ToTable("fin_bank_statement_lines");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.JournalLineId).IsConcurrencyToken();
        b.HasIndex(x => x.JournalLineId);
    }
}
