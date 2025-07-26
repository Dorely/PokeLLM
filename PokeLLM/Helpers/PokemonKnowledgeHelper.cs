namespace PokeLLM.Game.Helpers;

public static class PokemonKnowledgeHelper
{
    public static double EffectivenessChart(string attackType, string defenseType)
    {
        // Complete Pokemon type effectiveness chart
        var effectiveness = new Dictionary<string, double>();

        // Normal type effectiveness
        effectiveness["Normal_Rock"] = 0.5;
        effectiveness["Normal_Ghost"] = 0.0;
        effectiveness["Normal_Steel"] = 0.5;

        // Fire type effectiveness
        effectiveness["Fire_Grass"] = 2.0;
        effectiveness["Fire_Ice"] = 2.0;
        effectiveness["Fire_Bug"] = 2.0;
        effectiveness["Fire_Steel"] = 2.0;
        effectiveness["Fire_Water"] = 0.5;
        effectiveness["Fire_Fire"] = 0.5;
        effectiveness["Fire_Rock"] = 0.5;
        effectiveness["Fire_Dragon"] = 0.5;

        // Water type effectiveness
        effectiveness["Water_Fire"] = 2.0;
        effectiveness["Water_Ground"] = 2.0;
        effectiveness["Water_Rock"] = 2.0;
        effectiveness["Water_Water"] = 0.5;
        effectiveness["Water_Grass"] = 0.5;
        effectiveness["Water_Dragon"] = 0.5;

        // Electric type effectiveness
        effectiveness["Electric_Water"] = 2.0;
        effectiveness["Electric_Flying"] = 2.0;
        effectiveness["Electric_Electric"] = 0.5;
        effectiveness["Electric_Grass"] = 0.5;
        effectiveness["Electric_Dragon"] = 0.5;
        effectiveness["Electric_Ground"] = 0.0;

        // Grass type effectiveness
        effectiveness["Grass_Water"] = 2.0;
        effectiveness["Grass_Ground"] = 2.0;
        effectiveness["Grass_Rock"] = 2.0;
        effectiveness["Grass_Fire"] = 0.5;
        effectiveness["Grass_Grass"] = 0.5;
        effectiveness["Grass_Poison"] = 0.5;
        effectiveness["Grass_Flying"] = 0.5;
        effectiveness["Grass_Bug"] = 0.5;
        effectiveness["Grass_Dragon"] = 0.5;
        effectiveness["Grass_Steel"] = 0.5;

        // Ice type effectiveness
        effectiveness["Ice_Grass"] = 2.0;
        effectiveness["Ice_Ground"] = 2.0;
        effectiveness["Ice_Flying"] = 2.0;
        effectiveness["Ice_Dragon"] = 2.0;
        effectiveness["Ice_Fire"] = 0.5;
        effectiveness["Ice_Water"] = 0.5;
        effectiveness["Ice_Ice"] = 0.5;
        effectiveness["Ice_Steel"] = 0.5;

        // Fighting type effectiveness
        effectiveness["Fighting_Normal"] = 2.0;
        effectiveness["Fighting_Ice"] = 2.0;
        effectiveness["Fighting_Rock"] = 2.0;
        effectiveness["Fighting_Dark"] = 2.0;
        effectiveness["Fighting_Steel"] = 2.0;
        effectiveness["Fighting_Poison"] = 0.5;
        effectiveness["Fighting_Flying"] = 0.5;
        effectiveness["Fighting_Psychic"] = 0.5;
        effectiveness["Fighting_Bug"] = 0.5;
        effectiveness["Fighting_Fairy"] = 0.5;
        effectiveness["Fighting_Ghost"] = 0.0;

        // Poison type effectiveness
        effectiveness["Poison_Grass"] = 2.0;
        effectiveness["Poison_Fairy"] = 2.0;
        effectiveness["Poison_Poison"] = 0.5;
        effectiveness["Poison_Ground"] = 0.5;
        effectiveness["Poison_Rock"] = 0.5;
        effectiveness["Poison_Ghost"] = 0.5;
        effectiveness["Poison_Steel"] = 0.0;

        // Ground type effectiveness
        effectiveness["Ground_Fire"] = 2.0;
        effectiveness["Ground_Electric"] = 2.0;
        effectiveness["Ground_Poison"] = 2.0;
        effectiveness["Ground_Rock"] = 2.0;
        effectiveness["Ground_Steel"] = 2.0;
        effectiveness["Ground_Grass"] = 0.5;
        effectiveness["Ground_Bug"] = 0.5;
        effectiveness["Ground_Flying"] = 0.0;

        // Flying type effectiveness
        effectiveness["Flying_Electric"] = 0.5;
        effectiveness["Flying_Rock"] = 0.5;
        effectiveness["Flying_Steel"] = 0.5;
        effectiveness["Flying_Grass"] = 2.0;
        effectiveness["Flying_Fighting"] = 2.0;
        effectiveness["Flying_Bug"] = 2.0;

        // Psychic type effectiveness
        effectiveness["Psychic_Fighting"] = 2.0;
        effectiveness["Psychic_Poison"] = 2.0;
        effectiveness["Psychic_Psychic"] = 0.5;
        effectiveness["Psychic_Steel"] = 0.5;
        effectiveness["Psychic_Dark"] = 0.0;

        // Bug type effectiveness
        effectiveness["Bug_Grass"] = 2.0;
        effectiveness["Bug_Psychic"] = 2.0;
        effectiveness["Bug_Dark"] = 2.0;
        effectiveness["Bug_Fire"] = 0.5;
        effectiveness["Bug_Fighting"] = 0.5;
        effectiveness["Bug_Poison"] = 0.5;
        effectiveness["Bug_Flying"] = 0.5;
        effectiveness["Bug_Ghost"] = 0.5;
        effectiveness["Bug_Steel"] = 0.5;
        effectiveness["Bug_Fairy"] = 0.5;

        // Rock type effectiveness
        effectiveness["Rock_Fire"] = 2.0;
        effectiveness["Rock_Ice"] = 2.0;
        effectiveness["Rock_Flying"] = 2.0;
        effectiveness["Rock_Bug"] = 2.0;
        effectiveness["Rock_Fighting"] = 0.5;
        effectiveness["Rock_Ground"] = 0.5;
        effectiveness["Rock_Steel"] = 0.5;

        // Ghost type effectiveness
        effectiveness["Ghost_Psychic"] = 2.0;
        effectiveness["Ghost_Ghost"] = 2.0;
        effectiveness["Ghost_Dark"] = 0.5;
        effectiveness["Ghost_Normal"] = 0.0;

        // Dragon type effectiveness
        effectiveness["Dragon_Dragon"] = 2.0;
        effectiveness["Dragon_Steel"] = 0.5;
        effectiveness["Dragon_Fairy"] = 0.0;

        // Dark type effectiveness
        effectiveness["Dark_Psychic"] = 2.0;
        effectiveness["Dark_Ghost"] = 2.0;
        effectiveness["Dark_Fighting"] = 0.5;
        effectiveness["Dark_Dark"] = 0.5;
        effectiveness["Dark_Fairy"] = 0.5;

        // Steel type effectiveness
        effectiveness["Steel_Ice"] = 2.0;
        effectiveness["Steel_Rock"] = 2.0;
        effectiveness["Steel_Fairy"] = 2.0;
        effectiveness["Steel_Fire"] = 0.5;
        effectiveness["Steel_Water"] = 0.5;
        effectiveness["Steel_Electric"] = 0.5;
        effectiveness["Steel_Steel"] = 0.5;

        // Fairy type effectiveness
        effectiveness["Fairy_Fighting"] = 2.0;
        effectiveness["Fairy_Dragon"] = 2.0;
        effectiveness["Fairy_Dark"] = 2.0;
        effectiveness["Fairy_Fire"] = 0.5;
        effectiveness["Fairy_Poison"] = 0.5;
        effectiveness["Fairy_Steel"] = 0.5;

        var key = $"{attackType}_{defenseType}";
        return effectiveness.GetValueOrDefault(key, 1.0);
    }

