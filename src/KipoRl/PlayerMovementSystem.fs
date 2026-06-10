namespace KipoRl

open System.Numerics

module PlayerMovementSystem =
  open Mibo.Elmish
  let moveSpeed = 5.f

  let update(world: World) : struct (World * Cmd<_>) =
    for KeyValue(entityId, states) in world.Input.ActionStates do
      if not(world.Players.Contains entityId) then
        ()
      else

      let dx =
        if states.Held.Contains MoveLeft then -moveSpeed
        elif states.Held.Contains MoveRight then moveSpeed
        else 0.f

      let dy =
        if states.Held.Contains MoveUp then moveSpeed
        elif states.Held.Contains MoveDown then -moveSpeed
        else 0.f

      let dz =
        if states.Held.Contains MoveForward then -moveSpeed
        elif states.Held.Contains MoveBackward then moveSpeed
        else 0.f

      world.Entities.Velocities[entityId] <- Vector3(dx, dy, dz)

    world, Cmd.none
