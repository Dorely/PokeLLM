using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Core GameEnginePlugin that provides essential game state management functions.
/// Handles Pokemon management, character state, world state, and game progression.
/// </summary>
public class GameStatePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameStatePlugin(IGameStateRepository repository)
    {
        _repository = repository;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [KernelFunction("update_entity")]
    [Description("Modifies the state of a character, the player, or a Pokémon. Use this to change stats, level, inventory, money, relationships, or current health (vigor).")]
    public async Task<string> UpdateEntity(
            [Description("The unique ID of the character or Pokémon to update (e.g., 'player', 'char_prof_oak', 'pkmn_inst_001_pidgey').")]
            string entityId,
            [Description("A JSON object string containing only the fields to be updated. For example: '{\"level\": 5, \"money\": 3500}' or '{\"currentVigor\": 15}'.")]
            string updates,
            [Description("A JSON object string representing deltas for NPC relationships. For example: '{\\\"char_gary_oak\\\": -10}' to decrease a relationship by 10.")]
            string npcRelationshipDeltas = "{}",
            [Description("A JSON object string representing deltas for Faction reputations. For example: '{\\\"faction_team_rocket\\\": 20}' to increase reputation by 20.")]
            string factionRelationshipDeltas = "{}")
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] UpdateEntity called for {entityId}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Parse the updates
            var updateDict = JsonSerializer.Deserialize<Dictionary<string, object>>(updates);
            var npcDeltas = JsonSerializer.Deserialize<Dictionary<string, int>>(npcRelationshipDeltas);
            var factionDeltas = JsonSerializer.Deserialize<Dictionary<string, int>>(factionRelationshipDeltas);

            bool updated = false;

            // Handle player updates
            if (entityId == "player")
            {
                updated = UpdatePlayerCharacter(gameState.Player, updateDict);
                
                // Update NPC relationships
                if (npcDeltas != null && npcDeltas.Count > 0)
                {
                    foreach (var kvp in npcDeltas)
                    {
                        if (gameState.Player.PlayerNpcRelationships.ContainsKey(kvp.Key))
                        {
                            gameState.Player.PlayerNpcRelationships[kvp.Key] = Math.Clamp(
                                gameState.Player.PlayerNpcRelationships[kvp.Key] + kvp.Value, -100, 100);
                        }
                        else
                        {
                            gameState.Player.PlayerNpcRelationships[kvp.Key] = Math.Clamp(kvp.Value, -100, 100);
                        }
                        updated = true;
                    }
                }

                // Update faction relationships
                if (factionDeltas != null && factionDeltas.Count > 0)
                {
                    foreach (var kvp in factionDeltas)
                    {
                        if (gameState.Player.PlayerFactionRelationships.ContainsKey(kvp.Key))
                        {
                            gameState.Player.PlayerFactionRelationships[kvp.Key] = Math.Clamp(
                                gameState.Player.PlayerFactionRelationships[kvp.Key] + kvp.Value, -100, 100);
                        }
                        else
                        {
                            gameState.Player.PlayerFactionRelationships[kvp.Key] = Math.Clamp(kvp.Value, -100, 100);
                        }
                        updated = true;
                    }
                }
            }
            // Handle NPC updates
            else if (gameState.WorldNpcs.ContainsKey(entityId))
            {
                updated = UpdateCharacter(gameState.WorldNpcs[entityId], updateDict);
            }
            // Handle Pokemon updates (owned by player)
            else if (entityId.StartsWith("pkmn_inst_"))
            {
                // Check player's team pokemon
                var teamPokemon = gameState.Player.TeamPokemon.FirstOrDefault(p => p.Pokemon.Id == entityId);
                if (teamPokemon != null)
                {
                    updated = UpdatePokemon(teamPokemon.Pokemon, updateDict);
                    if (updateDict.ContainsKey("experience"))
                    {
                        teamPokemon.Experience = Convert.ToInt32(updateDict["experience"]);
                        var newLevel = CalculateLevelFromExperience(teamPokemon.Experience);
                        if (newLevel > teamPokemon.Pokemon.Level)
                        {
                            teamPokemon.AvailableStatPoints += (newLevel - teamPokemon.Pokemon.Level);
                            teamPokemon.Pokemon.Level = newLevel;
                        }
                        updated = true;
                    }
                }
                else
                {
                    // Check player's boxed pokemon
                    var boxedPokemon = gameState.Player.BoxedPokemon.FirstOrDefault(p => p.Pokemon.Id == entityId);
                    if (boxedPokemon != null)
                    {
                        updated = UpdatePokemon(boxedPokemon.Pokemon, updateDict);
                        if (updateDict.ContainsKey("experience"))
                        {
                            boxedPokemon.Experience = Convert.ToInt32(updateDict["experience"]);
                            var newLevel = CalculateLevelFromExperience(boxedPokemon.Experience);
                            if (newLevel > boxedPokemon.Pokemon.Level)
                            {
                                boxedPokemon.AvailableStatPoints += (newLevel - boxedPokemon.Pokemon.Level);
                                boxedPokemon.Pokemon.Level = newLevel;
                            }
                            updated = true;
                        }
                    }
                    else
                    {
                        // Check world pokemon
                        if (gameState.WorldPokemon.ContainsKey(entityId))
                        {
                            updated = UpdatePokemon(gameState.WorldPokemon[entityId], updateDict);
                        }
                    }
                }
            }

            if (updated)
            {
                gameState.LastSaveTime = DateTime.UtcNow;
                await _repository.SaveStateAsync(gameState);
                return JsonSerializer.Serialize(new { success = true, message = $"Entity {entityId} updated successfully" }, _jsonOptions);
            }
            else
            {
                return JsonSerializer.Serialize(new { error = $"Entity {entityId} not found or no valid updates provided" }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] UpdateEntity error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to update entity: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("update_inventory")]
    [Description("Adds or removes items from a character's inventory. Use this for giving rewards, picking up items, using items, or buying/selling.")]
    public async Task<string> UpdateInventory(
        [Description("The unique ID of the character whose inventory is changing (e.g., 'player').")]
            string characterId,
        [Description("A JSON string representing a list of items to add. Example: '[{\"itemId\": \"item_potion\", \"quantity\": 2}]'.")]
            string itemsToAdd,
        [Description("A JSON string representing a list of items to remove. Example: '[{\"itemId\": \"item_pokeball\", \"quantity\": 1}]'.")]
            string itemsToRemove)
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] UpdateInventory called for {characterId}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            List<ItemInstance> inventory = null;

            // Get the character's inventory
            if (characterId == "player")
            {
                inventory = gameState.Player.Character.Inventory;
            }
            else if (gameState.WorldNpcs.ContainsKey(characterId))
            {
                inventory = gameState.WorldNpcs[characterId].Inventory;
            }
            else
            {
                return JsonSerializer.Serialize(new { error = $"Character {characterId} not found" }, _jsonOptions);
            }

            // Parse items to add
            if (!string.IsNullOrEmpty(itemsToAdd) && itemsToAdd != "[]")
            {
                var itemsAdd = JsonSerializer.Deserialize<List<ItemInstance>>(itemsToAdd);
                foreach (var item in itemsAdd)
                {
                    var existingItem = inventory.FirstOrDefault(i => i.ItemId == item.ItemId);
                    if (existingItem != null)
                    {
                        existingItem.Quantity += item.Quantity;
                    }
                    else
                    {
                        inventory.Add(new ItemInstance { ItemId = item.ItemId, Quantity = item.Quantity });
                    }
                }
            }

            // Parse items to remove
            if (!string.IsNullOrEmpty(itemsToRemove) && itemsToRemove != "[]")
            {
                var itemsRem = JsonSerializer.Deserialize<List<ItemInstance>>(itemsToRemove);
                foreach (var item in itemsRem)
                {
                    var existingItem = inventory.FirstOrDefault(i => i.ItemId == item.ItemId);
                    if (existingItem != null)
                    {
                        existingItem.Quantity -= item.Quantity;
                        if (existingItem.Quantity <= 0)
                        {
                            inventory.Remove(existingItem);
                        }
                    }
                }
            }

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);
            
            return JsonSerializer.Serialize(new { success = true, message = $"Inventory updated for {characterId}" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] UpdateInventory error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to update inventory: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("create_new_entity")]
    [Description("Creates a new character or Pokémon and adds it to the world. Use when the story introduces a new, persistent entity. The system will assign and return a new unique ID.")]
    public async Task<string> CreateNewEntity(
        [Description("The type of entity to create. Must be either 'Character' or 'Pokemon'.")]
            string entityType,
        [Description("A JSON object string with the initial properties for the new entity (e.g., '{\"name\": \"Bob the Merchant\", \"isTrainer\": false}' or '{\"species\": \"Rattata\", \"level\": 3}').")]
            string properties)
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] CreateNewEntity called for {entityType}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (entityType.Equals("Character", StringComparison.OrdinalIgnoreCase))
            {
                var props = JsonSerializer.Deserialize<Dictionary<string, object>>(properties);
                var character = new Character();
                
                // Generate unique ID
                var name = props.ContainsKey("name") ? props["name"].ToString() : "Unknown";
                var baseId = $"char_{name.ToLower().Replace(" ", "_")}";
                var id = baseId;
                int counter = 1;
                while (gameState.WorldNpcs.ContainsKey(id))
                {
                    id = $"{baseId}_{counter}";
                    counter++;
                }
                character.Id = id;

                // Apply properties
                ApplyCharacterProperties(character, props);

                gameState.WorldNpcs[character.Id] = character;
                gameState.LastSaveTime = DateTime.UtcNow;
                await _repository.SaveStateAsync(gameState);

                return JsonSerializer.Serialize(new { success = true, entityId = character.Id, message = $"Character {character.Name} created with ID {character.Id}" }, _jsonOptions);
            }
            else if (entityType.Equals("Pokemon", StringComparison.OrdinalIgnoreCase))
            {
                var props = JsonSerializer.Deserialize<Dictionary<string, object>>(properties);
                var pokemon = new Pokemon();
                
                // Generate unique ID
                var species = props.ContainsKey("species") ? props["species"].ToString() : "unknown";
                var baseId = $"pkmn_inst_{species.ToLower()}";
                var id = baseId;
                int counter = 1;
                while (gameState.WorldPokemon.ContainsKey(id))
                {
                    id = $"{baseId}_{counter:D3}";
                    counter++;
                }
                pokemon.Id = id;

                // Apply properties
                ApplyPokemonProperties(pokemon, props);

                gameState.WorldPokemon[pokemon.Id] = pokemon;
                gameState.LastSaveTime = DateTime.UtcNow;
                await _repository.SaveStateAsync(gameState);

                return JsonSerializer.Serialize(new { success = true, entityId = pokemon.Id, message = $"Pokemon {pokemon.Species} created with ID {pokemon.Id}" }, _jsonOptions);
            }
            else
            {
                return JsonSerializer.Serialize(new { error = $"Invalid entity type: {entityType}. Must be 'Character' or 'Pokemon'." }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] CreateNewEntity error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to create entity: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("move_entity_to_location")]
    [Description("Moves a character or Pokémon to a new location. For the player, this changes their current location. For NPCs, it makes them appear in a different place.")]
    public async Task<string> MoveEntityToLocation(
        [Description("The unique ID of the character or Pokémon to move (e.g., 'player', 'char_prof_oak').")]
            string entityId,
        [Description("The unique ID of the destination location (e.g., 'loc_route_1').")]
            string locationId,
        [Description("Optional: The ID of a specific point of interest within the location to move to (e.g., 'poi_oaks_lab').")]
            string pointOfInterestId = null)
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] MoveEntityToLocation called for {entityId} to {locationId}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Verify destination location exists
            if (!gameState.WorldLocations.ContainsKey(locationId))
            {
                return JsonSerializer.Serialize(new { error = $"Location {locationId} not found" }, _jsonOptions);
            }

            var location = gameState.WorldLocations[locationId];

            // Verify point of interest if specified
            if (!string.IsNullOrEmpty(pointOfInterestId) && !location.PointsOfInterest.ContainsKey(pointOfInterestId))
            {
                return JsonSerializer.Serialize(new { error = $"Point of interest {pointOfInterestId} not found in location {locationId}" }, _jsonOptions);
            }

            if (entityId == "player")
            {
                // Move player
                var previousLocation = gameState.CurrentLocationId;
                gameState.CurrentLocationId = locationId;
                
                // Add to recent events
                var moveEvent = string.IsNullOrEmpty(pointOfInterestId) 
                    ? $"Player moved from {previousLocation} to {locationId}"
                    : $"Player moved from {previousLocation} to {locationId} ({pointOfInterestId})";
                gameState.RecentEvents.Add(moveEvent);
                
                // Keep only last 10 events
                if (gameState.RecentEvents.Count > 10)
                {
                    gameState.RecentEvents.RemoveAt(0);
                }
            }
            else if (gameState.WorldNpcs.ContainsKey(entityId))
            {
                // Remove NPC from all locations
                foreach (var loc in gameState.WorldLocations.Values)
                {
                    loc.PresentNpcIds.Remove(entityId);
                }
                
                // Add NPC to new location
                location.PresentNpcIds.Add(entityId);
            }
            else if (gameState.WorldPokemon.ContainsKey(entityId))
            {
                // Remove Pokemon from all locations
                foreach (var loc in gameState.WorldLocations.Values)
                {
                    loc.PresentPokemonIds.Remove(entityId);
                }
                
                // Add Pokemon to new location
                location.PresentPokemonIds.Add(entityId);
            }
            else
            {
                return JsonSerializer.Serialize(new { error = $"Entity {entityId} not found" }, _jsonOptions);
            }

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            var poiMessage = string.IsNullOrEmpty(pointOfInterestId) ? "" : $" at {pointOfInterestId}";
            return JsonSerializer.Serialize(new { success = true, message = $"Entity {entityId} moved to {locationId}{poiMessage}" }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] MoveEntityToLocation error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to move entity: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("initiate_pokemon_capture")]
    [Description("Attempts to catch a wild Pokémon with a Poké Ball. The system will determine the outcome based on game rules.")]
    public async Task<string> InitiatePokemonCapture(
        [Description("The ID of the trainer attempting the capture (usually 'player').")]
            string trainerId,
        [Description("The unique ID of the wild Pokémon being targeted for capture.")]
            string targetPokemonId,
        [Description("The item ID of the Poké Ball being used (e.g., 'item_pokeball', 'item_greatball').")]
            string pokeballItemId,
        [Description("An optional custom catch percentage modifier to be added for narrative purposes")]
            int? catchModifier)
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] InitiatePokemonCapture called for {targetPokemonId}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Verify the pokemon exists and is wild
            if (!gameState.WorldPokemon.ContainsKey(targetPokemonId))
            {
                return JsonSerializer.Serialize(new { error = $"Wild Pokemon {targetPokemonId} not found" }, _jsonOptions);
            }

            var targetPokemon = gameState.WorldPokemon[targetPokemonId];

            // Verify trainer has the pokeball
            List<ItemInstance> inventory = null;
            if (trainerId == "player")
            {
                inventory = gameState.Player.Character.Inventory;
            }
            else if (gameState.WorldNpcs.ContainsKey(trainerId))
            {
                inventory = gameState.WorldNpcs[trainerId].Inventory;
            }
            else
            {
                return JsonSerializer.Serialize(new { error = $"Trainer {trainerId} not found" }, _jsonOptions);
            }

            var pokeball = inventory.FirstOrDefault(i => i.ItemId == pokeballItemId);
            if (pokeball == null || pokeball.Quantity <= 0)
            {
                return JsonSerializer.Serialize(new { error = $"No {pokeballItemId} available" }, _jsonOptions);
            }

            // Calculate catch chance
            var baseChance = 50; // Base 50% chance
            var healthModifier = (targetPokemon.MaxVigor - targetPokemon.CurrentVigor) * 2; // Lower health = better chance
            var levelModifier = Math.Max(0, 20 - targetPokemon.Level); // Lower level = better chance
            var ballModifier = GetPokeBallModifier(pokeballItemId);
            
            // Use Charisma modifier for catch attempts (D&D 5e style)
            var charismaScore = trainerId == "player" ? gameState.Player.Character.Stats.Charisma : 10;
            var charismaModifier = CalculateAbilityModifier(charismaScore) * 5;
            
            var totalChance = baseChance + healthModifier + levelModifier + ballModifier + charismaModifier + (catchModifier ?? 0);
            totalChance = Math.Clamp(totalChance, 5, 95); // Never 0% or 100%

            var random = new Random();
            var roll = random.Next(1, 101);
            bool caught = roll <= totalChance;

            // Remove pokeball from inventory
            pokeball.Quantity--;
            if (pokeball.Quantity <= 0)
            {
                inventory.Remove(pokeball);
            }

            string resultMessage;
            if (caught)
            {
                // Create owned pokemon
                var ownedPokemon = new OwnedPokemon
                {
                    Pokemon = targetPokemon,
                    Experience = CalculateExperienceForLevel(targetPokemon.Level),
                    CaughtLocationId = gameState.CurrentLocationId,
                    Friendship = 50
                };

                // Remove from world pokemon
                gameState.WorldPokemon.Remove(targetPokemonId);

                // Remove from location
                if (gameState.WorldLocations.ContainsKey(gameState.CurrentLocationId))
                {
                    gameState.WorldLocations[gameState.CurrentLocationId].PresentPokemonIds.Remove(targetPokemonId);
                }

                // Add to player's team or box
                if (trainerId == "player")
                {
                    if (gameState.Player.TeamPokemon.Count < 6)
                    {
                        gameState.Player.TeamPokemon.Add(ownedPokemon);
                        resultMessage = $"The {targetPokemon.Species} was caught! It has been added to your team.";
                    }
                    else
                    {
                        gameState.Player.BoxedPokemon.Add(ownedPokemon);
                        resultMessage = $"The {targetPokemon.Species} was caught! It has been sent to your PC box.";
                    }
                }
                else
                {
                    // For NPCs, add to their pokemon owned list
                    gameState.WorldNpcs[trainerId].PokemonOwned.Add(targetPokemonId);
                    resultMessage = $"The {targetPokemon.Species} was caught by {trainerId}!";
                }

                gameState.RecentEvents.Add($"{trainerId} caught {targetPokemon.Species} using {pokeballItemId}");
            }
            else
            {
                resultMessage = $"Oh no! The {targetPokemon.Species} broke free from the {pokeballItemId}!";
                gameState.RecentEvents.Add($"{trainerId} failed to catch {targetPokemon.Species}");
            }

            // Keep only last 10 events
            if (gameState.RecentEvents.Count > 10)
            {
                gameState.RecentEvents.RemoveAt(0);
            }

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true, 
                caught = caught, 
                message = resultMessage,
                catchChance = totalChance,
                roll = roll
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] InitiatePokemonCapture error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to attempt capture: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("manage_pokemon_team")]
    [Description("Manages a trainer's Pokémon party by moving Pokémon between the active team and the PC box.")]
    public async Task<string> ManagePokemonTeam(
        [Description("The ID of the trainer whose team is being managed (usually 'player').")]
            string trainerId,
        [Description("A JSON string of a list of Pokémon instance IDs to move from the box to the active team. Example: '[\"pkmn_inst_004_ratatta\"]'")]
            string pokemonToTeam,
        [Description("A JSON string of a list of Pokémon instance IDs to move from the active team to the box. Example: '[\"pkmn_inst_002_pidgey\"]'")]
            string pokemonToBox)
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] ManagePokemonTeam called for {trainerId}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (trainerId != "player")
            {
                return JsonSerializer.Serialize(new { error = "Team management currently only supported for player" }, _jsonOptions);
            }

            var toTeamList = string.IsNullOrEmpty(pokemonToTeam) || pokemonToTeam == "[]" 
                ? new List<string>() 
                : JsonSerializer.Deserialize<List<string>>(pokemonToTeam);
            
            var toBoxList = string.IsNullOrEmpty(pokemonToBox) || pokemonToBox == "[]" 
                ? new List<string>() 
                : JsonSerializer.Deserialize<List<string>>(pokemonToBox);

            var changes = new List<string>();

            // Move Pokemon to team from box
            foreach (var pokemonId in toTeamList)
            {
                var boxedPokemon = gameState.Player.BoxedPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
                if (boxedPokemon != null)
                {
                    if (gameState.Player.TeamPokemon.Count < 6)
                    {
                        gameState.Player.BoxedPokemon.Remove(boxedPokemon);
                        gameState.Player.TeamPokemon.Add(boxedPokemon);
                        changes.Add($"Moved {boxedPokemon.Pokemon.Species} to team");
                    }
                    else
                    {
                        changes.Add($"Cannot move {boxedPokemon.Pokemon.Species} to team - team is full");
                    }
                }
                else
                {
                    changes.Add($"Pokemon {pokemonId} not found in box");
                }
            }

            // Move Pokemon to box from team
            foreach (var pokemonId in toBoxList)
            {
                var teamPokemon = gameState.Player.TeamPokemon.FirstOrDefault(p => p.Pokemon.Id == pokemonId);
                if (teamPokemon != null)
                {
                    // Don't allow removing the last Pokemon from team
                    if (gameState.Player.TeamPokemon.Count > 1)
                    {
                        gameState.Player.TeamPokemon.Remove(teamPokemon);
                        gameState.Player.BoxedPokemon.Add(teamPokemon);
                        changes.Add($"Moved {teamPokemon.Pokemon.Species} to box");
                    }
                    else
                    {
                        changes.Add($"Cannot move {teamPokemon.Pokemon.Species} to box - must have at least one Pokemon on team");
                    }
                }
                else
                {
                    changes.Add($"Pokemon {pokemonId} not found on team");
                }
            }

            if (changes.Count > 0)
            {
                gameState.LastSaveTime = DateTime.UtcNow;
                await _repository.SaveStateAsync(gameState);
            }

            return JsonSerializer.Serialize(new { 
                success = true, 
                changes = changes,
                teamSize = gameState.Player.TeamPokemon.Count,
                boxSize = gameState.Player.BoxedPokemon.Count
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] ManagePokemonTeam error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to manage Pokemon team: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("change_game_phase")]
    [Description("Changes the primary state of the game, for example, from 'Exploration' to 'Combat' when a battle starts.")]
    public async Task<string> ChangeGamePhase(
        [Description("The game phase to transition to. Valid options are: GameCreation, CharacterCreation, WorldGeneration, Exploration, Combat, LevelUp.")]
            string newPhase,
        [Description("A brief text summary of the event that triggered the phase change for context.")]
            string summary)
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] ChangeGamePhase called to {newPhase}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (!Enum.TryParse<GamePhase>(newPhase, out var phase))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid game phase: {newPhase}" }, _jsonOptions);
            }

            var previousPhase = gameState.CurrentPhase;
            gameState.CurrentPhase = phase;
            gameState.PhaseChangeSummary = summary;
            gameState.LastSaveTime = DateTime.UtcNow;

            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Game phase changed from {previousPhase} to {phase}",
                previousPhase = previousPhase.ToString(),
                newPhase = phase.ToString(),
                summary = summary
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] ChangeGamePhase error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to change game phase: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("query_game_state")]
    [Description("Gets specific, detailed information from the current game state that may not be in the main prompt summary. Use this to ask targeted questions before deciding on an action.")]
    public async Task<string> QueryState(
        [Description("A JSON string representing a list of queries for specific data points. Example: '[\"player.money\", \"worldNpcs.char_gary_oak.stats\"]'")]
            string queries)
    {
        try
        {
            Debug.WriteLine($"[GameStatePlugin] QueryState called");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            var queryList = JsonSerializer.Deserialize<List<string>>(queries);
            var results = new Dictionary<string, object>();

            foreach (var query in queryList)
            {
                try
                {
                    var result = ExecuteQuery(gameState, query);
                    results[query] = result;
                }
                catch (Exception ex)
                {
                    results[query] = $"Error: {ex.Message}";
                }
            }

            return JsonSerializer.Serialize(new { success = true, results = results }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatePlugin] QueryState error: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to query state: {ex.Message}" }, _jsonOptions);
        }
    }

    // --- Helper Methods ---

    private bool UpdatePlayerCharacter(PlayerState playerState, Dictionary<string, object> updates)
    {
        var updated = false;
        
        // Update character properties
        updated |= UpdateCharacter(playerState.Character, updates);

        // Update player-specific properties
        if (updates.ContainsKey("experience"))
        {
            var newExp = Convert.ToInt32(updates["experience"]);
            var oldLevel = playerState.Character.Level;
            playerState.Experience = newExp;
            var newLevel = CalculateLevelFromExperience(newExp);
            
            if (newLevel > oldLevel)
            {
                playerState.AvailableStatPoints += (newLevel - oldLevel);
                playerState.Character.Level = newLevel;
            }
            
            updated = true;
        }

        if (updates.ContainsKey("availableStatPoints"))
        {
            playerState.AvailableStatPoints = Convert.ToInt32(updates["availableStatPoints"]);
            updated = true;
        }

        if (updates.ContainsKey("characterCreationComplete"))
        {
            playerState.CharacterCreationComplete = Convert.ToBoolean(updates["characterCreationComplete"]);
            updated = true;
        }

        return updated;
    }

    private bool UpdateCharacter(Character character, Dictionary<string, object> updates)
    {
        var updated = false;

        if (updates.ContainsKey("name"))
        {
            character.Name = updates["name"].ToString();
            updated = true;
        }

        if (updates.ContainsKey("level"))
        {
            character.Level = Convert.ToInt32(updates["level"]);
            updated = true;
        }

        if (updates.ContainsKey("money"))
        {
            character.Money = Convert.ToInt32(updates["money"]);
            updated = true;
        }

        if (updates.ContainsKey("globalRenown"))
        {
            character.GlobalRenown = Math.Clamp(Convert.ToInt32(updates["globalRenown"]), 0, 100);
            updated = true;
        }

        if (updates.ContainsKey("globalNotoriety"))
        {
            character.GlobalNotoriety = Math.Clamp(Convert.ToInt32(updates["globalNotoriety"]), 0, 100);
            updated = true;
        }

        // Update stats if provided
        if (updates.ContainsKey("stats"))
        {
            var statsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(updates["stats"].ToString());
            updated |= UpdateStats(character.Stats, statsDict);
        }

        return updated;
    }

    private bool UpdatePokemon(Pokemon pokemon, Dictionary<string, object> updates)
    {
        var updated = false;

        if (updates.ContainsKey("nickName"))
        {
            pokemon.NickName = updates["nickName"].ToString();
            updated = true;
        }

        if (updates.ContainsKey("level"))
        {
            pokemon.Level = Convert.ToInt32(updates["level"]);
            updated = true;
        }

        if (updates.ContainsKey("currentVigor"))
        {
            pokemon.CurrentVigor = Math.Clamp(Convert.ToInt32(updates["currentVigor"]), 0, pokemon.MaxVigor);
            updated = true;
        }

        if (updates.ContainsKey("maxVigor"))
        {
            pokemon.MaxVigor = Convert.ToInt32(updates["maxVigor"]);
            updated = true;
        }

        // Update stats if provided
        if (updates.ContainsKey("stats"))
        {
            var statsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(updates["stats"].ToString());
            updated |= UpdateStats(pokemon.Stats, statsDict);
        }

        return updated;
    }

    private bool UpdateStats(Stats stats, Dictionary<string, object> updates)
    {
        var updated = false;

        if (updates.ContainsKey("strength"))
        {
            stats.Strength = Convert.ToInt32(updates["strength"]);
            updated = true;
        }

        if (updates.ContainsKey("dexterity"))
        {
            stats.Dexterity = Convert.ToInt32(updates["dexterity"]);
            updated = true;
        }

        if (updates.ContainsKey("constitution"))
        {
            stats.Constitution = Convert.ToInt32(updates["constitution"]);
            updated = true;
        }

        if (updates.ContainsKey("intelligence"))
        {
            stats.Intelligence = Convert.ToInt32(updates["intelligence"]);
            updated = true;
        }

        if (updates.ContainsKey("wisdom"))
        {
            stats.Wisdom = Convert.ToInt32(updates["wisdom"]);
            updated = true;
        }

        if (updates.ContainsKey("charisma"))
        {
            stats.Charisma = Convert.ToInt32(updates["charisma"]);
            updated = true;
        }

        return updated;
    }

    private void ApplyCharacterProperties(Character character, Dictionary<string, object> properties)
    {
        if (properties.ContainsKey("name"))
            character.Name = properties["name"].ToString();

        if (properties.ContainsKey("level"))
            character.Level = Convert.ToInt32(properties["level"]);

        if (properties.ContainsKey("money"))
            character.Money = Convert.ToInt32(properties["money"]);

        if (properties.ContainsKey("isTrainer"))
            character.IsTrainer = Convert.ToBoolean(properties["isTrainer"]);

        if (properties.ContainsKey("globalRenown"))
            character.GlobalRenown = Math.Clamp(Convert.ToInt32(properties["globalRenown"]), 0, 100);

        if (properties.ContainsKey("globalNotoriety"))
            character.GlobalNotoriety = Math.Clamp(Convert.ToInt32(properties["globalNotoriety"]), 0, 100);
    }

    private void ApplyPokemonProperties(Pokemon pokemon, Dictionary<string, object> properties)
    {
        if (properties.ContainsKey("species"))
            pokemon.Species = properties["species"].ToString();

        if (properties.ContainsKey("nickName"))
            pokemon.NickName = properties["nickName"].ToString();

        if (properties.ContainsKey("level"))
        {
            pokemon.Level = Convert.ToInt32(properties["level"]);
            // Set default vigor based on level
            pokemon.MaxVigor = 10 + (pokemon.Level * 2);
            pokemon.CurrentVigor = pokemon.MaxVigor;
        }

        if (properties.ContainsKey("type1") && Enum.TryParse<PokemonType>(properties["type1"].ToString(), out var type1))
            pokemon.Type1 = type1;

        if (properties.ContainsKey("type2") && Enum.TryParse<PokemonType>(properties["type2"].ToString(), out var type2))
            pokemon.Type2 = type2;
    }

    private int GetPokeBallModifier(string pokeballItemId)
    {
        return pokeballItemId.ToLower() switch
        {
            "item_pokeball" => 0,
            "item_greatball" => 15,
            "item_ultraball" => 25,
            "item_masterball" => 85, // Near guaranteed catch
            _ => 0
        };
    }

    private object ExecuteQuery(GameStateModel gameState, string query)
    {
        var parts = query.Split('.');
        object current = gameState;

        foreach (var part in parts)
        {
            if (current == null) return null;

            var type = current.GetType();
            
            if (current is Dictionary<string, object> dict)
            {
                current = dict.ContainsKey(part) ? dict[part] : null;
            }
            else if (current is Dictionary<string, Character> charDict)
            {
                current = charDict.ContainsKey(part) ? charDict[part] : null;
            }
            else if (current is Dictionary<string, Pokemon> pokemonDict)
            {
                current = pokemonDict.ContainsKey(part) ? pokemonDict[part] : null;
            }
            else if (current is Dictionary<string, Location> locationDict)
            {
                current = locationDict.ContainsKey(part) ? locationDict[part] : null;
            }
            else
            {
                var property = type.GetProperty(part, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                current = property?.GetValue(current);
            }
        }

        return current;
    }

    private int CalculateLevelFromExperience(int experience)
    {
        var level = 1;
        while (CalculateExperienceForLevel(level + 1) <= experience)
        {
            level++;
        }
        return level;
    }

    private int CalculateExperienceForLevel(int level)
    {
        // Experience curve: 1000 * (level - 1)^1.5
        return (int)(1000 * Math.Pow(level - 1, 1.5));
    }

    /// <summary>
    /// Calculates the D&D 5e ability modifier from an ability score.
    /// Formula: floor((abilityScore - 10) / 2)
    /// </summary>
    /// <param name="abilityScore">The ability score (typically 3-20)</param>
    /// <returns>The modifier (-4 to +5 for typical scores)</returns>
    private int CalculateAbilityModifier(int abilityScore)
    {
        return (int)Math.Floor((abilityScore - 10) / 2.0);
    }
}

/*
 * POTENTIAL ADDITIONAL FUNCTIONS THAT MIGHT BE MISSING:
 * 
 * 1. heal_pokemon - For healing Pokemon at Pokemon Centers or using items
 * 2. learn_move - For Pokemon learning new moves through leveling or TMs
 * 3. evolve_pokemon - For handling Pokemon evolution mechanics
 * 4. start_battle - For initiating combat between trainers or with wild Pokemon
 * 5. end_battle - For handling battle resolution and rewards
 * 6. trade_pokemon - For trading Pokemon between trainers
 * 7. use_item - For using consumable items like potions, berries, etc.
 * 8. save_game - For manual save triggers beyond automatic saves
 * 9. generate_location - For procedurally generating new locations
 * 10. spawn_wild_pokemon - For adding wild Pokemon to locations
 * 11. award_gym_badge - For giving gym badges to the player
 * 12. update_weather - For changing weather conditions
 * 13. update_time - For advancing time of day
 * 14. trigger_event - For story events and cutscenes
 * 15. update_faction_standing - More granular faction relationship management
 */