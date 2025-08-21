using Microsoft.SemanticKernel;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing RPG-style Pokemon combat encounters
/// </summary>
public class CombatPhasePlugin
{
    private readonly IGameLogicService _gameLogicService;
    private readonly IGameStateRepository _gameStateRepo;
    private readonly JsonSerializerOptions _jsonOptions;

    public CombatPhasePlugin(IGameStateRepository gameStateRepo, IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("end_combat")]
    [Description("End the combat encounter and transition back to exploration")]
    public async Task EndCombat([Description("A summary of the combat encounter and the results")] string combatSummary)
    {
        Debug.WriteLine($"[CombatPhasePlugin] EndCombat called with summary: {combatSummary}");
        
        try
        {
            // Load the current game state
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Set the phase back to Exploration
            gameState.CurrentPhase = GamePhase.Exploration;
            
            // Save the combat summary in the phase change summary
            gameState.PhaseChangeSummary = combatSummary;
            
            // Clear the combat state since combat is ending
            gameState.CombatState = null;
            
            // Add the combat result to recent events for continuity
            gameState.RecentEvents.Add(new EventLog { TurnNumber = gameState.GameTurnNumber, EventDescription = $"Combat Ended: {combatSummary}" });
            
            // Update the last save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the updated game state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            Debug.WriteLine($"[CombatPhasePlugin] Successfully ended combat and transitioned to Exploration phase");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CombatPhasePlugin] Error ending combat: {ex.Message}");
            throw;
        }
    }

    [KernelFunction("make_skill_check")]
    [Description("Makes a skill check using standard RPG rules with the player's ability scores")]
    public async Task<string> MakeSkillCheck(
        [Description("Stat to use: Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma")] string statName,
        [Description("Difficulty Class (5=Very Easy, 8=Easy, 11=Medium, 14=Hard, 17=Very Hard, 20=Nearly Impossible)")] int difficultyClass,
        [Description("Whether the check has advantage (roll twice, take higher)")] bool advantage = false,
        [Description("Whether the check has disadvantage (roll twice, take lower)")] bool disadvantage = false,
        [Description("Additional modifier to add to the roll")] int modifier = 0)
    {
        Debug.WriteLine($"[CombatPhasePlugin] MakeSkillCheck called: stat={statName}, DC={difficultyClass}, adv={advantage}, dis={disadvantage}");

        try
        {
            var result = await _gameLogicService.MakeSkillCheckAsync(statName, difficultyClass, advantage, disadvantage, modifier);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.WriteLine($"[CombatPhasePlugin] Skill check error: {result.Error}");
                return JsonSerializer.Serialize(new { error = result.Error }, _jsonOptions);
            }

            var formattedResult = $"{result.Outcome} {result.StatName} check: {result.TotalRoll} vs DC {result.DifficultyClass} ({result.Margin:+#;-#;0})";

            Debug.WriteLine($"[CombatPhasePlugin] Skill check: {result.StatName} {result.TotalRoll} vs DC {result.DifficultyClass} = {(result.Success ? "SUCCESS" : "FAILURE")}");
            return formattedResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CombatPhasePlugin] Error in MakeSkillCheck: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    #region Core Required Functions from Todo List

