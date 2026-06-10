namespace KipoRl

open Mibo.Elmish
open Mibo.Input

module InputSystem =
  let update
    (entityId: int)
    (states: ActionState<GameAction>)
    (world: World)
    : Cmd<TopLevelMsg> =
    world.Input.ActionStates[entityId] <- states
    Cmd.none
