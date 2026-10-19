using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string connectionString =
    "Host=127.0.0.1;" +
    "Port=5432;" +
    "Username=postgres;" +
    "Database=gamenetworklab;";

await AuthHelpers.EnsureSchemaAsync(connectionString);

app.MapGet("/", () =>
{
    return "GameDataServer 실행 중";
});

// -----------------------------------------------------------------------------
// 기존 DAY13 점수 API
// -----------------------------------------------------------------------------

app.MapGet("/score/{playerId}", async (string playerId) =>
{
    await using NpgsqlConnection connection =
        new NpgsqlConnection(connectionString);

    await connection.OpenAsync();

    await using NpgsqlCommand command =
        new NpgsqlCommand(
            """
            SELECT player_id, score, last_saved_at
            FROM player_scores
            WHERE player_id = @playerId;
            """,
            connection);

    command.Parameters.AddWithValue("playerId", playerId);

    await using NpgsqlDataReader reader =
        await command.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
    {
        return Results.NotFound(
            new
            {
                message = "플레이어 점수를 찾을 수 없습니다.",
                playerId
            });
    }

    string id = reader.GetString(0);
    int score = reader.GetInt32(1);
    DateTime lastSavedAt = reader.GetDateTime(2);

    return Results.Ok(
        new
        {
            PlayerId = id,
            Score = score,
            LastSavedAt = lastSavedAt
        });
});

app.MapPost("/score", async (SaveScoreRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.PlayerId))
    {
        return Results.BadRequest(
            new
            {
                message = "PlayerId가 필요합니다."
            });
    }

    await using NpgsqlConnection connection =
        new NpgsqlConnection(connectionString);

    await connection.OpenAsync();

    await using NpgsqlCommand command =
        new NpgsqlCommand(
            """
            INSERT INTO player_scores
                (player_id, score, last_saved_at)
            VALUES
                (@playerId, @score, NOW())
            ON CONFLICT (player_id)
            DO UPDATE SET
                score = EXCLUDED.score,
                last_saved_at = NOW()
            RETURNING last_saved_at;
            """,
            connection);

    command.Parameters.AddWithValue(
        "playerId",
        request.PlayerId);

    command.Parameters.AddWithValue(
        "score",
        request.Score);

    DateTime lastSavedAt =
        (DateTime)(await command.ExecuteScalarAsync())!;

    return Results.Ok(
        new
        {
            request.PlayerId,
            request.Score,
            LastSavedAt = lastSavedAt
        });
});

// -----------------------------------------------------------------------------
// 계정 인증 API
// -----------------------------------------------------------------------------

app.MapPost("/auth/register", async (RegisterRequest request) =>
{
    string username =
        request.Username?.Trim() ?? string.Empty;

    string password =
        request.Password ?? string.Empty;

    string? validationError =
        AuthHelpers.ValidateCredentials(username, password);

    if (validationError != null)
    {
        return Results.BadRequest(
            new
            {
                message = validationError
            });
    }

    string playerId =
        $"player-{Guid.NewGuid():N}";

    byte[] salt =
        RandomNumberGenerator.GetBytes(16);

    byte[] passwordHash =
        AuthHelpers.HashPassword(password, salt);

    await using NpgsqlConnection connection =
        new NpgsqlConnection(connectionString);

    await connection.OpenAsync();

    await using NpgsqlTransaction transaction =
        await connection.BeginTransactionAsync();

    try
    {
        await using (NpgsqlCommand accountCommand =
            new NpgsqlCommand(
                """
                INSERT INTO player_accounts
                    (player_id, username, password_salt, password_hash)
                VALUES
                    (@playerId, @username, @salt, @passwordHash);
                """,
                connection,
                transaction))
        {
            accountCommand.Parameters.AddWithValue(
                "playerId",
                playerId);

            accountCommand.Parameters.AddWithValue(
                "username",
                username);

            accountCommand.Parameters.AddWithValue(
                "salt",
                salt);

            accountCommand.Parameters.AddWithValue(
                "passwordHash",
                passwordHash);

            await accountCommand.ExecuteNonQueryAsync();
        }

        await using (NpgsqlCommand scoreCommand =
            new NpgsqlCommand(
                """
                INSERT INTO player_scores
                    (player_id, score, last_saved_at)
                VALUES
                    (@playerId, 0, NOW())
                ON CONFLICT (player_id) DO NOTHING;
                """,
                connection,
                transaction))
        {
            scoreCommand.Parameters.AddWithValue(
                "playerId",
                playerId);

            await scoreCommand.ExecuteNonQueryAsync();
        }

        string token =
            await AuthHelpers.CreateSessionAsync(
                connection,
                transaction,
                playerId);

        await transaction.CommitAsync();

        return Results.Ok(
            new AuthResponse(
                playerId,
                username,
                token));
    }
    catch (PostgresException exception)
        when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        await transaction.RollbackAsync();

        return Results.Conflict(
            new
            {
                message = "이미 사용 중인 사용자 이름입니다."
            });
    }
});

