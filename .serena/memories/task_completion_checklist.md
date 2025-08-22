# Task Completion Checklist

When completing any development task in PokeLLM, follow these steps:

## 1. Build and Compilation
```cmd
dotnet build
```
- Ensure no compilation errors
- Address any new warnings if they indicate real issues
- Verify all projects in solution build successfully

## 2. Testing
```cmd
dotnet test Tests/PokeLLM.Tests.csproj
```
- Run existing tests to ensure no regressions
- Add new tests for significant new functionality
- All tests must pass before considering task complete

## 3. Code Quality Checks
- Follow naming conventions from `.editorconfig`
- Ensure proper async/await patterns
- Verify dependency injection is properly configured
- Check that nullable reference types are handled correctly

## 4. Documentation Updates
- Update CLAUDE.md if new patterns or approaches are introduced
- Add inline documentation for complex public APIs
- Update README.md only if explicitly requested

## 5. Integration Verification
- Test the application runs without errors: `dotnet run --project PokeLLM/PokeLLM.Game.csproj`
- Verify console UI loads and responds appropriately
- Check that new services integrate properly with existing architecture

## 6. Configuration Validation
- Ensure all new services are registered in ServiceConfiguration
- Verify configuration settings are properly handled
- Test with different LLM provider configurations if applicable

## 7. Agent Architecture Compliance
- New agents must implement `IGameAgent` interface
- Follow established patterns for agent communication
- Respect agent responsibilities (Setup, Supervisor, Narrator, Mechanics)
- Use proper event logging and state management

## Important Notes
- **No automatic commits**: Only commit changes when explicitly requested
- **Preserve existing patterns**: Follow established architectural decisions
- **Test thoroughly**: The application should remain functional after changes
- **Windows compatibility**: Ensure all file paths and commands work on Windows