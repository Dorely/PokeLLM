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

    #region Game State Management

    [KernelFunction("create_new_game")]
    [Description("Create a new game state with a fresh trainer and world. Example: create_new_game('Ash') creates a new trainer named Ash")]
    public async Task<string> CreateNewGame(
        [Description("Name for the player trainer")] string trainerName = "Trainer")
    {
        Debug.WriteLine($"[GameStatePlugin] CreateNewGame called with trainerName: '{trainerName}'");
        var gameState = await _repository.CreateNewGameStateAsync(trainerName);
        return JsonSerializer.Serialize(gameState, _jsonOptions);
    }

    [KernelFunction("load_game_state")]
    [Description("Load and return the current complete game state for context. Use this to get full details about the current game situation")]
    public async Task<string> LoadGameState()
    {
        Debug.WriteLine($"[GameStatePlugin] LoadGameState called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found. Create a new game first.";
        
        return JsonSerializer.Serialize(gameState, _jsonOptions);
    }

    [KernelFunction("has_game_state")]
    [Description("Check if a saved game state exists. Returns 'true' if a game exists, 'false' if no saved game found")]
    public async Task<string> HasGameState()
    {
        Debug.WriteLine($"[GameStatePlugin] HasGameState called");
        var hasState = await _repository.HasGameStateAsync();
        return hasState ? "true" : "false";
    }

    #endregion

    #region State Queries and Summaries

    [KernelFunction("get_trainer_summary")]
    [Description("Get a comprehensive summary of the trainer's current state, stats, level, conditions, and character creation progress. Example usage: Check if character creation is complete or see current stat values")]
    public async Task<string> GetTrainerSummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetTrainerSummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No trainer found. Create a new game first.";

        var character = gameState.Player.Character;
        var summary = new
        {
            name = character.Name,
            level = character.Level,
            experience = gameState.Player.Experience,
            characterCreationComplete = gameState.Player.CharacterCreationComplete,
            availableStatPoints = gameState.Player.AvailableStatPoints,
            stats = new
            {
                power = character.Stats.Power.Level.ToString(),
                speed = character.Stats.Speed.Level.ToString(),
                mind = character.Stats.Mind.Level.ToString(),
                charm = character.Stats.Charm.Level.ToString(),
                defense = character.Stats.Defense.Level.ToString(),
                spirit = character.Stats.Spirit.Level.ToString()
            },
            statModifiers = new
            {
                powerModifier = (int)character.Stats.Power.Level,
                speedModifier = (int)character.Stats.Speed.Level,
                mindModifier = (int)character.Stats.Mind.Level,
                charmModifier = (int)character.Stats.Charm.Level,
                defenseModifier = (int)character.Stats.Defense.Level,
                spiritModifier = (int)character.Stats.Spirit.Level
            },
            conditions = character.Conditions.Select(c => new { 
                type = c.Type.ToString(), 
                duration = c.Duration,
                severity = c.Severity
            }),
            money = character.Money,
            globalRenown = character.GlobalRenown,
            globalNotoriety = character.GlobalNotoriety,
            inventoryCount = character.Inventory.Count,
            topItems = character.Inventory.Take(5).Select(kvp => new { item = kvp.Key, quantity = kvp.Value }),
            needsCharacterCreation = !gameState.Player.CharacterCreationComplete && gameState.Player.AvailableStatPoints > 0
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("get_pokemon_team_summary")]
    [Description("Get a summary of the trainer's Pokemon team including active party and boxed Pokemon. Shows health status, types, levels, and friendship. Example: Use to check team readiness for battle")]
    public async Task<string> GetPokemonTeamSummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetPokemonTeamSummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var pokemonTeam = gameState.Player.Character.PokemonTeam;
        var summary = new
        {
            activePokemon = pokemonTeam.ActivePokemon.Select(p => new { 
                name = p.Pokemon.Name, 
                species = p.Pokemon.Species, 
                level = p.Pokemon.Level,
                vigor = $"{p.Pokemon.CurrentVigor}/{p.Pokemon.MaxVigor}",
                vigorPercentage = (p.Pokemon.CurrentVigor * 100) / Math.Max(1, p.Pokemon.MaxVigor),
                type1 = p.Pokemon.Type1,
                type2 = p.Pokemon.Type2,
                friendship = p.Friendship,
                caughtLocation = p.CaughtLocation,
                ability = p.Pokemon.Ability
            }),
            boxedPokemon = pokemonTeam.BoxedPokemon.Select(p => new { 
                name = p.Pokemon.Name, 
                species = p.Pokemon.Species, 
                level = p.Pokemon.Level,
                type1 = p.Pokemon.Type1,
                type2 = p.Pokemon.Type2
            }),
            activeCount = pokemonTeam.ActivePokemon.Count,
            boxedCount = pokemonTeam.BoxedPokemon.Count,
            totalCount = pokemonTeam.ActivePokemon.Count + pokemonTeam.BoxedPokemon.Count
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("get_world_state_summary")]
    [Description("Get a summary of the current world state including location, region, time, weather, badges earned, and discovered lore. Example: Use to understand current scene context")]
    public async Task<string> GetWorldStateSummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetWorldStateSummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var worldState = gameState.WorldState;
        var summary = new
        {
            currentLocation = worldState.CurrentLocation,
            currentRegion = worldState.CurrentRegion,
            timeOfDay = worldState.TimeOfDay.ToString(),
            weatherCondition = worldState.WeatherCondition,
            visitedLocationsCount = worldState.VisitedLocations.Count,
            visitedLocations = worldState.VisitedLocations.Take(10),
            gymBadges = worldState.GymBadges.Select(b => new {
                gymName = b.GymName,
                leaderName = b.LeaderName,
                location = b.Location,
                badgeType = b.BadgeType
            }),
            badgeCount = worldState.GymBadges.Count,
            npcRelationshipsCount = worldState.NPCRelationships.Count,
            topNpcRelationships = worldState.NPCRelationships.Take(5).Select(kvp => new { 
                npc = kvp.Key, 
                relationship = kvp.Value 
            }),
            factionReputationsCount = worldState.FactionReputations.Count,
            factionReputations = worldState.FactionReputations.Select(kvp => new { 
                faction = kvp.Key, 
                reputation = kvp.Value 
            }),
            discoveredLoreCount = worldState.DiscoveredLore.Count,
            recentLore = worldState.DiscoveredLore.Take(3),
            activeNpcs = worldState.ActiveNpcs.Count,
            activeNpcPokemon = worldState.ActiveNpcPokemon.Count
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("get_battle_readiness")]
    [Description("Get information about Pokemon team's readiness for battle including health status and team composition. Example: Check if any Pokemon need healing before a gym battle")]
    public async Task<string> GetBattleReadiness()
    {
        Debug.WriteLine($"[GameStatePlugin] GetBattleReadiness called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var activePokemon = gameState.Player.Character.PokemonTeam.ActivePokemon;
        var healthyPokemon = activePokemon.Where(p => p.Pokemon.CurrentVigor > 0).ToList();
        var faintedPokemon = activePokemon.Where(p => p.Pokemon.CurrentVigor == 0).ToList();
        
        var readiness = new
        {
            totalActivePokemon = activePokemon.Count,
            healthyPokemon = healthyPokemon.Count,
            faintedPokemon = faintedPokemon.Count,
            averageVigorPercentage = activePokemon.Any() ? 
                activePokemon.Average(p => (p.Pokemon.CurrentVigor * 100.0) / Math.Max(1, p.Pokemon.MaxVigor)) : 0,
            isReadyForBattle = healthyPokemon.Any(),
            pokemonStatus = activePokemon.Select(p => new {
                name = p.Pokemon.Name,
                species = p.Pokemon.Species,
                vigorPercentage = (p.Pokemon.CurrentVigor * 100) / Math.Max(1, p.Pokemon.MaxVigor),
                status = p.Pokemon.CurrentVigor > 0 ? "Healthy" : "Fainted"
            }),
            recommendedAction = healthyPokemon.Any() ? 
                "Ready for battle" : 
                "Visit Pokemon Center - all Pokemon fainted"
        };

        return JsonSerializer.Serialize(readiness, _jsonOptions);
    }

    [KernelFunction("get_inventory_summary")]
    [Description("Get a detailed summary of the trainer's inventory including money, item counts, and categorized items like healing items and Pokeballs. Example: Check if trainer has enough healing items for a journey")]
    public async Task<string> GetInventorySummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetInventorySummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var inventory = gameState.Player.Character.Inventory;
        var summary = new
        {
            totalItems = inventory.Count,
            totalQuantity = inventory.Values.Sum(),
            money = gameState.Player.Character.Money,
            items = inventory.Select(kvp => new {
                name = kvp.Key,
                quantity = kvp.Value
            }).OrderByDescending(i => i.quantity),
            healingItems = inventory.Where(kvp => kvp.Key.ToLower().Contains("potion") || 
                                                 kvp.Key.ToLower().Contains("heal")).Select(kvp => new {
                name = kvp.Key,
                quantity = kvp.Value
            }),
            pokeballs = inventory.Where(kvp => kvp.Key.ToLower().Contains("ball")).Select(kvp => new {
                name = kvp.Key,
                quantity = kvp.Value
            })
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("get_current_context")]
    [Description("Get focused context information for the current scene including location, time, trainer status, team condition, and battle status. Example: Use to understand the immediate situation for narrative decisions")]
    public async Task<string> GetCurrentContext()
    {
        Debug.WriteLine($"[GameStatePlugin] GetCurrentContext called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var character = gameState.Player.Character;
        var context = new
        {
            // Location context
            location = new
            {
                current = gameState.WorldState.CurrentLocation,
                region = gameState.WorldState.CurrentRegion,
                timeOfDay = gameState.WorldState.TimeOfDay.ToString(),
                weather = gameState.WorldState.WeatherCondition,
                hasVisitedBefore = gameState.WorldState.VisitedLocations.Contains(gameState.WorldState.CurrentLocation)
            },
            
            // Trainer context
            trainer = new
            {
                name = character.Name,
                level = character.Level,
                money = character.Money,
                activeConditions = character.Conditions.Select(c => c.Type.ToString()),
                characterCreationComplete = gameState.Player.CharacterCreationComplete,
                availableStatPoints = gameState.Player.AvailableStatPoints
            },
            
            // Team context
            team = new
            {
                leadPokemon = character.PokemonTeam.ActivePokemon.FirstOrDefault()?.Pokemon.Name ?? "None",
                healthyCount = character.PokemonTeam.ActivePokemon.Count(p => p.Pokemon.CurrentVigor > 0),
                totalActive = character.PokemonTeam.ActivePokemon.Count
            },
            
            // Progress context
            progress = new
            {
                gymBadges = gameState.WorldState.GymBadges.Count,
                locationsVisited = gameState.WorldState.VisitedLocations.Count,
                globalRenown = character.GlobalRenown,
                globalNotoriety = character.GlobalNotoriety
            },
            
            // Battle context
            battleStatus = gameState.BattleState?.IsActive == true ? (object)new
            {
                inBattle = true,
                battleType = gameState.BattleState.BattleType.ToString(),
                currentTurn = gameState.BattleState.CurrentTurn,
                currentPhase = gameState.BattleState.CurrentPhase.ToString()
            } : new
            {
                inBattle = false,
                battleType = "",
                currentTurn = 0,
                currentPhase = ""
            },
            
            // Active NPCs and Pokemon in scene
            activeEntities = new
            {
                npcCount = gameState.WorldState.ActiveNpcs.Count,
                npcPokemonCount = gameState.WorldState.ActiveNpcPokemon.Count,
                npcs = gameState.WorldState.ActiveNpcs.Take(3).Select(npc => new { npc.Name, npc.Faction }),
                npcPokemon = gameState.WorldState.ActiveNpcPokemon.Take(3).Select(p => new { p.Name, p.Species, p.Faction })
            }
        };

        return JsonSerializer.Serialize(context, _jsonOptions);
    }

    #endregion

    #region Debug and Utility

    [KernelFunction("save_game_state")]
    [Description("Manually save the current game state (typically handled automatically). Used for debugging or forced saves. Example: save_game_state(updatedGameStateJson)")]
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

    #endregion
}