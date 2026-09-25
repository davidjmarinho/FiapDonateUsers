using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Domain.ValueObjects;
using FiapDonateUsers.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiapDonateUsers.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private const string DoadorRole = "Doador";

    private readonly UserManager<ApplicationUser> _userManager;
    public UsersController(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserDto dto)
    {
        var cpf = CpfValidator.Normalize(dto.Cpf);

        var cpfEmUso = await _userManager.Users.AnyAsync(u => u.Cpf == cpf);
        if (cpfEmUso)
            return BadRequest(new { mensagem = "Já existe um usuário cadastrado com este CPF." });

        var usuario = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            Nome = dto.Nome,
            Cpf = cpf
        };

        var resultado = await _userManager.CreateAsync(usuario, dto.Senha);

        if (!resultado.Succeeded)
            return BadRequest(resultado.Errors);

        await _userManager.AddToRoleAsync(usuario, DoadorRole);

        return Ok(new { mensagem = "Usuário registrado com sucesso." });
    }
}