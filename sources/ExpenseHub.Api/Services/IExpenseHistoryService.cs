using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Services;

/// <summary>Contrato do serviço de histórico auditável dos reembolsos.</summary>
public interface IExpenseHistoryService
{
    /// <summary>Registra a criação de um reembolso na operação lógica em curso, sem persistir.</summary>
    /// <param name="expense">Reembolso recém-criado, já adicionado ao contexto de dados.</param>
    /// <param name="actorId">Identificador do ator que executou a ação, derivado do token de acesso.</param>
    /// <param name="occurredAtUtc">Instante UTC da ação, definido pelo relógio do servidor.</param>
    void RecordCreation(Expense expense, string actorId, DateTime occurredAtUtc);

    /// <summary>Registra alterações realizadas em um rascunho na operação lógica em curso, sem persistir.</summary>
    /// <param name="expense">Rascunho alterado.</param>
    /// <param name="actorId">Identificador do ator que executou a ação, derivado do token de acesso.</param>
    /// <param name="occurredAtUtc">Instante UTC da ação, definido pelo relógio do servidor.</param>
    /// <param name="changesSummary">Resumo das alterações no formato antes→depois.</param>
    void RecordDraftChanges(Expense expense, string actorId, DateTime occurredAtUtc, string changesSummary);

    /// <summary>Registra uma transição de estado na operação lógica em curso, sem persistir.</summary>
    /// <param name="expense">Reembolso transicionado.</param>
    /// <param name="actorId">Identificador do ator que executou a ação, derivado do token de acesso.</param>
    /// <param name="occurredAtUtc">Instante UTC da ação, definido pelo relógio do servidor.</param>
    /// <param name="previousState">Estado anterior à ação.</param>
    /// <param name="newState">Estado resultante da ação.</param>
    /// <param name="rejectionJustification">Justificativa da reprovação; nulo nas demais ações.</param>
    void RecordTransition(Expense expense, string actorId, DateTime occurredAtUtc, ExpenseState previousState, ExpenseState newState, string? rejectionJustification = null);
}
