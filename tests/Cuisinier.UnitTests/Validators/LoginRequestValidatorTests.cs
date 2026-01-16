using Cuisinier.Shared.DTOs;
using Cuisinier.Shared.Validators;

namespace Cuisinier.UnitTests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_WithValidRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenEmailIsEmpty()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "",
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_Fails_WhenEmailIsInvalid()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "not-an-email",
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Email" &&
            e.ErrorMessage.Contains("email valide"));
    }

    [Fact]
    public void Validate_Fails_WhenPasswordIsEmpty()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = ""
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Theory]
    [InlineData("user@domain.com")]
    [InlineData("user.name@domain.co.uk")]
    [InlineData("user+tag@example.org")]
    public void Validate_Succeeds_WithVariousValidEmails(string email)
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
