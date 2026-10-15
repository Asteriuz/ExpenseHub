using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>Endpoint público de cadastro de usuários.</summary>
[ApiController]
[Route("register")]
public sealed class RegistrationController : ControllerBase
{
    private readonly IUserRegistrationService _registrationService;

    /// <summary>Inicializa o controller com o serviço de cadastro.</summary>
    /// <param name="registrationService">Serviço de cadastro de usuários.</param>
    public RegistrationController(IUserRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>Cadastra um usuário sem qualquer role privilegiada.</summary>
    /// <param name="request">Dados validados do novo usuário.</param>
    /// <returns>O usuário criado; entrada inválida retorna <c>400</c>.</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisteredUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        RegistrationOutcome outcome = await _registrationService.RegisterAsync(request);
        if (!outcome.Succeeded)
        {
            return Problem(
                detail: string.Join("; ", outcome.Errors),
                statusCode: StatusCodes.Status400BadRequest,
                title: "Entrada inválida");
        }

        RegisteredUserResponse response = new()
        {
            Id = outcome.UserId ?? string.Empty,
            Email = outcome.Email ?? string.Empty,
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }
}
