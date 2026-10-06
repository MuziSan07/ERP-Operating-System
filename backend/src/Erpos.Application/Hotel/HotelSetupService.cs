using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Hotel;

/// <summary>Room types, rooms, the housekeeping board and the guest register.</summary>
public class HotelSetupService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    private async Task<HashSet<Guid>> HotelEntitiesAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        return mine.ByEntity.Where(kv => kv.Value.Any(p => p.StartsWith("hotel."))).Select(kv => kv.Key).ToHashSet();
    }

    public async Task<List<RoomTypeDto>> RoomTypesAsync(Guid? entityId, CancellationToken ct)
    {
        var visible = await HotelEntitiesAsync(ct);
        var q = db.RoomTypes.Where(t => visible.Contains(t.EntityId));
        if (entityId != null) q = q.Where(t => t.EntityId == entityId);
        return await q.OrderBy(t => t.Entity!.Name).ThenBy(t => t.BaseRate)
            .Select(t => new RoomTypeDto(t.Id, t.EntityId, t.Entity!.Name, t.Code, t.Name, t.BaseRate, t.MaxAdults, t.MaxChildren, t.Description,
                t.TaxRateId, t.IncomeAccountId, t.IsActive, db.Rooms.Count(r => r.RoomTypeId == t.Id && r.IsActive))).ToListAsync(ct);
    }

    public async Task<RoomTypeDto> SaveRoomTypeAsync(Guid? id, SaveRoomTypeRequest req, CancellationToken ct)
    {
        var t = id == null ? null : await db.RoomTypes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Room type");
        if (t == null)
        {
            await access.EnsureAsync(Permissions.RoomsCreate, req.EntityId, ct);
            t = new RoomType { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId };
            db.RoomTypes.Add(t);
        }
        else
        {
            await access.EnsureAsync(Permissions.RoomsEdit, t.EntityId, ct);
            if (req.EntityId != t.EntityId) throw new ValidationException("A room type can't move to another property.");
        }
        var code = Guard.Code(req.Code);
        if (await db.RoomTypes.AnyAsync(x => x.EntityId == req.EntityId && x.Code == code && x.Id != t.Id, ct)) throw new ValidationException($"Room type {code} exists at this property.");
        if (req.BaseRate < 0) throw new ValidationException("Rate can't be negative.");
        t.Code = code;
        t.Name = Guard.Required(req.Name, "Name", 100);
        t.BaseRate = req.BaseRate;
        t.MaxAdults = Math.Clamp(req.MaxAdults, 1, 20);
        t.MaxChildren = Math.Clamp(req.MaxChildren, 0, 20);
        t.Description = req.Description;
        t.TaxRateId = req.TaxRateId;
        t.IncomeAccountId = req.IncomeAccountId;
        t.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await RoomTypesAsync(t.EntityId, ct)).First(x => x.Id == t.Id);
    }

    /// <summary>Rooms with current occupancy (doubles as the housekeeping board).</summary>
    public async Task<List<RoomDto>> RoomsAsync(Guid? entityId, CancellationToken ct)
    {
        var visible = await HotelEntitiesAsync(ct);
        var q = db.Rooms.Where(r => visible.Contains(r.EntityId));
        if (entityId != null) q = q.Where(r => r.EntityId == entityId);
        var rooms = await q.OrderBy(r => r.Number).Select(r => new { Room = r, TypeName = r.RoomType!.Name }).ToListAsync(ct);
        var roomIds = rooms.Select(r => r.Room.Id).ToList();
        var inHouse = await db.ReservationRooms
            .Join(db.Reservations, rr => rr.ReservationId, res => res.Id, (rr, res) => new { rr, res })
            .Where(x => x.rr.RoomId != null && roomIds.Contains(x.rr.RoomId.Value) && x.res.Status == ReservationStatus.CheckedIn)
            .Select(x => new { RoomId = x.rr.RoomId!.Value, x.res.Id, x.res.Guest!.FullName, x.res.DepartureDate }).ToListAsync(ct);
        return rooms.Select(r =>
        {
            var occ = inHouse.FirstOrDefault(i => i.RoomId == r.Room.Id);
            return new RoomDto(r.Room.Id, r.Room.EntityId, r.Room.Number, r.Room.RoomTypeId, r.TypeName, r.Room.Floor, r.Room.Housekeeping,
                r.Room.HousekeepingNote, r.Room.IsActive, occ != null, occ?.FullName, occ?.Id, occ?.DepartureDate);
        }).ToList();
    }

    public async Task<RoomDto> SaveRoomAsync(Guid? id, SaveRoomRequest req, CancellationToken ct)
    {
        var r = id == null ? null : await db.Rooms.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Room");
        if (r == null)
        {
            await access.EnsureAsync(Permissions.RoomsCreate, req.EntityId, ct);
            r = new Room { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId };
            db.Rooms.Add(r);
        }
        else await access.EnsureAsync(Permissions.RoomsEdit, r.EntityId, ct);
        var type = await db.RoomTypes.FirstOrDefaultAsync(t => t.Id == req.RoomTypeId, ct) ?? throw new NotFoundException("Room type");
        if (type.EntityId != req.EntityId) throw new ValidationException("The room type belongs to another property.");
        var number = Guard.Required(req.Number, "Room number", 20).ToUpperInvariant();
        if (await db.Rooms.AnyAsync(x => x.EntityId == req.EntityId && x.Number == number && x.Id != r.Id, ct)) throw new ValidationException($"Room {number} already exists.");
        r.EntityId = req.EntityId;
        r.Number = number;
        r.RoomTypeId = type.Id;
        r.Floor = req.Floor;
        r.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await RoomsAsync(r.EntityId, ct)).First(x => x.Id == r.Id);
    }

    public async Task<RoomDto> SetHousekeepingAsync(Guid roomId, HousekeepingUpdate req, CancellationToken ct)
    {
        var r = await db.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, ct) ?? throw new NotFoundException("Room");
        await access.EnsureAsync(Permissions.HousekeepingEdit, r.EntityId, ct);
        r.Housekeeping = req.Status;
        r.HousekeepingNote = req.Note;
        await db.SaveChangesAsync(ct);
        return (await RoomsAsync(r.EntityId, ct)).First(x => x.Id == r.Id);
    }

    // ---------------- Guests ----------------

    public async Task<List<GuestDto>> GuestsAsync(string? search, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.ReservationsView, ct);
        var q = db.Guests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(g => g.FullName.ToLower().Contains(s) || (g.Phone != null && g.Phone.Contains(s)) || (g.Cnic != null && g.Cnic.Contains(s)) ||
                             (g.PassportNo != null && g.PassportNo.ToLower().Contains(s)));
        }
        return await q.OrderBy(g => g.FullName).Take(200).Select(g => new GuestDto(g.Id, g.FullName, g.Phone, g.Email, g.Cnic, g.PassportNo, g.Nationality,
            g.Address, g.City, g.IsVip, g.Notes, db.Reservations.Count(r => r.GuestId == g.Id && r.Status == ReservationStatus.CheckedOut))).ToListAsync(ct);
    }

    public async Task<GuestDto> SaveGuestAsync(Guid? id, SaveGuestRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(id == null ? Permissions.ReservationsCreate : Permissions.ReservationsEdit, ct);
        var g = id == null ? null : await db.Guests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Guest");
        if (g == null) { g = new Guest { TenantId = currentUser.TenantId!.Value }; db.Guests.Add(g); }
        Apply(g, req);
        await db.SaveChangesAsync(ct);
        return (await GuestsAsync(g.FullName, ct)).First(x => x.Id == g.Id);
    }

    public static void Apply(Guest g, SaveGuestRequest req)
    {
        g.FullName = Guard.Required(req.FullName, "Guest name");
        g.Phone = req.Phone;
        g.Email = req.Email;
        g.Cnic = req.Cnic;
        g.PassportNo = req.PassportNo;
        g.Nationality = req.Nationality;
        g.Address = req.Address;
        g.City = req.City;
        g.IsVip = req.IsVip;
        g.Notes = req.Notes;
    }
}
