namespace ExpenseHub.Api.Identity;

/// <summary>Tipos de claim customizados emitidos no token de acesso do ExpenseHub.</summary>
public static class ExpenseHubClaims
{
    /// <summary>Claim que carrega o nome de cada role atribuída ao usuário.</summary>
    public const string Role = "role";
}
