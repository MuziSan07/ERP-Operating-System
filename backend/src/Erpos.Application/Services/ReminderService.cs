using System.Text;
using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

public record ReminderItem(string Area, string Text, string Link);
public record ReminderResult(DateOnly Date, bool Skipped, int Recipients, IReadOnlyList<ReminderItem> Items);

/// <summary>
/// Daily digest for an organization's administrators: what needs attention today across modules. Runs once a day per
/// organization from the background scheduler (or on demand); works with the tenant filters of the current context.
/// </summary>
public class ReminderService(IAppDbContext db, ICurrentUser currentUser, IAccessService access, EmailService email)
{
    public async Task<ReminderResult> RunNowAsync(CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.AuditView, ct);
        return await RunAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)), force: true, ct);
    }

    public async Task<ReminderResult> RunAsync(DateOnly today, bool force, CancellationToken ct)
    {
        var tenant = currentUser.TenantId ?? throw new InvalidOperationException("Reminders need an organization context.");
        if (!force && await db.ReminderRuns.AnyAsync(r => r.TenantId == tenant && r.Date == today, ct))
            return new ReminderResult(today, true, 0, []);

        var items = await CollectAsync(today, ct);
        var recipients = await db.Users.Where(u => u.IsActive && !u.IsDeleted && (u.UserType == UserType.SuperAdmin || u.UserType == UserType.Admin))
            .Select(u => new { u.Email, u.FullName }).ToListAsync(ct);
        if (items.Count > 0)
        {
            var html = new StringBuilder($"<p>Good morning. Here is what needs attention on <b>{today:dddd, d MMMM yyyy}</b>:</p><table style=\"border-collapse:collapse;width:100%\">");
            foreach (var g in items.GroupBy(i => i.Area))
            {
                html.Append($"<tr><td colspan=\"2\" style=\"padding:10px 0 4px;font-weight:600;color:#2f54eb\">{EmailService.Esc(g.Key)}</td></tr>");
                foreach (var i in g)
                    html.Append($"<tr><td style=\"padding:4px 0;border-bottom:1px solid #f0f0f0\">{EmailService.Esc(i.Text)}</td><td style=\"text-align:right;border-bottom:1px solid #f0f0f0\"><a href=\"{EmailService.Esc(email.Link(i.Link))}\">Open</a></td></tr>");
            }
            html.Append("</table>");
            foreach (var r in recipients)
                email.Queue(r.Email, $"ERPOS reminders — {items.Count} item(s) for {today:d MMM}", $"<p>Dear {EmailService.Esc(r.FullName)},</p>{html}", "reminders", tenant);
        }
        db.ReminderRuns.Add(new ReminderRun { TenantId = tenant, Date = today, Items = items.Count, Recipients = items.Count > 0 ? recipients.Count : 0 });
        await db.SaveChangesAsync(ct);
        return new ReminderResult(today, false, items.Count > 0 ? recipients.Count : 0, items);
    }

    private async Task<List<ReminderItem>> CollectAsync(DateOnly today, CancellationToken ct)
    {
        var items = new List<ReminderItem>();
        var open = new[] { DocumentStatus.Open, DocumentStatus.PartiallyPaid };

        var overdue = await db.FinanceDocuments.Where(d => d.Kind == DocumentKind.Invoice && open.Contains(d.Status) && d.DueDate < today)
            .Select(d => d.BaseTotal - d.BasePaid).ToListAsync(ct);
        if (overdue.Count > 0) items.Add(new("Finance", $"{overdue.Count} overdue customer invoice(s) totalling {overdue.Sum():N0}", "/finance/invoices"));
        var billsDue = await db.FinanceDocuments.Where(d => d.Kind == DocumentKind.Bill && open.Contains(d.Status) && d.DueDate >= today && d.DueDate <= today.AddDays(7))
            .Select(d => d.BaseTotal - d.BasePaid).ToListAsync(ct);
        if (billsDue.Count > 0) items.Add(new("Finance", $"{billsDue.Count} supplier bill(s) due in the next 7 days totalling {billsDue.Sum():N0}", "/finance/bills"));

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var wht = await db.Payments.Where(p => !p.IsVoid && p.WithholdingTax > 0 && p.WhtDepositId == null && p.Date < monthStart).SumAsync(p => (decimal?)p.WithholdingTax, ct) ?? 0;
        if (wht > 0) items.Add(new("Tax", $"{wht:N0} of income tax withheld last month is not yet deposited (due by the 15th)", "/finance/withholding"));

        var lastMonth = monthStart.AddMonths(-1);
        var lastPeriod = lastMonth.Year * 100 + lastMonth.Month;
        var undepreciated = await db.FixedAssets.CountAsync(a => a.Status == AssetStatus.Active && a.DepreciationStart < monthStart && a.DepreciatedThrough < lastPeriod, ct);
        if (undepreciated > 0) items.Add(new("Finance", $"Depreciation for {lastMonth:MMMM yyyy} hasn't been run ({undepreciated} asset(s))", "/finance/assets"));

        var leave = await db.LeaveRequests.CountAsync(l => l.Status == LeaveStatus.Pending, ct);
        if (leave > 0) items.Add(new("People", $"{leave} leave request(s) waiting for approval", "/hr/leave"));
        var timesheets = await db.TimeEntries.CountAsync(t => t.Status == TimeEntryStatus.Submitted, ct);
        if (timesheets > 0) items.Add(new("Projects", $"{timesheets} timesheet entr(ies) waiting for approval", "/projects/approvals"));

        var soon = today.AddDays(30);
        var papers = await db.Vehicles.CountAsync(v => v.Status != VehicleStatus.Inactive && ((v.FitnessExpiry != null && v.FitnessExpiry <= soon) || (v.InsuranceExpiry != null && v.InsuranceExpiry <= soon)
                                                     || (v.RoutePermitExpiry != null && v.RoutePermitExpiry <= soon) || (v.TokenTaxExpiry != null && v.TokenTaxExpiry <= soon)), ct);
        if (papers > 0) items.Add(new("Logistics", $"{papers} vehicle(s) with papers expired or expiring within 30 days", "/logistics/fleet"));
        var licences = await db.Drivers.CountAsync(d => d.IsActive && d.LicenseExpiry != null && d.LicenseExpiry <= soon, ct);
        if (licences > 0) items.Add(new("Logistics", $"{licences} driver licence(s) expired or expiring within 30 days", "/logistics/fleet"));

        var reports = await db.GrantReports.Join(db.Grants, r => r.GrantId, g => g.Id, (r, g) => new { r, g.Status })
            .CountAsync(x => x.Status == GrantStatus.Active && x.r.SubmittedOn == null && x.r.DueDate <= today.AddDays(14), ct);
        if (reports > 0) items.Add(new("NGO", $"{reports} donor report(s) due within 14 days or overdue", "/ngo"));
        return items;
    }
}
