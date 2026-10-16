using DineMaster.Application.Abstractions;
using DineMaster.Application.DTOs.Auth;
using DineMaster.Api.Extensions;
using DineMaster.Domain.Enums;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly AppDbContext _db;

    public AuthController(IAuthService auth, AppDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("email-code")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(req.Email, ct);
        return Ok(new { message = "Agar bu email ro'yxatdan o'tgan bo'lsa, unga tiklash kodi yuborildi." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(req.Email, req.Code, req.NewPassword, ct);
        return Ok(new { message = "Parol muvaffaqiyatli yangilandi." });
    }

    /// <summary>Emailni tasdiqlash kodini (qayta) yuborish.</summary>
    [HttpPost("email/send-code")]
    [Authorize]
    [EnableRateLimiting("email-code")]
    public async Task<IActionResult> SendEmailCode(CancellationToken ct)
    {
        await _auth.SendEmailVerificationCodeAsync(User.GetUserIdOrThrow(), ct);
        return Ok(new { message = "Tasdiqlash kodi emailingizga yuborildi." });
    }

    [HttpPost("email/verify")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
    {
        await _auth.VerifyEmailAsync(User.GetUserIdOrThrow(), req.Code, ct);
        return Ok(new { message = "Email tasdiqlandi.", emailConfirmed = true });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var profile = await _db.Profiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == userId, ct);
        if (profile is null) return NotFound();

        var roles = await _db.UserRoles
            .Where(r => r.UserId == userId)
            .Select(r => r.Role.ToRoleString())
            .ToListAsync(ct);

        return Ok(new
        {
            id = userId,
            email = profile.Email,
            fullName = profile.FullName,
            avatarUrl = profile.AvatarUrl,
            yumsBalance = profile.YumsBalance,
            preferredLanguage = profile.PreferredLanguage,
            emailConfirmed = profile.User?.EmailConfirmed ?? false,
            roles
        });
    }

    /// <summary>
    /// Bir martali superadmin setup (X-Setup-Key header bilan himoyalangan).
    /// </summary>
    [HttpPost("setup-superadmin")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> SetupSuperadmin(
        [FromBody] InitialSuperadminRequest req,
        [FromHeader(Name = "X-Setup-Key")] string? setupKey,
        [FromServices] IConfiguration config,
        CancellationToken ct)
    {
        var expected = config["SetupKey"];
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(setupKey) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(setupKey)))
            return StatusCode(403, new { message = "Setup key noto'g'ri." });

        var result = await _auth.AssignInitialSuperadminAsync(req.Email, req.Password, req.FullName, ct);
        return Ok(result);
    }
}
