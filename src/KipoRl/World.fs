namespace KipoRl

open System
open System.Collections.Generic
open System.Numerics
open Mibo.Input

[<Struct>]
type GameAction =
  | PrimaryAction
  | SecondaryAction
  | UseSlot1
  | UseSlot2
  | UseSlot3
  | UseSlot4
  | UseSlot5
  | UseSlot6
  | UseSlot7
  | UseSlot8
  | SetActionSet1
  | SetActionSet2
  | SetActionSet3
  | SetActionSet4
  | SetActionSet5
  | SetActionSet6
  | SetActionSet7
  | SetActionSet8
  | Cancel
  | ToggleInventory
  | ToggleAbilities
  | ToggleCharacterSheet

type InputState() =
  member val ActionStates: Dictionary<int64<EntityId>, ActionState<GameAction>> =
    Dictionary() with get, set

type EntityState() =
  member val Positions: Dictionary<int64<EntityId>, Vector3> =
    Dictionary() with get, set

  member val Velocities: Dictionary<int64<EntityId>, Vector3> =
    Dictionary() with get, set

type MovementState() =
  member val States: Dictionary<int64<EntityId>, MovementStateKind> =
    Dictionary() with get, set

  member val Targets: Dictionary<int64<EntityId>, WorldPosition> =
    Dictionary() with get, set

type CombatState() =
  member val Resources: Dictionary<int64<EntityId>, Resource> =
    Dictionary() with get, set

  member val DerivedStats: Dictionary<int64<EntityId>, DerivedStats> =
    Dictionary() with get, set

  member val InCombatUntil: Dictionary<int64<EntityId>, TimeSpan> =
    Dictionary() with get, set

  member val RegenAccumulators: Dictionary<int64<EntityId>, struct (float32 * float32)> =
    Dictionary() with get, set

type World() =
  member val Input: InputState = InputState() with get, set
  member val Entities: EntityState = EntityState() with get, set
  member val Movement: MovementState = MovementState() with get, set
  member val Combat: CombatState = CombatState() with get, set
  member val Players: HashSet<int64<EntityId>> = HashSet() with get, set
