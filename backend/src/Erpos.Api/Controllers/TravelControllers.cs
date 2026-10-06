using Erpos.Application.Dtos;
using Erpos.Application.Travel;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/tours")]
public class ToursController(TourService tours) : ControllerBase
{
    [HttpGet("packages")] public Task<List<TourPackageDto>> Packages([FromQuery] bool activeOnly = false, CancellationToken ct = default) => tours.PackagesAsync(activeOnly, ct);
    [HttpPost("packages")] public Task<TourPackageDto> CreatePackage(SaveTourPackageRequest req, CancellationToken ct) => tours.SavePackageAsync(null, req, ct);
    [HttpPut("packages/{id:guid}")] public Task<TourPackageDto> UpdatePackage(Guid id, SaveTourPackageRequest req, CancellationToken ct) => tours.SavePackageAsync(id, req, ct);

    [HttpGet("guides")] public Task<List<GuideDto>> Guides(CancellationToken ct) => tours.GuidesAsync(ct);
    [HttpPost("guides")] public Task<GuideDto> CreateGuide(SaveGuideRequest req, CancellationToken ct) => tours.SaveGuideAsync(null, req, ct);
    [HttpPut("guides/{id:guid}")] public Task<GuideDto> UpdateGuide(Guid id, SaveGuideRequest req, CancellationToken ct) => tours.SaveGuideAsync(id, req, ct);

    [HttpGet("departures")]
    public Task<List<DepartureListItem>> Departures([FromQuery] Guid? packageId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] bool openOnly = false,
        CancellationToken ct = default) => tours.DeparturesAsync(packageId, from, to, openOnly, ct);
    [HttpGet("departures/{id:guid}")] public Task<DepartureDto> Departure(Guid id, CancellationToken ct) => tours.DepartureAsync(id, ct);
    [HttpPost("departures")] public Task<DepartureDto> CreateDeparture(SaveDepartureRequest req, CancellationToken ct) => tours.SaveDepartureAsync(null, req, ct);
    [HttpPut("departures/{id:guid}")] public Task<DepartureDto> UpdateDeparture(Guid id, SaveDepartureRequest req, CancellationToken ct) => tours.SaveDepartureAsync(id, req, ct);
    [HttpPost("departures/{id:guid}/status")] public Task<DepartureDto> Status(Guid id, SetDepartureStatusRequest req, CancellationToken ct) => tours.SetStatusAsync(id, req, ct);
    [HttpPost("departures/{id:guid}/guides")] public Task<DepartureDto> AssignGuide(Guid id, AssignGuideRequest req, CancellationToken ct) => tours.AssignGuideAsync(id, req, ct);
    [HttpDelete("departures/{id:guid}/guides/{assignmentId:guid}")]
    public Task<DepartureDto> RemoveGuide(Guid id, Guid assignmentId, CancellationToken ct) => tours.RemoveGuideAsync(id, assignmentId, ct);
    [HttpPost("departures/{id:guid}/costs")] public Task<DepartureDto> AddCost(Guid id, SaveDepartureCostRequest req, CancellationToken ct) => tours.SaveCostAsync(id, null, req, ct);
    [HttpPut("departures/{id:guid}/costs/{costId:guid}")]
    public Task<DepartureDto> UpdateCost(Guid id, Guid costId, SaveDepartureCostRequest req, CancellationToken ct) => tours.SaveCostAsync(id, costId, req, ct);
    [HttpPost("departures/{id:guid}/costs/{costId:guid}/bill")]
    public Task<DepartureDto> BillCost(Guid id, Guid costId, CancellationToken ct) => tours.BillCostAsync(id, costId, ct);
    [HttpGet("departures/{id:guid}/manifest")] public Task<List<ManifestRow>> Manifest(Guid id, CancellationToken ct) => tours.ManifestAsync(id, ct);
}

[ApiController, Authorize, Route("api/travel")]
public class TravelBookingsController(TravelBookingService bookings) : ControllerBase
{
    [HttpGet("dashboard")] public Task<TravelDashboardDto> Dashboard(CancellationToken ct) => bookings.DashboardAsync(ct);
    [HttpGet("bookings")]
    public Task<PagedResult<BookingListItem>> List([FromQuery] TravelBookingStatus? status, [FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken ct = default) => bookings.ListAsync(status, search, page, pageSize, ct);
    [HttpGet("bookings/{id:guid}")] public Task<BookingDto> Get(Guid id, CancellationToken ct) => bookings.GetAsync(id, ct);
    [HttpPost("bookings")] public Task<BookingDto> Create(SaveBookingRequest req, CancellationToken ct) => bookings.SaveAsync(null, req, ct);
    [HttpPut("bookings/{id:guid}")] public Task<BookingDto> Update(Guid id, SaveBookingRequest req, CancellationToken ct) => bookings.SaveAsync(id, req, ct);
    [HttpPost("bookings/{id:guid}/confirm")] public Task<BookingDto> Confirm(Guid id, CancellationToken ct) => bookings.ConfirmAsync(id, ct);
    [HttpPost("bookings/{id:guid}/cancel")] public Task<BookingDto> Cancel(Guid id, CancellationToken ct) => bookings.CancelAsync(id, ct);
    [HttpPost("bookings/{id:guid}/deposits")] public Task<BookingDto> Deposit(Guid id, BookingDepositRequest req, CancellationToken ct) => bookings.DepositAsync(id, req, ct);
    [HttpPost("bookings/{id:guid}/invoice")] public Task<BookingDto> Invoice(Guid id, CancellationToken ct) => bookings.InvoiceAsync(id, ct);
    [HttpPost("bookings/{id:guid}/supplier-bills")] public Task<SupplierBillsResult> SupplierBills(Guid id, CancellationToken ct) => bookings.SupplierBillsAsync(id, ct);
    [HttpPut("bookings/{id:guid}/visas/{itemId:guid}")]
    public Task<BookingDto> Visa(Guid id, Guid itemId, VisaUpdateRequest req, CancellationToken ct) => bookings.UpdateVisaAsync(id, itemId, req, ct);
}
