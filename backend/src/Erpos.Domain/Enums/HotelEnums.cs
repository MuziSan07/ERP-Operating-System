namespace Erpos.Domain.Enums;

public enum HousekeepingStatus { Clean = 1, Dirty = 2, Inspected = 3, OutOfOrder = 4 }

public enum BookingSource { WalkIn = 1, Phone = 2, Email = 3, Website = 4, Ota = 5, Corporate = 6, TravelAgent = 7 }

public enum ReservationStatus { Tentative = 1, Confirmed = 2, CheckedIn = 3, CheckedOut = 4, Cancelled = 5, NoShow = 6 }

public enum FolioChargeType { Room = 1, Restaurant = 2, Laundry = 3, Minibar = 4, Transport = 5, Other = 6 }
