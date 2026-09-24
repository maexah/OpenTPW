# Work queue

Written 2026-09-22 against `main` at `9b0ebab`. Line numbers are from that commit.

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
`docs/REVIEW-2026-09-22.md` for Q2 and Q8 to Q12. The patch files are in `~/Downloads/opentpw/`.

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
  so four runs reported an identical clean 894 and read exactly like four survivals. See `VERIFYING.md`.
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
- [ ] **Q42. The lobby's keys act on the press, and Enter does not enter the park.** Found by Q8's decode
  (`docs/exe/lobby.md`, "The island keys wait for the fly-in"). The original's lobby takes its keys on
  release (UI message `0x1000b`): the island camera's `0x005e2310` maps cursor Left and Right to previous and
  next, and Enter to Enter this park (`+0x40`, with its key test). Here `IslandPanel.Update` moves on the
  press (`Input.KeysPressed`, a held key's repeats included) and nothing takes Enter. Escape too: the lobby
  cancels a fly-in or opens its menu on the release (`0x005e41c7`), and `FrontEnd.MenuKey` does both on the press,
  said at the site (Q41). Open in the decode:
  whether a left button press on the lobby view (`0x10005`, also mapped to `+0x40`) reaches the camera,
  which depends on the root control's hit test. Confirm: hold Right, one island per release; Enter on an
  affordable island starts the fly-in; log lines and a screenshot.

- [ ] **Q44. A left park stays in memory through the lobby.** Found by Q10's sweep (8 agents: four
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

- [ ] **Q45. The VM charges `CRIT_LOCK` against the budget; the original does not.** Found by Q11's decode.
  `FUN_005516b0` reads the critical flag after the instruction has run (`0x00551724`), so `CRIT_LOCK` is free and a
  lock reached with one unit of budget left still runs its whole section in that turn. `RideScript.Step` charges
  before `Execute`, by the flag as it stood: there the turn ends locked, `Turn` clears the flag, and the section runs
  next turn unlocked and budgeted. Reachable, by static walk, at 68 of the 150 locks, 18 of them in Lost Kingdom;
  not yet seen in a run. `RideScriptSchedulerTests.ACriticalSectionDoesNotOutliveItsTurn` pins 3 where the engine
  gives 4. In the same loop, a time slice of nought or less skips the turn in the original, where `Turn` floors it to
  1 without saying so; every shipped file says 50, so that is dead by CONTENT and wants only a comment. Confirm: the
  test at 4, and in the jungle a section at one of those sites run whole in one turn, by the `rides` figure against
  its path length.
- [ ] **Q47. Two more hollow tests.** The other two of `docs/REVIEW-2026-09-22.md` section 5, re-measured by Q12's
  review at its tip. `ParkGuestPlacementTests.AGuestWhoHasStoppedIsDrawnInOnePlace` stamps by hand, so deleting
  `peep.Navigator.StampPrevious()` from `ParkPeople.OnUpdate`'s peep loop, or moving it into `PeepWalk.Step`, leaves
  every test green. `ParkScreamChainTests` now pins the chain itself (a chain that never replays fails three), but
  deleting `_screams.Pump( Time.Now )` from `ParkAudio.OnUpdate`, or looping each child, leaves every test green.
  Q12's two patterns reach both: a stand-in level with a real `ParkState`, and `Audio.Ready` set for a test. No game run.
- [ ] **Q48. Three holes in the camcorder's sweep. Decode first.** Found by Q12's mutation hunt, with a probe, not
  yet in the game. (1) At exactly 45 degrees - reachable, since the rotate keys keep the orbit's yaw at multiples of
  pi/4 and entering the camcorder copies it - the fraction that reaches the nearer boundary carries the other axis
  onto its own, `floor` puts the viewer in the next cell, and that side is never asked: 16,034 leaking frames in
  5,684 of 254,016 probe walks, e.g. from (249.999, 30.001) at 5pi/4 through the east side of (24,3). (2) A step
  whose reach is exactly 1 is taken whole without asking, and a positive-going one lands in the refused cell.
  (3) `Step` clamps to 1..1280, and 1280 is cell 128, off the map, where every crossing is refused: entering the
  camcorder past the east edge traps the viewer there. Decode what `FUN_0042b1c0` does in each case first - the
  original may share (1) and (2) - then build. Confirm: each case walked in the game, photographed, with `camcorder`.
- [ ] **Q50. Every other way out of a queue costs `MediumHappinessChange` too.** Found by Q36's decode.
  `FUN_005012f0` docks it unconditionally (`0x00501359`) and has seven callers; Q36 built only the sale's. The other
  six unlink the guest first (`FUN_004ddd20`): the queue edited or shortened under them (`FUN_00501390`), a ride
  closing mid-admission (`FUN_004e0450`), "Couldn't get to my place in the queue" (`FUN_004ffbc0`), bored and leaving
  (`FUN_004ffff0`), and two in state 13 (`FUN_005006b0`: too expensive, which docks twice, and "Couldn't rejoin FOQ").
  It also writes event 6 and plays the kids' `0x80` when the guest's id is a multiple of eight (`0x0050133d`); today
  only the sale plays it, from `ParkPeople.ThingRemoved`, so build it with the dock. Check which of those OpenTPW
  builds and whether each docks. Confirm: a guest who leaves a queue that way, `peeps` happiness before and after, and
  the `put off` log line for an id that is a multiple of eight.
- [ ] **Q53. A put-off queuer on cleared ground can leave only by going home. Decode first.** Found by Q36's game
  run: two queuers put off the sold Belly Bounce stood in Deciding for twelve seconds on cells the sale cleared, where
  no neighbour connects and `SetRandomDest` has no candidate; a probe's queuer left only as its day ran out. Every
  failed wander restamps the 30-sweep thinking gap, as the original's does (`FUN_004fec90`, `0x004ff3f4`..`0x004ff400`),
  and `mGameTick` counts thing sweeps (`0x00516394`, called inside the every-8th block), so the gap matches too. Two
  differences are known: `Decide` also restamps after a SUCCESSFUL wander, which the original does not
  (`0x004ff3d6` returns first), and the original docks `SmallHappinessChange` when the chooser finds nothing
  (`0x004ff492`). Decode whether the original strands these guests as well - `FUN_004f9490`'s other arms, the stranded
  stamp and its "?" - then build what differs. Confirm: the Q36 staging, `peeps` for the put-off queuers.
- [ ] **Q56. A right press over a panel arms the quick click.** Found by Q39's decode. The original posts a press to
  the hovered window (`FUN_00658af1`), so only a press on the park view reaches `Park_MouseMessageProc` and arms the
  click; the park still gets its own release wherever the pointer has gone. Here `Level.RightButton` arms on a press
  anywhere, said at the site, because `WindowStack.PointerTaken` is worked out for the left button only. Confirm:
  something in the hand, a quick right click over the gadget, `hand` still full; one over the park, empty.
- [ ] **Q57. The park's Escape acts on the press.** Found by Q39's decode. The original runs every game-table key on
  the release (message `0x1000b`; the key-down runs nothing, `FUN_0040c900`), and so lets go of the hand, closes the
  locator and opens the menu on the release. `WindowStack` hands Escape to `ParkFrontEnd.MenuKey` on the press, said
  at the site. The lobby's half is Q42. Confirm: hold Escape with something in the hand - nothing until the release.
- [ ] **Q59. A right double click in first person does not leave it.** Found by Q39's decode. With RMB cancel on, the
  viewfinder layer's handler answers a right double click by leaving first person as Escape does (`0x00488aa1`..
  `0x00488ad6`). Nothing here reads a right click in first person. Confirm: camcorder, a double right click, `camera`
  back to orbit, photographed.

