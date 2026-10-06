using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Logistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "log_drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Cnic = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    Phone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    LicenseNo = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    LicenseCategory = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    LicenseExpiry = table.Column<DateTime>(type: "date", nullable: true),
                    DailyAllowance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_drivers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "log_maintenance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    VehicleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Odometer = table.Column<int>(type: "int", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VendorId = table.Column<Guid>(type: "char(36)", nullable: true),
                    BillId = table.Column<Guid>(type: "char(36)", nullable: true),
                    NextServiceDate = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_maintenance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_maintenance_fin_contacts_VendorId",
                        column: x => x.VendorId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "log_routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Origin = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Destination = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    DistanceKm = table.Column<int>(type: "int", nullable: false),
                    StandardHours = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false),
                    RatePerKg = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FullTruckRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FuelSurchargePercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_routes", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "log_vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RegistrationNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    MakeModel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CapacityKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapacityCbm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    IsHired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OwnerVendorId = table.Column<Guid>(type: "char(36)", nullable: true),
                    FitnessExpiry = table.Column<DateTime>(type: "date", nullable: true),
                    InsuranceExpiry = table.Column<DateTime>(type: "date", nullable: true),
                    RoutePermitExpiry = table.Column<DateTime>(type: "date", nullable: true),
                    TokenTaxExpiry = table.Column<DateTime>(type: "date", nullable: true),
                    Odometer = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_vehicles_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_log_vehicles_fin_contacts_OwnerVendorId",
                        column: x => x.OwnerVendorId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "log_shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    BookingDate = table.Column<DateTime>(type: "date", nullable: false),
                    CustomerId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ShipperName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    ShipperPhone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ShipperAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ConsigneeName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    ConsigneePhone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ConsigneeAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    OriginCity = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    DestinationCity = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    RouteId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Service = table.Column<int>(type: "int", nullable: false),
                    GoodsDescription = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Pieces = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VolumeCbm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    DeclaredValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMode = table.Column<int>(type: "int", nullable: false),
                    Freight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FuelSurcharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodCollected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodRemitted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PromisedDate = table.Column<DateTime>(type: "date", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReceivedBy = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    DeliveryRemarks = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CurrentTripId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_shipments_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_log_shipments_fin_contacts_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_log_shipments_log_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "log_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "log_trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    VehicleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DriverId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RouteId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Origin = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Destination = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "date", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ArrivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OdometerStart = table.Column<int>(type: "int", nullable: true),
                    OdometerEnd = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_trips_log_drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "log_drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_log_trips_log_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "log_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_log_trips_log_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "log_vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "log_shipment_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    At = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ByUserId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_shipment_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_shipment_events_log_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "log_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "log_trip_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TripId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidFromAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    VendorId = table.Column<Guid>(type: "char(36)", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    BillId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_trip_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_trip_expenses_log_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "log_trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_log_drivers_TenantId_LicenseNo",
                table: "log_drivers",
                columns: new[] { "TenantId", "LicenseNo" });

            migrationBuilder.CreateIndex(
                name: "IX_log_maintenance_VehicleId",
                table: "log_maintenance",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_log_maintenance_VendorId",
                table: "log_maintenance",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_log_routes_TenantId_Code",
                table: "log_routes",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_log_shipment_events_ShipmentId",
                table: "log_shipment_events",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_log_shipments_CurrentTripId",
                table: "log_shipments",
                column: "CurrentTripId");

            migrationBuilder.CreateIndex(
                name: "IX_log_shipments_CustomerId_InvoiceId",
                table: "log_shipments",
                columns: new[] { "CustomerId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_log_shipments_EntityId",
                table: "log_shipments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_log_shipments_RouteId",
                table: "log_shipments",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_log_shipments_TenantId_Number",
                table: "log_shipments",
                columns: new[] { "TenantId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_log_shipments_TenantId_Status_BookingDate",
                table: "log_shipments",
                columns: new[] { "TenantId", "Status", "BookingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_log_trip_expenses_TripId",
                table: "log_trip_expenses",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_log_trips_DriverId",
                table: "log_trips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_log_trips_RouteId",
                table: "log_trips",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_log_trips_TenantId_Status",
                table: "log_trips",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_log_trips_VehicleId",
                table: "log_trips",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_log_vehicles_EntityId",
                table: "log_vehicles",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_log_vehicles_OwnerVendorId",
                table: "log_vehicles",
                column: "OwnerVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_log_vehicles_TenantId_RegistrationNo",
                table: "log_vehicles",
                columns: new[] { "TenantId", "RegistrationNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_maintenance");

            migrationBuilder.DropTable(
                name: "log_shipment_events");

            migrationBuilder.DropTable(
                name: "log_trip_expenses");

            migrationBuilder.DropTable(
                name: "log_shipments");

            migrationBuilder.DropTable(
                name: "log_trips");

            migrationBuilder.DropTable(
                name: "log_drivers");

            migrationBuilder.DropTable(
                name: "log_routes");

            migrationBuilder.DropTable(
                name: "log_vehicles");
        }
    }
}
