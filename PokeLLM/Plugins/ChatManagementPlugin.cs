using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace PokeLLM.Game.Plugins;

public class ChatManagementPlugin
{
    public ChatManagementPlugin()
    {
    }

    [KernelFunction("placeholder")]
    [Description("Placeholder function for chat management")]
    public async Task<string> Placeholder()
    {
        await Task.Yield();
        return "Chat management placeholder";
    }
}
