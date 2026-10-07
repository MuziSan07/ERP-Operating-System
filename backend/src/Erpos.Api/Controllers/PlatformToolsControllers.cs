using Erpos.Api.Infrastructure;
using Erpos.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/attachments")]
public class AttachmentsController(AttachmentService attachments) : ControllerBase
{
    [HttpGet] public Task<List<AttachmentDto>> List([FromQuery] string recordType, [FromQuery] Guid recordId, CancellationToken ct) => attachments.ListAsync(recordType, recordId, ct);

    [HttpPost, RequestSizeLimit(AttachmentService.MaxBytes + 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = AttachmentService.MaxBytes + 1024 * 1024)]
    public async Task<AttachmentDto> Upload([FromForm] string recordType, [FromForm] Guid recordId, IFormFile file, [FromForm] string? description, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await attachments.UploadAsync(recordType, recordId, file.FileName, file.Length, stream, description, ct);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var f = await attachments.DownloadAsync(id, ct);
        return File(f.Content, f.ContentType, f.FileName); // Content-Disposition: attachment, never rendered inline
    }

    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await attachments.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Route("api/admin")]
public class AdminToolsController(EmailService email, ReminderService reminders) : ControllerBase
{
    [HttpGet("outbox")] public Task<OutboxView> Outbox(CancellationToken ct) => email.OutboxAsync(ct);
    [HttpPost("reminders/run")] public Task<ReminderResult> RunReminders(CancellationToken ct) => reminders.RunNowAsync(ct);
}

public record ForgotPasswordRequest(string Email);
public record ResetPasswordWithTokenRequest(string Token, string NewPassword);

[ApiController, Route("api/auth")]
public class PasswordResetController(Application.Services.AuthService auth) : ControllerBase
{
    [AllowAnonymous, HttpPost("forgot-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest req, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(req.Email, ct);
        return Ok(new { message = "If that email belongs to an account, a reset link is on its way." });
    }

    [AllowAnonymous, HttpPost("reset-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Reset(ResetPasswordWithTokenRequest req, CancellationToken ct)
    {
        await auth.ResetPasswordWithTokenAsync(req.Token, req.NewPassword, ct);
        return Ok(new { message = "Password changed. Sign in with your new password." });
    }
}
