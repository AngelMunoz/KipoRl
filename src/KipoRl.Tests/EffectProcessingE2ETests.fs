module KipoRl.Tests.EffectProcessingE2ETests

open System
open System.Collections.Generic
open System.Numerics
open Expecto
open KipoRl
open Mibo.Elmish
open FSharp.UMX

let private makeWorld(entityId: int64<EntityId>) =
  let world = World()
  world.Players.Add entityId |> ignore
  world.Entities.Positions[entityId] <- Vector3.Zero
  world.Combat.Resources[entityId] <- { HP = 100; MP = 100; Status = Alive }

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

  world

let private makeEffect
  (kind: EffectKind)
  (duration: Duration)
  (intensity: int)
  =
  {
    Id = UMX.tag<EffectId> 0
    SourceEffect = {
      Name = "test"
      Kind = kind
      DamageSource = Physical
      Stacking = NoStack
      Duration = duration
      Visuals = VisualManifest.empty
      Modifiers = [||]
    }
    SourceEntity = UMX.tag 0L
    TargetEntity = UMX.tag 0L
    StartTime = TimeSpan.Zero
    StackCount = intensity
  }

let private applyEffect
  (world: World)
  (entityId: int64<EntityId>)
  (effect: ActiveEffect)
  =
  let msg = TopLevelMsg.EffectProcessing(ApplyEffect(entityId, effect))
  let struct (world, _) = Program.update msg world
  world

let private collectMsgs(cmd: Cmd<TopLevelMsg>) =
  let msgs = ResizeArray<TopLevelMsg>()

  let rec go(c: Cmd<TopLevelMsg>) =
    match c with
    | Cmd.Empty -> ()
    | Cmd.Quit -> ()
    | Cmd.DeferNextFrame _ -> ()
    | Cmd.Single eff -> eff.Invoke(fun m -> msgs.Add m)
    | Cmd.Batch effs ->
      for e in effs do
        e.Invoke(fun m -> msgs.Add m)
    | Cmd.NowAndDeferNextFrame(now, _) ->
      for e in now do
        e.Invoke(fun m -> msgs.Add m)

  go cmd
  msgs

let private tickAndCollect(world: World) =
  let struct (w, cmd) = Program.update (FixedStep(1.f / 60.f)) world
  struct (w, collectMsgs cmd)

let private tickNCollect (world: World) (n: int) =
  let mutable w = world
  let allMsgs = ResizeArray<TopLevelMsg>()

  for _ = 1 to n do
    let struct (w2, msgs) = tickAndCollect w
    w <- w2
    allMsgs.AddRange(msgs)

  struct (w, allMsgs)

let private tickN (world: World) (n: int) =
  let struct (w, _) = tickNCollect world n
  w

