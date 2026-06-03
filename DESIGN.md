# Kipo Simulation Engine — Design Document

## 1. Overview

Kipo is an ARPG simulation engine built on Mibo.Raylib's Elmish architecture. This document defines the complete architecture: model structure, system pipeline, tick lifecycle, event cascade model, spatial system, and derived state caching.

This is a ground-up rewrite informed by the lessons of the original Pomo/MonoGame implementation. It is not a 1-1 port.

### Core Principles

- **All models are classes or structs, never records.** Mutable in-place mutation, no copy semantics.
- **Systems mutate in-place.** Each system receives the World, mutates the sub-models it owns, returns `struct (World * Cmd<Msg>)`.
- **Cmds are the only inter-system communication.** No EventBus. No shared mutable queues. Simulation Cmds cascade to completion within the same step. Visual Cmds are deferred to the next frame.
- **Semantic grouping.** World state is partitioned into domain-specific sub-models. Systems only access the sub-models they need.
- **Struct DUs for messages.** Small, stack-allocated, grouped by domain for efficient dispatch.
- **Derived caches are static mutable classes.** Refreshed once per tick at the end of the pipeline. Read by all systems in the next tick.
- **Hex grid for everything.** Block map, spatial indexing, pathfinding — all use Mibo's `HexGrid3D`.
- **Fixed timestep.** Deterministic simulation at 60Hz via `Program.withFixedStep`.
- **BCL data structures only.** No FSharp.Data.Adaptive types. `Dictionary<K,V>`, `ResizeArray<T>`, `HashSet<T>`, arrays. No `HashMap`, `IndexList`, `cmap`, `cval`.
- **Exhaustive pattern matching on DUs.** No `| _ ->` wildcard discards on discriminated unions. Always match every case explicitly. Exception: `ValueOption`/`Option` where `ValueNone`/`None` is a single semantic case.

### Data Structure Mapping

| Old (FDA/Pomo) | New (BCL) | Notes |
|---|---|---|
| `HashMap<K,V>` (FSharp.Data.Adaptive) | `Dictionary<K,V>` (System.Collections.Generic) | Mutable hash map |
| `IndexList<T>` (FSharp.Data.Adaptive) | `ResizeArray<T>` (System.Collections.Generic.List<T>) | F# alias for BCL List<T> |
| `HashSet<T>` (FSharp.Data.Adaptive) | `HashSet<T>` (System.Collections.Generic) | Mutable hash set |
| `cmap<K,V>` (adaptive) | `Dictionary<K,V>` | Mutable dictionary, no reactive graph |
| `cval<T>` (adaptive) | mutable field on class | `member val X = v with get, set` |
| `amap<K,V>` (adaptive projection) | `Dictionary<K,V>` (cached) | Rebuilt at flush, read by next step |
| `aval<T>` (adaptive value) | computed on read or cached field | No subscription graph |

### ResizeArray Module

The BCL `ResizeArray<T>` (`System.Collections.Generic.List<T>`) has no efficient `choose`, `map`, or `filter` with `ValueOption`. The standard F# `ResizeArray` module allocates a new `ResizeArray` for each operation. We need a module that operates in-place where possible and uses `ValueOption` to avoid heap allocation.

All functions are `inline`. All callbacks/predicates use `[<InlineIfLambda>]` for zero-overhead lambda composition, following Mibo.Raylib's `System.fs` pattern.

#### Naming Convention

| Suffix | Meaning |
|---|---|
| `V` | Uses `ValueOption` instead of `Option` (no heap alloc) |
| `InPlace` | Mutates the input `ResizeArray` instead of allocating a new one |
| `VInPlace` | Both: `ValueOption` + in-place mutation |

#### Functions

All functions are `inline`. Lambda parameters use `[<InlineIfLambda>]` to ensure zero-overhead composition — the JIT inlines the lambda body at the call site.

| Function | Signature | Behavior |
|---|---|---|
| `chooseV` | `inline ([<InlineIfLambda>] chooser: 'T -> 'U voption) -> ResizeArray<'T> -> ResizeArray<'U>` | Filters + maps. Allocates new `ResizeArray`. |
| `chooseVInPlace` | `inline ([<InlineIfLambda>] chooser: 'T -> 'U voption) -> ResizeArray<'T> -> unit` | Filters + maps in-place. Compact-pass removes rejected elements. |
| `filterV` | `inline ([<InlineIfLambda>] predicate: 'T -> bool) -> ResizeArray<'T> -> ResizeArray<'T>` | Filters. Allocates new `ResizeArray`. |
| `filterVInPlace` | `inline ([<InlineIfLambda>] predicate: 'T -> bool) -> ResizeArray<'T> -> unit` | Filters in-place. Compact-pass. |
| `mapV` | `inline ([<InlineIfLambda>] mapper: 'T -> 'U) -> ResizeArray<'T> -> ResizeArray<'U>` | Maps. Allocates new `ResizeArray`. |
| `mapVInPlace` | `inline ([<InlineIfLambda>] mapper: 'T -> 'U) -> ResizeArray<'T> -> unit` | Maps in-place (same type only). Overwrites elements. |
| `tryFindV` | `inline ([<InlineIfLambda>] predicate: 'T -> bool) -> ResizeArray<'T> -> 'T voption` | Linear scan. Returns `ValueOption`. |
| `existsV` | `inline ([<InlineIfLambda>] predicate: 'T -> bool) -> ResizeArray<'T> -> bool` | Short-circuit scan. |
| `iterV` | `inline ([<InlineIfLambda>] action: 'T -> unit) -> ResizeArray<'T> -> unit` | Iterates all elements. |
| `foldV` | `inline ([<InlineIfLambda>] folder: 'S -> 'T -> 'S) -> 'S -> ResizeArray<'T> -> 'S` | Left fold. |
| `collectV` | `inline ([<InlineIfLambda>] mapper: 'T -> 'U[]) -> ResizeArray<'T> -> ResizeArray<'U>` | Flat map. Allocates new `ResizeArray`. |

#### In-Place Compact Pattern

`chooseVInPlace`, `filterVInPlace` use a two-pointer compact pass:

```
Read index  → scans all elements
Write index → advances only for kept elements

After scan: RemoveRange(writeIndex, Count - writeIndex) to trim tail
```

