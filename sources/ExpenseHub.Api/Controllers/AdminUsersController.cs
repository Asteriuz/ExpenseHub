using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Identity;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>Operações de administração de usuários, exclusivas da role Admin.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserAdminService _userAdminService;

    /// <summary>Inicializa o controller com o serviço de administração de usuários.</summary>
    /// <param name="userAdminService">Serviço de administração de usuários e roles.</param>
    public AdminUsersController(IUserAdminService userAdminService)
    {
        _userAdminService = userAdminService;
    }

    /// <summary>Lista todos os usuários com as respectivas roles.</summary>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>A lista de usuários; autenticado sem a role Admin retorna <c>403</c>.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminUserListItem> users = await _userAdminService.ListUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>Substitui as roles de um usuário pela lista informada.</summary>
    /// <param name="id">Identificador do usuário alvo.</param>
    /// <param name="request">Lista de nomes de role desejados.</param>
    /// <returns>As roles vigentes após a alteração.</returns>
    [HttpPut("{id}/roles")]
    [ProducesResponseType(typeof(ReplaceRolesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceRoles(string id, [FromBody] ReplaceRolesRequest request)
    {
        string actorId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
        ReplaceRolesOutcome outcome = await _userAdminService.ReplaceRolesAsync(id, request.Roles, actorId);

        IActionResult result = outcome.Status switch
        {
            ReplaceRolesStatus.Updated => Ok(new ReplaceRolesResponse { Id = id, Roles = outcome.Roles }),
            ReplaceRolesStatus.UserNotFound => Problem(
                detail: "Usuário inexistente.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso não encontrado"),
            ReplaceRolesStatus.UnknownRoles => Problem(
                detail: $"Roles desconhecidas: {string.Join(", ", outcome.UnknownRoles)}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Entrada inválida"),
            ReplaceRolesStatus.SelfAdminRemoval => Problem(
                detail: "O Admin não pode remover a própria role 'Admin'.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acesso negado"),
            _ => throw new InvalidOperationException($"Estado inesperado de substituição de roles: {outcome.Status}."),
        };

        return result;
    }
}
