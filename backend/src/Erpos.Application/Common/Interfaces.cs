using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Common;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    UserType? UserType { get; }
    bool IsAuthenticated => UserId.HasValue;
    bool IsPlatformAdmin => UserType == Domain.Enums.UserType.PlatformAdmin;
    bool IsSuperAdmin => UserType == Domain.Enums.UserType.SuperAdmin;
}

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<BusinessEntity> Entities { get; }
    DbSet<EntityModule> EntityModules { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRoleAssignment> UserRoleAssignments { get; }
    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // HR & payroll
    DbSet<Department> Departments { get; }
    DbSet<Designation> Designations { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<LeaveApprovalStep> LeaveApprovalSteps { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<LeaveApproval> LeaveApprovals { get; }
    DbSet<HrSettings> HrSettings { get; }
    DbSet<PayComponent> PayComponents { get; }
    DbSet<EmployeeSalary> EmployeeSalaries { get; }
    DbSet<EmployeeSalaryLine> EmployeeSalaryLines { get; }
    DbSet<TaxYear> TaxYears { get; }
    DbSet<TaxSlab> TaxSlabs { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollAdjustment> PayrollAdjustments { get; }
    DbSet<Payslip> Payslips { get; }
    DbSet<PayslipLine> PayslipLines { get; }

    // Finance
    DbSet<Account> Accounts { get; }
    DbSet<FinanceSettings> FinanceSettings { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<TaxRate> TaxRates { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<NumberSequence> NumberSequences { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<FinanceDocument> FinanceDocuments { get; }
    DbSet<FinanceDocumentLine> FinanceDocumentLines { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentAllocation> PaymentAllocations { get; }

    // Inventory & procurement
    DbSet<ItemCategory> ItemCategories { get; }
    DbSet<Item> Items { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<StockBatch> StockBatches { get; }
    DbSet<StockLevel> StockLevels { get; }
    DbSet<StockBatchLevel> StockBatchLevels { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<StockTransactionLine> StockTransactionLines { get; }
    DbSet<PurchaseRequest> PurchaseRequests { get; }
    DbSet<PurchaseRequestLine> PurchaseRequestLines { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }

    // Hotel
    DbSet<RoomType> RoomTypes { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Guest> Guests { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<ReservationRoom> ReservationRooms { get; }
    DbSet<FolioCharge> FolioCharges { get; }
    DbSet<FolioDeposit> FolioDeposits { get; }

    // Travel & tours
    DbSet<TourPackage> TourPackages { get; }
    DbSet<ItineraryDay> ItineraryDays { get; }
    DbSet<Guide> Guides { get; }
    DbSet<TourDeparture> TourDepartures { get; }
    DbSet<DepartureGuide> DepartureGuides { get; }
    DbSet<DepartureCost> DepartureCosts { get; }
    DbSet<TravelBooking> TravelBookings { get; }
    DbSet<BookingPassenger> BookingPassengers { get; }
    DbSet<BookingItem> BookingItems { get; }
    DbSet<BookingDeposit> BookingDeposits { get; }

    // Logistics
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<FreightRoute> FreightRoutes { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentEvent> ShipmentEvents { get; }
    DbSet<Trip> Trips { get; }
    DbSet<TripExpense> TripExpenses { get; }
    DbSet<Donor> Donors { get; }
    DbSet<Fund> Funds { get; }
    DbSet<NgoProgram> NgoPrograms { get; }
    DbSet<Grant> Grants { get; }
    DbSet<GrantBudgetLine> GrantBudgetLines { get; }
    DbSet<GrantTranche> GrantTranches { get; }
    DbSet<GrantReport> GrantReports { get; }
    DbSet<Donation> Donations { get; }
    DbSet<FundExpense> FundExpenses { get; }
    DbSet<Beneficiary> Beneficiaries { get; }
    DbSet<Assistance> Assistance { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectMember> ProjectMembers { get; }
    DbSet<ProjectMilestone> ProjectMilestones { get; }
    DbSet<ProjectTask> ProjectTasks { get; }
    DbSet<TimeEntry> TimeEntries { get; }
    DbSet<WithholdingTaxRate> WithholdingTaxRates { get; }
    DbSet<WhtDeposit> WhtDeposits { get; }
    DbSet<BankReconciliation> BankReconciliations { get; }
    DbSet<BankStatementLine> BankStatementLines { get; }
    DbSet<MaintenanceRecord> MaintenanceRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Locks one row (SELECT … FOR UPDATE) until the current transaction ends, so a "check, then insert" rule (room free,
    /// grant line within budget, one payroll run per month) can't be raced by a simultaneous request.
    /// </summary>
    Task LockAsync<T>(Guid id, CancellationToken ct = default) where T : class;

    /// <summary>Atomically issues the next number of a per-organization series, independent of pending changes.</summary>
    Task<int> NextSequenceAsync(string key, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashToken(string token);
    int RefreshTokenDays { get; }
}
