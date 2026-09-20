# What a player notices

The work queue, **ordered by when a player meets it**, not by size or by how interesting it is to build.
Drawn up 2026-09-20 from `docs/STATUS.md`, the 42-thing Lost Kingdom census
(`docs/history/current-task-archive.md`, grep `THE PARK IS FULLY DECODED`) and the live plan.

**Alexah sets which item is the goal. One item per session** (`CLAUDE.md`, "How a session runs" 4).
Tick an item here in the same commit that lands it, and move its detail to the page that owns it.

Scope is **Lost Kingdom only**. Anything that changes nothing in `data/levels/jungle` is not on this list,
however large it looks — that trap has been hit twice.

## >>> THE CURRENT GOAL, SET BY ALEXAH 2026-09-20: CLOSE THE GUEST LOOP. HALF DONE. <<<

> Guests should be able to visit and purchase from shops and sideshows, new visitors should arrive,
> visitors should go home, and they should be dropped off and picked up at the front of the park by a
> **cruise ship**, a **sea plane** and a **bus**.

**Where it stands, 2026-09-20.** The **arrival/leave half is DONE** — guests arrive by themselves, are
carried by all three vehicles, and go home when their day runs out; confirmed in the running game by
census **and by screenshot**, all three drawn and visibly moving. **The spending half is not started:**
sideshows are nearly there and shops are blocked on something structural. So the next goal for this
line is item 8, taken in that order.

**This reorders the queue.** Items 3 and 6 below stop being separate gaps and become halves of one
loop; item 8 is new. Items 2, 5 and 7 stand but are no longer next.

**The order, agreed with Alexah:**

| | | why here |
|---|---|---|
| ~~**1st**~~ | ~~**Arrivals, and the three vehicles** — item 3~~ **DONE 2026-09-20** | Nothing else in the loop can be seen without people coming in |
| ~~**2nd**~~ | ~~**Departures** — item 6~~ **DONE 2026-09-20**, built with the first rather than after it | The same vehicle loop: one that drops off must pick up. Built alone it drains the park |
| **3rd** | **Sideshow spending** — item 8 | Nearly there already: `CHARGE` is built (`c18ead8`) and pays on leaving |
| **4th** | **Shops** — item 8 | Last, because it is blocked on something structural rather than unbuilt |

