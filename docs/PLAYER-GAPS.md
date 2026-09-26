# What a player notices

What a player notices, **ordered by when a player meets it**, not by size or by how interesting it is to build.
**This is not the work queue: `docs/QUEUE.md` is**, and it reaches this file's open gaps 4, 5 and 7 in its section F.
Drawn up 2026-09-20 from `docs/STATUS.md`, the 42-thing Lost Kingdom census
(`docs/history/current-task-archive.md`, grep `THE PARK IS FULLY DECODED`) and the live plan.

`docs/CLEANUP-PLAN.md` was a second list of nine things a player sees, handed over 2026-09-21. All nine are
closed; `docs/QUEUE.md` Q13 moves it into `docs/history/`. Until then it is untracked and exists on this machine only.

**All nine closed by 2026-09-22:** 9 (park load time), 6 (sound held
under a pause), 4 (the lobby ocean), 5 (one scream on a loop), 3 (guests moving in jumps), 1
(everything replaying its "being built" clip), 2 (the camcorder walking through rides), 8 (the lobby's
attract camera) and 7 (no "entering the park" animation).

**Three of the nine were mis-diagnosed in the writing, and that is the part worth carrying forward.**
Item 8's three claims were *all* refuted by measurement — the wander box is the original's own field
for field, the aim does not swing, and not one frame of 16,407 stepped out of line — so it closed as
measured-faithful plus a deviation Alexah chose afterwards. Item 7 was the reverse: **Alexah's memory
was right and the decode was wrong**, having stopped at the first of five steps; the original really
does swing the camera onto the gate and fly it in before the park loads. And item 1's two guessed
causes were both refuted on the way to a real fix.

**Nothing in *this* file was ticked by any of that work — not one of the nine appears here**, so this
list is untouched by it; its own 4, 5 and 7 are queued in `docs/QUEUE.md` section F.

**Alexah sets which item is the goal. One item per session** (`CLAUDE.md`, "How a session runs" 4).
Tick an item here in the same commit that lands it, and move its detail to the page that owns it.

Scope is **Lost Kingdom only**. Anything that changes nothing in `data/levels/jungle` is not on this list,
however large it looks — that trap has been hit twice.

## Item 2 is done, 2026-09-21. The work comes from `docs/QUEUE.md`; gaps 4, 5 and 7 below follow it (its section F)

> *"let's get the park management gadget buttons working. We can start with the purchase menu, so we
> can start work on allowing players to build/delete paths, and queues, along with purchasing,
> deleting, moving, and managing the rides/shops/sideshows themselves."*

**Every clause of that is now true, and all three steps of Alexah's dependency order are done:**

1. ~~**The purchase menu**~~ — buy and hire, with buy, sell, move, carry, hire, fire, pick up, put down.
2. ~~**PATHS and QUEUES**~~ — laid and lifted, with `FUN_004de1f0`'s invalidation wired so queues cannot
   go stale, and **building by pointing** (anchor, then commit — there is no drag, both drag slots are
   bare `RET` stubs).
3. ~~**RIDES / SHOPS / SIDESHOWS**~~ — a placed ride's management window cycles, deletes and moves.

**And the category buttons, 2026-09-21.** Info and Money open four real screens — all staff, all items,
all visitors and the entry price — every one confirmed on screen. **Five of the six gadget buttons now
open something.**

