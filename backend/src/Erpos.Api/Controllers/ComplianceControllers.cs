using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/finance/withholding")]
public class WithholdingController(WithholdingService wht) : ControllerBase
{
    [HttpGet("rates")] public Task<List<WhtRateDto>> Rates(CancellationToken ct) => wht.RatesAsync(ct);
    [HttpPost("rates")] public Task<WhtRateDto> CreateRate(SaveWhtRateRequest req, CancellationToken ct) => wht.SaveRateAsync(null, req, ct);
    [HttpPut("rates/{id:guid}")] public Task<WhtRateDto> UpdateRate(Guid id, SaveWhtRateRequest req, CancellationToken ct) => wht.SaveRateAsync(id, req, ct);
    [HttpGet("deductions")]
    public Task<WhtSummaryDto> Deductions([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? vendorId, CancellationToken ct) =>
        wht.DeductionsAsync(from, to, vendorId, ct);
    [HttpGet("deposits")] public Task<List<WhtDepositDto>> Deposits(CancellationToken ct) => wht.DepositsAsync(ct);
    [HttpPost("deposits")] public Task<WhtDepositDto> Deposit(DepositWhtRequest req, CancellationToken ct) => wht.DepositAsync(req, ct);
    [HttpGet("certificate")]
    public Task<WhtCertificateDto> Certificate([FromQuery] Guid vendorId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        wht.CertificateAsync(vendorId, from, to, ct);
}

[ApiController, Authorize, Route("api/finance/reconciliations")]
public class BankReconciliationController(BankReconciliationService recs) : ControllerBase
{
    [HttpGet] public Task<List<ReconciliationListItem>> List([FromQuery] Guid? bankAccountId, CancellationToken ct) => recs.ListAsync(bankAccountId, ct);
    [HttpGet("{id:guid}")] public Task<ReconciliationDto> Get(Guid id, CancellationToken ct) => recs.GetAsync(id, ct);
    [HttpPost] public Task<ReconciliationDto> Create(CreateReconciliationRequest req, CancellationToken ct) => recs.CreateAsync(req, ct);
    [HttpPost("{id:guid}/auto-match")] public Task<AutoMatchResult> AutoMatch(Guid id, CancellationToken ct) => recs.AutoMatchAsync(id, ct);
    [HttpPost("{id:guid}/match")] public Task<ReconciliationDto> Match(Guid id, MatchRequest req, CancellationToken ct) => recs.MatchAsync(id, req, ct);
    [HttpPost("{id:guid}/unmatch")] public Task<ReconciliationDto> Unmatch(Guid id, UnmatchRequest req, CancellationToken ct) => recs.UnmatchAsync(id, req, ct);
    [HttpPost("{id:guid}/create-entry")]
    public Task<ReconciliationDto> CreateEntry(Guid id, CreateEntryFromLineRequest req, CancellationToken ct) => recs.CreateEntryAsync(id, req, ct);
    [HttpPost("{id:guid}/complete")] public Task<ReconciliationDto> Complete(Guid id, CancellationToken ct) => recs.CompleteAsync(id, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await recs.DeleteAsync(id, ct); return NoContent(); }
}
