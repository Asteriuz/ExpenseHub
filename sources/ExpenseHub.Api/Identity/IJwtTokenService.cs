using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Identity;

/// <summary>Serviço responsável por emitir tokens de acesso JWT.</summary>
public interface IJwtTokenService
{
    /// <summary>Cria um token de acesso para o usuário informado.</summary>
    /// <param name="user">Usuário autenticado.</param>
    /// <param name="roles">Roles atribuídas ao usuário, incluídas como claims.</param>
    /// <returns>Token assinado e instante UTC de expiração.</returns>
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(IdentityUser user, IEnumerable<string> roles);
}
