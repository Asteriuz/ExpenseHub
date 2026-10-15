using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>Endpoints públicos de autenticação.</summary>
[ApiController]
[Route("login")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    /// <summary>Inicializa o controller com os serviços de identidade e de token.</summary>
    /// <param name="userManager">Gerenciador de usuários do Identity.</param>
    /// <param name="jwtTokenService">Serviço de emissão de tokens JWT.</param>
    public AuthController(UserManager<IdentityUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>Autentica um usuário e devolve um token de acesso.</summary>
    /// <param name="request">Credenciais do usuário.</param>
    /// <returns>Token de acesso quando as credenciais são válidas; caso contrário, <c>401</c>.</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        IdentityUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Problem(
                detail: "Credenciais inválidas.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Não autenticado");
        }

        IList<string> roles = await _userManager.GetRolesAsync(user);
        (string token, DateTime expiresAtUtc) = _jwtTokenService.CreateAccessToken(user, roles);

        LoginResponse response = new()
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
        };

        return Ok(response);
    }
}
