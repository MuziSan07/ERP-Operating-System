using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Hr;
using Erpos.Application.Payroll;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/hr")]
public class HrStructureController(EmployeeService employees) : ControllerBase
{
    [HttpGet("departments")]
    public Task<List<DepartmentDto>> Departments([FromQuery] Guid? entityId, CancellationToken ct) => employees.ListDepartmentsAsync(entityId, ct);
    [HttpPost("departments")]
    public Task<DepartmentDto> CreateDepartment(SaveDepartmentRequest req, CancellationToken ct) => employees.SaveDepartmentAsync(null, req, ct);
    [HttpPut("departments/{id:guid}")]
    public Task<DepartmentDto> UpdateDepartment(Guid id, SaveDepartmentRequest req, CancellationToken ct) => employees.SaveDepartmentAsync(id, req, ct);
    [HttpDelete("departments/{id:guid}")]
    public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken ct) { await employees.DeleteDepartmentAsync(id, ct); return NoContent(); }

    [HttpGet("designations")]
    public Task<List<DesignationDto>> Designations(CancellationToken ct) => employees.ListDesignationsAsync(ct);
    [HttpPost("designations")]
    public Task<DesignationDto> CreateDesignation(SaveDesignationRequest req, CancellationToken ct) => employees.SaveDesignationAsync(null, req, ct);
    [HttpPut("designations/{id:guid}")]
    public Task<DesignationDto> UpdateDesignation(Guid id, SaveDesignationRequest req, CancellationToken ct) => employees.SaveDesignationAsync(id, req, ct);
    [HttpDelete("designations/{id:guid}")]
    public async Task<IActionResult> DeleteDesignation(Guid id, CancellationToken ct) { await employees.DeleteDesignationAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Route("api/hr/employees")]
