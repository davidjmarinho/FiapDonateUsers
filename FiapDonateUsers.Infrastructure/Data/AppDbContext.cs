using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FiapDonateUsers.Infrastructure.Identity;

namespace FiapDonateUsers.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .Property(u => u.Cpf)
            .HasMaxLength(11)
            .IsRequired();

        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.Cpf)
            .IsUnique();
    }
}