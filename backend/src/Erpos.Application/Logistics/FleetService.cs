using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Logistics;

/// <summary>Vehicles, drivers, routes / rate cards and maintenance.</summary>
public class FleetService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents)
{
    internal static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    /// <summary>Expired or soon-expiring vehicle papers (fitness, insurance, route permit, token tax).</summary>
    internal static List<(string Document, DateOnly? Expiry)> VehicleDocuments(Vehicle v) =>
        [("Fitness certificate", v.FitnessExpiry), ("Insurance", v.InsuranceExpiry), ("Route permit", v.RoutePermitExpiry), ("Token tax", v.TokenTaxExpiry)];

    internal static List<string> Alerts(Vehicle v, int withinDays = 30) => VehicleDocuments(v)
        .Where(d => d.Expiry != null && d.Expiry <= Today.AddDays(withinDays))
        .Select(d => d.Expiry < Today ? $"{d.Document} expired {d.Expiry:dd MMM yyyy}" : $"{d.Document} expires {d.Expiry:dd MMM yyyy}").ToList();

    // ---------------- Vehicles ----------------

    public async Task<List<VehicleDto>> VehiclesAsync(CancellationToken ct)
    {
        var visible = await LogisticsEntitiesAsync(ct);
        var vehicles = await db.Vehicles.Where(v => visible.Contains(v.EntityId)).Include(v => v.Entity).Include(v => v.OwnerVendor).OrderBy(v => v.RegistrationNo).ToListAsync(ct);
        var ids = vehicles.Select(v => v.Id).ToList();
        var trips = await db.Trips.Where(t => ids.Contains(t.VehicleId) && t.Status == TripStatus.Dispatched).Select(t => new { t.VehicleId, t.Number }).ToListAsync(ct);
        return vehicles.Select(v => new VehicleDto(v.Id, v.EntityId, v.Entity!.Name, v.RegistrationNo, v.Type, v.MakeModel, v.CapacityKg, v.CapacityCbm, v.IsHired,
            v.OwnerVendorId, v.OwnerVendor?.Name, v.FitnessExpiry, v.InsuranceExpiry, v.RoutePermitExpiry, v.TokenTaxExpiry, v.Odometer, v.Status, Alerts(v),
            trips.FirstOrDefault(t => t.VehicleId == v.Id)?.Number)).ToList();
    }

    public async Task<VehicleDto> SaveVehicleAsync(Guid? id, SaveVehicleRequest req, CancellationToken ct)
    {
        var v = id == null ? null : await db.Vehicles.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Vehicle");
        if (v == null)
        {
            await access.EnsureAsync(Permissions.FleetCreate, req.EntityId, ct);
            v = new Vehicle { TenantId = currentUser.TenantId!.Value };
            db.Vehicles.Add(v);
        }
        else
        {
            await access.EnsureAsync(Permissions.FleetEdit, v.EntityId, ct);
            if (v.Status == VehicleStatus.OnTrip && req.Status != VehicleStatus.OnTrip) throw new ValidationException("The vehicle is on a trip; complete the trip first.");
        }
        var reg = Guard.Required(req.RegistrationNo, "Registration no.", 20).ToUpperInvariant().Replace(" ", "-");
        if (await db.Vehicles.AnyAsync(x => x.RegistrationNo == reg && x.Id != v.Id, ct)) throw new ValidationException($"{reg} is already registered.");
        if (req.CapacityKg <= 0) throw new ValidationException("Enter the payload capacity in kg.");
        if (req.IsHired && req.OwnerVendorId == null) throw new ValidationException("Choose the owner / broker of a hired vehicle.");
        if (req.Status == VehicleStatus.OnTrip && v.Status != VehicleStatus.OnTrip) throw new ValidationException("A vehicle goes on trip by dispatching a trip.");

        v.EntityId = req.EntityId;
        v.RegistrationNo = reg;
        v.Type = req.Type;
        v.MakeModel = req.MakeModel;
        v.CapacityKg = req.CapacityKg;
        v.CapacityCbm = req.CapacityCbm;
        v.IsHired = req.IsHired;
        v.OwnerVendorId = req.IsHired ? req.OwnerVendorId : null;
        v.FitnessExpiry = req.FitnessExpiry;
        v.InsuranceExpiry = req.InsuranceExpiry;
        v.RoutePermitExpiry = req.RoutePermitExpiry;
        v.TokenTaxExpiry = req.TokenTaxExpiry;
        v.Odometer = Math.Max(v.Odometer, req.Odometer);
        v.Status = req.Status;
        await db.SaveChangesAsync(ct);
        return (await VehiclesAsync(ct)).First(x => x.Id == v.Id);
    }

