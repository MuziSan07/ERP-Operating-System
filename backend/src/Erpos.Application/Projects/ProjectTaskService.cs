using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Projects;

/// <summary>Task board per project. Assignees must be on the team; assignees may move their own cards.</summary>
public class ProjectTaskService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    public async Task<List<TaskDto>> ListAsync(Guid? projectId, Guid? assigneeEmployeeId, bool openOnly, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.TasksView, ct);
        var q = db.ProjectTasks.Where(t => visible.Contains(t.Project!.EntityId));
        if (projectId != null) q = q.Where(t => t.ProjectId == projectId);
        if (assigneeEmployeeId != null) q = q.Where(t => t.AssigneeEmployeeId == assigneeEmployeeId);
        if (openOnly) q = q.Where(t => t.Status != WorkItemStatus.Done);
        return await ItemsAsync(q.OrderBy(t => t.Status).ThenBy(t => t.SortOrder).ThenBy(t => t.Number).Take(500), ct);
    }

    internal async Task<List<TaskDto>> ItemsAsync(IQueryable<ProjectTask> q, CancellationToken ct)
    {
        var today = ProjectsCommon.Today;
        return await q.Select(t => new TaskDto(t.Id, t.ProjectId, t.Project!.Code + "-" + t.Number, t.Title, t.Description, t.Status, t.Priority, t.AssigneeEmployeeId,
            t.Assignee == null ? null : t.Assignee.User!.FullName, t.EstimateHours,
            db.TimeEntries.Where(e => e.TaskId == t.Id && e.Status != TimeEntryStatus.Rejected).Sum(e => (decimal?)e.Hours) ?? 0,
            t.DueDate, t.MilestoneId, t.SortOrder, t.Status != WorkItemStatus.Done && t.DueDate != null && t.DueDate < today)).ToListAsync(ct);
    }

    public async Task<TaskDto> SaveAsync(Guid? id, SaveTaskRequest req, CancellationToken ct)
    {
        var project = await db.Projects.Include(p => p.Members).Include(p => p.Milestones).FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct)
                      ?? throw new NotFoundException("Project");
        ProjectTask t;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.TasksCreate, project.EntityId, ct);
            t = new ProjectTask { TenantId = currentUser.TenantId!.Value, ProjectId = project.Id, Number = project.NextTaskNumber++ };
            t.SortOrder = await db.ProjectTasks.Where(x => x.ProjectId == project.Id && x.Status == req.Status).CountAsync(ct);
            db.ProjectTasks.Add(t);
        }
        else
        {
            t = await db.ProjectTasks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Task");
            await access.EnsureAsync(Permissions.TasksEdit, project.EntityId, ct);
            if (t.ProjectId != project.Id) throw new ValidationException("A task can't move to another project.");
        }
        if (project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled) throw new ValidationException("The project is closed.");
        if (req.AssigneeEmployeeId is { } a && project.Members.All(m => m.EmployeeId != a)) throw new ValidationException("Assign the task to someone on the project team.");
        if (req.MilestoneId is { } ms && project.Milestones.All(m => m.Id != ms)) throw new NotFoundException("Milestone");
        if (req.EstimateHours < 0) throw new ValidationException("The estimate can't be negative.");
        t.Title = Guard.Required(req.Title, "Title", 200);
        t.Description = req.Description;
        SetStatus(t, req.Status);
        t.Priority = req.Priority;
        t.AssigneeEmployeeId = req.AssigneeEmployeeId;
        t.EstimateHours = req.EstimateHours;
        t.DueDate = req.DueDate;
        t.MilestoneId = req.MilestoneId;
        await db.SaveChangesAsync(ct);
        return (await ItemsAsync(db.ProjectTasks.Where(x => x.Id == t.Id), ct)).First();
    }

    public async Task<TaskDto> MoveAsync(Guid id, MoveTaskRequest req, CancellationToken ct)
    {
        var t = await db.ProjectTasks.Include(x => x.Project).Include(x => x.Assignee).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Task");
        var mine = t.Assignee != null && t.Assignee.UserId == currentUser.UserId;
        if (!mine) await access.EnsureAsync(Permissions.TasksEdit, t.Project!.EntityId, ct);
        if (t.Project!.Status is ProjectStatus.Completed or ProjectStatus.Cancelled) throw new ValidationException("The project is closed.");
        SetStatus(t, req.Status);
        t.SortOrder = Math.Max(0, req.SortOrder);
        await db.SaveChangesAsync(ct);
        return (await ItemsAsync(db.ProjectTasks.Where(x => x.Id == t.Id), ct)).First();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var t = await db.ProjectTasks.Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Task");
        await access.EnsureAsync(Permissions.TasksDelete, t.Project!.EntityId, ct);
        if (await db.TimeEntries.AnyAsync(e => e.TaskId == id, ct)) throw new ValidationException("Time has been logged on this task; mark it done instead.");
        t.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    private static void SetStatus(ProjectTask t, WorkItemStatus status)
    {
        if (status == WorkItemStatus.Done && t.Status != WorkItemStatus.Done) t.CompletedAt = DateTime.UtcNow;
        if (status != WorkItemStatus.Done) t.CompletedAt = null;
        t.Status = status;
    }
}
