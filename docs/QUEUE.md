# Work queue

Begun 2026-09-22 against `main` at `9b0ebab`; items from Q35 on were filed against later tips. File and doc line
numbers in Q13-Q34 are from `9b0ebab` and have drifted (`park-engine.md` by up to ~800 lines): find the member or
the heading, never the line.

**How to use this file.** One item per session. Take the first unticked item and do only that one.
Tick it in the commit that lands it, with the number that proves it. If an item turns out to be two,
split it into two lines here and stop after the first. Alexah may reorder; nobody else does.

**Every item, no exceptions.**

- Start from the repo root. Read `CLAUDE.md`, `docs/STATUS.md`, then this file.
- Branch from `main`, named for the item, next free number: `alexah/N-<what-it-changes>`.
- "Confirm" means the running game: a screenshot **and** the census or log line that goes with it,
  both. Predict the number before reading it. A test is added for regression only. It is never the proof.
- Put the bug back and re-run the new test. If it stays green, the test is hollow. Fix the test.
- Build and test the commit alone in a throwaway worktree.
- Facts found go in `docs/exe/`. The commit message says why. The code comment says what the code does
  now. No "used to say", no dates, no `>>>` banners in code.
- Update `docs/STATUS.md` in the same commit. It stays under 120 lines.
- When confirmed, fast-forward merge into `main`. Commit locally. **Pushing `main` still needs Alexah's
  yes** (rule 1). Say what is ready and stop. Do not start the next item.
- End with four lines: what is ready, what was confirmed on screen, what was not, what the next item is.

**Where the detail is.** `docs/REVIEW-2026-09-21.md` section 5 for Q1 and Q3 to Q7.
`docs/REVIEW-2026-09-22.md` for Q2 and Q8 to Q12. Q70-Q75 come from the 2026-09-12 review, whose three
artifacts are listed in `docs/history/README.md`.

---

## A. Bugs first

- [x] **Q1. A ride bought this session never takes a turn.** `ParkPeople.cs:1270, 1400, 1468, 1573`
  loop over `world.Objects`, the save file's list. So do `ParkRides.cs:228, 653`, `ParkObjects.cs:203`,
  `ParkFixedItems.cs:214`. Bought things live in `ParkState.Objects` (`ParkState.cs:144`). Read from
  `ParkState` everywhere the simulation asks "which objects are in the park". Confirm: buy a ride, let the
  park run, screenshot a guest boarding it, `rides` census beside it.
  **SPLIT 2026-09-22, and the first half has landed** on `alexah/109-bought-things-join-the-park`. What
  is done and confirmed in a running park: the four `ParkPeople` reads, a **live object chain** on
  `ParkState` (the original links a new thing at the HEAD, `FUN_00519d80`, and unlinks on demolish,
  `FUN_00519dc0` - one call site each), and the `CanLoad` / `Flags` that `Buy` never set. Measured: a
  bought Belly Bounce appears in `objects` (14 → 15) and in `rides` with `capacity 5 duration 30`, its
  script cycling `role 2`, where before it printed **no line at all**; thing 13 still carries riders, so
  nothing regressed. **Q1b landed too**, so a bought thing now carries a real entry and exit cell,
  derived the original's way from its own shape picture.
  **TICKED 2026-09-22 on the census, with the photograph short - both halves stated.** A guest boards
  a ride bought during play, measured in **five independent runs**: `onride 1 bouncing 1: 29@0'body'`,
  then 29 again, then 43, 43 and 57, each on the ride's own bounce node at world (425.4,252.6), each
  with a different scream sample, and `save/` unchanged within every run.
  **The last cause was `PeepBehaviour.Chosen`**, which resolved `MajorDest` against `_park.Objects` -
  the file's list. A bought ride is not in it, so `JoinTheQueue` bailed into `GiveUpOnIt` the moment a
  guest arrived: they chose it, walked the whole way, gave up silently and chose it again. One line,
  and it un-blinds all five of `Chosen`'s callers. Mutation-checked: putting it back **survives all
  888**, so it rests on the game run as its siblings do.
  **What is NOT met is the picture of the rider.** The bought ride is photographed standing,
  animating (5.95% of pixels change between two frames, bounded to the rows its body occupies) and
  with guests queued beside it - but the rider's node is at z **10.3** while the camcorder's eye is
  **5.0** at `pitch 0.0`, and the console has no pitch argument, so the dinosaur's own body stands
  between the camera and the guest. `docs/VERIFYING.md` 111 and 112 carry what the aiming cost.
  Worth one short session with a pitch argument added to `camcorder`.
- [x] **Q1b. A bought thing has no entry cell, so no queue can serve it.** Done inside Q1's commit
  (`95dcf29`) - decode and build in one session rather than two. `ParkBuilding.Buy` left `EntryPos` and
  `ExitPos` at their record defaults, and `ParkRideChoice.CanBeOffered` (`:90`) refuses on
  `EntryPos == 0` **before** the queue is ever walked (`QueueCellsFor` (`:167`) bails on the same test),
  so a queue laid and joined to a path still measured `cells 0 back 0` - which reads exactly like a
  queue fault and is not one. The `Info.Shape` picture's markers are now parsed, carried through
  `ParkItemCatalogue.Item` and set on buy, confirmed in a running park as `cell (42,23) type 9` after a
  buy, against the shipped ride's own `mEntryPos` 2997 = (52,23). **The derivation itself is written
  once, in `docs/exe/park-engine.md`** under "Where a built thing's entry and exit cells come from"
  (`:790`): the three formulas at `0x004db2da`..`0x004db36b`, and `FUN_004d9cc0` named as
  `MapDelta::Rotate` from its own assert. `mTopLeft` is deliberately still not set - nothing in this
  tree reads it. The checkbox was left unticked when the work shipped, while `docs/STATUS.md` has said
  it was ticked since that day.
- [x] **Q2. Things loaded from the save cannot be clicked.** Done 2026-09-22,
  `alexah/110-click-a-thing-the-save-placed`. **The stated cause was half right and the prescription
  was unsafe.** Occupancy is not merely unset for the save's objects: a placed thing is on exactly
  **one** cell's occupancy list - its anchor - and owns the rest of its footprint through `mParentID`,
  the packed cell of the owner. Measured over the shipped park: all twelve cells of the Belly Bounce
  read `par 2996` while only (51,23) reads `occ 13`; the Jungle Spray's nine read `par 3892`; the
  Drinks Shop's four `par 3884`. **Stamping the footprint "the way `Buy` does" was tried first and
  broke guests**: the occupancy links are keyed per THING, so pushing one thing onto twelve cells makes
  the twelfth overwrite the first and a guest underneath is lost -
  `AGuestStandingOnACellIsKeptBehindTheThingBuiltOverThem` went red, expected 7 got 0. The fix is
  `ParkPicking.ThingOn`: whoever is standing there, else the object that owns the cell. `Buy` now writes
  the owner over the footprint and enters only the anchor; `Sell` leaves the list rather than zeroing
  its head.
  **The instrument was wrong too, and would have hidden the whole thing.** `worldclick` resolved the
  thing with `CellAt( x, y ).Occupant` directly and only then called `ClickWorldAt`, so it never went
  through the picking code at all - the baseline and every confirming run through it would have read
  1 of 12 whatever the fix did. It now resolves the way a frame does.
  Measured in a running park, every count predicted before it was read: the save's Belly Bounce
  **12 of 12** cells (was 1), Jungle Spray **9 of 9** (was 1), Drinks Shop **4 of 4** (was 1), open
  ground beside the ride **0 of 4**, a ride **bought** this session **12 of 12** - a half that had no
  baseline, since `Stamp` no longer writes occupancy - and **0 of 12** after selling it. Clicking
  (52,25), a cell the anchor does not cover, printed `Ride window: showing 'Belly Bounce' (thing 13)`
  and `world click: opened the window for thing 13`, photographed open. `save/` unchanged within the run.
  **A mutation survived twice and was closed rather than declared.** Keying the owner on the
  footprint's top-left instead of the anchor passed all 892, and still passed after a turned-thing test
  was added - because `ParkFootprintOccupancyTests` writes the owner in its own helper and never called
  the mutated code at all. Diagnosing the cause ("no test builds a turned thing") and then fixing the
  wrong thing is the whole of that miss. `Stamp` is now internal and called directly by a test using the
  shipped Staff Room's own numbers - anchored (58,16), covering (58,15)..(59,16) - which is the same
  reason `ParkPicking.ThingOn` is internal.
  **A second defect was found while closing it.** `Sell` swept `LeaveCell` across the whole footprint,
  and `LeaveCell` drops a thing's own links whether or not it found it on the cell asked about - so the
  sweep reached the anchor with nothing left to relink and put nought into the head instead of promoting
  whoever stood behind it. It lost a guest only for a **turned** thing, since every other footprint
  starts at its own anchor and the sweep happened to reach it first, which is why the suite and a whole
  driven run stayed green over it. The cleanup is now `Unstamp`, beside `Stamp`.
  Final mutation record, each called in advance: the owner fallback removed **5 red** (predicted 4),
  occupancy no longer asked first **2 red** (predicted 1) - both under by one, because
  `LeavingACellTheThingIsNotOnKeepsWhoeverStandsBehindIt` rests on both and was counted for neither -
  `Stamp` keyed on the corner **1 red**, `Unstamp` leaving the corner **1 red**, both as predicted.
  **Four mutation results before those were fiction and were thrown away**: the build was failing on a
  brace, `dotnet test --no-build` ran the previous assembly, and the harness swallowed the compile error,
  so four runs reported an identical clean 894 and read exactly like four survivals. See `VERIFYING.md` rule 113.
- [x] **Q3. A laid queue cell has no neighbours and no tile piece.** Done 2026-09-22,
  `alexah/114-a-laid-queue-joins-up`, the build half behind `alexah/112`'s decode. **Both halves of the
  title were real, and this entry's account of each was wrong in a different way.**
  **The neighbour half is a PAIR and only one end of it existed.** `ParkBuilding.Mark` wrote the end
  cell's bit; nothing ever wrote the bit back on the cell it faces, so the two did not adjoin and
  `CellEdge.Blocked` refused the step in. `JoinToWhateverIsThere` now writes it - OR'd rather than
  over, the way `FUN_00528a70` does at `0x005297f0`..`0x00529837`. Measured: a ride bought at (41,23)
  leaves (42,22) `neighbours 0x10 direction 0x00` on bare ground, where the baseline read `0x00/0x00`.
  **The `mDirection` half of that pair is REFUTED and is deliberately not built.** The decode has the
  faced cell taking a direction byte too; the shipped exit at (52,26) faces (52,27), which reads
  `neighbours 0x39 direction 0x00` - the bit, and no direction. It is the only shipped cell that can
  testify, because the entrance's faced cell is a queue cell whose byte the queue tool would write
  anyway. Written up in `park-engine.md`.
  **The tile half, and "piece 0" was wrong.** `Retile` returned for anything not a path, so a laid
  queue cell kept the tile index of the ground under it - **55 on bare ground, not piece 0** - which is
  outside `ParkQueues.Pieces`, so it drew **nothing at all**. `FUN_00535dd0` has an `abs(mType) == 3`
  arm and `ParkPathTiles.TileFor` already held its eleven-row table, so the fix was a caller plus the
  mutual-path-link count.
  **A third defect was found by LOOKING, and no number in the run reported it.** Two mutual path links
  bump the index past the game's eight models; the ground has already left the cell to the queue
  renderer, so **the sky showed through a hole in the park**. Links are now dropped until the index is
  one the table holds, counted as `QUEUE_TILE_INDEX_OUTSIDE_TABLE` - the original gates that bump on a
  TRACK-cell flags test this project has no layer for.
  **The rotate was inverted, with a test pinning it that way.** `RotateBit` turned a compass bit two
  places the wrong way, so a thing built at 90 or 270 pointed its way in back across its own footprint.
  `FUN_004d8c20` left-rotates by `log2` of the angle's base bit - a RIGHT rotate of two per quarter -
  and `MapDelta::Rotate` agrees at all four angles. Only a quarter turn separates the two senses, and
  nothing in the suite had ever built one.
  **Note (3) below was an INSTRUMENT limit, not a game rule.** The console's `queue` always passes
  `lastOfRun: true` so it can never lay queue over path; `Level.RunBuildMode` passes it correctly, and
  the decoded gesture - lay a path run, then queue over it - works.
  **Confirmed in a running park, every count predicted first:** the run reported `stopped after 3`,
  exactly the refusal the original makes on the last cell; the three cells read `index 5/5/5` against a
  baseline of `55`; `drawn` went `queues 4 pieces` to **7**; thing 43 went `cells 1` to **`cells 3`**,
  `OFFERABLE True`; and a guest **walked it** - thing 35 `InQueue at (44.385,22.498)` on a cell laid
  this session, thing 29 `Riding`, `queue 1/12`. Photographed against a control frame of the shipped
  queue and a before frame of the same view at the same zoom. `save/` unchanged within every run.
  Mutations, each predicted before running: **2, 2, 3, 1, 1** red, plus one deliberate survivor.
  **>>> REOPENED THE SAME DAY BY ALEXAH, WHO PLAYED IT. Two faults, both real, both outside what the
  confirm above had tested. <<<**
  *"I'm unable to build queues for newly purchased rides still. The node never shows up to begin
  building a queue. The exit doesn't seem to connect up to an existing path when placed against one
  either."*
  **(1) NO PLAYER COULD START A QUEUE AT ALL, and the confirm above did not notice because it armed the
  tool from the debug console.** `ParkBuildMode.Arm` had exactly two call sites and both were
  `DebugConsole`; no UI armed any build mode, so every number above was produced through a route the
  game does not have. The ride window's queue button (`0x3e34`) was built and clickable and fell into
  `Verb`'s default arm. It now arms the tool against that ride, and **clicking a queue cell re-arms it
  for the thing that queue serves** - the original's mode `0x14`. Both are declared deviations in
  mechanism: `b_queue`'s own handler is undecoded, and `0x14` is entered from an EXISTING queue cell so
  it cannot be the route to a first one.
  **(2) THE EXIT WAS JOINING ALL ALONG AND NOTHING RETILED THE PATH.** `ParkBuilding` had no
  `Retile` call anywhere, and `ParkPaths` redraws each cell from its STORED tile index - so the mask
  said joined and the art went on showing the piece it drew before the ride arrived. The entrance side
  was repaired by accident, because laying a queue against it calls `RetileAround`; the exit side never
  was, which is exactly the asymmetry reported. Measured twice on clean builds: (36,27) goes
  `neighbours 0x00 index 0 angle 0` to `neighbours 0x01 index 1 angle 180` on the purchase alone.
  **(3) A ride whose picture marks no entrance no longer has a footprint CORNER typed as its way in.**
  `ItemDescriptionFile` zeroes all four deltas when there is no `S`, so entry and exit both fell on the
  anchor. Six of the jungle's seventeen rides are in that case - the three coasters, the go-karts, the
  water ride and the TV simulator - and they are now counted (`PLACED_ITEM_WITH_NO_ENTRANCE_MARK`)
  rather than given an entrance the picture does not declare.
  **Confirmed through the PLAYER'S OWN ROUTE this time**, against a clean build: `worldclick` opens the
  ride window, `click` on its queue button replies `the interface took` and `tool` reads back **mode
  3**, the window closes itself, two world clicks lay the run (`anchored`, then `stopped after 3`), and
  `drawn` goes `queues 4 pieces` to **7**. Clicking a laid queue cell re-arms the tool for thing 43,
  repeatably. **Photographed: the queue the player built, standing and joined, with no window over it.**
  **NOT photographed: the exit joining its path** - the ride is still an unhatched egg over its own
  exit cell, and a pale cyan band traces the cell edges beside it. `ParkObjects.CoversGround` is true
  for types 4, 9 and 10 so the ground leaves those cells alone, while `ParkPaths` draws tile set 1 and
  `ParkQueues` set 2 - **so a ride-end cell is drawn by nobody**, which is the same shape as the hole
  fixed above. Not chased this session; it is the next thing to look at.
  **One instrument defect found and recorded as `VERIFYING.md` 116:** a mutation harness restores the
  SOURCE and leaves the last mutation's BINARY on disk, and the game run minutes later drove it - which
  reported the queue-cell click as broken when it was not.
  **>>> THE NODE, and what it took: 2026-09-22, `alexah/115-a-placed-ride-lays-its-queue-node`. <<<**
  Alexah, twice: *"The node never shows up to begin building a queue."* **Decoded first, two workflows
  with a refuter per claim, and built from the decode.** The node is not a marker and not an arrow: when
  a ride with a queue goes down, **the placer stamps a one-cell NOMODIFY queue cell before its entrance**
  (drawn as `quedead`), a NOMODIFY path before its exit, and **the commit hands the player the queue tool
  anchored on that cell** - `FUN_0052a050` then `FUN_0052f580(3,0)`. So the next click lays the queue.
  The shipped park carries every stub: its nineteen NOMODIFY cells are the ten-cell avenue and these nine.
  **Four more defects came out of the decode, one of them large.**
  **(1) The shape alphabet was wrong.** `Info.Shape` is looked up in the executable's own table at
  `0x007396c8` - a keypad, `8 6 2 4` the entrances and `N E S W` the exits - so **`2` is the entrance and
  `S` an exit**, and the reader turns the rows upside down. The old reading agreed with the Belly Bounce
  by symmetry and nothing else: six jungle rides had no entrance at all and six more had it on the wrong
  cell. All eleven placed catalogue objects now land where the save has them.
  **(2) A queue run on bare ground never joined its neighbours both ways, nor the path it ended on.** The
  writer the old `QUEUE_CELL_NEIGHBOUR_AUTHORING` count could not find is the queue arm of
  `FUN_005348d0`: a gated link back to the previous cell, a bond to the entrance on a run's first cell,
  and nothing by type. The run's last cell may be a path - it stays a path, is joined, takes the ride as
  owner, and the tool puts itself away. Replaying it reproduces the shipped queue field for field.
  **(3) The tool never put itself away** (`Disarm` had no caller). It now ends on a path or its own
  queue, on a click at its anchor, on a red preview, and on a quick right click with RMB cancel on.
  **(4) The ride window's queue button is decoded**, `FUN_004af200(0)`: it installs mode `0x14`, the same
  "edit this queue" a click on a queue cell installs, which re-anchors on the queue's far end. The
  declared deviation and its count are gone.
  **The squares are built too** - the queue tool's strip from its anchor to the pointer, blue, red,
  `m_link`, `m_end`, from `data/generic/dynamic/textures` - and the cursor follows them. Ripple, red blink,
  icon turn and blend are counted. **`garrow.MD2` / `rarrow.MD2` are not the node**: an older model
  version the only `.md2` reader refuses, never named in the image - shipped data nothing reaches.
  **Confirmed through the PLAYER's route, every value predicted first:** `carry 1100` and a world click
  at (42,24) answered `queue node at (43,23)` with the tool armed there; the cell read `type 3 neighbours
  0x10 direction 0x10 flags 0x0020 tile set 2 index 1 parent (42,24)`; the strip to (39,23) read blue x4
  then `m_link`; one click laid three cells and joined the path (`0x50`, `0x44` x3, the path gaining
  `0x04` and the ride as owner). The queue button, a queue-cell click, a red run, the anchor click and
  `rightclick` each did what was predicted. **Aztec Mayhem, one of the six the old reading gave no
  entrance, placed at (57,23) with its node at (58,22), its `N` exit at (59,23) and that exit's path at
  (59,22), joined to the loop in one click.** After `load 40`, guests chose the new Belly Bounce, one stood
  `InQueue` in the queue the player laid and one went `BeingAdmitted` to `Riding`, screaming.
  Photographed: the node with its square, the strip, the joined queue, the exit joined from the far side,
  Aztec Mayhem. `save/` unchanged within the second run; the first run's `opentpw.cfg` rewrite was the
  loading bar relearning jungle's step count after the park started loading four more textures.
  **One prediction was wrong and says so:** the corpus test expected fourteen placed objects and compared
  eleven - the decoder's fourteen counted the bus, gates and lights, which the buy catalogue does not
  carry. All eleven matched. `VERIFYING.md` 117.
  **A review workflow then found six real defects, each fixed and decoded where it needed to be:** a queue
  run across a path kept the path's links (the stamp force-clears it first); nothing put the queue tool
  away when the player picked up something else (one mode, not a tool and a hand); **selling or moving a
  ride left its NOMODIFY node standing for good, so a moved ride could not go back on its own spot** -
  decoded from `FUN_00527ee0`: selling drains the whole queue, node included, refunds N-1 cells, and
  hands the paths before its ends back as ordinary path; placement refusal now follows the placer's own
  test pass; the preview's cash test skips path cells and shows `c_cash`; and the Ctrl-to-place-another
  and track-ride branches are built or counted. Confirmed in a third run: `sell` answered `its queue for
  225`, all four cells went, and the same ride went straight back onto (42,24) with its node.
  **Then Alexah answered from memory of the original:** the squares were see-through, "waved like a
  flag/water", and a right click put the tool away. The wave is decoded and built (`alexah/116`): each
  corner rises by `sin( phase + x + z )`, one world unit, from `FUN_004708d0`'s 4,096-entry table, the
  phase gaining 0.1 a frame unless paused. Confirmed by two frame pairs: under `pause` identical, running
  the strip's band moving. The brightness half of the wave is counted (`MARKER_RIPPLE_SHADING`).
