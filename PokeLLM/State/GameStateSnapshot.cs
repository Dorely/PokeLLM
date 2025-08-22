namespace PokeLLM.State;

public record GameStateSnapshot(
    string PlayerId,
    PlayerState Player,
    WorldState World,
    DateTime Timestamp)
{
    public static GameStateSnapshot Create(PlayerState player, WorldState world)
    {
        return new GameStateSnapshot(
            Guid.NewGuid().ToString(),
            player,
            world,
            DateTime.UtcNow);
    }
}

public record PlayerState(
    string Name,
    int Level,
    int Health,
    int MaxHealth,
    int Experience,
    Dictionary<string, int> Stats,
    List<string> Inventory,
    string CurrentLocation);

public record WorldState(
    string CurrentRegion,
    string CurrentLocation,
    Dictionary<string, object> LocationData,
    List<string> ActiveEvents,
    Dictionary<string, NpcState> NpcStates);

public record NpcState(
    string Id,
    string Name,
    string CurrentLocation,
    Dictionary<string, object> State);