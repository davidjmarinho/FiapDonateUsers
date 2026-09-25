# Cadastro de Doador Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the public Doador registration endpoint (Item 3 of the hackathon) on `FiapDonateUsers`, close the RBAC hole where a caller could self-assign `GestorONG`, add CPF validation, bring the service to DevOps parity with `FiapDonateCampaign`/`FiapDonateWorker` (Docker, k8s, CI, health checks), and fix the public transparency panel in `FiapDonateCampaign`.

**Architecture:** `FiapDonateUsers` owns the shared ASP.NET Identity schema (same physical SQL Server database as `FiapDonateCampaign`, via matching `ConnectionStrings:DefaultConnection`). It exposes `POST /api/users/register` (public, always assigns role `Doador`) and seeds a default `GestorONG` account at startup. `FiapDonateCampaign` only reads that schema (never migrates it) to issue JWTs at login — this is already correct and unchanged by this plan, except for one `[AllowAnonymous]` fix.

**Tech Stack:** .NET 8, ASP.NET Core Identity, EF Core 8 + SQL Server, FluentValidation 12.1.1, xUnit, Docker, Kubernetes.

## Global Constraints

- Target framework `net8.0` on every project (matches sibling repos).
- Passwords stored via ASP.NET Identity's built-in hasher (already satisfies "senha com hash" — no BCrypt package needed).
- Email uniqueness is enforced indirectly: `UserName` is always set to `Email`, and Identity enforces unique normalized usernames by default.
- CPF must pass real check-digit validation (not just length) and be unique in the database.
- The public register endpoint must never let the caller choose a role — it always assigns `Doador`.
- `FiapDonateCampaign`'s `IdentityStoreDbContext` must never gain a migration for Identity tables — `FiapDonateUsers` is the sole owner (per existing comment in that file).
- `FluentValidation` version `12.1.1`, `Microsoft.EntityFrameworkCore.SqlServer` version `8.0.0` — match versions already used in `FiapDonateUsers.Infrastructure.csproj`/`FiapDonateUsers.Application.csproj`.

---

## Task 1: CpfValidator (Domain, TDD)

**Files:**
- Create: `FiapDonateUsers.Domain/ValueObjects/CpfValidator.cs`
- Create: `FiapDonateUsers.Domain.Tests/FiapDonateUsers.Domain.Tests.csproj`
- Create: `FiapDonateUsers.Domain.Tests/CpfValidatorTests.cs`
- Modify: `FiapDonateUsers.slnx`

**Interfaces:**
- Produces: `FiapDonateUsers.Domain.ValueObjects.CpfValidator.IsValid(string cpf) : bool`, `CpfValidator.Normalize(string cpf) : string` — used by Task 3 (`RegisterUserValidator`) and Task 4 (`UsersController`).

- [ ] **Step 1: Create the test project**

```xml
<!-- FiapDonateUsers.Domain.Tests/FiapDonateUsers.Domain.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FiapDonateUsers.Domain\FiapDonateUsers.Domain.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing tests**

```csharp
// FiapDonateUsers.Domain.Tests/CpfValidatorTests.cs
using FiapDonateUsers.Domain.ValueObjects;

namespace FiapDonateUsers.Domain.Tests;

public class CpfValidatorTests
{
    [Theory]
    [InlineData("111.444.777-35")]
    [InlineData("11144477735")]
    public void IsValid_ComCpfValido_RetornaTrue(string cpf)
    {
        Assert.True(CpfValidator.IsValid(cpf));
    }

    [Theory]
    [InlineData("111.444.777-36")] // dígito verificador errado
    [InlineData("111.111.111-11")] // todos os dígitos iguais
    [InlineData("123")]            // tamanho errado
    [InlineData("")]
    public void IsValid_ComCpfInvalido_RetornaFalse(string cpf)
    {
        Assert.False(CpfValidator.IsValid(cpf));
    }

