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

Entering state 14 also writes the guest's `+0x1f1` from the sideshow win roll — see [the win roll](#the-sideshow-win-roll) below.

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
   then `FUN_004fdc60` (the id onto `mPreviousTemporaryRides`, `+0x1e8`), `MajorDest` 0, `+0x1fc` 0, state 6, no dock.
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
`GetBackOfQueue` non-zero; the score (`FUN_004fcc30`); at least 10, unsigned; above the best, signed, or equal to it
on an odd `mGameTick`; the offer gate `FUN_004dd920`; then **`FUN_004fa530( GetBackOfQueue )` - the centre of the back
cell** (`0x004fcbc4`) - and on a route the best and `MajorDest`. Its one caller, the state-6 turn `FUN_004fec90`,
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
animation 4, runs `FUN_004fea70(0)` and restamps `+0x1fc`. The other aims: the minor decision `FUN_004fd570` looks in
a 4×4 window for a thing that passes the offer gate and outscores the others (no threshold of 10), switches to it only
when the raw line search (`FUN_004d8b40`, no splices, the first leg uncounted) from the current thing's entry to the
guest is longer than to the new thing's entry, and aims at **its entry cell** (`+0x36`), switching `MajorDest` first
(the old kept at `+0x1de`) and ignoring the route's answer; the InQueue turn's board arm aims at the stand point with
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
`FUN_00516330` exactly (bar `Math.Abs` of `int.MinValue`, which throws where the engine answers `0x80000000`), then
halves it as `RAND`'s `SHR 1` does, but per script and seeded 1, and the engine's own seed is not established,
so only the range and the one draw a call are the
original's. A direction neither switch knows stands the point at the cell's centre, counted
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
| `FUN_004dda40` | — | The longest queue a guest will join (`FUN_004ddb60`, the arrival's third gate) or stay in (the InQueue turn's 5a): 100 for a thing without the queue-path bit (`0x004dda4c`); with it, the capacity sum in "The `InQueue` turn", whose descriptor field pairing is unproven. | Its two callers |
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
from each call's re-arm (`QUEUE_DRAIN_ADVISOR_0xCB`); the bank's `+0x114` gate on the debit
(`QUEUE_DRAIN_DEBIT_BANK_GATE`); `FUN_004d8c60`'s write in every measure (`QUEUE_REMEASURE_BACK_CELL_STAMP`); the
per-age percentage, on the refunds (`QUEUE_REFUND_DEPRECIATION`) and on the debit (`QUEUE_DRAIN_DEBIT_DEPRECIATION`,
`0x00527fe8`); a run over anything but queue or bare ground
(`QUEUE_DRAIN_CLEARS_ANOTHER_KIND`); and a walk past a thousand cells, which the original's never gives up
(`QUEUE_END_WALK_UNBOUNDED`). **Open:** what the bank's `+0x114` is; whether the advisor's `0xcb` posts are heard. `FUN_004e2290`, the per-age
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
`OPENED_RIDE_MODEL_CHANGE`), the coaster's track record, which the guard lets through as the choice does
(`OPEN_GUARD_COASTER_TRACK_RECORD`), the constructor's close (`BOUGHT_QUEUED_THING_STARTS_CLOSED`), the ride window's
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
two stamps 70 apart could reach it. `FUN_004dda40` returns 100 for a thing without a queue path (`+0x32 & 8`), else
`trunc(max(+0x5d × desc[+0x1b4 + lvl × 0x40] × R / +0x5c, 4.0))` with `lvl` = `+0x50` and `R` =
`+0x58 / desc[+0x1a8 + lvl × 0x40]`, or 1 for a speed of nought; `R` is stored through a float global (`0x007cdc54`),
the `+0x1b4` float is the one its assert calls `"No queue constant entered in SAM file"`, and the `+0x1a8` divisor is
an integer (`FIDIV`).

