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
/// Cria, edita e envia rascunhos de reembolso e consulta reembolsos, aplicando as regras
/// de campo, ownership, estado e visibilidade da matriz de autorização na camada de
/// serviço, com histórico registrado na mesma operação lógica de persistência.
/// </summary>
public sealed class ExpenseService : IExpenseService
{
    private const decimal MinAmount = 0.01m;

    private const decimal MaxAmount = 2147483647.00m;

    private const int MinDescriptionLength = 10;

    private const int MaxDescriptionLength = 500;

    private const string CreateAction = "Criar";

    private const string EditAction = "Editar";

    private const string SubmitAction = "Enviar";

    private const string NoCategoryLabel = "(sem categoria)";

    private readonly ExpenseHubDbContext _dbContext;

    private readonly TimeProvider _timeProvider;

    /// <summary>Inicializa o serviço com o contexto de dados e o relógio do servidor.</summary>
    /// <param name="dbContext">Contexto do Entity Framework Core.</param>
    /// <param name="timeProvider">Relógio do servidor, fonte exclusiva dos instantes UTC.</param>
    public ExpenseService(ExpenseHubDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<CreateExpenseOutcome> CreateAsync(CreateExpenseRequest request, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        List<string> errors = ValidateInput(request.Description, request.Amount, request.ExpenseDate);
        await EnsureCategoryExistsAsync(request.ExpenseCategoryId, errors, cancellationToken);
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
            State = ExpenseState.Draft,
            ExpenseCategoryId = request.ExpenseCategoryId,
            CreatedAtUtc = nowUtc,
        };

        _dbContext.Expenses.Add(expense);
        AddHistory(expense, CreateAction, ownerId, nowUtc, previousState: null, newState: ExpenseState.Draft, draftChangesSummary: null);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreateExpenseOutcome.Created(ToResponse(expense));
    }

    /// <inheritdoc />
    public async Task<UpdateExpenseOutcome> UpdateAsync(Guid id, UpdateExpenseRequest request, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

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

        List<string> errors = ValidateInput(request.Description, request.Amount, request.ExpenseDate);
        await EnsureCategoryExistsAsync(request.ExpenseCategoryId, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return UpdateExpenseOutcome.Invalid(errors);
        }

        List<string> changes = CollectChanges(expense, request);
        if (changes.Count > 0)
        {
            DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            expense.Description = request.Description;
            expense.Amount = request.Amount;
            expense.ExpenseDate = request.ExpenseDate;
            expense.ExpenseCategoryId = request.ExpenseCategoryId;
            expense.UpdatedAtUtc = nowUtc;

            AddHistory(expense, EditAction, ownerId, nowUtc, previousState: ExpenseState.Draft, newState: null, draftChangesSummary: string.Join("; ", changes));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return UpdateExpenseOutcome.Updated(ToResponse(expense));
    }

    /// <inheritdoc />
    public async Task<SubmitExpenseOutcome> SubmitAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

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
        AddHistory(expense, SubmitAction, ownerId, nowUtc, previousState: ExpenseState.Draft, newState: ExpenseState.Submitted, draftChangesSummary: null);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return SubmitExpenseOutcome.Submitted(ToResponse(expense));
    }

    /// <inheritdoc />
    public async Task<ListExpensesOutcome> ListAsync(IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        if (!HasAnyFunctionalRole(roles))
        {
            return ListExpensesOutcome.Forbidden();
        }

        List<Expense> visible = await _dbContext.Expenses
            .Where(BuildVisibilityExpression(roles, actorId))
            .OrderBy(expense => expense.CreatedAtUtc)
            .ThenBy(expense => expense.Id)
            .ToListAsync(cancellationToken);

        return ListExpensesOutcome.Listed(visible.Select(ToResponse).ToList());
    }

    /// <inheritdoc />
    public async Task<ExpenseDetailOutcome> GetByIdAsync(Guid id, IReadOnlyList<string> roles, string actorId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        if (!HasAnyFunctionalRole(roles))
        {
            return ExpenseDetailOutcome.Forbidden();
        }

        Expense? expense = await _dbContext.Expenses
            .Where(BuildVisibilityExpression(roles, actorId))
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (expense is null)
        {
            return ExpenseDetailOutcome.NotFound();
        }

        return ExpenseDetailOutcome.Found(ToResponse(expense));
    }