**ONLY RESEARCH REMAINS, AND IT IS BLOCKED BY ABSENCE, NOT BY EFFORT.** It is not a category
(`FUN_004a0840`'s case `0x2b` goes straight to `FUN_004aa480`, as the map does) and its screen is six
effort sliders over research groups, or a message box in Instant Action or with no researcher hired. This game has no
research and no groups; Lost Kingdom's one researcher (thing 30) walks, with nothing to research. It only logs, and
counts each click as `RESEARCH_BUTTON`. **Do not open it as a task until a research system exists.**

**Park status (screen 3) is deferred by Alexah, 2026-09-21** — *"I'm okay delaying work on the Info
screen. It's not important at the moment."* The Information category is therefore seeded to screen **4**
where the original seeds **3**, a declared deviation that reverts the day park status is built.

**Setting a patrol area was explicitly deferred by Alexah** and is not built; staff keep to the areas the save gives them.

## THE GOAL BEFORE IT, SET 2026-09-20: CLOSE THE GUEST LOOP. DONE.

> Guests should be able to visit and purchase from shops and sideshows, new visitors should arrive,
> visitors should go home, and they should be dropped off and picked up at the front of the park by a
> **cruise ship**, a **sea plane** and a **bus**.

**Where it stands, 2026-09-20: every clause of that is now true.** Guests arrive by themselves, are
carried by all three vehicles, go home when their day runs out, and **buy from both the shop and the
sideshow** — a filled park took **1110 at the Drinks Shop** (37 sales at 30) and **900 at the Jungle
Spray** (45 at 20). Both halves were confirmed in the running game by census **and** by screenshot.

**This reordered the queue, and that reorder is now spent.** Items 3, 6 and 8 were halves of one loop
and all three are ticked. **Items 2, 4, 5 and 7 remained when this was written on 2026-09-20; item 2
has since been done, so 4, 5 and 7 remain.** Item 5 is the one worth flagging rather than choosing: it
was found by playing, and on 2026-09-21 Alexah described it properly, which moves it from arithmetic to **rendering**: the bar draws in the wrong place and
repeats. See the item itself; do not assume it is small until that is measured.

**The order, agreed with Alexah:**

| | | why here |
|---|---|---|
| ~~**1st**~~ | ~~**Arrivals, and the three vehicles** — item 3~~ **DONE 2026-09-20** | Nothing else in the loop can be seen without people coming in |
| ~~**2nd**~~ | ~~**Departures** — item 6~~ **DONE 2026-09-20**, built with the first rather than after it | The same vehicle loop: one that drops off must pick up. Built alone it drains the park |
| ~~**3rd**~~ | ~~**Sideshow spending** — item 8~~ **DONE 2026-09-20** | It needed the win roll, which nothing had ever written: every visit took the losing arm |
| ~~**4th**~~ | ~~**Shops** — item 8~~ **DONE 2026-09-20** | The "structural blocker" was a misread field. Nothing structural was in the way |

**THAT FIRST QUESTION IS ANSWERED — 2026-09-20. IT WAS NEITHER OPTION.**
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
| ~~2~~ | ~~Four of the six gadget buttons do nothing~~ **DONE 2026-09-21** — five of six now open something | — |
| ~~3~~ | ~~Nobody new ever arrives~~ **DONE 2026-09-20** — and on all three vehicles | — |
| 4 | The advisor is silent unless you open a screen | — |
| 5 | The happiness gauge reads wrong | — |
| ~~6~~ | ~~Nobody goes home and no day ever ends~~ **DONE 2026-09-20** — same loop as 3 | — |
| 7 | A park cannot be saved | — |
| ~~8~~ | ~~Guests cannot buy anything from a shop, and barely from a sideshow~~ **DONE 2026-09-20** | — |

**Three remain: 4, 5 and 7.** Every one of them is independent of the others — none is blocked on
anything now built, and none blocks another.

---

## 1. The gates never open when you enter a park — DONE, 2026-09-20

- [x] The gate idles **shut**, opens **once** as the park-entry flight's camera finishes homing onto the
      gate side (`LobbyCameraMode.StepLeaving`, `0x005e06e4`), and shuts again if Escape cancels the flight
      from state 2 (`0x005e18ab`). The park is asked for when the camera arrives, not when the doors finish.
- That is the original's: see `docs/exe/lobby.md`, "Escape cancels the fly-in, and the gate is the flight's".
- **Not every gate swings.** Fantasy's is a worm with **no rotation tracks at all**, so it gets no
  `MeshRotator`. Each clip plays over the span it declares; the table is in `docs/exe/lobby.md`.
- **Confirmed in the running game** when the gate still opened at Enter and held the park load: jungle
  114–120 stepped frames against a predicted 114, fantasy 198–204 against 200. Those figures measured the
  old length and timing; Q41's run is the current confirmation. On screen the doors go shut → part-open → open, with the difference
  localised to a band dead centre on the doors (columns 101–135 of 256, peak 33.6 against a 5.84 mean).
- **Two traps worth keeping**, both of which cost runs: the debug console's `pause` sets `Time.Paused`
  and `Time.Delta` then reads **nought**, so anything driven by it freezes and never finishes — use
  `step <n>`, each one frame of ¹⁄₆₀ s. And the lobby camera orbits, so `settle` alone lands on
  whichever side it had reached: the gates face their island's **−Y** side, which is `orbit π`.

## 2. Four of the six gadget buttons do nothing - DONE 2026-09-21, five of six now open something

**TICKED. Only RESEARCH still does nothing, and it is the one with nothing behind it.**
Buy carries the whole purchase/hire/management half; paths, queues and building by pointing are all
laid and verified; and Info and Money now open real screens - **all four of their buildable screens
drew in a running park and were photographed**. Research is not a category at all (`FUN_004a0840`'s
case `0x2b` goes straight to `FUN_004aa480`, as the map does), and its one screen is six effort
sliders over research groups: this game has **no research and no groups**, so there is nothing to put behind the
button. It only logs, and counts each click as `RESEARCH_BUTTON`.

- [x] **Buy** opens `ParkBuyScreen` (stream `0x00754cf8`), which cross-links to `ParkHireScreen`
      (stream `0x00751fa8`). Both are built on the original's control **type 7**, a scrolling
      multi-column list that nothing in this tree had - `UiList`. A row's payload is sized by the
      COLUMN COUNT, which is why buy pushes three and hire two.
- [x] **Clicking a placed ride opens its management window** - `ParkObjectWindow`, stream
      `0x00755150`. **There are nine such windows**, dispatched by `FUN_00486920` on the thing's kind
      byte and the item's `WhichUIType`; the others stop at four counted names (`SHOP_WINDOW`, `SIDESHOW_WINDOW`,
      `FEATURE_WINDOW` for toilets, staff rooms, misc items and upgrades alike, and `STAFF_WINDOW`), and a visitor's
      reaches nothing counted. Close, delete, move and
      the two cycle arrows work.
