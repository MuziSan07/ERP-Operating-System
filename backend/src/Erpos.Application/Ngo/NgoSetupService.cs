using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Ngo;

internal static class NgoCommon
{
    internal static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    internal static async Task<Guid> AccountAsync(IAppDbContext db, string code, CancellationToken ct) =>
        await db.Accounts.Where(a => a.Code == code && !a.IsGroup).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw new ValidationException($"Account {code} is missing from the chart of accounts.");

    /// <summary>Deferred-income account a fund's receipts wait in until spent (null: recognised as income straight away).</summary>
    internal static string? DeferredAccount(FundKind kind) => kind switch { FundKind.Restricted => "2220", FundKind.Zakat => "2230", _ => null };

    /// <summary>Base-currency rate for converting a grant's spending into grant currency: weighted average of money received.</summary>
    internal static async Task<decimal> AverageRateAsync(IAppDbContext db, Grant g, CancellationToken ct)
    {
        var received = await db.GrantTranches.Where(t => t.GrantId == g.Id && t.ReceivedAmount != null)
            .Select(t => new { Amount = t.ReceivedAmount!.Value, Base = t.ReceivedBase!.Value }).ToListAsync(ct);
        var amount = received.Sum(t => t.Amount);
        return amount > 0 ? received.Sum(t => t.Base) / amount : g.AgreementRate;
    }

    internal static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(part / whole * 100, 1);

    internal static decimal TimeElapsed(DateOnly start, DateOnly end)
    {
        var total = end.DayNumber - start.DayNumber;
        if (total <= 0) return 100;
        return Math.Clamp(Math.Round((decimal)(Today.DayNumber - start.DayNumber) / total * 100, 1), 0, 100);
    }

    private static readonly string[] Ones = ["", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
        "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    /// <summary>Pakistani style (lakh, crore): 1,250,000 → "Twelve Lakh Fifty Thousand".</summary>
    internal static string InWords(decimal amount, string currency)
    {
        var whole = (long)Math.Floor(amount);
        var paisa = (int)Math.Round((amount - whole) * 100);
        string Below100(long n) => n < 20 ? Ones[n] : Tens[n / 10] + (n % 10 > 0 ? " " + Ones[n % 10] : "");
        string Below1000(long n) => n >= 100 ? Ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + Below100(n % 100) : "") : Below100(n);
        string Words(long n)
        {
            if (n == 0) return "Zero";
            var parts = new List<string>();
            if (n >= 10_000_000) { parts.Add(Words(n / 10_000_000) + " Crore"); n %= 10_000_000; }
            if (n >= 100_000) { parts.Add(Below100(n / 100_000) + " Lakh"); n %= 100_000; }
            if (n >= 1000) { parts.Add(Below100(n / 1000) + " Thousand"); n %= 1000; }
            if (n > 0) parts.Add(Below1000(n));
            return string.Join(" ", parts);
        }
        var name = currency == "PKR" ? "Rupees" : currency;
        return $"{name} {Words(whole)}{(paisa > 0 ? $" and {Below100(paisa)} Paisa" : "")} Only";
    }
}

