using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NecroWiKi.API.Models;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Infrastructure.Persistence;

namespace NecroWiKi.API.Services;

public sealed class PortalIdentityService
{
    private const int PasswordIterations = 310_000;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ISaveStateIdentityService _saveStateIdentityService;
    private static volatile bool _schemaReady;

    public PortalIdentityService(
        AppDbContext dbContext,
        IConfiguration configuration,
        ISaveStateIdentityService saveStateIdentityService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _saveStateIdentityService = saveStateIdentityService;
    }

    public async Task<PortalAuthResponse> RegisterAsync(string? userName, string? password, CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(cancellationToken);
        string normalizedName = ValidateUserName(userName);
        string validatedPassword = ValidatePassword(password);

        if (await _dbContext.PortalAccounts.AnyAsync(account => account.NormalizedUserName == normalizedName, cancellationToken))
        {
            throw new InvalidOperationException("Esse nome de usuário já está em uso.");
        }

        Guid? legacyId = await _saveStateIdentityService.ResolveAsync(
            normalizedName,
            validatedPassword,
            null,
            cancellationToken);
        if (!legacyId.HasValue)
        {
            throw new InvalidOperationException("Não foi possível validar a conta.");
        }

        PortalAccount account = new()
        {
            Id = legacyId.Value,
            UserName = userName!.Trim(),
            NormalizedUserName = normalizedName,
            PasswordHash = HashPassword(validatedPassword),
            Role = "User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PortalAccounts.Add(account);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Esse nome de usuário já está em uso.");
        }

