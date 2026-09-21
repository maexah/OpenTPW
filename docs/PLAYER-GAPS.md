# What a player notices

The work queue, **ordered by when a player meets it**, not by size or by how interesting it is to build.
Drawn up 2026-09-20 from `docs/STATUS.md`, the 42-thing Lost Kingdom census
(`docs/history/current-task-archive.md`, grep `THE PARK IS FULLY DECODED`) and the live plan.

**Alexah sets which item is the goal. One item per session** (`CLAUDE.md`, "How a session runs" 4).
Tick an item here in the same commit that lands it, and move its detail to the page that owns it.

Scope is **Lost Kingdom only**. Anything that changes nothing in `data/levels/jungle` is not on this list,
however large it looks — that trap has been hit twice.

## >>> THE CURRENT GOAL, SET BY ALEXAH 2026-09-20: ITEM 2, THE GADGET BUTTONS. <<<

> *"let's get the park management gadget buttons working. We can start with the purchase menu, so we
> can start work on allowing players to build/delete paths, and queues, along with purchasing,
> deleting, moving, and managing the rides/shops/sideshows themselves."*

**>>> ALEXAH HAS CHOSEN THE BIG HALF ON PURPOSE. <<<** Item 2 below warns that making the buttons
*honest* is small and making them *work* is "the single largest missing system in the project — do not
start the second one by accident." **This is not by accident.** The warning stands as a statement of
SIZE, not as a veto: scope one session at a time, and do not treat it as a reason to hesitate.

**The order Alexah gave is a dependency order, not a preference:**

1. **The purchase menu first** — every later verb hangs off it.
2. Then **build / delete PATHS and QUEUES** — the map-editing half.
3. Then **purchase, delete, move and manage RIDES / SHOPS / SIDESHOWS** — the object half.

**>>> WHERE THIS STANDS, 2026-09-21: STEPS 1 AND 3 ARE DONE. STEP 2 IS NOT. <<<**

Alexah narrowed the goal for one session: *"Get the purchase/hire UI working per the gap plan please.
It should be fully functional. Rides can be purchased, moved or sold, clicked to open their management
menu, staff can be hired, fired, picked up and moved. Patrol areas can stay dead."* **Every clause of
that is now true and confirmed on screen.** The purchase menu and the hire screen open from the Buy
button and reach each other; things can be bought, sold, moved and carried; staff can be hired, fired,
picked up and put down; and clicking a placed ride opens its management window, which cycles, deletes
and moves.

**What is left of item 2, and it is the middle step of the order above:** paths and queues, which is
where `FUN_004de1f0`'s invalidation has to be wired or queues go stale, and the other three category
buttons - Info, Money and Research. **Patrol areas were explicitly deferred by Alexah** and are still
dead.

**One thing item 8 leaves this goal, and it is a live hook rather than a note.**
`ParkRideChoice.QueueCellsFor` walks queue cells off the **map**, not out of the save — so anything
that edits paths or queues changes that answer at runtime, and `mBackOfQueue` caches it. The original's
own invalidate-and-rewalk is `FUN_004de1f0`: it zeroes `+0x3a`, recomputes, and logs *"Object's queue
is now %d cells long"* — and its callers are exactly the path/cell editing family. **The editing half
already has its engine-side hook identified; wire the invalidation or queues go stale.**

## >>> THE GOAL BEFORE IT, SET 2026-09-20: CLOSE THE GUEST LOOP. DONE. <<<

> Guests should be able to visit and purchase from shops and sideshows, new visitors should arrive,
> visitors should go home, and they should be dropped off and picked up at the front of the park by a
> **cruise ship**, a **sea plane** and a **bus**.

**Where it stands, 2026-09-20: every clause of that is now true.** Guests arrive by themselves, are
carried by all three vehicles, go home when their day runs out, and **buy from both the shop and the
sideshow** — a filled park took **1110 at the Drinks Shop** (37 sales at 30) and **900 at the Jungle
Spray** (45 at 20). Both halves were confirmed in the running game by census **and** by screenshot.

**This reordered the queue, and that reorder is now spent.** Items 3, 6 and 8 were halves of one loop
and all three are ticked. **Items 2, 4, 5 and 7 remain, and Alexah picks which is next.** Item 5 is
the one worth flagging rather than choosing: it is the last fault Alexah found *by playing* that is
still open, and it is small — the gauge's own arithmetic is already exonerated, leaving the
`meter.wct` mapping.

**The order, agreed with Alexah:**

