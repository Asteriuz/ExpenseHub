using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Registro imutável de uma ação realizada em um reembolso.</summary>
public class ExpenseHistory
{
    /// <summary>Identificador gerado pelo servidor.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador do reembolso ao qual o registro pertence.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>Reembolso ao qual o registro pertence.</summary>
    public Expense? Expense { get; set; }

    /// <summary>Ação executada, como criar, editar, enviar, aprovar, reprovar ou pagar.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Identificador do usuário que executou a ação, derivado do token.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Instante UTC da ação, definido pelo servidor.</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>Estado anterior à ação; nulo na criação.</summary>
    public ExpenseState? PreviousState { get; set; }

    /// <summary>Estado resultante da ação; nulo quando o estado não muda, como na edição em rascunho.</summary>
    public ExpenseState? NewState { get; set; }

    /// <summary>Justificativa da reprovação; nulo nas demais ações.</summary>
    public string? RejectionJustification { get; set; }

    /// <summary>Resumo das alterações realizadas enquanto o reembolso estava em rascunho; nulo nas demais ações.</summary>
    public string? DraftChangesSummary { get; set; }
}
