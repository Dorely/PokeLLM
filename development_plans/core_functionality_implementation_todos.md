# Core Functionality Implementation TODOs

## Overview

Based on Gemini's function specifications, this document provides a comprehensive TODO list for implementing the required core TTRPG functionality in the main Phase plugins. The Pokemon-specific extensions should remain in the rulesets, while universal functions belong in the plugins.

## Legend
- ✅ **EXISTS** - Function already implemented and functional
- 🔄 **MODIFY** - Function exists but needs changes to match specification
- ❌ **MISSING** - Function needs to be created
- 🧪 **TEST** - Testing requirements for function

---

## GameSetup Phase Plugin

### Current Status
- **File**: `PokeLLM/Plugins/GameSetupPhasePlugin.cs`
- **Functions**: 5 implemented

### Required Functions from Gemini Spec

#### ❌ NEW Adventure Setting Function
1. **set_adventure_setting** - ❌ MISSING
   - **Purpose**: Store player's choice of setting for WorldGeneration phase
   - **Implementation**: Simple write to `gamestate.adventure_setting`
   - **Location**: GameSetupPhasePlugin
   - **Details**: Used as primary input for WorldGeneration phase
   - 🧪 **TEST**: Given empty gamestate, setting details must be stored correctly

#### 🔄 Character Creation Functions  
2. **create_player_character** - 🔄 MODIFY
   - **Current**: Separate functions for name, stats
   - **Needed**: Unified function with Vector DB integration
   - **Purpose**: Create official player character AND corresponding lore entry
   - **Implementation**: 
     ```csharp
     [KernelFunction("create_player_character")]
     public async Task<string> CreatePlayerCharacter(
         string name, 
         string className, 
         string backstory, 
         Dictionary<string, int> stats)
     ```
   - **Location**: **RULESET** (touches className which is ruleset-specific)
   - **Details**: Must write to `gamestate.player_character` AND create Vector DB entry for backstory
   - 🧪 **TEST**: Character creation + verify backstory retrievable from Vector DB

3. **get_character_stats_schema** - ❌ MISSING
   - **Purpose**: Retrieve list of required character statistics from loaded ruleset
   - **Implementation**: Call into ruleset to fetch stat name array (e.g., ["Strength", "Dexterity", ...])
   - **Location**: **RULESET** (reads ruleset-specific stat definitions)
   - **Details**: Must return stats defined by current ruleset
   - 🧪 **TEST**: With Pokemon D&D ruleset, must return six D&D-style stat names

4. **roll_stats** - 🔄 MODIFY  
   - **Current**: `generate_random_stats`, `generate_standard_stats`
   - **Needed**: Unified function with method parameter
   - **Purpose**: Utility to simulate rolling dice for character ability scores
   - **Implementation**:
     ```csharp
     [KernelFunction("roll_stats")]
     public async Task<string> RollStats(string method = "4d6_drop_lowest")
     ```
   - **Location**: **CORE PLUGIN** (generic dice rolling, no ruleset content)
   - **Details**: Method parameter determines rolling logic
   - 🧪 **TEST**: With "4d6_drop_lowest", must return 6 integers between 3-18

#### ❌ New Class System Functions

5. **define_new_class** - ❌ MISSING
   - **Purpose**: Dynamically add custom character class to ruleset for current session
   - **Implementation**: Modify in-memory and local copy of ruleset representation, add to `content.classes` array
   - **Location**: **RULESET** (directly modifies ruleset class definitions)
   - **Details**: Does NOT modify base ruleset file on disk, but should be saved to the copy of the file next to the save data
   - 🧪 **TEST**: After creation, `get_available_classes()` must include new class

---

## WorldGeneration Phase Plugin

### Current Status
- **File**: `PokeLLM/Plugins/WorldGenerationPhasePlugin.cs`
- **Functions**: 2 implemented (very basic)

### Required Functions from Gemini Spec

