using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;
using PokeLLM.Game.Helpers;

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

    #region Helper Methods

    private string GetParticipantId(BattleParticipant participant)
    {
        if (participant.Pokemon != null)
            return participant.Pokemon.Id;
        if (participant.Character != null)
            return participant.Character.Id;
        return Guid.NewGuid().ToString(); // Fallback
    }

    private string GetParticipantName(BattleParticipant participant)
    {
        if (participant.Pokemon != null)
            return participant.Pokemon.Name;
        if (participant.Character != null)
            return participant.Character.Name;
        return "Unknown";
    }

    private string GetParticipantFaction(BattleParticipant participant)
    {
        if (participant.Pokemon != null)
            return participant.Pokemon.Faction;
        if (participant.Character != null)
            return participant.Character.Faction;
        return "Unknown";
    }

    #endregion

    #region Battle Initialization

    [KernelFunction("start_battle")]
    [Description("Initialize a new battle with specified participants and conditions. Creates a tactical D&D-style Pokemon battle with turn order, positioning, and victory conditions. Example: start_battle('Wild', '[{\"pokemon\":{\"id\":\"pikachu1\",\"name\":\"Pikachu\"}}]', 'Forest Clearing', 'Clear')")]
    public async Task<string> StartBattle(
        [Description("Type of battle: Wild, Trainer, Gym, Elite, Champion, Team, Raid, Tournament")] string battleType,
        [Description("JSON array of battle participants with their data")] string participantsJson,
        [Description("Battlefield name/description for narrative context")] string battlefieldName = "Standard Field",
        [Description("Weather conditions affecting the battle: Clear, Rain, Sun, Sandstorm, Hail, etc.")] string weather = "Clear")
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
                VictoryCondition = GenerateDefaultVictoryCondition(battleTypeEnum),
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
    [Description("End the current battle and clean up battle state. Marks battle as inactive and logs the conclusion. Example: end_battle('Victory - All enemies defeated')")]
    public async Task<string> EndBattle(
        [Description("Reason for ending battle for logging purposes")] string reason = "Battle concluded")
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
    [Description("Get the current battle state and all participant information including positions, health, status effects, and turn order. Essential for understanding battle context.")]
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
    [Description("Get detailed status of a specific battle participant including health, position, status effects, and relationships. Example: get_participant_status('pikachu1')")]
    public async Task<string> GetParticipantStatus(
        [Description("Unique ID of the participant to check")] string participantId)
    {
        Debug.WriteLine($"[BattleStatePlugin] GetParticipantStatus called for: {participantId}");

        var gameState = await _repository.LoadLatestStateAsync();
        var participant = gameState?.BattleState?.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(participantId, StringComparison.OrdinalIgnoreCase));

        if (participant == null)
        {
            return JsonSerializer.Serialize(new { error = "Participant not found" }, _jsonOptions);
        }

        var status = new
        {
            id = GetParticipantId(participant),
            name = GetParticipantName(participant),
            type = participant.Type,
            position = participant.Position,
            initiative = participant.Initiative,
            hasActed = participant.HasActed,
            isDefeated = participant.IsDefeated,
            pokemon = participant.Pokemon != null ? new
            {
                participant.Pokemon.CurrentVigor,
                participant.Pokemon.MaxVigor,
                vigorPercentage = (participant.Pokemon.CurrentVigor * 100) / Math.Max(1, participant.Pokemon.MaxVigor),
                statusEffects = participant.Pokemon.StatusEffects.Count,
                knownMoves = participant.Pokemon.KnownMoves.Count
            } : null,
            character = participant.Character != null ? new
            {
                participant.Character.Name,
                hasActed = participant.HasActed,
                conditions = participant.Character.Conditions.Count
            } : null,
            relationships = participant.Relationships
        };

        return JsonSerializer.Serialize(status, _jsonOptions);
    }

    [KernelFunction("get_turn_order")]
    [Description("Get the current turn order and whose turn it is based on initiative rolls. Shows the sequence of actions for the current battle turn.")]
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
                id = GetParticipantId(p),
                name = GetParticipantName(p),
                initiative = p.Initiative,
                hasActed = p.HasActed,
                isDefeated = p.IsDefeated
            }).OrderByDescending(p => p.initiative)
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Battle Actions

    [KernelFunction("execute_battle_action")]
    [Description("Execute a battle action for a participant. For attack actions: rolls to hit against target defense, then calculates damage on successful hits. Example: execute_battle_action('pikachu1', 'Attack', '[\"rattata1\"]', 'Thunderbolt', 'Electric', 3, true)")]
    public async Task<string> ExecuteBattleAction(
        [Description("Unique ID of the acting participant")] string actorId,
        [Description("Type of action: Attack, Switch, Item, Escape")] string actionType,
        [Description("JSON array of target participant IDs")] string targetIdsJson,
        [Description("Name of move/action being performed")] string actionName = "",
        [Description("For attacks: type of the move (Fire, Water, Electric, etc.)")] string moveType = "Normal",
        [Description("For attacks: number of damage dice to roll")] int numDice = 2,
        [Description("For attacks: whether this uses special attack/defense stats")] bool isSpecialMove = false)
    {
        Debug.WriteLine($"[BattleStatePlugin] ExecuteBattleAction called: {actorId} using {actionName}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null || !gameState.BattleState.IsActive)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        try
        {
            var targetIds = JsonSerializer.Deserialize<List<string>>(targetIdsJson, _jsonOptions);
            if (targetIds == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid target IDs format" }, _jsonOptions);
            }

            // Create strongly typed battle action
            var action = new BattleAction
            {
                ActorId = actorId,
                ActionType = Enum.Parse<BattleActionType>(actionType, true),
                TargetIds = targetIds,
                MoveName = actionName
            };

            // For attack actions, populate move details
            if (action.ActionType == BattleActionType.Move)
            {
                action.Parameters["moveType"] = moveType;
                action.Parameters["numDice"] = numDice;
                action.Parameters["isSpecial"] = isSpecialMove;
            }

            var results = await ProcessBattleAction(gameState.BattleState, action);
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { success = true, results = results }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error processing action: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("advance_battle_phase")]
    [Description("Advance the battle to the next phase in the turn sequence: Initialize -> SelectAction -> ResolveActions -> ApplyEffects -> CheckVictory -> EndTurn. Manages turn progression.")]
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
    [Description("Add a new participant to the current battle and recalculate turn order. Used for reinforcements, summoned Pokemon, or late joiners. Example: add_battle_participant('{\"pokemon\":{\"id\":\"charizard1\",\"name\":\"Charizard\"}}')")]
    public async Task<string> AddBattleParticipant(
        [Description("JSON string with participant data including pokemon or character data")] string participantJson)
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
                    $"{GetParticipantName(participant)} joined the battle");
            });

            return JsonSerializer.Serialize(new { success = true, participantId = GetParticipantId(participant) }, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error parsing participant: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("remove_battle_participant")]
    [Description("Remove a participant from the current battle due to defeat, escape, or withdrawal. Updates turn order accordingly. Example: remove_battle_participant('rattata1')")]
    public async Task<string> RemoveBattleParticipant(
        [Description("Unique ID of the participant to remove")] string participantId)
    {
        Debug.WriteLine($"[BattleStatePlugin] RemoveBattleParticipant called for: {participantId}");

        await _repository.UpdateBattleStateAsync(battleState =>
        {
            var participant = battleState.BattleParticipants
                .FirstOrDefault(p => GetParticipantId(p).Equals(participantId, StringComparison.OrdinalIgnoreCase));
            
            if (participant != null)
            {
                battleState.BattleParticipants.Remove(participant);
                battleState.TurnOrder.Remove(participantId);
                
                LogBattleEvent(battleState, "System", "Participant Removed", new List<string>(), 
                    $"{GetParticipantName(participant)} left the battle");
            }
        });

        return JsonSerializer.Serialize(new { success = true }, _jsonOptions);
    }

    [KernelFunction("update_participant_vigor")]
    [Description("Update a Pokemon participant's vigor (health) due to damage, healing, or other effects. Automatically checks for defeat at 0 vigor. Example: update_participant_vigor('pikachu1', 45, 'Took damage from Tackle')")]
    public async Task<string> UpdateParticipantVigor(
        [Description("Unique ID of the participant")] string participantId,
        [Description("New current vigor value (0 = fainted)")] int newVigor,
        [Description("Reason for change for battle log")] string reason = "")
    {
        Debug.WriteLine($"[BattleStatePlugin] UpdateParticipantVigor called for: {participantId}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var participant = gameState.BattleState.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(participantId, StringComparison.OrdinalIgnoreCase));

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
                $"{GetParticipantName(participant)} was defeated");
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
    [Description("Apply a status effect to a battle participant such as poison, paralysis, or stat changes. Effects can be temporary or permanent. Example: apply_status_effect('rattata1', '{\"name\":\"Poison\",\"type\":\"Debuff\",\"duration\":5,\"severity\":1}')")]
    public async Task<string> ApplyStatusEffect(
        [Description("Unique ID of the target participant")] string targetId,
        [Description("JSON string with status effect data including name, type, duration, and effects")] string statusEffectJson)
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
                .FirstOrDefault(p => GetParticipantId(p).Equals(targetId, StringComparison.OrdinalIgnoreCase));

            if (participant?.Pokemon == null)
            {
                return JsonSerializer.Serialize(new { error = "Target not found or not a Pokemon" }, _jsonOptions);
            }

            // Remove existing effect of same name
            participant.Pokemon.StatusEffects.RemoveAll(e => e.Name.Equals(statusEffect.Name, StringComparison.OrdinalIgnoreCase));
            
            // Add new effect
            participant.Pokemon.StatusEffects.Add(statusEffect);

            LogBattleEvent(gameState.BattleState, "System", "Status Effect Applied", new List<string> { targetId }, 
                $"{GetParticipantName(participant)} is now {statusEffect.Name}");

            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { success = true, effectName = statusEffect.Name }, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error parsing status effect: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("remove_status_effect")]
    [Description("Remove a status effect from a battle participant, typically due to natural expiration, healing, or other cure effects. Example: remove_status_effect('pikachu1', 'Poison')")]
    public async Task<string> RemoveStatusEffect(
        [Description("Unique ID of the target participant")] string targetId,
        [Description("Name of the status effect to remove (case-insensitive)")] string effectName)
    {
        Debug.WriteLine($"[BattleStatePlugin] RemoveStatusEffect called for: {targetId}, effect: {effectName}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var participant = gameState.BattleState.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(targetId, StringComparison.OrdinalIgnoreCase));

        if (participant?.Pokemon == null)
        {
            return JsonSerializer.Serialize(new { error = "Target not found or not a Pokemon" }, _jsonOptions);
        }

        var removed = participant.Pokemon.StatusEffects.RemoveAll(e => 
            e.Name.Equals(effectName, StringComparison.OrdinalIgnoreCase)) > 0;

        if (removed)
        {
            LogBattleEvent(gameState.BattleState, "System", "Status Effect Removed", new List<string> { targetId }, 
                $"{GetParticipantName(participant)} is no longer {effectName}");
        }

        await _repository.SaveStateAsync(gameState);

        return JsonSerializer.Serialize(new { success = true, removed = removed }, _jsonOptions);
    }

    #endregion

    #region Battle Log and History

    [KernelFunction("get_battle_log")]
    [Description("Get the battle log with optional filtering to review recent actions and their results. Useful for narrative descriptions and understanding battle flow. Example: get_battle_log(5, 'pikachu1')")]
    public async Task<string> GetBattleLog(
        [Description("Number of recent entries to return (0 = all entries)")] int count = 10,
        [Description("Filter by actor ID to see only specific participant's actions")] string actorFilter = "")
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
    [Description("Create a Pokemon battle participant from a Pokemon in the trainer's team with specified position and faction. Example: create_pokemon_participant('Pikachu', 'Player', 2, 3)")]
    public async Task<string> CreatePokemonParticipant(
        [Description("Name of the Pokemon from trainer's team")] string pokemonName,
        [Description("Faction: Player, Enemy, Neutral, Allied")] string faction = "Player",
        [Description("X position on battlefield (0-9)")] int x = 0,
        [Description("Y position on battlefield (0-9)")] int y = 0)
    {
        Debug.WriteLine($"[BattleStatePlugin] CreatePokemonParticipant called for: {pokemonName}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
        {
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);
        }

        // Find the Pokemon in the trainer's team
        var ownedPokemon = gameState.Player.Character.PokemonTeam.ActivePokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase)) ??
            gameState.Player.Character.PokemonTeam.BoxedPokemon
            .FirstOrDefault(p => p.Pokemon.Name.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

        if (ownedPokemon == null)
        {
            return JsonSerializer.Serialize(new { error = "Pokemon not found in trainer's team" }, _jsonOptions);
        }

        var participant = new BattleParticipant
        {
            Type = faction.ToLower() == "player" ? ParticipantType.PlayerPokemon : ParticipantType.EnemyPokemon,
            Pokemon = ownedPokemon.Pokemon,
            Position = new BattlePosition { X = x, Y = y },
            Relationships = new Dictionary<string, RelationshipType>(),
            TemporaryStats = new Dictionary<string, int>()
        };

        return JsonSerializer.Serialize(participant, _jsonOptions);
    }

    [KernelFunction("create_trainer_participant")]
    [Description("Create a trainer battle participant with their Pokemon team and battle capabilities. Example: create_trainer_participant('Gary', 'Enemy', '[]', true)")]
    public async Task<string> CreateTrainerParticipant(
        [Description("Name of the trainer")] string trainerName,
        [Description("Faction: Player, Enemy, Neutral, Allied")] string faction = "Enemy",
        [Description("JSON array of Pokemon for this trainer (simplified format)")] string pokemonListJson = "[]",
        [Description("Can this trainer escape from battle?")] bool canEscape = true)
    {
        Debug.WriteLine($"[BattleStatePlugin] CreateTrainerParticipant called for: {trainerName}");

        try
        {
            var participant = new BattleParticipant
            {
                Type = faction.ToLower() == "player" ? ParticipantType.PlayerTrainer : ParticipantType.EnemyTrainer,
                Character = new Character
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = trainerName,
                    Stats = new Stats
                    {
                        Power = new Stat { Type = StatType.Power, Level = StatLevel.Trained },
                        Speed = new Stat { Type = StatType.Speed, Level = StatLevel.Trained },
                        Mind = new Stat { Type = StatType.Mind, Level = StatLevel.Trained },
                        Charm = new Stat { Type = StatType.Charm, Level = StatLevel.Trained },
                        Defense = new Stat { Type = StatType.Defense, Level = StatLevel.Trained },
                        Spirit = new Stat { Type = StatType.Spirit, Level = StatLevel.Trained }
                    },
                    Conditions = new List<ActiveCondition>(),
                    Faction = faction,
                    IsTrainer = true,
                    Inventory = new Dictionary<string, int>(),
                    PokemonTeam = new PokemonTeam { ActivePokemon = new List<OwnedPokemon>(), BoxedPokemon = new List<OwnedPokemon>() }
                },
                Position = new BattlePosition(),
                Relationships = new Dictionary<string, RelationshipType>(),
                TemporaryStats = new Dictionary<string, int>()
            };

            return JsonSerializer.Serialize(participant, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Error creating trainer participant: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("check_victory_conditions")]
    [Description("Check if any victory conditions have been met to determine if the battle should end. Returns status of victory condition and whether battle is concluded.")]
    public async Task<string> CheckVictoryConditions()
    {
        Debug.WriteLine($"[BattleStatePlugin] CheckVictoryConditions called");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var battleState = gameState.BattleState;
        var condition = battleState.VictoryCondition;
        
        var met = CheckSingleVictoryCondition(battleState, condition);
        var victoryResult = new
        {
            condition.Type,
            condition.Description,
            met = met,
            reason = met ? $"Victory condition achieved: {condition.Type}" : "Condition not met"
        };

        return JsonSerializer.Serialize(new
        {
            battleEnded = met,
            victoryCondition = victoryResult
        }, _jsonOptions);
    }

    [KernelFunction("get_battlefield_summary")]
    [Description("Get a summary of the current battlefield state including participants, turn info, weather, and recent events. Useful for narrative descriptions and tactical overview.")]
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
            factions = battleState.BattleParticipants.Select(p => GetParticipantFaction(p)).Distinct().ToList(),
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
            VictoryType.DefeatAllEnemies => CheckDefeatAllEnemies(battleState),
            VictoryType.DefeatSpecificTarget => CheckDefeatSpecificTarget(battleState, condition),
            VictoryType.Survival => CheckSurvival(battleState, condition),
            VictoryType.Escape => CheckEscape(battleState, condition),
            VictoryType.Objective => CheckObjective(battleState, condition),
            VictoryType.Timer => CheckTimer(battleState, condition),
            _ => false
        };
    }

    private bool CheckDefeatAllEnemies(BattleState battleState)
    {
        var enemies = battleState.BattleParticipants
            .Where(p => GetParticipantFaction(p) != "Player" && (p.Type == ParticipantType.EnemyPokemon || p.Type == ParticipantType.EnemyTrainer))
            .ToList();

        return enemies.All(e => e.IsDefeated);
    }

    private bool CheckDefeatSpecificTarget(BattleState battleState, VictoryCondition condition)
    {
        if (!condition.Parameters.TryGetValue("targetId", out var targetIdObj) || targetIdObj is not string targetId)
            return false;

        var target = battleState.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(targetId, StringComparison.OrdinalIgnoreCase));

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
        // Check if any participant has successfully escaped
        return false; // Placeholder - would need to track escapes
    }

    private bool CheckObjective(BattleState battleState, VictoryCondition condition)
    {
        // Custom objective checking based on parameters
        return false; // Placeholder
    }

    private bool CheckTimer(BattleState battleState, VictoryCondition condition)
    {
        if (!condition.Parameters.TryGetValue("timeLimit", out var timeLimitObj) || timeLimitObj is not DateTime timeLimit)
            return false;

        return DateTime.UtcNow >= timeLimit;
    }

    private void CalculateInitiativeOrder(BattleState battleState)
    {
        // Calculate initiative for each participant using BattleCalcHelper
        foreach (var participant in battleState.BattleParticipants)
        {
            if (participant.Pokemon != null)
            {
                participant.Initiative = BattleCalcHelper.CalculateInitiative(participant.Pokemon, _random);
            }
            else if (participant.Character != null)
            {
                participant.Initiative = BattleCalcHelper.CalculateTrainerInitiative(participant.Character, _random);
            }
            else
            {
                participant.Initiative = _random.Next(1, 21);
            }
        }

        // Sort by initiative (highest first) and create turn order
        battleState.TurnOrder = battleState.BattleParticipants
            .OrderByDescending(p => p.Initiative)
            .Select(p => GetParticipantId(p))
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
            foreach (var other in battleState.BattleParticipants.Where(p => GetParticipantId(p) != GetParticipantId(participant)))
            {
                if (GetParticipantFaction(participant) != GetParticipantFaction(other))
                {
                    participant.Relationships[GetParticipantId(other)] = RelationshipType.Hostile;
                }
                else
                {
                    participant.Relationships[GetParticipantId(other)] = RelationshipType.Allied;
                }
            }
        }
    }

    private VictoryCondition GenerateDefaultVictoryCondition(BattleType battleType)
    {
        return battleType switch
        {
            BattleType.Wild => new VictoryCondition 
            { 
                Type = VictoryType.DefeatAllEnemies, 
                Description = "Defeat or capture all wild Pokemon",
                Parameters = new Dictionary<string, object>()
            },
            BattleType.Trainer => new VictoryCondition 
            { 
                Type = VictoryType.DefeatAllEnemies, 
                Description = "Defeat all opponent Pokemon",
                Parameters = new Dictionary<string, object>()
            },
            _ => new VictoryCondition 
            { 
                Type = VictoryType.DefeatAllEnemies, 
                Description = "Defeat all enemies",
                Parameters = new Dictionary<string, object>()
            }
        };
    }

    private async Task<List<ActionResult>> ProcessBattleAction(BattleState battleState, BattleAction action)
    {
        var results = new List<ActionResult>();

        // Find the acting participant
        var actor = battleState.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(action.ActorId, StringComparison.OrdinalIgnoreCase));

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
                results.AddRange(ProcessAttackAction(battleState, action, actor));
                break;
            case BattleActionType.Switch:
                results.Add(new ActionResult { Success = false, Message = "Switch action logged - functionality not implemented. Handle Elsewhere" });
                break;
            case BattleActionType.Item:
                results.Add(new ActionResult { Success = false, Message = "Item action logged - functionality not implemented. Handle Elsewhere" });
                break;
            case BattleActionType.Escape:
                results.Add(new ActionResult { Success = false, Message = "Escape action logged - functionality not implemented. Handle Elsewhere" });
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
            action.TargetIds, $"{GetParticipantName(actor)} used {action.MoveName}");

        return results;
    }

    private List<ActionResult> ProcessAttackAction(BattleState battleState, BattleAction action, BattleParticipant actor)
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

        // Get move details from action parameters
        var moveType = action.Parameters.GetValueOrDefault("moveType", "Normal")?.ToString() ?? "Normal";
        var numDice = int.TryParse(action.Parameters.GetValueOrDefault("numDice", 2)?.ToString(), out var dice) ? dice : 2;
        var isSpecialMove = bool.TryParse(action.Parameters.GetValueOrDefault("isSpecial", false)?.ToString(), out var special) && special;

        // Process each target
        foreach (var targetId in action.TargetIds)
        {
            var target = battleState.BattleParticipants
                .FirstOrDefault(p => GetParticipantId(p).Equals(targetId, StringComparison.OrdinalIgnoreCase));

            if (target?.Pokemon == null)
            {
                results.Add(new ActionResult
                {
                    TargetId = targetId,
                    Success = false,
                    Message = "Target not found or not a Pokemon"
                });
                continue;
            }

            // Step 1: Roll to hit against target's defense
            var hitResult = RollToHit(actor.Pokemon, target.Pokemon, isSpecialMove);
            
            var result = new ActionResult
            {
                TargetId = targetId,
                Success = hitResult.Hit,
                Message = $"{GetParticipantName(actor)} used {action.MoveName} on {GetParticipantName(target)}"
            };

            // Add hit roll information to effects
            result.Effects["hitRoll"] = hitResult.HitRoll;
            result.Effects["targetDefense"] = hitResult.TargetDefense;
            result.Effects["hitMargin"] = hitResult.Margin;

            if (!hitResult.Hit)
            {
                result.Message += " - MISS!";
                result.Effects["missed"] = true;
                results.Add(result);
                continue;
            }

            result.Message += " - HIT!";

            // Step 2: Calculate damage if hit was successful
            var damage = BattleCalcHelper.CalculateMoveDamage(
                actor.Pokemon, 
                target.Pokemon, 
                action.MoveName, 
                moveType, 
                numDice, 
                hitResult.HitRoll, 
                isSpecialMove, 
                _random);

            result.Damage = damage;
            result.Message += $" Deals {damage} damage";

            // Add critical hit message
            if (hitResult.HitRoll == 20)
            {
                result.Message += " - Critical hit!";
                result.Effects["critical"] = true;
            }

            // Add type effectiveness information
            var typeEffectiveness = BattleCalcHelper.CalculateDualTypeEffectiveness(
                moveType, 
                target.Pokemon.Type1, 
                target.Pokemon.Type2);

            result.Effects["typeEffectiveness"] = typeEffectiveness;
            result.Effects["effectivenessDescription"] = BattleCalcHelper.GetEffectivenessDescription(typeEffectiveness);

            if (typeEffectiveness > 1.0)
            {
                result.Message += " - It's super effective!";
            }
            else if (typeEffectiveness < 1.0 && typeEffectiveness > 0.0)
            {
                result.Message += " - It's not very effective...";
            }
            else if (typeEffectiveness == 0.0)
            {
                result.Message += " - It had no effect!";
            }

            // Step 3: Apply damage to target
            if (damage > 0)
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

    /// <summary>
    /// Roll to hit against target's defense, following D&D-style attack mechanics
    /// </summary>
    private HitResult RollToHit(Pokemon attacker, Pokemon target, bool isSpecialMove)
    {
        // Roll d20 for hit
        var hitRoll = _random.Next(1, 21);
        
        // Get attacker's relevant attack stat
        var attackStat = isSpecialMove ? attacker.Stats.Mind.Level : attacker.Stats.Power.Level;
        var attackModifier = (int)attackStat;
        
        // Get target's relevant defense stat (Defense for physical, Spirit for special)
        var defenseStat = isSpecialMove ? target.Stats.Spirit.Level : target.Stats.Defense.Level;
        var defenseValue = 10 + (int)defenseStat; // Base defense of 10 + stat modifier
        
        var totalAttackRoll = hitRoll + attackModifier;
        var hit = totalAttackRoll >= defenseValue;
        var margin = totalAttackRoll - defenseValue;
        
        return new HitResult
        {
            Hit = hit,
            HitRoll = hitRoll,
            AttackModifier = attackModifier,
            TotalAttackRoll = totalAttackRoll,
            TargetDefense = defenseValue,
            Margin = margin
        };
    }

    /// <summary>
    /// Result of a hit roll attempt
    /// </summary>
    private class HitResult
    {
        public bool Hit { get; set; }
        public int HitRoll { get; set; }
        public int AttackModifier { get; set; }
        public int TotalAttackRoll { get; set; }
        public int TargetDefense { get; set; }
        public int Margin { get; set; }
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
            Result = result
        });
    }

    #endregion

    #region Battle Calculation Functions

    // TODO: Add battle calculation functions like damage preview, effectiveness analysis, escape chance, etc.
    // These would use the BattleCalcHelper methods to provide tactical information

    #endregion

    #region Additional Battle Actions

    [KernelFunction("attack_target")]
    [Description("Simplified attack action - roll to hit and calculate damage against a single target. Example: attack_target('pikachu1', 'rattata1', 'Thunderbolt', 'Electric', 3, true)")]
    public async Task<string> AttackTarget(
        [Description("ID of the attacking Pokemon")] string attackerId,
        [Description("ID of the target Pokemon")] string targetId,
        [Description("Name of the move being used")] string moveName,
        [Description("Type of the move (Fire, Water, Electric, etc.)")] string moveType = "Normal",
        [Description("Number of damage dice to roll (typically 1-4)")] int numDice = 2,
        [Description("Whether this is a special attack (uses Mind/Spirit) vs physical (Power/Defense)")] bool isSpecialMove = false)
    {
        Debug.WriteLine($"[BattleStatePlugin] AttackTarget called: {attackerId} attacks {targetId} with {moveName}");

        return await ExecuteBattleAction(attackerId, "Attack", $"[\"{targetId}\"]", moveName, moveType, numDice, isSpecialMove);
    }

    [KernelFunction("get_pokemon_moves")]
    [Description("Get available moves for a Pokemon participant in battle, including move types and power levels. Example: get_pokemon_moves('pikachu1')")]
    public async Task<string> GetPokemonMoves(
        [Description("ID of the Pokemon participant")] string pokemonId)
    {
        Debug.WriteLine($"[BattleStatePlugin] GetPokemonMoves called for: {pokemonId}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var participant = gameState.BattleState.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(pokemonId, StringComparison.OrdinalIgnoreCase));

        if (participant?.Pokemon == null)
        {
            return JsonSerializer.Serialize(new { error = "Pokemon not found" }, _jsonOptions);
        }

        // Note: KnownMoves appears to be a List<string> based on the context, so we'll simplify this
        var moves = participant.Pokemon.KnownMoves.Select((moveName, index) => new
        {
            name = moveName,
            type = "Normal", // Default - would need actual move data
            category = "Physical", // Default - would need actual move data  
            power = 50, // Default - would need actual move data
            accuracy = 100, // Default - would need actual move data
            description = $"A {moveName} attack",
            isSpecial = false, // Default - would need actual move data
            suggestedDice = 2 // Default dice count
        }).ToList();

        var result = new
        {
            pokemonId = pokemonId,
            pokemonName = participant.Pokemon.Name,
            availableMoves = moves,
            moveCount = moves.Count,
            message = $"{participant.Pokemon.Name} has {moves.Count} available moves"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("preview_attack")]
    [Description("Preview an attack without executing it - shows hit chances and potential damage ranges. Example: preview_attack('pikachu1', 'rattata1', 'Thunderbolt', 'Electric', 3, true)")]
    public async Task<string> PreviewAttack(
        [Description("ID of the attacking Pokemon")] string attackerId,
        [Description("ID of the target Pokemon")] string targetId,
        [Description("Name of the move being used")] string moveName,
        [Description("Type of the move")] string moveType = "Normal",
        [Description("Number of damage dice")] int numDice = 2,
        [Description("Whether this is a special attack")] bool isSpecialMove = false)
    {
        Debug.WriteLine($"[BattleStatePlugin] PreviewAttack called: {attackerId} vs {targetId}");

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState?.BattleState == null)
        {
            return JsonSerializer.Serialize(new { error = "No active battle" }, _jsonOptions);
        }

        var attacker = gameState.BattleState.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(attackerId, StringComparison.OrdinalIgnoreCase));

        var target = gameState.BattleState.BattleParticipants
            .FirstOrDefault(p => GetParticipantId(p).Equals(targetId, StringComparison.OrdinalIgnoreCase));

        if (attacker?.Pokemon == null || target?.Pokemon == null)
        {
            return JsonSerializer.Serialize(new { error = "Attacker or target not found" }, _jsonOptions);
        }

        // Calculate hit chance
        var attackStat = isSpecialMove ? attacker.Pokemon.Stats.Mind.Level : attacker.Pokemon.Stats.Power.Level;
        var defenseStat = isSpecialMove ? target.Pokemon.Stats.Spirit.Level : target.Pokemon.Stats.Defense.Level;
        var attackModifier = (int)attackStat;
        var defenseValue = 10 + (int)defenseStat;

        // Hit chance calculation (need to roll attackModifier + d20 >= defenseValue)
        // So need to roll (defenseValue - attackModifier) or higher on d20
        var neededRoll = Math.Max(1, defenseValue - attackModifier);
        var hitChance = Math.Max(5, Math.Min(95, (21 - neededRoll) * 5)); // 5% min, 95% max

        // Type effectiveness
        var typeEffectiveness = BattleCalcHelper.CalculateDualTypeEffectiveness(moveType, target.Pokemon.Type1, target.Pokemon.Type2);

        // Calculate bonus dice: +1 die for every 2 skill levels above Novice
        var attackStatValue = (int)attackStat;
        var bonusDice = attackStatValue / 2;
        var totalDice = numDice + bonusDice;

        // Damage ranges (updated calculation without stat modifiers)
        var minDamage = totalDice; // minimum roll is 1 per die
        var maxDamage = totalDice * 6; // maximum roll is 6 per die
        var avgDamage = totalDice * 3.5; // average roll is 3.5 per die

        // No type effectiveness multiplier or defense reduction in new system
        minDamage = Math.Max(1, minDamage);
        maxDamage = Math.Max(1, maxDamage);
        avgDamage = Math.Max(1, avgDamage);

        var result = new
        {
            attackerName = GetParticipantName(attacker),
            targetName = GetParticipantName(target),
            moveName = moveName,
            moveType = moveType,
            hitChance = $"{hitChance}%",
            hitCalculation = $"Need {neededRoll}+ on d20 (attack +{attackModifier} vs defense {defenseValue})",
            typeEffectiveness = typeEffectiveness,
            effectivenessDescription = BattleCalcHelper.GetEffectivenessDescription(typeEffectiveness),
            damageRange = new
            {
                minimum = minDamage,
                maximum = maxDamage,
                average = (int)avgDamage,
                diceExpression = $"{totalDice}d6 (base {numDice} + {bonusDice} bonus dice from {attackStat} skill)",
                advantageDisadvantage = typeEffectiveness > 1.0 ? "Advantage (roll twice, take higher)" :
                                      typeEffectiveness < 1.0 && typeEffectiveness > 0.0 ? "Disadvantage (roll twice, take lower)" :
                                      "Normal rolls"
            },
            criticalHitDamage = new
            {
                minimum = (int)(minDamage * 1.5),
                maximum = (int)(maxDamage * 1.5),
                average = (int)(avgDamage * 1.5)
            },
            recommendation = hitChance >= 60 ? "Good chance to hit" : 
                           hitChance >= 40 ? "Moderate chance to hit" : "Low chance to hit"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion
}