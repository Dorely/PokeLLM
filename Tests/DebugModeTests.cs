using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PokeLLM.Configuration;
using PokeLLM.Logging;
using System.IO;
using Moq;

namespace PokeLLM.Tests;

/// <summary>
/// Tests for debug mode functionality
/// </summary>
public class DebugModeTests : IDisposable
{
    private readonly string _tempLogFile;
    private readonly IServiceProvider _serviceProvider;

    public DebugModeTests()
    {
        _tempLogFile = Path.GetTempFileName();
        
        // Create a test configuration that enables debug mode
        var configData = new Dictionary<string, string>
        {
            {"Debug:Enabled", "true"},
            {"Debug:VerboseLogging", "true"},
            {"Debug:UseDebugPrompts", "true"},
            {"Debug:LogPath", _tempLogFile}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDebugConfiguration, DebugConfiguration>();
        services.AddSingleton<IDebugLogger, DebugLogger>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void DebugConfiguration_WithEnvironmentVariable_IsEnabled()
    {
        // Arrange
        Environment.SetEnvironmentVariable("POKELLM_DEBUG", "true");
        
        try
        {
            var config = _serviceProvider.GetRequiredService<IDebugConfiguration>();

            // Act & Assert
            Assert.True(config.IsDebugModeEnabled);
            Assert.True(config.IsVerboseLoggingEnabled);
            Assert.True(config.IsDebugPromptsEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POKELLM_DEBUG", null);
        }
    }

    [Fact]
    public void DebugConfiguration_WithoutEnvironmentVariable_UsesConfigFile()
    {
        // Arrange
        Environment.SetEnvironmentVariable("POKELLM_DEBUG", null);
        var config = _serviceProvider.GetRequiredService<IDebugConfiguration>();

        // Act & Assert
        Assert.True(config.IsDebugModeEnabled); // Should be true from config file
        Assert.True(config.IsVerboseLoggingEnabled);
        Assert.True(config.IsDebugPromptsEnabled);
    }

    [Fact]
    public void DebugLogger_LogsToFile_WhenDebugEnabled()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<IDebugLogger>();

        // Act
        logger.LogDebug("Test debug message");
        logger.LogUserInput("Test user input");
        logger.LogSystemOutput("Test system output");
        logger.Flush();
        
        // Dispose the logger to release the file handle
        logger.Dispose();

        // Assert
        Assert.True(File.Exists(_tempLogFile));
        var logContent = File.ReadAllText(_tempLogFile);
        Assert.Contains("Test debug message", logContent);
        Assert.Contains("Test user input", logContent);
        Assert.Contains("Test system output", logContent);
        Assert.Contains("PokeLLM Debug Session Started", logContent);
    }

    [Fact]
    public void DebugLogger_LogsFunctionCalls_WithParametersAndResults()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<IDebugLogger>();

        // Act
        logger.LogFunctionCall("test_function", "{\"param1\": \"value1\"}", "Function executed successfully");
        logger.Flush();
        
        // Dispose the logger to release the file handle
        logger.Dispose();

        // Assert
        var logContent = File.ReadAllText(_tempLogFile);
        Assert.Contains("test_function", logContent);
        Assert.Contains("param1", logContent);
        Assert.Contains("value1", logContent);
        Assert.Contains("Function executed successfully", logContent);
    }

    [Fact]
    public void DebugLogger_LogsPhaseTransitions_WithReasons()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<IDebugLogger>();

        // Act
        logger.LogPhaseTransition("GameSetup", "WorldGeneration", "Setup completed successfully");
        logger.Flush();
        
        // Dispose the logger to release the file handle
        logger.Dispose();

        // Assert
        var logContent = File.ReadAllText(_tempLogFile);
        Assert.Contains("GameSetup", logContent);
        Assert.Contains("WorldGeneration", logContent);
        Assert.Contains("Setup completed successfully", logContent);
    }

    [Fact]
    public void DebugConfiguration_LogFilePath_ReturnsValidPath()
    {
        // Arrange
        var config = _serviceProvider.GetRequiredService<IDebugConfiguration>();

        // Act
        var logPath = config.LogFilePath;

        // Assert
        Assert.NotNull(logPath);
        Assert.NotEmpty(logPath);
        Assert.True(Path.IsPathFullyQualified(logPath));
    }

    [Fact]
    public void DebugConfiguration_LogFilePath_WithEnvironmentVariable_UsesCustomPath()
    {
        // Arrange
        var customPath = Path.Combine(Path.GetTempPath(), "custom-pokellm-log.txt");
        Environment.SetEnvironmentVariable("POKELLM_LOG_PATH", customPath);
        
        try
        {
            var config = _serviceProvider.GetRequiredService<IDebugConfiguration>();

            // Act
            var logPath = config.LogFilePath;

            // Assert
            Assert.Equal(customPath, logPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POKELLM_LOG_PATH", null);
        }
    }

    [Fact]
    public void DebugLogger_DisposesCleanly_AndWritesSessionEnd()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<IDebugLogger>();
        logger.LogDebug("Test message before dispose");

        // Act
        logger.Dispose();

        // Assert
        var logContent = File.ReadAllText(_tempLogFile);
        Assert.Contains("SESSION_END", logContent);
        Assert.Contains("Test message before dispose", logContent);
    }

    public void Dispose()
    {
        _serviceProvider?.GetService<IDebugLogger>()?.Dispose();
        
        if (File.Exists(_tempLogFile))
        {
            try
            {
                File.Delete(_tempLogFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}