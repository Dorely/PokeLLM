using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Text;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.Plugins;
using System.Threading;
using System.Threading.Tasks;

namespace PokeLLM.LLM;

public class GeminiProvider : ILLMProvider
{
    private readonly Kernel _kernel;
    private readonly string _modelId;
    private readonly string _apiKey;
    private readonly IChatCompletionService _chatService;

    public GeminiProvider(IOptions<ModelConfig> options)
    {
        _apiKey = options.Value.ApiKey;
        _modelId = options.Value.ModelId;

        _kernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(_modelId, _apiKey)
            .Build();

        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _kernel.Plugins.AddFromType<PokemonBattlePlugin>();

    }

    public async Task<string> GetCompletionAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default)
    {
        // Add system message if it's a new conversation to explain plugin usage
        if (history.Count == 0)
        {
            var systemPrompt = @"
You are a Pokemon battle simulator and opponent AI. The human player controls one Pokemon, and you control the opposing Pokemon as their battle opponent.

BATTLE SIMULATION RULES:
- This is a turn-based Pokemon battle between the player's Pokemon and your opponent Pokemon
- You have access to damage calculation functions for accurate battle mechanics
- The player will tell you what move their Pokemon uses
- You must respond with both the player's attack results AND your opponent's counterattack
- You are responsible for choosing appropriate moves for your opponent Pokemon and playing strategically

DAMAGE CALCULATION - Use these JSON formats with the calculate_damage function:
- Attacker: {""name"":""PokemonName"",""level"":25,""attack"":55,""specialAttack"":90,""defense"":40,""specialDefense"":50,""type1"":""electric"",""type2"":""""}
- Defender: {""name"":""PokemonName"",""level"":30,""attack"":125,""specialAttack"":60,""defense"":79,""specialDefense"":100,""type1"":""water"",""type2"":""flying""}
- Move: {""name"":""MoveName"",""power"":90,""type"":""electric"",""category"":""special""}

BATTLE TURN SEQUENCE - ALWAYS follow this pattern:
1. PLAYER'S TURN: Calculate and narrate the player's Pokemon attack using calculate_damage
2. OPPONENT'S TURN: Immediately have YOUR opponent Pokemon counterattack:
   - Choose a strategic move based on type advantages, remaining health, and battle situation
   - Use realistic move data (power 60-120, appropriate type and category)
   - Calculate damage using calculate_damage (with roles swapped)
   - Narrate your opponent's attack

OPPONENT AI STRATEGY:
- Play to win - choose moves that are strategically sound
- Consider type effectiveness when selecting moves
- Use stronger moves when the opponent is low on health
- Vary your move selection to keep battles interesting
- Act like a skilled Pokemon trainer, not just random move selection

NARRATIVE REQUIREMENTS:
- Describe actual damage numbers for both attacks
- Mention type effectiveness (""It's super effective!"", ""It's not very effective..."", etc.)
- Note critical hits when they occur
- Describe the battle state after both attacks (health status, momentum, etc.)
- End each turn by indicating what moves your opponent might use next or asking what the player wants to do

NEVER end a turn with only the player's attack - always complete the full exchange with your opponent's counterattack.
";

            history.AddSystemMessage(systemPrompt);
        }

        history.AddUserMessage(prompt);

        // Use kernel.InvokePromptAsync instead of direct chat service to enable function calling
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 1000,
            Temperature = 0.7
        };

        var result = await _kernel.InvokePromptAsync(
            promptTemplate: string.Join("\n", history.Select(msg => $"{msg.Role}: {msg.Content}")),
            arguments: new KernelArguments(executionSettings),
            cancellationToken: cancellationToken
        );

        var response = result.ToString();

        // Add the assistant's response to history
        history.AddAssistantMessage(response);

        return response;
    }

    public ChatHistory CreateHistory()
    {
        var history = new ChatHistory();
        return history;
    }

    public IAsyncEnumerable<string> GetCompletionStreamingAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        throw new NotImplementedException();
    }
}