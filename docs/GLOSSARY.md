# Glossary

One line each. If a word is used two ways in the code, both are listed and the preferred one is marked.

| Term | Means | Prefer |
|---|---|---|
| **Peep** | Any person in a park: a guest or a member of staff. | ✅ |
| Guest / Visitor / Person | Same as a peep who is not staff. Used in older files and in the save-file reader. | use *guest* only when staff are excluded |
| **Staff** | Handyman, mechanic, entertainer, guard, researcher. One shared behaviour with five extensions. | |
| **Thing** | Anything the save gives a `ThingId`. Objects, people and sprites are all things. Guest and staff ids share one numbering. | |
| **Object** | A placed thing: shop, ride, sideshow, toilet, bin, fountain... A `ParkWorld.CatalogueObject`. | |
| **Item** | The catalogue *definition* of an object, from the theme's `ItemDescriptionFile`. An object is an item placed on cells. | |
| **Ride** | An object whose item can be ridden or queued for. `ParkRides` owns them. `World/Ride.cs` is dead upstream code. | |
| **Fixed item** | Gate, traffic lights: things the save never positions; `ParkFixedItems` places them. | |
| **Cell** | One square of the 128 × 128 park map. `ParkWorld.MapCell` is what the save says; `ParkState.RuntimeCell` is what play changes. | |
| **ParkWorld** | The save **file**, read once, immutable. Lives in `OpenTPW.Files`. | |
| **ParkState** | The **running** park: balance, visitors, litter, queues, takings. Owned by `Level`. | |
| **Level** | One scene: the lobby or a park. `Level.Current` is the live one. Building a new one replaces it between frames. | |
| **Scene** | `Level.Scene.Lobby` or `Level.Scene.Park`. | |
| **Theme** | `jungle`, `fantasy`, `hallow`, `space`. Lower-case inside the engine. | |
| **Lost Kingdom** | The one park that ships: `levels/jungle/Easymode.TPWI`. | |
| **Time** | Frame clock. Never pauses. UI runs on it. | |
| **GameClock** | The game's 31 ms tick. A park menu freezes it. Simulation runs on it. | |
| **GameCalendar** | In-game date, advanced by a park's ticks only. | |
| **Tick** | One `GameClock` step. Ride scripts take a turn every 8th tick; the "thing engine" once in eight. | |
| **RideScript** | The live ride-script VM (`VM/RideScript.cs`) and its scheduler. | ✅ |
| RideVM | The OLD upstream VM (`VM/RideVM.cs`, `Handlers/`, `Includes/`). Never constructed. Dead. | ❌ |
| **Opcode** | One of 106 instructions in `OpenTPW.Files/Formats/Script/Opcode.cs`. Unbuilt ones call `Unimplemented.Report`. | |
| **Channel / lane** | One of an item's `NumSimultAnims` animation players. `TRIGANIM_CH` names one. | |
| **Sprite** | A guest's or staff member's 2-D art and its `SpriteScript`. Peeps are sprites, not models. | |
| **Walk** | A peep's movement along a route (`PeepWalk`). Guests and staff keep separate walk pools. | |
| **Route** | A planned path across cells (`CellRoute`, `PeepNavigator`). | |
| **Unimplemented** | The reporter: says once, counts after. `unimplemented` on the debug console prints the tally. | |
