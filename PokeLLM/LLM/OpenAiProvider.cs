using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace PokeLLM.Game.LLM;

public class OpenAiProvider : ILLMProvider
{
    private readonly Kernel _kernel;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IServiceProvider _serviceProvider;
    private GamePhase _currentPhase;
    private Dictionary<GamePhase, ChatHistory> _histories = new();
    
    // Configuration for chat history management
    private const int MAX_HISTORY_LENGTH = 20; // Maximum number of messages before summarization
    private const int MESSAGES_TO_KEEP_RECENT = 6; // Keep most recent messages after summarization
    private const int MAX_CONTEXT_TOKENS = 8000; // Rough token limit for context

    public OpenAiProvider(IOptions<ModelConfig> options, IGameStateRepository gameStateRepository, IVectorStoreService vectorStoreService, IServiceProvider serviceProvider)
    {
        var apiKey = options.Value.ApiKey;
        var modelId = options.Value.ModelId;
        var embeddingModelId = options.Value.EmbeddingModelId;

        _gameStateRepository = gameStateRepository;
        _vectorStoreService = vectorStoreService;
        _serviceProvider = serviceProvider;

        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey
        );

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        kernelBuilder.AddOpenAIEmbeddingGenerator(
            modelId: embeddingModelId,
            apiKey: apiKey
        );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        // Register plugin services in kernel DI
        kernelBuilder.Services.AddSingleton(_gameStateRepository);
        kernelBuilder.Services.AddSingleton(_vectorStoreService);

