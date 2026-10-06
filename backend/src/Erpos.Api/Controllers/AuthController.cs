using Erpos.Application.Dtos;
using Erpos.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest req, CancellationToken ct) => auth.LoginAsync(req, ct);

    [AllowAnonymous, HttpPost("refresh")]
    public Task<AuthResponse> Refresh(RefreshRequest req, CancellationToken ct) => auth.RefreshAsync(req, ct);

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req, CancellationToken ct)
    {
        await auth.LogoutAsync(req, ct);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public Task<MeResponse> Me(CancellationToken ct) => auth.MeAsync(ct);

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(req, ct);
        return NoContent();
    }
}
