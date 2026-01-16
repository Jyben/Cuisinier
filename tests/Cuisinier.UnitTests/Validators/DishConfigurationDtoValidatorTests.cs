using Cuisinier.Shared.DTOs;
using Cuisinier.Shared.Validators;

namespace Cuisinier.UnitTests.Validators;

public class DishConfigurationDtoValidatorTests
{
    private readonly DishConfigurationDtoValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_WithValidConfiguration()
    {
        // Arrange
        var config = CreateValidDishConfiguration();

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Fails_WhenNumberOfDishesIsNotPositive(int numberOfDishes)
    {
        // Arrange
        var config = CreateValidDishConfiguration();
        config.NumberOfDishes = numberOfDishes;

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NumberOfDishes");
    }

    [Fact]
    public void Validate_Fails_WhenNumberOfDishesExceeds20()
    {
        // Arrange
        var config = CreateValidDishConfiguration();
        config.NumberOfDishes = 21;

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "NumberOfDishes" &&
            e.ErrorMessage.Contains("20"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Fails_WhenServingsIsNotPositive(int servings)
    {
        // Arrange
        var config = CreateValidDishConfiguration();
        config.Servings = servings;

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Servings");
    }

    [Fact]
    public void Validate_Fails_WhenServingsExceeds20()
    {
        // Arrange
        var config = CreateValidDishConfiguration();
        config.Servings = 21;

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Servings" &&
            e.ErrorMessage.Contains("20"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 4)]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    public void Validate_Succeeds_WithValidDishesAndServings(int dishes, int servings)
    {
        // Arrange
        var config = CreateValidDishConfiguration();
        config.NumberOfDishes = dishes;
        config.Servings = servings;

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenParametersIsNull()
    {
        // Arrange
        var config = CreateValidDishConfiguration();
        config.Parameters = null!;

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Parameters");
    }

    private static DishConfigurationDto CreateValidDishConfiguration()
    {
        return new DishConfigurationDto
        {
            NumberOfDishes = 5,
            Servings = 4,
            Parameters = new DishConfigurationParametersDto()
        };
    }
}

public class DishConfigurationParametersDtoValidatorTests
{
    private readonly DishConfigurationParametersDtoValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_WithEmptyParameters()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto();

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenMaxPreparationTimeIsNegative()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MaxPreparationTime = TimeSpan.FromMinutes(-10)
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxPreparationTime");
    }

    [Fact]
    public void Validate_Fails_WhenMaxCookingTimeIsNegative()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MaxCookingTime = TimeSpan.FromMinutes(-10)
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxCookingTime");
    }

    [Fact]
    public void Validate_Fails_WhenMinKcalIsNegative()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MinKcalPerDish = -100
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MinKcalPerDish");
    }

    [Fact]
    public void Validate_Fails_WhenMaxKcalIsNegative()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MaxKcalPerDish = -100
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxKcalPerDish");
    }

    [Fact]
    public void Validate_Fails_WhenMinKcalGreaterThanMaxKcal()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MinKcalPerDish = 1000,
            MaxKcalPerDish = 500
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("inférieures ou égales"));
    }

    [Fact]
    public void Validate_Succeeds_WhenMinKcalEqualsMaxKcal()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MinKcalPerDish = 500,
            MaxKcalPerDish = 500
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Succeeds_WithValidTimeConstraints()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MaxPreparationTime = TimeSpan.FromMinutes(30),
            MaxCookingTime = TimeSpan.FromMinutes(60)
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Succeeds_WithValidCalorieRange()
    {
        // Arrange
        var parameters = new DishConfigurationParametersDto
        {
            MinKcalPerDish = 300,
            MaxKcalPerDish = 800
        };

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
