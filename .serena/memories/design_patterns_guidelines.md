# Design Patterns and Guidelines

## Established Patterns in PokeLLM

### 1. Dependency Injection Pattern
- **Implementation**: Microsoft.Extensions.DependencyInjection
- **Registration**: All services registered in `ServiceConfiguration.ConfigureServices()`
- **Usage**: Constructor injection throughout the application
- **Interfaces**: All major services implement interfaces for testability

### 2. Repository Pattern
- **Example**: `IGameStateRepository` / `GameStateRepository`
- **Purpose**: Abstracts data access layer (SQLite)
- **Benefits**: Testable, swappable persistence mechanisms

### 3. Provider Pattern
- **Implementation**: `ILLMProvider` with multiple implementations
- **Providers**: OpenAI, Ollama, Gemini
- **Configuration**: Flexible provider selection via constants
- **Benefits**: Easy switching between LLM services

### 4. Plugin Architecture
- **Framework**: Semantic Kernel plugins
- **Structure**: Phase-specific plugins with focused functionality
- **Examples**: `ExplorationPhasePlugin`, `CombatPhasePlugin`
- **Benefits**: Modular, extensible game functionality

### 5. Strategy Pattern
- **Application**: LLM provider selection
- **Implementation**: Runtime provider switching based on configuration
- **Flexibility**: Mix and match providers (e.g., Gemini for chat, Ollama for embeddings)

### 6. Factory Pattern
- **Usage**: `DynamicFunctionFactory` for generating LLM functions
- **Purpose**: Create functions from ruleset definitions
- **Benefits**: Dynamic function generation from configuration

## Architectural Guidelines

### Service Layer Organization
- **GameLogic/**: Core business logic services
- **Orchestration/**: Phase management and coordination
- **LLM/**: Provider abstractions and implementations
- **GameState/**: Data persistence and state management

### Error Handling Strategy
- **Comprehensive**: Error handling throughout orchestration layers
- **Logging**: Debug logging for troubleshooting
- **Graceful Degradation**: Handle LLM provider failures gracefully

### Configuration Management
- **Centralized**: `ServiceConfiguration` handles all DI setup
- **Flexible**: Support for multiple provider configurations
- **Secure**: User secrets for API keys
- **Environment-Aware**: Different configs for dev/test/prod

### Testing Strategy
- **Unit Tests**: Focus on individual service functionality
- **Integration Tests**: Test service interactions and DI resolution
- **Mocking**: Use Moq for external dependencies
- **Coverage**: Emphasize critical paths and error scenarios

## Best Practices

### When Adding New Services
1. Create interface first (`IYourService`)
2. Implement service with constructor injection
3. Register in `ServiceConfiguration.ConfigureServices()`
4. Add comprehensive unit tests
5. Document public API and key behaviors

### When Adding New Game Phases
1. Create phase-specific plugin
2. Add system prompt in Prompts/ directory
3. Update orchestration logic
4. Add phase to game state model
5. Create integration tests

### When Modifying LLM Integration
1. Consider impact on all providers
2. Test with different provider configurations
3. Ensure backwards compatibility
4. Update provider-specific configurations