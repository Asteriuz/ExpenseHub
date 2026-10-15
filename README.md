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

### Cadastro

`POST /register` é público e recebe `{ "email": "...", "password": "..." }`:

```shell
curl -s -X POST http://localhost:5245/register \
  -H "Content-Type: application/json" \
  -d '{"email":"funcionario@expensehub.local","password":"<senha-forte>"}'
```

Regras do cadastro (issue I03):

- o DTO **não possui campo de role**: o cadastro nunca promove ninguém. O usuário
  recém-criado nasce **sem roles** e quem atribui roles é o Admin (endpoint abaixo);
- entrada inválida (DTO inválido, senha fora da política ou **e-mail já cadastrado**)
  → `400` com `ProblemDetails`. A duplicidade de e-mail retorna `400` (e não `409`)
  porque o contrato reserva `409` para transições de estado de reembolso;
- sucesso → `201` com `{ "id": "...", "email": "..." }`; o identificador é gerado
  pelo servidor.

### Administração de usuários e roles (somente Admin)

`GET /api/admin/users` lista os usuários e suas roles. Requer a role `Admin`:
anônimo → `401`; autenticado sem `Admin` → `403`.

`PUT /api/admin/users/{id}/roles` substitui o conjunto completo de roles do usuário:

```shell
curl -s -X PUT http://localhost:5245/api/admin/users/<id>/roles \
  -H "Authorization: Bearer <token-admin>" \
  -H "Content-Type: application/json" \
  -d '{"roles":["Employee","Approver"]}'
```

- **whitelist**: apenas as 5 roles conhecidas são aceitas (`Admin`, `Employee`,
  `Approver`, `Finance`, `Auditor`); role arbitrária → `400`. A comparação ignora
  maiúsculas/minúsculas e duplicatas são ignoradas;
- usuário inexistente → `404`;
- o Admin **não pode remover a própria role `Admin`** → `403`. Decisão documentada:
  a entrada é válida (todas as roles existem), mas o ator autenticado não tem
  permissão para a operação sobre a própria conta — caso previsto pelo contrato
  `403 = autenticado sem permissão`;
- lista vazia é aceita para outros usuários e remove todas as roles do alvo;
  para o próprio Admin ela também é bloqueada com `403`.

### Reautenticação após alteração de roles

O token de acesso carrega as roles como claims `role` emitidas no momento do
`POST /login`. Após qualquer alteração de roles, é necessário **autenticar
novamente** para obter um token com as claims atualizadas: o token antigo continua
válido até expirar, porém reflete as roles anteriores.

### Rotas protegidas

As rotas são protegidas por padrão (política de fallback exige autenticação); apenas
`POST /login` e `GET /health` são anônimas. As respostas diferenciam:

- anônimo em rota protegida → `401` com `ProblemDetails` (`application/problem+json`);
- autenticado sem a role exigida → `403` com `ProblemDetails` (`application/problem+json`).

## Matriz de autorização (evidência)

A tabela abaixo consolida `docs/MATRIZ-AUTORIZACAO.md` com o **status HTTP que cada
combinação retorna**. As regras contextuais (ownership, estado e visibilidade) são
aplicadas na **camada de serviço** (`ExpenseService`), nunca apenas no atributo de
role da rota; o atributo `[Authorize(Roles = ...)]` fica na rota, conforme a regra de
autorização em duas camadas.

Cada célula descreve o resultado para um usuário que possui **apenas** aquela role.
`*` indica que a permissão existe somente se o usuário também possuir `Employee`
(roles acumulam permissões; a união dos escopos de leitura é aplicada no serviço).
`†` indica operação cuja rota é entregue nas issues I07/I08; o status listado é o
contrato já fixado pela matriz.

| Operação | Anônimo | Employee | Approver | Finance | Auditor | Admin |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Registrar usuário (`POST /register`) | 201 / 400 | 201 / 400 | 201 / 400 | 201 / 400 | 201 / 400 | 201 / 400 |
| Login (`POST /login`) | 200 / 401 | 200 / 401 | 200 / 401 | 200 / 401 | 200 / 401 | 200 / 401 |
| Listar usuários (`GET /api/admin/users`) | 401 | 403 | 403 | 403 | 403 | 200 |
| Alterar roles (`PUT /api/admin/users/{id}/roles`) | 401 | 403 | 403 | 403 | 403 | 200 / 400 / 403 / 404 |
| Criar reembolso (`POST /api/expenses`) | 401 | 201 / 400 | 403* | 403* | 403 | 403* |
| Editar rascunho (`PUT /api/expenses/{id}`) | 401 | 200 / 400 / 403 / 404 / 409 | 403* | 403* | 403 | 403* |
| Enviar rascunho (`POST /api/expenses/{id}/submit`) | 401 | 200 / 403 / 404 / 409 | 403* | 403* | 403 | 403* |
| Listar reembolsos (`GET /api/expenses`) | 401 | 200 (próprios) | 200 (Submitted) | 200 (Approved/Paid) | 200 (todos) | 403 |
| Consultar detalhe (`GET /api/expenses/{id}`) | 401 | 200 (próprio) / 404 | 200 (Submitted) / 404 | 200 (Approved/Paid) / 404 | 200 (todos) | 403 |
| Aprovar (`POST /api/expenses/{id}/approve`) † | 401 | 403 | 200 / 403 / 404 / 409 | 403 | 403 | 403 |
| Reprovar (`POST /api/expenses/{id}/reject`) † | 401 | 403 | 200 / 400 / 403 / 404 / 409 | 403 | 403 | 403 |
| Pagar (`POST /api/expenses/{id}/pay`) † | 401 | 403 | 403 | 200 / 403 / 404 / 409 | 403 | 403 |
| Consultar histórico (`GET /api/expenses/{id}/history`) † | 401 | 200 (próprio) / 404 | 200 (Submitted) / 404 | 200 (Approved/Paid) / 404 | 200 (todos) | 403 |

Legenda dos status compostos:

- `400`: entrada inválida (DTO com DataAnnotations, whitelist de roles, justificativa da reprovação);
- `401`: requisição anônima em rota protegida;
- `403`: autenticado sem a role da rota, sem a regra contextual (não proprietário) ou sem role funcional;
- `404`: reembolso inexistente **ou fora do escopo de leitura do ator** (a existência de recurso alheio nunca é revelada);
- `409`: transição incompatível ou repetida (editar/enviar fora de `Draft`, decidir fora de `Submitted`, pagar fora de `Approved`).

### Escopos de leitura e união de roles

`GET /api/expenses` e `GET /api/expenses/{id}` aplicam a visibilidade como filtro **no
IQueryable, antes de materializar** os dados:

| Role | Escopo de leitura |
|---|---|
| `Employee` | Somente os próprios reembolsos, em qualquer estado |
| `Approver` | Reembolsos em `Submitted` (de qualquer proprietário) |
| `Finance` | Reembolsos em `Approved` e `Paid` |
| `Auditor` | Todos os reembolsos, somente leitura |
| `Admin` | Nenhum: a role `Admin` não concede acesso funcional (`403`) |

Usuário com múltiplas roles recebe a **união** dos escopos (exemplo: `Employee` +
`Approver` vê os próprios rascunhos **e** os envios de terceiros). As proibições de
**autoaprovação** e **autopagamento** permanecem sempre: mesmo acumulando `Approver`
ou `Finance`, ninguém decide ou paga o próprio reembolso (`403` quando as rotas de
decisão e pagamento existirem, issues I07/I08).

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
