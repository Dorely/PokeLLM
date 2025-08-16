using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using PokeLLM.GameLogic.Services;

namespace PokeLLM.Plugins;

/// <summary>
/// Plugin for LLM-driven ruleset creation and validation assistance
/// </summary>
public class RulesetWizardPlugin
{
    private readonly IRulesetSchemaValidator _schemaValidator;

    public RulesetWizardPlugin(IRulesetSchemaValidator schemaValidator)
    {
        _schemaValidator = schemaValidator;
    }

    [KernelFunction("suggest_metadata_improvements")]
    [Description("Suggests improvements to ruleset metadata like description and tags")]
    public async Task<string> SuggestMetadataImprovements(
        [Description("Ruleset ID")] string id,
        [Description("Ruleset name")] string name,
        [Description("Current description")] string description,
        [Description("Current tags (comma-separated)")] string tags)
    {
        var suggestions = new List<string>();

        // Analyze description quality
        if (string.IsNullOrWhiteSpace(description))
        {
            suggestions.Add("Consider adding a detailed description that explains the game's theme, setting, and core gameplay elements.");
        }
        else if (description.Length < 50)
        {
            suggestions.Add("Your description could be more detailed. Consider explaining the game's unique features and what makes it engaging.");
        }

        // Analyze tags
        var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        if (tagList.Count == 0)
        {
            suggestions.Add("Adding tags will help players discover your ruleset. Consider tags like: genre (fantasy, sci-fi), mechanics (combat, exploration), or theme (magic, technology).");
        }
        else if (tagList.Count < 3)
        {
            suggestions.Add("Consider adding more descriptive tags to help categorize your game. Think about genre, mechanics, setting, and target audience.");
        }

        // Suggest tags based on content analysis
        var contentSuggestions = AnalyzeContentForTags(name, description);
        if (contentSuggestions.Any())
        {
            suggestions.Add($"Based on your game concept, you might want to consider these additional tags: {string.Join(", ", contentSuggestions)}");
        }

        // ID validation
        if (id.Contains("_") || id.Contains("-"))
        {
            suggestions.Add("Your ID looks good - using hyphens or underscores for readability is a great practice.");
        }

        if (suggestions.Any())
        {
            return "Here are some suggestions for improving your ruleset metadata:\n\n" + string.Join("\n\n", suggestions);
        }
        else
        {
            return "Your metadata looks great! The ID is well-formatted, description is detailed, and you have good tag coverage.";
        }
    }