let tests =
  testList "EffectProcessing E2E" [
    // ── Expiry ──

    test "timed effect expires after duration" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let effect = makeEffect Stun (Timed(TimeSpan.FromSeconds(2.0))) 0
      let world = applyEffect world entityId effect

      Expect.equal
        (world.Combat.ActiveEffects[entityId].Count)
        1
        "Effect should be applied"

      let w = tickN world 126

      Expect.equal
        (w.Combat.ActiveEffects[entityId].Count)
        0
        "Effect should expire"
    }

    test "timed effect emits EffectExpired on expiry" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let effect = makeEffect Stun (Timed(TimeSpan.FromSeconds(1.0))) 0
      let world = applyEffect world entityId effect

      let struct (_, msgs) = tickNCollect world 66

      let hasExpired =
        msgs
        |> Seq.exists(fun m ->
          match m with
          | EffectProcessing(EffectExpired _) -> true
          | _ -> false)

      Expect.isTrue hasExpired "Should emit EffectExpired"
    }

    test "timed effect persists before duration" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let effect = makeEffect Stun (Timed(TimeSpan.FromSeconds(10.0))) 0
      let world = applyEffect world entityId effect

      let w = tickN world 120

      Expect.equal
        (w.Combat.ActiveEffects[entityId].Count)
        1
        "Effect should still be active"
    }

    test "permanent effect never expires" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let effect = makeEffect Stun Permanent 0
      let world = applyEffect world entityId effect

      let w = tickN world 600

      Expect.equal
        (w.Combat.ActiveEffects[entityId].Count)
        1
        "Permanent effect should never expire"
    }

    // ── DoT emission ──

    test "DamageOverTime loop emits correct number of ticks with correct values" {
      let entityId = UMX.tag 42L
      let world = makeWorld entityId

      let effect =
        makeEffect
          DamageOverTime
          (Loop(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(5.0)))
          10

      let world = applyEffect world entityId effect

      // Tick 3.5s (210 ticks) to ensure 3rd tick at t≈3.0 fires
      let struct (_, msgs) = tickNCollect world 210

      let damages =
        msgs
        |> Seq.choose(fun m ->
          match m with
          | Combat(CombatMsg.EffectDamage(target, amount, _)) ->
            Some(target, amount)
          | _ -> None)
        |> Seq.toList

      Expect.equal damages.Length 3 "Should have 3 DoT ticks in 3.5 seconds"

      for (target, amount) in damages do
        Expect.equal target entityId "Each tick should target correct entity"
        Expect.equal amount 10 "Each tick should use StackCount as damage"
    }

    // ── HoT emission ──

    test
      "ResourceOverTime loop emits correct number of ticks with correct values" {
      let entityId = UMX.tag 42L
      let world = makeWorld entityId

      world.Combat.Resources[entityId] <- { HP = 0; MP = 100; Status = Alive }

      let effect =
        makeEffect
          ResourceOverTime
          (Loop(TimeSpan.FromSeconds 1.0, TimeSpan.FromSeconds 5.0))
          15

      let world = applyEffect world entityId effect

      let struct (_, msgs) = tickNCollect world 210

      let restores =
        msgs
        |> Seq.choose(fun m ->
          match m with
          | ResourceManager(RestoreResource(target, resource, amount)) ->
            Some(target, resource, amount)
          | _ -> None)
        |> Seq.toList

      Expect.equal restores.Length 3 "Should have 3 HoT ticks in 3.5 seconds"

      for (target, resource, amount) in restores do
        Expect.equal target entityId "Each tick should target correct entity"
        Expect.equal resource HP "Each tick should restore HP"
        Expect.equal amount 15 "Each tick should restore StackCount amount"
    }

    test "ResourceOverTime stops emitting after expiry" {
      let entityId = UMX.tag 42L
      let world = makeWorld entityId

      world.Combat.Resources[entityId] <- { HP = 0; MP = 100; Status = Alive }

      let effect =
        makeEffect
          ResourceOverTime
          (Loop(TimeSpan.FromSeconds 1., TimeSpan.FromSeconds 3.))
          10

      let world = applyEffect world entityId effect

      let struct (_, msgs) = tickNCollect world 210

      let restores =
        msgs
        |> ResizeArray.chooseV(fun m ->
          match m with
          | ResourceManager(RestoreResource(target, resource, amount)) ->
            ValueSome(target, resource, amount)
          | _ -> ValueNone)

      Expect.equal restores.Count 2 "Should emit exactly 2 ticks then stop"
    }

    // ── PermanentLoop emission ──

    test "PermanentLoop emits correct number of ticks with correct values" {
      let entityId = UMX.tag 42L
      let world = makeWorld entityId

      world.Combat.Resources[entityId] <- { HP = 0; MP = 100; Status = Alive }

      let effect =
        makeEffect ResourceOverTime (PermanentLoop(TimeSpan.FromSeconds(1.0))) 7

      let world = applyEffect world entityId effect

      let struct (_, msgs) = tickNCollect world 210

      let restores =
        msgs
        |> Seq.choose(fun m ->
          match m with
          | ResourceManager(RestoreResource(target, resource, amount)) ->
            Some(target, resource, amount)
          | _ -> None)
        |> Seq.toList

      Expect.equal
        restores.Length
        3
        "Should emit every interval (3 ticks in 3.5s)"

      for (target, resource, amount) in restores do
        Expect.equal target entityId "Each tick should target correct entity"
        Expect.equal resource HP "Each tick should restore HP"
        Expect.equal amount 7 "Each tick should restore StackCount amount"
    }

    // ── No tick before interval ──

    test "loop does not emit before first interval" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      let effect =
        makeEffect
          DamageOverTime
          (Loop(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(5.0)))
          10

      let world = applyEffect world entityId effect

      // Single tick at t=1/60 — should not emit
      let struct (_, msgs) = tickAndCollect world

      let damages =
        msgs
        |> Seq.choose(fun m ->
          match m with
          | Combat(CombatMsg.EffectDamage _) -> Some()
          | _ -> None)
        |> Seq.toList

      Expect.equal damages.Length 0 "Should not emit before first interval"
    }

    // ── RemoveEffect ──

    test "RemoveEffect removes effect from entity" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let effect = makeEffect Stun (Timed(TimeSpan.FromSeconds(10.0))) 0
      let world = applyEffect world entityId effect

      Expect.equal
        (world.Combat.ActiveEffects[entityId].Count)
        1
        "Effect should be applied"

      let removeMsg = TopLevelMsg.EffectProcessing(RemoveEffect(entityId, 0))
      let struct (world, _) = Program.update removeMsg world

      Expect.equal
        (world.Combat.ActiveEffects[entityId].Count)
        0
        "Effect should be removed"
    }

    // ── Multiple effects ──

    test "multiple effects are independent" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let effect1 = makeEffect Stun (Timed(TimeSpan.FromSeconds(2.0))) 0
      let base2 = makeEffect Silence (Timed(TimeSpan.FromSeconds(5.0))) 0

      let effect2 = {
        base2 with
            SourceEffect = {
              base2.SourceEffect with
                  Name = "silence"
            }
      }

      let world = applyEffect world entityId effect1
      let world = applyEffect world entityId effect2

      Expect.equal
        (world.Combat.ActiveEffects[entityId].Count)
        2
        "Should have 2 effects"

      let w = tickN world 126

      Expect.equal
        (w.Combat.ActiveEffects[entityId].Count)
        1
        "Should have 1 after first expires"
    }

    // ── Safety ──

    test "entity with no effects does not crash" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      let struct (_, _) = Program.update (FixedStep(1.f / 60.f)) world
      ()
    }

    // ── Stacking rules ──

    test "NoStack prevents duplicate effects" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let effect = makeEffect Stun (Timed(TimeSpan.FromSeconds(5.0))) 0

      let world = applyEffect world entityId effect
      let world = applyEffect world entityId effect
      let world = applyEffect world entityId effect
      let world = applyEffect world entityId effect
      let world = applyEffect world entityId effect

      Expect.equal
        (world.Combat.ActiveEffects[entityId].Count)
        1
        "Should not stack"
    }

    test "RefreshDuration resets timer on duplicate" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let baseEffect = makeEffect Stun (Timed(TimeSpan.FromSeconds(5.0))) 0

      let effect = {
        baseEffect with
            SourceEffect = {
              baseEffect.SourceEffect with
                  Stacking = RefreshDuration
            }
      }

      let world = applyEffect world entityId effect
      let w = tickN world 60

      let world2 = applyEffect w entityId effect

      let remaining =
        world2.Combat.ActiveEffects[entityId].[0].SourceEffect.Duration

      match remaining with
      | Timed d ->
        Expect.equal d (TimeSpan.FromSeconds 5.0) "Timer should reset"
      | _ -> failtest "Expected Timed duration"
    }

    test "AddStack increments stack count up to max" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId
      let baseEffect = makeEffect Debuff (Timed(TimeSpan.FromSeconds(5.0))) 0

      let effect = {
        baseEffect with
            SourceEffect = {
              baseEffect.SourceEffect with
                  Stacking = AddStack 3
            }
      }

      let world = applyEffect world entityId effect
      let world = applyEffect world entityId effect
      let world = applyEffect world entityId effect
      let world = applyEffect world entityId effect

      Expect.equal
        (world.Combat.ActiveEffects[entityId].Count)
        1
        "Should have 1 effect"

      Expect.equal
        (world.Combat.ActiveEffects[entityId].[0].StackCount)
        3
        "Should be at max stacks"
    }
  ]
