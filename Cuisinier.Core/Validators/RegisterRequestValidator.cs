using FluentValidation;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Core.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("L'email est requis")
            .EmailAddress()
            .WithMessage("L'email doit être une adresse email valide")
            .MaximumLength(256)
            .WithMessage("L'email ne peut pas dépasser 256 caractères");

        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("Le nom d'utilisateur est requis")
            .MinimumLength(3)
            .WithMessage("Le nom d'utilisateur doit contenir au moins 3 caractères")
            .MaximumLength(256)
            .WithMessage("Le nom d'utilisateur ne peut pas dépasser 256 caractères");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Le mot de passe est requis")
            .MinimumLength(8)
            .WithMessage("Le mot de passe doit contenir au moins 8 caractères")
            .Matches(@"[A-Z]")
            .WithMessage("Le mot de passe doit contenir au moins une majuscule")
            .Matches(@"[a-z]")
            .WithMessage("Le mot de passe doit contenir au moins une minuscule")
            .Matches(@"[0-9]")
            .WithMessage("Le mot de passe doit contenir au moins un chiffre")
            .Matches(@"[^a-zA-Z0-9]")
            .WithMessage("Le mot de passe doit contenir au moins un caractère spécial");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("La confirmation du mot de passe est requise")
            .Equal(x => x.Password)
            .WithMessage("Les mots de passe ne correspondent pas");
    }
}