using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly HttpClient Client;
    protected readonly CustomWebApplicationFactory Factory;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected async Task<string> CreateAndAuthenticateUserAsync(
        string email = "test@test.com",
        string password = "Password123!",
        string userName = "TestUser")
    {
        // Register user
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            UserName = userName
        };

        await Client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Manually confirm email in database (since we mock email service)
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user != null)
        {
            user.EmailConfirmed = true;
            await db.SaveChangesAsync();
        }

        // Login
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return loginResult?.AccessToken ?? throw new InvalidOperationException("Failed to authenticate");
    }

    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }
}
