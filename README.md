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