This is O(n) with zero allocation beyond the existing `ResizeArray` storage.

#### Example Usage

```fsharp
// Filter alive entities in-place (no allocation)
world.Combat.LiveEntities
|> ResizeArray.filterVInPlace (fun id ->
    match world.Combat.Resources |> Dictionary.tryFindV id with
    | ValueSome r -> r.Status = Alive
    | ValueNone -> false)

// Choose active effects that have expired (in-place compact)
world.Combat.ActiveEffects[entityId]
|> ResizeArray.chooseVInPlace (fun effect ->
    if isExpired world.Time.TotalGameTime effect then
        ValueNone  // remove
    else
        ValueSome effect)  // keep

// Find first entity in range (short-circuit, ValueOption return)
let target =
    candidates
    |> ResizeArray.tryFindV (fun id ->
        match world.Physics.Positions[scenarioId] |> Dictionary.tryFindV id with
        | ValueSome pos -> WorldPosition.distance pos center <= radius
        | ValueNone -> false)
```

### Dictionary Module

BCL `Dictionary<K,V>.TryGetValue` returns `bool * 'V` — forces pattern matching on the tuple. We need a module that wraps common dictionary operations with `ValueOption` returns, avoiding the `match dict.TryGetValue(key) with | true, v -> ValueSome v | false, _ -> ValueNone` boilerplate.

All functions are `inline`. Lambda parameters use `[<InlineIfLambda>]` for zero-overhead composition.

#### Functions

| Function | Signature | Behavior |
|---|---|---|
| `tryFindV` | `inline key -> Dictionary<K,V> -> V voption` | Wraps `TryGetValue`. Returns `ValueOption`. |
| `findV` | `inline key -> Dictionary<K,V> -> V` | Like indexer but throws `KeyNotFoundException` with key info. |
| `containsKey` | `inline key -> Dictionary<K,V> -> bool` | Alias for `ContainsKey`. Reads better in pipelines. |
| `removeWhere` | `inline ([<InlineIfLambda>] predicate: K -> V -> bool) -> Dictionary<K,V> -> int` | Remove all matching entries. Returns count removed. |
| `iterKV` | `inline ([<InlineIfLambda>] action: K -> V -> unit) -> Dictionary<K,V> -> unit` | Iterate key-value pairs. |
| `foldKV` | `inline ([<InlineIfLambda>] folder: 'S -> K -> V -> 'S) -> 'S -> Dictionary<K,V> -> 'S` | Fold over key-value pairs. |
| `toArray` | `inline Dictionary<K,V> -> struct(K * V)[]` | Snapshot to struct tuple array. |
| `countWhere` | `inline ([<InlineIfLambda>] predicate: K -> V -> bool) -> Dictionary<K,V> -> int` | Count matching entries. |
| `tryFindOrDefault` | `inline key -> defaultValue: V -> Dictionary<K,V> -> V` | Returns value or default. Avoids ValueOption chain for simple fallbacks. |

#### Example Usage

```fsharp
// Before (verbose):
let speed =
    match world.Combat.DerivedStats.TryGetValue(entityId) with
    | true, stats -> float32 stats.MS
    | false, _ -> 100.0f

// After (concise):
let speed =
    world.Combat.DerivedStats
    |> Dictionary.tryFindV entityId
    |> ValueOption.map (fun s -> float32 s.MS)
    |> ValueOption.defaultValue 100.0f

// Even simpler with tryFindOrDefault:
let speed =
    world.Combat.DerivedStats
    |> Dictionary.tryFindOrDefault entityId defaultStats
    |> fun s -> float32 s.MS

// Pipeline-friendly chained lookups:
entityId
|> Dictionary.tryFindV world.Combat.DerivedStats
|> ValueOption.bind (fun stats ->
    skillId
    |> Dictionary.tryFindV stats.Cooldowns)

// Remove dead entities from all dictionaries:
world.Entities.Positions
|> Dictionary.removeWhere (fun id _ ->
    not (world.Combat.LiveEntities.Contains id))

// Count alive entities in a scenario:
let aliveCount =
    world.Entities.Positions
    |> Dictionary.countWhere (fun id _ ->
        world.Combat.LiveEntities.Contains id)
```

---

## 2. Tick Lifecycle

### Frame Structure

Every frame follows this sequence, managed by Mibo's runtime. A frame may contain zero or more simulation steps.

```
Frame N
│
├─ 1. DEFERRED EFFECTS (from Frame N-1's Cmd.deferNextFrame)
│   Mibo runtime executes all deferred effects before Tick.
│   These are visual-only: notifications, VFX, sound.
│   They dispatch TopLevelMsg into the message queue.
│
├─ 2. FIXED STEP MESSAGES
│   Mibo runtime enqueues zero or more FixedStep(dt) messages
│   based on accumulated time. Each triggers the full simulation
│   pipeline (systems + queue drain + cache refresh).
│   Simulation cascades complete within each step.
│
├─ 3. TICK MESSAGE
│   Mibo runtime enqueues Tick(gt) for per-frame work
│   (UI updates, camera interpolation, diagnostics).
│
├─ 4. MESSAGE QUEUE DRAIN
│   Mibo runtime processes all pending messages in order:
│   ├─ Deferred msgs (from step 1) → routed to system.handleMsg
│   ├─ FixedStep msgs (from step 2) → triggers system pipeline + drain
│   └─ Tick msg (from step 3) → per-frame work
│
├─ 5. RENDER
│   Read-only access to World. Uses predicted positions from
│   PhysicsCache, derived stats from DerivedStatsCache.
│
Frame N ends
```

### FixedStep Pipeline Detail

When `FixedStep(dt)` is processed, the following pipeline executes. After the pipeline, the message queue drains — simulation cascades resolve within the same step.

