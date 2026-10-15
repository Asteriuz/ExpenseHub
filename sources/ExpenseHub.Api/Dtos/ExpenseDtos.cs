using System;
using System.ComponentModel.DataAnnotations;
using ExpenseHub.Api.Validation;

namespace ExpenseHub.Api.Dtos;

/// <summary>Solicitação de criação de um rascunho de reembolso.</summary>
public sealed class CreateExpenseRequest
{
    /// <summary>Descrição da despesa; entre 10 e 500 caracteres.</summary>
    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "A descrição deve ter entre 10 e 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Valor único do reembolso, em reais; entre 0,01 e 2.147.483.647,00.</summary>
    [Range(typeof(decimal), "0.01", "2147483647.00", ErrorMessage = "O valor deve estar entre 0,01 e 2.147.483.647,00.")]
    public decimal Amount { get; set; }

    /// <summary>Data da despesa; preenchida e não futura.</summary>
    [NotFutureDate]
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Identificador opcional da categoria da despesa.</summary>
    public Guid? ExpenseCategoryId { get; set; }
}

/// <summary>Solicitação de edição de um rascunho de reembolso próprio.</summary>
public sealed class UpdateExpenseRequest
{
    /// <summary>Descrição da despesa; entre 10 e 500 caracteres.</summary>
    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "A descrição deve ter entre 10 e 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Valor único do reembolso, em reais; entre 0,01 e 2.147.483.647,00.</summary>
    [Range(typeof(decimal), "0.01", "2147483647.00", ErrorMessage = "O valor deve estar entre 0,01 e 2.147.483.647,00.")]
    public decimal Amount { get; set; }

    /// <summary>Data da despesa; preenchida e não futura.</summary>
    [NotFutureDate]
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Identificador opcional da categoria da despesa.</summary>
    public Guid? ExpenseCategoryId { get; set; }
}

/// <summary>Representação de um reembolso retornada pela API.</summary>
public sealed class ExpenseResponse
{
    /// <summary>Identificador gerado pelo servidor.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador do usuário proprietário, derivado do token de autenticação.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Descrição da despesa.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Valor único do reembolso, em reais.</summary>
    public decimal Amount { get; set; }

    /// <summary>Data da despesa.</summary>
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Nome do estado atual, definido exclusivamente pelo servidor.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Identificador opcional da categoria da despesa.</summary>
    public Guid? ExpenseCategoryId { get; set; }

    /// <summary>Instante UTC de criação, definido pelo servidor.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Instante UTC da última edição em rascunho; nulo se nunca editado.</summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
