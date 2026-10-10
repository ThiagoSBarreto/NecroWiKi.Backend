using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NecroWiKi.Application.Interfaces;

namespace NecroWiKi.Application.Services;

public class SaveStateService : ISaveStateService
{
    private readonly string _rootPath;

    public SaveStateService(IConfiguration configuration)
    {
        _rootPath = Path.GetFullPath(
            configuration["SaveStates:Path"] ?? Path.Combine(AppContext.BaseDirectory, "SaveStates"));
    }

    public async Task SaveAsync(Guid browserId, string gameKey, Stream content, CancellationToken cancellationToken)
    {
        string path = GetSavePath(browserId, gameKey);
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await content.CopyToAsync(output, cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task<Stream?> LoadAsync(Guid browserId, string gameKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetSavePath(browserId, gameKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public async Task MergeAsync(Guid sourceUuid, Guid targetUuid, CancellationToken cancellationToken)
    {
        if (sourceUuid == targetUuid)
        {
            return;
        }

        string sourceDirectory = Path.Combine(_rootPath, sourceUuid.ToString("N"));
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        string targetDirectory = Path.Combine(_rootPath, targetUuid.ToString("N"));
        Directory.CreateDirectory(targetDirectory);

        foreach (string sourcePath in Directory.EnumerateFiles(sourceDirectory, "*.state"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));
            if (File.Exists(targetPath))
            {
                continue;
            }

            string temporaryPath = Path.Combine(targetDirectory, $"{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
                await using (var target = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await source.CopyToAsync(target, cancellationToken);
                }

                try
                {
                    File.Move(temporaryPath, targetPath);
                }
                catch (IOException) when (File.Exists(targetPath))
                {
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    private string GetSavePath(Guid browserId, string gameKey)
    {
        byte[] keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(gameKey));
        string fileName = Convert.ToHexString(keyHash).ToLowerInvariant() + ".state";
        return Path.Combine(_rootPath, browserId.ToString("N"), fileName);
    }
}