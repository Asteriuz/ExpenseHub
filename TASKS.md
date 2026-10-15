Vou primeiro buscar o repositório para entender o conteúdo e as tarefas dele.
O repositório é um checkpoint acadêmico de C# (API corporativa de reembolsos). Vou explorar a pasta `docs/` e as issues para montar as tasks com precisão.
Encontrei as 10 issues (I01–I10) e os documentos de especificação. Vou ler os documentos-chave (via raw) para construir o prompt com os critérios corretos.
Agora vou ler as regras de qualidade, o uso de IA e o detalhe de cada issue (via API do GitHub).
Li o repositório completo: README, os 5 documentos de especificação (`docs/ENUNCIADO.md`, `REQUISITOS.md`, `MATRIZ-AUTORIZACAO.md`, `RUBRICA.md`, `USO-DE-IA.md`, `code-quality-rules.md`) e as 10 issues do backlog ([issues do repo](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues)). Abaixo está o material pronto para o Kilo Code.

**Resumo do que é:** API REST ASP.NET Core (Identity + EF Core + JWT bearer) de reembolsos, com fluxo `Draft → Submitted → Approved/Rejected → Paid`, roles `Employee/Approver/Finance/Auditor/Admin`, ownership e histórico auditável. Entrega: **13/10/2026**, com pipeline de qualidade valendo **25%** da nota (RUBRICA.md).

---

## Parte 1 — Preparação (você faz uma vez, fora do Kilo Code)

1. Crie o repositório do grupo com **Use this template** (não use fork) e adicione os colegas como colaboradores (README.md).
2. Clone e abra no VS Code com a extensão **Kilo Code**.
3. Cadastre a senha do Admin seed em segredo de desenvolvimento (nunca em prompt, código ou commit):
   ```bash
   cd sources/ExpenseHub.Api
   dotnet user-secrets init
   dotnet user-secrets set "Seed:AdminPassword" "<senha-forte>"
   ```
4. Nas **Custom Instructions / Rules do Kilo Code** (Settings → Rules, ou arquivo de regras do projeto na raiz, conforme sua versão), cole o bloco da Parte 2.
5. **Regra de ouro do grupo** (USO-DE-IA.md): o Kilo Code edita os arquivos, mas **quem commita é o integrante, com o próprio usuário**, após revisar o `git diff`. Cada membro precisa de **mais de um commit** e cada issue vira uma branch + PR própria. Rode uma task por sessão do Kilo Code, revisando antes de commitar.

## Parte 2 — Regras globais (cole nas regras do Kilo Code, vale para todas as tasks)

```text
# Contexto
Projeto ExpenseHub: API ASP.NET Core de reembolsos com ASP.NET Core Identity,
Entity Framework Core, autenticação bearer JWT e autorização por roles.
Leia sempre docs/REQUISITOS.md, docs/MATRIZ-AUTORIZACAO.md e docs/RUBRICA.md
antes de implementar. Estrutura: sources/ExpenseHub.slnx (Api + UnitTests).

# Regras inegociáveis
1. NUNCA modifique .github/, .editorconfig, Directory.Build.props ou scripts/.
   Alterar o pipeline para inflar score pode zerar a nota (I10).
2. NUNCA versionar segredos, senhas, tokens ou connection string com senha.
   Senha do Admin vem de user-secrets/variável de ambiente. CI roda Gitleaks.
3. NUNCA versionar bin/, obj/, .vs/ ou banco de dados local.
4. NÃO faça commits: você apenas edita os arquivos e para. O humano revisa e commita.
5. NUNCA confiar em dados do cliente: ownerId, role, estado, ator e timestamps
   são sempre derivados do token JWT e do relógio do servidor (UTC).
6. Autorização em duas camadas: role via [Authorize(Roles=...)] na rota;
   ownership/estado/visibilidade validados na camada de serviço, nunca só no controller.
7. Listagens filtram no IQueryable ANTES de materializar (nunca carregar tudo e
   filtrar em memória). Preferir AnyAsync a CountAsync > 0. Sem Thread.Sleep e
   sem consulta síncrona ao banco em controller.
8. Endpoints recebem SEMPRE DTOs com validação declarativa (DataAnnotations);
   nunca receber a entidade EF direto. Erros usam ProblemDetails.
9. Contrato HTTP: 400 entrada inválida; 401 anônimo/credencial inválida;
   403 autenticado sem permissão; 404 inexistente ou fora do escopo de leitura;
   409 transição incompatível ou repetida.
10. Zero warnings: compilação, analisadores e .editorconfig. Build e testes devem
    passar antes de encerrar a task. Não alterar o TFM definido em Directory.Build.props.
```

