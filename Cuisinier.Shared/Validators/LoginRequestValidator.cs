using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Shared.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("L'email est requis")
            .EmailAddress()
            .WithMessage("L'email doit être une adresse email valide");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Le mot de passe est requis");
    }
}