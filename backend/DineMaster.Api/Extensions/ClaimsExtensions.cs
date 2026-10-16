using System.Security.Claims;
using DineMaster.Domain.Enums;

namespace DineMaster.Api.Extensions;

public static class ClaimsExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    public static Guid GetUserIdOrThrow(this ClaimsPrincipal principal)
        => principal.GetUserId() ?? throw new UnauthorizedAccessException("User ID claim topilmadi.");

    public static bool IsSuperadmin(this ClaimsPrincipal principal)
        => principal.IsInRole(Roles.Superadmin);

    public static bool IsAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole(Roles.Admin) || principal.IsSuperadmin();
}
