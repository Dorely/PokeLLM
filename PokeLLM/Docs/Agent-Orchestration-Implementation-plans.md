# Agent Orchestration Implementation TODO (from gtp5)

Goal: Fully replace legacy PhaseService / phase plugins orchestration with Semantic Kernel (SK) 1.61.x multi?agent architecture using the refined agent taxonomy below. No backward compatibility or migration safety required; perform a clean architectural replacement.

---
## Updated Agent Taxonomy (Requested Pattern)
| Agent | Role & Responsibilities | Activation | Tools / Integrations | Separation Rationale |
|-------|-------------------------|------------|----------------------|----------------------|
| Setup Agent | Guides player through selecting setting, character, and backstory; generates initial structured Adventure Module JSON (quests, NPC seeds, regions, hooks). | One?time at new game start | LLM prompt for module synthesis; input parsing (intent classification); JSON schema validator | Isolates deterministic world bootstrap; prevents mid?game drift |
| GM Supervisor Agent | Central coordinator: receives raw player input, classifies intent, routes to subordinate agents, enforces rules & module consistency, merges outputs into final turn narration. | Every player turn | Routing/classifier function; state query/validation functions; intent-to-agent dispatch table | Single orchestration brain reduces cross?agent conflicts |
| Narrator Agent | Produces immersive descriptive prose (scenes, sensory details, dialogue tone) grounded in canonical state and memory. Avoids mechanical calculations. | Invoked by Supervisor for non?mechanical / narrative enrichment phases of a turn | RAG memory (past events); style / tone prompt; summarization & compression tools | Separates creative variability from mechanical correctness |
| Mechanics Agent | Resolves rules outcomes: combat rolls, inventory changes, skill checks, status effects, XP, level/evolution; applies validated state mutations. | Invoked when routed intent classified as mechanical (attack, use item, check skill, rest) | Dice/random provider; rules engine functions; state mutation APIs (CQRS); deterministic RNG seed control | Keeps authoritative logic deterministic & auditable |
| NPC / World Agent (v2) | Simulates NPC personalities, schedules, world events, dynamic quest evolution & environmental changes. | Invoked on NPC interaction intents or periodic world ticks | Personality prompt templates; event generator; world state diff generator; optional background task runner | Modular world agency layer—can be added after core loop is stable |

### Legacy-to-New Mapping
| Legacy Concept | New Agent Ownership / Strategy |
|----------------|--------------------------------|
| GameSetupPhasePlugin / StartNewGame | Setup Agent |
| GameMasterAgent (narrative + delegation) | Split: GM Supervisor (delegation) + Narrator (pure prose) |
| ExplorationAgent (player input router) | GM Supervisor intent classification + delegated Narrator/Mechanics |
| StateManagerAgent | Mechanics Agent (state mutation) + underlying state repository |
| CombatAgent | Mechanics Agent (combat resolution module) |
| CharacterDevelopmentAgent | Mechanics Agent (level/evolution submodule) |
| WorldAgent / EventAgent | NPC / World Agent (v2) |
| MemoryAgent | Replace with shared memory components (RAG + compression) accessible to Supervisor + Narrator (no standalone agent unless needed later) |
| NPCAgent(s) | NPC / World Agent persona subcontexts (v2) |

---
## References (Latest SK Agent Docs)
Primary Concepts & APIs:
- Core agent abstractions & AgentGroupChat: https://github.com/microsoft/semantic-kernel/blob/main/docs/decisions/0032-agents.md
- Declarative agent schema & factories: https://github.com/microsoft/semantic-kernel/blob/main/docs/decisions/0070-declarative-agent-schema.md
- Agents with memory (RAG & components): https://github.com/microsoft/semantic-kernel/blob/main/docs/decisions/0072-agents-with-memory.md
- Agent chat serialization: https://github.com/microsoft/semantic-kernel/blob/main/docs/decisions/0048-agent-chat-serialization.md
- C# agent samples (OpenAI/Azure/Bedrock, telemetry): https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/GettingStartedWithAgents

---
## High-Level Milestones (Revised)
1. Foundation & Infra
2. Core Agent Set (Setup, GM Supervisor, Narrator, Mechanics)
3. Orchestration Loop (Supervisor-centered Group Chat)
4. Legacy Purge
5. Memory & State Integration (shared components + Mechanics authority)
6. NPC / World Agent (v2) + Background World Simulation
7. Advanced Patterns (handoff, strategies, world ticks)
8. Telemetry, Observability, Serialization
9. Testing & Load
10. Extensibility & Declarative Config

