using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing D&D 5e-style Pokemon combat encounters
/// </summary>
public class CombatManagementPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public CombatManagementPlugin(IGameStateRepository repository)
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

    [KernelFunction("resolve_pokemon_attack")]
    [Description("Resolves a Pokemon attack using D&D 5e-style combat mechanics with type effectiveness.")]
    public async Task<string> ResolvePokemonAttack(
        [Description("The ID of the attacking Pokemon")]
        string attackerId,
        [Description("The ID of the move being used")]
        string moveId,
        [Description("The ID of the target Pokemon")]
        string targetId,
        [Description("Optional modifier to the attack roll")]
        int attackModifier = 0)
    {
        try
        {
            Debug.WriteLine($"[CombatManagementPlugin] ResolvePokemonAttack called: {attackerId} -> {targetId} with {moveId}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Find attacker and target
            var attacker = FindPokemon(gameState, attackerId);
            var target = FindPokemon(gameState, targetId);
            
            if (attacker == null)
                return JsonSerializer.Serialize(new { error = $"Attacker Pokemon {attackerId} not found" }, _jsonOptions);
            
            if (target == null)
                return JsonSerializer.Serialize(new { error = $"Target Pokemon {targetId} not found" }, _jsonOptions);

            // Find the move
            var move = attacker.KnownMoves.FirstOrDefault(m => m.Id == moveId);
            if (move == null)
                return JsonSerializer.Serialize(new { error = $"Move {moveId} not known by {attackerId}" }, _jsonOptions);

            // Check if attacker has enough vigor
            if (attacker.CurrentVigor < move.VigorCost)
                return JsonSerializer.Serialize(new { error = $"{attacker.Species} doesn't have enough vigor to use {move.Name}" }, _jsonOptions);

            // Resolve the attack
            var result = CombatRules.ResolveAttack(attacker, move, target, attackModifier);

            // Apply vigor cost
            attacker.CurrentVigor = Math.Max(0, attacker.CurrentVigor - move.VigorCost);

            // Apply damage
            if (result.Damage > 0)
            {
                target.CurrentVigor = Math.Max(0, target.CurrentVigor - result.Damage);
            }

            // Save game state
            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new
            {
                success = true,
                attacker = attacker.Species,
                target = target.Species,
                move = move.Name,
                attackRoll = result.AttackRoll,
                hit = result.Hit,
                damage = result.Damage,
                critical = result.Critical,
                typeEffectiveness = result.TypeEffectiveness,
                description = result.Description,
                targetVigor = target.CurrentVigor,
                attackerVigor = attacker.CurrentVigor
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CombatManagementPlugin] Error in ResolvePokemonAttack: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to resolve attack: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("make_saving_throw")]
    [Description("Makes a saving throw for a Pokemon to resist a status effect or special attack.")]
    public async Task<string> MakeSavingThrow(
        [Description("The ID of the Pokemon making the saving throw")]
        string pokemonId,
        [Description("The ability to use for the save (Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma)")]
        string ability,
        [Description("The Difficulty Class for the saving throw")]
        int difficultyClass,
        [Description("Optional modifier to the saving throw")]
        int modifier = 0)
    {
        try
        {
            Debug.WriteLine($"[CombatManagementPlugin] MakeSavingThrow called for {pokemonId}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            var pokemon = FindPokemon(gameState, pokemonId);
            if (pokemon == null)
                return JsonSerializer.Serialize(new { error = $"Pokemon {pokemonId} not found" }, _jsonOptions);

            var success = CombatRules.ResolveSavingThrow(pokemon, difficultyClass, ability, modifier);

            return JsonSerializer.Serialize(new
            {
                success = true,
                pokemon = pokemon.Species,
                ability = ability,
                difficultyClass = difficultyClass,
                saveSuccessful = success.Success,
                roll = success.Roll,
                total = success.Total,
                description = success.Description
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CombatManagementPlugin] Error in MakeSavingThrow: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to make saving throw: {ex.Message}" }, _jsonOptions);
        }
    }

    private Pokemon? FindPokemon(GameStateModel gameState, string pokemonId)
    {
        // Check world pokemon
        if (gameState.WorldPokemon.ContainsKey(pokemonId))
            return gameState.WorldPokemon[pokemonId];

        // Check player's team
        var teamPokemon = gameState.Player.TeamPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (teamPokemon != null)
            return teamPokemon.Pokemon;

        // Check player's box
        var boxedPokemon = gameState.Player.BoxedPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
        if (boxedPokemon != null)
            return boxedPokemon.Pokemon;

        return null;
    }
}

/// <summary>
/// Static helper class for D&D 5e-style Pokemon combat rules
/// </summary>
public static class CombatRules
{
    private static readonly Random _random = new Random();

    /// <summary>
    /// Calculates the D&D 5e ability modifier from an ability score
    /// Formula: floor((abilityScore - 10) / 2)
    /// </summary>
    /// <param name="abilityScore">The ability score (typically 3-20)</param>
    /// <returns>The modifier (-4 to +5 for typical scores)</returns>
    public static int CalculateModifier(int abilityScore)
    {
        return (int)Math.Floor((abilityScore - 10) / 2.0);
    }

    /// <summary>
    /// Resolves a Pokemon attack using D&D 5e mechanics with Pokemon type effectiveness
    /// </summary>
    /// <param name="attacker">The attacking Pokemon</param>
    /// <param name="move">The move being used</param>
    /// <param name="target">The target Pokemon</param>
    /// <param name="attackModifier">Optional modifier to the attack roll</param>
    /// <returns>The result of the attack</returns>
    public static CombatResult ResolveAttack(Pokemon attacker, Move move, Pokemon target, int attackModifier = 0)
    {//TODO move this to its own file, remove similar methods between this and PokemonKnowledgeHelper
        var result = new CombatResult
        {
            AttackerName = attacker.Species,
            TargetName = target.Species,
            MoveName = move.Name,
            MoveCategory = move.Category
        };

        // Status moves don't use attack rolls or deal damage
        if (move.Category == MoveCategory.Status)
        {
            result.Hit = true;
            result.Damage = 0;
            result.Description = $"{attacker.Species} used {move.Name}! {move.Description}";
            return result;
        }

        // Calculate attack modifier based on move category
        var attackStat = move.Category == MoveCategory.Physical ? attacker.Stats.Strength : attacker.Stats.Intelligence;
        var attackStatModifier = CalculateModifier(attackStat);

        // Calculate target's AC (10 + DEX modifier)
        var targetAC = 10 + CalculateModifier(target.Stats.Dexterity);

        // Roll d20 for attack
        var d20Roll = _random.Next(1, 21);
        var attackRoll = d20Roll + attackStatModifier + attackModifier;

        result.AttackRoll = attackRoll;
        result.Hit = attackRoll >= targetAC;
        result.Critical = d20Roll == 20;

        // Calculate type effectiveness
        var typeEffectiveness = CalculateTypeEffectiveness(move.Type, target.Type1, target.Type2);
        result.TypeEffectiveness = typeEffectiveness;

        if (!result.Hit && !result.Critical)
        {
            result.Damage = 0;
            result.Description = $"{attacker.Species} used {move.Name}, but it missed!";
            return result;
        }

        // Parse damage dice (e.g., "2d6" becomes 2 dice of 6 sides)
        if (!string.IsNullOrEmpty(move.DamageDice))
        {
            var (numDice, diceSize) = ParseDamageDice(move.DamageDice);
            var baseDamage = 0;

            // Roll damage with type effectiveness affecting advantage/disadvantage
            for (int i = 0; i < numDice; i++)
            {
                if (typeEffectiveness > 1.0)
                {
                    // Super effective - advantage (roll twice, take higher)
                    var roll1 = _random.Next(1, diceSize + 1);
                    var roll2 = _random.Next(1, diceSize + 1);
                    baseDamage += Math.Max(roll1, roll2);
                }
                else if (typeEffectiveness < 1.0 && typeEffectiveness > 0.0)
                {
                    // Not very effective - disadvantage (roll twice, take lower)
                    var roll1 = _random.Next(1, diceSize + 1);
                    var roll2 = _random.Next(1, diceSize + 1);
                    baseDamage += Math.Min(roll1, roll2);
                }
                else
                {
                    // Normal effectiveness
                    baseDamage += _random.Next(1, diceSize + 1);
                }
            }

            // Add ability modifier to damage
            baseDamage += Math.Max(0, attackStatModifier);

            // Critical hits multiply damage by 1.5
            if (result.Critical)
            {
                baseDamage = (int)(baseDamage * 1.5);
            }

            result.Damage = Math.Max(1, baseDamage);
        }
        else
        {
            result.Damage = 0;
        }

        // Create description
        var effectivenessText = typeEffectiveness switch
        {
            0.0 => " It had no effect!",
            < 1.0 => " It's not very effective...",
            > 1.0 => " It's super effective!",
            _ => ""
        };

        var criticalText = result.Critical ? " Critical hit!" : "";
        
        result.Description = $"{attacker.Species} used {move.Name}!{effectivenessText}{criticalText}";
        if (result.Damage > 0)
        {
            result.Description += $" It dealt {result.Damage} damage!";
        }

        return result;
    }

    /// <summary>
    /// Resolves a saving throw for a Pokemon
    /// </summary>
    /// <param name="target">The Pokemon making the save</param>
    /// <param name="dc">The Difficulty Class</param>
    /// <param name="ability">The ability to use for the save</param>
    /// <param name="modifier">Optional modifier</param>
    /// <returns>The result of the saving throw</returns>
    public static SavingThrowResult ResolveSavingThrow(Pokemon target, int dc, string ability, int modifier = 0)
    {
        var abilityScore = ability.ToLower() switch
        {
            "strength" => target.Stats.Strength,
            "dexterity" => target.Stats.Dexterity,
            "constitution" => target.Stats.Constitution,
            "intelligence" => target.Stats.Intelligence,
            "wisdom" => target.Stats.Wisdom,
            "charisma" => target.Stats.Charisma,
            _ => 10
        };

        var abilityModifier = CalculateModifier(abilityScore);
        var roll = _random.Next(1, 21);
        var total = roll + abilityModifier + modifier;
        var success = total >= dc;

        return new SavingThrowResult
        {
            PokemonName = target.Species,
            Ability = ability,
            Roll = roll,
            Total = total,
            DifficultyClass = dc,
            Success = success,
            Description = $"{target.Species} makes a {ability} saving throw: {total} vs DC {dc} - {(success ? "Success!" : "Failure!")}"
        };
    }

    private static (int numDice, int diceSize) ParseDamageDice(string damageDice)
    {
        // Parse damage dice string like "2d6" or "1d8"
        var parts = damageDice.ToLower().Split('d');
        if (parts.Length == 2 && int.TryParse(parts[0], out var numDice) && int.TryParse(parts[1], out var diceSize))
        {
            return (numDice, diceSize);
        }
        return (1, 6); // Default to 1d6
    }

    private static double CalculateTypeEffectiveness(PokemonType attackType, PokemonType defenseType1, PokemonType? defenseType2)
    {
        var effectiveness1 = GetTypeEffectiveness(attackType, defenseType1);
        var effectiveness2 = defenseType2.HasValue ? GetTypeEffectiveness(attackType, defenseType2.Value) : 1.0;
        return effectiveness1 * effectiveness2;
    }

//TODO expand this
    private static double GetTypeEffectiveness(PokemonType attackType, PokemonType defenseType)
    {
        // Simplified type effectiveness chart - can be expanded
        return (attackType, defenseType) switch
        {
            // Fire effectiveness
            (PokemonType.Fire, PokemonType.Grass) => 2.0,
            (PokemonType.Fire, PokemonType.Ice) => 2.0,
            (PokemonType.Fire, PokemonType.Bug) => 2.0,
            (PokemonType.Fire, PokemonType.Steel) => 2.0,
            (PokemonType.Fire, PokemonType.Water) => 0.5,
            (PokemonType.Fire, PokemonType.Fire) => 0.5,
            (PokemonType.Fire, PokemonType.Rock) => 0.5,
            (PokemonType.Fire, PokemonType.Dragon) => 0.5,

            // Water effectiveness
            (PokemonType.Water, PokemonType.Fire) => 2.0,
            (PokemonType.Water, PokemonType.Ground) => 2.0,
            (PokemonType.Water, PokemonType.Rock) => 2.0,
            (PokemonType.Water, PokemonType.Water) => 0.5,
            (PokemonType.Water, PokemonType.Grass) => 0.5,
            (PokemonType.Water, PokemonType.Dragon) => 0.5,

            // Electric effectiveness
            (PokemonType.Electric, PokemonType.Water) => 2.0,
            (PokemonType.Electric, PokemonType.Flying) => 2.0,
            (PokemonType.Electric, PokemonType.Ground) => 0.0,
            (PokemonType.Electric, PokemonType.Electric) => 0.5,
            (PokemonType.Electric, PokemonType.Grass) => 0.5,
            (PokemonType.Electric, PokemonType.Dragon) => 0.5,

            // Grass effectiveness
            (PokemonType.Grass, PokemonType.Water) => 2.0,
            (PokemonType.Grass, PokemonType.Ground) => 2.0,
            (PokemonType.Grass, PokemonType.Rock) => 2.0,
            (PokemonType.Grass, PokemonType.Fire) => 0.5,
            (PokemonType.Grass, PokemonType.Grass) => 0.5,
            (PokemonType.Grass, PokemonType.Poison) => 0.5,
            (PokemonType.Grass, PokemonType.Flying) => 0.5,
            (PokemonType.Grass, PokemonType.Bug) => 0.5,
            (PokemonType.Grass, PokemonType.Dragon) => 0.5,
            (PokemonType.Grass, PokemonType.Steel) => 0.5,

            // Default neutral effectiveness
            _ => 1.0
        };
    }
}

/// <summary>
/// Result of a Pokemon attack resolution
/// </summary>
public class CombatResult
{
    public string AttackerName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string MoveName { get; set; } = string.Empty;
    public MoveCategory MoveCategory { get; set; }
    public int AttackRoll { get; set; }
    public bool Hit { get; set; }
    public bool Critical { get; set; }
    public int Damage { get; set; }
    public double TypeEffectiveness { get; set; } = 1.0;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Result of a saving throw
/// </summary>
public class SavingThrowResult
{
    public string PokemonName { get; set; } = string.Empty;
    public string Ability { get; set; } = string.Empty;
    public int Roll { get; set; }
    public int Total { get; set; }
    public int DifficultyClass { get; set; }
    public bool Success { get; set; }
    public string Description { get; set; } = string.Empty;
}