## Parte 3 — Prompts por task (cole um por sessão do Kilo Code, na ordem)

### TASK 1 — I01: Fundação da solução e EF Core (4%) — branch `i01-foundation-ef`

```text
TASK 1 — Fundação e Entity Framework Core (issue Racass/checkpoint-csharpracass-expensehub#1)

Contexto: sources/ExpenseHub.slnx com ExpenseHub.Api e ExpenseHub.UnitTests.
Leia docs/REQUISITOS.md (seção Entidades mínimas) e a estrutura atual antes de codar.

Implemente:
1. Inspecione a solução (TFM, pacotes, framework de testes) e reporte o que encontrou.
2. Escolha o provider relacional: use SQLite (Microsoft.EntityFrameworkCore.Sqlite),
   documentando a escolha. O build NÃO pode depender de servidor de banco rodando.
3. DbContext com mapeamentos das entidades mínimas: Expense, ExpenseCategory,
   ExpenseHistory, PaymentRecord. Cada reembolso tem UM único valor (sem coleção de itens).
   Campos de Expense: Id (server), OwnerId, Description, Amount (decimal), ExpenseDate,
   State (enum Draft, Submitted, Approved, Rejected, Paid), timestamps de auditoria.
   ExpenseHistory: ação, ator, instante UTC, estado anterior, estado posterior,
   justificativa da reprovação, resumo das alterações em Draft. PaymentRecord: ator e UTC.
4. Migrations criadas e comandos documentados no README: provider e pacote,
   configuração, como criar/atualizar o banco, como rodar a API.
5. Nada de endpoints de domínio nesta task (fora de escopo da issue).

Valide: dotnet restore ./sources/ExpenseHub.slnx && dotnet build ./sources/ExpenseHub.slnx
sem erros e sem warnings.
```

### TASK 2 — I02: Identity, Admin e autenticação (9%) — branch `i02-identity-auth`

```text
TASK 2 — Identity, bearer e seed (issue Racass/checkpoint-csharpracass-expensehub#2)

Implemente:
1. ASP.NET Core Identity persistido no banco relacional (IdentityUser + EF Core).
2. Autenticação bearer JWT (Microsoft.AspNetCore.Authentication.JwtBearer): token com
   sub (user id) e roles como claims. POST /login público: credenciais válidas emitem
   token; inválidas → 401. Rotas de negócio protegidas por padrão; GET /health anônimo.
3. Seed idempotente (pode rodar várias vezes sem duplicar): cria as roles Admin,
   Employee, Approver, Finance, Auditor e UMA única conta Admin vinculada à role Admin.
4. Senha do Admin vem de configuração segura (user-secrets/variável de ambiente,
   ex.: "Seed:AdminPassword"). NADA de senha em código-fonte, appsettings versionado ou log.
5. Diferencie respostas: anônimo em rota protegida → 401; autenticado sem a role
   necessária → 403 (via handler de autorização com ProblemDetails).

Fora de escopo: cadastro de usuários e gestão de roles (issue I03).

Valide: login válido/inválido, seed executado 2x sem duplicar, build sem warnings.
```

### TASK 3 — I03: Cadastro HTTP e gestão de roles (8%) — branch `i03-user-roles`

