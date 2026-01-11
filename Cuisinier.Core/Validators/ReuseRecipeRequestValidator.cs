using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Core.Validators;

public class ReuseRecipeRequestValidator : AbstractValidator<ReuseRecipeRequest>
{
    public ReuseRecipeRequestValidator()
    {
        RuleFor(x => x.MenuId)
            .GreaterThan(0)
            .WithMessage("Menu ID must be greater than 0");
    }
}

