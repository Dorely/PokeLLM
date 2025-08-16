using PokeLLM.GameRules.Interfaces;

namespace PokeLLM.GameLogic.Services;

/// <summary>
/// Service for handling ruleset selection before starting a new game
/// </summary>
public interface IRulesetSelectionService
{
    /// <summary>
    /// Display available rulesets and allow user to select one
    /// </summary>
    Task<string> SelectRulesetAsync();
    
    /// <summary>
    /// Display available rulesets with option to create custom ruleset
    /// </summary>
    Task<string> SelectRulesetWithWizardAsync();
}

public class RulesetSelectionService : IRulesetSelectionService
{
    private readonly IRulesetManager _rulesetManager;
    private readonly IRulesetWizardService _rulesetWizard;

    public RulesetSelectionService(IRulesetManager rulesetManager, IRulesetWizardService rulesetWizard)
    {
        _rulesetManager = rulesetManager;
        _rulesetWizard = rulesetWizard;
    }

    public async Task<string> SelectRulesetAsync()
    {
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("         POKELLM ADVENTURE SYSTEM        ");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        
        var availableRulesets = await _rulesetManager.GetAvailableRulesetsAsync();
        
        if (availableRulesets.Count == 0)
        {
            Console.WriteLine("No rulesets found. Using default pokemon-adventure ruleset.");
            return "pokemon-adventure";
        }

        if (availableRulesets.Count == 1)
        {
            var singleRuleset = availableRulesets.First();
            Console.WriteLine($"Only one ruleset available: {singleRuleset.Name}");
            Console.WriteLine($"Description: {singleRuleset.Description}");
            Console.WriteLine();
            return singleRuleset.Id;
        }

        Console.WriteLine("Select a ruleset for your adventure:");
        Console.WriteLine();

        for (int i = 0; i < availableRulesets.Count; i++)
        {
            var ruleset = availableRulesets[i];
            Console.WriteLine($"{i + 1}. {ruleset.Name} (v{ruleset.Version})");
            Console.WriteLine($"   {ruleset.Description}");
            if (ruleset.Tags.Any())
            {
                Console.WriteLine($"   Tags: {string.Join(", ", ruleset.Tags)}");
            }
            Console.WriteLine();
        }

        while (true)
        {
            Console.Write($"Enter your choice (1-{availableRulesets.Count}): ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= availableRulesets.Count)
            {
                var selectedRuleset = availableRulesets[choice - 1];
                Console.WriteLine();
                Console.WriteLine($"Selected: {selectedRuleset.Name}");
                Console.WriteLine($"{selectedRuleset.Description}");
                Console.WriteLine();
                Console.WriteLine("Starting your adventure...");
                Console.WriteLine();
                return selectedRuleset.Id;
            }

            Console.WriteLine($"Invalid choice. Please enter a number between 1 and {availableRulesets.Count}.");
        }
    }

    public async Task<string> SelectRulesetWithWizardAsync()
    {
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("         POKELLM ADVENTURE SYSTEM        ");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        
        var availableRulesets = await _rulesetManager.GetAvailableRulesetsAsync();
        
        Console.WriteLine("Select a ruleset for your adventure:");
        Console.WriteLine();

        // Display existing rulesets
        for (int i = 0; i < availableRulesets.Count; i++)
        {
            var ruleset = availableRulesets[i];
            Console.WriteLine($"{i + 1}. {ruleset.Name} (v{ruleset.Version})");
            Console.WriteLine($"   {ruleset.Description}");
            if (ruleset.Tags.Any())
            {
                Console.WriteLine($"   Tags: {string.Join(", ", ruleset.Tags)}");
            }
            Console.WriteLine();
        }

        // Add wizard options
        var createOption = availableRulesets.Count + 1;
        var loadCustomOption = availableRulesets.Count + 2;

        Console.WriteLine($"{createOption}. Create Custom Ruleset (NEW)");
        Console.WriteLine("   Use the AI-powered wizard to create your own game ruleset");
        Console.WriteLine();

        Console.WriteLine($"{loadCustomOption}. Load Custom Ruleset from File");
        Console.WriteLine("   Load a previously created custom ruleset for editing");
        Console.WriteLine();

        var maxChoice = loadCustomOption;

        while (true)
        {
            Console.Write($"Enter your choice (1-{maxChoice}): ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= maxChoice)
            {
                if (choice <= availableRulesets.Count)
                {
                    // Existing ruleset selected
                    var selectedRuleset = availableRulesets[choice - 1];
                    Console.WriteLine();
                    Console.WriteLine($"Selected: {selectedRuleset.Name}");
                    Console.WriteLine($"{selectedRuleset.Description}");
                    Console.WriteLine();
                    Console.WriteLine("Starting your adventure...");
                    Console.WriteLine();
                    return selectedRuleset.Id;
                }
                else if (choice == createOption)
                {
                    // Create new custom ruleset
                    Console.WriteLine();
                    Console.WriteLine("Starting ruleset creation wizard...");
                    Console.WriteLine();
                    
                    var newRulesetId = await _rulesetWizard.CreateRulesetInteractivelyAsync();
                    
                    if (string.IsNullOrWhiteSpace(newRulesetId))
                    {
                        Console.WriteLine("Ruleset creation was cancelled. Please select an existing ruleset.");
                        Console.WriteLine();
                        continue; // Go back to selection
                    }
                    
                    return newRulesetId;
                }
                else if (choice == loadCustomOption)
                {
                    // Load custom ruleset for editing
                    Console.WriteLine();
                    Console.WriteLine("Available custom rulesets:");
                    
                    var customRulesets = availableRulesets
                        .Where(r => r.Id.StartsWith("custom-"))
                        .ToList();
                    
                    if (!customRulesets.Any())
                    {
                        Console.WriteLine("No custom rulesets found. Create one first using option " + createOption);
                        Console.WriteLine();
                        continue;
                    }
                    
                    for (int i = 0; i < customRulesets.Count; i++)
                    {
                        var ruleset = customRulesets[i];
                        Console.WriteLine($"{i + 1}. {ruleset.Name} (v{ruleset.Version})");
                        Console.WriteLine($"   {ruleset.Description}");
                    }
                    Console.WriteLine();
                    
                    Console.Write($"Enter custom ruleset number (1-{customRulesets.Count}) or 0 to go back: ");
                    var customInput = Console.ReadLine();
                    
                    if (int.TryParse(customInput, out int customChoice))
                    {
                        if (customChoice == 0)
                        {
                            continue; // Go back to main selection
                        }
                        else if (customChoice >= 1 && customChoice <= customRulesets.Count)
                        {
                            var selectedCustomRuleset = customRulesets[customChoice - 1];
                            
                            Console.WriteLine();
                            Console.Write($"Do you want to (1) Use '{selectedCustomRuleset.Name}' as-is or (2) Edit it? Enter 1 or 2: ");
                            var actionInput = Console.ReadLine();
                            
                            if (actionInput == "1")
                            {
                                Console.WriteLine($"Selected: {selectedCustomRuleset.Name}");
                                Console.WriteLine("Starting your adventure...");
                                Console.WriteLine();
                                return selectedCustomRuleset.Id;
                            }
                            else if (actionInput == "2")
                            {
                                Console.WriteLine("Starting ruleset editing wizard...");
                                Console.WriteLine();
                                
                                var editedRulesetId = await _rulesetWizard.EditRulesetInteractivelyAsync(selectedCustomRuleset.Id);
                                
                                if (string.IsNullOrWhiteSpace(editedRulesetId))
                                {
                                    Console.WriteLine("Ruleset editing was cancelled. Please select an existing ruleset.");
                                    Console.WriteLine();
                                    continue;
                                }
                                
                                return editedRulesetId;
                            }
                        }
                    }
                    
                    Console.WriteLine("Invalid selection. Please try again.");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine($"Invalid choice. Please enter a number between 1 and {maxChoice}.");
            }
        }
    }
}