    [Fact]
    public void Normalize_RemoveMascara()
    {
        Assert.Equal("11144477735", CpfValidator.Normalize("111.444.777-35"));
    }
}
```

- [ ] **Step 3: Add the test project to the solution and confirm the tests fail to compile**

```bash
# In FiapDonateUsers.slnx, add a /tests/ folder with the new project:
```

```xml
<!-- FiapDonateUsers.slnx (full file) -->
<Solution>
  <Folder Name="/src/">
    <Project Path="FiapDonateUsers.API/FiapDonateUsers.API.csproj" />
    <Project Path="FiapDonateUsers.Application/FiapDonateUsers.Application.csproj" />
    <Project Path="FiapDonateUsers.Domain/FiapDonateUsers.Domain.csproj" />
    <Project Path="FiapDonateUsers.Infrastructure/FiapDonateUsers.Infrastructure.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="FiapDonateUsers.Domain.Tests/FiapDonateUsers.Domain.Tests.csproj" />
    <Project Path="FiapDonateUsers.Application.Tests/FiapDonateUsers.Application.Tests.csproj" />
  </Folder>
</Solution>
```

Run: `dotnet test FiapDonateUsers.slnx`
Expected: FAIL to build — `FiapDonateUsers.Application.Tests` doesn't exist yet (created in Task 3) and `CpfValidator` doesn't exist yet. This is expected; continue to Step 4.

- [ ] **Step 4: Implement `CpfValidator`**

```csharp
// FiapDonateUsers.Domain/ValueObjects/CpfValidator.cs
namespace FiapDonateUsers.Domain.ValueObjects;