#### ❌ Quest & Faction Functions
6. **create_quest** - ❌ MISSING  
   - **Purpose**: Create primary entities for game world
   - **Implementation**: Dual-write - structured object in gamestate + descriptive text in Vector DB
   - **Location**: **CORE PLUGIN** (generic quest structure, no ruleset content)
   - **Details**: Must return unique ID of created entity
   - 🧪 **TEST**: Entity in gamestate + description retrievable via `search_lore`

7. **create_faction** - ❌ MISSING
   - **Purpose**: Create primary entities for game world  
   - **Implementation**: Dual-write - structured object in gamestate + ideology text in Vector DB
   - **Location**: **CORE PLUGIN** (generic faction structure, no ruleset content)
   - **Details**: Must return unique ID of created entity
   - 🧪 **TEST**: Entity in gamestate + ideology retrievable via `search_lore`

#### ❌ NPC Management Functions

8. **create_npc** - ❌ MISSING
   - **Purpose**: Create primary entities for game world
   - **Implementation**: Dual-write - structured object in gamestate + backstory in Vector DB
   - **Location**: **RULESET** (touches className parameter, ruleset-specific)
   - **Details**: Must return unique ID of created entity
   - 🧪 **TEST**: NPC in gamestate + backstory retrievable via `search_lore`

#### ❌ World Building Functions

9. **create_location** - ❌ MISSING
   - **Purpose**: Create primary entities for game world
   - **Implementation**: Dual-write - structured object in gamestate + description in Vector DB
   - **Location**: **CORE PLUGIN** (generic location structure, no ruleset content)
   - **Details**: Must return unique ID of created entity  
   - 🧪 **TEST**: Location in gamestate + description retrievable via `search_lore`

10. **link_locations** - ❌ MISSING
    - **Purpose**: Create traversable connection between location nodes
    - **Implementation**: Two-way update - add exits to both locations
    - **Location**: **CORE PLUGIN** (generic location linking, no ruleset content)
    - **Details**: A→B "north" creates B→A "south" automatically
    - 🧪 **TEST**: Both locations must have reciprocal exits after linking

11. **place_npc_in_location** - ❌ MISSING
    - **Purpose**: Associate NPCs with specific locations
    - **Implementation**: Update location's NPC list and NPC's location reference
    - **Location**: **CORE PLUGIN** (generic NPC-location association, no ruleset content)
    - 🧪 **TEST**: NPC appears in location's NPC list, location shows in NPC data

---

## Exploration Phase Plugin

### Current Status
- **File**: `PokeLLM/Plugins/ExplorationPhasePlugin.cs`
- **Functions**: 6 implemented

### Required Functions from Gemini Spec

#### ✅ Dice & Skill System
13. **roll_dice** - ✅ EXISTS (as part of `manage_dice_and_checks`)
    - Current implementation supports various dice types
    - 🧪 **TEST**: All dice notation (1d20, 3d6+2, etc.)

14. **make_skill_check** - ✅ EXISTS (as part of `manage_dice_and_checks`)
    - Current implementation supports stat-based checks
    - 🧪 **TEST**: All stats, difficulty classes, advantage/disadvantage

#### ❌ World Information Functions

15. **get_location_details** - ❌ MISSING
    - **Purpose**: Retrieve all structured data about a specific location for LLM
    - **Implementation**: Read-only function that fetches location object from gamestate
    - **Location**: **CORE PLUGIN** (generic location data retrieval, no ruleset content)
    - **Details**: Must return complete location data including exits, NPCs, features
    - 🧪 **TEST**: Given location with exit to "Viridian City", must return correct exit data

16. **search_lore** - ❌ MISSING
    - **Purpose**: Perform semantic search on Vector DB for relevant lore/descriptions
    - **Implementation**: Direct interface to Vector DB's query engine
    - **Location**: **CORE PLUGIN** (generic Vector DB search, no ruleset content)
    - **Details**: Must integrate with existing VectorPlugin
    - 🧪 **TEST**: Query "who is the Pokémon professor" must return Professor Oak backstory

