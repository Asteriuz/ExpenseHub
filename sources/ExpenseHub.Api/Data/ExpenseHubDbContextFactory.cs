using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Fábrica de contexto usada apenas pelas ferramentas de linha de comando do Entity
/// Framework Core (<c>dotnet ef</c>). Evita que o processo de migração execute o
/// código de inicialização da aplicação.
/// </summary>
public sealed class ExpenseHubDbContextFactory : IDesignTimeDbContextFactory<ExpenseHubDbContext>
{
    /// <summary>Cria uma instância do <see cref="ExpenseHubDbContext"/> em tempo de design.</summary>
    /// <param name="args">Argumentos passados pela ferramenta; não utilizados.</param>
    /// <returns>Contexto configurado com o provider SQLite.</returns>
    public ExpenseHubDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ExpenseHubDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=expensehub.db");
        return new ExpenseHubDbContext(optionsBuilder.Options);
    }
}
