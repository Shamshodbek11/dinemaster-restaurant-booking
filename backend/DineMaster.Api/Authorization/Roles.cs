// Re-export from Domain for Api layer convenience
using DineMaster.Domain.Enums;

namespace DineMaster.Api.Authorization;

public static class ApiRoles
{
    public const string User = Roles.User;
    public const string Admin = Roles.Admin;
    public const string Superadmin = Roles.Superadmin;
    public const string AdminOrSuperadmin = Admin + "," + Superadmin;
}