app.MapPost("/auth/login", async (LoginRequest request) =>
{
    string username =
        request.Username?.Trim() ?? string.Empty;

    string password =
        request.Password ?? string.Empty;

    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
    {
        return Results.BadRequest(
            new
            {
                message = "사용자 이름과 비밀번호가 필요합니다."
            });
    }

    await using NpgsqlConnection connection =
        new NpgsqlConnection(connectionString);

    await connection.OpenAsync();

    string? playerId = null;
    string? storedUsername = null;
    byte[]? salt = null;
    byte[]? storedHash = null;

    await using (NpgsqlCommand command =
        new NpgsqlCommand(
            """
            SELECT
                player_id,
                username,
                password_salt,
                password_hash
            FROM player_accounts
            WHERE LOWER(username) = LOWER(@username)
            LIMIT 1;
            """,
            connection))
    {
        command.Parameters.AddWithValue(
            "username",
            username);

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            playerId = reader.GetString(0);
            storedUsername = reader.GetString(1);
            salt = (byte[])reader[2];
            storedHash = (byte[])reader[3];
        }
    }

    if (playerId == null ||
        storedUsername == null ||
        salt == null ||
        storedHash == null)
    {
        return Results.Unauthorized();
    }

    byte[] suppliedHash =
        AuthHelpers.HashPassword(password, salt);

    if (!CryptographicOperations.FixedTimeEquals(
        suppliedHash,
        storedHash))
    {
        return Results.Unauthorized();
    }

    await using NpgsqlTransaction transaction =
        await connection.BeginTransactionAsync();

    string token =
        await AuthHelpers.CreateSessionAsync(
            connection,
            transaction,
            playerId);

    await transaction.CommitAsync();

    return Results.Ok(
        new AuthResponse(
            playerId,
            storedUsername,
            token));
});

app.MapPost("/auth/logout", async (HttpRequest httpRequest) =>
{
    string? token =
        AuthHelpers.ReadBearerToken(httpRequest);

    if (token == null)
        return Results.Unauthorized();

    byte[] tokenHash =
        AuthHelpers.HashToken(token);

    await using NpgsqlConnection connection =
        new NpgsqlConnection(connectionString);

    await connection.OpenAsync();

    await using NpgsqlCommand command =
        new NpgsqlCommand(
            """
            DELETE FROM auth_sessions
            WHERE token_hash = @tokenHash;
            """,
            connection);

    command.Parameters.AddWithValue(
        "tokenHash",
        tokenHash);

    await command.ExecuteNonQueryAsync();

    return Results.Ok(
        new
        {
            message = "로그아웃 완료"
        });
});

// -----------------------------------------------------------------------------
// 인증된 계정 전용 점수 API
// -----------------------------------------------------------------------------

app.MapGet("/account/score", async (HttpRequest httpRequest) =>
{
    string? playerId =
        await AuthHelpers.GetAuthenticatedPlayerIdAsync(
            httpRequest,
            connectionString);

    if (playerId == null)
        return Results.Unauthorized();

    await using NpgsqlConnection connection =
        new NpgsqlConnection(connectionString);

    await connection.OpenAsync();

    await using NpgsqlCommand command =
        new NpgsqlCommand(
            """
            SELECT player_id, score, last_saved_at
            FROM player_scores
            WHERE player_id = @playerId;
            """,
            connection);

    command.Parameters.AddWithValue(
        "playerId",
        playerId);

    await using NpgsqlDataReader reader =
        await command.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
    {
        return Results.NotFound(
            new
            {
                message = "계정 점수를 찾을 수 없습니다."
            });
    }

    return Results.Ok(
        new
        {
            PlayerId = reader.GetString(0),
            Score = reader.GetInt32(1),
            LastSavedAt = reader.GetDateTime(2)
        });
});

