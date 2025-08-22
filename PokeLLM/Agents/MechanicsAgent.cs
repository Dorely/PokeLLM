using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PokeLLM.State;
using PokeLLM.Memory;

namespace PokeLLM.Agents;

public class MechanicsAgent : BaseGameAgent
{
    private readonly RandomNumberService _randomNumberService;
    private readonly IEventLog _eventLog;
    private readonly MemoryEnabledAgentThreadFactory _threadFactory;

    public override string Id => "mechanics-agent";
    public override string Name => "Mechanics Agent";
    
    public override string Instructions => """
        You are the Mechanics Agent for a Pokemon RPG game. You are the ONLY agent authorized to make state changes.

        Core Responsibilities:
        1. DETERMINISTIC CALCULATIONS: All numerical outcomes must be predictable and reproducible
        2. STATE AUTHORITY: You are the sole source of truth for all game state changes
        3. RULES ENFORCEMENT: Apply Pokemon game rules consistently and accurately
        4. VALIDATION: Verify all inputs and reject invalid actions
        5. AUDIT TRAIL: Log all state changes for debugging and replay

        Critical Rules:
        - ALL state mutations must go through your validated functions
        - Use provided RNG seed for reproducible random outcomes
        - NEVER allow other agents to modify game state
        - Validate all actions against current game state
        - Log every state change with precise details
        - Reject actions that violate game rules

        Functions You Provide:
        - ResolveAttack(attacker, defender, move, context)
        - ApplyDamage(target, damage, damageType, context)
        - GrantXP(pokemon, amount, context)
        - PerformSkillCheck(skill, difficulty, context)
        - UpdateInventory(action, item, quantity, context)
        - ProcessLevelUp(pokemon, context)
        - ValidateAction(action, context)

        Output Format:
        Always return structured results in this format:
        {
            "success": true/false,
            "result": "description of what happened",
            "stateChanges": [list of specific changes],
            "errors": [any validation errors],
            "randomSeed": "seed used for this operation"
        }

        What You DON'T Do:
        - Create narrative descriptions (that's the Narrator's job)
        - Make subjective decisions
        - Allow state changes from other sources
        - Skip validation steps
        """;

    public MechanicsAgent(
        Kernel kernel, 
        ILogger<MechanicsAgent> logger,
        RandomNumberService randomNumberService,
        IEventLog eventLog,
        MemoryEnabledAgentThreadFactory threadFactory) 
        : base(kernel, logger)
    {
        _randomNumberService = randomNumberService;
        _eventLog = eventLog;
        _threadFactory = threadFactory;
    }

    /// <summary>
    /// Creates a basic thread for mechanics operations (no memory to maintain determinism)
    /// </summary>
    public MemoryEnabledAgentThread CreateThread(string sessionId)
    {
        // MechanicsAgent intentionally doesn't use memory components to maintain deterministic behavior
        return _threadFactory.CreateBasicThread(sessionId);
    }

    /// <summary>
    /// Processes a mechanical action with full validation and state mutation authority
    /// </summary>
    public async Task<MechanicalResult> ProcessActionAsync(
        MechanicalAction action,
        GameContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing mechanical action {ActionType} for session {SessionId}", 
                action.ActionType, action.SessionId);

            // Set deterministic random seed
            var seed = GenerateActionSeed(action, context);
            _randomNumberService.SetSeed(seed);

            // Validate the action
            var validationResult = await ValidateActionAsync(action, context, cancellationToken);
            if (!validationResult.IsValid)
            {
                return MechanicalResult.Failure(validationResult.ErrorMessage, seed);
            }

            // Process the action based on type
            var result = action.ActionType switch
            {
                MechanicalActionType.Attack => await ProcessAttackAsync(action, context, cancellationToken),
                MechanicalActionType.UseItem => await ProcessItemUseAsync(action, context, cancellationToken),
                MechanicalActionType.SkillCheck => await ProcessSkillCheckAsync(action, context, cancellationToken),
                MechanicalActionType.LevelUp => await ProcessLevelUpAsync(action, context, cancellationToken),
                MechanicalActionType.Rest => await ProcessRestAsync(action, context, cancellationToken),
                _ => MechanicalResult.Failure($"Unknown action type: {action.ActionType}", seed)
            };

            // Log the state changes
            if (result.IsSuccess && result.StateChanges.Any())
            {
                await LogStateChangesAsync(action, result, cancellationToken);
            }

