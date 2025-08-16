using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using PokeLLM.Game;
using PokeLLM.Game.GameLogic;
using PokeLLM.Game.Orchestration;
using PokeLLM.Game.Plugins;
using PokeLLM.Game.VectorStore.Interfaces;
using PokeLLM.Game.LLM.Interfaces;
using PokeLLM.GameState;
using PokeLLM.GameRules.Interfaces;
using PokeLLM.GameLogic;
using PokeLLM.GameLogic.Services;
using PokeLLM.Plugins;

namespace PokeLLM.Tests;

public class DependencyResolutionTests
{
    [Fact]
    public void CoreServices_ShouldResolve()
    {
        var provider = Program.BuildServiceProvider();

        // Core game services
        var gameController = provider.GetRequiredService<IGameController>();
        var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();
        var vectorStoreService = provider.GetRequiredService<IVectorStoreService>();
        var entityService = provider.GetRequiredService<IEntityService>();

        Assert.NotNull(gameController);
        Assert.NotNull(gameStateRepository);
        Assert.NotNull(vectorStoreService);
        Assert.NotNull(entityService);
    }

    [Fact]
    public void GameLogicServices_ShouldResolve()
    {
        var provider = Program.BuildServiceProvider();

        var gameLogicService = provider.GetRequiredService<IGameLogicService>();
        var characterManagementService = provider.GetRequiredService<ICharacterManagementService>();
        var informationManagementService = provider.GetRequiredService<IInformationManagementService>();
        var npcManagementService = provider.GetRequiredService<INpcManagementService>();
        var worldManagementService = provider.GetRequiredService<IWorldManagementService>();
        var rulesetSelectionService = provider.GetRequiredService<IRulesetSelectionService>();
        
        Assert.NotNull(gameLogicService);
        Assert.NotNull(characterManagementService);
        Assert.NotNull(informationManagementService);
        Assert.NotNull(npcManagementService);
        Assert.NotNull(worldManagementService);
        Assert.NotNull(rulesetSelectionService);
    }

    [Fact]
    public void Plugins_ShouldResolve()
    {
        var provider = Program.BuildServiceProvider();

        var explorationPhasePlugin = provider.GetRequiredService<ExplorationPhasePlugin>();
        var combatPhasePlugin = provider.GetRequiredService<CombatPhasePlugin>();
        var levelUpPhasePlugin = provider.GetRequiredService<LevelUpPhasePlugin>();
        var unifiedContextPlugin = provider.GetRequiredService<UnifiedContextPlugin>();
        var gameSetupPhasePlugin = provider.GetRequiredService<GameSetupPhasePlugin>();
        var worldGenerationPhasePlugin = provider.GetRequiredService<WorldGenerationPhasePlugin>();
        var rulesetManagementPlugin = provider.GetRequiredService<RulesetManagementPlugin>();
        var rulesetWizardPlugin = provider.GetRequiredService<RulesetWizardPlugin>();
        
        Assert.NotNull(explorationPhasePlugin);
        Assert.NotNull(combatPhasePlugin);
        Assert.NotNull(levelUpPhasePlugin);
        Assert.NotNull(unifiedContextPlugin);
        Assert.NotNull(gameSetupPhasePlugin);
        Assert.NotNull(worldGenerationPhasePlugin);
        Assert.NotNull(rulesetManagementPlugin);
        Assert.NotNull(rulesetWizardPlugin);
    }

    [Fact]
    public void RuleSystemServices_ShouldResolve()
    {
        var provider = Program.BuildServiceProvider();

        var javaScriptRuleEngine = provider.GetRequiredService<IJavaScriptRuleEngine>();
        var dynamicFunctionFactory = provider.GetRequiredService<IDynamicFunctionFactory>();
        var rulesetService = provider.GetRequiredService<IRulesetService>();
        var rulesetManager = provider.GetRequiredService<IRulesetManager>();
        
        Assert.NotNull(javaScriptRuleEngine);
        Assert.NotNull(dynamicFunctionFactory);
        Assert.NotNull(rulesetService);
        Assert.NotNull(rulesetManager);
    }

    [Fact]
    public void RulesetWizardServices_ShouldResolve()
    {
        var provider = Program.BuildServiceProvider();

        var rulesetWizardService = provider.GetRequiredService<IRulesetWizardService>();
        var rulesetBuilderService = provider.GetRequiredService<IRulesetBuilderService>();
        var rulesetSchemaValidator = provider.GetRequiredService<IRulesetSchemaValidator>();
        
        Assert.NotNull(rulesetWizardService);
        Assert.NotNull(rulesetBuilderService);
        Assert.NotNull(rulesetSchemaValidator);
    }

    [Fact]
    public void LLMServices_ShouldResolve()
    {
        var provider = Program.BuildServiceProvider();

        var llmProvider = provider.GetRequiredService<ILLMProvider>();
        var embeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        
        Assert.NotNull(llmProvider);
        Assert.NotNull(embeddingGenerator);
    }

    [Fact]
    public void NewArchitectureServices_ShouldResolve()
    {
        var provider = Program.BuildServiceProvider();

        var unifiedContextService = provider.GetRequiredService<IUnifiedContextService>();
        var phaseServiceProvider = provider.GetRequiredService<IPhaseServiceProvider>();
        
        Assert.NotNull(unifiedContextService);
        Assert.NotNull(phaseServiceProvider);
    }

