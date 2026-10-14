namespace ExpenseHub.Api.Domain;

/// <summary>Estados possíveis do ciclo de vida de um reembolso.</summary>
public enum ExpenseState
{
    /// <summary>Rascunho criado pelo empregado.</summary>
    Draft = 1,

    /// <summary>Enviado para aprovação.</summary>
    Submitted = 2,

    /// <summary>Aprovado por um aprovador.</summary>
    Approved = 3,

    /// <summary>Reprovado com justificativa; estado final.</summary>
    Rejected = 4,

    /// <summary>Pago pelo financeiro; estado final.</summary>
    Paid = 5,
}
