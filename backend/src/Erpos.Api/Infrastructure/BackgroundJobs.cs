using Erpos.Application.Common;
using Erpos.Application.Services;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Erpos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Api.Infrastructure;

/// <summary>Delivers queued emails every few seconds; retries failures up to five times. Idle when email isn't configured.</summary>
public class OutboxDispatcher(IServiceScopeFactory scopes, IEmailTransport transport, IConfiguration config, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var interval = TimeSpan.FromSeconds(config.GetValue("Email:DispatchSeconds", 5));
        while (!stop.IsCancellationRequested)
        {
            try { if (transport.Mode != "disabled") await DispatchAsync(stop); }
            catch (Exception ex) when (!stop.IsCancellationRequested) { logger.LogError(ex, "Outbox dispatch failed"); }
            await Task.Delay(interval, stop).ContinueWith(_ => { });
        }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.OutboxEmails.Where(e => e.Status == EmailStatus.Pending && e.Attempts < MaxAttempts).OrderBy(e => e.CreatedAt).Take(20).ToListAsync(ct);
        foreach (var m in batch)
        {
            try
            {
                await transport.SendAsync(m, ct);
                m.Status = EmailStatus.Sent;
                m.SentAt = DateTime.UtcNow;
                m.LastError = null;
            }
            catch (Exception ex)
            {
                m.Attempts++;
                m.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                if (m.Attempts >= MaxAttempts) m.Status = EmailStatus.Failed;
                logger.LogWarning("Email {Id} to {To} failed (attempt {N}): {Error}", m.Id, m.To, m.Attempts, m.LastError);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>Sends each active organization its daily reminder digest once a day, after 08:00 Pakistan time.</summary>
public class ReminderScheduler(IServiceScopeFactory scopes, IConfiguration config, ILogger<ReminderScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!config.GetValue("Reminders:Enabled", true)) return;
        var hour = config.GetValue("Reminders:Hour", 8);
        while (!stop.IsCancellationRequested)
        {
            var now = DateTime.UtcNow.AddHours(5);
            if (now.Hour >= hour)
            {
                try { await RunAllAsync(DateOnly.FromDateTime(now), stop); }
                catch (Exception ex) when (!stop.IsCancellationRequested) { logger.LogError(ex, "Reminder run failed"); }
            }
            await Task.Delay(TimeSpan.FromMinutes(10), stop).ContinueWith(_ => { });
        }
    }

    private async Task RunAllAsync(DateOnly today, CancellationToken ct)
    {
        List<Guid> tenants;
        using (var scope = scopes.CreateScope())
            tenants = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Tenants.IgnoreQueryFilters()
                .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).Select(t => t.Id).ToListAsync(ct);
        foreach (var tenant in tenants)
        {
            // One scope per organization, so the tenant query filters apply to that organization only.
            using var scope = scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<BackgroundTenantContext>().TenantId = tenant;
            try
            {
                var result = await scope.ServiceProvider.GetRequiredService<ReminderService>().RunAsync(today, force: false, ct);
                if (!result.Skipped && result.Items.Count > 0) logger.LogInformation("Reminders for {Tenant}: {Items} items to {Recipients} people", tenant, result.Items.Count, result.Recipients);
            }
            catch (Exception ex) { logger.LogError(ex, "Reminders failed for {Tenant}", tenant); }
        }
    }
}
