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
/// Endpoints de reembolsos. Rotas de escrita exigem a role Employee; as rotas de
/// leitura aplicam a visibilidade por perfil na camada de serviço. Proprietário,
/// ator, estado e instantes são sempre derivados do token e do relógio do servidor.
/// </summary>
[ApiController]
[Authorize]
[Route("api/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    /// <summary>Inicializa o controller com o serviço de reembolsos.</summary>
    /// <param name="expenseService">Serviço de regras de negócio dos reembolsos.</param>
    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>Cria um rascunho de reembolso em estado Draft para o ator autenticado.</summary>
    /// <param name="request">Campos informados pelo cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O reembolso criado; entrada inválida retorna <c>400</c>.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Employee)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        CreateExpenseOutcome outcome = await _expenseService.CreateAsync(request, GetActorId(), cancellationToken);
        return outcome.Status switch
        {
            CreateExpenseStatus.Created => CreatedAtAction(nameof(GetById), new { id = outcome.Expense!.Id }, outcome.Expense),
            CreateExpenseStatus.Invalid => InvalidEntry(outcome.Errors),
            _ => throw new InvalidOperationException($"Estado inesperado de criação de reembolso: {outcome.Status}."),
        };
    }

    /// <summary>Edita um rascunho próprio em estado Draft, sem alterar o estado.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Campos informados pelo cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O rascunho atualizado.</returns>
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
        UpdateExpenseOutcome outcome = await _expenseService.UpdateAsync(id, request, GetActorId(), cancellationToken);
        return outcome.Status switch
        {
            UpdateExpenseStatus.Updated => Ok(outcome.Expense),
            UpdateExpenseStatus.Invalid => InvalidEntry(outcome.Errors),
            UpdateExpenseStatus.Forbidden => AccessDenied(),
            UpdateExpenseStatus.NotFound => ExpenseNotFound(),
            UpdateExpenseStatus.NotDraft => StateConflict(),
            _ => throw new InvalidOperationException($"Estado inesperado de edição de reembolso: {outcome.Status}."),
        };
    }

    /// <summary>Envia um rascunho próprio, transicionando de Draft para Submitted.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O reembolso em estado Submitted.</returns>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = Roles.Employee)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        SubmitExpenseOutcome outcome = await _expenseService.SubmitAsync(id, GetActorId(), cancellationToken);
        return outcome.Status switch
        {
            SubmitExpenseStatus.Submitted => Ok(outcome.Expense),
            SubmitExpenseStatus.Forbidden => AccessDenied(),
            SubmitExpenseStatus.NotFound => ExpenseNotFound(),
            SubmitExpenseStatus.NotDraft => StateConflict(),
            _ => throw new InvalidOperationException($"Estado inesperado de envio de reembolso: {outcome.Status}."),
        };
    }

    /// <summary>Aprova um reembolso de outro usuário, transicionando de Submitted para Approved.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O reembolso em estado Approved.</returns>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Roles.Approver)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        ApproveExpenseOutcome outcome = await _expenseService.ApproveAsync(id, GetActorId(), cancellationToken);
        return outcome.Status switch
        {
            ApproveExpenseStatus.Approved => Ok(outcome.Expense),
            ApproveExpenseStatus.Forbidden => AccessDenied(),
            ApproveExpenseStatus.NotFound => ExpenseNotFound(),
            ApproveExpenseStatus.NotSubmitted => StateConflict(),
            _ => throw new InvalidOperationException($"Estado inesperado de aprovação de reembolso: {outcome.Status}."),
        };
    }

    /// <summary>Reprova um reembolso de outro usuário, transicionando de Submitted para Rejected.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Justificativa da reprovação informada pelo cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O reembolso em estado Rejected.</returns>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.Approver)]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectExpenseRequest request, CancellationToken cancellationToken)
    {
        RejectExpenseOutcome outcome = await _expenseService.RejectAsync(id, request, GetActorId(), cancellationToken);
        return outcome.Status switch
        {
            RejectExpenseStatus.Rejected => Ok(outcome.Expense),
            RejectExpenseStatus.Invalid => InvalidEntry(outcome.Errors),
            RejectExpenseStatus.Forbidden => AccessDenied(),
            RejectExpenseStatus.NotFound => ExpenseNotFound(),
            RejectExpenseStatus.NotSubmitted => StateConflict(),
            _ => throw new InvalidOperationException($"Estado inesperado de reprovação de reembolso: {outcome.Status}."),
        };
    }

    /// <summary>Lista os reembolsos no escopo visível ao ator conforme suas roles.</summary>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Os reembolsos visíveis; autenticado sem role funcional retorna <c>403</c>.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        ListExpensesOutcome outcome = await _expenseService.ListAsync(GetActorRoles(), GetActorId(), cancellationToken);
        return outcome.Status switch
        {
            ListExpensesStatus.Listed => Ok(outcome.Items),
            ListExpensesStatus.Forbidden => AccessDenied(),
            _ => throw new InvalidOperationException($"Estado inesperado de listagem de reembolsos: {outcome.Status}."),
        };
    }

    /// <summary>Consulta o detalhe de um reembolso dentro do escopo de leitura do ator.</summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>O reembolso; recurso fora do escopo retorna <c>404</c> sem revelar existência.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        ExpenseDetailOutcome outcome = await _expenseService.GetByIdAsync(id, GetActorRoles(), GetActorId(), cancellationToken);
        return outcome.Status switch
        {
            ExpenseDetailStatus.Found => Ok(outcome.Expense),
            ExpenseDetailStatus.Forbidden => AccessDenied(),
            ExpenseDetailStatus.NotFound => ExpenseNotFound(),
            _ => throw new InvalidOperationException($"Estado inesperado de consulta de reembolso: {outcome.Status}."),
        };
    }

    /// <summary>Obtém o identificador do ator autenticado, derivado do token de acesso.</summary>
    /// <returns>O identificador do usuário autenticado.</returns>
    private string GetActorId()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
    }

    /// <summary>Obtém as roles do ator autenticado, derivadas do token de acesso.</summary>
    /// <returns>A lista de roles atribuídas ao token.</returns>
    private IReadOnlyList<string> GetActorRoles()
    {
        return User.FindAll(ExpenseHubClaims.Role)
            .Select(claim => claim.Value)
            .ToList();
    }

    /// <summary>Produz a resposta de entrada inválida com as mensagens de validação.</summary>
    /// <param name="errors">Mensagens de validação.</param>
    /// <returns>A resposta <c>400</c> com ProblemDetails.</returns>
    private IActionResult InvalidEntry(IReadOnlyList<string> errors)
    {
        return Problem(
            detail: string.Join("; ", errors),
            statusCode: StatusCodes.Status400BadRequest,
            title: "Entrada inválida");
    }

    /// <summary>Produz a resposta de acesso negado para o ator autenticado.</summary>
    /// <returns>A resposta <c>403</c> com ProblemDetails.</returns>
    private IActionResult AccessDenied()
    {
        return Problem(
            detail: "O usuário autenticado não possui permissão para esta operação sobre o reembolso.",
            statusCode: StatusCodes.Status403Forbidden,
            title: "Acesso negado");
    }

    /// <summary>Produz a resposta de recurso inexistente ou fora do escopo de leitura.</summary>
    /// <returns>A resposta <c>404</c> com ProblemDetails.</returns>
    private IActionResult ExpenseNotFound()
    {
        return Problem(
            detail: "Reembolso inexistente ou fora do escopo de leitura do usuário autenticado.",
            statusCode: StatusCodes.Status404NotFound,
            title: "Recurso não encontrado");
    }

    /// <summary>Produz a resposta de transição incompatível ou repetida.</summary>
    /// <returns>A resposta <c>409</c> com ProblemDetails.</returns>
    private IActionResult StateConflict()
    {
        return Problem(
            detail: "A operação é incompatível com o estado atual do reembolso ou já foi realizada.",
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflito de estado");
    }
}
