using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Payroll;
using Erpos.Application.Services;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Hr;

public class EmployeeService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, IPasswordHasher hasher,
    UserRules rules)
{
    // ---------------- Departments ----------------

    public async Task<List<DepartmentDto>> ListDepartmentsAsync(Guid? entityId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.DepartmentsView, ct);
        // Employees pick departments in forms, so also expose those of entities where they can manage employees.
        visible.UnionWith(await access.EntitiesWithAsync(Permissions.EmployeesCreate, ct));
        visible.UnionWith(await access.EntitiesWithAsync(Permissions.EmployeesEdit, ct));
        var q = db.Departments.Where(d => visible.Contains(d.EntityId));
        if (entityId != null) q = q.Where(d => d.EntityId == entityId);
        return await q.OrderBy(d => d.Entity!.Depth).ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.EntityId, d.Entity!.Name, d.Name, d.Code, d.HeadEmployeeId,
                d.HeadEmployee == null ? null : d.HeadEmployee.User!.FullName,
                db.Employees.Count(e => e.DepartmentId == d.Id)))
            .ToListAsync(ct);
    }

    public async Task<DepartmentDto> SaveDepartmentAsync(Guid? id, SaveDepartmentRequest req, CancellationToken ct)
    {
        Department dept;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.DepartmentsCreate, req.EntityId, ct);
            dept = new Department { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId };
            db.Departments.Add(dept);
        }
        else
        {
            dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Department");
            await access.EnsureAsync(Permissions.DepartmentsEdit, dept.EntityId, ct);
            if (req.EntityId != dept.EntityId)
            {
                await access.EnsureAsync(Permissions.DepartmentsCreate, req.EntityId, ct);
                dept.EntityId = req.EntityId;
            }
        }

        var code = Guard.Code(req.Code);
        if (await db.Departments.AnyAsync(d => d.EntityId == req.EntityId && d.Code == code && d.Id != dept.Id, ct))
            throw new ValidationException($"Department code '{code}' already exists in this entity.");
        if (req.HeadEmployeeId != null && !await db.Employees.AnyAsync(e => e.Id == req.HeadEmployeeId, ct))
            throw new NotFoundException("Head employee");

        dept.Name = Guard.Required(req.Name, "Name");
        dept.Code = code;
        dept.HeadEmployeeId = req.HeadEmployeeId;
        await db.SaveChangesAsync(ct);
        return (await ListDepartmentsAsync(dept.EntityId, ct)).First(d => d.Id == dept.Id);
    }

    public async Task DeleteDepartmentAsync(Guid id, CancellationToken ct)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Department");
        await access.EnsureAsync(Permissions.DepartmentsDelete, dept.EntityId, ct);
        if (await db.Employees.AnyAsync(e => e.DepartmentId == id, ct))
            throw new ValidationException("Move the employees of this department first.");
        dept.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Designations (organization-wide) ----------------

    public Task<List<DesignationDto>> ListDesignationsAsync(CancellationToken ct) =>
        db.Designations.OrderBy(d => d.Title).Select(d => new DesignationDto(d.Id, d.Title, d.Grade)).ToListAsync(ct);

    public async Task<DesignationDto> SaveDesignationAsync(Guid? id, SaveDesignationRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.DepartmentsCreate, ct);
        var d = id == null ? null : await db.Designations.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Designation");
        if (d == null)
        {
            d = new Designation { TenantId = currentUser.TenantId!.Value };
            db.Designations.Add(d);
        }
        var title = Guard.Required(req.Title, "Title", 100);
        if (await db.Designations.AnyAsync(x => x.Title == title && x.Id != d.Id, ct))
            throw new ValidationException($"Designation '{title}' already exists.");
        d.Title = title;
        d.Grade = req.Grade;
        await db.SaveChangesAsync(ct);
        return new DesignationDto(d.Id, d.Title, d.Grade);
    }

    public async Task DeleteDesignationAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.DepartmentsDelete, ct);
        var d = await db.Designations.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Designation");
        if (await db.Employees.AnyAsync(e => e.DesignationId == id, ct))
            throw new ValidationException("This designation is used by employees.");
        d.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Employees ----------------

    public async Task<PagedResult<EmployeeListItem>> ListAsync(Guid? entityId, bool includeSubEntities, Guid? departmentId,
        EmployeeStatus? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.EmployeesView, ct);
        var q = db.Employees.Where(e => visible.Contains(e.EntityId));

        if (entityId is { } eid)
        {
            if (includeSubEntities)
            {
                var path = await db.Entities.Where(e => e.Id == eid).Select(e => e.Path).FirstOrDefaultAsync(ct)
                           ?? throw new NotFoundException("Entity");
                q = q.Where(e => e.Entity!.Path.StartsWith(path));
            }
            else q = q.Where(e => e.EntityId == eid);
        }
        if (departmentId != null) q = q.Where(e => e.DepartmentId == departmentId);
        if (status != null) q = q.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(e => e.User!.FullName.ToLower().Contains(s) || e.EmployeeCode.ToLower().Contains(s) ||
                             e.User.Email.Contains(s) || (e.Cnic != null && e.Cnic.Contains(s)));
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(e => e.EmployeeCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EmployeeListItem(e.Id, e.EmployeeCode, e.UserId, e.User!.FullName, e.User.Email, e.User.Phone,
                e.EntityId, e.Entity!.Name, e.Department == null ? null : e.Department.Name,
                e.Designation == null ? null : e.Designation.Title,
                e.Manager == null ? null : e.Manager.User!.FullName, e.EmploymentType, e.Status, e.JoinDate))
            .ToListAsync(ct);
        return new PagedResult<EmployeeListItem>(items, total, page, pageSize);
    }

    /// <summary>HR with view rights, or the employee themself.</summary>
    public async Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await Project(db.Employees.Where(e => e.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Employee");
        if (dto.UserId != currentUser.UserId) await access.EnsureAsync(Permissions.EmployeesView, dto.EntityId, ct);
        return dto;
    }

    public Task<EmployeeDto?> GetByUserAsync(Guid userId, CancellationToken ct) =>
        Project(db.Employees.Where(e => e.UserId == userId)).FirstOrDefaultAsync(ct);

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest req, CancellationToken ct)
    {
        var d = req.Data;
        await access.EnsureAsync(Permissions.EmployeesCreate, d.EntityId, ct);
        rules.EnsureCanManageType(req.UserType);
        if (req.UserType is not (UserType.Employee or UserType.Manager or UserType.Admin))
            throw new ValidationException("Employees can be created as Employee, Manager or Admin users.");
        if (req.MonthlyGross is > 0) await access.EnsureAsync(Permissions.SalaryCreate, d.EntityId, ct);

        var email = Guard.Email(req.Email);
        Guard.Password(req.Password);
        await rules.EnsureEmailFreeAsync(email, ct);
        await rules.EnsureSeatAvailableAsync(ct);

        var tenantId = currentUser.TenantId!.Value;
        var user = new User
        {
            TenantId = tenantId, Email = email, UserType = req.UserType, PasswordHash = hasher.Hash(req.Password)
        };
        var emp = new Employee { TenantId = tenantId, UserId = user.Id };
        await ApplyAsync(emp, user, d, ct);
        db.Users.Add(user);
        db.Employees.Add(emp);

        var roleId = req.RoleId;
        if (roleId != null) await rules.EnsureCanAssignRoleAsync(roleId.Value, d.EntityId, ct);
        else roleId = await rules.BaselineRoleIdAsync(ct); // the self-service baseline every employee gets
        if (roleId != null)
            db.UserRoleAssignments.Add(new UserRoleAssignment { TenantId = tenantId, UserId = user.Id, RoleId = roleId.Value, EntityId = d.EntityId });

        if (req.MonthlyGross is > 0 and var gross)
        {
            var components = await db.PayComponents.ToDictionaryAsync(c => c.Code, c => c.Id, ct);
            var salary = new EmployeeSalary
            {
                TenantId = tenantId, EntityId = d.EntityId, EmployeeId = emp.Id, EffectiveFrom = d.JoinDate,
                Remarks = "Initial salary"
            };
            foreach (var (code, amount) in HrDefaults.SplitGross(gross))
                if (components.TryGetValue(code, out var cid))
                    salary.Lines.Add(new EmployeeSalaryLine { EmployeeSalaryId = salary.Id, PayComponentId = cid, Amount = amount });
            db.EmployeeSalaries.Add(salary);
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(emp.Id, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, EmployeeData d, CancellationToken ct)
    {
        var emp = await db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee");
        await access.EnsureAsync(Permissions.EmployeesEdit, emp.EntityId, ct);
        if (d.EntityId != emp.EntityId) await access.EnsureAsync(Permissions.EmployeesCreate, d.EntityId, ct);

        var willBeActive = d.Status == EmployeeStatus.Active;
        if (emp.User!.IsActive != willBeActive)
        {
            if (emp.UserId == currentUser.UserId) throw new ValidationException("You cannot change your own employment status.");
            rules.EnsureCanManageType(emp.User.UserType);
        }

        await ApplyAsync(emp, emp.User, d, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var emp = await db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee");
        await access.EnsureAsync(Permissions.EmployeesDelete, emp.EntityId, ct);
        rules.EnsureCanManageType(emp.User!.UserType);
        if (emp.UserId == currentUser.UserId) throw new ValidationException("You cannot delete your own employee record.");
        if (await db.Payslips.AnyAsync(p => p.EmployeeId == id, ct))
            throw new ValidationException("This employee has payslips. Set the status to Resigned/Terminated instead.");

        emp.IsDeleted = true;
        emp.User.IsDeleted = true;
        emp.User.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Employee emp, User user, EmployeeData d, CancellationToken ct)
    {
        var code = Guard.Code(d.EmployeeCode, "Employee code");
        if (await db.Employees.AnyAsync(e => e.EmployeeCode == code && e.Id != emp.Id, ct))
            throw new ValidationException($"Employee code '{code}' is already used.");
        if (!await db.Entities.AnyAsync(e => e.Id == d.EntityId, ct)) throw new NotFoundException("Entity");
        if (d.DepartmentId != null && !await db.Departments.AnyAsync(x => x.Id == d.DepartmentId, ct)) throw new NotFoundException("Department");
        if (d.DesignationId != null && !await db.Designations.AnyAsync(x => x.Id == d.DesignationId, ct)) throw new NotFoundException("Designation");
        if (d.Status != EmployeeStatus.Active && d.ExitDate == null)
            throw new ValidationException("Exit date is required when the employee is no longer active.");
        if (d.ExitDate != null && d.ExitDate < d.JoinDate) throw new ValidationException("Exit date is before the joining date.");
        if (d.Cnic != null && !System.Text.RegularExpressions.Regex.IsMatch(d.Cnic, @"^\d{5}-?\d{7}-?\d$"))
            throw new ValidationException("CNIC must be 13 digits (e.g. 12345-1234567-1).");
        await EnsureNoManagerCycleAsync(emp.Id, d.ManagerId, ct);

        emp.EmployeeCode = code;
        emp.EntityId = d.EntityId;
        emp.DepartmentId = d.DepartmentId;
        emp.DesignationId = d.DesignationId;
        emp.ManagerId = d.ManagerId;
        emp.EmploymentType = d.EmploymentType;
        emp.Status = d.Status;
        emp.JoinDate = d.JoinDate;
        emp.ConfirmationDate = d.ConfirmationDate;
        emp.ExitDate = d.Status == EmployeeStatus.Active ? null : d.ExitDate;
        emp.FatherName = d.FatherName;
        emp.Cnic = d.Cnic;
        emp.Gender = d.Gender;
        emp.DateOfBirth = d.DateOfBirth;
        emp.Address = d.Address;
        emp.City = d.City;
        emp.EmergencyContactName = d.EmergencyContactName;
        emp.EmergencyContactPhone = d.EmergencyContactPhone;
        emp.BankName = d.BankName;
        emp.BankAccountTitle = d.BankAccountTitle;
        emp.Iban = d.Iban?.Replace(" ", "").ToUpperInvariant();
        emp.Ntn = d.Ntn;
        emp.EobiNumber = d.EobiNumber;
        emp.EobiMember = d.EobiMember;
        emp.ProvidentFundMember = d.ProvidentFundMember;
        emp.SocialSecurityMember = d.SocialSecurityMember;

        // The login follows the HR record: same name and home entity; it is disabled once the person leaves.
        user.FullName = Guard.Required(d.FullName, "Full name");
        user.Phone = d.Phone;
        user.PrimaryEntityId = d.EntityId;
        user.IsActive = d.Status == EmployeeStatus.Active;
    }

    private async Task EnsureNoManagerCycleAsync(Guid employeeId, Guid? managerId, CancellationToken ct)
    {
        if (managerId == null) return;
        if (!await db.Employees.AnyAsync(e => e.Id == managerId, ct)) throw new NotFoundException("Manager");

        var seen = new HashSet<Guid> { employeeId };
        var current = managerId;
        while (current != null)
        {
            if (!seen.Add(current.Value)) throw new ValidationException("That manager reports to this employee (circular reporting line).");
            current = await db.Employees.Where(e => e.Id == current).Select(e => e.ManagerId).FirstOrDefaultAsync(ct);
        }
    }

    private static IQueryable<EmployeeDto> Project(IQueryable<Employee> q) => q.Select(e => new EmployeeDto(
        e.Id, e.EmployeeCode, e.UserId, e.User!.FullName, e.User.Email, e.User.Phone, e.User.UserType, e.EntityId,
        e.Entity!.Name, e.DepartmentId, e.Department == null ? null : e.Department.Name, e.DesignationId,
        e.Designation == null ? null : e.Designation.Title, e.ManagerId,
        e.Manager == null ? null : e.Manager.User!.FullName, e.EmploymentType, e.Status, e.JoinDate, e.ConfirmationDate,
        e.ExitDate, e.FatherName, e.Cnic, e.Gender, e.DateOfBirth, e.Address, e.City, e.EmergencyContactName,
        e.EmergencyContactPhone, e.BankName, e.BankAccountTitle, e.Iban, e.Ntn, e.EobiNumber, e.EobiMember,
        e.ProvidentFundMember, e.SocialSecurityMember));
}
