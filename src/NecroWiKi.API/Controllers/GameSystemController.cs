using Microsoft.AspNetCore.Mvc;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;

namespace NecroWiKi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameSystemController : ControllerBase
    {
        private readonly IGameSystemService _gameSystemService;

        public GameSystemController(IGameSystemService gameSystemService)
        {
            _gameSystemService = gameSystemService;
        }

        [HttpGet("{system}/roms")]
        public async Task<IActionResult> GetGameList([FromRoute] string system)
        {
            List<GameListDTO> list = await _gameSystemService.GetGameList(system.ToUpperInvariant());
            return Ok(list);
        }

        [HttpGet("{system}/image/{*fileName}")]
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
        public IActionResult GetRom([FromRoute] string system, [FromRoute] string fileName)
        {
            string romPath = Path.Combine("/games/ROMS", system.ToUpperInvariant(), fileName);

            if (!System.IO.File.Exists(romPath))
            {
                return NotFound();
            }

            return PhysicalFile(romPath, "application/octet-stream", enableRangeProcessing: true);
        }

        [HttpPost("{system}/upload")]
        public async Task<IActionResult> UploadGame([FromRoute] string system, [FromForm] string name, [FromForm] List<IFormFile> romFiles, [FromForm] IFormFile? coverFile)
        {
            string result = await _gameSystemService.UploadGame(system.ToUpperInvariant(), name, romFiles, coverFile);
            return Ok(new { message = result });
        }
    }
}