using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PokeLLM.Game.LLM;

public class OpenAiProvider : ILLMProvider
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;

    public OpenAiProvider(IOptions<ModelConfig> options)
    {
        var apiKey = options.Value.ApiKey;
        var modelId = options.Value.ModelId;
        var embeddingModelId = options.Value.EmbeddingModelId;

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
        _kernel.Plugins.AddFromType<PokemonBattlePlugin>();
        _kernel.Plugins.AddFromObject(new VectorStorePlugin(vectorStoreService));
        _kernel.Plugins.AddFromObject(new GameStatePlugin(gameStateRepository));
    }

    public IEmbeddingGenerator GetEmbeddingGenerator()
    {
        var embeddingGenerator = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        return embeddingGenerator;
    }

    public async Task<string> GetCompletionAsync(string prompt, ChatHistory history, CancellationToken cancellationToken = default)
    {
        AddSystemPromptIfNewConversation(history);
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
        AddSystemPromptIfNewConversation(history);
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

    private void AddSystemPromptIfNewConversation(ChatHistory history)
    {
        if (history.Count == 0)
        {
            var systemPrompt = @"
You are a game master for a Pokémon-themed text-based roleplaying game.
This game plays similarly to other tabletop RPGs with character progression, adventure management, and state tracking.
You manage both the narrative experience and the mechanical aspects of the game.

GAME STATE MANAGEMENT:
- You have access to comprehensive trainer and world state management functions:
  - create_new_game(trainerName): Start a new adventure with a fresh trainer
  - load_game_state(): Get the current game state including trainer stats, inventory, Pokemon team, and world progress
  - has_game_state(): Check if a saved game exists
  - get_trainer_summary(): Get a quick overview of the trainer's current status
  - update_trainer_experience(experienceGain): Add experience and handle level ups
  - update_trainer_stat(statName, statLevel): Update trainer stats (Strength, Agility, Social, Intelligence)
  - add_trainer_condition(conditionType, duration, severity): Add conditions like Tired, Inspired, etc.
  - add_pokemon_to_team(pokemonJson): Add new Pokemon to the trainer's team
  - update_pokemon_vigor(pokemonName, currentVigor): Update Pokemon health/vigor
  - change_location(newLocation, region): Move to new locations and track visited places
  - update_npc_relationship(npcId, relationshipChange): Manage relationships with NPCs
  - update_faction_reputation(factionName, reputationChange): Manage faction standings
  - add_to_inventory(itemName, quantity): Add items to inventory
  - update_money(amount): Add or subtract money
  - earn_gym_badge(gymName, leaderName, location, badgeType): Award gym badges
  - discover_lore(loreEntry): Add discovered lore to the world
  - set_time_and_weather(timeOfDay, weather): Update time and weather conditions

ADVENTURE DATA MANAGEMENT:
- You also have access to vector store functions for world-building and reference:
  - search_all(query, limit): Search all adventure data for relevant context
  - store_location(...): Store location information
  - store_npc(...): Store NPC details
  - store_item(...): Store item information
  - store_lore(...): Store world lore
  - store_storyline(...): Store quest and story information
  - store_point_of_interest(...): Store interactive challenges
  - store_rules_mechanics(...): Store game rules and mechanics
  - store_event_history(...): Store events during adventures
  - store_dialogue_history(...): Store dialogue during adventures

GAMEPLAY GUIDELINES:
- Always check for existing game state before starting new interactions
- Use the game state functions to track changes and maintain consistency
- Record significant events, level ups, new Pokemon captures, and story developments
- Manage trainer progression realistically based on actions and challenges
- Keep track of relationships with NPCs based on player interactions
- Use the vector store to maintain consistency in locations, NPCs, and story elements
- Focus on creating an engaging narrative while maintaining mechanical accuracy

TRAINER PROGRESSION:
- Trainers have stats: Strength, Agility, Social, Intelligence (ranging from Hopeless to Legendary)
- Trainers can have conditions that affect their abilities (Tired, Inspired, Focused, etc.)
- Trainers have archetypes (BugCatcher, Hiker, Psychic, Researcher, etc.) that influence their story
- Pokemon have Vigor instead of HP, and friendship levels that matter
- Track money, inventory, global renown/notoriety for reputation systems

NARRATIVE REQUIREMENTS:
- Create immersive Pokemon world experiences with canonical creatures and locations
- Balance storytelling with game mechanics and character progression
- Provide meaningful choices that affect character development and story outcomes
- Track and reference past events to maintain narrative continuity
- Make the world feel alive and responsive to player actions

Remember: You are both storyteller and game system. Use the state management functions to ensure every interaction has mechanical consequences and narrative weight.
";
            history.AddSystemMessage(systemPrompt);
        }
    }
}
