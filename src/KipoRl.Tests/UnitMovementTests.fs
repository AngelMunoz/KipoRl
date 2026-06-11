module KipoRl.Tests.UnitMovementTests

open Expecto
open KipoRl
open System.Numerics
open FSharp.UMX

let tests =
  testList "UnitMovementSystem" [
    test "sets velocity toward target and state to Moving" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Entities.Positions[entityId] <- Vector3(0.f, 0.f, 0.f)
      world.Movement.Targets[entityId] <- { X = 10.f; Y = 0.f; Z = 0.f }

      let struct (_, _) = UnitMovementSystem.update 1.f world

      let vel = world.Entities.Velocities[entityId]
      Expect.isGreaterThan (abs vel.X) 0.001f "X velocity should be nonzero"
      Expect.equal world.Movement.States[entityId] Moving "Should be Moving"
    }

    test "sets state to Idle when no target" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Entities.Positions[entityId] <- Vector3(5.f, 0.f, 5.f)

      let struct (_, _) = UnitMovementSystem.update 1.f world

      Expect.equal world.Movement.States[entityId] Idle "Should be Idle"
    }

    test "clears target and sets Idle when reached" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Entities.Positions[entityId] <- Vector3(10.f, 0.f, 0.f)
      world.Movement.Targets[entityId] <- { X = 10.f; Y = 0.f; Z = 0.f }

      let struct (_, _) = UnitMovementSystem.update 1.f world

      Expect.isFalse
        (world.Movement.Targets.ContainsKey entityId)
        "Target should be cleared"

      Expect.equal
        world.Movement.States[entityId]
        Idle
        "Should be Idle when reached"

      Expect.equal
        world.Entities.Velocities[entityId]
        Vector3.Zero
        "Velocity should be zero when idle"
    }

    test "does not move player entities" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Players.Add entityId |> ignore
      world.Entities.Positions[entityId] <- Vector3(0.f, 0.f, 0.f)
      world.Movement.Targets[entityId] <- { X = 10.f; Y = 0.f; Z = 0.f }

      let struct (_, _) = UnitMovementSystem.update 1.f world

      Expect.isFalse
        (world.Entities.Velocities.ContainsKey entityId)
        "Player velocity should not be set by UnitMovementSystem"

      Expect.isTrue
        (world.Movement.Targets.ContainsKey entityId)
        "Player target should remain untouched"
    }
  ]
