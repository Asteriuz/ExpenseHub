# Checkpoint 2 — ExpenseHub

Checkpoint de C# em grupos de até 3 pessoas para construção de uma Application Programming Interface (API) corporativa de reembolsos.

O prazo de entrega é **13 de outubro de 2026**. O grupo deverá implementar autenticação, autorização, fluxo de aprovação e reprovação, pagamento simulado, histórico e testes unitários.

## Criar seu repositório

1. Clique em **Use this template**.
2. Selecione **Create a new repository**.
3. Crie um repositório **público** em uma das contas do grupo.
4. Adicione os demais integrantes como colaboradores.
5. Clone o repositório.

Não use fork. As issues permanecem neste repositório original como especificação comum da turma.

Os commits serão utilizados para avaliar a participação. Todos os membros do grupo
devem possuir mais de um commit no repositório.

## Fluxo de trabalho

Para cada issue:

1. leia os critérios no repositório original;
2. crie uma branch com o identificador, por exemplo `i06-ownership`;
3. implemente e valide a feature;
4. abra uma pull request no seu próprio repositório;
5. use um título como `I06 — Ownership e matriz de acesso`;
6. adicione na descrição uma referência completa, como `Racass/checkpoint-csharpracass-expensehub#6`;
7. não use `Closes`, `Fixes` ou `Resolves`, pois a issue original deve permanecer aberta;
8. conclua a auto-revisão e faça o merge.

## Estrutura inicial

```text
sources/
├── ExpenseHub.slnx
├── ExpenseHub.Api/
└── ExpenseHub.UnitTests/
```

A solução começa sem Identity, banco, domínio ou testes funcionais. Toda implementação avaliada deve ser criada por você.

## Comandos

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

O endpoint inicial `GET /health` existe apenas para confirmar que a aplicação inicia.

## Documentação

- [Enunciado](docs/ENUNCIADO.md)
- [Requisitos e contratos](docs/REQUISITOS.md)
- [Rubrica](docs/RUBRICA.md)
- [Matriz de autorização](docs/MATRIZ-AUTORIZACAO.md)
- [Processo no GitHub](docs/PROCESSO-GITHUB.md)
- [Uso de Inteligência Artificial](docs/USO-DE-IA.md)
- [Regras do pipeline de qualidade](docs/code-quality-rules.md)

## Banco de dados

A aplicação usa **SQLite** como provider relacional do Entity Framework Core. A escolha
prioriza simplicidade de execução: os dados ficam em um arquivo local e o build/teste
**não depende de nenhum servidor de banco rodando**.

### Provider e pacotes

| Pacote | Versão | Finalidade |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.12 | Provider relacional SQLite |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.12 | Suporte a `dotnet ef` (migrations) |

A versão da ferramenta `dotnet-ef` está fixada no manifest local `dotnet-tools.json`.

### Configuração

A connection string fica em `sources/ExpenseHub.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "ExpenseHub": "Data Source=expensehub.db"
}
```

O arquivo `expensehub.db` é criado no diretório de execução da API e está ignorado
pelo Git (banco local nunca é versionado). A string não contém segredos. O contexto
é `ExpenseHub.Data.ExpenseHubDbContext`, registrado em `Program.cs` com `UseSqlite`.

### Criar ou atualizar o banco

```shell
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project sources/ExpenseHub.Api --startup-project sources/ExpenseHub.Api
dotnet ef database update --project sources/ExpenseHub.Api --startup-project sources/ExpenseHub.Api
```

A migration inicial `InitialCreate` já está versionada em
`sources/ExpenseHub.Api/Migrations/`; basta executar `dotnet ef database update`.
A migration `AddIdentity` adiciona as tabelas do ASP.NET Core Identity.

### Como iniciar a aplicação

```shell
dotnet run --project sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Ao iniciar, a API aplica as migrations pendentes e executa o seed de identidade
(idempotente). Em ambiente de desenvolvimento, o OpenAPI fica em `/openapi/v1.json`.
O endpoint `GET /health` confirma que a aplicação subiu.

## Autenticação e identidade

A API usa **ASP.NET Core Identity** persistido no SQLite (mesmo `ExpenseHubDbContext`,
que herda de `IdentityDbContext<IdentityUser>`) e autenticação **bearer JWT**
(`Microsoft.AspNetCore.Authentication.JwtBearer`).

### Roles e seed

O seed cria de forma idempotente as roles `Admin`, `Employee`, `Approver`, `Finance`
e `Auditor` e **uma única** conta Admin vinculada à role `Admin`. Pode ser executado
quantas vezes for necessário sem duplicar registros. Nenhum outro usuário é criado
pelo seed; os demais devem ser cadastrados por HTTP (issue I03).

### Configuração segura (sem segredos no repositório)

| Chave | Origem | Observação |
|---|---|---|
| `Jwt:Issuer` | `appsettings.json` | Não é segredo. |
| `Jwt:Audience` | `appsettings.json` | Não é segredo. |
| `Jwt:LifetimeMinutes` | `appsettings.json` | Tempo de vida do token. |
| `Jwt:SigningKey` | user-secrets / variável de ambiente | Mínimo de 32 bytes. **Nunca versionar.** |
| `Seed:AdminEmail` | `appsettings.json` | E-mail da conta Admin (padrão `admin@expensehub.local`). |
| `Seed:AdminPassword` | user-secrets / variável de ambiente | **Nunca versionar.** |

```shell
cd sources/ExpenseHub.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha-forte>"
dotnet user-secrets set "Jwt:SigningKey" "<chave-com-32-bytes-ou-mais>"
```

Alternativamente, use variáveis de ambiente (`Jwt__SigningKey`, `Seed__AdminPassword`).
Sem esses valores a aplicação falha ao iniciar, com mensagem explícita.

### Login

`POST /login` é público e recebe `{ "email": "...", "password": "..." }`. Credenciais
válidas devolvem `200` com `{ "accessToken": "...", "tokenType": "Bearer", "expiresAtUtc": "..." }`;
credenciais inválidas devolvem `401` com `ProblemDetails`. O token carrega o `sub`
(identificador do usuário) e uma claim `role` por role atribuída.

```shell
curl -s -X POST http://localhost:5245/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@expensehub.local","password":"<senha>"}'
```

### Rotas protegidas

As rotas são protegidas por padrão (política de fallback exige autenticação); apenas
`POST /login` e `GET /health` são anônimas. As respostas diferenciam:

- anônimo em rota protegida → `401` com `ProblemDetails` (`application/problem+json`);
- autenticado sem a role exigida → `403` com `ProblemDetails` (`application/problem+json`).

Após uma alteração de roles, é necessário autenticar novamente para obter um token
com as novas claims.

## Testes

Somente testes unitários escritos por você entram na nota. Testes de integração, end-to-end ou de interface são permitidos, mas opcionais e sem pontuação.

Os testes unitários devem executar sem banco, rede ou serviço externo.

## Entrega

Entregue:

- URL do repositório público;
- commit Secure Hash Algorithm (SHA) final;
- integração contínua executada;
- documentação atualizada.

O projeto deve compilar sem erros e ser entregue sem warnings para receber a pontuação integral de Qualidade de Código.
