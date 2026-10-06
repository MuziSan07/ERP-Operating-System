using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class AccountConfig : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("fin_accounts");
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class FinanceSettingsConfig : IEntityTypeConfiguration<FinanceSettings>
{
    public void Configure(EntityTypeBuilder<FinanceSettings> b)
    {
        b.ToTable("fin_settings");
        b.Property(x => x.BaseCurrency).HasMaxLength(3);
        b.Property(x => x.Ntn).HasMaxLength(20);
        b.Property(x => x.Strn).HasMaxLength(20);
        b.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class ExchangeRateConfig : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("fin_exchange_rates");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Rate).HasPrecision(18, 6);
        b.HasIndex(x => new { x.TenantId, x.Currency, x.Date });
    }
}

public class TaxRateConfig : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> b)
    {
        b.ToTable("fin_tax_rates");
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Authority).HasMaxLength(50);
        b.Property(x => x.Rate).HasPrecision(9, 4);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class ContactConfig : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> b)
    {
        b.ToTable("fin_contacts");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.Ntn).HasMaxLength(20);
        b.Property(x => x.Strn).HasMaxLength(20);
        b.Property(x => x.Cnic).HasMaxLength(15);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.HasIndex(x => new { x.TenantId, x.Code });
        b.HasIndex(x => new { x.TenantId, x.Name });
    }
}

public class NumberSequenceConfig : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> b)
    {
        b.ToTable("fin_number_sequences");
        b.Property(x => x.Key).HasMaxLength(50).IsRequired();
        // Required by the INSERT … ON DUPLICATE KEY UPDATE counter in AppDbContext.NextSequenceAsync.
        b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
    }
}

public class JournalEntryConfig : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b)
    {
        b.ToTable("fin_journal_entries");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(l => l.JournalEntry).HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Date });
        b.HasIndex(x => new { x.TenantId, x.Number });
        b.HasIndex(x => new { x.Source, x.SourceId });
    }
}

public class JournalLineConfig : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> b)
    {
        b.ToTable("fin_journal_lines");
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AccountId, x.EntityId });
        b.HasIndex(x => x.ContactId);
    }
}

public class FinanceDocumentConfig : IEntityTypeConfiguration<FinanceDocument>
{
    public void Configure(EntityTypeBuilder<FinanceDocument> b)
    {
        // Concurrency guard: a second simultaneous change to these fails with 409 instead of double-posting.
        b.Property(x => x.Status).IsConcurrencyToken();
        b.Property(x => x.AmountPaid).IsConcurrencyToken();
        b.ToTable("fin_documents");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Kind, x.Status });
        b.HasIndex(x => new { x.TenantId, x.ContactId });
    }
}

public class FinanceDocumentLineConfig : IEntityTypeConfiguration<FinanceDocumentLine>
{
    public void Configure(EntityTypeBuilder<FinanceDocumentLine> b)
    {
        b.ToTable("fin_document_lines");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitPrice).HasPrecision(18, 4);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TaxRate).WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        // Concurrency guard: a second simultaneous change to these fails with 409 instead of double-posting.
        b.Property(x => x.IsVoid).IsConcurrencyToken();
        b.ToTable("fin_payments");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Allocations).WithOne().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Date });
    }
}

public class PaymentAllocationConfig : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> b)
    {
        b.ToTable("fin_payment_allocations");
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
