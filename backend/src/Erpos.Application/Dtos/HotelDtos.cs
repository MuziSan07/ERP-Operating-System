using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Setup ----
public record RoomTypeDto(Guid Id, Guid EntityId, string EntityName, string Code, string Name, decimal BaseRate, int MaxAdults, int MaxChildren,
    string? Description, Guid? TaxRateId, Guid? IncomeAccountId, bool IsActive, int RoomCount);
public record SaveRoomTypeRequest(Guid EntityId, string Code, string Name, decimal BaseRate, int MaxAdults, int MaxChildren, string? Description,
    Guid? TaxRateId, Guid? IncomeAccountId, bool IsActive);
public record RoomDto(Guid Id, Guid EntityId, string Number, Guid RoomTypeId, string RoomTypeName, string? Floor, HousekeepingStatus Housekeeping,
    string? HousekeepingNote, bool IsActive, bool Occupied, string? GuestName, Guid? ReservationId, DateOnly? DepartureDate);
public record SaveRoomRequest(Guid EntityId, string Number, Guid RoomTypeId, string? Floor, bool IsActive);
public record HousekeepingUpdate(HousekeepingStatus Status, string? Note);

// ---- Guests ----
public record GuestDto(Guid Id, string FullName, string? Phone, string? Email, string? Cnic, string? PassportNo, string? Nationality,
    string? Address, string? City, bool IsVip, string? Notes, int Stays);
public record SaveGuestRequest(string FullName, string? Phone, string? Email, string? Cnic, string? PassportNo, string? Nationality,
    string? Address, string? City, bool IsVip, string? Notes);

// ---- Reservations ----
public record ReservationRoomInput(Guid RoomTypeId, Guid? RoomId, decimal? Rate);
public record SaveReservationRequest(Guid EntityId, Guid? GuestId, SaveGuestRequest? NewGuest, Guid? BillToContactId, BookingSource Source,
    DateOnly ArrivalDate, DateOnly DepartureDate, int Adults, int Children, bool Tentative, string? ExternalReference, string? Notes,
    List<ReservationRoomInput> Rooms);
public record ReservationRoomDto(Guid Id, Guid RoomTypeId, string RoomTypeName, Guid? RoomId, string? RoomNumber, decimal Rate);
public record FolioChargeDto(Guid Id, DateOnly Date, FolioChargeType Type, string Description, decimal Quantity, decimal UnitPrice, decimal Amount,
    string? TaxName, decimal TaxAmount, bool IsVoid, bool FromStock);
public record FolioDepositDto(Guid Id, DateOnly Date, decimal Amount, string BankAccountName, string? Reference);
public record ReservationDto(Guid Id, string Number, Guid EntityId, string EntityName, Guid GuestId, GuestDto Guest, Guid? BillToContactId,
    string? BillToName, BookingSource Source, DateOnly ArrivalDate, DateOnly DepartureDate, int Nights, int Adults, int Children,
    ReservationStatus Status, string? ExternalReference, string? Notes, DateTime? CheckedInAt, DateTime? CheckedOutAt, Guid? InvoiceId,
    string? InvoiceNumber, IReadOnlyList<ReservationRoomDto> Rooms, IReadOnlyList<FolioChargeDto> Charges, IReadOnlyList<FolioDepositDto> Deposits,
    decimal ChargesTotal, decimal TaxTotal, decimal DepositsTotal, decimal Balance, decimal EstimatedStayTotal);
public record ReservationListItem(Guid Id, string Number, string GuestName, string? Phone, bool IsVip, string EntityName, DateOnly ArrivalDate,
    DateOnly DepartureDate, int Nights, string Rooms, BookingSource Source, ReservationStatus Status, decimal Balance);

public record AddChargeRequest(FolioChargeType Type, string Description, decimal Quantity, decimal UnitPrice, Guid? TaxRateId, Guid? IncomeAccountId,
    Guid? ItemId, Guid? WarehouseId, DateOnly? Date);
public record DepositRequest(decimal Amount, Guid BankAccountId, DateOnly? Date, string? Reference);
public record AssignRoomRequest(Guid ReservationRoomId, Guid RoomId);
public record CheckOutRequest(DateOnly? Date, Guid? BankAccountId, decimal? AmountPaid, string? PaymentReference);
public record NightAuditRequest(Guid EntityId, DateOnly Date);
public record NightAuditResult(DateOnly Date, int ReservationsCharged, decimal RoomRevenue);

// ---- Front desk ----
public record AvailabilityRow(Guid RoomTypeId, string RoomTypeName, decimal BaseRate, int TotalRooms, IReadOnlyList<int> AvailableByNight, int MinAvailable);
public record AvailabilityDto(DateOnly From, DateOnly To, IReadOnlyList<DateOnly> Nights, IReadOnlyList<AvailabilityRow> RoomTypes);
public record FrontDeskDto(DateOnly Date, int TotalRooms, int Occupied, int VacantClean, int VacantDirty, int OutOfOrder, decimal OccupancyPercent,
    IReadOnlyList<ReservationListItem> Arrivals, IReadOnlyList<ReservationListItem> Departures, IReadOnlyList<ReservationListItem> InHouse);
public record TapeBlock(Guid ReservationId, string Number, string GuestName, DateOnly From, DateOnly To, ReservationStatus Status, bool IsVip);
public record TapeRoom(Guid RoomId, string Number, string RoomTypeName, HousekeepingStatus Housekeeping, IReadOnlyList<TapeBlock> Blocks);
public record TapeChartDto(DateOnly From, int Days, IReadOnlyList<TapeRoom> Rooms, IReadOnlyList<TapeBlock> Unassigned);
public record HotelReportRow(DateOnly Date, int Available, int Sold, decimal Occupancy, decimal RoomRevenue, decimal Adr, decimal RevPar);
public record HotelReportDto(DateOnly From, DateOnly To, IReadOnlyList<HotelReportRow> Days, int Available, int Sold, decimal Occupancy,
    decimal RoomRevenue, decimal Adr, decimal RevPar, IReadOnlyDictionary<string, decimal> RevenueByType);