public static class CpfValidator
{
    public static string Normalize(string cpf)
    {
        return new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    public static bool IsValid(string cpf)
    {
        var digits = Normalize(cpf);

        if (digits.Length != 11)
            return false;

        if (digits.Distinct().Count() == 1)
            return false;

        var numbers = digits.Select(c => c - '0').ToArray();

        var firstCheck = CalculateCheckDigit(numbers, 9);
        if (firstCheck != numbers[9])
            return false;

        var secondCheck = CalculateCheckDigit(numbers, 10);
        if (secondCheck != numbers[10])
            return false;

        return true;
    }

    private static int CalculateCheckDigit(int[] numbers, int length)
    {
        var weight = length + 1;
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += numbers[i] * (weight - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
```

- [ ] **Step 5: Run the Domain tests and verify they pass**

Run: `dotnet test FiapDonateUsers.Domain.Tests/FiapDonateUsers.Domain.Tests.csproj`
Expected: PASS — 5 tests (3 valid/invalid theories collapse into their `InlineData` cases: 2 valid + 4 invalid + 1 normalize = 7 total assertions across `IsValid`/`Normalize`).

- [ ] **Step 6: Commit**

```bash
git add FiapDonateUsers.Domain/ValueObjects/CpfValidator.cs FiapDonateUsers.Domain.Tests FiapDonateUsers.slnx
git commit -m "feat: adiciona CpfValidator com validação de dígito verificador"
```

---

## Task 2: CPF column, unique index and migration

**Files:**
- Modify: `FiapDonateUsers.Infrastructure/Identity/ApplicationUser.cs`
- Modify: `FiapDonateUsers.Infrastructure/Data/AppDbContext.cs`
- Modify: `FiapDonateUsers.API/Program.cs`
- Create: `.config/dotnet-tools.json`
- Create: migration files under `FiapDonateUsers.Infrastructure/Migrations/` (generated by `dotnet ef`, not hand-written)

**Interfaces:**
- Consumes: none new.
- Produces: `ApplicationUser.Cpf : string` — used by Task 4 (`UsersController`) and Task 5 (`AdminSeeder`).

- [ ] **Step 1: Add the tool manifest so `dotnet ef` is reproducible**

```json
// .config/dotnet-tools.json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "8.0.11",
      "commands": [
        "dotnet-ef"
      ],
      "rollForward": false
    }
  }
}
```

Run: `dotnet tool restore`
Expected: `Tool "dotnet-ef" ... is already installed` or a successful install message.

- [ ] **Step 2: Add the `Cpf` property**

```csharp
// FiapDonateUsers.Infrastructure/Identity/ApplicationUser.cs (full file)
using Microsoft.AspNetCore.Identity;

namespace FiapDonateUsers.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Configure the unique index**

```csharp
// FiapDonateUsers.Infrastructure/Data/AppDbContext.cs (full file)
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
```

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AddCpfToAspNetUsers --project FiapDonateUsers.Infrastructure/FiapDonateUsers.Infrastructure.csproj --startup-project FiapDonateUsers.API/FiapDonateUsers.API.csproj
```
Expected: `Done.` and two new files under `FiapDonateUsers.Infrastructure/Migrations/` (`..._AddCpfToAspNetUsers.cs` and `.Designer.cs`), plus an updated `AppDbContextModelSnapshot.cs`.

Verify: open the generated `..._AddCpfToAspNetUsers.cs` and confirm it contains `migrationBuilder.AddColumn<string>(name: "Cpf", table: "AspNetUsers", ...)` followed by `migrationBuilder.CreateIndex(name: "IX_AspNetUsers_Cpf", table: "AspNetUsers", column: "Cpf", unique: true);`. If EF instead generated a full table drop/recreate, stop and re-check Step 3 — it must be additive.

- [ ] **Step 5: Apply migrations automatically at startup**

The current `Program.cs` never applies migrations — a grader would have to run `dotnet ef database update` by hand. Add automatic migration (same pattern `FiapDonateWorker` already uses), so the register endpoint works right after `dotnet run`.

```csharp
// FiapDonateUsers.API/Program.cs (full file)
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using FiapDonateUsers.Application.Validators;
using FiapDonateUsers.Infrastructure;
using FiapDonateUsers.Infrastructure.Data;
using FiapDonateUsers.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FiapDonateUsers", Version = "v1" });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await RoleSeeder.SeedAsync(roleManager);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
```

- [ ] **Step 6: Verify the build**

Run: `dotnet build FiapDonateUsers.slnx`
Expected: `Compilação com êxito` / `Build succeeded`, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add .config FiapDonateUsers.Infrastructure/Identity/ApplicationUser.cs FiapDonateUsers.Infrastructure/Data/AppDbContext.cs FiapDonateUsers.Infrastructure/Migrations FiapDonateUsers.API/Program.cs
git commit -m "feat: adiciona coluna Cpf (única) e aplica migrations automaticamente no startup"
```

---

## Task 3: RegisterUserDto + RegisterUserValidator (TDD)

**Files:**
- Modify: `FiapDonateUsers.Application/DTOs/RegisterUserDto.cs`
- Modify: `FiapDonateUsers.Application/Validators/RegisterUserValidator.cs`
- Create: `FiapDonateUsers.Application.Tests/FiapDonateUsers.Application.Tests.csproj`
- Create: `FiapDonateUsers.Application.Tests/RegisterUserValidatorTests.cs`

**Interfaces:**
- Consumes: `CpfValidator.IsValid` from Task 1.
- Produces: `RegisterUserDto(string Nome, string Email, string Cpf, string Senha)` — used by Task 4 (`UsersController`). Note the field order and that `Role` no longer exists.

- [ ] **Step 1: Create the Application test project**

```xml
<!-- FiapDonateUsers.Application.Tests/FiapDonateUsers.Application.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="FluentValidation" Version="12.1.1" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FiapDonateUsers.Application\FiapDonateUsers.Application.csproj" />
  </ItemGroup>

</Project>
```

(The `.slnx` already references this path from Task 1, Step 3 — no further solution edit needed.)

- [ ] **Step 2: Write the failing tests**

```csharp
// FiapDonateUsers.Application.Tests/RegisterUserValidatorTests.cs
using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Application.Validators;

namespace FiapDonateUsers.Application.Tests;

public class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    [Fact]
    public void Validate_ComDadosValidos_NaoRetornaErros()
    {
        var dto = new RegisterUserDto("Maria Silva", "maria@example.com", "111.444.777-35", "SenhaForte1");

        var resultado = _validator.Validate(dto);

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void Validate_ComCpfInvalido_RetornaErroNoCampoCpf()
    {
        var dto = new RegisterUserDto("Maria Silva", "maria@example.com", "111.111.111-11", "SenhaForte1");

        var resultado = _validator.Validate(dto);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == "Cpf");
    }

    [Fact]
    public void Validate_ComEmailInvalido_RetornaErroNoCampoEmail()
    {
        var dto = new RegisterUserDto("Maria Silva", "nao-e-email", "111.444.777-35", "SenhaForte1");

        var resultado = _validator.Validate(dto);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_ComSenhaCurta_RetornaErroNoCampoSenha()
    {
        var dto = new RegisterUserDto("Maria Silva", "maria@example.com", "111.444.777-35", "123");

        var resultado = _validator.Validate(dto);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == "Senha");
    }
}
```

- [ ] **Step 3: Run tests to verify they fail to compile**

Run: `dotnet test FiapDonateUsers.Application.Tests/FiapDonateUsers.Application.Tests.csproj`
Expected: FAIL — `RegisterUserDto` still has 4 positional args in the wrong order (`Role` instead of `Cpf`) and `RegisterUserValidator` still validates `Role`, not `Cpf`.

- [ ] **Step 4: Update the DTO**

```csharp
// FiapDonateUsers.Application/DTOs/RegisterUserDto.cs (full file)
namespace FiapDonateUsers.Application.DTOs;

public record RegisterUserDto(string Nome, string Email, string Cpf, string Senha);
```

- [ ] **Step 5: Update the validator**

```csharp
// FiapDonateUsers.Application/Validators/RegisterUserValidator.cs (full file)
using FluentValidation;
using FiapDonateUsers.Application.DTOs;
using FiapDonateUsers.Domain.ValueObjects;

namespace FiapDonateUsers.Application.Validators;

public class RegisterUserValidator : AbstractValidator<RegisterUserDto>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Cpf).NotEmpty()
            .Must(CpfValidator.IsValid)
            .WithMessage("CPF inválido.");
    }
}
```

- [ ] **Step 6: Run tests and verify they pass**

Run: `dotnet test FiapDonateUsers.Application.Tests/FiapDonateUsers.Application.Tests.csproj`
Expected: PASS — 4 tests.

- [ ] **Step 7: Commit**

```bash
git add FiapDonateUsers.Application/DTOs/RegisterUserDto.cs FiapDonateUsers.Application/Validators/RegisterUserValidator.cs FiapDonateUsers.Application.Tests
git commit -m "feat: troca Role por Cpf no cadastro e valida CPF real"
```

---

## Task 4: Close the self-assigned-role hole in UsersController

**Files:**
- Modify: `FiapDonateUsers.API/Controllers/UsersController.cs`

**Interfaces:**
- Consumes: `RegisterUserDto` (Task 3), `CpfValidator.Normalize` (Task 1), `ApplicationUser.Cpf` (Task 2).
- Produces: `POST /api/users/register` — public, always assigns role `Doador`.

- [ ] **Step 1: Rewrite the controller**

```csharp
// FiapDonateUsers.API/Controllers/UsersController.cs (full file)
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
```

Note: no client input can select a role anymore — `DoadorRole` is a compile-time constant.

- [ ] **Step 2: Verify the build**

Run: `dotnet build FiapDonateUsers.slnx`
Expected: `Compilação com êxito`, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add FiapDonateUsers.API/Controllers/UsersController.cs
git commit -m "fix: registro público sempre atribui role Doador e valida CPF único"
```

---

## Task 5: AdminSeeder (default GestorONG account)

**Files:**
- Create: `FiapDonateUsers.Infrastructure/Identity/AdminSeeder.cs`
- Modify: `FiapDonateUsers.API/Program.cs`
- Modify: `FiapDonateUsers.API/appsettings.json`

**Interfaces:**
- Consumes: `ApplicationUser` (Task 2).
- Produces: `AdminSeeder.SeedAsync(UserManager<ApplicationUser> userManager, string email, string senha) : Task` — idempotent, only called from `Program.cs`.

- [ ] **Step 1: Implement the seeder**

```csharp
// FiapDonateUsers.Infrastructure/Identity/AdminSeeder.cs
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

