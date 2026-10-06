namespace Erpos.Domain.Enums;

public enum VehicleType { Pickup = 1, MiniTruck = 2, Truck = 3, Trailer = 4, Container20 = 5, Container40 = 6, Van = 7, Reefer = 8 }

public enum VehicleStatus { Available = 1, OnTrip = 2, Maintenance = 3, Inactive = 4 }

public enum ServiceLevel { PartLoad = 1, FullTruck = 2, Express = 3 }

/// <summary>Prepaid: paid at booking. ToPay: freight collected from the consignee. Account: billed to the customer monthly.</summary>
public enum PaymentMode { Prepaid = 1, ToPay = 2, Account = 3 }

public enum ShipmentStatus { Booked = 1, PickedUp = 2, InTransit = 3, AtHub = 4, OutForDelivery = 5, Delivered = 6, Returned = 7, Cancelled = 8 }

public enum TripStatus { Planned = 1, Dispatched = 2, Completed = 3, Cancelled = 4 }

public enum TripExpenseType { Fuel = 1, Tolls = 2, DriverAllowance = 3, Loading = 4, VehicleHire = 5, Repairs = 6, Other = 7 }
