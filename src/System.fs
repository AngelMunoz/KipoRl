namespace KipoRl

[<AutoOpen>]
module System =
    let start (world: World) : struct (World * Cmd<TopLevelMsg>) =
        struct (world, Cmd.none)

    let pipeMutable (f: World -> Cmd<TopLevelMsg>) (struct (world, acc): struct (World * Cmd<TopLevelMsg>)) : struct (World * Cmd<TopLevelMsg>) =
        let cmd = f world
        struct (world, Cmd.batch [ acc; cmd ])

    let finish (pair: struct (World * Cmd<TopLevelMsg>)) : struct (World * Cmd<TopLevelMsg>) =
        pair
