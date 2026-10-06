using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TravelTours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tour_guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Languages = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    LicenseNo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    DailyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_tour_guides", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tour_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Destination = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Inclusions = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Exclusions = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    AdultPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChildPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SingleSupplement = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IncomeAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tour_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tour_packages_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "trv_bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ContactPhone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    TravelDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trv_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trv_bookings_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trv_bookings_fin_contacts_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "trv_deposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TravelBookingId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Applied = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trv_deposits", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tour_departures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TourPackageId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    AdultPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChildPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_tour_departures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tour_departures_tour_packages_TourPackageId",
                        column: x => x.TourPackageId,
                        principalTable: "tour_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tour_itinerary_days",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TourPackageId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Overnight = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Meals = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tour_itinerary_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tour_itinerary_days_tour_packages_TourPackageId",
                        column: x => x.TourPackageId,
                        principalTable: "tour_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "trv_passengers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TravelBookingId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    PassportNo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    PassportExpiry = table.Column<DateTime>(type: "date", nullable: true),
                    Nationality = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Cnic = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: true),
                    Phone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trv_passengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trv_passengers_trv_bookings_TravelBookingId",
                        column: x => x.TravelBookingId,
                        principalTable: "trv_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tour_departure_costs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TourDepartureId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    VendorId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BillId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tour_departure_costs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tour_departure_costs_fin_contacts_VendorId",
                        column: x => x.VendorId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tour_departure_costs_tour_departures_TourDepartureId",
                        column: x => x.TourDepartureId,
                        principalTable: "tour_departures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tour_departure_guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TourDepartureId = table.Column<Guid>(type: "char(36)", nullable: false),
                    GuideId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tour_departure_guides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tour_departure_guides_tour_departures_TourDepartureId",
                        column: x => x.TourDepartureId,
                        principalTable: "tour_departures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tour_departure_guides_tour_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "tour_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "trv_booking_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TravelBookingId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "date", nullable: true),
                    SupplierId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IncomeAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TourDepartureId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Adults = table.Column<int>(type: "int", nullable: false),
                    Children = table.Column<int>(type: "int", nullable: false),
                    Airline = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Pnr = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    TicketNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    Route = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Country = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    VisaStatus = table.Column<int>(type: "int", nullable: true),
                    PassengerId = table.Column<Guid>(type: "char(36)", nullable: true),
                    BillId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trv_booking_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trv_booking_items_fin_contacts_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trv_booking_items_tour_departures_TourDepartureId",
                        column: x => x.TourDepartureId,
                        principalTable: "tour_departures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trv_booking_items_trv_bookings_TravelBookingId",
                        column: x => x.TravelBookingId,
                        principalTable: "trv_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_tour_departure_costs_TourDepartureId",
                table: "tour_departure_costs",
                column: "TourDepartureId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_departure_costs_VendorId",
                table: "tour_departure_costs",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_departure_guides_GuideId",
                table: "tour_departure_guides",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_departure_guides_TourDepartureId",
                table: "tour_departure_guides",
                column: "TourDepartureId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_departures_TenantId_Code",
                table: "tour_departures",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_tour_departures_TenantId_StartDate",
                table: "tour_departures",
                columns: new[] { "TenantId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_tour_departures_TourPackageId",
                table: "tour_departures",
                column: "TourPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_itinerary_days_TourPackageId",
                table: "tour_itinerary_days",
                column: "TourPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_packages_EntityId",
                table: "tour_packages",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_packages_TenantId_Code",
                table: "tour_packages",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_trv_booking_items_SupplierId",
                table: "trv_booking_items",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_trv_booking_items_TourDepartureId",
                table: "trv_booking_items",
                column: "TourDepartureId");

            migrationBuilder.CreateIndex(
                name: "IX_trv_booking_items_TravelBookingId",
                table: "trv_booking_items",
                column: "TravelBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_trv_bookings_CustomerId",
                table: "trv_bookings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_trv_bookings_EntityId",
                table: "trv_bookings",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_trv_bookings_TenantId_Status",
                table: "trv_bookings",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_trv_bookings_TenantId_TravelDate",
                table: "trv_bookings",
                columns: new[] { "TenantId", "TravelDate" });

            migrationBuilder.CreateIndex(
                name: "IX_trv_deposits_TravelBookingId",
                table: "trv_deposits",
                column: "TravelBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_trv_passengers_TravelBookingId",
                table: "trv_passengers",
                column: "TravelBookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tour_departure_costs");

            migrationBuilder.DropTable(
                name: "tour_departure_guides");

            migrationBuilder.DropTable(
                name: "tour_itinerary_days");

            migrationBuilder.DropTable(
                name: "trv_booking_items");

            migrationBuilder.DropTable(
                name: "trv_deposits");

            migrationBuilder.DropTable(
                name: "trv_passengers");

            migrationBuilder.DropTable(
                name: "tour_guides");

            migrationBuilder.DropTable(
                name: "tour_departures");

            migrationBuilder.DropTable(
                name: "trv_bookings");

            migrationBuilder.DropTable(
                name: "tour_packages");
        }
    }
}