#### ❌ Movement & Navigation

17. **update_player_location** - ❌ MISSING
    - **Purpose**: Handle player movement between locations with validation
    - **Implementation**: Location transition with exit validation
    - **Location**: **CORE PLUGIN** (generic movement system, no ruleset content)
    - **Details**: Must validate exits exist before allowing movement
    - 🧪 **TEST**: Movement validation, invalid moves rejected, valid moves succeed

#### ❌ Environment & Time

18. **advance_time** - ❌ MISSING
    - **Purpose**: Move the in-game clock forward
    - **Implementation**: Update `gamestate.world_state.time` and `day` variables
    - **Location**: **CORE PLUGIN** (generic time system, no ruleset content)
    - **Details**: Must parse duration strings like "1 hour", "to next morning"
    - 🧪 **TEST**: "Day 1, 09:00" + "6 hours" = "Day 1, 15:00"

---

## Combat Phase Plugin

### Current Status
- **File**: `PokeLLM/Plugins/CombatPhasePlugin.cs`
- **Functions**: 2 implemented (minimal)

### Required Functions from Gemini Spec

#### ❌ Combat Initialization

19. **start_combat** - ❌ MISSING
    - **Purpose**: Initialize the combat state
    - **Implementation**: Create `combat_state` object, set phase to "Combat", populate combatants
    - **Location**: **CORE PLUGIN** (generic combat initialization, no ruleset content)
    - **Details**: Does NOT roll initiative, only sets up combat structure
    - 🧪 **TEST**: Phase set to "Combat", combatants list populated with provided IDs

20. **roll_for_initiative** - ❌ MISSING
    - **Purpose**: Determine turn order for all combatants
    - **Implementation**: Initiative rolling with stat bonuses
    - **Location**: **RULESET** (uses stat modifiers, ruleset-specific calculations)
    - 🧪 **TEST**: Initiative calculation, turn order generation

21. **get_turn_order** - ❌ MISSING
    - **Purpose**: Return current combat turn sequence
    - **Implementation**: Sorted participant list by initiative
    - **Location**: **CORE PLUGIN** (generic turn order display, no ruleset content)
    - 🧪 **TEST**: Turn order accuracy, tie handling

#### ❌ Combat Actions

22. **make_attack_roll** - ❌ MISSING
    - **Purpose**: Core D&D dice resolution for attacks
    - **Implementation**: 1d20 + stat_modifier vs target_ac, handle critical hits
    - **Location**: **CORE PLUGIN** (generic dice mechanics, no ruleset content)
    - **Details**: Must identify natural 20 as critical hit
    - 🧪 **TEST**: +7 modifier vs AC 18, hit on 11+, crit on natural 20

23. **make_saving_throw** - ❌ MISSING
    - **Purpose**: Core D&D dice resolution for saves
    - **Implementation**: 1d20 + relevant stat modifier vs difficulty_class
    - **Location**: **CORE PLUGIN** (generic dice mechanics, no ruleset content)
    - **Details**: Compare total roll against DC for success/failure
    - 🧪 **TEST**: All save types, DC comparisons, modifier applications

24. **apply_damage** - ❌ MISSING
    - **Purpose**: Modify character state as result of damage
    - **Implementation**: Direct mutation of target's `current_hp` in gamestate
    - **Location**: **CORE PLUGIN** (generic damage application, no ruleset content)
    - **Details**: Must handle different damage types
    - 🧪 **TEST**: Character with 50 HP takes 15 damage = 35 HP remaining

25. **apply_condition** - ❌ MISSING
    - **Purpose**: Modify character state with status effects
    - **Implementation**: Add to target's `active_conditions` array in gamestate
    - **Location**: **CORE PLUGIN** (generic condition tracking, no ruleset content)
    - **Details**: Must track condition name and duration
    - 🧪 **TEST**: Condition appears in character's active_conditions with duration

---