- [x] **The rides panel - 2026-09-21, with three rows still counted.** All three sliders take their
      range from the item and their value from the ride, and **save**: capacity 5 of 1..10 clicked to
      6 read back as 6 after the window was closed and reopened.
      **The stats table fills four of its seven rows.** The four condition rows are GAUGES, not text -
      `FUN_004ade40` hands each `((v & 0xff) << 10) / 100`, a 0..100 percentage onto a 0..1024 bar -
      which is why they rendered their labels perfectly and showed no values at all. State of repair
      and Remaining life are now drawn, from two floats at save **1074** and **1070** that nothing was
      reading; Belly Bounce measures **repair 100, life 100**. Age and Scrap value were already there.
      **Excitement, Reliability and Users last month stay counted**, and not for want of a control:
      the first two divide by the descriptor's `+0x1a8` and `+0x1a0`, whose `.sam` mapping `park.md`
      records as unproven and not to be guessed, and the third needs the record's ring buffers, which
      `ParkWorld` deliberately does not read.
      **The preview draws the ride's own model** - the one standing in the park, so it animates as the
      ride runs - fitted by a real bounding box, filling 93px of a 194px panel.
      **It orbited until the per-mesh box was fixed.** A burst of sixteen frames showed the centroid
      tracing a circle of constant radius; `LobbyModel` was boxing each mesh without putting the mesh's
      own rotation through, which displaces the box and so displaces the centre it spins about. Spread
      fell from across 0.387 / down 0.299 to **across 0.040 / down 0.024**.
      **The ride sitting low in the panel is not a fault**: the drawn box and the fit centre agree to
      (0.0, 0.0, 0.0), and the 0.591 centroid is where the pixels are - a wide wooden base under a thin
      figure. Centring the silhouette would deviate from the engine, which fits from the box.
      **One caveat.** The spin **stops while the clock is held**: the original differences a real-time
      clock and keeps turning through a pause, and nothing here exposes wall-clock time while paused,
      so that deviation is declared at the site rather than a wider clock invented for it. The scissor
      clipping is proven: it is the *"harsh cutoff"* Alexah reported across the model.
