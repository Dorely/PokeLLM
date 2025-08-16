using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.Game.VectorStore.Models;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.GameState;
using PokeLLM.GameRules.Interfaces;
using Microsoft.Extensions.VectorData;
using System.Text.Json;

namespace PokeLLM.Game.GameLogic;

public interface IInformationManagementService
{
    // Entity methods
    Task<string> UpsertEntityAsync(string entityId, string entityType, string name, string description, string propertiesJson, Guid? id = null);
    Task<EntityVectorRecord> GetEntityAsync(string entityId);
    Task<IEnumerable<EntityVectorRecord>> SearchEntitiesAsync(List<string> queries, string entityType = null);

    // Location methods  
    Task<string> UpsertLocationAsync(string locationId, string name, string description, string region, List<string> tags = null, Guid? id = null);
    Task<LocationVectorRecord> GetLocationAsync(string locationId);

    // Lore methods
    Task<string> UpsertLoreAsync(string entryId, string entryType, string title, string content, List<string> tags = null, Guid? id = null);
    Task<LoreVectorRecord> GetLoreAsync(string entryId);
    Task<IEnumerable<LoreVectorRecord>> SearchLoreAsync(List<string> queries, string entryType = null);

    // Game rule methods
    Task<string> UpsertGameRuleAsync(string entryId, string entryType, string title, string content, List<string> tags = null, Guid? id = null);
    Task<GameRuleVectorRecord> GetGameRuleAsync(string entryId);
    Task<IEnumerable<GameRuleVectorRecord>> SearchGameRulesAsync(List<string> queries, string entryType = null);

    // Narrative log methods
    Task<string> LogNarrativeEventAsync(string eventType, string eventSummary, string fullTranscript, List<string> involvedEntities, string locationId, Guid? id = null, int? turnNumber = null);
    Task<NarrativeLogVectorRecord> GetNarrativeEventAsync(string sessionId, int gameTurnNumber);
    Task<IEnumerable<NarrativeLogVectorRecord>> FindMemoriesAsync(string sessionId, string query, List<string> involvedEntities = null, double minRelevanceScore = 0.75);
}

