using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace ExpenseHub.Api.Validation;

/// <summary>
/// Exige que a data informada não esteja no futuro, avaliada no relógio do
/// servidor em tempo Universal Coordenado (UTC).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotFutureDateAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date)
        {
            return ValidationResult.Success;
        }

        DateOnly todayUtc = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        if (date <= todayUtc)
        {
            return ValidationResult.Success;
        }

        string message = string.Format(
            CultureInfo.InvariantCulture,
            "O campo {0} não pode estar no futuro.",
            validationContext.DisplayName);

        List<string>? memberNames = validationContext.MemberName is { } memberName
            ? [memberName]
            : null;

        return new ValidationResult(message, memberNames);
    }
}
