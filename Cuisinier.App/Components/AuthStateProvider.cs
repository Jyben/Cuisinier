using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Cuisinier.App.Services;

namespace Cuisinier.App.Components;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _authService;

    public AuthStateProvider(AuthService authService)
    {
        _authService = authService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var isAuthenticated = await _authService.IsAuthenticatedAsync();
        
        if (!isAuthenticated)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var accessToken = await _authService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        try
        {
            var claims = ParseTokenClaims(accessToken);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            
            return new AuthenticationState(user);
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public void NotifyUserAuthenticationChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private IEnumerable<Claim> ParseTokenClaims(string token)
    {
        try
        {
            // Simple JWT parsing - in production, use a library like System.IdentityModel.Tokens.Jwt
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return Enumerable.Empty<Claim>();
            }

            var payload = parts[1];
            // Add padding if needed
            var padding = 4 - (payload.Length % 4);
            if (padding != 4)
            {
                payload += new string('=', padding);
            }

            var jsonBytes = Convert.FromBase64String(payload);
            var jsonString = System.Text.Encoding.UTF8.GetString(jsonBytes);
            var json = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);

            if (json == null)
            {
                return Enumerable.Empty<Claim>();
            }

            var claims = new List<Claim>();

            foreach (var kvp in json)
            {
                if (kvp.Value is System.Text.Json.JsonElement element)
                {
                    if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        claims.Add(new Claim(kvp.Key, element.GetString() ?? string.Empty));
                    }
                    else if (element.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        claims.Add(new Claim(kvp.Key, element.GetRawText()));
                    }
                    else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in element.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                claims.Add(new Claim(kvp.Key, item.GetString() ?? string.Empty));
                            }
                        }
                    }
                }
                else if (kvp.Value != null)
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString() ?? string.Empty));
                }
            }

            return claims;
        }
        catch
        {
            return Enumerable.Empty<Claim>();
        }
    }
}