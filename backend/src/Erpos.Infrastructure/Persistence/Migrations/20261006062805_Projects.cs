using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erpos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Projects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prj_projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: true),
                    BillingType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    BudgetHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ContractAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultBillRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ManagerEmployeeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    NextTaskNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prj_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prj_projects_entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prj_projects_fin_contacts_ClientId",
                        column: x => x.ClientId,
                        principalTable: "fin_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "prj_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Role = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BillRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CostRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prj_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prj_members_hr_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prj_members_prj_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "prj_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "prj_milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "date", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prj_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prj_milestones_prj_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "prj_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "prj_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssigneeEmployeeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    EstimateHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: true),
                    MilestoneId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prj_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prj_tasks_hr_employees_AssigneeEmployeeId",
                        column: x => x.AssigneeEmployeeId,
                        principalTable: "hr_employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prj_tasks_prj_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "prj_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "prj_time_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TaskId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Billable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BillRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RejectReason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prj_time_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prj_time_entries_hr_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prj_time_entries_prj_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "prj_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prj_time_entries_prj_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "prj_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_prj_members_EmployeeId",
                table: "prj_members",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_prj_members_ProjectId_EmployeeId",
                table: "prj_members",
                columns: new[] { "ProjectId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prj_milestones_ProjectId",
                table: "prj_milestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_prj_projects_ClientId",
                table: "prj_projects",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_prj_projects_EntityId",
                table: "prj_projects",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_prj_projects_TenantId_Code",
                table: "prj_projects",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_projects_TenantId_Status",
                table: "prj_projects",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_tasks_AssigneeEmployeeId",
                table: "prj_tasks",
                column: "AssigneeEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_prj_tasks_ProjectId_Status",
                table: "prj_tasks",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_time_entries_EmployeeId_Date",
                table: "prj_time_entries",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_time_entries_ProjectId_Status",
                table: "prj_time_entries",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_time_entries_TaskId",
                table: "prj_time_entries",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_prj_time_entries_TenantId_Status",
                table: "prj_time_entries",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prj_members");

            migrationBuilder.DropTable(
                name: "prj_milestones");

            migrationBuilder.DropTable(
                name: "prj_time_entries");

            migrationBuilder.DropTable(
                name: "prj_tasks");

            migrationBuilder.DropTable(
                name: "prj_projects");
        }
    }
}
