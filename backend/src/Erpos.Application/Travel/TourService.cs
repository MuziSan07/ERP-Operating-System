using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Travel;

/// <summary>Tour packages, guides and departures (seats, guides, operating costs, manifest, profitability).</summary>
public class TourService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents)
{
    /// <summary>Bookings that hold seats on a departure.</summary>
    internal static readonly TravelBookingStatus[] Holding = [TravelBookingStatus.Confirmed, TravelBookingStatus.Invoiced];

    // ======================= Packages =======================

    public async Task<List<TourPackageDto>> PackagesAsync(bool activeOnly, CancellationToken ct)
    {
        var visible = await TravelEntitiesAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = db.TourPackages.Where(p => visible.Contains(p.EntityId));
        if (activeOnly) q = q.Where(p => p.IsActive);
        var rows = await q.Include(p => p.Entity).Include(p => p.Itinerary).OrderBy(p => p.Name).ToListAsync(ct);
        var ids = rows.Select(p => p.Id).ToList();
        var upcoming = await db.TourDepartures.Where(d => ids.Contains(d.TourPackageId) && d.StartDate >= today && d.Status == DepartureStatus.Open)
            .GroupBy(d => d.TourPackageId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return rows.Select(p => ToDto(p, upcoming.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<TourPackageDto> SavePackageAsync(Guid? id, SaveTourPackageRequest req, CancellationToken ct)
    {
        TourPackage p;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.PackagesCreate, req.EntityId, ct);
            p = new TourPackage { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId };
            db.TourPackages.Add(p);
        }
        else
        {
            p = await db.TourPackages.Include(x => x.Itinerary).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Package");
            await access.EnsureAsync(Permissions.PackagesEdit, p.EntityId, ct);
            db.ItineraryDays.RemoveRange(p.Itinerary);
            p.Itinerary.Clear();
        }
        var code = Guard.Code(req.Code);
        if (await db.TourPackages.AnyAsync(x => x.Code == code && x.Id != p.Id, ct)) throw new ValidationException($"Package code {code} already exists.");
        if (req.DurationDays is < 1 or > 60) throw new ValidationException("Duration must be 1–60 days.");
        if (req.AdultPrice < 0 || req.ChildPrice < 0 || req.SingleSupplement < 0) throw new ValidationException("Prices can't be negative.");
        if (req.Itinerary.Any(d => d.DayNumber < 1 || d.DayNumber > req.DurationDays)) throw new ValidationException("Itinerary days must fall within the duration.");

        p.Code = code;
        p.Name = Guard.Required(req.Name, "Name", 150);
        p.Destination = Guard.Required(req.Destination, "Destination", 150);
        p.DurationDays = req.DurationDays;
        p.Summary = req.Summary;
        p.Inclusions = req.Inclusions;
        p.Exclusions = req.Exclusions;
        p.AdultPrice = req.AdultPrice;
        p.ChildPrice = req.ChildPrice;
        p.SingleSupplement = req.SingleSupplement;
        p.TaxRateId = req.TaxRateId;
        p.IncomeAccountId = req.IncomeAccountId;
        p.IsActive = req.IsActive;
        foreach (var d in req.Itinerary.OrderBy(d => d.DayNumber))
            p.Itinerary.Add(new ItineraryDay { TourPackageId = p.Id, DayNumber = d.DayNumber, Title = Guard.Required(d.Title, "Day title", 150), Description = d.Description, Overnight = d.Overnight, Meals = d.Meals });
        await db.SaveChangesAsync(ct);
        return (await PackagesAsync(false, ct)).First(x => x.Id == p.Id);
    }

    private static TourPackageDto ToDto(TourPackage p, int upcoming) => new(p.Id, p.EntityId, p.Entity!.Name, p.Code, p.Name, p.Destination, p.DurationDays,
        p.Summary, p.Inclusions, p.Exclusions, p.AdultPrice, p.ChildPrice, p.SingleSupplement, p.TaxRateId, p.IncomeAccountId, p.IsActive, upcoming,
        p.Itinerary.OrderBy(d => d.DayNumber).Select(d => new ItineraryDayDto(d.DayNumber, d.Title, d.Description, d.Overnight, d.Meals)).ToList());

    // ======================= Guides =======================

    public async Task<List<GuideDto>> GuidesAsync(CancellationToken ct)
    {
        await EnsureTravelUserAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var guides = await db.Guides.OrderBy(g => g.FullName).ToListAsync(ct);
        var assignments = await db.DepartureGuides.Join(db.TourDepartures, dg => dg.TourDepartureId, d => d.Id, (dg, d) => new { dg.GuideId, d.Code, d.StartDate, d.Status })
            .Where(x => x.StartDate >= today && x.Status != DepartureStatus.Cancelled).ToListAsync(ct);
        return guides.Select(g => new GuideDto(g.Id, g.FullName, g.Phone, g.Languages, g.LicenseNo, g.DailyRate, g.EmployeeId, g.IsActive,
            assignments.Where(a => a.GuideId == g.Id).OrderBy(a => a.StartDate).Select(a => $"{a.Code} ({a.StartDate:dd MMM})").ToList())).ToList();
    }

    public async Task<GuideDto> SaveGuideAsync(Guid? id, SaveGuideRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(id == null ? Permissions.GuidesCreate : Permissions.GuidesEdit, ct);
        var g = id == null ? null : await db.Guides.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Guide");
        if (g == null) { g = new Guide { TenantId = currentUser.TenantId!.Value }; db.Guides.Add(g); }
        g.FullName = Guard.Required(req.FullName, "Name");
        g.Phone = req.Phone;
        g.Languages = req.Languages;
        g.LicenseNo = req.LicenseNo;
        g.DailyRate = Math.Max(0, req.DailyRate);
        g.EmployeeId = req.EmployeeId;
        g.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await GuidesAsync(ct)).First(x => x.Id == g.Id);
    }

    // ======================= Departures =======================

    public async Task<List<DepartureListItem>> DeparturesAsync(Guid? packageId, DateOnly? from, DateOnly? to, bool openOnly, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.DeparturesView, ct);
        // Travel consultants sell seats, so they see departures of entities where they can book.
        visible.UnionWith(await access.EntitiesWithAsync(Permissions.BookingsCreate, ct));
        var q = db.TourDepartures.Where(d => visible.Contains(d.EntityId));
        if (packageId != null) q = q.Where(d => d.TourPackageId == packageId);
        if (from != null) q = q.Where(d => d.EndDate >= from);
        if (to != null) q = q.Where(d => d.StartDate <= to);
        if (openOnly) q = q.Where(d => d.Status == DepartureStatus.Open);
        var rows = await q.OrderBy(d => d.StartDate).Select(d => new
        {
            d.Id, d.Code, d.TourPackageId, Package = d.TourPackage!.Name, d.TourPackage.Destination, d.StartDate, d.EndDate, d.Capacity, d.AdultPrice, d.Status,
            Guides = d.Guides.Select(g => g.Guide!.FullName).ToList()
        }).ToListAsync(ct);
        var booked = await BookedSeatsAsync(rows.Select(r => r.Id).ToList(), null, ct);
        return rows.Select(r =>
        {
            var b = booked.GetValueOrDefault(r.Id);
            return new DepartureListItem(r.Id, r.Code, r.TourPackageId, r.Package, r.Destination, r.StartDate, r.EndDate, r.Capacity, b, r.Capacity - b,
                r.Capacity == 0 ? 0 : Math.Round(b * 100m / r.Capacity, 1), r.AdultPrice, r.Status, string.Join(", ", r.Guides));
        }).ToList();
    }

