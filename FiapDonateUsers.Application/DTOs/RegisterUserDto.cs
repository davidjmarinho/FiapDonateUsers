namespace FiapDonateUsers.Application.DTOs;

public record RegisterUserDto(string Nome, string Email, string Senha, string Role); // Role: "GestorONG" ou "Doador"