```text
TASK 3 — Cadastro e administração de roles (issue Racass/checkpoint-csharpracass-expensehub#3)

Implemente:
1. POST /register (público): cria usuário por DTO validado. O DTO NÃO pode conter
   campo de role: cadastro nunca promove ninguém. Entrada inválida → 400.
2. GET /api/admin/users: somente Admin → lista usuários e suas roles. Demais
   autenticados → 403.
3. PUT /api/admin/users/{id}/roles: somente Admin. Aceita lista de nomes de role;
   apenas as 5 roles conhecidas são aceitas (whitelist) — role arbitrária → 400;
   usuário inexistente → 404.
4. Proteção: o Admin NÃO pode remover a própria role Admin (escolha 400 ou 403,
   documente a decisão no README e mantenha consistente).
5. O token carrega roles: documente no README que após alteração de roles é
   necessário autenticar novamente.

Valide: cadastro sem promoção, listagem negada a não-Admin, atribuição/remoção
válida, remoção da própria Admin bloqueada, build/testes sem warnings.
```

### TASK 4 — I04: Criar e editar rascunho (7%) — branch `i04-expense-draft`

```text
TASK 4 — Draft de reembolso (issue Racass/checkpoint-csharpracass-expensehub#4)

Implemente em camadas (controller → serviço → persistência):
1. POST /api/expenses [Authorize(Roles="Employee")]: cria rascunho em estado Draft.
   OwnerId obtido do token. DTO de entrada APENAS com Description, Amount, ExpenseDate
   (e categoria se houver) — sem campos de owner, estado, ator ou datas de auditoria.
2. Validações: Description 10–500 caracteres; Amount decimal entre 0,01 e
   2.147.483.647,00; ExpenseDate válida e NÃO futura. Violação → 400 com ProblemDetails.
3. PUT /api/expenses/{id} [Authorize(Roles="Employee")]: edita SOMENTE rascunho próprio.
   Não proprietário → 403 (ou 404, decida, documente e mantenha consistente);
   inexistente → 404; estado diferente de Draft → 409.
4. Estado, Id, owner e timestamps são definidos exclusivamente pelo servidor.
5. Estruture o serviço de Expense para permitir registrar histórico na mesma
   transação lógica (será usado em tasks seguintes).

Valide: criação e edição positivas; os 4 casos negativos da issue; build/testes limpos.
```

### TASK 5 — I05: Enviar, listar e consultar (7%) — branch `i05-submit-query`

```text
TASK 5 — Submit e consulta por perfil (issue Racass/checkpoint-csharpracass-expensehub#5)

Implemente:
1. POST /api/expenses/{id}/submit [Authorize(Roles="Employee")]: somente o proprietário;
   Draft → Submitted. Não proprietário → 403; estado inválido ou repetido → 409.
2. GET /api/expenses: visibilidade aplicada como filtro NO IQueryable, antes de
   materializar: Employee→próprios; Approver→Submitted; Finance→Approved e Paid;
   Auditor→todos; Admin→403 (role Admin não concede acesso funcional).
3. GET /api/expenses/{id}: mesma visibilidade; recurso inexistente ou fora do escopo
   de leitura → 404 (nunca vaze existência de recurso alheio).
4. Usuário autenticado sem role em rota de negócio → 403; anônimo → 401.

Valide: requisições com 4+ identidades distintas; Employee A não vê nem envia
reembolso do Employee B; build/testes limpos.
```

### TASK 6 — I06: Ownership e matriz de acesso (10%) — branch `i06-ownership-access`

```text
TASK 6 — Matriz de autorização completa (issue Racass/checkpoint-csharpracass-expensehub#6)

Objetivo: consolidar a docs/MATRIZ-AUTORIZACAO.md inteira na camada de serviço.

1. Revise TODAS as rotas contra a matriz. Regras contextuais na camada de serviço
   (nunca só atributo de role): isolamento total entre Employees; Approver só vê
   Submitted e NUNCA decide sobre o próprio; Finance só vê Approved/Paid e NUNCA
   paga o próprio; Auditor somente leitura; Admin sem acesso funcional.
2. Usuário com múltiplas roles recebe a união das permissões, mas as proibições de
   autoaprovação e autopagamento permanecem sempre.
3. Requisições anônimas → 401; sem role → 403; fora do escopo de leitura → 404;
   sem permissão na operação → 403. Repita os casos negativos obrigatórios da matriz.
4. Adicione no README (ou docs/) a tabela da matriz preenchida com o status HTTP
   que cada combinação retorna, como evidência.

Valide: matriz preenchida, testes negativos dos bloqueios (autoaprovação,
autopagamento, isolamento, Admin sem função), build/testes limpos.
```

