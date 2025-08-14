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

    // D&D-specific properties for multi-ruleset tests
    public List<string> KnownSpells { get; set; } = new();
    public Dictionary<int, int> SpellSlots { get; set; } = new();
    public List<DnDEquipment> Equipment { get; set; } = new();

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

    public bool CanCastSpell(string spellId, int spellLevel)
    {
        return KnownSpells.Contains(spellId) && 
               SpellSlots.ContainsKey(spellLevel) && 
               SpellSlots[spellLevel] > 0;
    }
}

public class DnDEquipment
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}