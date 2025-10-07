using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for handling character and Pokemon level up progression with anime-style celebrations
/// </summary>
public class LevelUpPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IPokemonManagementService _pokemonManagementService;
    private readonly IInformationManagementService _informationManagementService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public LevelUpPhasePlugin(
        IGameStateRepository gameStateRepo,
        ICharacterManagementService characterManagementService,
        IPokemonManagementService pokemonManagementService,
        IInformationManagementService informationManagementService,
        IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _characterManagementService = characterManagementService;
        _pokemonManagementService = pokemonManagementService;
        _informationManagementService = informationManagementService;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("manage_player_advancement")]
    [Description("Handle player character level ups and stat improvements")]
    public async Task<string> ManagePlayerAdvancement(
        [Description("Action: 'level_up', 'improve_stats', 'learn_ability', 'check_advancement_eligibility'")] string action,
        [Description("Stat to improve (Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma)")] string statName = "",
        [Description("Amount to increase stat by")] int improvement = 1,
        [Description("New ability or skill to learn")] string abilityName = "",
        [Description("Reason or context for the advancement")] string advancementReason = "")
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] ManagePlayerAdvancement called: {action}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            switch (action.ToLower())
            {
                case "level_up":
                    var currentPlayer = await _characterManagementService.GetPlayerDetails();
                    var newLevel = currentPlayer.Level + 1;
                    
                    // Update player level directly by adding experience
                    var expNeeded = (newLevel * 1000) - currentPlayer.Experience;
                    await _characterManagementService.AddPlayerExperiencePoints(expNeeded);
                    
                    // Log the level up as a narrative event
                    await _informationManagementService.LogNarrativeEventAsync(
                        "player_level_up",
                        $"Player advanced to level {newLevel}",
                        $"Through {advancementReason}, the player has grown stronger and reached level {newLevel}. This represents their growing bond with their Pokemon and increasing skill as a trainer.",
                        new List<string> { "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    // Add to recent events
                    gameState.RecentEvents.Add(new EventLog 
                    { 
                        TurnNumber = gameState.GameTurnNumber, 
                        EventDescription = $"Player Level Up: Reached level {newLevel} - {advancementReason}" 
                    });
                    await _gameStateRepo.SaveStateAsync(gameState);
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Player advanced to level {newLevel}!",
                        newLevel = newLevel,
                        previousLevel = newLevel - 1,
                        reason = advancementReason,
                        action = action
                    }, _jsonOptions);
                    
                case "improve_stats":
                    if (string.IsNullOrEmpty(statName))
                    {
                        return JsonSerializer.Serialize(new { error = "Stat name is required for improvement" }, _jsonOptions);
                    }
                    
                    // For stat improvement, we'll need to get current stats and improve them
                    var player = await _characterManagementService.GetPlayerDetails();
                    var currentStats = new int[] 
                    {
                        player.Stats.Strength,
                        player.Stats.Dexterity,
                        player.Stats.Constitution,
                        player.Stats.Intelligence,
                        player.Stats.Wisdom,
                        player.Stats.Charisma
                    };
                    
                    // Improve the specific stat
                    var statIndex = statName.ToLower() switch
                    {
                        "strength" => 0,
                        "dexterity" => 1,
                        "constitution" => 2,
                        "intelligence" => 3,
                        "wisdom" => 4,
                        "charisma" => 5,
                        _ => -1
                    };
                    
                    if (statIndex == -1)
                    {
                        return JsonSerializer.Serialize(new { error = $"Invalid stat name: {statName}" }, _jsonOptions);
                    }
                    
                    currentStats[statIndex] += improvement;
                    await _characterManagementService.SetPlayerStats(currentStats);
                    
                    // Log the stat improvement
                    await _informationManagementService.LogNarrativeEventAsync(
                        "stat_improvement",
                        $"Player improved {statName} by {improvement}",
                        $"Through dedication and growth, the player's {statName} has increased by {improvement}. {advancementReason}",
                        new List<string> { "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"{statName} improved by {improvement}!",
                        statName = statName,
                        improvement = improvement,
                        newValue = currentStats[statIndex],
                        reason = advancementReason,
                        action = action
                    }, _jsonOptions);
                    
                case "learn_ability":
                    if (string.IsNullOrEmpty(abilityName))
                    {
                        return JsonSerializer.Serialize(new { error = "Ability name is required" }, _jsonOptions);
                    }
                    
                    await _characterManagementService.LearnPlayerAbility(abilityName);
                    
                    // Log the new ability
                    await _informationManagementService.LogNarrativeEventAsync(
                        "ability_learned",
                        $"Player learned new ability: {abilityName}",
                        $"The player has mastered a new skill: {abilityName}. {advancementReason}",
                        new List<string> { "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Learned new ability: {abilityName}!",
                        abilityName = abilityName,
                        reason = advancementReason,
                        action = action
                    }, _jsonOptions);
                    
                case "check_advancement_eligibility":
                    var playerDetails = await _characterManagementService.GetPlayerDetails();
                    var eligibilityInfo = new
                    {
                        currentLevel = playerDetails.Level,
                        currentExperience = playerDetails.Experience,
                        experienceToNextLevel = ((playerDetails.Level + 1) * 1000) - playerDetails.Experience, // Simplified calculation
                        canLevelUp = playerDetails.Experience >= ((playerDetails.Level + 1) * 1000),
                        currentStats = playerDetails.Stats,
                        abilities = playerDetails.Abilities
                    };
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        eligibility = eligibilityInfo,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown advancement action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in ManagePlayerAdvancement: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("manage_pokemon_advancement")]
    [Description("Handle Pokemon level ups, evolution, and move learning")]
    public async Task<string> ManagePokemonAdvancement(
        [Description("Action: 'level_up', 'evolve', 'learn_move', 'forget_move', 'check_evolution_eligibility'")] string action,
        [Description("Pokemon instance ID")] string pokemonInstanceId,
        [Description("New level (for level up)")] int newLevel = 0,
        [Description("Evolution species name")] string evolutionSpecies = "",
        [Description("Move to learn or forget")] Move moveData = null,
        [Description("Reason or context for advancement")] string advancementReason = "")
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] ManagePokemonAdvancement called: {action} for {pokemonInstanceId}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            switch (action.ToLower())
            {
                case "level_up":
                    if (newLevel <= 0)
                    {
                        return JsonSerializer.Serialize(new { error = "New level must be greater than 0" }, _jsonOptions);
                    }
                    
                    await _pokemonManagementService.SetPokemonLevel(pokemonInstanceId, newLevel);
                    
                    var pokemon = await _pokemonManagementService.GetPokemonDetails(pokemonInstanceId);
                    
                    // Log the Pokemon level up
                    await _informationManagementService.LogNarrativeEventAsync(
                        "pokemon_level_up",
                        $"{pokemon?.Species ?? pokemonInstanceId} advanced to level {newLevel}",
                        $"Through training and bonding with their trainer, {pokemon?.Species ?? pokemonInstanceId} has grown stronger and reached level {newLevel}. {advancementReason}",
                        new List<string> { pokemonInstanceId, "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"{pokemon?.Species ?? pokemonInstanceId} advanced to level {newLevel}!",
                        pokemonId = pokemonInstanceId,
                        species = pokemon?.Species,
                        newLevel = newLevel,
                        reason = advancementReason,
                        action = action
                    }, _jsonOptions);
                    
                case "evolve":
                    if (string.IsNullOrEmpty(evolutionSpecies))
                    {
                        return JsonSerializer.Serialize(new { error = "Evolution species is required" }, _jsonOptions);
                    }
                    
                    var prevoEvoPokemon = await _pokemonManagementService.GetPokemonDetails(pokemonInstanceId);
                    await _pokemonManagementService.EvolvePokemon(pokemonInstanceId, evolutionSpecies);
                    
                    // Log the evolution
                    await _informationManagementService.LogNarrativeEventAsync(
                        "pokemon_evolution",
                        $"{prevoEvoPokemon?.Species ?? pokemonInstanceId} evolved into {evolutionSpecies}",
                        $"In a brilliant flash of light, {prevoEvoPokemon?.Species ?? pokemonInstanceId} has evolved into {evolutionSpecies}! This evolution represents the deep bond between trainer and Pokemon. {advancementReason}",
                        new List<string> { pokemonInstanceId, "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    // Also increase friendship for evolution
                    await _pokemonManagementService.ChangePokemonFriendship(pokemonInstanceId, 10);
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"{prevoEvoPokemon?.Species ?? "Pokemon"} evolved into {evolutionSpecies}!",
                        pokemonId = pokemonInstanceId,
                        previousSpecies = prevoEvoPokemon?.Species,
                        newSpecies = evolutionSpecies,
                        reason = advancementReason,
                        action = action
                    }, _jsonOptions);
                    
                case "learn_move":
                    if (moveData == null)
                    {
                        return JsonSerializer.Serialize(new { error = "Move data is required" }, _jsonOptions);
                    }
                    
                    await _pokemonManagementService.LearnPokemonMove(pokemonInstanceId, moveData.Name);
                    
                    var learningPokemon = await _pokemonManagementService.GetPokemonDetails(pokemonInstanceId);
                    
                    // Log the move learning
                    await _informationManagementService.LogNarrativeEventAsync(
                        "move_learned",
                        $"{learningPokemon?.Species ?? pokemonInstanceId} learned {moveData.Name}",
                        $"Through practice and determination, {learningPokemon?.Species ?? pokemonInstanceId} has mastered the move {moveData.Name}! {advancementReason}",
                        new List<string> { pokemonInstanceId, "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"{learningPokemon?.Species ?? "Pokemon"} learned {moveData.Name}!",
                        pokemonId = pokemonInstanceId,
                        species = learningPokemon?.Species,
                        moveName = moveData.Name,
                        moveType = moveData.Type.ToString(),
                        reason = advancementReason,
                        action = action
                    }, _jsonOptions);
                    
                case "forget_move":
                    if (moveData == null || string.IsNullOrEmpty(moveData.Id))
                    {
                        return JsonSerializer.Serialize(new { error = "Move ID is required to forget a move" }, _jsonOptions);
                    }
                    
                    await _pokemonManagementService.ForgetPokemonMove(pokemonInstanceId, moveData.Id);
                    
                    var forgettingPokemon = await _pokemonManagementService.GetPokemonDetails(pokemonInstanceId);
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"{forgettingPokemon?.Species ?? "Pokemon"} forgot {moveData.Name}",
                        pokemonId = pokemonInstanceId,
                        species = forgettingPokemon?.Species,
                        forgottenMove = moveData.Name,
                        action = action
                    }, _jsonOptions);
                    
                case "check_evolution_eligibility":
                    var checkPokemon = await _pokemonManagementService.GetPokemonDetails(pokemonInstanceId);
                    var ownedPokemon = await _pokemonManagementService.GetOwnedPokemonDetails(pokemonInstanceId);
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        pokemonId = pokemonInstanceId,
                        species = checkPokemon?.Species,
                        level = checkPokemon?.Level ?? 0,
                        friendship = ownedPokemon?.Friendship ?? 0,
                        // In a full implementation, this would check evolution requirements
                        eligibleForEvolution = (checkPokemon?.Level ?? 0) >= 16, // Simplified check
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown Pokemon advancement action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in ManagePokemonAdvancement: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("manage_experience_and_rewards")]
    [Description("Handle experience point distribution and rewards from adventures")]
    public async Task<string> ManageExperienceAndRewards(
        [Description("Action: 'award_experience', 'distribute_team_exp', 'grant_rewards', 'calculate_exp_gain'")] string action,
        [Description("Amount of experience points")] int experiencePoints = 0,
        [Description("Target Pokemon ID (for individual exp awards)")] string pokemonInstanceId = "",
        [Description("Reason for the experience/reward")] string reason = "",
        [Description("Type of accomplishment: 'battle_victory', 'quest_completion', 'discovery', 'training', 'bonding'")] string accomplishmentType = "battle_victory",
        [Description("Additional rewards (items, money, etc.)")] Dictionary<string, int> additionalRewards = null)
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] ManageExperienceAndRewards called: {action}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            switch (action.ToLower())
            {
                case "award_experience":
                    if (experiencePoints <= 0)
                    {
                        return JsonSerializer.Serialize(new { error = "Experience points must be greater than 0" }, _jsonOptions);
                    }
                    
                    // Award experience to player
                    await _characterManagementService.AddPlayerExperiencePoints(experiencePoints);
                    
                    // Log the experience gain
                    await _informationManagementService.LogNarrativeEventAsync(
                        "experience_gained",
                        $"Player gained {experiencePoints} experience",
                        $"Through {accomplishmentType}, the player has gained {experiencePoints} experience points. {reason}",
                        new List<string> { "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Gained {experiencePoints} experience!",
                        experiencePoints = experiencePoints,
                        accomplishmentType = accomplishmentType,
                        reason = reason,
                        action = action
                    }, _jsonOptions);
                    
                case "distribute_team_exp":
                    if (experiencePoints <= 0)
                    {
                        return JsonSerializer.Serialize(new { error = "Experience points must be greater than 0" }, _jsonOptions);
                    }
                    
                    var player = await _characterManagementService.GetPlayerDetails();
                    var teamCount = player.TeamPokemon.Count;
                    
                    if (teamCount == 0)
                    {
                        return JsonSerializer.Serialize(new { error = "No Pokemon in team to award experience" }, _jsonOptions);
                    }
                    
                    var expPerPokemon = experiencePoints / teamCount;
                    var awardedPokemon = new List<object>();
                    
                    foreach (var teamMember in player.TeamPokemon)
                    {
                        await _pokemonManagementService.AddPokemonExperience(teamMember.Pokemon.Id, expPerPokemon);
                        awardedPokemon.Add(new 
                        { 
                            pokemonId = teamMember.Pokemon.Id,
                            species = teamMember.Pokemon.Species,
                            experienceGained = expPerPokemon
                        });
                    }
                    
                    // Log team experience distribution
                    await _informationManagementService.LogNarrativeEventAsync(
                        "team_experience_distributed",
                        $"Team gained {experiencePoints} experience",
                        $"The entire Pokemon team has grown stronger through {accomplishmentType}, with each member gaining {expPerPokemon} experience. {reason}",
                        new List<string> { "player", "team" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Team gained {experiencePoints} experience!",
                        totalExperience = experiencePoints,
                        expPerPokemon = expPerPokemon,
                        teamSize = teamCount,
                        awardedPokemon = awardedPokemon,
                        action = action
                    }, _jsonOptions);
                    
                case "grant_rewards":
                    var rewardResults = new List<object>();
                    
                    // Handle money rewards
                    if (additionalRewards?.ContainsKey("money") == true)
                    {
                        var moneyAmount = additionalRewards["money"];
                        await _characterManagementService.ChangePlayerMoney(moneyAmount);
                        rewardResults.Add(new { type = "money", amount = moneyAmount });
                    }
                    
                    // In a full implementation, would handle items and other rewards
                    
                    // Log the rewards
                    await _informationManagementService.LogNarrativeEventAsync(
                        "rewards_granted",
                        $"Player received rewards for {accomplishmentType}",
                        $"As a reward for {accomplishmentType}, the player has received various benefits. {reason}",
                        new List<string> { "player" },
                        gameState.CurrentLocationId,
                        null,
                        gameState.GameTurnNumber
                    );
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = "Rewards granted!",
                        rewards = rewardResults,
                        accomplishmentType = accomplishmentType,
                        reason = reason,
                        action = action
                    }, _jsonOptions);
                    
                case "calculate_exp_gain":
                    // Simplified experience calculation
                    var baseExp = accomplishmentType switch
                    {
                        "battle_victory" => 100,
                        "quest_completion" => 200,
                        "discovery" => 50,
                        "training" => 25,
                        "bonding" => 30,
                        _ => 50
                    };
                    
                    // Add some randomness using a simple random number instead of dice roll
                    var random = new Random();
                    var randomBonus = random.Next(1, 21) * 5; // Simulate 1d20 * 5
                    var calculatedExp = baseExp + randomBonus;
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        calculatedExperience = calculatedExp,
                        baseExperience = baseExp,
                        randomBonus = randomBonus,
                        accomplishmentType = accomplishmentType,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown experience action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in ManageExperienceAndRewards: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("finalize_level_up_phase")]
    [Description("Complete the level up phase and return to exploration")]
    public async Task<string> FinalizeLevelUpPhase(
        [Description("Summary of all advancement that occurred during this phase")] string levelUpSummary)
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] FinalizeLevelUpPhase called");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Set the phase back to Exploration
            gameState.CurrentPhase = GamePhase.Exploration;
            
            // Set the phase change summary
            gameState.PhaseChangeSummary = $"Level up phase completed. {levelUpSummary}";
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"Level Up Phase Completed: {levelUpSummary}" 
            });
            
            // Update save time
            gameState.LastSaveTime = DateTime.UtcNow;
            
            // Save the state
            await _gameStateRepo.SaveStateAsync(gameState);
            
            // Log the completion
            await _informationManagementService.LogNarrativeEventAsync(
                "level_up_phase_completed",
                "Level up phase completed successfully",
                $"The level up phase has been completed with the following advancements: {levelUpSummary}",
                new List<string> { "player" },
                gameState.CurrentLocationId,
                null,
                gameState.GameTurnNumber
            );
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Level up phase completed successfully",
                nextPhase = "Exploration",
                summary = levelUpSummary,
                sessionId = gameState.SessionId,
                phaseTransitionCompleted = true
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in FinalizeLevelUpPhase: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }
}