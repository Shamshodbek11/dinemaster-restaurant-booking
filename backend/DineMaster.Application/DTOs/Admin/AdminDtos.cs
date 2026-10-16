using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Admin;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int YumsBalance { get; set; }
    public bool IsBanned { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class AdjustYumsRequest
{
    [Required] public Guid UserId { get; set; }
    [Range(-100000, 100000)] public int Amount { get; set; }
    public string? Reason { get; set; }
}

public class BanUserRequest
{
    [Required] public Guid UserId { get; set; }
    public string? Reason { get; set; }
}

public class AssignRoleRequest
{
    [Required] public Guid UserId { get; set; }
    [Required][RegularExpression("^(user|admin|superadmin)$")]
    public string Role { get; set; } = "admin";
}

public class ApprovalRequest
{
    public bool Approve { get; set; } = true;
    public string? RejectionReason { get; set; }
}

public class PlatformStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalAdmins { get; set; }
    public int TotalRestaurants { get; set; }
    public int PendingRestaurants { get; set; }
    public int TotalReservations { get; set; }
    public int TotalReservationsToday { get; set; }
    public int TotalVenues { get; set; }
    public int BannedUsers { get; set; }
    public long TotalYumsInCirculation { get; set; }
}
