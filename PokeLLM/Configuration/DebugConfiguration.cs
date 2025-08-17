using Microsoft.Extensions.Configuration;

namespace PokeLLM.Configuration;

/// <summary>
/// Service for managing logging and debug mode configuration
/// </summary>
public interface IDebugConfiguration
{
    bool IsDebugModeEnabled { get; }
    bool IsVerboseLoggingEnabled { get; }
    bool IsDebugPromptsEnabled { get; }
    string LogFilePath { get; }
    bool IsLoggingEnabled { get; }
}

public class DebugConfiguration : IDebugConfiguration
{
    private readonly IConfiguration _configuration;
    
    public DebugConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Check if debug mode is enabled via environment variable or configuration.
    /// Debug mode enables verbose console output and debug prompts.
    /// </summary>
    public bool IsDebugModeEnabled => 
        Environment.GetEnvironmentVariable("POKELLM_DEBUG")?.ToLowerInvariant() == "true" ||
        _configuration.GetValue<bool>("Debug:Enabled", false);

    /// <summary>
    /// Check if verbose console logging is enabled (requires debug mode)
    /// </summary>
    public bool IsVerboseLoggingEnabled => 
        IsDebugModeEnabled && (
            Environment.GetEnvironmentVariable("POKELLM_VERBOSE")?.ToLowerInvariant() == "true" ||
            _configuration.GetValue<bool>("Debug:VerboseLogging", true)
        );

    /// <summary>
    /// Check if debug prompts should be used instead of standard prompts (requires debug mode)
    /// </summary>
    public bool IsDebugPromptsEnabled => 
        IsDebugModeEnabled && (
            Environment.GetEnvironmentVariable("POKELLM_DEBUG_PROMPTS")?.ToLowerInvariant() != "false" &&
            _configuration.GetValue<bool>("Debug:UseDebugPrompts", true)
        );

    /// <summary>
    /// Check if file logging is enabled. File logging is always enabled unless explicitly disabled.
    /// </summary>
    public bool IsLoggingEnabled =>
        Environment.GetEnvironmentVariable("POKELLM_LOGGING")?.ToLowerInvariant() != "false" &&
        _configuration.GetValue<bool>("Debug:Logging", true);

    /// <summary>
    /// Get the log file path. File logging is always enabled unless explicitly disabled.
    /// </summary>
    public string LogFilePath
    {
        get
        {
            var customPath = Environment.GetEnvironmentVariable("POKELLM_LOG_PATH") ?? 
                           _configuration.GetValue<string>("Debug:LogPath");
            
            if (!string.IsNullOrEmpty(customPath))
            {
                return customPath;
            }

            // Use the same base directory as game data (MyDocuments/PokeLLM)
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var pokeLLMDirectory = Path.Combine(documentsPath, "PokeLLM");
            var logsDirectory = Path.Combine(pokeLLMDirectory, "Logs");
            
            // Ensure the logs directory exists
            Directory.CreateDirectory(logsDirectory);
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return Path.Combine(logsDirectory, $"pokellm-{timestamp}.log");
        }
    }
}