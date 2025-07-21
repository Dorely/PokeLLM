using PokeLLM.Game.Data;

namespace PokeLLM.Game.VectorStore.Interfaces;

public interface IVectorStoreService
{
    // Upsert methods - create new or update existing entries
    Task<Guid> UpsertLocationAsync(string name, string description, string type, string environment = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedPointsOfInterest = null);
    Task<Guid> UpsertNPCAsync(string name, string description, string role, string location, string faction = "", string motivations = "", string abilities = "", string challengeLevel = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedQuests = null);
    Task<Guid> UpsertItemAsync(string name, string description, string category, string rarity, string mechanicalEffects = "", string requirements = "", int value = 0, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null);
    Task<Guid> UpsertLoreAsync(string name, string description, string category, string timePeriod, string importance, string region, string[] relatedEvents = null, string[] relatedNpcs = null, string[] relatedLocations = null);
    Task<Guid> UpsertStorylinesAsync(string name, string description, string plothooks = "", string potentialOutcomes = "", int complexityLevel = 3, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStorylines = null);
    Task<Guid> UpsertEventHistoryAsync(string name, string description, string type, string consequences = "", string playerChoices = "", string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null);
    Task<Guid> UpsertDialogueHistoryAsync(string speaker, string content, string topic, string context = "", string[] relatedNpcs = null, string[] relatedLocations = null, string[] relatedStoryLines = null);
    Task<Guid> UpsertPointOfInterestAsync(string name, string description, string challengeType, int difficultyClass, string requiredSkills, string potentialOutcomes, string environmentalFactors, string rewards, string[] relatedLocations = null, string[] relatedNpcs = null, string[] relatedItems = null, string[] relatedStoryLines = null);
    Task<Guid> UpsertRulesMechanicsAsync(string name, string description, string category, string ruleSet, string usage, string examples, string[] relatedRules = null);

    // Search methods - retrieve relevant entries by semantic similarity
    Task<IEnumerable<VectorSearchResult>> SearchLocationsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchNPCsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchItemsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchLoreAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchStorylinesAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchEventsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchDialogueAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchPointsOfInterestAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchRulesMechanicsAsync(string query, int limit = 10);
    Task<IEnumerable<VectorSearchResult>> SearchAllAsync(string query, int limit = 10);
}
