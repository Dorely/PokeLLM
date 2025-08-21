### **Function Implementation & Testing Plan**

This document details each function required by the LLM-powered TTRPG engine. Functions are separated into two categories: the game-agnostic **Core Plugin** and the game-specific **Ruleset Functions**.

---
### **Part 1: Generic Core Plugin Functions**
---

These functions are universal and form the backbone of the game engine.

#### **GameSetup Phase**

*   **Function:** `set_adventure_setting(setting_details)`
    *   **Phase:** GameSetup
    *   **Location:** Core Plugin
    *   **Purpose:** To store the player's choice of setting (e.g., a canonical region name or a descriptive paragraph for a custom world) in the main gamestate. This data is used as a primary input for the WorldGeneration phase.
    *   **Implementation Details:** This is a simple write operation to a top-level key (e.g., `gamestate.adventure_setting`) in the gamestate object.
    *   **Test Case:** Given an empty gamestate, when `set_adventure_setting` is called with `{"details": "Kanto Region"}`, the `gamestate.adventure_setting.details` property must be equal to "Kanto Region".

*   **Function:** `create_player_character(name, class_name, backstory, stats)`
    *   **Phase:** GameSetup
    *   **Location:** Core Plugin
    *   **Purpose:** To create the official player character object in the gamestate and its corresponding lore entry.
    *   **Implementation Details:** Writes a structured object to `gamestate.player_character`. It also creates an entry in the Vector DB containing the `backstory` for semantic searching.
    *   **Test Case:** When called with valid parameters, the gamestate should contain a `player_character` object with matching `name`, `class_name`, and `stats`. A separate check should verify that the `backstory` text can be retrieved from the Vector DB.

*   **Function:** `define_new_class(name, description, starting_perks)`
    *   **Phase:** GameSetup
    *   **Location:** Core Plugin
    *   **Purpose:** To dynamically add a new, custom character class to the ruleset for the current game session.
    *   **Implementation Details:** This function modifies the *in-memory representation* of the loaded ruleset, adding a new class object to its `content.classes` array. It does not modify the base ruleset file on disk.
    *   **Test Case:** After calling this function with "Pokémon Ranger" details, a subsequent call to the ruleset's `get_available_classes()` function must include "Pokémon Ranger" in its returned list.

*   **Function:** `get_character_stats_schema()`
    *   **Phase:** GameSetup
    *   **Location:** Core Plugin
    *   **Purpose:** To retrieve the list of required character statistics from the loaded ruleset.
    *   **Implementation Details:** This function calls into the ruleset to fetch a simple array of strings (e.g., `["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"]`).
    *   **Test Case:** When the Pokémon D&D ruleset is loaded, this function must return the expected array of six D&D-style stat names.

*   **Function:** `roll_stats(method)`
    *   **Phase:** GameSetup
    *   **Location:** Core Plugin
    *   **Purpose:** A utility to simulate rolling dice for character ability scores.
    *   **Implementation Details:** The `method` parameter (e.g., "4d6_drop_lowest") determines the rolling logic.
    *   **Test Case:** When called with `method="4d6_drop_lowest"`, the function must return an array of 6 integers, each of which must be between 3 and 18.

---
#### **WorldGeneration Phase**

*   **Function:** `create_quest(title, description, objectives)`
*   **Function:** `create_faction(name, ideology, goal_summary)`
*   **Function:** `create_npc(name, class_name, backstory, stats)`
*   **Function:** `create_location(name, description, tags)`
    *   **Phase:** WorldGeneration (but can be used later)
    *   **Location:** Core Plugin
    *   **Purpose:** These functions create the primary entities of the game world.
    *   **Implementation Details:** Each of these functions performs a dual-write: a structured object is created in the gamestate, and a corresponding descriptive text (`description`, `backstory`, `ideology`) is indexed in the Vector DB. They should return the unique ID of the created entity.
    *   **Test Case:** After calling `create_location` with a name and description, the gamestate must contain a location object with that name and a new ID. A subsequent call to `search_lore` with a query related to the description must return that description text.

*   **Function:** `link_locations(location_id_1, location_id_2, direction, description)`
    *   **Phase:** WorldGeneration
    *   **Location:** Core Plugin
    *   **Purpose:** To create a traversable connection between two location nodes.
    *   **Implementation Details:** This function must perform a two-way update. It adds an exit to `location_1`'s `exits` object (e.g., `{"north": "location_2"}`) and a corresponding exit to `location_2`'s `exits` object (e.g., `{"south": "location_1"}`).
    *   **Test Case:** Given two locations A and B, after calling `link_locations(A.id, B.id, "north", ...)`: `gamestate.locations[A.id].exits.north` must equal `B.id`, AND `gamestate.locations[B.id].exits.south` must equal `A.id`.

---
#### **Exploration Phase & Sub-systems**

