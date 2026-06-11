module KipoRl.Tests.ResourceManagerE2ETests

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
  world.Players.Add entityId |> ignore
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

let private tickN (world: World) (n: int) =
  let mutable w = world

  for _ = 1 to n do
    let struct (w2, _) = Program.update (FixedStep(1.f / 60.f)) w
    w <- w2

  w

let tests =
  testList "ResourceManager E2E" [
    // ── Regen through pipeline ──

    test "FixedStep regens HP for entity" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let res = world.Combat.Resources[entityId]
      Expect.isGreaterThan res.HP 50 "HP should regen through pipeline"
    }

    test "FixedStep regens MP for entity" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let res = world.Combat.Resources[entityId]
      Expect.isGreaterThan res.MP 50 "MP should regen through pipeline"
    }

    test "multiple ticks accumulate regen" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60
      let w = tickN world 10

      let res = w.Combat.Resources[entityId]
      Expect.isGreaterThan res.HP 50 "HP should regen over multiple ticks"
      Expect.isGreaterThan res.MP 50 "MP should regen over multiple ticks"
    }

    // ── Overflow protection ──

    test "HP does not overflow max" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 99 50 100 100 60 60

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 100 "HP should cap at max, not overflow"
    }

    test "MP does not overflow max" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 99 100 100 60 60

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.MP 100 "MP should cap at max, not overflow"
    }

    test "HP at max stays at max" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 100 50 100 100 60 60

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 100 "HP should stay at max"
    }

    // ── Combat timer ──

    test "entity in combat does not regen" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60
      world.Combat.InCombatUntil[entityId] <- TimeSpan.MaxValue

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let res = world.Combat.Resources[entityId]
      Expect.equal res.HP 50 "HP should not regen in combat"
      Expect.equal res.MP 50 "MP should not regen in combat"
    }

    test "combat timer delays regen then allows it" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60
      world.Time.TotalGameTime <- TimeSpan.FromSeconds(10.0)
      world.Combat.InCombatUntil[entityId] <- TimeSpan.FromSeconds(15.0)

      // Tick at 10s — still in combat
      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      Expect.equal
        (world.Combat.Resources[entityId].HP)
        50
        "No regen during combat"

      // Advance past combat timer
      world.Time.TotalGameTime <- TimeSpan.FromSeconds(16.0)
      let w = tickN world 60

      Expect.isGreaterThan
        (w.Combat.Resources[entityId].HP)
        50
        "HP should regen after combat timer expires"
    }

    test "entity without combat timer regens immediately" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      let res = world.Combat.Resources[entityId]
      Expect.isGreaterThan res.HP 50 "HP should regen when no combat timer"
    }

    // ── Effects on regen ──

    test "changing DerivedStats changes regen rate" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 60 60

      // First tick with HPRegen=60 → regen 1
      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world
      Expect.equal (world.Combat.Resources[entityId].HP) 51 "HP should be 51"

      // Simulate debuff: reduce regen to 0
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

      let w = tickN world 60

      Expect.equal
        (w.Combat.Resources[entityId].HP)
        51
        "HP should not change with 0 regen"
    }

    // ── RestoreResource through pipeline ──

    test "RestoreResource message restores HP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 0 0

      let msg =
        TopLevelMsg.ResourceManager(
          ResourceManagerMsg.RestoreResource(entityId, HP, 30)
        )

      let struct (world, _) = Program.update msg world

      Expect.equal
        (world.Combat.Resources[entityId].HP)
        80
        "HP should increase by 30"
    }

    test "RestoreResource message restores MP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 0 0

      let msg =
        TopLevelMsg.ResourceManager(
          ResourceManagerMsg.RestoreResource(entityId, MP, 25)
        )

      let struct (world, _) = Program.update msg world

      Expect.equal
        (world.Combat.Resources[entityId].MP)
        75
        "MP should increase by 25"
    }

    test "RestoreResource caps at max HP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 90 50 100 100 0 0

      let msg =
        TopLevelMsg.ResourceManager(
          ResourceManagerMsg.RestoreResource(entityId, HP, 50)
        )

      let struct (world, _) = Program.update msg world

      Expect.equal
        (world.Combat.Resources[entityId].HP)
        100
        "HP should cap at max"
    }

    test "RestoreResource caps at max MP" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 90 100 100 0 0

      let msg =
        TopLevelMsg.ResourceManager(
          ResourceManagerMsg.RestoreResource(entityId, MP, 50)
        )

      let struct (world, _) = Program.update msg world

      Expect.equal
        (world.Combat.Resources[entityId].MP)
        100
        "MP should cap at max"
    }

    test "RestoreResource emits correct NotificationMsg" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 0 0

      let msg =
        TopLevelMsg.ResourceManager(
          ResourceManagerMsg.RestoreResource(entityId, HP, 30)
        )

      let struct (_, cmd) = Program.update msg world

      match cmd with
      | Cmd.Single effect ->
        let mutable found = false

        effect.Invoke(fun actual ->
          match actual with
          | Notification(ResourceRestored(entity, resource, amount)) ->
            Expect.equal entity entityId "Should target correct entity"
            Expect.equal resource HP "Should restore HP"
            Expect.equal amount 30 "Should restore 30"
            found <- true
          | other -> failwithf "Unexpected msg: %A" other)

        Expect.isTrue found "Should have emitted ResourceRestored notification"
      | other -> failwithf "Expected Cmd.Single, got %A" other
    }

    test "CombatMsg.EffectResource routes to ResourceManagerSystem" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId 50 50 100 100 0 0

      let msg = TopLevelMsg.Combat(CombatMsg.EffectResource(entityId, HP, 30))

      let struct (world, _) = Program.update msg world

      Expect.equal
        (world.Combat.Resources[entityId].HP)
        80
        "EffectResource should route to ResourceManager"
    }

    // ── Multi-entity independence ──

    test "multiple entities regen independently" {
      let e1 = UMX.tag 0L
      let e2 = UMX.tag 1L
      let world = World()
      world.Players.Add e1 |> ignore
      world.Players.Add e2 |> ignore
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

      let struct (world, _) = Program.update (FixedStep(1.f / 60.f)) world

      Expect.isGreaterThan (world.Combat.Resources[e1].HP) 50 "e1 should regen"

      Expect.equal
        (world.Combat.Resources[e2].HP)
        80
        "e2 should not regen (0 rate)"
    }

    // ── Safety ──

    test "entity with no DerivedStats does not crash" {
      let entityId = UMX.tag 0L
      let world = World()
      world.Entities.Positions[entityId] <- Vector3.Zero
      world.Combat.Resources[entityId] <- { HP = 50; MP = 50; Status = Alive }

      let struct (_, _) = Program.update (FixedStep(1.f / 60.f)) world
      ()
    }
  ]