```
FixedStep(dt) received
│
│  Systems read from caches built at the END of Frame N-1:
│    world.Physics.Positions[scenarioId]    — predicted positions
│    world.Physics.SpatialGrids[scenarioId] — hex spatial index
│    world.Combat.DerivedStats              — cached derived stats
│    world.Combat.CombatStatuses            — stun/silence per entity
│    world.Combat.LiveEntities              — set of alive entity IDs
│
├─ System.pipeMutable (InputSystem.update dt)
│   Reads:  hardware keyboard/mouse state
│   Mutates: world.Input.RawInputs, world.Input.ActionStates
│   Cmds:   Cmd.ofMsg (Input ActionStatesChanged)
│
├─ System.pipeMutable (InputMappingSystem.update dt)
│   Reads:  world.Input.RawInputs, world.Input.InputMaps
│   Mutates: world.Input.ActionStates
│
├─ System.pipeMutable (PlayerMovementSystem.update dt)
│   Reads:  world.Input.ActionStates
│           world.Physics.Positions (predicted)
│           world.Combat.DerivedStats[entityId].MS (speed)
│           world.Combat.CombatStatuses[entityId] (stun/root)
│   Mutates: world.Entities.Velocities, world.Movement.States
│   Cmds:   Cmd.ofMsg (Movement (MovementStateChanged ...))
│
├─ System.pipeMutable (UnitMovementSystem.update dt)
│   Reads:  world.Movement.States (non-player entities)
│           world.Physics.Positions (predicted)
│           world.Combat.DerivedStats[entityId].MS
│   Mutates: world.Entities.Velocities, world.Movement.States
│
├─ System.pipeMutable (AbilityActivationSystem.update dt)
│   Reads:  world.Input.ActionStates (pressed actions)
│           world.Input.ActionSets (slot → skill mapping)
│           world.Combat.CombatStatuses (stun/silence)
│           world.Combat.Resources, world.Combat.Cooldowns
│           world.Physics.Positions (predicted, for pending cast)
│   Mutates: world.Combat.InCombatUntil, world.Combat.PendingCasts
│   Cmds:   Cmd.ofMsg (Combat (AbilityIntent ...))
│           Cmd.ofMsg (Input (SlotActivated ...))  — targeting mode (simulation)
│
├─ System.pipeMutable (ResourceManagerSystem.update dt)
│   Reads:  world.Combat.Resources, world.Combat.DerivedStats
│           world.Combat.InCombatUntil, world.Time
│   Mutates: world.Combat.Resources (HP/MP regen)
│   Cmds:   Cmd.deferNextFrame (Cmd.ofMsg (Notification (ResourceRestored ...)))
│
├─ System.pipeMutable (ProjectileSystem.update dt)
│   Reads:  world.Projectiles.Live, world.Physics.Positions
│           world.Combat.LiveEntities, world.Time
│   Mutates: world.Entities.Velocities, world.Entities.Positions
│           world.Projectiles.Live (remove on impact)
│   Cmds:   Cmd.ofMsg (Projectile (ProjectileImpacted ...))
│           Cmd.ofMsg (Spawn (EntityRemoved ...))
│
├─ System.pipeMutable (OrbitalSystem.update dt)
│   Reads:  world.Orbitals.Active, world.Orbitals.Charges
│           world.Physics.Positions, world.Time
│   Mutates: world.Orbitals.Active, world.Orbitals.Charges
│   Cmds:   Cmd.ofMsg (Projectile (ChargeCompleted ...))
│
├─ System.pipeMutable (MovementSystem.update dt)
│   Reads:  world.Physics.Positions (predicted)
│           world.Scenarios (for block map)
│           world.Projectiles.Live (skip projectiles)
│   Mutates: world.Entities.Positions (FINAL after block collision)
│            world.Entities.Rotations (FINAL)
│   Cmds:   Cmd.ofMsg (Combat (EffectApplication ...)) — lava, etc.
│
│   *** This is where predicted positions become final positions. ***
│   *** Block collision resolves here.                         ***
│
├─ System.pipeMutable (CollisionSystem.update dt)
│   Reads:  world.Physics.Positions (predicted)
│           world.Physics.SpatialGrids (hex grid)
│   Cmds:   Cmd.ofMsg (Collision (EntityCollision ...))
│
├─ System.pipeMutable (NotificationSystem.update dt)
│   Reads:  world.VFX.Notifications, world.Time
│   Mutates: world.VFX.Notifications (update positions/life)
│
├─ System.pipeMutable (EffectProcessingSystem.update dt)
│   Reads:  world.Combat.ActiveEffects, world.Time
│   Mutates: world.Combat.ActiveEffects (expire timed effects)
│   Cmds:   Cmd.ofMsg (Combat (EffectDamage ...)) — DoT ticks
│           Cmd.ofMsg (Combat (EffectResource ...)) — RoT ticks
│
├─ System.pipeMutable (SpawnSystem.update dt)
│   Reads:  world.AI.SpawningEntities, world.Time
│   Mutates: world.Entities, world.Combat, world.AI — spawn bundles
│   Cmds:   Cmd.ofMsg (Spawn (SpawnEntity ...)) — respawns
│
├─ System.pipeMutable (AISystem.update dt)
│   Reads:  world.AI.Controllers, world.Physics.Positions
│           world.Combat.Factions, world.Combat.Cooldowns
│   Mutates: world.AI.Controllers (updated memories/state)
│   Cmds:   Cmd.ofMsg (Combat (AbilityIntent ...))
│           Cmd.ofMsg (Movement (MovementTarget ...))
│
├─ System.pipeMutable (AnimationSystem.update dt)
│   Mutates: world.Animation.Animations, world.Animation.Poses
│
├─ System.pipeMutable (ParticleSystem.update dt)
│   Reads:  world.VFX.VisualEffects, world.Time
│   Mutates: world.VFX.VisualEffects (in-memory particle pools)
│
│  ── PHASE 3: REFRESH DERIVED CACHES ──
│
├─ System.pipeMutable (fun _ -> world.Derived.Refresh(world, itemStore))
│   Recomputes DerivedStats for ALL entities.
│   ~2 microseconds for 30 entities — negligible.
│
├─ System.pipeMutable (fun _ -> CombatStatusCache.Refresh(world))
│   Extracts stun/silence from ActiveEffects per entity.
│
├─ System.pipeMutable (fun _ -> LiveEntityCache.Refresh(world))
│   Rebuilds LiveEntities set from Resources where Alive.
│
├─ System.pipeMutable (fun _ -> world.Physics.Refresh(world))
│   For each scenario:
│     Predict positions (stored pos + velocity × dt)
│     Build HexGrid3D spatial index from predicted positions
│     Predict rotations from velocity direction
│   *** Systems in the NEXT step read these predicted values. ***
│
└─ System.finish id
    Returns struct (world, accumulatedCmds)

│  ── PHASE 4: MESSAGE QUEUE DRAIN ──
│
│  Mibo runtime drains the message queue.
│  Simulation Cmds (Cmd.ofMsg) are processed here.
│  Each handler may emit further Cmds — cascades resolve
│  within the same drain pass.
│
│  Visual Cmds (Cmd.deferNextFrame) are NOT processed here.
│  They execute at the start of the next frame.
│
│  After drain: step complete. Render.
```

