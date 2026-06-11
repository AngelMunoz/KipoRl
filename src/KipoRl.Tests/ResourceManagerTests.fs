module KipoRl.Tests.ResourceManagerTests

open System
open System.Collections.Generic
open System.Numerics
open Expecto
open KipoRl
open Mibo.Elmish
open FSharp.UMX

let private makeWorld
  (entityId: int64<EntityId>)
  (hp: int)
  (mp: int)
  (maxHp: int)
  (maxMp: int)
  (hpRegen: int)
  (mpRegen: int)
  =
  let world = World()
  world.Entities.Positions[entityId] <- Vector3.Zero
  world.Combat.Resources[entityId] <- { HP = hp; MP = mp; Status = Alive }

  world.Combat.DerivedStats[entityId] <-
    {
      AP = 0
      AC = 0
      DX = 0
      MP = maxMp
      MA = 0
      MD = 0
      WT = 0
      DA = 0
      LK = 0
      HP = maxHp
      DP = 0
      HV = 0
      MS = 0
      HPRegen = hpRegen
      MPRegen = mpRegen
      ElementAttributes = Dictionary()
      ElementResistances = Dictionary()
    }

  world

let private tickN (world: World) (n: int) (dt: float32) =
  let mutable w = world

  for _ = 1 to n do
    let struct (w2, _) = ResourceManagerSystem.update dt w
    w <- w2

  w