- [x] **The verbs underneath**: `ParkBuilding` buys, sells, moves and carries; `ParkStaffPool` and
      `ParkPeople` hire, fire, pick up and put down. Money is taken at PLACE time, from the item's
      `+0x1b8`, which is why cancelling a carry needs no refund.
- [x] **INFO and MONEY - DONE 2026-09-21, four screens, all confirmed on screen.** The HUD is six
      CATEGORY pickers, not seventeen buttons: `FUN_004a0940( n )` opens whichever screen a category
      was last left on, and `FUN_004a0810( category, screen )` in each builder confirms the seeding
      from the other direction. Built: **allstaff** (`0x750e10`), **allitems** (`0x7508e0`),
      **allpeeps** (`0x7506c8`) and **entryprice** (`0x751798`).
      **The Info and Money buttons were pressed for real** - through `click`, the genuine hit test and
      both handlers, only SDL skipped - and each opened its screen: `the interface took (162,558)`
      and `(100,609)`.
      **The numbers were predicted before they were read.** The fee reads **25** and the spinner moved
      it **25 -> 26 -> 24**; `b_door` took the gate **open -> shut -> open**; the cleaner's wage read
      **63** and the scientist's **125**, which are `PerGradeStaffConsts[grade].BaseWage` times
      `PerTypeStaffConsts[kind].PayMultiplier` on the EASY numbers (7x9 and 5x25). All Miscellaneous
      Items counted **Small Toilet 3**, which the save's own `VisitableFlag` census independently
      records as three.
      **Three of the four screens' columns are filled only where this game has the number.** Rides
      fill State Of Repair and Remaining Life; miscellaneous items fill both columns; shops,
      sideshows, visitors' Time In Park and Rides Ridden stay blank and counted rather than carrying a
      plausible wrong quantity. **UITEXT row 117 is literally `"?"`** - the original ships that visitor
      column unnamed too.
      **The screenshots found four defects every green check had passed.** A column heading 160px
      clear of its column and a tab strip that vanished with the list it hung off, both invisible at
      4:3 and both only on a 16:9 window; a kind->label table transposed because the executable's own
      switch is out of order (case 2 takes `0x6e`, case 3 `0x6d`), which drew guards under
      "Entertainers' Happiness"; and a visitor list rebuilt every frame, caught as **260** gap reports
      from one visit. See `docs/exe/hud.md`.
- [ ] **RESEARCH** - logs and counts (`RESEARCH_BUTTON`), and blocked on a system that does not exist rather than on effort. See
      the note at the head of this item.
- [x] **PATHS - DONE 2026-09-21, and verified in the running game.** `path 10 10` lays one for
      **20** (`Costs.PathCell`, measured from `Standard.sam` and confirmed through the game's own
      balance reader); `delpath` lifts one and **refunds nothing**, which is the original's own
      asymmetry - only a queue cell credits anything back. A laid cell joins itself up by
      `FUN_005348d0`'s rule and takes its art from the executable's own two tile tables.
      **Photographed:** a plus of five cells drew end pieces at all four tips and a **crossroads** at
      the centre, and the HUD money went **87987 -> 87887** - exactly 100 for five cells, on screen.
      **The running game found a defect no test could have:** tiles were stale by one operation,
      because `Lay` retiled only the cell it laid while the link pass had also rewritten its
      NEIGHBOURS' masks. The lift path was always right, and that is what identified it.
