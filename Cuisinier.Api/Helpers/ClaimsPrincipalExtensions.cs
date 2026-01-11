using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cuisinier.Api.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email) 
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);
    }
}