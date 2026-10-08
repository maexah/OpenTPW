# Work queue: finished items

Every ticked item of `../QUEUE.md`, moved here verbatim, in queue order and under its section heading. **Nothing
here is current**: each entry is frozen at the commit that ticked it. Grep it by Q-number; never read it whole.

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
  once, in `docs/exe/park-engine.md`** under "Where a built thing's entry and exit cells come from":
  the three formulas at `0x004db2da`..`0x004db36b`, and `FUN_004d9cc0` named as
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
  **That queue could not be laid over path was an INSTRUMENT limit, not a game rule.** The console's `queue` always passes
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
    at each site: `Walk` reading the keys, `Update` placing the lobby camera, the panel's Enter (since pinned by
    Q41's and Q42's tests), the texture's GPU handles, the mixer's fade, and a threshold moved by less than one
    frame. 979 tests with the game, 447 ran and 532 skipped without, 123 warnings.
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
    30-sweep gap, where the original's sends them to the nearest path (Q53). A probe run's put-off queuer left by going home.
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
    here armed the click until Q56 (`b178d5e`); on the gadget's body it still does (Q113). **Found:** Q56 to Q60.
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
    five other ways out. `PeepBehaviour.QueueTurn`'s drift is the original's 32-bit unsigned compare. `peeps` prints each
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
- [x] **Q69. Seven unbuilt paths are not counted: five now are, and two are not reached.** Done 2026-09-25,
  `alexah/156-count-seven-unbuilt-paths`. Mapped by a read-only workflow (one agent a path, a skeptic over all seven),
  and every load-bearing address re-read by hand.
  - **Counted, where the original takes each:** `LOBBY_ADVISOR_IDLE_REPEAT` in `LobbyCameraMode.Update`, every frame a
    player is picked (`0x005e184c` is in the island camera's own update; `ui.md`, "The lobby's idle repeat", new);
    `LOBBY_ISLE_RANDOM_CLIP` in `LobbyIsland.OnUpdate`, once an isle as its first draw; `BOOT_SPLASH`,
    `BOOT_LEGAL_SCREEN`, `INTRO_MOVIE_BULLFROG` and `INTRO_MOVIE_PARK` in `Game.Run`, once a run; `RESEARCH_BUTTON` on
    each click (`ParkGadget.NotYet`, which had one caller, is gone); `FIRST_PERSON_WALK_INTO_RIDE` after every pass of
    the camcorder's sweep that ends on an entrance whose owner's item has `UsageInfo.CannotRide` nought
    (`FUN_0042a340`, `0x0042c587`), which needed the key read (`ItemDescriptionFile.CannotRide`; FileFormats
    `sam.md`): every theme's rides category sets 0, its shops, sideshows and features 1, its upgrades nothing, and
    none of the 274 items overrides it.
  - **Not reached, so not counted, and said so:** the advisor's glints are UI particles that only the park advisor's
    golden-ticket lines start (gesture rows 1 and 13, `scenes.md`), never the lobby, even in the original; the stale
    hover cannot happen here, because `WindowStack.OnUpdate` hit-tests every frame, which is a deviation said at the site
    and in `lobby.md`, where "or closes" is refuted (every close refreshes the hover; `FUN_006589f9` has ten callers);
    and the `welcome_<lang>` overlay is dead by content (no Init folder ships one).
  - **Confirmed in the game** (`q69confirm.py`, silent; every PREDICT line written first). Research and the camcorder
    are the park's, so the lobby session goes on into it. Boot: 13 frames grabbed and 5 kept, the first black before
    the first present; the other four are the bar screen, alike pixel for pixel above the bar, with no splash and no
    legal strip. Bare lobby: `unimplemented 5` - 4x
    `LOBBY_ISLE_RANDOM_CLIP` and the four boot names 1x, the same 10 s later. With a player made at the slots:
    `LOBBY_ADVISOR_IDLE_REPEAT` grew 1440 in 10.01 s at 143.9 fps (1440 predicted), and froze at 1883 once the park
    was up. Research clicked three times with the real pointer: 3x, the help row 472 lit and nothing opened
    (photographed). Paused in first person: the Staff Room's entrance walk added none; the Belly Bounce's queue head
    to its entrance (52,23) ended at (525.000,239.999) and counted 54, as predicted (photographed on the queue head and
    in the entrance against its body). A first try read 92: a `step` before the walk let the view steer toward the
    pointer, left on the gadget, to yaw 0.36. The control, main's build, the same session: no Q69 name in any census,
    the same walk to the same point. `save/` unchanged in all three. An isle's draw and the advisor's arm have nothing
    to photograph: those shots show the lobby each count was read in.
  - **Tests:** `LobbyCountedGapsTests` (two), `ParkGadgetTests.EachClickOnResearchIsCounted`,
    `ParkCamcorderWalkTests.APassEndingOnARideIsCounted`, `WalkingIntoTheBellyBouncesEntranceIsCounted` (54, and only
    (52,23) rides) and `OnlyRidesCanBeRidden` (all four themes). Twelve bugs put back (`q69mutate.py`), each red. The
    boot's four have no test (`Game.Run` needs a window); the control run is their put-back.
  - **Reviewed** by a read-only workflow (four lenses, a skeptic on each): 21 findings, ten distinct ones upheld and all
    acted on (a stale `PLAYER-GAPS.md` line, the butterflies' sentence, `NewPlayerDialog_Open`'s address, how often the
    walk counts, the boot frames' claim, the upgrades in `sam.md`, the key's values left to FileFormats, a test summary,
    a `STATUS.md` line, a doubled sentence); four refused.
  - **Found:** Q139 (five more reached paths that only log, and the boot's unheard sound), Q140 (the camcorder walks
    onto entrances the original shuts), Q141 (golden tickets, which the glints wait on).

  The item as written: Found by the 2026-09-24 staleness audit.
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
- [x] **Q70. `BFSTReader` parses every string table twice: now once.** Done 2026-09-25,
  `alexah/157-parse-each-string-table-once`.
  - **Built:** `BFSTReader.ReadFromStream` takes the bytes and parses nothing; `StringFile`'s one `ReadFile()` is the
    parse. `ReadFile` logs `String table: <n> strings in <bytes> bytes` once a parse, which is both the test's counter
    and the game's log line; with no logger set (the `strdump` harness) it logs nothing. `Entity.cs`'s
    `using System.Reflection` is gone; the `Entity.All` remark beside it is Q14's.
  - **Confirmed in the game** (`q70confirm.py`, silent; every PREDICT line written first), though the item asked for no
    run. Fix: the boot into the lobby logs 2 lines, UITEXT (474 strings in 17,076 bytes) and UIHELPTEXT (589 in 33,032),
    once each; `park jungle` logs 28, as predicted: a name table for each of the pool's 22 candidates (`Standard.sam`'s
    5+5+5+5+2), STAFF_TYPES 5 times for its summary line, THEMENAMES once for the gate's sign; opening the hire screen
    logs none. The control, the discarded `ReadFile()` put back: 4, 56 and 0, every table twice. Photographed: the
    lobby's four "Create New Player" and "Quit Game", the gate's "Lost Kingdom" and the help bar's "Left-click to extend
    this path", and the hire screen's five cleaners by name, the same five in the control (175 pixels of the list
    differ, all inside the pointer's 30 by 28 box; the names' rows are identical). `save/` unchanged in both runs.
  - **Tests:** `StringFileTests` (two): a made table of two empty strings, which opens no character table and needs no
    game, and UITEXT.str (474 strings, row 200 "Park name"). Each reads 2 with the bug put back and 0 with the line
    taken out.
  - **Reviewed** by a read-only workflow (three lenses, a skeptic on each, a completeness critic): ten findings, five
    distinct ones upheld and acted on (the logger made optional, which a `hud.md` recipe also needed; a test comment's
    wrong attribution; the pixel count's method; Q142's confirm; the string length below, with its two stale notes).
    The reader's copy of the layout, refused by its skeptic and raised again by the critic, is left to Q143.
  - **Found:** the BFST header's second word is a number from 1000 to 1020, one per table and the same in both language
    folders (all 42 shipped files; FileFormats `strings.md`, on `docs/format-corrections`). Q142, and Q143: a string's
    length is more than its one byte.

  The item as written: From the 2026-09-12 review (Phase A, the one step of
  it not landed). `BFSTReader.ReadFromStream` calls `ReadFile()` and throws the result away (`BFSTReader.cs`) before
  `StringFile`'s constructor calls it again - delete the first. `World/Entity/Entity.cs`'s `using System.Reflection`,
  from the same step, is now unused. No game run; a test that one read happens.
- [x] **Q71. Two guards that do not guard: now both guard.** Done 2026-09-25, `alexah/158-two-guards-that-guard`.
  - **Built:** `Rotation.From` takes any angle as it is. Its three clamps to 180, applied after the angles became
    radians, are gone: a whole turn more or less is the same rotation, and the same clamp in degrees would make 270 a
    half turn. Only tests call it, and nothing its `Vector3` overload, so it is labelled dead by CODE. `UiFonts.Get`
    looks a slot up through `FileName`, bounded by the length of the set it indexes. The original's `0x00485a70`
    bounds every set by one unsigned compare with 13, the stride of its one flat table (`lobby.md`, "Meshes and
    fonts"), and every shipped set is 13 long, so the two agree on every slot. `Get` logs `UI: font <slot> of set
    <set> is <file|none>` once per set and slot.
  - **Confirmed in the game** (`q71confirm.py`, silent; every PREDICT line written first), though the item asked for no
    run. Fix: the lobby at 1280x720 logs 1 line, font 5 of set 2 (TITLEBIG). The console's `size` takes the window to
    1024x768, 640x480 and 512x384, and each logs its one line, font 5 of set 3, 1 and 0 (TITLEBIG, TITLEMED,
    TITLESMALL); back at 1280x720, none. `park jungle` logs fonts 1, 2 and 3 of set 2 (CASHBIG, SESHBIG, DATEBIG, the
    gadget's balance, count and date), and 7 (GAME9, the help bar), predicted on the `pointer` reply's help row 442.
    `hirescreen` logs font 6 (GAME8). That is 9 lines, none answered "none". The control, the bound put back on the
    first set: the same 9, as predicted, since every set is 13 long; the hire screen's panel and the gadget are
    identical to the pixel. Photographed: the player slots' lettering in all four sets, and the hire screen's title,
    rows and gadget. `save/` unchanged in both runs. Neither fix changes anything the game shows; this is the proof.
  - **Tests:** `RotationTests.FromTakesAnyAngle` (on each axis, a quarter turn against the same with thirty turns more,
    and 270 degrees against -90) and `UiFontsTests` (a made table whose second set is shorter). Each failed before the
    fix, and goes red with the radian clamp put back, with a clamp in degrees, and with the bound on the first set.
  - **Reviewed** by a read-only workflow (three lenses, a skeptic on each finding, a completeness critic): two
    findings. Upheld and acted on: the dead-by-CODE remark said nothing calls `From`, where the new test does. Refuted:
    that no test pins `Get`'s call to `FileName`; on the shipped table no test can tell the old line from the new.

  The item as written: From the 2026-09-12 review. `Rotation.From( pitch, yaw, roll )` turns
  degrees into radians and then clamps them to -180..180, degree limits; `UiFonts.Get` checks `slot` against
  `Sets[0].Length` and then indexes `Sets[SetIndex]`. Each reads as protective and is not. Fix each with a test that
  fails first. No game run.
- [x] **Q83. The VM's stack errors, and `HUSH`'s result register: the decode.** Done 2026-09-28,
  `alexah/167-decode-the-vm-stack-errors`. Decode only; the build is Q83b. `park.md`, "The two stacks" and "Arithmetic,
  the destination rule and the result register"; FileFormats `saves.md` (the script module's block 1 and struct dwords
  16, 17, 18 and 21, on `docs/save-module-chain`) and `instructions.md` (`JSR` to `HOP`, on `docs/rsse-instruction-set`).
  - **The engine, read by hand.** A `JSR` with no stack or no room logs, parks and **jumps anyway** (`0x00553a24`): with
    no stack its `RETURN` ends the script, with a full one that `RETURN` pops the enclosing frame and returns one level
    too far. A `RETURN` with no frame ends the script. `PUSH` and `HUSH` write the result register (`0x00553c89`,
    `0x00553d95`); a heap error changes nothing. The two stacks never check each other: `HOP`'s `+0x40 > +0x54` can
    never be true, so it is no collision guard. The save reader restores both indices, the register and the array.
  - **Nothing shipped reaches a stack or heap error, and no branch reads what `HUSH` or `HOP` left.** All 308 `.RSE`:
    `PUSH`/`POP` unused; every `JSR` a label, the deepest call 1, every `HUSH`/`HOP` at depth 0; each `HUSH` rider's
    capacity clamped to a `MaxCapacity` that fits its stack (`FUN_004dd7f0`); every path from a `HUSH` or `HOP` to a
    branch passes a writer. The shipped save holds no open frame, no heap value, and no saved register a branch reads.
  - **Where OpenTPW parts**, all Q83b and none of it on a shipped path: dead by CONTENT, `PushCall`, `PopCall`, the
    error arms of `Call`, `Return` and `PopValue`, a literal-destination `COPY`; inert, `PushValue`'s extra guard;
    reached and masked, `PushValue`'s and `WALKON`'s result register, untagged frames, and a load that restores no
    index, array or register.
  - **Docs corrected:** `park.md`'s "writes `+0x48` unconditionally" (`ADD`, `COPY`, `TEST`, `CMP`, `GETANIM_CH`,
    the child/parent getters, `FORCEUNLIMBO` and a heap error do not), "`ADD` does the same inline", "a collision
    guard", and the 17 literal destinations, ten of which are the register used on purpose, not artefacts; the
    FileFormats `JSR` "is refused", `HUSH` "the engine checks the two do not meet", `HOP` "ignores the instruction".
  - **Measured in the game** (a throwaway instrumented build, never committed: per script, calls and returns, the
    deepest call, `HUSH`es and `HOP`s, the deepest heap, and a count of each divergent arm; `q83confirm.py`,
    `q83confirm2.py`, `q83why.py`, `q83sell.py`, silent, jungle), predicted first. The shipped park: the Belly Bounce's script alone
    has a stack (3), its calls one deep, no heap used, every count nought. Then an Inca Totem and an Aztec Mayhem bought
    and queued to the path; no guest chose either in 10 to 15 minutes (`why`: 26 of 26, then 43 of 43, aimed at the
    Belly Bounce; Q165), so the Belly Bounce was sold, as a player may. The Totem then took 15 riders and let off 15,
    its heap never deeper than 6, its capacity, its 17 calls all one deep; the Aztec Mayhem took one; every divergent
    arm counted nought in every script, a branch after a `HUSH` included. Photographed with the census read while
    paused: the Totem with one rider aboard, the Aztec Mayhem with its rider at the door. `save/` unchanged in all six
    runs.
  - **No test was added**: nothing was built. Q83b's tests are the build's.
  - **Reviewed** by read-only adversarial agents, six slices, three of which a crash cut off and a second session re-ran:
    23 verdicts, 15 upheld, 8 amended, none refuted; B2, B4 and B7 re-derived inside other slices. The amendments and
    the misses (the full-stack `RETURN`, `COPY`'s death, `GETANIM_CH`, `WALKON`, both directions of the collision)
    were re-read in Ghidra and are in the page.
  - **Found:** Q83b and Q165.

  The item as written: Found by Q45's handler sweep, not yet measured as reached. The engine's `RETURN` with no frame
  parks the script (`0x00553a63`); `RideScript.Return` carries on. Its `JSR` on a full stack logs, parks, then jumps anyway with no return address
  (`0x00553a24`), and with a non-label operand pushes and does nothing; `Call` stops the script for both. `PUSH` and
  `POP` errors park (0 uses). **Engine `HUSH` also writes the result register with the value it pushed** (`0x00553d95`,
  `MOV [EBP+0x48],EDX`, read first-hand), and `PUSH` too (`0x00553c89`); `PushValue` does not, and `HUSH` has 39 uses:
  find whether any shipped branch reads the register after one.
- [x] **Q83b. The VM's stacks and result register, made the engine's: the build.** Done 2026-09-28,
  `alexah/168-make-the-vm-stacks-the-engines`. `park.md`, "The two stacks" ("OpenTPW builds all of this").
  - **Built** (`RideScript`). `Call` pushes the next word tagged `0x20000000`; with no stack or no room it parks and
    jumps anyway, and a non-label operand pushes and carries on, or stays parked. `Return` with no frame ends the
    script, and drops a popped word without the tag and carries on. `PushCall` writes `Result` either way and parks
    with no room; `PopCall` parks on an empty stack after its store. `PushValue` writes `Result` and is bounded by the
    stack alone; `PopValue` on an empty heap writes nothing. `ADD` and `COPY` test the destination first, and a
    literal-destination `COPY` leaves the position on its source for the next dispatch to refuse. `WALKON` leaves
    `Result` alone (`WalkOn` is void). `GETANIM_CH` with no model stores the register as it stands. Both errors log
    once per script. The class summary's destination bullet says what each does.
  - **The restore.** `ParkScriptStates` reads dwords 16, 17 and 18 and block 1 (`SavedScript.CallIndex`, `HeapIndex`,
    `Result`, `Stack`); `ParkRides.Resume` hands them to `RideScript.RestoreStacks` once `ResumeAt` has taken the
    counter, the saved block's length becoming the stack's size. One departure: `HOP` refuses a heap index past the
    stack, where the engine reads past the array; only a save could hold one.
  - **Measured first**: `GETANIM_CH` never runs with no model (`q83bgetanim.py`, jungle: 30 runs, all on the Jungle
    Spray, each with its model and channel; photographed with a guest in its middle lane). All 15 shipped name a
    literal destination.
  - **Tests**: 18 new (`RideScriptStackTests` 16, one each in `ParkScriptStateTests` and `ParkRidesTests`), and
    `RideScriptChannelTests`' no-model test rewritten. The first 17 were red on the old VM with only the new members
    stubbed, each for its own reason; 33 put-the-bug-back mutations, one per arm, bound and field, were each red.
  - **Confirmed in the game** (a throwaway instrument, `q83b-instrument.py` and `q83bsell.py`, silent, jungle),
    predicted first. As loaded: every error count nought, the fountain's register **3033** as saved (its loop never
    writes it) and the Belly Bounce's top slot **`20000018`**, the save's tagged frame; the old VM, run as a control,
    read 0 and `00000018`. Played, with a Totem and an Aztec Mayhem bought and queued and the Belly Bounce sold, in two
    runs: the Totem took 16 and 11 riders, its heap never deeper than its capacity, 6, its 17 and 38 calls all one deep
    and its frame tagged (`2000005A`, the control's `0000005A`); every error arm counted nought in every script; the
    fountain still read 3033. Photographed paused with the census read: the Totem with one rider counted aboard (not
    drawn on it; see STATUS). `save/` unchanged in all five runs.
  - **Not confirmed**: the Aztec Mayhem took nobody in either run, 343 s and 900 s, and no guest ever made it a
    destination. The old VM's control did the same over 900 s, so it is the chooser, not the VM (Q165); Q83's one
    Aztec Mayhem rider was one guest in one run.
  - **Reviewed** by five read-only adversarial agents, one per slice (the call end, the heap end and the restore, the
    other register writers, every comment and doc touched, completeness), each against the disassembly: no behaviour
    wrong. Fixed from it: a wrong struct-read address; the stale-slot wording (a returned call's address stays in its
    slot); the channel test pinning the old invented nought; two walk-test comments; `ride-operation.md`'s restore
    account; park.md's departures (a truncated last instruction) and the Belly Bounce's two calls; two test gaps
    (`JSR` and `RETURN` leave the register, and so does an empty `HOP`); `addresses.md` (30 rows, three of them
    earlier drift). FileFormats `saves.md`: the alignment fixes every offset, not "the other two"
    (`docs/save-module-chain`).
  - **Found:** Q166, and Q165's Aztec Mayhem.

  The item as written: From Q83's decode (`park.md`, "The two stacks"). None of it changes a shipped path, so each fix
  gets a test that fails first, and the game run is Q83's census (`q83sell.py`) reading the same. `RideScript`: `Call`
  jumps anyway on no stack or no room, and pushes and carries on for a non-label operand; `Return` with no frame ends
  the script; frames are stored tagged `0x20000000` and popped through the tag test; `PushCall` writes `Result` and
  ends the script on an error; `PopCall` ends it after its writes; `PushValue` writes `Result` and loses its
  `_values > _calls` guard; `PopValue` on an empty heap writes nothing; `Store` writes `Result` for `ADD`, `COPY`,
  `TEST` and `CMP` only with a variable, and a literal-destination `COPY` ends the script; `WALKON` stops writing
  `Result`; the class summary's "does nothing at all rather than failing" is wrong for `COPY`. First measure whether
  `GETANIM_CH` ever runs with no model (the engine then leaves `Result` and copies it). `ParkRides.Resume`: restore
  `+0x40`, `+0x44`, `+0x48` and block 1, only the slots above the call index being frames - a save with a `HUSH` ride
  mid-cycle, or `bugstv`/`Rocket` saved inside their `WAIT`, needs it.
- [x] **Q165. Every guest chooses the Belly Bounce over a bought ride: the decode.** Done 2026-09-28,
  `alexah/170-decode-the-ride-score`. Decode only; the build is Q165b and Q165c. `ride-operation.md`, "What a thing is
  worth to a guest"; `park-engine.md` and `park.md` corrected; FileFormats `saves.md`, the build stamp at 22
  (`docs/sam-and-saves-corrections`).
  - **The original does send guests to a new ride.** A bought or moved thing scores five times over for 184 sweeps
    (about 46 s) on the park calendar; the Totem 1.108 times over for costing more than 3,000; five guest types in
    eight prefer the new rides' 70 to the Belly Bounce's 40; and a guest who has left the Belly Bounce scores it nought
    until they leave something else.
  - **Why OpenTPW does not.** `PeepBehaviour` builds the chooser with no `ParkBalance`, so every guest type prefers 50
    (the file says 80, 65, 50, 35, 65, 80, 45, 80), which puts the Belly Bounce 20 excitement points ahead for
    everybody; every arrival is type 0; and there is no visit history, no age, no price or golden-ticket factor and
    no rain.
  - **Measured in the game** (a throwaway build scoring every decision both ways from the same inputs; `q165run.py`,
    `q165run2.py`, silent, predicted first, `save/` unchanged): `why` 43 of 43 at the Belly Bounce; the 17 decisions
    logged all chose it, the decode the Totem in all 17; 75 s after the purchase, asked of all 41 guests: OpenTPW
    picks the Belly Bounce for 40, the decode the Totem for 35, the Aztec Mayhem for 1 and the Belly Bounce for 5,
    split by type exactly as predicted. Photographed paused with that census: 13 in the Belly Bounce's queue, the
    Totem's empty. The decode's picks are computed inside OpenTPW's park; the original itself was not run (Q168).
  - **Also settled:** `+0xc4` is `GoldenTicketCost`, not `Research.Group` (Q95); `+0x1a0` and `+0x1a8` are
    `InitDuration` and `InitSpeed`; a thing's age is on the park calendar, not the real clock; the sideshow's
    excitement; `FUN_00519590`'s `+0x30` is `mCurrentDrops`.
  - **No test was added**: nothing was built.
  - **Reviewed** by read-only adversarial agents, four slices against the disassembly, the data and the run logs: 73
    verdicts, 54 upheld, 18 amended, 1 refuted (the queue was 13 of 16, not full). Taken from them: the sideshow's
    three fields are the object's, not the descriptor's; the track-handle and coaster branches; the relief weights
    always count; slot nought's 5 is dead; the queue term's count and cells; what the instrument leaves out.
  - **Found:** Q165b, Q165c.

  The item as written: Found by Q83's game runs: with an
  Inca Totem and an Aztec Mayhem bought beside the path and queued to it, `why` aimed every guest at the Belly Bounce
  (26 of 26, then 43 of 43) and none rode either in 15 minutes; with it sold, guests chose the Totem within a minute.
  Decode what `FUN_004fcc30`'s seven terms give each (`ParkRideScore`) and whether the original sends nobody to a new
  ride while an old one stands. Confirm: the same `why` census. Q83b's runs add one: with the Belly Bounce sold, no
  guest made the Aztec Mayhem a destination in 343 s, 900 s, or 900 s on the old VM, while the Totem took 11 to 16.
- [x] **Q165b. Guests' preferences and types, the original's.** Done 2026-09-28,
  `alexah/171-guests-prefer-their-own-excitement`. `ride-operation.md`, "What a thing is worth to a guest", "Built: each
  kind's preference and an arrival's kind".
  - **Built.** `PeepBehaviour` takes the park's `ParkBalance` (`ParkPeople` hands in the level's) and scores with
    `new ParkRideScore( balance )`, so each kind prefers its own `PreferredExcitement` in the chooser and at the
    arrival's refusal, which reads the same byte (`FUN_004fd4e0`, `0x004fd50a`). `ParkPeople.Admit` draws a new guest's
    kind over 0..7 (`FUN_004faec0`, `0x004fb019`) before reading its starting cash; a test may still name one.
    `ParkRideScore` says a null balance prefers 50 for every kind, and `ParkPeople` what its balance feeds.
  - **Decoded on the way:** the divisor `[0x007851d4]` is the balance's `PeepTypes` row count, the highest row the
    stack sets plus one (four raises in `FUN_004017a0`, reset before the global file only): 8 in every shipped park,
    from `data/levels/Standard.sam` or `Online_Standard.sam`. The kind is the second of the constructor's eight
    unconditional draws; the others are Q85's.
  - **Tests:** `ParkGuestTypeTests`, 4. A type 3 and a type 0 on (48,25) choose the Jungle Spray and the Belly Bounce,
    through `ParkPeople`'s `why` and through a Deciding turn's `MajorDest`; a type 0 turns away from the Spray at its
    back cell where a type 2 joins; 64 arrivals from a seeded draw cover the eight kinds, each with its own cash. Six
    put-the-bug-back mutations (the behaviour or the people dropping the balance, the kind left 0, drawn over 7, the
    cash of kind 0, the Deciding turn's wants as kind 0) were each red. The cell came from a throwaway search: with no
    balance, no two kinds on one cell can choose differently.
  - **Confirmed in the game** (`q165brun.py`, `q165bphoto.py`, silent, jungle; Q165's scene, a Totem at (57,23) and an
    Aztec Mayhem at (60,30) queued to the path, then `load 30`; predicted first; `save/` unchanged in both runs). At
    the purchase `why` split the 13 saved guests by kind, as predicted: kinds 0, 1, 5 and 7 (8) at the Totem, 2, 3 and
    6 (5) at the Belly Bounce. 75 s on, the 30 arrivals held all eight kinds (5, 1, 3, 7, 3, 3, 3, 5); every kind 0, 1
    and 4 aimed at the Totem, every 2 and 6 at the Belly Bounce, the 3s at it or the Spray. The three kind-5 and kind-7
    answers at the Belly Bounce were guests walking to the Totem (`dest 43`) past its entry, where its distance and
    queue terms win; run 2 read the same of three more. Over 480 s the Totem took 13 riders, kinds 0 ×4, 1, 4, 5 ×3
    and 7 ×4, none of 2, 3 or 6; the Belly Bounce 11, kinds 2 ×3, 3 ×5 and 6 ×3. Photographed paused (run 2) with the
    census read: a kind 7 counted riding the Totem (not drawn on it; see STATUS), kinds 5, 5 and 4 on its queue deck,
    and the Belly Bounce's queue kinds 3, 3, 6 and 3 with a kind 2 aboard.
  - **Not confirmed:** nobody aimed at the Aztec Mayhem in either run, farther than the Totem for everybody without
    Q165c's five-fold new window; the refusal at the Spray was not seen in the game, tested only.
  - **Reviewed** by five read-only agents (the wiring, the Ghidra claims, the tests, stale text, and a verifier): 14
    findings, 8 upheld, 4 amended, 2 refuted, no behaviour wrong. Taken from them: the constructor's draw count (eight,
    not three), which files set the rows, whose 35 the refusal example is, `ParkPeople`'s balance remark,
    `addresses.md`, and the test through the Deciding turn.
  - **Found:** Q169, and the note under Q165c.

  The item as written: From Q165 (`ride-operation.md`, "What a thing is worth
  to a guest", "Where OpenTPW differs"). Hand `ParkRideScore` the park's `ParkBalance`, so each guest type prefers its
  own `PeepTypes[n].PreferredExcitement` in the score and at the arrival's excitement refusal; draw an arriving
  guest's type `rand % 8` (`FUN_004faec0`, `0x004fb019`) where `ParkPeople.Admit` leaves 0; correct `ParkRideScore`'s
  remark that a null balance keeps the file's numbers. Confirm: Q165's scene, `why` and a photograph, the Totem chosen
  and ridden by guests of types 0, 1, 4, 5 and 7 while the Belly Bounce stands.
- [x] **Q165c. The rest of the score, the original's.** Done 2026-09-28, `alexah/172-the-rest-of-the-score`.
  `ride-operation.md`, "What a thing is worth to a guest", "Built: the rest of the score"; FileFormats `sam.md`
  (`GoldenTicketCost`, `BumperType`, the tiers' `InitSpeed`) and `saves.md` (470, the histories checked against played
  saves), `docs/sam-and-saves-corrections`.
  - **Built.** `ParkRideScore.Of` takes all twelve steps in order: the same kind as the thing left last nought; the queue
    term over the walked cells, counted to and including the first guest no longer queueing (`ParkState.QueueCount`,
    which the room test and the arrival's gates read too); an unsigned mean; new while `(uint)age <= 7` on the park's
    calendar (`ParkState.CalendarNow`: 2000-01-01 plus `mGameTick` × 3750 s; `AgeInDays`; `ParkRideChooser.AgeOf`);
    shelter × 5 while the weather's drops fall; `Priced`, the golden-ticket and price factors; the first matching visit
    and every matching refusal. `ExcitementOf` (`FUN_004e0860`): a sideshow's from its cost, price and chance (the Jungle
    Spray 30), a ride's level scaled by its speed and duration; the coaster, track-crowd and upgrade-tier branches
    counted. `Peep` keeps both histories, read from the save, written on leaving any thing (before the charge) and at
    both refusals, a nought on every sweep whose `mGameTick` divides by 20, cleared by a removal. A bought thing is
    stamped (`0x004db66a`), the object window's Age reads the park's calendar, the arrival's too-long gate stands, and
    the catalogue reads `GoldenTicketCost` and `BumperType` and the save `mUpgradeLevel`. Corrected: `ParkRideChooser`'s
    real clock, `ParkRideScore`'s sideshow formula and unproven keys, `GameCalendar.Epoch` (traced to `0x004f7ea0`),
    and the remarks in `ParkObjectWindow`, `ParkBuilding`, `park-engine.md` and `PLAYER-GAPS.md`.
  - **Tests:** `ParkVisitHistoryTests` 13, `ParkRideScoreTests` 5 new and 2 rewritten, `ParkGuestTypeTests` 1 new.
    Q165b's (48,25) is an 18-18 tie now, re-picked to (55,30); `ParkRideChoiceTests`' doorstep a 23-23 tie, moved to
    (53,29). 39 put-the-bug-back mutations (`q165c-mutate.py`), each red; one needed a new test, and the review found
    six that the first tests could not tell apart, each now red (`q165c-mutate2.out`).
  - **Measured outside the game:** Alexah's played jungle saves, read-only (`q165cprobe`): 1,060 non-zero history
    entries, every one a thing in the park; ages on the park calendar 50 to 824 days; one tier-1 Belly Bounce at speed
    75; 107 of 111 consecutive same-kind visits are toilets (Q170).
  - **Confirmed in the game** (`q165crun.py`, `q165crun2.py`, `q165crun3.py`; silent, jungle, each reading predicted
    first; `save/` unchanged in all three). **A guest leaving the Belly Bounce chooses something else:** 20 leavers over
    the three runs, 0 of 379 `peeps` samples naming 13 while it headed their visits and 0 of 51 `why` answers 13, where
    the same censuses' other guests answered 13 360 times; leavers set off for the Spray and the Drinks Shop or found
    nothing. Photographed (run 3, `L-photo-1-marked.png`): guest 43, a kind 4 who had left the Belly Bounce, riding
    the Jungle Spray, ringed; four seconds later `peeps` gave their visits as 14, 13. **A bought ride's five-fold
    window:** at the purchase every guest of every kind answered the Totem at age 0 (73 of 73 over the three runs);
    inside the window 31 of 32 guests who set off chose it (runs 2 and 3), kinds 2, 3 and 6 13 of 13; 185 sweeps on,
    its age read 8 and kinds 2, 3 and 6 answered the Belly Bounce, 27 of 27, none the Totem. Run 1 read that census
    while the Belly Bounce was mid-cycle and offered to nobody: its kinds 3 and 6 answered the Spray, two kind 2s the
    Totem. Photographed (run 3,
    `N-photo-1-marked.png`, `N-photo-2-marked.png`): guest 71, a kind 3, ringed just inside the gates and two cells on,
    aimed at the Totem 18 sweeps after it was bought. **Rain:** with drops falling every answer was indoors, 41 of 41.
  - **Not confirmed:** most leavers go home straight off the Belly Bounce, their exit level run out in its queue (the
    Q109 deviation), so the leaver photographed is one of few; the Totem's queue was not photographed full inside the
    window; the too-long gate refuses only at a hundred, which no queue in the park reaches (tested only); a removal
    clearing the histories, the refusals' aging and the purchase stamp were read in censuses and tests, not photographed.
  - **Reviewed** by five read-only agents (the executable claims in Ghidra, the behaviour, the tests, the text, and a
    verifier): 40 findings, 35 upheld, 3 amended, 2 refuted, no behaviour wrong that the park reaches. Taken from them:
    the precision remark (62 at double, 63 at single or extended, and which is live not settled), an undatable stamp
    not new, the spending census counting like the chooser, the too-long gate's inputs named (Q173), the arrival
    refusals' thoughts counted, the Q170 and Q171 deviations said at their sites, six tests that could not tell the
    wiring apart, and stale text in five files.
  - **Found:** Q170, Q171, Q172, Q173; notes under Q97, Q103, Q105, Q149, Q157 and Q167.

  The item as written: From Q165, the same table. Write `mPreviousRides` on leaving
  any thing and `mPreviousTemporaryRides` at the two refusals, with the nought every 20 sweeps; clear both on a
  removal and read both from a save; score the same kind as the last visit nought, and divide by the second history
  for every match. Stamp a bought or moved thing on the park calendar and multiply by `DecisionVariable2` for 184
  sweeps, compared unsigned. Add the golden-ticket and price factors (read `GoldenTicketCost`), hand in the rain, and
  compute the sideshow's excitement. Correct what says otherwise: `ParkRideChooser`'s real clock and negative ages,
  `ParkRideScore`'s sideshow formula, unproven keys and "ridden lately" (any visited thing counts),
  `GameCalendar.Epoch`'s untraced default, and `ParkObjectWindow`'s Age on the real clock. The queue term counts up
  to the first guest no longer queueing, over the walked cell count. Confirm: a guest leaving the Belly Bounce choosing something else, and a
  bought ride's five-fold window, each by census and photograph.
  From Q165b's review: the sideshow's computed excitement makes the Jungle Spray 30, which turns
  `ParkGuestTypeTests`' (48,25) case into an 18-18 tie for the type 3, decided by the tick's parity; pick the case again.
- [x] **Q166. `park.md` counts ten shipped instructions that store into a literal on purpose; there are at least 76.**
  Done 2026-09-28, `alexah/173-count-the-literal-destinations`. `park.md`, "Arithmetic, the destination rule and the result
  register" and "`BUMP` and `TOUR`"; FileFormats `vm/info.md` and `vm/instructions.md` (`docs/rsse-instruction-set`).
  - **Counted** over all 308 `.RSE` against every handler's own store (`q166count.py`): 351 instructions store into a
    literal, in 16 opcodes. 95 are the register used on purpose, each read by a conditional branch on every path before
    anything writes it again: `LIMBOSPACE` 24, `GETTIMER` 21, `GETANIM_CH` 15, `COAST 2 0` 12, `INLIMBO` 4, `MOD` 4,
    `RAND` 4, `BUMP 11 0` 4, `GETREMOTEVAR` 2, `SUB` 2, `MIN`, `SEC`, `WALKFLOATSTAT`; 79 name operand 0. The other
    256 are the triggers' length thrown away (`TRIGWAITANIM` 132, `TRIGANIM` 64, `TRIGANIM_CH` 60), none read.
  - **Decoded**: all 86 opcodes with an operand that is not a label or a string, by six Opus decoders each checked by
    an Opus refuter in Ghidra (62 upheld, 20 upheld with side notes, 4 not as written - `DIV` and `MOD` for a wrong
    comment, `WALKOFF` for a missed difference, `WALKFLOATSTAT` for its register's nought - none on a destination):
    which operand is a destination, and whether a literal there writes the register first (most), tests first and
    writes nothing (`ADD`, `FORCEUNLIMBO`, `GETVARINCHILD`, `GETVARINPARENT`, `BUMP 2`, `TOUR 4` and `16`), or ends
    the script (`COPY`). `YEAR` to `SEC` read the real clock, not the park's; `TRIGANIM_CH`'s third operand is its
    destination, not a rate.
  - **Checked against `RideScript`**: every built one stores the way its handler does, 344 of the 351, but for the
    declared model-less `TRIGWAITANIM` and a `COAST` with no ride state; the seven in unbuilt opcodes (`BUMP 11 0`,
    `MIN`, `SEC`, `WALKFLOATSTAT`) leave the register stale for their branch, none in Lost Kingdom's save.
    Deviations now said at their sites: `StartScream` leaves the register the engine writes (no branch reads it),
    `TRIGWAITANIM`'s model-less path writes neither and its re-entry reads the pose flag (Q174), `WAITANIM`'s first
    visit (Q174), the walk legs (Q175), the trigger's play rate (Q155), `GetVariableIn`'s unknown id, `NextDraw`'s
    overflow and `ParkAudio.Scream`'s second scream (Q176). Corrected: `Divide` (the original's `IDIV` faults, it
    does not wrap), `BOUNCESETNODE`, the class summary's destination rule, `ride-operation.md`'s walk legs;
    `addresses.md` gains nine rows.
  - **Tests**: `RideScriptLiteralDestinationTests`, 8: seven register idioms (`LIMBOSPACE`, `INLIMBO`, `RAND`, `MOD`,
    `SUB`, `GETREMOTEVAR`, `COAST 2 0`), each run alone for its answer and again with the branch its script takes, and
    `TRIGANIM_CH`'s unread length; and one in `RideScriptChannelTests`, `GETANIM_CH`'s literal with the Jungle
    Spray's model. Eight put-the-bug-back mutations (`q166-mutate.py`), each red as predicted. The first pass found
    three survivors, because the tests primed the register through the same store the mutation broke; they now prime
    through `TEST`, and variable nought holds a mark.
  - **Confirmed in the game** (`q166-instrument.py`, a tally of every ignored write per script, the register after it
    and how many branches read it; `q166run.py`, `q166run2.py`; silent, jungle; predicted first; `save/` unchanged in
    both). A bought Steak Shop's `LIMBOSPACE` 115 times, register 10, read 115 times; an Arcade's `INLIMBO` 118, 0, 118;
    an Aztec Mayhem's `GETTIMER` 227, 6032, 227; a Round Fountain's `TRIGANIM` once, 3033, never read; the Mammoth
    and Lava Fountains' `TRIGWAITANIM` 8 and 7, never read; in the stock park a toilet's `TRIGANIM` and the Jungle
    Spray's `TRIGANIM_CH`, never read, and its `GETANIM_CH` 17, read 18 times. Photographed paused with the census
    (`q166-run{1,2}/B-*.png`): the Steak Shop, the Arcade, the Aztec Mayhem, and the Round and Mammoth Fountains
    standing; the Lava Fountain's frame is filled by the park gate, so its reading is the census's alone.
  - **One prediction wrong in form**: I predicted `GETANIM_CH`'s reads would equal its runs. Each is followed by
    `BRANCH_PV` then `BRANCH_Z`, so one answering nought or below is read twice: 16 read once and the last, at -1,
    twice. The stock park reached nothing in the first 90 s, where I had predicted the Jungle Spray and the triggers.
  - **Not confirmed on screen**: the shapes no jungle thing reaches unaided (`RAND`, `MOD`, `SUB`, `GETREMOTEVAR`,
    `COAST 2 0`): tested only.
  - **Reviewed** by five read-only Opus agents (park.md, the FileFormats pages, the code and tests, this entry and
    STATUS, and a sweep for stale copies): 69 findings, 8 wrong, 23 misleading, 11 on a rule, 27 nits, all taken but
    the TOUR list's mixed full stops. Among them: the triggers' `<dest>` on the FileFormats page still said a branch
    reads the length; `COAST 2 0` and a model `GETANIM_CH` were pinned by no test; `DIV` and `MOD` each run their own
    `IDIV`; `BUMP 11` answers a word of the ride's record, not 0 or 1, three times in each water ride.
  - **Found:** Q174, Q175, Q176; a note under Q155.

  The item as written: Found by Q83b. "Arithmetic, the destination rule and the result register" says 17 shipped
  instructions have a literal operand 0, ten of them the register used on purpose (`MOD` 4, `RAND` 4, `SUB` 2). Among
  the opcodes `RideScript` builds, 76 store into a literal operand 0 across the 308 `.RSE`: `LIMBOSPACE` 24,
  `GETTIMER` 21, `GETANIM_CH` 15, `RAND` 4, `INLIMBO` 4, `MOD` 4, `SUB` 2, `GETREMOTEVAR` 2 (plus `SETVARINCHILD`'s 7,
  which is no destination). The unbuilt opcodes with a literal operand 0 (`SEC`, `MIN`, `WALKFLOATSTAT` and others)
  are not yet classed. Correct the count, and check each against its handler's store.
- [x] **Q169. A visit's excitement match is unbuilt and uncounted.** Done 2026-09-28,
  `alexah/174-match-a-visits-excitement`. `ride-operation.md`, "The excitement match" and "The effects of a visit";
  `park-engine.md`, the `PeepInfo` addresses.
  - **Decoded** first-hand and put to three Opus refuters (9 claims, all upheld). `FUN_004fdcc0` runs behind the
    settle-up's `+0x1f1` gate, after a sideshow's prize and before the item's effects. Nothing when the excitement's low
    byte is nought; otherwise | kind's liking − excitement | under 5, 15 or 40 adds `PerfectRide`, `GoodRide` or
    `OKRide`
    (25, 15, 5), from 40 nothing; then illness gains `((100 − trunc( hunger )) / 20) × (excitement / RideVomitDivisor)`,
    whole-number divisions, both meters held to 0..100. Hunger rises with time, so a full stomach is the sick one. The
    four keys are the int globals `0x00785064`..`0x00785070` by the executable's own table, which settles
    `park-engine.md`'s doubt; `FUN_004fdcc0` is their only reader.
  - **Built.** `ParkAdmission` reads the four (nought when absent, as the original; the divisor held at one, a
    deviation); `ParkRideOperation.MatchTheExcitement` takes the likings from the park's `ParkRideScore` and logs each
    match with both meters before and after; `SettleUp` runs the prize, the match, the effects and the winner's cheer
    in the original's order, and counts the two pieces it does not keep (Q177). Deviations said at their sites.
  - **Tests**: `ParkExcitementMatchTests`, 14 - every threshold from both sides (gaps 4/5, 14/15, 39/40, from the
    Belly Bounce's speed), the truncated hunger (80.9, 0.9), whole-number divisions, the Jungle Spray (84), a thing with
    no excitement, the gate, no likings. 17 put-the-bug-back mutations (`q169-mutate.py`): 14 red, two of those one test
    off my prediction (a second test also stands at gap 5; a list I miscounted), and 3 green as predicted - the match's
    own clamp, which the effects hold again; its order against the effects, which no shipped thing can show; and
    `ParkPeople` handing in no score, which the game run shows. The first pass found the OK boundary tested twice over
    (now once).
  - **Confirmed in the game** (silent, jungle, predicted first; `save/` unchanged within runs 3 to 5, run 1's before
    and run 2's after not kept, the value the same at every start; `q169run.py`,
    `q169run2.py`..`q169run5.py`; runs `q169-run{1..5}/`). Runs 3 to 5 on the final build judged every match line
    against the rule and the census before it; `q169-deltas.py` paired the lines with `peeps` either side: run 1 (the
    build before the review) 22 of 22, run 3 9 of 9, run 4 5 of 7 (two came after the last census, both right by the
    rule), run 5 19 of 19, the two Jungle Spray kind 3 winners reading +34, the match's 15 and the winner's 19. On a
    bought Totem with the Belly Bounce sold, a kind 0 (guest 100) and a kind 3 (guest 102): gap 10,
    happiness 0 to 15, and gap 35, 0 to 5; illness +7 at hunger 62 and +35 at hunger 0; `peeps` read the same.
    Photographed paused with the census: the Totem, its queue full. The Belly Bounce's kind 3s in run 1: gap 5, +15;
    and in run 2 (the build before the review, the same arithmetic) two guests liking 80 there, gap 40: happiness +0,
    illness +16 and +20.
  - **The counts, on the committed build** (`q169counts.py`, 150 s, predicted): `SETTLE_UP_EVENT_HISTORY`,
    `_HAPPINESS_SINCE_JOIN` and `_OBJECT_VISIT_COUNT` 4 each beside 4 match lines, and one sideshow thought; no toilet
    was visited; `save/` unchanged.
  - **Predictions that missed**: guest 100's illness, predicted +21 from the hunger of 34 they queued with, was +7 -
    their hunger rose to 62 while they queued; and no kind 3 rode the Totem in runs 1 to 3 (about 60 minutes): with the
    Belly Bounce standing a kind 3 prefers it, and run 3's re-bought Totems put off the kind 3s already queueing.
  - **Not confirmed on screen**: the perfect arm, which no shipped pairing is within four of (tested only); the rider
    drawn on the Totem (see STATUS).
  - **Found:** Q177; notes under Q85 and Q171.

  The item as written: Found by Q165b's review; `docs/PLAYER-GAPS.md` names
  it, and nothing counts it (`CLAUDE.md` rule 4). The settle-up `FUN_004fe1e0` calls `FUN_004fdcc0` on every visit
  (`0x004fe259`). It does nothing when the thing's excitement (`FUN_004e0860`) is nought; otherwise it takes |the
  kind's `PreferredExcitement` (`0x004fdce8`) − the excitement| and adds `PerfectRide`, `GoodRide` or `OKRide`
  (`0x00785064`..`0x0078506c`) to happiness below 5, 15 or 40, clamped 0..100, then raises illness `+0x1b0` by the
  excitement over `RideVomitDivisor` times ( 100 − hunger ) / 20 (`0x004fddab`..). `ParkRideOperation.SettleUp` names
  "three more happiness changes" only. Count it first, decode the arithmetic's types, then build it. Confirm: `peeps`
  happiness and vomit after a Totem ride by a kind 0 and by a kind 3.
- [x] **Q170. Guests reach a second toilet through the walk, not the ride score: the decode.** Done 2026-09-28,
  `alexah/175-decode-the-toilet-visits`. Decode only; the build is Q170b. `ride-operation.md`, "A second toilet: the
  minor decision and the saved major", step 5 of "The effects of a visit", "Where a guest is aimed" and the guest
  record; FileFormats `saves.md`, `mSavedMajorDest` (file 499, `docs/sam-and-saves-corrections`).
  - **Decoded** by five read-only decoders (the history's writers, the destination's writers, what reads toilet-ness
    and the need, the walk and the exit, the state machine and `Toilet.rse`), each put to a skeptic, and a synthesis:
    85 claims, 71 upheld, 14 amended, none refuted. Re-read first-hand: the walking turn's counter (`0x004ffef2`),
    `FUN_004fd570`, `FUN_00500900`, `FUN_004d8b40`'s search `FUN_00511ef0` and the toilet arm (`0x004fe78f`..).
  - **The answer.** The same-kind nought holds for every small toilet (`+0xe` is `mId`); from Deciding only the chooser
    aims a guest at a toilet, and on the walk the minor decision and the restore do. On the walk to a chosen thing,
    every 12th turn (`+0x2c`, `mCount`), the minor decision scores the things in a 4×4 window nearer the major's back
    cell, from nought, a tie winning on an odd tick, and switches to the best when the raw search puts it nearer the
    major's entry than the guest is, saving the major in `+0x1de`; after that visit, state 15 restores the major with no
    score. Toilet A chosen, toilet B on the way, then A: the history, newest first, reads A, B. Also a stale `+0x1de`
    (only the restore, a removal and the constructor clear it) and a toilet taken at nought on an odd tick. And a toilet
    visit empties the need, so in the original nobody leaves one still in need.
  - **In Alexah's saves:** 19 (20) guests part-way through a diversion against 5 bound for a toilet with nothing saved,
    3 of them walking (0.79), and the histories' 107 toilet-then-toilet against 136 other-then-toilet (0.79);
    `mSavedMajorDest` non-zero on 50 (51) of 339 guests, and on none of the shipped park's 13. Illness, which the score
    counts only for a toilet, sends more guests to one than need does.
  - **Measured in the game** (a throwaway build shadowing the minor decision, never enacting it, an object seen only on
    its anchor cell and scored by OpenTPW's `ScoreOf`; `q170run.py`, `q170run2.py`, `q170run4.py`, `q170analyse.py`;
    silent, jungle, `toilet 90` every 20 s, once in run 1; predicted first; `save/` unchanged within all four runs;
    `q170-run{1..4}/`). Run 3, 900 s: 32 toilet visits of 32 left the need where it was, 90 to 95
    (`SETTLE_UP_TOILET_RELIEF` 32); no toilet chosen or visited after a toilet; 7 of 7 first choices after one the Belly
    Bounce; a switch toward a toilet on 12 of the 14 walks to 23 - first to 21 from (56,19) six times, to 22 from
    (56,18) six - none on the 24 walks to 21 or the 5 to 22 or toward anything else: 12 toilet-then-toilet visits the
    original makes and OpenTPW cannot. Five decisions, by two guests just off a toilet, had a toilet best at nought, on
    odd ticks as the tie rule requires; none switched. Photographed paused with `peeps` and `why`: guest 66 at (56,19)
    bound for 23 as a switch to 21 is logged (run 3); guest 35 off toilet 21 at need 90 walking to the Belly Bounce,
    `dest` 13 in both (run 4, 45 s).
  - **Predictions that missed**: run 1 measured the lengths through OpenTPW's reroute stage, which `FUN_00511ef0` does
    not run, and logged a switch at (56,17) that the raw search refuses (2 and 2, a tie); re-run as run 3. The shadow
    logged a second switch on four walks, which I had not foreseen: it never enacts one, so a guest already turned to 21
    goes on being asked about 22. Of 40 toilet choices west of the corridor, 36 have the parity a tie would give (21
    odd, 23 even) and four do not; no score was logged, so none was judged a tie.
  - **Not confirmed on screen**: the switch and the restore, which the shadow only works out (Q170b); the re-aim from a
    switched-to entry cell to its back cell; how the saves' 107 pairs split between the three ways. And in none of the
    four runs did OpenTPW's `LeavingRide` report arriving (0 of 32 toilet exits in run 3), which the restore needs
    (Q170b).
  - **No test was added**: nothing was built.
  - **Found:** Q170b; notes under Q100 and Q177.

  The item as written: Found by Q165c, in Alexah's
  played jungle saves (`mGameTick` 19,004 and 19,007; `q165cprobe`): of 772 pairs of consecutive visits in
  `mPreviousRides`, 111 are the same kind twice and 107 of those are toilets in the later save (109 and 105 in the
  other), mostly one of three adjacent toilets and then another. The score's same-kind nought (`FUN_004fcc30`, `0x004fcd3f`) forbids that choice, so something else
  sends a guest in need to a toilet, or a toilet visit is written otherwise; OpenTPW sends guests to toilets through
  the score alone, so after one toilet a guest can now choose no other. Decode where (the needs turn `FUN_00501650`,
  the queue turn's toilet arm, `FUN_004fcb10` itself), then build it. Confirm: a guest leaving a toilet still in
  need, `why` and the next `dest`.
  From Q169: OpenTPW's settle-up never empties the toilet need (`0x004fe7b6`, counted `SETTLE_UP_TOILET_RELIEF`, Q177),
  so a guest leaves a toilet as much in need as they went in; build that before reading guests' toilet visits.
- [x] **Q170b. The walk's minor decision, the saved major and a toilet's relief: the build.** Done 2026-09-28,
  `alexah/176-a-second-toilet-by-the-walk`. `ride-operation.md`, "A second toilet" (its "Built" paragraph and table)
  and "The effects of a visit", steps 3b and 5; FileFormats `saves.md`, "The person base" and the map cell's `mWho`
  (`docs/sam-and-saves-corrections`).
  - **Built.** The toilet arm (`ParkRideOperation.UseTheToilet`): the need to nought, illness to nought above 90 (its
    truncated byte), the hurry speed 25; the dirtying and events `0x11`/`0x12` counted, `SETTLE_UP_TOILET_RELIEF` gone.
    `Peep.SavedMajorDest` from file 499, cleared for every guest by a removal naming it. `Peep.WalkingTurns` from the
    person record's byte 36 (`mCount`, established this session), counted on every `GoingToRide` walking turn with the
    park open (the shut arm counted, `GOING_TO_RIDE_PARK_SHUT`, Q102). The minor decision on the twelfth
    (`MinorDecision`, `ParkRideChooser.MinorDecisionFor`, `CellSearch.RouteLength`); the restore off a thing
    (`SentOnToTheSavedMajor`). Also counted, found on the way (rule 4): the special-ingredient switch, the `+0x198`
    happiness terms and the appearance arm (`SETTLE_UP_SPECIAL_INGREDIENT`, `_INGREDIENT_HAPPINESS`, `_APPEARANCE`).
  - **Why `LeavingRide` never arrived:** `PutDownAtTheExit` stepped against the exit's facing - `CellEdge.DirectionFor`
    answers from the side of the cell entered, `FUN_004d97e0` from the cell's own - aiming a toilet's user at bare
    ground (54,y) and the Belly Bounce's into its own footprint (52,25), so every guest let off anything gave up on the
    spot. Found by reading, not by a run; now `DirectionFor( Opposite( facing ) )`, (56,y) and (52,27).
  - **Re-verified first** (`wf_e3c34849-dbb`: four Opus readers in Ghidra, a skeptic each; nothing load-bearing
    refuted): `0x004f8cc8` is the save's write arm, the load `0x004f91e8`; `mCount` is file byte 36; the removal
    clears `+0x1de` for every guest; the restore posts no event; a switch overwrites `+0x1de` whatever it held, and
    23 to 22 then 21 is possible in the corridor (lengths 2 and 1 from 22's entry); guest `+0x188` is the navigator's
    mode, which the gate's states, the put-down and the leaving retry set to 1 (`ParkPeople.WalkingMode` said nothing
    did; corrected).
  - **Measured in every save** (`q170bprobe`, read-only): `mCount` 0..11 on all 745 guests of the shipped park and
    Alexah's four played saves, never 12; `mSavedMajorDest` non-zero on 50, 51, 6 and 6, each an object in its save.
  - **Confirmed in the game** (`q170brun.py`, `q170banalyse.py`; silent, jungle, fine weather, two buses, `toilet 90`
    every 20 s, 720 s, predicted first; `save/` unchanged; `q170b-run1/`): 13 switches, each from (56,19) to 21 at
    lengths 4 and 3 or from (56,18) to 22 at 3 and 2, all on walks to 23; 10 kept, each with the guest no farther (2 2,
    3 3, and off a toilet 16..19 against 18..19); 10 restores to 23, each after the switched guest used the nearer
    toilet (the other 3 went home first, Q109); 33 toilet uses, 33 left the need at nought; no walk off failed, 24
    arrivals logged; the census 33 `SETTLE_UP_TOILET_EVENT`, 13 `MINOR_DECISION_EVENT`, no `SETTLE_UP_TOILET_RELIEF`.
    Photographed paused with `peeps`: guest 42 at (56,19), dest 21, saved-major 23, turns 0; on the path beside 21,
    dest 23, saved-major 0, visits [21,...], toilet 0; inside 23 (hidden by its roof, the census places them), visits
    [23,21,0,0], toilet 0, speed 25.
    Re-run on the commit's own build, 300 s (`q170b-run2/`): 4 of 4 checks, 10 switches as above, 9 of 9 restores,
    27 of 27 uses at nought, no failed walk off, `save/` unchanged; the three settle-up counts unreached, as no drink sold.
  - **Predictions that missed:** the need at the second toilet (90, 93, 0, 90 on the four who reached 23): the harness
    sets every guest to 90 every 20 s. Mutant M23 turned two tests red, not one.
  - **Not confirmed on screen:** illness emptied (none passed 90), a second switch, a stale saved major, the thing-gone
    arm, the removal's clear (nothing sold), a played save's two fields loaded (probed only), the park-shut non-count,
    a switch at nought on an odd tick (all 13 were on even ticks), the Belly Bounce's walk off (logged, not shot).
  - **Tests:** `ParkSecondToiletTests` (19), and `ParkRideExitTests`' Bounce guest now walks off and arrives.
    `q170b-mutate.py`: 32 mutants, every one red, each count as predicted (M23's on the second run).
  - **Reviewed** (`wf_6f422e48-d65`, four Opus reviewers, a skeptic each): 16 of 19 findings real, all words or tests
    (the walking-mode reason was false, a stale comment, rule 4's uncounted switch, five tests), each fixed.
  - **Found:** notes under Q177, Q102, Q100 and Q109.

  The item as written: Found by Q170's decode
  (`ride-operation.md`, "A second toilet: the minor decision and the saved major"). In order: (1) the settle-up's toilet
  arm, Q177's part 3: the need to nought (`0x004fe7b6`), illness to nought when its byte is above 90
  (`0x004fe7dc`..`0x004fe7ef`), the hurry speed 25 (`0x004fe7f5`); count the dirtying `FUN_004e2440` (Q100) and events
  `0x11` and `0x12` (Q177). (2) `Peep.SavedMajorDest` (`+0x1de`), read from the save (file 499, into
  `ParkWorld.GuestState`), written by a switch (step 4, `0x004fd934`), cleared by the restore (step 5, `0x0050092a`) and
  by `ThingRemoved` when it names the thing (`0x004fb4a6`..`0x004fb4b3`), and by nothing else. (3) The walking-turn
  count (`+0x2c`, `mCount`, whose file offset in the person base is not established: start it at nought and say so at
  the site), counted on every `GoingToRide` walking turn and never reset between walks, after the park-shut arm Q102
  builds. (4) `FUN_004fd570` on the twelfth, in its order, sharing `ParkRideScore` and `ParkRideChoice.CanBeOffered`
  with the chooser but not `ParkRideChooser.Beats` (a first candidate at nought wins on an odd tick), the lengths from
  `CellSearch` alone at 60,000 with no reroute; the window's things read from the cells objects stand on (which cell a
  larger object is linked on is not established). (5) `LeavingRide`'s arrival as `FUN_00500900` - but in Q170's four
  runs OpenTPW's `LeavingRide` never reported arriving (0 of 32 toilet exits in run 3): find why first, or the restore
  is not reached. Count each until it is built. Confirm: in the stock park with `toilet 90`, a guest bound for toilet 23
  switched to 22 or 21 at (56,18) or (56,19), `peeps`' dest before and after, then sent on to 23 unscored, `peeps`'
  visits reading 23 then 22 or 21; the toilet need nought after each toilet.
- [x] **Q171. A bought thing starts at a price of nought: now at its item's.** Done 2026-09-28,
  `alexah/177-a-bought-thing-starts-at-its-price`. `park-engine.md`, "The object window's stats panel" and "How a key
  finds its global"; FileFormats `sam.md` (`docs/sam-and-saves-corrections`).
  - **Built.** `ItemDescriptionFile.InitPricePerUse` (the item's over its category's), carried by the catalogue's
    `Item`. `ParkBuilding.Constructed`, the record `FUN_004db090` fills, writes it unclamped (`0x004db3ad`), and the
    speed, capacity and duration by `ParkBuilding.StartingSettings`: each only above nought, the capacity's low byte
    held to its bounds only when they sum above nought (`FUN_004dd7f0`), the duration's always. The capacity was not in
    the item; it sits between the two the item names, and four sideshows elsewhere start held down by it. The charge
    logs "paid", and a purchase its price and settings.
  - **Verified first** (`wf_727b3f26-329`: four Opus readers in Ghidra, a skeptic each; all upheld): descriptor `+0xe4`
    is `InitPricePerUse` by the compiled schema, an int copied unclamped; the category is parsed first and the item's
    own over it, the later of a repeated key kept; nothing on the purchase path writes the four again; a move rebuilds
    through the same constructor, so a moved thing's price starts afresh.
  - **Measured in the data** (`q171probe`, read-only): every one of the 49 shops and sideshows in the four themes
    declares its own price, from 10 to 75, and no ride or feature one. The rules change no jungle item's starting
    settings; across the four themes they change eight.
  - **Confirmed in the game** (`q171run.py`; silent, jungle, fine weather, the saved Jungle Spray 14 sold and one bought
    at (51,30) through `carry 1303` and `put`, two buses, 480 s, predicted first; `save/` unchanged; `q171-after/`):
    `spend` read the bought Spray, thing 43, at price 20, took 0; the purchase logged "price 20, speed 0, capacity 3,
    duration 0"; 9 guests paid 20 at it and its took read 180; paused at the first, guest 29's `peeps` cash, 550,
    matched the logged cash; both winners' excitement read 30. Photographed paused: the bought Spray spraying, guest 29
    at its entrance. 6 of 6 checks. The control, the parent build in a worktree at `52926cd` (`q171-before/`): price 0,
    6 winners at excitement 34, took 0, 4 of 4. Re-run on the commit's own build, 360 s (`q171-commit/`): 6 paid 20,
    took 120, the payer's cash matched, `why` at the pause had 1 of 53 choosing it at age 9; none of the 6 won, so the
    excitement check had nothing to read (5 of 6).
  - **Predictions that missed:** `why` at the pause (54 s): none of 53 guests chose the bought Spray (43 the Belly
    Bounce, 10 nothing), where the control's pause at 96 s had 5 of 47; eight more chose it and paid later, nine in
    all. The control's age for it read 16, not 0: the park calendar runs about a sixth of a day a second.
  - **Not confirmed on screen:** the held settings (no Lost Kingdom item needs them), a move's price starting afresh (no
    screen sets a price yet), a category's price showing through (no shipped item leaves it).
  - **Tests:** `ParkStartingSettingsTests` (7). `q171-mutate.py`: 18 mutants, every one red as predicted.
  - **Reviewed** (`wf_a002938d-86c`, three Opus reviewers, a skeptic each): 15 findings real, all words, tests or the
    address index (a rule edge each test missed, the Arcade's repeated key, "inside its bounds" false for the shops),
    each fixed; the fixes checked (`wf_7666a068-b90`), which found four more (a rule edge, three words), fixed.
  - **Found:** Q178; a note under Q177.

  The item as written: Found by Q165c. `ParkBuilding` writes no `PricePerUse`
  and the catalogue reads no `UsageInfo.InitPricePerUse` (the Drinks Shop 30, the Jungle Spray 20), which the
  constructor copies into the object's `+0x194` (`0x004db378`..`0x004db3ad`). A bought shop or sideshow charges
  nothing, and a bought Jungle Spray's excitement is 34 where the original's is 30. Read the key, with the category's
  showing through, and write it on a purchase. Confirm: buy a Jungle Spray, `peeps` a guest paying 20 at it, and `why`.
  From Q169: the excitement match reads the same price, so a bought Jungle Spray's 34 moves a kind 3 (35) from the good
  ride to the perfect one. And `ParkBuilding` writes `OperatingSpeed` and `OperatingDuration` whatever the item says,
  the duration unclamped, where the constructor writes `+0x58` only for a starting speed above nought
  (`0x004db51c`..`0x004db54c`) and `+0x5c` only for a starting duration above nought, held to `Min`/`MaxDuration`
  (`0x004db565`..`0x004db64f`); no jungle ride shows it, but the excitement's ratios read both.
- [x] **Q172. A coaster's, a track ride's and an upgraded ride's excitement: the decode.** Done 2026-09-28,
  `alexah/178-decode-the-track-and-tier-excitement`. Decode only; the build is Q172b. `ride-operation.md`, "A coaster's,
  a track ride's and an upgraded ride's excitement", step 4 and "Where OpenTPW differs"; `park-engine.md` corrected
  (the closing multiply's order, and the "crowd", which counts track sections). FileFormats `saves.md`, the object
  record's handles, `mIsTrackRideValid` and `mUpgradeLevel`, and `sam.md`, the tiers (`docs/sam-and-saves-corrections`);
  `saves.md`, the `KART` and `SAOC` modules and the script's `+0xe0` (`docs/save-module-chain`).
  - **Decoded** by three read-only decoders (the coaster, the track handle, the tier), each put to a skeptic, and a
    synthesis: 117 claims, 100 upheld, 17 amended, none refuted. Re-read first-hand: the templates' first dwords (-1,
    -4, -5, -11), `FUN_0054b2f0` the only writer of the section list, the offer gate's coaster call, the Instant Action
    skip, the capacity slot overwritten at `0x004e0680` and the closing multiply; the `KART`, `SAOC` and `ESSR` bytes
    measured again with my own walks (`q172/main/`), each landing on its tag in all nine park files.
  - **The answer.** A track ride is any object with a handle (`+0x28`), which the placer gives every item with a
    `BumperType` and never nought; its base is 60% of the level plus `3 × crossings + the longest straight + 2 ×
    bends` (held 0..40), and only laying track adds sections, so a bought Hot Pot is **42** in the original, where
    OpenTPW says 70 (Dino Karts 48 against 80, Splish Splash 45 against 75; Alexah's saved Dino Karts, 33 sections, 81
    against 80). A coaster is `trunc( 50 + f / 2 )` of its node's rating `+0x104`, nought until its script's `COAST 8`
    binds the handle, and **no coaster is offered until the editor closes its circuit** (`FUN_00441970`), where OpenTPW
    offers a bought one at 90. The tier divides by `Upgrades[l]`'s starting speed and duration; the stock park is
    Instant Action, which offers no upgrade, and Alexah's tier-1 Belly Bounce is 40 either way at its settings (32 at
    speed 60 in the original).
  - **Measured in the game** (the build before any change; `q172run.py`, silent, the stock jungle park, predicted
    first, 7 of 7 in both runs; `save/` unchanged; `q172-run1/`, `q172-run2/`): the three keys 0 at load and after a
    `why` of 13; a Hot Pot bought at (57,23) with its queue joined at (56,22), paused: the purchase adds nothing, one
    `why` adds 13 to `RIDE_EXCITEMENT_TRACK_CROWD` for 13 lines (all 13 choosing it), a second 13 more; after 90 s
    with `load 40`, one `why` adds 48 for 48 lines, 37 choosing it; the coaster and tier keys stay 0. Photographed
    paused beside that census (`hotpot-ran-z70.png`).
  - **Not confirmed on screen**: the original's 42, which only a build can show (Q172b); the coaster's gate and its
    station-only rating; the tier and the saved Dino Karts, which wait for a copy of Alexah's save to load (Q167).
    Seen, not chased: magenta pads under the bought Hot Pot's entrance.
  - **No test was added**: nothing was built.
  - **Found:** Q172b.

  The item as written: Found by Q165c, which counts all three: `RIDE_EXCITEMENT_COASTER_TRACK` (track type 3:
  `trunc( 50 + f / 2 )` of what `FUN_0043e0b0` answers for `FUN_0055a4e0`'s find, or nought with nothing found), `RIDE_EXCITEMENT_TRACK_CROWD` (a thing with a
  track handle, a non-zero `Bumper.BumperType`: 60% of the level plus `FUN_00545310`'s crowd `3a + b + 2c`, held 0..40,
  the sum held 0..100) and `RIDE_EXCITEMENT_UPGRADE_TIER` (the tier's `InitSpeed` and `InitDuration`, where the
  catalogue reads tier nought: Alexah's played park has a tier-1 Belly Bounce at speed 75). Nothing Lost Kingdom's save
  places reaches them; a bought Hot Pot, Dino Karts, Splish Splash or coaster does. Confirm: `unimplemented`, and
  `why` beside a bought Hot Pot.
- [x] **Q172b. A track ride's excitement from its handle, an upgraded ride's from its tier, and no coaster offered
  without a closed circuit.** Done 2026-09-28, `alexah/179-track-and-tier-excitement`. `ride-operation.md`, "A
  coaster's, a track ride's and an upgraded ride's excitement", its "Built" and "Measured in the game after the build";
  FileFormats `saves.md`, a saved coaster's header (`docs/save-module-chain`, `529867f`).
  - **Built:** `ParkTrackRides` reads `KART` at the boundary, refused unless the walk lands on its tag;
    `ParkTrackRideTable`, in `ParkState`, is the table of 64, seeded record by record with `FUN_0054b2f0`'s refusals
    (stale handle, duplicate cell, the pool of `0x400`), a slot taken by the placer for a `BumperType` and freed by the
    demolisher, and `FUN_00545310` ported; `ExcitementOf` takes the track arm on the object's handle and divides by its
    own tier (`Item.StartingAt`, tiers 0..2 over the category's); `CanBeOffered` asks `CircuitClosed` of a coaster after
    the room test, a saved one found by the record's `MeshInstanceID` (offset 54) in `ParkCoasters`, the `SAOC` module's
    first header. `RIDE_EXCITEMENT_TRACK_CROWD` retired; `spend` prints each thing's excitement; the purchase log its
    handle; a refused `KART` or `SAOC` is logged at load.
  - **Verified first:** three Ghidra re-reads of the decode (`wf_a412f5cf-86c`) upheld it and amended it: the
    demolisher's keep is never reached for an object, so a move frees its slot and needs no count; the constructor sets
    `mIsTrackRideValid` for every object; a no-bend list's longest is nought; the coaster gate finds its node by the
    object's model instance, and `+0x140` is a clash count the save restores; a tier of 3 reads past `Upgrades`. Folded
    into `ride-operation.md`.
  - **Reviewed** (`wf_bb55d7b7-48d`, four lenses, each finding put to a skeptic): 23 findings, 17 upheld, all fixed -
    the loader's section refusals ported, stale comments and docs, the refused modules logged and counted, a dead
    `SectionsOf` removed, the tier-3 test at speed 80, a test through a failed put-down.
  - **Tests** (14 new, each predicted first): the walk (the played track's (12, 6, 1), `[9, 5, 9]` 2), the table, the
    readers, the saved track 81, a bought Hot Pot 42, Dino Karts 48, Splish Splash 45, a Dino Karts with no handle 80,
    the tier-1 Belly Bounce 32 and 40 (50 on tier nought), tier 2's 40, the coaster gate. **Mutations** (`q172b-mutate.py`,
    `.out`, `mutate3.out`, `mutate3b.out`): 44 put back, all as predicted, 42 red; two predicted misses: a caller passing
    no table (a bought ride has no track, so only a saved one would show it) and a saved BumperType past the templates.
  - **Measured on Alexah's saves**, read-only (`q172bsave`): all nine park files' `KART` walks land; the Dino Karts 81;
    the tier-1 Belly Bounce 40 at 75; the Temple Of Gloom's model instance 330, its header's, flags `0x101`, no clash.
  - **Confirmed in the game** (`q172brun.py`, silent, predicted first, `save/` unchanged; `q172b-final/`, 7 of 7): the
    Hot Pot bought with `track 0xffffff00` and `spend` excitement 42; the Temple Of Gloom not offerable and named by no
    `why` line, paused or after 240 s with `load 40`; `RIDE_EXCITEMENT_TRACK_CROWD` 0 throughout. The control on the
    build before it (`q172b-control/`, 7 of 7): the Temple Of Gloom offerable and chosen, the counter one a scoring.
    Photographed paused beside the census (`ran-z70-marked.png`).
  - **Not confirmed on screen**: a rider's match log reading 42, since the Hot Pot lets no rider off in either build
    (Q179); the saved track ride, tier and coaster, which wait for Q167.
  - **Found:** Q179. Seen, not chased: magenta pads under the bought Hot Pot's entrance, as in Q172.

  The item as written: Found by Q172's decode (`ride-operation.md`, "A coaster's, a track ride's and an upgraded
  ride's excitement"). Build:
  (1) a reader, at the boundary, for the save's track-rides module (FileFormats `saves.md`, `KART`): each ride's handle
  and each section's type in file order, refusing the file unless the walk lands on the tag;
  (2) a bought item with a `BumperType` takes `TrackRide` = the first of 64 slots no loaded or bought ride holds,
  `| BumperType << 8` (`0x00529e4d`, `FUN_00545890`), with no sections, freed on a sale (`0x00528584`); a move, which
  may keep its entry (`0x00528570`), and a 65th ride (a null read at `0x00546225`) are counted, not chosen;
  (3) `FUN_00545310` ported: two passes without a reset, half the bends, the longest run, half the crossings, and a
  stale handle writing nothing;
  (4) `ParkRideScore.ExcitementOf` takes the track arm on the object's `TrackRide` (`0x004e06ce`), not the item's
  `BumperType`, base `clamp( E × 60 / 100 + clamp( 3 × crossings + longest + 2 × bends, 0, 40 ), 0, 100 )` into the
  ratio tail, and `RIDE_EXCITEMENT_TRACK_CROWD` retires;
  (5) the catalogue carries `Upgrades[0..2].InitSpeed` and `InitDuration`, each over its category's, and the tail
  divides by tier `l` (`0x004e0691`, `0x004e06be`); `RIDE_EXCITEMENT_UPGRADE_TIER` stays only for `l` above 2;
  (6) the offer gate's coaster arm (`FUN_00441970`, `0x004dd9b7`): a coaster placed here has no closed circuit and is
  refused; a saved one whose `SAOC` flags pass is let through, and what cannot be read of it is counted.
  Stays counted: `RIDE_EXCITEMENT_COASTER_TRACK`, whose rating needs the spline and sample run no save holds, and
  laying track (`PLACED_TRACK_RIDE_FIRST_TRACK_CELLS`). Tests, predicted first, each bug put back: the saved Dino
  Karts' 33 sections give (12, 6, 1) and 81; a bought Hot Pot 42, Dino Karts 48, Splish Splash 45; `[9, 5, 9]` a
  longest run of 2, and 1 with one pass; a Dino Karts record with `TrackRide` 0 gives 80; the tier-1 Belly Bounce 32 at
  speed 60 and 40 at 75 (50 on tier-nought divisors), a tier-2 40 at 90; the counter assertion after
  `ParkRideScoreTests.cs:366` rewritten. Confirm: buy a Hot Pot and join its queue; predict every rider's match log to
  read excitement 42 (70 before the build), and `unimplemented` without `RIDE_EXCITEMENT_TRACK_CROWD`; buy a Temple
  Of Gloom with its queue and predict `why` never naming it. The tier and the saved Dino Karts wait for Q167.
- [x] **Q173. The longest queue a guest joins or stays in, for a ride with a queue path.** Done 2026-09-29,
  `alexah/180-the-longest-queue`. `ride-operation.md`, "The longest queue - `FUN_004dda40`" and the "OpenTPW builds"
  paragraph after the `InQueue` turn; `park-engine.md`, "Which rounding is live" (the renderer's third exit) and "How a
  key finds its global" (a float's grammar); FileFormats `sam.md`, `Upgrades[n].QueueWaitTimeConstant`
  (`docs/sam-and-saves-corrections`, `7a85c8d`).
  - **Built:** the catalogue reads each tier's `QueueWaitTimeConstant` as a float over its category's
    (`ItemDescriptionFile.QueueWaitTimeConstantAt`); `PeepBehaviour.LongestQueue` ports `FUN_004dda40` (R stored as a
    float, the floor of 4 taking not-a-number, `__ftol`'s low dword, nought for an infinite value), and both gates read
    it: the arrival's `QueueTooLong` and the `InQueue` turn's arm 5a, which puts a queuer out "by the capacity".
    `QUEUE_TOO_LONG_CAPACITY` and `QUEUE_CAPACITY_RECHECK` retire; a tier past the third and an item the catalogue
    lacks are counted (`QUEUE_CAPACITY_UPGRADE_TIER`, `QUEUE_CAPACITY_UNKNOWN_ITEM`) and let through. `spend` prints
    each thing's `longest`.
  - **Verified first** (`wf_82a1de67-786`, three Ghidra re-reads, each told to refute): every claim held, three
    sharpened - the float stores' order, `__ftol` keeping the low dword, and the renderer's third exit (64-bit when
    `[0x008bd508]`'s `0x2` bit is set), which `park-engine.md` missed; exactly two callers, both unsigned; the loader's
    type-7 float. Measured: 217 declarations over all 312 wads and the loose files, whole numbers 3 to 250; all 17
    jungle rides with a queue state their own (the Belly Bounce 130, 135, 145), so `Rides.sam`'s 30 reaches none. At 53
    and 64 bits the answer is the exact one on all 6,769,800 slider settings; at 24 bits 83,454 answer one more (Lost
    Kingdom 21,708 of 1,690,500), each with the speed off its tier's.
  - **Reviewed** (`wf_5e6e1825-06f`, four lenses, each finding put to a skeptic): 35 findings, 30 upheld (about 15
    distinct), 5 refuted; no behaviour wrong. Fixed: the Belly Bounce's thresholds (a duration of 41, not 44), a stale
    sentence on the arrival's gate, the comments' claims about the asserts, the census asking a counted path, the
    loader's grammar moved from FileFormats to `park-engine.md`, and four tests strengthened.
  - **Tests** (10 new, each number predicted first): the key over the category at all three tiers; the catalogue's
    constants; the saved Belly Bounce 21; capacity 2 8, 1 4, nought 4, speed nought 21, tier 1 22, tier 2 24; the float
    R (6, not 7), the unsigned speed (1550960493) and the low 32 bits (3579141840); nought for a duration or `InitSpeed`
    of nought, 4 for nought over nought and for no constant; a queue too long at 21 and unsigned; an arrival at 8 turned
    away and at 7 let in; places 22 and 9 put out, 21 and 8 kept; `spend` 21 and 100. **Mutations**
    (`q173-mutate.py`, `.out`, `mutate2.out`, `mutate3.out` on the final tree): 27 put back, all red, 24 exactly as
    predicted and 3 with one more red than predicted.
  - **Confirmed in the game** (`q173run.py`, silent, jungle, predicted first, `save/` unchanged in all four runs): at
    load `spend` read the Belly Bounce's `longest 21` and the Spray's 100, and neither retired counter appeared at load
    or after running; its capacity clicked from 5 to 2 in the ride window read `longest 8`. Run 3 (`q173-run3/`, 7 of
    8): of the three standing past place 8 at the change, two were put out by the capacity, each at place 9, and one
    was saved by a boarding, as predicted; the queue settled at 9, places 0 to 8; 13 arrivals were turned away as too
    long, every one at a count of 8 or more. Photographed paused before (12 on the bridge) and after (9)
    (`before-marked.png`, `after-marked.png`). The control on `main` (`q173-control/`, 8 of 8): the same change puts
    nobody out and turns nobody away, the queue stays at 12, and the two counters count (6,434 re-checks).
  - **Predictions missed:** runs 1 and 2 (`q173-run1/`, `q173-run2/`, 6 of 8 each) and run 3's settled count. Each miss
    was my model of the cascade, not the build: arm 5a asks only a guest in their recorded place, and each departure
    moves the rest up one, so the next drifts out a delay of 1.2 × their place before re-taking it and going; and a
    boarding that saves one leaves the count at 9. Every guest past place 8 in runs 1 and 2 went in turn (2 of 2, 3
    of 3), at 9, 21 and 33 s in run 2.
  - **Not confirmed on screen:** a tier past the third, an unknown item, a duration or `InitSpeed` of nought (none in
    shipped data), and the 24-bit margin: tested only.

  The item as written: Found by Q165c's review.
  `FUN_004dda40` is `trunc( max( capacity × QueueWaitTimeConstant × speed / InitSpeed / duration, 4.0f ) )` at the
  ride's tier (`ride-operation.md`, "The `InQueue` turn"), every input now named: `+0x1b4` is
  `Upgrades[l].QueueWaitTimeConstant` by the compiled `.sam` schema (`Rides.sam` 30 at tier nought), which the catalogue
  does not read. Two gates count it: the arrival's too-long gate (`QUEUE_TOO_LONG_CAPACITY`) and the `InQueue` turn's
  re-check (`QUEUE_CAPACITY_RECHECK`), both reached by the Belly Bounce. Read the key, compute it with the float stores
  the disassembly shows (`0x004dda56`..`0x004ddb51`), and build both. Confirm: `unimplemented` without the two, and the
  Belly Bounce's capacity in a census, predicted first.
- [x] **Q174. Two animation-state differences in the triggers: the decode.** Done 2026-09-29,
  `alexah/181-decode-the-animation-waits`. Decode only; the build is Q174b. `park.md`, "The animation opcodes" (its
  table, `LOOPANIM`'s guard, `+0xe4`, the `TRIGWAITANIM` re-entry) and its new "Where OpenTPW's animation state parts
  from the engine's"; the channel section corrected (the held flag, the take-over gates, "finished" as the last advance
  left it, an entry past a role's count, both advance routes, the idle default that never fires on a thing's model, a
  new build's frozen role 0); `ride-operation.md`'s hold and its `RSYS` restore; `park-engine.md`, the clock's save and
  restore. FileFormats `saves.md`, the script struct's `0xa0`, `0xa4`, `0xa8`, `0xbc` and `0xe4` and the saved clock
  (`docs/save-module-chain`), and `vm/instructions.md` (`docs/rsse-instruction-set`). Comments corrected at their sites;
  no code changed.
  - **Decoded** (`wf_05e40d85-135`): two read-only decoders, each put to a skeptic in Ghidra (68 verdicts: 50 upheld, 18
    amended, none refuted; 15 points the decoders missed), two corpus walkers written apart that agree site for site,
    and a reach measure over all 133 `TRIGWAITANIM`s with each target clip's length. Re-read first-hand: the re-entry
    at `0x00552cfc`..`0x00552d11` and `FUN_00473fb0`, `WAITANIM`'s first visit to `0x00552b1a` on both paths,
    `TRIGWAITANIM`'s inline writes, `WAIT4ANIM`'s clear, the loader's `0xffff`, the scheduler's `+0xe4` push and its
    three floats, the build's frozen role 0, the idle default's gate, the `0xf9` clears and `FUN_00473e30`'s `0x10`;
    the Easymode records measured again with my own reader (`~/.cache/tpw-harnesses/q174/mysave.py`, where the
    decode's JSON, walkers and reach scripts are kept too).
  - **The answer.** `WAITANIM`'s writes are reached: 55 `LOOPANIM`s skip where the engine starts the loop again, 7 in
    Lost Kingdom, and the Aztec Mayhem's at the end of every ride; its `+0xa4` half only at space `hoverbot` @259,
    invisible. `TRIGWAITANIM`'s raw role is not reached at a normal frame rate: a held re-entry needs a target clip under
    about 2200 ms queued and played out between two turns; in Lost Kingdom only `Monkey` 202 (1999 ms) comes near,
    after one frame of 58 ticks or more, and across the corpus 8 uses can, the nearest Hallowe'en's `Firework` 67 after
    one of 20 ticks or more. Found beside them (`park.md`, differences 3 to 5): the trigger's `MoveTo` passes a
    `TRIGWAITANIM` a turn early at a loop's cycle end (8 Lost Kingdom uses); a load restores none of `+0xa0`, `+0xa4`,
    `+0xa8` and `+0xbc` and restarts every saved channel (the Easymode cameras' waits and the Belly Bounce's key 2 on
    every load; six scripts in Alexah's jungle `New Save.TPWS`); and a start keeps no stale held bit (no Lost Kingdom
    path).
  - **Measured in the game** (the build before any change; throwaway `q174-instrument.py`, which keeps the engine's
    fields beside OpenTPW's and counts where they decide apart; `q174run.py`, silent, the stock jungle park, predicted
    first; `save/` unchanged; `q174-run1/`): a bought Aztec Mayhem, its queue joined, rode three cycles and counted
    `LPdivSkip` 3, one at word 17 after each `WAITANIM 6, 0`, and stood on role 6 held (`0x14`) between rides; no
    `TRIGWAITANIM` split over the ferry (3 first visits, 3 re-entries), the seaplane (3, 3), a Mammoth Fountain (26,
    60) and a Lava Fountain (21, 48); no early take-over and no `WAIT4ANIM` split. Photographed paused beside that
    census (`C2-held-paused-z70-marked.png`), and the fountains (`C3-1405.png`, `C3-1421.png`).
  - **Predictions wrong**: the load reading, where I predicted no split, already held the Belly Bounce's one
    `LPdivStart`, because the park ran 2.5 s before the harness paused it; I had predicted that split later, in the
    running stock park. The same check also failed on the ferry and seaplane, which it counted with the fourteen saved
    scripts; their first `TRIGWAITANIM` had run in those 2.5 s. Two fountain checks failed in the harness, which looked
    them up by thing id, not script id; their rows read as predicted.
  - **Not confirmed on screen**: the skip itself, since the Aztec Mayhem's loop and its hold look alike (a frame pair's
    difference in its box is 0.20 looping and 0.44 held, noise); the raw role's held re-entry and the early pass, which
    nothing reached; the save's deadlines in Alexah's save, which waits to load (Q167).
  - **No test was added**: nothing was built.
  - **Reviewed** (`wf_aabdaf8f-b16`, five read-only lenses, each put to a skeptic): 53 findings, 30 upheld, 19 amended,
    4 refuted, and 14 more the skeptics raised; all taken. Among them: the queue split is 15 and 17, not 16 and 16; a
    load also loses `+0xa0` and restarts every saved channel; the save's restore had no build item; a fifth difference;
    stale copies in `ParkRides`, `ParkObjects`, `RideAnimations`, a test's comment and the FileFormats pages.
  - **Found:** Q174b, Q174c; a note under Q155. Seen, not chased: `ParkRides` binds a fresh `RideAnimations.Load` for a
    placed thing `ParkObjects` did not stand, and nothing advances that player, so a clip queued on it never starts;
    whether any live thing takes it is not measured.

  The item as written: Found by Q166's decode. (1) `TRIGWAITANIM`'s
  re-entry compares channel 0's role raw (`FUN_00473fb0`, `0x00552cfc`..`0x00552d0f`), where
  `RideScript.TriggerAndWaitForAnimation` asks `RoleOn`, which answers -1 for a channel holding its pose (flag `0x4`,
  which among the script handlers only `GETANIM` and `GETANIM_CH` test, `0x00552e05`, `0x0055374e`; the trigger
  `FUN_004732a0` reads it to count a held channel free, `0x00473326`): a clip held when the wait re-enters lets the
  engine go on and keeps OpenTPW waiting. (2) `WAITANIM`'s first visit zeroes `+0xa4` and sets `+0xa8` to `0xffff`
  (`0x00552b14`, `0x00552b1a`), which `WaitOutAnimation` does not, so a `WAIT4ANIM` after it waits on an older trigger's
  deadline and a `LOOPANIM` of the key last looped is skipped. Measure whether shipped content reaches either (133
  `TRIGWAITANIM`, 547 `WAITANIM`), then build. Confirm: `rides` over a Lost Kingdom `TRIGWAITANIM` ride through a cycle.
- [x] **Q174b. A `WAITANIM`'s two writes, `TRIGWAITANIM`'s raw role, and a trigger that asks the channel as the last
  frame left it.** Done 2026-09-29, `alexah/182-the-animation-waits-as-the-engine`. `RideScript.WaitOutAnimation`'s
  first visit clears `_animationUntil` and sets `_looping` to `OneShot`, model or not (`0x00552b14`, `0x00552b1a`);
  `TriggerAndWaitForAnimation`'s re-entry reads channel 0's role raw through the new `AnimationOn` (`FUN_00473fb0`,
  `0x00552cfc`..`0x00552d11`), `GETANIM_CH` keeping `RoleOn`; `RideAnimations.Trigger` no longer brings the channel up to
  the tick (`0x00473315`, the remainder `0x0047337b`). `ParkRides.PlayersFor` binds all three sites and logs a player
  nothing advances; the `rides` census says ` LOOP` and ` then R/E`. `park.md`'s differences say the first three are
  matched, keep what content reaches each, and gain a sixth (below).
  - **Measured in the game** (silent, `save/` unchanged within each run; `q174brun.py`, each reading predicted first;
    the stock park loaded under a lobby pause and stepped 3600 then 7200 frames, so builds meet at ticks 1939 and 5810;
    then an Aztec Mayhem bought at (57,23), its queue laid, `load 40`, two rides). The build before (`46fee5f`,
    `q174b-control/`): after its ride channel 0 stood on `role 6 entry 0 frame 50.0/50.0 HELD` in every census to the
    end of the run. This one (`q174b-new/`, with a throwaway instrument logging each place it decides apart from the
    build before): after the first ride `0:role 2 entry 0 frame 12.3/50.0 LOOP`, and after the second, held on role 6
    while the riders walked off (words 105-147), then `LOOP` again at `LOOPANIM 2, 0`; the instrument logged one restart
    a ride at word 20, and no `TRIGWAITANIM`, trigger or `WAIT4ANIM` decided apart anywhere. The commit's own build
    (`q174b-commit/`, no instrument) read the same: `0:role 2 entry 0 frame 21.8/50.0 LOOP` after the first ride, loop
    again after the second, photographed paused beside the census (`q174b-commit/C2-paused-z70.png`); and the stock
    park's 14 things read the role, entry and hold of the build before at both ticks.
  - **Predictions wrong**: I predicted the stock park's frames equal at tick 1939 as well, as if no guest were in it;
    the save holds guests, whose choices are unseeded, and the Belly Bounce read frame 22.7 against 11.8. At 5810 the
    Belly Bounce and the Jungle Spray differed in role too. The instrument's nought said none of the three changes acted
    there, and the commit's own run, the same code less the instrument, read both as the build before: between runs.
  - **Tests**: five new (`ATriggerAsksTheChannelAsTheLastSweepLeftIt`, `TheAztecMayhemLoopsAgainAfterItsRide`,
    `TriggerAndWaitPassesAChannelHeldOnItsRole`, `AnAnimationWaitForgetsTheDeadlineATriggerLeft`,
    `AnAnimationWaitLetsTheSameLoopBeAskedForAgain`), and the ferry's queue test given the sweep it leaned on the
    `MoveTo` for. Each change put back alone turns exactly its own tests red: the deadline 1, the key 2, `RoleOn` 1, the
    `MoveTo` 1 (`q174b-mutate.py`, `q174b/mutate.out`).
  - **Not confirmed on screen**: the loop itself. The Aztec Mayhem's moving parts are inside its pyramid, so the
    photograph (`q174b-new/C2-paused-z70.png`, beside the census) shows the ride and the census shows the loop, as Q174
    found. The raw re-entry and the trigger on the last frame's state: no run reached either, tested only.
  - **Reviewed** (`wf_0b758377-379`, three read-only lenses, each finding put to an Opus skeptic): 7 findings, 6 upheld,
    1 refuted, all taken: `STATUS.md` still said the Aztec Mayhem does not loop; two comments still carried the
    overshoot onto a trigger; `ParkRides`' summary said a bound
    script has no playing channel; difference 2 had dropped the zero-frame clip; "every channel once a frame" overstated
    the decode (a coaster's car models are also advanced from inside the loop, `0x004311f0`; checked first-hand); and a
    triggered clip's start stamp is the tick's instant where the engine's is the frame's snapshot (`0x00472bff`), named
    at `RideScript.StartAnimation` and as `park.md`'s difference 6.
  - **Found:** a note under Q174c (difference 6).

  The item as written: Found by Q174's decode (`park.md`, "Where OpenTPW's animation state parts from the engine's",
  differences 1 to 3). (1) `WaitOutAnimation`'s first visit, model or not, sets `_animationUntil` to null and `_looping`
  to `OneShot` after its deadline (`0x00552b14`, `0x00552b1a`); the re-entry writes neither. (2)
  `TriggerAndWaitForAnimation`'s re-entry compares channel 0's `AnimID` plus one against the mark, not `RoleOn`'s
  (`0x00552d01`..`0x00552d11`); `GETANIM_CH` keeps `RoleOn`. (3) `RideAnimations.Trigger` decides free or queued on the
  channel as the last frame's advance left it, without its `MoveTo` (`0x00473315`). Confirm, each predicted first: the
  Aztec Mayhem through a ride cycle, `rides` showing its `channels [0:role 2 ...]` looping between rides, where
  Q174's run read `[0:role 6 entry 0 frame 50.0/50.0 HELD]` (`q174-run1/rides-C-end.txt`), photographed beside it; the
  stock park's `rides` channel rows unchanged in role and flags; for (2), a test with channel 0 held on the marked role
  at a re-entry that passes it, red with the change reverted; for (3), a test triggering onto a clip that ran out
  after the last advance and finding the new one queued, red with the `MoveTo` put back.
- [x] **Q174c. A loaded script's four saved fields and its channels' timebase.** Done 2026-09-29,
  `alexah/183-restore-a-scripts-waits-and-channels`. `ParkRides.Resume` puts back a saved script's `+0xa0`, `+0xa4`,
  `+0xa8`, `+0xbc` and `+0xc4` (`RideScript.RestoreClockState`), and `ParkRides.Restore` each channel's three stamps and
  its queue (`AnimTimeControl.Restamp`, `AnimTimeControl.Queue`), every reading moved by its distance from the save's
  clock (new `ParkClock`, `KOLC`'s first dword) onto the load's moment, `GameClock.Ticks` x 31 (`ParkRides.Moved`). The
  restored frame is the restore's own, the span times 0.03 divided by the speed (`0x00472cf4`), until the next advance.
  `SavedScript` and `SavedChannel` carry the new fields. `+0xc4` is a fifth field beside the item's four: the same
  struct, the same clock, set in Alexah's saves. The key and the mark come back only with the thing's channels, so a
  save whose channel module will not read leaves its loops fresh. `park.md` difference 4 says it is matched.
  - **Decoded first** (`wf_ff945b38-6f1`: three Opus decoders in Ghidra, each put to an Opus skeptic; every answer
    upheld, with corrections taken): the `RSYS` copy of all eleven saved dwords (`0x00464bcb`..`0x00464c17`) and
    `FUN_00472cb0`'s divided frame; the clock restore of both `KOLC` dwords (`0x00415193`) and the first frame's re-base
    of `last` (`0x0054f425`), so no catch-up; the five script fields read back raw, compared unsigned (`JC`), `GETTIMER`
    signed (`JNS`); scripts' deadlines and channel stamps on one clock (`FUN_004031e0`); an offline park, Instant
    Action's included, loaded in mode 2. Written to `park.md` (difference 4, the scheduler), `park-engine.md` (the clock
    table, whose two sources were swapped, and the rate keys), `ride-operation.md` and `boot.md`; FileFormats
    `saves.md` (`KOLC`, the script module's header, `+0xc4`, the channel's eleven dwords), `29385a1` on
    `docs/save-module-chain`.
  - **Measured in the game** (silent, `save/` unchanged within each run; `q174crun.py`, every reading predicted first,
    7 of 7 on each build; the stock park loaded under a lobby pause, stepped in frames of 1/60 s). This build
    (`q174c-new/`): at the load the Belly Bounce's channel 0 read `role 2 entry 0 frame 45.4/90.0 LOOP` (1,376 ms x 1.1
    x 0.03) and the Fountain's 48.8 of 50; the Belly Bounce 55.6 at tick 10 and 41.3 at tick 84, round once at 1.1;
    no `then` in 48 readings over ticks 10 to 60; both cameras `role 6 ... HELD` at tick 75 and `role 4` at tick 84,
    3.7 and 4.7 frames in. The build before (`fc63e0f`, `q174c-before/`): 4.1 and 3.7 at the load, `then 2/0` from tick
    36 to 60, the cameras held on 6 at tick 84 and on 4 only by tick 176. Photographed paused at the same step on both
    builds (`q174c/L0-bouncy-paused-pair.png`, `q174c/C2-camera-paused-pair.png`): the Belly Bounce's pod stands open
    wider, and the camera's box has risen on its post, where the census says each should.
  - **Predictions wrong**: the build before's Belly Bounce, whose `WAIT 500` I said would pass near ticks 17 to 25;
    500 ms is three of its one-in-eight turns, so it passed at 28 and queued at 36. In the tests, the pass through word 43
    comes a turn after the wait passes, since `CRIT_UNLOCK` at word 99 ends that turn; and a restored held frame stands a
    hair under its total (the saved stamps are whole milliseconds) until the next advance.
  - **Tests**: thirteen new (`TheSavedClockAndEachScriptsWaitsRead`, `TheSavedChannelsTimeStampsAndQueuesRead`,
    `ALoadedCameraWaitsOutOnlyWhatItsSaveHadLeft`, `TheLoadsMomentIsTheClockTheParksTicksRunOn`,
    `ALoadedBellyBounceKeepsTheLoopItsSaveWasRunning`, `EverySavedWaitMarkTimerAndQueueComesBack` on a copy of the shipped
    park with a trigger deadline, mark, timer and queue written in, `ALoadWhoseChannelsWillNotReadKeepsItsLoopsFresh` on
    one whose channel module is spoiled, two for `Restamp`, three for `RestoreClockState` and one for a restored
    `TRIGWAITANIM` mark); the Fountain's frame in `ABoundScriptResumesWhereTheSaveLeftIt`, and
    `TheParksScriptsRunWhenTheyAreGivenTurns` driven from the load's moment. Each change put back alone turns its
    tests red, 16 of 16 as predicted (`q174c-mutate.py`, `q174c/mutate2.out`); a first round before the review ran 13, 12
    as predicted, the timer read from the wrong dword also reddening the copy-of-the-park test that reads it.
  - **Not confirmed on screen**: the mark, the timer and a queued channel, which the shipped park does not save, and a
    load whose channels will not read (tested only); Alexah's saves, which wait for Q167.
  - **Reviewed** (`wf_eb68d867-c39`, three read-only Opus lenses, each finding put to an Opus skeptic): 21 findings, 19
    upheld, 2 refuted; all 19 taken. Among them: the key and the mark restored for a thing whose channels were not, so a
    refused channel module (Alexah's jungle saves) would leave the Belly Bounce's `LOOPANIM` doing nothing over an idle
    channel; `TheParksScriptsRunWhenTheyAreGivenTurns` still ran from nought and no longer reached a restored wait; the
    load-moment test's float arithmetic and order; a saved nought, the queue's flags and speed and the Bus's third stamp
    unpinned; `+0xc4` missing from FileFormats; stale comments (`ChannelDwords`, the toilets' pairing, `+0xa4`'s
    clearers, `Moved`'s "of representation only"); `park.md`'s "zeroes"; Q181's count.
  - **Found:** Q180, Q181, Q182 (the note from Q174b's review, moved), a note under Q167.

  The item as written: Found by Q174's decode and review
  (`park.md`, "Where OpenTPW's animation state parts from the engine's", difference 4; `ride-operation.md`, the `RSYS`
  restore). The engine reads each script's whole struct back and restores its channels as saved; OpenTPW's
  `ParkRides.Resume` restores neither `+0xa0` (a `WAIT`'s or `WAITANIM`'s deadline), `+0xa4`, `+0xa8` nor `+0xbc`, and
  `ParkRides.Restore` restarts each channel at frame nought at the load, dropping its queue. Read the four at the
  boundary (FileFormats `saves.md`), rebase each deadline as now plus (saved deadline less the saved clock, `KOLC`'s
  first dword), and restore each channel's stamps and queue against that clock. The shipped park reaches it on every
  load: the cameras' `WAIT 5000` at word 14 with 2,341 and 2,329 ms left, the Belly Bounce's `WAIT 500` at word 46 with
  63, and its saved key 2 at its `LOOPANIM 2, 0` at word 43. Confirm, predicted first: the Belly Bounce's `LOOPANIM 2,
  0` no longer triggering after a load, and the cameras' first `WAITANIM` about 2.3 s after their first turn, not 5 s.
- [x] **Q175. A rider's walk leg: the decode.** Done 2026-09-29, `alexah/186-decode-the-walk-legs`. Decode only; the
  build is Q175b. `ride-operation.md`, "The WALK family": the slot table, the function rows (`FUN_00557ab0` once a
  frame for every script, `FUN_00557d80`'s arrival restamp, `FUN_005580a0`'s one write) and its new "How long a leg
  lasts, and where its ends are"; "Where a rider is drawn" and "The BOUNCE family" corrected (the engine finds a
  rider's node by id in `0x800`, and the node base starts at 1); `audio.md`, `park.md` and `park-engine.md` brought
  into line. FileFormats (`alexah/186-decode-the-walk-legs`): `models.md`, header `0x78` the root node, the `0x0C`
  pointer and "Which records have a position"; `sam.md`, `Info.DoHeadProcessing`; `saves.md`, the ride scripts' walk
  slots and the ride system's per-record flags; `vm/instructions.md`, `WALKON`, `WALKOFF` and `BOUNCESETNODE`. Comments
  corrected at their sites (`RideScript`, `ParkPeople`, `ModelFile`, `ParkThingStates`); no code changed.
  - **Decoded** (`wf_ad8db498-b32`): three Opus decoders in Ghidra (the matrices, the clock and stepper, the model a
    script's nodes are looked up on), each put to an Opus skeptic (61 claims: 48 upheld, 12 amended, 1 refuted, none
    changing an answer), and two corpus walkers written apart. The Sonnet walker scaled the distance without truncating
    it (1165 for 1100); its node positions agree with the Opus walker's, whose legs are the doc's. Re-read first-hand:
    `FUN_00556b90`, `FUN_00556f40` to its `FSQRT` and `__ftol`, `FUN_005571a0`, `FUN_00557d80`, `FUN_0044b220`,
    `FUN_0044a870`, `FUN_0044aa10`, `FUN_0044ab30`'s identity root, `FUN_004702d0`, the loader's `+0x84` choice.
  - **The answer.** A leg is trunc( the distance between its two nodes ) × 100 ms, nought becoming 100, each way; the
    positions are the nodes' posed matrices, in the world, which the pose walk stores for a childless node only under
    runtime bit 8 (the loader's `0x580f00` mask, or every record of a `DoHeadProcessing` item), 2 or 4, else (0, 0, 0).
    Every one of the 240 records the shipped walk instructions can name is stored; the (0, 0, 0) end and the miss
    (which reads the stack) are dead by CONTENT. `DoHeadProcessing` is the item's `+0x84` by the compiled `.sam`
    schema, set by six items; no category file sets it.
  - **Measured in the original** (the reference install under Proton, off-screen, its stock Lost Kingdom park, the
    Jungle Spray's script found in the script list; `~/.cache/tpw-harnesses/q175/`), predicted first from the model:
    lanes 1 and 3 1100 ms each way, lane 2 700. 151 transitions over 16 minutes, every one as predicted: lane 1 13 on
    and 13 off at 1100, lane 2 59 and 60 at 700, lane 3 3 and 3 at 1100; facings 7, 0, 1 on and 3, 4, 5 off, as the
    decoded formula gives; all 75 arrivals restamped start. The walk nodes' runtime flags read `0x29` and their world
    positions the rest positions moved by (510.073, 0, 299.904); the `camera` record `0x21`, at (0, 0, 0). The park
    clock was NOT TRUE (1.55× at the clock check and 1.65× over the two watches, 6.8 days up), which moves no stored leg:
    start and due are read back to back, so due less start is the leg to within one clock step whatever the clock's rate,
    and every leg read was exact. Every walk
    slot in Alexah's Full Simulation saves agrees too: the Steak Shop 600, the Inca God's walk off 800, the Hyenas 1000
    and 400, the Gift and Balloon Shops 1000, the Jungle Spray 700 and 1100, and in Wonder Land the Big Apple 2100,
    SquirtEm 700, 200 and 700, Frushy 800. Those saves also refuted the decoders' first reading that the Aztec
    Mayhem's `0xb1` heads are never posed: all 39 of its records save bit 8 (`0x29`, or `0x2b` on the five heads
    carrying a rider), which led to `DoHeadProcessing`.
  - **Measured in the game** (the build before any change, with a throwaway instrument `q175-instrument.py` that logs
    each leg; `q175run.py`, silent, the stock jungle park, `load 40`, camera on the Jungle Spray; `q175-run2/`), predicted
    first: every leg 100, each way. Two riders in seven minutes, lanes 2 and 1, where the original's are 700 and 1100:
    all four legs 100, each arrival and each walk off's end noticed at the script's next turn, 248 ms after the leg
    began (148 ms past due). Photographed paused with the
    lane 2 rider carried beside the census line (`carried-paused-marked.png`, `walks-carried.txt`); nothing here places
    a walk-on rider, so they stand where the queue left them. `save/` unchanged.
  - **Not confirmed on screen**: the original's legs were read from its memory, not photographed (its off-screen
    picture is not trustworthy, `TOOLING.md`); lane 3 was seen live but is in no save; the Lookout, Totem and Aztec
    Mayhem legs, whose heads ride an animated ancestor, are from the rest pose only.
  - **No test was added**: nothing was built.
  - **Reviewed** (`wf_6936f8f2-ff8`, three read-only Opus lenses - Ghidra, the numbers, what else goes stale - each put
    to an Opus skeptic): 41 findings, 25 upheld, 16 amended, none refuted, and 11 more the skeptics raised; all taken.
    Among them: the Belly Bounce's node base starts at 1, not nought (`0x00558c45`), so its riders are ids 1 to 10,
    which the engine finds by id in `0x800`, not by name; the ride view and `FUN_0044b510` also touch the stored
    matrices; `FUN_005580a0` writes a carried rider's facing; the walk off here takes `WalkTick` by the fallback, not
    by keeping the walk on's leg; the Inca God's 24 slots were 12 saved twice; every shipped `WALKON` passes flags 1.
  - **Found:** Q175b (the build), Q183 (the stepper's cadence). `tpwmem.py watch` never printed a change (a buffered
    reader serves a repeat read from its cache): fixed in the harness, which is not in the repo.

  The item as written: A rider's walk off keeps the walk on's leg, where the original's works out its own. Found by
  Q166's decode (`WALKON`) and its refuter (`WALKOFF`). `FUN_00556f40` (`WALKON`) sets the slot's due time to now +
  trunc( the distance from the walk node to the head node ) × 100, nought becoming 100 (`0x00556fce`..`0x005570af`), and
  `FUN_005571a0` (`WALKOFF`) a new leg, the distance from the off-from node to the off-to node, the same way
  (`0x00557276`..`0x005572db`). `RideScript.WalkOn` gives every leg `WalkTick`, the deviation `WalkTick` declares
  because no model node can be resolved by id, and `WalkOff` keeps that leg. Resolve the nodes first (`FUN_00556b90`
  against the ride's model, space `0x800` for a walk node and `0x80` for a head node; Q22 needs the same), then build
  both legs from their positions. Confirm: `rides` over a Jungle Spray lane's walk, each due time predicted from its
  two nodes.
- [x] **Q175b. A rider's walk leg from its two nodes: the build.** Done 2026-09-29, `alexah/187-walk-legs-from-the-nodes`.
  `RideScript.Leg` gives each leg trunc( the distance between its two nodes ) × 100, nought becoming 100, `WALKON` walk
  node to head node and `WALKOFF` off-from to off-to, worked out before the slot search as the engine does; `WalkTick` is
  gone and `WalkFloor` (100) is the floor. `ModelFile.FindNode` is `FUN_0044b220` (the first record with the id whose
  flags share a bit with the mask; a mask sharing none with `0x3da1f83` becomes `0x3da1f82`). `RideNodes` holds the
  thing's own model, `Info.DoHeadProcessing` (read now) and the nodes its clips move or morph, stood at the thing's cell
  and turn: each node composed from the root down in floats in the engine's order, the root's file rows turned by the
  engine's table at its own two indices and its translation replaced by the placement. The slot keeps the flags and its
  node ids and action as 16 bits; `rides` prints each walk slot and, while walked, its leg; each leg is logged. Counted,
  not built: `WALK_LEG_REST_POSE` (a head on a moving part stands at rest), `WALK_NODE_ON_A_FACE`, `WALK_NODE_MISS`,
  `WALK_NODE_UNPOSED`, `WALK_NODE_NEGATIVE_ID`, `WALK_NODES_NO_MODEL` (the engine's stepper does not survive a walking
  script without a model). `ride-operation.md` ("How long a leg lasts") and `audio.md` carry the facts, `TOOLING.md`
  11-13 the instrument's; FileFormats `models.md` the roots' own transforms (`alexah/187-walk-legs-from-the-nodes`).
  - **Measured in the original first** (the reference install under Proton, read from memory; `~/.cache/tpw-harnesses/
    q175b/`, `simwatch.py`, `nodedump.py`, `PREDICT-original.txt`), predicted from the rest pose before each read: an
    Aztec Mayhem bought in the stock park at (40, 22), on screen, walked on in 1400, 1300, 900, 1000 and 2000 ms by head,
    15 times over three boardings, and off in 2000, 900, 1300, 1700 and 1500, 10 times, every one as predicted; its heads
    were at rest at the start of each walk. Alexah's jungle save, loaded from a copy of her player folder: the pose stored
    at the load of the 49 walk and head records of its eight walk things (turns 0, 180, 270) equals `RideNodes` in x and
    z to six decimals, 98 values, and in y but for ground height; 31 of the Inca God's 32 heads read (0, 0, 0), as
    `WALK_NODE_UNPOSED` says, and the one `ADDHEAD` attached a rider to held a position. The park then stalls
    (`TOOLING.md` 13). Getting there took llvmpipe's 0.01 frames a second and a crash under an Instant Action player
    (`TOOLING.md` 11, 12).
  - **Measured in the game** (`q175brun.py`, silent, the stock park, a Laughing Hyenas bought at (46, 29) and a Strength
    Bird at (49, 29), camera over the three sideshows; runs `q175b-run1/` with `load 40` and `q175b-run2/` with `load 80`,
    the second from a build of this source but for one doc comment), predicted first, legs read from the new log line:
    the Jungle Spray's lane 2, 700 on and 700 off, three times; the Hyenas' lane 2, 400 each way, once; the Strength
    Bird, 500 each way, six times; 20 legs, all as predicted, none 100. Photographed paused with the census beside each:
    `Jungle Spray Sideshow ... walking 1: 0:128 WalkingOn 4->2 leg 700` with the rider in the spray's middle lane
    (`q175b-run2/spray-walking-marked.png`), and likewise the Hyenas' and the Strength Bird's. No `WALK_` count was
    reached; `save/` unchanged both runs. Not seen in the game: lanes 1 and 3 of the Jungle Spray and the Hyenas (1100,
    1000), which fill only when riders overlap - no two did in 40 minutes, as lane 2 took 59 of the original's 75 walks;
    the shipped Jungle Spray script walks all three lanes in `EachJungleSprayLaneWalksItsOwnNodesDistanceEachWay`.
  - **Tests**: `RideNodesTests` (the original's stored positions at three turns, the Aztec Mayhem's heads at rest, an
    unposed record, a miss and a negative id, an id in its own space and the mask swap, the quarter-turn sines, the Fire
    Pit's face head), four in `RideScriptWalkTests` (the Jungle Spray's three lanes each way, the Aztec Mayhem's legs as
    the original walked them, the Totem's walk off from its own node, no model and a miss and a nought-unit walk at the
    floor, the Rat Race's world-float leg) and `ParkRidesTests` (the bound Jungle Spray's entrance where the original held
    it, the Drinks Shop given no nodes). Put back one at a time (`q175b-mutate.py`, `q175b/mutate2.out`): 19 of 22 red as
    predicted; composing in floats, squaring x and z unrounded and taking the cosine a quarter on stay green, changing no
    shipped position or leg.
  - **Not confirmed**: the Rat Race's 1000 (worked out, hallow); a ride left off screen mid-cycle (the engine's frozen
    pose); a turn off the quarter; y on raised ground against the original (x and z only); the Lookout and the Totem in
    the original (worked out, 0.094 and 0.063 from rest); the build's FPU precision.
  - **Reviewed** (`wf_1948cf78-e76`, three read-only Opus lenses - the engine in Ghidra, the code and tests, the docs -
    each put to an Opus skeptic): all taken. Among them: the engine has no model-less walker, so the floor there is
    OpenTPW's and counted; the action is 16 bits; the composition's order and the placement's two table indices are the
    engine's; the census printed a false leg for a carried or finished slot; the walk counts were wrong in the doc.
  - **Found:** nothing new filed; a note under Q93 (the original's bought ride opens itself once its line joins).

  The item as written: Found by Q175's decode (`ride-operation.md`, "How
  long a leg lasts, and where its ends are"). `RideScript.WalkOn` gives every leg `WalkTick`, 100 ms, and `WalkOff`
  gives the walk off `WalkTick` too; the engine's leg is trunc( the distance between the two nodes ) × 100, nought becoming 100, each way. Look
  each node up on the thing's own model (`<stem>.md2`) as `FUN_0044b220` does: the first lookup record with that id
  whose flags share a bit with `0x800` (a walk node), or `0x80` for the head and the walk off's first node when the
  action is 4 (`ModelFile.ReadNodeIds` already keeps each node's id and flags). Take its position as the model stands
  posed in the world, `WALKON` walk node to head node, `WALKOFF` off-from to off-to; keep the flags operand. The
  (0, 0, 0) end and the miss are dead by content: count them (`Unimplemented.Report`), do not build them. Q22 needs
  the same lookup. Confirm, predicted: the Jungle Spray's lanes 1 and 3 1100 ms each way and lane 2 700, the Hyenas
  1000, 400 and 1000, Squark 500, in a census beside a photograph of a rider on a lane; and one of the three whose
  heads ride an animated ancestor (Lookout, Totem, the Aztec Mayhem) measured against the original first.
- [x] **Q184. Console commands that put a guest where a test needs one.** Done 2026-09-29, `alexah/189-guest-console-commands`.
  `admit <x> <y> [kind 0-7]` makes a guest through `ParkPeople.Admit` and then does what `Entering`'s arrival does
  (`PeepBehaviour.AdmitAsEntered`: paid, numbered a visitor, `Deciding`), so they stand on that cell deciding. `send
  <guest> <thing>` is `PeepBehaviour.SendAsChosen`: the ride arm of `Decide` with `Choose` skipped - the route to the
  back of queue and `MajorDest` are `SetOffFor`, the half `ChooseSomewhereToGo` now shares, then `GoingToRide`. It
  sends only a guest in `Deciding` or `Wandering`, to a thing the park has, with a route. No player reaches either.
  - **Confirmed** (`q184run.py`, runs `q184-run1..3/`, save/ unchanged each): predicted and read, `admit 55 30 3` x3
    "deciding at (55,30)" and `send <id> 14` x3 "going to thing 14", `peeps` then `GoingToRide dest 14 aim
    (52.500,29.500)`; the three board the Jungle Spray and walk 4->1 1100, 4->2 700, 4->3 1100 and back the same, 6 of 6
    in the log, and `rides` paused on each 1100 leg shows `44 WalkingOn 4->1 leg 1100`, `45 WalkingOn 4->3 leg 1100`,
    `44 WalkingOff 1->4 leg 1100`, `45 WalkingOff 3->4 leg 1100`, each beside a photograph of the riders on the Spray
    (`q184-run3/spray-*-lane{1,3}.png`, `crops.png`). A run using them proves the thing's side, not the guest's choice.
  - **Tests** `ParkConsoleGuestTests` (4). Put back one at a time: no `GoingToRide`, no route, no `AdmitAsEntered`, no
    state guard, not paid - each red.
  - **Not confirmed**: a still shows where a rider stands, not how long a leg lasts; the durations are the census's.
  - **Found, not filed:** `ParkPeople.Admit` counts a visitor on arrival and `Entering` counts them again, where
    `PeepBehaviour.VisitorsToDate`'s own note says only `Entering` counts (a test pins the arrival's count).

  The item as written: Asked for by Alexah 2026-09-29, after
  Q175b's game runs waited 40 minutes for two riders to overlap on a sideshow and never saw its lanes 1 and 3.
  `arrive <x> <y>` (`ParkPeople.Admit`) makes a guest on any cell, but starts them `AtGate`, to walk to a booth and
  pay, and nothing sends a guest to a chosen thing: a guest reaches a ride only by its own choice (`PeepBehaviour`,
  the original's score, `ride-operation.md`, "What a thing is worth to a guest"). Add, in `DebugConsole` only, a guest
  made already admitted and `Deciding` on a chosen cell, of a chosen kind; and a command that sends a named guest to a
  named thing through the same path a guest takes once it has chosen, so that only the choice is skipped. Both are
  instruments: nothing a player reaches changes, and a run using them proves the thing's side, not the guest's
  choice, and says so. Confirm, predicted first: three guests sent to the stock park's Jungle Spray at once, and the
  `rides` census over its lanes 1 and 3 walking 1100 ms each way beside a photograph of the riders.
- [x] **Q176. Two latent differences in the VM's draw and the second scream.** Done 2026-09-29,
  `alexah/190-the-draw-and-the-second-scream`. `RideScript.NextDraw` hands the generator's `0x80000000` back as it is
  and halves it unsigned, as `FUN_00516330` (`0x0051635f`) and both opcodes' `SHR 1` (`0x0055398f`, `0x005560d0`) do:
  that state draws `0x40000000`, where `Math.Abs` threw. A second `STARTSCREAM` over a held scream is refused and the
  held chain let go (`ParkScreams.LetGo`): it screams on held by nothing, the script's `STOPSCREAM` and its removal
  miss it, and the park's end stops it. `RideScript.Screaming` asks `ParkAudio` what the script holds rather than
  keeping a copy, and the `rides` header counts `screams let go`. The review moved the stop of a held scream from
  the scheduler's teardown of relations into `Release`, the flat destructor's own work (`FUN_00558500`), so a child
  or sound script removed flat stops its scream too.
  - **Measured** (`q176/screamwalk.py`: every path of all 308 `.RSE` from word 0, an exact `JSR` stack, the held bit
    per path): none of the 40 `STARTSCREAM`s is reached holding a scream; the control, a `STOPSCREAM` that keeps it,
    finds 40 of 40. A reviewer's walker, written apart, agrees site for site. The save reader leaves `+0xd0` as saved
    (no `+0xd0` operand among `FUN_005597a0`'s 758 instructions; its `+0xd4` write is found), and the 12 scripts
    Alexah's Full Simulation saves hold with a handle each resume onto a path that stops first, so no load refuses
    (`ride-operation.md`, "How a scream VARIES").
  - **Confirmed** (`q176run.py`, run `q176-run3/` after a one-cycle `q176-run2/`, save/ unchanged each), predicted
    first: the Belly Bounce through three cycles, guests 43-45 made by `admit 48 22 3` and sent by `send`. With each
    rider on, `rides` read `bouncing 1` and `screaming True scream [effect 71 band 1 level 20 ...]` and the log `band 1
    effect 71 volume 35`; with each off, `stopped screaming after 14-16 sample(s)`, `band 0 ... screams not at all`,
    `screaming False scream [none]`; `screams let go 0` at all eight readings, 23 of 23 checks. Photographed paused:
    `q176-run3/05-band1.png` with the rider on the ride's belly, `06-band0.png` without (`crops.png`). Again from the
    commit's own build (`q176-commit/`, `q176-commit.out`): 23 of 23, save/ unchanged.
  - **One prediction wrong in form**: I predicted bands 1, 2 and 3 as the three boarded together. The Belly Bounce
    took one at a time (`Invite` reads no `RunsContinuously`, Q157), so every cycle was band 1, then 0.
  - **The let-go in the running game** (`q176run-letgo.py`, a throwaway build whose Belly Bounce `STOPSCREAM` does
    nothing; run `q176-letgo/`, stdout `q176-letgo.out`, save/ unchanged), predicted first: each rider's leaving had
    its band-0 start refused with the new warning naming effect 71, and `screams let go` read one higher at the next
    reading, 1 to 11 over the 11 riders of 15 minutes, with `screaming False` at the end; the chains let go made 3,308
    children "held by nothing", the first at its 440th as the game quit. 59 of 60: the miss is the harness, waiting
    for a band-0 line a refusal does not print.
  - **Tests**: `RideScriptClockTests.TheStateWithNoPositiveTwinDrawsAQuarterOfTheRange` and
    `RideScriptRelativeTests.TheStateWithNoPositiveTwinPicksWhatAQuarterOfTheRangePicks`, seeded by the new
    `SeedRandom`; `ParkScreamChainTests.ASecondStartLetsTheFirstScreamGoOnHeldByNothing` and
    `ADyingScriptStopsItsScreamAndItsChildsButNotOneLetGo`, where the chain test's old "keeps the one it holds" is
    gone. Put back one at a time (`q176/q176-mutate.py`, `q176/mutate2.out`): `Math.Abs`, no halving, a refusal that
    keeps, stops or replaces the first, a pump or a park's end that skips the chains let go, the stop left in the
    teardown of relations or taken out, and a script answering from elsewhere, 10 red as predicted; a signed halving
    stays green, as predicted, since both callers take the remainder's absolute value.
  - **Reviewed** by four read-only Opus agents (the engine in Ghidra, the measurement, the code and tests, the record),
    each finding put to an Opus skeptic (`wf_7887c2c1-02b`): 26 findings, 23 upheld and taken, 3 refuted. Among them:
    the draw's state is unreachable from seed 1; the flat removal's scream; the `STARTSCREAM` row's operand and
    string; the options' stop of every voice; FileFormats' `STOPSCREAM`, which cuts a held scream and never fades it.
  - **Not confirmed on screen**: the draw (tested only, and unreachable in a run: every script starts at seed 1, whose
    cycle of 248,316,293 states never meets `0x80000000`); the let-go only in the throwaway build; a child's scream
    stopped by its removal (no shipped child screams); a restart from one held band to another, since the Belly
    Bounce takes one rider at a time here (`Invite` reads no `RunsContinuously`, Q157).

  The item as written: Found by Q166's decode. (1) `NextDraw`
  takes `Math.Abs` of the generator's state, which throws for `0x80000000`; `FUN_00516330` hands that back unchanged
  (`0x0051635f`) and `RAND` and `FINDSCRIPTRAND` halve it to `0x40000000` (`ride-operation.md` already records it). (2)
  A second `STARTSCREAM` while one is held: the engine refuses it and stores the refusal's nought over `+0xd0`
  (`0x00555ee6`), so the first scream plays on and a later `STOPSCREAM` finds nothing to stop (`0x00555efd`);
  `ParkAudio.Scream` keeps the chain reachable, so a later stop ends it. Both are said at their sites. Measure whether
  any shipped script starts a scream over a held one, then build both. Confirm: a test for each, and `rides` over the
  Belly Bounce through a cycle.
- [x] **Q177. The settle-up's bookkeeping, counted and not kept: the decode.** Done 2026-09-29,
  `alexah/191-decode-the-settle-up`. Decode only; the build is Q177b to Q177e. `ride-operation.md`'s new "The
  settle-up's bookkeeping" (the step table, the cost of goods and the park's money, the six day rings, the join's
  snapshot, the event history, thoughts 5 and 6, the analyser's samples, what Lost Kingdom reaches, both measurements,
  where OpenTPW differs); "Spending" and "The effects of a visit" corrected (the challenge posts that were called a
  tally, the analyser's accumulators that were called the world's, the two independent docks, sugar's unclamped
  `mAdjustorSpeed`, the balloon and costume arms whole); "`+0x1f1` at the settle-up is the win roll" replaces "not
  settled"; the win roll draws the park's generator, not `rand()`; the guest and object field tables. `advisor-park.md`:
  the advisor also polls (one of 156 rows a tick), the metadata filler found at `0x005a0a50`, a row's `+0x24` is the
  first response, not a sample. `hud.md` and `park-engine.md` brought into line. FileFormats `saves.md`: the object
  record's six rings placed by the serialiser (the even split was a guess and holds), and the park analyser's twenty
  rings. Comments corrected at their sites (`ParkRideOperation`, `ParkState`, `ParkObjectWindow`); no code changed.
  - **Decoded** (`wf_1c25d211-f5e`): five Opus decoders in Ghidra (the money, the since-join and counts, the analyser,
    the events and thoughts, the ingredient and appearance arms), each put to an Opus skeptic (165 verdicts: 145
    upheld, 19 amended, 1 refuted, and 36 points the decoders missed), a Sonnet data sweep of the level's 67 items and
    the stock save, and an Opus critic that settled the gate and found the golden ticket, the toilet window and the
    dead advisor rows. One decoder also scanned with `objdump`; its skeptic re-checked every claim in Ghidra. Re-read
    first-hand: `FUN_004e1920`, `FUN_004d01f0`, `FUN_004e1690`, `FUN_004e1e00`, `FUN_004e19f0`, `FUN_004e1b40` to its
    `__ftol`, `0x004fd9d8`..`0x004fda66`, the thought gate `0x0050c039`, the win roll `0x004e26c6`, `FUN_004e16b0`'s
    analyser and challenge posts, `Advisor_SayResponse`'s lookup.
  - **The answer.** Every settle-up counts the visit (`FUN_004e1690`, before the gate) and the guest's rides, purchases
    or sideshows; on the effects arm it books a shop's or a won sideshow's cost of goods against the object and
    withdraws it from the bank (gated on `mWithdrawalsEnabled`, 1 in every offline park), averages 3 × the happiness
    gained since the join into the object's day of satisfaction, counts it served, and posts a sample nothing reads.
    What a player sees of it: the money (a drink nets +10, a won spray play −30), the profit and customer figures of
    the object windows and the all-items screen, the customer satisfaction gauges, map and advisor lines, the visitor
    window's four counts, and a sideshow player's thumbs-up or thumbs-down bubble; the event ring and the analyser's
    settle-up samples reach nothing. `+0x1f1` is the win roll for every object; all but a sideshow always win.
  - **Measured in the original** (the reference install under Proton, off-screen, its stock Lost Kingdom park;
    `~/.cache/tpw-harnesses/q177/origread.py`, `orig/watch1.log`, `orig/analyse.py`, `orig/analyse1.out`): 814 changes
    over 7 minutes, 145 settle-ups, the 47 with one guest moving checked, 254 of 257 checks as decoded; the three others
    were the harness's (a stale thirst snapshot, one poll holding a settle-up and a day's roll). 16 drinks each +10 to
    the balance with costs +20, takings +30, satisfaction by `FUN_004e1e00` and kind 12's `3d + 50`; 9 lost spray plays
    +20 with thought 6 and a live bubble; 4 won plays (read from their polls) −30, costs +50, thought 5; 8 toilet visits, customers and served
    +1, event `0x11`, no guest count; 14 Belly Bounce rides. The six watched objects' satisfaction cursors stepped together each game
    day and wrapped at 30, today's figures going to nought (walk-aways not read). The last frame's HUD read 88070, the balance in memory (`orig/s05.png`).
  - **Measured in the game** (the build before any change; `q177run.py`, silent, the stock jungle park, three guests
    made inside and sent, predicted first; `save/` unchanged; `q177-run1/`): each settle-up counter's rise between two
    censuses as the interval's log lines predict, four intervals of four (the spray play, both drinks in one, the toilet, an empty tail); 6 of the run's 7 checks matched;
    `money` balance 88112 and takings 125 on both sides of both drinks; the drinker thirst 80 to 40, happiness 0 to 5,
    cash 700 to 670 (the original's: thirst 60, happiness 7); the toilet user's need to 0. Photographed paused beside
    the census: the HUD at 88112 before and after the drinks (`C-spray.png`, `A-drinks.png`).
  - **Predictions wrong**: my check "money unchanged across the spray play" failed on five gate fees in the same
    interval (takings +125; balance less takings 87987 throughout). The critic's "profit this year is zeroed on entering
    a park" was refuted by the original's memory (−12013 saved, −11888 after five fees). The original's checks were
    written from the decode after its first minute had been glanced at, before any drink or spray block was read.
  - **Not confirmed on screen**: the original's bubbles (a live sprite handle in memory, not photographed; off-screen
    pictures are memory-grade, `TOOLING.md`); the original's +10 as a before and after pair on its HUD; every window,
    screen and advisor line that reads the figures (none was opened); the challenge posts (off in Easymode), the
    park's end after 180 days in the red and golden ticket 4 (game type 0). In OpenTPW the lost spray player was
    photographed from the Drinks Shop's camera, so the missing bubble shows only as nothing drawn anywhere.
  - **No test was added**: nothing was built.
  - **Reviewed** (`wf_a566e319-b6e`, four read-only lenses - the new section in Ghidra, the edits in Ghidra, every
    number against its evidence file, what else goes stale - each put to an Opus skeptic): 28 findings, 18 upheld, 10
    amended, none refuted, and 28 more the skeptics raised; all taken. Among them: I had counted two won spray plays
    and fifteen rides where the log holds four and fourteen (a grep for `MATCH` also matched `MISMATCH`); the red
    balance's stamp re-arms at any withdrawal that stays at nought or more; the advisor also counts the months in the
    red (rows 103 to 105); row 247 scores walk-aways, not satisfaction; the scrap value's 100 needs no customer yet as
    well as youth; the advisor's metadata table resolves every screen's message statically (the map's `0x130` to
    sample 582); stale copies in `ParkWorld`, `ItemDescriptionFile`, `ParkItemsScreen`, `ParkLines`, `ParkGadget`,
    `ParkHireScreen`, `ParkPathBuilding`, `ParkBuilding`, a test's summary, `PLAYER-GAPS.md` and `hud.md`; and a shipped
    object (thing 15) whose unreached ring entries hold `0xCDCDCDCD`.
  - **Found:** Q177b, Q177c, Q177d, Q177e; notes under Q31, Q96, Q97 and Q110. `UIStrings` 37 to 39 are one row off
    (UITEXT 37 is "Scrap value", 38 "Local happiness", 39 "Quality of goods", 40 "Sale price"); nothing uses the three
    members yet (Q31's note). `RIDE_USERS_LAST_MONTH` is now readable from the save (Q177b).

  The item as written: Found by Q169's review and its check
  of the fixes; each piece is counted now (`SETTLE_UP_*`). (1) A shop's and a sideshow's cost of goods, booked against
  the object by `FUN_004e1920` (`0x004fe225`, `0x004fe251`; its `+0xf8`/`+0x184` and a negated ledger post) and debited
  from the park's balance (`FUN_004d01f0` at `0x004e1952`, only while the bank's `+0x114` is non-zero), the mirror of
  Q96's deposit. (2) The guest's event history (`FUN_0050c100`: 8 at `0x004fe204`, 0x11, 0x18, 0x19). (3) A toilet
  emptying the toilet need (`0x004fe7b6`), which bears on Q170. (4) After `FUN_004fe1e0`, three times the change in
  happiness since the join's snapshot at `+0x20c` (written at `0x004ffd92`; `ride-operation.md`, the join), logged
  "Happiness changed by %d since using object %d", averaged into the object (`FUN_004e1e00`, `+0x340`) and, for a shop
  or sideshow, posted plus 50 as an event (`0x004fda1b`..`0x004fdb2c`). (5) The object's visit count
  (`FUN_004e19f0`, `0x004fdb33`) and a sideshow's thoughts 5 and 6 (`0x004fdb66`, `0x004fdc25`). Decode what reads
  `+0x340` and the event and who else writes `+0x20c` (`FUN_005179c0`, `0x0051861c`), then build what the park
  reaches; the toilet first. Confirm: `unimplemented` without them, a toilet visit's `peeps`, and the money line.
  From Q170: part 3, the toilet, is decoded whole (`ride-operation.md`, "The effects of a visit", step 5) and is the
  first step of Q170b. `FUN_004fe1e0` also pushes events `0xc` (`0x004fe78a`) and `0x12` (`0x004fe7ea`, the toilet's
  illness arm), which part 2 does not list.
  From Q170b: part 3 is built (`UseTheToilet`). Also counted now and not built: the `+0x198` happiness terms, the
  special-ingredient switch (`0x004fe527`; the Drinks Shop's ice puts 20 of its 40 thirst back at the stock amount 50)
  and the appearance arm (`ride-operation.md`, "The effects of a visit", 3b). `+0x198` is `mAmountOfSpecialIngredient`
  (file 1058), which `ParkWorld` does not read.
  From Q171: the constructor sets a bought thing's `+0x198` and its `+0x18c` (`mQualityOfGoods`) to 50
  (`0x004db3b3`, `0x004db389`); `ParkBuilding.Constructed` writes neither, as the record carries neither.
- [x] **Q177b. The settle-up's counts: the visit, the guest's three, the object's day rings and satisfaction.** Done
  2026-09-30, `alexah/192-the-settle-ups-counts`. The settle-up now keeps steps 1, 2, 4, 5 and 7 and a sideshow
  winner's count: the guest's `mNumRides`, `mNumShops`, `mNumSideshows` and `mNumSideshowsWon` (read from the record at
  444..456, `Peep`), the object's customers before the gate, three times the happiness gained since the join averaged
  into the day's satisfaction (`FUN_004e1e00`), and its served. Each object's six day rings and two counts are
  `ParkObjectRings`, read from its record (`ParkWorld.ReadRings`, 228..1033), fresh for a thing built, dropped with a
  thing sold, rolled on the day's change after the frame's turns (`ParkState.RollTheDay`, logging "The day's change");
  the charge credits today's takings and the door's refusal counts the walk-away (`FUN_004e1670`). The join copies
  happiness (`Peep.JoinHappiness`, not saved). The ride window's Users last month is the thirty finished days'
  customers, refilled every four seconds while open as the original's timer does; the all-visitors list's Rides
  Ridden is `mNumRides`. Console: `rings [thing]`, and `peeps` prints the four counts and the join. Newly counted
  (reached, unbuilt): `SETTLE_UP_ANALYSER_SAMPLE`, `CHARGE_SOUND`, `CHARGE_ANALYSER_MONTH_TOTAL`, `CHARGE_CHALLENGE_POST`,
  `DOOR_EVENT_HISTORY`; gone: `SETTLE_UP_OBJECT_VISIT_COUNT`, `SETTLE_UP_HAPPINESS_SINCE_JOIN`, `DOOR_WALK_AWAY_COUNT`,
  `RIDE_USERS_LAST_MONTH`, `VISITOR_RIDES_RIDDEN`. Built and tested alone in a worktree: 1367 pass, 0 skip with the
  game; 537 ran, 830 skipped without; 123 warnings.
  - **Mapped** (`wf_99b6262c-a37`): three Opus readers in Ghidra (the arithmetic, the rings and the day's change, the
    join's snapshot), each put to an Opus skeptic, and a Sonnet map of the code; reports in
    `~/.cache/tpw-harnesses/q177b/map/`. Found beside the build: the settle-up's `+0xe8` is `FatigueEffect`, taken off
    `mTiredness`, not a need relieved (a no-op: nothing raises it); the roll's order and signed wrap; the day test
    compares the day of the month only, after every thing's turn, in ascending thing id; Users last month adds the
    last min( filled, 30 ) finished days as unsigned figures, never today; a walk-away is counted only by the door's
    price refusal; the window refills on a 4000 ms timer (`0x004af67b`); the all-visitors row adder's six sources
    (`hud.md`).
  - **Measured across the data**: all nine park files to hand (the shipped one and Alexah's eight) hold 30 in every
    ring and close each record on 1034; the C# reader agrees with the independent Python walk on every one
    (`~/.cache/tpw-harnesses/q177bsave/`, `q177/satisfaction/objrings.py`: jungle thing 236, 150 customers, 56 in the
    last thirty days; guest totals 1703, 1378, 1065, 624). 287 of 339 played guests hold counts; none has won more
    sideshows than it played.
  - **Confirmed in the game** (silent, the stock jungle park, `save/` unchanged in every run; predicted first):
    run 1 (`q177brun.py`, `q177b-run1/`): the ride window over the Belly Bounce printed **Users last month 5**, the
    census's `last30` 5 (photographed, `window-users.png`), the logged changes 45 (a kind 3 joined at 40, cheered
    15) and 15; the all-visitors list's Rides Ridden 1 on the two guests `peeps` counts one ride (`visitors.png`).
    Run 3 (`q177brun2.py`, `q177b-run3/`, six guests at the Drinks Shop): day 7 held two drinks, 99 then 105, and the
    census's satisfaction for it read **102**; every finished day matched the fold of its logged changes; customers
    and served over thirty days 8, as logged. Run 4 (`q177brun4.py`, `q177b-run4/`): the same open window went from
    Users last month **0 to 1** (Age 32 to 42) 1.26 s after the day's change, never reopened, fills every 4.0 s
    (`users-before-after.png`).
  - **Predictions wrong**: in run 1, my last-thirty count added every finished day where 43 had passed (the game's 5
    is right: day 11's visit is past thirty); the kind 1 never rode in time (one rider a cycle, about 5.5 days); in
    run 3, my "every later drinker logs 105" missed that guests joined either side of the `happy 70`.
  - **Mutation** (`~/.cache/tpw-harnesses/q177b/mutate.py`, `mutate.out`): 34 mutants over four rounds, each red
    against the tests that stood when it ran, except the two guards against adding an unfilled day, each equivalent
    alone and red together; one hollow spot (a saved count reaching the guest) found and closed.
  - **Reviewed** (`wf_ea5275a7-590`: the code against Ghidra, the C# and its tests, the docs, what else went stale;
    each put to an Opus skeptic; reports in `~/.cache/tpw-harnesses/q177b/review/`): all taken. Among them: the
    window's 4-second refresh (built), the charge's sound and a shop's or sideshow's analyser totals and challenge
    posts and the door's event 10 (reached, now counted), tests that could not tell the six rings or the guests'
    offsets apart (now patched-payload tests), and stale text in `ParkItemsScreen`, `ParkMapScreen`,
    `ParkRideRecordTests`, `ParkVisitorsScreen`, `PLAYER-GAPS.md` and `hud.md`.
  - **Not confirmed on screen**: satisfaction (no screen here shows it: the shop and sideshow windows and the map's
    layers are unbuilt; census only); the walk-away (tested only; no guest was refused on price in the runs); a
    saved park's counts (tested on a patched payload; no load of a played save).
  - **Found:** the rings roll on `GameCalendar`'s edge, which counts from nought (a note under Q149).

  The item as written: Found
  by Q177's decode (`ride-operation.md`, "The settle-up's bookkeeping", steps 1, 2, 4, 5 and 7). OpenTPW neither
  builds nor counts `FUN_004e1690` (the object's `mNumCustomers` and today's customers, every settle-up, before the
  gate) nor the guest's `mNumRides`, `mNumShops` and `mNumSideshows` (`CLAUDE.md` rule 4): count them first. Then read
  the six rings and the two counts from the object record (`saves.md`, file 228 to 1033), roll them on the day's change
  (message `0xb`), keep `+0x20c` at the queue's join (not saved: nought after a load), and build steps 4, 5 and 7 and
  `mNumSideshowsWon`; `SETTLE_UP_OBJECT_VISIT_COUNT` stands for step 7, the served count, and wants that name. The ride
  window's Users last month (`RIDE_USERS_LAST_MONTH`) is then the thirty days' customers. Confirm: the ride window's
  Users last month on the Belly Bounce after its riders, photographed, beside a census of its rings; each
  satisfaction day as `FUN_004e1e00` makes it (measured in the original: 18 and 18 to 18, 45 and 15 to 30).
- [x] **Q177c. A shop's and a won sideshow's cost of goods, booked and withdrawn; with Q96.** Done 2026-09-30,
  `alexah/194-cost-of-goods-and-deposit`, with Q96. Every payment is now the bank's own: `ParkState.Spend` is the
  withdrawal `FUN_004d01f0` at every site that pays (purchase, path and queue cells, the drain, a dismissal, a sale's
  cost of goods, a sold coaster's nought), gated on `WithdrawalsEnabled` and moving `LastBalance`, `TurnEnteredRed` and
  `ProfitThisYear`; `ParkState.Refund` is now `Deposit` (`FUN_004d0190`: the balance and the profit), which the charge
  calls (Q96); `Take`, the gate fee, adds the profit too. `ParkState.BookCostOfGoods` is `FUN_004e1920` (today's costs,
  `mTotalCosts`, then the withdrawal) and `ParkRideOperation.ShopCostOfGoods` is `FUN_004e1b40` (the terms as floats,
  the sum in double, `__ftol`'s low dword); the settle-up books a won sideshow's cost before its prize and a shop's
  amount. `ParkWorld` reads object file 1046, 1058 and 1086 and a bought thing starts at 50 and 50; the four bank
  fields are seeded from the save; `GameCalendar` makes the month's and the year's compares, and the year's change
  zeroes the profit (`TurnTheYear`). Console: `money` prints profit, last, red and withdrawals; `rings` and `spend` an
  object's total costs; the bank logs every movement. Gone: `SETTLE_UP_COST_OF_GOODS_BOOKING`, `CHARGE_BANK_DEPOSIT`,
  `QUEUE_DRAIN_DEBIT_BANK_GATE`. Newly counted: `BANK_ANALYSER_MONEY_IN`, `_MONEY_OUT`, `GATE_FEE_ANALYSER_TOTALS`,
  `GATE_FEE_CHALLENGE_POST`, `COST_OF_GOODS_CHALLENGE_POST`, `BANK_MONTH_TURN`, `STAFF_MONTHLY_WAGE`,
  `STAFF_MONTHLY_TRAINING`, `PURCHASE_GOLDEN_TICKET_ARM`, `SALE_TRACK_TEARDOWN` (Q198). Built and tested alone in a
  worktree: 1394 pass, 0 skip with the game; 549 ran, 845 skipped without; 123 warnings.
  - **Mapped** (`wf_245f2fbd-cf9`: three Opus readers in Ghidra and a Sonnet code map, each put to an Opus skeptic;
    `~/.cache/tpw-harnesses/q177c/map/`): every `Spend` caller is the withdrawal and every `Refund` caller the deposit
    in the original, so the gate belongs in `Spend`, not at the drain alone; the gate fee's five steps; every caller
    of the bank; the inlined bank bodies in the month turn and the loans.
  - **Measured across the data** (`q177c/objmoney.py`, all nine park files): the per-sale cost `FUN_004e1b40` gives
    matches what the original booked in Alexah's saves (Drinks Shop at quality 0 and amount 100: 10 a sale, 5,240 over
    524; 37 for 30 at quality 100; 22, 37, 10), and the shipped park holds 50 and 50 on all fourteen objects.
  - **Confirmed in the game** (silent, the stock jungle park, `save/` unchanged; predicted first): run 1
    (`q177crun.py`, `q177c-run1/`, 23 of 23) and run 2 on the final build (`q177crun2.py`, `q177c-run2/`, 26 of 26).
    Two guests sent to the Drinks Shop, each held and stepped six frames at a time while inside: the HUD read
    **88132 then 88142**, and **88142 then 88152**, across one drink each alone in its chunk (`balance-pairs.png`,
    `run2-evidence.png`), and `money` the same, profit +10, last the new balance, takings unchanged. Over the run the
    balance moved +165 as the logged charges, gate fees and costs predict; after five gate fees the year's profit read
    **−11888**, the figure measured in the original's memory. At the first month's change the gadget read 2/1/2000
    (predicted as 1/2/2000: it prints the month first) and `unimplemented` counted `BANK_MONTH_TURN` 1 and a wage and a
    training share for each of the 5 staff.
  - **Mutation** (`q177c/mutate.py`, `mutate.out`, `mutate3.log`): three rounds; two hollow spots found and closed (the
    save's withdrawal flag, a shop's amount off 50 and 50); the final round, 57 mutants against the committed tree,
    each red.
  - **Reviewed** (`wf_5b5068ab-a8f`: four lenses - the code against Ghidra, the C# and its tests, the docs, what else
    went stale - each finding put to an Opus skeptic; `q177c/review/findings.json`): 26 findings, 5 upheld, 21
    amended, none refuted; all taken. Among them: `__ftol` keeps a low dword where a double-to-int cast saturates; the
    sale's track arm (a coaster withdraws nought); the golden-ticket arm, the month turn, the wage and training
    reached and uncounted; the year's change is message `0xd`, not the month turn; the ingredient's clamp and a ride
    booking nothing untested; the severance guard; stale test wording.
  - **Not confirmed on screen**: the year's change (a year is 35 minutes; tested only), withdrawals off, the red stamp,
    a sold coaster's nought and the ticket count (tested only); a won Jungle Spray's −30 (both runs' plays were lost:
    +20, logged).
  - **Found:** Q198 (the month turn, the wage and training; counted); a note under Q141 (the buy list's mystery row);
    notes under Q97 (the played parks' chances) and Q177d (its two fields are read now).

  The item as written: Found by Q177's decode
  ("The cost of goods and the park's money"). `FUN_004e1920`: the object's today's costs and `mTotalCosts` (file
  1086), then `FUN_004d01f0` on the bank, gated on `mWithdrawalsEnabled` (bank file 28), which moves `mBalance`,
  `mTurnEnteredRed`, `mLastBalance`, the analyser's month costs and `mProfitThisYear`; a shop's amount from
  `FUN_004e1b40` (quality and ingredient, file 1046 and 1058), a sideshow's its `mCostOfGoods`. The challenge post stays
  counted (off in Easymode). Land it with Q96's deposit or after it: alone it takes 20 a drink off the HUD and puts
  nothing in. The queue drain's debit (`ParkState.Spend`) wants the same gate (`QUEUE_DRAIN_DEBIT_BANK_GATE`).
  Confirm: the HUD before and after a drink, +10 as the original's, photographed; `money`.
- [x] **Q177d. The ingredient's and the quality's terms of a visit.** Done 2026-09-30,
  `alexah/195-ingredient-and-walking-speed`. `ParkRideOperation.TakeTheIngredient` runs after the five effects: the
  hunger dock then the thirst dock (a draw for each non-zero effect, whether or not it can fire; `(r & 7)` + the
  amount + the effect under 30, unsigned, docks `SmallHappinessChange`'s low byte), the amount times `HappinessEffect`
  over a hundred, and fat (toilet need), salt (thirst), ice (the amount times `ThirstEffect` over a hundred back to
  thirst) and sugar (the amount times six over a hundred into `Peep.AdjustorSpeed`); it logs each visit's terms. The
  draws are the ride turn's `System.Random`, where the original's are the park's one generator (said at the site).
  Sugar made the walking speed part of the item: `Peep.Pace` is `FUN_004fa870`'s first half and `FUN_00510190` for
  guests, every sweep before the stamp (the three words summed over 100, eased a quarter of the way in single
  precision, held to 2.0, the mover's speed and force truncated and at least 655; the sugar's word to 99 hundredths),
  and `PeepWalk.Step` reads the navigator's speed and force afresh every step. `ParkWorld.PaceState` reads the person
  base's four speed words (file 32, 34, 220, 236) for everyone; an arrival is made at a base drawn `% 5` from 60..140,
  hurrying at 25 from a standstill, and `Admit` no longer refuses a park with no guests (the guard was for the speed it
  copied). Staff are not eased (Q136): a hire walks at a rested member's 1.4 (18350), no longer copying anyone. The
  census prints the speed words, the eased speed and the mover's speed; `Peep.Pace` logs each sweep while sugar is in
  the word. Gone: `SETTLE_UP_INGREDIENT_HAPPINESS`, `SETTLE_UP_SPECIAL_INGREDIENT`. Built and tested alone in a
  worktree: 1418 pass, 0 skip with the game; 556 ran, 862 skipped without; 123 warnings.
  - **Decoded again first-hand** (`wf_f501c35b-cc7`: two Opus decoders in Ghidra, a code map and a content sweep, each
    put to an Opus skeptic): the docks and switch at `0x004fe41c`..`0x004fe615`, `FUN_004fa870`, `FUN_00510190`, the
    four fields' load arm, the arrival's draw; the save's `max_speed` and `max_force` are exactly the eased speed
    truncated on all 18 people of the shipped park and all 392 of each of Alexah's two played Lost Kingdom saves.
    Every arm is a jungle shop (Burger fat, Fries salt, Drinks ice, Ice Cream sugar), buyable here now.
  - **Mutation** (`q177d-mut/mutate.py`): 33 mutants, each red, among them the unsigned compare, the low three bits,
    the draw order, 0 and 5 acting as an arm, the offsets 32, 34, 220 and 236, the sweep's call, the arrival's
    words, the hire's speed and the walk keeping its planned speed.
  - **Reviewed** (`wf_4e09ebe1-b64`: four lenses, each finding put to an Opus skeptic; `review.json` in the session's
    scratchpad): 28 findings, several one fault seen by two lenses; 27 upheld or amended and each acted on (the run for
    the bus queued, Q199), 1 refuted. Among them: the toilet's hurry deviation no
    longer exists (the speed now reads the hurry before the needs turn resets it, as the original's does); the hire's
    copied speed; two hollow tests; stale comments and docs; the address index.
  - **Confirmed in the game** (silent, the stock jungle park, `save/` unchanged; predicted first): before the build
    (`q177drun.py`, `q177d-before/`) a drinker at thirst 36 and happiness 50 left at **0 and 55**; after it
    (`q177d-after/`, 10 of 10) at **20 and 57**, the original's measured drink, in `peeps`, in the terms' own log line
    (`ingredient 3 at 50: happiness 55 to 57 (0 docked), thirst 0 to 20`) and on the all-visitors list, the
    drinker's row **700 / 50** then **670 / 57** (`1-visitors-before-marked.png`, `2-visitors-after-marked.png`). The
    Drinks Shop sold and an Ice Cream Shop bought in its place (`q177d-sugar/`, 5 of 5): an arrival at base 140 took
    **5406** on its first sweep, as predicted; settled at 1.4 it bought an ice cream (`adjustor 0 to 3`) and the next
    three sweeps logged speeds **18448, 18489, 18487** at 3, 2 and 1, as predicted from 1.4, then none; the shop
    photographed serving (`3a-inside-the-ice-cream-shop-marked.png`, `3-ice-cream-shop.png`, the HUD 88112 then 88122).
  - **Not confirmed on screen**: a dock firing (none can at the stock amount, and nothing here moves the amount; tested
    only), fat and salt (tested only), the speed's hold at 2.0 and floor, and a hire's speed (tested only).
  - **Found:** Q199 (the run for the bus, now a speed), Q200 (the all-visitors list jumps back to its top); notes under
    Q136 and Q177e.

  The item as written: Found by Q177's decode ("The effects of a
  visit", 3 and 3b). Read `mQualityOfGoods` and `mAmountOfSpecialIngredient` (file 1046 and 1058; 50 on a bought
  thing) and build the two independent docks, the `amount × HappinessEffect / 100` term, the four ingredient arms and
  `mAdjustorSpeed` in the walking speed (`FUN_004fa870`); counted now as `SETTLE_UP_INGREDIENT_HAPPINESS` and
  `SETTLE_UP_SPECIAL_INGREDIENT`. Each non-zero hunger or thirst effect takes a draw of the park's one generator,
  which OpenTPW does not share (each behaviour keeps its own): say so at the site. Confirm: a drink at the Drinks
  Shop, the drinker's `peeps` thirst 20 back and happiness 2 more, as the original's (measured: 36 to 20, 50 to 57).
  From Q177c: both fields are read now (`ParkWorld.CatalogueObject.QualityOfGoods`, `AmountOfSpecialIngredient`; 50 on
  a bought thing), and the terms read their low byte, as the shop's cost of goods does.
- [x] **Q177e. Balloons.** Done 2026-09-30, `alexah/196-balloons`. The item was two: the costume is Q177f, below. A
  Balloon Shop's winner is given a balloon (`ParkRideOperation.GiveABalloon`, `FUN_004fe1e0`'s arm) in the colour the
  guest's id gives (`Balloon.ColourFor`: the park's generator reseeded with the id; `ParkGenerator`, now shared with
  `RideScript`'s `RAND`) and a life of the shop's quality x 255 / 100 held to 25..255, 127 for a bought shop.
  `Peep.SetState` puts it away on boarding and lets it go on entering state 17; `Dismiss` builds it again on leaving
  anything but a balloon shop, same colour, same life. `Peep.Tick` takes one off the life a needs sweep outside states
  16 and 17 on a cell of type 0, 1, 3, 9 or 10 (`Peep.CountsOn`, of the cell the park links the guest into), counting
  the thought picker's draw there (`NEEDS_THOUGHT_PICKER`); at nought the same sprite goes on the let-go script
  (`0x0074f4c0`: frame 1, the burst, thirteen turns from alpha 250 down by 20, then gone), which `SpriteScript` now
  runs (`SubLocal`, `LoopStart`, `LoopWhile`, the end word; and a frame of -1 hides and yields, as `0x0047698c`
  does). A departing guest's goes with them, unburst. `ParkGuestSprites` packs the balloon bank and places each held
  balloon every frame as `FUN_004fa030` does (trailing 0.35 of a sweep and a frame, lower by twice the trail, a shared
  bob stepping every eleven placements, kept across parks as the original's globals are, taken at 30 a second on the
  frame clock, said at the site) and draws the bursts; its banks are numbered in name order, as the loader sorts them. The save's `mBalloonScript`, `mRemainingBalloonLife` and `mLastPosX`/`Y` are read, and a saved balloon is the
  kind-10 sprite its slot names. Console: `balloon <life>` (an instrument, as `thirst` is); `peeps` prints the life
  and picture, `guests` each balloon's place after the people; a let-go is logged. Counted: the event (`SETTLE_UP_BALLOON_EVENT`), the
  costume (`SETTLE_UP_COSTUME`).
  - **Decoded** (`wf_19cae377-f55`: two Opus decoders in Ghidra, each put to an Opus checker, every claim upheld or
    amended; `ride-operation.md`, "A held balloon" and "A costume"): frame 1 of a balloon set is the burst (the item's
    open question); the costume-head callers of `FUN_0044b410` draw the rider's own head; leaving the park deletes a
    balloon unburst, which the page had wrong; a challenge reads the balloons.
  - **Measured:** the colour by id matches all 29 and 28 balloons in Alexah's two played jungle saves and 11 and 11 in
    the fantasy ones, read through this build's own reader (`q177e-colour`; in the jungle the first draw instead matches
    10, an id one higher 4, against 7.25 by chance), none of the 79 bursting; the bank, read with the repo's readers
    (`q177e-bank`: red, green, blue, yellow; body alpha 218); all 21 sprite folders listed in name order
    (`q177e-order`); the shipped park's guests all stand on type-30 cells, which do not count.
  - **Tests:** `ParkBalloonTests`, 25. **Mutation** (`q177e-mut/mutate.py` on the final tree, `mutate-final.out`): 59 mutants in one run, 58 red;
    the green one, `LoopWhile`'s `>=` as `>`, cannot differ on any copied script (the alpha never lands on nought).
  - **Reviewed** (`wf_5e26ddcc-6fb`: three lenses - the code against Ghidra, the C# and its tests, the docs and what
    went stale - each finding put to an Opus skeptic): 35 findings, 29 upheld or amended and each acted on, 6 refuted.
    Among them: the bob's phase restarted with each park (now static, as the original's globals); the `guests` header
    counted balloons as guests; a doc comment hung on the wrong method; a hollow assert; stale comments (`Shown`, the
    draw's flags word, the sprite path); the loader sorts a folder by name; wrong words in the page (the costume draws
    no set, boarding is admission's, the heads' callers, the seed); the address index and `PLAYER-GAPS.md`.
  - **Confirmed in the game** (silent, the stock jungle park, a Balloon Shop bought at (42,30), `save/` unchanged;
    predicted first; `q177erun.py`, `q177e-run1/`, `q177e-run2/`): guest 44 bought a balloon, **colour 0 (red), life
    127**, as predicted from the id and the quality, in the log line and `peeps`; photographed over the guest on the
    path (`q177e-run2/1-held-zoom.png`); the life fell by one on `mGameTick` 891, 895 and 899 and on none between (run
    2: 899, 903, 907); set to 3 by `balloon`, let go at life 0, `guests` showing frame 1 at alpha 250, photographed
    bursting (`3-burst-zoom.png`) and gone four seconds on (`4-after-zoom.png`); `SETTLE_UP_APPEARANCE` gone. Unprompted,
    saved guest 40 chose the shop and bought one too, colour 0 as its id gives (so run 2 tallied 8 of 10: its one-let-go
    and one-event predictions saw two). Run 3, on the final build, 10 of 10 (`q177e-run3/`). By eye the balloon floats as
    high over its guest as one does in the original's own frame (`content/ReferenceScreenshots/Park/ParkInterior_Polish_cropped.jpg`).
  - **Seen, not compared with the original:** over a shop's doorway a balloon sits inside the shop's model, and the
    depth test hides it (`q177e-run1/1-held-zoom.png`).
  - **Not confirmed on screen:** the balloon built again after a later visit, boarding's putting it away, a departure's
    unburst deletion, a saved balloon (tested only); the bob, about two pixels, and the trail (census only); a balloon of
    any colour but red: guests 44 and 40 both draw set 0, which rules out the first draw (44 would be 3) and an id one
    higher (2) but not a constant red, so the colour rests on the saves and `ABalloonsColourIsItsGuestsId`.
  - **Found:** Q177f (the costume).
  - Built and tested alone in a worktree: 1443 pass, 0 skip with the game; 569 ran, 874 skipped without; 123 warnings.

  The item as written: Found by Q177's decode ("The effects of a visit", 4). A Balloon Shop is
  buyable in Lost Kingdom from the start by its research cost (not measured). The arm reseeds the park's generator
  with the guest's id, draws a bank-10 sprite, keeps `mBalloonScript` and `mRemainingBalloonLife` (quality × 255 / 100,
  held 25..255), counts it down on the needs sweeps, frees it on a ride and rebuilds it after, and lets it go at
  nought, on leaving and to a prankster; the costume sets `mESPSprite` 2 and a variant. Counted now as
  `SETTLE_UP_APPEARANCE`. Open: which picture frame 1 of the balloons bank is, and what the costume-head callers of
  `FUN_0044b410` draw. Confirm: a guest leaving a bought Balloon Shop with a balloon on screen, and its life counting
  down in `peeps`. From Q177d: both arms draw the park's generator through `FUN_00541f70` and `FUN_00541fd0` besides
  the reseed (`ride-operation.md`, "The effects of a visit", 4); OpenTPW has one generator per system (Q177d's site).
- [x] **Q177f. Costumes, after the arrival's child.** Done 2026-09-30, `alexah/197-costumes`. `ParkSpriteBanks` counts,
  as a park loads, the kid banks under the detail file's `NUMKIDS` cap (`FUN_0041a9d0`: two, four, six or eight; six at
  medium and high), the theme's costume banks and the balloon's colours (`Level`, from the detail file the particles'
  density comes from). A person's `mESPSprite` and `mSpriteID` (file 37, 246) are read, and a guest keeps them as
  `Peep.SpriteKind` and `SpriteBank`, a saved child reduced within the kid banks as a load does (`0x004f93a6`: the
  shipped park's guests 33, 35 and 29 come in on 0, 1 and 1). An arrival is the child its id gives
  (`ParkSpriteBanks.ChildOf`: reseeded with the id, one draw over the kid banks). The settle-up's costume arm
  (`ParkRideOperation.DressOrUndress`): anything but a costume is dressed, a costume bank drawn over the theme's (the
  ride turn's draw, said at the site), event `0xb` counted (`SETTLE_UP_COSTUME_EVENT`); a costume is given back as the
  guest's child. `ParkGuestSprites` draws a guest in what they wear now and packs every child and costume bank, not the
  seventh and eighth children. The staff folders' cap from the same key (`FUN_0041aa40`: one bank at `NUMKIDS` 0, else
  two) is built too: a staff member's saved bank is brought within it as they are drawn and packed. Console: `peeps` prints `sprite kind/bank`, `guests` the drawn and saved kind and bank.
  - **Measured** through this build's reader (`q177f-kids`): each person's two fields equal their sprite's on all 18
    shipped people and every person with a sprite in Alexah's played parks; the roll gives all 13 shipped children over
    eight and all 296 jungle and 53 fantasy played children over six; the jungle park's 43 costumed guests all on bank 0.
    The two guests Q177e's decode left unexplained (213 and 181) are riders in costume.
  - **Tests:** `ParkCostumeTests`, 15. **Mutation** (`q177f-mut/mutate.py` on the final tree, `mutate-final.out`): 30 mutants, 29
    red; the green one showed `BanksToPack`'s filter of saved children redundant beside the reduction, so it went, and the
    reduction's own mutant and the costume packing's re-ran red.
  - **Reviewed** (`wf_b57c6c53-9d0`, kept small for the week's usage: one Opus reviewer over the code against the decode,
    the C# and tests and the docs, and one Opus skeptic over every finding): 10 findings, all upheld or amended and each
    acted on. Among them: a hollow test (a costume bank fixed at nought passed it); the staff folders' cap and a staff
    member's reduction missing; stale comments and doc rows (the person record's row had kind and bank the wrong way
    round); Q32, STATUS and the FileFormats wording.
  - **Confirmed in the game** (silent, the stock jungle park, a Costume Shop bought at (42,30), `save/` unchanged;
    predicted first; `q177frun.py`, `q177f-run4/`, 8 of 8): the shipped guests 33, 35, 29 on 0, 1, 1 and the rest as
    saved, in `peeps`; guest 44 arrived as **child bank 5**, their id's, photographed (`0-arrived-zoom.png`); dressed at
    the shop, **`sprite 2/0`** in `peeps` and the log, photographed in the leopard costume (`1-in-costume-zoom.png`); a
    second visit gave back **child bank 5**, the log's "returned a costume" line and `peeps`, photographed
    (`2-child-again-zoom.png`); one `SETTLE_UP_COSTUME_EVENT` a dressing (guests 31 and 42 chose the shop unprompted and
    were dressed too). Runs 1 to 3 stopped on harness faults (a `send` refused while walking elsewhere, a payment queue
    not drained, another guest's payment), none the game's. Run 5, on the final build, 8 of 8 (`q177f-run5/`).
  - **Not confirmed on screen:** low detail's two kid banks and one staff bank (tested only); a theme with more than one
    costume bank (none ships one; tested only); costume heads on a ride, as no head is drawn here (Q190).
  - Built and tested alone in a worktree: 1457 pass, 0 skip with the game; 572 ran, 885 skipped without; 123 warnings.

  The item as written: Split from Q177e; decoded there (`ride-operation.md`, "A
  costume"). A shop whose `AppearanceEffect` is 2 (Lost Kingdom's Costume Shop, 1202: `CostOfResearch` 550, research
  group 3, offered from the start here, which has no research) dresses a guest in the theme's costume (`mESPSprite` 2,
  `mSpriteID` a draw over the costume banks, one in Lost Kingdom, with no reseed; event `0xb`), and undresses one
  already in it back into the child they arrived as (kind 0, the generator reseeded with their id, `(r >> 2) %` the kid
  banks; no event). That return needs what OpenTPW lacks: arrivals all wear kid bank 0 (`ParkPeople.Admit`), the kid
  banks are not capped at the original's six (medium and high detail, `FUN_0041a9d0`), and a load does not reduce a
  saved `mSpriteID` modulo them (`0x004f93a6`). So build the arrival's roll first (`FUN_004faec0`, `0x004fb18d`..
  `0x004fb1bc`), pack the costume bank, then the costume; the picture changes as the guest leaves the shop. Counted
  now as `SETTLE_UP_COSTUME`. Confirm: a guest leaving a bought Costume Shop in the tiger costume, `peeps` showing kind
  2, and after a second visit the child they arrived as.
- [x] **Q198. The bank's month turn, the wage and training: the decode.** Done 2026-09-30,
  `alexah/198-decode-the-month-turn`. Decode only; the build is Q198b. `ride-operation.md`'s new "The month's change"
  (who hears `0xc` and in what order, the training, the analyser's month, the bank's month turn, the wage, what Lost
  Kingdom reaches, both measurements, what the build needs); "The cost of goods and the park's money" and "Who sends
  the day's change" pointed at it, the training payer named (thing 1, `mStaffHQ`). `weather.md`: the calendar is not
  the sole writer of its month and day, and the year is not saved, so a park saved in another year gets `0xd` on its
  first sweep. `hud.md`: the staffcosts screen's five budgets. FileFormats (`alexah/198-decode-the-month-turn`):
  `saves.md`'s new "The staff HQ (model 9)" (the training budgets at file 82) and "The message centre module" (the 29
  listener sets; set `0xc` in all nine park files), and the batch and loans nought in all nine. Comments corrected at
  their sites (`Level`, `ParkPeople`, `ParkState`); no code changed.
  - **Decoded** (`wf_362d8882-a82`): three Opus decoders in Ghidra (the month's listeners and their order, the bank's
    month turn, the wage and the training), each put to an Opus skeptic (93 verdicts: 84 upheld, 9 amended, none refuted and none changing an answer; the
    points they found missed, a promotion paid at the new grade in the same change, the whole share withdrawn, the
    park that ends mid-walk still paying its staff, the easy wages Instant Action's only, are in the section). Re-read first-hand:
    `FUN_004d0370`, `FUN_00504c70`, `FUN_00505a10`, `FUN_0050c800`, `FUN_00506490`, the loan drawdown's gate and the
    payoff's profit share; measured first-hand: the message
    centre's sets and the bank's batch and loans in all nine park files, thing 1's record in the shipped park.
  - **The answer.** At each month's change set `0xc` is told in ascending thing id: thing 1 pays the training, the
    analyser closes the month, the bank runs its turn, then each member of staff pays `PayMultiplier[type]` ×
    `BaseWage[grade]`, untested and unprorated. The training divides each kind's budget among its members (signed),
    buys a point per `PoundsPerTrainingPoint[grade]` up to 100 a month, and promotes at 100; the budgets are nought in
    the shipped park. The bank's turn banks `mBatchBalance` and pays bought loans (nought and none in every park
    file) and ends a park six thirty-day months into the red. So the shipped park's month costs **538**, all wages. A
    load sends no `0xc`: after `Easymode.TPWI` the first is at tick 1383.
  - **Measured in the original** (the reference install under Proton, off-screen, the stock park; `q198/orig/`),
    predicted first: 538 at 3.1 (tick 1383, as decoded; 88212 to 87674) and at 4.1 (88519 to 87981, photographed on
    3.31 and 4.1, `turn-*-hud.png`); with the mechanics' budget raised to 25 on the Staff Training Budgets screen,
    563 at 5.1 and the mechanic's progress 0 to 1. The analyser's month costs read 538 after each change, which put the
    training in the closing month and the wages in the new one before the decoders had reported it. The live tables
    held the easy wages and the global training costs. Park clock NOT TRUE (1.48×), which moves no amount.
  - **Measured in the game** (the build before any change; `q198run.py`, `q198-run1/`, silent, the stock park, a
    handyman hired at 36, predicted first; `save/` unchanged; 8 of 8): the balance 88137 on both sides of 2/1/2000 with
    no Bank line between, photographed (`0-before-month.png`, `1-after-month.png`); `unimplemented` none, then
    `BANK_MONTH_TURN` 1, `STAFF_MONTHLY_WAGE` 6, `STAFF_MONTHLY_TRAINING` 6.
  - **Not confirmed on screen:** a promotion (1500 at a grade-3 member's 15 a point); a loan's instalment and its
    unsigned profit (no loan is bought in any file, and our loans screen is not built); the end of the park six months
    in the red; the year's `0xd` on loading a park saved in another year (decoded only, Q149's note).
  - Built and tested alone in a worktree: 1457 pass, 0 skip with the game; 572 ran, 885 skipped without; 123 warnings.

  The item as written: Found by Q177c's map and review (`ride-operation.md`, "The cost of goods and the park's money",
  every caller of the bank, and "Who sends the day's change"). The calendar sends the month's change, message `0xc`, on
  its own compare (`0x004f83b9`). On it the bank's handler runs its month turn `FUN_004d0370` (it banks
  `mBatchBalance`, pays each loan's instalment, counts the months in the red from `mTurnEnteredRed` and ends the park
  at six), each member of staff withdraws a month's wage (`FUN_00504c70`, `0x00504cb9`) and the staff manager pays out
  the training budget (`FUN_00505a10`, `0x00505a45`). Counted now, not built: `BANK_MONTH_TURN`, `STAFF_MONTHLY_WAGE`
  and `STAFF_MONTHLY_TRAINING`, one of each a member. Also counted: the purchase's golden-ticket arm
  (`PURCHASE_GOLDEN_TICKET_ARM`; the buy list's side is Q141's note) and a kart or water ride's track at a sale
  (`SALE_TRACK_TEARDOWN`: its cells cleared and paid for, `0x0052801d`). Decode the month turn's order and the wage's
  and training's amounts in the park that ships, then build them. Confirm: `money` either side of a month's change
  with staff hired, and `unimplemented`.
- [x] **Q198b. The month's change: the training, the bank's turn and the wages.** Done 2026-09-30 on
  `alexah/199-month-turn`. `Level` sends the month in the original's order: `ParkPeople.TrainTheStaff` (the save's
  staff HQ budgets, `ParkWorld.StaffHq`), `ParkState.TurnTheMonth` (the analyser counted; batch, loans, red months),
  `ParkPeople.PayTheWages`. `Staff.PayGrade` and `PercentageThroughGrade` now change. Counted: the strike check, the
  analyser's training and staff totals, the park's end six months in the red and its advisor record
  (`ride-operation.md`, "The month's change", OpenTPW).
  - **Measured in the game** (`q198brun.py`, `q198b-run1/`, silent, stock park, a hire at 36, predicted first; `save/`
    unchanged; 15 of 15): over 2/1/2000 six training withdrawals of 0, a deposit of 0, then wages 63, 161, 84, 105,
    125, 36; the balance 88137 to 87563 (574 = 538 + 36), the HUD photographed on both sides; `BANK_MONTH_TURN`,
    `STAFF_MONTHLY_WAGE`, `STAFF_MONTHLY_TRAINING` gone.
  - Tests: the 538, the mechanic's 25 (563, 0 to 1), a promotion paid at the new grade, grade 4 refused, a loan's
    unsigned profit and close, 4147 vs 4148 sweeps in the red. Each bug put back failed its test (signed division, no
    hold to 100, no wage, no grade-4 refusal, an end at five).
  - **Not confirmed on screen:** a promotion, a loan, the batch, the end six months in the red (tested only; no
    screen sets a budget or buys a loan). Found in passing: seven older `ParkBankTests` fail when that class runs
    alone (the static `Log` is null), on `main` too; the whole suite passes.
  - Built and tested alone in a worktree: 1462 pass, 0 skip with the game; 573 ran, 889 skipped without; 123 warnings.

  The item as written: Decoded by Q198 (`ride-operation.md`,
  "The month's change"). At `GameCalendar.MonthRolled`, in the original's order: thing 1's training (`mBudget[0..4]`
  read from the save's model-9 record at file 82, which `ParkWorld` skips; each kind's budget over its members, signed;
  `TrainMe` for every member, grade 4 refused, `ParkState.Spend( share )` even for nought, the progress and the
  promotion, so `Staff.PayGrade` and `PercentageThroughGrade` must change); the analyser's month, counted; the bank's
  turn (`mBatchBalance` and the eight loans seeded from the save, both dead by CONTENT in every park file; the red
  count, and the end of the park and the advisor's record `0x6a` counted where it reaches them); then each member's
  wage, `ParkStaffPool.WageFor` through `ParkState.Spend`. `PURCHASE_GOLDEN_TICKET_ARM` and `SALE_TRACK_TEARDOWN` stay
  counted. Confirm: `money` either side of 2/1/2000 with a member hired, the fall the members' wages (the shipped five's
  538 and the hire's), the HUD photographed on both sides, and `unimplemented` without the month's three counts.
- [x] **Q199. A guest running for the bus. Count it first.** Done 2026-09-30 on `alexah/200-bus-hurry`. Counted first:
  `GATE_HURRY_FOR_THE_BUS` in the running game (`q199-count/`) reached 39 sweeps while guest 43, the load's, headed for
  the gate and the bus answered 3, so it was a GAP. Built: `PeepBehaviour.GateHurry` gives 50 at the bus's 3, else 25
  or 0 by id; `ParkPeople.BusStatus` asks only the bus, only while it is the current vehicle, and counts the
  original's let-go of a spent one (`GATE_HURRY_FORGETS_SPENT_VEHICLE`, for Q131). `park.md`, "Arrivals".
  - Confirmed in the game (`q199run.py`, `q199-build2/`, 7 of 7, `save/` unchanged): one sweep after the bus answered
    3, guest 43 `speed 50`, not counted; four sweeps on, eased 1.3274 against 1.3274 predicted (base 100); the bus at
    4, `speed 0` again. The gate photographed with the bus pulling away and the guest crossing.
  - Tests: the rule (50 only at 3, for an id that hurries and one that does not), and the park's own bus script
    wired through `ParkPeople` (none at 3 before a load, 50 at 3, own hurry at 4, counted at 6, a seaplane at 3 not
    run for). Each bug put back failed a test (the seam not handed in, no current-vehicle check, any vehicle asked, no
    count at 6, the turn not asking, the rule ignoring 3). Review `wf_0a38fff7-9db`: three findings, all fixed.
  - **Not confirmed on screen:** the spent bus's count and a larger vehicle at 3 (tested only).
  - Built and tested alone in a worktree: 1464 pass, 0 skip with the game; 573 ran, 891 skipped without; 123 warnings.

  The item as written: Found by Q177d's review (`wf_4e09ebe1-b64`). Heading for
  the gate, `FUN_004ff730` writes the hurry 50 (`0x0075c7f4`) when `FUN_0051aad0` answers 0 and `FUN_0051a690` 3: the
  current arrival vehicle is the size-1 one and its script's `VAR_STATUS` is 3 ("The bus is coming!  RUUUUUUUUUUN!!!!",
  `0x0075d914`); else 25 on `(id & 3) == 0`, else 0. OpenTPW writes 25 or 0 (`PeepBehaviour.HurriesToTheGate`), and
  since Q177d the hurry is summed into the walking speed, so a base-120 guest runs at 1.45 where the original's reaches
  1.7. Count it (`Unimplemented.Report`) where the bus reports 3, then build it from `ParkPeople`'s vehicle script.
  Confirm: `peeps` `speed 50` and the eased speed on a guest heading for the gate while a load's bus reports 3.
- [x] **Q200. The all-visitors list jumps back to its top every two seconds: the decode.** Done 2026-09-30,
  `alexah/201-decode-the-visitors-refresh`. Decode only; the build is Q200b. `hud.md`'s new "How allpeeps keeps itself
  current"; the deviation said at `ParkVisitorsScreen.RefreshEvery`; no code changed.
  - **Decoded** (`wf_95649c5e-08a`, one Opus skeptic: C1-C3 upheld, C4-C5 amended, none refuted; the amendment that a
    removal clamps the top row re-read first-hand, `FUN_00664ea3`). The builder adds a row per guest once and arms the
    2000 ms timer `0x80083`; on it the handler (`0x00493270`) rewrites each existing row's six values in place
    (`FUN_006644ea`), row 0's call redrawing from the top row `list+0x154`, and never clears, re-sorts or scrolls. A
    guest's construction (`0x1c`) inserts their row in sort order and a thing's delete (`0x1b`, guests only) removes it;
    the scrollbar then clamps the top row to count - visible. The skeptic's edges (count <= visible leaves the top row
    unclamped; a removed selection reselects by slot; the selection is not shifted) are in the section.
  - **Measured in the game** (the build before any change; `q200run.py`, `q200-run1/`, silent, stock park, twenty
    admitted, 33 guests; `save/` unchanged), predicted first: opened at its top (first row cash 684), four rows down
    after the wheel (306), back at its top (684) 3.1 s later, photographed (`0-opened`, `1-scrolled`, `2-after-3s`).
  - **Not confirmed:** the original's own list under Proton (decoded, not photographed); the three edges.

  The item as written: Found by Q177d's game run.
  `ParkVisitorsScreen` refills its list every two seconds (`RefreshEvery`) through `UiList.Clear`, which sets the
  scroll to the top, so a player scrolled down to a guest past the thirteenth row is thrown back up within two
  seconds. Decode how the original refreshes `allpeeps` (`FUN_00493530`'s list and its row adder) and whether it keeps
  the scroll; then match it. Confirm: scroll the list in a park of more than thirteen guests and photograph it after
  three seconds.
- [x] **Q200b. The all-visitors list kept current in place.** Done 2026-09-30 on `alexah/202-visitors-kept-in-place`.
  `ParkVisitorsScreen` fills the list once in Visitor Number order, rewrites each row in place every two seconds, and
  adds and removes a row on `ParkPeople.GuestArrived`/`GuestLeaving` (the original's `0x1c`/`0x1b`) until it closes.
  `UiList` gained the sorted insert (after every row not greater, `FUN_00663edc` read first-hand), the removal, the
  in-place rewrite, the slider's clamp only past a full window, and a selection kept as an index, reselected by slot
  and told as `0x401`. Two branches not decoded are counted: `LIST_FIRST_ROW_SELECTED` (an add to an empty list
  selects row 0, every list) and `LIST_RESELECT_PAST_THE_WINDOW` (`+0x48 & 0x100`). `hud.md`, the same section.
  - Confirmed in the game (`q200brun.py`, `q200b-run1/` and `q200b-run2/` on the final build, silent, stock park,
    twenty admitted, 33 rows; `save/` unchanged), each predicted first: four rows down, first row visitor 45, cash 450,
    and the same three seconds on, 48 written in place unsorted; `arrive` logged "row added for guest 63 (visitor 0)
    at 1 - 34 rows, top row 4", its `depart` "row removed ... at 1 - 33 rows, top row 4"; at the bottom (top 20) the
    last row's going "at 32 - 32 rows, top row 19", the view pulled up a row. All six shots looked at.
  - Tests: `UiListTests` (8) and `ParkVisitorsScreenTests` drive the real screen; `ParkPeopleTests` the events. Each
    bug put back failed a test (settling always, inserting before equals, a rewrite scrolling, selection shifted or
    reselected without the top row or untold, the wheel not clamping, either event unraised, the timer refilling,
    either subscription or the unsubscription missing). Review `wf_43732b5c-f68`: three findings, all fixed.
  - **Not confirmed on screen:** the selection edges and the wheel's clamp of a short list (tested only); the
    original's own list under Proton.
  - Built and tested alone in a worktree: 1474 pass, 0 skip with the game; 581 ran, 893 skipped without; 123 warnings.

  The item as written: From Q200's decode (`hud.md`, "How allpeeps keeps itself
  current"). Open the list once, sorted by the remembered `DAT_007508bc`; every two seconds rewrite each row's values
  in place without clearing, sorting or scrolling; add an arriving guest's row in sort order and remove a leaving
  guest's, with the top row clamped only as the original's slider clamps it. Confirm: scroll the list in a park of
  more than thirteen guests, photograph it after three seconds (the same first row), and a log line per row added
  and removed while it is open.
- [x] **Q178. Instant Action's catalogue: each item's `Easy_` file, laid last and required: the decode.** Done
  2026-09-30, `alexah/203-decode-the-easy-catalogue`. Decode only; the build is Q178b. `park-engine.md`, "How a key
  finds its global", rewritten from "An item's description"; the deviation said at `ParkItemCatalogue`'s item loop.
  FileFormats `sam.md` names the 20 WADs without one (`138d3c6`, its branch 203).
  - **Decoded** (first-hand, then `wf_8a4195bd-544`, two Opus skeptics: G1-G5 and F1-F5 upheld, none refuted). The
    catalogue is rebuilt at every park load (state 9, `FUN_00407e00` at `0x0054ed3f`, before the balance); in type 2
    `FUN_00413930` keeps an item only if its own wad holds `Easy_<stem>.sam` (`FUN_0041f190`, the strings at
    `0x00747930` and `0x00747928`, matched lowercased), and `FUN_00413c10` lays that file last. A refused value in any
    item's file quits the game at the park load, with an error box at exit. One skeptic placed the load in state 1
    ("data init"); read again first-hand, `FUN_00407e00` sits under `case 9:`.
  - **Measured** (wadcat, all 70 jungle item wads): 50 hold an `Easy_` file and 20 do not (the list in the section);
    12 rides' `Easy_` files set `Upgrades[i].WearRate` and `CostOfResearch`, `minecart`'s also `Research.Group`, and
    the other 38 hold comments only. `Easymode.TPWI` places none of the 20.
  - **Measured in the game** (the build before any change; `q178/shoptab.py`, `q178/run1/` and `run2/`, silent, stock
    park; `save/` unchanged), predicted first: `catalogue` lists 67 items (predicted 65: I counted two category files
    as wads; the original's type 2 keeps 50); the shops tab shows 8 rows with the Gift Shop and the Steak Restaurant
    (predicted 9, the same miscount), photographed (`run2/shops-tab.png`), and the rides tab lists Chac Atak, Eruption,
    Gorilla Thrilla and Jurassic Tours (`run1/shops-tab.png`). `objects` shows 14 placed things, none of the 20.
  - **Not confirmed:** the original's own buy screen under Proton (decoded, not photographed); the quit on a refused
    value (no shipped file has one).

  The item as written: Found by
  Q171's verify (`wf_727b3f26-329`, a reader and a skeptic agreeing; `park-engine.md`, "How a key finds its global").
  In game type 2 (`DAT_00fb3b7c`), `FUN_00413c10` lays `Easy_<stem>.sam` over the category and the item's own file
  (`0x00413ffe`..`0x0041404d`), and `FUN_00413930` drops an item whose wad has none before cataloguing it
  (`0x00413ac4`..`0x00413b3a`): in jungle the Gift Shop, the Steak Restaurant, the Arcade, Chac Atak, Gorilla Thrilla,
  Sun God, Jurassic Tours, Eruption, nine features (`5x5rck`, `5x5rck2`, `lavspurt`, `lure`, `mamfount`,
  `speaker2`-`4`, `statue2`) and the three upgrades. OpenTPW reads no item's `Easy_` file and catalogues all of them,
  while `Level` builds its balance with `easyMode: true`. The rides' `Easy_` files set `Upgrades[i].WearRate`,
  `CostOfResearch` and minecart's `Research.Group`; the rest are comments. From the same decode: a bounded key no file
  sets reads its lower bound, not nought (`FUN_004013e0`, `0x0040153a`), so every shop and sideshow's
  `Info.NewAttractionDecayTime` is 1, which OpenTPW reads as nought, and every item's `UsageInfo.ExciteFactor` but a
  sideshow's (60, `SideShow.sam`) 50, a key OpenTPW does not read (nothing reads either yet); and a value the loader
  refuses (a negative in a type-5 key, a bounded key outside `[lo, hi)`) ends the whole catalogue load (`0x00413f18`),
  where `ItemDescriptionFile.Number` takes it. Decode whether Lost Kingdom's `Easymode.TPWI` is type 2
  (`docs/exe/boot.md`), then build the layer and the gate. Confirm: the buy screen's shops tab without the Gift Shop
  and the Steak Restaurant, and `unimplemented`.
  - **Note (fork review, 2026-09-30):** the decode question is answered. The original's `Easymode.TPWI` only ever runs in type 2:
    `FUN_005c8190` copies it in only for an Instant Action player, `gms.dat +0x24` stores 0 or 1 and Select maps it to
    `SetGameType` 0 or 2 (`0x005c85ae`), and no park file carries the type (`GSYS`, `FUN_005506e0`, holds nine dwords,
    none of them `DAT_00fb3b7c`). So `easyMode: true` is right for that file: key the `Easy_` layer and the catalogue
    gate on the same condition as the balance (the loaded park is the type-2 Easymode), not on the player alone, which
    would pair a Full Simulation player with Easymode and the Standard catalogue, a combination the original never runs
    (Q186). `park-engine.md`'s third `Easy_Standard.sam` pass is written as unconditional, where `FUN_005156a0` makes it
    only in type 2; Q185 corrects it. Review items gap3-1, gap3-8.
- [x] **Q178b. Instant Action's catalogue: the `Easy_` layer and the gate, built.** Done 2026-09-30 on
  `alexah/204-easy-catalogue` (FileFormats: the same branch name). `Level.InstantAction` is the one condition: the
  balance lays `Easy_Standard.sam` on it and `ParkItemCatalogue` its gate, leaving out an item whose wad has no
  `Easy_<stem>.sam` and laying the file over the item's own (`ItemDescriptionFile.Overlay`, every key overwrites).
  The item schema at `0x00744b30` was read record by record: only `Info.NewAttractionDecayTime` (1) and
  `UsageInfo.ExciteFactor` (50, not read) have a lower bound above nought, so the first now falls back to 1.
  `Upgrades[i].WearRate`, `Upgrades[i].CostOfResearch` and `Research.Group` are counted (`ITEM_WEAR_RATE`,
  `ITEM_RESEARCH_KEYS`). **A refused line - a key the schema does not name, a malformed number, a negative in a type-5
  key, a value outside its bounds - leaves out what its file describes (the item; a category's whole folder), logged
  with the file's name and counted once `ITEM_VALUE_REFUSED`, where the original quits** (Alexah, 2026-09-30, rule
  11; said at the site). `park-engine.md`, "How a key finds its global" (the item schema); FileFormats `sam.md`.
  - Confirmed in the game on the final build (`q178b/shoptab.py`, `q178b/run2/`, silent, stock park; `save/`
    unchanged; `run1/` the same on the first build), predicted first: "catalogued 50 items, 17 left out of Instant
    Action"; `catalogue` 50 rows; the shops tab, photographed and looked at, 6 rows without the Gift Shop and the Steak
    Restaurant (Q178's `run2/` showed 8); `ITEM_WEAR_RATE` 54 as predicted; `ITEM_RESEARCH_KEYS` 174 against 164
    predicted, the 10 being the five fixed items' own files read by `ParkFixedItems` (2 each), counted before
    accepting it; no `ITEM_VALUE_REFUSED`.
  - **The original, under Proton** (off-screen, player `ref`, `q178b/orig/`): `DAT_00fb3b7c` reads 2; no Gift Shop and
    no Steak Restaurant. **But its shops tab lists 3, not 6**, and its rides tab 4: it lists only items whose own
    `Upgrades[0].CostOfResearch` is 0, all four tabs (`hud.md`, "What the buy list actually filters on"). Queued as Q201.
  - Tests: `ParkEasyCatalogueTests` (27); the control reads all 128 of Lost Kingdom's item descriptions against the
    whole schema, predicted 128 (its first run caught the coasters' `Coaster.sam`, a track-texture file). Each of 12
    bugs put back failed a test: the gate off (it first stayed green, as a missing `Easy_` file threw and dropped the
    item anyway; the overlay now reads only a file that exists), the overlay off, an item's or a category's refusal
    kept, the lower bound nought, the bound closed, the index not stripped, an unknown key taken, a `+` taken, a
    shape's rows read as keys, either count dropped. Review `wf_ba54a723-24e`: three findings (unknown keys and number
    forms unrefused; a category's refusal naming the item and counted per item; the counts untested), all fixed.
  - **Not confirmed on screen:** a refused line (no shipped file has one; tested only).
  - Built and tested alone in a worktree: 1501 pass, 0 skip with the game; 605 ran, 896 skipped without; 123 warnings.

  The item as written: From Q178's decode
  (`park-engine.md`, "How a key finds its global"). When the loaded park is the type-2 Easymode (the same condition
  `Level` uses for `easyMode: true`, not the player alone, Q186), `ParkItemCatalogue` lays `Easy_<stem>.sam` from the
  item's own wad over its own file, matched without regard to case, and leaves out an item whose wad has none. Read the
  keys it sets (`Upgrades[i].WearRate`, `Upgrades[i].CostOfResearch`, `Research.Group`) or count them where nothing uses
  them yet. A bounded key no file sets reads its lower bound. A refused value is a quit in the original: decide with
  Alexah whether OpenTPW quits too (rule 11), and say it at the site. Confirm: `catalogue` lists 50 items, and the
  shops tab, photographed, shows 6 rows without the Gift Shop and the Steak Restaurant.
- [x] **Q201. The buy list lists only researched items: the decode.** Done 2026-09-30,
  `alexah/205-decode-the-researched-flag`. Decode only; the build is Q201b. `hud.md`, "What the buy list actually
  filters on" and "The mystery row", rewritten; `saves.md`'s "ride ids" row. FileFormats `saves.md` settles the
  control record's `0x10` and `0x14` (its branch 205).
  - **Decoded** (first-hand, then `wf_8fb3c3ca-f2c`, three Opus skeptics: 9 of 11 claims upheld, 2 corrected). The
    researched flag is the control record's `+0x10`, and at a park load it is **the save's**: world setup
    (`FUN_005156a0`, after the catalogue at `0x00407e26`) zeroes the array and seeds `+0x10` = `Upgrades[0].
    CostOfResearch` is nought (`0x004d3e92`), and research setup (`FUN_005031a0`) sets the same again; the park's save
    (mode 2) then reads `mObjectControls[150]` raw over it (`0x005181e7`), and `FUN_00415140` seeds only items the save
    lacks. Research completing (`FUN_00504630`) sets it and `+0x14`, the tier. Corrected by the skeptics: the mystery
    row's `item+0xC4` is `GoldenTicketCost`, not `Research.Group` (`+0x178`), and its unlock test is the player's
    golden-ticket set in `gms.dat`; the zeroing is also a writer. No balance file sets a research cost (grep, with a
    control).
  - **Measured** (`q201/ctrldump`, `q201/fcost.py`): `Easymode.TPWI`'s 50 records are the 50 items with an `Easy_`
    file; `+0x10` is 1 on 26, exactly those whose file sets the cost 0 (no mismatch), so the two rules agree in the
    shipped park; `+0x14` is 2 on 17 of them, 0 elsewhere.
  - **Measured in the game** (the build before any change; `q201/shoptab.py`, `q201/run1/`, silent, stock park;
    `save/` unchanged), predicted first: "catalogued 50 items" as predicted; the shops tab, photographed and looked
    at, shows 6 rows (Balloon, Burger, Costume, Drinks, Fries, Ice Cream) as predicted, against the original's 3
    (`q178b/orig/s07.png`: Balloon, Burger, Drinks).
  - **Not confirmed:** a played save whose research set more flags than its files (none here); the mystery row (no
    researched item in Instant Action carries a ticket cost).

  The item as written: Found by Q178b's run of the original (Lost
  Kingdom, Instant Action, `q178b/orig/s06`-`s09.png`): its tabs list rides Aztec Mayhem, Belly Bounce, Crazy Ape,
  Rocky Racers; shops Balloon, Burger, Drinks; sideshows Jungle Spray, Strength Bird; features Buy Land, Clear Land
  and eight more - exactly the items whose own file sets `Upgrades[0].CostOfResearch` 0, the researched flag
  `desc+0x10` (`hud.md`, "What the buy list actually filters on"). OpenTPW lists every catalogued item: 6 shops.
  Decode where `desc+0x10` is set at level start (from the file alone, or also from the save's research state), and
  reconcile `hud.md`'s mystery row (`Research.Group` above nought and not unlocked), which the original showed none
  of although Temple of Gloom and Ice Cream carry a group. Then build the filter. Confirm: the shops tab, photographed,
  lists the three.
- [x] **Q201b. The buy list lists only researched items: the build.** Done 2026-09-30,
  `alexah/206-researched-buy-list`. No FileFormats change (the record's layout was already there).
  - **Built.** `ParkWorld.ObjectControlRecords` reads the first `mNumObjectControls` records (id, `+0x10`, `+0x14`;
    a count past 150 is held to it). `ItemDescriptionFile.ResearchCost` reads `Upgrades[0].CostOfResearch` (the other
    research keys stay counted). `ParkResearch` keeps the save's flag and tier for each item it holds and seeds an item
    it lacks from a nought cost; `Level.Research`. `ParkBuyScreen.Listed` lists only researched items. The mystery row
    is built from the current player's `RideIds` (UITEXT 137, the ticket cost negated); choosing it is counted
    `MYSTERY_RIDE_PURCHASE`. Research completing is counted `RESEARCH_COMPLETING` where a researcher would research.
    Said at the site: the row's price is the item file's, not the record's `+0x04` (equal in all 50 shipped records).
  - **Confirmed in the game** (`q201b/shoptab.py`, `q201b/run2/`, silent, stock park; `save/` unchanged), predicted
    first: "Research: 26 of 50 items researched, 50 from the save's records and 0 seeded from their files"; the tabs,
    photographed and looked at: rides 4 (Aztec Mayhem, Belly Bounce, Crazy Ape, Rocky Racers), sideshows 2 (Jungle
    Spray, Strength Bird), shops 3 (Balloon, Burger, Drinks), as the original's `q178b/orig/`. `ITEM_RESEARCH_KEYS`
    115 (174 before; not predicted to the unit). `RESEARCH_COMPLETING` was reached (the park has a researcher).
  - Tests: `ParkResearchTests` (7). Each of five bugs put back failed a test: the filter off, the save's records
    ignored, `+0x11` read for `+0x10`, the unlocks ignored, the seed ignoring the cost. Review `wf_55d38b7b-61e` (two
    Opus): four low findings (a wrong address, a throw on a bad count, a stale `park-engine.md` line, the price's
    source unsaid), all fixed.
  - **Not confirmed on screen:** the mystery row (no researched Instant Action item has a ticket cost) and a played
    save whose records differ from the files (none here). The game ran on the build before the review's fixes (comments,
    a clamp no shipped save reaches), none of which changes what is drawn.
  - Built and tested alone in a worktree: 1508 pass, 0 skip with the game; 609 ran, 899 skipped without; 123 warnings.

  The item as written: From Q201's decode (`hud.md`, "What the buy
  list actually filters on"). `ParkWorld` reads `mObjectControls` (it skips it now, `ParkWorld.cs`) - each record's id,
  `+0x10` and `+0x14` - and the running park keeps a researched flag per item: the save's for every item it holds,
  `Upgrades[0].CostOfResearch` is nought for any catalogued item it lacks. `ParkBuyScreen.Show` lists only researched
  items. Research completing is inert here (count it where the flag would be set). The mystery row needs the
  player's golden-ticket set (`gms.dat`'s ride ids): build it if the profile reader has it, count it otherwise.
  Confirm: the shops tab, photographed, lists the three (Balloon, Burger, Drinks), and the rides tab the four.
- [x] **Q179. The Hot Pot lets no rider off: the decode.** Done 2026-09-30, `alexah/207-decode-the-hot-pot-unload`.
  Decode only; the build is Q179b. `park.md`, "How a bumper ride ends a go, and lets its riders off", new (the record,
  the car, what each selector calls, the chain, the tick, three quirks); line 957's "(frames per second)" is 31 ms
  ticks; `park-engine.md`'s `FUN_00546c80` row is the track-ride tick. FileFormats `vm/instructions.md` gives every
  `BUMP` command its meaning, 9, 12, 13 and 16 among them (its branch 207).
  - **Decoded** (the fork review's gap1-1..gap1-7 and its refuter, read again first-hand, then `wf_37cc14a6-a0a`,
    three Opus skeptics: eleven claims upheld, details corrected, and "nothing else writes the timer" refuted). The
    track tick `FUN_00546c80` runs once per 31 ms step and `FUN_005474b0` counts each car's timer down while the
    ride runs unbroken; at 0 the car's riders go to the leaving list (`FUN_0054ac70`), and `BUMP 2 VAR_LETMEOFF` @221
    takes them one a pass. Corrected by the skeptics: `BUMP 3` and `7` act by type (water and -2 differ), a duration
    of 0 still unloads on close, `BUMP 12` can seat an unloading car and cancel its unload, and the save loader
    (`FUN_00543560`, chunk 5) restores whole car records, timer and flags.
  - **Measured in the game** (the build before any change; `q179/q179run.py`, `q179/run2/` 6 minutes and `run3/` 4,
    silent, stock park, Belly Bounce sold and The Hot Pot bought at (57,23); `save/` unchanged), predicted first, 5
    of 5 both runs: built capacity 4, duration 25; no match line for it; 22 and 19 guests in state `Riding` on it;
    `BUMP` @102, @108 and @221 each counted once per admission, equal to the riders (the stale register at @221 lets
    `VAR_ONRIDE` count down with nobody off, so it admits past 4). Photographed and looked at (`run3/ran-z60-y180.png`,
    `ran-z100-y90.png`): the pot stands empty, no car and no rider drawn, while 19 are counted on it.
  - **Not confirmed:** the unit in the running original (30 ticks a unit is read from the code: a go of 25 should
    last 750 ticks, 23.25 s); the cars' motion (`FUN_0054a040`, buoys), not needed for the unload.

  The item as written: Found by Q172b's game run. A Hot Pot bought at (57,23)
  with its queue laid to the path at (56,22), then `load 40`: guests are admitted ("been AdmitPerson'd to ride 43")
  and sit in state `Riding`, 13 in 20 minutes, 16 in the build before Q172b in 4, past its capacity of 4, and none is
  let off, so no settle-up runs for it (no excitement match, visit history or charge). Its script reaches `BUMP`,
  unbuilt and counted ("Bumper Car: BUMP at N was reached and does nothing"). Decode how the Hot Pot's script ends a
  ride and lets its riders off, and what `BUMP` answers it. Confirm: riders let off the Hot Pot, each match log reading
  excitement 42 (`q172brun.py`, `q172b-long/`).
  - **Note (fork review, 2026-09-30): the decode is done; write it, then build.** Lead: Aluzed's fork (`FUN_0054a040`, T-007 item
    22); established by the review in Ghidra and over all 199 `BUMP` uses (items gap1-1..gap1-7, re-checked by a
    refuter). The record is `DAT_00877b60 + (h & 0xff) * 0xd0` under the `GFEJ` magic (`+4` duration, `+0x50` state,
    `+0x54` broken or worn, `+0x5c` cars, `+0x60` riders seated, `+0x64` most cars, `+0xc4` boarding list, `+0xc8`
    leaving list); the cars are `DAT_00877b68`, 256 of `0xac` (`+0x88` timer, `+0x30` riders, flag `0x20` unloading).
    `BUMP 1` pushes the rider onto the boarding list, 4 and 12 move the list into a car, 13 sets the duration to the
    value × 30, 3 starts. The track-ride tick `FUN_00546c80`, once per 31 ms tick, calls `FUN_005474b0`, which counts
    each car down while the state is 2 and the ride is not broken, and at 0 sets `0x20`; `FUN_0054ac70` moves the car's
    riders to the leaving list and answers the ride-wide seated count, so the state goes back to 1 only when the whole
    ride is empty; `BUMP 2` pops one rider a pass into `VAR_LETMEOFF`. Close (6) and car removal (10, 16) feed the
    leaving list too; after 6 a bumper ride reads loading again within a tick; `BUMP 16` ignores its operand and
    answers 0 even after removing an occupied car. Write the chain once in `park.md`, "BUMP and TOUR" (line 957's
    "(frames per second)" becomes 31 ms ticks), and name selectors 9, 12, 13 and 16 in FileFormats
    `vm/instructions.md`. Count the timer in ticks, never seconds (a duration of 0 never unloads). Build the bumper
    family only: go-karts and the water ride end their cars by steering buoys (`DAT_00877b78`, `FUN_0054a040`), so
    their arms stay counted by name, as do steering and the cars' emitters. Count the saved car chunks
    `ParkTrackRides` skips (types 5 and 9). Predict the ride's length (`VAR_DURATION` × 30 × 31 ms) before the run, and
    put the bug back (never set `0x20`).
- [x] **Q179b. The Hot Pot lets its riders off, from boats that float in its pot: the build.** Done 2026-09-30,
  `alexah/209-hot-pot-boats`; FileFormats `sam.md` gains the `Bumper.*Adjust` and `SupplementalMeshes` rows.
  - **Built.** `ParkBumperCars` (beside `ParkTrackRideTable`, which owns it): the bumper family's record (state, wear,
    duration, cars, seated, boarding and leaving lists, the arena) for each slot, the Hot Pot's template only (another
    bumper type counted `BUMPER_TEMPLATE_n`), and the pool of 256 cars and 1024 list nodes; `RideScript.Bump`,
    selectors 1-14, 16 and 17 as decoded, 16's 0 and 6's reopen copied, karts and water a counted no-op as before;
    the track tick before the scripts in each 31 ms step (`ParkRides.OnUpdate`). `ParkBumperBoats` stands each live car
    as its supplemental mesh (`b_car.md2`), synced after the entity pass, with its rider on seat node `Head1`
    (`ParkGuestSprites.Seated`). Decoded here and written to `park.md`, "Where a bumper ride's cars float": the
    placement, the arena centre (the item's `Bumper.*Adjust` plus the placer's offsets: the footprint's middle at every
    turn), the template, the sine table, the draw's bob and heading. Said at the site: the height is the water mesh's
    top (the original's surface lookup is not decoded); the generator is the cars' own. Counted: motion and target
    (Q179c), the lead's sound, rocking, wake, splash, smoke, particles, the performance, saved car chunks. Console:
    `bumpers`.
  - **Confirmed in the game** (`q179b/q179brun.py`; `run2/`, `run3/`, `run4/` on the final build, silent, stock park,
    Belly Bounce sold and the Hot Pot bought at (57,23); `save/` unchanged), predicted first, 5 of 5 each run:
    capacity 4 and duration 25; before anyone boards the ride loading with 4 cars, none seated, 4 boats drawn, all at
    rest; riders let off, every match reading excitement 42 (17, 9, 8 of them); never more than 4 seated; every go 750
    track ticks from `BUMP 3` to empty (5, 3, 2 goes). Photographed and looked at: four boats floating empty in the
    pot (`empty-z70-y135.png`, where Q179's `run3/` showed an empty pot), and four riders seated in four boats in a
    go (`go-0-z55-y200.png`).
  - Tests: `ParkBumperCarsTests` (11) and `HotPotScriptTests` (2, the real `bumper.RSE` driving the cars). Bugs put
    back: the unload flag never set (3 red), the retarget not clearing it (1 red). Review `wf_76d88b08-ff2` (three
    Opus): the sine table's peaks are 255 (the binary's rounded constant), `BUMP 1` with a literal calls nothing, the
    retarget clears the unload, rounding toward nought, uncounted particles and the lead's sound, a boat that will not
    load retried each frame, comment and doc drift; all fixed.
  - **Not confirmed on screen:** the rocking, wake, splash and smoke (counted); the yaw's sense (a random heading
    cannot show it; Q179c's motion will); break, wear, close, removal and a sale with riders aboard (tested only).
    **A loaded park with a Hot Pot comes back with no cars** while its script resumes, so a save made mid-go never
    lets its riders off (no worse than before; `SAVED_TRACK_RIDE_CARS` counts it). Tests read the static
    `ParkState.Current` for the bind, which a later test could cross.
  - Built and tested alone in a worktree: 1521 pass, 0 skip with the game; 620 ran, 901 skipped without; 123 warnings.
  - **Then, at Alexah's word (2026-09-30): the riders sat on top of their boats, whole.** A boat's rider is now drawn as
    their head at the seat node and no body (`ParkGuestSprites.DrawHead`; `ride-operation.md`, "What the head is"): the
    child's `Kidsheads` bank, or the costume's `Costumeheads`, set 0 frame 0, both kinds packed. The decode says the
    original leaves the body standing where it boarded; Alexah chose head only. The original was not run: moving the
    mouse on the desktop was refused by the session's permissions (the reference install's park was patched to research
    the Hot Pot, then restored to the disc's bytes). Console: `scriptvar` writes a script variable as the engine would.
  - **Every built path confirmed in the game** (`q179b/q179bconfirm.py`, `confirm2/`, silent, `save/` unchanged), 8 of
    8, predicted first: A 4 riders drawn HEAD only at z 33.5; B `VAR_BREAKSTAT` 1: wear 2, all at rest, timers frozen,
    and 0: rocking again, counting again; C `VAR_WORN` 1 reaches `BUMP 9` with a literal 0, the fix arm, so nothing is
    worn and the go counts on; D the park's door mid-go: 4 riders off at 42 with time left, the ride closed 17 times with
    the copied reopen between, and reopened a rider boards; E capacity cut to 1 in the ride window: `BUMP 16` leaves 1
    boat, drawn; F sold mid-go: no ride, no boats, nobody riding. Photographed and looked at: `A-heads-*`, `B-broken`,
    `D-closed`, `E-fewer-boats`, `F-sold`. `confirm1/` (5 of 8) was the same run with three predictions of mine wrong:
    `BUMP 9`'s literal, the script closing again each pass, and a 90 s wait after the queue was turned away.
  - Tests: `RiderHeadTests` (2), each red with its bug back (head banks not packed, a costume given the child's head). Built
    and tested alone in a worktree: 1523 pass, 0 skip with the game; 622 ran, 901 skipped without; 123 warnings.
  - **Then the heads' facing, at Alexah's word: they always faced the camera.** Decoded (`ride-operation.md`, "Which
    picture a head shows"): a head bank is 8 headings by 7 heights, and `FUN_0044b510` picks the one the seat node shows
    the camera; the code had walked the heights column. Built (`ParkBumperBoats.HeadFrame`). Confirmed
    (`q179b/q179bheads.py`, `heads1/`, silent, `save/` unchanged): A again, and H1, heads on boats an eighth of a turn
    or more apart show different columns (frames 22, 28, 28, 27 for headings 475, 321, 325, 285); photographed from two
    sides and looked at, every face points away from its boat's fan. H2 was mispredicted: turning the camera 90° moved
    three columns by exactly −2 and one by −1, with rows moving between 2 and 3, because the camera orbits a ground point
    near the boats, 33 units below the heads. `RiderHeadTests` gains the pick (red with row and column swapped). Built
    and tested alone in a worktree: 1524 pass, 0 skip with the game; 623 ran, 901 skipped without; 123 warnings.
  - **Still not confirmed, and why:** the boats' yaw sense (needs the original); a loaded park with a
    Hot Pot (nothing here saves a park, and no save has one); the rocking, wake, splash, smoke, particles and the lead's
    sound, which are not built, only counted.

  The item as written: From Q179's decode
  (`park.md`, "How a bumper ride ends a go, and lets its riders off"), and Alexah's account of the original
  (2026-09-30): *the Hot Pot's riders sit in bumper boats floating in the water on its top, as many boats as the
  capacity is set to; the boats sit empty in the pot, visible and floating idle, when it is not running.* That is
  `BUMP 4` @55 launching one car per unit of `VAR_CAPACITY` at open (anim `0xc`, idle), one rider a `b_car` seat
  (`0x80` id 1), and `BUMP 3` turning them to anim 5 for a go. Build the bumper family's record and cars beside
  `ParkTrackRideTable` (`World/Park`): the boarding and leaving lists, a pool car with its riders, timer and model,
  drawn in the pot with its rider seated, and `BUMP` 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13 and 16 as decoded, with
  16's answer of 0 and 6's reopen copied, not fixed. The track tick counts the timer in `GameClock` ticks, never
  seconds or frames, and its unload arm moves a car's riders to the leaving list and sets loading only when the
  ride-wide seated count is 0. The boats' motion and bumping are Q179c: until then a boat stays where it was
  launched, and that is said at the site and counted (`Unimplemented.Report`), as are the karts' and water ride's
  arms, sounds and emitters, and the saved car chunks `ParkTrackRides` skips (types 5 and 9), which the original
  restores whole. Where a new boat is placed is decoded (`FUN_0054a040`: a random point in the arena circle, up to
  100 tries clear of other boats) and belongs here. Confirm in the game (`q179run.py`, then `q172brun.py`): predict
  the go's length first (`VAR_DURATION` 25 × 30 × 31 ms = 23.25 s from `BUMP 3`); 4 boats photographed floating
  empty before anyone boards; riders let off, each match log reading excitement 42, no more than 4 on at once;
  and put the bug back (never set `0x20`) and see the new test go red.
- [x] **Q179c. The Hot Pot's boats move and bump each other during a go: the decode.** Done 2026-09-30,
  `alexah/212-decode-the-hot-pot-motion`; FileFormats `sam.md` names the four bumper types and the speed they inherit.
  - **Decoded** (`park.md`, "How a bumper ride's cars move"; the functions named `Bumper_*` and `TrackRides_Tick` in
    Ghidra): the two passes of the track tick, the target (a chase 3 in 16, else a buoy, next or random, patience 3
    buoys or 90 ticks), the lead pursuit, the steering (turn by the record's step; thrust only with `0x4000` and not
    `0x80000`, so only a boat with riders in a go drives), the step (friction, the Hot Pot's own heading ease), the
    pairwise bump (each pair kicked twice from one snapshot, restitution over 1024), the rim's reflection, and the
    performance: the script's speed word, 60 as bought, lerped into thrust 10, friction 990, turn 11, restitution 1060.
    Measured over all four bumper types' templates and arenas (-1, -6, -11, -14). Skeptics `wf_ab4da4ce-b87` (three
    Opus): the double kick, the turn's flags, the terminal speed (247, not 262), -3's template, -6's friction 976,
    the push on every scheduler visit; all corrected.
  - **Confirmed against the original under Proton** (`q179c/boatlog.py`, the reference park patched to research the
    Hot Pot, restored afterwards; its clock 1.56 times real time, so ticks only): predicted first, the record read
    performance 60, thrust 10, friction 990, turn 11, restitution 1060; two goes of 750 ticks; the decoded step
    reproduced 2308 of 2323 logged transitions exactly (the 15 others are three torn reads); top speed 228; no boat
    past the rim; filled boats waiting swung their steering heading and stood still. Photographed (`q179b/orig/go-*`,
    four boats moving with wakes) and plotted (`q179c/go1-paths.png`: the two boats with riders drive through the
    buoys, the two empty ones move only when struck).
  - **OpenTPW's baseline** (`q179c/baseline.py`, `base1/`, silent, `save/` unchanged): predicted first, in a go the four
    boats stood where they were for 54 ticks and `BUMPER_CAR_MOTION` grew by exactly 4 × 54; photographed.
  - **Not confirmed:** the bump's impulse and the rim's reflection were read in the disassembly and by the skeptics,
    not replayed against the log (the step check skipped boats near another or the rim).

  The item as written: Alexah (2026-09-30): *they
  have physics, and try to bump into each other during the ride's run.* Decode the bumper family's motion, which
  Q179 left out: `FUN_0054a040` (the next target: a random buoy of the eight `FUN_00545890` lays, or with chance
  3/16 another car of the ride when it has two or more), `FUN_00547f50` (the step: velocity, heading by atan2, and
  the emitters, none for the Hot Pot), and `FUN_00546c80`'s pairwise pass (`FUN_005497b0`, `FUN_005494d0`,
  `FUN_00547170`), with the fixed-point units and the arena radius (template `+0x08` 768, `+0xc0`). Measure over
  all four bumper scripts' cars. Confirm against the original under Proton: the boats' paths in a go, photographed.
- [x] **Q179d. The Hot Pot's boats move and bump each other during a go: the build.** Done 2026-09-30,
  `alexah/213-hot-pot-boats-move`. No FileFormats change.
  - **Built** (`ParkBumperCars`): the performance from the script's speed word (the item's starting speed, 60, kept on
    the record at bind and pushed after each tick's scripts, as the scheduler's visit does; `RideScript` keeps no speed
    word, Q155), the template's four ranges and the eight buoys; the target (chase 3 in 16, buoy next or random,
    patience), the lead pursuit, the steering, the step with the Hot Pot's heading ease, the pairwise bump from the
    stepped snapshot and the rim's reflection, in the track tick's two passes. The draw eases each boat back across the
    tick, as the original's, whose argument was read here (`park.md`, "Read for the build (Q179d)"). Departures said at
    the site: the generator is the cars' own (`ParkGenerator`); arenas are searched in slot order. Counted: another
    ride's arena as a target, the karts' and water's targets. Console: `bumpers` gains velocity, speed, steering, the
    point steered at, the target, patience and the record's four values.
  - **Confirmed in the game** (`q179d/run.py`, `run2/`; `run3.py`, `run3/`; silent, stock park, Belly Bounce sold, Hot
    Pot bought at (57,23); `save/` unchanged), predicted first, 3 of 3 each: W the record reads performance 60, thrust
    10, friction 990, turn 11, restitution 1060, four empty boats at speed 0; G (run2, four goes, every boat ridden)
    top speed 236, never past 4608 of the centre; L every go 750 track ticks (four of four); E (run3, two riders sent)
    the two ridden boats drove 80663 and 71066 units, the two empty ones stood still in 44 of 75 samples, moving only
    after a strike, top 189; R OpenTPW's per-tick census replayed through Q179c's `stepcheck.py` model: 98 exact, none
    mismatched. Photographed and looked at: `run2/pot3.png` and `run3/pot3.png`, the boats moved between frames, each
    toward its rider's face with the fan behind, as in the original's `q179b/orig/go-pair.png`; the empty boat stays put.
  - Review (one Opus, read-only, against the disassembly): `HeadingOf( 0, 0 )` gave 0 where the binary's negated
    integer loads +0 and gives 256, which turns the bump's branch for two boats at rest; the drawn heading's ease is
    truncated; a comment's "under 1/π". All fixed. **`run4/` on the final build**, predicted first, 3 of 3: W again; E
    the two ridden boats 80610 and 70952 units, the empty ones still in 44 of 74, top 188, at most 4594 out; R 285 exact,
    none mismatched; `run4/pot3.png` looked at: in a one-rider go the three empty boats stand where they float in all
    three frames while the ridden one crosses the pot.
  - Tests: seven in `ParkBumperCarsTests` (performance, heading, ring, only a ridden boat in a go drives, a full go in the
    pot under 247 with bumps and chases, the double kick, the rim). Bugs put back: thrust without `0x4000` was green at
    first, because the empty boat's random heading never pointed within 22 of its buoy; the test now aims it, and goes
    red. The negative nought: red. My heading prediction for +x was 128; the binary's constant, just over 1/π, gives 127.
  - Built and tested alone in a worktree: 1531 pass, 0 skip with the game; 630 ran, 901 skipped without; 123 warnings.
  - **Then, at Alexah's word (2026-09-30): the riders' heads were "hilariously large".** Decoded: `FUN_0044b510` sets each
    head sprite's scale to 0.685 every frame (`0x0074ced0`; `ride-operation.md`, "Which picture a head shows"); OpenTPW
    drew it at a body's 1.0. Alexah chose the original's scale. Built (`ParkGuestSprites.HeadScale`, branch
    `alexah/214-rider-heads-scale`). Confirmed (`q179d/heads1/`, silent, `save/` unchanged, 3 of 3 again): predicted
    every head about 0.69 of its `run4/` height at the same camera; `heads1/compare.png` looked at, the heads about
    two-thirds their old size, half a boat wide. No test pins the constant (a test of it would only restate it).
  - **Not confirmed:** the bump's impulse and the rim against the original's log (the replay skips boats near another
    or the rim; tested only); the boats' yaw sense is read from the frames, not measured; the wake under a boat is not
    drawn (`BUMPER_CAR_WAKE`).

  The item as written: From Q179c's decode (`park.md`,
  "How a bumper ride's cars move"). In `ParkBumperCars`: the performance from the script's speed word, the target,
  the chase, the steering, the step and the Hot Pot's heading ease, the pairwise bump and the rim, in the track tick's
  two passes; the draws from the park's generator. The other types' particles and sounds stay counted. Confirm in the
  game against `q179c/go1.jsonl`'s numbers: a go of 750 ticks, top speed under 247, no boat past the rim, only boats
  with riders driving; photographed in a go; and put the bug back (thrust without `0x4000`) and see the test go red.
- [x] **Q180. A loaded park's scripts take their turns on the save's ticks.** Done 2026-09-30,
  `alexah/215-saved-script-ticks`; FileFormats the same branch (the records are stored newest first). Found by Q174c's decode (`park.md`, "The
  scheduler"). The `RSSE` module's header puts the scheduler's globals back (`0x005598d7`): its tick counter (6,055 in
  the shipped park) and the next script handle (16); each script keeps its saved handle at `+0x08`, so its turn
  (`(handle ^ tick) & 7`, `0x005516e9`) keeps its phase across the load, and each is put at the head of the list
  (`0x005599d3`), reversing the order of turns within a tick. `RideScriptScheduler` starts at tick nought and numbers
  the bound scripts itself (`Scheduler.Spawn`), so a loaded script takes its turns on other ticks and in another order:
  the security cameras' saved waits end on turns at ticks 73 and 74 after the load in the engine, or 81 and 82, and in
  ticks 76 to 83 here. Keep the saved handles, the saved tick and the next handle (mind every reader of a script's id:
  `FINDSCRIPT`, `COAST`'s ride handle). Confirm: `rides` over the cameras through their first wait after a load, each
  passing on the tick the engine's rule gives.
  - **Built:** `ParkScriptStates` reads the header's tick and next handle and keeps the records' order;
    `ParkRides` restores both (`RideScriptScheduler.Restore`) and binds each saved script under its own handle in that
    order (`Spawn( name, id )`), anything else numbered from the next handle; `Advance` walks newest first, as
    `FUN_005516b0` walks from the head, over a copy (a departure, said at the site). `FINDSCRIPT` and `COAST` needed
    nothing: every reader reaches a script through the registry by the id it was bound under. Console: `rides` prints
    the tick, and each line the script's handle and position.
  - **Confirmed in the game** (`q180/run.py`, `run1/`; silent, stock park, `save/` unchanged), predicted first: the
    load reads tick 6055 with the cameras on handles 8 and 9 at word 14, role 6; stepped a frame at a time, handle 8
    leaves word 14 on tick 6136 (last read waiting at 6135) and handle 9 on 6137, both onto role 4 (2 of 2). Shots
    `B1-before` (camera down) and `A-after-both` (up), looked at. Tests: the cameras' 81st and 82nd ticks, the order
    of turns, the header, a bought thing's handle 16, the newest-first walk, a script killed mid-walk; six planted bugs
    each failed a test. Review `wf_8ad60fc1-e89` (one agent): three findings, fixed (that test; saved handles only from a
    module read whole; a short header read as far as it goes).
- [x] **Q181. A `GETTIME` reading kept in a script variable across a load. Decode first.** Found by Q174c's probe
  (`q174c/clockvars.py`). `GETTIME` stores the clock raw; twelve Lost Kingdom ride scripts keep one in `VAR_STARTNOW`
  (`bumper`, `GoKarts`, `incagod`, `Lookout`, `Monkey`, `Mumbo`, `PorkPie`, `Spider`, `Totem`, `TourRide`, `Volcano`,
  `Wateride`), the gift shop in `VAR_TIMER1`, `end` in `VAR_ENDTIME`, and each subtracts it from the clock later. The engine's
  clock reads the saved reading again after a load, so the difference survives; OpenTPW moves a saved deadline onto its
  own clock (`ParkRides.Moved`) but cannot tell which variables hold readings, so a loaded `VAR_STARTNOW` is millions
  of milliseconds off. Alexah's jungle saves hold one in eight scripts (`spider`, `mumbo`, `gokarts`, `tourride`,
  `incagod`, `monkey`, `porkpie`, `giftshop`); the shipped park, none. Decode what each script does with the reading,
  then choose: run the scripts on the save's own clock, as the engine does (a float clock holds 114 million ms only to
  8 ms), or move the readings a walk of the script finds. Confirm, after Q167: a loaded ride's cycle timed from
  `VAR_STARTNOW` ends when it would have without the load.
  - **Decoded** (`park.md`, "What a kept `GETTIME` reading is"; branch `alexah/216-decode-the-kept-readings`): every
    kept reading is a deadline, the clock plus 10,000 (5,000 once, 4,000 or 6,000 for `VAR_TIMER1`), read only by
    `GETTIME VAR_TEMP; SUB VAR_TEMP, <deadline>, VAR_TEMP` and a sign branch; only `VAR_STARTNOW`, `VAR_TIMER1` and
    `VAR_ENDTIME` keep one across turns, and `VAR_TEMP` and the result register only when a turn ends inside an
    unlocked pair. Of Alexah's eight jungle holders, `mumbo`, `monkey` and `giftshop` read the stale one first. Chosen:
    move the readings (Q181b), not the clock. Skeptics `wf_8ddcb5a1-fe2` (two Opus): eleven claims, five corrected, all
    folded in.
  - **Measured in the game** (`q181/run.py`, `run4/`; silent, stock park, `save/` unchanged), predicted first: a Hot
    Pot with one rider and `VAR_STARTNOW` planted at the shipped clock plus 10,000 stayed loading for 14 s (1 of 1);
    the control, 0, went 2.3 s after the write, predicted within 2 (a reading late). Shots `B1-still-loading`,
    `A1-going`, looked at. No code changed but a comment.
- [x] **Q181b. Move a kept `GETTIME` deadline across a load.** From Q181's decode (`park.md`, "What a kept `GETTIME`
  reading is"). At `ParkRides.Resume`, move through `Moved` every variable a walk of the script finds written only by
  `GETTIME` and `ADD` to itself (`VAR_STARTNOW`, `VAR_TIMER1`, `VAR_ENDTIME`), never by name; and, when the saved
  script stands on a `SUB` whose word before is `GETTIME V`, that `V` and the result register (check the save carries
  `+0x48` and that `Resume` restores it). Say the deviation at the site. Tests: Mumbo's and Monkey's boarding timeouts
  after a load, the gift shop's idle, a split pair; put each bug back. Confirm, after Q167 (or a planted reading if
  Alexah's saves still will not load): a loaded Mumbo or Monkey starts its go 9.5 s or 7.3 s after the load with nobody
  boarding, predicted first, a screenshot and the `rides` line.
  - **Built** (branch `alexah/217-move-kept-readings`): `RideScript.MoveKeptReadings`, from `ParkRides.Resume`, through
    `Moved`; the walk (`KeptReadings`) keeps a variable `GETTIME` writes that appears only as `GETTIME`'s destination,
    `ADD`'s first operand or a `SUB`'s read: 51, 2 and 1 bodies, as Q181's sweep. A saved `SUB` after `GETTIME V` moves
    `V` and the register; nought stays. The load's line counts them (`kept clock readings moved`) and names each script.
  - **Confirmed in the game** (`q181b/run.py`; silent; a COPY of Alexah's jungle `New Save.TPWS` laid as `Easymode.TPWI`
    in a symlinked game folder with its own `save/`; the real `save/` unchanged): the load moved 5, predicted 8 - the tour
    ride, Inca god and gift shop are not in the Instant Action catalogue, so nothing binds them (Q186). That save crashes
    `ParkGuestSprites` (note under Q167), so the timing ran on a throwaway build stepping past it, never committed:
    Mumbo, 1 rider, nobody boarding, left its loop at +313 ticks (predicted 307-315), `run3/`; with the move taken out it
    was still looping at +401, `control1/`. Monkey went at +190 both times because two guests filled it, so it tested
    the fill, not the deadline. Shots `B-mumbo-boarding`, `A-mumbo-later` and the control's, looked at. Tests
    (`RideScriptKeptReadingsTests`): Mumbo, Monkey, the gift shop, a split pair, nought, an unsplit `VAR_TEMP`, the
    corpus; five planted bugs each failed one. No `ParkRides`-level test: no shipped placed script declares
    `VAR_STARTNOW`, so removing the call in `Resume` leaves the suite green; the game run is its proof. Worktree: 1544
    pass 0 skip; 633 ran 911 skipped without; 123 warnings. Review (one Opus agent, read-only): no defect; a count
    made with no save clock now is not made, and no branch lands on any of the 57 `GETTIME`-`SUB` pairs.
- [x] **Q182. A frame's ticks and clips read one clock in the engine. Decode first.** Moved from Q174c, where Q174b's
  review left it as a note (`park.md`, difference 6). A triggered clip starts at the tick's own instant in OpenTPW
  (`RideScript.StartAnimation`, `ParkRides.MillisecondsAt`), where the engine stamps a fresh start with the frame's
  snapshot `DAT_007b496c` (`FUN_00472bc0`, `0x00472bff`), the one its advance reads (`0x004736b3`); its scripts' own
  deadlines read the live clock (`0x0055299d`), which barely moves through a catch-up, where each tick here has its own
  instant 31 ms on. Reached only when a frame runs more than one tick; not measured. Decode how far the live clock moves
  across one frame's ticks, then build (Q182b).
  - **Done 2026-10-01, `alexah/218-decode-the-frame-clock`. Decode only.** `park.md`, difference 6, "What the engine's
    ticks read across one frame": the frame reads the clock twice above the catch-up loop (snapshot `0x0054f475`, `now`
    `0x0054f47f`) and nothing in the loop moves it; its one stepper `FUN_00402ef0` runs once a drawn frame and only
    while latched, which nothing offline sets. So every tick of a catch-up sees the frame's `now` plus its own running
    time. The original was not run: this computer is up 8 days, so its clock holds only multiples of 64 ms (Alexah
    warned the same); the per-tick running time stays unmeasured, the `GetTickCount` rings named as the instrument.
  - **Measured in the game** (`q182/run1/`, silent, stock jungle park, `save/` unchanged, predicted first, 2 of 2): worst
    frames 7.04, 7.05, 7.13 ms at 143.9 fps, so no frame ran two ticks; a first Hot Pot put down took one 96.14 ms frame
    (three or four ticks). Shots `S1-running`, `S2-bought`, looked at. No code; no test. Skeptics `wf_850d5f4f-228`
    (core held; added: a message box in a tick pauses the clock, the engine's clamp runs 65 ticks, a third timing ring).
- [x] **Q182b. Each tick keeps its own instant: say the deviation.** From Q182's decode (`park.md`, difference 6).
  The engine hands every tick of a frame the frame's one instant, so a script's wait ends only on a frame boundary and a
  clip triggered mid-catch-up starts from nought: frame-rate dependent (about 7 ms late at 144 fps, up to a frame at
  10). Alexah, 2026-10-01: keep OpenTPW's frame-independent instants (`ParkRides.MillisecondsAt`, a tick 31 ms after
  the last) as a deliberate deviation, not copied. Say it at `MillisecondsAt`, replacing its comment's reason (a
  one-tick `WAIT` coming due at once, which does not hold for the engine), and at `RideScript.StartAnimation`; mark
  difference 6 in `park.md` as kept by decision. Separately, match the engine's 2000 ms clamp, which runs 65 ticks
  where `GameClock` runs 64 (`0x0054f49b`), or say that too. Confirm: a stall forced past 2 s, the tick count across
  it in a census, predicted first, and a shot.
  - **Done 2026-10-01, `alexah/220-say-the-tick-instant`.** Said at `ParkRides.MillisecondsAt` (its old reason
    replaced), `RideScript.StartAnimation`, and difference 6 in `park.md`, marked kept by decision. The 65 is the
    engine's loop test, `while now > last` (`0x0054f4ad`): a tick runs at the start of its 31 ms, ours at the end.
    Matching it moves every tick's phase and `PartialTick`, so Alexah chose (2026-10-01) to keep 64 and say it, at
    `GameClock.ParkCatchUp` (the lobby's 17 against 16 too). A capped frame now logs its count. Game (`q182b/run2/`,
    silent, stock jungle park, `save/` unchanged, predicted first, 2 of 2; `run1/` the same before the line's wording
    was fixed): a 3 s `SIGSTOP` stall logged `a 3014 ms backlog capped at 2000 ms, 64 ticks run`; `state` ticks 177 to
    241, 64 against 64.2 predicted. Shots `S1-before`, `S3-after` (the date on to 1/2, guests walked on), looked at.
    Test: the cap test now pins the 16 ms left owed too; rounding up instead turns it and four others red. Worktree:
    1544 pass 0 skip; 633 ran 911 skipped without; 123 warnings. Review `wf_f1594c0e-584` (Opus, read-only): every
    address held; the line said "frame" for the backlog, fixed.
- [x] **Q183. A script's walks are stepped in its own turn, where the engine steps every script's once a frame.** Found
  by Q175's decode (`ride-operation.md`, `FUN_00557ab0` and `FUN_00557d80`). `RideScript.StepTheWalks` runs at the head
  of a running script's turn, every eighth tick, and restamps start at both arrivals; the engine's `FUN_00557ab0` steps
  every script in the list once a park frame, after the 31 ms catch-up loop (`0x0054fa08`), and restamps start only on
  arriving on the ride (`0x00557e79`; `0x00558018` writes the state alone). Both differences are named at their site,
  and a rider's arrival is noticed at most a turn late. Nothing in the engine reads start in state 4 but the save, which
  copies it raw; nothing here reads it. **Alexah, 2026-10-01: step every script's walks once a tick, not once a
  frame**, right after that tick's scripts (`ParkRides.OnUpdate`'s loop), so the original's order holds (the walks after
  the scripts) and an arrival is noticed within one tick (31 ms), as at the engine's own ~30 fps, where once a frame would
  tie it to the frame rate (7 ms late at 144 fps, 33 at 30) and the turn here is up to 248 ms late. Say that deviation at
  the site. Match the restamp: start only on arriving on the ride (`0x00557e79`), the state alone on arriving off
  (`0x00558018`). Confirm: each promotion's instant against the clock in a census, within one tick, predicted first,
  and a shot.
  - **Done 2026-10-01, `alexah/222-walks-once-a-tick`.** `RideScriptScheduler.StepTheWalks` steps every script's
    walks, newest first, and `ParkRides.OnUpdate` calls it once a tick right after that tick's `Advance`, at its instant;
    `Turn` no longer steps them. The deviation is said at both sites (`0x0054fa08`, now in `addresses.md`). Start is
    restamped on arriving on only; a finished walk off keeps its leg, which the `rides` census now shows. Each arrival
    logs `arrived on|off at T ms, due D, L ms after`. Game (`q183/run1/`, silent, stock jungle park, Q184's three kind-3
    guests sent to the Jungle Spray, `save/` unchanged, predicted first): 6 of 6 arrivals, 13 ms after due for the 700
    leg and 16 for each 1100, against up to 248 before; paused on one, `state` read ticks 381, × 31 = 11811 ms, its
    arrival instant. Shots `on-handle44`, `on-handle45`, looked at. Tests: `ParkTickTests.EveryWalkIsNoticedWithinOne
    TickOfComingDue` (the park run, three arrivals at 16, 13, 16) and `RideScriptWalkTests.AFinishedWalkOffKeepsTheLeg
    ItWalked`; stepping in the turn again, dropping the park's call, and restamping the walk off each turn one red.
    Worktree: 1546 pass 0 skip; 633 ran 913 skipped without; 123 warnings. Review `wf_00a61c40-a32` (Opus, read-only): no defect; three stale comments and the address row
    fixed.
- [x] **Q185. Correct what our own pages say wrong.** Found by the fork review of 2026-09-30 (Aluzed's
  `github.com/aluzed/OpenTPW-decomp`; its items are named by id, and `CLAUDE.local.md` has the path), each re-checked
  by an independent refuter. Docs only, lines as of `537428f`:
  - FileFormats `vm/instructions.md`, `WAITABS`: the engine adds the operand to the clock unscaled (`0x005538c9`), so
    it is a delay without the speed divisor, not a deadline; `docs/exe/park.md:605` already says so (vm-3).
  - `docs/exe/saves.md`: the 824 + 711 preamble table and its "Divergence, unresolved" paragraph (148-172) and the
    bullet at 212 give way to a pointer to FileFormats `saves.md`, "Header", and the loader `FUN_00416240` (its `0x500`
    and `0x100` fields; the magic `0x01221985` at `0x00749870`). Also 139-140 (FileFormats `master` reads the version
    right now), 174-178 (nine park files agree, not `Easymode.TPWI` alone) and 202 (the original's own `Config.tcf` and
    `gms.dat` exist, in Alexah's Full Simulation saves) (economy-v4, refute rank 8).
  - `docs/DECISIONS.md:71`: `FUN_00672e60` is the pQGT/MUVf codec's block decoder, which no shipped movie reaches, not
    "the intro-movie decoder"; the conclusion stands (fmt-media-v3).
  - `docs/exe/lobby.md:51-52`: `gms.dat +0x24` stores 0 (Full Simulation) or 1 (Instant Action); Select maps it to
    `SetGameType` 0 or 2 (`0x005c85ae`), and type 1, the online type, never comes from `gms.dat`. `boot.md:26` and
    `:157`: `0x00550ca0` is the mode object's lazily run constructor, which derives the type from flag bits
    (`0x2000000` gives 2, `0x1000000` gives 1, otherwise 0); `0x00550d80` is the only setter (gap3-8, refute rank 3).
  - `docs/exe/park-engine.md:211`: the `Easy_Standard.sam` pass runs only in game type 2 (`0x00515811`..`0x0051585c`).
  - `docs/exe/audio.md:128`, `:133`: the ride sound's position (`FUN_00556b90`) is the centre of the thing's cell
    rectangle ×10 in x and z, at the node's base height; it drops the box's heights. `docs/exe/ride-operation.md:2524`:
    `FUN_00466b70` is the thing's box (x and z from `+0xc0`/`+0xc4`, max + 1, ×10; heights from the `.hmp` at `+0xcc`,
    `+0x1c`/`+0x28`, plus the base Y), with eleven callers, not "the sound position" (gap2-9, refute rank 8).
  - Label the unused `NAudio` reference in `OpenTPW.csproj` (only ModKit's `SoundViewer` uses NAudio; rule 3).
  The FileFormats edit goes on a branch fast-forwarded into `master` with this one. Confirm: no game run; grep each
  corrected claim afterwards and find no stale copy.
  - **Done 2026-10-01, `alexah/223-correct-our-pages`** (FileFormats: the same branch name). Each claim re-read in
    Ghidra first. Corrected as listed, and the stale copies the greps and the review found: `park-engine.md`, "The save
    container" (the old cut, now a pointer), `park.md`'s container line, `saves.md`'s header branches, the comments in
    `SaveReader.cs` (its reads unchanged), `IslandPanel.cs`, `RideNodes.cs` and `RideScript.cs` (the cells, not the
    model's box). Also found: the online-header flag reads an author header on any non-zero value (FileFormats
    `saves.md`), `0x00550d80` has five callers and the constructor `0x00550ca0` 85 call sites under 26 guards
    (`boot.md`). The item asks no game run; one was made for `SaveReader`'s comment edit (`q185/run1/`, silent,
    `save/` unchanged): the shipped park loads, `rides` lists 14 object scripts, handles 1-15 less 5, as saved
    (predicted 14 for its `scripts` field, which reads 16 with the two no object holds); shot `park` looked at. Review `wf_6dac9b6e-f43` (three Opus, read-only): the claims hold;
    seven findings, fixed. Worktree: 1546 pass 0 skip; 633 ran 913 skipped
    without; 123 warnings.
- [x] **Q186. A Full Simulation player is handed the Instant Action park, and nothing counts it.** Found by the fork
  review (gap3-3, gap3-9, refute rank 2). In the original a new Full Simulation player's first park loads no file:
  the new world (`FUN_00407d80`, `FUN_00515540`) and the level load build it fresh (Q197). OpenTPW gives every player
  `Easymode.TPWI` and the `Easy_` balance (`Level.ReadPark`). Count `FULL_SIMULATION_NEW_PARK` where `Level` knows the
  player, only when `Players.Roster.Current is { InstantAction: false }` (`ReadPark` is static and takes only a theme,
  and a console `park jungle` has no player), and say the deviation at `ReadPark` and at `easyMode: true`; its comment
  that "the two are identical" holds only for Instant Action. Add one line to `docs/STATUS.md`, "Does not", and one
  under `docs/PLAYER-GAPS.md` gap 7. Whether a Full Simulation player keeps Easymode meanwhile or takes the empty-park
  path, which cannot run a jungle park today (`README.md`), is Alexah's call: ask, do not switch it. Confirm with the
  `unimplemented` census: a Full Simulation player made for the run counts it once on entering Lost Kingdom, an
  Instant Action control does not; put the condition back to show the control count. Delete only the player folder
  the run made.
  **Done 2026-10-01** on `alexah/224-full-simulation-new-park`. Alexah chose: keep Easymode meanwhile. `Level.CountAFullSimulationPark`,
  called after `ReadPark`, counts `FULL_SIMULATION_NEW_PARK`; the deviation is said at `ReadPark` and `InstantAction`. Game
  (`q186confirm.py`, players made at the slots, entered by `enter`): `q186/fs1/` Full Simulation 1x, 1x 5 s later
  (predicted 1); `q186/ia1/` Instant Action 0x (predicted 0); shots looked at; only the run's player folders deleted,
  `save/` unchanged. Test `LevelFullSimulationParkTests`: the condition dropped and the report dropped each go red.
- [x] **Q187. First person keeps the top-view sprites where the original swaps to `.FPC`.** Found by the fork review
  (peeps-v1, peeps-v2, gap6-2, refute rank 5). Every sprite bank loads as `.TPC`. Entering first person
  (`FUN_0042ae70` calls `FUN_00542420` at `0x0042af85`) reloads every bank whose `.ESP` byte `0x10C` is set from its
  `.FPC`, and leaving swaps back (`FUN_00542640` at `0x0042afba`); an `.FPC` picture is the same figure seen from
  ground level, not a level of detail. In Lost Kingdom: the eight kids, guards, handymen, both mechanics, researchers,
  `SPR_TI`, `SPR_DI` and `SPR_NA`. The flag is 1 in 27 of the 29 banks with an `.FPC` and 0 in all 17 without one.
  OpenTPW always loads `.TPC` (`ParkGuestSprites.cs:255`), unsaid and uncounted. First write the facts once: the role
  and the state-9 call `0x0054ed2c` on `boot.md`'s `0x00540900` row; `FUN_00542420`, `FUN_00542640` and the ride
  view's call at `0x0042a6ee` (conditional on the item's `+0x80`, untraced, unreached here) in `park-engine.md` beside
  "FUN_0042ae70 is the first-person toggle"; `lobby.md:123` corrected (the four statics are built at startup;
  `DAT_008768fc` is always 0); FileFormats `sprites.md` (`0x10C`, the counts, `SPR_EX`'s 219 and 210 pictures, and that
  the pair differ in viewing elevation, not only size). Count `FIRST_PERSON_SPRITE_SWAP` in
  `ParkCamcorderCameraMode.Enter`. Then build: on entering, reload the flagged banks from `.FPC`; on leaving, from
  `.TPC`. `ParkGuestSprites` builds one atlas from every picture, so rebuild it on the swap or hold both sets, and say
  which at the site. Confirm: a first-person screenshot of the original under Proton beside ours, the upright figures
  predicted before looking.
  **Done 2026-10-01** on `alexah/225-first-person-sprites`. Facts written once: `boot.md` (`0x00540900`), `park-engine.md`
  ("Entering and leaving first person", the swap and the ride view's `0x0042a6ee`), `lobby.md` (`SpriteBank_Load`, the
  four statics found); FileFormats `sprites.md` (`0x10C`, 27/2/17, `SPR_EX` 219/210, the `.FPC` taller for its width in all
  29 pairs). `SpriteBankFile.UsesFirstPersonPictures`; `ParkGuestSprites.UseFirstPersonPictures`, called by
  `ParkCamcorderCameraMode.Enter` and `Leave`, packs the atlas again whole (the deviation in how is said at the site).
  `FIRST_PERSON_SPRITE_SWAP` was not added: the swap is built, and nothing unbuilt is left on the path. Game (`q187run.py`):
  `q187/run2/` the log reads 20 banks, 0 from `.FPC` at load, **12** on entering (predicted 10, wrong: the park packs six kid
  banks and six staff and entertainer banks, not eight and one; the eight left are the six kid heads, the costume head
  and the balloons, all unflagged), 0 on leaving; `pair-fix` against `pair-control` (main's build) from the same places,
  looked at: the near child seen from above becomes upright and face-on. The original under Proton, first person in the
  reference park, `q187/orig/q187-13.png`, looked at: a handyman and a child upright at eye level, as predicted. Tests
  `ParkGuestArtTests`: the flag never read and the pack always `.TPC` each go red. `save/` unchanged. Review
  `wf_8cf0bcf7-43d`: `Build` never deleted the model it replaced (on `Add` and `Remove` too), fixed, `q187/run5/` re-run
  20/12/0 and looked at; three comment and naming nits fixed; the decode's claims held. Worktree: 1549 pass 0 skip; 634
  ran 915 skipped without; 123 warnings.
- [x] **Q189. Light the park as the original does.** Found by the fork review (gap6-7, gap6-8, refute rank 4). The
  model is decoded: models through `FUN_0057aa10` → `FUN_00574660` → `FUN_005741b0`, terrain through `FUN_0056f670` →
  `FUN_0056ef10` → `FUN_00574530` → `FUN_005741b0` (terrain normals `(dh·k, 1.0, dh·k)`, not normalised). Per vertex:
  the ambient as a colour (`ThemeEngine.AmbientLightLevel`'s R, G, B / 255: 0.333, 0.333, 0.408 in the base file and
  all four themes' `Standard.sam`) plus the sun's colour × max(0, −d·n), where d is `LightNormal`, the direction light
  travels, taken into the model's space; each channel clamped, ×255, and ANDed with a channel mask at
  `[0x0087a248]+0x2c` (`0xFFFFFFFF` in play). The sun is built at `0x0054eca4`, its inputs filled by `FUN_004080e0` at
  `0x0054ed3f` and applied by `FUN_00458590` at `0x0054ed64`, once in state 9; nothing time-driven writes them.
  OpenTPW uses a flat 0.4 ambient and calls the sign "a CHOICE" (`Level.cs:233-251`). First write it into
  `park-engine.md`, "The blocking unknowns", item 2, and `park.md`'s ThemeEngine section, and cite `FUN_005741b0` in
  `Level.cs`. Then change the shader: coloured ambient and the clamped sum, per vertex, or say the per-pixel
  difference at the site. Upward and sun-facing faces saturate to the texture's full brightness; shading shows only
  where −d·n falls below about 0.67. Confirm: the same view in the original under Proton and in ours, where shading
  appears predicted first. Not claimed here: that the original has no day and night; only a frame pair from the
  original a few game days apart may say so.
  **Done 2026-10-01** on `alexah/226-light-the-park`. Facts written once: `park-engine.md`, "The lighting model" (blocking
  unknown 2 settled; a row runs +Z), `park.md`'s ThemeEngine section; FileFormats `models.md` (heightfield `+0x20`/`+0x24`,
  the authored height range, 5/5 files). Found on the way: the terrain's normals are `((h[x-1]-h[x+1])k, 1, (h[r-1]-h[r+1])k)`,
  not normalised, `k` = 1/(`+0x24`-`+0x20`) (`FUN_0056e3f0`; jungle 1/70), and the last row steps back 128.
  `ParkLight`, `test.shader` per vertex (clamped, rounded to a byte), `ParkGround.NormalAt` in the original's form. The lobby
  and screen-drawn previews keep the old lighting. Game (`q189run.py`): log `jungle: lit per vertex by ambient (0.333,
  0.333, 0.408) and a sun travelling (0.408, 0.408, -0.816)` as predicted; frame mean 127.7 -> 113.2 (0.886, predicted
  0.85 +- 0.04); flat ground R, G x0.81-0.85, B x0.92-0.95 (predicted 0.82 / 0.92); the south-facing pillar ~unchanged.
  Beside the original's opening view (`q187/orig/q187-08.png`, an earlier run under Proton; the lighting is set once),
  `q189/before4`, `after4`: lawn (97,122) -> (81,104) against (82,100), kerb R 148 -> 122 against 122, brick nearer in all
  three channels. Our blue runs higher than the original's on every surface; not the lighting, not chased. Review
  `wf_4d901afb-06d` (two comments fixed; uniform-layout test added). Worktree: 1552 pass 0 skip; 635 ran 917 skipped
  without; 123 warnings.
- [x] **Q190. Build `ADDHEAD` and `DELHEAD`.** Found by the fork review (vm-12; lead: Aluzed's fork, T-007 item 18).
  Six Lost Kingdom rides reach them (incagod, Monkey, Mumbo, PorkPie, Spider, Volcano); both are unbuilt and counted.
  `ADDHEAD` (`0x00554c3e`) does nothing with no head table (`+0x30` null) or no free slot among the `+0x4c`;
  otherwise it draws from the world generator (`FUN_00516330` on `[0x007cf83c]`; SHR 1, abs, mod `+0x4c`,
  `0x00554caf`..`0x00554ccb`) until a slot is free, stores the visitor there, and attaches head node slot + 1 (mask
  `0x80`, `FUN_0044b220`, `FUN_0044b410`). `DELHEAD` (`0x00554d26`) detaches and zeroes every slot holding the visitor
  (`FUN_0044b4c0`), with no break. Neither writes the register. Each retry is one draw, so use the park's world
  generator, never `System.Random`, or every later draw shifts. First read how these rides seat their riders today
  (`park.md:270`, `ParkRides.cs:132`, Q22), and write the block in `park.md` beside the limbo subsystem and in
  FileFormats `vm/instructions.md`. Confirm: a screenshot of riders on one of the six rides beside a census of its
  head slots, predicted first.

  - **Built** (branch `alexah/227-heads`): `RideScript.AddHead`/`DeleteHead`/`Heads`, the table sized by
    `RideNodes.HeadCount` in `ParkRides.NodesFor` (now read for a script carrying `ADDHEAD` too) and restored from the
    save's head block (`SavedScript.Heads`, which `ParkScriptStates` had skipped as "two further blocks": the head table,
    then the directory). `ParkGuestSprites.HeadOnRide` draws a rider in a table as their head on the node, no body; the
    `rides` census prints `heads n/slots` with each node's position beside the drawn model's. Counts measured on all 67
    jungle items and matched by every table in Alexah's played saves (`q190/heads`). Departures said at the sites:
    no table for a script without the pair, the script's own generator, a 65,536-draw stop, heads at rest
    (`RIDER_HEAD_REST_POSE`, `RIDER_HEAD_ON_A_FACE`). Docs: `park.md`, "The head table"; FileFormats `vm/instructions.md`,
    `formats/saves.md`. Tests `RideScriptHeadTests` (5); the draw, the no-break, a register write and the count each put
    back and caught. Review `wf_c271aa67-e3b` (one agent): every address confirmed, six comment and consistency
    findings fixed (the gap counted once a head hung, a saved table only where the pair reads it). Worktree: 1557 pass 0 skip;
    639 ran 918 skipped without; 123 warnings.
  - **Confirmed in the game** (`q190run.py`, silent, `save/` unchanged): a bought Mumbo, predicted 0/5 then heads equal
    to riders: `run3` 4/4 (0/5, 5/5 with each head at the drawn node to 0.1, 0/5 let off), `run2` 43 census lines all
    heads = onride. Shots `run3/aboard-1`, `off` looked at: heads at the tentacles while full, none when empty, a little
    off the moving tentacles (the rest-pose departure). After the review, `run4` 4/4 again on the final code (4 aboard,
    `RIDER_HEAD_ON_A_FACE` 4x, once a head); its shot, the ride at rest, has heads on the tentacle tips. Not run: a loaded save's restored heads (Alexah's jungle save
    still throws in `ParkGuestSprites`, Q167's note), the other five rides, the original beside it.
  - **Then, at Alexah's word (2026-10-01): the heads follow the ride** (branch `alexah/228-heads-follow-the-ride`).
    Mumbo's heads stood at rest and clipped through the moving tentacles. Decoded the face anchor (`park.md`, "The head
    table"; FileFormats `models.md`), measured on all 248 anchors (247 at rest within 0.05); `ModelFile.PointOnFace`,
    `LobbyModel.TryGetDrawnNode` draw a head on its tentacle as the morph poses it, or where its car's mesh is drawn.
    Tests: two more (at rest on the node, moved with the tentacle); U/V swapped, no normal offset and no vertex order each
    put back and caught. Game (`q190brun.py`, `q190crun.py`, silent, `save/` unchanged): Mumbo's five heads moved 5.0 to
    8.3 units in height with their tentacles in a go and sat on them in the shot (`follow1/go-1`); Rocky Racers' four
    followed the cars (`racers1/go-1`). Predicted wrong: before the go the heads already moved, as the boarding clip
    morphs the tentacles too. Review `wf_f5d32f08-574` (one agent): every address confirmed; heads looked up by
    node index, the ancestor walk guarded, the tentacle test made to move one corner; `follow2` after it, five heads on
    the tips (shot looked at). Worktree: 1559 pass 0 skip; 639 ran 920 skipped without; 123 warnings. Still counted: a head's picture turn (`RIDER_HEAD_TURN_AT_REST`).
- [x] **Q191. Read the `.hmp`, and lift the build squares over a built cell with it.** Found by the fork review
  (gap2-1..gap2-10, refute rank 9; lead: Aluzed's fork, the header). Build the reader with its consumer (rule 9).
  The layout, over all 435 files (jungle 110, fantasy 106, hallow 110, space 109): signature dwords `0xAB1E0003` and
  `0x00640005`; u16 cols (x) and rows (z); three offsets; a six-float box at `0x18` (min xyz, max xyz); one
  (5·cols) × (5·rows) height raster (byte = trunc(y × 2.55)); a per-cell maximum-height grid; a plane of `Info.Shape`
  marks; size 48 + 27n. The loader `FUN_00451640` rebuilds a missing or mismatched file (`FUN_00451880`) and writes it
  back; the shipped files come from an older generator (29 hold a 255 where 2.0's caps at 254), so read them, never
  rebuild: count `HMP_REBUILD` where the original would rebuild (missing, bad signature, cols or rows unlike
  `Info.Shape`'s box, or `Engine*Override` for the fixed items). The lift (`MARKER_LIFT_OVER_BUILT_CELL`,
  `ParkBuildMarkers`): `FUN_00452ae0` answers grid byte / 2.55 + the thing's base Y (the root node's world Y,
  model `+0x78` → `+0x44`), undoing its rotation (`0x00452bb0`..`0x00452c1d`), through the thing-per-cell lookup
  (`FUN_0053bf30`, then `FUN_00527e80`), falling back to the terrain height (`FUN_004527f0`). The lift is
  ceil10(trunc(v)), passed as lift + 1.5, and `FUN_0053ddd0`'s wave branch adds the terrain height again, which looks
  like counting raised ground twice: check it in the original under Proton first, the height predicted. Write the
  layout to FileFormats `hmp.md` (retitled: a height map with a shape-mark plane), `models.md`'s `0x80` row, and
  `park.md:142`; the reader's test runs over every shipped `.hmp`. Confirm: the squares over a built cell on raised
  ground beside the original's.

  - **Done 2026-10-01** on `alexah/229-hmp-lift` (FileFormats: its branch 229). `ItemHeightMapFile` reads the file;
    `ParkItemHeights` finds a cell's owner and reads its grid through the turn, counting `HMP_REBUILD` where the
    original would rebuild (no shipped file does); `ParkBuildMarkers.LiftOver` lifts by `ceil10(trunc(v))` and the
    corner adds the ground again, as `FUN_0053ddd0` does. Track-layer things (the park's hoarding, kinds 25/12) are
    not built here, so a square over one is counted (`MARKER_LIFT_OVER_TRACK_THING`) and stays on the ground; the
    walls the original drops from a lifted square are counted (`MARKER_LIFT_SIDE_FACES`). Docs: FileFormats `hmp.md`
    (retitled), `models.md` `0x80`; `park.md`, `park-engine.md` "Placement feedback". The tests read all 435 files
    (274 against their `.sam`) and the lift over the shipped park and a Bounce on the (76,64) hill; the turn, the
    base and the lift each put back turned them red.
  - **Confirmed in the game** (`q191run.py`, silent, `save/` unchanged): predicted then read `strip` lifts 20, 20, 10
    over the Bounce, 40 over the fountain's middle, 40 on the hill (ground 37), 0 on a path; shots `q191/after2/`
    looked at. **The original under Proton** (`q191/*.py`, face list and BlueprintMesh read live): (53,23) and (53,24)
    lift 20, as predicted and as ours; shot `q179b/orig/q191-12.png`, side by side in `q191/side-by-side.png`.
    **Not confirmed on screen:** the ground counted twice on raised ground, since the original's strip stops before a
    raised out-of-park cell and Lost Kingdom builds nothing on raised ground; it rests on the code and the two halves
    measured (`park-engine.md`).
- [x] **Q192. Write down the review's verified facts: rides, the script VM, ride sounds.** Found by the fork review
  (vm-2, vm-5, vm-14, vm-15, vm-v2..vm-v4, history-v3, rides-v3, fmt-media-v1, fmt-media-11, rides-v4, rides-v6,
  rides-8, rides-14, rides-15, gap5-17). No code. FileFormats `vm/instructions.md`: `DBGMSG` steps over its operand
  and does nothing (`0x00554243`); `ENABLELIGHT`, `DISABLELIGHT`, `SETLIGHT`, `COLOURLIGHT` take a light node id (mask
  `0x20000`; levels percent × 0.01, `0x00700fe0`; setters `0x004587e0`, `0x00458890`); `EVENT_EXT` is `EVENT` with
  `ADDOBJ_EXT`'s extra operand (`0x00552833`); `SPARK` stores two particle-node ids at `+0xdc`/`+0xde`, checks them and
  spawns nothing (`0x005564ed`); `TOUR 1` makes the tour record at walk node 99, `TOUR 2` destroys it (`FUN_0055d3d0`);
  `MONTH`'s INC at `0x00556622` (it answers 1-12). Add `0x20000` (light) to `models.md`'s masks. Ride sounds through
  EventMap slots (`FUN_0051eeb0` plays a slot's value in `cat_rides`; slots 0, 1, 3 and 4 are read, 10 is a sound
  parameter id, 2 never; two slot layouts; five shipped requests name ids their category lacks) into `audio.md` and
  `park.md`, its five callers named first. Track-ride and coaster keys (`SupplementalMeshes`, `asCarTypes`,
  `GTexture`, `coaster.sam`'s edge and select tables) into `park.md` and FileFormats `sam.md`. Open, and written as
  open: `FUN_00461f10` clears header bit `0x4` unless the load keeps it (the cars `FUN_00430130` loads).

  - **Done 2026-10-01** on `alexah/230-review-facts-rides-vm` (FileFormats: its branch 230). Every fact re-read in
    Ghidra or the data first (three read-only verifiers, `wf_638574f1-80e`). FileFormats `vm/instructions.md`
    (`DBGMSG`, the four lights, `EVENT_EXT`, `SPARK`, `TOUR` 1 and 2, `MONTH`), `models.md` (`0x20000`), `sam.md`
    (`coaster.sam`'s tables, `GTexture`, the jungle's `SupplementalMeshes`). `audio.md`, "What an EventMap's slots
    feed", with the five callers named: the coaster's trains (`FUN_004392a0`), `Bumper_Retarget`,
    `Bumper_PlayCarSound`, the go-karts' car removal (`FUN_0054ae50`) and the tour ride's new car (`FUN_0055a720`);
    `park.md` (the EventMap layouts, the five missing ids, `GTexture`, the lights, `SPARK`, `TOUR` 1/2, the car's
    mesh, bit `0x4` as open). Corrected on the way: `TOUR 1` passes the position ×300 and the facing in 4096ths;
    `FUN_005da3c0` is a bare `RET`, so no complaint is ever printed; `TrackMaster` is `FUN_0042fad0`'s, not
    `FUN_0042fd90`'s; the jungle has no `Balloon`; slot 10's level is constant only for the flying cars. Review
    `wf_43164183-37d` (7 findings, fixed; the `SPARK` reader scan re-run by hand). No code.
  - **Game** (`q192run.py`, silent, `save/` unchanged): park loads (shot `q192/run1/park.png` looked at); predicted
    `rides` 14 and read 15 (the Bus had arrived; I left it out); the `unimplemented` census names none of the eight
    opcodes, as predicted. The one ride-sound site OpenTPW reaches, the Hot Pot's lead car, is counted
    (`BUMPER_CAR_SOUND`) and unbuilt: Q202.
- [x] **Q202. The Hot Pot's lead car makes no sound.** Found by Q192 (`audio.md`, "What an EventMap's slots feed");
  queued by Alexah 2026-10-01. `ParkBumperCars.Retarget` counts `BUMPER_CAR_SOUND` where `Bumper_Retarget`'s bumper
  arm (`0x0054a366`) plays the ride's `EventMap.rse` slot 0 through `FUN_0051eeb0` into the car's held voice
  `+0x20`, in the park's `cat_rides`: for the jungle `bumper`, effect 194 (an engine), 0 skipped. Read first, in
  Ghidra: the exact gate on that play (the code's comment says the running lead car, active and not unloading), how
  the voice is held and looped, and the unload's fade. Then, each step, `Bumper_StepCar`'s Hot Pot arm moves a live
  voice to the car (`FUN_0051c270`) and sets sound parameter slot 10 (`VAR_PAR0`, 16) to the speed / 3 through
  `FUN_0051bc40`; `FUN_0054ae50` fades it as the car is taken off (only go-karts play slot 1 there). Read the slots
  from the script's `SPAWNSOUND` child by index, never by a hard-coded id. `Bumper_PlayCarSound` stays silent for
  the Hot Pot, as `Bump` says. Confirm: buy the Hot Pot, run a go, and show the voice starting, following the car,
  its parameter tracking the speed and fading at the end, in the log beside a screenshot; then the engine sample heard
  in the game's own mix, captured through the disk driver. Put the gate back and the new test must fail.
  - **Done 2026-10-01** on `alexah/232-hot-pot-car-sound` (FileFormats: its branch 232). Decoded first-hand
    (`audio.md`, "The bumper arm's engine" and "A voice's two controllers"; FileFormats `sound-categories.md`, the
    variation header's volume, pitch and two controllers, measured over all 1,595 variations): the start's gate
    (no voice, `0x404000`, running, not unloading), the step's move and parameter (16, slot 10, = speed / 3, every
    bumper type), the pitch (-24..36 96ths of an octave by the tables at `0x00782f40`, one step on), and the fades.
  - **Measured in the original first** (`q202/voicelog.py`, reference park patched and restored; shots
    `q179b/orig/q202-*`): **my static read said the engine was a one-shot per retarget; the memory said otherwise** -
    the lead's handle lived the whole go (557 ticks), went 8 ticks after the ride went back to loading, and a new
    one came with the next go; no other boat held one. That led to the unloading arm's fade (`0x0054788e`), which
    keeps the handle until the voice has gone.
  - **Built:** `ParkBumperCars.ISounds` (the car's `+0x20` as `Car.Voice`), `ParkCarSounds` (the `EventMap` read by
    slot through `RideScriptScheduler.SoundVariable`, `cat_rides` loaded by `ParkAudio`), `Voice.MoveTo` and
    `SetRate`, the reader's new header fields; the `bumpers` census shows each car's sound.
  - **Game** (`q202/run.py`, game's own mix by the disk driver, `save/` unchanged): run 1 caught a defect the
    tests missed - the step's per-tick volume set cancelled the fade, so the engine sounded on after the go (fixed,
    and a device-backed test added). Run 2, each predicted: L 4 boats loading, nothing held; G one start, the lead's,
    194 `Engine.mp2`; F 49 censuses in the go, param = speed / 3, pitch and rate as decoded, place × 0.0033036, one
    voice; E one fade, nothing held 2 s on, boats stay. Mix (`engine.py`, `scan.py`): the engine correlates 0.52-0.92
    (median 0.67) in every half second of the go at the census's pitch; the mix falls to silence 0.4 s after the
    go's last running census. Shots `q202/run2/G-go`, `F-go`, `E-after` looked at. Review `wf_c14d721e-f77` (one
    finding: key 0 was a literal 0, not the effect's `+0x12`; fixed, and the fade test now drives key 16). Run 3 on
    the final code: the same four matches, the engine 0.54-0.89 through the go, `save/` unchanged; shot looked at.
  - **Bug put back, tests red:** the start replaced by the old counted no-op (3 red), the lead check dropped (3
    red), the unloading fade dropped (1 red), the fade guard dropped (1 red).
  - **Worktree:** 1570 pass, 0 skip with the game; 644 ran, 926 skipped without; 123 warnings; opcodes 77.
  - **Not done:** the original's engine was not heard (its runs are silent); the volume's group scale
    (`FUN_006bb860`) and QMixer's volume unit are not read, so 68 is taken as 68 / 100.
- [x] **Q203. Magenta under the Hot Pot.** Seen since Q172 ("magenta pads under the bought Hot Pot's entrance") and
  in Q202's frames; fixed at Alexah's word 2026-10-01. Not fire, not the sign: `content/shaders/test.shader` bound 16
  textures and draws flat magenta for any other index, and the pot's floor `jbb_floor` names 25 materials, its faces
  on 16-24 (`m_grass3`, `pp_grsph`) between the logs. Over every readable `.md2` (2,117, 4,914 meshes) 49 meshes name
  more than 16, at most 31 (FileFormats `models.md`, "Materials"): Lost Kingdom's Inca God base, both coasters' and
  the mine cart's entrances and the 5x5 rocks among them. **Built:** `Material.TextureSlots` = 32, the shader's 32
  bindings, and the five fillers (`LobbyModel`, `UiMesh`, `ParkGround`, `ParkPaths`, `ParkBuildMarkers`) sized by
  it, rather than splitting such meshes into draws of 16. Thirty-three bindings is past Vulkan's guaranteed minimum
  of 16 sampled images a stage; this machine's RADV (RX 6700 XT) reports 8,388,606 and llvmpipe 1,000,000, and no
  other driver was measured.
  - **Done 2026-10-01** on `alexah/233-hot-pot-magenta` (FileFormats: its branch 233). Tests: every shipped mesh fits
    the slots (31 at most, 48 of the archives' 4,823 past 16, `wr_tunnel.md2` the one unread), and the shader binds
    and picks each slot. Bug put back: 16 slots (both red), the old shader (1 red). Worktree: 1572 pass, 0 skip
    with the game; 645 ran, 927 skipped without; 123 warnings; opcodes 77.
  - **Game** (`q202/magenta.py`, silent, `save/` unchanged), flat magenta (r, b over 200, g under 15) counted,
    predicted 0: the bought Hot Pot from two angles 0 and 0, where Q202's `run3/G-go.png` held 2,006; the lobby 0.
    Shots `q202/mag2/hotpot-tooloff.png`, `hotpot2.png` looked at: the floor shows between the logs. The red and grey
    squares on the water in `hotpot.png` are the queue tool's, gone with the tool put away.
  - **Not checked:** the original was not run for this; its frame `q179b/orig/go-1.png` shows no magenta there.
- [x] **Q193. Write down the review's verified facts: saves, particles, sprites, audio.** Found by the fork review
  (fmt-assets-v1, fmt-assets-v2, gap5-1..gap5-4, gap3-6, world-sim-2, economy-6, economy-11, level-build-v2,
  gap3-8, fmt-assets-1, peeps-1, gap5-6, gap5-8, fmt-media-v2, ui-render-platform-v1, ui-render-platform-v2). No code.
  FileFormats `saves.md`: the `PART` module ('LCTP', an enabled flag; the live system, `0x9e68` bytes: a `0x2c`
  header, 120 emitters of `0x140`, 20 effectors of `0x68`, `0x1c` of list heads; the templates, `0x8b60` bytes: 105
  effects of `0x140` and 20 effectors of `0x68`; a particle pool the saver always writes empty), the game's name for
  each module (the `SAD_` strings, paired by `FUN_00415270`'s compares), `GSYS` as nine dwords (`FUN_005506e0`; its
  sixth is 2 in both types) and `CHTS` (byte 0 is the cheats flag, `+0xa`; byte 1 goes to `+0xb`; Easymode ships 01 01). `docs/exe/saves.md`:
  `PAR_SaveStatus` `FUN_0051f680` and its reader `FUN_0051f7a0`, then `Particles_KillAllOnScreen` (`0x005200b0`).
  FileFormats `particles.md`: an `.emt` is one 320-byte effect record, and row `0xA2` takes 0 as 1000.
  `park-engine.md`: every `ACTION_*` id beside `ACTION_SET_MODE` (`FUN_004041d0`, strings
  `0x007472f0`..`0x007475b8`). FileFormats `sprites.md`: the engine's row decoder (`0x00564790` through vtable
  `0x00701168` slot `+8`; paths `0x005648c0`, `0x0056492c`) and our signed rule decoding all 75 packs exactly (10,223
  pictures, 500,222 rows). FileFormats `sounds.md`: NLayer 3.0.0 matches ffmpeg on all 3,659 decodable entries, and the
  80 it rejects are identical one-frame placeholders in global `speechHD.SDT`. Open, written as open: 336 entries
  decode past full scale; whether the original's voice decode saturates or wraps (the `0x006c82c0` family).

  - **Done 2026-10-01** on `alexah/234-review-facts-saves-assets` (FileFormats: its branch 234). Every fact re-read in
    Ghidra or across all the shipped data first (three read-only verifiers, `wf_0f99a8b7-33e`). FileFormats `saves.md`
    (the game's two names for every module; new `TRAP`, `SYSG`, `STHC` sections), `particles.md` (`.emt`, the 0xA2
    default), `sprites.md` (the engine's decoder, all 75 packs), `sounds.md` ("Decoding"); `docs/exe/saves.md`
    ("Loading the modules"), `park-engine.md` (the action ids), `audio.md` ("How a voice is decoded"). Corrected on
    the way: the `PART` magic is the game's `PTCL`, stored reversed like the tags; `GSYS`'s sixth dword is
    uninitialised stack, 2 only by accident (`0x0075DBC8` in Easymode); the `ACTION_*` strings run `0x00747254` to
    `0x007475a8`, with 80, 85 and 999 named and 69 recorded but unnamed; a placeholder's play length is 816 bytes,
    408 samples. Settled, not open: the original **clamps** each voice to 16 bits as it decodes (`0x006c9270`'s clamp,
    `0x006ca580`'s `PACKSSDW`); OpenTPW keeps NLayer's floats and clamps only the mix, said at `AudioClip.Samples`.
    Also said at its site: our sprite decoder reads a 0 code as one byte, the engine's as two (no shipped picture has
    one). Review `wf_d5b78449-ab4` (13 findings, fixed). Comments only in code; no new test, so no bug to put back.
  - **Game** (`q193run.py`, silent, `save/` unchanged): predicted `rides` 15 with 16 scripts and the same 13
    `unimplemented` keys as `q192/run1`; read 15, 16 and the same 13. Shot `q193/run1/park.png` looked at: the park
    and its peeps drawn.
- [x] **Q194. Write down the review's verified facts: tickets, challenges, advisor, terrain.** Found by the fork
  review (economy-3..economy-5, economy-8, economy-9, economy-v1, economy-v2, history-11, ghidra-docs-20,
  world-sim-v1, world-sim-v2, world-sim-v4, world-sim-1, world-sim-7, gap5-5, ui-render-platform-v3,
  ghidra-docs-11..ghidra-docs-13, gap5-7, gap5-9, gap5-11, fmt-media-8, gap5-14, history-1). No code. Golden
  tickets (`ride-operation.md`): six local (`FUN_004d4bc0`), four global (`FUN_004d4e50`) and one secret, each awarded
  alone by a strict '>', type 0 only; `AtLeastThisManyHappyPeople` gates on people in the park; the advisor ids. Challenges:
  `ChallengesInThisLevel` holds 1-based indexes into `Challenges[]`, at most 20 (`FUN_004d1ce0`); the offer order and
  its gates (`FUN_004d2a70`); pacing 540, 270 and 270 game days; the win test `FUN_004d1660`; the manager is thing 10,
  model 19, saved in the World module (`FUN_004d2320`); `Challenges.sam`'s own comments agree with the executable for
  Types 1-8, 12 and 13 (FileFormats `sam.md`). Advisor (`advisor-park.md`, for gap 4): `GeneralAdvisor.MinTimeAnyMessage`
  and `MinTimeSameMessage` have no reader, so the only pacing is a line's length + 1000 ms and each category's
  cooldown; `FUN_00429e90` plays advisor clip N. The terrain compositor as blocking unknown 6's entry point
  (`FUN_0055f780`, `FUN_0055e780`, `FUN_0056e7e0`, up to `FUN_004504c0`). The loose addresses to their pages: the
  gated FPS print `FUN_0046c0d0`, the DirectDraw callers, the sprite-under-cursor lookup `FUN_00532bd0`, the file probe
  `FUN_0044a220`, the font blitters. Measure the challenge's 937-byte record before writing any of its offsets.

  - **Done 2026-10-01** on `alexah/235-review-facts-tickets-challenges` (FileFormats: its branch 235). Every fact
    re-read in Ghidra or across all the shipped data first (four read-only Opus verifiers, `wf_cba77d07-b66`; the
    challenge manager's record measured in ten distinct park files). `ride-operation.md`: new "Golden tickets" (six
    Local, four Global, the Secret; ticket 3 and the Secret partly decoded; `MinCellsOwned` unread) and "Challenges"
    (pool, offer, pacing, win test). `saves.md`: an award's four results and the Global record that moves between
    parks. `advisor-park.md`: the two unread `GeneralAdvisor` keys (no global gap), the kinds table (0, 2 and 7 added,
    1 corrected), the ticket messages' lines. `ui.md`: `0x00790398` and `FUN_00429e90`. `park-engine.md`: unknown 6's
    entry point and `FUN_00532bd0`. `park.md`: the probe `FUN_0044a220`, `.LND`'s version check. `boot.md`: the
    DirectDraw callers, the other message loops, the FPS print (dead by CONTENT), the `wea*.dll` imports;
    `render-states.md` the software renderer; `lobby.md` the font object. FileFormats `sam.md` (the ticket thresholds
    per theme, `Challenges.*`, `ChallengesInThisLevel` as indexes and its four lists, the `Type` meanings),
    `saves.md` ("The challenge manager (model 19)"), `models.md` (flag `0x20` in 880 of 2,129, re-counted),
    `fonts.md`. Corrected on the way: `ChallengesInThisLevel[7]` = 9 is `Challenges[9]`, Type 20, not Type 9
    (`ride-operation.md` said otherwise); with `DAT_007a1a8c & 0x2000` off the `base.lnd` call is skipped silently, the
    "not allowed" log is another flag's (`park-engine.md` said otherwise); `FUN_00429e90` plays only a line's first
    clip; #27's `TargetObj2` is ignored. Review `wf_83f27a06-521` (9 findings, fixed). Docs only; no test, so no bug
    to put back.
  - **Game** (`q194run.py`, silent, `save/` unchanged): predicted `rides` 15 with 16 scripts and the same 13
    `unimplemented` keys as `q193/run1`; read 15, 16 and the same 13. Shot `q194/run1/park.png` looked at: the park,
    its rides and peeps drawn, 0 tickets and 0 keys on the HUD.
  - **Not checked:** the readers of `DaysAfterCompletedChallenge`/`DaysAfterDeclinedChallenge`, the follow-up lookup,
    which `GoldTicketNearTo*` row reads which threshold, every metric helper; the Ghidra server dropped mid-run.
- [x] **Q195. Write down the movie player, and the emulator as an instrument.** Found by the fork review (gap4-1..
  gap4-14, fmt-media-4..fmt-media-7, ghidra-docs-v3, history-4). The movies stay cut (section G) and
  `INTRO_MOVIE_BULLFROG` and `INTRO_MOVIE_PARK` stay counted; this writes down what the executable does, for when they
  are taken up. `boot.md` beside `Intro_PlayBullfrogMovie`: the chain (`FUN_0051b010`, the chunk reader
  `FUN_0066e410`, `FUN_00670c20` case 4 for pIQT), the header `FUN_00670890`, the dequant `FUN_006710c0`, the
  macroblock decoder `FUN_006747d0`, the IDCT `FUN_0067673c`/`FUN_00676965`, the colour tables `FUN_00670350`
  (undecoded), the audio chunks `FUN_0066ed60`/`FUN_0066e9a0` and stereo EA-ADPCM `FUN_00672210`. FileFormats
  `video.md`: the byte layout and the census of all nine movies (all pIQT; drop "rumored UV2f"). `docs/TOOLING.md`:
  one short section on running the executable's own leaf routines under unicorn as a bit-exact oracle, which ran the
  dequant, the macroblock decoder and the ADPCM over all 9,412 frames, and its caveats (x87 precision, register
  conventions, tables built at run time). Never port the fork's `TqiDecoder` (tuned, and derived from jsmpeg).

  - **Done 2026-10-01** on `alexah/236-movie-player` (FileFormats: its branch 236). Solo, no workflow: the week's usage
    was at 97% (rule 18). Every function named above re-read in Ghidra first; the TQI DC tables checked entry by entry
    against MPEG-1's; the colour stage's six constants read (BT.601's, to four places; how they combine is still
    undecoded). `boot.md`: new "The movie player" (the start call and its volume, the chunk reader's FourCC table, the
    TQI frame, dequant formula, macroblock, IDCT and colour stage, the audio header and chunk decoders, what is not
    established) and thirteen address rows. FileFormats `video.md` rewritten (`*.tgq`; the chunk census of all nine,
    the `pIQT` header, the bitstream, `SCHl` tags, the `SCDl` layout; "rumored UV2f" dropped: the game reads it as
    `pIQT`). `TOOLING.md`: "The executable's own routines under unicorn". `tgqscan.py` predicted and read every count
    in the table (9,412 frames, all 320 x 352, quant 99, bytes 5-7 = 20/22/3, `SCDl` sums = tag `0x85`, every chunk
    the predicted size). Re-ran the instrument: the emulated table equals the Ghidra formula, and `FUN_00672210` equals
    ffmpeg in all nine; `fpcw.py` reproduced the precision warning (24-bit changes 37.5% of `bf.tgq` frame 120's
    samples, by up to 46). `DECISIONS.md` already called `FUN_00672e60` the type-3 decoder (gap4-11), so unchanged.
    Docs only; no test, so no bug to put back.
  - **Game** (`q195run.py`, silent, `save/` unchanged): predicted `rides` 15 with 16 scripts, the same 13
    `unimplemented` keys as `q194/run1`, `INTRO_MOVIE_BULLFROG` and `INTRO_MOVIE_PARK` 1 each; read 15, 16, the same
    13, 1 and 1. Shot `q195/run1/park.png` looked at: the jungle park, its rides, paths and peeps drawn, the HUD up.
  - **Not checked:** the TQI AC tables entry by entry (ffmpeg staying in step is the evidence), the colour tables'
    arithmetic, `FUN_00672cb3` (codec 10), which x87 precision the original decodes under, and the video emulation
    over all 9,412 frames (re-run here on three frames; the full run is the review's).
- [x] **Q196. `tpw-setup.sh` opens a raw disc image.** Found by the fork review (ui-render-platform-13). Its file
  branch (`7z x`, then `bsdtar`) cannot open a CloneCD `.img` or a single-track BIN/CUE `.bin`: raw 2352-byte
  sectors, which both tools refuse. Aluzed's `tools/ccd-img-to-iso.py` (OpenTPW-decomp commit `907d58f`, MIT) turned
  a raw Mode 1 image made from our ISO back into a byte-identical ISO. In the `-f $DISC` branch, detect the 12-byte
  sync (00, ten FF, 00) at offset 0 and copy bytes 16-2063 of every 2352-byte sector into a temporary `.iso` before
  `7z`; refuse Mode 0 and audio sectors, as the script does. Credit it with a header line and an `Adapted-from:`
  trailer (`docs/WORKFLOW.md`, "Commits"). Add the line to `tools/play-the-original/README.md`, "What you need".
  Confirm: the raw image made from `content/SimThemePark.iso` installs as the ISO does; claim real dumps only after
  one is tried.
  - **Done 2026-10-01** on `alexah/238-raw-disc-image`. The `-f` branch tests the first 12 bytes for the sync
    (`is_raw_image`) and `raw_to_iso` (python3, already required) copies bytes 16-2063 of each sector into
    `$DEST/.disc-image.iso`, removed after unpacking. It refuses a cut-short image, any sector without the sync (audio)
    and any not Mode 1, each with its own message. The fork's Mode 2 Form 1 arm is left out: the disc is Mode 1 and no
    Mode 2 image could be tried. README "What you need" names `.img`/`.bin` and says no real dump has been tried.
  - **Measured** (`~/.cache/tpw-q196/`, `mkraw.py`: sync, MSF header, mode 1, EDC/ECC zeroed; 254,110 sectors):
    predicted the converted `.iso` byte-identical to `SimThemePark.iso`: md5 `7b88dd39...` both. Installed the ISO
    and the raw image with `--no-menu` (host 7z): 2,487 game files, every md5 equal; only `play.sh` differs, by its
    folder name; the temporary `.iso` gone. Refusals read on a mode 0, an audio and a cut-short image.
  - **Bug back** (detection off): the raw install stops, 7z "Cannot open the file as archive", line 163.
  - **Game:** the original from the raw install (`original.sh` with `TPW_ORIGINAL`, offscreen, silent): player screen
    at 30.16 fps in the Wine log; shot `q196/orig-raw.png` looked at. OpenTPW `--game` that install (`q196run.py`):
    predicted `rides` 15 with 16 scripts and q194's 13 keys; read 15, 16, and 14 keys, `STAFF_NO_LINKS_WANDER` once
    more (its `save/` started empty, so the loading bar was untrained and the run's timing differed; the files are
    identical). Shot `q196/run1/park.png` looked at: the jungle park, rides, paths, peeps and HUD.
  - **Not checked:** a real CloneCD or BIN/CUE dump, a multi-track `.bin`, bsdtar's arm (the host has 7z), the
    no-7z-no-bsdtar refusal.
- [x] **Q106. `APathCellCostsWhatTheBalanceFileSays` fails when its class runs alone.** Found by Q50h, on `main` as well.
  `ParkPathBuildingTests`' `[TestInitialize]` keeps `GameData.Required()` in a field and never mounts it as the global
  `FileSystem`, so run by itself (`--filter FullyQualifiedName~ParkPathBuildingTests`) `levels/Standard.sam` does not
  load and the price answers -1; the whole suite passes only because an earlier class mounted it. Mount it as
  `ParkQueueRemeasureTests` does, and sweep the other test classes for the same order dependence. Confirm: each class
  alone, green. No game run.
  One more is `ParkPeopleTests.AnArrivalJoinsEveryListThatHasToKnowAboutIt` (the 2026-09-26 staleness audit): it never
  deletes its `ParkPeople`, which stays in `Entity.All` and `ParkPeople.Current` for every class after it. Clean up
  with `Delete()` and `Entity.ApplyDeletions()`, as `ParkQueueTurnTests` does.
  - **Done 2026-10-01** on `alexah/239-tests-pass-alone`. Each of the 185 class names run alone with the game
    (`--filter FullyQualifiedName~OpenTPW.Tests.<class>.`; `Probe` is a nested helper with no tests): four failed, not
    the one named. `ParkPathBuildingTests` (unmounted, -1), and `ParkBankTests` (7), `ParkSettleUpCountsTests` (1) and
    `PeepPaceTests` (3), which log and only `GameData.Required()` made the global `Log`. New `TestRun.cs`: an
    `[AssemblyInitialize]` makes the log, as the game does first; `DeleteEvery<T>()` ends what a test made; and an
    `[AssemblyCleanup]` fails the run if any `ParkPeople` is left in `Entity.All`. MSTest 2.2.7 reports a failed
    assembly cleanup and still says "Passed!" (exit 0), so it calls `Environment.FailFast`, which aborts `dotnet test`
    (exit 1) with the reason quoted. That check found twelve classes leaving one, not the one named: `ParkPeopleTests`,
    `ParkBankTests`, `ParkBalloonTests`, `ParkCostumeTests`, `ParkGuestTypeTests`, `ParkStateTests`,
    `ParkHappinessGaugeTests`, `ParkStaffPlacementTests`, `ParkStaffDrawingTests` and `ParkTickTests` now delete in a
    `[TestCleanup]`; `ParkHandTests` and `ParkLeaveTests` add theirs to the `made` list they already clear.
  - **Measured:** after, 184 of 184 classes pass alone, none leaving a `ParkPeople` (predicted 184). The suite 1572 pass
    0 skip with the game, 645 ran 927 skipped without; 123 warnings. **Bugs put back:** the log's initialize emptied,
    `ParkBankTests` alone 29 fail, `PeepPaceTests` 6; `ParkPathBuildingTests` unmounted, 1 fail; `ParkPeopleTests`'
    cleanup emptied, the run aborts, exit 1, "2 ParkPeople left" (the first try, an `Assert` in the cleanup, stayed
    green and was replaced).
  - **Game** (`q106run.py`, `q106/run1`; nothing in the game changed): jungle loads, `rides` 15 with 16 scripts as
    predicted; 13 unimplemented keys where 14 was predicted, `STAFF_NO_LINKS_WANDER` not reached this run. Shot
    `q106/run1/park.png` looked at: the park, rides, paths, peeps and HUD. `save/` unchanged.
  - **Not checked:** an order dependence through any other static (`ParkState.Current`, `Level.Current`, the options)
    that running each class alone would not show; MSTest 2 has no random order.
- [x] **Q118. The camcorder key acts on its press, both ways.** Found by Q57. The original's C is shortcuts row 16
  (`0x0040c5c0`), run on the key's release as every row is (`FUN_0040c990`), and first person is left on a key-up whose
  action is camcorder (`FUN_00488a00`, the same exit as Escape's; `scenes.md`, "The park Escape route").
  `ParkOrbitCameraMode.Update` enters and `ParkCamcorderCameraMode.Update` leaves on `Input.Pressed( InputButton.CamcorderMode )`,
  and neither says so at the site. Confirm: hold C in orbit, then in first person - nothing until each release; `state`'s
  camera height and a screenshot.
  - **Done 2026-10-01** on `alexah/240-camcorder-key-on-the-release`. Read again in Ghidra: `FUN_00488a00`'s key-down
    (`0x1000a`) only latches (`FUN_0040c900`); its key-up (`0x1000b`) leaves first person when the key is `0x1b` or
    `FUN_0040c870( key, modifiers )` finds action 16. New `Input.KeyUp`: a key of the binding let go this frame with
    exactly its modifiers held, so a modifier pressed under the held key is no release (`Input.Released` would fire on
    it). Both camera modes read it, each citing its address.
  - **Tests.** `ParkCamcorderKeyOnReleaseTests`, 2: real key events through `Input.UpdateFrom` and `Camera.Update` over
    the real modes. Four mutations, each predicted and each as predicted: either site back on `Pressed` turns both red;
    `KeyUp` as the release edge, or without its modifier test, turns the Ctrl one red.
  - **Confirmed in the game** (`q118confirm.py`, silent, jungle, 1280x720, C held 2.4 s through XTEST; `main` at
    `86a29ce` as the control, `q118/control`, and the fix, `q118/fix`). `state`'s cam z: orbit 49. Control: mid-hold
    already 5 (first person), the release changed nothing; held in first person, already back to 49. Fix: mid-hold 49,
    after the release 5; held in first person 5, after the release 49 - all four as predicted. Shots looked at: the
    gadget while held in orbit, the viewfinder frame after the release and while held down, the gadget again after.
    save/ unchanged. Ctrl+C was not pressed in the game: it is Close Park; the test covers it.
- [x] **Q116. In first person a left press still reaches the park.** Found by Q56's review. Entering first person hides
  layer 0 (`FUN_004a2ac0( 0 )`, `park-engine.md`, "Whose a right press is"), so no press reaches `Park_MouseMessageProc`;
  layer 1's `FUN_00488a00` hands a press to the camera table alone. Here `Level.WorldClick` runs in first person: a left
  click on a path arms the path tool, one on a ride opens its window. Confirm: in first person, a left click
  on a path, `tool` still None; a screenshot.
  - **Done 2026-10-01** on `alexah/241-first-person-left-press`. Read again in Ghidra: `FUN_00488a00` sends a press
    to the key table and `FUN_0042a760`, whose left press only sets `DAT_00790aac` bit 1; nothing picks the park. New
    `Level.LeftPressTaken` (first person, or a window took it) guards `WorldClick` and the console's `click`, which now
    answers "first person took". Test `InFirstPersonALeftPressOnAPathArmsNothing` (the orbit press arms the path tool,
    the first-person one nothing); first person put back out of the predicate turns it red. Game `q116confirm.py`:
    control (main) a real left click on path (47,24) in first person armed the path tool, its square drawn
    (`q116/control/B1`); fix: tool 0 after `click` and a real click, hand empty, windows gadget and viewfinder only,
    the orbit click still arms it (`q116/fix/A`, `B1`, looked at); save/ unchanged. Left alone: first person still
    shows help row 442, "Left-click to extend this path".
- [x] **Q87. `AdmitPerson` refuses where the original does not.** Found by Q50's decode. The original only logs a
  wrong person (`0x004e092c`..`0x004e0982`) and lets go of the nominee before it tests `VAR_LETMEON`
  (`0x004e09b0`); `ParkRideOperation.AdmitPerson` refuses the first and keeps the nominee on the second. Build the
  original's order. Confirm: `rides` and `peeps` through one admission.
  **Done 2026-10-01, `alexah/242-admit-person-order`.** Re-read `FUN_004e0900` in Ghidra: the wrong person is logged
  (`"admitting wrong person - check d..."`) and admitted, `+0x6c` is zeroed at `0x004e09b0` whoever asked, and only
  then a full `VAR_LETMEON` refuses with `"cannot admit person %d, script changed its mind about admission!"`.
  `AdmitPerson` now does exactly that, with both lines logged. Tests: `TheWrongPersonIsOnlyLoggedAndAdmitted`, and
  the full-slot test now asserts the nominee is let go. Bug back: refusing the wrong person reds both; asking the slot
  before letting go reds the full-slot test. Game `q87run.py` → `q87/run1` (predicted, then read): `rides` 15 with 16
  scripts, 2 "been AdmitPerson'd", 0 wrong person, 0 cannot admit, `peeps` 13 with one Riding, 14 unimplemented
  keys; shot looked at; save/ unchanged. Neither changed arm is reached by the shipped park (Invite calls forward
  only into an empty slot), so on screen this is "admissions still work", not the new arms firing.
- [x] **Q96. A charge is never deposited in the park's bank.** Done 2026-09-30 with Q177c (its account), on its branch
  `alexah/194-cost-of-goods-and-deposit`. `ParkState.TakeAt` deposits the price through `ParkState.Deposit`
  (`FUN_004d0190`, `0x004e16c6`), which moves `Balance` and `ProfitThisYear`, writes no `LastBalance` and has no gate;
  the analyser's month cash in is counted (`BANK_ANALYSER_MONEY_IN`). Gone: `CHARGE_BANK_DEPOSIT`. The test is now
  `ParkRideExitTests.PayingForARideBanksThePrice`. Confirmed by Q177c's runs: the log's "Bank: deposit 30" then
  "Bank: withdrawal 20" at each drink and the HUD's +10, since the two land together; a lost Jungle Spray play's +20.

  The item as written: Found by Q50c's review. `FUN_004e16b0` first calls
  `FUN_004d0190( price )` on the bank thing (`0x004e16bf`..`0x004e16c6`): the balance `+0xc`, the world's `+0x1fc90`
  and the bank's `+0x124`, the adds the gate fee's `FUN_004d0600` makes (`ride-operation.md`, "Spending").
  `ParkState.TakeAt` credits the object alone and counts the rest (`CHARGE_BANK_DEPOSIT`).
  `ParkRideExitTests.PayingForARideLeavesTheParksBalanceAlone` pins the gap and turns round with it.
  Decide what `+0x1fc90` and `+0x124` are before keeping either. Confirm: a drink sold at the Drinks Shop, the HUD's
  money before and after (+30), and `money`.
  From Q177: its mirror, the cost of goods' withdrawal, is Q177c; the two should land together. `+0x1fc90` is the park
  analyser's month cash in and `+0x124` the bank's `mProfitThisYear`, zeroed each year and not on entering a park
  (measured). A deposit writes no `mLastBalance`, and a park in the red for six thirty-day spans at a month's check
  ends (`FUN_004d0370`, message `0x13`; `ride-operation.md`, "The cost of goods and the park's money").
- [x] **Q197. A Full Simulation player's fresh park.** Found by the fork review (gap3-2..gap3-11, refute rank 2);
  queued by Alexah 2026-09-30, beyond the Easymode scope. The original builds it with no file: `FUN_00407d80`
  allocates the world and `FUN_00515540` builds the staff pool, the calendar, the arrival block, the 16,384 map cells
  and the other tables, the park closed. The level load `FUN_00407e00` then reads the catalogue (`FUN_00413140`, which
  also puts the items' `.emt` into free particle slots) and `FUN_005156a0` lays the balance stack without `Easy_` and
  makes exactly twelve things: the ten managers, the gates (11) and the traffic lights (12), both unplaced; then the
  sky, `Scape.omp` and the lighting. Fee from `InitialAdmissionFee` (20), cash from `InitialCash`, each loan's
  repayment = amount × (1 + APR/100)^(period/24) / period (`FUN_004cf7c0`, constants `0x00700378`..`0x00700390`).
  Keep ids 3 and 10 for the advisor thing and the challenge manager, which a load reaches by the ids the new world
  gave them. No `.hmp` needs generating. Write the chain into `boot.md`'s step 9 rows and beside `park-engine.md`'s
  allocation paragraph first, then build from the empty-park path, after Q186. Confirm against jungle `restart.INTS`
  (Q167's note): the twelve things, the fee, the APRs and the repayments; then a screenshot of the empty park.
- [x] **Q85. A guest who arrives starts with happiness nought, and stays there. Decode first.** Found by Q50's game
  runs: every one of the 33 guests who arrived (30 by `load 30`) read `happy 0` in `peeps`, none above it in nine minutes,
  while the save's 13 kept theirs (most at 50) until they went home, all by about four minutes, so a dock on anyone left
  clamps and shows nothing. `ParkPeople`'s new-guest record writes `Happiness: 0f` (and nought thirst, hunger,
  toilet, vomit, litter) with no note. Decode what the guest constructor `FUN_004faec0` and the arrival give a new
  guest, and whether a ride's settle-up should raise it, then build it. Confirm: `load 30`, `peeps` over a few
  minutes. **Then build the `InQueue` turn's unhappy arm** (`QUEUE_TURN_UNHAPPY` in `PeepBehaviour.QueueTurn`): below
  happiness 10, thought `0xb`, out. Alexah held it at Q50d (2026-09-24) until arrivals start at the original's 50
  (`FUN_004faec0`, `0x004fb075`), since at nought it would put every arrival out of every queue it joins;
  `ParkQueueTurnTests.AnUnhappyQueuerStaysUntilArrivalsHaveTheOriginalsHappiness` pins the hold and turns round with it.
  From Q165b: the constructor's eight unconditional draws on the world generator are the exit level's variation
  (`0x004faff8`), the kind (`0x004fb01f`, built), the cash's variation (`0x004fb046`), thirst and hunger `% 50` (`+0x1a4`,
  `+0x1a8`), toilet `% 30` (`+0x1ac`), one discarded (`0x004fb109`), and `+0x1c0` set to 100 when `% 100` is under
  `PrankeryLikelihood` (`0x004fb114`); two more follow when `FUN_004fa990` answers nought (`0x004fb201`, `0x004fb21c`).
  From Q169: the excitement match reads the arrival's hunger, so an OpenTPW arrival whose id does not divide by four
  (the three quarters whose hunger never drifts) takes 35 on every Totem ride until they eat, where the original's,
  drawn `% 50`, take 35, 28, 21 or 14.
  **Outcome, 2026-10-03:** Done 2026-10-03,
  `alexah/255-q85-arrival-decode`. Decode only; the build and game confirmation are Q85b.
  `docs/exe/guest-arrivals.md`: constructor happiness **50**, thirst/hunger **0..49**, toilet **0..29**,
  cash/exit/prankery formulas, eight direct pre-reseed draws plus the base speed and child-bank draw,
  the ID reseed, both arrival callers, existing ride happiness gains, and the below-10 queue arm.
  Fresh headless Ghidra instructions against the hash-matched reference; no runtime confirmation claimed.
- [x] **Q85b. Build the decoded new-guest values, then the unhappy queue arm.** Q85's decode is
  `docs/exe/guest-arrivals.md`. Implement the constructor's initial happiness and needs, cash/exit variation
  and prankery, retaining the decoded draw order and stating any remaining generator/arrival-path deviation.
  `ParkPeople.Admit` still starts happiness/needs at zero, fixes cash/exit level and drops prankery.
  The ride excitement gain already exists; verify it through a real arrival rather than rebuilding it.
  Then replace `QUEUE_TURN_UNHAPPY` in `PeepBehaviour.QueueTurn`: below happiness 10 (the truncated low byte),
  after the original's earlier guards and 30-tick window, thought `0xb`, out by the common leave path.
  Turn around `ParkQueueTurnTests.AnUnhappyQueuerStaysUntilArrivalsHaveTheOriginalsHappiness`.
  Confirm: `load 30`, `peeps` over a few minutes, initial happiness **50**, later gains distinguished from
  departures; a screenshot AND the corresponding census/log. Predict each measured count before reading it.
  Cover the real constructor and queue boundaries; restore the zero initialization and held queue arm,
  re-run the new tests and strengthen any that stay green. Build/test the exact commit alone in a worktree.
  - **Done 2026-10-03, `alexah/256-q85b-guest-arrivals`.** Constructor values and direct draw order built;
    prankery retained; unhappy queue exit uses the shared leave path, thought `0xb` counted. Predicted
    **30** new guests at happiness **50**, all matched; screenshots and censuses across four simulated minutes.
    Real arrival 66's ride gain **+5**, 50 to 55; natural unhappy exit 60 and instrumented 75's **9 to 0**,
    thought census **1 to 2**. Both restored defects fail the new tests; full suite **1641**, no failures/skips.
    Remaining generator/entrance/thought/tiredness limits and evidence in
    `docs/exe/guest-arrivals.md`. Q86 not started.
- [x] **Q206. Staff wander down ride queues and outside the park.** Alexah's report, investigated ahead of Q86.
  `alexah/257-staff-wander-boundaries`: restore linked-cell destination filters and path-only patrol rolls;
  explicitly apply containment during free-wander movement after destination-only filtering failed at the gate.
  Predicted zero queue/approach excursions: zero across 3,840 staff census rows in two 120-second game runs,
  screenshots inspected. Restored bug fails nine tests; containment and patrol type mutations fail three each.
  1,654 tests pass with game data, none skipped. Evidence and limitations: `docs/exe/staff-wandering.md`.
- [x] **Q86. Clearing a path joined to an entrance puts its whole queue out.** Found by Q50's decode. `ClearCell`'s
  path arm re-walks the entrance owner's queue (`0x0053694b`) after unlinking both sides, so the queue measures 0 and
  all but the nominee and state 14 go. `ParkPathBuilding.ClearPathCell` re-walks nothing. First check it is reachable
  in Lost Kingdom, where every path before an entrance is NOMODIFY, and from the queue stamp's forced clear
  (`0x00534741`).
  Completed on `alexah/258-q86-path-clear-queue`; decode, runtime evidence and reachability limits in
  `docs/exe/ride-operation.md`, "Q86: clearing a path joined to an entrance". Normal placement reaches an empty
  queue; populated-queue release confirmed with explicit instrumentation.
- [x] **Q89. The park's door does not command the gate. Decode first.** Found by Q50b's decode (`ride-operation.md`,
  "The closed ride"; `lobby.md`, "The park gate"). Opening the park writes the gate's `VAR_COMMAND` 1; closing writes 0,
  and only when `FUN_004c9130` counts nobody in the park and `VAR_STATUS` reads 1; 2 is the end-of-park routine's
  alone (`FUN_005168f0`). Counted `PARK_DOOR_COMMANDS_THE_GATE`. Decode what `Gates.RSE` does with 0 against 2 and which
  cells `FUN_004c9130` counts, then build it; `ParkRides.CommandTheGate` writes 2 for a park saved closed and
  `ParkFixedItems` and its tests call that 2 a stand-in for the door's close. Confirm: close an empty park at the door, photograph the gate.
  **Outcome, 2026-10-03:** Done 2026-10-03,
  `alexah/259-q89-gate-decode`. Decode only; implementation and game confirmation are Q89b.
  `docs/exe/park-gate.md`: command **0** ordinary close, **2** end sequence ending in a permanent
  yield loop; guest kind 1 on cell types **0, 1, 3, 9, 10**. The delayed writer checks every **30**
  world sweeps and also waits for state-byte-0 staff outside those types; the immediate close does not.
  Hash-matched private Ghidra; **4/4** gate scripts freshly extracted, **308/308** corpus scripts parsed.
  No runtime confirmation claimed.
- [x] **Q89b. Build the decoded park door and deferred gate close.** Q89's contract is
  `docs/exe/park-gate.md`, with script content in FileFormats `vm/park-gates.md`. Opening commands **1**;
  closing commands **0** only with the position-cell guest census empty and gate status **1**.
  Build the **30-world-sweep** retry and its extra state-byte-0 staff-outside guard, distinct from
  the immediate door close. Resolve script variables by name. Replace `ParkRides.CommandTheGate`'s
  saved-closed command **2** stand-in without clobbering valid resumed script state; check fresh closed
  and saved closed gates separately. Correct its and `ParkFixedItemsTests`' stale idle/writer comments.
  Retire `PARK_DOOR_COMMANDS_THE_GATE` only where built. Confirm: open then close an empty park at
  the entry-price door, photograph the gate AND log the command/status/census, predicted first; reopen
  to prove normal dispatch remains live. Cover populated closure, the last guest leaving, and the
  delayed staff guard. Add regression tests, restore each bug and prove the new tests fail. Build/test
  the exact commit alone in a throwaway worktree. Do not implement Q90's advisor messages here.
  Confirmed Q89b on `alexah/260-q89b-park-gate`: door 1/1 → 0/0 → 1/1 command/status,
  populated close waits for the last guest (retry tick 60); staff outside block ticks 780/810/840,
  inside closes at 870, then reopens. Screenshots + predicted census: `docs/exe/park-gate.md`.
  Twelve regressions, eleven restored defects fail (occupancy fixture repaired); 1679 tests pass.
  Saved-closed follow-up `alexah/261-q89b-saved-gate-proof`: unchanged original restart loads 0/0,
  opens 1/1, closes 0/0 and reopens 1/1, with screenshots and predicted census. A labelled
  one-field stock fixture preserves saved 1/1 while closed. Evidence: `docs/exe/park-gate.md`;
  original saves unchanged, no code change, Q90 unchanged.
- [x] **Q90. The advisor says nothing when the park opens or closes. Decode first.** Found by Q50b's decode. The door
  posts a type-`0x13` message, 3 or 4, whether or not anything changed, and `CAdvisor::ReceiveMessage`
  (`FUN_0059b060`) answers with its own message `0x80` or `0x81` (`FUN_0059ae20`); `advisor-park.md` lists neither.
  Counted `PARK_OPENED_ADVISOR_MESSAGE`, `PARK_CLOSED_ADVISOR_MESSAGE`. Decode what the two say and when, then build.
  Confirm: press the door, the advisor's line in the log and on screen.
  **Outcome, 2026-10-03:** Decoded 2026-10-03:
  events 3/4 → messages `0x80`/`0x81` → responses 308–311 → samples 342–345. Both shipped scores are **20**,
  below the tick's strict **>25** threshold: stock silence is expected, though OpenTPW still lacks the posting/gate
  path. Low-score slots remain occupied in the tick. Fresh Ghidra initializer/schema reconstruction and independent
  review; `docs/exe/advisor-park.md`, Q90. No implementation or runtime confirmation this session; Q90b follows.
- [x] **Q90b. Build the decoded park-door advisor message path, preserving stock silence.** Q90 decoded
  `docs/exe/advisor-park.md`: door events 3/4 always post messages `0x80`/`0x81`, including unchanged states.
  Derive scores and thresholds from data; one pending instance per message, category-0 cooldown, priority queue,
  and two-line histories. Remove `PARK_OPENED_ADVISOR_MESSAGE` / `PARK_CLOSED_ADVISOR_MESSAGE` only when built.
  Confirm: predict **0** spoken door announcements at stock score 20 / threshold 25, then press the real door and
  capture a screenshot AND message/score/threshold log. In a labelled private configuration fixture with only
  both scores raised to 26 before posting, predict and confirm the opening/closing response/sample sequence (342/343, 344/345)
  on screen and in the log. Stock silence is fidelity, not a reason to force speech. Add regression for routing,
  repeated states, duplicate cap, strict 25 boundary, cooldown, priority and rotation; restore defects and require
  the new tests to fail. Build/test the exact commit alone in a throwaway worktree.
  **Confirmed (2026-10-03):** real door screenshots plus logs: stock **0** attempts / **2** pending; private score-26
  response/sample sequence **308/342, 310/344, 309/343, 311/345**. All **14** restored defects fail; **1699** tests pass,
  no skips. Independent applied review passed after timing corrections. `docs/exe/advisor-park.md`; branch
  `alexah/263-q90b-advisor-door`. Saved histories remain counted; original data and saves unchanged.
- [x] **Q91. A ride's model does not change as it closes and opens. Decode first.** Found by Q50b's decode. Every
  close calls `FUN_00454550( model, 1 )` and every open `FUN_004547c0( model )` (`ride-operation.md`, the
  `FUN_00454550` row); the model loader around them names `Hoardings`, and nothing parses or reads `RideInfo.Hoarding`.
  Counted `CLOSED_RIDE_MODEL_CHANGE`, `OPENED_RIDE_MODEL_CHANGE`. Decode what the four texture offsets and `+0xbc` draw,
  then build. Confirm: photograph the Belly Bounce before and after the door.
  **Outcome, 2026-10-03:** Decoded only on
  `alexah/264-q91-closed-ride-decode`: separate generated hoardings, four texture frames (byte offsets
  0/8/16/24), progress rates +0.2/−0.3 per engine-clock second, staggered vertex/UV animation and guarded
  warning selection. `docs/exe/ride-hoardings.md`. No runtime screenshot/census, implementation or mutation
  claimed; Q91b carries the build and confirmation.
- [x] **Q91b. Build the closed/open ride hoardings.** Q91's decode is in `docs/exe/ride-hoardings.md`.
  Parse `Info.Hoarding`, build the original outline's generated panels with its terrain/corner fitting,
  four MiscMesh textures, staggered height/UV animation, warning-selection guards and per-instance lifecycle.
  Follow the original block parser and complete corner algorithm when porting; no bounding-box substitute.
  Restore saved progress/flags. Use `Time.Delta` for movement; keep engine-clock pause/rate behavior explicit.
  Remove `CLOSED_RIDE_MODEL_CHANGE` and `OPENED_RIDE_MODEL_CHANGE` only when built. Confirm: photograph
  Belly Bounce before and after the park door closes and after reopening, paired with a predicted census/log
  of panel count, texture and progress. Add regression tests; restore the bug and prove the new tests fail.
  Done 2026-10-03 on `alexah/265-q91b-ride-hoardings`: twelve panels, Closed, progress 0/0.2/1/0
  photographed and predicted. Twelve tests, ten restored bugs fail; full suite 1711 passes.
  Details, deviations and screen-proof limits: `docs/exe/ride-hoardings.md`.
- [x] **Q92. The ride window's door is not built.** Found by Q50b's decode. `FUN_004af600` case `0x3e38` →
  `FUN_0048ccf0` closes with `FUN_004df300` or opens with `FUN_004df390`, unguarded; `FUN_004ad4e0` sets the switch
  from `mCanLoad`, greys it for a closed ride the guard refuses, and the window's box (`0x3e25`) shows status code 1,
  `CLOSED` (UITEXT 365), or `0x17`, `CLOSED: QUEUE NOT CONNECTED` (`FUN_00485f60`). `ParkRideOperation.Close` and
  `Open` exist now, and the switch already follows `mCanLoad`. Counted `RIDE_WINDOW_OPEN_OR_CLOSE_THE_RIDE`,
  `RIDE_WINDOW_CLOSED_STATUS`, `RIDE_WINDOW_DOOR_GREYED`, and the all-items row colour `ALL_ITEMS_CLOSED_ROW_COLOUR`.
  Confirm: close the Belly Bounce from its window, `objects` and `peeps`, photographed.
  **Done 2026-10-03**, `alexah/266-q92-ride-window-door`: predicted `canload` 1→0→1 and 13 peeps
  match real-pointer screenshots/logs. Seven regressions; six logical mutations and one render mutation fail.
  Ordinary closed status/colour built; guard/disconnected wording tested. `docs/exe/ride-window-door.md`.
- [x] **Q93. A bought thing with a queue starts open, where the original's starts closed.** Found by Q50b's decode.
  The constructor closes every object with the queue-path bit (`0x004db712`..`0x004db793`), and the first queue
  measure that finds its back connected opens it. `ParkBuilding` sets the bit (`Info.HasQueue`, descriptor `+0x40`,
  pinned by Q50b) but not the close, counted `BOUGHT_QUEUED_THING_STARTS_CLOSED`. Build the close after the script is
  bound, and re-confirm Q1's flow on top of it. Confirm: buy a Belly Bounce, `objects` canload 0 until its queue joins
  a path, then a guest boarding. Seen in the original (Q175b): an Aztec Mayhem bought in the stock park read "CLOSED:
  LINE NOT CONNECTED" in its window until its queue joined the path, then open, with no press of its door.
  **Confirmed 2026-10-03 on `alexah/267-q93-bought-ride-closed`:** bought Belly Bounce 43 canload 0→1,
  12 Closed panels progress 1→0, and guest 35 visibly riding (`onride 1 bouncing 1`). Queue editing and actual
  path-tail removal close it with panels raised; reconnection reopens it. Nine regressions, nine rejected
  mutations, 1,727 full-suite passes. Loaded/disconnected normalization is tested; demolition preserves its
  nominee. Screenshots, predictions, census and original-trace limits: `docs/exe/ride-operation.md`, Q93.
- [x] **Q94. The console's `path` lays what the path tool refuses.** Found by Q50b's decode. `ParkPathBuilding.Lay`
  checks the cell's type and NOMODIFY but not the verdict `FUN_00535670`, which refuses path over a queue cell whose
  `mNeighbours` has more than one bit - every Belly Bounce queue cell (`ride-operation.md`, "The queue measured
  again"). Q50's game run cut the queue that way, a cut the player cannot make in one click. Route the console
  through the verdict and re-stage Q50's confirmation with a cut the player can make, or say which. Confirm: `path 51 22`
  refused with the verdict's reason.
  **Outcome, 2026-10-03:** Done 2026-10-03,
  `alexah/268-q94-console-path-verdict`. Public `Lay` shares the player verdict. Predicted `path 51 22` refusal,
  zero changed cells, zero charge and four retained queue pieces confirmed with screenshots and census.
  Q50 restaged by detaching each tail first: legal cuts reduce 12 → 8 → 4 queuers, eight predicted releases at
  happiness 35, photographed with logs. Eleven regressions; restoring the old method fails eight; 1,738 tests pass.
  `ride-operation.md`, "Q94: the console path shares the player verdict", records instruments and limits.
- [x] **Q97. The object's own cost of goods and chance of winning.** Found by Q50c's review. The object keeps both at
  `+0x188` and `+0x190`, built from the item at placement but saved and loaded with it (`FUN_004db7d0`,
  `0x004dcd01`..; file 1042 and 1050) and set per object from its window (`FUN_004e1a20`, `FUN_004e21c0`). OpenTPW
  reads the item's in the price opinion, the win roll (`FUN_004e2670`, `ParkRideOperation`) and the prize;
  the shipped Lost Kingdom save holds the items' own; Alexah's played parks hold a chance of 55 to 58 on all seven
  sideshows (the Jungle Spray 58, its item's 25), which the win roll, the price opinion and the excitement read. Read both from the save record, a bought thing's
  from its item, and say it at each site. The window's setters wait on Q31. No game run beyond a census of the two.
  Q165c added a fourth reader: a sideshow's excitement (`ParkRideScore.ExcitementOf`) takes both from the item too.
  From Q177: the shop's booking (`FUN_004e1b40`) reads `+0x188` too, and the win roll draws the park's generator
  (`0x004e26c6`) where `Succeeds` draws the `Random` it is handed.
  From Q177c: the booking is built and reads the item's cost of goods for both a shop (`ShopCostOfGoods`) and a
  sideshow (`SettleUp`, the same value as the prize, booked first); switch both with the rest.
  Completed 2026-10-03, `alexah/269-q97-object-goods-chance`: object fields and all readers restored. Screenshot +
  predicted `spend`: stock cost/chance 50/25 and 20/100; labelled fixture 80/58 and 80/100. Fourteen regressions,
  eight mutation failures, 1,752 tests pass. RNG ownership remains a deviation; played-save render fails on
  duplicate sprite key 0. `docs/exe/ride-operation.md`, Q97.
- [x] **Q207. Repair three defects found by an audit of the 2026-10-03 commits.** Asked for together by Alexah, 2026-10-03.
  Fresh Full Simulation hires and draws all five staff kinds from native initialization and archive counts;
  price opinions use catalogue InitCostOfGoods for the base and object cost only for the sideshow prize;
  failed selection reloads revoke cached profile write permission until a successful reload. Real hire-screen/XTEST
  drops, sprite censuses and screenshots; five defect/mapping mutations fail; restored 1,764-test suite has no skips.
  Branch `alexah/270-audit-fixes`. Evidence and remaining limits: `docs/exe/park-engine.md`, "Fresh staff sprites";
  pricing `docs/exe/ride-operation.md`, Q207; profiles `docs/exe/saves.md`, preservation policy.
- [x] **Q208. An open ride whose queue is cut off stays open.** Found by the 2026-10-04 review of Q93. Q93 built a
  close for every queued ride whose queue leaves the path (on a measure, on a path clear beside the tail, on load, and
  in `Open`), which the original does not have: `FUN_004df300` has three callers and none is an edit, the tail of
  `FUN_004de1f0` only reopens, `FUN_005367a0` measures only a type-1 cell beside a type-9, and `FUN_004df390` opens
  whatever its guard answers. Take the four out, build status 22 (UITEXT 388, 255/150/30), and correct
  `ride-operation.md`. Confirm: in the original, clear the path at the Belly Bounce's queue tail and read `+0x68` and
  its window; then the same in OpenTPW.
  **Done 2026-10-04**, `alexah/271-open-rides-stay-open-with-a-detached-queue`. The original (reference park under
  Proton, Backspace on the path cell at the tail): `+0x68` 1 at park ticks 7219 and 8331, queue length 4, the box
  reading "LINE NOT CONNECTED", the door's light green. OpenTPW, predicted first, `delpath 48 22`: `canload 1`, no
  "Closing" line, hoarding 13 at progress 0, the box "QUEUE NOT CONNECTED" in orange, photographed beside the
  original's. Four tests rewritten to the original; four ways of putting the bug back each fail one or two of them.
  Not done: the box's lettering is smaller than the original's (Q213); no test runs `ParkBuilding.Buy` (Q211).
- [x] **Q209. A player folder with no gms.dat is never saved again.** Found by the 2026-10-04 review of the profile
  preservation commits. `Players.Load` gave such a folder the read-only stand-in meant for a file that would not
  read, and `Select` revoked writing whenever the load answered null, so `SavePlayer` refused for good: the player
  played and lost everything on quitting, with a log line the only sign. Before those commits the folder was a new
  player; the original's writer (`0x005afc60`) writes unconditionally. Keep a missing file writable, keep a file that
  is there but unreadable protected, and do not replace a file that appears after the player was read. Confirm: a
  folder with no gms.dat, picked in the lobby, the game closed, the file on disk.
  **Done 2026-10-04**, `alexah/272-a-player-with-no-gms-dat-is-saved`. Predicted the file; a private game folder with
  `save/users/1nofile/` and nothing in it, the first slot clicked, the window closed: `Saves: wrote
  users/1nofile/gms.dat`, 748 bytes, version 12. `ProfilePreservationTests` gains a folder with no file saved and read
  back, and a file that would not open left alone once it can; four restored bugs each fail one or two tests.
  Not done: nothing on screen tells a player when a save is refused (Q216).
- [x] **Q98. Spot animations are never played. Decode first.** Found by Q50d. `FUN_004fc800(n)` plays animation `n`,
  stamps `mTimeOfLastSpotAnim` (`+0x208`), saves the state in `+0x224` and enters state 8, whose return
  (`FUN_004fc890`) is not built either; for `n` 4 it also plays sound `0x7e` for an id whose low nibble is nought. The queue turn
  reaches it for happiness above 80 and from 10 to 19 (`QUEUE_SPOT_ANIMATION`). While it is unbuilt a queuer's mood is
  read on every turn, and the window after an animation - the heading turned one turn in ten (`QUEUE_TURN_HEADING`) -
  is never reached. Decode its other callers and what animations 4 and 5 are, then build both. Confirm: `peeps` over
  a queue at happiness 90, the guests animating on screen.
  **Outcome, 2026-10-04:** decoded only, on `alexah/271-q98-spot-animation-decode`: five callers in two functions (5
  happy, 4 bored, 7 the vomit), state 8 returning on the eleventh sweep, scripts and picture sets 14, 12 and 6 in all
  twelve guest banks of 46, effect `0x7e` three yawns. `ride-operation.md`, "Spot animations". Nothing built, nothing
  run in the game; Q98b carries the build and its confirmation.
- [x] **Q98b. Build the spot animations.** Q98's decode is `ride-operation.md`, "Spot animations - `FUN_004fc800` and
  state 8". Build `FUN_004fc800` (the request, the stamp, the saved state, state 8, the yawn for an id whose low nibble
  is nought) and state 8's return through the saved state's own SetState; call it from the queue turn's two arms and
  retire `QUEUE_SPOT_ANIMATION`. The state-6 callers stay with their own items (Q107's 4, Q111's 5 and 7): say so at
  each site. Confirm: `peeps` over a queue at happiness 90, predicted first - a queuer in state 8 for eleven sweeps of
  every 31 - and the jump photographed with `pause` and `step`; the same at happiness 15 for the hands on hips.
  Done 2026-10-04: `PeepBehaviour.PlaySpotAnimation` and state 8's return; `QUEUE_SPOT_ANIMATION` is gone. In two
  runs 147 of 147 starts returned 11 sweeps on and 115 of 116 repeats came 31 on; sets 12 and 14 photographed.
- [x] **Q99. The board arm's put-out when no route is found.** Found by Q50d. The original forgets a guest called
  forward and puts them out when `FUN_004fa5f0` fails (`0x0050010a`); `QueueTurn` counts it (`QUEUE_BOARD_NO_ROUTE`)
  and walks them on. The stand point is on the entry cell (`FUN_004dedf0(0)`, `ride-operation.md`, "Leaving a
  ride"), but `FUN_004fa5f0` also fails without routing when its retry stamp at `+0x198` says so (`0x004fa62a`,
  `FUN_004fa770`), which nothing here keeps. The stamp is decoded (Q50e): `mStrandedTime`, set only at the dead end
  of `FUN_004f9490`'s linked walk and zeroed by every walk tick, so on the board arm it is nought unless a save loaded
  it - build the arm and say so at the site. Confirm: `unimplemented` over a long run (the counter's rate), then a
  boarding guest cut off by a path edit.
  Done 2026-10-04, `alexah/277-board-arm-put-out`: the arm is `Forget`, `LeaveQueue` and the common put-out, and
  `QUEUE_BOARD_NO_ROUTE` is gone. A guest at the front cannot be cut off (the first queue cell is NOMODIFY and the
  path verdict refuses it), so the run cut off a guest walking up a bought Belly Bounce's empty queue, re-laid under
  them: predicted and read, one put-out a sweep after the nomination, happiness 50 to 35, nominee 44 to 0; the
  unchanged build counted 1 and the guest rode. 300 s left alone: 0 in both builds. Four restored bugs each fail the
  new test. `docs/exe/ride-operation.md`, "Q99".
- [x] **Q100. A toilet's `+0x44`, which the queue turn's dirt gate reads. Decode first.** Found by Q50d.
  `FUN_004e0390` puts out a queuer for a toilet (`+0x32 & 1`) whose `+0x44` truncates below 25.0 (`0x00700550`);
  `+0x44` is its State of repair, saved at file 1074 and read as `CatalogueObject.StateOfRepair` (`park-engine.md`,
  "The object window's stats panel"), but nothing here lowers it and the gate is counted (`QUEUE_TOILET_DIRT_GATE`).
  Decode what lowers it (the handyman's cleaning, use), then build the gate. Confirm: a queue at Lost Kingdom's
  toilet, `peeps` before and after.
  From Q170: use lowers it. The settle-up's toilet arm calls `FUN_004e2440` with the need's byte (`0x004fe7a8`), which
  takes 0.05 of it off `+0x44`, held to 0..100, and on falling below 25 logs "Toilet has become dirty and smelly",
  unstamps `RegionFX` 1 around the toilet and stamps 6; in the online game (mode 1) there is no dirtying, and a toilet
  already below 25 is cleaned instead (`ride-operation.md`, "The effects of a visit", step 5). The handyman's cleaning
  is still to decode. From Q170b: the call is counted, `SETTLE_UP_TOILET_DIRTYING`, with the need taken before it is
  emptied.
  **Outcome, 2026-10-04:** decoded only, on `alexah/278-q100-toilet-dirt-decode`: use is the one thing that lowers a
  shipped toilet's `+0x44` (the wear `FUN_004df670` needs a `WearRate`, 0 for every feature); five readers of the
  dirty test; the handyman's search `FUN_004d7880` and states `0xa` and `0xb`, cleaning for `WorkDuration` then 100
  again; the three stock toilets saved at 100, so the sixteenth use at the earliest. `ride-operation.md`, "A toilet's
  dirt". Checked by a second reader in the listing: three statements corrected. Nothing built, nothing run in the
  game; Q100b carries the build and its confirmation.
- [x] **Q100b. Build a toilet's dirt.** Q100's decode is `ride-operation.md`, "A toilet's dirt". Keep the State of
  repair in `ParkState`, not `ParkWorld`; take 0.05 of the need's byte off it at the settle-up (retire
  `SETTLE_UP_TOILET_DIRTYING`), read it at the queue's gate (retire `QUEUE_TOILET_DIRT_GATE`, thought `0xe` counted),
  write `VAR_WORN` to a dirty toilet each turn, by name, so `Toilet.rse` adds its two objects, and answer 100 at the
  arrival's excitement difference. Count, do not build: the effect 1 and 6 stamps (no cell effects are kept here) and
  the handyman's search where his decide makes it (Q133 builds the decide; the clean needs it). Confirm: one toilet
  used sixteen times by guests sent with `send` at need 100, predicted first - 100 to 20 in steps of 5, the queuers
  behind put out on the sixteenth, `peeps` and the script's `VAR_WORN` before and after; the toilet's two script
  objects photographed.
  Done 2026-10-04, `alexah/279-toilet-dirt`: `ParkRideOperation.WearByUse` lowers the park's own record,
  `PeepBehaviour.QueueTurn` gates on `ParkState.IsDirty`, the toilet's turn writes `VAR_WORN` and the arrival's
  difference answers 100 (dead by content here); the effect stamps and the handyman's search are counted. Predicted
  and read on toilet 21: sixteen uses, 100 to 20 in fives, dirty once on the sixteenth, `VAR_WORN` 0 to 1, the two
  queuers behind (three in a second run) put out on the next sweep for 15, the script's two objects in `rides`.
  Photographed, and nothing shows: a script's particles are not drawn (Q20b). Not predicted: a seventeenth use, since
  an arrival at an empty queue is invited before the gate sees them, as the listing has it. The first run found the
  use breaking the rides' sweep; it walks a copy now. Eight tests; eleven restored bugs each fail one.
  `docs/exe/ride-operation.md`, "Q100b". A toilet here never becomes clean again until Q133 builds the handyman.
- [x] **Q102. The walk to a chosen thing's own arms.** Found by Q50e's decode (`ride-operation.md`, "Walking to a new
  place in the queue", the first caller). The original's state 10 (`FUN_004ffbc0`) takes `BigHappinessChange` (25)
  and pushes event 3 when the walk is stuck, where `GoingToRide` only goes back to deciding; takes 25 and clears
  `MajorDest` while walking with the park shut (`"The park has closed underneath me!"`), which nothing here does; and
  every 12th walking turn, counted across walks by the saved byte `+0x2c`, runs the minor decision `FUN_004fd570`,
  which may switch to a nearer thing and aim at its entry. Build the first two; the minor decision needs
  `FUN_004d8b40`'s raw line-search length. Confirm: `peeps` over a guest walking to a ride when the door shuts, and
  over one cut off by a path edit.
  From Q170: the walking-turn count and the minor decision are Q170b's steps 3 and 4, built after this item's park-shut
  arm (`ride-operation.md`, "A second toilet: the minor decision and the saved major").
  From Q170b: both are built, in `PeepBehaviour.WalkOn`; the shut arm is counted there (`GOING_TO_RIDE_PARK_SHUT`) and
  skips the count, as the original's does.
  Done 2026-10-04: both arms built (`PeepBehaviour.LoseHeartOnTheWay`), `GOING_TO_RIDE_PARK_SHUT` retired, the stuck
  arm's event 3 counted (`GOING_TO_RIDE_STUCK_EVENT`). Predicted and read in the game: the door pressed under guest 43
  walking to the Belly Bounce, happiness 50 to 25, dest 13 to 0, Deciding, one "closed underneath me" line; the path
  at (48,22) lifted under another, 50 to 25, dest 0, Deciding, one "stuck" line and the counter at 1; both
  photographed. Three tests; six restored bugs each fail. The guest reaches the gap before the walk answers stuck,
  where the original's walker re-plans on the edit: not measured. `docs/exe/ride-operation.md`, "Q102".
- [x] **Q103. The gates at the back of a queue.** Done 2026-10-04, `alexah/281-arrival-gates-keep-dest`: the room
  refusal keeps `MajorDest`, the excitement refusal zeroes `+0x1fc`, every arm's event and thought counted by name
  (`ride-operation.md`, "Q103"). In the game, predicted first: of seven guests sent to Small Toilet 21 from its back
  cell four joined and three were Deciding with `dest 21` (the unchanged build: `dest 0`), `ARRIVAL_NO_ROOM_EVENT`
  3; a kind 0 at the Jungle Spray, `idle` 202 to 0. The item as written: Found by Q50e's decode. On arriving, the original refuses on room
  with event `0x15` and KEEPS `MajorDest` (`GiveUpOnIt` clears it); asks excitement only when the item's `+0x13c`
  (`UsageInfo.ExcitementLevel`) has a non-zero low byte, of the OBJECT's computed excitement (`FUN_004e0860( object,
  0 )`, `FUN_004e0560`) where `TurnsAwayFrom` reads the catalogue level, with an event and thought per arm, then
  pushes the thing onto `mPreviousTemporaryRides` and zeroes `+0x1fc`; and refuses a queue too long (`FUN_004ddb60`
  against `FUN_004dda40`, which is 100 for a thing without the queue-path bit, so Lost Kingdom's five need no
  unproven field). The computed excitement is blocked on the divisors `park-engine.md` will not guess: say at
  `TurnsAwayFrom` that the catalogue level stands in for it. Confirm: `peeps` for a guest refused at a full queue,
  dest kept.
  Q165c built three of these: `TurnsAwayFrom` reads the computed excitement, both refusals push the thing onto
  `mPreviousTemporaryRides`, and the too-long gate stands (100 without the queue-path bit; with it, counted as
  `QUEUE_TOO_LONG_CAPACITY`). Left: the room refusal keeping `MajorDest`, the events and thoughts, and `+0x1fc`.
  From Q173: the too-long gate is built for a thing with a queue path too (`PeepBehaviour.QueueTooLong` on
  `PeepBehaviour.LongestQueue`), and `QUEUE_TOO_LONG_CAPACITY` is gone.
- [x] **Q104. The chooser routes as it walks the objects.** Found by Q50e's decode. `FUN_004fcb10` routes every
  candidate that beats the best in turn, so a better one that cannot be routed still leaves the walker failed while
  `MajorDest` names the earlier winner, and the first state-10 turn takes the stuck arm (−25) - unless a ground change
  revives the loser's route and walks the guest to the loser's queue. `ChooseSomewhereToGo` routes once, to the final
  best. Build it with the walker's failed state, and say it at the site. Confirm: a guest choosing between a
  reachable shop and a better ride cut off by a path edit, `peeps` and the log.
  **Done 2026-10-04.** Predicted and read in the running game with (48,22) lifted: kind-2 guests 46 and 47 routed to
  thing 14 (score 14), found no route to thing 13 (17), stood `GoingToRide` `dest 14` at happiness 50, and a sweep on
  were `Deciding`, `dest 0`, 25, `GOING_TO_RIDE_STUCK_EVENT` 2; the unchanged build: no counter, `dest 0`, 50.
  Photographed. Seven tests; nine restored bugs each fail. 1789 pass, 0 skip. The revived route is not built
  (`PeepWalk` keeps no ground stamp), said at `ChooseSomewhereToGo`; not compared with the original.
- [x] **Q105. The chooser scores the distance at the back of the queue.** Found by Q50g (`ride-operation.md`, "Where a
  guest is aimed"). `FUN_004fcc30` reads the squared distance, the close-to-queue test (under 9) and the nearby-effects
  divisor at `GetBackOfQueue`'s cell (`FUN_004de110` with the object in `ECX`, `0x004fcc49`..`0x004fcc7d`);
  `ParkRideChooser.ScoreOf` reads all three at the entry cell, which for the Belly Bounce is four cells from its back.
  Build it and retire the remark at `ScoreOf`. Confirm: `why` over a guest nearer the Belly Bounce's entrance than its
  back of queue, the chosen thing before and after.
  The score's queue term has the same root (the 2026-09-26 staleness audit): `ParkRideScore` divides by the save's
  `mQueueSizeInCells`, nought on the Drinks Shop and the three toilets (a guard makes it one), where the original's
  `+0x40` is the count `GetBackOfQueue` walks, the call `FUN_004fcc30` makes first (`ride-operation.md`, the
  `GetBackOfQueue` row). Divide by the walked count (`ParkRideChoice.QueueCellsFor`) with it.
  Q165c built that half, the count to the first guest no longer queueing over the walked cells; the distance, the
  close-to-queue test and the effects divisor at the back-of-queue cell remain.
  **Done 2026-10-04.** `ScoreOf` reads all three at `GetBackOfQueue`'s cell, re-read first-hand in the listing.
  Predicted and read in the running game with `why`, which now prints each candidate's score: a kind 2 by the Belly
  Bounce's entrance, (53,21), scores it 17 (the unchanged reading: 25) and one at its back of queue, (48,22), 25 (17),
  20 with eight queueing (17); a kind 0 by the entrance with a toilet need of 60 chooses toilet 21, 12 against 11,
  where the unchanged reading chooses the Belly Bounce, 19. Photographed. Three new tests, nine re-aimed at back
  cells; five restored bugs each fail. 1792 pass, 0 skip. Not compared with the original; no shipped cell has an
  effects count, so that divisor is tested only.
- [x] **Q107. `Decide`'s stamps and the chooser's empty hand.** Found by Q53 (`ride-operation.md`, "The state-6 turn,
  in order", the split). The original restamps `+0x1fc` only when a wander fails (`0x004ff3f4`) and when the chooser
  finds nothing (`0x004ff4a3`); `Decide` restamps after a routed wander and before choosing. With nothing chosen the
  original pushes event 1, plays spot animation 4 (Q98) and docks `SmallHappinessChange` (`0x004ff492`); ours does none.
  Confirm: `happy` and `peeps` over a Deciding guest the chooser fails, −5 each time it runs.
  **Done 2026-10-05.** `Decide`'s split is the listing's, re-read first-hand (`0x004ff3b4`..`0x004ff4a9`): a routed
  wander and a choice that names a thing stamp nothing; a failed wander stamps; the empty hand counts event 1
  (`DECIDE_NOTHING_CHOSEN_EVENT`), plays spot animation 4, takes `SmallHappinessChange` and stamps; the gate is
  unsigned. Predicted and read in the running game on six kind-0 guests made with `admit` on the approach around
  (47,12), where `why` picks nothing: 43 "the chooser found nothing" lines over three runs, every one exactly 5 off
  (50 to 45 down to 25 to 20), `PlayingSpotAnimation` with Deciding saved, back 11 sweeps on (27 of 27 in the third run), no guest's
  two closer than 32 sweeps, the counter 30 against 30 lines, guest 48 yawning on 6 of its 6; in a fourth run 43 of
  43 `idle` readings were nought, an empty hand's tick or a failed wander's. Photographed: a guest at (45,28) after a
  drink, `guests` reading set 14, then set 0. The unchanged build, same run: no line, no counter, 55 readings at
  happiness 50. The first run's prediction was wrong twice (a queue stamps a queuer; a drink gives happiness back),
  and its photograph was six guests on one cell, retaken. Seven new tests, three re-aimed; eleven restored bugs each
  fail. Compared with the original afterwards, the same day, by memory (`q107/orig/watch.py`, `watch1.log`): 112 of 112
  empty hands stamped `+0x1fc` and `+0x208` with that sweep's `mGameTick`, 108 exactly 5 off, all back 11 sweeps on,
  none inside 31; 377 wanders and 72 choices wrote no stamp. It found Q222. The events are counted, not kept; the yawn
  not listened to. Harness
  `q107confirm.py`, runs `q107/run1`..`run4`, `control`. `docs/exe/ride-operation.md`, "Q107".
- [x] **Q108. `SetRandomDest`'s linked walk.** Found by Q53 (`ride-operation.md`, "SetRandomDest", the linked arm).
  The original walks r % 5 + 1 linked cells from the mask of the cell being LEFT, never ending on the guest's own
  cell, and aims inside the last; it drops queue and entrance neighbours from a path cell, a queue cell's
  `mDirection` slot and exit cells; below a count of 2 it takes a fixed order, else a random start with no reverse.
  Ours steps one adjacent cell. Q206 restores the staff destination filters and adds explicit movement containment;
  the guest filters and the multi-cell walk remain here. Add `mSetDestSuccessfully` and SetState(7)'s re-aim with it. Confirm: over a run, no
  wanderer steps from (48,22) onto a queue or entrance cell, and wanders of up to five cells in the census.
  **Done 2026-10-05.** The linked walk is `LinkedWander.Walk`, the listing's (`0x004f95c6`..`0x004f99f2`, re-read
  first-hand): slots from the mask of the cell being left in the order `0x10`, `0x04`, `0x01`, `0x40`, no edge test,
  the three filters and the count, the fixed order below two and the random start with no reverse above, the extra
  pass off the own cell, the aim inside the last cell. Guests and staff inside their patrol area walk it;
  `Peep.SetDestSuccessfully` and SetState(7)'s re-aim are built (`PeepBehaviour.SetWandering`); a guest's dead end is
  counted (`WANDER_DEAD_END_STRANDED_STAMP`, its stamp and thought are Q110's). Predicted and read in the running
  game on forty guests made with `admit` on (48,22) and left 150 s, every wander logged with its passes and cells:
  543 wanders, passes 1 to 5 at 114, 107, 112, 101, 109; every step along a link of the cell left; none from path
  onto a queue, entrance or exit cell; none ending on its start; ends 1 to 5 cells off; **0 of 52 from (48,22)
  stepped onto the queue's back cell (49,22)**. The control, the same build with the path filter taken out: 17 of 58,
  predicted a quarter. Photographed 12 s and 40 s in. One prediction was worded wrong: ten `peeps` readings had a
  wanderer on (49,22), each a guest turned away at the full queue whose wander began there and left by the path.
  Compared with the original the same session, by memory (`q108/orig/wander.py`, `wander1.log`, 946 sweeps of the
  stock park): 348 wander ends, none on the own cell, none past five cells (89, 80, 80, 44, 55), all in path cells,
  sub-cell bytes all 5..123, `+0xd0` 1 on all, 62 from a path cell linked to a queue or entrance and none into it.
  Not read there: the cells stepped, the slot order, the single-link choice, a wander from a queue cell. Nineteen new
  tests, four re-aimed; nineteen restored bugs each fail (two stayed green until a staff test was added). Staff
  outside their area whose patrol roll fails still answer false (the original walks on unfiltered), said in
  `ride-operation.md`'s table. Harness `q108confirm.py`, runs `q108/run1`, `control`. `docs/exe/ride-operation.md`,
  "Q108".
- [x] **Q109. When a guest leaves. Alexah's call first.** Found by Q53 (`ride-operation.md`, arm (d)). The original
  tests leaving in state 6 alone: the happiness byte nought, `mExitLevel` exactly nought (it counts down unclamped, so
  a four-sweep window) or the park shut; it docks 25 every turn the test holds, aims at `CrossingParkSide` (47,9) and
  (48,9) with a mode-1 retry, and sets state `0x12` only on a route. `Step` sends home any unheld guest at
  `ExitLevel <= 0` from any state, docking nothing, at the bus stops, whatever the route. Retiring `Step`'s arm keeps
  most guests in the park until unhappy or shut, which a player will see: measure first how many leave through the
  window, then ask.
  From Q170b: that retry writes `+0x188`, the navigator's walking mode (`0x004fef04`); so do the gate's states and the
  put-down `FUN_004feb50`. Every person here walks in mode 0 (`ParkPeople.WalkingMode`).
  **Done 2026-10-05.** Measured first, in the original under Proton (the stock park, 692 s, `q109/orig/leave.py`):
  37 guests left, **5 at exit level nought** (each from state 6, exactly 25 off) and 32 with the happiness byte
  nought; **36 passed nought and stayed**. OpenTPW's old arm in the same park for 720 s sent all 17 guests home and
  left the park empty. Alexah chose the original's rule (2026-10-05). Built as the listing reads (`0x004fee5b`..
  `0x004fef13`, re-read first-hand): `PeepBehaviour.WantsToLeave` and `Leave` in the deciding turn, on its one draw;
  `Step`'s arm gone; the crossing's cells in `ParkAdmission`; a stuck leaver put back to deciding (`FUN_00500a50`);
  the gate's three leaving arms zero the exit level. The mode-1 second pass is counted (`LEAVE_ROUTE_MODE_1_RETRY`).
  Predicted and read in the running game: the stock park left 720 s, 0 left for the exit level (predicted 0 to 4), 10
  miserable, 7 still in at the end, six below nought; forty guests made with `admit`, 240 s: **8 left as their day
  ran out, each at exactly 0 and 25 off** (predicted 2 to 10), 23 below nought and still in; the entry-price door:
  30 of 30 left as the park shut, each 25 off, to (47,9) or (48,9). Photographed walking out through the gate.
  Nineteen new tests, one re-aimed; nineteen restored bugs each fail (one by a test of its own only after one was
  added); the twentieth, crossing B read on its own row, cannot fail on shipped data, every theme's two rows being equal. Leavers are taken out at the crossing until Q128 builds the walk on to the stop.
  Harnesses `q109confirm.py` (before), `q109fix.py`, `q109fix2.py`; runs `q109/run1`, `fix1`, `fix2`.
  `docs/exe/ride-operation.md`, "Q109".
- [x] **Q110. The stranded bookkeeping, and thought bubbles.** Found by Q53 (`ride-operation.md`, "The stranded
  bookkeeping"). The shared counter, the 33 × 33 block stamps its map writes leave, `FUN_004fa770`'s 3 × 3 test, the
  refusals in SetRandomDest, `FUN_004fa530` and `FUN_004fa5f0`, the dead-end stamp, and SetThought's bubble
  (`FUN_0050be80`: sprite script `0x0074f2f8` of kind 9, gone 13 to 16 sweeps on). None is kept; only the queue
  re-measure's stamp (`QUEUE_REMEASURE_BACK_CELL_STAMP`) and five guest sites' thoughts (`*_THOUGHT_*`) are counted.
  Count the rest first; measure whether a Lost Kingdom guest ever reaches `0x004f9e09`; decode which picture thought
  `0x11` is.
  Q82 found the staff's own thoughts through the same `FUN_0050be80`: `0x14` tired, `0x13` unhappy, `0x12` very happy,
  `0x15` the strike walk, `0x16` a failed patrol roll (`ride-operation.md`, "Drawn on the way").
  From Q177: SetThought is decoded whole (`ride-operation.md`, "Thoughts 5 and 6, and the bubble"): the class gate,
  the pictures, the lift of 2.5, the expiry and the four readers. A Jungle Spray player's thought 5 or 6 is class 0 and
  showed a live bubble on every play measured in the original; here it is counted as `SETTLE_UP_SIDESHOW_THOUGHT`.
  **Done 2026-10-05**, as the count, the measure and the decode; the build is Q110b. **Thought `0x11` is a blue
  bubble holding a question mark**, kind 9's set 15 (`Generic\Thoughts\SPR_TB`), named "Confused..." by THOUGHTS.str
  row 17; all 22 thoughts' pictures and classes and all 22 callers of SetThought are tabled. Counted where the
  original does it: the block stamp (`MAP_TYPE_WRITE_BLOCK_STAMP`, where a cell becomes ground, path or queue), the
  fee's thought 6 (`FEE_JUDGEMENT_THOUGHT_6`), the staff's `0x14`, `0x13`, `0x12` (with its draw) and `0x16`. The
  refusals need a stamp, so they are reached only after `WANDER_DEAD_END_STRANDED_STAMP`. **In the original** under
  Proton: the stock park left alone 1,630 sweeps, no `+0x198` written and no thought `0x11` in 108 bubbles; with the
  path at the Belly Bounce's queue tail taken up and the ride shut, three put-out queuers were stamped inside 32
  sweeps, each under a blue question mark with a red square blinking beneath (photographed), stood 900 sweeps
  thinking it 900 times, and were stamped afresh when the path cell was laid back. **In OpenTPW**, predicted first:
  the stock park 150 s, every new count nought; `delpath 48 22`, the block stamp 1 (the queue re-measure's 0, where
  1 was predicted: wrong, a path beside a queue cell measures nothing); ten guests made on the cut tail, 343
  wanders, 343 dead ends, all from (49,22); photographed, nothing over them, and four joined the queue where the
  original's stand refused. Eight new tests, three extended; eighteen restored bugs each fail.
  Harness `q110confirm.py`, run `q110/run1`; the original: `q110/orig/strand.py`, `strand1.log`, `strand2.log`,
  `s13.png`, `s15.png`; the pictures: `q110/thoughts.py`. `docs/exe/ride-operation.md`, "Thoughts and their
  pictures" and "Q110"; FileFormats `sprites.md`, "Thought bubbles".
- [x] **Q223. Decode the red square under a stranded person, and the counter's 14 sites.** Split from Q110b, which
  said to decode the square first. Done 2026-10-05, decode only, nothing built or run: `FUN_004fa030` queues the
  build tools' own red square (`FUN_0053c8d0`, texture 1, face 0, one cell) at the person's own cell, 1.0 over the
  ground, waving, every frame `+0x198` is non-zero, for guests and all five staff kinds; red blinks seven rendered
  frames on and two off for every red square at once (`FUN_0053c3f0`); all 14 callers of `FUN_004d8c50` tabled
  (`ride-operation.md`, "The stranded bookkeeping", "The red square under a stranded person"). The proof is the
  listing: 14 of 14 call sites read. Q110b is the build.
- [x] **Q110b. Build the stranded stamp, its refusals and the thought bubble.** Split from Q110, which counted,
  measured and decoded it (`ride-operation.md`, "The stranded bookkeeping", "Thoughts and their pictures", "Q110").
  Keep the shared counter (`FUN_004d8c50`, its 14 sites) and the 33 × 33 block stamps (`ParkState.CountBlockStamp`
  is where the three writers land; the queue re-measure's is `QUEUE_REMEASURE_BACK_CELL_STAMP`); `mStrandedTime` on
  the guest, written at the dead end (`WANDER_DEAD_END_STRANDED_STAMP`); `FUN_004fa770`'s 3 × 3 test and the refusals
  in SetRandomDest's entry, `FUN_004fa530` and `FUN_004fa5f0`; the walker's re-plan on a newer stamp
  (`0x0050ed79`), which Q102 and Q104 left unbuilt. Then `mLastThought` and SetThought's bubble: a kind-9 world
  sprite from the two `Generic\Thoughts` banks by the tabled set, 2.5 above the guest, the class's wait, gone 13 to
  16 sweeps on, hidden in first person; and the red square blinking under a stranded guest (decoded by Q223:
  `ParkBuildMarkers`' red square at the guest's own cell, 1.0 up, seven frames on and two off, which also settles
  `MARKER_RED_BLINK_TIMING`). Every counted `*_THOUGHT_*` site then calls it. Confirm as the original was measured:
  `delpath 48 22`, the Belly Bounce shut by its window's door - the put-out queuers stamped, standing, each under
  the blue question mark, `peeps` and a screenshot beside `q110/orig/s13.png`; then the path laid back and each
  stamped afresh. The original's three stood 900 sweeps; here four of ten joined the queue.
  **Done 2026-10-05.** Ten guests in the queue, the cut, the door: 10 of 10 stamped inside 30 sweeps, each thought
  `0x11`, bubble 0/15, none moved, refused 0.33 of their sweeps (the original: 3 of 3 inside 32, 0.33); the path laid
  back, 10 of 10 stamped afresh; photographed under the blue question mark over the red square, and with the red
  gone. The needs' thought picker `FUN_004fc8a0` was decoded and built with it. Left: a failed route is not revived
  by a stamp (Q104's note), the picker's litter arm and the thoughts' sounds are counted, a saved bubble is not made
  again. `ride-operation.md`, "Q110b".
- [x] **Q111. The state-6 turn's arms before its split are unbuilt and uncounted.** Found by Q53 (`ride-operation.md`,
  "The state-6 turn, in order"). (a) spot animation 5 above happiness 80, (b) vomit, (c) litter to a bin (the Litter
  Bin at (44,29)), (e) facing an entertainer, (f) pranks: each is reached in Lost Kingdom and none calls
  `Unimplemented.Report` (`CLAUDE.md` rule 4); (e)'s fireworks half is dead by content. Count each where the original
  tests it, with its one draw, and put its build in the queue.
  **Done 2026-10-05:** counted, nine names in `PeepBehaviour.CountBeforeLeaving` and `CountAfterLeaving`. Read in
  Lost Kingdom: the stock park 120 s, all nought but an entertainer beside 4; every guest at litter 90, 33 deciding
  turns, the happy jump 29, the vomit 10, the bin in reach 8; prankery 101, 18 litter pranks in 18 turns. The
  original's stock park (Q107's log): one happy jump and eight entertainer watches in 964 sweeps. Seven new tests;
  twenty-two restored bugs each fail (two only after the tests were tightened). The builds are Q224 to Q228.
- [x] **Q112. Staff on a cell with no links do not look for path.** Found by Q53b. The no-links arm reads no person
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
  **Done 2026-10-05.** Measured first: the stock park left alone 240 s reaches the count 0 times. Built:
  `StaffBehaviour.SetRandomDest` takes `PeepBehaviour.WanderFromNowhere` on a cell with no links, inside the area or
  after a failed roll outside it; the count is gone. Predicted from the probe table and read: the guard put down on
  (42,24), (54,24) and (42,26) aimed at (45,21), (56,26) and (44,28), the researcher on (42,24) at (45,21), 4 of 4,
  each there inside 6 s, photographed; the unchanged build counted 1, 2, 3 and 8 and walked to the roll's cells. **The
  original**: the guard put down on (42,24) and (42,26) was given (45,21) and (44,28), each the cell's centre, 2 of 2.
  Three new tests; eight restored bugs each fail. The put-down's own timing differs: Q229.
- [x] **Q113. The gadget's body, aerial and arm take no press.** Done 2026-10-05,
  `alexah/293-gadget-body-aerial-arm-take-a-press`. The body answers inside its 23-point outline and the handle inside
  its 16-point one; the arm, its end and the aerial over their rectangles; the handle sits where the builder leaves it,
  645 left of the stream and in front of the body, and the aerial's top on the neck (`hud.md`, "The arm and the aerial
  as built"). The aerial's right click is counted, `AERIAL_DELETE_ALL_MESSAGES`. Confirmed in the game with the Belly
  Bounce in the hand and a real quick right click: 8 of 8 gadget points kept it, the bare corner let it go, each
  predicted first (the unchanged build let go 9 of 9); photographed beside the original's frame. In the original, path
  tool armed: body, red top, mast and handle kept it; grass and the bare corner did not. Q66's hit test is untouched.
  The item as written: found by Q56. The original's body `0x1d` answers
  inside its 23-point outline (stream `0x00752940`, sub-op 4 at `0x00752ac2`), the arm `0x21` and its end over their
  rects, the handle `0x23` inside a 16-point outline, and the aerial `0x2d`/`0x2e` over theirs (`0x2e` answers a right
  click itself, `0x004a11a2`); here none takes the pointer, so a left or a quick right click on them reaches the park.
  Read the outlines into `UiControl.Outline`, build the aerial, and see Q66 before changing the hit test. Confirm: an item
  in the hand, a quick right click on the body's bare metal keeps it; a screenshot.
- [x] **Q114. The park's full-screen toggle `FUN_004a29d0`. Decode first.** Found by Q56. It hides layer 0 under a
  full-screen control whose handler `0x004a2840` gives a right press to the camera and arms nothing. Only handler
  `0x0048a740` turns it on (`0x0048a7f3`, `0x0048a8d8`), installed by the screens `FUN_0048ac40` and `FUN_0048adb0`
  build; `FUN_004815d0`, `FUN_0048ac40`, `FUN_0048adb0`, `FUN_0048ae70` and `FUN_004a9180` turn it off. Decode what a
  player reaches it from, and whether F2 here is it, before building anything.
  **Decoded 2026-10-05.** It is **F3**: `game` row 4, key `0x72`, through the thunk `0x00481490`; F2 is bound nowhere.
  On, a full-screen control hides layer 0 and hears only the camera, F3, Escape and Ctrl+P; refused in first person.
  The screens `FUN_0048ac40` and `FUN_0048adb0` build are the end of a park's. In the original: F2 nothing, F3
  `[0x007cb2e8]` 0 to 1 and the gadget gone, F3 or Escape back with no menu, each predicted. `park-engine.md`, "The
  full-screen view: F3".
- [x] **Q114b. Build the full-screen view on F3.** Done 2026-10-05, `alexah/296-full-screen-view-f3`:
  `ParkFrontEnd.ToggleFullScreen` over `WindowStack.Covered`. F2 measured first (10 of 11 predictions; the miss was
  the harness's pixel ruler) and kept at Alexah's word (`docs/DECISIONS.md`). Predicted and read in Lost Kingdom with
  real keys and buttons: F3 held nothing, let go the gadget's blue 4775 pixels to 0 and `windows` "under the
  full-screen view"; B, a left click on grass, Backspace, Delete, a quick right click with the Belly Bounce in the
  hand and C changed nothing; the Left arrow, W and the wheel moved the camera; Escape put the interface back, hand
  and tool as they were, no GameMenu; refused in first person. In the original the same day: B, a click, C and Escape
  under F3, every reading as predicted (one control click, outside the park after the camera turned, run again). Twelve new tests; nineteen restored bugs each fail. The item as written:
  Split from Q114, whose decode is `park-engine.md`, "The full-screen
  view: F3". First measure what F2 does here now (the hand, a tool, a shortcut, Escape, first person, F2 sent
  through XTEST), predicted first. Then build the original's: F3 let go toggles it in a park, refused in first person;
  on, the park's layer is hidden and a full-screen layer keeps the focus, so the camera's keys and mouse work, F3 or
  a plain Escape puts the interface back and opens no menu, Ctrl+P is the postcard's (counted), and no other key or
  press reaches the park. Whether F2 stays as OpenTPW's own key beside it (it also works in the lobby) is Alexah's
  call: ask first. The end of a park's use of the view waits for an end of a park. Confirm: F3 in Lost Kingdom, the
  frame without the gadget beside the original's (`q114/orig/k2-F3.png`), `windows` and `tool` either side; B and a
  left click on grass under it change nothing; Escape, the gadget back and no GameMenu.
- [x] **Q115. The park screens are modal here and are not in the original.** Done 2026-10-05,
  `alexah/297-park-screens-not-modal`: no park screen is modal (`UiWindow.ParkScreen`, `WindowStack.OnParkScreen`,
  `ParkScreenOpen`, `Level.KeptFromThePark`). Decoded first-hand: `FUN_00485b70` switches the camera, cheat and game
  tables off as a screen opens and its handler runs the shortcuts' alone, so **F3 over a screen is the original's
  nothing** and `FULL_SCREEN_VIEW_OVER_PARK_SCREEN` is gone; a press beside a screen is skipped but its release still
  commits an armed tool. Predicted and read in Lost Kingdom with real clicks and keys, 12 of 12 (the unchanged build
  the other way on each): Info beside the buy screen left `windows` ending ParkStaffScreen; a click on the ride
  window's bare frame and on grass beside it, `tool` mode 0; the same grass click with no screen, mode 1 anchored at
  (47,20); with the tool armed beside the buy screen, a run laid to (49,20); F3, the Left arrow and Backspace over a
  screen, nothing; the window's all-items button, the buy screen alone. In the original the same day, 13 of 13 read
  from memory (`park-engine.md`, "A park screen is open"). Eleven tests new or re-aimed; twenty-seven restored bugs
  each fail (one only after a test was tightened). Filed: Q231. The item as written: Found by Q56's review. The original builds
  the six management screens and the nine object windows onto layer 0 (`park-engine.md`, "Whose a right press is"): its
  gadget answers beside a screen (`FUN_004a0940` tests only the game menu), a left press on the park beside one is kept
  from the hand (`0x00488741`) but reaches the layer, a screen's root takes a press anywhere on it, and opening one
  closes the one open (`FUN_00485b40`, `DAT_007c24c8`). Here the six are `Modal`, which shuts out the gadget; the object
  window's bare frame lets a left press through to `ClickWorldAt`, and a left press beside it acts on the park; and the
  buy screen opens over an object window and leaves it. One hit reading for both buttons. Confirm: with a ride's window
  open, a left click on its frame over a path does nothing; the gadget's Info beside the buy screen switches screens.
  From Q114b: F3 over a management screen does nothing here and is counted (`FULL_SCREEN_VIEW_OVER_PARK_SCREEN`),
  where the original's screen handler runs the key tables (`FUN_00488ba0`) unless the screen switched them off as it
  opened (`FUN_00486b70`, eleven callers, not decoded screen by screen): decode which, and let F3 hide a screen that
  leaves them on (`park-engine.md`, "The full-screen view: F3").
- [x] **Q117. A right click on a list row or an object window's preview.** Done 2026-10-05,
  `alexah/298-list-row-right-click`. Decoded first-hand: `FUN_004867b0` puts the camera on the corner of the thing's
  cell (bytes `+5`, `+7`, times ten) and leaves the spin and zoom; the list's `FUN_0066563d` selects the row under the
  click and posts `0x402` with the selected row, so a miss names the row selected before; the preview's handler
  `0x0048d1a0` answers either button. Built: `ParkOrbitCameraMode.GoToThing`, `UiList.RowRightClicked`, the three
  screens and the preview. Predicted and read in Lost Kingdom with real clicks, **10 of 10**: a guest's row, the
  camera from (300,300) to (480,170), ten times the paused census's cell (48,17), the screen shut, photographed;
  the unchanged build 5 of 5 the other way (`LIST_ROW_RIGHT_CLICK` 3). **In the original**: visitor 3's row, the
  look-at (475,175) to (560,280), ten times its cell, the screen shut; the preview with each button the same.
  Nine tests new, 29 restored bugs each fail. Not built: the miscellaneous tab's rows (counted) and the row a list
  opens with selected (Q232). `park-engine.md`, "The camera goes to a thing"; `hud.md`, "A right click on a list".
  The item as written: Found by Q56. The all-staff, visitors and
  all-items lists answer a right click on a row (`0x402`) by moving the camera to that thing and closing the screen
  (`FUN_004867b0`: `0x0049602f`, `0x004934c5`, `0x00495584`); an object window's preview answers any click the same way
  (`LAB_0048d1a0`). The click is the UI library's (`hud.md`, "A click and a double click"; `WindowStack.RightClick`). Counted on the
  press as `LIST_ROW_RIGHT_CLICK`; the preview is no control of its own here, so its click is not counted. Confirm: a right
  click on a guest's row, the camera on that guest and the screen shut; a screenshot.
- [x] **Q188. The object windows draw the park's own model where the original previews its P model.** Done 2026-10-05,
  `alexah/299-object-window-p-model-preview`. Decoded first-hand: `FUN_004629d0` loads `"p%s"` into `+0xd0` behind
  flag `0x20000`; `FUN_004689f0` makes a FRESH instance (`0x400` takes the P model, the item's own without one),
  carries the name board only where both models name both halves, fits by the `.hmp` box (two thirds of the width
  for half the footprint's diagonal, or the height for the corner-to-corner from height nought), loops M entry 0;
  `FUN_00468e50` tips it 45 degrees and turns it 0.4 rad/s. Built: `ParkObjectPreview`, `ParkPreviewFit`, the
  console's `preview`. Predicted and read in Lost Kingdom, **5 of 5**, the fit's numbers the original's own read
  from its memory first: the Belly Bounce `bouncy.MD2`, half (0.15102,0.20136) share 0.55745 reach 0.31705; a bought
  Aztec Mayhem `Ptvsim.MD2`, (0.16666,0.16666) 0.59899 0.35205; a bought Inca Totem `Ptotem.MD2`, no pit under it
  (main draws the pit); the turn 0.408 rad/s (the original's 0.396). Photographed beside the original's at the same
  angles: the Belly Bounce's window, and the Aztec Mayhem beside the original's buy screen. Fifteen tests new, 25
  restored bugs each fail (one only after tightening). **Not in the original: the Inca Totem**, which its reference
  park has not researched. Not built: the first angle (counted), the light (OpenTPW's own), the wall clock.
  `park-engine.md`, "The object window's preview".
  The item as written: Found by the
  fork review (ghidra-docs-6, ghidra-docs-v2, refute rank 6; lead: Aluzed's fork, `docs/08`). For every item but the
  six fixed ones (bus, ferry, seaplane, gates, lights, end: `Info.DontApplyOffset`), the loader also loads
  `p<stem>` (`0x00462bd1`..`0x00462c07`) into `+0xd0`. The ride window's preview `FUN_004ad7f0` (vtable `0x006ffbe8`)
  and the shop window's (`0x004afbdc`, in the function at `0x004afb70`) call `FUN_00486410`, then `FUN_004689f0`: a
  fresh instance of the P model (flag `0x400` at `0x0046309b`) wearing the item's own sign textures (`FUN_00468950`),
  fitted by the item's `.hmp` box (six floats at file `0x18`, record `+0xcc`), playing role 5 (M) entry 0 looped at
  speed 1.0. Eleven jungle items ship one: totem, lookout, mumbo, spider, tvsim, both coasters, minecart, GOKARTS,
  wateride, Junspray. `ParkObjectWindow.DrawPreview` draws the live park instance fitted by its meshes, so a broken or
  closed ride previews its live pose. First: count `OBJECT_PREVIEW_P_MODEL` where the preview draws; correct
  `ParkObjectWindow.cs`'s comments (199-201, 669-673 say "the model's bounding box"); answer `park.md:239` and FileFormats
  `models.md:1271` ("not known"); cite `FUN_004ad7f0` and `0x004ad85c` in `park-engine.md`'s Ride window section. Then
  build: load `P<stem>.md2` beside the item's model for the preview only; carry `sign1`/`sign2` only where both models
  have them (`Pcoaster1` and `PJunspray` have none); fit by the `.hmp` box (the first consumer of `.hmp` builds its
  reader, Q191); loop M. The buy screen's missing preview (Q158) takes the same path. Confirm: the Inca Totem's
  window beside the original's, the difference predicted first. Alexah asked to work the ride preview's strangeness
  together (a wide base under a thin figure, 2026-09-21), and this may be its cause: start this item with Alexah.
- [x] **Q233. The preview's footprint picture: the item's size, entrance and exit. Decode first.** Asked for by
  Alexah, 2026-10-06, as the next item. Beside the turning model the original draws a small flat picture of the
  item's footprint at the panel's lower left: a blue block the footprint's size with a green and a brown mark on
  its edge, which do not turn with the model. Seen in Q188's frames of the buy screen with the Aztec Mayhem (a large
  block), the Crazy Ape and the Balloon Shop (a small one); not seen in the Belly Bounce's ride window, so where it
  shows is the first thing to settle (`~/.cache/tpw-harnesses/q188/orig/`, `sim*`, `buy*`, `shops.png`, `bbc*`).
  Nothing of it is decoded: `park-engine.md`, "The object window's preview" names the blue square and no more.
  Decode what draws it (start from `FUN_004ab1b0`'s four calls of `FUN_00486410` and the instance `FUN_004689f0`
  makes), what the colours stand for and where each mark is taken from (`Info.Shape`, the `.hmp`'s mark plane, the
  entry and exit cells), and which panels show it; write it to `docs/exe/` and stop. The build is the session after:
  `ParkObjectPreview` draws it, and the buy screen's panel (Q158) with it if that is where it lives. Confirm: the
  panel beside the original's for a ride and a shop, the block's size and each mark's place predicted first.
  Done 2026-10-06, decode only, nothing built: the buy screen's own fill `FUN_004ab1b0` paints the item's
  `Info.Shape` grid (`FUN_0052c5b0`) into control `0x1eb`, a square a cell, an eighth of the control each way, row 0
  at the bottom: blue a footprint cell, green the entrance (kind 9), brown the exit (kind 10), `.` and `+` nothing;
  no other window has it. In the original, the grid read from memory with each row hovered, predicted first, 4 of 4
  (Aztec Mayhem row 0 `1 2 3 1`), and the block 38.5 px for four cells of 6 game pixels. `park-engine.md`, "The buy
  screen's footprint picture". Q233b is the build.
- [x] **Q233b. Build the buy screen's footprint picture.** Done 2026-10-06. `ParkFootprintPicture` is control `0x1eb`
  in the buy screen's panel: the row's `Info.Shape` cells, an eighth of the control each, row 0 at the bottom, blue,
  green the entrance, brown the exit, at the 8/15 the original's frames read; cleared for the land rows and a mystery
  ride; shown more than 500 ms after the pointer moves onto a row, which selects it as the list's flag `0x80` does
  (`FUN_006656a0`, new in the decode). In Lost Kingdom, real pointer moves, **12 of 12 predicted** on the second run:
  Aztec Mayhem row 0 `1 2 3 1`, block (366,225)-(402,261) in a 1280 by 720 window, a cell 9 by 9; Crazy Ape's exit in
  row 3; Staff Room 2 by 2; Buy Land nothing; the unchanged build paints none. Beside the original's frames the blocks
  agree to a pixel of its 640 by 480 screen. Found on the way and fixed: the panel's `!frame` was drawn in the stats
  panel's shape (`hud.md`, "A frame worn at two sizes"). **The turning model was not put in the panel, and Alexah was
  not asked** (the session ran unattended): it stays Q158's. Nine tests new; thirty-three restored bugs each fail. `park-engine.md`, "The buy
  screen's footprint picture".
- [x] **Q119. A plain Escape does not close the park screen in front.** Found by Q57's review. In the original the six
  management screens, an object window and the map take the focus as they open (`FUN_00485b70`, `FUN_004862a0`), and
  their key handler answers a plain Escape let go by closing the screen, and nothing more (`FUN_00488ba0`, `0x00488bc6`;
  the map at `0x005f17ef`; `scenes.md`, "The park Escape route"). Here a management screen keeps Escape and does
  nothing with it (`ParkFrontEnd.MenuKey`), and an object window lets it through to the hand and the menu. Said at
  the site. Measured in the original by Q115: Escape over the entry-price screen and over Park Information closed each,
  the menu shut, an armed path tool kept (`park-engine.md`, "A park screen is open"). Confirm: the buy screen, then an object window with the path tool armed - Escape let go closes each, `tool`
  still Path and `windows` without GameMenu; a screenshot.
  Done 2026-10-06: `ParkFrontEnd.MenuKey` closes the park screen in front, or the map, on a plain Escape let go and
  is spent doing it; with a modifier held it does nothing there. Predicted and read in Lost Kingdom with real keys,
  **7 of 7** (the unchanged build 8 of 8 the other way): the path tool armed at (47,20), the buy screen, Escape,
  `windows` ParkGadget, ParkViewfinder and `tool` mode 1 at (47,20); the same over the Belly Bounce's window and the
  map; Shift+Escape, nothing. In the original, 7 of 7 from memory: the buy screen, a visitor's window and the map
  each closed, the menu shut, the tool and its anchor kept. Fourteen restored bugs each fail a test.
- [x] **Q120. The modifiers are judged as the frame ends, not at each key.** Found by Q57's review. The original reads
  Shift, Ctrl and Alt with `GetKeyState` at each key-up (`0x0046bb0b`, `scenes.md`, "The park Escape route"), so a
  modifier let go in the same frame as Escape but after it still counts. `Input.BindingMatches` and
  `Input.NoModifierHeld` read the held set as the frame ends, so Shift+Escape let go with Escape first, inside one frame,
  empties the hand or opens the menu. Carry each event's modifiers (SDL's, Shift, Ctrl and Alt only) or replay the
  frame's events in order. Said at `NoModifierHeld`. Confirm: Shift+Escape with the tool armed, Escape up then Shift up
  in one frame through XTEST - `tool` still Path.
  Done 2026-10-06: `Input.Releases` replays the frame's key events in order and gives each release the modifiers held
  as it came up; Escape, F3, the full-screen view's keys and `Input.KeyUp` read them, and `NoModifierHeld` is gone.
  Predicted and read in Lost Kingdom with real keys, two events landed in one frame by stopping the game, **6 of 6**
  (the unchanged build 6 of 6 the other way): Shift and Escape down, the path tool armed at (47,20), Escape up then
  Shift up, the log's "Shift held as it came up, None as the frame ends", `tool` mode 1 at (47,20), no GameMenu;
  Escape up then Shift down, a plain Escape, mode 0; the buy screen kept; Ctrl+C no camcorder. In the original, 4 of 4
  from memory: the same two orders, the tool kept and then put away, the menu 0. Ten restored bugs each fail a test.
  A binding read on its press or as a held state is still rebuilt as the frame ends.
- [x] **Q121. A held right button in first person does not walk.** Found by Q59's decode (`hud.md`, "Four ways out of
  camcorder mode"). The viewfinder layer's handler hands every message to `FUN_0042a760`, which sets 4 in `DAT_00790aac`
  while the right button is down (`0x0042a8bd`), and the walking camera adds the Up arrow's 0.1 to its forward term for
  as long as it is set (`0x0042b935`), whatever RMB cancel is. Here only the keys walk. Counted per frame held,
  `FIRST_PERSON_RIGHT_BUTTON_WALK` (`ParkCamcorderCameraMode.Walk`), a hold on the eject button included; the walk
  built must not take one, since the button's press never reaches the layer. Confirm: in first person hold the right
  button 2 s on the view, `state`'s camera moved forward; a screenshot either side.
  Done 2026-10-06: `ParkCamcorderCameraMode.RightHeld` is the bit, set by a press the interface did not take and
  cleared by the release, and `Walk` adds the forward key's amount for it; the counter is gone. Predicted and read in
  Lost Kingdom with a real button, **7 of 7** on the second run (the unchanged build 7 of 7 the other way): held 2.00 s
  at (475,110), the log's "walking from (475.0,110.0)" and "walked to (475.0,190.0)", 80.05 forward, still in first
  person; the same with RMB cancel off, 79.77; W alone 79.77; W with the button 1 s, 82; 2 s on the eject button,
  nothing; a quick click still leaves. In the original, read from memory: held 1.84 s, the button word `0x84`, 40.4
  forward; the Up arrow 1.87 s, 40.4; both, twice the rate; Down with it, standing; the eject button, nothing. The
  original walks at about 21.8 units a second where ours is a chosen 40 (noted under Q25). Nine tests; fourteen of fifteen restored bugs each fail (one only after its test was
  fixed); the fifteenth, `Level`'s one call taken out, only the game run sees.
- [x] **Q122. Entering first person leaves a park screen open.** Found by Q59's review. `FUN_00481a10`, which C and
  `b_1person` both reach, first closes the open screen (`FUN_00485b40`, message 5 to `DAT_007c24c8`), and entering hides
  layer 0 with anything else on it. Here an object window or a management screen stays up over first person, and a right
  press on its body is the screen's, so it does not leave (said at `ParkViewfinder.RightClickAnswer`). Add the call to
  `park-engine.md`'s decode of `FUN_00481a10`. Confirm: a ride's window open, C, the window gone; a screenshot.
  From Q115: `WindowStack.Open` closes the open park screen for any window that sets `UiWindow.ClosesParkScreen`
  (the game menu and the map do); entering first person opens no window, so it needs the close called.
  Done 2026-10-06: `ParkCamcorderCameraMode.Enter` calls `Level.CloseParkScreen` (`WindowStack.CloseParkScreen`, the
  body `Open` ran) before anything else, as `FUN_00481a10`'s first call does (`0x00481a2b`, re-read). Predicted and read
  in Lost Kingdom with real keys and buttons, **6 of 6** on the second run (the unchanged build 6 of 6 the other way):
  the Belly Bounce's window up, C let go, the log's "First person: closed the open park screen", `windows` ParkGadget,
  ParkViewfinder, in first person; a quick right click where the window was, out; the buy screen the same and one
  Escape out; the arm's first-person button the same; C alone, no line. In the original, read from memory: the buy
  screen, `[0x007c24c8]` `0x46bf8d0` to 0 on C; a visitor's window, `0x46cad30` to 0. Four test cases new; seven
  restored bugs each fail. Photographed either side.
- [x] **Q123. The click limits run on the frame clock.** Found by Q59's review. `WindowStack.RightClick`'s 500 ms and
  `Level.RightButton`'s 200 ms read `Time.Now`, whose frames are clamped to 0.1 s and which the console's `pause` holds;
  the original times both in milliseconds of wall time (`FUN_0065968e`, `hud.md`, "A click and a double click"). Below
  10 fps a held press can pass as a click. Said at `RightClick`. Keep the tests able to set the time.
  Done 2026-10-06: `Time.WallMilliseconds` (a monotonic stopwatch; `Time.PinWall` for tests) times the quick click,
  the interface's right click and the buy panel's wait, which reads the same clock (`0x004ac438`). Predicted and read
  in Lost Kingdom, **8 of 8** (the unchanged build 8 of 8 the other way): under `pause` a 600 ms hold is no quick
  click (`tool` mode 1; unchanged, mode 0), nor an 800 ms hold in first person, and a buy row shows after its half
  second; a press held across a 450 ms stopped frame is no click. In the original the same holds across a stopped
  process kept the tool (3 of 3) and first person (2 of 2). Seventeen restored bugs each fail.
- [x] **Q123b. A left click has no time limit.** Found by Q123. The base proc makes the left button's click as it
  makes the right's: a release under 500 ms from its press, not strayed more than 6 (`hud.md`, "A click and a double
  click"). Here a left press and release on a control is a click however long it is held (`WindowStack`, the object
  window's preview among them, `park-engine.md`, "The camera goes to a thing"). **Decode first** which controls act
  on the click message `0x10006` and which on the release itself: a button's own handler may not use the click at
  all. Then build what the decode finds on `Time.WallMilliseconds`. Confirm: a press held 600 ms on each kind of
  control beside the original's.
  Done 2026-10-06, decode only: a button answers its own release and reads no clock (`FUN_00668f9c`), so it has no
  limit; a list's rows, a slider's track, the game menu's rows and an object window's preview act on the click, which
  reaches a list and a slider as `0x11006` (`FUN_0065dcaf`). In the original, 6 of 6: Buy held 1.0 s opened the buy
  screen; a visitors row held 600 ms did nothing and a 200 ms click opened the window; Resume Game held 600 ms did
  nothing, and a quick click of either button chose it. `hud.md`, "Who acts on the click, and who on the release".
- [x] **Q123c. Lists, the game menu's rows and the preview act on a press, where the original waits for the click.**
  Found by Q123b (`hud.md`, "Who acts on the click, and who on the release"). Keep the left button's record in
  `WindowStack` as the right's is kept (state, press point, stamp, on `Time.WallMilliseconds`); a list's row click and
  its double click (`0x11007`: decode what that arm does first), the game menu's rows for either button, and the
  preview's left click go through it; buttons stay as they are. A slider's track pages towards a click, if a
  slider's track is built. Confirm: a visitors row and Resume Game held 600 ms and clicked quickly, `windows` after
  each, beside the original's readings in `hud.md`; a screenshot.
  Done 2026-10-06: `WindowStack.LeftClick` and `UiControl.LeftClicked`/`LeftClickedAt`/`LeftDoubleClicked`; the list,
  the menu's rows (either button) and the preview answer them; buttons as they were. Predicted and read in Lost
  Kingdom, **8 of 8** (the unchanged build 8 of 8 the other way): a buy row held 600 ms, the screen up and `hand`
  item 0, a 150 ms click, item 1100; Publish Park held 600 ms, nothing, a quick right click, chosen; the preview held
  600 ms, the window up. In the original, new: a visitor's preview held 600 ms stayed, a 150 ms click closed it.
  A slider's track pages on the click too (tested only). A read-only review then found, and the commit builds: a list
  judges no stray, a slider's thumb leaves the stamp alone, the lobby's player slots and Quit Game are click-driven
  (Quit Game held 600 ms asked nothing in both games; a right click asked). Thirty-two restored bugs each fail.
- [x] **Q124. `Material.Default` compiles a shader nothing draws with.** Found by Q67's sweep. `Material.UI.cs` builds
  it from `content/shaders/3d.shader` the first time `Material` is touched and keeps it for the life of the process.
  Its one reader is the guard in `Material.Delete`, which can fire only if something holds it, and nothing does. The
  summaries of `Model.Delete` and `Material.Delete` say so: the park's ground, the lobby's models and the paths build
  their own `test.shader` materials, and the sea its `water.shader`. Dead by CODE: label it (rule 3). Confirm: a grep
  for readers, and `assets list` in the lobby and a park, before and after.
  Done 2026-10-06: labelled at `Material.Default`, kept. The grep finds the `Delete` guard and nothing else; `assets
  list` shows one material and its shader on `3d.shader` in the lobby (845 assets) and in Lost Kingdom (2204). A
  comment only, so "before and after" are one build.
- [x] **Q125. `CacheFileSystem` is set, and nothing in the game reads it.** Found by Q67's sweep. `Game.Run` creates
  `OpenTPW/cache` under the local application data folder on every launch and mounts it as `CacheFileSystem`
  (`Game.cs`, "mainly for editor-related stuff"). Its one reader is ModKit's thumbnail cache (`Editor.cs`), a separate
  program that never sets it and would read null. Dead by CODE in the game, and making the folder is all it does.
  Label the game's side (rule 3); the property stays, since ModKit, which is Alexah's call, reads it.
  Confirm: a grep for readers, and a launch, listing the folder before and after.
  Done 2026-10-06: labelled at `Game.Run`, kept with the property. The grep finds the setter, the property and
  ModKit's one reader; the folder was empty with the same time either side of a launch to the lobby.
- [x] **Q126. A long frame runs every thing sweep it owes, where the original runs three.** Found by Q68's review.
  The park loop counts a frame's sweeps (`[0x00879064]`, `0x0054f680`) and drops any past the third: the step and its
  counter move on, `mGameTick` does not, and nothing makes it up (`park-engine.md`, "What the 31 ms tick drives").
  `ParkPeople` runs one sweep for every eight ticks `GameClock` owes, up to its 2 s cap, so eight after a stall.
  Reachable only in a frame longer than about 0.74 s. Build the cap where the sweeps are counted, said at the site.
  Confirm: a long frame forced, and the sweeps it ran counted in the log.
  Done 2026-10-06: `ParkPeople.SweepsAFrame`, counted and dropped in `OnUpdate`. The game stopped 2.5 s, three
  times: the log's "3 run and 5 dropped in a frame of 64 ticks", `mGameTick` `+5`, `+5`, `+6` half a second on (the
  unchanged build `+10`, `+11`, `+10`); the original's, read from memory, `+5`, `+5`, `+5`. Six restored bugs each fail.
- [x] **Q127. A new guest is made at each stop in turn, where the original makes every one at stop B.** Found by
  Q68's decode (`park.md`, "Arrivals"). `FUN_004cf720` always asks `FUN_004d8650` for `BusStopB` (`0x004cf745`) and,
  while `FUN_0051aad0` reports a vehicle standing, takes two rows off the packed id (`0x004cf75c`): (53,3) in Lost
  Kingdom. `ParkPeople.StepArrivals` alternates `BusStopA` and `BusStopB` by the tick's parity, which nothing cites.
  Decode `FUN_0051aad0` first, then build it. Confirm: the `arrived at` log lines of a timed run, and a screenshot.
  Done 2026-10-06: `FUN_0051aad0` is "a vehicle is current and it is not the small crowd's". `ParkPeople.ArrivalCell`:
  the timed first load's guest at (53,5), `load 6` six at (53,5), `load 40` forty at (53,3) (the unchanged build
  half at each stop). The original's first load, thirteen guests, each first seen on (53,5). Six restored bugs fail.
- [x] **Q128. A guest going home stops at the park's edge: the stop's cells are now proven.** Found by Q68's decode.
  `PickingACellOutside` (19) and `AtTheBusStop` (21) walk to cells from `FUN_004d8650`, and `PeepBehaviour` leaves
  both unbuilt; `ParkPeople` treats 19 as the end of the walk and starts a new guest `AtGate`. The slot table proves
  the pair is `FixedItemInfo.BusStopA/B` (`park.md`, "Arrivals"), and both files say so. Check the two states' decode
  is whole, then build them and retire what `ParkPeople` says waits on Q128. Confirm: a guest who has decided to leave
  walks to the stop and is removed there; `peeps` and a screenshot.
  From Q109: a guest who leaves from inside the park now aims at the crossing's park side, (47,9) or (48,9), as the
  original's does, and is taken out there on reaching state 19: four rows short of the stop until this is built. The
  gate's two leavers (`PeepBehaviour.Judge`, `Wait`) still aim at a bus stop, where the listing sends them to the
  same `FUN_004d86d0` cells (`ride-operation.md`, "Q109"): move them with this build.
  Done 2026-10-06, the decode only (the build is Q128b): the four cells are stop A's, a pair for the bus and a pair
  two rows out for a larger vehicle; a leaver waits at the crossing while the bus loads (`FUN_0051a760`), stands at
  the stop until the vehicle's status is 4, and goes only as the head of their cell; the manager's tail summons a
  vehicle for them and sends it on when nobody waits. In the original, the park shut and 300 s watched: sixteen of
  sixteen went from `0x15` on a cell of the current vehicle's pair. `ride-operation.md`, "Q128".
- [x] **Q131. A spent vehicle is sent round again, and waits at the stop for the next load. Decode first.** Found by
  Q68b's review. `FUN_0051a690` answers a vehicle at state 6 by writing its script's variable 1 (`FUN_0055a070`,
  `FUN_0055a0b0`; decode what) and clearing `mCurrentArrivalVehicle`, and nudges nothing, so the vehicle waits at its
  last spin (`bus.RSE` 117, its object killed) until the next load's `FUN_0051a2f0` summons it, and every load has the
  drive in. Lost Kingdom's save holds its bus there, at pc 120 with `VAR_STATUS` 0. `ParkPeople.StepVehicle` releases
  state 6 and forgets the vehicle, so the bus drives back and waits at the stop at 2: in Q68b's run it stood there
  from its first circuit to the next call, and that load's guest came on the call's own sweep. While a load is held
  the original's -1 arm summons the load's vehicle by size again at once (`0x004cf489`), where `StepArrivals` asks
  vehicle 0, which `ParkFixedItems.VehicleName` answers as the bus. Confirm: the bus photographed away from the stop
  between loads, and the log's call-to-drop run-in the same on every load.
  Moved here ahead of Q128b on Alexah's word, 2026-10-06: the leavers' road cannot work while the bus stands at the
  stop.
  Done 2026-10-06, the decode only (the build is Q131b): a 6 is answered by writing `VAR_STATUS` nought and clearing
  the current vehicle, and nothing triggers the script until `FUN_0051a2f0` summons it, for a load by size or for a
  waiting leaver at random; an existing vehicle is summoned by `VAR_TRIGGER`, a new one by `VAR_STATUS` 1. In the
  original the bus's script was read through its first load: 1, 2, 3, 4, 0, 5, summoned again for a leaver and round
  once more, then not current with status 0. `park.md`, "The spent vehicle".
- [x] **Q131b. Build the spent vehicle: it stays away until it is summoned.** From Q131 (`park.md`, "The spent
  vehicle"). `ParkPeople.StepVehicle` and `ReleasesVehicle`: a 6 writes the script's `VAR_STATUS` nought and clears
  the current vehicle, with no trigger; the summons is the one place a waiting script is triggered (an existing
  vehicle's `VAR_TRIGGER`, a new one's `VAR_STATUS` 1), for a load by its size; status 4 is triggered when nobody
  waits at the stop (nobody can, until Q128b); `GATE_HURRY_FORGETS_SPENT_VEHICLE` and `BusStatus` follow. Keep the
  current vehicle as its own word, apart from the load's size, so Q128b and `ArrivalCell` can ask it. Confirm: the
  log's call-to-drop run-in the same on every load; the bus's status through a load read with `rides` or a new
  census line beside the original's sequence in `park.md`; the bus photographed away from the stops between loads.
  Done 2026-10-06: `ParkPeople.VehicleStatus`, `Summon`, `StepVehicle`. In Lost Kingdom, 4 of 4: the bus at status 0
  before the first load; summoned 1264, its guest 1300 (36 sweeps, the original's 1279 to 1315); spent 1394, 93
  sweeps after the let-go (the original's 1328 to 1421); `load 3` summoned 1499, first guest 1535. The unchanged
  build stood the bus at the stop after its first circuit. Photographed. Eleven of twelve restored bugs fail; the
  twelfth is the summons at random, unreached until Q128b.
- [x] **Q128b. Build the leavers' road to the stop.** From Q128 (`ride-operation.md`, "Q128: from the crossing to
  the stop, and out"). States `0x13`, `0x14` and `0x15` in `PeepBehaviour`: the wait at the crossing on
  `FUN_0051a760`, the four cells of stop A, the facing, the shuffle to the current vehicle's pair, and the going at
  status 4 as the head of the cell with particle `0x13`; `ParkPeople` stops taking a guest out at `0x13`. The arrival
  manager's tail in `StepArrivals`/`StepVehicle`: `FUN_0051a9d0`, the summons at random for a waiting leaver, the
  trigger at status 4 with nobody waiting and at 0 or a spent 2 with somebody. The current vehicle then outlives its
  load, so `ArrivalCell` and the gate's hurry must ask which vehicle is current (Q127's remark). Move the gate's two
  leavers (`Judge`, `Wait`) to the crossing's cells. The ferry and the seaplane are stood as the park loads and
  stand at their first spin, so a first summons sends them round empty (`ParkPeople.Summon`'s remark): make each at
  its first summons instead, as the original does, started by `VAR_STATUS` 1. Q131's spent vehicle is the same machine: read it before
  building, and build what the two share once. Confirm: the park shut with the door, `peeps` showing guests in 19,
  20 and 21 on stop A's cells and going from there, beside the original's log in `ride-operation.md`; a screenshot
  of guests standing at the stop.
  Done 2026-10-06: the three states, `MayLeaveForTheStop`, `LeaverAtTheStop`, the tail's other arm and the summons at
  random. The park shut with its door: nine set off, a seaplane and then a bus were summoned at random, and thirteen
  of thirteen went from the pair of the vehicle then current, (42,3)/(43,3) or (42,5)/(43,5); the unchanged build
  took all nine out at the crossing. Twenty-one of twenty-two restored bugs fail. Not done here: the ferry and the
  seaplane are still stood as the park loads (a first summons finds them at their first spin and sets no trigger);
  making them on demand is Q26's. The puff as a guest goes is counted.
- [x] **Q129. The crowd sets the music's level every frame, where the original sets it once a second.** Found by Q68's
  review. The park loop reaches `FUN_0051e790` only on every 32nd tick (`TEST [0x00877d34],0x1f`, `0x0054f82d`) and
  clamps the crowd's level, half its count, to 89 before it (`0x0054f84e`), which binds from 180 guests; the park holds 1,500.
  `ParkAudio` asks every frame, and does not apply the clamp, as its
  comments say. Build the cadence and the clamp. Confirm: the
  level's changes counted over a timed run, and guests added with `load` past 180.
  The same call also sends nought while `mWorldState` (`+0x1da738`) is 4 (`CMP [EDX+0x1da738],EBP` at `0x0054f860`,
  `EBP` set to 4 at `0x0054f4ba`; `scenes.md`, the `FUN_0051e790` row), which `ParkAudio` does not do either (the
  2026-09-26 staleness audit): build it with them.
  Done 2026-10-06: `ParkAudio.MusicLevel` (held to 89, nought in world state 4) and `SetsMusicLevel` (every 32nd
  tick). In Lost Kingdom: level 6 with thirteen guests, 20 sets in 646 ticks; `load 190`, the level 89 from 179
  guests and still 89 at 203 (the unchanged build: every frame, 96 and 100). Nine restored bugs each fail. Not
  measured in the original (the level is inside the sound library's voice).
- [x] **Q130. The staff pool never refreshes, and nothing counts it. Decode first.** Found by Q68's review. Every
  sweep the original runs `FUN_005084f0` (`0x004d7b30`), which drops a candidate left in the pool longer than
  `StaffTimeoutTime` plus up to half again, and every `TimeBetweenStaffUpdates` tops the pool up by at most
  `MaxNumberOfStaffPerUpdate`, both keys counted in fours of sweeps, as the arrival timer's is (`FUN_0041a970`
  against the key times four, `0x0050850f`, `0x00508549`; the lifetime drawn at `0x005077b9`).
  `ParkStaffPool` fills the opening pool only and reads none of the three keys, and no `Unimplemented.Report` says so
  (`CLAUDE.md` rule 4). Count it now; decode the refresh (`FUN_005084f0`, `FUN_00507600`), then build it on Q68b's
  counter. Confirm: the hire screen's candidates over a timed run, a screenshot before and after a refresh.
  Done 2026-10-06, the decode and the count (the build is Q130b): `FUN_005084f0` drops a candidate older than four
  times its lifetime in sweeps and tops the pool up every `TimeBetweenStaffUpdates * 4` sweeps by at most
  `MaxNumberOfStaffPerUpdate`, weighted by what each kind wants, then makes up the minimums. In the original, the
  pool read from memory for 240 s: 24 of 24 went on the first sweep past four times their lifetime, and the pool was
  topped up by ten on `mGameTick` 1083 and 1444, 361 apart. `ParkStaffPool.Sweep` counts the turn
  (`STAFF_POOL_REFRESH`). `park-engine.md`, "The staff pool's refresh".
- [x] **Q130b. Build the staff pool's refresh, and read the save's pool.** From Q130 (`park-engine.md`, "The staff
  pool's refresh"). `ParkStaffPool.Sweep`: a mark and a lifetime on every candidate (the lifetime a draw modulo half
  `StaffTimeoutTime` plus it), the drop past four times the lifetime unless the candidate is in the hand, and the
  top-up every `TimeBetweenStaffUpdates * 4` sweeps on `ParkState.GameTick`: the budget, the staff counted in the
  park, each kind's want, the weighted draw, then the minimums' pass in its order. The hire screen's list must lose
  and gain rows as it is open. Read the save's own pool (32 records at the world's start, with their marks and
  lifetimes, and the pool's mark at `+0x294`) in place of rolling an opening one where a save holds one; a fresh
  park still rolls. Confirm: the `candidates` census over a timed run beside the original's log (drops at four
  times the lifetime plus one, top-ups 361 sweeps apart, of ten in Lost Kingdom); a screenshot of the hire screen
  before and after a refresh.
  Done 2026-10-06, the refresh (the save's pool is split off as Q130c: it needs the save reader and the FileFormats
  page): `ParkStaffPool.Sweep` drops and tops up on `ParkState.GameTick`, and the hire list follows it. In Lost
  Kingdom: the opening twenty-two, made on 755, each went on 755 plus four times their lifetime plus one; topped up
  on 1116 by two and on 1477 by ten; the unchanged build kept the same twenty-two 210 s on. The hire screen
  photographed with five, six and one cleaner. Twenty-three of twenty-four restored bugs fail.
- [x] **Q130c. Read the save's staff pool.** From Q130b. The save holds the pool: 32 records of 20 bytes at the
  world's start, which `ParkWorld`'s reader skips (`PoolRecords`, `PoolRecordSize`), each a kind, a name's row, a
  grade, a costume, the occupied and taken bytes, the `mGameTick` it was made on and its lifetime; and the pool's own
  mark (`+0x294` in memory: find it in the file). Read them, write the layout to the FileFormats saves page
  (`CLAUDE.md` rule 14), and give a loaded park that pool in place of a rolled one; a fresh park still rolls.
  Confirm: Lost Kingdom's hire screen showing the save's fourteen candidates, the first drops on `mGameTick` 926,
  934 and 946 and the first top-up on 1083, as the original's log has them (`park-engine.md`, "The staff pool's
  refresh"); a screenshot beside the original's hire screen.
  **Done 2026-10-06**: `ParkWorld.StaffPool` and `StaffPoolTimeSig`; `ParkStaffPool` takes a save's records. The file
  holds sixteen, not fourteen (two go on 858, before the original's log began). Every drop to 1403 and both top-ups
  on the original's ticks; the hire screen beside the original's (`park-engine.md`, "The staff pool's refresh").
- [x] **Q234. One staff test fails now and then in the whole suite.** Found by Q123's gate (and once unnamed at
  Q233's). `ParkStaffBehaviourTests.TheParksSweepHandsTheStaffItsOwnClock` failed 1 of 7 whole-suite runs with the
  game, alone in a worktree, and 0 of 40 runs of its class alone, so something another class leaves behind reaches
  it, or a draw it rests on is not seeded. Again at Q123b's gate, 2 of 9 runs by then: "Assert.IsTrue failed. staff 30
  ended only 2 walks in 400 sweeps" (line 500). Find what it reads that is shared (the world's random, `GameClock`, `Time`), and pin it. No game run.
  **Done 2026-10-06** (the review's fix 6): nothing shared reaches it; the draws were not seeded. How long a staff
  walk lasts is drawn, and of 300 seeds two leave the guard or the researcher with under three ended walks in 400
  sweeps, the failure quoted. Seeded, the two members' whole 400 sweeps are the same alone, in the class and in the
  whole suite (`GameClock` at 0 or at 137,495, a staff pool left behind or none), so the test now seeds all four of
  the park's generators. The "0 of 40 alone" against "2 of 9 in the suite" was chance on a rare draw.
- [x] **Q235. A walk stops short of its aim. DECODE FIRST.** Found by the review's fix 1 (`docs/exe/park.md`,
  "From the stop to the booths"). In the original the thirteen guests of a load, aimed by `FUN_004fa5f0` at points up
  to 245 of 256 across their roadside cell and walking in from the east, all stood on the cell aimed at
  (`~/.cache/tpw-harnesses/rv1/orig/a.log`). Here a walk ends up to about a sixth of a cell short: guest 43, aimed at
  (47.89,5.78), stands on (47,5) in the game but on (48,5) in `ParkTickTests`' sweeps, and of 30 leavers' walks into a
  stop's second cell 7 ended one cell east and were sent again
  (`~/.cache/tpw-harnesses/review-run-2026-10-06/game/lean-probe.txt`). Decode where `FUN_004fa2a0` and the mover
  count a walk done (`0x00510100` on) against `PeepWalk.Step`'s `Progress() == One`, write it to `docs/exe/` and
  stop. Two tests allow the cell beyond until then (`ParkTickTests`, `ParkDecidingTests`).
  **Done 2026-10-06 (decode): there is no difference.** The original ends a walk as `PeepWalk.Step` does: the
  finished byte, set before the move inside 1.6 radii, then one slowed step (`ride-operation.md`, "Where a walk ends,
  measured"). In the original the thirteen ended 0.019 to 0.158 of a cell short, and guest 53, aimed at (47,5),
  stands on (48,5) (`~/.cache/tpw-harnesses/q235/orig/a.log`). The premise came from a wrong draw order: Q235b. The
  two tests' allowance stays, as the original's behaviour.
- [x] **Q235b. A new guest's walk in is aimed by the wrong two draws.** Found by Q235. `PeepBehaviour.WalkInDraws`
  takes the fourth draw from the id for the roadside cell and the fifth for the place across; the original's are
  the second and the third, thirteen of thirteen on both by the walkers' own targets (`docs/exe/park.md`, "From the
  stop to the booths"; `~/.cache/tpw-harnesses/q235/orig/a.log`: guest 43 is aimed at (47.742,5.781), not
  (47.895,5.781)). Take the two draws after the child's bank, and correct the remarks and `ParkPeopleTests` that
  say fourth. Confirm: guest 43's aim in the `peeps` census, predicted first, and a screenshot of them at the
  roadside.
  Done 2026-10-06, `alexah/330-walk-in-second-and-third-draws`: one draw skipped, not three. Guest 43's aim
  **predicted (47.742,5.781), read (47.742,5.781)**, twice; the build before read (47.895,5.781), as predicted. It
  stands at (47.756,5.781), photographed at the roadside about a fifth of a cell west of the build before's. The
  first run's standing spot was 0.001 outside the band I had predicted (0.014 short of the aim; the original's
  thirteen were 0.019 to 0.158 short), and its photograph had the bus in front. The test's oracle is now the
  original's thirteen logged aims (`q235/orig/a.log`), cell and place; the fourth and fifth draws, and the first and
  second, each put back, fail it (`~/.cache/tpw-harnesses/q235b/`). Not compared afresh with the original: its
  load makes other ids (38, 33, 44 to 54), never 43, so 43's aim is the generator's, checked on those thirteen.
- [x] **Q237. One Full Simulation test failed twice with no message kept.** Found by the 2026-10-06 review (fix 6).
  `LevelFullSimulationParkTests.FullSimulationCreatesTwelveThingsWithRegularLoans` failed once in the review's
  put-back runs and once at a gate, about two whole-suite runs in a hundred and twenty, and neither output was kept.
  Nothing it reads is drawn (`FreshPark` takes no generator; its `RandomSeed` is the clock's and nothing in the test
  reads it), and forty whole-suite runs after Q234's fix were green (`~/.cache/tpw-harnesses/rv6/loop.log`). When it
  next fails, keep the run's output first: the loop in `rv6` saves any failing run whole. No game run.
  From Q235b: `ParkTickTests.TheBusIsHeldAtTheStopUntilTheSweepAfterItsLastGuest` failed once in 25 runs of its
  class beside `ParkPeopleTests` ("Expected:<0>. Actual:<1>. so the bus is NOT let go on the last drop's sweep"),
  never alone. Its `ParkPeople` takes no generators, and a leaver standing at the stop triggers a bus at 2 with
  nobody left to drop (`StepVehicle`): a likely cause, not shown.
  **Done 2026-10-06.** The bus test's cause is shown and mended; the Full Simulation test's is not found. **The bus:**
  by the draw a saved guest (39, once 32) goes home and stands at the stop before the load is called on 1264, and the
  manager's tail answers them: a bus at 2 with its load off is sent on (the message quoted), or another vehicle was
  summoned first ("Expected:<1>. Actual:<0>. and the waiting bus summoned on the same sweep", the one failure in
  180 whole-suite runs of the build before, kept whole: `~/.cache/tpw-harnesses/q237/suiteB-fail-20.log`). Seeding
  alone did not pin it: guests take turns on `GameClock.Ticks / 8` (Q132), which no scene entry resets, so one seed
  passed or failed by the tests run before it. With the clock set to nought too, seven of seeds 0 to 599 put a guest
  at the stop, the same seven on a second pass. The test now seeds all four generators, pins the clock and asserts the
  stop empty, and `TheBusHandshakeNamesALeaverAtTheStop` holds one of the seven to that message. Four bugs put back,
  four caught (a failing seed, the assertions out, the generators unseeded, the clock unpinned). **The Full
  Simulation test** did not fail in those 180 runs (three at a time for 120 of them), nor in 150 runs of the two
  classes; at two in 120 that is about one chance in twenty of seeing none. Read and ruled out: no test runs in
  parallel, no generator or clock reaches `FreshPark`, the catalogue or `ParkState`'s two answers, the path art is not
  drawn, the archives are read into memory, and the test host held 200 file handles of a million. The one trace of an
  old failure is its duration, 121 ms (a passing run takes about 200). Nothing was changed for it. If it fails
  again: `~/.cache/tpw-harnesses/q237/loop.sh NAME COUNT` keeps a failing run's whole output.
  The mechanism in the running game, predicted first (Q128b's `confirm.py`, `q237/game`): the park shut, guests
  stand in `AtTheBusStop`, four summonses "at random" answer them and 13 go from the stop; photographed.
- [x] **Q238. Seven leads from the 2026-10-06 review that nobody checked.** They were low and unverified, so they
  were left off its list (`~/.cache/tpw-harnesses/review-run-2026-10-06/LEDGER.md`, the lines marked OPEN, "lead" or
  "not checked", and what fixes 8 and 9 counted without a queue item). Check each first-hand; then mend it, say it
  at the site, or strike it here:
  (a) whether guests at the stop go in the original's order: a newcomer heads its cell's list and the sweep visits
  guests by descending id, which reproduces Q128's log exactly; nobody has compared `ParkState`'s head and the
  sweep's order with it;
  (b) the buy screen's own 1000 ms timer (`0x80080`, `0x004ac3ee`), not decoded, against a balance rewritten every
  frame here;
  (c) the three callers of the timers' hold that are not identified (`FUN_0048a6e0`, `FUN_0048a720`,
  `FUN_005f0b40`; `hud.md`);
  (d) a hired member of staff keeps no name here, so a new candidate can be given one in use in the park
  (`STAFF_NAME_IN_USE_IN_THE_PARK`);
  (e) the leaver's stay and the analyser's ring of fifty are counted with nothing built (`LEAVER_STAY_SAMPLE`): what
  sets a guest's `+0x204`, and who reads the history ring at `+0x21164`;
  (f) `ParkSweepCapTests`' remarks name a bug that was never put back, and one of Q123c's tests stages a state the
  original cannot reach;
  (g) Q125: the cache folder's `CreateDirectory` runs before the renderer and can stop the boot for a folder nothing
  reads.
  No game run for (c), (f) and (g).
  **Done 2026-10-06.** Each read first-hand; five mended, two said. (a) A difference: the original links a new
  thing in at the head of the used list (`FUN_00516270`) and the sweep walks from the head, so the newest goes
  first; `ParkPeople` appended. Now a new guest and a hire head their lists. Measured in the original: 54, 53 ... 43,
  38, 42 ... after the first load. In the game, predicted first: six guests made stand first in `peeps`, 48 to 43,
  and every tick's leavers go in descending id (20 gone; the build before logged 38, 33, 48 on one tick). (b) The
  timer redraws the list and letters the corner "Cash  $ " and the balance (UITEXT `0x1ca`, `0x1cb`) once a second;
  built, read with `buymoney` and photographed beside the original's. One prediction was wrong: the original's
  corner is lettered in its first frame, not a second on: its opener letters it too (`FUN_004acc70`, `0x004acdae`, found by the review), and the build follows. (c) The map
  (`WindowStack.HoldsTimers` has it now) and the pause overlay's two doors, the overlay unbuilt and the focus's
  leaving counted (`FOCUS_LOSS_PAUSE_OVERLAY`); `hud.md`. (d) `Staff.Name`: the save's `mName` is text (87 staff
  in eleven saves), a hire keeps the candidate's, the pool asks the park, and the staff screen shows it ("Duke
  Mighten" predicted and photographed). (e) `+0x204` is `mPaidAdmission`, set at the gate; the history's readers
  are the park status screen's graph; the count now asks the flag. (f) The cap's "owed to the next frame" put back:
  caught; the double click's test restaged with the move that selects. (g) The cache folder's failure is logged
  and the boot goes on (run with the folder unmakeable). Sixteen bugs put back, sixteen caught (`q238/mutate.py`).
- [x] **Q236. The crowd's own voice and `FUN_0055ab50` are counted, not built. DECODE FIRST.** Found by the
  2026-10-06 review (fix 4). On every 32nd step, after the music's level, the park loop hands kids 91 the guests
  within four cells of the cell at `[0x007b05cc]`, held to 100 (`FUN_004c8d30`, then `FUN_0051e7b0`: it plays the
  effect from `[0x00803a24]` while `[0x00803aa8]` is set, sets its parameter 7 and stops it at nought), and calls
  `FUN_0055ab50`, which scales four words at `0x007660b8` (`park-engine.md`, "The music's level"). OpenTPW counts both
  (`CROWD_VOICE_LEVEL`, `PARK_LOOP_FUN_0055AB50`) and plays no crowd. Kids 91 is of the music's class: twelve
  variations in bands of 16, the first six naming controller 7 over a volume range of 14 to 22. Decode what that
  controller does to the volume (`FUN_006bc090`), which cell `[0x007b05cc]` holds, and what the four words feed.
  Confirm: the crowd found in a capture beside the original's (`rv4/orig/xcorr.py` is the method), near a crowd and
  away from one.
  **Done 2026-10-06 (decode only).** `audio.md`, "The crowd's voice"; `park-engine.md`, "The music's level". The cell
  is the one under the pointer (`FUN_0045d560`); the level picks the variation by bands of 16 and is the volume of
  variations 1 to 6, `level × 8 / 100 + 14` of 100; the four words are the flying cars' rectangle. In the original
  (memory, 75 s, `q236/orig/a.log`) the level word followed the count of guests within four cells of the pointer's
  cell, 12 over the Belly Bounce's queue and 0 on empty ground, predicted first. No capture made; the build is Q236b.
- [x] **Q236b. Build the crowd's voice.** From Q236 (`audio.md`, "The crowd's voice"). On the music's beat, count the
  guests on the cells within four of the cell under the pointer (a 9 by 9 square cut at the map's edge, nought with
  no cell), hold it to 100, nought in world state 4; above nought play kids 91 flat as a chain of the music's class
  whose parameter 7 is the count (the variation by the zones, the volume `level × 8 / 100 + 14` on variations 1 to
  6 and a draw from 14 to 21 on the twins, the pitch a draw from 0 to 5), start it again if it ends, and fade it out
  at nought. Where `CROWD_VOICE_LEVEL` is counted; `PARK_LOOP_FUN_0055AB50` stays counted until something flies.
  The gain past the variation's volume is not decoded: set it by measurement. Confirm: the crowd found in a capture
  beside the original's (`rv4/orig/xcorr.py` is the method), with the pointer over the Belly Bounce's queue and over
  empty ground, the level predicted first and read in the log; a screenshot of each.
  **Done 2026-10-06** (`ParkAudio.SetCrowdVoice`, `NextCrowdSample`, `ParkPeople.GuestsNear`; console `crowd` and
  `bus`). In the game, predicted first, 4 of 4 (`q236b/fix`): over the queue level 13 from 13 guests, held, 70
  samples of kids 91 found in 30 s of the mix at a median gain of 0.0218; over empty ground level 0, not held, none
  found; the build before finds none (`q236b/control`, 2 of 2). In the original (`q236b/orig`): level 9 over the
  queue, 53 samples matched at 0.6 or better, all six pitches; level 0 over empty ground, one. The gain is 0.30, from
  the original's crowd at about 0.12 of its music (0.09 to 0.17). 27 bugs put back, 27 caught. Found on the way:
  `Voice.SetVolume` with no time never arrived (mended). Not explained: the original's samples differ in gain, one
  from another, 0.022 to 0.046 (`audio.md`).
- [x] **Q130d. The hire list stands in name order in the original. Decode first.** From Q130c. On four tabs the
  original lists its candidates by name (Chris Battson above Rajan Tande, who holds the earlier slot); OpenTPW's
  `ParkHireScreen` lists them in the pool's order. Decode what orders the list (`FUN_00481550` adds a row;
  `UiList.Insert` already models the sorted insert `FUN_0066403b`) and whether a heading's click re-sorts it. Confirm
  beside the original's screen, whose reference install reads the `american` name tables. Q130c's review: the
  list's builder is `FUN_0049b5b0`, which adds rows in slot order with the slot as the row's key; the pool here
  keeps no slots (`ParkStaffPool.Candidates`), which matters if the order turns out to be the slots'.
  **Done 2026-10-06 (decode only).** `hud.md`, "A list's order". The list orders itself: every add is a sorted
  insert on the list's own column and direction, the hire screen's word `[0x007523e4]` is 1 (the name, ascending),
  and a heading's click flips or changes it and re-sorts. In the original, predicted first (`q130d/orig`): the word
  read 1, then -1, 2, -2 on three heading clicks with the rows following, kept across a tab and a reopening; all five
  tabs in name order, four not in slot order. Nothing built; the build is Q130e.
- [x] **Q130e. Build the lists' order: the sorted insert on the hire and buy lists, and a heading's click.** From
  Q130d (`hud.md`, "A list's order"). Done 2026-10-07: `UiList` keeps the sort, `Add` is the sorted insert, a
  heading is a button that re-sorts; hire, buy and visitors keep their word for the session. In the game, predicted
  first: 21 of 22 (five tabs in name order, words 2, 2, -2, 1, -1 on the entertainers with the ties as predicted,
  the buy list -1, 2, -2, 3); the one miss was mine, the features tab's names, which the `american` tables give the
  original differently. Beside the original under Proton on the save's own sixteen candidates: the same rows at
  every step. 28 bugs put back, 28 caught.
- [x] **Q132. Guests and rides take their turns on the game clock over eight, where the original hands them
  `mGameTick`. Decode first.** Found by Q68b. `ParkPeople.OnUpdate` hands `Peep.Tick`, `PeepBehaviour.Step` and the
  rides' turns `GameClock.Ticks / 8`, which runs from the program's start and is not reset on entering a park
  (`PeepBehaviour.Step`'s `tick` note); the original's handlers read `mGameTick`, which `ParkState.GameTick` now
  carries from the save's 755. The needs share `(id & 3) == (tick & 3)`, the behaviours' time stamps and the chooser's
  tie on `mGameTick & 1` (`ParkRideChooser.Beats`) turn on it. Decode which of them read `mGameTick`, then pass the
  park's clock, as Q82b did for the staff. Confirm: a saved guest's stamp read against 755, in the `peeps` census,
  predicted first.
  Q82 found the guests' needs gate reads `mGameTick & 3` (`FUN_00501650`, `0x00501669`), as `ParkPeople.OnUpdate`'s
  thing-tick note says.
  From Q126: a dropped sweep makes the handed tick jump (n, n+1, n+2, then n+8 after a 64-tick frame), so the
  guests of one needs slot miss a turn the original's `mGameTick` gives them on its next sweep, and stamps taken
  from it jump five sweeps.
  From Q237: because that clock is never reset, a test with every generator seeded still plays out by how many
  ticks the tests before it ran (`ParkTickTests.PinTheClock` sets it to nought for one test). On the park's clock a
  seeded run would repeat by itself.
  **Decoded 2026-10-07** (`ride-operation.md`, "The guests' and the objects' clock"): every one of the guests' 18
  reads and the objects' 8 is `mGameTick`, of 85 in the executable; none of their code reads the 31 ms counter or
  the millisecond clock. In the original, five predictions of five: the thirteen saved guests' `mArrivalDate` 648 to
  660 against 755 (the file's bytes agree), 514 stamps all the sweep's tick, 8,654 exit-level falls all on the
  guest's own sweep in four, the toilet's drift only on `& 0xf` nought and only for ids that divide by four. Today's
  build: stamps near 200 beside a park clock of 997. The build is Q132b.
- [x] **Q132b. Hand the guests and the rides the park's clock, and read the save's three stamps.** Decoded by Q132
  (`ride-operation.md`, "The guests' and the objects' clock"). Pass `ParkState.GameTick` to `Peep.Tick`, `DueOn`,
  `PeepBehaviour.Step` and `TakeTheRidesTurns`, and to the six callers that work the frame clock out themselves
  (`Admit`, `AdmitAsEntered`, `SendAsChosen`, `ThingRemoved`, `QueueRemeasured`, `WhyCensus`, which hands on the
  31 ms tick undivided). `ParkRideOperation.SpriteClock` stays on the frame clock: it is a sprite's milliseconds.
  Read `mArrivalDate` (`+398`), `mTimeOfLastSpotAnim` (`+513`) and `mTimeStartedIdling` (`+517`) in
  `ParkWorld.ReadGuest`, keep the arrival on the guest and stamp it at making (`0x004fafcf`); print all three in
  `peeps`. `ParkTickTests.PinTheClock` and the comments that name Q132 go with it. Confirm: `peeps` on entering Lost
  Kingdom, the thirteen saved guests' arrival 648 to 660 beside `sweeps`' 755 and a queuer's idle stamp within 30
  of it, predicted first; the falls of `exit` on each guest's own sweep in four; a screenshot. Beside the original:
  `q132/orig/a.log` holds its run, `clock.py` reads it again.
  **Done 2026-10-07.** In the game, predicted first (`q132b/confirm.py`): on mGameTick 755 the thirteen saved guests'
  `arrived` 648 to 660 in id order; eight single sweeps, 104 checks, 26 falls of `exit`, none off the guest's own
  sweep in four (the build before: 52 off); 60 s on, on 1004, idle stamps 820 to 986 (before: 60 to 208); a guest
  made stamped 1004. 3 of 4: the miss is this item's own "within 30", which is not the original's either (its
  queuers stood 11 to 169 behind at that reading; a queuer's stamp is the sweep they last took a place on).
  `PinTheClock` is gone: the bus test's seeds come out the same wherever the frame clock stands (five of 600).
  18 bugs put back, 14 caught, four equivalent (`q132b/mutations.txt`). 2052 tests.
- [x] **Q133. The mechanic, the handyman and the entertainer stand once their saved walk ends, where the original's
  walk about.** Found by Q82 (`ride-operation.md`, "Leaving idle, or a walk: the choice by kind"). With no work the
  mechanic's `FUN_004da5b0` and the handyman's `FUN_004d7100` take a random walk every time (`0x004da6fa`,
  `0x004d712d`), and SetState(0) only when none is found; the entertainer's `FUN_004d46d0`, after a draw mod 3 and no
  guest within `ActivationDistance`, takes the guard's `mGameTick & 3`. `StaffBehaviour.Decide` stands all three, and
  none of their searches is counted (`CLAUDE.md` rule 4): a broken ride, litter, a loo, guests to perform to.
  From Q100: the handyman's loo search and his states `0xa` and `0xb` are decoded (`ride-operation.md`, "A toilet's
  dirt"); building them belongs with this decide.
  Build the no-work walk and count each search where the original makes it; every kind's decide calls
  `FUN_00506a40` first (`0x004da5b8`, `0x004d7108`, `0x004d46d5`), as `StaffActivity.Idle` says. `Decide`'s summary
  and the class remarks already say the original's three walk about (`e0462c9`). Confirm: all five staff walking in
  a timed run, the `staff` census and `unimplemented`, photographed.
  **Done 2026-10-07.** `StaffBehaviour.Decide` is each kind's own: the mechanic and the handyman walk on every
  decide, the entertainer on `mGameTick & 3`, every kind asks the tired arm first, and `MECHANIC_RIDE_SEARCH`,
  `HANDYMAN_LITTER_SEARCH`, `HANDYMAN_TOILET_SEARCH` and `ENTERTAINER_GUEST_SEARCH` are counted. In the game,
  predicted first (`q133/confirm.py`): over 150 s and 330 readings from mGameTick 775 on, the handyman and the mechanic Walking on every one, on 39 and 45 cells (the build before: Idle on all 337, one cell each); the entertainer Idle and Walking, its fourteen idle stamps each a multiple of four; the guard and the researcher on 56 and 35 cells; counted 48, 51, 51 and 17; each of the three photographed somewhere else eight seconds on. 4 of 5, the miss mine: one pre-step look on 770, which the handyman, never idle now, does not make. **Beside the original under Proton**, 3 of 3 predicted
  (`q133/orig/a.log`): its handyman and mechanic in state 1 on all 578 sweeps, its entertainer idle only from a
  multiple of four (6 of 6) and performing on 382. 17 bugs put back, 17 caught (`q133/mutate.py`). The work itself was split off: Q133b and Q133c.
- [x] **Q133b. The handyman never cleans a toilet.** Split from Q133, which built the walk and counted the search
  (`HANDYMAN_TOILET_SEARCH`). The search `FUN_004d7880`, his states `0xa` (to a loo) and `0xb` (cleaning) and the
  clean `FUN_004dfd80` are decoded (`ride-operation.md`, "A toilet's dirt"): build them where the search is counted,
  with `mAssignedStaffMember` and its 100-tick forgetting. The request for service (`+0x64`) has no control here:
  count it. Confirm: a toilet dirtied by sixteen uses (`q100bconfirm.py`'s way) with the handyman in range, `staff`
  showing him walk to it and clean for 11 sweeps, `objects` showing `repair 100` and its queue taking guests again;
  a screenshot; the same in the original under Proton.
  **Done 2026-10-07.** Built: `StaffBehaviour.FindToilet`, `ArriveAtTheLoo`, `CleanOn`, `ParkRideOperation.Clean`.
  In the game, predicted first, 8 of 8: put down on (56,19) on mGameTick 1865, found toilet 21 on 1866, cleaning from
  1880, finished on 1891 (S + 11), repair 20.95 to 100, two guests after 100 to 95 to 90; the build before 3 of 3
  (no walk, dirty to the end). The original, 4 of 5: `0xb` on 1398, done on 1409, repair 100. 41 bugs put back, 41
  caught. Found: the stock park's handyman is never in range of a toilet (`ride-operation.md`, "A toilet's dirt").
  The request's control: none here for a toilet; the search and the clean read and clear a saved `+0x64`.
- [x] **Q133c. The entertainer never performs. (The decode.)** Split from Q133, which counted the look (`ENTERTAINER_GUEST_SEARCH`).
  On a decide's draw mod 3 of nought, `FUN_004c8eb0` (`FUN_004c8d30` with a last argument of 1) looks for a guest in
  the square of `EntertainerConstsPerGrade.ActivationDistance`; one found draws again for animation `0xd` and up
  (`FUN_00541fa0`), state `0xe`, `+0x214` = mGameTick, for WorkDuration + 1 sweeps (51 at grade 3), each turn through
  `FUN_00506760`, then effect `0x87` and the decide again (`ride-operation.md`, "The jobs, on the same clock" and
  "The no-work walk, in both games"). Decode `FUN_004c8d30` first. The original's entertainer performed on 382 of 578
  sweeps (`q133/orig/a.log`). Confirm: `staff` showing state `0xe` for 51 sweeps beside a guest, predicted first; a
  screenshot of the performance beside the original's. Q227 follows it.
  **Decoded 2026-10-07** (`ride-operation.md`, "The entertainer's performance"): the look answers yes or no and keeps
  nobody; a second draw picks among the bank's state groups, one in every jungle bank, so always animation `0xd`,
  the bank's set 4 looped by script word 1760. In the original, predicted first, 5 of 5: 15 starts each with a guest
  in the nine by nine, 11 spells of 51 sweeps exactly, 682 of 682 readings on set 4. The build is Q133d.
- [x] **Q133d. Build the entertainer's performance.** Decoded by Q133c (`ride-operation.md`, "The entertainer's
  performance"). In `StaffBehaviour.Entertain`, where `ENTERTAINER_GUEST_SEARCH` is counted: the look (any guest on
  the cells within `ActivationDistance` each way, which nothing reads from the balance file yet), the second draw,
  state `0xe` stamped inline (no idle stamp), 51 work turns at grade 3 (`Work`'s cost), effect `0x87` counted (Q135),
  and the decide again in the same turn. The picture is the bank's own: read the four groups at `.ESP` `0x14e`, give
  `SpriteScript` words 1726 to 1783 and their five opcodes, and loop set 4. Confirm: `staff` showing `0xe` for 51
  sweeps beside a guest and never without one in reach, predicted first; a screenshot of the performance beside
  `q133c/orig/sheet-performing.png`. Q227 follows it.
  **Built 2026-10-07** (`ride-operation.md`, "The performance, in both games"): `StaffBehaviour.Perform`, state
  `0xe` inline, the bank's state group and script words 1726 to 1781. In the game, predicted first, 7 of 7 (5 of 7
  the first run, both misses the harness's): 15 performances in 240 s, each with a guest within four cells, 14 of 14
  finished spells 51 sweeps, 406 of 406 readings on set 4 at script 1760; the build before never performs, 2 of 2.
  Away from guests no performance began (6 put-downs, where 8 were predicted). 46 bugs put back, 44 caught, the two left an equivalent and a save field the shipped save holds nought in (`q133d/mutate.py`).
- [x] **Q134. The researcher researches, where ours stands. Alexah's call first.** Found by Q82. On a nought from its
  draw, or no destination, the researcher takes state `0xf` (animation 10, `+0x214` = mGameTick) for
  `ResearcherConstsPerGrade.WorkDuration` + 1 sweeps (31 at grade 2), then walks or researches again; it never idles of
  its own accord, and every 20 sweeps it adds `ResearchAbility` to the lab (`0x00502984`). OpenTPW has no state `0xf`,
  so the fourth decide stands. Research waits on a research system this game lacks (`docs/PLAYER-GAPS.md`), but the
  state and its timer need no lab: ask whether to build that half now, and count the points meanwhile.
  - **Note (fork review, 2026-09-30), data for the build:** the global `Standard.sam` holds Research Effort by category (100, 15, 30,
    15, 10) and `ResearchTech`; `Easy_Standard.sam` overrides categories 3 and 4 (25 and 0). `Upgrades[n].CostOfResearch`
    is research points; `DurationOfUpgrade`, only in the category files, is the mechanic's upgrade time. In Instant
    Action the research screen shows UITEXT `0x1d4`, "research is automatic". Why the screen has six sliders for five
    categories is not decoded: read how the lab spends points by category first. Review items economy-1, economy-v3.
  **Built 2026-10-07, at Alexah's word ("build it now")** (`ride-operation.md`, "The research, in both games"):
  `StaffBehaviour.Research`, state `0xf` by three inline writes, its turn and its end, the save's stamp (697) read;
  the lab's points counted, `RESEARCH_POINTS_TO_THE_LAB`, and spent by nothing. In the original, predicted first, 5 of
  5: eight spells of 31 sweeps, never idle. In the game, predicted first, 7 of 7: twelve spells in 240 s, each 31
  sweeps and ended in a walk, 199 of 199 readings on script 402 set 4, never Idle, fifty lots of 12 points; the build
  before stands on 168 readings of 596, 2 of 2. 43 bugs put back, 41 caught; the two left read another kind's `+503` as the researcher's stamp, nought in every other member of the shipped save (`q134/mutate.py`). The lab itself, and the research
  screen, are still not built (`docs/PLAYER-GAPS.md`); the note above is its data.
- [x] **Q135. The staff's sounds are neither played nor counted.** Found by Q82. Every idle and walking turn draws
  the world random and on one in sixteen plays a cat_staff effect at the member's position (`FUN_004faa00`): idle
  `0xa1`, `0xa3`, `0xa5`, `0xa7`, `0xa9` and walking `0xa0`, `0xa2`, `0xa4`, `0xa6`, `0xa8` (handyman, mechanic,
  entertainer, guard, researcher), `0x8a` a researching turn; and with no draw `0x87` a performance's end, the guard's
  `0x88` (`Oi.mp2`) as a chase starts and `0x89` on a catch, both waiting on the chase, itself unbuilt. Count them first
  (`CLAUDE.md` rule 4); then decode each effect's samples and build. What a sample says is known only by listening.
  Confirm: `voices` and `unimplemented` over a timed run.
  **Built 2026-10-07** (`ride-operation.md`, "The staff's sounds, in both games"; `audio.md`, "The staff's voices"):
  `StaffBehaviour.DrawForSound` on every idle, walking and researching turn, the end's `0x87` with no draw,
  `ParkAudio.StaffSound` (the variation's own volume and pitch, placed). In the game, 240 s, predicted first, 6 of
  8, both misses the audio measure's: 4,262 draws and 258 drawn sounds (0.0605), each its member's own effect; 276
  voices for 276 sounds; `0x87` on each of thirteen ends and `TADA.mp2` in the mix at all thirteen;
  `STAFF_SOUND_PERFORMANCE_END` gone. The build before: none, 3 of 3. In the original: `TADA.mp2` in its own mix
  on five of ten ends, the kinds' voices not found for want of an instrument. The samples carry no words (an
  offline transcriber's reading; no person listened). `0x88` and `0x89` wait on the guard's chase, which has no
  site here to count them at. 41 bugs put back, 41 caught, one of them by the game run alone.
- [x] **Q136. Six small differences in the staff's decide.** Found by Q82 (`ride-operation.md`, "Drawn on the way").
  (a) Tired is `(u8)trunc( rest ) <= RestLevel`, signed and inclusive (`0x00506b41`); `StaffBehaviour.Decide` tests
  the float `< RestLevel` and misses [1, 2). (b) The patrol roll `FUN_00506f30` takes only a path cell (`mType` 1,
  `FUN_00536310`) before it routes; **built in Q206**, with queue/approach wandering containment. (c) Not
  tired, `FUN_00506a40` sets the speed word `+0xc0` from the rest byte (60 to 140, `[0x0075c7f8]`), one of
  `FUN_004fa870`'s three terms. From Q177d: how the terms reach the walk is decoded (`ride-operation.md`, "Where a
  WALKING peep is drawn") and every person's four speed words are read (`ParkWorld.PaceState`); guests are eased
  (`Peep.Pace`), staff keep the saved speed and a hire a rested member's 1.4. The build is `Pace` for staff, with this
  base by rest.
  (d) Tired with no rest area found or reached, `FUN_00506a40` answers 0 and the kind's own choice follows (the guard's
  at `0x004d6554`, the researcher's at `0x00502b9f`); `Decide` stands them instead, so a tired member with no reachable
  Staff Room never walks again; when the kind's choice follows, the three kinds' searches need `FUN_00506680`'s gate
  (the rest byte under `RestLevel`: no search, straight to the walk), which `Decide` leaves out because nobody so
  tired gets that far today. (e) At the end of a rest the original runs the kind's decide in the same sweep
  (`FUN_005061d0`, `0x00506298`); `Rest` sets Idle at stamp 0 and decides a sweep later, which after Q82b reads the
  guard's `mGameTick & 3` a sweep late. (f) Found by Q82b: at hire the guard and the researcher decide at once, after
  `FUN_00506a40` (the guard's `0x004d5e76` on `mGameTick & 3`, the researcher's `0x005026cb` on a draw); `Hire` sets
  Idle at stamp 0, so they decide a sweep later. Confirm each in the `staff` census.
  **Built 2026-10-07** (`ride-operation.md`, "The decide's differences, in both games"): (a), (c), (d), (e) and (f);
  (b) was Q206's. Read again first: every kind's constructor ends in its decide, not the guard's and the
  researcher's alone, and a hire starts at base 60 or 100 with a speed of nought. In the game, predicted first, 6
  of 7 and 5 of 7 from the gate's build (each miss my own tolerance): the guard at a rest of 1.99 "tired on mGameTick 991, rest 1.990, and sets off
  for rest area 20"; his rest's end on 1110 read Walking on 1110, base 140; five hires on 1110 all Walking with the
  park held, then 0.3500, 0.6125, 0.8094; the Staff Room sold, all five tired members carry on, the guard on 23
  cells. The build before, 6 of 6: none of it. In the original under Proton, 3 of 4 (the miss mine): state 2 on
  907 from a 1.99 written on 896, state 1 on 981 from a rest ended there, the tired guard on 22 cells with no rest
  area and the researcher never `0xf`; a mechanic hired by hand read 0.0, 0.35, 0.6125, 0.8094.
- [x] **Q137. A guest going home under the `facing` overlay crashes the park.** Found by Q82's first run: an
  `IndexOutOfRangeException` in `ParkGuestSprites.Collapse` in the frame guest 37 went home. `Remove` rebuilds the
  vertex array at two quads a person, and the draw after it collapses every quad up to the last frame's `_uploaded`,
  which with the overlay's dash was two a person of the crowd before: past the new end. Without the overlay it waits
  for more than half the drawn crowd, staff included, to go before one draw, one going to none being the single case.
  Confirm: `facing 1`, a guest sent home with `depart`, the park still drawing, photographed.
  **Fixed 2026-10-07.** The fault is the upload count kept across a rebuild of the vertex array: `Resize` makes
  the array and zeroes the count, `Fold` is the draw's fold of the leftover quads. The recipe as written had
  stopped crashing already: Q177e's sixteen quads of room closed one guest's reach and Q110b's four quads a person
  widened it, so main needed many gone inside one frame. In the game, predicted first, 7 of 8 (the miss mine: the
  build before Q110b lives through the recipe): main lives through `facing 1` and one `depart`, 18 people to 17,
  and dies in `Collapse` with 42 of 62 gone in one frame under the overlay and with 104 of 128 without it; the fix
  lives through all three, 17, 20 and 24 people drawn, photographed; the build before Q177e dies on the recipe as
  filed. 4 bugs put back, 4 caught.
- [x] **Q138. The staff strike is neither built nor counted. Decode first.** Found by Q82's review
  (`ride-operation.md`, "Drawn on the way", the strike). `mStaffHQ`'s month handler `FUN_00508e70` runs every month
  the park is open: for each kind with staff it clears a set flag `[HQ + 0x28 + kind × 12]` or calls `FUN_00508f70`,
  which returns until the date passes 24 months; past that `FUN_00509360` raises the level, and levels 1 to 4 set the
  flag and post the warnings. Every decide opens with `FUN_00506a40`'s strike arm (the flag and the gate's
  `VAR_STATUS`), a walk to the strike area in state 4; state 5's `FUN_00506300` ends it. OpenTPW has none of it,
  counts none of it, and reads nothing of the save's model-9 record (`mForceStrike`, `mStrikeLevel[i]`). Decode the
  reach first: the epoch of `FUN_004f8800`'s 24-month gate. Count the monthly consideration where `FUN_00508f70` is
  reached (`CLAUDE.md` rule 4). The flag is `mStaffHQ`'s own, and the arm's one script read is the gate's status,
  which `ParkRides.GateStatus` answers, as `StaffBehaviour`'s class remarks say (`e0462c9`).
  **Decoded 2026-10-08, nothing built** (`ride-operation.md`, "The strike"). The gate's epoch is `mGameTick` nought:
  `FUN_004f8800` adds `mFunnyTimeStart` and takes it off again, so 24 thirty-day months is tick 16,589 and the first
  month to turn past it tick 16,843, 66.5 minutes past Lost Kingdom's 755. In front of it is a gate nobody had read:
  unless `mForceStrike` is set the look returns when the park is shut or any guest is inside it. The record is
  {level, flag, stamp}; a strike lasts a month and is looked at again the month after; the posts go to the advisor
  alone. The month's look was counted already (`STAFF_HQ_MONTHLY_STRIKE_CHECK`, Q198b). In the original, five
  predictions of six (the miss my timetable): stamps 715 kept with 8 guests inside, {0, 0, 2097} forced under 24
  months, {1, 0, 16837} then {2, 1, 16843} past them, all five staff in state 5 on (42,9) to (45,9), {2, 0, 17557}
  and back to work, {3, 1, 18202}. `q138/orig/`.
- [x] **Q138b. Build the staff strike.** From Q138's decode (`ride-operation.md`, "The strike"). Read the save's
  model-9 strike fields (`mForceStrike`, the five {level, flag, stamp}); at the month's change run the look as the
  original's: the gate (park shut, or a guest inside by Q148's filter, unless forced), the stamp, the flag's month
  off, the 24 months from `ParkState.GameTick`, the causes (the handymen's cell ratio is counted until Q225 reads
  the cell's dword), the levels; the three posts are counted until the park advisor speaks (`docs/PLAYER-GAPS.md`
  gap 4). In `StaffBehaviour`, the decide's strike arm through `ParkRides.GateStatus`, the four draws, state 4 with
  thought `0x15`, state 5's turn, its end by a shut and empty park, and the walk to `EntranceA`. Move the counter
  to where `FUN_00508f70` is reached. A console `strike <kind>` that sets the force and a way to set the clock are
  the run's instruments, as the original's run used. Confirm: `staff` and a new `strikes` census beside
  `q138/orig/a.log` (the records on each month and the sweeps to state 4 and 5), predicted first; a screenshot of
  the picket beside `q138/orig/s4-strike.png`.
  **Built 2026-10-08.** `ParkStrikes` (the HQ's records, read from the save: `Look`, `Consider`, `HasCause`),
  `StaffBehaviour.GoOnStrike` and `Picket`, state 4's thought; the three posts and the handymen's cell ratio are
  counted where `FUN_00508f70` and `FUN_00509360` reach them, and the month's own counter is gone. The force is one
  word for the park, so the instrument is `forcestrike <0|1>` (`strike` is the lobby's bolt), with `clock <tick>`
  and the `strikes` census. In the game, predicted first, 8 of 9 beside `q138/orig/a.log`: stamps 715 kept on two
  months with 5 and 12 guests inside; {0, 0, 2097} forced; {1, 0, 16837}, {2, 1, 16843}, {2, 0, 17557},
  {3, 1, 18202} on the original's ticks; four in state 4 within 30 sweeps and on (41,9) to (45,9) by 17013, all
  five state 1 on 17558 and state 4 again by 18241. The miss, mine: the entertainer was on the way to rest and
  joined at that rest's end, 287 sweeps on. Control, the look taken out: records 715, nobody on strike. The picket
  photographed, all five on row 9 (`q138b/photo/p2-picket.png`). 71 bugs put back, 71 caught, nine of them only
  after two tests were added and a cleanup that had aborted the run was fixed.
- [x] **Q139. Six more reached paths only log, and the boot's one sound is unheard.** Found by Q69's sweep, the same
  shape as its seven (`CLAUDE.md` rule 4): the lobby menu's Go Online (`FrontEnd`, only logs); the park menu's Load Game,
  Save Game and Publish Park (`ParkFrontEnd.NotYet`, only logs); and R, research's shortcut, bound and consumed by
  nothing (`InputButton.Research`; the original's shortcuts row 11 runs `0x0040c5b0`, a thunk to `FUN_004aa480`, as
  read by the sweep). `Boot_Init` also plays `cat_ui` effect `0xd2` after sound starts (`docs/exe/boot.md`, step 4),
  which nobody has listened to: listen first, then count it or build it. Count each where it is reached. Confirm: the
  `unimplemented` census after choosing each.
  The sixth is the gadget's postcard (the 2026-09-26 staleness audit): `ParkGadget.SendPostcard` (`b_postcard`,
  `FUN_004a9380`, which pauses the game and writes a picture out; the game ships `Postcard.wad`, `postcard.jpg` and an
  HTML template) only logs and closes the arm.
  From Q114b: Ctrl+P under the full-screen view reaches the same `FUN_004a9380` and is counted
  (`FULL_SCREEN_VIEW_POSTCARD`).
  **Done 2026-10-08.** Counted where each is reached: `GO_ONLINE`, `LOAD_GAME`, `SAVE_GAME`, `PUBLISH_PARK`,
  `POSTCARD_BUTTON`, and `RESEARCH_SHORTCUT` for R let go with no modifier over the park or under a park screen
  (the shortcuts' row 11, key `0x52`, `0x0040c5b0` into `FUN_004aa480`, read in Ghidra). The boot's sound was listened
  to by measurement and built, not counted: `cat_ui` `0xd2` is `BUTTON01` on a variation of volume (0, 0), the
  original's mix is exact zeros through its boot (76.8 s of the file), and `UiSounds.Boot` plays it at nought
  (`boot.md`, step 4). In the game, predicted first: 16 of 16 on the change and 16 of 16 on the unchanged build,
  which counts none (`q139/run1`, `control`); the boot's own mix silent to the lobby's first sound, and the click at
  0.46 s, 1.00, in a build at volume 1. 15 bugs put back, 15 caught. One prediction wrong: the control click in the
  original's lobby matched at 0.26, not 0.4. Ctrl+P over the park and in first person is still read by nothing.
- [x] **Q140. The camcorder walks onto entrances the original shuts. Decode first.** Found by Q69's sweep. The
  original's edge test `FUN_004d8750` in mode 2, stepping into a type-9 cell, reads the entrance's owner chain
  (`0x004d8883`-`0x004d8b28`, as read by the sweep, not checked): it shuts the step unless the first catalogue object's
  ride has a view (`FUN_0042a440`: a coaster handle, a script camera, or a model node flagged `0x1000`) and its
  `UsageInfo.CannotRide` is nought, and opens it if both hold, whatever the neighbour bits say. `CellEdge.For` declines
  this (`queueAhead` always answers nothing there), so our viewer walks onto the Staff Room's, the toilets', the Drinks
  Shop's, the Jungle Spray's and the bin's entrances. Decode `FUN_0042a440` and the arm, then build the gate. Whether the
  original's viewer reaches the Belly Bounce's entrance by walking rests on `FUN_0042a440` for its model.
  **Decoded 2026-10-08, nothing built** (`park-engine.md`, "An entrance is shut to the viewer"). The sweep's reading
  of the arm holds: the first catalogue object on the owner's chain decides alone, shut without a view or with
  `CannotRide`, open with both, the cell left and the link bits unread. `FUN_0042a440`'s four are the track ride's
  lead car (`+0x28`, not a coaster's), the TOUR record's first car, a coaster's node, and the model's lookup record
  of id 1 with `0x1000`, which 100 of the 2,129 shipped `.md2` carry, the Belly Bounce's among them. In the
  original, three predictions of three: parked at 299.999 against the Drinks Shop's and the Jungle Spray's
  entrances from their linked paths, and across into the Belly Bounce's from its footprint, the camera flags
  `0x202` to `0x604`. `q140/orig/`.
- [x] **Q140b. Build the entrance gate of the edge test.** From Q140's decode (`park-engine.md`, "An entrance is shut
  to the viewer"). Give `CellEdge.For` the `queueAhead` it declines, for mode 2: on a type-9 cell, the first
  catalogue object anchored on the owner's cell; `InTheWay` unless it has a view and its item's `CannotRide` is
  nought, `LetThemThrough` if both, `NothingThere` with no such object. The view is `FUN_0042a440`'s four: build the
  model's node (`ModelFile.FindNode( 1, 0x1000 )` on the placed thing's model) and count the three that need a track
  ride's lead car, a TOUR record's car and a coaster's node where each is asked, none of them met in Lost Kingdom's
  stock park. Confirm: `camcorder`, then the walk from (43,28) at the Drinks Shop's entrance and from (52,29) at the
  Jungle Spray's, the stand parked at 299.999 as the original's (`q140/orig/a.log`); from the footprint (52,24) into
  the Belly Bounce's, `FIRST_PERSON_WALK_INTO_RIDE` counted; each predicted first; a screenshot of the viewer held at
  the Drinks Shop's door.
  **Done 2026-10-08.** `ParkEntranceGate` is the arm, handed to `CellEdge` by the camcorder's edge test: the first
  placed object on the owner's cell decides, open only with the model's view node (`FindNode( 1, 0x1000 )`, read once
  an item) and `CannotRide` nought. The three other views are counted where asked and taken as none
  (`RIDE_VIEW_TRACK_RIDE_LEAD_CAR`, `RIDE_VIEW_TOUR_CAR`, `RIDE_VIEW_COASTER_NODE`; Q246). In the game, predicted
  first (`q140b/run1`, `gate`, `control`): parked at 299.999 at the Drinks Shop's entrance and the Jungle Spray's, the
  original's 299.99899 (`q140/orig/a.log`), where the unchanged build walks in to 309.999; from the footprint (52,24)
  into the Belly Bounce's, counted, where the unchanged build stops at 240.0. Two predictions wrong, mine: the viewer
  does not park in the entrance at 230.0 with 54 counts but walks on into the queue cell (52,22), 16 counts, an
  entrance being one of the edge test's two queue types; the original's walk ends there in its ride view (Q246).
  17 bugs put back, 16 caught; the one left is the gate's read of a `TOUR` script through `ParkRides.Current`, no test.
- [x] **Q145. A fee set on the entry-price screen never reaches the gate.** Found by the 2026-09-26 staleness audit.
  Guests judge and pay `ParkAdmission.Fee` (`PeepBehaviour.Judge`: `OpinionAt`, then `State.Take( admission.Fee )`),
  which `ParkPeople` captures once from the save's economy thing. The screen's plus and minus move
  `ParkState.AdmissionFee`, which only the screen and the `money` census read, so the price shown and the price
  charged part the moment the player changes it. The remark over `ENTRY_PRICE_REJUDGE_WAITING_GUESTS` ("The gate reads
  the fee through ParkState") and `SetAdmissionFee`'s remark become true when the judgement and the charge both read
  `ParkState.AdmissionFee`. Confirm: the fee raised by five on the screen, `money` showing it, then the next guest
  through the turnstile, `money`'s takings up by the new fee, predicted first; a screenshot.
  **Done 2026-10-08**: the gate reads the running park's fee (`ParkAdmission.Fee`), as the original's two reads of the
  bank's `+0x118` (`hud.md`). Five more is 30, the expensive line, so the next guest sulked and paid nothing; one
  less and the same guest paid 29: takings 125 to 154, 5 of 5 predicted (`q145/run1`, `gate`), and the unchanged
  build charged 25 with the screen at 30 (`q145/control`). 4 bugs put back, 4 caught.
## B. Docs and comments

- [x] **Q88. One label from Q50's decode.** Done 2026-09-26, `alexah/163-q88-heldbyathing-state8-label`.
  `PeepBehaviour.HeldByAThing` misses a state-8 queuer (dead by CONTENT): labelled (`CLAUDE.md` rule 3) with a
  `<remarks>` pointing at `ParkRideOperation.IsQueueing`, which already reads `SavedState` for
  `PeepState.PlayingSpotAnimation` where `HeldByAThing` does not - dead by CONTENT because nothing sets a guest to
  that state while playing a spot animation is unbuilt (Q98). The rest was already done: `ParkRideChooser`'s
  entry-cell remark (`2d5a4bd`), and the comments on `ParkIsClosed` and the settle-up and `DropStaleQueueHeads`'
  dead-by-CODE label (`e0462c9`). No game run.
- [x] **Q101. `ParkWorld`'s person-block walk names two fields the game does not.** Done 2026-09-26,
  `alexah/165-q101-guest-state-field-names`. Found by Q50d's decode. The summary of `ParkWorld.GuestState` listed
  `mHappiness 422` and `mToilet 525` among the serialiser's own names; `FUN_004fb530` tags every need float `pv`
  (`0x0075b444`), and no string `mHappiness` or `mToilet` is in the binary. Widened past what the item named: none
  of the block's seven need floats has its own string, `mLitter` included - the one `mLitter` in the binary belongs
  to the map-cell record's own litter block (`docs/exe/park.md`, "The save's world block: map cells"), not this
  one, confirmed with `strings` over the shipped exe. The order and offsets stand (happiness `+0x19c` and toilet `+0x1ac` are named by the debug strings at
  `0x004fda74` and `0x004fd10e`, already recorded in `docs/exe/ride-operation.md`); the comment now says all seven
  spellings are this project's own, not just the two named here, and says so consistently with `mTiredness`'s own
  paragraph rather than beside it.
- [x] **Q46. Two more stacked doc comments.** Done 2026-09-26, `alexah/164-q46-stacked-doc-comments`. Found by
  Q11's scan of every source file (the six in Q11 were the first). Each sat on another member's summary, so it
  documented the wrong member: in `ParkGuestSprites`, `Standing`'s block landed on `StandingFrom`, so `Standing`
  read `<inheritdoc cref="StandingFrom"/>` for a doc that was actually its own; the block moved onto `Standing`
  and the `<inheritdoc>` is gone, leaving `StandingFrom` with only the summary that was already its own. In
  `ParkGround`, the constructor's `<param name="world">` sat above the `_world` field instead of the constructor;
  moved onto `public ParkGround(...)`, leaving the field with only its own one-line summary. The rest this item
  named is done: `a134742` parted `StepVehicle` and `ReleasesVehicle`; `e0462c9` parted `Fire` and `IsStaff`, `Step`
  and `HeldByAThing`, and `ChooseSomewhereToGo` and `Explain`, dropped `NextThingId`'s stale upper summary, and
  corrected `SettleUp`'s "Five", `Explain`'s "actually get there" and the exit test's remark; `SettleUp`'s summary
  went with `d7f00bd` and `e0462c9`.
  No game run.
- [x] **Q49. A stale comment in `CreateTexture`.** Done 2026-09-26 by the staleness audit,
  `alexah/159-audit-docs-and-memory`: the comment names the byte[] and Stream constructors, not `SignFile`.
  The item as written: it says its cache check is also reached from `SignFile`, but sign
  textures are built by the byte[] constructor, with no path. (Q12's review also doubted `TryAdoptCached`'s "nothing
  the game ships asks for one path under two sets of flags"; ddfd089 measured it, texture=542 distinct=535 with and
  without the guard, and Q12's run found the sea adopted on the way back from a park.) No game run.
- [x] **Q84. `RideScript.Wait`'s summary says no opcode writes the speed word, "so it is 50 for every script".** Done
  2026-09-26 by the staleness audit, `alexah/159-audit-docs-and-memory`: the summary names the constructor's write
  and `RIDE_SPEED_SCALES_WAITS`. The item as written: found by
  Q45's handler sweep. True of the opcodes, false of the script: the object constructor pushes the item's operating
  speed in through `FUN_0055a300` (`0x004db534`; `docs/exe/park.md`, "The clock, the speed word, and WAIT"), so it is
  50 only for a script nothing binds. Rewrite the summary; the divisor is still counted as `RIDE_SPEED_SCALES_WAITS`.
- [x] **Q205. Move the ticked items out of the live queue.** `docs/QUEUE.md` is 528 KB, mostly ticked entries, and
  every session greps or reads into it. By script, move each ticked item verbatim, in queue order and under its
  section heading, into a file in `docs/history/` listed in `docs/history/README.md`, leaving the open items where they
  stand. Then repoint what says the ticked entries live in `QUEUE.md` (`docs/STATUS.md`'s "Next", `docs/WORKFLOW.md`,
  the memory index) and check every Q-number still resolves to exactly one entry across the two files. Alexah's yes
  2026-10-01. No game run.

## D. Alexah's list: decode first, then build (two sessions each)

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

## E. Large

- [x] **Q34. README rewrite.** Done 2026-09-26, `alexah/161-beginner-readme`, at Alexah's request: newcomer first -
  what it is, whether it plays yet (only Lost Kingdom's shipped park, no fresh park on any island), which release is
  tested (Sim Theme Park only), how to build and run. The status narrative went to a link to `docs/STATUS.md`, the
  format footnotes to a one-line note per row, and the dead `opentpw.gu3.me` links to the FileFormats repository.
- [x] **Q168. Run the original under Wine or Proton, as a reference to compare against.** Done 2026-09-29, GE-Proton
  10-34, outside Steam. The reference install and harness are in `docs/TOOLING.md`, "The original under Proton"; the
  retail `TP.exe` cannot start under Proton (`exe/boot.md`, "Under Wine and Proton"). Found on the way: the original's
  park clock loses precision with uptime (`exe/park-engine.md`), so timings taken on it need `tpwmem.py clock` first
  (`VERIFYING.md` rule 127).
