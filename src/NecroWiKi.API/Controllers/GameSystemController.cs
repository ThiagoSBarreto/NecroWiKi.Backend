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

        public GameSystemController(
            IGameSystemService gameSystemService
            )
        {
            _gameSystemService = gameSystemService;
        }

        [HttpGet("{system}/roms")]
        public async Task<IActionResult> GetGameList([FromRoute] string system)
        {
            List<GameListDTO> list = await _gameSystemService.GetGameList(system);
            return Ok(list);
        }

        [HttpGet("{system}/image/{*fileName}")]
        public IActionResult GetImage(string system, string fileName)
        {
            string imagePath = Path.Combine(
                "/games/ROMS",
                system.ToUpper(),
                "images",
                fileName);

            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound();
            }

            return PhysicalFile(imagePath, "image/png");
        }
    }
}