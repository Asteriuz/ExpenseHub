# Entrega final — evidências

Documento de fechamento (task de entrega). O conteúdo da seção **Descrição da PR final**
está pronto para colar na PR de entrega.

## Checklist de confirmação

| Item | Resultado | Evidência |
|---|---|---|
| Compilação sem warnings | ✅ 0 warnings, 0 erros | `dotnet build ./sources/ExpenseHub.slnx` (2026-10-15) |
| Testes unitários | ✅ 72/72 passando | `dotnet test ./sources/ExpenseHub.slnx` (2026-10-15) |
| CI `code-quality` verde em `main` | ✅ score **100/100** | run [38678190282](https://github.com/Asteriuz/ExpenseHub/actions/runs/38678190282), commit `5a97f3ed229452248970fde95149c4f56620a7a8`; artefato `code-quality-report` (`report.md`: status `passed`, categorias build/security/hygiene/style/best-practices 20/20, nenhum bloqueante) |
| PRs sem `Closes`/`Fixes`/`Resolves` | ✅ 9/9 PRs conformes | PRs #1–#9 referenciam `Racass/checkpoint-csharpracass-expensehub#N` sem palavras de fechamento |
| Commits por integrante | ✅ todos com mais de um commit | Asteriuz (16 commits), Juan Muradas (5 commits). O histórico registra dois autores; se o grupo tiver um terceiro integrante, ele ainda precisa registrar seus próprios commits antes da entrega |

O score 100/100 também vale para a PR de qualidade (run
[38678019444](https://github.com/Asteriuz/ExpenseHub/actions/runs/38678019444), branch
`i10-code-quality`), evidenciando que a nota integral de I10 foi obtida antes do merge.

## Issues implementadas

| Issue | Conteúdo | PR |
|---|---|---|
| I01 | Fundação da solução e Entity Framework Core (SQLite) | commits `e70e707`, `b80d326` |
| I02 | Identity, Admin e autenticação bearer JWT | PR #1 (`f2e4021`) |
| I03 | Cadastro HTTP e gestão de roles | PR #2 (`e0f3875`) |
| I04 | Criar e editar rascunho | PR #3 (`ab7d2b2`, `e27485d`, `47ef58f`) |
| I05 | Enviar, listar e consultar | PR #4 (`b81da6f`) |
| I06 | Ownership e matriz de acesso | PR #5 fechada sem merge; regras consolidadas na camada de serviço e entregues nas PRs #4, #6, #7 e #8 (owner checks em todas as transições, união de escopos por roles, bloqueio de autoaprovação/autopagamento); tabela da matriz com status HTTP documentada no README nesta task |
| I07 | Aprovar e reprovar com justificativa | PR #6 (`54608e2`) |
| I08 | Pagamento e histórico auditável | PR #7 (`37cfd9b`) |
| I09 | Testes unitários | PR #8 (`5474859`) |
| I10 | Qualidade de código — zero warnings, score 100 | PR #9 (`265b770`) |

## Descrição da PR final (colar na PR)

**Título:** `I11 — Entrega final: documentação e evidências`

**Descrição:**

```markdown
Entrega final do Checkpoint 2 — ExpenseHub.

- **URL do repositório:** https://github.com/Asteriuz/ExpenseHub
- **SHA do commit final:** `<preencher com o SHA do commit final após o merge>`
- **Integração contínua:** workflow `code-quality` verde em `main` — score
  **100/100** (run 38678190282, commit 5a97f3e; artefato `code-quality-report`,
  categorias build/security/hygiene/style/best-practices 20/20, nenhum bloqueante).
- **Issues implementadas:** Racass/checkpoint-csharpracass-expensehub#1 a #10,
  uma PR por issue (PRs #1–#9), todas referenciadas sem `Closes`/`Fixes`/`Resolves`.
- **Commits por integrante:** Asteriuz (16), Juan Muradas (5) — todos com mais de
  um commit.
- **Build e testes:** `dotnet build` com 0 warnings/0 erros; `dotnet test` com
  72/72 testes passando.
- **Documentação:** README com provider e pacotes, configuração segura
  (user-secrets/variáveis de ambiente), criação/atualização do banco, como iniciar
  a API, nota sobre reautenticação após troca de roles e tabela da matriz de
  autorização com o status HTTP de cada combinação ator × operação.

Referências: Racass/checkpoint-csharpracass-expensehub#1,
Racass/checkpoint-csharpracass-expensehub#2, Racass/checkpoint-csharpracass-expensehub#3,
Racass/checkpoint-csharpracass-expensehub#4, Racass/checkpoint-csharpracass-expensehub#5,
Racass/checkpoint-csharpracass-expensehub#6, Racass/checkpoint-csharpracass-expensehub#7,
Racass/checkpoint-csharpracass-expensehub#8, Racass/checkpoint-csharpracass-expensehub#9,
Racass/checkpoint-csharpracass-expensehub#10.
```

> **Atenção:** não usar `Closes`, `Fixes` ou `Resolves` — as issues originais devem
> permanecer abertas.
