using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_designations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Grade = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_designations", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_holidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_holidays", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_leave_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    DaysPerYear = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false),
                    IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowHalfDay = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OnlyForGender = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_types", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_leave_workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ApproverType = table.Column<int>(type: "int", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_workflow", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WeeklyOffDays = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    MinimumWage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EobiEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EobiEmployeeRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    EobiEmployerRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ProvidentFundEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ProvidentFundEmployeeRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ProvidentFundEmployerRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    SocialSecurityEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SocialSecurityEmployerRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    SocialSecurityWageCeiling = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollRequiresSecondApprover = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_settings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    IsTaxable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsBasic = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExemptUpToFractionOfBasic = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_components", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    IncludeSubEntities = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Warnings = table.Column<string>(type: "text", nullable: true),
                    TotalGross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalNet = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEmployerContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_pay_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pay_runs_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_tax_years",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SurchargeThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SurchargeRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_tax_years", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsTaxable = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pay_adjustments_pay_runs_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "pay_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_tax_slabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TaxYearId = table.Column<Guid>(type: "char(36)", nullable: false),
                    From = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    To = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FixedTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_tax_slabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pay_tax_slabs_pay_tax_years_TaxYearId",
                        column: x => x.TaxYearId,
                        principalTable: "pay_tax_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    CheckOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    Remarks = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_attendance", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    HeadEmployeeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_departments_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DesignationId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ManagerId = table.Column<Guid>(type: "char(36)", nullable: true),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JoinDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ConfirmationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FatherName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Cnic = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    BankAccountTitle = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Iban = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: true),
                    Ntn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    EobiNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    EobiMember = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ProvidentFundMember = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SocialSecurityMember = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_employees_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employees_hr_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "hr_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hr_employees_hr_designations_DesignationId",
                        column: x => x.DesignationId,
                        principalTable: "hr_designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hr_employees_hr_employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "hr_employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employees_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_leave_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsHalfDay = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_leave_requests_hr_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hr_leave_requests_hr_leave_types_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "hr_leave_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_employee_salaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_employee_salaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pay_employee_salaries_hr_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_payslips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    EmployeeCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Designation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Department = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    EntityName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Cnic = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    Iban = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: true),
                    BankName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    DaysInMonth = table.Column<int>(type: "int", nullable: false),
                    UnpaidDays = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false),
                    PayableDays = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false),
                    MonthlyGross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossEarnings = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncomeTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployerContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProjectedAnnualTaxable = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProjectedAnnualTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_payslips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pay_payslips_hr_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pay_payslips_pay_runs_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "pay_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hr_leave_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    LeaveRequestId = table.Column<Guid>(type: "char(36)", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    StepName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ApproverType = table.Column<int>(type: "int", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ActedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_leave_approvals_hr_leave_requests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "hr_leave_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_employee_salary_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeSalaryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PayComponentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_employee_salary_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pay_employee_salary_lines_pay_components_PayComponentId",
                        column: x => x.PayComponentId,
                        principalTable: "pay_components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pay_employee_salary_lines_pay_employee_salaries_EmployeeSala~",
                        column: x => x.EmployeeSalaryId,
                        principalTable: "pay_employee_salaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pay_payslip_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PayslipId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsEmployerContribution = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_payslip_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pay_payslip_lines_pay_payslips_PayslipId",
                        column: x => x.PayslipId,
                        principalTable: "pay_payslips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_EmployeeId_Date",
                table: "hr_attendance",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_TenantId_Date",
                table: "hr_attendance",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_departments_EntityId_Code",
                table: "hr_departments",
                columns: new[] { "EntityId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_departments_HeadEmployeeId",
                table: "hr_departments",
                column: "HeadEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_designations_TenantId_Title",
                table: "hr_designations",
                columns: new[] { "TenantId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employees_DepartmentId",
                table: "hr_employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employees_DesignationId",
                table: "hr_employees",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employees_EntityId",
                table: "hr_employees",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employees_ManagerId",
                table: "hr_employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employees_TenantId_EmployeeCode",
                table: "hr_employees",
                columns: new[] { "TenantId", "EmployeeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employees_TenantId_EntityId",
                table: "hr_employees",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employees_UserId",
                table: "hr_employees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_holidays_TenantId_Date",
                table: "hr_holidays",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_approvals_ApproverUserId",
                table: "hr_leave_approvals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_approvals_LeaveRequestId",
                table: "hr_leave_approvals",
                column: "LeaveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_requests_EmployeeId_FromDate",
                table: "hr_leave_requests",
                columns: new[] { "EmployeeId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_requests_LeaveTypeId",
                table: "hr_leave_requests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_requests_TenantId_Status",
                table: "hr_leave_requests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_types_TenantId_Code",
                table: "hr_leave_types",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_workflow_TenantId_StepOrder",
                table: "hr_leave_workflow",
                columns: new[] { "TenantId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_settings_TenantId",
                table: "hr_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pay_adjustments_PayrollRunId",
                table: "pay_adjustments",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_pay_components_TenantId_Code",
                table: "pay_components",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_pay_employee_salaries_EmployeeId_EffectiveFrom",
                table: "pay_employee_salaries",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_pay_employee_salary_lines_EmployeeSalaryId",
                table: "pay_employee_salary_lines",
                column: "EmployeeSalaryId");

            migrationBuilder.CreateIndex(
                name: "IX_pay_employee_salary_lines_PayComponentId",
                table: "pay_employee_salary_lines",
                column: "PayComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_pay_payslip_lines_PayslipId",
                table: "pay_payslip_lines",
                column: "PayslipId");

            migrationBuilder.CreateIndex(
                name: "IX_pay_payslips_EmployeeId_Year_Month",
                table: "pay_payslips",
                columns: new[] { "EmployeeId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_pay_payslips_PayrollRunId",
                table: "pay_payslips",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_pay_runs_EntityId",
                table: "pay_runs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_pay_runs_TenantId_Year_Month",
                table: "pay_runs",
                columns: new[] { "TenantId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_pay_tax_slabs_TaxYearId",
                table: "pay_tax_slabs",
                column: "TaxYearId");

            migrationBuilder.CreateIndex(
                name: "IX_pay_tax_years_TenantId_Year",
                table: "pay_tax_years",
                columns: new[] { "TenantId", "Year" });

            migrationBuilder.AddForeignKey(
                name: "FK_hr_attendance_hr_employees_EmployeeId",
                table: "hr_attendance",
                column: "EmployeeId",
                principalTable: "hr_employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_hr_departments_hr_employees_HeadEmployeeId",
                table: "hr_departments",
                column: "HeadEmployeeId",
                principalTable: "hr_employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hr_departments_hr_employees_HeadEmployeeId",
                table: "hr_departments");

            migrationBuilder.DropTable(
                name: "hr_attendance");

            migrationBuilder.DropTable(
                name: "hr_holidays");

            migrationBuilder.DropTable(
                name: "hr_leave_approvals");

            migrationBuilder.DropTable(
                name: "hr_leave_workflow");

            migrationBuilder.DropTable(
                name: "hr_settings");

            migrationBuilder.DropTable(
                name: "pay_adjustments");

            migrationBuilder.DropTable(
                name: "pay_employee_salary_lines");

            migrationBuilder.DropTable(
                name: "pay_payslip_lines");

            migrationBuilder.DropTable(
                name: "pay_tax_slabs");

            migrationBuilder.DropTable(
                name: "hr_leave_requests");

            migrationBuilder.DropTable(
                name: "pay_components");

            migrationBuilder.DropTable(
                name: "pay_employee_salaries");

            migrationBuilder.DropTable(
                name: "pay_payslips");

            migrationBuilder.DropTable(
                name: "pay_tax_years");

            migrationBuilder.DropTable(
                name: "hr_leave_types");

            migrationBuilder.DropTable(
                name: "pay_runs");

            migrationBuilder.DropTable(
                name: "hr_employees");

            migrationBuilder.DropTable(
                name: "hr_departments");

            migrationBuilder.DropTable(
                name: "hr_designations");
        }
    }
}
