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

            IEnumerable<string> romFiles = Directory
                .EnumerateFiles(
                    systemPath,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(file =>
                    validExtensions.Contains(
                        Path.GetExtension(file),
                        StringComparer.OrdinalIgnoreCase));

            foreach (string romFile in romFiles)
            {
                string romName = Path.GetFileNameWithoutExtension(romFile);

                string imageFileName = $"{romName}.png";

                string physicalImagePath = Path.Combine(
                    imagesPath,
                    imageFileName);

                string imageUrl = string.Empty;

                if (File.Exists(physicalImagePath))
                {
                    string encodedImageName =
                        Uri.EscapeDataString(imageFileName);

                    imageUrl =
                        $"/api/GameSystem/{normalizedSystem}/image/{encodedImageName}";
                }

                GameListDTO game = new GameListDTO
                {
                    Name = romName,
                    ImagePath = imageUrl,
                    RomPath = romFile
                };

                result.Add(game);
            }

            List<GameListDTO> orderedResult = result
                .OrderBy(game => game.Name)
                .ToList();

            return Task.FromResult(orderedResult);
        }

        private static string[] GetValidExtensions(string system)
        {
            if (system.Equals(
                "PSX",
                StringComparison.OrdinalIgnoreCase))
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
    }
}