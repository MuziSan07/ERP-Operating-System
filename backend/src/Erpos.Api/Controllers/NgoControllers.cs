using Erpos.Application.Dtos;
using Erpos.Application.Ngo;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/ngo")]
public class NgoSetupController(NgoSetupService setup, FundService funds, NgoReportService reports) : ControllerBase
{
    [HttpGet("dashboard")] public Task<NgoDashboardDto> Dashboard(CancellationToken ct) => reports.DashboardAsync(ct);
    [HttpGet("donors")] public Task<List<DonorDto>> Donors([FromQuery] string? search, CancellationToken ct) => setup.DonorsAsync(search, ct);
    [HttpPost("donors")] public Task<DonorDto> CreateDonor(SaveDonorRequest req, CancellationToken ct) => setup.SaveDonorAsync(null, req, ct);
    [HttpPut("donors/{id:guid}")] public Task<DonorDto> UpdateDonor(Guid id, SaveDonorRequest req, CancellationToken ct) => setup.SaveDonorAsync(id, req, ct);
    [HttpGet("programs")] public Task<List<ProgramDto>> Programs(CancellationToken ct) => setup.ProgramsAsync(ct);
    [HttpPost("programs")] public Task<ProgramDto> CreateProgram(SaveProgramRequest req, CancellationToken ct) => setup.SaveProgramAsync(null, req, ct);
    [HttpPut("programs/{id:guid}")] public Task<ProgramDto> UpdateProgram(Guid id, SaveProgramRequest req, CancellationToken ct) => setup.SaveProgramAsync(id, req, ct);
    [HttpGet("funds")] public Task<List<FundDto>> Funds(CancellationToken ct) => funds.FundsAsync(ct);
    [HttpPost("funds")] public Task<FundDto> CreateFund(SaveFundRequest req, CancellationToken ct) => funds.SaveFundAsync(null, req, ct);
    [HttpPut("funds/{id:guid}")] public Task<FundDto> UpdateFund(Guid id, SaveFundRequest req, CancellationToken ct) => funds.SaveFundAsync(id, req, ct);

    [HttpGet("donations")]
    public Task<PagedResult<DonationListItem>> Donations([FromQuery] Guid? donorId, [FromQuery] Guid? fundId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        funds.DonationsAsync(donorId, fundId, from, to, search, page, pageSize, ct);
    [HttpGet("donations/{id:guid}")] public Task<DonationDto> Donation(Guid id, CancellationToken ct) => funds.GetDonationAsync(id, ct);
    [HttpPost("donations")] public Task<DonationDto> Donate(RecordDonationRequest req, CancellationToken ct) => funds.RecordDonationAsync(req, ct);

    [HttpGet("expenses")]
    public Task<List<FundExpenseDto>> Expenses([FromQuery] Guid? fundId, [FromQuery] Guid? grantId, [FromQuery] Guid? programId, CancellationToken ct) =>
        funds.ExpensesAsync(fundId, grantId, programId, ct);
    [HttpPost("expenses")] public Task<FundExpenseDto> Charge(ChargeExpenseRequest req, CancellationToken ct) => funds.ChargeAsync(req, ct);

    [HttpGet("reports/functional")]
    public Task<FunctionalExpensesDto> Functional([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => reports.FunctionalExpensesAsync(from, to, ct);
    [HttpGet("reports/donors")]
    public Task<List<DonorSummaryRow>> DonorSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => reports.DonorSummaryAsync(from, to, ct);
}

[ApiController, Authorize, Route("api/ngo/grants")]
public class GrantsController(GrantService grants) : ControllerBase
{
    [HttpGet] public Task<List<GrantListItem>> List([FromQuery] GrantStatus? status, [FromQuery] Guid? donorId, CancellationToken ct) => grants.ListAsync(status, donorId, ct);
    [HttpGet("{id:guid}")] public Task<GrantDto> Get(Guid id, CancellationToken ct) => grants.GetAsync(id, ct);
    [HttpPost] public Task<GrantDto> Create(SaveGrantRequest req, CancellationToken ct) => grants.SaveAsync(null, req, ct);
    [HttpPut("{id:guid}")] public Task<GrantDto> Update(Guid id, SaveGrantRequest req, CancellationToken ct) => grants.SaveAsync(id, req, ct);
    [HttpPost("{id:guid}/activate")] public Task<GrantDto> Activate(Guid id, CancellationToken ct) => grants.ActivateAsync(id, ct);
    [HttpPost("{id:guid}/tranches")] public Task<GrantDto> Receive(Guid id, ReceiveTrancheRequest req, CancellationToken ct) => grants.ReceiveTrancheAsync(id, req, ct);
    [HttpPost("{id:guid}/reports/{reportId:guid}/submit")]
    public Task<GrantDto> Submit(Guid id, Guid reportId, SubmitReportRequest req, CancellationToken ct) => grants.SubmitReportAsync(id, reportId, req, ct);
    [HttpPost("{id:guid}/close")] public Task<GrantDto> Close(Guid id, CloseGrantRequest req, CancellationToken ct) => grants.CloseAsync(id, req, ct);
    [HttpPost("{id:guid}/cancel")] public Task<GrantDto> Cancel(Guid id, CancellationToken ct) => grants.CancelAsync(id, ct);
}

[ApiController, Authorize, Route("api/ngo/beneficiaries")]
public class BeneficiariesController(BeneficiaryService beneficiaries) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<BeneficiaryListItem>> List([FromQuery] Guid? programId, [FromQuery] string? district, [FromQuery] string? search,
        [FromQuery] bool activeOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        beneficiaries.ListAsync(programId, district, search, activeOnly, page, pageSize, ct);
    [HttpGet("{id:guid}")] public Task<BeneficiaryDto> Get(Guid id, CancellationToken ct) => beneficiaries.GetAsync(id, ct);
    [HttpPost] public Task<BeneficiaryDto> Create(SaveBeneficiaryRequest req, CancellationToken ct) => beneficiaries.SaveAsync(null, req, ct);
    [HttpPut("{id:guid}")] public Task<BeneficiaryDto> Update(Guid id, SaveBeneficiaryRequest req, CancellationToken ct) => beneficiaries.SaveAsync(id, req, ct);
    [HttpPost("{id:guid}/assistance")] public Task<BeneficiaryDto> Assist(Guid id, AddAssistanceRequest req, CancellationToken ct) => beneficiaries.AddAssistanceAsync(id, req, ct);
}
