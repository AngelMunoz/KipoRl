module KipoRl.Tests.InputSystemTests

open Expecto
open KipoRl
open Mibo.Input

let tests =
  testList "InputSystem" [
    test "InputSystem stores action states in world" {
      let world = World()
      let states = ActionState.empty
      InputSystem.update 0 states world

      Expect.isTrue
        (world.Input.ActionStates.ContainsKey(0))
        "Should store states for entity 0"
    }
  ]
