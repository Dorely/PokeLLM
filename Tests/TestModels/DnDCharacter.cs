// Test model for D&D 5e ruleset validation - not part of main application
namespace PokeLLM.Tests.TestModels;

public class DnDCharacter
{
    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int HitPoints { get; set; } = 10;
    
    // Simple ability scores for testing
    public int Strength { get; set; } = 10;
    public int Dexterity { get; set; } = 10;
    public int Constitution { get; set; } = 10;
    public int Intelligence { get; set; } = 10;
    public int Wisdom { get; set; } = 10;
    public int Charisma { get; set; } = 10;

    public int GetAbilityModifier(string ability)
    {
        var score = ability.ToLower() switch
        {
            "strength" => Strength,
            "dexterity" => Dexterity,
            "constitution" => Constitution,
            "intelligence" => Intelligence,
            "wisdom" => Wisdom,
            "charisma" => Charisma,
            _ => 10
        };
        return (score - 10) / 2;
    }
}