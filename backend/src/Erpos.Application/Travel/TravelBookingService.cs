using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Travel;

/// <summary>
/// Customer travel files. Quotation → Confirmed (tour seats are held, capacity enforced) → Invoiced.
/// Deposits are customer advances applied to the invoice; supplier costs become draft bills (cost of sales) per supplier.
/// </summary>
public class TravelBookingService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents,
    AdvanceService advances, TourService tours)
{
    public async Task<PagedResult<BookingListItem>> ListAsync(TravelBookingStatus? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.BookingsView, ct);
        var q = db.TravelBookings.Where(b => visible.Contains(b.EntityId));
        if (status != null) q = q.Where(b => b.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(b => b.Number.Contains(search) || b.Customer!.Name.Contains(search) || b.Passengers.Any(p => p.FullName.Contains(search) || (p.PassportNo != null && p.PassportNo.Contains(search))));
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await ListItemsAsync(q.OrderByDescending(b => b.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new PagedResult<BookingListItem>(items, total, page, pageSize);
    }

    private async Task<List<BookingListItem>> ListItemsAsync(IQueryable<TravelBooking> q, CancellationToken ct)
    {
        var rows = await q.Include(b => b.Customer).Include(b => b.Passengers).Include(b => b.Items).ThenInclude(i => i.TourDeparture).ToListAsync(ct);
        return rows.Select(b => new BookingListItem(b.Id, b.Number, b.Customer!.Name, b.TravelDate, b.Passengers.Count,
            string.Join(", ", b.Items.Select(i => i.Type.ToString()).Distinct()), b.Status, b.Items.Sum(i => i.Quantity * i.UnitPrice),
            b.Items.Sum(i => i.Quantity * (i.UnitPrice - i.UnitCost)), Warnings(b).Count)).ToList();
    }

    public async Task<BookingDto> GetAsync(Guid id, CancellationToken ct)
    {
        var b = await LoadAsync(id, ct);
        await access.EnsureAsync(Permissions.BookingsView, b.EntityId, ct);
        return await ToDtoAsync(b, ct);
    }

    public async Task<BookingDto> SaveAsync(Guid? id, SaveBookingRequest req, CancellationToken ct)
    {
        TravelBooking b;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.BookingsCreate, req.EntityId, ct);
            b = new TravelBooking { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Number = await ledger.NextNumberAsync("TRV", DateOnly.FromDateTime(DateTime.UtcNow), ct) };
            db.TravelBookings.Add(b);
        }
        else
        {
            b = await db.TravelBookings.Include(x => x.Passengers).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");
            await access.EnsureAsync(Permissions.BookingsEdit, b.EntityId, ct);
            if (b.Status is TravelBookingStatus.Invoiced or TravelBookingStatus.Cancelled) throw new ValidationException("Invoiced or cancelled bookings can't be edited.");
            if (req.EntityId != b.EntityId) throw new ValidationException("A booking can't move to another branch.");
            db.BookingItems.RemoveRange(b.Items);
            b.Items.Clear();
        }
        var customer = await db.Contacts.FirstOrDefaultAsync(c => c.Id == req.CustomerId && c.IsCustomer, ct) ?? throw new ValidationException("Choose a customer.");
        if (req.Passengers.Count == 0) throw new ValidationException("Add at least one passenger.");
        if (req.Items.Count == 0) throw new ValidationException("Add at least one service.");

        b.CustomerId = customer.Id;
        b.ContactPhone = req.ContactPhone ?? customer.Phone;
        b.Notes = req.Notes;

        // Passengers: update in place so visa items keep pointing at the same person.
        var keep = req.Passengers.Where(p => p.Id != null).Select(p => p.Id!.Value).ToHashSet();
        foreach (var gone in b.Passengers.Where(p => !keep.Contains(p.Id)).ToList()) { db.BookingPassengers.Remove(gone); b.Passengers.Remove(gone); }
        var passengers = new List<BookingPassenger>();
        foreach (var p in req.Passengers)
        {
            var pax = p.Id is { } pid ? b.Passengers.FirstOrDefault(x => x.Id == pid) ?? throw new NotFoundException("Passenger") : null;
            if (pax == null) { pax = new BookingPassenger { TravelBookingId = b.Id }; b.Passengers.Add(pax); }
            pax.FullName = Guard.Required(p.FullName, "Passenger name", 150);
            pax.Type = p.Type;
            pax.PassportNo = string.IsNullOrWhiteSpace(p.PassportNo) ? null : p.PassportNo.Trim().ToUpperInvariant();
            pax.PassportExpiry = p.PassportExpiry;
            pax.Nationality = p.Nationality;
            pax.Cnic = p.Cnic;
            pax.DateOfBirth = p.DateOfBirth;
            pax.Phone = p.Phone;
            passengers.Add(pax);
        }

        var departureIds = req.Items.Where(i => i.TourDepartureId != null).Select(i => i.TourDepartureId!.Value).Distinct().ToList();
        var departures = await db.TourDepartures.Include(d => d.TourPackage).Where(d => departureIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        foreach (var i in req.Items)
        {
            if (i.SupplierId is { } s && !await db.Contacts.AnyAsync(c => c.Id == s && c.IsVendor, ct)) throw new ValidationException("Suppliers must be set up as vendors.");
            if (i.UnitCost < 0 || i.UnitPrice < 0) throw new ValidationException("Costs and prices can't be negative.");
            var item = new BookingItem
            {
                TravelBookingId = b.Id, Type = i.Type, ServiceDate = i.ServiceDate, SupplierId = i.SupplierId, Quantity = i.Quantity, UnitCost = i.UnitCost,
                UnitPrice = i.UnitPrice, TaxRateId = i.TaxRateId, IncomeAccountId = i.IncomeAccountId, Airline = i.Airline, Pnr = i.Pnr?.ToUpperInvariant(),
                TicketNumber = i.TicketNumber, Route = i.Route?.ToUpperInvariant(), Country = i.Country, VisaStatus = i.Type == BookingItemType.Visa ? i.VisaStatus ?? VisaStatus.NotStarted : null,
                PassengerId = i.PassengerIndex is { } pi && pi >= 0 && pi < passengers.Count ? passengers[pi].Id : null
            };
            if (i.Type == BookingItemType.TourSeats)
            {
                var d = i.TourDepartureId is { } did && departures.TryGetValue(did, out var dep) ? dep : throw new ValidationException("Choose the tour departure.");
                if (d.Status != DepartureStatus.Open) throw new ValidationException($"{d.Code} is not open for sale.");
                if (i.Adults < 0 || i.Children < 0 || i.Adults + i.Children == 0) throw new ValidationException("Enter the number of travellers on the tour.");
                item.TourDepartureId = d.Id;
                item.Adults = i.Adults;
                item.Children = i.Children;
                item.ServiceDate = d.StartDate;
                // Price per traveller: the line carries the blended total so cost/margin stay per booking line.
                var sell = i.Adults * d.AdultPrice + i.Children * d.ChildPrice;
                item.Quantity = 1;
                item.UnitPrice = i.UnitPrice > 0 ? i.UnitPrice : sell;
                item.TaxRateId ??= d.TourPackage!.TaxRateId;
                item.IncomeAccountId ??= d.TourPackage.IncomeAccountId;
                item.Description = string.IsNullOrWhiteSpace(i.Description) ? $"{d.TourPackage.Name} — {d.StartDate:dd MMM yyyy} ({i.Adults} adult{(i.Adults == 1 ? "" : "s")}{(i.Children > 0 ? $", {i.Children} child" : "")})" : i.Description.Trim();
            }
            else
            {
                if (i.Quantity <= 0) throw new ValidationException("Quantity must be positive.");
                item.Description = string.IsNullOrWhiteSpace(i.Description)
                    ? i.Type switch
                    {
                        BookingItemType.Flight => $"Air ticket {i.Airline} {i.Route}".Trim(),
                        BookingItemType.Visa => $"Visa — {i.Country}",
                        _ => throw new ValidationException("Describe the service.")
                    }
                    : i.Description.Trim();
            }
            b.Items.Add(item);
        }
        b.TravelDate = req.TravelDate ?? b.Items.Where(i => i.ServiceDate != null).Select(i => i.ServiceDate).Min();
        if (b.Status == TravelBookingStatus.Confirmed) await EnsureSeatsAsync(b, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(b.Id, ct);
    }

    private async Task EnsureSeatsAsync(TravelBooking b, CancellationToken ct)
    {
        foreach (var g in b.Items.Where(i => i.TourDepartureId != null).GroupBy(i => i.TourDepartureId!.Value))
        {
            var d = await db.TourDepartures.FirstAsync(x => x.Id == g.Key, ct);
            var others = (await tours.BookedSeatsAsync([g.Key], b.Id, ct)).GetValueOrDefault(g.Key);
            var wanted = g.Sum(i => i.Adults + i.Children);
            if (others + wanted > d.Capacity) throw new ValidationException($"{d.Code} has only {Math.Max(0, d.Capacity - others)} seat(s) left; {wanted} requested.");
        }
    }

    public async Task<BookingDto> ConfirmAsync(Guid id, CancellationToken ct)
    {
        var b = await db.TravelBookings.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");
        await access.EnsureAsync(Permissions.BookingsEdit, b.EntityId, ct);
        if (b.Status != TravelBookingStatus.Quotation) throw new ValidationException("Only quotations can be confirmed.");
        await EnsureSeatsAsync(b, ct);
        b.Status = TravelBookingStatus.Confirmed;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<BookingDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var b = await db.TravelBookings.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");
        await access.EnsureAsync(Permissions.BookingsCancel, b.EntityId, ct);
        if (b.Status == TravelBookingStatus.Invoiced) throw new ValidationException("This booking is invoiced. Void the invoice in Finance (or issue a credit) first.");
        if (b.Status == TravelBookingStatus.Cancelled) throw new ValidationException("Already cancelled.");
        b.Status = TravelBookingStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<BookingDto> UpdateVisaAsync(Guid id, Guid itemId, VisaUpdateRequest req, CancellationToken ct)
    {
        var b = await db.TravelBookings.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");
        await access.EnsureAsync(Permissions.VisasEdit, b.EntityId, ct);
        var item = b.Items.FirstOrDefault(i => i.Id == itemId && i.Type == BookingItemType.Visa) ?? throw new NotFoundException("Visa item");
        item.VisaStatus = req.Status;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<BookingDto> DepositAsync(Guid id, BookingDepositRequest req, CancellationToken ct)
    {
        var b = await db.TravelBookings.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");
        await access.EnsureAsync(Permissions.BookingsEdit, b.EntityId, ct);
        if (b.Status is TravelBookingStatus.Invoiced or TravelBookingStatus.Cancelled) throw new ValidationException("Record payments against the invoice in Finance.");
        var date = req.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dep = new BookingDeposit { TenantId = b.TenantId, TravelBookingId = b.Id, Date = date, Amount = LedgerService.Round(req.Amount), BankAccountId = req.BankAccountId, Reference = req.Reference };
        var entry = await advances.ReceiveAsync(b.EntityId, b.CustomerId, req.BankAccountId, dep.Amount, date, $"Advance — {b.Number} {b.Customer!.Name}", req.Reference ?? b.Number, dep.Id, ct);
        dep.JournalEntryId = entry.Id;
        db.BookingDeposits.Add(dep);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Issues and approves the sales invoice for the booking and applies any advances.</summary>
    public async Task<BookingDto> InvoiceAsync(Guid id, CancellationToken ct)
    {
        var b = await db.TravelBookings.Include(x => x.Items).Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");
        await access.EnsureAsync(Permissions.BookingsEdit, b.EntityId, ct);
        if (b.Status != TravelBookingStatus.Confirmed) throw new ValidationException("Confirm the booking before invoicing.");
        var settings = await ledger.SettingsAsync(ct);
        var income = await db.Accounts.Where(a => a.Code == "4100" && !a.IsGroup).Select(a => a.Id).FirstAsync(ct);
        var lines = b.Items.Select(i => new DocumentLineInput(i.Description + (i.Pnr != null ? $" · PNR {i.Pnr}" : "") + (i.TicketNumber != null ? $" · Tkt {i.TicketNumber}" : ""),
            i.IncomeAccountId ?? income, i.Quantity, i.UnitPrice, i.TaxRateId)).ToList();
        var draft = await documents.CreateAsync(DocumentKind.Invoice, new SaveDocumentRequest(b.EntityId, b.CustomerId, DateOnly.FromDateTime(DateTime.UtcNow), null,
            b.Number, $"Travel file {b.Number}" + (b.TravelDate != null ? $", travel {b.TravelDate:dd MMM yyyy}" : ""), settings.BaseCurrency, 1, lines), ct, system: true);
        var invoice = await documents.ApproveAsync(draft.Id, ct, system: true);
        b.InvoiceId = invoice.Id;
        b.Status = TravelBookingStatus.Invoiced;
        await db.SaveChangesAsync(ct);

        var deposits = await db.BookingDeposits.Where(d => d.TravelBookingId == b.Id && !d.Applied).ToListAsync(ct);
        if (deposits.Count > 0)
        {
            var doc = await db.FinanceDocuments.FirstAsync(d => d.Id == invoice.Id, ct);
            var apply = Math.Min(deposits.Sum(d => d.Amount), doc.Total);
            await advances.ApplyAsync(doc, apply, doc.Date, $"Advances applied — {b.Number}", ct);
            foreach (var d in deposits) d.Applied = true;
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    /// <summary>One draft cost-of-sales bill per supplier for items not yet billed. Accounts approve them.</summary>
    public async Task<SupplierBillsResult> SupplierBillsAsync(Guid id, CancellationToken ct)
    {
        var b = await db.TravelBookings.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");
        await access.EnsureAsync(Permissions.BookingsEdit, b.EntityId, ct);
        if (b.Status is not (TravelBookingStatus.Confirmed or TravelBookingStatus.Invoiced)) throw new ValidationException("Confirm the booking first.");
        var pending = b.Items.Where(i => i.BillId == null && i.SupplierId != null && i.UnitCost > 0).ToList();
        if (pending.Count == 0) throw new ValidationException("No supplier costs left to bill.");
        var settings = await ledger.SettingsAsync(ct);
        var cos = await db.Accounts.Where(a => a.Code == "5100" && !a.IsGroup).Select(a => a.Id).FirstAsync(ct);
        var billIds = new List<Guid>();
        foreach (var g in pending.GroupBy(i => i.SupplierId!.Value))
        {
            var bill = await documents.CreateAsync(DocumentKind.Bill, new SaveDocumentRequest(b.EntityId, g.Key, DateOnly.FromDateTime(DateTime.UtcNow), null,
                b.Number, $"Supplier cost for travel file {b.Number}", settings.BaseCurrency, 1,
                g.Select(i => new DocumentLineInput($"{i.Description}{(i.TicketNumber != null ? $" · Tkt {i.TicketNumber}" : "")}", cos, i.Quantity, i.UnitCost, null)).ToList()), ct, system: true);
            foreach (var i in g) i.BillId = bill.Id;
            billIds.Add(bill.Id);
        }
        await db.SaveChangesAsync(ct);
        return new SupplierBillsResult(billIds, billIds.Count);
    }

    // ======================= Dashboard =======================

    public async Task<TravelDashboardDto> DashboardAsync(CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.BookingsView, ct);
        var deptVisible = await access.EntitiesWithAsync(Permissions.DeparturesView, ct);
        if (visible.Count == 0 && deptVisible.Count == 0) throw new ForbiddenException();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var q = db.TravelBookings.Where(b => visible.Contains(b.EntityId));
        var open = await q.CountAsync(b => b.Status == TravelBookingStatus.Quotation || b.Status == TravelBookingStatus.Confirmed, ct);
        var since = monthStart.ToDateTime(TimeOnly.MinValue);
        var monthRows = await q.Where(b => (b.Status == TravelBookingStatus.Confirmed || b.Status == TravelBookingStatus.Invoiced) && b.CreatedAt >= since)
            .Select(b => new { Sell = b.Items.Sum(i => i.Quantity * i.UnitPrice), Margin = b.Items.Sum(i => i.Quantity * (i.UnitPrice - i.UnitCost)) }).ToListAsync(ct);
        var departures = (await tours.DeparturesAsync(null, today, today.AddDays(60), false, ct)).Where(d => d.Status != DepartureStatus.Cancelled).Take(10).ToList();
        var upcoming = await ListItemsAsync(q.Where(b => b.Status != TravelBookingStatus.Cancelled && b.TravelDate >= today && b.TravelDate <= today.AddDays(30)).OrderBy(b => b.TravelDate).Take(15), ct);
        var visas = await db.BookingItems.Join(q, i => i.TravelBookingId, b => b.Id, (i, b) => new { i, b })
            .Where(x => x.i.Type == BookingItemType.Visa && x.b.Status != TravelBookingStatus.Cancelled && x.i.VisaStatus != VisaStatus.Approved && x.i.VisaStatus != VisaStatus.Rejected)
            .OrderBy(x => x.b.TravelDate)
            .Select(x => new VisaRow(x.b.Id, x.b.Number, x.b.Customer!.Name, db.BookingPassengers.Where(p => p.Id == x.i.PassengerId).Select(p => p.FullName).FirstOrDefault(),
                x.i.Country, x.i.VisaStatus ?? VisaStatus.NotStarted, x.b.TravelDate)).Take(20).ToListAsync(ct);
        var paxRows = await q.Where(b => b.Status != TravelBookingStatus.Cancelled && b.TravelDate >= today).Include(b => b.Passengers).Include(b => b.Customer).ToListAsync(ct);
        var passportWarnings = paxRows.SelectMany(b => b.Passengers.Where(p => TourService.PassportWarning(p, b.TravelDate))
            .Select(p => new ManifestRow(b.Number, b.Customer!.Name, p.FullName, p.Type, p.PassportNo, p.PassportExpiry, p.Nationality, p.Cnic, p.Phone, true))).Take(20).ToList();
        return new TravelDashboardDto(open, monthRows.Count, monthRows.Sum(r => r.Sell), monthRows.Sum(r => r.Margin), departures, upcoming, visas, passportWarnings);
    }

    // ======================= helpers =======================

    private static List<string> Warnings(TravelBooking b)
    {
        var w = new List<string>();
        var travelEnd = b.Items.Where(i => i.TourDeparture != null).Select(i => (DateOnly?)i.TourDeparture!.EndDate).Max() ?? b.TravelDate;
        foreach (var p in b.Passengers)
        {
            // A visa means international travel; domestic flights only need a CNIC.
            if (b.Items.Any(i => i.Type == BookingItemType.Visa) && p.PassportNo == null)
                w.Add($"{p.FullName}: passport number missing for international travel.");
            if (TourService.PassportWarning(p, travelEnd))
                w.Add($"{p.FullName}: passport {(p.PassportExpiry == null ? "expiry unknown" : $"expires {p.PassportExpiry:dd MMM yyyy}")} — less than 6 months after travel.");
        }
        foreach (var v in b.Items.Where(i => i.Type == BookingItemType.Visa && i.VisaStatus == VisaStatus.Rejected))
            w.Add($"Visa rejected: {v.Description}.");
        if (b.Items.Any(i => i.Type == BookingItemType.Flight && string.IsNullOrEmpty(i.TicketNumber)) && b.Status != TravelBookingStatus.Quotation)
            w.Add("Flight booked without a ticket number yet.");
        return w;
    }

    private async Task<TravelBooking> LoadAsync(Guid id, CancellationToken ct) =>
        await db.TravelBookings.Include(x => x.Entity).Include(x => x.Customer).Include(x => x.Passengers)
            .Include(x => x.Items).ThenInclude(i => i.Supplier).Include(x => x.Items).ThenInclude(i => i.TourDeparture)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking");

    private async Task<BookingDto> ToDtoAsync(TravelBooking b, CancellationToken ct)
    {
        var deposits = await db.BookingDeposits.Where(d => d.TravelBookingId == b.Id).OrderBy(d => d.Date)
            .Select(d => new BookingDepositDto(d.Id, d.Date, d.Amount, db.Accounts.Where(a => a.Id == d.BankAccountId).Select(a => a.Name).First(), d.Reference, d.Applied)).ToListAsync(ct);
        var invoiceNo = b.InvoiceId == null ? null : await db.FinanceDocuments.Where(d => d.Id == b.InvoiceId).Select(d => d.Number).FirstOrDefaultAsync(ct);
        var by = b.CreatedBy == null ? null : await db.Users.Where(u => u.Id == b.CreatedBy).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var pax = b.Passengers.OrderBy(p => p.Type).ThenBy(p => p.FullName).ToList();
        var sell = b.Items.Sum(i => i.Quantity * i.UnitPrice);
        var cost = b.Items.Sum(i => i.Quantity * i.UnitCost);
        return new BookingDto(b.Id, b.Number, b.EntityId, b.Entity!.Name, b.CustomerId, b.Customer!.Name, b.ContactPhone, b.TravelDate, b.Status, b.Notes,
            b.InvoiceId, invoiceNo,
            pax.Select(p => new PassengerDto(p.Id, p.FullName, p.Type, p.PassportNo, p.PassportExpiry, p.Nationality, p.Cnic, p.DateOfBirth, p.Phone)).ToList(),
            b.Items.OrderBy(i => i.ServiceDate).ThenBy(i => i.Type).Select(i => new BookingItemDto(i.Id, i.Type, i.Description, i.ServiceDate, i.SupplierId, i.Supplier?.Name,
                i.Quantity, i.UnitCost, i.UnitPrice, i.Quantity * i.UnitCost, i.Quantity * i.UnitPrice, i.TaxRateId, i.TourDepartureId, i.TourDeparture?.Code,
                i.Adults, i.Children, i.Airline, i.Pnr, i.TicketNumber, i.Route, i.Country, i.VisaStatus, i.PassengerId,
                b.Passengers.FirstOrDefault(p => p.Id == i.PassengerId)?.FullName, i.BillId)).ToList(),
            deposits, sell, cost, sell - cost, deposits.Sum(d => d.Amount), Warnings(b), by);
    }
}
