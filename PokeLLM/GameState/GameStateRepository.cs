using Microsoft.Data.Sqlite;
using System.Threading.Tasks;

namespace PokeLLM.GameState;

public class GameStateRepository
{
    private readonly string _connectionString;

    public GameStateRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        Initialize().Wait();
    }

    private async Task Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS GameState (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StateJson TEXT NOT NULL
            );";
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveStateAsync(string stateJson)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO GameState (StateJson) VALUES ($stateJson);";
        command.Parameters.AddWithValue("$stateJson", stateJson);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string?> LoadLatestStateAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT StateJson FROM GameState ORDER BY Id DESC LIMIT 1;";
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }
}