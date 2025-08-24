using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.Orchestration.Interfaces;

public interface IGameController
{
    IAsyncEnumerable<string> ProcessInputAsync(string input, CancellationToken cancellationToken = default);
}
