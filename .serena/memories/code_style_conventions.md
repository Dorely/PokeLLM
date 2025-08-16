# PokeLLM Code Style and Conventions

## C# Code Style (from .editorconfig)

### General Conventions
- **Target Framework**: .NET 8.0
- **Implicit Usings**: Enabled
- **Nullable**: Enabled in test projects
- **Indentation**: 4 spaces, tabs width 4
- **Line Endings**: CRLF (Windows)

### Naming Conventions
- **Interfaces**: Must begin with 'I' (e.g., `IGameController`)
- **Types**: PascalCase (classes, structs, interfaces, enums)
- **Members**: PascalCase (properties, events, methods)
- **Example**: `IGameStateRepository`, `ProcessInputAsync()`

### Code Style Preferences
- **Namespaces**: Block-scoped (not file-scoped)
- **Using Directives**: Outside namespace, simple using statements preferred
- **Method Bodies**: Block bodies for methods, constructors, operators (not expression-bodied)
- **Properties**: Expression-bodied properties preferred
- **Primary Constructors**: Preferred when applicable
- **Braces**: Always use braces for control structures

### Architecture Patterns
- **Dependency Injection**: Extensive use of DI container
- **Interface Segregation**: Services implement focused interfaces
- **Repository Pattern**: Used for data access (GameStateRepository)
- **Plugin Pattern**: Phase-specific plugins with Semantic Kernel
- **Provider Pattern**: Abstracted LLM providers with multiple implementations

### Project-Specific Conventions
- **Services**: End with 'Service' (e.g., `GameLogicService`)
- **Interfaces**: Start with 'I' and match implementation name
- **Plugins**: End with 'Plugin' (e.g., `ExplorationPhasePlugin`)
- **Models**: End with 'Model' for data structures
- **Async Methods**: End with 'Async' and return Task/Task<T>

### File Organization
- **Prompts**: Markdown files in Prompts/ directory
- **Rulesets**: JSON files in Rulesets/ directory
- **Configuration**: Centralized in Configuration/ directory
- **Tests**: Separate test project with comprehensive coverage

## Diagnostic Suppressions
- **SKEXP0070**: Semantic Kernel experimental features (suppressed)
- **SKEXP0001**: Additional SK experimental warnings (silenced)