module KipoRl.Tests.MovementTests

open Expecto
open KipoRl
open System.Numerics
open FSharp.UMX

let tests =
  testList "Movement" [
    test "PlayerMovementSystem sets velocity toward target" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Players.Add entityId |> ignore
      world.Entities.Positions[entityId] <- Vector3.Zero
      world.Movement.Targets[entityId] <- { X = 10.f; Y = 0.f; Z = 0.f }

      let struct (world, _) = PlayerMovementSystem.update world

      let vel = world.Entities.Velocities[entityId]
      Expect.isGreaterThan (abs vel.X) 0.001f "X velocity should be nonzero"
      Expect.equal world.Movement.States[entityId] Moving "Should be Moving"
    }

    test "PlayerMovementSystem sets Idle when no target" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Players.Add entityId |> ignore
      world.Entities.Positions[entityId] <- Vector3(5.f, 0.f, 5.f)

      let struct (world, _) = PlayerMovementSystem.update world

      Expect.equal world.Movement.States[entityId] Idle "Should be Idle"
    }

    test "MovementSystem applies velocity to position" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Entities.Positions[entityId] <- Vector3(1.f, 0.f, 1.f)
      world.Entities.Velocities[entityId] <- Vector3(2.f, 0.f, 2.f)

      let struct (world, _) = MovementSystem.update 0.5f world

      let pos = world.Entities.Positions[entityId]

      Expect.floatClose
        Accuracy.medium
        (float pos.X)
        2.0
        "X should be 1 + 2*0.5"

      Expect.floatClose
        Accuracy.medium
        (float pos.Z)
        2.0
        "Z should be 1 + 2*0.5"
    }
  ]
