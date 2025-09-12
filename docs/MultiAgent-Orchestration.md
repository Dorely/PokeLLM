# PokeLLM Multi‑Agent Orchestration (Atomic Turns)

Status: Draft
Owner: PokeLLM maintainers
Last updated: YYYY‑MM‑DD

## Summary
- Replace the phase‑based flow with a multi‑agent orchestration model built for atomic turns (no cross‑turn chat history).
- Each player input triggers a one‑shot orchestration that reconstructs all needed context from persisted state and vector memory, routes to the appropriate domain agent(s), applies a single atomic state commit, and returns a response.
- No migration/backwards‑compat required; this is a clean design targeting the new approach.

## Goals
- Atomic turns: zero reliance on prior chat messages across turns; correctness derives from canonical state + facts.
- Accurate state: a single writer applies validated state deltas in an atomic commit at the end of a turn.
- Clear separation of concerns: specialized agents with explicit tool allowlists.
- Determinism where needed: seeded RNG and idempotent commits to avoid double‑apply.
- Efficient context: strict token budgets; summarize recent events and pull only relevant facts.

## Non‑Goals
- Backwards compatibility with the old phase architecture.
- Long‑running conversational memory across turns.
- Enabling commented integration tests or external dependencies beyond existing vector store abstractions.

## Orchestration Pattern
We will use SK Handoff Orchestration for per‑turn gameplay and SK Magentic Orchestration for game setup (Adventure Module creation):
- Handoff (per turn): Agents delegate control to each other dynamically (e.g., Dialogue ↔ Combat ↔ Exploration) within a single atomic turn. Minimal central routing; agents decide applicability and next handoff.
- Magentic (setup): Planner/manager coordinates generation, validation, indexing, and seeding of the Adventure Module before play.

## High‑Level Flow
1) Input: Player message arrives (no prior chat context carried).
2) Context Synthesis (ContextBroker):
   - Read canonical state and slice recent events.
   - Build dialogue recap for involved participants and scene.
   - Retrieve relevant facts from vector memory (top‑K) with citations; pull exact NPC dialogue if referenced.
   - Produce a compact ContextPack.
3) Guard Decision (GuardAgent):
   - Assess player intent against current state and the Adventure Module; classify as valid, reject, improv, or needs_player_input.
   - On reject: produce an in-world narrative explaining why; end the turn without commit.
   - On improv: propose a ModulePatch (e.g., add a blacksmith); may request a dice roll from the player to gate improv.
   - On needs_player_input: emit a prompt and pendingActionId; end the turn without commit.
   - Emit GuardDecision for the orchestrator.
4) Plot Direction (PlotDirector):
   - Read Adventure Module (quests, arcs, beats) and current quest progress.
   - Retrieve relevant quest nodes/hints/beats and entity cards.
   - Emit PlotDirective (targetObjectives, suggestedBeat, pacing, spotlightNPCs, softConstraints).
5) Handoff Start: PlotDirector selects the initial domain agent and passes a message; active agents may hand off to others as needed (e.g., Combat → Dialogue → Combat) within budgets.
6) Domain Execution:
   - Active agent proposes narrative and a StateDelta; uses RulesTools for checks/rolls as needed, shaped by PlotDirective.
   - If more work is needed by another domain, agent issues a directed handoff; otherwise returns completed.
   - If player input is required (dice/choice), agent returns needs_player_input with a prompt and pendingActionId.
7) Commit (WorldAgent): Validate and atomically apply StateDelta; append events. If needs_player_input, do not commit; persist PendingAction instead.
8) Memory Curation: Extract durable facts from new events and upsert to vector store with citations.
9) Output: FinalNarrative to player or a player prompt if input is required; discard ephemeral context.

## Agents and Responsibilities
- ContextBroker (read‑only): Sole authority to assemble “what matters now.”
  - Tools: StateRead.*, MemoryRetrieve.*, Summarize.*
  - Output: ContextPack (structured, token‑bounded)
- GuardAgent (guard): Validates player intent; rejects cheating/impossible actions with in-world narrative; proposes controlled improvisations (ModulePatch) and may request dice rolls to gate improv.
  - Tools: ModuleRead.*, QuestGraph.*, MemoryRetrieve.* (read-only), Rules.check/roll
  - Output: GuardDecision (valid | reject | improv | needs_player_input)
  - Writes: none (proposes ModulePatch via DomainResult/StateDelta events)
