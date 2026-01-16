using Cuisinier.Shared.DTOs;
using Cuisinier.Shared.Validators;

namespace Cuisinier.UnitTests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_WithValidRequest()
    {
        // Arrange
        var request = CreateValidRegisterRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenEmailIsEmpty()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.Email = "";

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
        var request = CreateValidRegisterRequest();
        request.Email = "not-an-email";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Email" &&
            e.ErrorMessage.Contains("email valide"));
    }

    [Fact]
    public void Validate_Fails_WhenUserNameIsTooShort()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.UserName = "AB";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "UserName" &&
            e.ErrorMessage.Contains("3 caractères"));
    }

    [Fact]
    public void Validate_Fails_WhenPasswordIsTooShort()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.Password = "Pass1!";
        request.ConfirmPassword = "Pass1!";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Password" &&
            e.ErrorMessage.Contains("8 caractères"));
    }

    [Fact]
    public void Validate_Fails_WhenPasswordHasNoUppercase()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.Password = "password123!";
        request.ConfirmPassword = "password123!";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Password" &&
            e.ErrorMessage.Contains("majuscule"));
    }

    [Fact]
    public void Validate_Fails_WhenPasswordHasNoLowercase()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.Password = "PASSWORD123!";
        request.ConfirmPassword = "PASSWORD123!";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Password" &&
            e.ErrorMessage.Contains("minuscule"));
    }

    [Fact]
    public void Validate_Fails_WhenPasswordHasNoDigit()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.Password = "PasswordABC!";
        request.ConfirmPassword = "PasswordABC!";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Password" &&
            e.ErrorMessage.Contains("chiffre"));
    }

    [Fact]
    public void Validate_Fails_WhenPasswordHasNoSpecialCharacter()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.Password = "Password123";
        request.ConfirmPassword = "Password123";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Password" &&
            e.ErrorMessage.Contains("caractère spécial"));
    }

    [Fact]
    public void Validate_Fails_WhenPasswordsDoNotMatch()
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.ConfirmPassword = "DifferentPassword123!";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "ConfirmPassword" &&
            e.ErrorMessage.Contains("correspondent pas"));
    }

    [Theory]
    [InlineData("Password1!")]
    [InlineData("MySecure@Pass99")]
    [InlineData("C0mpl3x#Password")]
    public void Validate_Succeeds_WithVariousValidPasswords(string password)
    {
        // Arrange
        var request = CreateValidRegisterRequest();
        request.Password = password;
        request.ConfirmPassword = password;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static RegisterRequest CreateValidRegisterRequest()
    {
        return new RegisterRequest
        {
            Email = "test@example.com",
            UserName = "TestUser",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };
    }
}
