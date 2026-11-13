using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;

namespace NecroWiKi.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/tutorials")]
    public class TutorialsController : ControllerBase
    {
        private const long MaxRequestBytes = 128L * 1024 * 1024;
        private readonly ITutorialService _tutorialService;

        public TutorialsController(ITutorialService tutorialService)
        {
            _tutorialService = tutorialService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTutorials([FromQuery] string? category, [FromQuery] string? q)
        {
            try
            {
            return Ok(await _tutorialService.GetTutorials(category, q));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> GetTutorial([FromRoute] string slug)
        {
            TutorialDocument? tutorial = await _tutorialService.GetTutorial(slug);
            return tutorial is null ? NotFound() : Ok(tutorial);
        }

        [HttpGet("{slug}/images/{fileName}")]
        public IActionResult GetImage([FromRoute] string slug, [FromRoute] string fileName)
        {
            TutorialImage? image = _tutorialService.GetImage(slug, fileName);
            return image is null ? NotFound() : PhysicalFile(image.FilePath, image.ContentType);
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> CreateTutorial(
            [FromForm] string title,
            [FromForm] string category,
            [FromForm] string summary,
            [FromForm] string? videoUrl,
            [FromForm] IFormFile markdownFile,
            [FromForm] List<IFormFile>? images)
        {
            try
            {
                if (!TryGetCurrentAccount(out Guid accountId))
                {
                    return Unauthorized();
                }

                TutorialDocument tutorial = await _tutorialService.CreateTutorial(
                    accountId,
                    User.Identity?.Name ?? "",
                    title,
                    category,
                    summary,
                    videoUrl,
                    markdownFile,
                    images);
                return CreatedAtAction(nameof(GetTutorial), new { slug = tutorial.Slug }, tutorial);
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

        [HttpPut("{slug}")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> UpdateTutorial(
            [FromRoute] string slug,
            [FromForm] string title,
            [FromForm] string category,
            [FromForm] string summary,
            [FromForm] string? videoUrl,
            [FromForm] IFormFile? markdownFile,
            [FromForm] List<IFormFile>? images)
        {
            TutorialDocument? existing = await _tutorialService.GetTutorial(slug);
            if (existing is null)
            {
                return NotFound();
            }

            if (!CanManage(existing))
            {
                return Forbid();
            }

            try
            {
                TutorialDocument? updated = await _tutorialService.UpdateTutorial(
                    slug, title, category, summary, videoUrl, markdownFile, images);
                return updated is null ? NotFound() : Ok(updated);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpDelete("{slug}")]
        public async Task<IActionResult> DeleteTutorial([FromRoute] string slug)
        {
            TutorialDocument? existing = await _tutorialService.GetTutorial(slug);
            if (existing is null)
            {
                return NotFound();
            }

            if (!CanManage(existing))
            {
                return Forbid();
            }

            return await _tutorialService.DeleteTutorial(slug) ? NoContent() : NotFound();
        }

        private bool CanManage(TutorialDocument tutorial) =>
            User.IsInRole("Admin") ||
            (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid accountId) && tutorial.OwnerId == accountId);

        private bool TryGetCurrentAccount(out Guid accountId) =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out accountId);
    }
}