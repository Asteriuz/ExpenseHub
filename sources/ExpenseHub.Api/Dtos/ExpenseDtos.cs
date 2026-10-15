using System;
using System.ComponentModel.DataAnnotations;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Dtos;

/// <summary>Campos de rascunho de reembolso informados pelo cliente.</summary>
public interface IExpenseDraftRequest
{
    /// <summary>Descrição da despesa; entre 10 e 500 caracteres.</summary>
    string Description { get; }

    /// <summary>Valor único do reembolso, em reais; entre 0,01 e Int32.MaxValue.</summary>
    decimal Amount { get; }

    /// <summary>Data da despesa; válida e não futura.</summary>
    DateOnly ExpenseDate { get; }

    /// <summary>Identificador opcional da categoria da despesa.</summary>
    Guid? ExpenseCategoryId { get; }
}

/// <summary>Dados enviados ao endpoint de criação de rascunho de reembolso.</summary>
/// <remarks>
/// O DTO contém apenas campos editáveis pelo cliente: proprietário, estado, ator
/// e timestamps de auditoria são sempre derivados do token JWT e do relógio do servidor.
/// </remarks>
public sealed class CreateExpenseRequest : IExpenseDraftRequest
{
    /// <summary>Descrição da despesa; entre 10 e 500 caracteres.</summary>
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Valor único do reembolso, em reais; entre 0,01 e Int32.MaxValue.</summary>
    [Range(typeof(decimal), "0.01", "2147483647.00")]
    public decimal Amount { get; set; }

    /// <summary>Data da despesa; válida e não futura.</summary>
    [NotFutureDate]
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Identificador opcional da categoria; quando informado, deve existir.</summary>
    public Guid? ExpenseCategoryId { get; set; }
}

/// <summary>Dados enviados ao endpoint de edição de rascunho de reembolso.</summary>
/// <remarks>
/// O DTO contém apenas campos editáveis pelo cliente: proprietário, estado, ator
/// e timestamps de auditoria são sempre derivados do token JWT e do relógio do servidor.
/// </remarks>
public sealed class UpdateExpenseRequest : IExpenseDraftRequest
{
    /// <summary>Descrição da despesa; entre 10 e 500 caracteres.</summary>
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Valor único do reembolso, em reais; entre 0,01 e Int32.MaxValue.</summary>
    [Range(typeof(decimal), "0.01", "2147483647.00")]
    public decimal Amount { get; set; }

    /// <summary>Data da despesa; válida e não futura.</summary>
    [NotFutureDate]
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Identificador opcional da categoria; quando informado, deve existir.</summary>
    public Guid? ExpenseCategoryId { get; set; }
}

/// <summary>Reembolso devolvido pela API, sem dados de auditoria internos.</summary>
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

    /// <summary>Estado atual, definido exclusivamente pelo servidor.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Identificador opcional da categoria da despesa.</summary>
    public Guid? ExpenseCategoryId { get; set; }

    /// <summary>Instante UTC de criação, definido pelo servidor.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Instante UTC da última edição em rascunho; nulo se nunca editado.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Cria a resposta correspondente a uma entidade de reembolso.</summary>
    /// <param name="expense">Entidade de reembolso persistida.</param>
    /// <returns>A resposta com os dados do reembolso.</returns>
    public static ExpenseResponse FromExpense(Expense expense)
    {
        return new ExpenseResponse
        {
            Id = expense.Id,
            OwnerId = expense.OwnerId,
            Description = expense.Description,
            Amount = expense.Amount,
            ExpenseDate = expense.ExpenseDate,
            State = expense.State.ToString(),
            ExpenseCategoryId = expense.ExpenseCategoryId,
            CreatedAtUtc = expense.CreatedAtUtc,
            UpdatedAtUtc = expense.UpdatedAtUtc,
        };
    }
}
