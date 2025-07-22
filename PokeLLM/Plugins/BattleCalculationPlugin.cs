using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Models;
using PokeLLM.Game.Helpers;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for Pokemon battle calculations including type effectiveness, damage calculations,
/// and battle mechanics for the Pokemon D&D campaign
/// </summary>
public class BattleCalculationPlugin
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public BattleCalculationPlugin()
    {
        _random = new Random();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Type Effectiveness

    [KernelFunction("calculate_type_effectiveness")]
    [Description("Calculate type effectiveness multiplier for attacks considering single or dual types. Returns damage multiplier for strategic planning. Example: calculate_type_effectiveness('Fire', 'Grass', '') returns 2.0 for super effective damage.")]
    public async Task<string> CalculateTypeEffectiveness(
        [Description("Type of the attacking move (Fire, Water, Electric, etc.)")] string attackType,
        [Description("Primary defending type")] string defenseType1,
        [Description("Secondary defending type (empty string if monotype)")] string defenseType2 = "")
    {
        Debug.WriteLine($"[BattleCalculationPlugin] CalculateTypeEffectiveness called: {attackType} vs {defenseType1}/{defenseType2}");
        
        var effectiveness = BattleCalcHelper.CalculateDualTypeEffectiveness(attackType, defenseType1, defenseType2);
        var description = BattleCalcHelper.GetEffectivenessDescription(effectiveness);

        var result = new
        {
            attackType = attackType,
            defenseType1 = defenseType1,
            defenseType2 = string.IsNullOrEmpty(defenseType2) ? null : defenseType2,
            effectivenessMultiplier = effectiveness,
            description = description,
            damageCategory = effectiveness switch
            {
                0.0 => "No Effect",
                <= 0.5 => "Not Very Effective",
                1.0 => "Normal Damage",
                >= 2.0 => "Super Effective",
                _ => "Normal Damage"
            },
            strategicAdvice = effectiveness switch
            {
                0.0 => "Attack will have no effect - choose a different move type",
                <= 0.5 => "Weak damage - consider switching moves or Pokemon",
                1.0 => "Standard damage expected",
                >= 2.0 => "High damage potential - good tactical choice!",
                _ => "Standard effectiveness"
            }
        };

        Debug.WriteLine($"[BattleCalculationPlugin] Type effectiveness: {attackType} vs {defenseType1}/{defenseType2} = {effectiveness}x");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_super_effective_types")]
    [Description("Get all types that a given attacking type is super effective against. Useful for strategic planning and Pokemon selection. Example: get_super_effective_types('Fire') returns Grass, Ice, Bug, Steel types.")]
    public async Task<string> GetSuperEffectiveTypes(
        [Description("The attacking type to analyze")] string attackType)
    {
        Debug.WriteLine($"[BattleCalculationPlugin] GetSuperEffectiveTypes called: {attackType}");
        
        var superEffectiveTypes = BattleCalcHelper.GetSuperEffectiveTypes(attackType);
        
        var result = new
        {
            attackType = attackType,
            superEffectiveAgainst = superEffectiveTypes,
            count = superEffectiveTypes.Count,
            strategicValue = superEffectiveTypes.Count switch
            {
                0 => "Low offensive coverage",
                1 or 2 => "Limited offensive coverage", 
                3 or 4 => "Good offensive coverage",
                _ => "Excellent offensive coverage"
            },
            message = superEffectiveTypes.Count > 0 ? 
                $"{attackType} is super effective against: {string.Join(", ", superEffectiveTypes)}" :
                $"{attackType} has no super effective matchups"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_not_very_effective_types")]
    [Description("Get all types that a given attacking type is not very effective against. Important for understanding weaknesses and strategic limitations. Example: get_not_very_effective_types('Fire') shows Water, Fire, Rock, Dragon resistances.")]
    public async Task<string> GetNotVeryEffectiveTypes(
        [Description("The attacking type to analyze")] string attackType)
    {
        Debug.WriteLine($"[BattleCalculationPlugin] GetNotVeryEffectiveTypes called: {attackType}");
        
        var notVeryEffectiveTypes = BattleCalcHelper.GetNotVeryEffectiveTypes(attackType);
        
        var result = new
        {
            attackType = attackType,
            notVeryEffectiveAgainst = notVeryEffectiveTypes,
            count = notVeryEffectiveTypes.Count,
            defensiveWeakness = notVeryEffectiveTypes.Count switch
            {
                0 => "No resistances to worry about",
                1 or 2 => "Few resistances - good coverage",
                3 or 4 => "Several resistances - moderate coverage",
                _ => "Many resistances - limited coverage"
            },
            message = notVeryEffectiveTypes.Count > 0 ? 
                $"{attackType} is not very effective against: {string.Join(", ", notVeryEffectiveTypes)}" :
                $"{attackType} has no reduced effectiveness matchups"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_no_effect_types")]
    [Description("Get all types that a given attacking type has no effect against (0x damage). Critical for avoiding wasted turns. Example: get_no_effect_types('Normal') shows Ghost immunity.")]
    public async Task<string> GetNoEffectTypes(
        [Description("The attacking type to analyze")] string attackType)
    {
        Debug.WriteLine($"[BattleCalculationPlugin] GetNoEffectTypes called: {attackType}");
        
        var noEffectTypes = BattleCalcHelper.GetNoEffectTypes(attackType);
        
        var result = new
        {
            attackType = attackType,
            noEffectAgainst = noEffectTypes,
            count = noEffectTypes.Count,
            immunityWarning = noEffectTypes.Count > 0,
            message = noEffectTypes.Count > 0 ? 
                $"{attackType} has NO EFFECT against: {string.Join(", ", noEffectTypes)}" :
                $"{attackType} has no immunity matchups"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("analyze_type_matchup")]
    [Description("Comprehensive analysis of type matchup including offense and defense considerations. Provides strategic recommendations for battle planning. Example: analyze_type_matchup('Fire', 'Water', 'Flying') for complete tactical assessment.")]
    public async Task<string> AnalyzeTypeMatchup(
        [Description("Attacking type")] string attackType,
        [Description("Defending primary type")] string defenseType1,
        [Description("Defending secondary type (optional)")] string defenseType2 = "")
    {
        Debug.WriteLine($"[BattleCalculationPlugin] AnalyzeTypeMatchup called: {attackType} vs {defenseType1}/{defenseType2}");
        
        var effectiveness = BattleCalcHelper.CalculateDualTypeEffectiveness(attackType, defenseType1, defenseType2);
        var description = BattleCalcHelper.GetEffectivenessDescription(effectiveness);
        
        // Get what this attack type is good/bad against
        var superEffective = BattleCalcHelper.GetSuperEffectiveTypes(attackType);
        var notVeryEffective = BattleCalcHelper.GetNotVeryEffectiveTypes(attackType);
        var noEffect = BattleCalcHelper.GetNoEffectTypes(attackType);

        // Analyze the specific matchup
        var defenseTypes = string.IsNullOrEmpty(defenseType2) ? 
            new[] { defenseType1 } : 
            new[] { defenseType1, defenseType2 };

        var strategicAdvice = new List<string>();
        
        if (effectiveness >= 2.0)
            strategicAdvice.Add("EXCELLENT choice - deal massive damage!");
        else if (effectiveness <= 0.5)
            strategicAdvice.Add("POOR choice - minimal damage expected");
        else if (effectiveness == 0.0)
            strategicAdvice.Add("USELESS - attack will have no effect!");
        else
            strategicAdvice.Add("Standard effectiveness - moderate damage");

        // Add coverage information
        if (superEffective.Count >= 4)
            strategicAdvice.Add($"{attackType} type provides excellent offensive coverage");
        else if (superEffective.Count <= 1)
            strategicAdvice.Add($"{attackType} type has limited offensive coverage");

        var result = new
        {
            matchup = new
            {
                attackType = attackType,
                defenderTypes = defenseTypes,
                effectiveness = effectiveness,
                description = description
            },
            typeAnalysis = new
            {
                superEffectiveAgainst = superEffective,
                notVeryEffectiveAgainst = notVeryEffective,
                noEffectAgainst = noEffect
            },
            battleRecommendation = new
            {
                recommendUse = effectiveness >= 1.0,
                damageExpectation = effectiveness switch
                {
                    0.0 => "No damage",
                    <= 0.5 => "Reduced damage",
                    1.0 => "Normal damage",
                    >= 2.0 => "Enhanced damage",
                    _ => "Normal damage"
                },
                strategicAdvice = strategicAdvice
            },
            summary = $"{attackType} vs {string.Join("/", defenseTypes)}: {effectiveness}x damage ({description})"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Battle Mechanics

    [KernelFunction("calculate_initiative")]
    [Description("Calculate initiative order for Pokemon battle using Speed stats and dice rolls. Determines turn order for strategic planning. Example: Use at battle start to establish action sequence.")]
    public async Task<string> CalculateInitiative(
        [Description("First Pokemon's Speed stat level")] string pokemon1Speed,
        [Description("Second Pokemon's Speed stat level")] string pokemon2Speed,
        [Description("Name of first Pokemon")] string pokemon1Name = "Pokemon 1",
        [Description("Name of second Pokemon")] string pokemon2Name = "Pokemon 2")
    {
        Debug.WriteLine($"[BattleCalculationPlugin] CalculateInitiative called: {pokemon1Name} vs {pokemon2Name}");
        
        if (!Enum.TryParse<StatLevel>(pokemon1Speed, true, out var speed1))
            return JsonSerializer.Serialize(new { error = "Invalid speed level for Pokemon 1" }, _jsonOptions);
        
        if (!Enum.TryParse<StatLevel>(pokemon2Speed, true, out var speed2))
            return JsonSerializer.Serialize(new { error = "Invalid speed level for Pokemon 2" }, _jsonOptions);

        // Create mock Pokemon for initiative calculation
        var pokemon1 = new Pokemon
        {
            Name = pokemon1Name,
            Stats = new Stats { Speed = new Stat { Level = speed1 } }
        };
        
        var pokemon2 = new Pokemon
        {
            Name = pokemon2Name,
            Stats = new Stats { Speed = new Stat { Level = speed2 } }
        };

        var initiative1 = BattleCalcHelper.CalculateInitiative(pokemon1, _random);
        var initiative2 = BattleCalcHelper.CalculateInitiative(pokemon2, _random);

        var winner = initiative1 > initiative2 ? pokemon1Name :
                    initiative2 > initiative1 ? pokemon2Name : "Tie";

        var result = new
        {
            pokemon1 = new
            {
                name = pokemon1Name,
                speedLevel = pokemon1Speed,
                speedModifier = (int)speed1,
                initiative = initiative1
            },
            pokemon2 = new
            {
                name = pokemon2Name,
                speedLevel = pokemon2Speed,
                speedModifier = (int)speed2,
                initiative = initiative2
            },
            turnOrder = initiative1 > initiative2 ? 
                new[] { pokemon1Name, pokemon2Name } :
                initiative2 > initiative1 ?
                new[] { pokemon2Name, pokemon1Name } :
                new[] { "TIE - roll again or choose", "" },
            winner = winner,
            message = winner == "Tie" ? 
                $"Tie! Both rolled {initiative1} - roll again or choose order" :
                $"{winner} goes first! ({(winner == pokemon1Name ? initiative1 : initiative2)} initiative)"
        };

        Debug.WriteLine($"[BattleCalculationPlugin] Initiative: {pokemon1Name} {initiative1} vs {pokemon2Name} {initiative2}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_all_pokemon_types")]
    [Description("Get a complete list of all Pokemon types available in the system. Useful for reference and validation. Example: Use for team planning and type coverage analysis.")]
    public async Task<string> GetAllPokemonTypes()
    {
        Debug.WriteLine($"[BattleCalculationPlugin] GetAllPokemonTypes called");
        
        var allTypes = BattleCalcHelper.GetAllTypes();
        
        var result = new
        {
            totalTypes = allTypes.Count,
            types = allTypes,
            typeCategories = new
            {
                physical = new[] { "Normal", "Fighting", "Rock", "Bug", "Ghost", "Steel" },
                special = new[] { "Fire", "Water", "Electric", "Grass", "Ice", "Psychic", "Dragon", "Dark", "Fairy" },
                variable = new[] { "Poison", "Ground", "Flying" }
            },
            message = $"There are {allTypes.Count} Pokemon types available"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Damage Calculations

    [KernelFunction("estimate_move_damage")]
    [Description("Estimate damage for a Pokemon move using simplified calculations. Provides damage range for strategic planning without full battle context. Example: estimate_move_damage(75, 'Fire', 'Expert', 'Competent', 'Grass', '') for Flamethrower damage estimation.")]
    public async Task<string> EstimateMoveDamage(
        [Description("Base power of the move (typically 40-120)")] int movePower,
        [Description("Type of the move")] string moveType,
        [Description("Attacker's relevant stat level (Power for physical, Mind for special)")] string attackerStatLevel,
        [Description("Defender's relevant stat level (Defense for physical, Spirit for special)")] string defenderStatLevel,
        [Description("Defender's primary type")] string defenderType1,
        [Description("Defender's secondary type (optional)")] string defenderType2 = "")
    {
        Debug.WriteLine($"[BattleCalculationPlugin] EstimateMoveDamage called: {movePower} BP {moveType} move");
        
        if (!Enum.TryParse<StatLevel>(attackerStatLevel, true, out var attackStat))
            return JsonSerializer.Serialize(new { error = "Invalid attacker stat level" }, _jsonOptions);
        
        if (!Enum.TryParse<StatLevel>(defenderStatLevel, true, out var defenseStat))
            return JsonSerializer.Serialize(new { error = "Invalid defender stat level" }, _jsonOptions);

        // Calculate type effectiveness
        var typeEffectiveness = BattleCalcHelper.CalculateDualTypeEffectiveness(moveType, defenderType1, defenderType2);
        var effectivenessDescription = BattleCalcHelper.GetEffectivenessDescription(typeEffectiveness);

        // Simplified damage calculation
        var numDice = Math.Max(1, movePower / 25);
        var minDiceRoll = numDice;
        var maxDiceRoll = numDice * 6;
        var averageDiceRoll = numDice * 3.5;

        var attackModifier = (int)attackStat;
        var defenseModifier = (int)defenseStat;

        // Calculate damage ranges
        var minBaseDamage = (int)((minDiceRoll + attackModifier) * Math.Min(typeEffectiveness, 2.0) - defenseModifier);
        var maxBaseDamage = (int)((maxDiceRoll + attackModifier) * Math.Min(typeEffectiveness, 2.0) - defenseModifier);
        var averageBaseDamage = (int)((averageDiceRoll + attackModifier) * Math.Min(typeEffectiveness, 2.0) - defenseModifier);

        // Ensure minimum 1 damage
        minBaseDamage = Math.Max(1, minBaseDamage);
        maxBaseDamage = Math.Max(1, maxBaseDamage);
        averageBaseDamage = Math.Max(1, averageBaseDamage);

        // Critical hit potential
        var criticalMin = minBaseDamage * 2;
        var criticalMax = maxBaseDamage * 2;
        var criticalAverage = averageBaseDamage * 2;

        var result = new
        {
            moveAnalysis = new
            {
                movePower = movePower,
                moveType = moveType,
                diceRolled = $"{numDice}d6",
                attackerStat = attackerStatLevel,
                defenderStat = defenderStatLevel
            },
            typeMatchup = new
            {
                defenderTypes = string.IsNullOrEmpty(defenderType2) ? 
                    new[] { defenderType1 } : 
                    new[] { defenderType1, defenderType2 },
                effectiveness = typeEffectiveness,
                description = effectivenessDescription
            },
            damageEstimate = new
            {
                minimum = minBaseDamage,
                maximum = maxBaseDamage,
                average = averageBaseDamage,
                range = $"{minBaseDamage}-{maxBaseDamage}",
                criticalHit = new
                {
                    minimum = criticalMin,
                    maximum = criticalMax,
                    average = criticalAverage,
                    range = $"{criticalMin}-{criticalMax}"
                }
            },
            strategicAssessment = new
            {
                damageCategory = averageBaseDamage switch
                {
                    <= 5 => "Very Low",
                    <= 10 => "Low",
                    <= 20 => "Moderate",
                    <= 35 => "High",
                    _ => "Very High"
                },
                recommendation = typeEffectiveness >= 2.0 ? "Excellent choice!" :
                               typeEffectiveness <= 0.5 ? "Poor choice" :
                               typeEffectiveness == 0.0 ? "No effect!" :
                               "Standard effectiveness"
            },
            calculation = $"{numDice}d6 + {attackModifier} (attack) × {typeEffectiveness} (type) - {defenseModifier} (defense)"
        };

        Debug.WriteLine($"[BattleCalculationPlugin] Damage estimate: {minBaseDamage}-{maxBaseDamage} (avg {averageBaseDamage})");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion
}