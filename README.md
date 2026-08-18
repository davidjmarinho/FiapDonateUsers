# FiapDonateUsers - Serviço de Gerenciamento de Usuários

## 📋 Sobre o Projeto

**FiapDonateUsers** é uma API REST desenvolvida em **.NET 8** que gerencia o cadastro e autenticação de usuários para plataforma de doações **FIAP Donate**. O serviço é responsável por:

- ✅ Registro e autenticação de usuários
- ✅ Gerenciamento de roles e permissões (Gestor ONG, Doador)
- ✅ Validação de dados de entrada
- ✅ Persistência de dados em SQL Server

A arquitetura segue princípios de **Domain-Driven Design (DDD)** com separação clara de responsabilidades entre camadas.

---

## 🏗️ Arquitetura

O projeto está estruturado em **4 camadas principais**:

### 1. **FiapDonateUsers.API** 🌐
- **Responsabilidade**: Apresentação e exposição de endpoints HTTP
- **Componentes principais**:
  - `Controllers/UsersController.cs` - Endpoints REST para gerenciamento de usuários
  - `Program.cs` - Configuração da aplicação, DI e middlewares
  - `appsettings.json` - Configurações de ambiente
  - `FiapDonateUsers.API.http` - Testes de requisições HTTP

### 2. **FiapDonateUsers.Application** 📱
- **Responsabilidade**: Regras de negócio e lógica de aplicação
- **Componentes principais**:
  - `DTOs/` - Data Transfer Objects para requisições/respostas
  - `Validators/` - Validações de dados usando FluentValidation

### 3. **FiapDonateUsers.Domain** 📦
- **Responsabilidade**: Conceitos principais do domínio
- Status: Estrutura preparada para futuras entidades de domínio

### 4. **FiapDonateUsers.Infrastructure** 🔌
- **Responsabilidade**: Acesso a dados, identidade e recursos externos
- **Componentes principais**:
  - `Data/AppDbContext.cs` - Contexto do Entity Framework Core
  - `Identity/` - Implementações de autenticação (ApplicationUser, RoleSeeder)
  - `Migrations/` - Histórico de mudanças no banco de dados
  - `DependencyInjection.cs` - Registro de serviços

### Diagrama de Fluxo de Requisição

```
HTTP Request
	 ↓
[UsersController] (API)
	 ↓
[RegisterUserValidator] (Application)
	 ↓
[UserManager] (Infrastructure.Identity)
	 ↓
[AppDbContext] (Infrastructure.Data)
	 ↓
[SQL Server Database]
```

---

## 🛠️ Stack Tecnológico

| Componente | Versão | Descrição |
|-----------|--------|-----------|
| .NET | 8.0 LTS | Framework principal |
| Entity Framework Core | Última | ORM para acesso a dados |
| ASP.NET Core Identity | Última | Gerenciamento de autenticação |
| FluentValidation | Última | Validação de dados |
| SQL Server | 2019+ | Banco de dados |
| Swagger/OpenAPI | Última | Documentação de API |

---

## 📋 Regras de Negócio

### 1. **Registro de Usuários**
- **Campo obrigatório**: Nome, Email, Senha, Role
- **Valores permitidos para Role**: `GestorONG` ou `Doador`
- **Email**: Deve ser único no sistema
- **Senha**: Mínimo de 6 caracteres (sem caracteres especiais obrigatórios)

### 2. **Validações de Entrada**
Todas as requisições POST para registro devem incluir:
```json
{
  "nome": "string (obrigatório, mínimo 3 caracteres)",
  "email": "string (obrigatório, formato válido)",
  "senha": "string (obrigatório, mínimo 6 caracteres)",
  "role": "GestorONG | Doador (obrigatório)"
}
```

### 3. **Roles e Permissões**
- **Doador**: Usuário que realiza doações
- **GestorONG**: Usuário que gerencia uma ONG

### 4. **Resposta de Sucesso**
```json
{
  "mensagem": "Usuário registrado com sucesso."
}
```

### 5. **Tratamento de Erros**
- Email duplicado → HTTP 400 (Bad Request)
- Validação falha → HTTP 400 (Bad Request) + detalhes dos erros
- Servidor indisponível → HTTP 500 (Internal Server Error)

---

## 🚀 Como Iniciar

### Pré-requisitos
- .NET 8 SDK instalado
- SQL Server 2019 ou superior
- Visual Studio 2022+ / Visual Studio Code

