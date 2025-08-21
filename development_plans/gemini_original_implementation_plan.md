### **High-Level Summary of Game Phases**

1.  **GameSetup:** The player collaborates with the LLM to choose a setting and create a unique player character, complete with a name, a standard or custom-made class, a backstory, and D&D-style stats.
2.  **WorldGeneration:** The LLM acts as a Dungeon Master, using the player's choices to procedurally generate a unique adventure module. It builds the main plot around a Legendary Pokémon conflict, creates villainous factions and rivals, and lays out the world map with towns, routes, and a Gym Challenge structure. This data is stored in both a structured gamestate file and a searchable vector database for lore.
3.  **Exploration:** The core of the game. The player navigates the world, and the LLM describes locations by pulling from the generated lore. The LLM uses a D&D-style "Event Check" (a d20 roll) to determine the nature of random encounters—be they dangerous, beneficial, or standard. This allows for dynamic, procedurally fleshed-out details and encounters with wild Pokémon, which may possess rare **Unique Traits**.
4.  **Combat:** A tactical, turn-based phase merging Pokémon and D&D mechanics. All participants (human and Pokémon) roll for initiative and act in a shared turn order. Humans can take personal actions (attacking, using skills) or use their main action to **Command** a Pokémon, granting it a **Synergy Bonus**. A Pokémon acting without a command takes a sub-optimal but still useful **Default Action**. Attacks are resolved with D&D-style Attack Rolls vs. AC and Saving Throws.
5.  **LevelUp:** Triggered after combat, quest completion, or significant story events. The LLM's functions award experience to both the player and their Pokémon. The LLM then guides the player through an interactive process of leveling up, applying stat increases, gaining new class features, learning new moves, and potentially evolving their Pokémon.

---

### **Final, Consolidated Function List**

### **Generic Core Functions (Application Layer)**

*These universal functions form the game's engine.*

**Setup & World**
*   `set_adventure_setting(setting_details)`
*   `create_player_character(name, class_name, backstory, stats)`
*   `define_new_class(name, description, starting_perks)`
*   `get_character_stats_schema()`
*   `roll_stats(method)`
*   `create_quest(title, description, objectives)`
*   `create_faction(name, ideology, goal_summary)`
*   `create_npc(name, class_name, backstory, stats)`
*   `set_npc_role(npc_id, role)`
*   `create_location(name, description, tags)`
*   `link_locations(location_id_1, location_id_2, direction, description)`
*   `place_npc_in_location(npc_id, location_id)`

**Exploration & Interaction**
*   `get_location_details(location_id)`
*   `search_lore(query_text)`
*   `update_player_location(player_id, new_location_id)`
*   `roll_dice(dice_string)`
*   `make_skill_check(character_id, skill_name, difficulty_class)`
*   `get_world_state()`
*   `advance_time(duration)`
*   `get_environmental_obstacles(location_id)`

**Combat**
*   `start_combat(combatant_ids)`
*   `roll_for_initiative(character_id)`
*   `get_turn_order()`
*   `make_attack_roll(attacker_id, stat_modifier, target_ac)`
*   `make_saving_throw(target_id, stat_to_save, difficulty_class)`
*   `apply_damage(target_id, amount, damage_type)`
*   `apply_condition(target_id, condition_name, duration)`

**Progression & Inventory**
*   `award_experience(recipient_ids, amount)`
*   `get_player_levelup_benefits(player_id)`
*   `apply_player_levelup(player_id, choices)`
*   `get_shop_inventory(location_id)`
*   `update_player_money(player_id, amount)`
*   `buy_item(player_id, item_name, quantity)`
*   `equip_item(character_id, item_id, slot)`
*   `get_character_bonuses(character_id)`
*   `update_bond_score(trainer_id, pokemon_id, amount_to_change, reason)`

**Reputation & Time**
*   `get_faction_reputation(player_id, faction_id)`
*   `update_faction_reputation(player_id, faction_id, amount_to_change)`
*   `update_npc_objective(npc_id, quest_title, new_status)`

---

### **Pokémon Ruleset Functions (JavaScript Engine)**

*These functions contain Pokémon-specific logic and mechanics.*

**Core Pokémon Management**
*   `create_pokemon_instance(species_name, level)` *(Internally, this function has a chance to add a **Unique Trait** to the Pokémon)*
*   `assign_pokemon_to_npc(npc_id, pokemon_instance_id)`
*   `create_gym(location_id, gym_leader_npc_id, type_specialty, badge_name)`
*   `get_pc_box_contents(player_id)`
*   `deposit_pokemon(player_id, pokemon_to_deposit_id)`
*   `withdraw_pokemon(player_id, pokemon_to_withdraw_id)`
*   `get_region_suggestions(count)`
*   `get_available_classes()`

