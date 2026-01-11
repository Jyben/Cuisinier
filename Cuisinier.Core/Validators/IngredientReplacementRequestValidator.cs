using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Core.Validators;

public class IngredientReplacementRequestValidator : AbstractValidator<IngredientReplacementRequest>
{
    public IngredientReplacementRequestValidator()
    {
        RuleFor(x => x.IngredientToReplace)
            .NotEmpty()
            .WithMessage("Ingredient to replace is required")
            .MaximumLength(200)
            .WithMessage("Ingredient to replace must not exceed 200 characters");

        RuleFor(x => x.NewIngredient)
            .NotEmpty()
            .WithMessage("New ingredient is required")
            .MaximumLength(200)
            .WithMessage("New ingredient must not exceed 200 characters");
    }
}

