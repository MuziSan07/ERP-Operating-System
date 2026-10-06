using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Projects;

/// <summary>
/// Weekly timesheets. People log their own hours on projects they're on the team of (active, within the project dates,
/// not in the future, at most 24 h a day), submit the week, and an approver of the project's entity (never themselves)
/// approves or rejects with a reason. Rates are captured when the time is logged.
/// </summary>
public class TimesheetService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private async Task<Employee> MeAsync(CancellationToken ct) =>
        await db.Employees.FirstOrDefaultAsync(e => e.UserId == currentUser.UserId, ct)
        ?? throw new ValidationException("Your login isn't linked to an employee record, so you can't keep a timesheet.");

    private IQueryable<TimeEntryDto> Items(IQueryable<TimeEntry> q) => q.Select(t => new TimeEntryDto(t.Id, t.EmployeeId, t.Employee!.User!.FullName, t.ProjectId,
        t.Project!.Code, t.Project.Name, t.TaskId, t.Task == null ? null : t.Project.Code + "-" + t.Task.Number, t.Task == null ? null : t.Task.Title, t.Date, t.Hours,
        t.Description, t.Billable, t.Status, t.BillRate, t.RejectReason, db.Users.Where(u => u.Id == t.ApprovedByUserId).Select(u => u.FullName).FirstOrDefault()));

    public async Task<MyWeekDto> MyWeekAsync(DateOnly? date, CancellationToken ct)
    {
        var start = WeekStart(date ?? ProjectsCommon.Today);
        var end = start.AddDays(6);
        var me = await db.Employees.FirstOrDefaultAsync(e => e.UserId == currentUser.UserId, ct);
        if (me == null) return new MyWeekDto(start, end, null, [], [], 0, 0, false);
        var entries = await Items(db.TimeEntries.Where(t => t.EmployeeId == me.Id && t.Date >= start && t.Date <= end).OrderBy(t => t.Date).ThenBy(t => t.CreatedAt)).ToListAsync(ct);
        var projects = await db.Projects.Where(p => p.Status == ProjectStatus.Active && p.Members.Any(m => m.EmployeeId == me.Id)).OrderBy(p => p.Code)
            .Select(p => new TimesheetProject(p.Id, p.Code, p.Name, p.BillingType == ProjectBillingType.TimeAndMaterials,
                db.ProjectTasks.Where(t => t.ProjectId == p.Id && t.Status != WorkItemStatus.Done).OrderBy(t => t.Number)
                    .Select(t => new TaskOption(t.Id, p.Code + "-" + t.Number, t.Title)).ToList())).ToListAsync(ct);
        var counted = entries.Where(e => e.Status != TimeEntryStatus.Rejected).ToList();
        return new MyWeekDto(start, end, me.Id, entries, projects, counted.Sum(e => e.Hours), counted.Where(e => e.Billable).Sum(e => e.Hours),
            entries.Any(e => e.Status is TimeEntryStatus.Draft or TimeEntryStatus.Rejected));
    }

    public async Task<TimeEntryDto> SaveAsync(Guid? id, SaveTimeEntryRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.TimesheetsCreate, ct);
        var me = await MeAsync(ct);
        TimeEntry t;
        if (id == null)
        {
            t = new TimeEntry { TenantId = me.TenantId, EmployeeId = me.Id };
            db.TimeEntries.Add(t);
        }
        else
        {
            t = await db.TimeEntries.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Time entry");
            if (t.EmployeeId != me.Id) throw new ForbiddenException();
            if (t.Status is not (TimeEntryStatus.Draft or TimeEntryStatus.Rejected)) throw new ValidationException("Submitted time can't be changed.");
        }
        var p = await db.Projects.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == req.ProjectId, ct) ?? throw new NotFoundException("Project");
        var member = p.Members.FirstOrDefault(m => m.EmployeeId == me.Id) ?? throw new ValidationException($"You're not on the {p.Code} team.");
        if (p.Status != ProjectStatus.Active) throw new ValidationException($"{p.Code} isn't active.");
        if (req.Date > ProjectsCommon.Today) throw new ValidationException("Time can't be logged for future days.");
        if (req.Date < p.StartDate || (p.EndDate is { } end && req.Date > end)) throw new ValidationException($"The date is outside the {p.Code} project dates.");
        if (req.Hours <= 0 || req.Hours > 24 || req.Hours * 4 != Math.Floor(req.Hours * 4)) throw new ValidationException("Log hours in quarter-hour steps, up to 24.");
        var sameDay = await db.TimeEntries.Where(x => x.EmployeeId == me.Id && x.Date == req.Date && x.Id != t.Id && x.Status != TimeEntryStatus.Rejected)
            .SumAsync(x => (decimal?)x.Hours, ct) ?? 0;
        if (sameDay + req.Hours > 24) throw new ValidationException($"That makes {sameDay + req.Hours:0.##} hours on {req.Date:dd MMM}; a day has 24.");
        if (req.TaskId is { } task && !await db.ProjectTasks.AnyAsync(x => x.Id == task && x.ProjectId == p.Id, ct)) throw new NotFoundException("Task");

        t.EntityId = p.EntityId;
        t.ProjectId = p.Id;
        t.TaskId = req.TaskId;
        t.Date = req.Date;
        t.Hours = req.Hours;
        t.Description = req.Description;
        t.Billable = p.BillingType == ProjectBillingType.TimeAndMaterials && req.Billable;
        t.BillRate = member.BillRate ?? p.DefaultBillRate;
        t.CostRate = member.CostRate;
        t.Status = TimeEntryStatus.Draft;
        t.RejectReason = null;
        await db.SaveChangesAsync(ct);
        return await Items(db.TimeEntries.Where(x => x.Id == t.Id)).FirstAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var t = await db.TimeEntries.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Time entry");
        if (t.EmployeeId != me.Id) throw new ForbiddenException();
        if (t.Status is not (TimeEntryStatus.Draft or TimeEntryStatus.Rejected)) throw new ValidationException("Submitted time can't be deleted.");
        t.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<MyWeekDto> SubmitWeekAsync(DateOnly date, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var start = WeekStart(date);
        var end = start.AddDays(6);
        var entries = await db.TimeEntries.Where(t => t.EmployeeId == me.Id && t.Date >= start && t.Date <= end &&
                                                      (t.Status == TimeEntryStatus.Draft || t.Status == TimeEntryStatus.Rejected)).ToListAsync(ct);
        if (entries.Count == 0) throw new ValidationException("There's nothing to submit for this week.");
        foreach (var t in entries) { t.Status = TimeEntryStatus.Submitted; t.SubmittedAt = DateTime.UtcNow; t.RejectReason = null; }
        await db.SaveChangesAsync(ct);
        return await MyWeekAsync(start, ct);
    }

    /// <summary>Team timesheets for reviewers: entries in entities where the user may view or approve timesheets.</summary>
    public async Task<List<TimeEntryDto>> ListAsync(Guid? projectId, Guid? employeeId, TimeEntryStatus? status, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        var visible = mine.ByEntity.Where(kv => kv.Value.Contains(Permissions.TimesheetsView) || kv.Value.Contains(Permissions.TimesheetsApprove))
            .Select(kv => kv.Key).ToHashSet();
        var q = db.TimeEntries.Where(t => visible.Contains(t.EntityId));
        if (projectId != null) q = q.Where(t => t.ProjectId == projectId);
        if (employeeId != null) q = q.Where(t => t.EmployeeId == employeeId);
        if (status != null) q = q.Where(t => t.Status == status);
        if (from != null) q = q.Where(t => t.Date >= from);
        if (to != null) q = q.Where(t => t.Date <= to);
        return await Items(q.OrderByDescending(t => t.Date).ThenBy(t => t.Employee!.User!.FullName).Take(1000)).ToListAsync(ct);
    }

    public async Task<List<TimeEntryDto>> PendingApprovalsAsync(CancellationToken ct)
    {
        var approvable = await access.EntitiesWithAsync(Permissions.TimesheetsApprove, ct);
        var meId = await db.Employees.Where(e => e.UserId == currentUser.UserId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        return await Items(db.TimeEntries.Where(t => approvable.Contains(t.EntityId) && t.Status == TimeEntryStatus.Submitted && t.EmployeeId != meId)
            .OrderBy(t => t.Employee!.User!.FullName).ThenBy(t => t.Date)).ToListAsync(ct);
    }

    public Task<int> ApproveAsync(DecideTimeRequest req, CancellationToken ct) => DecideAsync(req, true, ct);
    public Task<int> RejectAsync(DecideTimeRequest req, CancellationToken ct) => DecideAsync(req, false, ct);

    private async Task<int> DecideAsync(DecideTimeRequest req, bool approve, CancellationToken ct)
    {
        if (req.EntryIds.Count == 0) throw new ValidationException("Choose the time entries.");
        if (!approve && string.IsNullOrWhiteSpace(req.Reason)) throw new ValidationException("Say why the time is rejected.");
        var entries = await db.TimeEntries.Include(t => t.Employee).Where(t => req.EntryIds.Contains(t.Id)).ToListAsync(ct);
        if (entries.Count != req.EntryIds.Distinct().Count()) throw new NotFoundException("Time entry");
        foreach (var t in entries)
        {
            await access.EnsureAsync(Permissions.TimesheetsApprove, t.EntityId, ct);
            if (t.Employee!.UserId == currentUser.UserId) throw new ValidationException("You can't approve your own time.");
            if (t.Status != TimeEntryStatus.Submitted) throw new ValidationException("Only submitted time can be approved or rejected.");
            t.Status = approve ? TimeEntryStatus.Approved : TimeEntryStatus.Rejected;
            t.ApprovedByUserId = currentUser.UserId;
            t.ApprovedAt = DateTime.UtcNow;
            t.RejectReason = approve ? null : req.Reason!.Trim();
        }
        await db.SaveChangesAsync(ct);
        return entries.Count;
    }
}
