# PokeLLM Debug Mode

PokeLLM includes a comprehensive debug mode that provides enhanced logging and specialized debug prompts for troubleshooting and development.

## Enabling Debug Mode

### Environment Variables

Set the `POKELLM_DEBUG` environment variable to enable debug mode:

```bash
# Windows Command Prompt
set POKELLM_DEBUG=true

# Windows PowerShell
$env:POKELLM_DEBUG = "true"

# Linux/macOS
export POKELLM_DEBUG=true
```

### Additional Debug Environment Variables

- `POKELLM_VERBOSE=true` - Enables verbose console output (requires debug mode)
- `POKELLM_DEBUG_PROMPTS=false` - Disables debug prompts but keeps logging
- `POKELLM_LOG_PATH=path/to/custom/log.txt` - Custom log file location

### Configuration File

Alternatively, you can enable debug mode in `appsettings.json`:

```json
{
  "Debug": {
    "Enabled": true,
    "VerboseLogging": true,
    "UseDebugPrompts": true,
    "LogPath": "Logs/custom-debug.log"
  }
}
```

## Debug Features

### 1. Enhanced Logging

When debug mode is enabled, all program output is captured and written to log files:

- **User Input**: Every input message from the user
- **System Output**: All streaming responses
- **LLM Responses**: Complete LLM outputs
- **Function Calls**: Function names, parameters, and results
- **Phase Transitions**: Game phase changes with reasons
- **Game State**: Complete game state JSON after changes
- **Prompts**: System prompts and their content
- **Errors**: Detailed error messages with stack traces

### 2. Debug Prompts

Debug mode uses specialized prompts located in `Prompts/Debug/` that:

- **List all available functions** at the start of each interaction
- **Require verbose function calling** with detailed explanations
- **Show function parameters and results** in the response
- **Explain reasoning** for each function call
- **Display raw function output** verbatim

### 3. Log File Location

Debug logs are automatically created in:
- Default: `Logs/pokellm-debug-{timestamp}.log` in the application directory
- Custom: Location specified by `POKELLM_LOG_PATH` environment variable or config

### 4. Log Format

Log entries include:
- Timestamp with milliseconds
- Log level (DEBUG, USER INPUT, SYSTEM OUTPUT, etc.)
- Detailed message content

Example log entry:
```
[2025-08-16 16:45:23.456] USER INPUT     Hello, I want to start a Pokemon adventure
[2025-08-16 16:45:23.789] LLM            **DEBUG MODE ENABLED** Listing all available functions...
[2025-08-16 16:45:24.123] FUNCTION_CALL  Function: select_trainer_class
                                         Parameters: {"trainerClass": "trainer"}
                                         Result: Trainer class set successfully
```

## Using Debug Mode

### Starting the Application

1. Set the environment variable: `set POKELLM_DEBUG=true`
2. Run the application: `dotnet run --project PokeLLM/PokeLLM.Game.csproj`
3. Check the console for the debug log file location

### What to Expect

When debug mode is active:

1. **Console Output**: You'll see verbose logging messages in the console
2. **LLM Behavior**: The LLM will be much more verbose, showing all available functions and explaining every action
3. **File Logging**: All interactions are logged to a timestamped file
4. **Function Visibility**: You can see exactly what functions are available and how they're being called

### Example Debug Session

```
[DEBUG] Logging enabled. Log file: C:\temp\pokellm-debug-2025-08-16_16-45-23.log
[USER INPUT] I want to create a character
[DEBUG] [GameSetupPhaseService] Processing user input: I want to create a character
[SYSTEM] **DEBUG MODE ENABLED**

Before I help you create a character, let me list ALL available functions I have access to:

1. **select_trainer_class** - Choose your trainer class/profession
2. **choose_starter_pokemon** - Select your starting Pokemon companion  
3. **set_trainer_name** - Set your character's name
4. **finalize_game_setup** - Complete setup and begin your adventure

Now, let me help you create your character. I'll start by calling the function to show available trainer classes...

FUNCTION CALL: select_trainer_class
PARAMETERS: {"showOptions": true}
RESULT: Available trainer classes: Ace Trainer, Bug Catcher, Youngster, Lass...
```

## Debug Prompt Structure

All debug prompts follow this pattern:

1. **Function Discovery Section**: Lists every available function with descriptions
2. **Enhanced Instructions**: Detailed requirements for verbose function usage
3. **Step-by-Step Process**: Explicit workflow with debug logging requirements
4. **Verification Requirements**: Mandatory display of all function results

## Troubleshooting

### Debug Mode Not Working

- Verify the environment variable is set: `echo $POKELLM_DEBUG` (Linux/macOS) or `echo %POKELLM_DEBUG%` (Windows)
- Check file permissions for the log directory
- Ensure the application has write access to the log location

### Log File Not Created

- Check if the `Logs` directory exists in your application folder
- Verify file system permissions
- Try setting a custom log path with `POKELLM_LOG_PATH`

### Prompts Not Loading

- Ensure `Prompts/Debug/` directory exists
- Check that debug prompt files are present and readable
- Verify the debug configuration is properly enabled

## Performance Impact

Debug mode has some performance implications:

- **File I/O**: Continuous writing to log files
- **Verbose Prompts**: Larger prompts and responses
- **Function Listing**: Additional processing to enumerate functions
- **Memory Usage**: Log message queuing

For production use, disable debug mode to optimize performance.

## Security Notes

- Debug logs contain all user inputs and system outputs
- Game state information is logged in full
- Function parameters and results are captured
- Ensure log files are secured and not accessible to unauthorized users
- Consider log rotation for long-running debug sessions