/// <summary>Donors (on top of finance contacts) and programs.</summary>
public partial class NgoSetupService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    // ---------------- Donors ----------------

    public async Task<List<DonorDto>> DonorsAsync(string? search, CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        if (!mine.HasAnywhere(Permissions.DonorsView) && !mine.HasAnywhere(Permissions.DonationsCreate) && !mine.HasAnywhere(Permissions.GrantsView))
            throw new ForbiddenException();
        var q = db.Donors.Include(d => d.Contact).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(d => d.Contact!.Name.Contains(search) || d.Contact.Code.Contains(search) || (d.Contact.Cnic != null && d.Contact.Cnic.Contains(search)));
        var donors = await q.OrderBy(d => d.Contact!.Name).Take(500).ToListAsync(ct);
        var ids = donors.Select(d => d.Id).ToList();
        var gifts = await db.Donations.Where(x => ids.Contains(x.DonorId)).GroupBy(x => x.DonorId)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Amount), Count = g.Count(), Last = g.Max(x => x.Date) }).ToListAsync(ct);
        var grants = await db.Grants.Where(g => ids.Contains(g.DonorId) && g.Status != GrantStatus.Cancelled).GroupBy(g => g.DonorId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var received = await db.GrantTranches.Join(db.Grants, t => t.GrantId, g => g.Id, (t, g) => new { t, g })
            .Where(x => ids.Contains(x.g.DonorId) && x.t.ReceivedBase != null).GroupBy(x => x.g.DonorId)
            .Select(g => new { g.Key, Base = g.Sum(x => x.t.ReceivedBase!.Value) }).ToListAsync(ct);
        return donors.Select(d =>
        {
            var c = d.Contact!;
            var gi = gifts.FirstOrDefault(x => x.Key == d.Id);
            return new DonorDto(d.Id, c.Id, c.Code, c.Name, d.Type, c.Email, c.Phone, c.Cnic, c.Ntn, c.Address, c.City, c.Country, d.Notes,
                gi?.Total ?? 0, gi?.Count ?? 0, gi?.Last, grants.FirstOrDefault(x => x.Key == d.Id)?.Count ?? 0, received.FirstOrDefault(x => x.Key == d.Id)?.Base ?? 0);
        }).ToList();
    }

    public async Task<DonorDto> SaveDonorAsync(Guid? id, SaveDonorRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(id == null ? Permissions.DonorsCreate : Permissions.DonorsEdit, ct);
        Donor d;
        if (id == null)
        {
            var contact = new Contact { TenantId = currentUser.TenantId!.Value, Code = $"DNR-{await db.NextSequenceAsync("DNR", ct):D5}" };
            db.Contacts.Add(contact);
            d = new Donor { TenantId = contact.TenantId, ContactId = contact.Id, Contact = contact };
            db.Donors.Add(d);
        }
        else d = await db.Donors.Include(x => x.Contact).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Donor");
        var cnic = string.IsNullOrWhiteSpace(req.Cnic) ? null : req.Cnic.Trim();
        if (cnic != null && !CnicRegex().IsMatch(cnic)) throw new ValidationException("CNIC must look like 35202-1234567-1.");
        var c = d.Contact!;
        c.Name = Guard.Required(req.Name, "Name", 200);
        c.Email = req.Email;
        c.Phone = req.Phone;
        c.Cnic = cnic;
        c.Ntn = req.Ntn;
        c.Address = req.Address;
        c.City = req.City;
        c.Country = req.Country;
        c.IsActive = true;
        d.Type = req.Type;
        d.Notes = req.Notes;
        await db.SaveChangesAsync(ct);
        return (await DonorsAsync(null, ct)).First(x => x.Id == d.Id);
    }

    // ---------------- Programs ----------------

    public async Task<List<ProgramDto>> ProgramsAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        var visible = mine.ByEntity.Where(kv => kv.Value.Any(p => p.StartsWith("ngo."))).Select(kv => kv.Key).ToHashSet();
        var programs = await db.NgoPrograms.Include(p => p.Entity).Where(p => visible.Contains(p.EntityId)).OrderBy(p => p.Code).ToListAsync(ct);
        var ids = programs.Select(p => p.Id).ToList();
        var people = await db.Beneficiaries.Where(b => b.ProgramId != null && ids.Contains(b.ProgramId.Value) && b.IsActive).GroupBy(b => b.ProgramId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var spent = await db.FundExpenses.Where(e => e.ProgramId != null && ids.Contains(e.ProgramId.Value)).GroupBy(e => e.ProgramId)
            .Select(g => new { g.Key, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        return programs.Select(p => new ProgramDto(p.Id, p.EntityId, p.Entity!.Name, p.Code, p.Name, p.Sector, p.Description, p.TargetBeneficiaries, p.IsActive,
            people.FirstOrDefault(x => x.Key == p.Id)?.Count ?? 0, spent.FirstOrDefault(x => x.Key == p.Id)?.Total ?? 0)).ToList();
    }

    public async Task<ProgramDto> SaveProgramAsync(Guid? id, SaveProgramRequest req, CancellationToken ct)
    {
        NgoProgram p;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.ProgramsCreate, req.EntityId, ct);
            p = new NgoProgram { TenantId = currentUser.TenantId!.Value };
            db.NgoPrograms.Add(p);
        }
        else
        {
            p = await db.NgoPrograms.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Program");
            await access.EnsureAsync(Permissions.ProgramsEdit, p.EntityId, ct);
            if (req.EntityId != p.EntityId) await access.EnsureAsync(Permissions.ProgramsEdit, req.EntityId, ct);
        }
        var code = Guard.Code(req.Code);
        if (await db.NgoPrograms.AnyAsync(x => x.Code == code && x.Id != p.Id, ct)) throw new ValidationException($"Program {code} exists.");
        p.EntityId = req.EntityId;
        p.Code = code;
        p.Name = Guard.Required(req.Name, "Name", 150);
        p.Sector = req.Sector;
        p.Description = req.Description;
        p.TargetBeneficiaries = Math.Max(0, req.TargetBeneficiaries);
        p.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await ProgramsAsync(ct)).First(x => x.Id == p.Id);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\d{5}-\d{7}-\d$")]
    internal static partial System.Text.RegularExpressions.Regex CnicRegex();
}