**The clock and the stamps.** `mGameTick` (`[0x0080239c] + 0x1da70c`, named by the world serialiser) goes up by one
at the start of each thing sweep (`0x00516394`), which runs on game ticks whose low three bits are nought and at most
three times a rendered frame; it is zeroed at level start (`0x00515865`) and loaded from a save (`0x00517bec`).
`FUN_004fc800(n)` plays animation `n`, for `n` 4 plays sound `0x7e` for a guest whose id's low nibble is nought,
stamps `+0x208`, copies the state into `mSavedState` (`+0x224`) and enters state 8, which `FUN_00502430` looks
through. The guest constructor `FUN_004faec0` zeroes `+0x208` and `+0x1fc` and sets happiness to **50.0**
(`0x004fb075`). The serialiser tags every need float `pv` (`0x0075b444`); happiness and toilet are known by the debug
strings that print them (`0x004fda74`, `0x004fd10e`) and by the needs tick, not by a name in the game.

**OpenTPW builds** the turn as `PeepBehaviour.QueueTurn`, with `ParkRideOperation.LeaveQueue` as the tail's
`FUN_004ddd20` and `DismissFromTheQueue` as `FUN_005012f0`: arms 1, 2 and 4, 5a for a thing without a queue path,
5b for a car track, the re-take (`FindQueueDestination`, and out when it fails), the broken ride's skipped re-take, and
the toilet. `ParkState.LeaveQueue` splices by the leaver's own links, so a leaver with nobody in front
writes their own next as the head (`0x004ddde9`): an unlinked one empties it and the rest of that queue is lost to the
walk in turn. **Counted:** the no-route board (`QUEUE_BOARD_NO_ROUTE`: ours routes to the entry cell's centre, the original to
the stand point on the same cell, and `FUN_004fa5f0` also fails without routing on `mStrandedTime` at `+0x198`,
`0x004fa62a`, that nothing here keeps: nought on the queue paths but from a save), the dirt gate (`QUEUE_TOILET_DIRT_GATE`), the capacity on a queue path (`QUEUE_CAPACITY_RECHECK`), the
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
`ParkRideOperation.Forget` is `FUN_004e0ac0`. Thought 6, `FUN_004e1670`'s two counters and the samples are counted
(`DOOR_PRICE_THOUGHT_6`, `DOOR_WALK_AWAY_COUNT`, `DOOR_PRICE_ANALYSER_SAMPLE`); the event ring is not kept.

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
| Speed by rest | `+0xc0`, 60 to 140, one of `FUN_004fa870`'s three terms | none: the walk keeps the saved `max_speed` | every decide (Q136) |
| Thoughts `0x12` to `0x16` | shown | none, uncounted | tired, unhappy, very happy staff (Q110) |
| Strikes | `mStaffHQ`'s monthly flag, the strike walk, state 5's end | none, uncounted, the model-9 record unread | the monthly consideration every month the park is open; a strike only past the 24-month gate (Q138) |

