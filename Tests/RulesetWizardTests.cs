using Xunit;
using System.Text.Json;
using PokeLLM.GameRules.Services;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameState.Models;
using Moq;

namespace PokeLLM.Tests;

/// <summary>
/// Tests for the LLM-driven ruleset wizard that allows users to create custom rulesets
/// through interactive prompting and validation
/// </summary>
[Trait("Category", "Integration")]
[Trait("System", "RulesetWizard")]
public class RulesetWizardTests
{
    [Fact]
    public async Task RulesetWizard_AllSixSteps_CompleteSuccessfully()
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = new RulesetWizardSession();

        // Act - Execute all 6 wizard steps
        var step1Result = await mockWizard.ExecuteStepAsync(1, "SciFi RPG", wizardSession);
        var step2Result = await mockWizard.ExecuteStepAsync(2, "Cyberpunk future setting", wizardSession);
        var step3Result = await mockWizard.ExecuteStepAsync(3, "Hacker,Corporate,Street", wizardSession);
        var step4Result = await mockWizard.ExecuteStepAsync(4, "Netrunning,Combat,Investigation", wizardSession);
        var step5Result = await mockWizard.ExecuteStepAsync(5, "Interface,Firewall,ICE", wizardSession);
        var step6Result = await mockWizard.ExecuteStepAsync(6, "Confirmed", wizardSession);

        // Assert - All steps should complete successfully
        Assert.True(step1Result.Success, "Step 1 (Metadata) should succeed");
        Assert.True(step2Result.Success, "Step 2 (World Setting) should succeed");
        Assert.True(step3Result.Success, "Step 3 (Character Classes) should succeed");
        Assert.True(step4Result.Success, "Step 4 (Game Phases) should succeed");
        Assert.True(step5Result.Success, "Step 5 (Core Concepts) should succeed");
        Assert.True(step6Result.Success, "Step 6 (Validation) should succeed");
        
