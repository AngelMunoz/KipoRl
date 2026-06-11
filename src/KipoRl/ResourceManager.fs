namespace KipoRl

open System
open Mibo.Elmish

module ResourceManagerSystem =
  let inline applyRegen
    (prevAcc: float32)
    (regenRate: int)
    (dt: float32)
    (current: int)
    (max: int)
    =
    let newAcc = prevAcc + float32 regenRate * dt
    let toHeal = int newAcc
    let remainder = newAcc - float32 toHeal
    let newValue = min max (current + toHeal)
    struct (newValue, remainder)

  let inline applyRestore (amount: int) (current: int) (max: int) =
    min max (current + amount)

  let handleMsg
    (world: World)
    (msg: ResourceManagerMsg)
    : struct (World * Cmd<TopLevelMsg>) =
    match msg with
    | RestoreResource(target, resource, amount) ->
      world.Combat.Resources
      |> Dictionary.tryFindV target
      |> ValueOption.iter(fun current ->
        let max =
          world.Combat.DerivedStats
          |> Dictionary.tryFindV target
          |> ValueOption.map(fun stats ->
            match resource with
            | HP -> stats.HP
            | MP -> stats.MP)
          |> ValueOption.defaultValue current.HP

        let newRes =
          match resource with
          | HP -> {
              current with
                  HP = applyRestore amount current.HP max
            }
          | MP ->
              {
                current with
                    MP = applyRestore amount current.MP max
              }

        if newRes.HP <> current.HP || newRes.MP <> current.MP then
          world.Combat.Resources[target] <- newRes)

      world, Cmd.ofMsg(Notification(ResourceRestored(target, resource, amount)))

  let update (dt: float32) (world: World) : struct (World * Cmd<TopLevelMsg>) =
    let totalGameTime = world.Time.TotalGameTime

    for KeyValue(entityId, resource) in world.Combat.Resources do
      let inCombat =
        world.Combat.InCombatUntil
        |> Dictionary.tryFindV entityId
        |> ValueOption.map(fun until -> totalGameTime <= until)
        |> ValueOption.defaultValue false

      if not inCombat then
        world.Combat.DerivedStats
        |> Dictionary.tryFindV entityId
        |> ValueOption.iter(fun stats ->
          let struct (prevHpAcc, prevMpAcc) =
            world.Combat.RegenAccumulators
            |> Dictionary.tryFindOrDefault entityId struct (0.f, 0.f)

          let struct (newHP, newHpAcc) =
            applyRegen prevHpAcc stats.HPRegen dt resource.HP stats.HP

          let struct (newMP, newMpAcc) =
            applyRegen prevMpAcc stats.MPRegen dt resource.MP stats.MP

          world.Combat.RegenAccumulators[entityId] <-
            struct (newHpAcc, newMpAcc)

          if newHP <> resource.HP || newMP <> resource.MP then
            world.Combat.Resources[entityId] <-
              { resource with HP = newHP; MP = newMP })

    world, Cmd.none