## B. Docs and comments

- [ ] **Q13. STATUS diet, and track the loose files.** `docs/STATUS.md` is 1,163 lines; "Recent" alone is
  over 1,000. Cut to under 120: "Recent" is five entries of ten lines, everything older is the git log.
  Commit this `QUEUE.md` under `docs/`. Move `docs/CLEANUP-PLAN.md` (every item closed) into
  `docs/history/` and commit it there. Commit `docs/REVIEW-2026-09-21.md` and
  `docs/REVIEW-2026-09-22.md`. No game run.
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
- [ ] **Q14. Comment sweep of the 24 cleanup commits.** Replace history-voice comments with what the
  code does now: `IslandPanel.cs:319-320`, `LobbyGate.cs:46-47`, `LobbyCameraMode.cs:88, 96, 103`,
  `ParkCamcorderCameraMode.cs:396, 526-527`, `ParkThingStates.cs:54-55`, `ParkScriptStates.cs:33-34`,
  `AudioListener.cs:67-83` (also a `<para>` inside a `<para>`), `GameClock.cs:44-45`,
  `Texture.Cache.cs:29, 50-51`, `ParkPeople.cs:244` (a test count in a code comment), `RideScript.cs:1310`.
  Tab fix at `DebugConsole.cs:396-399`. `WalkSpeed / 60f` at `ParkCamcorderCameraMode.cs:439` uses
  `Time.Delta`. No game run.

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
  `Input.Right` into `Step`. `park-engine.md:598-620` decodes the sweep by two velocities but not the
  keys. Remove the strafe. Confirm: press the strafe key, the stand position in the `camcorder` census
  does not move sideways, screenshot.
