using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Fleet, drivers, routes ----
public record VehicleDto(Guid Id, Guid EntityId, string EntityName, string RegistrationNo, VehicleType Type, string? MakeModel, decimal CapacityKg,
    decimal? CapacityCbm, bool IsHired, Guid? OwnerVendorId, string? OwnerVendorName, DateOnly? FitnessExpiry, DateOnly? InsuranceExpiry,
    DateOnly? RoutePermitExpiry, DateOnly? TokenTaxExpiry, int Odometer, VehicleStatus Status, IReadOnlyList<string> DocumentAlerts, string? CurrentTrip);
public record SaveVehicleRequest(Guid EntityId, string RegistrationNo, VehicleType Type, string? MakeModel, decimal CapacityKg, decimal? CapacityCbm,
    bool IsHired, Guid? OwnerVendorId, DateOnly? FitnessExpiry, DateOnly? InsuranceExpiry, DateOnly? RoutePermitExpiry, DateOnly? TokenTaxExpiry,
    int Odometer, VehicleStatus Status);
public record DriverDto(Guid Id, Guid EntityId, string FullName, string? Cnic, string? Phone, string LicenseNo, string? LicenseCategory,
    DateOnly? LicenseExpiry, decimal DailyAllowance, Guid? EmployeeId, bool IsActive, bool OnTrip, string? LicenseAlert);
public record SaveDriverRequest(Guid EntityId, string FullName, string? Cnic, string? Phone, string LicenseNo, string? LicenseCategory,
    DateOnly? LicenseExpiry, decimal DailyAllowance, Guid? EmployeeId, bool IsActive);
public record RouteDto(Guid Id, string Code, string Origin, string Destination, int DistanceKm, decimal StandardHours, decimal RatePerKg,
    decimal MinimumCharge, decimal FullTruckRate, decimal FuelSurchargePercent, Guid? TaxRateId, bool IsActive);
public record SaveRouteRequest(string Code, string Origin, string Destination, int DistanceKm, decimal StandardHours, decimal RatePerKg,
    decimal MinimumCharge, decimal FullTruckRate, decimal FuelSurchargePercent, Guid? TaxRateId, bool IsActive);
public record MaintenanceDto(Guid Id, Guid VehicleId, string RegistrationNo, DateOnly Date, string Description, int? Odometer, decimal Cost,
    string? VendorName, Guid? BillId, DateOnly? NextServiceDate);
public record SaveMaintenanceRequest(Guid VehicleId, DateOnly Date, string Description, int? Odometer, decimal Cost, Guid? VendorId, DateOnly? NextServiceDate);

// ---- Shipments ----
public record QuoteRequest(Guid? RouteId, ServiceLevel Service, decimal WeightKg, decimal OtherCharges);
public record QuoteDto(decimal Freight, decimal FuelSurcharge, decimal OtherCharges, decimal Subtotal, decimal TaxAmount, decimal Total, string Basis);
public record SaveShipmentRequest(Guid EntityId, Guid CustomerId, DateOnly BookingDate, string ShipperName, string? ShipperPhone, string? ShipperAddress,
    string ConsigneeName, string? ConsigneePhone, string? ConsigneeAddress, string OriginCity, string DestinationCity, Guid? RouteId,
    ServiceLevel Service, string? GoodsDescription, int Pieces, decimal WeightKg, decimal? VolumeCbm, decimal DeclaredValue, PaymentMode PaymentMode,
    decimal? FreightOverride, decimal OtherCharges, decimal CodAmount, DateOnly? PromisedDate,
    Guid? PaidIntoAccountId);
