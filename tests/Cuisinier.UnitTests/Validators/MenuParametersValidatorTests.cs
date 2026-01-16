using Cuisinier.Shared.DTOs;
using Cuisinier.Shared.Validators;

namespace Cuisinier.UnitTests.Validators;

public class MenuParametersValidatorTests
{
    private readonly MenuParametersValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_WithValidParameters()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenWeekStartDateIsEmpty()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.WeekStartDate = default;

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "WeekStartDate");
    }

    [Fact]
    public void Validate_Fails_WhenWeekStartDateIsNotMonday()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.WeekStartDate = new DateTime(2024, 1, 2); // Tuesday

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "WeekStartDate" &&
            e.ErrorMessage.Contains("lundi"));
    }

    [Fact]
    public void Validate_Succeeds_WhenWeekStartDateIsMonday()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.WeekStartDate = new DateTime(2024, 1, 1); // Monday

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenConfigurationsIsEmpty()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.Configurations.Clear();

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configurations");
    }

    [Fact]
    public void Validate_Fails_WhenConfigurationHasInvalidNumberOfDishes()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.Configurations[0].NumberOfDishes = 0;

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("NumberOfDishes"));
    }

    [Fact]
    public void Validate_Fails_WhenConfigurationHasInvalidServings()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.Configurations[0].Servings = 0;

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Servings"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Validate_Succeeds_WithValidNumberOfDishes(int numberOfDishes)
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.Configurations[0].NumberOfDishes = numberOfDishes;

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenNumberOfDishesExceedsLimit()
    {
        // Arrange
        var parameters = CreateValidMenuParameters();
        parameters.Configurations[0].NumberOfDishes = 21;

        // Act
        var result = _validator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("NumberOfDishes"));
    }

    private static MenuParameters CreateValidMenuParameters()
    {
        return new MenuParameters
        {
            WeekStartDate = GetNextMonday(),
            SeasonalFoods = true,
            Configurations = new List<DishConfigurationDto>
            {
                new()
                {
                    NumberOfDishes = 5,
                    Servings = 4,
                    Parameters = new DishConfigurationParametersDto()
                }
            }
        };
    }

    private static DateTime GetNextMonday()
    {
        var today = DateTime.Today;
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0 && today.DayOfWeek != DayOfWeek.Monday)
        {
            daysUntilMonday = 7;
        }
        return today.AddDays(daysUntilMonday);
    }
}
