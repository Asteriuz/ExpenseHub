using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Services;

/// <summary>
/// Verifica as regras de criação e edição de rascunhos de reembolso na camada de
/// serviço: ownership, estado, validações de campo e histórico na mesma operação.
/// </summary>
[TestClass]
public sealed class ExpenseServiceTests
{
    private static readonly DateTimeOffset FixedNowUtc = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifica que a criação válida grava rascunho próprio em Draft com histórico de criação.</summary>
    [TestMethod]
    public async Task Create_WithValidRequest_CreatesDraftOwnedByActorAndRecordsCreationHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseCategory category = new() { Name = "Transporte" };
        dbContext.ExpenseCategories.Add(category);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseOutcome outcome = await service.CreateAsync(
            new CreateExpenseRequest
            {
                Description = "Passagem de táxi a serviço",
                Amount = 89.90m,
                ExpenseDate = new DateOnly(2026, 10, 10),
                ExpenseCategoryId = category.Id,
            },
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Created, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
        Assert.AreEqual("employee-a", outcome.Expense.OwnerId);
        Assert.AreEqual("Draft", outcome.Expense.State);
        Assert.AreEqual(FixedNowUtc.UtcDateTime, outcome.Expense.CreatedAtUtc);
        Assert.AreEqual(category.Id, outcome.Expense.ExpenseCategoryId);

        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(ExpenseState.Draft, persisted.State);
        ExpenseHistory history = await dbContext.ExpenseHistories.SingleAsync();
        Assert.AreEqual("Criar", history.Action);
        Assert.AreEqual("employee-a", history.ActorId);
        Assert.IsNull(history.PreviousState);
        Assert.AreEqual(ExpenseState.Draft, history.NewState);
    }

