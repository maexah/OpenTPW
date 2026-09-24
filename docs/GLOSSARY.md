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
| **Cell** | One square of the 128 × 128 park map. `ParkWorld.MapCell` is what the save says. `ParkState.Record( x, y )` is what the cell is now: the save's record, overlaid with any cell play has built on or cleared. `ParkState.CellAt( x, y )` returns its `RuntimeCell`: litter, litter collector, occupant. | |
| **ParkWorld** | The save **file**, read once, immutable. Lives in `OpenTPW.Files`. | |
| **ParkState** | The **running** park: balance, visitors, litter, queues, takings. Owned by `Level`. | |
| **Level** | One scene: the lobby or a park. `Level.Current` is the live one. Building a new one replaces it between frames. | |
| **Scene** | `Level.Scene.Lobby` or `Level.Scene.Park`. | |
| **Theme** | `jungle`, `fantasy`, `hallow`, `space`. Lower-case inside the engine. | |
| **Lost Kingdom** | The one park that ships: `levels/jungle/Easymode.TPWI`. | |
| **Time** | Frame clock. No menu stops it; only the debug console's `pause`/`step` hold it. UI runs on it. | |
| **GameClock** | The game's 31 ms tick. A park's menu freezes it; nothing in the lobby does. Simulation runs on it. | |
| **GameCalendar** | In-game date, advanced by a park's ticks only. | |
| **Tick** | One `GameClock` step: 31 ms. Counted by `GameClock.Ticks`. | |
| **Thing tick** | **Eight** game ticks — 248 ms, about four a second. The beat the thing engine, peep behaviour and ride scripts all take their turn on (`ParkPeople.ThingTickEvery`); the original gates it at `0054f668`. | ✅ say *thing tick* when you mean the eight, not "tick" |
| **Frame** | One pass of `Level.Update` and the render, at whatever rate the machine manages. **A peep's position is simulated on the thing tick and DRAWN every frame**, interpolated between the two by `ParkPeople.ThingTickFraction` — so frame, tick and thing tick are three different beats and are not interchangeable. | |
| **RideScript** | The live ride-script VM (`VM/RideScript.cs`) and its scheduler. | ✅ |
| RideVM | The OLD upstream VM: `VM/RideVM.cs`, `VM/Handlers/`, `VM/Includes/ScriptDefs.cs`, reached only through the dead `World/Ride.cs`, which nothing constructs; `VM/Includes/Events.cs` is referenced by nothing. Dead. `VM/Includes/ParLib.cs` is the exception: a live particle-id table. | ❌ |
| **Opcode** | One of 106 instructions in `OpenTPW.Files/Formats/Script/Opcode.cs`. Unbuilt ones call `Unimplemented.Report`. | |
| **Channel / lane** | One of an item's `NumSimultAnims` animation players. `TRIGANIM_CH` names one. | |
| **Sprite** | A guest's or staff member's 2-D art and its `SpriteScript`. Peeps are sprites, not models. | |
| **Walk** | A peep's movement along a route (`PeepWalk`). Guests and staff keep separate walk pools. | |
| **Route** | A planned path across cells (`CellRoute`, `PeepNavigator`). | |
| **Unimplemented** | The reporter: says once, counts after. `unimplemented` on the debug console prints the tally. | |