## Spending — a guest pays on LEAVING

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004fd970` | — | The settle-up for leaving **any** visitable thing (not just a sideshow). Shifts the guest's recent-things history (`+0x1e0`..`+0x1e6`), bumps one of three counters by the descriptor's `+0x4ac`, charges, relieves a need by the descriptor's `+0xe8`, then splits on `+0x1f1`: nought logs `"Person lost this sideshow…"` and docks happiness at `+0x19c`; otherwise it runs `FUN_004fe1e0` and moves happiness by `(a-b)*3`. | Its own strings |
| `FUN_004fe1a0` | — | **The charge.** `price = object[+0x194]`; when non-zero it credits the ride, plays a sound, and does `person[+0x1a0] -= price`. **The only `SUB [reg+0x1A0], reg` in the image.** | Byte search |
| `FUN_004e16b0` | — | **The economy feed**: first the bank's deposit, `FUN_004d0190( price )` (the balance, `0x004e16c6`), then `ride[+0x180] += price`, `ride[+0x70] += price`, then on the descriptor's `+0x4ac` — **1, a shop, credits `global[+0x20130]`; 2, a sideshow, credits `global[+0x20380]`**; a ride (0) credits no pool. Inside the shop arm a second switch on `+0x164` (`ShopType`, then `+0x158` `SpecialIngredient` for types 2 and 4) buckets the visit and passes **1, not the money** — a tally, not a second payment. | Disassembly |
| `FUN_004d0600` | — | The park-balance path. **The ride charge does not go through it.** | Disassembly |
| `+0x194` | `mPricePerUse` | File **1054**. | Save record |
| `+0x180` | `mTotalTakings` | File **1090**. | Save record |
| `+0x1a0` | `mCash` | **Runtime** offset on the guest. The file's `mCash` is at **414** — do not conflate. | `FUN_004fe1a0` subtracts from it, `FUN_004fde50` compares against it |
| `+0x1e0` | `mPreviousRides[4]` | The recent-things history, shifted by three (four entries, not three). | Save reader |
| `+0xe8` | — | Descriptor field: the need relieved on leaving. | Disassembly |
| `+0x4ac` | — | Descriptor field: object kind, **a copy of `+0x4c`, `Info.WhichUIType`** (`0x004134f1`..`0x004134f5`, in `FUN_00413410`, its only store): 0 rides, 1 shops, 2 sideshows, 3 features. `FUN_004fde50`'s and `FUN_004e16b0`'s arm 1 read `SpecialIngredient`, `AppearanceEffect` and `ShopType`. | Disassembly |
| `+0x164` | — | Descriptor field: `UsageInfo.ShopType`, for the shops' visit tally in `FUN_004e16b0` (the schema's order puts it there). | Disassembly |

**`person[+0x1a0]` is the guest's cash — confirmed by USE, not by adjacency.** The field a price is SUBTRACTED from in `FUN_004fe1a0` is the field a price is COMPARED against in `FUN_004fde50`, by two unrelated functions. `+0x19c` is happiness and `+0x1a0` adjoins it, but adjacency was never the evidence.

**A charge is deposited in the park's bank.** `FUN_004e16b0` first calls `FUN_004d0190` on the bank thing with the price (`0x004e16bf`..`0x004e16c6`): the balance at `+0xc`, the world's `+0x1fc90` and the bank's `+0x124`, the same three adds the gate fee's `FUN_004d0600` makes. Then it credits the object's `+0x180` and `+0x70` and a global pool chosen by the descriptor's `+0x4ac` (`+0x20130` shops, `+0x20380` sideshows; a ride none). OpenTPW does not make the deposit: `ParkState.TakeAt` counts it (`CHARGE_BANK_DEPOSIT`, `docs/QUEUE.md` Q96).

### Measured prices and takings in Lost Kingdom

Drinks Shop **30**, Jungle Spray sideshow **20**, **Belly Bounce zero**; `mTotalTakings` nought on every object. The charge is gated on `price != 0`, so it never fires for the park's only ride.

## The effects of a visit — `FUN_004fe1e0`

Named by its own strings: `"Litter gone up by %d, is now %d"`, `"Customer bought a balloon, Aaah!"`, `"Trying to give a balloon to a pe…"`, `"Customer returning a costume."`, `"Balance file error: Shop has unk…"`, `"Sideshow won - happiness up %d p…"`. What it does, in order:

1. **A sideshow (`+0x4ac` == 2) PAYS OUT:** `FUN_004e1a10` — the **cost of goods**, not the chance of winning — feeds `FUN_004e1920`, and then **`person[+0x1a0] += FUN_004e1a10()`** — a prize ADDED to the guest's cash. A shop (`+0x4ac` == 1) takes the `FUN_004e1b40` path instead. **In Lost Kingdom that prize is 50 against a price of 20**, so winning the Jungle Spray leaves a guest 30 up.
2. **The item's own effects**, each added to a guest meter and clamped 0..100: the descriptor's `+0x144` and `+0x148` (with a sound of `0x83` or `0x84` depending which is larger), `+0x14c` → `+0x1b0`, `+0x150` → happiness `+0x19c`, `+0x154` → litter `+0x1b4`. Three more happiness changes follow, each reading the object's byte `+0x198`, which is not decoded: for the hunger effect `+0x148` and then the thirst effect `+0x144`, whichever is non-zero, `(rand & 7) + byte [+0x198] + that effect` under 30 docks `PeepInfo.SmallHappinessChange` (`0x004fe453`, `0x004fe4a5`); then happiness gains `byte [+0x198] * desc[+0x150] / 100` (`0x004fe4cf`..`0x004fe525`).
3. **Shop arms on the descriptor's `+0x15c`:** 1 gives a BALLOON (asserting the guest has none, building a sprite, and clamping a value between `DAT_0075d0f0` and `DAT_0075d0f4`); 2 hands out or takes back a COSTUME via the guest's `+0x24`/`+0x20`; anything else is a balance-file error.
4. **A toilet (`mFlags & 1`)** zeroes `+0x1ac`, may zero `+0x1b0` above 90, and stamps `+0xc2`.
5. **Then, for a sideshow only:** `person[+0x1d0] += 1` and a happiness rise computed from **`log2( costOfGoods / pricePerUse )`** - `FUN_004e1a10` (`+0x188`, cost of goods) over `FUN_004e1a00` (`+0x194`, price), `FILD`/`FIDIV` at `0x004fe835`/`0x004fe84b`, the logarithm by `FYL2X` over `ln 2` - scaled by the byte at `DAT_0078505c` (`PeepInfo.MediumHappinessChange`), and logged as `"Sideshow won - happiness up %d points to %d"`.

**The signs are not uniform**, and the decompile shows it: `FUN_004fe1e0` does `-(float)desc + meter` for thirst and hunger but `+(float)desc + meter` for vomit, happiness and litter. **Deduct two, add three** — which is exactly what the balance file's own comment column says.

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
| `FUN_004e2670` | — | Reached from `FUN_00501db0` case `0xe` (entering `EnteringRide`). Asserts `"Non sideshow object number %d ha[s]…"` (descriptor `+0x4ac` == 2), reads a chance-of-winning byte at **`+0x190`** (decimal 400), computes **`rand() % 100 <= chance`**, writes the result into script variable **11 (`VAR_PARAM`)**, and returns it. | Its own assert |
| `+0x190` | `mChanceOfWinning` | **It is the OBJECT's, and it is saved and loaded with the object** (`FUN_004db7d0`, beside `mCostOfGoods` at `+0x188`, `0x004dcd01`..; file 1050); two setters (`FUN_004e1a20`, `FUN_004e21c0`) are reached from the object window. Placing one, `FUN_004db090` derives it as `100 - descriptor[+0xec]` at `004db38f`..`004db3a1` — `MOV EDX,[EDI+0xec]` / `MOV ECX,0x64` / `SUB ECX,EDX` / `MOV [ESI+0x190],ECX` — where `+0xec` is `UsageInfo.InitChanceOfLoosing`. That `FUN_004e2670` takes both the catalogue id (`+0xe`) and the script handle (`+0x24`) off the same pointer is what fixes it as the object rather than the person. OpenTPW reads the item's figure instead (Q97). | Disassembly |
| `FUN_004e1a10` | `mCostOfGoods` | **Not the chance-of-winning accessor.** It is two instructions, `MOV EAX,[ECX+0x188]; RET`, on the OBJECT. `FUN_004db090` builds `+0x188` from the descriptor's `+0x140`, which is `UsageInfo.InitCostOfGoods`. It is the sideshow's **prize** and the numerator of what winning is worth. The chance of winning is `+0x190`, reached by `FUN_004e21b0`. | Disassembly, 2026-09-20 |
| `FUN_004e1a00` | `mPricePerUse` | `MOV EAX,[ECX+0x194]`. The divisor in the happiness sum below. | Disassembly |
| `FUN_004e21b0` | — | `MOV AL,[ECX+0x190]` — the real chance-of-winning accessor. | Disassembly |
| `UsageInfo.InitChanceOfLoosing` | — | **VERIFIED as the source behind `mChanceOfWinning`, with a per-item override:** `sideshow/SideShow.sam` declares **70** as the category default and **`Junspray.sam`, inside `junspray.wad`, overrides it to 75** — so the Jungle Spray's chance of winning is **25**. A grep of the installed folder cannot see that, because an item's overrides live in the `.sam` inside its own `.wad`. **Nothing in `shops` or `rides` declares the key at all, so their chance of winning is 100 and their roll never fails** — which is the whole reason a shop always serves, and why `FUN_004e2670`'s assert reads "sideshow **or** this value is `'d'`" (decimal 100). (`Loosing` is the game's own spelling.) | `.sam` sweep + `FUN_004db090` |

### What `+0x1f1` means is not settled

The save reader names the byte `mQueuePos`; the state setter writes the sideshow win roll into the same byte; and `FUN_004fd970` branches on it between `"Person lost this sideshow…"` and the full effects path. **It is overloaded, and the meaning at settle-up time should be treated as UNKNOWN** — "Person lost this sideshow" reads at least as much like *did they get their go / were they served* as *did they win*. For a sideshow with `VAR_LANE1..3` the position plausibly says which lane the guest got, nought meaning none.

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
| `+0x1d0` | sideshow visit counter |
| `+0x1e0` | `mPreviousRides[4]` |
| `+0x1f0` | `mPersonType` |
| `+0x1f1` | `mQueuePos` (file 494) |
| `+0x1f4` | `mQueueMoveDelay` (file 490) |
| `+0x208` | `mTimeOfLastSpotAnim` |
| `+0x210` | `mBalloonScript` |
| `+0x220` | `mState` |
| `+0x224` | `mSavedState` |
| `+0x20` / `+0x24` | costume in / out |
| `+0xc2` | stamped by the toilet arm |

`FUN_005019f0` case `0x11` walks the guard chain from `mFirstGuard` (`+0x1da744`, see the header's list heads above) through `+0x210` / `+0x212`, so those two are read off a staff record and are not evidence against the guest table's `mBalloonScript`. The case itself is undecoded.

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
| `+0x70` | takings accumulator credited alongside `+0x180` | |
| `+0x180` | `mTotalTakings` | File 1090 |
| `+0x190` | `mChanceOfWinning` | Saved with the object (file 1050); derived at placement as `100 - descriptor[+0xec]` (see "The sideshow win roll"). **This is an OBJECT offset.** Do not confuse it with the **person** `+0x190` (`mPreviousX`) in "Where a WALKING peep is drawn" below — different records, same number |
| `+0x194` | `mPricePerUse` | File 1054 |
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
| 11 | `VAR_PARAM` | The sideshow win roll, written by `FUN_004e2670` |


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
| `+0x04` / `+0x06` | the destination pair |
| `+0x08` | start time |
| `+0x0c` | due time |
| `+0x10` | visitor handle |
| `+0x14` | facing octant |
| `+0x16` | action |
| `+0x18` | state |
| `+0x1a` | flags |

**The machine is `0 free → 1 walking on → 2 carried → 3 walking off → 4 done`**, and the script drives only the ends of it.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00555963` | `RSSE_WALKON` | Handler. | Dispatch table entry |
| `0x00555b0c` | `RSSE_WALKOFF` | Handler. | Dispatch table entry |
| `0x00555b34` | `RSSE_WALKGET` | Handler; writes the result back into the operand's variable slot when the operand carries the `0x40000000` tag — the same outbox shape `UNBOUNCE` has. | Dispatch table entry |
| `FUN_00556f40` | — | `WALKON`'s implementation. Takes the first slot whose STATE is 0, stores the handle and both nodes, sets due = now + `duration * 100` (a zero duration becomes 100), derives the facing with `fpatan` between the two node positions **masked to 3 bits (8 octants)**, and sets **state 1**. Asserts `"Walknodes need a `setwalk`…"` if no node table is declared, and `"WALK: Could not add peep t…"` when every slot is busy. | Disassembly |
| `FUN_005571a0` | — | `WALKOFF`: finds the slot holding that visitor, restamps the timers, recomputes the facing, spawns particles when the action is 2, sets **state 3**. | Disassembly |
| `FUN_00557110` | — | `WALKGET`: scans for a slot in **state 4**, clears its state and handle, returns the handle — 0 if none. | Disassembly |
| `FUN_00557d80` | — | The **per-frame** stepper, called once per script per frame from the positioner. Progress is `(now - start) * 1000 / (due - start)`; at **≥ 1000** state 1 becomes **2** (and action 4 attaches the rider to the head node), and state 3 becomes **4**. | Disassembly |
| `FUN_00557ab0` | — | The positioner; also the only reader of `+0x6e`. | Disassembly |
| `FUN_005580a0` | — | Pure presentation: interpolates between two node positions and calls `FUN_004f9e60` to place the sprite. | Disassembly |
| `FUN_00556b90` | — | Resolves a node id **in the ride's MODEL**, in the space its fifth argument names — `0x800` for a walk node and `0x80` for a head node from the walk family, `0x200` for a sound and `0x100` for particles from `FUN_005573d0` — and logs `"RSSE: Invalid Node ID"` on a miss. | Its own string |
| `FUN_004f9e60` | — | Place a sprite. | Disassembly |