### Event Cascade Model

The original Kipo uses a ring-buffer EventBus with `FlushToObservable()` at frame start. The `while count > 0` loop processes cascading events within the same flush — if a subscriber publishes a new event during flush, it is picked up in the same flush call. This gave Kipo a **two-frame latency** for full combat cascades: Frame N systems publish events, Frame N+1 flush delivers them and cascades to completion.

With fixed timestep, we can do better. The pipeline commits all mutations in-place before the message queue drains. When a system dispatches `Cmd.ofMsg`, the message is processed **after all pipeline systems have finished**, so every handler sees the fully consistent world state. Cascades complete within the same step.

#### Dispatch Rules

| Category | Dispatch | Latency | Rationale |
|---|---|---|---|
| **Simulation** (combat, movement, spawning, effects) | `Cmd.ofMsg` | Same step | All simulation logic resolves before next step. Deterministic. |
| **Visual-only** (notifications, VFX, sound) | `Cmd.deferNextFrame` | Next frame | No simulation impact. Deferring avoids unnecessary work in the current step's queue drain. |

This gives **1-step latency** for simulation (~16ms at 60Hz fixed step) vs Kipo's 2-frame latency (~33ms). A meaningful improvement for ARPG combat responsiveness.

#### Why This Is Safe

The pipeline runs all systems in fixed order, mutating World in-place. After `System.finish`, the message queue drains. At that point:

1. All positions are final (MovementSystem already applied block collision)
2. All effects are applied (EffectProcessingSystem already processed ticks)
3. All caches are refreshed (DerivedStats, Physics, etc.)

So when CombatSystem handles `AbilityIntent` during the queue drain, it reads a fully consistent world. The resulting `DamageDealt` cascade is also safe — ResourceManager reads final HP, and `EntityDied` is handled by SpawnSystem, all within the same drain.

#### Cascade Example (skill → damage → death → respawn)

```
FixedStep(dt) received
│
├─ Pipeline: all systems run, mutate World in-place
│   AbilityActivationSystem detects skill key pressed
│   → returns Cmd.ofMsg (Combat (AbilityIntent(caster, skillId, target)))
│
├─ Pipeline finishes. Caches refreshed.
│
├─ Message queue drain begins:
│
│   Combat (AbilityIntent) dispatched
│   → CombatSystem.handleMsg processes it
│   → reads world.Derived.TryGet(caster) — fully computed
│   → reads world.Physics.Positions — predicted from last step
│   → calculates damage, applies resource cost
│   → returns Cmd.batch [
│       Cmd.ofMsg (Combat (DamageDealt(target, 42, Fire)))
│       Cmd.ofMsg (Combat (EffectApplied(target, poisonEffect)))
│       Cmd.deferNextFrame (Cmd.ofMsg (Notification (ShowMessage("42", pos, Damage))))
│     ]
│
│   Combat (DamageDealt) dispatched
│   → ResourceManagerSystem.handleMsg subtracts HP
│   → if HP ≤ 0: returns Cmd.ofMsg (Combat (EntityDied(target, scenarioId)))
│
│   Combat (EffectApplied) dispatched
│   → EffectProcessingSystem.handleMsg adds effect to world.Combat.ActiveEffects
│
│   Combat (EntityDied) dispatched
│   → SpawnSystem.handleMsg removes entity from all dictionaries
│   → returns Cmd.deferNextFrame (Cmd.ofMsg (Spawn (SpawnEntity(...))))
│
│   Queue drain complete. All simulation cascades resolved.
│
├─ Render (read-only): sees updated world, floating damage text deferred
│
│
Next frame:
├─ Deferred effects execute:
│   Notification (ShowMessage("42", ...)) → NotificationSystem buffers float text
│   Spawn (SpawnEntity(...)) → SpawnSystem creates new entity
│
├─ Next FixedStep(dt): new entity exists, combat cascade complete
```

**All simulation cascades complete within one step.** Only visual effects (notifications, respawns) are deferred to the next frame.

---

## 3. Model Structure

### 3.1 Top-Level Model

```fsharp
type World() =
    member val Time        : Time           with get, set
    member val Input       : InputState     with get, set
    member val Entities    : EntityState    with get, set
    member val Combat      : CombatState    with get, set
    member val Movement    : MovementState  with get, set
    member val Inventory   : InventoryState with get, set
    member val AI          : AIState        with get, set
    member val Projectiles : ProjectileState with get, set
    member val Orbitals    : OrbitalState   with get, set
    member val Animation   : AnimationState with get, set
    member val VFX         : VFXState       with get, set
    member val Scenarios   : Dictionary<Guid<ScenarioId>, Scenario> with get, set

    // Derived caches — refreshed at end of each tick
    member val Physics : PhysicsCache       with get, set
    member val Derived : DerivedStatsCache  with get, set
```

### 3.2 Semantic Sub-Models

Each sub-model is a class with `member val` properties. Systems only access the sub-models they need.

#### InputState

Owned by: InputSystem, InputMappingSystem
Read by: PlayerMovementSystem, AbilityActivationSystem, UISystem

```fsharp
type InputState() =
    member val RawInputs    : Dictionary<EntityId, RawInputState>
    member val InputMaps    : Dictionary<EntityId, InputMap>
    member val ActionStates : Dictionary<EntityId, Dictionary<GameAction, InputActionState>>
    member val ActionSets   : Dictionary<EntityId, Dictionary<int, Dictionary<GameAction, SlotProcessing>>>
    member val ActiveSets   : Dictionary<EntityId, int>
```

#### EntityState

Owned by: SpawnSystem, MovementSystem
Read by: all systems (positions, existence checks)

