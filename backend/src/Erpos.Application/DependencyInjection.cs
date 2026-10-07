using Erpos.Application.Authorization;
using Erpos.Application.Hr;
using Erpos.Application.Payroll;
using Erpos.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Erpos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<AuthService>();
        services.AddScoped<TenantService>();
        services.AddScoped<EntityService>();
        services.AddScoped<UserService>();
        services.AddScoped<RoleService>();
        services.AddScoped<InsightService>();
        services.AddScoped<UserRules>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<LeaveService>();
        services.AddScoped<PayrollSetupService>();
        services.AddScoped<PayrollRunService>();
        services.AddScoped<Finance.LedgerService>();
        services.AddScoped<Finance.FinanceSetupService>();
        services.AddScoped<Finance.JournalService>();
        services.AddScoped<Finance.DocumentService>();
        services.AddScoped<Finance.PaymentService>();
        services.AddScoped<Finance.PayrollAccountingService>();
        services.AddScoped<Finance.ReportService>();
        services.AddScoped<Inventory.StockEngine>();
        services.AddScoped<Inventory.InventoryService>();
        services.AddScoped<Inventory.ProcurementService>();
        services.AddScoped<Finance.AdvanceService>();
        services.AddScoped<Hotel.HotelSetupService>();
        services.AddScoped<Hotel.ReservationService>();
        services.AddScoped<Travel.TourService>();
        services.AddScoped<Travel.TravelBookingService>();
        services.AddScoped<Logistics.FleetService>();
        services.AddScoped<Logistics.ShipmentService>();
        services.AddScoped<Logistics.TripService>();
        services.AddScoped<Ngo.NgoSetupService>();
        services.AddScoped<Ngo.FundService>();
        services.AddScoped<Ngo.GrantService>();
        services.AddScoped<Ngo.BeneficiaryService>();
        services.AddScoped<Ngo.NgoReportService>();
        services.AddScoped<Projects.ProjectService>();
        services.AddScoped<Projects.ProjectTaskService>();
        services.AddScoped<Projects.TimesheetService>();
        services.AddScoped<Projects.ProjectReportService>();
        services.AddScoped<Finance.WithholdingService>();
        services.AddScoped<Finance.BankReconciliationService>();
        services.AddScoped<Finance.FixedAssetService>();
        services.AddScoped<Services.AttachmentService>();
        services.AddScoped<Services.EmailService>();
        services.AddScoped<Services.ReminderService>();
        services.AddScoped<Common.BackgroundTenantContext>();
        // Modules that release their records when Finance voids an invoice or bill.
        services.AddScoped<Finance.IDocumentVoidHandler, Finance.ProjectInvoiceVoidHandler>();
        services.AddScoped<Finance.IDocumentVoidHandler, Finance.ShipmentInvoiceVoidHandler>();
        services.AddScoped<Finance.IDocumentVoidHandler, Finance.TravelInvoiceVoidHandler>();
        services.AddScoped<Finance.IDocumentVoidHandler, Finance.FundExpenseVoidHandler>();
        return services;
    }
}
