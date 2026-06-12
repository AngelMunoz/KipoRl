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
    EffectProcessingE2ETests.tests
    NotificationE2ETests.tests
    PlayerMovementE2ETests.tests
  ]

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv allTests