    /// <summary>Verifica que a descrição com menos de 10 caracteres é rejeitada.</summary>
    [TestMethod]
    public async Task Create_WithShortDescription_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseRequest request = ValidCreateRequest();
        request.Description = "curta";
        CreateExpenseOutcome outcome = await service.CreateAsync(
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("descrição", StringComparison.Ordinal)));
        Assert.AreEqual(0, await dbContext.Expenses.CountAsync());
    }

    /// <summary>Verifica que a descrição com mais de 500 caracteres é rejeitada.</summary>
    [TestMethod]
    public async Task Create_WithLongDescription_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseRequest request = ValidCreateRequest();
        request.Description = new string('a', 501);
        CreateExpenseOutcome outcome = await service.CreateAsync(
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("descrição", StringComparison.Ordinal)));
    }

    /// <summary>Verifica que descrição com apenas espaços é rejeitada mesmo com tamanho válido.</summary>
    [TestMethod]
    public async Task Create_WithBlankDescription_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseRequest request = ValidCreateRequest();
        request.Description = "          ";
        CreateExpenseOutcome outcome = await service.CreateAsync(
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("descrição", StringComparison.Ordinal)));
    }

    /// <summary>Verifica que valor menor que 0,01 é rejeitado.</summary>
    [TestMethod]
    public async Task Create_WithAmountBelowMinimum_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseRequest request = ValidCreateRequest();
        request.Amount = 0m;
        CreateExpenseOutcome outcome = await service.CreateAsync(
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("valor", StringComparison.Ordinal)));
    }

    /// <summary>Verifica que valor maior que Int32.MaxValue reais é rejeitado.</summary>
    [TestMethod]
    public async Task Create_WithAmountAboveLimit_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseRequest request = ValidCreateRequest();
        request.Amount = 2147483648.00m;
        CreateExpenseOutcome outcome = await service.CreateAsync(
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("valor", StringComparison.Ordinal)));
    }

    /// <summary>Verifica que data de despesa futura no relógio do servidor é rejeitada.</summary>
    [TestMethod]
    public async Task Create_WithFutureExpenseDate_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseRequest request = ValidCreateRequest();
        request.ExpenseDate = new DateOnly(2026, 10, 16);
        CreateExpenseOutcome outcome = await service.CreateAsync(
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("futuro", StringComparison.Ordinal)));
    }

    /// <summary>Verifica que categoria inexistente informada na entrada é rejeitada.</summary>
    [TestMethod]
    public async Task Create_WithUnknownCategory_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        CreateExpenseRequest request = ValidCreateRequest();
        request.ExpenseCategoryId = Guid.NewGuid();
        CreateExpenseOutcome outcome = await service.CreateAsync(
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(CreateExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("categoria", StringComparison.Ordinal)));
        Assert.AreEqual(0, await dbContext.Expenses.CountAsync());
    }

    /// <summary>Verifica que o proprietário edita o rascunho próprio e o histórico registra as alterações.</summary>
    [TestMethod]
    public async Task Update_WithValidChanges_UpdatesDraftAndRecordsChangeHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft(ownerId: "employee-a");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        UpdateExpenseOutcome outcome = await service.UpdateAsync(
            expense.Id,
            new UpdateExpenseRequest
            {
                Description = "Jantar com cliente no aeroporto",
                Amount = 150.00m,
                ExpenseDate = new DateOnly(2026, 10, 12),
            },
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(UpdateExpenseStatus.Updated, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
        Assert.AreEqual("Jantar com cliente no aeroporto", outcome.Expense.Description);
        Assert.AreEqual(150.00m, outcome.Expense.Amount);
        Assert.AreEqual(new DateOnly(2026, 10, 12), outcome.Expense.ExpenseDate);
        Assert.AreEqual(FixedNowUtc.UtcDateTime, outcome.Expense.UpdatedAtUtc);

        ExpenseHistory history = await dbContext.ExpenseHistories.SingleAsync();
        Assert.AreEqual("Editar", history.Action);
        Assert.AreEqual("employee-a", history.ActorId);
        Assert.AreEqual(ExpenseState.Draft, history.PreviousState);
        Assert.IsNull(history.NewState);
        Assert.IsTrue(history.DraftChangesSummary!.Contains("Descrição:", StringComparison.Ordinal));
        Assert.IsTrue(history.DraftChangesSummary.Contains("Valor:", StringComparison.Ordinal));
        Assert.IsTrue(history.DraftChangesSummary.Contains("Data da despesa:", StringComparison.Ordinal));
    }

    /// <summary>Verifica que Employee não edita rascunho de outro Employee.</summary>
    [TestMethod]
    public async Task Update_ByNonOwner_ReturnsForbidden()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft(ownerId: "employee-b");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        UpdateExpenseOutcome outcome = await service.UpdateAsync(
            expense.Id,
            ValidUpdateRequest(),
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(UpdateExpenseStatus.Forbidden, outcome.Status);
        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual("Passagem de táxi a serviço", persisted.Description);
        Assert.AreEqual(0, await dbContext.ExpenseHistories.CountAsync());
    }

    /// <summary>Verifica que edição de reembolso inexistente retorna não encontrado.</summary>
    [TestMethod]
    public async Task Update_WithUnknownId_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        UpdateExpenseOutcome outcome = await service.UpdateAsync(
            Guid.NewGuid(),
            ValidUpdateRequest(),
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(UpdateExpenseStatus.NotFound, outcome.Status);
    }

    /// <summary>Verifica que estado diferente de Draft bloqueia edição com conflito e sem histórico.</summary>
    [TestMethod]
    public async Task Update_WhenStateIsNotDraft_ReturnsConflictWithoutHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft(ownerId: "employee-a");
        expense.State = ExpenseState.Submitted;
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        UpdateExpenseOutcome outcome = await service.UpdateAsync(
            expense.Id,
            ValidUpdateRequest(),
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(UpdateExpenseStatus.NotDraft, outcome.Status);
        Assert.AreEqual(0, await dbContext.ExpenseHistories.CountAsync());
    }

    /// <summary>Verifica que edição com valor fora da faixa é rejeitada sem alterar o rascunho.</summary>
    [TestMethod]
    public async Task Update_WithInvalidAmount_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft(ownerId: "employee-a");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        UpdateExpenseRequest request = ValidUpdateRequest();
        request.Amount = 0m;
        UpdateExpenseOutcome outcome = await service.UpdateAsync(
            expense.Id,
            request,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(UpdateExpenseStatus.Invalid, outcome.Status);
        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(89.90m, persisted.Amount);
        Assert.AreEqual(0, await dbContext.ExpenseHistories.CountAsync());
    }

    /// <summary>Verifica que edição sem alterações efetivas mantém rascunho sem registrar histórico.</summary>
    [TestMethod]
    public async Task Update_WithNoChanges_ReturnsUpdatedWithoutNewHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft(ownerId: "employee-a");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        UpdateExpenseOutcome outcome = await service.UpdateAsync(
            expense.Id,
            new UpdateExpenseRequest
            {
                Description = "Passagem de táxi a serviço",
                Amount = 89.90m,
                ExpenseDate = new DateOnly(2026, 10, 10),
            },
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(UpdateExpenseStatus.Updated, outcome.Status);
        Assert.IsNull(outcome.Expense!.UpdatedAtUtc);
        Assert.AreEqual(0, await dbContext.ExpenseHistories.CountAsync());
    }

    private static ExpenseHubDbContext CreateContext()
    {
        return new ExpenseHubDbContext(
            new DbContextOptionsBuilder<ExpenseHubDbContext>()
                .UseInMemoryDatabase($"expensehub-tests-{Guid.NewGuid():N}")
                .Options);
    }

    private static ExpenseService CreateService(ExpenseHubDbContext dbContext)
    {
        return new ExpenseService(dbContext, new FixedTimeProvider(FixedNowUtc));
    }

    private static CreateExpenseRequest ValidCreateRequest()
    {
        return new CreateExpenseRequest
        {
            Description = "Passagem de táxi a serviço",
            Amount = 89.90m,
            ExpenseDate = new DateOnly(2026, 10, 10),
        };
    }

    private static UpdateExpenseRequest ValidUpdateRequest()
    {
        return new UpdateExpenseRequest
        {
            Description = "Jantar com cliente no aeroporto",
            Amount = 150.00m,
            ExpenseDate = new DateOnly(2026, 10, 12),
        };
    }

    private static Expense CreateDraft(string ownerId)
    {
        return new Expense
        {
            OwnerId = ownerId,
            Description = "Passagem de táxi a serviço",
            Amount = 89.90m,
            ExpenseDate = new DateOnly(2026, 10, 10),
            State = ExpenseState.Draft,
            CreatedAtUtc = FixedNowUtc.UtcDateTime,
        };
    }

    /// <summary>Relógio fixo usado nos testes para tornar os instantes UTC determinísticos.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        /// <summary>Inicializa o relógio com o instante fixo informado.</summary>
        /// <param name="utcNow">Instante UTC fixo retornado pelo relógio.</param>
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        /// <summary>Retorna o instante UTC fixo configurado.</summary>
        /// <returns>O instante UTC fixo.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return this.utcNow;
        }
    }
}
