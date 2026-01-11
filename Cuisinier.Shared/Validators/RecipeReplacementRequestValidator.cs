using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Shared.Validators;

public class RecipeReplacementRequestValidator : AbstractValidator<RecipeReplacementRequest>
{
    public RecipeReplacementRequestValidator()
    {
        RuleFor(x => x.Parameters)
            .NotNull()
            .WithMessage("Parameters are required")
            .SetValidator(new MenuParametersValidator());
    }
}