## LevelUp Phase Plugin

### Current Status
- **File**: `PokeLLM/Plugins/LevelUpPhasePlugin.cs`
- **Functions**: Unknown (need to analyze)

### Required Functions from Gemini Spec

#### ❌ Experience & Progression

26. **award_experience** - ❌ MISSING
    - **Purpose**: Grant experience points to characters (core XP system)
    - **Implementation**: XP distribution with level threshold checking
    - **Location**: **CORE PLUGIN** (generic XP system, no ruleset content)
    - **Details**: Universal function for any TTRPG system
    - 🧪 **TEST**: XP calculation, level thresholds, party distribution

27. **get_player_levelup_benefits** - ❌ MISSING
    - **Purpose**: Display available level-up options from ruleset
    - **Implementation**: Class-based progression options retrieved from active ruleset
    - **Location**: **RULESET** (reads class-specific advancement, ruleset content)
    - **Details**: Must query ruleset for class-specific advancement options
    - 🧪 **TEST**: Class progression matches ruleset, prerequisites enforced

28. **apply_player_levelup** - ❌ MISSING
    - **Purpose**: Apply chosen level-up improvements to character
    - **Implementation**: Stat increases, ability unlocks, feature grants based on choices
    - **Location**: **RULESET** (applies class-specific features, ruleset content)
    - **Details**: Must integrate with ruleset for valid advancement options
    - 🧪 **TEST**: Stat changes applied correctly, abilities granted match choices

#### ❌ Inventory & Equipment (Cross-Phase Functions)

29. **get_shop_inventory** - ❌ MISSING
    - **Purpose**: Display available items for purchase at location
    - **Implementation**: Location-based shop inventory retrieval
    - **Location**: **RULESET** (item definitions and availability rules, ruleset content)
    - **Details**: Must support location-specific item availability
    - 🧪 **TEST**: Shop inventory matches location, item availability correct

30. **update_player_money** - ❌ MISSING
    - **Purpose**: Modify player currency amounts
    - **Implementation**: Currency tracking with transaction validation
    - **Location**: **CORE PLUGIN** (generic currency system, no ruleset content)
    - **Details**: Must prevent negative currency, handle overflow
    - 🧪 **TEST**: Currency calculations, transaction validation, bounds checking

31. **buy_item** - ❌ MISSING
    - **Purpose**: Execute item purchase transactions
    - **Implementation**: Cost validation, inventory addition, money deduction
    - **Location**: **RULESET** (item costs and properties, ruleset content)
    - **Details**: Must validate funds, inventory space, item availability
    - 🧪 **TEST**: Purchase validation, inventory limits, cost deduction

32. **equip_item** - ❌ MISSING
    - **Purpose**: Equip items to character equipment slots
    - **Implementation**: Equipment slot management with stat bonus application
    - **Location**: **RULESET** (item stat bonuses and restrictions, ruleset content)
    - **Details**: Must handle slot restrictions, unequip previous items
    - 🧪 **TEST**: Slot restrictions enforced, stat bonuses applied correctly

33. **get_character_bonuses** - ❌ MISSING
    - **Purpose**: Calculate total character bonuses from all sources
    - **Implementation**: Bonus aggregation from equipment, abilities, conditions
    - **Location**: **RULESET** (bonus calculation rules, ruleset content)
    - **Details**: Must aggregate bonuses without double-counting
    - 🧪 **TEST**: Bonus stacking rules, source tracking, calculation accuracy

#### ❌ Relationships & Reputation (Cross-Phase Functions)

34. **update_bond_score** - ❌ MISSING
    - **Purpose**: Modify relationships between trainer and Pokemon
    - **Implementation**: Relationship value tracking with threshold effects
    - **Location**: **CORE PLUGIN** (generic relationship system, no ruleset content)
    - **Details**: Core relationship mechanic for any character-pet system
    - 🧪 **TEST**: Bond value changes, threshold triggers, relationship progression

