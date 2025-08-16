# Suggested Commands for PokeLLM Development

## Essential Development Commands

### Build and Run
```cmd
# Build the project
dotnet build

# Run the main application
dotnet run --project PokeLLM/PokeLLM.Game.csproj

# Clean and rebuild
dotnet clean
dotnet build --no-restore
```

### Testing
```cmd
# Run all tests
dotnet test Tests/Tests.csproj

# Run tests with detailed output
dotnet test Tests/Tests.csproj --verbosity normal

# Run specific test class
dotnet test Tests/Tests.csproj --filter "ClassName=DependencyResolutionTests"
```

### Project Management
```cmd
# Restore NuGet packages
dotnet restore

# Add new package reference
dotnet add PokeLLM/PokeLLM.Game.csproj package PackageName

# List package references
dotnet list PokeLLM/PokeLLM.Game.csproj package
```

### Windows System Commands
```cmd
# List files and directories
dir
ls  # If PowerShell

# Navigate directories
cd path\to\directory

# Find files
dir /s /b *.cs          # Find all C# files recursively
findstr /s /i "text" *.cs  # Search text in C# files

# Git operations
git status
git add .
git commit -m "message"
git push
```

### Configuration Management
```cmd
# Manage user secrets (for API keys)
dotnet user-secrets set "OpenAI:ApiKey" "your-key-here" --project PokeLLM/PokeLLM.Game.csproj
dotnet user-secrets list --project PokeLLM/PokeLLM.Game.csproj
```

## Development Workflow Commands

### When Starting Development
1. `dotnet build` - Ensure project builds
2. `dotnet test Tests/Tests.csproj` - Run tests to verify baseline
3. Check git status and create feature branch if needed

### When Task is Completed
1. `dotnet build` - Verify build still works
2. `dotnet test Tests/Tests.csproj` - Run full test suite
3. Review changes and commit if appropriate