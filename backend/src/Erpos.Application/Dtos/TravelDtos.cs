using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Packages, guides, departures ----
public record ItineraryDayDto(int DayNumber, string Title, string? Description, string? Overnight, string? Meals);
public record TourPackageDto(Guid Id, Guid EntityId, string EntityName, string Code, string Name, string Destination, int DurationDays, string? Summary,
    string? Inclusions, string? Exclusions, decimal AdultPrice, decimal ChildPrice, decimal SingleSupplement, Guid? TaxRateId, Guid? IncomeAccountId,
    bool IsActive, int UpcomingDepartures, IReadOnlyList<ItineraryDayDto> Itinerary);
public record SaveTourPackageRequest(Guid EntityId, string Code, string Name, string Destination, int DurationDays, string? Summary, string? Inclusions,
    string? Exclusions, decimal AdultPrice, decimal ChildPrice, decimal SingleSupplement, Guid? TaxRateId, Guid? IncomeAccountId, bool IsActive,
    List<ItineraryDayDto> Itinerary);

public record GuideDto(Guid Id, string FullName, string? Phone, string? Languages, string? LicenseNo, decimal DailyRate, Guid? EmployeeId, bool IsActive,
    IReadOnlyList<string> UpcomingDepartures);
public record SaveGuideRequest(string FullName, string? Phone, string? Languages, string? LicenseNo, decimal DailyRate, Guid? EmployeeId, bool IsActive);

public record DepartureGuideDto(Guid Id, Guid GuideId, string GuideName, string? Phone, string? Role, decimal DailyRate);
public record DepartureCostDto(Guid Id, string Description, Guid? VendorId, string? VendorName, decimal Amount, Guid? BillId, string? BillNumber);
public record DepartureListItem(Guid Id, string Code, Guid TourPackageId, string PackageName, string Destination, DateOnly StartDate, DateOnly EndDate,
    int Capacity, int Booked, int SeatsLeft, decimal LoadFactor, decimal AdultPrice, DepartureStatus Status, string Guides);
public record DepartureDto(Guid Id, Guid EntityId, string Code, Guid TourPackageId, string PackageName, string Destination, DateOnly StartDate, DateOnly EndDate,
    int Capacity, int Booked, int SeatsLeft, decimal AdultPrice, decimal ChildPrice, DepartureStatus Status, string? Notes,
    IReadOnlyList<DepartureGuideDto> Guides, IReadOnlyList<DepartureCostDto> Costs, decimal Revenue, decimal TotalCost, decimal Margin);
public record SaveDepartureRequest(Guid TourPackageId, DateOnly StartDate, int Capacity, decimal? AdultPrice, decimal? ChildPrice, string? Notes);
public record SetDepartureStatusRequest(DepartureStatus Status);
public record AssignGuideRequest(Guid GuideId, string? Role);
public record SaveDepartureCostRequest(string Description, Guid? VendorId, decimal Amount);
public record ManifestRow(string BookingNumber, string CustomerName, string PassengerName, PassengerType Type, string? PassportNo, DateOnly? PassportExpiry,
    string? Nationality, string? Cnic, string? Phone, bool PassportWarning);

// ---- Bookings ----
public record PassengerDto(Guid Id, string FullName, PassengerType Type, string? PassportNo, DateOnly? PassportExpiry, string? Nationality, string? Cnic,
    DateOnly? DateOfBirth, string? Phone);
public record PassengerInput(Guid? Id, string FullName, PassengerType Type, string? PassportNo, DateOnly? PassportExpiry, string? Nationality, string? Cnic,
    DateOnly? DateOfBirth, string? Phone);
public record BookingItemDto(Guid Id, BookingItemType Type, string Description, DateOnly? ServiceDate, Guid? SupplierId, string? SupplierName,
    decimal Quantity, decimal UnitCost, decimal UnitPrice, decimal CostTotal, decimal SellTotal, Guid? TaxRateId, Guid? TourDepartureId, string? DepartureCode,
    int Adults, int Children, string? Airline, string? Pnr, string? TicketNumber, string? Route, string? Country, VisaStatus? VisaStatus,
    Guid? PassengerId, string? PassengerName, Guid? BillId);
public record BookingItemInput(BookingItemType Type, string? Description, DateOnly? ServiceDate, Guid? SupplierId, decimal Quantity, decimal UnitCost,
    decimal UnitPrice, Guid? TaxRateId, Guid? IncomeAccountId, Guid? TourDepartureId, int Adults, int Children, string? Airline, string? Pnr,
    string? TicketNumber, string? Route, string? Country, VisaStatus? VisaStatus, int? PassengerIndex);
public record SaveBookingRequest(Guid EntityId, Guid CustomerId, string? ContactPhone, DateOnly? TravelDate, string? Notes,
    List<PassengerInput> Passengers, List<BookingItemInput> Items);
public record BookingDepositDto(Guid Id, DateOnly Date, decimal Amount, string BankAccountName, string? Reference, bool Applied);
public record BookingDto(Guid Id, string Number, Guid EntityId, string EntityName, Guid CustomerId, string CustomerName, string? ContactPhone,
    DateOnly? TravelDate, TravelBookingStatus Status, string? Notes, Guid? InvoiceId, string? InvoiceNumber, IReadOnlyList<PassengerDto> Passengers,
    IReadOnlyList<BookingItemDto> Items, IReadOnlyList<BookingDepositDto> Deposits, decimal SellTotal, decimal CostTotal, decimal Margin,
    decimal DepositsTotal, IReadOnlyList<string> Warnings, string? CreatedByName);
public record BookingListItem(Guid Id, string Number, string CustomerName, DateOnly? TravelDate, int Passengers, string Services,
    TravelBookingStatus Status, decimal SellTotal, decimal Margin, int Warnings);
public record VisaUpdateRequest(VisaStatus Status);
public record BookingDepositRequest(decimal Amount, Guid BankAccountId, DateOnly? Date, string? Reference);
public record SupplierBillsResult(IReadOnlyList<Guid> BillIds, int Suppliers);

// ---- Dashboard ----
public record VisaRow(Guid BookingId, string BookingNumber, string CustomerName, string? PassengerName, string? Country, VisaStatus Status, DateOnly? TravelDate);
public record TravelDashboardDto(int OpenBookings, int ConfirmedThisMonth, decimal SalesThisMonth, decimal MarginThisMonth,
    IReadOnlyList<DepartureListItem> UpcomingDepartures, IReadOnlyList<BookingListItem> UpcomingTravel, IReadOnlyList<VisaRow> PendingVisas,
    IReadOnlyList<ManifestRow> PassportWarnings);