```fsharp
type EntityState() =
    member val Exists       : HashSet<EntityId>
    member val Positions    : Dictionary<EntityId, WorldPosition>
    member val Velocities   : Dictionary<EntityId, Vector3>
    member val Rotations    : Dictionary<EntityId, float32>
    member val Factions     : Dictionary<EntityId, HashSet<Faction>>
    member val ModelConfigs : Dictionary<EntityId, string>
    member val Scenarios    : Dictionary<EntityId, Guid<ScenarioId>>
```

#### CombatState

Owned by: CombatSystem, ResourceManagerSystem, EffectProcessingSystem
Read by: AbilityActivationSystem, AISystem, PlayerMovementSystem, all caches

```fsharp
type CombatState() =
    member val Resources      : Dictionary<EntityId, Resource>
    member val BaseStats      : Dictionary<EntityId, BaseStats>
    member val DerivedStats   : Dictionary<EntityId, DerivedStats>     // cache
    member val ActiveEffects  : Dictionary<EntityId, ResizeArray<ActiveEffect>>
    member val Cooldowns      : Dictionary<EntityId, Dictionary<int<SkillId>, TimeSpan>>
    member val InCombatUntil  : Dictionary<EntityId, TimeSpan>
    member val PendingCasts   : Dictionary<EntityId, struct (int<SkillId> * SkillTarget)>
    member val CombatStatuses : Dictionary<EntityId, ResizeArray<CombatStatus>>  // cache
    member val LiveEntities   : HashSet<EntityId>                         // cache
    member val Factions       : Dictionary<EntityId, HashSet<Faction>>
```

#### MovementState

Owned by: PlayerMovementSystem, UnitMovementSystem
Read by: AbilityActivationSystem (for pending cast when idle)

```fsharp
type MovementState() =
    member val States : Dictionary<EntityId, MovementStateKind>
```

#### InventoryState

Owned by: InventorySystem, EquipmentSystem
Read by: DerivedStatsCache (equipment bonuses)

```fsharp
type InventoryState() =
    member val ItemInstances : ConcurrentDictionary<Guid<ItemInstanceId>, ItemInstance>
    member val Inventories   : Dictionary<EntityId, HashSet<Guid<ItemInstanceId>>>
    member val Equipped      : Dictionary<EntityId, Dictionary<Slot, Guid<ItemInstanceId>>>
```

#### AIState

Owned by: AISystem, SpawnSystem
Read by: AISystem (controllers), SpawnSystem (zone management)

```fsharp
type AIState() =
    member val Controllers      : Dictionary<EntityId, AIController>
    member val SpawningEntities : Dictionary<EntityId, struct (SpawnType * WorldPosition * TimeSpan)>
```

#### ProjectileState

Owned by: ProjectileSystem
Read by: MovementSystem (skip projectiles), CollisionSystem

```fsharp
type ProjectileState() =
    member val Live : Dictionary<EntityId, LiveProjectile>
```

#### OrbitalState

Owned by: OrbitalSystem
Read by: CombatSystem (charge completion)

```fsharp
type OrbitalState() =
    member val Active  : Dictionary<EntityId, ActiveOrbital>
    member val Charges : Dictionary<EntityId, ActiveCharge>
```

#### AnimationState (sub-model)

Owned by: AnimationSystem
Read by: RenderOrchestrator

```fsharp
type AnimationState() =
    member val Poses      : Dictionary<EntityId, Dictionary<string, Matrix>>
    member val Animations : Dictionary<EntityId, Animation3DState[]>
```

#### VFXState

Owned by: ParticleSystem, NotificationSystem
Read by: RenderOrchestrator

```fsharp
type VFXState() =
    member val VisualEffects : ResizeArray<VisualEffect>
    member val Notifications : ResizeArray<WorldText>
```

#### Scenario

```fsharp
[<Struct>]
type Scenario = {
    Id       : Guid<ScenarioId>
    BlockMap : HexGrid3D<BlockType>
}
```

---

## 4. Derived Caches

### 4.1 PhysicsCache

Refreshed once per tick at the end of the pipeline. All systems in the next tick read predicted positions from this cache, not from raw `Entities.Positions`.

```fsharp
type PhysicsCache() =
    /// Predicted positions per scenario (stored pos + velocity × dt)
    member val Positions   : Dictionary<Guid<ScenarioId>, Dictionary<EntityId, WorldPosition>>
    /// Hex spatial grid per scenario (built from predicted positions)
    member val SpatialGrids : Dictionary<Guid<ScenarioId>, HexGrid3D<EntityId[]>>
    /// Predicted rotations per scenario (derived from velocity)
    member val Rotations   : Dictionary<Guid<ScenarioId>, Dictionary<EntityId, float32>>

    /// Refresh all caches. Called at end of tick pipeline.
    member this.Refresh(world: World) =
        let dt = float32 world.Time.Delta.TotalSeconds

        for KeyValue(scenarioId, scenario) in world.Scenarios do
            let predictedPos = this.GetOrCreatePositions(scenarioId)
            let predictedRot = this.GetOrCreateRotations(scenarioId)
            let grid = this.GetOrCreateGrid(scenarioId, scenario)

            predictedPos.Clear()
            predictedRot.Clear()
            clearGrid grid

            for KeyValue(entityId, startPos) in world.Entities.Positions do
                match world.Entities.Scenarios |> Dictionary.tryFindV entityId with
                | ValueSome sid when sid = scenarioId ->
                    // Predict position
                    let vel = world.Entities.Velocities |> Dictionary.tryFindV entityId
                    let pos =
                        match vel with
                        | ValueSome v when v <> Vector3.Zero ->
                            { X = startPos.X + v.X * dt
                              Y = startPos.Y + v.Y * dt
                              Z = startPos.Z + v.Z * dt }
                        | _ -> startPos

                    predictedPos[entityId] <- pos

                    // Predict rotation
                    let rot =
                        match vel with
                        | ValueSome v when v <> Vector3.Zero -> MathF.Atan2(v.X, v.Z)
                        | _ ->
                            world.Entities.Rotations
                            |> Dictionary.tryFindOrDefault entityId 0.0f

                    predictedRot[entityId] <- rot

                    // Index into hex grid
                    let struct (col, row, layer) = Hex3DSpatial.worldToCell pos grid
                    insertIntoGrid grid col row layer entityId
                | _ -> ()
```

