module KipoRl.Tests.PlayerMovementE2ETests

open System
open System.Collections.Generic
open System.Numerics
open Expecto
open KipoRl
open FSharp.UMX

let private makeWorld(entityId: int64<EntityId>) =
  let world = World()
  world.Players.Add entityId |> ignore
  world.Entities.Positions[entityId] <- Vector3.Zero
  world.Movement.Targets[entityId] <- { X = 10.f; Y = 0.f; Z = 0.f }
  world

let tests =
  testList "PlayerMovement E2E" [
    test "stunned player does not move" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      world.Combat.CombatStatuses[entityId] <-
        ResizeArray([ CombatStatus.Stunned ])

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      Expect.isFalse
        (world.Entities.Velocities.ContainsKey entityId)
        "Stunned player should not have velocity set"
    }

    test "player uses DerivedStats MS for speed" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      world.Combat.DerivedStats[entityId] <-
        {
          AP = 0
          AC = 0
          DX = 0
          MP = 0
          MA = 0
          MD = 0
          WT = 0
          DA = 0
          LK = 0
          HP = 100
          DP = 0
          HV = 0
          MS = 10
          HPRegen = 0
          MPRegen = 0
          ElementAttributes = Dictionary()
          ElementResistances = Dictionary()
        }

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let vel = world.Entities.Velocities[entityId]
      let expectedSpeed = 10.0

      Expect.floatClose
        Accuracy.medium
        (float(vel.Length()))
        expectedSpeed
        "Speed should match DerivedStats.MS"
    }

    test "player without DerivedStats uses default speed" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let vel = world.Entities.Velocities[entityId]

      Expect.floatClose
        Accuracy.medium
        (float(vel.Length()))
        5.0
        "Speed should be default 5"
    }
  ]
