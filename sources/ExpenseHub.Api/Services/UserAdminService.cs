using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Services;

/// <summary>Administra usuários e roles por meio do ASP.NET Core Identity.</summary>
public sealed class UserAdminService : IUserAdminService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ExpenseHubDbContext _dbContext;

    /// <summary>Inicializa o serviço com o gerenciador de usuários e o contexto de dados.</summary>
    /// <param name="userManager">Gerenciador de usuários do Identity.</param>
    /// <param name="dbContext">Contexto do Entity Framework Core.</param>
    public UserAdminService(UserManager<IdentityUser> userManager, ExpenseHubDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminUserListItem>> ListUsersAsync(CancellationToken cancellationToken)
    {
        List<AdminUserListItem> users = await _dbContext.Users
            .OrderBy(user => user.Email)
            .Select(user => new AdminUserListItem
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                Roles = _dbContext.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(_dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Name!)
                    .OrderBy(roleName => roleName)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return users;
    }

    /// <inheritdoc />
    public async Task<ReplaceRolesOutcome> ReplaceRolesAsync(string targetUserId, IEnumerable<string> requestedRoles, string actorId)
    {
        ArgumentNullException.ThrowIfNull(requestedRoles);

        Dictionary<string, string> canonicalRolesByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (string knownRole in Roles.All)
        {
            canonicalRolesByName.Add(knownRole, knownRole);
        }

        List<string> desiredRoles = requestedRoles
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Select(roleName => roleName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> unknownRoles = desiredRoles
            .Where(roleName => !canonicalRolesByName.ContainsKey(roleName))
            .ToList();

        if (unknownRoles.Count > 0)
        {
            return ReplaceRolesOutcome.WithUnknownRoles(unknownRoles);
        }

        List<string> desiredCanonicalRoles = desiredRoles
            .Select(roleName => canonicalRolesByName[roleName])
            .ToList();

        IdentityUser? user = await _userManager.FindByIdAsync(targetUserId);
        if (user is null)
        {
            return ReplaceRolesOutcome.UserNotFound();
        }

        if (string.Equals(user.Id, actorId, StringComparison.Ordinal) && !desiredCanonicalRoles.Contains(Roles.Admin))
        {
            return ReplaceRolesOutcome.SelfAdminRemoval();
        }

        IList<string> currentRoles = await _userManager.GetRolesAsync(user);
        List<string> rolesToAdd = desiredCanonicalRoles.Except(currentRoles, StringComparer.Ordinal).ToList();
        List<string> rolesToRemove = currentRoles.Except(desiredCanonicalRoles, StringComparer.Ordinal).ToList();

        if (rolesToAdd.Count > 0)
        {
            EnsureSucceeded(await _userManager.AddToRolesAsync(user, rolesToAdd), "adicionar roles");
        }

        if (rolesToRemove.Count > 0)
        {
            EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, rolesToRemove), "remover roles");
        }

        IList<string> updatedRoles = await _userManager.GetRolesAsync(user);
        List<string> orderedRoles = updatedRoles.OrderBy(roleName => roleName, StringComparer.Ordinal).ToList();
        return ReplaceRolesOutcome.Updated(orderedRoles);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Não foi possível {operation}: {errors}");
    }
}
