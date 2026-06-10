namespace KipoRl

open System
open System.Collections.Generic
open System.Numerics
open FSharp.UMX

[<Measure>]
type EntityId

[<Measure>]
type SkillId

[<Measure>]
type ItemId

[<Measure>]
type ItemInstanceId

[<Measure>]
type ScenarioId

[<Measure>]
type AiArchetypeId

[<Struct>]
type WorldPosition = { X: float32; Y: float32; Z: float32 }

module WorldPosition =
  let zero = { X = 0.0f; Y = 0.0f; Z = 0.0f }

  let inline fromVector2(v: Vector2) = { X = v.X; Y = 0f; Z = v.Y }

  let inline toVector2(p: WorldPosition) = Vector2(p.X, p.Z)

  let inline fromVector3(v: Vector3) = { X = v.X; Y = v.Y; Z = v.Z }

  let inline toVector3(p: WorldPosition) = Vector3(p.X, p.Y, p.Z)

  let inline distance (a: WorldPosition) (b: WorldPosition) =
    let dx = a.X - b.X
    let dy = a.Y - b.Y
    let dz = a.Z - b.Z
    sqrt (dx * dx + dy * dy + dz * dz)

[<Struct>]
type Element =
  | Fire
  | Water
  | Earth
  | Air
  | Lightning
  | Light
  | Dark
  | Neutral

[<Struct>]
type CombatStatus =
  | Stunned
  | Silenced
  | Rooted

[<Struct>]
type Status =
  | Alive
  | Dead

[<Struct>]
type Faction =
  | Player
  | NPC
  | Ally
  | Enemy
  | AIControlled
  | TeamRed
  | TeamBlue
  | TeamGreen
  | TeamYellow
  | TeamOrange
  | TeamPurple
  | TeamPink
  | TeamCyan
  | TeamWhite
  | TeamBlack

[<Struct>]
type Resource = { HP: int; MP: int; Status: Status }

[<Struct>]
type Family =
  | Power
  | Magic
  | Charm
  | Sense

[<Struct>]
type Stage =
  | First
  | Second
  | Third

[<Struct>]
type Profession = { Family: Family; Stage: Stage }

[<Struct>]
type BaseStats = {
  Power: int
  Magic: int
  Sense: int
  Charm: int
}

[<Struct>]
type DerivedStats = {
  AP: int
  AC: int
  DX: int
  MP: int
  MA: int
  MD: int
  WT: int
  DA: int
  LK: int
  HP: int
  DP: int
  HV: int
  MS: int
  HPRegen: int
  MPRegen: int
  ElementAttributes: Dictionary<Element, float>
  ElementResistances: Dictionary<Element, float>
}

[<Struct>]
type Slot =
  | Head
  | Chest
  | Legs
  | Feet
  | Hands
  | Weapon
  | Shield
  | Accessory

[<Struct>]
type Stat =
  | AP
  | AC
  | DX
  | MP
  | MA
  | MD
  | WT
  | DA
  | LK
  | HP
  | DP
  | HV
  | MS
  | HPRegen
  | MPRegen
  | ElementResistance of ofElement: Element
  | ElementAttribute of ofElement: Element

[<Struct>]
type StatModifier =
  | Additive of addStat: Stat * adStatValue: float
  | Multiplicative of mulStat: Stat * mulStatValue: float

[<Struct>]
type SlotProcessing =
  | Skill of skillId: int<SkillId>
  | Item of itemInstanceId: Guid<ItemInstanceId>

[<Struct>]
type VisualManifest = {
  ModelId: string voption
  VfxId: string voption
  AnimationId: string voption
  AttachmentPoint: string voption
}

module VisualManifest =
  let empty = {
    ModelId = ValueNone
    VfxId = ValueNone
    AnimationId = ValueNone
    AttachmentPoint = ValueNone
  }

[<Struct>]
type EffectKind =
  | Buff
  | Debuff
  | DamageOverTime
  | ResourceOverTime
  | Stun
  | Silence
  | Taunt

[<Struct>]
type StackingRule =
  | NoStack
  | StackDuration
  | StackIntensity

[<Struct>]
type ResourceKind =
  | HP
  | MP

[<Struct>]
type SkillTarget =
  | SelfTarget
  | EntityTarget of entity: Guid<EntityId>
  | PositionTarget of position: WorldPosition

[<Struct>]
type CollisionMode =
  | IgnoreTerrain
  | BlockedByTerrain

[<Struct>]
type ExtraVariations =
  | Chained of jumpsLeft: int * maxRange: float32
  | Bouncing of bouncesLeft: int
  | Descending of currentAltitude: float32 * fallSpeed: float32

[<Struct>]
type ProjectileTarget =
  | EntityTarget of entity: Guid<EntityId>
  | PositionTarget of position: WorldPosition

[<Struct>]
type ProjectileInfo = {
  Speed: float32
  Collision: CollisionMode
  Variations: ExtraVariations voption
  Visuals: VisualManifest
  TerrainImpactVisuals: VisualManifest voption
}

[<Struct>]
type LiveProjectile = {
  Caster: Guid<EntityId>
  Target: ProjectileTarget
  SkillId: int<SkillId>
  Info: ProjectileInfo
}

[<Struct>]
type BlockType =
  | Empty
  | Solid
  | Platform
  | Lava
  | Water

[<Struct>]
type MovementStateKind =
  | Idle
  | Moving
  | Casting
  | Stunned

[<Struct>]
type NotificationType =
  | Damage
  | Heal
  | Info
  | Warning

[<Struct>]
type SpawnType =
  | PlayerSpawn
  | EnemySpawn of archetype: Guid<AiArchetypeId>
  | ItemSpawn of itemId: int<ItemId>

[<Struct>]
type SpawnZone = {
  Center: WorldPosition
  Radius: float32
  SpawnType: SpawnType
  MaxCount: int
  RespawnTime: TimeSpan
}

[<Struct>]
type ActiveEffect = {
  SourceSkillId: int<SkillId>
  SourceEffectKind: EffectKind
  RemainingDuration: TimeSpan
  StackingRule: StackingRule
  Intensity: int
  Visual: VisualManifest voption
}

[<Struct>]
type ItemDefinition = {
  Id: int<ItemId>
  Name: string
  Weight: int
  Slot: Slot voption
  Stats: StatModifier[]
}

[<Struct>]
type ItemInstance = {
  InstanceId: Guid<ItemInstanceId>
  ItemId: int<ItemId>
  UsesLeft: int voption
}

[<Struct>]
type WorldText = {
  Text: string
  Position: WorldPosition
  Velocity: Vector3
  Color: System.Numerics.Vector4
  RemainingLife: TimeSpan
  Type: NotificationType
}

[<Struct>]
type VisualEffect = {
  Position: WorldPosition
  Velocity: Vector3
  RemainingLife: TimeSpan
  Visual: VisualManifest
}