**The operand mapping, measured from the push order** (cdecl, right-to-left, so operands 1..7 are `param_2`..`param_8` of `FUN_00556f40` IN ORDER): 1 handle (`+0x10`), 2 walk node (`+0x00`), 3 head node (`+0x02`), 4 off-from (`+0x04`), 5 off-to (`+0x06`), **6 ACTION (`+0x16`, the one tested against 4)**, 7 flags (`+0x1a`). Confirmed by the corpus: action takes only 1, 4, 5, 6 across the park, and the two scripts passing **4** — `Totem` and `tvsim` — are exactly the head-node case. `WALKON`'s action operand picks the node space: **4 = a HEAD node (space `0x80`)**, anything else a walk node (space `0x800`).

**The duration is NOT an operand**: `FUN_00556f40` takes it from an `__ftol()` of a float already on the x87 stack, so it comes from the script or the object, not the instruction. The engine's leg duration is the **distance between two model nodes**.

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

`Bouncy.RSE` calls `BOUNCESETBASE 8` and never `BOUNCESETNODE`, so the node base stays nought and its rider nodes are **0..9**.

## Where a rider is drawn

**The simulation never moves a rider; the drawing places them.** None of `FUN_004fa930`'s five callers is a rider, so a rider's world position legitimately stays on the cell they queued on, and the RENDERER draws them at a node of the ride's own model.