### TASK 7 — I07: Aprovar e reprovar com justificativa (12%) — branch `i07-approve-reject`

```text
TASK 7 — Decisão de reembolsos (issue Racass/checkpoint-csharpracass-expensehub#7)

Implemente:
1. POST /api/expenses/{id}/approve [Authorize(Roles="Approver")]: Submitted → Approved.
   Ator e instante derivados do token/servidor (UTC).
2. POST /api/expenses/{id}/reject [Authorize(Roles="Approver")]: Submitted → Rejected,
   com DTO de justificativa OBRIGATÓRIA de 10–500 caracteres (fora disso → 400).
3. Somente Approver NÃO proprietário decide: proprietário com role Approver → 403.
   Employee/Finance/Auditor/Admin sem role Approver → 403.
4. Criar o serviço de histórico (ExpenseHistory): cada transição registrada na MESMA
   operação lógica (mesmo SaveChanges) com ação, ator, UTC, estado anterior e posterior.
   Inclua também a transição de submit (Draft → Submitted).
5. Repetição de decisão ou estado incompatível → 409 e NENHUMA linha de histórico
   duplicada. Rejected é estado final: sem reabertura, reenvio ou nova decisão.

Valide: aprovação/reprovação válidas; justificativa vazia/curta/longa → 400;
repetição → 409 sem histórico duplicado; Approved/Rejected/Paid não recebem decisão.
```

### TASK 8 — I08: Pagamento e histórico (8%) — branch `i08-payment-history`

```text
TASK 8 — Pagamento e histórico auditável (issue Racass/checkpoint-csharpracass-expensehub#8)

Implemente:
1. POST /api/expenses/{id}/pay [Authorize(Roles="Finance")]: Approved → Paid.
   Somente Finance NÃO proprietário paga (proprietário → 403). Estado inválido ou
   pagamento repetido → 409.
2. PaymentRecord com ator e horário (UTC) derivados do servidor, vinculado ao
   Expense, persistido na mesma transição.
3. Histórico completo: registrar também criação, edições em Draft (resumo das
   alterações antes→depois), submit e pagamento. Alteração e histórico sempre na
   mesma operação lógica (atomicidade: falha de persistência não deixa estado e
   histórico divergentes).
4. GET /api/expenses/{id}/history: mesma visibilidade da consulta de detalhe
   (Employee→próprio; Approver→Submitted; Finance→Approved/Paid; Auditor→todos;
   fora do escopo → 404). Auditor lê, mas não altera nada.

Valide: fluxo completo create→edit→submit→approve/reject→pay com um único Admin
criando roles; pagamentos e decisões repetidos → 409; histórico consultado por
cada perfil.
```

### TASK 9 — I09: Testes unitários (10%) — branch `i09-unit-tests`

```text
TASK 9 — Suíte de testes unitários (issue Racass/checkpoint-csharpracass-expensehub#9)

No projeto ExpenseHub.UnitTests (use o framework já configurado no template):

1. Cubra as regras CENTRAIS na camada de serviço, SEM banco, rede ou serviço externo
   (use abstrações/fakes ou provider in-memory apenas como substituto do EF).
2. Casos obrigatórios:
   - estados e transições válidas e inválidas (Draft→Submitted→Approved/Rejected→Paid);
   - ownership: Employee não lê/edita/envia reembolso alheio;
   - autoaprovação e autopagamento proibidos mesmo com roles acumuladas;
   - reprovação com justificativa 10–500 (vazia/curta/longa → erro);
   - pagamento apenas de Approved por Finance não proprietário;
   - repetição de transição → conflito sem histórico duplicado;
   - validações de descrição, valor e data.
3. Nomes de teste e assertivas expressam a REGRA verificada (ex.:
   Approve_ByNonOwnerExpense_Submitted_TransitionsToApproved). Sem testes de
   getters/mocks vazios — não pontuam.
4. Verificação de regressão: inverta temporariamente uma regra no serviço e confirme
   que o teste correspondente falha; depois restaure.
5. Atualize a cobertura faltante identificada nas tasks anteriores.

Valide: dotnet test ./sources/ExpenseHub.slnx verde, sem warnings.
```

