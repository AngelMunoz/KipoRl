# Foundation + InputSystem Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use compose:subagent (recommended) or compose:execute to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire up existing foundation code, create the World/System infrastructure, implement InputSystem, and get a running pipeline that captures keyboard input.

**Architecture:** Minimal World class with InputState sub-model. System module provides pipeMutable for chaining systems. InputSystem receives ActionState from Mibo's subscription and stores it in World. No rendering changes yet — just the pipeline running.

**Tech Stack:** F#, .NET 10, Mibo.Raylib (Elmish), BCL collections (Dictionary, HashSet, ResizeArray)

---

### Task 1: Wire up existing files into .fsproj

**Files:**

- Modify: `src/KipoRl/KipoRl.fsproj`

- [ ] **Step 1: Update .fsproj to include files**

Add the files in dependency order (Types first, then helpers):

```xml
<ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="Dictionary.fs" />
    <Compile Include="ResizeArray.fs" />
    <Compile Include="Program.fs" />
</ItemGroup>
```

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build`
Expected: Build succeeds (may have warnings about unused types, that's fine)

- [ ] **Step 3: Commit**

```bash
dotnet fantomas .
git add src/KipoRl/KipoRl.fsproj
git commit -m "chore: wire up foundation types and helper modules"
```

---

### Task 2: Create World class and InputState sub-model

**Files:**

- Create: src/KipoRl/World.fs`

- [ ] **Step 1: Create src/KipoRl/World.fs with InputState and World class**

```fsharp
namespace KipoRl

open System
open System.Collections.Generic
open Mibo.Input

type InputState() =
    member val ActionStates: Dictionary<int, ActionState<GameAction>> = Dictionary() with get, set

type World() =
    member val Input: InputState = InputState() with get, set
```

Note: `GameAction` is defined in Program.fs. We need to move it to a shared location or reference it. Since F# requires definitions before usage, we'll define GameAction here.

Actually — GameAction is currently in Program.fs. For World.fs to reference it, we need to either:

1. Move GameAction to World.fs (or a Types file)
2. Or keep it in Program.fs and have World.fs not reference it directly

Since InputState stores `ActionState<GameAction>`, and GameAction is a simple DU, the cleanest approach is to define it in World.fs (or move it to Types.fs).

Let me revise: define GameAction in World.fs since it's part of the input domain.

```fsharp
namespace KipoRl

open System
open System.Collections.Generic
open Mibo.Input

[<Struct>]
type GameAction =
    | MoveForward
    | MoveBackward
    | MoveLeft
    | MoveRight
    | MoveUp
    | MoveDown

type InputState() =
    member val ActionStates: Dictionary<int, ActionState<GameAction>> = Dictionary() with get, set

type World() =
    member val Input: InputState = InputState() with get, set
```

- [ ] **Step 2: Update .fsproj to include World.fs before src/KipoRl/Program.fs**

```xml
<ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="Dictionary.fs" />
    <Compile Include="ResizeArray.fs" />
    <Compile Include="World.fs" />
    <Compile Include="Program.fs" />
</ItemGroup>
```

- [ ] **Step 3: Remove GameAction from Program.fs**

Remove the GameAction type definition from src/KipoRl/Program.fs (lines 15-22). Keep the inputMap and everything else.

- [ ] **Step 4: Verify build compiles**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
dotnet fantomas .
git add src/KipoRl/World.fs src/KipoRl/KipoRl.fsproj src/KipoRl/Program.fs
git commit -m "feat: add World class with InputState sub-model"
```

---

### Task 3: Create message types

**Files:**

- Create: Messages.fs`

- [ ] **Step 1: Create Messages.fs with TopLevelMsg and InputMsg**

```fsharp
namespace KipoRl

open Mibo.Input

[<Struct>]
type InputMsg =
    | ActionStatesChanged of entityId: int * states: ActionState<GameAction>

[<Struct>]
type TopLevelMsg =
    | Tick of tick: GameTime
    | FixedStep of dt: float32
    | Input of msg: InputMsg
```

- [ ] **Step 2: Update .fsproj to include Messages.fs before World.fs**

```xml
<ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="Dictionary.fs" />
    <Compile Include="ResizeArray.fs" />
    <Compile Include="Messages.fs" />
    <Compile Include="World.fs" />
    <Compile Include="Program.fs" />
</ItemGroup>
```

- [ ] **Step 3: Verify build compiles**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
dotnet fantomas .
git add src/KipoRl/Messages.fs src/KipoRl/KipoRl.fsproj
git commit -m "feat: add TopLevelMsg and InputMsg types"
```

---

### Task 4: Create System module

**_SYSTEMS ALREADY EXIST IN MIBO.RAYLIB_**

Mibo System's are documented at https://angelmunoz.github.io/Mibo.Raylib/system.html
ans source can be found at ~/repos/Mibo.Raylib/src/Mibo.Raylib/Elmish.System.fs

---

### Task 5: Implement InputSystem

**Files:**

- Create: `src/KipoRl/InputSystem.fs`

- [ ] **Step 1: Create src/KipoRl/InputSystem.fs**

```fsharp
namespace KipoRl

