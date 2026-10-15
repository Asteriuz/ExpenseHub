using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>Dados enviados ao endpoint público de cadastro de usuário.</summary>
/// <remarks>O DTO não possui campo de role: o cadastro nunca promove ninguém.</remarks>
public sealed class RegisterRequest
{
    /// <summary>E-mail que identificará o usuário.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Senha do usuário, com no mínimo 8 caracteres.</summary>
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Usuário recém-cadastrado.</summary>
public sealed class RegisteredUserResponse
{
    /// <summary>Identificador gerado pelo servidor.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>E-mail cadastrado.</summary>
    public string Email { get; set; } = string.Empty;
}
