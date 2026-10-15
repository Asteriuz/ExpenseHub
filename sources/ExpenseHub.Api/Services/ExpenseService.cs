using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Implementa as regras de negócio dos reembolsos na camada de serviço: criação e
/// edição de rascunhos, envio para aprovação, aprovação, reprovação e consulta por
/// perfil, sempre com proprietário, ator e instantes UTC derivados do token e do
/// relógio do servidor, e histórico registrado na mesma operação lógica.
/// </summary>
public sealed class ExpenseService : IExpenseService
{
    private const decimal MinimumAmount = 0.01m;

    private const decimal MaximumAmount = 2147483647.00m;

    private const int MinimumDescriptionLength = 10;

    private const int MaximumDescriptionLength = 500;

    private const int MinimumJustificationLength = 10;

    private const int MaximumJustificationLength = 500;

    private readonly ExpenseHubDbContext _dbContext;

    private readonly IExpenseHistoryService _expenseHistoryService;

    private readonly TimeProvider _timeProvider;

    /// <summary>Inicializa o serviço com o contexto de dados, o serviço de histórico e o relógio do servidor.</summary>
    /// <param name="dbContext">Contexto do Entity Framework Core.</param>
    /// <param name="expenseHistoryService">Serviço de histórico, alimentado na mesma operação lógica.</param>
    /// <param name="timeProvider">Relógio do servidor, usado para todos os instantes UTC.</param>
    public ExpenseService(ExpenseHubDbContext dbContext, IExpenseHistoryService expenseHistoryService, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _expenseHistoryService = expenseHistoryService;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<CreateExpenseOutcome> CreateAsync(CreateExpenseRequest request, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> errors = await ValidateAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return CreateExpenseOutcome.Invalid(errors);
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        Expense expense = new()
        {
            OwnerId = ownerId,
            Description = request.Description,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            ExpenseCategoryId = request.ExpenseCategoryId,
            State = ExpenseState.Draft,
            CreatedAtUtc = nowUtc,
        };

        _dbContext.Expenses.Add(expense);
        _expenseHistoryService.RecordCreation(expense, ownerId, nowUtc);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return CreateExpenseOutcome.Created(ExpenseResponse.FromExpense(expense));
    }

    /// <inheritdoc />
    public async Task<UpdateExpenseOutcome> UpdateAsync(Guid id, UpdateExpenseRequest request, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Expense? expense = await _dbContext.Expenses
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (expense is null)
        {
            return UpdateExpenseOutcome.NotFound();
        }

        if (!string.Equals(expense.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return UpdateExpenseOutcome.Forbidden();
        }

        if (expense.State != ExpenseState.Draft)
        {
            return UpdateExpenseOutcome.NotDraft();
        }

        List<string> errors = await ValidateAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return UpdateExpenseOutcome.Invalid(errors);
        }

        List<string> changes = DescribeChanges(expense, request);
        if (changes.Count == 0)
        {
            return UpdateExpenseOutcome.Updated(ExpenseResponse.FromExpense(expense));
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        expense.Description = request.Description;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.UpdatedAtUtc = nowUtc;

        _expenseHistoryService.RecordDraftChanges(expense, ownerId, nowUtc, string.Join("; ", changes));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return UpdateExpenseOutcome.Updated(ExpenseResponse.FromExpense(expense));
    }

    /// <inheritdoc />
    public async Task<SubmitExpenseOutcome> SubmitAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        Expense? expense = await _dbContext.Expenses
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (expense is null)
        {
            return SubmitExpenseOutcome.NotFound();
        }

        if (!string.Equals(expense.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return SubmitExpenseOutcome.Forbidden();
        }

        if (expense.State != ExpenseState.Draft)
        {
            return SubmitExpenseOutcome.NotDraft();
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        expense.State = ExpenseState.Submitted;
        _expenseHistoryService.RecordTransition(expense, ownerId, nowUtc, ExpenseState.Draft, ExpenseState.Submitted);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return SubmitExpenseOutcome.Submitted(ExpenseResponse.FromExpense(expense));
    }

    /// <inheritdoc />
    public async Task<ApproveExpenseOutcome> ApproveAsync(Guid id, string actorId, CancellationToken cancellationToken)
    {
        Expense? expense = await _dbContext.Expenses
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (expense is null)
        {
            return ApproveExpenseOutcome.NotFound();
        }

        if (string.Equals(expense.OwnerId, actorId, StringComparison.Ordinal))
        {
            return ApproveExpenseOutcome.Forbidden();
        }

        if (expense.State != ExpenseState.Submitted)
        {
            return ApproveExpenseOutcome.NotSubmitted();
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        expense.State = ExpenseState.Approved;
        _expenseHistoryService.RecordTransition(expense, actorId, nowUtc, ExpenseState.Submitted, ExpenseState.Approved);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApproveExpenseOutcome.Approved(ExpenseResponse.FromExpense(expense));
    }

    /// <inheritdoc />
    public async Task<RejectExpenseOutcome> RejectAsync(Guid id, RejectExpenseRequest request, string actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> errors = ValidateJustification(request.Justification);
        if (errors.Count > 0)
        {
            return RejectExpenseOutcome.Invalid(errors);
        }

        Expense? expense = await _dbContext.Expenses
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (expense is null)
        {
            return RejectExpenseOutcome.NotFound();
        }

        if (string.Equals(expense.OwnerId, actorId, StringComparison.Ordinal))
        {
            return RejectExpenseOutcome.Forbidden();
        }

        if (expense.State != ExpenseState.Submitted)
        {
            return RejectExpenseOutcome.NotSubmitted();
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        expense.State = ExpenseState.Rejected;
        _expenseHistoryService.RecordTransition(expense, actorId, nowUtc, ExpenseState.Submitted, ExpenseState.Rejected, request.Justification);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RejectExpenseOutcome.Rejected(ExpenseResponse.FromExpense(expense));
    }

    /// <inheritdoc />
    public async Task<ListExpensesOutcome> ListAsync(IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken)
    {
        (bool isEmployee, bool isApprover, bool isFinance, bool isAuditor) = GetReadScopes(roles);
        if (!isEmployee && !isApprover && !isFinance && !isAuditor)
        {
            return ListExpensesOutcome.Forbidden();
        }

        List<Expense> expenses = await _dbContext.Expenses
            .Where(BuildReadScope(actorId, isEmployee, isApprover, isFinance, isAuditor))
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ThenBy(expense => expense.Id)
            .ToListAsync(cancellationToken);

        return ListExpensesOutcome.Listed(expenses.Select(ExpenseResponse.FromExpense).ToList());
    }

    /// <inheritdoc />
    public async Task<ExpenseDetailOutcome> GetByIdAsync(Guid id, IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken)
    {
        (bool isEmployee, bool isApprover, bool isFinance, bool isAuditor) = GetReadScopes(roles);
        if (!isEmployee && !isApprover && !isFinance && !isAuditor)
        {
            return ExpenseDetailOutcome.Forbidden();
        }

        Expense? expense = await _dbContext.Expenses
            .Where(BuildReadScope(actorId, isEmployee, isApprover, isFinance, isAuditor))
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return expense is null
            ? ExpenseDetailOutcome.NotFound()
            : ExpenseDetailOutcome.Found(ExpenseResponse.FromExpense(expense));
    }

    /// <summary>Determina os escopos de leitura concedidos por cada role do ator.</summary>
    /// <param name="roles">Roles do ator autenticado, derivadas do token de acesso.</param>
    /// <returns>Um indicador por role com acesso de leitura.</returns>
    private static (bool IsEmployee, bool IsApprover, bool IsFinance, bool IsAuditor) GetReadScopes(IReadOnlyList<string> roles)
    {
        return (
            IsEmployee: roles.Contains(Roles.Employee),
            IsApprover: roles.Contains(Roles.Approver),
            IsFinance: roles.Contains(Roles.Finance),
            IsAuditor: roles.Contains(Roles.Auditor));
    }

    /// <summary>
    /// Constrói o predicado de visibilidade por perfil, aplicado como filtro no
    /// IQueryable antes de materializar qualquer resultado.
    /// </summary>
    /// <param name="actorId">Identificador do ator autenticado.</param>
    /// <param name="isEmployee">Indica se o ator possui a role Employee.</param>
    /// <param name="isApprover">Indica se o ator possui a role Approver.</param>
    /// <param name="isFinance">Indica se o ator possui a role Finance.</param>
    /// <param name="isAuditor">Indica se o ator possui a role Auditor.</param>
    /// <returns>O predicado de visibilidade para consulta no banco.</returns>
    private static Expression<Func<Expense, bool>> BuildReadScope(string actorId, bool isEmployee, bool isApprover, bool isFinance, bool isAuditor)
    {
        return expense =>
            (isEmployee && expense.OwnerId == actorId) ||
            (isApprover && expense.State == ExpenseState.Submitted) ||
            (isFinance && (expense.State == ExpenseState.Approved || expense.State == ExpenseState.Paid)) ||
            isAuditor;
    }

    /// <summary>Descreve as alterações de rascunho no formato antes→depois.</summary>
    /// <param name="expense">Rascunho persistido.</param>
    /// <param name="request">Campos informados pelo cliente.</param>
    /// <returns>As alterações efetivas detectadas; vazia quando nada muda.</returns>
    private static List<string> DescribeChanges(Expense expense, UpdateExpenseRequest request)
    {
        List<string> changes = new();
        if (!string.Equals(expense.Description, request.Description, StringComparison.Ordinal))
        {
            changes.Add($"Descrição: \"{expense.Description}\" → \"{request.Description}\"");
        }

        if (expense.Amount != request.Amount)
        {
            changes.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Valor: {0:F2} → {1:F2}",
                expense.Amount,
                request.Amount));
        }

        if (expense.ExpenseDate != request.ExpenseDate)
        {
            changes.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Data da despesa: {0:yyyy-MM-dd} → {1:yyyy-MM-dd}",
                expense.ExpenseDate,
                request.ExpenseDate));
        }

        return changes;
    }

    /// <summary>Valida os campos de rascunho no relógio do servidor e no banco.</summary>
    /// <param name="request">Campos informados pelo cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>As mensagens de erro; vazia quando a entrada é válida.</returns>
    private async Task<List<string>> ValidateAsync(IExpenseDraftRequest request, CancellationToken cancellationToken)
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add("A descrição é obrigatória.");
        }
        else if (request.Description.Length < MinimumDescriptionLength || request.Description.Length > MaximumDescriptionLength)
        {
            errors.Add(string.Format(
                CultureInfo.InvariantCulture,
                "A descrição deve ter entre {0} e {1} caracteres.",
                MinimumDescriptionLength,
                MaximumDescriptionLength));
        }

