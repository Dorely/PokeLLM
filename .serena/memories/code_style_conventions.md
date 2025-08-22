# PokeLLM Code Style and Conventions

## C# Style Guidelines
Based on `.editorconfig` file analysis:

### Naming Conventions
- **Interfaces**: Start with `I` (e.g., `IGameAgent`, `IEventLog`)
- **Classes, Structs, Enums**: PascalCase (e.g., `GameSession`, `AdventureModule`)
- **Methods, Properties**: PascalCase (e.g., `ProcessPlayerInputAsync`, `SessionId`)
- **Fields**: Generally camelCase with underscore prefix for private fields (e.g., `_logger`, `_gameState`)

### Language Features
- **Nullable Reference Types**: Enabled project-wide
- **Implicit Usings**: Enabled
- **Top-level Statements**: Preferred for Program.cs
- **Primary Constructors**: Suggested for simple classes
- **Block-scoped Namespaces**: Preferred over file-scoped
- **Expression-bodied Members**: Preferred for properties and accessors, avoided for methods and constructors

### Code Organization
- **Using Directives**: Place outside namespace
- **Braces**: Required for all control structures
- **Indentation**: 4 spaces, no tabs
- **Line Endings**: CRLF (Windows style)

### Project-Specific Patterns
- **Records**: Used for immutable data structures (e.g., `GameTurnResult`, `GameContext`)
- **Async Methods**: All game operations are async with `CancellationToken` support
- **Extension Methods**: Used for service registration (e.g., `AddGameAgents()`)
- **Interface Segregation**: Small, focused interfaces for each service

### File Organization
- One class per file, file name matches class name
- Group related interfaces and implementations in same directory
- Separate concerns: Agents, Controllers, State, Configuration
- Use meaningful directory structure that reflects architecture

### Semantic Kernel Specific
- Suppress experimental warnings via `.editorconfig`
- Use proper `ChatHistory` management
- Implement `IAsyncEnumerable<ChatMessageContent>` for streaming
- Use dependency injection for Kernel and agent creation