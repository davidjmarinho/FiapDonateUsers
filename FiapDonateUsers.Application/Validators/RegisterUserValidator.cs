using FluentValidation;
using FiapDonateUsers.Application.DTOs;

namespace FiapDonateUsers.Application.Validators;

public class RegisterUserValidator : AbstractValidator<RegisterUserDto>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Role).Must(r => r == "GestorONG" || r == "Doador")
            .WithMessage("Role inválida. Use 'GestorONG' ou 'Doador'.");
    }
}