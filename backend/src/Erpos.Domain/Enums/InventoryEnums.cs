namespace Erpos.Domain.Enums;

public enum ItemType { Stock = 1, Service = 2 }

public enum StockMovementType { Opening = 1, Receipt = 2, Issue = 3, TransferOut = 4, TransferIn = 5, AdjustmentIn = 6, AdjustmentOut = 7 }

public enum StockTransactionType { Issue = 1, Transfer = 2, Adjustment = 3, Opening = 4 }

public enum PurchaseRequestStatus { Draft = 1, Submitted = 2, Approved = 3, Rejected = 4, Ordered = 5, Cancelled = 6 }

public enum PurchaseOrderStatus { Draft = 1, Approved = 2, PartiallyReceived = 3, Received = 4, Closed = 5, Cancelled = 6 }