        await userManager.AddToRoleAsync(admin, "GestorONG");
    }
}
```

- [ ] **Step 2: Wire it into `Program.cs`, after `RoleSeeder`**

```csharp
// FiapDonateUsers.API/Program.cs (full file)
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using FiapDonateUsers.Application.Validators;
using FiapDonateUsers.Infrastructure;
using FiapDonateUsers.Infrastructure.Data;
using FiapDonateUsers.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FiapDonateUsers", Version = "v1" });
});

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

app.Run();
```

- [ ] **Step 3: Document the defaults in `appsettings.json`**

```json
// FiapDonateUsers.API/appsettings.json (full file)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=FiapDonateDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  },
  "Admin": {
    "Email": "gestor@fiapdonate.com",
    "Password": "Gestor@123"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 4: Verify the build**

Run: `dotnet build FiapDonateUsers.slnx`
Expected: `Compilação com êxito`, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add FiapDonateUsers.Infrastructure/Identity/AdminSeeder.cs FiapDonateUsers.API/Program.cs FiapDonateUsers.API/appsettings.json
git commit -m "feat: seed automático de conta GestorONG padrão no startup"
```

---

## Task 6: Health checks

**Files:**
- Modify: `FiapDonateUsers.API/FiapDonateUsers.API.csproj`
- Modify: `FiapDonateUsers.API/Program.cs`

**Interfaces:**
- Produces: `GET /health/live`, `GET /health/ready` — consumed by Task 8's k8s probes.

- [ ] **Step 1: Add the health check package**

```xml
<!-- FiapDonateUsers.API/FiapDonateUsers.API.csproj (full file) -->
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />
    <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.1" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.30" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FiapDonateUsers.Application\FiapDonateUsers.Application.csproj" />
    <ProjectReference Include="..\FiapDonateUsers.Infrastructure\FiapDonateUsers.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Register and map the checks**

```csharp
// FiapDonateUsers.API/Program.cs (full file)
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
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
```

- [ ] **Step 3: Verify the build**

Run: `dotnet build FiapDonateUsers.slnx`
Expected: `Compilação com êxito`, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add FiapDonateUsers.API/FiapDonateUsers.API.csproj FiapDonateUsers.API/Program.cs
git commit -m "feat: expõe /health/live e /health/ready"
```

---

## Task 7: Dockerfile + local docker-compose

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`
- Create: `docker-compose.yml`

**Interfaces:** none (deployment artifacts only).

- [ ] **Step 1: Create `.dockerignore`**

```
**/bin/
**/obj/
.vs/
.git/
FiapDonateUsers.Domain.Tests/
FiapDonateUsers.Application.Tests/
```

- [ ] **Step 2: Create the Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["FiapDonateUsers.slnx", "./"]
COPY ["FiapDonateUsers.API/FiapDonateUsers.API.csproj", "FiapDonateUsers.API/"]
COPY ["FiapDonateUsers.Application/FiapDonateUsers.Application.csproj", "FiapDonateUsers.Application/"]
COPY ["FiapDonateUsers.Domain/FiapDonateUsers.Domain.csproj", "FiapDonateUsers.Domain/"]
COPY ["FiapDonateUsers.Infrastructure/FiapDonateUsers.Infrastructure.csproj", "FiapDonateUsers.Infrastructure/"]
RUN dotnet restore "FiapDonateUsers.API/FiapDonateUsers.API.csproj"

COPY . .
RUN dotnet publish "FiapDonateUsers.API/FiapDonateUsers.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FiapDonateUsers.API.dll"]
```

- [ ] **Step 3: Create the local dev compose file**

```yaml
# docker-compose.yml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "YourStrong!Passw0rd"
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql

