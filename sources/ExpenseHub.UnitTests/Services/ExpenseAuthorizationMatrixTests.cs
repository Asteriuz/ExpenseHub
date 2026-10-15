using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Services;

/// <summary>
/// Verifica a matriz de autorização aplicada na camada de serviço: escopos de leitura
/// por role, união de permissões com roles acumuladas e transições de estado.
/// </summary>
[TestClass]
public sealed class ExpenseAuthorizationMatrixTests
{
    private static readonly DateTimeOffset FixedNowUtc = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifica que o Finance consulta apenas reembolsos em Approved e Paid.</summary>
    [TestMethod]
    public async Task GetById_ByFinanceOnly_ReturnsApprovedAndPaidAndHidesOtherStates()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense draft = CreateDraft("employee-a", ExpenseState.Draft);
        Expense submitted = CreateDraft("employee-b", ExpenseState.Submitted);
        Expense approved = CreateDraft("employee-c", ExpenseState.Approved);
        Expense paid = CreateDraft("employee-d", ExpenseState.Paid);
        dbContext.Expenses.AddRange(draft, submitted, approved, paid);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome draftOutcome = await service.GetByIdAsync(draft.Id, ["Finance"], "finance", CancellationToken.None);
        ExpenseDetailOutcome submittedOutcome = await service.GetByIdAsync(submitted.Id, ["Finance"], "finance", CancellationToken.None);
        ExpenseDetailOutcome approvedOutcome = await service.GetByIdAsync(approved.Id, ["Finance"], "finance", CancellationToken.None);
        ExpenseDetailOutcome paidOutcome = await service.GetByIdAsync(paid.Id, ["Finance"], "finance", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.NotFound, draftOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.NotFound, submittedOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.Found, approvedOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.Found, paidOutcome.Status);
    }

    /// <summary>Verifica que o Approver consulta apenas reembolsos em Submitted.</summary>
    [TestMethod]
    public async Task GetById_ByApproverOnly_ForApprovedExpense_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense approved = CreateDraft("employee-a", ExpenseState.Approved);
        dbContext.Expenses.Add(approved);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(approved.Id, ["Approver"], "approver", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.NotFound, outcome.Status);
        Assert.IsNull(outcome.Expense);
    }

    /// <summary>Verifica que o Auditor consulta reembolsos em qualquer estado, inclusive finais.</summary>
    [TestMethod]
    public async Task GetById_ByAuditorOnly_ForRejectedExpense_ReturnsFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense rejected = CreateDraft("employee-a", ExpenseState.Rejected);
        dbContext.Expenses.Add(rejected);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(rejected.Id, ["Auditor"], "auditor", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Found, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
    }

    /// <summary>Verifica que roles acumuladas de leitura recebem a união dos escopos no detalhe.</summary>
    [TestMethod]
    public async Task GetById_ByFinanceAndEmployee_ReturnsOwnDraftAndApprovedFromOthers()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense ownDraft = CreateDraft("finance-employee", ExpenseState.Draft);
        Expense approvedFromOther = CreateDraft("employee-a", ExpenseState.Approved);
        Expense draftFromOther = CreateDraft("employee-b", ExpenseState.Draft);
        dbContext.Expenses.AddRange(ownDraft, approvedFromOther, draftFromOther);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome ownOutcome = await service.GetByIdAsync(ownDraft.Id, ["Finance", "Employee"], "finance-employee", CancellationToken.None);
        ExpenseDetailOutcome approvedOutcome = await service.GetByIdAsync(approvedFromOther.Id, ["Finance", "Employee"], "finance-employee", CancellationToken.None);
        ExpenseDetailOutcome draftOutcome = await service.GetByIdAsync(draftFromOther.Id, ["Finance", "Employee"], "finance-employee", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Found, ownOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.Found, approvedOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.NotFound, draftOutcome.Status);
    }

    /// <summary>Verifica que Employee com Approver consulta o próprio Draft e reembolsos de terceiros em Submitted.</summary>
    [TestMethod]
    public async Task GetById_ByApproverAndEmployee_ReturnsOwnDraftAndSubmittedFromOthers()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense ownDraft = CreateDraft("approver-employee", ExpenseState.Draft);
        Expense ownSubmitted = CreateDraft("approver-employee", ExpenseState.Submitted);
        Expense submittedFromOther = CreateDraft("employee-a", ExpenseState.Submitted);
        Expense draftFromOther = CreateDraft("employee-b", ExpenseState.Draft);
        dbContext.Expenses.AddRange(ownDraft, ownSubmitted, submittedFromOther, draftFromOther);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome ownDraftOutcome = await service.GetByIdAsync(ownDraft.Id, ["Approver", "Employee"], "approver-employee", CancellationToken.None);
        ExpenseDetailOutcome ownSubmittedOutcome = await service.GetByIdAsync(ownSubmitted.Id, ["Approver", "Employee"], "approver-employee", CancellationToken.None);
        ExpenseDetailOutcome otherSubmittedOutcome = await service.GetByIdAsync(submittedFromOther.Id, ["Approver", "Employee"], "approver-employee", CancellationToken.None);
        ExpenseDetailOutcome otherDraftOutcome = await service.GetByIdAsync(draftFromOther.Id, ["Approver", "Employee"], "approver-employee", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Found, ownDraftOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.Found, ownSubmittedOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.Found, otherSubmittedOutcome.Status);
        Assert.AreEqual(ExpenseDetailStatus.NotFound, otherDraftOutcome.Status);
    }

    /// <summary>Verifica que Admin acumulado não amplia o escopo de leitura do Auditor.</summary>
    [TestMethod]
    public async Task GetById_ByAuditorWithAdmin_ReturnsSameScopeAsAuditorAlone()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense draft = CreateDraft("employee-a", ExpenseState.Draft);
        dbContext.Expenses.Add(draft);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(draft.Id, ["Auditor", "Admin"], "auditor-admin", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Found, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
    }

    /// <summary>Verifica que ator sem role funcional recebe acesso negado, mesmo com recurso existente.</summary>
    [TestMethod]
    public async Task GetById_WithNoFunctionalRole_ReturnsForbidden()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a", ExpenseState.Draft);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(expense.Id, [], "admin", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.Forbidden, outcome.Status);
        Assert.IsNull(outcome.Expense);
    }

    /// <summary>Verifica que a listagem para Approver e Finance é a união de Submitted com Approved e Paid.</summary>
    [TestMethod]
    public async Task List_ByApproverAndFinance_ReturnsSubmittedApprovedAndPaid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense submitted = CreateDraft("employee-a", ExpenseState.Submitted);
        Expense approved = CreateDraft("employee-b", ExpenseState.Approved);
        Expense paid = CreateDraft("employee-c", ExpenseState.Paid);
        Expense draft = CreateDraft("employee-d", ExpenseState.Draft);
        dbContext.Expenses.AddRange(submitted, approved, paid, draft);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(["Approver", "Finance"], "approver-finance", CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Listed, outcome.Status);
        Assert.HasCount(3, outcome.Items);
        Assert.IsTrue(outcome.Items.Any(item => item.Id == submitted.Id));
        Assert.IsTrue(outcome.Items.Any(item => item.Id == approved.Id));
        Assert.IsTrue(outcome.Items.Any(item => item.Id == paid.Id));
        Assert.IsFalse(outcome.Items.Any(item => item.Id == draft.Id));
    }

    /// <summary>Verifica que a listagem para Employee e Auditor retorna todos os reembolsos.</summary>
    [TestMethod]
    public async Task List_ByEmployeeAndAuditor_ReturnsAllExpenses()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense own = CreateDraft("employee-auditor", ExpenseState.Draft);
        Expense other = CreateDraft("employee-b", ExpenseState.Paid);
        dbContext.Expenses.AddRange(own, other);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync(["Employee", "Auditor"], "employee-auditor", CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Listed, outcome.Status);
        Assert.HasCount(2, outcome.Items);
        Assert.IsTrue(outcome.Items.Any(item => item.Id == own.Id));
        Assert.IsTrue(outcome.Items.Any(item => item.Id == other.Id));
    }

    /// <summary>Verifica que a listagem sem role funcional recebe acesso negado.</summary>
    [TestMethod]
    public async Task List_WithNoFunctionalRole_ReturnsForbidden()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        dbContext.Expenses.Add(CreateDraft("employee-a", ExpenseState.Draft));
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ListExpensesOutcome outcome = await service.ListAsync([], "admin", CancellationToken.None);

        Assert.AreEqual(ListExpensesStatus.Forbidden, outcome.Status);
        Assert.HasCount(0, outcome.Items);
    }

    /// <summary>Verifica que reembolso em Approved não pode ser enviado, sem histórico duplicado.</summary>
    [TestMethod]
    public async Task Submit_WhenStateIsApproved_ReturnsConflictWithoutHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a", ExpenseState.Approved);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        SubmitExpenseOutcome outcome = await service.SubmitAsync(expense.Id, "employee-a", CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.NotDraft, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que estado final Rejected não reabre o fluxo de envio.</summary>
    [TestMethod]
    public async Task Submit_WhenStateIsRejected_ReturnsConflictWithoutHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-a", ExpenseState.Rejected);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        SubmitExpenseOutcome outcome = await service.SubmitAsync(expense.Id, "employee-a", CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.NotDraft, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que o isolamento entre Employees se mantém mesmo com roles acumuladas.</summary>
    [TestMethod]
    public async Task GetById_ByEmployeeWithOtherRoles_ForOtherOwnerDraft_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateDraft("employee-b", ExpenseState.Draft);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseDetailOutcome outcome = await service.GetByIdAsync(expense.Id, ["Employee", "Finance", "Admin"], "employee-a", CancellationToken.None);

        Assert.AreEqual(ExpenseDetailStatus.NotFound, outcome.Status);
        Assert.IsNull(outcome.Expense);
    }

    private static ExpenseHubDbContext CreateContext()
    {
        return new ExpenseHubDbContext(
            new DbContextOptionsBuilder<ExpenseHubDbContext>()
                .UseInMemoryDatabase($"expensehub-matrix-tests-{Guid.NewGuid():N}")
                .Options);
    }

    private static ExpenseService CreateService(ExpenseHubDbContext dbContext)
    {
        return new ExpenseService(dbContext, new FixedTimeProvider(FixedNowUtc));
    }

    private static Expense CreateDraft(string ownerId, ExpenseState state)
    {
        return new Expense
        {
            OwnerId = ownerId,
            Description = "Passagem de táxi a serviço",
            Amount = 89.90m,
            ExpenseDate = new DateOnly(2026, 10, 10),
            State = state,
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
