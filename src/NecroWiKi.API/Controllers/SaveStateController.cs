using Microsoft.AspNetCore.Mvc;
using NecroWiKi.Application.Interfaces;

namespace NecroWiKi.API.Controllers;

[ApiController]
[Route("api/savestate")]
public class SaveStateController : ControllerBase
{
    private const long MaxSaveStateBytes = 64L * 1024 * 1024;
    private readonly ISaveStateService _saveStateService;

    public SaveStateController(ISaveStateService saveStateService)
    {
        _saveStateService = saveStateService;
    }

    [HttpPut("{browserId:guid}")]
    [RequestSizeLimit(MaxSaveStateBytes)]
    public async Task<IActionResult> Save(
        [FromRoute] Guid browserId,
        [FromQuery] string gameKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            return BadRequest();
        }

        if (Request.ContentLength > MaxSaveStateBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        await _saveStateService.SaveAsync(browserId, gameKey, Request.Body, cancellationToken);
        return NoContent();
    }

    [HttpGet("{browserId:guid}")]
    public async Task<IActionResult> Load(
        [FromRoute] Guid browserId,
        [FromQuery] string gameKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            return BadRequest();
        }

        Stream? state = await _saveStateService.LoadAsync(browserId, gameKey, cancellationToken);
        return state is null ? NotFound() : File(state, "application/octet-stream");
    }
}