    internal async Task<Dictionary<Guid, int>> BookedSeatsAsync(List<Guid> departureIds, Guid? excludeBooking, CancellationToken ct) =>
        await db.BookingItems.Join(db.TravelBookings, i => i.TravelBookingId, b => b.Id, (i, b) => new { i, b })
            .Where(x => x.i.TourDepartureId != null && departureIds.Contains(x.i.TourDepartureId.Value) && Holding.Contains(x.b.Status) && x.b.Id != excludeBooking)
            .GroupBy(x => x.i.TourDepartureId!.Value).Select(g => new { g.Key, Seats = g.Sum(x => x.i.Adults + x.i.Children) })
            .ToDictionaryAsync(x => x.Key, x => x.Seats, ct);

    public async Task<DepartureDto> DepartureAsync(Guid id, CancellationToken ct)
    {
        var d = await db.TourDepartures.Include(x => x.TourPackage).Include(x => x.Guides).ThenInclude(g => g.Guide)
                    .Include(x => x.Costs).ThenInclude(c => c.Vendor).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
        if (!await access.HasAsync(Permissions.DeparturesView, d.EntityId, ct)) await access.EnsureAsync(Permissions.BookingsCreate, d.EntityId, ct);
        var booked = (await BookedSeatsAsync([id], null, ct)).GetValueOrDefault(id);
        var revenue = await db.BookingItems.Join(db.TravelBookings, i => i.TravelBookingId, b => b.Id, (i, b) => new { i, b })
            .Where(x => x.i.TourDepartureId == id && Holding.Contains(x.b.Status)).SumAsync(x => x.i.Quantity * x.i.UnitPrice, ct);
        var billIds = d.Costs.Where(c => c.BillId != null).Select(c => c.BillId!.Value).ToList();
        var bills = await db.FinanceDocuments.Where(b => billIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Number, ct);
        var days = d.EndDate.DayNumber - d.StartDate.DayNumber + 1;
        var guideCost = d.Guides.Sum(g => g.Guide!.DailyRate * days);
        var cost = d.Costs.Sum(c => c.Amount) + guideCost;
        return new DepartureDto(d.Id, d.EntityId, d.Code, d.TourPackageId, d.TourPackage!.Name, d.TourPackage.Destination, d.StartDate, d.EndDate, d.Capacity,
            booked, d.Capacity - booked, d.AdultPrice, d.ChildPrice, d.Status, d.Notes,
            d.Guides.Select(g => new DepartureGuideDto(g.Id, g.GuideId, g.Guide!.FullName, g.Guide.Phone, g.Role, g.Guide.DailyRate)).ToList(),
            d.Costs.Select(c => new DepartureCostDto(c.Id, c.Description, c.VendorId, c.Vendor?.Name, c.Amount, c.BillId,
                c.BillId is { } b && bills.TryGetValue(b, out var n) && !string.IsNullOrEmpty(n) ? n : c.BillId == null ? null : "Draft")).ToList(),
            revenue, cost, revenue - cost);
    }

