using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    [Description("Create a new game state with a fresh trainer and world")]
    public async Task<string> CreateNewGame(
        [Description("Name for the player trainer")] string trainerName = "Trainer")
    {
        Debug.WriteLine($"[GameStatePlugin] CreateNewGame called with trainerName: '{trainerName}'");
        var gameState = await _repository.CreateNewGameStateAsync(trainerName);
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

    [KernelFunction("update_trainer_experience")]
    [Description("Add experience to the trainer and handle level ups")]
    public async Task<string> UpdateTrainerExperience(
        [Description("Amount of experience to add")] int experienceGain)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateTrainerExperience called with experienceGain: {experienceGain}");
        var leveledUp = false;
        var newLevel = 0;

        await _repository.UpdateTrainerAsync(trainer =>
        {
            trainer.Experience += experienceGain;
            
            // Simple level calculation: every 1000 exp = level up
            var targetLevel = (trainer.Experience / 1000) + 1;
            if (targetLevel > trainer.Level)
            {
                trainer.Level = targetLevel;
                leveledUp = true;
                newLevel = trainer.Level;
            }
        });

        if (leveledUp)
            return $"Gained {experienceGain} experience! Leveled up to level {newLevel}!";
        else
            return $"Gained {experienceGain} experience.";
    }

    [KernelFunction("update_trainer_stat")]
    [Description("Update a trainer's stat level")]
    public async Task<string> UpdateTrainerStat(
        [Description("The stat to update (Strength, Agility, Social, Intelligence)")] string statName,
        [Description("The new stat level (-2 to 7)")] int statLevel)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateTrainerStat called with statName: '{statName}', statLevel: {statLevel}");
        
        if (statLevel < -2 || statLevel > 7)
            return "Stat level must be between -2 (Hopeless) and 7 (Legendary).";

        var statLevelEnum = (StatLevel)statLevel;
        var updated = false;

        await _repository.UpdateTrainerAsync(trainer =>
        {
            switch (statName.ToLower())
            {
                case "strength":
                    trainer.Stats.Strength = statLevelEnum;
                    updated = true;
                    break;
                case "agility":
                    trainer.Stats.Agility = statLevelEnum;
                    updated = true;
                    break;
                case "social":
                    trainer.Stats.Social = statLevelEnum;
                    updated = true;
                    break;
                case "intelligence":
                    trainer.Stats.Intelligence = statLevelEnum;
                    updated = true;
                    break;
            }
        });

        return updated ? $"Updated {statName} to {statLevelEnum}." : "Invalid stat name.";
    }

    [KernelFunction("add_trainer_condition")]
    [Description("Add a condition to the trainer")]
    public async Task<string> AddTrainerCondition(
        [Description("The condition type")] string conditionType,
        [Description("Duration in turns (-1 for permanent)")] int duration = -1,
        [Description("Severity level (1-10)")] int severity = 1)
    {
        Debug.WriteLine($"[GameStatePlugin] AddTrainerCondition called with conditionType: '{conditionType}', duration: {duration}, severity: {severity}");
        
        if (!Enum.TryParse<TrainerCondition>(conditionType, true, out var condition))
            return "Invalid condition type.";

        await _repository.UpdateTrainerAsync(trainer =>
        {
            // Remove existing condition of same type
            trainer.Conditions.RemoveAll(c => c.Type == condition);
            
            // Add new condition
            trainer.Conditions.Add(new ActiveCondition
            {
                Type = condition,
                Duration = duration,
                Severity = severity
            });
        });

        return $"Added condition {condition} with duration {duration} and severity {severity}.";
    }

    [KernelFunction("add_pokemon_to_team")]
    [Description("Add a new Pokemon to the trainer's team")]
    public async Task<string> AddPokemonToTeam(
        [Description("JSON string with Pokemon data: {name, species, level, type1, type2?, currentVigor, maxVigor, caughtLocation, friendship?}")] string pokemonJson)
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

    [KernelFunction("update_pokemon_vigor")]
    [Description("Update a Pokemon's vigor (health/energy) by name")]
    public async Task<string> UpdatePokemonVigor(
        [Description("Name of the Pokemon to update")] string pokemonName,
        [Description("New current vigor value")] int currentVigor)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdatePokemonVigor called with pokemonName: '{pokemonName}', currentVigor: {currentVigor}");
        var found = false;

        var state = await _repository.LoadLatestStateAsync();
        if (state != null)
        {
            var pokemon = state.PokemonTeam.ActivePokemon.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase)) ??
                         state.PokemonTeam.BoxedPokemon.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));
            
            if (pokemon != null)
            {
                pokemon.CurrentVigor = Math.Max(0, Math.Min(currentVigor, pokemon.MaxVigor));
                found = true;
                await _repository.SaveStateAsync(state);
            }
        }

        return found ? $"Updated {pokemonName}'s vigor to {currentVigor}." : "Pokemon not found.";
    }

    [KernelFunction("change_location")]
    [Description("Move the trainer to a new location")]
    public async Task<string> ChangeLocation(
        [Description("Name of the new location")] string newLocation,
        [Description("Region where the location is (optional)")] string region = "")
    {
        Debug.WriteLine($"[GameStatePlugin] ChangeLocation called with newLocation: '{newLocation}', region: '{region}'");
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            worldState.CurrentLocation = newLocation;
            if (!string.IsNullOrEmpty(region))
                worldState.CurrentRegion = region;
            
            worldState.VisitedLocations.Add(newLocation);
        });

        return $"Moved to {newLocation}" + (string.IsNullOrEmpty(region) ? "" : $" in {region}") + ".";
    }

    [KernelFunction("update_npc_relationship")]
    [Description("Update relationship with an NPC")]
    public async Task<string> UpdateNPCRelationship(
        [Description("Name or ID of the NPC")] string npcId,
        [Description("Change in relationship level (-100 to 100)")] int relationshipChange)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateNPCRelationship called with npcId: '{npcId}', relationshipChange: {relationshipChange}");
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            var currentLevel = worldState.NPCRelationships.GetValueOrDefault(npcId, 0);
            worldState.NPCRelationships[npcId] = Math.Max(-100, Math.Min(100, currentLevel + relationshipChange));
        });

        return $"Relationship with {npcId} updated by {relationshipChange:+#;-#;0}.";
    }

    [KernelFunction("update_faction_reputation")]
    [Description("Update reputation with a faction")]
    public async Task<string> UpdateFactionReputation(
        [Description("Name of the faction")] string factionName,
        [Description("Change in reputation (-100 to 100)")] int reputationChange)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateFactionReputation called with factionName: '{factionName}', reputationChange: {reputationChange}");
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            var currentRep = worldState.FactionReputations.GetValueOrDefault(factionName, 0);
            worldState.FactionReputations[factionName] = Math.Max(-100, Math.Min(100, currentRep + reputationChange));
        });

        return $"Reputation with {factionName} updated by {reputationChange:+#;-#;0}.";
    }

    [KernelFunction("add_to_inventory")]
    [Description("Add items to the trainer's inventory")]
    public async Task<string> AddToInventory(
        [Description("Name of the item")] string itemName,
        [Description("Quantity to add")] int quantity = 1)
    {
        Debug.WriteLine($"[GameStatePlugin] AddToInventory called with itemName: '{itemName}', quantity: {quantity}");
        await _repository.UpdateTrainerAsync(trainer =>
        {
            trainer.Inventory[itemName] = trainer.Inventory.GetValueOrDefault(itemName, 0) + quantity;
        });

        return $"Added {quantity} {itemName}(s) to inventory.";
    }

    [KernelFunction("update_money")]
    [Description("Update the trainer's money")]
    public async Task<string> UpdateMoney(
        [Description("Amount to add or subtract")] int amount)
    {
        Debug.WriteLine($"[GameStatePlugin] UpdateMoney called with amount: {amount}");
        var newAmount = 0;
        
        await _repository.UpdateTrainerAsync(trainer =>
        {
            trainer.Money = Math.Max(0, trainer.Money + amount);
            newAmount = trainer.Money;
        });

        return $"Money updated by {amount:+#;-#;0}. Current money: {newAmount}.";
    }

    [KernelFunction("earn_gym_badge")]
    [Description("Award a gym badge to the trainer")]
    public async Task<string> EarnGymBadge(
        [Description("Name of the gym")] string gymName,
        [Description("Name of the gym leader")] string leaderName,
        [Description("Location of the gym")] string location,
        [Description("Badge type (Fire, Water, etc.)")] string badgeType)
    {
        Debug.WriteLine($"[GameStatePlugin] EarnGymBadge called with gymName: '{gymName}', leaderName: '{leaderName}'");
        
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            // Check if badge already exists
            if (!worldState.GymBadges.Any(b => b.GymName.Equals(gymName, StringComparison.OrdinalIgnoreCase)))
            {
                worldState.GymBadges.Add(new GymBadge
                {
                    GymName = gymName,
                    LeaderName = leaderName,
                    Location = location,
                    BadgeType = badgeType
                });
            }
        });

        return $"Earned the {badgeType} Badge from {gymName} in {location}!";
    }

    [KernelFunction("discover_lore")]
    [Description("Add discovered lore to the game world")]
    public async Task<string> DiscoverLore(
        [Description("The lore entry to add")] string loreEntry)
    {
        Debug.WriteLine($"[GameStatePlugin] DiscoverLore called with loreEntry: '{loreEntry}'");
        
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            worldState.DiscoveredLore.Add(loreEntry);
        });

        return $"Discovered new lore: {loreEntry}";
    }

    [KernelFunction("set_time_and_weather")]
    [Description("Update the time of day and weather")]
    public async Task<string> SetTimeAndWeather(
        [Description("Time of day (Morning, Afternoon, Evening, Night)")] string timeOfDay,
        [Description("Weather condition")] string weather = "Clear")
    {
        Debug.WriteLine($"[GameStatePlugin] SetTimeAndWeather called with timeOfDay: '{timeOfDay}', weather: '{weather}'");
        
        if (!Enum.TryParse<TimeOfDay>(timeOfDay, true, out var time))
            return "Invalid time of day.";

        await _repository.UpdateWorldStateAsync(worldState =>
        {
            worldState.TimeOfDay = time;
            worldState.WeatherCondition = weather;
        });

        return $"Time set to {time}, weather set to {weather}.";
    }

    [KernelFunction("get_trainer_summary")]
    [Description("Get a summary of the trainer's current state")]
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
            stats = new
            {
                strength = trainer.Stats.Strength.ToString(),
                agility = trainer.Stats.Agility.ToString(),
                social = trainer.Stats.Social.ToString(),
                intelligence = trainer.Stats.Intelligence.ToString()
            },
            archetype = trainer.Archetype.ToString(),
            conditions = trainer.Conditions.Select(c => new { 
                type = c.Type.ToString(), 
                duration = c.Duration,
                severity = c.Severity
            }),
            pokemonTeam = gameState.PokemonTeam.ActivePokemon.Select(p => new { 
                name = p.Name, 
                species = p.Species, 
                level = p.Level,
                vigor = $"{p.CurrentVigor}/{p.MaxVigor}",
                type1 = p.Type1,
                type2 = p.Type2,
                friendship = p.Friendship
            }),
            money = trainer.Money,
            location = gameState.WorldState.CurrentLocation,
            region = gameState.WorldState.CurrentRegion,
            timeOfDay = gameState.WorldState.TimeOfDay.ToString(),
            weather = gameState.WorldState.WeatherCondition,
            gymBadges = gameState.WorldState.GymBadges.Count,
            renown = trainer.GlobalRenown,
            notoriety = trainer.GlobalNotoriety
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