module KipoRl.Program

open System
open System.Numerics
open Raylib_cs
open FSharp.UMX
open Mibo.Elmish
open Mibo.Elmish.Graphics3D
open Mibo.Elmish.Graphics3D.Pipelines
open Mibo.Input

// ─────────────────────────────────────────────────────────────
// Input
// ─────────────────────────────────────────────────────────────

let inputMap =
  InputMap.empty
  // Ability slots
  |> InputMap.key UseSlot1 KeyboardKey.Q
  |> InputMap.key UseSlot2 KeyboardKey.E
  |> InputMap.key UseSlot3 KeyboardKey.R
  |> InputMap.key UseSlot4 KeyboardKey.F
  |> InputMap.key UseSlot5 KeyboardKey.Z
  |> InputMap.key UseSlot6 KeyboardKey.X
  |> InputMap.key UseSlot7 KeyboardKey.C
  |> InputMap.key UseSlot8 KeyboardKey.V
  // Action set switching
  |> InputMap.key SetActionSet1 KeyboardKey.One
  |> InputMap.key SetActionSet2 KeyboardKey.Two
  |> InputMap.key SetActionSet3 KeyboardKey.Three
  |> InputMap.key SetActionSet4 KeyboardKey.Four
  |> InputMap.key SetActionSet5 KeyboardKey.Five
  |> InputMap.key SetActionSet6 KeyboardKey.Six
  |> InputMap.key SetActionSet7 KeyboardKey.Seven
  |> InputMap.key SetActionSet8 KeyboardKey.Eight
  // UI
  |> InputMap.key Cancel KeyboardKey.Escape
  |> InputMap.key ToggleInventory KeyboardKey.I
  |> InputMap.key ToggleAbilities KeyboardKey.K
  |> InputMap.key ToggleCharacterSheet KeyboardKey.C

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────

let init(_ctx: GameContext) : struct (World * Cmd<TopLevelMsg>) =
  let world = World()
  let defaultPlayer = UMX.tag 0L
  world.Players.Add defaultPlayer |> ignore
  world.Entities.Positions[defaultPlayer] <- Vector3.Zero
  struct (world, Cmd.none)

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update
  (msg: TopLevelMsg)
  (world: World)
  : struct (World * Cmd<TopLevelMsg>) =
  match msg with
  | Tick gt -> world, Cmd.none
  | FixedStep dt ->
    world.Time.Delta <- TimeSpan.FromSeconds(float dt)
    world.Time.TotalGameTime <- world.Time.TotalGameTime + world.Time.Delta

    world
    |> System.start
    |> System.pipeMutable PlayerMovementSystem.update
    |> System.pipeMutable(MovementSystem.update dt)
    |> System.pipeMutable(UnitMovementSystem.update dt)
    |> System.pipeMutable(ResourceManagerSystem.update dt)
    |> System.pipeMutable(EffectProcessingSystem.update dt)
    |> System.finish id

  | Input inputMsg ->
    match inputMsg with
    | ActionStatesChanged(entityId, states) ->
      InputSystem.update entityId states world
      world, Cmd.none
    | MouseClick(entityId, screenPos) ->
      // TODO: Convert screen → world using camera when camera system lands
      let worldPos = {
        X = screenPos.X
        Y = 0.f
        Z = screenPos.Y
      }

      MovementSystem.handleMsg
        world
        (MovementMsg.MovementTarget(entityId, worldPos))

      world, Cmd.none

  | Movement movementMsg ->
    MovementSystem.handleMsg world movementMsg
    world, Cmd.none

  | ResourceManager resourceMsg ->
    ResourceManagerSystem.handleMsg world resourceMsg

  | Combat combatMsg ->
    match combatMsg with
    | CombatMsg.EffectResource(resTarget, resource, amount) ->
      ResourceManagerSystem.handleMsg
        world
        (RestoreResource(resTarget, resource, amount))
    | CombatMsg.AbilityIntent _ -> world, Cmd.none
    | CombatMsg.DamageDealt _ -> world, Cmd.none
    | CombatMsg.EffectApplied _ -> world, Cmd.none
    | CombatMsg.EffectDamage _ -> world, Cmd.none
    | CombatMsg.EntityDied _ -> world, Cmd.none

  | EffectProcessing effectMsg ->
    EffectProcessingSystem.handleMsg world effectMsg

  | Notification _ -> world, Cmd.none

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
  |> Draw3D.drop

  for KeyValue(_, position) in world.Entities.Positions do
    let transform = Raymath.MatrixTranslate(position.X, position.Y, position.Z)
    let material = Material3D.colored Color.Red

    buffer |> Draw3D.drawMesh Primitive3D.cube transform material |> Draw3D.drop

  buffer |> Draw3D.endCamera |> Draw3D.drop

// ─────────────────────────────────────────────────────────────
// Program
// ─────────────────────────────────────────────────────────────

let playerId = UMX.tag<EntityId> 0L

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
      let inputSub =
        InputMapper.subscribeStatic
          inputMap
          (fun states -> Input(ActionStatesChanged(playerId, states)))
          ctx

      let clickSub =
        Mouse.onLeftClick (fun pos -> Input(MouseClick(playerId, pos))) ctx

      Sub.batch [ inputSub; clickSub ])
    |> Program.withTick Tick
    |> Program.withFixedStep {
      StepSeconds = (1.f / 60.f)
      MaxStepsPerFrame = 5
      MaxFrameSeconds = ValueNone
      Map = FixedStep
    }
    |> Program.withRenderer(fun () ->
      Renderer3D.create (ForwardPbrPipeline()) view)

  let game = new RaylibGame<World, TopLevelMsg>(program)
  game.Run()
  0
