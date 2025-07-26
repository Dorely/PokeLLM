using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using PokeLLM.GameState.Interfaces;
using PokeLLM.GameState.Models;

namespace PokeLLM.Game.Plugins;

/// <summary>
/// Plugin for handling character creation and level up mechanics using D&D 5e-style ability scores
/// </summary>
public class CharacterCreationPlugin
{
    private readonly IGameStateRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // D&D 5e Standard Array for ability score allocation
    private static readonly int[] StandardArray = { 15, 14, 13, 12, 10, 8 };

    public CharacterCreationPlugin(IGameStateRepository repository)
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

    [KernelFunction("allocate_ability_scores")]
    [Description("Allocates the D&D Standard Array (15, 14, 13, 12, 10, 8) to the player's six ability scores.")]
    public async Task<string> AllocateAbilityScores(
        [Description("Score to assign to Strength (choose from: 15, 14, 13, 12, 10, 8)")]
        int strength,
        [Description("Score to assign to Dexterity (choose from: 15, 14, 13, 12, 10, 8)")]
        int dexterity,
        [Description("Score to assign to Constitution (choose from: 15, 14, 13, 12, 10, 8)")]
        int constitution,
        [Description("Score to assign to Intelligence (choose from: 15, 14, 13, 12, 10, 8)")]
        int intelligence,
        [Description("Score to assign to Wisdom (choose from: 15, 14, 13, 12, 10, 8)")]
        int wisdom,
        [Description("Score to assign to Charisma (choose from: 15, 14, 13, 12, 10, 8)")]
        int charisma)
    {
        try
        {
            Debug.WriteLine($"[CharacterCreationPlugin] AllocateAbilityScores called");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Validate that all scores are from the standard array and each is used exactly once
            var assignedScores = new[] { strength, dexterity, constitution, intelligence, wisdom, charisma };
            var standardArrayCopy = StandardArray.ToList();

            foreach (var score in assignedScores)
            {
                if (!standardArrayCopy.Contains(score))
                {
                    return JsonSerializer.Serialize(new { 
                        error = $"Invalid score allocation. Each score must be from the Standard Array: {string.Join(", ", StandardArray)}" 
                    }, _jsonOptions);
                }
                standardArrayCopy.Remove(score);
            }

            if (standardArrayCopy.Count > 0)
            {
                return JsonSerializer.Serialize(new { 
                    error = $"All scores from the Standard Array must be used exactly once: {string.Join(", ", StandardArray)}" 
                }, _jsonOptions);
            }

            // Apply the ability scores
            gameState.Player.Character.Stats.Strength = strength;
            gameState.Player.Character.Stats.Dexterity = dexterity;
            gameState.Player.Character.Stats.Constitution = constitution;
            gameState.Player.Character.Stats.Intelligence = intelligence;
            gameState.Player.Character.Stats.Wisdom = wisdom;
            gameState.Player.Character.Stats.Charisma = charisma;

            // Mark character creation as complete
            gameState.Player.CharacterCreationComplete = true;
            
            // Reset available stat points since we just allocated the initial scores
            gameState.Player.AvailableStatPoints = 0;

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Ability scores allocated successfully!",
                stats = new
                {
                    strength = strength,
                    dexterity = dexterity,
                    constitution = constitution,
                    intelligence = intelligence,
                    wisdom = wisdom,
                    charisma = charisma
                },
                modifiers = new
                {
                    strength = CalculateAbilityModifier(strength),
                    dexterity = CalculateAbilityModifier(dexterity),
                    constitution = CalculateAbilityModifier(constitution),
                    intelligence = CalculateAbilityModifier(intelligence),
                    wisdom = CalculateAbilityModifier(wisdom),
                    charisma = CalculateAbilityModifier(charisma)
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPlugin] Error in AllocateAbilityScores: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to allocate ability scores: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("set_trainer_class")]
    [Description("Sets the player's trainer class, which affects their progression and abilities.")]
    public async Task<string> SetTrainerClass(
        [Description("The trainer class to assign (e.g., 'Researcher', 'Athlete', 'Coordinator', 'Ranger', 'Breeder')")]
        string className)
    {
        try
        {
            Debug.WriteLine($"[CharacterCreationPlugin] SetTrainerClass called: {className}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Validate the class name (you could expand this with a predefined list)
            if (string.IsNullOrWhiteSpace(className))
            {
                return JsonSerializer.Serialize(new { error = "Class name cannot be empty" }, _jsonOptions);
            }

            gameState.Player.Class = className;
            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Trainer class set to {className}",
                className = className
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPlugin] Error in SetTrainerClass: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to set trainer class: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("create_starter_pokemon")]
    [Description("Creates a starter Pokemon with base stats from species data and grants it two starting moves.")]
    public async Task<string> CreateStarterPokemon(
        [Description("The species name of the starter Pokemon (e.g., 'Bulbasaur', 'Charmander', 'Squirtle')")]
        string species,
        [Description("Optional nickname for the Pokemon")]
        string nickname = "")
    {
        try
        {
            Debug.WriteLine($"[CharacterCreationPlugin] CreateStarterPokemon called: {species}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            // Get species data (for now, we'll use default values - this would normally come from a database)
            var speciesData = GetDefaultSpeciesData(species);
            
            // Generate unique ID for the Pokemon
            var pokemonId = $"pkmn_inst_{species.ToLower()}_001";
            int counter = 1;
            while (gameState.WorldPokemon.ContainsKey(pokemonId) || 
                   gameState.Player.TeamPokemon.Any(p => p.Pokemon.Id == pokemonId) ||
                   gameState.Player.BoxedPokemon.Any(p => p.Pokemon.Id == pokemonId))
            {
                counter++;
                pokemonId = $"pkmn_inst_{species.ToLower()}_{counter:D3}";
            }

            // Create the Pokemon
            var pokemon = new Pokemon
            {
                Id = pokemonId,
                Species = species,
                NickName = string.IsNullOrWhiteSpace(nickname) ? species : nickname,
                Level = 5, // Starter level
                Stats = new Stats
                {
                    Strength = speciesData.BaseAbilityScores.Strength,
                    Dexterity = speciesData.BaseAbilityScores.Dexterity,
                    Constitution = speciesData.BaseAbilityScores.Constitution,
                    Intelligence = speciesData.BaseAbilityScores.Intelligence,
                    Wisdom = speciesData.BaseAbilityScores.Wisdom,
                    Charisma = speciesData.BaseAbilityScores.Charisma
                },
                Type1 = speciesData.Type1,
                Type2 = speciesData.Type2,
                MaxVigor = speciesData.BaseVigor + (5 * 2), // Base + (level * 2)
                CurrentVigor = speciesData.BaseVigor + (5 * 2),
                KnownMoves = GetStartingMoves(speciesData, 2) // Grant 2 starting moves
            };

            // Create owned Pokemon wrapper
            var ownedPokemon = new OwnedPokemon
            {
                Pokemon = pokemon,
                Experience = 0,
                AvailableStatPoints = 0,
                CaughtLocationId = "starter_selection",
                Friendship = 70 // Starters start with higher friendship
            };

            // Add to player's team
            gameState.Player.TeamPokemon.Add(ownedPokemon);

            // Update adventure summary
            gameState.AdventureSummary += $" {gameState.Player.Character.Name} chose {species} as their starter Pokemon.";
            gameState.RecentEvents.Add($"Player chose {species} as their starter Pokemon");

            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"{species} has been added to your team!",
                pokemon = new
                {
                    id = pokemon.Id,
                    species = pokemon.Species,
                    nickname = pokemon.NickName,
                    level = pokemon.Level,
                    type1 = pokemon.Type1.ToString(),
                    type2 = pokemon.Type2?.ToString(),
                    vigor = $"{pokemon.CurrentVigor}/{pokemon.MaxVigor}",
                    stats = pokemon.Stats,
                    moves = pokemon.KnownMoves.Select(m => m.Name).ToList()
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPlugin] Error in CreateStarterPokemon: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to create starter Pokemon: {ex.Message}" }, _jsonOptions);
        }
    }

    [KernelFunction("level_up_ability_score")]
    [Description("Increases an ability score by 1 using available stat points. Characters get stat points when they level up.")]
    public async Task<string> LevelUpAbilityScore(
        [Description("The ability score to increase: Strength, Dexterity, Constitution, Intelligence, Wisdom, or Charisma")]
        string abilityName)
    {
        try
        {
            Debug.WriteLine($"[CharacterCreationPlugin] LevelUpAbilityScore called: {abilityName}");
            var gameState = await _repository.LoadLatestStateAsync();
            
            if (gameState == null)
                return JsonSerializer.Serialize(new { error = "No game state found" }, _jsonOptions);

            if (gameState.Player.AvailableStatPoints <= 0)
            {
                return JsonSerializer.Serialize(new { error = "No available stat points to spend" }, _jsonOptions);
            }

            var stats = gameState.Player.Character.Stats;
            var currentScore = abilityName.ToLower() switch
            {
                "strength" => stats.Strength,
                "dexterity" => stats.Dexterity,
                "constitution" => stats.Constitution,
                "intelligence" => stats.Intelligence,
                "wisdom" => stats.Wisdom,
                "charisma" => stats.Charisma,
                _ => 0
            };

            if (currentScore == 0)
            {
                return JsonSerializer.Serialize(new { error = "Invalid ability name. Use: Strength, Dexterity, Constitution, Intelligence, Wisdom, or Charisma" }, _jsonOptions);
            }

            if (currentScore >= 20)
            {
                return JsonSerializer.Serialize(new { error = $"{abilityName} is already at maximum (20)" }, _jsonOptions);
            }

            // Increase the ability score
            switch (abilityName.ToLower())
            {
                case "strength": stats.Strength++; break;
                case "dexterity": stats.Dexterity++; break;
                case "constitution": stats.Constitution++; break;
                case "intelligence": stats.Intelligence++; break;
                case "wisdom": stats.Wisdom++; break;
                case "charisma": stats.Charisma++; break;
            }

            gameState.Player.AvailableStatPoints--;
            gameState.LastSaveTime = DateTime.UtcNow;
            await _repository.SaveStateAsync(gameState);

            var newScore = currentScore + 1;
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"{abilityName} increased from {currentScore} to {newScore}",
                abilityName = abilityName,
                oldScore = currentScore,
                newScore = newScore,
                newModifier = CalculateAbilityModifier(newScore),
                remainingStatPoints = gameState.Player.AvailableStatPoints
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterCreationPlugin] Error in LevelUpAbilityScore: {ex.Message}");
            return JsonSerializer.Serialize(new { error = $"Failed to level up ability score: {ex.Message}" }, _jsonOptions);
        }
    }

    /// <summary>
    /// Calculates the D&D 5e ability modifier from an ability score
    /// </summary>
    private int CalculateAbilityModifier(int abilityScore)
    {
        return (int)Math.Floor((abilityScore - 10) / 2.0);
    }

    /// <summary>
    /// Gets default species data for common starter Pokemon
    /// In a full implementation, this would come from a database or vector store
    /// </summary>
    private PokemonSpeciesData GetDefaultSpeciesData(string species)
    {
//TODO update logic elsewhere to populate and retrieve these from the vector store
        return species.ToLower() switch
        {
            "bulbasaur" => new PokemonSpeciesData
            {
                SpeciesName = "Bulbasaur",
                BaseAbilityScores = new Stats { Strength = 10, Dexterity = 10, Constitution = 11, Intelligence = 13, Wisdom = 12, Charisma = 10 },
                Type1 = PokemonType.Grass,
                Type2 = PokemonType.Poison,
                BaseVigor = 12,
                LearnableMoves = GetDefaultMoves("Bulbasaur")
            },
            "charmander" => new PokemonSpeciesData
            {
                SpeciesName = "Charmander",
                BaseAbilityScores = new Stats { Strength = 12, Dexterity = 13, Constitution = 10, Intelligence = 11, Wisdom = 10, Charisma = 12 },
                Type1 = PokemonType.Fire,
                Type2 = null,
                BaseVigor = 10,
                LearnableMoves = GetDefaultMoves("Charmander")
            },
            "squirtle" => new PokemonSpeciesData
            {
                SpeciesName = "Squirtle",
                BaseAbilityScores = new Stats { Strength = 10, Dexterity = 11, Constitution = 13, Intelligence = 12, Wisdom = 12, Charisma = 10 },
                Type1 = PokemonType.Water,
                Type2 = null,
                BaseVigor = 14,
                LearnableMoves = GetDefaultMoves("Squirtle")
            },
            "pikachu" => new PokemonSpeciesData
            {
                SpeciesName = "Pikachu",
                BaseAbilityScores = new Stats { Strength = 8, Dexterity = 15, Constitution = 9, Intelligence = 12, Wisdom = 11, Charisma = 14 },
                Type1 = PokemonType.Electric,
                Type2 = null,
                BaseVigor = 8,
                LearnableMoves = GetDefaultMoves("Pikachu")
            },
            _ => new PokemonSpeciesData
            {
                SpeciesName = species,
                BaseAbilityScores = new Stats { Strength = 10, Dexterity = 10, Constitution = 10, Intelligence = 10, Wisdom = 10, Charisma = 10 },
                Type1 = PokemonType.Normal,
                Type2 = null,
                BaseVigor = 10,
                LearnableMoves = GetDefaultMoves("Normal")
            }
        };
    }

    /// <summary>
    /// Gets default starting moves for a Pokemon species
    /// In a full implementation, this would come from a database
    /// </summary>
    private List<Move> GetDefaultMoves(string species)
    {
        //TODO update logic elsewhere to populate and retrieve these from the vector store
        return species.ToLower() switch
        {
            "bulbasaur" => new List<Move>
            {
                new Move { Id = "move_tackle", Name = "Tackle", Category = MoveCategory.Physical, DamageDice = "1d6", Type = PokemonType.Normal, VigorCost = 1, Description = "A simple ramming attack." },
                new Move { Id = "move_vine_whip", Name = "Vine Whip", Category = MoveCategory.Physical, DamageDice = "1d8", Type = PokemonType.Grass, VigorCost = 2, Description = "Strikes with vines." }
            },
            "charmander" => new List<Move>
            {
                new Move { Id = "move_scratch", Name = "Scratch", Category = MoveCategory.Physical, DamageDice = "1d6", Type = PokemonType.Normal, VigorCost = 1, Description = "Scratches with claws." },
                new Move { Id = "move_ember", Name = "Ember", Category = MoveCategory.Special, DamageDice = "1d8", Type = PokemonType.Fire, VigorCost = 2, Description = "Breathes small flames." }
            },
            "squirtle" => new List<Move>
            {
                new Move { Id = "move_tackle", Name = "Tackle", Category = MoveCategory.Physical, DamageDice = "1d6", Type = PokemonType.Normal, VigorCost = 1, Description = "A simple ramming attack." },
                new Move { Id = "move_water_gun", Name = "Water Gun", Category = MoveCategory.Special, DamageDice = "1d8", Type = PokemonType.Water, VigorCost = 2, Description = "Squirts water forcefully." }
            },
            "pikachu" => new List<Move>
            {
                new Move { Id = "move_quick_attack", Name = "Quick Attack", Category = MoveCategory.Physical, DamageDice = "1d6", Type = PokemonType.Normal, VigorCost = 1, Description = "A fast attack that always goes first." },
                new Move { Id = "move_thunder_shock", Name = "Thunder Shock", Category = MoveCategory.Special, DamageDice = "1d8", Type = PokemonType.Electric, VigorCost = 2, Description = "An electric shock attack." }
            },
            _ => new List<Move>
            {
                new Move { Id = "move_tackle", Name = "Tackle", Category = MoveCategory.Physical, DamageDice = "1d6", Type = PokemonType.Normal, VigorCost = 1, Description = "A simple ramming attack." },
                new Move { Id = "move_growl", Name = "Growl", Category = MoveCategory.Status, DamageDice = "", Type = PokemonType.Normal, VigorCost = 1, Description = "Reduces the target's attack." }
            }
        };
    }

    /// <summary>
    /// Gets the specified number of starting moves from a species' learnable moves
    /// </summary>
    private List<Move> GetStartingMoves(PokemonSpeciesData speciesData, int count)
    {
        return speciesData.LearnableMoves.Take(count).ToList();
    }
}
