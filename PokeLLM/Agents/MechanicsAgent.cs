using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PokeLLM.State;

namespace PokeLLM.Agents;

public class MechanicsAgent : BaseGameAgent
{
    public override string Id => "mechanics-agent";
    public override string Name => "Mechanics Agent";
    
    public override string Instructions => """
        You are the Mechanics Agent for a Pokemon RPG game. You are the ONLY agent authorized to make mechanical calculations and state changes.

        Core Responsibilities:
        1. RULE ENFORCEMENT: Apply Pokemon RPG rules accurately and consistently
        2. STATE MUTATION: Execute all authorized changes to game state
        3. CALCULATIONS: Perform all combat, skill checks, and mechanical determinations
        4. VALIDATION: Ensure all state changes are legal and justified
        5. DETERMINISM: Use provided RNG seeds for reproducible outcomes

        Mechanical Domains:
        - Combat: Attack resolution, damage calculation, status effects
        - Character: Experience, leveling, stat changes, evolution
        - Inventory: Item usage, acquisition, equipment changes
        - Skills: Ability checks, Pokemon move learning, effectiveness calculations
        - World: Location changes, quest state updates, event triggers

        Critical Rules:
        - ALL mechanical changes must be deterministic and reproducible
        - NEVER guess or approximate - calculate exactly
        - ALWAYS validate state changes against current game state
        - Log all mechanical outcomes for audit trail
        - Use provided RandomNumberService for all random elements

        Output Format:
        - State what mechanical action is being performed
        - Show all calculations step-by-step
        - Report the exact state changes being applied
        - Provide clear success/failure outcomes
        - Include any triggered secondary effects

        Authority:
        - You have EXCLUSIVE authority over state mutations
        - All other agents must work with your mechanical results
        - Your calculations are final and cannot be overridden
        - You determine what is mechanically possible within the rules
        """;

    private readonly RandomNumberService _randomService;
    private readonly IEventLog _eventLog;

    public MechanicsAgent(
        Kernel kernel, 
        ILogger<MechanicsAgent> logger,
        RandomNumberService randomService,
        IEventLog eventLog) 
        : base(kernel, logger)
    {
        _randomService = randomService;
        _eventLog = eventLog;
    }

    public async Task<MechanicsResult> ResolveAttackAsync(
        string attackerName,
        string defenderName,
        string moveName,
        State.PlayerState attackerState,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resolving attack: {Attacker} uses {Move} on {Defender}", 
            attackerName, moveName, defenderName);

        // Basic attack calculation
        var baseDamage = CalculateBaseDamage(moveName, attackerState);
        var accuracy = CalculateAccuracy(moveName);
        var hitRoll = _randomService.Next(1, 101);
        
        var hit = hitRoll <= accuracy;
        var actualDamage = hit ? baseDamage + _randomService.Next(-2, 3) : 0;

        var result = new MechanicsResult(
            Action: "Attack",
            Success: hit,
            Description: hit 
                ? $"{attackerName} hits with {moveName} for {actualDamage} damage!"
                : $"{attackerName}'s {moveName} missed!",
            StateChanges: hit 
                ? new Dictionary<string, object> { ["damage"] = actualDamage }
                : new Dictionary<string, object>(),
            Calculations: $"Base damage: {baseDamage}, Hit roll: {hitRoll}/{accuracy}, Final damage: {actualDamage}"
        );