        _kernel = kernelBuilder.Build();
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        var embeddingGenerator = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        return embeddingGenerator;
    }

    public async Task RefreshPhaseAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var newPhase = gameState?.CurrentPhase ?? GamePhase.GameCreation;
        
        var oldPhase = _currentPhase;
        var phaseChanged = _currentPhase != newPhase;
        _currentPhase = newPhase;
        
        // Only clear and reload plugins if the phase has actually changed
        if (phaseChanged)
        {
            await ClearAllPluginsAsync();
            await LoadPhasePluginsAsync(newPhase);
            Debug.WriteLine($"Phase changed from {oldPhase} to: {newPhase}");
            Debug.WriteLine($"Plugins loaded for {newPhase} phase");
        }
        
        // Ensure histories exist for all phases
        await EnsureAllPhaseHistoriesExistAsync();
    }

    private async Task EnsureAllPhaseHistoriesExistAsync()
    {
        // Get all GamePhase enum values
        var allPhases = Enum.GetValues<GamePhase>();
        
        foreach (var phase in allPhases)
        {
            if (!_histories.ContainsKey(phase))
            {
                _histories[phase] = CreateHistory();
                Debug.WriteLine($"Created history for {phase} phase");
            }
        }
    }

    private async Task ClearAllPluginsAsync()
    {
        try
        {
            // Clear all existing plugins
            _kernel.Plugins.Clear();
            Debug.WriteLine("All plugins cleared from kernel");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Failed to clear plugins: {ex.Message}");
        }
    }

    private async Task LoadPhasePluginsAsync(GamePhase phase)
    {
        try
        {
            switch (phase)
            {
                case GamePhase.GameCreation:
                    await LoadGameCreationPluginsAsync();
                    break;
                    
                case GamePhase.CharacterCreation:
                    await LoadCharacterCreationPluginsAsync();
                    break;
                    
                case GamePhase.WorldGeneration:
                    await LoadWorldGenerationPluginsAsync();
                    break;
                    
                case GamePhase.Exploration:
                    await LoadExplorationPluginsAsync();
                    break;
                    
                case GamePhase.Combat:
                    await LoadCombatPluginsAsync();
                    break;
                    
                case GamePhase.LevelUp:
                    await LoadLevelUpPluginsAsync();
                    break;
                    
                default:
                    Debug.WriteLine($"Warning: Unknown phase {phase}, loading default plugins");
                    await LoadGameCreationPluginsAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading plugins for phase {phase}: {ex.Message}");
        }
    }

    private async Task LoadGameCreationPluginsAsync()
    {
        // Game Creation only needs phase transition to get started
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        // Core game engine for creating new game
        var gameEnginePlugin = new GameEnginePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        Debug.WriteLine("Loaded Game Creation plugins: PhaseTransition, GameEngine");
    }

    private async Task LoadCharacterCreationPluginsAsync()
    {
        // Character creation specific functions
        var characterCreationPlugin = new CharacterCreationPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(characterCreationPlugin, "CharacterCreation");
        
        // Phase transitions to next stage
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        // Basic dice rolling for character generation
        var dicePlugin = new DicePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        Debug.WriteLine("Loaded Character Creation plugins: CharacterCreation, PhaseTransition, Dice");
    }

    private async Task LoadWorldGenerationPluginsAsync()
    {
        // World building and information storage
        var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
        _kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
        
        // Phase transitions
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        // Core game engine for world state management
        var gameEnginePlugin = new GameEnginePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        // Dice for random world generation
        var dicePlugin = new DicePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        Debug.WriteLine("Loaded World Generation plugins: VectorStore, PhaseTransition, GameEngine, Dice");
    }

    private async Task LoadExplorationPluginsAsync()
    {
        // Full plugin suite for exploration
        var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
        _kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
        
        var gameEnginePlugin = new GameEnginePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        Debug.WriteLine("Loaded Exploration plugins: VectorStore, GameEngine, Dice, PhaseTransition");
    }

    private async Task LoadCombatPluginsAsync()
    {
        // Combat-focused plugins
        var gameEnginePlugin = new GameEnginePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        // Vector store for combat reference data (moves, abilities, etc.)
        var vectorStorePlugin = new VectorStorePlugin(_vectorStoreService);
        _kernel.ImportPluginFromObject(vectorStorePlugin, "VectorStore");
        
        Debug.WriteLine("Loaded Combat plugins: GameEngine, Dice, PhaseTransition, VectorStore");
    }

    private async Task LoadLevelUpPluginsAsync()
    {
        // Level up uses character creation functions for stat increases
        var characterCreationPlugin = new CharacterCreationPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(characterCreationPlugin, "CharacterCreation");
        
        var gameEnginePlugin = new GameEnginePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(gameEnginePlugin, "GameEngine");
        
        var dicePlugin = new DicePlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(dicePlugin, "Dice");
        
        var phaseTransitionPlugin = new PhaseTransitionPlugin(_gameStateRepository);
        _kernel.ImportPluginFromObject(phaseTransitionPlugin, "PhaseTransition");
        
        Console.WriteLine("Loaded Level Up plugins: CharacterCreation, GameEngine, Dice, PhaseTransition");
    }

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        await RefreshPhaseAsync(); // Check for phase changes before processing
        await AddSystemPromptIfNewConversationAsync(_histories[_currentPhase]);
        
        // Check if summarization is needed before adding new message
        await CheckAndSummarizeHistoryAsync(_histories[_currentPhase]);
        
        _histories[_currentPhase].AddUserMessage(prompt);
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 10000
        };
        var result = await _kernel.InvokePromptAsync(
            promptTemplate: string.Join("\n", _histories[_currentPhase].Select(msg => $"{msg.Role}: {msg.Content}")),
            arguments: new KernelArguments(executionSettings),
            cancellationToken: cancellationToken
        );
        var response = result.ToString();
        _histories[_currentPhase].AddAssistantMessage(response);
        return response;
    }

    public async IAsyncEnumerable<string> GetCompletionStreamingAsync(string prompt, CancellationToken cancellationToken = default)
    {
        await RefreshPhaseAsync(); // Check for phase changes before processing
        await AddSystemPromptIfNewConversationAsync(_histories[_currentPhase]);
        
        // Check if summarization is needed before adding new message
        await CheckAndSummarizeHistoryAsync(_histories[_currentPhase]);
        
        _histories[_currentPhase].AddUserMessage(prompt);
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 10000
        };
        
        var responseBuilder = new StringBuilder();
        var result = _kernel.InvokePromptStreamingAsync(
            promptTemplate: string.Join("\n", _histories[_currentPhase].Select(msg => $"{msg.Role}: {msg.Content}")),
            arguments: new KernelArguments(executionSettings),
            cancellationToken: cancellationToken
        );
        
        await foreach (var chunk in result)
        {
            var chunkText = chunk.ToString();
            responseBuilder.Append(chunkText);
            yield return chunkText;
        }
        
        // Add complete response to history
        _histories[_currentPhase].AddAssistantMessage(responseBuilder.ToString());
    }

    public ChatHistory CreateHistory()
    {
        return new ChatHistory();
    }

    private async Task AddSystemPromptIfNewConversationAsync(ChatHistory history)
    {
        if (history.Count == 0)
        {
            var prompt = await LoadSystemPromptAsync();
            
            history.AddSystemMessage(prompt);
        }
    }

    private async Task CheckAndSummarizeHistoryAsync(ChatHistory history)
    {
        // Skip if history is too short or contains only system message
        if (history.Count <= MAX_HISTORY_LENGTH || history.Count <= 1)
            return;

        try
        {
            Console.WriteLine("Chat history getting long, summarizing...");
            
            // Get messages to summarize (excluding system message and recent messages)
            var systemMessage = history.FirstOrDefault(m => m.Role == AuthorRole.System);
            var recentMessages = history.TakeLast(MESSAGES_TO_KEEP_RECENT).ToList();
            var messagesToSummarize = history
                .Skip(systemMessage != null ? 1 : 0) // Skip system message
                .Take(history.Count - (systemMessage != null ? 1 : 0) - MESSAGES_TO_KEEP_RECENT)
                .ToList();

            if (messagesToSummarize.Any())
            {
                // Create summary of old messages
                var summary = await SummarizeMessagesAsync(messagesToSummarize);
                
                // Store summary in vector store for future retrieval
                await StoreConversationChunkAsync(summary, messagesToSummarize);
                
                // Rebuild history with system message, summary, and recent messages
                history.Clear();
                
                if (systemMessage != null)
                {
                    history.Add(systemMessage);
                }
                
                // Add summary as a system message
                history.AddSystemMessage($"Previous conversation summary: {summary}");
                
                // Add recent messages
                foreach (var message in recentMessages)
                {
                    history.Add(message);
                }
                
                Console.WriteLine($"History summarized. Reduced from {messagesToSummarize.Count + recentMessages.Count + (systemMessage != null ? 1 : 0)} to {history.Count} messages.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to summarize chat history: {ex.Message}");
            // Continue without summarization if it fails
        }
    }

    private async Task<string> SummarizeMessagesAsync(IEnumerable<ChatMessageContent> messages)
    {
        var chatManagementPrompt = await LoadChatManagementPromptAsync();
        
        // Format messages for summarization
        var conversationText = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));
        
        var summarizationPrompt = $@"{chatManagementPrompt}

