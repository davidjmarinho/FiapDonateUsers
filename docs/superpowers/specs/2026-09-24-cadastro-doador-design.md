# Cadastro de Doador (Item 3 do Hackathon) — Design

Data: 2026-09-24
Repositórios afetados: `FiapDonateUsers` (principal), `FiapDonateCampaign` (fix pontual)

## Contexto

O enunciado do hackathon exige (Item 3, acesso público): cadastro de Doador com
Nome Completo, Email (único), CPF (formato validado) e Senha (hash). Esse
endpoint não existe em nenhum repositório hoje.

O time já havia decidido a arquitetura correta: o `FiapDonateCampaign` teve sua
migration de Identity e `RoleSeeder` removidos deliberadamente (commit
`e32ef6e`, "Retirado a criacao de usuario e ajustado as regras"), deixando um
comentário explícito em `IdentityStoreDbContext.cs`:

> "Usado exclusivamente para o UserManager/SignInManager conseguirem consultar
> a tabela de usuários (criada e migrada pela FiapDonateUsers). NUNCA rode
> Add-Migration usando este DbContext — o schema de Identity pertence à
> FiapDonateUsers."

O repositório `FiapDonateUsers` existe desde então, mas ficou vazio (só commit
inicial) na branch `main`. Um teammate (Lucas) chegou a implementar boa parte
do serviço na branch `origin/Lucas-Development` (não mergeada): projeto em
camadas, `UsersController.POST /api/users/register`, `ApplicationUser` com
`Nome`, `RoleSeeder`, migration `InitialIdentity`.

Essa branch foi adotada como ponto de partida (branch local
`develop_FiapDonateUsers_Oberdan`, criada a partir de
`origin/Lucas-Development`). Ela tem gaps reais contra o enunciado, cobertos
abaixo.

Verificação de compatibilidade com o Campaign (já confirmada, sem mudança
necessária no Campaign para isso): mesma connection string local
(`(localdb)\mssqllocaldb;Database=FiapDonateDb`), mesmo shape de
`ApplicationUser.Nome`, mesmo `IdentityOptions.Password.RequireNonAlphanumeric
= false`, mesmos nomes de role (`GestorONG`/`Doador`), nenhum dos dois faz
seed de role duplicado.

## Gaps a fechar (sobre a base do Lucas)

### 1. CPF ausente

`ApplicationUser` só tem `Nome`. Precisa de `Cpf` (formato validado, conforme
o enunciado).

**Decisão:** validar formato completo (11 dígitos + algoritmo de dígito
verificador real, não só contagem de dígitos) e garantir unicidade no banco.
Justificativa: mais robusto e convincente para a correção; custo baixo.

- `ApplicationUser.Cpf` (string, armazenado normalizado — só dígitos).
- `CpfValidator` (Domain ou Application): método estático `IsValid(string cpf)`
  implementando o algoritmo de dígito verificador do CPF (rejeita sequências
  repetidas tipo `111.111.111-11`).
- Nova migration `AddCpfToAspNetUsers`: coluna `Cpf` (`nvarchar(11)`, not
  null) + índice único.
- `RegisterUserValidator`: regra `Cpf` usando `CpfValidator.IsValid`.
- `UsersController.Register`: antes de `CreateAsync`, checar se já existe
  usuário com o mesmo CPF; se existir, `BadRequest` com mensagem clara. (A
  unicidade também é garantida no banco pelo índice, como cinto e suspensório
  contra condição de corrida.)

### 2. Falha de RBAC: registro público permite virar GestorONG

`RegisterUserDto` atual tem um campo `Role` escolhido pelo próprio chamador
(`"GestorONG"` ou `"Doador"`), num endpoint sem autenticação. Isso permite
qualquer pessoa se auto-promover ao role que cria/edita campanhas — quebra o
RBAC exigido no Item 1 do enunciado.

