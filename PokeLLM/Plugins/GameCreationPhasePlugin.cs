using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace PokeLLM.Game.Plugins;

public class GameCreationPhasePlugin
{
    public GameCreationPhasePlugin()
    {
    }

    [KernelFunction("placeholder")]
    [Description("Placeholder function for game creation phase")]
    public async Task<string> Placeholder()
    {
        await Task.Yield();
        return "Game creation phase placeholder";
    }
}