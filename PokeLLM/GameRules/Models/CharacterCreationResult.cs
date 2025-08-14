#nullable enable

namespace PokeLLM.GameRules.Models;

public class CharacterCreationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Character { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public static CharacterCreationResult SuccessResult(object character, string message)
    {
        return new CharacterCreationResult
        {
            Success = true,
            Character = character,
            Message = message,
            ValidationErrors = new List<string>()
        };
    }

    public static CharacterCreationResult Failure(string message, List<string> validationErrors)
    {
        return new CharacterCreationResult
        {
            Success = false,
            Character = null,
            Message = message,
            ValidationErrors = validationErrors
        };
    }
}