/// <summary>
/// This service contains methods for querying, inserting, and updating the vector store
/// </summary>
public class InformationManagementService : IInformationManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IRulesetManager _rulesetManager;

    public InformationManagementService(IGameStateRepository gameStateRepository, IVectorStoreService vectorStoreService, IRulesetManager rulesetManager)
    {
        _gameStateRepository = gameStateRepository;
        _vectorStoreService = vectorStoreService;
        _rulesetManager = rulesetManager;
    }

    #region Entity Methods

    public async Task<string> UpsertEntityAsync(string entityId, string entityType, string name, string description, string propertiesJson, Guid? id = null)
    {
        // Validate that description is not empty since it's used for embedding
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Description cannot be empty";
        }

        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingEntity = await _vectorStoreService.GetEntityByIdAsync(entityId);
            if (existingEntity != null)
            {
                // Update existing record - use its Guid
                actualId = existingEntity.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var entity = new EntityVectorRecord
        {
            Id = actualId,
            EntityId = entityId,
            EntityType = entityType,
            Name = name,
            Description = description,
            PropertiesJson = propertiesJson ?? "{}",
            Embedding = description.Trim('\r', '\n', ' ', '\t') // Strip newlines and whitespace from beginning and end
        };

        var vectorId = await _vectorStoreService.AddOrUpdateEntityAsync(entity);
        return $"Entity {entityId} upserted with ID {vectorId}";
    }

    public async Task<EntityVectorRecord> GetEntityAsync(string entityId)
    {
        return await _vectorStoreService.GetEntityByIdAsync(entityId);
    }

    public async Task<IEnumerable<EntityVectorRecord>> SearchEntitiesAsync(List<string> queries, string entityType = null)
    {
        var results = new List<EntityVectorRecord>();
        
        foreach (var query in queries)
        {
            // For entity search, we'll use GetEntityByIdAsync for exact matches since entities use specific IDs
            var entity = await _vectorStoreService.GetEntityByIdAsync(query);
            if (entity != null && (entityType == null || entity.EntityType == entityType))
            {
                results.Add(entity);
            }
        }
        
        return results.Distinct();
    }

    #endregion

    #region Location Methods

    public async Task<string> UpsertLocationAsync(string locationId, string name, string description, string region, List<string> tags = null, Guid? id = null)
    {
        // Validate that description is not empty since it's used for embedding
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Description cannot be empty";
        }

        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingLocation = await _vectorStoreService.GetLocationByIdAsync(locationId);
            if (existingLocation != null)
            {
                // Update existing record - use its Guid
                actualId = existingLocation.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var location = new LocationVectorRecord
        {
            Id = actualId,
            LocationId = locationId,
            Name = name,
            Description = description,
            Region = region,
            Tags = tags.ToArray() ?? Array.Empty<string>(),
            Embedding = description.Trim('\r', '\n', ' ', '\t') // Strip newlines and whitespace from beginning and end
        };

        var vectorId = await _vectorStoreService.AddOrUpdateLocationAsync(location);
        return $"Location {locationId} upserted with ID {vectorId}";
    }

    public async Task<LocationVectorRecord> GetLocationAsync(string locationId)
    {
        return await _vectorStoreService.GetLocationByIdAsync(locationId);
    }

    #endregion

    #region Lore Methods

    public async Task<string> UpsertLoreAsync(string entryId, string entryType, string title, string content, List<string> tags = null, Guid? id = null)
    {
        // Validate that content is not empty since it's used for embedding
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Content cannot be empty";
        }

        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingLore = await _vectorStoreService.GetLoreByIdAsync(entryId);
            if (existingLore != null)
            {
                // Update existing record - use its Guid
                actualId = existingLore.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var lore = new LoreVectorRecord
        {
            Id = actualId,
            EntryId = entryId,
            EntryType = entryType,
            Title = title,
            Content = content,
            Tags = tags.ToArray() ?? Array.Empty<string>(),
            Embedding = content.Trim('\r', '\n', ' ', '\t') // Strip newlines and whitespace from beginning and end
        };

        var vectorId = await _vectorStoreService.AddOrUpdateLoreAsync(lore);
        return $"Lore {entryId} upserted with ID {vectorId}";
    }

    public async Task<LoreVectorRecord> GetLoreAsync(string entryId)
    {
        return await _vectorStoreService.GetLoreByIdAsync(entryId);
    }

    public async Task<IEnumerable<LoreVectorRecord>> SearchLoreAsync(List<string> queries, string entryType = null)
    {
        var results = new List<LoreVectorRecord>();
        
        foreach (var query in queries)
        {
            var searchResults = await _vectorStoreService.SearchLoreAsync(query);
            foreach (var result in searchResults)
            {
                if (entryType == null || result.Record.EntryType == entryType)
                {
                    results.Add(result.Record);
                }
            }
            
            // If no results found in vector database, search the ruleset
            if (!searchResults.Any())
            {
                var rulesetData = await SearchRulesetForLoreAsync(query, entryType);
                results.AddRange(rulesetData);
            }
        }
        
        return results.Distinct();
    }

    #endregion

    #region Game Rule Methods

    public async Task<string> UpsertGameRuleAsync(string entryId, string entryType, string title, string content, List<string> tags = null, Guid? id = null)
    {
        // Validate that content is not empty since it's used for embedding
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Content cannot be empty";
        }

        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the human-readable ID
            var existingRule = await _vectorStoreService.GetGameRuleByIdAsync(entryId);
            if (existingRule != null)
            {
                // Update existing record - use its Guid
                actualId = existingRule.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var rule = new GameRuleVectorRecord
        {
            Id = actualId,
            EntryId = entryId,
            EntryType = entryType,
            Title = title,
            Content = content,
            Tags = tags.ToArray() ?? Array.Empty<string>(),
            Embedding = content.Trim('\r', '\n', ' ', '\t') // Strip newlines and whitespace from beginning and end
        };

        var vectorId = await _vectorStoreService.AddOrUpdateGameRuleAsync(rule);
        return $"Game rule {entryId} upserted with ID {vectorId}";
    }

    public async Task<GameRuleVectorRecord> GetGameRuleAsync(string entryId)
    {
        return await _vectorStoreService.GetGameRuleByIdAsync(entryId);
    }

    public async Task<IEnumerable<GameRuleVectorRecord>> SearchGameRulesAsync(List<string> queries, string entryType = null)
    {
        var results = new List<GameRuleVectorRecord>();

        foreach (var query in queries)
        {
            var searchResults = await _vectorStoreService.SearchGameRulesAsync(query);
            foreach (var result in searchResults)
            {
                if (entryType == null || result.Record.EntryType == entryType)
                {
                    results.Add(result.Record);
                }
            }
            
            // If no results found in vector database, search the ruleset
            if (!searchResults.Any())
            {
                var rulesetData = await SearchRulesetForGameRulesAsync(query, entryType);
                results.AddRange(rulesetData);
            }
        }

        return results.Distinct();
    }

    #endregion

    #region Narrative Log Methods

    public async Task<string> LogNarrativeEventAsync(string eventType, string eventSummary, string fullTranscript, List<string> involvedEntities, string locationId, Guid? id = null, int? turnNumber = null)
    {
        // Validate that eventSummary is not empty since it's used for embedding
        if (string.IsNullOrWhiteSpace(eventSummary))
        {
            return "Event Summary cannot be empty";
        }

        // Load current game state to get sessionId and gameTurnNumber
        var gameState = await _gameStateRepository.LoadLatestStateAsync();
        var sessionId = gameState.SessionId;
        var gameTurnNumber = turnNumber ?? gameState.GameTurnNumber;
        
        Guid actualId;
        
        if (id.HasValue)
        {
            // Use the provided Guid
            actualId = id.Value;
        }
        else
        {
            // Search for existing record using the sessionId and gameTurnNumber
            var existingEvent = await _vectorStoreService.GetNarrativeEventAsync(sessionId, gameTurnNumber);
            if (existingEvent != null)
            {
                // Update existing record - use its Guid
                actualId = existingEvent.Id;
            }
            else
            {
                // Create new record - generate new Guid
                actualId = Guid.NewGuid();
            }
        }

        var narrativeLog = new NarrativeLogVectorRecord
        {
            Id = actualId,
            SessionId = sessionId,
            GameTurnNumber = gameTurnNumber,
            EventType = eventType,
            EventSummary = eventSummary,
            FullTranscript = fullTranscript ?? string.Empty,
            InvolvedEntities = involvedEntities?.ToArray() ?? Array.Empty<string>(),
            LocationId = locationId,
            Embedding = eventSummary.Trim('\r', '\n', ' ', '\t') // Strip newlines and whitespace from beginning and end
        };

        var vectorId = await _vectorStoreService.LogNarrativeEventAsync(narrativeLog);
        return $"Narrative event logged with ID {vectorId}";
    }

    public async Task<NarrativeLogVectorRecord> GetNarrativeEventAsync(string sessionId, int gameTurnNumber)
    {
        return await _vectorStoreService.GetNarrativeEventAsync(sessionId, gameTurnNumber);
    }

    public async Task<IEnumerable<NarrativeLogVectorRecord>> FindMemoriesAsync(string sessionId, string query, List<string> involvedEntities = null, double minRelevanceScore = 0.75)
    {
        var searchResults = await _vectorStoreService.FindMemoriesAsync(sessionId, query, involvedEntities?.ToArray() ?? Array.Empty<string>(), minRelevanceScore);
        return searchResults.Select(r => r.Record);
    }

    #endregion

    #region Private Ruleset Search Methods

    private async Task<IEnumerable<LoreVectorRecord>> SearchRulesetForLoreAsync(string query, string entryType = null)
    {
        var results = new List<LoreVectorRecord>();
        var ruleset = _rulesetManager.GetActiveRuleset();
        
        if (ruleset == null)
            return results;

        try
        {
            // Search for matching Pokemon species
            if (entryType == null || entryType.Equals("Pokemon", StringComparison.OrdinalIgnoreCase))
            {
                if (ruleset.RootElement.TryGetProperty("species", out var species))
                {
                    foreach (var pokemon in species.EnumerateArray())
                    {
                        if (pokemon.TryGetProperty("name", out var nameElement) &&
                            nameElement.GetString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var pokemonData = CreateLoreFromPokemon(pokemon);
                            if (pokemonData != null)
                            {
                                results.Add(pokemonData);
                                // Store in vector database for future searches
                                await _vectorStoreService.AddOrUpdateLoreAsync(pokemonData);
                            }
                        }
                    }
                }
            }

            // Search for matching moves
            if (entryType == null || entryType.Equals("Move", StringComparison.OrdinalIgnoreCase))
            {
                if (ruleset.RootElement.TryGetProperty("moves", out var moves))
                {
                    foreach (var move in moves.EnumerateArray())
                    {
                        if (move.TryGetProperty("name", out var nameElement) &&
                            nameElement.GetString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var moveData = CreateLoreFromMove(move);
                            if (moveData != null)
                            {
                                results.Add(moveData);
                                // Store in vector database for future searches
                                await _vectorStoreService.AddOrUpdateLoreAsync(moveData);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching ruleset for lore: {ex.Message}");
        }

        return results;
    }

    private async Task<IEnumerable<GameRuleVectorRecord>> SearchRulesetForGameRulesAsync(string query, string entryType = null)
    {
        var results = new List<GameRuleVectorRecord>();
        var ruleset = _rulesetManager.GetActiveRuleset();
        
        if (ruleset == null)
            return results;

        try
        {
            // Search for matching trainer classes
            if (entryType == null || entryType.Equals("CharacterClass", StringComparison.OrdinalIgnoreCase))
            {
                if (ruleset.RootElement.TryGetProperty("trainerClasses", out var trainerClasses))
                {
                    foreach (var trainerClass in trainerClasses.EnumerateArray())
                    {
                        if (trainerClass.TryGetProperty("name", out var nameElement) &&
                            nameElement.GetString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var classData = CreateGameRuleFromTrainerClass(trainerClass);
                            if (classData != null)
                            {
                                results.Add(classData);
                                // Store in vector database for future searches
                                await _vectorStoreService.AddOrUpdateGameRuleAsync(classData);
                            }
                        }
                    }
                }
            }

            // Search for type effectiveness data
            if (entryType == null || entryType.Equals("TypeEffectiveness", StringComparison.OrdinalIgnoreCase))
            {
                if (ruleset.RootElement.TryGetProperty("typeEffectiveness", out var typeEffectiveness))
                {
                    var typeData = CreateGameRuleFromTypeEffectiveness(typeEffectiveness, query);
                    if (typeData != null)
                    {
                        results.Add(typeData);
                        // Store in vector database for future searches
                        await _vectorStoreService.AddOrUpdateGameRuleAsync(typeData);
                    }
                }
            }

            // Search for items
            if (entryType == null || entryType.Equals("Item", StringComparison.OrdinalIgnoreCase))
            {
                if (ruleset.RootElement.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var nameElement) &&
                            nameElement.GetString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var itemData = CreateGameRuleFromItem(item);
                            if (itemData != null)
                            {
                                results.Add(itemData);
                                // Store in vector database for future searches
                                await _vectorStoreService.AddOrUpdateGameRuleAsync(itemData);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching ruleset for game rules: {ex.Message}");
        }

        return results;
    }

    private LoreVectorRecord CreateLoreFromPokemon(JsonElement pokemon)
    {
        try
        {
            var id = pokemon.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var name = pokemon.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                return null;

            var content = $"Pokemon: {name}\n";
            
            if (pokemon.TryGetProperty("type1", out var type1))
                content += $"Type: {type1.GetString()}";
            
            if (pokemon.TryGetProperty("type2", out var type2) && !type2.ValueKind.Equals(JsonValueKind.Null))
                content += $"/{type2.GetString()}";
            
            content += "\n";

            if (pokemon.TryGetProperty("baseStats", out var stats))
            {
                content += "Base Stats:\n";
                foreach (var stat in stats.EnumerateObject())
                {
                    content += $"  {stat.Name}: {stat.Value.GetInt32()}\n";
                }
            }

            if (pokemon.TryGetProperty("abilities", out var abilities))
            {
                content += "Abilities: ";
                var abilityList = new List<string>();
                foreach (var ability in abilities.EnumerateArray())
                {
                    abilityList.Add(ability.GetString());
                }
                content += string.Join(", ", abilityList) + "\n";
            }

            return new LoreVectorRecord
            {
                Id = Guid.NewGuid(),
                EntryId = id,
                EntryType = "Pokemon",
                Title = name,
                Content = content,
                Tags = new[] { "pokemon", "species" },
                Embedding = content.Trim()
            };
        }
        catch
        {
            return null;
        }
    }

    private LoreVectorRecord CreateLoreFromMove(JsonElement move)
    {
        try
        {
            var id = move.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var name = move.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                return null;

            var content = $"Move: {name}\n";
            
            if (move.TryGetProperty("type", out var type))
                content += $"Type: {type.GetString()}\n";
            
            if (move.TryGetProperty("category", out var category))
                content += $"Category: {category.GetString()}\n";
            
            if (move.TryGetProperty("power", out var power))
                content += $"Power: {power.GetInt32()}\n";
            
            if (move.TryGetProperty("energyCost", out var energyCost))
                content += $"Energy Cost: {energyCost.GetInt32()}\n";
            
            if (move.TryGetProperty("accuracy", out var accuracy))
                content += $"Accuracy: {accuracy.GetInt32()}%\n";

            if (move.TryGetProperty("effects", out var effects))
            {
                content += "Effects:\n";
                foreach (var effect in effects.EnumerateArray())
                {
                    if (effect.TryGetProperty("type", out var effectType))
                    {
                        content += $"  - {effectType.GetString()}";
                        
                        if (effect.TryGetProperty("status", out var status))
                            content += $" ({status.GetString()})";
                        
                        if (effect.TryGetProperty("chance", out var chance))
                            content += $" {chance.GetInt32()}% chance";
                        
                        content += "\n";
                    }
                }
            }

            return new LoreVectorRecord
            {
                Id = Guid.NewGuid(),
                EntryId = id,
                EntryType = "Move",
                Title = name,
                Content = content,
                Tags = new[] { "move", "attack" },
                Embedding = content.Trim()
            };
        }
        catch
        {
            return null;
        }
    }

    private GameRuleVectorRecord CreateGameRuleFromTrainerClass(JsonElement trainerClass)
    {
        try
        {
            var id = trainerClass.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var name = trainerClass.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                return null;

            var content = $"Trainer Class: {name}\n";
            
            if (trainerClass.TryGetProperty("description", out var description))
                content += $"Description: {description.GetString()}\n";

            if (trainerClass.TryGetProperty("baseStats", out var stats))
            {
                content += "Base Stats:\n";
                foreach (var stat in stats.EnumerateObject())
                {
                    content += $"  {stat.Name}: {stat.Value.GetInt32()}\n";
                }
            }

            if (trainerClass.TryGetProperty("startingAbilities", out var abilities))
            {
                content += "Starting Abilities: ";
                var abilityList = new List<string>();
                foreach (var ability in abilities.EnumerateArray())
                {
                    abilityList.Add(ability.GetString());
                }
                content += string.Join(", ", abilityList) + "\n";
            }

            return new GameRuleVectorRecord
            {
                Id = Guid.NewGuid(),
                EntryId = id,
                EntryType = "CharacterClass",
                Title = name,
                Content = content,
                Tags = new[] { "class", "character", "trainer" },
                Embedding = content.Trim()
            };
        }
        catch
        {
            return null;
        }
    }

    private GameRuleVectorRecord CreateGameRuleFromTypeEffectiveness(JsonElement typeEffectiveness, string query)
    {
        try
        {
            var content = "Type Effectiveness Chart:\n";
            
            foreach (var attackingType in typeEffectiveness.EnumerateObject())
            {
                if (attackingType.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    content += $"{attackingType.Name} type moves:\n";
                    
                    foreach (var defendingType in attackingType.Value.EnumerateObject())
                    {
                        var effectiveness = defendingType.Value.GetDouble();
                        var effectString = effectiveness switch
                        {
                            2.0 => "Super effective",
                            0.5 => "Not very effective",
                            0.0 => "No effect",
                            _ => "Normal damage"
                        };
                        content += $"  vs {defendingType.Name}: {effectiveness}x ({effectString})\n";
                    }
                    content += "\n";
                }
            }

            return new GameRuleVectorRecord
            {
                Id = Guid.NewGuid(),
                EntryId = $"type_effectiveness_{query.ToLower()}",
                EntryType = "TypeEffectiveness",
                Title = $"Type Effectiveness for {query}",
                Content = content,
                Tags = new[] { "type", "effectiveness", "battle" },
                Embedding = content.Trim()
            };
        }
        catch
        {
            return null;
        }
    }

    private GameRuleVectorRecord CreateGameRuleFromItem(JsonElement item)
    {
        try
        {
            var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                return null;

            var content = $"Item: {name}\n";
            
            if (item.TryGetProperty("description", out var description))
                content += $"Description: {description.GetString()}\n";
            
            if (item.TryGetProperty("category", out var category))
                content += $"Category: {category.GetString()}\n";
            
            if (item.TryGetProperty("effects", out var effects))
            {
                content += "Effects:\n";
                foreach (var effect in effects.EnumerateArray())
                {
                    if (effect.TryGetProperty("type", out var effectType))
                    {
                        content += $"  - {effectType.GetString()}";
                        
                        if (effect.TryGetProperty("value", out var value))
                            content += $" ({value.GetInt32()})";
                        
                        content += "\n";
                    }
                }
            }

            return new GameRuleVectorRecord
            {
                Id = Guid.NewGuid(),
                EntryId = id,
                EntryType = "Item",
                Title = name,
                Content = content,
                Tags = new[] { "item", "equipment" },
                Embedding = content.Trim()
            };
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
