# AI Agent Instructions

## Quick Start

- Read this file completely before making any code changes
- Follow F# conventions and Data Oriented Programming principles strictly
- When in doubt, ask clarifying questions

## General Guidelines

- **Do not add extra comments to the code**
- Follow the coding style and conventions used in the existing codebase
- Avoid aggressive refactors; always do small, methodical, incremental, and verifiable changes

**IMPORTANT**: ALWAYS present the investigation and analysis of the issue before presenting the solution.

- Ask the right questions to get the context of the issue.
- Do not assume things, research the code, trace the workflow and understand the context before presenting the solution.
- Do not present bandaids or half-baked solutions, always provide a sensible solution.
- IF you are unable to present a good solution, it is all right to say you are unable to do so, present your hypothesis and ask the user to handle the situation.
- ALWAYS ASK BEFORE COMMITING A CHANGE, even if it is a small one.
- NEVER PUSH CODE WITHOUT EXPLICIT PERMISSION.
- Always format the code before commiting `dotnet fantomas .`

**IMPORTANT**: You can find supplementary guidelines and conventions in the `.agents` folder in the project root. See [./.agents/README.md](./.agents/README.md) for details.

## Programming Paradigm

This codebase follows **Mutable Data-Oriented Programming**:

- **Data structures are classes or structs, never records.** Mutable in-place mutation, no copy semantics.
- **Systems mutate in-place.** Each system receives the World, mutates the sub-models it owns, returns `struct (World * Cmd<Msg>)`.
- **Cmds are the only inter-system communication.** No EventBus. No shared mutable queues.
- **Semantic grouping.** World state is partitioned into domain-specific sub-models. Systems only access the sub-models they need.
- **Struct DUs for messages.** Small, stack-allocated, grouped by domain for efficient dispatch.
- **Derived caches are mutable classes.** Refreshed once per tick at the end of the pipeline.
- **BCL data structures only.** `Dictionary<K,V>`, `ResizeArray<T>`, `HashSet<T>`, arrays. No FSharp.Data.Adaptive types.

## Performance Guidelines

**This is a game engine. All code must favor no-allocation operations.**

- **Domain and value-like types must be decorated as struct**
- **Discriminated unions that represent domain concepts must be decorated as struct DU**
- **Value tuples** (`struct(v1,v2)`) are favored over reference tuples
- **ValueOption** is favored over Option (`match vopt with | ValueSome v -> ... | ValueNone -> ...`)
- **All module functions must be `inline`** with `[<InlineIfLambda>]` on callbacks/predicates
- **Use `ResizeArray<T>`** (BCL `List<T>`) instead of F# immutable `list` for mutable collections
- **Use `Dictionary<K,V>`** for mutable key-value storage
- **Use `ArrayPool<T>.Shared`** for temporary per-frame buffers

### Module Design Rules

The `ResizeArray` and `Dictionary` modules provide efficient, inline helpers with `ValueOption` returns:

```fsharp
// GOOD: inline, ValueOption, no allocation
world.Combat.DerivedStats
|> Dictionary.tryFindV entityId
|> ValueOption.map (fun s -> float32 s.MS)
|> ValueOption.defaultValue 100.0f

// BAD: verbose TryGetValue pattern matching
match world.Combat.DerivedStats.TryGetValue(entityId) with
| true, stats -> float32 stats.MS
| false, _ -> 100.0f
```

## Code Conventions

**CRITICAL**: Please review the general F# coding conventions defined in [./.agents/fsharp_conventions.md](./.agents/fsharp_conventions.md) before proceeding.

### Functions Must Be Focused

Large function bodies should be refactored into smaller functions. Logic that can be reused should be moved to module-level functions.

### Modules Must Be Cohesive

Group related functions and types into modules that represent a single concept or area of functionality.

### Match Expressions Body Should Be Small

Each branch of a match expression should be concise. If a branch is complex, consider extracting it into a separate function.

### Match Expressions Must Be Exhaustive — No Wildcard Discards on DUs

**DO NOT use `| _ ->` on discriminated unions.** Always match every case explicitly.

DO: ✅

```fsharp
match msg with
| AbilityIntent(caster, skillId, target) -> handleAbility caster skillId target
| DamageDealt(target, amount, element) -> handleDamage target amount element
| EffectApplied(target, effect) -> handleEffect target effect
| EffectDamage(target, amount, element) -> handleEffectDamage target amount element
| EffectResource(target, resource, amount) -> handleResource target resource amount
| EntityDied(entityId, scenarioId) -> handleDeath entityId scenarioId
```

DO NOT: ❌

```fsharp
match msg with
| AbilityIntent(caster, skillId, target) -> handleAbility caster skillId target
| DamageDealt(target, amount, element) -> handleDamage target amount element
| _ -> ()  // ❌ hides unhandled cases
```

**Exception:** Wildcard is acceptable only for:

- Truly logically unreachable cases with a comment explaining why
- `ValueOption` / `Option` where `ValueNone` / `None` is a single semantic case
- BCL enums (which we don't use AFAIK)

```fsharp
// Acceptable: ValueOption has exactly two cases, None is a single semantic meaning
match Dictionary.tryFindV entityId world.Combat.Resources with
| ValueSome r -> processResource r
| ValueNone -> () // entity has no resources — expected for some entity types
```

### Avoid Deep Nesting

Use inline active patterns, partial active patterns, and function composition to flatten nested logic.

## Data Structure Selection

| Use Case                    | Type                  | Why                                      |
| --------------------------- | --------------------- | ---------------------------------------- |
| Per-frame mutable state     | `Dictionary<K,V>`     | O(1) amortized, no GC pressure           |
| Ordered mutable collection  | `ResizeArray<T>`      | Cache-friendly, in-place mutation        |
| Entity existence set        | `HashSet<T>`          | O(1) contains, O(1) add                  |
| Temporary per-frame buffer  | `ArrayPool<T>.Shared` | Zero persistent allocation               |
| Stack-allocated small data  | `[<Struct>]` types    | No heap allocation                       |
| Messages / commands         | `[<Struct>]` DU       | Stack-allocated, pattern-match efficient |
| Parallel arrays (particles) | `T[]` arrays          | Best cache locality                      |

## Testing Strategy

- **Unit Tests**: Test core logic modules in isolation
- **Property-Based Tests**: Use FsCheck to verify mathematical correctness of rules
- **Deterministic Simulation**: Fixed timestep + fixed seed RNG for reproducible tests

**CRITICAL**: Please review the general F# coding conventions defined in [./.agents/fsharp_conventions.md](./.agents/fsharp_conventions.md) before proceeding.
