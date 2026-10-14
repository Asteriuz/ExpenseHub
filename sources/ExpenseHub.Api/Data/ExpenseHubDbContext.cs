using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Data;

/// <summary>Contexto do Entity Framework Core com o modelo de persistência do ExpenseHub.</summary>
public class ExpenseHubDbContext : DbContext
{
    /// <summary>Nome da connection string do contexto declarada em appsettings.json.</summary>
    public const string ConnectionStringName = "ExpenseHub";

    /// <summary>Inicializa o contexto com as opções configuradas na aplicação.</summary>
    /// <param name="options">Opções do contexto fornecidas pelo contêiner de dependências.</param>
    public ExpenseHubDbContext(DbContextOptions<ExpenseHubDbContext> options)
        : base(options)
    {
    }

    /// <summary>Conjunto de reembolsos.</summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>Conjunto de categorias de reembolso.</summary>
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    /// <summary>Conjunto de registros de histórico de reembolso.</summary>
    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    /// <summary>Conjunto de registros de pagamento.</summary>
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    /// <summary>Configura os mapeamentos das entidades no modelo relacional.</summary>
    /// <param name="modelBuilder">Construtor do modelo do Entity Framework Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");

            entity.HasKey(expense => expense.Id);

            entity.Property(expense => expense.OwnerId)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(expense => expense.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(expense => expense.Amount)
                .HasPrecision(18, 2);

            entity.Property(expense => expense.State)
                .HasConversion<int>();

            entity.HasOne(expense => expense.Category)
                .WithMany()
                .HasForeignKey(expense => expense.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(expense => expense.OwnerId);

            entity.HasIndex(expense => expense.State);

            entity.HasIndex(expense => new { expense.OwnerId, expense.State });
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.ToTable("ExpenseCategories");

            entity.HasKey(category => category.Id);

            entity.Property(category => category.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(category => category.Name)
                .IsUnique();
        });

        modelBuilder.Entity<ExpenseHistory>(entity =>
        {
            entity.ToTable("ExpenseHistories");

            entity.HasKey(history => history.Id);

            entity.Property(history => history.Action)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(history => history.ActorId)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(history => history.RejectionJustification)
                .HasMaxLength(500);

            entity.Property(history => history.DraftChangesSummary)
                .HasMaxLength(2000);

            entity.HasOne(history => history.Expense)
                .WithMany()
                .HasForeignKey(history => history.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(history => new { history.ExpenseId, history.OccurredAtUtc });
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.ToTable("PaymentRecords");

            entity.HasKey(payment => payment.Id);

            entity.Property(payment => payment.ActorId)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasOne(payment => payment.Expense)
                .WithOne()
                .HasForeignKey<PaymentRecord>(payment => payment.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(payment => payment.ExpenseId)
                .IsUnique();
        });
    }
}
