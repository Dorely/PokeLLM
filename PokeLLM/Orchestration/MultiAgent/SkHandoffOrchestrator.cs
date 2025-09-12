
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace PokeLLM.Game.Orchestration.MultiAgent;
// SK Handoff orchestration implementation (compiled only when SK_HANDOFF is defined)
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.Handoff;
using Microsoft.SemanticKernel.Agents.Runtime;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game.Agents.ContextBroker;
using PokeLLM.Game.LLM;

public class SkHandoffOrchestrator : ITurnOrchestrator
{


    private readonly IContextBroker _contextBroker;
    private readonly ILLMProvider _llmProvider;

    public SkHandoffOrchestrator(IContextBroker contextBroker, ILLMProvider llmProvider)
    {
        _contextBroker = contextBroker;
        _llmProvider = llmProvider;
    }

    public async Task<string> ProcessTurnAsync(string playerInput, CancellationToken cancellationToken = default)
    {
        var context = await _contextBroker.BuildContextAsync(playerInput, cancellationToken);
        var task = BuildTask(context, playerInput);

        var guardAgent = await CreateAgentAsync(
            name: "GuardAgent",
            description: "Validates and routes player intents.",
            instructions: "Validate player input; reject cheating with a brief explanation; otherwise hand off to PlotDirector or Dialogue.");

        var plotAgent = await CreateAgentAsync(
            name: "PlotDirector",
            description: "Guides pacing and selects next agent.",
            instructions: "Read scene summary and recent events; suggest the next primary agent and keep responses concise.");

        var dialogueAgent = await CreateAgentAsync(
            name: "DialogueAgent",
            description: "Handles NPC and social interactions.",
            instructions: "Reply as world/NPCs in 1-3 sentences, grounded in the scene summary and recent events. If not dialogue, hand back to PlotDirector.");

        var returnAgent = await CreateAgentAsync(
            name: "ReturnAgent",
            description: "Returns final narrative to the player.",
            instructions: "Output only the final narrative line(s) for the player with no extra commentary.");


        var handoffs = OrchestrationHandoffs
            .StartWith(guardAgent)
            .Add(guardAgent, plotAgent, dialogueAgent)
            .Add(plotAgent, dialogueAgent, guardAgent)
            .Add(dialogueAgent, returnAgent, plotAgent, "Return to PlotDirector if additional guidance is required");
        throw new NotImplementedException();

    }

    private async Task<ChatCompletionAgent> CreateAgentAsync(string name, string description, string instructions)
    {
        var kernel = await _llmProvider.CreateKernelAsync();
        return new ChatCompletionAgent
        {
            Name = name,
            Description = description,
            Instructions = instructions,
            Kernel = kernel
        };
    }

    private static string BuildTask(dynamic context, string playerInput)
    {
        string header;
        try
        {
            var recap = (context.DialogueRecap as IEnumerable<string> ?? Array.Empty<string>()).ToList();
            var events = (context.RecentEvents as IEnumerable<object> ?? Array.Empty<object>())
                .Select(e => e?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(3);

            header = $"Scene: {context.SceneId}\nTime: {context.TimeOfDay}, Weather: {context.Weather}\nSummary: {context.SceneSummary}";
            if (recap.Count > 0)
            {
                header += "\nDialogue recap: " + string.Join(" | ", recap);
            }
            var eventsJoined = string.Join(" | ", events);
            if (!string.IsNullOrWhiteSpace(eventsJoined))
            {
                header += "\nRecent events: " + eventsJoined;
            }
        }
        catch
        {
            header = string.Empty;
        }

        var input = (playerInput ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(input) ? header : header + "\n\nPlayer: " + input;
    }
}