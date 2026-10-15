using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseHub.Api.Identity;

/// <summary>Emite tokens de acesso JWT assinados com HMAC-SHA256.</summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    /// <summary>Inicializa o serviço com as opções de JWT configuradas.</summary>
    /// <param name="options">Opções de emissão e validação do token.</param>
    public JwtTokenService(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(IdentityUser user, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        DateTime issuedAtUtc = DateTime.UtcNow;
        DateTime expiresAtUtc = issuedAtUtc.AddMinutes(_options.LifetimeMinutes);

        List<Claim> claims = new()
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture)),
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        foreach (string role in roles)
        {
            claims.Add(new Claim(ExpenseHubClaims.Role, role));
        }

        SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(_options.SigningKey));
        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        string value = new JwtSecurityTokenHandler().WriteToken(token);
        return (value, expiresAtUtc);
    }
}