- **`bouncy.MD2`'s rider nodes are `body`, `body01`..`body09`, ids 1..10** — ten, matching the ten declared slots, sitting 9-12 units up.
- **Look them up by NAME, not id: an id is unique only within its capability.** In that one model id 1 is `body`, `air`, `camera` AND `body11`, and the capability word is undecoded. A bare-id lookup draws riders on the camera.
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
| `FUN_004fa870` | Works out the speed first, `((+0xc0 + +0xc2 + +0xc4) / (u16)[0x0075c7fc] − +0xc8 × [0x007006f0]) × [0x007006f4]`, stores it back at `+0xc8` and passes it to `FUN_00510190`, and decays `+0xc4` to 99/100. Then it stamps `previous := current` with `FUN_00510160( person+0x190, person+0x194 )` — a **thiscall** on the mover (`person+0xd4`), so the decompiler drops `ECX` and it reads as two args — and ends with `FUN_004d4190` on `person+0xc`. It is **the first call of every person kind's tick handler** — `FUN_00501650` at `0x00501658` (guests), `FUN_00505490` at `0x00505495` (staff) — and in the guest handler it sits **ahead of the `(id & 3)` needs stagger**, so it is unconditional: every peep, every sweep. Straight-line, no early return. | Disassembly |
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
one frame of deliberate lag for something trailing its owner. **What that something is remains unsettled** —
one reading is a held balloon (`+0x210` is named `mBalloonScript` by the guest serialiser, and the height
term shortens as the owner moves), another a ground effect — and the reads of `+0x210` in
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
| `FUN_00551130` | `STARTSCREAM` | Opcode **86**, 2 operands. **Refuses if a scream handle is already held**, logging `"RSSE: Started screaming without s…"`, and then stores the refusal's 0 over `+0xd0` (`0x00555ee6`), so the old chain screams on unstopped. Operand 2 bands the sample: 0 plays nothing, 1 → effect **0x47**, 2-3 → **0x48**, 4-7 → **0x49**, 8+ → **0x4a**. Straight after the play it sets the voice's **parameter 6** to `(operand + speed) / 2` via `FUN_0051bc40` (`0x00551261`..`0x00551265`). The handle is kept on the script at **`+0xd0`**. | Its own string |
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
states rather than requests, and `FUN_004732a0`'s own `Start` clears `0x6` on the way in and then sets
`0x2` or `0x14` itself for the two pseudo-roles. So the engine does not express "this channel was held"
as a flag at all: it re-enters the channel with role **14**, which acts on the clip already loaded and
backdates the timebase a whole clip so the elapsed frame lands exactly on the total. Role **13** is the
same for a freeze at frame nought. In the shipped park **eleven of the fifteen** saved channels carry
`0x4`, so a restore that passes the saved word through as caller flags drops the held pose on nearly all
of them and restarts the clip — including the Litter Bin, which is saved on role 0.

