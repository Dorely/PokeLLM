using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing Pokemon team operations including adding Pokemon, healing, status management,
/// and team composition within the Pokemon D&D-style campaign
/// </summary>
public class PokemonManagementPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public PokemonManagementPlugin(IGameStateRepository repository)
    {
        _repository = repository;
        _random = new Random();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Pokemon Team Management

    [KernelFunction("add_pokemon_to_team")]
    [Description("Add a Pokemon to the trainer's team with specific attributes. Creates a new team member with stats, type, and location info. Example: add_pokemon_to_team('Sparky', 'Pikachu', 5, 'Electric', '', 35, 35, 'Route 1', 50, 'Static') for a newly caught Pokemon.")]
    public async Task<string> AddPokemonToTeam(
        [Description("Nickname/name for the Pokemon")] string name,
        [Description("Species name (e.g., 'Pikachu', 'Charizard')")] string species,
        [Description("Current level of the Pokemon")] int level,
        [Description("Primary type (Fire, Water, Electric, etc.)")] string type1,
        [Description("Secondary type (empty string if monotype)")] string type2 = "",
        [Description("Current vigor/health points")] int currentVigor = 100,
        [Description("Maximum vigor/health points")] int maxVigor = 100,
        [Description("Location where Pokemon was caught")] string caughtLocation = "",
        [Description("Friendship level with trainer (0-100)")] int friendship = 50,
        [Description("Pokemon's special ability")] string ability = "")
    {
        Debug.WriteLine($"[PokemonManagementPlugin] AddPokemonToTeam called: name={name}, species={species}, level={level}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Check team size limit (typically 6)
        if (gameState.Player.Character.PokemonTeam.ActivePokemon.Count >= 6)
        {
            return JsonSerializer.Serialize(new { error = "Team is full (6 Pokemon maximum). Store Pokemon in PC first." }, _jsonOptions);
        }

        // Create new Pokemon
        var pokemon = new Pokemon
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Species = species,
            Level = level,
            Type1 = type1,
            Type2 = string.IsNullOrEmpty(type2) ? null : type2,
            CurrentVigor = currentVigor,
            MaxVigor = maxVigor,
            Ability = ability,
            KnownMoves = new HashSet<string>(),
            StatusEffects = new List<StatusEffect>(),
            Stats = GenerateBasePokemonStats(level),
            Faction = gameState.Player.Character.Faction // Pokemon inherits trainer's faction
        };

        // Create owned Pokemon wrapper
        var ownedPokemon = new OwnedPokemon
        {
            Pokemon = pokemon,
            Experience = 0,
            CaughtLocation = caughtLocation,
            Friendship = friendship
        };

        await _repository.UpdatePlayerAsync(player =>
        {
            player.Character.PokemonTeam.ActivePokemon.Add(ownedPokemon);
        });

        var result = new
        {
            success = true,
            pokemon = new
            {
                id = pokemon.Id,
                name = pokemon.Name,
                species = pokemon.Species,
                level = pokemon.Level,
                type1 = pokemon.Type1,
                type2 = pokemon.Type2,
                currentVigor = pokemon.CurrentVigor,
                maxVigor = pokemon.MaxVigor,
                caughtLocation = ownedPokemon.CaughtLocation,
                friendship = ownedPokemon.Friendship,
                ability = pokemon.Ability
            },
            teamSize = gameState.Player.Character.PokemonTeam.ActivePokemon.Count + 1,
            message = $"{name} the {species} joined the team!"
        };

        Debug.WriteLine($"[PokemonManagementPlugin] Added {name} to team");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("update_pokemon_vigor")]
    [Description("Update a Pokemon's current vigor/health, typically due to battle damage, healing, or environmental effects. Example: update_pokemon_vigor('Sparky', 20, 'Took damage from Gym battle') to reduce health.")]
    public async Task<string> UpdatePokemonVigor(
        [Description("Name or ID of the Pokemon to update")] string pokemonName,
        [Description("New current vigor amount (0 = fainted)")] int currentVigor,
        [Description("Reason for the vigor change")] string reason = "")
    {
        Debug.WriteLine($"[PokemonManagementPlugin] UpdatePokemonVigor called: pokemonName={pokemonName}, vigor={currentVigor}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Find Pokemon by name or ID
        var ownedPokemon = gameState.Player.Character.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase) || 
                                p.Pokemon.Id.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (ownedPokemon == null)
        {
            return JsonSerializer.Serialize(new { error = $"Pokemon '{pokemonName}' not found in active team" }, _jsonOptions);
        }

        var oldVigor = ownedPokemon.Pokemon.CurrentVigor;
        var clampedVigor = Math.Max(0, Math.Min(currentVigor, ownedPokemon.Pokemon.MaxVigor));

        await _repository.UpdatePlayerAsync(player =>
        {
            var targetPokemon = player.Character.PokemonTeam.ActivePokemon
                .First(p => p.Pokemon.Id == ownedPokemon.Pokemon.Id);
            targetPokemon.Pokemon.CurrentVigor = clampedVigor;
        });

        var status = clampedVigor == 0 ? "fainted" : 
                    clampedVigor < ownedPokemon.Pokemon.MaxVigor * 0.25 ? "critical" :
                    clampedVigor < ownedPokemon.Pokemon.MaxVigor * 0.5 ? "low" : "healthy";

        var result = new
        {
            success = true,
            pokemon = ownedPokemon.Pokemon.Name,
            oldVigor = oldVigor,
            newVigor = clampedVigor,
            maxVigor = ownedPokemon.Pokemon.MaxVigor,
            status = status,
            reason = reason,
            message = $"{ownedPokemon.Pokemon.Name}'s vigor changed from {oldVigor} to {clampedVigor}"
        };

        Debug.WriteLine($"[PokemonManagementPlugin] Updated {ownedPokemon.Pokemon.Name} vigor: {oldVigor} -> {clampedVigor}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("heal_pokemon")]
    [Description("Fully heal a Pokemon's vigor to maximum, typically at Pokemon Centers or through items. Example: heal_pokemon('Sparky', 'Pokemon Center treatment') to restore full health.")]
    public async Task<string> HealPokemon(
        [Description("Name or ID of the Pokemon to heal")] string pokemonName,
        [Description("Reason for healing (Pokemon Center, Potion, etc.)")] string reason = "")
    {
        Debug.WriteLine($"[PokemonManagementPlugin] HealPokemon called: pokemonName={pokemonName}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        // Find Pokemon by name or ID
        var ownedPokemon = gameState.Player.Character.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase) || 
                                p.Pokemon.Id.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (ownedPokemon == null)
        {
            return JsonSerializer.Serialize(new { error = $"Pokemon '{pokemonName}' not found in active team" }, _jsonOptions);
        }

        var oldVigor = ownedPokemon.Pokemon.CurrentVigor;

        await _repository.UpdatePlayerAsync(player =>
        {
            var targetPokemon = player.Character.PokemonTeam.ActivePokemon
                .First(p => p.Pokemon.Id == ownedPokemon.Pokemon.Id);
            targetPokemon.Pokemon.CurrentVigor = targetPokemon.Pokemon.MaxVigor;
            // Also clear status effects when fully healed
            targetPokemon.Pokemon.StatusEffects.Clear();
        });

        var result = new
        {
            success = true,
            pokemon = ownedPokemon.Pokemon.Name,
            oldVigor = oldVigor,
            newVigor = ownedPokemon.Pokemon.MaxVigor,
            statusEffectsCleared = true,
            reason = reason,
            message = $"{ownedPokemon.Pokemon.Name} fully healed! {reason}"
        };

        Debug.WriteLine($"[PokemonManagementPlugin] Healed {ownedPokemon.Pokemon.Name} from {oldVigor} to {ownedPokemon.Pokemon.MaxVigor}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("add_pokemon_status_effect")]
    [Description("Add a status effect to a Pokemon (Sleep, Paralysis, Burn, etc.). Effects can impact battle performance and interactions. Example: add_pokemon_status_effect('Sparky', 'Paralysis', 5, 3) for battle conditions.")]
    public async Task<string> AddPokemonStatusEffect(
        [Description("Name or ID of the Pokemon")] string pokemonName,
        [Description("Status effect type: Sleep, Paralysis, Burn, Poison, Freeze, Confusion, etc.")] string statusType,
        [Description("Duration in turns (-1 for indefinite)")] int duration = -1,
        [Description("Severity level affecting impact (1-10)")] int severity = 1)
    {
        Debug.WriteLine($"[PokemonManagementPlugin] AddPokemonStatusEffect called: pokemonName={pokemonName}, status={statusType}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var ownedPokemon = gameState.Player.Character.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase) || 
                                p.Pokemon.Id.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (ownedPokemon == null)
        {
            return JsonSerializer.Serialize(new { error = $"Pokemon '{pokemonName}' not found in active team" }, _jsonOptions);
        }

        await _repository.UpdatePlayerAsync(player =>
        {
            var targetPokemon = player.Character.PokemonTeam.ActivePokemon
                .First(p => p.Pokemon.Id == ownedPokemon.Pokemon.Id);
            
            // Remove existing status of same type
            targetPokemon.Pokemon.StatusEffects.RemoveAll(s => s.Name.Equals(statusType, StringComparison.OrdinalIgnoreCase));
            
            // Add new status effect
            targetPokemon.Pokemon.StatusEffects.Add(new StatusEffect
            {
                Name = statusType,
                Type = StatusEffectType.Debuff, // Default to debuff
                Duration = duration,
                Severity = severity
            });
        });

        var result = new
        {
            success = true,
            pokemon = ownedPokemon.Pokemon.Name,
            statusEffect = statusType,
            duration = duration,
            severity = severity,
            message = $"{ownedPokemon.Pokemon.Name} is affected by {statusType}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("remove_pokemon_status_effect")]
    [Description("Remove a specific status effect from a Pokemon, typically through healing or natural recovery. Example: remove_pokemon_status_effect('Sparky', 'Paralysis') after successful treatment.")]
    public async Task<string> RemovePokemonStatusEffect(
        [Description("Name or ID of the Pokemon")] string pokemonName,
        [Description("Status effect type to remove")] string statusType)
    {
        Debug.WriteLine($"[PokemonManagementPlugin] RemovePokemonStatusEffect called: pokemonName={pokemonName}, status={statusType}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var ownedPokemon = gameState.Player.Character.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase) || 
                                p.Pokemon.Id.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (ownedPokemon == null)
        {
            return JsonSerializer.Serialize(new { error = $"Pokemon '{pokemonName}' not found in active team" }, _jsonOptions);
        }

        var removed = false;
        await _repository.UpdatePlayerAsync(player =>
        {
            var targetPokemon = player.Character.PokemonTeam.ActivePokemon
                .First(p => p.Pokemon.Id == ownedPokemon.Pokemon.Id);
            var initialCount = targetPokemon.Pokemon.StatusEffects.Count;
            targetPokemon.Pokemon.StatusEffects.RemoveAll(s => s.Name.Equals(statusType, StringComparison.OrdinalIgnoreCase));
            removed = targetPokemon.Pokemon.StatusEffects.Count < initialCount;
        });

        var result = new
        {
            success = removed,
            pokemon = ownedPokemon.Pokemon.Name,
            statusEffect = statusType,
            message = removed ? $"Removed {statusType} from {ownedPokemon.Pokemon.Name}" : $"{ownedPokemon.Pokemon.Name} was not affected by {statusType}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("update_pokemon_friendship")]
    [Description("Update a Pokemon's friendship level with the trainer based on interactions, care, and shared experiences. Affects performance and evolution. Example: update_pokemon_friendship('Sparky', 75, 'Excellent care and training') for positive bonding.")]
    public async Task<string> UpdatePokemonFriendship(
        [Description("Name or ID of the Pokemon")] string pokemonName,
        [Description("New friendship level (0-100)")] int newFriendshipLevel,
        [Description("Reason for friendship change")] string reason = "")
    {
        Debug.WriteLine($"[PokemonManagementPlugin] UpdatePokemonFriendship called: pokemonName={pokemonName}, friendship={newFriendshipLevel}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var ownedPokemon = gameState.Player.Character.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase) || 
                                p.Pokemon.Id.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (ownedPokemon == null)
        {
            return JsonSerializer.Serialize(new { error = $"Pokemon '{pokemonName}' not found in active team" }, _jsonOptions);
        }

        var oldFriendship = ownedPokemon.Friendship;
        var clampedFriendship = Math.Max(0, Math.Min(newFriendshipLevel, 100));

        await _repository.UpdatePlayerAsync(player =>
        {
            var targetPokemon = player.Character.PokemonTeam.ActivePokemon
                .First(p => p.Pokemon.Id == ownedPokemon.Pokemon.Id);
            targetPokemon.Friendship = clampedFriendship;
        });

        var friendshipDescription = clampedFriendship switch
        {
            >= 80 => "Best Friends",
            >= 60 => "Close Friends", 
            >= 40 => "Good Friends",
            >= 20 => "Friendly",
            _ => "Distant"
        };

        var result = new
        {
            success = true,
            pokemon = ownedPokemon.Pokemon.Name,
            oldFriendship = oldFriendship,
            newFriendship = clampedFriendship,
            friendshipLevel = friendshipDescription,
            reason = reason,
            message = $"{ownedPokemon.Pokemon.Name}'s friendship changed from {oldFriendship} to {clampedFriendship} ({friendshipDescription})"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("teach_pokemon_move")]
    [Description("Teach a Pokemon a new move, adding it to their known moves list. Represents learning through training, TMs, or level advancement. Example: teach_pokemon_move('Sparky', 'Thunderbolt', 'Learned from TM24') for special attacks.")]
    public async Task<string> TeachPokemonMove(
        [Description("Name or ID of the Pokemon")] string pokemonName,
        [Description("Name of the move to learn")] string moveName,
        [Description("How the move was learned (level up, TM, tutor, etc.)")] string learnMethod = "")
    {
        Debug.WriteLine($"[PokemonManagementPlugin] TeachPokemonMove called: pokemonName={pokemonName}, move={moveName}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var ownedPokemon = gameState.Player.Character.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase) || 
                                p.Pokemon.Id.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (ownedPokemon == null)
        {
            return JsonSerializer.Serialize(new { error = $"Pokemon '{pokemonName}' not found in active team" }, _jsonOptions);
        }

        var alreadyKnown = ownedPokemon.Pokemon.KnownMoves.Contains(moveName);
        
        await _repository.UpdatePlayerAsync(player =>
        {
            var targetPokemon = player.Character.PokemonTeam.ActivePokemon
                .First(p => p.Pokemon.Id == ownedPokemon.Pokemon.Id);
            targetPokemon.Pokemon.KnownMoves.Add(moveName);
        });

        var result = new
        {
            success = true,
            pokemon = ownedPokemon.Pokemon.Name,
            move = moveName,
            learnMethod = learnMethod,
            alreadyKnown = alreadyKnown,
            totalMoves = ownedPokemon.Pokemon.KnownMoves.Count + (alreadyKnown ? 0 : 1),
            message = alreadyKnown ? 
                $"{ownedPokemon.Pokemon.Name} already knows {moveName}" : 
                $"{ownedPokemon.Pokemon.Name} learned {moveName}! {learnMethod}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_team_status")]
    [Description("Get comprehensive status of the trainer's Pokemon team including health, levels, and conditions. Useful for battle preparation and team management. Example: Check team before gym challenge.")]
    public async Task<string> GetTeamStatus()
    {
        Debug.WriteLine($"[PokemonManagementPlugin] GetTeamStatus called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var team = gameState.Player.Character.PokemonTeam.ActivePokemon;
        var teamStatus = team.Select(op => new
        {
            id = op.Pokemon.Id,
            name = op.Pokemon.Name,
            species = op.Pokemon.Species,
            level = op.Pokemon.Level,
            type1 = op.Pokemon.Type1,
            type2 = op.Pokemon.Type2,
            currentVigor = op.Pokemon.CurrentVigor,
            maxVigor = op.Pokemon.MaxVigor,
            vigorPercent = (double)op.Pokemon.CurrentVigor / op.Pokemon.MaxVigor * 100,
            friendship = op.Friendship,
            status = op.Pokemon.CurrentVigor == 0 ? "Fainted" :
                    op.Pokemon.CurrentVigor < op.Pokemon.MaxVigor * 0.25 ? "Critical" :
                    op.Pokemon.CurrentVigor < op.Pokemon.MaxVigor * 0.5 ? "Low" : "Healthy",
            statusEffects = op.Pokemon.StatusEffects.Select(s => new { name = s.Name, duration = s.Duration, severity = s.Severity } ),
            knownMoves = op.Pokemon.KnownMoves.ToList(),
            ability = op.Pokemon.Ability,
            caughtLocation = op.CaughtLocation,
            experience = op.Experience
        }).ToList();

        var healthyPokemon = team.Count(p => p.Pokemon.CurrentVigor > 0);
        var faintedPokemon = team.Count(p => p.Pokemon.CurrentVigor == 0);

        var result = new
        {
            teamSize = team.Count,
            maxTeamSize = 6,
            healthyPokemon = healthyPokemon,
            faintedPokemon = faintedPokemon,
            averageLevel = team.Count > 0 ? team.Average(p => p.Pokemon.Level) : 0,
            team = teamStatus
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generate base stats for a Pokemon at a given level
    /// This is a simplified stat generation - in a full game this would be species-specific
    /// </summary>
    private Stats GenerateBasePokemonStats(int level)
    {
        // Base Pokemon stats scale with level, but are generally lower than trainer stats
        var baseStatLevel = level switch
        {
            <= 5 => StatLevel.Incompetent,
            <= 10 => StatLevel.Novice,
            <= 20 => StatLevel.Trained,
            <= 30 => StatLevel.Expert,
            <= 40 => StatLevel.Master,
            _ => StatLevel.Legendary
        };

        return new Stats
        {
            Power = new Stat { Level = baseStatLevel },
            Speed = new Stat { Level = baseStatLevel },
            Mind = new Stat { Level = baseStatLevel },
            Charm = new Stat { Level = StatLevel.Incompetent }, // Pokemon typically have lower charm
            Defense = new Stat { Level = baseStatLevel },
            Spirit = new Stat { Level = baseStatLevel }
        };
    }

    #endregion
}