using System.Collections.Generic;

namespace ExpenseHub.Api.Identity;

/// <summary>Nomes das roles conhecidas pela aplicação.</summary>
public static class Roles
{
    /// <summary>Administra usuários e roles; não possui acesso funcional aos reembolsos.</summary>
    public const string Admin = "Admin";

    /// <summary>Cria, edita, envia e consulta os próprios reembolsos.</summary>
    public const string Employee = "Employee";

    /// <summary>Aprova ou reprova reembolsos enviados que não sejam próprios.</summary>
    public const string Approver = "Approver";

    /// <summary>Registra o pagamento de reembolsos aprovados que não sejam próprios.</summary>
    public const string Finance = "Finance";

    /// <summary>Consulta todos os reembolsos e históricos, sem escrita.</summary>
    public const string Auditor = "Auditor";

    /// <summary>Todas as roles que devem existir na aplicação.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Admin, Employee, Approver, Finance, Auditor };

    /// <summary>Roles com acesso de leitura às listagens e detalhes de reembolsos; Admin fica fora por não ter acesso funcional.</summary>
    public const string Query = $"{Employee},{Approver},{Finance},{Auditor}";
}
