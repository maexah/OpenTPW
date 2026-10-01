# Ride operation in the original executable

How a placed catalogue object — a ride, a shop, a sideshow, a toilet — operates in the original game: the turn it takes on the park's thing sweep, the boarding handshake it shares with a guest, how a rider leaves and pays, how the queue renumbers itself, and the script instructions the engine and the ride use to talk to each other. The short version of the turn is: **the park sweeps every thing once in eight game ticks; a healthy object drops a stale queue head, maybe requests a breakdown, invites the guest at the front, then dismisses anyone who has finished.** The admission itself is driven from the *guest's* side, not the ride's — the ride invites, the guest accepts. Everything below is read off the disassembly, the shipped `.RSE` scripts and the shipped `.sam` balance files; where a fact is a measurement, its provenance is named in the row.

Two kinds of offset appear on this page and they are **not** interchangeable. A `+0x..` is a **runtime** offset into the live struct. A bare decimal ("file 214") is an offset into the **saved record** that `ParkWorld` walks. `mCash` is runtime `+0x1a0` and file 414; `mFlags` is runtime `+0x32` and file 58, and `mEntryPos` runtime `+0x36` and file 206.

## Where a ride's turn comes from

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00516380` | — | The park's whole thing sweep. Increments the global tick counter at `+0x1da70c`, then walks the thing list once from its head calling `FUN_0050b360` on every live thing. Guests, staff and rides all come off this one loop. | Disassembly |
| `FUN_0050b360` | — | Switches on a thing's model byte. Model 3 (a placed catalogue object) is handed to `FUN_004e0b90` then `FUN_004e0e00` — the same needs-then-behaviour shape every person kind gets. | Disassembly |
| `0054f56b` | — | `CALL 0x005516b0`, the SCRIPT system, in `Game_StateMachine`. | Disassembly of the straight-line region |
| `0x005516b0` | — | The ride-script system entry point. | Call site above |
| `0054f5fb` | — | The sprite step, after scripts. | Disassembly |
| `0054f668` | — | `TEST byte ptr [0x00877d34],0x7 / JNZ` — the gate in front of the thing sweep. | Disassembly |
| `0054f7bb` | — | The thing sweep's call site. | Disassembly |
| `+0x1da70c` | — | The global tick counter the sweep increments; the same counter `FUN_004e0b90` masks with `& 7`. | Disassembly |

### Two object iterations, and they are not the same list

Reading either of these as "the object list" gets the other wrong.

| | What walks it | Over what |
|---|---|---|
| A ride's **turn** | `FUN_00516380` → `FUN_0050b360`, the rows above | the **thing** list — every live thing, guests and staff included |
| A guest's **choice** | `FUN_004fcb10` | the **`mFirstObject`** chain — placed catalogue objects only |

`FUN_004e0e00` has exactly one caller (`FUN_0050b360`), and that one has exactly one (`FUN_00516380`), so a ride's turn comes off the thing sweep and nothing else.

**The object chain is LIVE, and a newly built object joins it at the HEAD.**

| Address | What it is | Evidence |
|---|---|---|
| `+0x1da746` | `mFirstObject`, the chain head | `FUN_00516c80` writes the literal string `mFirstObject` against this offset |
| thing `+0xc` | the link — the save's `mNext`, file 208 | `FUN_004db090` zeroes it before linking; `FUN_004fcb10` advances by it |
| `FUN_00519d80( world, thing )` | **LINK.** `head = this; if (oldHead) this->next = oldHead` | Decompiled |
| `FUN_00519dc0( world, thing )` | **UNLINK.** Walks from the head matching `+0xc`, patches the predecessor, or moves the head | Decompiled |

**One call site each, so there are no exceptions to hunt**: `FUN_00519d80` only from the object constructor `FUN_004db090` at `0x004db1ec`, `FUN_00519dc0` only from the demolish `FUN_004dd0a0` at `0x004dd0eb`. Every object built is linked; every one demolished is unlinked.

**Head insertion is observable rather than cosmetic**: `FUN_004fcb10` keeps the later candidate on a tie only when `mGameTick & 1` (`+0x1da70c`, named by the same writer), so where an object sits in the walk decides ties between equally good candidates.

**`mFirstObject` is a live runtime head, not a save artefact.** `FUN_00516c80` writes it as a world variable beside `mParkGates` and `mTrafficLights`, while the thing array is saved separately under `Used_Thing_Head` / `Used_Thing_Next` (`DAT_007cf56c`). Two chains, two save mechanisms. Model byte 3 is a placed catalogue object, corroborated from the other side by that writer's own model switch sending case 3 to `FUN_004db7d0`, the object serialiser.

**The header's family of list heads**, from `FUN_00516c80`: `mFirstHandyman` `+0x1da73c`, `mFirstMechanic` `+0x1da73e`, `mFirstEntertainer` `+0x1da740`, `mFirstResearcher` `+0x1da742`, `mFirstGuard` `+0x1da744`, `mFirstObject` `+0x1da746`. The writer emits Guard **before** Researcher while their offsets run the other way, so the save's field order is the write order and not ascending offset — which corroborates `ParkWorld.cs`'s existing remark from the save side. **This does not close** the open item on `+0x1da744` walked through `+0x210` / `+0x212`: that head is `mFirstGuard`, a staff chain with different link offsets, and `FUN_005019f0` case `0x11` is still undecoded.

**`FUN_004d3d10` is not a chain.** It is `CControlManager::GetObjectControl…`, a linear scan of 32-byte per-item records with a one-entry cache, and its `+0x18` is a count of how many of that item stand in the park — incremented by the constructor, decremented by the demolish.

**A structural absence read from a decompilation is only as good as the calls followed.** `FUN_004db090` and `FUN_004dd0a0` do not link or unlink inline: each makes one call to a helper — `FUN_00519d80` among sixty-odd field initialisations, `FUN_00519dc0` behind the refund arithmetic. A reading that misses those calls concludes that the original could never offer a ride the player had just built, which proves too much.

Two consequences of that ordering, both settled by reading the straight-line region rather than by comparing addresses (address order only implies execution order *inside* one straight-line region):

- **Scripts run before things.** A ride's write to a script variable is seen by that script on the **next** pass, not the same one.
- **The thing sweep runs one game tick in eight** (`& 7`).

## The first half of the turn — `FUN_004e0b90`

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004e0b90` | — | The object's needs half. See the ordered list below. | Disassembly |
| `FUN_004cd4e0`, `FUN_004d8460(7, …)` | — | Run when `mFlags & 0x80` and script var 0 reads 1; the engine then writes **2** back into that variable. | Disassembly |
| `FUN_004e0db0` | — | Sets `VAR_BREAKSTAT`. | Disassembly |
| `FUN_004e03f0` | — | Clears `VAR_BREAKSTAT`. | Disassembly |
| `FUN_00502430` | — | "Is this person queueing?" — the second half of the stale-head test. | Disassembly |
| `+0x48` | — | The object's wear, clamped 0..100. | Disassembly |

In order:

1. If `mFlags & 0x80`: read script var **0** (`VAR_LETMEON`); if it reads **1**, run `FUN_004cd4e0` / `FUN_004d8460(7, …)` and write **2** back. **The `0x80` bit is `IsFireworks`** (descriptor `+0x110`, set by `FUN_004db090`); no jungle item sets it.
2. **States 3 and 4 return immediately** — a state-3 object does nothing at all on its turn.
3. **The breakdown request.** Unless the state is 1, or `mCanLoad` is nought, or a global counter's low three bits are set, it checks the ride and — for a non-toilet whose `VAR_BREAKSTAT` is nought — logs `"Object %d: requested breakdown"`, takes a constant off the wear at `+0x48` (clamped 0..100), and writes **`VAR_BREAKSTAT` = 1**. So var 4 is an engine → script channel, confirmed from the other side by `FUN_004e0db0` setting it and `FUN_004e03f0` clearing it.
4. **The worn flag.** A wear value below a threshold writes **`VAR_WORN` (var 8) = 1**.
5. **The stale-queue-head drop.** If `mFirstInQ` names nobody the engine knows, or names somebody not queueing (`FUN_00502430`), the head is cleared and nothing else is tidied.

## The second half — `FUN_004e0e00`, a switch on `mState` (`+0x19c`)

| State | What the object does |
|---|---|
| 0 | `FUN_004e14e0` — the normal turn |
| 1, 2, 4 | `FUN_004e0450` (complete admission) then `FUN_004e1410` (dismiss) |
| 3 | Nothing |
| other | Asserts `"Unknown state in CObject::ModelS…"` |

`FUN_004e14e0`, the normal turn, is only this:

1. **`FUN_004e1220()` — INVITE, unconditionally, first thing.**
2. Read script var **7** (`VAR_BROKEN`). If nought, **`FUN_004e1410()` — DISMISS** — and return.
3. Otherwise the wear at `+0x48`, truncated (`__ftol`), decides. Non-zero: log `"Object %d: Setting state BROKEN_DOWN"` (`0x0075c038`), `FUN_00454550( modelSlotTable[ +0x20 ], 2 )`, then `FUN_004e0e60( 1 )` = SetState. Nought: log `"Object %d: Setting state CONDEMNED"` (`0x0075c014`), `FUN_00454550( …, 4 )`, then `FUN_004e0e60( 4 )`.

**So a healthy object's whole turn is: drop a stale queue head → maybe request a breakdown → Invite → Dismiss.** In the shipped Lost Kingdom park every VISITABLE object is state 0 and everything else is state 3, so that path is the live one.

### Where an object's state comes from

`FUN_004e0e60` (SetState) is the only store to `mState` at run time (`0x004e11b5`); the constructor and the save's serialiser are the others. Its nine call sites, by the value each writes:

| State | Meaning | Written by |
|---|---|---|
| 0 | Operating | The constructor `FUN_004db090` for a choosable item (descriptor `+0x3c` `Info.IsChoosable`, which becomes flag byte `+0x32` bit `0x04`); every "open" path: `FUN_004de1f0`, `FUN_004df390`, the repair `FUN_004df8f0`, `FUN_004dfe30`, `FUN_004e0050(0)` |
| 1 | Broken down | `FUN_004e14e0` only, step 3 above |
| 2 | An upgrade requested, closed until done | `FUN_004e0050(1)` only, and only with bit `0x04` set (it logs `"Object %d: wants maintenance"` first). Left by `FUN_004e0050(0)` (`"Cancelling request for upgrade"`) or by the repair, which on state 2 raises the level at `+0x50`. |
| 3 | Never offered | The constructor `FUN_004db090` only (`0x004db4fb`), for an item whose `IsChoosable` is nought |
| 4 | Condemned | `FUN_004e14e0` only, step 3 above |

SetState's own jump table (`0x004e11d0`): 0 and 3 store and do nothing else. 1, 2 and 4 each run `FUN_004e0450` (complete admission), log `"Object %d: Closing..."`, set `mCanLoad` (`+0x68`) and `+0x6c` to nought, write `VAR_RIDECLOSED` (var 6) = 1, call `FUN_00454550( slot, 1 )` and post an event; 4 also logs `"Ride has become CONDEMNED!!!"`. A value above 4 logs `"Unknown state in CObject::SetState"` and is stored anyway.

The offer gate `FUN_004dd920` refuses 1 and 4 by number, 2 through `mCanLoad`, and 3 through bit `0x04`, which it tests first. **An "open" path can move a state-3 object to 0**: the guard `FUN_004df290` tests neither 3 nor bit `0x04`, the constructor closes a queued item it has just put in state 3 (`0x004db4fb`, then `0x004db712`), and the tail of `FUN_004de1f0` opens it again with SetState(0). The repair, `FUN_004e0050(0)` and three of `FUN_004df390`'s callers have no refusing guard at all: `FUN_004df390` logs `"Opening non-openable ride!"` five times and opens anyway.

`Invite` itself completes a pending admission on one arm: `FUN_004e0450` has five callers, and one is **`FUN_004e1220` at `004e13fc`** — the `mCanLoad == 0` bail, which does `FUN_004e0450(); return;` rather than simply returning. The other four are three `FUN_004e0e60` (SetState) paths and the states-1/2/4 arm, all catch-ups while closing. `mCanLoad` is 1 on all fourteen objects when Lost Kingdom loads, but every close clears it and leaves `mState` as it was - the park's door, the ride window's door, a blocked exit - so this bail is how a closed ride's queue is turned away, one head a turn: see "Every way out of a queue", "The closed ride".

**`Invite`'s fullness test is skipped for a WATER (2) or COASTER (3) track**: the original tests the item descriptor's track type against **3**, then **2**. Track-type constants are car **1**, water **2**, coaster **3**. (A track ride refused as "not valid" elsewhere tests 1 and 2 for an entirely different reason; the two tests must not be carried across.)

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004e0e00` | — | The behaviour half; the state switch above and nothing else. | Disassembly |
| `FUN_004e14e0` | — | The normal (state 0) turn. | Disassembly |
| `FUN_004e1220` | Invite | Calls the head of the queue forward. | Disassembly, xrefs |
| `FUN_004e1410` | Dismiss | Lets a finished rider off. | Disassembly |
| `FUN_004e0450` | CompleteAdmission | Completes a pending admission. Five callers; three SetState paths, the states-1/2/4 arm, and `Invite`'s bail at `004e13fc`. | Xref sweep |
| `FUN_004e0e60` | SetState | Writes `mState` and runs the side effects. | Disassembly |
| `FUN_004e0a70` | — | Four lines: `script[VAR_LETMEON] != mFirstInQ`, the gate in front of completion. | Disassembly |
| `FUN_004e0ac0` | — | Tells the object to forget a person. | Disassembly |
| `FUN_00454550` | — | A change to the object's model, `DAT_007a4610[ +0x20 ]`: acts only when model `+0xb0` is set, stores one of four texture offsets (1, 2, 4, 8) through `[[model+0xb4]+8]+0x2c`, sets `+0xbc` = 0.2f and flag bits at `+4`. **Eleven callers**: `FUN_004e14e0` with 2 (broken, `0x004e1542`) or 4 (condemned, `0x004e1584`); every close with 1 - SetState 1, 2 and 4 (`0x004e0f20`, `0x004e101f`, `0x004e1123`), `FUN_004df300` (`0x004df37e`), `FUN_004df150` (`0x004df265`), `FUN_004dfe30` (`0x004dfeed`), the repair `FUN_004df8f0` (`0x004dfd65`), the constructor (`0x004db78e`); `FUN_004e0050` with 8 (`0x004e0098`). `FUN_004547c0` is its counterpart on every open (`+0xbc` = -0.3f, seven callers: `0x004de462`, `0x004df413`, `0x004dfc11`, `0x004dfc95`, `0x004e0017`, `0x004e010b`, `0x004e0191`). Not a sound; the model loader around it names `Hoardings`, so probably the hoarding, not seen. | E8 scan, disassembly |
| `+0x19c` | `mState` | The object's state byte. | Disassembly |
| `+0x33` bit 0 | RunsContinuously | Descriptor `+0x48`, set by `FUN_004db090`. It lets a ride invite while running. | Disassembly |

## Opening a ride — where capacity and duration come from

A ride script never writes its own capacity: `Bouncy.RSE` declares `VAR_CAPACITY` (index 2) and READS it once (`81 CMP VAR_CAPACITY, VAR_TEMP`), and has no `COAST` instructions at all. The engine pushes the value. With `VAR_CAPACITY` at nought the fullness test `capacity <= onRide` is `0 <= 0`, so an object that is neither water nor coaster reads as FULL for ever and `Invite` refuses every turn.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004dd7f0` | SetCapacity | Logs `"CAPACITY = %d"`. Clamps the wanted value between the descriptor's `+0x124` and `+0x128` (only when their sum is positive), **writes script variable 2**, and stores the result to the object's `+0x5d`. | Its own string |
| `FUN_004df8f0` | — | The OPEN/REPAIR path, `"Object %d: repairing fully"`. Sets the State of repair `+0x44` to 100.0f (never the wear at `+0x48`), writes `VAR_WORN` 0, and on the upgrade arm (`+0x19c` == 2) passes the descriptor's per-upgrade `+0x198` to SetCapacity and writes the speed from `+0x1a8` (`"SPEED = %d"`) and `VAR_DURATION` (var 3, `"DUR = %d"`). | Its own strings |
| `FUN_004dfe30`, `FUN_004e0050` | — | Two more functions carrying the same "open a ride" tail. | Disassembly |
| `+0x5d` | `mOperatingCapacity` | File **1034**. | Save record |
| `+0x5c` | `mOperatingDuration` | Written by the upgrade arm from the descriptor's `+0x1a0`, clamped. | Disassembly |
| `+0x58` | `mOperatingSpeed` | From the descriptor's `+0x1a8`. | Disassembly |
| `+0x68` | `mCanLoad` | File **214**, 4 bytes. | Save record |
| `+0x124` / `+0x128` | `UsageInfo.MinCapacity` / `MaxCapacity` | The clamp in `FUN_004dd7f0`. Declared by 20 items each. | `.sam` sweep |

**The "open a ride" idiom appears in at least five functions** (`FUN_004df390`, `FUN_004df8f0`, `FUN_004dfe30`, `FUN_004e0050`, the tail of `FUN_004de1f0`) and is always the same tail: **`+0x68` (`mCanLoad`) = 1**, `FUN_004547c0( model )`, write `VAR_RIDECLOSED` (var 6) = 0, then `FUN_004e0e60(0)` = SetState(**0**). See "The closed ride".

`FUN_004dd7f0`'s clamp is between two descriptor fields, so a value already stored in a save has been clamped once; applying the rule a second time would clamp twice.

## The boarding chain, end to end

    Invite → mBeenAdmitted + nomination → the guest's InQueue arm → BeingAdmitted
           → AdmitPerson → EnteringRide → CompleteAdmission → Riding
           → Dismiss → ExitRide → LeavingRide

**The ride invites; the guest accepts.** The two middle steps belong to the guest's own turn, not the object's.

### State 13 (`BeingAdmitted`) — `FUN_005006b0`

A guest in `BeingAdmitted` runs the walk tick; on arriving (and `"got stuck in middle o[f]…"` is treated as arriving), it does three things in order:

1. **Affordability** — `FUN_004fde50`, the price-opinion function. Non-zero means too expensive: it logs `"Person %d: Object %d is too expe[nsive]…"`, raises thought 6 and event 10, docks `MediumHappinessChange` (`0x00500778`), counts a walk-away on the object (`FUN_004e1670`, `mNumWalkAways`), tells the object to forget them (`FUN_004e0ac0`), **leaves the queue** (`FUN_004ddd20`) and runs `FUN_005012f0` (`0x005007b4`), which docks it a second time, clears `mMajorDest` and goes to `Deciding`. See "Every way out of a queue".
2. **`FUN_004e0900` = `AdmitPerson`.** If it answers non-zero: log `"Person %d been AdmitPerson'd to r[ide]…"` and **`SetState(0xe)` — `EnteringRide`.**
3. Otherwise **try to rejoin the front of the queue** (`FUN_00501160`, `0x00500826`): still linked at the head, the guest walks back to place 0's point in state 12. If that fails too, `"Couldn't rejoin FOQ even!"`, leave the queue and `FUN_005012f0` (`0x00500857`).

### State 14 (`EnteringRide`) — `FUN_005019f0` case `0xe`

`FUN_00500870` inlined: test the gate, unlink from the queue, assert `"Person not correctly removed fro[m queue]"`, `SetState(0x10)` — `Riding`. The gate is `FUN_004e0a70`: `script[VAR_LETMEON] != mFirstInQ`.

**The original does not complete an admission from a healthy ride's turn.** `FUN_004e0450` is reached only from the three SetState paths, the states-1/2/4 arm and `Invite`'s `mCanLoad == 0` bail. Completion in normal play is the guest's state-14 turn. This looks like a missing call on the ride side and is not one.

Entering state 14 also writes the guest's `+0x1f1` from the win roll, for every kind of thing (only a sideshow's can lose) — see [the win roll](#the-sideshow-win-roll) below.

### The state → handler map (guest side)

| State | Name | Handler |
|---|---|---|
| 10 | `GoingToRide` | `FUN_004ffbc0` (`FUN_005019f0` case 10, `0x00501b0d`): the walk to a chosen thing, the arrival test on the back of its queue, the gates, the join. See "Walking to a new place in the queue". |
| 0xb (11) | `InQueue` | `FUN_005019f0` case `0xb` = `FUN_004ffff0`. Entering it, SetState's case `0xb` (`0x00501e91`) sets `+0x10` = 3, stamps `mTimeStartedIdling` and derives `+0x1f4` = `trunc( +0x1f1 × 1.2f )` |
| 12 | `SteppingUpQueue` | Inline in `FUN_005019f0` case `0xc`: run the walk tick, and on arriving **or** on getting stuck alike, `SetState(0xb)` back to `InQueue`. It renumbers nobody and routes nowhere new. Entering it writes only `+0xc2` = 0 and `+0x10` = 1. |
| 13 | `BeingAdmitted` | `FUN_005006b0` |
| 0xe (14) | `EnteringRide` | `FUN_005019f0` case `0xe` = `FUN_00500870` inlined |
| 0xf (15) | Set on **leaving** — `FUN_005014e0` ends `FUN_00501db0(0xf)` | `FUN_00500900`: on arriving with a saved `+0x1de`, back to that thing's queue (state 10) |
| 0x10 (16) | Set on **admission** | |
| 18 | `HeadingForExit` | `FUN_00500a50` |

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_005006b0` | — | The state-13 handler; the three steps above. | Disassembly |
| `FUN_004e0900` | AdmitPerson | Called **by the guest**, not by the ride. | Disassembly of `FUN_005006b0` |
| `FUN_00500870` | — | Forcing the head on: the object from the guest's own `MajorDest`, the gate `FUN_004e0a70`, unlink, assert, `SetState(0x10)`. Its one caller is the object's `FUN_004e0450` (`0x004e0500`); the guest's own case `0xe` is a separate inlined copy that boards the calling guest. | Disassembly |
| `FUN_005019f0` | — | The guest's per-state turn dispatch. | Disassembly |
| `FUN_00501db0` | SetState | The guest state setter this project reproduces as `Peep.SetState`. Case `0xe` writes `person[+0x1f1] = FUN_004e2670( object )`. Case `0xf` asks for animation 1, the walk (`0x00501f5c`; `FUN_004d4140( 1, ... )` when `+0xc` is nought); case `0x10` asks for 3, the stand, on a thing whose byte `+0x32` carries `0x20` (`0x0050212b`), and calls `FUN_004d4170` otherwise. | `search_bytes` for `88 ?? f1 01 00 00`; decompile |
| `FUN_00500a50` | — | The state-18 (`HeadingForExit`) handler. | State → handler map |
| `FUN_004fde50` | — | The price-opinion function. Ends `if ((price <= worth) && (price <= person[+0x1a0])) return 0;`. Computes what a guest thinks a thing is WORTH from the item descriptor's `+0x140`..`+0x150` (through `FUN_004dd4e0`), `UsageInfo.RipOffOK`, and the object's chance of winning (`FUN_004e21b0`, `+0x190`) and, **for a sideshow only** (`+0x4ac` == 2), prize (`FUN_004e1a10`); then pushes a price sample. See "At the door" below. | Disassembly |

**`FUN_004fde50` is the gate at the DOOR, never on paying.** Its one caller is the state-13 handler (`0x00500715`): too expensive means turning away before boarding, after the walk; the chooser never asks it, and it is not consulted when the charge is taken.

## Leaving a ride

**A rider is *teleported* onto the exit, not walked to it.** `FUN_005014e0` (ExitRide) reads the exit point with `FUN_004dedf0(obj, 1, &x, &y)` and calls **`FUN_004fa930`**, which sets the person's POSITION with zero velocity, and only *then* sets a destination. Walking a guest to the exit cannot work: a cell edge opens a ride end only along the way it faces, so the route fails and the guest gives up where they stand.

- **The destination is the cell BEYOND the exit**, not the exit. Direction = the exit cell's own direction byte (`+0xd`, via `FUN_00522850`), **flipped to the opposite (`FUN_004d8c00`, a four-bit rotate) when `mExitPos == mEntryPos`** — which is **ten of the eleven objects** in Lost Kingdom. `FUN_004d97e0` steps to the neighbour. It must not be a queue cell (`FUN_00536320`).
- **`FUN_004dedf0` yields a FIXED-POINT position:** high byte the cell (`(mExitPos - 1) & 0x7f`, `>> 7`), low byte a sub-cell offset taken from the ITEM's own `.sam` — descriptor `+0xdc`/`+0xe0` for the exit, `+0xd4`/`+0xd8` for the stand point — validated with `"Dodgy X exit point in SAM file"`. Non-zero `which` selects the exit (`+0x38`), nought the stand point (`+0x36`); both are PACKED.
- **The failure arm closes the ride.** If the aim will not route, the original refuses the dismissal and calls **`FUN_004df150`**: clears `mCanLoad` (`+0x68`) and `mPersonBeingLoaded` (`+0x6c`), logs `"Object %d: Closing…"`, sets script var 6 (`VAR_RIDECLOSED`). Same body as `FUN_004e0e60`. **This is observable in the original**: a ride whose exit is not connected still teleports the guest onto it and leaves them there with a `?` overhead. If that is thought `0x11`, the stranded bubble, it comes only from SetRandomDest's LINKED walk reaching a dead end or its refusal after one: an exit cell with no links takes the no-links arm, which raises nothing ("Deciding and wandering"). Which picture `0x11` is, and the teleported guest's cell, are not established.
- `FUN_004fa530` is SetDest and **answers whether a route exists**; ExitRide charges and changes state ONLY when it does. It aims at the cell's centre, writes the destination before it routes, and can answer 0 without routing on `mStrandedTime` (see "Walking to a new place in the queue").

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_005014e0` | ExitRide | `"Person %d: ExitRide, leaving rid…"`. Teleport, aim, settle up, `SetState(0xf)`. | Its own string |
| `FUN_004dedf0` | — | Exit / stand point, fixed point, `"Dodgy X exit point in SAM file"`. | Its own string |
| `FUN_004fa930` | — | Place a person: sets position, zero velocity. **Five callers, not one of them a rider**: `FUN_005014e0` ExitRide, `FUN_004feb50` (a generic put-down-and-aim, `"<Humph>"`), `FUN_004f7e20` (a three-line wrapper), `FUN_004d7580` (the handyman's litter arm), `FUN_00505ea0` (dropping a staff member). | Xref sweep |
| `FUN_004fa530` | SetDest | Sets a destination, the cell's centre (`0x80, 0x80`), and answers whether a route exists; `FUN_004fa5f0` is the same to any 8.8 point. | Disassembly |
| `FUN_004df150` | — | Close the object: `"Object %d: Closing…"`. | Its own string |
| `FUN_004d8c00` | — | Four-bit rotate — flips a direction to its opposite. | Disassembly |
| `FUN_004d97e0` | — | Step to the neighbouring cell. | Disassembly |
| `FUN_00522850` | — | The cell direction accessor (`+0xd`). | Disassembly |
| `FUN_00536320` | — | "Is this a queue cell" predicate: mType 3 **or 9** (an entrance). | Disassembly |
| `FUN_004f9490` | — | `"Peep %d: stranded at time %d"`; also the source of the four direction bits 1 / 4 / 0x10 / 0x40. | Its own string |
| `+0x38` | `mExitPos` | File **218**, PACKED. | Save record |
| `+0x36` | `mEntryPos` | File **206**. | Save record |
| `+0x6c` | `mPersonBeingLoaded` | — | Disassembly |
| `+0xdc` / `+0xe0` | `UsageInfo.ExitCellAppearPosX/Y` | Sub-cell exit offset (23 items declare it). | `.sam` sweep |
| `+0xd4` / `+0xd8` | `UsageInfo.EntryCellStandPosX/Y` | Sub-cell stand offset (23 items declare it). | `.sam` sweep |

## The queue

The queue is **exactly a doubly-linked list**: the head on the object (`mFirstInQ`, `+0x3c`), `mQNext` / `mQPrev` through the guests, and no tail: `+0x3a` (`mBackOfQueue`) is the back CELL, which `GetBackOfQueue` caches.

**Nothing renumbers a queue when somebody leaves** — `FUN_004ddd20` only unlinks. The original recomputes a guest's position from the links every turn instead:

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004ddf50` | GetPositionInQueue | Walks `mFirstInQ` along `mQNext` counting from nought; `-1` if absent. Its comparison is the middle arm of `FUN_004ffff0`. | Disassembly |
| `FUN_004ddb90` | — | Join: for an empty queue writes `mFirstInQ` = the joiner (`0x004ddbd1`); otherwise walks raw `mQNext` to the tail, asking nobody `FUN_00502430`, and writes its `mQNext`; then the joiner's `mQPrev` (the old tail or 0) and `mQNext` (0). No membership test. **Writes no position.** | Disassembly |
| `FUN_004ddd20` | — | Leave: patches the neighbours' links, zeroes the leaver's, asserts both nought (`"Person not successfully removed f…"`). **Also clears `VAR_LETMEON` when it names the leaver.** | Disassembly |
| `FUN_00501290` | — | Literally `state == 0xb && person[0x1f1] == 0` — the head-at-nought test `Invite` calls forward on. | Disassembly |
| `FUN_00501160` | FindQueueDestination | Takes the guest's CURRENT place (`FUN_004ddf50`) into `+0x1f1`, then routes them to its point and sets state 12. See "Walking to a new place in the queue". | Its own log |
| `FUN_005012f0` | — | Dismissed from the queue: event 6, the kids' effect `0x80` when the guest's id `& 7` is nought, happiness down by `MediumHappinessChange` (`0x00501359`, every time), `mQPrev` `+0x22a`, `mQNext` `+0x228`, `mBeenAdmitted` `+0x1f8`, `mMajorDest` and `+0x1f1` zeroed, state 6. Seven callers; six unlink the guest from the object first (`FUN_004ddd20`), the sale (`FUN_004fb360`) does not. See `park-engine.md`, "Selling and the people on it". | Disassembly |
| `FUN_004faec0` | — | Guest construction; writes `+0x1f1` twice. | `search_bytes` |
| `+0x1f1` | `mQueuePos` | **Runtime byte**, file offset **494**. Five byte stores write it on a guest: the constructor `FUN_004faec0` twice (`0x004faf63`, `0x004fb17b`), `FUN_00501160` (`0x005011cb`), `FUN_005012f0` (`0x0050137f`) and `FUN_00501db0` case `0xe` (`0x00501f41`); the serialiser loads it (`0x004fbdbc`). The dword stores at `+0x1f0` are on staff records. | `search_bytes` for `88 ?? f1 01 00 00` |
| `+0x1f4` | `mQueueMoveDelay` | 4 bytes, file **490**; `mQPrev` is file **488**. Re-take at once if it is nought **or** the drift exceeds 2; otherwise spend one. **The drift is an unsigned compare** of the zero-extended byte minus the place (`0x005002c2`..`0x005002e5`), so a place that moved BACKWARD wraps and re-takes immediately. Written by the constructor (0, `0x004faf69`), that spend (`0x005002ec`), SetState's case `0xb` (`0x00501ec8`) and the serialiser. | Disassembly |
| `+0x3c` | `mFirstInQ` | Queue head on the object. | Disassembly |
| `+0x3a` | `mBackOfQueue` | File **212**. | Save record |

### Walking to a new place in the queue - `FUN_00501160`, FindQueueDestination

Decoded 2026-09-24 (`docs/QUEUE.md` Q50e): four decoders - the place to a point, the re-take and its route, its three
callers, the aim - each report put to a skeptic reading the disassembly; where a skeptic amended a claim, the amended
reading is what stands here. The function names itself in its own failure log (`0x0075df6c`).

**FindQueueDestination**, `ECX` the guest, in order: the object from `MajorDest` through the thing table; `place =
FUN_004ddf50( object, own id )`. **-1 answers 0** (`0x005011be`) with nothing written, no log of its own and no random
draw. Otherwise the byte `+0x1f1` = the place's low byte (`0x005011cb`), before anything that can fail; then
`FUN_004de7e0( place, &cell, &subX, &subY )` with the whole dword place; then the two 8.8 words `X = ((cell - 1) & 0x7f)
<< 8 | subX` and `Y = (((cell - 1) >> 7) & 0xff) << 8 | subY`; then `FUN_004fa5f0( X, Y )` (`0x0050124c`). A route:
SetState(12), answers 1. None: `"QQQ - FindQueueDestination SetDest failed, so I'm standing in queue"`, answers 0, the
state unchanged - **and every caller then puts the guest out**, whatever the string says. It draws the engine's
generator (`FUN_00516330`) exactly once on every call past the -1 exit, inside the arm, route or no route.

**The place becomes a point.** `FUN_004de7e0` branches on the queue-path bit (`+0x32 & 8`, `0x004de7e3`) and returns
nothing anyone reads; every other `+0x32 & 8` test in these helpers (`FUN_004de110`, `FUN_004de130`, `FUN_004de840`,
`FUN_004dec30`) has identical arms. Its `"Virtual queue problem!"` (place below 4, unsigned) is the bare `RET`. Both
arms take the same two numbers:

- **along** = `(u8)__ftol( (u64)n × 0.25f × 255.0f )` (the floats at `0x0070055c` and `0x00700560`; `__ftol` chops),
  which is `((n × 255) >> 2) & 0xff`: **0, 63, 127, 191** for n 0..3, then 255, 62, 126, 190, 254 ...;
- **J** = `FUN_00516330() % 28 + 114`, an unsigned `DIV`, 114..141 - the jitter across the queue, drawn once and
  unconditionally, used or not.

**With the bit, `FUN_004de840`** walks from the FRONT: the cell `FUN_004de040` names (the one the entrance's
`mNeighbours` points at), then `while ( n >= 4 && cell ) { n -= 4; cell = FUN_004de670( cell ); }` - one queue cell per
four places, both tests unsigned - and writes the cell (`0x004de8a6`) before anything else. For along 128 or less
(unsigned, `0x004de8db`) the switch reads **this cell's** `mDirection`. For along above 128 - only the fourth place of
a cell - it reads **the next queue cell's** `mDirection`; at the back cell, where there is none, it takes the side
**opposite** the first of N, E, S, W (bytes `01 04 10 40`, `0x004dea15`) that this cell's `mNeighbours` holds (asked
twice) and whose neighbour is plain path (`+0x8 == 1`, `FUN_00536310`); with none of those, this cell's own
(`"*** No path attached to end of queue! ***"`, the bare `RET`).

**Without the bit, `FUN_004dec30`** does no walk: the cell is `GetBackOfQueue` (`FUN_004de130`, `0x004dec9f`) and the
switch reads **the entry cell's** `mDirection` (`mEntryPos`, `0x004decd1`), with n the whole place, so a fifth guest
wraps to along 255 on the same cell. It calls `FUN_004de130` a second time into a dead slot; the cache makes it a no-op.

| Direction | First switch: along 128 or less, and every `FUN_004dec30` place | Second switch: along above 128 |
|---|---|---|
| `0x01` | (J, along) | (J, along) |
| `0x10` | (J, 255 − along) | (J, 255 − along) |
| `0x04` | (255 − along, J) | **((along + 128) & 0xff, J)** - `ADD AL,0x80` at `0x004deb26`, so 191 gives 63, not 64 |
| `0x40` | (along, J) | (along, J) |

The pairs are (subX, subY), each 0..255 of a cell. Any other direction writes **neither** sub byte (the first switch
and `FUN_004dec30`'s log `"Dodgy cell direction"`, the second nothing), and FindQueueDestination routes with whatever
its stack held. A queue cell's `mDirection` points at the front - `FUN_004de670` accepts a neighbour only when its
direction points back at the cell it comes from - so **place 0 stands on the front edge of the front cell, and each
place after it 63/256 of a cell further back**; the fourth in a cell turns to the next cell's axis, and at a bend
stands on the side toward it.

**A place past the queue's cells.** When `FUN_004de670` runs out (or `FUN_004de040` finds nothing), the arm writes
**cell 0** with the remainder. Cell 0 reads world fields as a cell (`base − 0x44` = world `+0x294`; its `mNeighbours`
and `mDirection` at world `+0x2a0`, zeroed by `FUN_004f7e80`, `0x004f7e95`), so no sub byte is written, and
FindQueueDestination packs cell 0 as x 127, y 255, which the byte-wise pathfinder cannot reach (every step off the map
edge is refused): the route fails and the guest is put out. On an unchanged map `place < 4 × +0x40` keeps this away;
the arm re-walks from the start each call, while `GetBackOfQueue` answers its cached `+0x3a`.

**The route - `FUN_004fa5f0`** is `FUN_004fa530` (SetDest to a cell, which aims at `0x80, 0x80`, **the centre**)
taking any 8.8 point, in order: (1) the route counter `[0x007cdb98]` goes up (`FUN_004d8c50`; a route found puts it up
again, stamping walker `+0x48`); (2) `mStrandedTime` (`+0x198`, named by the serialiser at `0x004f8eac`) is zeroed if
the counter is below it; (3) if `mStrandedTime` is still non-zero and `FUN_004fa770` answers 1 - no 16×16 region stamp
in the 3×3 cells around one base cell (the far end of the guest's queue run when they stand on a queue cell, else
their own) is at least `mStrandedTime` - it answers 0 **having written nothing**; (4) otherwise `+0x1a` = Y, `+0x198`
= 0, `+0x18` = X, then `FUN_00510100( MOVSX( X ) << 8, MOVSX( Y ) << 8 )` on the walker (guest `+0xd4`) - a 16.16
target, the sub byte s landing at s/256 of the cell - and it answers the route's 1 or 0. A failed route keeps the
target, zeroes the waypoints, and sets walker `+0x60` = 1 and `+0xb8` (guest `+0x18c`) = 1, so the next walk tick
answers 2 - unless, in that tick's own step, the path follower's map-change re-plan (a 16×16 block stamp newer than
the walker's `path_timestamp`, `+0x48`, which a failure never writes) routes to the stored target after all
(`0x0050ed79`). **`mStrandedTime` is set only at the dead end of `FUN_004f9490`'s linked walk** (`0x004f9e09`, from
states 6 and 7, for a person whose type byte is not 4..8; "Deciding and wandering") and **every walk tick zeroes it**
(`0x004fa30b`), so on the three queue paths it is nought unless a save loaded it.

The pathfinder (`FUN_0050f8e0` → `FUN_00511420` → `FUN_00511470`, a line stepper with wall-following, 60000 iterations
and at most 250 cells, then `FUN_005108a0`'s up to nine splices, which cannot rescue a failed first search) takes the
start and target cells as bytes; the walker keeps a window of five cell-centre waypoints and plans again from where it
stands when the window is used up, which can fail mid-walk too. Every step asks the edge test
`FUN_004d8750` - OpenTPW's `CellEdge.Blocked`, line for line. A queue (mType 3) or entrance (9) cell is entered only
from path or another 3/9 cell, and only when **the entered cell's** `mNeighbours` holds the side it is entered from;
mType 3 is left only onto linked path or an approach cell; the edge test never reads a queue cell's `mDirection`. So
**the edges let a guest be routed forward or back along a queue whose links are mutual**, and a route may end inside
one - along the Belly Bounce's `0x50, 0x44, 0x44, 0x44` every step is open both ways - though the line stepper must
still find the way. Waypoints are cell centres, passed at 0.4 of a cell; on the last one the walker steers at the exact
target, and **arriving is a radius, not a snap**: `FUN_0050ed10` flags it once the octagonal distance
(`|d|max + |d|min / 2`) is below `radius × 0x19999 >> 16` = `0x51eb`, about 0.32 of a cell (walker `+4`, the serialised
`radius`: `0x3333` as constructed, the saved value for a loaded guest). It is tested on the position before that
step's move, so the guest ends the tick near the point, not on it; nothing snaps them there.

**State 12.** SetState(0xc) writes the state, `+0xc2` = 0 (the first word of the speed table at `0x0075c7f0`, one of
three percentage terms `FUN_004fa870` sums) and `+0x10` = 1, and nothing else: no idle stamp, no delay. Its turn
(`FUN_005019f0` case 0xc) runs the walk tick and on arrival, **or on 2** (the route failed, a failed re-plan
included), sets 11; SetState(0xb) stamps `mTimeStartedIdling` and `+0x1f4 = trunc( mQueuePos × 1.2f )` from the byte
written when the place was taken. Nothing in state 12 reads the place again: the walker only re-plans toward the same
stored target (`"The ground changed under this peep..."`, or 6 of the last 15 steps stuck). A guest whose walk fails
part-way stands in state 11 where it stopped, with `mQueuePos` equal to their place, so the InQueue turn's equality
arm holds and nothing routes them again until their place moves; its other arms still apply.

**The three callers, and each failure.**

1. **Joining, `FUN_004ffbc0`** - the state-10 handler (`FUN_005019f0` case 10, `0x00501b0d`), in order. The walk tick
   (which zeroes `+0x198` first). **2, stuck:** `"The person has become stuck on their way to the ride they were
   interested in"`, event 3, `FUN_004fea70(2)` (`BigHappinessChange`, 25 in Lost Kingdom), `MajorDest` 0, state 6.
   **1, walking:** the park closed (`+0x1da710`): `"The park has closed underneath me!"`, −25, `MajorDest` 0, state 6;
   open: the byte `+0x2c` goes up, and above 11 it is zeroed and the minor decision `FUN_004fd570` runs - every 12th
   walking turn, counted across walks (nothing resets it). **0, arrived:** `MajorDest` nought, state 6. Then **the
   arrival test** (`0x004ffc3d`): the guest's cell, `y × 128 + x + 1` from bytes `+7` and `+5`, against
   `GetBackOfQueue` as a word. Not equal: `"The back of the queue has moved while I was walking here"`,
   `FUN_004fa530( GetBackOfQueue )` again, and a route keeps state 10; no cell or no route: event `0x16`, state 6,
   **`MajorDest` kept**. Equal: the gates. **Room** (`FUN_004dda20`): refused, event `0x15`, state 6, `MajorDest` kept,
   no dock. **Excitement**: `FUN_004fd4e0` always computed, asked only when the descriptor's `+0x13c`
   (`UsageInfo.ExcitementLevel`) has a non-zero low byte, refused at a difference of 45 or more (signed, `0x004ffc7a`):
   `"ride is not exciting enough!"`, event 5, thought `0xc`; or `"ride is too exciting!"`, event 4, thought `0xf`; both
   then `FUN_004fdc60` (the id onto `mPreviousTemporaryRides`, `+0x1e8`, where it divides the thing's score until
   aged out), `MajorDest` 0, `+0x1fc` 0, state 6, no dock.
   The difference is `FUN_004fd4e0`: the guest type's preference byte (`0x7850e4 + 12 × +0x1f0`) against
   `FUN_004e0860( object, 0 )`, **the object's computed excitement** (`FUN_004e0560`), clamped to ±50 and negated.
   **Too long** (`FUN_004ddb60`: `FUN_004ddf50( 0 ) >= FUN_004dda40()`, unsigned): `"queue is too long!"`, event
   `0x15`, `FUN_004fdc60`, thought `0x10`, `MajorDest` 0, state 6. Then the join: `+0x20c` = happiness (the snapshot
   `FUN_004fd970` compares after the visit), `FUN_004ddb90`, and FindQueueDestination (`0x004ffdad`): state 12 and
   return. Failing - a -1 included, when somebody in front has stopped queueing - `"Person %d: Couldn't get to my
   place in the queue, leaving!"`, `FUN_004ddd20`, `FUN_005012f0` (−15), state 6 again (`0x004ffdf4`). Nothing on
   the arrival reads the ride's `mCanLoad` or `mState`; only the park's door, and only while walking.
2. **The InQueue re-take** (`0x00500532`, arm 6 of "The `InQueue` turn"). The turn's own `FUN_004ddf50` has already
   answered, so the -1 exit cannot happen: it always writes the byte and draws once, and fails only on the route:
   `"Couldn't get to my intended queue position"`, `FUN_004ddd20`, `FUN_005012f0`, state 6 (`0x005004b3`). After a
   re-take the mood still runs on the same turn, in state 12, and can change it: a spot animation saves 12 and later
   returns through SetState(0xc), which does not route again - the walker keeps its target.
3. **The refused door** (`FUN_005006b0`, `0x00500826`). `AdmitPerson` refused a guest still linked at the head with
   `mBeenAdmitted` nought, so the place is 0: they walk back to place 0's point in state 12, then wait in state 11
   with delay 0 for a new call forward. Failing: `"Couldn't rejoin FOQ even!"`, `FUN_004ddd20`, `FUN_005012f0`,
   state 6 (`0x00500857`) - no `FUN_004e0ac0`, no thought.

**Where a guest is aimed.** The chooser `FUN_004fcb10` walks the object chain and, for each: `+0x32 & 4`;
`GetBackOfQueue` non-zero; the score (`FUN_004fcc30`, "What a thing is worth to a guest"); at least 10, unsigned;
above the best, signed, or equal to it on an odd `mGameTick`; the offer gate `FUN_004dd920`; then
**`FUN_004fa530( GetBackOfQueue )` - the centre of the back cell** (`0x004fcbc4`) - and on a route the best and
`MajorDest`. Its one caller, the state-6 turn `FUN_004fec90`,
draws the generator once at its top (`0x004fecb4`) and runs the chooser only on `rand % 3 == 0` and past the 30-turn
thinking gap. The routing happens inside the chooser's walk, so **a better candidate that passes the gate but cannot
be routed still rewrites the walker** with its failed route, while `MajorDest` names the earlier winner; the caller
then sets state 10 (event 2), and the first state-10 turn answers 2 and takes the stuck arm, −25 - or, if the ground
near the guest changed since their last good route, the re-plan revives the loser's route and they walk to the loser's
back cell under the winner's name, to be re-aimed there. **The score is measured at the back cell too**:
`FUN_004fcc30` asks `GetBackOfQueue` of the object (`FUN_004de110` with the object in `ECX` at `0x004fcc49`,
`0x004fcc65` and `0x004fcc7d`; the guest is `EDI`) and reads the squared distance from the guest's cell (bytes `+5`
and `+7`), the close-to-queue test (under 9) and the nearby-effects divisor (the word `+8` of what `FUN_004d8410`
answers for that cell id; its log says "nearby fireworks") all at that cell.
OpenTPW's `ParkRideChooser.ScoreOf` reads the three at the entry cell (Q105). With nothing chosen the caller pushes event 1, plays spot
animation 4, runs `FUN_004fea70(0)` and restamps `+0x1fc`. The other aims: the minor decision `FUN_004fd570` ("A second
toilet", below) looks in a 4×4 window for a thing that passes the offer gate and scores best from nought, an equal
score winning on an odd tick (no threshold of 10), switches to it only when the raw line search (`FUN_004d8b40`, no
splices, the first leg uncounted) from the current thing's entry to the guest is longer than to the new thing's entry,
and aims at **its entry cell** (`+0x36`), switching `MajorDest` first (the old kept at `+0x1de`)
and ignoring the route's answer; the InQueue turn's board arm aims at the stand point with
`FUN_004fa5f0` (state 13); state 15's `FUN_00500900`, on arriving with a saved `+0x1de`, restores it and aims at
**that thing's back of queue** (state 10); `FUN_00500dc0` aims at the entry of a `+0x32 & 0x40` thing (state 9).

**Lost Kingdom**, read in the running game with `cell` (`docs/QUEUE.md` Q50e). Only the Belly Bounce (thing 13) has
the bit. Its queue, front to back: (52,22) `mDirection 0x10`,
then (51,22), (50,22), (49,22) `mDirection 0x04`; `mNeighbours` `0x50, 0x44, 0x44, 0x44`; the path beyond the back is
(48,22), so the back cell's fourth place turns to the side opposite W, `0x04`. The original stands its sixteen places
at, in cells:

| Places | Cell | Places 0-2 of the cell (x, y) | Place 3 of the cell (x, y) |
|---|---|---|---|
| 0-3 | (52,22), `0x10` | (52 + J, 22 + 255/256), (52 + J, 22.750), (52 + J, 22.500) | next cell `0x04`: (52 + 63/256, 22 + J) |
| 4-7 | (51,22), `0x04` | (51 + 255/256, 22 + J), (51.750, 22 + J), (51.500, 22 + J) | next cell `0x04`: (51 + 63/256, 22 + J) |
| 8-11 | (50,22), `0x04` | the same on (50,22) | next cell `0x04`: (50 + 63/256, 22 + J) |
| 12-15 | (49,22), `0x04` | the same on (49,22) | path to the W, so `0x04`: (49 + 63/256, 22 + J) |

J is 114/256..141/256 (0.445..0.551) of the cell. The other five queueable things have no bit and take `FUN_004dec30`.
The Jungle Spray's entrance (52,30) and the Drinks Shop's (43,30) read `mNeighbours 0x01` and `mDirection 0x10`, their
back of queue the path cell north of each, (52,29) and (43,29): places 0 to 3 stand at y + 255, 192, 128 and 64
(/256) of it, J across. Each toilet's entrance, (55,15), (55,16) and (55,17), reads `mNeighbours 0x04` and
`mDirection 0x40`, its back of queue the path cell east of it, (56,y): places 0 to 3 at x + 0, 63, 127 and 191. In
all five the head stands on the entrance's edge, and a fifth guest wraps to along 255 on the same cell. The entrance's
`mDirection` is not one rule: the Belly Bounce's (52,23) reads `0x01`, toward its queue.

**OpenTPW builds it** (`docs/QUEUE.md` Q50g). `ParkQueuePlace` is `FUN_004de7e0` and its two arms;
`PeepBehaviour.FindQueueDestination` is `FUN_00501160`, and its three callers are `JoinTheQueue` (after the arrival
test and its re-aim), `QueueTurn`'s re-take and the refused door in `Step`'s `BeingAdmitted`; `ChooseSomewhereToGo`
aims at the back cell's centre. An 8.8 sub byte becomes a navigator coordinate as `s × FixedVector.One / 256`. The
arrival radius is the same (`DefaultRadius = One / 5`, times 1.6), and so is a route of no length arriving at once:
`FUN_0050fd40` answers `0x10000` when its total `+0xa0` is nought (`0x0050fda8`), as `PeepNavigator.Progress` does.
**Where it still differs.** The jitter draws from `PeepBehaviour`'s `System.Random`: `RideScript.NextDraw` reproduces
`FUN_00516330` exactly, `0x80000000` included, then halves it as `RAND`'s unsigned `SHR 1` does, but per script and
seeded 1, where the engine's is one state for the whole park, the world's `mRandomSeed`, reset to an id at eight
points (`park.md`, "`RAND` (28)"), so only the range and the one draw a call are the original's.
Seed 1's cycle, 248,316,293 states, never meets `0x80000000`, which lies on another of 1,300,914,561.
A direction neither switch knows stands the point at the cell's centre, counted
`QUEUE_PLACE_DODGY_DIRECTION`, where the original routes with whatever its stack held. A place past the queue's cells
is refused before routing, where the original routes to (127, 255) and fails. `FUN_004fa5f0`'s stranded refusal is
absent: nothing keeps `mStrandedTime`. State 10's own arms, the gates' side effects and the chooser's in-walk routing
are Q102 to Q104.

The supporting helpers:

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004de7e0` | — | The dispatcher on `+0x32 & 8`: thiscall on the object, `( place, &cell, &subX, &subY )`, `RET 0x10`, forwarding all four; its answer, always 0, is ignored. | Disassembly |
| `FUN_004de840` | — | The queue-path arm: the walk from the front, one cell per four places, the two switches. Writes the cell first, draws once. | Disassembly |
| `FUN_004dec30` | — | The virtual-queue arm: `GetBackOfQueue`'s cell, the entry cell's direction, no walk, no bound on the place. | Disassembly |
| `FUN_004de670` | — | The next queue cell, `cdecl ( &out, cell )`: probes N, S, E, W through `FUN_004d96f0` (NULL off the map, each checked) and takes the first neighbour of mType **exactly 3** (`FUN_00536320` 3 or 9, `FUN_00536340` not 9) whose `mDirection` points back (`0x10`, `0x01`, `0x40`, `0x04`); its own `mNeighbours` is never read. Writes the neighbour's id word, or 0. | Disassembly |
| `DAT_007cdba0`..`…bdc` | — | **EIGHT step vectors, not four.** They are `(dx, dy)` pairs written by per-object static initialisers (so the image reads zeros — do not conclude they are unset), laid out in link order rather than compass order. Measured from the jump table in `FUN_004d97e0`: `0x01`→`ba0/ba4` (0,−1); `0x02`→`bd8/bdc` (+1,−1); `0x04`→`bc8/bcc` (+1,0); `0x08`→`bb8/bbc` (+1,+1); `0x10`→`ba8/bac` (0,+1); `0x20`→`bb0/bb4` (−1,+1); `0x40`→`bd0/bd4` (−1,0); `0x80`→`bc0/bc4` (−1,−1). **This independently confirms the compass in `park.md` from the executable rather than from save statistics.** | Static-initialiser immediates + jump table |
| `FUN_004d97e0` | `CMapCell::GetNeighbouringCell( Direction )` | Named by its own assert, `"Incorrect use of function CMapCell::GetNeighbouringCell( Direction )"` at `0x0075b054`. Exactly eight of its 128 map entries are legal — a **single** compass bit — and every other value reaches that assert. | Its own assert |
| `FUN_004de040` | — | **Start of queue, and it reads `mNeighbours`, NOT `mDirection`.** It calls `FUN_00522770` with the object's own entry cell (`LEA ECX,[EDX + ECX*0x4 + -0x44]` off `mEntryPos`), takes the **first set bit** in the fixed order `0x01`, `0x10`, `0x40`, `0x04`, and returns `mEntryPos + dy*128 + dx` (a 16-bit add), or 0 with none of the four (`0x004de0f9`). Its whole body holds **one** call, so it checks nothing — not the cell's type, not the map edge, not `+0x32`. All the checking is `FUN_004de670`'s. | Disassembly |
| `FUN_004d99c0` / `FUN_004d96f0` | — | The neighbour lookup pair. `FUN_004d96f0` works from the cell's own id word and answers NULL off the 0..127 map (`FUN_004d8300`, signed). | Disassembly |
| `FUN_00536310` / `FUN_00536320` / `FUN_00536340` | — | Cell predicates on the type dword `+0x8`: 1 (path); 3 or 9 (queue or entrance); 9. | Disassembly |
| `FUN_004de130` | GetBackOfQueue | Named by its own `"*** GetBackOfQueue() crashed! ***"` (`0x0075b864`). Answers the cached `+0x3a` when non-zero, walking nothing. Otherwise `+0x40` = 0, `+0x3a` = the start, and up to 1000 steps of `FUN_004de670`, each writing `+0x3a` and adding 1 to `+0x40`: the last cell and the count, the start included. A start of 0 answers 0 silently and re-walks every call; a chain of 1000 or more logs, answers 0 once and leaves `+0x3a` set, so the next call answers that. `FUN_004de110` is the same call. | Disassembly |
| `FUN_00522770` | `CMapCell::GetNeighbours` | Nine instructions: returns the cell's byte at **`+0xc`**, which the game's own cell serialiser `FUN_004d0b30` names **`mNeighbours`** (`LEA EAX,[ESI+0xc]` paired with the string `"mNeighbours"` at `0x0075a064`). It returns `+0x22` (`mHoardingNeighbours`) instead only while `DAT_0081b4cc` is set **and** the cell's `+0x2` is 2 — an overlay path only three editing functions raise (`park-engine.md`, "`FUN_00522700` has a second arm"); it reaches the start of queue, the attached-path loop and every edge test. `+0xd` is `mDirection` and is a different field, read by `FUN_00522850`; conflating the two inverts every queue walk. | Disassembly + the serialiser's own strings |
| `FUN_004dda20` | — | The queue-room test, `FUN_004ddf50( 0 ) < +0x40 × 4`, unsigned. Asked with id 0, `FUN_004ddf50` never answers -1: it counts from `mFirstInQ` up to **and including** the first guest who has stopped queueing, and no further. | Disassembly |
| `FUN_004dda40` | — | The longest queue a guest will join (`FUN_004ddb60`, the arrival's third gate) or stay in (the InQueue turn's 5a): 100 for a thing without the queue-path bit (`0x004dda4c`); with it, the capacity in "The `InQueue` turn", over `Upgrades[l].QueueWaitTimeConstant` (`+0x1b4`), `InitSpeed` (`+0x1a8`) and the ride's settings. | Its two callers |
| `FUN_004fa5f0` / `FUN_004fa530` | SetDest | To an 8.8 point / to a cell's centre. The stranded refusal, then `+0x18`, `+0x1a`, `+0x198` written before the route. | Disassembly |
| `FUN_004fa770` | — | The stranded refusal: 1 when no 16×16 block stamp of the 3×3 cells around the guest (or the far end of their queue run) reaches `mStrandedTime` ("The stranded bookkeeping"). `FUN_004de1f0` stamps the back of a queue it measures again, so a queue edit frees its guests. | Disassembly |
| `+0x198` | `mStrandedTime` | Saved (`FUN_004f8b10`, `0x004f8eac`). Set only at `0x004f9e09`, the dead end of SetRandomDest's linked walk; zeroed by every walk tick, SetDest, and `FUN_004fa030` when the counter is below it. | Serialiser string |
| `FUN_004d8750` | — | The edge test every route step asks: `( x, y, dir 0 N / 1 E / 2 S / 3 W, mode )`, non-zero blocked. A guest walks in mode 0 (walker `+0xb4`, guest `+0x188`; guests write only 0 or 1). | Disassembly |
| `FUN_0050fd40` / `FUN_0050ed10` | — | The walk tick's progress (`0x10000` = arrived) and the path follower that flags arrival within 0.32 of a cell of the exact target. | Disassembly |
| `FUN_00516330` | — | The engine's generator: `state = ROR32( state × 0x19660d + 0x3c6ef35f, 13 )`, kept at world `+0x1da708`, answered as its absolute value (`0x80000000` unchanged). One sequence for the queue arms and the scripts' `RAND`. | Disassembly |

### Every way out of a queue

Decoded 2026-09-24 (`docs/QUEUE.md` Q50): five decoders, one per caller, each report put to a refuter reading the
disassembly. **`FUN_005012f0` is the one way out of a queue, and it has seven callers** (`get_xrefs_to` and a byte
scan of `testme.exe` agree). It logs `"Person %d: dismissed from queue on ride %d"`, puts event 6 in the ring, plays
the kids' `0x80` when the guest's id `& 7` is nought (`0x0050133d`), takes `MediumHappinessChange` off
(`FUN_004fea70(1)`, `0x00501359`, clamped 0..100), zeroes `mQPrev`, `mQNext`, `mBeenAdmitted`, `MajorDest` and
`mQueuePos`, and sets state 6. **It never unlinks**: six callers run `FUN_004ddd20` first, the sale does not.

| Site | Caller | When | Also on the path | Happiness | OpenTPW |
|---|---|---|---|---|---|
| `0x004fb409` | `FUN_004fb360`, the sale's type-10 answer | queueing for a thing sold or picked up | no unlink; then the sale's own `SmallHappinessChange` | −15 −5 | built, `PeepBehaviour.ThingRemoved` |
| `0x005014b4` | `FUN_00501390`, told by `FUN_004de1f0` | place `>=` cells × 4, unsigned, and not state 14 | thought `0xd` when id % 3 is nought; `FUN_004ddd20` | −15 | built, `ParkPeople.QueueRemeasured` |
| `0x004e0554` | `FUN_004e0450`, the object's completion | the head, when `VAR_LETMEON` still names them or they are not in state 14 | `FUN_004ddd20` | −15 | built, `ParkPeople.CompleteOrTurnAway` |
| `0x004ffdf4` | `FUN_004ffbc0`, arriving at the queue | joined, and `FUN_00501160` finds no route to their place, or answers -1 because somebody in front has stopped queueing | `FUN_004ddd20` | −15 | built, `PeepBehaviour.JoinTheQueue` |
| `0x005004b3` | `FUN_004ffff0`, the `InQueue` turn | nine arms, below | `FUN_004ddd20`, a thought on most arms | −15 | the lost place, the failed re-take, the toilet and halves of 5a and 5b built, `PeepBehaviour.QueueTurn`; the rest counted |
| `0x005007b4` | `FUN_005006b0`, at the door | `FUN_004fde50` says too expensive | thought 6, event 10, **a first −15** (`0x00500778`), `mNumWalkAways` +1 (`FUN_004e1670`), `FUN_004e0ac0`, `FUN_004ddd20` | −30 | built, `PeepBehaviour.WalkAwayFromTheDoor` |
| `0x00500857` | `FUN_005006b0`, at the door | `AdmitPerson` refuses and `FUN_00501160` fails: `"Couldn't rejoin FOQ even!"`; no `FUN_004e0ac0`, no thought | `FUN_004ddd20` | −15 | built, `PeepBehaviour.Step`, `BeingAdmitted` |

**`FUN_004ddd20` is the whole of leaving**: it empties script variable 0 (`VAR_LETMEON`) when it names the leaver
(`0x004ddd4e`..`0x004ddd7d`), then splices with the leaver's own links and tests no membership - with no `mQPrev`
it writes `mFirstInQ` = the leaver's `mQNext` (`0x004ddde9`), so an unlinked leaver clears the head. OpenTPW's is
`ParkRideOperation.LeaveQueue`, over `ParkState.LeaveQueue`, which splices the same way and reports only whether the
leaver was at the head or linked.

**`FUN_004ddf50` (GetPositionInQueue) gives up at a guest who has stopped queueing.** Walking from `mFirstInQ`, it
asks `FUN_00502430` of every guest it steps past, the head included, and answers -1 at the first who fails
(`0x004ddfa9`, `0x004ddfbf`). The guest sought is never asked. So a stale link puts everybody behind it at -1.

#### The queue measured again - `FUN_004de1f0` and `FUN_00501390`

`FUN_004de1f0` zeroes `mBackOfQueue` (`+0x3a`), re-walks the cells (`FUN_004de130`, which rewrites the count at
`+0x40`), logs `"Object's queue is now %d cells long"` and `"Telling people in queue to reevaluate"`, and walks the
queue head first, reading each `mQNext` before the call (`0x004de2bd`) and **skipping the object's nominee** `+0x6c`
(`0x004de2b9`). Each guest runs `FUN_00501390`: the object from their own `MajorDest`, the place from `FUN_004ddf50`,
and `place >= cells * 4` compared unsigned (`0x00501413`..`0x0050141c`, so -1 is past the end); **state 14 is never
put out** (`0x00501422`) - the raw `+0x220`, so a guest in state 8 whose saved state is 14 is. Then `"The queue was
shortened and there's no room for me any more"`, thought `0xd` when the id divides by three (`0x0050148a`),
`FUN_004ddd20`, `FUN_005012f0`, and `MajorDest` = 0 and state 6 again. **Then its tail** (`0x004de2d5`..`0x004de48c`):
it logs `"Back of queue is %sconnected"` (`FUN_004de4a0`) and, when the ride is closed (`mCanLoad` nought,
`0x004de2f7`), passes an inlined copy of the open guard `FUN_004df290` (not in state 1, 4 or 2, `mRequestedService`
`+0x64` nought, the back of the queue connected, and for type 3 `FUN_00441970`) and, for track types 1 to 3, has
`mIsTrackRideValid` (`+0x2c`, `0x004de3da`), opens it again with an inlined copy of `FUN_004df390`: `mCanLoad` = 1,
`FUN_004547c0( model )` (not a sound; see `FUN_00454550`), `VAR_RIDECLOSED` = 0, SetState(0) (`0x004de487`). **It
always zeroes `mAssignedStaffMember`** (`+0x5e`, `0x004de48c`), so every queue measured again makes the ride forget
who was servicing it; `+0x60` and `+0x64` stand. Nothing in the tail reads the park's door: a closed ride whose queue
is edited opens whatever the door says. OpenTPW builds it and counts what it leaves out: thought `0xd`
(`QUEUE_SHORTENED_THOUGHT_0xD`), the back cell's stamp (`QUEUE_REMEASURE_BACK_CELL_STAMP`), and the guard's and the
open's own counted parts ("The closed ride"). The walk and the tail are
`ParkPeople.QueueRemeasured`, the tail `ParkRideOperation.ReopenAfterRemeasure`. Its eight call sites, each with the
object in `ECX`:

| Site | Transaction | Shortens? | OpenTPW |
|---|---|---|---|
| `0x0052537a`, `0x005259ae`, `0x00529890` | a thing bought, moved or placed: its own new, empty queue | no | `ParkBuilding` buy |
| `0x00526118` | the queue-edit arm (`0x14`), after `FUN_00530120` detaches the back | no | `ParkPathBuilding.EditQueue` |
| `0x00527541` | a queue run laid (mode 3) | no | `LayQueue`, `RunQueue` |
| `0x00534858` | the stamp: path laid over a queue cell | **yes** | `LayPathRun`, `ParkBuilding.LayPathStub` |
| `0x0053694b` | `ClearCell`'s path arm, a path joined to an entrance cleared: the link goes first, so the queue measures **0** and all but the nominee and state 14 go | **yes** | not built: `ClearPathCell` re-walks no entrance |
| `0x0052ffec` | `FUN_0052fe50`, the backtrack: Backspace with the queue tool (`FUN_0052fe50(0,1)` at `0x0040beb3`, gated on a vtable answer of 3), and the demolisher's drain before the destructor | **yes** | Backspace counted (`BACKSPACE_UNDO_QUEUE_RUN`); the drain, `ParkPathBuilding.DrainQueue` |

The console's `delqueue` (`LiftQueue`) re-measures too. In Lost Kingdom the one queue that could be cut is the Belly
Bounce's: cells (52,22), (51,22), (50,22), (49,22), of which (52,22) is NOMODIFY, and path over (51,22) would leave
one cell and room for four. **The path tool refuses that click in the shipped map.** Its verdict `FUN_00535670`
lets path over a queue cell only when the cell's `mNeighbours` has exactly one bit (`FUN_00522790`,
`0x00535ce0`..`0x00535ce9`) and the cell that way is a queue cell (`0x00535ced`..`0x00535cfe`); otherwise it answers
red (`0x00535d12`). All four cells carry two bits (0x50, 0x44, 0x44, 0x44). The console's `path`, which Q50's game
run used, calls `LayPathRun` without the verdict.

#### The sale's drain - `FUN_00530120` and `FUN_0052fe50`

Decoded 2026-09-24 (`docs/QUEUE.md` Q50f): four decoders (the list, the pop, the measure, the sale end to end), each
report put to a skeptic reading the disassembly, then a critic over all four - 108 claims, 85 upheld, 23 amended, none
refuted. **The drain puts out every queuer from the fifth place back, but the nominee and raw state 14, and leaves the
first four to the sale.** It runs in the demolisher (`FUN_00527ee0`, `0x00527f99`..`0x00528013`) before the footprint
passes and the destructor, for a sale and for a move alike.

**The gate.** Only a thing whose descriptor has `HasQueue` (`+0x40`, `0x00527f93`) drains, and only while its CACHED
queue length `+0x40` is above nought (`0x00527fb4`); otherwise `FUN_0052fbd0` throws the list away. In Lost Kingdom
that is the Belly Bounce alone: the class files give the Jungle Spray, the Drinks Shop and the toilets
`Info.HasQueue 0`, so their queuers meet only the sale's message.

**The list, `FUN_00530120( object )`.** It empties the pending list (`0x00530150`) and switches on the entry cell's
whole `mNeighbours`, with an arm for a single cardinal bit only (`0x0053019a`). It pushes the cell that bit faces as
element 0 (`0x005301ff`) - or, when that is a queue or entrance cell of another owner (`+0x10`, `0x005301ea`), pushes
it alone and returns. Then it walks **from the entry cell itself** (`0x0053024a`): a type-3 or 9 cell that is a corner
(`FUN_0053ae00`: two cardinal links, one each way) gets `+0x20` = 1, is pushed and turns the walk
(`mNeighbours ^ Opposite( dir )`, `0x005302aa`); any other gets `+0x20` = 0. The walk stops at a type-3 cell with one
link of the eight (`0x005302f7`); at a path, which it cuts - the last queue cell loses its bit toward the path and the
path its bit back, both retiled (`0x00530380`, `0x0053039c`), and when the entry cell faced the path directly only the
path's bit goes; or at anything else. The cell it stopped on is pushed and anchored (`0x00530494`). An entry cell with
no link pushes the one cell its angle names (`0x005303f1`). A multi-bit entry cell has no arm and the walk never
leaves it. Nothing is written on the object. A faced cell that is a corner is pushed twice, so **the Belly Bounce's
list is (52,22), (52,22), (49,22)**, and (49,22) is cut from (48,22) before any clear.

**The pops.** Under mode 3 (`FUN_0052f200( 3, 0 )`) and the force flag, the demolisher calls `FUN_0052fe50( object,
1 )` until it answers nought. Each call pops the top as T (`0x0052fea9`); if that leaves the count at nought it writes
1 back, re-arms mode 3 (`0x005300a6`) and answers nought (`0x0052fecf`), so **the bottom entry is never T**. Otherwise it
arms mode `0x34`, whose apply at T only anchors (`0x00525f37`), and applies at the new top P (`0x0052ff4b`): op `0x32`
then op `0x86` over the straight line from T to P, P included and last (`FUN_00536100`, which walks the longer axis
holding T's other coordinate, a tie along Y: `JLE` at `0x00536140`) - unless the mode was 3 and P is a path
(`0x0052ff9a`), when nothing is cleared. **So the bottom entry is cleared as the far end of the last pop.** Then it
re-arms mode 3 (`FUN_0052f580( 3, 1 )` at `0x0052ffcb`, which posts advisor `0xcb` and sets cursor 4), measures the
queue again with the object in `ECX` (`FUN_004de1f0`, `0x0052ffec`), and answers 1. A list of N entries gives N calls,
N re-arms, N-1 clears and N-1 measures, and leaves the count at 1. The demolisher's own `FUN_0052f200( 3, 0 )`
(`0x00527fa5`) posts `0xcb` as well, before the gate, so **the Belly Bounce's sale posts `0xcb` four times**, and a
thing that fails the gate once. The forced clear refunds a
queue cell when its owner cell is typed (`0x00536a37`), resets it whole and **unlinks no neighbour** (`0x00536a0e` to
`0x00536bc9`, past the loop at `0x00536b60`); a cell already bare is left alone (`0x005367ed`).

**What the queue measures.** `FUN_004de040` takes the entry cell's first linked side without asking what lies there,
and `FUN_004de130` counts that cell before it asks for the next (`0x004de1ae`). The drain leaves the entry cell's link,
so **every measure answers at least one cell**: the faced cell, bare or not, and the queue cells still standing behind
it. The runs go from the back toward the entrance and the last pop always reaches the faced cell, so **every drain ends
measuring one cell, room for four.** That back is bare, so `FUN_004de4a0` answers not connected and nothing reopens. A
measure between two pops that leaves queue cells standing, which only a queue with a corner past its node has, finds
a back with both its links, reads connected, and would reopen a closed ride that passes the inlined guard.

**Who goes.** Each measure puts out, head first, every queuer at a place `>=` four times the cells (unsigned, so -1
too), except the nominee (`0x004de2b9`) and a guest in raw state 14 (`0x00501422`): `FUN_00501390`, −15, `MajorDest`
nought, state 6. The Belly Bounce's drain:

| Call | T to P | Cleared | Measured | Put out |
|---|---|---|---|---|
| 1 | (49,22) to (52,22) | all four cells, four refunds, no unlink; the node loses NOMODIFY | 1 cell, back (52,22), not connected | every queuer from place 4 back but the nominee and raw state 14 |
| 2 | (52,22) to (52,22) | nothing, already bare | 1 cell | nobody |
| 3 | - | nothing; the count written back to 1, mode 3 re-armed | - | - |

Then one cell's worth is debited (`FUN_004d01f0`), which subtracts only while the bank's `+0x114` is non-zero
(`0x004d01f3`), and the force flag drops. Nothing after the drain measures the queue again: the second footprint pass
unlinks the entry cell through the unforced queue arm, which never re-measures, and message `0x1b`, sent before the
destructor (`FUN_0050b780`), has no guest among its subscribers. **So at the type-10 message the first four places,
the nominee and a queuer in raw state 14 still name the thing, and lose 15 and 5, 20 in all; everyone the drain put
out has `MajorDest` nought and loses nothing more, 15 in all.** A move walks the queue once more at the pickup
(`0x0048d006`), which cuts the same end, and the demolisher's own walk then gives the same list; it reaches the
demolisher only while the red-cell latch `DAT_00816d48` is clear.

**OpenTPW builds it** (`docs/QUEUE.md` Q50h): `ParkPathBuilding.QueueEnds` is the list, `DrainQueue` the gate, the
pops and the debit, and `ClearQueueLine` one run under force; each pop measures the queue through
`ParkState.RemeasureQueue`. Counted, not built: the advisor `0xcb` posts, one from the demolisher's mode 3 and one
from each call's re-arm (`QUEUE_DRAIN_ADVISOR_0xCB`); `FUN_004d8c60`'s write in every measure (`QUEUE_REMEASURE_BACK_CELL_STAMP`); the
per-age percentage, on the refunds (`QUEUE_REFUND_DEPRECIATION`) and on the debit (`QUEUE_DRAIN_DEBIT_DEPRECIATION`,
`0x00527fe8`); a run over anything but queue or bare ground
(`QUEUE_DRAIN_CLEARS_ANOTHER_KIND`); and a walk past a thousand cells, which the original's never gives up
(`QUEUE_END_WALK_UNBOUNDED`). **Open:** whether the advisor's `0xcb` posts are heard (message `0xcb` is response 422, sample 392, the queue
builder's help: `advisor-park.md`). The bank's `+0x114` is `mWithdrawalsEnabled`, 1 in every offline park ("The cost
of goods and the park's money"). `FUN_004e2290`, the per-age
percentage the refund and the debit both scale by, is decoded in `park-engine.md`, "Sell, move and the scrap value". `FUN_004d8c60` (`0x004de266`) writes a fresh
counter value into the back cell's 16 × 16 block stamp ("The stranded bookkeeping").

#### The closed ride - `FUN_004e0450`

Decoded 2026-09-24 (`docs/QUEUE.md` Q50b): five decoders, each report put to a refuter reading the disassembly.

**The completion.** The script's `VAR_LETMEON` is read; if it differs from `mFirstInQ` and the head's raw state at
`+0x220` is 14 (no look-through to `+0x224`, `0x004e04b5`), it logs `"Object %d: script admitted person %d but he
doesn't know yet, forcing him onto ride"` and calls `FUN_00500870` on the head, which boards them. Otherwise, if
`mFirstInQ` is not nought, it puts the head out: `FUN_004ddd20` on the calling object, then `FUN_005012f0`
(`0x004e0554`), −15. **One head per call, no loop**, and no nominee or script write but `FUN_004ddd20`'s. Its five
callers are SetState 1, 2 and 4 (`0x004e0ea3`, `0x004e0fa2`, `0x004e10a6`), the states-1/2/4 turn (`0x004e0e20`) and
`Invite`'s `mCanLoad == 0` bail (`0x004e13fc`), which runs it and returns, so the watchdog at `0x004e1382` is not
reached. **The bail is the common one**: a close leaves `mState` alone, so each later state-0 turn puts one head out.
On the turn a closed ride's `VAR_BROKEN` goes non-zero, SetState(1 or 4) runs it again, and that turn puts out two.
OpenTPW: `ParkPeople.CompleteOrTurnAway`, from the bail and from the states-1/2/4 turn.

**What closes a ride.** Every close clears `mCanLoad` (`+0x68`) and the nominee (`+0x6c`, a word), writes
`VAR_RIDECLOSED` (script variable 6) = 1, calls `FUN_00454550( model, 1 )`, and leaves `mState` alone. SetState 1,
2 and 4 close the same way and do set the state ("Where an object's state comes from").

| Close | Reached from | Guard | Besides |
|---|---|---|---|
| `FUN_004df300` | the park's door (`0x0051a1ae`); the ride window's door (`FUN_004af600` case `0x3e38` → `FUN_0048ccf0(1)`); the track editor (`FUN_00447520`, `0x0044755c`) | none | logs `"Object %d: Closing..."` and the nominee it lets go of |
| `FUN_004df150` | a blocked exit (`0x0050163b`) | only an open ride | posts a type-`0x14` message first |
| inline, the constructor `FUN_004db090` | every object with the queue-path bit, which it sets from `Info.HasQueue` (descriptor `+0x40`, `0x004db420`) (`0x004db712`..`0x004db793`) | none | so a bought queued thing starts closed, until a queue measure finds its back connected |
| inline, `FUN_004dfe30(1)` | a mechanic called (the ride window's `b_callmech`) | `+0x64` nought | sets `mRequestedService` (`+0x64`) = 1 first |
| inline, the repair `FUN_004df8f0` | after opening, a type-3 track whose `FUN_00441970` answers nought (`0x004dfd1e`) | none | never a non-track ride |

**What opens one.** `FUN_004df390`: `mCanLoad` = 1, `FUN_004547c0( model )`, `VAR_RIDECLOSED` = 0, SetState(0),
which for 0 is the store alone; the nominee is left. It asks the guard first and opens whatever the answer, logging
`"Opening non-openable ride!"` five times when it refuses (`0x004df3ea`). **The guard `FUN_004df290`** refuses state
1, state 4, `mRequestedService` non-zero, state 2, the back of the queue not connected, and a type-3 track whose
`FUN_00441970` answers nought, in that order. Its callers that ask it first: the park's door (`0x0051a013`) and the
four in the build dispatcher `FUN_00524960`; those that do not: the ride window's door (`FUN_0048ccf0(0)`, `0x0048cd06`),
the track editor on leaving (`0x00447748`) and `FUN_004d73c0`. The ride window greys its door for a closed ride the
guard refuses (`FUN_004ad4e0`, `0x004ad5c6`..`0x004ad5e2`), and sets its position from `mCanLoad`.

**`FUN_004de4a0`, the back of the queue connected.** With the queue-path bit (`+0x32 & 8`) it takes the back cell
from `FUN_004de130` (`mBackOfQueue` as it stands, walked only when nought); nought is not connected, and otherwise it
answers `mNeighbours != mDirection` for that cell (`0x004de4da`..`0x004de4f1`), so a back cell linked on to anything
beyond the cell ahead. Without the bit it steps from the entry cell: the angle names a side (0 as `0x10`, 90 as
`0x04`, 180 as `0x01`, 270 as `0x40`, `0x004de510`..`0x004de53f`; any other angle reads an unset byte), and the cell
one step the opposite way must be path (`FUN_00536310`) whose `mNeighbours` has that side, with the entry cell's
`mNeighbours` holding the opposite. In the shipped park all six visitable objects answer connected.

**The park's door - `FUN_00519ef0( open, 0 )`.** The first argument is the OPEN flag. A non-zero second argument
takes a third path that only writes the gate's `VAR_COMMAND` = 2 and returns (`0x00519f17`..`0x00519f66`); its one
caller is the end-of-park routine `FUN_005168f0` (`0x00516ada`).

| Arm | Runs when | In order |
|---|---|---|
| open, first argument non-zero (`0x00519f76`) | the park is closed | `mParkClosed` = 0; the gate's `VAR_COMMAND` = 1; along `mFirstObject`, every object with `+0x32 & 4` that `FUN_004df290` allows is opened (`FUN_004df390`) |
| close, first argument nought (`0x0051a091`) | the park is open | `mParkClosed` = 1; if `FUN_004c9130` counts nobody (things of kind 1 on cells of type 0, 1, 3, 9 or 10) and the gate's `VAR_STATUS` reads 1, the gate's `VAR_COMMAND` = 0 (`0x0051a0e8`..`0x0051a161`); along `mFirstObject`, every object with `+0x32 & 4` is closed (`FUN_004df300`) |

Both arms then post a type-`0x13` message, 3 on open and 4 on close (`0x0051a031`, `0x0051a1c1`), whether or not
anything changed; `CAdvisor::ReceiveMessage` (`FUN_0059b060`, `0x0059b1f8`) answers it with its own message `0x80`
on open and `0x81` on close (`FUN_0059ae20`). The log `"*** You have just %s your park ***"` is a `RET` stub. The
entry-price screen's `b_door` calls it as `FUN_00519ef0( down != 1, 0 )` (`0x00498d33`), where the fourth handler
argument is the switch's down byte after the click (`FUN_00668abd`, posted at `0x006691e1`); the screen's builder
sets the switch down for a closed park (`Button_SetDown`, `0x00498fd9`). **Down is closed.** `FUN_00516700`, the
other opening caller (`0x00516724`), is the online-chat park-creation path.

**What a closed ride changes.** The offer gate `FUN_004dd920` refuses it, so no guest chooses it, the minor decision
`FUN_004fd570` does not switch to it, and it drops out of `FUN_004c8240`'s sum behind the arrival headcount and the
park's excitement against its ticket price. `AdmitPerson` refuses. The breakdown request is skipped. The windows show
status code 1, `CLOSED` (UITEXT 365, grey), or `0x17`, `CLOSED: QUEUE NOT CONNECTED` (`FUN_00485f60`), and the
allitems list colours its row; five advisor scores count closed objects (`RidesClosed` .. `StaffroomClosed`); the 2D
map draws its cells differently. A guest already walking to it still joins its queue (`FUN_004ffbc0` does not ask).
**The engine never reads `VAR_RIDECLOSED` back; the scripts do**: 28 of Lost Kingdom's 73 read it. `Bouncy.RSE`
stops admitting (`FLUSHANIM`, `VAR_RUNNING` = 0, `TRIGANIM 2,0,0`) and loops `FORCEUNBOUNCE` into `VAR_LETMEOFF`
until it reads nought; `Coconut.RSE` runs `TRIGANIM 5,0,0` and `KILLOBJ 1`; `Junspray.RSE` and `Toilet.RSE` never
touch it.

**OpenTPW builds** the door's two arms (`ParkState.SetParkClosed` → `ParkPeople.DoorMoved`), the close, the open,
the guard and `FUN_004de4a0` (`ParkRideOperation.Close`, `Open`, `MayOpen`, `BackOfQueueConnected`), the completion,
the reopen, the queue-path bit on a bought thing (`ParkBuilding.FlagsFor`) and the ride window's door switch following
`mCanLoad`. **Counted:** the gate's command (`PARK_DOOR_COMMANDS_THE_GATE`), the `0x13` message
(`PARK_CLOSED_ADVISOR_MESSAGE`, `PARK_OPENED_ADVISOR_MESSAGE`), the model changes (`CLOSED_RIDE_MODEL_CHANGE`,
`OPENED_RIDE_MODEL_CHANGE`), the coaster's closed circuit, which the guard lets through where the choice refuses
without it (`OPEN_GUARD_COASTER_TRACK_RECORD`; `ParkRideChoice.CircuitClosed`), the constructor's close (`BOUGHT_QUEUED_THING_STARTS_CLOSED`), the ride window's
status box and greyed door (`RIDE_WINDOW_CLOSED_STATUS`, `RIDE_WINDOW_DOOR_GREYED`) and the all-items row colour
(`ALL_ITEMS_CLOSED_ROW_COLOUR`). **Not built:** the ride window's door as a button
(`RIDE_WINDOW_OPEN_OR_CLOSE_THE_RIDE`), the blocked exit (deliberately, `ParkRideOperation.Dismiss`), the maintenance
and track-editor closes, the advisor scores, and the second completion on a breakdown turn.

#### The `InQueue` turn - `FUN_004ffff0`

Verified 2026-09-24 (`docs/QUEUE.md` Q50d): five claim groups, each read from the disassembly and put to a skeptic;
all held. `ESI` is the guest and `EDI` the object, reloaded from the thing table by `MajorDest` before every jump to
the one tail (`0x0050049e` / `0x005004aa`): `FUN_004ddd20`, then `FUN_005012f0` (which sets state 6 itself,
`0x00501385`), then state 6 again. Every log and assert on the way is the bare `RET` `FUN_005da3c0`. In code order:

1. **Board**: `mQueuePos` (a byte) nought, `mBeenAdmitted` non-zero and `FUN_004e0aa0` (the object's word `+0x6c` is
   this guest): `mBeenAdmitted` cleared, route to the stand point (`FUN_004dedf0(0)`, then `FUN_004fa5f0`), state 13.
   **1b**, no route: `"the player has removed the path from under me"` (`0x0050010a`), `FUN_004e0ac0`, out.
2. **Wait**: the same two with the object naming somebody else: the whole turn is nothing (`0x005001d8`). The
   invitation is kept, and nothing below runs, not even the mood.
3. **Dirt gate**: `FUN_004e0390`, a toilet (`+0x32 & 1`) whose `+0x44` (its State of repair, `park-engine.md`; what lowers it
   is not decoded) truncates to a
   byte below 25.0 (`0x00700550`): thought `0xe`, out.
4. **Lost place**: `FUN_004ddf50` answers -1 (the guest is unlinked, or somebody in front is no longer in states
   11..14): out. Its string, `"Problem with a queue - shouldn't be fatal, closing and reopening the ride with the
   problem!"` (`0x0075dbac`), promises a close and reopen that nothing does.
5. Place equal to `mQueuePos` (`0x0050059d`): **5a capacity** - `FUN_004dda40` below `mQueuePos`, unsigned: thought
   `0x10`, event `0x15`, out; **5b track gate** - item track type 3 with `FUN_00441970` nought, or track type 1 with
   `mIsTrackRideValid` (`+0x2c`) nought: thought `0xd`, out. Otherwise the mood below.
6. **Drift**: delay (`+0x1f4`) non-zero and `mQueuePos - place` at most 2 (a 32-bit unsigned compare of the
   zero-extended byte, `0x005002c2`..`0x005002e5`, so a guest ahead of their true place, `mQueuePos` below it, always
   re-takes): spend one.
   Otherwise, unless the object is broken down (`FUN_004e0370`, its `mState` `+0x19c` is 1; 2 and 4 re-take),
   re-take through `FUN_00501160` (`0x00500532`), which writes `+0x1f1` before routing and sets state 12; if it
   fails, `"Couldn't get to my intended queue position"`, out. After a re-take, a spent delay or arm 5, the mood below
   runs on the same turn, whatever the state.
7. **Mood**, when `mGameTick - mTimeOfLastSpotAnim` (`+0x208`) exceeds 30 unsigned (`0x00500308`). Happiness
   (`+0x19c`) is truncated to a byte: 81 and up plays spot animation 5 (`FUN_004fc800`) and returns; 20..80 reads the
   toilet need (`+0x1ac`, the same byte truncation), and above 80 thinks thought 4 and goes out unless the thing is a
   toilet (`+0x32 & 1`), at or below 80 returns; 10..19 plays spot animation 4; below 10 thinks thought `0xb`, out.
8. **Window** (30 or less): `mGameTick` at most `mTimeStartedIdling` (`+0x1fc`) + 100 turns the heading (`+0x1c`)
   one turn in ten by `rand % 800 - 400`, wrapped into 0..`0x7ff`; beyond it, **boredom** (`0x00500432`, event 7,
   thought `0xc`, out).

**Boredom never fires in the original's own play.** `+0x208` is written only by the constructor (0) and at a spot
animation's start (`0x004fc871`, `0x0050237b`); state 8 returns only after `mGameTick > +0x208 + 10`, and returning
to 11 stamps `mTimeStartedIdling` (`0x00501eb7`), so in state 11 it is at least 11 past `+0x208`. The window wants
`mGameTick <= +0x208 + 30` and boredom `mGameTick > +0x208 + 111`. Only a save holding a state-11 guest with the
two stamps 70 apart could reach it.

**The longest queue - `FUN_004dda40`.** Verified 2026-09-28 (`docs/QUEUE.md` Q173): three claim groups, each read from
the disassembly and put to a skeptic; all held, three sharpened. It returns 100 for a thing without the queue-path bit
(`+0x32 & 8`, `0x004dda4c`). With it, `trunc( max( ((cap × q) × R) / dur, 4.0 ) )`, in that order
(`0x004ddb26`..`0x004ddb38`):

- `cap` and `dur` are the bytes `+0x5d` (`mOperatingCapacity`) and `+0x5c` (`mOperatingDuration`), loaded as integers.
- `q` is the float at descriptor `+0x1b4 + 0x40 × l` (`0x004ddac9`), `l` the byte `+0x50` (`mUpgradeLevel`), never
  bounds-checked: `Upgrades[l].QueueWaitTimeConstant`, a float leaf (type 7) of the compiled `.sam` schema (entry
  `0x007465e8`, name `0x007465ec`, between `WearRate` `+0x1b0` and `CostOfUpgrade` `+0x1b8`). The loader converts it
  with `atof` and stores a float (`0x00401e22`); an item's own file overrides its category's key by key, and a key
  neither states reads nought. The descriptor comes from `FUN_00412e90( 0x7890a0, word +0xe )`, which answers nought
  for an unknown item; nothing checks it.
- `R` is the dword `+0x58` (`mOperatingSpeed`) zero-extended (`FILD` qword) over the signed `InitSpeed` at
  `+0x1a8 + 0x40 × l` (`FIDIV`), **stored as a float** at `0x007cdc54` (`0x004dda95`, read only at `0x004ddb32`), or
  `1.0f` when the speed is nought (`0x004dda9d`).
- The floor is `FCOM 4.0f` then `TEST AH,0x41` (`0x004ddb3c`): a value at or below 4, or not a number, becomes 4.
- The truncation is `__ftol` (`0x0067a830`): chop, `FISTP` qword, the low dword. An infinite value, or one of 2^63 or
  more, stores the integer indefinite, whose low dword is nought.

Its two asserts, `q` above nought (`"No queue constant entered in SAM file for object num %d ..."`, `0x0075b714`) and
the duration non-zero (`"Operating duration set to zero - shouldn't have a zero duration for a ride!"`, `0x0075b6c8`),
go to the bare `RET` at `0x005da3c0`, so nothing is refused, and nothing checks `InitSpeed`. A tier stating no
constant holds 4. A duration of nought, or an `InitSpeed` of nought under a speed, is infinite and holds **nought**,
unless `cap × q` is nought: then it is not a number and holds 4. No shipped ride sets a duration or `InitSpeed` of
nought.

Exactly two callers read it (`0x004ddb75`, `0x0050059f`; no other reference, jump or pointer in the image), both
unsigned: the arrival refuses at a count **at or past** it, the `InQueue` turn puts out a guest whose place is
**past** it, so a queue settles at one more under the turn than the arrival lets in. `FUN_004ddf50( 0 )` never answers
-1, so the unsigned compare matters only in principle. It runs in the thing sweep, a sibling of the frame renderer
and never inside it, and nothing on its path writes the precision control: it sees what the renderer's last exit left
(`park-engine.md`, "Which rounding is live"). Over every slider setting of every shipped ride with a queue (6,769,800),
the answer at 53 bits and at 64 bits is the exact one; at 24 bits 83,454 of them (1.2%) answer one more, each with a
speed off the tier's, where the float `R` is inexact and the value lands just under a whole number.

In Lost Kingdom all 17 rides with a queue state their own three constants (FileFormats `sam.md`), so `Rides.sam`'s 30,
35 and 40 reach none. The Belly Bounce as saved - capacity 5, duration 30, speed 60, tier nought, constant 130 - holds
**21**; the room gate (`FUN_004dda20`, 16 over its four cells) is asked first, so at its shipped settings neither of
its callers can refuse or put anyone out. A capacity of 3 (13) or less, a duration of 41 (15) or more, or a speed of
44 (15) or less brings it under 16, where the arrival refuses before the room gate does; the turn's arm 5a, which needs
a place past it after a slider has moved, fires at 14 or less (a duration of 44, a speed of 41).

**The clock and the stamps.** `mGameTick` (`[0x0080239c] + 0x1da70c`, named by the world serialiser) goes up by one
at the start of each thing sweep (`0x00516394`), which runs on game ticks whose low three bits are nought and at most
three times a rendered frame; it is zeroed at level start (`0x00515865`) and loaded from a save (`0x00517bec`).
`FUN_004fc800(n)` plays animation `n`, for `n` 4 plays sound `0x7e` for a guest whose id's low nibble is nought,
stamps `+0x208`, copies the state into `mSavedState` (`+0x224`) and enters state 8, which `FUN_00502430` looks
through. The guest constructor `FUN_004faec0` zeroes `+0x208` and `+0x1fc` and sets happiness to **50.0**
(`0x004fb075`). The serialiser tags every need float `pv` (`0x0075b444`); happiness and toilet are known by the debug
strings that print them (`0x004fda74`, `0x004fd10e`) and by the needs tick, not by a name in the game.

**OpenTPW builds** the turn as `PeepBehaviour.QueueTurn`, with `ParkRideOperation.LeaveQueue` as the tail's
`FUN_004ddd20` and `DismissFromTheQueue` as `FUN_005012f0`: arms 1, 2 and 4, 5a on `PeepBehaviour.LongestQueue`,
5b for a car track, the re-take (`FindQueueDestination`, and out when it fails), the broken ride's skipped re-take, and
the toilet. `ParkState.LeaveQueue` splices by the leaver's own links, so a leaver with nobody in front
writes their own next as the head (`0x004ddde9`): an unlinked one empties it and the rest of that queue is lost to the
walk in turn. **Counted:** the no-route board (`QUEUE_BOARD_NO_ROUTE`: ours routes to the entry cell's centre, the original to
the stand point on the same cell, and `FUN_004fa5f0` also fails without routing on `mStrandedTime` at `+0x198`,
`0x004fa62a`, that nothing here keeps: nought on the queue paths but from a save), the dirt gate (`QUEUE_TOILET_DIRT_GATE`), the
coaster's record (`QUEUE_TURN_COASTER_TRACK_RECORD`, let through), the
thoughts, the spot animations (`QUEUE_SPOT_ANIMATION`), the heading (`QUEUE_TURN_HEADING`) and boredom
(`QUEUE_TURN_BOREDOM`). **The unhappy arm is held** (`QUEUE_TURN_UNHAPPY`) until Q85: an arriving guest here starts at
happiness nought, not the constructor's 50, and the arm would put every arrival out of every queue. 5b's built half is
dead by content: the shipped park places nothing tracked, nothing here sets `mIsTrackRideValid`, and the choice
sends nobody to a car track without it, so only a save holding a queue for an invalid Dino Karts (item 1150, the
jungle's one car track) reaches it. With spot animations unbuilt `TimeOfLastSpotAnim` stays at nought, and the thing
tick is `GameClock.Ticks` over eight, which is not reset on entering a park: a queuer's mood is read on every turn and
the window is not reached once the lobby has run about seven seconds.

#### At the door - `FUN_005006b0` and `FUN_004fde50`

**`FUN_004fde50` is asked only here** (`0x00500715`, its one caller), of a thing with a price (nought answers nought,
`0x004fde6a`). With the guest's meters truncated to bytes: `mood = 100 + d150 × (100 − happy)/100 + d144 × thirst/100
+ d148 × hunger/100 − d14c × illness/100`; `w1 = mood × (d140 × 115/100) / 100`; `worth = ((prize × win/100 + w1) ×
(r + 100)/100) × (happy + 100)/100`, where `d` is the item descriptor, `prize` is the object's `+0x188` for a
sideshow (`+0x4ac` == 2) and nought otherwise, `win` the byte at `+0x190`, and `r` the control record's `+0xc`,
copied at level start from descriptor `+0x16c` (`0x004d3e7b`) - **`UsageInfo.RipOffOK`** by the compiled schema's
order (`0x007460c0`; `Shops.sam` 100, `SideShow.sam` 250). Too expensive is `price > worth` or `cash < price`, both
unsigned (`0x004fe15f`, `0x004fe167`), so a guest whose cash has gone below nought passes the cash test. At the shipped
prices the Drinks Shop's worth runs 42..128 against 30 and the Jungle Spray's 241..482 against 20, so in Lost Kingdom
only the cash test can fire.

**The arithmetic, exactly.** Each meter is `__ftol` (`0x0067a830`) then `AND 0xff`; each of the four meter products
is divided by 100 on its own and **signed** (`IMUL`, `SAR 5`, the sign bit added back), as are `d140 × 115 / 100` and
`prize × win / 100` - six in all. The three divisions of a running product are **unsigned** (`MUL`, `SHR 5` at
`0x004fdf6d`, `0x004fdfe7`, `0x004fe005`), and every product wraps at 32 bits (`IMUL`, or `LEA`/`SHL` for the 115).
`d140` is `UsageInfo.InitCostOfGoods`.

**The offsets are pinned by the compiled `.sam` schema**, a table of 0x3c-byte entries (a type dword, then the
name): UsageInfo runs `InitCostOfGoods`, `ThirstEffect`, `HungerEffect`, `VomitEffect`, `HappinessEffect`,
`LitterEffect`, `SpecialIngredient`, `AppearanceEffect`, `InitPrizeValue`, `ShopType`, `ExciteFactor`, `RipOffOK`,
`NumSimultAnims` (`0x00745e2c`..`0x007460fc`), four bytes each from `+0x140`, so `RipOffOK` is `+0x16c` - and the two
ends are anchored independently: `+0x144` is the thirst `FUN_004fe1e0` takes away, `+0x170` the channel count
`FUN_00413c10` hands the model loader. The control record is built once per item (`FUN_004d3e00`: `+4` from
`+0x1b8`, `+8` from `+0xe8`, `+0xc` from `+0x16c`), nothing else stores to its `+0xc`, and it is saved and loaded
raw as `mObjectControls[150]` (`FUN_004d3aa0`). In Lost Kingdom's save all 50 records' `+0xc` equal their items'
`RipOffOK` (100 for the six shops, 250 for the four sideshows, 0 for the rest), so reading the item gives the
number the loaded park holds.

**Before the verdict it pushes price samples** to the park analyser (`FUN_00519510`, then `FUN_004c74b0`, a byte
ring per kind), each `(worth + 5) × 10 − price × 10` kept when below 100 unsigned and 100 otherwise
(`0x004fe037`), so a price more than five over the worth records 100 as well: for a sideshow (`+0x4ac` 2) one, kind 9; for `+0x4ac` 1 one for
`SpecialIngredient` 1..4 (kind 1..4, through the jump table at `0x004fe184`) and one for `AppearanceEffect` 1 or 2
(kind 6 or 7). The Drinks Shop's ingredient is 3, so its door pushes one.

**The walk-away itself** (`0x00500722`..`0x005007c6`): thought 6 (`FUN_0050be80`, which also spawns a thought-bubble
sprite), event 10, `FUN_004fea70(1)` (`MediumHappinessChange`, clamped 0..100), `FUN_004e1670` (object `+0x1a4`
`mNumWalkAways` and `+0x230`, a history record's accumulator, each +1), `FUN_004e0ac0` (the nominee `+0x6c` zeroed
unconditionally and `VAR_LETMEON` emptied if it names the guest; its only assert is that the guest was the nominee),
`FUN_004ddd20`, `FUN_005012f0`, then `MajorDest` 0 and state 6 a second time. Each dock clamps on its own, so the loss
is 30 only from happiness 30 up.

OpenTPW builds it: `PeepPriceOpinion` is the opinion, `PeepBehaviour.WalkAwayFromTheDoor` the walk-away and
`ParkRideOperation.Forget` is `FUN_004e0ac0`; `FUN_004e1670`'s two counters are `ParkObjectRings.CountWalkAway`.
Thought 6 and the samples are counted (`DOOR_PRICE_THOUGHT_6`, `DOOR_PRICE_ANALYSER_SAMPLE`); the event ring is not
kept.

**`AdmitPerson` refuses** on `mState` 1 or 4 or `mCanLoad` nought, or on `VAR_LETMEON` full after it has zeroed the
nominee (`0x004e09b0`); a wrong person is only logged. Since `Invite` calls forward only while the slot is empty and
nothing on the way refills it, the realistic refusal is a ride that closed or broke while the guest walked.

## Deciding and wandering - `FUN_004fec90` and `FUN_004f9490`

Decoded 2026-09-25 (`docs/QUEUE.md` Q53): five decoders (the no-links arm, the linked arm, the stranded bookkeeping, the
state-6 turn, the ground after a sale), each report put to a skeptic reading the disassembly, then a critic over all
five - 173 claims, 144 upheld, 28 amended, 1 refuted (about a run's output file, not the executable). **A guest put off
onto cells a sale cleared is not stranded in the original**: SetRandomDest has an arm for a cell with no links, and it
sends them to the nearest path. OpenTPW builds it for guests (Q53b).

### The state-6 turn, in order

`FUN_004fec90`, called only from `FUN_005019f0` case 6, once per thing sweep. One draw r at the top (`0x004fecb4`, kept
at `[ESP+0x18]`) serves every arm. A byte below is `(u8)__ftol` of the float named.

1. **(a) Happy.** More than 100 sweeps since `+0x208` and happiness (`+0x19c`) above 80: spot animation 5, return.
   `FUN_004fc800( n )` sets `+0x10` = n, `+0x208` = mGameTick, saves the state at `+0x224` and sets state 8, whose turn
   restores it once mGameTick > `+0x208` + 10.
2. **(b) Vomit.** Illness (`+0x1b0`) exactly 100 and r % 3 nought: animation 7, litter type 7 on the guest's cell, event
   `0x12`, sound `0xcc`, `+0x1b0` = 0, return.
3. **(c) Litter.** Litter (`+0x1b4`) 90 or more: `FUN_00500dc0` aims at the nearest `HoldsLitter` thing (`+0x32 & 0x40`)
   within squared distance under 9 that routes, at its `mEntryPos` - `MajorDest` and state 9, return; none: litter of
   type 1 + (a fresh draw % 5), `+0x1b4` = 0, on.
4. **(d) Leaving** (`0x004fee5b`..`0x004fee81`): the happiness byte nought, **or `mExitLevel` (`+0x1bc`) exactly
   nought**, or the park shut (`FUN_0051a280`, world `+0x1da710`). It docks `BigHappinessChange` (25,
   `FUN_004fea70( 2 )`) before routing, every turn the test holds; reads the gate script's variable 1 and discards it
   (`FUN_0051a290`); then `FUN_004fa530` to `FUN_004d86d0( 0 )` and `( 1 )`, **`CrossingParkSide` A and (B's x, A's y),
   (47,9) and (48,9) in Lost Kingdom - not the bus stops**, which are `FUN_004d8650` - in the order r's bit 0 picks,
   each with `+0x188` as it stands, then both again with `+0x188` = 1. The first route sets state `0x12` and returns;
   four failures write `+0x188` = 0 and fall through, still in state 6. `mExitLevel` starts at `ExitLevel` + a draw mod
   (2 × `ExitLevelVar`) − `ExitLevelVar` (60..179, `0x004fafe1`..`0x004fb00d`) and loses one on the sweeps where
   `(mGameTick & 3) == (id & 3)` (`0x00501676`..`0x00501697`), unclamped, so **it reads nought for four sweeps**; a
   guest not in state 6 then never leaves for it. Only states 3 and 4 write it nought again, each just after
   SetState(`0x12`).
5. **(e) Watching.** The nearest `IsFireworks` thing (`+0x32 & 0x80`) within squared distance 4 whose script variable 0
   is nought (`FUN_00501020`): face it (`+0x1c`), return - dead by content in Lost Kingdom, whose items set no
   `IsFireworks` ("The first half of the turn"). Else an entertainer on the 3×3 around the guest (`FUN_004c8eb0`) and
   the nearest one's staff state `0xe`: event `0xe`, face them, return. Both skip the facing when dx + dy is nought.
6. **(f) Pranks.** Happiness below 15 and r % 101 below `mPrankeryIndex` (`+0x1c0`: 100 + id % 3 for a prankster, else
   0): a stink bomb, litter, or another guest's balloon on the same cell; event `0xf`, a type-`0xe` bus message,
   happiness +1. It does not return.
7. **The split** (`0x004ff3b4`), r % 3. **0**, only when mGameTick > `+0x1fc` + 30, unsigned: the chooser
   `FUN_004fcb10`; a route gives event 2 and state 10; none gives event 1, spot animation 4 (sound `0x7e` when id &
   `0xf` is nought), `FUN_004fea70( 0 )` (−5, `SmallHappinessChange`) and `+0x1fc` = mGameTick
   (`0x004ff480`..`0x004ff4a3`). **1**: SetRandomDest; non-zero sets state 7 and returns **without a restamp**
   (`0x004ff3d6`), nought restamps `+0x1fc` (`0x004ff3f4`..`0x004ff400`). **2**: nothing.

SetState(6) writes the state and `+0x10` = 3; SetState(7) first routes again to `+0x18`/`+0x1a` with `FUN_004fa5f0` when
`mSetDestSuccessfully` (`+0xd0`) is set - every successful SetRandomDest sets it, only the constructor and a load clear
it - then `+0x10` = 1. Neither stamps `+0x1fc`. **The state-7 turn** (`FUN_005019f0` case 7, `0x00501abd`..`0x00501afe`)
runs the walk tick; on arriving (0) or failing (2) it draws: r & 3 non-zero, state 6; else SetRandomDest, state 7 on 1
and 6 on 0.

### SetRandomDest - `FUN_004f9490`

Thiscall on the person, in order:

1. **The stranded refusal** (`0x004f94e2`..`0x004f9527`): the counter goes up (`FUN_004d8c50`); `+0x198` is zeroed when
   the counter is below it; still non-zero with `FUN_004fa770` answering 1, thought `0x11` and answer 0, with no draw.
   Otherwise `+0x198` = 0 (`0x004f9528`).
2. One draw, r % 5 + 1.
3. **Staff** (type byte 4..8) outside their patrol rectangle (`FUN_00506ed0`, the cell ids at `+0x20a`/`+0x20c`):
   `FUN_00506f30`, 30 tries at a random path cell inside it, answers 1 on a route; its failure sets a flag the linked
   arm reads.
4. **The count** `FUN_00522810` of the person's OWN cell: how many of the cardinal bits `0x01`, `0x04`, `0x10`, `0x40`
   of `mNeighbours` are set (`+0x22` under the hoarding overlay, which only edit code raises). **Nought takes the
   no-links arm** (`0x004f95c0`, `JLE 0x004f9a05`), whatever the person.

**The linked arm** (`0x004f95c6`..`0x004f9a04`) walks r % 5 + 1 passes (a post-decrement at `0x004f9907`). Each pass
stands on one cell (the person's, then the last chosen) and makes four slots from **that cell's own mask**, `& 0x55`, in
the order `0x10` (0,+1), `0x04` (+1,0), `0x01` (0,−1), `0x40` (−1,0), each along its own bit's vector; nothing reads a
candidate's mask before it is chosen. The filters, each lowering the count: for staff who began inside their rectangle,
a slot outside it; **on a path cell, a queue or entrance neighbour** (`0x004f9733`..`0x004f9774`); on a type-3 cell, the
slot its `mDirection` names, the count lowered even when that slot was already empty (`0x004f977b`..`0x004f97e3`); a
type-10 (exit) neighbour. Then **a count below 2** takes the first non-empty slot in that order, a step back allowed;
**two or more** start at r & 3 and take the first non-empty slot that is not the reverse of the last step (none on the
first pass); **none left, on any pass**, is the dead end below. A last pass that would end on the person's own cell gets
one more. So **a wander ends 1 to 5 linked cells away, never on the person's own cell**, at a point inside the last one:
sub-bytes `r & 0x7f` clamped to 5..`0x7b`, x then y (`0x004f991c`..`0x004f997f`), through an inline SetDest; a route
sets `+0xd0` = 1 and answers 1, a failure answers 0 and stamps nothing.

**The dead end** (`0x004f9861` to `0x004f9d64`): "Peep can't SetRandomDest anywhere"; a guest (type byte not 4..8) gets
"Serious person navigation problem", thought `0x11`, **`+0x198` = the counter** (`0x004f9e09`) and "Peep %d: stranded at
time %d", and the answer 0, the walk so far dropped; staff take `FUN_00506f30` instead. Every log here is
`FUN_005da3c0`, a bare `RET` in this build. `0x004f9e09` is the only live non-zero write to `mStrandedTime`:
`FUN_004fa670` stamps with a non-zero argument, but both its callers pass 0, so that arm is dead by code.

**The no-links arm** (`0x004f9a05`..`0x004f9d5f`) reads no person type. It probes direction d = 0..7 outside and
distance k = 0..3 inside, through the table at `0x004f9e40`:

| d | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|---|
| (dx, dy) | (0, 0) | (k, k) | (k, 0) | (k, −k) | (0, −k) | (−k, −k) | (−k, 0) | (−k, k) |

so every k = 0 and all of d 0 probe the person's own cell - case 0 (`0x004f9a45`) leaves both offsets nought - and
**nothing probes due south**, (0, +k). The id is the own id + dy × 128 + dx in 16 bits, so an x past column 0 or 127
wraps into the next row. A probe **hits** on the map (`FUN_004d8300`) and on path (`FUN_00536310`, type 1); a hit aims
at the cell's centre (`0x80`, `0x80`) and routes, and **a failed route moves on to the next probe** (`0x004f9b98`). A
route sets `+0xd0` = 1 and answers 1. After 32 probes, five tries at own + (r % 11 − 5, r % 11 − 5), x first: on the
map, **any type**, the centre; an off-map draw uses a try. Five failures answer 0, with **no stamp, no thought and no
log**.

### The stranded bookkeeping

- **The counter** `[0x007cdb98]`: `FUN_004d8c50` adds one and answers it. It starts at 0 and is never reset or saved; 14
  sites share it - every SetDest, SetRandomDest's entry and each of its routes, a route found (walker `+0x48`), every
  map type write, the queue re-measure, and the per-frame person update `FUN_004fa030`. It is a logical clock, not a
  count of routes.
- **The block stamps**: a 33 × 33 dword array at world `+0x1b02d8`, one per 16 × 16 cells, `(y >> 4) × 33 + (x >> 4)`. A
  map type write (`FUN_005346d0`, ClearCell's tail among its callers; `FUN_005348d0`; `FUN_00538fc0`) and the queue
  re-measure's back cell (`FUN_004de1f0`) write a fresh counter value there (`FUN_004d8c60`). Zeroed at map init and
  after a load; not saved. The path follower reads them too: a stamp newer than walker `+0x48` re-plans (`FUN_0050ed10`,
  `0x0050ed79`).
- **`FUN_004fa770`** answers 1, still stranded, when every stamp of the 3 × 3 cells around a base is below `+0x198`. The
  base is the person's cell, or on a queue or entrance cell the far end of the queue run (`FUN_004de670`, unbounded).
- **The refusals.** SetRandomDest's entry (with thought `0x11`), `FUN_004fa530` and `FUN_004fa5f0` answer 0 without
  routing or writing a destination. So a stranded guest in state 6 can be chosen no ride, cannot leave and cannot
  wander; state 6 never runs the walk tick, whose zeroing (`0x004fa30b`) would free them. **It ends** when a stamp at or
  above `+0x198` lands in one of the nine cells' blocks - a path or queue cell laid or cleared in that 16 × 16 block -
  or, after a load, when the counter is below the saved value (`FUN_004fa030` clears it then).
- **Thought `0x11`** is `FUN_0050be80`, SetThought (its own "Not a known thought!"), on `+0x30`: the thought stored, the
  old bubble freed, and sprite script `0x0074f2f8` (set 15, frame 0, looping) of kind 9, which the name table calls
  "thoughts", spawned again on every call; `FUN_0050be40` takes it away 13 to 16 sweeps later. It changes no happiness.
  While `+0x198` is non-zero `FUN_004fa030` also queues a blinking square under the person. Which picture set 15 shows
  is not established.

### A queuer put off onto cleared ground

After the Belly Bounce's sale (49..52, 22) are type 0 with no links - ClearCell's tail under force,
`FUN_00522730( 0xff )` at `0x00536bd0` and `FUN_005346d0( 0 )` at `0x00536bf7` - and so is the entrance (52,23) after
the footprint pass. A put-off guest's `+0x198` is nought, zeroed by every walk tick. So on their first state-6 turn with
r % 3 = 1, SetRandomDest counts no links and takes the no-links arm. Lost Kingdom, read with `cell` after a sale
(`q53measure.py`): y 21 is path from x 46 to 55 (mask `0x44`), (47..48, 19..25) are path, and the rest of x 46..55, y
19..25 is type 0. d 0, d 1 (x+1..x+3, 23..25) and d 2 (x+1..x+3, 22) miss; **probe 14, d 3 k 1, is (x + 1, 21)**, path.
The line stepper steps E then N (seed 0 below |dx|, `0x005114ef`): grass to grass, open unless (x + 1, 22)'s track
record shuts it, then grass to path, open before any other test (`0x004d8806`). If the E step is shut, d 4 k 1, (x, 21),
is one N step. **Either way the guest leaves as Wandering, in about three sweeps**, with nothing stamped; the W ray to
(48,22) is never reached. From (52,23) the first hit is d 3 k 2, (54,21), unless (55,26), unread, is path.

**OpenTPW takes the arm** for a guest (Q53b): `PeepBehaviour.SetRandomDest` takes the call's r % 5 + 1 draw, counts the
own cell's links with `CellEdge.Links` and at none calls `WanderFromNowhere`, which walks `NoLinksProbes` (the table,
its quirks and the sixteen-bit wrap) and then the five tries. OpenTPW's route planner answers where the line stepper
does here. A park never loaded is taken as linked. Measured over two runs (Q53b): of 13 put-off guests on cleared cells,
the 5 whose first roll was the wander were aimed at exactly the predicted cell, (x + 1, 21) or (54,21); the other 8
rolled the chooser first and went to the Jungle Spray, its gate open because nothing restamps it between choosing a ride
and its sale.

### Where OpenTPW differs

| What | The original | OpenTPW | Reached in Lost Kingdom |
|---|---|---|---|
| A cell with no links, staff | the no-links arm, whoever asks: inside the patrol area, or outside it once `FUN_00506f30` fails (`0x004f95af`) | inside, the neighbour pick; outside, false; both counted `STAFF_NO_LINKS_WANDER` (Q112) | a guard or researcher put down off a path |
| A linked wander | 1 to 5 linked cells, aimed at the last | one adjacent cell | every wander (Q108) |
| Its candidates | the LEFT cell's mask, along each bit | the ENTERED cell's mask back, and the edge test | only on a one-way link or a shut track edge |
| Its filters | from path, no queue or entrance; a queue cell's `mDirection` slot; no exit | none | (48,22) onto the Belly Bounce's back cell; the toilets', Spray's and Drinks Shop's entrances |
| Its choice | count under 2: fixed order, back allowed; else a random start, no reverse | a random start, reverse allowed | every multi-step walk |
| The dead end and the stamp | thought `0x11`, `+0x198` stamped, every route refused until a map edit | false, nothing kept | not measured (Q110) |
| After a routed wander | state 7, no restamp | restamps `TimeStartedIdling` | yes (Q107) |
| Before choosing | restamps only when nothing is chosen | restamps first | yes (Q107) |
| Nothing chosen | event 1, spot animation 4, −5 | nothing | every failed choice (Q107) |
| Arms (a), (b), (c), (e) entertainer, (f) | run before the split | absent and uncounted | (a) above 80; (c) the Drinks Shop's litter and the bin at (44,29); (f) pranksters (Q111) |
| Leaving | state 6 only: happiness byte 0, `mExitLevel` exactly 0, or shut; −25 every turn it holds; (47,9)/(48,9); state `0x12` only on a route | `Step`: `ExitLevel <= 0` in any state a thing does not hold, no dock; `Decide`: shut only, −25; the bus stops; `HeadingForExit` whatever the route | yes: the measured run's three left this way (Q109) |
| `mSetDestSuccessfully` | SetState(7) routes again to the stored target | absent | every wander, invisibly |

## What a thing is worth to a guest - `FUN_004fcc30`

Decoded 2026-09-28 (`docs/QUEUE.md` Q165): read by hand whole, then five read-only decoders (the calendar, the balance
keys, the two histories, the item fields, and rain, excitement and the chooser), each claim at an address, and the
load-bearing ones re-read first-hand. The chooser (`FUN_004fcb10`, "Where a guest is aimed") keeps the highest score of
at least 10. Thiscall on the **guest** (`EDI`), the argument the **object** (`ESI`); in order:

1. **The same kind as the last visit scores nought** (`0x004fcd3f`..`0x004fcd97`): when `mPreviousRides[0]` (`+0x1e0`)
   is non-zero and the thing it names (table `0x7cfb90`, stride 20) has the candidate's item id (`+0xe`). A guest
   leaving the Belly Bounce cannot choose it, nor any other Belly Bounce, until they have left something else or it is
   removed. The function has already asked `GetBackOfQueue` three times by then (`0x004fcc49`..`0x004fcc7d`), which
   re-walks the queue and rewrites `+0x3a` and `+0x40` when `+0x3a` is nought.
2. **Distance**, at `GetBackOfQueue`'s cell (`FUN_004de110`): `d²` from the guest's cell (bytes `+5`, `+7`);
   `100 − min( 100, d² × 100 / 450 )`, divided (unsigned) by the word `+8` of `FUN_004d8410( cell )` when that is
   non-zero (its log: "nearby fireworks").
3. **Queue**, only when `d² <= 8` (`JG` at `0x004fce3e`): `100 − q × 100 / ( max( +0x40, 1 ) × 4 )`, unsigned, `q` =
   `FUN_004ddf50( 0 )`; farther away the term **and its weight** are nought.
4. **Excitement**: `100 − 2 × min( |pref − exc|, 50 )`. `pref` is `PeepTypes[+0x1f0].PreferredExcitement`, the byte at
   `0x7850e4 + 12 × type`; `exc` is `FUN_004e0860( object, 0 )`. The weight counts only when the low byte of
   `UsageInfo.ExcitementLevel` (`+0x13c`, `FUN_004e0860( object, 1 )`) is non-zero (`0x004fcf06`).
   `FUN_004e0860( object, 0 )`, of which the score takes the low byte: nought for an `ExcitementLevel` of nought; a
   sideshow (the descriptor's `+0x4ac` == 2) is `20 + trunc( 0.08 × mChanceOfWinning × √clamp( mCostOfGoods −
   mPricePerUse, 0, 100 ) )`, the **object's** byte `+0x190` and its `+0x188` and `+0x194`, which the constructor fills
   from 100 − `InitChanceOfLoosing`, `InitCostOfGoods` and `InitPricePerUse` (`0x004e058a`..`0x004e05d4`,
   `0x004db378`..`0x004db3ad`), so it follows the player's price; a coaster (track type `+0x9c` 3) is
   `trunc( 50 + f / 2 )`, `f` the coaster node's excitement rating `+0x104`, which `FUN_0043e0b0` answers for the handle
   `FUN_0055a4e0` finds from `+0x24`, or nought
   when that finds nothing (`0x004e05f8`..`0x004e0640`);
   anything else is `ExcitementLevel`, unclamped - or, with a track handle at `+0x28`, `clamp( E × 60 / 100 +
   clamp( 3 × crossings + the longest straight + 2 × bends, 0, 40 ), 0, 100 )` (`FUN_00545310`) - times
   `clamp( +0x58 / Upgrades[l].InitSpeed, 0.75, 1.25 )` times `clamp( +0x5c / Upgrades[l].InitDuration, 0.75,
   1.25 )`, truncated, `l` the upgrade level `+0x50`. The divisors
   are descriptor `+0x1a8` and `+0x1a0` plus `0x40 × l`, which the compiled `.sam` schema (`0x744b30`) names
   `InitSpeed` and `InitDuration`. The constructor copies tier nought's into `+0x58` and `+0x5c`, each only when
   above nought, the duration's low byte held to Min/MaxDuration (`park-engine.md`, "The object window's stats
   panel"), so a ride at its starting settings has both ratios 1 wherever its duration lies inside those bounds, as
   every jungle ride's does, and without a track handle scores its own `ExcitementLevel`. The placer gives a handle
   to any item whose `Bumper.BumperType` is set (`FUN_00529e10`, `0x00529e4d`), its slot with the BumperType
   above it, never nought, so a bought Hot Pot, Dino Karts or Splish Splash scores 60% of its level, 42, 48 and 45,
   until track is laid ("A coaster's, a track ride's and an upgraded ride's excitement", below).
5. **Thirst and hunger**: the table at `0x0075d0f8`, `[need / 10 + effect / 10 × 11]`, the need the guest's float
   (`+0x1a4`, `+0x1a8`) as a byte, the effect the low byte of `UsageInfo.ThirstEffect` (`+0x144`) or `HungerEffect`
   (`+0x148`).
6. **Toilet and illness**: nought unless the object's `+0x32` bit 0 is set (the constructor sets it from
   `UsageInfo.ProvidesRelief`, `0x004db3c0`), then the table at `0x0075d178`, `[( need + 4 ) / 5]`, the needs `+0x1ac`
   and `+0x1b0`. Their weights count in the mean either way.
7. **The mean**, unsigned: the sum of term × weight over the sum of all seven weights, of which only the queue's and
   the excitement's are ever nought (steps 3 and 4). The weights are
   `PeepInfo.DecisionVar{Dist,Queue,Excitement,Thirst,Hunger,Toilet,Illness}Weight` at `0x007850b0`..`0x007850c8`,
   1, 1, 1, 2, 2, 2, 2 (`data/levels/Standard.sam`; nothing in the jungle overrides them), all but the queue's read
   as 16 bits.
8. **New**: × `DecisionVariable2` (5) while `FUN_004dd670` is at most `DecisionVariable1` (7), **unsigned** (`JA` at
   `0x004fd24f`). `FUN_004dd670` is `( now − object +0x18 ) / 864,000,000,000`, a signed divide, where `now` is the
   **park calendar**, not the real clock: `FUN_004f8690` on world `+0x2a0`, `mFunnyTimeStart + mGameTick ×
   mFunnySecsPerRealSec / 4` seconds. The constructor `FUN_004db090` stamps `+0x18` with the same `now`
   (`0x004db66a`), on a purchase and on a move's put-down, so **a bought thing is new from its purchase sweep T through
   T + 184** (184 × 3750 s is under eight days, 185 × 3750 is not) - about 46 s. A stamp a day or more in the future
   reads as a huge unsigned age: not new.
9. **Rain**: × `DecisionVariable3` (5) when the weather thing's (world `+0x1da724`) `mCurrentDrops` (`+0x30`) is above
   nought and `UsageInfo.ISIndoors` (`+0x114`) is set.
10. **Golden ticket, or price** (`0x004fd2aa`..`0x004fd352`): when `UsageInfo.GoldenTicketCost` (`+0xc4`) is above
    nought, `trunc( ( 1.0 − ( g + 1 ) × −0.1 ) × s )`, × 1.1 + 0.1g; otherwise when `Upgrades[0].CostOfUpgrade`
    (`+0x1b8`, tier nought whatever the level) × `(float)1/3000` is above 1.0, `trunc( ( 1 − x × −0.1f ) × s )`,
    × 1 + cost / 30,000. The log calls them "GT-only ride" and "expensive ride". The truncation can hang on the FPU's
    precision: a golden-ticket cost of 3 (× 1.4) gives 63 or 62 for a score of 45.
11. **The last four visits** (`mPreviousRides`, `+0x1e0`..`+0x1e6`, thing ids, newest first): the FIRST slot naming the
    candidate divides by 5, 4, 3 or 2 - though slot nought's 5 never runs, as step 1 has already returned nought.
12. **The last four refusals** (`mPreviousTemporaryRides`, `+0x1e8`..`+0x1ee`): EVERY slot naming the candidate divides
    again, by 5, 4, 3 and 2 in turn.

**The two histories.** `mPreviousRides` has one writer, the settle-up `FUN_004fd970` (`0x004fd98b`..`0x004fd9a5`): the
three older ids move back and the thing left goes in front. Its one caller is `ExitRide` (`FUN_005014e0`,
`0x005015f4`), reached only when the cell off the exit is neither queue nor entrance (`FUN_00536320`, `0x005015e3`) and
the route to it succeeds (`0x005015ef`), before the charge - so **any thing visited**
counts, a shop, a sideshow or a toilet as much as a ride. `mPreviousTemporaryRides` is pushed the same way by
`FUN_004fdc60`: the id at the arrival's two refusals (excitement, `0x004ffce6`; queue too long, `0x004ffd74`), and **a
nought for every guest on each sweep where `mGameTick` % 20 is nought** (`FUN_004fdc90`, the last call of the
guest tick handler `FUN_00501650`, `0x005019da`, after its `(id & 3)` needs block, so on every sweep, and before the
step that holds the refusals), so a
refusal is forgotten 61 to 80 sweeps later. The constructor zeroes both; the thing-removed
message (`FUN_004fb360`, `0x004fb4ba`..`0x004fb4d9`) zeroes each `mPreviousRides` slot naming the thing and the
temporary slot at the same index, whatever that holds; both are saved, interleaved (FileFormats `saves.md`, 470).
`FUN_004dd670`, the age, has other readers too: the object window's Age and the arrivals' headcount score
(`FUN_004c8240`, `0x004c8391`).

**Measured in two played saves** (Alexah's jungle park, `mGameTick` 19,004 and 19,007; `q165cprobe`, read-only): of
772 pairs of consecutive visits in `mPreviousRides`, 111 are the same kind twice in the later save (109 in the other),
and **107 of those are toilets** (105) -
mostly one of three adjacent toilets and then another. The same-kind nought forbids exactly that choice: the walk makes
it, through the minor decision and the unscored restore ("A second toilet", below; Q170). The other
four are a sideshow, the Jungle Spray, Temple Of Gloom and the Aztec Mayhem, once each.

### A coaster's, a track ride's and an upgraded ride's excitement

`FUN_004e0560` after its sideshow arm (`docs/QUEUE.md` Q172). `FUN_004e0860( object, 0 )` hands it the object's
`+0x58`, byte `+0x5c` and byte `+0x5d` (`0x004e08d9`..`0x004e08eb`); the capacity is never read, its slot overwritten
with `E`, the descriptor's `+0x13c`, at `0x004e0680`. Besides step 4's three readers, the ride-list gauges
(`FUN_004955e0` at `0x004956f1` and `0x00495918`, `FUN_00493cd0` at `0x00493dc9`, `0x00494158`), the map overlay
(`FUN_005f2380`, `0x005f24eb`) and `FUN_004cc3b0` (`0x004cc44d`) read it; the object window's gauge (`0x004adfcc`) and
control `0xa099` (`FUN_004b16c0`, `0x004b1a4c`) call `FUN_004e0560` on their sliders. The chooser scores a candidate
(`0x004fcb8d`) before it asks the offer gate (`0x004fcbb4`), so a refused one's excitement is worked out and dropped.

**A coaster** (`+0x9c` == 3, `0x004e05f8`) is `trunc( 50 + f / 2 )`. `FUN_0055a4e0( +0x24 )` finds the ride script by
its id and answers its `+0xe0`, the coaster handle; nought gives nought (`0x004e0611`). `FUN_0043e0b0` reads the node
`[0x790bd0 + 4h]`, and `f` is its `+0x104` (`0x004e062d`, the call's third argument), `50 − f × −0.5f` truncated
(`0x7005a4`, `0x7005b4`, `__ftol`); the `+0x100` beside it, the sickness rating, is dropped. The arm reads no argument
and not the tier. **The handle**: the script loader zeroes `+0xe0` (`0x00558c6b`); `COAST 8`, run once near the top of
all 12 coaster scripts, stores `FUN_0043b050( script id )` (`0x00554a9f`), the `+0x14` of the node on the list
`DAT_00790fe0` whose `+0x24` is that id. The node is made with the object's mesh (`FUN_004368f0`, called only from
`FUN_00463060`, `0x00463897`; the constructor at `0x004db510`), zeroed, with a handle from 1 to 255 (`FUN_00468760`); the
script starter then writes the id to it (`FUN_004dcf90` to `FUN_004685c0`, the mesh instance's flag `0x40000`,
`FUN_00436f60`, `0x00436f87`). **The rating**: only `FUN_0043df90` writes `+0x104` (`0x0043e012`): `clamp( 0.5 × last
+ 1.7 × peak, 0, 100 )` of the excitement the per-sample run keeps (`FUN_0043c450` to `FUN_0043dc50`: each sample
`max( 0, 0.1v + Σ|g| + 5|b| − ( |g0| + U ) )`, plus half the last, from `fInitialExcitement` 1.0). The weights are
`sCoasterType` fields (`FUN_0042f390`) that no shipped `Coaster.sam` sets, so the compiled defaults (`FUN_0042fd90`)
are live. The run is redone on the next state tick (`FUN_00435a30` from `0x0054fa8f`) for a node whose `node[0]` bit 0
is set, as `FUN_0043c3d0` sets it whenever the speed setting changes: the cap `+0x34` = `80 × ( 1 − s / 100 ) + 130 × s
/ 100` (`fMaxSpeedAtMinSetting`, `fMaxSpeedAtMaxSetting`). The script's speed word `+0xc0` is pushed at `COAST 8`
(`0x00554ab4`) and on every VM turn (`0x005517d7`..`0x005517ea`), redoing the run only on a change (`0x0043b2c6`), so
the player's speed and an upgrade's (`0x004dfabc`) reach `f` through the cap; the tier does not. A new node starts at
setting 60, cap 110 (`FUN_004368f0`'s `FUN_0043c3d0( node, 0x3c )`). The coasters module (`SAOC`) saves neither rating
nor `+0x24`; its loader pairs each record with its object by mesh instance and marks the node for a rerun
(`0x004380af`), so a loaded coaster answers 50 until the first state tick.

**No coaster is offered before its circuit is closed.** The offer gate asks `FUN_00441970` for type 3 (`0x004dd9b7`),
after the room test: the node on the list `DAT_00790fe0` whose `+0x10` is the object's `+0x20` (`FUN_00436860`), which
is not a pointer but the object's model instance, the handle `FUN_00463060` returns (slot + 1); then on that node
`+0x140` nought, `+0x3c` bit 0 set and bit 1 clear, and not the one the editor has open (`DAT_00790fec`). No node, or an
object whose `+0x20` is nought, is refused. `+0x140` counts pairs of the coaster's sections whose boxes intersect
(`FUN_00439c40` adds, `FUN_0043ad50` takes away). Bit 0 is set only by the editor's finishing action (`FUN_00435570`,
`0x0043557f`) and by the `SAOC` loader from the saved flags (`0x004380e7`); bit 1, a gap open in the circuit, by the
editor's gap action (`FUN_00446740`, `0x004468f8`) and the loader (`0x004382b3`), and cleared when a piece bridges it
(`0x00442cac`). A new node's `+0x3c` holds bit 3 only (`0x004369f8`), so a coaster bought and never finished is refused.
The loader rebuilds each node from its saved header, the clash count last (`0x0043837d`), and zeroes `DAT_00790fec`
after each (`0x0043860a`); the header's layout is FileFormats `saves.md`'s. `mIsTrackRideValid` plays no part in the
gate or the excitement.

**A track ride** is any object whose `mTrackRideHandle` (`+0x28`) is non-zero (`0x004e06ce`): the base is `clamp( E ×
60 / 100 + clamp( 3 × crossings + the longest straight + 2 × bends, 0, 40 ), 0, 100 )` (`0x004e06fe`..`0x004e0756`),
then the ratio tail. `FUN_00545310( +0x28 )` walks the section list at the entry's `+0xbc`, linked through `+0x24`, by
each section's type (`+4`, low 16 bits): 9 and 10, 12 and 13, and 11 lengthen a run, 11 also counting a crossing;
anything else ends the run and counts a bend. The names follow the collision objects `FUN_0054b2f0` builds for each
type: one for 9, 10, 12 and 13 (12 and 13 flagged `0x100`), two for 1 to 8, five for 11. It walks the list twice
without a reset (`0x0054538d`), so a run can wrap from the tail into the head, and answers half the bends, the longest
run and half the crossings. A run is kept only when a bend ends it, so a list with no bend has a longest of nought. A
fifth answer, the straights with an add-on (12 and 13 whose `+8` is nought), is counted over both passes and read by no
caller of the excitement, and the return is the list's length (`FUN_004cc5d0` and `FUN_004cc6f0` read it). A stale handle (not `slot | entry[0] << 8`, `0x0054536d`) answers nought and writes
nothing: 60% of `E`. The placer calls `FUN_00545890( BumperType, … )` for any item whose `Bumper.BumperType` is set
(`0x00529e4d`): the first free of 64 entries, a copy of the template at `0x764178 − ( bt + 1 ) × 0xd0` whose first dword
is the BumperType, so the handle is `slot | BumperType << 8`, never nought; with all 64 taken it reads through a null
entry (`0x00546225`). The constructor stores it (`0x004db0dc`) and sets `mIsTrackRideValid` to 1 for every object
(`0x004db0df`); the place commit clears that only for an item with both a queue and a track type (`0x00524daa`,
`0x00524db7`, `0x005251b8`), so a bought Hot Pot or Belly Bounce keeps 1 and a bought Dino Karts does not. The table,
64 entries of `0xd0` bytes allocated zeroed (`0x005443d2`), is emptied on every full load (`FUN_005445d0`,
`0x0041506a`), and the `KART` loader puts each saved ride back in the slot its handle names (`FUN_00545890` handed the
handle, `0x00543725`, kept when the answer is that handle, `0x0054372d`), so a ride bought later takes the lowest slot
no saved one holds.
`FUN_00545890` adds no section; only `FUN_0054b2f0` does, called by the circuit walk `FUN_0052a970` as track is laid
(`0x0052abda`, `0x0052ac34`, `0x0052addb`) and by the loader of the track-rides module (`KART`, `0x00544061`). An arena
has no track cells, so the Hot Pot never has a section; a kart or water ride has none until the player lays track.
Demolition frees the entry (`FUN_00545610`, `0x00528584`: its sections go and `entry[0]` is nought) unless its third
argument keeps it (`0x00528570`); the only caller passing 1 is the cell reset `FUN_0053b280` (`0x0053b6e8`), on cells no
object stands on, so every demolition of an object frees it, a move's pickup included, and the put-down takes the first
free slot again.

**The tier** divides the track arm and the plain one, the sideshow's and the coaster's having returned: `InitSpeed[l]` (`+0x1a8 + 0x40l`) and `InitDuration[l]` (`+0x1a0 +
0x40l`), `l` the byte `+0x50`, unbounded (`0x004e0689`..`0x004e06c5`), both read before the track test. Only the
constructor (nought), the save (a raw byte) and the upgrade's completion (`FUN_004df8f0`, `0x004df966`) write `l`; the
completion then charges `CostOfUpgrade[l]` and sets the speed to `InitSpeed[l]`, the capacity to `InitCapacity[l]` and
the duration to `InitDuration[l]`, held as the constructor holds them (`0x004dfa9b`..`0x004dfbdf`), so an upgraded ride
starts at both ratios 1. The compiled schema's `Upgrades` has three tiers (`0x0074669c`), so `l` = 3 reads `+0x260` and
`+0x268`, `SupplementalMeshes[7].FileName` and `Attraction[0].NewBonus`; the upgrade list offers one only below tier 2
(`0x004ae2d4`) and the completion's "already at level three" check is a bare `RET` (`FUN_005da3c0`), so only a save's
byte can hold 3 or more. The upgrade list offers nothing in game type 2 (`0x004ae1a2`, UITEXT 27 "Upgrades are not
available in Instant Action mode"), and the stock Lost Kingdom park is Instant Action: nothing is upgraded there. Every
`Rides.sam` gives `InitSpeed` 60, 75 and 90, which no item overrides; `InitDuration` is 3, or the item's own at all
three tiers (the Belly Bounce 30, the Hot Pot 25, Jurassic Tours 40).

| Case | Inputs | The original | OpenTPW |
|---|---|---|---|
| The Hot Pot (1140), bought | `E` 70, handle `0xffffff00`, no sections, 60/60, 25/25 | 42 | 42 |
| Dino Karts (1150), bought | `E` 80, `0xfffffc00`, no sections, 60/60, 3/3 | 48, never offered (`+0x2c` nought) | 48, never read: `CanBeOffered` refuses first |
| Splish Splash (1160), bought | `E` 75, `0xfffffb00`, no sections, 60/60, 3/3 | 45, never offered | 45, as Dino Karts |
| Temple Of Gloom (1180), bought | `+0xe0` nought until `COAST 8`, then the station's own run | 0, then `50 + f / 2` of a run not measured; never offered | never offered: its record carries no model instance, so no saved header is its |
| Alexah's Dino Karts (thing 289) | 33 sections: 12 bends, 1 crossing, the longest run 6, at the list's head | 48 + 33 = 81 | 81 |
| Alexah's Temple Of Gloom (236) | `+0xe0` 1, `SAOC` handle 1, 38 pieces, speed 80 (cap 120); model instance 330, header flags `0x101`, no clash | 50 to 100, not known without the run; offered | 90, counted; offered |
| Alexah's tier-1 Belly Bounce (53) | `E` 40, `l` 1, 75/75, 30/30 | 40; speed 60 gives 32, 59 31, 90 48 | as the original |

The handles shown take slot 0; the low byte is whichever slot is free first.

Instant Action catalogues only items with an `Easy_` file (`0x00413ac4`): all four bought cases have one; Chac Atak
and Gorilla Thrilla do not. The saved rows were measured read-only in Alexah's played jungle saves (`q172save`, and a
walk of the `KART` module that lands on its tag in all nine park files); the bought rows follow from the reads above.

**Built** (`docs/QUEUE.md` Q172b). `ParkTrackRides` reads the `KART` module at the boundary, refusing it unless the
chunk walk lands on its tag, and `ParkTrackRideTable`, which `ParkState` holds, is the table of 64: seeded from the
save record by record as the loader does, with `FUN_0054b2f0`'s three refusals (a stale handle, a duplicate cell, the
pool of `0x400`), a slot taken by the placer for a bought item with a `BumperType` (`ParkBuilding.TakeTrackRide`) and
let go by the demolisher, and `FUN_00545310` ported (`Walk`). `ParkRideScore.ExcitementOf` takes the track arm on the
object's handle (`TrackBase`) and divides by the object's own tier (`ParkItemCatalogue.Item.StartingAt`, which reads
`Upgrades[0..2].InitSpeed` and `InitDuration` over the category's). `ParkRideChoice.CanBeOffered` asks `CircuitClosed`
of a coaster after the room test: a saved one is found by the record's `MeshInstanceID` in the `SAOC` module's first
header (`ParkCoasters`), and one placed here has none, so is refused. **Counted, not built:** the coaster's rating
(`RIDE_EXCITEMENT_COASTER_TRACK`), a tier past the third (`RIDE_EXCITEMENT_UPGRADE_TIER`, scored without the ratios),
a 65th track ride (`TRACK_RIDE_TABLE_FULL`), a saved ride outside the table (`SAVED_TRACK_RIDE_OUTSIDE_THE_TABLE`), and a
saved coaster whose header lies past the first or in a module that will not read (`SAVED_COASTER_HEADER_UNREAD`, let
through). There is still no coaster ride (the node list, `Coaster.sam`, the spline and sample run) and no editor or
track laying, and the door and the queue turn do not ask `CircuitClosed` (`OPEN_GUARD_COASTER_TRACK_RECORD`,
`QUEUE_TURN_COASTER_TRACK_RECORD`). OpenTPW asks the gate before the score, which changes only what the `unimplemented`
census counts. A bought thing starts with `mIsTrackRideValid` nought, where the original's is 1 for any item without
both a queue and a track type; nothing reads it for track type 0.

**Measured in the game** (`q172run.py`, silent, the stock jungle park, the build before any Q172 change, every reading
predicted first; `save/` unchanged; runs `q172-run1/`, `q172-run2/`). All three keys read 0 at load and after a `why`
of 13 guests. With the Belly Bounce sold and a Hot Pot bought at (57,23), its queue joined to the path at (56,22) and
`spend` reading it offerable, paused: the purchase added nothing; one `why` added 13 to `RIDE_EXCITEMENT_TRACK_CROWD`
for its 13 lines, all 13 choosing the Hot Pot, and a second 13 more; after 90 s running with `load 40`, one `why` added
48 for 48 lines, 37 of them choosing it. `RIDE_EXCITEMENT_COASTER_TRACK` and `RIDE_EXCITEMENT_UPGRADE_TIER` stayed 0.
Photographed paused beside that census: the Hot Pot with its queue, guests at the gate.

**Measured in the game after the build** (`q172brun.py`, silent, the stock jungle park, every reading predicted first;
`save/` unchanged; beside a control on the build before it; runs `q172b-final/`, `q172b-control/`). With the Belly
Bounce sold, a Hot Pot bought at (57,23) with its queue laid from (59,22) to the path at (56,22), and a Temple Of Gloom
at (60,30) with its queue laid to the path at (56,28), paused: the purchase logs the Hot Pot's `track 0xffffff00` (slot
0, BumperType -1) and `spend` its excitement 42; `spend` reads the Temple Of Gloom not offerable, and no `why` line
names it, of 13 guests paused or of 12 after 240 s running with `load 40`, where the control reads it offerable and a
guest chooses it; `RIDE_EXCITEMENT_TRACK_CROWD` stays 0, where the control's rises by one a scoring (13 for a `why`
of 13, 58 after the run). Photographed paused beside that census. No Hot Pot rider's excitement match was reached:
the Hot Pot lets no rider off, in this build and the one before it (13 guests boarded in 20 minutes, none let off;
`docs/QUEUE.md` Q179). Alexah's played jungle saves, read-only (`q172bsave`): the Dino Karts scores 81, the tier-1
Belly Bounce 40 at its speed of 75, and the Temple Of Gloom's record holds model instance 330, its header's, with flags
`0x101` and no clash, so it is offered.

### What it gives the three rides in Lost Kingdom

The Belly Bounce (1100) is `ExcitementLevel` 40 and costs 500; the Inca Totem (1110) 70 and 3,250; the Aztec Mayhem
(1104) 70 and 2,500. None is indoors, a track ride, or has a golden-ticket cost (only Jurassic Tours, 1, and
Eruption, 3, do in this theme; the Totem's 4 is its `Research.Group`, `+0x178`), and each scores its own
`ExcitementLevel` at its starting settings. Their thirst, hunger and relief terms are nought, so far from the queue
the mean is `( distance + excitement ) / 10`, and a ride needs the two to add up to 100.

| Preferred excitement (types) | Belly Bounce's excitement term | Totem's and Aztec's |
|---|---|---|
| 80 (0, 5, 7) | 20 | 80 |
| 65 (1, 4) | 50 | 90 |
| 50 (2) | 80 | 60 |
| 45 (6) | 90 | 50 |
| 35 (3) | 90 | 30 |

**The original does send guests to a new ride while an old one stands.** From its purchase sweep through the 184th
after it a bought ride scores five times over; the Totem scores 1.108 times over for ever (3,250 > 3,000: `trunc( s × 133 / 120 )` for every score
up to 5,000, at 24-, 53- or 64-bit precision); five guest types in eight prefer the two new rides' excitement; and a
guest leaving the Belly Bounce scores it nought until they leave something else, then a quarter, a third and a half
over their next three visits. The shipped park holds nothing new: its calendar loads at 2000-02-02 18:27:30
(`mFunnyTimeStart` 2000-01-01, `mGameTick` 755), every choosable object is stamped 2000-01-01 15:37:30, 32 days
before, and the bus, six days old, is not choosable.

**Measured in the game** (a throwaway instrumented build, never committed, which scores every guest decision twice from
the same inputs - OpenTPW's `ParkRideScore`, and this decode without rain, the refusal history, the track branches and
the original's queue count, none reached here but rain, which was not recorded - and prints each candidate's terms;
`q165run.py` and `q165run2.py`, silent, jungle, every reading predicted first; `save/` unchanged). The decode's pick
is computed inside OpenTPW's park, its positions and queues; it is not a run of the original. Q83's scene: an Inca Totem at
(57,23) and an Aztec Mayhem at (60,30) bought and queued to the path, then `load 30`. `why` aimed 43 of 43 guests at
the Belly Bounce, as in Q83; all 17 decisions logged over 480 s chose it, and the decode chose the Totem in all 17.
**75 s after the purchase, past the new window, asked of all 41 guests at once: OpenTPW's chooser picks the Belly Bounce
for 40 and the Drinks Shop for 1; the decode picks the Totem for 35, the Aztec Mayhem for 1 and the Belly Bounce for 5**
- every guest of types 0, 1, 5 and 7 a new ride and every guest of types 2, 3 and 6 the old one, as predicted. Thirty of the 41 were type
0 only because OpenTPW then made every arrival type 0 (Q165b draws it); the original draws one of eight, so about three
in eight would be types 2, 3 or 6. Photographed paused with that census: 13 guests in the Belly Bounce's queue of sixteen places, the
Totem's queue empty beside it. One guest's terms, a type 7 at (48,16) just after the purchase:

| | OpenTPW: distance, excitement, score | The decode: distance, excitement, score |
|---|---|---|
| Belly Bounce | 86 to the entry, 80 (preference 50), **16** | 92 to the back cell, 20 (preference 80), **11** |
| Inca Totem | 67, 60, 12 | 74, 80, 15, × 5 new, × 1.108 price: **83** |
| Aztec Mayhem | 19, 60, 7 | 49, 80, 12, × 5 new: **60** |

With the Belly Bounce sold, OpenTPW chose the Jungle Spray in all four decisions logged over 240 s, where the decode
chose the Totem twice and the Aztec Mayhem twice. The Spray is a sideshow: OpenTPW then scored its `ExcitementLevel`,
35, where the original computes 30 from its cost of goods 50, price 20 and chance of winning 25. That run's OpenTPW
column is the chooser before Q165b and Q165c built what the decode column shows.

### Where OpenTPW differs

| What | The original | OpenTPW | Reached in Lost Kingdom |
|---|---|---|---|
| Distance, the effects divisor and the queue term's distance test | at the back-of-queue cell | at the entry cell (Q105) | every candidate |
| The FPU's precision | not settled (`park-engine.md`, "Which rounding is live") | double, the runtime's starting precision | Eruption's golden ticket at some scores (62 against 63 at 45); the longest queue at 21,708 of Lost Kingdom's 1,690,500 slider settings (1.3%), each with the speed moved off its tier's (one more at 24 bits); a shop's cost of goods at an amount of special ingredient off the steps of 50 (the Drinks Shop at quality 0 and amount 10: 18, and 19 at 24 bits), only by a save's byte until the shop window is built: no save read holds one |
| A coaster's excitement | `trunc( 50 + f / 2 )` of its node's rating, or nought before `COAST 8` binds it | its `ExcitementLevel`, counted (`RIDE_EXCITEMENT_COASTER_TRACK`) | Alexah's saved Temple Of Gloom, which is offered (Q167); a bought one is refused before it is scored |
| A tier past the third | the divisors read from the fields after `Upgrades` (`+0x260`, `+0x268`) | its base without the ratios, counted (`RIDE_EXCITEMENT_UPGRADE_TIER`) | only by a save's byte; no save read has one |
| A sideshow's cost of goods and chance of winning, and a shop's cost of goods | the object's own `+0x188` and `+0x190` | the item's (Q97) | the chance, in Alexah's played parks: all seven sideshows hold 55 to 58, the Jungle Spray 58 where its item's is 25 (`~/.cache/tpw-harnesses/q177c/objmoney.py`); the cost of goods matches the item's on every jungle shop checked (`ParkBankTests`) |
| The longest queue at a tier past the third, or for an item the catalogue lacks | the constant and speed read from past `Upgrades`, or through a null descriptor | counted, and both gates let the guest through (`QUEUE_CAPACITY_UPGRADE_TIER`, `QUEUE_CAPACITY_UNKNOWN_ITEM`) | only by a save's byte; no save read has one |
| The calendar at load | the save's `mGameTick`, 755: 2000-02-02 18:27:30 | the score's calendar is the original's (`ParkState.CalendarNow`); the gadget's date and the weather's days count from nought (`GameCalendar.Rebase`, Q149) | every load |

**Built: the rest of the score** (`docs/QUEUE.md` Q165c). `ParkRideScore.Of` runs all twelve steps in the original's
order: the same kind as the thing left last scores nought first; the queue term counts to the first guest no longer
queueing (`ParkState.QueueCount`, `FUN_004ddf50( 0 )`) over the walked cells (`ParkRideChoice.QueueCellsFor`), and the
choice's room test and the arrival's gates read the same count; the mean is unsigned; new is `(uint)age <= 7`; rain is
the weather's `Drops` above nought, which `ParkPeople` hands in; then the golden-ticket or price factor
(`ParkRideScore.Priced`, at double precision, the runtime's starting precision, which is an assumption: Eruption's 1.4
on a score of 45 gives 62, where single or extended precision gives 63); then the two histories, the first matching visit and every matching
refusal. `ParkRideScore.ExcitementOf` is `FUN_004e0860( object, 0 )`, which the chooser, the arrival's excitement
refusal and the settle-up's excitement match all read: the Jungle Spray is 30, and the Belly Bounce at its settings 40. Each guest keeps both histories
(`Peep`), read from the save (`ParkWorld.GuestState`), written on leaving any thing (`ParkRideOperation.SettleUp`,
before the charge) and at both refusals, aged on the sweep and cleared by a removal as above. A bought thing is
stamped with the park's calendar (`ParkBuilding`, `0x004db66a`); the calendar is `GameCalendar.Epoch`, 2000-01-01,
which the clock constructor seeds (`FUN_004f7e80`, `0x004f7ea0`), plus `mGameTick × 3750` seconds; the object window's
Age reads the same age, signed. The arrival's third gate, too long (`FUN_004ddb60`), is built on
`PeepBehaviour.LongestQueue` (100 without the queue-path bit; with it, "The longest queue - `FUN_004dda40`" above) and
pushes the refusal like the excitement gate.

**Built: each kind's preference and an arrival's kind** (`docs/QUEUE.md` Q165b). `PeepBehaviour` hands the park's
`ParkBalance` to its chooser's `ParkRideScore`, so each kind prefers its own `PeepTypes[n].PreferredExcitement`, 80, 65,
50, 35, 65, 80, 45 and 80, in the score and at the arrival's refusal, which reads the same byte (`FUN_004fd4e0`,
`0x004fd50a`): a kind 0, 5 or 7 turns away from the Jungle Spray, 50 from its 80 where 44 is allowed.
`ParkPeople.Admit` draws each new guest's kind.
The constructor's draw (`FUN_004faec0`, `0x004fb019`) is the world generator `FUN_00516330` modulo `[0x007851d4]`, the
balance's `PeepTypes` row count, the slot after the table's 20 rows of 12 bytes at `0x007850e4`. `FUN_004013e0` zeroes
it before the global file only (`FUN_004017a0`, `0x004017b6`), and each of the loader's value paths
(`0x00401aab`, `0x00401bbb`, `0x00401ccf`, `0x00401e4b`) raises it to the index set plus one, so it is the highest row
the balance stack sets, plus one. The two global files, `data/levels/Standard.sam` and `Online_Standard.sam` (one or
the other is loaded, `FUN_005156a0`), each set rows 0 to 7, and no theme file sets a row: 8 in every shipped park,
which OpenTPW takes as the constant `ParkWorld.GuestState.PersonTypes`. The person base's constructor draws first,
the base speed `% 5` (`FUN_004f8940`, `0x004f89e1`; "Where a WALKING peep is drawn"); then the kind is the second of
the guest constructor's eight unconditional draws, after the exit level's variation (`0x004faff8`) and before the
cash's (`0x004fb046`); the five after it set thirst and hunger (`% 50`), toilet (`% 30`), one discarded, and the
prankery byte `+0x1c0` against `PrankeryLikelihood`, and two more follow on one branch (Q85). OpenTPW draws the kind
and then the base speed, from `System.Random`, and varies neither cash nor exit level (`park.md`, "What a new guest's
fields come from"), so the ranges are the original's and the sequence is not.

## A second toilet: the minor decision and the saved major - `FUN_004fd570` and `FUN_00500900`

Decoded 2026-09-28 (`docs/QUEUE.md` Q170): five read-only decoders (the history's writers, the destination's writers,
what reads toilet-ness and the need, the walk and the exit, the state machine and `Toilet.rse`), each put to a skeptic
reading the disassembly, then a synthesis - 85 claims, 71 upheld, 14 amended, none refuted; the walking arm's counter,
both functions, `FUN_004d8b40`'s search and the toilet arm re-read first-hand. **The score does not send a guest to a
second toilet; the walk does.** The same-kind nought (step 1 above) holds for every small toilet after any other: `+0xe`
is `mId`, the catalogue item (written by the constructor at `0x004db0c7`, read by the object reader under `"mId"`,
handed to the item lookup `FUN_00412e90`), 1402 on all of them. `mPreviousRides` has four writers - the constructor, the
settle-up, the removal message and the save reader - and the settle-up's one caller is `ExitRide`. Nothing else aims a
guest at a toilet: the only writers of a thing into `+0x1dc` are the chooser (`0x004fcbdc`), the minor decision
(`0x004fd92a`), the litter arm (`0x004fedef`, `+0x32 & 0x40` only; a small toilet's flags are `0x25`), the restore
(`0x00500923`) and the save reader; `PeepInfo.ToiletDesparate`'s global (`0x00785074`) has no reader; the needs turn,
the `InQueue` turn, the thought picker `FUN_004fc8a0` and `Toilet.rse` aim nobody.

**The minor decision**, `FUN_004fd570`, on the guest (`ECX`). The state-10 walking turn, still walking and the park
open, adds one to the byte `+0x2c` (`mCount`, file offset 36 of the person record) and, the byte stored first and
compared unsigned, on passing 11 zeroes it and then calls this (`0x004ffef2`..`0x004fff06`): every 12th walking turn,
counted across walks, since only the person base's constructor (`0x004f8988`) and its loader (`0x004f91de`,
`0x004f91e8`; `0x004f8cc8` is the save's write arm) otherwise write it - a program-wide scan of byte stores at `+0x2c`
finds no other on a person. Nothing follows the call in the guest's turn, which the needs turn opened. The park-shut arm
before it (`0x004ffeb9`..`0x004ffee5`) pushes no event and counts nothing. A thing is swept once a `mGameTick` and 12 is
even, so the decisions of one walk fall on one parity while a walking turn comes every tick; in Alexah's two saves the
byte spans exactly 0..11 on all 339 guests, and 0 on every member of staff.

1. Nothing without a `MajorDest`, or when its `GetBackOfQueue` cell is nought (`0x004fd582`, `0x004fd62e`).
2. The window: x from the guest's x − 2 to + 1 (outer), y from − 2 to + 1 (inner), on the map; only a cell whose squared
   distance to the major's back cell is **strictly less** than the guest's own (`0x004fd6cc`).
3. Each such cell's thing list (the word at the cell record's `+0x24`, next at the thing's `+0xa`): an object (type byte
   `+2` = 3) that is not the major (`0x004fd734`) and passes the offer gate `FUN_004dd920` (`0x004fd73f`), scored by
   `FUN_004fcc30` (`0x004fd74b`). **The best starts at nought** (`0x004fd599`, `0x004fd59e`): a higher score wins, and
   an equal one wins on an odd `mGameTick` (`0x004fd754`..`0x004fd76d`) - nought included, which the chooser's floor of
   10 never allows. Nothing taken, return (`0x004fd7aa`).
4. **The switch test**: `FUN_004d8b40` from the major's entry (`+0x36`) to the guest's cell, and from the same entry to
   the candidate's entry; neither −1 and the second **shorter** (`JLE` at `0x004fd888` refuses a tie). `FUN_004d8b40`
   runs `FUN_00511ef0`, which is `FUN_00511420` without its `FUN_005108a0`: the line stepper `FUN_00511470` alone (its
   own straightening at `0x00511a6c` included), with a budget of 60,000 and the guest's mode (`+0x188`), and no splices
   after it. It sums `|dx| + |dy|` between the waypoints it recorded
   (the record's `+6` on), so the leg from the start is not counted; −1 on `0x70000000`.
5. **A switch**: the log "Minor Decision: OID=%d, @=(%d, %d), sc=%d" (the candidate's `mId`, its cell, the score),
   event `0x17` naming the candidate (`0x004fd915`), `+0x1dc` = the candidate and `+0x1de` = the old major, **whatever
   `+0x1de` held** (`0x004fd91f`..`0x004fd934`; nothing here or in its caller reads `+0x1de`), and `FUN_004fa530` to
   the centre of the candidate's **entry cell**, its answer ignored (`0x004fd93b`); still state 10. The stranded
   refusal inside `FUN_004fa530` cannot fire here, since the same turn's walk zeroed `+0x198` (`0x004fa30b`). Arriving
   at the entry, the arrival's test (`0x004ffc3d`) finds it is not the back cell and re-aims there
   (`0x004ffe16`..`0x004ffe44`).

An object is linked on one cell's thing list, the list at the cell record's `+0x24` (`mWho`, chained by each thing's
`mMapChild` `+0xa` and `mMapParent` `+8`): the cell it is constructed on (`FUN_0050afe0`, `0x0050b057`), which is its
position cell; only a person is relinked (`FUN_0050b6a0`, `0x0050b76b`). So the window finds an object on its anchor,
never its footprint or entry, and meets it at most once. The gate `FUN_004dd920` and the score `FUN_004fcc30` are the
chooser's own calls with the chooser's arguments, asked gate first, and the score cannot come out negative.

**A second switch is possible in Lost Kingdom's corridor**: after 23 to 22 at (56,18), toilet 21 on (55,17) is in the
next window if the guest is still on (56,18), which would save 22 and lose 23.

**The restore**, `FUN_00500900`, the state-15 turn after `ExitRide`: on arriving, with `+0x1de` set, `MajorDest` = it
and `+0x1de` = 0 (`0x00500923`, `0x0050092a`), before anything is asked; an object whose back cell routes gives state 10
("Left minor destination, found old major one again!", no event); a thing whose type byte is no longer 3 gives
`MajorDest` 0 ("Deleted major dest while I was doing minor dest!"); and anything else falls to the tail, "successfully
left ride %d, becoming idle" and Deciding with `MajorDest` as it stands (`0x005009f9`) - with nothing saved, the thing
just left. **No score, no offer gate, no shut or room test**, so the same-kind nought is never asked. A failed walk off
(2) zeroes `MajorDest` and leaves `+0x1de`. ExitRide enters state 15 only when its aim routed (`0x005015ef`).

**Three ways to a second toilet**, then:

- **Diverted, then restored.** The chooser sends a guest whose last visit was not a toilet to toilet A; on the way the
  minor decision takes a toilet B on the way and saves A; B is visited and written first; the restore sends them on to A
  unscored. The history, newest first, reads A, B.
- **A stale saved major.** `+0x1de` is cleared only by the restore, the removal message (`0x004fb4b3`) and the
  constructor; every other way of giving up clears `+0x1dc` alone (`0x004fcb21`, `0x004ffcef` - the arrival's
  excitement refusal - `0x004ffe9f`, `0x004ffeda`, `0x00501378`, `0x005014bd`, `0x005007bd`, `0x00500a3b`, `0x005022e0`,
  `0x0050231c`), and the litter arm, which also runs in state 10, puts a bin in `+0x1dc` and leaves `+0x1de`
  (`0x004fedef`). A diversion given up leaves A saved, to be
  restored unscored after the guest's next exit from anything - the same thing twice when the chooser has picked A again
  meanwhile.
- **Nought on an odd tick.** Right after a toilet every small toilet scores nought, and the minor decision still takes
  one on an odd tick when nothing in its window scores above nought.

**A toilet visit empties the need** (step 5 of "The effects of a visit"), so in the original nobody leaves a toilet
still in need; a second toilet is the walk's doing, not the bladder's.

**In Alexah's two played saves** (`mGameTick` 19,007 and 19,004; `q170probe`, and the synthesis's read-only walk of the
records): 28 small toilets stand in rows along dead-end corridors, so the chooser's straight line to a back cell often
names one deep in a corridor and the walk passes others. 19 guests (20) are part-way through a diversion - `MajorDest` a
toilet, `mSavedMajorDest` another, the newest visit not a toilet - against 5 bound for a toilet with nothing saved, 3 of
them walking, one in its queue and one inside: 19 of 24, 0.79, a snapshot in which the 3 walking may yet be diverted.
The histories agree on their own: 107 toilet-then-toilet pairs (105) against 136 other-then-toilet, 0.79, which puts
toilet-then-toilet at 0.79 / 1.79 = 0.44 of the pairs that start at a toilet, against 107 of 254 (0.42) read.
`mSavedMajorDest` is non-zero on 50 guests of 339 (51), 31 of them a toilet; 3 guests hold the same thing in both, which
only the stale saved major makes; and two runs of three toilets in a row need a nought taken, since a restore clears
`+0x1de`. 68 of the 75 guests whose newest visit is a toilet have need nought, and all 7 others have an id divisible by
four, the only guests whose need grows. Illness pulls more guests to toilets than need does - the score's illness term
counts only for a toilet (step 6 above): of 46 (47) guests heading for one, 6 have need above 40 and 20 (22) illness
above 40, 4 both, and 24 (23) neither, near enough to score one 10 to 19 on distance and queue alone. How the 107 split
between the three ways is not established: the original's log is the bare `RET` `FUN_005da3c0`.

**Measured in OpenTPW's park before the build** (a throwaway build working out the minor decision on every 12th walking turn - the
window, the scores, the raw search's two lengths - and logging what the original would do without doing it; it finds an
object only on its anchor cell and scores with OpenTPW's `ScoreOf`, the distance at the entry cell (Q105); `q170run.py`,
`q170run2.py`, `q170run4.py`, judged by `q170analyse.py`; silent, jungle, `toilet 90` on every guest every 20 s (once,
in run 1), predicted first; `save/` unchanged within each run). Lost Kingdom's three toilets stand at the dead end of
the path up column 56, 21 at y 17, 22 at 16 and 23 at 15, each back cell the path beside it. In 900 s: 43 walks to a
toilet long enough for a decision, and on 12 of the 14 walks to 23 a switch - the first on each walk to 21 from (56,19),
lengths 4 and 3, six times, and to 22 from (56,18), 3 and 2, six times - none on the 24 walks to 21 or the 5 to 22, and
none toward anything else. So the original would make 12 toilet-then-toilet visits there; OpenTPW then made none. (Four
walks log a second switch, to 22, which a guest already turned to 21 would not reach: the shadow never switches.) The
`JLE` refuses a tie from (56,17) toward 22, 2 and 2, eleven times, and from (56,18) toward 21, 3 and 3, four times. The
shadow's decisions came 12 ticks apart, 397 of 397, so OpenTPW turns a walking guest once a tick; a toilet was the best
at nought five times, for two guests just off a toilet, on odd ticks as the tie rule requires, and none switched
(lengths 16 to 19 against 18 or 19). Photographed paused with `peeps` and `why`: guest 66 at (56,19) bound for 23 as a
switch to 21 is logged. Before the build a toilet emptied nothing: 32 visits of 32 left the need where it was, 90 to 95
(`SETTLE_UP_TOILET_RELIEF` 32), no guest chose or visited a toilet after one, and all 7 first choices after one were the
Belly Bounce; in a fourth run of 45 s, guest 35 was photographed walking to it off toilet 21, need 90 in `peeps`. And in
none of the four runs did a guest's walk off a thing report arriving (the shadow's line there, 0 of 32 toilet exits in
the third): OpenTPW then left `LeavingRide` by its give-up arm (below).

**Built** (Q170b): the count, read from the save's byte 36, on every `GoingToRide` walking turn with the park open
(`PeepBehaviour.WalkOn`); the decision (`MinorDecision`, the pick `ParkRideChooser.MinorDecisionFor`, the lengths
`CellSearch.RouteLength`); `Peep.SavedMajorDest`, read from file 499, cleared for every guest by a removal naming it;
the restore (`SentOnToTheSavedMajor`); and the toilet arm (`ParkRideOperation.UseTheToilet`). Building it found why
OpenTPW's walk off never arrived: `PutDownAtTheExit` stepped against the exit's facing (`CellEdge.DirectionFor` answers
from the side of the cell entered), aiming a toilet's user at the bare ground west of it and the Belly Bounce's into
its own footprint, so every guest let off anything gave up on the spot.

**Where OpenTPW still differs:**

| What | The original | OpenTPW | Reached in Lost Kingdom |
|---|---|---|---|
| The switch test's mode | the guest's `+0x188`, which some arms set to 1 | mode 0 (`ParkPeople.WalkingMode`); mode 1 also lets a step leave a path for bare ground (`0x004d8a37`) | not established: which states a guest walking to a thing can hold 1 in |
| The score in the window | to the candidate's back cell | to its entry cell (Q105) | every decision |
| Within one cell | the list, newest linked first | the park's order | no: no two objects share a cell |
| The park shut under a walk | `BigHappinessChange`, `MajorDest` 0, Deciding, uncounted | walks on, uncounted, `GOING_TO_RIDE_PARK_SHUT` (Q102) | the entry-price door |
| The switch's event `0x17`, the toilet's `0x11` and `0x12` | the guest's event ring | counted (`MINOR_DECISION_EVENT`, `SETTLE_UP_TOILET_EVENT`, `SETTLE_UP_TOILET_ILLNESS_EVENT`) | every switch and toilet use |
| A toilet dirtied by use | `FUN_004e2440` | counted, `SETTLE_UP_TOILET_DIRTYING` (Q100) | every toilet use |
| An exit that will not route | ExitRide closes the ride, no state 15 | dismissed anyway, then the walk off gives up, keeping `+0x1de` | none in the stock park |

## The staff turn - `CStaff`, every clock `mGameTick`

Decoded 2026-09-25 (`docs/QUEUE.md` Q82): the guard's handler read by hand, then six decoders (the researcher, the
mechanic, the handyman, the entertainer, the shared `CStaff` code, and the caller with the save and the balance slots),
each report put to a skeptic reading the disassembly - 116 claims, 97 upheld, 19 amended, none refuted - the
load-bearing ones re-read by hand, and this page put to a read-only review (four reviewers, a skeptic on each finding).
**Every clock a member of staff reads is `mGameTick`**, one count a thing sweep (248 ms; `park-engine.md`, "What the
31 ms tick drives"). No staff function, nor any of the 191 within three calls of the five handlers and their
pre-steps, reads the 31 ms counter `[0x00877d34]`. The game's millisecond clock (`0x00785970`, read by
`FUN_00402d70`) is reached only four calls down, in the world-sprite initialiser `FUN_004758f0` (`0x004759ce`, through
`FUN_00475a10`), for how long a sprite lives: a thought balloon's (`FUN_0050be80`, `0x0050c062`), and the upgrade
effect when a mechanic finishes a ride in state 2 (`FUN_004df8f0`, `0x004dfa54`).

### How a turn is reached

`FUN_00516380` increments `mGameTick` (`0x00516394`) and then calls `FUN_0050b360` for every live thing
(`0x005163fb`), which switches on the model byte (`0x0050b366`, table `0x0050b500`) and gives each kind of staff a
pre-step and its handler back to back. They are the only callers, so **each handler runs once per `mGameTick`**, sees
it already incremented, and has no stagger. In world state 4 the whole thing loop is skipped (`0x0051639e`) while
`mGameTick` still counts.

| Model | Kind | Pre-step | Handler | Its own SetState | Its decide |
|---|---|---|---|---|---|
| 4 | mechanic | `FUN_004da5a0` (a thunk) | `FUN_004da490` | `FUN_004da370` | `FUN_004da5b0` |
| 5 | handyman | `FUN_004d7060` | `FUN_004d73c0` | `FUN_004d7330` | `FUN_004d7100` |
| 6 | entertainer | `FUN_004d4660` | `FUN_004d4810` | none: `FUN_005054d0` direct, `0xe` written inline | `FUN_004d46d0` |
| 7 | guard | `FUN_004d6360` | `FUN_004d6410` | `FUN_004d65d0` | inline; `FUN_004d63d0` after a rest |
| 8 | researcher | `FUN_00502960` | `FUN_005029f0` | `FUN_00502c20` | inline; `FUN_00502c70` after a rest |

Every pre-step reaches the shared staff tick `FUN_00505490`: `FUN_004fa870` every sweep, and `FUN_0050be40` (a thought
balloon's expiry, mGameTick > its stamp + 12) only when `(mGameTick & 3) == (id & 3)` (`0x005054a6`). The handyman's
pre-step calls `FUN_0050be40` again, ungated, every sweep (`0x004d70f1`). Each handler switches on `mState`
(`+0x19c`): 0 and 1 are its own arms, 2 is `FUN_00505fe0` (going to a rest area), 3 is `FUN_005061d0` (resting), 4 to
7 are `CStaff::ModelState` `FUN_005056e0`, and the kind's own states are the mechanic's `0xc` (to a ride) and `0xd`
(repairing), the handyman's 8 and 9 (to litter, sweeping) and `0xa` and `0xb` (to a loo, cleaning), the
entertainer's `0xe` (performing), the guard's `0x10` (a chase), `0x12` and `0x13` (to the exit and back), and the
researcher's `0xf` (researching). Another kind's number reaching `FUN_005056e0` takes its default, which does nothing
(its log call, `FUN_005da3c0`, is a bare `RET`). At the end of a rest `FUN_005061d0` runs the kind's decide in the same
sweep (`0x00506298`), after `FUN_00506d10` has set state 0 with stamp 0. The rest ends on `+0x1fc` alone:
`FLD [ESI+0x1fc]`, `__ftol`, `CMP AL,0x64` (`0x00506275`..`0x00506280`), so it ends when that float truncates to 100;
`+0x1f8`, raised beside it, is not tested.

### The idle wait, and the stamp it counts from

- **The same test in all five idle arms** - guard `0x004d6545`, researcher `0x00502b90`, mechanic `0x004da54d`,
  handyman `0x004d752c`, entertainer `0x004d4957`: mGameTick against `[0x00785330 + grade * 0x10] + [+0x200]`, and
  `JBE` stays. So a wait ends on the first sweep where mGameTick > stamp + IdleDuration, unsigned: **IdleDuration + 1
  sweeps**. `0x00785330` is `PerGradeStaffConsts[grade].IdleDuration` (stride 16, after `BaseWage`; the `PeepInfo`
  slot table replayed as `park.md`, "Arrivals", did, closing on `0x00785828` and landing `Arrival.MinPeople` on
  `0x00785310` as its control). `data/levels/Standard.sam` gives 40, 30, 20, 10, 5, which no jungle file overrides:
  41, 31, 21, 11 and 6 sweeps, 10.17, 7.69, 5.21, 2.73 and 1.49 s.
- **Only a walk stamps it.** `CStaff::SetState` `FUN_005054d0` case 0 writes `+0x200` = mGameTick while `+0x19c` still
  reads 1 (`0x00505534`) and 0 otherwise (`0x00505542`), so an idle entered from a rest, a job, a strike or another
  idle is over on the next sweep. Case 6 always stamps (`0x005055b1`). The same setter writes the purpose speed
  `+0xc2`: nought in cases 0 and 5 (`0x00505555`, `0x00505590`) and 25 in case 4 (`0x0050556c`), the words at
  `0x0075c7f0` and `0x0075c7f2`.
- **State 6** (OpenTPW's `Waiting`; no string names it; `STAFFSTATES.str` holds Idle, Patrolling, Working, Resting, On
  strike, Picked up): `FUN_005056e0` leaves it for 0 once mGameTick − `+0x200` > 3 × IdleDuration, unsigned
  (`0x00505745`). Nothing in the executable enters state 6; only a saved `mState` can.
- **Nothing zeroes a stamp that reads ahead of the clock.** What `FUN_004f9490` zeroes at its opening is `+0x198`,
  `mStrandedTime`, against the route-call serial `[0x007cdb98]` (`FUN_004d8c50`) - a different stamp on a different
  counter - and a member of staff gets a non-zero `+0x198` only from a save: its one live writer, `0x004f9e09`, is past
  the type test that sends kinds 4 to 8 to the patrol roll.
- **The save keeps the stamps on the save's clock.** The `CStaff` serialiser `FUN_00504de0` reads `mTimeStartedIdling`
  into `+0x200` raw (`0x0050540a`); the World block's writer `FUN_00516c80` writes `mGameTick` (`0x00516f06`) and the
  five staff serialisers (`0x00517723`..`0x0051779b`) in one pass, the loader reads `mGameTick` before any thing
  (`0x00517bec`), and nothing rebases either. Each kind's `+0x214` is saved too (FileFormats `saves.md`).
- **So Lost Kingdom's guard**, saved idle at grade 3 with 752 against the save's 755, **leaves idle on mGameTick 763**,
  the eighth sweep, 1.98 s in. `FUN_00506a40` answers 0 there. Its strike arm is skipped (`JZ` at `0x00506a63`): the
  guards' flag, `mStaffHQ` `+0x28 + 3 × 12` (`0x00506a5c`), is 0, because the save's strike-system record (model 9,
  read by `FUN_00508bb0`) holds 0 for all five kinds with `mForceStrike` 0, and only `FUN_00508f70` sets one, not
  before the park's 24th month. The guard's rest byte 79 is over `RestLevel` 1, and a happiness of 91 only spares the
  mood draw. 763 & 3 is 3: the guard walks if a destination is found.

### Leaving idle, or a walk: the choice by kind

Every decide in this table calls `FUN_00506a40` first (strike, tired, mood; it answers 1 only after setting state 4 or
2); the end of research does not (the jobs table). Then:

| Kind | The choice | On staying, or on no destination | Where |
|---|---|---|---|
| guard | **`mGameTick & 3`, not a roll**: 0 stays, else `FUN_004f9490` | `FUN_004d65d0( 0 )` | `0x004d655d` idle, `0x004d64e9` walking, `0x004d63e1` after a rest, `0x004d5e76` at hire |
| entertainer | unless too tired (`FUN_00506680`), a world draw mod 3 (`0x004d4756`): 0 looks for a guest in the square of `ActivationDistance` (3, 3, 4, 4, 5) around them, and one found draws once more (`0x004d47b5`, unused) and performs; otherwise **`mGameTick & 3`**, as the guard's | staying: SetState(0); no destination: the state is left as it was | `0x004d46fe` |
| researcher | **the world random** `FUN_00516330` & 3: 0 does not walk | **research**: state `0xf`, `+0x214` = mGameTick, animation 10 - unless `FUN_00506680` says too tired, which leaves the state; its decide never picks 0 | `0x00502ba9` idle, `0x00502ad2` walking, `0x00502c82` after a rest, `0x005026cb` at hire |
| mechanic | none: a ride to fix (`FUN_004daa90`), else **a random walk every time** | `FUN_004f9490` failing: SetState(0) | `0x004da6fa` |
| handyman | none: litter (`FUN_004c8ed0`), else a loo (`FUN_004d7880`), else **a random walk every time** | as the mechanic | `0x004d712d` |

**So every kind walks about when it has no work**: the mechanic and the handyman without a pause, the guard and the
entertainer three sweeps in four by the clock's bits, the researcher three in four by a draw, researching on the
fourth. **A tired member of staff with no rest area found or reached walks on too**: that arm of `FUN_00506a40`
answers 0 (`0x00506c04`..`0x00506cf9`) and the kind's own choice follows. A guard's wait after a walk begins either on
a multiple of four or on a sweep where the next destination was not found. No shipped IdleDuration + 1 is a multiple
of four, so the first kind ends on a sweep that tries a walk, taken if `FUN_00506a40` answers 0 and a destination is
found; the second, stamped on a remainder of 1 to 3, can end on a multiple of four and stay, which stamps 0 and decides
again on the next sweep. The entertainer discards one more draw after staying and after a walk found (`0x004d4734`,
`0x004d4718`), none when no destination is found.

### The jobs, on the same clock

| Kind, state | `+0x214` | Ends | Balance key (Lost Kingdom, grades 0-4) |
|---|---|---|---|
| entertainer `0xe` | `mTimeStartedEntertaining` = mGameTick (`0x004d47f4`) | mGameTick > it + WorkDuration (`0x004d4858`), then cat_staff effect `0x87` | `EntertainerConstsPerGrade.WorkDuration`, `0x00785398`: 10, 20, 30, 50, 75 |
| handyman 9, `0xb` | `mTimeStartedCleaning` = mGameTick (`0x004d7371`) | the same test (`0x004d76c9`, `0x004d7462`) | `HandymanConstsPerGrade.WorkDuration`, `0x007853ec`: 40, 30, 20, 10, 5 |
| researcher `0xf` | `mTimeStartedResearching` = mGameTick (`0x00502c45` and inline) | the same test (`0x00502a36`), then `FUN_004f9490` at once (`0x00502a40`), with no `FUN_00506a40` and no draw: a destination walks, none restamps and researches again. A tired researcher rests only after that walk's own decide | `ResearcherConstsPerGrade.WorkDuration`, `0x00785458`: 10, 20, 30, 40, 50 |
| mechanic `0xd` | `mDurationOfRepair`, a count down one a turn: WorkDuration × (100 − the ride's `+0x44`) / 100 for a repair, × the item's figure for an upgrade | at 0 (`0x004da86d`..`0x004da8d6`), and on a broken ride only once `VAR_BROKEN` reads 0 | `MechanicConstsPerGrade.WorkDuration`, `0x0078542c`: 80, 60, 40, 30, 20 |
| guard `0x10` | `mProsecutionTimestamp`, **a count down despite its name** | at 0 the chase is abandoned (`0x004d6605` loads it) | `GuardConstsPerGrade.WorkDuration`, `0x00785498`: 10, 20, 30, 50, 75 |

A ride's or a loo's claim is stamped `+0x60` = mGameTick (`FUN_004e01f0`, `0x004e01ff`) and let go by `FUN_004e0220`
once mGameTick > it + 100 if the claimant has moved on. A researcher adds `ResearchAbility[grade]` to the lab on every
sweep where mGameTick % 20 is nought (`0x00502984`), in any state but 2 to 5 and 7. An idle handyman's pre-step looks
for litter on sweeps where mGameTick % IdleDuration is nought (`0x004d7087`). The guard's `mGameTick & 1` picks ticket
booth or entrance A or B in states `0x10`, `0x12` and `0x13`. `mTimeHired` is mGameTick scaled to the calendar
(`FUN_004f8690`, `0x00504bb8`).

### Drawn on the way: sounds, thoughts, the random and the strike

- **Every idle and walking turn of every kind draws the world random once**, and at `(r & 0xf) == 0` plays a
  cat_staff effect at the member's position through `FUN_004faa00` (a thiscall: `FUN_004754e0` for the position, then
  `Sound_PlayEffect`): idle `0xa1` handyman, `0xa3` mechanic, `0xa5` entertainer, `0xa7` guard, `0xa9` researcher;
  walking `0xa0`, `0xa2`, `0xa4`, `0xa6`, `0xa8`; `0x8a` a researching turn. The draw is taken whether or not anything
  plays. Three more play every time, with no draw: `0x87` (`TADA.mp2`) at a performance's end, the guard's `0x88`
  (`Oi.mp2`) as a chase starts (`FUN_004d6260`, `0x004d62a1`, reached only from `mStaffHQ`'s `FUN_00508a30`) and
  `0x89` on a catch (`FUN_004d6790`, `0x004d692e`). The guard's `0xa6` is `blank44.mp2` 60% and `gd_wk01`..`gd_wk05`,
  `0xa7` `blank44.mp2` about 79% and `gd_st01`, `gd_st02`; `0x89` and `0x8a` hold only the 9 ms `blank44.mp2`.
- **Thoughts** go through `FUN_0050be80` on the member's `+0x30`, with a second argument of 0 at every staff call.
  Past two early outs whose meaning is open (`[0x00790ab0] & 0x16`; `[0x00fb3b7c]` = 1), it first frees whatever
  balloon `+0x84` holds (`0x0050bee3`), then shows the new one only when mGameTick >= `+0x8c` + 20 × class, unsigned
  (`0x0050c04b`). `+0x8c` is stamped on any showing (`0x0050c05c`), so one stamp serves every thought, and a gated call
  leaves no balloon. Classes: `0x12` 3 (60 sweeps), `0x13` 2 (40), `0x14`, `0x15` and `0x16` 0, shown on every call.
  `0x14` tired, `0x13` happiness at 10 or under, `0x12` over 97 on a 1-in-16, `0x15` each strike-walk turn, `0x16` a
  patrol roll that fails thirty times.
- **A handyman who finds litter reseeds the world random with the litter cell's id** (`FUN_004d7100`, `0x004d71ca`,
  `FUN_00516370`, which writes `[world+0x1da708]`), then draws twice for the point in the cell (`0x004d71d5`,
  `0x004d71e5`), each `& 0x7f` clamped to 1..9 and times 13, x then y. The point depends on the cell alone, almost
  always 117 (P(9) = 119/128), and every later world draw in the park goes on from that seed.
- **Tired is `(u8)trunc( rest ) <= RestLevel`**, signed and inclusive (`0x00506b41`); "too tired to work",
  `FUN_00506680`, is strict. Not tired, `FUN_00506a40` sets the speed word `+0xc0` from the rest byte: 60, 80, 100,
  120, 140 by fifths (`[0x0075c7f8]`).
- **The patrol roll `FUN_00506f30`** takes a cell only when it is on the map and path, `mType` 1 (`FUN_00536310`),
  before it routes. A member of staff whose corners are both 0 is outside every cell (`FUN_00506ed0` has no case for
  it); every Lost Kingdom member carries an area.
- **The strike.** `mStaffHQ`'s month handler `FUN_00508e70` runs every month the park is open (its park-closed gate is
  `0x00508e7e`..`0x00508ec1`): for each kind with staff it clears a set flag `[HQ + 0x28 + kind × 12]`, or calls
  `FUN_00508f70`, which returns until the date passes 24 months; past that, `FUN_00509360` raises the kind's level and
  levels 1 to 4 set the flag. Every decide opens with `FUN_00506a40`'s strike arm (`0x00506a4d`..`0x00506a77`: the flag,
  and `FUN_0051a290`, the gate's `VAR_STATUS`, reading 1), which aims at the strike area with four draws and takes
  state 4; state 5's `FUN_00506300` ends it. The epoch of the 24-month gate (`FUN_004f8800`) is not traced.

### Measured in the game before the build, nothing changed

This is OpenTPW as Q82 found it, on the 31 ms counter; the build (Q82b) is measured in the next section.
`q82measure.py`, silent, jungle, two runs, predicted before the park loaded; `save/` unchanged in both. The instruments
are `arrivals` (`ParkState.GameTick`, the park's `mGameTick`), `state` (`GameClock`'s `ticks=`) and `staff`, read
together in one frame, sweep by sweep. Each run missed ten sweeps while it took the on-show photographs (756 to 765 in
the first, 781 to 790 in the second). The first run crashed after mGameTick 1006, when a guest went home under the
`facing` overlay (Q137); the second ran from 755 to 1380, 625 sweeps, 616 of them read.

- **The stamps are the 31 ms counter, not the park's clock**: each is the 31 ms tick of its spell's first sweep, a
  multiple of eight (312 at mGameTick 794), which the census's `ticks=` reads on that sweep or one later.
  `GameClock.Ticks` is not reset on entering the park: `GameClock.Update` is its only writer and `Rebase` leaves it,
  and it read 5 at the park on show, before any sweep - the lobby's 162 ms carried in.
- **The guard's 752 is gone on the first sweep** (idleSince 0 at tick 8); the guard stood through 757 and walked on 758,
  where the original walks on 763.
- **Every guard spell after a walk held the walk's stamp exactly 2 sweeps** (25 of 25, predicted 2; the original's
  11): tick > stamp + 10 first holds at stamp + 16. Nineteen ended there; six went on one or two sweeps at stamp 0,
  the random stay-put or no destination. They began on sweeps of every remainder mod 4 (5, 4, 12, 4).
- **Every researcher spell after a walk held its stamp exactly 3 sweeps** (25 of 25; the one the photographs cut is
  left out), 17 ending there. That the rest go on at stamp 0 was predicted only after the first run showed it.
- **The handyman and the mechanic stood from their saved walk's end** (mGameTick 761, stamp 48 for 2 sweeps, then 0)
  **to the end of the run, 620 sweeps (610 of them read)**, the entertainer from 768 (104, then 0); none walked again.
- Photographed, held by `pause` with `staff` read while paused: the guard standing on the path at (43.14, 28.34) on
  mGameTick 794, idle since tick 312, a paused control identical but for the advisor's mouth, and walking on from
  there on 798.

### Measured in the game after the build

`StaffBehaviour.Step`, `ThingRemoved`, a pickup and a put-down take `ParkState.GameTick`; the guard's walk-or-stay is
its `& 3`; nothing zeroes a stamp. `q82bmeasure.py`, silent, jungle, three runs of 648 to 650 sweeps (755 to about
1403), every sweep read (each photograph held by `pause`), predicted before the park loaded; `save/` unchanged in all
three.

- **The guard reads Idle since 752 on every sweep from 755 to 762 and Walking on 763**, in all three runs.
- **Every guard spell after a walk is stamped with its first sweep's mGameTick and holds it exactly 11 sweeps**: 28,
  19 and 21 spells, all 68 begun on a multiple of four and ended on stamp + 11 in a walk (no destination was ever
  missed). The guard set off from idle 29, 20 and 22 times, every one on a sweep that is 3 mod 4. A walk goes on,
  leg after leg, while its legs end on other remainders: the first run's first walk ran from 763 to 807 and stood on 808.
- **Every researcher spell after a walk holds its stamp exactly 21 sweeps**: 14, 17 and 15 spells; 6, 7 and 4 of them
  went on at stamp 0 first. The third run logged each stand's cause (`Staff: <id> stands on mGameTick <n>`): 20 of 20
  were the draw's low bits, over all four remainders of mGameTick (8, 3, 4, 5), and none was a destination not found.
  The guard's 22 stands in that run were all on a multiple of four.
- **The handyman and the mechanic are stamped 761 as their saved walks end, the entertainer 768 (its 712 kept until
  then)**, each held 11 sweeps and 0 after, standing to the run's end (Q133).
- Photographed, held by `pause` with `staff` read while paused and the camera not moved between shots: the guard
  standing at the corner of the path at (40.04, 28.03) on mGameTick 792, idle since 792, a paused control identical;
  turned to set off on 803; and walking at (39.48, 28.51) on 807. The first load came on mGameTick 1264, as Q68b's.

### Where OpenTPW differs

| What | The original | OpenTPW | Reached in Lost Kingdom |
|---|---|---|---|
| A hire's first decide | at once: the guard's on `mGameTick & 3` (`0x004d5e76`), the researcher's on a draw (`0x005026cb`) | Idle at stamp 0, decided on the next sweep | every guard or researcher hired (Q136) |
| The mechanic, the handyman and the entertainer with no work | walk about | stand, uncounted | from their saved walks' ends (Q133) |
| The researcher's fourth decide | researches, state `0xf` | stands | every fourth researcher decide (Q134) |
| Staff sounds | fourteen cat_staff effects | none, uncounted | every idle and walking turn; a performance's end; a guard's chase and catch (Q135) |
| Tired | the byte `<=` 1 | the float `<` 1 | a rest in [1, 2) (Q136) |
| Tired with no rest area found or reached | `FUN_00506a40` answers 0 and the kind's choice follows | the guard and the researcher stand and ask again after the idle wait | once the Staff Room at (58,16) is sold or cannot be routed to (Q136) |
| The end of a rest | the kind's decide in the same sweep | Idle at stamp 0, decided on the next sweep | every rest (Q136) |
| The patrol roll | path cells only | any cell | every roll (Q136) |
| Speed by rest | `+0xc0`, 60 to 140, one of `FUN_004fa870`'s three terms | none: staff are not eased and keep the saved `max_speed` (a guest's is, `Peep.Pace`) | every decide (Q136) |
| Thoughts `0x12` to `0x16` | shown | none, uncounted | tired, unhappy, very happy staff (Q110) |
| Strikes | `mStaffHQ`'s monthly flag, the strike walk, state 5's end | none, uncounted, the model-9 record unread | the monthly consideration every month the park is open; a strike only past the 24-month gate (Q138) |

## Spending — a guest pays on LEAVING

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004fd970` | — | The settle-up for leaving **any** visitable thing (not just a sideshow). Shifts the guest's recent-things history (`+0x1e0`..`+0x1e6`), bumps the guest's `mNumRides`, `mNumShops` or `mNumSideshows` by the descriptor's `+0x4ac`, charges, counts the visit on the object (`FUN_004e1690`), takes the descriptor's `+0xe8` (`FatigueEffect`) off the guest's `mTiredness` `+0x1b8` held to 0..100 (`0x004fd9e7`..`0x004fda00`; the constructor zeroes it and a load restores the saved one, file 521 (`0x004fc794`), nought on every shipped guest; nothing raises it, so it stays nought), then splits on `+0x1f1`, the win roll: nought logs `"Person lost this sideshow…"`, docks happiness at `+0x19c` and, at a sideshow, thinks thought 6 and pushes event `0x19` (`0x004fdc1c`..`0x004fdc3e`); otherwise it runs `FUN_004fe1e0`, then takes three times the change in happiness since the guest's snapshot at `+0x20c` (both truncated), logs it (`"Person %d: Happiness changed by %d since using object %d"`, `0x0075d6cc`), averages it into the object's satisfaction (`FUN_004e1e00`), posts it plus 50 to the park analyser for a shop or sideshow (`0x004fda1b`..`0x004fdb2c`) counts the object's served (`FUN_004e19f0`) and, at a sideshow, thinks thought 5 and pushes event `0x18` (`0x004fdb5d`..`0x004fdb7f`); happiness itself is not moved. Each step: "The settle-up's bookkeeping", below. | Its own strings |
| `FUN_004fe1a0` | — | **The charge.** `price = object[+0x194]`; when non-zero it credits the ride, plays a sound, and does `person[+0x1a0] -= price`. **The only `SUB [reg+0x1A0], reg` in the image.** | Byte search |
| `FUN_004e16b0` | — | **The economy feed**: first the bank's deposit, `FUN_004d0190( price )` (the balance, `0x004e16c6`), then `ride[+0x180] += price`, `ride[+0x70] += price`, then on the descriptor's `+0x4ac` — **1, a shop, credits the park analyser's `+0x20130`; 2, a sideshow, its `+0x20380`** (month accumulators, `FUN_00519510`, `0x004e170c`); a ride (0) credits neither and posts nothing. The shop arm then posts the price to the challenge manager as progress on challenge type 12 (shops' profit), the sideshow arm on 13 (sideshows'). Inside the shop arm a second switch on `+0x164` (`ShopType`, table `0x004e18e4`; then `+0x158` `SpecialIngredient` for types 2 and 4, table `0x004e18fc`) posts **1, not the money**, as progress on a selling challenge: ShopType 1 type 5 (gifts), 3 type 8 (meals), 5 type 7 (costumes), 6 type 6 (balloons), and by ingredient salt 1, fat 2, ice 3, sugar 4 (the Drinks Shop's "Sell 30 drinks"). A post lands only while a challenge of that type is on ("The settle-up's bookkeeping", the cost of goods). | Disassembly |
| `FUN_004d0600` | — | **The admission fee**, no argument (`RET`, `0x004d068d`): the fee is the bank's `mAdmissionFee` `+0x118` (`0x004d0609`). No gate, and no test of nought; the deposit's three adds (`+0xc`, the analyser's `+0x1fc90`, `+0x124`), then the analyser's month gate takings `+0x1fee0` (`0x004d0670`, the only add to it), then `FUN_004c7520` (`0x004d0686`): the analyser's `mLifetimeVisitors` `+0x21c08` +1, `mMostPaidForTicket` `+0x21c40` raised to the fee (signed), and a message of type `0x1a` (sent at `0x004c75d8`) whose one listener, the challenge manager, counts one toward a challenge of type 11, "Get 100 new visitors" (`FUN_004d2860`, `0x004d28bb`). One caller, the guest's opinions 2 and 3 (`0x004ffae5`). **The ride charge does not go through it.** | Disassembly |
| `+0x194` | `mPricePerUse` | File **1054**. | Save record |
| `+0x180` | `mTotalTakings` | File **1090**. | Save record |
| `+0x1a0` | `mCash` | **Runtime** offset on the guest. The file's `mCash` is at **414** — do not conflate. | `FUN_004fe1a0` subtracts from it, `FUN_004fde50` compares against it |
| `+0x1e0` | `mPreviousRides[4]` | The recent-things history, shifted by three (four entries, not three). | Save reader |
| `+0xe8` | `FatigueEffect` | Descriptor field, 5 in each theme's Rides, Shops and SideShow `.sam` ("reduce fatigue value by this amount (peep gets MORE tired!)") and in jungle's `burger.wad` `Burger.sam`; no feature declares it, so a toilet's is the category parse's nought. Subtracted from the guest's `mTiredness` at the settle-up (`FCHS`, `0x004fd9fb`). | Key table row `0x00745904`, between `InitPricePerUse` (`+0xe4`) and `InitChanceOfLoosing` (`+0xec`) |
| `+0x4ac` | — | Descriptor field: object kind, **a copy of `+0x4c`, `Info.WhichUIType`** (`0x004134f1`..`0x004134f5`, in `FUN_00413410`, its only store): 0 rides, 1 shops, 2 sideshows, 3 features. `FUN_004fde50`'s and `FUN_004e16b0`'s arm 1 read `SpecialIngredient`, `AppearanceEffect` and `ShopType`. | Disassembly |
| `+0x164` | — | Descriptor field: `UsageInfo.ShopType`, which `FUN_004e16b0`'s shop arm switches on to post 1 as progress on a selling challenge (the `FUN_004e16b0` row above; the schema's order puts it there). | Disassembly |

**`person[+0x1a0]` is the guest's cash — confirmed by USE, not by adjacency.** The field a price is SUBTRACTED from in `FUN_004fe1a0` is the field a price is COMPARED against in `FUN_004fde50`, by two unrelated functions. `+0x19c` is happiness and `+0x1a0` adjoins it, but adjacency was never the evidence.

**A charge is deposited in the park's bank.** `FUN_004e16b0` first calls `FUN_004d0190` on the bank thing with the price (`0x004e16bf`..`0x004e16c6`): `mBalance` at `+0xc`, the park analyser's month cash-in `+0x1fc90` and the bank's `mProfitThisYear` `+0x124`, the adds the gate fee's `FUN_004d0600` makes too; it has no gate, refuses nothing (its size check is handed to the bare `RET`) and does not write `mLastBalance`. Then it credits the object's `mTotalTakings` `+0x180` and today's takings `+0x70` and the analyser's shop or sideshow accumulator (`+0x20130`, `+0x20380`; a ride neither). OpenTPW makes the deposit: `ParkState.TakeAt` calls `ParkState.Deposit` (Q96); the analyser's totals are counted.

### Measured prices and takings in Lost Kingdom

Drinks Shop **30**, Jungle Spray sideshow **20**, **Belly Bounce zero**; `mTotalTakings` nought on every object. The charge is gated on `price != 0`, so it never fires for the park's only ride.

## The effects of a visit — `FUN_004fe1e0`

Named by its own strings: `"Litter gone up by %d, is now %d"`, `"Customer bought a balloon.  Aaah."`, `"Trying to give a balloon to a pe…"`, `"Customer returning a costume."`, `"Balance file error: Shop has unk…"`, `"Sideshow won - happiness up %d p…"`. What it does, in order:

1. **A sideshow (`+0x4ac` == 2) PAYS OUT:** `FUN_004e1a10` — the **cost of goods**, not the chance of winning — feeds `FUN_004e1920` (`0x004fe225`: the cost booked against the object and debited from the park's balance, "The settle-up's bookkeeping"; OpenTPW books it, and the shop's, through `ParkState.BookCostOfGoods`), and then **`person[+0x1a0] += FUN_004e1a10()`** — a prize ADDED to the guest's cash. A shop (`+0x4ac` == 1) books `FUN_004e1b40`, its cost of goods scaled by its quality and ingredient settings, instead (`0x004fe251`), and pays nobody. **In Lost Kingdom that prize is 50 against a price of 20**, so winning the Jungle Spray leaves a guest 30 up and the park 30 down.
2. **The excitement match**, `FUN_004fdcc0( object )` at `0x004fe259`: how the thing's excitement suited the guest's kind
   moves their happiness, and the excitement makes them sick by how little hungry they are ("The excitement match",
   below).
3. **The item's own effects**, each clamped 0..100: the descriptor's `+0x144` taken from thirst `+0x1a4` and `+0x148` from hunger `+0x1a8` (`FCHS` at `0x004fe26d`, `0x004fe2b7`; with a sound of `0x83` or `0x84` depending which is larger), then added: `+0x14c` → `+0x1b0`, `+0x150` → happiness `+0x19c`, `+0x154` → litter `+0x1b4`. Three more happiness changes follow, each reading the object's byte `+0x198`, `mAmountOfSpecialIngredient` by the object reader's own name (`0x004dc601`, string `0x0075b45c`; saved at file 1058): for the hunger effect `+0x148` and then, independently, the thirst effect `+0x144`, each when it is non-zero, one draw `r` of the park's generator (`FUN_00516330`, even when the dock cannot fire) and `(r & 7) + byte [+0x198] + that effect` under 30, unsigned, docks `PeepInfo.SmallHappinessChange` (`0x004fe453`, `0x004fe4a5`), so a shop with both effects takes two draws and can be docked twice; then happiness gains `byte [+0x198] * desc[+0x150] / 100`, truncated toward nought and held to 0..100 (`0x004fe4cf`..`0x004fe525`). These run after all five effects and their log. The gain runs for every object, a ride's nought effect included, so it holds happiness to 0..100 there too. At the stock amount 50 a drink cannot be docked (50 + 40) and gains 2 more happiness (measured in the original: +7 in all). OpenTPW builds the docks and the gain (`ParkRideOperation.TakeTheIngredient`, Q177d), drawing from the ride turn's generator where the original draws the park's one.
3b. **The special ingredient**, a switch on the descriptor's `+0x158` (`0x004fe527`, table `0x004fe8e8`), each by the
   same byte: 1 (fat) adds it to the toilet need `+0x1ac`, 2 (salt) to thirst, 3 (ice) adds `byte * ThirstEffect / 100`
   to thirst, each held 0..100, and 4 (sugar) `byte * 6 / 100` to the guest's `mAdjustorSpeed`, the word `+0xc4` (person
   file 32), with **no clamp** (`0x004fe60e`). `mAdjustorSpeed` joins the walking speed, (`mBaseSpeed` + the hurry speed +
   it) / 100 eased a quarter of the way each sweep, and loses one a sweep below a hundred (`FUN_004fa870`, "Where a
   WALKING peep is drawn"): at the stock amount it is 3, gone in three sweeps, and it lifts a guest settled at base 120
   from a mover speed of 15728 to 15826, 15867 (the peak, 0.9% up), 15865, then back over about 24 sweeps, six
   seconds. The key's bounds are `[0, 6)` (`0x00745fb8`) and the switch skips anything above 4, unsigned (`0x004fe530`),
   so 0 (the table's first entry goes to the switch's end, `0x004fe615`) and 5, the one legal value above 4, do
   nothing. The Drinks Shop is ice: a drink takes 40 thirst, held at nought, and
   gives 20 back at the stock amount (measured in the original: 36 to 20). In Lost Kingdom every arm is a jungle shop:
   fat the Burger Shop (1207), salt the Fries Shop (1212), ice the Drinks Shop (1203), sugar the Ice Cream Shop (1206);
   the Steak Restaurant's file comment says fat and its value is nought. OpenTPW builds this and the `+0x198` terms
   above (`ParkRideOperation.TakeTheIngredient`, `Peep.Pace`; Q177d).
4. **Shop arms on the descriptor's `+0x15c`, `AppearanceEffect`:** nought does nothing; any value but 0, 1 or 2 logs a
   balance-file error (`0x0075d7b8`) into the bare `RET` and does nothing more (`0x004fe615`..`0x004fe63d`). Both arms
   draw the park's generator through `FUN_00541f70` (`0x004fe652`, `0x004fe6aa`, `0x004fe703`), and the balloon arm also
   through `FUN_00541fd0` (`0x004fe716`), each taking its `ECX` from the world's other pointer, `[0x007cf83c]`; the
   balloon arm and a costume's return reseed it with the guest's id first, the giving of a costume does not.
   - **1, a BALLOON** (`0x004fe6ba`..`0x004fe78a`): a balloon in the guest's own colour and a life from the shop's
     quality; see "A held balloon", below. OpenTPW builds it (`ParkRideOperation.GiveABalloon`, Q177e).
   - **2, a COSTUME** (`0x004fe642`..`0x004fe6b5`): the guest's picture is changed to the theme's costume, or given back;
     see "A costume", below. OpenTPW builds it (`ParkRideOperation.DressOrUndress`, Q177f).
5. **A toilet (`mFlags & 1`)**, in order (`0x004fe78f`..`0x004fe7fb`): dirties the toilet by the need the guest brought
   (`FUN_004e2440` with the need's byte: the State of repair `+0x44` falls by 0.05 of it, held to 0..100, and on falling
   below 25 - "Toilet has become dirty and smelly" - unstamps `RegionFX` 1 around the toilet's cell and stamps 6, each
   over the effect's radius; in the online game, mode 1, there is no dirtying, and a toilet already below 25 is cleaned
   instead: back to 100, 6 unstamped and 1 stamped, `+0x5e` and `+0x64` zeroed, `0x004e24bc`..`0x004e252a`); zeroes the
   toilet need `+0x1ac` (`0x004fe7b6`); pushes event `0x11`; zeroes illness `+0x1b0` when its byte is above 90, with
   event `0x12` (`0x004fe7dc`..`0x004fe7ef`); and sets `+0xc2`, the hurry speed, to 25 (`0x0075c7f2`, `0x004fe7f5`),
   which the needs turn sets back from the need within four sweeps. Event `0x11` names the toilet; `0x12` names nothing
   and is pushed before illness is emptied. The online branch also sets the toilet's script variable 8 to nought
   (`0x004e2517`). OpenTPW builds the need, the illness and the speed, and counts the dirtying and both events
   (`ParkRideOperation.UseTheToilet`, Q170b).
6. **Then, for a sideshow only:** `person[+0x1d0] += 1` and a happiness rise computed from **`log2( costOfGoods / pricePerUse )`** - `FUN_004e1a10` (`+0x188`, cost of goods) over `FUN_004e1a00` (`+0x194`, price), `FILD`/`FIDIV` at `0x004fe835`/`0x004fe84b`, the logarithm by `FYL2X` over `ln 2` - scaled by the byte at `DAT_0078505c` (`PeepInfo.MediumHappinessChange`), and logged as `"Sideshow won - happiness up %d points to %d"`.

**The signs are not uniform**, and the decompile shows it: `FUN_004fe1e0` does `-(float)desc + meter` for thirst and hunger but `+(float)desc + meter` for vomit, happiness and litter. **Deduct two, add three** — which is exactly what the balance file's own comment column says.

### A held balloon

Decoded first-hand and put to an adversarial check (Q177e, every claim upheld or amended at its address); measured in
Alexah's two played Lost Kingdom saves, written by the original, which hold 29 and 28 balloons.

1. **The arm** (`0x004fe6ba`..`0x004fe78a`, `ESI` the guest, `EBX` the shop): a log (`"Customer bought a balloon.  Aaah."`)
   and the assert that `mBalloonScript` is nought (`"Trying to give a balloon to a person who already has one!"`), both
   into the bare `RET`; the park's generator reseeded with the guest's id word (`FUN_0050b350`, `FUN_00516370`,
   `0x004fe6fc`); `FUN_00541f70( 10 )` for the bank, `(r >> 2) %` the kind's bank count, one; `FUN_00541fd0( 10, bank )`
   for the set, `(r >> 2) %` the bank's count of sets with pictures (`+0x21e`, four); a sprite made by
   `FUN_00475a10( 0x0074f480, 10, bank, set, 0.0, 0.0, 0.0 )`, its one-based slot in the table `DAT_007b49f0` kept in
   `mBalloonScript` `+0x210`; `mRemainingBalloonLife` `+0x214` = the object's `mQualityOfGoods` byte (`+0x18c`) × 255 /
   100, unsigned and truncated, held to 25..255 (`DAT_0075d0f0`, `DAT_0075d0f4`), so 127 for a bought shop; event `0xc`
   naming the shop (`0x004fe775`). **So a balloon's colour is its guest's id and nothing else**: predicted from the id,
   all 29 and 28 saved balloons match, balloons rebuilt after rides among them; the first draw instead matches 10 and
   an id one higher 4, near the 7.25 of chance.
2. **The bank.** One in the whole game, `Generic\Balloons\SPR_BL`, kind 10 ("balloons", `0x00764090`): four sets of two
   frames and no directions, red, green, blue and yellow (sets 0 to 3), frame 0 the whole balloon and frame 1 the same
   colour burst. Every picture's top is 73 to 79 pixels above its anchor, so a balloon floats above its guest's head
   (FileFormats `sprites.md`).
3. **Drawn with its guest every frame** (`FUN_004fa030`, called for a model-1 thing holding one, `0x004fa184`; from the
   per-frame driver `FUN_00518f90` at `0x00519012`, and from two more callers: the state setter's case `0xf`
   (`0x00501fc5`, `t` nought, when the left thing's descriptor `+0x100` is set) and the constructor (`0x004fb265`)). The
   guest is sampled twice by `FUN_004f9f00`: at the frame's fraction of the sweep, held to 0..1, and 0.35 of a sweep
   earlier (`0x0075c910`), held to -1..2, each `prev + (cur - prev) × t` truncated by `__ftol` and scaled by 10 over
   65536. `FUN_004fe900` puts the sprite at the guest's `mLastPosX`/`mLastPosY` (`+0x218`, `+0x21c`, file 430 and 434),
   last frame's trailing sample, and stores this frame's there: the balloon trails a frame and a third of a sweep. Its
   `+0x8c`, the height above the ground under it, is `1.0 - (|dx| + |dz|) / 0.5 + 1.5 × bob`, the gap between the two
   samples, unheld, so a walking guest's hangs lower (the saves: standing holders 1.00 to 1.30, walkers down to -1.2).
   The bob is the middle float of twenty rows at `0x0075c810` (0 up to 0.2 in steps of 0.02 and back, the last
   nought; the other two floats nought in every row), row `(id + P) % 20`; the phase `P` (`0x007cedd4`) steps once every
   eleven placements of any balloon (`0x007cedd8` counts 0.1 to 1.0, `0x004fa244`..`0x004fa28b`), shared by the whole
   park and never reset. The draw (`FUN_00475430` → `FUN_00542010`) puts it at the ground plus `+0x8c` at the alpha
   byte `+0xa0`, once its script has shown a frame (`+0x114`).
4. **It lives on the needs sweeps.** In `FUN_00501650`'s `(id & 3)` block (`0x005018f8`..`0x00501949`): outside states 16
   and 17 and on a cell `FUN_004fa990` passes (the runtime cell's `mType` under the thing's own cell bytes: 0, 1, 3, 9
   or 10), one draw of the generator, a tenth of which picks a thought (`FUN_004fc8a0`), then the life down by one,
   unsigned, whether or not a balloon is showing, and at nought `FUN_004fe950`. At a sweep in four, about 25 s, 2 min
   or 4 min at quality 0, 50 or 100 of qualifying time. The shipped park's guests all stand on the gateway's approach,
   cells of type 30, which do not count.
5. **Let go** (`FUN_004fe950`, whose callers are the countdown, the prank, the state setter's case `0x11` and the
   person-hide `FUN_004f9ed0`): the same sprite put on the script at `0x0074f4c0` by `FUN_00475b80`, which keeps its
   place, colour, alpha and due time, and `mBalloonScript` nought; the life is left. The script (words 1666, then
   1654..1664): the alpha to 250, then frame 1 and the alpha down by 20 while it is still nought or more
   (`0x004763d0`'s comparison 8, signed), then the end word `0x005da3c0`, which hides it; the next due turn frees it. So
   **it bursts where it was, thirteen turns from alpha 250 down to 10, and neither rises nor drifts**. Until its next
   turn it keeps showing the whole balloon, unplaced.
6. **Boarding and leaving.** Boarding anything (state `0x10`, `0x00502156`) deletes the sprite with no burst
   (`FUN_00475550`) and keeps the life. Leaving (state `0xf`, `0x00501fd3`..`0x0050208a`), with life left and the thing
   left (`mMajorDest`'s descriptor) not a balloon shop, builds it again, reseeded, so in the same colour, with no event
   and the life as it was; the assert that none is held goes to the bare `RET`. The giving (the settle-up's arm,
   `0x005015f4`) and this rebuild (`0x005015fd`, straight after) run only on ExitRide's routable path
   (`0x005015e1`..`0x005015ef`); boarding is admission's (`0x005008ef`, `0x00501bc7`), not ExitRide's. A rider put off by a sale (`FUN_004fb360`) goes to state 6
   and gets none back until they next leave a thing.
7. **Leaving the park does not burst it.** The guest keeps it through states `0x12` to `0x15` and is deleted at the bus
   (`FUN_00500bd0` → `FUN_0050b780` → `FUN_004fb330`), which deletes the sprite outright. A guard's catch
   (`FUN_004feb10`) deletes it too, before state `0x11`, whose own let-go then finds none.
8. **The prankster** (`FUN_004fec90`, arm 102): a guest whose `mPrankeryIndex` is 102 (100 by the `PrankeryLikelihood`
   roll at construction, the second of two draws, plus `id % 3`), below 15 happiness, lets go the balloon of the first
   other guest on its cell who holds one (`0x004ff156`..`0x004ff1f7`). The life is left, so the victim gets it back on
   leaving their next thing.
9. **A save keeps it**: `mBalloonScript` and `mRemainingBalloonLife` are read back raw, and the table is rebuilt slot for
   slot (`FUN_00475730`), so the slot still names the balloon. Every saved balloon is kind 10, bank 0, frame 0, alpha 255,
   on script 1650 at 1652.
10. **What reads it.** Case 9 of the challenge check `FUN_004d1660` counts the guests on counting cells who hold one
    against all of them (`FUN_004c9530` over `FUN_004c9130`); case 10 counts costumes the same way. Lost Kingdom's
    `ChallengesInThisLevel[7]` has `ChallengeType` 9; that the file's numbers are the switch's is not checked. Nothing
    reads it for happiness or a need.

**OpenTPW builds it** (`Balloon`, `ParkRideOperation.GiveABalloon` and `Dismiss`, `Peep.SetState` and `Peep.Tick`,
`ParkPeople`, `ParkGuestSprites.DrawBalloons`), counting the event (`SETTLE_UP_BALLOON_EVENT`) and the thought picker's
draw (`NEEDS_THOUGHT_PICKER`). Its departures: no shared generator, so the reseed's effect on the park's later draws
is not reproduced (`ParkGenerator`); the bob's phase counts placements on the frame clock at 30 a second rather than
per rendered frame; the two other callers of the placement are not reproduced; whether the per-frame placement runs
while a park's menu has paused the clock is not traced; a saved balloon's first turn is one interval after the load,
as a person's is; a park saved while a balloon bursts loses the burst at load, as no guest names it any more and its
loop stack is not read (none of the 79 balloons in Alexah's four played saves that hold any is bursting). No prank
and no challenge exists here to read it.

### A costume

Decoded and checked with the balloon (Q177e); built by Q177f.

1. **Giving** (`0x004fe642`..`0x004fe672`): a guest whose `mESPSprite` `+0x24` is not exactly 2 gets 2 and `mSpriteID`
   `+0x20` = `FUN_00541f70( 2 )`, `(r >> 2) %` the theme's costume banks, with no reseed; event `0xb` naming the shop.
   Lost Kingdom has one costume bank, `Jungle\Costumes\SPR_TI`, so it is always 0 and the draw is taken all the same.
2. **Returning** (`0x004fe674`..`0x004fe6b5`, "Customer returning a costume."): `mESPSprite` 0, the generator reseeded with
   the guest's id, `mSpriteID` = `FUN_00541f70( 0 )`; no event. The arrival (`FUN_004faec0`, `0x004fb18d`..`0x004fb1bc`)
   rolls the same way, so the return gives the arrival's child back while the count of kid banks is unchanged.
3. **The count of kid banks is capped by the detail level**: `Sprites_LoadFolder` loads the four avatars first (`SPR_BI`,
   `SPR_KI`, `SPR_TA`, `SPR_SU`, `0x00764030`), then the rest by name, and stops at `FUN_0041a9d0`'s cap: 2, 4, 6 or 8
   for `GameOptions.NUMKIDS` (`DAT_007858d0`) 0, 1, 2 or more. `low.sam` sets 0 and `med.sam` and `high.sam` 2, so the
   default game has **six** (`BI, KI, TA, SU, BE, CH`); the `.sam` files' own comment ("0->4, 1->6, 2->8") is wrong.
   Measured through the person base's `mSpriteID` (file 246): the shipped park's 13 children fit the roll over 8, every
   one of Alexah's played saves' 296 and 53 over 6. A load reduces `mSpriteID` and each sprite's bank modulo the count
   again (`0x004f93a6`, `FUN_00475f40`), so the shipped park's guests 33, 35 and 29, saved on banks 6, 7 and 7, come in
   on 0, 1 and 1.
4. **When it shows.** The picture changes at state `0xf` on leaving, which builds the sprite from `+0x24`/`+0x20` when its
   handle `+0xc` is nought (`FUN_004d4140`); every costume shop's `RideHandlesSprite` is 0, so boarding freed it. Nothing
   else changes: the walk's scripts are the same for any kind.
5. **The heads.** `FUN_0044b410` puts a head on a ride's node. For five of its six callers (`ADDHEAD`, `WALKON` action
   4, `COAST` cars, the `BUMP` arm `FUN_00549c60` reached from `BUMP` 4 and 12, `TOUR` cars) it asks `FUN_004fcac0` of
   the rider: a costume head (kind 3) for a guest in costume, else the kid head of the same index (kind 1). The sixth,
   the bumper family's re-show `FUN_00548e80`, passes handle 0 (`0x00548ffe`) and draws kid head 0.

**OpenTPW builds it** (Q177f): `ParkSpriteBanks` counts the kid banks under the detail file's cap, the theme's costumes and
the balloon's colours as a park loads (`Level`, from the file the particles' density comes from); an arrival is the
child its id gives (`ParkPeople.Admit`, `ParkSpriteBanks.ChildOf`); a load reduces a saved child; the settle-up dresses and
undresses (`ParkRideOperation.DressOrUndress`), counting event `0xb` (`SETTLE_UP_COSTUME_EVENT`); and a guest is drawn in
`Peep.SpriteKind` and `SpriteBank` (`ParkGuestSprites`), every child and costume bank packed. Its departures: the costume's
draw is the ride turn's generator; the picture changes at the settle-up, which the original's case `0xf` follows straight
after, where OpenTPW never hides a rider at all (Q52); and the only heads drawn here are a bumper boat's riders'
(Q179b, `ParkGuestSprites.DrawHead`: the head of the rider's child bank, or their costume's), while `ADDHEAD`'s wait on
Q190.

**What the head is.** `FUN_0044b410` makes a world sprite (`FUN_00475a10( 0x0074f558, kind, bank )`) whose script is
`SETSET 0`, local 13 = 0, local 16 = `0x3000080`, then a loop of `FRAME` local 13: set 0, frame 0 of a `Kidsheads` (kind
1) or `Costumeheads` (kind 3) bank, seven directions. **The body:** admission to a thing with flag `0x20` sets the guest's
`+0x28` to 1 and asks their sprite for the standing script (`0x00502136`, `FUN_004217f0( 3 )`), and the guest draw
places the body only while `+0x28` is nought (`FUN_004fa030`), so the original leaves it standing where it boarded.
OpenTPW draws no body for a boat's rider, by Alexah's account of the game (2026-09-30); which way an attached head faces
is not decoded, and here it is the boat's heading. The `0x4000` custom-detail path is not reached: OpenTPW has only the three detail files. The
staff folders' cap from the same key (`FUN_0041aa40`: one bank at `NUMKIDS` 0, else two; only the mechanics have two) is
built with it, and a staff member's saved bank is brought within it as they are drawn.

### The excitement match — `FUN_004fdcc0`

Decoded first-hand and put to three refuters (Q169), every step at its address. It runs behind the settle-up's gate on
`+0x1f1` (`FUN_004fd970`, `0x004fda05`..`0x004fda16`), so only when that byte, the win roll, is non-zero ("`+0x1f1` at the settle-up is the win roll", below), and before the item's effects, so it reads the guest's hunger as they came off.

1. **No excitement, nothing.** `FUN_004e0860( 0 )` on the object, the thing's excitement (the ride score's own reading,
   `ParkRideScore.ExcitementOf`); a low byte of nought returns at once (`0x004fdcd6`), neither half run.
2. **Happiness by the gap.** The gap is | the kind's `PeepTypes[k].PreferredExcitement` (the byte at `0x007850e4 + 12 x
   kind`, the kind the guest's byte `+0x1f0`) − the excitement's low byte |, as an int. Under 5 it adds
   `PeepInfo.PerfectRide`, under 15 `GoodRide`, under 40 `OKRide`, each a whole int read by `FILD dword`
   (`0x004fdd17`, `0x004fdd24`, `0x004fdd67`); from 40 on it adds nothing (`0x004fdd65`). Happiness (`+0x19c`) is then
   held to 0..100: above 100.0 it is 100, below 0.0 it is 0 (the floats at `0x0070072c`, `0x00700730`).
3. **Sickness by the stomach**, whatever the gap: `vomit += ((100 - (trunc( hunger ) & 0xff)) / 20) x ((excitement & 0xff) /
   PeepInfo.RideVomitDivisor)`, every division a signed whole-number one (`IDIV` at `0x004fddc5`, the `/20` by the
   `0x66666667` multiply at `0x004fddda`) and the hunger (`+0x1a8`) truncated by `__ftol` (`0x0067a830`); the illness meter
   (`+0x1b0`, OpenTPW's `Peep.Vomit`; the game's log calls it illness) is held to 0..100 the same way. Hunger rises with
   time and food takes it away, so a guest who is not hungry at all (under 1) counts five times, a hunger from 1 to 20
   four, and a hungry one of 81 or more not at all: a full stomach is the sick one.

**The four keys sit where the executable's table puts them, not where the file lists them.** `PeepInfo`'s table at
`0x0073fc70` runs `...BigHappinessChange, PerfectRide, GoodRide, OKRide, RideVomitDivisor, ToiletDesparate...`, so by the
slot rule (`park-engine.md`, "How a key finds its global") they are the type-5 ints at `0x00785064`, `0x00785068`,
`0x0078506c` and `0x00785070`, and `FUN_004fdcc0` is their only reader. `data/levels/Standard.sam` sets 25, 15, 5 and
10 (`Online_Standard.sam` 25, 10, 5, 10); nothing Lost Kingdom loads offline overrides them. An absent key reads nought, so
a divisor of nought would fault the `IDIV`.

**What the park shows.** The Belly Bounce is 40 and the Inca Totem 70 against kinds liking 80, 65, 50, 35, 65, 80, 45
and 80: on the Totem a kind liking 80 is ten off (+15) and a kind 3 thirty-five (+5); on the Belly Bounce a kind 3 is five
off (+15, not the perfect 25) and a kind liking 80 forty (nothing). The Totem's 70 over 10 is 7. The original's arrival draws its hunger
`% 50` (`FUN_004faec0`, `0x004fb0c5`), so a ride there at that hunger adds 35, 28, 21 or 14, most often 28 or 21;
OpenTPW's arrival comes in at hunger nought (Q85) and takes 35. In both, the quarter of guests whose id divides by four
grow hungrier as they go (`FUN_00501650`, `Peep.Tick`) and so take less. A shop declares no excitement, so no shipped thing has both an
excitement and a hunger effect, and the order against the effects cannot show.

**OpenTPW builds it** (`ParkRideOperation.MatchTheExcitement`, the keys on `ParkAdmission`, the likings from the park's
`ParkRideScore`), and logs each match. Its departures: an absent divisor is held at one where the engine would fault, and
the excitement is worked out once where the engine asks three times (the same answer, since nothing it reads moves).
And it reads `ExcitementOf`, so that function's departures in "Where OpenTPW differs" (Q97, Q172) reach it too.

### The effect block, descriptor field to guest meter

Each row is pinned by **the meter it writes**, not by key order, so the mapping does not assume the `.sam` and the struct share a layout.

| Descriptor | `.sam` key | Guest meter | Drinks Shop value and the file's own comment |
|---|---|---|---|
| `+0x144` | `UsageInfo.ThirstEffect` | `+0x1a4` thirst | 40, "How much thirst to deduct" |
| `+0x148` | `UsageInfo.HungerEffect` | `+0x1a8` hunger | 0, "to deduct" |
| `+0x14c` | `UsageInfo.VomitEffect` | `+0x1b0` illness | 10, "How much vomit to add" |
| `+0x150` | `UsageInfo.HappinessEffect` | `+0x19c` happiness | 5, "to add" |
| `+0x154` | `UsageInfo.LitterEffect` | `+0x1b4` litter | 50, "to add" |

**Exactly eight items declare the block, and all eight are shops**, by `Info.Id`: Balloon 1209, Burger 1207, Coconut 1203 (the Drinks Shop), Cost_shp 1202, Fries 1212, GiftShop 1211, IceCream 1206, Steak 1208. Of the six objects Lost Kingdom offers, only the Drinks Shop (Coconut 1203) declares it; the Belly Bounce 1100, the Jungle Spray 1303 and the toilets declare none, and neither do the rides or the other sideshows. `Junspray.sam`'s `UsageInfo` runs price, cost, chance-of-losing, excitement, stand/appear positions, sprite handling and anim count, and stops.

**Where the numbers live, because this has caused a false reading:** `shops/Shops.sam` declares all five keys at **5** and is the CATEGORY DEFAULT; each item's override lives in the `.sam` **inside its own `.wad`**, invisible to any grep of the installed game folder. So a shop carries the block twice over, inherited and overridden. `Rides.sam` and `SideShow.sam` declare none, so a ride reading **0** for an effect is a real fallback rather than a coincidence.

**In Lost Kingdom the effects arm pays out at the Drinks Shop alone**: it is the one offerable object that declares the block, and it is offered because its queue room comes from the map walk, not its saved `mQueueSizeInCells` of 0 (see "Faithful, not defects").

### Easy-mode overrides

In `rides`, **twelve of thirteen** `Easy_*.sam` files carry real content — `Easy_Bouncy.sam` is `Upgrades[0..2].WearRate` 3/2/1 plus two `CostOfResearch` 0 — and only `Easy_mystery.sam` is empty. The shops' and sideshow's `Easy_*.sam` files are one line, `# empty`.

### The sideshow win roll

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004e2670` | — | Reached from `FUN_00501db0` case `0xe` (entering `EnteringRide`). Asserts `"Non sideshow object number %d ha[s]…"` (descriptor `+0x4ac` == 2), reads a chance-of-winning byte at **`+0x190`** (decimal 400), draws **`r = FUN_00516330() % 100`** from the park's own generator (`0x004e26c6`, the world's `mRandomSeed`, not the C library's `rand()`), wins when **`chance >= r`** (`CMP`/`SBB`/`INC` at `0x004e26e2`), so a chance below 100 wins (chance + 1) times in 100, writes the result into script variable **11 (`VAR_PARAM`)**, and returns it. One draw for every admission to any object. | Its own assert |
| `+0x190` | `mChanceOfWinning` | **It is the OBJECT's, and it is saved and loaded with the object** (`FUN_004db7d0`, beside `mCostOfGoods` at `+0x188`, `0x004dcd01`..; file 1050); two setters (`FUN_004e1a20`, `FUN_004e21c0`) are reached from the object window. Placing one, `FUN_004db090` derives it as `100 - descriptor[+0xec]` at `004db38f`..`004db3a1` — `MOV EDX,[EDI+0xec]` / `MOV ECX,0x64` / `SUB ECX,EDX` / `MOV [ESI+0x190],ECX` — where `+0xec` is `UsageInfo.InitChanceOfLoosing`. That `FUN_004e2670` takes both the catalogue id (`+0xe`) and the script handle (`+0x24`) off the same pointer is what fixes it as the object rather than the person. OpenTPW reads the item's figure instead (Q97). | Disassembly |
| `FUN_004e1a10` | `mCostOfGoods` | **Not the chance-of-winning accessor.** It is two instructions, `MOV EAX,[ECX+0x188]; RET`, on the OBJECT. `FUN_004db090` builds `+0x188` from the descriptor's `+0x140`, which is `UsageInfo.InitCostOfGoods`. It is the sideshow's **prize** and the numerator of what winning is worth. The chance of winning is `+0x190`, reached by `FUN_004e21b0`. | Disassembly, 2026-09-20 |
| `FUN_004e1a00` | `mPricePerUse` | `MOV EAX,[ECX+0x194]`. The divisor in the happiness sum below. | Disassembly |
| `FUN_004e21b0` | — | `MOV AL,[ECX+0x190]` — the real chance-of-winning accessor. | Disassembly |
| `UsageInfo.InitChanceOfLoosing` | — | **VERIFIED as the source behind `mChanceOfWinning`, with a per-item override:** `sideshow/SideShow.sam` declares **70** as the category default and **`Junspray.sam`, inside `junspray.wad`, overrides it to 75** — so the Jungle Spray's chance of winning is **25**. A grep of the installed folder cannot see that, because an item's overrides live in the `.sam` inside its own `.wad`. **Nothing in `shops` or `rides` declares the key at all, so their chance of winning is 100 and their roll never fails** — which is the whole reason a shop always serves, and why `FUN_004e2670`'s assert reads "sideshow **or** this value is `'d'`" (decimal 100). (`Loosing` is the game's own spelling.) | `.sam` sweep + `FUN_004db090` |

### `+0x1f1` at the settle-up is the win roll

The save reader names the byte `mQueuePos`, and while a guest queues it is their place. **By the settle-up it is the
win roll, for every kind of object** (Q177's critic, re-read): `SetState(0x10)` has two callers, the state-14 turn
(`0x00501bc7`) and the forced boarding `FUN_00500870` (`0x005008ef`), which runs only on a head already in raw state 14
(`0x004e04b5`), so every rider has been through `SetState(0xe)`, which stores `FUN_004e2670`'s roll (`0x00501f41`).
The settle-up's one caller is ExitRide (`0x005015f4`), whose one caller is Dismiss (`0x004e14b5`), and the byte's other
writers are queue-side (`FUN_00501160` at `0x004ffdad`, `0x00500532`, `0x00500826`) or send the guest to state 6
(`FUN_005012f0`). Every object but a sideshow carries a chance of 100 (the item declares no `InitChanceOfLoosing`), so a
ride, a shop or a toilet always takes the effects arm; a sideshow's player takes it when they won. Measured in Alexah's
played jungle park: all 73 riders at non-sideshows hold 1, and the sideshows set to 55 to 58 served 58 winners of 98
customers over 30 days, against about 57 expected; in the original's stock park every spray play that went down the
nought arm was a loss by its cash and its thought ("The settle-up's bookkeeping", below).

**The ordinary queue flow writes `mQueuePos` in `FUN_00501160`** (`0x005011cb`), which runs on joining (`0x004ffdad`), on every re-take from the `InQueue` turn (`0x00500532`) and at a refused door (`0x00500826`). Join (`FUN_004ddb90`), leave (`FUN_004ddd20`), the guest-side completion (`FUN_00500870`) and the state-12 shuffle write none; `FUN_005012f0` zeroes it on the way out.

### The guest record, as named by the game's own save reader

`FUN_004fb530` (person base `FUN_004f8b10`) is the save reader that names the struct — the strongest provenance available, and the method that cracked `mQNext`.

| Offset | Name |
|---|---|
| `+0x19c` | happiness |
| `+0x1a0` | `mCash` (file 414) |
| `+0x1a4` | thirst |
| `+0x1a8` | hunger |
| `+0x1ac` | zeroed by the toilet arm |
| `+0x1b0` | illness (the balance file calls it "vomit") |
| `+0x1b4` | litter |
| `+0x1c4` / `+0x1c8` / `+0x1cc` | `mNumRides`, `mNumShops`, `mNumSideshows` (file 444, 448, 452): the visitor window's "Rides ridden", "Purchases made", "Sideshows played" |
| `+0x1d0` | `mNumSideshowsWon` (file 456): "Sideshows won" |
| `+0x1dc` | `mMajorDest` (file 442) |
| `+0x1de` | `mSavedMajorDest` (file 499): the major the minor decision switched away from |
| `+0x1e0` | `mPreviousRides[4]` |
| `+0x1f0` | `mPersonType` |
| `+0x1f1` | `mQueuePos` (file 494) |
| `+0x1f4` | `mQueueMoveDelay` (file 490) |
| `+0x208` | `mTimeOfLastSpotAnim` |
| `+0x20c` | happiness at the queue's join, a float; not saved ("The settle-up's bookkeeping") |
| `+0x210` | `mBalloonScript` (file 406): the balloon's one-based slot in the sprite table, nought for none ("A held balloon") |
| `+0x214` | `mRemainingBalloonLife` (file 495) |
| `+0x218` / `+0x21c` | `mLastPosX`, `mLastPosY` (file 430, 434): where the held balloon goes next frame, in world units |
| `+0x220` | `mState` |
| `+0x224` | `mSavedState` |
| `+0x20` / `+0x24` | `mSpriteID`, the bank of that kind, and `mESPSprite`, the sprite kind (0 a child, 2 a costume) ("A costume") |
| `+0x30` | the history block: `mLastThought`, the event ring from `+0x34`, the bubble, its cursor and stamp (file 254) |
| `+0xc4` | `mAdjustorSpeed` (file 32), the sugar's speed |
| `+0xc2` | the hurry speed, from the table at `0x0075c7f0` (0, 25, 50): the needs turn writes 25 above a toilet need of 80, else nought; the toilet arm 25; the walk to the gate 50 when a bus is due and 25 for a handle whose low two bits are nought; entering state 12 nought |
| `+0x2c` | `mCount`, the person base's byte at file 36 (loaded at `0x004f91e8`, written out at `0x004f8cc8`): the minor decision's walking-turn count |

`FUN_005019f0` case `0x11` walks the guard chain from `mFirstGuard` (`+0x1da744`, see the header's list heads above) through `+0x210` / `+0x212`, so those two are read off a staff record and are not evidence against the guest table's `mBalloonScript`. The case itself is undecoded.

## The settle-up's bookkeeping - what `FUN_004fd970` keeps besides the effects

Decoded for Q177 (`wf_1c25d211-f5e`: five decoders in Ghidra, each put to a skeptic, then a critic; the load-bearing
sites re-read first-hand) and measured in the original's memory over its stock Lost Kingdom park ("Measured in the
original", below). One settle-up, in order:

| Step | Where | What it keeps | What a player sees of it |
|---|---|---|---|
| 0 | `0x004fd983`..`0x004fd9a5` | The guest's recent-things history: `+0x1e0`..`+0x1e6` shift by one and `+0x1e0` takes the object, on both arms | Nothing directly; it steers later choices ("What a thing is worth to a guest"). OpenTPW builds it (`Peep.RememberVisit`) |
| 1 | `0x004fd9ac`..`0x004fd9d2` | The guest's `mNumRides` `+0x1c4`, `mNumShops` `+0x1c8` or `mNumSideshows` `+0x1cc` (person file 444, 448, 452) +1 by the descriptor's `+0x4ac` 0, 1 or 2, before the charge, on both arms; a feature (a toilet) bumps none | The visitor window's "Rides ridden", "Purchases made" and "Sideshows played" (`FUN_004b72a0`, controls `0x372e`..`0x3730`); the all-visitors list's "Rides Ridden" |
| 2 | `FUN_004e1690`, `0x004fd9e2` | **The visit count**: the object's `mNumCustomers` `+0x1a0` (object file 494) and today's customers `+0x1a8` +1, every settle-up, before the gate | The ride and toilet windows' "Users last month" (`0x3e21`; `FUN_004973a0` for a toilet), the all-items customer columns, and the shop and sideshow windows' "C of M" (UITEXT 460: C the thirty days' customers, M those plus the thirty days'
walk-aways, ring `+0x230`); `mNumCustomers` above nought loses the new object's full refund (`FUN_004e2290`) |
| 3 | `FUN_004fe1e0` | The effects arm, only when `+0x1f1`, the win roll, is non-zero: event 8, the cost of goods (below), a sideshow's prize, the effects, a toilet's relief, and a sideshow winner's `mNumSideshowsWon` `+0x1d0` (file 456) +1 (`0x004fe81f`) | "Sideshows won" |
| 4 | `0x004fda1b`..`0x004fda47` | `3d` = 3 × ((trunc happiness `& 0xff`) − (trunc snapshot `+0x20c` `& 0xff`)), logged into the bare `RET`; happiness itself is not moved | - |
| 5 | `FUN_004e1e00` | Today's satisfaction `+0x340`: `3d` when it is nought, otherwise (it + `3d`) / 2, truncated toward nought; no count, no clamp, so a day that averages to nought is overwritten by the next visit | Customer satisfaction, below |
| 6 | `0x004fda92`..`0x004fdb2c` | Analyser samples of `(3d + 50) & 0xff` (a byte `ADD`, so it wraps): a shop one of kind 10..13 by `SpecialIngredient` 1..4 and one of kind 15 or 16 by `AppearanceEffect` 1 or 2; a sideshow kind 18; a ride or a feature none | Nothing (below) |
| 7 | `FUN_004e19f0`, `0x004fdb33` | Today's served count `+0x2b8` +1, for every kind of object | A sideshow's "Winners last month" (`FUN_004e1f20`: 30 days' served × 100 / 30 days' customers, `0xa096`, UITEXT 42); for any other kind nothing |
| 8 | `0x004fdb38`..`0x004fdb7f` | A sideshow's winner: thought 5 and event `0x18` naming it | A thumbs-up bubble; the thought in the visitor window |
| - | `0x004fdb8d`.. | The nought arm instead: "Person lost this sideshow...", happiness less `MediumHappinessChange`, and for a sideshow thought 6 and event `0x19`; none of steps 3 to 8 | A thumbs-down bubble |

### The cost of goods and the park's money

**`FUN_004e1920( amount )`** adds the amount to the object's today's cost `+0xf8` and `mTotalCosts` `+0x184` (object
file 1086; `0x004e1938`, `0x004e193e`), then withdraws it from the bank (`FUN_004d01f0` on `mBankAccount`, world
`+0x1da726`; `0x004e1952`), then posts minus the amount to the challenge manager as challenge type 12 (a shop) or 13 (a
sideshow) (`FUN_004d27a0`, `0x004e19d9`). The shop's amount is `FUN_004e1b40`, trunc( `mCostOfGoods` × (1 + q ± a) ),
where q = clamp( (`mQualityOfGoods` − 50) × 0.005, ±0.5 ) and a the same of `mAmountOfSpecialIngredient`, subtracted for
fat or ice (`SpecialIngredient` 1 or 3) and added otherwise; at 50 and 50 it is the cost itself. A sideshow's is
`mCostOfGoods` raw, booked only for a win, beside the prize.

**The withdrawal, `FUN_004d01f0`, does nothing while the bank's `mWithdrawalsEnabled` (`+0x114`, bank file 28) is
nought** (`0x004d01f3`). Otherwise `mBalance` falls by the amount (`0x004d0205`); if it goes below nought (a `JNS` on
the result) from an OLD `mLastBalance` of nought or more (a signed test), `mTurnEnteredRed` `+0x120` takes `mGameTick`
(`0x004d020a`..`0x004d0222`); `mLastBalance` `+0x11c` takes the new balance (`0x004d0228`); the park analyser's month
total costs `+0x1f5a0` rise (`0x004d0246`) and the bank's `mProfitThisYear` `+0x124` falls by it. It refuses nothing
and has no floor. The deposit's analyser add is `0x004d01d0`. The queue drain's debit is the same function; a loan's instalments and its paying off read the flag
themselves (`FUN_004d0370`, `FUN_004d0850`), its only other readers. The constructor sets the flag to 1 and the load reads it; `FUN_00404140` clears it
and `FUN_004041d0` sets it again around an online park's layout replay (game type 1 only), so it is 1 in Easymode and
every offline park. The deposit, `FUN_004d0190` ("Spending", above), has no such gate.

**So a drink nets the park +10** (30 in, 20 out), a Jungle Spray play won −30 (20 in, 50 out, 50 to the guest) and one
lost +20. The challenge posts land only while a challenge of that type is current and on; the challenge system switches
on only in game type 0 and after the days at `0x007857c8` (`DaysUntilFirstChallenge` by key order; `FUN_004d1e90`), and Easymode's manager (thing 10) saves it
off.

**The detail, re-read for Q177c** (`wf_245f2fbd-cf9`: three Opus readers in Ghidra and a code map, each put to an
Opus skeptic; reports in `~/.cache/tpw-harnesses/q177c/map/`):

- **The shop's amount.** `FUN_004e1b40` reads the low byte of `mQualityOfGoods` `+0x18c` and of
  `mAmountOfSpecialIngredient` `+0x198` (`0x004e1b47`, `0x004e1b4d`) and the cost `+0x188` as unsigned
  (`0x004e1c30`). Each term is (byte − 50.0f) × 0.005f (`0x3ba3d70a`), stored as a float (`0x004e1b82`, `0x004e1b97`)
  and held strictly to ±0.5 (`0x004e1b86`..`0x004e1bec`); the ingredient's is negated for `SpecialIngredient` 1 and 3
  (`0x004e1c12`..`0x004e1c1e`) and added for any other value; then, on the FPU's stack, (q ± a) − (−1.0) times the
  cost, `__ftol`'d toward nought. The shop window's
  preview `FUN_004e1a30` is the same arithmetic. Measured against what the original booked in Alexah's Lost Kingdom
  saves (`~/.cache/tpw-harnesses/q177c/objmoney.py`): item 1203 (ice) at quality 0 and amount 100 books 10, 1206 (sugar)
  at 0 and 0 books 10, 1209 at 100 and 50 books 37, 1211 at 0 and 50 books 37, 1208 at 0 and 50 books 22, and every
  saved object's `mTotalCosts` is a sum of its per-sale amount; the park that ships holds 50 and 50 on all fourteen.
  **The FPU's precision matters only off the steps of 50 the saves hold**: the Drinks Shop at quality 0 and amount 10
  books 18 at 53 bits and 19 at 24, which is not settled (`park-engine.md`, "Which rounding is live"; the game never
  passes `DDSCL_FPUPRESERVE`).
- **Where the amount leaves the steps of 50**: the shop window's amount slider (`0xc086`) runs 0 to 100 (`0x004b06d2`),
  its quality slider (`0xc06b`) 0 to 2, times 50 (`0x004b06b7`), so only the amount does; no save read holds one off
  the steps, and the shop window is not built here.
- **The booking's order in `FUN_004fe1e0`**: event 8 (`0x004fe204`); a sideshow books `FUN_004e1a10` (`0x004fe225`)
  and then pays the same to the guest (`0x004fe231`); a shop books `FUN_004e1b40` (`0x004fe251`); a ride or a feature
  books nothing. `FUN_004e1920` tests nothing, books the object's two even while withdrawals are off, and posts to the
  challenge manager with nought for the item and thing filters, so a challenge naming one item never receives it. A
  zero withdrawal is not a no-op: it writes `mLastBalance` and can stamp the red.
- **A thing just built** (`FUN_004db090`): `+0x188` the item's `InitCostOfGoods` (`0x004db383`), `+0x18c` and `+0x198`
  50 (`0x004db389`, `0x004db3b3`), `+0x190` 100 less `InitChanceOfLoosing` (`0x004db3a1`), today's costs and
  `mTotalCosts` nought (`0x004db14b`, `0x004db169`); the bank's constructor (`FUN_004cf7c0`) starts `mWithdrawalsEnabled`
  at 1 and the other three at nought (`0x004cf7f0`..`0x004cf81a`).
- **Every caller of the bank.** The withdrawal has 15 callers: the purchase (the object constructor, `0x004db4f6`),
  a path cell and a queue cell (`0x00534879`, `0x005348ad`), the sale's queue drain and its track (`0x00528007`, `0x00528179`: the
  demolisher's switch on `WhichTrackType` inside its queue arm, `0x0052801d`, where karts and the water ride clear
  their cells and pay for them and any other non-zero type withdraws nought), an upgrade on completion (`0x004dfa96`), the monthly wage on message `0xc`
  (`0x00504cb9`), a dismissal (`0x00505944`), staff training (`0x00505a45`), the cost of goods (`0x004e1952`), Buy Land
  (`0x00525d6d`), a track cell (`0x005391de`) and the coaster editor (`0x004352ac`, `0x00441c99`, `0x00445c95`). The
  deposit has 11: the charge (`0x004e16c6`), every object's destructor (`0x004dd19f`, the sale's refund), a cleared
  queue cell forced and unforced (`0x00536aa8`, `0x00536b5b`), a cleared track cell (`0x0053b622`), a challenge's prize
  and its half (`0x004d3108`, `0x004d2f33`), the coaster editor's paybacks (`0x00441c8a`, `0x00445c2a`, `0x00445c86`)
  and the cheat that banks 10,000 (`0x0040c462`). The purchase skips its withdrawal when the item's `GoldenTicketCost`
  (`+0xc4`) is above nought and `FUN_004d4b70` answers nought, calling `FUN_004d4ad0` instead, which spends the tickets
  only when the player holds enough (`0x004db4a6`..`0x004db4e0`); `FUN_004d4b70` asks whether the player has bought the
  item with tickets before, so only the first purchase goes without cash. The buy list asks the same: such a row is
  named "??? Mystery Ride! ???" (UITEXT 137, `0x004ab02e`), and a click on it is refused when the player holds too few
  tickets (`FUN_004d4b90`, `0x004ac5a9`). The bank's month turn
  `FUN_004d0370` carries inlined copies: it banks `mBatchBalance` ungated and zeroes it (`0x004d0393`..`0x004d03db`),
  withdraws each loan's instalment gated (`0x004d0402`) and adds the principal's share back onto `mProfitThisYear`; a
  loan's drawdown (`FUN_004d0750`) refuses only a slot already bought (`0x004d075e`), never reading `loan_available`, and
  deposits and takes the amount back off the profit (`0x004d07ee`); its payoff (`FUN_004d0850`) refuses a balance below
  what is owed by an unsigned test (`0x004d089a`), so a negative balance passes, and zeroes `months_repaid`
  (`0x004d0916`) before its profit share reads it (`0x004d0945`), so the year is charged the whole loan's interest
  again rather than the months left's.

**OpenTPW** (Q177c with Q96): `ParkState.Spend` is the withdrawal at every site that pays (purchase, path, queue, drain,
dismissal, cost of goods, a sold coaster's nought), gated on `WithdrawalsEnabled` and moving `LastBalance`,
`TurnEnteredRed` (on `GameTick`) and `ProfitThisYear`; `ParkState.Deposit` is the deposit (a sale's refund, a cleared
queue cell, the charge through `TakeAt`); `ParkState.Take` is the gate fee; `ParkState.BookCostOfGoods` is
`FUN_004e1920`, and `ParkRideOperation.ShopCostOfGoods` is `FUN_004e1b40`, its terms as floats and its sum and product
in double ("What a thing is worth to a guest", "Where OpenTPW differs", the FPU's row), with the item's cost of goods
(Q97). The four bank fields are seeded from the save, and the year's change zeroes the profit
(`ParkState.TurnTheYear`, on `GameCalendar.YearRolled`). Counted, not built: the analyser's money in and out
(`BANK_ANALYSER_MONEY_IN`, `_MONEY_OUT`), the gate fee's analyser totals and challenge post
(`GATE_FEE_ANALYSER_TOTALS`, `GATE_FEE_CHALLENGE_POST`), the booking's (`COST_OF_GOODS_CHALLENGE_POST`), the purchase's golden-ticket arm (`PURCHASE_GOLDEN_TICKET_ARM`, on
every such purchase, as no list of ticket-bought items is kept) and a kart or water ride's track at a sale
(`SALE_TRACK_TEARDOWN`). The month's change is built: "The month's change", below. Not counted: the buy list's mystery row and its ticket test (Q141).

**Who reads what the booking moves**:

- The balance, whose getter is read at 26 sites (`FUN_005195d0` then `FUN_006ad810`): the park gadget's money
  (`FUN_004a0ab0`); the buy list's affordability and row colour; the hire screen (`FUN_0049b650`, `FUN_0049bdd0`); the
  loans screen's pay-off test (`0x0049f5d4`); the path tools' price checks (`FUN_00535670` reddens a cell dearer than
  the balance, `0x00535914`; `FUN_005346d0`, `0x00534779`; `FUN_00539760`, `0x00539f82`); the analyser's month-end
  balance sample (`FUN_004c7720`, `0x004c7730`, ring `+0x1f104`); Buy Land's pending price (`FUN_00531a50`,
  `0x00532383`) and a track cell's (`FUN_00538fc0`, `0x00539056`); four not yet named (`0x00487b36`, `0x004abd8d`,
  `0x004af4a5`, `0x00523645`); and eight advisor rows in undisassembled code
  (`0x0059defb`..`0x0059eafd`): three compare it with a row's threshold (`+0x274`, `+0x298`, `+0x330`), one compares
  it less `FUN_004d0a00` with `FUN_004d0970` × `+0x338`, and four fire only below nought (`0x0059e9b0` when
  `FUN_004d0810` answers non-zero, and the three red-time rows below).
- Going red: `FUN_004d0370`, at each month change with the balance and `mLastBalance` both below nought, divides the
  time since `mTurnEnteredRed` by 30 days and at 6 or more broadcasts message `0x13` with 2 (`0x004d054c`,
  `0x004d05b2`), which the bank's handler turns into the end of the park (`FUN_004d02d0`, `FUN_005168f0`) unless the world's state
  (`+0x1da738`) is already 4 or `FUN_00516c00` finds the word at `0x007cf4f4` set (`0x004d030d`, `0x004d031b`): the
  thing id of the "End" feature, which `FUN_00516b00` builds and stores (`0x00516bad`) and `FUN_00516c10` deletes and
  zeroes (`0x00516c6a`). A deposit
  never writes `mLastBalance`, so climbing out on deposits alone does not clear the stamp: if the next withdrawal takes
  the balance below nought again, `mTurnEnteredRed` is not stamped afresh and the count runs from the first entry. Any
  withdrawal that leaves the balance at nought or more (a shop's cost of goods, a loan's instalment) writes a
  non-negative `mLastBalance`, and the next dip stamps afresh. The advisor counts the same months: rows 103 to 105
  (`0x0059e9f0`, `0x0059ea70`, `0x0059eaf0`) score only while the balance is below nought and `FUN_004d0810` answers
  nought (it looks over the eight loans; what it asks is not decoded), by `FUN_004d0260`'s time since `mTurnEnteredRed`
  over 30 days: under 3 months, 3 or 4, and exactly 5, the month before the end.
- `mProfitThisYear`: golden ticket 4 when it passes the global at `0x007857a0` (`GoldenTicketLocal.ProfitYear` by key order; `FUN_004d4bc0`, `0x004d4d6b`) and
  advisor row 318 near it, both in game type 0 only. It is zeroed on the year's change (`0x004d034e`), not on entering a
  park: the original entered Easymode with −12013 and read −11888 after five gate fees.
- The object's cost ring and total: the all-items "Profit Last Month" (`FUN_004e1c70`: up to 30 finished days of
  takings `+0x70` less costs `+0xf8`, today's excluded) and "Total Profit" (`FUN_004e1c60`, `mTotalTakings` −
  `mTotalCosts`), and the shop and sideshow windows' "Profit last month" (`0xc078`, `0xa092`). The shop window's "Cost of
  goods" (`0xc076`) shows `FUN_004e1a30` of the window's pending quality and amount, which reach the shop only when the
  window closes, steps to the next shop or applies to all (`FUN_004b0aa0`, vtable slot `+0x3c`), so a moved slider
  shows a cost the sales do not book yet.
- The analyser's month totals, pushed into 144-month rings at `+0x1fc94` and `+0x1f5a4` on the month's change
  (`FUN_004c7720`, with the staff, training and a dozen more, after thing 1's training and before the bank's turn and
  the wages: "The month's change"): the finance graph's "Money out" once the month is finished, the hire screen's mini-balance
  ("Other costs", "Balance": `hud.md`), the staff-costs and loans screens, and the gadget's icon beside the money
  (`FUN_004a0e30`, control `0x31`, frame 1 when last month's cash in was below its total costs; likely the red down arrow beside the
  money in the original's frame, though which picture each frame is was not established).

### The month's change: the training, the analyser, the bank and the wages

Decoded for Q198 (`wf_362d8882-a82`: three Opus decoders in Ghidra, each put to an Opus skeptic; reports in
`~/.cache/tpw-harnesses/q198/decode/`), and measured in the original and in the game (below).

**Who hears it, and in what order.** The calendar sends message `0xc` at most once a sweep, after every thing's turn,
between the day's `0xb` and the year's `0xd` (`FUN_004f8260`: built at `0x004f83c5`, sent at `0x004f8420`); it stores
the new month, day and year only after all three sends (`0x004f8541`..`0x004f8552`). The message centre (`[0x00788d3c]`,
29 sets of thing ids, one per message type) hands a message to the things in its set one at a time, each handler
finishing before the next, in ascending id (`FUN_0040fb10`; the set is ordered by an unsigned compare, `0x0040fe8b`).
Five constructors join set `0xc`: model 9's (`0x00508a08`), the tag system's, the analyser's, the bank's
(`0x004cf8f0`) and every member of staff's (`FUN_00504b90`, `0x00504c53`); a load replaces every set with the file's
(`FUN_0040fd60`; FileFormats `saves.md`, "The message centre module"). In all nine park files set `0xc` is things 1,
4, 5 and 8 and then every member of staff, so a month's change runs, in this order:

1. **Thing 1**, model 9, the header's `mStaffHQ`, pays the training (`FUN_00508a30` → `FUN_00508e70` → `FUN_0050c800`,
   `0x00508e79`), whether the park is open or shut, and then looks at strikes (not decoded).
2. **Thing 4**, the tag system, ignores it.
3. **Thing 5**, the park analyser, closes the month (`FUN_004c7720`, its one call `0x004c739c`): it samples the
   balance into its ring (`+0x1f104`), then pushes and zeroes the month's costs `+0x1f5a0`, staff `+0x1f7f0`, training
   `+0x1fa40` and cash in `+0x1fc90`, each into a 144-month ring (`FUN_004ce290`), and rolls a dozen totals besides.
4. **Thing 8**, the bank, runs its month turn (`FUN_004d02d0` → `FUN_004d0370`, `0x004d035f`).
5. **Each member of staff** pays a month's wage (`FUN_00504c70`, `0x00504cb9`).

So the training is booked into the month that closes, and the bank's turn and every wage into the new one; the
analyser's month-end balance sample sees the training and not the wages, and the bank's red test sees the training
but not this change's wages. Nothing else hears `0xc` (the advisor, the research lab, the weather, the UI receiver, the
challenge manager, objects and guests subscribe to other messages). Entering a park seeds the calendar's month and
year from tick 0 (`FUN_004f85a0`); a load then reads the month and the day but not the year (`FUN_004f7f30`,
`0x004f81b5`, `0x004f81f7`), so loading `Easymode.TPWI` (tick 755, 2 February 2000) sends nothing, and its first
month's change is at tick 1383, 1 March, 628 sweeps in. A park saved in a year other than 2000 would get `0xd` on its
first sweep and lose `mProfitThisYear` (decoded, not measured; `QUEUE.md` Q149).

**The training, `FUN_0050c800`.** Thing 1 keeps five monthly budgets, `mBudget[0..4]` at `+0xc` (file 82; FileFormats
`saves.md`, "The staff HQ"), the handymen's, mechanics', entertainers', guards' and researchers' in that order, set on
the Staff Training Budgets screen only (0 to 10000 in steps of 25, `FUN_004b2750`; `hud.md`). It walks every thing
twice: once counting the staff of each model, then calling `CStaff::TrainMe` (`FUN_00505a10`) on every member with
budget / count, a signed division (`0x0050c945`..`0x0050c97c`). Neither pass writes a budget, which stays set from
month to month; what the division leaves over is never taken; and nothing tests the budget, so a nought budget still
makes the call. `TrainMe`:

- at grade 4 (`mCurrentPayGrade`, `+0x1e4`) takes nothing and returns (`0x00505a15`), though the member counted in the
  divisor;
- otherwise withdraws the share whole (`FUN_004d01f0`, gated; a nought share still writes `mLastBalance`) and adds it,
  ungated, to the analyser's training total (`0x00505a62`);
- divides the share by the grade's `PoundsPerTrainingPoint` (signed, `0x00505ae1`: `HandymanConstsPerGrade[g]`,
  `MechanicConstsPerGrade[g]` and so on by model; the global `Standard.sam`'s in every jungle park, which no theme or
  easy file overrides: 5, 8, 12 and 15 for grades 0 to 3, the researcher's 8, 12, 15 and 18, and nought at grade 4),
  holds the quotient to 100 (`0x00505ae5`) and adds it to `mPercentageThroughGrade` (`+0x1e8`, one byte);
- at 100 or more promotes: the grade one up, the progress less 100, happiness (`+0x1f8`) to 100
  (`0x00505b2a`..`0x00505b3c`). So a member rises at most one grade a month, and is paid at the new grade in the
  same change, as the wage comes after.

What the point division leaves, and whatever buys past 100 points, is spent for nothing: 25 on a grade-3 mechanic
buys one point and loses 10. Only the withdrawal is gated, so with withdrawals off the training advances for nothing.

**The bank's month turn, `FUN_004d0370`.**

- `mBatchBalance` (`+0x10`) is deposited as `FUN_004d0190` deposits (the balance, the analyser's cash in and the
  profit, ungated) and zeroed (`0x004d0393`..`0x004d03db`). Nothing but a file makes it non-zero, and it is nought
  in all nine park files: dead by CONTENT.
- Each of the eight loans whose `loan_bought` is set (`0x004d03e1`) pays its `monthly_repayment` by the withdrawal,
  inlined (`0x004d0402`..`0x004d0460`, the same as `FUN_004d01f0`); then, ungated, `months_repaid` goes up one
  (`0x004d0466`) and `mProfitThisYear` gains m − (m × P − A) / P, the division UNSIGNED (`DIV`, `0x004d0499`), which
  gives the principal's share back so that only the interest counts against the year. When `months_repaid` reaches the
  period, `loan_bought` and `months_repaid` go to nought (`0x004d04c6`, `0x004d04c9`). On an easy park's loans (APR
  nought) m × P − A is minus what A / P leaves over, near 2^32 unsigned: loan 3, 10000 over 36 months at 277, would
  take 119,304,646 off the year's profit each month, where a signed division would take nought. No loan is bought in
  any of the nine park files, and OpenTPW has no loans screen: dead by CONTENT.
- The end of the park: when the balance and `mLastBalance` are both below nought after the loans, the time since
  `mTurnEnteredRed` (`FUN_004f88b0`: the ticks × the clock's rate / 4, in seconds) is divided into thirty-day months,
  and at 6 or more (4148 sweeps at the shipped rate, about 17 minutes of play) the bank broadcasts message `0x13` with
  2 (`0x004d054c`..`0x004d05b2`). Its own handler then ends the park (`FUN_005168f0`) unless it is ending already, and
  the advisor raises record `0x6a` (`0x0059aee7`). The end sends nothing further and the walk goes on, so every member
  of staff is still paid in the ended park. A park that spends past nought and stays there reaches it.

**The wage, `FUN_00504c70`**: `PerTypeStaffConsts[type].PayMultiplier` × `PerGradeStaffConsts[grade].BaseWage`
(`0x00504ca4`; the type is 0 to 4 for the handyman, mechanic, entertainer, guard and researcher, `FUN_00506490`),
`Easy_Standard.sam`'s numbers in Instant Action (game type 2, which Lost Kingdom's Easymode is; the full game's
files make the shipped five 810), withdrawn gated (`0x00504cb9`) and added ungated to the analyser's
staff total (`0x00504cf1`). Nothing is tested first: a member resting, on strike, in the hand or hired that month
pays the whole month. A candidate in the hire pool is not a thing and is paid nothing. A dismissal pays one more
(`FUN_00505790`, `0x00505944`), and the delete's message `0x1b` takes the member out of every set.

**What Lost Kingdom reaches.** The shipped park's five staff pay 7 × 9, 7 × 23, 7 × 12, 7 × 15 and 5 × 25, **538 a
month**. Its budgets are nought, so the training is five withdrawals of nought, and its bank's turn moves nothing.

**Measured in the original** (the reference install under Proton, off-screen, the stock Lost Kingdom park loaded on
2.8.2000; `~/.cache/tpw-harnesses/q198/orig/`: `watch.py`, `watch1.log`, `watch2.log`, `shootturn.py`, `tables.py`),
predicted first from the decode: 538 at each change. The live tables held the easy wages (`PayMultiplier` 9, 23, 12,
15, 25; `BaseWage` 3, 4, 5, 7, 9) and the global training costs above. At 3.1 (tick 1383, as decoded) the balance fell
88212 to 87674, and at 4.1 88519 to 87981, both 538, `mLastBalance` taking the new balance and the profit falling by
it; photographed on both sides of the second (`turn-0-before-hud.png`, $88519 on 3.31.2000; `turn-1-after-hud.png`,
$87981 on 4.1.2000). The shipped budgets read nought on the Staff Training Budgets screen (`o11.png`). With the
mechanics' budget raised one step, to 25 (`o12.png`), 5.1 took 563 and the mechanic's `mPercentageThroughGrade` went
0 to 1 (25 / 15), both as predicted; and each change left the analyser's month costs at 538, so the 25 went into the
month that closed and the wages into the new one. The park clock was NOT TRUE (1.48×, 7.8 days up), which moves no
amount.

**Measured in the game** (the build before any change; `q198run.py`, silent, the stock jungle park, one handyman hired
at 36 a month, predicted first; `save/` unchanged; `q198-run1/`, 8 of 8): paused on 1/31/2000 and stepped to 2/1/2000,
`money` read a balance of 88137 on both sides with no Bank line between, and `unimplemented` went from none to
`BANK_MONTH_TURN` 1, `STAFF_MONTHLY_WAGE` 6 and `STAFF_MONTHLY_TRAINING` 6; photographed on both sides, the HUD at
$88137 (`0-before-month.png`, `1-after-month.png`).

**OpenTPW** (Q198b). `Level` sends the month's change in the original's order: `ParkPeople.TrainTheStaff` (thing 1:
the budgets read from the save's staff HQ, `ParkWorld.StaffHq`; each kind's budget over its members, signed;
`ParkPeople.Train` is `TrainMe`), `ParkState.TurnTheMonth` (the analyser's close, counted `ANALYSER_MONTH_CLOSE`; the
batch deposited, the bought loans paid with the unsigned division, and `MonthsInTheRed` as `FUN_004f88b0` and the
thirty-day divisor count it), then `ParkPeople.PayTheWages` (`ParkStaffPool.WageFrom` through `ParkState.Spend`, in
ascending thing id). Counted, not built: the strike check thing 1 makes after the training
(`STAFF_HQ_MONTHLY_STRIKE_CHECK`), the analyser's training and staff totals (`STAFF_TRAINING_ANALYSER_TOTAL`,
`STAFF_WAGE_ANALYSER_TOTAL`), and the end of a park six months in the red with the advisor's record `0x6a`
(`BANK_PARK_ENDS_IN_THE_RED`, `ADVISOR_PARK_ENDED_IN_THE_RED`). A grade with no `PoundsPerTrainingPoint`, which the
original divides by unguarded and no shipped file has below grade 4, is counted (`STAFF_TRAINING_NO_POINT_COST`) and
buys nothing. No screen sets the budgets or buys a loan yet, so both keep what the save holds. OpenTPW's calendar
counts from nought (Q149), so its first month's change is 1 February, 177 s in, where the original's after the same
load is 1 March.

**Measured in the game after the build** (`q198brun.py`, `q198b-run1/`, silent, the stock park, the pool's first
candidate hired at 36; predicted first; `save/` unchanged; 15 of 15): stepped paused over 2/1/2000, six training
withdrawals of nought, a deposit of nought, then the wages 63, 161, 84, 105, 125 and the hire's 36 last; the balance
88137 to 87563, 574, photographed on both sides (`0-before-month.png`, `1-after-month.png`); the three Q198 counts
gone and the four above counted once, once, six and six times.

### The object's six day rings, and what shows them

An object keeps six rings of 30 whole numbers, each 0x88 bytes in memory: `mTemp` (today's) `+0`, `mData[30]` `+4`,
`mCurrentEntry` `+0x7c` (from −1), `mNumEntries` `+0x80` (30) and `mWrappedAround` `+0x84`, one byte. On message `0xb`,
**the day's change**, the object's handler `FUN_004dd320` rolls all six (`0x004dd369`..`0x004dd4c8`), in the order
customers, walk-aways, served, takings, costs, satisfaction: `cur += 1`, and at `cur >= mNumEntries` (a signed test)
`cur = 0` and the flag set; then `mData[cur] = mTemp`, `mTemp = 0`. It calls nothing and touches nothing else; the
handler's only other case is `0x1b` (the serving staff member gone). `mNumCustomers` `+0x1a0` and `mNumWalkAways`
`+0x1a4` are lifetime counts and never roll. In the object's save record, in the serialiser's order (`FUN_004db7d0`):
today's costs `+0xf8` (file 228), takings `+0x70` (361), `mNumCustomers` (494), customers `+0x1a8` (498),
`mNumWalkAways` (631), walk-aways `+0x230` (635), served `+0x2b8` (768), satisfaction `+0x340` (901); the record's tail
follows at 1034. Each ring is written `mCurrentEntry` (4), `mNumEntries` (4), `mWrappedAround` (1), `mTemp` (4) and then
`mNumEntries` days (`FUN_004e2c10`); the load checks only that each read returned its bytes, and clamps neither count.
The constructor (`FUN_004db090`) and the load's ring init (`FUN_0051ade0`) set `mTemp` 0, the entry −1, 30 and the
flag 0, and leave `mData` unwritten (the object is `_nh_malloc`'d, `0x450` bytes), so an unreached slot holds whatever
the heap did; no reader of the figures goes past the filled days (the serialiser writes all 30).

**Who sends the day's change.** Only the calendar (`FUN_004f8260`): once a world tick at most, when the day of the
month (and only it, `0x004f8321`) differs from `mDayAtLastUpdate`. The same call then compares the month on its own
(`0x004f83b9`) and sends message `0xc`, the month's change (`0x004f83c5`), and the year (`0x004f84d0`) and sends `0xd`
(`0x004f84d8`). The bank's handler (`FUN_004d02d0`) hands `0xc` to its month turn `FUN_004d0370` (`0x004d035f`) and
answers `0xd` by zeroing `mProfitThisYear` alone (`0x004d034e`); `0xc` goes to thing 1's training, the analyser, the bank
and each member of staff's wage, in that order ("The month's change"). OpenTPW's `GameCalendar` makes the three compares (`DayRolled`, `MonthRolled`, `YearRolled`). It goes to the `0xb` listeners - every object built
by `FUN_004db090` (`0x004db23a`) and the challenge manager - in ascending thing id, synchronously, and after every
thing's turn in that tick (`0x00516695`), so the tick's settle-ups count into the day that is closing. Persons never
get it. A new world rolls on its first tick (the calendar's constructor sets the day to −1, `0x004f7ebd`); a load reads
`mDayAtLastUpdate` from the file (`0x004f81e7`) and the listener sets with it (`FUN_0040fd60`), so after a load which
objects roll is what the save's sets name.

**"Last month" is the last thirty finished days, today's never among them.** The ride window's Users last month
(`FUN_004ade40`, `0x004ade77`..`0x004adf20`) adds the customers ring's last min( filled, 30 ) finished days, filled
being `mNumEntries` once wrapped and `mCurrentEntry` + 1 before (`FUN_00495d40`), walking back from the entry and
round the end only when wrapped (`FUN_00495cf0`); each day is added as an unsigned 32-bit figure into a double and the
total `__ftol`'d. Before the first day ends it is nought. A number painter (`FUN_0048fde0`, font 6) prints it `"%d"`
(`0x0048ff8f`); the toilet window's `0x15bbd` is the same sum with the same painter (`FUN_00497100`). The same
min( filled, 30 ) customers sum also feeds the all-items refresh (`FUN_004955e0`) and its three row adders
(`FUN_00493cd0`, `0x00493f30`, `0x00494070`), the shop and sideshow windows' C of M, winners (`FUN_004e1f20`) and
seven advisor scorers (`FUN_004caae0`, `0x004cadf0`, `0x004cb300`, `0x004cb640`, `0x004cbba0`, `0x004cbec0`,
`FUN_004cc090`). `mNumCustomers` is read only by the full refund's test (`0x004e2390`) and the serialiser. The window
fills its figures when a thing is shown (`FUN_004ae430`), on the door's press (`0x004af871`), and every 4000 ms of real
time while it is open: message `0x15`, as the window is made, arms timer `0x80080` (`0x004af67b`), whose ticks come
back as message `0x10` (`0x004af63b`); the timer walk stands still under the game menu, the options screen and a
message box (`0x006622d6`).

**Walk-aways** are counted only by `FUN_004e1670` (`mNumWalkAways` and today's `+0x230`, one each), whose one caller
(`0x0050077f`) is the door's refusal on price (`FUN_004fde50`), which answers nought at once for a free object: a full
or shut queue, a failed rejoin or a refused admission count nothing.

**Customer satisfaction is `FUN_004e1e30`**: 50 + trunc( the sum of the last n finished days / n ), n the days filled up
to 30, and 50 with none; today's is excluded, a day with no visit counts nought and pulls it toward 50, and nothing
holds it to 0..100. It shows in the shop and sideshow windows' gauge (`0xc079`, `0xa094`, UITEXT 36 and 46), the
all-items Shops and Sideshows columns, and the map's satisfaction colours, which paint only ride, shop and sideshow
cells (codes 1, 2, 3, 8 of `FUN_005f2050`), so a toilet's is shown nowhere and a ride's only on the map. The advisor
reads it for shops and sideshows more than 30 days old: the drinks "great satisfaction" row (256, response 496) scores
(sat − 70) × 4 and can pass the advisor's 25 from 77; the "poor" rows 243 to 246 (food, shops, restaurant, drinks) score a satisfaction
below their line, and row 247 (sideshows, though its log says "satisfaction") the lowest thirty-day walk-away
percentage below its line (`FUN_004cc090`), each times a positive `ScorePerPoint`, never above nought, so they never
play with the shipped `Advisor.sam`. Nothing in a
guest's choice reads it.

### The join's snapshot, `+0x20c`

A float, a bit-for-bit copy of the guest's happiness (`MOV`, `0x004ffd88`..`0x004ffd92`) taken in `FUN_004ffbc0`, the
state-10 turn, when a guest with a thing chosen has arrived on its back-of-queue cell and passed the room, excitement
(only when the descriptor's `+0x13c` low byte is non-zero) and too-long gates, just before its only call to
`FUN_004ddb90`, which links them onto the queue. A failed walk to their place afterwards leaves it written. The
InQueue re-take (`0x00500532`) and the door's rejoin (`0x00500826`) do not copy it again. Every guest who reaches a
settle-up passed it in the same visit, whatever the thing (the settle-up is reached only through `VAR_LETMEOFF`, which
only a guest admitted from the queue's head reaches). It is zeroed by the guest's constructor (`0x004faf8d`) and by the
load's factory before the record is read (`FUN_005179c0`, `0x0051861c`); those three are its only writers (a scan of
every `+0x20c` store). It is not saved, and the settle-up is its only reader. So a guest already queued or riding when
a park is saved (state 11, 12, 13, 14 or 16, or 8 with one of those saved) compares against nought after the load, 3 ×
their whole happiness. On staff the same offsets are the patrol corners (`+0x20a`, `+0x20c`).

### The event history

`FUN_0050c100( event, argument )` writes into the 0x90-byte history block at the person's `+0x30`: `mLastThought` at
`+0x30`, `mEventHistory`, 32 entries of two words from `+0x34`, `mThoughtScript` `+0xb4`, the cursor `mActionHistIndex`
`+0xb8` and `mTimeBubbleShown` `+0xbc`. A push stores at the cursor and steps it by one, wrapping at 32, and skips an
entry equal to the newest. The block is saved (person file 254). **The ring's only reader is the destructor's dump**
(`FUN_004faa60` through `FUN_0050c190`), which prints into the bare `RET` `FUN_005da3c0`; wider scans (every register
made from `+0x30`, every `+0x34`/`+0x36` address) found no other. So nothing a player sees comes from it. The settle-up
pushes 8 (the object, every effects arm), `0xb` and `0xc` (a costume given, a balloon), `0x11` (a toilet), `0x12` (the
toilet's illness), `0x18` and `0x19` (a sideshow won or lost, naming it).

### Thoughts 5 and 6, and the bubble

`FUN_0050be80( thought, 0 )`, SetThought, stores the thought in `mLastThought` first, then shows nothing more in any
first-person view (`gui_CameraFlags & 0x16`) or an online game; otherwise it frees the old bubble and, when `mGameTick`
is at least `mTimeBubbleShown` + 20 × the thought's class, builds a world sprite of kind 9 ("thoughts") and stamps the
time (`0x0050c039`..`0x0050c06a`). Thoughts 5 and 6 are class 0, so a sideshow player always gets one: 5 "Pleased", a
thumbs-up; 6 "Dissatisfied", a thumbs-down (THOUGHTS.str rows 5 and 6; the pictures in `Generic\Thoughts`). Each frame
`FUN_004fa030` puts the bubble 2.5 units above the guest; `FUN_0050be40` takes it away 13 to 16 sweeps later, or a
later thought frees it sooner. `mLastThought` is read by the visitor window (its icon and text), Park Status's "Top 3
Thoughts" (three icons, empty ones hidden), the all-visitors list and the locator panel. Thought 6 also comes from the
door's price (`0x00500756`) and the park's fee (`FUN_004ff9d0`).

### The analyser's samples

The park analyser (`mParkAnalyser`, model 13) keeps 20 byte rings of 50, one per kind, at `+0x216fc` + kind × `0x40`
(`mTemp`, `mData[50]`, `mCurrentEntry` from −1 at `+0x34`, `mNumEntries` at `+0x38`, `mWrappedAround` at `+0x3c`);
`FUN_004c74b0( kind, value )` steps the entry on and stores the byte. **Only kind 0 is ever read**: the gate's ticket
opinion (`FUN_004ff5b0`), by the advisor's messages 91 and 92 ("high ticket price", "a steal") through `0x004c9830` and
`0x004c9920`, whose four calls all pass 0. The settle-up's kinds 10 to 13, 15, 16 and 18, and the door's 1 to 4, 6, 7
and 9, are written, saved and loaded, and read by nothing else.

### What Lost Kingdom reaches

All of it. The Belly Bounce (a ride): steps 1 to 5 and 7. The Drinks Shop (`Coconut`, shop, ice, appearance nought):
steps 1 to 7, a booking of 20, kind 12, the ingredient's +2 and thirst back. The Jungle Spray (sideshow, chance 25):
steps 1 and 2 on every play, the rest on a win (the booking of 50, kind 18, thought 5), thought 6 on a loss. The three
Small Toilets (features): step 2, and on the effects arm steps 3 (events 8, `0x11`, and `0x12` when illness is above 90), 4, 5 and 7, with no guest counter,
no booking and no sample. By their items' research cost (`hud.md`'s rule, not measured) a player can place a Balloon Shop (the balloon arm) and a
Burger Shop (fat) from the start, and a Costume Shop, Fries (salt) and Ice Cream (sugar) after research; each has an
`Easy_` file. The shop window's
quality and ingredient sliders reach the cost of goods, the ingredient's terms and a balloon's life.

### Measured in the original

The reference install under Proton, off-screen, its stock Lost Kingdom park entered by the reference player, read from
memory at each settle-up (`~/.cache/tpw-harnesses/q177/origread.py`, `orig/watch1.log`, checked by `orig/analyse.py`):
814 changes over 7 minutes, 145 of them settle-ups; the 47 in which one guest alone moved were checked against the
decode, 254 checks of 257 as predicted, and the three that were not are the harness's: one thirst read against a stale
snapshot, and two where one poll caught a ride's settle-up and the day's roll together. 16 drinks each moved the balance
+10, 9 lost spray plays +20 and 8 toilet visits nothing, and 14 Belly Bounce rides were as decoded. The four won spray
plays (guests 33, 48, 67 and 44) each shared their poll with guests who moved no counter, and each moved the balance
−30, costs +50, served +1, cash +30, with thought 5, a live bubble and event `0x18`; the Jungle Spray's `mTotalCosts`
went from 0 to 200 over the run. A drink (guest 42): the balance 87724 to 87734,
`mProfitThisYear` +10, `mLastBalance` the new balance, the analyser's month cash in +30 and costs +20, the shop's
takings +30 and costs +20 with today's +30 and +20, customers and served +1, satisfaction 21 (3 × 7) and kind 12's
sample 71, the guest's purchases +1, cash −30, happiness 50 to 57, thirst 36 to 20, event 8 naming thing 16. A Jungle
Spray win (guest 33): happiness 59 to 83, satisfaction 72, kind 18's sample 122, the balance −30, costs +50, cash +30,
thought 5 with a live bubble, event `0x18`. A loss (guests 40, 34, 31): the balance +20, no cost, nothing served,
satisfaction unchanged, happiness −15, thought 6 with a live bubble, event `0x19`. A toilet (guest 37): customers and
served +1, no guest counter, event `0x11`. The Belly Bounce: rides ridden +1, served +1, event 8, and today's
satisfaction by step 5 (18 averaged with 18; 45 then 15 to 30). At each game day's change the six watched objects' satisfaction
cursors (`+0x3bc`) stepped together, wrapping from 29 to 0, and today's costs, takings, customers, served and
satisfaction went to nought with them (the walk-aways and the other five cursors were not read). The HUD of the last frame read 88070, the balance memory held at that tick (`orig/s05.png`), beside a red down arrow.
The park clock ran fast (the machine had been up seven days, `TOOLING.md`); nothing here is a timing.

### Measured in the game, before any change

`q177run.py` (silent, the stock jungle park, three guests made inside by `admit` and sent by `send`; predicted first;
`save/` unchanged; `~/.cache/tpw-harnesses/q177-run1/`): each counter's rise between two censuses was the one the
interval's log lines predict: the lost spray play, the two drinks (one interval), the toilet visit, and an empty tail
(four of four intervals); `money`
read balance 88112 and takings 125 across both drinks, where the original's would have risen 20; the drinker's line
read thirst 80 to 40, happiness 0 to 5, cash 700 to 670, where the original's gives thirst 60 and happiness 7. The HUD
shows 88112 in the photographs before and after the drinks (`C-spray.png`, `A-drinks.png`), and no bubble anywhere
after the lost play. Of the run's seven checks six matched; the seventh, balance unchanged across the spray play, failed on the harness: five gate fees
(125) landed in the same interval, and balance less takings stayed 87987.

### Where OpenTPW differs

`ParkRideOperation.SettleUp` builds steps 0, 1, 2, 4, 5 and 7 and a sideshow winner's count beside the charge, the
prize, the excitement match, the five effects, the ingredient's two docks, gain and fat, salt, ice and sugar
(`TakeTheIngredient`; the sugar feeds `Peep.Pace`, Q177d), a Balloon Shop's balloon (`GiveABalloon`, Q177e; "A held
balloon"), a Costume Shop's costume (`DressOrUndress`, Q177f; "A costume"), the toilet's relief, the winner's cheer and
the lost dock; the fatigue
step is a no-op in both games (said at the site). Each object's six rings and two counts are `ParkObjectRings`, seeded
from its record, fresh for a thing built, dropped with a thing sold; the charge credits today's takings and the door's
refusal counts the walk-away; the charge banks the price and a shop's or won sideshow's cost of goods is booked and
withdrawn ("The cost of goods and the park's money", OpenTPW). The day's change rolls every object the park holds after the frame's turns, on
`GameCalendar`'s edge; that calendar counts from nought rather than from the save's clock (`GameCalendar.Rebase`), so
its days turn at other moments than the original's would after the same load, and it does not roll on the first tick.
The join's snapshot is `Peep.JoinHappiness`. The ride window's Users last month (filled on show and every four
seconds, on the frame clock) and the all-visitors list's Rides Ridden read them. Counted by name: `SETTLE_UP_EVENT_HISTORY`, `_BALLOON_EVENT`, `_COSTUME_EVENT`, `_ANALYSER_SAMPLE` (step 6),
`_SIDESHOW_THOUGHT` and the toilet's three. Its win roll (`PeepBehaviour`, `Succeeds`) and the ingredient's docks (the
ride turn's) draw from `System.Random` where the original draws from the park's generator, and the roll reads the
item's chance where the original reads the object's (Q97).

## Object fields

| Offset | Name | Note |
|---|---|---|
| `+0x20` | model slot | Indexed into `modelSlotTable` by `FUN_004e14e0` |
| `+0x24` | script id | The destructor hands it to the script teardown (`0x004dd2c9`) - see `park.md`, "What selling a thing does to its script" |
| `+0x32` | the flag byte | `FUN_004db090` builds it from the descriptor: `0x01` ProvidesRelief, `0x02` ChillsYouOut, `0x04` IsChoosable, `0x08` HasQueue, `0x10` ProvidesSecurity, `0x20` RideHandlesSprite, `0x40` HoldsLitter, `0x80` IsFireworks - see `park-engine.md`, "Still open" |
| `+0x32` bit 3 (`0x8`) | queue-path flag | HasQueue. Jungle Spray: one queue cell, no bit. Belly Bounce: four cells, bit set |
| `+0x33` bit 0 | RunsContinuously | Descriptor `+0x48`, set by `FUN_004db090`. Lets a ride invite while running |
| `+0x36` | `mEntryPos` | File 206, packed |
| `+0x38` | `mExitPos` | File 218, packed |
| `+0x3a` | `mBackOfQueue` | File 212 |
| `+0x3c` | `mFirstInQ` | |
| `+0x48` | wear | Clamped 0..100 |
| `+0x58` | `mOperatingSpeed` | |
| `+0x5c` | `mOperatingDuration` | |
| `+0x5d` | `mOperatingCapacity` | File 1034 |
| `+0x68` | `mCanLoad` | File 214, 4 bytes |
| `+0x6c` | `mPersonBeingLoaded` | |
| `+0x70` | today's takings, the `mTemp` of the day ring saved at file 361 (`mTemp` itself at 370) | Credited alongside `+0x180`; "The settle-up's bookkeeping" |
| `+0xf8` | today's costs, the `mTemp` of the day ring saved at file 228 (`mTemp` at 237) | The cost of goods booked |
| `+0x180` | `mTotalTakings` | File 1090 |
| `+0x184` | `mTotalCosts` | File 1086 |
| `+0x188` | `mCostOfGoods` | File 1042 |
| `+0x18c` | `mQualityOfGoods` | File 1046; 50 when built, the shop window's quality |
| `+0x190` | `mChanceOfWinning` | Saved with the object (file 1050); derived at placement as `100 - descriptor[+0xec]` (see "The sideshow win roll"). **This is an OBJECT offset.** Do not confuse it with the **person** `+0x190` (`mPreviousX`) in "Where a WALKING peep is drawn" below — different records, same number |
| `+0x194` | `mPricePerUse` | File 1054 |
| `+0x198` | `mAmountOfSpecialIngredient` | File 1058; 50 when built, the shop window's ingredient |
| `+0x1a0` | `mNumCustomers` | File 494 |
| `+0x1a8`, `+0x230`, `+0x2b8`, `+0x340` | today's customers, walk-aways, served and satisfaction, day rings' `mTemp` | Rings saved at file 498, 635, 768, 901; the `mTemp`s at 507, 644, 777, 910 |
| `+0x19c` | `mState` | |

Object records are also read at file offsets 1035 and 1062.

## The script side: how the engine talks to a ride

**The engine talks to a ride through its script's variables, BY NAME, never by index.** A ride archive's companion scripts (`child.RSE`, `effects.RSE`, `EventMap.RSE`) declare none of the common set, while every ride's, shop's, sideshow's and toilet's main script declares all twelve; the other features' and the upgrades' declare none of them. **All thirteen shop and sideshow scripts in the jungle declare the identical common twelve in the identical order**, then all but `Cost_shp` append their own (`VAR_PEEPID`, `VAR_TEMP`, `VAR_TEMP2`, `VAR_LANE1..3` / `VAR_LANERES1..3`, `VAR_TIMER1`, `VAR_SOUND`). `Bouncy` declares all twelve in enum order plus `VAR_TEMP` and `VAR_SCREAMING`.

| Index | Name | Direction and meaning |
|---|---|---|
| 0 | `VAR_LETMEON` | Engine → script **inbox**: admit this guest. The script zeroes it to acknowledge |
| 1 | `VAR_LETMEOFF` | Script → engine **outbox**: this guest has finished. The engine clears it |
| 2 | `VAR_CAPACITY` | Engine → script, pushed by `FUN_004dd7f0` |
| 3 | `VAR_DURATION` | Engine → script, pushed by `FUN_004df8f0` |
| 4 | `VAR_BREAKSTAT` | Breakdown, engine → script |
| 5 | `VAR_ONRIDE` | |
| 6 | `VAR_RIDECLOSED` | Closed |
| 7 | `VAR_BROKEN` | Broken |
| 8 | `VAR_WORN` | Worn |
| 9 | `VAR_RUNNING` | |
| 10 | `VAR_PAD` | |
| 11 | `VAR_PARAM` | The win roll, written by `FUN_004e2670` at every admission |


The inbox/outbox asymmetry is why the polarity looks inverted; it was settled by disassembling a known writer and a known reader beside each other.

**Which opcode writes `VAR_LETMEOFF` depends on the ride — there are SIX, so never say "`UNBOUNCE` writes it" without naming the ride.** Measured by listing all 30 Lost Kingdom ride scripts:

| Opcode | Rides that use it to dismiss |
|---|---|
| `UNBOUNCE` / `FORCEUNBOUNCE` | Bouncy |
| `BUMP 2` | bumper, GoKarts, Wateride |
| `COAST 3` | Coaster1, Coaster3, Minecart |
| `WALKGET` | incagod, Lookout, Totem, tvsim |
| `HOP` + `DELHEAD` | Mumbo, PorkPie, Spider, Volcano, Monkey |
| `TOUR 4` | TourRide |

### How every jungle script dismisses — swept whole, 81 scripts, all five folders

The walk-slot count is the header word at `0x1c`, and it is non-zero for **exactly** the `WALKGET` users.

    WALKGET (10)   balloon 10, giftshop 10, Hyenas 3, incagod 40, Junspray 3, Lookout 20,
                   Squark 1, steak 10, Totem 20, tvsim 20        <- slots at 0x1c
    DELHEAD (5)    Mumbo, PorkPie, Spider, Volcano, Monkey
    BUMP (3)       bumper, GoKarts, Wateride
    COAST (3)      Coaster1, Coaster3, Minecart
    UNBOUNCE (1)   Bouncy
    TOUR (1)       TourRide
    UNLIMBO (3)    arc2x3, Cost_shp, SupBog
    COPY (6)       burger, Coconut, fries, icecream, Puzzle, Toilet

The one-to-one rule — declares walk slots ⟺ uses the walk family — holds park-wide in both directions, and `Bouncy` alone declares bounce slots (10) and no walk ones.

`LIMBO` is used by **6** scripts: arc2x3, balloon, Cost_shp, giftshop, steak, SupBog. `Cost_shp` is LIMBO 1, LIMBOSPACE 1, UNLIMBO 1, FORCEUNLIMBO 1, INLIMBO 0; `Bouncy` is STARTSCREAM 1, STOPSCREAM 2, COAST 0.

### The opcode dispatch

**It is a plain opcode → handler pointer table at `005567d8`.** The dispatch is a bounds check and one indirect jump - `CMP ECX,0x69` / `JA 0x0055679c` (the default) / `JMP [ECX*4 + 0x5567d8]` at `0x00551d3d` - which scans for a switch statement's own table did not find. Entry N is the handler for opcode N; all 106 entries are code pointers. **To find any opcode's handler, read `[0x005567d8 + opcode*4]`.**

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x005567d8` | — | The opcode → handler pointer table. Verified with a control: entries 70..75 land exactly on six functions named earlier by decoding their bodies. | Table read + control |
| `0x00765280` | — | A table of `{name, operand-count-as-a-STRING}`, **106 entries, `NOP` to `SPARK`** (record 106 is not one). Confirms operand counts (WALKON 7, BOUNCE 2, WALKST_FLOAT 3, WALKFLOATSTOP 0). **Its ORDER matches the handler table's**, which is what confirms the numbering. | Table read |
| `FUN_00551600` | `RSSE_Initialise` | `"Tried to Initialise twice"`. Its only use of the opcode name table is to SUM ITS CHARACTERS into a checksum at `DAT_00879190`. Not a dispatch. | Its own string |
| `FUN_005597a0` | `RSSE_Load` | `RSSE` magic `0x45535352`, per-script mallocs, then the `OBJ ` list `0x204a424f`. Allocates walk slots as `count << 5`. | Disassembly |
| `FUN_0055abf0` | — | The per-tick driver, 5,712 bytes, the largest in the region; walks the TOUR ride records at `[0x8791f8]` (slots 1..99, `0x122c` bytes each, twenty cars at stride `0xe4`, made by `FUN_0055a620`). Contains **no CMP/SUB against 73..78 at all**, so it is not the decoder. | Disassembly |
| `FUN_00557a70` | — | Fetch the next operand word: advances the IP at `+0x3c`, bounds-checked against `+0x50`, logging `"Tried to read outside scri…"` and parking the IP at `0xffffd8f0`. | Its own string |
| `FUN_005573a0` | — | Resolve an operand: tagged `0x40000000` indexes the variable array at `+0x1c`, otherwise sign-extend the low 16 bits. | Disassembly |

Script-frame layout used by the families below:

| Offset | What it is |
|---|---|
| `+0x1c` | variable array |
| `+0x24` | limbo records (header count at `0x14`, 8-byte records) |
| `+0x28` | bounce records (header count at `0x18`, 16-byte records) |
| `+0x2c` | walk slots (header count at `0x1c`, 32-byte records) |
| `+0x3c` | instruction pointer |
| `+0x50` | script bounds |
| `+0x64` | bounce slot count |
| `+0x6c` | 16-bit bouncing tally (`BOUNCING` sign-extends it) |
| `+0x6e` | the field `BOUNCESETBASE` writes; read only by the positioner |
| `+0x70` | bounce node base, written by `BOUNCESETNODE` |
| `+0x7c` | walk slot count |
| `+0xc0` | the script's **speed word**, a short the loader sets to 50 (`MOV word ptr [EBP+0xc0],0x32`); the same one `WAIT` divides by |
| `+0xc8` | the script's **model** handle, `[ThingArray[+0xac].ptr+0x20]` (loader `0x00558d5e`..`0x00558d68`); the thing index is `+0xac` |
| `+0xd0` | the held scream handle |

## The WALK family

The slot array is the script's `+0x2c`, counted by `+0x7c`, **`0x20` = 32 bytes a slot** — sized by the header's `0x1c` field and confirmed a third way by `RSSE_Load`'s `count << 5`.

| Slot offset | What it is |
|---|---|
| `+0x00` | walk node |
| `+0x02` | head node |
| `+0x04` / `+0x06` | the walk-off pair: off-from, off-to |
| `+0x08` | start, a game-clock reading in milliseconds |
| `+0x0c` | due, the same clock |
| `+0x10` | visitor handle |
| `+0x14` | facing octant |
| `+0x16` | action |
| `+0x18` | state |
| `+0x1a` | flags: bit 0 takes the height along the line between the two nodes, and every shipped `WALKON` passes 1; without it (dead by CONTENT) the height is `FUN_004511a0`'s under the point plus header `+0xa0`, in the mode `FUN_00450ea0` sets for the ride's model: its surface meshes (with those of any lookup record flagged `0x2`), or the landscape when header `+0x30` carries `0x80` or `+0xa0` is not zero |

**The machine is `0 free → 1 walking on → 2 carried → 3 walking off → 4 done`**, and the script drives only the ends of it.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00555963` | `RSSE_WALKON` | Handler. | Dispatch table entry |
| `0x00555b0c` | `RSSE_WALKOFF` | Handler. | Dispatch table entry |
| `0x00555b34` | `RSSE_WALKGET` | Handler; writes the result back into the operand's variable slot when the operand carries the `0x40000000` tag — the same outbox shape `UNBOUNCE` has. | Dispatch table entry |
| `FUN_00556f40` | — | `WALKON`'s implementation. Resolves the walk node in space `0x800`, and the head node in `0x80` when the action is 4, else `0x800`; takes the first slot whose STATE is 0; stores the handle, both node pairs, the action and the flags; start = the clock, due = the clock read again + the leg (below, `0x0055709b`..`0x005570af`); the facing (below); **state 1**. Its complaints ("Bad walknode", "Walknodes need a `setwalk`", "WALK: Could not add peep") go to `FUN_005da3c0`. | Disassembly |
| `FUN_005571a0` | — | `WALKOFF`: the first slot in any state but 0 holding that handle, so a rider still walking on is cut short and one already done is sent round again. Resolves off-from in `0x80` when the action is 4, else `0x800`, and off-to in `0x800`; a new leg the same way (`0x00557276`..`0x00557286`); the facing; then due, then start, from the clock (`0x005572db`, `0x005572e8`); particle `0x13` at off-from for action 2; for action 4 in state 2 the rider is let go of the head node (`FUN_0044b4c0`); **state 3**. | Disassembly |
| `FUN_00557110` | — | `WALKGET`: scans for a slot in **state 4**, clears its state and handle, returns the handle — 0 if none. | Disassembly |
| `FUN_00557d80` | — | The stepper, for one script. Per slot, progress = `(now × 1000 − start × 1000) / (due − start)`, unsigned; at **≥ 1000** state 1 becomes **2** and start becomes now (`0x00557e79`; due is left as it was) — action 4 attaches the rider to the head node (`FUN_0044b410`), action 2 spawns particle `0x13` there — and state 3 becomes **4**, writing nothing else (`0x00558018`). A carried rider (state 2) never advances; for an action other than 1, 2 or 4 its facing is taken again every frame from the head node's direction row. | Disassembly |
| `FUN_00557ab0` | — | Called once a park frame, after the 31 ms catch-up loop, by its one caller (`0x0054fa08`), for **every** script in the list: places the bounce riders (the only reader of `+0x6e`), then `FUN_00557d80`. Nothing tests whether the ride is on screen. | Disassembly |
| `FUN_005580a0` | — | Presentation, with one write: for a carried rider (state 2) while the script's timed effect runs (`+0x80` start, `+0x84` length, set by `FUN_00557160`), it steps the slot's facing `+0x14` with the clock each frame, mod 8 (`0x005583e5`), and shakes the sprite. It interpolates between two node positions, `A + (B − A) × min(p, 1000) / 1000` (x and z; y too under flag bit 0), and calls `FUN_004f9e60` to place the sprite. | Disassembly |
| `FUN_00556b90` | — | Resolves a node id **in the ride's MODEL**, in the space its fifth argument names — `0x800` for a walk node and `0x80` for a head node from the walk family, `0x200` for a sound and `0x100` for particles from `FUN_005573d0`. The position is the translation row (`+0x30`..`+0x38`) of the node's stored matrix (below); a record with no matrix gives (0, 0, 0). On a miss it returns 0 having written nothing (`0x00556d2c`), and its "RSSE: Invalid Node ID" goes to `FUN_005da3c0`. | Disassembly |
| `FUN_005da3c0` | — | The bare `RET` every complaint above goes to (`park-engine.md`, "Every diagnostic string goes to a bare `RET`"): nothing prints or stops. | Disassembly |
| `FUN_004f9e60` | — | Place a sprite. | Disassembly |

**The operand mapping, measured from the push order** (cdecl, right-to-left, so operands 1..7 are `param_2`..`param_8` of `FUN_00556f40` IN ORDER): 1 handle (`+0x10`), 2 walk node (`+0x00`), 3 head node (`+0x02`), 4 off-from (`+0x04`), 5 off-to (`+0x06`), **6 ACTION (`+0x16`, the one tested against 4)**, 7 flags (`+0x1a`). Confirmed by the corpus: action takes only 1, 4, 5, 6 across Lost Kingdom (2 as well elsewhere, in `Ghostshp` and `scitour`), and the two Lost Kingdom scripts passing **4** — `Totem` and `tvsim` — are exactly the head-node case; across the four themes the five passing 4 are `firepit`, `Totem`, `tvsim`, `hoverbot` and `tv_ride`. `WALKON`'s action operand picks the node space: **4 = a HEAD node (space `0x80`)**, anything else a walk node (space `0x800`).

### How long a leg lasts, and where its ends are

**A leg lasts trunc( the distance between its two nodes ) × 100 ms, nought becoming 100.** No operand is a duration.
`WALKON`'s leg runs from the walk node to the head node, `WALKOFF`'s from off-from to off-to. The distance is over all
three axes (`0x00556fce`..`0x00557013`): the x and z differences are each rounded to a float once and multiplied by
themselves unrounded (`FST` at `0x00556fea` and `0x00556ffc`), the y difference squared whole, then `FSQRT`, at the
FPU's precision, which is not measured; truncated by `__ftol` whatever the FPU's mode (`0x0067a830` forces chop), and
times 100 (`0x0055701a`..`0x00557025`). `WALKON` looks both nodes up with its whole operands and works the leg out
before it looks for a free slot, then keeps the ids as 16 bits; `WALKOFF` reads them back sign-extended. Start and due
are readings of the game clock (`0x785970`) in milliseconds, read live at each call rather than from the frame's
snapshot, so two reads back to back can part by one clock step. **The facing** is `trunc( 10.5 − 4θ/π ) mod 8`,
θ = atan2( Δz, Δx ) from the first node to the second, with the build's truncated π constants
(`0x005570b2`..`0x005570fd`, `0x00700fe8`); it turns with the ride.

**The ends are the nodes' stored matrices, in the world.** `FUN_00556b90` reads the translation row of a matrix kept
per node-lookup record: the script's model (`[0x7a4610 + handle × 4]`) `+8` → `+0x28` → `+4`, 20 bytes a record, the
matrix pointer at `+4` (that `+8` object's `+4` is the file header). Only the pose walk `FUN_0044ab30` writes their
positions (`FUN_0044b510` negates the x and z axis rows of an attached record's matrix when its node carries `0x400`,
a half turn about y, and leaves the translation): it composes each node's local transform with its parent's
(`FUN_004702d0`, an affine local × parent, each element the third product plus the second plus the first, the
translation row adding the parent's last, stored as a float, `0x004702ea`), from the root that header `+0x78` names.
The placement (`FUN_00467030`) turns the root's own file rows in place, x' = x·c + z·s and z' = z·c − x·s
(`FUN_0046f650`), and replaces its translation with the world position (`0x004671c5`..`0x004671e3`); each call turns by
the change of angle from the rows as they stand. c and s come from `FUN_004708d0`'s table of 4096 floats
`sin( i / 4096 × 6.2831855f )`: θ = float( angle × 0.017453292f ), the sine's index the float θ × 651.89862 rounded
to the nearest (`0x0046717c`), and the cosine's rounded on its own from ( θ + 1.5707964f ) × 651.89862
(`0x00467194`). A right angle's cosine is so `sin( π )` in float, −8.742278e-8, and at 270° the cosine's 4095.99976
reaches entry 0 only by rounding to the nearest, which the stored positions at 180° and 270° show (below). So the
positions are world positions, and a distance is the model's own but for float rounding: hallow's Rat Race walks
9.9999995 in its file (900) and 10 between world floats at (510, 0, 300) (1000; computed, not measured), and an exact
whole-unit leg can come out 100 short near the map's low edge. A ride's records are stored when it is built
(`0x00463851`), when the ride view is put on it (`FUN_0042a560`, `0x0042a6d6`) and every frame it stays there
(`FUN_0044e410( 2 )`, `0x0054fa96`), and at every animation advance on screen (`FUN_00473c70`, `0x00473e1b`); off
screen, only for a model whose header `+0x30` carries `0x4`, which the Lookout's, the Totem's and the Aztec Mayhem's
(`0x9`) do not. (The track rides' stepper `FUN_0043ce20` also poses some of the car models it moves, through
`FUN_00438800`.)

**Which records have a matrix, and which the walk stores.** A record gets a zero-filled 64-byte matrix only when its
file flags carry `0x10` or `0x20` (`TEST byte [rec],0x30` at `0x0044a904`, `FUN_0044a870`; `FUN_0045b9d0` with 1 is
`GMEM_ZEROINIT`). The walk stores it when the node
has a child, or when the record carries runtime bit 2 (a rider attached, `FUN_0044b410`), 4 (the ride view,
`FUN_0042a560`) or 8 (`0x0044abd0`..`0x0044abd9`); a childless node gets runtime bit `0x20` (`FUN_0044aa10`). The item
loader sets bit 8 on every record whose file flags meet `0x580f00`, which takes in the walk bit `0x800` and not the
head bit `0x80`, and on **every** record of an item that sets `Info.DoHeadProcessing` (`FUN_004629d0` at `0x00462d4a`,
under the loader's `0x400000`, which `FUN_00413c10` takes from the item's `+0x84` at `0x00414136` and `0x0041418c`). A
record never stored reads (0, 0, 0): the Jungle Spray's `camera` record (file flags `0x1031`, runtime `0x21`) does, live.

**Every node the shipped scripts name is stored.** All 173 literal node operands of the 47 `WALKON`s in 37 scripts
(the 53 `WALKOFF`s look the same nodes up again from the slot), across the four themes, name a childless node whose
flags carry `0x10` or `0x20`. Those found in space `0x800` carry bit 8
by their own flags, and every `0x80` lookup belongs to an item that sets `DoHeadProcessing`. The items that set it are
FileFormats `sam.md`'s ("`Info.DoHeadProcessing`"): the five whose scripts pass action 4, and one that walks no one. No lookup misses within
a ride's capacity. So the (0, 0, 0) end and the miss are both dead by CONTENT. The miss would not be harmless:
`FUN_00556b90` returns 0 having written nothing, and the leg is worked out of whatever the stack last held there.

**Measured in the original** (Q175): the Jungle Spray in the reference install's Lost Kingdom park, read live under
Proton (`docs/TOOLING.md`), and every walk slot in Alexah's Full Simulation saves (the ride-script module keeps each
script's slots raw). The Jungle Spray's walk nodes read runtime `0x29` and world positions exactly their rest
positions at the placement (510, 0, 300), its root's own file translation (−0.073, 0, 0.096) replaced by it:
`entrance` (525.127, 0.984, 300.844), `Kid_pos04` (515.931, 0.984, 308.005), `kid_pos02` (525.127, 0.984, 308.005),
`kid_pos03` (534.323, 0.984, 308.005). Every walked leg was the prediction:

| Where | Leg (due − start) | Seen |
|---|---|---|
| Jungle Spray lane 1, on and off | 1100 (11.655) | 13 + 13 live, facings 7 on, 3 off; saves |
| Jungle Spray lane 2, on and off | 700 (7.161) | 59 + 60 live, facings 0 on, 4 off; saves |
| Jungle Spray lane 3, on and off | 1100 (11.655) | 3 + 3 live, facings 1 on, 5 off |
| Hyena Sideshow lanes 1 and 2, off | 1000 (10.06), 400 (4.54) | saves |
| Inca God, off | 800 (8.02) | 12 slots, the same in both saves |
| Gift Shop, Balloon Shop | 1000 (10.0) | saves |
| Steak Shop | 600 (6.18) | saves |
| Big Apple, SquirtEm lanes 1-3, Frushy lane 1 (Wonder Land) | 2100, 700 / 200 / 700, 800 | saves |
| Aztec Mayhem, heads 1-5, on / off | 1400, 1300, 900, 1000, 2000 / 2000, 900, 1300, 1700, 1500 | Q175b: 15 + 10 live |

All 90 arrivals seen live (75 in Q175, 15 in Q175b) set start to the moment of arrival, at or after due. A finished
slot keeps its last leg, which is why the saves show a ride's walk-off leg.

**Measured again for the build (Q175b)**: an Aztec Mayhem bought in the reference install's stock park at cell (40, 22)
and watched in memory, on screen, walked the rest-pose legs over three boardings, the first two walked off before the
watch ended. Its heads were rising (y 2.372 and 1.561 at two arrivals) and were at rest (y 1.159) at the start of every
walk, which by the script comes while channel 0 holds the I clip (no tracks) or E's last frame. With Alexah's jungle
save loaded (`TOOLING.md` 12), the pose stored at the load of the 49 walk and head records its eight walk things pose,
turned 0, 180 and 270, is OpenTPW's composition in x and z to six decimals (98 values), and in y but for each thing's
ground height. 31 of the Inca God's 32 heads read (0, 0, 0) with runtime `0x21`; `head08`, which its `ADDHEAD` had
attached a rider to (`0x23`), held a position.

**Lost Kingdom's legs**, every other one from the same rule: `incagod` 1700 on and 800 off; `balloon` and `giftshop`
1000; `steak` 600; `Hyenas` 1000, 400 and 1000 by lane; `Junspray` 1100, 700 and 1100; `Squark` 500. `Lookout`,
`Totem` and `tvsim` take the head node from a variable, one head a rider (`VAR_ONRIDE` plus 1 or 2), and their heads
ride an animated ancestor (the lookout's lift, the totem's cart, the simulator's seats), so their legs depend on the
pose at the moment: from the rest pose, `Lookout` 800 to 2500 on and 800 to 2600 off, `Totem` 1200, 1000 or 800 on
and 100 off (exactly 1.0), `tvsim` 500 to 2000. No walk, entrance, exit or off-to node moves in any clip of its own model (the roles
`FUN_00461f10` loads), and no Lost Kingdom walk node at all outside those three.

**OpenTPW** (`RideScript.Leg`, `RideNodes`) finds both ends with `ModelFile.FindNode` on the thing's own model and
takes them where the build's pose stores them, composed in floats at the thing's cell and turn as above. A node riding a
clip-driven ancestor stands at rest, counted `WALK_LEG_REST_POSE`: the engine's is the last drawn frame's, and every
shipped walk comes at a pose that gives the rest-pose legs (the Aztec Mayhem's measured; the Lookout's lift 0.094 and
the Totem's cart 0.063 from rest, worked out), but a ride left off screen mid-cycle keeps that frame's pose. A head the
engine takes from a face of a morphing mesh is counted `WALK_NODE_ON_A_FACE` (none in Lost Kingdom). A miss, an unposed
record, a negative id and a walking script with no model, which the engine's stepper does not survive, are counted
(`WALK_NODE_MISS`, `WALK_NODE_UNPOSED`, `WALK_NODE_NEGATIVE_ID`, `WALK_NODES_NO_MODEL`) and walk the shortest leg. It
also steps a script's walks inside that script's turn, where the engine steps every script's once a frame (Q183).

**Walk-slot declarations across Lost Kingdom** (header word `0x1c`): incagod 40; Lookout, Totem, tvsim 20; balloon, giftshop, steak 10; Hyenas, Junspray 3; Squark 1. `WALKGET` appears in all of them, which makes it the corpus's dominant dismissal.

### The sideshow's own script

`Junspray.RSE` lives **inside `data/levels/jungle/sideshow/junspray.wad`** — a bare `sideshow/Junspray.RSE` path does not exist. 112 instructions, 18 variables: the common twelve plus **`VAR_LANE1..3`** and **`VAR_LANERES1..3`**. Per lane, and all three lanes are identical but for operands 3 and 4:

```
TEST VAR_LANEn / BRANCH_NZ        lane busy -> try the next
COPY VAR_LANEn, VAR_LETMEON       remember who is on it
COPY VAR_LANERESn, VAR_PARAM      <-- the win/lose roll, from FUN_004e2670
WALKON VAR_LETMEON, 4, n, n, 4, 6, 1
COPY VAR_LETMEON, 0               clear the inbox
ADD VAR_ONRIDE, 1 / WAIT 1000 / EVENT / ADDOBJ
TEST VAR_LANERESn -> one of two TRIGANIM_CH  (won or lost)
...
GETANIM_CH 0,n / BRANCH_PV        wait for that lane's animation to finish
KILLOBJ / EVENT 1,4,74 + EVENT 5,65535,212 on a WIN
WALKOFF VAR_LANEn / COPY VAR_LANEn, 0
TEST VAR_LETMEOFF / BRANCH_NZ
WALKGET VAR_LETMEOFF              pop one who has finished; zero if none
ADD VAR_ONRIDE, 65535             i.e. MINUS ONE, two's complement
```

It declares **3 walk slots** and dismisses with `WALKGET VAR_LETMEOFF` at instruction 320. So the engine rolls the outcome into `VAR_PARAM`, the script copies it per lane, and plays a winning or a losing animation — two independent decodes joining up.

## The BOUNCE family

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x005555d9` | `RSSE_BOUNCESETNODE` | Opcode **70**. Writes **`+0x70`**, the node base, and takes its operand **RAW** (no `0x40000000` tag test). | Dispatch table + body decode |
| `0x00555618` | `RSSE_BOUNCESETBASE` | Opcode **71**. Writes **`+0x6e`**, a separate 16-bit field read only by the positioner `FUN_00557ab0` — presentation, not bookkeeping. | Dispatch table + body decode |
| `0x00555674` | `RSSE_BOUNCE` | Opcode **72**, 2 operands. Only RECORDS a rider; it places nobody. | Dispatch table + body decode |
| `0x005557a3` | `RSSE_UNBOUNCE` | Opcode **73**. **WRITES** its operand (an outbox), it does not read it as "who to remove". | Dispatch table + body decode |
| `0x00555842` | `RSSE_FORCEUNBOUNCE` | Opcode **74**. Drops the deadline test and **keeps the release window**. | Dispatch table + body decode |
| `0x00555910` | `RSSE_BOUNCING` | Opcode **75**. Sign-extends the 16-bit tally at `+0x6c`. | Dispatch table + body decode |

**The two setters are swapped relative to their names**: `BOUNCESETNODE` writes the node base, `BOUNCESETBASE` writes a presentation field nothing in the family reads.

The record is **16 bytes**: handle `+0`, node `+4` = `*(EBP+0x70) + slotIndex`, expiry `+8` = `now + seconds*1000`, start `+0xc`. A free slot is handle nought; scan from the front, same as limbo.

**RELEASE RULE:** the deadline **strictly** past **AND** `(elapsed % 1000) / 200 == 0` — only the first fifth of each second. Sane because the scripts **poll** it every pass, so it costs at most a second. **The 200 was read off the code:** `0x51eb851f` (2^37/100) with **`SAR 6`** = shift 38 = ÷200; the canonical ÷100 is `SAR 5`. That byte pair occurs **exactly twice in the whole executable**, both in the unbounce arms. `FUN_004fde50` is a `SAR 5` site and renders ÷100. Matching on `f7 ea c1 fa 06` alone finds `(ptr - base) / 0xd0`, an unrelated idiom.

**Identified by one-to-one agreement across all 308 scripts:** the scripts declaring bounce slots are exactly the scripts using `BOUNCE`. `0x14` and `0x1c` hold different sets, so a wrong offset gives somebody ELSE'S answer rather than nonsense.

**One bouncing ride per theme, all different:** jungle `Bouncy.RSE` (10 slots, base 8), space `Bouncy.RSE` (**12**, base 16), hallow `Brainb.RSE` (12, base 6), fantasy `Jelly.RSE` (10, base 12). **Two themes share the FILENAME with different contents** — a corpus walk matching on name alone returns whichever theme it reaches first. Ask by path. **`Jelly.RSE` uses `BOUNCESETNODE`** (literal 3); every shipped use is a literal, which is what makes the raw store unobservable from the corpus.

`Bouncy.RSE` calls `BOUNCESETBASE 8` and never `BOUNCESETNODE`, so the node base stays at the 1 the script loader gives every script (`0x00558c45`) and its riders' node ids are **1..10**, `body` to `body09`.

## Where a rider is drawn

**The simulation never moves a rider; the drawing places them.** None of `FUN_004fa930`'s five callers is a rider, so a rider's world position legitimately stays on the cell they queued on, and the RENDERER draws them at a node of the ride's own model.

- **`bouncy.MD2`'s rider nodes are `body`, `body01`..`body09`, ids 1..10** — ten, matching the ten declared slots, sitting 9-12 units up.
- **The engine finds them by id in the walk space `0x800`**: `FUN_00557ab0` hands `FUN_0044b220` the bounce record's node with `0x800` (`0x00557b3a`..`0x00557b54`; the lookup is `audio.md`'s "Node lookup is by id AND a capability flag"). An id is unique only together with its flag. In that one model id 1 is `body` (`0x10000851`), `air` (`0x171`), `camera` (`0x1031`) and `body11` (`0x411`), and only `body` carries `0x800`.
- Park objects load at the origin and are afterwards moved, so a node position must have the placed rotation and origin applied; the load origin is not the placed one.
- `UsageInfo.RideHandlesSprite` is the flag "If the script handles the person sprite".
- **Measured in the running game**: node 0 of thing 13 resolves to **(525.4, 252.6, 10.3)**, against the queue front at (525, 234).

## Where a WALKING peep is drawn, and it is INTERPOLATED per frame

**The simulation moves a peep once every eight ticks; the renderer slides them there across the frames
between.** This is a wholly different path from the rider case above — it never touches `FUN_004f9e60` —
and it is the answer to why a peep would otherwise step rather than walk.

| Address / offset | What it is | Evidence |
|---|---|---|
| person `+0x190` / `+0x194` | `mPreviousX` / `mPreviousY` — the position as it stood at the **last** thing sweep. `+0x194` is the one that pairs with the Z axis. | Named by the person-base serialiser `FUN_004f8b10` |
| person `+0xd4`, its `+0x8` / `+0xc` | the mover sub-object's live position, 16.16 fixed point, `0x10000` = one cell (the constructor seeds `(cellX << 16) + 0x8000`, the cell centre) | Disassembly |
| `FUN_004fa870` | Works out the speed first: the words `+0xc2`, `+0xc0` and `+0xc4`, each zero-extended, summed and divided by the word at `0x0075c7fc` (100, the middle of `{60, 80, 100, 120, 140}` at `0x0075c7f8`), then `(that − +0xc8 × −3.0) × 0.25` (`0x007006f0`, `0x007006f4`), a quarter of the way on from `+0xc8`; stored back at `+0xc8` and passed to `FUN_00510190`; then `+0xc4` becomes `((+0xc4 × 99) & 0xffff) / 100`, a 16-bit `IMUL` (`0x004fa8ea`), one less a sweep below a hundred. Then it stamps `previous := current` with `FUN_00510160( person+0x190, person+0x194 )` — a **thiscall** on the mover (`person+0xd4`), so the decompiler drops `ECX` and it reads as two args — and ends with `FUN_004d4190` on `person+0xc`. It is **the first call of every person kind's tick handler** — `FUN_00501650` at `0x00501658` (guests), `FUN_00505490` at `0x00505495` (staff) — and in the guest handler it sits **ahead of the `(id & 3)` needs stagger**, so it is unconditional: every peep, every sweep. Straight-line, no early return. A third caller, `0x004f7c63`, is the online person's (model 18). OpenTPW eases a guest's speed (`Peep.Pace`, Q177d), in single precision (`park-engine.md`, "Which rounding is live"); staff keep the saved one or, hired, a rested member's 1.4 (Q136). | Disassembly |
| `FUN_00510190` | A thiscall on the mover with one float, held to at most 2.0 (`0x007009a0`; the argument only, `+0xc8` keeps its own), then `max_force` `+0x18` = `__ftol( s × 26214.4 )` and `max_speed` `+0x1c` = `__ftol( s × 13107.2 )` (the doubles at `0x007009a8`, `0x007009b0`), each at least 655 (`0x28f`, `0x005101d0`..`0x005101e3`); `__ftol` truncates. One caller, `0x004fa8d9`. The steering step reads both from the mover every step (`0x0050f3f8`, `0x0050f450`, `0x0050f496`), and two behaviours cap with `max_speed` (`0x0050f10b` in follow_path, `0x0050def9`), so the speed written at the top of a sweep governs that sweep's walk. The save's two fields are exactly this of `mPreviousSpeed` on all 18 people of the shipped park and all 392 of each of Alexah's two played Lost Kingdom saves. | Disassembly, `read_memory`, the saves |
| person `+0xc0`, `+0xc2`, `+0xc4`, `+0xc8` | `mBaseSpeed`, the hurry `mPurposeSpeed`, `mAdjustorSpeed` (words) and `mPreviousSpeed` (a float): file 34, 236, 32 and 220, all four loaded (`0x004f91a6`, `0x004f931e`, `0x004f9161`, `0x004f9282`). A guest's base is drawn `% 5` from the table as the person base is made (`FUN_004f8940`, `0x004f89e1`..`0x004f89f7`; the played saves hold 74, 56, 70, 62 and 77 guests at the five), the guest's constructor sets the hurry to 25 (`0x004fb1c9`), and the speed starts at 0.0 (`0x004f89ab`), so an arrival walks off slowly. Staff get 60 or 100 at hire (`FUN_00504b90`), an entertainer 60, and a base by rest in the staff decide (`FUN_00506a40`, Q136). | Disassembly |
| `FUN_004f9f00` | **The blend.** `0x004f9f89`–`0x004f9fd2`: `MOV EAX,[ESI+0x194]` / `SUB` / `FILD` / `FMUL [ESP+0x14]` / `FIADD`, then the identical six instructions for `[ESI+0x190]` — i.e. `prev + (cur − prev) · t` per axis. | Disassembly |
| — | **Height is forced to nought, not interpolated**: `0x004f9ffd MOV dword ptr [EAX],0x0`. The ground under the sprite is resolved separately. | Disassembly |
| — | **Facing is NOT interpolated**: `0x004fa015 MOV EDX,[ESI+0x1c]` goes straight to the out-param. So a peep's position glides while its octant **snaps** at sweep boundaries. | Disassembly |
| — | Both axes leave scaled by `FMUL [0x00700698]` (**10.0**, world units per cell) then `FMUL [0x007006d8]` (**1/65536**). | `read_memory` + the file on disk |
| `FUN_00518f90` | The per-frame driver, single caller `0x0054fa85` — **past the 31 ms catch-up loop's back edge at `0x0054f8da`**, so once per frame and not once per tick. Walks the same thing list the sweep walks; admits kinds 1, 4, 5, 6, 7, 8 and `0x12`. | Disassembly |
| `t` | `0x0054fa5c`: `[0x008786bc]` (now, sampled once a frame) minus `[0x00878a1c]` (the baseline), `FILD`, then `FMUL [0x00700f94]` = **1/248.000007**. | Disassembly + file |
| the baseline | `0x0054f683` writes `[0x00878a1c]` from `[0x00878c74]` **inside** the every-8th gate `0x0054f668 TEST byte ptr [0x00877d34],0x7 / JNZ 0x0054f82d`. So `t` is literally "how far through the current thing sweep are we", and 8 × 31 ms = **248 ms** exactly. | Disassembly |
| the clamp | The placement sample clamps to **[0.0, 1.0]**, readable as immediates rather than data (`0x004f9f3a` stores `0x3f800000`). The other mode, a trailing sample at `t − 0.35`, clamps to **[−1.0, 2.0]** (`0xbf800000`, `0x40000000`). | Disassembly |
| `FUN_004fa930` | The teleport **re-stamps** `previous := current`, so a placed peep does not streak from wherever it used to be. | Disassembly |

**It is an engine-wide convention at three rates, off one shared "now".** All three alphas are computed in
the same per-frame block from `[0x008786bc]`, each against its own baseline and its own reciprocal:

| Constant | Value | Baseline | Driver | What it paces |
|---|---|---|---|---|
| `0x00700f8c` | 1/31.000001 | `[0x00878c74]` | `FUN_00519060` | placed objects, track rides |
| `0x00700f90` | 1/62.000002 | `[0x0087879c]` | `FUN_0055ce60` | particles and models |
| `0x00700f94` | **1/248.000007** | `[0x00878a1c]` | `FUN_00518f90` | **peeps and staff** |

**The every-2nd-tick sprite step moves NOBODY — a clean negative worth keeping.** `FUN_00475360`'s only
callee is the sprite-script VM `FUN_00475010`, and a sweep of all 698 instructions of the handler region
`0x00476000`–`0x00476a40` plus its four helpers touches the instance's position `+0x88`/`+0x8c`/`+0x90`
**nowhere**. That 62 ms beat advances which frame of the walk cycle is drawn, and never a position.

**`mLastPosX`/`mLastPosY` are NOT this pair, and reading them as a previous position is a trap.** They are
live at person `+0x218`/`+0x21c` (save 430 / 434), and their only live reader is `FUN_004fe900`, gated on
`+0x210`: it places a *secondary* sprite at the pair's old value and only then overwrites them, which is
one frame of deliberate lag for something trailing its owner. **That something is the held balloon**:
`+0x210` is the sprite the settle-up's balloon arm builds, and `FUN_004fa030` places it through `FUN_004fe900` each frame
for a guest holding one (`0x004fa184`..`0x004fa196`), so it trails a frame and a third of a sweep behind ("A held balloon", 3); the
height term shortens as the owner moves. The reads of `+0x210` in
`FUN_005019f0` case `0x11` are off a staff record, so they do not question that name ("The guest record, as named by the game's own save reader" above). It does not bear on the walking case either way.

### What these constants cost to confirm

`run_python`'s `memory.getBytes`, `api.getBytes` **and** `memory.getBlock` all report **no block** at
`0x00700f94`, while the `read_memory` tool reads it immediately and the file on disk agrees byte for byte.
`list_segments` lists Ghidra's own memory blocks and puts the address inside an initialized `.rdata` block,
while the reader denies it exists. Every constant above was therefore taken **twice** — once
through `read_memory`, once out of the executable — and `docs/VERIFYING.md` rule 102 records the trap.

## The SCREAM family

All of it plays from **`cat_kids`** — `DAT_00803a24`, named outright by `Sound_RegisterGlobalCategories`, whose slots are **not in address order**: `0x803a20` ambient, `0x803a28` rides, `0x803a2c` ui, **`0x803a24` kids**, `0x803a30` staff, `0x803a34` speech. Guessing the name from the address gives `cat_rides` and is wrong.

The family's dispatch-table handlers sit at **`0x00555e5e`** (86), **`0x00555ef7`** (87), **`0x00555f1b`** (88) and **`0x00555fda`** (89), in the same `0x555…` region as the bounce and walk handlers. Like the walk handlers, all four are reached through the pointer table like every other opcode, and Ghidra's auto-analysis leaves them undefined bytes; in the project they are functions named `RSSE_STARTSCREAM`, `RSSE_STOPSCREAM`, `RSSE_SINGLESCREAM` and `RSSE_SCREAMLEVEL`. Read them out of the table rather than hunting them: the jump table is at **`0x005567d8`**, so opcode *n*'s handler is the dword at `0x005567d8 + n*4`.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00551130` | `STARTSCREAM` | Opcode **86**, 2 operands. **Refuses if a scream handle is already held**, logging `"RSSE: Started screaming without stopping first."` (`0x00765a4c`), and then stores the refusal's 0 over `+0xd0` (`0x00555ee6`), so the old chain screams on unstopped. The first operand bands the sample: 0 plays nothing, 1 → effect **0x47**, 2-3 → **0x48**, 4-7 → **0x49**, 8+ → **0x4a**. Straight after the play it sets the voice's **parameter 6** to `(second operand + speed) / 2` via `FUN_0051bc40` (`0x00551261`..`0x00551265`). The handle is kept on the script at **`+0xd0`**. | Its own string |
| — | `STOPSCREAM` | Opcode **87**, 0 operands. Calls `Sound_StopFading` on the held handle when there is one, and clears `+0xd0` (`0x00555ef7`..`0x00555f0a`). For a held scream that is a **hard cut** of its chain's newest child, fading on or off - see `audio.md`, "How the engine plays an effect". | Disassembly |
| `FUN_00551320` | `SINGLESCREAM` | Opcode **88**, 2 operands. A **4×4 grid**: the same first-operand band crossed with `(a+b)/0x32` clamped 0..3, giving ids **0x4b..0x5a**. So 71-74 are the held screams (chains of one-shot children, `audio.md`) and 75-90 the one-shots. **It applies NO volume and keeps NO handle** — every arm calls `Sound_PlayEffect` and returns it, and the handler at `0x00555f1b` throws the result away rather than storing it at `+0xd0` or `+0x48`. Fire and forget. | Disassembly |
| `FUN_00551560` | — | `SINGLESCREAM`'s **negative branch**, `if ( operand2 < 0 )`: picks on band alone — 1 → **0x69**, 2-3 → **0x6a**, 4-7 → **0x6c**, 8+ → **0x6d** — and sets no volume. **0x6b is skipped; that is the original's own gap, not a transcription slip.** | Disassembly |
| `FUN_00551290` | `SCREAMLEVEL` | Opcode **89**, 1 operand. `FUN_00551290( handle, operand, speed )`: re-sets **parameter 6** of the scream ALREADY held, by the same `(a+b)/2` clamp (`0x005512b8 PUSH 0x6`). It does nothing at all when no handle is held. | Disassembly |
| `0x00556009` | — | **`SCREAMLEVEL` overwrites the scream handle with the PARAMETER CALL's return value** — `MOV dword ptr [EBP + 0xd0],EAX` straight after `CALL 0x00551290`, whose own return is `FUN_0051bc40`'s, which is `FUN_006b5b80`'s, which is a bare virtual call that Ghidra types `void`. **What lands in `+0xd0` is not proven from this executable.** The engine's own music and kids-91 code stores the same call's return back into its handle global (`0x0051e75f`, `0x0051e808`), the same idiom, so it is most likely the handle itself. Do not reproduce this without saying so. | Disassembly |
| `FUN_0051bc40` | — | Sets a sound parameter on a handle. For a held scream, parameter 6 binds to the voice's slot 0, which picks each later child's variation (`0x006c3e1c`). **Whether anything also reads it as a volume is not established.** | Disassembly |
| `FUN_00466b70` | — | The sound position: indexes `DAT_007a4610` by the script's model handle and fills SIX floats — two points with heights from the model's `+0x1c`/`+0x28`. **It is the RIDE's position, never a rider's.** | Disassembly |
| `+0xc8` | — | The script's model handle, which is where the position comes from. | Disassembly |

**Parameter 6 is `(operand + the script's SPEED) / 2`, clamped 0..100.** `+0xc0` is not a scream field. It is the script's speed word, a short the loader sets to 50, the same one `WAIT` divides by. All three instructions READ it and none writes it. So `Bouncy`'s `STARTSCREAM VAR_TEMP, 20` at default speed gives `(20+50)/2 = 35`, which the zones of all four scream effects send to **variation 2**. The first child of a chain always plays variation 1. The only reader found is the variation pick, and OpenTPW's use of it as a gain is a choice, Q43. `SCREAMLEVEL` does not write the speed field.

`Bouncy` passes `SINGLESCREAM VAR_ONRIDE, 65535`, and 65535 as a SHORT is **-1**, so it takes the negative branch. The handler chooses between the two at `0x00555f6b` (`CMP ESI,EDI` against a zeroed EDI, then `JGE`), so the test is on the **second** operand and `>= 0` takes the grid.

**Both branches are live in shipped content, and the split is lopsided**: of the **46** uses, **44** pass 65535 and take the negative branch, while **`jungle/rides/monkey.wad/Monkey.rse @259`** passes **90** and **`jungle/rides/totem.wad/Totem.RSE @136`** passes **100** — the only two that reach the 4×4 grid at all, and both in jungle. A reading that called the grid dead would be wrong.

**Who uses the family — counted fresh over all 306 wads and all 308 scripts, 0 rejected:**

| opcode | uses | scripts |
|---|---|---|
| `STARTSCREAM` | 40 | 40 |
| `STOPSCREAM` | 80 | 41 |
| `SINGLESCREAM` | 46 | 44 |
| `SCREAMLEVEL` | **81** | **36** |

**Match the extension case-insensitively:** `Monkey.rse` is lower-case, and a case-sensitive `.RSE` match drops it silently. Jungle alone is **8** `STARTSCREAM` scripts: Bouncy, incagod, Lookout, **Monkey**, Mumbo, PorkPie, Spider, Volcano. `SCREAMLEVEL` has the most uses of the family, 81, in the fewest scripts, 36.

Only **Bouncy** is placed in Lost Kingdom, and **Bouncy never calls `SCREAMLEVEL`** — so that opcode is dead by CONTENT there while being unavoidable corpus-wide.

**The category listing corroborates the decode from an unrelated direction**: `global/sound/kids` declares `[105x1, 106x1, 108x2, 109x1, … 71x25, …]` — exactly the `0x69`, `0x6a`, `0x6c`, `0x6d` of the negative branch, **with 107 (`0x6b`) absent**, matching the gap in the original's own switch. That gap was read off the disassembly before the category was ever loaded. `cat_kidsSFX.map` declares 71-74 at offsets 312/332/352/372, a 20-byte stride.

## How a scream VARIES, and it is the script that does it

The variety is not inside the sound engine. It is `Bouncy.RSE`'s own subroutine at **193**, reached by `JSR ->193` from **22** and **116** — that is, on **every pass** of the ride loop:

```
193  BOUNCING     VAR_TEMP        ; how many riders are bouncing NOW
195  CMP          VAR_SCREAMING, VAR_TEMP
198  BRANCH_Z     ->207           ; unchanged? return, leave the scream alone
200  STOPSCREAM
201  STARTSCREAM  VAR_TEMP, 20    ; band = the RIDER COUNT
204  COPY         VAR_SCREAMING, VAR_TEMP
207  RETURN
```

So **the band operand is the rider count**, and the scream is torn down and restarted whenever that count changes, even inside one of `STARTSCREAM`'s own bands (1, 2-3, 4-7, 8+). `COPY VAR_SCREAMING, 65535` at instruction **17** seeds the cache with -1, a value `BOUNCING` can never return, so the first pass always starts one. This is also why `STOPSCREAM` outnumbers `STARTSCREAM` two to one across the corpus: the pair is a restart idiom, not a start/stop pair.

**No shipped script starts a scream over a held one.** Walked over all 308 from word 0 (`q176/screamwalk.py`), every path with an exact `JSR` stack and both arms of every branch, the held bit carried per path: each of the 40 `STARTSCREAM`s is reached only with `+0xd0` nought, because a `STOPSCREAM` stands between it and any earlier start on every path. The control, a `STOPSCREAM` that keeps the handle, finds all 40 held. A load does not change that: the save reader reads `+0xd0` back with the struct and does not clear it (`FUN_005597a0`), so a script saved screaming resumes holding the saving session's handle, and its next `STOPSCREAM` clears it before any start. What `Sound_StopFading` does with that stale handle is not traced. Alexah's Full Simulation saves hold 12 such scripts (Spider, Inca God, Pork Pie and Bouncy in the jungle, Flowspin and Big Apple in fantasy, each in two saves), and each resumes onto a path that reaches a `STOPSCREAM` first. **A chain let go screams until something stops every voice**: the park's end, or accepting the options after an audio-quality change (`0x00423847`); nothing else reaches it, since nothing else in the script system reads or writes a script's `+0xd0`. **OpenTPW builds the refusal** (Q176): `ParkAudio.Scream` refuses a script already holding a scream, logs the engine's line, and lets the held chain go (`ParkScreams.LetGo`), so it screams on held by nothing, missed by the script's `STOPSCREAM` and by the flat destructor (`RideScriptScheduler.Release`, which stops a held one), until the park ends: OpenTPW's options restart no sound. The `rides` census counts these as `screams let go`. `RideScript.Screaming` asks `ParkAudio` what the script holds. A loaded script holds no scream here.

## A held scream is REPLAYED, by a chain in the executable

**`Sound_PlayEffect( handle, category, effect, x, y, z )` has no loop parameter**, and QMixer is never asked to loop: its one `QSWaveMixPlayEx` call passes `nLoops` 0 (`0x006d2463`, `0x006d2470`). What makes `STARTSCREAM`'s handle go on screaming is the **effect's flags word**. Effects 71-74 carry `0x0404`, which builds a voice that plays nothing itself and keeps a chain of one-shot children. Each child gets a fresh variation, a fresh sample and a fresh wait, until the handle is stopped. `SINGLESCREAM`'s effects carry no bit `0x4`, so each is a one-shot that dies after its sample. The whole mechanism is in `audio.md`, "How the engine plays an effect: priority, not a repeat delay".

`DAT_00802bcc` is a forwarder, written through a pointer, which is why it shows only reads; the play is the manager's `+0x10`, `0x006b87d0`. `FUN_006bf330` opens both map files, but it is the whole category load, reached only from registration and never from a play.

**What the shipped data says.** From `data/global/sound/cat_kidsSFX.map`:

| effect | variations | samples | wait between children | record `+0xc` (a priority, not a delay) | sample length |
|---|---|---|---|---|---|
| 71 (`0x47`) | 4 | **25** | **1000-3000 ms** | 2700 | 392-1341 ms, mean 771 |
| 72 (`0x48`) | 4 | **50** | 500-2000 ms | 2700 | 294-3030 ms, mean 805 |
| 73 (`0x49`) | 4 | **60** | 100-1000 ms | 2700 | 310-3378 ms, mean 847 |
| 74 (`0x4a`) | 4 | **59** | 0-500 ms | 2700 | 284-4400 ms, mean 888 |

The wait is drawn per child and counted from that child's start, so a busy ride screams more often and can overlap itself. A chain belongs to one handle, and nothing is kept per effect. **Two rides in one band each scream on their own clock, and one stopping leaves the other exactly as it was.** OpenTPW keeps one chain per ride (`ParkScreams`, Q9).

**The record's fifth int is not a loop flag.** It looks like one in `cat_kids` alone, where it is 0 for every one-shot and `0x00060404` for all four scream bands. It is `{u16 flags, u8 parameter id, u8 0}`:
- The flags pick the voice class (`0x006b6774`). The `0x0404` of 71-74 is the chaining class.
- The byte is the parameter that drives the chain's variation (`0x006bbfae`). It is 6 for the screams, 4 for music, and 7 for kids 91.

The 45 values at or above `0x10000` include `music` effect 2 and `ui` 154 and 155. Those carry the bits `0x4|0x2` of a different class, not the chain.

## Presence in a placed script is not execution

Of the opcodes that appear in the scripts of things actually **placed** in Lost Kingdom, only some ever fire. `Bouncy` carries `REPAIREFFECT` ×2, `SINGLESCREAM` ×1, `STARTSCREAM` ×1, `STOPSCREAM` ×2; `Junspray` carries `GETANIM_CH` ×3 and `TRIGANIM_CH` ×9; `Coconut` carries none. In a live park only **STARTSCREAM, STOPSCREAM and TRIGANIM_CH** were ever reached. The other three sit on branches a normal run does not take:

- **`REPAIREFFECT`** (Bouncy instructions 181, 186) sits behind `TEST VAR_BREAKSTAT` / `BRANCH_NZ` — the **breakdown repair** limb.
- **`SINGLESCREAM`** (Bouncy 162) follows `COPY VAR_BROKEN, 1` and a `STOPSCREAM`, so it is the scream **on breaking down** — the same limb.
- **`GETANIM_CH`** (Junspray 220, 254, 288) sits behind `TEST VAR_LANE1/2/3` + `BRANCH_Z`, so it needs a lane actually OCCUPIED — and the Jungle Spray is chosen about **one run in five**.

**So a static count of unreached instructions is a FLOOR, not a total**: the static scan OVERSTATES what is live and the running game UNDERSTATES what is latent. Neither number alone is the answer — the static one says what could bite, the live one says what does. Break a ride, or catch the sideshow admitting somebody, and three more announce themselves.

`STARTSCREAM` is 2 operands, `STOPSCREAM` 0, **`TRIGANIM_CH` is opcode 23 with 4 operands**.

## Loading a park does not rebuild what is in it — and there is no guard anywhere

A ride's script starts by playing the clip that builds the ride. `Bouncy.RSE`'s **third** instruction,
body word 4, is `WAITANIM 0 0` — role 0 entry 0, the construction clip — and `WAITANIM` starts the
animation as well as waiting on it. (The **first** is `NAME`, at word 0, in all fourteen of the shipped
park's scripts.) So anything that starts these scripts from word 0 watches every
placed thing build itself again. **Nothing in the engine guards against that**: what prevents it is
that a loaded park never starts its scripts from the beginning at all.

`FUN_00415270` is the whole-game restore chain. It reads module after module, each followed by a
four-character tag it checks on the way back in; the tags are compared as **dwords**, so they sit
little-endian in the file and read backwards in a dump (`WRLD` as `DLRW`). Two of its arms matter here.

| Address / offset | Original name (if known) | What it is | Evidence |
|---|---|---|---|
| `FUN_00415270` | — | the restore chain: 17 modules in order, each checked against its trailing tag. Logs `"<module> loaded %d bytes"` per arm | read |
| `FUN_004647a0` | — | the `RSYS` arm, "Ride System". Calls the build path per object, then **overwrites every animation channel from the saved record** and restores the per-node flag words (bit `0x10` = hidden) | read |
| `FUN_005597a0` | — | the `RSSE` arm, "RSSE scripts". Mallocs 244 bytes per script and reads **the whole struct from the file**, preserving only the two list pointers around the read — so the program counter at `+0x3c` comes back with it | read |
| `FUN_00463060` | — | the build path. Checks role 0 exists, triggers it, then triggers `0xd` — role **13**, freeze-at-frame-0 — which starts at once (`FUN_004732a0` queues neither pseudo-role) and holds role 0's clip at frame nought | read |
| `FUN_00473e30` | — | the channel reset the build path calls first. With its second argument set it writes the sentinel `0xc` into every channel's `AnimID` *and* `DeferredAnimID` | read |
| `FUN_00473550` | — | called when the restore put **no** channel on role 0; walks the nodes and clears `0x800` off any carrying `0x100` | read |
| `FUN_00472cb0` | — | binds a clip to a channel: writes the span and the elapsed-frame scale | read |
| `FUN_00464580` | — | a debug dumper that prints one channel field by field, and the source of every `AnimTimeControl` field name | read |
| `FUN_0055a300` | — | a two-byte setter, `*(short *)(script + 0xc0) = value`. The **third writer** of the speed word, and the one that pushes an object's operating speed over the loader's hardcoded 50 | read |

So a newly built thing and a loaded one are the same code with opposite outcomes. The player building
one reaches `FUN_00463060`, which holds role 0 at frame nought, and nothing overwrites it afterwards, so
its script's `WAITANIM 0 0` plays the clip through to its last frame — which is exactly what a built item should look like. A **loaded** one runs the same
path and then has its channels and its script state written over from the file before a frame is drawn,
so the trigger is discarded and the construction clip never appears.

**Measured in the shipped park** (`Easymode.TPWI`, 14 saved scripts): every counter is parked mid-flight,
not at nought — 120 of 124, 216 of 329, 46 of 208 for the Belly Bounce, and so on — and each lands on a
`BRANCH`, `BRANCH_Z`, `TEST` or `WAIT`. Bouncy's 46 holds a `WAIT 500` and is the target of `BRANCH ->46`
from words **36 and 41** — words 33 and 38 are the `LOOPANIM 5 0` and `LOOPANIM 5 1` those branches
follow — so it is **one** instruction past the `LOOPANIM 2 0` at word 43 that starts the clip it idles
on. Decoded from the body the save itself carries: `33 LOOPANIM 5 0 / 36 BRANCH ->46 / 38 LOOPANIM 5 1 /
41 BRANCH ->46 / 43 LOOPANIM 2 0 / 46 WAIT 500`. The saved variables come back too, and their slot 2 (`VAR_CAPACITY`) agrees with each
object record's own `mOperatingCapacity`. The byte layout is in the FileFormats docs, not here.

**A channel's flag word is the engine's own field, and it is NOT the flag word a caller passes in.** The
two overlap and disagree. A caller's flags are `0x1` loop, `0x2` start at once, `0x4` do not lay the rest
pose down, `0x8` do not apply the hide list. The field stored on the channel uses `0x1` and `0x8` the
same way, but its `0x2` means **frozen at frame nought** and its `0x4` means **held on the last frame** —
states rather than requests. A start of a loaded role's in-range entry over a channel that is not idle
clears both (`FUN_00472f60`, `0x0047302b`, `0x0047303b`); the pseudo-roles instead OR in `0x2` (role 13)
or `0x14` (role 14) without clearing, and role 14 sets the clip time a whole clip past the start stamp
(`0x00473193`..`0x004731a9`), so the elapsed frame lands exactly on the total. **The engine's `RSYS`
restore copies the saved channel back as it stands** (`FUN_004647a0`, `0x00464bcb`..`0x00464c17`): the flag
word, with `0x10` added to a held channel (`0x00464bfb`), the role and entry, the three time stamps, the speed
and the queue, so a clip resumes where it was, against the clock the save puts back. In the shipped park **eleven of the fifteen** saved channels carry
`0x4`, so a restore that passes the saved word through as caller flags drops the held pose on nearly all
of them and restarts the clip — including the Litter Bin, which is saved on role 0.

**What OpenTPW does with this.** It restores **both halves**: from `RSSE` the script's counter, its
variables, its stack with both its indices, its result register, its two wait deadlines, its looping key,
`TRIGWAITANIM`'s mark, its `SETTIMER` deadline and its declared name (`park.md`, "The two stacks"), and from `RSYS`
each thing's animation channels — the role, the entry, the speed, the three time stamps, the queue and the flag word,
with the two bits that mean the same thing carried across and the held and frozen states re-entered through the
pseudo-roles 14 and 13, where the engine copies the word: the same bits, reached another way. Every deadline and stamp
is a reading of the clock the save's `KOLC` module holds, which the engine makes read the saved value again; OpenTPW
moves each by its distance from that reading onto the load's moment on its own clock, so a wait ends and a clip goes on
where the save left them (`park.md`, "Where OpenTPW's animation state parts from the engine's", difference 4).
Restoring only the script is a net loss, and measurably so: a thing whose
steady-state loop holds no animation instruction never reaches the `LOOPANIM` in its prologue again, and
ten of Lost Kingdom's fourteen placed things stood frozen for the whole session when the counter alone
was put back. The rest is stepped over by length, neither restored nor counted in the `unimplemented` census — the
limbo, bounce and walk tables — so the walk still has to add up. The name is **not** in the
saved struct and is taken off the script's own opening `NAME` instead, which matters because resuming
skips that instruction and `FINDSCRIPTRAND` looks a script up by name. `ParkRides.BindNew` — the path a
player takes by building something — deliberately restores nothing, because a new thing has no past and
role 0 is the one animation it is meant to play.

## Faithful, not defects

Behaviour that reads like a bug and is the original:

- **A guest short of the price is left short, not refused, when the charge is taken.** `FUN_004fe1a0` subtracts the price, unclamped, when it is non-zero; the only test of a price is `FUN_004fde50` at the door before boarding (`0x00500715`), and it is not asked again at the charge.
- **A healthy ride's turn never completes an admission.** `FUN_004e0450` is reached only while closing, or from `Invite`'s `mCanLoad == 0` bail. The guest's own state-14 turn does the completion.
- **A shop is offered and paid like a ride.** The queue-room test reads the object's `+0x40`, which is **not** `mQueueSizeInCells` as loaded: `FUN_004de130` (`GetBackOfQueue`) *overwrites* it by walking the map whenever `mBackOfQueue` is nought, and `FUN_004dd920` calls that **before** it reads the count. The shop's entry cell connects to a path, so the walk answers one cell and `0 < 4` passes. All six objects carrying the choosable bit really can be offered — the three toilets are in the identical position, and no toilet in any park could be visited if the count were read as loaded. **And a shop does NOT take its money through LIMBO**: `Coconut.RSE` declares zero limbo slots and zero walk slots and uses neither family, running the same `VAR_LETMEON` → `WAIT 1000` → `VAR_LETMEOFF` handshake a ride runs. The engine never reads a script's limbo slots either: swept over all 43 functions that resolve a script frame, with the opcode handlers' own accesses as the positive control. It is paid by the ordinary dismiss path, `FUN_004e1410` → `FUN_005014e0` → `FUN_004fd970` → `FUN_004fe1a0`. Limbo is real, but it is how `steak`, `giftshop`, `balloon`, `Cost_shp`, `arc2x3` and the Super Toilet's `SupBog` hold a guest — never `coconut`, and it is script-private bookkeeping the engine never reads.
- **`SINGLESCREAM`'s negative branch skips effect `0x6b`.** The gap is in the original's switch and is matched by the shipped category listing.
- **`BOUNCESETBASE` writes a field the bounce family never reads**, so `Bouncy`'s rider nodes stay at 0..9.
- **A guest the ride cannot route away is teleported onto the exit and left there** with a `?` bubble, and the ride closes itself.
- **`UsageInfo.InitChanceOfLoosing`** is the game's own spelling, and it is stated as LOSING where the runtime field is winning.

## Measured state of the shipped Lost Kingdom park

| What | Value | Provenance |
|---|---|---|
| Objects with `mCanLoad` 1 | all **fourteen** | Save read |
| Objects with `mExitPos == mEntryPos` | **ten of eleven** | Save read |
| Belly Bounce (thing 13, cat 1100) | capacity **5**, duration **30**, price **0**, four queue cells + path bit | Save read |
| Jungle Spray (thing 14, cat 1303) | capacity **3**, price **20**, one queue cell, no path bit, `mCanLoad` 1, state 0, visitable | Save read |
| Drinks Shop (thing 16, cat 1203) | capacity **1**, price **30**, `QueueSizeInCells` **0** | Save read |
| Toilets (cat 1402), three of them | capacity **1** | Save read |
| Duration anywhere but the Belly Bounce | **0** | Save read |
| `mTotalTakings` | nought on every object | Save read |
| Things that can be queued for | **all six that carry the choosable bit** — 13, 14, 16 and the three toilets 21/22/23. Decided by the engine's map walk (`FUN_004de130`), not by the save's `mQueueSizeInCells` | Save read + map walk |
| Things ever seen calling somebody forward, over 2,688 thing ticks | **[13] — the ride alone** | Driven run |
| Park entry fee | **25** against a floor of 20 | Balance file |
| Bus stops | **(42,5)** and **(53,5)** | Save read |
| Guest states over 180 game seconds / 1,170 censuses | Wandering 426, InQueue 241, Deciding 188, Riding 89, GoingToRide 66, SteppingUpQueue 23, BeingAdmitted 17, EnteringRide 1 | Driven run, sampled ~2 s |
| A rider put down on leaving | **(52.500, 26.500)** — the exit cell's exact centre | Driven run |

`LeavingRide` never appears in that census: it is too brief for a ~2 s cadence. **That is a SAMPLING limit and must not be read as the state never occurring.**

The Jungle Spray is queued for and invited in **about one run in five** at that budget: five runs of identical code gave `queuedFor 13` four times and `queuedFor 13,14` / `invited 13,14` once. A single unseeded run is one sample, not a property of the code — `longest` queue was observed as 2 and then 3 from identical code.

## Open and unverified

- **`FUN_0041a9d0`'s custom-detail path** (`FUN_0044a590` answering `0x4000`, "A costume", 3).
- **`FUN_005019f0` case `0x11`**, the walk of the `mFirstGuard` chain through `+0x210` / `+0x212`.
- **Whether a shop's duration of nought is correct** (it may simply not read it) where `FUN_004df8f0` would take a clamped value from the descriptor's `+0x1a0`. A bought one's is: the constructor writes `+0x5c` only for a starting duration above nought, and every shop's is nought (Q171).
- **Refuted, so do not repeat:** "only `UNBOUNCE` writes `VAR_LETMEOFF`" — there are six writers, and the claim is false for 16 of the park theme's 17 dismissing ride scripts. "The shops' `mOperatingCapacity` might be nought, leaving them permanently full" — every visitable object has a non-zero capacity.
- Descriptor keys present in the `.sam` files that OpenTPW does not read yet: `ShopType`, `RideHandlesSprite` (the flag byte's `0x20`, Q52), `RequiresTeleport`.

## Measuring the corpus without inventing findings

- **Match a script listing FIELD-EXACTLY** (`awk '{for(i=1;i<=NF;i++) if($i==op) c++}'`) — no anchors, no substrings — and state the expected count for a known script before running it. A `^\s*[0-9]+\s+OP` anchor silently skips every script whose listing sits on the current instruction (marked `>>`), which made `LIMBO` read as 0 scripts when it is 6. A substring match gave `BUMP` 3 for the wrong reason, then 0 after the anchor "fix"; it is 3.
- **`wadcat`**: see `park.md`, "Instruments and preserved artifacts".
- **A census that recomputes is not an observation.** A census that calls the position function itself rather than reading what the renderer used will report the queue cell while riders are being drawn on the ride — indistinguishable from a build where nothing happens.
- **Collect every state; do not enumerate the ones you expect.** A disjunction across a step boundary ("`BeingAdmitted` or `EnteringRide`") passes on its first half while the chain stalls at exactly that boundary, and cannot detect the missing step.
