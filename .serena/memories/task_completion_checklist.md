# Task Completion Checklist

Before handing off a change:

- Build succeeds: `dotnet build`
- Tests pass: `dotnet test`
- Basic run smoke‑test: `dotnet run --project PokeLLM/PokeLLM.Game.csproj` (launches console; quick input/exiting works)
- Formatting: run `dotnet format` (if available) or ensure IDE formatting rules applied
- Config: avoid committing secrets; if you changed `appsettings.json`, verify defaults are safe; document any required env vars/UserSecrets
- Docs: update `README.md` and `AGENTS.md` if behavior or workflows changed