app.MapPost("/account/score", async (
    HttpRequest httpRequest,
    SaveOwnScoreRequest request) =>
{
    string? playerId =
        await AuthHelpers.GetAuthenticatedPlayerIdAsync(
            httpRequest,
            connectionString);

    if (playerId == null)
        return Results.Unauthorized();

    if (request.Score < 0)
    {
        return Results.BadRequest(
            new
            {
                message = "Score는 0 이상이어야 합니다."
            });
    }

    await using NpgsqlConnection connection =
        new NpgsqlConnection(connectionString);

    await connection.OpenAsync();

    await using NpgsqlCommand command =
        new NpgsqlCommand(
            """
            INSERT INTO player_scores
                (player_id, score, last_saved_at)
            VALUES
                (@playerId, @score, NOW())
            ON CONFLICT (player_id)
            DO UPDATE SET
                score = EXCLUDED.score,
                last_saved_at = NOW()
            RETURNING last_saved_at;
            """,
            connection);

    command.Parameters.AddWithValue(
        "playerId",
        playerId);

    command.Parameters.AddWithValue(
        "score",
        request.Score);

    DateTime lastSavedAt =
        (DateTime)(await command.ExecuteScalarAsync())!;

    return Results.Ok(
        new
        {
            PlayerId = playerId,
            request.Score,
            LastSavedAt = lastSavedAt
        });
});

app.Run("http://127.0.0.1:5000");

public record SaveScoreRequest(
    string PlayerId,
    int Score);

public record RegisterRequest(
    string Username,
    string Password);

public record LoginRequest(
    string Username,
    string Password);

public record SaveOwnScoreRequest(
    int Score);

public record AuthResponse(
    string PlayerId,
    string Username,
    string Token);

public static class AuthHelpers
{
    private const int PasswordIterations = 120_000;
    private const int PasswordHashLength = 32;

    public static async Task EnsureSchemaAsync(
        string connectionString)
    {
        await using NpgsqlConnection connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync();

        await using NpgsqlCommand command =
            new NpgsqlCommand(
                """
                CREATE TABLE IF NOT EXISTS player_accounts (
                    player_id TEXT PRIMARY KEY,
                    username TEXT NOT NULL,
                    password_salt BYTEA NOT NULL,
                    password_hash BYTEA NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS
                    ux_player_accounts_username_lower
                ON player_accounts (LOWER(username));

                CREATE TABLE IF NOT EXISTS auth_sessions (
                    token_hash BYTEA PRIMARY KEY,
                    player_id TEXT NOT NULL
                        REFERENCES player_accounts(player_id)
                        ON DELETE CASCADE,
                    expires_at TIMESTAMPTZ NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS
                    ix_auth_sessions_player_id
                ON auth_sessions (player_id);
                """,
                connection);

        await command.ExecuteNonQueryAsync();
    }

    public static string? ValidateCredentials(
        string username,
        string password)
    {
        if (!Regex.IsMatch(
            username,
            @"^[A-Za-z0-9_-]{3,32}$"))
        {
            return
                "사용자 이름은 영문, 숫자, _, -만 사용해 3~32자로 입력하세요.";
        }

        if (password.Length < 8 ||
            password.Length > 128)
        {
            return
                "비밀번호는 8~128자로 입력하세요.";
        }

        return null;
    }

    public static byte[] HashPassword(
        string password,
        byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            PasswordHashLength);
    }

    public static byte[] HashToken(
        string token)
    {
        return SHA256.HashData(
            Encoding.UTF8.GetBytes(token));
    }

    public static string? ReadBearerToken(
        HttpRequest request)
    {
        string authorization =
            request.Headers.Authorization.ToString();

        const string prefix = "Bearer ";

        if (!authorization.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string token =
            authorization[prefix.Length..].Trim();

        return string.IsNullOrWhiteSpace(token)
            ? null
            : token;
    }

    public static async Task<string?> GetAuthenticatedPlayerIdAsync(
        HttpRequest request,
        string connectionString)
    {
        string? token =
            ReadBearerToken(request);

        if (token == null)
            return null;

        byte[] tokenHash =
            HashToken(token);

        await using NpgsqlConnection connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync();

        await using NpgsqlCommand command =
            new NpgsqlCommand(
                """
                SELECT player_id
                FROM auth_sessions
                WHERE token_hash = @tokenHash
                  AND expires_at > NOW()
                LIMIT 1;
                """,
                connection);

        command.Parameters.AddWithValue(
            "tokenHash",
            tokenHash);

        object? result =
            await command.ExecuteScalarAsync();

        return result as string;
    }

    public static async Task<string> CreateSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string playerId)
    {
        byte[] rawToken =
            RandomNumberGenerator.GetBytes(32);

        string token =
            Convert.ToHexString(rawToken)
                .ToLowerInvariant();

        byte[] tokenHash =
            HashToken(token);

        await using NpgsqlCommand command =
            new NpgsqlCommand(
                """
                INSERT INTO auth_sessions
                    (token_hash, player_id, expires_at)
                VALUES
                    (@tokenHash, @playerId, NOW() + INTERVAL '7 days');
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "tokenHash",
            tokenHash);

        command.Parameters.AddWithValue(
            "playerId",
            playerId);

        await command.ExecuteNonQueryAsync();

        return token;
    }
}
