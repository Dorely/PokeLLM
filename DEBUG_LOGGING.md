# Debug Logging Setup

## Overview
PokeLLM includes comprehensive logging to track application behavior, LLM interactions, and troubleshoot issues. **File logging is enabled by default** and works independently of debug mode.

## Logging vs Debug Mode

### File Logging (Always Enabled)
- **User Input**: Every input message from the user
- **System Output**: All streaming responses  
- **LLM Response**: Complete LLM outputs
- **Function Calls**: Plugin function calls with parameters and results
- **Phase Transitions**: Game phase changes with reasons
- **Game State**: Game state snapshots
- **Prompts**: System prompts sent to LLMs
- **Errors**: All errors and exceptions

### Debug Mode (Optional Enhancement)
Debug mode **only** controls:
- **Prompt Selection**: Uses enhanced debug prompts vs standard prompts
- **Console Verbosity**: Shows detailed info in console output

## Configuration

### Automatic (Default)
Both logging and debug mode are enabled by default in `appsettings.json`:

```json
{
  "Debug": {
    "Logging": true,      // File logging (always recommended)
    "Enabled": true,      // Debug mode (enhanced prompts + verbose console)
    "VerboseLogging": true,
    "UseDebugPrompts": true,
    "LogPath": ""
  }
}
```

### Environment Variables (Optional)
You can control behavior with environment variables:

- `POKELLM_LOGGING=false` - Disable file logging (not recommended)
- `POKELLM_DEBUG=true` - Enable debug mode (enhanced prompts + verbose console)
- `POKELLM_VERBOSE=true` - Enable verbose console output (requires debug mode)
- `POKELLM_DEBUG_PROMPTS=false` - Use standard prompts instead of debug prompts
- `POKELLM_LOG_PATH=C:\custom\path\game.log` - Custom log file location

### Configuration Priority
Environment variables override appsettings.json values.

## Log File Location

Logs are stored in your user documents folder alongside game data:

```
?? Documents/PokeLLM/
  ?? Games/           ? Game save files
  ?? Logs/            ? Log files
    ?? pokellm-2024-01-15_14-30-45.log
    ?? pokellm-2024-01-15_15-22-18.log
```

Each session creates a new timestamped log file.

## Log Format

```
[2024-01-15 14:30:45.123] USERINPUT    Hello, I want to explore the forest
[2024-01-15 14:30:45.124] DEBUG        [GameController] Starting to process input
[2024-01-15 14:30:45.125] LLMRESPONSE  You venture into the ancient forest...
[2024-01-15 14:30:45.126] FUNCTIONCALL Function: search_location
                                       Parameters: {"locationId": "forest_entrance"}
                                       Result: {"entities": ["ancient_tree", "forest_path"]}
```

## Debug Mode vs Standard Mode

### Standard Mode (Debug:Enabled = false)
- **File Logging**: ? Full logging to file
- **Console Output**: ? Minimal console output  
- **Prompts**: ?? Standard prompts used
- **Function Visibility**: ? LLM doesn't show function details

### Debug Mode (Debug:Enabled = true)  
- **File Logging**: ? Full logging to file
- **Console Output**: ? Verbose console logging
- **Prompts**: ?? Enhanced debug prompts used
- **Function Visibility**: ? LLM shows all function calls and parameters

## Common Configurations

### Production/Normal Use
```json
{
  "Debug": {
    "Logging": true,        // Keep logs for troubleshooting
    "Enabled": false,       // No debug prompts
    "VerboseLogging": false,
    "UseDebugPrompts": false
  }
}
```

### Development/Troubleshooting
```json
{
  "Debug": {
    "Logging": true,        // Full logging
    "Enabled": true,        // Debug prompts + verbose console
    "VerboseLogging": true,
    "UseDebugPrompts": true
  }
}
```

### Minimal Logging (Not Recommended)
```json
{
  "Debug": {
    "Logging": false,       // No file logging
    "Enabled": false
  }
}
```

## Troubleshooting

### No Log File Created
1. Check if logging is enabled: `"Debug": { "Logging": true }`
2. Verify the Documents/PokeLLM/Logs directory exists
3. Check file permissions for the Documents folder

### Large Log Files
Log files can grow large during extended sessions. Each session creates a new file, so you can safely delete old logs.

### Performance Impact
Logging has minimal performance impact. Logs are written asynchronously and flushed every 5 seconds.

## Integration Points

File logging is integrated throughout the application and works regardless of debug mode:

- **Program.cs**: Application startup, configuration loading, ruleset selection
- **GameController.cs**: User input processing, phase transitions  
- **PhaseService.cs**: LLM interactions, prompt loading, response processing
- **UnifiedContextService.cs**: Context management operations
- **LLM Providers**: Kernel creation, execution settings
- **All Plugin Functions**: Function calls with parameters and results

**Key Point**: Debug mode only changes the prompts and console verbosity - all the important data is always logged to files for troubleshooting.