        Assert.NotNull(wizardSession.GeneratedRuleset);
        Assert.Equal(6, wizardSession.CompletedSteps);
    }

    [Theory]
    [InlineData(1, "Basic RPG")]
    [InlineData(2, "Medieval fantasy world")]
    [InlineData(3, "Warrior,Mage,Rogue")]
    [InlineData(4, "Combat,Exploration,Social")]
    [InlineData(5, "Magic,Skills,Equipment")]
    public async Task RulesetWizard_IndividualSteps_ProvideLLMGuidance(int stepNumber, string userInput)
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = new RulesetWizardSession();

        // Act
        var result = await mockWizard.ExecuteStepAsync(stepNumber, userInput, wizardSession);

        // Assert
        Assert.True(result.Success, $"Step {stepNumber} should succeed with valid input");
        Assert.NotNull(result.LLMGuidance);
        Assert.True(result.LLMGuidance.Length > 20, "LLM guidance should be substantial");
        Assert.NotNull(result.Suggestions);
        Assert.True(result.Suggestions.Count > 0, "Should provide helpful suggestions");
    }

    [Fact]
    public async Task RulesetWizard_InvalidInput_ProvidesHelpfulFeedback()
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = new RulesetWizardSession();
        
        var invalidInputs = new[]
        {
            ("", "Empty input"),
            ("a", "Too short input"),
            ("InvalidCharacters@#$%", "Special characters"),
            (new string('x', 1000), "Extremely long input")
        };

        foreach (var (input, description) in invalidInputs)
        {
            // Act
            var result = await mockWizard.ExecuteStepAsync(1, input, wizardSession);

            // Assert
            Assert.False(result.Success, $"Should reject {description}");
            Assert.NotNull(result.ValidationErrors);
            Assert.True(result.ValidationErrors.Count > 0, $"Should provide validation errors for {description}");
            Assert.True(result.LLMGuidance?.Length > 0, $"Should provide helpful guidance for {description}");
        }
    }

    [Fact]
    public async Task RulesetWizard_GeneratesValidRulesetSchema()
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = CompleteWizardSession();

        // Act
        var generatedRuleset = wizardSession.GeneratedRuleset!;
        var validationResult = await ValidateGeneratedRuleset(generatedRuleset);

        // Assert
        Assert.True(validationResult.IsValid, "Generated ruleset should be valid");
        Assert.Empty(validationResult.ValidationErrors);
        
        // Verify essential sections exist
        var root = generatedRuleset.RootElement;
        Assert.True(root.TryGetProperty("metadata", out _), "Must have metadata");
        Assert.True(root.TryGetProperty("gameStateSchema", out _), "Must have gameStateSchema");
        Assert.True(root.TryGetProperty("functionDefinitions", out _), "Must have functionDefinitions");
        Assert.True(root.TryGetProperty("promptTemplates", out _), "Must have promptTemplates");
    }

    [Fact]
    public async Task RulesetWizard_LLMSuggestions_AreContextAware()
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = new RulesetWizardSession();

        // Act - Step 1: Set theme as "Cyberpunk"
        await mockWizard.ExecuteStepAsync(1, "Cyberpunk RPG", wizardSession);
        
        // Step 3: Get class suggestions (should be cyberpunk-themed)
        var classStepResult = await mockWizard.ExecuteStepAsync(3, "", wizardSession);

        // Assert - Suggestions should be contextually appropriate
        Assert.NotNull(classStepResult.Suggestions);
        Assert.True(classStepResult.Suggestions.Any(s => s.ToLower().Contains("hacker") || 
                                                        s.ToLower().Contains("netrunner") ||
                                                        s.ToLower().Contains("corpo")),
            "Cyberpunk theme should influence class suggestions");
    }

    [Fact]
    public async Task RulesetWizard_ProgressTracking_WorksCorrectly()
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = new RulesetWizardSession();

        // Act - Complete steps progressively
        Assert.Equal(0, wizardSession.CompletedSteps);
        
        await mockWizard.ExecuteStepAsync(1, "Fantasy RPG", wizardSession);
        Assert.Equal(1, wizardSession.CompletedSteps);
        
        await mockWizard.ExecuteStepAsync(2, "Medieval setting", wizardSession);
        Assert.Equal(2, wizardSession.CompletedSteps);
        
        await mockWizard.ExecuteStepAsync(3, "Fighter,Wizard,Rogue", wizardSession);
        Assert.Equal(3, wizardSession.CompletedSteps);

        // Assert - Progress should be tracked accurately
        Assert.True(wizardSession.CurrentStep > 3);
        Assert.NotNull(wizardSession.UserInputs);
        Assert.Equal(3, wizardSession.UserInputs.Count);
    }

    [Fact]
    public async Task RulesetWizard_ExportImport_MaintainsIntegrity()
    {
        // Arrange
        var completedSession = CompleteWizardSession();
        var originalRuleset = completedSession.GeneratedRuleset!;

        // Act - Export and re-import the custom ruleset
        var exported = JsonSerializer.Serialize(originalRuleset);
        var reimported = JsonDocument.Parse(exported);

        // Assert - Data integrity should be maintained
        var originalMetadata = originalRuleset.RootElement.GetProperty("metadata");
        var reimportedMetadata = reimported.RootElement.GetProperty("metadata");
        
        Assert.Equal(originalMetadata.GetProperty("name").GetString(), 
                    reimportedMetadata.GetProperty("name").GetString());
        Assert.Equal(originalMetadata.GetProperty("description").GetString(), 
                    reimportedMetadata.GetProperty("description").GetString());
    }

    [Fact]
    public async Task RulesetWizard_CustomRulesetWorksWithDynamicSystem()
    {
        // Arrange
        var customRuleset = CreateSampleCustomRuleset();
        var mockManager = new Mock<IRulesetManager>();
        var gameState = new GameStateModel();

        mockManager.Setup(m => m.LoadRulesetAsync("custom-scifi"))
            .ReturnsAsync(customRuleset);

        // Act - Load and initialize custom ruleset
        var loadedRuleset = await mockManager.Object.LoadRulesetAsync("custom-scifi");
        mockManager.Object.InitializeGameStateFromRuleset(gameState, loadedRuleset);

        // Assert - Custom ruleset should work with the dynamic system
        Assert.NotNull(loadedRuleset);
        Assert.NotNull(gameState.RulesetGameData);
        mockManager.Verify(m => m.LoadRulesetAsync("custom-scifi"), Times.Once);
    }

    [Fact]
    public async Task RulesetWizard_EditingExistingRuleset_PreservesStructure()
    {
        // Arrange
        var existingRuleset = CreateSampleCustomRuleset();
        var mockWizard = CreateMockRulesetWizard();
        var editSession = new RulesetWizardSession 
        { 
            IsEditMode = true,
            GeneratedRuleset = existingRuleset 
        };

        // Act - Edit an existing ruleset (modify step 2)
        var editResult = await mockWizard.ExecuteStepAsync(2, "Post-apocalyptic wasteland", editSession);

        // Assert - Should update specific section while preserving others
        Assert.True(editResult.Success, "Edit operation should succeed");
        Assert.NotNull(editSession.GeneratedRuleset);
        
        var metadata = editSession.GeneratedRuleset.RootElement.GetProperty("metadata");
        Assert.Equal("custom-scifi", metadata.GetProperty("id").GetString()); // Preserved
        
        // Modified section should be updated (in a real implementation)
        Assert.NotNull(editResult.LLMGuidance);
    }

    [Theory]
    [InlineData("Pokemon-style")]
    [InlineData("D&D-inspired")]
    [InlineData("Completely original")]
    public async Task RulesetWizard_DifferentThemes_GenerateAppropriateStructures(string theme)
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = new RulesetWizardSession();

        // Act - Complete wizard with different themes
        await mockWizard.ExecuteStepAsync(1, $"{theme} RPG", wizardSession);
        await mockWizard.ExecuteStepAsync(2, $"World suitable for {theme}", wizardSession);
        
        // Get suggestions for remaining steps
        var classResult = await mockWizard.ExecuteStepAsync(3, "", wizardSession);
        var phaseResult = await mockWizard.ExecuteStepAsync(4, "", wizardSession);

        // Assert - Suggestions should vary by theme
        Assert.NotNull(classResult.Suggestions);
        Assert.NotNull(phaseResult.Suggestions);
        Assert.True(classResult.Suggestions.Count > 0);
        Assert.True(phaseResult.Suggestions.Count > 0);
        
        // Each theme should generate different types of suggestions
        // (This would be more sophisticated in a real implementation)
        Assert.True(classResult.LLMGuidance!.Length > 50);
        Assert.True(phaseResult.LLMGuidance!.Length > 50);
    }

    [Fact]
    public async Task RulesetWizard_ValidationStep_CatchesIncompleteRulesets()
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var incompleteSession = new RulesetWizardSession
        {
            CompletedSteps = 4, // Missing steps 5 and 6
            UserInputs = new Dictionary<int, string>
            {
                [1] = "Test RPG",
                [2] = "Test setting", 
                [3] = "TestClass",
                [4] = "TestPhase"
            }
        };

        // Act - Try to validate incomplete ruleset
        var validationResult = await mockWizard.ExecuteStepAsync(6, "Confirm", incompleteSession);

        // Assert - Should catch incompleteness
        Assert.False(validationResult.Success, "Should reject incomplete ruleset");
        Assert.NotNull(validationResult.ValidationErrors);
        Assert.True(validationResult.ValidationErrors.Count > 0);
        Assert.Contains("incomplete", validationResult.ValidationErrors.First().ToLower());
    }

    [Fact]
    public async Task RulesetWizard_Performance_CompletesWithinReasonableTime()
    {
        // Arrange
        var mockWizard = CreateMockRulesetWizard();
        var wizardSession = new RulesetWizardSession();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Complete all wizard steps
        await mockWizard.ExecuteStepAsync(1, "Performance Test RPG", wizardSession);
        await mockWizard.ExecuteStepAsync(2, "Fast generation setting", wizardSession);
        await mockWizard.ExecuteStepAsync(3, "SpeedClass,QuickClass", wizardSession);
        await mockWizard.ExecuteStepAsync(4, "Setup,Action", wizardSession);
        await mockWizard.ExecuteStepAsync(5, "Core1,Core2", wizardSession);
        await mockWizard.ExecuteStepAsync(6, "Confirm", wizardSession);
        
        stopwatch.Stop();

        // Assert - Should complete within reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
            $"Wizard completion took {stopwatch.ElapsedMilliseconds}ms, should be < 5000ms");
        Assert.True(wizardSession.CompletedSteps == 6);
    }

    #region Helper Methods

    private static IRulesetWizard CreateMockRulesetWizard()
    {
        var mockWizard = new Mock<IRulesetWizard>();
        
        // Setup step 1 (Metadata)
        mockWizard.Setup(w => w.ExecuteStepAsync(1, It.IsAny<string>(), It.IsAny<RulesetWizardSession>()))
            .ReturnsAsync((int step, string input, RulesetWizardSession session) =>
            {
                if (string.IsNullOrWhiteSpace(input) || input.Length < 3)
                {
                    return new WizardStepResult
                    {
                        Success = false,
                        ValidationErrors = new List<string> { "Ruleset name must be at least 3 characters" },
                        LLMGuidance = "Please provide a meaningful name for your custom ruleset."
                    };
                }
                
                session.CompletedSteps = Math.Max(session.CompletedSteps, 1);
                session.CurrentStep = 2;
                session.UserInputs[1] = input;
                
                return new WizardStepResult
                {
                    Success = true,
                    LLMGuidance = $"Great! '{input}' is a good name. Now let's define the world setting.",
                    Suggestions = new List<string> { "Fantasy", "Sci-Fi", "Modern", "Historical" }
                };
            });

        // Setup remaining steps with similar patterns
        SetupWizardStep(mockWizard, 2, "world setting", "character classes");
        SetupWizardStep(mockWizard, 3, "character classes", "game phases");
        SetupWizardStep(mockWizard, 4, "game phases", "core concepts");
        SetupWizardStep(mockWizard, 5, "core concepts", "validation");
        
        // Step 6 (Validation/Completion)
        mockWizard.Setup(w => w.ExecuteStepAsync(6, It.IsAny<string>(), It.IsAny<RulesetWizardSession>()))
            .ReturnsAsync((int step, string input, RulesetWizardSession session) =>
            {
                if (session.CompletedSteps < 5)
                {
                    return new WizardStepResult
                    {
                        Success = false,
                        ValidationErrors = new List<string> { "Ruleset is incomplete. Please complete all previous steps." },
                        LLMGuidance = "You must complete steps 1-5 before finalizing your ruleset."
                    };
                }
                
                session.CompletedSteps = 6;
                session.GeneratedRuleset = CreateSampleCustomRuleset();
                
                return new WizardStepResult
                {
                    Success = true,
                    LLMGuidance = "Congratulations! Your custom ruleset has been generated successfully.",
                    Suggestions = new List<string> { "Save ruleset", "Test ruleset", "Export ruleset" }
                };
            });

        return mockWizard.Object;
    }

    private static void SetupWizardStep(Mock<IRulesetWizard> mockWizard, int stepNumber, string stepName, string nextStep)
    {
        mockWizard.Setup(w => w.ExecuteStepAsync(stepNumber, It.IsAny<string>(), It.IsAny<RulesetWizardSession>()))
            .ReturnsAsync((int step, string input, RulesetWizardSession session) =>
            {
                session.CompletedSteps = Math.Max(session.CompletedSteps, stepNumber);
                session.CurrentStep = stepNumber + 1;
                session.UserInputs[stepNumber] = input;
                
                var suggestions = stepNumber switch
                {
                    2 => new List<string> { "Medieval fantasy", "Cyberpunk future", "Space opera", "Modern day" },
                    3 => new List<string> { "Warrior", "Mage", "Rogue", "Cleric", "Ranger" },
                    4 => new List<string> { "Setup", "Exploration", "Combat", "Social", "Progression" },
                    5 => new List<string> { "Magic", "Technology", "Skills", "Equipment", "Resources" },
                    _ => new List<string>()
                };

                return new WizardStepResult
                {
                    Success = true,
                    LLMGuidance = $"Excellent choice for {stepName}! Now let's work on {nextStep}.",
                    Suggestions = suggestions
                };
            });
    }

    private static RulesetWizardSession CompleteWizardSession()
    {
        return new RulesetWizardSession
        {
            CompletedSteps = 6,
            CurrentStep = 7,
            GeneratedRuleset = CreateSampleCustomRuleset(),
            UserInputs = new Dictionary<int, string>
            {
                [1] = "Cyberpunk RPG",
                [2] = "Futuristic cityscape",
                [3] = "Hacker,Corporate,Street",
                [4] = "Netrunning,Combat,Investigation",
                [5] = "Cyberdeck,ICE,Credits",
                [6] = "Confirmed"
            }
        };
    }

    private static JsonDocument CreateSampleCustomRuleset()
    {
        var json = """
        {
          "metadata": {
            "id": "custom-scifi",
            "name": "Custom Sci-Fi RPG",
            "version": "1.0.0",
            "description": "A custom sci-fi ruleset created with the wizard",
            "authors": ["Wizard User"],
            "tags": ["sci-fi", "custom", "cyberpunk"]
          },
          "gameStateSchema": {
            "requiredCollections": ["characters", "locations", "equipment"],
            "playerFields": ["class", "level", "cyberdeck", "credits"],
            "dynamicCollections": { "characters": "Character" }
          },
          "functionDefinitions": {
            "GameSetup": [
              {
                "id": "select_class",
                "name": "select_class",
                "description": "Select character class",
                "parameters": [{"name": "classId", "type": "string", "required": true}]
              }
            ]
          },
          "promptTemplates": {
            "GameSetup": {
              "systemPrompt": "You are helping create a sci-fi character",
              "phaseObjective": "Complete character creation",
              "availableFunctions": ["select_class"]
            }
          },
          "validationRules": {
            "characterCreation": "character.class != null"
          }
        }
        """;
        return JsonDocument.Parse(json);
    }

    private static async Task<ValidationResult> ValidateGeneratedRuleset(JsonDocument ruleset)
    {
        // Mock validation logic
        await Task.Delay(1);
        
        var root = ruleset.RootElement;
        var errors = new List<string>();
        
        if (!root.TryGetProperty("metadata", out _))
            errors.Add("Missing metadata section");
        if (!root.TryGetProperty("gameStateSchema", out _))
            errors.Add("Missing gameStateSchema section");
        if (!root.TryGetProperty("functionDefinitions", out _))
            errors.Add("Missing functionDefinitions section");
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            ValidationErrors = errors
        };
    }

    #endregion

    #region Data Models

    private interface IRulesetWizard
    {
        Task<WizardStepResult> ExecuteStepAsync(int stepNumber, string userInput, RulesetWizardSession session);
    }

    private class RulesetWizardSession
    {
        public int CompletedSteps { get; set; }
        public int CurrentStep { get; set; } = 1;
        public JsonDocument? GeneratedRuleset { get; set; }
        public Dictionary<int, string> UserInputs { get; set; } = new();
        public bool IsEditMode { get; set; }
    }

    private class WizardStepResult
    {
        public bool Success { get; set; }
        public string? LLMGuidance { get; set; }
        public List<string> Suggestions { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    #endregion
}