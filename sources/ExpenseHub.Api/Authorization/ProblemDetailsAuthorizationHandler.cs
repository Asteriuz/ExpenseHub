using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Authorization;

/// <summary>
/// Produz respostas <see cref="ProblemDetails"/> para falhas de autorização,
/// diferenciando <c>401</c> (não autenticado) de <c>403</c> (autenticado sem permissão).
/// </summary>
public sealed class ProblemDetailsAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Não autenticado",
                "É necessário um token de acesso válido para acessar este recurso.");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Acesso negado",
                "O usuário autenticado não possui permissão para esta operação.");
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, int statusCode, string title, string detail)
    {
        ProblemDetails problem = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            problem,
            (JsonSerializerOptions?)null,
            "application/problem+json",
            context.RequestAborted);
    }
}
