using Erpos.Domain.Enums;

namespace Erpos.Application.Authorization;

public record ModuleDef(string Code, string Name, string Description, bool AlwaysOn, IndustryType[] SuggestedFor);

public static class Modules
{
    public const string Core = "core";
    public const string Hr = "hr";
    public const string Payroll = "payroll";
    public const string Finance = "finance";
    public const string Inventory = "inventory";
    public const string Procurement = "procurement";
    public const string Hotel = "hotel";
    public const string Travel = "travel";
    public const string Tourism = "tourism";
    public const string Logistics = "logistics";
    public const string Ngo = "ngo";
    public const string Projects = "projects";

    private static readonly IndustryType[] All = Enum.GetValues<IndustryType>();

    public static readonly IReadOnlyList<ModuleDef> Catalog =
    [
        new(Core, "Administration", "Entities, users, roles, permissions, audit", true, All),
        new(Hr, "Human Resources", "Employees, departments, attendance, leave", false, All),
        new(Payroll, "Payroll", "Salary structures, payslips, payroll runs", false, All),
        new(Finance, "Finance & Accounting", "Chart of accounts, journals, invoices, expenses", false, All),
        new(Inventory, "Inventory", "Items, warehouses, stock movements", false,
            [IndustryType.Hotel, IndustryType.Logistics, IndustryType.Retail, IndustryType.Manufacturing, IndustryType.Ngo]),
        new(Procurement, "Procurement", "Vendors, purchase requests, purchase orders", false, All),
        new(Hotel, "Hotel Management", "Rooms, reservations, front desk, housekeeping", false, [IndustryType.Hotel, IndustryType.Tourism]),
        new(Travel, "Travel Agency", "Bookings, tickets, visas, itineraries", false, [IndustryType.Travel, IndustryType.Tourism]),
        new(Tourism, "Tours & Packages", "Tour packages, departures, guides", false, [IndustryType.Tourism, IndustryType.Travel]),
        new(Logistics, "Logistics", "Shipments, fleet, drivers, routes, tracking", false, [IndustryType.Logistics]),
        new(Ngo, "NGO Management", "Donors, grants, programs, beneficiaries", false, [IndustryType.Ngo]),
        new(Projects, "Projects & Services", "Clients, projects, tasks, timesheets", false,
            [IndustryType.SoftwareServices, IndustryType.Ngo, IndustryType.General]),
    ];

    public static bool Exists(string code) => Catalog.Any(m => m.Code == code);
    public static bool IsAlwaysOn(string code) => Catalog.Any(m => m.Code == code && m.AlwaysOn);
}
