using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;
using System.Data;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

public class RegisterService : IRegisterService
{
    private readonly WoWSettings _WoWSettings;
    private readonly string _WoWConnectionString;
    private readonly string _ragnarokConnectionString;

    public RegisterService(
        IConfiguration configuration,
        IOptions<WoWSettings> WoWSettings)
    {
        _WoWSettings = WoWSettings.Value;

        _WoWConnectionString =
            configuration.GetConnectionString(
                _WoWSettings.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionString '{_WoWSettings.ConnectionStringName}' não encontrada.");

        _ragnarokConnectionString =
            configuration.GetConnectionString("Ragnarok") ?? throw
            new InvalidOperationException("Connection String Ragnarok não encontrada");
    }

    public async Task<string> RegisterWoWAsync(RegisterModel model)
    {
        Validate(model);

        var username = model.Login.Trim().ToUpperInvariant();
        var password = model.Password.ToUpperInvariant();
        var email = model.Email.Trim().ToUpperInvariant();

        var salt = RandomNumberGenerator.GetBytes(32);

        var verifier = CalculateVerifier(
            username,
            password,
            salt);

        await using var connection =
            new MySqlConnection(_WoWConnectionString);

        await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                INSERT INTO account
                (
                    username,
                    salt,
                    verifier,
                    expansion,
                    reg_mail,
                    email,
                    joindate
                )
                VALUES
                (
                    @username,
                    @salt,
                    @verifier,
                    @expansion,
                    @regMail,
                    @email,
                    NOW()
                );
                """;

            await using var command = new MySqlCommand(
                sql,
                connection,
                transaction);

            command.Parameters.AddWithValue(
                "@username",
                username);

            command.Parameters.Add(
                "@salt",
                MySqlDbType.Binary,
                32).Value = salt;

            command.Parameters.Add(
                "@verifier",
                MySqlDbType.Binary,
                32).Value = verifier;

            command.Parameters.AddWithValue(
                "@expansion",
                _WoWSettings.Expansion);

            command.Parameters.AddWithValue(
                "@regMail",
                email);

            command.Parameters.AddWithValue(
                "@email",
                email);

            await command.ExecuteNonQueryAsync();

            var accountId = command.LastInsertedId;

            await InitializeRealmCharactersAsync(
                connection,
                transaction,
                accountId);

            await transaction.CommitAsync();

            return accountId.ToString();
        }
        catch (MySqlException ex)
            when (ex.Number == 1062)
        {
            await transaction.RollbackAsync();

            throw new InvalidOperationException(
                "Esse usuário já existe.",
                ex);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task InitializeRealmCharactersAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        long accountId)
    {
        const string realmSql = """
            SELECT id
            FROM realmlist;
            """;

        await using var realmCommand =
            new MySqlCommand(
                realmSql,
                connection,
                transaction);

        await using var reader =
            await realmCommand.ExecuteReaderAsync();

        var realmIds = new List<int>();

        while (await reader.ReadAsync())
        {
            realmIds.Add(
                reader.GetInt32("id"));
        }

        await reader.CloseAsync();

        const string insertSql = """
            INSERT IGNORE INTO realmcharacters
            (
                realmid,
                acctid,
                numchars
            )
            VALUES
            (
                @realmId,
                @accountId,
                0
            );
            """;

        foreach (var realmId in realmIds)
        {
            await using var command =
                new MySqlCommand(
                    insertSql,
                    connection,
                    transaction);

            command.Parameters.AddWithValue(
                "@realmId",
                realmId);

            command.Parameters.AddWithValue(
                "@accountId",
                accountId);

            await command.ExecuteNonQueryAsync();
        }
    }

    private void Validate(RegisterModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Login))
            throw new ArgumentException(
                "O usuário é obrigatório.");

        if (string.IsNullOrEmpty(model.Password))
            throw new ArgumentException(
                "A senha é obrigatória.");

        if (string.IsNullOrWhiteSpace(model.Email))
            throw new ArgumentException(
                "O e-mail é obrigatório.");

        if (model.Login.Trim().Length > 16)
            throw new ArgumentException(
                "O usuário deve possuir no máximo 16 caracteres.");

        if (model.Password.Length > 16)
            throw new ArgumentException(
                "A senha deve possuir no máximo 16 caracteres.");

        if (!System.Net.Mail.MailAddress.TryCreate(
                model.Email.Trim(),
                out _))
        {
            throw new ArgumentException(
                "Informe um e-mail válido.");
        }
    }

    private byte[] CalculateVerifier(
        string username,
        string password,
        byte[] salt)
    {
        using var sha1 = SHA1.Create();

        /*
         * H(username + ":" + password)
         */
        var usernamePasswordHash = sha1.ComputeHash(
            Encoding.UTF8.GetBytes(
                $"{username}:{password}"));

        /*
         * H(salt + H(username:password))
         */
        var xHashInput = new byte[
            salt.Length + usernamePasswordHash.Length];

        Buffer.BlockCopy(
            salt,
            0,
            xHashInput,
            0,
            salt.Length);

        Buffer.BlockCopy(
            usernamePasswordHash,
            0,
            xHashInput,
            salt.Length,
            usernamePasswordHash.Length);

        var xHash = sha1.ComputeHash(xHashInput);

        /*
         * AzerothCore utiliza little-endian.
         */
        var xHashLittleEndian =
            xHash.Reverse().ToArray();

        var x = new BigInteger(
            xHashLittleEndian,
            isUnsigned: true,
            isBigEndian: false);

        var g = new BigInteger(
            _WoWSettings.Generator);

        var n = BigInteger.Parse(
            _WoWSettings.Modulus,
            System.Globalization.NumberStyles.HexNumber);

        /*
         * v = g^x mod N
         */
        var verifier = BigInteger.ModPow(
            g,
            x,
            n);

        /*
         * AzerothCore espera 32 bytes
         * little-endian.
         */
        var verifierBytes =
            verifier.ToByteArray(
                isUnsigned: true,
                isBigEndian: false);

        var result = new byte[32];

        Buffer.BlockCopy(
            verifierBytes,
            0,
            result,
            0,
            Math.Min(
                verifierBytes.Length,
                result.Length));

        return result;
    }

    public async Task<string> RegisterRagnarokAsync(RegisterModel model)
    {
        string userid = model.Login.Trim();
        string password = model.Password;
        string email = model.Email.Trim();
        string? sex = model.Genero;

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                userid,
                @"^[a-zA-Z0-9_]{3,23}$"))
        {
            throw new ArgumentException(
                "Usuário inválido. Use de 3 a 23 caracteres: letras, números ou _.");
        }

        if (password.Length < 4)
        {
            throw new ArgumentException(
                "A senha deve ter pelo menos 4 caracteres.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(
                email,
                out _))
        {
            throw new ArgumentException(
                "E-mail inválido.");
        }

        if (sex is not ("M" or "F"))
        {
            throw new ArgumentException(
                "Selecione o sexo.");
        }

        await using MySqlConnection connection =
            new MySqlConnection(_ragnarokConnectionString);

        await connection.OpenAsync();

        const string checkSql = """
        SELECT account_id
        FROM login
        WHERE userid = @userid
        LIMIT 1;
        """;

        await using (MySqlCommand checkCommand =
            new MySqlCommand(checkSql, connection))
        {
            checkCommand.Parameters.AddWithValue(
                "@userid",
                userid);

            object? result =
                await checkCommand.ExecuteScalarAsync();

            if (result != null)
            {
                throw new InvalidOperationException(
                    "Esse usuário já está cadastrado.");
            }
        }

        const string insertSql = """
        INSERT INTO login
        (
            userid,
            user_pass,
            sex,
            email
        )
        VALUES
        (
            @userid,
            @password,
            @sex,
            @email
        );
        """;

        await using MySqlCommand insertCommand =
            new MySqlCommand(insertSql, connection);

        insertCommand.Parameters.AddWithValue(
            "@userid",
            userid);

        insertCommand.Parameters.AddWithValue(
            "@password",
            password);

        insertCommand.Parameters.AddWithValue(
            "@sex",
            sex);

        insertCommand.Parameters.AddWithValue(
            "@email",
            email);

        try
        {
            await insertCommand.ExecuteNonQueryAsync();
        }
        catch (MySqlException ex)
            when (ex.Number == 1062)
        {
            throw new InvalidOperationException(
                "Esse usuário já está cadastrado.",
                ex);
        }

        return "Conta criada com sucesso! Você já pode entrar no Ragnarok.";
    }
}