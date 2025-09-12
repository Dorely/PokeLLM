using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.Handoff;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.Game.Agents.ContextBroker;
using PokeLLM.Game.LLM;

namespace PokeLLM.Game.Orchestration.MultiAgent;

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

        // Create agents
        var guardAgent = await CreateAgentAsync(
            name: "GuardAgent",
            description: "Validates and routes player intents.",
            instructions: "Validate the player's input against the current scene and facts. If the input is impossible or cheating, respond briefly and end. Otherwise, hand off to PlotDirector or Dialogue as needed.");

        var plotAgent = await CreateAgentAsync(
            name: "PlotDirector",
            description: "Guides pacing and selects next agent.",
            instructions: "Read the scene summary and recent events. Suggest the primary next agent. Keep responses concise and focused on objectives.");

        var dialogueAgent = await CreateAgentAsync(
            name: "DialogueAgent",
            description: "Handles NPC and social interactions.",
            instructions: "Reply as the world/NPCs in 1-3 sentences, grounded in the provided scene summary and recent events. If not a dialogue matter, hand back to PlotDirector.");

        var returnAgent = await CreateAgentAsync(
            name: "ReturnAgent",
            description: "Returns the final narrative to the player.",
            instructions: "Output only the final narrative line(s) for the player with no extra commentary.");

#pragma warning disable SKEXP0110
        // Define allowed handoffs (API is experimental and subject to change)
        var handoffs = OrchestrationHandoffs.StartWith(guardAgent);
        handoffs = handoffs.Add(guardAgent, plotAgent, dialogueAgent);
        handoffs = handoffs.Add(plotAgent, dialogueAgent, guardAgent);
        handoffs = handoffs.Add(dialogueAgent, returnAgent, plotAgent);

        // Create and run the orchestration
        var orchestration = new HandoffOrchestration(
            handoffs,
            guardAgent,
            plotAgent,
            dialogueAgent,
            returnAgent);
#pragma warning restore SKEXP0110

        await using var runtime = new InProcessRuntime();
        await runtime.StartAsync(cancellationToken);

#pragma warning disable SKEXP0110
        var result = await orchestration.InvokeAsync(task, runtime, cancellationToken);
        var output = await result.GetValueAsync(TimeSpan.FromSeconds(60), cancellationToken);
#pragma warning restore SKEXP0110

        await runtime.RunUntilIdleAsync();
        return output;
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