open Mibo.Input

module InputSystem =
    let update (entityId: int) (states: ActionState<GameAction>) (world: World) : Cmd<TopLevelMsg> =
        world.Input.ActionStates[entityId] <- states
        Cmd.none
```

This is intentionally minimal. The InputSystem:

1. Receives an entityId and the current ActionState from Mibo's subscription
2. Stores it in World.Input.ActionStates
3. Returns no commands (for now)

- [ ] **Step 2: Update .fsproj to include InputSystem.fs**

```xml
<ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="Dictionary.fs" />
    <Compile Include="ResizeArray.fs" />
    <Compile Include="Messages.fs" />
    <Compile Include="World.fs" />
    <Compile Include="System.fs" />
    <Compile Include="InputSystem.fs" />
    <Compile Include="Program.fs" />
</ItemGroup>
```

- [ ] **Step 3: Verify build compiles**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
dotnet fantomas .
git add src/KipoRl/InputSystem.fs src/KipoRl/KipoRl.fsproj
git commit -m "feat: implement InputSystem"
```

---

### Task 6: Update src/KipoRl/Program.fs to use World + System pipeline

**Files:**

- Modify: `src/KipoRl/Program.fs`

- [ ] **Step 1: Rewrite src/KipoRl/Program.fs to use World and System pipeline**

Replace the Model-based Elmish with World-based:

```fsharp
module KipoRl.Program

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics3D
open Mibo.Elmish.Graphics3D.Pipelines
open Mibo.Input

// ─────────────────────────────────────────────────────────────
// Input
// ─────────────────────────────────────────────────────────────

let inputMap =
    InputMap.empty
    |> InputMap.key MoveForward KeyboardKey.W
    |> InputMap.key MoveForward KeyboardKey.Up
    |> InputMap.key MoveBackward KeyboardKey.S
    |> InputMap.key MoveBackward KeyboardKey.Down
    |> InputMap.key MoveLeft KeyboardKey.A
    |> InputMap.key MoveLeft KeyboardKey.Left
    |> InputMap.key MoveRight KeyboardKey.D
    |> InputMap.key MoveRight KeyboardKey.Right
    |> InputMap.key MoveUp KeyboardKey.Space
    |> InputMap.key MoveDown KeyboardKey.LeftShift

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────

let init(_ctx: GameContext) : struct (World * Cmd<TopLevelMsg>) =
    let world = World()
    world, Cmd.none

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update (msg: TopLevelMsg) (world: World) : struct (World * Cmd<TopLevelMsg>) =
    match msg with
    | Tick _gt ->
        struct (world, Cmd.none)

    | FixedStep _dt ->
        struct (world, Cmd.none)

    | Input msg ->
        InputSystem.update 0 msg world |> ignore
        struct (world, Cmd.none)

// ─────────────────────────────────────────────────────────────
// View
// ─────────────────────────────────────────────────────────────

let view (_ctx: GameContext) (world: World) (buffer: RenderBuffer3D) =
    let camera =
        Camera3D(
            Vector3(12.f, 12.f, 12.f),
            Vector3.Zero,
            Vector3.UnitY,
            55.0f,
            CameraProjection.Perspective
        )

    let transform = Raymath.MatrixTranslate(0.f, 0.f, 0.f)
    let material = Material3D.colored Color.Red

    buffer
    |> Draw3D.beginCameraWith(
        Camera3D.render camera |> Camera3D.withClear Color.RayWhite
    )
    |> Draw3D.setAmbientLight {
        Color = Color.White
        Intensity = 0.5f
    }
    |> Draw3D.addDirectionalLight {
        Direction = Vector3(1.f, -1.f, 1.f)
        Color = Color.White
        Intensity = 1.f
        CastsShadows = false
    }
    |> Draw3D.drawMesh Primitive3D.cube transform material
    |> Draw3D.endCamera
    |> Draw3D.drop

// ─────────────────────────────────────────────────────────────
// Program
// ─────────────────────────────────────────────────────────────

let playerId = 0

[<EntryPoint>]
let main _ =
    let program =
        Program.mkProgram init update
        |> Program.withConfig(fun cfg -> {
            cfg with
                Width = 800
                Height = 600
                Title = "KipoRl"
                TargetFPS = 60
        })
        |> Program.withInput
        |> Program.withSubscription(fun ctx _model ->
            InputMapper.subscribeStatic inputMap (fun states ->
                Input(ActionStatesChanged(playerId, states))
            ) ctx)
        |> Program.withTick Tick
        |> Program.withRenderer(fun () ->
            Renderer3D.create (ForwardPbrPipeline()) view)

    let game = new RaylibGame<World, TopLevelMsg>(program)
    game.Run()
    0
```

