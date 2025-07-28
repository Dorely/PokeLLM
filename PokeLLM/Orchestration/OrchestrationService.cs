using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace PokeLLM.Game.Orchestration;
public interface IOrchestrationService
{
    public IAsyncEnumerable<string> OrchestrateAsync(string inputMessage, CancellationToken cancellationToken = default);
    public Task<string> RunContextManagement(string directive, CancellationToken cancellationToken = default);
}

public class OrchestrationService : IOrchestrationService
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGameStateRepository _gameStateRepository;
    private Dictionary<string, Kernel> _kernels;
    private Dictionary<string, ChatHistory> _histories;
    
    private GamePhase _currentPhase;

    public OrchestrationService(
        ILLMProvider llmProvider,
        IGameStateRepository gameStateRepository)
    {
        _llmProvider = llmProvider;
        _gameStateRepository = gameStateRepository;
        //TODO instantiate a kernel and history for all chat prompts
        //load the right plugin for each kernel
        SetupPromptsAndPlugins();
    }
    private void SetupPromptsAndPlugins()
    {
        //TODO
    }

    public async IAsyncEnumerable<string> OrchestrateAsync(string inputMessage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        //TODO main game flow logic goes here.
        //Check the current game phase, and retrieve the correct kernel and history
        //Execute the context gathering subroutine and add its results as a system message
        var kernel = await _llmProvider.CreateKernelAsync(); //TEMP
        var history = new ChatHistory(); //TEMP
        history.AddSystemMessage("This is where the system prompt needs to go"); //TEMP system prompt will be added when history is first initialized
        history.AddSystemMessage(inputMessage);//TEMP before the input message is added, the message will be intercepted by the context gathering subroutine
        var result = ExecutePromptStreamingAsync(history, kernel, cancellationToken);


        var responseBuilder = new StringBuilder();
        await foreach (var chunk in result)
        {
            var chunkText = chunk.ToString();
            responseBuilder.Append(chunkText);
            yield return chunkText;
        }

    }

    public async Task<string> RunContextManagement(string directive, CancellationToken cancellationToken = default)
    {
        //This method will be exposed to various plugins and will run a chat subroutine with instructions to:
        //Inspect the chat histories, the game state, stored context
        //methodically go through and correct any consistencies between them
        //This will do things like make sure NPCs exist in the vector database, and that they also exist in the worldNpcs collection
        //This will have instructions not to invent anything new unless it was mentioned already in the chat histories in an authoritative way
        //For example, the GM stating "You enter the town and see an old man doing something" this is authoritative and the old man needs to be brought into context
        //But if the player were to have something in their chat history that is like "I go and find a blacksmith" then this would search the vector DB for the current location
        //and if there is a blacksmith, it will include proper context, but if there is not, it will provide guidance to the GM that there is no blacksmith and to deny the player's request
        var kernel = await _llmProvider.CreateKernelAsync(); //TEMP
        var history = new ChatHistory(); //TEMP
        history.AddSystemMessage("This is where the system prompt needs to go"); //TEMP system prompt will be added when history is first initialized
        history.AddSystemMessage(directive);//TEMP before the input message is added, the message will be intercepted by the context gathering subroutine
        var result = await ExecutePromptAsync(history, kernel, cancellationToken);
        return result;
    }

    private async Task<string> ExecutePromptAsync(ChatHistory history, Kernel kernel, CancellationToken cancellationToken = default)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);

        var result = await chatService.GetChatMessageContentAsync(
            history,
            executionSettings,
            kernel,
            cancellationToken
        );
        
        var response = result.ToString();
        await AddResponseToHistory(response, history);
        
        return response;
    }

    private async IAsyncEnumerable<string> ExecutePromptStreamingAsync(ChatHistory history, Kernel kernel, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var executionSettings = _llmProvider.GetExecutionSettings(
            maxTokens: 10000,
            temperature: 0.7f,
            enableFunctionCalling: true);
        
        var responseBuilder = new StringBuilder();
        var result = chatService.GetStreamingChatMessageContentsAsync(
            history,
            executionSettings,
            kernel,
            cancellationToken
        );
        
        await foreach (var chunk in result)
        {
            var chunkText = chunk.ToString();
            responseBuilder.Append(chunkText);
            yield return chunkText;
        }
        
        await AddResponseToHistory(responseBuilder.ToString(), history);
    }

    private async Task AddResponseToHistory(string response, ChatHistory history)
    {
        await Task.Yield();
        history.AddAssistantMessage(response);

        //TODO add history management logic here
    }


    public async Task<string> LoadSystemPromptAsync(GamePhase phase)
    {
        try
        {
            var promptPath = phase switch
            {
                GamePhase.GameCreation => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md"),
                GamePhase.CharacterCreation => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CharacterCreationPhase.md"),
                GamePhase.WorldGeneration => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "WorldGenerationPhase.md"),
                GamePhase.Exploration => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "ExplorationPhase.md"),
                GamePhase.Combat => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "CombatPhase.md"),
                GamePhase.LevelUp => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "LevelUpPhase.md"),
                _ => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "GameCreationPhase.md")
            };
            var systemPrompt = await File.ReadAllTextAsync(promptPath);

            return systemPrompt;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not load system prompt for phase {phase}: {ex.Message}");
            throw;
        }
    }

}