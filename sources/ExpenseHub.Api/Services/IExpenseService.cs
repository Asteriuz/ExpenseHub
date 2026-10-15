using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>Estado do resultado de uma criação de rascunho de reembolso.</summary>
public enum CreateExpenseStatus
{
    /// <summary>Rascunho criado com sucesso.</summary>
    Created,

    /// <summary>Entrada inválida conforme as regras de campo.</summary>
    Invalid,
}

/// <summary>Estado do resultado de uma edição de rascunho de reembolso.</summary>
public enum UpdateExpenseStatus
{
    /// <summary>Rascunho editado com sucesso.</summary>
    Updated,

    /// <summary>Reembolso inexistente.</summary>
    NotFound,

    /// <summary>O reembolso existe, mas o ator autenticado não é o proprietário.</summary>
    Forbidden,

    /// <summary>O reembolso não está em Draft; transição incompatível.</summary>
    NotDraft,

    /// <summary>Entrada inválida conforme as regras de campo.</summary>
    Invalid,
}

/// <summary>Resultado da criação de um rascunho de reembolso.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso criado quando o status é <see cref="CreateExpenseStatus.Created"/>.</param>
/// <param name="Errors">Mensagens de erro quando o status é <see cref="CreateExpenseStatus.Invalid"/>.</param>
public sealed record CreateExpenseOutcome(
    CreateExpenseStatus Status,
    ExpenseResponse? Expense,
    IReadOnlyList<string> Errors)
{
    /// <summary>Cria o resultado de uma criação bem-sucedida.</summary>
    /// <param name="expense">Rascunho criado.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static CreateExpenseOutcome Created(ExpenseResponse expense) => new(CreateExpenseStatus.Created, expense, []);

    /// <summary>Cria o resultado de entrada inválida.</summary>
    /// <param name="errors">Mensagens de erro da validação.</param>
    /// <returns>O resultado de entrada inválida.</returns>
    public static CreateExpenseOutcome Invalid(IReadOnlyList<string> errors) => new(CreateExpenseStatus.Invalid, null, errors);
}

/// <summary>Resultado da edição de um rascunho de reembolso.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso atualizado quando o status é <see cref="UpdateExpenseStatus.Updated"/>.</param>
/// <param name="Errors">Mensagens de erro quando o status é <see cref="UpdateExpenseStatus.Invalid"/>.</param>
public sealed record UpdateExpenseOutcome(
    UpdateExpenseStatus Status,
    ExpenseResponse? Expense,
    IReadOnlyList<string> Errors)
{
    /// <summary>Cria o resultado de uma edição bem-sucedida.</summary>
    /// <param name="expense">Rascunho atualizado.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static UpdateExpenseOutcome Updated(ExpenseResponse expense) => new(UpdateExpenseStatus.Updated, expense, []);

    /// <summary>Cria o resultado para reembolso inexistente.</summary>
    /// <returns>O resultado de reembolso inexistente.</returns>
    public static UpdateExpenseOutcome NotFound() => new(UpdateExpenseStatus.NotFound, null, []);

    /// <summary>Cria o resultado para ator não proprietário.</summary>
    /// <returns>O resultado de acesso proibido.</returns>
    public static UpdateExpenseOutcome Forbidden() => new(UpdateExpenseStatus.Forbidden, null, []);

    /// <summary>Cria o resultado para estado incompatível com edição.</summary>
    /// <returns>O resultado de transição incompatível.</returns>
    public static UpdateExpenseOutcome NotDraft() => new(UpdateExpenseStatus.NotDraft, null, []);

    /// <summary>Cria o resultado de entrada inválida.</summary>
    /// <param name="errors">Mensagens de erro da validação.</param>
    /// <returns>O resultado de entrada inválida.</returns>
    public static UpdateExpenseOutcome Invalid(IReadOnlyList<string> errors) => new(UpdateExpenseStatus.Invalid, null, errors);
}

/// <summary>Estado do resultado de um envio de rascunho para aprovação.</summary>
public enum SubmitExpenseStatus
{
    /// <summary>Rascunho enviado com sucesso.</summary>
    Submitted,

    /// <summary>Reembolso inexistente.</summary>
    NotFound,

    /// <summary>O reembolso existe, mas o ator autenticado não é o proprietário.</summary>
    Forbidden,

    /// <summary>O reembolso não está em Draft; transição incompatível ou repetida.</summary>
    NotDraft,
}

/// <summary>Estado do resultado de uma listagem de reembolsos conforme o perfil do ator.</summary>
public enum ListExpensesStatus
{
    /// <summary>Listagem produzida com o escopo de leitura do ator.</summary>
    Listed,

    /// <summary>O ator autenticado não possui role funcional de leitura.</summary>
    Forbidden,
}

/// <summary>Estado do resultado de uma consulta de detalhe de reembolso.</summary>
public enum ExpenseDetailStatus
{
    /// <summary>Reembolso visível para o ator autenticado.</summary>
    Found,

    /// <summary>Reembolso inexistente ou fora do escopo de leitura do ator.</summary>
    NotFound,

    /// <summary>O ator autenticado não possui role funcional de leitura.</summary>
    Forbidden,
}