Wait — there's a problem. The subscription callback needs to return a `Msg`, but we're using `TopLevelMsg` as the message type. The `InputMapper.subscribeStatic` callback should return `TopLevelMsg`.

Looking at the original:

```fsharp
InputMapper.subscribeStatic inputMap InputChanged ctx
```

`InputChanged` is a case of `Msg`. So we need:

```fsharp
InputMapper.subscribeStatic inputMap (fun states ->
    Input(ActionStatesChanged(playerId, states))
) ctx
```

But wait — `InputMapper.subscribeStatic` might expect a specific signature. Let me check what Mibo provides.

Actually, looking at the original code more carefully:

```fsharp
|> Program.withSubscription(fun ctx _model ->
    InputMapper.subscribeStatic inputMap InputChanged ctx)
```

`InputChanged` is a function `ActionState<GameAction> -> Msg`. So `subscribeStatic` takes an `InputMap` and a function `ActionState<GameAction> -> 'Msg`.

So our version should be:

```fsharp
|> Program.withSubscription(fun ctx _model ->
    InputMapper.subscribeStatic inputMap (fun states ->
        Input(ActionStatesChanged(playerId, states))
    ) ctx)
```

This should work. The `Input` case wraps `InputMsg` into `TopLevelMsg`.

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 3: Run the game to verify it launches**

Run: `dotnet run`
Expected: Window opens with a red cube (stationary, since no movement system yet)

- [ ] **Step 4: Commit**

```bash
dotnet fantomas .
git add src/KipoRl/Program.fs
git commit -m "feat: wire Program.fs to use World and System pipeline"
```

---

### Task 7: Add test project

**Files:**

- Create: `src/KipoRl.Tests/KipoRl.Tests.fsproj`
- Create: `src/KipoRl.Tests/WorldTests.fs`
- Create: `src/KipoRl.Tests/InputSystemTests.fs`
- Create: `src/KipoRl.Tests/SystemTests.fs`

- [ ] **Step 1: Create test project**

```xml
<!-- src/KipoRl.Tests/KipoRl.Tests.fsproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <TargetFramework>net10.0</TargetFramework>
    <GenerateProgramFile>false</GenerateProgramFile>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>

    <Compile Include="WorldTests.fs" />
    <Compile Include="InputSystemTests.fs" />
    <Compile Include="SystemTests.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Expecto" Version="10.*" />
    <PackageReference Include="YoloDev.Expecto.TestSdk" Version="0.15.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\KipoRl\KipoRl.fsproj" />
  </ItemGroup>
</Project>

```

- [ ] **Step 2: Create src/KipoRl.Tests/WorldTests.fs**

```fsharp
module KipoRl.Tests.WorldTests

open Xunit
open KipoRl

[<Fact>]
let ``World initializes with empty InputState`` () =
    let world = World()
    Assert.NotNull(world.Input)
    Assert.Empty(world.Input.ActionStates)
```

- [ ] **Step 3: Create src/KipoRl.Tests/InputSystemTests.fs**

```fsharp
module KipoRl.Tests.InputSystemTests

open Xunit
open KipoRl
open Mibo.Input

[<Fact>]
let ``InputSystem stores action states in world`` () =
    let world = World()
    let states = ActionState.empty

    InputSystem.update 0 states world |> ignore

    Assert.True(world.Input.ActionStates.ContainsKey(0))
```

- [ ] **Step 4: Create src/KipoRl.Tests/SystemTests.fs**

```fsharp
module KipoRl.Tests.SystemTests

open Xunit
open KipoRl

[<Fact>]
let ``System.start returns world with no commands`` () =
    let world = World()
    let struct (w, cmd) = System.start world
    Assert.Same(world, w)
    Assert.Equal(Cmd.none, cmd)

[<Fact>]
let ``System.pipeMutable chains system execution`` () =
    let world = World()
    let mutable mutated = false

    let system _w =
        mutated <- true
        Cmd.none

    let struct (_w, _cmd) =
        System.start world
        |> System.pipeMutable system

    Assert.True(mutated)
```

- [ ] **Step 5: Verify tests pass**

Run: `dotnet test src/KipoRl.Tests/KipoRl.Tests.fsproj`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
dotnet fantomas .
git add src/KipoRl.Tests/
git commit -m "test: add unit tests for World, InputSystem, and System module"
```

---

## Summary

After completing all tasks:

- Foundation types are wired into the build
- World class with InputState exists
- Message types (TopLevelMsg, InputMsg) exist
- System module provides pipeMutable pipeline
- InputSystem captures keyboard input into World
- Program.fs runs the pipeline (game launches, cube renders, input is captured)
- Unit tests verify core infrastructure

**What's next:** Add PlayerMovementSystem + MovementSystem to get the cube moving with WASD input.

```

```