- PlotDirector (advisory): Guides progression toward quests/arcs/beats based on the Adventure Module and current progress.
  - Tools: ModuleRead.*, QuestGraph.*, MemoryRetrieve.* (read-only)
  - Output: PlotDirective (objectives, suggested beat, pacing, spotlight, constraints)
  - Writes: none
- Handoff Control: The currently active agent decides whether to complete, hand off to another agent, or request player input.
- DialogueAgent: NPC and social interactions; produces DialogueSpoken events.
  - Tools: Dialogue.*, Suggest.* (read‑only helpers), Rules.* (checks/rolls)
  - Writes: proposes StateDelta; no direct writes
- CombatAgent: Encounter orchestration; resolves actions and outcomes.
  - Tools: Rules.*, Combat.* (read/compute)
  - Writes: proposes StateDelta; no direct writes
- ExplorationAgent: Movement, investigation, environment interactions.
  - Tools: WorldQuery.*, Action.* (requests), Rules.* (environmental checks/rolls)
  - Writes: proposes StateDelta; no direct writes
- WorldAgent (single writer): Applies validated StateDelta; appends events; advances time.
  - Tools: StateWrite.*
- MemoryCurator: Upserts durable facts with event citations; dedupes.
  - Tools: MemoryWrite.*

## Action Economy
- Cinematic (non-combat): One primary state change per turn plus narrative; examples: reveal one clue, move to a location with time advance, perform a single interaction. If more detail or a player decision is needed, end the turn with a clear playerPrompt (PendingAction).
- Tactical (combat): One Action + optional Bonus + Move per participant turn; brief dialogue is allowed via a short DialogueAgent handoff (one line). Reactions are deferred for a later version.
- One-exchange rule: Any mid-turn back-and-forth is limited to a single short exchange; otherwise, end the turn with a consolidated prompt.
- Batch prompts: When multiple small choices exist, aggregate them into one playerPrompt with stable option ids to reduce round trips.

## Core Contracts (schemas)
These are language‑agnostic shapes; concrete C# models will mirror them.

- ContextPack
  - sceneId: string
  - sceneSummary: string
  - participants: EntityRef[]
  - dialogueRecap: string[] (last M relevant utterances)
  - recentEvents: EventPreview[] (last K relevant events)
  - openObjectives: ObjectivePreview[]
  - timeWeather: { time: string, weather?: string }
  - relevantFacts: FactPreview[] (text + citation ids)

- PlotDirective (from PlotDirector)
  - targetObjectives: { questId: string, objectiveId: string, reason: string }[]
  - suggestedBeat?: { arcId: string, beatId: string, rationale: string }
  - pacing: { tension: "low"|"med"|"high", sceneLengthHint: "short"|"normal"|"long" }
  - spotlightNPCs: string[] (npc ids)
  - softConstraints: string[] (e.g., avoid spoilers, respect secrets)

- GuardDecision (from GuardAgent)
  - status: "valid" | "reject" | "improv" | "needs_player_input"
  - narrative?: string (used when status = reject)
  - improvPatch?: ModulePatch (proposed addition/modification to the Adventure Module)
  - playerPrompt?: { type: "dice_roll" | "choice", data: object } (e.g., roll to discover a blacksmith exists)

- DomainResult (agent → orchestration)
  - status: "completed" | "continue" | "needs_player_input" | "error"
  - nextAgent?: string (for "continue")
  - stateDelta?: StateDelta
  - playerPrompt?: { type: "dice_roll" | "choice" | "free_text", data: object }
  - error?: string

- PendingAction (persisted when player input is needed)
  - id: string
  - requestedBy: string (agent name)
  - prompt: { type: string, data: object }
  - createdAt: string (ISO)
  - expiresAt?: string (ISO)
  - turnId: string

- ModulePatch (proposal; typically proposed by GuardAgent; applied by WorldAgent as a ModulePatched event)
  - patchId: string
  - kind: "AddEntity" | "AddLocation" | "AddShop" | "AddNPC" | "AddQuestNode" | "EditEntity" | "EditLocation" | "Custom"
  - payload: object (typed by kind)
  - rationale: string
  - citations?: string[] (events or module refs)

