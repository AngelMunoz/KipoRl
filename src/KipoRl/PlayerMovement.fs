namespace KipoRl

open System.Numerics
open Mibo.Elmish

module PlayerMovementSystem =
  // TODO: Replace with DerivedStatsCache MS when combat system lands
  let moveSpeed = 5.f
  let arrivalThreshold = 0.5f

  let update(world: World) : struct (World * Cmd<TopLevelMsg>) =
    for KeyValue(entityId, _) in world.Entities.Positions do
      if not(world.Players.Contains entityId) then
        ()
      else
        match world.Movement.Targets |> Dictionary.tryFindV entityId with
        | ValueSome target ->
          let pos = world.Entities.Positions[entityId]

          let dir =
            Vector3(target.X - pos.X, target.Y - pos.Y, target.Z - pos.Z)

          let dist = dir.Length()

          if dist < arrivalThreshold then
            world.Movement.Targets.Remove(entityId) |> ignore
            world.Movement.States[entityId] <- Idle
            world.Entities.Velocities[entityId] <- Vector3.Zero
          else
            let vel = Vector3.Normalize(dir) * moveSpeed
            world.Entities.Velocities[entityId] <- vel
            world.Movement.States[entityId] <- Moving

        | ValueNone ->
          match world.Movement.States |> Dictionary.tryFindV entityId with
          | ValueSome Moving ->
            world.Movement.States[entityId] <- Idle
            world.Entities.Velocities[entityId] <- Vector3.Zero
          | ValueSome Idle
          | ValueSome Casting
          | ValueSome Stunned -> ()
          | ValueNone -> world.Movement.States[entityId] <- Idle

    struct (world, Cmd.none)
