module KipoRl.Tests.WorldTests

open Expecto
open KipoRl

let tests =
  testList "World" [
    test "World initializes with empty InputState" {
      let world = World()

      Expect.equal
        (world.Input.ActionStates.Count)
        0
        "ActionStates should be empty"
    }
  ]
