using FluentValidation;
using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Domain.ValueObjects;

namespace FiapDonateUsers.Application.Validators;

public class RegisterUserValidator : AbstractValidator<RegisterUserDto>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Cpf).NotEmpty()
            .Must(CpfValidator.IsValid)
            .WithMessage("CPF inválido.");
    }
}