        return CreateAuthResponse(account);
    }

    public async Task<PortalAuthResponse?> LoginAsync(string? userName, string? password, CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(cancellationToken);
        string normalizedName = NormalizeUserName(userName);
        PortalAccount? account = await _dbContext.PortalAccounts
            .SingleOrDefaultAsync(item => item.NormalizedUserName == normalizedName, cancellationToken);

        if (account is null || !account.IsActive || !VerifyPassword(password ?? string.Empty, account.PasswordHash))
        {
            return null;
        }

        return CreateAuthResponse(account);
    }

    public async Task<PortalAccountSummary?> GetAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(cancellationToken);
        PortalAccount? account = await _dbContext.PortalAccounts.FindAsync([id], cancellationToken);
        return account is null || !account.IsActive ? null : ToSummary(account);
    }

    public async Task<List<PortalAccountSummary>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(cancellationToken);
        return await _dbContext.PortalAccounts.AsNoTracking()
            .OrderBy(account => account.NormalizedUserName)
            .Select(account => new PortalAccountSummary(
                account.Id, account.UserName, account.Role, account.IsActive, account.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<PortalAccountSummary> CreateAccountAsync(
        string? userName,
        string? password,
        string? role,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(cancellationToken);
        string normalizedName = ValidateUserName(userName);
        string validatedPassword = ValidatePassword(password);
        string normalizedRole = ValidateRole(role);

        if (await _dbContext.PortalAccounts.AnyAsync(account => account.NormalizedUserName == normalizedName, cancellationToken))
        {
            throw new InvalidOperationException("Esse nome de usuário já está em uso.");
        }

        PortalAccount account = new()
        {
            Id = Guid.NewGuid(),
            UserName = userName!.Trim(),
            NormalizedUserName = normalizedName,
            PasswordHash = HashPassword(validatedPassword),
            Role = normalizedRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PortalAccounts.Add(account);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Esse nome de usuário já está em uso.");
        }

        return ToSummary(account);
    }

    public async Task<PortalAccountSummary?> UpdateAccountAsync(
        Guid id,
        string? role,
        bool isActive,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(cancellationToken);
        PortalAccount? account = await _dbContext.PortalAccounts.FindAsync([id], cancellationToken);
        if (account is null)
        {
            return null;
        }

        string normalizedRole = ValidateRole(role);
        if (account.Role == "Admin" && (normalizedRole != "Admin" || !isActive) &&
            !await HasAnotherActiveAdminAsync(account.Id, cancellationToken))
        {
            throw new InvalidOperationException("O portal precisa manter pelo menos um Admin ativo.");
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            account.PasswordHash = HashPassword(ValidatePassword(newPassword));
        }

        account.Role = normalizedRole;
        account.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(account);
    }

    public async Task<bool> DeleteAccountAsync(Guid id, Guid requesterId, CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(cancellationToken);
        PortalAccount? account = await _dbContext.PortalAccounts.FindAsync([id], cancellationToken);
        if (account is null)
        {
            return false;
        }

        if (account.Id == requesterId)
        {
            throw new InvalidOperationException("Não é possível excluir a própria conta por este portal.");
        }

        if (account.Role == "Admin" && account.IsActive && !await HasAnotherActiveAdminAsync(account.Id, cancellationToken))
        {
            throw new InvalidOperationException("O portal precisa manter pelo menos um Admin ativo.");
        }

        _dbContext.PortalAccounts.Remove(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task EnsureStorageAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            const string createTableSql = """
                CREATE TABLE IF NOT EXISTS `PORTAL_ACCOUNTS` (
                    `ID` char(36) NOT NULL,
                    `USERNAME` varchar(32) NOT NULL,
                    `NORMALIZED_USERNAME` varchar(32) NOT NULL,
                    `PASSWORD_HASH` varchar(255) NOT NULL,
                    `ROLE` varchar(16) NOT NULL,
                    `IS_ACTIVE` bit NOT NULL,
                    `CREATED_AT_UTC` datetime(6) NOT NULL,
                    PRIMARY KEY (`ID`),
                    UNIQUE KEY `IX_PORTAL_ACCOUNTS_NORMALIZED_USERNAME` (`NORMALIZED_USERNAME`)
                ) CHARACTER SET utf8mb4;
                """;

            await _dbContext.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
            await SeedInitialAdminAsync(cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task SeedInitialAdminAsync(CancellationToken cancellationToken)
    {
        string? userName = _configuration["PortalAdmin:UserName"];
        string? password = _configuration["PortalAdmin:Password"];
        if (string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        string normalizedName = ValidateUserName(userName);
        string validatedPassword = ValidatePassword(password);
        if (await _dbContext.PortalAccounts.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.PortalAccounts.Add(new PortalAccount
        {
            Id = Guid.NewGuid(),
            UserName = userName!.Trim(),
            NormalizedUserName = normalizedName,
            PasswordHash = HashPassword(validatedPassword),
            Role = "Admin",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasAnotherActiveAdminAsync(Guid excludedId, CancellationToken cancellationToken) =>
        await _dbContext.PortalAccounts.AnyAsync(
            account => account.Id != excludedId && account.Role == "Admin" && account.IsActive,
            cancellationToken);

    private PortalAuthResponse CreateAuthResponse(PortalAccount account)
    {
        byte[] keyBytes = GetSigningKey();
        DateTime expiresAt = DateTime.UtcNow.AddHours(12);
        Claim[] claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.UserName),
            new Claim(ClaimTypes.Role, account.Role)
        ];

        JwtSecurityToken token = new(
            issuer: _configuration["Authentication:Issuer"] ?? "NecroWiKi",
            audience: _configuration["Authentication:Audience"] ?? "NecroWiKi",
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256));

        return new PortalAuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            ToSummary(account));
    }

    private byte[] GetSigningKey()
    {
        string? key = _configuration["Authentication:JwtKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("Configure Authentication:JwtKey com pelo menos 32 bytes.");
        }

        return Encoding.UTF8.GetBytes(key);
    }

    private static PortalAccountSummary ToSummary(PortalAccount account) =>
        new(account.Id, account.UserName, account.Role, account.IsActive, account.CreatedAtUtc);

    private static string NormalizeUserName(string? userName) => (userName ?? string.Empty).Trim().ToUpperInvariant();

    private static string ValidateUserName(string? userName)
    {
        string normalized = NormalizeUserName(userName);
        if (normalized.Length is < 3 or > 32 || normalized.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("O usuário deve ter de 3 a 32 caracteres: letras, números, ponto, hífen ou sublinhado.");
        }

        return normalized;
    }

    private static string ValidatePassword(string? password)
    {
        if (password is null || password.Length is < 10 or > 128)
        {
            throw new ArgumentException("A senha deve ter entre 10 e 128 caracteres.");
        }

        return password;
    }

    private static string ValidateRole(string? role)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
        {
            return "User";
        }

        throw new ArgumentException("O nível da conta deve ser Admin ou User.");
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        string[] parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" ||
            !int.TryParse(parts[1], out int iterations) || iterations < 100_000 || iterations > 1_000_000)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}