35. **get_faction_reputation** - ❌ MISSING
    - **Purpose**: Retrieve current standing with factions
    - **Implementation**: Reputation value retrieval with status descriptions
    - **Location**: **CORE PLUGIN** (generic reputation system, no ruleset content)
    - **Details**: Must return numeric value and descriptive status
    - 🧪 **TEST**: Reputation accuracy, status descriptions match values

36. **update_faction_reputation** - ❌ MISSING
    - **Purpose**: Modify faction relationship values
    - **Implementation**: Reputation change tracking with consequence triggers
    - **Location**: **CORE PLUGIN** (generic reputation system, no ruleset content)
    - **Details**: Must trigger consequences at reputation thresholds
    - 🧪 **TEST**: Reputation changes applied, consequences trigger correctly

37. **update_npc_objective** - ❌ MISSING
    - **Purpose**: Modify NPC quest states and goals
    - **Implementation**: NPC objective status tracking and updates
    - **Location**: **CORE PLUGIN** (generic quest system, no ruleset content)
    - **Details**: Must support quest progression and completion detection
    - 🧪 **TEST**: Objective status updates, quest progression tracking

---

## Function Location Summary (Separation of Concerns)

### **CORE PLUGIN Functions** (Rule-Agnostic)
**Total: 23 functions**

**GameSetup Plugin:**
- `set_adventure_setting` - Generic setting storage
- `roll_stats` - Generic dice rolling

**WorldGeneration Plugin:**
- `create_quest` - Generic quest structure
- `create_faction` - Generic faction structure  
- `create_location` - Generic location structure
- `link_locations` - Generic location linking
- `place_npc_in_location` - Generic NPC placement

**Exploration Plugin:**
- `get_location_details` - Generic location data retrieval
- `search_lore` - Generic Vector DB search
- `update_player_location` - Generic movement system
- `advance_time` - Generic time system
- `update_player_money` - Generic currency system
- `update_bond_score` - Generic relationship system
- `get_faction_reputation` - Generic reputation system
- `update_faction_reputation` - Generic reputation system
- `update_npc_objective` - Generic quest system

**Combat Plugin:**
- `start_combat` - Generic combat initialization
- `get_turn_order` - Generic turn order display
- `make_attack_roll` - Generic dice mechanics
- `make_saving_throw` - Generic dice mechanics
- `apply_damage` - Generic damage application
- `apply_condition` - Generic condition tracking

**LevelUp Plugin:**
- `award_experience` - Generic XP system

### **RULESET Functions** (Rule-Specific)
**Total: 13 functions**

**GameSetup Phase:**
- `create_player_character` - Uses className (ruleset-specific)
- `get_character_stats_schema` - Reads ruleset stat definitions
- `define_new_class` - Modifies ruleset class definitions

**WorldGeneration Phase:**
- `create_npc` - Uses className (ruleset-specific)

**Combat Phase:**
- `roll_for_initiative` - Uses stat modifiers (ruleset calculations)

**LevelUp Phase:**
- `get_player_levelup_benefits` - Reads class advancement (ruleset content)
- `apply_player_levelup` - Applies class features (ruleset content)

**Cross-Phase Functions:**
- `get_shop_inventory` - Item definitions (ruleset content)
- `buy_item` - Item costs and properties (ruleset content)
- `equip_item` - Item bonuses and restrictions (ruleset content)
- `get_character_bonuses` - Bonus calculation rules (ruleset content)

---

## Implementation Priority (Updated with Gemini Details)

### Phase 1 (High Priority - Core Game Loop)
1. `roll_dice` - Universal dice system with standard notation parsing
2. `make_skill_check` - D&D-style resolution (1d20 + modifier vs DC)
3. `get_location_details` - Read-only location data retrieval 
4. `update_player_location` - Movement with exit validation
5. `start_combat` - Combat state initialization (no initiative rolling)
6. `search_lore` - Vector DB semantic search integration

