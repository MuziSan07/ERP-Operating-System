using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Logistics;

/// <summary>
/// Consignment notes and the money around them:
///   Prepaid → invoiced and paid at booking. To-pay → invoiced and collected at delivery. Account → monthly consolidated invoice.
///   COD (goods value) collected at delivery: Dr Cash / Cr COD payable to shipper; remittance: Dr COD payable / Cr Bank.
/// </summary>
public class ShipmentService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents,
    PaymentService payments, FleetService fleet)
{
    private static readonly ShipmentStatus[] Closed = [ShipmentStatus.Delivered, ShipmentStatus.Returned, ShipmentStatus.Cancelled];

    public async Task<QuoteDto> QuoteAsync(QuoteRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.ShipmentsCreate, ct);
        var route = req.RouteId == null ? null : await db.FreightRoutes.FirstOrDefaultAsync(r => r.Id == req.RouteId, ct) ?? throw new NotFoundException("Route");
        return await PriceAsync(route, req.Service, req.WeightKg, null, req.OtherCharges, ct);
    }

    private async Task<QuoteDto> PriceAsync(FreightRoute? route, ServiceLevel service, decimal weight, decimal? freightOverride, decimal other, CancellationToken ct)
    {
        decimal freight; string basis;
        if (freightOverride is { } fo) { freight = fo; basis = "Negotiated freight"; }
        else if (route == null) throw new ValidationException("Choose a route or enter the agreed freight.");
        else if (service == ServiceLevel.FullTruck) { freight = route.FullTruckRate; basis = $"Full truck {route.Origin}–{route.Destination}"; }
        else
        {
            var byWeight = weight * route.RatePerKg * (service == ServiceLevel.Express ? 1.5m : 1m);
            freight = Math.Max(route.MinimumCharge, byWeight);
            basis = byWeight >= route.MinimumCharge ? $"{weight:0.##} kg × {route.RatePerKg:0.##}{(service == ServiceLevel.Express ? " × 1.5 express" : "")}" : "Minimum charge";
        }
        freight = LedgerService.Round(freight);
        var fuel = freightOverride == null && route != null ? LedgerService.Round(freight * route.FuelSurchargePercent / 100m) : 0;
        var sub = freight + fuel + other;
        var rate = route?.TaxRateId == null ? 0 : await db.TaxRates.Where(t => t.Id == route.TaxRateId).Select(t => t.Rate).FirstAsync(ct);
        var tax = LedgerService.Round(sub * rate);
        return new QuoteDto(freight, fuel, other, sub, tax, sub + tax, basis);
    }

    // ======================= Booking =======================

    public async Task<PagedResult<ShipmentListItem>> ListAsync(ShipmentStatus? status, Guid? customerId, string? search, bool openOnly, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ShipmentsView, ct);
        var q = db.Shipments.Where(s => visible.Contains(s.EntityId));
        if (status != null) q = q.Where(s => s.Status == status);
        if (customerId != null) q = q.Where(s => s.CustomerId == customerId);
        if (openOnly) q = q.Where(s => !Closed.Contains(s.Status));
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(s => s.Number.Contains(search) || s.ConsigneeName.Contains(search) || s.ShipperName.Contains(search) || (s.ConsigneePhone != null && s.ConsigneePhone.Contains(search)));
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var total = await q.CountAsync(ct);
        var items = await ListItemsAsync(q.OrderByDescending(s => s.BookingDate).ThenByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new PagedResult<ShipmentListItem>(items, total, page, pageSize);
    }

    internal async Task<List<ShipmentListItem>> ListItemsAsync(IQueryable<Shipment> q, CancellationToken ct)
    {
        var today = FleetService.Today;
        var rows = await q.Select(s => new { s, Customer = s.Customer!.Name, Trip = db.Trips.Where(t => t.Id == s.CurrentTripId).Select(t => t.Number).FirstOrDefault() }).ToListAsync(ct);
        return rows.Select(r => new ShipmentListItem(r.s.Id, r.s.Number, r.s.BookingDate, r.Customer, r.s.ConsigneeName, r.s.OriginCity, r.s.DestinationCity, r.s.Pieces,
            r.s.WeightKg, r.s.PaymentMode, r.s.Total, r.s.CodAmount, r.s.Status, r.Trip,
            r.s.PromisedDate is { } p && !Closed.Contains(r.s.Status) && p < today, r.s.InvoiceId != null)).ToList();
    }

    public async Task<ShipmentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await db.Shipments.Include(x => x.Entity).Include(x => x.Customer).Include(x => x.Route).Include(x => x.Events)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Consignment");
        await access.EnsureAsync(Permissions.ShipmentsView, s.EntityId, ct);
        return await ToDtoAsync(s, ct);
    }

    /// <summary>Find a consignment by its CN number (tracking desk).</summary>
    public async Task<ShipmentDto> TrackAsync(string number, CancellationToken ct)
    {
        var s = await db.Shipments.Where(x => x.Number == number.Trim().ToUpper()).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Consignment");
        return await GetAsync(s, ct);
    }

    public async Task<ShipmentDto> SaveAsync(Guid? id, SaveShipmentRequest req, CancellationToken ct)
    {
        Shipment s;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.ShipmentsCreate, req.EntityId, ct);
            s = new Shipment { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Number = await ledger.NextNumberAsync("CN", req.BookingDate, ct) };
            db.Shipments.Add(s);
            s.Events.Add(new ShipmentEvent { ShipmentId = s.Id, Status = ShipmentStatus.Booked, Location = req.OriginCity, Remarks = "Consignment booked", ByUserId = currentUser.UserId });
        }
        else
        {
            s = await db.Shipments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Consignment");
            await access.EnsureAsync(Permissions.ShipmentsEdit, s.EntityId, ct);
            if (s.Status != ShipmentStatus.Booked || s.CurrentTripId != null) throw new ValidationException("Only consignments not yet loaded can be edited.");
            if (s.InvoiceId != null) throw new ValidationException("This consignment is already invoiced.");
            if (req.PaymentMode == PaymentMode.Prepaid) throw new ValidationException("Changing to prepaid after booking isn't supported; cancel and rebook.");
        }
        var customer = await db.Contacts.FirstOrDefaultAsync(c => c.Id == req.CustomerId && c.IsCustomer, ct) ?? throw new ValidationException("Choose the bill-to customer.");
        var route = req.RouteId == null ? null : await db.FreightRoutes.FirstOrDefaultAsync(r => r.Id == req.RouteId && r.IsActive, ct) ?? throw new NotFoundException("Route");
        if (req.WeightKg <= 0 || req.Pieces < 1) throw new ValidationException("Enter pieces and weight.");
        if (req.CodAmount < 0 || req.OtherCharges < 0 || req.DeclaredValue < 0) throw new ValidationException("Amounts can't be negative.");
        var price = await PriceAsync(route, req.Service, req.WeightKg, req.FreightOverride, req.OtherCharges, ct);

        s.BookingDate = req.BookingDate;
        s.CustomerId = customer.Id;
        s.ShipperName = Guard.Required(req.ShipperName, "Shipper", 150);
        s.ShipperPhone = req.ShipperPhone;
        s.ShipperAddress = req.ShipperAddress;
        s.ConsigneeName = Guard.Required(req.ConsigneeName, "Consignee", 150);
        s.ConsigneePhone = req.ConsigneePhone;
        s.ConsigneeAddress = req.ConsigneeAddress;
        s.OriginCity = Guard.Required(route?.Origin ?? req.OriginCity, "Origin", 100);
        s.DestinationCity = Guard.Required(route?.Destination ?? req.DestinationCity, "Destination", 100);
        s.RouteId = route?.Id;
        s.Service = req.Service;
        s.GoodsDescription = req.GoodsDescription;
        s.Pieces = req.Pieces;
        s.WeightKg = req.WeightKg;
        s.VolumeCbm = req.VolumeCbm;
        s.DeclaredValue = req.DeclaredValue;
        s.PaymentMode = req.PaymentMode;
        s.Freight = price.Freight;
        s.FuelSurcharge = price.FuelSurcharge;
        s.OtherCharges = price.OtherCharges;
        s.TaxRateId = route?.TaxRateId;
        s.TaxAmount = price.TaxAmount;
        s.Total = price.Total;
        s.CodAmount = LedgerService.Round(req.CodAmount);
        s.PromisedDate = req.PromisedDate ?? (route == null ? null : req.BookingDate.AddDays((int)Math.Ceiling((double)Math.Max(route.StandardHours, 1) / 24) + 1));
        await db.SaveChangesAsync(ct);

        if (id == null && req.PaymentMode == PaymentMode.Prepaid)
        {
            var bank = req.PaidIntoAccountId ?? throw new ValidationException("Prepaid freight: choose the cash or bank account it was paid into.");
            s.InvoiceId = await InvoiceAndCollectAsync(s, bank, s.BookingDate, ct);
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(s.Id, ct);
    }

    public async Task<ShipmentDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var s = await db.Shipments.Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Consignment");
        await access.EnsureAsync(Permissions.ShipmentsEdit, s.EntityId, ct);
        if (s.Status != ShipmentStatus.Booked || s.CurrentTripId != null) throw new ValidationException("Only consignments not yet loaded can be cancelled.");
        if (s.InvoiceId != null) throw new ValidationException("This consignment is invoiced; void the invoice in Finance first.");
        s.Status = ShipmentStatus.Cancelled;
        s.Events.Add(new ShipmentEvent { ShipmentId = s.Id, Status = ShipmentStatus.Cancelled, Remarks = "Cancelled", ByUserId = currentUser.UserId });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Manual tracking update (picked up, at hub, out for delivery, returned).</summary>
    public async Task<ShipmentDto> UpdateStatusAsync(Guid id, TrackRequest req, CancellationToken ct)
    {
        var s = await db.Shipments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Consignment");
        await access.EnsureAsync(Permissions.ShipmentsEdit, s.EntityId, ct);
        if (Closed.Contains(s.Status)) throw new ValidationException("This consignment is closed.");
        if (req.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled or ShipmentStatus.InTransit or ShipmentStatus.Booked)
            throw new ValidationException("Use Deliver, Cancel or trip dispatch for that status.");
        if (req.Status == ShipmentStatus.Returned && s.CodCollected > 0) throw new ValidationException("COD was already collected.");
        s.Status = req.Status;
        db.ShipmentEvents.Add(new ShipmentEvent { ShipmentId = s.Id, Status = req.Status, Location = req.Location, Remarks = req.Remarks, ByUserId = currentUser.UserId });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Proof of delivery; collects to-pay freight and COD into a cash/bank account.</summary>
    public async Task<ShipmentDto> DeliverAsync(Guid id, DeliverRequest req, CancellationToken ct)
    {
        var s = await db.Shipments.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Consignment");
        await access.EnsureAsync(Permissions.ShipmentsEdit, s.EntityId, ct);
        if (Closed.Contains(s.Status)) throw new ValidationException("This consignment is already closed.");
        if (s.Status == ShipmentStatus.Booked) throw new ValidationException("Load and dispatch the consignment before delivering it.");
        if (s.CurrentTripId is { } tid && await db.Trips.AnyAsync(t => t.Id == tid && t.Status == TripStatus.Dispatched, ct))
            throw new ValidationException("The vehicle hasn't arrived yet; mark the trip as arrived first.");
        var cod = req.CodCollected ?? 0;
        if (s.CodAmount > 0 && cod != s.CodAmount) throw new ValidationException($"Collect the full COD amount of {s.CodAmount:N0} (or mark the consignment returned).");
        var needsCash = cod > 0 || (s.PaymentMode == PaymentMode.ToPay && s.InvoiceId == null);
        if (needsCash && req.CollectedIntoAccountId == null) throw new ValidationException("Choose the cash account the rider/driver collected into.");
        var date = FleetService.Today;

        if (s.PaymentMode == PaymentMode.ToPay && s.InvoiceId == null)
            s.InvoiceId = await InvoiceAndCollectAsync(s, req.CollectedIntoAccountId!.Value, date, ct);
        if (cod > 0)
        {
            var codAccount = await fleet.AccountAsync("2210", ct);
            var settings = await ledger.SettingsAsync(ct);
            await ledger.BuildAndPostAsync(s.EntityId, date, $"COD collected — {s.Number} for {s.Customer!.Name}", settings.BaseCurrency, 1, JournalSource.Payment, s.Id,
                [new(req.CollectedIntoAccountId!.Value, cod, 0, null, s.Number, s.CustomerId), new(codAccount, 0, cod, null, s.Number, s.CustomerId)], s.Number, ct);
            s.CodCollected = cod;
        }
        s.Status = ShipmentStatus.Delivered;
        s.DeliveredAt = DateTime.UtcNow;
        s.ReceivedBy = Guard.Required(req.ReceivedBy, "Received by", 150);
        s.DeliveryRemarks = req.Remarks;
        db.ShipmentEvents.Add(new ShipmentEvent { ShipmentId = s.Id, Status = ShipmentStatus.Delivered, Location = s.DestinationCity,
            Remarks = $"Delivered — received by {s.ReceivedBy}{(cod > 0 ? $", COD {cod:N0} collected" : "")}", ByUserId = currentUser.UserId });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Pays a shipper all COD collected on their delivered consignments.</summary>
    public async Task<CodRemitResult> RemitCodAsync(CodRemitRequest req, CancellationToken ct)
    {
        var entities = await access.EntitiesWithAsync(Permissions.CodRemit, ct);
        if (entities.Count == 0) throw new ForbiddenException("Missing permission 'logistics.cod.remit'.");
        var shipments = await db.Shipments.Where(s => s.CustomerId == req.CustomerId && entities.Contains(s.EntityId) && s.CodCollected > 0 && !s.CodRemitted).ToListAsync(ct);
        if (shipments.Count == 0) throw new ValidationException("No collected COD is waiting to be remitted to this customer.");
        var amount = shipments.Sum(s => s.CodCollected);
        var codAccount = await fleet.AccountAsync("2210", ct);
        var settings = await ledger.SettingsAsync(ct);
        var customer = await db.Contacts.FirstAsync(c => c.Id == req.CustomerId, ct);
        var entry = await ledger.BuildAndPostAsync(shipments[0].EntityId, req.Date ?? FleetService.Today, $"COD remittance to {customer.Name} ({shipments.Count} CNs)",
            settings.BaseCurrency, 1, JournalSource.Payment, req.CustomerId,
            [new(codAccount, amount, 0, null, string.Join(", ", shipments.Select(s => s.Number).Take(10)), req.CustomerId), new(req.BankAccountId, 0, amount, null, req.Reference, req.CustomerId)],
            req.Reference ?? "COD remittance", ct);
        foreach (var s in shipments) s.CodRemitted = true;
        await db.SaveChangesAsync(ct);
        return new CodRemitResult(shipments.Count, amount, entry.Id);
    }

    /// <summary>One consolidated invoice for an account customer's uninvoiced consignments.</summary>
    public async Task<BillCustomerResult> BillCustomerAsync(BillCustomerRequest req, CancellationToken ct)
    {
        var entities = await access.EntitiesWithAsync(Permissions.ShipmentsEdit, ct);
        var upTo = req.UpTo ?? FleetService.Today;
        var shipments = await db.Shipments.Where(s => s.CustomerId == req.CustomerId && entities.Contains(s.EntityId) && s.PaymentMode == PaymentMode.Account
                                                      && s.InvoiceId == null && s.Status != ShipmentStatus.Cancelled && s.BookingDate <= upTo)
            .OrderBy(s => s.BookingDate).ThenBy(s => s.Number).ToListAsync(ct);
        if (shipments.Count == 0) throw new ValidationException("No uninvoiced account consignments for this customer.");
        var settings = await ledger.SettingsAsync(ct);
        var revenue = await fleet.AccountAsync("4120", ct);
        var draft = await documents.CreateAsync(DocumentKind.Invoice, new SaveDocumentRequest(shipments[0].EntityId, req.CustomerId, FleetService.Today, null,
            $"Freight to {upTo:dd MMM yyyy}", $"{shipments.Count} consignment(s) {shipments[0].BookingDate:dd MMM}–{shipments[^1].BookingDate:dd MMM yyyy}", settings.BaseCurrency, 1,
            shipments.Select(s => Line(s, revenue)).ToList()), ct, system: true);
        var invoice = await documents.ApproveAsync(draft.Id, ct, system: true);
        foreach (var s in shipments) s.InvoiceId = invoice.Id;
        await db.SaveChangesAsync(ct);
        return new BillCustomerResult(invoice.Id, invoice.Number, shipments.Count, invoice.Total);
    }

    private static DocumentLineInput Line(Shipment s, Guid revenue) =>
        new($"CN {s.Number} · {s.BookingDate:dd MMM} · {s.OriginCity}→{s.DestinationCity} · {s.Pieces} pcs / {s.WeightKg:0.##} kg", revenue, 1,
            s.Freight + s.FuelSurcharge + s.OtherCharges, s.TaxRateId);

    private async Task<Guid> InvoiceAndCollectAsync(Shipment s, Guid bankAccountId, DateOnly date, CancellationToken ct)
    {
        var settings = await ledger.SettingsAsync(ct);
        var revenue = await fleet.AccountAsync("4120", ct);
        var draft = await documents.CreateAsync(DocumentKind.Invoice, new SaveDocumentRequest(s.EntityId, s.CustomerId, date, date, s.Number,
            $"Freight {s.PaymentMode.ToString().ToLower()} — CN {s.Number}", settings.BaseCurrency, 1, [Line(s, revenue)]), ct, system: true);
        var invoice = await documents.ApproveAsync(draft.Id, ct, system: true);
        await payments.CreateAsync(new CreatePaymentRequest(PaymentKind.Receipt, s.EntityId, s.CustomerId, date, bankAccountId, settings.BaseCurrency, 1,
            invoice.Total, s.Number, $"Freight collected — CN {s.Number}", [new AllocationInput(invoice.Id, invoice.Total)]), ct, system: true);
        return invoice.Id;
    }

    // ======================= Dashboard =======================

    public async Task<LogisticsDashboardDto> DashboardAsync(CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ShipmentsView, ct);
        var fleetVisible = await access.EntitiesWithAsync(Permissions.FleetView, ct);
        if (visible.Count == 0 && fleetVisible.Count == 0) throw new ForbiddenException();
        var today = FleetService.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var q = db.Shipments.Where(s => visible.Contains(s.EntityId));
        var byStatus = await q.Where(s => s.BookingDate >= today.AddDays(-90) && s.Status != ShipmentStatus.Cancelled)
            .GroupBy(s => s.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.N, ct);
        var bookedToday = await q.CountAsync(s => s.BookingDate == today && s.Status != ShipmentStatus.Cancelled, ct);
        var startUtc = today.ToDateTime(TimeOnly.MinValue).AddHours(-5);
        var deliveredToday = await q.CountAsync(s => s.DeliveredAt >= startUtc, ct);
        var deliveredMonth = await q.Where(s => s.Status == ShipmentStatus.Delivered && s.DeliveredAt >= monthStart.ToDateTime(TimeOnly.MinValue))
            .Select(s => new { s.PromisedDate, s.DeliveredAt }).ToListAsync(ct);
        var onTime = deliveredMonth.Count == 0 ? 100 : Math.Round(deliveredMonth.Count(d => d.PromisedDate == null || DateOnly.FromDateTime(d.DeliveredAt!.Value.AddHours(5)) <= d.PromisedDate) * 100m / deliveredMonth.Count, 1);
        var revenue = await q.Where(s => s.BookingDate >= monthStart && s.Status != ShipmentStatus.Cancelled).SumAsync(s => s.Freight + s.FuelSurcharge + s.OtherCharges, ct);
        var codPending = await q.Where(s => s.CodCollected > 0 && !s.CodRemitted).SumAsync(s => s.CodCollected, ct);

        var vehicles = await db.Vehicles.Where(v => fleetVisible.Contains(v.EntityId)).ToListAsync(ct);
        var drivers = await db.Drivers.Where(d => fleetVisible.Contains(d.EntityId) && d.IsActive).ToListAsync(ct);
        var alerts = vehicles.SelectMany(v => FleetService.VehicleDocuments(v).Where(d => d.Expiry != null && d.Expiry <= today.AddDays(30))
                .Select(d => new ExpiryAlert("Vehicle", v.RegistrationNo, d.Document, d.Expiry, d.Expiry!.Value.DayNumber - today.DayNumber)))
            .Concat(drivers.Where(d => d.LicenseExpiry != null && d.LicenseExpiry <= today.AddDays(30))
                .Select(d => new ExpiryAlert("Driver", d.FullName, "Driving licence", d.LicenseExpiry, d.LicenseExpiry!.Value.DayNumber - today.DayNumber)))
            .OrderBy(a => a.DaysLeft).ToList();
        var activeTrips = await TripService.ListItemsAsync(db, db.Trips.Where(t => fleetVisible.Contains(t.EntityId) && (t.Status == TripStatus.Planned || t.Status == TripStatus.Dispatched)).OrderBy(t => t.PlannedDate), ct);
        return new LogisticsDashboardDto(byStatus, bookedToday, deliveredToday, onTime, revenue, codPending,
            vehicles.GroupBy(v => v.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()), alerts, activeTrips);
    }

    private async Task<ShipmentDto> ToDtoAsync(Shipment s, CancellationToken ct)
    {
        var invoiceNo = s.InvoiceId == null ? null : await db.FinanceDocuments.Where(d => d.Id == s.InvoiceId).Select(d => d.Number).FirstOrDefaultAsync(ct);
        var trip = s.CurrentTripId == null ? null : await db.Trips.Where(t => t.Id == s.CurrentTripId).Select(t => t.Number).FirstOrDefaultAsync(ct);
        var userIds = s.Events.Where(e => e.ByUserId != null).Select(e => e.ByUserId!.Value).Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return new ShipmentDto(s.Id, s.Number, s.EntityId, s.Entity!.Name, s.BookingDate, s.CustomerId, s.Customer!.Name, s.ShipperName, s.ShipperPhone, s.ShipperAddress,
            s.ConsigneeName, s.ConsigneePhone, s.ConsigneeAddress, s.OriginCity, s.DestinationCity, s.RouteId, s.Route?.Code, s.Service, s.GoodsDescription, s.Pieces,
            s.WeightKg, s.VolumeCbm, s.DeclaredValue, s.PaymentMode, s.Freight, s.FuelSurcharge, s.OtherCharges, s.TaxAmount, s.Total, s.CodAmount, s.CodCollected,
            s.CodRemitted, s.Status, s.PromisedDate, s.DeliveredAt, s.ReceivedBy, s.DeliveryRemarks, s.InvoiceId, invoiceNo, trip,
            s.Events.OrderBy(e => e.At).Select(e => new ShipmentEventDto(e.At, e.Status, e.Location, e.Remarks, e.ByUserId is { } u ? users.GetValueOrDefault(u) : null)).ToList());
    }
}
