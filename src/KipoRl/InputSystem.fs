namespace KipoRl

open Mibo.Input

module InputSystem =
  let update
    (entityId: int64<EntityId>)
    (states: ActionState<GameAction>)
    (world: World)
    =
    world.Input.ActionStates[entityId] <- states
