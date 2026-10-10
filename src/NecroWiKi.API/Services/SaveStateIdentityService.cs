using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Infrastructure.Persistence;

namespace NecroWiKi.API.Services;

public class SaveStateIdentityService : ISaveStateIdentityService
{
    private const int PasswordIterations = 210_000;
    private readonly AppDbContext _dbContext;
    private readonly ISaveStateService _saveStateService;

    public SaveStateIdentityService(AppDbContext dbContext, ISaveStateService saveStateService)
    {
        _dbContext = dbContext;
        _saveStateService = saveStateService;
    }

    public Task<bool> IsRegisteredAsync(Guid uuid, CancellationToken cancellationToken)
    {
        return _dbContext.SaveStateUsers.AnyAsync(user => user.Uuid == uuid, cancellationToken);
    }

    public async Task<Guid?> ResolveAsync(
        string userName,
        string password,
        Guid? currentUuid,
        CancellationToken cancellationToken)
    {
        string normalizedUserName = userName.Trim().ToUpperInvariant();
        SaveStateUser? existingUser = await _dbContext.SaveStateUsers
            .SingleOrDefaultAsync(user => user.User == normalizedUserName, cancellationToken);

        if (existingUser is not null)
        {
            if (!VerifyPassword(password, existingUser.PasswordHash))
            {
                return null;
            }

            if (currentUuid.HasValue && currentUuid.Value != existingUser.Uuid)
            {
                await _saveStateService.MergeAsync(currentUuid.Value, existingUser.Uuid, cancellationToken);
            }

            return existingUser.Uuid;
        }

        if (currentUuid.HasValue && await _dbContext.SaveStateUsers
            .AnyAsync(user => user.Uuid == currentUuid.Value, cancellationToken))
        {
            return null;
        }

        SaveStateUser newUser = new()
        {
            User = normalizedUserName,
            PasswordHash = HashPassword(password),
            Uuid = currentUuid ?? Guid.NewGuid()
        };

        _dbContext.SaveStateUsers.Add(newUser);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return newUser.Uuid;
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(newUser).State = EntityState.Detached;
            existingUser = await _dbContext.SaveStateUsers
                .SingleOrDefaultAsync(user => user.User == normalizedUserName, cancellationToken);

            if (existingUser is null || !VerifyPassword(password, existingUser.PasswordHash))
            {
                return null;
            }

            if (currentUuid.HasValue && currentUuid.Value != existingUser.Uuid)
            {
                await _saveStateService.MergeAsync(currentUuid.Value, existingUser.Uuid, cancellationToken);
            }

            return existingUser.Uuid;
        }
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            32);

        return $"pbkdf2-sha256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        string[] parts = storedHash.Split('$');
        if (parts.Length != 4
            || parts[0] != "pbkdf2-sha256"
            || !int.TryParse(parts[1], out int iterations)
            || iterations < 100_000
            || iterations > 1_000_000)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expectedHash = Convert.FromBase64String(parts[3]);
            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}