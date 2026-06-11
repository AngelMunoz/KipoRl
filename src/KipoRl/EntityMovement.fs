namespace KipoRl

open System.Numerics
open Mibo.Elmish

module MovementSystem =
  let handleMsg (world: World) (msg: MovementMsg) =
    match msg with
    | MovementTarget(entityId, destination) ->
      world.Movement.Targets[entityId] <- destination
    | MovementStateChanged(entityId, state) ->
      world.Movement.States[entityId] <- state
    | MovementPathChanged _ -> ()

  let update (dt: float32) (world: World) : struct (World * Cmd<_>) =
    for KeyValue(entityId, velocity) in world.Entities.Velocities do
      world.Entities.Positions
      |> Dictionary.tryFindV entityId
      |> ValueOption.iter(fun pos ->
        world.Entities.Positions[entityId] <- pos + velocity * dt)

    world, Cmd.none
