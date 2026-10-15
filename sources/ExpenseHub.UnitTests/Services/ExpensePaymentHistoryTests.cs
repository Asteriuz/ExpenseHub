using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Identity;
using ExpenseHub.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Services;

/// <summary>
/// Verifica as regras de pagamento e de consulta de histórico na camada de serviço:
/// pagamento por Finance não proprietário apenas de reembolso Approved, pagamento
/// repetido bloqueado, e histórico visível segundo a matriz de autorização.
/// </summary>
[TestClass]
public sealed class ExpensePaymentHistoryTests
{
    private static readonly DateTimeOffset FixedNowUtc = new(2026, 10, 15, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Verifica que Finance não proprietário paga reembolso Approved, com registro e histórico.</summary>
    [TestMethod]
    public async Task Pay_ByNonOwnerFinance_Approved_TransitionsToPaidAndRecordsPayment()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Approved);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        PayExpenseOutcome outcome = await service.PayAsync(expense.Id, actorId: "finance", CancellationToken.None);

        Assert.AreEqual(PayExpenseStatus.Paid, outcome.Status);
        Assert.IsNotNull(outcome.Expense);
        Assert.AreEqual("Paid", outcome.Expense.State);
        Assert.IsNotNull(outcome.Payment);
        Assert.AreEqual("finance", outcome.Payment.ActorId);
        Assert.AreEqual(FixedNowUtc.UtcDateTime, outcome.Payment.PaidAtUtc);

        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(ExpenseState.Paid, persisted.State);
        PaymentRecord payment = await dbContext.PaymentRecords.SingleAsync();
        Assert.AreEqual(expense.Id, payment.ExpenseId);
        Assert.AreEqual("finance", payment.ActorId);
        Assert.AreEqual(FixedNowUtc.UtcDateTime, payment.PaidAtUtc);
        ExpenseHistory history = await dbContext.ExpenseHistories.SingleAsync();
        Assert.AreEqual("Pagar", history.Action);
        Assert.AreEqual("finance", history.ActorId);
        Assert.AreEqual(ExpenseState.Approved, history.PreviousState);
        Assert.AreEqual(ExpenseState.Paid, history.NewState);
    }

    /// <summary>Verifica que o Finance proprietário não paga o próprio reembolso.</summary>
    [TestMethod]
    public async Task Pay_ByOwnerWithFinanceRole_ReturnsForbiddenAndKeepsApproved()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-finance", ExpenseState.Approved);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        PayExpenseOutcome outcome = await service.PayAsync(expense.Id, actorId: "employee-finance", CancellationToken.None);

        Assert.AreEqual(PayExpenseStatus.Forbidden, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(ExpenseState.Approved, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.PaymentRecords.ToListAsync());
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que pagamento de reembolso inexistente retorna não encontrado.</summary>
    [TestMethod]
    public async Task Pay_WithUnknownId_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        PayExpenseOutcome outcome = await service.PayAsync(Guid.NewGuid(), "finance", CancellationToken.None);

        Assert.AreEqual(PayExpenseStatus.NotFound, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.IsNull(outcome.Payment);
    }

    /// <summary>Verifica que apenas reembolsos Approved recebem pagamento (409, sem registro).</summary>
    [TestMethod]
    [DataRow(ExpenseState.Draft)]
    [DataRow(ExpenseState.Submitted)]
    [DataRow(ExpenseState.Rejected)]
    [DataRow(ExpenseState.Paid)]
    public async Task Pay_WhenStateIsNotApproved_ReturnsConflictWithoutPayment(ExpenseState state)
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", state);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        PayExpenseOutcome outcome = await service.PayAsync(expense.Id, actorId: "finance", CancellationToken.None);

        Assert.AreEqual(PayExpenseStatus.NotApproved, outcome.Status);
        Assert.IsNull(outcome.Expense);
        Assert.AreEqual(state, (await dbContext.Expenses.SingleAsync()).State);
        Assert.IsEmpty(await dbContext.PaymentRecords.ToListAsync());
        Assert.IsEmpty(await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que pagamento repetido retorna conflito sem registro nem histórico duplicados.</summary>
    [TestMethod]
    public async Task Pay_Twice_SecondPay_ReturnsConflictWithoutDuplicatedRecords()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Approved);
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        PayExpenseOutcome first = await service.PayAsync(expense.Id, "finance", CancellationToken.None);
        PayExpenseOutcome second = await service.PayAsync(expense.Id, "finance", CancellationToken.None);

        Assert.AreEqual(PayExpenseStatus.Paid, first.Status);
        Assert.AreEqual(PayExpenseStatus.NotApproved, second.Status);
        Assert.IsNull(second.Expense);
        Assert.HasCount(1, await dbContext.PaymentRecords.ToListAsync());
        Assert.HasCount(1, await dbContext.ExpenseHistories.ToListAsync());
    }

    /// <summary>Verifica que o Employee lê o histórico do próprio reembolso completo.</summary>
    [TestMethod]
    public async Task GetHistory_ByEmployeeOwner_ReturnsAllEntriesOfOwnExpense()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Paid);
        dbContext.Expenses.Add(expense);
        dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = "Criar",
            ActorId = "employee-a",
            OccurredAtUtc = FixedNowUtc.UtcDateTime.AddMinutes(1),
            NewState = ExpenseState.Draft,
        });
        dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = "Pagar",
            ActorId = "finance",
            OccurredAtUtc = FixedNowUtc.UtcDateTime.AddMinutes(2),
            PreviousState = ExpenseState.Approved,
            NewState = ExpenseState.Paid,
        });
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseHistoryOutcome outcome = await service.GetHistoryAsync(
            expense.Id,
            [Roles.Employee],
            actorId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(ExpenseHistoryStatus.Found, outcome.Status);
        Assert.HasCount(2, outcome.Entries);
        Assert.AreEqual("Criar", outcome.Entries[0].Action);
        Assert.AreEqual("Pagar", outcome.Entries[1].Action);
        Assert.IsTrue(outcome.Entries.SequenceEqual(outcome.Entries.OrderBy(entry => entry.OccurredAtUtc)));
    }

    /// <summary>Verifica que o Employee não lê o histórico de reembolso alheio (404).</summary>
    [TestMethod]
    public async Task GetHistory_ByEmployeeOfOtherOwner_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-b", ExpenseState.Submitted);
        dbContext.Expenses.Add(expense);
        dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = "Criar",
            ActorId = "employee-b",
            OccurredAtUtc = FixedNowUtc.UtcDateTime,
            NewState = ExpenseState.Draft,
        });
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseHistoryOutcome outcome = await service.GetHistoryAsync(
            expense.Id,
            [Roles.Employee],
            actorId: "employee-a",
            CancellationToken.None);

        Assert.AreEqual(ExpenseHistoryStatus.NotFound, outcome.Status);
        Assert.IsEmpty(outcome.Entries);
    }

    /// <summary>Verifica que o Auditor lê o histórico de qualquer reembolso.</summary>
    [TestMethod]
    public async Task GetHistory_ByAuditor_ReturnsHistoryOfAnyExpense()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        Expense expense = CreateExpense("employee-a", ExpenseState.Approved);
        dbContext.Expenses.Add(expense);
        dbContext.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = "Aprovar",
            ActorId = "approver",
            OccurredAtUtc = FixedNowUtc.UtcDateTime,
            PreviousState = ExpenseState.Submitted,
            NewState = ExpenseState.Approved,
        });
        await dbContext.SaveChangesAsync();
        ExpenseService service = CreateService(dbContext);

        ExpenseHistoryOutcome outcome = await service.GetHistoryAsync(
            expense.Id,
            [Roles.Auditor],
            actorId: "auditor",
            CancellationToken.None);

        Assert.AreEqual(ExpenseHistoryStatus.Found, outcome.Status);
        Assert.HasCount(1, outcome.Entries);
        Assert.AreEqual("Aprovar", outcome.Entries[0].Action);
    }

    /// <summary>Verifica que ator sem role funcional não consulta histórico (403).</summary>
    [TestMethod]
    public async Task GetHistory_WithoutFunctionalRole_ReturnsForbidden()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        ExpenseHistoryOutcome outcome = await service.GetHistoryAsync(
            Guid.NewGuid(),
            [Roles.Admin],
            actorId: "admin",
            CancellationToken.None);

        Assert.AreEqual(ExpenseHistoryStatus.Forbidden, outcome.Status);
        Assert.IsEmpty(outcome.Entries);
    }

    /// <summary>Verifica que histórico de reembolso inexistente retorna não encontrado.</summary>
    [TestMethod]
    public async Task GetHistory_WithUnknownId_ReturnsNotFound()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext);

        ExpenseHistoryOutcome outcome = await service.GetHistoryAsync(
            Guid.NewGuid(),
            [Roles.Auditor],
            actorId: "auditor",
            CancellationToken.None);

        Assert.AreEqual(ExpenseHistoryStatus.NotFound, outcome.Status);
        Assert.IsEmpty(outcome.Entries);
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
        return new ExpenseService(dbContext, new ExpenseHistoryService(dbContext), new FixedTimeProvider(FixedNowUtc));
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
