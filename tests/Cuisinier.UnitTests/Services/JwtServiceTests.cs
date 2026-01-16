using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Cuisinier.Api.Services;

namespace Cuisinier.UnitTests.Services;

public class JwtServiceTests
{
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;

    public JwtServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "ThisIsAVerySecureTestKeyThatIsAtLeast32Characters!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:AccessTokenExpirationMinutes"] = "15"
            })
            .Build();

        _jwtService = new JwtService(_configuration);
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidToken()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var email = "test@test.com";
        var userName = "TestUser";

        // Act
        var token = _jwtService.GenerateAccessToken(userId, email, userName);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateAccessToken_TokenContainsCorrectClaims()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var email = "test@test.com";
        var userName = "TestUser";

        // Act
        var token = _jwtService.GenerateAccessToken(userId, email, userName);
        var principal = _jwtService.GetPrincipalFromToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.Should().Be(userId);
        principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value.Should().Be(email);
        principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value.Should().Be(userName);
    }

    [Fact]
    public void GenerateAccessToken_WithAdditionalClaims_IncludesAllClaims()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var email = "test@test.com";
        var userName = "TestUser";
        var additionalClaims = new[]
        {
            new Claim("custom_claim", "custom_value"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        // Act
        var token = _jwtService.GenerateAccessToken(userId, email, userName, additionalClaims);
        var principal = _jwtService.GetPrincipalFromToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.FindFirst("custom_claim")?.Value.Should().Be("custom_value");
        principal.FindFirst(ClaimTypes.Role)?.Value.Should().Be("Admin");
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyString()
    {
        // Act
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokens()
    {
        // Act
        var token1 = _jwtService.GenerateRefreshToken();
        var token2 = _jwtService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsSufficientLength()
    {
        // Act
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Assert - Base64 encoded 64 bytes should be around 88 characters
        refreshToken.Length.Should().BeGreaterThanOrEqualTo(80);
    }

    [Fact]
    public void GetPrincipalFromToken_ReturnsNull_ForInvalidToken()
    {
        // Act
        var result = _jwtService.GetPrincipalFromToken("invalid.token.here");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromToken_ReturnsNull_ForEmptyToken()
    {
        // Act
        var result = _jwtService.GetPrincipalFromToken(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromToken_ReturnsNull_ForMalformedToken()
    {
        // Act
        var result = _jwtService.GetPrincipalFromToken("not-even-close-to-a-jwt");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromToken_ValidatesIssuer()
    {
        // Arrange - Create a token with wrong issuer
        var wrongIssuerConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "ThisIsAVerySecureTestKeyThatIsAtLeast32Characters!",
                ["Jwt:Issuer"] = "WrongIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:AccessTokenExpirationMinutes"] = "15"
            })
            .Build();

        var wrongIssuerService = new JwtService(wrongIssuerConfig);
        var token = wrongIssuerService.GenerateAccessToken("user", "email@test.com", "User");

        // Act - Try to validate with correct issuer
        var result = _jwtService.GetPrincipalFromToken(token);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void HashToken_ReturnsConsistentHash()
    {
        // Arrange
        var token = "test-token-value";

        // Act
        var hash1 = _jwtService.HashToken(token);
        var hash2 = _jwtService.HashToken(token);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashToken_ReturnsDifferentHashForDifferentTokens()
    {
        // Arrange
        var token1 = "token-one";
        var token2 = "token-two";

        // Act
        var hash1 = _jwtService.HashToken(token1);
        var hash2 = _jwtService.HashToken(token2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashToken_ReturnsBase64EncodedSha256Hash()
    {
        // Arrange
        var token = "test-token";

        // Act
        var hash = _jwtService.HashToken(token);

        // Assert - SHA256 produces 32 bytes, Base64 encoded is ~44 characters
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().BeGreaterThanOrEqualTo(40);
    }

    [Fact]
    public void Constructor_ThrowsException_WhenSecretKeyNotConfigured()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();

        // Act & Assert
        var act = () => new JwtService(invalidConfig);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SecretKey*");
    }
}
