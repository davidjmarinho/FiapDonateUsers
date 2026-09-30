using FiapDonateUsers.API.Services;
using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Domain.ValueObjects;
using FiapDonateUsers.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
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
    private readonly ITokenService _tokenService;

    public UsersController(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginUserDto dto)
    {
        var email = dto.Email.Trim();
        var senha = dto.SenhaNormalizada;

        var usuario = await _userManager.FindByEmailAsync(email);
        if (usuario is null || !await _userManager.CheckPasswordAsync(usuario, senha))
            return Unauthorized(new { mensagem = "Email ou senha inválidos." });

        var roles = await _userManager.GetRolesAsync(usuario);
        var token = _tokenService.GenerateToken(usuario, roles);

        return Ok(new { token });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserDto dto)
    {
        var nome = dto.NomeNormalizado;
        var email = dto.Email.Trim();
        var senha = dto.SenhaNormalizada;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            return BadRequest(new { mensagem = "Email e senha são obrigatórios." });

        var cpf = string.IsNullOrWhiteSpace(dto.CpfNormalizado)
            ? GenerateFallbackCpf()
            : CpfValidator.Normalize(dto.CpfNormalizado);

        var cpfEmUso = await _userManager.Users.AnyAsync(u => u.Cpf == cpf);
        if (cpfEmUso)
            return BadRequest(new { mensagem = "Já existe um usuário cadastrado com este CPF." });

        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Nome = nome,
            Cpf = cpf
        };

        try
        {
            var resultado = await _userManager.CreateAsync(usuario, senha);

            if (!resultado.Succeeded)
                return BadRequest(resultado.Errors);
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { mensagem = "Já existe um usuário cadastrado com este CPF." });
        }

        var roleResult = await _userManager.AddToRoleAsync(usuario, DoadorRole);

        if (!roleResult.Succeeded)
            return StatusCode(500, new { mensagem = "Falha ao atribuir função de Doador.", erros = roleResult.Errors });

        var token = _tokenService.GenerateToken(usuario, new[] { DoadorRole });

        return Ok(new { mensagem = "Usuário registrado com sucesso.", token });
    }

    private static string GenerateFallbackCpf()
    {
        var source = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        return source.Length >= 11 ? source[^11..] : source.PadLeft(11, '0');
    }
}