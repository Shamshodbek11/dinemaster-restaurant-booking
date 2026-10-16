using DineMaster.Application.DTOs.Auth;

namespace DineMaster.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> AssignInitialSuperadminAsync(string email, string password, string fullName, CancellationToken ct = default);

    /// <summary>Emailni tasdiqlash kodini (qayta) yuboradi.</summary>
    Task SendEmailVerificationCodeAsync(Guid userId, CancellationToken ct = default);
    Task VerifyEmailAsync(Guid userId, string code, CancellationToken ct = default);

    /// <summary>Parolni tiklash kodini emailga yuboradi (email topilmasa ham jim qaytadi).</summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(string email, string code, string newPassword, CancellationToken ct = default);
}
