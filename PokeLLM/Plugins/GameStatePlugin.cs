using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

public class GameStatePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameStatePlugin(IGameStateRepository repository)
    {
        _repository = repository;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("create_new_game")]
    [Description("Create a new game state with a fresh character and adventure")]
    public async Task<string> CreateNewGame(
        [Description("Name for the player character")] string characterName = "Trainer")
    {
        Debug.WriteLine($"[GameStatePlugin] CreateNewGame called with characterName: '{characterName}'");
        var gameState = await _repository.CreateNewGameStateAsync(characterName);
        return JsonSerializer.Serialize(gameState, _jsonOptions);
    }

    [KernelFunction("load_game_state")]
    [Description("Load the current game state")]
    public async Task<string> LoadGameState()
    {
        Debug.WriteLine($"[GameStatePlugin] LoadGameState called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found. Create a new game first.";
        
        return JsonSerializer.Serialize(gameState, _jsonOptions);
    }

    [KernelFunction("save_game_state")]
    [Description("Save the current game state with updated data")]
    public async Task<string> SaveGameState(
        [Description("JSON string containing the complete game state to save")] string gameStateJson)
    {
        Debug.WriteLine($"[GameStatePlugin] SaveGameState called");
        try
        {
            var gameState = JsonSerializer.Deserialize<GameStateModel>(gameStateJson, _jsonOptions);
            if (gameState == null)
                return "Invalid game state data provided.";

            await _repository.SaveStateAsync(gameState);
            return "Game state saved successfully.";
        }
        catch (JsonException ex)
        {
            return $"Error saving game state: {ex.Message}";
        }
    }

    [KernelFunction("update_character_health")]
    [Description("Update character's current health")]
    public async Task<string> UpdateCharacterHealth(
        [Description("New current health value")] int currentHealth)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateCharacterHealth called with currentHealth: {currentHealth}");
        await _repository.UpdateCharacterAsync(character =>
        {
            character.CurrentHealth = Math.Max(0, Math.Min(currentHealth, character.MaxHealth));
        });
        return $"Character health updated to {currentHealth}.";
    }

    [KernelFunction("update_character_experience")]
    [Description("Add experience to the character and handle level ups")]
    public async Task<string> UpdateCharacterExperience(
        [Description("Amount of experience to add")] int experienceGain)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateCharacterExperience called with experienceGain: {experienceGain}");
        var leveledUp = false;
        var newLevel = 0;

        await _repository.UpdateCharacterAsync(character =>
        {
            character.Experience += experienceGain;
            
            // Handle level ups (simple calculation)
            while (character.Experience >= character.ExperienceToNextLevel)
            {
                character.Experience -= character.ExperienceToNextLevel;
                character.Level++;
                character.ExperienceToNextLevel = (int)(character.ExperienceToNextLevel * 1.2); // 20% increase each level
                character.MaxHealth += 10; // Gain 10 HP per level
                character.CurrentHealth = character.MaxHealth; // Full heal on level up
                leveledUp = true;
                newLevel = character.Level;
            }
        });

        if (leveledUp)
            return $"Gained {experienceGain} experience! Leveled up to level {newLevel}!";
        else
            return $"Gained {experienceGain} experience.";
    }

    [KernelFunction("add_pokemon_to_team")]
    [Description("Add a new Pokemon to the character's team")]
    public async Task<string> AddPokemonToTeam(
        [Description("JSON string with Pokemon data: {name, species, level, primaryType, secondaryType?, currentHealth, maxHealth}")] string pokemonJson)
    {
        Debug.WriteLine($"[GameStatePlugin] AddPokemonToTeam called with pokemonJson: '{pokemonJson}'");
        try
        {
            var pokemon = JsonSerializer.Deserialize<Pokemon>(pokemonJson, _jsonOptions);
            if (pokemon == null)
                return "Invalid Pokemon data provided.";

            await _repository.AddPokemonToTeamAsync(pokemon);
            return $"Added {pokemon.Name} ({pokemon.Species}) to the team!";
        }
        catch (JsonException ex)
        {
            return $"Error adding Pokemon: {ex.Message}";
        }
    }

    [KernelFunction("update_pokemon_health")]
    [Description("Update a Pokemon's health by ID")]
    public async Task<string> UpdatePokemonHealth(
        [Description("ID of the Pokemon to update")] string pokemonId,
        [Description("New current health value")] int currentHealth)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdatePokemonHealth called with pokemonId: '{pokemonId}', currentHealth: {currentHealth}");
        var found = false;
        var pokemonName = "";

        await _repository.UpdateCharacterAsync(character =>
        {
            var pokemon = character.PokemonTeam.FirstOrDefault(p => p.Id == pokemonId) ??
                         character.StoredPokemon.FirstOrDefault(p => p.Id == pokemonId);
            
            if (pokemon != null)
            {
                pokemon.CurrentHealth = Math.Max(0, Math.Min(currentHealth, pokemon.MaxHealth));
                if (pokemon.CurrentHealth == 0)
                    pokemon.Status = PokemonStatus.Fainted;
                else if (pokemon.Status == PokemonStatus.Fainted)
                    pokemon.Status = PokemonStatus.Healthy;
                
                found = true;
                pokemonName = pokemon.Name;
            }
        });

        return found ? $"Updated {pokemonName}'s health to {currentHealth}." : "Pokemon not found.";
    }

    [KernelFunction("change_location")]
    [Description("Move the character to a new location")]
    public async Task<string> ChangeLocation(
        [Description("Name of the new location")] string newLocation,
        [Description("Region where the location is (optional)")] string region = "")
    {
        Debug.WriteLine($"[GameStatePlugin] ChangeLocation called with newLocation: '{newLocation}', region: '{region}'");
        await _repository.UpdateAdventureAsync(adventure =>
        {
            adventure.CurrentLocation = newLocation;
            if (!string.IsNullOrEmpty(region))
                adventure.CurrentRegion = region;
            
            adventure.VisitedLocations[newLocation] = true;
            adventure.LocationVisitTimes[newLocation] = DateTime.UtcNow;
        });

        return $"Moved to {newLocation}" + (string.IsNullOrEmpty(region) ? "" : $" in {region}") + ".";
    }

    [KernelFunction("update_npc_relationship")]
    [Description("Update relationship with an NPC")]
    public async Task<string> UpdateNPCRelationship(
        [Description("ID or name of the NPC")] string npcId,
        [Description("Name of the NPC for display")] string npcName,
        [Description("Change in relationship level (-100 to 100)")] int relationshipChange,
        [Description("Type of relationship (Enemy, Hostile, Neutral, Friendly, Ally)")] string relationshipType = "Neutral")
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateNPCRelationship called with npcId: '{npcId}', relationshipChange: {relationshipChange}");
        await _repository.UpdateAdventureAsync(adventure =>
        {
            if (!adventure.NPCRelationships.ContainsKey(npcId))
            {
                adventure.NPCRelationships[npcId] = new NPCRelationship
                {
                    NPCId = npcId,
                    NPCName = npcName,
                    FirstMet = DateTime.UtcNow,
                    RelationshipType = relationshipType
                };
            }

            var relationship = adventure.NPCRelationships[npcId];
            relationship.RelationshipLevel = Math.Max(-100, Math.Min(100, relationship.RelationshipLevel + relationshipChange));
            relationship.LastInteraction = DateTime.UtcNow;
            relationship.TimesEncountered++;
            
            // Auto-update relationship type based on level
            if (relationship.RelationshipLevel >= 75)
                relationship.RelationshipType = "Ally";
            else if (relationship.RelationshipLevel >= 25)
                relationship.RelationshipType = "Friendly";
            else if (relationship.RelationshipLevel >= -25)
                relationship.RelationshipType = "Neutral";
            else if (relationship.RelationshipLevel >= -75)
                relationship.RelationshipType = "Hostile";
            else
                relationship.RelationshipType = "Enemy";
        });

        return $"Relationship with {npcName} updated by {relationshipChange:+#;-#;0}. New type: {relationshipType}";
    }

    [KernelFunction("add_to_inventory")]
    [Description("Add items to the character's inventory")]
    public async Task<string> AddToInventory(
        [Description("Name of the item")] string itemName,
        [Description("Quantity to add")] int quantity = 1,
        [Description("Type of item (Items, KeyItems, Pokeballs, Medicine, TMsHMs, Berries)")] string itemType = "Items")
    {
        Debug.WriteLine($"[GameStatePlugin] AddToInventory called with itemName: '{itemName}', quantity: {quantity}, itemType: '{itemType}'");
        await _repository.UpdateCharacterAsync(character =>
        {
            var inventory = character.Inventory;
            Dictionary<string, int> targetCollection = itemType.ToLower() switch
            {
                "keyitems" => inventory.KeyItems,
                "pokeballs" => inventory.Pokeballs,
                "medicine" => inventory.Medicine,
                "tmshms" => inventory.TMsHMs,
                "berries" => inventory.Berries,
                _ => inventory.Items
            };

            targetCollection[itemName] = targetCollection.GetValueOrDefault(itemName, 0) + quantity;
        });

        return $"Added {quantity} {itemName}(s) to inventory.";
    }

    [KernelFunction("add_game_event")]
    [Description("Add an event to the game history")]
    public async Task<string> AddGameEvent(
        [Description("Type of event")] string eventType,
        [Description("Description of what happened")] string description,
        [Description("Location where the event occurred")] string location = "")
    {
        Debug.WriteLine($"[GameStatePlugin] AddGameEvent called with eventType: '{eventType}', description: '{description}'");
        var gameEvent = new GameEvent
        {
            EventType = eventType,
            Description = description,
            Location = location
        };

        await _repository.AddEventToHistoryAsync(gameEvent);
        return "Event added to history.";
    }

    [KernelFunction("get_character_summary")]
    [Description("Get a summary of the character's current state")]
    public async Task<string> GetCharacterSummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetCharacterSummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No character found. Create a new game first.";

        var character = gameState.Character;
        var summary = new
        {
            name = character.Name,
            level = character.Level,
            health = $"{character.CurrentHealth}/{character.MaxHealth}",
            experience = character.Experience,
            experienceToNext = character.ExperienceToNextLevel,
            pokemonTeam = character.PokemonTeam.Select(p => new { 
                name = p.Name, 
                species = p.Species, 
                level = p.Level,
                health = $"{p.CurrentHealth}/{p.MaxHealth}"
            }),
            money = character.Inventory.Money,
            location = gameState.Adventure.CurrentLocation,
            region = gameState.Adventure.CurrentRegion
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("has_game_state")]
    [Description("Check if a game state exists")]
    public async Task<string> HasGameState()
    {
        Debug.WriteLine($"[GameStatePlugin] HasGameState called");
        var hasState = await _repository.HasGameStateAsync();
        return hasState ? "true" : "false";
    }
}