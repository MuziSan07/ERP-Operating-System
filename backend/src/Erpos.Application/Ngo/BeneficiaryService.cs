using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Ngo;

/// <summary>
/// Beneficiary register (one record per CNIC across the organization) and assistance. Cash assistance is charged to a
/// fund or grant line; the same help under the same program isn't repeated within 30 days unless deliberately allowed.
/// </summary>
public class BeneficiaryService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, FundService funds)
{
    private const int RepeatWindowDays = 30;

    public async Task<PagedResult<BeneficiaryListItem>> ListAsync(Guid? programId, string? district, string? search, bool activeOnly, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.BeneficiariesView, ct);
        var q = db.Beneficiaries.Where(b => visible.Contains(b.EntityId));
        if (programId != null) q = q.Where(b => b.ProgramId == programId);
        if (!string.IsNullOrWhiteSpace(district)) q = q.Where(b => b.District == district);
        if (activeOnly) q = q.Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(b => b.FullName.Contains(search) || b.RegistrationNo.Contains(search) || (b.Cnic != null && b.Cnic.Contains(search)) || (b.Phone != null && b.Phone.Contains(search)));
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(b => b.EnrolledOn).ThenBy(b => b.FullName).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
            .Select(b => new BeneficiaryListItem(b.Id, b.RegistrationNo, b.FullName, b.Cnic, b.Gender, b.District, b.HouseholdSize,
                b.Program == null ? null : b.Program.Name, b.ZakatEligible, b.IsActive, b.EnrolledOn,
                db.Assistance.Count(a => a.BeneficiaryId == b.Id), db.Assistance.Where(a => a.BeneficiaryId == b.Id).Sum(a => a.Value),
                db.Assistance.Where(a => a.BeneficiaryId == b.Id).Max(a => (DateOnly?)a.Date))).ToListAsync(ct);
        return new PagedResult<BeneficiaryListItem>(items, total, page, pageSize);
    }

    public async Task<BeneficiaryDto> GetAsync(Guid id, CancellationToken ct)
    {
        var b = await db.Beneficiaries.Include(x => x.Program).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Beneficiary");
        await access.EnsureAsync(Permissions.BeneficiariesView, b.EntityId, ct);
        var entity = await db.Entities.Where(e => e.Id == b.EntityId).Select(e => e.Name).FirstAsync(ct);
        var help = await db.Assistance.Where(a => a.BeneficiaryId == id).OrderByDescending(a => a.Date)
            .Select(a => new AssistanceDto(a.Id, a.Date, a.Type, a.Description, a.Quantity, a.Value,
                db.NgoPrograms.Where(p => p.Id == a.ProgramId).Select(p => p.Name).FirstOrDefault(),
                db.Funds.Where(f => f.Id == a.FundId).Select(f => f.Code).FirstOrDefault(),
                db.FundExpenses.Where(e => e.Id == a.ExpenseId).Select(e => e.Number).FirstOrDefault())).ToListAsync(ct);
        int? age = b.DateOfBirth is { } dob ? (NgoCommon.Today.DayNumber - dob.DayNumber) / 365 : null;
        return new BeneficiaryDto(b.Id, b.EntityId, entity, b.RegistrationNo, b.FullName, b.Cnic, b.Gender, b.DateOfBirth, age, b.Phone, b.District, b.Address,
            b.HouseholdSize, b.Vulnerabilities, b.ZakatEligible, b.ProgramId, b.Program?.Name, b.EnrolledOn, b.IsActive, help, help.Sum(a => a.Value));
    }

    public async Task<BeneficiaryDto> SaveAsync(Guid? id, SaveBeneficiaryRequest req, CancellationToken ct)
    {
        Beneficiary b;
        if (id == null)
        {
            await access.EnsureAsync(Permissions.BeneficiariesCreate, req.EntityId, ct);
            b = new Beneficiary { TenantId = currentUser.TenantId!.Value, RegistrationNo = $"BEN-{await db.NextSequenceAsync("BEN", ct):D6}" };
            db.Beneficiaries.Add(b);
        }
        else
        {
            b = await db.Beneficiaries.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Beneficiary");
            await access.EnsureAsync(Permissions.BeneficiariesEdit, b.EntityId, ct);
            if (req.EntityId != b.EntityId) await access.EnsureAsync(Permissions.BeneficiariesEdit, req.EntityId, ct);
        }
        var cnic = string.IsNullOrWhiteSpace(req.Cnic) ? null : req.Cnic.Trim();
        if (cnic != null)
        {
            if (!NgoSetupService.CnicRegex().IsMatch(cnic)) throw new ValidationException("CNIC must look like 35202-1234567-1.");
            // Deduplication across the whole organization, including entities this user can't see.
            var existing = await db.Beneficiaries.Where(x => x.Cnic == cnic && x.Id != b.Id).Select(x => x.RegistrationNo).FirstOrDefaultAsync(ct);
            if (existing != null) throw new ValidationException($"CNIC {cnic} is already registered as {existing}.");
        }
        if (req.HouseholdSize is < 1 or > 50) throw new ValidationException("Household size must be between 1 and 50.");
        if (req.DateOfBirth is { } dob && dob > NgoCommon.Today) throw new ValidationException("Date of birth can't be in the future.");
        if (req.ProgramId is { } pid && !await db.NgoPrograms.AnyAsync(p => p.Id == pid, ct)) throw new NotFoundException("Program");
        b.EntityId = req.EntityId;
        b.FullName = Guard.Required(req.FullName, "Name", 150);
        b.Cnic = cnic;
        b.Gender = req.Gender;
        b.DateOfBirth = req.DateOfBirth;
        b.Phone = req.Phone;
        b.District = string.IsNullOrWhiteSpace(req.District) ? null : req.District.Trim();
        b.Address = req.Address;
        b.HouseholdSize = req.HouseholdSize;
        b.Vulnerabilities = req.Vulnerabilities;
        b.ZakatEligible = req.ZakatEligible;
        b.ProgramId = req.ProgramId;
        b.EnrolledOn = req.EnrolledOn ?? (id == null ? NgoCommon.Today : b.EnrolledOn);
        b.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return await GetAsync(b.Id, ct);
    }

    public async Task<BeneficiaryDto> AddAssistanceAsync(Guid id, AddAssistanceRequest req, CancellationToken ct)
    {
        var b = await db.Beneficiaries.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Beneficiary");
        await access.EnsureAsync(Permissions.BeneficiariesEdit, b.EntityId, ct);
        if (!b.IsActive) throw new ValidationException($"{b.FullName} is inactive.");
        if (req.Value < 0 || (req.Type == AssistanceType.Cash && req.Value <= 0)) throw new ValidationException("Enter the value of the assistance.");
        var programId = req.ProgramId ?? b.ProgramId;
        if (!req.AllowRepeat)
        {
            var from = req.Date.AddDays(-RepeatWindowDays);
            var to = req.Date.AddDays(RepeatWindowDays);
            var last = await db.Assistance.Where(a => a.BeneficiaryId == id && a.Type == req.Type && a.ProgramId == programId && a.Date > from && a.Date < to)
                .OrderByDescending(a => a.Date).Select(a => (DateOnly?)a.Date).FirstOrDefaultAsync(ct);
            if (last != null)
                throw new ValidationException($"{b.FullName} already received {req.Type.ToString().ToLower()} assistance under this program on {last:dd MMM yyyy}. " +
                                              "Tick 'allow repeat' if this is intended.");
        }
        var a = new Assistance
        {
            TenantId = b.TenantId, BeneficiaryId = id, ProgramId = programId, Date = req.Date, Type = req.Type,
            Description = Guard.Required(req.Description, "Description", 300), Quantity = req.Quantity, Value = LedgerService.Round(req.Value), FundId = req.FundId
        };
        if (req.Type == AssistanceType.Cash)
        {
            if (req.PaidFromAccountId == null) throw new ValidationException("Choose the cash/bank account the money was paid from.");
            if (req.FundId == null && req.GrantId == null) throw new ValidationException("Choose the fund or grant paying for this assistance.");
            var expense = await funds.ChargeInternalAsync(new ChargeExpenseRequest(b.EntityId, req.Date, req.FundId, req.GrantId, req.BudgetLineId, programId,
                FunctionalCategory.Program, await NgoCommon.AccountAsync(db, "6950", ct), $"{a.Description} — {b.RegistrationNo} {b.FullName}", a.Value,
                req.PaidFromAccountId, null, false), b, ct);
            expense.AssistanceId = a.Id;
            a.ExpenseId = expense.Id;
            a.FundId = expense.FundId;
        }
        else if (req.FundId is { } fid && !await db.Funds.AnyAsync(f => f.Id == fid, ct)) throw new NotFoundException("Fund");
        db.Assistance.Add(a);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }
}