    [KernelFunction("start_combat")]
    [Description("Initialize the combat state")]
    public async Task<string> StartCombat(
        [Description("Array of combatant IDs participating in combat")] string[] combatants,
        [Description("Combat encounter description or context")] string encounterDescription = "")
    {
        try
        {
            if (combatants == null || combatants.Length == 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "At least one combatant is required to start combat" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Create combat state object
            var combatState = new
            {
                combatants = combatants.ToList(),
                currentTurn = 0,
                turnOrder = new List<object>(), // Will be populated by roll_for_initiative
                round = 1,
                encounterDescription = encounterDescription,
                startedAt = DateTime.UtcNow.ToString("O"),
                combatEffects = new Dictionary<string, object>(),
                combatLog = new List<object>()
            };

            // Set phase to Combat and save combat state
            gameState.CurrentPhase = GamePhase.Combat;
            gameState.CombatState = combatState;
            gameState.PhaseChangeSummary = $"Combat started: {encounterDescription}";
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"Combat Started: {encounterDescription}" 
            });
            
            // Update save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Combat initialized successfully",
                combatants = combatants,
                encounterDescription = encounterDescription,
                phase = "Combat",
                note = "Use roll_for_initiative to determine turn order"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error starting combat: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("get_turn_order")]
    [Description("Return current combat turn sequence")]
    public async Task<string> GetTurnOrder()
    {
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            if (gameState.CurrentPhase != GamePhase.Combat)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Not currently in combat" 
                }, _jsonOptions);
            }

            if (gameState.CombatState == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Combat state not found" 
                }, _jsonOptions);
            }

            // Parse combat state
            var combatStateJson = JsonSerializer.Serialize(gameState.CombatState);
            var combatState = JsonSerializer.Deserialize<Dictionary<string, object>>(combatStateJson);
            
            var turnOrderElement = combatState.ContainsKey("turnOrder") 
                ? combatState["turnOrder"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<object>())
                : JsonSerializer.SerializeToElement(new List<object>());
            
            var turnOrder = turnOrderElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<object>>(turnOrderElement) ?? new List<object>()
                : new List<object>();

            var currentTurn = combatState.ContainsKey("currentTurn") ? 
                ((JsonElement)combatState["currentTurn"]).GetInt32() : 0;
            var round = combatState.ContainsKey("round") ? 
                ((JsonElement)combatState["round"]).GetInt32() : 1;

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                turnOrder = turnOrder,
                currentTurn = currentTurn,
                round = round,
                totalCombatants = turnOrder.Count
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error getting turn order: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("make_attack_roll")]
    [Description("Core D&D dice resolution for attacks")]
    public async Task<string> MakeAttackRoll(
        [Description("Attacker's attack bonus modifier")] int attackBonus,
        [Description("Target's Armor Class")] int targetAC,
        [Description("Whether the attack has advantage")] bool advantage = false,
        [Description("Whether the attack has disadvantage")] bool disadvantage = false)
    {
        try
        {
            // Roll 1d20 for attack
            var roll1 = await _gameLogicService.RollDiceAsync(20, 1);
            if (!roll1.Success)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = roll1.Error 
                }, _jsonOptions);
            }

            var attackRoll = roll1.Total;
            var finalRoll = attackRoll;
            
            // Handle advantage/disadvantage
            if (advantage && !disadvantage)
            {
                var roll2 = await _gameLogicService.RollDiceAsync(20, 1);
                if (roll2.Success)
                {
                    finalRoll = Math.Max(attackRoll, roll2.Total);
                }
            }
            else if (disadvantage && !advantage)
            {
                var roll2 = await _gameLogicService.RollDiceAsync(20, 1);
                if (roll2.Success)
                {
                    finalRoll = Math.Min(attackRoll, roll2.Total);
                }
            }

            var totalAttackRoll = finalRoll + attackBonus;
            var isHit = totalAttackRoll >= targetAC;
            var isCriticalHit = finalRoll == 20;
            var isCriticalMiss = finalRoll == 1;
            
            var rollType = advantage && !disadvantage ? "advantage" : 
                          disadvantage && !advantage ? "disadvantage" : "normal";

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                attackRoll = new
                {
                    naturalRoll = finalRoll,
                    attackBonus = attackBonus,
                    totalRoll = totalAttackRoll,
                    targetAC = targetAC,
                    isHit = isHit,
                    isCriticalHit = isCriticalHit,
                    isCriticalMiss = isCriticalMiss,
                    rollType = rollType,
                    margin = totalAttackRoll - targetAC
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error making attack roll: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("make_saving_throw")]
    [Description("Core D&D dice resolution for saves")]
    public async Task<string> MakeSavingThrow(
        [Description("Saving throw bonus modifier")] int saveBonus,
        [Description("Difficulty Class for the save")] int difficultyClass,
        [Description("Type of saving throw (Strength, Dexterity, Constitution, etc.)")] string saveType = "General",
        [Description("Whether the save has advantage")] bool advantage = false,
        [Description("Whether the save has disadvantage")] bool disadvantage = false)
    {
        try
        {
            if (difficultyClass < 1 || difficultyClass > 30)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Difficulty Class must be between 1 and 30" 
                }, _jsonOptions);
            }

            // Roll 1d20 for save
            var roll1 = await _gameLogicService.RollDiceAsync(20, 1);
            if (!roll1.Success)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = roll1.Error 
                }, _jsonOptions);
            }

            var saveRoll = roll1.Total;
            var finalRoll = saveRoll;
            
            // Handle advantage/disadvantage
            if (advantage && !disadvantage)
            {
                var roll2 = await _gameLogicService.RollDiceAsync(20, 1);
                if (roll2.Success)
                {
                    finalRoll = Math.Max(saveRoll, roll2.Total);
                }
            }
            else if (disadvantage && !advantage)
            {
                var roll2 = await _gameLogicService.RollDiceAsync(20, 1);
                if (roll2.Success)
                {
                    finalRoll = Math.Min(saveRoll, roll2.Total);
                }
            }

            var totalSaveRoll = finalRoll + saveBonus;
            var isSuccess = totalSaveRoll >= difficultyClass;
            var isCriticalSuccess = finalRoll == 20;
            var isCriticalFailure = finalRoll == 1;
            
            var rollType = advantage && !disadvantage ? "advantage" : 
                          disadvantage && !advantage ? "disadvantage" : "normal";

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                savingThrow = new
                {
                    naturalRoll = finalRoll,
                    saveBonus = saveBonus,
                    totalRoll = totalSaveRoll,
                    difficultyClass = difficultyClass,
                    saveType = saveType,
                    isSuccess = isSuccess,
                    isCriticalSuccess = isCriticalSuccess,
                    isCriticalFailure = isCriticalFailure,
                    rollType = rollType,
                    margin = totalSaveRoll - difficultyClass
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error making saving throw: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("apply_damage")]
    [Description("Modify character state as result of damage")]
    public async Task<string> ApplyDamage(
        [Description("Target entity ID (character, NPC, or creature)")] string targetId,
        [Description("Amount of damage to apply")] int damageAmount,
        [Description("Type of damage (physical, fire, cold, etc.)")] string damageType = "physical",
        [Description("Source of the damage (for logging)")] string damageSource = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Target ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (damageAmount < 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Damage amount cannot be negative. Use healing functions for restoring HP." 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Check if target exists in world entities or is the player
            object target = null;
            bool isPlayer = false;
            
            if (targetId.ToLower() == "player" || (gameState.Player != null && gameState.Player.Name == targetId))
            {
                target = gameState.Player;
                isPlayer = true;
            }
            else if (gameState.WorldEntities.ContainsKey(targetId))
            {
                target = gameState.WorldEntities[targetId];
            }
            else
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Target '{targetId}' not found" 
                }, _jsonOptions);
            }

            // Get current HP from the target
            var targetJson = JsonSerializer.Serialize(target);
            var targetData = JsonSerializer.Deserialize<Dictionary<string, object>>(targetJson);
            
            var currentHP = 0;
            var maxHP = 100; // Default max HP
            
            if (targetData.ContainsKey("currentHP"))
            {
                var hpElement = targetData["currentHP"] as JsonElement? ?? JsonSerializer.SerializeToElement(0);
                currentHP = hpElement.ValueKind == JsonValueKind.Number ? hpElement.GetInt32() : 0;
            }
            else if (targetData.ContainsKey("hitPoints"))
            {
                var hpElement = targetData["hitPoints"] as JsonElement? ?? JsonSerializer.SerializeToElement(0);
                currentHP = hpElement.ValueKind == JsonValueKind.Number ? hpElement.GetInt32() : 100;
            }
            
            if (targetData.ContainsKey("maxHP"))
            {
                var maxHpElement = targetData["maxHP"] as JsonElement? ?? JsonSerializer.SerializeToElement(100);
                maxHP = maxHpElement.ValueKind == JsonValueKind.Number ? maxHpElement.GetInt32() : 100;
            }

            // Apply damage
            var newHP = Math.Max(0, currentHP - damageAmount);
            var actualDamage = currentHP - newHP;
            var isDead = newHP <= 0;
            
            // Update target's HP
            targetData["currentHP"] = newHP;
            if (isDead && !targetData.ContainsKey("isDead"))
            {
                targetData["isDead"] = true;
                targetData["deathTimestamp"] = DateTime.UtcNow.ToString("O");
            }
            
            // Update damage history
            if (!targetData.ContainsKey("damageHistory"))
                targetData["damageHistory"] = new List<object>();
            
            var historyElement = targetData["damageHistory"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<object>());
            var damageHistory = historyElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<object>>(historyElement) ?? new List<object>()
                : new List<object>();
            
            damageHistory.Add(new {
                damage = actualDamage,
                damageType = damageType,
                source = damageSource,
                previousHP = currentHP,
                newHP = newHP,
                timestamp = DateTime.UtcNow.ToString("O")
            });
            
            targetData["damageHistory"] = damageHistory;
            
            // Save back to game state
            if (isPlayer)
            {
                gameState.Player = JsonSerializer.Deserialize<BasicPlayerState>(JsonSerializer.Serialize(targetData));
            }
            else
            {
                gameState.WorldEntities[targetId] = targetData;
            }
            
            await _gameStateRepo.SaveStateAsync(gameState);

            var targetName = targetData.ContainsKey("name") ? targetData["name"]?.ToString() : targetId;

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"{targetName} took {actualDamage} {damageType} damage",
                targetId = targetId,
                targetName = targetName,
                damageApplied = new
                {
                    damage = actualDamage,
                    damageType = damageType,
                    source = damageSource,
                    previousHP = currentHP,
                    newHP = newHP,
                    maxHP = maxHP,
                    isDead = isDead
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error applying damage: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("apply_condition")]
    [Description("Modify character state with status effects")]
    public async Task<string> ApplyCondition(
        [Description("Target entity ID (character, NPC, or creature)")] string targetId,
        [Description("Condition name (stunned, poisoned, blessed, etc.)")] string conditionName,
        [Description("Duration in rounds/turns (-1 for permanent until removed)")] int duration = 1,
        [Description("Source of the condition (for logging)")] string conditionSource = "",
        [Description("Additional condition details or effects")] string conditionDetails = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Target ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(conditionName))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Condition name cannot be null or empty" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Check if target exists in world entities or is the player
            object target = null;
            bool isPlayer = false;
            
            if (targetId.ToLower() == "player" || (gameState.Player != null && gameState.Player.Name == targetId))
            {
                target = gameState.Player;
                isPlayer = true;
            }
            else if (gameState.WorldEntities.ContainsKey(targetId))
            {
                target = gameState.WorldEntities[targetId];
            }
            else
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Target '{targetId}' not found" 
                }, _jsonOptions);
            }

            // Get target data
            var targetJson = JsonSerializer.Serialize(target);
            var targetData = JsonSerializer.Deserialize<Dictionary<string, object>>(targetJson);
            
            // Initialize active conditions if not present
            if (!targetData.ContainsKey("activeConditions"))
                targetData["activeConditions"] = new Dictionary<string, object>();
            
            var conditionsElement = targetData["activeConditions"] as JsonElement? ?? JsonSerializer.SerializeToElement(new Dictionary<string, object>());
            var activeConditions = conditionsElement.ValueKind == JsonValueKind.Object 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(conditionsElement) ?? new Dictionary<string, object>()
                : new Dictionary<string, object>();
            
            // Check if condition already exists
            var wasAlreadyActive = activeConditions.ContainsKey(conditionName);
            
            // Add or update condition
            var condition = new
            {
                name = conditionName,
                duration = duration,
                source = conditionSource,
                details = conditionDetails,
                appliedAt = DateTime.UtcNow.ToString("O"),
                remainingDuration = duration
            };
            
            activeConditions[conditionName] = condition;
            targetData["activeConditions"] = activeConditions;
            
            // Update condition history
            if (!targetData.ContainsKey("conditionHistory"))
                targetData["conditionHistory"] = new List<object>();
            
            var historyElement = targetData["conditionHistory"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<object>());
            var conditionHistory = historyElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<object>>(historyElement) ?? new List<object>()
                : new List<object>();
            
            conditionHistory.Add(new {
                action = wasAlreadyActive ? "updated" : "applied",
                conditionName = conditionName,
                duration = duration,
                source = conditionSource,
                timestamp = DateTime.UtcNow.ToString("O")
            });
            
            targetData["conditionHistory"] = conditionHistory;
            
            // Save back to game state
            if (isPlayer)
            {
                gameState.Player = JsonSerializer.Deserialize<BasicPlayerState>(JsonSerializer.Serialize(targetData));
            }
            else
            {
                gameState.WorldEntities[targetId] = targetData;
            }
            
            await _gameStateRepo.SaveStateAsync(gameState);

            var targetName = targetData.ContainsKey("name") ? targetData["name"]?.ToString() : targetId;
            var actionText = wasAlreadyActive ? "updated" : "applied";

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Condition '{conditionName}' {actionText} to {targetName}",
                targetId = targetId,
                targetName = targetName,
                conditionApplied = new
                {
                    conditionName = conditionName,
                    duration = duration,
                    source = conditionSource,
                    details = conditionDetails,
                    wasAlreadyActive = wasAlreadyActive,
                    totalActiveConditions = activeConditions.Count
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error applying condition: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    #endregion
}