**Fix:** remover `Role` do DTO. `RegisterUserDto` passa a ser
`(string Nome, string Email, string Cpf, string Senha)`. O controller fixa
`"Doador"` via `AddToRoleAsync(usuario, "Doador")`, sem input do cliente.

### 3. Como contas GestorONG são criadas

Já que o registro público só permite Doador, precisa existir uma forma de ter
pelo menos um GestorONG para testar os endpoints de gestão de campanha.

**Decisão:** seed automático no startup. `AdminSeeder.SeedAsync` roda junto
com o `RoleSeeder` existente: se não existir nenhum usuário com role
`GestorONG`, cria um com email/senha vindos de configuração
(`Admin:Email`/`Admin:Password`), com defaults em `appsettings.json` —
`gestor@fiapdonate.com` / `Gestor@123` — documentados no README para uso
local/demo. Idempotente — não recria se já existir.

### 4. Paridade de DevOps com Campaign/Worker

Hoje o `FiapDonateUsers` não tem Dockerfile, k8s, CI ou health checks. Os
outros dois serviços do time têm todos. Sem isso, o Users não sobe no cluster
para a demo.

- **Dockerfile**: multi-stage igual ao do Worker (`sdk:8.0` build →
  `aspnet:8.0` runtime, `EXPOSE 8080`).
- **`/health`**: `AspNetCore.Diagnostics.HealthChecks` + `.AddSqlServer(...)`
  (sem RabbitMQ — Users não consome/publica fila). Endpoints `/health/live`
  (sem checar dependências) e `/health/ready` (checa SQL Server), seguindo o
  mesmo padrão do Worker.
- **k8s** (`k8s/`): `configmap.yaml`, `secret.example.yaml`,
  `deployment.yaml` (probes em `/health/live` e `/health/ready`, 1 réplica),
  `service.yaml` — mesmo padrão dos outros dois repositórios.
- **CI** (`.github/workflows/ci.yml`): restore → build → test → `docker
  build`, replicando o pipeline do Worker.
- **docker-compose.yml**: sobe SQL Server local para dev isolado do Users,
  mesmo padrão dos outros repos (mesmo nome de banco `FiapDonateDb` para
  bater com o Campaign em execução local lado a lado).

### 5. Testes

- `CpfValidator`: casos válidos, formato errado, dígito verificador errado,
  sequência repetida.
- `RegisterUserValidator`: Nome/Email/Cpf/Senha inválidos e válidos.

## Fix relacionado no FiapDonateCampaign

`GET /api/campaign/ativas` (Painel de Transparência, Item 4 do enunciado —
acesso público) está atrás de `[Authorize]` de classe, sem
`[AllowAnonymous]` no método. Isso contradiz o requisito de acesso público.

**Fix:** adicionar `[AllowAnonymous]` no método `ListarAtivas` de
`CampaignController`. Mudança de uma linha, sem impacto em mais nada (os
demais endpoints do controller continuam exigindo autenticação/roles como
estão).

## Fora de escopo (não bloqueia esta entrega)

- Orquestração raiz com um único SQL Server compartilhado entre
  Campaign+Worker+Users em compose/k8s (cada repo mantém seu próprio compose
  de dev por enquanto; documentar no README do Users que, em execução
  integrada com o Campaign, a connection string deve apontar para o mesmo
  banco físico).
- Endpoint protegido para criar novos GestorONG além do seed inicial.
- CPF: não há verificação de veracidade (API externa da Receita, etc.), só
  formato + dígito verificador.

## Riscos / pontos de atenção

- A migration `InitialIdentity` do Lucas já cria `AspNetUsers` com a coluna
  `Nome` `NOT NULL`; a nova migration de CPF precisa ser aditiva (não
  recriar a tabela).
- `AdminSeeder` deve rodar depois do `RoleSeeder` (precisa que a role
  `GestorONG` já exista antes de atribuí-la).
- Senha padrão do seed de GestorONG documentada em texto claro no README é
  aceitável apenas para ambiente de demo/correção do hackathon — não é uma
  prática de produção.
