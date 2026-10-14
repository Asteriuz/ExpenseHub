using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Reembolso cadastrado por um usuário.</summary>
public class Expense
{
    /// <summary>Identificador gerado pelo servidor.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador do usuário proprietário, derivado do token de autenticação.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Descrição da despesa; entre 10 e 500 caracteres.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Valor único do reembolso, em reais; entre 0,01 e Int32.MaxValue.</summary>
    public decimal Amount { get; set; }

    /// <summary>Data da despesa; válida e não futura.</summary>
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Estado atual, definido exclusivamente pelo servidor.</summary>
    public ExpenseState State { get; set; } = ExpenseState.Draft;

    /// <summary>Identificador opcional da categoria da despesa.</summary>
    public Guid? ExpenseCategoryId { get; set; }

    /// <summary>Categoria da despesa.</summary>
    public ExpenseCategory? Category { get; set; }

    /// <summary>Instante UTC de criação, definido pelo servidor.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Instante UTC da última edição em rascunho; nulo se nunca editado.</summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