volumes:
  sqlserver-data:
```

- [ ] **Step 4: Build the image and verify it succeeds**

Run: `docker build -t fiapdonateusers:local .`
Expected: image builds successfully, ends with `writing image sha256:...` / `naming to docker.io/library/fiapdonateusers:local`.

- [ ] **Step 5: Commit**

```bash
git add Dockerfile .dockerignore docker-compose.yml
git commit -m "chore: adiciona Dockerfile e compose de desenvolvimento local"
```

---

## Task 8: Kubernetes manifests

**Files:**
- Create: `k8s/configmap.yaml`
- Create: `k8s/secret.example.yaml`
- Create: `k8s/deployment.yaml`
- Create: `k8s/service.yaml`

**Interfaces:** none (deployment artifacts only).

- [ ] **Step 1: ConfigMap**

```yaml
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: fiapdonateusers-config
data:
  Admin__Email: "gestor@fiapdonate.com"
```

- [ ] **Step 2: Secret example**

```yaml
# k8s/secret.example.yaml
apiVersion: v1
kind: Secret
metadata:
  name: fiapdonateusers-secret
type: Opaque
stringData:
  Admin__Password: "CHANGE_ME"
  # Host "sqlserver" assume o nome do Service Kubernetes exposto pela infra
  # compartilhada. Ajuste se o nome real for outro. Deve ser o MESMO banco
  # físico usado pelo FiapDonateCampaign (schema de Identity compartilhado).
  ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;Database=FiapDonateDb;User Id=sa;Password=CHANGE_ME;TrustServerCertificate=True;"
