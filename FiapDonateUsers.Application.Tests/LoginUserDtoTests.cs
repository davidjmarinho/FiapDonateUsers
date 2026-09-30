using FiapDonateUsers.Application.DTOs;

namespace FiapDonateUsers.Application.Tests;

public class LoginUserDtoTests
{
    [Fact]
    public void LoginUserDto_DeveAceitarEmailESenha()
    {
        var dto = new LoginUserDto
        {
            Email = "user@fiapdonate.com",
            Senha = "Senha@123"
        };

        Assert.Equal("user@fiapdonate.com", dto.Email);
        Assert.Equal("Senha@123", dto.Senha);
    }
}