        if (request.Amount < MinimumAmount || request.Amount > MaximumAmount)
        {
            errors.Add(string.Format(
                CultureInfo.InvariantCulture,
                "O valor deve estar entre {0} e {1}.",
                MinimumAmount,
                MaximumAmount));
        }

        if (request.ExpenseDate == default)
        {
            errors.Add("A data da despesa é obrigatória.");
        }
        else
        {
            DateOnly todayUtc = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            if (request.ExpenseDate > todayUtc)
            {
                errors.Add("A data da despesa não pode estar no futuro.");
            }
        }

        if (request.ExpenseCategoryId is { } categoryId
            && !await _dbContext.ExpenseCategories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            errors.Add("A categoria informada não existe.");
        }

        return errors;
    }

    /// <summary>Valida a justificativa da reprovação informada pelo cliente.</summary>
    /// <param name="justification">Justificativa informada.</param>
    /// <returns>As mensagens de erro; vazia quando a entrada é válida.</returns>
    private static List<string> ValidateJustification(string justification)
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(justification))
        {
            errors.Add("A justificativa é obrigatória.");
        }
        else if (justification.Length < MinimumJustificationLength || justification.Length > MaximumJustificationLength)
        {
            errors.Add(string.Format(
                CultureInfo.InvariantCulture,
                "A justificativa deve ter entre {0} e {1} caracteres.",
                MinimumJustificationLength,
                MaximumJustificationLength));
        }

        return errors;
    }
}