/// <summary>Resultado do envio de um rascunho para aprovação.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso atualizado quando o status é <see cref="SubmitExpenseStatus.Submitted"/>.</param>
public sealed record SubmitExpenseOutcome(SubmitExpenseStatus Status, ExpenseResponse? Expense)
{
    /// <summary>Cria o resultado de um envio bem-sucedido.</summary>
    /// <param name="expense">Reembolso enviado.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static SubmitExpenseOutcome Submitted(ExpenseResponse expense) => new(SubmitExpenseStatus.Submitted, expense);

    /// <summary>Cria o resultado para reembolso inexistente.</summary>
    /// <returns>O resultado de reembolso inexistente.</returns>
    public static SubmitExpenseOutcome NotFound() => new(SubmitExpenseStatus.NotFound, null);

    /// <summary>Cria o resultado para ator não proprietário.</summary>
    /// <returns>O resultado de acesso proibido.</returns>
    public static SubmitExpenseOutcome Forbidden() => new(SubmitExpenseStatus.Forbidden, null);

    /// <summary>Cria o resultado para estado incompatível com envio.</summary>
    /// <returns>O resultado de transição incompatível.</returns>
    public static SubmitExpenseOutcome NotDraft() => new(SubmitExpenseStatus.NotDraft, null);
}

/// <summary>Resultado da listagem de reembolsos conforme o perfil do ator.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Items">Reembolsos visíveis; lista vazia quando o status é <see cref="ListExpensesStatus.Forbidden"/>.</param>
public sealed record ListExpensesOutcome(ListExpensesStatus Status, IReadOnlyList<ExpenseResponse> Items)
{
    /// <summary>Cria o resultado de uma listagem bem-sucedida.</summary>
    /// <param name="items">Reembolsos visíveis para o ator.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static ListExpensesOutcome Listed(IReadOnlyList<ExpenseResponse> items) => new(ListExpensesStatus.Listed, items);

    /// <summary>Cria o resultado para ator sem role funcional de leitura.</summary>
    /// <returns>O resultado de acesso proibido.</returns>
    public static ListExpensesOutcome Forbidden() => new(ListExpensesStatus.Forbidden, []);
}

/// <summary>Resultado da consulta de detalhe de um reembolso.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Expense">Reembolso visível quando o status é <see cref="ExpenseDetailStatus.Found"/>.</param>
public sealed record ExpenseDetailOutcome(ExpenseDetailStatus Status, ExpenseResponse? Expense)
{
    /// <summary>Cria o resultado de uma consulta bem-sucedida.</summary>
    /// <param name="expense">Reembolso visível para o ator.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static ExpenseDetailOutcome Found(ExpenseResponse expense) => new(ExpenseDetailStatus.Found, expense);

    /// <summary>Cria o resultado para reembolso inexistente ou fora do escopo de leitura.</summary>
    /// <returns>O resultado de reembolso não encontrado.</returns>
    public static ExpenseDetailOutcome NotFound() => new(ExpenseDetailStatus.NotFound, null);

    /// <summary>Cria o resultado para ator sem role funcional de leitura.</summary>
    /// <returns>O resultado de acesso proibido.</returns>
    public static ExpenseDetailOutcome Forbidden() => new(ExpenseDetailStatus.Forbidden, null);
}

/// <summary>
/// Operações de reembolso com a matriz de autorização aplicada na camada de serviço:
/// ownership, estado e visibilidade validados além dos atributos de role das rotas.
/// </summary>
public interface IExpenseService
{
    /// <summary>Cria um rascunho de reembolso em estado Draft para o proprietário informado.</summary>
    /// <param name="request">Dados validados do rascunho.</param>
    /// <param name="ownerId">Identificador do Employee autenticado, derivado do token.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da criação.</returns>
    Task<CreateExpenseOutcome> CreateAsync(CreateExpenseRequest request, string ownerId, CancellationToken cancellationToken);

    /// <summary>Edita um rascunho de reembolso próprio, mantendo o estado Draft.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Dados validados da edição.</param>
    /// <param name="ownerId">Identificador do Employee autenticado, derivado do token.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da edição.</returns>
    Task<UpdateExpenseOutcome> UpdateAsync(Guid id, UpdateExpenseRequest request, string ownerId, CancellationToken cancellationToken);

    /// <summary>Envia um rascunho próprio para aprovação, transicionando de Draft para Submitted.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="ownerId">Identificador do Employee autenticado, derivado do token.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado do envio.</returns>
    Task<SubmitExpenseOutcome> SubmitAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>Lista os reembolsos visíveis para o ator, aplicando a união dos escopos de suas roles no IQueryable.</summary>
    /// <param name="roles">Roles do ator autenticado, derivadas do token.</param>
    /// <param name="actorId">Identificador do ator autenticado, derivado do token.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da listagem.</returns>
    Task<ListExpensesOutcome> ListAsync(IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken);

    /// <summary>Consulta o detalhe de um reembolso respeitando o escopo de leitura do ator, sem revelar recursos fora dele.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="roles">Roles do ator autenticado, derivadas do token.</param>
    /// <param name="actorId">Identificador do ator autenticado, derivado do token.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O resultado da consulta.</returns>
    Task<ExpenseDetailOutcome> GetByIdAsync(Guid id, IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken);
}
