using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
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

/// <summary>
/// Operações de reembolso: rascunhos exclusivos da role Employee e consultas com
/// visibilidade definida pela matriz de autorização, aplicada na camada de serviço.
/// </summary>
[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    /// <summary>Inicializa o controller com o serviço de reembolsos.</summary>
    /// <param name="expenseService">Serviço de criação, edição, envio e consulta de reembolsos.</param>
    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>Cria um rascunho de reembolso em estado Draft para o Employee autenticado.</summary>
    /// <param name="request">Dados validados do rascunho.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O rascunho criado; entrada inválida retorna <c>400</c>.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Employee)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        string ownerId = GetActorId();
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
    [Authorize(Roles = Roles.Employee)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        string ownerId = GetActorId();
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

    /// <summary>Envia um rascunho próprio para aprovação, transicionando de Draft para Submitted.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O reembolso enviado; os casos negativos retornam <c>403</c>, <c>404</c> ou <c>409</c>.</returns>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = Roles.Employee)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        string ownerId = GetActorId();
        SubmitExpenseOutcome outcome = await _expenseService.SubmitAsync(id, ownerId, cancellationToken);

        return outcome.Status switch
        {
            SubmitExpenseStatus.Submitted => Ok(outcome.Expense),
            SubmitExpenseStatus.NotFound => Problem(
                detail: "Reembolso inexistente.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso não encontrado"),
            SubmitExpenseStatus.Forbidden => Problem(
                detail: "Somente o proprietário pode enviar o rascunho.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acesso negado"),
            SubmitExpenseStatus.NotDraft => Problem(
                detail: "Somente rascunhos em estado Draft podem ser enviados.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Transição incompatível"),
            _ => throw new InvalidOperationException($"Estado inesperado de envio de rascunho: {outcome.Status}."),
        };
    }

    /// <summary>Lista os reembolsos visíveis ao ator conforme a união das roles do token.</summary>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Employee vê os próprios; Approver vê os em Submitted; Finance vê os em Approved e Paid; Auditor vê todos.</returns>
    [HttpGet]
    [Authorize(Roles = Roles.Query)]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        ListExpensesOutcome outcome = await _expenseService.ListAsync(GetRoles(), GetActorId(), cancellationToken);

        return outcome.Status switch
        {
            ListExpensesStatus.Listed => Ok(outcome.Items),
            ListExpensesStatus.Forbidden => Problem(
                detail: "O usuário autenticado não possui role funcional para consultar reembolsos.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acesso negado"),
            _ => throw new InvalidOperationException($"Estado inesperado de listagem de reembolsos: {outcome.Status}."),
        };
    }

    /// <summary>Consulta o detalhe de um reembolso respeitando o escopo de leitura do ator.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O reembolso visível; recurso inexistente ou fora do escopo retorna <c>404</c>.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Query)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        ExpenseDetailOutcome outcome = await _expenseService.GetByIdAsync(id, GetRoles(), GetActorId(), cancellationToken);

        return outcome.Status switch
        {
            ExpenseDetailStatus.Found => Ok(outcome.Expense),
            ExpenseDetailStatus.NotFound => Problem(
                detail: "Reembolso inexistente ou fora do escopo de leitura.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso não encontrado"),
            ExpenseDetailStatus.Forbidden => Problem(
                detail: "O usuário autenticado não possui role funcional para consultar reembolsos.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acesso negado"),
            _ => throw new InvalidOperationException($"Estado inesperado de consulta de reembolso: {outcome.Status}."),
        };
    }

    private string GetActorId()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
    }

    private List<string> GetRoles()
    {
        return [.. User.FindAll(ExpenseHubClaims.Role).Select(claim => claim.Value)];
    }
}
