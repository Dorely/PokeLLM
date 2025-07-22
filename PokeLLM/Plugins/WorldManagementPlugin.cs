using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing world state, locations, time, weather, NPCs, and environmental interactions
/// Handles the dynamic world aspects of the Pokemon D&D campaign
/// </summary>
public class WorldManagementPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Random _random;

    public WorldManagementPlugin(IGameStateRepository repository)
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

    #region Location Management

    [KernelFunction("change_location")]
    [Description("Move the trainer to a new location, updating their position in the world. Triggers location-specific events and encounters. Example: change_location('Viridian City', 'Kanto', 'Traveled via Route 1') for story progression.")]
    public async Task<string> ChangeLocation(
        [Description("Name of the new location (city, route, building, etc.)")] string newLocation,
        [Description("Region the location is in")] string region,
        [Description("How/why the trainer moved to this location")] string reason = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] ChangeLocation called: newLocation={newLocation}, region={region}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var oldLocation = gameState.WorldState.CurrentLocation;
        var oldRegion = gameState.WorldState.CurrentRegion;

        await _repository.UpdateWorldStateAsync(worldState =>
        {
            worldState.CurrentLocation = newLocation;
            worldState.CurrentRegion = region;
            worldState.VisitedLocations.Add(newLocation);
        });

        var result = new
        {
            success = true,
            oldLocation = oldLocation,
            oldRegion = oldRegion,
            newLocation = newLocation,
            newRegion = region,
            reason = reason,
            message = $"Moved from {oldLocation} to {newLocation} in {region}. {reason}"
        };

        Debug.WriteLine($"[WorldManagementPlugin] Location changed: {oldLocation} -> {newLocation}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("set_time_and_weather")]
    [Description("Update the current time of day and weather conditions, affecting available activities and encounters. Example: set_time_and_weather('Night', 'Thunderstorm', 'Storm clouds gathering') for dramatic atmosphere.")]
    public async Task<string> SetTimeAndWeather(
        [Description("Time of day: Morning, Afternoon, Evening, Night")] string timeOfDay,
        [Description("Weather condition: Clear, Cloudy, Rain, Storm, Thunderstorm, Snow, Fog, Sandstorm, Sunny, Overcast")] string weather,
        [Description("Reason for the time/weather change")] string reason = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] SetTimeAndWeather called: timeOfDay={timeOfDay}, weather={weather}");
        
        if (!Enum.TryParse<TimeOfDay>(timeOfDay, true, out var timeEnum))
            return JsonSerializer.Serialize(new { error = "Invalid time of day" }, _jsonOptions);

        await _repository.UpdateWorldStateAsync(worldState =>
        {
            worldState.TimeOfDay = timeEnum;
            worldState.WeatherCondition = weather;
        });

        var result = new
        {
            success = true,
            timeOfDay = timeEnum.ToString(),
            weather = weather,
            reason = reason,
            message = $"Time set to {timeEnum}, weather changed to {weather}. {reason}"
        };

        Debug.WriteLine($"[WorldManagementPlugin] Time and weather updated: {timeEnum}, {weather}");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region NPC and Relationship Management

    [KernelFunction("update_npc_relationship")]
    [Description("Update relationship standing with a specific NPC based on interactions and story events. Affects dialogue options and quest availability. Example: update_npc_relationship('gym_leader_brock', 25, 'Impressed by battle skills') for positive interaction.")]
    public async Task<string> UpdateNpcRelationship(
        [Description("Unique identifier for the NPC")] string npcId,
        [Description("Change in relationship (-100 to +100, negative for worse relations)")] int relationshipChange,
        [Description("Reason for the relationship change")] string reason = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] UpdateNpcRelationship called: npcId={npcId}, change={relationshipChange}");
        
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            if (!worldState.NPCRelationships.ContainsKey(npcId))
            {
                worldState.NPCRelationships[npcId] = 0;
            }
            
            var oldValue = worldState.NPCRelationships[npcId];
            var newValue = Math.Max(-100, Math.Min(100, oldValue + relationshipChange));
            worldState.NPCRelationships[npcId] = newValue;
        });

        var gameState = await _repository.LoadLatestStateAsync();
        var currentRelationship = gameState!.WorldState.NPCRelationships.GetValueOrDefault(npcId, 0);
        
        var relationshipLevel = currentRelationship switch
        {
            >= 80 => "Beloved",
            >= 60 => "Trusted Friend",
            >= 40 => "Good Friend",
            >= 20 => "Friend",
            >= 0 => "Neutral",
            >= -20 => "Disliked",
            >= -40 => "Enemy",
            >= -60 => "Hated",
            _ => "Nemesis"
        };

        var result = new
        {
            success = true,
            npcId = npcId,
            relationshipChange = relationshipChange,
            currentRelationship = currentRelationship,
            relationshipLevel = relationshipLevel,
            reason = reason,
            message = $"Relationship with {npcId} changed by {relationshipChange} (now {currentRelationship}: {relationshipLevel}). {reason}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("update_faction_reputation")]
    [Description("Update standing with a faction or organization based on actions and story progression. Affects access to facilities and quest lines. Example: update_faction_reputation('Team Rocket', -50, 'Disrupted their operation') for antagonistic actions.")]
    public async Task<string> UpdateFactionReputation(
        [Description("Name of the faction/organization")] string factionName,
        [Description("Change in reputation (-100 to +100, negative for worse standing)")] int reputationChange,
        [Description("Reason for the reputation change")] string reason = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] UpdateFactionReputation called: factionName={factionName}, change={reputationChange}");
        
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            if (!worldState.FactionReputations.ContainsKey(factionName))
            {
                worldState.FactionReputations[factionName] = 0;
            }
            
            var oldValue = worldState.FactionReputations[factionName];
            var newValue = Math.Max(-100, Math.Min(100, oldValue + reputationChange));
            worldState.FactionReputations[factionName] = newValue;
        });

        var gameState = await _repository.LoadLatestStateAsync();
        var currentReputation = gameState!.WorldState.FactionReputations.GetValueOrDefault(factionName, 0);
        
        var reputationLevel = currentReputation switch
        {
            >= 80 => "Revered",
            >= 60 => "Highly Respected",
            >= 40 => "Respected",
            >= 20 => "Liked",
            >= 0 => "Neutral",
            >= -20 => "Disliked",
            >= -40 => "Hostile",
            >= -60 => "Enemy",
            _ => "Nemesis"
        };

        var result = new
        {
            success = true,
            faction = factionName,
            reputationChange = reputationChange,
            currentReputation = currentReputation,
            reputationLevel = reputationLevel,
            reason = reason,
            message = $"Reputation with {factionName} changed by {reputationChange} (now {currentReputation}: {reputationLevel}). {reason}"
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Achievement and Progress Management

    [KernelFunction("earn_gym_badge")]
    [Description("Award a gym badge for defeating a gym leader, representing major story milestones and character progression. Example: earn_gym_badge('Boulder Badge', 'Brock', 'Pewter City', 'Rock', 'Defeated with type advantage strategy') for first gym victory.")]
    public async Task<string> EarnGymBadge(
        [Description("Name of the gym badge")] string badgeName,
        [Description("Name of the gym leader")] string leaderName,
        [Description("Location of the gym")] string gymLocation,
        [Description("Type specialty of the gym")] string gymType,
        [Description("How the badge was earned")] string achievement = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] EarnGymBadge called: badgeName={badgeName}, leader={leaderName}");
        
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            // Avoid duplicates
            if (!worldState.GymBadges.Any(b => b.GymName.Equals(badgeName, StringComparison.OrdinalIgnoreCase)))
            {
                worldState.GymBadges.Add(new GymBadge
                {
                    GymName = badgeName,
                    LeaderName = leaderName,
                    Location = gymLocation,
                    BadgeType = gymType
                });
            }
        });

        var gameState = await _repository.LoadLatestStateAsync();
        var totalBadges = gameState!.WorldState.GymBadges.Count;

        var result = new
        {
            success = true,
            badgeName = badgeName,
            leaderName = leaderName,
            gymLocation = gymLocation,
            gymType = gymType,
            achievement = achievement,
            totalBadges = totalBadges,
            message = $"Earned the {badgeName} from {leaderName} at {gymLocation} Gym! ({totalBadges} total badges). {achievement}"
        };

        Debug.WriteLine($"[WorldManagementPlugin] Gym badge earned: {badgeName} ({totalBadges} total)");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("discover_lore")]
    [Description("Add discovered lore, history, or world knowledge to the trainer's understanding. Represents learning about the world through exploration and interaction. Example: discover_lore('Ancient Pokemon lived in these ruins', 'Explored mysterious cave') for world-building.")]
    public async Task<string> DiscoverLore(
        [Description("The lore/knowledge that was discovered")] string loreEntry,
        [Description("How this lore was discovered")] string discoveryMethod = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] DiscoverLore called: loreEntry={loreEntry}");
        
        await _repository.UpdateWorldStateAsync(worldState =>
        {
            // Avoid exact duplicates
            if (!worldState.DiscoveredLore.Contains(loreEntry))
            {
                worldState.DiscoveredLore.Add(loreEntry);
            }
        });

        var gameState = await _repository.LoadLatestStateAsync();
        var totalLore = gameState!.WorldState.DiscoveredLore.Count;

        var result = new
        {
            success = true,
            loreEntry = loreEntry,
            discoveryMethod = discoveryMethod,
            totalLoreEntries = totalLore,
            message = $"Discovered new lore: {loreEntry} ({discoveryMethod}). Total lore entries: {totalLore}"
        };

        Debug.WriteLine($"[WorldManagementPlugin] Lore discovered: {loreEntry} ({totalLore} total)");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion

    #region Inventory and Economy Management

    [KernelFunction("update_money")]
    [Description("Add or subtract money from the trainer's funds due to transactions, rewards, or expenses. Example: update_money(500, 'Sold valuable items') for positive income or update_money(-200, 'Bought Pokeballs') for purchases.")]
    public async Task<string> UpdateMoney(
        [Description("Amount to add (positive) or subtract (negative)")] int amount,
        [Description("Reason for the money change")] string reason = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] UpdateMoney called: amount={amount}, reason={reason}");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var oldMoney = gameState.Player.Character.Money;
        var newMoney = Math.Max(0, oldMoney + amount);

        if (amount < 0 && oldMoney < Math.Abs(amount))
        {
            return JsonSerializer.Serialize(new { error = "Insufficient funds", currentMoney = oldMoney, attempted = amount }, _jsonOptions);
        }

        await _repository.UpdatePlayerAsync(player =>
        {
            player.Character.Money = newMoney;
        });

        var result = new
        {
            success = true,
            change = amount,
            oldMoney = oldMoney,
            newMoney = newMoney,
            reason = reason,
            message = amount >= 0 ? 
                $"Gained ${amount}! New total: ${newMoney}. {reason}" :
                $"Spent ${Math.Abs(amount)}. Remaining: ${newMoney}. {reason}"
        };

        Debug.WriteLine($"[WorldManagementPlugin] Money updated: {oldMoney} -> {newMoney} ({amount:+#;-#;0})");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("add_to_inventory")]
    [Description("Add items to the trainer's inventory from purchases, finds, or rewards. Example: add_to_inventory('Pokeball', 5, 'Purchased from Pokemart') to stock up on catching supplies.")]
    public async Task<string> AddToInventory(
        [Description("Name of the item to add")] string itemName,
        [Description("Quantity to add")] int quantity,
        [Description("How the items were obtained")] string reason = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] AddToInventory called: itemName={itemName}, quantity={quantity}");
        
        if (quantity <= 0)
            return JsonSerializer.Serialize(new { error = "Quantity must be positive" }, _jsonOptions);

        await _repository.UpdatePlayerAsync(player =>
        {
            if (!player.Character.Inventory.ContainsKey(itemName))
            {
                player.Character.Inventory[itemName] = 0;
            }
            player.Character.Inventory[itemName] += quantity;
        });

        var gameState = await _repository.LoadLatestStateAsync();
        var newTotal = gameState!.Player.Character.Inventory.GetValueOrDefault(itemName, 0);

        var result = new
        {
            success = true,
            item = itemName,
            quantityAdded = quantity,
            newTotal = newTotal,
            reason = reason,
            message = $"Added {quantity} {itemName}(s) to inventory. Total: {newTotal}. {reason}"
        };

        Debug.WriteLine($"[WorldManagementPlugin] Added to inventory: {quantity}x {itemName} (total: {newTotal})");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("remove_from_inventory")]
    [Description("Remove items from the trainer's inventory due to usage, trading, or loss. Example: remove_from_inventory('Pokeball', 1, 'Used to catch Pikachu') when consuming items.")]
    public async Task<string> RemoveFromInventory(
        [Description("Name of the item to remove")] string itemName,
        [Description("Quantity to remove")] int quantity,
        [Description("Why the items were removed")] string reason = "")
    {
        Debug.WriteLine($"[WorldManagementPlugin] RemoveFromInventory called: itemName={itemName}, quantity={quantity}");
        
        if (quantity <= 0)
            return JsonSerializer.Serialize(new { error = "Quantity must be positive" }, _jsonOptions);

        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var currentQuantity = gameState.Player.Character.Inventory.GetValueOrDefault(itemName, 0);
        
        if (currentQuantity < quantity)
        {
            return JsonSerializer.Serialize(new { error = "Insufficient items", currentQuantity = currentQuantity, requested = quantity }, _jsonOptions);
        }

        await _repository.UpdatePlayerAsync(player =>
        {
            player.Character.Inventory[itemName] -= quantity;
            if (player.Character.Inventory[itemName] <= 0)
            {
                player.Character.Inventory.Remove(itemName);
            }
        });

        var newTotal = Math.Max(0, currentQuantity - quantity);

        var result = new
        {
            success = true,
            item = itemName,
            quantityRemoved = quantity,
            previousTotal = currentQuantity,
            newTotal = newTotal,
            reason = reason,
            message = $"Used {quantity} {itemName}(s). Remaining: {newTotal}. {reason}"
        };

        Debug.WriteLine($"[WorldManagementPlugin] Removed from inventory: {quantity}x {itemName} ({currentQuantity} -> {newTotal})");
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [KernelFunction("get_world_status")]
    [Description("Get comprehensive status of the world state including location, time, weather, and key progress indicators. Useful for setting scene context and tracking campaign progress.")]
    public async Task<string> GetWorldStatus()
    {
        Debug.WriteLine($"[WorldManagementPlugin] GetWorldStatus called");
        
        var gameState = await _repository.LoadLatestStateAsync();
        if (gameState == null)
            return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

        var result = new
        {
            location = new
            {
                current = gameState.WorldState.CurrentLocation,
                region = gameState.WorldState.CurrentRegion
            },
            time = new
            {
                timeOfDay = gameState.WorldState.TimeOfDay.ToString(),
                weather = gameState.WorldState.WeatherCondition
            },
            progress = new
            {
                trainerLevel = gameState.Player.Character.Level,
                gymBadges = gameState.WorldState.GymBadges.Count,
                badgeList = gameState.WorldState.GymBadges.Select(b => new { name = b.GymName, type = b.BadgeType, leader = b.LeaderName, location = b.Location }),
                loreEntries = gameState.WorldState.DiscoveredLore.Count,
                money = gameState.Player.Character.Money
            },
            relationships = new
            {
                npcCount = gameState.WorldState.NPCRelationships.Count,
                factionCount = gameState.WorldState.FactionReputations.Count
            },
            inventory = gameState.Player.Character.Inventory
        };

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    #endregion
}