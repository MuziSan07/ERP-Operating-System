using Erpos.Application.Dtos;
using Erpos.Application.Hotel;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/hotel")]
public class HotelSetupController(HotelSetupService setup) : ControllerBase
{
    [HttpGet("room-types")] public Task<List<RoomTypeDto>> RoomTypes([FromQuery] Guid? entityId, CancellationToken ct) => setup.RoomTypesAsync(entityId, ct);
    [HttpPost("room-types")] public Task<RoomTypeDto> CreateRoomType(SaveRoomTypeRequest req, CancellationToken ct) => setup.SaveRoomTypeAsync(null, req, ct);
    [HttpPut("room-types/{id:guid}")] public Task<RoomTypeDto> UpdateRoomType(Guid id, SaveRoomTypeRequest req, CancellationToken ct) => setup.SaveRoomTypeAsync(id, req, ct);

    [HttpGet("rooms")] public Task<List<RoomDto>> Rooms([FromQuery] Guid? entityId, CancellationToken ct) => setup.RoomsAsync(entityId, ct);
    [HttpPost("rooms")] public Task<RoomDto> CreateRoom(SaveRoomRequest req, CancellationToken ct) => setup.SaveRoomAsync(null, req, ct);
    [HttpPut("rooms/{id:guid}")] public Task<RoomDto> UpdateRoom(Guid id, SaveRoomRequest req, CancellationToken ct) => setup.SaveRoomAsync(id, req, ct);
    [HttpPut("rooms/{id:guid}/housekeeping")]
    public Task<RoomDto> Housekeeping(Guid id, HousekeepingUpdate req, CancellationToken ct) => setup.SetHousekeepingAsync(id, req, ct);

    [HttpGet("guests")] public Task<List<GuestDto>> Guests([FromQuery] string? search, CancellationToken ct) => setup.GuestsAsync(search, ct);
    [HttpPost("guests")] public Task<GuestDto> CreateGuest(SaveGuestRequest req, CancellationToken ct) => setup.SaveGuestAsync(null, req, ct);
    [HttpPut("guests/{id:guid}")] public Task<GuestDto> UpdateGuest(Guid id, SaveGuestRequest req, CancellationToken ct) => setup.SaveGuestAsync(id, req, ct);
}

[ApiController, Authorize, Route("api/hotel")]
public class ReservationsController(ReservationService reservations) : ControllerBase
{
    [HttpGet("availability")]
    public Task<AvailabilityDto> Availability([FromQuery] Guid entityId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        reservations.AvailabilityAsync(entityId, from, to, ct);
    [HttpGet("front-desk")]
    public Task<FrontDeskDto> FrontDesk([FromQuery] Guid entityId, [FromQuery] DateOnly? date, CancellationToken ct) => reservations.FrontDeskAsync(entityId, date, ct);
    [HttpGet("tape-chart")]
    public Task<TapeChartDto> TapeChart([FromQuery] Guid entityId, [FromQuery] DateOnly from, [FromQuery] int days = 14, CancellationToken ct = default) =>
        reservations.TapeChartAsync(entityId, from, days, ct);
    [HttpGet("report")]
    public Task<HotelReportDto> Report([FromQuery] Guid entityId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        reservations.ReportAsync(entityId, from, to, ct);
    [HttpPost("night-audit")] public Task<NightAuditResult> NightAudit(NightAuditRequest req, CancellationToken ct) => reservations.NightAuditAsync(req, ct);

    [HttpGet("reservations")]
    public Task<PagedResult<ReservationListItem>> List([FromQuery] Guid? entityId, [FromQuery] ReservationStatus? status, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        reservations.ListAsync(entityId, status, from, to, search, page, pageSize, ct);
    [HttpGet("reservations/{id:guid}")] public Task<ReservationDto> Get(Guid id, CancellationToken ct) => reservations.GetAsync(id, ct);
    [HttpPost("reservations")] public Task<ReservationDto> Create(SaveReservationRequest req, CancellationToken ct) => reservations.SaveAsync(null, req, ct);
    [HttpPut("reservations/{id:guid}")] public Task<ReservationDto> Update(Guid id, SaveReservationRequest req, CancellationToken ct) => reservations.SaveAsync(id, req, ct);
    [HttpPost("reservations/{id:guid}/assign-room")] public Task<ReservationDto> Assign(Guid id, AssignRoomRequest req, CancellationToken ct) => reservations.AssignRoomAsync(id, req, ct);
    [HttpPost("reservations/{id:guid}/cancel")] public Task<ReservationDto> Cancel(Guid id, CancellationToken ct) => reservations.CancelAsync(id, false, ct);
    [HttpPost("reservations/{id:guid}/no-show")] public Task<ReservationDto> NoShow(Guid id, CancellationToken ct) => reservations.CancelAsync(id, true, ct);
    [HttpPost("reservations/{id:guid}/check-in")] public Task<ReservationDto> CheckIn(Guid id, CancellationToken ct) => reservations.CheckInAsync(id, ct);
    [HttpPost("reservations/{id:guid}/charges")] public Task<ReservationDto> Charge(Guid id, AddChargeRequest req, CancellationToken ct) => reservations.AddChargeAsync(id, req, ct);
    [HttpPost("reservations/{id:guid}/charges/{chargeId:guid}/void")]
    public Task<ReservationDto> VoidCharge(Guid id, Guid chargeId, CancellationToken ct) => reservations.VoidChargeAsync(id, chargeId, ct);
    [HttpPost("reservations/{id:guid}/deposits")] public Task<ReservationDto> Deposit(Guid id, DepositRequest req, CancellationToken ct) => reservations.DepositAsync(id, req, ct);
    [HttpPost("reservations/{id:guid}/check-out")] public Task<ReservationDto> CheckOut(Guid id, CheckOutRequest req, CancellationToken ct) => reservations.CheckOutAsync(id, req, ct);
}
