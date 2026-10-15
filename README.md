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

## Reembolsos — rascunho (issue I04)

### Criar rascunho

`POST /api/expenses` exige a role `Employee` e recebe:

```json
{
  "description": "Passagem de táxi a serviço",
  "amount": 89.90,
  "expenseDate": "2026-10-10",
  "expenseCategoryId": "6f1c2b3a-0000-0000-0000-000000000000"
}
```

- o DTO contém apenas `description`, `amount`, `expenseDate` e `expenseCategoryId`
  (opcional); o proprietário (`ownerId`), o estado, o ator e os timestamps são sempre
  derivados do token JWT e do relógio do servidor (UTC) — nunca do cliente;
- validações: descrição obrigatória de 10 a 500 caracteres; valor `decimal` entre
  R$ 0,01 e R$ 2.147.483.647,00 (`Int32.MaxValue`); data da despesa preenchida e não
  futura, avaliada no relógio do servidor em UTC; `expenseCategoryId`, quando
  informada, deve existir;
- sucesso → `201` com o reembolso criado em estado `Draft`;
- entrada inválida ou categoria inexistente → `400` com `ProblemDetails`;
- anônimo → `401`; autenticado sem a role `Employee` → `403`.

### Editar rascunho próprio

`PUT /api/expenses/{id}` exige a role `Employee`, segue as mesmas regras de campo e
somente edita reembolso próprio em estado `Draft`. Decisões de contrato:

- reembolso inexistente (ou identificador malformado) → `404`;
- reembolso existente **não próprio** → `403`. Decisão documentada: o recurso existe,
  porém o ator autenticado não tem permissão sobre ele — caso previsto pelo contrato
  `403 = autenticado sem permissão`. A escolha mantém consistência com as proibições
  de `403` para não proprietário nas demais operações de escrita (enviar, aprovar,
  pagar); a regra do `404` fica reservada ao escopo de **leitura**;
- estado diferente de `Draft` → `409` (transição incompatível); a edição nunca altera
  o estado do reembolso;
- entrada inválida → `400`;
- sucesso → `200` com o rascunho atualizado.

A criação e a edição gravam o registro de histórico (`Criar`/`Editar`, com ator,
instante UTC e resumo das alterações antes→depois) na **mesma operação lógica** de
persistência que o próprio reembolso (`SaveChanges` único). Edição sem alterações
efetivas responde `200` e não grava histórico.

## Reembolsos — decisão (issue I07)

### Serviço de histórico

O registro de histórico é centralizado no serviço `ExpenseHistoryService`: toda
transição (criar, editar, enviar, aprovar, reprovar) é gravada **na mesma operação
lógica** da alteração de estado — um único `SaveChanges` persiste o reembolso e a
linha de histórico, de modo que uma falha de persistência nunca deixa estado e
histórico divergentes. Cada entrada registra ação, reembolso, ator (derivado do
token), instante UTC (derivado do relógio do servidor), estado anterior, estado
posterior e, na reprovação, a justificativa.

### Aprovar reembolso

`POST /api/expenses/{id}/approve` exige a role `Approver` e transiciona
`Submitted → Approved`. O ator e o instante UTC são derivados do token e do
relógio do servidor. Contrato:

- somente `Approver` **não proprietário** decide: o proprietário que acumula a
  role `Approver` recebe `403` (regra contextual validada na camada de serviço);
- `Employee`/`Finance`/`Auditor`/`Admin` sem a role `Approver` → `403` na rota;
- estado diferente de `Submitted` (incluindo `Draft`, `Approved`, `Rejected` e
  `Paid`) ou decisão repetida → `409`, sem nenhuma linha de histórico duplicada;
- reembolso inexistente → `404`; anônimo → `401`.

### Reprovar reembolso

`POST /api/expenses/{id}/reject` exige a role `Approver`, transiciona
`Submitted → Rejected` e recebe `{ "justification": "..." }`:

- a justificativa é **obrigatória**, com 10 a 500 caracteres; vazia, com espaços
  apenas, curta ou longa → `400` com `ProblemDetails` (validação declarativa no
  DTO e revalidação na camada de serviço);
- as mesmas regras de autorização e estado da aprovação se aplicam: proprietário
  com a role `Approver` → `403`; estado não `Submitted` ou decisão repetida →
  `409` sem histórico duplicado; inexistente → `404`; anônimo → `401`.

### Estados finais

`Rejected` e `Paid` são estados finais: não há reabertura, cancelamento, reenvio
ou nova decisão. Tentativas sobre esses estados retornam `409` e não alteram o
histórico.

## Reembolsos — pagamento e histórico (issue I08)

### Pagar reembolso

`POST /api/expenses/{id}/pay` exige a role `Finance` e transiciona
`Approved → Paid`. O ator e o instante UTC são derivados do token e do relógio
do servidor, e um `PaymentRecord` vinculado ao reembolso é persistido **na mesma
operação lógica** da transição (mesmo `SaveChanges` que grava estado, pagamento e
histórico). Contrato:

- somente `Finance` **não proprietário** paga: o proprietário que acumula a role
  `Finance` recebe `403` (regra contextual validada na camada de serviço);
- `Employee`/`Approver`/`Auditor`/`Admin` sem a role `Finance` → `403` na rota;
- estado diferente de `Approved` (incluindo `Draft`, `Submitted` e `Rejected`) ou
  pagamento repetido → `409`, sem nenhum registro de pagamento ou linha de
  histórico duplicados;
- reembolso inexistente → `404`; anônimo → `401`.

### Consultar histórico