- StateDelta (proposed by domain agent; applied by WorldAgent)
  - entityChanges: EntityChange[] (patches)
  - questChanges: QuestChange[]
  - timeDelta?: { minutes?: int, hours?: int }
  - newEvents: Event[] (proposed event payloads)
  - turnId: string (idempotency)

- Event
  - id: string
  - type: string (DialogueSpoken, ActionAttempted, OutcomeApplied, Movement, ItemTransfer, CombatStart/Turn/End, SceneChange, QuestUpdate, WorldTick)
  - ts: string (ISO)
  - entities: EntityRef[]
  - payload: object
  - turnId: string
  - parentEventIds?: string[]

- Fact (vector memory)
  - id: string
  - text: string
  - entities: EntityRef[]
  - citations: string[] (event ids)
  - embedding: float[] (provider‑specific dims)

### StateDelta aggregation and commit
- Accumulation: During a turn, each DomainResult may include a partial StateDelta. The orchestration maintains an accumulator that merges patches in order.
- Validation: WorldAgent validates the aggregated delta for conflicts (e.g., double spend, invalid transitions) before commit.
- Module patches: If GuardDecision.improv produces a ModulePatch, it is included as a proposed event (ModulePatched) and applied by WorldAgent at commit.
- Idempotency: The final aggregated delta carries the turnId; intermediate partials are not persisted.
- Events: Proposed events from partial deltas are appended to the final event list in temporal order before commit.

## Setup Contracts (schemas)
- NewGameRequest
  - requestedBy: string (player id or session id)
  - loadFromPath?: string (if provided, skip setup and load)
  - options?: WorldGenOptions
- PlayerCharacter
  - name: string
  - archetype/class: string
  - background: string
  - goals: string[]
  - quirks?: string[]
- SettingChoice
  - id?: string (predefined setting id)
  - name: string
  - description?: string
  - themes?: string[]
- WorldGenOptions
  - tone: "grim"|"neutral"|"heroic"
  - difficulty: "easy"|"normal"|"hard"
  - contentTags?: string[] (e.g., exploration, intrigue)
- SetupResult
  - adventureModuleId: string
  - savePath: string
  - initialSceneId: string
  - characterId: string

## Generated Content Schemas (v1)
- Ability
  - id: string; name: string; description: string
  - actionTemplateId: string (RulesTools template id)
  - params: object (e.g., { dice: "1d6+DEX.mod", damageType: "fire", status: { id: "burn", duration: 1 } })
  - cost?: { resource?: string, amount?: number }; cooldown?: number
  - tags?: string[]; prerequisites?: string[] (ability ids or attributes)
- ClassArchetype
  - id: string; name: string; primaryAttributes: string[]
  - baseStats: object; growthCurves: object
  - startingAbilities: string[] (ability ids)
  - featureUnlocks: { [level: number]: string[] } (features or ability ids)
- Species
  - id: string; name: string; types/tags: string[]
  - baseStats: object; learnset: { [level: number]: string[] }
  - evolution?: { trigger: "level"|"item"|"quest", value?: number|string }
- Progression
  - xpCurveId: string; statGainFormula: string; proficiencyProgress?: object
- ContentProvenance
  - seed: string; prompts: string[]; generatedAt: string (ISO)

## Fact Promotion
- Meaning: Converting transient events into durable, retrievable facts stored in vector memory (session_facts) with citations to the source events.
- Criteria (initial):
  - Stable world truths revealed (e.g., “Bandits operate on the north road”).
  - Player/NPC commitments or stateful tags that persist beyond the scene (e.g., “Guard is wary of the party”).
  - Quest-relevant discoveries (clues, unlocked hints, objective completions).
- Non-facts: ephemeral emotions, one-off quips, random flavor unless referenced by future mechanics.
- Workflow: After commit, MemoryCurator scans new events → extracts candidate facts → dedupes by similarity (entity- and quest-scoped) → upserts with eventId citations.

## On-Demand Content Generation (runtime)
- Who: GuardAgent proposes ModulePatch when content is missing (e.g., blacksmith shop, missing move/ability), optionally dice-gated via playerPrompt.
- Constraints: All generated content must bind to valid RulesTools templates and pass ContentValidator power-budget checks before WorldAgent applies the ModulePatched event.
- Provenance: Include generation seed and rationale in ModulePatch payload for later audit.

