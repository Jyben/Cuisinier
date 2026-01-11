using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Core.Validators;

public class MenuParametersValidator : AbstractValidator<MenuParameters>
{
    public MenuParametersValidator()
    {
        RuleFor(x => x.WeekStartDate)
            .NotEmpty()
            .WithMessage("Week start date is required")
            .Must(BeValidWeekStartDate)
            .WithMessage("Week start date must be a Monday");

        RuleFor(x => x.NumberOfDishes)
            .NotEmpty()
            .WithMessage("At least one number of dishes configuration is required");

        RuleForEach(x => x.NumberOfDishes)
            .SetValidator(new NumberOfDishesDtoValidator());

        RuleFor(x => x.MaxPreparationTime)
            .Must(time => !time.HasValue || time.Value.TotalMinutes > 0)
            .WithMessage("Maximum preparation time must be positive");

        RuleFor(x => x.MaxCookingTime)
            .Must(time => !time.HasValue || time.Value.TotalMinutes > 0)
            .WithMessage("Maximum cooking time must be positive");

        RuleFor(x => x.MinKcalPerDish)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinKcalPerDish.HasValue);
            
        RuleFor(x => x.MaxKcalPerDish)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxKcalPerDish.HasValue);
            
        RuleFor(x => x)
            .Must(x => !x.MinKcalPerDish.HasValue || !x.MaxKcalPerDish.HasValue || x.MinKcalPerDish.Value <= x.MaxKcalPerDish.Value)
            .WithMessage("Les calories minimales doivent être inférieures ou égales aux calories maximales");
    }

    private bool BeValidWeekStartDate(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Monday;
    }
}

