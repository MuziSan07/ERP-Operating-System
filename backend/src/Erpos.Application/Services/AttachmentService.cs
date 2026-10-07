using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

public record AttachmentDto(Guid Id, string RecordType, Guid RecordId, string FileName, string ContentType, long SizeBytes, string? Description,
    DateTime UploadedAt, string? UploadedBy);
public record AttachmentFile(Stream Content, string FileName, string ContentType);

/// <summary>
/// Files attached to records. A user may list and download a record's files if they may view the record, and upload or
/// delete if they may edit it — the record's own entity and permissions decide, so attachments never widen access.
/// </summary>
public class AttachmentService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, IFileStorage storage)
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".xls"] = "application/vnd.ms-excel", [".csv"] = "text/csv",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".doc"] = "application/msword", [".txt"] = "text/plain",
    };

    /// <summary>Record kinds that accept attachments: (entity of the record, view permission, edit permission).</summary>
    private async Task<(Guid EntityId, string View, string Edit)> ResolveAsync(string recordType, Guid recordId, CancellationToken ct)
    {
        Guid? entity;
        (string, string) perms;
        switch (recordType.ToLowerInvariant())
        {
            case "invoice":
            case "bill":
                var doc = await db.FinanceDocuments.Where(d => d.Id == recordId).Select(d => new { d.EntityId, d.Kind }).FirstOrDefaultAsync(ct);
                entity = doc?.EntityId;
                perms = doc?.Kind == DocumentKind.Bill ? ("finance.bills.view", "finance.bills.edit") : ("finance.invoices.view", "finance.invoices.edit");
                break;
            case "payment":
                entity = await db.Payments.Where(p => p.Id == recordId).Select(p => (Guid?)p.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.PaymentsView, Permissions.PaymentsCreate);
                break;
            case "journal":
                entity = await db.JournalEntries.Where(j => j.Id == recordId).Select(j => (Guid?)j.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.JournalsView, Permissions.JournalsCreate);
                break;
            case "asset":
                entity = await db.FixedAssets.Where(a => a.Id == recordId).Select(a => (Guid?)a.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.AssetsView, Permissions.AssetsEdit);
                break;
            case "employee":
                entity = await db.Employees.Where(e => e.Id == recordId).Select(e => (Guid?)e.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.EmployeesView, Permissions.EmployeesEdit);
                break;
            case "shipment":
                entity = await db.Shipments.Where(s => s.Id == recordId).Select(s => (Guid?)s.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.ShipmentsView, Permissions.ShipmentsEdit);
                break;
            case "grant":
                entity = await db.Grants.Where(g => g.Id == recordId).Select(g => (Guid?)g.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.GrantsView, Permissions.GrantsEdit);
                break;
            case "project":
                entity = await db.Projects.Where(p => p.Id == recordId).Select(p => (Guid?)p.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.ProjectsView, Permissions.ProjectsEdit);
                break;
            case "beneficiary":
                entity = await db.Beneficiaries.Where(b => b.Id == recordId).Select(b => (Guid?)b.EntityId).FirstOrDefaultAsync(ct);
                perms = (Permissions.BeneficiariesView, Permissions.BeneficiariesEdit);
                break;
            default:
                throw new ValidationException($"Files can't be attached to '{recordType}'.");
        }
        return (entity ?? throw new NotFoundException("Record"), perms.Item1, perms.Item2);
    }

    public async Task<List<AttachmentDto>> ListAsync(string recordType, Guid recordId, CancellationToken ct)
    {
        var (entity, view, _) = await ResolveAsync(recordType, recordId, ct);
        await access.EnsureAsync(view, entity, ct);
        var type = recordType.ToLowerInvariant();
        return await db.Attachments.Where(a => a.RecordType == type && a.RecordId == recordId).OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentDto(a.Id, a.RecordType, a.RecordId, a.FileName, a.ContentType, a.SizeBytes, a.Description, a.CreatedAt,
                db.Users.Where(u => u.Id == a.CreatedBy).Select(u => u.FullName).FirstOrDefault())).ToListAsync(ct);
    }

    public async Task<AttachmentDto> UploadAsync(string recordType, Guid recordId, string fileName, long length, Stream content, string? description, CancellationToken ct)
    {
        var (entity, _, edit) = await ResolveAsync(recordType, recordId, ct);
        await access.EnsureAsync(edit, entity, ct);
        if (length <= 0) throw new ValidationException("The file is empty.");
        if (length > MaxBytes) throw new ValidationException($"Files can be up to {MaxBytes / 1024 / 1024} MB.");
        var name = Path.GetFileName(fileName ?? "").Trim();
        var ext = Path.GetExtension(name);
        if (string.IsNullOrEmpty(name) || !AllowedTypes.TryGetValue(ext, out var contentType))
            throw new ValidationException("Allowed files: PDF, images (PNG, JPG, WEBP), Excel, CSV, Word and text.");
        if (name.Length > 200) name = name[..(200 - ext.Length)] + ext;

        var tenant = currentUser.TenantId!.Value;
        var a = new Attachment
        {
            TenantId = tenant, RecordType = recordType.ToLowerInvariant(), RecordId = recordId, FileName = name, ContentType = contentType, SizeBytes = length,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()[..Math.Min(description.Trim().Length, 300)]
        };
        // The stored name never comes from the user: tenant / year / month / random id.
        a.StorageKey = $"{tenant:N}/{DateTime.UtcNow:yyyy/MM}/{a.Id:N}{ext.ToLowerInvariant()}";
        await storage.SaveAsync(a.StorageKey, content, ct);
        db.Attachments.Add(a);
        try { await db.SaveChangesAsync(ct); }
        catch { await storage.DeleteAsync(a.StorageKey, CancellationToken.None); throw; }
        return (await ListAsync(recordType, recordId, ct)).First(x => x.Id == a.Id);
    }

    public async Task<AttachmentFile> DownloadAsync(Guid id, CancellationToken ct)
    {
        var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Attachment");
        var (entity, view, _) = await ResolveAsync(a.RecordType, a.RecordId, ct);
        await access.EnsureAsync(view, entity, ct);
        return new AttachmentFile(await storage.OpenAsync(a.StorageKey, ct), a.FileName, a.ContentType);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Attachment");
        var (entity, _, edit) = await ResolveAsync(a.RecordType, a.RecordId, ct);
        await access.EnsureAsync(edit, entity, ct);
        a.IsDeleted = true; // the file is kept for the audit trail; soft-deleted rows are hidden everywhere
        await db.SaveChangesAsync(ct);
    }
}