```

- [ ] **Step 3: Deployment**

```yaml
# k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: fiapdonateusers
spec:
  replicas: 1
  selector:
    matchLabels:
      app: fiapdonateusers
  template:
    metadata:
      labels:
        app: fiapdonateusers
    spec:
      containers:
        - name: users
          image: fiapdonateusers:local
          imagePullPolicy: IfNotPresent
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: fiapdonateusers-config
            - secretRef:
                name: fiapdonateusers-secret
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 15
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
```

- [ ] **Step 4: Service**

```yaml
# k8s/service.yaml
apiVersion: v1
kind: Service
metadata:
  name: fiapdonateusers
spec:
  selector:
    app: fiapdonateusers
  ports:
    - port: 8080
      targetPort: 8080
  type: ClusterIP
```

- [ ] **Step 5: Validate the manifests parse**

Run: `kubectl apply --dry-run=client -f k8s/configmap.yaml -f k8s/deployment.yaml -f k8s/service.yaml`

(Skip `secret.example.yaml` in this dry run — copy it to `k8s/secret.yaml` with real values first if you want to validate it too; `secret.yaml` must never be committed.)

Expected: `configmap/fiapdonateusers-config created (dry run)`, `deployment.apps/fiapdonateusers created (dry run)`, `service/fiapdonateusers created (dry run)` — no YAML errors.

- [ ] **Step 6: Commit**

```bash
git add k8s
git commit -m "chore: adiciona manifests Kubernetes (configmap, secret.example, deployment, service)"
```

---

## Task 9: CI pipeline

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:** none.

- [ ] **Step 1: Create the workflow**

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Restore
        run: dotnet restore FiapDonateUsers.slnx

      - name: Build
        run: dotnet build FiapDonateUsers.slnx --no-restore --configuration Release

      - name: Test
        run: dotnet test FiapDonateUsers.slnx --no-build --configuration Release

      - name: Build Docker image
        run: docker build -t fiapdonateusers:${{ github.sha }} -f Dockerfile .
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: adiciona pipeline de build, test e imagem Docker"
```

---

## Task 10: README

**Files:**
- Modify: `README.md`

**Interfaces:** none.

- [ ] **Step 1: Replace the README with accurate, runnable instructions**

```markdown
# FiapDonateUsers

API .NET 8 responsável pelo cadastro público de Doadores e pelo schema
compartilhado de ASP.NET Identity da plataforma FiapDonate (Hackathon FIAP).

> Este serviço é o único dono das migrations de Identity (`AspNetUsers`,
> `AspNetRoles`, etc.). O `FiapDonateCampaign` só **lê** esse schema (mesmo
> banco físico) para emitir o JWT no login — nunca gera migration a partir
> dele. Veja o comentário em `IdentityStoreDbContext.cs` no repositório do
> Campaign.

## Tecnologias

- .NET 8 e ASP.NET Core
- Entity Framework Core 8 com SQL Server
- ASP.NET Core Identity
- FluentValidation
- xUnit

## Requisitos

- .NET SDK 8
- Docker Desktop (para o SQL Server local e para build da imagem)
- `kubectl` (opcional, para deploy no cluster)

## Configuração

| Chave | Descrição |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Connection string do SQL Server. Deve apontar para o **mesmo banco físico** usado pelo `FiapDonateCampaign` (schema de Identity compartilhado). |
| `Admin__Email` / `Admin__Password` | Credenciais do usuário `GestorONG` padrão, criado automaticamente no startup se ainda não existir nenhum GestorONG. Default local: `gestor@fiapdonate.com` / `Gestor@123`. |

## Execução local

1. Suba o SQL Server:

   ```bash
   docker compose up -d
   ```

2. Rode a API (aplica as migrations automaticamente, inclusive na primeira
   execução):

   ```bash
   dotnet run --project FiapDonateUsers.API
   ```

3. Confirme que o serviço está saudável:

   ```bash
   curl http://localhost:5000/health/ready
   ```

Swagger disponível em `http://localhost:5000/swagger` em ambiente
`Development`.

