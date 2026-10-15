using System;
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

/// <summary>Operações de rascunho de reembolso, exclusivas da role Employee.</summary>
[ApiController]
[Authorize(Roles = Roles.Employee)]
[Route("api/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    /// <summary>Inicializa o controller com o serviço de rascunhos de reembolso.</summary>
    /// <param name="expenseService">Serviço de criação e edição de rascunhos.</param>
    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>Cria um rascunho de reembolso em estado Draft para o Employee autenticado.</summary>
    /// <param name="request">Dados validados do rascunho.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O rascunho criado; entrada inválida retorna <c>400</c>.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        string ownerId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
        CreateExpenseOutcome outcome = await _expenseService.CreateAsync(request, ownerId, cancellationToken);

        return outcome.Status switch
        {
            CreateExpenseStatus.Created => StatusCode(StatusCodes.Status201Created, outcome.Expense),
            CreateExpenseStatus.Invalid => Problem(
                detail: string.Join("; ", outcome.Errors),
                statusCode: StatusCodes.Status400BadRequest,
                title: "Entrada inválida"),
            _ => throw new InvalidOperationException($"Estado inesperado de criação de rascunho: {outcome.Status}."),
        };
    }

    /// <summary>Edita um rascunho de reembolso próprio, mantendo o estado Draft.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Dados validados da edição.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O rascunho atualizado; os casos negativos retornam <c>400</c>, <c>403</c>, <c>404</c> ou <c>409</c>.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        string ownerId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
        UpdateExpenseOutcome outcome = await _expenseService.UpdateAsync(id, request, ownerId, cancellationToken);

        return outcome.Status switch
        {
            UpdateExpenseStatus.Updated => Ok(outcome.Expense),
            UpdateExpenseStatus.NotFound => Problem(
                detail: "Reembolso inexistente.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso não encontrado"),
            UpdateExpenseStatus.Forbidden => Problem(
                detail: "Somente o proprietário pode editar o rascunho.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acesso negado"),
            UpdateExpenseStatus.NotDraft => Problem(
                detail: "Somente rascunhos em estado Draft podem ser editados.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Transição incompatível"),
            UpdateExpenseStatus.Invalid => Problem(
                detail: string.Join("; ", outcome.Errors),
                statusCode: StatusCodes.Status400BadRequest,
                title: "Entrada inválida"),
            _ => throw new InvalidOperationException($"Estado inesperado de edição de rascunho: {outcome.Status}."),
        };
    }
}
