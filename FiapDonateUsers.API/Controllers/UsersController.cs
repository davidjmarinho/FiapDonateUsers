using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Infrastructure.Identity;

namespace FiapDonateUsers.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    public UsersController(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserDto dto)
    {
        var usuario = new ApplicationUser { UserName = dto.Email, Email = dto.Email, Nome = dto.Nome };
        var resultado = await _userManager.CreateAsync(usuario, dto.Senha);

        if (!resultado.Succeeded)
            return BadRequest(resultado.Errors);

        await _userManager.AddToRoleAsync(usuario, dto.Role);

        return Ok(new { mensagem = "Usuário registrado com sucesso." });
    }
}