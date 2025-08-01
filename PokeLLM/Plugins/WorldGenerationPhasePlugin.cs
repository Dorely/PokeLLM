using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace PokeLLM.Game.Plugins;

public class WorldGenerationPhasePlugin
{
    public WorldGenerationPhasePlugin()
    {
    }

    [KernelFunction("placeholder")]
    [Description("Placeholder function. Does nothing")]
    public async Task<string> Placeholder()
    {
        await Task.Yield();
        return "placeholder";
    }
}