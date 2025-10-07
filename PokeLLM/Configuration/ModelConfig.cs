namespace PokeLLM.Game.Configuration;

public class ModelConfig
{
    public string ApiKey { get; set; }
    public string ModelId { get; set; }
    public string EmbeddingModelId { get; set; }
    public int EmbeddingDimensions { get; set; }
    // New: Request timeout in seconds for LLM/embedding HTTP calls
    public int? RequestTimeoutSeconds { get; set; }
}

public class HybridConfig
{
    public LLMConfig LLM { get; set; } = new();
    public EmbeddingConfig Embedding { get; set; } = new();
}

public class LLMConfig
{
    public string Provider { get; set; } = "OpenAI"; // "OpenAI", "Ollama", or "Gemini"
    public string ApiKey { get; set; }
    public string ModelId { get; set; }
    public string Endpoint { get; set; } // For Ollama
    // New: Request timeout in seconds for chat calls
    public int? RequestTimeoutSeconds { get; set; }
}

public class EmbeddingConfig
{
    public string Provider { get; set; } = "Ollama"; // "OpenAI" or "Ollama" 
    public string ApiKey { get; set; }
    public string ModelId { get; set; }
    public string Endpoint { get; set; } // For Ollama
    public int Dimensions { get; set; }
    // New: Request timeout in seconds for embedding calls
    public int? RequestTimeoutSeconds { get; set; }
}

/// <summary>
/// Configuration for the new flexible LLM and embedding provider system
/// </summary>
public class FlexibleProviderConfig
{
    public LLMConfig LLM { get; set; } = new();
    public EmbeddingConfig Embedding { get; set; } = new();
}
