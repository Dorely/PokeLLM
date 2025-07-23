using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using PokeLLM.Game.Plugins;
using PokeLLM.GameState.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PokeLLM.Game.LLM;

public class OpenAiProvider : ILLMProvider
{
    private readonly Kernel _kernel;
    private readonly IPhaseManager _phaseManager;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IVectorStoreService _vectorStoreService;
    private string _systemPrompt;
    private GamePhase? _currentPhase;

    public OpenAiProvider(IOptions<ModelConfig> options, IPhaseManager phaseManager, IGameStateRepository gameStateRepository, IVectorStoreService vectorStoreService)
    {
        var apiKey = options.Value.ApiKey;
        var modelId = options.Value.ModelId;
        var embeddingModelId = options.Value.EmbeddingModelId;

        _phaseManager = phaseManager;
        _gameStateRepository = gameStateRepository;
        _vectorStoreService = vectorStoreService;

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

        _kernel = kernelBuilder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public void RegisterPlugins(IVectorStoreService vectorStoreService, IGameStateRepository gameStateRepository)
    {
        // Phase-specific plugin registration is now handled by RefreshPhaseAsync
        // This method is kept for compatibility but delegates to the phase manager
    }

    public async Task RefreshPhaseAsync()
    {
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var newPhase = gameState?.CurrentPhase ?? GamePhase.GameCreation;
        
        // Only refresh if phase has changed or if this is the first time
        if (_currentPhase != newPhase)
        {
            _currentPhase = newPhase;
            _systemPrompt = null; // Force reload of system prompt
            
            // Register phase-specific plugins
            _phaseManager.RegisterPluginsForPhase(_kernel, newPhase, _vectorStoreService, _gameStateRepository);
            
            Console.WriteLine($"Phase refreshed to: {newPhase}");
        }
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        var embeddingGenerator = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        return embeddingGenerator;
    }

    public async Task<string> GetCompletionAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default)
    {
        await RefreshPhaseAsync(); // Check for phase changes before processing
        await AddSystemPromptIfNewConversationAsync(history);
        history.AddUserMessage(prompt);
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 10000
        };
        var result = await _kernel.InvokePromptAsync(
            promptTemplate: string.Join("\n", history.Select(msg => $"{msg.Role}: {msg.Content}")),
            arguments: new KernelArguments(executionSettings),
            cancellationToken: cancellationToken
        );
        var response = result.ToString();
        history.AddAssistantMessage(response);
        return response;
    }

    public async IAsyncEnumerable<string> GetCompletionStreamingAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default)
    {
        await RefreshPhaseAsync(); // Check for phase changes before processing
        await AddSystemPromptIfNewConversationAsync(history);
        history.AddUserMessage(prompt);
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 10000
        };
        var result = _kernel.InvokePromptStreamingAsync(
            promptTemplate: string.Join("\n", history.Select(msg => $"{msg.Role}: {msg.Content}")),
            arguments: new KernelArguments(executionSettings),
            cancellationToken: cancellationToken
        );
        await foreach (var chunk in result)
        {
            yield return chunk.ToString();
        }
    }

    public ChatHistory CreateHistory()
    {
        return new ChatHistory();
    }

    private async Task AddSystemPromptIfNewConversationAsync(ChatHistory history)
    {
        if (history.Count == 0)
        {
            if (_systemPrompt == null)
            {
                await LoadSystemPromptAsync();
            }
            
            history.AddSystemMessage(_systemPrompt!);
        }
    }

    private async Task LoadSystemPromptAsync()
    {
        try
        {
            if (_currentPhase.HasValue)
            {
                _systemPrompt = await _phaseManager.GetSystemPromptForPhaseAsync(_currentPhase.Value);
            }
            else
            {
                // Fallback to default if no phase is set yet
                var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md");
                _systemPrompt = await File.ReadAllTextAsync(promptPath);
            }
        }
        catch (Exception ex)
        {
            // Fallback to a basic prompt if file reading fails
            _systemPrompt = "There has been an error loading the game prompt. Do not continue";
            Console.WriteLine($"Warning: Could not load system prompt from file. Using fallback prompt. Error: {ex.Message}");
        }
    }
}
