using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for managing D&D 5e-style Pokemon combat encounters
/// </summary>
public class CombatPhasePlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;

    public CombatPhasePlugin(IGameStateRepository repository)
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

    [KernelFunction("end_combat")]
    [Description("End the combat encounter and transition back to exploration")]
    public async Task EndCombat([Description("A summary of the combat encounter and the results")] string combatSummary)
    {
        //TODO this method ends the encounter and will be only function available while combat is not fully implemented.
        //The summary is to be fed into the ChatManagement routine and the phase is changed to exploration
    }

    [KernelFunction("skill_check")]
    [Description("Make a skill check during combat (e.g., for special moves or environmental actions)")]
    public async Task<string> MakeSkillCheck(
        [Description("The skill being checked")] string skillName,
        [Description("The difficulty class for the check")] int difficultyClass)
    {
        //TODO: Implement skill check functionality for combat scenarios
        throw new NotImplementedException("Skill checks in combat not yet implemented");
    }
}