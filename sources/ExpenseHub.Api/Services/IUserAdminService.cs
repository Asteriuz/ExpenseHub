using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>Estado do resultado de uma substituição de roles.</summary>
public enum ReplaceRolesStatus
{
    /// <summary>Roles substituídas com sucesso.</summary>
    Updated,

    /// <summary>Usuário alvo inexistente.</summary>
    UserNotFound,

    /// <summary>Pelo menos uma role informada não é conhecida.</summary>
    UnknownRoles,

    /// <summary>O Admin tentou remover a própria role Admin.</summary>
    SelfAdminRemoval,
}

/// <summary>Resultado da substituição das roles de um usuário.</summary>
/// <param name="Status">Estado do resultado.</param>
/// <param name="Roles">Roles vigentes quando o status é <see cref="ReplaceRolesStatus.Updated"/>.</param>
/// <param name="UnknownRoles">Nomes rejeitados quando o status é <see cref="ReplaceRolesStatus.UnknownRoles"/>.</param>
public sealed record ReplaceRolesOutcome(
    ReplaceRolesStatus Status,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> UnknownRoles)
{
    /// <summary>Cria o resultado de uma substituição bem-sucedida.</summary>
    /// <param name="roles">Roles vigentes após a substituição.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static ReplaceRolesOutcome Updated(IReadOnlyList<string> roles) => new(ReplaceRolesStatus.Updated, roles, []);

    /// <summary>Cria o resultado para usuário inexistente.</summary>
    /// <returns>O resultado de usuário inexistente.</returns>
    public static ReplaceRolesOutcome UserNotFound() => new(ReplaceRolesStatus.UserNotFound, [], []);

    /// <summary>Cria o resultado para roles desconhecidas.</summary>
    /// <param name="unknownRoles">Nomes de role fora da whitelist.</param>
    /// <returns>O resultado de roles desconhecidas.</returns>
    public static ReplaceRolesOutcome WithUnknownRoles(IReadOnlyList<string> unknownRoles) => new(ReplaceRolesStatus.UnknownRoles, [], unknownRoles);

    /// <summary>Cria o resultado para remoção da própria role Admin.</summary>
    /// <returns>O resultado de remoção proibida da própria role Admin.</returns>
    public static ReplaceRolesOutcome SelfAdminRemoval() => new(ReplaceRolesStatus.SelfAdminRemoval, [], []);
}

/// <summary>Lista usuários e administra roles; operações exclusivas da role Admin.</summary>
public interface IUserAdminService
{
    /// <summary>Lista todos os usuários com as respectivas roles.</summary>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Os usuários listados com suas roles.</returns>
    Task<IReadOnlyList<AdminUserListItem>> ListUsersAsync(CancellationToken cancellationToken);

    /// <summary>Substitui as roles de um usuário pela lista informada.</summary>
    /// <param name="targetUserId">Identificador do usuário alvo.</param>
    /// <param name="requestedRoles">Nomes de role desejados.</param>
    /// <param name="actorId">Identificador do Admin autenticado, derivado do token.</param>
    /// <returns>O resultado da substituição.</returns>
    Task<ReplaceRolesOutcome> ReplaceRolesAsync(string targetUserId, IEnumerable<string> requestedRoles, string actorId);
}
