namespace Erpos.Application.Authorization;

/// <summary>One permission. Codes look like "module.resource.action" and the first segment is always the module code.</summary>
public record PermissionDef(string Code, string Module, string Resource, string Action, string Description);

public static class Permissions
{
    // Core administration — referenced directly by the API.
    public const string EntitiesView = "core.entities.view";
    public const string EntitiesCreate = "core.entities.create";
    public const string EntitiesEdit = "core.entities.edit";
    public const string EntitiesDelete = "core.entities.delete";
    public const string ModulesManage = "core.modules.manage";
    public const string UsersView = "core.users.view";
    public const string UsersCreate = "core.users.create";
    public const string UsersEdit = "core.users.edit";
    public const string UsersDelete = "core.users.delete";
    public const string UsersAssign = "core.users.assign";
    public const string RolesView = "core.roles.view";
    public const string RolesManage = "core.roles.manage";
    public const string AuditView = "core.audit.view";

    // HR & payroll
    public const string EmployeesView = "hr.employees.view";
    public const string EmployeesCreate = "hr.employees.create";
    public const string EmployeesEdit = "hr.employees.edit";
    public const string EmployeesDelete = "hr.employees.delete";
    public const string DepartmentsView = "hr.departments.view";
    public const string DepartmentsCreate = "hr.departments.create";
    public const string DepartmentsEdit = "hr.departments.edit";
    public const string DepartmentsDelete = "hr.departments.delete";
    public const string AttendanceView = "hr.attendance.view";
    public const string AttendanceCreate = "hr.attendance.create";
    public const string AttendanceEdit = "hr.attendance.edit";
    public const string LeaveView = "hr.leave.view";
    public const string LeaveCreate = "hr.leave.create";
    public const string LeaveApprove = "hr.leave.approve";
    public const string HrSettingsManage = "hr.settings.manage";
    public const string SalaryView = "payroll.salary_structures.view";
    public const string SalaryCreate = "payroll.salary_structures.create";
    public const string SalaryEdit = "payroll.salary_structures.edit";
    public const string PayrollView = "payroll.payroll_runs.view";
    public const string PayrollCreate = "payroll.payroll_runs.create";
    public const string PayrollApprove = "payroll.payroll_runs.approve";
    public const string PayrollPost = "payroll.payroll_runs.post";
    public const string PayslipsView = "payroll.payslips.view";
    public const string PayrollSettingsManage = "payroll.settings.manage";

    // Finance
    public const string AccountsView = "finance.accounts.view";
    public const string AccountsCreate = "finance.accounts.create";
    public const string AccountsEdit = "finance.accounts.edit";
    public const string JournalsView = "finance.journals.view";
    public const string JournalsCreate = "finance.journals.create";
    public const string JournalsPost = "finance.journals.post";
    public const string JournalsReverse = "finance.journals.reverse";
    public const string ContactsView = "finance.contacts.view";
    public const string ContactsCreate = "finance.contacts.create";
    public const string ContactsEdit = "finance.contacts.edit";
    public const string PaymentsView = "finance.payments.view";
    public const string PaymentsCreate = "finance.payments.create";
    public const string PaymentsApprove = "finance.payments.approve";
    public const string ReportsView = "finance.reports.view";
    public const string FinanceSettingsManage = "finance.settings.manage";

    // Inventory & procurement
    public const string ItemsView = "inventory.items.view";
    public const string ItemsCreate = "inventory.items.create";
    public const string ItemsEdit = "inventory.items.edit";
    public const string WarehousesView = "inventory.warehouses.view";
    public const string WarehousesCreate = "inventory.warehouses.create";
    public const string WarehousesEdit = "inventory.warehouses.edit";
    public const string StockView = "inventory.stock.view";
    public const string StockIssue = "inventory.stock.issue";
    public const string StockTransfer = "inventory.stock.transfer";
    public const string StockAdjust = "inventory.stock.adjust";
    public const string PrView = "procurement.purchase_requests.view";
    public const string PrCreate = "procurement.purchase_requests.create";
    public const string PrEdit = "procurement.purchase_requests.edit";
    public const string PrApprove = "procurement.purchase_requests.approve";
    public const string PoView = "procurement.purchase_orders.view";
    public const string PoCreate = "procurement.purchase_orders.create";
    public const string PoEdit = "procurement.purchase_orders.edit";
    public const string PoApprove = "procurement.purchase_orders.approve";
    public const string GrnView = "procurement.goods_receipts.view";
    public const string GrnCreate = "procurement.goods_receipts.create";

