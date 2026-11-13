using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NecroWiKi.API.Models;
using NecroWiKi.API.Services;

namespace NecroWiKi.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly PortalIdentityService _identityService;

    public AuthController(PortalIdentityService identityService)
    {
        _identityService = identityService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] PortalRegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityService.RegisterAsync(request.UserName, request.Password, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] PortalLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            PortalAuthResponse? response = await _identityService.LoginAsync(
                request.UserName, request.Password, cancellationToken);
            return response is null
                ? Unauthorized(new { message = "Usuário ou senha inválidos, ou conta inativa." })
                : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        string? id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out Guid accountId)
            ? Ok(await _identityService.GetAccountAsync(accountId, cancellationToken))
            : Unauthorized();
    }
}