using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

public class Vehicle : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string RegistrationNo { get; set; } = "";
    public VehicleType Type { get; set; }
    public string? MakeModel { get; set; }
    public decimal CapacityKg { get; set; }
    public decimal? CapacityCbm { get; set; }
    /// <summary>Hired vehicles belong to a vendor (broker / owner-operator).</summary>
    public bool IsHired { get; set; }
    public Guid? OwnerVendorId { get; set; }
    public Contact? OwnerVendor { get; set; }
    public DateOnly? FitnessExpiry { get; set; }
    public DateOnly? InsuranceExpiry { get; set; }
    public DateOnly? RoutePermitExpiry { get; set; }
    public DateOnly? TokenTaxExpiry { get; set; }
    public int Odometer { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
}

public class Driver : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string FullName { get; set; } = "";
    public string? Cnic { get; set; }
    public string? Phone { get; set; }
    public string LicenseNo { get; set; } = "";
    public string? LicenseCategory { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public decimal DailyAllowance { get; set; }
    public Guid? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A lane with its rate card.</summary>
public class FreightRoute : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public int DistanceKm { get; set; }
    public decimal StandardHours { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal MinimumCharge { get; set; }
    /// <summary>Full-truck-load price for the whole vehicle.</summary>
    public decimal FullTruckRate { get; set; }
    public decimal FuelSurchargePercent { get; set; }
    public Guid? TaxRateId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Consignment note (CN / bilty).</summary>
public class Shipment : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Number { get; set; } = "";
    public DateOnly BookingDate { get; set; }
    /// <summary>Bill-to customer (account holder or the paying party).</summary>
    public Guid CustomerId { get; set; }
    public Contact? Customer { get; set; }
    public string ShipperName { get; set; } = "";
    public string? ShipperPhone { get; set; }
    public string? ShipperAddress { get; set; }
    public string ConsigneeName { get; set; } = "";
    public string? ConsigneePhone { get; set; }
    public string? ConsigneeAddress { get; set; }
    public string OriginCity { get; set; } = "";
    public string DestinationCity { get; set; } = "";
    public Guid? RouteId { get; set; }
    public FreightRoute? Route { get; set; }
    public ServiceLevel Service { get; set; } = ServiceLevel.PartLoad;
    public string? GoodsDescription { get; set; }
    public int Pieces { get; set; } = 1;
    public decimal WeightKg { get; set; }
    public decimal? VolumeCbm { get; set; }
    public decimal DeclaredValue { get; set; }
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Account;

    public decimal Freight { get; set; }
    public decimal FuelSurcharge { get; set; }
    public decimal OtherCharges { get; set; }
    public Guid? TaxRateId { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    /// <summary>Goods value to collect from the consignee on behalf of the shipper.</summary>
    public decimal CodAmount { get; set; }
    public decimal CodCollected { get; set; }
    public bool CodRemitted { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Booked;
    public DateOnly? PromisedDate { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ReceivedBy { get; set; }
    public string? DeliveryRemarks { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? CurrentTripId { get; set; }
    public ICollection<ShipmentEvent> Events { get; set; } = new List<ShipmentEvent>();
}

public class ShipmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public ShipmentStatus Status { get; set; }
    public string? Location { get; set; }
    public string? Remarks { get; set; }
    public Guid? ByUserId { get; set; }
}

/// <summary>A vehicle movement carrying consignments (load sheet / manifest).</summary>
public class Trip : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Number { get; set; } = "";
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }
    public Guid? RouteId { get; set; }
    public FreightRoute? Route { get; set; }
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public DateOnly PlannedDate { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public int? OdometerStart { get; set; }
    public int? OdometerEnd { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Planned;
    public string? Notes { get; set; }
    public ICollection<TripExpense> Expenses { get; set; } = new List<TripExpense>();
}

public class TripExpense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public TripExpenseType Type { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Paid in cash/bank now (posted immediately) …</summary>
    public Guid? PaidFromAccountId { get; set; }
    /// <summary>… or owed to a vendor (fuel station on credit, vehicle owner) as a draft bill.</summary>
    public Guid? VendorId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? BillId { get; set; }
    public DateOnly Date { get; set; }
}

public class MaintenanceRecord : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public int? Odometer { get; set; }
    public decimal Cost { get; set; }
    public Guid? VendorId { get; set; }
    public Contact? Vendor { get; set; }
    public Guid? BillId { get; set; }
    public DateOnly? NextServiceDate { get; set; }
}
