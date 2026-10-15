using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>Estado do resultado da criação de um rascunho de reembolso.</summary>
public enum CreateExpenseStatus
{
    /// <summary>Rascunho criado com sucesso.</summary>
    Created,

    /// <summary>Entrada inválida; nenhum rascunho foi criado.</summary>
    Invalid,
}

/// <summary>Estado do resultado da edição de um rascunho de reembolso.</summary>
public enum UpdateExpenseStatus
{
    /// <summary>Rascunho atualizado com sucesso.</summary>
    Updated,

    /// <summary>Entrada inválida; o rascunho permanece inalterado.</summary>
    Invalid,

    /// <summary>O reembolso não pertence ao ator autenticado.</summary>
    Forbidden,

    /// <summary>Reembolso inexistente.</summary>
    NotFound,

    /// <summary>Estado do reembolso é diferente de Draft.</summary>
    NotDraft,
}

/// <summary>Estado do resultado do envio de um rascunho de reembolso.</summary>
public enum SubmitExpenseStatus
{
    /// <summary>Reembolso enviado com sucesso para aprovação.</summary>
    Submitted,

    /// <summary>O reembolso não pertence ao ator autenticado.</summary>
    Forbidden,

    /// <summary>Reembolso inexistente.</summary>
    NotFound,

    /// <summary>Estado do reembolso é diferente de Draft, inclusive envio repetido.</summary>
    NotDraft,
}

/// <summary>Estado do resultado da listagem de reembolsos.</summary>
public enum ListExpensesStatus
{
    /// <summary>Listagem produzida com o escopo visível ao ator.</summary>
    Listed,

    /// <summary>O ator autenticado não possui nenhuma role com acesso funcional.</summary>
    Forbidden,
}

/// <summary>Estado do resultado da consulta de detalhe de um reembolso.</summary>
public enum ExpenseDetailStatus
{
    /// <summary>Reembolso encontrado dentro do escopo de leitura do ator.</summary>
    Found,

    /// <summary>Reembolso inexistente ou fora do escopo de leitura do ator.</summary>
    NotFound,

    /// <summary>O ator autenticado não possui nenhuma role com acesso funcional.</summary>
    Forbidden,
}

/// <summary>Resultado da criação de um rascunho de reembolso.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso criado quando o status é <see cref="CreateExpenseStatus.Created"/>.</param>
/// <param name="Errors">Mensagens de validação quando o status é <see cref="CreateExpenseStatus.Invalid"/>.</param>
public sealed record CreateExpenseOutcome(CreateExpenseStatus Status, ExpenseResponse? Expense, IReadOnlyList<string> Errors)
{
    /// <summary>Cria o resultado de um rascunho criado com sucesso.</summary>
    /// <param name="expense">Reembolso criado.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static CreateExpenseOutcome Created(ExpenseResponse expense) => new(CreateExpenseStatus.Created, expense, []);

    /// <summary>Cria o resultado de entrada inválida.</summary>
    /// <param name="errors">Mensagens de validação.</param>
    /// <returns>O resultado de entrada inválida.</returns>
    public static CreateExpenseOutcome Invalid(IReadOnlyList<string> errors) => new(CreateExpenseStatus.Invalid, null, errors);
}

/// <summary>Resultado da edição de um rascunho de reembolso.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso atualizado quando o status é <see cref="UpdateExpenseStatus.Updated"/>.</param>
/// <param name="Errors">Mensagens de validação quando o status é <see cref="UpdateExpenseStatus.Invalid"/>.</param>
public sealed record UpdateExpenseOutcome(UpdateExpenseStatus Status, ExpenseResponse? Expense, IReadOnlyList<string> Errors)
{
    /// <summary>Cria o resultado de um rascunho atualizado com sucesso.</summary>
    /// <param name="expense">Reembolso atualizado.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static UpdateExpenseOutcome Updated(ExpenseResponse expense) => new(UpdateExpenseStatus.Updated, expense, []);

    /// <summary>Cria o resultado de entrada inválida.</summary>
    /// <param name="errors">Mensagens de validação.</param>
    /// <returns>O resultado de entrada inválida.</returns>
    public static UpdateExpenseOutcome Invalid(IReadOnlyList<string> errors) => new(UpdateExpenseStatus.Invalid, null, errors);

    /// <summary>Cria o resultado de ator sem permissão sobre o reembolso.</summary>
    /// <returns>O resultado de acesso negado.</returns>
    public static UpdateExpenseOutcome Forbidden() => new(UpdateExpenseStatus.Forbidden, null, []);

    /// <summary>Cria o resultado de reembolso inexistente.</summary>
    /// <returns>O resultado de recurso não encontrado.</returns>
    public static UpdateExpenseOutcome NotFound() => new(UpdateExpenseStatus.NotFound, null, []);

    /// <summary>Cria o resultado de estado incompatível com edição.</summary>
    /// <returns>O resultado de conflito de estado.</returns>
    public static UpdateExpenseOutcome NotDraft() => new(UpdateExpenseStatus.NotDraft, null, []);
}

