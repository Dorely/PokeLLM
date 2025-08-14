// Test model for Pokemon trainer ruleset validation - not part of main application
using System.Collections.Generic;

namespace PokeLLM.Tests.TestModels;

public class PokemonTrainer
{
    public string Name { get; set; } = string.Empty;
    public string TrainerClass { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;

    // Pokemon trainer stats (different from D&D)
    public int Vigor { get; set; } = 10;
    public int Dexterity { get; set; } = 10;
    public int Intelligence { get; set; } = 10;
    public int Empathy { get; set; } = 10;
    public int Intuition { get; set; } = 10;
    public int Charisma { get; set; } = 10;

    // Pokemon-specific properties
    public List<string> Pokemon { get; set; } = new();
    public Dictionary<string, int> Inventory { get; set; } = new();
    public List<string> Badges { get; set; } = new();
    public List<string> Traits { get; set; } = new();

    public int GetStatModifier(string statName)
    {
        var score = statName.ToLower() switch
        {
            "vigor" => Vigor,
            "dexterity" => Dexterity,
            "intelligence" => Intelligence,
            "empathy" => Empathy,
            "intuition" => Intuition,
            "charisma" => Charisma,
            _ => 10
        };
        return (score - 10) / 2; // Same formula but different stats
    }

    public bool CanCapturePokemon()
    {
        return Pokemon.Count < 6; // Standard Pokemon team limit
    }

    public bool HasItem(string itemId)
    {
        return Inventory.ContainsKey(itemId) && Inventory[itemId] > 0;
    }

    // Ensure trainer starts with some items for testing
    public void SetupTestInventory()
    {
        Inventory["pokeball"] = 5;
        Inventory["potion"] = 3;
        Inventory["revive"] = 1;
    }
}