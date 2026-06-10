module KipoRl.Tests.InputSystemTests

open Expecto
open KipoRl
open Mibo.Input

let tests =
  testList "InputSystem" [
    test "stores action state for entity" {
      let world = World()

      let states = {
        ActionState.empty with
            Held = Set [ MoveLeft; MoveForward ]
            Started = Set [ MoveForward ]
      }

      InputSystem.update 0 states world

      let stored = world.Input.ActionStates[0]
      Expect.isTrue (stored.Held.Contains MoveLeft) "MoveLeft should be held"

      Expect.isTrue
        (stored.Held.Contains MoveForward)
        "MoveForward should be held"

      Expect.isTrue
        (stored.Started.Contains MoveForward)
        "MoveForward should be started"

      Expect.isFalse
        (stored.Held.Contains MoveRight)
        "MoveRight should not be held"
    }

    test "separate entities have independent input states" {
      let world = World()

      let playerState = {
        ActionState.empty with
            Held = Set [ MoveLeft ]
      }

      let enemyState = {
        ActionState.empty with
            Held = Set [ MoveRight ]
      }

      InputSystem.update 0 playerState world
      InputSystem.update 1 enemyState world

      Expect.isTrue
        (world.Input.ActionStates[0].Held.Contains MoveLeft)
        "Player should have MoveLeft"

      Expect.isTrue
        (world.Input.ActionStates[1].Held.Contains MoveRight)
        "Enemy should have MoveRight"

      Expect.isFalse
        (world.Input.ActionStates[0].Held.Contains MoveRight)
        "Player should not have MoveRight"
    }

    test "updating same entity overwrites previous state" {
      let world = World()

      let frame1 = {
        ActionState.empty with
            Held = Set [ MoveLeft ]
      }

      let frame2 = {
        ActionState.empty with
            Held = Set [ MoveRight ]
      }

      InputSystem.update 0 frame1 world
      InputSystem.update 0 frame2 world

      let stored = world.Input.ActionStates[0]

      Expect.isFalse
        (stored.Held.Contains MoveLeft)
        "MoveLeft should be gone after overwrite"

      Expect.isTrue
        (stored.Held.Contains MoveRight)
        "MoveRight should be present after overwrite"
    }
  ]
