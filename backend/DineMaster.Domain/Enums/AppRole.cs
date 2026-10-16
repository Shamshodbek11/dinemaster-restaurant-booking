namespace DineMaster.Domain.Enums;

public enum AppRole
{
    User = 0,
    Admin = 1,
    Superadmin = 2,
    /// <summary>Restoran xodimi (manager/host) — ruxsatlar RestaurantStaff orqali.</summary>
    Staff = 3
}

public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";
    public const string Superadmin = "superadmin";
    public const string Staff = "staff";
}

public static class RoleExtensions
{
    public static string ToRoleString(this AppRole role) => role switch
    {
        AppRole.User => Roles.User,
        AppRole.Admin => Roles.Admin,
        AppRole.Superadmin => Roles.Superadmin,
        AppRole.Staff => Roles.Staff,
        _ => Roles.User
    };

    public static AppRole ParseRole(string? role) => role?.ToLowerInvariant() switch
    {
        "admin" => AppRole.Admin,
        "superadmin" => AppRole.Superadmin,
        "staff" => AppRole.Staff,
        _ => AppRole.User
    };
}