- [x] **QUEUES - DONE 2026-09-21, and the `FUN_004de1f0` HOOK IS PROVEN.** `queue <x> <y> <thing>
      <fromX> <fromY>` lays one for **75**; `delqueue` lifts one and **refunds**, where a path does
      not - the original's own asymmetry. A cell records the object it serves and a flow direction
      that is the **opposite of the step taken into it** (first writer wins), which is what makes the
      queue measurable: the walk accepts a neighbour only when its flow points back the way the walk
      came.
      **The hook this item has flagged from the beginning now works, and the numbers were predicted
      before they were read.** Deleting a middle cell took the Belly Bounce from **4 cells ending
      2866** to **2 ending 2868**, and the capacity readout followed it from 0/16 to 0/8 - while the
      save's own `mQueueSizeInCells` sat unchanged at 4, correctly ignored. Without
      `ParkState.InvalidateQueue` that number could not have moved at all, because `QueueCellsFor`
      returns the cached pair whenever the save sets it - and this park sets it on exactly that ride.
      **Deleting a queue cell ORPHANS the remainder and that is correct** - the original has no
      trimming loop anywhere.
- [x] **Building by POINTING - DONE 2026-09-21, confirmed by console.** `ParkBuildMode` holds the
      original's single MODE global and its anchor pair. There is **no drag** - both drag vtable
      slots are bare `RET` stubs - so a run is click-to-anchor then click-to-commit, the target
      snapped to the dominant axis, **and the anchor then advances to that snapped target rather than
      to where the run reached**, which is exactly what lets an L be laid click by click.
      Measured: click one anchored and laid nothing; click two laid **6 cells (8,9)→(13,9)**; click
      three laid **4 more** down the other axis, and the corner came out as tile index 3 at 90 - a
      corner piece - with end pieces at both tips. Money fell **87987 → 87787**, exactly 200 for ten
      cells. **An armed mode consumed a click on the Belly Bounce instead of opening its window, and
      once disarmed the identical click opened thing 13's window** - the original's rule, shown both
      ways. A run **aborts entirely on the first cell that refuses**, as the original's line walker
      does.
      Seen on screen since, through the player's own route: a queue laid by two clicks was photographed
      standing and joined (`docs/QUEUE.md` Q3).
      **THE QUEUE TOOL IS NO LONGER CONSOLE-ONLY, 2026-09-22.** The ride window's own queue button
      arms it against that ride (`ParkObjectWindow`, verb `0x3e34`), and clicking a queue cell re-arms
      it for the thing that queue serves - the original's mode `0x14`. Both were driven through the
      real interface and the armed mode read back as `mode 3`.
      **THE PATH TOOL IS NO LONGER CONSOLE-ONLY, 2026-09-22 (Q35).** A click on grass or path arms and
      anchors it, Backspace takes the last run back up, and Escape puts it away - confirmed in the
      running game with the real keys.
- [x] **Placing by pointing - DONE 2026-09-21.** Both screens put the item or the person in the hand,
      and clicking the park puts them down. `Level.WorldClick` takes the click only when the interface
      did not, and **anything in the hand goes down before any window opens** - the original's own
      order, since a place mode consumes the click and only an idle mode opens windows. The console's
      `put` and `hire` still do it in one step, for a test that cannot move the pointer.
      **A quick right click cancels, with RMB cancel on, and so does Escape** - the original's own ways
      out of a place mode. Nothing is charged for picking something up, so putting it back gives nothing
      back and takes nobody out of the pool. The console's `drop` reaches the same `ParkHand.LetGo`
      rather than a copy (`docs/exe/park-engine.md`, "The hand's ways out").

### The original section, for the part still open

- [x] **Was seen:** Info, Money and Research do nothing. **Five of the six now open something**, as of
      2026-09-21 — Buy, Camcorder, Map, Info and Money. Only Research is left.
- **Lives:** `ParkGadget.cs` — `b_resrch` logs each click and counts it as `RESEARCH_BUTTON`. `b_info` and `b_money` go through
  `ParkCategoryScreens.Open`, which is the original's own remembered-tab picker.
- **The real dispatch is decoded** in `docs/exe/hud.md`: `FUN_004a0940( n )` opens *the screen that
  category was last left on*, from three globals seeded 1 / 3 / 10. **The HUD is six category pickers,
  not 17 buttons.**
