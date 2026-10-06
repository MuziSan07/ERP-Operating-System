using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Logistics;

/// <summary>
/// Trips (load sheets): Planned (load consignments, weight ≤ capacity) → Dispatched (papers and licence valid, vehicle and
/// driver not already out; consignments in transit) → Completed (arrived at the destination hub). Expenses post to the ledger.
/// </summary>
public class TripService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents,
    ShipmentService shipments, FleetService fleet)
{
    public async Task<List<TripListItem>> ListAsync(TripStatus? status, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ShipmentsView, ct);
        var q = db.Trips.Where(t => visible.Contains(t.EntityId));
        if (status != null) q = q.Where(t => t.Status == status);
        return await ListItemsAsync(db, q.OrderByDescending(t => t.PlannedDate).ThenByDescending(t => t.CreatedAt).Take(200), ct);
    }

    internal static async Task<List<TripListItem>> ListItemsAsync(IAppDbContext db, IQueryable<Trip> q, CancellationToken ct) =>
        await q.Select(t => new TripListItem(t.Id, t.Number, t.Vehicle!.RegistrationNo, t.Driver!.FullName, t.Origin, t.Destination, t.PlannedDate, t.Status,
            db.Shipments.Count(s => s.CurrentTripId == t.Id), db.Shipments.Where(s => s.CurrentTripId == t.Id).Sum(s => s.WeightKg), t.Vehicle.CapacityKg,
            db.Shipments.Where(s => s.CurrentTripId == t.Id).Sum(s => s.Freight + s.FuelSurcharge + s.OtherCharges), t.Expenses.Sum(e => e.Amount))).ToListAsync(ct);

    public async Task<TripDto> GetAsync(Guid id, CancellationToken ct)
    {
        var t = await db.Trips.Include(x => x.Vehicle).Include(x => x.Driver).Include(x => x.Route).Include(x => x.Expenses)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
        await access.EnsureAsync(Permissions.ShipmentsView, t.EntityId, ct);
        var loaded = await db.Shipments.Where(s => s.CurrentTripId == id).ToListAsync(ct);
        var items = await shipments.ListItemsAsync(db.Shipments.Where(s => s.CurrentTripId == id).OrderBy(s => s.Number), ct);
        var accountIds = t.Expenses.Where(e => e.PaidFromAccountId != null).Select(e => e.PaidFromAccountId!.Value).ToList();
        var accounts = await db.Accounts.Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var vendorIds = t.Expenses.Where(e => e.VendorId != null).Select(e => e.VendorId!.Value).ToList();
        var vendors = await db.Contacts.Where(c => vendorIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var freight = loaded.Sum(s => s.Freight + s.FuelSurcharge + s.OtherCharges);
        var expenses = t.Expenses.Sum(e => e.Amount);
        return new TripDto(t.Id, t.Number, t.EntityId, t.VehicleId, t.Vehicle!.RegistrationNo, t.Vehicle.Type, t.Vehicle.CapacityKg, t.DriverId, t.Driver!.FullName,
            t.Driver.Phone, t.RouteId, t.Route?.Code, t.Origin, t.Destination, t.PlannedDate, t.DispatchedAt, t.ArrivedAt, t.OdometerStart, t.OdometerEnd, t.Status, t.Notes,
            items, t.Expenses.OrderBy(e => e.Date).Select(e => new TripExpenseDto(e.Id, e.Type, e.Description, e.Amount,
                e.PaidFromAccountId is { } a ? accounts.GetValueOrDefault(a) : null, e.VendorId is { } v ? vendors.GetValueOrDefault(v) : null, e.BillId, e.Date)).ToList(),
            loaded.Sum(s => s.WeightKg), freight, expenses, freight - expenses);
    }

    public async Task<TripDto> SaveAsync(Guid? id, SaveTripRequest req, CancellationToken ct)
    {
        Trip t;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.ShipmentsDispatch, req.EntityId, ct);
            t = new Trip { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Number = await ledger.NextNumberAsync("TRIP", req.PlannedDate, ct) };
            db.Trips.Add(t);
        }
        else
        {
            t = await db.Trips.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
            await access.EnsureAsync(Permissions.ShipmentsDispatch, t.EntityId, ct);
            if (t.Status != TripStatus.Planned) throw new ValidationException("Only planned trips can be changed.");
        }
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == req.VehicleId, ct) ?? throw new NotFoundException("Vehicle");
        if (vehicle.Status is VehicleStatus.Maintenance or VehicleStatus.Inactive) throw new ValidationException($"{vehicle.RegistrationNo} is {vehicle.Status.ToString().ToLower()}.");
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == req.DriverId && d.IsActive, ct) ?? throw new ValidationException("Choose an active driver.");
        var route = req.RouteId == null ? null : await db.FreightRoutes.FirstOrDefaultAsync(r => r.Id == req.RouteId, ct) ?? throw new NotFoundException("Route");
        if (id != null)
        {
            var load = await db.Shipments.Where(s => s.CurrentTripId == t.Id).SumAsync(s => s.WeightKg, ct);
            if (load > vehicle.CapacityKg) throw new ValidationException($"{vehicle.RegistrationNo} carries {vehicle.CapacityKg:N0} kg; {load:N0} kg is already loaded.");
        }
        t.VehicleId = vehicle.Id;
        t.DriverId = driver.Id;
        t.RouteId = route?.Id;
        t.Origin = Guard.Required(route?.Origin ?? req.Origin, "Origin", 100);
        t.Destination = Guard.Required(route?.Destination ?? req.Destination, "Destination", 100);
        t.PlannedDate = req.PlannedDate;
        t.Notes = req.Notes;
        await db.SaveChangesAsync(ct);
        return await GetAsync(t.Id, ct);
    }

    public async Task<TripDto> LoadAsync(Guid id, TripShipmentsRequest req, CancellationToken ct)
    {
        var t = await db.Trips.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
        await access.EnsureAsync(Permissions.ShipmentsDispatch, t.EntityId, ct);
        if (t.Status != TripStatus.Planned) throw new ValidationException("Consignments can only be loaded on a planned trip.");
        var list = await db.Shipments.Where(s => req.ShipmentIds.Contains(s.Id)).ToListAsync(ct);
        if (list.Count != req.ShipmentIds.Distinct().Count()) throw new NotFoundException("Consignment");
        foreach (var s in list)
        {
            if (s.Status is not (ShipmentStatus.Booked or ShipmentStatus.PickedUp or ShipmentStatus.AtHub)) throw new ValidationException($"{s.Number} is {s.Status} and can't be loaded.");
            if (s.CurrentTripId is { } other && other != t.Id && await db.Trips.AnyAsync(x => x.Id == other && (x.Status == TripStatus.Planned || x.Status == TripStatus.Dispatched), ct))
                throw new ValidationException($"{s.Number} is already on another trip.");
        }
        var current = await db.Shipments.Where(s => s.CurrentTripId == t.Id && !req.ShipmentIds.Contains(s.Id)).SumAsync(s => s.WeightKg, ct);
        var total = current + list.Sum(s => s.WeightKg);
        if (total > t.Vehicle!.CapacityKg) throw new ValidationException($"Overload: {total:N0} kg on a {t.Vehicle.CapacityKg:N0} kg vehicle.");
        foreach (var s in list) s.CurrentTripId = t.Id;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TripDto> UnloadAsync(Guid id, Guid shipmentId, CancellationToken ct)
    {
        var t = await db.Trips.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
        await access.EnsureAsync(Permissions.ShipmentsDispatch, t.EntityId, ct);
        if (t.Status != TripStatus.Planned) throw new ValidationException("The trip has left; unload at the destination hub instead.");
        var s = await db.Shipments.FirstOrDefaultAsync(x => x.Id == shipmentId && x.CurrentTripId == id, ct) ?? throw new NotFoundException("Consignment");
        s.CurrentTripId = null;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TripDto> DispatchAsync(Guid id, DispatchRequest req, CancellationToken ct)
    {
        var t = await db.Trips.Include(x => x.Vehicle).Include(x => x.Driver).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
        await access.EnsureAsync(Permissions.ShipmentsDispatch, t.EntityId, ct);
        if (t.Status != TripStatus.Planned) throw new ValidationException("Only planned trips can be dispatched.");
        var today = FleetService.Today;
        var expired = FleetService.VehicleDocuments(t.Vehicle!).Where(d => d.Expiry != null && d.Expiry < today).Select(d => d.Document).ToList();
        if (expired.Count > 0) throw new ValidationException($"{t.Vehicle!.RegistrationNo} can't leave: {string.Join(", ", expired)} expired.");
        if (t.Driver!.LicenseExpiry is { } lic && lic < today) throw new ValidationException($"{t.Driver.FullName}'s driving licence expired on {lic:dd MMM yyyy}.");
        if (t.Vehicle!.Status is VehicleStatus.Maintenance or VehicleStatus.Inactive) throw new ValidationException($"{t.Vehicle.RegistrationNo} is not available.");
        var busy = await db.Trips.Where(x => x.Id != t.Id && x.Status == TripStatus.Dispatched && (x.VehicleId == t.VehicleId || x.DriverId == t.DriverId)).Select(x => x.Number).FirstOrDefaultAsync(ct);
        if (busy != null) throw new ValidationException($"The vehicle or driver is still out on {busy}.");
        var loaded = await db.Shipments.Where(s => s.CurrentTripId == t.Id).ToListAsync(ct);
        if (loaded.Count == 0) throw new ValidationException("Load at least one consignment before dispatch.");

        t.Status = TripStatus.Dispatched;
        t.DispatchedAt = DateTime.UtcNow;
        t.OdometerStart = req.OdometerStart ?? t.Vehicle.Odometer;
        if (t.OdometerStart < t.Vehicle.Odometer) throw new ValidationException($"Odometer can't be below the last reading ({t.Vehicle.Odometer:N0} km).");
        t.Vehicle.Status = VehicleStatus.OnTrip;
        t.Vehicle.Odometer = t.OdometerStart.Value;
        foreach (var s in loaded)
        {
            s.Status = ShipmentStatus.InTransit;
            db.ShipmentEvents.Add(new ShipmentEvent { ShipmentId = s.Id, Status = ShipmentStatus.InTransit, Location = t.Origin,
                Remarks = $"Departed {t.Origin} on {t.Vehicle.RegistrationNo} ({t.Number}) for {t.Destination}", ByUserId = currentUser.UserId });
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TripDto> ArriveAsync(Guid id, ArriveRequest req, CancellationToken ct)
    {
        var t = await db.Trips.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
        await access.EnsureAsync(Permissions.ShipmentsDispatch, t.EntityId, ct);
        if (t.Status != TripStatus.Dispatched) throw new ValidationException("Only dispatched trips can arrive.");
        if (req.OdometerEnd is { } end && end < (t.OdometerStart ?? 0)) throw new ValidationException("The closing odometer is below the opening reading.");
        t.Status = TripStatus.Completed;
        t.ArrivedAt = DateTime.UtcNow;
        t.OdometerEnd = req.OdometerEnd;
        t.Vehicle!.Status = VehicleStatus.Available;
        if (req.OdometerEnd is { } odo) t.Vehicle.Odometer = odo;
        var location = req.Location ?? t.Destination;
        foreach (var s in await db.Shipments.Where(s => s.CurrentTripId == t.Id && s.Status == ShipmentStatus.InTransit).ToListAsync(ct))
        {
            s.Status = ShipmentStatus.AtHub;
            db.ShipmentEvents.Add(new ShipmentEvent { ShipmentId = s.Id, Status = ShipmentStatus.AtHub, Location = location, Remarks = $"Arrived at {location} hub ({t.Number})", ByUserId = currentUser.UserId });
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TripDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var t = await db.Trips.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
        await access.EnsureAsync(Permissions.ShipmentsDispatch, t.EntityId, ct);
        if (t.Status != TripStatus.Planned) throw new ValidationException("Only planned trips can be cancelled.");
        foreach (var s in await db.Shipments.Where(s => s.CurrentTripId == t.Id).ToListAsync(ct)) s.CurrentTripId = null;
        t.Status = TripStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Paid now from cash/bank (posted immediately) or owed to a vendor (draft bill). Vehicle hire → hire charges; repairs → maintenance.</summary>
    public async Task<TripDto> AddExpenseAsync(Guid id, AddTripExpenseRequest req, CancellationToken ct)
    {
        var t = await db.Trips.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trip");
        await access.EnsureAsync(Permissions.ShipmentsDispatch, t.EntityId, ct);
        if (t.Status == TripStatus.Cancelled) throw new ValidationException("The trip is cancelled.");
        if (req.Amount <= 0) throw new ValidationException("Amount must be positive.");
        if ((req.PaidFromAccountId == null) == (req.VendorId == null)) throw new ValidationException("Choose either the cash/bank account it was paid from, or the vendor it is owed to.");
        var date = req.Date ?? FleetService.Today;
        var account = await fleet.AccountAsync(req.Type switch { TripExpenseType.VehicleHire => "5140", TripExpenseType.Repairs => "6450", _ => "5130" }, ct);
        var text = $"{t.Number} {t.Vehicle!.RegistrationNo} — {req.Type}{(string.IsNullOrWhiteSpace(req.Description) ? "" : $": {req.Description}")}";
        var e = new TripExpense { TripId = t.Id, Type = req.Type, Description = req.Description, Amount = LedgerService.Round(req.Amount), PaidFromAccountId = req.PaidFromAccountId, VendorId = req.VendorId, Date = date };
        var settings = await ledger.SettingsAsync(ct);
        if (req.PaidFromAccountId is { } bank)
        {
            var bankAcc = await db.Accounts.FirstOrDefaultAsync(a => a.Id == bank, ct) ?? throw new NotFoundException("Account");
            if (bankAcc.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Pay from a bank or cash account.");
            var entry = await ledger.BuildAndPostAsync(t.EntityId, date, text, settings.BaseCurrency, 1, JournalSource.Payment, t.Id,
                [new(account, e.Amount, 0, null, text), new(bank, 0, e.Amount, null, text)], t.Number, ct);
            e.JournalEntryId = entry.Id;
        }
        else
        {
            if (!await db.Contacts.AnyAsync(c => c.Id == req.VendorId && c.IsVendor, ct)) throw new ValidationException("Choose a vendor.");
            var bill = await documents.CreateAsync(DocumentKind.Bill, new SaveDocumentRequest(t.EntityId, req.VendorId!.Value, date, null, t.Number, text,
                settings.BaseCurrency, 1, [new DocumentLineInput(text, account, 1, e.Amount, null)]), ct, system: true);
            e.BillId = bill.Id;
        }
        db.TripExpenses.Add(e);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }
}
