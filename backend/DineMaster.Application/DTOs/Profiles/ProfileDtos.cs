using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Profiles;

public class ProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public int YumsBalance { get; set; }
    public string PreferredLanguage { get; set; } = "uz";
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class UpdateProfileRequest
{
    [MaxLength(255)] public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(10)] public string? PreferredLanguage { get; set; }
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
}
