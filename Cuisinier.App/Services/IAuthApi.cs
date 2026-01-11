using Cuisinier.Shared.DTOs;
using Refit;

namespace Cuisinier.App.Services;

public interface IAuthApi
{
    [Post("/api/auth/register")]
    Task<ApiResponse<object>> RegisterAsync([Body] RegisterRequest request);

    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest request);

    [Post("/api/auth/refresh")]
    Task<LoginResponse> RefreshTokenAsync([Body] RefreshTokenRequest request);

    [Post("/api/auth/logout")]
    Task LogoutAsync([Body] RefreshTokenRequest request);

    [Post("/api/auth/confirm-email")]
    Task<ApiResponse<object>> ConfirmEmailAsync([Body] ConfirmEmailRequest request);

    [Post("/api/auth/resend-confirmation")]
    Task<ApiResponse<object>> ResendConfirmationEmailAsync([Body] ForgotPasswordRequest request);

    [Post("/api/auth/forgot-password")]
    Task<ApiResponse<object>> ForgotPasswordAsync([Body] ForgotPasswordRequest request);

    [Post("/api/auth/reset-password")]
    Task<ApiResponse<object>> ResetPasswordAsync([Body] ResetPasswordRequest request);
}