Please summarize the following conversation segment, focusing on:
- Key story developments and character interactions
- Important decisions made by the player
- Game state changes (items acquired, pokemon caught, locations visited, etc.)
- Any ongoing plot threads or unresolved situations

Conversation to summarize:
{conversationText}

Provide a concise but comprehensive summary:";
//TODO the prompt currently instructs it to store things using functions. remove those instructions
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 500,
            Temperature = 0.3 // Lower temperature for more consistent summaries
        };

        var result = await _kernel.InvokePromptAsync(
            summarizationPrompt,
            new KernelArguments(executionSettings)
        );

        return result.ToString();
    }

    private async Task StoreConversationChunkAsync(string summary, IEnumerable<ChatMessageContent> originalMessages)
    {
        try
        {
            var chunkContent = string.Join("\n", originalMessages.Select(m => $"{m.Role}: {m.Content}"));
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            
            await _vectorStoreService.UpsertInformationAsync(
                name: $"Conversation Chunk - {_currentPhase} - {timestamp}",
                description: summary,
                content: chunkContent,
                type: "conversation_history",
//TODO fix embedding field
                tags: new[] { _currentPhase.ToString().ToLower(), "chat_history", "summarized" },
                relatedEntries: null
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to store conversation chunk in vector store: {ex.Message}");
        }
    }

    private async Task<string> LoadChatManagementPromptAsync()
    {
        try
        {
            var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ChatManagementPrompt.md");
            return await File.ReadAllTextAsync(promptPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not load chat management prompt: {ex.Message}");
            return "You are a helpful assistant that summarizes conversations concisely while preserving important details.";
        }
    }

    private async Task<string> LoadSystemPromptAsync()
    {
        try
        {
            var promptPath = _currentPhase switch
            {
                GamePhase.GameCreation => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md"),
                GamePhase.CharacterCreation => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CharacterCreationPhase.md"),
                GamePhase.WorldGeneration => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "WorldGenerationPhase.md"),
                GamePhase.Exploration => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ExplorationPhase.md"),
                GamePhase.Combat => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CombatPhase.md"),
                GamePhase.LevelUp => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "LevelUpPhase.md"),
                _ => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md")
            };
            var systemPrompt = await File.ReadAllTextAsync(promptPath);//load prompts,
            return systemPrompt;
        }
        catch (Exception ex)
        {
            // Fallback to a basic prompt if file reading fails
            Debug.WriteLine($"Warning: Could not load system prompt from file. Using fallback prompt. Error: {ex.Message}");
            return "There has been an error loading the game prompt. Do not continue";
        }
    }
}
