using Cuisinier.Core.DTOs;
using Blazored.LocalStorage;
using Refit;

namespace Cuisinier.App.Services;

public class AuthService
{
    private const string AccessTokenKey = "accessToken";
    private const string RefreshTokenKey = "refreshToken";
    private const string UserIdKey = "userId";
    private const string EmailKey = "email";
    private const string UserNameKey = "userName";
    private const string ExpiresAtKey = "expiresAt";

    private readonly ILocalStorageService _localStorage;
    private readonly IAuthApi _authApi;

    public AuthService(ILocalStorageService localStorage, IAuthApi authApi)
    {
        _localStorage = localStorage;
        _authApi = authApi;
    }

    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _authApi.RegisterAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _authApi.LoginAsync(request);
            await SaveTokensAsync(response);
            return response;
        }
        catch (ApiException apiEx) when (apiEx.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Re-throw BadRequest exceptions (email not confirmed) so they can be handled in the UI
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            var refreshToken = await GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                return false;
            }

            var response = await _authApi.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = refreshToken });
            await SaveTokensAsync(response);
            return true;
        }
        catch
        {
            await ClearTokensAsync();
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = await GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authApi.LogoutAsync(new RefreshTokenRequest { RefreshToken = refreshToken });
            }
        }
        catch
        {
            // Ignore errors during logout
        }
        finally
        {
            await ClearTokensAsync();
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            if (!await _localStorage.ContainKeyAsync(AccessTokenKey))
            {
                return null;
            }

            var token = await _localStorage.GetItemAsync<string>(AccessTokenKey);
            var expiresAt = await GetExpiresAtAsync();
            
            // Check if token is expired
            if (expiresAt.HasValue && DateTime.UtcNow >= expiresAt.Value)
            {
                // Try to refresh token
                var refreshed = await RefreshTokenAsync();
                if (refreshed)
                {
                    return await _localStorage.GetItemAsync<string>(AccessTokenKey);
                }
                return null;
            }

            return token;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            if (!await _localStorage.ContainKeyAsync(RefreshTokenKey))
            {
                return null;
            }
            return await _localStorage.GetItemAsync<string>(RefreshTokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetAccessTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task<bool> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        try
        {
            var response = await _authApi.ConfirmEmailAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResendConfirmationEmailAsync(string email)
    {
        try
        {
            var response = await _authApi.ResendConfirmationEmailAsync(new ForgotPasswordRequest { Email = email });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        try
        {
            var response = await _authApi.ForgotPasswordAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var response = await _authApi.ResetPasswordAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task SaveTokensAsync(LoginResponse response)
    {
        await _localStorage.SetItemAsync(AccessTokenKey, response.AccessToken);
        await _localStorage.SetItemAsync(RefreshTokenKey, response.RefreshToken);
        await _localStorage.SetItemAsync(UserIdKey, response.UserId);
        await _localStorage.SetItemAsync(EmailKey, response.Email);
        await _localStorage.SetItemAsync(UserNameKey, response.UserName);
        await _localStorage.SetItemAsync(ExpiresAtKey, response.ExpiresAt);
    }

    private async Task ClearTokensAsync()
    {
        try
        {
            await _localStorage.RemoveItemAsync(AccessTokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);
            await _localStorage.RemoveItemAsync(UserIdKey);
            await _localStorage.RemoveItemAsync(EmailKey);
            await _localStorage.RemoveItemAsync(UserNameKey);
            await _localStorage.RemoveItemAsync(ExpiresAtKey);
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private async Task<DateTime?> GetExpiresAtAsync()
    {
        try
        {
            if (!await _localStorage.ContainKeyAsync(ExpiresAtKey))
            {
                return null;
            }
            return await _localStorage.GetItemAsync<DateTime>(ExpiresAtKey);
        }
        catch
        {
            return null;
        }
    }
}