*   **Function:** `get_location_details(location_id)`
    *   **Phase:** Exploration
    *   **Location:** Core Plugin
    *   **Purpose:** To retrieve all structured data about a specific location for the LLM.
    *   **Implementation Details:** This is a read-only function that fetches a location object from the gamestate.
    *   **Test Case:** Given a gamestate where location "Route 1" has an exit to "Viridian City", calling `get_location_details("route_1")` must return an object containing the name "Route 1" and the correct exit data.

*   **Function:** `search_lore(query_text)`
    *   **Phase:** Exploration
    *   **Location:** Core Plugin
    *   **Purpose:** To perform a semantic search on the Vector DB to find relevant lore, history, and descriptions.
    *   **Implementation Details:** This function is a direct interface to the Vector DB's query engine.
    *   **Test Case:** Given a Vector DB indexed with the backstory of an NPC named "Professor Oak", a call with `query_text="who is the Pokémon professor"` should return the relevant text snippet.

*   **Function:** `roll_dice(dice_string)`
    *   **Phase:** Exploration / Combat
    *   **Location:** Core Plugin
    *   **Purpose:** A generic utility to simulate dice rolls.
    *   **Implementation Details:** Must parse standard dice notation (e.g., '1d20', '2d6', '1d100+5').
    *   **Test Case:** A call with `'1d20'` must return an integer between 1 and 20. A call with `'2d6'` must return an integer between 2 and 12.

*   **Function:** `make_skill_check(character_id, skill_name, difficulty_class)`
    *   **Phase:** Exploration (Social)
    *   **Location:** Core Plugin
    *   **Purpose:** To resolve a D&D-style skill check.
    *   **Implementation Details:** The function rolls a `1d20`, adds the character's relevant skill modifier (derived from their base stats and proficiencies), and compares the total to the `difficulty_class` (DC).
    *   **Test Case:** Given a character with a +5 bonus in "Persuasion", when this function is called with `skill_name="Persuasion"` and `difficulty_class=15`, it should return `{"success": true}` on a d20 roll of 10 or higher, and `{"success": false}` on a roll of 9 or lower.

*   **Function:** `advance_time(duration)`
    *   **Phase:** Exploration (Downtime)
    *   **Location:** Core Plugin
    *   **Purpose:** To move the in-game clock forward.
    *   **Implementation Details:** Updates the `gamestate.world_state.time` and `gamestate.world_state.day` variables according to the `duration` string (e.g., "1 hour", "to next morning").
    *   **Test Case:** Given the time is "Day 1, 09:00", calling `advance_time("6 hours")` should change the time to "Day 1, 15:00".

---
#### **Combat Phase**

*   **Function:** `start_combat(combatant_ids)`
    *   **Phase:** Combat
    *   **Location:** Core Plugin
    *   **Purpose:** To initialize the combat state.
    *   **Implementation Details:** Creates a `combat_state` object in the gamestate, sets `gamestate.current_phase` to "Combat", and populates the combatant list. It does NOT roll initiative.
    *   **Test Case:** After calling with a list of IDs, `gamestate.current_phase` must be "Combat" and `gamestate.combat_state.combatants` must contain objects for each ID provided.

*   **Function:** `make_attack_roll(attacker_id, stat_modifier, target_ac)`
*   **Function:** `make_saving_throw(target_id, stat_to_save, difficulty_class)`
    *   **Phase:** Combat
    *   **Location:** Core Plugin
    *   **Purpose:** The core dice resolution mechanics of D&D combat.
    *   **Implementation Details:** These functions perform a `1d20` roll, add the relevant modifier from the character's stats, and compare against the target value (AC or DC).
    *   **Test Case:** For `make_attack_roll`, given a `stat_modifier` of +7 and a `target_ac` of 18, the function must return `{"hit": true}` on a d20 roll of 11 or higher, and `{"hit": false}` otherwise. It must also correctly identify a natural 20 as a critical hit.

*   **Function:** `apply_damage(target_id, amount, damage_type)`
*   **Function:** `apply_condition(target_id, condition_name, duration)`
    *   **Phase:** Combat
    *   **Location:** Core Plugin
    *   **Purpose:** To modify a character's state as a result of an action.
    *   **Implementation Details:** These functions directly mutate the target character's object in the gamestate, reducing their `current_hp` or adding to their `active_conditions` array.
    *   **Test Case:** Given a character with 50 HP, calling `apply_damage` with `amount=15` must result in the character's `current_hp` being 35.

---
### **Part 2: Pokémon Ruleset Functions**
---

These functions contain game-specific logic and are loaded by the Core Plugin.

#### **Core Pokémon Management**

