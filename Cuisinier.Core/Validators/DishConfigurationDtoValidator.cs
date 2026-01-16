using FluentValidation;
using Cuisinier.Core.DTOs;

namespace Cuisinier.Core.Validators;

public class DishConfigurationDtoValidator : AbstractValidator<DishConfigurationDto>
{
    public DishConfigurationDtoValidator()
    {
        RuleFor(x => x.NumberOfDishes)
            .GreaterThan(0)
            .WithMessage("Le nombre de plats doit être supérieur à 0")
            .LessThanOrEqualTo(20)
            .WithMessage("Le nombre de plats ne peut pas dépasser 20");

        RuleFor(x => x.Servings)
            .GreaterThan(0)
            .WithMessage("Le nombre de personnes doit être supérieur à 0")
            .LessThanOrEqualTo(20)
            .WithMessage("Le nombre de personnes ne peut pas dépasser 20");

        RuleFor(x => x.Parameters)
            .NotNull()
            .WithMessage("Les paramètres de configuration sont requis")
            .SetValidator(new DishConfigurationParametersDtoValidator());
    }
}

public class DishConfigurationParametersDtoValidator : AbstractValidator<DishConfigurationParametersDto>
{
    public DishConfigurationParametersDtoValidator()
    {
        RuleFor(x => x.MaxPreparationTime)
            .Must(time => !time.HasValue || time.Value.TotalMinutes > 0)
            .WithMessage("Le temps de préparation maximum doit être positif");

        RuleFor(x => x.MaxCookingTime)
            .Must(time => !time.HasValue || time.Value.TotalMinutes > 0)
            .WithMessage("Le temps de cuisson maximum doit être positif");

        RuleFor(x => x.MinKcalPerDish)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinKcalPerDish.HasValue)
            .WithMessage("Les calories minimales doivent être positives ou nulles");

        RuleFor(x => x.MaxKcalPerDish)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxKcalPerDish.HasValue)
            .WithMessage("Les calories maximales doivent être positives ou nulles");

        RuleFor(x => x)
            .Must(x => !x.MinKcalPerDish.HasValue || !x.MaxKcalPerDish.HasValue || x.MinKcalPerDish.Value <= x.MaxKcalPerDish.Value)
            .WithMessage("Les calories minimales doivent être inférieures ou égales aux calories maximales");
    }
}