    [KernelFunction("suggest_game_mechanics")]
    [Description("Suggests appropriate game mechanics based on game description")]
    public async Task<string> SuggestGameMechanics(
        [Description("Description of the game type and theme")] string gameDescription)
    {
        var suggestions = new Dictionary<string, List<string>>();
        var gameDescLower = gameDescription.ToLowerInvariant();

        // Analyze game type and suggest mechanics
        if (gameDescLower.Contains("cyberpunk") || gameDescLower.Contains("hacking") || gameDescLower.Contains("corporate"))
        {
            suggestions["Core Mechanics"] = new List<string>
            {
                "Hacking systems with mini-games or skill checks",
                "Reputation tracking with different factions",
                "Cybernetic enhancement progression",
                "Information gathering and social engineering",
                "Corporate espionage missions"
            };
            suggestions["Victory Conditions"] = new List<string>
            {
                "Expose a major corporate conspiracy",
                "Achieve independence from corporate control",
                "Build a powerful underground network"
            };
        }
        else if (gameDescLower.Contains("fantasy") || gameDescLower.Contains("magic") || gameDescLower.Contains("medieval"))
        {
            suggestions["Core Mechanics"] = new List<string>
            {
                "Spell casting with reagent costs or mana",
                "Character classes with unique abilities",
                "Equipment crafting and enchantment",
                "Guild or faction reputation systems",
                "Dungeon exploration with puzzle elements"
            };
            suggestions["Victory Conditions"] = new List<string>
            {
                "Defeat an ancient evil threatening the realm",
                "Unite the kingdoms under your banner",
                "Master all schools of magic"
            };
        }
        else if (gameDescLower.Contains("space") || gameDescLower.Contains("sci-fi") || gameDescLower.Contains("alien"))
        {
            suggestions["Core Mechanics"] = new List<string>
            {
                "Spaceship management and upgrades",
                "Resource mining and trading",
                "Alien diplomacy and first contact",
                "Technology research trees",
                "Exploration of unknown systems"
            };
            suggestions["Victory Conditions"] = new List<string>
            {
                "Establish a galactic trade empire",
                "Make peaceful contact with all alien species",
                "Discover the secrets of an ancient civilization"
            };
        }
        else if (gameDescLower.Contains("detective") || gameDescLower.Contains("mystery") || gameDescLower.Contains("investigation"))
        {
            suggestions["Core Mechanics"] = new List<string>
            {
                "Clue gathering and deduction systems",
                "Interview mechanics with NPCs",
                "Evidence analysis and connecting dots",
                "Timeline reconstruction",
                "Red herring management"
            };
            suggestions["Victory Conditions"] = new List<string>
            {
                "Solve the central mystery",
                "Bring the perpetrator to justice",
                "Uncover the truth behind a conspiracy"
            };
        }
        else
        {
            // Generic suggestions
            suggestions["Core Mechanics"] = new List<string>
            {
                "Character progression through experience or achievements",
                "Resource management (health, energy, currency)",
                "Social interactions with NPCs",
                "Exploration and discovery systems",
                "Conflict resolution (combat, negotiation, or puzzles)"
            };
            suggestions["Victory Conditions"] = new List<string>
            {
                "Complete a series of interconnected quests",
                "Achieve mastery in your chosen field",
                "Overcome a central antagonist or challenge"
            };
        }

        var result = "Based on your game description, here are some mechanic suggestions:\n\n";
        
        foreach (var category in suggestions)
        {
            result += $"**{category.Key}:**\n";
            foreach (var suggestion in category.Value)
            {
                result += $"• {suggestion}\n";
            }
            result += "\n";
        }

        result += "**Additional Considerations:**\n";
        result += "• How will players progress and feel a sense of achievement?\n";
        result += "• What choices will create meaningful consequences?\n";
        result += "• How can failure states create interesting story opportunities?\n";
        result += "• What makes your game unique compared to similar themes?\n";

        return result;
    }

    [KernelFunction("suggest_game_schema")]
    [Description("Suggests game state schema based on mechanics")]
    public async Task<string> SuggestGameSchema(
        [Description("List of core game mechanics")] string coreMechanics,
        [Description("Victory condition")] string victoryCondition,
        [Description("Progression system")] string progressionSystem)
    {
        var mechanicsList = coreMechanics.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim().ToLowerInvariant()).ToList();

        var suggestions = new Dictionary<string, List<string>>();

        // Collections based on mechanics
        var collections = new List<string> { "locations", "npcs" }; // Base collections every game needs

        if (mechanicsList.Any(m => m.Contains("combat") || m.Contains("battle") || m.Contains("fight")))
        {
            collections.AddRange(new[] { "enemies", "battleStates", "weapons", "armor" });
        }

        if (mechanicsList.Any(m => m.Contains("quest") || m.Contains("mission") || m.Contains("objective")))
        {
            collections.AddRange(new[] { "quests", "objectives", "questProgress" });
        }

        if (mechanicsList.Any(m => m.Contains("item") || m.Contains("inventory") || m.Contains("equipment")))
        {
            collections.AddRange(new[] { "items", "inventory" });
        }

        if (mechanicsList.Any(m => m.Contains("faction") || m.Contains("reputation") || m.Contains("guild")))
        {
            collections.AddRange(new[] { "factions", "reputations" });
        }

        if (mechanicsList.Any(m => m.Contains("craft") || m.Contains("build") || m.Contains("construct")))
        {
            collections.AddRange(new[] { "recipes", "crafting" });
        }

        if (mechanicsList.Any(m => m.Contains("magic") || m.Contains("spell") || m.Contains("ability")))
        {
            collections.AddRange(new[] { "spells", "abilities", "magicItems" });
        }

        if (mechanicsList.Any(m => m.Contains("trade") || m.Contains("market") || m.Contains("economy")))
        {
            collections.AddRange(new[] { "markets", "tradeRoutes", "economy" });
        }

        suggestions["Required Collections"] = collections.Distinct().ToList();

