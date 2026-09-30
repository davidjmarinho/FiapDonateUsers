using System.Text.Json.Serialization;

namespace FiapDonateUsers.Application.DTOs;

public class RegisterUserDto
{
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Cpf { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string? FullName { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    public string NomeNormalizado => !string.IsNullOrWhiteSpace(Nome) ? Nome : FullName ?? Name ?? string.Empty;

    public string SenhaNormalizada => !string.IsNullOrWhiteSpace(Senha) ? Senha : Password ?? string.Empty;

    public string CpfNormalizado => Cpf ?? string.Empty;
}