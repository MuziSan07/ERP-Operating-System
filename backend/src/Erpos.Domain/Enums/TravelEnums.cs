namespace Erpos.Domain.Enums;

public enum DepartureStatus { Open = 1, Closed = 2, Departed = 3, Completed = 4, Cancelled = 5 }

public enum TravelBookingStatus { Quotation = 1, Confirmed = 2, Invoiced = 3, Cancelled = 4 }

public enum PassengerType { Adult = 1, Child = 2, Infant = 3 }

public enum BookingItemType { TourSeats = 1, Flight = 2, Hotel = 3, Visa = 4, Transport = 5, Insurance = 6, Other = 7 }

public enum VisaStatus { NotStarted = 0, DocumentsCollected = 1, Submitted = 2, Approved = 3, Rejected = 4 }
