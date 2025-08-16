using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

public interface ICombatLogicService
{
    Task<bool> InitiateCombat(string attackerId, string defenderId);
    Task<bool> ProcessCombatTurn(string activeParticipantId, string actionType, Dictionary<string, object> actionParams);
    Task<bool> IsCombatActive();
    Task<List<string>> GetCombatParticipants();
    Task<string> GetCurrentTurnParticipant();
    Task EndCombat();
    Task<int> CalculateAttackRoll(string attackerId, string moveId);
    Task<int> CalculateDamage(string attackerId, string defenderId, string moveId);
    Task<bool> AttemptEntityCapture(string entityInstanceId, string captureItemType);
    Task<List<string>> GetAvailableActions(string entityInstanceId);
    Task<bool> CanUseActionCost(string entityInstanceId, string actionId);
    Task ApplyDamage(string targetId, int damage);
    Task ApplyHealing(string targetId, int healing);
    Task ApplyStatusEffect(string targetId, string statusEffect);
    Task RemoveStatusEffect(string targetId, string statusEffect);
    Task ProcessStatusEffects(string participantId);
    Task<bool> CheckFaintedStatus(string participantId);
    Task HandleFaintedParticipant(string participantId);
    Task<List<int>> CalculateInitiativeOrder(List<string> participantIds);
    Task SwitchActiveEntity(string controllerId, string newEntityId);
    Task<bool> CanSwitchEntity(string controllerId);
    Task<double> CalculateTypeEffectiveness(string moveType, string defenderType1, string defenderType2);
    Task<bool> AttemptFlee(string participantId);
    Task<string> GetCombatSummary();
    Task<bool> IsControllerBattle();
    Task<bool> IsWildEntityBattle();
    Task ProcessCombatEndRewards(string winnerId);
}

/// <summary>
/// This service contains methods for managing combat mechanics and battle resolution
/// </summary>
public class CombatLogicService : ICombatLogicService
{
    private readonly IGameStateRepository _gameStateRepository;
    public CombatLogicService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

    public async Task<bool> InitiateCombat(string attackerId, string defenderId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> ProcessCombatTurn(string activeParticipantId, string actionType, Dictionary<string, object> actionParams)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> IsCombatActive()
    {
        throw new NotImplementedException();
    }

    public async Task<List<string>> GetCombatParticipants()
    {
        throw new NotImplementedException();
    }

    public async Task<string> GetCurrentTurnParticipant()
    {
        throw new NotImplementedException();
    }

    public async Task EndCombat()
    {
        throw new NotImplementedException();
    }

    public async Task<int> CalculateAttackRoll(string attackerId, string moveId)
    {
        throw new NotImplementedException();
    }

    public async Task<int> CalculateDamage(string attackerId, string defenderId, string moveId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> AttemptEntityCapture(string entityInstanceId, string captureItemType)
    {
        throw new NotImplementedException();
    }

    public async Task<List<string>> GetAvailableActions(string entityInstanceId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanUseActionCost(string entityInstanceId, string actionId)
    {
        throw new NotImplementedException();
    }

    public async Task ApplyDamage(string targetId, int damage)
    {
        throw new NotImplementedException();
    }

    public async Task ApplyHealing(string targetId, int healing)
    {
        throw new NotImplementedException();
    }

    public async Task ApplyStatusEffect(string targetId, string statusEffect)
    {
        throw new NotImplementedException();
    }

    public async Task RemoveStatusEffect(string targetId, string statusEffect)
    {
        throw new NotImplementedException();
    }

    public async Task ProcessStatusEffects(string participantId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CheckFaintedStatus(string participantId)
    {
        throw new NotImplementedException();
    }

    public async Task HandleFaintedParticipant(string participantId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<int>> CalculateInitiativeOrder(List<string> participantIds)
    {
        throw new NotImplementedException();
    }

    public async Task SwitchActiveEntity(string controllerId, string newEntityId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanSwitchEntity(string controllerId)
    {
        throw new NotImplementedException();
    }

    public async Task<double> CalculateTypeEffectiveness(string moveType, string defenderType1, string defenderType2)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> AttemptFlee(string participantId)
    {
        throw new NotImplementedException();
    }

    public async Task<string> GetCombatSummary()
    {
        throw new NotImplementedException();
    }

    public async Task<bool> IsControllerBattle()
    {
        throw new NotImplementedException();
    }

    public async Task<bool> IsWildEntityBattle()
    {
        throw new NotImplementedException();
    }

    public async Task ProcessCombatEndRewards(string winnerId)
    {
        throw new NotImplementedException();
    }

    // ...existing type effectiveness code...
    private double EffectivenessChart(string attackType, string defenseType)
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
    public double CalculateDualTypeEffectiveness(string attackType, string defenseType1, string defenseType2 = "")
    {
        var effectiveness1 = EffectivenessChart(attackType, defenseType1);
        var effectiveness2 = string.IsNullOrEmpty(defenseType2) ? 1.0 : EffectivenessChart(attackType, defenseType2);

        return effectiveness1 * effectiveness2;
    }

    /// <summary>
    /// Get a list of all Pokemon types available in the system
    /// </summary>
    public List<string> GetAllTypes()
    {
        var list = new List<string> { "normal", "fire", "water", "electric", "grass", "ice", "fighting", "poison", "ground", "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark", "steel", "fairy" };

        return list;
    }

    /// <summary>
    /// Get all types that a given attacking type is super effective against
    /// </summary>
    public List<string> GetSuperEffectiveTypes(string attackType)
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
    public List<string> GetNotVeryEffectiveTypes(string attackType)
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
    public List<string> GetNoEffectTypes(string attackType)
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
    /// Generic damage calculation for entities based on ruleset data
    /// </summary>
    public int CalculateMoveDamage(Dictionary<string, object> attacker, Dictionary<string, object> defender, string moveName,
        int attackPower, string moveType, double effectiveness = 1.0, bool isCritical = false, Random random = null)
    {
        random ??= new Random();
        
        // Basic damage calculation - can be extended by rulesets
        var baseDamage = attackPower;
        
        // Apply effectiveness multiplier
        baseDamage = (int)(baseDamage * effectiveness);
        
        // Apply critical hit
        if (isCritical)
        {
            baseDamage = (int)(baseDamage * 1.5);
        }
        
        // Add some randomness (85-100% of calculated damage)
        var randomFactor = 85 + random.Next(16); // 85-100
        baseDamage = (int)(baseDamage * randomFactor / 100.0);
        
        return Math.Max(1, baseDamage); // Minimum 1 damage
    }

    /// <summary>
    /// Get a human-readable description of type effectiveness
    /// </summary>
    /// <param name="effectiveness">Effectiveness multiplier (0.0, 0.5, 1.0, 2.0, etc.)</param>
    /// <returns>Description string</returns>
    public string GetEffectivenessDescription(double effectiveness)
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
    /// Generic initiative calculation for combat entities
    /// </summary>
    public int CalculateInitiative(Dictionary<string, object> entity, Random random)
    {
        // Try to get speed/dexterity from entity data
        var speed = 50; // Default speed
        
        if (entity.ContainsKey("speed"))
        {
            if (int.TryParse(entity["speed"]?.ToString(), out var speedValue))
                speed = speedValue;
        }
        else if (entity.ContainsKey("dexterity"))
        {
            if (int.TryParse(entity["dexterity"]?.ToString(), out var dexValue))
                speed = dexValue;
        }
        
        // Add random factor (d20)
        return speed + random.Next(1, 21);
    }
}
