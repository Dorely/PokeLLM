namespace PokeLLM.Game.RAG;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;

public class RagService
{
    private readonly ISemanticTextMemory _memory;
    private readonly Kernel _kernel;
    private const string QdrantCollectionName = "sk-rag-demo";

    public RagService(string openAIModel, string openAIApiKey, string? openAIOrgId = null)
    {
        // Set up the Qdrant memory store.
        // We are using a local Qdrant instance running on default ports.
        // The vector size of 1536 is specific to the text-embedding-ada-002 model.
        var memoryStore = new QdrantMemoryStore("http://localhost:6334", 1536);

        // Create an embedding generator using the OpenAI model.
        var embeddingGenerator = new OpenAITextEmbeddingGenerationService(openAIModel, openAIApiKey, openAIOrgId);

        // Build the Semantic Text Memory, combining the store and the embedding generator.
        _memory = new SemanticTextMemory(memoryStore, embeddingGenerator);

        // Build the Kernel for the "Generation" part of RAG
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(openAIModel, openAIApiKey, openAIOrgId);
        _kernel = builder.Build();
    }

    /// <summary>
    /// Stores text data in the Qdrant vector database.
    /// It splits the text into smaller chunks for more effective retrieval.
    /// </summary>
    public async Task StoreDataAsync(string text)
    {
        Console.WriteLine($"Storing data in Qdrant collection '{QdrantCollectionName}'...");

        // Use a text chunker to split the text into smaller, more manageable paragraphs.
        // This improves the relevance of search results.
        var paragraphs = TextChunker.SplitPlainTextParagraphs(TextChunker.SplitPlainTextLines(text, 128), 512);

        for (int i = 0; i < paragraphs.Count; i++)
        {
            // Save each paragraph to Qdrant. The SemanticTextMemory class handles
            // the embedding generation and storage automatically.
            await _memory.SaveInformationAsync(
                collection: QdrantCollectionName,
                text: paragraphs[i],
                id: $"doc_chunk_{i}" // A unique ID for each chunk
            );
            Console.WriteLine($"  - Stored chunk {i + 1}/{paragraphs.Count}");
        }

        Console.WriteLine("Data storage complete.\n");
    }

    /// <summary>
    /// Implements the RAG pattern:
    /// 1. Retrieves relevant context from Qdrant.
    /// 2. Augments a prompt with that context.
    /// 3. Generates a response using the LLM.
    /// </summary>
    public async Task<string> RetrieveAndGenerateAsync(string query)
    {
        Console.WriteLine("--- Starting RAG Process ---");
        Console.WriteLine($"User Query: {query}\n");

        // 1. Retrieve relevant documents from Qdrant (the "Retrieval" part)
        // SearchAsync generates an embedding for the query and finds similar documents.
        var searchResults = _memory.SearchAsync(
            collection: QdrantCollectionName,
            query: query,
            limit: 3, // Retrieve the top 3 most relevant chunks
            minRelevanceScore: 0.75 // Filter out chunks with low relevance
        );

        string context = "";
        Console.WriteLine("Retrieved Context from Qdrant:");
        await foreach (var result in searchResults)
        {
            context += result.Text + "\n";
            Console.WriteLine("  - " + result.Text.Trim());
        }

        if (string.IsNullOrWhiteSpace(context))
        {
            return "I'm sorry, I couldn't find any relevant information to answer your question.";
        }
        Console.WriteLine("---\n");

        // 2. Augment the prompt with the retrieved context (the "Augmented" part)
        var prompt = """
                    Answer the user's question based only on the following context.
                    If the context does not contain the answer, state that you cannot answer.

                    CONTEXT:
                    {{$context}}

                    USER'S QUESTION:
                    {{$query}}

                    YOUR ANSWER:
                    """;

        var arguments = new KernelArguments
        {
            { "context", context },
            { "query", query }
        };

        // 3. Generate the final answer (the "Generation" part)
        var result = await _kernel.InvokePromptAsync(prompt, arguments);

        Console.WriteLine("LLM-Generated Answer:");
        Console.WriteLine(result.GetValue<string>());
        Console.WriteLine("--------------------------\n");

        return result.GetValue<string>() ?? "No response generated.";
    }
}