**>>> THAT FIRST QUESTION IS ANSWERED — 2026-09-20. IT WAS NEITHER OPTION. <<<**
It asked whether the vehicles need the `.RSE` runtime built, or can be driven by our own animation
player. Both premises were wrong. The `.RSE` runtime is **already sufficient** — the bus's script
binds and runs to completion, reaching nothing unbuilt — and the movement is in neither place: it is a
**Bézier route in the model file** (the array at `0xac`, indexed by a node's `+0x52`) driven by a
**per-frame percentage in the animation** (channel `0x200`). Our own player applies it, in
`LobbyModel.Pose`. So it was **a contained job**, and it is done for the bus.

Confirmed from the engine as well as the data: `FUN_00471860` indexes the table as
`*(model + 0xac) + node[+0x52] * 0x10`, and `FUN_00474840` is the cubic Bézier over it.

| # | What a player sees | Depends on |
|---|---|---|
| ~~1~~ | ~~The gates never open when you enter a park~~ **DONE 2026-09-20** | — |
| 2 | Four of the six gadget buttons do nothing | — (to be honest); everything (to work) |
| ~~3~~ | ~~Nobody new ever arrives~~ **DONE 2026-09-20** — and on all three vehicles | — |
| 4 | The advisor is silent unless you open a screen | — |
| 5 | The happiness gauge reads wrong | — |
| ~~6~~ | ~~Nobody goes home and no day ever ends~~ **DONE 2026-09-20** — same loop as 3 | — |
| 7 | A park cannot be saved | — |

---

## 1. The gates never open when you enter a park — DONE, 2026-09-20

- [x] The gate now idles **shut** and plays its opening clip **once** as the player commits to the park;
      the park is asked for the moment the doors finish. `LobbyGate.Open`, called from
      `IslandPanel.EnterPark`.
- **The original does not do this**, measured rather than assumed: `IslandLobby_LeaveForPark`
  (`0x005e1e30`) is three calls — the leaving flag, `IslandPanel_KeyPuffAndEnterSound`, and a UI
  message 6 that closes the panel — and the state-3 teardown behind it only tears down. So this is
  `CLAUDE.md` rule 11, marked as a deviation at the call site. The queue's old "needs a held scene
  transition" note was a previous session's inference and is withdrawn.
- **Not every gate swings.** Fantasy's is a worm with **no rotation tracks at all**, so it gets no
  `MeshRotator`; a length taken from the rotator alone left it inert. A gate is played for as long as
  it *moves* — the rotation movement's end where it has one, the clip's own span otherwise. Full table
  in `docs/exe/lobby.md`.
- **Confirmed in the running game**, not from code: jungle held the load for 114–120 stepped frames
  against a predicted 114 (57 ÷ 30 fps), reproduced three times; fantasy 198–204 against a predicted
  200, on the other branch. On screen the doors go shut → part-open → open, with the difference
  localised to a band dead centre on the doors (columns 101–135 of 256, peak 33.6 against a 5.84 mean).
- **Two traps worth keeping**, both of which cost runs: the debug console's `pause` sets `Time.Paused`
  and `Time.Delta` then reads **nought**, so anything driven by it freezes and never finishes — use
  `step <n>`, each one frame of ¹⁄₆₀ s. And the lobby camera orbits, so `settle` alone lands on
  whichever side it had reached: the gates face their island's **−Y** side, which is `orbit π`.

## 2. Four of the six gadget buttons do nothing

- [ ] **Seen:** Buy is the first thing anyone clicks in a theme park game, and it is inert. So are Info,
      Money and Research.
- **Lives:** `ParkGadget.cs:311/329/344/347`, all four `NotYet(...)`. Camcorder and Map are the two that work.
- **The real dispatch is decoded** in `docs/exe/hud.md`: `FUN_004a0940( n )` opens *the screen that
  category was last left on*, from three globals seeded 1 / 3 / 10. **The HUD is six category pickers,
  not 17 buttons.**
- **Two jobs, and they are very different sizes.** Making the buttons honest is small. Making them *work*
  means buying, building, hiring, finances and research, none of which exist in any form — the single
  largest missing system in the project. Do not start the second one by accident.
- **Gate:** the `unimplemented` census in the debug console.

## 3. Nobody new ever arrives — DONE, 2026-09-20

- [x] **Was seen:** the park is permanently the save's 13 guests and 5 staff. Once they have ridden the
      one ride, nothing changes again, ever.
- **>>> DONE, 2026-09-20: GUESTS ARRIVE BY THEMSELVES AND GO HOME BY THEMSELVES. <<<** A park left
  alone runs `peeps 13 → 14 → 15 → 13 → 12 → 11` with nothing typed — seven arrivals about 18.6 s
  apart and ten departures as the saved guests' day ran out. `ParkPeople.StepArrivals` is the manager
  and `ParkPeople.Depart` the other half; `ExitLevel` is what sends them home, a countdown nothing had
  ever read. **And the vehicles carry them now — 2026-09-20, all three drive.** The one thing missing
  was an opcode: `Ferry.RSE` and `seaplane.RSE` start every animation with `TRIGWAITANIM` where
  `bus.RSE` uses plain `TRIGANIM`, so while that had no case in `RideScript` those two stood still and
  only the bus moved — the whole of the difference. With it built, `load 40` flies the seaplane in and
  `load 70` sails the ferry in, and the guests step off once it has landed rather than before: `peeps`
  holds at 13 through about nine seconds of approach, then 16 → 21 → 25 → 30.
- **And a second half to it: releasing the vehicle three times a circuit, not once.** Each vehicle
  script parks at three `TEST VAR_TRIGGER` spins; releasing only the first left the bus stopped at
  `VAR_STATUS` 4 for ever and, because arrivals are gated on the vehicle reporting 2, **the park
  drained to nought after one guest**. `ParkPeople.StepVehicle` is the tail of `FUN_004cf3e0`, which
  runs every tick and re-triggers on status -1, 0, 4 and on 2 with an empty load. Unattended, the
  population now moves both ways: 13 → 15 → … → 9 → 10 → 7, five in and ten home over three minutes.
  A `vehicles` console census was added to see this at all - `paths` reports where a vehicle is drawn,
  which reads identically whether its script is running or parked, and only the pc separates them.
- **(superseded) HALF DONE: A GUEST CAN NOW ARRIVE, BUT NOTHING MAKES ONE ARRIVE.**
  `ParkPeople.Admit( cellX, cellY )` creates a guest who was never in the save and wires the five
  places that have to know — the simulation list, the by-id index, the walk, the sprite pool and the
  cell's occupancy list — then `ParkState.Admit()` counts them. Confirmed in a live park: `peeps`
  13 → 14 → 15, the newcomer at the exact cell centre, then `AtGate` → `HeadingForGate` and walking
  east to the ticket booths with a real route.
  **What is missing is the trigger.** Arrivals are forced by hand from the debug console (`arrive`);
  the timer, the headcount and the vehicle choice are decoded (`docs/exe/park.md`, "Arrivals") and
  unbuilt. Until that lands, no guest arrives unless somebody types the command.
- **Lives:** `ParkPeople.PeepsIn` is the *only* place a `Peep` is constructed, and it builds the list from
  `park.People` — the save — and never adds. `PeepState.AtTheBusStop` (21) and `ParkAdmission.BusStopA/B`
  already exist as anchor points with nothing feeding them.
- **Census:** ids 29, 31-41 **and 42** are the 13 guests, on the bus road at x 47-48, y 9-15, already
  walking in. (This line listed only twelve until 2026-09-20; thing 42 is a guest too, which a live
  census showed when a newly admitted guest was handed id 43 rather than the 42 that was predicted
  from this list. Compute a free id, never take one from here.)
- **Not blocked:** the gate-admission path it would feed is built and measured — guests pay at the gate.
- **Gate:** `park jungle`, then the `guests` census over time. **Predict the count before reading it.**
- **(superseded) NOT confirmed in a run** — true while this rested on code reading alone.
- **(superseded) Still true as written: nobody arrives.** Both halves are built and confirmed in a live
  park: guests are created, carried in by whichever of the three vehicles the crowd size calls for,
  admitted, and removed again when their `ExitLevel` runs out.
- **What remained for the loop — all of it done, 2026-09-20:** an arrival manager (its rate is
  `TimeBetweenArrivals` in **quarter-ticks**, and the sweep runs **one tick in eight** —
  `docs/exe/boot.md`, corrected 2026-09-20 — which together give the 18.6 s predicted and then measured
  at 18.9 and 18.8, so the tick units this line once wanted are pinned); guests created and walked in
  from the stop; guests walked out and removed; and the ferry and seaplane **driving** alongside the bus.
- **>>> THE WHOLE MECHANISM IS NOW DECODED — see `docs/exe/park.md`, "Arrivals". <<<** It is no longer
  a design question, and the shape to build is not the one this list assumed:
  - `FUN_004cf3e0` waits out a timer, asks `FUN_004cf5b0` for a headcount, summons a vehicle, and then
    makes **one guest per tick** through `FUN_004cf720` until the load is spent.
  - **The vehicle is chosen by crowd size** — under 36 the bus, up to 60 the seaplane, beyond that the
    ferry. Not at random, except on the dismiss path.
  - **The vehicle thing is created on demand** (`FUN_0051a2f0`), which is why this park places a bus
    and neither of the others: its `mArrivalVehicle_Size1` slot holds the bus (thing 15, measured) and
    the other two slots are nought because no crowd that big has ever arrived.
  - **Nobody rides in anything.** The guest is constructed at a cell near the stop, so the vehicles are
    mechanism rather than transport — and state 21 deletes one when it reads 4.
- **(resolved 2026-09-20) The one thing still missing is the rate.** It is `Arrival.TimeBetweenArrivals`
  150 in **quarter-ticks** = 600 game ticks ≈ **18.6 s at 31 ms**, predicted before observing and
  measured at 18.9 and 18.8. Nothing in the executable writes the globals, so the key-to-global mapping
  is by arithmetic **role and is not proven** — recorded with that caveat in `docs/exe/park.md`.
  **In play the rate is now the timer OR the vehicle's circuit, whichever is slower**: gaps measured
  18.9, then 32.0, 38.7, 38.7, which is what gating arrivals on the vehicle must mean.

### The three vehicles they arrive and leave on

Alexah wants them dropped off and picked up at the front of the park by a **cruise ship, a sea plane
and a bus**. All three are shipped fixed items, and **none of them is missing art**:

| Item | `Info.Id` | Where it sits now |
|---|---|---|
| Bus | **1600** | **Drives.** Spawns at cell (29.7, −11.5), runs its route, parks at the shelter — world (510.5, 65.5), between `BusStopA` and `BusStopB` |
| Seaplane | **1602** | **Flies, lands and unloads.** Photographed on the water in the bay |
| Ferry — **this is the cruise ship**, confirmed by Alexah 2026-09-20 | **1604** | **Sails and docks.** Photographed alongside the quay |
| (Gates 1601, Lights 1603, End 1605 — End is three aircraft 59 units up) | | |

**`ParkFixedItems` now loads gates, lights and all three vehicles.** Only the End sign is still held
back. All six carry `DontApplyOffset 1` and `WhichUIType 4`.

**The bus was the worked example, and the other two followed it** — `88cd6de` stood it and bound its
script, `14d977d` read the routes, `9d4fb04` decoded the progress scalar, `3872a34` drove it. The other
two then needed **two** things the bus did not: the `TRIGWAITANIM` opcode (`d132b95`), which their
scripts use where `bus.RSE` uses plain `TRIGANIM`, and releasing each vehicle at all **three** of its
`VAR_TRIGGER` spins rather than one. Routes: FERRY 33 points, Seaplane 33, both closed Bézier loops;
both rest **exactly** on their own route, which the bus does not quite manage.

**One limitation carried forward, and it is visible:** a vehicle **does not turn to face its direction
of travel**, so the bus arrives sitting diagonally across the crossing. That is a real engine behaviour
left unbuilt on purpose and counted as `ANIM_PATH_FACING` — the engine samples the route's first
derivative (`FUN_00474a20`) and builds a basis from the tangent (`FUN_00470780`).

**(resolved) The bus drives its route once and parks.** That was true while only the first of its three
`VAR_TRIGGER` spins was released. With `ParkPeople.StepVehicle` it runs the circuit repeatedly — watched
through statuses 1, 2, 3, 4 and 5, and the seaplane and ferry both loop back to the start of theirs.

**The stop geometry is already decoded**, from the theme's own `Standard.sam` — do not re-derive it:

    BusStopA/B        (42,5)  (53,5)
    CrossingBSSideA/B (47,5)  (48,5)      <- bus-stop side of the crossing
    CrossingParkSideA/B (47,9) (48,9)     <- park side
    TicketBoothA/B    (47,13) (48,13)
    EntranceA/B       (47,17) (48,17)
    MapInfo.FixedItemOrigin (48,17)

The census places the save's 13 guests on the bus road at x 47–48, y 9–15 — i.e. already walking in
from the crossing. So the arrival path they would take is the one the shipped save shows mid-use.

## 4. The advisor is silent unless you open a screen

- [ ] **Seen:** the advisor is a constant presence in the original and says nothing here while you play.
- **Lives:** plan item N2; `docs/exe/advisor-park.md`.
- **The "needs simulation" blocker was refuted:** his acceptance gate consults no world state, and the HUD
  it waited on exists. This is buildable today.
- **Do not transcribe anything first:** every global speech line is already transcribed in
  `/home/alex/ghidra/notes/global-speech-transcripts.tsv`. Grep it.
- **Gate:** audio capture, cross-correlated against the game's own mix — `verifying-audio-by-capture.md`.
- **NOT confirmed in a run** — this rests on code reading alone.

## 5. The happiness gauge reads wrong

- [ ] **Seen:** the gauge does not track how the park is actually doing.
- **Lives:** the `meter.wct` mapping. `ParkGadget.ShowHappiness` and `UiMeter` draw it;
  `ParkPeople.AverageHappiness` supplies the number and is *not* the fault.
- **Largely exonerated already:** `UiMeter.Max` is 100 and it is handed 50. The mapping from the skin to
  the drawn height is what is left.
- **This is the last fault Alexah found by playing that is still open.** It was found by playing;
  confirm the fix the same way.

## 6. Nobody goes home and no day ever ends — DONE, 2026-09-20

- [x] Guests go home when their `ExitLevel` runs out — a countdown that had been ticking since the save
      was first read with **nothing anywhere reading it**. `ParkPeople.Depart` reverses everything
      `Admit` wires, including `ParkState.Forget`; a guest a ride or a queue is holding is refused,
      which is the original's own condition. Confirmed unattended: fourteen departures by name, the
      population moving both ways rather than only down.
- **It was built with 3, not after it** — exactly as the note below predicted. Built alone, departures
  drain the park and leave it that way, and that is not hypothetical: a defect in the vehicle handshake
  did drain it to nought for a while, which is what caught the fault.
- **The day still never closes.** Guests leaving is built; a day *ending* is not, and no calendar
  rollover is wired. That half of this line stands.
- **Lives:** plan item N3 — "the last of the twenty-two the park can reach".
- **Gate:** the `peeps` census, with `pause` and `step <n>` to make a short-lived state observable.
- **Same vehicle loop as item 3**, which is why they are now one job: a bus that drops off must also
  pick up, and a guest who goes home has to leave by something.

## 7. A park cannot be saved

- [ ] **Seen:** Load, Save and Publish do nothing, which a player meets at the moment they try to stop.
- **Lives:** `ParkFrontEnd.cs:172/173/178`, all three `NotYet(...)`.
- **The real cost is not the button.** **No `.TPWS` has ever been read** — the reader must not be assumed
  to generalise from the one file the game ships. `docs/exe/saves.md` also records an unreconciled
  divergence between the traced preamble byte counts and what the shipped file measures.

---

## 8. Guests cannot buy anything from a shop, and barely from a sideshow

- [ ] **Seen:** the park has a Drinks Shop and a sideshow, and a guest's money never reaches either.
- **Spending is half built.** `CHARGE` is built (`c18ead8`, 727 tests) and pays **on leaving**, not on
  boarding. Its payoff today is the **sideshow alone**.
- **>>> THE SHOP IS BLOCKED ON SOMETHING STRUCTURAL, WHICH IS WHY IT IS LAST. <<<** The Drinks Shop
  (census id 16, cell (43,30)) carries the "**may be offered**" flag — bit `0x4`, which
  `FUN_004fcb10` tests before it will even score a candidate — **but declares no queue cells**, and the
  filter `FUN_004dda20` is `length < mQueueSizeInCells * 4`. With zero cells nothing passes, so the
  chooser never sends anyone and the dismiss path cannot reach it either. `ParkRideChooserTests` pins
  the consequence: "the sideshow and the ride, and nothing else".
- **So the question to decode first** is whether the original reaches a shop by a path *other* than the
  ride/sideshow queue. Do not "fix" it by inventing queue cells for the shop.
- **Unbuilt in spending:** the happiness arm of the settle-up, and the two income pools.

---

## Just behind these

- **34 of 106 opcodes are unimplemented** (`TRIGWAITANIM` was built 2026-09-20) — but only three are reached by shipped content in this park,
  so it is mostly invisible here. Take the count fresh; three README lines and `RideScriptFile.cs:99`
  still quote older ones.

## Deliberately not on this list

Each was deferred for a measured reason, not for want of interest: the handyman's litter arm (**no litter
source in this park**), the `BUMP` / `COAST` / `HOP` families (**their rides are not placed in Lost
Kingdom**), the HUD's four screens, and the other three themes.
