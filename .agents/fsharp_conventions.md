# F# Code Generation Instructions

## CRITICAL: Indentation and Formatting Rules

### Absolute Requirements

**SPACES ONLY - NEVER TABS:**

- Use 2 spaces per indentation level
- Tabs cause compiler errors in F#
- Consistency mandatory across entire file

**The Offside Rule:**
F# uses significant whitespace. Once you establish an indentation level, all subsequent lines in that block MUST align at that exact column.

### Indentation Patterns

**Let Bindings:**

```fsharp
let x = 42
let y =
    someExpression
    + another

let result =
    let inner = 10
    inner + 20
```

**Functions:**

```fsharp
let add a b = a + b

let processData input =
    let validated = validate input
    let transformed = transform validated
    save transformed
```

**Pattern Matching - All | align, bodies indent 2 spaces:**

```fsharp
let describe x =
    match x with
    | 0 -> "zero"
    | 1 -> "one"
    | 2 -> "two"

// Multiline arms
let complexMatch x =
    match x with
    | Some value ->
        printfn "Found: %d" value
        value * 2
    | None ->
        printfn "Not found"
        0
```

**If/Then/Else:**

```fsharp
let x = if condition then a else b

// Multiline
let result =
    if condition then
        doSomething()
    else
        doOtherThing()
```

**Pipelines - Each |> at same level:**

```fsharp
let result =
    input
    |> validate
    |> transform
    |> save

// With lambda
let result =
    input
    |> ResizeArray.mapV (fun x ->
        compute x * 2)
```

**Records (only for DTOs, not for game state):**

```fsharp
type Config = {
    Width: int
    Height: int
}

let config = {
    Width = 1280
    Height = 720
}
```

**Classes (for game state):**

```fsharp
type EntityState() =
    member val Positions = Dictionary<EntityId, WorldPosition>() with get, set
    member val Velocities = Dictionary<EntityId, Vector3>() with get, set
```

**Lists and Arrays:**

```fsharp
let numbers = [| 1; 2; 3; 4; 5 |]

// Multiline
let numbers = [|
    1
    2
    3
|]

let squares = [| for x in 1 .. 10 -> x * x |]
```

### Whitespace Rules

**DO:**

- One space after commas: `(1, 2, 3)`
- One space around operators: `x + y`
- Blank line between functions
- `spam (ham 1)` - space between function and args

**DO NOT:**

- Spaces inside parentheses: `spam( ham 1 )` ❌
- Align by variable name length (fragile) ❌
- Use tabs ❌

### Comments

```fsharp
// Use // for inline comments

/// Use /// for XML documentation on public APIs
let publicFunction x = x + 1
```

## Core Principles

Generate F# code following these principles:

1. **Succinct, expressive, composable** - Minimal boilerplate, clear intent, natural composition
2. **Interoperable** - Consider .NET language consumption
3. **Performance-first** - Mutable classes, struct types, inline functions
4. **No allocation in hot paths** - Avoid heap pressure in per-frame code
5. **Toolable** - Compatible with F# tooling and formatters

## Type System

### Classes (for game state)

- Mutable `member val` properties with `with get, set`
- Constructor-less (use `type X()` not `type X(a, b)`)
- Sub-models as nested classes for semantic grouping
- Never use records for game state (copy semantics are too expensive)

### Structs (for small data)

- `[<Struct>]` for types smaller than 16-24 bytes
- Grid coordinates, world positions, messages, commands
- Struct tuples `struct (a, b)` over reference tuples `(a, b)`

### Discriminated Unions

- `[<Struct>]` DU for messages and commands (stack-allocated)
- PascalCase case names
- `[<RequireQualifiedAccess>]` when case names are common
- **NEVER use `| _ ->` wildcard on DUs** — always match every case explicitly

### Match Expressions — Exhaustive, No Wildcard Discards

**DO: ✅**

```fsharp
match msg with
| AbilityIntent(caster, skillId, target) -> handleAbility caster skillId target
| DamageDealt(target, amount, element) -> handleDamage target amount element
| EffectApplied(target, effect) -> handleEffect target effect
| EffectDamage(target, amount, element) -> handleEffectDamage target amount element
| EffectResource(target, resource, amount) -> handleResource target resource amount
| EntityDied(entityId, scenarioId) -> handleDeath entityId scenarioId
```