## Determinism, Idempotency, Safety
- RNG: Derive a per‑turn seed (e.g., hash of turnId + player input); RulesTools use only this seed.
- Idempotent commits: WorldAgent stores lastAppliedTurnIds; reject duplicate StateDelta.turnId.
- Single writer: Only WorldAgent mutates game state; all others propose deltas.
- Budgets: Max handoffs per turn, max internal rounds/tool calls per agent, and strict ContextPack/prompt token limits. Turns terminate on completion, budget exhaustion, or pending player input.

## Data Model (state + module + memory)

### Adventure Module (Canonical)
- Adventure: id, title, synopsis, themes, tags.
- Locations: id, name, description, tags, connections.
- NPCs: id, name, persona, motivations, secrets, relationships (ids), homeLocationId.
- Quests: id, title, arcs[], objectives[] with preconditions, success/fail effects, hints, rewards.
- Factions: id, traits, goals, relationships.
- Items/Artifacts: id, lore, properties, whereFound.
- Plot Arcs: id, beats[] (intended sequence, triggers, fail‑forward alternatives).
- ModulePatches: additive edits tracked as patches; base module remains versioned and auditable.
- Derived indices: quest dependency graph; entity cross‑references for fast reads.

#### Plot Milestones (beats)
- Each plot arc is a sequence of milestones with:
  - readyWhen: a minimal predicate over state/facts determining if a milestone can advance.
  - onAdvance: effects/rewards applied when advancing (e.g., add_gold, unlock_hint, reputation_delta).
- Predicate examples:
  - discovered_fact("bandits_north_road") AND relationship("barkeep") >= 1
  - scene_visited("forge") AND item_owned("lost_ring")
  - encounter_won("bandit_ambush") OR time_elapsed_minutes >= 30

### Living World State (Runtime)
- Entities: Instances referencing module ids with dynamic fields (hp/status/conditions, inventory, location).
- Scene: active scene id, participants (entity ids), tone, environment.
- WorldClock/Weather: current time and conditions.
- Quest Progress: per quest/objective flags (unseen/active/complete/failed), discovered hints, evidence.
- PendingAction: optional; persisted when an agent requests mid‑turn player input; consumed on next input.
- Event Log: append‑only events with citations to module ids; indexed by entityId, sceneId, questId.
- Snapshots: ActiveSceneSnapshot, CharacterSnapshot(s) computed from state+events for fast reads.

### Vector Memory (Retrieval Layer)
- Collections and tags:
  - module_docs: authored long‑form module text. Tags: {type, adventureId, locationId?, npcId?, questId?, arcId?, factionId?}
  - entity_cards: concise per‑entity profiles. Tags: {type, entityId}
  - quest_nodes: objectives/hints/beats as standalone chunks. Tags: {type: "objective"|"hint"|"beat", questId, objectiveId?, arcId?}
  - lore_canon: global/region setting info. Tags: {type: "lore", scope}
  - npc_dialogue: verbatim NPC lines (one chunk per utterance or short exchange). Tags: {type: "dialogue", speakerId, sceneId?, eventId, turnId}
  - session_facts: curated facts from events with citations. Tags: {type: "fact", entityIds[], questId?, sceneId?, turnIds[]}
- Usage: filter by tags first, then similarity top‑K; all entries carry module/event citations for provenance; mutable runtime state is not stored here.
- Dialogue policy: Event log maintains concise dialogue recaps for recent context; ContextBroker pulls exact NPC quotes from npc_dialogue when precision is required (e.g., player disputes wording).

