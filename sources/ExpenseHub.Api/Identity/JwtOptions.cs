using System;
using System.Text;

namespace ExpenseHub.Api.Identity;

/// <summary>Configuração do token de acesso JWT emitido pela aplicação.</summary>
public sealed class JwtOptions
{
    /// <summary>Nome da seção de configuração que contém os valores de JWT.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Emissor do token; validado na autenticação.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Destinatário do token; validado na autenticação.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Chave simétrica de assinatura. Deve ser fornecida por user-secrets ou
    /// variável de ambiente e nunca versionada no repositório.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Tempo de vida do token em minutos.</summary>
    public int LifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Valida os valores obrigatórios da configuração, lançando exceção quando a
    /// aplicação está mal configurada.
    /// </summary>
    /// <exception cref="InvalidOperationException">Lançada quando um valor obrigatório está ausente ou inválido.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("A configuração 'Jwt:Issuer' é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("A configuração 'Jwt:Audience' é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                "A configuração 'Jwt:SigningKey' é obrigatória e deve vir de user-secrets ou variável de ambiente.");
        }

        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "A configuração 'Jwt:SigningKey' deve ter ao menos 32 bytes para assinatura HMAC-SHA256.");
        }

        if (LifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("A configuração 'Jwt:LifetimeMinutes' deve ser positiva.");
        }
    }
}
