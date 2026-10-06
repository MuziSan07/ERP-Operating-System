using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Projects;

internal static class ProjectsCommon
{
    internal static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    /// <summary>Standard month for cost rates: 22 working days Ã— 8 hours.</summary>
    internal const decimal HoursPerMonth = 176;

    /// <summary>Hourly cost from the current salary (earnings Ã· 176). Employees without a salary cost 0 until set on the member.</summary>
    internal static async Task<Dictionary<Guid, decimal>> CostRatesAsync(IAppDbContext db, IEnumerable<Guid> employeeIds, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().ToList();
        var today = Today;
        var salaries = await db.EmployeeSalaries.Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= today)
            .Select(s => new { s.EmployeeId, s.EffectiveFrom, Gross = s.Lines.Where(l => l.PayComponent!.Kind == PayComponentKind.Earning).Sum(l => l.Amount) })
            .ToListAsync(ct);
        return salaries.GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g => Math.Round(g.OrderByDescending(s => s.EffectiveFrom).First().Gross / HoursPerMonth, 2));
    }

    internal static bool CountsAsLogged(TimeEntryStatus s) => s != TimeEntryStatus.Rejected;
}

/// <summary>Per-project figures from time entries and milestones.</summary>
internal record ProjectStats(decimal Logged, decimal Approved, decimal Pending, decimal BillableHours, decimal Billed, decimal Unbilled, decimal Cost)
{
    public decimal Margin => Billed + Unbilled - Cost;
}

/// <summary>Clients, projects (team, rates, budget, milestones) and billing: time &amp; materials hours or fixed-price milestones.</summary>
public class ProjectService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, DocumentService documents)
{
    // ---------------- People & clients ----------------

