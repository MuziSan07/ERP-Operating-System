using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>Voided project invoice: its hours go back to "approved" and its milestone back to "completed, not invoiced".</summary>
public class ProjectInvoiceVoidHandler(IAppDbContext db) : IDocumentVoidHandler
{
    public async Task OnVoidedAsync(FinanceDocument document, CancellationToken ct)
    {
        if (document.Kind != DocumentKind.Invoice) return;
        foreach (var t in await db.TimeEntries.Where(t => t.InvoiceId == document.Id).ToListAsync(ct))
        {
            t.Status = TimeEntryStatus.Approved;
            t.InvoiceId = null;
        }
        foreach (var m in await db.ProjectMilestones.Where(m => m.InvoiceId == document.Id).ToListAsync(ct)) m.InvoiceId = null;
    }
}

/// <summary>Voided freight invoice: the consignments can be billed again.</summary>
public class ShipmentInvoiceVoidHandler(IAppDbContext db) : IDocumentVoidHandler
{
    public async Task OnVoidedAsync(FinanceDocument document, CancellationToken ct)
    {
        if (document.Kind != DocumentKind.Invoice) return;
        foreach (var s in await db.Shipments.Where(s => s.InvoiceId == document.Id).ToListAsync(ct)) s.InvoiceId = null;
    }
}

/// <summary>Voided travel invoice: the booking returns to "confirmed" so it can be corrected and invoiced again.</summary>
public class TravelInvoiceVoidHandler(IAppDbContext db) : IDocumentVoidHandler
{
    public async Task OnVoidedAsync(FinanceDocument document, CancellationToken ct)
    {
        if (document.Kind != DocumentKind.Invoice) return;
        foreach (var b in await db.TravelBookings.Where(b => b.InvoiceId == document.Id).ToListAsync(ct))
        {
            b.Status = TravelBookingStatus.Confirmed;
            b.InvoiceId = null;
        }
    }
}

/// <summary>Voided vendor bill charged to a fund or grant: the spending is removed and its restricted-income release reversed.</summary>
public class FundExpenseVoidHandler(IAppDbContext db, LedgerService ledger) : IDocumentVoidHandler
{
    public async Task OnVoidedAsync(FinanceDocument document, CancellationToken ct)
    {
        if (document.Kind != DocumentKind.Bill) return;
        foreach (var e in await db.FundExpenses.Where(e => e.BillId == document.Id).ToListAsync(ct))
        {
            if (e.ReleaseJournalId is { } jid && await db.JournalEntries.FirstOrDefaultAsync(j => j.Id == jid && j.Status == JournalStatus.Posted, ct) is { } release)
                await ledger.ReverseAsync(release, document.Date, $"Bill {document.Number} voided", ct);
            e.IsDeleted = true;
        }
    }
}
