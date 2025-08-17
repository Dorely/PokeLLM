using PokeLLM.Configuration;
using System.Collections.Concurrent;
using System.Text;

namespace PokeLLM.Logging;

/// <summary>
/// Interface for debug logging functionality
/// </summary>
public interface IDebugLogger : IDisposable
{
    void LogUserInput(string input);
    void LogLLMResponse(string response);
    void LogFunctionCall(string functionName, string parameters, string result);
    void LogPhaseTransition(string fromPhase, string toPhase, string reason);
    void LogGameState(string gameStateJson);
    void LogPrompt(string promptType, string prompt);
    void LogError(string error, Exception? exception = null);
    void LogDebug(string message);
    void Flush();
}

/// <summary>
/// Logger that captures all program output and writes to files. 
/// Logging is ALWAYS enabled - debug mode only controls prompts and console verbosity.
/// </summary>
public class DebugLogger : IDebugLogger, IDisposable
{
    private readonly IDebugConfiguration _debugConfig;
    private readonly StreamWriter _logWriter;
    private readonly object _lock = new object();
    private readonly ConcurrentQueue<LogEntry> _logQueue = new();
    private readonly Timer _flushTimer;
    private bool _disposed = false;

    public DebugLogger(IDebugConfiguration debugConfig)
    {
        _debugConfig = debugConfig;
        
        // Check if logging is enabled (should always be true unless explicitly disabled)
        if (_debugConfig.IsLoggingEnabled)
        {
            var logPath = _debugConfig.LogFilePath;
            _logWriter = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = false };
            
            // Write session header
            WriteSessionHeader();
            
            // Set up periodic flush timer (every 5 seconds)
            _flushTimer = new Timer(FlushPeriodically, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            // Only show a simple startup message
            Console.WriteLine($"Logging to: {logPath}");
        }
        else
        {
            // Logging is explicitly disabled
            _logWriter = StreamWriter.Null;
            _flushTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            Console.WriteLine($"File logging disabled.");
        }
    }

    private void WriteSessionHeader()
    {
        var header = new StringBuilder();
        header.AppendLine("================================================================================");
        header.AppendLine($"PokeLLM Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        header.AppendLine($"Debug Mode: {_debugConfig.IsDebugModeEnabled}");
        header.AppendLine($"Verbose Logging: {_debugConfig.IsVerboseLoggingEnabled}");
        header.AppendLine($"Debug Prompts: {_debugConfig.IsDebugPromptsEnabled}");
        header.AppendLine($"Log File Path: {_debugConfig.LogFilePath}");
        header.AppendLine($"Environment Variables:");
        header.AppendLine($"  POKELLM_DEBUG: {Environment.GetEnvironmentVariable("POKELLM_DEBUG") ?? "not set"}");
        header.AppendLine($"  POKELLM_VERBOSE: {Environment.GetEnvironmentVariable("POKELLM_VERBOSE") ?? "not set"}");
        header.AppendLine($"  POKELLM_DEBUG_PROMPTS: {Environment.GetEnvironmentVariable("POKELLM_DEBUG_PROMPTS") ?? "not set"}");
        header.AppendLine($"  POKELLM_LOG_PATH: {Environment.GetEnvironmentVariable("POKELLM_LOG_PATH") ?? "not set"}");
        header.AppendLine("================================================================================");
        header.AppendLine();

        lock (_lock)
        {
            _logWriter.Write(header.ToString());
            _logWriter.Flush();
        }
    }

    public void LogUserInput(string input)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.UserInput,
                Message = input
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // No console output - keep game screen clean
    }

    public void LogLLMResponse(string response)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.LLMResponse,
                Message = response
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // No console output - keep game screen clean
    }

    public void LogFunctionCall(string functionName, string parameters, string result)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.FunctionCall,
                Message = $"Function: {functionName}\nParameters: {parameters}\nResult: {result}"
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // No console output - this is debug info, not user-facing
    }

    public void LogPhaseTransition(string fromPhase, string toPhase, string reason)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.PhaseTransition,
                Message = $"Phase transition: {fromPhase} -> {toPhase}\nReason: {reason}"
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // No console output - keep game screen clean
        // Phase transitions will be visible through the game narrative itself
    }

    public void LogGameState(string gameStateJson)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.GameState,
                Message = gameStateJson
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // No console output - this is debug info, not user-facing
    }

    public void LogPrompt(string promptType, string prompt)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Prompt,
                Message = $"Prompt Type: {promptType}\nPrompt Content:\n{prompt}"
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // No console output - this is debug info, not user-facing
    }

    public void LogError(string error, Exception? exception = null)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            // Always log errors to file if logging is enabled
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Error,
                Message = exception != null ? $"{error}\nException: {exception}" : error
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // Still show errors in console as they're critical
        Console.WriteLine($"[ERROR] {error}");
        if (exception != null)
        {
            Console.WriteLine($"[ERROR] Exception: {exception}");
        }
    }

    public void LogDebug(string message)
    {
        if (_debugConfig.IsLoggingEnabled)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Debug,
                Message = message
            };
            
            _logQueue.Enqueue(entry);
        }
        
        // No console output - this is debug info, not user-facing
    }

    public void Flush()
    {
        if (!_debugConfig.IsLoggingEnabled) return;
        
        lock (_lock)
        {
            ProcessLogQueue();
            _logWriter?.Flush();
        }
    }

    private void FlushPeriodically(object? state)
    {
        if (_disposed) return;
        Flush();
    }

    private void ProcessLogQueue()
    {
        if (_logWriter == null) return;

        while (_logQueue.TryDequeue(out var entry))
        {
            var logLine = FormatLogEntry(entry);
            _logWriter.WriteLine(logLine);
        }
    }

    private string FormatLogEntry(LogEntry entry)
    {
        var levelStr = entry.Level.ToString().ToUpperInvariant().PadRight(12);
        var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        var lines = entry.Message.Split('\n');
        if (lines.Length == 1)
        {
            return $"[{timestamp}] {levelStr} {lines[0]}";
        }
        else
        {
            var result = new StringBuilder();
            result.AppendLine($"[{timestamp}] {levelStr} {lines[0]}");
            for (int i = 1; i < lines.Length; i++)
            {
                result.AppendLine($"[{timestamp}] {levelStr} {lines[i]}");
            }
            return result.ToString().TrimEnd();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _flushTimer?.Dispose();
        
        if (_debugConfig.IsLoggingEnabled)
        {
            // Flush and close log file
            Flush();
            
            lock (_lock)
            {
                _logWriter?.WriteLine();
                _logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SESSION_END Session ended");
                _logWriter?.WriteLine("================================================================================");
                _logWriter?.WriteLine();
                _logWriter?.Dispose();
            }
            
            // Minimal shutdown message
            Console.WriteLine($"Session logged.");
        }
    }

    private record LogEntry
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    private enum LogLevel
    {
        Debug,
        UserInput,
        LLMResponse,
        FunctionCall,
        PhaseTransition,
        GameState,
        Prompt,
        Error
    }
}