    private static bool HasAnyFunctionalRole(IReadOnlyList<string>? roles)
    {
        return ContainsRole(roles, Roles.Employee)
            || ContainsRole(roles, Roles.Approver)
            || ContainsRole(roles, Roles.Finance)
            || ContainsRole(roles, Roles.Auditor);
    }

    private static bool ContainsRole(IReadOnlyList<string>? roles, string role)
    {
        if (roles is null)
        {
            return false;
        }

        for (int index = 0; index < roles.Count; index++)
        {
            if (string.Equals(roles[index], role, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Expression<Func<Expense, bool>> BuildVisibilityExpression(IReadOnlyList<string>? roles, string actorId)
    {
        bool isEmployee = ContainsRole(roles, Roles.Employee);
        bool isApprover = ContainsRole(roles, Roles.Approver);
        bool isFinance = ContainsRole(roles, Roles.Finance);
        bool isAuditor = ContainsRole(roles, Roles.Auditor);

        return expense =>
            (isEmployee && expense.OwnerId == actorId)
            || (isApprover && expense.State == ExpenseState.Submitted)
            || (isFinance && (expense.State == ExpenseState.Approved || expense.State == ExpenseState.Paid))
            || isAuditor;
    }

    private List<string> ValidateInput(string description, decimal amount, DateOnly expenseDate)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(description))
        {
            errors.Add("A descrição é obrigatória.");
        }
        else if (description.Length < MinDescriptionLength || description.Length > MaxDescriptionLength)
        {
            errors.Add("A descrição deve ter entre 10 e 500 caracteres.");
        }

        if (amount < MinAmount || amount > MaxAmount)
        {
            errors.Add("O valor deve estar entre 0,01 e 2.147.483.647,00.");
        }

        DateOnly todayUtc = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (expenseDate == default)
        {
            errors.Add("A data da despesa é obrigatória.");
        }
        else if (expenseDate > todayUtc)
        {
            errors.Add("A data da despesa não pode estar no futuro.");
        }

        return errors;
    }

    private async Task EnsureCategoryExistsAsync(Guid? expenseCategoryId, List<string> errors, CancellationToken cancellationToken)
    {
        if (expenseCategoryId is not { } categoryId)
        {
            return;
        }

        bool categoryExists = await _dbContext.ExpenseCategories
            .AnyAsync(category => category.Id == categoryId, cancellationToken);
        if (!categoryExists)
        {
            errors.Add("A categoria informada não existe.");
        }
    }

    private static List<string> CollectChanges(Expense expense, UpdateExpenseRequest request)
    {
        List<string> changes = [];

        if (!string.Equals(expense.Description, request.Description, StringComparison.Ordinal))
        {
            changes.Add($"Descrição: '{expense.Description}' → '{request.Description}'");
        }

        if (expense.Amount != request.Amount)
        {
            changes.Add($"Valor: {FormatAmount(expense.Amount)} → {FormatAmount(request.Amount)}");
        }

        if (expense.ExpenseDate != request.ExpenseDate)
        {
            changes.Add($"Data da despesa: {FormatDate(expense.ExpenseDate)} → {FormatDate(request.ExpenseDate)}");
        }

        if (expense.ExpenseCategoryId != request.ExpenseCategoryId)
        {
            changes.Add($"Categoria: {FormatCategory(expense.ExpenseCategoryId)} → {FormatCategory(request.ExpenseCategoryId)}");
        }

        return changes;
    }

    private void AddHistory(
        Expense expense,
        string action,
        string actorId,
        DateTime occurredAtUtc,
        ExpenseState? previousState,
        ExpenseState? newState,
        string? draftChangesSummary)
    {
        _dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = action,
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            PreviousState = previousState,
            NewState = newState,
            RejectionJustification = null,
            DraftChangesSummary = draftChangesSummary,
        });
    }

    private static ExpenseResponse ToResponse(Expense expense)
    {
        return new ExpenseResponse
        {
            Id = expense.Id,
            OwnerId = expense.OwnerId,
            Description = expense.Description,
            Amount = expense.Amount,
            ExpenseDate = expense.ExpenseDate,
            State = expense.State.ToString(),
            ExpenseCategoryId = expense.ExpenseCategoryId,
            CreatedAtUtc = expense.CreatedAtUtc,
            UpdatedAtUtc = expense.UpdatedAtUtc,
        };
    }

    private static string FormatAmount(decimal amount)
    {
        return amount.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatCategory(Guid? expenseCategoryId)
    {
        return expenseCategoryId?.ToString() ?? NoCategoryLabel;
    }
}
