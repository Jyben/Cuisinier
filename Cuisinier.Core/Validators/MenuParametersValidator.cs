using FluentValidation;
using Cuisinier.Core.DTOs;

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

        RuleFor(x => x.TotalKcalPerDish)
            .Must(kcal => !kcal.HasValue || kcal.Value > 0)
            .WithMessage("Total calories per dish must be positive");
    }

    private bool BeValidWeekStartDate(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Monday;
    }
}