### TASK 10 — I10: Qualidade de código (25%!) — branch `i10-code-quality`

```text
TASK 10 — Zero warnings e score 100 no pipeline (issue Racass/checkpoint-csharpracass-expensehub#10)

1. Rode o workflow code-quality no GitHub (ou o pipeline local, se houver script em
   scripts/) e LEIA o relatório/artefato code-quality-report com o score 0–100.
2. Corrija TODOS os findings por categoria, sem alterar .github/, .editorconfig,
   Directory.Build.props ou scripts/:
   - Build: restore/build limpos, sem warnings de compilador e analisadores (IDE*/CA*).
   - Segurança: nenhum segredo versionado (Gitleaks), nenhuma senha em texto puro.
   - Higiene: .gitignore e README presentes; nada de bin/obj/.vs/DB/artefatos;
     sem scaffolding WeatherForecast remanescente.
   - Estilo/Boas práticas: AnyAsync (nunca CountAsync>0), sem Thread.Sleep, sem
     consulta síncrona ao banco em controller, DTOs (nunca entidade) com
     DataAnnotations, projeto de testes passando.
3. dotnet build e dotnet test sem erros E sem warnings; todos os testes passando.
4. Registre o score final obtido na descrição da PR de entrega.
```

### TASK 11 — Entrega final (fechamento)

```text
TASK 11 — Documentação e entrega (leia docs/ENUNCIADO.md seção Entrega esperada)

1. README final com: provider e pacote, configuração (user-secrets/variáveis),
   criação/atualização do banco, como iniciar a API, nota sobre novo login após
   troca de roles, e tabela da matriz de autorização com status HTTP.
2. Confirme: compila sem warnings; testes passam; CI code-quality verde com score
   registrado; nenhuma PR usou Closes/Fixes/Resolves contra as issues originais;
   cada integrante do grupo tem mais de um commit.
3. Liste na PR final: URL do repositório, SHA do commit final, evidência do score
   do relatório de qualidade e das issues implementadas.
```

## Parte 4 — Fluxo de execução por task (repita para cada issue)

1. `git checkout main && git pull` → crie a branch sugerida no prompt (ex.: `i04-expense-draft`).
2. Cole o prompt da task no Kilo Code (sessão nova, modo *Code*; use *Architect* se quiser plano antes).
3. Revise o diff gerado (`git diff`) — **você é responsável pelo código** (USO-DE-IA.md).
4. Rode você mesmo: `dotnet build ./sources/ExpenseHub.slnx` e `dotnet test ./sources/ExpenseHub.slnx`.
5. Commit **com seu usuário**, push, abra a PR no **seu** repositório com título `I0X — <título>` e referência `Racass/checkpoint-csharpracass-expensehub#N` na descrição (**nunca** Closes/Fixes/Resolves — a issue original deve permanecer aberta).
6. Auto-revisão, merge, e siga para a próxima task.

**Atenção aos pontos que mais tiram nota:** I10 vale 25% e warnings descontam progressivamente; gates da rubrica zaram a nota acima de 2,0 se não compilar, 4,0 se autorização estiver sistematicamente ausente e 8,0 sem testes próprios significativos (RUBRICA.md). Priorize o pipeline de qualidade desde a Task 1, não só no final.

Fontes: [README do repositório](https://github.com/Racass/checkpoint-csharpracass-expensehub), [docs/](https://github.com/Racass/checkpoint-csharpracass-expensehub/tree/main/docs) e [issues I01–I10](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues) (lidas via API pública do GitHub).

Se quiser, posso adaptar o bloco de regras globais para o formato específico do seu Kilo Code (`.kilocode/rules`) ou gerar uma versão "one-shot" única com todas as tasks em sequência.