`GET /api/expenses/{id}/history` devolve os registros auditáveis do reembolso
(criar, editar com resumo antes→depois, enviar, aprovar, reprovar com
justificativa e pagar), do mais antigo ao mais recente. A visibilidade é a mesma
da consulta de detalhe, aplicada como filtro no `IQueryable`:

- `Employee` → apenas o próprio reembolso;
- `Approver` → reembolsos em estado `Submitted`;
- `Finance` → reembolsos em estado `Approved` ou `Paid`;
- `Auditor` → todos os reembolsos (somente leitura);
- `Admin` → `403` (a role `Admin` não concede acesso funcional);
- recurso inexistente ou fora do escopo de leitura → `404`, sem revelar
  existência; anônimo → `401`.

## Matriz de autorização — status HTTP (evidência)

Tabela preenchida com o status HTTP que cada combinação de ator × operação retorna
nesta implementação (caminho feliz; as exceções por operação estão listadas após a
tabela). Requisições anônimas em rota protegida retornam `401` e usuários autenticados
sem a role exigida retornam `403`, sempre com `ProblemDetails` (`application/problem+json`).

| Operação | Rota | Anônimo | Employee | Approver | Finance | Auditor | Admin |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Registrar usuário | `POST /register` | 201 | 201 | 201 | 201 | 201 | 201 |
| Login | `POST /login` | 200 | 200 | 200 | 200 | 200 | 200 |
| Health check | `GET /health` | 200 | 200 | 200 | 200 | 200 | 200 |
| Listar usuários | `GET /api/admin/users` | 401 | 403 | 403 | 403 | 403 | 200 |
| Alterar roles | `PUT /api/admin/users/{id}/roles` | 401 | 403 | 403 | 403 | 403 | 200 |
| Criar rascunho | `POST /api/expenses` | 401 | 201 | 403 | 403 | 403 | 403 |
| Editar rascunho | `PUT /api/expenses/{id}` | 401 | 200 | 403 | 403 | 403 | 403 |
| Enviar rascunho | `POST /api/expenses/{id}/submit` | 401 | 200 | 403 | 403 | 403 | 403 |
| Listar reembolsos | `GET /api/expenses` | 401 | 200 | 200 | 200 | 200 | 403 |
| Consultar detalhe | `GET /api/expenses/{id}` | 401 | 200 | 200 | 200 | 200 | 403 |
| Aprovar | `POST /api/expenses/{id}/approve` | 401 | 403 | 200 | 403 | 403 | 403 |
| Reprovar | `POST /api/expenses/{id}/reject` | 401 | 403 | 200 | 403 | 403 | 403 |
| Pagar | `POST /api/expenses/{id}/pay` | 401 | 403 | 403 | 200 | 403 | 403 |
| Consultar histórico | `GET /api/expenses/{id}/history` | 401 | 200 | 200 | 200 | 200 | 403 |

Observações por operação:

- **Registrar usuário** — `400` quando o DTO é inválido, a senha fere a política ou o
  e-mail já está cadastrado. O DTO nunca contém campo de role.
- **Login** — `401` quando as credenciais são inválidas.
- **Alterar roles** — `400` quando alguma role está fora da whitelist das 5 roles
  conhecidas; `404` quando o usuário alvo não existe; `403` quando o Admin tenta
  remover a própria role `Admin`.
- **Criar/editar rascunho** — `400` entrada inválida (descrição, valor ou data) ou
  categoria inexistente. Na edição: `403` reembolso de outro Employee, `409` estado
  diferente de `Draft`, `404` reembolso inexistente.
- **Enviar rascunho** — `403` reembolso de outro Employee; `409` estado diferente de
  `Draft` ou envio repetido; `404` reembolso inexistente.
- **Listar / consultar detalhe / consultar histórico** — o resultado `200` respeita o
  escopo de leitura por perfil: `Employee` → próprios; `Approver` → `Submitted`;
  `Finance` → `Approved`/`Paid`; `Auditor` → todos. Fora desse escopo (ou reembolso
  inexistente) a leitura retorna `404`, sem revelar existência de recurso alheio.
  Usuário com múltiplas roles recebe a união dos escopos.
- **Aprovar/reprovar** — `403` quando o Approver é o proprietário do reembolso;
  `409` quando o estado não é `Submitted` ou a decisão é repetida; `404` inexistente;
  na reprovação, `400` quando a justificativa não tem de 10 a 500 caracteres.
- **Pagar** — `403` quando o Finance é o proprietário; `409` quando o estado não é
  `Approved` ou o pagamento é repetido; `404` inexistente.

As rotas de escrita exigem a role no token (`[Authorize(Roles=...)]`). Um usuário com
roles acumuladas recebe a união das permissões — por exemplo, `Approver`+`Employee`
também pode criar, editar e enviar os próprios rascunhos — porém as proibições de
autoaprovação, autopagamento e remoção da própria role `Admin` permanecem sempre.
A role `Admin` não concede acesso funcional: apenas administração de usuários e roles.

## Testes

Somente testes unitários escritos por você entram na nota. Testes de integração, end-to-end ou de interface são permitidos, mas opcionais e sem pontuação.

Os testes unitários devem executar sem banco, rede ou serviço externo.

## Entrega

Entregue:

- URL do repositório público;
- commit Secure Hash Algorithm (SHA) final;
- integração contínua executada;
- documentação atualizada.

O checklist de confirmação da entrega (build e testes, score do pipeline de qualidade,
PRs sem `Closes`/`Fixes`/`Resolves` e commits por integrante) e o rascunho da descrição
da PR final estão em [docs/ENTREGA.md](docs/ENTREGA.md).

O projeto deve compilar sem erros e ser entregue sem warnings para receber a pontuação integral de Qualidade de Código.