    public async Task<DepartureDto> SaveDepartureAsync(Guid? id, SaveDepartureRequest req, CancellationToken ct)
    {
        var pkg = await db.TourPackages.FirstOrDefaultAsync(p => p.Id == req.TourPackageId, ct) ?? throw new NotFoundException("Package");
        TourDeparture d;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.DeparturesCreate, pkg.EntityId, ct);
            if (!pkg.IsActive) throw new ValidationException("The package is inactive.");
            d = new TourDeparture { TenantId = currentUser.TenantId!.Value, EntityId = pkg.EntityId, TourPackageId = pkg.Id };
            db.TourDepartures.Add(d);
        }
        else
        {
            d = await db.TourDepartures.Include(x => x.Guides).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
            await access.EnsureAsync(Permissions.DeparturesEdit, d.EntityId, ct);
            if (d.TourPackageId != pkg.Id) throw new ValidationException("A departure can't switch packages.");
            var booked = (await BookedSeatsAsync([d.Id], null, ct)).GetValueOrDefault(d.Id);
            if (req.Capacity < booked) throw new ValidationException($"{booked} seats are already sold; capacity can't go below that.");
        }
        if (req.Capacity is < 1 or > 500) throw new ValidationException("Capacity must be 1–500 seats.");
        d.StartDate = req.StartDate;
        d.EndDate = req.StartDate.AddDays(pkg.DurationDays - 1);
        d.Capacity = req.Capacity;
        d.AdultPrice = req.AdultPrice ?? pkg.AdultPrice;
        d.ChildPrice = req.ChildPrice ?? pkg.ChildPrice;
        d.Notes = req.Notes;
        d.Code = $"{pkg.Code}-{req.StartDate:yyMMdd}";
        if (await db.TourDepartures.AnyAsync(x => x.Code == d.Code && x.Id != d.Id && x.Status != DepartureStatus.Cancelled, ct))
            throw new ValidationException($"{pkg.Name} already departs on {req.StartDate:dd MMM yyyy}.");
        foreach (var g in d.Guides) await EnsureGuideFreeAsync(g.GuideId, d, ct);
        await db.SaveChangesAsync(ct);
        return await DepartureAsync(d.Id, ct);
    }

    public async Task<DepartureDto> SetStatusAsync(Guid id, SetDepartureStatusRequest req, CancellationToken ct)
    {
        var d = await db.TourDepartures.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
        await access.EnsureAsync(Permissions.DeparturesEdit, d.EntityId, ct);
        if (req.Status == DepartureStatus.Cancelled && (await BookedSeatsAsync([id], null, ct)).GetValueOrDefault(id) > 0)
            throw new ValidationException("Cancel or move the confirmed bookings on this departure first.");
        d.Status = req.Status;
        await db.SaveChangesAsync(ct);
        return await DepartureAsync(id, ct);
    }

    public async Task<DepartureDto> AssignGuideAsync(Guid id, AssignGuideRequest req, CancellationToken ct)
    {
        var d = await db.TourDepartures.Include(x => x.Guides).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
        await access.EnsureAsync(Permissions.DeparturesEdit, d.EntityId, ct);
        if (d.Guides.Any(g => g.GuideId == req.GuideId)) throw new ValidationException("That guide is already on this departure.");
        var guide = await db.Guides.FirstOrDefaultAsync(g => g.Id == req.GuideId && g.IsActive, ct) ?? throw new NotFoundException("Guide");
        await EnsureGuideFreeAsync(guide.Id, d, ct);
        db.DepartureGuides.Add(new DepartureGuide { TourDepartureId = d.Id, GuideId = guide.Id, Role = req.Role });
        await db.SaveChangesAsync(ct);
        return await DepartureAsync(id, ct);
    }

    public async Task<DepartureDto> RemoveGuideAsync(Guid id, Guid assignmentId, CancellationToken ct)
    {
        var d = await db.TourDepartures.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
        await access.EnsureAsync(Permissions.DeparturesEdit, d.EntityId, ct);
        var a = await db.DepartureGuides.FirstOrDefaultAsync(x => x.Id == assignmentId && x.TourDepartureId == id, ct) ?? throw new NotFoundException("Assignment");
        db.DepartureGuides.Remove(a);
        await db.SaveChangesAsync(ct);
        return await DepartureAsync(id, ct);
    }

    private async Task EnsureGuideFreeAsync(Guid guideId, TourDeparture d, CancellationToken ct)
    {
        var clash = await db.DepartureGuides.Join(db.TourDepartures, dg => dg.TourDepartureId, x => x.Id, (dg, x) => new { dg.GuideId, x })
            .Where(z => z.GuideId == guideId && z.x.Id != d.Id && z.x.Status != DepartureStatus.Cancelled && z.x.StartDate <= d.EndDate && z.x.EndDate >= d.StartDate)
            .Select(z => z.x.Code).FirstOrDefaultAsync(ct);
        if (clash != null) throw new ValidationException($"The guide is already leading {clash} on overlapping dates.");
    }

    public async Task<DepartureDto> SaveCostAsync(Guid id, Guid? costId, SaveDepartureCostRequest req, CancellationToken ct)
    {
        var d = await db.TourDepartures.Include(x => x.Costs).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
        await access.EnsureAsync(Permissions.DeparturesEdit, d.EntityId, ct);
        if (req.Amount < 0) throw new ValidationException("Amount can't be negative.");
        if (req.VendorId is { } v && !await db.Contacts.AnyAsync(c => c.Id == v && c.IsVendor, ct)) throw new ValidationException("Choose a vendor.");
        var c = costId == null ? null : d.Costs.FirstOrDefault(x => x.Id == costId) ?? throw new NotFoundException("Cost");
        if (c?.BillId != null) throw new ValidationException("This cost is already billed; change the bill in Finance.");
        if (c == null) { c = new DepartureCost { TourDepartureId = d.Id }; db.DepartureCosts.Add(c); }
        c.Description = Guard.Required(req.Description, "Description", 200);
        c.VendorId = req.VendorId;
        c.Amount = LedgerService.Round(req.Amount);
        await db.SaveChangesAsync(ct);
        return await DepartureAsync(id, ct);
    }

    /// <summary>Drafts the vendor bill for a cost line (cost of sales). Accounts approve it in Purchase bills.</summary>
    public async Task<DepartureDto> BillCostAsync(Guid id, Guid costId, CancellationToken ct)
    {
        var d = await db.TourDepartures.Include(x => x.Costs).Include(x => x.TourPackage).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
        await access.EnsureAsync(Permissions.DeparturesEdit, d.EntityId, ct);
        var c = d.Costs.FirstOrDefault(x => x.Id == costId) ?? throw new NotFoundException("Cost");
        if (c.BillId != null) throw new ValidationException("Already billed.");
        if (c.VendorId == null) throw new ValidationException("Choose the vendor before billing.");
        var cos = await db.Accounts.Where(a => a.Code == "5100" && !a.IsGroup).Select(a => a.Id).FirstAsync(ct);
        var settings = await ledger.SettingsAsync(ct);
        var bill = await documents.CreateAsync(DocumentKind.Bill, new SaveDocumentRequest(d.EntityId, c.VendorId.Value, DateOnly.FromDateTime(DateTime.UtcNow), null,
            d.Code, $"Tour {d.TourPackage!.Name} departing {d.StartDate:dd MMM yyyy}", settings.BaseCurrency, 1,
            [new DocumentLineInput($"{c.Description} — {d.Code}", cos, 1, c.Amount, null)]), ct, system: true);
        c.BillId = bill.Id;
        await db.SaveChangesAsync(ct);
        return await DepartureAsync(id, ct);
    }

    public async Task<List<ManifestRow>> ManifestAsync(Guid id, CancellationToken ct)
    {
        var d = await db.TourDepartures.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Departure");
        if (!await access.HasAsync(Permissions.DeparturesView, d.EntityId, ct)) await access.EnsureAsync(Permissions.BookingsView, d.EntityId, ct);
        var bookings = await db.TravelBookings.Where(b => Holding.Contains(b.Status) && b.Items.Any(i => i.TourDepartureId == id))
            .Include(b => b.Customer).Include(b => b.Passengers).ToListAsync(ct);
        return bookings.SelectMany(b => b.Passengers.Select(p => new ManifestRow(b.Number, b.Customer!.Name, p.FullName, p.Type, p.PassportNo, p.PassportExpiry,
                p.Nationality, p.Cnic, p.Phone, PassportWarning(p, d.EndDate))))
            .OrderBy(r => r.BookingNumber).ThenBy(r => r.PassengerName).ToList();
    }

    /// <summary>Many countries require 6 months of passport validity beyond travel.</summary>
    internal static bool PassportWarning(BookingPassenger p, DateOnly? travelEnd) =>
        p.PassportNo != null && travelEnd != null && (p.PassportExpiry == null || p.PassportExpiry < travelEnd.Value.AddMonths(6));

    // ---------------- helpers ----------------

    private async Task<HashSet<Guid>> TravelEntitiesAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        return mine.ByEntity.Where(kv => kv.Value.Any(p => p.StartsWith("tourism.") || p.StartsWith("travel."))).Select(kv => kv.Key).ToHashSet();
    }

    private async Task EnsureTravelUserAsync(CancellationToken ct)
    {
        if ((await TravelEntitiesAsync(ct)).Count == 0) throw new ForbiddenException();
    }
}