        // Player fields based on mechanics and progression
        var playerFields = new List<string> { "name", "level", "experience", "currentLocation" };

        if (progressionSystem.ToLowerInvariant().Contains("stat"))
        {
            // Suggest generic attribute system - let ruleset define specific stats
            playerFields.AddRange(new[] { "primaryAttributes", "secondaryAttributes", "derivedStats" });
        }

        if (mechanicsList.Any(m => m.Contains("health") || m.Contains("combat")))
        {
            playerFields.AddRange(new[] { "health", "maxHealth" });
        }

        if (mechanicsList.Any(m => m.Contains("energy") || m.Contains("mana") || m.Contains("stamina")))
        {
            playerFields.AddRange(new[] { "energy", "maxEnergy" });
        }

        if (mechanicsList.Any(m => m.Contains("inventory") || m.Contains("item")))
        {
            playerFields.AddRange(new[] { "inventory", "equippedItems" });
        }

        if (mechanicsList.Any(m => m.Contains("skill") || m.Contains("ability")))
        {
            playerFields.AddRange(new[] { "skills", "abilities", "knownSpells" });
        }

        if (mechanicsList.Any(m => m.Contains("reputation") || m.Contains("faction")))
        {
            playerFields.Add("factionReputations");
        }

        if (mechanicsList.Any(m => m.Contains("currency") || m.Contains("money") || m.Contains("trade")))
        {
            playerFields.Add("currency");
        }

        suggestions["Player Fields"] = playerFields.Distinct().ToList();

        // Dynamic collections with entity types
        var dynamicCollections = new Dictionary<string, string>();
        
        foreach (var collection in suggestions["Required Collections"])
        {
            var entityType = collection switch
            {
                "locations" => "Location",
                "npcs" => "NPC",
                "enemies" => "Enemy",
                "battleStates" => "Battle",
                "weapons" or "armor" or "items" or "magicItems" => "Item",
                "quests" => "Quest",
                "objectives" => "Objective",
                "factions" => "Faction",
                "spells" or "abilities" => "Ability",
                "markets" => "Market",
                "recipes" => "Recipe",
                _ => "Entity"
            };
            dynamicCollections[collection] = entityType;
        }

        var result = "Based on your game mechanics, here's a suggested schema:\n\n";
        
        result += "**Required Collections:**\n";
        foreach (var collection in suggestions["Required Collections"])
        {
            result += $"• {collection}\n";
        }
        result += "\n";

        result += "**Player Fields:**\n";
        foreach (var field in suggestions["Player Fields"])
        {
            result += $"• {field}\n";
        }
        result += "\n";

        result += "**Dynamic Collections (Collection → Entity Type):**\n";
        foreach (var kvp in dynamicCollections)
        {
            result += $"• {kvp.Key} → {kvp.Value}\n";
        }
        result += "\n";

        result += "**Schema Considerations:**\n";
        result += "• Collections store related game objects (NPCs, items, locations)\n";
        result += "• Player fields track character state and progression\n";
        result += "• Entity types define the structure for objects in collections\n";
        result += "• Consider what data your game mechanics need to access frequently\n";
        result += "• Use generic attribute fields (primaryAttributes, etc.) rather than hardcoded stats to support any ruleset\n";

