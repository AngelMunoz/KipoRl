namespace KipoRl

open System.Collections.Generic
open Mibo.Input

[<Struct>]
type GameAction =
    | MoveForward
    | MoveBackward
    | MoveLeft
    | MoveRight
    | MoveUp
    | MoveDown

type InputState() =
    member val ActionStates: Dictionary<int, ActionState<GameAction>> = Dictionary() with get, set

type World() =
    member val Input: InputState = InputState() with get, set
