using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Auth;

public class RegisterRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    [Required] public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PreferredLanguage { get; set; } = "uz";
    /// <summary>Do'st taklif kodi (havola: /signup?ref=KOD).</summary>
    [MaxLength(12)] public string? ReferralCode { get; set; }
    /// <summary>Foydalanish shartlari va maxfiylik siyosatiga rozilik (majburiy).</summary>
    public bool AcceptTerms { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public AuthUserDto User { get; set; } = new();
}

public class AuthUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public int YumsBalance { get; set; }
    public string? AvatarUrl { get; set; }
    public string PreferredLanguage { get; set; } = "uz";
    public bool EmailConfirmed { get; set; }
}

public class InitialSuperadminRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    [Required] public string FullName { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(6, MinimumLength = 6)] public string Code { get; set; } = string.Empty;
    [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
}

public class VerifyEmailRequest
{
    [Required, StringLength(6, MinimumLength = 6)] public string Code { get; set; } = string.Empty;
}
