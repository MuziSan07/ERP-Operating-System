using System.Globalization;
using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Bank reconciliation: statement lines are matched to the bank account's ledger lines (automatically by amount and date, or
/// by hand); bank charges and other items only the bank knows about are posted from the statement line. The statement
/// balance plus deposits in transit minus outstanding payments must equal the book balance before it can be completed.
/// </summary>
public class BankReconciliationService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger)
{
    private const int MatchWindowDays = 7;

    public async Task<List<ReconciliationListItem>> ListAsync(Guid? bankAccountId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.JournalsPost, ct);
        visible.UnionWith(await access.EntitiesWithAsync(Permissions.ReportsView, ct));
        var q = db.BankReconciliations.Where(r => visible.Contains(r.EntityId));
        if (bankAccountId != null) q = q.Where(r => r.BankAccountId == bankAccountId);
        return await q.OrderByDescending(r => r.StatementDate)
            .Select(r => new ReconciliationListItem(r.Id, r.BankAccount!.Code + " " + r.BankAccount.Name, r.StatementDate, r.StatementBalance, r.Status,
                r.Lines.Count, r.Lines.Count(l => l.JournalLineId == null))).ToListAsync(ct);
    }

    public async Task<ReconciliationDto> CreateAsync(CreateReconciliationRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.JournalsPost, req.EntityId, ct);
        var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.BankAccountId, ct) ?? throw new NotFoundException("Bank account");
        if (bank.SubType != AccountSubType.Bank) throw new ValidationException("Reconcile a bank account.");
        await db.LockAsync<Account>(bank.Id, ct);
        if (await db.BankReconciliations.AnyAsync(r => r.BankAccountId == bank.Id && r.Status == ReconciliationStatus.Draft, ct))
            throw new ValidationException($"{bank.Name} already has a reconciliation in progress; finish or delete it first.");
        if (await db.BankReconciliations.AnyAsync(r => r.BankAccountId == bank.Id && r.Status == ReconciliationStatus.Completed && r.StatementDate >= req.StatementDate, ct))
            throw new ValidationException("A later statement for this account is already reconciled.");
        var lines = req.Lines is { Count: > 0 } ? req.Lines : string.IsNullOrWhiteSpace(req.Csv) ? [] : ParseCsv(req.Csv);
        if (lines.Count == 0) throw new ValidationException("Add the statement lines (or paste the bank's CSV export).");
        if (lines.Any(l => l.Date > req.StatementDate)) throw new ValidationException("Statement lines can't be dated after the statement date.");

        var r = new BankReconciliation
        {
            TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, BankAccountId = bank.Id, StatementDate = req.StatementDate,
            StatementBalance = LedgerService.Round(req.StatementBalance)
        };
        var i = 0;
        foreach (var l in lines.OrderBy(l => l.Date))
            r.Lines.Add(new BankStatementLine
            {
                ReconciliationId = r.Id, Date = l.Date, Description = Guard.Required(l.Description, "Description", 300), Reference = l.Reference,
                Amount = LedgerService.Round(l.Amount), SortOrder = i++
            });
        if (r.Lines.Any(l => l.Amount == 0)) throw new ValidationException("Statement lines need a non-zero amount.");
        db.BankReconciliations.Add(r);
        await db.SaveChangesAsync(ct);
        return await GetAsync(r.Id, ct);
    }

    /// <summary>Reads "Date, Description, Reference, Amount" or "Date, Description, Reference, Debit, Credit" (withdrawal / deposit) exports.</summary>
    internal static List<StatementLineInput> ParseCsv(string csv)
    {
        var rows = csv.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(SplitCsv).ToList();
        if (rows.Count == 0) return [];
        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => header.FindIndex(h => names.Any(n => h.Contains(n)));
        var date = Col("date");
        var desc = Col("description", "narration", "particular", "detail");
        var reference = Col("reference", "ref", "cheque", "chq");
        var amount = Col("amount");
        var debit = Col("debit", "withdrawal");
        var credit = Col("credit", "deposit");
        if (date < 0 || desc < 0 || (amount < 0 && (debit < 0 || credit < 0)))
            throw new ValidationException("The CSV needs a header row with Date, Description and either Amount or Debit/Credit columns.");
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "dd MMM yyyy", "MM/dd/yyyy"];
        decimal Num(List<string> row, int col) => col < 0 || col >= row.Count || string.IsNullOrWhiteSpace(row[col]) ? 0
            : decimal.Parse(row[col].Replace(",", "").Replace("(", "-").Replace(")", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture);
        var result = new List<StatementLineInput>();
        foreach (var (row, n) in rows.Skip(1).Select((row, n) => (row, n + 2)))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (!DateOnly.TryParseExact(row[date].Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                throw new ValidationException($"Line {n}: can't read the date '{row[date]}'.");
            var value = amount >= 0 ? Num(row, amount) : Num(row, credit) - Num(row, debit);
            result.Add(new StatementLineInput(d, row[desc].Trim(), reference >= 0 && reference < row.Count ? row[reference].Trim() : null, value));
        }
        return result;
    }

    private static List<string> SplitCsv(string line)
    {
        var cells = new List<string>();
        var cur = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cells.Add(cur.ToString());
        return cells;
    }

    // ---------------- View ----------------

    private sealed record BookLine(Guid Id, DateOnly Date, string Number, string Description, decimal Amount, Guid? ReconciliationId);

    /// <summary>
    /// Ledger lines on the bank account up to the statement date. An entry and its reversal both dated on or before the
    /// statement date cancel out and never reach the bank, so they're left out.
    /// </summary>
    private async Task<List<BookLine>> BookLinesAsync(Guid bankAccountId, DateOnly upTo, CancellationToken ct)
    {
        var rows = await db.JournalLines.Where(l => l.AccountId == bankAccountId && l.JournalEntry!.Status != JournalStatus.Draft && l.JournalEntry.Date <= upTo)
            .Select(l => new
            {
                l.Id, l.JournalEntry!.Date, l.JournalEntry.Number, Description = l.JournalEntry.Description, Amount = l.BaseDebit - l.BaseCredit, l.ReconciliationId,
                l.JournalEntry.Status, l.JournalEntry.ReversalOfId, ReversedOn = db.JournalEntries.Where(r => r.Id == l.JournalEntry.ReversedById).Select(r => (DateOnly?)r.Date).FirstOrDefault(),
                // A reversal of something that already cleared the bank (e.g. a bounced cheque) is real money and stays.
                OriginalCleared = db.JournalLines.Any(o => o.JournalEntryId == l.JournalEntry.ReversalOfId && o.AccountId == bankAccountId && o.ReconciliationId != null)
            }).ToListAsync(ct);
        return rows.Where(r => r.ReconciliationId != null
                               || (r.ReversalOfId == null ? !(r.Status == JournalStatus.Reversed && r.ReversedOn <= upTo) : r.OriginalCleared))
            .Select(r => new BookLine(r.Id, r.Date, r.Number, r.Description, r.Amount, r.ReconciliationId)).ToList();
    }

    public async Task<ReconciliationDto> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await db.BankReconciliations.Include(x => x.Lines).Include(x => x.BankAccount).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Reconciliation");
        if (!await access.HasAsync(Permissions.JournalsPost, r.EntityId, ct)) await access.EnsureAsync(Permissions.ReportsView, r.EntityId, ct);

        var bookBalance = await db.JournalLines.Where(l => l.AccountId == r.BankAccountId && l.JournalEntry!.Status != JournalStatus.Draft && l.JournalEntry.Date <= r.StatementDate)
            .SumAsync(l => (decimal?)(l.BaseDebit - l.BaseCredit), ct) ?? 0;
        var book = await BookLinesAsync(r.BankAccountId, r.StatementDate, ct);
        // Items still waiting to appear on a statement, and the ones matched on this one.
        var relevant = book.Where(b => b.ReconciliationId == null || b.ReconciliationId == r.Id).OrderBy(b => b.Date).ThenBy(b => b.Number).ToList();
        var outstanding = book.Where(b => b.ReconciliationId == null).ToList();
        var inTransit = outstanding.Where(b => b.Amount > 0).Sum(b => b.Amount);
        var unpresented = -outstanding.Where(b => b.Amount < 0).Sum(b => b.Amount);
        var adjusted = r.StatementBalance + inTransit - unpresented;
        var matchedNumbers = book.Where(b => b.ReconciliationId == r.Id).ToDictionary(b => b.Id, b => b.Number);
        var unmatched = r.Lines.Count(l => l.JournalLineId == null);
        var difference = LedgerService.Round(bookBalance - adjusted);
        return new ReconciliationDto(r.Id, r.EntityId, r.BankAccountId, $"{r.BankAccount!.Code} {r.BankAccount.Name}", r.StatementDate, r.StatementBalance, r.Status,
            r.Lines.OrderBy(l => l.SortOrder).Select(l => new StatementLineDto(l.Id, l.Date, l.Description, l.Reference, l.Amount, l.JournalLineId,
                l.JournalLineId is { } j ? matchedNumbers.GetValueOrDefault(j) : null)).ToList(),
            relevant.Select(b => new BookItemDto(b.Id, b.Date, b.Number, b.Description, b.Amount, b.ReconciliationId == r.Id)).ToList(),
            LedgerService.Round(bookBalance), inTransit, unpresented, LedgerService.Round(adjusted), difference, unmatched,
            r.Status == ReconciliationStatus.Draft && unmatched == 0 && difference == 0);
    }

    // ---------------- Matching ----------------

    private async Task<BankReconciliation> DraftAsync(Guid id, CancellationToken ct)
    {
        var r = await db.BankReconciliations.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reconciliation");
        await access.EnsureAsync(Permissions.JournalsPost, r.EntityId, ct);
        if (r.Status != ReconciliationStatus.Draft) throw new ValidationException("This reconciliation is completed.");
        await db.LockAsync<BankReconciliation>(r.Id, ct);
        return r;
    }

    /// <summary>Pairs each unmatched statement line with an unmatched ledger line of the same amount, preferring a matching reference, then the nearest date.</summary>
    public async Task<AutoMatchResult> AutoMatchAsync(Guid id, CancellationToken ct)
    {
        var r = await DraftAsync(id, ct);
        var free = (await BookLinesAsync(r.BankAccountId, r.StatementDate, ct)).Where(b => b.ReconciliationId == null).ToList();
        var lineIds = free.Select(f => f.Id).ToList();
        var refs = await db.JournalLines.Where(l => lineIds.Contains(l.Id)).Select(l => new { l.Id, l.JournalEntry!.Reference, l.Description })
            .ToDictionaryAsync(x => x.Id, x => $"{x.Reference} {x.Description}", ct);
        var matched = 0;
        foreach (var s in r.Lines.Where(l => l.JournalLineId == null).OrderBy(l => l.Date))
        {
            var best = free.Where(f => f.Amount == s.Amount && Math.Abs(f.Date.DayNumber - s.Date.DayNumber) <= MatchWindowDays)
                .OrderByDescending(f => !string.IsNullOrWhiteSpace(s.Reference) &&
                                        (f.Number.Contains(s.Reference, StringComparison.OrdinalIgnoreCase) || refs.GetValueOrDefault(f.Id, "").Contains(s.Reference, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(f => Math.Abs(f.Date.DayNumber - s.Date.DayNumber)).FirstOrDefault();
            if (best == null) continue;
            await LinkAsync(r, s, best.Id, ct);
            free.Remove(best);
            matched++;
        }
        await db.SaveChangesAsync(ct);
        return new AutoMatchResult(matched, await GetAsync(id, ct));
    }

    public async Task<ReconciliationDto> MatchAsync(Guid id, MatchRequest req, CancellationToken ct)
    {
        var r = await DraftAsync(id, ct);
        var s = r.Lines.FirstOrDefault(l => l.Id == req.StatementLineId) ?? throw new NotFoundException("Statement line");
        if (s.JournalLineId != null) throw new ValidationException("That statement line is already matched; unmatch it first.");
        var line = await db.JournalLines.Where(l => l.Id == req.JournalLineId).Select(l => new { l.AccountId, l.ReconciliationId, Amount = l.BaseDebit - l.BaseCredit, l.JournalEntry!.Date })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Ledger line");
        if (line.AccountId != r.BankAccountId) throw new ValidationException("That ledger line isn't on this bank account.");
        if (line.ReconciliationId != null) throw new ValidationException("That ledger line is already matched.");
        if (line.Date > r.StatementDate) throw new ValidationException("That ledger line is dated after the statement.");
        if (line.Amount != s.Amount) throw new ValidationException($"Amounts differ ({s.Amount:N2} on the statement, {line.Amount:N2} in the books).");
        await LinkAsync(r, s, req.JournalLineId, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReconciliationDto> UnmatchAsync(Guid id, UnmatchRequest req, CancellationToken ct)
    {
        var r = await DraftAsync(id, ct);
        var s = r.Lines.FirstOrDefault(l => l.Id == req.StatementLineId) ?? throw new NotFoundException("Statement line");
        if (s.JournalLineId is { } jl)
        {
            var line = await db.JournalLines.FirstAsync(l => l.Id == jl, ct);
            line.ReconciliationId = null;
            s.JournalLineId = null;
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    /// <summary>Posts what only the bank knew about (charges, profit, direct credits) from the statement line, and matches it.</summary>
    public async Task<ReconciliationDto> CreateEntryAsync(Guid id, CreateEntryFromLineRequest req, CancellationToken ct)
    {
        var r = await DraftAsync(id, ct);
        var s = r.Lines.FirstOrDefault(l => l.Id == req.StatementLineId) ?? throw new NotFoundException("Statement line");
        if (s.JournalLineId != null) throw new ValidationException("That statement line is already matched.");
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.AccountId, ct) ?? throw new NotFoundException("Account");
        if (account.IsGroup || account.Id == r.BankAccountId) throw new ValidationException("Choose the other side of the entry (e.g. bank charges).");
        var settings = await ledger.SettingsAsync(ct);
        var text = string.IsNullOrWhiteSpace(req.Description) ? s.Description : req.Description.Trim();
        var amount = Math.Abs(s.Amount);
        var entry = await ledger.BuildAndPostAsync(r.EntityId, s.Date, text, settings.BaseCurrency, 1, JournalSource.Manual, r.Id,
            s.Amount > 0
                ? [new(r.BankAccountId, amount, 0, null, text), new(account.Id, 0, amount, null, text)]
                : [new(account.Id, amount, 0, null, text), new(r.BankAccountId, 0, amount, null, text)], s.Reference ?? "Bank statement", ct);
        var bankLine = entry.Lines.First(l => l.AccountId == r.BankAccountId);
        bankLine.ReconciliationId = r.Id;
        s.JournalLineId = bankLine.Id;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task LinkAsync(BankReconciliation r, BankStatementLine s, Guid journalLineId, CancellationToken ct)
    {
        var line = await db.JournalLines.FirstAsync(l => l.Id == journalLineId, ct);
        line.ReconciliationId = r.Id;
        s.JournalLineId = journalLineId;
    }

    public async Task<ReconciliationDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        var r = await DraftAsync(id, ct);
        var view = await GetAsync(id, ct);
        if (view.UnmatchedLines > 0) throw new ValidationException($"{view.UnmatchedLines} statement line(s) are not matched yet.");
        if (view.Difference != 0)
            throw new ValidationException($"The books ({view.BookBalance:N2}) and the adjusted statement balance ({view.AdjustedStatementBalance:N2}) differ by {view.Difference:N2}.");
        r.Status = ReconciliationStatus.Completed;
        r.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var r = await DraftAsync(id, ct);
        var ids = r.Lines.Where(l => l.JournalLineId != null).Select(l => l.JournalLineId!.Value).ToList();
        foreach (var line in await db.JournalLines.Where(l => ids.Contains(l.Id)).ToListAsync(ct)) line.ReconciliationId = null;
        r.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}
