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