    public async Task<List<ProjectPerson>> PeopleAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        var visible = mine.ByEntity.Where(kv => kv.Value.Contains(Permissions.ProjectsView) || kv.Value.Contains(Permissions.ProjectsCreate)).Select(kv => kv.Key).ToHashSet();
        if (visible.Count == 0) throw new ForbiddenException();
        var people = await db.Employees.Where(e => visible.Contains(e.EntityId) && e.Status == EmployeeStatus.Active)
            .Select(e => new { e.Id, e.User!.FullName, e.EmployeeCode, Designation = e.Designation == null ? null : e.Designation.Title, Entity = e.Entity!.Name })
            .OrderBy(e => e.FullName).ToListAsync(ct);
        var rates = await ProjectsCommon.CostRatesAsync(db, people.Select(p => p.Id), ct);
        return people.Select(p => new ProjectPerson(p.Id, p.FullName, p.EmployeeCode, p.Designation, p.Entity, rates.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<List<ClientDto>> ClientsAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        if (!mine.HasAnywhere(Permissions.ClientsView) && !mine.HasAnywhere(Permissions.ProjectsView)) throw new ForbiddenException();
        var clients = await db.Contacts.Where(c => c.IsCustomer && c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);
        var projects = await db.Projects.Where(p => p.ClientId != null).Select(p => new { p.Id, p.ClientId, p.Status }).ToListAsync(ct);
        var stats = await StatsAsync(projects.Select(p => p.Id).ToList(), ct);
        var withProjects = projects.Select(p => p.ClientId).ToHashSet();
        return clients.Where(c => withProjects.Contains(c.Id) || c.Code.StartsWith("CL-")).Select(c =>
        {
            var mineP = projects.Where(p => p.ClientId == c.Id).ToList();
            return new ClientDto(c.Id, c.Code, c.Name, c.Email, c.Phone, c.City, c.Ntn, mineP.Count(p => p.Status == ProjectStatus.Active),
                mineP.Sum(p => stats.GetValueOrDefault(p.Id)?.Billed ?? 0), mineP.Sum(p => stats.GetValueOrDefault(p.Id)?.Unbilled ?? 0));
        }).ToList();
    }

    public async Task<ClientDto> SaveClientAsync(Guid? id, SaveClientRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(id == null ? Permissions.ClientsCreate : Permissions.ClientsEdit, ct);
        Contact c;
        if (id == null)
        {
            c = new Contact { TenantId = currentUser.TenantId!.Value, Code = $"CL-{await db.NextSequenceAsync("CLI", ct):D5}", IsCustomer = true };
            db.Contacts.Add(c);
        }
        else c = await db.Contacts.FirstOrDefaultAsync(x => x.Id == id && x.IsCustomer, ct) ?? throw new NotFoundException("Client");
        c.Name = Guard.Required(req.Name, "Name", 200);
        c.Email = req.Email;
        c.Phone = req.Phone;
        c.Address = req.Address;
        c.City = req.City;
        c.Country = req.Country;
        c.Ntn = req.Ntn;
        c.Strn = req.Strn;
        c.PaymentTermsDays = Math.Clamp(req.PaymentTermsDays, 0, 365);
        c.IsActive = true;
        await db.SaveChangesAsync(ct);
        return (await ClientsAsync(ct)).First(x => x.Id == c.Id);
    }

    // ---------------- Projects ----------------

    internal async Task<Dictionary<Guid, ProjectStats>> StatsAsync(List<Guid> ids, CancellationToken ct)
    {
        var rows = await db.TimeEntries.Where(t => ids.Contains(t.ProjectId)).GroupBy(t => new { t.ProjectId, t.Status, t.Billable })
            .Select(g => new { g.Key.ProjectId, g.Key.Status, g.Key.Billable, Hours = g.Sum(t => t.Hours), Cost = g.Sum(t => t.Hours * t.CostRate), Value = g.Sum(t => t.Hours * t.BillRate) })
            .ToListAsync(ct);
        var milestones = await db.ProjectMilestones.Where(m => ids.Contains(m.ProjectId) && m.CompletedOn != null)
            .Select(m => new { m.ProjectId, m.Amount, Invoiced = m.InvoiceId != null }).ToListAsync(ct);
        var types = await db.Projects.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.BillingType, ct);
        return ids.ToDictionary(id => id, id =>
        {
            var r = rows.Where(x => x.ProjectId == id).ToList();
            var done = r.Where(x => x.Status is TimeEntryStatus.Approved or TimeEntryStatus.Invoiced).ToList();
            var fixedPrice = types.GetValueOrDefault(id) == ProjectBillingType.FixedPrice;
            var ms = milestones.Where(m => m.ProjectId == id).ToList();
            return new ProjectStats(
                r.Where(x => ProjectsCommon.CountsAsLogged(x.Status)).Sum(x => x.Hours), done.Sum(x => x.Hours),
                r.Where(x => x.Status == TimeEntryStatus.Submitted).Sum(x => x.Hours), r.Where(x => x.Billable && ProjectsCommon.CountsAsLogged(x.Status)).Sum(x => x.Hours),
                LedgerService.Round(fixedPrice ? ms.Where(m => m.Invoiced).Sum(m => m.Amount) : r.Where(x => x.Billable && x.Status == TimeEntryStatus.Invoiced).Sum(x => x.Value)),
                LedgerService.Round(fixedPrice ? ms.Where(m => !m.Invoiced).Sum(m => m.Amount) : r.Where(x => x.Billable && x.Status == TimeEntryStatus.Approved).Sum(x => x.Value)),
                LedgerService.Round(done.Sum(x => x.Cost)));
        });
    }

