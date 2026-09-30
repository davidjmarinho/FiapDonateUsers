using System.Text.Json.Serialization;

namespace FiapDonateUsers.Application.DTOs;

public class LoginUserDto
{
    public string Email { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    public string SenhaNormalizada => !string.IsNullOrWhiteSpace(Senha) ? Senha : Password ?? string.Empty;
}
