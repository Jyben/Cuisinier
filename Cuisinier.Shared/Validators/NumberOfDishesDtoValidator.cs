using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Shared.Validators;

public class NumberOfDishesDtoValidator : AbstractValidator<NumberOfDishesDto>
{
    public NumberOfDishesDtoValidator()
    {
        RuleFor(x => x.NumberOfDishes)
            .GreaterThan(0)
            .WithMessage("Number of dishes must be greater than 0");

        RuleFor(x => x.Servings)
            .GreaterThan(0)
            .WithMessage("Number of servings must be greater than 0");
    }
}

