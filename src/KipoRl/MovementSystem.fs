namespace KipoRl

open System.Numerics

module MovementSystem =
  let update (dt: float32) (world: World) =
    for KeyValue(entityId, velocity) in world.Entities.Velocities do
      match world.Entities.Positions.TryGetValue(entityId) with
      | true, pos -> world.Entities.Positions[entityId] <- pos + velocity * dt
      | false, _ -> world.Entities.Positions[entityId] <- velocity * dt
