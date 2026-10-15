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

/// <summary>Cria e edita rascunhos de reembolso; operações exclusivas da role Employee.</summary>
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
}
