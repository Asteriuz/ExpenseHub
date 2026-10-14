using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Registro do pagamento de um reembolso aprovado.</summary>
public class PaymentRecord
{
    /// <summary>Identificador gerado pelo servidor.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador do reembolso pago; um reembolso possui no máximo um pagamento.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>Reembolso pago.</summary>
    public Expense? Expense { get; set; }

    /// <summary>Identificador do usuário que registrou o pagamento, derivado do token.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Instante UTC do pagamento, definido pelo servidor.</summary>
    public DateTime PaidAtUtc { get; set; }
}
