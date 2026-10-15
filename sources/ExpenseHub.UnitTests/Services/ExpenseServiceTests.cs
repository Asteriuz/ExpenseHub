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
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
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
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
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
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
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
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que o envio pelo proprietário transiciona de Draft para Submitted com histórico.</summary>
    [TestMethod]
    public async Task Submit_ByOwner_Draft_TransitionsToSubmittedAndRecordsHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        SubmitExpenseOutcome outcome = await service.SubmitAsync(
            expense.Id,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.Submitted, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
        Assert.AreEqual("Submitted", outcome.Expense.State);

        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(ExpenseState.Submitted, persisted.State);
        ExpenseHistory history = await dbContext.ExpenseHistories.SingleAsync();
        Assert.AreEqual("Enviar", history.Action);
        Assert.AreEqual("employee-a", history.ActorId);
        Assert.AreEqual(ExpenseState.Draft, history.PreviousState);
        Assert.AreEqual(ExpenseState.Submitted, history.NewState);
        Assert.AreEqual(FixedNowUtc.UtcDateTime, history.OccurredAtUtc);
    }

    /// <summary>Verifica que o envio por quem não é o proprietário é negado sem alterar estado.</summary>
    [TestMethod]
    public async Task Submit_ByNonOwner_ReturnsForbiddenAndKeepsDraft()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        SubmitExpenseOutcome outcome = await service.SubmitAsync(
            expense.Id,
            ownerId: "employee-b",
            CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.Forbidden, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(ExpenseState.Draft, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que o envio de reembolso inexistente retorna não encontrado.</summary>
    [TestMethod]
    public async Task Submit_WithUnknownId_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        SubmitExpenseOutcome outcome = await service.SubmitAsync(
            Guid.NewGuid(),
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.NotFound, outcome.Status);
        Assert.IsNull(outcome.Expense);
    }

    /// <summary>Verifica que o envio repetido retorna conflito sem histórico duplicado.</summary>
    [TestMethod]
    public async Task Submit_WhenStateIsNotDraft_ReturnsConflictWithoutDuplicatedHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a");
        expense.State = ExpenseState.Submitted;
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        SubmitExpenseOutcome outcome = await service.SubmitAsync(
            expense.Id,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.NotDraft, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que o Employee lista somente os próprios reembolsos.</summary>
    [TestMethod]
    public async Task List_ByEmployeeOnly_ReturnsOnlyOwnExpenses()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense own = CreateDraft("employee-a");
        Expense other = CreateDraft("employee-b");
        dbContext.Expenses.AddRange(own, other);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(
            roles: ["Employee"],
            actorId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Listed, outcome.Status);
        ExpenseResponse ownItem = outcome.Items.Single(item => item.Id == own.Id);
        Assert.IsFalse(outcome.Items.Any(item => item.Id == other.Id));
        Assert.AreEqual("employee-a", ownItem.OwnerId);
    }

    /// <summary>Verifica que o Approver lista somente os reembolsos em Submitted.</summary>
    [TestMethod]
    public async Task List_ByApproverOnly_ReturnsOnlySubmittedExpenses()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense submitted = CreateDraft("employee-a");
        submitted.State = ExpenseState.Submitted;
        Expense draft = CreateDraft("employee-b");
        Expense approved = CreateDraft("employee-c");
        approved.State = ExpenseState.Approved;
        dbContext.Expenses.AddRange(submitted, draft, approved);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(
            roles: ["Approver"],
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Listed, outcome.Status);
        Assert.HasCount(1, outcome.Items);
        Assert.AreEqual(submitted.Id, outcome.Items[0].Id);
    }

    /// <summary>Verifica que o Finance lista somente os reembolsos em Approved e Paid.</summary>
    [TestMethod]
    public async Task List_ByFinanceOnly_ReturnsOnlyApprovedAndPaidExpenses()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense approved = CreateDraft("employee-a");
        approved.State = ExpenseState.Approved;
        Expense paid = CreateDraft("employee-b");
        paid.State = ExpenseState.Paid;
        Expense submitted = CreateDraft("employee-c");
        submitted.State = ExpenseState.Submitted;
        dbContext.Expenses.AddRange(approved, paid, submitted);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(
            roles: ["Finance"],
            actorId: "finance",
            CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Listed, outcome.Status);
        Assert.HasCount(2, outcome.Items);
        Assert.IsTrue(outcome.Items.Any(item => item.Id == approved.Id));
        Assert.IsTrue(outcome.Items.Any(item => item.Id == paid.Id));
    }

    /// <summary>Verifica que o Auditor lista todos os reembolsos, em qualquer estado.</summary>
    [TestMethod]
    public async Task List_ByAuditorOnly_ReturnsAllExpenses()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense draft = CreateDraft("employee-a");
        Expense submitted = CreateDraft("employee-b");
        submitted.State = ExpenseState.Submitted;
        Expense paid = CreateDraft("employee-c");
        paid.State = ExpenseState.Paid;
        dbContext.Expenses.AddRange(draft, submitted, paid);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(
            roles: ["Auditor"],
            actorId: "auditor",
            CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Listed, outcome.Status);
        Assert.HasCount(3, outcome.Items);
    }

    /// <summary>Verifica que roles acumuladas recebem a união dos escopos de leitura.</summary>
    [TestMethod]
    public async Task List_WithAccumulatedRoles_ReturnsUnionOfScopes()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense own = CreateDraft("employee-approver");
        Expense submitted = CreateDraft("employee-a");
        submitted.State = ExpenseState.Submitted;
        Expense otherDraft = CreateDraft("employee-b");
        dbContext.Expenses.AddRange(own, submitted, otherDraft);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(
            roles: ["Employee", "Approver"],
            actorId: "employee-approver",
            CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Listed, outcome.Status);
        Assert.HasCount(2, outcome.Items);
        Assert.IsTrue(outcome.Items.Any(item => item.Id == own.Id));
        Assert.IsTrue(outcome.Items.Any(item => item.Id == submitted.Id));
        Assert.IsFalse(outcome.Items.Any(item => item.Id == otherDraft.Id));
    }

    /// <summary>Verifica que ator sem role funcional (Admin) recebe acesso negado.</summary>
    [TestMethod]
    public async Task List_WithoutFunctionalRole_ReturnsForbidden()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        dbContext.Expenses.Add(CreateDraft("employee-a"));
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(
            roles: ["Admin"],
            actorId: "admin",
            CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Forbidden, outcome.Status);
        Assert.HasCount(0, outcome.Items);
    }

    /// <summary>Verifica que Employee não consulta detalhe de reembolso alheio (404 sem vazar existência).</summary>
    [TestMethod]
    public async Task GetById_ByEmployee_ForOtherOwnerExpense_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-b");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(
            expense.Id,
            roles: ["Employee"],
            actorId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.NotFound, outcome.Status);
        Assert.IsNull(outcome.Expense);
    }

    /// <summary>Verifica que Employee consulta o próprio reembolso.</summary>
    [TestMethod]
    public async Task GetById_ByEmployee_ForOwnExpense_ReturnsFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(
            expense.Id,
            roles: ["Employee"],
            actorId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Found, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
        Assert.AreEqual(expense.Id, outcome.Expense.Id);
        Assert.AreEqual("employee-a", outcome.Expense.OwnerId);
    }

    /// <summary>Verifica que Approver consulta detalhe apenas de reembolso em Submitted.</summary>
    [TestMethod]
    public async Task GetById_ByApprover_ForSubmittedExpense_ReturnsFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense submitted = CreateDraft("employee-a");
        submitted.State = ExpenseState.Submitted;
        Expense draft = CreateDraft("employee-b");
        dbContext.Expenses.AddRange(submitted, draft);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome submittedOutcome = await service.GetByIdAsync(
            submitted.Id,
            roles: ["Approver"],
            actorId: "approver",
            CancellationToken.None);
        ExpenseDetailOutcome draftOutcome = await service.GetByIdAsync(
            draft.Id,
            roles: ["Approver"],
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Found, submittedOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.NotFound, draftOutcome.Status);
    }

    /// <summary>Verifica que Auditor consulta o detalhe de qualquer reembolso.</summary>
    [TestMethod]
    public async Task GetById_ByAuditor_ReturnsFoundForAnyExpense()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense rejected = CreateDraft("employee-a");
        rejected.State = ExpenseState.Rejected;
        dbContext.Expenses.Add(rejected);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(
            rejected.Id,
            roles: ["Auditor"],
            actorId: "auditor",
            CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Found, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
    }

    /// <summary>Verifica que ator sem role funcional não consulta detalhe de reembolso.</summary>
    [TestMethod]
    public async Task GetById_WithoutFunctionalRole_ReturnsForbidden()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a");
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(
            expense.Id,
            roles: ["Admin"],
            actorId: "admin",
            CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Forbidden, outcome.Status);
        Assert.IsNull(outcome.Expense);
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
