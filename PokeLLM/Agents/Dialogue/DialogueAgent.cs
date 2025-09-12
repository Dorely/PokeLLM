using System.Text.Json;
using PokeLLM.Game.Agents.Core.Contracts;

namespace PokeLLM.Game.Agents.Dialogue;

public interface IDialogueAgent
{
    Task<DomainResult> ExecuteAsync(ContextPack context, PlotDirective directive, string playerInput, CancellationToken cancellationToken = default);
}

public class DialogueAgent : IDialogueAgent
{
    public Task<DomainResult> ExecuteAsync(ContextPack context, PlotDirective directive, string playerInput, CancellationToken cancellationToken = default)
    {
        var narrative = GenerateSimpleReply(playerInput);

        var delta = new StateDelta
        {
            NewEvents = new List<ProposedEvent>
            {
                new ProposedEvent
                {
                    Type = "DialogueSpoken",
                    PayloadJson = JsonSerializer.Serialize(new { player = true, text = playerInput ?? string.Empty })
                },
                new ProposedEvent
                {
                    Type = "DialogueSpoken",
                    PayloadJson = JsonSerializer.Serialize(new { player = false, text = narrative })
                }
            }
        };

        return Task.FromResult(new DomainResult
        {
            Status = DomainStatus.Completed,
            StateDelta = delta,
            FinalNarrative = narrative
        });
    }

    private static string GenerateSimpleReply(string input)
    {
        input = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return "The world waits patiently. What do you do?";
        }

        return $"You say, \"{input}\". The barkeep nods thoughtfully and replies with a friendly smile.";
    }
}

