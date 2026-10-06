using Erpos.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Hr;

/// <summary>
/// Working days for the organization: weekly offs from settings plus holidays. A holiday set on an entity
/// also applies to its sub-entities; one without an entity applies everywhere.
/// </summary>
public class WorkCalendar
{
    private readonly HashSet<DayOfWeek> _weeklyOffs;
    private readonly List<(DateOnly Date, string Name, string? Path)> _holidays;

    private WorkCalendar(HashSet<DayOfWeek> weeklyOffs, List<(DateOnly, string, string?)> holidays)
    {
        _weeklyOffs = weeklyOffs;
        _holidays = holidays;
    }

    public static async Task<WorkCalendar> LoadAsync(IAppDbContext db, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var offs = await db.HrSettings.Select(s => s.WeeklyOffDays).FirstOrDefaultAsync(ct) ?? "Sunday";
        var holidays = await db.Holidays.Where(h => h.Date >= from && h.Date <= to)
            .Select(h => new
            {
                h.Date, h.Name,
                Path = h.EntityId == null ? null : db.Entities.Where(e => e.Id == h.EntityId).Select(e => e.Path).FirstOrDefault()
            })
            .ToListAsync(ct);
        return new WorkCalendar(ParseWeeklyOffs(offs), holidays.Select(h => (h.Date, h.Name, h.Path)).ToList());
    }

    public static HashSet<DayOfWeek> ParseWeeklyOffs(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => Enum.TryParse<DayOfWeek>(d, true, out var day) ? (DayOfWeek?)day : null)
            .Where(d => d.HasValue).Select(d => d!.Value).ToHashSet();

    /// <summary>"Weekly off", "Holiday: …", or null for a working day — for an entity identified by its path.</summary>
    public string? DayType(DateOnly date, string entityPath)
    {
        var holiday = _holidays.FirstOrDefault(h => h.Date == date && (h.Path == null || entityPath.StartsWith(h.Path)));
        if (holiday.Name != null) return $"Holiday: {holiday.Name}";
        return _weeklyOffs.Contains(date.DayOfWeek) ? "Weekly off" : null;
    }

    public bool IsWorkingDay(DateOnly date, string entityPath) => DayType(date, entityPath) == null;

    public IEnumerable<DateOnly> WorkingDays(DateOnly from, DateOnly to, string entityPath)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
            if (IsWorkingDay(d, entityPath)) yield return d;
    }
}
