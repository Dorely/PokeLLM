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
    [Description("Create a new game state with a fresh trainer and world")]
    public async Task<string> CreateNewGame(
        [Description("Name for the player trainer")] string trainerName = "Trainer")
    {
        Debug.WriteLine($"[GameStatePlugin] CreateNewGame called with trainerName: '{trainerName}'");
        var gameState = await _repository.CreateNewGameStateAsync(trainerName);
        return JsonSerializer.Serialize(gameState, _jsonOptions);
    }

    [KernelFunction("load_game_state")]
    [Description("Load and return the current complete game state for context")]
    public async Task<string> LoadGameState()
    {
        Debug.WriteLine($"[GameStatePlugin] LoadGameState called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found. Create a new game first.";
        
        return JsonSerializer.Serialize(gameState, _jsonOptions);
    }

    [KernelFunction("has_game_state")]
    [Description("Check if a saved game state exists")]
    public async Task<string> HasGameState()
    {
        Debug.WriteLine($"[GameStatePlugin] HasGameState called");
        var hasState = await _repository.HasGameStateAsync();
        return hasState ? "true" : "false";
    }

    #endregion

    #region State Queries and Summaries

    [KernelFunction("get_trainer_summary")]
    [Description("Get a comprehensive summary of the trainer's current state, stats, and progress")]
    public async Task<string> GetTrainerSummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetTrainerSummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No trainer found. Create a new game first.";

        var trainer = gameState.Trainer;
        var summary = new
        {
            name = trainer.Name,
            level = trainer.Level,
            experience = trainer.Experience,
            characterCreationComplete = trainer.CharacterCreationComplete,
            availableStatPoints = trainer.AvailableStatPoints,
            stats = new
            {
                strength = trainer.Stats.Strength.ToString(),
                agility = trainer.Stats.Agility.ToString(),
                social = trainer.Stats.Social.ToString(),
                intelligence = trainer.Stats.Intelligence.ToString()
            },
            statModifiers = new
            {
                strengthModifier = (int)trainer.Stats.Strength,
                agilityModifier = (int)trainer.Stats.Agility,
                socialModifier = (int)trainer.Stats.Social,
                intelligenceModifier = (int)trainer.Stats.Intelligence
            },
            conditions = trainer.Conditions.Select(c => new { 
                type = c.Type.ToString(), 
                duration = c.Duration,
                severity = c.Severity
            }),
            money = trainer.Money,
            globalRenown = trainer.GlobalRenown,
            globalNotoriety = trainer.GlobalNotoriety,
            inventoryCount = trainer.Inventory.Count,
            topItems = trainer.Inventory.Take(5).Select(kvp => new { item = kvp.Key, quantity = kvp.Value }),
            needsCharacterCreation = !trainer.CharacterCreationComplete && trainer.AvailableStatPoints > 0
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("get_pokemon_team_summary")]
    [Description("Get a summary of the trainer's Pokemon team")]
    public async Task<string> GetPokemonTeamSummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetPokemonTeamSummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var summary = new
        {
            activePokemon = gameState.PokemonTeam.ActivePokemon.Select(p => new { 
                name = p.Name, 
                species = p.Species, 
                level = p.Level,
                vigor = $"{p.CurrentVigor}/{p.MaxVigor}",
                vigorPercentage = (p.CurrentVigor * 100) / Math.Max(1, p.MaxVigor),
                type1 = p.Type1,
                type2 = p.Type2,
                friendship = p.Friendship,
                caughtLocation = p.CaughtLocation,
                ability = p.Ability
            }),
            boxedPokemon = gameState.PokemonTeam.BoxedPokemon.Select(p => new { 
                name = p.Name, 
                species = p.Species, 
                level = p.Level,
                type1 = p.Type1,
                type2 = p.Type2
            }),
            activeCount = gameState.PokemonTeam.ActivePokemon.Count,
            boxedCount = gameState.PokemonTeam.BoxedPokemon.Count,
            totalCount = gameState.PokemonTeam.ActivePokemon.Count + gameState.PokemonTeam.BoxedPokemon.Count
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("get_world_state_summary")]
    [Description("Get a summary of the current world state, location, and progress")]
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
            recentLore = worldState.DiscoveredLore.Take(3)
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    [KernelFunction("get_battle_readiness")]
    [Description("Get information about Pokemon team's readiness for battle")]
    public async Task<string> GetBattleReadiness()
    {
        Debug.WriteLine($"[GameStatePlugin] GetBattleReadiness called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var activePokemon = gameState.PokemonTeam.ActivePokemon;
        var healthyPokemon = activePokemon.Where(p => p.CurrentVigor > 0).ToList();
        var faintedPokemon = activePokemon.Where(p => p.CurrentVigor == 0).ToList();
        
        var readiness = new
        {
            totalActivePokemon = activePokemon.Count,
            healthyPokemon = healthyPokemon.Count,
            faintedPokemon = faintedPokemon.Count,
            averageVigorPercentage = activePokemon.Any() ? 
                activePokemon.Average(p => (p.CurrentVigor * 100.0) / Math.Max(1, p.MaxVigor)) : 0,
            isReadyForBattle = healthyPokemon.Any(),
            pokemonStatus = activePokemon.Select(p => new {
                name = p.Name,
                species = p.Species,
                vigorPercentage = (p.CurrentVigor * 100) / Math.Max(1, p.MaxVigor),
                status = p.CurrentVigor > 0 ? "Healthy" : "Fainted"
            }),
            recommendedAction = healthyPokemon.Any() ? 
                "Ready for battle" : 
                "Visit Pokemon Center - all Pokemon fainted"
        };

        return JsonSerializer.Serialize(readiness, _jsonOptions);
    }

    [KernelFunction("get_inventory_summary")]
    [Description("Get a detailed summary of the trainer's inventory")]
    public async Task<string> GetInventorySummary()
    {
        Debug.WriteLine($"[GameStatePlugin] GetInventorySummary called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

        var inventory = gameState.Trainer.Inventory;
        var summary = new
        {
            totalItems = inventory.Count,
            totalQuantity = inventory.Values.Sum(),
            money = gameState.Trainer.Money,
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
    [Description("Get focused context information for the current scene (location, time, immediate surroundings)")]
    public async Task<string> GetCurrentContext()
    {
        Debug.WriteLine($"[GameStatePlugin] GetCurrentContext called");
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return "No game state found.";

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
                name = gameState.Trainer.Name,
                level = gameState.Trainer.Level,
                money = gameState.Trainer.Money,
                activeConditions = gameState.Trainer.Conditions.Select(c => c.Type.ToString()),
                characterCreationComplete = gameState.Trainer.CharacterCreationComplete,
                availableStatPoints = gameState.Trainer.AvailableStatPoints
            },
            
            // Team context
            team = new
            {
                leadPokemon = gameState.PokemonTeam.ActivePokemon.FirstOrDefault()?.Name ?? "None",
                healthyCount = gameState.PokemonTeam.ActivePokemon.Count(p => p.CurrentVigor > 0),
                totalActive = gameState.PokemonTeam.ActivePokemon.Count
            },
            
            // Progress context
            progress = new
            {
                gymBadges = gameState.WorldState.GymBadges.Count,
                locationsVisited = gameState.WorldState.VisitedLocations.Count,
                globalRenown = gameState.Trainer.GlobalRenown,
                globalNotoriety = gameState.Trainer.GlobalNotoriety
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
            }
        };

        return JsonSerializer.Serialize(context, _jsonOptions);
    }

    #endregion

    #region Debug and Utility

    [KernelFunction("save_game_state")]
    [Description("Manually save the current game state (typically handled automatically)")]
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