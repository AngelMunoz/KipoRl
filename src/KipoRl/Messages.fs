namespace KipoRl

open System.Numerics
open Mibo.Elmish
open Mibo.Input

[<Struct>]
type InputMsg =
  | ActionStatesChanged of
    entityId: int64<EntityId> *
    states: ActionState<GameAction>
  | MouseClick of entityId: int64<EntityId> * position: Vector2

[<Struct>]
type MovementMsg =
  | MovementTarget of entityId: int64<EntityId> * destination: WorldPosition
  | MovementStateChanged of entityId: int64<EntityId> * state: MovementStateKind
  | MovementPathChanged of entityId: int64<EntityId> * path: WorldPosition[]

[<Struct>]
type NotificationMsg =
  | ResourceRestored of entityId: int64<EntityId> * resource: ResourceKind * amount: int

[<Struct>]
type TopLevelMsg =
  | Tick of tick: GameTime
  | FixedStep of dt: float32
  | Input of inputMsg: InputMsg
  | Movement of movementMsg: MovementMsg
  | Notification of notificationMsg: NotificationMsg
