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

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Nome do jogo não informado.");
            }

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
                string romFileName = Path.GetFileName(romFile.FileName);
                string romPath = Path.Combine(systemPath, romFileName);

                await using FileStream stream = new FileStream(romPath, FileMode.Create);
                await romFile.CopyToAsync(stream);
            }

            if (coverFile != null && coverFile.Length > 0)
            {
                string coverPath = Path.Combine(imagesPath, $"{name.Trim()}.png");

                await using FileStream stream = new FileStream(coverPath, FileMode.Create);
                await coverFile.CopyToAsync(stream);
            }

            return "success";
        }
    }
}