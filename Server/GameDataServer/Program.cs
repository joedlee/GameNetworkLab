using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string connectionString =
    "Host=127.0.0.1;" +
    "Port=5432;" +
    "Username=postgres;" +
    "Database=gamenetworklab;";

app.MapGet("/", () =>
{
    return "GameDataServer 실행 중";
});

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

app.Run("http://127.0.0.1:5000");

public record SaveScoreRequest(
    string PlayerId,
    int Score);