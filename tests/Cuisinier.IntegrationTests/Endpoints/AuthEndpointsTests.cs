using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Cuisinier.Infrastructure.Data;
using Cuisinier.IntegrationTests.Infrastructure;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.IntegrationTests.Endpoints;

public class AuthEndpointsTests : IntegrationTestBase
{
    public AuthEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region Register Tests

    [Fact]
    public async Task Register_ReturnsCreated_WithValidRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid()}@test.com",
            UserName = "TestUser",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WithInvalidEmail()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "not-an-email",
            UserName = "TestUser",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WithWeakPassword()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid()}@test.com",
            UserName = "TestUser",
            Password = "weak",
            ConfirmPassword = "weak"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordsDontMatch()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid()}@test.com",
            UserName = "TestUser",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WithDuplicateEmail()
    {
        // Arrange
        var email = $"duplicate{Guid.NewGuid()}@test.com";
        var request = new RegisterRequest
        {
            Email = email,
            UserName = "TestUser1",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Register first user
        await Client.PostAsJsonAsync("/api/auth/register", request);

        // Try to register second user with same email
        var request2 = new RegisterRequest
        {
            Email = email,
            UserName = "TestUser2",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", request2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_ReturnsOk_WithValidCredentials()
    {
        // Arrange
        var email = $"login{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        // Register and confirm email
        var registerRequest = new RegisterRequest
        {
            Email = email,
            UserName = "LoginUser",
            Password = password,
            ConfirmPassword = password
        };
        await Client.PostAsJsonAsync("/api/auth/register", registerRequest);
        await ConfirmUserEmailAsync(email);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.RefreshToken.Should().NotBeNullOrEmpty();
        loginResponse.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WithInvalidPassword()
    {
        // Arrange
        var email = $"wrongpass{Guid.NewGuid()}@test.com";

        // Register and confirm email
        var registerRequest = new RegisterRequest
        {
            Email = email,
            UserName = "WrongPassUser",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };
        await Client.PostAsJsonAsync("/api/auth/register", registerRequest);
        await ConfirmUserEmailAsync(email);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WithNonExistentUser()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@test.com",
            Password = "Password123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenEmailNotConfirmed()
    {
        // Arrange
        var email = $"unconfirmed{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        // Register but don't confirm email
        var registerRequest = new RegisterRequest
        {
            Email = email,
            UserName = "UnconfirmedUser",
            Password = password,
            ConfirmPassword = password
        };
        await Client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_ReturnsOk_WithValidRefreshToken()
    {
        // Arrange
        var email = $"refresh{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        // Register, confirm, and login
        var registerRequest = new RegisterRequest
        {
            Email = email,
            UserName = "RefreshUser",
            Password = password,
            ConfirmPassword = password
        };
        await Client.PostAsJsonAsync("/api/auth/register", registerRequest);
        await ConfirmUserEmailAsync(email);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResult!.RefreshToken
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
        refreshResult.Should().NotBeNull();
        refreshResult!.AccessToken.Should().NotBeNullOrEmpty();
        refreshResult.AccessToken.Should().NotBe(loginResult.AccessToken);
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WithInvalidToken()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_ReturnsNoContent_WithValidToken()
    {
        // Arrange
        var email = $"logout{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        // Register, confirm, and login
        var registerRequest = new RegisterRequest
        {
            Email = email,
            UserName = "LogoutUser",
            Password = password,
            ConfirmPassword = password
        };
        await Client.PostAsJsonAsync("/api/auth/register", registerRequest);
        await ConfirmUserEmailAsync(email);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        SetAuthorizationHeader(loginResult!.AccessToken);

        var logoutRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResult.RefreshToken
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WithoutToken()
    {
        // Arrange
        ClearAuthorizationHeader();
        var logoutRequest = new RefreshTokenRequest
        {
            RefreshToken = "some-token"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Helper Methods

    private async Task ConfirmUserEmailAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user != null)
        {
            user.EmailConfirmed = true;
            await db.SaveChangesAsync();
        }
    }

    #endregion
}
