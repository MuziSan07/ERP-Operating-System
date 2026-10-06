using Erpos.Application.Dtos;
using Erpos.Application.Projects;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/projects")]
public class ProjectsController(ProjectService projects, ProjectReportService reports) : ControllerBase
{
    [HttpGet("dashboard")] public Task<ProjectsDashboardDto> Dashboard(CancellationToken ct) => reports.DashboardAsync(ct);
    [HttpGet("people")] public Task<List<ProjectPerson>> People(CancellationToken ct) => projects.PeopleAsync(ct);
    [HttpGet("clients")] public Task<List<ClientDto>> Clients(CancellationToken ct) => projects.ClientsAsync(ct);
    [HttpPost("clients")] public Task<ClientDto> CreateClient(SaveClientRequest req, CancellationToken ct) => projects.SaveClientAsync(null, req, ct);
    [HttpPut("clients/{id:guid}")] public Task<ClientDto> UpdateClient(Guid id, SaveClientRequest req, CancellationToken ct) => projects.SaveClientAsync(id, req, ct);
    [HttpGet("reports/utilization")]
    public Task<UtilizationDto> Utilization([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => reports.UtilizationAsync(from, to, ct);

    [HttpGet] public Task<List<ProjectListItem>> List([FromQuery] ProjectStatus? status, [FromQuery] Guid? clientId, CancellationToken ct) => projects.ListAsync(status, clientId, ct);
    [HttpGet("{id:guid}")] public Task<ProjectDto> Get(Guid id, CancellationToken ct) => projects.GetAsync(id, ct);
    [HttpPost] public Task<ProjectDto> Create(SaveProjectRequest req, CancellationToken ct) => projects.SaveAsync(null, req, ct);
    [HttpPut("{id:guid}")] public Task<ProjectDto> Update(Guid id, SaveProjectRequest req, CancellationToken ct) => projects.SaveAsync(id, req, ct);
    [HttpPost("{id:guid}/status/{status}")] public Task<ProjectDto> Status(Guid id, ProjectStatus status, CancellationToken ct) => projects.SetStatusAsync(id, status, ct);
    [HttpPost("{id:guid}/milestones/{milestoneId:guid}/complete")]
    public Task<ProjectDto> Complete(Guid id, Guid milestoneId, CancellationToken ct) => projects.CompleteMilestoneAsync(id, milestoneId, ct);
    [HttpPost("{id:guid}/milestones/{milestoneId:guid}/invoice")]
    public Task<ProjectInvoiceResult> InvoiceMilestone(Guid id, Guid milestoneId, InvoiceProjectRequest req, CancellationToken ct) => projects.InvoiceMilestoneAsync(id, milestoneId, req, ct);
    [HttpPost("{id:guid}/invoice")] public Task<ProjectInvoiceResult> InvoiceTime(Guid id, InvoiceProjectRequest req, CancellationToken ct) => projects.InvoiceTimeAsync(id, req, ct);
}

[ApiController, Authorize, Route("api/projects/tasks")]
public class ProjectTasksController(ProjectTaskService tasks) : ControllerBase
{
    [HttpGet]
    public Task<List<TaskDto>> List([FromQuery] Guid? projectId, [FromQuery] Guid? assigneeEmployeeId, [FromQuery] bool openOnly = false, CancellationToken ct = default) =>
        tasks.ListAsync(projectId, assigneeEmployeeId, openOnly, ct);
    [HttpPost] public Task<TaskDto> Create(SaveTaskRequest req, CancellationToken ct) => tasks.SaveAsync(null, req, ct);
    [HttpPut("{id:guid}")] public Task<TaskDto> Update(Guid id, SaveTaskRequest req, CancellationToken ct) => tasks.SaveAsync(id, req, ct);
    [HttpPost("{id:guid}/move")] public Task<TaskDto> Move(Guid id, MoveTaskRequest req, CancellationToken ct) => tasks.MoveAsync(id, req, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await tasks.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Route("api/timesheets")]
public class TimesheetsController(TimesheetService timesheets) : ControllerBase
{
    [HttpGet("me")] public Task<MyWeekDto> MyWeek([FromQuery] DateOnly? date, CancellationToken ct) => timesheets.MyWeekAsync(date, ct);
    [HttpPost("me/submit")] public Task<MyWeekDto> Submit([FromQuery] DateOnly date, CancellationToken ct) => timesheets.SubmitWeekAsync(date, ct);
    [HttpPost("entries")] public Task<TimeEntryDto> Create(SaveTimeEntryRequest req, CancellationToken ct) => timesheets.SaveAsync(null, req, ct);
    [HttpPut("entries/{id:guid}")] public Task<TimeEntryDto> Update(Guid id, SaveTimeEntryRequest req, CancellationToken ct) => timesheets.SaveAsync(id, req, ct);
    [HttpDelete("entries/{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await timesheets.DeleteAsync(id, ct); return NoContent(); }
    [HttpGet]
    public Task<List<TimeEntryDto>> List([FromQuery] Guid? projectId, [FromQuery] Guid? employeeId, [FromQuery] TimeEntryStatus? status, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, CancellationToken ct) => timesheets.ListAsync(projectId, employeeId, status, from, to, ct);
    [HttpGet("approvals")] public Task<List<TimeEntryDto>> Approvals(CancellationToken ct) => timesheets.PendingApprovalsAsync(ct);
    [HttpPost("approve")] public Task<int> Approve(DecideTimeRequest req, CancellationToken ct) => timesheets.ApproveAsync(req, ct);
    [HttpPost("reject")] public Task<int> Reject(DecideTimeRequest req, CancellationToken ct) => timesheets.RejectAsync(req, ct);
}