---
## Detailed TODO Checklist (Revised)
### 1. Foundation & Infrastructure
[ ] NuGet refs: Microsoft.SemanticKernel >=1.61.0; Connectors.OpenAI
[ ] (Optional explicit) Agents.* packages if not included transitively
[ ] IGameAgent abstraction (Id, Name, Instructions, InvokeAsync returning IAsyncEnumerable<AgentResponseItem<ChatMessageContent>>)
[ ] GameAgentManager (register, get, metrics, disposal)
[ ] GameContext (immutable snapshot + mutation events)
[ ] State store + event log (append-only) separate from agent classes
[ ] GameKernelBuilder (model config, plugin loading, memory services, logging)
[ ] GameIntent enum + classifier function (LLM or simple rules) used by Supervisor
[ ] Structured Adventure Module JSON schema + validator
[ ] GameAgentMessage record (agent id, content, intent?, metadata, timestamp)
[ ] RandomNumberService (deterministic seed injection for Mechanics Agent)

### 2. Core Agents
[ ] SetupAgent: prompt templates, collects player inputs, produces AdventureModule JSON persisted to state store
[ ] GMSupervisorAgent: instruction includes routing rules + consistency enforcement steps
[ ] NarratorAgent: instruction focuses on evocative, concise, canon-preserving prose; uses memory component
[ ] MechanicsAgent: instruction mandates deterministic calculations; exposes KernelFunctions for rule ops (ResolveAttack, ApplyDamage, GrantXP, PerformSkillCheck)
[ ] Register kernel plugins (RulesPlugin, ModulePlugin, NarrativeStylePlugin)
[ ] Wire agents in GameAgentManager with shared Kernel and memory components

### 3. Orchestration Loop
[ ] Initial game start: Run SetupAgent once; store module; Supervisor loads summary into context
[ ] AgentGroupChat composition (Supervisor, Narrator, Mechanics; Setup only during bootstrap)
[ ] Player input pipeline: user message -> Supervisor classify intent
[ ] Routing: if mechanical -> invoke Mechanics then (optional) Narrator for flavor; else Narrator only
[ ] Supervisor final aggregation step -> output streamed to UI
[ ] Termination strategy stub (max turns or Exit intent)

### 4. Legacy Purge
[ ] Remove PhaseService, IPhaseService
[ ] Remove *PhasePlugin.cs (GameSetup, Exploration, WorldGeneration, etc.)
[ ] Remove UnifiedContextService (replaced by memory components + state store)
[ ] Clean Program.cs to new bootstrap path
[ ] Delete reflection plugin loading logic

### 5. Memory & State Integration
[ ] Implement vector store wrapper (reuse QdrantVectorStoreService) for narrative memory + fact memory
[ ] Memory components: UserFactsMemory, EventSummaryMemory
[ ] Compression job (summarize older turns -> condensed context) invoked periodically
[ ] MechanicsAgent: only path for state mutation (guarded functions + validation)
[ ] AdventureModule persisted; Supervisor loads read-only copy per turn
[ ] Snapshot & restore (save game) using serialization of state + chat history

### 6. NPC / World Agent (v2)
[ ] WorldSimulationAgent (optional) providing: GenerateNPCResponse, AdvanceWorldTick, SpawnDynamicEvent
[ ] Persona profile store (seed from AdventureModule NPC seeds)
[ ] Background tick scheduler (BackgroundAgentManager) invoking world evolution
[ ] Integration: Supervisor may request world diff suggestions -> Mechanics applies validated subset

### 7. Advanced Patterns
[ ] AgentHandoffManager: Supervisor <-> Mechanics micro chat for complex multi-step resolution (e.g., multi-attack round)
[ ] SelectionStrategy customization: Supervisor always first; chooses next based on intent
[ ] TerminationStrategy: stop after final aggregated response or EndGame intent
[ ] Structured output enforcement: JSON schema for Mechanics results & Narrator narrative wrapper
[ ] Error fallback: if Narrator generation fails -> Supervisor minimal mechanical summary