| | | why here |
|---|---|---|
| ~~**1st**~~ | ~~**Arrivals, and the three vehicles** — item 3~~ **DONE 2026-09-20** | Nothing else in the loop can be seen without people coming in |
| ~~**2nd**~~ | ~~**Departures** — item 6~~ **DONE 2026-09-20**, built with the first rather than after it | The same vehicle loop: one that drops off must pick up. Built alone it drains the park |
| ~~**3rd**~~ | ~~**Sideshow spending** — item 8~~ **DONE 2026-09-20** | It needed the win roll, which nothing had ever written: every visit took the losing arm |
| ~~**4th**~~ | ~~**Shops** — item 8~~ **DONE 2026-09-20** | The "structural blocker" was a misread field. Nothing structural was in the way |

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
| ~~8~~ | ~~Guests cannot buy anything from a shop, and barely from a sideshow~~ **DONE 2026-09-20** | — |

**Four remain: 2, 4, 5 and 7.** Every one of them is independent of the others — none is blocked on
anything now built, and none blocks another.

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

## 2. Four of the six gadget buttons do nothing - BUY IS DONE, 2026-09-21; THREE REMAIN

**>>> NOT TICKED, ON PURPOSE. <<<** Buy now works and carries the whole purchase/hire/management half
behind it. **Info, Money and Research are still inert**, and the paths-and-queues half of Alexah's own
dependency order is untouched, so this item stays open.

- [x] **Buy** opens `ParkBuyScreen` (stream `0x00754cf8`), which cross-links to `ParkHireScreen`
      (stream `0x00751fa8`). Both are built on the original's control **type 7**, a scrolling
      multi-column list that nothing in this tree had - `UiList`. A row's payload is sized by the
      COLUMN COUNT, which is why buy pushes three and hire two.
- [x] **Clicking a placed ride opens its management window** - `ParkObjectWindow`, stream
      `0x00755150`. **There are nine such windows**, dispatched by `FUN_00486920` on the thing's kind
      byte and the item's `WhichUIType`; the other eight are counted by name. Close, delete, move and
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
      so that deviation is declared at the site rather than a wider clock invented for it. (This entry
      previously called the scissor unproven - Alexah's report of a *"harsh cutoff"* across the model
      is that scissor clipping, observed, so it is proven.)
- [x] **The verbs underneath**: `ParkBuilding` buys, sells, moves and carries; `ParkStaffPool` and
      `ParkPeople` hire, fire, pick up and put down. Money is taken at PLACE time, from the item's
      `+0x1b8`, which is why cancelling a carry needs no refund.
- [ ] **Info, Money and Research** - still `NotYet(...)` at `ParkGadget.cs`.
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
      **NOT YET SEEN ON SCREEN** - the capture instrument was returning stale frames (see below), so
      this rests on console evidence alone. The tool is still reached only from the console; wiring
      it to the hover classifier (class 1 = a path cell, class 2 = **plain ground**) is what remains.
- **>>> SETTLED: NEWLY BUILT PATH DOES RENDER, AND THE METHOD THAT SETTLED IT IS THE POINT. <<<**
  A **difference image** against a **control pair** is what finally answered it, after nine wrong
  explanations. Lay one cell and the diff shows a single clean quadrilateral on open grass; lay nine
  more along the same row and it becomes a continuous band. Both are plainly distinct from the
  guest-shaped and advisor-shaped blobs elsewhere in the frame. **Shape discriminates where a scalar
  cannot.**
  **The control pair is the part worth keeping.** Two frames with *nothing done between them* differ
  by **2.54–2.60%** of the frame, because a running park moves guests, flags and water. So every
  whole-frame percentage quoted earlier in this work — 1.42%, 1.04%, 0.15% — was **below the noise
  floor**, and the 1.42% once offered as proof was smaller than doing nothing at all. Measure the
  floor first; a percentage above it means nothing until its *shape* is inspected.
  **Two further traps, both real:** never grab while paused (`pause` + `step` stops presentation and
  yields byte-identical frames even as the renderer reports new geometry); and aim the camera at the
  *built park*, since ten cells out on empty terrain at zoom 80 are a few pixels near the horizon.
  **What was NOT wrong:** the game. No deferred-disposal race, no material-slot fault, no diverging
  `ParkState`, no HUD refresh defect — all of those were my hypotheses and all were refuted. The
  `drawn` console command settled the other half in one frame: `paths 88 cells, overlay 10 changed
  cells, balance 87912`, with `money` reporting 87912 in the same run and
  `Level.ParkState is ParkState.Current: True`.
  *(The superseded diagnoses are kept below, because the sequence of wrong answers is the useful
  record - each was plausible, and two were asserted confidently before the cited line was read.)*
