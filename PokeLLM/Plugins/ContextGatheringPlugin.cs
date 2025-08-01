using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing context consistency across vector database, game state, and chat histories
/// </summary>
public class ContextGatheringPlugin
{
    public ContextGatheringPlugin()
    {
    }

    [KernelFunction("place_holder")]
    [Description("placeholder")]
    public async Task<string> PlaceHolder(
        [Description("placeholder")] string placeholder)
    {
        throw new NotImplementedException("placeholder not yet implemented");
    }

    //TODO
    /*
     * Context Gathering is a readonly step that needs access to functions that let is gather information for the main story teller
     * functions needed: all vector lookups, full gamestate read
     * 
     */
}