using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Minimal test version of UnifiedContextPlugin to isolate Gemini compatibility issues
/// </summary>
public class MinimalUnifiedContextPlugin
{
    [KernelFunction("test_simple_function")]
    [Description("Simple test function that just returns a message")]
    public string TestSimpleFunction(
        [Description("Test input message")] string message = "test")
    {
        return $"Received: {message}";
    }
}