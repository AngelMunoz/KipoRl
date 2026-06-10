namespace KipoRl

open Mibo.Input

[<Struct>]
type InputMsg =
    | ActionStatesChanged of entityId: int * states: ActionState<GameAction>

[<Struct>]
type TopLevelMsg =
    | Tick of tick: GameTime
    | FixedStep of dt: float32
    | Input of msg: InputMsg
