using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/finance")]
public class FinanceSetupController(FinanceSetupService setup) : ControllerBase
{
    [HttpGet("accounts")] public Task<List<AccountDto>> Accounts(CancellationToken ct) => setup.AccountsAsync(ct);
    [HttpPost("accounts")] public Task<AccountDto> CreateAccount(SaveAccountRequest req, CancellationToken ct) => setup.SaveAccountAsync(null, req, ct);
    [HttpPut("accounts/{id:guid}")] public Task<AccountDto> UpdateAccount(Guid id, SaveAccountRequest req, CancellationToken ct) => setup.SaveAccountAsync(id, req, ct);

    [HttpGet("settings")] public Task<FinanceSettingsDto> Settings(CancellationToken ct) => setup.GetSettingsAsync(ct);
    [HttpPut("settings")] public Task<FinanceSettingsDto> SaveSettings(FinanceSettingsDto req, CancellationToken ct) => setup.SaveSettingsAsync(req, ct);

    [HttpGet("exchange-rates")] public Task<List<ExchangeRateDto>> Rates([FromQuery] string? currency, CancellationToken ct) => setup.RatesAsync(currency, ct);
    [HttpPut("exchange-rates")] public Task<ExchangeRateDto> SaveRate(SaveExchangeRateRequest req, CancellationToken ct) => setup.SaveRateAsync(req, ct);

    [HttpGet("tax-rates")] public Task<List<TaxRateDto>> TaxRates(CancellationToken ct) => setup.TaxRatesAsync(ct);
    [HttpPost("tax-rates")] public Task<TaxRateDto> CreateTaxRate(SaveTaxRateRequest req, CancellationToken ct) => setup.SaveTaxRateAsync(null, req, ct);
    [HttpPut("tax-rates/{id:guid}")] public Task<TaxRateDto> UpdateTaxRate(Guid id, SaveTaxRateRequest req, CancellationToken ct) => setup.SaveTaxRateAsync(id, req, ct);

    [HttpGet("contacts")]
    public Task<List<ContactDto>> Contacts([FromQuery] bool? customers, [FromQuery] bool? vendors, [FromQuery] string? search, CancellationToken ct) =>
        setup.ContactsAsync(customers, vendors, search, ct);
    [HttpPost("contacts")] public Task<ContactDto> CreateContact(SaveContactRequest req, CancellationToken ct) => setup.SaveContactAsync(null, req, ct);
    [HttpPut("contacts/{id:guid}")] public Task<ContactDto> UpdateContact(Guid id, SaveContactRequest req, CancellationToken ct) => setup.SaveContactAsync(id, req, ct);
}

[ApiController, Authorize, Route("api/finance/journals")]
public class JournalsController(JournalService journals) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<JournalEntryDto>> List([FromQuery] Guid? entityId, [FromQuery] JournalSource? source, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        journals.ListAsync(entityId, source, from, to, search, page, pageSize, ct);
    [HttpGet("{id:guid}")] public Task<JournalEntryDto> Get(Guid id, CancellationToken ct) => journals.GetAsync(id, ct);
    [HttpPost] public Task<JournalEntryDto> Create(SaveJournalRequest req, CancellationToken ct) => journals.CreateAsync(req, ct);
    [HttpPut("{id:guid}")] public Task<JournalEntryDto> Update(Guid id, SaveJournalRequest req, CancellationToken ct) => journals.UpdateAsync(id, req, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await journals.DeleteAsync(id, ct); return NoContent(); }
    [HttpPost("{id:guid}/post")] public Task<JournalEntryDto> Post(Guid id, CancellationToken ct) => journals.PostAsync(id, ct);
    [HttpPost("{id:guid}/reverse")] public Task<JournalEntryDto> Reverse(Guid id, ReverseRequest req, CancellationToken ct) => journals.ReverseAsync(id, req, ct);
}

/// <summary>Invoices at /api/finance/invoices, bills at /api/finance/bills.</summary>
[ApiController, Authorize]
public class DocumentsController(DocumentService docs) : ControllerBase
{
    private static DocumentKind Kind(string kind) => kind == "invoices" ? DocumentKind.Invoice : DocumentKind.Bill;

