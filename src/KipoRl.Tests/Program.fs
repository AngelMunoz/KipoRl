module KipoRl.Tests.Program

open Expecto

let allTests = testList "All" [ WorldTests.tests; InputSystemTests.tests ]

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv allTests