let tests =
  testList "ResourceManager" [
    test "entity not in combat regens HP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.isGreaterThan res.HP 50 "HP should have increased"
    }

    test "entity not in combat regens MP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.isGreaterThan res.MP 50 "MP should have increased"
    }

    test "HP does not overflow max" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 99 50 100 100 60 10

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 100 "HP should cap at max, not overflow"
    }

    test "MP does not overflow max" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 99 100 100 10 60

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.MP 100 "MP should cap at max, not overflow"
    }

    test "HP already at max does not change" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 100 50 100 100 60 60

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 100 "HP should stay at max"
    }

    test "entity in combat does not regen" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60
      world.Combat.InCombatUntil[entityId] <- TimeSpan.MaxValue

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 50 "HP should not change in combat"
      Expect.equal res.MP 50 "MP should not change in combat"
    }

    test "combat timer delays regen for 5 seconds" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60
      world.Time.TotalGameTime <- TimeSpan.FromSeconds(10.0)
      world.Time.Delta <- TimeSpan.FromSeconds(1.0 / 60.0)

      world.Combat.InCombatUntil[entityId] <- TimeSpan.FromSeconds(15.0)

      // Tick at 10s — still in combat
      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world
      let res1 = world.Combat.Resources[entityId]
      Expect.equal res1.HP 50 "HP should not regen during combat window"

      // Advance to 16s — combat timer expired, tick enough to regen
      world.Time.TotalGameTime <- TimeSpan.FromSeconds(16.0)
      let w = tickN world 60 (1.f / 60.f)
      let res2 = w.Combat.Resources[entityId]

      Expect.isGreaterThan
        res2.HP
        50
        "HP should regen after combat timer expires"
    }

    test "entity without combat timer regens immediately" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.isGreaterThan res.HP 50 "HP should regen when no combat timer"
    }

    test "accumulator carries fractional regen across ticks" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 30 30

      // HPRegen=30, dt=1/60 → 0.5 per tick. After 1 tick: acc=0.5, hpGain=0
      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world
      let res1 = world.Combat.Resources[entityId]
      Expect.equal res1.HP 50 "HP should not change after 1 tick (acc=0.5)"

      // After 2 ticks: acc=1.0, hpGain=1
      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world
      let res2 = world.Combat.Resources[entityId]
      Expect.equal res2.HP 51 "HP should regen 1 after 2 ticks"
    }

    test "multiple entities regen independently" {
      let e1 = UMX.tag 0L
      let e2 = UMX.tag 1L
      let world = World()
      world.Entities.Positions[e1] <- Vector3.Zero
      world.Entities.Positions[e2] <- Vector3.Zero
      world.Combat.Resources[e1] <- { HP = 50; MP = 50; Status = Alive }
      world.Combat.Resources[e2] <- { HP = 80; MP = 80; Status = Alive }

      world.Combat.DerivedStats[e1] <-
        {
          AP = 0
          AC = 0
          DX = 0
          MP = 100
          MA = 0
          MD = 0
          WT = 0
          DA = 0
          LK = 0
          HP = 100
          DP = 0
          HV = 0
          MS = 0
          HPRegen = 60
          MPRegen = 60
          ElementAttributes = Dictionary()
          ElementResistances = Dictionary()
        }

      world.Combat.DerivedStats[e2] <-
        {
          AP = 0
          AC = 0
          DX = 0
          MP = 100
          MA = 0
          MD = 0
          WT = 0
          DA = 0
          LK = 0
          HP = 100
          DP = 0
          HV = 0
          MS = 0
          HPRegen = 0
          MPRegen = 0
          ElementAttributes = Dictionary()
          ElementResistances = Dictionary()
        }

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res1 = world.Combat.Resources[e1]
      let res2 = world.Combat.Resources[e2]
      Expect.isGreaterThan res1.HP 50 "e1 should regen"
      Expect.equal res2.HP 80 "e2 should not regen (0 rate)"
    }

    test "effects modifying DerivedStats change regen rate" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      // Tick once with HPRegen=60 → should regen 1
      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world
      let hpAfterFirst = world.Combat.Resources[entityId].HP
      Expect.equal hpAfterFirst 51 "HP should be 51 after first tick"

      // Simulate debuff reducing regen to 0
      world.Combat.DerivedStats[entityId] <-
        {
          AP = 0
          AC = 0
          DX = 0
          MP = 100
          MA = 0
          MD = 0
          WT = 0
          DA = 0
          LK = 0
          HP = 100
          DP = 0
          HV = 0
          MS = 0
          HPRegen = 0
          MPRegen = 0
          ElementAttributes = Dictionary()
          ElementResistances = Dictionary()
        }

      // Tick 60 more times — should NOT regen (rate=0)
      let w = tickN world 60 (1.f / 60.f)
      let res2 = w.Combat.Resources[entityId]
      Expect.equal res2.HP 51 "HP should not change with 0 regen rate"
    }

    test "zero regen rate means no change" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 0 0

      let struct (world, _) = ResourceManagerSystem.update (1.f / 60.f) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 50 "HP should not change with 0 regen"
      Expect.equal res.MP 50 "MP should not change with 0 regen"
    }

    test "RestoreResource restores HP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 0 0

      let msg = ResourceManagerMsg.RestoreResource(entityId, HP, 30)
      let _cmd = ResourceManagerSystem.handleMsg world msg

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 80 "HP should increase by 30"
    }

    test "RestoreResource restores MP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 0 0

      let msg = ResourceManagerMsg.RestoreResource(entityId, MP, 25)
      let _cmd = ResourceManagerSystem.handleMsg world msg

      let res = world.Combat.Resources[entityId]
      Expect.equal res.MP 75 "MP should increase by 25"
    }

    test "RestoreResource caps at max HP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 90 50 100 100 0 0

      let msg = ResourceManagerMsg.RestoreResource(entityId, HP, 50)
      let _cmd = ResourceManagerSystem.handleMsg world msg

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 100 "HP should cap at max"
    }

    test "RestoreResource caps at max MP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 90 100 100 0 0

      let msg = ResourceManagerMsg.RestoreResource(entityId, MP, 50)
      let _cmd = ResourceManagerSystem.handleMsg world msg

      let res = world.Combat.Resources[entityId]
      Expect.equal res.MP 100 "MP should cap at max"
    }

    test "RestoreResource on entity without DerivedStats uses current HP as max" {
      let entityId = UMX.tag 0L
      let world = World()
      world.Entities.Positions[entityId] <- Vector3.Zero
      world.Combat.Resources[entityId] <- { HP = 50; MP = 50; Status = Alive }

      let msg = ResourceManagerMsg.RestoreResource(entityId, HP, 100)
      let _cmd = ResourceManagerSystem.handleMsg world msg

      let res = world.Combat.Resources[entityId]

      Expect.equal
        res.HP
        50
        "HP should not exceed current (no DerivedStats = no max info)"
    }
  ]
