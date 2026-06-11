module KipoRl.Tests.InputSystemTests

open Expecto
open KipoRl
open Mibo.Input
open FSharp.UMX

let tests =
  testList "InputSystem" [
    test "stores action state for entity" {
      let world = World()

      let states = {
        ActionState.empty with
            Held = Set [ UseSlot1; UseSlot2 ]
            Started = Set [ UseSlot2 ]
      }

      InputSystem.update (UMX.tag 0L) states world

      let stored = world.Input.ActionStates[UMX.tag 0L]
      Expect.isTrue (stored.Held.Contains UseSlot1) "UseSlot1 should be held"
      Expect.isTrue (stored.Held.Contains UseSlot2) "UseSlot2 should be held"

      Expect.isTrue
        (stored.Started.Contains UseSlot2)
        "UseSlot2 should be started"

      Expect.isFalse
        (stored.Held.Contains UseSlot3)
        "UseSlot3 should not be held"
    }

    test "separate entities have independent input states" {
      let world = World()

      let playerState = {
        ActionState.empty with
            Held = Set [ UseSlot1 ]
      }

      let enemyState = {
        ActionState.empty with
            Held = Set [ UseSlot3 ]
      }

      InputSystem.update (UMX.tag 0L) playerState world
      InputSystem.update (UMX.tag 1L) enemyState world

      Expect.isTrue
        (world.Input.ActionStates[UMX.tag 0L].Held.Contains UseSlot1)
        "Player should have UseSlot1"

      Expect.isTrue
        (world.Input.ActionStates[UMX.tag 1L].Held.Contains UseSlot3)
        "Enemy should have UseSlot3"

      Expect.isFalse
        (world.Input.ActionStates[UMX.tag 0L].Held.Contains UseSlot3)
        "Player should not have UseSlot3"
    }

    test "updating same entity overwrites previous state" {
      let world = World()

      let frame1 = {
        ActionState.empty with
            Held = Set [ UseSlot1 ]
      }

      let frame2 = {
        ActionState.empty with
            Held = Set [ UseSlot2 ]
      }

      InputSystem.update (UMX.tag 0L) frame1 world
      InputSystem.update (UMX.tag 0L) frame2 world

      let stored = world.Input.ActionStates[UMX.tag 0L]

      Expect.isFalse
        (stored.Held.Contains UseSlot1)
        "UseSlot1 should be gone after overwrite"

      Expect.isTrue
        (stored.Held.Contains UseSlot2)
        "UseSlot2 should be present after overwrite"
    }
  ]
