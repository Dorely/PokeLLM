using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.Game.GameLogic;
using PokeLLM.GameState.Models;
using PokeLLM.GameLogic;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for handling generic level up utilities and phase management
/// </summary>
public class LevelUpPhasePlugin
{
    private readonly IGameStateRepository _gameStateRepo;
    private readonly ICharacterManagementService _characterManagementService;
    private readonly IEntityService _entityService;
    private readonly IGameLogicService _gameLogicService;
    private readonly JsonSerializerOptions _jsonOptions;

    public LevelUpPhasePlugin(
        IGameStateRepository gameStateRepo,
        ICharacterManagementService characterManagementService,
        IEntityService entityService,
        IGameLogicService gameLogicService)
    {
        _gameStateRepo = gameStateRepo;
        _characterManagementService = characterManagementService;
        _entityService = entityService;
        _gameLogicService = gameLogicService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("manage_generic_advancement")]
    [Description("Handle generic character advancement operations")]
    public async Task<string> ManageGenericAdvancement(
        [Description("Action: 'get_player_status', 'add_experience'")] string action,
        [Description("Amount of experience to add (for add_experience action)")] int experiencePoints = 0,
        [Description("Reason or context for the advancement")] string reason = "")
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] ManageGenericAdvancement called: {action}");
        
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            switch (action.ToLower())
            {
                case "get_player_status":
                    var playerDetails = await _characterManagementService.GetPlayerDetails();
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        playerLevel = playerDetails.Level,
                        playerExperience = playerDetails.Experience,
                        playerConditions = playerDetails.Conditions,
                        action = action
                    }, _jsonOptions);
                    
                case "add_experience":
                    if (experiencePoints <= 0)
                    {
                        return JsonSerializer.Serialize(new { error = "Experience points must be greater than 0" }, _jsonOptions);
                    }
                    
                    await _characterManagementService.AddPlayerExperiencePoints(experiencePoints);
                    
                    // Add to recent events
                    gameState.RecentEvents.Add(new EventLog 
                    { 
                        TurnNumber = gameState.GameTurnNumber, 
                        EventDescription = $"Player gained {experiencePoints} experience - {reason}" 
                    });
                    await _gameStateRepo.SaveStateAsync(gameState);
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Added {experiencePoints} experience points",
                        experiencePoints = experiencePoints,
                        reason = reason,
                        action = action
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown advancement action: {action}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in ManageGenericAdvancement: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, action = action }, _jsonOptions);
        }
    }

    [KernelFunction("generate_random_values")]
    [Description("Generate random values for level up rewards and calculations")]
    public async Task<string> GenerateRandomValues(
        [Description("Type of random generation: 'experience_bonus', 'skill_improvement', 'random_range'")] string randomType,
        [Description("Base value for calculations")] int baseValue = 0,
        [Description("Minimum value for range generation")] int minValue = 1,
        [Description("Maximum value for range generation")] int maxValue = 100)
    {
        Debug.WriteLine($"[LevelUpPhasePlugin] GenerateRandomValues called: {randomType}");
        
        try
        {
            var random = new Random();
            
            switch (randomType.ToLower())
            {
                case "experience_bonus":
                    var expBonus = random.Next(1, 21) * 5; // 1d20 * 5
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        randomValue = expBonus,
                        baseValue = baseValue,
                        totalValue = baseValue + expBonus,
                        randomType = randomType
                    }, _jsonOptions);
                    
                case "skill_improvement":
                    var skillBonus = random.Next(1, 7); // 1d6
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        randomValue = skillBonus,
                        randomType = randomType
                    }, _jsonOptions);
                    
                case "random_range":
                    if (minValue >= maxValue)
                    {
                        return JsonSerializer.Serialize(new { error = "Maximum value must be greater than minimum value" }, _jsonOptions);
                    }
                    var rangeValue = random.Next(minValue, maxValue + 1);
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        randomValue = rangeValue,
                        minValue = minValue,
                        maxValue = maxValue,
                        randomType = randomType
                    }, _jsonOptions);
                    
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown random type: {randomType}" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LevelUpPhasePlugin] Error in GenerateRandomValues: {ex.Message}");
            return JsonSerializer.Serialize(new { error = ex.Message, randomType = randomType }, _jsonOptions);
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
            
            // Note: Event logging is now handled through VectorPlugin's manage_vector_store function
            
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

    #region Core Required Functions from Todo List

    [KernelFunction("award_experience")]
    [Description("Grant experience points to characters with level threshold checking")]
    public async Task<string> AwardExperience(
        [Description("Amount of experience points to award")] int experiencePoints,
        [Description("Reason for awarding XP (for logging)")] string reason = "",
        [Description("Target character ID (defaults to player)")] string targetId = "player")
    {
        try
        {
            if (experiencePoints <= 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Experience points must be greater than 0" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // For now, focus on player - could be extended to support party members
            if (targetId.ToLower() != "player")
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Only player XP is currently supported" 
                }, _jsonOptions);
            }

            var previousXP = gameState.Player.Experience;
            var previousLevel = gameState.Player.Level;
            
            // Award the experience
            await _characterManagementService.AddPlayerExperiencePoints(experiencePoints);
            
            // Reload to get updated values
            var updatedGameState = await _gameStateRepo.LoadLatestStateAsync();
            var newXP = updatedGameState.Player.Experience;
            var newLevel = updatedGameState.Player.Level;
            
            var leveledUp = newLevel > previousLevel;
            var xpToNextLevel = CalculateXPToNextLevel(newLevel, newXP);
            
            // Add to recent events
            var eventDescription = $"Awarded {experiencePoints} XP: {reason}";
            if (leveledUp)
            {
                eventDescription += $" (Leveled up from {previousLevel} to {newLevel}!)";
            }
            
            updatedGameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = updatedGameState.GameTurnNumber, 
                EventDescription = eventDescription 
            });
            
            await _gameStateRepo.SaveStateAsync(updatedGameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = leveledUp ? 
                    $"Awarded {experiencePoints} XP and leveled up from {previousLevel} to {newLevel}!" :
                    $"Awarded {experiencePoints} XP",
                experienceAwarded = new
                {
                    amount = experiencePoints,
                    reason = reason,
                    previousXP = previousXP,
                    newXP = newXP,
                    previousLevel = previousLevel,
                    newLevel = newLevel,
                    leveledUp = leveledUp,
                    xpToNextLevel = xpToNextLevel
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error awarding experience: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("get_shop_inventory")]
    [Description("Display available items for purchase at current location")]
    public async Task<string> GetShopInventory(
        [Description("Optional specific location ID (defaults to current location)")] string locationId = "")
    {
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var targetLocationId = string.IsNullOrEmpty(locationId) ? gameState.CurrentLocationId : locationId;
            
            if (string.IsNullOrEmpty(targetLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "No location specified and player location not found" 
                }, _jsonOptions);
            }

            // Check if location exists
            if (!gameState.WorldLocations.ContainsKey(targetLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Location '{targetLocationId}' not found" 
                }, _jsonOptions);
            }

            // Get location data
            var locationJson = JsonSerializer.Serialize(gameState.WorldLocations[targetLocationId]);
            var locationData = JsonSerializer.Deserialize<Dictionary<string, object>>(locationJson);
            
            // Check if location has a shop
            if (!locationData.ContainsKey("shop") && !locationData.ContainsKey("shopInventory"))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"No shop available at {locationData.GetValueOrDefault("name", targetLocationId)}" 
                }, _jsonOptions);
            }

            // Get shop inventory
            var shopInventory = new List<object>();
            
            if (locationData.ContainsKey("shopInventory"))
            {
                var inventoryElement = locationData["shopInventory"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<object>());
                if (inventoryElement.ValueKind == JsonValueKind.Array)
                {
                    shopInventory = JsonSerializer.Deserialize<List<object>>(inventoryElement) ?? new List<object>();
                }
            }
            
            var locationName = locationData.ContainsKey("name") ? locationData["name"]?.ToString() : targetLocationId;

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                locationId = targetLocationId,
                locationName = locationName,
                shopInventory = shopInventory,
                totalItems = shopInventory.Count,
                message = shopInventory.Count > 0 ? 
                    $"Shop at {locationName} has {shopInventory.Count} items available" :
                    $"Shop at {locationName} is currently empty"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error retrieving shop inventory: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("buy_item")]
    [Description("Execute item purchase transactions with validation")]
    public async Task<string> BuyItem(
        [Description("ID of the item to purchase")] string itemId,
        [Description("Quantity to purchase")] int quantity = 1,
        [Description("Location ID where purchase is taking place (defaults to current)")] string locationId = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Item ID cannot be null or empty" 
                }, _jsonOptions);
            }

            if (quantity <= 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Quantity must be greater than 0" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            var targetLocationId = string.IsNullOrEmpty(locationId) ? gameState.CurrentLocationId : locationId;
            
            // Check if location has shop with the item
            if (!gameState.WorldLocations.ContainsKey(targetLocationId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Location '{targetLocationId}' not found" 
                }, _jsonOptions);
            }

            var locationJson = JsonSerializer.Serialize(gameState.WorldLocations[targetLocationId]);
            var locationData = JsonSerializer.Deserialize<Dictionary<string, object>>(locationJson);
            
            if (!locationData.ContainsKey("shopInventory"))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "No shop available at this location" 
                }, _jsonOptions);
            }

            // Find the item in shop inventory
            var inventoryElement = locationData["shopInventory"] as JsonElement? ?? JsonSerializer.SerializeToElement(new List<object>());
            var shopInventory = inventoryElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<Dictionary<string, object>>>(inventoryElement) ?? new List<Dictionary<string, object>>()
                : new List<Dictionary<string, object>>();

            var item = shopInventory.FirstOrDefault(i => 
                i.ContainsKey("itemId") && i["itemId"]?.ToString() == itemId);

            if (item == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Item '{itemId}' not available in shop" 
                }, _jsonOptions);
            }

            // Get item details
            var itemName = item.GetValueOrDefault("name", itemId)?.ToString() ?? itemId;
            var itemPrice = 0;
            
            if (item.ContainsKey("price"))
            {
                var priceElement = item["price"] as JsonElement? ?? JsonSerializer.SerializeToElement(0);
                itemPrice = priceElement.ValueKind == JsonValueKind.Number ? priceElement.GetInt32() : 0;
            }

            var totalCost = itemPrice * quantity;
            
            // Check if player has enough money
            var playerMoney = 0;
            if (gameState.RulesetGameData.ContainsKey("playerMoney"))
            {
                var moneyElement = gameState.RulesetGameData["playerMoney"];
                playerMoney = moneyElement.ValueKind == JsonValueKind.Number ? moneyElement.GetInt32() : 0;
            }

            if (playerMoney < totalCost)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Insufficient funds. Need {totalCost} gold, but only have {playerMoney} gold"
                }, _jsonOptions);
            }

            // Deduct money
            gameState.RulesetGameData["playerMoney"] = JsonSerializer.SerializeToElement(playerMoney - totalCost);
            
            // Add item to player inventory
            if (!gameState.RulesetGameData.ContainsKey("playerInventory"))
            {
                gameState.RulesetGameData["playerInventory"] = JsonSerializer.SerializeToElement(new List<object>());
            }

            var inventoryJsonElement = gameState.RulesetGameData["playerInventory"];
            var playerInventory = inventoryJsonElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<Dictionary<string, object>>>(inventoryJsonElement) ?? new List<Dictionary<string, object>>()
                : new List<Dictionary<string, object>>();

            // Check if player already has this item
            var existingItem = playerInventory.FirstOrDefault(i => 
                i.ContainsKey("itemId") && i["itemId"]?.ToString() == itemId);

            if (existingItem != null)
            {
                // Increase quantity
                var currentQuantity = 0;
                if (existingItem.ContainsKey("quantity"))
                {
                    var qtyElement = existingItem["quantity"] as JsonElement? ?? JsonSerializer.SerializeToElement(0);
                    currentQuantity = qtyElement.ValueKind == JsonValueKind.Number ? qtyElement.GetInt32() : 0;
                }
                existingItem["quantity"] = currentQuantity + quantity;
            }
            else
            {
                // Add new item
                var newItem = new Dictionary<string, object>
                {
                    ["itemId"] = itemId,
                    ["name"] = itemName,
                    ["quantity"] = quantity,
                    ["purchasePrice"] = itemPrice
                };
                playerInventory.Add(newItem);
            }

            gameState.RulesetGameData["playerInventory"] = JsonSerializer.SerializeToElement(playerInventory);
            
            // Add to recent events
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = $"Purchased {quantity}x {itemName} for {totalCost} gold" 
            });
            
            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Successfully purchased {quantity}x {itemName} for {totalCost} gold",
                purchase = new
                {
                    itemId = itemId,
                    itemName = itemName,
                    quantity = quantity,
                    unitPrice = itemPrice,
                    totalCost = totalCost,
                    remainingMoney = playerMoney - totalCost
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error purchasing item: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("equip_item")]
    [Description("Equip items to character equipment slots")]
    public async Task<string> EquipItem(
        [Description("ID of the item to equip")] string itemId,
        [Description("Equipment slot (weapon, armor, accessory, etc.)")] string slot = "",
        [Description("Whether to force equip even if slot is occupied")] bool forceEquip = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Item ID cannot be null or empty" 
                }, _jsonOptions);
            }

            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            // Get player inventory
            if (!gameState.RulesetGameData.ContainsKey("playerInventory"))
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = "Player has no inventory" 
                }, _jsonOptions);
            }

            var inventoryElement = gameState.RulesetGameData["playerInventory"];
            var playerInventory = inventoryElement.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<Dictionary<string, object>>>(inventoryElement) ?? new List<Dictionary<string, object>>()
                : new List<Dictionary<string, object>>();

            // Find the item
            var item = playerInventory.FirstOrDefault(i => 
                i.ContainsKey("itemId") && i["itemId"]?.ToString() == itemId);

            if (item == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false,
                    error = $"Item '{itemId}' not found in inventory" 
                }, _jsonOptions);
            }

            var itemName = item.GetValueOrDefault("name", itemId)?.ToString() ?? itemId;
            
            // Determine slot if not specified
            if (string.IsNullOrWhiteSpace(slot))
            {
                if (item.ContainsKey("slot"))
                {
                    slot = item["slot"]?.ToString() ?? "accessory";
                }
                else
                {
                    slot = "accessory"; // Default slot
                }
            }

            // Initialize equipment if not present
            if (!gameState.RulesetGameData.ContainsKey("playerEquipment"))
            {
                gameState.RulesetGameData["playerEquipment"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>());
            }

            var equipmentElement = gameState.RulesetGameData["playerEquipment"];
            var playerEquipment = equipmentElement.ValueKind == JsonValueKind.Object 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(equipmentElement) ?? new Dictionary<string, object>()
                : new Dictionary<string, object>();

            // Check if slot is occupied
            var previousItem = "";
            if (playerEquipment.ContainsKey(slot))
            {
                var currentItemElement = playerEquipment[slot] as JsonElement?;
                if (currentItemElement.HasValue && currentItemElement.Value.ValueKind != JsonValueKind.Null)
                {
                    var currentItemData = JsonSerializer.Deserialize<Dictionary<string, object>>(currentItemElement.Value);
                    previousItem = currentItemData?.GetValueOrDefault("name", "unknown item")?.ToString() ?? "unknown item";
                    
                    if (!forceEquip)
                    {
                        return JsonSerializer.Serialize(new { 
                            success = false,
                            error = $"Slot '{slot}' is already occupied by {previousItem}. Use forceEquip=true to replace." 
                        }, _jsonOptions);
                    }
                }
            }

            // Equip the item
            var equippedItem = new Dictionary<string, object>
            {
                ["itemId"] = itemId,
                ["name"] = itemName,
                ["slot"] = slot,
                ["equippedAt"] = DateTime.UtcNow.ToString("O")
            };

            // Copy any bonus stats from the item
            if (item.ContainsKey("bonuses"))
            {
                equippedItem["bonuses"] = item["bonuses"];
            }

            playerEquipment[slot] = equippedItem;
            gameState.RulesetGameData["playerEquipment"] = JsonSerializer.SerializeToElement(playerEquipment);
            
            // Add to recent events
            var eventMessage = string.IsNullOrEmpty(previousItem) 
                ? $"Equipped {itemName} in {slot} slot"
                : $"Equipped {itemName} in {slot} slot (replaced {previousItem})";
                
            gameState.RecentEvents.Add(new EventLog 
            { 
                TurnNumber = gameState.GameTurnNumber, 
                EventDescription = eventMessage 
            });
            
            await _gameStateRepo.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = eventMessage,
                equipment = new
                {
                    itemId = itemId,
                    itemName = itemName,
                    slot = slot,
                    previousItem = previousItem,
                    wasReplaced = !string.IsNullOrEmpty(previousItem)
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error equipping item: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    [KernelFunction("get_character_bonuses")]
    [Description("Calculate total character bonuses from all sources")]
    public async Task<string> GetCharacterBonuses(
        [Description("Type of bonuses to calculate (all, combat, skills, stats)")] string bonusType = "all")
    {
        try
        {
            var gameState = await _gameStateRepo.LoadLatestStateAsync();
            
            var totalBonuses = new Dictionary<string, int>();
            var bonusSources = new List<object>();

            // Get equipment bonuses
            if (gameState.RulesetGameData.ContainsKey("playerEquipment"))
            {
                var equipmentElement = gameState.RulesetGameData["playerEquipment"];
                if (equipmentElement.ValueKind == JsonValueKind.Object)
                {
                    var playerEquipment = JsonSerializer.Deserialize<Dictionary<string, object>>(equipmentElement) ?? new Dictionary<string, object>();
                    
                    foreach (var slot in playerEquipment)
                    {
                        var itemElement = slot.Value as JsonElement? ?? JsonSerializer.SerializeToElement(new Dictionary<string, object>());
                        if (itemElement.ValueKind == JsonValueKind.Object)
                        {
                            var itemData = JsonSerializer.Deserialize<Dictionary<string, object>>(itemElement) ?? new Dictionary<string, object>();
                            
                            if (itemData.ContainsKey("bonuses"))
                            {
                                var bonusesElement = itemData["bonuses"] as JsonElement? ?? JsonSerializer.SerializeToElement(new Dictionary<string, object>());
                                if (bonusesElement.ValueKind == JsonValueKind.Object)
                                {
                                    var itemBonuses = JsonSerializer.Deserialize<Dictionary<string, int>>(bonusesElement) ?? new Dictionary<string, int>();
                                    var itemName = itemData.GetValueOrDefault("name", "unknown")?.ToString() ?? "unknown";
                                    
                                    foreach (var bonus in itemBonuses)
                                    {
                                        if (totalBonuses.ContainsKey(bonus.Key))
                                            totalBonuses[bonus.Key] += bonus.Value;
                                        else
                                            totalBonuses[bonus.Key] = bonus.Value;
                                    }
                                    
                                    bonusSources.Add(new
                                    {
                                        source = $"Equipment: {itemName}",
                                        slot = slot.Key,
                                        bonuses = itemBonuses
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // Get condition bonuses
            var activeConditions = gameState.Player.Conditions;
            foreach (var condition in activeConditions)
            {
                // For demonstration, add simple condition bonuses
                // In a real implementation, this would reference the ruleset
                if (condition.ToLower().Contains("blessed"))
                {
                    totalBonuses["luck"] = totalBonuses.GetValueOrDefault("luck", 0) + 2;
                    bonusSources.Add(new
                    {
                        source = "Condition: Blessed",
                        bonuses = new Dictionary<string, int> { ["luck"] = 2 }
                    });
                }
            }

            // Filter by bonus type if specified
            var filteredBonuses = totalBonuses;
            if (bonusType.ToLower() != "all")
            {
                filteredBonuses = new Dictionary<string, int>();
                foreach (var bonus in totalBonuses)
                {
                    var includeBonus = bonusType.ToLower() switch
                    {
                        "combat" => bonus.Key.Contains("attack") || bonus.Key.Contains("damage") || bonus.Key.Contains("defense"),
                        "skills" => bonus.Key.Contains("skill") || bonus.Key.Contains("check"),
                        "stats" => bonus.Key.Contains("strength") || bonus.Key.Contains("dexterity") || 
                                  bonus.Key.Contains("constitution") || bonus.Key.Contains("intelligence") ||
                                  bonus.Key.Contains("wisdom") || bonus.Key.Contains("charisma"),
                        _ => true
                    };
                    
                    if (includeBonus)
                    {
                        filteredBonuses[bonus.Key] = bonus.Value;
                    }
                }
            }

            return JsonSerializer.Serialize(new 
            { 
                success = true,
                bonusType = bonusType,
                totalBonuses = filteredBonuses,
                bonusSources = bonusSources,
                totalBonusCount = filteredBonuses.Count,
                message = filteredBonuses.Count > 0 ? 
                    $"Character has {filteredBonuses.Count} active bonuses" :
                    "Character has no active bonuses"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false,
                error = $"Error calculating character bonuses: {ex.Message}" 
            }, _jsonOptions);
        }
    }

    #endregion

    #region Helper Methods

    private int CalculateXPToNextLevel(int currentLevel, int currentXP)
    {
        // Simple XP progression formula: level * 1000
        // Real implementation would use ruleset-specific formulas
        var xpForNextLevel = (currentLevel + 1) * 1000;
        return Math.Max(0, xpForNextLevel - currentXP);
    }

    #endregion
}