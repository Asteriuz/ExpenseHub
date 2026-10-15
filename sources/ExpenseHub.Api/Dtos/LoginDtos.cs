using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>Credenciais enviadas ao endpoint público de login.</summary>
public sealed class LoginRequest
{
    /// <summary>E-mail que identifica o usuário.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Senha do usuário.</summary>
    [Required]
    [StringLength(256)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Token de acesso emitido após uma autenticação bem-sucedida.</summary>
public sealed class LoginResponse
{
    /// <summary>Token JWT assinado.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Tipo do token; sempre <c>Bearer</c>.</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Instante UTC em que o token expira.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
