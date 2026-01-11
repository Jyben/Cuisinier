using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Cuisinier.Core.DTOs;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Api.Services;
using Cuisinier.Api.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cuisinier.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Register a new user")
            .Produces<LoginResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("auth");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Login user")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting("auth");

        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshToken")
            .WithSummary("Refresh access token")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Logout user")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/confirm-email", ConfirmEmail)
            .WithName("ConfirmEmail")
            .WithSummary("Confirm user email")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/resend-confirmation", ResendConfirmationEmail)
            .WithName("ResendConfirmationEmail")
            .WithSummary("Resend email confirmation")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/forgot-password", ForgotPassword)
            .WithName("ForgotPassword")
            .WithSummary("Request password reset")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("auth");

        group.MapPost("/reset-password", ResetPassword)
            .WithName("ResetPassword")
            .WithSummary("Reset password")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("auth");
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AuthEndpoints");
        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // Log detailed errors for debugging
            logger.LogWarning("User registration failed for {Email}. Errors: {Errors}", 
                request.Email, 
                string.Join(", ", result.Errors.Select(e => e.Description)));
            
            // Return generic message to client
            return Results.BadRequest(new { message = "Erreur lors de la création du compte. Veuillez vérifier vos informations." });
        }

        // Assign default role
        await userManager.AddToRoleAsync(user, "User");

        // Generate email confirmation token
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

        // Send confirmation email
        try
        {
            await emailService.SendConfirmationEmailAsync(user.Email!, user.Id, confirmationToken);
        }
        catch (Exception)
        {
            // Log error but don't fail registration
            // In production, you might want to handle this differently
        }

        return Results.Created($"/api/auth/login", new { 
            message = "Compte créé avec succès. Veuillez confirmer votre email avant de vous connecter.",
            userId = user.Id
        });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtService jwtService,
        CuisinierDbContext context,
        IConfiguration configuration)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            return Results.Unauthorized();
        }

        // Check password using UserManager (not SignInManager) to avoid email confirmation requirement
        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid)
        {
            return Results.Unauthorized();
        }

        // If password is correct but email is not confirmed, return specific error
        if (!user.EmailConfirmed)
        {
            return Results.BadRequest(new { 
                message = "Veuillez confirmer votre email avant de vous connecter.",
                emailNotConfirmed = true,
                email = user.Email
            });
        }

        // Generate access token
        var roles = await userManager.GetRolesAsync(user);
        var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        var accessToken = jwtService.GenerateAccessToken(user.Id, user.Email!, user.UserName!, roleClaims);

        // Generate refresh token
        var refreshTokenValue = jwtService.GenerateRefreshToken();
        var refreshTokenExpirationDays = int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = jwtService.HashToken(refreshTokenValue), // Hash token before storing
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays)
        };

        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        // Revoke old refresh tokens (keep only last 5)
        // Note: IsActive is a computed property, so we use the underlying database columns directly
        var now = DateTime.UtcNow;
        var oldTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id 
                && rt.ExpiresAt > now 
                && rt.RevokedAt == null)
            .OrderByDescending(rt => rt.CreatedAt)
            .Skip(5)
            .ToListAsync();

        foreach (var oldToken in oldTokens)
        {
            oldToken.RevokedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        var accessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");

        return Results.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpirationMinutes),
            UserId = user.Id,
            Email = user.Email!,
            UserName = user.UserName!,
            EmailConfirmed = user.EmailConfirmed
        });
    }

    private static async Task<IResult> RefreshToken(
        RefreshTokenRequest request,
        IJwtService jwtService,
        UserManager<ApplicationUser> userManager,
        CuisinierDbContext context,
        IConfiguration configuration)
    {
        // Hash the provided token to compare with stored hash
        var hashedToken = jwtService.HashToken(request.RefreshToken);
        var storedToken = await context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken);

        if (storedToken == null || !storedToken.IsActive)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId);
        if (user == null || !user.EmailConfirmed)
        {
            return Results.Unauthorized();
        }

        // Revoke old token
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var roles = await userManager.GetRolesAsync(user);
        var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        var newAccessToken = jwtService.GenerateAccessToken(user.Id, user.Email!, user.UserName!, roleClaims);
        var newRefreshTokenValue = jwtService.GenerateRefreshToken();
        var refreshTokenExpirationDays = int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = jwtService.HashToken(newRefreshTokenValue), // Hash token before storing
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays)
        };

        context.RefreshTokens.Add(newRefreshToken);
        await context.SaveChangesAsync();

        var accessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");

        return Results.Ok(new LoginResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpirationMinutes),
            UserId = user.Id,
            Email = user.Email!,
            UserName = user.UserName!,
            EmailConfirmed = user.EmailConfirmed
        });
    }

    private static async Task<IResult> Logout(
        RefreshTokenRequest request,
        ClaimsPrincipal user,
        IJwtService jwtService,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Hash the provided token to compare with stored hash
        var hashedToken = jwtService.HashToken(request.RefreshToken);
        var refreshToken = await context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken && rt.UserId == userId);

        if (refreshToken != null && refreshToken.IsActive)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmail(
        ConfirmEmailRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Results.BadRequest(new { message = "Utilisateur introuvable." });
        }

        if (user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Email déjà confirmé." });
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new { 
                message = "Token de confirmation invalide ou expiré.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Results.Ok(new { message = "Email confirmé avec succès." });
    }

    private static async Task<IResult> ResendConfirmationEmail(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        
        // Don't reveal if user exists or not for security
        if (user == null)
        {
            return Results.Ok(new { message = "Si un compte existe avec cet email, un email de confirmation a été envoyé." });
        }

        // If email is already confirmed, don't reveal it but return success
        if (user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Si un compte existe avec cet email, un email de confirmation a été envoyé." });
        }

        // Generate new confirmation token
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

        try
        {
            await emailService.SendConfirmationEmailAsync(user.Email!, user.Id, confirmationToken);
        }
        catch (Exception)
        {
            // Log error but don't reveal it
            return Results.BadRequest(new { message = "Erreur lors de l'envoi de l'email." });
        }

        return Results.Ok(new { message = "Si un compte existe avec cet email, un email de confirmation a été envoyé." });
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        
        // Don't reveal if user exists or not for security
        if (user == null || !user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Si un compte existe avec cet email, un lien de réinitialisation a été envoyé." });
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);

        try
        {
            await emailService.SendPasswordResetEmailAsync(user.Email!, user.Id, resetToken);
        }
        catch (Exception)
        {
            // Log error but don't reveal it
            return Results.BadRequest(new { message = "Erreur lors de l'envoi de l'email." });
        }

        return Results.Ok(new { message = "Si un compte existe avec cet email, un lien de réinitialisation a été envoyé." });
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Results.BadRequest(new { message = "Utilisateur introuvable." });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new { 
                message = "Token de réinitialisation invalide ou expiré.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Results.Ok(new { message = "Mot de passe réinitialisé avec succès." });
    }
}