**What reads from PhysicsCache:**
- PlayerMovementSystem → `Physics.Positions[scenarioId][entityId]`
- UnitMovementSystem → `Physics.Positions[scenarioId]`
- MovementSystem → `Physics.Positions[scenarioId]` (apply block collision → write final to `Entities.Positions`)
- CollisionSystem → `Physics.Positions[scenarioId]`, `Physics.SpatialGrids[scenarioId]`
- AbilityActivationSystem → `Physics.Positions[scenarioId]` (pending cast resolution)
- AISystem → `Physics.Positions[scenarioId]` (spatial queries, ability targeting)
- ProjectileSystem → `Physics.Positions[scenarioId]` (impact detection)
- OrbitalSystem → `Physics.Positions[scenarioId]`
- RenderOrchestrator → `Physics.Positions[scenarioId]` (entity placement)

### 4.2 DerivedStatsCache

Recomputes DerivedStats for all entities. ~2 microseconds for 30 entities.

```fsharp
type DerivedStatsCache() =
    member val Stats : Dictionary<EntityId, DerivedStats>

    /// Recompute all. Called at end of tick pipeline.
    member this.Refresh(world: World, itemStore: ItemStore) =
        this.Stats.Clear()
        for KeyValue(entityId, baseStats) in world.Combat.BaseStats do
            let effects = world.Combat.ActiveEffects |> Dictionary.tryFindV entityId
            let equipmentBonuses = getEquipmentBonuses world.Inventory itemStore entityId
            let initial = calculateBase baseStats
            let final = applyModifiers initial effects equipmentBonuses
            this.Stats[entityId] <- final

    member this.TryGet(entityId: EntityId) : DerivedStats voption =
        this.Stats |> Dictionary.tryFindV entityId
```

**What reads from DerivedStatsCache:**
- PlayerMovementSystem → `Derived.TryGet(id).MS` (movement speed)
- UnitMovementSystem → `Derived.TryGet(id).MS`
- ResourceManagerSystem → `Derived.TryGet(id).HPRegen`, `.MPRegen`
- CombatSystem → full DerivedStats for damage calculation
- AISystem → DerivedStats for decision-making
- AbilityActivationSystem → DerivedStats for validation

### 4.3 CombatStatusCache

Extracts stun/silence status from ActiveEffects.

```fsharp
type CombatStatusCache() =
    member val Statuses : Dictionary<EntityId, ResizeArray<CombatStatus>>

    member this.Refresh(world: CombatState) =
        this.Statuses.Clear()
        for KeyValue(entityId, effects) in world.ActiveEffects do
            let statuses = ResizeArray()
            for effect in effects do
                match effect.SourceEffect.Kind with
                | EffectKind.Stun -> statuses.Add CombatStatus.Stunned
                | EffectKind.Silence -> statuses.Add CombatStatus.Silenced
                | _ -> ()
            if statuses.Count > 0 then
                this.Statuses[entityId] <- statuses
```

### 4.4 LiveEntityCache

Maintains the set of alive entity IDs.

```fsharp
type LiveEntityCache() =
    member val Entities : HashSet<EntityId>

    member this.Refresh(world: CombatState) =
        this.Entities.Clear()
        for KeyValue(entityId, resource) in world.Resources do
            if resource.Status = Alive then
                this.Entities.Add(entityId) |> ignore
```

---

## 5. Message Types

Struct DUs grouped by domain. Each group maps to a system or set of systems.

```fsharp
[<Struct>]
type InputMsg =
    | RawInputChanged   of entityId: EntityId
    | ActionStatesChanged of entityId: EntityId

[<Struct>]
type CombatMsg =
    | AbilityIntent     of caster: EntityId * skillId: int<SkillId> * target: SkillTarget
    | DamageDealt       of target: EntityId * amount: int * element: Element
    | EffectApplied     of target: EntityId * effect: ActiveEffect
    | EffectDamage      of target: EntityId * amount: int * element: Element
    | EffectResource    of target: EntityId * resource: ResourceKind * amount: int
    | EntityDied        of entityId: EntityId * scenarioId: Guid<ScenarioId>

[<Struct>]
type MovementMsg =
    | MovementTarget     of entityId: EntityId * destination: WorldPosition
    | MovementStateChanged of entityId: EntityId * state: MovementStateKind
    | MovementPathChanged of entityId: EntityId * path: WorldPosition[]

[<Struct>]
type ProjectileMsg =
    | ProjectileCreated of projId: EntityId * proj: LiveProjectile
    | ProjectileImpacted of projId: EntityId * target: EntityId
    | ChargeCompleted   of entityId: EntityId * skillId: int<SkillId>

[<Struct>]
type SpawnMsg =
    | SpawnEntity  of spawnType: SpawnType * pos: WorldPosition * scenarioId: Guid<ScenarioId>
    | RegisterZones of scenarioId: Guid<ScenarioId> * zones: SpawnZone[]
    | EntityRemoved of entityId: EntityId

[<Struct>]
type InventoryMsg =
    | ItemPickUp  of entityId: EntityId * itemId: Guid<ItemInstanceId>
    | ItemEquip   of entityId: EntityId * slot: Slot * itemId: Guid<ItemInstanceId>
    | ItemUnequip of entityId: EntityId * slot: Slot
    | ItemUse     of entityId: EntityId * itemId: Guid<ItemInstanceId>

[<Struct>]
type CollisionMsg =
    | EntityCollision of entityA: EntityId * entityB: EntityId

[<Struct>]
type NotificationMsg =
    | ShowMessage       of text: string * pos: WorldPosition * typ: NotificationType
    | ResourceRestored  of entityId: EntityId * amount: int

[<Struct>]
type TopLevelMsg =
    | Tick          of tick: GameTime
    | FixedStep     of dt: float32
    | Input         of msg: InputMsg
    | Combat        of msg: CombatMsg
    | Movement      of msg: MovementMsg
    | Projectile    of msg: ProjectileMsg
    | Spawn         of msg: SpawnMsg
    | Inventory     of msg: InventoryMsg
    | Collision     of msg: CollisionMsg
    | Notification  of msg: NotificationMsg
    | ChunkCreated  of key: struct (int * int) * chunk: Scenario
```

---

## 6. System Module Contract

Each system is a module with:

- No internal `Model` type (state lives in World sub-models)
- An `update` function that mutates World in-place and returns Cmds

