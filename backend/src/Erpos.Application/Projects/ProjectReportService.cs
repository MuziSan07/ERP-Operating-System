using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Projects;

public class ProjectReportService(IAppDbContext db, IAccessService access, ProjectService projects, ProjectTaskService tasks)
{
    /// <summary>
    /// Utilization: billable hours ÷ capacity (8 h per weekday the person was employed in the period). Counts submitted,
    /// approved and invoiced time; drafts and rejected time are left out.
    /// </summary>
    public async Task<UtilizationDto> UtilizationAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ProjectReports, ct);
        if (visible.Count == 0) throw new ForbiddenException();
        var today = ProjectsCommon.Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (end < start) throw new ValidationException("The period ends before it starts.");
        var time = await db.TimeEntries.Where(t => visible.Contains(t.EntityId) && t.Date >= start && t.Date <= end &&
                                                   (t.Status == TimeEntryStatus.Submitted || t.Status == TimeEntryStatus.Approved || t.Status == TimeEntryStatus.Invoiced))
            .GroupBy(t => new { t.EmployeeId, t.Billable }).Select(g => new { g.Key.EmployeeId, g.Key.Billable, Hours = g.Sum(t => t.Hours), Value = g.Sum(t => t.Hours * t.BillRate) })
            .ToListAsync(ct);
        var memberIds = await db.ProjectMembers.Join(db.Projects, m => m.ProjectId, p => p.Id, (m, p) => new { m.EmployeeId, p.EntityId, p.Status })
            .Where(x => visible.Contains(x.EntityId) && x.Status == ProjectStatus.Active).Select(x => x.EmployeeId).Distinct().ToListAsync(ct);
        var ids = memberIds.Union(time.Select(t => t.EmployeeId)).ToList();
        var people = await db.Employees.Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.User!.FullName, Designation = e.Designation == null ? null : e.Designation.Title, e.JoinDate, e.ExitDate }).ToListAsync(ct);
        var rows = people.Select(p =>
        {
            var from2 = p.JoinDate > start ? p.JoinDate : start;
            var to2 = p.ExitDate is { } x && x < end ? x : end;
            var capacity = Weekdays(from2, to2) * 8m;
            var hours = time.Where(t => t.EmployeeId == p.Id).Sum(t => t.Hours);
            var billable = time.Where(t => t.EmployeeId == p.Id && t.Billable).Sum(t => t.Hours);
            return new UtilizationRow(p.Id, p.FullName, p.Designation, capacity, hours, billable, capacity == 0 ? 0 : Math.Round(billable / capacity * 100, 1),
                Math.Round(time.Where(t => t.EmployeeId == p.Id && t.Billable).Sum(t => t.Value), 2));
        }).OrderByDescending(r => r.Utilization).ThenBy(r => r.Name).ToList();
        var cap = rows.Sum(r => r.Capacity);
        var bill = rows.Sum(r => r.BillableHours);
        return new UtilizationDto(start, end, rows, cap, rows.Sum(r => r.Hours), bill, cap == 0 ? 0 : Math.Round(bill / cap * 100, 1));
    }

    internal static int Weekdays(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) n++;
        return n;
    }

    public async Task<ProjectsDashboardDto> DashboardAsync(CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ProjectsView, ct);
        if (visible.Count == 0) throw new ForbiddenException();
        var today = ProjectsCommon.Today;
        var weekStart = TimesheetService.WeekStart(today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var active = await projects.ListItemsAsync(db.Projects.Where(p => visible.Contains(p.EntityId) && p.Status == ProjectStatus.Active), ct);
        var week = await db.TimeEntries.Where(t => visible.Contains(t.EntityId) && t.Date >= weekStart && t.Date <= today && t.Status != TimeEntryStatus.Rejected)
            .Select(t => new { t.Hours, t.Billable }).ToListAsync(ct);
        var billedThisMonth = await db.TimeEntries.Where(t => visible.Contains(t.EntityId) && t.Status == TimeEntryStatus.Invoiced)
            .Join(db.FinanceDocuments, t => t.InvoiceId, d => (Guid?)d.Id, (t, d) => new { Value = t.Hours * t.BillRate, d.Date })
            .Where(x => x.Date >= monthStart).SumAsync(x => (decimal?)x.Value, ct) ?? 0;
        var milestoneBilled = await db.ProjectMilestones.Join(db.Projects, m => m.ProjectId, p => p.Id, (m, p) => new { m, p.EntityId })
            .Join(db.FinanceDocuments, x => x.m.InvoiceId, d => (Guid?)d.Id, (x, d) => new { x.m.Amount, x.EntityId, d.Date })
            .Where(x => visible.Contains(x.EntityId) && x.Date >= monthStart).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var approvable = await access.EntitiesWithAsync(Permissions.TimesheetsApprove, ct);
        var pending = await db.TimeEntries.CountAsync(t => approvable.Contains(t.EntityId) && t.Status == TimeEntryStatus.Submitted, ct);
        var activeIds = active.Select(p => p.Id).ToList();
        var milestones = await db.ProjectMilestones.Where(m => activeIds.Contains(m.ProjectId) && m.InvoiceId == null && m.DueDate <= today.AddDays(30))
            .Join(db.Projects, m => m.ProjectId, p => p.Id, (m, p) => new { m, p.Code }).OrderBy(x => x.m.DueDate).ToListAsync(ct);
        var overdue = await tasks.ItemsAsync(db.ProjectTasks.Where(t => activeIds.Contains(t.ProjectId) && t.Status != WorkItemStatus.Done && t.DueDate != null && t.DueDate < today)
            .OrderBy(t => t.DueDate).Take(20), ct);
        return new ProjectsDashboardDto(active.Count, week.Sum(w => w.Hours), week.Where(w => w.Billable).Sum(w => w.Hours), active.Sum(p => p.Unbilled),
            Math.Round(billedThisMonth + milestoneBilled, 2), pending,
            active.Where(p => p.OverBudget || p.HoursPercent >= 90 || p.Margin < 0).ToList(),
            milestones.Select(x => new MilestoneDue(x.m.ProjectId, x.Code, x.m.Name, x.m.DueDate, x.m.Amount, x.m.CompletedOn != null, x.m.CompletedOn == null && x.m.DueDate < today)).ToList(),
            overdue);
    }
}
