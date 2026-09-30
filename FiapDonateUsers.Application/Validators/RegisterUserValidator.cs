using FluentValidation;
using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Domain.ValueObjects;

namespace FiapDonateUsers.Application.Validators;

public class RegisterUserValidator : AbstractValidator<RegisterUserDto>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.NomeNormalizado).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.SenhaNormalizada).NotEmpty().MinimumLength(6);
        RuleFor(x => x.CpfNormalizado)
            .Must(cpf => string.IsNullOrWhiteSpace(cpf) || CpfValidator.IsValid(cpf))
            .WithMessage("CPF inválido.");
    }
}