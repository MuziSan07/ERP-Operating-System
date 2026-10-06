using Erpos.Application.Dtos;
using Erpos.Application.Inventory;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/inventory")]
public class InventoryController(InventoryService inventory) : ControllerBase
{
    [HttpGet("categories")] public Task<List<ItemCategoryDto>> Categories(CancellationToken ct) => inventory.CategoriesAsync(ct);
    [HttpPost("categories")] public Task<ItemCategoryDto> CreateCategory(SaveItemCategoryRequest req, CancellationToken ct) => inventory.SaveCategoryAsync(null, req, ct);
    [HttpPut("categories/{id:guid}")] public Task<ItemCategoryDto> UpdateCategory(Guid id, SaveItemCategoryRequest req, CancellationToken ct) => inventory.SaveCategoryAsync(id, req, ct);

    [HttpGet("items")]
    public Task<List<ItemDto>> Items([FromQuery] string? search, [FromQuery] Guid? categoryId, [FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        inventory.ItemsAsync(search, categoryId, activeOnly, ct);
    [HttpPost("items")] public Task<ItemDto> CreateItem(SaveItemRequest req, CancellationToken ct) => inventory.SaveItemAsync(null, req, ct);
    [HttpPut("items/{id:guid}")] public Task<ItemDto> UpdateItem(Guid id, SaveItemRequest req, CancellationToken ct) => inventory.SaveItemAsync(id, req, ct);

    [HttpGet("warehouses")] public Task<List<WarehouseDto>> Warehouses(CancellationToken ct) => inventory.WarehousesAsync(ct);
    [HttpPost("warehouses")] public Task<WarehouseDto> CreateWarehouse(SaveWarehouseRequest req, CancellationToken ct) => inventory.SaveWarehouseAsync(null, req, ct);
    [HttpPut("warehouses/{id:guid}")] public Task<WarehouseDto> UpdateWarehouse(Guid id, SaveWarehouseRequest req, CancellationToken ct) => inventory.SaveWarehouseAsync(id, req, ct);

    [HttpGet("transactions")]
    public Task<PagedResult<StockTransactionDto>> Transactions([FromQuery] StockTransactionType? type, [FromQuery] Guid? warehouseId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) => inventory.TransactionsAsync(type, warehouseId, page, pageSize, ct);
    [HttpGet("transactions/{id:guid}")] public Task<StockTransactionDto> Transaction(Guid id, CancellationToken ct) => inventory.GetTransactionAsync(id, ct);
    [HttpPost("transactions")] public Task<StockTransactionDto> CreateTransaction(CreateStockTransactionRequest req, CancellationToken ct) => inventory.CreateTransactionAsync(req, ct);

    [HttpGet("stock")]
    public Task<List<StockRow>> Stock([FromQuery] Guid? warehouseId, [FromQuery] string? search, [FromQuery] bool belowReorder = false, CancellationToken ct = default) =>
        inventory.StockOnHandAsync(warehouseId, search, belowReorder, ct);
    [HttpGet("batches")]
    public Task<List<BatchStockRow>> Batches([FromQuery] Guid? warehouseId, [FromQuery] Guid? itemId, [FromQuery] int? expiringWithinDays, CancellationToken ct) =>
        inventory.BatchesAsync(warehouseId, itemId, expiringWithinDays, ct);
    [HttpGet("movements")]
    public Task<List<MovementRow>> Movements([FromQuery] Guid? itemId, [FromQuery] Guid? warehouseId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct) => inventory.MovementsAsync(itemId, warehouseId, from, to, ct);
    [HttpGet("valuation")] public Task<StockValuationDto> Valuation(CancellationToken ct) => inventory.ValuationAsync(ct);
}

[ApiController, Authorize, Route("api/procurement")]
public class ProcurementController(ProcurementService procurement) : ControllerBase
{
    [HttpGet("requests")]
    public Task<PagedResult<PurchaseRequestDto>> Requests([FromQuery] PurchaseRequestStatus? status, [FromQuery] bool mine = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) => procurement.RequestsAsync(status, mine, page, pageSize, ct);
    [HttpGet("requests/{id:guid}")] public Task<PurchaseRequestDto> Request(Guid id, CancellationToken ct) => procurement.RequestAsync(id, ct);
    [HttpPost("requests")] public Task<PurchaseRequestDto> CreateRequest(SavePurchaseRequestRequest req, CancellationToken ct) => procurement.SaveRequestAsync(null, req, ct);
    [HttpPut("requests/{id:guid}")] public Task<PurchaseRequestDto> UpdateRequest(Guid id, SavePurchaseRequestRequest req, CancellationToken ct) => procurement.SaveRequestAsync(id, req, ct);
    [HttpPost("requests/{id:guid}/submit")] public Task<PurchaseRequestDto> Submit(Guid id, CancellationToken ct) => procurement.SubmitRequestAsync(id, ct);
    [HttpPost("requests/{id:guid}/decision")] public Task<PurchaseRequestDto> Decide(Guid id, DecisionRequest req, CancellationToken ct) => procurement.DecideRequestAsync(id, req, ct);
    [HttpPost("requests/{id:guid}/cancel")] public Task<PurchaseRequestDto> CancelRequest(Guid id, CancellationToken ct) => procurement.CancelRequestAsync(id, ct);

    [HttpGet("orders")]
    public Task<PagedResult<PurchaseOrderListItem>> Orders([FromQuery] PurchaseOrderStatus? status, [FromQuery] Guid? vendorId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) => procurement.OrdersAsync(status, vendorId, search, page, pageSize, ct);
    [HttpGet("orders/{id:guid}")] public Task<PurchaseOrderDto> Order(Guid id, CancellationToken ct) => procurement.OrderAsync(id, ct);
    [HttpPost("orders")] public Task<PurchaseOrderDto> CreateOrder(SavePurchaseOrderRequest req, CancellationToken ct) => procurement.SaveOrderAsync(null, req, ct);
    [HttpPut("orders/{id:guid}")] public Task<PurchaseOrderDto> UpdateOrder(Guid id, SavePurchaseOrderRequest req, CancellationToken ct) => procurement.SaveOrderAsync(id, req, ct);
    [HttpPost("orders/{id:guid}/approve")] public Task<PurchaseOrderDto> Approve(Guid id, CancellationToken ct) => procurement.ApproveOrderAsync(id, ct);
    [HttpPost("orders/{id:guid}/close")] public Task<PurchaseOrderDto> Close(Guid id, CancellationToken ct) => procurement.CloseOrderAsync(id, ct);
    [HttpPost("orders/{id:guid}/bill")]
    public async Task<object> CreateBill(Guid id, CancellationToken ct) => new { billId = await procurement.CreateBillAsync(id, ct) };

    [HttpGet("receipts")] public Task<List<GoodsReceiptDto>> Receipts([FromQuery] Guid? purchaseOrderId, CancellationToken ct) => procurement.ReceiptsAsync(purchaseOrderId, ct);
    [HttpGet("receipts/{id:guid}")] public Task<GoodsReceiptDto> Receipt(Guid id, CancellationToken ct) => procurement.ReceiptAsync(id, ct);
    [HttpPost("receipts")] public Task<GoodsReceiptDto> Receive(CreateGoodsReceiptRequest req, CancellationToken ct) => procurement.ReceiveAsync(req, ct);

    [HttpGet("grni")] public Task<List<GrniRow>> Grni(CancellationToken ct) => procurement.GrniAsync(ct);
}