```fsharp
module CombatSystem

/// Process a CombatMsg. Mutates world.Combat in-place.
/// Returns Cmds for downstream events.
let handleMsg (world: World) (itemStore: ItemStore) (msg: CombatMsg) : Cmd<TopLevelMsg> =
    match msg with
    | AbilityIntent(caster, skillId, target) ->
        let stats = world.Derived.TryGet(caster)
        let cooldowns = world.Combat.Cooldowns[caster]
        // ... validate, calculate damage, apply costs ...
        Cmd.batch [
            Cmd.ofMsg (Combat (DamageDealt(target, damage, element)))
            Cmd.ofMsg (Combat (EffectApplied(target, effect)))
        ]
    | DamageDealt(target, amount, element) ->
        // ... subtract HP ...
        if hp <= 0 then
            Cmd.ofMsg (Combat (EntityDied(target, scenarioId)))
        else
            Cmd.none
    | ...

/// Per-tick update (if system needs per-tick work beyond message handling).
/// Most systems only need handleMsg; this is for continuous processes like regen.
let update (dt: float32) (world: World) : struct (World * Cmd<TopLevelMsg>) =
    // ... per-tick logic (e.g., HP/MP regen) ...
    struct (world, Cmd.none)
```

### Update Function Wiring

```fsharp
let update (msg: TopLevelMsg) (world: World) : struct (World * Cmd<TopLevelMsg>) =
    match msg with
    | FixedStep dt ->
        System.start world
        |> System.pipeMutable (InputSystem.update dt)
        |> System.pipeMutable (InputMappingSystem.update dt)
        |> System.pipeMutable (PlayerMovementSystem.update dt)
        |> System.pipeMutable (UnitMovementSystem.update dt)
        |> System.pipeMutable (AbilityActivationSystem.update dt)
        |> System.pipeMutable (ResourceManagerSystem.update dt)
        |> System.pipeMutable (ProjectileSystem.update dt)
        |> System.pipeMutable (OrbitalSystem.update dt)
        |> System.pipeMutable (MovementSystem.update dt)
        |> System.pipeMutable (CollisionSystem.update dt)
        |> System.pipeMutable (NotificationSystem.update dt)
        |> System.pipeMutable (EffectProcessingSystem.update dt)
        |> System.pipeMutable (SpawnSystem.update dt)
        |> System.pipeMutable (AISystem.update dt)
        |> System.pipeMutable (AnimationSystem.update dt)
        |> System.pipeMutable (ParticleSystem.update dt)
        |> System.pipeMutable (fun _ -> world.Derived.Refresh(world, itemStore))
        |> System.pipeMutable (fun _ -> CombatStatusCache.Refresh(world.Combat))
        |> System.pipeMutable (fun _ -> LiveEntityCache.Refresh(world.Combat))
        |> System.pipeMutable (fun _ -> world.Physics.Refresh(world))
        |> System.finish id

    | Tick gt ->
        // Per-frame work: UI, camera, diagnostics
        struct (world, Cmd.none)

    | Input msg       -> InputSystem.handleMsg world msg; struct (world, Cmd.none)
    | Combat msg      -> struct (world, CombatSystem.handleMsg world itemStore msg)
    | Movement msg    -> MovementSystem.handleMsg world msg; struct (world, Cmd.none)
    | Projectile msg  -> ProjectileSystem.handleMsg world msg; struct (world, Cmd.none)
    | Spawn msg       -> SpawnSystem.handleMsg world msg; struct (world, Cmd.none)
    | Inventory msg   -> InventorySystem.handleMsg world msg; struct (world, Cmd.none)
    | Collision msg   -> CollisionSystem.handleMsg world msg; struct (world, Cmd.none)
    | Notification msg -> NotificationSystem.handleMsg world msg; struct (world, Cmd.none)
    | ChunkCreated(key, scenario) ->
        world.Scenarios[key] <- scenario
        struct (world, Cmd.none)
```

---

## 7. Spatial System: HexGrid3D

### 7.1 Block Map

The world is stored as `HexGrid3D<BlockType>`. Each cell contains a block type or `ValueNone` (empty).

```fsharp
type Scenario = {
    Id       : Guid<ScenarioId>
    BlockMap : HexGrid3D<BlockType>
}
```

### 7.2 Spatial Indexing

The PhysicsCache builds a `HexGrid3D<EntityId[]>` per scenario from predicted positions. This serves as the spatial index for:

- **Neighbor queries**: `Hex3DSpatial.neighbors col row layer grid` — 6 uniform neighbors
- **Range queries**: `Hex3DSpatial.inRange col row layer radius grid` — entities within hex radius
- **Line of sight**: `Hex3DSpatial.lineOfSight ... isBlocked grid` — 3D Bresenham on hex
- **Pathfinding**: `Hex3DSpatial.findPath start goal isPassable costFn grid` — A* with ArrayPool scratch

### 7.3 Movement on Hex Grid

Hex grid advantages for ARPG movement:

- **6 uniform neighbors**: No diagonal bias. All adjacent cells are equidistant.
- **Natural movement costs**: All adjacent moves cost the same. No sqrt(2) diagonal penalty.
- **Better AoE shapes**: Hex circles approximate true circles better than square grids.
- **Smoother pathfinding**: A* on hex produces more natural-looking paths.

### 7.4 Collision Integration

Kipo's collision predicates (`canOccupy`, `canTraverse`, `tryProjectToGround`) are ported as pluggable predicates that feed into Mibo's hex A*:

```fsharp
module Spatial

let findPath (grid: HexGrid3D<BlockType>) (entityHeight: float32) (start: Vector3) (goal: Vector3) =
    let struct (sc, sr, sl) = Hex3DSpatial.worldToCell start grid
    let struct (gc, gr, gl) = Hex3DSpatial.worldToCell goal grid
    Hex3DSpatial.findPath
        struct (sc, sr, sl)
        struct (gc, gr, gl)
        (fun c r l -> canOccupy entityHeight grid c r l)
        (fun fc fr fl tc tr tl -> moveCost entityHeight grid fc fr fl tc tr tl)
        grid
    |> ValueOption.map (Array.map (fun struct (c, r, l) -> HexGrid3D.getWorldPos c r l grid))
```

### 7.5 Entity Spatial Search

Standalone functions operating on the PhysicsCache spatial grid:

