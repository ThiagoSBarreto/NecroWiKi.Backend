using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;

namespace NecroWiKi.Application.Services
{
    public class TutorialService : ITutorialService
    {
        private const long MaxMarkdownBytes = 2L * 1024 * 1024;
        private const long MaxImageBytes = 10L * 1024 * 1024;
        private const int MaxImageCount = 12;

        private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
        {
            "wow",
            "ragnarok",
            "warcraft3",
            "diablo2",
            "arcade"
        };

        private static readonly Dictionary<string, string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".webm",
            ".ogg"
        };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly string _rootPath;

        public TutorialService(IOptions<TutorialStorageSettings> settings)
        {
            _rootPath = Path.GetFullPath(settings.Value.RootPath);
        }

        public async Task<List<TutorialListItem>> GetTutorials(string? category, string? search)
        {
            string? normalizedCategory = null;
            if (!string.IsNullOrWhiteSpace(category))
            {
                normalizedCategory = ValidateCategory(category);
            }

            string? normalizedSearch = search?.Trim();
            if (normalizedSearch?.Length > 100)
            {
                throw new ArgumentException("A busca deve ter no máximo 100 caracteres.");
            }

            if (!Directory.Exists(_rootPath))
            {
                return new List<TutorialListItem>();
            }

            List<TutorialListItem> tutorials = new();
            foreach (string directory in Directory.EnumerateDirectories(_rootPath))
            {
                TutorialMetadata? metadata = await ReadMetadata(directory);
                if (metadata is null || (normalizedCategory is not null &&
                    !string.Equals(metadata.Category, normalizedCategory, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(normalizedSearch) &&
                    !metadata.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
                    !metadata.Summary.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                {
                    string markdownPath = Path.Combine(directory, "index.md");
                    if (!File.Exists(markdownPath) || !(await File.ReadAllTextAsync(markdownPath))
                        .Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                tutorials.Add(ToListItem(metadata));
            }

            return tutorials.OrderByDescending(tutorial => tutorial.UpdatedAt).ToList();
        }

        public async Task<TutorialDocument?> GetTutorial(string slug)
        {
            if (!IsValidSlug(slug))
            {
                return null;
            }

            string directory = GetTutorialDirectory(slug);
            TutorialMetadata? metadata = await ReadMetadata(directory);
            string markdownPath = Path.Combine(directory, "index.md");
            if (metadata is null || !File.Exists(markdownPath))
            {
                return null;
            }

            TutorialListItem listItem = ToListItem(metadata);
            return new TutorialDocument
            {
                Slug = listItem.Slug,
                Title = listItem.Title,
                Category = listItem.Category,
                Summary = listItem.Summary,
                VideoUrl = listItem.VideoUrl,
                OwnerId = listItem.OwnerId,
                OwnerName = listItem.OwnerName,
                UpdatedAt = listItem.UpdatedAt,
                Markdown = await File.ReadAllTextAsync(markdownPath)
            };
        }

        public async Task<TutorialDocument> CreateTutorial(
            Guid ownerId,
            string ownerName,
            string title,
            string category,
            string summary,
            string? videoUrl,
            IFormFile markdownFile,
            List<IFormFile>? images)
        {
            string normalizedTitle = ValidateTitle(title);
            string normalizedCategory = ValidateCategory(category);
            string normalizedSummary = ValidateSummary(summary);
            string? normalizedVideoUrl = ValidateVideoUrl(videoUrl);

            if (markdownFile is null || markdownFile.Length == 0 || markdownFile.Length > MaxMarkdownBytes ||
                !string.Equals(Path.GetExtension(markdownFile.FileName), ".md", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Envie um arquivo Markdown (.md) de até 2 MB.");
            }

            List<(IFormFile File, string Name)> imageFiles = ValidateImages(images);
            string slug = CreateSlug(normalizedTitle);
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = $"tutorial-{Guid.NewGuid():N}";
            }

            Directory.CreateDirectory(_rootPath);
            string tutorialDirectory = GetTutorialDirectory(slug);
            if (Directory.Exists(tutorialDirectory))
            {
                throw new InvalidOperationException("Já existe um tutorial com esse título.");
            }

            string stagingDirectory = Path.Combine(_rootPath, $".{slug}-{Guid.NewGuid():N}.tmp");
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TutorialMetadata metadata = new()
            {
                Slug = slug,
                Title = normalizedTitle,
                Category = normalizedCategory,
                Summary = normalizedSummary,
                VideoUrl = normalizedVideoUrl,
                OwnerId = ownerId,
                OwnerName = ownerName,
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                Directory.CreateDirectory(Path.Combine(stagingDirectory, "images"));

                string markdownPath = Path.Combine(stagingDirectory, "index.md");
                await using (FileStream stream = new(markdownPath, FileMode.CreateNew))
                {
                    await markdownFile.CopyToAsync(stream);
                }

                foreach ((IFormFile file, string name) in imageFiles)
                {
                    string imagePath = Path.Combine(stagingDirectory, "images", name);
                    await using FileStream stream = new(imagePath, FileMode.CreateNew);
                    await file.CopyToAsync(stream);
                }

                string metadataPath = Path.Combine(stagingDirectory, "metadata.json");
                await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions));
                Directory.Move(stagingDirectory, tutorialDirectory);
            }
            catch
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }

                throw;
            }

            return (await GetTutorial(slug))!;
        }

        public async Task<TutorialDocument?> UpdateTutorial(
            string slug,
            string title,
            string category,
            string summary,
            string? videoUrl,
            IFormFile? markdownFile,
            List<IFormFile>? images)
        {
            if (!IsValidSlug(slug))
            {
                return null;
            }

            string tutorialDirectory = GetTutorialDirectory(slug);
            TutorialMetadata? metadata = await ReadMetadata(tutorialDirectory);
            if (metadata is null)
            {
                return null;
            }

            string normalizedTitle = ValidateTitle(title);
            string normalizedCategory = ValidateCategory(category);
            string normalizedSummary = ValidateSummary(summary);
            string? normalizedVideoUrl = ValidateVideoUrl(videoUrl);
            if (markdownFile is not null &&
                (markdownFile.Length == 0 || markdownFile.Length > MaxMarkdownBytes ||
                 !string.Equals(Path.GetExtension(markdownFile.FileName), ".md", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Envie um arquivo Markdown (.md) de até 2 MB.");
            }

            List<(IFormFile File, string Name)> imageFiles = ValidateImages(images);
            List<string> temporaryFiles = new();

            try
            {
                if (markdownFile is not null)
                {
                    string temporaryMarkdown = await SaveTemporaryFile(tutorialDirectory, markdownFile);
                    temporaryFiles.Add(temporaryMarkdown);
                    File.Move(temporaryMarkdown, Path.Combine(tutorialDirectory, "index.md"), overwrite: true);
                    temporaryFiles.Remove(temporaryMarkdown);
                }

                if (imageFiles.Count > 0)
                {
                    string imagesPath = Path.Combine(tutorialDirectory, "images");
                    Directory.CreateDirectory(imagesPath);
                    foreach ((IFormFile file, string name) in imageFiles)
                    {
                        string temporaryImage = await SaveTemporaryFile(imagesPath, file);
                        temporaryFiles.Add(temporaryImage);
                        File.Move(temporaryImage, Path.Combine(imagesPath, name), overwrite: true);
                        temporaryFiles.Remove(temporaryImage);
                    }
                }

                metadata.Title = normalizedTitle;
                metadata.Category = normalizedCategory;
                metadata.Summary = normalizedSummary;
                metadata.VideoUrl = normalizedVideoUrl;
                metadata.UpdatedAt = DateTimeOffset.UtcNow;

                string temporaryMetadata = Path.Combine(tutorialDirectory, $".{Guid.NewGuid():N}.tmp");
                temporaryFiles.Add(temporaryMetadata);
                await File.WriteAllTextAsync(temporaryMetadata, JsonSerializer.Serialize(metadata, JsonOptions));
                File.Move(temporaryMetadata, Path.Combine(tutorialDirectory, "metadata.json"), overwrite: true);
                temporaryFiles.Remove(temporaryMetadata);
            }
            finally
            {
                foreach (string temporaryFile in temporaryFiles)
                {
                    if (File.Exists(temporaryFile))
                    {
                        File.Delete(temporaryFile);
                    }
                }
            }

            return await GetTutorial(slug);
        }

        public async Task<bool> DeleteTutorial(string slug)
        {
            if (!IsValidSlug(slug))
            {
                return false;
            }

            string tutorialDirectory = GetTutorialDirectory(slug);
            if (await ReadMetadata(tutorialDirectory) is null)
            {
                return false;
            }

            Directory.Delete(tutorialDirectory, recursive: true);
            return true;
        }

        public TutorialImage? GetImage(string slug, string fileName)
        {
            if (!IsValidSlug(slug) || string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            {
                return null;
            }

            string extension = Path.GetExtension(fileName);
            if (!ImageContentTypes.TryGetValue(extension, out string? contentType))
            {
                return null;
            }

            string imagePath = Path.GetFullPath(Path.Combine(GetTutorialDirectory(slug), "images", fileName));
            string imageRoot = Path.GetFullPath(Path.Combine(GetTutorialDirectory(slug), "images")) + Path.DirectorySeparatorChar;
            if (!imagePath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(imagePath))
            {
                return null;
            }

            return new TutorialImage { FilePath = imagePath, ContentType = contentType };
        }

        private string GetTutorialDirectory(string slug) => Path.Combine(_rootPath, slug);

        private static async Task<string> SaveTemporaryFile(string directory, IFormFile file)
        {
            string temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
            try
            {
                await using FileStream stream = new(temporaryPath, FileMode.CreateNew);
                await file.CopyToAsync(stream);
                return temporaryPath;
            }
            catch
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                throw;
            }
        }

        private static async Task<TutorialMetadata?> ReadMetadata(string directory)
        {
            string metadataPath = Path.Combine(directory, "metadata.json");
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                string json = await File.ReadAllTextAsync(metadataPath);
                return JsonSerializer.Deserialize<TutorialMetadata>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static TutorialListItem ToListItem(TutorialMetadata metadata) => new()
        {
            Slug = metadata.Slug,
            Title = metadata.Title,
            Category = metadata.Category,
            Summary = metadata.Summary,
            VideoUrl = metadata.VideoUrl,
            OwnerId = metadata.OwnerId,
            OwnerName = metadata.OwnerName,
            UpdatedAt = metadata.UpdatedAt
        };

        private static string ValidateTitle(string title)
        {
            string normalizedTitle = title?.Trim() ?? string.Empty;
            if (normalizedTitle.Length is 0 or > 120)
            {
                throw new ArgumentException("O título deve ter entre 1 e 120 caracteres.");
            }

            return normalizedTitle;
        }

        private static string ValidateCategory(string category)
        {
            string normalizedCategory = category?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!Categories.Contains(normalizedCategory))
            {
                throw new ArgumentException("Categoria de tutorial inválida.");
            }

            return normalizedCategory;
        }

        private static string ValidateSummary(string summary)
        {
            string normalizedSummary = summary?.Trim() ?? string.Empty;
            if (normalizedSummary.Length > 360)
            {
                throw new ArgumentException("O resumo deve ter no máximo 360 caracteres.");
            }

            return normalizedSummary;
        }

        private static string? ValidateVideoUrl(string? videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                return null;
            }

            if (!Uri.TryCreate(videoUrl.Trim(), UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !VideoExtensions.Contains(Path.GetExtension(uri.AbsolutePath)))
            {
                throw new ArgumentException("O vídeo precisa ser uma URL direta de arquivo MP4, WebM ou OGG.");
            }

            return uri.ToString();
        }

        private static List<(IFormFile File, string Name)> ValidateImages(List<IFormFile>? images)
        {
            if (images is null || images.Count == 0)
            {
                return new List<(IFormFile, string)>();
            }

            if (images.Count > MaxImageCount)
            {
                throw new ArgumentException($"Envie no máximo {MaxImageCount} imagens por tutorial.");
            }

            List<(IFormFile File, string Name)> result = new();
            HashSet<string> fileNames = new(StringComparer.OrdinalIgnoreCase);

            foreach (IFormFile image in images)
            {
                string fileName = Path.GetFileName(image.FileName);
                string extension = Path.GetExtension(fileName);
                if (image.Length == 0 || image.Length > MaxImageBytes ||
                    !ImageContentTypes.ContainsKey(extension) ||
                    !string.Equals(fileName, image.FileName, StringComparison.Ordinal) ||
                    !fileNames.Add(fileName))
                {
                    throw new ArgumentException("As imagens devem ser PNG, JPG, GIF ou WebP, ter até 10 MB e nomes únicos.");
                }

                result.Add((image, fileName));
            }

            return result;
        }

        private static string CreateSlug(string title)
        {
            StringBuilder slug = new();
            bool needsSeparator = false;

            foreach (char character in title.Normalize(NormalizationForm.FormD))
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsAsciiLetterOrDigit(character))
                {
                    if (needsSeparator && slug.Length > 0)
                    {
                        slug.Append('-');
                    }

                    slug.Append(char.ToLowerInvariant(character));
                    needsSeparator = false;
                }
                else
                {
                    needsSeparator = true;
                }
            }

            return slug.ToString();
        }

        private static bool IsValidSlug(string slug) =>
            !string.IsNullOrWhiteSpace(slug) && slug.Length <= 120 &&
            slug.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }
}