    /// <summary>
    /// Calculate total type effectiveness considering dual types
    /// </summary>
    public static double CalculateDualTypeEffectiveness(string attackType, string defenseType1, string defenseType2 = "")
    {
        var effectiveness1 = EffectivenessChart(attackType, defenseType1);
        var effectiveness2 = string.IsNullOrEmpty(defenseType2) ? 1.0 : EffectivenessChart(attackType, defenseType2);
        
        return effectiveness1 * effectiveness2;
    }

    /// <summary>
    /// Get a list of all Pokemon types available in the system
    /// </summary>
    public static List<string> GetAllTypes()
    {
        var list = new List<string>(Enum.GetNames(typeof(PokemonType)));

        return list;
    }

    /// <summary>
    /// Get all types that a given attacking type is super effective against
    /// </summary>
    public static List<string> GetSuperEffectiveTypes(string attackType)
    {
        var superEffectiveTypes = new List<string>();
        var allTypes = GetAllTypes();
        
        foreach (var defenseType in allTypes)
        {
            if (EffectivenessChart(attackType, defenseType) == 2.0)
            {
                superEffectiveTypes.Add(defenseType);
            }
        }
        
        return superEffectiveTypes;
    }

    /// <summary>
    /// Get all types that a given attacking type is not very effective against
    /// </summary>
    public static List<string> GetNotVeryEffectiveTypes(string attackType)
    {
        var notVeryEffectiveTypes = new List<string>();
        var allTypes = GetAllTypes();
        
        foreach (var defenseType in allTypes)
        {
            var effectiveness = EffectivenessChart(attackType, defenseType);
            if (effectiveness == 0.5 || effectiveness == 0.25)
            {
                notVeryEffectiveTypes.Add(defenseType);
            }
        }
        
        return notVeryEffectiveTypes;
    }

