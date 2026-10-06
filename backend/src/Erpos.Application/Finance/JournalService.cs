using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>Manual journal vouchers plus read access to every journal (generated ones included).</summary>
public class JournalService(IAppDbContext db, IAccessService access, LedgerService ledger)
{
    public async Task<PagedResult<JournalEntryDto>> ListAsync(Guid? entityId, JournalSource? source, DateOnly? from, DateOnly? to,
        string? search, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.JournalsView, ct);
        var q = db.JournalEntries.Where(j => visible.Contains(j.EntityId));
        if (entityId != null) q = q.Where(j => j.EntityId == entityId);
        if (source != null) q = q.Where(j => j.Source == source);
        if (from != null) q = q.Where(j => j.Date >= from);
        if (to != null) q = q.Where(j => j.Date <= to);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(j => j.Number.Contains(search) || j.Description.Contains(search) || (j.Reference != null && j.Reference.Contains(search)));

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(j => j.Date).ThenByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(j => j.Id).ToListAsync(ct);
        var items = await LoadAsync(ids, ct);
        return new PagedResult<JournalEntryDto>(ids.Select(id => items.First(i => i.Id == id)).ToList(), total, page, pageSize);
    }

    public async Task<JournalEntryDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = (await LoadAsync([id], ct)).FirstOrDefault() ?? throw new NotFoundException("Journal entry");
        await access.EnsureAsync(Permissions.JournalsView, dto.EntityId, ct);
        return dto;
    }

    public async Task<JournalEntryDto> CreateAsync(SaveJournalRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.JournalsCreate, req.EntityId, ct);
        var entry = await BuildAsync(req, ct);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return await GetAsync(entry.Id, ct);
    }

    public async Task<JournalEntryDto> UpdateAsync(Guid id, SaveJournalRequest req, CancellationToken ct)
    {
        var entry = await db.JournalEntries.Include(j => j.Lines).FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new NotFoundException("Journal entry");
        await access.EnsureAsync(Permissions.JournalsCreate, entry.EntityId, ct);
        await access.EnsureAsync(Permissions.JournalsCreate, req.EntityId, ct);
        if (entry.Status != JournalStatus.Draft || entry.Source != JournalSource.Manual) throw new ValidationException("Only draft manual journals can be edited.");

        var rebuilt = await BuildAsync(req, ct);
        db.JournalLines.RemoveRange(entry.Lines);
        entry.Lines.Clear();
        entry.EntityId = rebuilt.EntityId;
        entry.Date = rebuilt.Date;
        entry.Reference = rebuilt.Reference;
        entry.Description = rebuilt.Description;
        entry.Currency = rebuilt.Currency;
        entry.ExchangeRate = rebuilt.ExchangeRate;
        foreach (var l in rebuilt.Lines)
        {
            l.JournalEntryId = entry.Id;
            entry.Lines.Add(l);
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.JournalEntries.Include(j => j.Lines).FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new NotFoundException("Journal entry");
        await access.EnsureAsync(Permissions.JournalsCreate, entry.EntityId, ct);
        if (entry.Status != JournalStatus.Draft) throw new ValidationException("Posted entries can't be deleted; reverse them instead.");
        db.JournalLines.RemoveRange(entry.Lines);
        db.JournalEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<JournalEntryDto> PostAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.JournalEntries.FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new NotFoundException("Journal entry");
        await access.EnsureAsync(Permissions.JournalsPost, entry.EntityId, ct);
        await ledger.PostAsync(entry, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Manual entries only; generated entries are reversed by voiding their invoice/bill/payment.</summary>
    public async Task<JournalEntryDto> ReverseAsync(Guid id, ReverseRequest req, CancellationToken ct)
    {
        var entry = await db.JournalEntries.FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new NotFoundException("Journal entry");
        await access.EnsureAsync(Permissions.JournalsReverse, entry.EntityId, ct);
        if (entry.Source != JournalSource.Manual) throw new ValidationException("Void the source invoice, bill or payment instead.");
        var reversal = await ledger.ReverseAsync(entry, req.Date ?? DateOnly.FromDateTime(DateTime.UtcNow), req.Reason, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(reversal.Id, ct);
    }

    private async Task<JournalEntry> BuildAsync(SaveJournalRequest req, CancellationToken ct)
    {
        foreach (var e in req.Lines.Where(l => l.EntityId != null && l.EntityId != req.EntityId).Select(l => l.EntityId!.Value).Distinct())
            await access.EnsureAsync(Permissions.JournalsCreate, e, ct);
        var (currency, rate) = await ledger.ResolveCurrencyAsync(req.Currency, req.ExchangeRate, req.Date, ct);
        return await ledger.BuildAsync(req.EntityId, req.Date, req.Description, currency, rate, JournalSource.Manual, null,
            req.Lines.Select(l => new LineInput(l.AccountId, l.Debit, l.Credit, l.EntityId, l.Description, l.ContactId)).ToList(), req.Reference, ct);
    }

    private async Task<List<JournalEntryDto>> LoadAsync(List<Guid> ids, CancellationToken ct)
    {
        var entries = await db.JournalEntries.Where(j => ids.Contains(j.Id)).Include(j => j.Entity).ToListAsync(ct);
        var lines = await db.JournalLines.Where(l => ids.Contains(l.JournalEntryId)).Include(l => l.Account).OrderBy(l => l.SortOrder).ToListAsync(ct);
        var entityIds = lines.Select(l => l.EntityId).Distinct().ToList();
        var entityNames = await db.Entities.Where(e => entityIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var contactIds = lines.Where(l => l.ContactId != null).Select(l => l.ContactId!.Value).Distinct().ToList();
        var contacts = await db.Contacts.Where(c => contactIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var userIds = entries.SelectMany(e => new[] { e.CreatedBy, e.PostedBy }).Where(u => u != null).Select(u => u!.Value).Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return entries.Select(e =>
        {
            var ls = lines.Where(l => l.JournalEntryId == e.Id).ToList();
            return new JournalEntryDto(e.Id, string.IsNullOrEmpty(e.Number) ? null : e.Number, e.EntityId, e.Entity!.Name, e.Date, e.Reference,
                e.Description, e.Status, e.Source, e.SourceId, e.Currency, e.ExchangeRate, ls.Sum(l => l.Debit), ls.Sum(l => l.BaseDebit),
                e.CreatedBy is { } c ? users.GetValueOrDefault(c) : null, e.PostedBy is { } p ? users.GetValueOrDefault(p) : null, e.PostedAt,
                e.ReversalOfId, e.ReversedById,
                ls.Select(l => new JournalLineDto(l.AccountId, l.Account!.Code, l.Account.Name, l.EntityId, entityNames.GetValueOrDefault(l.EntityId),
                    l.Description, l.Debit, l.Credit, l.BaseDebit, l.BaseCredit, l.ContactId,
                    l.ContactId is { } cid ? contacts.GetValueOrDefault(cid) : null)).ToList());
        }).ToList();
    }
}
