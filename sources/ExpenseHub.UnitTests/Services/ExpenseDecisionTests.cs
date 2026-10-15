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
/// Verifica as regras de decisão de reembolsos na camada de serviço: aprovação e
/// reprovação por Approver não proprietário, justificativa obrigatória, estados
/// finais e histórico registrado na mesma operação lógica.
/// </summary>
[TestClass]
public sealed class ExpenseDecisionTests
{
    private static readonly DateTimeOffset _fixedNowUtc = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifica que Approver não proprietário aprova reembolso em Submitted com histórico.</summary>
    [TestMethod]
    public async Task Approve_ByNonOwnerApprover_Submitted_TransitionsToApprovedAndRecordsHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ApproveExpenseOutcome outcome = await service.ApproveAsync(
            expense.Id,
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(ApproveExpenseStatus.Approved, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
        Assert.AreEqual("Approved", outcome.Expense.State);

        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(ExpenseState.Approved, persisted.State);
        ExpenseHistory history = await dbContext.ExpenseHistories.SingleAsync();
        Assert.AreEqual("Aprovar", history.Action);
        Assert.AreEqual("approver", history.ActorId);
        Assert.AreEqual(ExpenseState.Submitted, history.PreviousState);
        Assert.AreEqual(ExpenseState.Approved, history.NewState);
        Assert.AreEqual(_fixedNowUtc.UtcDateTime, history.OccurredAtUtc);
        Assert.IsNull(history.RejectionJustification);
    }

    /// <summary>Verifica que o Approver proprietário não decide sobre o próprio reembolso.</summary>
    [TestMethod]
    public async Task Approve_ByOwnerWithApproverRole_ReturnsForbiddenAndKeepsSubmitted()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-approver", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ApproveExpenseOutcome outcome = await service.ApproveAsync(
            expense.Id,
            actorId: "employee-approver",
            CancellationToken.None);

