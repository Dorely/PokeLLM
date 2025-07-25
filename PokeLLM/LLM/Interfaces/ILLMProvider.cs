using Microsoft.Extensions.AI;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.LLM.Interfaces;


/// <summary>
/// Interface for low-level LLM providers that can be plugged into the orchestration service
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Creates a kernel configured for this LLM provider
    /// </summary>
    Task<Microsoft.SemanticKernel.Kernel> CreateKernelAsync();

    /// <summary>
    /// Gets the embedding generator for this provider
    /// </summary>
    IEmbeddingGenerator GetEmbeddingGenerator();

    /// <summary>
    /// Gets provider-specific execution settings
    /// </summary>
    object GetExecutionSettings(int maxTokens, float temperature, bool enableFunctionCalling = false);
}