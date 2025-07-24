using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Core GameEnginePlugin that provides essential game state management functions.
/// Handles Pokemon management, character state, world state, and game progression.
/// </summary>
public class GameEnginePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameEnginePlugin(IGameStateRepository repository)
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

    // --- Game Initialization ---


    // --- Pokemon Management ---

    [KernelFunction("create_pokemon")]
    [Description("Create a new Pokemon and add it to the WorldPokemon Collection")]
    public async Task<string> CreatePokemon(Pokemon pokemon)
    {
        try
        {
            if (pokemon == null)
                return JsonSerializer.Serialize(new { error = "Invalid Pokemon object" }, _jsonOptions);

            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (string.IsNullOrEmpty(pokemon.Id))
                pokemon.Id = Guid.NewGuid().ToString();

            // Set default values if not provided
            if (pokemon.CurrentVigor == 0 && pokemon.MaxVigor == 0)
            {
                pokemon.CurrentVigor = 10 + (pokemon.Level - 1) * 2;
                pokemon.MaxVigor = pokemon.CurrentVigor;
            }

            if (string.IsNullOrEmpty(pokemon.NickName))
                pokemon.NickName = pokemon.Species;

            gameState.WorldPokemon.Add(pokemon);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { success = true, message = $"Created wild Pokemon: {pokemon.NickName} ({pokemon.Species})" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to create Pokemon: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("add_pokemon_to_team")]
    [Description("Move a Pokemon from world or collection to team using Pokemon ID")]
    public async Task<string> AddPokemonToTeam(string pokemonId)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (gameState.Player.TeamPokemon.Count >= 6)
                return JsonSerializer.Serialize(new { error = "Team is full (6 Pokemon maximum)" }, _jsonOptions);

            Pokemon pokemon = null;
            string sourceLocation = "";

            // Try to find in world Pokemon first
            var worldPokemon = gameState.WorldPokemon.FirstOrDefault(p => p.Id == pokemonId);
            if (worldPokemon != null)
            {
                pokemon = worldPokemon;
                gameState.WorldPokemon.Remove(worldPokemon);
                sourceLocation = "world";
            }
            else
            {
                // Try to find in boxed Pokemon
                var ownedPokemon = gameState.Player.BoxedPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
                if (ownedPokemon != null)
                {
                    pokemon = ownedPokemon.Pokemon;
                    gameState.Player.BoxedPokemon.Remove(ownedPokemon);
                    sourceLocation = "collection";
                }
            }

            if (pokemon == null)
                return JsonSerializer.Serialize(new { error = "Pokemon not found in world or collection" }, _jsonOptions);

            var teamPokemon = new OwnedPokemon
            {
                Pokemon = pokemon,
                CaughtLocation = sourceLocation == "world" ? gameState.CurrentLocation : 
                    gameState.Player.BoxedPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId)?.CaughtLocation ?? gameState.CurrentLocation
            };

            gameState.Player.TeamPokemon.Add(teamPokemon);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Moved {pokemon.NickName} ({pokemon.Species}) from {sourceLocation} to team" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to add Pokemon to team: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("add_pokemon_to_collection")]
    [Description("Move a Pokemon from world or team to collection using Pokemon ID")]
    public async Task<string> AddPokemonToCollection(string pokemonId)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            Pokemon pokemon = null;
            string sourceLocation = "";
            string caughtLocation = gameState.CurrentLocation;

            // Try to find in world Pokemon first
            var worldPokemon = gameState.WorldPokemon.FirstOrDefault(p => p.Id == pokemonId);
            if (worldPokemon != null)
            {
                pokemon = worldPokemon;
                gameState.WorldPokemon.Remove(worldPokemon);
                sourceLocation = "world";
            }
            else
            {
                // Try to find in team
                var teamPokemon = gameState.Player.TeamPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
                if (teamPokemon != null)
                {
                    pokemon = teamPokemon.Pokemon;
                    caughtLocation = teamPokemon.CaughtLocation;
                    gameState.Player.TeamPokemon.Remove(teamPokemon);
                    sourceLocation = "team";
                }
            }

            if (pokemon == null)
                return JsonSerializer.Serialize(new { error = "Pokemon not found in world or team" }, _jsonOptions);

            var ownedPokemon = new OwnedPokemon
            {
                Pokemon = pokemon,
                CaughtLocation = caughtLocation
            };

            gameState.Player.BoxedPokemon.Add(ownedPokemon);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Moved {pokemon.NickName} ({pokemon.Species}) from {sourceLocation} to collection" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to add Pokemon to collection: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("release_pokemon")]
    [Description("Release a Pokemon from team or collection back to the wild")]
    public async Task<string> ReleasePokemon(string pokemonId)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            Pokemon pokemon = null;
            string sourceLocation = "";

            // Try to find in team first
            var teamPokemon = gameState.Player.TeamPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
            if (teamPokemon != null)
            {
                pokemon = teamPokemon.Pokemon;
                gameState.Player.TeamPokemon.Remove(teamPokemon);
                sourceLocation = "team";
            }
            else
            {
                // Try to find in boxed Pokemon
                var ownedPokemon = gameState.Player.BoxedPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
                if (ownedPokemon != null)
                {
                    pokemon = ownedPokemon.Pokemon;
                    gameState.Player.BoxedPokemon.Remove(ownedPokemon);
                    sourceLocation = "collection";
                }
            }

            if (pokemon == null)
                return JsonSerializer.Serialize(new { error = "Pokemon not found in team or collection" }, _jsonOptions);

            // Reset Pokemon to wild state (heal and clear status effects)
            pokemon.CurrentVigor = pokemon.MaxVigor;
            pokemon.StatusEffects.Clear();

            gameState.WorldPokemon.Add(pokemon);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Released {pokemon.NickName} ({pokemon.Species}) from {sourceLocation} back to the wild" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to release Pokemon: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("heal_all_pokemon")]
    public async Task<string> HealAllPokemon()
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            var healedCount = 0;
            foreach (var ownedPokemon in gameState.Player.TeamPokemon)
            {
                var pokemon = ownedPokemon.Pokemon;
                if (pokemon.CurrentVigor < pokemon.MaxVigor)
                {
                    pokemon.CurrentVigor = pokemon.MaxVigor;
                    pokemon.StatusEffects.Clear();
                    healedCount++;
                }
            }

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new { success = true, message = $"Healed {healedCount} Pokemon to full health" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to heal Pokemon: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("update_pokemon_vigor")]
    [Description("Vigor represents both health and energy. Using moves and taking damage both expend it.")]
    public async Task<string> UpdatePokemonVigor(string pokemonId, int amount)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            var ownedPokemon = gameState.Player.TeamPokemon
                .FirstOrDefault(p => p.Pokemon.Id.Equals(pokemonId, StringComparison.OrdinalIgnoreCase));

            if (ownedPokemon == null)
                return JsonSerializer.Serialize(new { error = "Pokemon not found in team" }, _jsonOptions);

            var pokemon = ownedPokemon.Pokemon;
            pokemon.CurrentVigor = Math.Max(0, Math.Min(pokemon.MaxVigor, pokemon.CurrentVigor + amount));
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"{pokemon.NickName} vigor updated to {pokemon.CurrentVigor}/{pokemon.MaxVigor}" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to update Pokemon vigor: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("get_player_pokemon_team")]
    public async Task<string> GetPlayerPokemonTeam()
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var team = gameState.Player.TeamPokemon.Select(owned => new {
            owned.Pokemon.Id,
            owned.Pokemon.NickName,
            owned.Pokemon.Species,
            owned.Pokemon.Level,
            Vigor = $"{owned.Pokemon.CurrentVigor}/{owned.Pokemon.MaxVigor}",
            owned.Pokemon.Stats,
            owned.Pokemon.Type1,
            owned.Pokemon.Type2,
            owned.Pokemon.KnownMoves,
            owned.Pokemon.Abilities,
            owned.Pokemon.StatusEffects,
            owned.CaughtLocation,
            owned.Friendship,
            owned.Experience,
            owned.AvailableStatPoints
        }).ToList();

        return JsonSerializer.Serialize(team, _jsonOptions);
    }

    [KernelFunction("get_player_pokemon_collection")]
    public async Task<string> GetPlayerPokemonCollection()
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var collection = gameState.Player.BoxedPokemon.Select(owned => new {
            owned.Pokemon.Id,
            owned.Pokemon.NickName,
            owned.Pokemon.Species,
            owned.Pokemon.Level,
            Vigor = $"{owned.Pokemon.CurrentVigor}/{owned.Pokemon.MaxVigor}",
            owned.Pokemon.Stats,
            owned.CaughtLocation,
            owned.Friendship
        }).ToList();

        return JsonSerializer.Serialize(collection, _jsonOptions);
    }

    [KernelFunction("get_world_pokemon")]
    public async Task<string> GetWorldPokemon()
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var worldPokemon = gameState.WorldPokemon.Select(p => new {
            p.Id,
            p.NickName,
            p.Species,
            p.Level,
            Vigor = $"{p.CurrentVigor}/{p.MaxVigor}",
            p.Stats
        }).ToList();

        return JsonSerializer.Serialize(worldPokemon, _jsonOptions);
    }

    [KernelFunction("get_npc_pokemon_team")]
    public async Task<string> GetNpcPokemonTeam(string npcId)
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Find the NPC from WorldNpcs collection
        var npc = gameState.WorldNpcs?.FirstOrDefault(n => n.Id.Equals(npcId, StringComparison.OrdinalIgnoreCase));
        if (npc == null)
            return JsonSerializer.Serialize(new { error = "NPC not found" }, _jsonOptions);

        // Get the Pokemon that belong to this NPC
        var npcPokemonTeam = gameState.WorldPokemon
            .Where(p => npc.PokemonOwned.Contains(p.Id))
            .Select(p => new {
                p.Id,
                p.NickName,
                p.Species,
                p.Level,
                Vigor = $"{p.CurrentVigor}/{p.MaxVigor}",
                p.Stats,
                p.Type1,
                p.Type2,
                p.KnownMoves,
                p.Abilities,
                p.StatusEffects
            }).ToList();

        var result = new
        {
            npcId = npc.Id,
            npcName = npc.Name,
            isTrainer = npc.IsTrainer,
            teamSize = npcPokemonTeam.Count,
            pokemonTeam = npcPokemonTeam
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // --- World State Management ---

    [KernelFunction("change_location")]
    public async Task<string> ChangeActiveLocation(string location)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            gameState.CurrentLocation = location;
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { success = true, message = $"Location changed to {location}" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to change location: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("set_time_and_weather")]
    public async Task<string> SetTimeAndWeather(string timeOfDay, string weather)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (Enum.TryParse<TimeOfDay>(timeOfDay, true, out var timeEnum))
                gameState.TimeOfDay = timeEnum;
            
            if (Enum.TryParse<Weather>(weather, true, out var weatherEnum))
                gameState.Weather = weatherEnum;

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Time set to {gameState.TimeOfDay}, Weather set to {gameState.Weather}" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to set time and weather: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("update_adventure_summary")]
    public async Task<string> UpdateAdventureSummary(string summary)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            gameState.AdventureSummary = summary;
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { success = true, message = "Adventure summary updated" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to update adventure summary: {ex.Message}" }, _jsonOptions);
        }
    }

    // --- Character Management ---
    [KernelFunction("award_player_experience")]
    public async Task<string> AwardPlayerExperience(int amount, string reason = "")
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            var oldLevel = CalculateLevelFromExperience(gameState.Player.Experience);
            gameState.Player.Experience += amount;
            var newLevel = CalculateLevelFromExperience(gameState.Player.Experience);

            var leveledUp = newLevel > oldLevel;
            if (leveledUp)
            {
                gameState.Player.Character.Level = newLevel;
                gameState.Player.AvailableStatPoints += (newLevel - oldLevel);
            }

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            var message = $"Awarded {amount} experience";
            if (!string.IsNullOrEmpty(reason)) message += $" for {reason}";
            if (leveledUp) message += $". Level up! Now level {newLevel}";

            return JsonSerializer.Serialize(new { 
                success = true, 
                message, 
                leveledUp,
                newLevel = leveledUp ? newLevel : oldLevel
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to award experience: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("update_money")]
    public async Task<string> UpdateMoney(int amount)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            gameState.Player.Character.Money = Math.Max(0, gameState.Player.Character.Money + amount);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Money updated by {amount:+#;-#;0}. Current: {gameState.Player.Character.Money}" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to update money: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("add_to_inventory")]
    public async Task<string> AddToInventory(string item, int quantity = 1)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (gameState.Player.Character.Inventory.ContainsKey(item))
                gameState.Player.Character.Inventory[item] += quantity;
            else
                gameState.Player.Character.Inventory[item] = quantity;

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Added {quantity}x {item} to inventory" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to add to inventory: {ex.Message}" }, _jsonOptions);
        }
    }

    // --- NPC Management ---

    [KernelFunction("create_npc")]
    [Description("Create a new NPC using JSON. Example: { \"name\": \"Professor Oak\", \"level\": 10, \"isTrainer\": true, \"stats\": { \"power\": \"Expert\", \"mind\": \"Master\", \"charm\": \"Experienced\" }, \"inventory\": { \"Pokeball\": 5, \"Potion\": 3 }, \"money\": 1000, \"factions\": [\"Pokemon League\"] }")]
    public async Task<string> CreateNpc(string npcJson)
    {
        try
        {
            var npc = JsonSerializer.Deserialize<Character>(npcJson, _jsonOptions);
            if (npc == null)
                return JsonSerializer.Serialize(new { error = "Invalid NPC JSON" }, _jsonOptions);

            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (string.IsNullOrEmpty(npc.Id))
                npc.Id = Guid.NewGuid().ToString();

            gameState.WorldNpcs.Add(npc);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { success = true, message = $"Created NPC: {npc.Name}" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to create NPC: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("assign_pokemon_to_npc")]
    [Description("Assign a Pokemon from the WorldPokemon collection to an NPC's team")]
    public async Task<string> AssignPokemonToNpc(string npcId, string pokemonId)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Find the NPC
            var npc = gameState.WorldNpcs?.FirstOrDefault(n => n.Id.Equals(npcId, StringComparison.OrdinalIgnoreCase));
            if (npc == null)
                return JsonSerializer.Serialize(new { error = "NPC not found" }, _jsonOptions);

            // Find the Pokemon
            var pokemon = gameState.WorldPokemon?.FirstOrDefault(p => p.Id.Equals(pokemonId, StringComparison.OrdinalIgnoreCase));
            if (pokemon == null)
                return JsonSerializer.Serialize(new { error = "Pokemon not found in world collection" }, _jsonOptions);

            // Check if Pokemon is already owned by this NPC
            if (npc.PokemonOwned.Contains(pokemonId))
                return JsonSerializer.Serialize(new { error = "Pokemon is already owned by this NPC" }, _jsonOptions);

            // Check if Pokemon is already owned by another NPC
            var existingOwner = gameState.WorldNpcs?.FirstOrDefault(n => n.PokemonOwned.Contains(pokemonId));
            if (existingOwner != null && existingOwner.Id != npcId)
                return JsonSerializer.Serialize(new { error = $"Pokemon is already owned by {existingOwner.Name}" }, _jsonOptions);

            // Assign the Pokemon to the NPC
            npc.PokemonOwned.Add(pokemonId);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Assigned {pokemon.NickName} ({pokemon.Species}) to {npc.Name}" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to assign Pokemon to NPC: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("remove_pokemon_from_npc")]
    [Description("Remove a Pokemon from an NPC's team (Pokemon remains in WorldPokemon collection)")]
    public async Task<string> RemovePokemonFromNpc(string npcId, string pokemonId)
    {
        try
        {
            var gameState = await _repository.LoadLatestStateAsync();
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Find the NPC
            var npc = gameState.WorldNpcs?.FirstOrDefault(n => n.Id.Equals(npcId, StringComparison.OrdinalIgnoreCase));
            if (npc == null)
                return JsonSerializer.Serialize(new { error = "NPC not found" }, _jsonOptions);

            // Check if NPC owns this Pokemon
            if (!npc.PokemonOwned.Contains(pokemonId))
                return JsonSerializer.Serialize(new { error = "NPC does not own this Pokemon" }, _jsonOptions);

            // Find the Pokemon for the response message
            var pokemon = gameState.WorldPokemon?.FirstOrDefault(p => p.Id.Equals(pokemonId, StringComparison.OrdinalIgnoreCase));

            // Remove the Pokemon from the NPC's ownership
            npc.PokemonOwned.Remove(pokemonId);
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _repository.SaveStateAsync(gameState);
            
            var pokemonName = pokemon != null ? $"{pokemon.NickName} ({pokemon.Species})" : "Pokemon";
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Removed {pokemonName} from {npc.Name}'s team" 
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to remove Pokemon from NPC: {ex.Message}" }, _jsonOptions);
        }
    }

    // --- Readonly Functions ---

    [KernelFunction("get_adventure_summary")]
    [Description("Get the current adventure summary")]
    public async Task<string> GetAdventureSummary()
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        return JsonSerializer.Serialize(new { 
            adventureSummary = gameState.AdventureSummary,
            lastUpdated = gameState.LastSaveTime 
        }, _jsonOptions);
    }

    [KernelFunction("get_player_state")]
    public async Task<string> GetPlayerState()
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        return JsonSerializer.Serialize(gameState.Player, _jsonOptions);
    }

    [KernelFunction("get_world_state")]
    public async Task<string> GetWorldState()
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);
        var result = new
        {
            location = gameState.CurrentLocation,
            region = gameState.Region,
            timeOfDay = gameState.TimeOfDay,
            weather = gameState.Weather
        };
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_world_npcs")]
    public async Task<string> GetWorldNpcs()
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);
        var npcs = gameState.WorldNpcs != null
            ? gameState.WorldNpcs.Select(npc => new { npc.Id, npc.Name }).ToList<object>()
            : new List<object>();
        return JsonSerializer.Serialize(npcs, _jsonOptions);
    }

    [KernelFunction("get_npc_details")]
    public async Task<string> GetNpcDetails(string id)
    {
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);
        var npc = gameState.WorldNpcs?.FirstOrDefault(n => n.Id == id);
        if (npc == null)
            return JsonSerializer.Serialize(new { error = "NPC not found" }, _jsonOptions);
        return JsonSerializer.Serialize(npc, _jsonOptions);
    }

    [KernelFunction("get_condition_effects")]
    public async Task<string> GetConditionEffects()
    {
        await Task.Yield();

        var conditions = new Dictionary<string, string>()
        {
            {"Healthy", "No penalties, baseline state"},
            {"Tired", "-1 to most checks, needs rest" },
            {"Injured" , "-2 to Power and Speed checks" },
            {"Poisoned" , "-1 to all checks, periodic damage risk"},
            {"Inspired" , "+2 to Charm checks, increased motivation"},
            {"Focused" , "+2 to Mind checks, enhanced concentration"},
            {"Exhausted" , "-2 to all checks, severe fatigue"},
            {"Confident" , "+1 to Charm and Power checks"},
            {"Intimidated" , "-2 to Charm checks, reduced confidence"}
        };

        var result = new
        {
            conditions,
            mechanics = new
            {
                stacking = "Multiple conditions can be active simultaneously",
                removal = "Conditions can be removed through rest, items, or story events"
            }
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // --- Helper Methods ---

    private int CalculateLevelFromExperience(int experience)
    {
        var level = 1;
        while (CalculateExperienceForLevel(level + 1) <= experience)
        {
            level++;
        }
        return level;
    }

    private int CalculateExperienceForLevel(int level)
    {
        // Experience curve: 1000 * (level - 1)^1.5
        return (int)(1000 * Math.Pow(level - 1, 1.5));
    }
}