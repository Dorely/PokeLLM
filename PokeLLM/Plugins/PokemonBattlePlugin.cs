using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

public class PokemonBattlePlugin
{
    private readonly Dictionary<string, TypeEffectiveness> _typeChart;

    public PokemonBattlePlugin()
    {
        _typeChart = InitializeTypeChart();
    }

    [KernelFunction("calculate_damage")]
    [Description("Calculate damage for a Pokemon move in battle")]
    public async Task<string> CalculateDamage(
        [Description("JSON string with attacker stats: {name, level, attack, specialAttack, type1, type2}")] string attacker,
        [Description("JSON string with defender stats: {name, level, defense, specialDefense, type1, type2}")] string defender,
        [Description("JSON string with move details: {name, power, type, category}")] string move,
        [Description("Random factor between 0.85 and 1.0")] double randomFactor = 0.925)
    {
        try
        {
            var attackerData = JsonSerializer.Deserialize<PokemonStats>(attacker);
            var defenderData = JsonSerializer.Deserialize<PokemonStats>(defender);
            var moveData = JsonSerializer.Deserialize<MoveData>(move);

            var damage = CalculateDamageInternal(attackerData, defenderData, moveData, randomFactor);

            var result = new DamageResult
            {
                Damage = damage.FinalDamage,
                TypeEffectiveness = damage.TypeMultiplier,
                EffectivenessText = GetEffectivenessText(damage.TypeMultiplier),
                IsCritical = damage.IsCritical,
                AttackType = moveData.Category,
                MoveName = moveData.Name
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [KernelFunction("get_type_effectiveness")]
    [Description("Get type effectiveness multiplier between attacking type and defending types")]
    public string GetTypeEffectiveness(
        [Description("Attacking move type")] string attackingType,
        [Description("Defender's first type")] string defenderType1,
        [Description("Defender's second type (optional)")] string defenderType2 = "")
    {
        var multiplier = CalculateTypeEffectiveness(attackingType, defenderType1, defenderType2);
        return JsonSerializer.Serialize(new
        {
            multiplier = multiplier,
            effectiveness = GetEffectivenessText(multiplier)
        });
    }

    private DamageCalculation CalculateDamageInternal(PokemonStats attacker, PokemonStats defender, MoveData move, double randomFactor)
    {
        // Determine if it's a critical hit (simplified - 1/24 chance)
        var isCritical = new Random().NextDouble() < (1.0 / 24.0);

        // Get appropriate attack and defense stats
        var attackStat = move.Category.ToLower() == "physical" ? attacker.Attack : attacker.SpecialAttack;
        var defenseStat = move.Category.ToLower() == "physical" ? defender.Defense : defender.SpecialDefense;

        // STAB (Same Type Attack Bonus)
        var stab = (attacker.Type1.ToLower() == move.Type.ToLower() ||
                   attacker.Type2?.ToLower() == move.Type.ToLower()) ? 1.5 : 1.0;

        // Type effectiveness
        var typeMultiplier = CalculateTypeEffectiveness(move.Type, defender.Type1, defender.Type2);

        // Critical hit multiplier
        var criticalMultiplier = isCritical ? 1.5 : 1.0;

        // Base damage calculation (simplified Pokemon formula)
        var baseDamage = ((2.0 * attacker.Level / 5.0 + 2.0) * move.Power * attackStat / defenseStat / 50.0 + 2.0);

        // Apply all multipliers
        var finalDamage = (int)(baseDamage * stab * typeMultiplier * criticalMultiplier * randomFactor);

        // Ensure minimum 1 damage
        finalDamage = Math.Max(1, finalDamage);

        return new DamageCalculation
        {
            FinalDamage = finalDamage,
            TypeMultiplier = typeMultiplier,
            IsCritical = isCritical,
            StabMultiplier = stab
        };
    }

    private double CalculateTypeEffectiveness(string attackingType, string defenderType1, string defenderType2)
    {
        var effectiveness1 = GetSingleTypeEffectiveness(attackingType, defenderType1);
        var effectiveness2 = string.IsNullOrEmpty(defenderType2) ? 1.0 : GetSingleTypeEffectiveness(attackingType, defenderType2);

        return effectiveness1 * effectiveness2;
    }

    private double GetSingleTypeEffectiveness(string attackingType, string defendingType)
    {
        var attacking = attackingType.ToLower();
        var defending = defendingType.ToLower();

        if (_typeChart.ContainsKey(attacking))
        {
            var effectiveness = _typeChart[attacking];

            if (effectiveness.SuperEffective.Contains(defending))
                return 2.0;
            else if (effectiveness.NotVeryEffective.Contains(defending))
                return 0.5;
            else if (effectiveness.NoEffect.Contains(defending))
                return 0.0;
        }

        return 1.0; // Normal effectiveness
    }

    private string GetEffectivenessText(double multiplier)
    {
        return multiplier switch
        {
            0.0 => "It had no effect!",
            < 1.0 => "It's not very effective...",
            > 1.0 => "It's super effective!",
            _ => ""
        };
    }

    private Dictionary<string, TypeEffectiveness> InitializeTypeChart()
    {
        return new Dictionary<string, TypeEffectiveness>
        {
            ["normal"] = new TypeEffectiveness
            {
                NotVeryEffective = new[] { "rock", "steel" },
                NoEffect = new[] { "ghost" }
            },
            ["fire"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "grass", "ice", "bug", "steel" },
                NotVeryEffective = new[] { "fire", "water", "rock", "dragon" }
            },
            ["water"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "fire", "ground", "rock" },
                NotVeryEffective = new[] { "water", "grass", "dragon" }
            },
            ["electric"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "water", "flying" },
                NotVeryEffective = new[] { "electric", "grass", "dragon" },
                NoEffect = new[] { "ground" }
            },
            ["grass"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "water", "ground", "rock" },
                NotVeryEffective = new[] { "fire", "grass", "poison", "flying", "bug", "dragon", "steel" }
            },
            ["ice"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "grass", "ground", "flying", "dragon" },
                NotVeryEffective = new[] { "fire", "water", "ice", "steel" }
            },
            ["fighting"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "normal", "ice", "rock", "dark", "steel" },
                NotVeryEffective = new[] { "poison", "flying", "psychic", "bug", "fairy" },
                NoEffect = new[] { "ghost" }
            },
            ["poison"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "grass", "fairy" },
                NotVeryEffective = new[] { "poison", "ground", "rock", "ghost" },
                NoEffect = new[] { "steel" }
            },
            ["ground"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "fire", "electric", "poison", "rock", "steel" },
                NotVeryEffective = new[] { "grass", "bug" },
                NoEffect = new[] { "flying" }
            },
            ["flying"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "electric", "grass", "fighting", "bug" },
                NotVeryEffective = new[] { "rock", "steel" }
            },
            ["psychic"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "fighting", "poison" },
                NotVeryEffective = new[] { "psychic", "steel" },
                NoEffect = new[] { "dark" }
            },
            ["bug"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "grass", "psychic", "dark" },
                NotVeryEffective = new[] { "fire", "fighting", "poison", "flying", "ghost", "steel", "fairy" }
            },
            ["rock"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "fire", "ice", "flying", "bug" },
                NotVeryEffective = new[] { "fighting", "ground", "steel" }
            },
            ["ghost"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "psychic", "ghost" },
                NotVeryEffective = new[] { "dark" },
                NoEffect = new[] { "normal" }
            },
            ["dragon"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "dragon" },
                NotVeryEffective = new[] { "steel" },
                NoEffect = new[] { "fairy" }
            },
            ["dark"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "psychic", "ghost" },
                NotVeryEffective = new[] { "fighting", "dark", "fairy" }
            },
            ["steel"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "ice", "rock", "fairy" },
                NotVeryEffective = new[] { "fire", "water", "electric", "steel" }
            },
            ["fairy"] = new TypeEffectiveness
            {
                SuperEffective = new[] { "fighting", "dragon", "dark" },
                NotVeryEffective = new[] { "fire", "poison", "steel" }
            }
        };
    }
}

