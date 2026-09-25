using Microsoft.AspNetCore.Identity;

namespace FiapDonateUsers.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
}