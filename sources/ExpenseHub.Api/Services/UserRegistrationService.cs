using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Services;

/// <summary>Cadastra usuários por meio do ASP.NET Core Identity, sem atribuir roles.</summary>
public sealed class UserRegistrationService : IUserRegistrationService
{
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>Inicializa o serviço com o gerenciador de usuários.</summary>
    /// <param name="userManager">Gerenciador de usuários do Identity.</param>
    public UserRegistrationService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<RegistrationOutcome> RegisterAsync(RegisterRequest request)
    {
        IdentityUser user = new()
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
        };

        IdentityResult result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return RegistrationOutcome.Invalid(result.Errors.Select(error => error.Description).ToList());
        }

        return RegistrationOutcome.Created(user.Id, request.Email);
    }
}