public record ShipmentEventDto(DateTime At, ShipmentStatus Status, string? Location, string? Remarks, string? ByName);
public record ShipmentDto(Guid Id, string Number, Guid EntityId, string EntityName, DateOnly BookingDate, Guid CustomerId, string CustomerName,
    string ShipperName, string? ShipperPhone, string? ShipperAddress, string ConsigneeName, string? ConsigneePhone, string? ConsigneeAddress,
    string OriginCity, string DestinationCity, Guid? RouteId, string? RouteCode, ServiceLevel Service, string? GoodsDescription, int Pieces,
    decimal WeightKg, decimal? VolumeCbm, decimal DeclaredValue, PaymentMode PaymentMode, decimal Freight, decimal FuelSurcharge, decimal OtherCharges,
    decimal TaxAmount, decimal Total, decimal CodAmount, decimal CodCollected, bool CodRemitted, ShipmentStatus Status, DateOnly? PromisedDate,
    DateTime? DeliveredAt, string? ReceivedBy, string? DeliveryRemarks, Guid? InvoiceId, string? InvoiceNumber, string? TripNumber,
    IReadOnlyList<ShipmentEventDto> Events);
public record ShipmentListItem(Guid Id, string Number, DateOnly BookingDate, string CustomerName, string ConsigneeName, string OriginCity,
    string DestinationCity, int Pieces, decimal WeightKg, PaymentMode PaymentMode, decimal Total, decimal CodAmount, ShipmentStatus Status,
    string? TripNumber, bool Late, bool Invoiced);
public record TrackRequest(ShipmentStatus Status, string? Location, string? Remarks);
public record DeliverRequest(string ReceivedBy, string? Remarks, decimal? CodCollected, Guid? CollectedIntoAccountId);
public record CodRemitRequest(Guid CustomerId, Guid BankAccountId, DateOnly? Date, string? Reference);
public record CodRemitResult(int Shipments, decimal Amount, Guid JournalEntryId);
public record BillCustomerRequest(Guid CustomerId, DateOnly? UpTo);
public record BillCustomerResult(Guid InvoiceId, string? InvoiceNumber, int Shipments, decimal Total);

// ---- Trips ----
public record TripExpenseDto(Guid Id, TripExpenseType Type, string? Description, decimal Amount, string? PaidFrom, string? VendorName, Guid? BillId, DateOnly Date);
public record TripListItem(Guid Id, string Number, string VehicleNo, string DriverName, string Origin, string Destination, DateOnly PlannedDate,
    TripStatus Status, int Shipments, decimal LoadKg, decimal CapacityKg, decimal Freight, decimal Expenses);
public record TripDto(Guid Id, string Number, Guid EntityId, Guid VehicleId, string VehicleNo, VehicleType VehicleType, decimal CapacityKg, Guid DriverId,
    string DriverName, string? DriverPhone, Guid? RouteId, string? RouteCode, string Origin, string Destination, DateOnly PlannedDate,
    DateTime? DispatchedAt, DateTime? ArrivedAt, int? OdometerStart, int? OdometerEnd, TripStatus Status, string? Notes,
    IReadOnlyList<ShipmentListItem> Shipments, IReadOnlyList<TripExpenseDto> Expenses, decimal LoadKg, decimal Freight, decimal ExpensesTotal, decimal Margin);
public record SaveTripRequest(Guid EntityId, Guid VehicleId, Guid DriverId, Guid? RouteId, string? Origin, string? Destination, DateOnly PlannedDate, string? Notes);
public record TripShipmentsRequest(List<Guid> ShipmentIds);
public record DispatchRequest(int? OdometerStart);
public record ArriveRequest(int? OdometerEnd, string? Location);
public record AddTripExpenseRequest(TripExpenseType Type, string? Description, decimal Amount, Guid? PaidFromAccountId, Guid? VendorId, DateOnly? Date);

// ---- Dashboard ----
public record ExpiryAlert(string Kind, string Subject, string Document, DateOnly? Expiry, int? DaysLeft);
public record LogisticsDashboardDto(IReadOnlyDictionary<string, int> ShipmentsByStatus, int BookedToday, int DeliveredToday, decimal OnTimePercent,
    decimal RevenueThisMonth, decimal CodPendingRemittance, IReadOnlyDictionary<string, int> FleetByStatus, IReadOnlyList<ExpiryAlert> Alerts,
    IReadOnlyList<TripListItem> ActiveTrips);
