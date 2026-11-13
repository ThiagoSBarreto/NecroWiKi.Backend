using Microsoft.AspNetCore.Http;
using NecroWiKi.Application.Models;

namespace NecroWiKi.Application.Interfaces
{
    public interface ITutorialService
    {
        Task<List<TutorialListItem>> GetTutorials(string? category, string? search);
        Task<TutorialDocument?> GetTutorial(string slug);
        Task<TutorialDocument> CreateTutorial(
            Guid ownerId,
            string ownerName,
            string title,
            string category,
            string summary,
            string? videoUrl,
            IFormFile markdownFile,
            List<IFormFile>? images);
        Task<TutorialDocument?> UpdateTutorial(
            string slug,
            string title,
            string category,
            string summary,
            string? videoUrl,
            IFormFile? markdownFile,
            List<IFormFile>? images);
        Task<bool> DeleteTutorial(string slug);
        TutorialImage? GetImage(string slug, string fileName);
    }
}