- [ ] **Q18. The advisor draws in front of the dimmed screen.** `Level.cs:915-944` draws the HUD, then
  the overlay pass with the advisor. Windows that do not pause do not hide him (`ui.md:120`). Draw the
  dimmer over him, or hide him for those windows too. Confirm: screenshot with the buy screen open.
- [ ] **Q19. VSync and a frame limiter.** `Renderer.cs:345, 357` hard-code VSync on. `Display.cs` already
  carries each mode's refresh rate. Add an Options row: VSync mode, and a frame limit from 30 up to
  Unlimited, default the monitor's refresh rate on first launch. Confirm: screenshot the options row;
  log the measured frame time at two settings.
- [ ] **Q20. No bubbles on the drinks shop, no chimney smoke on the staff room.** The scripts ask for the
  effects and `RideEffects.cs:136-181` records the request; nothing consumes the records.
  `ParticleSystem.Spawn` exists (`ParticleSystem.cs:127`); `ParLib.cs` names `Bubbles = 58`,
  `Smoke = 2`, `SmallSmoke = 13`; `park.md:594-629` decodes the node and the per-tick push. Join the two
  halves. Confirm: screenshot the drinks shop with bubbles and the staff room with a staff member inside
  and smoke rising; log lines for both spawns.
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
  node each frame (`ride-operation.md:466-470, 524-533`). Then build both.
- [ ] **Q23. Camera rotation snaps by 45 degrees.** `ParkOrbitCameraMode.cs:196-200`. The 90-degree
  option exists (`GameOptions.NinetyDegreeRotation`) and is read by nothing. Decode the original's step
  and its easing (`park-engine.md:254` has the saved and required rotation, not the rate). Build what
  the decode says, driven by `Time.SmoothingFactor`. Alexah: match the original, do not invent.
- [ ] **Q24. Nothing highlights under the mouse.** No hover code exists. Decode what the original
  highlights when the cursor is over a thing, and whether it picks by cell or by mesh. Then build.
- [ ] **Q25. The camcorder button should give a crosshair and place the camera where you click.**
  `ParkGadget.cs:233-240` enters the mode at once. The original installs a mouse-interaction mode
  (`FUN_00481a10`, `park-engine.md:570-590`); its click handler is not decoded. Decode it, then build.
- [ ] **Q26. Ferry, seaplane and bus are always there.** `ParkFixedItems.cs:154-173` stands all three
  permanently. `ParkPeople.VehicleFor` (`:635`) floors the headcount at 1, so only the bus is ever
  called. The original creates the vehicle on demand (`FUN_0051a2f0`, `park.md:800-835`); the
  headcount score (`FUN_004c8240`) is not decoded. Decode the score and the pause between visits, then
  build create-on-demand, the pauses and the bus / ferry / plane ordering.
- [ ] **Q27. Pushing the mouse at the screen edge does not scroll.** The "push scroll" option exists and
  is read by nothing. `ParkOrbitCameraMode.cs:214-228` scrolls from keys only. Decode the camera
  binding table at `0x00748158` (`park-engine.md:236-257`), then build.
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

## E. Large

- [ ] **Q31. The other eight object windows.** Only rides (`UiType 0`) open a window; shops, sideshows
  and features stop at `SHOP_WINDOW`, `SIDESHOW_WINDOW`, `FEATURE_WINDOW` (`Level.cs:745-757`).
  `park-engine.md:866-880` lists the nine classes. One window per session, shop first.
- [ ] **Q32. Graphics tiers.** Nothing reads `high.sam`. The detail-file loader is `0x00423bc0`
  (`OptionsScreen.cs:52`). Decode it, then build low / medium / high.
- [ ] **Q33. UI scale.** The UI has one fixed virtual size (`UiControl.cs:59`). Add Auto / small / medium
  / large in the dead Video Card row (`OptionsScreen.cs:71-72`).
- [ ] **Q34. README rewrite, then pictures.** Newcomer first: what it is, what runs, how to build, how to
  run. Technical detail moves to `docs/`. Line 117 is already stale. Animated pictures need a capture
  tool; none exists in the repo, so that is its own item afterwards.

## F. Then

Back to `docs/PLAYER-GAPS.md`: gap 5 (happiness gauge), gap 4 (advisor in a park), gap 7 (saving a
park, decode first). Also litter and the day ending, whose deferral reasons expired
(`docs/REVIEW-2026-09-21.md` section 6).
