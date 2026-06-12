namespace KipoRl

open Mibo.Elmish

module NotificationSystem =
  let handleMsg
    (world: World)
    (msg: NotificationMsg)
    : struct (World * Cmd<TopLevelMsg>) =
    match msg with
    | ShowMessage(entityId, text, typ) ->
      world.Notifications.Add {
        EntityId = entityId
        Text = text
        Typ = typ
      }

      struct (world, Cmd.none)

    | ResourceRestored(entityId, resource, amount) ->
      let text =
        match resource with
        | HP -> $"+%d{amount} HP"
        | MP -> $"+%d{amount} MP"

      world.Notifications.Add {
        EntityId = entityId
        Text = text
        Typ = Heal
      }

      struct (world, Cmd.none)

  let update(world: World) : struct (World * Cmd<TopLevelMsg>) =
    struct (world, Cmd.none)