        Assert.AreEqual(ApproveExpenseStatus.Forbidden, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(ExpenseState.Submitted, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que aprovação de reembolso inexistente retorna não encontrado.</summary>
    [TestMethod]
    public async Task Approve_WithUnknownId_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        ApproveExpenseOutcome outcome = await service.ApproveAsync(
            Guid.NewGuid(),
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(ApproveExpenseStatus.NotFound, outcome.Status);
        Assert.IsNull(outcome.Expense);
    }

    /// <summary>Verifica que estados diferentes de Submitted não recebem aprovação (409, sem histórico).</summary>
    [TestMethod]
    [DataRow(ExpenseState.Draft)]
    [DataRow(ExpenseState.Approved)]
    [DataRow(ExpenseState.Rejected)]
    [DataRow(ExpenseState.Paid)]
    public async Task Approve_WhenStateIsNotSubmitted_ReturnsConflictWithoutHistory(ExpenseState state)
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", state);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ApproveExpenseOutcome outcome = await service.ApproveAsync(
            expense.Id,
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(ApproveExpenseStatus.NotSubmitted, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(state, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que aprovação repetida retorna conflito sem histórico duplicado.</summary>
    [TestMethod]
    public async Task Approve_Twice_SecondApprove_ReturnsConflictWithoutDuplicatedHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ApproveExpenseOutcome first = await service.ApproveAsync(expense.Id, "approver", CancellationToken.None);
        ApproveExpenseOutcome second = await service.ApproveAsync(expense.Id, "approver", CancellationToken.None);

        Assert.AreEqual(ApproveExpenseStatus.Approved, first.Status);
        Assert.AreEqual(ApproveExpenseStatus.NotSubmitted, second.Status);
        Assert.IsNull(second.Expense);
        Assert.HasCount(1, await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que Approver não proprietário reprova com justificativa registrada no histórico.</summary>
    [TestMethod]
    public async Task Reject_ByNonOwnerApprover_Submitted_TransitionsToRejectedAndRecordsJustification()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseOutcome outcome = await service.RejectAsync(
            expense.Id,
            new RejectExpenseRequest { Justification = "Recibo não comprova a despesa." },
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.Rejected, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
        Assert.AreEqual("Rejected", outcome.Expense.State);

        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(ExpenseState.Rejected, persisted.State);
        ExpenseHistory history = await dbContext.ExpenseHistories.SingleAsync();
        Assert.AreEqual("Reprovar", history.Action);
        Assert.AreEqual("approver", history.ActorId);
        Assert.AreEqual(ExpenseState.Submitted, history.PreviousState);
        Assert.AreEqual(ExpenseState.Rejected, history.NewState);
        Assert.AreEqual("Recibo não comprova a despesa.", history.RejectionJustification);
        Assert.AreEqual(_fixedNowUtc.UtcDateTime, history.OccurredAtUtc);
    }

    /// <summary>Verifica que o Approver proprietário não reprova o próprio reembolso.</summary>
    [TestMethod]
    public async Task Reject_ByOwnerWithApproverRole_ReturnsForbiddenAndKeepsSubmitted()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-approver", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseOutcome outcome = await service.RejectAsync(
            expense.Id,
            ValidRejectRequest(),
            actorId: "employee-approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.Forbidden, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(ExpenseState.Submitted, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que reprovação de reembolso inexistente retorna não encontrado.</summary>
    [TestMethod]
    public async Task Reject_WithUnknownId_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseOutcome outcome = await service.RejectAsync(
            Guid.NewGuid(),
            ValidRejectRequest(),
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.NotFound, outcome.Status);
        Assert.IsNull(outcome.Expense);
    }

    /// <summary>Verifica que justificativa apenas com espaços é rejeitada sem alterar o reembolso.</summary>
    [TestMethod]
    public async Task Reject_WithBlankJustification_ReturnsInvalidWithoutStateChange()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseRequest request = ValidRejectRequest();
        request.Justification = "          ";
        RejectExpenseOutcome outcome = await service.RejectAsync(
            expense.Id,
            request,
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.Invalid, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("justificativa", StringComparison.Ordinal)));
        Assert.AreEqual(ExpenseState.Submitted, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que justificativa com menos de 10 caracteres é rejeitada.</summary>
    [TestMethod]
    public async Task Reject_WithShortJustification_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseRequest request = ValidRejectRequest();
        request.Justification = "curta";
        RejectExpenseOutcome outcome = await service.RejectAsync(
            expense.Id,
            request,
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("justificativa", StringComparison.Ordinal)));
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que justificativa com mais de 500 caracteres é rejeitada.</summary>
    [TestMethod]
    public async Task Reject_WithLongJustification_ReturnsInvalid()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseRequest request = ValidRejectRequest();
        request.Justification = new string('a', 501);
        RejectExpenseOutcome outcome = await service.RejectAsync(
            expense.Id,
            request,
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.Invalid, outcome.Status);
        Assert.IsTrue(outcome.Errors.Any(error => error.Contains("justificativa", StringComparison.Ordinal)));
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que reprovação repetida retorna conflito sem histórico duplicado.</summary>
    [TestMethod]
    public async Task Reject_Twice_SecondReject_ReturnsConflictWithoutDuplicatedHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseOutcome first = await service.RejectAsync(
            expense.Id,
            ValidRejectRequest(),
            "approver",
            CancellationToken.None);
        RejectExpenseOutcome second = await service.RejectAsync(
            expense.Id,
            ValidRejectRequest(),
            "approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.Rejected, first.Status);
        Assert.AreEqual(RejectExpenseStatus.NotSubmitted, second.Status);
        Assert.IsNull(second.Expense);
        Assert.HasCount(1, await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que estados diferentes de Submitted não recebem reprovação (409, sem histórico), incluindo o estado final Rejected.</summary>
    [TestMethod]
    [DataRow(ExpenseState.Draft)]
    [DataRow(ExpenseState.Approved)]
    [DataRow(ExpenseState.Rejected)]
    [DataRow(ExpenseState.Paid)]
    public async Task Reject_WhenStateIsNotSubmitted_ReturnsConflictWithoutHistory(ExpenseState state)
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", state);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        RejectExpenseOutcome outcome = await service.RejectAsync(
            expense.Id,
            ValidRejectRequest(),
            actorId: "approver",
            CancellationToken.None);

        Assert.AreEqual(RejectExpenseStatus.NotSubmitted, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(state, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que roles acumuladas não eliminam a proibição de decisão sobre o próprio reembolso.</summary>
    [TestMethod]
    public async Task Approve_ByOwnerWithAccumulatedEmployeeAndApproverRoles_ReturnsForbidden()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-approver", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ApproveExpenseOutcome approveOutcome = await service.ApproveAsync(
            expense.Id,
            actorId: "employee-approver",
            CancellationToken.None);
        RejectExpenseOutcome rejectOutcome = await service.RejectAsync(
            expense.Id,
            ValidRejectRequest(),
            actorId: "employee-approver",
            CancellationToken.None);

        Assert.AreEqual(ApproveExpenseStatus.Forbidden, approveOutcome.Status);
        Assert.AreEqual(RejectExpenseStatus.Forbidden, rejectOutcome.Status);
        Assert.AreEqual(ExpenseState.Submitted, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que Rejected é estado final: o reenvio retorna conflito sem histórico.</summary>
    [TestMethod]
    public async Task Submit_WhenStateIsRejected_ReturnsConflictWithoutHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Rejected);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        SubmitExpenseOutcome outcome = await service.SubmitAsync(
            expense.Id,
            ownerId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.NotDraft, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(ExpenseState.Rejected, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
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
        return new ExpenseService(dbContext, new ExpenseHistoryService(dbContext), new FixedTimeProvider(_fixedNowUtc));
    }

    private static RejectExpenseRequest ValidRejectRequest()
    {
        return new RejectExpenseRequest
        {
            Justification = "Recibo não comprova a despesa informada.",
        };
    }

    private static Expense CreateExpense(string ownerId, ExpenseState state)
    {
        return new Expense
        {
            OwnerId = ownerId,
            Description = "Passagem de táxi a serviço",
            Amount = 89.90m,
            ExpenseDate = new DateOnly(2026, 10, 10),
            State = state,
            CreatedAtUtc = _fixedNowUtc.UtcDateTime,
        };
    }

    /// <summary>Relógio fixo usado nos testes para tornar os instantes UTC determinísticos.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        /// <summary>Inicializa o relógio com o instante fixo informado.</summary>
        /// <param name="utcNow">Instante UTC fixo retornado pelo relógio.</param>
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        /// <summary>Retorna o instante UTC fixo configurado.</summary>
        /// <returns>O instante UTC fixo.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
