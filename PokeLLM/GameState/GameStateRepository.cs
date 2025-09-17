using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PokeLLM.GameState.Models;

namespace PokeLLM.GameState;

public interface IGameStateRepository
{
    Task<AdventureSessionState> CreateNewGameStateAsync();
    Task SaveStateAsync(AdventureSessionState sessionState);
    Task<AdventureSessionState> LoadLatestStateAsync();
    Task<bool> HasGameStateAsync();
}

public class GameStateRepository : IGameStateRepository
{
    public const string DefaultDirectoryName = "AdventureSessions";
    private const string SessionFileName = "adventure_session.json";

    private readonly string _sessionDirectory;
    private readonly string _currentSessionFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameStateRepository(IOptions<AdventureSessionRepositoryOptions>? options = null)
    {
        _sessionDirectory = ResolveDirectory(options?.Value?.DataDirectory);
        Directory.CreateDirectory(_sessionDirectory);
        _currentSessionFile = Path.Combine(_sessionDirectory, SessionFileName);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<AdventureSessionState> CreateNewGameStateAsync()
    {
        var sessionState = new AdventureSessionState();
        await SaveStateAsync(sessionState);
        return sessionState;
    }

    public async Task SaveStateAsync(AdventureSessionState sessionState)
    {
        if (sessionState == null)
        {
            throw new ArgumentNullException(nameof(sessionState));
        }

        sessionState.Metadata.LastUpdatedTime = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(sessionState, _jsonOptions);
        await File.WriteAllTextAsync(_currentSessionFile, json);
    }

    public async Task<AdventureSessionState> LoadLatestStateAsync()
    {
        if (!File.Exists(_currentSessionFile))
        {
            return await CreateNewGameStateAsync();
        }

        var json = await File.ReadAllTextAsync(_currentSessionFile);
        return JsonSerializer.Deserialize<AdventureSessionState>(json, _jsonOptions)!;
    }

    public async Task<bool> HasGameStateAsync()
    {
        await Task.Yield();
        return File.Exists(_currentSessionFile);
    }

    private static string ResolveDirectory(string? dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            return Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectoryName);
        }

        return Path.GetFullPath(dataDirectory, Directory.GetCurrentDirectory());
    }
}