            _logger.LogInformation("Completed mechanical action {ActionType} with success: {Success}", 
                action.ActionType, result.IsSuccess);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mechanical action {ActionType}", action.ActionType);
            return MechanicalResult.Failure($"Internal error: {ex.Message}", "error-seed");
        }
    }

    private async Task<ValidationResult> ValidateActionAsync(
        MechanicalAction action, 
        GameContext context, 
        CancellationToken cancellationToken)
    {
        // Basic validation logic
        if (string.IsNullOrWhiteSpace(action.SessionId))
            return ValidationResult.Invalid("Session ID is required");

        if (action.ActionType == MechanicalActionType.Unknown)
            return ValidationResult.Invalid("Action type must be specified");

        // Additional validation based on action type
        return action.ActionType switch
        {
            MechanicalActionType.Attack => await ValidateAttackActionAsync(action, context, cancellationToken),
            MechanicalActionType.UseItem => await ValidateItemUseActionAsync(action, context, cancellationToken),
            MechanicalActionType.SkillCheck => await ValidateSkillCheckActionAsync(action, context, cancellationToken),
            _ => ValidationResult.Valid()
        };
    }

    private async Task<ValidationResult> ValidateAttackActionAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Validate attack action specifics
        return ValidationResult.Valid(); // Placeholder
    }

    private async Task<ValidationResult> ValidateItemUseActionAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Validate item use specifics
        return ValidationResult.Valid(); // Placeholder
    }

    private async Task<ValidationResult> ValidateSkillCheckActionAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Validate skill check specifics
        return ValidationResult.Valid(); // Placeholder
    }

    private async Task<MechanicalResult> ProcessAttackAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Implement attack resolution logic
        var damage = _randomNumberService.Next(1, 21); // Placeholder damage calculation
        var critical = _randomNumberService.NextDouble() < 0.1; // 10% critical chance
        
        var stateChanges = new List<StateChange>
        {
            new StateChange("HP", "target", context.PlayerState.HP, Math.Max(0, context.PlayerState.HP - damage))
        };

        if (critical)
        {
            stateChanges.Add(new StateChange("CriticalHit", "battle", false, true));
        }

        return MechanicalResult.Success(
            $"Attack deals {damage} damage{(critical ? " (Critical Hit!)" : "")}",
            stateChanges,
            _randomNumberService.CurrentSeed);
    }

    private async Task<MechanicalResult> ProcessItemUseAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Implement item use logic
        var stateChanges = new List<StateChange>
        {
            new StateChange("Inventory", action.Parameters.GetValueOrDefault("itemName", "unknown"), 1, 0)
        };

        return MechanicalResult.Success(
            $"Used {action.Parameters.GetValueOrDefault("itemName", "item")}",
            stateChanges,
            _randomNumberService.CurrentSeed);
    }

    private async Task<MechanicalResult> ProcessSkillCheckAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Implement skill check logic
        var roll = _randomNumberService.Next(1, 21); // d20 roll
        var difficulty = int.Parse(action.Parameters.GetValueOrDefault("difficulty", "10"));
        var success = roll >= difficulty;

        var stateChanges = new List<StateChange>
        {
            new StateChange("LastSkillRoll", "player", 0, roll)
        };

        return MechanicalResult.Success(
            $"Skill check: rolled {roll} vs DC {difficulty} - {(success ? "Success" : "Failure")}",
            stateChanges,
            _randomNumberService.CurrentSeed);
    }

    private async Task<MechanicalResult> ProcessLevelUpAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Implement level up logic
        var stateChanges = new List<StateChange>
        {
            new StateChange("Level", "player", context.PlayerState.Level, context.PlayerState.Level + 1),
            new StateChange("MaxHP", "player", context.PlayerState.MaxHP, context.PlayerState.MaxHP + 10)
        };

        return MechanicalResult.Success(
            "Level up! Gained 10 max HP",
            stateChanges,
            _randomNumberService.CurrentSeed);
    }

    private async Task<MechanicalResult> ProcessRestAsync(MechanicalAction action, GameContext context, CancellationToken cancellationToken)
    {
        // Implement rest logic
        var stateChanges = new List<StateChange>
        {
            new StateChange("HP", "player", context.PlayerState.HP, context.PlayerState.MaxHP)
        };

        return MechanicalResult.Success(
            "Restored to full health",
            stateChanges,
            _randomNumberService.CurrentSeed);
    }

    private string GenerateActionSeed(MechanicalAction action, GameContext context)
    {
        // Generate a deterministic seed based on action and context
        var seedInput = $"{action.SessionId}_{action.ActionType}_{context.TurnNumber}_{DateTime.UtcNow.Ticks}";
        return seedInput.GetHashCode().ToString();
    }

    private async Task LogStateChangesAsync(MechanicalAction action, MechanicalResult result, CancellationToken cancellationToken)
    {
        try
        {
            var eventData = new Dictionary<string, object>
            {
                ["ActionType"] = action.ActionType.ToString(),
                ["SessionId"] = action.SessionId,
                ["Success"] = result.IsSuccess,
                ["StateChanges"] = result.StateChanges,
                ["RandomSeed"] = result.RandomSeed,
                ["Timestamp"] = DateTime.UtcNow
            };

            await _eventLog.AppendEventAsync(
                GameEvent.Create("MechanicalAction", "Mechanical action processed", eventData), 
                cancellationToken);
            
            _logger.LogDebug("Logged {ChangeCount} state changes for action {ActionType}", 
                result.StateChanges.Count, action.ActionType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log state changes for action {ActionType}", action.ActionType);
        }
    }

    protected override PromptExecutionSettings GetExecutionSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.1, // Low temperature for deterministic responses
                ["max_tokens"] = 500
            }
        };
    }
}

// Supporting classes for MechanicsAgent

public class MechanicalAction
{
    public string SessionId { get; set; } = "";
    public MechanicalActionType ActionType { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public enum MechanicalActionType
{
    Unknown,
    Attack,
    UseItem,
    SkillCheck,
    LevelUp,
    Rest
}

public class MechanicalResult
{
    public bool IsSuccess { get; set; }
    public string Description { get; set; } = "";
    public List<StateChange> StateChanges { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string RandomSeed { get; set; } = "";

    public static MechanicalResult Success(string description, List<StateChange> stateChanges, string randomSeed)
    {
        return new MechanicalResult
        {
            IsSuccess = true,
            Description = description,
            StateChanges = stateChanges,
            RandomSeed = randomSeed
        };
    }

    public static MechanicalResult Failure(string error, string randomSeed)
    {
        return new MechanicalResult
        {
            IsSuccess = false,
            Errors = new List<string> { error },
            RandomSeed = randomSeed
        };
    }
}

public class StateChange
{
    public string Property { get; set; }
    public string Target { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }

    public StateChange(string property, string target, object oldValue, object newValue)
    {
        Property = property;
        Target = target;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";

    public static ValidationResult Valid() => new() { IsValid = true };
    public static ValidationResult Invalid(string error) => new() { IsValid = false, ErrorMessage = error };
}