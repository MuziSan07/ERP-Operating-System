using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hotel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerAdvanceAccountId",
                table: "fin_settings",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "htl_folio_charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReservationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncomeAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReservationRoomId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: true),
                    StockTransactionId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsVoid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_htl_folio_charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_htl_folio_charges_fin_tax_rates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "fin_tax_rates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "htl_folio_deposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReservationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_htl_folio_deposits", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "htl_guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Cnic = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    PassportNo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    Nationality = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsVip = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    ContactId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_htl_guests", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "htl_room_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    BaseRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxAdults = table.Column<int>(type: "int", nullable: false),
                    MaxChildren = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_htl_room_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_htl_room_types_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "htl_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    GuestId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BillToContactId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ArrivalDate = table.Column<DateTime>(type: "date", nullable: false),
                    DepartureDate = table.Column<DateTime>(type: "date", nullable: false),
                    Adults = table.Column<int>(type: "int", nullable: false),
                    Children = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExternalReference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_htl_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_htl_reservations_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_htl_reservations_fin_contacts_BillToContactId",
                        column: x => x.BillToContactId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_htl_reservations_htl_guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "htl_guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "htl_rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Floor = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Housekeeping = table.Column<int>(type: "int", nullable: false),
                    HousekeepingNote = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_htl_rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_htl_rooms_htl_room_types_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "htl_room_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "htl_reservation_rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReservationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RoomId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_htl_reservation_rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_htl_reservation_rooms_htl_reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "htl_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_htl_reservation_rooms_htl_room_types_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "htl_room_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_htl_reservation_rooms_htl_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "htl_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_htl_folio_charges_ReservationId_Date",
                table: "htl_folio_charges",
                columns: new[] { "ReservationId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_htl_folio_charges_TaxRateId",
                table: "htl_folio_charges",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_htl_folio_deposits_ReservationId",
                table: "htl_folio_deposits",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_htl_guests_TenantId_FullName",
                table: "htl_guests",
                columns: new[] { "TenantId", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_htl_guests_TenantId_Phone",
                table: "htl_guests",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_htl_reservation_rooms_ReservationId",
                table: "htl_reservation_rooms",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_htl_reservation_rooms_RoomId",
                table: "htl_reservation_rooms",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_htl_reservation_rooms_RoomTypeId",
                table: "htl_reservation_rooms",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_htl_reservations_BillToContactId",
                table: "htl_reservations",
                column: "BillToContactId");

            migrationBuilder.CreateIndex(
                name: "IX_htl_reservations_EntityId_DepartureDate",
                table: "htl_reservations",
                columns: new[] { "EntityId", "DepartureDate" });

            migrationBuilder.CreateIndex(
                name: "IX_htl_reservations_EntityId_Status_ArrivalDate",
                table: "htl_reservations",
                columns: new[] { "EntityId", "Status", "ArrivalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_htl_reservations_GuestId",
                table: "htl_reservations",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_htl_room_types_EntityId_Code",
                table: "htl_room_types",
                columns: new[] { "EntityId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_htl_rooms_EntityId_Number",
                table: "htl_rooms",
                columns: new[] { "EntityId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_htl_rooms_RoomTypeId",
                table: "htl_rooms",
                column: "RoomTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "htl_folio_charges");

            migrationBuilder.DropTable(
                name: "htl_folio_deposits");

            migrationBuilder.DropTable(
                name: "htl_reservation_rooms");

            migrationBuilder.DropTable(
                name: "htl_reservations");

            migrationBuilder.DropTable(
                name: "htl_rooms");

            migrationBuilder.DropTable(
                name: "htl_guests");

            migrationBuilder.DropTable(
                name: "htl_room_types");

            migrationBuilder.DropColumn(
                name: "CustomerAdvanceAccountId",
                table: "fin_settings");
        }
    }
}
