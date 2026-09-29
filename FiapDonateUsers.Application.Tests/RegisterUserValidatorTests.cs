using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Application.Validators;

namespace FiapDonateUsers.Application.Tests;

public class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    [Fact]
    public void Validate_ComDadosValidos_NaoRetornaErros()
    {
        var dto = new RegisterUserDto("Maria Silva", "maria@example.com", "111.444.777-35", "SenhaForte1");

        var resultado = _validator.Validate(dto);

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void Validate_ComCpfInvalido_RetornaErroNoCampoCpf()
    {
        var dto = new RegisterUserDto("Maria Silva", "maria@example.com", "111.111.111-11", "SenhaForte1");

        var resultado = _validator.Validate(dto);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == "Cpf");
    }

    [Fact]
    public void Validate_ComEmailInvalido_RetornaErroNoCampoEmail()
    {
        var dto = new RegisterUserDto("Maria Silva", "nao-e-email", "111.444.777-35", "SenhaForte1");

        var resultado = _validator.Validate(dto);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_ComSenhaCurta_RetornaErroNoCampoSenha()
    {
        var dto = new RegisterUserDto("Maria Silva", "maria@example.com", "111.444.777-35", "123");

        var resultado = _validator.Validate(dto);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == "Senha");
    }
}
