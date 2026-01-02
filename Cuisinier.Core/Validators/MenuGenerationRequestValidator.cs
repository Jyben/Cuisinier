using FluentValidation;
using Cuisinier.Core.DTOs;

namespace Cuisinier.Core.Validators;

public class MenuGenerationRequestValidator : AbstractValidator<MenuGenerationRequest>
{
    public MenuGenerationRequestValidator()
    {
        RuleFor(x => x.Parameters)
            .NotNull()
            .WithMessage("Parameters are required")
            .SetValidator(new MenuParametersValidator());

        RuleFor(x => x.RecipeIds)
            .Must(ids => ids == null || ids.All(id => id > 0))
            .WithMessage("All recipe IDs must be positive");
    }
}