- [x] **Q4. Sell leaves the ride's script bound and scheduled.** Done 2026-09-23,
  `alexah/118-a-sold-thing-takes-its-script-down`; decode in `docs/exe/park.md` "What selling a thing
  does to its script" and `park-engine.md` "The demolisher's order, and the cells it leaves". `Sell` now
  unbinds (`ParkRides.Unbind`, the scheduler's one-level `Destroy`, mode 7), which also takes whatever
  the script spawned; the death particle, the demolish sound and the eviction are counted, not built.
  **The Confirm as written was hollow**: the `rides` census walks what is standing, so it dropped a sold
  thing with or without the fix. The header now prints `scripts N bound M`. **The unmeasured finding was
  real and visible**: on the unfixed build a sold save-placed ride left a sky-blue hole in its
  footprint's shape and refused anything built there again. `Unstamp` now writes the original's cleared
  cell (tile 55, the ends' counters zeroed) to the cells the thing owns, and so does the queue drain,
  which had left the node's counter at one. The gates can no longer be sold from the console. Confirmed in the game, every number
  predicted: scripts 16, 15 after the window's Delete, 16 back on its own spot, then 15, 14, 13, and 13
  after 20 s running. The item as written: `ParkBuilding.Sell` removes the model and the state object;
  `ParkRides` has no unbind. Add it, and drop queue cells keyed to the sold thing. Confirm: sell a running
  ride, `rides` census no longer lists it, no errors in the log, screenshot. Also check `Unstamp`'s
  `ClearRecord`, which falls back to the SAVE's record.
- [x] **Q5. Console Move is Sell then Buy.** Done 2026-09-23, `alexah/119-move-keeps-the-thing-in-the-hand`;
  decode in `docs/exe/park-engine.md` "Moving a thing". `ParkBuilding.PickUp` is the one body for the window's
  Move and the console's: the sale, then the item into the hand **at the thing's own angle** (the original's
  `FUN_0052f1b0( [thing + 0x10], 1 )`) and with no affordability test, which the original has only at put-down.
  Console `move` is that and one put-down; a refused cell leaves the thing in the hand. The sale answers a
  value (`Demolish`) that the pickup reads, where Move parsed `Sell`'s string. Confirmed in the game, every
  number predicted: `move 13 58 16` refused, `carry` answering `hand: holding 'Belly Bounce'`, scripts 16 to
  15, balance 87987 to 88712. **Nothing draws a carried thing** (`CARRY_PREVIEW_MARKERS`), so the screenshot
  shows the ride gone from the park while the hand holds it, and the next click stood it back up for 500 -
  through the window as well, after a refused click. The Staff Room moved and put back turned 90, its frame
  identical to before. The review filed Q39 (the hand's ways out). The item as written:
  `ParkBuilding.cs:163-177`. A refused cell loses the
  object and banks the refund. Do Sell then Carry, as `ParkObjectWindow.Move` does. Return a result
  value, not a string the caller parses (`:171`). Confirm: move to a cell that refuses, screenshot the
  object still in the hand.
- [x] **Q6. Placing a carried staff member destroys the candidate before the hire can fail.** Done 2026-09-23,
  `alexah/120-a-refused-drop-keeps-the-candidate`; decode in `docs/exe/park-engine.md` "Putting a candidate
  down: the type-5 mode", every claim put to a refuter. The original's place-staff click answers a refused
  cell with an inert log line and nothing else - mode, preview and candidate stay, and the next click tries
  again - and the pool loses a candidate only in the mode's uninstall, once a click was accepted.
  `ParkStaffPool.Hire` is the one body for the click and the console's `hire`: the worker goes up first, the
  pool loses them second, and a refusal leaves them on the cursor and in the pool. **The original has no
  off-map refusal** (its picker clamps to an edge cell), so this park's three refusals are its own, said at
  the site. Confirmed in the game, every number predicted: on `main` a refused drop printed `hired Duke Mighten
  as thing 0`, candidates 22 to 21, staff 5, his row gone from the hire screen; with the fix `cannot be put
  down`, 22 and 5, the row unchanged, and the next click hired him as thing 43 at (49,25), 21 and 6,
  photographed standing. Filed Q40. The item as written: `ParkStaffPool.PlaceCarried`
  (`ParkStaffPool.cs:95-100`): Take, then Carrying = 0, then Hire, which can return 0. Use the console's own
  safe order. Confirm: put down on an off-map cell, candidate still in the list, screenshot.
- [x] **Q7. Leaving a park does not empty the hand.** Done 2026-09-23, `alexah/121-leaving-a-park-empties-the-hand`;
  decode in `docs/exe/park-engine.md` "Leaving a park with something in the hand", every claim put to three
  refuters. The original's park end takes its interaction mode down while the park still stands - the save it makes
  on leaving installs the idle mode over it (`0x00516d13`), and online `FUN_00515dd0` installs none - so its hand never
  outlives the park. `Level.ForgetPark`, part
  of `Unload`, now lets both hands go through their own `Drop` and logs `Leaving the park:`. Confirmed in the game by
  the player's routes (the window's Move, the hire screen's row, Exit To Lobby, the island gates), every number
  predicted: on `main` a moved Belly Bounce outlived Exit To Lobby and the jungle's next first click built a second
  one at (42,24) for 500; a candidate outlived it into Wonder Land, where a click still tried to put him down, and
  was hired back in the jungle as thing 43. With the fix the hand came back empty, the click only picked up the path
  tool, 87987 and scripts 16, candidates 22 and staff 5, photographed; the two crops differ from `main`'s by 22.17
  and 16.41 against noise floors of 0.02 and 0.00. Found on the way: the purchase carry is mode type 3, not 4, and
  the Alt+L quick load keeps the hand in the original (static). The item as written: `Level.Unload`
  (`Level.cs:862-890`) forgets the cameras and the build mode but not `ParkBuilding.Carrying` or
  `ParkStaffPool.Carrying`. Confirm: leave mid-carry, enter another theme, click, nothing placed, log line.
- [x] **Q8. The island keys work during the fly-in, and the fly-in state survives the lobby.** Done 2026-09-23,
  `alexah/122-island-keys-wait-for-the-fly-in`; decode in `docs/exe/lobby.md` "The island keys wait for the
  fly-in", every claim put to two refuters. The original's arrow handlers (next `0x005e1ee0`, previous
  `0x005e1f40`) refuse while the camera's state `+0x14` is non-zero, before the Instant Action test, and every
  route in (the panel's arrows, the cursor keys on key-up) goes through them; its leave state is a field of a
  camera every lobby builds afresh at nought. `LobbyCameraMode.Step` is now that pair: the bracket keys
  (OpenTPW's own) ask through it, and it refuses while leaving. `ForgetIsland` clears the whole leave.
  Confirmed in the game with a real `]` through XTEST at radius 52.82, every number predicted: on `main` it
  logged `moving to island 1, 'Wonder Land'`, the camera turned to Wonder Land and the jungle loaded anyway,
  and a lobby rebuilt mid-flight kept `leave=FlyingIn radius=52.82` and loaded the jungle unasked. With the
  fix it logged `staying on island 0`, the flight ran on to 26.12 into Lost Kingdom's gate, photographed
  (0.14 from a run with no press, against noise floors of 0.17 and 0.25; `main`'s frame 38.28), and the
  rebuilt lobby read `leave=No ... waiting=False` and loaded nothing in 180 frames. The decode also found
  that Escape cancels the fly-in in the original, and that its lobby keys act on release and take Enter:
  filed as Q41 and Q42. The item as written:
  `LobbyCameraMode.cs:288-292` answers next/previous island whenever the player is not Instant Action.
  `LeaveForPark` (`:439-447`) guards only its own re-entry. `ForgetIsland` (`:761-768`) clears
  `CurrentIsland` and `_wandering` but not `_leaving`, `_whenArrived` or the three leave numbers. Block
  the island keys while leaving; clear all the leave state in `ForgetIsland`. Confirm: press next-island
  during the fly-in, the camera keeps flying into the island you chose; screenshot mid-flight and the
  log line.
- [x] **Q9. Stopping one ride's scream releases the scream effect every ride in that band shares.** Done 2026-09-23,
  `alexah/123-each-ride-screams-on-its-own-clock`. The decode is in `docs/exe/audio.md`, "How the engine plays an
  effect: priority, not a repeat delay"; every claim was put to two refuters, and `0x006bc2d0`, `0x006c3e00` and
  `0x006c0676` were re-read by hand. **The shared claim has no counterpart at all.** The original keeps nothing per
  effect, and the "2700 ms repeat delay" is a voice priority. A held scream is a chain:
  - the first child plays at once, from variation 1;
  - each later child waits a random 1000-3000 ms (effect 71), drawn from its variation header and counted from
    its own start;
  - later children take their variation from the zones, keyed by parameter 6;
  - a stop hard-cuts that ride's newest child and nothing else.

  What was built:
  - `ParkScreams` is that chain, one per ride;
  - `SoundCategory.PickFrom` picks a child's sample with no gate;
  - `SoundCategoryFile.ReadVariations` walks all 31 maps to the byte (1,267 effects, 1,595 variations).

  Confirmed in the game, two Belly Bounces with guests, `save/` unchanged in both runs:
  - **On `main`:** of 8 stops while the other ride held the same scream, the other's plays rose at the first
    poll after the stop in **6**. Those were exactly the 6 where it was between samples; in the other 2 it was
    mid-sample. In 4 of the 6 it had been kept from even its first scream (`plays 0`). At one such stop the mix
    went from -80 dB to -25 dB, and the onset is `nkwoop1.mp2` (cross-correlation 1.00), starting at the stop.
    The census names that same sample for the other ride.
  - **With the fix:** in **8 of 8**, the other ride's next child came on its own logged time (+1 to +6 ms),
    0.9-2.3 s after the stop and never at it.
  - All 147 child intervals lay inside their range, at most 8 ms past the logged wait.
  - In 11 places the two rides' children started within 100 ms of each other.
  - Every sound in one stop's mix is identified by cross-correlation against every global and jungle bank:
    - the other ride's `nkwoo2.mp2` at -232 ms and `kid22.mp2` at +2322 ms (both 1.00 and 0.99), where the log
      has -257 ms and +2300 ms, so the mix trails the log by a steady ~23 ms;
    - digital silence from +0.4 to +1.0 s;
    - then the park music's next arrangement from +1.11 s (1.00, `levels/jungle/Music`).

  Mutations M1-M8 all went red as predicted. M9, the old `Release` line put back, survives as predicted, because
  nothing a chain does reads the throttle, which M3 pins. Filed Q43. The item as written:
  `ParkAudio.StopScream` (`ParkAudio.cs:557`) calls `_kids.Release( effect )`, which sets the shared
  effect's `AvailableAt` to minus infinity (`SoundCategory.cs:169-175`). With two rides in one band, one
  stopping makes the other scream again at once instead of after its declared delay. Make the claim
  per ride, not per effect. Confirm: needs the second ride from Q1; capture the mix over one stop.
- [x] **Q10. The camcorder's blocked-cell cache is static and never forgotten.** Done 2026-09-23,
  `alexah/124-the-camcorder-forgets-the-park`. `Forget` now lets go of the edge test and the park it was built for.
  The original keeps nothing of the kind: its edge test reads the live world every step, and that world is
  freed on leaving (`docs/exe/park-engine.md`, "Walking on the ground").
  - **The sweep** (8 agents: four investigations, each put to a refuter) found three roots holding a left park in the lobby:
    `ParkState.Current`, `ParkRides.Current` (through `Entity.Level`) and the camcorder. So the fix frees the
    park when the next one is built, not in the lobby, and the other two are filed as Q44.
  - **Before the fix,** the left park lived until the first camcorder step in a later park with a save; a park
    with no save never replaced it.
  - **The instrument** is the console's new `parks`: every save seen, held weakly, reported alive or collected
    after a forced full collection, with the named roots that hold it.
  - **Confirmed in the game,** jungle, camcorder walk into the Belly Bounce, lobby, jungle again. With the second
    park up and the camcorder unused:
    - control (the fix taken out): `parks seen 2 alive 2 | #1 jungle alive, held by camcorder`;
    - fix: `alive 1 | #1 jungle collected`.
    
    The camcorder's walk in the second park then collected #1 in the control, and the heap fell 2.4 MB within that
    run; the same walk left the heap unchanged in both fix runs. Heap figures from different runs differ by more
    than that, so they are not compared. All 24 predictions held across three runs, the last on the committed
    build. The fix's walk in the second park still stopped at (50,24), photographed, and `save/` was unchanged in
    every run.
  - **The test** is `ParkCamcorderForgetTests`, a weak reference after a forced collection. M1-M4 (either field
    kept, the call from `ForgetPark` removed, no fix) all go red.

  The item as written: `ParkCamcorderCameraMode.cs:443-458` keys it on the `ParkWorld`; `Forget` (`:213-219`)
  clears stand, yaw and pitch only. The previous park's whole save stays alive through the lobby. Clear it in
  `Forget`. No game run needed; a test that enters two parks and checks the reference is released.
- [x] **Q11. Small fixes.** Done 2026-09-23, `alexah/125-small-fixes`, eight commits. The six from
  `small-fixes-v2.patch` went in by `git am`, each checked against the executable or the compiler first (10 agents,
  three of them refuters), and each corrected in place where its prose overstated: state 3 has a jump-table arm of
  its own, a bare return; the log strings are `BROKEN_DOWN` and `CONDEMNED`; `==` was true whenever rounding pushed a
  dot above 1; `BaseStream` now carries its dead-code label. Then the six doc comments, moved by hand (three had stale
  text, and the Charge one gave the sideshow formula wrong, as `docs/exe/ride-operation.md` did too). Then the cap.
  - **The guard at `Turn` does not end a spinning section.** It stops a lock outliving its turn, but `CRIT_LOCK`
    and a backward `BRANCH` with no unlock or yield loops for ever inside one `Turn`. So does the original:
    `FUN_005516b0`'s loop ends only on the budget or a negative PC (`docs/exe/park.md`, "The scheduler").
    `RideScript.CriticalStepCap` (10,000) ends such a turn, a deviation said at the site. None of the 150 shipped
    sections can loop, and the longest runs 23 instructions, 19 in Lost Kingdom ("Corpus shape").
  - **The instrument:** `rides` prints `critical N` per thing, the longest section it has run in one turn, and a
    header `critical longest N cap 10000 reached K`.
  - **Confirmed in the game,** a control on `main` and the fix, lobby then jungle, both photographed. The park frame
    paused on load is pixel-identical to `main`'s, and the 14 `Sound category` lines are identical, which is the
    `Material` and `SoundFile` commits on the load path. After 60 s: `critical longest 5 cap 10000 reached 0`, the
    Bouncy Dino's boarding path, as predicted; toilets, kiosk and Jungle Spray 3 each, the quiet path. At load I
    predicted 3 and read 0: no script had reached its lock before the pause. `save/` unchanged.
  - **Tests:** `ExpandedMemoryStreamTests`, `SoundFileTests`, `EqualityTests.RotationComparesByValue` (now with a
    quarter turn, whose dot rounds below 1, and the tolerance pinned from both sides), and three in
    `RideScriptClockTests`. 968 tests with the game. Mutations M1-M9 all red.
  - **Found:** Q45 (the VM charges `CRIT_LOCK`; the original does not) and Q46 (seven more stacked doc comments).
- [x] **Q12. Four hollow tests.** Done 2026-09-23, `alexah/126-four-hollow-tests`. Each now fails with its fix
  reverted, and with every smaller piece of that fix a 28-agent review (four mutation hunters) found surviving:
  - `TextureSamplerTests`: two stand-in cached textures of opposite values, made without a constructor, adopted by
    the real path constructor. Pins the five copies, `Requested`, and no second registration. No production change.
  - `ParkCamcorderWalkTests`: `Step` walked with a stand-in level holding Lost Kingdom - 64 ways into a footprint,
    forward and sideways, and the 105 sides where mode 2 answers otherwise than mode 0 (one side than mode 1) - and
    the dead band's upper side. No production change.
  - `LobbyLeaveForParkTests`: `StepLeaving` and `CameraSettings` internal. The sequence is stepped at 60 and at 30
    frames a second from four laps into the orbit, every count exact (121 and 185, 61 and 92), with a second Enter
    refused. The second rate is what catches a rate per frame of 60 (`docs/VERIFYING.md` rule 120).
  - `VoicePlacementTests`: `Audio.Voices` internal and `Voice.IsHeld`. The tests stand in for a device by setting
    `Audio.Ready` and play through `Audio.Play`: the hold and its release, `ParkAudio.Update` under a held
    `GameClock`, a voice born during a hold, and `StopAll` letting the hold go.
  - **Proof:** 46 mutations, each predicted and each red, the four fixes reverted among them. Still unpinned, said
    at each site: `Walk` reading the keys, `Update` placing the lobby camera, the panel's Enter, the texture's GPU
    handles, the mixer's fade, and a threshold moved by less than one frame. 979 tests with the game, 447 ran and
    532 skipped without, 123 warnings.
  - **Confirmed in the game as well**, though the item asked for no run: `~/.cache/tpw-harnesses/q12confirm.py`,
    one silent run, every reading predicted from the tests' own numbers and photographed. The first lobby's sea read
    `requested Wrap sampler AnisotropicWrap size 128x128 adopted False`, and back from the park `adopted True` with
    the same. The fly-in, stepped at 1/60 from `orbit 2.6`, read 70.00/20.00 after 66 frames, then 62.25/6.97,
    34.62/0.04 and 8.08, and asked for the park on its 185th frame. The camcorder, walked east into the Belly
    Bounce, stopped at `at=(50,23) type=0`. With the menu open, the new `voices` census read
    `Thunder4.mp2 Effects placed held` and the music flat and playing. One miss: the advisor's `Speech flat` line
    read `held` too - his own hold (`Advisor.Paused`), which I had not predicted. `save/` unchanged.
  - **Found:** Q47 (two more hollow tests), Q48 (three holes in the camcorder's sweep), Q49 (two doubted comments).
- [x] **Q36. Selling a thing lets nobody go.** Done 2026-09-23, `alexah/128-selling-lets-the-people-go`. Decoded
  first (11 agents, each report put to two refuters; `park-engine.md`, "Selling and the people on it" and "How a key
  finds its global"): `DAT_00785058` is `PeepInfo.SmallHappinessChange` (5) and `DAT_0078505c` is
  `MediumHappinessChange` (15), by the balance table's slot order and by `FUN_004fe980`.
  - Built: `ParkPeople.ThingRemoved`, called by the demolisher where it counted `SOLD_THING_EVICTION`, so a sale and a
    move's pickup both send it. A guest whose `MajorDest` is the thing, in any state, loses 5 and goes to Deciding where
    they stand; a queuer first loses 15, their own two queue links, the invitation and the place (`FUN_005012f0`, which
    does not tell the thing). A rider plays the kids' effect `0x80` at the seat (the origin on a thing without flag
    `0x20`, which admission then destroys the sprite of). Staff resting in it stand Idle and claim the nearest other
    rest area; staff on the way give it up. `GoAndRest` walks the live object chain, so a sold room is never offered.
  - **Proof:** 33 tests in three classes; 19 mutations, each predicted and each red. Two that first stayed green showed
    two hollow asserts (a lone queuer has no link to lose; a sold room's entry cannot be routed to), both fixed. The
    whole bug back turns nine test methods red. A 19-agent review found 13 real faults, all fixed or said at the site:
    among them the chooser now lets `MajorDest` go before it chooses (`FUN_004fcb10`, `0x004fcb21`), which decides who
    a sale reaches; the sound's place is a tested rule; and each mood key is pinned by a balance file of its own.
    1012 tests with the game, 459 ran and 553 skipped without, 123 warnings.
  - **Confirmed in the game**, `~/.cache/tpw-harnesses/q36confirm.py`, a control on `main` and the fix, each staged by
    letting the park run to a guest riding the Belly Bounce while two queue: on `main` all three still name thing 13
    after the sale and twelve seconds on. On the fix, all three predictions held: rider 35 `Deciding dest 0 happy 45`,
    queuers 29 and 42 `Deciding dest 0 happy 30`, none moved; one `put off` sound, `bootout.mp2`, at the seat
    `(525.4,252.6,10.3)`; `SOLD_THING_EVICTION` gone. Photographed before, just after and later. `save/` unchanged.
    Again on the committed build after the review (`q36-confirm2/`), 4 of 4: rider 43 held at 0, queuer 29 to 30,
    and two walking to it, 42 to 45 and 44 held at 0; the same sound at the seat.
    **One miss:** I predicted all three would leave Deciding within twelve seconds. The rider chose the Jungle Spray;
    the queuers stood still on cleared cells no neighbour connects to, so every wander fails and restamps the
    30-sweep gap - which the original's does too (Q53). A probe run's put-off queuer left by going home.
  - **Not confirmed on screen:** the staff arms - nobody rests in this park in the first minute (`staff` now prints
    `rest`). **Found:** Q50 to Q55.
- [x] **Q39. The hand's ways out are not the original's.** Done 2026-09-23, `alexah/129-the-hands-ways-out`. Decoded
  first (four decoders, each put to a refuter; `park-engine.md`, "The hand's ways out"): there is one hand, the current
  mode, and the setter runs the outgoing mode's uninstall before every install, whatever either type is.
  - Built: `ParkHand.LetGo` is the idle mode installed over the item, the candidate, the worker and the build tool.
    A quick right click lets go only with RMB cancel on, armed on the press and let go of after 200 ms or a move of
    more than 8 interface units either way (`Level.RightButton`); Escape lets go and opens no menu; the Delete key,
    the camcorder, the queue button, a buy, a hire, a move and a worker's pickup all let go of what was held; a sale
    lets a candidate or worker go when no item is held (`0x0052818d`); leaving the park lets go before anything in
    it is deleted. A worker let go of is put down in their own cell (`ParkPeople.PutBack`, `0x0046cdc0`). Console
    `hand` and `rmbcancel`.
  - **Proof:** 16 tests in `ParkHandTests`, one in `ParkLeaveTests`; 31 mutations, each predicted and each as
    predicted: 28 red, and three green by prediction - the second RMB option test (`QuickRightClick` makes it too),
    Escape's order against first person (a full hand there needs the console), and leaving's order in `Unload` (no
    test builds a level; the game run is its proof). The whole bug back turns 16 test methods red. An 8-agent decode
    and a 37-agent review: 22 of its 32 findings real, all fixed or said at the site. 1028 tests with the game, 459
    ran and 569 skipped without, 123 warnings.
  - **Confirmed in the game**, `~/.cache/tpw-harnesses/q39confirm.py` and `q39confirm2.py`, a real right button and a
    real Escape through XTEST, on `main` (`q39-control/`, `q39-control3/`) and the fix (`q39-fix/`, `q39-fix3/`). On
    `main` every fault showed: the press alone dropped the moved Belly Bounce; Escape over an item opened the menu
    (`windows=3`) with the item still held; a held press dropped the candidate; a candidate and an item were held
    together; the camcorder kept the item; a worker stayed Held after a quick click; `lobby` left with them held. On
    the fix every prediction held: with RMB cancel off a quick click kept the Belly Bounce; Escape let it go with
    `windows=2`, photographed, and the second Escape opened the menu; a 0.5 s press kept candidate 1 and a quick click
    put them back; `carry`, the buy row's own body, let the candidate go; `camcorder` let the item go; a quick click
    put worker 30 back at (47,19), Idle at (47.500,19.500); `lobby` logged `Leaving the park: put thing 30 back down
    at (47,19)`. `save/` unchanged in every run. **One miss:** I first read the worker's census with the clock running
    and predicted them standing at the centre; they had already walked on. Read again paused, it held.
  - **Not confirmed on screen:** the Delete key and a sale's let-go (tested only); a right press over a panel, which
    here still arms the click (Q56). **Found:** Q56 to Q60.
- [x] **Q41. Escape during the park-entry fly-in cancels it.** Done 2026-09-23, `alexah/130-escape-cancels-the-fly-in`.
  Decoded first (four decoders, each put to a refuter; `docs/exe/lobby.md`, "Escape cancels the fly-in, and the gate
  is the flight's"). The lobby acts on Escape's release; a menu or message box in front takes the key; otherwise
  `IslandLobby_OnKey` asks every active child's `+0x18` and opens the menu only if none answered. The island camera's
  `0x005e1890` puts a leave back to orbit and shows the panel, from state 2 first playing clip 1 on `island+8` - which
  is the **gate**, not the isle: state 1's arrival plays the gate's M1 (`0x005e06e4`), and `lobby.md` had the original
  never animating it. Also found: the fly-in darkens the screen, and a cancel lifts it (Q61).
  - Built: `LobbyCameraMode.CancelLeave`, the orbit carrying on from the leave's angle and the gate shut from state 2;
    `FrontEnd.MenuKey` asks it after the menu and modal checks and gives the island panel back to a player;
    `IslandPanel.EnterPark` tests the camera's state (`0x005e1ce0`) rather than a flag of its own. The gate opens at the
    homing's arrival, not at Enter, and `LobbyGate` plays each clip once over its declared span with one clip queued.
    Console `windows`, `control <id>` and `state`'s `gate=`; `LOBBY_FLY_IN_FADE` counted.
  - **Proof:** 9 tests in `LobbyEscapeTests` and a declared-span test replacing two; 22 mutations, each predicted: 20
    red and two green by prediction (the fade's counted report; Enter's camera test, unreachable mid-flight). **One
    miss:** the bug back turned E8 red as well as E7 - its prediction was written before E8 existed. An 8-agent decode
    and a 27-agent review, 18 of 23 findings real, all fixed. 1036 tests with the game, 462 ran and 574 skipped
    without, 123 warnings.
  - **Confirmed in the game**, `~/.cache/tpw-harnesses/q41confirm.py`: a throwaway player made at the slots, Enter this
    park by the interface's own click, a real Escape through XTEST at radius 52.82, on `main` (`q41-control/`) and the
    fix (`q41-fix/`). On `main` the menu opened over the flight (windows 0 to 1, frame mean 104.9 to 54.0), the flight
    ran on to 26.12 and the jungle loaded under the menu. On the fix every prediction held: `Lobby camera: Escape
    cancelled the leave for a park while FlyingIn, at angle 3.142`, `leave=No ... waiting=False`, `orbit=3.142`,
    `gate=M1,playing,0.40/2.00,then=M2`, `windows: IslandPanel`, photographed with the price and the sparkle back; 180
    frames on `cam=373,337,32` as simulated and the gate on M2; 600 more with no park asked for; the next Escape opened
    the menu and the next closed it; Enter again loaded the jungle. One near miss: M2 read 1.38 against 1.40, the
    single-precision frame the test also meets. `save/` unchanged in both runs; the run's player was deleted.
  - **Not confirmed on screen:** Escape while the camera is still swinging round (tested only). **Found:** Q61 to Q63.
  The item as written: `FrontEnd.MenuKey` opened the game menu over the flight, which ran on under it and loaded the
  park; Select New Player in those seconds reached `SelectFirst`. Confirm: Escape mid-flight; `state` reads
  `leave=No`, the panel is back, no menu, no park load; screenshot.
- [x] **Q42. The lobby's keys act on the press, and Enter does not enter the park.** Done 2026-09-23,
  `alexah/131-lobby-keys-on-the-release`. Decoded first (four decoders, each put to a refuter: 124 of 130 claims held, the
  six refuted all side details; `docs/exe/lobby.md`, "The lobby's keys act on the release, and a press on the view enters
  the park"). Nothing in the original's lobby acts on a key's press. Every key reaches the island camera through the
  lobby's full-screen root control `0xbf431` on its release: the main Enter only (`0x0d`; the keypad's is `0x0d00`), and
  the cursor keys only (the keypad's arrows are other codes). A left press on the bare view is Enter this park, on the
  press; the panel's root takes a press inside its 23-point outline.
  - Built: `Input.KeysReleased` and `MouseInfo.LeftWentDown`; the stack's `KeysWithoutFocus` and `ViewPressed`; the name
    box takes the first of Enter (main) and Escape to come up; `FrontEnd.LobbyKeys` and `ViewPressed`; `UiControl.Outline`,
    the original's crossings test `0x0066c5a4`, with the panel's L. `IslandPanel.EnterPark` makes the original's five
    tests in order, counting the keys held (`LobbyIsland.GlobalLoaded` for the record). A frame the focus left in is
    dropped whole, since SDL lets go of held keys as the focus leaves. Console `pointer` in the lobby; `click` says
    whether the view took the press.
  - **Proof:** 12 tests in `LobbyKeysOnReleaseTests`. 29 mutations, each predicted, the last of four passes 28 of 29 as
    predicted: 25 red, and 4 green by prediction - three reached only by a running game, which the game runs show, and
    one equivalent. Two passes found my tests at fault (a static island index leaking
    between tests; a hollow check that a cost refused first), both fixed. **One miss:** the record tested before
    Instant Action also turned Q41's `EscapeDuringTheFlightGivesThePanelBackAndOpensNoMenu` red. A 22-agent review
    found 12 real faults, all fixed or filed. 1048 tests with the game, 465 ran and 583 skipped without, 123 warnings.
  - **Confirmed in the game**, `~/.cache/tpw-harnesses/q42confirm.py`, a real player made at the slots, real keys and
    a real left button through XTEST, a control on `main` (`q42-control/`) and the fix (`q42-fix2/`). On `main`,
    Right held 1.55 s made **25** moves while held (predicted 23 to 35 from the hold) and none at the release; Enter did
    nothing; Escape opened the menu on the press; the view press did nothing. On the fix every prediction held:
    Right held, no move, then one at the release to Wonder Land, photographed both ways; Enter held did nothing, let go
    entered Lost Kingdom; Escape held cancelled nothing, let go cancelled; the view's press entered on the press, and
    one inside the L did nothing; the tick's own Enter entered nothing; Enter before the advisor's cue entered on the
    live key; the keypad's Enter did nothing; Wonder Land refused, `1 keys, and it costs 3`; the park loaded. The
    first fix run missed at Z (a key held through a loss of focus): two let-go lines where I predicted none. The
    instrument (`q42z.py`, logged focus changes) showed SDL's key-up for the lost focus arriving with the focus's return
    in one frame, so a frame the focus left in is now dropped whole. Then 4 of 4 held, fix and control. `save/`
    unchanged in every run.
  - **Not confirmed on screen:** the name box's order of two releases in one frame, and the record test (tested only).
    **Found:** Q64 (Escape over the player slots), Q65 (the system table's release keys), Q66 (disabled buttons).
  The item as written: `IslandPanel.Update` moved on the press, a held key's repeats included, and nothing took Enter;
  `FrontEnd.MenuKey` took Escape on the press. Confirm: hold Right, one island per release; Enter on an affordable
  island starts the fly-in; log lines and a screenshot.

- [x] **Q44. A left park stays in memory through the lobby.** Done 2026-09-24, `alexah/132-a-left-park-is-let-go`.
  `ParkRides` lets go of `Current` as it is deleted, and `Level.ForgetRunningPark`, the last of `Unload`, lets go of
  `ParkState.Current` and `ParkStaffPool.Current`. That is the original's order: its park teardown destroys every
  thing before it frees the world, whose first member is the hiring pool (`docs/exe/park-engine.md`, "Leaving a
  park with something in the hand", read as disassembly).
  - **The sweep** (10 agents: five investigations, each put to a refuter) found every reader of the three null-safe
    and no other root. `ParkRides.Current` cannot go in `ForgetPark`: an open ride window commits its sliders
    through it as the interface closes, which is after `ForgetPark` and before the entities.
  - **The instrument**: `parks` now asks after each park's hiring pool too, `; its staff pool collected` or
    `alive, held by` the level, the rides' level or `ParkStaffPool.Current`.
  - **Confirmed in the game**, jungle with a worker in the hand, lobby, jungle, lobby. In the lobby after the first:
    - control (the three clears taken out): `parks seen 1 alive 1 | #1 jungle alive, held by state rides; its staff
      pool alive, held by rides pool`;
    - fix: `parks seen 1 alive 0 | #1 jungle collected; its staff pool collected`, and after the second park both
      collected.

    The worker was put back on leaving, the second park ran the first's 15 rides and 16 scripts, every prediction
    held in every run, the last on the committed build, and `save/` was unchanged. Photographed: both parks and the
    lobby after each.
  - **The tests** are `ParkForgetTests`, weak references after a forced collection. Taking out the rides' clear, its
    guard, either of the other two, or all three, turns red exactly the tests predicted; the call from `Unload` is
    reached only by the game, as `ForgetPark`'s is.

  The item as written: found by Q10's sweep (8 agents: four
  investigations, each put to a refuter) and measured with the console's `parks`. In the lobby after the jungle,
  `#1 jungle alive, held by state rides`:
  - `ParkState.Current` holds the save directly. Its setter is private and nothing clears it; `ParkState.cs`
    says so itself.
  - `ParkRides.Current` is never cleared either: `ParkRides` has no `OnDelete`, unlike every other park
    entity's `Current`. It reaches the save through `Entity.Level`, and that keeps the whole of the old
    level alive: its HUD, its catalogue and its `ParkObjects`.

  Both are replaced when the next park is built, so this costs memory through the lobby, not a wrong answer
  in the next park. The original frees its world on leaving (`FUN_00409180`, `docs/exe/park-engine.md`,
  "Walking on the ground"). `ParkStaffPool.Current` is never cleared either, but it reaches no save. Clear both
  in `Level.ForgetPark`, or on the entity's delete, and check every reader of either for a null in the lobby.
  Confirm: `parks` in the lobby after a park reads `#1 jungle collected`.

- [x] **Q45. The VM charges `CRIT_LOCK` against the budget; the original does not.** Done 2026-09-24,
  `alexah/134-crit-lock-is-free`. `RideScript.Step` now charges after `Execute`, by the critical flag as it then stands
  (`FUN_005516b0`, `0x00551724`-`0x00551730`, re-read with both lock handlers): the lock is free, and a lock taken on
  the last unit runs its section in that turn. `critical N` counts as before, so the Q11 figures and tests stand. The
  slice floored to 1 now says so at the site. `rides` gains `lastunit K ran M` per thing and a `lastunit` total.
  - **Tests:** `ACriticalSectionDoesNotOutliveItsTurn` at 4; new `ALockTakenOnTheLastUnitRunsItsSectionInThatTurn`
    (slice 2: position 5, `lastunit 1 ran 3`; slice 3 counts no last unit). 1052 with the game. `q45mutate.py`: the
    charge put back fails both, `== 1` as `>= 1` and the unmarked turn fail the new one, no flag reset fails the old.
  - **The walk, rebuilt** (not kept by Q11): 68 of 150, 18 of 36 in Lost Kingdom, three walkers agreeing lock by
    lock. But the world is frozen inside a turn, and then 15 of the 18 cannot happen; none of the Easymode park's six
    is in the 68 at all (`docs/exe/park.md`, "Corpus shape").
  - **Confirmed in the game, a control with the charge put back beside it:** the jungle paused on load, an Aztec
    Mayhem bought at (57,23) (tvsim @32, one of the 18), then 60 s. Both read `critical longest 5 cap 10000 reached 0
    lastunit 0`, every thing `lastunit 0 ran 0`, the Simulator `critical 3`, as predicted; photographed running.
    Missed: at the load pause I predicted `critical longest 0` (Q11's reading) and read 3, the pause landing after the
    first locks; and 18 scripts, not 17 (the Simulator spawns `torches.rse`). `save/` unchanged in both.
  - **NOT met: a section run whole on the last unit in the jungle.** Nothing the stock park runs can arrive there. The
    one route in this interpreter is the Hot Pot (`bumper` @92, research-gated): 8 riders with its capacity cut to 4
    during the one-second ride, or 7 cut to 1, and it exists only because `BUMP` is unbuilt. Not driven.
  - **Found:** Q83 (the VM's stack errors and `HUSH`'s result register) and Q84 (a stale `Wait` summary), from the
    handler sweep; `park.md`'s script counts for three animation opcodes corrected (250, 114, 75, measured twice).
- [x] **Q47. Two more hollow tests.** Done 2026-09-24, `alexah/135-two-more-hollow-tests`. Both now reach the wiring
  through the park, and a six-agent review (three worktree hunters, each put to a refuter) found what else survived;
  of the survivors it found that are not equivalent, all but four (named under Proof) are now red:
  - `ParkTickTests.EverySweepStampsEverybodyWhereTheyStoodAsItBegan`: the shipped park, scripts wired, balance read,
    40,000 frames with a 0.55 s hitch every 2,000th. Every frame, every guest's and member of staff's previous position
    is where they stood as the sweep began, and unchanged between sweeps; a put-down at an exit is stamped where it
    put them; one guest and one worker have their route taken away once. Unseeded, so counts vary: 2,706 sweeps,
    660-734 guest stops, 218-236 staff stops, 19-21 put-downs, 175-187 stamps inside a hitch. `ParkTickTests` now
    mounts the global file system, without which `TickingTheParkLetsARideCallSomebodyAboard` failed 8 runs in 10 alone.
  - `ParkScreamChainTests`: `AParkMakesEachChildWhenItsTimeHasPassedAndNoneWhileHeld` (jungle, and a theme with no
    music) drives `ParkAudio` with `Audio.Ready` stood in, two rides' chains, each child on the first frame past its
    due time, none while held, each first wait from the call. `EachChildIsAPlacedOneShotOfItsVariationAtTheChainsLevel`:
    place, level 90 then 20, the variation's own samples, the child before not ending, stop, park end, band nought, a
    second start, the census. `AChildIsDueOnlyOnceItsTimeHasPassed` pins the strict `>`. Read by them: `ParkAudio.HeldScream`,
    `Voice.Place`, `Voice.Ending`. Also `ParkHandTests` pins the hand's put-down stamp and `ParkGuestPlacementTests`
    the y blend.
  - **Proof:** 43 mutations, each predicted and each red on its named assertion (`docs/VERIFYING.md` rules 124-126,
    new). Nine more are equivalent and named in the commit. Still unpinned, said at each site: the render path's
    fraction, a child's first balance, the console's pause, and the pump's order against the hold. 1057 tests.
  - **Confirmed in the game** (`q47confirm.py`, silent, the jungle paused and stepped): 0 of 10,170 drawn positions off
    `prev + (cur - prev) * alpha` from where each person stood as the sweep began, across 41 sweeps; 45 stops, each
    drawn in one place (predicted at least 2). Photographed inside one sweep, alpha 0.01 and 0.81: the guest standing in
    the gate's mouth is unchanged while the crowd behind moves. The Bouncy Dino's scream: children logged 1354, 2048,
    2008, 2439 ms apart against 1353, 2048, 2008, 2434 predicted; with the menu open 5.28 s none, the child held; one
    3 ms after closing. Missed first: a `voices` read between children listed none (rule 126); re-run, four of four
    read `Effects placed`, none `looped` (the census now says `looped`). `save/` unchanged in both runs.
- [x] **Q48. Three holes in the camcorder's sweep: the decode.** Done 2026-09-24,
  `alexah/136-decode-the-camcorder-sweep`. Decode only; the build is Q48b. `park-engine.md`, "Walking on the ground is
  swept against the cell edges", "Entering and leaving first person" and "Where OpenTPW's camcorder differs": read by
  hand, then put to five refuters and three judges.
  - **The original has none of the three.** (1) After an asked pass, the axis not asked is put back into its old cell
    if its cell changed (`0x0042c197`, `0x0042c389`), and an exact tie divides the X step by 1.01 so Y is asked first
    (`0x0042bff8`). (2) The whole step puts back any axis whose cell changed, open or shut (`0x0042c460`). (3) Nothing
    clamps the position and no cell is refused for lying off the map (`FUN_004d8750` takes bytes and guards only 0 and
    127, by equality), but the viewer never gets there: 'C' only installs mode 9, and a left click stands the viewer
    on the picked ground point, on a cell of type 0, 1, 3, 9 or 30 inside the 96 by 85 heightfield (`FUN_0046d0d0`,
    `FUN_0042ae70`). The bound on walking is soft, on the velocity, at 960 by 850. Leaving puts the saved point of
    interest and yaw back.
  - **Corrected:** the page said 0.001 is how far inside a refused side the viewer is parked. It parks at `cell * 10`
    going negative; 0.001 is the nudge after an open crossing. **Open:** which x87 precision the sweep runs at (53-bit
    from the CRT, 24-bit after one failed frame). It moves only the margin.
  - **Reproduced in the game with nothing changed** (`q48repro.py`, `q48repro2.py`, `q48repro3.py`; silent, jungle),
    each predicted from a float32 copy of `Slide` and photographed. (1) From (505,225) at 7pi/4, 11 frames:
    `at=(51,23) type=4`, the Belly Bounce's footprint, with only (50,22) east asked. (2) From (515,229.33333) facing
    +y, one frame: `at=(51,23) type=4`, through the shut south side of the queue cell (51,22); 30 more frames reach
    (51,25), inside the ride's mesh on screen. From (515,225), 60 frames stop in (51,22). (3) Entered through `Enter`
    at (1300,245): held at (1280,245), cell 128, for 60 frames. At (1100,245), type 7: held. A census of the save: all
    8,224 cells beyond 96 by 85 are type 7, which is solid. `save/` unchanged in all three runs.
  - **Two misses, both mine.** A control at (1270,245) did not move where I predicted a free walk: type 7 is solid, and
    my model knew only the footprint. A first hole-(2) case walked through (52,22)'s south side, which is open (a queue
    into its own entrance), so it proved nothing; it was redone against a shut side.
  - **No test was added**: nothing was built, so there was no fix to put back. Q48b's tests are the build's.
  - **Found:** Q48b.
- [x] **Q48b. Three holes in the camcorder's sweep: the build.** Done 2026-09-24, `alexah/137-the-sweep-puts-back`.
  `ParkCamcorderCameraMode.Slide` now runs the original's pass whole (`park-engine.md`, "Walking on the ground is swept
  against the cell edges"): the tie broken by 1.01 (step 3), the axis not asked put back (6), the whole step put back
  (7), and with them the two smaller differences, the reach from `modf` of `position * 0.1f` and a refusal going
  negative parked at `cell * 10`. The arithmetic is 53-bit, `double` between the original's stores and `float` at
  each. `Step`'s clamp and the off-map refusal are said as ours at the site; the entry stays Q25. The `camcorder`
  census's `stand=` now prints three decimals, which is what shows a tie.
  - **Why the smaller two as well.** A probe (`q48bprobe.py`: 4,000 two-second walks on the jungle's real edge test)
    put 4 of 480,000 frames in a different cell under the old reach and parking with the three rules added than
    under the original's pass, each a boundary crossed a frame early or late. And with the original's reach, Q48's
    first hole does not reach step 6 at all, so each rule needed new walks of the original's own arithmetic to be
    seen: `q48bsearch.py` found p6, which goes into the Belly Bounce only without step 6, and p7, which goes into the
    ride at (58,16) before this build and without step 7.
  - **Proof by model.** A copy of the original's pass (`q48bslide.py`) equals `Slide` bit for bit on 200,000 inputs
    (random, aimed at corners, ending on boundaries, exact ties; three edge tests). The same inputs differ in 45,900
    at 24 bits, 16,352 without the tie-break, 11,955 without step 6 and 1,026 without step 7.
  - **Tests.** Seven new in `ParkCamcorderWalkTests`, each red with its rule put back: the tie (also red with the
    tie only below a reach of 1), step 6, step 7, the park at `cell * 10`, the `modf` reach, the pass cap (a NaN
    step), and the nudge (red in two existing tests); the four jungle walks, red without step 6 or 7. `main`'s old
    `Slide` fails six of the seven. Equivalent, named: the cell by `floor( x / 10 )`, the same on the map; the far
    park in float, under an ulp. 1064 tests.
  - **Confirmed in the game** (`q48bconfirm.py`, silent, jungle): 18 walks, each photographed before and after, on
    three builds, all predicted first. The old sweep, 18 of 18 as the old model said. This build, 18 of 18 to the
    thousandth. This build with the three rules out, 9 of 9 on the walks that need them (p6 into (51,23), p7 into
    (58,16), Q48's second into (51,25), the tie at 585.667). Q48's repros: (1) 11 frames from (505,225) now read
    `stand=(510.185,229.999) at=(51,22) type=3`, and 60 end in the entrance (52,23), type 9, where the old went on to
    (53,25); (2) one frame from (515,229.33333) reads 229.999 in (51,22), and 30 more stay there, where the old walked
    on to (51,25); the controls as before; entered off the park, held at 1280 and at (1100,245) as before (Q25). p7
    stops in its entrance (58,15) at 159.999; the tie from (585,585) reads `stand=(585.660,585.667)`. **On screen**
    the 60-frame hole 1, the 31-frame hole 2 and p7's 40 frames differ: before, the viewer stands inside a ride (the
    Belly Bounce's pink mesh cut open at (51,25)); after, at its queue or entrance. The rest move under 0.2 units and
    are the census's alone. `save/` unchanged in all four runs.
  - **Read-only review** (a workflow of 25 agents: five lenses - the X pass, the Y pass, reach with tie and whole
    step, the loop, rounding - each claim put to two skeptics). Every lens read the port equal to the disassembly
    instruction for instruction on the map with a finite step. Upheld, all at the margins and all said at the site
    and in `park-engine.md`: X's lower dead-band edge is tested on `dt * velX` before it is stored; the x87 reads a
    NaN as nought, so the original ends on one (I had written that it never would); a put-back reads a cell below 0
    as unsigned; `__ftol` keeps the low word past 2e10.
  - **Missed first, mine.** The harness looked for `stand=` and `at=` as one string, and the census puts `cell=`
    between them: the first run of this build read NO on 18 lines that all matched. Fixed and re-run.
- [x] **Q50. Every other way out of a queue costs `MediumHappinessChange` too: decoded, and a queue measured shorter
  puts out whoever stands past its end.** Done 2026-09-24, `alexah/138-a-shortened-queue-puts-them-out`. Decoded
  first (five decoders, each report put to a refuter; `ride-operation.md`, "Every way out of a queue"): OpenTPW built
  none of the six other callers of `FUN_005012f0` and counted none. One could be built and seen now, `FUN_00501390`;
  the other five are split out as Q50b-Q50f and each is counted where the program reaches it.
  - **Built:** `ParkState.RemeasureQueue`, `FUN_004de1f0` to the end of its walk, at every cell edit that measures a
    queue again - path over a queue cell, `delqueue`, the queue tool's runs and edits, the placer - but not the sale's
    drain (Q50f). `ParkPeople.QueueRemeasured` walks the queue head first, skipping the nominee; a guest at or past
    cells × 4 (unsigned, so -1 too) and not `EnteringRide` goes through `FUN_004ddd20`, now whole
    (`ParkRideOperation.LeaveQueue` lets go of the `VAR_LETMEON` slot), and `FUN_005012f0`: −15, Deciding, the kids'
    `0x80` for an id divisible by eight. `PositionInQueue` gives up at a guest no longer queueing, as `FUN_004ddf50`
    does. Counted: thought `0xd`, `FUN_004de1f0`'s reopen of a closed ride, the park door's per-ride close, and the
    five other ways out. `StepUpTheQueue`'s drift is the original's 32-bit unsigned compare. `peeps` prints each
    queuer's `place`; `happy <n>` sets every guest's happiness, an instrument as `thirst` is.
  - **Proof:** seven new tests (`ParkQueueRemeasureTests`, and one in `ParkRideExitTests`); 13 mutations, each
    predicted and each red, the whole bug back turning five red. A read-only review (21 agents, three lenses, each
    finding put to a skeptic) upheld ten of eighteen, six faults, all fixed: the reopen tail was claimed as built; the closed
    ride's count at `Invite`'s bail ignored `FUN_004e0450`'s forcing arm; two "dead by content" labels were gaps; the
    track editor's close was missing; two "gate on choosing" sentences were stale; the drift. 1071 tests
    with the game; 475 ran and 596 skipped without; 123 warnings.
  - **Confirmed in the game** (`q50confirm.py`, silent, jungle): `load 30`, paused at 14 queuers for the Belly Bounce,
    every guest set to happiness 50, then `path 51 22`. All 14 predictions held: places 0-3 unchanged at 50; the ten at
    4-13 Deciding, dest 0, 35, not moved; ten `put out` lines; `put off` (`bootout.mp2`) for 64 and 72 at their feet;
    the thought counted for 66 and 72. Photographed before, just after and eight seconds on, as they walk off, six to
    the Jungle Spray. The control on `main`, staged the same way at six queuers (no `happy` there, so all at 0): the same
    cut left all six queueing eight seconds on, 6 of 6 predicted, with no `put out` line, sound or count. `save/`
    unchanged in every run.
  - **Missed first, mine.** The first run's queuers were all arrivals at happiness 0 (Q85), so its 11 of 11 showed
    the dock only as 0 to 0; the second waited 25 minutes for one with happiness to lose and found none. I read that
    first as everyone draining to 0 in two minutes; the census says the save's own guests kept theirs and went home.
  - **Not confirmed on screen:** the slot let go (nothing in this park names a guest past the four), the nominee's
    and `EnteringRide`'s exemptions, and the walk giving up at a stale link - tested only. The four left queued stand
    on the cut-off cells, since the walk to a place is unbuilt (Q50e). **Found:** Q50b-Q50f, Q85-Q88.
- [x] **Q50b. A closed ride turns its queue away, one head a turn; the park's door settled.** Done 2026-09-24,
  `alexah/139-a-closed-ride-turns-its-queue-away`. Split from Q50. Decoded first (five decoders, each report put to a
  refuter; `ride-operation.md`, "The closed ride"): `FUN_00519ef0`'s first argument is the OPEN flag - non-zero opens
  (`0x00519f76`), nought closes (`0x0051a091`), and `b_door` passes `down != 1` - so **down is shut**, and OpenTPW
  drew and read the switch the wrong way round. Its close arm runs `FUN_004df300` on every object a guest may be
  offered; its open arm `FUN_004df390` behind the guard `FUN_004df290`.
  - **Built:** the door's sense (`ParkEntryPriceScreen`); both arms (`ParkState.SetParkClosed` → `ParkPeople.DoorMoved`),
    each only on a change; `ParkRideOperation.Close`, `Open`, `MayOpen` and `BackOfQueueConnected` (`FUN_004de4a0`,
    both arms); a closed ride's turn runs `FUN_004e0450` in `Invite`'s place (`ParkPeople.CompleteOrTurnAway`): an
    `EnteringRide` head the slot no longer names is forced on, any other head put out, −15, the kids' sound for an id
    divisible by eight; the same from the states-1/2/4 turn; the tail of `FUN_004de1f0` (`ReopenAfterRemeasure`:
    reopen, `mAssignedStaffMember` forgotten); `mRequestedService` read at file 1078; `Info.HasQueue` pinned as
    descriptor `+0x40` from the compiled schema (`0x00744e3c`) and set on a bought thing (`ParkBuilding.FlagsFor`);
    the ride window's door follows `mCanLoad`; `objects` prints `state` and `canload`. Counted: the gate's command,
    the advisor's `0x80`/`0x81`, the model changes, the coaster's track record, the constructor's close, the ride
    window's status and greying, the all-items row colour (Q89-Q93).
  - **Proof:** 13 new tests (`ParkClosedRideTests`); 19 mutations, each predicted: 16 exact, M4 turned 5 red of the 9
    I named (four of them do not go through the door), M16 two more than named, M17 (the tail's `mCanLoad` test)
    unobservable by construction. A read-only review (15 agents, three lenses, each finding put to a skeptic) upheld
    twelve, all fixed: **a bought or moved queued ride could never open again after the door** (no queue-path bit, so
    the guard took the entrance arm onto its own queue cell) - pinned and set, M19 red; the ride window's shut state
    uncounted; five false comments (the gate's "2 shuts", the bail "cannot fire"); two doc rows. 1083 tests with the
    game; 475 ran and 608 skipped without; 123 warnings.
  - **Confirmed in the game** (`q50bconfirm.py`, silent, jungle): six queuers for the Belly Bounce, `happy 50`, the
    entry-price screen's real `b_door` pressed. The switch showed the green lamp up for the open park and the red lamp
    down once pressed; six `Closing...` lines, the six visitable objects `canload 0`, gate shut; then one head out a
    sweep for seven sweeps, 40, 71, 72, 70, 69, 63, and 60 who walked up after the close, each Deciding at 35;
    `put off` (`bootout.mp2`) for 40 and 72; the Belly Bounce's window showed its door down. Pressed again: six
    `opened`, all `canload 1`, gate open, the green lamp. The control on `main`: the red lamp for the open park and
    the green once shut, no ride closed, the queue of eight standing four sweeps on. `save/` unchanged in both.
  - **Missed first, mine.** Every prediction in the game held but one guest's: 40, one of the save's own, went home
    at 35 where I said 10, read so at six sweeps; its `exit` read -27, so its day had run out while the queue held it,
    and the day's end is asked before `Decide`. The unit test's first final check made the same mistake the other way.
  - **Not confirmed on screen:** a reopen by an edit of the queue, a head forced on, the guard's refusals and a bought
    ride's bit - tested only. **Found:** Q89-Q94, among them that the console's `path` lays what the path tool
    refuses, which Q50's own run relied on (Q94).
- [x] **Q50c. The door's price opinion.** Done 2026-09-24, `alexah/140-the-doors-price-opinion`. Split from Q50 ("At
  the door"). The decode was put to three read-only Ghidra checks, each to a skeptic, and all held
  (`ride-operation.md`, "At the door"): `FUN_004fde50` is the guest's, with the object pushed; three divisions of a
  running product are unsigned, the rest signed; the compiled `.sam` schema puts `RipOffOK` at `+0x16c`, anchored at
  both ends; the control record holding its copy is saved raw (`mObjectControls`), and all 50 in Lost Kingdom's save
  equal their items'. **`+0x4ac` is a copy of `Info.WhichUIType`** (`0x004134f5`), so its 1 is a shop: four doc rows
  said ride, corrected.
  - **Built:** `PeepPriceOpinion` (the worth and the verdict, truncation for truncation); the walk-away in the
    `BeingAdmitted` arm (`PeepBehaviour.WalkAwayFromTheDoor`: −15, the ride's side through a new `walkAway` delegate,
    `ParkRideOperation.Forget` = `FUN_004e0ac0` then `LeaveQueue`, then `DismissFromTheQueue` −15, the kids' sound);
    `ItemDescriptionFile` reads `RipOffOK`, `SpecialIngredient`, `AppearanceEffect`; the admission's own log line.
    Counted: thought 6, `FUN_004e1670`'s two counters, the analyser samples. Replaces `DOOR_PRICE_OPINION`. Console:
    `cash <n>`, and `thirst [n]` takes a level.
  - **Proof:** 12 new tests (`PeepPriceOpinionTests`); five mutations (no opinion, `RipOffOK` dropped, the ride not
    told, cash compared signed, `ParkPeople`'s own `walkAway` emptied), each turned a named test red. The last was
    green at first: an open shop's own turn drops a nominee not boarding and a head not queueing a moment later, so
    the test now puts the shop in state 3, whose turn does nothing. A read-only review (three lenses, each finding put
    to a skeptic) upheld nine, all fixed: a miscount of the signed divisions, the stale not-read list, `SetCash`'s
    "less the gate fee" (nothing takes it), the `thirst` reply, that test, a test summary, `LeaveQueue`'s remark, and
    **the charge's comments calling a
    missing bank deposit the original's** (Q96, counted as `CHARGE_BANK_DEPOSIT`), and **the object's saved prize and
    chance** read from the item unsaid (Q97; two doc rows said `+0x190` is not saved). 1095 tests with the game, none
    skipped; 475 ran and 620 skipped without; 123 warnings.
  - **Confirmed in the game** (`q50cconfirm.py`, silent, jungle): every guest thirsty and carrying 10, the Drinks
    Shop's price 30; once one queued, happiness 50 and thirst 90. Guests 35, 29 and 31 each logged "Object 16 is too
    expensive" at the door and read Deciding, dest 0, happiness 20, cash 10; each counter +3. The control, the same
    tree with the opinion taken out: 31 and 42 admitted at 50 and 10, then out charged to −20 at 55. `save/` unchanged
    in all three runs. The frame at the door is alike in both (a guest standing there); the census tells them apart,
    and afterwards the fix's guests walk off down the path.
  - **Missed first, mine.** The first run predicted 20 and read 0: `thirst` at 100 drains a point of happiness a
    turn (`Peep.Tick`), so both guests reached the door at 5 and 3, and each dock clamps at nought. The rerun set
    thirst to 90 once they queued.
  - **Not confirmed on screen:** a refusal on worth (none can happen at Lost Kingdom's prices), negative cash passing
    (tested), and the kids' sound at the door (no guest turned away had an id divisible by eight; untested too).
    **Found:** Q95-Q97.
- [x] **Q50d. The `InQueue` turn's own ways out.** Done 2026-09-24, `alexah/141-the-queue-turns-ways-out`. Split from
  Q50 ("The `InQueue` turn"). The decode was put to a read-only workflow first (five claim groups, each to a skeptic;
  all held; `ride-operation.md`, "The `InQueue` turn"): every log on the way is the bare `RET`; an invited guest who is
  not the nominee does nothing that turn; the lost place's "closing and reopening" string closes nothing;
  `FUN_004ddd20` splices by the leaver's own links; `mGameTick` is +1 per thing sweep; the guest constructor gives
  happiness 50.0 (`0x004fb075`).
  - **Built:** `PeepBehaviour.QueueTurn`, the arms in the original's order, each put-out through `PutOutOfTheQueue`
    (the ride's `FUN_004ddd20` through its script, `DismissFromTheQueue`, the kids' sound): the toilet (happiness
    20..80 as a byte, toilet above 80, not a toilet's queue), the lost place (the walk now stops where
    `FUN_004ddf50` stops), the invited guest who is not the nominee, the broken ride's skipped re-take, 5a for a thing
    with no queue path (100) and 5b for a car track. `ParkState.LeaveQueue` splices a leaver it cannot find, so an
    unlinked one empties the head. Console: `toilet [n]`. Counted by name: the no-route board, the dirt gate, 5a on
    a queue path, the coaster's record, the failed re-take, the thoughts, the spot animations, the window's heading
    and boredom; `QUEUE_TURN_DISMISSALS` is gone. **Held by Alexah:** the unhappy arm (`QUEUE_TURN_UNHAPPY`), until
    Q85 - an arrival starts at 0 here and it would put every arrival out; both game runs counted it 134 and 353
    times before staging.
  - **Proof:** 13 new tests (`ParkQueueTurnTests`), two in `ParkQueueJoinTests` (one replacing the test that pinned
    the refusal), and `ParkBoardingTests`' helper now stands its guest in the queue it names. 16 mutations, each
    predicted, all red as named: the whole bug back turns 10 red. A read-only review (21 agents, three lenses, each
    finding put to a skeptic) upheld ten distinct findings, all in comments and docs, all fixed (below). 1109 tests
    with the game, none skipped; 476 ran and 633 skipped without; 123 warnings.
  - **Confirmed in the game** (`q50dconfirm.py`, silent, jungle): 15 guests queueing for the Belly Bounce, paused,
    `happy 50`, `thirst 50`, `toilet 80`; the drift sweep predicted from the clock (game tick 2816), where only an id
    divisible by four drifts. Guests 64 and 72, `InQueue`, logged "needs the toilet" and "put out of thing 13's queue
    by their own turn" on that sweep: Deciding, dest 0, 50 to 35, toilet 81, `bootout.mp2` for both; the queue closed
    up behind them; the other twelve stayed at 80 and 50; `QUEUE_TURN_THOUGHT_4` +2. Guest 68, stepping up at the
    drift, went out on its first queue turn after: 14 of 15 predicted (68 missed, below). A second run, 8 of 8: guest
    72 at the queue's head went out on the predicted sweep (2944), 66 became head and was called forward within 12
    s, and 72 walked off the back cell to (47.4,24.1); photographed at 0, 4, 8 and 12 s, the last with the queuers
    walking the path below the queue. The control, the same tree with the toilet's put-out taken out: guest 44
    passed 80 on the predicted sweep (3328) and stayed, 8 of 8. `save/` unchanged in all three runs.
  - **Missed first, mine.** The park-driven walk test was hollow: it ran until the guest left, and the guest who
    stopped queueing chose the ride again and cut the chain anyway; it now reads one sweep. In the game I predicted
    toilet 80 for guest 68 because it was stepping up; the drift does not ask the state. For the control I predicted
    no thought and no log line; only the put-out was taken out. The review's ten: the thing tick was said to start
    at nought per park (`GameClock.Ticks` is not reset); the stand point was said to be undecoded (it is on the entry
    cell; the no-route board is counted for `FUN_004fa5f0`'s retry stamp instead); "no item runs on a car track"
    (Dino Karts does, so the gate is now tested); the drift note had the wrap backwards; a stale `LeaveQueue`
    sentence, the staff clock's note, the toilet log's wording, a test summary, and `addresses.md`.
  - **Not confirmed on screen:** the lost place, the invited guest who is not the nominee, the broken ride and the
    car track - tested only. The frames cannot tell one queuer from another (every `InQueue` guest stands on the
    queue's back cell, Q50e); the census and the log do. **Found:** Q98-Q101.
- [x] **Q50e. The walk to a place in a queue: the decode.** Done 2026-09-24,
  `alexah/142-decode-the-walk-to-a-queue-place`. Decode only; the build is Q50g. `ride-operation.md`, "Walking to a
  new place in the queue", rewritten whole: four decoders (the place to a point, the re-take and its route, the three
  callers, the aim), each report put to a skeptic reading the disassembly - 144 claims and doc corrections, 103
  upheld, 41 amended, none refuted.
  - **Found.** `FUN_00501160` is FindQueueDestination (its own log): the guest's CURRENT place, not the front, into
    `+0x1f1` before routing; one draw of the engine's generator; `FUN_004fa5f0` to an 8.8 point; state 12, or 0 and
    every caller puts the guest out. With the queue-path bit `FUN_004de840` walks from the FRONT, four places a cell
    at 0, 63, 127 and 191/256 back from the front edge (`__ftol( n × 0.25f × 255 )`), across at `rand % 28 + 114`,
    the fourth turned to the next cell's axis (`ADD AL,0x80`, so 63, not 64); without it `FUN_004dec30` uses the
    back of queue and the ENTRY cell's direction, no bound on the place. `+0x198` is `mStrandedTime`, nought on the
    queue paths but from a save. State 12 never reads the place again. The chooser aims at the back cell's centre
    and runs only on `rand % 3 == 0`; state 10 re-aims when the back has moved.
  - **Corrected:** the `FUN_00501160` row ("the front"), the along axis ("pos, or -1 - pos"), "`FUN_004dec30`... not
    decoded", the four writers of `+0x1f1` (five), `FUN_004ddb90` (it writes `mFirstInQ`), `FUN_004dda40`'s role (its
    callers establish it), the state map (10, 11, 12 and 15), `FUN_00536320` (3 or 9), and `park-engine.md`'s
    "`+0x13c`... appears in no offset table" (`UsageInfo.ExcitementLevel`).
  - **Measured in the game** (`q50econfirm.py`, silent, jungle, this tree's build, twice): `cell` read the Belly
    Bounce's queue as the decode needs it, as predicted - `mNeighbours 0x50 0x44 0x44 0x44`, `mDirection 0x10 0x04
    0x04 0x04`, (48,22) path - and `why` aimed every chooser of it at the entry (52,23), where the original aims at
    (49,22). Paused at seven InQueue queuers, places 0 to 6 all stood at (49.626, 22.507), 7 of 7 on the back cell
    both times as predicted, where the decode stands them in a line from (52.5, 22.996) back to (51.5, 22.5); four
    seconds on, 10 of 10 and 6 of 6; `QUEUE_PLACE_WALK` 11 times. Photographed close: the seven draw as one figure on
    the back cell, while thing 73, `GoingToRide` at (50.339, 22.404), walks the queue's length to the entrance, to be
    sent back to the back cell - a guest the original never walks up its queue. The five without the bit, read with
    `cell`: the Jungle Spray's and the Drinks Shop's entrances `mDirection 0x10`, the toilets' `0x40` (the table in
    the page). `save/` unchanged in both runs.
  - **Missed first, mine.** I predicted the Jungle Spray's and Drinks Shop's entrances face `0x01`, as the Belly
    Bounce's does; both read `0x10`, so their heads stand on the entrance's edge (the toilets' `0x40`, predicted from
    those two, held). The harness reused the X display's name as a loop variable and lost the first run's second
    photograph; fixed, and run again closer.
  - **Not confirmed on screen:** anything of the original's own - its guests are not run here; the decode is the
    executable's, and the run measures only ours and the park's data. Nothing is built, so there is no test and no
    mutation. **Found:** Q50g, Q102-Q104.
- [x] **Q50g. The walk to a place in a queue: the build.** Done 2026-09-24, `alexah/143-walk-to-a-place-in-a-queue`.
  Split from Q50e, whose decode it builds (`ride-operation.md`, "Walking to a new place in the queue"); `FUN_00501160`,
  `FUN_004ffbc0`, `FUN_004de840`, `FUN_004dec30`, `FUN_005006b0` and `FUN_0050fd40` were read again first, and it held.
  - **Built:** `ParkQueuePlace` is `FUN_004de7e0` and its two arms: with the queue-path bit, from the front a cell per
    four places, along 0, 63, 127 and 191, the fourth turning to the next cell's direction (`ADD AL,0x80`) or, at the
    back, away from the first path it joins; without it, every place in the back cell, facing the entry cell's
    direction, unbounded. `PeepBehaviour.FindQueueDestination` is `FUN_00501160`: the walked place (-1 answers false
    with nothing written), its byte into `QueuePos` first, one jitter draw, a route to the exact point, state 12. Its
    callers: the join, after state 10's new arrival test on the back cell and its re-aim (no back or no route:
    deciding, `MajorDest` kept); the `InQueue` re-take, whose mood still runs on the same turn; and the refused door,
    back to place 0. Each failure puts the guest out through `PutOutOfTheQueue` with the original's line.
    `ChooseSomewhereToGo` aims at the back cell's centre. `QUEUE_PLACE_WALK` is gone; a direction neither switch knows
    is counted, `QUEUE_PLACE_DODGY_DIRECTION`, and stands at the centre. `peeps` shows `recorded` and `aim`, `why` the
    aim. The two remarks are corrected: `ParkWorld`'s on `FUN_004dec30`, and `ParkRideChooser`'s, which now says its
    deviation (Q105).
  - **Proof:** 18 new tests (`ParkQueuePlaceTests`); four re-take tests now stand their guest on the queue. 22
    mutations, each predicted: the whole bug back turns 16 red, and 19 more went red as named. `sub-shift-7` turned
    one more red than predicted. `no-cell-zero-guard` stays green by design: the route to cell 0's (127, -1) fails
    anyway, as the original's to (127, 255) does. `clamp-to-back-cell`, a place past the cells stood on the back cell,
    turns the cell-0 test red instead. A read-only review (8 agents, four lenses, each put to a skeptic) upheld 13
    findings and amended 2, all in comments and tests, all fixed; one refuted. The worst: the one test reaching the
    back cell's turn could not tell the path search from its fallback, since both give `0x04` at (49,22); a map edit
    now separates them. 1127 tests with the game, none skipped; 476 ran and 651 skipped without; 123 warnings.
  - **Confirmed in the game** (`q50gconfirm.py`, silent, jungle, `load 60`): `why` aimed a chooser of the Belly Bounce
    at (49,22), as predicted. Staged at 101 s with 14 InQueue (predicted 13 within 600 s). 14 of 14 were aimed at the
    decode's point for their recorded place, computed by the harness from the decode's table and the cells read, not
    from the C#: along exact, across 114..141. 14 of 14 stood within 0.33 of it (0.089 to 0.241), on all four cells;
    four seconds on, 15 of 15. Photographed: the queue stands in a line along its four cells, where Q50e's shot from
    the same camera drew seven as one figure on (49,22). No "moved", "couldn't get", "rejoin" or `QQQ` line; nothing
    counted; `save/` unchanged. A second run for the virtual arm: the Jungle Spray's guest 82 at place 0 was aimed at
    (52.535, 29.996), the decode's (52 + J, 29 + 255/256), 1 of 1.
  - **Missed first, mine.** The park-run test: predicted more than four Belly Bounce places from the save's 13 guests
    in 600 turns, got 0 to 2; it now asks for more than one. The first `no-dodgy-count` mutation deleted the line an
    `if` governed, so the next statement became its body, and six unrelated tests went red; re-run with an empty body.
    I counted 14 new tests where there were 15. The Spray run: predicted 2 InQueue within 600 s, saw none in 900 s -
    the sideshow admits at once, so the census caught one guest walking to his place, not standing in it.
  - **Not confirmed on screen:** the arrival's re-aim (no back moved in either run), the three failed walks, the
    refused door's walk, a dodgy direction, a place past the cells - tested only. The virtual arm is the census's
    one sample, not photographed. **Found:** Q105.
- [x] **Q50f. What the sale's drain does to its queuers: the decode.** Done 2026-09-24,
  `alexah/144-decode-the-sale-drain`. Decode only; the build is Q50h. `ride-operation.md`, "The sale's drain", new:
  four decoders (the list, the pop, the measure, the sale end to end), each report put to a skeptic reading the
  disassembly, then a critic over all four - 108 claims, 85 upheld, 23 amended, none refuted.
  - **Found.** `FUN_00530120` pushes the cell the entry cell's one link faces, then walks from the entry cell itself,
    pushing each corner, and cuts the last queue cell from its path: the Belly Bounce's list is (52,22), (52,22),
    (49,22). `FUN_0052fe50` never pops the bottom entry but clears it as the far end of the last run (unless, in mode
    3, it is a path); a list of N gives N calls, N-1 clears and N-1 measures. The Belly Bounce's first pop clears all
    four cells under force, unlinking nothing, so the entry cell keeps `0x01`; `FUN_004de040` takes the faced cell
    with no type test and `FUN_004de130` counts it, so **every measure answers one cell, room for four**, never
    nought. The first puts out every queuer from place 4 back but the nominee and raw state 14 (−15, `MajorDest`
    nought); the second changes nothing. At the type-10 message the first four, the nominee and raw state 14 lose 20;
    the rest have lost 15. Only a thing with `HasQueue` drains - in Lost Kingdom the Belly Bounce alone - and a move
    drains as a sale does.
  - **Corrected:** `ride-operation.md`'s "never applies the stack's bottom entry" (it is the last pop's far end),
    "state 14 is never put out" (the raw state: state 8 over a saved 14 goes) and "passes the open guard" (an inlined
    copy). `park-engine.md`: the queue-edit walk (the faced cell first, the walk from the entry), the drain's gate
    (the object's cached `+0x40`) and its debit (gated on the bank's `+0x114`), the Backspace re-arm
    (`FUN_0052f580( mode, 1 )`, not `(1,1)`), the path arm's NOMODIFY return (not under force), the bottom entry's
    path exception (mode 3 only), and the kind-17 relay's answer to `0x1b`.
  - **Measured in the game** (`q50fmeasure.py`, silent, jungle, `main`'s build in a throwaway worktree): `load 60`,
    staged at 91 s with eight queueing for the Belly Bounce and six walking to it, paused, `happy 50`. `cell` read
    the decode's inputs: the entry (52,23) `neighbours 0x01` parent 2996 and the node (52,22) `0x50` parent 2996, so
    the owners match and the list is three entries. The nominee was 46, at place 0. `sell 13`: 14 of 14 predicted
    for this build - the eight queuers 50 to 30, the six walkers 50 to 45, 14 `put off thing 13` lines,
    `SALE_DRAIN_QUEUE_REMEASURE` counted once, "sold for 500, its queue for 225", the four cells type 0 after. The
    decode differs on 4 of the 14: places 4 to 7 (guests 100, 93, 98 and 91) would lose 15, not 20. Photographed
    before (the eight on the Belly Bounce's bridge), just after (bare grass, everyone where they stood) and eight
    steps on (three gone to the Jungle Spray). `save/` unchanged.
  - **Not confirmed on screen:** anything of the original's own - its guests are not run here; the decode is the
    executable's, and the run measures only ours and the park's data. Nothing is built, so there is no test and no
    mutation. **Found:** Q50h.
- [x] **Q50h. The sale's drain puts out whoever stands past its first cell: the build.** Done 2026-09-25,
  `alexah/145-build-the-sale-drain`. Split from Q50f, whose decode it builds (`ride-operation.md`, "The sale's drain");
  the demolisher's order, `FUN_00530120` and its helpers were read again first, and it held.
  - **Built:** `ParkPathBuilding.QueueEnds` is `FUN_00530120`: the faced cell (alone when another owner's), the walk
    from the entry cell turning at corners and setting each queue or entrance cell's counter, its stops (one link, a
    path, anything else), the cut at a path stepping back unless onto the entrance, and the angle arm. `DrainQueue`
    takes the list before the gate on the cached queue length, then pops: `ClearQueueLine` clears each run under force
    (a queue cell refunds while its owner's cell is typed; a bare one is left; a run ending on a path clears nothing),
    then `ParkState.RemeasureQueue`; then the debit. `SALE_DRAIN_QUEUE_REMEASURE` is gone. Counted:
    `QUEUE_DRAIN_ADVISOR_0xCB`, `QUEUE_DRAIN_DEBIT_BANK_GATE`, `QUEUE_DRAIN_DEBIT_DEPRECIATION`,
    `QUEUE_REMEASURE_BACK_CELL_STAMP` (every measure), `QUEUE_DRAIN_CLEARS_ANOTHER_KIND`, `QUEUE_END_WALK_UNBOUNDED`,
    and `QUEUE_REFUND_DEPRECIATION` in the drain as well.
  - **Proof:** 7 new tests; `ASaleLeavesEveryQueuerToTheSale`, which pinned the bug, replaced; the old drain test now
    stamps its ride's footprint, as the placer does, without which the owner rule refunds nothing. 15 mutations, all
    red; the bug back (the measurement thrown away) turns 2 red. A read-only review (9 agents: three reviewers, each
    finding put to a skeptic) upheld 6 findings, refuted none, all fixed: the advisor posts are four, not two (the
    demolisher's mode 3 before the gate, and the last call's re-arm at `0x005300a6`, which the decode had left out); a
    tie in `FUN_00536100` runs along Y (`0x00536140`); the debit's comment claimed a scaling the code does not do; and
    the list test's counter asserts were hollow, the save holding the same values. 1134 tests with the game, none
    skipped; 476 ran and 658 skipped without; 123 warnings.
  - **Corrected:** `ride-operation.md`'s pops (the call that empties the list re-arms too; four `0xcb` posts; the tie)
    and `park-engine.md`'s `FUN_0052fe50` paragraph.
  - **Confirmed in the game** (`q50hmeasure.py`, silent, jungle, `load 60`, the final build in a throwaway worktree):
    staged at 88 s, nine queueing for the Belly Bounce, a rider and eight walking to it, paused, `happy 50`, no
    nominee. Predicted and read, 18 of 18: places 4 to 8 (guests 99, 100, 97, 96, 94) 50 to 35, five "put out of thing
    13's queue at place 4 of 4" lines; places 0 to 3 50 to 30 and the nine others 50 to 45, 13 "put off" lines;
    `QUEUE_DRAIN_ADVISOR_0xCB` 4, `QUEUE_REMEASURE_BACK_CELL_STAMP` 2, both debit counts 1, the old count absent; "sold
    for 500, its queue for 225"; the four cells type 0. Photographed before (nine on the bridge), just after (bare
    grass, everyone where they stood, the balance up 725) and eight steps on (the first four, a walker and guest 94
    gone for the Jungle Spray, four of the drained still on the cleared cells: Q53). `save/` unchanged. An earlier run
    on the pre-review build
    matched 14 of 14.
  - **Not confirmed on screen:** a queue with a corner past its node, whose measure between runs can reopen a closed
    ride, and the other arms (another owner's queue faced, a path faced, no link, an owner's cell bare): tested only,
    nothing in Lost Kingdom reaches them. A tie in a run: built, reached by no list. **Found:** Q106.
- [x] **Q53. A put-off queuer on cleared ground can leave only by going home: the decode.** Done 2026-09-25,
  `alexah/146-decode-the-stranded-guest`. Decode only; the build is Q53b. `ride-operation.md`, "Deciding and
  wandering", new: five decoders (the no-links arm, the linked arm, the stranded bookkeeping, the state-6 turn, the
  ground after a sale), each report put to a skeptic reading the disassembly, then a critic over all five - 173
  claims, 144 upheld, 28 amended, 1 refuted (about the run's output file, not the executable).
  - **Found.** The original does not strand them. `FUN_004f9490` counts the cardinal links of the guest's own cell,
    and a cleared cell has none (ClearCell's tail zeroes the mask and the type), so it takes an arm OpenTPW lacks: path
    cells on seven rays up to three away - the table's case 0 probes the guest's own cell, so no ray runs due south -
    each routed to its centre, a failure moving on, then five random cells within 5 of any type. In Lost Kingdom the
    first hit from every cleared cell is (x + 1, 21), and the guest leaves Wandering on their first turn with
    r % 3 = 1. `SetRandomDest`'s summary calls that fallback staff-only; nothing in the arm reads the person type. The
    stranded stamp (`0x004f9e09`) and thought `0x11` belong to the LINKED walk's dead end alone, and a stranded guest
    can then be routed nowhere until a map edit stamps their block. Also found: a linked wander walks 1 to 5 cells
    with three filters; `Decide` restamps where the original does not and never docks the chooser's −5; the leave
    test is state 6's alone, with `mExitLevel` exactly nought (a four-sweep window), aimed at `CrossingParkSide`
    (47,9) and (48,9), not the bus stops.
  - **Corrected:** `ride-operation.md` (where `mStrandedTime` is set, what zeroes it, the `?` over a teleported rider)
    and `park.md` (what sends a guest home).
  - **Measured in the game** (`q53measure.py`, silent, jungle, `main`'s build in a throwaway worktree,
    `Q50F_STAGE=3`; a first try asking for eight queuers never staged in 900 s): staged at 85 s with four queueing,
    a rider and nine walking to the Belly Bounce, `happy 50`, `sell 13` paused. `cell` read x 46..55, y 19..25: the
    four queue cells type 0 mask `00`, y 21 path along the whole window. Predicted four stranded (no side our wander can
    take) and read four: 52 on (52,22), 100 to 102 on (50,22), all at 30. Predicted each keeps their cell and
    happiness while Deciding and leaves only as GoingToRide or HeadingForExit: held over 15 samples in 90 s. 101 left
    for the Jungle Spray at 6 s; 52, 100 and 102 at 61 and 73 s, when their exit level reached nought. Photographed
    before (four on the bridge), just after (bare grass, the four where they stood) and 90 s on (the grass empty, the
    three walking home up x 47-48). `save/` unchanged.
  - **Missed, mine:** I predicted at least one still Deciding at 90 s; none was. The load's guests arrived together, so
    their days ran out together (52's exit level was 57 at the sale). The harness printed "prediction breaks: 0"
    because it never counted that prediction; the critic found it, and it now does.
  - **Not confirmed on screen:** anything of the original's own - its guests are not run here, so the decode is the
    executable's and the run measures ours. Nothing is built, so there is no test and no mutation. **Found:** Q53b,
    Q107 to Q111.
- [x] **Q53b. A guest on a cell with no links wanders to the nearest path: the build.** Done 2026-09-25,
  `alexah/147-wander-from-a-cell-with-no-links`. Split from Q53, whose decode it builds (`ride-operation.md`,
  "SetRandomDest - `FUN_004f9490`", the no-links arm), checked against the disassembly first.
  - **Built.** `CellEdge.Links` (`FUN_00522810`). `PeepBehaviour.SetRandomDest` takes the call's r % 5 + 1 draw,
    counts the own cell's links and at none calls `WanderFromNowhere`: `NoLinksProbes` (the `0x004f9e40` table, the own
    cell at every k 0, no (0, +k) ray, x wrapping through the sixteen-bit id), a path hit routed to its centre, a
    failed route moving on; then five tries within 5, x drawn before y, any type, an off-map draw using a try. A park
    never loaded is taken as linked. Its summary no longer calls the tries staff-only. Staff reach the count too,
    inside their area or outside it once the patrol roll fails: counted `STAFF_NO_LINKS_WANDER`, filed as Q112.
  - **Tests.** `ParkNoLinksWanderTests`, 15, on a real sale of the Belly Bounce. Twelve mutations each turn at least
    one red; the bug put back (the arm removed) turns ten.
  - **Reviewed.** A read-only workflow (four lenses, each finding put to a skeptic) found the staff reach outside the
    area, Q112's dead end and handyman, the missing per-call draw, four hollow or missing tests and two over-claiming
    comments. All fixed before the game runs.
  - **Confirmed in the game** (`q53b2measure.py`, silent, jungle, `load 60`, `Q50F_STAGE=3`, the commit's build in a
    throwaway worktree, `pause` then `step 15` a sweep at a time). Predicted and read: 5 of 5 put-off guests on cleared
    cells left Deciding within 4 sweeps. 35 and 65 went as Wandering aimed at exactly (53.5, 21.5), one line each
    ("Peep 35: no links at (52,22); probe 14 aims at path (53,21)"), and stood on (53,21) at sweeps 6 and 14. 42, 60
    and 92 went as GoingToRide for the Jungle Spray with no such line. None went home, and no line named another cell.
    Photographed before, just after (the five on bare grass) and eight seconds on (two on row 21, three at the Spray).
    `save/` unchanged in both runs.
  - **Missed, mine.** The first run (`q53bmeasure.py`) predicted all eight would wander. Three did, exactly as
    predicted (42 from the entrance by probe 15 to (54,21), 76 and 101), and five took the chooser first. I had read
    Q53's run as the chooser seldom succeeding, when it seldom ran: every failed wander restamped its gate, and a sale
    leaves that gate open. The original races the same two arms. I also predicted `STAFF_NO_LINKS_WANDER` at 0 before
    the sale: the first run read 23 and the second 0, with no guard or researcher sampled on a cell with no links.
    Where it is reached is not measured (Q112).
  - **Not confirmed on screen:** the five tries, a lone path cell, a failed probe route and the wrap. They are tested
    only, because from a sold Belly Bounce the probes always find row 21.
- [x] **Q56. A right press over a panel arms the quick click.** Done 2026-09-25, `alexah/148-right-press-over-a-panel`.
  Decoded first to settle it (three decoders, each put to a refuter; `park-engine.md`, "Whose a right press is"): a
  right press goes to the control under the pointer and on to no parent, so only one landing on the park's layer 0 arms.
  The game menu and the message box cover the layer, the options and map screens hide it, and so does first person
  (`FUN_004a2ac0( 0 )`). The six management screens and the nine object windows are **not modal** in the original: they
  are built onto the layer, so a right press beside one arms and cancels with the window left open, and one on it does
  not. Also settled: the lobby's game menu is the same full-screen panel (`lobby.md`).
  - **Built.** `WindowStack.TakesRightPress`: a control under the pointer, a modal window, or the root of a park screen
    (`UiWindow.ParkScreen`, on the six screens and the object window) takes it. `WindowStack.RightPointerTaken` records
    it on the press, and `Level.RightPressTaken` adds first person and gives the park every press while F2 hides the HUD.
    `Level.RightButton` arms only a press nobody took. `UiControl.RightPressed` counts a right press on the all-staff,
    visitors and all-items lists (`LIST_ROW_RIGHT_CLICK`). The console's `pointer` names the control under the pointer
    and whose a right press there is.
  - **Tests.** Seven in `ParkHandTests` (23 there): the level's arm, the gadget with a hidden viewfinder, the buy screen,
    all seven park screens, a message box, and two real frames through a `WindowStack` and the level's own `WorldClick`
    (over the gauge and the park, and first person). Eleven mutations, each predicted and each as predicted, all red
    (`q56mutate.py`): the bug put back in `RightButton` turns three, and put back at its call in `WorldClick` two.
  - **Reviewed.** A read-only workflow (four lenses, each finding put to a skeptic): 28 findings, all real. The review
    found first person, the untested wiring (the bug put back at its call left the first four tests green), the untested
    hidden-window guard and six screens, F2, a cleanup order, the uncounted list click, and the doc slips. All
    fixed before the last game runs; the rest filed as Q113-Q117.
  - **Confirmed in the game** (`q56confirm.py`, silent, jungle, a real XTEST pointer move and right button, 1280x720;
    main at `4478e1a` as the control, `q56/control3`, and the fix, `q56/fix3`). Predicted and read with the Belly Bounce
    in the hand (`hand: item 1100`): over the gadget's gauge the fix's `pointer` said `over control 0x1e; a right press
    here is the interface's` and `hand` kept 1100, where main let go; on the park both let go
    (`world click: right click - drop: let go of item 1100`); on the buy screen's bare left frame the fix kept it and
    main let go; below the buy screen both let go and `windows` still listed `ParkBuyScreen`; with the game menu up, on
    an item and off every item, the fix kept it and main let go; in first person the same; on the all-staff list
    (`over control 0x321`) the fix kept it and `unimplemented` read `1x LIST_ROW_RIGHT_CLICK`, and main
    let go with no such row. Photographed at each: the help bar reading the gauge's line, the cursor on the buy frame,
    below it, off the dimmed menu's items, in the viewfinder, on the staff row. `save/` unchanged in every run.
  - **Missed, mine.** My first control run's menu stage opened no menu: a modal screen in front keeps Escape, and the
    console's `menu` is Escape's body. Redone with the buy screen shut by its own button. Both censuses read
    `unimplemented 5` though the fix's holds the new row: `STAFF_NO_LINKS_WANDER` (Q112) came 6 on main and 0 on the
    fix, a random wander, and `CARRY_PREVIEW_MARKERS` counts carries, which main made more of.
  - **Not confirmed on screen:** the F2 case and the message box, the other five management screens and the object
    window (tested only).
- [x] **Q57. The park's Escape acts on the press.** Done 2026-09-25, `alexah/149-park-escape-on-the-release`. The decode
  read again in Ghidra (`scenes.md`, "The park Escape route"): `Park_MouseMessageProc` runs its tables on the key-up
  (`0x00488921`, `FUN_0040c990`) and only latches on the key-down; the menu's handler closes on an Escape let go
  whatever the modifiers (`0x0048bb36`), and so does first person (`FUN_00488a00`); the game and shortcuts Escape rows
  name no modifier, and neither is rebound. Also found: the coaster table's row 0 is Escape too, run only by the
  coaster bar; a park screen in front takes the key and closes on it (Q119); the modifiers are read at each key-up (Q120).
  - **Built.** `WindowStack.EscapeWithoutFocus` is gone: the park, like the lobby, reads its keys through
    `KeysWithoutFocus`, and `ParkFrontEnd.ParkKeys` hands `MenuKey` each Escape release, the front window read again for
    each. `MenuKey` closes the menu and leaves first person whatever the modifiers, then empties the hand or opens the
    menu only with none held (`Input.NoModifierHeld`). `InputButton.Menu` is read by nothing and says so.
  - **Tests.** `ParkEscapeOnReleaseTests`, 8 with its data rows: real key events through `Input.UpdateFrom` and the
    stack, over the real park front end. Thirteen mutations, each predicted and each as predicted (`q57mutate.py`,
    `q57-mutations.txt`): `main`'s own source turns all 8 red; reading the presses, 2.
  - **Reviewed.** A read-only workflow (four lenses, each finding put to a refuter): 15 real, 6 refuted. It found the
    screens' own Escape (Q119), the frame-end modifiers (Q120), a modifier test only Shift and Ctrl covered, the
    untested modal guard, leaked orbit statics, and six stale sentences. All fixed or filed, before the last game run.
  - **Confirmed in the game** (`q57confirm.py`, silent, jungle, 1280x720, real keys through XTEST, Escape held 2.4 s
    with its repeats; `main` at `b178d5e` as the control, `q57/control`, and the fix, `q57/fix`). With the path tool
    in the hand (`hand: ... tool 1`): on `main` the line `Escape: the build tool is put away` came 0.01 s after the
    PRESS and `hand` read tool 0 mid-hold; on the fix, 0 lines while held, tool 1 mid-hold and the tool's square on
    screen, then the one line 0.00 s after the release, tool 0 and the idle cursor. Empty-handed: `main` opened the menu
    mid-hold (photographed, mean 70.1 against 131.7); the fix opened none while held and the menu at the release. Over
    the menu: `main` closed it mid-hold, the fix at the release. Shift+Escape with the tool: nothing, both. Shift+Escape
    over the menu: the fix closed it at the release, `main` never. First person (`state` cam z 5, orbit 49): `main` was
    in orbit mid-hold; the fix held z 5 with the viewfinder on screen and was at z 49 after the release. `save/`
    unchanged in both runs, `unimplemented 3` in both. The committed build (`q57/fix-committed`) held every stage
    again; its census read 4, the fourth `2x STAFF_NO_LINKS_WANDER`, Q112's random wander, which I had not predicted.
  - **Not confirmed on screen:** the message box over the menu, two releases in one frame, and Ctrl, Alt and the right
    Shift (tested only).
  The item as written: found by Q39's decode. The original runs every game-table key on the release (message
  `0x1000b`; the key-down runs nothing, `FUN_0040c900`), and so lets go of the hand, closes the locator and opens the
  menu on the release. `WindowStack` handed Escape to `ParkFrontEnd.MenuKey` on the press, said at the site. The lobby's
  half is Q42. Confirm: hold Escape with something in the hand - nothing until the release.
- [x] **Q59. A right click in first person leaves it.** Done 2026-09-25, `alexah/150-right-click-leaves-first-person`.
  Decoded first to settle it (three decoders, each put to a refuter, then a critic; `hud.md`, "Four ways out of camcorder
  mode" and "A click and a double click"): the layer's `0x10006` is the UI library's **single** click, a press let go of
  under 500 ms that never strayed more than 6 units; a press within 500 ms of the button's last clean release, on any
  control, is a double click's second and makes none, so a double click leaves on its first click. It leaves anywhere
  but the eject button, which drops a right click; the frame is disabled and takes no pointer. Also found: a held right
  button walks in the original's first person (Q121, counted).
  - **Built.** `WindowStack.RightClick`, the base proc's click for the right button with the button's one stamp; a press
    on the view asks `ViewRightClick` what answers it, and the park answers `ParkViewfinder.RightClicked` in first person,
    which leaves with RMB cancel on. Only a press the window system sent clicks (`Input.MouseInfo.RightWentDown`).
  - **Tests.** `ParkFirstPersonRightClickTests`, 14: real frames through a real stack over the real park front end.
    Twenty-one mutations (`q59mutate.py`, `q59-mutations-2.txt`), each predicted: 20 as predicted; the miss was mine,
    taking the click on the press also turns the interface-units test red. `main`'s source turns 11 of the 14 red.
  - **Reviewed.** A read-only workflow (four lenses, each finding put to a refuter): 29 findings, 27 real. It found the
    answer settled at the release (a press made in orbit left first person once C put it up), a held button taken as a
    press after F2, five untested rules and fourteen doc slips; all fixed before the last game run. Filed: Q122 (entering
    first person leaves a park screen open) and Q123 (the click limits on the frame clock).
  - **Confirmed in the game** (`q59confirm.py`, silent, jungle, 1280x720, a real XTEST pointer and right button; `main` at
    `29f71a8` as the control, `q59/control`, and the fix, `q59/fix2`). Orbit cam z 49, first person 5. A quick right
    click in the middle: the fix logged `Right click: out of first person` 0.01 s after the release, z 49, the orbit and
    gadget photographed; `main` stayed at 5 with the viewfinder photographed. On the eject button, with RMB cancel off,
    dragged 40 px, and held 0.8 s: z 5, no line, both. The double click: out after the first release. A click 0.2 s after
    a press held 0.7 s: z 5, photographed; the next 0.8 s later: out. A right press in orbit, C, the release: z 5, no line.
    `FIRST_PERSON_RIGHT_BUTTON_WALK` read 152 where I predicted 40-60 (about 146 fps, and the earlier holds count); the
    second run predicted about 150 and read 151. `save/` unchanged in every run.
  - **Not confirmed on screen:** a stray over the eject button, the 490/500 ms edges, 6 units against 7, a button held
    through F2 (tested only).
  The item as written: Found by Q39's decode. With RMB cancel on, the
  viewfinder layer's handler answers a right double click by leaving first person as Escape does (`0x00488aa1`..
  `0x00488ad6`). Nothing here reads a right click in first person. Confirm: camcorder, a double right click, `camera`
  back to orbit, photographed.
- [x] **Q67. `RootPanel.Instance` is taken out.** Done 2026-09-25, `alexah/151-root-panel-instance-out`. Nothing read it,
  and its constructor kept the first interface ever built, the first lobby's, for the life of the process. Taken out
  rather than labelled: the item offered either, and a label would have kept the pin it names.
  - **The sweep** (8 agents: four read-only investigations, each put to a refuter, all upheld) found no reader in any
    project by name, reflection or string, two construction sites (`Level.SetupHud`, `SetupParkHud`), and no other
    holder of a left level's interface once the next level's first frame runs. Every HUD has four panels, and none
    once `Level.Unload` has run. It found two more statics of the same shape, filed as Q124 and Q125.
  - **The instrument**: the console's `huds`, every interface seen on show held weakly, each `collected` or `alive,
    held by` the level on show or `nothing named`, with its panel count.
  - **Confirmed in the game**, lobby, jungle, lobby, jungle, `huds` at each, every reading predicted first:
    - control (`Instance ??= this` put back): `#1 lobby alive, held by nothing named, 0 panels` in the park and at
      every step after it;
    - fix: `huds seen 2 alive 1 | #1 lobby collected | #2 jungle alive, held by level, 4 panels` in the park, and
      `huds seen 4 alive 1` at the end, the park on show the one alive. `parks` in the lobby after the park still
      reads Q44's `#1 jungle collected`. Photographed: both lobbies and both parks, their interfaces whole. `save/`
      unchanged in every run.
    - **Missed first:** the first fix run read every HUD alive, emptied, held by nothing named. It ran the last
      mutation's binary, a static list of every interface: the source restored and not rebuilt (`VERIFYING.md` rule
      116, now in "Start here"). That mutation built on its own reproduces the reading line for line.
  - **The tests** are `HudForgetTests`: weak references after a forced collection, and every static field of the
    game's declared `RootPanel`. Keeping the first interface, or the last, in such a field fails both; keeping every
    one in a list fails the first only, as its remarks say.

  The item as written: Found by Q44's sweep. `RootPanel`'s constructor sets it to the
  first panel ever built (`Instance ??= this`, `RootPanel.cs`) and nothing in any project reads it, so it pins the
  first lobby's emptied interface for the life of the process. It holds nothing of a park. Label it or take it out,
  as rule 3 says for dead by CODE. Confirm: a grep for readers, and the build.
- [x] **Q68. Guests arrive eight times as often as the original's: the decode.** Done 2026-09-25,
  `alexah/152-decode-the-arrival-clock`. Decode only; the build is Q68b. `park.md`, "Arrivals", and `park-engine.md`,
  "What the 31 ms tick drives": read by hand, then put to a read-only review (a workflow of 16 agents: four groups of
  claims each given to a skeptic, two open questions, and a second skeptic on each claim not upheld whole). Every
  correction it made was re-read before use.
  - **The original's clock is `mGameTick`, one count a thing sweep.** `FUN_00516380` increments it (`0x00516394`) and
    calls the manager on every path (`0x00516695`, `FUN_004d7b20`, `0x004d7b29`), so the timer and the drip both run
    once a sweep, every 248 ms, at most three a frame. The compare is unsigned and strict (`0x004cf3f6`), and the mark
    is reset a sweep after the last guest gets off at the soonest (`0x004cf56b`): the next load is called 602 to 605
    sweeps after that, 149.3 to 150.0 s. The mark is `mTimeSig`, saved with the park (`FUN_004cf050`, the last 18
    bytes of the World block's 76-byte tail); entering loads it and `mGameTick` over `FUN_005156a0`'s zero
    (`FUN_005accf0` at `0x0054f12b`), so Lost Kingdom's 661 and 755 put its first load on sweep 509, 126.2 s in.
  - **Proven on the way: the period is `Arrival.TimeBetweenArrivals`**, which the page had by role only, and the stop
    `FUN_004d8650` reads is `FixedItemInfo.BusStopA/B`. The balance loader hands out four-byte slots from a descriptor
    table (`FUN_00401030`, `0x00402ae0`); replayed over all 283 descriptors from the file on disk, it puts
    `MinPeople`, `TimeBetweenArrivals` and `PointsPerVisitor` on the floor, the period and the divisor, and
    `BusStopAPosX` on `0x007855ac`, three anchors ten arrays apart. Two reviewers derived the same, one closing the
    whole table on the next object (`0x00785828`). `FixedRate` has no reader.
  - **Reproduced in the game with nothing changed** (`q68measure.py`, silent, jungle), predicted first: 600 ticks
    from a drop to the next call, 597 to 607 from the park on show to the first, one guest a load. Read: 600 and 600
    (18.594 s and 18.602 s wall), 604, and three calls with three drops. Photographed: the bus at the stop at the
    first two drops, the new guest by the pointer. `save/` unchanged.
  - **Corrected on the way**, each found by the review and re-read: `boot.md` said mode 1 does not sweep (it does,
    through `FUN_005166b0`, `0x005166f2`); `weather.md`'s skip at `0x0054f4d4` needs full screen as well as an inactive
    window; `park-engine.md` had the map straight after a 27-field header (26, then 5,522 bytes); `park.md` had the
    drip once a tick, the random vehicle on the dismiss path, the stop pair unproven, the packed id's stride 256
    (`FUN_004d8650` packs `y * 128`), and a headcount without `NewParkBonus` and its factor of 1.2 or 0.8, which makes
    a load 3 or 4 even at a score of nought (Q26 now says so); `PLAYER-GAPS.md` repeated three of those.
  - **No test was added**: nothing was built, so there was no fix to put back. Q68b's tests are the build's.
  - **Found:** Q68b and Q126-Q130.

  The item as written: Found by the 2026-09-24 staleness audit. `ParkPeople.StepArrivals` counts `GameClock.Ticks`,
  31 ms each, where `park.md` read the timer as quarters of `mGameTick`. Decode which clock `FUN_004cf3e0` reads at its
  call site and how often it runs, then build what it says. Confirm: the `guests` census over a timed run.
- [x] **Q68b. Guests arrive on the original's clock: the build.** Done 2026-09-25,
  `alexah/153-arrivals-on-the-original-clock`. `ParkState.GameTick` is `mGameTick`, seeded from the save's 755 and one
  up as each thing sweep begins; `ParkWorld.Arrival` reads the arrival block (0, 661, 5, 0, 0, 1, as FileFormats
  `saves.md` has it); `ParkPeople.StepArrivals` takes `FUN_004cf3e0`'s arms in order, re-read in Ghidra: the unsigned
  strict compare (`SHR`, `SHR`, `SUB`, then `JBE`), the call going straight on to ask the vehicle, an offloading flag
  apart from the count, and the let-go on the first sweep after the last drop that still finds the vehicle at 2
  (`0x004cf56b`). `StepVehicle` holds an unloading vehicle while a load is held.
  - **Confirmed in the game** (`q68bmeasure.py`, silent, jungle), predicted before the park loaded: the first call on
    `mGameTick` 1264, sweep 509, 126.05 s after the park came on show (126.23 predicted); its drop on 1300, 36 sweeps
    on, the bus driven in and photographed at the stop; the let-go on 1301; the next call on 1904, 604 sweeps after the
    drop and 149.54 s after the let-go, and its let-go on 1905. `peeps` 13, then 6 at the first drop and 2 at the
    second (thirteen went home); `unimplemented` without `SAVED_ARRIVAL_LOAD`; `save/` unchanged.
  - **Two predictions failed, both on the vehicle, not the clock.** The saved bus rests at pc 120 with `VAR_STATUS` 0,
    not 6, so the first call does not forget it: it went round and waited at the stop at 2 (pc 45) between loads,
    not at its leaving spin, and the second load's guest came on the call's own sweep, 1904, as a revised prediction
    written down before that call said. That is Q131.
  - **Put back and re-run:** twelve mutations, each failing a test: the mark from the frame clock; `>=`; a signed
    difference; the let-go on the drop's sweep; the mark stamped with the drop's tick; the clock not advanced; the
    clock read as `GameClock.Ticks`; the call returning before it asks the vehicle; the let-go not waiting for 2;
    `StepVehicle` passing `loadHeld: false`; the rule without `!loadHeld`; the block read two bytes early. New tests:
    `GuestsArriveOnTheParkClockFromTheWaitTheSaveLeft`, `TheBusIsHeldAtTheStopUntilTheSweepAfterItsLastGuest`
    (`bus.RSE`'s variables set by hand, a `ParkFixedItems` stood by reflection), `ALoadIsDueOnceItsWaitIsPastThePeriod`
    and `TheArrivalTimerIsReadFromItsOwnBlock`. `TickingARealParkCarriesItsGuestsThroughTheStateMachine` now expects 7
    visitors: no load is due in its 140 sweeps.
  - **Reviewed** by a read-only workflow (three lenses, a skeptic on each finding): eleven upheld, one refused, and the
    nine past each lens's first four checked by hand; all acted on. Stated at the site: the headcount floor (Q26), the
    stops (Q127), the refusals in world state 4 and at the cap (the original calls a load of nobody, or of what fits),
    a load saved half-dropped (counted, `SAVED_ARRIVAL_LOAD`), and the spent vehicle (Q131). "Game tick" stays
    `GameClock`'s; the park's counter is `mGameTick` in the log and in the new `arrivals` census.
  - **Found:** Q131 and Q132.

  The item as written: Found by Q68 (`park.md`, "Arrivals"). Give the park the original's `mGameTick`: the save's
  (`ParkWorld.GameTick`, 755 in Lost Kingdom), one up at the start of each thing sweep before anything in it runs. Read
  the arrival block (the last 18 of the 76 bytes `ParkWorld` skips; FileFormats `saves.md`) and start the mark from its
  `mTimeSig` (661). Call a load when `(tick >> 2) - (mark >> 2)`, unsigned, is more than `Arrival.TimeBetweenArrivals`,
  and reset the mark on the first sweep after the last drop that finds the vehicle still unloading. Turn round
  `StepArrivals`' summary, which calls the period the original's. Q82 wants the same counter for the staff. Confirm:
  predict the first load on sweep 509 (126.2 s of game time) and the next 602 to 605 sweeps after each last drop; the
  log, `peeps`, and the bus photographed at the stop.
- [x] **Q82. Staff may idle for an eighth of the original's time: the decode.** Done 2026-09-25,
  `alexah/154-decode-the-staff-idle-clock`. Decode only; the build is Q82b. `ride-operation.md`, "The staff turn": the
  guard's handler read by hand, then a read-only workflow of 12 agents (six decoders - the other four kinds, the shared
  `CStaff` code, and the caller with the save and the balance slots - each put to a skeptic reading the disassembly):
  116 claims, 97 upheld, 19 amended, none refuted; the load-bearing ones re-read by hand.
  - **Every clock a member of staff reads is `mGameTick`**, in all five kinds. Each idle arm (the guard's `0x004d6545`,
    the researcher's `0x00502b90`, the mechanic's `0x004da54d`, the handyman's `0x004d7524`, the entertainer's
    `0x004d4957`) waits until mGameTick > stamp + `IdleDuration`, unsigned: IdleDuration + 1 sweeps, 11 for Lost
    Kingdom's grade-3 guard. Each handler runs once a sweep, unstaggered (`FUN_00516380`, `FUN_0050b360`). The jobs'
    timers, a claim's expiry, research points and state 6's timeout count sweeps too; nothing reads the 31 ms counter.
  - **The guard's walk-or-stay is `mGameTick & 3`, not a roll** (`0x004d655d`, `0x004d64e9`, `0x004d63e1` after a
    rest, `0x004d5e76` at hire), and so is the entertainer's (`0x004d46fe`). The researcher's is the world random
    (`0x00502ba9`), and on a nought it researches: it never idles of its own accord. The mechanic and the handyman have
    no choice to make: with no work they take a random walk every time.
  - **Nothing zeroes a stamp that reads ahead of the clock.** `StaffBehaviour.Step`'s rule cites `FUN_004f9490`'s
    opening, which zeroes `+0x198`, `mStrandedTime`, against the route-call serial: another stamp on another counter.
    The saved stamps are readings of the saved `mGameTick` (one pass writes both, `0x00516f06`, `0x00517723`), so Lost
    Kingdom's guard, idle since 752 against 755, leaves idle on 763, the eighth sweep, and 763 & 3 is 3.
  - **Reproduced in the game with nothing changed** (`q82measure.py`, silent, jungle), predicted first. Every guard
    spell after a walk held its stamp exactly 2 sweeps (25 of 25; the original's 11) and every researcher spell 3 (25
    of 25; the original researches instead). The guard's 752 was gone on the first sweep and the guard walked on 758,
    where the original walks on 763. The handyman and the mechanic stood from sweep 761 to the end, 620 sweeps (610
    read, ten missed while photographing), and the entertainer from 768. The stamps are 31 ms ticks (312 on mGameTick
    794): `GameClock.Ticks` is not reset in a park (5 at the park on show, the lobby's carried in), so `Step`'s
    "starts again at nought" is wrong as the item says. Photographed, held by `pause` with the census read: the guard
    standing on the path on 794 and walking on at 798. One prediction fell short: that a researcher's spell goes on at
    stamp 0 after its 3 (8 of 25) was written in only after the first run, which crashed on a guest going home under
    the `facing` overlay (Q137). `save/` unchanged in both.
  - **No test was added**: nothing was built, so there was no fix to put back. Q82b's tests are the build's.
  - **Reviewed** by a read-only workflow of 20 agents (four reviewers, a skeptic on each finding): 17 upheld, 1
    refused, and the 20 minor ones checked by hand; all acted on in the page and the items below.
  - **Found:** Q82b and Q133-Q138, and notes on Q110 (the staff's thoughts), Q112 (every kind wanders) and Q132 (the
    guests' needs gate).

  The item as written: Found by the review of the 2026-09-24 staleness audit. `ParkPeople`'s staff loop hands
  `StaffBehaviour.Step` the 31 ms tick, but `FUN_004d6410` compares its idle stamp against `mGameTick` (`0x004d6545`),
  which counts thing sweeps (`park-engine.md`, "What the 31 ms tick drives") - the same question as Q68. Check the
  other per-kind staff handlers the same way, then pass `ParkState.GameTick`, the park's `mGameTick` (Q68b), and turn
  round the note in `ParkPeople`'s staff loop, which names the deviation. `StaffBehaviour.Step`'s stale-stamp note
  says "our clock starts again at nought": `GameClock.Ticks` is not reset on entering a park, so correct it too. Q68's
  decode settles the clock: the saved `mGameTick`, which Q68b gives the park. Confirm: the `staff` census over a timed
  run, the idle gap predicted first.
- [x] **Q82b. The staff take their turns on the park's clock: the build.** Done 2026-09-25,
  `alexah/155-staff-on-the-park-clock`. `ride-operation.md`, "Measured in the game after the build".
  - **Built:** `StaffBehaviour.Step`, `ThingRemoved`, and `ParkPeople`'s pickup and put-down take `ParkState.GameTick`;
    the guard's walk-or-stay is `mGameTick & 3`, the researcher's still a draw; `Step`'s zeroing of a stamp ahead of the
    clock is gone, and its idle and waiting tests compare unsigned, as `0x004d6545` and `0x00505745` do. Every note the
    item lists is turned round. A debug line names each stand's cause (`Staff: <id> stands on mGameTick <n>, ...`).
  - **Confirmed in the game** (`q82bmeasure.py`, silent, jungle, three runs of 648 to 650 sweeps, none missed: every
    photograph held by `pause`), predicted first. The guard read Idle since 752 through 762 and Walking on 763, all three
    runs. Every guard spell after a walk held its first sweep's mGameTick exactly 11 sweeps (28, 19 and 21 of them), all
    68 begun on a multiple of four and ended in a walk: no destination was missed. The guard set off from idle 71 times,
    every one on a sweep 3 mod 4. Every researcher spell held its stamp exactly 21 sweeps (14, 17, 15), and 6, 7 and 4
    went on at stamp 0, against about one in four predicted; the third run's log put all 20 of its stands on the draw,
    over all four remainders, and none on a missed destination. The handyman and mechanic were stamped 761, the
    entertainer 768 (712 kept while walking), 11 sweeps each, then 0. The first load came on 1264, as Q68b's.
    Photographed with the census read while paused: the guard standing at the path's corner on 792, idle since 792 (a
    paused control identical), turned to set off on 803, and walking 0.74 of a cell up the path on 807, the camera not
    moved. `save/` unchanged in all three.
  - **Tests:** `TheGuardSavedIdleSince752WalksOnMGameTick763`, `AStampAheadOfTheClockIsLeftAlone`,
    `AGuardStaysOnAMultipleOfFourWhateverTheDraws` (16 seeds, four sweeps), `AResearchersChoiceIsADrawNotTheClock`,
    `TheWaitingStateComparesUnsigned` and `TheParksSweepHandsTheStaffItsOwnClock` (400 sweeps through `ParkPeople`: the
    752, the 11 and 21 sweep holds, no set-off on a multiple of four), replacing
    `AStampFromTheOldParksClockDoesNotStrandThem`; the staff runs now start on the save's clock. Each of five bugs put
    back went red on its own test: the 31 ms tick in the sweep, the draw for the guard, the clock for the researcher, a
    signed state 6 wait, and the zeroing, which on the park's clock never fires (752 is behind 756).
  - **Reviewed** by a read-only workflow (four lenses, a skeptic on each): 5 upheld, 6 refused, all 5 acted on (the
    two tests above that pin the researcher's draw and state 6, the Q134 deviation said at `Decide`, a test summary and
    a leg list reworded, `addresses.md` regenerated).
  - **Found:** a hire's first decide, filed as Q136 (f).

  The item as written: Found by Q82 (`ride-operation.md`, "The
  staff turn"). Hand `StaffBehaviour.Step` and `ThingRemoved`, and `ParkPeople`'s pickup and put-down,
  `ParkState.GameTick`, the park's `mGameTick`, where they take `GameClock.Ticks`. Take the guard's walk-or-stay from
  `mGameTick & 3` (nought stays; the researcher keeps its draw). Take out `Step`'s zeroing of a stamp ahead of the
  clock, which has no counterpart. Turn round what names the deviation: the note in `ParkPeople`'s staff loop, `Step`'s
  tick parameter and stale-stamp note, the class remarks' "roll three times in four", `StayPutShare`'s summary and
  `Decide`'s "Three turns in four" (only the researcher's is a draw), and `StaffActivity.Waiting`'s "The original enters
  it from one place only" (nothing enters state 6; only a saved `mState` can). Confirm, predicted first: the guard,
  saved idle since 752, walks on mGameTick 763 if a destination is found; every guard spell after a walk holds the
  walk's stamp 11 sweeps and begins on a multiple of four unless no destination was found, so it ends in a walk unless
  none is found; every researcher spell after a walk holds its stamp 21 sweeps, about one in four then going on at
  stamp 0 (its own draw, until Q134). The `staff` and `arrivals` census over a timed run, and the guard photographed
  standing and walking.
- [ ] **Q69. Seven unbuilt paths are not counted.** Found by the 2026-09-24 staleness audit.
  `CLAUDE.md` rule 4 asks every unbuilt path the program reaches to call `Unimplemented.Report`, and these have no
  counter (two are queued for building, Q76 and Q77): the lobby's 90-second advisor repeat of response `0x18a`/`0x18b` (`0x005e184c`,
  `docs/exe/ui.md`); the advisor clip glints (`lobby.md`, "World sprites"); the isle's random M1/M2 clips, which
  loop here instead (`lobby.md`, "Not sound"); the press that goes to a stale hover after a window opens or closes
  (`lobby.md`, its "Unsettled" list); the splash, legal screen, movies and `welcome_<lang>` overlay (`ui.md`); the
  park gadget's research button, which only logs (`ParkGadget.NotYet`); and the camcorder walk's `FUN_0042a340`
  branch, left out of `ParkCamcorderCameraMode` (`park-engine.md`, "Walking on the ground is swept against the cell
  edges", its paragraph "One branch is decoded but not built here").
  Name a counter at the point each is reached - check it is reached first - and say so in the docs. Confirm: the
  `unimplemented` census after a lobby session names each one reached. Building the idle nag and the isle's clips is
  Q77 and Q76.
- [ ] **Q70. `BFSTReader` parses every string table twice.** From the 2026-09-12 review (Phase A, the one step of
  it not landed). `BFSTReader.ReadFromStream` calls `ReadFile()` and throws the result away (`BFSTReader.cs`) before
  `StringFile`'s constructor calls it again - delete the first. `World/Entity/Entity.cs`'s `using System.Reflection`,
  from the same step, is now unused. No game run; a test that one read happens.
- [ ] **Q71. Two guards that do not guard.** From the 2026-09-12 review. `Rotation.From( pitch, yaw, roll )` turns
  degrees into radians and then clamps them to -180..180, degree limits; `UiFonts.Get` checks `slot` against
  `Sets[0].Length` and then indexes `Sets[SetIndex]`. Each reads as protective and is not. Fix each with a test that
  fails first. No game run.

- [ ] **Q83. The VM's stack errors, and `HUSH`'s result register, are not the engine's. Decode first.** Found by Q45's
  handler sweep, not yet measured as reached. The engine's `RETURN` with no frame parks the script (`0x00553a63`);
  `RideScript.Return` carries on. Its `JSR` on a full stack logs, parks, then jumps anyway with no return address
  (`0x00553a24`), and with a non-label operand pushes and does nothing; `Call` stops the script for both. `PUSH` and
  `POP` errors park (0 uses). **Engine `HUSH` also writes the result register with the value it pushed** (`0x00553d95`,
  `MOV [EBP+0x48],EDX`, read first-hand), and `PUSH` too (`0x00553c89`); `PushValue` does not, and `HUSH` has 39 uses:
  find whether any shipped branch reads the register after one.

- [ ] **Q85. A guest who arrives starts with happiness nought, and stays there. Decode first.** Found by Q50's game
  runs: every one of the 33 guests who arrived (30 by `load 30`) read `happy 0` in `peeps`, none above it in nine minutes,
  while the save's 13 kept theirs (most at 50) until they went home, all by about four minutes, so a dock on anyone left
  clamps and shows nothing. `ParkPeople`'s new-guest record writes `Happiness: 0f` (and nought thirst, hunger,
  toilet, vomit, litter) with no note. Decode what the guest constructor `FUN_004faec0` and the arrival give a new
  guest, and whether a ride's settle-up should raise it, then build it. Confirm: `load 30`, `peeps` over a few
  minutes. **Then build the `InQueue` turn's unhappy arm** (`QUEUE_TURN_UNHAPPY` in `PeepBehaviour.QueueTurn`): below
  happiness 10, thought `0xb`, out. Alexah held it at Q50d (2026-09-24) until arrivals start at the original's 50
  (`FUN_004faec0`, `0x004fb075`), since at nought it would put every arrival out of every queue it joins;
  `ParkQueueTurnTests.AnUnhappyQueuerStaysUntilArrivalsHaveTheOriginalsHappiness` pins the hold and turns round with it.
- [ ] **Q86. Clearing a path joined to an entrance puts its whole queue out.** Found by Q50's decode. `ClearCell`'s
  path arm re-walks the entrance owner's queue (`0x0053694b`) after unlinking both sides, so the queue measures 0 and
  all but the nominee and state 14 go. `ParkPathBuilding.ClearPathCell` re-walks nothing. First check it is reachable
  in Lost Kingdom, where every path before an entrance is NOMODIFY, and from the queue stamp's forced clear
  (`0x00534741`).
- [ ] **Q87. `AdmitPerson` refuses where the original does not.** Found by Q50's decode. The original only logs a
  wrong person (`0x004e092c`..`0x004e0982`) and lets go of the nominee before it tests `VAR_LETMEON`
  (`0x004e09b0`); `ParkRideOperation.AdmitPerson` refuses the first and keeps the nominee on the second. Build the
  original's order. Confirm: `rides` and `peeps` through one admission.
- [ ] **Q89. The park's door does not command the gate. Decode first.** Found by Q50b's decode (`ride-operation.md`,
  "The closed ride"; `lobby.md`, "The park gate"). Opening the park writes the gate's `VAR_COMMAND` 1; closing writes 0,
  and only when `FUN_004c9130` counts nobody in the park and `VAR_STATUS` reads 1; 2 is the end-of-park routine's
  alone (`FUN_005168f0`). Counted `PARK_DOOR_COMMANDS_THE_GATE`. Decode what `Gates.RSE` does with 0 against 2 and which
  cells `FUN_004c9130` counts, then build it; `ParkRides.CommandTheGate` writes 2 for a park saved closed and
  `ParkFixedItems` and its tests say "2 shuts". Confirm: close an empty park at the door, photograph the gate.
- [ ] **Q90. The advisor says nothing when the park opens or closes. Decode first.** Found by Q50b's decode. The door
  posts a type-`0x13` message, 3 or 4, whether or not anything changed, and `CAdvisor::ReceiveMessage`
  (`FUN_0059b060`) answers with its own message `0x80` or `0x81` (`FUN_0059ae20`); `advisor-park.md` lists neither.
  Counted `PARK_OPENED_ADVISOR_MESSAGE`, `PARK_CLOSED_ADVISOR_MESSAGE`. Decode what the two say and when, then build.
  Confirm: press the door, the advisor's line in the log and on screen.
- [ ] **Q91. A ride's model does not change as it closes and opens. Decode first.** Found by Q50b's decode. Every
  close calls `FUN_00454550( model, 1 )` and every open `FUN_004547c0( model )` (`ride-operation.md`, the
  `FUN_00454550` row); the model loader around them names `Hoardings`, and `RideInfo.Hoarding` is parsed and never read.
  Counted `CLOSED_RIDE_MODEL_CHANGE`, `OPENED_RIDE_MODEL_CHANGE`. Decode what the four texture offsets and `+0xbc` draw,
  then build. Confirm: photograph the Belly Bounce before and after the door.
- [ ] **Q92. The ride window's door is not built.** Found by Q50b's decode. `FUN_004af600` case `0x3e38` →
  `FUN_0048ccf0` closes with `FUN_004df300` or opens with `FUN_004df390`, unguarded; `FUN_004ad4e0` sets the switch
  from `mCanLoad`, greys it for a closed ride the guard refuses, and the window's box (`0x3e25`) shows status code 1,
  `CLOSED` (UITEXT 365), or `0x17`, `CLOSED: QUEUE NOT CONNECTED` (`FUN_00485f60`). `ParkRideOperation.Close` and
  `Open` exist now, and the switch already follows `mCanLoad`. Counted `RIDE_WINDOW_OPEN_OR_CLOSE_THE_RIDE`,
  `RIDE_WINDOW_CLOSED_STATUS`, `RIDE_WINDOW_DOOR_GREYED`, and the all-items row colour `ALL_ITEMS_CLOSED_ROW_COLOUR`.
  Confirm: close the Belly Bounce from its window, `objects` and `peeps`, photographed.
- [ ] **Q93. A bought thing with a queue starts open, where the original's starts closed.** Found by Q50b's decode.
  The constructor closes every object with the queue-path bit (`0x004db712`..`0x004db793`), and the first queue
  measure that finds its back connected opens it. `ParkBuilding` sets the bit (`Info.HasQueue`, descriptor `+0x40`,
  pinned by Q50b) but not the close, counted `BOUGHT_QUEUED_THING_STARTS_CLOSED`. Build the close after the script is
  bound, and re-confirm Q1's flow on top of it. Confirm: buy a Belly Bounce, `objects` canload 0 until its queue joins
  a path, then a guest boarding.
- [ ] **Q94. The console's `path` lays what the path tool refuses.** Found by Q50b's decode. `ParkPathBuilding.Lay`
  checks the cell's type and NOMODIFY but not the verdict `FUN_00535670`, which refuses path over a queue cell whose
  `mNeighbours` has more than one bit - every Belly Bounce queue cell (`ride-operation.md`, "The queue measured
  again"). Q50's game run cut the queue that way, a cut the player cannot make in one click. Route the console
  through the verdict and re-stage Q50's confirmation with a cut the player can make, or say which. Confirm: `path 51 22`
  refused with the verdict's reason.
- [ ] **Q96. A charge is never deposited in the park's bank.** Found by Q50c's review. `FUN_004e16b0` first calls
  `FUN_004d0190( price )` on the bank thing (`0x004e16bf`..`0x004e16c6`): the balance `+0xc`, the world's `+0x1fc90`
  and the bank's `+0x124`, the adds the gate fee's `FUN_004d0600` makes (`ride-operation.md`, "Spending").
  `ParkState.TakeAt` credits the object alone and counts the rest (`CHARGE_BANK_DEPOSIT`); the comments said this was
  the original's. `ParkRideExitTests.PayingForARideLeavesTheParksBalanceAlone` pins the gap and turns round with it.
  Decide what `+0x1fc90` and `+0x124` are before keeping either. Confirm: a drink sold at the Drinks Shop, the HUD's
  money before and after (+30), and `money`.
- [ ] **Q97. The object's own cost of goods and chance of winning.** Found by Q50c's review. The object keeps both at
  `+0x188` and `+0x190`, built from the item at placement but saved and loaded with it (`FUN_004db7d0`,
  `0x004dcd01`..; file 1042 and 1050) and set per object from its window (`FUN_004e1a20`, `FUN_004e21c0`). OpenTPW
  reads the item's in the price opinion, the win roll (`FUN_004e2670`, `ParkRideOperation`) and the prize;
  Lost Kingdom's save holds the items' own, so nothing differs yet. Read both from the save record, a bought thing's
  from its item, and say it at each site. The window's setters wait on Q31. No game run beyond a census of the two.

- [ ] **Q98. Spot animations are never played. Decode first.** Found by Q50d. `FUN_004fc800(n)` plays animation `n`,
  stamps `mTimeOfLastSpotAnim` (`+0x208`), saves the state in `+0x224` and enters state 8, whose return
  (`FUN_004fc890`) is built; for `n` 4 it also plays sound `0x7e` for an id whose low nibble is nought. The queue turn
  reaches it for happiness above 80 and from 10 to 19 (`QUEUE_SPOT_ANIMATION`). While it is unbuilt a queuer's mood is
  read on every turn, and the window after an animation - the heading turned one turn in ten (`QUEUE_TURN_HEADING`) -
  is never reached. Decode its other callers and what animations 4 and 5 are, then build both. Confirm: `peeps` over
  a queue at happiness 90, the guests animating on screen.
- [ ] **Q99. The board arm's put-out when no route is found.** Found by Q50d. The original forgets a guest called
  forward and puts them out when `FUN_004fa5f0` fails (`0x0050010a`); `QueueTurn` counts it (`QUEUE_BOARD_NO_ROUTE`)
  and walks them on. The stand point is on the entry cell (`FUN_004dedf0(0)`, `ride-operation.md`, "Leaving a
  ride"), but `FUN_004fa5f0` also fails without routing when its retry stamp at `+0x198` says so (`0x004fa62a`,
  `FUN_004fa770`), which nothing here keeps. The stamp is decoded (Q50e): `mStrandedTime`, set only on
  `FUN_004f9490`'s stranded path and zeroed by every walk tick, so on the board arm it is nought unless a save loaded
  it - build the arm and say so at the site. Confirm: `unimplemented` over a long run (the counter's rate), then a
  boarding guest cut off by a path edit.
- [ ] **Q100. A toilet's `+0x44`, which the queue turn's dirt gate reads. Decode first.** Found by Q50d.
  `FUN_004e0390` puts out a queuer for a toilet (`+0x32 & 1`) whose `+0x44` truncates below 25.0 (`0x00700550`);
  nothing here keeps the field (`QUEUE_TOILET_DIRT_GATE`). Decode what writes it (the handyman's cleaning, use) and
  whether it is saved, then build the gate. Confirm: a queue at Lost Kingdom's toilet, `peeps` before and after.
- [ ] **Q102. The walk to a chosen thing's own arms.** Found by Q50e's decode (`ride-operation.md`, "Walking to a new
  place in the queue", the first caller). The original's state 10 (`FUN_004ffbc0`) takes `BigHappinessChange` (25)
  and pushes event 3 when the walk is stuck, where `GoingToRide` only goes back to deciding; takes 25 and clears
  `MajorDest` while walking with the park shut (`"The park has closed underneath me!"`), which nothing here does; and
  every 12th walking turn, counted across walks by the saved byte `+0x2c`, runs the minor decision `FUN_004fd570`,
  which may switch to a nearer thing and aim at its entry. Build the first two; the minor decision needs
  `FUN_004d8b40`'s raw line-search length. Confirm: `peeps` over a guest walking to a ride when the door shuts, and
  over one cut off by a path edit.
- [ ] **Q103. The gates at the back of a queue.** Found by Q50e's decode. On arriving, the original refuses on room
  with event `0x15` and KEEPS `MajorDest` (`GiveUpOnIt` clears it); asks excitement only when the item's `+0x13c`
  (`UsageInfo.ExcitementLevel`) has a non-zero low byte, of the OBJECT's computed excitement (`FUN_004e0860( object,
  0 )`, `FUN_004e0560`) where `TurnsAwayFrom` reads the catalogue level, with an event and thought per arm, then
  pushes the thing onto `mPreviousTemporaryRides` and zeroes `+0x1fc`; and refuses a queue too long (`FUN_004ddb60`
  against `FUN_004dda40`, which is 100 for a thing without the queue-path bit, so Lost Kingdom's five need no
  unproven field). The computed excitement is blocked on the divisors `park-engine.md` will not guess: say at
  `TurnsAwayFrom` that the catalogue level stands in for it. Confirm: `peeps` for a guest refused at a full queue,
  dest kept.
- [ ] **Q104. The chooser routes as it walks the objects.** Found by Q50e's decode. `FUN_004fcb10` routes every
  candidate that beats the best in turn, so a better one that cannot be routed still leaves the walker failed while
  `MajorDest` names the earlier winner, and the first state-10 turn takes the stuck arm (−25) - unless a ground change
  revives the loser's route and walks the guest to the loser's queue. `ChooseSomewhereToGo` routes once, to the final
  best. Build it with the walker's failed state, and say it at the site. Confirm: a guest choosing between a
  reachable shop and a better ride cut off by a path edit, `peeps` and the log.
- [ ] **Q105. The chooser scores the distance at the back of the queue.** Found by Q50g (`ride-operation.md`, "Where a
  guest is aimed"). `FUN_004fcc30` reads the squared distance, the close-to-queue test (under 9) and the nearby-effects
  divisor at `GetBackOfQueue`'s cell (`FUN_004de110` with the object in `ECX`, `0x004fcc49`..`0x004fcc7d`);
  `ParkRideChooser.ScoreOf` reads all three at the entry cell, which for the Belly Bounce is four cells from its back.
  Build it and retire the remark at `ScoreOf`. Confirm: `why` over a guest nearer the Belly Bounce's entrance than its
  back of queue, the chosen thing before and after.
- [ ] **Q106. `APathCellCostsWhatTheBalanceFileSays` fails when its class runs alone.** Found by Q50h, on `main` as well.
  `ParkPathBuildingTests`' `[TestInitialize]` keeps `GameData.Required()` in a field and never mounts it as the global
  `FileSystem`, so run by itself (`--filter FullyQualifiedName~ParkPathBuildingTests`) `levels/Standard.sam` does not
  load and the price answers -1; the whole suite passes only because an earlier class mounted it. Mount it as
  `ParkQueueRemeasureTests` does, and sweep the other test classes for the same order dependence. Confirm: each class
  alone, green. No game run.
- [ ] **Q107. `Decide`'s stamps and the chooser's empty hand.** Found by Q53 (`ride-operation.md`, "The state-6 turn,
  in order", the split). The original restamps `+0x1fc` only when a wander fails (`0x004ff3f4`) and when the chooser
  finds nothing (`0x004ff4a3`); `Decide` restamps after a routed wander and before choosing. With nothing chosen the
  original pushes event 1, plays spot animation 4 (Q98) and docks `SmallHappinessChange` (`0x004ff492`); ours does none.
  Confirm: `happy` and `peeps` over a Deciding guest the chooser fails, −5 each time it runs.
- [ ] **Q108. `SetRandomDest`'s linked walk.** Found by Q53 (`ride-operation.md`, "SetRandomDest", the linked arm).
  The original walks r % 5 + 1 linked cells from the mask of the cell being LEFT, never ending on the guest's own
  cell, and aims inside the last; it drops queue and entrance neighbours from a path cell, a queue cell's
  `mDirection` slot and exit cells; below a count of 2 it takes a fixed order, else a random start with no reverse.
  Ours steps one adjacent cell. Add `mSetDestSuccessfully` and SetState(7)'s re-aim with it. Confirm: over a run, no
  wanderer steps from (48,22) onto a queue or entrance cell, and wanders of up to five cells in the census.
- [ ] **Q109. When a guest leaves. Alexah's call first.** Found by Q53 (`ride-operation.md`, arm (d)). The original
  tests leaving in state 6 alone: the happiness byte nought, `mExitLevel` exactly nought (it counts down unclamped, so
  a four-sweep window) or the park shut; it docks 25 every turn the test holds, aims at `CrossingParkSide` (47,9) and
  (48,9) with a mode-1 retry, and sets state `0x12` only on a route. `Step` sends home any unheld guest at
  `ExitLevel <= 0` from any state, docking nothing, at the bus stops, whatever the route. Retiring `Step`'s arm keeps
  most guests in the park until unhappy or shut, which a player will see: measure first how many leave through the
  window, then ask.
- [ ] **Q110. The stranded bookkeeping, and thought bubbles.** Found by Q53 (`ride-operation.md`, "The stranded
  bookkeeping"). The shared counter, the 33 × 33 block stamps its map writes leave, `FUN_004fa770`'s 3 × 3 test, the
  refusals in SetRandomDest, `FUN_004fa530` and `FUN_004fa5f0`, the dead-end stamp, and SetThought's bubble
  (`FUN_0050be80`: sprite script `0x0074f2f8` of kind 9, gone 13 to 16 sweeps on). None is kept or counted. Count them
  first; measure whether a Lost Kingdom guest ever reaches `0x004f9e09`; decode which picture thought `0x11` is.
  Q82 found the staff's own thoughts through the same `FUN_0050be80`: `0x14` tired, `0x13` unhappy, `0x12` very happy,
  `0x15` the strike walk, `0x16` a failed patrol roll (`ride-operation.md`, "Drawn on the way").
- [ ] **Q111. The state-6 turn's arms before its split are unbuilt and uncounted.** Found by Q53 (`ride-operation.md`,
  "The state-6 turn, in order"). (a) spot animation 5 above happiness 80, (b) vomit, (c) litter to a bin (the Litter
  Bin at (44,29)), (e) facing an entertainer, (f) pranks: each is reached in Lost Kingdom and none calls
  `Unimplemented.Report` (`CLAUDE.md` rule 4); (e)'s fireworks half is dead by content. Count each where the original
  tests it, with its one draw, and put its build in the queue.
- [ ] **Q112. Staff on a cell with no links do not look for path.** Found by Q53b. The no-links arm reads no person
  type (`ride-operation.md`, "SetRandomDest"): a member of staff reaches the count inside their patrol area, or outside
  it once `FUN_00506f30` fails (`0x004f95af` falls through, where ours answers false for either arm), after the
  call's r % 5 + 1 draw, which ours does not take. `StaffBehaviour.SetRandomDest` counts both reaches
  (`STAFF_NO_LINKS_WANDER`). Wire `PeepBehaviour.WanderFromNowhere` into it: five failed tries answer 0 for staff too,
  with no stamp and no `FUN_00506f30`, which is the linked walk's dead end alone. Only a guard or a researcher reaches
  the staff wander (`StaffBehaviour.Decide`). Measure first where the count is reached: one of Q53b's two runs counted
  23 before any sale, the other none. Confirm: the guard put down on grass inside their area walks to the nearest
  path, `staff` and `unimplemented` read before and after, photographed.
  Q82 found every kind reaching the wander in the original, not only the guard and the researcher: the mechanic and the
  handyman with no work, and the entertainer (Q133).
- [ ] **Q113. The gadget's body, aerial and arm take no press.** Found by Q56. The original's body `0x1d` answers
  inside its 23-point outline (stream `0x00752940`, sub-op 4 at `0x00752ac2`), the arm `0x21` and its end over their
  rects, the handle `0x23` inside a 16-point outline, and the aerial `0x2d`/`0x2e` over theirs (`0x2e` answers a right
  click itself, `0x004a11a2`); here none takes the pointer, so a left or a quick right click on them reaches the park.
  Read the outlines into `UiControl.Outline`, build the aerial, and see Q66 before changing the hit test. Confirm: an item
  in the hand, a quick right click on the body's bare metal keeps it; a screenshot.
- [ ] **Q114. The park's full-screen toggle `FUN_004a29d0`. Decode first.** Found by Q56. It hides layer 0 under a
  full-screen control whose handler `0x004a2840` gives a right press to the camera and arms nothing. Only handler
  `0x0048a740` turns it on (`0x0048a7f3`, `0x0048a8d8`), installed by the screens `FUN_0048ac40` and `FUN_0048adb0`
  build; `FUN_004815d0`, `FUN_0048ac40`, `FUN_0048adb0`, `FUN_0048ae70` and `FUN_004a9180` turn it off. Decode what a
  player reaches it from, and whether F2 here is it, before building anything.
- [ ] **Q115. The park screens are modal here and are not in the original.** Found by Q56's review. The original builds
  the six management screens and the nine object windows onto layer 0 (`park-engine.md`, "Whose a right press is"): its
  gadget answers beside a screen (`FUN_004a0940` tests only the game menu), a left press on the park beside one is kept
  from the hand (`0x00488741`) but reaches the layer, a screen's root takes a press anywhere on it, and opening one
  closes the one open (`FUN_00485b40`, `DAT_007c24c8`). Here the six are `Modal`, which shuts out the gadget; the object
  window's bare frame lets a left press through to `ClickWorldAt`, and a left press beside it acts on the park; and the
  buy screen opens over an object window and leaves it. One hit reading for both buttons. Confirm: with a ride's window
  open, a left click on its frame over a path does nothing; the gadget's Info beside the buy screen switches screens.
- [ ] **Q116. In first person a left press still reaches the park.** Found by Q56's review. Entering first person hides
  layer 0 (`FUN_004a2ac0( 0 )`, `park-engine.md`, "Whose a right press is"), so no press reaches `Park_MouseMessageProc`;
  layer 1's `FUN_00488a00` hands a press to the camera table alone. Here `Level.WorldClick` runs in first person: a left
  click on a path arms the path tool, one on a ride opens its window. With Q59. Confirm: in first person, a left click
  on a path, `tool` still None; a screenshot.
- [ ] **Q117. A right click on a list row or an object window's preview.** Found by Q56. The all-staff, visitors and
  all-items lists answer a right click on a row (`0x402`) by moving the camera to that thing and closing the screen
  (`FUN_004867b0`: `0x0049602f`, `0x004934c5`, `0x00495584`); an object window's preview answers any click the same way
  (`LAB_0048d1a0`). The click is the UI library's (`hud.md`, "A click and a double click"; `WindowStack.RightClick`). Counted on the
  press as `LIST_ROW_RIGHT_CLICK`; the preview takes no pointer here (Q115), so its click is not counted. Confirm: a right
  click on a guest's row, the camera on that guest and the screen shut; a screenshot.
- [ ] **Q118. The camcorder key acts on its press, both ways.** Found by Q57. The original's C is shortcuts row 16
  (`0x0040c5c0`), run on the key's release as every row is (`FUN_0040c990`), and first person is left on a key-up whose
  action is camcorder (`FUN_00488a00`, the same exit as Escape's; `scenes.md`, "The park Escape route").
  `ParkOrbitCameraMode.Update` enters and `ParkCamcorderCameraMode.Update` leaves on `Input.Pressed( InputButton.CamcorderMode )`,
  and neither says so at the site. Confirm: hold C in orbit, then in first person - nothing until each release; `state`'s
  camera height and a screenshot.
- [ ] **Q119. A plain Escape does not close the park screen in front.** Found by Q57's review. In the original the six
  management screens, an object window and the map take the focus as they open (`FUN_00485b70`, `FUN_004862a0`), and
  their key handler answers a plain Escape let go by closing the screen, and nothing more (`FUN_00488ba0`, `0x00488bc6`;
  the map at `0x005f17ef`; `scenes.md`, "The park Escape route"). Here those screens are modal and keep Escape
  (`ParkFrontEnd.MenuKey`), and an object window, which is not modal, lets it through to the hand and the menu. Said at
  the site. Confirm: the buy screen, then an object window with the path tool armed - Escape let go closes each, `tool`
  still Path and `windows` without GameMenu; a screenshot.
- [ ] **Q120. The modifiers are judged as the frame ends, not at each key.** Found by Q57's review. The original reads
  Shift, Ctrl and Alt with `GetKeyState` at each key-up (`0x0046bb0b`, `scenes.md`, "The park Escape route"), so a
  modifier let go in the same frame as Escape but after it still counts. `Input.BindingMatches` and
  `Input.NoModifierHeld` read the held set as the frame ends, so Shift+Escape let go with Escape first, inside one frame,
  empties the hand or opens the menu. Carry each event's modifiers (SDL's, Shift, Ctrl and Alt only) or replay the
  frame's events in order. Said at `NoModifierHeld`. Confirm: Shift+Escape with the tool armed, Escape up then Shift up
  in one frame through XTEST - `tool` still Path.
- [ ] **Q121. A held right button in first person does not walk.** Found by Q59's decode (`hud.md`, "Four ways out of
  camcorder mode"). The viewfinder layer's handler hands every message to `FUN_0042a760`, which sets 4 in `DAT_00790aac`
  while the right button is down (`0x0042a8bd`), and the walking camera adds the Up arrow's 0.1 to its forward term for
  as long as it is set (`0x0042b935`), whatever RMB cancel is. Here only the keys walk. Counted per frame held,
  `FIRST_PERSON_RIGHT_BUTTON_WALK` (`ParkCamcorderCameraMode.Walk`), a hold on the eject button included; the walk
  built must not take one, since the button's press never reaches the layer. Confirm: in first person hold the right
  button 2 s on the view, `state`'s camera moved forward; a screenshot either side.
- [ ] **Q122. Entering first person leaves a park screen open.** Found by Q59's review. `FUN_00481a10`, which C and
  `b_1person` both reach, first closes the open screen (`FUN_00485b40`, message 5 to `DAT_007c24c8`), and entering hides
  layer 0 with anything else on it. Here an object window or a management screen stays up over first person, and a right
  press on its body is the screen's, so it does not leave (said at `ParkViewfinder.RightClickAnswer`). Add the call to
  `park-engine.md`'s decode of `FUN_00481a10`. Confirm: a ride's window open, C, the window gone; a screenshot.
- [ ] **Q123. The click limits run on the frame clock.** Found by Q59's review. `WindowStack.RightClick`'s 500 ms and
  `Level.RightButton`'s 200 ms read `Time.Now`, whose frames are clamped to 0.1 s and which the console's `pause` holds;
  the original times both in milliseconds of wall time (`FUN_0065968e`, `hud.md`, "A click and a double click"). Below
  10 fps a held press can pass as a click. Said at `RightClick`. Keep the tests able to set the time.
- [ ] **Q124. `Material.Default` compiles a shader nothing draws with.** Found by Q67's sweep. `Material.UI.cs` builds
  it from `content/shaders/3d.shader` the first time `Material` is touched and keeps it for the life of the process.
  Its one reader is the guard in `Material.Delete`, which can fire only if something holds it, and nothing does. Two
  comments say the terrain draws with it (`Model`'s summary and `Material.Delete`'s): the park's ground, the lobby's
  models and the paths build their own `test.shader` materials, and the sea its `water.shader`. Dead by CODE: label it
  or take it out (rule 3, as Q67 did), and correct both comments; Q29 cites `3d.shader` for how the renderer lights,
  so check that against the shaders drawn. Confirm: a grep for readers, and `assets list` in the lobby and a park,
  before and after.
- [ ] **Q125. `CacheFileSystem` is set, and nothing in the game reads it.** Found by Q67's sweep. `Game.Run` creates
  `OpenTPW/cache` under the local application data folder on every launch and mounts it as `CacheFileSystem`
  (`Game.cs`, "mainly for editor-related stuff"). Its one reader is ModKit's thumbnail cache (`Editor.cs`), a separate
  program that never sets it and would read null. Dead by CODE in the game, and making the folder is all it does.
  Label the game's side or take it out (rule 3); the property stays, since ModKit, which is Alexah's call, reads it.
  Confirm: a grep for readers, and a launch, listing the folder before and after.
- [ ] **Q126. A long frame runs every thing sweep it owes, where the original runs three.** Found by Q68's review.
  The park loop counts a frame's sweeps (`[0x00879064]`, `0x0054f680`) and drops any past the third: the step and its
  counter move on, `mGameTick` does not, and nothing makes it up (`park-engine.md`, "What the 31 ms tick drives").
  `ParkPeople` runs one sweep for every eight ticks `GameClock` owes, up to its 2 s cap, so eight after a stall.
  Reachable only in a frame longer than about 0.74 s. Build the cap where the sweeps are counted, said at the site.
  Confirm: a long frame forced, and the sweeps it ran counted in the log.
- [ ] **Q127. A new guest is made at each stop in turn, where the original makes every one at stop B.** Found by
  Q68's decode (`park.md`, "Arrivals"). `FUN_004cf720` always asks `FUN_004d8650` for `BusStopB` (`0x004cf745`) and,
  while `FUN_0051aad0` reports a vehicle standing, takes two rows off the packed id (`0x004cf75c`): (53,3) in Lost
  Kingdom. `ParkPeople.StepArrivals` alternates `BusStopA` and `BusStopB` by the tick's parity, which nothing cites.
  Decode `FUN_0051aad0` first, then build it. Confirm: the `arrived at` log lines of a timed run, and a screenshot.
- [ ] **Q128. A guest going home stops at the park's edge: the stop's cells are now proven.** Found by Q68's decode.
  `PickingACellOutside` (19) and `AtTheBusStop` (21) walk to cells from `FUN_004d8650`, and `PeepBehaviour` leaves
  both unbuilt because the balance pair was unproven; `ParkPeople` treats 19 as the end of the walk. The slot table
  proves the pair is `FixedItemInfo.BusStopA/B` (`park.md`, "Arrivals"), so `PeepBehaviour`'s argument that its four
  candidates rule the stops out is wrong somewhere; find where. Check the two states' decode is whole, then build them
  and turn round the notes in `PeepBehaviour` and `ParkPeople`. Confirm: a guest who has decided to leave walks to the
  stop and is removed there; `peeps` and a screenshot.
- [ ] **Q129. The crowd sets the music's level every frame, where the original sets it once a second.** Found by Q68's
  review. The park loop reaches `FUN_0051e790` only on every 32nd tick (`TEST [0x00877d34],0x1f`, `0x0054f82d`) and
  clamps the crowd count to 89 before it (`0x0054f84e`), which binds from 178 guests; the park holds 1,500.
  `ParkAudio` asks every frame, and its comments say "every pass", that the clamp "never binds", and "Nobody spawns
  or leaves yet". Build the cadence and the clamp and correct the comments (`scenes.md` is corrected). Confirm: the
  level's changes counted over a timed run, and guests added with `load` past 178.
- [ ] **Q130. The staff pool never refreshes, and nothing counts it. Decode first.** Found by Q68's review. Every
  sweep the original runs `FUN_005084f0` (`0x004d7b30`), which drops a candidate left in the pool longer than
  `StaffTimeoutTime` plus up to half again, and every `TimeBetweenStaffUpdates` tops the pool up by at most
  `MaxNumberOfStaffPerUpdate`, both keys counted in fours of sweeps, as the arrival timer's is (`FUN_0041a970`
  against the key times four, `0x0050850f`, `0x00508549`; the lifetime drawn at `0x005077b9`).
  `ParkStaffPool` fills the opening pool only and reads none of the three keys, and no `Unimplemented.Report` says so
  (`CLAUDE.md` rule 4). Count it now; decode the refresh (`FUN_005084f0`, `FUN_00507600`), then build it on Q68b's
  counter. Confirm: the hire screen's candidates over a timed run, a screenshot before and after a refresh.
- [ ] **Q131. A spent vehicle is sent round again, and waits at the stop for the next load. Decode first.** Found by
  Q68b's review. `FUN_0051a690` answers a vehicle at state 6 by writing its script's variable 1 (`FUN_0055a070`,
  `FUN_0055a0b0`; decode what) and clearing `mCurrentArrivalVehicle`, and nudges nothing, so the vehicle waits at its
  last spin (`bus.RSE` 117, its object killed) until the next load's `FUN_0051a2f0` summons it, and every load has the
  drive in. Lost Kingdom's save holds its bus there, at pc 120 with `VAR_STATUS` 0. `ParkPeople.StepVehicle` releases
  state 6 and forgets the vehicle, so the bus drives back and waits at the stop at 2: in Q68b's run it stood there
  from its first circuit to the next call, and that load's guest came on the call's own sweep. While a load is held
  the original's -1 arm summons the load's vehicle by size again at once (`0x004cf489`), where `StepArrivals` asks
  vehicle 0, which `ParkFixedItems.VehicleName` answers as the bus. Confirm: the bus photographed away from the stop
  between loads, and the log's call-to-drop run-in the same on every load.
- [ ] **Q132. Guests and rides take their turns on the frame clock over eight, where the original hands them
  `mGameTick`. Decode first.** Found by Q68b. `ParkPeople.OnUpdate` hands `Peep.Tick`, `PeepBehaviour.Step` and the
  rides' turns `GameClock.Ticks / 8`, which runs from the program's start and is not reset on entering a park
  (`PeepBehaviour.Step`'s `tick` note); the original's handlers read `mGameTick`, which `ParkState.GameTick` now
  carries from the save's 755. The needs share `(id & 3) == (tick & 3)`, the behaviours' time stamps and the chooser's
  tie on `mGameTick & 1` (`ParkRideChooser.Beats`) turn on it. Decode which of them read `mGameTick`, then pass the
  park's clock, as Q82 does for the staff. Confirm: a saved guest's stamp read against 755, in the `peeps` census,
  predicted first.
  Q82 found the guests' needs gate reads `mGameTick & 3` (`FUN_00501650`, `0x00501669`), so `ParkPeople`'s "reads a
  separate counter for it" is wrong.
- [ ] **Q133. The mechanic, the handyman and the entertainer stand once their saved walk ends, where the original's
  walk about.** Found by Q82 (`ride-operation.md`, "Leaving idle, or a walk: the choice by kind"). With no work the
  mechanic's `FUN_004da5b0` and the handyman's `FUN_004d7100` take a random walk every time (`0x004da6fa`,
  `0x004d712d`), and SetState(0) only when none is found; the entertainer's `FUN_004d46d0`, after a draw mod 3 and no
  guest within `ActivationDistance`, takes the guard's `mGameTick & 3`. `StaffBehaviour.Decide` stands all three, and
  none of their searches is counted (`CLAUDE.md` rule 4): a broken ride, litter, a loo, guests to perform to. Q82b
  first. Build the no-work walk and count each search where the original makes it. Turn round what calls the standing
  the original's: `Decide`'s summary ("the original's shape rather than a limit of this build"), the class remarks'
  "finish the walk the save left them on and then stand, which is honest rather than invented", and
  `StaffActivity.Idle`'s "the guard and the researcher also check whether they are too fed up": every kind's decide
  calls `FUN_00506a40` first (`0x004da5b8`, `0x004d7108`, `0x004d46d5`). Confirm: all five staff walking in a timed
  run, the `staff` census and `unimplemented`, photographed.
- [ ] **Q134. The researcher researches, where ours stands. Alexah's call first.** Found by Q82. On a nought from its
  draw, or no destination, the researcher takes state `0xf` (animation 10, `+0x214` = mGameTick) for
  `ResearcherConstsPerGrade.WorkDuration` + 1 sweeps (31 at grade 2), then walks or researches again; it never idles of
  its own accord, and every 20 sweeps it adds `ResearchAbility` to the lab (`0x00502984`). OpenTPW has no state `0xf`,
  so the fourth decide stands. Research is deferred by Alexah (`docs/PLAYER-GAPS.md`), but the state and its timer need
  no lab: ask whether to build that half now, and count the points meanwhile.
- [ ] **Q135. The staff's sounds are neither played nor counted.** Found by Q82. Every idle and walking turn draws
  the world random and on one in sixteen plays a cat_staff effect at the member's position (`FUN_004faa00`): idle
  `0xa1`, `0xa3`, `0xa5`, `0xa7`, `0xa9` and walking `0xa0`, `0xa2`, `0xa4`, `0xa6`, `0xa8` (handyman, mechanic,
  entertainer, guard, researcher), `0x8a` a researching turn; and with no draw `0x87` a performance's end, the guard's
  `0x88` (`Oi.mp2`) as a chase starts and `0x89` on a catch, both waiting on the chase, itself unbuilt. Count them first
  (`CLAUDE.md` rule 4); then decode each effect's samples and build. What a sample says is known only by listening.
  Confirm: `voices` and `unimplemented` over a timed run.
- [ ] **Q136. Six small differences in the staff's decide.** Found by Q82 (`ride-operation.md`, "Drawn on the way").
  (a) Tired is `(u8)trunc( rest ) <= RestLevel`, signed and inclusive (`0x00506b41`); `StaffBehaviour.Decide` tests
  the float `< RestLevel` and misses [1, 2). (b) The patrol roll `FUN_00506f30` takes only a path cell (`mType` 1,
  `FUN_00536310`) before it routes; `PatrolRoll` routes to any, its remark calling the predicate unestablished. (c) Not
  tired, `FUN_00506a40` sets the speed word `+0xc0` from the rest byte (60 to 140, `[0x0075c7f8]`), one of
  `FUN_004fa870`'s three terms, where the walk keeps the saved `max_speed`: decode how the terms reach the walk first.
  (d) Tired with no rest area found or reached, `FUN_00506a40` answers 0 and the kind's own choice follows (the guard's
  at `0x004d6554`, the researcher's at `0x00502b9f`); `Decide` stands them instead, so a tired member with no reachable
  Staff Room never walks again. (e) At the end of a rest the original runs the kind's decide in the same sweep
  (`FUN_005061d0`, `0x00506298`); `Rest` sets Idle at stamp 0 and decides a sweep later, which after Q82b reads the
  guard's `mGameTick & 3` a sweep late. (f) Found by Q82b: at hire the guard and the researcher decide at once, after
  `FUN_00506a40` (the guard's `0x004d5e76` on `mGameTick & 3`, the researcher's `0x005026cb` on a draw); `Hire` sets
  Idle at stamp 0, so they decide a sweep later. Confirm each in the `staff` census.
- [ ] **Q137. A guest going home under the `facing` overlay crashes the park.** Found by Q82's first run: an
  `IndexOutOfRangeException` in `ParkGuestSprites.Collapse` in the frame guest 37 went home. `Remove` rebuilds the
  vertex array at two quads a person, and the draw after it collapses every quad up to the last frame's `_uploaded`,
  which with the overlay's dash was two a person of the crowd before: past the new end. Without the overlay it waits
  for more than half the drawn crowd, staff included, to go before one draw, one going to none being the single case.
  Confirm: `facing 1`, a guest sent home with `depart`, the park still drawing, photographed.
- [ ] **Q138. The staff strike is neither built nor counted. Decode first.** Found by Q82's review
  (`ride-operation.md`, "Drawn on the way", the strike). `mStaffHQ`'s month handler `FUN_00508e70` runs every month
  the park is open: for each kind with staff it clears a set flag `[HQ + 0x28 + kind × 12]` or calls `FUN_00508f70`,
  which returns until the date passes 24 months; past that `FUN_00509360` raises the level, and levels 1 to 4 set the
  flag and post the warnings. Every decide opens with `FUN_00506a40`'s strike arm (the flag and the gate's
  `VAR_STATUS`), a walk to the strike area in state 4; state 5's `FUN_00506300` ends it. OpenTPW has none of it,
  counts none of it, and reads nothing of the save's model-9 record (`mForceStrike`, `mStrikeLevel[i]`). Decode the
  reach first: the epoch of `FUN_004f8800`'s 24-month gate. Count the monthly consideration where `FUN_00508f70` is
  reached (`CLAUDE.md` rule 4), and turn round `StaffBehaviour`'s remark that the strike needs a script: the flag is
  `mStaffHQ`'s own, and the arm's one script read is the gate's status, which `ParkRides.GateStatus` answers.

## B. Docs and comments

- [ ] **Q13. Move `docs/CLEANUP-PLAN.md` into `docs/history/`.** Every item in it is closed. It is still untracked in
  `docs/`, so it exists on this machine only. Commit it under `docs/history/` and list it in `docs/history/README.md`.
  (The STATUS diet landed in `c445844`; `QUEUE.md` and both reviews were committed in `c2ddaf6`.) No game run.
- [ ] **Q88. Three stale comments and two labels from Q50's decode.** `PeepBehaviour.ParkIsClosed` says nothing can
  close a park (the entry-price door does, `ParkEntryPriceScreen` → `SetParkClosed`); `ParkRideOperation`'s settle-up
  calls the Jungle Spray's cost of goods five (`Junspray.sam` says 50); `ParkRideChooser` says the entry cell "is
  where a guest is actually sent" with no word that the original aims at the back of the queue (Q50e);
  `DropStaleQueueHeads` is dead by CODE (only tests call it) and `HeldByAThing` misses a state-8 queuer (dead by
  CONTENT): label both (`CLAUDE.md` rule 3). No game run.
- [ ] **Q95. Two descriptor offsets the compiled `.sam` schema names otherwise.** Found by Q50c's schema simulation
  (`FUN_00401030` over the table at `0x00744b30`), not yet checked against each page's own evidence: `hud.md` calls
  item `+0xC4` `Research.Group`, which the schema puts at `+0x178`, making `+0xC4` `UsageInfo.GoldenTicketCost`; and
  `park-engine.md` divides a capacity by `+0x1a0`, which the schema makes `Upgrades[0].InitDuration` (`+0x198` is
  `InitCapacity`). Settle each against the code that reads it, and correct the page that is wrong. No game run.
- [ ] **Q101. `ParkWorld`'s person-block walk names two fields the game does not.** Found by Q50d's decode. The
  comment beside `ParkWorld.ReadGuest` lists `mHappiness 422` and `mToilet 525` among the serialiser's own names;
  `FUN_004fb530` tags every need float `pv` (`0x0075b444`), and no string `mHappiness` or `mToilet` is in the binary.
  The order and offsets stand (happiness `+0x19c` and toilet `+0x1ac` are named by the debug strings at `0x004fda74`
  and `0x004fd10e`); say the two names are this project's.
- [ ] **Q46. Seven more stacked doc comments.** Found by Q11's scan of every source file (the six in Q11 were
  the first). Each sits on another member's summary, so it documents the wrong member. By member, since line numbers
  go stale: in `ParkGuestSprites`, `Standing`'s block lands on `StandingFrom` (Standing's own `<inheritdoc>` must go);
  in `ParkPeople`, `Fire`'s lands on `IsStaff` and `StepVehicle`'s on `ReleasesVehicle`; in `PeepBehaviour`, `Step`'s
  lands on `HeldByAThing` with a stray `<param name="tick">`, and `ChooseSomewhereToGo`'s on `Explain` (merge it with
  its `<returns>`); in `ParkGround`, the constructor's `<param name="world">` lands on the `_world` field; and in
  `ParkState`, `NextThingId`'s upper summary ("A thing id nothing is using") is stale, to delete rather than move. Stale in their own right,
  near them: `SettleUp`'s summary (three claims the code now contradicts) and its body's "Five, for the Jungle Spray" (the
  prize is fifty), `Explain`'s "whether they could actually get there", and `AGuestLetOffARideEndsUpStandingAtItsExit`'s "every other test here checks where a guest is aimed".
  No game run.
- [ ] **Q49. A stale comment in `CreateTexture`.** It says its cache check is also reached from `SignFile`, but sign
  textures are built by the byte[] constructor, with no path. (Q12's review also doubted `TryAdoptCached`'s "nothing
  the game ships asks for one path under two sets of flags"; ddfd089 measured it, texture=542 distinct=535 with and
  without the guard, and Q12's run found the sea adopted on the way back from a park.) No game run.
- [ ] **Q84. `RideScript.Wait`'s summary says no opcode writes the speed word, "so it is 50 for every script".** Found by
  Q45's handler sweep. True of the opcodes, false of the script: the object constructor pushes the item's operating
  speed in through `FUN_0055a300` (`0x004db54a`; `docs/exe/park.md`, "The clock, the speed word, and WAIT"), so it is
  50 only for a script nothing binds. Rewrite the summary; the divisor is still counted as `RIDE_SPEED_SCALES_WAITS`.
- [ ] **Q14. Comment sweep of the cleanup commits.** Replace history-voice comments with what the code does now,
  found by member because line numbers go stale: `LobbyCameraMode`'s attract-box remarks (the two `>>>` banners and
  "Alexah judged ... on 2026-09-22"); `ParkCamcorderCameraMode.Walk`'s "An earlier note here" and "until
  2026-09-22", `Steer`'s "An earlier note here claimed the exe corroborates the sign", and the doubled "Whichever
  side is met first decides" line in `Slide`; `ParkThingStates` ("this said eleven and three"); `ParkScriptStates`
  ("got wrong twice"); `AudioListener.AttenuationTo` ("This used to name", plus its `<para>` inside a `<para>`);
  `GameClock`'s pause-gate remark ("which this comment did, in both directions at different times"); `Texture.Cache` ("It used to be", "The paragraph this replaces");
  `ParkPeople`'s sprite-seeding note (a test count in a code comment); and `RideScript`'s default-opcode note ("Then
  it said"). Also found by the 2026-09-24 audit: `Entity.All`'s "This used to be seeded from reflection",
  `Texture`'s "This used to build another every time it was read" and "(This used to say", `Texture.Cache`'s opening,
  `RideVM`'s handler-count note, `Game`'s two loading-step seed summaries ("It was 3,214 until", "That last number
  was 876 until"), `ParkFrontEnd.OnUpdate` ("this comment used to miss it", "used to claim"), `LoadStepCounts`' class
  summary ("That number used to be a constant"), `ParkGadget` (three), `ParkPeople.PeepsIn`'s summary and
  `ParkPeople.WalkFor` ("This said ..."), `ParkRideOperation` (two), `ParkCamcorderCameraMode`'s class summary ("This
  said the gadget did not exist yet") and `ParkFixedItems`' remarks ("This paragraph once said"). `WalkSpeed / 60f` in `ParkCamcorderCameraMode.DebugWalk` uses `Time.Delta`. (The IslandPanel and
  LobbyGate sites and the DebugConsole tab were done by Q41, `325f102`.) No game run.
- [ ] **Q81. The FileFormats docs still disagree with themselves and with the game.** From the clone's 2026-09-18
  audit, as the 2026-09-24 staleness audit found it (`docs/history/fileformats-docs-ledger.md`). Most of it is fixed,
  on disjoint branches, which is the other half: `rsse.md`, `vm/instructions.md`, `sounds.md` and `saves.md` (whose
  World block is written three times) exist in divergent versions, and the `.md2` format has three pages
  (`models.md`, `md2.md`, `model.md`). `vm/info.md`'s per-script variable ids are fixed on `docs/rsse-instruction-set` (and
  `docs/save-module-chain`) only. Still wrong: `saves.md`'s `mFlags` `0x8` bit, its "version 85" (hex), and "the sixth
  person model is the visitor"; and `wad.md`'s name length, which includes the NUL. Open on both sides: what `0x14E`
  means (`SpriteBankFile`'s note). The Bumper enum is not a page error: the page's 1-17 is probably right, and it is
  `ScriptDefs.cs`, a labelled leftover left as it is, that looks corrupt. Settle each page's one
  version on the branch that owns it (`docs/README.md`, "The FileFormats docs clone"), then fix. No game run; the
  site builds.
- [ ] **Q75. The 2026-09-12 review's Phase F, what is left.** Opportunistic, never alone. Done: `LangVersion` 14.0
  (`4f6da17`), `Terrain.cs` (`2f38ce3`), `ReadInt16` (`f31862e`). Left: `SixLabors.ImageSharp` 3.1.6 in
  `OpenTPW.ModKit.csproj`; `content/textures/test.png`; `.editorconfig`'s `end_of_line = crlf` with no
  `.gitattributes`; and, to LABEL as dead by CODE rather than delete (rule 3), `Public/ModelFile.cs` and
  `Public/MapFile.cs` (both unreferenced), `Render/Primitives/Cube.cs` and `ReadUIntN`. `Singleton.cs` is used by
  `UI/Cursor.cs`, though nothing reads its `Instance`. Left to Alexah's call, not to be started: `.gitignore` for `.claude/`,
  `.vscode/` and `.mcp.json`, ModKit as a whole, and the nullable warnings except in passing.

## C. Alexah's list: cause known, one session each

- [ ] **Q15. Green line between path cells and on ride signs.** Path textures get the default sampler,
  `AnisotropicWrap` = Wrap (`ParkPaths.cs:220`, `Material.cs:106`). Sign halves get `AnisotropicRepeat`
  = Mirror (`SignTexture.cs:71-72`, `Material.cs:107`). The original clamps at every site
  (`render-states.md:99-112`). With wrap plus mipmaps, a tile's edge samples the far edge. Use clamp
  where the original does. Confirm: screenshot two paths in a column, a crossroads and a ride sign,
  before and after.
- [ ] **Q16. Screams are heard everywhere.** Sounds are placed and panned, but a park never sets
  `Audio.ReferenceDistance` (`Audio.cs:295`), so nothing attenuates. The lobby sets it
  (`LobbyAudio.cs:406`) and clears it on the way out. The distance law lives in `QMixer.dll`
  (`audio.md`), so the reference distance is a declared choice; say so at the site. Confirm: capture the
  mix with the camera on the ride and far from it; show the level difference; screenshot both camera
  positions.
- [ ] **Q17. Camcorder mode can strafe.** `ParkCamcorderCameraMode.Walk` (`:399-404`) passes
  `Input.Right` into `Step`. `park-engine.md`, "Walking on the ground is swept against the cell edges", decodes the sweep by two velocities but not the
  keys. Remove the strafe. Confirm: press the strafe key, the stand position in the `camcorder` census
  does not move sideways, screenshot.
- [ ] **Q18. The advisor draws in front of the dimmed screen.** `Level.cs:915-944` draws the HUD, then
  the overlay pass with the advisor. Windows that do not pause do not hide him (`ui.md:120`). Draw the
  dimmer over him, or hide him for those windows too. Confirm: screenshot with the buy screen open.
- [ ] **Q19. VSync and a frame limiter.** `Renderer.cs:345, 357` hard-code VSync on. `Display.cs` already
  carries each mode's refresh rate. Add an Options row: VSync mode, and a frame limit from 30 up to
  Unlimited, default the monitor's refresh rate on first launch. Confirm: screenshot the options row;
  log the measured frame time at two settings.
- [ ] **Q20a. A world particle pass.** Nothing draws a particle effect in the world (`ParticleSystem`'s summary, "Not
  built: drawing effects in the world"; `ScreenParticles` draws only `OnScreen` templates). Build it once, for Q20b and
  Q38. Confirm: one known effect drawn in the park, screenshot.
- [ ] **Q20b. No bubbles on the drinks shop, no chimney smoke on the staff room.** The scripts ask for the effects and
  `RideEffects` records the request; nothing consumes the records. Join them to `ParticleSystem.Spawn` through Q20a's
  pass (`ParLib.cs` names `Bubbles = 58`, `Smoke = 2`, `SmallSmoke = 13`; `park.md` decodes the node and the per-tick
  push). Confirm: screenshot the drinks shop with bubbles and the staff room with a staff member inside and smoke
  rising; log lines for both spawns.
- [ ] **Q21. Entering a park: hide the front end for the fly-in.** `IslandPanel.EnterPark` closes only the
  island panel, and the rest of the front end stays up while the camera flies in. Hide it for the flight, and
  say what Escape's cancel (`FrontEnd.MenuKey`, `LobbyCameraMode.CancelLeave`) brings back - today only the
  island panel. The gate half landed with Q41: the flight plays M1 as the homing ends and M2 on a cancel
  (`docs/exe/lobby.md`, "Escape cancels the fly-in"), so do not re-pace it. Confirm: screenshot burst from the
  click to the loading screen.

## D. Alexah's list: decode first, then build (two sessions each)

The decode session writes the finding to `docs/exe/` and stops. The build is the next session.

- [ ] **Q22. Riders sit still on the Belly Bounce.** Seat positions are read once from the model's rest
  pose (`LobbyModel.cs:297`) and never from the animated pose. A riding peep's sprite is `None`
  (`Peep.cs:490-492`). Decode: which frame a rider shows, and how the original re-resolves the seat
  node each frame (`ride-operation.md`, "Where a rider is drawn", and `FUN_005580a0` under "The WALK family").
  Then build both.
- [ ] **Q23. Camera rotation snaps by 45 degrees.** `ParkOrbitCameraMode.cs:196-200`. The 90-degree
  option exists (`GameOptions.NinetyDegreeRotation`) and is read by nothing. Decode the original's step
  and its easing (`park-engine.md`, "The park camera", has the saved and required rotation, not the rate). Build what
  the decode says, driven by `Time.SmoothingFactor`. Alexah: match the original, do not invent.
- [ ] **Q24. Nothing highlights a thing under the mouse.** The idle pointer already follows the hover category over
  ground and path (`Level.IdleOverPath`). Picking is decoded (`park-engine.md`, "Picking is a real ray cast, not a
  grid lookup"): the hit point comes from a ray against the terrain and then object meshes, and the hovered THING is
  taken from the cell, not by hitting its model (`ParkPicking.ThingUnderCursor`). Decode what the hover updater
  `FUN_00486d90` highlights when the hovered thing is an object or a person, then build it.
- [ ] **Q25. The camcorder button should give a crosshair and place the camera where you click.**
  `ParkGadget.cs:233-240` enters the mode at once. The original installs a mouse-interaction mode
  (`FUN_00481a10`, `park-engine.md`, "Camcorder mode — the first-person view"). Its click handler was decoded by Q48
  (`park-engine.md`, "Entering and leaving first person"): cursor `0x13`, a left click on a cell of type 0, 1, 3, 9 or
  30 stands the viewer at the picked point, and leaving puts the saved point of interest and yaw back, where ours keeps
  the walk. It is also Q48's hole (3). Left to decode: the click's own cell tests and its thing branch. Then build.
- [ ] **Q26. Ferry, seaplane and bus are always there.** `ParkFixedItems.cs:154-173` stands all three
  permanently. `ParkPeople.StepArrivals` sizes every load at `Arrival.MinPeople` (1), and `VehicleFor` gives one
  person the bus, so only the bus is ever called. The original creates the vehicle on demand (`FUN_0051a2f0`,
  `park.md`, "Arrivals: who comes, on what, and how often"); the
  headcount score (`FUN_004c8240`) is not decoded. Q68 found the rest of the headcount: `NewParkBonus` is added to
  the score on every call and the sum scaled by 1.2 or 0.8, so even a score of nought brings 3 or 4 to Lost Kingdom.
  Decode the score and the pause between visits, then build create-on-demand, the pauses and the bus / ferry / plane
  ordering.
- [ ] **Q27. Pushing the mouse at the screen edge does not scroll.** The "push scroll" option exists and
  is read by nothing. `ParkOrbitCameraMode.cs:214-228` scrolls from keys only. Decode the camera
  binding table at `0x00748158` (`park-engine.md`, "The park camera"), then build.
- [ ] **Q28. Renaming parks and rides.** The save carries a name per park (`saves.md:91`). No rename
  control exists; signs read fixed names (`ParkFixedItems.cs:452-470`, `ParkObjects.cs:436-448`,
  `LobbyIsland.cs:90-97`). Decode where the original edits the name and what it writes, then build the
  control and make the three sign painters read the save's name.
- [ ] **Q29. Parity with the original's hardware renderer.** `render-states.md:110-111` counts the
  MIN/MAG/MIPFILTER and LOD-bias sites but does not decode their values; no shade-mode row exists; ZFUNC
  is LESSEQUAL there and Less here. The renderer lights per pixel with 16x anisotropic filtering and
  full mipmaps (`3d.shader:52-62`, `Material.cs:93-121`). Decode the values, then match them. Confirm:
  side-by-side screenshots of the same view.
- [ ] **Q30. Waving flags at the bus stop.** Nothing found in code or docs. Decode what the original
  draws at `BusStopA/B` (`park.md:54, 98`), then build.
- [ ] **Q37. The placement terrain rule.** `ParkBuilding.Refusal` counts `PLACEMENT_TERRAIN_RULE` and lets a
  thing stand on types 2, 7 and 30 and on land flagged `0x40`, outside the park. The original allows a
  footprint only over in-park bare ground or path (`FUN_00535600`, `FUN_00535670` refusing `0x40` at
  `0x005357c7`); whether every placement route goes through that verdict (its op-`0x104` branch) is
  still open. When it is built, `Unstamp`'s two declared deviations go: keeping `0x40` where the original
  clears the whole flag word, and giving terrain back its save's record rather than clearing it.
- [ ] **Q38. What building and selling look and sound like.** Counted, not built: the death particle
  (`DESTROY_PARTICLE_EFFECT`, `Info.DestroyParticleEffect`, 75-78, a world effect spread over the
  footprint) and the demolish sound (`DEMOLISH_SOUND`, `0x96`-`0x99` by size). Not yet counted: the
  build side - `Info.CreateParticleEffect` and the `+0xd4` sound zone the script loader makes when an
  item has one (`park.md`, "What selling a thing does to its script"). Needs a world particle pass,
  which nothing here has; the screen pass draws only `OnScreen` templates.
- [ ] **Q40. The place-staff mode's cell rule and its carry preview.** Found by Q6's decode (`park-engine.md`,
  "Putting a candidate down: the type-5 mode"), so this is a build. The original takes a worker only on a
  cell of `mType` 0, 1, 3 or 9, not flagged `0x40`, whose track record after the 12/17 parent redirect is
  not track type 11, 13, 16, 18 or 25; here any cell on the map takes one (`STAFF_PLACEMENT_CELL_RULE`).
  While a candidate is carried the original shows cursor 9 (`c_carry.ani`), hangs a sprite of their kind
  in their costume under the pointer and puts a red square on a cell the click would refuse; here the
  pointer is plain (`STAFF_CARRY_PREVIEW`). Confirm: carry a candidate over a cell the rule refuses, click,
  still in the hand and the red square photographed; then a path cell, hired.
- [ ] **Q43. The number read as a repeat delay is a priority, and the original throttles nothing.** Found by
  Q9's decode (`docs/exe/audio.md`, "How the engine plays an effect: priority, not a repeat delay").
  `SoundCategoryFile.Effect.RepeatDelay` is the effect record's `+0x0c`, which the original reads only as a voice
  priority, and nothing on its play path reads a clock. So `SoundCategory.Play`'s per-effect throttle has no
  counterpart, and neither have the replays it times: the lobby's beds and one-shots, the park's music and
  weather, and SINGLESCREAM. The original's repeating voices come from the flags word instead. There are two kinds:
  - `0x0404` chains, built by Q9 for the screams only (staff 188 and the ambient beds are not);
  - the `0x4|0x2` class, which hands a variation's wait to the mixer (music 2, kids 91, ambient 33). It is undecoded.

  Also owed:
  - the top-N voice pool, keyed by priority and closeness (N is 12, at most 30);
  - weighted variation picks (4 of 63 effects are uneven, and `SoundCategory.Pick` is even);
  - whether parameter 6 is also a level (OpenTPW hears it as the scream's gain);
  - replacing `ReadSamples`' scan with the structural walk.

  Decode the `0x4|0x2` class first. Confirm: capture the lobby's mix and a park's mix, before and after.
- [x] **Q35. The path tool from the interface, and Backspace.** Done 2026-09-22,
  `alexah/117-the-path-tool-from-the-interface`; `docs/exe/park-engine.md` "The path tool" has the decode.
  No button arms it: a click on grass or path does, and anchors in the same click. Backspace pops the run
  list and clears each cell once, which the new `mOverlapCounter` makes remove only what the run laid;
  idle, it deletes the path under the pointer (kept at Alexah's word). Escape disarms without opening the
  menu; Delete is Clear Land, counted. Confirmed in the game, R1-R17 predicted first. The item as written:
  Path laying is still console-only: nothing
  in the UI arms mode 1 (`park-engine.md` has "clicking a path cell or bare ground gives mode 1" without
  the control that does it). Alexah, 2026-09-22, from playing the original: *"Backspace deletes the last
  section of path that was placed, but only in path building mode."* Decode both - which control arms
  mode 1, and the Backspace handler (Backspace reaches the key tables as `0x08`, `park-engine.md`
  "Keyboard bindings") - then build: the path tool's own preview squares and cursor states
  (`PATH_TOOL_PREVIEW`) come with it.
- [ ] **Q51. Rest-area occupancy is unbuilt at both ends.** Found by Q36's decode. Arriving to rest
  (`FUN_00505fe0`) adds one to the rest area's script variable 0 (`VAR_STAFFIN` in every staff room) and sends
  message 15 to the resting-staff list (UI control `0x1e7b`); leaving (`FUN_00506d10`, from the normal end of a rest
  and from a sale) takes the one back. Neither half is built, and all three sites count `REST_AREA_OCCUPANCY`. Decode
  what the staff-room script does with `VAR_STAFFIN`, and whether a resting member of staff is hidden (entering state
  3 frees the sprite). Confirm: a guard resting, `rides` showing the room's variable.
- [ ] **Q55. A member of staff going idle queues no stand.** Found by Q36's review. The original's state-0 setter
  queues animation 3 every time (`FUN_005054d0` case 0, through `FUN_004fa460`: `PUSH 3`, `FUN_004217f0`);
  `Staff.AnimationFor` answers nothing for `Idle`, so a member who stops keeps the cycle they had - a guard put out of
  a sold rest area walks on the spot. Check every state-0 entry the original makes against the kinds' own sprite
  scripts before adding it. Confirm: a guard walking, then idle, `guests` showing the set change, photographed.
- [ ] **Q52. A rider on a thing without flag `0x20` is hidden in the original.** Found by Q36's decode. Admission
  tests the object's flag bit `0x20` (`0x0050212b`) and, without it, destroys the rider's sprite (`0x00502147`);
  OpenTPW never hides a rider. Every visitable thing in Lost Kingdom's save carries the bit, so nothing there shows
  it; a thing bought this session carries none of the unpinned bits (`BOUGHT_OBJECT_FLAG_BITS`). Decode which
  descriptor field sets `0x20`, then build both. Confirm: a guest riding a bought Belly Bounce, `guests`, photographed.
- [ ] **Q54. The `.sam` reader against the original's parser.** Found by Q36's decode (`park-engine.md`, "How a key
  finds its global"). In the original the first bad line ends the file - an unknown key, a bounded value out of
  range, or a negative in a non-negative field - and an array's count is the highest index written plus one, so
  Lost Kingdom's guest type is `rand % 8`. Compare `ParkBalance` with both. Confirm: a test per behaviour against the
  shipped files, and the guest types a loaded park draws from.
- [ ] **Q58. A purchase faces whatever the hand last held. Decode first.** Found by Q39's decode. The original keeps
  one rotation, `DAT_0081d7a4`, and `FUN_0052f200` writes it only for tool 0 (`0x0052f3b6`), so an item bought over a
  carried move faces the moved thing's way; a candidate's or worker's pickup leaves it too. Here a purchase always
  starts at nought, said at `ParkBuilding.CarryingAngle`. Decode what a successful put-down leaves in it (the light
  setter `FUN_0052f580( 0, 0 )`) before building one rotation. Confirm: move a ride turned 90, buy over it, put the
  purchase down, `objects` showing its angle.
- [ ] **Q60. A mechanic put down should look for work at once. Decode first.** Found by Q39's decode. The type-6
  put-down (`FUN_00505ea0`) sets every kind idle but the mechanic, whose claims are cleared (`FUN_004dad20`) and whose
  job search runs there and then (`FUN_004da5b0`); here a mechanic goes idle like the rest, counted as
  `MECHANIC_PUT_DOWN_JOB_SEARCH`. Decode whether idle here reaches the same search a tick later, and what differs.
  Confirm: pick up a mechanic, put them down, `staff` on the next few ticks.
- [ ] **Q61. The park-entry fly-in darkens the screen, and nothing here draws it.** Found by Q41's decode
  (`docs/exe/lobby.md`, "Escape cancels the fly-in"). State 2 sets the render camera's `+0x60` to
  `clamp( (1 - (r - 8) / (SPINRADIUS - 8)) x 65536, 0, 65535 )` (`0x005e052f`), states 0 and 1 set it to 0, and
  `0x005d8cb0` eases `+0x5c` toward it by an eighth of the gap per lobby pass and draws a black rectangle over the
  view with alpha `min( +0x5c >> 8, 255 )` (`0x008bd4e4`, drawn at `0x00576e54`). So the flight fades to black as it
  closes on the gate, and a cancel lifts it. Counted as `LOBBY_FLY_IN_FADE`. The ease is per pass, not per second:
  convert it through `Time.SmoothingFactor` and say so (rule 10). Confirm: `step` into the flight, screenshots at
  three radii with mean brightness falling, then Escape and the brightness back within a second.
- [ ] **Q62. What View the online world does offline. Decode first.** Found by Q41's decode. `IslandPanel_Callback`'s
  case for button `0x1e0e9` (`0x004b8c18`-`0x004b8c2f`) calls the island camera's `+0x20`, its deactivate
  (`0x005e1bd0`, which Ghidra names `IslandLobby_ViewOnlineWorld`), and then the online-world child's `+0x1c`, with no
  online test read yet; `IslandPanel.ViewOnlineWorld` does nothing, saying the original does nothing offline. Decode
  whether a test sits in front, and what the online child shows with no connection. Confirm: the button pressed,
  `windows` and a screenshot.
- [ ] **Q63. Does the engine's idle default play a lobby gate by itself? Decode first.** Found by Q41's decode. The
  lobby loop's `FUN_0044e410( 3 )` calls `FUN_00473c70( model, 0, 8 )` on every model with `[+0x14] != 0` and
  `!(+4 & 0x3000)`, and that replays role M entry 0 whenever channel 0 has finished, unless `(model+4 & 0x8004) != 0`.
  If the lobby's gate instances pass, the original's gates open by themselves - at the start, and again after a
  cancel's M2 - which would change what `LobbyGate` idles on. Decode the lobby instances' `+4` and `+0x14` (they are
  built through `0x005d8870` with flags `0xc0`). Confirm: a test on the flags, and the gate's `state` over 30 s idle.
- [ ] **Q64. Escape over the player slots opens the game menu.** Found by Q42's decode (`docs/exe/lobby.md`, "The
  lobby's keys act on the release"). `FrontEnd_ShowPlayerSlots` hides the lobby's root control (`0x004a65a8`) and gives
  the slots the focus (`0x004a65d6`), and their callback `0x004a5fc0` drops every key, so with the slots up no key acts
  at all: Escape opens no menu, and the slots' own Quit is the way out. A message box closed over the slots moves the
  focus to the hidden root (`0x004862a0`), where keys still reach nothing. Here `FrontEnd.MenuKey` opens the lobby's
  menu over the slots, said at the site. Confirm: nobody playing, Escape let go, `windows` still `PlayerSlots`; a
  screenshot.
- [ ] **Q65. The system table's keys: Ctrl+H on the release, and F8.** Found by Q42's decode (`lobby.md`, "The
  lobby's keys act on the release", its paragraph "The system table's keys, in either scene"; `park-engine.md`, "The original fires its shortcuts on key RELEASE"). The
  window procedure matches the system table `[0x0078718c]` itself on every key, in both scenes, and runs its handlers
  on the release: `P` (pause, `0x0040bf70`), Ctrl+H (Popup Help, `0x0040c5d0`), F8 (a `Scr%05ld.tga` screenshot,
  `0x0040c470` and `0x00550460`) and Ctrl+Shift+Alt+F8 (`0x0040c480`, a flag read only at `0x0054f455` and
  `0x0054fb93`). The player slots switch the tables off (`0x0040cfa0`) and their close back on (`0x0040cf60`). Here
  Ctrl+H toggles on its press (`HelpBar.Update`, said at the site), F8 is not built, and nothing switches the keys off
  under the slots. Decode what the F8 chord's flag does, and where a screenshot may be written without touching the
  game's folder, before building either. Confirm: Ctrl+H held, the help bar unchanged until it is let go; a screenshot.
- [ ] **Q66. A disabled button takes the pointer.** Found by Q42's review. The original's hit test (`0x0065db25`) skips a
  control flagged `0x2` - a disabled button, `0x0065da8d` - with everything under it, so the pointer passes over it to
  whatever is behind: no hover, no help row, no glint. Here `UiButton.TakesMouse` is true whatever `Enabled` is, said at
  the site; the island panel's greyed arrows for an Instant Action player are the case in the lobby, and a press there
  still ends at the panel's outline, so nothing but the hover differs. Before changing the hit test, check every park
  window whose root takes no pointer: a press on a greyed button there would fall through to the world. Confirm: an
  Instant Action player's pointer on a grey arrow, no help row; a screenshot.

## E. Large

- [ ] **Q31. The other eight object windows.** `Level.OpenObjectWindow` opens only a ride's (`UiType 0`). Shops,
  sideshows and the rest stop at `SHOP_WINDOW`, `SIDESHOW_WINDOW` and `FEATURE_WINDOW`, and a staff member at
  `STAFF_WINDOW` (`Level.ClickWorldAt`); a clicked visitor reaches nothing counted. `park-engine.md`, "The
  per-object management screen is nine screens", lists the nine. One window per session, shop first.
- [ ] **Q32. Graphics tiers.** Only `Level.SetupParticles` reads the detail files (`low.sam`, `med.sam`, `high.sam`),
  and only `GameOptions.PARTICLEDENSITY` from them; nothing reads their `GraphicalOptions.*` keys (texture quality and
  filtering, sky, shadows, fog, mipmaps, view distance). The detail-file loader is `0x00423bc0` (`OptionsScreen`'s
  restart note). Decode what it does with each key - the lobby plan's item 11 names three: the `stexture` set,
  `SKYQUALITY` and the particle low-detail byte - then build low / medium / high.
- [ ] **Q33. UI scale.** The UI has one fixed virtual size (`UiControl.cs:59`). Add Auto / small / medium
  / large in the dead Video Card row (`OptionsScreen.cs:71-72`).
- [ ] **Q34. README rewrite, then pictures.** Newcomer first: what it is, what runs, how to build, how to
  run. Technical detail moves to `docs/`. Line 117 is already stale. Animated pictures need a capture
  tool; none exists in the repo, so that is its own item afterwards.
- [ ] **Q72. Values from data, not constants (the 2026-09-12 review's Phase B).** The lobby's four island names
  from `THEMENAMES.str` through the reader the gate already uses (`ParkFixedItems.ParkDisplayName`) in place of
  `LobbyIsland.DisplayNames`; the `ISLAND()` directory and model names in place of `themeName[0..3]` (`LobbyIsland`,
  `LobbyGate`); and `DUCKINGLEVEL` from `sound.sam` for the advisor's 0.38 duck - but that mapping to `0x00785914` is
  inferred, not proven (`Advisor`'s note), so decode the global's writer first. For the names and the `ISLAND()`
  fields the data equals the constant, so "nothing looks different" is the regression check (`docs/exe/lobby.md`,
  "Park names and the locale tables"). Confirm: a lobby screenshot before and after, identical.
- [ ] **Q73. Cache pipelines and assets by key (the rest of the review's Phase D).** One pipeline per material
  (`Material`) where one per (shader, flags, output) would do, and texture and shader caches that scan `Asset.All`
  linearly (`Texture.Cache`, `Shader.Cache`). The shared blank texture landed (`bb897f8`); measure the load before
  and after, as that step did, and do not re-take its measurement.
- [ ] **Q74. Lift the lobby out of `Level`, or record it as dropped (the review's Phase E). Alexah's call.** Park
  entry shipped without it: `Level` builds the lobby (`SetupEntities`, islands hard-coded, an unread `Global`) beside
  the park (`SetupParkEntities`). Ask Alexah before starting.

## F. Then

Back to `docs/PLAYER-GAPS.md`: gap 5 (happiness gauge), gap 4 (advisor in a park), gap 7 (saving a
park, decode first). Also litter and the day ending, whose deferral reasons expired
(`docs/REVIEW-2026-09-21.md` section 6).

## G. The lobby plan's open items

From the lobby plan Alexah agreed on 2026-09-12, "Finishing the Lobby" (its artifacts are listed in
`docs/history/README.md`). Alexah asked that its report be kept in use: when an item here lands or the order changes,
update it with the Artifact tool's `url` (https://claude.ai/code/artifact/48cf6fc2-0874-404c-b7bf-a05fe4b16470), never
a second copy. Items 1-4,
6-8 and the gate half of 5 landed; item 11 is Q32. Cut from the lobby that day and still cut: the intro movies and
splash, the three online UI trees, cones, reverb and Doppler, the message box's third button, and the original's full
state machine.

- [ ] **Q76. The isle's clips loop; the original picks one at random when idle (plan item 5).** The camera update's
  tail loop (`0x005e11f7`) starts the isle's M1 or M2 at random whenever it is idle (`docs/exe/lobby.md`, "Not
  sound"); here the isle's clips loop. The gate half landed (`3eed966`, `9b0ebab`, Q41); whether the gate also idles
  by itself is Q63. Confirm: `sound`/`state` over a minute of lobby, and a capture of the isle between clips.
- [ ] **Q77. The advisor's 90-second idle nag in the lobby (plan item 10).** Responses 394/395 (`0x18a`/`0x18b`,
  armed at `0x005e184c`, `docs/exe/ui.md`); sample 470 is never heard today. When it arms and re-arms wants one
  sitting with the original first. Confirm: the line heard after 90 idle seconds, by capture.
- [ ] **Q78. The lobby's sea from `lobby/Terrain/base.md2` (plan item 9). Decode first.** The lobby has a sea: a
  `Water` entity (`Level`) that `Sky` seals the horizon against. The item is re-sourcing it from `base.md2` through the
  park terrain loader - but `Sky`'s own note says the original's lobby has no sea below its islands, which contradicts
  the premise. Settle whether the original draws `base.md2`'s sea before building anything.
- [ ] **Q79. Robustness (plan item 12, the rest).** Atomic save writes exist (`BaseFileSystem.WriteAllBytes`) and
  input is dropped while unfocused (Q42). Open: top-level error containment (`Program.Main`/`Game.Run` catch nothing),
  and easing the render rate and music while the window is unfocused.
- [ ] **Q80. Two tests the lobby audit asked for (plan item 13, the rest).** A save round trip against a real
  game-written file (it wants a sitting with the original to make one), and `LobbyScript` over the four shipped island
  scripts. Sound categories are covered by `SoundCategoryTests`.
