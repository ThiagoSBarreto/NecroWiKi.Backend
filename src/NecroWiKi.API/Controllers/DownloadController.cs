using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NecroWiKi.Application.Interfaces;

namespace NecroWiKi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DownloadController : ControllerBase
{
    private readonly IDownloadService _downloadService;

    public DownloadController (
        IDownloadService downloadService
        )
    {
        _downloadService = downloadService;
    }

    [HttpGet("{gamename}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName) ||
            gameName.Any(character =>
                !char.IsLetterOrDigit(character) &&
                character != '_' &&
                character != '-' &&
                character != '.'))
        {
            return BadRequest();
        }

        Stream stream = await _downloadService.DownloadGame(gameName);
        if (stream == null)
        {
            return NotFound();
        }

        return File(stream, "application/zip", $"{gameName}.zip", enableRangeProcessing: true);
    }
}