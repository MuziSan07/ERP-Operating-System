using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Finance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "pay_runs",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentJournalEntryId",
                table: "pay_runs",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fin_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SubType = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsGroup = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_accounts_fin_accounts_ParentId",
                        column: x => x.ParentId,
                        principalTable: "fin_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    IsCustomer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsVendor = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Ntn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Strn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Cnic = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_contacts", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_exchange_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_exchange_rates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_journal_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ReversalOfId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ReversedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    PostedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_journal_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_journal_entries_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_number_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Key = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Next = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_number_sequences", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BaseCurrency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "int", nullable: false),
                    LockedThrough = table.Column<DateTime>(type: "date", nullable: true),
                    Ntn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Strn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ReceivableAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    PayableAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    RetainedEarningsAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ExchangeGainLossAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SalaryExpenseAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SalaryPayableAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SalaryTaxPayableAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    EobiExpenseAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    EobiPayableAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    PfExpenseAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    PfPayableAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SocialSecurityExpenseAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SocialSecurityPayableAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    OtherPayrollDeductionsAccountId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_settings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_tax_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Authority = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    OutputAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    InputAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_tax_rates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ContactId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BasePaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_documents_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fin_documents_fin_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ContactId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    IsVoid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_payments_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fin_payments_fin_accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "fin_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fin_payments_fin_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_journal_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContactId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_journal_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_journal_lines_fin_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "fin_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fin_journal_lines_fin_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "fin_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_document_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    AccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_document_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_document_lines_fin_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "fin_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fin_document_lines_fin_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "fin_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fin_document_lines_fin_tax_rates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "fin_tax_rates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fin_payment_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PaymentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_payment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fin_payment_allocations_fin_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "fin_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fin_payment_allocations_fin_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "fin_payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fin_accounts_ParentId",
                table: "fin_accounts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_accounts_TenantId_Code",
                table: "fin_accounts",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_contacts_TenantId_Code",
                table: "fin_contacts",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_contacts_TenantId_Name",
                table: "fin_contacts",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_document_lines_AccountId",
                table: "fin_document_lines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_document_lines_DocumentId",
                table: "fin_document_lines",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_document_lines_TaxRateId",
                table: "fin_document_lines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_documents_ContactId",
                table: "fin_documents",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_documents_EntityId",
                table: "fin_documents",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_documents_TenantId_ContactId",
                table: "fin_documents",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_documents_TenantId_Kind_Status",
                table: "fin_documents",
                columns: new[] { "TenantId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_exchange_rates_TenantId_Currency_Date",
                table: "fin_exchange_rates",
                columns: new[] { "TenantId", "Currency", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_journal_entries_EntityId",
                table: "fin_journal_entries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_journal_entries_Source_SourceId",
                table: "fin_journal_entries",
                columns: new[] { "Source", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_journal_entries_TenantId_Date",
                table: "fin_journal_entries",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_journal_entries_TenantId_Number",
                table: "fin_journal_entries",
                columns: new[] { "TenantId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_journal_lines_AccountId_EntityId",
                table: "fin_journal_lines",
                columns: new[] { "AccountId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_journal_lines_ContactId",
                table: "fin_journal_lines",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_journal_lines_JournalEntryId",
                table: "fin_journal_lines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_number_sequences_TenantId_Key",
                table: "fin_number_sequences",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fin_payment_allocations_DocumentId",
                table: "fin_payment_allocations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_payment_allocations_PaymentId",
                table: "fin_payment_allocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_payments_BankAccountId",
                table: "fin_payments",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_payments_ContactId",
                table: "fin_payments",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_payments_EntityId",
                table: "fin_payments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_fin_payments_TenantId_Date",
                table: "fin_payments",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_fin_settings_TenantId",
                table: "fin_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fin_tax_rates_TenantId_Code",
                table: "fin_tax_rates",
                columns: new[] { "TenantId", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fin_document_lines");

            migrationBuilder.DropTable(
                name: "fin_exchange_rates");

            migrationBuilder.DropTable(
                name: "fin_journal_lines");

            migrationBuilder.DropTable(
                name: "fin_number_sequences");

            migrationBuilder.DropTable(
                name: "fin_payment_allocations");

            migrationBuilder.DropTable(
                name: "fin_settings");

            migrationBuilder.DropTable(
                name: "fin_tax_rates");

            migrationBuilder.DropTable(
                name: "fin_journal_entries");

            migrationBuilder.DropTable(
                name: "fin_documents");

            migrationBuilder.DropTable(
                name: "fin_payments");

            migrationBuilder.DropTable(
                name: "fin_accounts");

            migrationBuilder.DropTable(
                name: "fin_contacts");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "pay_runs");

            migrationBuilder.DropColumn(
                name: "PaymentJournalEntryId",
                table: "pay_runs");
        }
    }
}