## Tool Catalogs (by agent)
- ContextBroker: StateRead.get_scene, StateRead.list_participants, StateRead.get_stats, StateRead.get_inventory, StateRead.get_quests, StateRead.get_time_weather, StateRead.get_recent_events, MemoryRetrieve.search_facts, MemoryRetrieve.fetch_citations, Summarize.summarize_events, Summarize.dialogue_recap
- GuardAgent: ModuleRead.get_adventure, QuestGraph.get_objectives, QuestGraph.get_beats, MemoryRetrieve.search_quest_nodes, Rules.check, Rules.roll
- PlotDirector: ModuleRead.get_adventure, ModuleRead.get_entity_card, QuestGraph.get_objectives, QuestGraph.get_beats, MemoryRetrieve.search_quest_nodes
- DialogueAgent: Dialogue.speak_as, Dialogue.propose_lines, Dialogue.update_relationship_request, Suggest.request_fact, Suggest.ask_recent_dialogue, Rules.check, Rules.roll
- CombatAgent: Rules.compute_dc, Rules.check, Rules.roll(dice_spec, seed), Combat.start_encounter, Combat.set_initiative, Combat.resolve_action, Combat.apply_effects, Combat.end_encounter
- ExplorationAgent: WorldQuery.describe_environment, WorldQuery.find_interactables, WorldQuery.check_access, Action.move_entity_request, Action.inspect_request, Action.item_interact_request, Rules.check, Rules.roll
- WorldAgent: StateWrite.apply_state_delta(transaction), StateWrite.create_event, StateWrite.move_entity, StateWrite.transfer_item, StateWrite.update_quest, StateWrite.tick_time
- MemoryCurator: MemoryWrite.upsert_fact(text, entities, citations), MemoryWrite.dedupe_by_similarity

- Setup agents (Magentic):
  - SetupDetector: SaveRead.exists, SaveRead.load_metadata
  - CharacterBuilder: Prompt.ask_character_details, Validate.character_sheet
  - SettingSelector: Prompt.pick_setting, Validate.setting
  - ContentBootstrapper: Content.derive_seeds
  - ContentGenerator: Content.generate_catalogs (classes, abilities, species, progression)
  - ContentValidator: Validate.content_schema, Validate.power_budget
  - ModuleGenerator: ModuleWrite.generate
  - ModuleValidator: Validate.module_schema, Validate.quest_graph, Validate.cross_refs
  - ModuleIndexer: MemoryWrite.seed_module_docs/entity_cards/quest_nodes, Build.indices
  - SeedState: StateWrite.initialize_world, StateWrite.session_started, StateWrite.character_created

## Orchestration Mapping (SK)
- Per-turn: HandoffOrchestration<ChatMessageContent, ChatMessageContent>
  - Step 1: ContextBroker builds ContextPack.
  - Step 2: ContextBroker initiates handoff to GuardAgent; GuardAgent emits GuardDecision.
  - Step 3: If GuardDecision.status = reject → return narrative to player; no commit. If needs_player_input → persist PendingAction and return prompt. If improv → include ModulePatch in accumulated StateDelta; proceed.
  - Step 4: PlotDirector emits PlotDirective and may hand back to ContextBroker for additional details.
  - Step 5: PlotDirector initiates the first domain agent (Dialogue/Combat/Exploration); active agent may hand off to another agent; may request player input via DomainResult.needs_player_input.
  - Step 6: WorldAgent validates and commits StateDelta when status is completed (including ModulePatched event if present).
  - Step 7: MemoryCurator derives/upserts facts.
- Game setup: Magentic orchestration coordinates character creation, setting selection, module generation, validation, indexing, memory seeding, and initial state save.

## Game Setup (Magentic)
- Trigger: On app start when no saved game is loaded from file.
- Flow (agents coordinated by a Magentic manager):
  - SetupDetector: checks for existing save; if found, short-circuits setup.
  - CharacterBuilder: collects player character details (name, archetype/class, background, goals, quirks); writes provisional Character entity.
  - SettingSelector: prompts user to pick a setting (or provides options based on preferences); captures SettingChoice and WorldGenOptions (tone, difficulty, themes).
  - ContentBootstrapper: derives initial content seeds from Character + Setting + Options (themes, types/tags, power budget, starter lists).
  - ContentGenerator: generates data-first content catalogs (classes/archetypes, abilities/moves, species/monsters, progression) that bind to RulesTools templates via parameters.
  - ContentValidator: schema and sanity checks on generated content (template ids/params present, power budgets, unique ids).
  - ModuleGenerator: generates the Adventure Module tailored to Character + Setting + Options and the generated content (adventure, locations, NPCs, quests, arcs); outputs canonical module JSON.
  - ModuleValidator: verifies module schema/consistency (referential integrity, quest graph sanity, entity completeness) and cross-checks content references.
  - ModuleIndexer: builds derived indices (quest dependency graph, entity cross-refs) and seeds vector memory collections (module_docs, entity_cards, quest_nodes, lore_canon) with readable cards/docs (not mechanics).
  - SeedState: initializes Living World State (initial scene, participants, clock/weather, inventory), appends SessionStarted/CharacterCreated events, and produces initial snapshots.
  - Persist: saves Adventure Module + indices and Living World State via GameStateRepository to file.
