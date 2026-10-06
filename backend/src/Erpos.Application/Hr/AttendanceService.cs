using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Hr;

/// <summary>Manual attendance. Only exceptions need marking: a working day without a record counts as present.</summary>
public class AttendanceService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    /// <summary>Attendance sheet for one day: every active employee of the entity (and its sub-entities).</summary>
    public async Task<List<AttendanceRow>> DailyAsync(Guid entityId, DateOnly date, bool includeSubEntities, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.AttendanceView, entityId, ct);
        var visible = await access.EntitiesWithAsync(Permissions.AttendanceView, ct);
        var path = await db.Entities.Where(e => e.Id == entityId).Select(e => e.Path).FirstAsync(ct);

        var employees = await db.Employees
            .Where(e => visible.Contains(e.EntityId) && e.JoinDate <= date && (e.ExitDate == null || e.ExitDate >= date))
            .Where(e => includeSubEntities ? e.Entity!.Path.StartsWith(path) : e.EntityId == entityId)
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new { e.Id, e.EmployeeCode, e.User!.FullName, Department = e.Department == null ? null : e.Department.Name, e.Entity!.Path })
            .ToListAsync(ct);
        var ids = employees.Select(e => e.Id).ToList();

        var records = await db.AttendanceRecords.Where(a => a.Date == date && ids.Contains(a.EmployeeId)).ToDictionaryAsync(a => a.EmployeeId, ct);
        var onLeave = await db.LeaveRequests
            .Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= date && l.ToDate >= date && ids.Contains(l.EmployeeId))
            .Select(l => new { l.EmployeeId, l.LeaveType!.Name, l.IsHalfDay }).ToListAsync(ct);
        var calendar = await WorkCalendar.LoadAsync(db, date, date, ct);

        return employees.Select(e =>
        {
            records.TryGetValue(e.Id, out var r);
            var leave = onLeave.FirstOrDefault(l => l.EmployeeId == e.Id);
            var dayType = leave != null ? $"On leave: {leave.Name}{(leave.IsHalfDay ? " (half day)" : "")}" : calendar.DayType(date, e.Path);
            return new AttendanceRow(e.Id, e.EmployeeCode, e.FullName, e.Department, r?.Status, r?.CheckIn, r?.CheckOut, r?.Remarks, dayType);
        }).ToList();
    }

    /// <summary>Saves a day's sheet. An entry with no status clears that day's record.</summary>
    public async Task SaveAsync(SaveAttendanceRequest req, CancellationToken ct)
    {
        if (req.Date > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(14)))
            throw new ValidationException("Attendance cannot be marked for future dates.");

        var ids = req.Entries.Select(e => e.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var existing = await db.AttendanceRecords.Where(a => a.Date == req.Date && ids.Contains(a.EmployeeId)).ToDictionaryAsync(a => a.EmployeeId, ct);
        var locked = await LockedEmployeesAsync(ids, req.Date.Year, req.Date.Month, ct);

        foreach (var entry in req.Entries)
        {
            if (!employees.TryGetValue(entry.EmployeeId, out var emp)) throw new NotFoundException("Employee");
            existing.TryGetValue(emp.Id, out var record);
            await access.EnsureAsync(record == null ? Permissions.AttendanceCreate : Permissions.AttendanceEdit, emp.EntityId, ct);
            if (locked.Contains(emp.Id))
                throw new ValidationException($"Payroll for {req.Date:MMMM yyyy} is already approved for {emp.EmployeeCode}; attendance is locked.");

            if (entry.Status == null)
            {
                if (record != null) db.AttendanceRecords.Remove(record);
                continue;
            }
            if (record == null)
            {
                record = new AttendanceRecord { TenantId = emp.TenantId, EntityId = emp.EntityId, EmployeeId = emp.Id, Date = req.Date };
                db.AttendanceRecords.Add(record);
            }
            record.Status = entry.Status.Value;
            record.CheckIn = entry.CheckIn;
            record.CheckOut = entry.CheckOut;
            record.Remarks = entry.Remarks;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>One employee's month, merging records, approved leave, holidays and weekly offs.</summary>
    public async Task<MonthlyAttendanceDto> MonthlyAsync(Guid employeeId, int year, int month, CancellationToken ct)
    {
        var emp = await db.Employees.Where(e => e.Id == employeeId)
            .Select(e => new { e.Id, e.UserId, e.EntityId, e.User!.FullName, e.Entity!.Path, e.JoinDate, e.ExitDate })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Employee");
        if (emp.UserId != currentUser.UserId) await access.EnsureAsync(Permissions.AttendanceView, emp.EntityId, ct);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var records = await db.AttendanceRecords.Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to)
            .ToDictionaryAsync(a => a.Date, ct);
        var leaves = await db.LeaveRequests
            .Where(l => l.EmployeeId == employeeId && l.Status == LeaveStatus.Approved && l.FromDate <= to && l.ToDate >= from)
            .Select(l => new { l.FromDate, l.ToDate, l.LeaveType!.Name }).ToListAsync(ct);
        var calendar = await WorkCalendar.LoadAsync(db, from, to, ct);

        var days = new List<AttendanceDay>();
        var summary = new Dictionary<string, decimal> { ["Present"] = 0, ["Absent"] = 0, ["Late"] = 0, ["HalfDay"] = 0, ["Leave"] = 0, ["Off"] = 0 };
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            records.TryGetValue(d, out var r);
            var leave = leaves.FirstOrDefault(l => l.FromDate <= d && l.ToDate >= d);
            var dayType = d < emp.JoinDate ? "Not joined" : emp.ExitDate != null && d > emp.ExitDate ? "Left" : calendar.DayType(d, emp.Path);
            days.Add(new AttendanceDay(d, r?.Status, dayType, r?.CheckIn, r?.CheckOut, r?.Remarks, leave?.Name));

            if (d > DateOnly.FromDateTime(DateTime.UtcNow) || dayType is "Not joined" or "Left") continue;
            if (leave != null && calendar.IsWorkingDay(d, emp.Path)) summary["Leave"]++;
            else if (r != null) summary[r.Status switch
            {
                AttendanceStatus.Absent => "Absent", AttendanceStatus.Late => "Late", AttendanceStatus.HalfDay => "HalfDay",
                AttendanceStatus.Leave => "Leave", AttendanceStatus.Holiday or AttendanceStatus.WeeklyOff => "Off", _ => "Present"
            }]++;
            else if (dayType != null) summary["Off"]++;
            else summary["Present"]++;
        }
        return new MonthlyAttendanceDto(emp.Id, emp.FullName, year, month, days, summary);
    }

    // ---------------- Holidays ----------------

    public Task<List<HolidayDto>> ListHolidaysAsync(int year, CancellationToken ct) =>
        db.Holidays.Where(h => h.Date >= new DateOnly(year, 1, 1) && h.Date <= new DateOnly(year, 12, 31)).OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name, h.EntityId,
                h.EntityId == null ? null : db.Entities.Where(e => e.Id == h.EntityId).Select(e => e.Name).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<HolidayDto> AddHolidayAsync(SaveHolidayRequest req, CancellationToken ct)
    {
        if (req.EntityId is { } entityId) await access.EnsureAsync(Permissions.HrSettingsManage, entityId, ct);
        else await access.EnsureAtRootAsync(Permissions.HrSettingsManage, ct);

        var h = new Holiday { TenantId = currentUser.TenantId!.Value, Date = req.Date, Name = Guard.Required(req.Name, "Name"), EntityId = req.EntityId };
        db.Holidays.Add(h);
        await db.SaveChangesAsync(ct);
        return (await ListHolidaysAsync(h.Date.Year, ct)).First(x => x.Id == h.Id);
    }

    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct)
    {
        var h = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Holiday");
        if (h.EntityId is { } entityId) await access.EnsureAsync(Permissions.HrSettingsManage, entityId, ct);
        else await access.EnsureAtRootAsync(Permissions.HrSettingsManage, ct);
        h.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Employees whose payroll for the month is approved or posted — their attendance and leave can't change.</summary>
    public static async Task<HashSet<Guid>> LockedEmployeesAsync(IAppDbContext db, IEnumerable<Guid> employeeIds, int year, int month, CancellationToken ct)
    {
        var ids = employeeIds.ToList();
        return (await db.Payslips
            .Where(p => p.Year == year && p.Month == month && ids.Contains(p.EmployeeId) &&
                        (p.PayrollRun!.Status == PayrollRunStatus.Approved || p.PayrollRun.Status == PayrollRunStatus.Posted))
            .Select(p => p.EmployeeId).ToListAsync(ct)).ToHashSet();
    }

    private Task<HashSet<Guid>> LockedEmployeesAsync(List<Guid> ids, int year, int month, CancellationToken ct) =>
        LockedEmployeesAsync(db, ids, year, month, ct);
}
