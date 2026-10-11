using Microsoft.AspNetCore.Http;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NecroWiKi.Application.Services
{
    public class GameSystemService : IGameSystemService
    {
        private const string GamesRootPath = "/games/ROMS";

        public Task<List<GameListDTO>> GetGameList(string system)
        {
            List<GameListDTO> result = new List<GameListDTO>();

            if (string.IsNullOrWhiteSpace(system))
            {
                return Task.FromResult(result);
            }

            string normalizedSystem = system.ToUpperInvariant();
            string systemPath = Path.Combine(GamesRootPath, normalizedSystem);
            string imagesPath = Path.Combine(systemPath, "images");

            if (!Directory.Exists(systemPath))
            {
                return Task.FromResult(result);
            }

            string[] validExtensions = GetValidExtensions(normalizedSystem);

            IEnumerable<string> romFiles = Directory.EnumerateFiles(systemPath, "*", SearchOption.TopDirectoryOnly)
                .Where(file => validExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));

            foreach (string romFile in romFiles)
            {
                string romName = Path.GetFileNameWithoutExtension(romFile);
                string imageFileName = $"{romName}.png";
                string physicalImagePath = Path.Combine(imagesPath, imageFileName);
                string imageUrl = string.Empty;

                if (File.Exists(physicalImagePath))
                {
                    string encodedImageName = Uri.EscapeDataString(imageFileName);
                    imageUrl = $"/api/GameSystem/{normalizedSystem}/image/{encodedImageName}";
                }

                string encodedRomName = Uri.EscapeDataString(Path.GetFileName(romFile));
                string romUrl = $"/api/GameSystem/{normalizedSystem}/rom/{encodedRomName}";

                GameListDTO game = new GameListDTO
                {
                    Name = romName,
                    ImagePath = imageUrl,
                    RomPath = romUrl
                };

                result.Add(game);
            }

            List<GameListDTO> orderedResult = result.OrderBy(game => game.Name).ToList();
            return Task.FromResult(orderedResult);
        }

        private static string[] GetValidExtensions(string system)
        {
            if (system.Equals("PSX", StringComparison.OrdinalIgnoreCase))
            {
                return new string[]
                {
                    ".bin"
                };
            }

            return new string[]
            {
                ".z64",
                ".n64",
                ".v64",
                ".nes",
                ".sfc",
                ".smc",
                ".gba",
                ".gb",
                ".gbc",
                ".nds",
                ".iso",
                ".cue",
                ".chd"
            };
        }

        public async Task<string> UploadGame(string system, string name, List<IFormFile> romFiles, IFormFile? coverFile)
        {
            if (string.IsNullOrWhiteSpace(system))
            {
                throw new ArgumentException("Sistema não informado.");
            }

            string normalizedName = ValidateGameName(name, "Nome do jogo");

            if (romFiles == null || romFiles.Count == 0)
            {
                throw new ArgumentException("Nenhum arquivo ROM informado.");
            }

            string normalizedSystem = system.ToUpperInvariant();
            string systemPath = Path.Combine(GamesRootPath, normalizedSystem);
            string imagesPath = Path.Combine(systemPath, "images");

            Directory.CreateDirectory(systemPath);
            Directory.CreateDirectory(imagesPath);

            foreach (IFormFile romFile in romFiles)
            {
                string extension = Path.GetExtension(romFile.FileName);

                if (string.IsNullOrWhiteSpace(extension))
                {
                    continue;
                }

                string romFileName = $"{normalizedName}{extension}";
                string romPath = Path.Combine(systemPath, romFileName);

                await using FileStream stream = new FileStream(romPath, FileMode.Create);
                await romFile.CopyToAsync(stream);
            }

            if (coverFile != null && coverFile.Length > 0)
            {
                string coverPath = Path.Combine(imagesPath, $"{normalizedName}.png");

                await using FileStream stream = new FileStream(coverPath, FileMode.Create);
                await coverFile.CopyToAsync(stream);
            }

            return "success";
        }

        public async Task<string> UpdateGame(
            string system,
            string currentName,
            string name,
            IFormFile? romFile,
            IFormFile? coverFile)
        {
            if (string.IsNullOrWhiteSpace(system))
            {
                throw new ArgumentException("Sistema não informado.");
            }

            string normalizedSystem = system.ToUpperInvariant();
            string currentGameName = ValidateGameName(currentName, "Nome atual do jogo");
            string newGameName = ValidateGameName(name, "Nome do jogo");
            string systemPath = Path.Combine(GamesRootPath, normalizedSystem);
            string imagesPath = Path.Combine(systemPath, "images");

            if (!Directory.Exists(systemPath))
            {
                throw new FileNotFoundException("Sistema ou jogo não encontrado.");
            }

            string[] validExtensions = GetValidExtensions(normalizedSystem);
            List<string> existingRomPaths = Directory.EnumerateFiles(systemPath, "*", SearchOption.TopDirectoryOnly)
                .Where(file => validExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Where(file => string.Equals(
                    Path.GetFileNameWithoutExtension(file), currentGameName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (existingRomPaths.Count == 0)
            {
                throw new FileNotFoundException("Jogo não encontrado.");
            }

            string existingCoverPath = Path.Combine(imagesPath, $"{currentGameName}.png");
            bool hasExistingCover = File.Exists(existingCoverPath);
            string newCoverPath = Path.Combine(imagesPath, $"{newGameName}.png");
            string? newRomPath = null;

            if (romFile is not null)
            {
                if (romFile.Length == 0)
                {
                    throw new ArgumentException("O arquivo ROM está vazio.");
                }

                string extension = Path.GetExtension(romFile.FileName);
                if (!validExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Formato de ROM não suportado para este sistema.");
                }

                newRomPath = Path.Combine(systemPath, $"{newGameName}{extension}");
            }

            if (coverFile is not null && coverFile.Length > 0 &&
                !string.Equals(Path.GetExtension(coverFile.FileName), ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("A capa deve estar no formato PNG.");
            }

            HashSet<string> sourcePaths = new(existingRomPaths, StringComparer.Ordinal);
            if (hasExistingCover)
            {
                sourcePaths.Add(existingCoverPath);
            }

            IEnumerable<string> romTargetPaths = newRomPath is null
                ? existingRomPaths.Select(path => Path.Combine(systemPath, $"{newGameName}{Path.GetExtension(path)}"))
                : new[] { newRomPath };

            foreach (string targetPath in romTargetPaths)
            {
                EnsureTargetAvailable(targetPath, sourcePaths);
            }

            if (coverFile is not null || hasExistingCover)
            {
                EnsureTargetAvailable(newCoverPath, sourcePaths);
            }

            string? temporaryRomPath = null;
            string? temporaryCoverPath = null;

            try
            {
                if (romFile is not null && newRomPath is not null)
                {
                    temporaryRomPath = await SaveTemporaryFile(systemPath, romFile);
                }

                if (coverFile is not null && coverFile.Length > 0)
                {
                    Directory.CreateDirectory(imagesPath);
                    temporaryCoverPath = await SaveTemporaryFile(imagesPath, coverFile);
                }

                if (temporaryRomPath is not null && newRomPath is not null)
                {
                    File.Move(temporaryRomPath, newRomPath, overwrite: true);
                    temporaryRomPath = null;

                    foreach (string existingRomPath in existingRomPaths.Where(path => !string.Equals(path, newRomPath, StringComparison.Ordinal)))
                    {
                        File.Delete(existingRomPath);
                    }
                }
                else
                {
                    foreach (string existingRomPath in existingRomPaths)
                    {
                        string targetPath = Path.Combine(systemPath, $"{newGameName}{Path.GetExtension(existingRomPath)}");
                        if (!string.Equals(existingRomPath, targetPath, StringComparison.Ordinal))
                        {
                            File.Move(existingRomPath, targetPath);
                        }
                    }
                }

                if (temporaryCoverPath is not null)
                {
                    File.Move(temporaryCoverPath, newCoverPath, overwrite: true);
                    temporaryCoverPath = null;

                    if (hasExistingCover && !string.Equals(existingCoverPath, newCoverPath, StringComparison.Ordinal))
                    {
                        File.Delete(existingCoverPath);
                    }
                }
                else if (hasExistingCover && !string.Equals(existingCoverPath, newCoverPath, StringComparison.Ordinal))
                {
                    File.Move(existingCoverPath, newCoverPath);
                }

                return "success";
            }
            finally
            {
                DeleteTemporaryFile(temporaryRomPath);
                DeleteTemporaryFile(temporaryCoverPath);
            }
        }

        private static string ValidateGameName(string name, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{fieldName} não informado.");
            }

            string normalizedName = name.Trim();
            if (normalizedName is "." or ".." ||
                normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                normalizedName.Contains('/') || normalizedName.Contains('\\'))
            {
                throw new ArgumentException($"{fieldName} inválido.");
            }

            return normalizedName;
        }

        private static void EnsureTargetAvailable(string targetPath, HashSet<string> sourcePaths)
        {
            if (File.Exists(targetPath) && !sourcePaths.Contains(targetPath))
            {
                throw new InvalidOperationException("Já existe um jogo com esse nome.");
            }
        }

        private static async Task<string> SaveTemporaryFile(string directoryPath, IFormFile file)
        {
            string temporaryPath = Path.Combine(directoryPath, $".{Guid.NewGuid():N}.tmp");
            try
            {
                await using FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew);
                await file.CopyToAsync(stream);
                return temporaryPath;
            }
            catch
            {
                DeleteTemporaryFile(temporaryPath);
                throw;
            }
        }

        private static void DeleteTemporaryFile(string? path)
        {
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}