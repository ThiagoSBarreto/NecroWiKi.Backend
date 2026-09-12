using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class GameSystemService : IGameSystemService
{
    private const string GamesRootPath = "/games/ROMS";

    public async Task<List<GameListDTO>> GetGameList(string system)
    {
        List<GameListDTO> result = new List<GameListDTO>();

        if (string.IsNullOrWhiteSpace(system))
        {
            return result;
        }

        string systemPath = Path.Combine(GamesRootPath, system.ToUpper());
        string imagesPath = Path.Combine(systemPath, "images");

        if (!Directory.Exists(systemPath))
        {
            return result;
        }

        string[] validExtensions = GetValidExtensions(system);

        IEnumerable<string> romFiles = Directory
            .EnumerateFiles(systemPath, "*", SearchOption.TopDirectoryOnly)
            .Where(file =>
                validExtensions.Contains(
                    Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase));

        foreach (string romFile in romFiles)
        {
            string romName = Path.GetFileNameWithoutExtension(romFile);
            string imagePath = Path.Combine(imagesPath, $"{romName}.png");

            GameListDTO game = new GameListDTO
            {
                Name = romName,
                RomPath = romFile,
                ImagePath = File.Exists(imagePath)
                    ? imagePath
                    : string.Empty
            };

            result.Add(game);
        }

        return await Task.FromResult(
            result
                .OrderBy(game => game.Name)
                .ToList());
    }

    private static string[] GetValidExtensions(string system)
    {
        if (system.Equals("psx", StringComparison.OrdinalIgnoreCase))
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