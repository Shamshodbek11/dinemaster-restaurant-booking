using DineMaster.Domain.Entities;

namespace DineMaster.Application.Abstractions;

public interface ITokenService
{
    string CreateAccessToken(AppUser user, IEnumerable<string> roles);
}