    // ---------------- Drivers ----------------

    public async Task<List<DriverDto>> DriversAsync(CancellationToken ct)
    {
        var visible = await LogisticsEntitiesAsync(ct);
        var drivers = await db.Drivers.Where(d => visible.Contains(d.EntityId)).OrderBy(d => d.FullName).ToListAsync(ct);
        var onTrip = (await db.Trips.Where(t => t.Status == TripStatus.Dispatched).Select(t => t.DriverId).ToListAsync(ct)).ToHashSet();
        return drivers.Select(d => new DriverDto(d.Id, d.EntityId, d.FullName, d.Cnic, d.Phone, d.LicenseNo, d.LicenseCategory, d.LicenseExpiry, d.DailyAllowance,
            d.EmployeeId, d.IsActive, onTrip.Contains(d.Id),
            d.LicenseExpiry is { } e && e <= Today.AddDays(30) ? (e < Today ? $"Licence expired {e:dd MMM yyyy}" : $"Licence expires {e:dd MMM yyyy}") : null)).ToList();
    }

    public async Task<DriverDto> SaveDriverAsync(Guid? id, SaveDriverRequest req, CancellationToken ct)
    {
        var d = id == null ? null : await db.Drivers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Driver");
        if (d == null)
        {
            await access.EnsureAsync(Permissions.DriversCreate, req.EntityId, ct);
            d = new Driver { TenantId = currentUser.TenantId!.Value };
            db.Drivers.Add(d);
        }
        else await access.EnsureAsync(Permissions.DriversEdit, d.EntityId, ct);
        var license = Guard.Required(req.LicenseNo, "Licence no.", 40).ToUpperInvariant();
        if (await db.Drivers.AnyAsync(x => x.LicenseNo == license && x.Id != d.Id, ct)) throw new ValidationException("Another driver has this licence number.");
        d.EntityId = req.EntityId;
        d.FullName = Guard.Required(req.FullName, "Name");
        d.Cnic = req.Cnic;
        d.Phone = req.Phone;
        d.LicenseNo = license;
        d.LicenseCategory = req.LicenseCategory;
        d.LicenseExpiry = req.LicenseExpiry;
        d.DailyAllowance = Math.Max(0, req.DailyAllowance);
        d.EmployeeId = req.EmployeeId;
        d.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await DriversAsync(ct)).First(x => x.Id == d.Id);
    }

    // ---------------- Routes ----------------

    public async Task<List<RouteDto>> RoutesAsync(CancellationToken ct)
    {
        if ((await LogisticsEntitiesAsync(ct)).Count == 0) throw new ForbiddenException();
        return await db.FreightRoutes.OrderBy(r => r.Origin).ThenBy(r => r.Destination)
            .Select(r => new RouteDto(r.Id, r.Code, r.Origin, r.Destination, r.DistanceKm, r.StandardHours, r.RatePerKg, r.MinimumCharge, r.FullTruckRate,
                r.FuelSurchargePercent, r.TaxRateId, r.IsActive)).ToListAsync(ct);
    }

    public async Task<RouteDto> SaveRouteAsync(Guid? id, SaveRouteRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(id == null ? Permissions.RoutesCreate : Permissions.RoutesEdit, ct);
        var r = id == null ? null : await db.FreightRoutes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Route");
        if (r == null) { r = new FreightRoute { TenantId = currentUser.TenantId!.Value }; db.FreightRoutes.Add(r); }
        var code = Guard.Code(req.Code);
        if (await db.FreightRoutes.AnyAsync(x => x.Code == code && x.Id != r.Id, ct)) throw new ValidationException($"Route {code} exists.");
        if (req.RatePerKg < 0 || req.MinimumCharge < 0 || req.FullTruckRate < 0 || req.FuelSurchargePercent is < 0 or > 100) throw new ValidationException("Check the rates.");
        r.Code = code;
        r.Origin = Guard.Required(req.Origin, "Origin", 100);
        r.Destination = Guard.Required(req.Destination, "Destination", 100);
        r.DistanceKm = Math.Max(0, req.DistanceKm);
        r.StandardHours = Math.Max(0, req.StandardHours);
        r.RatePerKg = req.RatePerKg;
        r.MinimumCharge = req.MinimumCharge;
        r.FullTruckRate = req.FullTruckRate;
        r.FuelSurchargePercent = req.FuelSurchargePercent;
        r.TaxRateId = req.TaxRateId;
        r.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await RoutesAsync(ct)).First(x => x.Id == r.Id);
    }

    // ---------------- Maintenance ----------------

    public async Task<List<MaintenanceDto>> MaintenanceAsync(Guid? vehicleId, CancellationToken ct)
    {
        var visible = await LogisticsEntitiesAsync(ct);
        var q = db.MaintenanceRecords.Join(db.Vehicles, m => m.VehicleId, v => v.Id, (m, v) => new { m, v }).Where(x => visible.Contains(x.v.EntityId));
        if (vehicleId != null) q = q.Where(x => x.m.VehicleId == vehicleId);
        return await q.OrderByDescending(x => x.m.Date).Take(300)
            .Select(x => new MaintenanceDto(x.m.Id, x.m.VehicleId, x.v.RegistrationNo, x.m.Date, x.m.Description, x.m.Odometer, x.m.Cost,
                x.m.Vendor == null ? null : x.m.Vendor.Name, x.m.BillId, x.m.NextServiceDate)).ToListAsync(ct);
    }

    /// <summary>Logs a service or repair; with a vendor it drafts the vendor bill (repairs & maintenance).</summary>
    public async Task<MaintenanceDto> AddMaintenanceAsync(SaveMaintenanceRequest req, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.Id == req.VehicleId, ct) ?? throw new NotFoundException("Vehicle");
        await access.EnsureAsync(Permissions.FleetEdit, v.EntityId, ct);
        if (req.Cost < 0) throw new ValidationException("Cost can't be negative.");
        var m = new MaintenanceRecord
        {
            TenantId = v.TenantId, VehicleId = v.Id, Date = req.Date, Description = Guard.Required(req.Description, "Description", 300), Odometer = req.Odometer,
            Cost = LedgerService.Round(req.Cost), VendorId = req.VendorId, NextServiceDate = req.NextServiceDate
        };
        if (req.Odometer is { } odo && odo > v.Odometer) v.Odometer = odo;
        if (req.VendorId is { } vendor && m.Cost > 0)
        {
            var account = await AccountAsync("6450", ct);
            var settings = await ledger.SettingsAsync(ct);
            var bill = await documents.CreateAsync(DocumentKind.Bill, new SaveDocumentRequest(v.EntityId, vendor, req.Date, null, v.RegistrationNo,
                $"Vehicle {v.RegistrationNo} — maintenance", settings.BaseCurrency, 1, [new DocumentLineInput($"{v.RegistrationNo}: {m.Description}", account, 1, m.Cost, null)]), ct, system: true);
            m.BillId = bill.Id;
        }
        db.MaintenanceRecords.Add(m);
        await db.SaveChangesAsync(ct);
        return (await MaintenanceAsync(v.Id, ct)).First(x => x.Id == m.Id);
    }

    // ---------------- helpers ----------------

    internal async Task<Guid> AccountAsync(string code, CancellationToken ct) =>
        await db.Accounts.Where(a => a.Code == code && !a.IsGroup).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw new ValidationException($"Account {code} is missing from the chart of accounts.");

    private async Task<HashSet<Guid>> LogisticsEntitiesAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        return mine.ByEntity.Where(kv => kv.Value.Any(p => p.StartsWith("logistics."))).Select(kv => kv.Key).ToHashSet();
    }
}