    [HttpGet("api/finance/{kind:regex(^(invoices|bills)$)}")]
    public Task<PagedResult<DocumentListItem>> List(string kind, [FromQuery] Guid? entityId, [FromQuery] Guid? contactId,
        [FromQuery] DocumentStatus? status, [FromQuery] bool overdue = false, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        docs.ListAsync(Kind(kind), entityId, contactId, status, overdue, search, page, pageSize, ct);

    [HttpGet("api/finance/{kind:regex(^(invoices|bills)$)}/{id:guid}")]
    public Task<DocumentDto> Get(string kind, Guid id, CancellationToken ct) => docs.GetAsync(id, ct);
    [HttpPost("api/finance/{kind:regex(^(invoices|bills)$)}")]
    public Task<DocumentDto> Create(string kind, SaveDocumentRequest req, CancellationToken ct) => docs.CreateAsync(Kind(kind), req, ct);
    [HttpPut("api/finance/{kind:regex(^(invoices|bills)$)}/{id:guid}")]
    public Task<DocumentDto> Update(string kind, Guid id, SaveDocumentRequest req, CancellationToken ct) => docs.UpdateAsync(id, req, ct);
    [HttpDelete("api/finance/{kind:regex(^(invoices|bills)$)}/{id:guid}")]
    public async Task<IActionResult> Delete(string kind, Guid id, CancellationToken ct) { await docs.DeleteAsync(id, ct); return NoContent(); }
    [HttpPost("api/finance/{kind:regex(^(invoices|bills)$)}/{id:guid}/approve")]
    public Task<DocumentDto> Approve(string kind, Guid id, CancellationToken ct) => docs.ApproveAsync(id, ct);
    [HttpPost("api/finance/{kind:regex(^(invoices|bills)$)}/{id:guid}/void")]
    public Task<DocumentDto> Void(string kind, Guid id, ReverseRequest req, CancellationToken ct) => docs.VoidAsync(id, req, ct);
}

[ApiController, Authorize, Route("api/finance/payments")]
public class PaymentsController(PaymentService payments, PayrollAccountingService payroll) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PaymentDto>> List([FromQuery] PaymentKind? kind, [FromQuery] Guid? contactId, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        payments.ListAsync(kind, contactId, from, to, page, pageSize, ct);
    [HttpGet("{id:guid}")] public Task<PaymentDto> Get(Guid id, CancellationToken ct) => payments.GetAsync(id, ct);
    [HttpPost] public Task<PaymentDto> Create(CreatePaymentRequest req, CancellationToken ct) => payments.CreateAsync(req, ct);
    [HttpPost("{id:guid}/void")] public Task<PaymentDto> Void(Guid id, ReverseRequest req, CancellationToken ct) => payments.VoidAsync(id, req, ct);

    [HttpPost("payroll/{runId:guid}")]
    public async Task<object> PaySalaries(Guid runId, PaySalariesRequest req, CancellationToken ct) =>
        new { journalEntryId = await payroll.PaySalariesAsync(runId, req, ct) };
}

[ApiController, Authorize, Route("api/finance/reports")]
public class FinanceReportsController(ReportService reports) : ControllerBase
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [HttpGet("dashboard")] public Task<FinanceDashboardDto> Dashboard([FromQuery] Guid? entityId, CancellationToken ct) => reports.DashboardAsync(entityId, ct);
    [HttpGet("trial-balance")]
    public Task<TrialBalanceDto> TrialBalance([FromQuery] Guid? entityId, [FromQuery] DateOnly? asOf, CancellationToken ct) =>
        reports.TrialBalanceAsync(entityId, asOf ?? Today, ct);
    [HttpGet("profit-loss")]
    public Task<FinancialStatementDto> ProfitLoss([FromQuery] Guid? entityId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        reports.ProfitAndLossAsync(entityId, from, to, ct);
    [HttpGet("balance-sheet")]
    public Task<FinancialStatementDto> BalanceSheet([FromQuery] Guid? entityId, [FromQuery] DateOnly? asOf, CancellationToken ct) =>
        reports.BalanceSheetAsync(entityId, asOf ?? Today, ct);
    [HttpGet("general-ledger")]
    public Task<GeneralLedgerDto> GeneralLedger([FromQuery] Guid accountId, [FromQuery] Guid? entityId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        CancellationToken ct) => reports.GeneralLedgerAsync(accountId, entityId, from, to, ct);
    [HttpGet("aging")]
    public Task<AgingDto> Aging([FromQuery] DocumentKind kind, [FromQuery] Guid? entityId, [FromQuery] DateOnly? asOf, CancellationToken ct) =>
        reports.AgingAsync(kind, entityId, asOf ?? Today, ct);
    [HttpGet("sales-tax")]
    public Task<SalesTaxReportDto> SalesTax([FromQuery] Guid? entityId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        reports.SalesTaxAsync(entityId, from, to, ct);
}
