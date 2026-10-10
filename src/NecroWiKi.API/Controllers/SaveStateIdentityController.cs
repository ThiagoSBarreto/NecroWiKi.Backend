using Microsoft.AspNetCore.Mvc;
using NecroWiKi.Application.Interfaces;

namespace NecroWiKi.API.Controllers;

[ApiController]
[Route("api/savestateuser")]
public class SaveStateIdentityController : ControllerBase
{
    private readonly ISaveStateIdentityService _identityService;

    public SaveStateIdentityController(ISaveStateIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet("{uuid:guid}")]
    public async Task<IActionResult> Check([FromRoute] Guid uuid, CancellationToken cancellationToken)
    {
        return await _identityService.IsRegisteredAsync(uuid, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(
        [FromBody] ResolveSaveStateIdentityRequest request,
        CancellationToken cancellationToken)
    {
        string userName = request.User?.Trim() ?? string.Empty;
        if (userName.Length is < 3 or > 64 || request.Password is null || request.Password.Length is < 8 or > 128)
        {
            return BadRequest(new { message = "Informe um usuário e uma senha válida." });
        }

        Guid? uuid = await _identityService.ResolveAsync(
            userName,
            request.Password,
            request.CurrentUuid,
            cancellationToken);

        return uuid.HasValue
            ? Ok(new { uuid = uuid.Value })
            : Unauthorized(new { message = "Usuário ou senha inválidos." });
    }
}

public sealed class ResolveSaveStateIdentityRequest
{
    public string? User { get; init; }
    public string? Password { get; init; }
    public Guid? CurrentUuid { get; init; }
}