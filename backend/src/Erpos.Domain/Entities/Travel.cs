using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>A sellable tour product with a day-by-day itinerary.</summary>
public class TourPackage : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Destination { get; set; } = "";
    public int DurationDays { get; set; } = 1;
    public string? Summary { get; set; }
    public string? Inclusions { get; set; }
    public string? Exclusions { get; set; }
    public decimal AdultPrice { get; set; }
    public decimal ChildPrice { get; set; }
    public decimal SingleSupplement { get; set; }
    public Guid? TaxRateId { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ItineraryDay> Itinerary { get; set; } = new List<ItineraryDay>();
}

public class ItineraryDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TourPackageId { get; set; }
    public int DayNumber { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Overnight { get; set; }
    public string? Meals { get; set; }
}

public class Guide : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Languages { get; set; }
    public string? LicenseNo { get; set; }
    public decimal DailyRate { get; set; }
    public Guid? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A dated run of a package with limited seats.</summary>
public class TourDeparture : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public Guid TourPackageId { get; set; }
    public TourPackage? TourPackage { get; set; }
    public string Code { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Capacity { get; set; }
    public decimal AdultPrice { get; set; }
    public decimal ChildPrice { get; set; }
    public DepartureStatus Status { get; set; } = DepartureStatus.Open;
    public string? Notes { get; set; }
    public ICollection<DepartureGuide> Guides { get; set; } = new List<DepartureGuide>();
    public ICollection<DepartureCost> Costs { get; set; } = new List<DepartureCost>();
}

public class DepartureGuide
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TourDepartureId { get; set; }
    public Guid GuideId { get; set; }
    public Guide? Guide { get; set; }
    public string? Role { get; set; }
}

/// <summary>Planned operating cost of a departure (coach, hotels, permits). Billing it creates a vendor bill.</summary>
public class DepartureCost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TourDepartureId { get; set; }
    public string Description { get; set; } = "";
    public Guid? VendorId { get; set; }
    public Contact? Vendor { get; set; }
    public decimal Amount { get; set; }
    public Guid? BillId { get; set; }
}

/// <summary>A customer's travel file: passengers plus sold services (tour seats, flights, hotels, visas…).</summary>
public class TravelBooking : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Number { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Contact? Customer { get; set; }
    public string? ContactPhone { get; set; }
    public DateOnly? TravelDate { get; set; }
    public TravelBookingStatus Status { get; set; } = TravelBookingStatus.Quotation;
    public string? Notes { get; set; }
    public Guid? InvoiceId { get; set; }
    public ICollection<BookingPassenger> Passengers { get; set; } = new List<BookingPassenger>();
    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
}

public class BookingPassenger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TravelBookingId { get; set; }
    public string FullName { get; set; } = "";
    public PassengerType Type { get; set; } = PassengerType.Adult;
    public string? PassportNo { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public string? Nationality { get; set; }
    public string? Cnic { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
}

public class BookingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TravelBookingId { get; set; }
    public BookingItemType Type { get; set; }
    public string Description { get; set; } = "";
    public DateOnly? ServiceDate { get; set; }
    public Guid? SupplierId { get; set; }
    public Contact? Supplier { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? TaxRateId { get; set; }
    public Guid? IncomeAccountId { get; set; }

    // Tour seats
    public Guid? TourDepartureId { get; set; }
    public TourDeparture? TourDeparture { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }

    // Flights
    public string? Airline { get; set; }
    public string? Pnr { get; set; }
    public string? TicketNumber { get; set; }
    public string? Route { get; set; }

    // Visas
    public string? Country { get; set; }
    public VisaStatus? VisaStatus { get; set; }
    public Guid? PassengerId { get; set; }

    /// <summary>Supplier bill raised for this item's cost.</summary>
    public Guid? BillId { get; set; }
}

/// <summary>Advance received on a booking before it is invoiced (held as a customer advance).</summary>
public class BookingDeposit : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid TravelBookingId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Guid BankAccountId { get; set; }
    public string? Reference { get; set; }
    public Guid? JournalEntryId { get; set; }
    public bool Applied { get; set; }
}
