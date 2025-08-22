namespace PokeLLM.Agents;

public enum GameIntent
{
    Unknown = 0,
    
    // Setup intents
    StartNewGame,
    SelectSetting,
    CreateCharacter,
    
    // Exploration intents
    Move,
    Examine,
    Talk,
    Search,
    Explore,
    
    // Combat intents
    Attack,
    UseItem,
    Flee,
    Defend,
    
    // Character management
    CheckStatus,
    CheckSkill,
    ManageInventory,
    Rest,
    LevelUp,
    
    // Meta intents
    Save,
    Load,
    Help,
    Exit
}

public record GameAgentMessage(
    string AgentId,
    string Content,
    GameIntent? Intent,
    Dictionary<string, object>? Metadata,
    DateTime Timestamp)
{
    public static GameAgentMessage Create(string agentId, string content, GameIntent? intent = null)
    {
        return new GameAgentMessage(agentId, content, intent, null, DateTime.UtcNow);
    }
}