using System;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Registra o histórico auditável dos reembolsos adicionando as entradas ao mesmo
/// contexto de dados da operação: a persistência ocorre no único SaveChanges da
/// transição, garantindo atomicidade entre o estado do reembolso e o histórico.
/// </summary>
public sealed class ExpenseHistoryService : IExpenseHistoryService
{
    /// <summary>Nome da ação de criação de rascunho.</summary>
    public const string CreateAction = "Criar";

    /// <summary>Nome da ação de edição de rascunho.</summary>
    public const string EditAction = "Editar";

    /// <summary>Nome da ação de envio do rascunho para aprovação.</summary>
    public const string SubmitAction = "Enviar";

    /// <summary>Nome da ação de aprovação de reembolso.</summary>
    public const string ApproveAction = "Aprovar";

    /// <summary>Nome da ação de reprovação de reembolso.</summary>
    public const string RejectAction = "Reprovar";

    /// <summary>Nome da ação de pagamento de reembolso.</summary>
    public const string PayAction = "Pagar";

    private readonly ExpenseHubDbContext _dbContext;

    /// <summary>Inicializa o serviço com o contexto de dados compartilhado da operação.</summary>
    /// <param name="dbContext">Contexto do Entity Framework Core da transição em curso.</param>
    public ExpenseHistoryService(ExpenseHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public void RecordCreation(Expense expense, string actorId, DateTime occurredAtUtc)
    {
        _dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = CreateAction,
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            PreviousState = null,
            NewState = expense.State,
        });
    }

    /// <inheritdoc />
    public void RecordDraftChanges(Expense expense, string actorId, DateTime occurredAtUtc, string changesSummary)
    {
        _dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = EditAction,
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            PreviousState = expense.State,
            NewState = null,
            DraftChangesSummary = changesSummary,
        });
    }

    /// <inheritdoc />
    public void RecordTransition(Expense expense, string actorId, DateTime occurredAtUtc, ExpenseState previousState, ExpenseState newState, string? rejectionJustification = null)
    {
        _dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = GetTransitionAction(newState),
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            PreviousState = previousState,
            NewState = newState,
            RejectionJustification = rejectionJustification,
        });
    }

    /// <summary>Obtém o nome da ação correspondente ao estado resultante da transição.</summary>
    /// <param name="newState">Estado resultante da transição.</param>
    /// <returns>O nome da ação registrada no histórico.</returns>
    /// <exception cref="InvalidOperationException">O estado resultante não corresponde a nenhuma transição do domínio.</exception>
    private static string GetTransitionAction(ExpenseState newState)
    {
        return newState switch
        {
            ExpenseState.Submitted => SubmitAction,
            ExpenseState.Approved => ApproveAction,
            ExpenseState.Rejected => RejectAction,
            ExpenseState.Paid => PayAction,
            _ => throw new InvalidOperationException($"Transição para estado inesperado: {newState}."),
        };
    }
}
