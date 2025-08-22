# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Core Directives

Always use:
- context7 to lookup up to date documentation for libraries whenever modifying code that uses them (especially semantic-kernel)
- sequential-thinking to plan the work before beginning
- serena for semantic code retrieval and editing tools

Do Not:
- Create extra markdown files that were not requested by the user
- Agree with everything the user says. You should critically evaluate the situation and feel free to disagree and suggest an alternate course of action if the user is incorrect

## Project Overview

PokeLLM is a C# console application that creates a structured Pokemon role-playing game powered by Large Language Models (LLM). The application supports multiple LLM providers (OpenAI, Ollama, Gemini) with flexible provider configuration and uses a layered architecture with game controllers, specialized services, and vector storage.

## Essential Commands

### Build and Run
```cmd
dotnet build
dotnet run --project PokeLLM/PokeLLM.Game.csproj
```

### Testing
```cmd
dotnet test Tests/PokeLLM.Tests.csproj
```

### Clean and Rebuild
```cmd
dotnet clean
dotnet build --no-restore
```