        return result;
    }

    [KernelFunction("validate_ruleset_section")]
    [Description("Validates a specific section of a ruleset and provides feedback")]
    public async Task<string> ValidateRulesetSection(
        [Description("JSON content of the section to validate")] string sectionJson,
        [Description("Name of the section (metadata, gameStateSchema, etc.)")] string sectionName)
    {
        try
        {
            var document = JsonDocument.Parse($"{{ \"{sectionName}\": {sectionJson} }}");
            var validationResult = await _schemaValidator.ValidateSectionAsync(document, sectionName);

            var response = $"Validation results for {sectionName} section:\n\n";

            if (validationResult.IsValid)
            {
                response += "✓ **Validation Passed!** This section meets all requirements.\n\n";
            }
            else
            {
                response += "❌ **Validation Issues Found:**\n\n";
            }

            if (validationResult.HasErrors)
            {
                response += "**Errors (must be fixed):**\n";
                foreach (var error in validationResult.Errors)
                {
                    response += $"• {error}\n";
                }
                response += "\n";
            }

            if (validationResult.HasWarnings)
            {
                response += "**Warnings (recommendations):**\n";
                foreach (var warning in validationResult.Warnings)
                {
                    response += $"• {warning}\n";
                }
                response += "\n";
            }

            // Add requirements for this section
            var requirements = _schemaValidator.GetSectionRequirements(sectionName);
            if (requirements.Any())
            {
                response += "**Section Requirements:**\n";
                foreach (var requirement in requirements)
                {
                    response += $"• {requirement}\n";
                }
            }

            return response;
        }
        catch (JsonException ex)
        {
            return $"❌ **JSON Parsing Error:** {ex.Message}\n\nPlease check that your JSON syntax is correct.";
        }
        catch (Exception ex)
        {
            return $"❌ **Validation Error:** {ex.Message}";
        }
    }

    [KernelFunction("suggest_functions_for_phase")]
    [Description("Suggests appropriate functions for a specific game phase")]
    public async Task<string> SuggestFunctionsForPhase(
        [Description("Game phase name (GameSetup, WorldGeneration, Exploration, Combat, LevelUp)")] string phaseName,
        [Description("List of core game mechanics")] string coreMechanics)
    {
        var mechanicsList = coreMechanics.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim().ToLowerInvariant()).ToList();

        var functions = new List<string>();

        switch (phaseName)
        {
            case "GameSetup":
                functions.AddRange(new[]
                {
                    "create_character - Create a new player character with base attributes",
                    "choose_starting_class - Select character class/archetype (if applicable)",
                    "set_starting_location - Establish where the adventure begins",
                    "initialize_inventory - Give starting equipment and items"
                });

                if (mechanicsList.Any(m => m.Contains("stat") || m.Contains("attribute")))
                {
                    functions.Add("allocate_starting_stats - Distribute character attribute points");
                }

                if (mechanicsList.Any(m => m.Contains("background") || m.Contains("history")))
                {
                    functions.Add("choose_background - Select character background/origin story");
                }
                break;

            case "WorldGeneration":
                functions.AddRange(new[]
                {
                    "create_location - Generate a new location with description and features",
                    "create_npc - Generate non-player characters with personalities",
                    "establish_factions - Create organizations or groups in the world",
                    "generate_local_events - Create ongoing events or situations"
                });

                if (mechanicsList.Any(m => m.Contains("quest") || m.Contains("mission")))
                {
                    functions.Add("create_quest - Generate quests or missions for the player");
                }

                if (mechanicsList.Any(m => m.Contains("economy") || m.Contains("trade")))
                {
                    functions.Add("establish_markets - Create trading posts or economic systems");
                }
                break;

            case "Exploration":
                functions.AddRange(new[]
                {
                    "move_to_location - Handle player movement between locations",
                    "interact_with_npc - Manage conversations and social interactions",
                    "search_area - Allow searching for items, clues, or secrets",
                    "investigate_object - Examine specific items or features",
                    "make_skill_check - Handle skill-based challenges"
                });

                if (mechanicsList.Any(m => m.Contains("quest")))
                {
                    functions.AddRange(new[]
                    {
                        "start_quest - Begin a new quest or mission",
                        "complete_quest_objective - Mark quest progress"
                    });
                }

                if (mechanicsList.Any(m => m.Contains("trade") || m.Contains("shop")))
                {
                    functions.Add("trade_with_merchant - Handle buying and selling");
                }
                break;

            case "Combat":
                if (mechanicsList.Any(m => m.Contains("combat") || m.Contains("battle") || m.Contains("fight")))
                {
                    functions.AddRange(new[]
                    {
                        "start_combat - Initialize a combat encounter",
                        "player_attack - Handle player attack actions",
                        "enemy_turn - Process enemy actions and AI",
                        "use_ability - Activate special abilities or spells",
                        "end_combat - Conclude combat and award rewards"
                    });

                    if (mechanicsList.Any(m => m.Contains("magic") || m.Contains("spell")))
                    {
                        functions.Add("cast_spell - Handle magical spell casting");
                    }

                    if (mechanicsList.Any(m => m.Contains("item") || m.Contains("potion")))
                    {
                        functions.Add("use_item - Use consumable items in combat");
                    }
                }
                else
                {
                    functions.AddRange(new[]
                    {
                        "resolve_conflict - Handle non-combat conflicts",
                        "negotiate - Manage diplomatic resolutions",
                        "escape_danger - Handle retreat or evasion"
                    });
                }
                break;

            case "LevelUp":
                functions.AddRange(new[]
                {
                    "gain_experience - Award experience points",
                    "level_up_character - Increase character level",
                    "improve_stats - Enhance character attributes"
                });

                if (mechanicsList.Any(m => m.Contains("skill")))
                {
                    functions.Add("improve_skills - Enhance character skills or proficiencies");
                }

                if (mechanicsList.Any(m => m.Contains("ability") || m.Contains("power")))
                {
                    functions.Add("unlock_ability - Grant new special abilities");
                }

                if (mechanicsList.Any(m => m.Contains("equipment") || m.Contains("craft")))
                {
                    functions.Add("upgrade_equipment - Improve or craft better gear");
                }
                break;
        }

        var result = $"**Suggested Functions for {phaseName} Phase:**\n\n";
        
        foreach (var function in functions)
        {
            result += $"• {function}\n";
        }

        result += "\n**Function Design Tips:**\n";
        result += "• Each function should have a clear, specific purpose\n";
        result += "• Include proper parameters for the function to work with\n";
        result += "• Add rule validations to ensure game consistency\n";
        result += "• Consider how functions interact with your game state schema\n";
        result += "• Functions should support the core mechanics you've identified\n";

        return result;
    }

    [KernelFunction("suggest_prompt_template")]
    [Description("Suggests a prompt template for a specific game phase")]
    public async Task<string> SuggestPromptTemplate(
        [Description("Game phase name")] string phaseName,
        [Description("Game name")] string gameName,
        [Description("Game description")] string gameDescription,
        [Description("Core mechanics")] string coreMechanics)
    {
        var template = phaseName switch
        {
            "GameSetup" => CreateGameSetupPrompt(gameName, gameDescription, coreMechanics),
            "WorldGeneration" => CreateWorldGenerationPrompt(gameName, gameDescription, coreMechanics),
            "Exploration" => CreateExplorationPrompt(gameName, gameDescription, coreMechanics),
            "Combat" => CreateCombatPrompt(gameName, gameDescription, coreMechanics),
            "LevelUp" => CreateLevelUpPrompt(gameName, gameDescription, coreMechanics),
            _ => "Unknown phase. Please specify GameSetup, WorldGeneration, Exploration, Combat, or LevelUp."
        };

        return $"**Suggested Prompt Template for {phaseName}:**\n\n```\n{template}\n```\n\n" +
               "**Customization Tips:**\n" +
               "• Adjust the tone to match your game's atmosphere\n" +
               "• Add specific placeholders for data your game tracks\n" +
               "• Include guidance for handling your unique mechanics\n" +
               "• Consider what information the AI needs to make good decisions\n";
    }

    private List<string> AnalyzeContentForTags(string name, string description)
    {
        var suggestions = new List<string>();
        var content = $"{name} {description}".ToLowerInvariant();

        var tagMapping = new Dictionary<string, string[]>
        {
            ["fantasy"] = new[] { "magic", "medieval", "dragon", "wizard", "kingdom", "quest", "adventure", "dungeon" },
            ["sci-fi"] = new[] { "space", "alien", "robot", "technology", "future", "cyberpunk", "android", "laser" },
            ["horror"] = new[] { "monster", "ghost", "fear", "dark", "supernatural", "demon", "zombie", "vampire" },
            ["mystery"] = new[] { "detective", "crime", "investigation", "clue", "murder", "puzzle", "solve" },
            ["combat"] = new[] { "battle", "fight", "war", "weapon", "army", "combat", "conflict", "duel" },
            ["exploration"] = new[] { "explore", "discover", "travel", "journey", "adventure", "world", "map" },
            ["social"] = new[] { "diplomacy", "negotiate", "conversation", "relationship", "politics", "intrigue" },
            ["survival"] = new[] { "survive", "resource", "craft", "build", "wilderness", "danger", "food", "shelter" },
            ["stealth"] = new[] { "sneak", "hide", "assassin", "spy", "infiltrate", "shadow", "thief" }
        };

        foreach (var kvp in tagMapping)
        {
            if (kvp.Value.Any(keyword => content.Contains(keyword)))
            {
                suggestions.Add(kvp.Key);
            }
        }

        return suggestions.Take(3).ToList(); // Limit to top 3 suggestions
    }

    private string CreateGameSetupPrompt(string gameName, string gameDescription, string coreMechanics)
    {
        return $@"You are the Game Master for {gameName}, {gameDescription}.

Your role is to guide the player through character creation and initial game setup. Be welcoming, engaging, and help them understand the game world and their character's place in it.

Core game mechanics: {coreMechanics}

Current character info: {{character_info}}
Available options: {{available_options}}
Game state: {{game_state}}

Guidelines:
- Explain character creation choices clearly
- Help players understand how their decisions affect gameplay
- Establish the game world's tone and atmosphere
- Ask engaging questions to involve the player in the setup process
- Ensure all required character information is collected";
    }

    private string CreateWorldGenerationPrompt(string gameName, string gameDescription, string coreMechanics)
    {
        return $@"You are the Game Master for {gameName}, {gameDescription}.

Your role is to create a vivid, immersive world filled with interesting locations, memorable NPCs, and engaging situations. Build a setting that supports the game's core mechanics and provides rich opportunities for adventure.

Core game mechanics: {coreMechanics}

Current location: {{current_location}}
World state: {{world_state}}
Game state: {{game_state}}

Guidelines:
- Create diverse, detailed locations with clear purposes
- Design NPCs with distinct personalities and motivations
- Establish connections between locations and characters
- Incorporate elements that support the core game mechanics
- Build a world that feels alive and reactive to player actions";
    }

    private string CreateExplorationPrompt(string gameName, string gameDescription, string coreMechanics)
    {
        return $@"You are the Game Master for {gameName}, {gameDescription}.

Your role is to narrate the player's adventures with engaging descriptions and meaningful choices. Respond to their actions, advance the story, and create memorable moments that highlight the game's unique mechanics.

Core game mechanics: {coreMechanics}

Player action: {{player_input}}
Current location: {{current_location}}
Character info: {{character_info}}
Available NPCs: {{nearby_npcs}}
Game state: {{game_state}}

Guidelines:
- Provide vivid, immersive descriptions of the environment
- Offer meaningful choices that have clear consequences
- React dynamically to player decisions and creativity
- Incorporate the core game mechanics naturally into the narrative
- Maintain consistent world logic and character behavior
- Create opportunities for the player to use their character's unique abilities";
    }

    private string CreateCombatPrompt(string gameName, string gameDescription, string coreMechanics)
    {
        return $@"You are the Game Master for {gameName}, {gameDescription}.

Your role is to manage combat encounters with exciting descriptions and tactical decisions. Handle combat mechanics according to the game rules while maintaining narrative excitement and player agency.

Core game mechanics: {coreMechanics}

Combat state: {{combat_state}}
Character info: {{character_info}}
Enemy info: {{enemy_info}}
Available actions: {{available_actions}}
Game state: {{game_state}}

Guidelines:
- Describe combat actions with vivid, exciting detail
- Apply combat mechanics consistently and fairly
- Provide clear information about tactical options
- Balance challenge with player capability
- Maintain tension while ensuring fair outcomes
- Incorporate environmental factors and tactical positioning";
    }

    private string CreateLevelUpPrompt(string gameName, string gameDescription, string coreMechanics)
    {
        return $@"You are the Game Master for {gameName}, {gameDescription}.

Your role is to guide character progression and celebrate the player's achievements. Handle level-ups, skill improvements, and new abilities in a way that feels rewarding and maintains game balance.

Core game mechanics: {coreMechanics}

Character info: {{character_info}}
Available upgrades: {{available_upgrades}}
Experience gained: {{experience_info}}
Achievement unlocked: {{achievements}}
Game state: {{game_state}}

Guidelines:
- Celebrate the player's achievements and progress
- Explain new abilities and improvements clearly
- Help the player understand their advancement options
- Maintain game balance while providing meaningful growth
- Connect character progression to the ongoing narrative
- Suggest how new abilities might be useful in future challenges";
    }
}