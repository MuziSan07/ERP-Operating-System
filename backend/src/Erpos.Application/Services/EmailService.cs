using System.Net;
using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

/// <summary>Application settings the services need (set from configuration at startup).</summary>
public class AppOptions
{
    /// <summary>Address users open the app at; used in links inside emails (never taken from the request).</summary>
    public string PublicUrl { get; set; } = "http://localhost:5173";
    public string ProductName { get; set; } = "ERPOS";
}

public record OutboxItem(Guid Id, string To, string Subject, string Category, EmailStatus Status, int Attempts, string? LastError, DateTime CreatedAt, DateTime? SentAt);
public record OutboxView(string Mode, IReadOnlyList<OutboxItem> Items);

/// <summary>Queues emails in the outbox (saved with the caller's transaction) and renders the shared layout.</summary>
public class EmailService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, IEmailTransport transport, AppOptions app)
{
    public static string Esc(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Adds an email to the outbox; the caller saves. Delivery happens in the background.</summary>
    public void Queue(string to, string subject, string htmlContent, string category, Guid? tenantId)
    {
        db.OutboxEmails.Add(new OutboxEmail
        {
            TenantId = tenantId, To = to, Subject = subject[..Math.Min(subject.Length, 250)], Category = category,
            HtmlBody = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;max-width:640px;margin:auto;color:#1f1f1f">
                  <div style="background:#2f54eb;color:#fff;padding:14px 20px;border-radius:8px 8px 0 0;font-size:18px;font-weight:600">{Esc(app.ProductName)}</div>
                  <div style="border:1px solid #e5e5e5;border-top:0;padding:20px;border-radius:0 0 8px 8px">{htmlContent}</div>
                  <p style="color:#8c8c8c;font-size:12px;margin-top:12px">This message was sent automatically by {Esc(app.ProductName)}. Please don't reply.</p>
                </div>
                """
        });
    }

    public string Link(string path) => app.PublicUrl.TrimEnd('/') + path;

    /// <summary>The organization's recent emails for its administrators (bodies are never listed).</summary>
    public async Task<OutboxView> OutboxAsync(CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.AuditView, ct);
        var tenant = currentUser.TenantId;
        var items = await db.OutboxEmails.Where(e => e.TenantId == tenant).OrderByDescending(e => e.CreatedAt).Take(200)
            .Select(e => new OutboxItem(e.Id, e.To, e.Subject, e.Category, e.Status, e.Attempts, e.LastError, e.CreatedAt, e.SentAt)).ToListAsync(ct);
        return new OutboxView(transport.Mode, items);
    }
}