- Outcome: A ready-to-play game with a tailored Adventure Module and initial state; runtime turns use Handoff orchestration.

## Generated Content (v1)
- Philosophy: Mechanics in code (RulesTools templates); content as data generated at setup.
- Binding: Every class/species/ability/progression element references a rules template id and params (e.g., AttackVsDefense + "1d6 + DEX.mod").
- Freeform character creation: If the player freeforms, auto-map to closest archetype or create an AdHocArchetype with a bounded power budget and select abilities that legally bind to templates.
- Balancing: Assign level-based power budgets; validators enforce dice ranges, bonuses, cooldowns, and status potency.
- Provenance: Save generation seed, prompts, and decisions as ContentProvenance in the module for auditability/replay/regeneration.
- Vector store: Seed only human-readable summaries (entity cards, docs); never store authoritative mechanics; mechanics live in module JSON.

## Console Loop (behavioral)
- On app start:
  - If a save file path is provided or detected: load Adventure Module + Living State and skip setup; enter per-turn Handoff.
  - Otherwise: run Game Setup (Magentic) to produce module + initial state; then enter per-turn Handoff.
- Turn handling:
  - If a PendingAction exists, include it in ContextPack and expect a response to resume the requesting agent.
  - Otherwise, for each user input: invoke the Handoff orchestration with that input; await DomainResult.
  - On needs_player_input: persist PendingAction and return playerPrompt to the user; do not commit.
  - On completed: commit once via WorldAgent; print FinalNarrative. Do not persist chat transcript; only events/state/facts persist.

## Player Dice Roll Subroutine
- Agent prompt shape (dice request):
  - type: "dice_roll", data: { formula: "1d20+DEX.mod+prof", options: ["roll","respond"], label?: "Pick lock" }
- CLI presents choices:
  - 1) Roll Dice → client rolls locally, displays result, returns { choice: "roll", result: number }.
  - 2) Respond another way → capture free text, returns { choice: "respond", text: string }.
- Resumption:
  - Next input is packaged with the PendingAction; the requesting agent consumes the payload and continues deterministically.
- Determinism:
  - Player rolls are client-provided; all other randomness uses the seeded RNG.
- Batching:
  - When multiple trivial prompts are required, aggregate them into a single playerPrompt with stable ids to minimize round trips.

## Observability
- Turn trace: spans per agent step with tool timings and token usage.
- Artifacts: Persist ContextPack snapshot and final StateDelta with commit result (for debugging/audits).
- Metrics: success rate, average tokens/turn, tool error rate, correction loops per turn.

## Configuration
- Agent registry and tool allowlists (per agent) via options.
- Token budgets (ContextPack, prompts), retrieval K, summarization thresholds.
- RNG seeding policy and idempotency window.

## Resolved Decisions
- Handoff initiation: Always start with ContextBroker to construct ContextPack; then hand off to GuardAgent; typically proceed to PlotDirector next. PlotDirector may hand back to ContextBroker for additional details before initiating a domain agent.
- Input guarding: Handled by GuardAgent. It rejects impossible/cheating inputs with in‑world narrative, or proposes controlled improvisations (optionally dice‑gated) as ModulePatch requests.
- Generated content (v1): Game Setup generates classes/species/abilities/progression as data in the Adventure Module; all bind to RulesTools templates; vector memory stores only readable cards/docs, not mechanics.
- Dialogue storage: Keep verbatim NPC dialogue in vector memory (npc_dialogue). Keep short dialogue recaps in the event log for fast access. ContextBroker can fetch exact quotes when needed.
- Fact promotion: Clarified above; MemoryCurator promotes durable, quest-relevant facts with citations; avoids ephemeral chatter.
- Cross-change stability: Not a concern in current scope; no affordances made.

## Remaining Questions
- Recap length defaults (M utterances) and retrieval K values per collection.
- Exact budget ceilings (handoffs per turn, max tool calls per agent) and timeout behavior.

