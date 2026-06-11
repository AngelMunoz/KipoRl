namespace KipoRl

open System
open System.Collections.Generic
open Mibo.Elmish

module EffectProcessingSystem =

  let inline private processInterval
    (entityId: int64<EntityId>)
    (effect: ActiveEffect)
    ([<InlineIfLambdaAttribute>] dispatchMessage)
    =
    match effect.SourceEffect.Kind with
    | DamageOverTime ->
      dispatchMessage(
        Combat(
          CombatMsg.EffectDamage(
            entityId,
            effect.StackCount,
            // FIXME: When skill/effect store is in place
            // we should use the effect's or parent skill's element
            effect.SourceEffect.DamageSource
            |> function
              | Physical -> Neutral
              | Magical -> Neutral
          )
        )
      )
    | ResourceOverTime ->
      dispatchMessage(
        ResourceManager(
          ResourceManagerMsg.RestoreResource(entityId, HP, effect.StackCount)
        )
      )
    | Buff
    | Debuff
    | Stun
    | Silence
    | Taunt -> ()

  let handleMsg
    (world: World)
    (msg: EffectProcessingMsg)
    : struct (World * Cmd<TopLevelMsg>) =
    match msg with
    | ApplyEffect(target, effect) ->
      let effects =
        world.Combat.ActiveEffects
        |> Dictionary.tryFindV target
        |> ValueOption.defaultValue(ResizeArray())

      effects.Add effect
      world.Combat.ActiveEffects[target] <- effects

      world, Cmd.none

    | RemoveEffect(target, effectIndex) ->
      world.Combat.ActiveEffects
      |> Dictionary.tryFindV target
      |> ValueOption.iter(fun effects ->
        if effectIndex >= 0 && effectIndex < effects.Count then
          effects.RemoveAt effectIndex)

      world, Cmd.none

  let inline private intervalCrossed dt interval elapsed =
    let deltaTime = TimeSpan.FromSeconds(float dt)
    let prevElapsed = elapsed - deltaTime

    interval > TimeSpan.Zero
    && int(elapsed / interval) - int(prevElapsed / interval) > 0


  let update (dt: float32) (world: World) : struct (World * Cmd<TopLevelMsg>) =
    let totalGameTime = world.Time.TotalGameTime
    let messages = ResizeArray()

    for KeyValue(entityId, effects) in world.Combat.ActiveEffects do
      let mutable i = effects.Count - 1

      while i >= 0 do
        let effect = effects[i]
        let elapsed = totalGameTime - effect.StartTime

        match effect.SourceEffect.Duration with
        | Instant -> effects.RemoveAt i

        | Timed duration ->
          if elapsed >= duration then
            effects.RemoveAt i

        | Loop(interval, duration) ->
          if elapsed >= duration then
            effects.RemoveAt i
          else if intervalCrossed dt interval elapsed then
            processInterval entityId effect (Cmd.ofMsg >> messages.Add)

        | PermanentLoop interval ->
          if intervalCrossed dt interval elapsed then
            processInterval entityId effect (Cmd.ofMsg >> messages.Add)

        | Permanent -> ()

        i <- i - 1

    world, Cmd.batch messages