**Exploration & Field Abilities**
*   `get_possible_pokemon(tags, level_range)`
*   `attempt_field_move(pokemon_id, move_name, target_obstacle_id)`

**Combat**
*   `get_character_ac(character_id)`
*   `execute_pokemon_move(attacker_pokemon_id, move_name, target_id, is_commanded)`
*   `execute_human_action(human_id, action_details)`
*   `determine_pokemon_default_action(pokemon_id, combat_state)`

**LevelUp & Progression**
*   `execute_pokemon_levelup(pokemon_id)`
*   `pokemon_learn_new_move(pokemon_id, move_to_learn, move_to_forget)`
*   `evolve_pokemon(pokemon_id)`
*   `get_bond_abilities(pokemon_id)`

**Downtime & Recovery**
*   `start_rest(rest_type, location_id)`
*   `recover_resources(character_ids)`
*   `restore_pokemon_pp(pokemon_ids)`

---

### **Implementation Plan: LLM-Powered Modular TTRPG Engine**

#### **1. Project Overview**

This document outlines the implementation plan for a framework designed to run tabletop role-playing games powered by a Large Language Model (LLM). The system is architected to be modular, allowing for interchangeable rulesets (e.g., Pokémon, Star Wars, classic fantasy). The initial implementation target is a "Pokémon D&D Adventure" ruleset, which merges the tactical, turn-based combat and character progression of Dungeons & Dragons with the world, creatures, and themes of Pokémon.

The LLM acts as the Game Master (GM), narrating the world and adjudicating actions by calling a structured set of API functions, ensuring that its creativity is grounded by the game's mechanical rules.

#### **2. Core Architecture: Two-Layer System**

The system is split into two primary components to maximize modularity and reusability.

*   **Generic Core Engine:** The application's backend (e.g., built in Node.js, Python). It is completely game-agnostic.
    *   **Responsibilities:**
        *   Managing the primary gamestate object (e.g., a JSON file or a database).
        *   Providing a Vector Database for storing and searching unstructured lore.
        *   Exposing a generic API for universal TTRPG actions (creating characters, moving between locations, rolling dice, applying damage).
        *   Managing the LLM API calls, including prompt construction.
        *   Loading and interfacing with the specified Ruleset Engine.

*   **JavaScript Ruleset Engine:** A sandboxed engine that loads and executes game-specific logic from a ruleset file.
    *   **Responsibilities:**
        *   Loading a specific game rule file (e.g., `pokemon-ruleset.js`).
        *   Exposing the ruleset's specific functions to the Generic Core Engine.
        *   Containing all game-specific data (e.g., lists of Pokémon, move effects, type charts).
        *   Having read/write access to the gamestate object, but limited or no access to other application layers.

#### **3. Data Models & Schemas**

The gamestate will be structured around several key data models.

*   **`PlayerCharacter`**: Represents the human player.
    ```json
    {
      "id": "player_1",
      "name": "Jax",
      "class": "Researcher",
      "stats": { "Strength": 10, "Dexterity": 14, ... },
      "inventory": [{ "item_id": "potion", "quantity": 5 }],
      "pokemon_party": ["pokemon_instance_12", "pokemon_instance_45"],
      "money": 3000,
      "current_location_id": "route_01"
    }
    ```
*   **`PokemonInstance`**: A unique instance of a Pokémon. Distinguishes between the species (data in ruleset) and the individual (data in gamestate).
    ```json
    {
      "instance_id": "pokemon_instance_12",
      "species_name": "Bulbasaur",
      "level": 16,
      "current_hp": 45,
      "stats": { "hp": 45, "attack": 52, ... },
      "moves": ["Tackle", "Vine Whip", "Leech Seed"],
      "unique_traits": ["Alpha"],
      "bond_score": 150
    }
    ```
*   **`Location`**: A node in the world graph.
    ```json
    {
      "id": "route_01",
      "name": "Route 1",
      "tags": ["grassland", "day"],
      "description": "A simple path leading north from Pallet Town.",
      "exits": { "north": "viridian_city", "south": "pallet_town" },
      "npcs": ["npc_youngster_joey"]
    }
    ```

#### **4. API Specification**

The following functions represent the complete API available to the LLM.

**4.1. Generic Core API (Game-Agnostic)**

*   **Setup & World:** `set_adventure_setting`, `create_player_character`, `define_new_class`, `get_character_stats_schema`, `roll_stats`, `create_quest`, `create_faction`, `create_npc`, `set_npc_role`, `create_location`, `link_locations`, `place_npc_in_location`
*   **Exploration & Interaction:** `get_location_details`, `search_lore`, `update_player_location`, `roll_dice`, `make_skill_check`, `get_world_state`, `advance_time`, `get_environmental_obstacles`
*   **Combat:** `start_combat`, `roll_for_initiative`, `get_turn_order`, `make_attack_roll`, `make_saving_throw`, `apply_damage`, `apply_condition`
*   **Progression & Inventory:** `award_experience`, `get_player_levelup_benefits`, `apply_player_levelup`, `get_shop_inventory`, `update_player_money`, `buy_item`, `equip_item`, `get_character_bonuses`, `update_bond_score`
*   **Reputation & Time:** `get_faction_reputation`, `update_faction_reputation`, `update_npc_objective`

