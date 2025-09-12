# Style and Conventions

- Language: C# targeting .NET 8 (`net8.0`).
- Naming: PascalCase for public types/members; camelCase for locals/parameters; interfaces prefixed with `I` (e.g., `ILLMProvider`, `IGameController`).
- Async: Suffix async methods with `Async` (e.g., `ProcessInputAsync`). Prefer `await foreach` for async streams when available.
- Dependency Injection: Use `Microsoft.Extensions.DependencyInjection`; register services in `ServiceConfiguration.ConfigureServices` and resolve via constructor injection.
- Options/Config: Bind strongly‑typed options from `appsettings.json` via `Microsoft.Extensions.Options`. Keep secrets in UserSecrets or environment variables.
- Nullability: Default project doesn’t explicitly enable `<Nullable>`; follow defensive null checks where appropriate.
- Organization: Keep features grouped by domain (`GameLogic`, `Orchestration`, `LLM`, `VectorStore`, `Plugins`, `GameState`). Prefer small, focused services with clear interfaces.
- Tests: xUnit with Moq for mocking; keep tests deterministic and provider‑agnostic when possible.
