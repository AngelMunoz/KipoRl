module KipoRl.Program

open System
open System.Numerics
open Raylib_cs
open FSharp.UMX
open Mibo.Elmish
open Mibo.Elmish.Graphics3D
open Mibo.Elmish.Graphics3D.Pipelines
open Mibo.Elmish.System
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
    world
    |> System.start
    |> System.pipeMutable(fun w ->
      PlayerMovementSystem.update w
      struct (w, Cmd.none))
    |> System.pipeMutable(fun w ->
      MovementSystem.update dt w
      struct (w, Cmd.none))
    |> System.finish id

  | Input inputMsg ->
    match inputMsg with
    | ActionStatesChanged(entityId, states) ->
      InputSystem.update entityId states world
      world, Cmd.none

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
      InputMapper.subscribeStatic
        inputMap
        (fun states -> Input(ActionStatesChanged(playerId, states)))
        ctx)
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