    /// <summary>
    /// Get all types that a given attacking type has no effect against
    /// </summary>
    public static List<string> GetNoEffectTypes(string attackType)
    {
        var noEffectTypes = new List<string>();
        var allTypes = GetAllTypes();
        
        foreach (var defenseType in allTypes)
        {
            if (EffectivenessChart(attackType, defenseType) == 0.0)
            {
                noEffectTypes.Add(defenseType);
            }
        }
        
        return noEffectTypes;
    }

    /// <summary>
    /// Calculate damage for a Pokemon move using D&D-style dice mechanics
    /// For Pokemon battles, damage is based on dice rolls with type effectiveness for advantage/disadvantage only
    /// </summary>
    /// <param name="attacker">The attacking Pokemon with full battle data</param>
    /// <param name="defender">The defending Pokemon with full battle data</param>
    /// <param name="moveName">Name of the move being used</param>
    /// <param name="moveType">Type of the move (Fire, Water, etc.)</param>
    /// <param name="numDice">Number of dice to roll for damage</param>
    /// <param name="hitDiceRoll">The d20 roll used to hit (used to determine critical hits on natural 20)</param>
    /// <param name="isSpecialMove">Whether this uses special attack/defense stats</param>
    /// <param name="random">Random number generator for dice rolls</param>
    /// <returns>Final damage amount after all calculations</returns>
    public static int CalculateMoveDamage(Pokemon attacker, Pokemon defender, string moveName, 
        string moveType, int numDice, int hitDiceRoll, bool isSpecialMove, Random random)
    {
        // Get attacker's relevant stat for calculating bonus dice
        var attackStat = isSpecialMove ? attacker.Stats.Intelligence : attacker.Stats.Strength;
        var attackModifier = (int)Math.Floor((attackStat - 10) / 2.0);
        
        // Calculate bonus dice: +1 die for every +2 ability modifier above 0
        var bonusDice = Math.Max(0, attackModifier / 2);
        var totalDice = numDice + bonusDice;
        
        int baseDamage = 0;
        
        // Apply type effectiveness for advantage/disadvantage on dice rolls only
        var typeEffectiveness = CalculateDualTypeEffectiveness(moveType, defender.Type1.ToString(), defender.Type2?.ToString() ?? "");
        
        // Roll damage dice with advantage/disadvantage based on type effectiveness
        if (typeEffectiveness > 1.0)
        {
            // Super effective - roll with advantage (roll twice, take higher)
            for (int i = 0; i < totalDice; i++)
            {
                int roll1 = random.Next(1, 7);
                int roll2 = random.Next(1, 7);
                baseDamage += Math.Max(roll1, roll2);
            }
        }
        else if (typeEffectiveness < 1.0 && typeEffectiveness > 0.0)
        {
            // Not very effective - roll with disadvantage (roll twice, take lower)
            for (int i = 0; i < totalDice; i++)
            {
                int roll1 = random.Next(1, 7);
                int roll2 = random.Next(1, 7);
                baseDamage += Math.Min(roll1, roll2);
            }
        }
        else
        {
            // Normal effectiveness or no effect - roll normally
            for (int i = 0; i < totalDice; i++)
            {
                baseDamage += random.Next(1, 7); // 1d6
            }
        }
                
        // Ensure minimum 1 damage before critical hit calculation
        baseDamage = Math.Max(1, baseDamage);
        
        // Check for critical hit (natural 20 on hit dice)
        if (hitDiceRoll == 20)
        {
            baseDamage = (int)(baseDamage * 1.5); // 50% increased damage for critical hit
        }
        
        return baseDamage;
    }

    /// <summary>
    /// Get a human-readable description of type effectiveness
    /// </summary>
    /// <param name="effectiveness">Effectiveness multiplier (0.0, 0.5, 1.0, 2.0, etc.)</param>
    /// <returns>Description string</returns>
    public static string GetEffectivenessDescription(double effectiveness)
    {
        return effectiveness switch
        {
            0.0 => "No Effect",
            <= 0.5 => "Not Very Effective",
            1.0 => "Normal Effectiveness", 
            >= 2.0 => "Super Effective",
            _ => "Normal Effectiveness"
        };
    }

    /// <summary>
    /// Calculate initiative for a Pokemon in battle using Dexterity stat + d20 roll
    /// </summary>
    /// <param name="pokemon">Pokemon to calculate initiative for</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Initiative value for turn order</returns>
    public static int CalculateInitiative(Pokemon pokemon, Random random)
    {
        int dexterityModifier = (int)Math.Floor((pokemon.Stats.Dexterity - 10) / 2.0);
        int roll = random.Next(1, 21); // 1d20
        return roll + dexterityModifier;
    }

}