        await LogMechanicsEvent("attack_resolved", result, cancellationToken);
        return result;
    }

    public async Task<MechanicsResult> UseItemAsync(
        string itemName,
        string targetName,
        State.PlayerState playerState,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Using item: {Item} on {Target}", itemName, targetName);

        if (!playerState.Inventory.Contains(itemName))
        {
            return new MechanicsResult(
                Action: "Use Item",
                Success: false,
                Description: $"You don't have {itemName} in your inventory.",
                StateChanges: new Dictionary<string, object>(),
                Calculations: "Item not found in inventory"
            );
        }

        var effect = GetItemEffect(itemName);
        var result = new MechanicsResult(
            Action: "Use Item",
            Success: true,
            Description: $"Used {itemName}. {effect.Description}",
            StateChanges: effect.StateChanges,
            Calculations: $"Item effect: {effect.Description}"
        );

        await LogMechanicsEvent("item_used", result, cancellationToken);
        return result;
    }

    public async Task<MechanicsResult> PerformSkillCheckAsync(
        string skillName,
        int difficulty,
        State.PlayerState playerState,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Skill check: {Skill} (DC {Difficulty})", skillName, difficulty);

        var baseStat = GetRelevantStat(skillName, playerState);
        var roll = _randomService.Next(1, 21); // d20 roll
        var total = baseStat + roll;
        var success = total >= difficulty;

        var result = new MechanicsResult(
            Action: "Skill Check",
            Success: success,
            Description: success 
                ? $"Success! {skillName} check passed."
                : $"Failed {skillName} check.",
            StateChanges: new Dictionary<string, object>(),
            Calculations: $"Roll: {roll} + {baseStat} = {total} vs DC {difficulty}"
        );

        await LogMechanicsEvent("skill_check", result, cancellationToken);
        return result;
    }

    public async Task<MechanicsResult> GrantExperienceAsync(
        int experienceAmount,
        State.PlayerState playerState,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Granting {XP} experience to {Player}", experienceAmount, playerState.Name);

        var newExperience = playerState.Experience + experienceAmount;
        var experienceToNextLevel = CalculateExperienceToNextLevel(playerState.Level);
        var leveledUp = newExperience >= experienceToNextLevel;
        
        var stateChanges = new Dictionary<string, object>
        {
            ["experience"] = newExperience
        };

        var description = $"Gained {experienceAmount} experience!";

        if (leveledUp)
        {
            var newLevel = playerState.Level + 1;
            var statBoosts = CalculateLevelUpStats(newLevel);
            
            stateChanges["level"] = newLevel;
            stateChanges["max_health"] = playerState.MaxHealth + statBoosts["health"];
            stateChanges["stats"] = statBoosts;
            
            description += $" Level up! Now level {newLevel}!";
        }

        var result = new MechanicsResult(
            Action: "Grant Experience",
            Success: true,
            Description: description,
            StateChanges: stateChanges,
            Calculations: $"XP: {playerState.Experience} + {experienceAmount} = {newExperience}"
        );

        await LogMechanicsEvent("experience_granted", result, cancellationToken);
        return result;
    }

    private int CalculateBaseDamage(string moveName, State.PlayerState attackerState)
    {
        var basePower = moveName.ToLower() switch
        {
            "tackle" => 40,
            "scratch" => 40,
            "bite" => 60,
            "slam" => 80,
            _ => 50
        };

        return (attackerState.Stats.GetValueOrDefault("Attack", 10) * basePower) / 50;
    }

    private int CalculateAccuracy(string moveName)
    {
        return moveName.ToLower() switch
        {
            "tackle" => 100,
            "scratch" => 100,
            "bite" => 100,
            "slam" => 75,
            _ => 85
        };
    }

    private int GetRelevantStat(string skillName, State.PlayerState playerState)
    {
        return skillName.ToLower() switch
        {
            "strength" => playerState.Stats.GetValueOrDefault("Attack", 10),
            "agility" => playerState.Stats.GetValueOrDefault("Speed", 10),
            "endurance" => playerState.Stats.GetValueOrDefault("Defense", 10),
            _ => playerState.Level
        };
    }

    private ItemEffect GetItemEffect(string itemName)
    {
        return itemName.ToLower() switch
        {
            "potion" => new ItemEffect("Restores 20 HP", new Dictionary<string, object> { ["health_restore"] = 20 }),
            "super potion" => new ItemEffect("Restores 50 HP", new Dictionary<string, object> { ["health_restore"] = 50 }),
            "antidote" => new ItemEffect("Cures poison", new Dictionary<string, object> { ["cure_poison"] = true }),
            _ => new ItemEffect("No effect", new Dictionary<string, object>())
        };
    }

    private int CalculateExperienceToNextLevel(int currentLevel)
    {
        return currentLevel * currentLevel * 100;
    }

    private Dictionary<string, int> CalculateLevelUpStats(int newLevel)
    {
        return new Dictionary<string, int>
        {
            ["health"] = 5 + _randomService.Next(0, 3),
            ["attack"] = 2 + _randomService.Next(0, 2),
            ["defense"] = 2 + _randomService.Next(0, 2),
            ["speed"] = 2 + _randomService.Next(0, 2)
        };
    }

    private async Task LogMechanicsEvent(string eventType, MechanicsResult result, CancellationToken cancellationToken)
    {
        await _eventLog.AppendEventAsync(
            GameEvent.Create(eventType, result.Description, 
                new Dictionary<string, object> 
                { 
                    ["action"] = result.Action,
                    ["success"] = result.Success,
                    ["calculations"] = result.Calculations 
                }),
            cancellationToken);
    }

    protected override PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.1, // Very low for deterministic calculations
                ["max_tokens"] = 1000
            }
        };
    }
}

public record MechanicsResult(
    string Action,
    bool Success,
    string Description,
    Dictionary<string, object> StateChanges,
    string Calculations);

public record ItemEffect(
    string Description,
    Dictionary<string, object> StateChanges);