## Implementation Checklist (initial)
1) Define C# models for Adventure Module (adventure, locations, npcs, quests, arcs), ContextPack, GuardDecision, PlotDirective, DomainResult, PendingAction, ModulePatch, StateDelta, Event, Fact.
2) Implement repositories/loaders for Adventure Module and indexes (quest dependency graph, entity cross‑refs).
3) Implement WorldAgent (single writer) and repository methods for atomic commits + event append.
4) Implement RulesTools with deterministic RNG.
5) Implement ContextBroker tools (state reads, retrieval, summarization) and prompt.
6) Implement GuardAgent (guard) and prompt; produce GuardDecision and optional ModulePatch/player prompts.
7) Implement PlotDirector agent (advisory) and prompt; produce PlotDirective and initial domain handoff target.
8) Implement DialogueAgent as first domain agent; commit via WorldAgent.
9) Add Handoff orchestration wiring; implement directed handoffs via DomainResult.nextAgent.
10) Add PendingAction repository and player‑prompt plumbing.
11) Create vector store collections (module_docs, entity_cards, quest_nodes, lore_canon, npc_dialogue, session_facts) and tagging strategy.
12) Integrate DI wiring and options; add unit tests for commit idempotency, guard decisions (reject/improv), handoff chaining, PendingAction resume, and context assembly.

## Appendix: Examples

### Turn — Happy Path (Dialogue)
- Input: “I ask the tavern keeper about rumors.”
- ContextBroker: builds ContextPack (scene=tavern, participants=player+barkeep, recap=last 3 lines, facts=local rumors).
- PlotDirector: proposes target objective (advance tavern rumor quest), highlights barkeep, and initiates handoff to DialogueAgent.
- DialogueAgent: drafts barkeep reply; uses RulesTools for a persuasion check; gets outcome; proposes StateDelta (relationship + rumor revealed event).
- WorldAgent: applies delta; appends DialogueSpoken, RuleCheck, OutcomeApplied events; advances time +2 minutes.
- MemoryCurator: promotes “Rumor: bandits on the north road” with citation to the DialogueSpoken event.
- Output: Narrative line(s) to player; no chat context retained.

### Turn — Needs Player Input (Dice Roll)
- Input: “I try to pick the lock quietly.”
- ContextBroker: builds ContextPack for current scene and door.
- GuardAgent: status=valid.
- PlotDirector: selects ExplorationAgent.
- ExplorationAgent: requests lockpicking check; returns DomainResult.needs_player_input with playerPrompt { type: "dice_roll", data: { formula: "d20 + DEX.mod + proficiency" } } and pendingActionId.
- Orchestrator: persists PendingAction; returns prompt to player; no commit.
- Next turn input: “My roll is 15 (DEX+prof = +3).” ContextBroker includes PendingAction.
- ExplorationAgent: resolves outcome with deterministic RNG and provided roll; proposes StateDelta (door unlocked, time +1 minute, noise=low).
- WorldAgent: commits; MemoryCurator promotes fact “Back door lock is simple.”
- Output: Narrative of successful (or failed) unlock.

### Turn — Guard Rejects Cheating Input
- Input: “I find a million gold on the ground.”
- ContextBroker: builds minimal context.
- GuardAgent: status=reject; narrative explains in‑world why this is impossible here (e.g., “the alley is empty and swept daily”).
- Orchestrator: returns narrative; no commit.

### Turn — Improv: Generate Missing Blacksmith
- Input: “I go find the local blacksmith.”
- ContextBroker: scene is a small village with no blacksmith.
- GuardAgent: status=improv; proposes ModulePatch to AddNPC (blacksmith) + AddShop (forge) with location on the main street; optionally returns playerPrompt { type: "dice_roll" } to gate existence.
- If gated and roll succeeds next turn: ModulePatch included in aggregated StateDelta.
- PlotDirector: selects ExplorationAgent.
- ExplorationAgent: describes directions to the forge; proposes StateDelta (SceneChange → forge, time +5 minutes).
- WorldAgent: commits StateDelta and applies ModulePatched event; MemoryCurator adds session facts about the new forge and owner.
- Output: Narrative guiding the player to the newly added blacksmith.

