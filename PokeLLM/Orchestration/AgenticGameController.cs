using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PokeLLM.Game.Orchestration.Interfaces;
using PokeLLM.GameState;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.Orchestration;

public class AgenticGameController : IGameController
{
    private readonly IGameStateRepository _gameStateRepository;
    private readonly ILLMProvider _llmProvider;

    public AgenticGameController(
        IGameStateRepository gameStateRepository,
        ILLMProvider llmProvider)
    {
        _gameStateRepository = gameStateRepository;
        _llmProvider = llmProvider;

        //TODO fix or move
        SetupAgents();
    }

    public IAsyncEnumerable<string> ProcessInputAsync(string input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async Task SetupAgents()
    {
        var combatKernel = await _llmProvider.CreateKernelAsync();
        combatKernel.Plugins.AddFromType<CombatPhasePlugin>();
        var combatPrompt = await LoadSystemPrompt("CombatPhase");
        ChatCompletionAgent combatAgent = new()
        {
            Instructions = combatPrompt,
            Description = "A combat focused Dungeon Master",
            Name = "CombatAgent",
            Kernel = combatKernel,
            Arguments = new KernelArguments(_llmProvider.GetExecutionSettings(5000, .5f, true))
        };

        var gameSetupKernel = await _llmProvider.CreateKernelAsync();
        gameSetupKernel.Plugins.AddFromType<GameSetupPhasePlugin>();
        var gameSetupPrompt = await LoadSystemPrompt("GameSetupPhase");
        ChatCompletionAgent setupAgent = new()
        {
            Instructions = gameSetupPrompt,
            Description = "A character creation focused Dungeon Master",
            Name = "SetupAgent",
            Kernel = gameSetupKernel,
            Arguments = new KernelArguments(_llmProvider.GetExecutionSettings(5000, 1.0f, true))
        };

        var managerKernel = await _llmProvider.CreateKernelAsync();
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var manager = new StandardMagenticManager(
            managerKernel.GetRequiredService<IChatCompletionService>(),
            _llmProvider.GetExecutionSettings(5000, 1.0f, true)
            )
        {
            MaximumInvocationCount = 5,
        };

        var orchestration = new MagenticOrchestration(
            manager,
            setupAgent,
            combatAgent);
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


    }


    private async Task<string> LoadSystemPrompt(string promptName)
    {
        try
        {
            var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", $"{promptName}.md");
            var systemPrompt = await File.ReadAllTextAsync(promptPath);
            return systemPrompt;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AgenticGameController Error: Could not load system prompt for {promptName}: {ex.Message}.");
            throw;
        }
    }
}
