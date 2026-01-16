using Cuisinier.Shared.DTOs;
using Cuisinier.Shared.Helpers;

namespace Cuisinier.UnitTests.Helpers;

public class MenuParametersMigrationHelperTests
{
    [Fact]
    public void MigrateIfNeeded_ReturnsDefault_WhenParametersIsNull()
    {
        // Act
        var result = MenuParametersMigrationHelper.MigrateIfNeeded(null!);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(1);
        result.Configurations[0].NumberOfDishes.Should().Be(5);
        result.Configurations[0].Servings.Should().Be(2);
    }

    [Fact]
    public void MigrateIfNeeded_ReturnsUnchanged_WhenAlreadyNewFormat()
    {
        // Arrange
        var newFormatParams = new MenuParameters
        {
            WeekStartDate = GetNextMonday(),
            SeasonalFoods = true,
            Configurations = new List<DishConfigurationDto>
            {
                new()
                {
                    NumberOfDishes = 7,
                    Servings = 3,
                    Parameters = new DishConfigurationParametersDto()
                }
            }
        };

        // Act
        var result = MenuParametersMigrationHelper.MigrateIfNeeded(newFormatParams);

        // Assert
        result.Should().BeSameAs(newFormatParams);
        result.Configurations[0].NumberOfDishes.Should().Be(7);
    }

    [Fact]
    public void MigrateIfNeeded_MigratesLegacyFormat_ToNewFormat()
    {
        // Arrange
#pragma warning disable CS0618
        var legacyParams = new MenuParameters
        {
            WeekStartDate = GetNextMonday(),
            SeasonalFoods = false,
            NumberOfDishes = new List<NumberOfDishesDto>
            {
                new() { NumberOfDishes = 6, Servings = 4 }
            },
            DishTypes = new Dictionary<string, int?> { { "Plat principal", 3 } },
            BannedFoods = new List<string> { "Champignons" },
            MaxPreparationTime = TimeSpan.FromMinutes(30),
            MaxCookingTime = TimeSpan.FromMinutes(45),
            MinKcalPerDish = 400,
            MaxKcalPerDish = 800
        };
#pragma warning restore CS0618

        // Act
        var result = MenuParametersMigrationHelper.MigrateIfNeeded(legacyParams);

        // Assert
        result.IsLegacyFormat.Should().BeFalse();
        result.Configurations.Should().HaveCount(1);
        result.Configurations[0].NumberOfDishes.Should().Be(6);
        result.Configurations[0].Servings.Should().Be(4);
        result.Configurations[0].Parameters.DishTypes.Should().ContainKey("Plat principal");
        result.Configurations[0].Parameters.BannedFoods.Should().Contain("Champignons");
        result.Configurations[0].Parameters.MaxPreparationTime.Should().Be(TimeSpan.FromMinutes(30));
        result.Configurations[0].Parameters.MaxCookingTime.Should().Be(TimeSpan.FromMinutes(45));
        result.Configurations[0].Parameters.MinKcalPerDish.Should().Be(400);
        result.Configurations[0].Parameters.MaxKcalPerDish.Should().Be(800);
    }

    [Fact]
    public void MigrateIfNeeded_PreservesGlobalParameters()
    {
        // Arrange
#pragma warning disable CS0618
        var legacyParams = new MenuParameters
        {
            WeekStartDate = new DateTime(2024, 1, 1),
            SeasonalFoods = true,
            NumberOfDishes = new List<NumberOfDishesDto>
            {
                new() { NumberOfDishes = 5, Servings = 2 }
            }
        };
#pragma warning restore CS0618

        // Act
        var result = MenuParametersMigrationHelper.MigrateIfNeeded(legacyParams);

        // Assert
        result.WeekStartDate.Should().Be(new DateTime(2024, 1, 1));
        result.SeasonalFoods.Should().BeTrue();
    }

    [Fact]
    public void CreateDefaultParameters_ReturnsValidDefaults()
    {
        // Act
        var result = MenuParametersMigrationHelper.CreateDefaultParameters();

        // Assert
        result.Should().NotBeNull();
        result.WeekStartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.SeasonalFoods.Should().BeTrue();
        result.Configurations.Should().HaveCount(1);
        result.Configurations[0].NumberOfDishes.Should().Be(5);
        result.Configurations[0].Servings.Should().Be(2);
    }

