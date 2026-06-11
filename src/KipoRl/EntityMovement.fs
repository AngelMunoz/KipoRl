namespace KipoRl

open System.Numerics
open Mibo.Elmish

module MovementSystem =
  let update (dt: float32) (world: World) : struct (World * Cmd<_>) =
    for KeyValue(entityId, velocity) in world.Entities.Velocities do
      world.Entities.Positions
      |> Dictionary.tryFindV entityId
      // only act if entity found
      |> ValueOption.iter(fun pos ->
        world.Entities.Positions[entityId] <- pos + velocity * dt)

    world, Cmd.none
