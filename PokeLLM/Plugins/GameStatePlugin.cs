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
    public Task<string> UpdateEntity(
            [Description("The unique ID of the character or Pokémon to update (e.g., 'player', 'char_prof_oak', 'pkmn_inst_001_pidgey').")]
            string entityId,
            [Description("A JSON object string containing only the fields to be updated. For example: '{\"level\": 5, \"money\": 3500}' or '{\"currentVigor\": 15}'.")]
            string updates,
            [Description("A JSON object string representing deltas for NPC relationships. For example: '{\\\"char_gary_oak\\\": -10}' to decrease a relationship by 10.")]
            string npcRelationshipDeltas = "{}",
            [Description("A JSON object string representing deltas for Faction reputations. For example: '{\\\"faction_team_rocket\\\": 20}' to increase reputation by 20.")]
            string factionRelationshipDeltas = "{}")
    {
        // Backend logic to find the entity by its ID, deserialize the update objects,
        // apply the changes to the GameStateModel, and save.
        throw new System.NotImplementedException();
    }

    [KernelFunction("update_inventory")]
    [Description("Adds or removes items from a character's inventory. Use this for giving rewards, picking up items, using items, or buying/selling.")]
    public Task<string> UpdateInventory(
        [Description("The unique ID of the character whose inventory is changing (e.g., 'player').")]
            string characterId,
        [Description("A JSON string representing a list of items to add. Example: '[{\"itemId\": \"item_potion\", \"quantity\": 2}]'.")]
            string itemsToAdd,
        [Description("A JSON string representing a list of items to remove. Example: '[{\"itemId\": \"item_pokeball\", \"quantity\": 1}]'.")]
            string itemsToRemove)
    {
        // Backend logic to find the character, parse the item lists,
        // and update the inventory in the GameStateModel.
        throw new System.NotImplementedException();
    }

    [KernelFunction("create_new_entity")]
    [Description("Creates a new character or Pokémon and adds it to the world. Use when the story introduces a new, persistent entity. The system will assign and return a new unique ID.")]
    public Task<string> CreateNewEntity(
        [Description("The type of entity to create. Must be either 'Character' or 'Pokemon'.")]
            string entityType,
        [Description("A JSON object string with the initial properties for the new entity (e.g., '{\"name\": \"Bob the Merchant\", \"isTrainer\": false}' or '{\"species\": \"Rattata\", \"level\": 3}').")]
            string properties)
    {
        // Backend logic to validate the entity type, deserialize properties,
        // create a new Character or Pokemon object, generate a unique ID,
        // add it to the GameStateModel (WorldNpcs or WorldPokemon), and also
        // trigger an update to the vector database for the new canonical entity.
        // Returns the new unique ID.
        throw new System.NotImplementedException();
    }

    [KernelFunction("move_entity_to_location")]
    [Description("Moves a character or Pokémon to a new location. For the player, this changes their current location. For NPCs, it makes them appear in a different place.")]
    public Task<string> MoveEntityToLocation(
        [Description("The unique ID of the character or Pokémon to move (e.g., 'player', 'char_prof_oak').")]
            string entityId,
        [Description("The unique ID of the destination location (e.g., 'loc_route_1').")]
            string locationId,
        [Description("Optional: The ID of a specific point of interest within the location to move to (e.g., 'poi_oaks_lab').")]
            string pointOfInterestId = null)
    {
        // Backend logic to update GameStateModel.CurrentLocationId for the player,
        // or to update the PresentNpcIds/PresentPokemonIds lists for locations when an NPC/Pokemon moves.
        throw new System.NotImplementedException();
    }

    [KernelFunction("initiate_pokemon_capture")]
    [Description("Attempts to catch a wild Pokémon with a Poké Ball. The system will determine the outcome based on game rules.")]
    public Task<string> InitiatePokemonCapture(
        [Description("The ID of the trainer attempting the capture (usually 'player').")]
            string trainerId,
        [Description("The unique ID of the wild Pokémon being targeted for capture.")]
            string targetPokemonId,
        [Description("The item ID of the Poké Ball being used (e.g., 'item_pokeball', 'item_greatball').")]
            string pokeballItemId,
        [Description("An optional custom catch percentage modifier to be added for narrative purposes")]
            int? catchModifier)
    {
        // Backend logic to calculate capture chance based on Pokémon health, status, and ball type.
        // This check uses Charm as the base skill modifier, but a narrative catchModifier can be added optionally to force a certain result
        // If successful, this function will call the necessary state updates: remove the wild Pokemon from WorldPokemon,
        // create an OwnedPokemon, add it to the player's team or box, and remove the pokeball from inventory.
        // Returns a string describing the outcome (e.g., "The Rattata was caught!" or "Oh no, the Pokémon broke free!").
        throw new System.NotImplementedException();
    }

    [KernelFunction("manage_pokemon_team")]
    [Description("Manages a trainer's Pokémon party by moving Pokémon between the active team and the PC box.")]
    public Task<string> ManagePokemonTeam(
        [Description("The ID of the trainer whose team is being managed (usually 'player').")]
            string trainerId,
        [Description("A JSON string of a list of Pokémon instance IDs to move from the box to the active team. Example: '[\"pkmn_inst_004_ratatta\"]'")]
            string pokemonToTeam,
        [Description("A JSON string of a list of Pokémon instance IDs to move from the active team to the box. Example: '[\"pkmn_inst_002_pidgey\"]'")]
            string pokemonToBox)
    {
        // Backend logic to find the player's TeamPokemon and BoxedPokemon lists,
        // validate the moves (e.g., ensuring team size does not exceed 6), and perform the list updates.
        throw new System.NotImplementedException();
    }

    [KernelFunction("change_game_phase")]
    [Description("Changes the primary state of the game, for example, from 'Exploration' to 'Combat' when a battle starts.")]
    public Task<string> ChangeGamePhase(
        [Description("The game phase to transition to. Valid options are: GameCreation, CharacterCreation, WorldGeneration, Exploration, Combat, LevelUp.")]
            string newPhase,
        [Description("A brief text summary of the event that triggered the phase change for context.")]
            string summary)
    {
        // Backend logic to parse the enum, update GameStateModel.CurrentPhase,
        // and set the GameStateModel.PhaseChangeSummary.
        throw new System.NotImplementedException();
    }

    [KernelFunction("query_game_state")]
    [Description("Gets specific, detailed information from the current game state that may not be in the main prompt summary. Use this to ask targeted questions before deciding on an action.")]
    public Task<string> QueryState(
        [Description("A JSON string representing a list of queries for specific data points. Example: '[\"player.money\", \"worldNpcs.char_gary_oak.stats\"]'")]
            string queries)
    {
        // Backend logic to parse the query strings, traverse the GameStateModel object graph
        // to find the requested data, and serialize the results into a JSON string for the LLM.
        throw new System.NotImplementedException();
    }


    // --- Helper Methods ---

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
}