## API HTTP

| Método | Rota | Acesso |
| --- | --- | --- |
| `POST` | `/api/users/register` | Público. Sempre cria o usuário com role `Doador` — não é possível escolher outra role pela API. |

### Cadastro de Doador

```http
POST /api/users/register
Content-Type: application/json

{
  "nome": "Maria Silva",
  "email": "maria@example.com",
  "cpf": "111.444.777-35",
  "senha": "SenhaForte1"
}
```

Regras: `email` deve ser único (garantido pela unicidade de `UserName`,
que é sempre igual ao email); `cpf` precisa ter dígito verificador válido e
ser único no banco; `senha` tem no mínimo 6 caracteres.

O login (`POST /api/auth/login`) e a emissão do JWT ficam no
`FiapDonateCampaign`, que consulta este mesmo banco.

### Conta GestorONG padrão

Criada automaticamente no primeiro startup, se ainda não existir nenhum
usuário com role `GestorONG`. Use as credenciais de `Admin:Email`/
`Admin:Password` (default local: `gestor@fiapdonate.com` / `Gestor@123`)
para logar no `FiapDonateCampaign` e testar a criação de campanhas.

## Testes

```bash
dotnet test FiapDonateUsers.slnx
```

## Kubernetes

Os manifestos ficam em [k8s](k8s):

```bash
docker build -t fiapdonateusers:local .
cp k8s/secret.example.yaml k8s/secret.yaml   # ajuste credenciais reais
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml -f k8s/deployment.yaml -f k8s/service.yaml
kubectl get pods
```

## Limitações conhecidas

- Este repositório mantém seu próprio `docker-compose.yml` com um SQL
  Server isolado para desenvolvimento solo. Para rodar lado a lado com o
  `FiapDonateCampaign` (necessário para testar login/criação de campanha
  ponta a ponta), aponte a connection string de um dos dois serviços para o
  SQL Server que o outro já subiu, em vez de rodar os dois `docker compose`
  simultaneamente (ambos usam a porta `1433` por padrão).
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: atualiza README com instruções reais de cadastro e execução"
```

---

## Task 11: FiapDonateCampaign — fix `[AllowAnonymous]` no painel de transparência

**Repository:** `FiapDonateCampaign` (branch `develop_FiapDonateCampaign_Oberdan`), not `FiapDonateUsers`.

**Files:**
- Modify: `FiapDonateCampaign.API/Controllers/CampaignController.cs:32-37`

**Interfaces:** none — no signature changes.

- [ ] **Step 1: Add `[AllowAnonymous]` to `ListarAtivas`**

```csharp
// FiapDonateCampaign.API/Controllers/CampaignController.cs (full file)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FiapDonateCampaign.Application.DTOs;
using FiapDonateCampaign.Application.Interface;

namespace FiapDonateCampaign.API.Controllers;

[ApiController]
[Route("api/campaign")]
[Authorize] // exige token válido em todos os endpoints deste controller, por padrão
public class CampaignController : ControllerBase
{
    private readonly ICampaignService _service;
    public CampaignController(ICampaignService service) => _service = service;