*   **Function:** `create_pokemon_instance(species_name, level)`
    *   **Phase:** WorldGeneration / Exploration
    *   **Location:** Ruleset
    *   **Purpose:** To create a unique, living instance of a Pokémon.
    *   **Implementation Details:** This function must look up the `species_name` in its internal content database, calculate stats based on the provided `level` and the species' base stats, and determine its moveset from the species' learnset. It must also contain the logic for a low-probability roll to add a `Unique Trait` like "Shiny".
    *   **Test Case:** When `create_pokemon_instance("Bulbasaur", 7)` is called, the returned object must have stats higher than its base stats and must know the move "Vine Whip" (learned at level 7) but not "Poison Powder" (learned at 13).

*   **Function:** `create_gym(location_id, gym_leader_npc_id, type_specialty, badge_name)`
    *   **Phase:** WorldGeneration
    *   **Location:** Ruleset
    *   **Purpose:** To designate a location as a Pokémon Gym.
    *   **Implementation Details:** Modifies the location object in the gamestate, adding a `gym_details` property containing the leader's ID and badge name.
    *   **Test Case:** After this function is called on location "Pewter City", the `gamestate.locations["pewter_city"].gym_details.badge_name` must be "Boulder Badge".

---
#### **Exploration & Field Abilities**

*   **Function:** `get_possible_pokemon(tags, level_range)`
    *   **Phase:** Exploration
    *   **Location:** Ruleset
    *   **Purpose:** To provide the LLM with a rules-compliant list of Pokémon that can be encountered in a specific environment.
    *   **Implementation Details:** Filters its internal Pokémon database where a Pokémon's `habitatTags` array is a superset of the input `tags`.
    *   **Test Case:** Given `tags=["forest", "day"]`, the function must return a list that includes "Caterpie" and "Pidgey", but must NOT include "Zubat" (cave) or "Magikarp" (water).

---
#### **Combat**

*   **Function:** `get_character_ac(character_id)`
    *   **Phase:** Combat
    *   **Location:** Ruleset
    *   **Purpose:** To calculate the Armor Class for any combatant based on the ruleset's specific formula.
    *   **Implementation Details:** Reads the AC formula from its `gameRules` section and applies it to the character's stats.
    *   **Test Case:** Given a Pokémon with a Dexterity of 14 (+2 modifier) and the formula `"10 + dexterityModifier"`, this function must return 12.

*   **Function:** `execute_pokemon_move(attacker_pokemon_id, move_name, target_id, is_commanded)`
    *   **Phase:** Combat
    *   **Location:** Ruleset
    *   **Purpose:** The main engine for resolving a Pokémon's action in combat. This is the most complex ruleset function.
    *   **Implementation Details:** This function is a controller that calls multiple Core Plugin functions. It looks up the move's data, determines if it's an attack roll or saving throw, applies the Synergy Bonus if `is_commanded` is true, calculates damage (including the crucial type effectiveness multiplier from its `gameRules`), and finally calls `apply_damage` or `apply_condition` with the final results.
    *   **Test Case:** Given a Fire-type move (power 40) used against a Grass-type Pokémon (which has a 2x weakness), when `execute_pokemon_move` is called, the final `amount` passed to the `apply_damage` function must be significantly higher than if it were used against a Water-type Pokémon (0.5x resistance).

*   **Function:** `determine_pokemon_default_action(pokemon_id, combat_state)`
    *   **Phase:** Combat
    *   **Location:** Ruleset
    *   **Purpose:** To decide a Pokémon's action when it is not directly commanded by the player.
    *   **Implementation Details:** Contains simple AI logic, such as "target the opponent with the lowest current HP" or "use a super-effective move if available". Returns a simple action object like `{"move_name": "Tackle", "target": "enemy_id"}`.
    *   **Test Case:** Given a combat state where one enemy is at 10% HP and another is at 100%, this function should return an action targeting the enemy with 10% HP.

---
#### **LevelUp & Progression**

*   **Function:** `execute_pokemon_levelup(pokemon_id)`
    *   **Phase:** LevelUp
    *   **Location:** Ruleset
    *   **Purpose:** To apply the automatic consequences of a Pokémon leveling up.
    *   **Implementation Details:** Increases the Pokémon's level, recalculates and applies its stat increases, and checks its learnset and evolution conditions.
    *   **Test Case:** Given a level 15 Bulbasaur, calling this function must change its level to 16, increase its stats, and the return value must include `{"evolution_pending": "Ivysaur"}`.

*   **Function:** `get_bond_abilities(pokemon_id)`
    *   **Phase:** Combat / Exploration
    *   **Location:** Ruleset
    *   **Purpose:** To check a Pokémon's current bond score and return any passive abilities it has unlocked.
    *   **Implementation Details:** Compares the Pokémon's `bond_score` against the thresholds defined in the `gameRules.bondTiers`.
    *   **Test Case:** Given a Pokémon with a `bond_score` of 120 and a bond tier defined at a threshold of 100, this function must return the ability associated with that tier. If the score is 90, it must return an empty list.