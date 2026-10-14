using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Categoria de classificação de um reembolso.</summary>
public class ExpenseCategory
{
    /// <summary>Identificador gerado pelo servidor.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nome único da categoria.</summary>
    public string Name { get; set; } = string.Empty;
}
