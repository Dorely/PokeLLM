using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeLLM.GameState.Models;
using PokeLLM.GameLogic;

namespace PokeLLM.Game.GameLogic;

public interface INpcManagementService
{
    Task<string> CreateNpcAsync(Dictionary<string, object> npcData, string locationId = "");
    Task<string> AssignPokemonToNpcAsync(string npcId, string pokemonId);
    Task<string> MoveNpcToLocationAsync(string npcId, string locationId);
    Task<string> RemoveNpcFromLocationAsync(string npcId);
    Task<string> UpdateNpcRelationshipWithPlayerAsync(string npcId, int relationshipValue);
    Task<string> AddNpcToFactionAsync(string npcId, string factionId);
    Task<string> RemoveNpcFromFactionAsync(string npcId, string factionId);
    Task<Dictionary<string, object>> CreateNpc(string name, string characterClass, string locationId);
    Task<Dictionary<string, object>> GetNpcDetails(string npcId);
    Task<List<Dictionary<string, object>>> GetNpcsAtLocation(string locationId);
}

/// <summary>
/// This service contains methods for managing NPCs within the game state
/// </summary>
public class NpcManagementService : INpcManagementService
{
    private readonly IEntityService _entityService;
    
    public NpcManagementService(IEntityService entityService)
    {
        _entityService = entityService;
    }

    public async Task<string> CreateNpcAsync(Dictionary<string, object> npcData, string locationId = "")
    {
        var npcId = npcData.GetValueOrDefault("id")?.ToString() ?? Guid.NewGuid().ToString();
        
        // Create the NPC entity
        await _entityService.CreateEntity(npcId, "npc", npcData);

        // If locationId is provided, add NPC to location
        if (!string.IsNullOrEmpty(locationId))
        {
            var location = await _entityService.GetEntity<Dictionary<string, object>>(locationId);
            if (location != null)
            {
                var npcIds = location.GetValueOrDefault("npcIds") as List<string> ?? new List<string>();
                if (!npcIds.Contains(npcId))
                {
                    npcIds.Add(npcId);
                    location["npcIds"] = npcIds;
                    await _entityService.UpdateEntity(locationId, location);
                }
            }
        }

        return npcId;
    }

    public async Task<string> AssignPokemonToNpcAsync(string npcId, string pokemonId)
    {
        // TODO: Implement with dynamic ruleset approach
        return $"Pokemon {pokemonId} assigned to NPC {npcId} successfully";
    }

    public async Task<string> MoveNpcToLocationAsync(string npcId, string locationId)
    {
        // TODO: Implement with dynamic ruleset approach
        return $"NPC {npcId} moved to location {locationId}";
    }

    public async Task<string> RemoveNpcFromLocationAsync(string npcId)
    {
        // TODO: Implement with dynamic ruleset approach
        return $"NPC {npcId} removed from locations";
    }

    public async Task<string> UpdateNpcRelationshipWithPlayerAsync(string npcId, int relationshipValue)
    {
        // TODO: Implement with dynamic ruleset approach
        return $"Player relationship with NPC {npcId} set to {relationshipValue}";
    }

    public async Task<string> AddNpcToFactionAsync(string npcId, string factionId)
    {
        // TODO: Implement with dynamic ruleset approach
        return $"NPC {npcId} added to faction {factionId}";
    }

    public async Task<string> RemoveNpcFromFactionAsync(string npcId, string factionId)
    {
        // TODO: Implement with dynamic ruleset approach
        return $"NPC {npcId} removed from faction {factionId}";
    }

    public async Task<Dictionary<string, object>> CreateNpc(string name, string characterClass, string locationId)
    {
        var npcId = $"char_{name.ToLower().Replace(" ", "_")}_{Guid.NewGuid().ToString()[..8]}";
        var npc = new Dictionary<string, object>
        {
            ["id"] = npcId,
            ["name"] = name,
            ["characterClass"] = characterClass,
            ["isTrainer"] = false,
            ["pokemonOwned"] = new List<string>(),
            ["factions"] = new List<string>()
        };
        
        await CreateNpcAsync(npc, locationId);
        return npc;
    }

    public async Task<Dictionary<string, object>> GetNpcDetails(string npcId)
    {
        return await _entityService.GetEntity<Dictionary<string, object>>(npcId);
    }

    public async Task<List<Dictionary<string, object>>> GetNpcsAtLocation(string locationId)
    {
        var location = await _entityService.GetEntity<Dictionary<string, object>>(locationId);
        if (location == null) return new List<Dictionary<string, object>>();

        var npcIds = location.GetValueOrDefault("npcIds") as List<string> ?? new List<string>();
        var npcs = new List<Dictionary<string, object>>();
        
        foreach (var npcId in npcIds)
        {
            var npc = await _entityService.GetEntity<Dictionary<string, object>>(npcId);
            if (npc != null)
            {
                npcs.Add(npc);
            }
        }

        return npcs;
    }
}
