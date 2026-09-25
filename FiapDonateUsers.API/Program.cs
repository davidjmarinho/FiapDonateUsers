using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FiapDonateUsers.Application.Validators;
using FiapDonateUsers.Infrastructure;
using FiapDonateUsers.Infrastructure.Data;
using FiapDonateUsers.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FiapDonateUsers", Version = "v1" });
});

// Tag "ready" marca o check que depende do SQL Server: uma indisponibilidade
// transitória do banco deve tirar o pod de circulação (readiness), mas NÃO
// deve reiniciar o processo via liveness.
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sqlserver", tags: new[] { "ready" });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await RoleSeeder.SeedAsync(roleManager);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var adminEmail = builder.Configuration["Admin:Email"] ?? "gestor@fiapdonate.com";
    var adminPassword = builder.Configuration["Admin:Password"] ?? "Gestor@123";
    await AdminSeeder.SeedAsync(userManager, adminEmail, adminPassword);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// /health/live: só confirma que o processo está de pé, sem checar dependências
// externas. Usado pela livenessProbe.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// /health/ready: executa os checks marcados com a tag "ready" (SQL Server).
// Usado pela readinessProbe.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();