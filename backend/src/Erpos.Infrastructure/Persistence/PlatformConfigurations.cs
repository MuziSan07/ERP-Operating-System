using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class AttachmentConfig : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("core_attachments");
        b.Property(x => x.RecordType).HasMaxLength(30).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => new { x.TenantId, x.RecordType, x.RecordId });
    }
}

public class OutboxEmailConfig : IEntityTypeConfiguration<OutboxEmail>
{
    public void Configure(EntityTypeBuilder<OutboxEmail> b)
    {
        b.ToTable("core_outbox_emails");
        b.Property(x => x.To).HasMaxLength(256).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(250).IsRequired();
        b.Property(x => x.HtmlBody).HasColumnType("mediumtext").IsRequired();
        b.Property(x => x.Category).HasMaxLength(40);
        b.Property(x => x.LastError).HasMaxLength(500);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

public class PasswordResetTokenConfig : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("core_password_reset_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.UsedAt).IsConcurrencyToken(); // a link can't be used twice even by simultaneous requests
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}

public class ReminderRunConfig : IEntityTypeConfiguration<ReminderRun>
{
    public void Configure(EntityTypeBuilder<ReminderRun> b)
    {
        b.ToTable("core_reminder_runs");
        b.HasIndex(x => new { x.TenantId, x.Date });
    }
}
