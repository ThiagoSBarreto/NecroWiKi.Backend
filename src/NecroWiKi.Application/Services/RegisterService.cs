using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

public class RegisterService : IRegisterService
{
    private readonly WoWSettings _WoWSettings;
    private readonly string _WoWConnectionString;
    private readonly string _ragnarokConnectionString;

    public RegisterService(IConfiguration configuration, IOptions<WoWSettings> WoWSettings)
    {
        _WoWSettings = WoWSettings.Value;
        _WoWConnectionString = configuration.GetConnectionString(_WoWSettings.ConnectionStringName) ?? throw new InvalidOperationException($"ConnectionString '{_WoWSettings.ConnectionStringName}' não encontrada.");
        _ragnarokConnectionString = configuration.GetConnectionString("Ragnarok") ?? throw new InvalidOperationException("Connection String Ragnarok não encontrada");
    }

    public async Task<string> RegisterWoWAsync(RegisterModel model)
    {
        try
        {
            string username = model.Login.Trim().ToUpperInvariant();
            string password = model.Password.ToUpperInvariant();
            string email = model.Email.Trim().ToUpperInvariant();
            byte[] salt = RandomNumberGenerator.GetBytes(32);
            byte[] verifier = CalculateVerifier(username, password, salt);

            await using MySqlConnection connection = new MySqlConnection(_WoWConnectionString);
            await connection.OpenAsync();
            await using MySqlTransaction transaction = await connection.BeginTransactionAsync();

            const string sql = """
                INSERT INTO account (username, salt, verifier, expansion, reg_mail, email, joindate)
                VALUES (@username, @salt, @verifier, @expansion, @regMail, @email, NOW());
                """;

            await using MySqlCommand command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.Add("@salt", MySqlDbType.Binary, 32).Value = salt;
            command.Parameters.Add("@verifier", MySqlDbType.Binary, 32).Value = verifier;
            command.Parameters.AddWithValue("@expansion", _WoWSettings.Expansion);
            command.Parameters.AddWithValue("@regMail", email);
            command.Parameters.AddWithValue("@email", email);

            await command.ExecuteNonQueryAsync();

            long accountId = command.LastInsertedId;

            await InitializeRealmCharactersAsync(connection, transaction, accountId);
            await transaction.CommitAsync();

            return "success";
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return "Nome de usuário já está em uso.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string> RegisterRagnarokAsync(RegisterModel model)
    {
        try
        {
            string userid = model.Login.Trim();
            string password = model.Password;
            string email = model.Email.Trim();
            string? sex = model.Genero;

            await using MySqlConnection connection = new MySqlConnection(_ragnarokConnectionString);
            await connection.OpenAsync();

            const string checkSql = """
                SELECT account_id
                FROM login
                WHERE userid = @userid
                LIMIT 1;
                """;

            await using MySqlCommand checkCommand = new MySqlCommand(checkSql, connection);
            checkCommand.Parameters.AddWithValue("@userid", userid);

            object? result = await checkCommand.ExecuteScalarAsync();

            if (result != null)
            {
                throw new InvalidOperationException("Nome de usuário já está em uso.");
            }

            const string insertSql = """
                INSERT INTO login (userid, user_pass, sex, email)
                VALUES (@userid, @password, @sex, @email);
                """;

            await using MySqlCommand insertCommand = new MySqlCommand(insertSql, connection);
            insertCommand.Parameters.AddWithValue("@userid", userid);
            insertCommand.Parameters.AddWithValue("@password", password);
            insertCommand.Parameters.AddWithValue("@sex", sex);
            insertCommand.Parameters.AddWithValue("@email", email);

            await insertCommand.ExecuteNonQueryAsync();

            return "success";
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return "Nome de usuário já está em uso.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task InitializeRealmCharactersAsync(MySqlConnection connection, MySqlTransaction transaction, long accountId)
    {
        const string realmSql = """
            SELECT id
            FROM realmlist;
            """;

        await using MySqlCommand realmCommand = new MySqlCommand(realmSql, connection, transaction);
        await using MySqlDataReader reader = await realmCommand.ExecuteReaderAsync();

        List<int> realmIds = new List<int>();

        while (await reader.ReadAsync())
        {
            realmIds.Add(reader.GetInt32("id"));
        }

        await reader.CloseAsync();

        const string insertSql = """
            INSERT IGNORE INTO realmcharacters (realmid, acctid, numchars)
            VALUES (@realmId, @accountId, 0);
            """;

        foreach (int realmId in realmIds)
        {
            await using MySqlCommand command = new MySqlCommand(insertSql, connection, transaction);
            command.Parameters.AddWithValue("@realmId", realmId);
            command.Parameters.AddWithValue("@accountId", accountId);
            await command.ExecuteNonQueryAsync();
        }
    }

    private byte[] CalculateVerifier(string username, string password, byte[] salt)
    {
        using SHA1 sha1 = SHA1.Create();

        byte[] usernamePasswordHash = sha1.ComputeHash(Encoding.UTF8.GetBytes($"{username}:{password}"));

        byte[] xHashInput = new byte[salt.Length + usernamePasswordHash.Length];

        Buffer.BlockCopy(salt, 0, xHashInput, 0, salt.Length);
        Buffer.BlockCopy(usernamePasswordHash, 0, xHashInput, salt.Length, usernamePasswordHash.Length);

        byte[] xHash = sha1.ComputeHash(xHashInput);
        byte[] xHashLittleEndian = xHash.Reverse().ToArray();

        BigInteger x = new BigInteger(xHashLittleEndian, isUnsigned: true, isBigEndian: false);
        BigInteger g = new BigInteger(_WoWSettings.Generator);
        BigInteger n = BigInteger.Parse(_WoWSettings.Modulus, System.Globalization.NumberStyles.HexNumber);
        BigInteger verifier = BigInteger.ModPow(g, x, n);

        byte[] verifierBytes = verifier.ToByteArray(isUnsigned: true, isBigEndian: false);
        byte[] result = new byte[32];

        Buffer.BlockCopy(verifierBytes, 0, result, 0, Math.Min(verifierBytes.Length, result.Length));

        return result;
    }
}