using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>A file attached to a record (invoice, employee, asset…). Access follows the record's own permissions.</summary>
public class Attachment : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    /// <summary>Kind of record, e.g. "invoice", "employee" (see AttachmentService for the list).</summary>
    public string RecordType { get; set; } = "";
    public Guid RecordId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";
    public string? Description { get; set; }
}

/// <summary>
/// Outgoing email, written in the same transaction as the business change and delivered by a background dispatcher, so a
/// mail server outage never loses a message or fails the user's action.
/// </summary>
public class OutboxEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string HtmlBody { get; set; } = "";
    /// <summary>e.g. "password-reset", "reminders"; password-reset bodies are never shown in the outbox view.</summary>
    public string Category { get; set; } = "";
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}

public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One daily reminder digest per organization.</summary>
public class ReminderRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public int Items { get; set; }
    public int Recipients { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
