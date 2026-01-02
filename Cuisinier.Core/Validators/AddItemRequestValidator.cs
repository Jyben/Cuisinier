using FluentValidation;
using Cuisinier.Core.DTOs;

namespace Cuisinier.Core.Validators;

public class AddItemRequestValidator : AbstractValidator<AddItemRequest>
{
    public AddItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Item name is required")
            .MaximumLength(200)
            .WithMessage("Item name must not exceed 200 characters");

        RuleFor(x => x.Quantity)
            .MaximumLength(100)
            .WithMessage("Quantity must not exceed 100 characters");

        RuleFor(x => x.Category)
            .MaximumLength(100)
            .WithMessage("Category must not exceed 100 characters");
    }
}