- **The superseded readings, wrong but instructive on their own terms.**
  A control shot settled it in one frame — aim the camera at the park's *own* shipped avenue, cell
  (47,21), and everything renders: the walkways, the gate, the Belly Bounce, the guests, the river.
  Every failed capture was aimed at cells x≈4–13, y≈9–15, which is **bare ground outside the built
  park**, where ten new path cells at zoom 80 are a few pixels near the horizon. The cells were laid
  correctly the whole time; the camera was looking at the wrong corner of the map.
  **So the game was never at fault and the harness was never broken** — the *test location* was. The
  `drawn` console command settled the other half: in one frame it reported `paths 88 cells, overlay
  10 changed cells, balance 87912`, and `money` in the same run reported 87912. Simulation and
  renderer agree exactly, and `Level.ParkState is ParkState.Current: True`.
  **The earlier withdrawal is itself withdrawn:** the HUD money *does* track spending. What looked
  like a divergence was two readings half a second apart with gate takings arriving between them.
  **The lesson worth keeping is the method, not the bug.** Seven explanations were offered — stale
  compositor, unsettled frames, frame budget, leftover processes, a HUD refresh defect, two diverging
  `ParkState` instances — each plausible, each wrong, and two of them asserted confidently before the
  cited line was read. What ended it was a **control**: photograph something already known to be
  there. That is cheaper than any theory and should have been the first move.
  *(Kept below for the record: the three superseded diagnoses.)*
- **The superseded reading, wrong but instructive on its own terms.**
  The rendered frame and the console disagree about the same run, repeatedly and in different ways.
  In one run the console read money 87987 → 87787 with the surfaces rebuilt from 78 to 88 path cells,
  while the frame showed 87987 and 205 changed pixels. In the next, the frame showed **88012** — a
  figure in neither reading, being the starting balance with the 200 **never spent**.
  - *"The compositor holds stale content"* — refuted; raising and focusing changed nothing.
  - *"Grab until two consecutive frames agree"* — refuted, and it made things worse: the delta fell
    to **exactly 0**, two byte-identical frames. **A frozen renderer passes that test perfectly**, so
    it cannot tell "settled" from "not drawing". That check was worse than none.
  - *"`step` spends a frame budget, so resume first"* — partly right; presentation resumed (0.15%)
    and the frame still disagreed.
  **What is not in doubt**, because the console, the censuses and the game's own log agree across many
  runs: the cells are laid, the masks and tiles are right, the money moves, the surfaces rebuild, and
  `save/` never changes. **What is in doubt is every screenshot claim.** A claim from an earlier
  capture is therefore withdrawn — that the HUD money moved 87987 → 87887 on screen. The plus of path
  in that image is real geometry; the timing and the cost it seemed to corroborate are not safe.
  **The next step is not a fourth guess**: have the game report, per frame, the path-cell count and
  the balance it is actually drawing, so the picture and the numbers come from one place.
- [x] **Placing by pointing - DONE 2026-09-21.** Both screens put the item or the person in the hand,
      and clicking the park puts them down. `Level.WorldClick` takes the click only when the interface
      did not, and **anything in the hand goes down before any window opens** - the original's own
      order, since a place mode consumes the click and only an idle mode opens windows. The console's
      `put` and `hire` still do it in one step, for a test that cannot move the pointer.
      **The right button cancels** - the original's own way out of a place mode. Nothing is charged
      for picking something up, so putting it back gives nothing back and takes nobody out of the
      pool. **That button's edge is no more driveable by a harness than the left's**, so the console's
      `drop` reaches the same `Level.CancelCarried` rather than a copy, and the shared body is what is
      measured: carry a ride, drop it, click the park, nothing is built.

### The original section, for the part still open

- [ ] **Seen:** Info, Money and Research do nothing. **Buy was the first thing anyone clicks and was
      inert too; it works as of 2026-09-21**, so this bullet now covers only the three that remain.
- **Lives:** `ParkGadget.cs:339/354/357` - `b_info`, `b_money` and `b_resrch`, three `NotYet(...)`.
  Camcorder, Map and now Buy are the three that work.
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

## 8. Guests cannot buy anything from a shop, and barely from a sideshow — DONE, 2026-09-20

- [x] **Guests buy from both.** Confirmed in the running game: a filled park took **1110 at the Drinks
      Shop** (37 sales at 30) and **900 at the Jungle Spray** (45 at 20) across two runs, with the
      `spend` census showing the cause before the effect — guests `heading {shop:16}` and then the till
      moving. `save/` unchanged within every run.
- **>>> THE "STRUCTURAL BLOCKER" WAS A MISREAD FIELD, NOT A BLOCKER. <<<** The filter compares a
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
- **>>> AND THE SHOP NEEDED NO NEW SCRIPT MECHANISM. <<<** `docs/exe/ride-operation.md` claimed "a shop
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

- **34 of 106 opcodes are unimplemented** (`TRIGWAITANIM` was built 2026-09-20) — but only three are reached by shipped content in this park,
  so it is mostly invisible here. Take the count fresh; three README lines and `RideScriptFile.cs:99`
  still quote older ones.

## Deliberately not on this list

Each was deferred for a measured reason, not for want of interest: the handyman's litter arm (**no litter
source in this park**), the `BUMP` / `COAST` / `HOP` families (**their rides are not placed in Lost
Kingdom**), the HUD's four screens, and the other three themes.