**DO NOT: ❌**

```fsharp
match msg with
| AbilityIntent(caster, skillId, target) -> handleAbility caster skillId target
| DamageDealt(target, amount, element) -> handleDamage target amount element
| _ -> ()  // ❌ hides unhandled cases
```

**Exception:** Wildcard is acceptable only for:
- Truly logically unreachable cases with a comment explaining why
- `ValueOption` / `Option` where `ValueNone` / `None` is a single semantic case
- BCL enums

```fsharp
// Acceptable: ValueOption has exactly two cases, None is a single semantic meaning
match Dictionary.tryFindV entityId world.Combat.Resources with
| ValueSome r -> processResource r
| ValueNone -> () // entity has no resources — expected for some entity types
```

### Option Types

- `ValueOption<'T>` is preferred over `Option<'T>` (no heap allocation)
- Pattern match: `match vopt with | ValueSome v -> ... | ValueNone -> ...`
- Module: `ValueOption.map`, `ValueOption.bind`, `ValueOption.defaultValue`
- At C# boundaries: convert to nullable

## Code Organization

### Namespaces and Modules

- Top level: namespaces (not modules)
- Within namespaces: nested modules for grouping
- `[<RequireQualifiedAccess>]` on modules with common names
- DO NOT use `[<AutoOpen>]` except for computation builders
- Maximum 2-3 module nesting levels

### File Structure

Within files, order:

1. Open statements (grouped)
2. Type definitions (classes, structs, DUs)
3. Module definitions with functions
4. Active patterns

### Dependency Order

- Definitions before usage (F# requirement)
- Helpers before callers
- Types before functions using them
- Use `and` for mutual recursion

## Performance

### Inline Functions

**All module functions must be `inline` with `[<InlineIfLambda>]` on callbacks:**

```fsharp
module ResizeArray =
    let inline chooseV
        ([<InlineIfLambda>] chooser: 'T -> 'U voption)
        (source: ResizeArray<'T>)
        : ResizeArray<'U> =
        // ...
```

This ensures zero-overhead lambda composition — the JIT inlines the lambda body at the call site.

### Struct Types

```fsharp
[<Struct>]
type Msg =
    | Damage of amount: int
    | Heal of amount: int

[<Struct>]
type WorldPosition = { X: float32; Y: float32; Z: float32 }
```

### Mutable Collections

```fsharp
// GOOD: Mutable, cache-friendly
let entities = ResizeArray<Entity>()
let positions = Dictionary<EntityId, WorldPosition>()

// BAD: Immutable, GC pressure per frame
let entities = [] // linked list
let positions = Map.empty // immutable tree
```

### ArrayPool for Temporary Buffers

```fsharp
open System.Buffers

let buffer = ArrayPool<int>.Shared.Rent(count)
try
    // use buffer
    ()
finally
    ArrayPool<int>.Shared.Return(buffer)
```

## Pattern Matching

**DO:**

- Use as primary control flow
- Match exhaustively on discriminated unions
- Decompose structures inline
- Combine with `when` guards

**DO NOT:**

- Use wildcard `| _ ->` on discriminated unions — always match every case explicitly
- Use if-else chains instead of pattern matching

## Naming

- **PascalCase**: Types, modules, namespaces, record fields, union cases, properties, methods
- **camelCase**: Functions, values, parameters, local bindings
- **Acronyms**: Treat as words (`XmlDocument`, not `XMLDocument`)

## Summary Checklist

When generating F# code:

- [ ] Use 2 spaces for indentation (NEVER tabs)
- [ ] Respect the offside rule (align all lines in block)
- [ ] Game state uses classes with `member val` (not records)
- [ ] Messages/commands use `[<Struct>]` DU
- [ ] All module functions are `inline` with `[<InlineIfLambda>]` on callbacks
- [ ] `ValueOption` over `Option`
- [ ] `ResizeArray<T>` over F# `list`
- [ ] `Dictionary<K,V>` over F# `Map`
- [ ] Exhaustive pattern matching — no `| _ ->` on DUs
- [ ] No extra comments in code
