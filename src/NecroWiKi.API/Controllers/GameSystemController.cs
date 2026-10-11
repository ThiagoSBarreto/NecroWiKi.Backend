using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;

namespace NecroWiKi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameSystemController : ControllerBase
    {
        private const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;

        private readonly IGameSystemService _gameSystemService;

        public GameSystemController(IGameSystemService gameSystemService)
        {
            _gameSystemService = gameSystemService;
        }

        [HttpGet("{system}/roms")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGameList([FromRoute] string system)
        {
            List<GameListDTO> list = await _gameSystemService.GetGameList(system.ToUpperInvariant());
            return Ok(list);
        }

        [HttpGet("{system}/image/{*fileName}")]
        [AllowAnonymous]
        public IActionResult GetImage([FromRoute] string system, [FromRoute] string fileName)
        {
            string imagePath = Path.Combine("/games/ROMS", system.ToUpperInvariant(), "images", fileName);

            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound();
            }

            return PhysicalFile(imagePath, "image/png");
        }

        [HttpGet("{system}/rom/{*fileName}")]
        [AllowAnonymous]
        public IActionResult GetRom([FromRoute] string system, [FromRoute] string fileName)
        {
            string romPath = Path.Combine("/games/ROMS", system.ToUpperInvariant(), fileName);

            if (!System.IO.File.Exists(romPath))
            {
                return NotFound();
            }

            return PhysicalFile(romPath, "application/octet-stream", enableRangeProcessing: true);
        }

        [HttpHead("{system}/rom/{*fileName}")]
        [AllowAnonymous]
        public IActionResult GetRomHeaders([FromRoute] string system, [FromRoute] string fileName)
        {
            string romPath = Path.Combine("/games/ROMS", system.ToUpperInvariant(), fileName);

            if (!System.IO.File.Exists(romPath))
            {
                return NotFound();
            }

            Response.ContentType = "application/octet-stream";
            Response.ContentLength = new FileInfo(romPath).Length;
            Response.Headers.AcceptRanges = "bytes";
            return Ok();
        }

        [HttpPost("{system}/upload")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<IActionResult> UploadGame([FromRoute] string system, [FromForm] string name, [FromForm] List<IFormFile> romFiles, [FromForm] IFormFile? coverFile)
        {
            string result = await _gameSystemService.UploadGame(system.ToUpperInvariant(), name, romFiles, coverFile);
            return Ok(new { message = result });
        }

        [HttpPut("{system}/roms/{currentName}")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<IActionResult> UpdateGame(
            [FromRoute] string system,
            [FromRoute] string currentName,
            [FromForm] string name,
            [FromForm] IFormFile? romFile,
            [FromForm] IFormFile? coverFile)
        {
            try
            {
                string result = await _gameSystemService.UpdateGame(
                    system.ToUpperInvariant(), currentName, name, romFile, coverFile);
                return Ok(new { message = result });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (FileNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
        }
    }
}