using Erpos.Application.Dtos;
using Erpos.Application.Logistics;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/logistics")]
public class FleetController(FleetService fleet) : ControllerBase
{
    [HttpGet("vehicles")] public Task<List<VehicleDto>> Vehicles(CancellationToken ct) => fleet.VehiclesAsync(ct);
    [HttpPost("vehicles")] public Task<VehicleDto> CreateVehicle(SaveVehicleRequest req, CancellationToken ct) => fleet.SaveVehicleAsync(null, req, ct);
    [HttpPut("vehicles/{id:guid}")] public Task<VehicleDto> UpdateVehicle(Guid id, SaveVehicleRequest req, CancellationToken ct) => fleet.SaveVehicleAsync(id, req, ct);
    [HttpGet("drivers")] public Task<List<DriverDto>> Drivers(CancellationToken ct) => fleet.DriversAsync(ct);
    [HttpPost("drivers")] public Task<DriverDto> CreateDriver(SaveDriverRequest req, CancellationToken ct) => fleet.SaveDriverAsync(null, req, ct);
    [HttpPut("drivers/{id:guid}")] public Task<DriverDto> UpdateDriver(Guid id, SaveDriverRequest req, CancellationToken ct) => fleet.SaveDriverAsync(id, req, ct);
    [HttpGet("routes")] public Task<List<RouteDto>> Routes(CancellationToken ct) => fleet.RoutesAsync(ct);
    [HttpPost("routes")] public Task<RouteDto> CreateRoute(SaveRouteRequest req, CancellationToken ct) => fleet.SaveRouteAsync(null, req, ct);
    [HttpPut("routes/{id:guid}")] public Task<RouteDto> UpdateRoute(Guid id, SaveRouteRequest req, CancellationToken ct) => fleet.SaveRouteAsync(id, req, ct);
    [HttpGet("maintenance")] public Task<List<MaintenanceDto>> Maintenance([FromQuery] Guid? vehicleId, CancellationToken ct) => fleet.MaintenanceAsync(vehicleId, ct);
    [HttpPost("maintenance")] public Task<MaintenanceDto> AddMaintenance(SaveMaintenanceRequest req, CancellationToken ct) => fleet.AddMaintenanceAsync(req, ct);
}

[ApiController, Authorize, Route("api/logistics")]
public class ShipmentsController(ShipmentService shipments) : ControllerBase
{
    [HttpGet("dashboard")] public Task<LogisticsDashboardDto> Dashboard(CancellationToken ct) => shipments.DashboardAsync(ct);
    [HttpPost("quote")] public Task<QuoteDto> Quote(QuoteRequest req, CancellationToken ct) => shipments.QuoteAsync(req, ct);
    [HttpGet("shipments")]
    public Task<PagedResult<ShipmentListItem>> List([FromQuery] ShipmentStatus? status, [FromQuery] Guid? customerId, [FromQuery] string? search,
        [FromQuery] bool openOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        shipments.ListAsync(status, customerId, search, openOnly, page, pageSize, ct);
    [HttpGet("shipments/{id:guid}")] public Task<ShipmentDto> Get(Guid id, CancellationToken ct) => shipments.GetAsync(id, ct);
    [HttpGet("track/{number}")] public Task<ShipmentDto> Track(string number, CancellationToken ct) => shipments.TrackAsync(number, ct);
    [HttpPost("shipments")] public Task<ShipmentDto> Create(SaveShipmentRequest req, CancellationToken ct) => shipments.SaveAsync(null, req, ct);
    [HttpPut("shipments/{id:guid}")] public Task<ShipmentDto> Update(Guid id, SaveShipmentRequest req, CancellationToken ct) => shipments.SaveAsync(id, req, ct);
    [HttpPost("shipments/{id:guid}/cancel")] public Task<ShipmentDto> Cancel(Guid id, CancellationToken ct) => shipments.CancelAsync(id, ct);
    [HttpPost("shipments/{id:guid}/status")] public Task<ShipmentDto> Status(Guid id, TrackRequest req, CancellationToken ct) => shipments.UpdateStatusAsync(id, req, ct);
    [HttpPost("shipments/{id:guid}/deliver")] public Task<ShipmentDto> Deliver(Guid id, DeliverRequest req, CancellationToken ct) => shipments.DeliverAsync(id, req, ct);
    [HttpPost("cod/remit")] public Task<CodRemitResult> RemitCod(CodRemitRequest req, CancellationToken ct) => shipments.RemitCodAsync(req, ct);
    [HttpPost("billing")] public Task<BillCustomerResult> Bill(BillCustomerRequest req, CancellationToken ct) => shipments.BillCustomerAsync(req, ct);
}

[ApiController, Authorize, Route("api/logistics/trips")]
public class TripsController(TripService trips) : ControllerBase
{
    [HttpGet] public Task<List<TripListItem>> List([FromQuery] TripStatus? status, CancellationToken ct) => trips.ListAsync(status, ct);
    [HttpGet("{id:guid}")] public Task<TripDto> Get(Guid id, CancellationToken ct) => trips.GetAsync(id, ct);
    [HttpPost] public Task<TripDto> Create(SaveTripRequest req, CancellationToken ct) => trips.SaveAsync(null, req, ct);
    [HttpPut("{id:guid}")] public Task<TripDto> Update(Guid id, SaveTripRequest req, CancellationToken ct) => trips.SaveAsync(id, req, ct);
    [HttpPost("{id:guid}/load")] public Task<TripDto> Load(Guid id, TripShipmentsRequest req, CancellationToken ct) => trips.LoadAsync(id, req, ct);
    [HttpDelete("{id:guid}/shipments/{shipmentId:guid}")] public Task<TripDto> Unload(Guid id, Guid shipmentId, CancellationToken ct) => trips.UnloadAsync(id, shipmentId, ct);
    [HttpPost("{id:guid}/dispatch")] public Task<TripDto> Dispatch(Guid id, DispatchRequest req, CancellationToken ct) => trips.DispatchAsync(id, req, ct);
    [HttpPost("{id:guid}/arrive")] public Task<TripDto> Arrive(Guid id, ArriveRequest req, CancellationToken ct) => trips.ArriveAsync(id, req, ct);
    [HttpPost("{id:guid}/cancel")] public Task<TripDto> Cancel(Guid id, CancellationToken ct) => trips.CancelAsync(id, ct);
    [HttpPost("{id:guid}/expenses")] public Task<TripDto> Expense(Guid id, AddTripExpenseRequest req, CancellationToken ct) => trips.AddExpenseAsync(id, req, ct);
}
