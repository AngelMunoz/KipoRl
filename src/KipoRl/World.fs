namespace KipoRl

open System
open System.Collections.Generic
open System.Numerics
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
  member val ActionStates: Dictionary<int64<EntityId>, ActionState<GameAction>> =
    Dictionary() with get, set

type EntityState() =
  member val Positions: Dictionary<int64<EntityId>, Vector3> =
    Dictionary() with get, set

  member val Velocities: Dictionary<int64<EntityId>, Vector3> =
    Dictionary() with get, set

type World() =
  member val Input: InputState = InputState() with get, set
  member val Entities: EntityState = EntityState() with get, set
  member val Players: HashSet<int64<EntityId>> = HashSet() with get, set