public class EmployeesController(EmployeeService employees, PayrollSetupService payroll, LeaveService leave, AttendanceService attendance)
    : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<EmployeeListItem>> List([FromQuery] Guid? entityId, [FromQuery] bool includeSubEntities = true,
        [FromQuery] Guid? departmentId = null, [FromQuery] EmployeeStatus? status = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        employees.ListAsync(entityId, includeSubEntities, departmentId, status, search, page, pageSize, ct);

    [HttpGet("{id:guid}")] public Task<EmployeeDto> Get(Guid id, CancellationToken ct) => employees.GetAsync(id, ct);
    [HttpPost] public Task<EmployeeDto> Create(CreateEmployeeRequest req, CancellationToken ct) => employees.CreateAsync(req, ct);
    [HttpPut("{id:guid}")] public Task<EmployeeDto> Update(Guid id, EmployeeData req, CancellationToken ct) => employees.UpdateAsync(id, req, ct);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await employees.DeleteAsync(id, ct); return NoContent(); }

    [HttpGet("{id:guid}/salary")] public Task<List<SalaryDto>> Salary(Guid id, CancellationToken ct) => payroll.SalaryHistoryAsync(id, ct);
    [HttpPost("{id:guid}/salary")]
    public Task<List<SalaryDto>> SaveSalary(Guid id, SaveSalaryRequest req, CancellationToken ct) => payroll.SaveSalaryAsync(id, req, ct);

    [HttpGet("{id:guid}/leave-balances")]
    public Task<List<LeaveBalanceDto>> Balances(Guid id, [FromQuery] int? year, CancellationToken ct) =>
        leave.BalancesAsync(id, year ?? DateTime.UtcNow.Year, ct);

    [HttpGet("{id:guid}/attendance")]
    public Task<MonthlyAttendanceDto> Attendance(Guid id, [FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        attendance.MonthlyAsync(id, year, month, ct);
}

[ApiController, Authorize, Route("api/hr/attendance")]
public class AttendanceController(AttendanceService attendance) : ControllerBase
{
    [HttpGet]
    public Task<List<AttendanceRow>> Daily([FromQuery] Guid entityId, [FromQuery] DateOnly date, [FromQuery] bool includeSubEntities = true,
        CancellationToken ct = default) => attendance.DailyAsync(entityId, date, includeSubEntities, ct);

    [HttpPut]
    public async Task<IActionResult> Save(SaveAttendanceRequest req, CancellationToken ct) { await attendance.SaveAsync(req, ct); return NoContent(); }

    [HttpGet("holidays")]
    public Task<List<HolidayDto>> Holidays([FromQuery] int? year, CancellationToken ct) => attendance.ListHolidaysAsync(year ?? DateTime.UtcNow.Year, ct);
    [HttpPost("holidays")]
    public Task<HolidayDto> AddHoliday(SaveHolidayRequest req, CancellationToken ct) => attendance.AddHolidayAsync(req, ct);
    [HttpDelete("holidays/{id:guid}")]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct) { await attendance.DeleteHolidayAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Route("api/hr/leave")]
public class LeaveController(LeaveService leave) : ControllerBase
{
    [HttpGet("types")] public Task<List<LeaveTypeDto>> Types(CancellationToken ct) => leave.ListTypesAsync(ct);
    [HttpPost("types")] public Task<LeaveTypeDto> CreateType(SaveLeaveTypeRequest req, CancellationToken ct) => leave.SaveTypeAsync(null, req, ct);
    [HttpPut("types/{id:guid}")]
    public Task<LeaveTypeDto> UpdateType(Guid id, SaveLeaveTypeRequest req, CancellationToken ct) => leave.SaveTypeAsync(id, req, ct);

    [HttpGet("workflow")] public Task<List<LeaveStepDto>> Workflow(CancellationToken ct) => leave.GetWorkflowAsync(ct);
    [HttpPut("workflow")] public Task<List<LeaveStepDto>> SaveWorkflow(SaveWorkflowRequest req, CancellationToken ct) => leave.SaveWorkflowAsync(req, ct);

    [HttpGet("requests")]
    public Task<PagedResult<LeaveRequestDto>> List([FromQuery] Guid? entityId, [FromQuery] LeaveStatus? status, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        leave.ListAsync(entityId, status, from, to, page, pageSize, ct);

    [HttpGet("requests/{id:guid}")] public Task<LeaveRequestDto> Get(Guid id, CancellationToken ct) => leave.GetAsync(id, ct);
    [HttpPost("requests")] public Task<LeaveRequestDto> Apply(ApplyLeaveRequest req, CancellationToken ct) => leave.ApplyAsync(req, ct);
    [HttpPost("requests/{id:guid}/decision")]
    public Task<LeaveRequestDto> Decide(Guid id, LeaveDecisionRequest req, CancellationToken ct) => leave.DecideAsync(id, req, ct);
    [HttpPost("requests/{id:guid}/cancel")] public Task<LeaveRequestDto> Cancel(Guid id, CancellationToken ct) => leave.CancelAsync(id, ct);

    [HttpGet("inbox")] public Task<List<LeaveRequestDto>> Inbox(CancellationToken ct) => leave.InboxAsync(ct);
}

[ApiController, Authorize, Route("api/payroll")]
public class PayrollController(PayrollSetupService setup, PayrollRunService runs) : ControllerBase
{
    [HttpGet("settings")] public Task<HrSettingsDto> Settings(CancellationToken ct) => setup.GetSettingsAsync(ct);
    [HttpPut("settings")] public Task<HrSettingsDto> SaveSettings(HrSettingsDto req, CancellationToken ct) => setup.SavePayrollSettingsAsync(req, ct);
    [HttpPut("settings/weekly-offs")]
    public Task<HrSettingsDto> SaveWeeklyOffs(List<string> days, CancellationToken ct) => setup.SaveCalendarAsync(days, ct);

    [HttpGet("components")] public Task<List<PayComponentDto>> Components(CancellationToken ct) => setup.ListComponentsAsync(ct);
    [HttpPost("components")]
    public Task<PayComponentDto> CreateComponent(SavePayComponentRequest req, CancellationToken ct) => setup.SaveComponentAsync(null, req, ct);
    [HttpPut("components/{id:guid}")]
    public Task<PayComponentDto> UpdateComponent(Guid id, SavePayComponentRequest req, CancellationToken ct) => setup.SaveComponentAsync(id, req, ct);

    [HttpGet("tax-years")] public Task<List<TaxYearDto>> TaxYears(CancellationToken ct) => setup.ListTaxYearsAsync(ct);
    [HttpPut("tax-years")] public Task<TaxYearDto> SaveTaxYear(SaveTaxYearRequest req, CancellationToken ct) => setup.SaveTaxYearAsync(req, ct);
    [HttpGet("tax-preview")]
    public Task<TaxPreviewDto> TaxPreview([FromQuery] int taxYear, [FromQuery] decimal monthlyTaxable, CancellationToken ct) =>
        setup.PreviewTaxAsync(taxYear, monthlyTaxable, ct);
    [HttpGet("salary-split")]
    public Task<List<SalaryLineDto>> SalarySplit([FromQuery] decimal gross, CancellationToken ct) => setup.SplitGrossAsync(gross, ct);

    [HttpGet("runs")] public Task<List<PayrollRunDto>> Runs([FromQuery] int? year, CancellationToken ct) => runs.ListAsync(year, ct);
    [HttpPost("runs")] public Task<PayrollRunDetailDto> CreateRun(CreatePayrollRunRequest req, CancellationToken ct) => runs.CreateAsync(req, ct);
    [HttpGet("runs/{id:guid}")] public Task<PayrollRunDetailDto> Run(Guid id, CancellationToken ct) => runs.GetAsync(id, ct);
    [HttpPost("runs/{id:guid}/recalculate")] public Task<PayrollRunDetailDto> Recalculate(Guid id, CancellationToken ct) => runs.RecalculateAsync(id, ct);
    [HttpPost("runs/{id:guid}/approve")] public Task<PayrollRunDetailDto> Approve(Guid id, CancellationToken ct) => runs.ApproveAsync(id, ct);
    [HttpPost("runs/{id:guid}/post")] public Task<PayrollRunDetailDto> Post(Guid id, CancellationToken ct) => runs.PostAsync(id, ct);
    [HttpPost("runs/{id:guid}/cancel")] public Task<PayrollRunDetailDto> Cancel(Guid id, CancellationToken ct) => runs.CancelAsync(id, ct);
    [HttpPost("runs/{id:guid}/adjustments")]
    public Task<PayrollRunDetailDto> AddAdjustment(Guid id, AddAdjustmentRequest req, CancellationToken ct) => runs.AddAdjustmentAsync(id, req, ct);
    [HttpDelete("runs/{id:guid}/adjustments/{adjustmentId:guid}")]
    public Task<PayrollRunDetailDto> RemoveAdjustment(Guid id, Guid adjustmentId, CancellationToken ct) => runs.RemoveAdjustmentAsync(id, adjustmentId, ct);

    [HttpGet("payslips/{id:guid}")] public Task<PayslipDto> Payslip(Guid id, CancellationToken ct) => runs.PayslipAsync(id, ct);
}

/// <summary>Self-service for the signed-in employee.</summary>
[ApiController, Authorize, Route("api/me")]
public class MeController(EmployeeService employees, LeaveService leave, PayrollRunService runs, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("hr")]
    public async Task<MyHrDto> Hr(CancellationToken ct)
    {
        var emp = currentUser.UserId is { } uid ? await employees.GetByUserAsync(uid, ct) : null;
        var balances = emp == null ? [] : await leave.BalancesAsync(emp.Id, DateTime.UtcNow.Year, ct);
        return new MyHrDto(emp, balances, await leave.PendingForMeCountAsync(ct));
    }

    [HttpGet("leave")] public Task<List<LeaveRequestDto>> Leave([FromQuery] int? year, CancellationToken ct) => leave.MyRequestsAsync(year ?? DateTime.UtcNow.Year, ct);
    [HttpGet("payslips")] public Task<List<PayslipDto>> Payslips(CancellationToken ct) => runs.MyPayslipsAsync(ct);
}
