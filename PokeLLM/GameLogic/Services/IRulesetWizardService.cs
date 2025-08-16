using PokeLLM.GameRules.Interfaces;
using System.Text.Json;
using PokeLLM.Game.Orchestration;

namespace PokeLLM.GameLogic.Services;

/// <summary>
/// Service for handling interactive LLM-driven ruleset creation
/// </summary>
public interface IRulesetWizardService
{
    /// <summary>
    /// Start the interactive ruleset creation wizard
    /// </summary>
    Task<string> CreateRulesetInteractivelyAsync();
    
    /// <summary>
    /// Load an existing custom ruleset for editing
    /// </summary>
    Task<string> EditRulesetInteractivelyAsync(string rulesetId);
}

/// <summary>
/// Interactive wizard service for creating custom rulesets with LLM assistance
/// </summary>
public class RulesetWizardService : IRulesetWizardService
{
    private readonly IRulesetBuilderService _rulesetBuilder;
    private readonly IRulesetSchemaValidator _schemaValidator;
    private readonly IPhaseService _phaseService;
    private readonly IRulesetManager _rulesetManager;

    public RulesetWizardService(
        IRulesetBuilderService rulesetBuilder,
        IRulesetSchemaValidator schemaValidator,
        IPhaseService phaseService,
        IRulesetManager rulesetManager)
    {
        _rulesetBuilder = rulesetBuilder;
        _schemaValidator = schemaValidator;
        _phaseService = phaseService;
        _rulesetManager = rulesetManager;
    }

    public async Task<string> CreateRulesetInteractivelyAsync()
    {
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("       RULESET CREATION WIZARD");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        Console.WriteLine("Welcome to the interactive ruleset creation wizard!");
        Console.WriteLine("I'll guide you through creating a custom game ruleset step by step.");
        Console.WriteLine();

        try
        {
            // Initialize builder
            _rulesetBuilder.Initialize();

            // Step 1: Basic Metadata
            await CollectBasicMetadataAsync();

            // Step 2: Game Type & Mechanics  
            await CollectGameMechanicsAsync();

            // Step 3: Game State Schema
            await CollectGameStateSchemaAsync();

            // Step 4: Game Data Definition
            await CollectGameDataAsync();

            // Step 5: Function Definitions
            await CollectFunctionDefinitionsAsync();

            // Step 6: Prompt Templates
            await CollectPromptTemplatesAsync();

            // Final validation and save
            var rulesetId = await FinalizeRulesetAsync();
            
            Console.WriteLine();
            Console.WriteLine($"✓ Ruleset '{rulesetId}' created successfully!");
            Console.WriteLine($"Your custom ruleset has been saved and is ready to use.");
            Console.WriteLine();

            return rulesetId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during ruleset creation: {ex.Message}");
            Console.WriteLine("Ruleset creation cancelled.");
            return string.Empty;
        }
    }

    public async Task<string> EditRulesetInteractivelyAsync(string rulesetId)
    {
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("       RULESET EDITING WIZARD");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        Console.WriteLine($"Loading ruleset '{rulesetId}' for editing...");
        Console.WriteLine();

        try
        {
            // Load existing ruleset
            var existingRuleset = await _rulesetManager.LoadRulesetAsync(rulesetId);
            _rulesetBuilder.LoadFromExisting(existingRuleset);

            // Show current metadata
            var metadata = _rulesetBuilder.GetCurrentMetadata();
            Console.WriteLine($"Current Ruleset: {metadata.Name} (v{metadata.Version})");
            Console.WriteLine($"Description: {metadata.Description}");
            Console.WriteLine();

            // Allow user to choose what to edit
            await ChooseEditSectionAsync();

            // Final validation and save
            var finalRulesetId = await FinalizeRulesetAsync();
            
            Console.WriteLine();
            Console.WriteLine($"✓ Ruleset '{finalRulesetId}' updated successfully!");
            Console.WriteLine();

            return finalRulesetId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during ruleset editing: {ex.Message}");
            Console.WriteLine("Ruleset editing cancelled.");
            return string.Empty;
        }
    }

