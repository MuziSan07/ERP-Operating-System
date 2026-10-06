using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Application.Inventory;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Hotel;

/// <summary>
/// Reservations, front desk and folios. A night is the date a guest sleeps (arrival ≤ night &lt; departure).
/// Room nights are charged by night audit (or at checkout for any missed nights); checkout turns the folio into an
/// approved sales tax invoice, applies deposits and records the settlement — all through the Finance module.
/// </summary>
public class ReservationService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger,
    DocumentService documents, PaymentService payments, AdvanceService advances, InventoryService inventory)
{
    private static readonly ReservationStatus[] Holding = [ReservationStatus.Tentative, ReservationStatus.Confirmed, ReservationStatus.CheckedIn];
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)); // Pakistan time for front-desk "today"

    // ======================= Availability =======================

    public async Task<AvailabilityDto> AvailabilityAsync(Guid entityId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.ReservationsView, entityId, ct);
        if (to <= from) to = from.AddDays(1);
        if (to.DayNumber - from.DayNumber > 62) throw new ValidationException("Check at most 62 nights at a time.");
        var nights = Enumerable.Range(0, to.DayNumber - from.DayNumber).Select(from.AddDays).ToList();
        var types = await db.RoomTypes.Where(t => t.EntityId == entityId && t.IsActive).OrderBy(t => t.BaseRate).ToListAsync(ct);
        var rows = new List<AvailabilityRow>();
        foreach (var t in types)
        {
            var total = await db.Rooms.CountAsync(r => r.RoomTypeId == t.Id && r.IsActive, ct);
            var booked = await BookedPerNightAsync(t.Id, from, to, null, ct);
            var avail = nights.Select(n => total - booked.GetValueOrDefault(n)).ToList();
            rows.Add(new AvailabilityRow(t.Id, t.Name, t.BaseRate, total, avail, avail.Count == 0 ? total : avail.Min()));
        }
        return new AvailabilityDto(from, to, nights, rows);
    }

    private async Task<Dictionary<DateOnly, int>> BookedPerNightAsync(Guid roomTypeId, DateOnly from, DateOnly to, Guid? excludeReservation, CancellationToken ct)
    {
        var stays = await db.ReservationRooms
            .Join(db.Reservations, rr => rr.ReservationId, r => r.Id, (rr, r) => new { rr.RoomTypeId, r.Id, r.Status, r.ArrivalDate, r.DepartureDate })
            .Where(x => x.RoomTypeId == roomTypeId && Holding.Contains(x.Status) && x.ArrivalDate < to && x.DepartureDate > from && x.Id != excludeReservation)
            .Select(x => new { x.ArrivalDate, x.DepartureDate }).ToListAsync(ct);
        var result = new Dictionary<DateOnly, int>();
        foreach (var s in stays)
            for (var d = s.ArrivalDate < from ? from : s.ArrivalDate; d < s.DepartureDate && d < to; d = d.AddDays(1))
                result[d] = result.GetValueOrDefault(d) + 1;
        return result;
    }

    // ======================= Reservations =======================

    public async Task<PagedResult<ReservationListItem>> ListAsync(Guid? entityId, ReservationStatus? status, DateOnly? from, DateOnly? to, string? search,
        int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ReservationsView, ct);
        var q = db.Reservations.Where(r => visible.Contains(r.EntityId));
        if (entityId != null) q = q.Where(r => r.EntityId == entityId);
        if (status != null) q = q.Where(r => r.Status == status);
        if (from != null) q = q.Where(r => r.DepartureDate > from);
        if (to != null) q = q.Where(r => r.ArrivalDate <= to);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(r => r.Number.Contains(search) || r.Guest!.FullName.Contains(search) || (r.Guest.Phone != null && r.Guest.Phone.Contains(search)));
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await ListItemsAsync(q.OrderByDescending(r => r.ArrivalDate).ThenBy(r => r.Number).Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new PagedResult<ReservationListItem>(items, total, page, pageSize);
    }

    private async Task<List<ReservationListItem>> ListItemsAsync(IQueryable<Reservation> q, CancellationToken ct)
    {
        var rows = await q.Select(r => new
        {
            r.Id, r.Number, r.Guest!.FullName, r.Guest.Phone, r.Guest.IsVip, Entity = r.Entity!.Name, r.ArrivalDate, r.DepartureDate, r.Source, r.Status,
            Rooms = r.Rooms.Select(x => x.Room != null ? x.Room.Number : x.RoomType!.Name).ToList(),
            Charges = db.FolioCharges.Where(c => c.ReservationId == r.Id && !c.IsVoid).Sum(c => c.Amount + c.TaxAmount),
            Deposits = db.FolioDeposits.Where(d => d.ReservationId == r.Id).Sum(d => d.Amount)
        }).ToListAsync(ct);
        return rows.Select(r => new ReservationListItem(r.Id, r.Number, r.FullName, r.Phone, r.IsVip, r.Entity, r.ArrivalDate, r.DepartureDate,
            r.DepartureDate.DayNumber - r.ArrivalDate.DayNumber, string.Join(", ", r.Rooms), r.Source, r.Status, r.Charges - r.Deposits)).ToList();
    }

    public async Task<ReservationDto> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await LoadAsync(id, ct);
        await access.EnsureAsync(Permissions.ReservationsView, r.EntityId, ct);
        return await ToDtoAsync(r, ct);
    }

    public async Task<ReservationDto> SaveAsync(Guid? id, SaveReservationRequest req, CancellationToken ct)
    {
        Reservation r;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.ReservationsCreate, req.EntityId, ct);
            r = new Reservation { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Number = await ledger.NextNumberAsync("RES", req.ArrivalDate, ct) };
            db.Reservations.Add(r);
        }
        else
        {
            r = await db.Reservations.Include(x => x.Rooms).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");
            await access.EnsureAsync(Permissions.ReservationsEdit, r.EntityId, ct);
            if (r.Status is not (ReservationStatus.Tentative or ReservationStatus.Confirmed)) throw new ValidationException("Only reservations not yet checked in can be changed.");
            if (req.EntityId != r.EntityId) throw new ValidationException("A reservation can't move to another property.");
            db.ReservationRooms.RemoveRange(r.Rooms);
            r.Rooms.Clear();
        }

        if (req.DepartureDate <= req.ArrivalDate) throw new ValidationException("Departure must be after arrival.");
        if (id == null && req.ArrivalDate < Today.AddDays(-1)) throw new ValidationException("Arrival can't be in the past.");
        if (req.DepartureDate.DayNumber - req.ArrivalDate.DayNumber > 90) throw new ValidationException("Stays longer than 90 nights need a long-stay contract.");
        if (req.Rooms.Count == 0) throw new ValidationException("Book at least one room.");
        if (req.Adults < 1) throw new ValidationException("At least one adult.");

        // Guest: existing or new.
        if (req.GuestId is { } gid)
        {
            if (!await db.Guests.AnyAsync(g => g.Id == gid, ct)) throw new NotFoundException("Guest");
            r.GuestId = gid;
        }
        else
        {
            var guest = new Guest { TenantId = currentUser.TenantId!.Value };
            HotelSetupService.Apply(guest, req.NewGuest ?? throw new ValidationException("Choose a guest or enter a new guest."));
            db.Guests.Add(guest);
            r.GuestId = guest.Id;
        }
        if (req.BillToContactId is { } bill && !await db.Contacts.AnyAsync(c => c.Id == bill && c.IsCustomer, ct)) throw new ValidationException("Bill-to must be a customer.");

        r.BillToContactId = req.BillToContactId;
        r.Source = req.Source;
        r.ArrivalDate = req.ArrivalDate;
        r.DepartureDate = req.DepartureDate;
        r.Adults = req.Adults;
        r.Children = Math.Max(0, req.Children);
        r.Status = req.Tentative ? ReservationStatus.Tentative : ReservationStatus.Confirmed;
        r.ExternalReference = req.ExternalReference;
        r.Notes = req.Notes;

        var types = await db.RoomTypes.Where(t => t.EntityId == req.EntityId).ToDictionaryAsync(t => t.Id, ct);
        foreach (var g in req.Rooms.GroupBy(x => x.RoomTypeId))
        {
            if (!types.TryGetValue(g.Key, out var type) || !type.IsActive) throw new ValidationException("Room type not available at this property.");
            var total = await db.Rooms.CountAsync(x => x.RoomTypeId == type.Id && x.IsActive, ct);
            var booked = await BookedPerNightAsync(type.Id, req.ArrivalDate, req.DepartureDate, r.Id, ct);
            var worst = booked.Count == 0 ? 0 : booked.Values.Max();
            if (worst + g.Count() > total)
                throw new ValidationException($"Only {Math.Max(0, total - worst)} {type.Name} room(s) free on every night of this stay; {g.Count()} requested.");
        }
        foreach (var x in req.Rooms)
        {
            var rate = x.Rate ?? types[x.RoomTypeId].BaseRate;
            if (rate < 0) throw new ValidationException("Rate can't be negative.");
            if (x.RoomId is { } roomId) await EnsureRoomFreeAsync(roomId, x.RoomTypeId, req.ArrivalDate, req.DepartureDate, r.Id, ct);
            r.Rooms.Add(new ReservationRoom { ReservationId = r.Id, RoomTypeId = x.RoomTypeId, RoomId = x.RoomId, Rate = rate });
        }
        if (req.Rooms.Where(x => x.RoomId != null).GroupBy(x => x.RoomId).Any(g => g.Count() > 1)) throw new ValidationException("The same room is assigned twice.");

        await db.SaveChangesAsync(ct);
        return await GetAsync(r.Id, ct);
    }

    private async Task EnsureRoomFreeAsync(Guid roomId, Guid roomTypeId, DateOnly from, DateOnly to, Guid reservationId, CancellationToken ct)
    {
        await db.LockAsync<Room>(roomId, ct); // a simultaneous booking of this room waits, then sees this one
        var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, ct) ?? throw new NotFoundException("Room");
        if (room.RoomTypeId != roomTypeId) throw new ValidationException($"Room {room.Number} is not of the booked room type.");
        if (!room.IsActive) throw new ValidationException($"Room {room.Number} is inactive.");
        var clash = await db.ReservationRooms
            .Join(db.Reservations, rr => rr.ReservationId, res => res.Id, (rr, res) => new { rr.RoomId, res })
            .Where(x => x.RoomId == roomId && x.res.Id != reservationId && Holding.Contains(x.res.Status) && x.res.ArrivalDate < to && x.res.DepartureDate > from)
            .Select(x => x.res.Number).FirstOrDefaultAsync(ct);
        if (clash != null) throw new ValidationException($"Room {room.Number} is already assigned to {clash} for some of these nights.");
    }

    public async Task<ReservationDto> AssignRoomAsync(Guid id, AssignRoomRequest req, CancellationToken ct)
    {
        var r = await db.Reservations.Include(x => x.Rooms).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");
        await access.EnsureAsync(Permissions.ReservationsEdit, r.EntityId, ct);
        if (r.Status is ReservationStatus.CheckedOut or ReservationStatus.Cancelled or ReservationStatus.NoShow) throw new ValidationException("This reservation is closed.");
        var line = r.Rooms.FirstOrDefault(x => x.Id == req.ReservationRoomId) ?? throw new NotFoundException("Booked room");
        await EnsureRoomFreeAsync(req.RoomId, line.RoomTypeId, r.Status == ReservationStatus.CheckedIn ? Today : r.ArrivalDate, r.DepartureDate, r.Id, ct);
        if (r.Status == ReservationStatus.CheckedIn)
        {
            // Room move for an in-house guest: the old room needs cleaning.
            if (line.RoomId is { } old) (await db.Rooms.FirstAsync(x => x.Id == old, ct)).Housekeeping = HousekeepingStatus.Dirty;
            var room = await db.Rooms.FirstAsync(x => x.Id == req.RoomId, ct);
            if (room.Housekeeping is HousekeepingStatus.Dirty or HousekeepingStatus.OutOfOrder) throw new ValidationException($"Room {room.Number} isn't ready.");
        }
        line.RoomId = req.RoomId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReservationDto> CancelAsync(Guid id, bool noShow, CancellationToken ct)
    {
        var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");
        await access.EnsureAsync(Permissions.ReservationsEdit, r.EntityId, ct);
        if (r.Status is not (ReservationStatus.Tentative or ReservationStatus.Confirmed)) throw new ValidationException("Only reservations not yet checked in can be cancelled.");
        if (noShow && r.ArrivalDate > Today) throw new ValidationException("A guest can only be a no-show after the arrival date.");
        r.Status = noShow ? ReservationStatus.NoShow : ReservationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ======================= Check-in / folio / check-out =======================

    public async Task<ReservationDto> CheckInAsync(Guid id, CancellationToken ct)
    {
        var r = await db.Reservations.Include(x => x.Rooms).ThenInclude(x => x.Room).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");
        await access.EnsureAsync(Permissions.ReservationsCheckIn, r.EntityId, ct);
        if (r.Status is not (ReservationStatus.Tentative or ReservationStatus.Confirmed)) throw new ValidationException("This reservation can't be checked in.");
        if (r.ArrivalDate > Today) throw new ValidationException($"Arrival is on {r.ArrivalDate:dd MMM yyyy}. Change the dates to check in early.");
        if (r.DepartureDate <= Today) throw new ValidationException("The stay has already ended; adjust the dates first.");
        foreach (var line in r.Rooms)
        {
            if (line.Room == null) throw new ValidationException("Assign a room to every booked room before check-in.");
            if (line.Room.Housekeeping is HousekeepingStatus.Dirty or HousekeepingStatus.OutOfOrder)
                throw new ValidationException($"Room {line.Room.Number} is {line.Room.Housekeeping.ToString().ToLower()}. Ask housekeeping to clear it first.");
            var occupied = await db.ReservationRooms.Join(db.Reservations, rr => rr.ReservationId, res => res.Id, (rr, res) => new { rr.RoomId, res })
                .AnyAsync(x => x.RoomId == line.RoomId && x.res.Status == ReservationStatus.CheckedIn && x.res.Id != r.Id, ct);
            if (occupied) throw new ValidationException($"Room {line.Room.Number} is still occupied.");
        }
        // Arriving after the booked arrival date: the stay starts today.
        if (r.ArrivalDate < Today) r.ArrivalDate = Today;
        r.Status = ReservationStatus.CheckedIn;
        r.CheckedInAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReservationDto> AddChargeAsync(Guid id, AddChargeRequest req, CancellationToken ct)
    {
        var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");
        await access.EnsureAsync(Permissions.ReservationsEdit, r.EntityId, ct);
        if (r.Status != ReservationStatus.CheckedIn) throw new ValidationException("Charges can only be posted to in-house guests.");
        if (req.Type == FolioChargeType.Room) throw new ValidationException("Room nights are charged by night audit.");
        if (req.Quantity <= 0 || req.UnitPrice < 0) throw new ValidationException("Quantity must be positive and price not negative.");
        var settings = await ledger.SettingsAsync(ct);
        var date = req.Date ?? Today;
        await ledger.EnsureOpenPeriodAsync(date, ct);

        var charge = new FolioCharge
        {
            TenantId = r.TenantId, ReservationId = r.Id, Date = date, Type = req.Type, Description = Guard.Required(req.Description, "Description", 200),
            Quantity = req.Quantity, UnitPrice = req.UnitPrice, TaxRateId = req.TaxRateId,
            IncomeAccountId = req.IncomeAccountId ?? await DefaultIncomeAccountAsync(ct)
        };
        Price(charge, req.TaxRateId == null ? 0 : await db.TaxRates.Where(t => t.Id == req.TaxRateId).Select(t => t.Rate).FirstAsync(ct));

        if (req.ItemId is { } itemId)
        {
            // Minibar / restaurant stock leaves the outlet's warehouse at average cost.
            var warehouse = req.WarehouseId ?? throw new ValidationException("Choose the outlet warehouse the item comes from.");
            // The stock issue below runs as a system action, so check here that the warehouse is the hotel's own, or that the
            // clerk may issue stock where it is.
            var warehouseEntity = await db.Warehouses.Where(w => w.Id == warehouse).Select(w => (Guid?)w.EntityId).FirstOrDefaultAsync(ct)
                                  ?? throw new NotFoundException("Warehouse");
            if (warehouseEntity != r.EntityId) await access.EnsureAsync(Permissions.StockIssue, warehouseEntity, ct);
            var tx = await inventory.CreateTransactionAsync(new CreateStockTransactionRequest(StockTransactionType.Issue, warehouse, null, date, null, r.EntityId,
                r.Number, $"{req.Type} — room folio {r.Number}", [new StockLineInput(itemId, req.Quantity, null, null, null, null, req.Description)]), ct, system: true);
            charge.ItemId = itemId;
            charge.StockTransactionId = tx.Id;
        }
        db.FolioCharges.Add(charge);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReservationDto> VoidChargeAsync(Guid id, Guid chargeId, CancellationToken ct)
    {
        var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");
        await access.EnsureAsync(Permissions.ReservationsCheckOut, r.EntityId, ct);
        if (r.Status != ReservationStatus.CheckedIn) throw new ValidationException("The folio is closed.");
        var c = await db.FolioCharges.FirstOrDefaultAsync(x => x.Id == chargeId && x.ReservationId == id, ct) ?? throw new NotFoundException("Charge");
        if (c.StockTransactionId != null) throw new ValidationException("This charge issued stock; return the items with a stock adjustment and post a correcting charge.");
        c.IsVoid = true;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReservationDto> DepositAsync(Guid id, DepositRequest req, CancellationToken ct)
    {
        var r = await db.Reservations.Include(x => x.Guest).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");
        await access.EnsureAsync(Permissions.ReservationsEdit, r.EntityId, ct);
        if (r.Status is ReservationStatus.CheckedOut or ReservationStatus.Cancelled or ReservationStatus.NoShow) throw new ValidationException("This reservation is closed.");
        var contactId = await BillingContactAsync(r, ct);
        var date = req.Date ?? Today;
        var deposit = new FolioDeposit { TenantId = r.TenantId, ReservationId = r.Id, Date = date, Amount = LedgerService.Round(req.Amount), BankAccountId = req.BankAccountId, Reference = req.Reference };
        var entry = await advances.ReceiveAsync(r.EntityId, contactId, req.BankAccountId, deposit.Amount, date, $"Deposit — {r.Number} {r.Guest!.FullName}", req.Reference ?? r.Number, deposit.Id, ct);
        deposit.JournalEntryId = entry.Id;
        db.FolioDeposits.Add(deposit);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Charges every in-house reservation of the property for one night (idempotent).</summary>
    public async Task<NightAuditResult> NightAuditAsync(NightAuditRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.ReservationsCheckOut, req.EntityId, ct);
        await ledger.EnsureOpenPeriodAsync(req.Date, ct);
        var inHouse = await db.Reservations.Include(x => x.Rooms).ThenInclude(x => x.RoomType)
            .Where(x => x.EntityId == req.EntityId && x.Status == ReservationStatus.CheckedIn && x.ArrivalDate <= req.Date && x.DepartureDate > req.Date).ToListAsync(ct);
        var count = 0;
        var revenue = 0m;
        foreach (var r in inHouse)
        {
            await db.LockAsync<Reservation>(r.Id, ct); // two audits at once can't both charge the same night
            var added = await PostRoomNightsAsync(r, req.Date, req.Date.AddDays(1), ct);
            if (added.Count > 0) count++;
            revenue += added.Sum(c => c.Amount);
        }
        await db.SaveChangesAsync(ct);
        return new NightAuditResult(req.Date, count, revenue);
    }

    private async Task<List<FolioCharge>> PostRoomNightsAsync(Reservation r, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var existing = (await db.FolioCharges.Where(c => c.ReservationId == r.Id && c.Type == FolioChargeType.Room && !c.IsVoid)
            .Select(c => new { c.ReservationRoomId, c.Date }).ToListAsync(ct)).Select(c => (c.ReservationRoomId, c.Date)).ToHashSet();
        var defaultIncome = await DefaultIncomeAccountAsync(ct);
        var added = new List<FolioCharge>();
        foreach (var line in r.Rooms)
        {
            var type = line.RoomType ?? await db.RoomTypes.FirstAsync(t => t.Id == line.RoomTypeId, ct);
            var taxRate = type.TaxRateId == null ? 0 : await db.TaxRates.Where(t => t.Id == type.TaxRateId).Select(t => t.Rate).FirstAsync(ct);
            var roomNo = line.RoomId == null ? type.Name : await db.Rooms.Where(x => x.Id == line.RoomId).Select(x => x.Number).FirstAsync(ct);
            for (var night = from; night < to; night = night.AddDays(1))
            {
                if (existing.Contains((line.Id, night))) continue;
                var c = new FolioCharge
                {
                    TenantId = r.TenantId, ReservationId = r.Id, Date = night, Type = FolioChargeType.Room, ReservationRoomId = line.Id,
                    Description = $"Room {roomNo} — {type.Name}", Quantity = 1, UnitPrice = line.Rate, TaxRateId = type.TaxRateId,
                    IncomeAccountId = type.IncomeAccountId ?? defaultIncome
                };
                Price(c, taxRate);
                db.FolioCharges.Add(c);
                added.Add(c);
            }
        }
        return added;
    }

    /// <summary>
    /// Charges any nights not yet audited, invoices the folio (sales tax invoice), applies deposits, records the
    /// settlement and marks the rooms dirty. A bill-to company keeps any balance on its account.
    /// </summary>
    public async Task<ReservationDto> CheckOutAsync(Guid id, CheckOutRequest req, CancellationToken ct)
    {
        var r = await db.Reservations.Include(x => x.Rooms).ThenInclude(x => x.RoomType).Include(x => x.Guest).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Reservation");
        await access.EnsureAsync(Permissions.ReservationsCheckOut, r.EntityId, ct);
        if (r.Status != ReservationStatus.CheckedIn) throw new ValidationException("Only in-house guests can be checked out.");
        var date = req.Date ?? Today;
        if (date < r.ArrivalDate) throw new ValidationException("Checkout can't be before arrival.");
        var settings = await ledger.SettingsAsync(ct);
        var contactId = await BillingContactAsync(r, ct);
        var held = await db.FolioDeposits.Where(d => d.ReservationId == r.Id).SumAsync(d => d.Amount, ct);

        // A previous attempt may have invoiced already and failed at settlement: resume instead of invoicing twice.
        if (r.InvoiceId == null)
        {
            // Early or late departure: bill the nights actually stayed (at least one).
            var departure = date > r.ArrivalDate ? date : r.ArrivalDate.AddDays(1);
            var newNights = await PostRoomNightsAsync(r, r.ArrivalDate, departure, ct);
            var charges = await db.FolioCharges.Where(c => c.ReservationId == r.Id && !c.IsVoid).OrderBy(c => c.Date).ToListAsync(ct);
            charges.AddRange(newNights);

            // Catch an unpaid walk-in before anything is posted.
            var expected = charges.Sum(c => c.Amount + c.TaxAmount) - held;
            if (r.BillToContactId == null && expected > 1 && (req.BankAccountId == null || (req.AmountPaid ?? 0) < expected - 1))
                throw new ValidationException($"Collect the balance of about {expected:N2} (or bill a company) to check out.");
            r.DepartureDate = departure;
            await db.SaveChangesAsync(ct);
            r.InvoiceId = await InvoiceFolioAsync(r, charges, contactId, date, departure, settings.BaseCurrency, ct);
            await db.SaveChangesAsync(ct);

            if (held > 0)
            {
                var fresh = await db.FinanceDocuments.FirstAsync(d => d.Id == r.InvoiceId, ct);
                await advances.ApplyAsync(fresh, Math.Min(held, fresh.Total), date, $"Deposits applied — {r.Number}", ct);
                await db.SaveChangesAsync(ct);
            }
        }
        var doc = await db.FinanceDocuments.FirstAsync(d => d.Id == r.InvoiceId, ct);

        if (req.BankAccountId is { } bank && req.AmountPaid is > 0 and var paid)
        {
            var balance = doc.Total - doc.AmountPaid;
            if (paid > balance + 0.005m) throw new ValidationException($"The balance due is {balance:N2}.");
            await payments.CreateAsync(new CreatePaymentRequest(PaymentKind.Receipt, r.EntityId, contactId, date, bank, settings.BaseCurrency, 1, Math.Min(paid, balance),
                req.PaymentReference ?? r.Number, $"Checkout settlement {r.Number}", [new AllocationInput(doc.Id, Math.Min(paid, balance))]), ct, system: true);
        }
        else if (r.BillToContactId == null && doc.Total - doc.AmountPaid > 0.005m)
            throw new ValidationException($"Collect the balance of {doc.Total - doc.AmountPaid:N2} (or bill a company) to check out.");

        foreach (var line in r.Rooms.Where(x => x.RoomId != null))
            (await db.Rooms.FirstAsync(x => x.Id == line.RoomId, ct)).Housekeeping = HousekeepingStatus.Dirty;
        r.Status = ReservationStatus.CheckedOut;
        r.CheckedOutAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Turns the folio into an approved sales tax invoice. Room nights are grouped per room and rate.</summary>
    private async Task<Guid> InvoiceFolioAsync(Reservation r, List<FolioCharge> charges, Guid contactId, DateOnly date, DateOnly departure, string currency, CancellationToken ct)
    {
        var lines = charges.Where(c => c.Type == FolioChargeType.Room)
            .GroupBy(c => new { c.ReservationRoomId, c.UnitPrice, c.TaxRateId, c.IncomeAccountId, c.Description })
            .Select(g => new DocumentLineInput($"{g.Key.Description} — {g.Count()} night(s) {g.Min(c => c.Date):dd MMM}–{g.Max(c => c.Date).AddDays(1):dd MMM}",
                g.Key.IncomeAccountId, g.Count(), g.Key.UnitPrice, g.Key.TaxRateId))
            .Concat(charges.Where(c => c.Type != FolioChargeType.Room)
                .Select(c => new DocumentLineInput($"{c.Type}: {c.Description} ({c.Date:dd MMM})", c.IncomeAccountId, c.Quantity, c.UnitPrice, c.TaxRateId)))
            .ToList();
        if (lines.Count == 0) throw new ValidationException("The folio is empty.");

        var draft = await documents.CreateAsync(DocumentKind.Invoice, new SaveDocumentRequest(r.EntityId, contactId, date,
            r.BillToContactId == null ? date : null, r.Number, $"Stay {r.ArrivalDate:dd MMM}–{departure:dd MMM yyyy}, guest {r.Guest!.FullName}", currency, 1, lines), ct, system: true);
        return (await documents.ApproveAsync(draft.Id, ct, system: true)).Id;
    }

    // ======================= Front desk =======================

    public async Task<FrontDeskDto> FrontDeskAsync(Guid entityId, DateOnly? date, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.ReservationsView, entityId, ct);
        var day = date ?? Today;
        var rooms = await db.Rooms.Where(x => x.EntityId == entityId && x.IsActive).Select(x => new { x.Id, x.Housekeeping }).ToListAsync(ct);
        var occupiedIds = (await db.ReservationRooms.Join(db.Reservations, rr => rr.ReservationId, res => res.Id, (rr, res) => new { rr.RoomId, res.Status, res.EntityId })
            .Where(x => x.EntityId == entityId && x.Status == ReservationStatus.CheckedIn && x.RoomId != null).Select(x => x.RoomId!.Value).ToListAsync(ct)).ToHashSet();
        var baseQ = db.Reservations.Where(x => x.EntityId == entityId);
        var arrivals = await ListItemsAsync(baseQ.Where(x => x.ArrivalDate <= day && (x.Status == ReservationStatus.Confirmed || x.Status == ReservationStatus.Tentative)), ct);
        var departures = await ListItemsAsync(baseQ.Where(x => x.Status == ReservationStatus.CheckedIn && x.DepartureDate <= day), ct);
        var inHouse = await ListItemsAsync(baseQ.Where(x => x.Status == ReservationStatus.CheckedIn), ct);
        var vacant = rooms.Where(x => !occupiedIds.Contains(x.Id)).ToList();
        var sellable = rooms.Count(x => x.Housekeeping != HousekeepingStatus.OutOfOrder);
        return new FrontDeskDto(day, rooms.Count, occupiedIds.Count,
            vacant.Count(x => x.Housekeeping is HousekeepingStatus.Clean or HousekeepingStatus.Inspected),
            vacant.Count(x => x.Housekeeping == HousekeepingStatus.Dirty), rooms.Count(x => x.Housekeeping == HousekeepingStatus.OutOfOrder),
            sellable == 0 ? 0 : Math.Round(occupiedIds.Count * 100m / sellable, 1), arrivals, departures, inHouse);
    }

    public async Task<TapeChartDto> TapeChartAsync(Guid entityId, DateOnly from, int days, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.ReservationsView, entityId, ct);
        days = Math.Clamp(days, 1, 31);
        var to = from.AddDays(days);
        var stays = await db.ReservationRooms.Join(db.Reservations, rr => rr.ReservationId, res => res.Id, (rr, res) => new { rr, res })
            .Where(x => x.res.EntityId == entityId && Holding.Contains(x.res.Status) && x.res.ArrivalDate < to && x.res.DepartureDate > from)
            .Select(x => new { x.rr.RoomId, x.res.Id, x.res.Number, x.res.Guest!.FullName, x.res.ArrivalDate, x.res.DepartureDate, x.res.Status, x.res.Guest.IsVip })
            .ToListAsync(ct);
        var rooms = await db.Rooms.Where(x => x.EntityId == entityId && x.IsActive).OrderBy(x => x.Number)
            .Select(x => new { x.Id, x.Number, Type = x.RoomType!.Name, x.Housekeeping }).ToListAsync(ct);
        return new TapeChartDto(from, days,
            rooms.Select(x => new TapeRoom(x.Id, x.Number, x.Type, x.Housekeeping,
                stays.Where(s => s.RoomId == x.Id).Select(s => new TapeBlock(s.Id, s.Number, s.FullName, s.ArrivalDate, s.DepartureDate, s.Status, s.IsVip)).ToList())).ToList(),
            stays.Where(s => s.RoomId == null).Select(s => new TapeBlock(s.Id, s.Number, s.FullName, s.ArrivalDate, s.DepartureDate, s.Status, s.IsVip)).ToList());
    }

    /// <summary>Occupancy, ADR (room revenue ÷ rooms sold) and RevPAR (room revenue ÷ rooms available) per night.</summary>
    public async Task<HotelReportDto> ReportAsync(Guid entityId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.HotelReports, entityId, ct);
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) throw new ValidationException("Report at most one year at a time.");
        var available = await db.Rooms.CountAsync(x => x.EntityId == entityId && x.IsActive, ct);
        var charges = await db.FolioCharges.Join(db.Reservations, c => c.ReservationId, r => r.Id, (c, r) => new { c, r.EntityId })
            .Where(x => x.EntityId == entityId && !x.c.IsVoid && x.c.Date >= from && x.c.Date <= to)
            .Select(x => new { x.c.Date, x.c.Type, x.c.Amount }).ToListAsync(ct);
        var days = new List<HotelReportRow>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var room = charges.Where(c => c.Date == d && c.Type == FolioChargeType.Room).ToList();
            var sold = room.Count;
            var rev = room.Sum(c => c.Amount);
            days.Add(new HotelReportRow(d, available, sold, available == 0 ? 0 : Math.Round(sold * 100m / available, 1), rev,
                sold == 0 ? 0 : Math.Round(rev / sold, 2), available == 0 ? 0 : Math.Round(rev / available, 2)));
        }
        var totalAvail = available * days.Count;
        var totalSold = days.Sum(x => x.Sold);
        var totalRev = days.Sum(x => x.RoomRevenue);
        return new HotelReportDto(from, to, days, totalAvail, totalSold, totalAvail == 0 ? 0 : Math.Round(totalSold * 100m / totalAvail, 1), totalRev,
            totalSold == 0 ? 0 : Math.Round(totalRev / totalSold, 2), totalAvail == 0 ? 0 : Math.Round(totalRev / totalAvail, 2),
            charges.GroupBy(c => c.Type.ToString()).ToDictionary(g => g.Key, g => g.Sum(c => c.Amount)));
    }

    // ======================= helpers =======================

    private static void Price(FolioCharge c, decimal taxRate)
    {
        c.Amount = LedgerService.Round(c.Quantity * c.UnitPrice);
        c.TaxAmount = LedgerService.Round(c.Amount * taxRate);
    }

    private async Task<Guid> DefaultIncomeAccountAsync(CancellationToken ct) =>
        await db.Accounts.Where(a => a.Code == "4100" && !a.IsGroup).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct)
        ?? await db.Accounts.Where(a => a.SubType == AccountSubType.Revenue && !a.IsGroup).Select(a => a.Id).FirstAsync(ct);

    /// <summary>The company billed, or the guest's own customer record (created on first use).</summary>
    private async Task<Guid> BillingContactAsync(Reservation r, CancellationToken ct)
    {
        if (r.BillToContactId is { } bill) return bill;
        var guest = r.Guest ?? await db.Guests.FirstAsync(g => g.Id == r.GuestId, ct);
        if (guest.ContactId is { } existing) return existing;
        var contact = new Contact
        {
            TenantId = guest.TenantId, Code = $"G-{await db.NextSequenceAsync("GUEST-CONTACT", ct):D5}", Name = guest.FullName, IsCustomer = true,
            Phone = guest.Phone, Email = guest.Email, Cnic = guest.Cnic, Address = guest.Address, City = guest.City, PaymentTermsDays = 0
        };
        db.Contacts.Add(contact);
        guest.ContactId = contact.Id;
        await db.SaveChangesAsync(ct);
        return contact.Id;
    }

    private async Task<Reservation> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Reservations.Include(x => x.Entity).Include(x => x.Guest).Include(x => x.BillToContact)
            .Include(x => x.Rooms).ThenInclude(x => x.RoomType).Include(x => x.Rooms).ThenInclude(x => x.Room)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reservation");

    private async Task<ReservationDto> ToDtoAsync(Reservation r, CancellationToken ct)
    {
        var charges = await db.FolioCharges.Where(c => c.ReservationId == r.Id).Include(c => c.TaxRate).OrderBy(c => c.Date).ThenBy(c => c.CreatedAt).ToListAsync(ct);
        var deposits = await db.FolioDeposits.Where(d => d.ReservationId == r.Id).OrderBy(d => d.Date)
            .Select(d => new FolioDepositDto(d.Id, d.Date, d.Amount, db.Accounts.Where(a => a.Id == d.BankAccountId).Select(a => a.Name).First(), d.Reference)).ToListAsync(ct);
        var invoiceNo = r.InvoiceId == null ? null : await db.FinanceDocuments.Where(d => d.Id == r.InvoiceId).Select(d => d.Number).FirstOrDefaultAsync(ct);
        var live = charges.Where(c => !c.IsVoid).ToList();
        var nights = r.DepartureDate.DayNumber - r.ArrivalDate.DayNumber;
        var guest = r.Guest!;
        var stays = await db.Reservations.CountAsync(x => x.GuestId == guest.Id && x.Status == ReservationStatus.CheckedOut, ct);
        return new ReservationDto(r.Id, r.Number, r.EntityId, r.Entity!.Name, r.GuestId,
            new GuestDto(guest.Id, guest.FullName, guest.Phone, guest.Email, guest.Cnic, guest.PassportNo, guest.Nationality, guest.Address, guest.City, guest.IsVip, guest.Notes, stays),
            r.BillToContactId, r.BillToContact?.Name, r.Source, r.ArrivalDate, r.DepartureDate, nights, r.Adults, r.Children, r.Status, r.ExternalReference,
            r.Notes, r.CheckedInAt, r.CheckedOutAt, r.InvoiceId, invoiceNo,
            r.Rooms.Select(x => new ReservationRoomDto(x.Id, x.RoomTypeId, x.RoomType!.Name, x.RoomId, x.Room?.Number, x.Rate)).ToList(),
            charges.Select(c => new FolioChargeDto(c.Id, c.Date, c.Type, c.Description, c.Quantity, c.UnitPrice, c.Amount, c.TaxRate?.Name, c.TaxAmount, c.IsVoid, c.StockTransactionId != null)).ToList(),
            deposits, live.Sum(c => c.Amount), live.Sum(c => c.TaxAmount), deposits.Sum(d => d.Amount),
            live.Sum(c => c.Amount + c.TaxAmount) - deposits.Sum(d => d.Amount), r.Rooms.Sum(x => x.Rate) * nights);
    }
}
