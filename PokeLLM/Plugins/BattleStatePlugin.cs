using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

public class BattleStatePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public BattleStatePlugin(IGameStateRepository repository)
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

    #region Battle Initialization

    [KernelFunction("start_battle")]
    [Description("Initialize a new battle with specified participants and conditions")]
    public async Task<string> StartBattle(
        [Description("Type of battle (Wild, Trainer, Gym, etc.)")] string battleType,
        [Description("JSON array of battle participants")] string participantsJson,
        [Description("Battlefield name/description")] string battlefieldName = "Standard Field",
        [Description("Weather conditions")] string weather = "Clear")
    {
        Debug.WriteLine($"[BattleStatePlugin] StartBattle called with type: {battleType}");

        if (await _repository.HasActiveBattleAsync())
        {
            return JsonSerializer.Serialize(new { error = "A battle is already active" }, _jsonOptions);
        }

        try
        {
            var participants = JsonSerializer.Deserialize<List<BattleParticipant>>(participantsJson, _jsonOptions);
            if (participants == null || !participants.Any())
            {
                return JsonSerializer.Serialize(new { error = "Invalid or empty participants list" }, _jsonOptions);
            }

            if (!Enum.TryParse<BattleType>(battleType, true, out var battleTypeEnum))
            {
                return JsonSerializer.Serialize(new { error = "Invalid battle type" }, _jsonOptions);
            }

            var battleState = new BattleState
            {
                IsActive = true,
                BattleType = battleTypeEnum,
                CurrentTurn = 1,
                CurrentPhase = BattlePhase.Initialize,
                BattleParticipants = participants,
                Battlefield = new Battlefield { Name = battlefieldName },
                Weather = new BattleWeather { Name = weather },
                VictoryConditions = GenerateDefaultVictoryConditions(battleTypeEnum),
                BattleLog = new List<BattleLogEntry>()
            };

            // Calculate initiative and set turn order
            CalculateInitiativeOrder(battleState);

            // Set up default relationships
            SetupDefaultRelationships(battleState);

            await _repository.StartBattleAsync(battleState);

            LogBattleEvent(battleState, "System", "Battle Started", new List<string>(), 
                $"Battle began: {battleType} on {battlefieldName}");

            var result = new
            {
                success = true,
                battleId = battleState.GetHashCode().ToString(),
                battleType = battleState.BattleType.ToString(),
                turnOrder = battleState.TurnOrder,
                participants = battleState.BattleParticipants.Count,
                currentPhase = battleState.CurrentPhase.ToString()
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error parsing participants: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("end_battle")]
    [Description("End the current battle and clean up battle state")]
    public async Task<string> EndBattle(
        [Description("Reason for ending battle")] string reason = "Battle concluded")
    {
        Debug.WriteLine($"[BattleStatePlugin] EndBattle called");

        if (!await _repository.HasActiveBattleAsync())
        {
            return JsonSerializer.Serialize(new { error = "No active battle to end" }, _jsonOptions);
        }

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState != null)
        {
            LogBattleEvent(gameState.BattleState, "System", "Battle Ended", new List<string>(), reason);
        }

        await _repository.EndBattleAsync();

        return JsonSerializer.Serialize(new { success = true, message = $"Battle ended: {reason}" }, _jsonOptions);
    }

    #endregion

    #region Battle State Queries

    [KernelFunction("get_battle_state")]
    [Description("Get the current battle state and all participant information")]
    public async Task<string> GetBattleState()
    {
        Debug.WriteLine($"[BattleStatePlugin] GetBattleState called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        return JsonSerializer.Serialize(gameState.BattleState, _jsonOptions);
    }

    [KernelFunction("get_participant_status")]
    [Description("Get detailed status of a specific battle participant")]
    public async Task<string> GetParticipantStatus(
        [Description("ID of the participant to check")] string participantId)
    {
        Debug.WriteLine($"[BattleStatePlugin] GetParticipantStatus called for: {participantId}");

        var gameState = await _repository.LoadLatestStateAsync();
        var participant = gameState?.BattleState?.BattleParticipants
            .FirstOrDefault(p => p.Id.Equals(participantId, StringComparison.OrdinalIgnoreCase));

        if (participant == null)
        {
            return JsonSerializer.Serialize(new { error = "Participant not found" }, _jsonOptions);
        }

        var status = new
        {
            participant.Id,
            participant.Name,
            participant.Type,
            participant.Faction,
            participant.Position,
            participant.Initiative,
            participant.HasActed,
            participant.IsDefeated,
            pokemon = participant.Pokemon != null ? new
            {
                participant.Pokemon.CurrentVigor,
                participant.Pokemon.MaxVigor,
                vigorPercentage = (participant.Pokemon.CurrentVigor * 100) / Math.Max(1, participant.Pokemon.MaxVigor),
                statusEffects = participant.Pokemon.StatusEffects.Count,
                participant.Pokemon.UsedMoves,
                participant.Pokemon.LastAction
            } : null,
            trainer = participant.Trainer != null ? new
            {
                participant.Trainer.Name,
                participant.Trainer.CanEscape,
                participant.Trainer.HasActed,
                remainingPokemon = participant.Trainer.RemainingPokemon.Count,
                conditions = participant.Trainer.Conditions.Count
            } : null,
            relationships = participant.Relationships
        };

        return JsonSerializer.Serialize(status, _jsonOptions);
    }

    [KernelFunction("get_turn_order")]
    [Description("Get the current turn order and whose turn it is")]
    public async Task<string> GetTurnOrder()
    {
        Debug.WriteLine($"[BattleStatePlugin] GetTurnOrder called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var battleState = gameState.BattleState;
        var result = new
        {
            currentTurn = battleState.CurrentTurn,
            currentPhase = battleState.CurrentPhase.ToString(),
            currentActor = battleState.CurrentActorId,
            turnOrder = battleState.TurnOrder,
            participants = battleState.BattleParticipants.Select(p => new
            {
                p.Id,
                p.Name,
                p.Initiative,
                p.HasActed,
                p.IsDefeated
            }).OrderByDescending(p => p.Initiative)
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Battle Actions

    [KernelFunction("execute_battle_action")]
    [Description("Execute a battle action for a participant")]
    public async Task<string> ExecuteBattleAction(
        [Description("JSON string with battle action data")] string actionJson)
    {
        Debug.WriteLine($"[BattleStatePlugin] ExecuteBattleAction called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null || !gameState.BattleState.IsActive)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        try
        {
            var action = JsonSerializer.Deserialize<BattleAction>(actionJson, _jsonOptions);
            if (action == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid action data" }, _jsonOptions);
            }

            var results = await ProcessBattleAction(gameState.BattleState, action);
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { success = true, results = results }, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error parsing action: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("advance_battle_phase")]
    [Description("Advance the battle to the next phase")]
    public async Task<string> AdvanceBattlePhase()
    {
        Debug.WriteLine($"[BattleStatePlugin] AdvanceBattlePhase called");

        await _repository.UpdateBattleStateAsync(battleState =>
        {
            battleState.CurrentPhase = battleState.CurrentPhase switch
            {
                BattlePhase.Initialize => BattlePhase.SelectAction,
                BattlePhase.SelectAction => BattlePhase.ResolveActions,
                BattlePhase.ResolveActions => BattlePhase.ApplyEffects,
                BattlePhase.ApplyEffects => BattlePhase.CheckVictory,
                BattlePhase.CheckVictory => BattlePhase.EndTurn,
                BattlePhase.EndTurn => BattlePhase.SelectAction,
                BattlePhase.BattleEnd => BattlePhase.BattleEnd,
                _ => BattlePhase.SelectAction
            };

            if (battleState.CurrentPhase == BattlePhase.SelectAction)
            {
                battleState.CurrentTurn++;
                // Reset acted flags for new turn
                foreach (var participant in battleState.BattleParticipants)
                {
                    participant.HasActed = false;
                    if (participant.Trainer != null)
                    {
                        participant.Trainer.HasActed = false;
                    }
                }
            }

            LogBattleEvent(battleState, "System", "Phase Advanced", new List<string>(), 
                $"Battle phase changed to {battleState.CurrentPhase}");
        });

        var gameState = await _repository.LoadLatestStateAsync();
        var result = new
        {
            success = true,
            currentTurn = gameState?.BattleState?.CurrentTurn,
            currentPhase = gameState?.BattleState?.CurrentPhase.ToString()
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Participant Management

    [KernelFunction("add_battle_participant")]
    [Description("Add a new participant to the current battle")]
    public async Task<string> AddBattleParticipant(
        [Description("JSON string with participant data")] string participantJson)
    {
        Debug.WriteLine($"[BattleStatePlugin] AddBattleParticipant called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null || !gameState.BattleState.IsActive)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        try
        {
            var participant = JsonSerializer.Deserialize<BattleParticipant>(participantJson, _jsonOptions);
            if (participant == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid participant data" }, _jsonOptions);
            }

            await _repository.UpdateBattleStateAsync(battleState =>
            {
                battleState.BattleParticipants.Add(participant);
                // Recalculate turn order
                CalculateInitiativeOrder(battleState);
                
                LogBattleEvent(battleState, "System", "Participant Added", new List<string>(), 
                    $"{participant.Name} joined the battle");
            });

            return JsonSerializer.Serialize(new { success = true, participantId = participant.Id }, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error parsing participant: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("remove_battle_participant")]
    [Description("Remove a participant from the current battle")]
    public async Task<string> RemoveBattleParticipant(
        [Description("ID of the participant to remove")] string participantId)
    {
        Debug.WriteLine($"[BattleStatePlugin] RemoveBattleParticipant called for: {participantId}");

        await _repository.UpdateBattleStateAsync(battleState =>
        {
            var participant = battleState.BattleParticipants
                .FirstOrDefault(p => p.Id.Equals(participantId, StringComparison.OrdinalIgnoreCase));
            
            if (participant != null)
            {
                battleState.BattleParticipants.Remove(participant);
                battleState.TurnOrder.Remove(participantId);
                
                LogBattleEvent(battleState, "System", "Participant Removed", new List<string>(), 
                    $"{participant.Name} left the battle");
            }
        });

        return JsonSerializer.Serialize(new { success = true }, _jsonOptions);
    }

    [KernelFunction("update_participant_vigor")]
    [Description("Update a Pokemon participant's vigor (health)")]
    public async Task<string> UpdateParticipantVigor(
        [Description("ID of the participant")] string participantId,
        [Description("New current vigor value")] int newVigor,
        [Description("Reason for change (optional)")] string reason = "")
    {
        Debug.WriteLine($"[BattleStatePlugin] UpdateParticipantVigor called for: {participantId}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var participant = gameState.BattleState.BattleParticipants
            .FirstOrDefault(p => p.Id.Equals(participantId, StringComparison.OrdinalIgnoreCase));

        if (participant?.Pokemon == null)
        {
            return JsonSerializer.Serialize(new { error = "Participant not found or not a Pokemon" }, _jsonOptions);
        }

        var oldVigor = participant.Pokemon.CurrentVigor;
        participant.Pokemon.CurrentVigor = Math.Max(0, Math.Min(newVigor, participant.Pokemon.MaxVigor));

        // Check if Pokemon is defeated
        if (participant.Pokemon.CurrentVigor == 0 && !participant.IsDefeated)
        {
            participant.IsDefeated = true;
            LogBattleEvent(gameState.BattleState, "System", "Pokemon Defeated", new List<string> { participantId }, 
                $"{participant.Name} was defeated");
        }

        await _repository.SaveStateAsync(gameState);

        var change = participant.Pokemon.CurrentVigor - oldVigor;
        var result = new
        {
            success = true,
            participantId = participantId,
            oldVigor = oldVigor,
            newVigor = participant.Pokemon.CurrentVigor,
            change = change,
            isDefeated = participant.IsDefeated,
            reason = reason
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Status Effects and Conditions

    [KernelFunction("apply_status_effect")]
    [Description("Apply a status effect to a battle participant")]
    public async Task<string> ApplyStatusEffect(
        [Description("ID of the target participant")] string targetId,
        [Description("JSON string with status effect data")] string statusEffectJson)
    {
        Debug.WriteLine($"[BattleStatePlugin] ApplyStatusEffect called for: {targetId}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        try
        {
            var statusEffect = JsonSerializer.Deserialize<StatusEffect>(statusEffectJson, _jsonOptions);
            if (statusEffect == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid status effect data" }, _jsonOptions);
            }

            var participant = gameState.BattleState.BattleParticipants
                .FirstOrDefault(p => p.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));

            if (participant?.Pokemon == null)
            {
                return JsonSerializer.Serialize(new { error = "Target not found or not a Pokemon" }, _jsonOptions);
            }

            // Remove existing effect of same name
            participant.Pokemon.StatusEffects.RemoveAll(e => e.Name.Equals(statusEffect.Name, StringComparison.OrdinalIgnoreCase));
            
            // Add new effect
            participant.Pokemon.StatusEffects.Add(statusEffect);

            LogBattleEvent(gameState.BattleState, "System", "Status Effect Applied", new List<string> { targetId }, 
                $"{participant.Name} is now {statusEffect.Name}");

            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { success = true, effectName = statusEffect.Name }, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error parsing status effect: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("remove_status_effect")]
    [Description("Remove a status effect from a battle participant")]
    public async Task<string> RemoveStatusEffect(
        [Description("ID of the target participant")] string targetId,
        [Description("Name of the status effect to remove")] string effectName)
    {
        Debug.WriteLine($"[BattleStatePlugin] RemoveStatusEffect called for: {targetId}, effect: {effectName}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var participant = gameState.BattleState.BattleParticipants
            .FirstOrDefault(p => p.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));

        if (participant?.Pokemon == null)
        {
            return JsonSerializer.Serialize(new { error = "Target not found or not a Pokemon" }, _jsonOptions);
        }

        var removed = participant.Pokemon.StatusEffects.RemoveAll(e => 
            e.Name.Equals(effectName, StringComparison.OrdinalIgnoreCase)) > 0;

        if (removed)
        {
            LogBattleEvent(gameState.BattleState, "System", "Status Effect Removed", new List<string> { targetId }, 
                $"{participant.Name} is no longer {effectName}");
        }

        await _repository.SaveStateAsync(gameState);

        return JsonSerializer.Serialize(new { success = true, removed = removed }, _jsonOptions);
    }

    #endregion

    #region Battle Log and History

    [KernelFunction("get_battle_log")]
    [Description("Get the battle log with optional filtering")]
    public async Task<string> GetBattleLog(
        [Description("Number of recent entries to return (0 = all)")] int count = 10,
        [Description("Filter by actor ID (optional)")] string actorFilter = "")
    {
        Debug.WriteLine($"[BattleStatePlugin] GetBattleLog called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var log = gameState.BattleState.BattleLog.AsEnumerable();

        if (!string.IsNullOrEmpty(actorFilter))
        {
            log = log.Where(entry => entry.ActorId.Equals(actorFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (count > 0)
        {
            log = log.TakeLast(count);
        }

        return JsonSerializer.Serialize(log.ToList(), _jsonOptions);
    }

    #endregion

    #region Utility Functions

    [KernelFunction("create_pokemon_participant")]
    [Description("Create a Pokemon battle participant from a Pokemon in the trainer's team")]
    public async Task<string> CreatePokemonParticipant(
        [Description("Name of the Pokemon from trainer's team")] string pokemonName,
        [Description("Faction (Player, Enemy, Neutral)")] string faction = "Player",
        [Description("X position on battlefield")] int x = 0,
        [Description("Y position on battlefield")] int y = 0)
    {
        Debug.WriteLine($"[BattleStatePlugin] CreatePokemonParticipant called for: {pokemonName}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
        {
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);
        }

        // Find the Pokemon in the trainer's team
        var pokemon = gameState.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase)) ??
            gameState.PokemonTeam.BoxedPokemon
            .FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (pokemon == null)
        {
            return JsonSerializer.Serialize(new { error = "Pokemon not found in trainer's team" }, _jsonOptions);
        }

        var participant = new BattleParticipant
        {
            Id = Guid.NewGuid().ToString(),
            Name = pokemon.Name,
            Type = faction.ToLower() == "player" ? ParticipantType.PlayerPokemon : ParticipantType.EnemyPokemon,
            Faction = faction,
            Pokemon = new BattlePokemon
            {
                PokemonData = pokemon,
                CurrentVigor = pokemon.CurrentVigor,
                MaxVigor = pokemon.MaxVigor,
                StatusEffects = new List<StatusEffect>(),
                TemporaryStats = new Dictionary<string, int>(),
                UsedMoves = new List<string>()
            },
            Position = new BattlePosition { X = x, Y = y },
            Relationships = new Dictionary<string, RelationshipType>()
        };

        return JsonSerializer.Serialize(participant, _jsonOptions);
    }

    [KernelFunction("create_trainer_participant")]
    [Description("Create a trainer battle participant")]
    public async Task<string> CreateTrainerParticipant(
        [Description("Name of the trainer")] string trainerName,
        [Description("Faction (Player, Enemy, Neutral)")] string faction = "Enemy",
        [Description("JSON array of Pokemon for this trainer")] string pokemonListJson = "[]",
        [Description("Can this trainer escape?")] bool canEscape = true)
    {
        Debug.WriteLine($"[BattleStatePlugin] CreateTrainerParticipant called for: {trainerName}");

        try
        {
            var pokemonList = JsonSerializer.Deserialize<List<Pokemon>>(pokemonListJson, _jsonOptions) ?? new List<Pokemon>();

            var participant = new BattleParticipant
            {
                Id = Guid.NewGuid().ToString(),
                Name = trainerName,
                Type = faction.ToLower() == "player" ? ParticipantType.PlayerTrainer : ParticipantType.EnemyTrainer,
                Faction = faction,
                Trainer = new BattleTrainer
                {
                    Name = trainerName,
                    Stats = new Stats(), // Default stats
                    Conditions = new List<ActiveCondition>(),
                    RemainingPokemon = pokemonList,
                    CanEscape = canEscape,
                    HasActed = false
                },
                Position = new BattlePosition(),
                Relationships = new Dictionary<string, RelationshipType>()
            };

            return JsonSerializer.Serialize(participant, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error parsing Pokemon list: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("check_victory_conditions")]
    [Description("Check if any victory conditions have been met")]
    public async Task<string> CheckVictoryConditions()
    {
        Debug.WriteLine($"[BattleStatePlugin] CheckVictoryConditions called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var battleState = gameState.BattleState;
        var victoryResults = new List<object>();

        foreach (var condition in battleState.VictoryConditions)
        {
            var met = CheckSingleVictoryCondition(battleState, condition);
            victoryResults.Add(new
            {
                condition.Type,
                condition.Faction,
                condition.Description,
                met = met,
                reason = met ? $"{condition.Faction} has achieved {condition.Type}" : "Condition not met"
            });
        }

        var anyVictory = victoryResults.Any(r => (bool)r.GetType().GetProperty("met")!.GetValue(r)!);

        return JsonSerializer.Serialize(new
        {
            battleEnded = anyVictory,
            victoryConditions = victoryResults
        }, _jsonOptions);
    }

    [KernelFunction("get_battlefield_summary")]
    [Description("Get a summary of the current battlefield state")]
    public async Task<string> GetBattlefieldSummary()
    {
        Debug.WriteLine($"[BattleStatePlugin] GetBattlefieldSummary called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var battleState = gameState.BattleState;
        var summary = new
        {
            battleType = battleState.BattleType.ToString(),
            turn = battleState.CurrentTurn,
            phase = battleState.CurrentPhase.ToString(),
            currentActor = battleState.CurrentActorId,
            totalParticipants = battleState.BattleParticipants.Count,
            activeParticipants = battleState.BattleParticipants.Count(p => !p.IsDefeated),
            defeatedParticipants = battleState.BattleParticipants.Count(p => p.IsDefeated),
            factions = battleState.BattleParticipants.Select(p => p.Faction).Distinct().ToList(),
            weather = battleState.Weather.Name,
            battlefield = battleState.Battlefield.Name,
            hazards = battleState.Battlefield.Hazards.Count,
            conditions = battleState.BattleConditions.Count,
            recentEvents = battleState.BattleLog.TakeLast(3).Select(log => new
            {
                log.Turn,
                log.ActorId,
                log.Action,
                log.Result
            })
        };

        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    #endregion

    #region Private Helper Methods

    private bool CheckSingleVictoryCondition(BattleState battleState, VictoryCondition condition)
    {
        return condition.Type switch
        {
            VictoryType.DefeatAllEnemies => CheckDefeatAllEnemies(battleState, condition.Faction),
            VictoryType.DefeatSpecificTarget => CheckDefeatSpecificTarget(battleState, condition),
            VictoryType.Survival => CheckSurvival(battleState, condition),
            VictoryType.Escape => CheckEscape(battleState, condition),
            VictoryType.Objective => CheckObjective(battleState, condition),
            VictoryType.Timer => CheckTimer(battleState, condition),
            _ => false
        };
    }

    private bool CheckDefeatAllEnemies(BattleState battleState, string faction)
    {
        var enemies = battleState.BattleParticipants
            .Where(p => p.Faction != faction && (p.Type == ParticipantType.EnemyPokemon || p.Type == ParticipantType.EnemyTrainer))
            .ToList();

        return enemies.All(e => e.IsDefeated);
    }

    private bool CheckDefeatSpecificTarget(BattleState battleState, VictoryCondition condition)
    {
        if (!condition.Parameters.TryGetValue("targetId", out var targetIdObj) || targetIdObj is not string targetId)
            return false;

        var target = battleState.BattleParticipants
            .FirstOrDefault(p => p.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));

        return target?.IsDefeated == true;
    }

    private bool CheckSurvival(BattleState battleState, VictoryCondition condition)
    {
        if (!condition.Parameters.TryGetValue("turns", out var turnsObj) || turnsObj is not int requiredTurns)
            return false;

        return battleState.CurrentTurn >= requiredTurns;
    }

    private bool CheckEscape(BattleState battleState, VictoryCondition condition)
    {
        // Check if any participant from the faction has successfully escaped
        // This would need to be tracked in the battle state when escape actions are processed
        return false; // Placeholder
    }

    private bool CheckObjective(BattleState battleState, VictoryCondition condition)
    {
        // Custom objective checking based on parameters
        // Implementation depends on specific objectives
        return false; // Placeholder
    }

    private bool CheckTimer(BattleState battleState, VictoryCondition condition)
    {
        if (!condition.Parameters.TryGetValue("timeLimit", out var timeLimitObj) || timeLimitObj is not DateTime timeLimit)
            return false;

        return DateTime.UtcNow >= timeLimit;
    }

    #endregion

    private void CalculateInitiativeOrder(BattleState battleState)
    {
        // Calculate initiative for each participant
        foreach (var participant in battleState.BattleParticipants)
        {
            if (participant.Pokemon != null)
            {
                // Base initiative on Pokemon's Agility stat + random factor
                var agilityModifier = (int)participant.Pokemon.PokemonData.Stats.Agility;
                participant.Initiative = agilityModifier * 10 + _random.Next(1, 21);
            }
            else if (participant.Trainer != null)
            {
                // Trainer initiative based on their Agility stat
                var agilityModifier = (int)participant.Trainer.Stats.Agility;
                participant.Initiative = agilityModifier * 10 + _random.Next(1, 21);
            }
            else
            {
                participant.Initiative = _random.Next(1, 21);
            }
        }

        // Sort by initiative (highest first) and create turn order
        battleState.TurnOrder = battleState.BattleParticipants
            .OrderByDescending(p => p.Initiative)
            .Select(p => p.Id)
            .ToList();

        // Set the first actor
        if (battleState.TurnOrder.Any())
        {
            battleState.CurrentActorId = battleState.TurnOrder.First();
        }
    }

    private void SetupDefaultRelationships(BattleState battleState)
    {
        // Set up default hostile relationships between different factions
        foreach (var participant in battleState.BattleParticipants)
        {
            foreach (var other in battleState.BattleParticipants.Where(p => p.Id != participant.Id))
            {
                if (participant.Faction != other.Faction)
                {
                    participant.Relationships[other.Id] = RelationshipType.Hostile;
                }
                else
                {
                    participant.Relationships[other.Id] = RelationshipType.Allied;
                }
            }
        }
    }

    private List<VictoryCondition> GenerateDefaultVictoryConditions(BattleType battleType)
    {
        return battleType switch
        {
            BattleType.Wild => new List<VictoryCondition>
            {
                new() { Type = VictoryType.DefeatAllEnemies, Faction = "Player", Description = "Defeat or capture all wild Pokemon" },
                new() { Type = VictoryType.Escape, Faction = "Player", Description = "Successfully escape from battle" }
            },
            BattleType.Trainer => new List<VictoryCondition>
            {
                new() { Type = VictoryType.DefeatAllEnemies, Faction = "Player", Description = "Defeat all opponent Pokemon" },
                new() { Type = VictoryType.DefeatAllEnemies, Faction = "Enemy", Description = "Defeat all player Pokemon" }
            },
            _ => new List<VictoryCondition>
            {
                new() { Type = VictoryType.DefeatAllEnemies, Faction = "Player", Description = "Defeat all enemies" }
            }
        };
    }

    private async Task<List<ActionResult>> ProcessBattleAction(BattleState battleState, BattleAction action)
    {
        var results = new List<ActionResult>();

        // Find the acting participant
        var actor = battleState.BattleParticipants
            .FirstOrDefault(p => p.Id.Equals(action.ActorId, StringComparison.OrdinalIgnoreCase));

        if (actor == null)
        {
            results.Add(new ActionResult
            {
                Success = false,
                Message = "Actor not found"
            });
            return results;
        }

        // Mark actor as having acted
        actor.HasActed = true;

        // Process the action based on type
        switch (action.ActionType)
        {
            case BattleActionType.Move:
                results.AddRange(await ProcessMoveAction(battleState, action, actor));
                break;
            case BattleActionType.Switch:
                results.AddRange(await ProcessSwitchAction(battleState, action, actor));
                break;
            case BattleActionType.Item:
                results.AddRange(await ProcessItemAction(battleState, action, actor));
                break;
            case BattleActionType.Escape:
                results.AddRange(await ProcessEscapeAction(battleState, action, actor));
                break;
            default:
                results.Add(new ActionResult
                {
                    Success = false,
                    Message = "Unknown action type"
                });
                break;
        }

        // Log the action
        LogBattleEvent(battleState, action.ActorId, action.ActionType.ToString(), 
            action.TargetIds, $"{actor.Name} used {action.MoveName}");

        return results;
    }

    private async Task<List<ActionResult>> ProcessMoveAction(BattleState battleState, BattleAction action, BattleParticipant actor)
    {
        var results = new List<ActionResult>();

        if (actor.Pokemon == null)
        {
            results.Add(new ActionResult
            {
                Success = false,
                Message = "Only Pokemon can use moves"
            });
            return results;
        }

        // Record the move as used
        if (!actor.Pokemon.UsedMoves.Contains(action.MoveName))
        {
            actor.Pokemon.UsedMoves.Add(action.MoveName);
        }

        // Process each target
        foreach (var targetId in action.TargetIds)
        {
            var target = battleState.BattleParticipants
                .FirstOrDefault(p => p.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                results.Add(new ActionResult
                {
                    TargetId = targetId,
                    Success = false,
                    Message = "Target not found"
                });
                continue;
            }

            // Calculate damage (simplified)
            var damage = CalculateMoveDamage(actor.Pokemon, target.Pokemon, action.MoveName);
            
            var result = new ActionResult
            {
                TargetId = targetId,
                Success = true,
                Damage = damage,
                Message = $"{actor.Name} used {action.MoveName} on {target.Name} for {damage} damage"
            };

            // Apply damage
            if (target.Pokemon != null && damage > 0)
            {
                target.Pokemon.CurrentVigor = Math.Max(0, target.Pokemon.CurrentVigor - damage);
                
                if (target.Pokemon.CurrentVigor == 0)
                {
                    target.IsDefeated = true;
                    result.Message += " - Target defeated!";
                }
            }

            results.Add(result);
        }

        return results;
    }

    private async Task<List<ActionResult>> ProcessSwitchAction(BattleState battleState, BattleAction action, BattleParticipant actor)
    {
        // Implementation for switching Pokemon
        return new List<ActionResult>
        {
            new() { Success = true, Message = $"{actor.Name} is switching Pokemon" }
        };
    }

    private async Task<List<ActionResult>> ProcessItemAction(BattleState battleState, BattleAction action, BattleParticipant actor)
    {
        // Implementation for using items
        return new List<ActionResult>
        {
            new() { Success = true, Message = $"{actor.Name} used an item" }
        };
    }

    private async Task<List<ActionResult>> ProcessEscapeAction(BattleState battleState, BattleAction action, BattleParticipant actor)
    {
        // Implementation for escaping
        return new List<ActionResult>
        {
            new() { Success = true, Message = $"{actor.Name} is trying to escape" }
        };
    }

    private int CalculateMoveDamage(BattlePokemon? attacker, BattlePokemon? defender, string moveName)
    {
        if (attacker == null || defender == null)
            return 0;

        // Simplified damage calculation
        var baseDamage = 50; // Default move power
        var attackStat = (int)attacker.PokemonData.Stats.Strength;
        var defenseStat = (int)defender.PokemonData.Stats.Strength;
        
        var damage = (baseDamage + attackStat - defenseStat) + _random.Next(-10, 11);
        return Math.Max(1, damage);
    }

    private void LogBattleEvent(BattleState battleState, string actorId, string action, List<string> targets, string result)
    {
        battleState.BattleLog.Add(new BattleLogEntry
        {
            Turn = battleState.CurrentTurn,
            Phase = battleState.CurrentPhase,
            ActorId = actorId,
            Action = action,
            Targets = targets,
            Result = result,
            Timestamp = DateTime.UtcNow
        });
    }
}