    public async Task<List<ProjectListItem>> ListAsync(ProjectStatus? status, Guid? clientId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.ProjectsView, ct);
        var q = db.Projects.Where(p => visible.Contains(p.EntityId));
        if (status != null) q = q.Where(p => p.Status == status);
        if (clientId != null) q = q.Where(p => p.ClientId == clientId);
        return await ListItemsAsync(q, ct);
    }

    internal async Task<List<ProjectListItem>> ListItemsAsync(IQueryable<Project> q, CancellationToken ct)
    {
        var projects = await q.Include(p => p.Client).OrderBy(p => p.Status).ThenBy(p => p.Code).Take(300).ToListAsync(ct);
        var ids = projects.Select(p => p.Id).ToList();
        var stats = await StatsAsync(ids, ct);
        var managers = await ManagerNamesAsync(projects.Select(p => p.ManagerEmployeeId), ct);
        var open = await db.ProjectTasks.Where(t => ids.Contains(t.ProjectId) && t.Status != WorkItemStatus.Done).GroupBy(t => t.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return projects.Select(p =>
        {
            var s = stats[p.Id];
            return new ProjectListItem(p.Id, p.Code, p.Name, p.Client?.Name, p.BillingType, p.Status, p.StartDate, p.EndDate,
                p.ManagerEmployeeId is { } m ? managers.GetValueOrDefault(m) : null, p.BudgetHours, s.Logged, Pct(s.Logged, p.BudgetHours), s.Billed, s.Unbilled, s.Cost,
                s.Margin, open.FirstOrDefault(x => x.Key == p.Id)?.Count ?? 0, OverBudget(p, s));
        }).ToList();
    }

    private static decimal Pct(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(part / whole * 100, 1);
    private static bool OverBudget(Project p, ProjectStats s) =>
        (p.BudgetHours > 0 && s.Logged > p.BudgetHours) || (p.BillingType == ProjectBillingType.FixedPrice && p.ContractAmount > 0 && s.Cost > p.ContractAmount);

    private async Task<Dictionary<Guid, string>> ManagerNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        return (await db.Employees.Where(e => list.Contains(e.Id)).Select(e => new { e.Id, e.User!.FullName }).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.FullName);
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Projects.Include(x => x.Entity).Include(x => x.Client).Include(x => x.Members).ThenInclude(m => m.Employee).ThenInclude(e => e!.User)
                    .Include(x => x.Milestones).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Project");
        await access.EnsureAsync(Permissions.ProjectsView, p.EntityId, ct);
        var s = (await StatsAsync([id], ct))[id];
        var today = ProjectsCommon.Today;
        var byMember = await db.TimeEntries.Where(t => t.ProjectId == id && t.Status != TimeEntryStatus.Rejected).GroupBy(t => new { t.EmployeeId, t.Billable })
            .Select(g => new { g.Key.EmployeeId, g.Key.Billable, Hours = g.Sum(t => t.Hours) }).ToListAsync(ct);
        var invoiceIds = p.Milestones.Where(m => m.InvoiceId != null).Select(m => m.InvoiceId!.Value).ToList();
        var invoices = await db.FinanceDocuments.Where(d => invoiceIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Number, ct);
        var tasks = await db.ProjectTasks.Where(t => t.ProjectId == id).GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var managers = await ManagerNamesAsync([p.ManagerEmployeeId], ct);

        var warnings = new List<string>();
        if (p.BudgetHours > 0 && s.Logged > p.BudgetHours) warnings.Add($"{s.Logged:0.##} hours logged against a budget of {p.BudgetHours:0.##}.");
        else if (p.BudgetHours > 0 && s.Logged > p.BudgetHours * 0.9m) warnings.Add($"{Pct(s.Logged, p.BudgetHours)}% of the hour budget is used.");
        if (p.BillingType == ProjectBillingType.FixedPrice && p.ContractAmount > 0 && s.Cost > p.ContractAmount)
            warnings.Add($"Team cost ({s.Cost:N0}) exceeds the fixed price ({p.ContractAmount:N0}).");
        if (p.BillingType == ProjectBillingType.FixedPrice && p.Milestones.Sum(m => m.Amount) != p.ContractAmount)
            warnings.Add($"Milestones add up to {p.Milestones.Sum(m => m.Amount):N0}, not the contract value of {p.ContractAmount:N0}.");
        foreach (var m in p.Milestones.Where(m => m.CompletedOn == null && m.DueDate < today && p.Status == ProjectStatus.Active))
            warnings.Add($"Milestone '{m.Name}' was due {m.DueDate:dd MMM yyyy}.");
        if (s.Pending > 0) warnings.Add($"{s.Pending:0.##} hours are waiting for approval.");
        if (p.Status == ProjectStatus.Active && p.EndDate is { } end && end < today) warnings.Add($"The planned end date ({end:dd MMM yyyy}) has passed.");
        if (p.BillingType == ProjectBillingType.TimeAndMaterials && s.Unbilled > 0) warnings.Add($"{s.Unbilled:N0} of approved time is ready to invoice.");

        var revenue = s.Billed + s.Unbilled;
        return new ProjectDto(p.Id, p.EntityId, p.Entity!.Name, p.Code, p.Name, p.ClientId, p.Client?.Name, p.BillingType, p.Status, p.StartDate, p.EndDate, p.BudgetHours,
            p.ContractAmount, p.DefaultBillRate, p.TaxRateId, p.ManagerEmployeeId, p.ManagerEmployeeId is { } mid ? managers.GetValueOrDefault(mid) : null, p.Description,
            p.Members.OrderBy(m => m.Employee!.User!.FullName).Select(m => new MemberDto(m.Id, m.EmployeeId, m.Employee!.User!.FullName, m.Role, m.BillRate,
                m.BillRate ?? p.DefaultBillRate, m.CostRate, byMember.Where(x => x.EmployeeId == m.EmployeeId).Sum(x => x.Hours),
                byMember.Where(x => x.EmployeeId == m.EmployeeId && x.Billable).Sum(x => x.Hours))).ToList(),
            p.Milestones.OrderBy(m => m.SortOrder).Select(m => new MilestoneDto(m.Id, m.Name, m.DueDate, m.Amount, m.CompletedOn, m.InvoiceId,
                m.InvoiceId is { } inv ? invoices.GetValueOrDefault(inv) : null, m.CompletedOn == null && m.DueDate < today)).ToList(),
            s.Logged, s.Approved, s.Pending, s.BillableHours, s.Billed, s.Unbilled, s.Cost, s.Margin, Pct(s.Margin, revenue), Pct(s.Logged, p.BudgetHours),
            tasks.ToDictionary(t => t.Key.ToString(), t => t.Count), warnings);
    }

    public async Task<ProjectDto> SaveAsync(Guid? id, SaveProjectRequest req, CancellationToken ct)
    {
        Project p;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.ProjectsCreate, req.EntityId, ct);
            p = new Project { TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId };
            db.Projects.Add(p);
        }
        else
        {
            p = await db.Projects.Include(x => x.Members).Include(x => x.Milestones).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Project");
            await access.EnsureAsync(Permissions.ProjectsEdit, p.EntityId, ct);
            if (p.Status is ProjectStatus.Completed or ProjectStatus.Cancelled) throw new ValidationException($"A {p.Status.ToString().ToLower()} project can't be edited.");
            if (req.EntityId != p.EntityId)
            {
                await access.EnsureAsync(Permissions.ProjectsEdit, req.EntityId, ct);
                if (await db.TimeEntries.AnyAsync(t => t.ProjectId == p.Id, ct)) throw new ValidationException("The project has time logged; it can't move to another entity.");
                p.EntityId = req.EntityId;
            }
            if (req.BillingType != p.BillingType && await db.TimeEntries.AnyAsync(t => t.ProjectId == p.Id && t.Status == TimeEntryStatus.Invoiced, ct))
                throw new ValidationException("The project has been invoiced; its billing type can't change.");
        }
        var code = Guard.Code(req.Code);
        if (await db.Projects.AnyAsync(x => x.Code == code && x.Id != p.Id, ct)) throw new ValidationException($"Project {code} exists.");
        if (req.BillingType != ProjectBillingType.NonBillable)
        {
            if (req.ClientId == null) throw new ValidationException("Choose the client for a billable project.");
            if (!await db.Contacts.AnyAsync(c => c.Id == req.ClientId && c.IsCustomer, ct)) throw new ValidationException("The client must be a customer.");
        }
        if (req.EndDate is { } end && end < req.StartDate) throw new ValidationException("The end date is before the start date.");
        if (req.BudgetHours < 0 || req.DefaultBillRate < 0 || req.ContractAmount < 0) throw new ValidationException("Budget and rates can't be negative.");
        if (req.BillingType == ProjectBillingType.TimeAndMaterials && req.DefaultBillRate <= 0 && req.Members.Any(m => (m.BillRate ?? 0) <= 0))
            throw new ValidationException("Set a default hourly rate, or a bill rate for every team member.");
        if (req.BillingType == ProjectBillingType.FixedPrice)
        {
            if (req.ContractAmount <= 0) throw new ValidationException("Enter the fixed contract value.");
            if (req.Milestones.Count == 0) throw new ValidationException("A fixed-price project is billed by milestones; add them.");
            if (LedgerService.Round(req.Milestones.Sum(m => m.Amount)) != LedgerService.Round(req.ContractAmount))
                throw new ValidationException($"Milestones add up to {req.Milestones.Sum(m => m.Amount):N0}; they must equal the contract value of {req.ContractAmount:N0}.");
        }
        if (req.Members.Select(m => m.EmployeeId).Distinct().Count() != req.Members.Count) throw new ValidationException("Each person can be on the team once.");
        var empIds = req.Members.Select(m => m.EmployeeId).ToList();
        if (req.ManagerEmployeeId is { } mgr && !empIds.Contains(mgr)) throw new ValidationException("The project manager must be on the team.");
        if (await db.Employees.CountAsync(e => empIds.Contains(e.Id), ct) != empIds.Count) throw new NotFoundException("Employee");
        var rates = await ProjectsCommon.CostRatesAsync(db, empIds, ct);

        p.Code = code;
        p.Name = Guard.Required(req.Name, "Name", 150);
        p.ClientId = req.ClientId;
        p.BillingType = req.BillingType;
        p.StartDate = req.StartDate;
        p.EndDate = req.EndDate;
        p.BudgetHours = req.BudgetHours;
        p.ContractAmount = req.BillingType == ProjectBillingType.FixedPrice ? LedgerService.Round(req.ContractAmount) : 0;
        p.DefaultBillRate = req.DefaultBillRate;
        p.TaxRateId = req.TaxRateId;
        p.ManagerEmployeeId = req.ManagerEmployeeId;
        p.Description = req.Description;

        var logged = id == null ? new HashSet<Guid>() : (await db.TimeEntries.Where(t => t.ProjectId == p.Id).Select(t => t.EmployeeId).Distinct().ToListAsync(ct)).ToHashSet();
        foreach (var gone in p.Members.Where(m => req.Members.All(r => r.EmployeeId != m.EmployeeId)).ToList())
        {
            if (logged.Contains(gone.EmployeeId)) throw new ValidationException("A team member with logged time can't be removed.");
            p.Members.Remove(gone);
        }
        foreach (var r in req.Members)
        {
            var m = p.Members.FirstOrDefault(x => x.EmployeeId == r.EmployeeId);
            if (m == null) { m = new ProjectMember { ProjectId = p.Id, EmployeeId = r.EmployeeId }; p.Members.Add(m); }
            m.Role = r.Role;
            m.BillRate = r.BillRate is > 0 ? r.BillRate : null;
            m.CostRate = r.CostRate is >= 0 and var c ? c : rates.GetValueOrDefault(r.EmployeeId);
        }

        foreach (var gone in p.Milestones.Where(m => req.Milestones.All(r => r.Id != m.Id)).ToList())
        {
            if (gone.InvoiceId != null) throw new ValidationException($"Milestone '{gone.Name}' is invoiced and can't be removed.");
            p.Milestones.Remove(gone);
        }
        for (var i = 0; i < req.Milestones.Count; i++)
        {
            var r = req.Milestones[i];
            var m = p.Milestones.FirstOrDefault(x => x.Id == r.Id);
            if (m == null) { m = new ProjectMilestone { ProjectId = p.Id }; p.Milestones.Add(m); }
            if (m.InvoiceId != null && m.Amount != r.Amount) throw new ValidationException($"Milestone '{m.Name}' is invoiced; its amount can't change.");
            m.Name = Guard.Required(r.Name, "Milestone", 150);
            m.DueDate = r.DueDate;
            m.Amount = LedgerService.Round(r.Amount);
            m.SortOrder = i;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, ct);
    }

    public async Task<ProjectDto> SetStatusAsync(Guid id, ProjectStatus status, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Project");
        await access.EnsureAsync(Permissions.ProjectsEdit, p.EntityId, ct);
        var allowed = (p.Status, status) switch
        {
            (ProjectStatus.Planned, ProjectStatus.Active or ProjectStatus.Cancelled) => true,
            (ProjectStatus.Active, ProjectStatus.OnHold or ProjectStatus.Completed) => true,
            (ProjectStatus.OnHold, ProjectStatus.Active or ProjectStatus.Completed or ProjectStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed) throw new ValidationException($"A {p.Status} project can't become {status}.");
        if (status == ProjectStatus.Completed && await db.TimeEntries.AnyAsync(t => t.ProjectId == id && t.Status == TimeEntryStatus.Submitted, ct))
            throw new ValidationException("Approve or reject the pending timesheets first.");
        if (status == ProjectStatus.Cancelled && await db.TimeEntries.AnyAsync(t => t.ProjectId == id && t.Status == TimeEntryStatus.Invoiced, ct))
            throw new ValidationException("The project has been invoiced; complete it instead.");
        p.Status = status;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---------------- Billing ----------------

    public async Task<ProjectDto> CompleteMilestoneAsync(Guid id, Guid milestoneId, CancellationToken ct)
    {
        var p = await db.Projects.Include(x => x.Milestones).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Project");
        await access.EnsureAsync(Permissions.ProjectsEdit, p.EntityId, ct);
        if (p.Status != ProjectStatus.Active) throw new ValidationException("The project isn't active.");
        var m = p.Milestones.FirstOrDefault(x => x.Id == milestoneId) ?? throw new NotFoundException("Milestone");
        if (m.CompletedOn != null) throw new ValidationException("Already completed.");
        m.CompletedOn = ProjectsCommon.Today;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ProjectInvoiceResult> InvoiceMilestoneAsync(Guid id, Guid milestoneId, InvoiceProjectRequest req, CancellationToken ct)
    {
        var p = await db.Projects.Include(x => x.Milestones).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Project");
        await access.EnsureAsync(Permissions.ProjectBilling, p.EntityId, ct);
        if (p.BillingType != ProjectBillingType.FixedPrice) throw new ValidationException("Only fixed-price projects are billed by milestone.");
        var m = p.Milestones.FirstOrDefault(x => x.Id == milestoneId) ?? throw new NotFoundException("Milestone");
        if (m.CompletedOn == null) throw new ValidationException("Mark the milestone complete before invoicing it.");
        if (m.InvoiceId != null) throw new ValidationException("This milestone is already invoiced.");
        var line = new DocumentLineInput($"{p.Code} {p.Name} â€” milestone: {m.Name}", await RevenueAccountAsync(ct), 1, m.Amount, p.TaxRateId);
        var invoice = await IssueAsync(p, req.InvoiceDate ?? ProjectsCommon.Today, $"{p.Name} â€” {m.Name}", [line], ct);
        m.InvoiceId = invoice.Id;
        await db.SaveChangesAsync(ct);
        return new ProjectInvoiceResult(invoice.Id, invoice.Number, 0, invoice.Total, 0);
    }

    /// <summary>Invoices approved, billable, un-invoiced hours up to a date: one line per person and rate.</summary>
    public async Task<ProjectInvoiceResult> InvoiceTimeAsync(Guid id, InvoiceProjectRequest req, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Project");
        await access.EnsureAsync(Permissions.ProjectBilling, p.EntityId, ct);
        if (p.BillingType != ProjectBillingType.TimeAndMaterials) throw new ValidationException("Only time-and-materials projects are billed by the hour.");
        var upTo = req.UpTo ?? ProjectsCommon.Today;
        var entries = await db.TimeEntries.Include(t => t.Employee).ThenInclude(e => e!.User)
            .Where(t => t.ProjectId == id && t.Status == TimeEntryStatus.Approved && t.Billable && t.Date <= upTo).ToListAsync(ct);
        if (entries.Count == 0) throw new ValidationException($"No approved billable time up to {upTo:dd MMM yyyy} is waiting to be invoiced.");
        var from = entries.Min(t => t.Date);
        var account = await RevenueAccountAsync(ct);
        var lines = entries.GroupBy(t => new { t.EmployeeId, t.Employee!.User!.FullName, t.BillRate }).OrderBy(g => g.Key.FullName)
            .Select(g => new DocumentLineInput($"{p.Code} â€” {g.Key.FullName}, {from:dd MMM}â€“{upTo:dd MMM yyyy}", account, g.Sum(t => t.Hours), g.Key.BillRate, p.TaxRateId)).ToList();
        var invoice = await IssueAsync(p, req.InvoiceDate ?? ProjectsCommon.Today, $"{p.Name} â€” services {from:dd MMM} to {upTo:dd MMM yyyy}", lines, ct);
        foreach (var t in entries) { t.Status = TimeEntryStatus.Invoiced; t.InvoiceId = invoice.Id; }
        await db.SaveChangesAsync(ct);
        return new ProjectInvoiceResult(invoice.Id, invoice.Number, entries.Sum(t => t.Hours), invoice.Total, entries.Count);
    }

    private async Task<DocumentDto> IssueAsync(Project p, DateOnly date, string notes, List<DocumentLineInput> lines, CancellationToken ct)
    {
        if (p.ClientId == null) throw new ValidationException("The project has no client.");
        var settings = await ledger.SettingsAsync(ct);
        var draft = await documents.CreateAsync(DocumentKind.Invoice, new SaveDocumentRequest(p.EntityId, p.ClientId.Value, date, null, p.Code, notes,
            settings.BaseCurrency, 1, lines), ct, system: true);
        return await documents.ApproveAsync(draft.Id, ct, system: true);
    }

    private async Task<Guid> RevenueAccountAsync(CancellationToken ct) =>
        await db.Accounts.Where(a => a.Code == "4100" && !a.IsGroup).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw new ValidationException("Account 4100 (revenue from services) is missing from the chart of accounts.");
}
