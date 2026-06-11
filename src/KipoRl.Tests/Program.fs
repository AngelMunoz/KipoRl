module KipoRl.Tests.Program

open Expecto

let allTests =
  testList "All" [
    InputSystemTests.tests
    MovementTests.tests
    UnitMovementTests.tests
    UnitMovementE2ETests.tests
    ResourceManagerTests.tests
    ResourceManagerE2ETests.tests
  ]

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv allTests
