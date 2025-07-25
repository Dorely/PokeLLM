using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace PokeLLM.Game.Plugins;

public class VectorStorePlugin
{
    private readonly IVectorStoreService _vectorStoreService;

    public VectorStorePlugin(IVectorStoreService vectorStoreService)
    {
        _vectorStoreService = vectorStoreService;
    }

    //TODO implement functions here to expose vector store operations to the LLM chat
}