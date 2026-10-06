using Erpos.Domain.Entities;

namespace Erpos.Application.Authorization;

/// <summary>Starter roles created for every new organization. They are ordinary roles afterwards and can be edited.</summary>
public static class DefaultRoles
{
    public static IEnumerable<Role> Create(Guid tenantId)
    {
        var all = Permissions.Catalog.Select(p => p.Code).ToList();

        var adminOnly = new HashSet<string>
        {
            Permissions.EntitiesDelete, Permissions.ModulesManage, Permissions.RolesManage, Permissions.UsersDelete,
            Permissions.AuditView
        };
        // Salaries are confidential: payroll belongs to HR, not to every operational manager.
        // The ledger, reports and payments stay with Finance; managers can raise invoices and bills.
        string[] financeOnly = ["finance.accounts.", "finance.journals.", "finance.reports.", "finance.settings.", "finance.payments.approve"];
        var manager = all.Where(c => !adminOnly.Contains(c) && !c.EndsWith(".delete") &&
                                     !c.StartsWith(Modules.Payroll + ".") && c != Permissions.HrSettingsManage &&
                                     !financeOnly.Any(c.StartsWith)).ToList();
        var accountant = all.Where(c => c.StartsWith(Modules.Finance + ".")).ToList();
        accountant.AddRange([Permissions.EntitiesView, Permissions.PayrollView, Permissions.PoView, Permissions.GrnView, Permissions.StockView, Permissions.ItemsView,
            Permissions.ProjectsView, Permissions.ProjectBilling, Permissions.ProjectReports, Permissions.ClientsView]);
        var storekeeper = all.Where(c => c.StartsWith(Modules.Inventory + ".") && !c.EndsWith(".delete")).ToList();
        storekeeper.AddRange([Permissions.EntitiesView, Permissions.GrnView, Permissions.GrnCreate, Permissions.PoView, Permissions.PrView, Permissions.PrCreate]);
        var procurement = all.Where(c => c.StartsWith(Modules.Procurement + ".")).ToList();
        procurement.AddRange([Permissions.EntitiesView, Permissions.ItemsView, Permissions.ItemsCreate, Permissions.StockView, Permissions.WarehousesView,
            Permissions.ContactsView, Permissions.ContactsCreate, Permissions.ContactsEdit]);
        var hrOfficer = all.Where(c => c.StartsWith(Modules.Hr + ".") || c.StartsWith(Modules.Payroll + ".")).ToList();
        hrOfficer.AddRange([Permissions.EntitiesView, Permissions.UsersView]);

        // Permissions apply to everyone at the entity, so the baseline role only lets people submit their own
        // requests. Own records (attendance, leave, payslips, salary) are always visible through self-service.
        var employee = new List<string>
        {
            Permissions.EntitiesView, Permissions.LeaveCreate, "finance.expenses.create", Permissions.TimesheetsCreate
        };

        yield return Build(tenantId, "Administrator", "Full access to the assigned entity and everything below it", all);
        yield return Build(tenantId, "Manager", "Operate and approve; cannot delete records or change security", manager);
        yield return Build(tenantId, "HR Officer", "Employees, attendance, leave and payroll", hrOfficer);
        yield return Build(tenantId, "Accountant", "Ledger, invoices, bills, payments and financial reports", accountant);
        yield return Build(tenantId, "Storekeeper", "Warehouses, stock issues, transfers, counts and goods receipts", storekeeper);
        yield return Build(tenantId, "Procurement Officer", "Purchase requests, purchase orders, vendors and receipts", procurement);
        yield return Build(tenantId, "Front Desk", "Reservations, check-in/out, folios and deposits",
            all.Where(c => c.StartsWith("hotel.reservations.") && !c.EndsWith(".delete"))
                .Concat([Permissions.RoomsView, Permissions.HousekeepingView, Permissions.EntitiesView, Permissions.ContactsView]));
        yield return Build(tenantId, "Housekeeping", "Room cleaning status board",
            [Permissions.HousekeepingView, Permissions.HousekeepingEdit, Permissions.RoomsView, Permissions.EntitiesView]);
        yield return Build(tenantId, "Travel Consultant", "Customer bookings, tickets, visas and tour seats",
            all.Where(c => c.StartsWith(Modules.Travel + ".") && !c.EndsWith(".delete"))
                .Concat([Permissions.PackagesView, Permissions.DeparturesView, Permissions.GuidesView, Permissions.EntitiesView,
                    Permissions.ContactsView, Permissions.ContactsCreate, Permissions.ContactsEdit]));
        yield return Build(tenantId, "Tour Operator", "Packages, departures, guides and manifests",
            all.Where(c => c.StartsWith(Modules.Tourism + ".") && !c.EndsWith(".delete"))
                .Concat([Permissions.BookingsView, Permissions.EntitiesView, Permissions.ContactsView]));
        yield return Build(tenantId, "Dispatcher", "Consignments, trips, fleet and drivers",
            all.Where(c => c.StartsWith(Modules.Logistics + ".") && !c.EndsWith(".delete") && c != Permissions.CodRemit)
                .Concat([Permissions.EntitiesView, Permissions.ContactsView, Permissions.ContactsCreate]));
        yield return Build(tenantId, "Grants Manager", "Grants, budgets, tranches, donor reports, fund spending and donations",
            all.Where(c => c.StartsWith(Modules.Ngo + ".") && !c.EndsWith(".delete") && c != Permissions.BeneficiariesCreate && c != Permissions.BeneficiariesEdit)
                .Concat([Permissions.EntitiesView, Permissions.ContactsView, Permissions.ContactsCreate]));
        yield return Build(tenantId, "Program Officer", "Beneficiary registration and in-kind assistance",
            [Permissions.BeneficiariesView, Permissions.BeneficiariesCreate, Permissions.BeneficiariesEdit, Permissions.ProgramsView, Permissions.GrantsView,
             Permissions.FundsView, Permissions.EntitiesView]);
        yield return Build(tenantId, "Fundraising Officer", "Donors, donations and receipts",
            [Permissions.DonorsView, Permissions.DonorsCreate, Permissions.DonorsEdit, Permissions.DonationsView, Permissions.DonationsCreate,
             Permissions.FundsView, Permissions.ProgramsView, Permissions.EntitiesView, Permissions.ContactsView]);
        yield return Build(tenantId, "Project Manager", "Projects, tasks, team timesheet approval, client billing and utilization",
            all.Where(c => c.StartsWith(Modules.Projects + ".") && !c.EndsWith(".delete") || c == Permissions.TasksDelete)
                .Concat([Permissions.EntitiesView, Permissions.ContactsView, Permissions.ContactsCreate]));
        yield return Build(tenantId, "Employee", "Self-service: apply for leave, submit expenses and timesheets", employee);
    }

    /// <summary>Default roles an organization doesn't have yet (it was created before a phase shipped).</summary>
    public static IEnumerable<Role> Missing(Guid tenantId, IEnumerable<string> existingNames)
    {
        var have = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Create(tenantId).Where(r => !have.Contains(r.Name));
    }

    private static Role Build(Guid tenantId, string name, string description, IEnumerable<string> codes)
    {
        var role = new Role { TenantId = tenantId, Name = name, Description = description, IsSystem = true };
        foreach (var c in codes.Distinct())
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = c });
        return role;
    }
}