    [HttpPost]
    [Authorize(Roles = "GestorONG")] // sobrescreve: só GestorOng pode criar
    public async Task<IActionResult> Criar(CampaignRequestDto dto)
    {
        var resultado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "GestorONG")] // só GestorOng pode editar
    public async Task<IActionResult> Editar(Guid id, CampaignRequestDto dto)
    {
        var resultado = await _service.AtualizarAsync(id, dto);
        return Ok(resultado);
    }

    [HttpGet("ativas")]
    [AllowAnonymous] // Painel de Transparência: acesso público, exigido pelo enunciado
    public async Task<IActionResult> ListarAtivas()
    {
        var resultado = await _service.ListarAtivasAsync();
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id) // GestorONG e Doador podem consultar
    {
        var resultado = await _service.ObterPorIdAsync(id);
        return resultado is null ? NotFound() : Ok(resultado);
    }
}
```

- [ ] **Step 2: Verify the build**

Run: `dotnet build FiapDonateCampaign.slnx` (from the `FiapDonateCampaign` repo root)
Expected: `Compilação com êxito`, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add FiapDonateCampaign.API/Controllers/CampaignController.cs
git commit -m "fix: painel de transparência (GET /api/campaign/ativas) volta a ser público"
```

---

## Task 12: End-to-end verification

**Files:** none — this task only runs and observes the system built by Tasks 1–11.

- [ ] **Step 1: Start the shared SQL Server (from `FiapDonateUsers`)**

```bash
cd FiapDonateUsers
docker compose up -d
```

Expected: `sqlserver` container running (`docker compose ps` shows `Up`).

- [ ] **Step 2: Run the Users API (applies Identity migrations + seeds roles/admin)**

```bash
dotnet run --project FiapDonateUsers.API
```

Expected console output includes no unhandled exceptions; `curl http://localhost:5000/health/ready` (adjust port to whatever `dotnet run` printed) returns `Healthy`.

- [ ] **Step 3: Apply the Campaign's own migrations against the same database**

In a second terminal:

```bash
cd FiapDonateCampaign
dotnet ef database update --project FiapDonateCampaign.Infrastructure --startup-project FiapDonateCampaign.API
```

Expected: `Done.` — creates `Campaigns`/`Donation` tables in the same `FiapDonateDb` database the Users API just migrated Identity into.

- [ ] **Step 4: Run the Campaign API**

```bash
dotnet run --project FiapDonateCampaign.API
```

Expected: starts without exceptions on `http://localhost:5296`.

- [ ] **Step 5: Register a Doador**

```bash
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"nome":"Maria Silva","email":"maria@example.com","cpf":"111.444.777-35","senha":"SenhaForte1"}'
```

Expected: `200 OK`, `{"mensagem":"Usuário registrado com sucesso."}`.

- [ ] **Step 6: Log in as the new Doador via Campaign and confirm the role claim**

```bash
curl -X POST http://localhost:5296/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"maria@example.com","senha":"SenhaForte1"}'
```

Expected: `200 OK` with a `token` field. Decode the JWT (e.g. paste into jwt.io or `dotnet user-jwts` locally) and confirm the `role` claim is `Doador`.

- [ ] **Step 7: Log in as the seeded GestorONG and create a campaign**

```bash
curl -X POST http://localhost:5296/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"gestor@fiapdonate.com","senha":"Gestor@123"}'
```

Copy the returned token, then:

```bash
curl -X POST http://localhost:5296/api/campaign \
  -H "Authorization: Bearer <TOKEN_DO_GESTOR>" \
  -H "Content-Type: application/json" \
  -d '{"titulo":"Campanha de Teste","descricao":"Verificação E2E","dataInicio":"2026-10-01T00:00:00Z","dataFim":"2026-12-01T00:00:00Z","metaFinanceira":1000.00}'
```

Expected: `201 Created` with the campaign payload.

- [ ] **Step 8: Confirm the public transparency panel works without a token**

```bash
curl http://localhost:5296/api/campaign/ativas
```

Expected: `200 OK` (not `401 Unauthorized`), JSON array including the campaign created in Step 7.

- [ ] **Step 9: Run both automated test suites**

```bash
cd FiapDonateUsers && dotnet test FiapDonateUsers.slnx
cd ../FiapDonateCampaign && dotnet test FiapDonateCampaign.slnx
```

Expected: all tests pass in both repositories, 0 failures.

- [ ] **Step 10: Tear down**

```bash
cd FiapDonateUsers
docker compose down
```

If every step above matched its expected output, the plan is complete: Item 3 (Cadastro de Doador) is implemented and closed, the RBAC hole is fixed, and the public transparency panel is public again.
