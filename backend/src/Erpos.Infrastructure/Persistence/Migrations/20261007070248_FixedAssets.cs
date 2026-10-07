using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fin_asset_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "int", nullable: false),
                    ReducingRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    AssetAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AccumulatedAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_asset_categories", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_depreciation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_depreciation_runs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_fixed_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    CategoryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SerialNo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    CustodianEmployeeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    AcquisitionDate = table.Column<DateTime>(type: "date", nullable: false),
                    DepreciationStart = table.Column<DateTime>(type: "date", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SalvageValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "int", nullable: false),
                    ReducingRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepreciatedThrough = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "date", nullable: true),
                    DisposalProceeds = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AcquisitionJournalId = table.Column<Guid>(type: "char(36)", nullable: true),
                    AcquisitionBillId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DisposalJournalId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_fixed_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_fixed_assets_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fin_fixed_assets_fin_asset_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "fin_asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_depreciation_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    RunId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AssetId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Months = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_depreciation_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_depreciation_lines_fin_depreciation_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "fin_depreciation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fin_asset_categories_TenantId_Code",
                table: "fin_asset_categories",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_depreciation_lines_AssetId",
                table: "fin_depreciation_lines",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_depreciation_lines_RunId",
                table: "fin_depreciation_lines",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_depreciation_runs_TenantId_EntityId_Year_Month",
                table: "fin_depreciation_runs",
                columns: new[] { "TenantId", "EntityId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_fixed_assets_CategoryId",
                table: "fin_fixed_assets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_fixed_assets_EntityId_Status",
                table: "fin_fixed_assets",
                columns: new[] { "EntityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_fixed_assets_TenantId_Code",
                table: "fin_fixed_assets",
                columns: new[] { "TenantId", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fin_depreciation_lines");

            migrationBuilder.DropTable(
                name: "fin_fixed_assets");

            migrationBuilder.DropTable(
                name: "fin_depreciation_runs");

            migrationBuilder.DropTable(
                name: "fin_asset_categories");
        }
    }
}