### 8. Telemetry, Observability, Serialization
[ ] Logging: per-agent correlation id + intent + latency + token counts
[ ] Metrics: counters (turns, mechanical resolutions, narration length avg)
[ ] Chat serialization (AgentChatSerializer) after each turn; rolling window retention
[ ] Replay utility: reconstruct last N turns for debugging
[ ] Failure diagnostics: capture raw LLM prompt & response on validation failures

### 9. Testing
[ ] Unit: Intent classifier accuracy (golden set)
[ ] Unit: Mechanics deterministic outcomes with fixed RNG seed
[ ] Unit: AdventureModule JSON validation (schema test)
[ ] Unit: Narrator avoids altering numeric facts (assert unchanged state)
[ ] Integration: Full start -> battle -> loot -> level -> narration flow
[ ] Integration: Memory recall (Narrator references earlier fact after compression)
[ ] Load: Parallel game sessions stress (N supervisors) throughput & latency
[ ] Serialization: Save/restore mid-combat consistency

### 10. Extensibility & Declarative Config
[ ] Declarative YAML definitions for agents (optional) via AggregatorAgentFactory
[ ] External plugin folder hot-reload (future)
[ ] Versioned AdventureModule schema (v1 baseline)
[ ] Developer docs: adding a new mechanical rule or world persona

---
## File / Code Actions Summary (Revised)
(Delete = permanent removal; Add = new file)
[ ] Delete: PokeLLM/Orchestration/PhaseService.cs
[ ] Delete: PokeLLM/Plugins/*Phase*.cs
[ ] Add: PokeLLM/Agents/SetupAgent.cs
[ ] Add: PokeLLM/Agents/GMSupervisorAgent.cs
[ ] Add: PokeLLM/Agents/NarratorAgent.cs
[ ] Add: PokeLLM/Agents/MechanicsAgent.cs
[ ] (v2) Add: PokeLLM/Agents/WorldSimulationAgent.cs
[ ] Add: PokeLLM/Agents/GameAgentManager.cs
[ ] Add: PokeLLM/Agents/GameContext.cs
[ ] Add: PokeLLM/Agents/AgentHandoffManager.cs
[ ] Add: PokeLLM/Agents/BackgroundAgentManager.cs
[ ] Add: PokeLLM/State/AdventureModule.cs (schema classes)
[ ] Add: PokeLLM/State/EventLog.cs
[ ] Add: PokeLLM/Memory/MemoryComponents.cs (user facts, summaries)
[ ] Modify: Program.cs (new bootstrap)
[ ] Add: PokeLLM/Controllers/GameController.cs
[ ] Remove: UnifiedContextService & references

---
## Implementation Order (Aggressive Wipe Strategy)
1. Scaffold infra (kernel builder, context, state store, manager, intents)
2. Implement SetupAgent -> produce AdventureModule
3. Implement GM Supervisor + Mechanics + Narrator minimal loop
4. Purge legacy phase system & wire new Program.cs
5. Add memory components + compression
6. Expand Mechanics rules + narrative enrichment
7. Add NPC / World simulation (optional v2)
8. Add telemetry, serialization & replay
9. Testing suite expansion & load tests
10. Declarative configs & docs

---
## Non-Goals / Explicit Exclusions (Unchanged)
- No transitional shims or dual-running systems
- No backward compatibility layers
- No preservation of phase plugin abstractions
- No incremental migration gating

---
## Acceptance Criteria (Revised)
[ ] Phase system code removed
[ ] New agents (Setup, Supervisor, Narrator, Mechanics) implemented & functional
[ ] AdventureModule JSON generated & validated at game start
[ ] All state mutations funneled through MechanicsAgent validated functions
[ ] Supervisor routes intents with >90% classifier accuracy on test set
[ ] Narrator adds prose without mutating canonical facts
[ ] Memory components improve contextual continuity (measurable in test)
[ ] Chat serialization & restore works mid-session
[ ] Optional: WorldSimulationAgent integrated (v2)

---
## Risk Notes & Mitigations (Updated)
- Misrouting intents: maintain & test labeled training examples; fallback to manual rule overrides
- Narrative hallucination of mechanics: segregate mechanical data; Narrator receives readonly snapshots
- State drift via parallel updates: single-thread MechanicsAgent mutations + event log
- RNG reproducibility: seed captured per turn in event log
- Memory bloat: periodic compression & vector relevance filtering
- Supervisor bottleneck: measure latency; consider lightweight routing micro-strategy function

---
Execute aggressively; optimize later.