    // Hotel
    public const string RoomsView = "hotel.rooms.view";
    public const string RoomsCreate = "hotel.rooms.create";
    public const string RoomsEdit = "hotel.rooms.edit";
    public const string ReservationsView = "hotel.reservations.view";
    public const string ReservationsCreate = "hotel.reservations.create";
    public const string ReservationsEdit = "hotel.reservations.edit";
    public const string ReservationsCheckIn = "hotel.reservations.checkin";
    public const string ReservationsCheckOut = "hotel.reservations.checkout";
    public const string HousekeepingView = "hotel.housekeeping.view";
    public const string HousekeepingEdit = "hotel.housekeeping.edit";
    public const string HotelReports = "hotel.reports.view";

    // Travel & tours
    public const string BookingsView = "travel.bookings.view";
    public const string BookingsCreate = "travel.bookings.create";
    public const string BookingsEdit = "travel.bookings.edit";
    public const string BookingsCancel = "travel.bookings.cancel";
    public const string VisasEdit = "travel.visas.edit";
    public const string TravelReports = "travel.reports.view";
    public const string PackagesView = "tourism.packages.view";
    public const string PackagesCreate = "tourism.packages.create";
    public const string PackagesEdit = "tourism.packages.edit";
    public const string DeparturesView = "tourism.departures.view";
    public const string DeparturesCreate = "tourism.departures.create";
    public const string DeparturesEdit = "tourism.departures.edit";
    public const string GuidesView = "tourism.guides.view";
    public const string GuidesCreate = "tourism.guides.create";
    public const string GuidesEdit = "tourism.guides.edit";

    // Logistics
    public const string ShipmentsView = "logistics.shipments.view";
    public const string ShipmentsCreate = "logistics.shipments.create";
    public const string ShipmentsEdit = "logistics.shipments.edit";
    public const string ShipmentsDispatch = "logistics.shipments.dispatch";
    public const string FleetView = "logistics.fleet.view";
    public const string FleetCreate = "logistics.fleet.create";
    public const string FleetEdit = "logistics.fleet.edit";
    public const string DriversView = "logistics.drivers.view";
    public const string DriversCreate = "logistics.drivers.create";
    public const string DriversEdit = "logistics.drivers.edit";
    public const string RoutesView = "logistics.routes.view";
    public const string RoutesCreate = "logistics.routes.create";
    public const string RoutesEdit = "logistics.routes.edit";
    public const string CodRemit = "logistics.cod.remit";
    public const string LogisticsReports = "logistics.reports.view";

    // NGO
    public const string DonorsView = "ngo.donors.view";
    public const string DonorsCreate = "ngo.donors.create";
    public const string DonorsEdit = "ngo.donors.edit";
    public const string GrantsView = "ngo.grants.view";
    public const string GrantsCreate = "ngo.grants.create";
    public const string GrantsEdit = "ngo.grants.edit";
    public const string GrantsApprove = "ngo.grants.approve";
    public const string ProgramsView = "ngo.programs.view";
    public const string ProgramsCreate = "ngo.programs.create";
    public const string ProgramsEdit = "ngo.programs.edit";
    public const string BeneficiariesView = "ngo.beneficiaries.view";
    public const string BeneficiariesCreate = "ngo.beneficiaries.create";
    public const string BeneficiariesEdit = "ngo.beneficiaries.edit";
    public const string DonationsView = "ngo.donations.view";
    public const string DonationsCreate = "ngo.donations.create";
    public const string FundsView = "ngo.funds.view";
    public const string FundsManage = "ngo.funds.manage";
    public const string FundExpensesView = "ngo.expenses.view";
    public const string FundExpensesCreate = "ngo.expenses.create";
    public const string NgoReports = "ngo.reports.view";

    // Projects & services
    public const string ClientsView = "projects.clients.view";
    public const string ClientsCreate = "projects.clients.create";
    public const string ClientsEdit = "projects.clients.edit";
    public const string ProjectsView = "projects.projects.view";
    public const string ProjectsCreate = "projects.projects.create";
    public const string ProjectsEdit = "projects.projects.edit";
    public const string TasksView = "projects.tasks.view";
    public const string TasksCreate = "projects.tasks.create";
    public const string TasksEdit = "projects.tasks.edit";
    public const string TasksDelete = "projects.tasks.delete";
    public const string TimesheetsView = "projects.timesheets.view";
    public const string TimesheetsCreate = "projects.timesheets.create";
    public const string TimesheetsApprove = "projects.timesheets.approve";
    public const string ProjectBilling = "projects.billing.create";
    public const string ProjectReports = "projects.reports.view";

    private static readonly string[] Crud = ["view", "create", "edit", "delete"];

