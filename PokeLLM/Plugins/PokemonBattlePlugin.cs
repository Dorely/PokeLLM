using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

public class PokemonBattlePlugin
{
    [KernelFunction("calculate_damage")]
    [Description("Calculate damage for a Pokemon move in battle")]
    public async Task<string> CalculateDamage(
        [Description("JSON string with attacker stats: {name, level, attack, specialAttack, type1, type2}")] string attacker,
        [Description("JSON string with defender stats: {name, level, defense, specialDefense, type1, type2}")] string defender,
        [Description("JSON string with move details: {name, power, type, category}")] string move,
        [Description("Random factor between 0.85 and 1.0")] double randomFactor = 0.925)
    {
        await Task.Yield();
        Debug.WriteLine($"[VectorStorePlugin] CalculateDamage called with name: '{attacker}', type: '{defender}', move:'{move}', randomFactor: '{randomFactor}'");
        return "5";
    }
}