    private async Task CollectBasicMetadataAsync()
    {
        Console.WriteLine("=== Step 1: Basic Metadata ===");
        Console.WriteLine();

        // Get basic input from user
        Console.Write("Enter a unique ID for your ruleset (lowercase, no spaces): ");
        var id = GetValidInput(input => !string.IsNullOrWhiteSpace(input) && !input.Contains(' '), 
            "ID cannot be empty or contain spaces");

        Console.Write("Enter the name of your ruleset: ");
        var name = GetValidInput(input => !string.IsNullOrWhiteSpace(input), 
            "Name cannot be empty");

        Console.Write("Enter a description of your game: ");
        var description = Console.ReadLine() ?? "";

        Console.Write("Enter your name as the author: ");
        var author = Console.ReadLine() ?? "Unknown";

        Console.Write("Enter tags (comma-separated, e.g. fantasy, combat, magic): ");
        var tagsInput = Console.ReadLine() ?? "";
        var tags = tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        // Use LLM to suggest improvements and validate
        var metadataPrompt = $@"
The user is creating a custom game ruleset with these details:
- ID: {id}
- Name: {name}
- Description: {description}
- Author: {author}
- Tags: {string.Join(", ", tags)}

Please provide feedback on this metadata. Suggest improvements to the description and additional relevant tags.
Also validate that the ID is appropriate (unique, descriptive, follows naming conventions).
Respond in a helpful, encouraging tone.";

        Console.WriteLine();
        Console.WriteLine("Let me provide some feedback on your metadata...");
        Console.WriteLine();

        await foreach (var chunk in _phaseService.ProcessInputWithSpecialPromptAsync(metadataPrompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();

        // Ask if user wants to make changes
        Console.WriteLine();
        Console.Write("Would you like to update any of this metadata? (y/n): ");
        if (Console.ReadLine()?.ToLower().StartsWith("y") == true)
        {
            await CollectBasicMetadataAsync(); // Recursive call to re-collect
            return;
        }

        // Store in builder
        _rulesetBuilder.SetMetadata(id, name, description, author, tags);
        Console.WriteLine("✓ Metadata saved");
        Console.WriteLine();
    }

    private async Task CollectGameMechanicsAsync()
    {
        Console.WriteLine("=== Step 2: Game Type & Mechanics ===");
        Console.WriteLine();

        Console.WriteLine("Describe the type of game you want to create and its core mechanics:");
        Console.WriteLine("Examples:");
        Console.WriteLine("- 'A cyberpunk RPG with hacking, corporate espionage, and augmented reality'");
        Console.WriteLine("- 'A medieval fantasy game with magic, dragons, and kingdom management'");
        Console.WriteLine("- 'A space exploration game with alien encounters and resource management'");
        Console.WriteLine();

        var gameDescription = GetValidInput(input => !string.IsNullOrWhiteSpace(input), 
            "Please provide a description of your game");

        var mechanicsPrompt = $@"
The user wants to create this type of game: {gameDescription}

Based on this description, please suggest:
1. Primary game mechanics that would fit this genre
2. Victory conditions and progression systems
3. Key game elements like character attributes, resources, or special systems
4. Combat mechanics (if applicable)
5. Social/exploration mechanics

Provide specific, actionable suggestions that I can use to structure the ruleset.
Be encouraging and ask follow-up questions to help refine the concept.";

        Console.WriteLine();
        Console.WriteLine("Let me analyze your game concept and suggest appropriate mechanics...");
        Console.WriteLine();

        await foreach (var chunk in _phaseService.ProcessInputWithSpecialPromptAsync(mechanicsPrompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();

        // Collect specific mechanics choices
        Console.WriteLine();
        Console.WriteLine("Based on the suggestions above, please specify:");
        Console.WriteLine();

        Console.Write("Primary victory condition: ");
        var victoryCondition = Console.ReadLine() ?? "";

        Console.Write("Main progression system (e.g., levels, skills, reputation): ");
        var progressionSystem = Console.ReadLine() ?? "";

        Console.Write("Core mechanics (comma-separated): ");
        var mechanicsInput = Console.ReadLine() ?? "";
        var mechanics = mechanicsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim()).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();

        _rulesetBuilder.SetGameMechanics(gameDescription, victoryCondition, progressionSystem, mechanics);
        Console.WriteLine("✓ Game mechanics saved");
        Console.WriteLine();
    }

    private async Task CollectGameStateSchemaAsync()
    {
        Console.WriteLine("=== Step 3: Game State Schema ===");
        Console.WriteLine();

        var mechanics = _rulesetBuilder.GetCurrentMechanics();
        var schemaPrompt = $@"
Based on these game mechanics: {string.Join(", ", mechanics.CoreMechanics)}
Victory condition: {mechanics.VictoryCondition}
Progression system: {mechanics.ProgressionSystem}

Please suggest a game state schema including:
1. Required collections (e.g., characters, items, locations, quests)
2. Player fields (e.g., level, health, inventory, abilities)  
3. Dynamic collections with their entity types

Explain why each collection and field is important for this type of game.
Format your response clearly so the user can make informed decisions.";

        Console.WriteLine("Let me suggest a game state schema based on your mechanics...");
        Console.WriteLine();

        await foreach (var chunk in _phaseService.ProcessInputWithSpecialPromptAsync(schemaPrompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();

        // Collect schema choices
        Console.WriteLine();
        Console.WriteLine("Based on the suggestions above, please specify:");
        Console.WriteLine();

        Console.Write("Required collections (comma-separated): ");
        var collectionsInput = Console.ReadLine() ?? "";
        var collections = collectionsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim()).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        Console.Write("Player fields (comma-separated): ");
        var fieldsInput = Console.ReadLine() ?? "";
        var playerFields = fieldsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList();

        Console.WriteLine();
        Console.WriteLine("For dynamic collections, specify the entity type for each:");
        var dynamicCollections = new Dictionary<string, string>();
        foreach (var collection in collections)
        {
            Console.Write($"Entity type for '{collection}' collection: ");
            var entityType = Console.ReadLine() ?? "Entity";
            dynamicCollections[collection] = entityType;
        }

        _rulesetBuilder.SetGameStateSchema(collections, playerFields, dynamicCollections);
        Console.WriteLine("✓ Game state schema saved");
        Console.WriteLine();
    }

    private async Task CollectGameDataAsync()
    {
        Console.WriteLine("=== Step 4: Game Data Definition ===");
        Console.WriteLine();

        var mechanics = _rulesetBuilder.GetCurrentMechanics();
        var dataPrompt = $@"
For this game with mechanics: {string.Join(", ", mechanics.CoreMechanics)}

Please suggest game data to include, such as:
1. Character classes/archetypes (if applicable)
2. Races/species/backgrounds  
3. Equipment and items
4. Abilities or special powers
5. Any other game-specific data

Provide specific examples with stats and descriptions that fit the theme.
Keep the scope manageable for a custom ruleset - suggest 3-5 items in each category.";

        Console.WriteLine("Let me suggest appropriate game data for your ruleset...");
        Console.WriteLine();

        await foreach (var chunk in _phaseService.ProcessInputWithSpecialPromptAsync(dataPrompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();

        // For now, allow user to specify they want to include certain data types
        Console.WriteLine();
        Console.Write("Include character classes? (y/n): ");
        var includeClasses = Console.ReadLine()?.ToLower().StartsWith("y") == true;

        Console.Write("Include items/equipment? (y/n): ");
        var includeItems = Console.ReadLine()?.ToLower().StartsWith("y") == true;

        Console.Write("Include special abilities? (y/n): ");
        var includeAbilities = Console.ReadLine()?.ToLower().StartsWith("y") == true;

        _rulesetBuilder.SetGameDataFlags(includeClasses, includeItems, includeAbilities);
        Console.WriteLine("✓ Game data configuration saved");
        Console.WriteLine();
    }

    private async Task CollectFunctionDefinitionsAsync()
    {
        Console.WriteLine("=== Step 5: Function Definitions ===");
        Console.WriteLine();

        var mechanics = _rulesetBuilder.GetCurrentMechanics();
        var functionsPrompt = $@"
For this game with mechanics: {string.Join(", ", mechanics.CoreMechanics)}

I need to generate function definitions for these game phases:
1. GameSetup - Initial character creation and game setup
2. WorldGeneration - Creating the game world  
3. Exploration - Main gameplay and exploration
4. Combat - Battle encounters (if applicable)
5. LevelUp - Character progression

For each phase, suggest 3-5 specific functions that would be needed.
Each function should have a clear name, description, parameters, and what it accomplishes.
Focus on functions that support the core mechanics you identified.";

        Console.WriteLine("Let me suggest functions for each game phase...");
        Console.WriteLine();

        await foreach (var chunk in _phaseService.ProcessInputWithSpecialPromptAsync(functionsPrompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();

        // For initial implementation, we'll generate basic functions
        // User can later customize these through editing
        _rulesetBuilder.GenerateBasicFunctions();
        Console.WriteLine("✓ Basic function definitions generated");
        Console.WriteLine();
    }

    private async Task CollectPromptTemplatesAsync()
    {
        Console.WriteLine("=== Step 6: Prompt Templates ===");
        Console.WriteLine();

        var metadata = _rulesetBuilder.GetCurrentMetadata();
        var mechanics = _rulesetBuilder.GetCurrentMechanics();
        
        var promptsTemplatePrompt = $@"
For the game '{metadata.Name}': {metadata.Description}
With mechanics: {string.Join(", ", mechanics.CoreMechanics)}

Please suggest system prompt templates for each game phase that will guide the LLM's behavior:

1. GameSetup - Character creation and initial setup
2. WorldGeneration - Creating locations and NPCs
3. Exploration - Main gameplay narration and interaction
4. Combat - Battle descriptions and mechanics (if applicable)  
5. LevelUp - Character progression and rewards

Each prompt should establish the tone, style, and behavior appropriate for this type of game.
Include placeholders for dynamic content like {{character_info}}, {{current_location}}, etc.";

        Console.WriteLine("Let me suggest appropriate prompt templates for your game...");
        Console.WriteLine();

        await foreach (var chunk in _phaseService.ProcessInputWithSpecialPromptAsync(promptsTemplatePrompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();

        _rulesetBuilder.GenerateBasicPromptTemplates();
        Console.WriteLine("✓ Basic prompt templates generated");
        Console.WriteLine();
    }

    private async Task<string> FinalizeRulesetAsync()
    {
        Console.WriteLine("=== Final Validation & Save ===");
        Console.WriteLine();

        // Build the complete ruleset
        var ruleset = _rulesetBuilder.BuildRuleset();
        
        // Validate against schema
        var validationResult = await _schemaValidator.ValidateRulesetAsync(ruleset);
        
        if (!validationResult.IsValid)
        {
            Console.WriteLine("⚠ Validation warnings found:");
            foreach (var warning in validationResult.Warnings)
            {
                Console.WriteLine($"  - {warning}");
            }
            Console.WriteLine();
        }

        if (validationResult.HasErrors)
        {
            Console.WriteLine("❌ Validation errors found:");
            foreach (var error in validationResult.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
            throw new InvalidOperationException("Ruleset validation failed. Cannot save ruleset with errors.");
        }

        // Save the ruleset
        var rulesetId = await _rulesetBuilder.SaveRulesetAsync();
        
        Console.WriteLine("✓ Ruleset validation passed");
        Console.WriteLine($"✓ Ruleset saved as: {rulesetId}");
        
        return rulesetId;
    }

    private async Task ChooseEditSectionAsync()
    {
        Console.WriteLine("What would you like to edit?");
        Console.WriteLine("1. Basic metadata (name, description, tags)");
        Console.WriteLine("2. Game mechanics and victory conditions");
        Console.WriteLine("3. Game state schema");
        Console.WriteLine("4. Game data (classes, items, abilities)");
        Console.WriteLine("5. Function definitions");
        Console.WriteLine("6. Prompt templates");
        Console.WriteLine("7. Complete review and save");
        Console.WriteLine();

        var choice = GetValidInput(
            input => int.TryParse(input, out var num) && num >= 1 && num <= 7,
            "Please enter a number between 1 and 7");

        switch (int.Parse(choice))
        {
            case 1:
                await CollectBasicMetadataAsync();
                break;
            case 2:
                await CollectGameMechanicsAsync();
                break;
            case 3:
                await CollectGameStateSchemaAsync();
                break;
            case 4:
                await CollectGameDataAsync();
                break;
            case 5:
                await CollectFunctionDefinitionsAsync();
                break;
            case 6:
                await CollectPromptTemplatesAsync();
                break;
            case 7:
                return; // Exit to finalization
        }

        // Ask if they want to edit more sections
        Console.WriteLine();
        Console.Write("Would you like to edit another section? (y/n): ");
        if (Console.ReadLine()?.ToLower().StartsWith("y") == true)
        {
            await ChooseEditSectionAsync();
        }
    }

    private static string GetValidInput(Func<string, bool> validator, string errorMessage)
    {
        while (true)
        {
            var input = Console.ReadLine() ?? "";
            if (validator(input))
                return input;
            
            Console.WriteLine(errorMessage);
            Console.Write("Please try again: ");
        }
    }
}