### Turn — Combat With Dialogue Handoff
- Input: “I taunt the bandit during my attack.”
- GuardAgent: status=valid.
- PlotDirector: selects CombatAgent.
- CombatAgent: resolves attack roll; then hands off to DialogueAgent for a one‑line taunt; DialogueAgent emits DialogueSpoken event; handoff returns to CombatAgent for damage/effects.
- WorldAgent: commits aggregated StateDelta with CombatTurn, DialogueSpoken, OutcomeApplied; MemoryCurator promotes fact “Bandit is rattled.”
- Output: Short combat narration with taunt included.

### Game Setup — New Game
- Trigger: app start with no save.
- CharacterBuilder: collects name, archetype/class or freeform choices; writes provisional Character.
- SettingSelector: captures setting and WorldGenOptions (tone, difficulty, themes).
- ContentBootstrapper → ContentGenerator → ContentValidator: generate classes/species/abilities/progression as data bound to RulesTools; validate power budgets.
- ModuleGenerator → ModuleValidator → ModuleIndexer: build canonical Adventure Module + indices; seed vector store with readable cards/docs.
- SeedState: initialize initial scene, participants, clock/weather; append SessionStarted/CharacterCreated events.
- Persist: save module + state to file; enter per‑turn Handoff runtime.

### Game Setup — Load Existing Save
- Trigger: app start with a save path or detected save.
- SetupDetector: short‑circuits setup; loads Adventure Module + Living World State from file.
- Enter per‑turn Handoff runtime immediately.

## Future: Pluggable Rulesets (V2)
- Scope: Not part of current implementation; documented here for future work.
- Goal: Support multiple RPG systems (e.g., D&D 5e, custom Pokémon, Cyberpunk, Pathfinder) without per‑system C# code paths.

- Design Overview
  - Rules Pack (data): JSON manifest defines attributes/skills, actions/effects, conditions, damage types, dice/resolution templates (e.g., d20, d100, dice pools), and helper functions.
  - Rules Engine (code): Sandboxed evaluator that executes templates against ContextPack + module annotations with deterministic RNG; no reflection/dynamic code loading.
  - Rules Annotations (module‑bound): Generated at world creation by a Module Adapter agent to bind the Rules Pack to the Adventure Module (stat blocks, objective gates, encounter seeds, item properties).
  - Rules Tools (plugins): Generic tools (check, opposed_check, attack, damage, saving_throw, apply_effects) that call the Rules Engine with rulesetId, annotations, context, and seed.

- Authoring Workflow
  - Choose rulesetId for a new game.
  - Module Adapter agent ingests the Rules Pack and the authored module; emits validated Rules Annotations (NPC stat blocks, gates for objectives, encounter definitions).
  - Store annotations alongside the Adventure Module (canonical, patchable).

- Runtime Usage
  - Domain agents propose intents (e.g., Persuasion check, Melee attack).
  - RulesTools evaluate via Rules Engine → return outcomes and Effects (hp deltas, conditions, relationship shifts, item transfers).
  - WorldAgent applies Effects atomically as StateDelta; PlotDirector remains rules‑agnostic and only reads progress flags/quest nodes.

- Contracts (sketch)
  - IRulesPackRepository: Load/validate rules pack JSON by id.
  - IRulesEngine: EvaluateCheckAsync / ResolveActionAsync → normalized outcomes + Effects; consumes deterministic RNG.
  - IModuleAnnotationsRepository: Load per‑module rules annotations (stat blocks, gates, encounters, items).
  - Effects (normalized): hpDelta, add/removeCondition, relationshipDelta, transferItem, updateObjective, timeDelta.

- Expressions & RNG
  - Dice: "1d20", "3d6+2", dice pools; attribute/skill refs (actor.STR.mod, target.AC), annotation refs (move.Power).
  - All randomness uses a per‑turn seed (hash of turnId + input + sceneId) for determinism.

- Vector Store Usage
  - Store readable rules docs and entity/quest cards for retrieval (tagged by rulesetId), not authoritative numbers/mechanics.
  - Store session_facts derived from events with citations; computations come from Rules Pack + annotations.

- Safety & Validation
  - Strict JSON schema validation for Rules Packs and Annotations.
  - Sandboxed evaluator with whitelisted functions/operators.
  - Single writer model preserved (WorldAgent commits only).

- Risks / Mitigations
  - System complexity: start with core templates (checks, opposed checks, attack/defense, damage, saves) and extend incrementally.
  - Annotation quality: add a Reviewer step or tests to simulate common actions against the pack before enabling a module.