### Phase 2 (Medium Priority - World Building with Vector Integration)
7. `create_location` - Dual-write to gamestate + Vector DB
8. `link_locations` - Bidirectional location connections
9. `create_npc` - Dual-write with backstory in Vector DB
10. `create_quest` - Dual-write with description in Vector DB  
11. `create_faction` - Dual-write with ideology in Vector DB
12. `advance_time` - Time progression with duration string parsing

### Phase 3 (Medium Priority - Combat Mechanics)
13. `make_attack_roll` - 1d20 + modifier vs AC with critical hit detection
14. `make_saving_throw` - 1d20 + stat modifier vs DC
15. `apply_damage` - Direct HP mutation in gamestate
16. `apply_condition` - Status effect tracking with duration
17. `roll_for_initiative` - Turn order determination
18. `get_turn_order` - Initiative-sorted participant list

### Phase 4 (Low Priority - Character Development)
19. `create_player_character` - Unified creation with Vector DB backstory
20. `set_adventure_setting` - Simple gamestate write operation
21. `get_character_stats_schema` - Ruleset stat structure retrieval
22. `roll_stats` - Unified stat generation with method parameter
23. `define_new_class` - In-memory ruleset modification
24. `award_experience` - Universal XP system
25. `get_player_levelup_benefits` - Ruleset-based advancement options
26. `apply_player_levelup` - Stat/ability advancement application

### Phase 5 (Low Priority - Economy & Social Systems)  
27. `get_shop_inventory` - Location-based item availability
28. `update_player_money` - Currency transaction validation
29. `buy_item` - Purchase validation and execution
30. `equip_item` - Equipment slot management with stat bonuses
31. `get_character_bonuses` - Multi-source bonus aggregation
32. `update_bond_score` - Relationship progression tracking
33. `get_faction_reputation` - Reputation status retrieval
34. `update_faction_reputation` - Reputation changes with consequences
35. `update_npc_objective` - Quest progression tracking
36. `place_npc_in_location` - NPC-location association management

---

## Testing Strategy

### Unit Tests
- Each function should have comprehensive unit tests
- Mock dependencies for isolation
- Test edge cases and error conditions

### Integration Tests
- Cross-plugin function interactions
- Ruleset integration validation
- State persistence verification

### End-to-End Tests
- Complete gameplay scenarios
- Phase transition workflows
- Multi-function operation sequences

### Performance Tests
- Function execution timing
- Memory usage under load
- Concurrent operation handling

---

## Notes

1. **Ruleset Integration**: All functions should properly integrate with the dynamic ruleset system
2. **Error Handling**: Consistent error reporting across all functions
3. **Logging**: Debug logging for all function calls and state changes
4. **Documentation**: XML documentation for all public functions
5. **Backwards Compatibility**: Ensure existing functionality continues to work
6. **JSON Serialization**: Consistent JSON response format across all functions

## Estimated Implementation Time (Updated)

- **Total Functions**: 36 core functions (reduced from 40 after analysis)
- **Estimated Effort**: 2-3 days per function (including comprehensive tests)
- **Key Integration Points**: 
  - Vector DB dual-write operations (6 functions)
  - Ruleset integration queries (5 functions) 
  - Gamestate mutation operations (8 functions)
  - D&D dice mechanics (4 functions)
- **Total Timeline**: 10-15 weeks for complete implementation
- **Minimum Viable Product**: 3-4 weeks (Phase 1 functions only)
- **Critical Path**: Vector DB integration must be completed early for world-building functions

## Key Implementation Notes

1. **Vector DB Integration Critical**: 6 functions require dual-write to Vector DB for lore searching
2. **Dice System Foundation**: Core dice mechanics support multiple game systems beyond D&D
3. **Ruleset Query Integration**: Functions must properly interface with `IRulesetManager`
4. **Gamestate Mutations**: Direct gamestate object manipulation requires careful validation
5. **Testing Priority**: Focus on integration tests for Vector DB and ruleset interactions