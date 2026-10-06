using System.Text.Json;
using Erpos.Application.Common;
using Erpos.Domain.Common;
using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erpos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<BusinessEntity> Entities => Set<BusinessEntity>();
    public DbSet<EntityModule> EntityModules => Set<EntityModule>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveApprovalStep> LeaveApprovalSteps => Set<LeaveApprovalStep>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveApproval> LeaveApprovals => Set<LeaveApproval>();
    public DbSet<HrSettings> HrSettings => Set<HrSettings>();
    public DbSet<PayComponent> PayComponents => Set<PayComponent>();
    public DbSet<EmployeeSalary> EmployeeSalaries => Set<EmployeeSalary>();
    public DbSet<EmployeeSalaryLine> EmployeeSalaryLines => Set<EmployeeSalaryLine>();
    public DbSet<TaxYear> TaxYears => Set<TaxYear>();
    public DbSet<TaxSlab> TaxSlabs => Set<TaxSlab>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayslipLine> PayslipLines => Set<PayslipLine>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FinanceSettings> FinanceSettings => Set<FinanceSettings>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<FinanceDocument> FinanceDocuments => Set<FinanceDocument>();
    public DbSet<FinanceDocumentLine> FinanceDocumentLines => Set<FinanceDocumentLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockBatch> StockBatches => Set<StockBatch>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockBatchLevel> StockBatchLevels => Set<StockBatchLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<StockTransactionLine> StockTransactionLines => Set<StockTransactionLine>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestLine> PurchaseRequestLines => Set<PurchaseRequestLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationRoom> ReservationRooms => Set<ReservationRoom>();
    public DbSet<FolioCharge> FolioCharges => Set<FolioCharge>();
    public DbSet<FolioDeposit> FolioDeposits => Set<FolioDeposit>();
    public DbSet<TourPackage> TourPackages => Set<TourPackage>();
    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();
    public DbSet<Guide> Guides => Set<Guide>();
    public DbSet<TourDeparture> TourDepartures => Set<TourDeparture>();
    public DbSet<DepartureGuide> DepartureGuides => Set<DepartureGuide>();
    public DbSet<DepartureCost> DepartureCosts => Set<DepartureCost>();
    public DbSet<TravelBooking> TravelBookings => Set<TravelBooking>();
    public DbSet<BookingPassenger> BookingPassengers => Set<BookingPassenger>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<BookingDeposit> BookingDeposits => Set<BookingDeposit>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<FreightRoute> FreightRoutes => Set<FreightRoute>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripExpense> TripExpenses => Set<TripExpense>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<NgoProgram> NgoPrograms => Set<NgoProgram>();
    public DbSet<Grant> Grants => Set<Grant>();
    public DbSet<GrantBudgetLine> GrantBudgetLines => Set<GrantBudgetLine>();
    public DbSet<GrantTranche> GrantTranches => Set<GrantTranche>();
    public DbSet<GrantReport> GrantReports => Set<GrantReport>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<FundExpense> FundExpenses => Set<FundExpense>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<Assistance> Assistance => Set<Assistance>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    /// <summary>Read by the query filters on every query. Null (anonymous or platform admin) matches no tenant rows.</summary>
    private Guid? CurrentTenantId => currentUser.TenantId;

    private static readonly HashSet<string> SecretProperties = [nameof(User.PasswordHash), nameof(RefreshToken.TokenHash)];
    // Recorded as changed but without values, so audit readers without HR/payroll rights don't see them.
    private static readonly HashSet<string> MaskedProperties = [nameof(Employee.Cnic), nameof(Employee.Iban), nameof(Employee.BankAccountTitle),
        nameof(Employee.EobiNumber), nameof(Employee.Ntn)];
    private static readonly HashSet<Type> MaskedEntities = [typeof(EmployeeSalary), typeof(EmployeeSalaryLine), typeof(Payslip)];

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // Money in PKR with paisa; rates are stored with their own precision in the configurations.
        c.Properties<decimal>().HavePrecision(18, 2);
        // The Oracle MySQL provider stores DateOnly/TimeOnly but reads them back as DateTime/TimeSpan.
        c.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        c.Properties<TimeOnly>().HaveConversion<TimeOnlyConverter>().HaveColumnType("time");
    }

    private class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
    private class TimeOnlyConverter() : ValueConverter<TimeOnly, TimeSpan>(t => t.ToTimeSpan(), t => TimeOnly.FromTimeSpan(t));

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // All Guid keys are created in code (Guid.NewGuid()). Telling EF so means new child rows added to an existing
        // parent's collection (e.g. replacing a draft's lines) are inserted, not mistaken for existing rows to update.
        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetKeys()).Where(k => k.IsPrimaryKey()))
            foreach (var p in key.Properties.Where(p => p.ClrType == typeof(Guid)))
                p.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;

        b.Entity<Tenant>().HasQueryFilter(t => !t.IsDeleted && t.Id == CurrentTenantId);
        b.Entity<User>().HasQueryFilter(u => !u.IsDeleted && u.TenantId == CurrentTenantId);
        b.Entity<BusinessEntity>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Role>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<UserRoleAssignment>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<UserPermissionOverride>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<EntityModule>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<RolePermission>().HasQueryFilter(rp => !rp.Role!.IsDeleted && rp.Role.TenantId == CurrentTenantId);

        // HR & payroll. Child rows (approvals, salary/payslip lines, slabs, adjustments) are only reached through their parent.
        b.Entity<Department>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Designation>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Holiday>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<AttendanceRecord>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<LeaveType>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<LeaveApprovalStep>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<LeaveRequest>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<HrSettings>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<PayComponent>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<EmployeeSalary>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<TaxYear>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<PayrollRun>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Payslip>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);

        // Finance. Journal/document lines and allocations are reached through their parent.
        b.Entity<Account>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<FinanceSettings>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<ExchangeRate>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<TaxRate>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Contact>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<NumberSequence>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<JournalEntry>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<FinanceDocument>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);

        // Inventory & procurement. Lines are reached through their parent document.
        b.Entity<ItemCategory>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Item>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Warehouse>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<StockBatch>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<StockLevel>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<StockBatchLevel>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<StockMovement>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        b.Entity<StockTransaction>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<PurchaseRequest>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<PurchaseOrder>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<GoodsReceipt>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);

        // Hotel
        b.Entity<RoomType>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Room>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Guest>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Reservation>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<FolioCharge>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<FolioDeposit>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);

        // Travel & tours
        b.Entity<TourPackage>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Guide>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<TourDeparture>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<TravelBooking>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<BookingDeposit>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);

        // Logistics
        b.Entity<Vehicle>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Driver>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<FreightRoute>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Shipment>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Trip>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<MaintenanceRecord>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);

        // NGO
        b.Entity<Donor>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Fund>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<NgoProgram>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Grant>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Donation>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<FundExpense>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Beneficiary>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<Assistance>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);

        // Projects
        b.Entity<Project>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<ProjectTask>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        b.Entity<TimeEntry>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
    }

    public async Task LockAsync<T>(Guid id, CancellationToken ct = default) where T : class
    {
        if (Database.CurrentTransaction == null) return; // no transaction to hold the lock
        var table = Model.FindEntityType(typeof(T))?.GetTableName() ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped.");
        await Database.ExecuteSqlRawAsync($"SELECT Id FROM `{table}` WHERE Id = {{0}} FOR UPDATE", [id.ToString()], ct);
    }

    public async Task<int> NextSequenceAsync(string key, CancellationToken ct = default)
    {
        var tenantId = CurrentTenantId ?? throw new InvalidOperationException("Numbering needs an organization context.");
        // Classic MySQL counter: LAST_INSERT_ID(expr) is per-connection, so both statements must share one connection.
        await Database.OpenConnectionAsync(ct);
        try
        {
            await Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO fin_number_sequences (Id, TenantId, `Key`, `Next`)
                VALUES ({Guid.NewGuid().ToString()}, {tenantId.ToString()}, {key}, LAST_INSERT_ID(1))
                ON DUPLICATE KEY UPDATE `Next` = LAST_INSERT_ID(`Next` + 1)", ct);
            return await Database.SqlQueryRaw<int>("SELECT CAST(LAST_INSERT_ID() AS SIGNED) AS `Value`").SingleAsync(ct);
        }
        finally
        {
            await Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Saves the changes and their audit rows atomically. Inside a request transaction (see the API's transaction filter)
    /// it simply joins it; on its own it opens a transaction so data and audit can't be split.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction != null) return await SaveWithAuditAsync(ct);
        await using var tx = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var result = await SaveWithAuditAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<int> SaveWithAuditAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var audits = new List<(EntityEntry Entry, AuditLog Log)>();

        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is BaseEntity be)
            {
                if (entry.State == EntityState.Added) be.CreatedBy ??= currentUser.UserId;
                else if (entry.State == EntityState.Modified)
                {
                    be.UpdatedAt = now;
                    be.UpdatedBy = currentUser.UserId;
                }
            }

            if (entry.Entity is AuditLog or RefreshToken or PayslipLine or LeaveApproval or JournalLine or NumberSequence or StockMovement or ShipmentEvent
                or StockLevel or StockBatchLevel) continue;
            audits.Add((entry, BuildAudit(entry, now)));
        }

        var result = await base.SaveChangesAsync(ct);

        if (audits.Count > 0)
        {
            foreach (var (entry, log) in audits)
                log.RecordId ??= entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();
            AuditLogs.AddRange(audits.Select(a => a.Log));
            await base.SaveChangesAsync(ct);
        }
        return result;
    }

    private AuditLog BuildAudit(EntityEntry entry, DateTime now)
    {
        var action = entry.State switch
        {
            EntityState.Added => "Create",
            EntityState.Deleted => "Delete",
            _ when entry.Entity is BaseEntity { IsDeleted: true } && entry.Property(nameof(BaseEntity.IsDeleted)).IsModified => "Delete",
            _ => "Update"
        };

        var changes = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            if (SecretProperties.Contains(p.Metadata.Name)) continue;
            var masked = MaskedEntities.Contains(entry.Entity.GetType()) && !p.Metadata.IsKey() && !p.Metadata.IsForeignKey() || MaskedProperties.Contains(p.Metadata.Name);
            if (entry.State == EntityState.Modified)
            {
                if (!p.IsModified || Equals(p.OriginalValue, p.CurrentValue)) continue;
                changes[p.Metadata.Name] = masked ? "(changed, hidden)" : new { from = p.OriginalValue, to = p.CurrentValue };
            }
            else changes[p.Metadata.Name] = masked ? "(hidden)" : entry.State == EntityState.Deleted ? p.OriginalValue : p.CurrentValue;
        }

        Guid? tenantId = entry.Entity switch
        {
            ITenantOwned t => t.TenantId,
            User u => u.TenantId,
            Tenant t => t.Id,
            _ => currentUser.TenantId
        };

        return new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            Action = action,
            TableName = entry.Metadata.ClrType.Name,
            RecordId = entry.State == EntityState.Added ? null
                : entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
            Changes = changes.Count == 0 ? null : JsonSerializer.Serialize(changes),
            Timestamp = now
        };
    }
}
