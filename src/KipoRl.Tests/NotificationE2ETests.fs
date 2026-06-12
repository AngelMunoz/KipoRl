module KipoRl.Tests.NotificationE2ETests

open System
open System.Collections.Generic
open System.Numerics
open Expecto
open KipoRl
open Mibo.Elmish
open FSharp.UMX

let private makeWorld(entityId: int64<EntityId>) =
  let world = World()
  world.Entities.Positions[entityId] <- Vector3(3.f, 0.f, 7.f)
  world

let tests =
  testList "Notification E2E" [
    test "ShowMessage stores notification with entity id" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      let msg = TopLevelMsg.Notification(ShowMessage(entityId, "42", Damage))

      let struct (world, _) = Program.update msg world

      Expect.equal world.Notifications.Count 1 "Should have 1 notification"

      Expect.equal
        world.Notifications[0].EntityId
        entityId
        "Should have correct entity"

      Expect.equal world.Notifications[0].Text "42" "Should have correct text"
      Expect.equal world.Notifications[0].Typ Damage "Should have correct type"
    }

    test "ResourceRestored stores heal notification" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      let msg = TopLevelMsg.Notification(ResourceRestored(entityId, HP, 25))

      let struct (world, _) = Program.update msg world

      Expect.equal world.Notifications.Count 1 "Should have 1 notification"

      Expect.equal
        world.Notifications[0].Text
        "+25 HP"
        "Should format HP restore"

      Expect.equal world.Notifications[0].Typ Heal "Should be Heal type"
    }

    test "multiple notifications accumulate" {
      let entityId = UMX.tag 0L
      let world = makeWorld entityId

      let msg1 = TopLevelMsg.Notification(ShowMessage(entityId, "hit", Damage))
      let struct (world, _) = Program.update msg1 world

      let msg2 = TopLevelMsg.Notification(ShowMessage(entityId, "crit", Damage))
      let struct (world, _) = Program.update msg2 world

      Expect.equal world.Notifications.Count 2 "Should have 2 notifications"
    }
  ]
