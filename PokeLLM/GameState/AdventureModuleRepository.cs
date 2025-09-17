using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PokeLLM.GameState.Models;

namespace PokeLLM.GameState;

public interface IAdventureModuleRepository
{
    AdventureModule CreateNewModule(string title = "", string summary = "");
    AdventureModule ApplyChanges(AdventureModule module, Action<AdventureModule> updateAction);
    Task SaveAsync(AdventureModule module, string? filePath = null, CancellationToken cancellationToken = default);
    Task<AdventureModule> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}

public class AdventureModuleRepository : IAdventureModuleRepository
{
    public const string DefaultDirectoryName = "AdventureModules";
    public const string FileExtension = ".json";

    private readonly string _modulesDirectory;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdventureModuleRepository(IOptions<GameStateRepositoryOptions>? options = null)
    {
        var configuredDirectory = options?.Value?.DataDirectory;
        var rootDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), GameStateRepository.DefaultDirectoryName)
            : Path.GetFullPath(configuredDirectory, Directory.GetCurrentDirectory());
        _modulesDirectory = Path.Combine(rootDirectory, DefaultDirectoryName);

        Directory.CreateDirectory(_modulesDirectory);
    }

    public AdventureModule CreateNewModule(string title = "", string summary = "")
    {
        var module = new AdventureModule
        {
            Metadata =
            {
                Title = title,
                Summary = summary,
                CreatedAt = DateTime.UtcNow
            }
        };

        return module;
    }

    public AdventureModule ApplyChanges(AdventureModule module, Action<AdventureModule> updateAction)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (updateAction is null)
        {
            throw new ArgumentNullException(nameof(updateAction));
        }

        updateAction(module);
        return module;
    }

    public async Task SaveAsync(AdventureModule module, string? filePath = null, CancellationToken cancellationToken = default)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        var targetPath = filePath ?? GetDefaultFilePath(module.Metadata.ModuleId);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(targetPath);
        await JsonSerializer.SerializeAsync(stream, module, _serializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdventureModule> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        await using var stream = File.OpenRead(filePath);
        var module = await JsonSerializer.DeserializeAsync<AdventureModule>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);

        if (module is null)
        {
            throw new InvalidOperationException($"Failed to deserialize adventure module from '{filePath}'.");
        }

        return module;
    }

    private string GetDefaultFilePath(string moduleId)
    {
        var safeId = string.IsNullOrWhiteSpace(moduleId) ? Guid.NewGuid().ToString("N") : moduleId;
        var fileName = safeId.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
            ? safeId
            : $"{safeId}{FileExtension}";
        return Path.Combine(_modulesDirectory, fileName);
    }
}
