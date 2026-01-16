using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Core.Validators;

public class MenuParametersValidator : AbstractValidator<MenuParameters>
{
    public MenuParametersValidator()
    {
        RuleFor(x => x.WeekStartDate)
            .NotEmpty()
            .WithMessage("La date de début de semaine est requise")
            .Must(BeValidWeekStartDate)
            .WithMessage("La date de début de semaine doit être un lundi");

        // Nouveau format : Configurations
        RuleFor(x => x.Configurations)
            .NotEmpty()
            .WithMessage("Au moins une configuration est requise")
            .When(x => !x.IsLegacyFormat);

        RuleForEach(x => x.Configurations)
            .SetValidator(new Cuisinier.Shared.Validators.DishConfigurationDtoValidator())
            .When(x => !x.IsLegacyFormat);

        // Ancien format (rétrocompatibilité) : NumberOfDishes
#pragma warning disable CS0618 // Type or member is obsolete
        RuleFor(x => x.NumberOfDishes)
            .NotEmpty()
            .WithMessage("Au moins une configuration de nombre de plats est requise")
            .When(x => x.IsLegacyFormat);

        RuleForEach(x => x.NumberOfDishes)
            .SetValidator(new NumberOfDishesDtoValidator())
            .When(x => x.IsLegacyFormat);

        RuleFor(x => x.MaxPreparationTime)
            .Must(time => !time.HasValue || time.Value.TotalMinutes > 0)
            .WithMessage("Le temps de préparation maximum doit être positif")
            .When(x => x.IsLegacyFormat);

        RuleFor(x => x.MaxCookingTime)
            .Must(time => !time.HasValue || time.Value.TotalMinutes > 0)
            .WithMessage("Le temps de cuisson maximum doit être positif")
            .When(x => x.IsLegacyFormat);

        RuleFor(x => x.MinKcalPerDish)
            .GreaterThanOrEqualTo(0)
            .When(x => x.IsLegacyFormat && x.MinKcalPerDish.HasValue);

        RuleFor(x => x.MaxKcalPerDish)
            .GreaterThanOrEqualTo(0)
            .When(x => x.IsLegacyFormat && x.MaxKcalPerDish.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MinKcalPerDish.HasValue || !x.MaxKcalPerDish.HasValue || x.MinKcalPerDish.Value <= x.MaxKcalPerDish.Value)
            .WithMessage("Les calories minimales doivent être inférieures ou égales aux calories maximales")
            .When(x => x.IsLegacyFormat);
#pragma warning restore CS0618
    }

    private bool BeValidWeekStartDate(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Monday;
    }
}