/// <summary>Resultado do envio de um rascunho de reembolso.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso enviado quando o status é <see cref="SubmitExpenseStatus.Submitted"/>.</param>
public sealed record SubmitExpenseOutcome(SubmitExpenseStatus Status, ExpenseResponse? Expense)
{
    /// <summary>Cria o resultado de um envio bem-sucedido.</summary>
    /// <param name="expense">Reembolso em estado Submitted.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static SubmitExpenseOutcome Submitted(ExpenseResponse expense) => new(SubmitExpenseStatus.Submitted, expense);

    /// <summary>Cria o resultado de ator sem permissão sobre o reembolso.</summary>
    /// <returns>O resultado de acesso negado.</returns>
    public static SubmitExpenseOutcome Forbidden() => new(SubmitExpenseStatus.Forbidden, null);

    /// <summary>Cria o resultado de reembolso inexistente.</summary>
    /// <returns>O resultado de recurso não encontrado.</returns>
    public static SubmitExpenseOutcome NotFound() => new(SubmitExpenseStatus.NotFound, null);

    /// <summary>Cria o resultado de estado incompatível com envio, inclusive envio repetido.</summary>
    /// <returns>O resultado de conflito de estado.</returns>
    public static SubmitExpenseOutcome NotDraft() => new(SubmitExpenseStatus.NotDraft, null);
}

/// <summary>Resultado da listagem de reembolsos.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Items">Reembolsos no escopo visível quando o status é <see cref="ListExpensesStatus.Listed"/>.</param>
public sealed record ListExpensesOutcome(ListExpensesStatus Status, IReadOnlyList<ExpenseResponse> Items)
{
    /// <summary>Cria o resultado de uma listagem bem-sucedida.</summary>
    /// <param name="items">Reembolsos no escopo visível.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static ListExpensesOutcome Listed(IReadOnlyList<ExpenseResponse> items) => new(ListExpensesStatus.Listed, items);

    /// <summary>Cria o resultado de ator sem acesso funcional.</summary>
    /// <returns>O resultado de acesso negado.</returns>
    public static ListExpensesOutcome Forbidden() => new(ListExpensesStatus.Forbidden, []);
}

/// <summary>Resultado da consulta de detalhe de um reembolso.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso encontrado quando o status é <see cref="ExpenseDetailStatus.Found"/>.</param>
public sealed record ExpenseDetailOutcome(ExpenseDetailStatus Status, ExpenseResponse? Expense)
{
    /// <summary>Cria o resultado de um reembolso encontrado.</summary>
    /// <param name="expense">Reembolso no escopo de leitura do ator.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static ExpenseDetailOutcome Found(ExpenseResponse expense) => new(ExpenseDetailStatus.Found, expense);

    /// <summary>Cria o resultado de reembolso inexistente ou fora do escopo de leitura.</summary>
    /// <returns>O resultado de recurso não encontrado.</returns>
    public static ExpenseDetailOutcome NotFound() => new(ExpenseDetailStatus.NotFound, null);

    /// <summary>Cria o resultado de ator sem acesso funcional.</summary>
    /// <returns>O resultado de acesso negado.</returns>
    public static ExpenseDetailOutcome Forbidden() => new(ExpenseDetailStatus.Forbidden, null);
}

/// <summary>Regras de negócio dos reembolsos: rascunhos, envio e consulta por perfil.</summary>
public interface IExpenseService
{
    /// <summary>Cria um rascunho de reembolso em estado Draft para o ator autenticado.</summary>
    /// <param name="request">Campos informados pelo cliente, já validados por DataAnnotations.</param>
    /// <param name="ownerId">Identificador do proprietário, derivado do token de acesso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da criação.</returns>
    Task<CreateExpenseOutcome> CreateAsync(CreateExpenseRequest request, string ownerId, CancellationToken cancellationToken);

    /// <summary>Edita um rascunho próprio em estado Draft, sem alterar o estado.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Campos informados pelo cliente, já validados por DataAnnotations.</param>
    /// <param name="ownerId">Identificador do proprietário, derivado do token de acesso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da edição.</returns>
    Task<UpdateExpenseOutcome> UpdateAsync(Guid id, UpdateExpenseRequest request, string ownerId, CancellationToken cancellationToken);

    /// <summary>Envia um rascunho próprio, transicionando de Draft para Submitted.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="ownerId">Identificador do proprietário, derivado do token de acesso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado do envio.</returns>
    Task<SubmitExpenseOutcome> SubmitAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>Lista os reembolsos visíveis ao ator conforme suas roles, filtrando no IQueryable.</summary>
    /// <param name="roles">Roles do ator autenticado, derivadas do token de acesso.</param>
    /// <param name="actorId">Identificador do ator autenticado, derivado do token de acesso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da listagem.</returns>
    Task<ListExpensesOutcome> ListAsync(IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken);

    /// <summary>Consulta o detalhe de um reembolso visível ao ator conforme suas roles.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="roles">Roles do ator autenticado, derivadas do token de acesso.</param>
    /// <param name="actorId">Identificador do ator autenticado, derivado do token de acesso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da consulta.</returns>
    Task<ExpenseDetailOutcome> GetByIdAsync(Guid id, IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken);
}
