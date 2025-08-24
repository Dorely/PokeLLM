using PokeLLM.Game.Orchestration.Interfaces;
using PokeLLM.GameState;
using System;
using System.Collections.Generic;
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

    }

    private Task SetupAgents()
    {

    }

    public IAsyncEnumerable<string> ProcessInputAsync(string input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
