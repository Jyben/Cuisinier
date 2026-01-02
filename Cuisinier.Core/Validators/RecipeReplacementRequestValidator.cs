using FluentValidation;
using Cuisinier.Core.DTOs;

namespace Cuisinier.Core.Validators;

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