- **Two jobs, and they are very different sizes.** Making the buttons honest is small. Making them *work*
  needed buying, building and hiring, which now exist (item 2 above), and finances and research, which still do not — the single
  largest missing system in the project. Do not start the second one by accident.
- **Gate:** the `unimplemented` census in the debug console.

## 3. Nobody new ever arrives — DONE, 2026-09-20

- [x] **Was seen:** the park is permanently the save's 13 guests and 5 staff. Once they have ridden the
      one ride, nothing changes again, ever.
- **DONE, 2026-09-20: GUESTS ARRIVE BY THEMSELVES AND GO HOME BY THEMSELVES.** A park left
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
  runs every tick and re-triggers on status -1, 0, 4 and on 2 once its load is let go. Unattended, the
  population now moves both ways: 13 → 15 → … → 9 → 10 → 7, five in and ten home over three minutes.
  A `vehicles` console census was added to see this at all - `paths` reports where a vehicle is drawn,
  which reads identically whether its script is running or parked, and only the pc separates them.
- **Lives:** `ParkPeople.PeepsIn` builds the save's guests; `ParkPeople.Admit` builds every arrival, from the bus
  stop (`PeepState.AtTheBusStop`, 21, and `ParkAdmission.BusStopA/B`).
- **Census:** ids 29, 31-41 **and 42** are the 13 guests, on the bus road at x 47-48, y 9-15, already
  walking in. (Thing 42 is a guest too. Compute a free id, never take one from here.)
- **Not blocked:** the gate-admission path it would feed is built and measured — guests pay at the gate.
- **Gate:** `park jungle`, then the `guests` census over time. **Predict the count before reading it.**
- **What remained for the loop — all of it done, 2026-09-20:** an arrival manager (its timer now counts fours of
  the thing sweep from the save's mark, as the original's does: the first load 126 s in and the next about 150 s
  after each, decoded by Q68 and built by Q68b); guests created and walked in from the stop; guests walked out and
  removed; and the ferry and seaplane **driving** alongside the bus.
- **The whole mechanism is decoded — see `docs/exe/park.md`, "Arrivals".** It is no longer
  a design question, and the shape to build is not the one this list assumed:
  - `FUN_004cf3e0` waits out a timer, asks `FUN_004cf5b0` for a headcount, summons a vehicle, and then
    makes **one guest per thing sweep** through `FUN_004cf720` until the load is spent.
  - **The vehicle is chosen by crowd size** — under 36 the bus, up to 60 the seaplane, beyond that the
    ferry. At random only when none is current and the manager's tail summons one.
  - **The vehicle thing is created on demand** (`FUN_0051a2f0`), which is why this park places a bus
    and neither of the others: its `mArrivalVehicle_Size1` slot holds the bus (thing 15, measured) and
    the other two slots are nought because no crowd that big has ever arrived.
  - **Nobody rides in anything.** The guest is constructed at a cell near the stop, so the vehicles are
    mechanism rather than transport — and state 21 deletes one when it reads 4.
- **The rate** is `Arrival.TimeBetweenArrivals` 150, proven by the balance loader's slot table. OpenTPW counts it as
  the original does, in `mGameTick >> 2`, fours of thing sweeps, and carries its wait over from the save: measured
  in Q68b's run, the first load on sweep 509 (126 s), and the next 604 sweeps (149.5 s) after the last guest got off
  (`docs/exe/park.md`, "Arrivals"). The vehicle's drive in comes on top, and the wait starts again only once the
  vehicle has put its load down. Between loads the bus waits at the stop where the original's waits out of sight,
  so a later load's guest comes on the sweep that calls it (Q131).

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

**The bus runs its circuit repeatedly** (`ParkPeople.StepVehicle`), watched through statuses 1, 2, 3, 4 and 5, and
the seaplane and ferry both loop back to the start of theirs.

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
- **Lives:** `docs/exe/advisor-park.md`; a park's lines are `UI/Park/ParkLines.cs`, which says one (`ExplainGadget`).
- **The "needs simulation" blocker was refuted:** his acceptance gate consults no world state, and the HUD
  it waited on exists. This is buildable today.