**What OpenTPW does with this.** It restores **both halves**: from `RSSE` the script's counter, its
variables and its declared name, and from `RSYS` each thing's animation channels — the role, the entry,
the speed and the flag word, with the two bits that mean the same thing carried across and the held and frozen
states re-entered through the pseudo-roles exactly as above. Restoring only the script is a net loss, and measurably so: a thing whose
steady-state loop holds no animation instruction never reaches the `LOOPANIM` in its prologue again, and
ten of Lost Kingdom's fourteen placed things stood frozen for the whole session when the counter alone
was put back. The rest is stepped over by length, neither restored nor counted in the `unimplemented` census — the
wait deadlines, the call stack and the limbo, bounce and walk tables — so the walk still has to add up. The name is **not** in the
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

- **What `+0x1f1` means at settle-up time.** The byte is overloaded between a queue position and a sideshow roll.
- **`+0x1a8` as a capacity re-check divisor.** `FUN_004dda40` reads it as an integer beside the `+0x1b4` queue constant (see "Every way out of a queue", the `InQueue` turn); which `.sam` key fills it is not established.
- **The balloon and costume SPRITE path** out of `FUN_004fe1e0`.
- **`FUN_005019f0` case `0x11`**, the walk of the `mFirstGuard` chain through `+0x210` / `+0x212`.
- **Whether a shop's duration of nought is correct** (it may simply not read it) where `FUN_004df8f0` would take a clamped value from the descriptor's `+0x1a0`.
- **Refuted, so do not repeat:** "only `UNBOUNCE` writes `VAR_LETMEOFF`" — there are six writers, and the claim is false for 16 of the park theme's 17 dismissing ride scripts. "The shops' `mOperatingCapacity` might be nought, leaving them permanently full" — every visitable object has a non-zero capacity.
- Descriptor keys present in the `.sam` files that OpenTPW does not read yet: `ShopType`, `RideHandlesSprite` (the flag byte's `0x20`, Q52), `RequiresTeleport`, `GoldenTicketCost`, `InitPricePerUse`.

## Measuring the corpus without inventing findings

- **Match a script listing FIELD-EXACTLY** (`awk '{for(i=1;i<=NF;i++) if($i==op) c++}'`) — no anchors, no substrings — and state the expected count for a known script before running it. A `^\s*[0-9]+\s+OP` anchor silently skips every script whose listing sits on the current instruction (marked `>>`), which made `LIMBO` read as 0 scripts when it is 6. A substring match gave `BUMP` 3 for the wrong reason, then 0 after the anchor "fix"; it is 3.
- **`wadcat`**: see `park.md`, "Instruments and preserved artifacts".
- **A census that recomputes is not an observation.** A census that calls the position function itself rather than reading what the renderer used will report the queue cell while riders are being drawn on the ride — indistinguishable from a build where nothing happens.
- **Collect every state; do not enumerate the ones you expect.** A disjunction across a step boundary ("`BeingAdmitted` or `EnteringRide`") passes on its first half while the chain stalls at exactly that boundary, and cannot detect the missing step.
