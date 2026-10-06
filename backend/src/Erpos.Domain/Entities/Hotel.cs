using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>A category of room at one property (entity), with its rack rate.</summary>
public class RoomType : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal BaseRate { get; set; }
    public int MaxAdults { get; set; } = 2;
    public int MaxChildren { get; set; } = 1;
    public string? Description { get; set; }
    /// <summary>Sales tax on room nights (e.g. provincial sales tax on hotel services).</summary>
    public Guid? TaxRateId { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Room : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Number { get; set; } = "";
    public Guid RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    public string? Floor { get; set; }
    public HousekeepingStatus Housekeeping { get; set; } = HousekeepingStatus.Clean;
    public string? HousekeepingNote { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A hotel guest. The finance customer used for invoices is created on first billing.</summary>
public class Guest : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Cnic { get; set; }
    public string? PassportNo { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public bool IsVip { get; set; }
    public string? Notes { get; set; }
    public Guid? ContactId { get; set; }
}

public class Reservation : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Number { get; set; } = "";
    public Guid GuestId { get; set; }
    public Guest? Guest { get; set; }
    /// <summary>Company or travel agent billed instead of the guest.</summary>
    public Guid? BillToContactId { get; set; }
    public Contact? BillToContact { get; set; }
    public BookingSource Source { get; set; } = BookingSource.WalkIn;
    public DateOnly ArrivalDate { get; set; }
    public DateOnly DepartureDate { get; set; }
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public Guid? InvoiceId { get; set; }
    public ICollection<ReservationRoom> Rooms { get; set; } = new List<ReservationRoom>();
}

/// <summary>One room booked on a reservation. The physical room is assigned before check-in.</summary>
public class ReservationRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReservationId { get; set; }
    public Guid RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    public Guid? RoomId { get; set; }
    public Room? Room { get; set; }
    /// <summary>Agreed nightly rate (excluding tax).</summary>
    public decimal Rate { get; set; }
}

/// <summary>A charge on a guest's folio. Room nights are posted by night audit / checkout.</summary>
public class FolioCharge : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ReservationId { get; set; }
    public DateOnly Date { get; set; }
    public FolioChargeType Type { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public Guid? TaxRateId { get; set; }
    public TaxRate? TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid IncomeAccountId { get; set; }
    /// <summary>Room-night charges: which booked room this night belongs to (one charge per room per night).</summary>
    public Guid? ReservationRoomId { get; set; }
    /// <summary>Stock sold from a warehouse (minibar, restaurant items) and the stock transaction that issued it.</summary>
    public Guid? ItemId { get; set; }
    public Guid? StockTransactionId { get; set; }
    public bool IsVoid { get; set; }
}

/// <summary>Money taken from the guest before checkout (held as a customer advance until the invoice exists).</summary>
public class FolioDeposit : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ReservationId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Guid BankAccountId { get; set; }
    public string? Reference { get; set; }
    public Guid? JournalEntryId { get; set; }
}