    [Fact]
    public void CloneParameters_CreatesDeepCopy()
    {
        // Arrange
        var source = new DishConfigurationParametersDto
        {
            DishTypes = new Dictionary<string, int?> { { "Plat principal", 3 } },
            BannedFoods = new List<string> { "Fruits de mer" },
            DesiredFoods = new List<DesiredFoodDto>
            {
                new() { Food = "Poulet", Weight = 5 }
            },
            WeightedOptions = new Dictionary<string, int?> { { "Végétarien", 2 } },
            MaxPreparationTime = TimeSpan.FromMinutes(20),
            MaxCookingTime = TimeSpan.FromMinutes(30),
            MinKcalPerDish = 300,
            MaxKcalPerDish = 600
        };

        // Act
        var clone = MenuParametersMigrationHelper.CloneParameters(source);

        // Assert
        clone.Should().NotBeSameAs(source);
        clone.DishTypes.Should().BeEquivalentTo(source.DishTypes);
        clone.BannedFoods.Should().BeEquivalentTo(source.BannedFoods);
        clone.DesiredFoods.Should().BeEquivalentTo(source.DesiredFoods);
        clone.WeightedOptions.Should().BeEquivalentTo(source.WeightedOptions);
        clone.MaxPreparationTime.Should().Be(source.MaxPreparationTime);
        clone.MaxCookingTime.Should().Be(source.MaxCookingTime);
        clone.MinKcalPerDish.Should().Be(source.MinKcalPerDish);
        clone.MaxKcalPerDish.Should().Be(source.MaxKcalPerDish);

        // Verify it's a deep copy by modifying source
        source.BannedFoods.Add("Test");
        clone.BannedFoods.Should().NotContain("Test");
    }

    [Fact]
    public void CopyParameters_CopiesAllByDefault()
    {
        // Arrange
        var source = new DishConfigurationParametersDto
        {
            DishTypes = new Dictionary<string, int?> { { "Entrée", 2 } },
            BannedFoods = new List<string> { "Gluten" },
            DesiredFoods = new List<DesiredFoodDto>
            {
                new() { Food = "Saumon", Weight = 3 }
            },
            WeightedOptions = new Dictionary<string, int?> { { "Rapide", 5 } },
            MaxPreparationTime = TimeSpan.FromMinutes(15),
            MaxCookingTime = TimeSpan.FromMinutes(25),
            MinKcalPerDish = 200,
            MaxKcalPerDish = 500
        };
        var target = new DishConfigurationParametersDto();

        // Act
        MenuParametersMigrationHelper.CopyParameters(source, target);

        // Assert
        target.DishTypes.Should().BeEquivalentTo(source.DishTypes);
        target.BannedFoods.Should().BeEquivalentTo(source.BannedFoods);
        target.DesiredFoods.Should().BeEquivalentTo(source.DesiredFoods);
        target.WeightedOptions.Should().BeEquivalentTo(source.WeightedOptions);
        target.MaxPreparationTime.Should().Be(source.MaxPreparationTime);
        target.MaxCookingTime.Should().Be(source.MaxCookingTime);
        target.MinKcalPerDish.Should().Be(source.MinKcalPerDish);
        target.MaxKcalPerDish.Should().Be(source.MaxKcalPerDish);
    }

    [Fact]
    public void CopyParameters_SelectiveCopy_OnlyTime()
    {
        // Arrange
        var source = new DishConfigurationParametersDto
        {
            DishTypes = new Dictionary<string, int?> { { "Entrée", 2 } },
            BannedFoods = new List<string> { "Gluten" },
            MaxPreparationTime = TimeSpan.FromMinutes(15),
            MaxCookingTime = TimeSpan.FromMinutes(25),
            MinKcalPerDish = 200,
            MaxKcalPerDish = 500
        };
        var target = new DishConfigurationParametersDto();

        // Act
        MenuParametersMigrationHelper.CopyParameters(
            source, target,
            copyDishTypes: false,
            copyBannedFoods: false,
            copyDesiredFoods: false,
            copyOptions: false,
            copyTime: true,
            copyNutrition: false);

        // Assert
        target.DishTypes.Should().BeEmpty();
        target.BannedFoods.Should().BeEmpty();
        target.MaxPreparationTime.Should().Be(TimeSpan.FromMinutes(15));
        target.MaxCookingTime.Should().Be(TimeSpan.FromMinutes(25));
        target.MinKcalPerDish.Should().BeNull();
        target.MaxKcalPerDish.Should().BeNull();
    }

    [Fact]
    public void CopyParameters_SelectiveCopy_OnlyNutrition()
    {
        // Arrange
        var source = new DishConfigurationParametersDto
        {
            MaxPreparationTime = TimeSpan.FromMinutes(15),
            MinKcalPerDish = 300,
            MaxKcalPerDish = 700
        };
        var target = new DishConfigurationParametersDto();

        // Act
        MenuParametersMigrationHelper.CopyParameters(
            source, target,
            copyDishTypes: false,
            copyBannedFoods: false,
            copyDesiredFoods: false,
            copyOptions: false,
            copyTime: false,
            copyNutrition: true);

        // Assert
        target.MaxPreparationTime.Should().BeNull();
        target.MinKcalPerDish.Should().Be(300);
        target.MaxKcalPerDish.Should().Be(700);
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
