using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Hr;

/// <summary>
/// Leave types, balances and requests with a multi-step approval chain (e.g. line manager → HR).
/// Steps that can't be resolved (no manager set, approver is the requester) are skipped automatically;
/// if every step is skipped an HR step is added, so no request is ever auto-approved.
/// </summary>
public class LeaveService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    // ---------------- Types & workflow (organization settings) ----------------

    public Task<List<LeaveTypeDto>> ListTypesAsync(CancellationToken ct) =>
        db.LeaveTypes.OrderBy(t => t.Name).Select(t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.DaysPerYear, t.IsPaid,
            t.AllowHalfDay, t.OnlyForGender, t.IsActive)).ToListAsync(ct);

    public async Task<LeaveTypeDto> SaveTypeAsync(Guid? id, SaveLeaveTypeRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.HrSettingsManage, ct);
        var t = id == null ? null : await db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Leave type");
        if (t == null)
        {
            t = new LeaveType { TenantId = currentUser.TenantId!.Value };
            db.LeaveTypes.Add(t);
        }
        var code = Guard.Code(req.Code);
        if (await db.LeaveTypes.AnyAsync(x => x.Code == code && x.Id != t.Id, ct))
            throw new ValidationException($"Leave type code '{code}' already exists.");
        if (req.DaysPerYear < 0 || req.DaysPerYear > 366) throw new ValidationException("Days per year must be between 0 and 366.");

        t.Code = code;
        t.Name = Guard.Required(req.Name, "Name", 100);
        t.DaysPerYear = req.DaysPerYear;
        t.IsPaid = req.IsPaid;
        t.AllowHalfDay = req.AllowHalfDay;
        t.OnlyForGender = req.OnlyForGender;
        t.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return new LeaveTypeDto(t.Id, t.Code, t.Name, t.DaysPerYear, t.IsPaid, t.AllowHalfDay, t.OnlyForGender, t.IsActive);
    }

    public Task<List<LeaveStepDto>> GetWorkflowAsync(CancellationToken ct) =>
        db.LeaveApprovalSteps.OrderBy(s => s.StepOrder).Select(s => new LeaveStepDto(s.Id, s.StepOrder, s.Name, s.ApproverType,
            s.ApproverUserId, s.ApproverUserId == null ? null : db.Users.Where(u => u.Id == s.ApproverUserId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);

    /// <summary>Replaces the chain. Requests already submitted keep the chain they were created with.</summary>
    public async Task<List<LeaveStepDto>> SaveWorkflowAsync(SaveWorkflowRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.HrSettingsManage, ct);
        if (req.Steps.Count is 0 or > 6) throw new ValidationException("Define between 1 and 6 approval steps.");
        foreach (var s in req.Steps.Where(s => s.ApproverType == ApproverType.SpecificUser))
            if (s.ApproverUserId == null || !await db.Users.AnyAsync(u => u.Id == s.ApproverUserId && u.IsActive, ct))
                throw new ValidationException($"Step '{s.Name}' needs an active approver user.");

        db.LeaveApprovalSteps.RemoveRange(await db.LeaveApprovalSteps.ToListAsync(ct));
        var order = 1;
        foreach (var s in req.Steps)
            db.LeaveApprovalSteps.Add(new LeaveApprovalStep
            {
                TenantId = currentUser.TenantId!.Value, StepOrder = order++, Name = Guard.Required(s.Name, "Step name", 100),
                ApproverType = s.ApproverType, ApproverUserId = s.ApproverType == ApproverType.SpecificUser ? s.ApproverUserId : null
            });
        await db.SaveChangesAsync(ct);
        return await GetWorkflowAsync(ct);
    }

    // ---------------- Balances ----------------

    public async Task<List<LeaveBalanceDto>> BalancesAsync(Guid employeeId, int year, CancellationToken ct)
    {
        var emp = await LoadEmployeeAsync(employeeId, Permissions.LeaveView, ct);
        return await ComputeBalancesAsync(emp, year, ct);
    }

    private async Task<List<LeaveBalanceDto>> ComputeBalancesAsync(Employee emp, int year, CancellationToken ct)
    {
        var types = await db.LeaveTypes.Where(t => t.IsActive && (t.OnlyForGender == null || t.OnlyForGender == emp.Gender))
            .OrderBy(t => t.Name).ToListAsync(ct);
        var (yearStart, yearEnd) = (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        var taken = await db.LeaveRequests
            .Where(l => l.EmployeeId == emp.Id && l.FromDate >= yearStart && l.FromDate <= yearEnd && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending))
            .GroupBy(l => new { l.LeaveTypeId, l.Status }).Select(g => new { g.Key.LeaveTypeId, g.Key.Status, Days = g.Sum(x => x.Days) })
            .ToListAsync(ct);

        return types.Select(t =>
        {
            var used = taken.Where(x => x.LeaveTypeId == t.Id && x.Status == LeaveStatus.Approved).Sum(x => x.Days);
            var pending = taken.Where(x => x.LeaveTypeId == t.Id && x.Status == LeaveStatus.Pending).Sum(x => x.Days);
            var entitled = Entitlement(t, emp.JoinDate, year);
            decimal? available = t.DaysPerYear > 0 ? entitled - used - pending : null;
            return new LeaveBalanceDto(t.Id, t.Code, t.Name, t.IsPaid, entitled, used, pending, available);
        }).ToList();
    }

    /// <summary>Full yearly entitlement, prorated by remaining months in the year an employee joins (rounded down to ½ day).</summary>
    private static decimal Entitlement(LeaveType t, DateOnly joinDate, int year)
    {
        if (t.DaysPerYear <= 0 || joinDate.Year > year) return 0;
        if (joinDate.Year < year) return t.DaysPerYear;
        var months = 12 - joinDate.Month + 1;
        return Math.Floor(t.DaysPerYear * months / 12m * 2) / 2;
    }

    // ---------------- Requests ----------------

    public async Task<LeaveRequestDto> ApplyAsync(ApplyLeaveRequest req, CancellationToken ct)
    {
        Employee emp;
        if (req.EmployeeId == null || await db.Employees.AnyAsync(e => e.Id == req.EmployeeId && e.UserId == currentUser.UserId, ct))
        {
            emp = await db.Employees.Include(e => e.Department).Include(e => e.Manager)
                      .FirstOrDefaultAsync(e => e.UserId == currentUser.UserId, ct)
                  ?? throw new ValidationException("Your login is not linked to an employee record.");
            await access.EnsureAsync(Permissions.LeaveCreate, emp.EntityId, ct);
        }
        else
        {
            // HR entering leave on someone's behalf.
            emp = await db.Employees.Include(e => e.Department).Include(e => e.Manager)
                      .FirstOrDefaultAsync(e => e.Id == req.EmployeeId, ct) ?? throw new NotFoundException("Employee");
            await access.EnsureAsync("hr.leave.edit", emp.EntityId, ct);
        }
        if (emp.Status != EmployeeStatus.Active) throw new ValidationException("Leave can only be requested for active employees.");

        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == req.LeaveTypeId && t.IsActive, ct) ?? throw new NotFoundException("Leave type");
        if (req.ToDate < req.FromDate) throw new ValidationException("The end date is before the start date.");
        if (req.FromDate.Year != req.ToDate.Year) throw new ValidationException("Split leave that crosses into a new year into two requests.");
        if (req.FromDate < emp.JoinDate) throw new ValidationException("Leave cannot start before the joining date.");
        if (type.OnlyForGender != null && type.OnlyForGender != emp.Gender) throw new ValidationException($"{type.Name} is not available for this employee.");
        if (req.IsHalfDay && (!type.AllowHalfDay || req.FromDate != req.ToDate))
            throw new ValidationException("A half day must be a single date of a leave type that allows half days.");

        if (await db.LeaveRequests.AnyAsync(l => l.EmployeeId == emp.Id && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved) &&
                                                 l.FromDate <= req.ToDate && l.ToDate >= req.FromDate, ct))
            throw new ValidationException("There is already a leave request covering some of these dates.");

        var path = await db.Entities.Where(e => e.Id == emp.EntityId).Select(e => e.Path).FirstAsync(ct);
        var calendar = await WorkCalendar.LoadAsync(db, req.FromDate, req.ToDate, ct);
        var working = calendar.WorkingDays(req.FromDate, req.ToDate, path).Count();
        var days = req.IsHalfDay ? 0.5m : working;
        if (days <= 0) throw new ValidationException("The selected dates are all weekly offs or holidays.");

        if (type.DaysPerYear > 0)
        {
            var balance = (await ComputeBalancesAsync(emp, req.FromDate.Year, ct)).First(b => b.LeaveTypeId == type.Id);
            if (balance.Available < days)
                throw new ValidationException($"Not enough {type.Name} balance: {balance.Available} day(s) available, {days} requested.");
        }

        var request = new LeaveRequest
        {
            TenantId = emp.TenantId, EntityId = emp.EntityId, EmployeeId = emp.Id, LeaveTypeId = type.Id,
            FromDate = req.FromDate, ToDate = req.ToDate, IsHalfDay = req.IsHalfDay, Days = days, Reason = req.Reason
        };
        await BuildApprovalChainAsync(request, emp, ct);
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(request.Id, ct);
    }

    private async Task BuildApprovalChainAsync(LeaveRequest request, Employee emp, CancellationToken ct)
    {
        var steps = await db.LeaveApprovalSteps.OrderBy(s => s.StepOrder).ToListAsync(ct);
        Guid? headUserId = emp.Department?.HeadEmployeeId is { } headId
            ? await db.Employees.Where(e => e.Id == headId).Select(e => (Guid?)e.UserId).FirstOrDefaultAsync(ct)
            : null;

        var resolvedBefore = new HashSet<Guid>();
        foreach (var s in steps)
        {
            Guid? approver = s.ApproverType switch
            {
                ApproverType.LineManager => emp.Manager?.UserId,
                ApproverType.DepartmentHead => headUserId,
                ApproverType.SpecificUser => s.ApproverUserId,
                _ => null
            };
            // A person never approves their own leave, and doesn't approve the same request twice.
            var skip = s.ApproverType != ApproverType.HrPermission &&
                       (approver == null || approver == emp.UserId || resolvedBefore.Contains(approver.Value));
            if (approver != null && !skip) resolvedBefore.Add(approver.Value);

            request.Approvals.Add(new LeaveApproval
            {
                LeaveRequestId = request.Id, StepOrder = s.StepOrder, StepName = s.Name, ApproverType = s.ApproverType,
                ApproverUserId = approver, Status = skip ? ApprovalStatus.Skipped : ApprovalStatus.Waiting
            });
        }

        if (request.Approvals.All(a => a.Status == ApprovalStatus.Skipped))
            request.Approvals.Add(new LeaveApproval
            {
                LeaveRequestId = request.Id, StepOrder = steps.Count + 1, StepName = "HR (fallback)",
                ApproverType = ApproverType.HrPermission, Status = ApprovalStatus.Waiting
            });

        request.Approvals.OrderBy(a => a.StepOrder).First(a => a.Status == ApprovalStatus.Waiting).Status = ApprovalStatus.Pending;
    }

    public async Task<LeaveRequestDto> DecideAsync(Guid id, LeaveDecisionRequest req, CancellationToken ct)
    {
        var request = await db.LeaveRequests.Include(l => l.Approvals).Include(l => l.Employee)
                          .FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Leave request");
        if (request.Status != LeaveStatus.Pending) throw new ValidationException("This request is no longer pending.");
        var step = request.Approvals.OrderBy(a => a.StepOrder).First(a => a.Status == ApprovalStatus.Pending);
        var earlier = request.Approvals.Where(a => a.StepOrder < step.StepOrder).Select(a => a.ActedByUserId);
        if (!await CanActAsync(step, request.EntityId, request.Employee!.UserId, earlier, ct))
            throw new ForbiddenException("This request is waiting for someone else's approval.");
        if (!req.Approve && string.IsNullOrWhiteSpace(req.Comment))
            throw new ValidationException("Please give a reason when rejecting.");

        step.Status = req.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        step.ActedByUserId = currentUser.UserId;
        step.ActedAt = DateTime.UtcNow;
        step.Comment = req.Comment;

        if (!req.Approve)
        {
            request.Status = LeaveStatus.Rejected;
            foreach (var a in request.Approvals.Where(a => a.Status == ApprovalStatus.Waiting)) a.Status = ApprovalStatus.Skipped;
        }
        else
        {
            var next = request.Approvals.OrderBy(a => a.StepOrder).FirstOrDefault(a => a.Status == ApprovalStatus.Waiting);
            if (next != null) next.Status = ApprovalStatus.Pending;
            else request.Status = LeaveStatus.Approved;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<LeaveRequestDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var request = await db.LeaveRequests.Include(l => l.Employee).Include(l => l.Approvals)
                          .FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Leave request");
        var isOwn = request.Employee!.UserId == currentUser.UserId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.Status is LeaveStatus.Rejected or LeaveStatus.Cancelled) throw new ValidationException("This request is already closed.");
        if (isOwn && !(request.Status == LeaveStatus.Pending || request.FromDate > today))
            throw new ValidationException("Leave that has started can only be cancelled by HR.");
        if (!isOwn) await access.EnsureAsync("hr.leave.edit", request.EntityId, ct);

        var locked = await AttendanceService.LockedEmployeesAsync(db, [request.EmployeeId], request.FromDate.Year, request.FromDate.Month, ct);
        if (locked.Count > 0 && request.Status == LeaveStatus.Approved)
            throw new ValidationException("Payroll for that month is already approved; this leave can't be cancelled.");

        request.Status = LeaveStatus.Cancelled;
        foreach (var a in request.Approvals.Where(a => a.Status is ApprovalStatus.Waiting or ApprovalStatus.Pending)) a.Status = ApprovalStatus.Skipped;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<LeaveRequestDto> GetAsync(Guid id, CancellationToken ct)
    {
        var list = await LoadAsync(db.LeaveRequests.Where(l => l.Id == id), ct);
        var dto = list.FirstOrDefault() ?? throw new NotFoundException("Leave request");
        // Visible to the requester, anyone who is (or was) an approver on it, and HR with view rights.
        var me = currentUser.UserId;
        var ownerUser = await db.Employees.Where(e => e.Id == dto.EmployeeId).Select(e => e.UserId).FirstAsync(ct);
        var involved = dto.CanAct || await db.LeaveApprovals.AnyAsync(a => a.LeaveRequestId == id &&
            (a.ApproverUserId == me || a.ActedByUserId == me), ct);
        if (ownerUser != me && !involved)
        {
            var entityId = await db.LeaveRequests.Where(l => l.Id == id).Select(l => l.EntityId).FirstAsync(ct);
            await access.EnsureAsync(Permissions.LeaveView, entityId, ct);
        }
        return dto;
    }

    public async Task<List<LeaveRequestDto>> MyRequestsAsync(int year, CancellationToken ct)
    {
        var (from, to) = (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        return await LoadAsync(db.LeaveRequests.Where(l => l.Employee!.UserId == currentUser.UserId && l.FromDate >= from && l.FromDate <= to), ct);
    }

    /// <summary>Requests currently waiting on the signed-in user.</summary>
    public async Task<List<LeaveRequestDto>> InboxAsync(CancellationToken ct) =>
        (await LoadAsync(db.LeaveRequests.Where(l => l.Status == LeaveStatus.Pending), ct)).Where(r => r.CanAct).ToList();

    public async Task<PagedResult<LeaveRequestDto>> ListAsync(Guid? entityId, LeaveStatus? status, DateOnly? from, DateOnly? to,
        int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.LeaveView, ct);
        var q = db.LeaveRequests.Where(l => visible.Contains(l.EntityId));
        if (entityId is { } eid)
        {
            var path = await db.Entities.Where(e => e.Id == eid).Select(e => e.Path).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Entity");
            q = q.Where(l => db.Entities.Any(e => e.Id == l.EntityId && e.Path.StartsWith(path)));
        }
        if (status != null) q = q.Where(l => l.Status == status);
        if (from != null) q = q.Where(l => l.ToDate >= from);
        if (to != null) q = q.Where(l => l.FromDate <= to);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await LoadAsync(q.OrderByDescending(l => l.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new PagedResult<LeaveRequestDto>(items, total, page, pageSize);
    }

    // ---------------- helpers ----------------

    /// <param name="earlierApprovers">Users who approved earlier steps — nobody approves the same request twice.</param>
    private async Task<bool> CanActAsync(LeaveApproval step, Guid entityId, Guid requesterUserId,
        IEnumerable<Guid?> earlierApprovers, CancellationToken ct)
    {
        if (currentUser.UserId == requesterUserId) return false;
        if (earlierApprovers.Contains(currentUser.UserId)) return false;
        if (currentUser.IsSuperAdmin) return true;
        return step.ApproverType == ApproverType.HrPermission
            ? await access.HasAsync(Permissions.LeaveApprove, entityId, ct)
            : step.ApproverUserId == currentUser.UserId;
    }

    private async Task<List<LeaveRequestDto>> LoadAsync(IQueryable<LeaveRequest> q, CancellationToken ct)
    {
        var rows = await q.OrderByDescending(l => l.CreatedAt).Select(l => new
        {
            l.Id, l.EmployeeId, EmployeeName = l.Employee!.User!.FullName, l.Employee.EmployeeCode, RequesterUserId = l.Employee.UserId,
            EntityName = db.Entities.Where(e => e.Id == l.EntityId).Select(e => e.Name).FirstOrDefault(), l.EntityId,
            l.LeaveTypeId, LeaveType = l.LeaveType!.Name, l.LeaveType.IsPaid, l.FromDate, l.ToDate, l.IsHalfDay, l.Days,
            l.Reason, l.Status, l.CreatedAt,
            Approvals = l.Approvals.OrderBy(a => a.StepOrder).Select(a => new
            {
                Entity = a,
                ApproverName = a.ApproverUserId == null ? null : db.Users.Where(u => u.Id == a.ApproverUserId).Select(u => u.FullName).FirstOrDefault(),
                ActedBy = a.ActedByUserId == null ? null : db.Users.Where(u => u.Id == a.ActedByUserId).Select(u => u.FullName).FirstOrDefault()
            }).ToList()
        }).ToListAsync(ct);

        var result = new List<LeaveRequestDto>();
        foreach (var r in rows)
        {
            var pending = r.Approvals.FirstOrDefault(a => a.Entity.Status == ApprovalStatus.Pending)?.Entity;
            var canAct = r.Status == LeaveStatus.Pending && pending != null && await CanActAsync(pending, r.EntityId, r.RequesterUserId,
                r.Approvals.Where(a => a.Entity.StepOrder < pending.StepOrder).Select(a => a.Entity.ActedByUserId), ct);
            result.Add(new LeaveRequestDto(r.Id, r.EmployeeId, r.EmployeeName, r.EmployeeCode, r.EntityName ?? "", r.LeaveTypeId,
                r.LeaveType, r.IsPaid, r.FromDate, r.ToDate, r.IsHalfDay, r.Days, r.Reason, r.Status, r.CreatedAt,
                r.Approvals.Select(a => new LeaveApprovalDto(a.Entity.StepOrder, a.Entity.StepName, a.Entity.ApproverType,
                    a.ApproverName ?? (a.Entity.ApproverType == ApproverType.HrPermission ? "HR team" : null),
                    a.Entity.Status, a.ActedBy, a.Entity.ActedAt, a.Entity.Comment)).ToList(), canAct));
        }
        return result;
    }

    private async Task<Employee> LoadEmployeeAsync(Guid id, string permission, CancellationToken ct)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee");
        if (emp.UserId != currentUser.UserId) await access.EnsureAsync(permission, emp.EntityId, ct);
        return emp;
    }

    public async Task<int> PendingForMeCountAsync(CancellationToken ct) => (await InboxAsync(ct)).Count;
}