### Passos

1. **Clone o repositório**
   ```bash
   git clone <repository-url>
   cd FiapDonateUsers
   ```

2. **Restaure as dependências**
   ```bash
   dotnet restore
   ```

3. **Configure a conexão com banco de dados**
   - Edite `appsettings.json` em `FiapDonateUsers.API/`:
   ```json
   {
	 "ConnectionStrings": {
	   "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=FiapDonateDb;Trusted_Connection=True;..."
	 }
   }
   ```

4. **Execute as migrações do banco de dados**
   ```bash
   dotnet ef database update --project FiapDonateUsers.Infrastructure --startup-project FiapDonateUsers.API
   ```

5. **Inicie a aplicação**
   ```bash
   dotnet run --project FiapDonateUsers.API
   ```

6. **Acesse a API**
   - Swagger UI: `http://localhost:5000/swagger/index.html`
   - Base URL: `http://localhost:5000/api/`

---

## 📡 Endpoints

### Registro de Usuário

```http
POST /api/users/register
Content-Type: application/json

{
  "nome": "João Silva",
  "email": "joao@example.com",
  "senha": "senha123",
  "role": "Doador"
}
```

**Respostas:**
- ✅ **200 OK**: Usuário criado com sucesso
- ❌ **400 Bad Request**: Validação falhou ou email duplicado

---

## 🗄️ Banco de Dados

### Schema Principal

**Tabelas criadas pela ASP.NET Identity:**
- `AspNetUsers` - Usuários do sistema
- `AspNetRoles` - Roles disponíveis
- `AspNetUserRoles` - Atribuição de roles aos usuários

### Extensões Customizadas
- **ApplicationUser**: Estende IdentityUser com campo `Nome`

### Roles Padrões Seedados
- `GestorONG`
- `Doador`

---

## 🧪 Testes

### Arquivo de Testes HTTP
- Localização: `FiapDonateUsers.API/FiapDonateUsers.API.http`
- Formato: REST Client (VS Code) ou Postman

### Exemplo de Teste
```http
### Registrar novo usuário
POST http://localhost:5000/api/users/register
Content-Type: application/json

{
  "nome": "Maria Santos",
  "email": "maria@example.com",
  "senha": "pass123",
  "role": "GestorONG"
}
```

---

## 📁 Estrutura de Pastas

```
FiapDonateUsers/
├── FiapDonateUsers.API/
│   ├── Controllers/
│   ├── Properties/
│   ├── Program.cs
│   └── appsettings*.json
├── FiapDonateUsers.Application/
│   ├── DTOs/
│   └── Validators/
├── FiapDonateUsers.Domain/
│   └── [Entidades de domínio]
├── FiapDonateUsers.Infrastructure/
│   ├── Data/
│   ├── Identity/
│   └── Migrations/
└── README.md
```

---

## 🔐 Segurança

### Implementado
- ✅ Autenticação via ASP.NET Core Identity
- ✅ Senhas hasheadas (PBKDF2 com salt)
- ✅ Validação de entrada com FluentValidation
- ✅ Isolamento de dados por role

### Recomendações para Produção
- 🔒 Implementar JWT para autorização stateless
- 🔒 Adicionar rate limiting
- 🔒 HTTPS obrigatório
- 🔒 Configurar CORS apropriadamente
- 🔒 Implementar logging e auditoria
- 🔒 Validar email com token de confirmação

---

## 📝 Configuração de Logging

O projeto utiliza logging nativo do .NET. Configurações em `appsettings.json`:

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Microsoft.AspNetCore": "Warning"
	}
  }
}
```

---

## 🤝 Contribuindo

1. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
2. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
3. Push para a branch (`git push origin feature/AmazingFeature`)
4. Abra um Pull Request

---

## 📄 Licença

Projeto desenvolvido para FIAP.

---

## 👥 Suporte

Para dúvidas ou problemas, abra uma issue no repositório ou entre em contato com o time de desenvolvimento.

---

## 🗺️ Roadmap Futuro

- [ ] Implementar autenticação JWT
- [ ] Adicionar endpoints de login/logout
- [ ] Gerenciamento de perfil de usuário
- [ ] Recuperação de senha por email
- [ ] Testes unitários
- [ ] Testes de integração
- [ ] Documentação de API com Swagger aprimorado
- [ ] Containerização com Docker

---

**Última atualização**: 2025
**Versão da API**: v1
