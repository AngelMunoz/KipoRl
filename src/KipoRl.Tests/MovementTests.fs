module KipoRl.Tests.MovementTests

open Expecto
open KipoRl
open Mibo.Input
open System.Numerics
open FSharp.UMX

let tests =
  testList "Movement" [
    test "PlayerMovementSystem sets velocity from input" {
      let world = World()
      let entityId = UMX.tag 0L
      world.Players.Add entityId |> ignore
      world.Entities.Positions[entityId] <- Vector3.Zero

      world.Input.ActionStates[entityId] <-
        {
          ActionState.empty with
              Held = Set [ MoveRight; MoveForward ]
        }

      let struct (world, _) = PlayerMovementSystem.update world

      let vel = world.Entities.Velocities[entityId]

      Expect.floatClose
        Accuracy.medium
        (float vel.X)
        5.0
        "X velocity should be moveSpeed"

      Expect.floatClose
        Accuracy.medium
        (float vel.Z)
        -5.0
        "Z velocity should be -moveSpeed"
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
