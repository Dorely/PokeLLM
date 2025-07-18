using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using PokeLLM.Game.Configuration;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.Game.Plugins;
using PokeLLM.Game.VectorStore.Interfaces;
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

    public void RegisterPlugins(IVectorStoreService vectorStoreService)
    {
        _kernel.Plugins.AddFromType<PokemonBattlePlugin>();
        _kernel.Plugins.AddFromObject(new VectorStorePlugin(vectorStoreService));
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
You are generating structured data for a vector database that will be used to implement a Pokémon-themed text-based adventure game. Your task is to create and describe entities that can be referenced for consistency and retrieval as the adventure unfolds.

DATA GENERATION INSTRUCTIONS:
- Generate detailed and unique entries for the following categories:
  - Locations: Towns, routes, landmarks, dungeons, gyms, and other places in the Pokémon world. Include descriptions, notable features, and possible events.
  - Lore: Historical events, myths, legends, and background stories that enrich the world and provide context for quests and characters.
  - Characters: Trainers, gym leaders, NPCs, rivals, and other personalities. Include names, roles, motivations, relationships, and backstories.
  - Questlines: Multi-step adventures, challenges, or story arcs. Describe objectives, progression, rewards, and how they connect to other entities.
  - Items: Usable objects, key items, artifacts, and equipment. Include descriptions, effects, and relevance to quests or lore.

VECTOR STORE USAGE:
- Structure each entry so it can be embedded and stored in a vector database for semantic search and retrieval.
- Ensure each entity is self-contained, with enough detail to be referenced independently or in combination with other entries.
- Use clear headings and consistent formatting for each category and entry.
- Avoid duplicating existing entries; strive for variety and depth.

PLUGIN FUNCTION CALLING:
- You have access to the following functions for searching and storing adventure data:
  - search_all(query, limit): Search all adventure data for relevant context.
  - store_location(...): Store a new location entry.
  - store_npc(...): Store a new NPC entry.
  - store_item(...): Store a new item entry.
  - store_lore(...): Store a new lore entry.
  - store_quest(...): Store a new quest entry.
  - store_event(...): Store a new event entry.
  - store_dialogue(...): Store a new dialogue entry.
- Use these functions to retrieve or store data as needed for consistency and reference.

NARRATIVE REQUIREMENTS:
- Make the world feel alive and interconnected. Reference locations, lore, characters, questlines, and items across entries where appropriate.
- Provide enough context for each entity so it can be used to answer questions, generate story content, or drive gameplay.
- Focus on creativity, coherence, and consistency.

You are not simulating battles or gameplay directly. Your primary goal is to generate high-quality, referenceable data for use in a Pokémon-themed text adventure and its supporting vector database.
";
            history.AddSystemMessage(systemPrompt);
        }
    }
}
