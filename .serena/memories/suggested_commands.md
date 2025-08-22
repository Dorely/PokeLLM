# Essential Commands for PokeLLM Development

## Build and Run
```cmd
dotnet build
dotnet run --project PokeLLM/PokeLLM.Game.csproj
```

## Testing
```cmd
dotnet test Tests/PokeLLM.Tests.csproj
```

## Clean and Rebuild
```cmd
dotnet clean
dotnet build --no-restore
```

## Windows Specific Commands
- `ls` equivalent: `dir`
- `rm` equivalent: `del` or `Remove-Item`
- `grep` equivalent: `findstr` or use `rg` (ripgrep)
- `cd` works the same as Unix
- `find` equivalent: `where` or `Get-ChildItem`

## Git Commands
Standard git commands work on Windows:
- `git status`
- `git add .`
- `git commit -m "message"`
- `git push`
- `git pull`

## Package Management
- `dotnet add package <PackageName>` - Add NuGet package
- `dotnet restore` - Restore packages
- `dotnet list package` - List installed packages