**4.2. Ruleset API (Pokémon-Specific)**

*   **Core Pokémon Management:** `create_pokemon_instance`, `assign_pokemon_to_npc`, `create_gym`, `get_pc_box_contents`, `deposit_pokemon`, `withdraw_pokemon`, `get_region_suggestions`, `get_available_classes`
*   **Exploration & Field Abilities:** `get_possible_pokemon`, `attempt_field_move`
*   **Combat:** `get_character_ac`, `execute_pokemon_move`, `execute_human_action`, `determine_pokemon_default_action`
*   **LevelUp & Progression:** `execute_pokemon_levelup`, `pokemon_learn_new_move`, `evolve_pokemon`, `get_bond_abilities`
*   **Downtime & Recovery:** `start_rest`, `recover_resources`, `restore_pokemon_pp`

#### **5. LLM Interaction Model: Prompt Engineering**

LLM calls will be constructed using a two-part prompt structure.

1.  **Master System Prompt:** A constant set of instructions defining the LLM's role as a GM, its core directives (narrate, use functions, be fair), and its step-by-step thinking process. This ensures consistent behavior.
2.  **Phase-Specific Task Prompt:** A dynamic block appended to the master prompt for each turn. It contains:
    *   `CURRENT PHASE`: e.g., "Exploration", "Combat".
    *   `Task`: A clear, immediate goal for the LLM (e.g., "The player has taken an action. Determine the outcome and narrate the result.").
    *   `Player Input`: The latest message from the player.
    *   `Game State Context`: A concise JSON snippet of the relevant parts of the current game state (e.g., player location, combatant statuses).

#### **6. Game Flow Logic (Phase Transitions)**

The game progresses through a defined state machine managed by the Generic Core Engine.

1.  **Start -> `GameSetup`**: Player and LLM collaborate to create the character.
2.  **`GameSetup` -> `WorldGeneration`**: An automated phase where the LLM calls functions to build the world.
3.  **`WorldGeneration` -> `Exploration`**: The main game loop begins.
4.  **`Exploration` <-> `Combat`**: `Exploration` actions can trigger `Combat`. The end of `Combat` always returns to `Exploration`.
5.  **`Exploration` -> `LevelUp`**: The `award_experience` function can return a `level_up_pending` flag. The LLM then initiates the `LevelUp` interactive flow, which, upon completion, returns to `Exploration`.
6.  Downtime, Social, and Inventory management are sub-routines handled within the `Exploration` phase.

#### **7. Ruleset File Specification (`pokemon-ruleset.js`)**

Each ruleset file will be a JavaScript object with a well-defined structure.

*   **`metadata`**: Basic info (name, version).
*   **`gameRules`**: Core mechanical values and formulas (AC calculation, type chart, synergy bonus effects, bond thresholds).
*   **`content`**: The game's "database" (lists of all Pokémon species, moves, items, player classes, starter options).
*   **`functionImplementations`**: The JavaScript code for all functions defined in the Ruleset API. These implementations will reference data from the `gameRules` and `content` sections.

#### **8. Implementation Steps**

1.  **Develop the Generic Core Engine:**
    *   Implement the gamestate management (JSON file I/O or database connection).
    *   Build the function stubs for the entire Generic Core API.
    *   Set up the Vector DB for lore storage and search.
2.  **Develop the JavaScript Ruleset Engine:**
    *   Create a sandboxed environment (e.g., using Node.js `vm` module) to safely load and execute ruleset files.
    *   Implement the bridge that allows the Generic Core Engine to call functions within the loaded ruleset.
3.  **Create the Pokémon Ruleset File:**
    *   Populate the `content` section with a starter set of Pokémon, moves, and items.
    *   Define the `gameRules` like the type chart.
    *   Implement the logic for all `functionImplementations`.
4.  **Implement LLM Integration:**
    *   Write the prompt construction logic that combines the Master System Prompt and the Phase-Specific Task Prompt.
    *   Implement the LLM API call handler, including the logic for function calling.
5.  **Build the Game Loop:**
    *   Implement the phase transition logic as described in Section 6.
    *   Create a simple user interface (command-line to start) to handle player input and display LLM narration.
6.  **Iterative Testing:** Test each phase individually, ensuring the LLM correctly understands its tasks, calls the right functions, and narrates the results appropriately.