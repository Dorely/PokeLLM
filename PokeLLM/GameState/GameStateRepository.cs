using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PokeLLM.GameState.Models;

namespace PokeLLM.GameState;

public interface IGameStateRepository
{
    Task<AdventureSessionState> CreateNewGameStateAsync(AdventureSessionState? seedState = null);
    Task SaveStateAsync(AdventureSessionState sessionState);
    Task<AdventureSessionState> LoadLatestStateAsync();
    Task<AdventureSessionState> LoadSessionAsync(string sessionFilePath);
    Task<bool> HasGameStateAsync();
    Task<IReadOnlyList<AdventureSessionSummary>> ListSessionsAsync();
    void SetActiveSession(string sessionFilePath);
    string? GetActiveSessionPath();
    string GenerateSessionDisplayName(AdventureSessionState sessionState);
}

public class GameStateRepository : IGameStateRepository
{
    public const string DefaultDirectoryName = "AdventureSessions";
    private const string LegacySessionFileName = "adventure_session.json";
    private const string SessionFileExtension = ".session.json";

    private readonly string _sessionDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private string _activeSessionFile;

    public GameStateRepository(IOptions<AdventureSessionRepositoryOptions>? options = null)
    {
        _sessionDirectory = ResolveDirectory(options?.Value?.DataDirectory);
        Directory.CreateDirectory(_sessionDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _activeSessionFile = Path.Combine(_sessionDirectory, LegacySessionFileName);

        // Prefer an existing session file if one is present
        var existingSession = Directory.EnumerateFiles(_sessionDirectory, $"*{SessionFileExtension}", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (existingSession is not null)
        {
            _activeSessionFile = existingSession;
        }
        else if (!File.Exists(_activeSessionFile))
        {
            // Legacy single session file is not present; prepare a default path for the first session we create
            _activeSessionFile = Path.Combine(_sessionDirectory, $"session_{Guid.NewGuid():N}{SessionFileExtension}");
        }
    }

    public string GenerateSessionDisplayName(AdventureSessionState sessionState)
    {
        if (sessionState is null)
        {
            throw new ArgumentNullException(nameof(sessionState));
        }

        var sessionId = sessionState.SessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            sessionState.Metadata.SessionId = sessionId;
        }

        var shortId = sessionId.Length > 8 ? sessionId[..8] : sessionId;
        var region = sessionState.Region?.Trim();
        var playerName = sessionState.Player?.Name?.Trim();

        if (!string.IsNullOrWhiteSpace(playerName) && !string.IsNullOrWhiteSpace(region))
        {
            return $"{playerName} - {region} ({shortId})";
        }

        if (!string.IsNullOrWhiteSpace(playerName))
        {
            return $"{playerName} ({shortId})";
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            return $"{region} ({shortId})";
        }

        return $"Session {shortId}";
    }

    public async Task<AdventureSessionState> CreateNewGameStateAsync(AdventureSessionState? seedState = null)
    {
        var sessionState = seedState ?? new AdventureSessionState();

        sessionState.SessionName = GenerateSessionDisplayName(sessionState);

        sessionState.Metadata.SessionStartTime = DateTime.UtcNow;
        sessionState.Metadata.LastUpdatedTime = sessionState.Metadata.SessionStartTime;

        var fileName = $"{sessionState.SessionId}{SessionFileExtension}";
        var targetPath = Path.Combine(_sessionDirectory, fileName);
        _activeSessionFile = targetPath;

        await SaveStateInternalAsync(sessionState, targetPath);
        return sessionState;
    }

    public async Task SaveStateAsync(AdventureSessionState sessionState)
    {
        if (sessionState is null)
        {
            throw new ArgumentNullException(nameof(sessionState));
        }

        var targetPath = EnsureActiveSessionPath();
        await SaveStateInternalAsync(sessionState, targetPath);
    }

    public async Task<AdventureSessionState> LoadLatestStateAsync()
    {
        var targetPath = EnsureActiveSessionPath();
        if (!File.Exists(targetPath))
        {
            return await CreateNewGameStateAsync();
        }

        return await LoadSessionAsync(targetPath);
    }

    public async Task<AdventureSessionState> LoadSessionAsync(string sessionFilePath)
    {
        if (string.IsNullOrWhiteSpace(sessionFilePath))
        {
            throw new ArgumentException("Session file path cannot be null or empty.", nameof(sessionFilePath));
        }

        var validatedPath = ValidateSessionFilePath(sessionFilePath);
        if (!File.Exists(validatedPath))
        {
            throw new FileNotFoundException($"Session file not found: {validatedPath}");
        }

        var json = await File.ReadAllTextAsync(validatedPath);
        var session = JsonSerializer.Deserialize<AdventureSessionState>(json, _jsonOptions)
                      ?? throw new InvalidOperationException($"Failed to deserialize adventure session from '{validatedPath}'.");

        _activeSessionFile = validatedPath;
        return session;
    }

    public async Task<bool> HasGameStateAsync()
    {
        await Task.Yield();
        return Directory.EnumerateFiles(_sessionDirectory, "*.json", SearchOption.TopDirectoryOnly).Any();
    }

    public async Task<IReadOnlyList<AdventureSessionSummary>> ListSessionsAsync()
    {
        await Task.Yield();
        var summaries = new List<AdventureSessionSummary>();

        foreach (var file in Directory.EnumerateFiles(_sessionDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (TryReadSummary(file, out var summary))
            {
                summaries.Add(summary);
            }
        }

        return summaries
            .OrderByDescending(s => s.LastUpdatedTime)
            .ThenBy(s => s.SessionName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SetActiveSession(string sessionFilePath)
    {
        if (string.IsNullOrWhiteSpace(sessionFilePath))
        {
            throw new ArgumentException("Session file path cannot be null or empty.", nameof(sessionFilePath));
        }

        var validatedPath = ValidateSessionFilePath(sessionFilePath);
        if (!File.Exists(validatedPath))
        {
            throw new FileNotFoundException($"Session file not found: {validatedPath}");
        }

        _activeSessionFile = validatedPath;
    }

    public string? GetActiveSessionPath() => _activeSessionFile;

    private async Task SaveStateInternalAsync(AdventureSessionState sessionState, string targetPath)
    {
        sessionState.Metadata.LastUpdatedTime = DateTime.UtcNow;
        sessionState.Metadata.SessionName = GenerateSessionDisplayName(sessionState);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(sessionState, _jsonOptions);
        await File.WriteAllTextAsync(targetPath, json);
    }

    private string EnsureActiveSessionPath()
    {
        if (string.IsNullOrWhiteSpace(_activeSessionFile))
        {
            _activeSessionFile = Path.Combine(_sessionDirectory, LegacySessionFileName);
        }

        return _activeSessionFile;
    }

    private string ValidateSessionFilePath(string sessionFilePath)
    {
        var fullPath = Path.IsPathRooted(sessionFilePath)
            ? sessionFilePath
            : Path.Combine(_sessionDirectory, sessionFilePath);

        fullPath = Path.GetFullPath(fullPath, _sessionDirectory);
        if (!fullPath.StartsWith(_sessionDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Session file path must reside within the configured session directory.");
        }

        return fullPath;
    }

    private bool TryReadSummary(string filePath, out AdventureSessionSummary summary)
    {
        summary = default!;

        try
        {
            var json = File.ReadAllText(filePath);
            var session = JsonSerializer.Deserialize<AdventureSessionState>(json, _jsonOptions);
            if (session is null)
            {
                return false;
            }

            summary = new AdventureSessionSummary
            {
                SessionId = session.SessionId,
                SessionName = string.IsNullOrWhiteSpace(session.SessionName) ? session.SessionId : session.SessionName,
                ModuleId = session.Module.ModuleId,
                ModuleTitle = session.Module.ModuleTitle,
                ModuleFileName = session.Module.ModuleFileName,
                LastUpdatedTime = session.Metadata.LastUpdatedTime,
                CurrentPhase = session.Metadata.CurrentPhase,
                IsSetupComplete = session.Metadata.IsSetupComplete,
                FilePath = filePath
            };

            return true;
        }
        catch
        {
            return false;
        }
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