```fsharp
module SpatialQuery

let findTargetsInRadius
    (grid: HexGrid3D<EntityId[]>)
    (liveEntities: HashSet<EntityId>)
    (center: WorldPosition)
    (radius: float32)
    : struct (EntityId * WorldPosition)[] =
    let struct (cc, cr, cl) = Hex3DSpatial.worldToCell center grid
    let cells = Hex3DSpatial.inRange cc cr cl (int(radius / grid.HexSize) + 1) grid
    // ... collect entities from cells, distance-filter ...
```

---

## 8. Program Configuration

```fsharp
let program =
    Program.mkProgram init update
    |> Program.withConfig (fun cfg -> {
        cfg with
            Width = 1280
            Height = 720
            Title = "Kipo"
            TargetFPS = 120
    })
    |> Program.withInput
    |> Program.withSubscription subscribe
    |> Program.withFixedStep {
        StepSeconds = 1.0f / 60.0f
        MaxStepsPerFrame = 5
        MaxFrameSeconds = ValueSome 0.25f
        Map = FixedStep
    }
    |> Program.withTick Tick
    |> Program.withRenderer (fun () -> Renderer3D.create pipeline sceneView)
    |> Program.withRenderer (fun () ->
        Renderer2D.createWith Renderer2DConfig.noClear hudView)
```

---

## 9. Performance Characteristics

| Metric | Value | Notes |
|---|---|---|
| DerivedStats recompute | ~2 µs for 30 entities | All entities, every tick. Negligible. |
| PhysicsCache refresh | ~50 µs for 30 entities | Position prediction + hex grid rebuild |
| Hex A* pathfinding | ~10-50 µs per path | ArrayPool scratch, zero persistent alloc |
| System pipeline | ~100 µs total | 17 systems × ~6 µs each |
| Total tick budget | ~172 µs | Well within 16.6 ms (60 FPS) |
| GC pressure per tick | Near zero | Classes are long-lived, no per-tick alloc |
| Struct Msg DU | Stack-allocated | No heap pressure for message passing |
| Cmd.none | Singleton | Zero allocation for empty commands |
| Cmd.batch2 | O(1) array merge | No list append, no reversing |
| ResizeArray chooseV/filterV | O(n) in-place compact | Zero allocation, two-pointer pattern |
| Dictionary tryFindV | O(1) amortized | Inline wrapper around TryGetValue, ValueOption return |
| All module functions | inline + InlineIfLambda | Zero-overhead lambda composition, JIT inlines at call site |

### Optimization Ladder (Mibo Performance Doc)

The engine operates at **Level 3+** of Mibo's performance ladder:

- **Level 0**: Domain types use idiomatic F# where appropriate (config, content)
- **Level 1**: All message types are `[<Struct>]` DUs. Grid coordinates are structs.
- **Level 2**: All function returns are `struct (Model * Cmd<Msg>)`. No heap tuples.
- **Level 3**: Entity arrays, particle pools, spatial grids use mutable collections (`ResizeArray`, arrays, `Dictionary`).
- **Level 4**: Hex A* uses `ArrayPool.Shared` for scratch arrays. Particle system uses parallel arrays.
- **Level 5**: Large struct parameters passed by `inref`/`byref` where profiling indicates need.

### Scaling Level (Mibo Scaling Doc)

The engine operates at **Level 3-4** of Mibo's scaling ladder:

- **Level 3**: System pipeline with `System.pipeMutable` for ordered phases. Derived caches refreshed at pipeline end.
- **Level 4**: Fixed timestep via `Program.withFixedStep`. Deterministic simulation. Time represented as data.

---

## 10. Data Flow Summary

```
┌─────────────────────────────────────────────────────────────┐
│                        STEP N-1                             │
│                                                             │
│  Systems read from caches built at end of Step N-2:        │
│    PhysicsCache: predicted positions, hex spatial grid      │
│    DerivedStatsCache: per-entity computed stats             │
│    CombatStatusCache: stun/silence per entity               │
│    LiveEntityCache: set of alive entity IDs                 │
│                                                             │
│  Pipeline: systems mutate World sub-models in-place        │
│    Input → Combat → Movement → AI → VFX → Caches           │
│                                                             │
│  Pipeline returns Cmds (accumulated via System.pipeMutable):│
│    Simulation: Cmd.ofMsg → processed in same step's drain   │
│    Visual:     Cmd.deferNextFrame → next frame              │
│                                                             │
│  End of pipeline: refresh all caches                        │
│    DerivedStatsCache.Refresh()                              │
│    CombatStatusCache.Refresh()                              │
│    LiveEntityCache.Refresh()                                │
│    PhysicsCache.Refresh()  ← predicted pos + hex grid       │
│                                                             │
│  Queue drain: simulation Cmds processed                     │
│    CombatSystem, ResourceManager, SpawnSystem handle msgs   │
│    Cascades resolve within same drain                       │
│    AbilityIntent → DamageDealt → EntityDied                 │
│    All handlers see fully consistent world + fresh caches   │
│                                                             │
│  Visual Cmds (deferNextFrame) deferred to next frame        │
│                                                             │
│  Render reads from caches (read-only)                       │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                        STEP N                               │
│                                                             │
│  Deferred visual effects from Step N-1 execute:             │
│    Notifications, VFX spawns, sound playback                │
│                                                             │
│  FixedStep(dt) enqueued → full pipeline repeats             │
│  Systems read from Step N-1's caches                        │
│  Simulation latency: 1 step (~16ms at 60Hz)                 │
│  ...                                                        │
└─────────────────────────────────────────────────────────────┘
```

### Latency Comparison

| Path | Old Kipo (EventBus) | New Kipo (Cmd.ofMsg) |
|---|---|---|
| Input → AbilityActivation | 1 frame (buffered) | Same step (immediate) |
| AbilityIntent → DamageDealt | 1 frame (EventBus flush) | Same step (queue drain) |
| DamageDealt → EntityDied | 1 frame (EventBus flush) | Same step (queue drain) |
| EntityDied → entity removed | 1 frame (StateWrite flush) | Same step (queue drain) |
| **Total input → visible result** | **2 frames (~33ms)** | **1 step (~16ms)** |
| Notification float text | Same frame | Deferred 1 frame (visual-only) |
