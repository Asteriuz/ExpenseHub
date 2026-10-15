using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>Resultado do cadastro de um usuário.</summary>
/// <param name="Succeeded">Indica se o cadastro foi criado.</param>
/// <param name="UserId">Identificador do usuário quando o cadastro foi criado.</param>
/// <param name="Email">E-mail cadastrado quando o cadastro foi criado.</param>
/// <param name="Errors">Mensagens de erro quando o cadastro foi rejeitado.</param>
public sealed record RegistrationOutcome(
    bool Succeeded,
    string? UserId,
    string? Email,
    IReadOnlyList<string> Errors)
{
    /// <summary>Cria o resultado de um cadastro criado com sucesso.</summary>
    /// <param name="userId">Identificador do usuário criado.</param>
    /// <param name="email">E-mail cadastrado.</param>
    /// <returns>O resultado de sucesso.</returns>
    public static RegistrationOutcome Created(string userId, string email) => new(true, userId, email, []);

    /// <summary>Cria o resultado de um cadastro rejeitado.</summary>
    /// <param name="errors">Mensagens de erro da rejeição.</param>
    /// <returns>O resultado de rejeição.</returns>
    public static RegistrationOutcome Invalid(IReadOnlyList<string> errors) => new(false, null, null, errors);
}

/// <summary>Cadastra usuários por HTTP sem conceder qualquer role.</summary>
public interface IUserRegistrationService
{
    /// <summary>Cria um usuário a partir de um DTO validado, sem atribuir roles.</summary>
    /// <param name="request">Dados validados do novo usuário.</param>
    /// <returns>O resultado do cadastro.</returns>
    Task<RegistrationOutcome> RegisterAsync(RegisterRequest request);
}