- **Do not transcribe anything first:** every global speech line is already transcribed in
  `global-speech-transcripts.tsv` in the Ghidra notes (where they live is in `CLAUDE.local.md`). Grep it.
- **Gate:** audio capture, cross-correlated against the game's own mix (the memory note `verifying-audio-by-capture.md`).
- **NOT confirmed in a run** — this rests on code reading alone.

## 5. The happiness gauge reads wrong

- [ ] **Seen:** the gauge does not track how the park is actually doing.
- **ALEXAH DESCRIBED IT PROPERLY ON 2026-09-21, AND IT IS A RENDERING FAULT:** *"The bar image
  just appears to not be rendering within the actual location properly, it looks like two copies of the
  bar split down the middle like it's repeating."* So the line above — which framed this as the gauge
  not tracking the park, i.e. as arithmetic — is **the wrong description**. The artwork is in the wrong
  place and it is repeated.
- **A lead, READ FROM THE CODE AND NOT MEASURED — do not report it as a finding.** `UiMeter.OnDraw`
  builds its skin as `new Texture( Skin )` with **no flags**, so `Texture.SamplerFor( None )` returns
  `AnisotropicRepeat`, which is `SamplerAddressMode.Mirror` — any UV outside 0..1 then draws a
  **mirrored second copy**, which is exactly what is described, and is the same family as the lobby
  sea (see `Texture.Cache`'s note on the sea drawn with the default `Mirror` on the way back from a park). **But its own UV rect reads as in-range**
  (`0, 1 - filled, 1, filled`, the same x/y/w/h shape as its position rect), so look at
  `Graphics.Quad`'s UV handling and at whether `Pixels` is the housing's true rect **before** touching
  the meter's arithmetic.
- **Lives:** the `meter.wct` mapping. `ParkGadget.ShowHappiness` and `UiMeter` draw it;
  `ParkPeople.AverageHappiness` supplies the number and is *not* the fault.
- **Largely exonerated already:** `UiMeter.Max` is 100 and it is handed 50. The mapping from the skin to
  the drawn height is what is left.
- **It was found by playing; confirm the fix the same way.**

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
- **Lives:** `GameCalendar.DayRolled`, which nothing reads yet.
- **Gate:** the `peeps` census, with `pause` and `step <n>` to make a short-lived state observable.
- **Same vehicle loop as item 3**, which is why they are now one job: a bus that drops off must also
  pick up, and a guest who goes home has to leave by something.

## 7. A park cannot be saved

- [ ] **Seen:** Load, Save and Publish do nothing, which a player meets at the moment they try to stop.
- **Lives:** `ParkFrontEnd`'s game menu, where Load Game, Save Game and Publish Park each call `NotYet(...)`.
- **The real cost is not the button.** **No `.TPWS` has ever been read** — the reader must not be assumed
  to generalise from the one file the game ships. `docs/exe/saves.md` also records an unreconciled
  divergence between the traced preamble byte counts and what the shipped file measures.

---

## 8. Guests cannot buy anything from a shop, and barely from a sideshow — DONE, 2026-09-20

- [x] **Guests buy from both.** Confirmed in the running game: a filled park took **1110 at the Drinks
      Shop** (37 sales at 30) and **900 at the Jungle Spray** (45 at 20) across two runs, with the
      `spend` census showing the cause before the effect — guests `heading {shop:16}` and then the till
      moving. `save/` unchanged within every run.
- **THE "STRUCTURAL BLOCKER" WAS A MISREAD FIELD, NOT A BLOCKER.** The filter compares a
  queue's length against the object's **`+0x40`**, and this project read that as `mQueueSizeInCells`
  out of the save — nought for the shop and all three toilets. It is not: `FUN_004de130`
  (`GetBackOfQueue`) **overwrites** `+0x40` by walking real type-3 cells off the map whenever
  `mBackOfQueue` is nought, and `FUN_004dd920` calls it **before** it reads the count. So `+0x40` is a
  cache, and the save's copy is the cached answer to that very walk.
  - **The walk reproduces both cached pairs the save already holds, and neither number was put in**:
    the Belly Bounce recomputes to 4 cells ending at 2866 = (49,22), the Jungle Spray to 1 cell ending
    at 3765 = (52,29). Those *are* its `mQueueSizeInCells` and `mBackOfQueue`.
  - The corroborating argument, which is what should have raised the doubt years earlier: **the three
    toilets are in the identical position.** Under the old reading no toilet in any park could ever be
    visited.
  - `FUN_004de040` reads the entry cell's **`mNeighbours`**, never its `mDirection`, and takes the
    first set bit in the order `0x01`, `0x10`, `0x40`, `0x04`. Full decode in `docs/exe/ride-operation.md`.
- **AND THE SHOP NEEDED NO NEW SCRIPT MECHANISM.** `docs/exe/ride-operation.md` claimed "a shop
  takes its money through the LIMBO mechanism"; **`Coconut.RSE` declares zero limbo slots and zero walk
  slots and uses neither family.** It runs the identical `VAR_LETMEON` → `WAIT 1000` → `VAR_LETMEOFF`
  handshake a ride runs, so the boarding chain already built for the Belly Bounce *is* the shop. Limbo
  is real but belongs to `steak`, `giftshop`, `balloon`, `Cost_shp` and `arc2x3`.
- **What made the visit do anything: the win roll.** `FUN_004e2670` rolls `rand()%100 <= chance` as a
  guest enters and writes it into `mQueuePos`, which the settle-up splits on. Nothing here ever wrote
  that byte, so **every visit in the park took the losing arm** — which is why a sideshow charged 20 and
  did nothing else. Chance of winning is `100 - UsageInfo.InitChanceOfLoosing`: a shop declares none, so
  its chance is **100** and a drink is always served; the Jungle Spray declares 75, so **25**.
- **Built with it:** the losing arm (happiness down by `MediumHappinessChange`), the sideshow's prize
  (`+50`, its `InitCostOfGoods`) and the win's happiness, `log2( cost / price ) * 15` = **+19**.
- **A number that was predicted wrong and measured right:** the Jungle Spray's prize is **50**, not 5.
  Predicted as 5, it made winning cost 30 happiness; measured at 50 it gains 19, and the engine's own
  "Sideshow won - happiness up %d points" reads honestly. The test caught it.
- **Still unbuilt, and named rather than quietly skipped:** the two global income pools
  (`+0x20130` / `+0x20380`), the `SpecialIngredient` and `AppearanceEffect` arms (balloons and
  costumes), and `FUN_004fdcc0`'s excitement-match happiness.
- **One honest limit.** A park left entirely alone still rarely buys a *drink*: only a quarter of guests
  ever grow thirsty (`Peep.Tick` shares the drift by thing id) and by then their exit countdown has
  usually run out — measured, of 148 samples at thirst 50+, **73 were HeadingForExit and only 11
  Deciding**. The shop is chosen the moment a thirsty guest *is* deciding, which
  `ParkRideChoiceTests` pins from four cells across the park. The `thirst` console command exists to
  create that condition, for the same reason `load` exists for the seaplane.

---

## Just behind these

- **32 of 106 opcodes are unimplemented** (74 built, `case Opcode.` labels against the enum, measured 2026-09-24).
  How many of the 32 shipped Lost Kingdom content reaches has not been re-measured since `TRIGWAITANIM`,
  `SINGLESCREAM` and `SCREAMLEVEL` were built; the `unimplemented` census answers it.

## Deliberately not on this list

Each was deferred for a measured reason, not for want of interest: the `BUMP` / `COAST` / `HOP` families
(**their rides are not placed in Lost Kingdom**) and the other three themes. Litter's reason expired once the
shops served; it is queued in `docs/QUEUE.md` section F.