    public static readonly IReadOnlyList<PermissionDef> Catalog = Build();

    private static List<PermissionDef> Build()
    {
        var list = new List<PermissionDef>();

        void Add(string module, string resource, params string[] actions)
        {
            foreach (var a in actions)
                list.Add(new($"{module}.{resource}.{a}", module, resource, a, $"{Cap(a)} {resource.Replace('_', ' ')}"));
        }

        Add(Modules.Core, "entities", Crud);
        Add(Modules.Core, "modules", "manage");
        Add(Modules.Core, "users", [.. Crud, "assign"]);
        Add(Modules.Core, "roles", "view", "manage");
        Add(Modules.Core, "audit", "view");

        Add(Modules.Hr, "employees", Crud);
        Add(Modules.Hr, "departments", Crud);
        Add(Modules.Hr, "attendance", "view", "create", "edit", "approve");
        Add(Modules.Hr, "leave", "view", "create", "edit", "approve");
        Add(Modules.Hr, "settings", "manage");

        Add(Modules.Payroll, "salary_structures", Crud);
        Add(Modules.Payroll, "payroll_runs", "view", "create", "approve", "post");
        Add(Modules.Payroll, "payslips", "view", "export");
        Add(Modules.Payroll, "settings", "manage");

        Add(Modules.Finance, "accounts", Crud);
        Add(Modules.Finance, "journals", "view", "create", "edit", "post", "reverse");
        Add(Modules.Finance, "contacts", "view", "create", "edit");
        Add(Modules.Finance, "invoices", [.. Crud, "approve"]);
        Add(Modules.Finance, "bills", [.. Crud, "approve"]);
        Add(Modules.Finance, "expenses", [.. Crud, "approve"]);
        Add(Modules.Finance, "payments", "view", "create", "approve");
        Add(Modules.Finance, "reports", "view", "export");
        Add(Modules.Finance, "settings", "manage");

        Add(Modules.Inventory, "items", Crud);
        Add(Modules.Inventory, "warehouses", Crud);
        Add(Modules.Inventory, "stock", "view", "issue", "transfer", "adjust");

        Add(Modules.Procurement, "vendors", Crud);
        Add(Modules.Procurement, "purchase_requests", [.. Crud, "approve"]);
        Add(Modules.Procurement, "purchase_orders", [.. Crud, "approve"]);
        Add(Modules.Procurement, "goods_receipts", "view", "create");

        Add(Modules.Hotel, "rooms", Crud);
        Add(Modules.Hotel, "reservations", [.. Crud, "checkin", "checkout"]);
        Add(Modules.Hotel, "housekeeping", "view", "edit");
        Add(Modules.Hotel, "reports", "view");

        Add(Modules.Travel, "bookings", [.. Crud, "cancel"]);
        Add(Modules.Travel, "tickets", Crud);
        Add(Modules.Travel, "visas", Crud);
        Add(Modules.Travel, "reports", "view");

        Add(Modules.Tourism, "packages", Crud);
        Add(Modules.Tourism, "departures", Crud);
        Add(Modules.Tourism, "guides", Crud);

        Add(Modules.Logistics, "shipments", [.. Crud, "dispatch"]);
        Add(Modules.Logistics, "fleet", Crud);
        Add(Modules.Logistics, "drivers", Crud);
        Add(Modules.Logistics, "routes", Crud);
        Add(Modules.Logistics, "cod", "remit");
        Add(Modules.Logistics, "reports", "view");

        Add(Modules.Ngo, "donors", Crud);
        Add(Modules.Ngo, "grants", [.. Crud, "approve"]);
        Add(Modules.Ngo, "programs", Crud);
        Add(Modules.Ngo, "beneficiaries", Crud);
        Add(Modules.Ngo, "donations", "view", "create");
        Add(Modules.Ngo, "funds", "view", "manage");
        Add(Modules.Ngo, "expenses", "view", "create");
        Add(Modules.Ngo, "reports", "view");

        Add(Modules.Projects, "clients", Crud);
        Add(Modules.Projects, "projects", Crud);
        Add(Modules.Projects, "tasks", Crud);
        Add(Modules.Projects, "timesheets", "view", "create", "edit", "approve");
        Add(Modules.Projects, "billing", "create");
        Add(Modules.Projects, "reports", "view");

        return list;
    }

    private static readonly HashSet<string> AllCodes = Catalog.Select(p => p.Code).ToHashSet();

    public static bool Exists(string code) => AllCodes.Contains(code);
    public static string ModuleOf(string code) => code[..code.IndexOf('.')];
    private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
