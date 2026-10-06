using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultConsumptionAccountId",
                table: "fin_settings",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultInventoryAccountId",
                table: "fin_settings",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrniAccountId",
                table: "fin_settings",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryAdjustmentAccountId",
                table: "fin_settings",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceTolerance",
                table: "fin_settings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceVarianceAccountId",
                table: "fin_settings",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                table: "fin_documents",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MatchedBaseValue",
                table: "fin_document_lines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderLineId",
                table: "fin_document_lines",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "inv_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BatchNo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "date", nullable: true),
                    ManufactureDate = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_batches", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_categories", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_warehouses_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "proc_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    RequiredBy = table.Column<DateTime>(type: "date", nullable: true),
                    Purpose = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DecisionComment = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proc_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_proc_requests_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_batch_levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BatchId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_batch_levels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_batch_levels_inv_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "inv_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Barcode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    TrackBatches = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TrackExpiry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StandardCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    InventoryAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsumptionAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    PurchaseTaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_items_inv_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "inv_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ToWarehouseId = table.Column<Guid>(type: "char(36)", nullable: true),
                    AccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ChargeEntityId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_transactions_inv_warehouses_ToWarehouseId",
                        column: x => x.ToWarehouseId,
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_transactions_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "proc_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    VendorId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    ExpectedDate = table.Column<DateTime>(type: "date", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Terms = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proc_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_proc_orders_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_orders_fin_contacts_VendorId",
                        column: x => x.VendorId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_orders_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BatchId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageCostAfter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    SourceId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_movements_inv_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "inv_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_movements_inv_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "inv_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_movements_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_stock_levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_stock_levels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_stock_levels_inv_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "inv_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_stock_levels_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "proc_request_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EstimatedUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proc_request_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_proc_request_lines_inv_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "inv_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_request_lines_proc_requests_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalTable: "proc_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inv_transaction_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    StockTransactionId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BatchId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_transaction_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_transaction_lines_inv_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "inv_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_transaction_lines_inv_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "inv_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_transaction_lines_inv_transactions_StockTransactionId",
                        column: x => x.StockTransactionId,
                        principalTable: "inv_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "proc_goods_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    DeliveryNote = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proc_goods_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_proc_goods_receipts_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_goods_receipts_proc_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "proc_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "proc_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityBilled = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedBaseValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BilledBaseValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseRequestLineId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proc_order_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_proc_order_lines_fin_tax_rates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "fin_tax_rates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_order_lines_inv_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "inv_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_order_lines_proc_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "proc_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "proc_goods_receipt_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BatchId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proc_goods_receipt_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_proc_goods_receipt_lines_inv_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "inv_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_goods_receipt_lines_inv_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "inv_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proc_goods_receipt_lines_proc_goods_receipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "proc_goods_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_inv_batch_levels_BatchId",
                table: "inv_batch_levels",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_batch_levels_ItemId_WarehouseId_BatchId",
                table: "inv_batch_levels",
                columns: new[] { "ItemId", "WarehouseId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_batches_ItemId_BatchNo",
                table: "inv_batches",
                columns: new[] { "ItemId", "BatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_categories_TenantId_Code",
                table: "inv_categories",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_items_CategoryId",
                table: "inv_items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_items_TenantId_Barcode",
                table: "inv_items",
                columns: new[] { "TenantId", "Barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_items_TenantId_Code",
                table: "inv_items",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_movements_BatchId",
                table: "inv_movements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_movements_ItemId",
                table: "inv_movements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_movements_SourceId",
                table: "inv_movements",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_movements_TenantId_ItemId_Date",
                table: "inv_movements",
                columns: new[] { "TenantId", "ItemId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_movements_WarehouseId",
                table: "inv_movements",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_stock_levels_ItemId_WarehouseId",
                table: "inv_stock_levels",
                columns: new[] { "ItemId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_stock_levels_WarehouseId",
                table: "inv_stock_levels",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transaction_lines_BatchId",
                table: "inv_transaction_lines",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transaction_lines_ItemId",
                table: "inv_transaction_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transaction_lines_StockTransactionId",
                table: "inv_transaction_lines",
                column: "StockTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transactions_TenantId_Date",
                table: "inv_transactions",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_transactions_ToWarehouseId",
                table: "inv_transactions",
                column: "ToWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transactions_WarehouseId",
                table: "inv_transactions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_warehouses_EntityId",
                table: "inv_warehouses",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_warehouses_TenantId_Code",
                table: "inv_warehouses",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_proc_goods_receipt_lines_BatchId",
                table: "proc_goods_receipt_lines",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_goods_receipt_lines_GoodsReceiptId",
                table: "proc_goods_receipt_lines",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_goods_receipt_lines_ItemId",
                table: "proc_goods_receipt_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_goods_receipts_PurchaseOrderId",
                table: "proc_goods_receipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_goods_receipts_WarehouseId",
                table: "proc_goods_receipts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_order_lines_ItemId",
                table: "proc_order_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_order_lines_PurchaseOrderId",
                table: "proc_order_lines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_order_lines_TaxRateId",
                table: "proc_order_lines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_orders_EntityId",
                table: "proc_orders",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_orders_TenantId_Status",
                table: "proc_orders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_proc_orders_VendorId",
                table: "proc_orders",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_orders_WarehouseId",
                table: "proc_orders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_request_lines_ItemId",
                table: "proc_request_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_request_lines_PurchaseRequestId",
                table: "proc_request_lines",
                column: "PurchaseRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_requests_EntityId",
                table: "proc_requests",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_proc_requests_TenantId_Status",
                table: "proc_requests",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inv_batch_levels");

            migrationBuilder.DropTable(
                name: "inv_movements");

            migrationBuilder.DropTable(
                name: "inv_stock_levels");

            migrationBuilder.DropTable(
                name: "inv_transaction_lines");

            migrationBuilder.DropTable(
                name: "proc_goods_receipt_lines");

            migrationBuilder.DropTable(
                name: "proc_order_lines");

            migrationBuilder.DropTable(
                name: "proc_request_lines");

            migrationBuilder.DropTable(
                name: "inv_transactions");

            migrationBuilder.DropTable(
                name: "inv_batches");

            migrationBuilder.DropTable(
                name: "proc_goods_receipts");

            migrationBuilder.DropTable(
                name: "inv_items");

            migrationBuilder.DropTable(
                name: "proc_requests");

            migrationBuilder.DropTable(
                name: "proc_orders");

            migrationBuilder.DropTable(
                name: "inv_categories");

            migrationBuilder.DropTable(
                name: "inv_warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultConsumptionAccountId",
                table: "fin_settings");

            migrationBuilder.DropColumn(
                name: "DefaultInventoryAccountId",
                table: "fin_settings");

            migrationBuilder.DropColumn(
                name: "GrniAccountId",
                table: "fin_settings");

            migrationBuilder.DropColumn(
                name: "InventoryAdjustmentAccountId",
                table: "fin_settings");

            migrationBuilder.DropColumn(
                name: "PriceTolerance",
                table: "fin_settings");

            migrationBuilder.DropColumn(
                name: "PriceVarianceAccountId",
                table: "fin_settings");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "fin_documents");

            migrationBuilder.DropColumn(
                name: "MatchedBaseValue",
                table: "fin_document_lines");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderLineId",
                table: "fin_document_lines");
        }
    }
}