public class PokemonStats
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("attack")]
    public int Attack { get; set; }

    [JsonPropertyName("specialAttack")]
    public int SpecialAttack { get; set; }

    [JsonPropertyName("defense")]
    public int Defense { get; set; }

    [JsonPropertyName("specialDefense")]
    public int SpecialDefense { get; set; }

    [JsonPropertyName("type1")]
    public string Type1 { get; set; } = "";

    [JsonPropertyName("type2")]
    public string? Type2 { get; set; }
}

public class MoveData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("power")]
    public int Power { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = ""; // "Physical" or "Special"
}

public class DamageResult
{
    public int Damage { get; set; }
    public double TypeEffectiveness { get; set; }
    public string EffectivenessText { get; set; } = "";
    public bool IsCritical { get; set; }
    public string AttackType { get; set; } = "";
    public string MoveName { get; set; } = "";
}

public class DamageCalculation
{
    public int FinalDamage { get; set; }
    public double TypeMultiplier { get; set; }
    public bool IsCritical { get; set; }
    public double StabMultiplier { get; set; }
}

public class TypeEffectiveness
{
    public string[] SuperEffective { get; set; } = Array.Empty<string>();
    public string[] NotVeryEffective { get; set; } = Array.Empty<string>();
    public string[] NoEffect { get; set; } = Array.Empty<string>();
}