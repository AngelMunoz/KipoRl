module KipoRl.Tests.UnitMovementE2ETests

open Expecto
open KipoRl
open System.Numerics
open FSharp.UMX
open Mibo.Input

let tests =
  testList "UnitMovement E2E" [
    test "MovementTarget message stores target in world" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Entities.Positions[entityId] <- Vector3(0.f, 0.f, 0.f)

      let target = { X = 10.f; Y = 0.f; Z = 0.f }

      let msg =
        TopLevelMsg.Movement(MovementMsg.MovementTarget(entityId, target))

      let struct (world, _) = Program.update msg world

      Expect.isTrue
        (world.Movement.Targets.ContainsKey entityId)
        "Target should be stored"

      Expect.equal
        world.Movement.Targets[entityId]
        target
        "Target position should match"
    }

    test "MovementStateChanged message updates state" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Movement.States[entityId] <- Idle

      let msg =
        TopLevelMsg.Movement(
          MovementMsg.MovementStateChanged(entityId, Casting)
        )

      let struct (world, _) = Program.update msg world

      Expect.equal
        world.Movement.States[entityId]
        Casting
        "State should be Casting"
    }

    test "FixedStep pipeline processes non-player movement" {
      let world = World()
      let entityId = UMX.tag 1L
      world.Entities.Positions[entityId] <- Vector3(0.f, 0.f, 0.f)
      world.Movement.Targets[entityId] <- { X = 10.f; Y = 0.f; Z = 0.f }

      let struct (world, _) = Program.update (FixedStep(1.f)) world

      let vel = world.Entities.Velocities[entityId]

      Expect.isGreaterThan
        (abs vel.X)
        0.001f
        "Should have velocity from pipeline"

      Expect.equal
        world.Movement.States[entityId]
        Moving
        "Should be Moving after pipeline"
    }

    test "full flow: store target then pipeline moves entity" {
      let world = World()
      let entityId = UMX.tag 2L
      world.Entities.Positions[entityId] <- Vector3(0.f, 0.f, 0.f)

      let target = { X = 10.f; Y = 0.f; Z = 0.f }

      let struct (world, _) =
        Program.update
          (TopLevelMsg.Movement(MovementMsg.MovementTarget(entityId, target)))
          world

      let struct (world, _) = Program.update (FixedStep(1.f)) world

      let vel = world.Entities.Velocities[entityId]

      Expect.isGreaterThan
        (abs vel.X)
        0.001f
        "Should have velocity after pipeline"

      Expect.equal world.Movement.States[entityId] Moving "Should be Moving"
    }

    test "player entities skip UnitMovementSystem in pipeline" {
      let world = World()
      let entityId = UMX.tag 3L
      world.Players.Add entityId |> ignore
      world.Entities.Positions[entityId] <- Vector3(0.f, 0.f, 0.f)
      world.Movement.Targets[entityId] <- { X = 10.f; Y = 0.f; Z = 0.f }

      let struct (world, _) = Program.update (FixedStep(1.f)) world

      Expect.isFalse
        (world.Entities.Velocities.ContainsKey entityId)
        "Player velocity should not be set by UnitMovementSystem"
    }

    test "player and non-player move independently in same world" {
      let world = World()
      let player = UMX.tag 0L
      let unit = UMX.tag 1L
      world.Players.Add player |> ignore
      world.Entities.Positions[player] <- Vector3(0.f, 0.f, 0.f)
      world.Entities.Positions[unit] <- Vector3(0.f, 0.f, 0.f)
      world.Movement.Targets[unit] <- { X = 10.f; Y = 0.f; Z = 0.f }

      let msg =
        TopLevelMsg.Input(
          InputMsg.ActionStatesChanged(
            player,
            {
              ActionState.empty with
                  Held = Set [ MoveRight ]
            }
          )
        )

      let struct (world, _) = Program.update msg world
      let struct (world, _) = Program.update (FixedStep(1.f)) world

      let playerVel = world.Entities.Velocities[player]
      let unitVel = world.Entities.Velocities[unit]

      Expect.isGreaterThan
        (abs playerVel.X)
        0.001f
        "Player should have velocity"

      Expect.isGreaterThan (abs unitVel.X) 0.001f "Unit should have velocity"
      Expect.equal world.Movement.States[unit] Moving "Unit should be Moving"
    }
  ]
