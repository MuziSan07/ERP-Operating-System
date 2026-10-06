using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Ngo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ngo_donors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ContactId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_donors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_donors_fin_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_funds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    GrantId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_funds", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Sector = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    TargetBeneficiaries = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_programs_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_donations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    DonorId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FundId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BankAccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProgramId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_donations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_donations_ngo_donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "ngo_donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ngo_donations_ngo_funds_FundId",
                        column: x => x.FundId,
                        principalTable: "ngo_funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_beneficiaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RegistrationNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Cnic = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: true),
                    Phone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    District = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    HouseholdSize = table.Column<int>(type: "int", nullable: false),
                    Vulnerabilities = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ZakatEligible = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ProgramId = table.Column<Guid>(type: "char(36)", nullable: true),
                    EnrolledOn = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_beneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_beneficiaries_ngo_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "ngo_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_fund_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    FundId = table.Column<Guid>(type: "char(36)", nullable: false),
                    GrantId = table.Column<Guid>(type: "char(36)", nullable: true),
                    BudgetLineId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ProgramId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Function = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidFromAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    VendorId = table.Column<Guid>(type: "char(36)", nullable: true),
                    AllocationOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BillId = table.Column<Guid>(type: "char(36)", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ReleaseJournalId = table.Column<Guid>(type: "char(36)", nullable: true),
                    AssistanceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_fund_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_fund_expenses_ngo_funds_FundId",
                        column: x => x.FundId,
                        principalTable: "ngo_funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ngo_fund_expenses_ngo_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "ngo_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    DonorId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AgreementRef = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ProgramId = table.Column<Guid>(type: "char(36)", nullable: true),
                    FundId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    AgreementRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportingFrequency = table.Column<int>(type: "int", nullable: false),
                    FlexibilityPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_grants_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ngo_grants_ngo_donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "ngo_donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ngo_grants_ngo_funds_FundId",
                        column: x => x.FundId,
                        principalTable: "ngo_funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ngo_grants_ngo_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "ngo_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_assistance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProgramId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FundId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ExpenseId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_assistance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_assistance_ngo_beneficiaries_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalTable: "ngo_beneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_grant_budget_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GrantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_grant_budget_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_grant_budget_lines_ngo_grants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "ngo_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_grant_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GrantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "date", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    SubmittedOn = table.Column<DateTime>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_grant_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_grant_reports_ngo_grants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "ngo_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ngo_grant_tranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GrantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Condition = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReceivedBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BankAccountId = table.Column<Guid>(type: "char(36)", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ngo_grant_tranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ngo_grant_tranches_ngo_grants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "ngo_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_assistance_BeneficiaryId_Date",
                table: "ngo_assistance",
                columns: new[] { "BeneficiaryId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_beneficiaries_ProgramId",
                table: "ngo_beneficiaries",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_beneficiaries_TenantId_Cnic",
                table: "ngo_beneficiaries",
                columns: new[] { "TenantId", "Cnic" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_beneficiaries_TenantId_RegistrationNo",
                table: "ngo_beneficiaries",
                columns: new[] { "TenantId", "RegistrationNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_donations_DonorId",
                table: "ngo_donations",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_donations_FundId",
                table: "ngo_donations",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_donations_TenantId_Date",
                table: "ngo_donations",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_donations_TenantId_Number",
                table: "ngo_donations",
                columns: new[] { "TenantId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_donors_ContactId",
                table: "ngo_donors",
                column: "ContactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ngo_fund_expenses_BudgetLineId",
                table: "ngo_fund_expenses",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_fund_expenses_FundId",
                table: "ngo_fund_expenses",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_fund_expenses_GrantId",
                table: "ngo_fund_expenses",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_fund_expenses_ProgramId",
                table: "ngo_fund_expenses",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_fund_expenses_TenantId_Date",
                table: "ngo_fund_expenses",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_funds_GrantId",
                table: "ngo_funds",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_funds_TenantId_Code",
                table: "ngo_funds",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grant_budget_lines_GrantId",
                table: "ngo_grant_budget_lines",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grant_reports_GrantId",
                table: "ngo_grant_reports",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grant_tranches_GrantId",
                table: "ngo_grant_tranches",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grants_DonorId",
                table: "ngo_grants",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grants_EntityId",
                table: "ngo_grants",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grants_FundId",
                table: "ngo_grants",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grants_ProgramId",
                table: "ngo_grants",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grants_TenantId_Number",
                table: "ngo_grants",
                columns: new[] { "TenantId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_grants_TenantId_Status",
                table: "ngo_grants",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ngo_programs_EntityId",
                table: "ngo_programs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ngo_programs_TenantId_Code",
                table: "ngo_programs",
                columns: new[] { "TenantId", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ngo_assistance");

            migrationBuilder.DropTable(
                name: "ngo_donations");

            migrationBuilder.DropTable(
                name: "ngo_fund_expenses");

            migrationBuilder.DropTable(
                name: "ngo_grant_budget_lines");

            migrationBuilder.DropTable(
                name: "ngo_grant_reports");

            migrationBuilder.DropTable(
                name: "ngo_grant_tranches");

            migrationBuilder.DropTable(
                name: "ngo_beneficiaries");

            migrationBuilder.DropTable(
                name: "ngo_grants");

            migrationBuilder.DropTable(
                name: "ngo_donors");

            migrationBuilder.DropTable(
                name: "ngo_funds");

            migrationBuilder.DropTable(
                name: "ngo_programs");
        }
    }
}
