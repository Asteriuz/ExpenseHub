using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Identity;
using ExpenseHub.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Services;

/// <summary>
/// Verifica o fluxo completo de transições de estado na camada de serviço:
/// criação, edição, envio, decisão e pagamento, além do estado final Rejected,
/// com o histórico completo registrado na mesma operação lógica de cada transição.
/// </summary>
[TestClass]
public sealed class ExpenseLifecycleTests
{
    private static readonly DateTimeOffset _startUtc = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifica que o fluxo válido transiciona Draft→Submitted→Approved→Paid com histórico completo e ordenado.</summary>
    [TestMethod]
    public async Task FullLifecycle_CreateEditSubmitApprovePay_TransitionsToPaidAndRecordsCompleteHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        AdvancingTimeProvider timeProvider = new(_startUtc);
        ExpenseService service = CreateService(dbContext, timeProvider);

        CreateExpenseOutcome created = await service.CreateAsync(
            ValidCreateRequest(),
            ownerId: "employee-a",
            CancellationToken.None);
        Assert.AreEqual(CreateExpenseStatus.Created, created.Status);

        UpdateExpenseOutcome updated = await service.UpdateAsync(
            created.Expense!.Id,
            new UpdateExpenseRequest
            {
                Description = "Jantar com cliente no aeroporto",
                Amount = 150.00m,
                ExpenseDate = new DateOnly(2026, 10, 12),
            },
            ownerId: "employee-a",
            CancellationToken.None);
        Assert.AreEqual(UpdateExpenseStatus.Updated, updated.Status);

        SubmitExpenseOutcome submitted = await service.SubmitAsync(
            created.Expense.Id,
            ownerId: "employee-a",
            CancellationToken.None);
        Assert.AreEqual(SubmitExpenseStatus.Submitted, submitted.Status);
        Assert.AreEqual("Submitted", submitted.Expense!.State);

        ApproveExpenseOutcome approved = await service.ApproveAsync(
            created.Expense.Id,
            actorId: "approver",
            CancellationToken.None);
        Assert.AreEqual(ApproveExpenseStatus.Approved, approved.Status);
        Assert.AreEqual("Approved", approved.Expense!.State);

        PayExpenseOutcome paid = await service.PayAsync(
            created.Expense.Id,
            actorId: "finance",
            CancellationToken.None);
        Assert.AreEqual(PayExpenseStatus.Paid, paid.Status);
        Assert.AreEqual("Paid", paid.Expense!.State);
        Assert.IsNotNull(paid.Payment);

        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(ExpenseState.Paid, persisted.State);
        Assert.HasCount(1, await dbContext.PaymentRecords.ToListAsync());

        ExpenseHistoryOutcome history = await service.GetHistoryAsync(
            created.Expense.Id,
            [Roles.Auditor],
            actorId: "auditor",
            CancellationToken.None);

