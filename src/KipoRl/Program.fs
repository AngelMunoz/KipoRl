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
  struct (world, Cmd.none)

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update
  (msg: TopLevelMsg)
  (world: World)
  : struct (World * Cmd<TopLevelMsg>) =
  match msg with
  | Tick _gt -> world, Cmd.none

  | FixedStep _dt -> world, Cmd.none

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
      InputMapper.subscribeStatic
        inputMap
        (fun states -> Input(ActionStatesChanged(playerId, states)))
        ctx)
    |> Program.withTick Tick
    |> Program.withRenderer(fun () ->
      Renderer3D.create (ForwardPbrPipeline()) view)

  let game = new RaylibGame<World, TopLevelMsg>(program)
  game.Run()
  0
