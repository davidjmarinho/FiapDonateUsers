using FiapDonateUsers.Domain.ValueObjects;

namespace FiapDonateUsers.Domain.Tests;

public class CpfValidatorTests
{
    [Theory]
    [InlineData("111.444.777-35")]
    [InlineData("11144477735")]
    public void IsValid_ComCpfValido_RetornaTrue(string cpf)
    {
        Assert.True(CpfValidator.IsValid(cpf));
    }

    [Theory]
    [InlineData("111.444.777-36")] // dígito verificador errado
    [InlineData("111.111.111-11")] // todos os dígitos iguais
    [InlineData("123")]            // tamanho errado
    [InlineData("")]
    public void IsValid_ComCpfInvalido_RetornaFalse(string cpf)
    {
        Assert.False(CpfValidator.IsValid(cpf));
    }

    [Fact]
    public void Normalize_RemoveMascara()
    {
        Assert.Equal("11144477735", CpfValidator.Normalize("111.444.777-35"));
    }
}