        Assert.AreEqual(ExpenseHistoryStatus.Found, history.Status);
        Assert.HasCount(5, history.Entries);
        Assert.AreEqual("Criar", history.Entries[0].Action);
        Assert.AreEqual("Editar", history.Entries[1].Action);
        Assert.AreEqual("Enviar", history.Entries[2].Action);
        Assert.AreEqual("Aprovar", history.Entries[3].Action);
        Assert.AreEqual("Pagar", history.Entries[4].Action);
        Assert.AreEqual("employee-a", history.Entries[2].ActorId);
        Assert.AreEqual("approver", history.Entries[3].ActorId);
        Assert.AreEqual("finance", history.Entries[4].ActorId);
        Assert.IsNull(history.Entries[3].RejectionJustification);
        Assert.IsTrue(history.Entries[0].OccurredAtUtc < history.Entries[4].OccurredAtUtc);
        Assert.IsTrue(history.Entries.SequenceEqual(history.Entries.OrderBy(entry => entry.OccurredAtUtc)));
    }

    /// <summary>Verifica que o fluxo com reprovação termina em Rejected e bloqueia toda transição posterior sem histórico duplicado.</summary>
    [TestMethod]
    public async Task FullLifecycle_WithRejection_RejectedIsFinalAndBlocksFurtherTransitionsWithoutDuplicatedHistory()
    {
        using ExpenseHubDbContext dbContext = CreateContext();
        ExpenseService service = CreateService(dbContext, new AdvancingTimeProvider(_startUtc));

        CreateExpenseOutcome created = await service.CreateAsync(
            ValidCreateRequest(),
            ownerId: "employee-a",
            CancellationToken.None);
        Assert.AreEqual(CreateExpenseStatus.Created, created.Status);

        SubmitExpenseOutcome submitted = await service.SubmitAsync(
            created.Expense!.Id,
            ownerId: "employee-a",
            CancellationToken.None);
        Assert.AreEqual(SubmitExpenseStatus.Submitted, submitted.Status);

        RejectExpenseOutcome rejected = await service.RejectAsync(
            created.Expense.Id,
            new RejectExpenseRequest { Justification = "Recibo não comprova a despesa informada." },
            actorId: "approver",
            CancellationToken.None);
        Assert.AreEqual(RejectExpenseStatus.Rejected, rejected.Status);
        Assert.AreEqual("Rejected", rejected.Expense!.State);

        SubmitExpenseOutcome resubmit = await service.SubmitAsync(
            created.Expense.Id,
            ownerId: "employee-a",
            CancellationToken.None);
        ApproveExpenseOutcome approve = await service.ApproveAsync(
            created.Expense.Id,
            actorId: "approver",
            CancellationToken.None);
        RejectExpenseOutcome reject = await service.RejectAsync(
            created.Expense.Id,
            new RejectExpenseRequest { Justification = "Recibo não comprova a despesa informada." },
            actorId: "approver",
            CancellationToken.None);
        PayExpenseOutcome pay = await service.PayAsync(
            created.Expense.Id,
            actorId: "finance",
            CancellationToken.None);

        Assert.AreEqual(SubmitExpenseStatus.NotDraft, resubmit.Status);
        Assert.AreEqual(ApproveExpenseStatus.NotSubmitted, approve.Status);
        Assert.AreEqual(RejectExpenseStatus.NotSubmitted, reject.Status);
        Assert.AreEqual(PayExpenseStatus.NotApproved, pay.Status);

        Expense persisted = await dbContext.Expenses.SingleAsync();
        Assert.AreEqual(ExpenseState.Rejected, persisted.State);
        Assert.IsEmpty(await dbContext.PaymentRecords.ToListAsync());

        ExpenseHistoryOutcome history = await service.GetHistoryAsync(
            created.Expense.Id,
            [Roles.Auditor],
            actorId: "auditor",
            CancellationToken.None);

        Assert.AreEqual(ExpenseHistoryStatus.Found, history.Status);
        Assert.HasCount(3, history.Entries);
        Assert.AreEqual("Criar", history.Entries[0].Action);
        Assert.AreEqual("Enviar", history.Entries[1].Action);
        Assert.AreEqual("Reprovar", history.Entries[2].Action);
        Assert.AreEqual("Recibo não comprova a despesa informada.", history.Entries[2].RejectionJustification);
    }

    private static ExpenseHubDbContext CreateContext()
    {
        return new ExpenseHubDbContext(
            new DbContextOptionsBuilder<ExpenseHubDbContext>()
                .UseInMemoryDatabase($"expensehub-tests-{Guid.NewGuid():N}")
                .Options);
    }

    private static ExpenseService CreateService(ExpenseHubDbContext dbContext, TimeProvider timeProvider)
    {
        return new ExpenseService(dbContext, new ExpenseHistoryService(dbContext), timeProvider);
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

    /// <summary>Relógio fixo que avança um minuto a cada consulta, para ordenar os instantes UTC de forma determinística.</summary>
    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        /// <summary>Inicializa o relógio com o instante inicial informado.</summary>
        /// <param name="startUtc">Instante UTC inicial retornado pelo relógio.</param>
        public AdvancingTimeProvider(DateTimeOffset startUtc)
        {
            _utcNow = startUtc;
        }

        /// <summary>Retorna o instante UTC atual e avança o relógio em um minuto.</summary>
        /// <returns>O instante UTC corrente antes do avanço.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            DateTimeOffset current = _utcNow;
            _utcNow = current.AddMinutes(1);
            return current;
        }
    }
}
