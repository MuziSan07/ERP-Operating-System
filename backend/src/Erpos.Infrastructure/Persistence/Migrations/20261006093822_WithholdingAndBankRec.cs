using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WithholdingAndBankRec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WhtDepositId",
                table: "fin_payments",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTax",
                table: "fin_payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxBase",
                table: "fin_payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxRateApplied",
                table: "fin_payments",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WithholdingTaxRateId",
                table: "fin_payments",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReconciliationId",
                table: "fin_journal_lines",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWhtRateId",
                table: "fin_contacts",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotOnActiveTaxpayerList",
                table: "fin_contacts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "fin_bank_reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    StatementDate = table.Column<DateTime>(type: "date", nullable: false),
                    StatementBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_bank_reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_bank_reconciliations_fin_accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "fin_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_wht_deposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CprNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    BankAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_wht_deposits", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_wht_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Section = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    PayableAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_wht_rates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_bank_statement_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JournalLineId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_bank_statement_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_bank_statement_lines_fin_bank_reconciliations_Reconcilia~",
                        column: x => x.ReconciliationId,
                        principalTable: "fin_bank_reconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fin_bank_reconciliations_BankAccountId",
                table: "fin_bank_reconciliations",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_bank_reconciliations_TenantId_BankAccountId_Status",
                table: "fin_bank_reconciliations",
                columns: new[] { "TenantId", "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_bank_statement_lines_JournalLineId",
                table: "fin_bank_statement_lines",
                column: "JournalLineId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_bank_statement_lines_ReconciliationId",
                table: "fin_bank_statement_lines",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_wht_deposits_TenantId_EntityId_Year_Month",
                table: "fin_wht_deposits",
                columns: new[] { "TenantId", "EntityId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_wht_rates_TenantId_Code",
                table: "fin_wht_rates",
                columns: new[] { "TenantId", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fin_bank_statement_lines");

            migrationBuilder.DropTable(
                name: "fin_wht_deposits");

            migrationBuilder.DropTable(
                name: "fin_wht_rates");

            migrationBuilder.DropTable(
                name: "fin_bank_reconciliations");

            migrationBuilder.DropColumn(
                name: "WhtDepositId",
                table: "fin_payments");

            migrationBuilder.DropColumn(
                name: "WithholdingTax",
                table: "fin_payments");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxBase",
                table: "fin_payments");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxRateApplied",
                table: "fin_payments");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxRateId",
                table: "fin_payments");

            migrationBuilder.DropColumn(
                name: "ReconciliationId",
                table: "fin_journal_lines");

            migrationBuilder.DropColumn(
                name: "DefaultWhtRateId",
                table: "fin_contacts");

            migrationBuilder.DropColumn(
                name: "NotOnActiveTaxpayerList",
                table: "fin_contacts");
        }
    }
}
