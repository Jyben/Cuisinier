using System.Security.Claims;

namespace Cuisinier.Api.Services;

public interface IJwtService
{
    string GenerateAccessToken(string userId, string email, string userName, IEnumerable<Claim>? additionalClaims = null);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromToken(string token);
    string HashToken(string token);
}