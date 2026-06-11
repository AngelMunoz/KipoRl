namespace KipoRl

open System
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
  | ResourceRestored of
    entityId: int64<EntityId> *
    resource: ResourceKind *
    amount: int

[<Struct>]
type ResourceManagerMsg =
  | RestoreResource of
    target: int64<EntityId> *
    resource: ResourceKind *
    amount: int

[<Struct>]
type EffectProcessingMsg =
  | ApplyEffect of target: int64<EntityId> * effect: ActiveEffect
  | RemoveEffect of target: int64<EntityId> * effectIndex: int

[<Struct>]
type CombatMsg =
  | AbilityIntent of
    caster: int64<EntityId> *
    skillId: int<SkillId> *
    skillTarget: SkillTarget
  | DamageDealt of dmgTarget: int64<EntityId> * amount: int * element: Element
  | EffectApplied of effectTarget: int64<EntityId> * effect: ActiveEffect
  | EffectDamage of
    effectDmgTarget: int64<EntityId> *
    amount: int *
    element: Element
  | EffectResource of
    resTarget: int64<EntityId> *
    resource: ResourceKind *
    amount: int
  | EntityDied of deadEntityId: int64<EntityId> * scenarioId: int64<ScenarioId>

[<Struct>]
type TopLevelMsg =
  | Tick of tick: GameTime
  | FixedStep of dt: float32
  | Input of inputMsg: InputMsg
  | Movement of movementMsg: MovementMsg
  | ResourceManager of resourceManagerMsg: ResourceManagerMsg
  | EffectProcessing of effectProcessingMsg: EffectProcessingMsg
  | Combat of combatMsg: CombatMsg
  | Notification of notificationMsg: NotificationMsg
