using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>Usuário listado para o Admin, com as respectivas roles.</summary>
public sealed class AdminUserListItem
{
    /// <summary>Identificador do usuário.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>E-mail do usuário.</summary>
    public string? Email { get; set; }

    /// <summary>Nome de usuário.</summary>
    public string? UserName { get; set; }

    /// <summary>Roles atribuídas ao usuário.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];
}

/// <summary>Solicitação de substituição das roles de um usuário.</summary>
public sealed class ReplaceRolesRequest
{
    /// <summary>Nomes das roles desejadas; a lista completa substitui as roles atuais.</summary>
    [Required]
    public IList<string> Roles { get; set; } = [];
}

/// <summary>Resultado da substituição das roles de um usuário.</summary>
public sealed class ReplaceRolesResponse
{
    /// <summary>Identificador do usuário alterado.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Roles vigentes após a alteração.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];
}
