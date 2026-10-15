using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Cria de forma idempotente as roles obrigatórias e a única conta Admin inicial.
/// Pode ser executado várias vezes sem duplicar registros.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>Chave de configuração que contém a senha inicial do Admin.</summary>
    public const string AdminPasswordConfigurationKey = "Seed:AdminPassword";

    /// <summary>Chave de configuração que contém o e-mail da conta Admin.</summary>
    public const string AdminEmailConfigurationKey = "Seed:AdminEmail";

    /// <summary>E-mail usado para a conta Admin quando nenhum é configurado.</summary>
    public const string DefaultAdminEmail = "admin@expensehub.local";

    /// <summary>Garante que as roles e a conta Admin existam.</summary>
    /// <param name="services">Provedor de serviços raiz da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Uma tarefa concluída quando o seed termina.</returns>
    /// <exception cref="InvalidOperationException">Lançada quando a senha do Admin não está configurada.</exception>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        using IServiceScope scope = services.CreateScope();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        await EnsureRolesAsync(roleManager);
        await EnsureAdminAsync(userManager, configuration);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (string roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                IdentityResult result = await roleManager.CreateAsync(new IdentityRole(roleName));
                EnsureSucceeded(result, $"criar a role '{roleName}'");
            }
        }
    }

    private static async Task EnsureAdminAsync(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        string? password = configuration[AdminPasswordConfigurationKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"A configuração '{AdminPasswordConfigurationKey}' é obrigatória e deve vir de user-secrets " +
                "ou variável de ambiente.");
        }

        string email = configuration[AdminEmailConfigurationKey] ?? DefaultAdminEmail;

        IdentityUser? admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };

            IdentityResult result = await userManager.CreateAsync(admin, password);
            EnsureSucceeded(result, "criar a conta Admin");
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            IdentityResult result = await userManager.AddToRoleAsync(admin, Roles.Admin);
            EnsureSucceeded(result, "vincular a conta Admin à role 'Admin'");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Não foi possível {operation}: {errors}");
    }
}
