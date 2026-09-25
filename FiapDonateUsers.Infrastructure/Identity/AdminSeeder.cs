using Microsoft.AspNetCore.Identity;

namespace FiapDonateUsers.Infrastructure.Identity;

public static class AdminSeeder
{
    public static async Task SeedAsync(UserManager<ApplicationUser> userManager, string email, string senha)
    {
        var existentes = await userManager.GetUsersInRoleAsync("GestorONG");
        if (existentes.Count > 0)
            return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Nome = "Gestor Padrão",
            Cpf = "00000000000" // conta de sistema — não passa pelo RegisterUserValidator
        };

        var resultado = await userManager.CreateAsync(admin, senha);
        if (!resultado.Succeeded)
            throw new InvalidOperationException(
                $"Falha ao criar usuário GestorONG padrão: {string.Join(", ", resultado.Errors.Select(e => e.Description))}");

        var roleResult = await userManager.AddToRoleAsync(admin, "GestorONG");
        if (!roleResult.Succeeded)
            throw new InvalidOperationException(
                $"Falha ao atribuir role GestorONG: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
    }
}
