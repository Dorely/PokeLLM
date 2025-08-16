# PokeLLM Project Overview

## Purpose
PokeLLM is a C# console application that creates a structured Pokemon role-playing game powered by Large Language Models (LLM). The application interfaces with LLM APIs to run an interactive Pokemon RPG experience.

## Tech Stack
- **Framework**: .NET 8.0 Console Application
- **Language**: C# with implicit usings enabled
- **LLM Integration**: Microsoft Semantic Kernel (v1.62.0)
- **LLM Providers**: 
  - OpenAI GPT models
  - Google Gemini (via alpha connector)
  - Ollama (local models, alpha connector)
- **Database**: SQLite (Microsoft.Data.Sqlite)
- **Vector Storage**: Qdrant for embeddings and semantic search
- **Configuration**: Microsoft.Extensions.Configuration with UserSecrets support
- **DI Container**: Microsoft.Extensions.DependencyInjection
- **JavaScript Engine**: Microsoft.ClearScript.V8 for rule execution
- **Testing**: xUnit with Moq for mocking

## Key Features
- Multi-provider LLM support with flexible configuration
- Dynamic ruleset system with JSON-based game mechanics
- Vector storage for semantic search and context management
- Structured game phases (Setup, World Generation, Exploration, Combat, Level Up)
- Plugin-based architecture for extensible game functionality
- Streaming responses with cancellation support
- Comprehensive game state persistence