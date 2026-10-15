using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Validation;

/// <summary>Exige uma data preenchida e não futura, avaliada no relógio do servidor em UTC.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotFutureDateAttribute : ValidationAttribute
{
    /// <summary>Valida se o valor é uma data preenchida e não futura.</summary>
    /// <param name="value">Valor informado no DTO.</param>
    /// <param name="validationContext">Contexto da validação declarativa.</param>
    /// <returns>Sucesso quando a data é válida; erro quando ausente ou futura.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date || date == default)
        {
            return new ValidationResult("A data da despesa é obrigatória.");
        }

        DateOnly todayUtc = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        if (date > todayUtc)
        {
            return new ValidationResult("A data da despesa não pode estar no futuro.");
        }

        return ValidationResult.Success;
    }
}