    [Fact]
    public void AllServices_ComprehensiveTest()
    {
        // This test tries to resolve all services at once to catch any circular dependencies
        // or other complex dependency resolution issues
        var provider = Program.BuildServiceProvider();

        try
        {
            // Core services
            var gameController = provider.GetRequiredService<IGameController>();
            var gameStateRepository = provider.GetRequiredService<IGameStateRepository>();
            var vectorStoreService = provider.GetRequiredService<IVectorStoreService>();
            var entityService = provider.GetRequiredService<IEntityService>();

            // Game logic services
            var gameLogicService = provider.GetRequiredService<IGameLogicService>();
            var characterManagementService = provider.GetRequiredService<ICharacterManagementService>();
            var informationManagementService = provider.GetRequiredService<IInformationManagementService>();
            var npcManagementService = provider.GetRequiredService<INpcManagementService>();
            var worldManagementService = provider.GetRequiredService<IWorldManagementService>();
            var rulesetSelectionService = provider.GetRequiredService<IRulesetSelectionService>();

            // Rule system services
            var javaScriptRuleEngine = provider.GetRequiredService<IJavaScriptRuleEngine>();
            var dynamicFunctionFactory = provider.GetRequiredService<IDynamicFunctionFactory>();
            var rulesetService = provider.GetRequiredService<IRulesetService>();
            var rulesetManager = provider.GetRequiredService<IRulesetManager>();

            // Ruleset wizard services
            var rulesetWizardService = provider.GetRequiredService<IRulesetWizardService>();
            var rulesetBuilderService = provider.GetRequiredService<IRulesetBuilderService>();
            var rulesetSchemaValidator = provider.GetRequiredService<IRulesetSchemaValidator>();

            // LLM services
            var llmProvider = provider.GetRequiredService<ILLMProvider>();
            var embeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

            // New architecture services
            var unifiedContextService = provider.GetRequiredService<IUnifiedContextService>();
            var phaseServiceProvider = provider.GetRequiredService<IPhaseServiceProvider>();

            // All services should be non-null
            Assert.NotNull(gameController);
            Assert.NotNull(gameStateRepository);
            Assert.NotNull(vectorStoreService);
            Assert.NotNull(entityService);
            Assert.NotNull(gameLogicService);
            Assert.NotNull(characterManagementService);
            Assert.NotNull(informationManagementService);
            Assert.NotNull(npcManagementService);
            Assert.NotNull(worldManagementService);
            Assert.NotNull(rulesetSelectionService);
            Assert.NotNull(javaScriptRuleEngine);
            Assert.NotNull(dynamicFunctionFactory);
            Assert.NotNull(rulesetService);
            Assert.NotNull(rulesetManager);
            Assert.NotNull(rulesetWizardService);
            Assert.NotNull(rulesetBuilderService);
            Assert.NotNull(rulesetSchemaValidator);
            Assert.NotNull(llmProvider);
            Assert.NotNull(embeddingGenerator);
            Assert.NotNull(unifiedContextService);
            Assert.NotNull(phaseServiceProvider);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to resolve services: {ex.Message}\nInner exception: {ex.InnerException?.Message}");
        }
    }

    [Fact]
    public void ServiceLifetime_ShouldBeCorrect()
    {
        var provider = Program.BuildServiceProvider();

        // Singleton services should return the same instance
        var gameStateRepo1 = provider.GetRequiredService<IGameStateRepository>();
        var gameStateRepo2 = provider.GetRequiredService<IGameStateRepository>();
        Assert.Same(gameStateRepo1, gameStateRepo2);

        var rulesetManager1 = provider.GetRequiredService<IRulesetManager>();
        var rulesetManager2 = provider.GetRequiredService<IRulesetManager>();
        Assert.Same(rulesetManager1, rulesetManager2);

        // Scoped services should return the same instance within the same scope
        using (var scope1 = provider.CreateScope())
        {
            var gameController1 = scope1.ServiceProvider.GetRequiredService<IGameController>();
            var gameController2 = scope1.ServiceProvider.GetRequiredService<IGameController>();
            Assert.Same(gameController1, gameController2);

            var unifiedContext1 = scope1.ServiceProvider.GetRequiredService<IUnifiedContextService>();
            var unifiedContext2 = scope1.ServiceProvider.GetRequiredService<IUnifiedContextService>();
            Assert.Same(unifiedContext1, unifiedContext2);
        }

        // Scoped services should return different instances in different scopes
        using (var scope1 = provider.CreateScope())
        using (var scope2 = provider.CreateScope())
        {
            var gameController1 = scope1.ServiceProvider.GetRequiredService<IGameController>();
            var gameController2 = scope2.ServiceProvider.GetRequiredService<IGameController>();
            Assert.NotSame(gameController1, gameController2);
        }

        // Transient services should return different instances
        var gameLogic1 = provider.GetRequiredService<IGameLogicService>();
        var gameLogic2 = provider.GetRequiredService<IGameLogicService>();
        Assert.NotSame(gameLogic1, gameLogic2);
    }

    [Fact]
    public void CanResolveIPhaseService_ShouldResolve()
    {
        // Test that IPhaseService can be resolved independently
        var provider = Program.BuildServiceProvider();

        var phaseService = provider.GetRequiredService<IPhaseService>();
        
        Assert.NotNull(phaseService);
        Assert.Equal(GameState.Models.GamePhase.Exploration, phaseService.Phase);
    }
}