using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.Data;

public static class PokemonKnowledgeData
{
    public static readonly List<KnowledgeEntry> Entries = new()
    {
        // Pokemon Types and Effectiveness
        new KnowledgeEntry
        {
            Id = "type_effectiveness_basics",
            Category = "Type Effectiveness",
            Content = "Fire-type moves are super effective against Grass, Ice, Bug, and Steel types. Fire moves are not very effective against Fire, Water, Rock, and Dragon types. Understanding type effectiveness is crucial for strategic Pokemon battles."
        },
        new KnowledgeEntry
        {
            Id = "water_type_effectiveness",
            Category = "Type Effectiveness",
            Content = "Water-type moves are super effective against Fire, Ground, and Rock types. Water moves are not very effective against Water, Grass, and Dragon types. Many Water-type Pokemon can learn Ice-type moves for coverage."
        },
        new KnowledgeEntry
        {
            Id = "electric_type_effectiveness",
            Category = "Type Effectiveness",
            Content = "Electric-type moves are super effective against Water and Flying types. Electric moves are not very effective against Electric, Grass, and Dragon types. Electric moves have no effect on Ground-type Pokemon."
        },
        new KnowledgeEntry
        {
            Id = "grass_type_effectiveness",
            Category = "Type Effectiveness",
            Content = "Grass-type moves are super effective against Water, Ground, and Rock types. Grass moves are not very effective against Fire, Grass, Poison, Flying, Bug, Dragon, and Steel types."
        },

        // Battle Mechanics
        new KnowledgeEntry
        {
            Id = "critical_hits",
            Category = "Battle Mechanics",
            Content = "Critical hits occur randomly with a base rate of 1/24 (approximately 4.17%). Critical hits deal 1.5x damage and ignore stat changes that would reduce damage. Some moves have higher critical hit ratios."
        },
        new KnowledgeEntry
        {
            Id = "stab_bonus",
            Category = "Battle Mechanics",
            Content = "STAB (Same Type Attack Bonus) gives a 1.5x damage multiplier when a Pokemon uses a move that matches one of its types. This bonus is applied after base damage calculation but before type effectiveness."
        },
        new KnowledgeEntry
        {
            Id = "damage_formula",
            Category = "Battle Mechanics",
            Content = "Pokemon damage is calculated using the formula: ((2 * Level / 5 + 2) * Power * Attack / Defense / 50 + 2) * Modifiers. Modifiers include STAB, type effectiveness, critical hits, and random factors."
        },
        new KnowledgeEntry
        {
            Id = "physical_vs_special",
            Category = "Battle Mechanics",
            Content = "Physical moves use the Attack stat of the attacker and Defense stat of the defender. Special moves use Special Attack and Special Defense stats. The move's category determines which stats are used."
        },

        // Popular Pokemon
        new KnowledgeEntry
        {
            Id = "charizard_info",
            Category = "Pokemon",
            Content = "Charizard is a Fire/Flying-type Pokemon. It has high Attack and Special Attack stats, making it versatile. Popular moves include Flamethrower, Air Slash, Dragon Pulse, and Solar Beam. Weak to Water, Electric, and especially Rock moves."
        },
        new KnowledgeEntry
        {
            Id = "pikachu_info",
            Category = "Pokemon",
            Content = "Pikachu is an Electric-type Pokemon known for its speed and special attack. Signature moves include Thunderbolt, Quick Attack, and Iron Tail. Pikachu is weak to Ground-type moves but strong against Water and Flying types."
        },
        new KnowledgeEntry
        {
            Id = "blastoise_info",
            Category = "Pokemon",
            Content = "Blastoise is a Water-type Pokemon with high Defense and Special Attack. Known for moves like Hydro Pump, Ice Beam, and Skull Bash. Strong against Fire, Ground, and Rock types but weak to Grass and Electric moves."
        },
        new KnowledgeEntry
        {
            Id = "venusaur_info",
            Category = "Pokemon",
            Content = "Venusaur is a Grass/Poison-type Pokemon with balanced stats and good bulk. Common moves include Solar Beam, Sludge Bomb, Earthquake, and Sleep Powder. Effective against Water, Ground, and Rock types."
        },

        // Battle Strategies
        new KnowledgeEntry
        {
            Id = "type_coverage",
            Category = "Strategy",
            Content = "Good type coverage means having moves that can hit many different types for super effective damage. Pokemon often learn moves outside their type to cover their weaknesses and hit more opponents effectively."
        },
        new KnowledgeEntry
        {
            Id = "switching_strategy",
            Category = "Strategy",
            Content = "Switching Pokemon is crucial when facing unfavorable matchups. Switch to a Pokemon that resists the opponent's attacks or has type advantage. Predict opponent switches to gain momentum."
        },
        new KnowledgeEntry
        {
            Id = "status_conditions",
            Category = "Strategy",
            Content = "Status conditions like paralysis, sleep, burn, freeze, and poison can significantly impact battles. Burn reduces Attack by 50%, paralysis can prevent moves, and poison deals continuous damage."
        },

        // Move Categories
        new KnowledgeEntry
        {
            Id = "priority_moves",
            Category = "Moves",
            Content = "Priority moves like Quick Attack, Bullet Punch, and Aqua Jet always go first regardless of speed stats. These moves typically have lower power but can be crucial for finishing weakened opponents."
        },
        new KnowledgeEntry
        {
            Id = "setup_moves",
            Category = "Moves",
            Content = "Setup moves like Swords Dance, Nasty Plot, and Dragon Dance boost stats but don't deal damage. These moves can turn the tide of battle by dramatically increasing offensive power."
        },
        new KnowledgeEntry
        {
            Id = "recovery_moves",
            Category = "Moves",
            Content = "Recovery moves like Recover, Roost, and Soft-Boiled restore HP during battle. These moves are essential for bulky Pokemon that want to stay in battle for extended periods."
        }
    };
}

public class KnowledgeEntry
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Content { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new();
}
