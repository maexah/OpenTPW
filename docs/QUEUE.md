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

- [ ] **Q1. A ride bought this session never takes a turn.** `ParkPeople.cs:1270, 1400, 1468, 1573`
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
  **This item stays unticked because its own confirm clause is still not met, and the reason is now
  Q3.** With the entry cell set, the next gate is `ParkRideChoice.StartOfQueue`, which reads that
  cell's `Neighbours` mask - and nothing ever sets it for a queue a player lays, so the walk still
  answers `cells 0 back 0` and `CanBeOffered` refuses. Measured twice in a running park, with the
  queue laid against the path on the entry cell's own side both times. **Finish Q3 and this ticks
  without another line of Q1's own code**; see the warning on Q3, because its written prescription
  would make things worse rather than better.
- [ ] **Q1b. A bought thing has no entry cell, so no queue can serve it.** DECODE FIRST.
  `ParkBuilding.Buy` leaves `EntryPos`, `ExitPos` and `TopLeft` at their record defaults, and
  `ParkRideChoice.CanBeOffered` (`:90`) refuses on `EntryPos == 0` **before** the queue is ever walked -
  `QueueCellsFor` (`:167`) bails on the same test. So a queue laid and connected to a path still measures
  `cells 0 back 0`, which looks exactly like a queue fault and is not one. The original derives all three
  in the object constructor, and the derivation is already read off the disassembly at `0x004db2da`..
  `0x004db36b`: `mTopLeft` (`+0x34`) = anchor + `MapDelta::Rotate( descriptor+0x4b0, angle + 180 )`,
  `mEntryPos` (`+0x36`) = anchor + `Rotate( descriptor+0x494, angle )`, `mExitPos` (`+0x38`) =
  anchor + `Rotate( descriptor+0x4a0, angle )`, each as `dy * 0x80 + dx` on the packed `y*128 + x + 1`
  cell. `FUN_004d9cc0` is `MapDelta::Rotate` and is decoded: 0 → (x,y), 90 → (y,−x), 180 → (−x,−y),
  270 → (−y,x). **What is NOT established is where the delta comes from.** The category `.sam` files
  carry only `UsageInfo.EntryCellStandPosX/Y`, which are fractions of a cell (0.5), not cell offsets;
  the likely source is the `Info.Shape` picture's own marker characters, which `ItemDescriptionFile.
  ReadShape` (`:555`) currently measures for width and depth and then **discards**. Decode that, write it
  to `docs/exe/`, and stop. The build is the next session: parse the markers, carry them through
  `ParkItemCatalogue.Item`, set the three fields on buy. Confirm then: buy a ride, lay a queue to it,
  screenshot a guest boarding it with the `rides` census beside it - Q1's clause, finally reachable.
- [ ] **Q2. Things loaded from the save cannot be clicked.** The pick reads `CellAt( x, y ).Occupant`
  (`ParkPicking.cs:129`). Only `ParkBuilding.Buy` (`:242`) and peeps ever set it. Nothing sets it for
  the save's own objects, so every pre-placed thing picks as 0. Stamp each loaded object's footprint at
  park load, the way `Buy` does. Confirm: click the Belly Bounce from the save, its window opens,
  screenshot and the log line. (Hover highlight is Q24; do not build it here.)
- [ ] **Q3. A laid queue cell has no neighbours and no tile piece.** `LayQueue`
  (`ParkPathBuilding.cs:249-256`) writes Type, TileSet, Direction, ParentId only. It never calls
  `ParkPathNeighbours.LinkPath` (the path arm does, `:126`) and `Retile` (`:426`) returns for anything
  not a path.
  **>>> DO NOT TAKE THAT PRESCRIPTION LITERALLY - measured 2026-09-22 and it is unsafe. <<<**
  Calling `LinkPath` from the queue arm would **destroy the cell it was meant to link**:
  `ParkPathNeighbours.cs:78-79` demotes a type-3 cell to path and clears its direction at the top of
  the very function, because in the original a path laid *over* a queue demotes it. And the linking
  rule itself excludes queues - `Cardinal` (`:115-121`) links a neighbour of type 1 unconditionally,
  types 9 and 10 on opposite senses of the direction byte, and **type 3 never forms a new link at
  all**. So whatever writes the entry cell's `mNeighbours` when a player lays a queue, it is not this
  function, and in the shipped park those bits are **authored in the save** rather than computed.
  **That mechanism is undecoded, and it is what this item really needs.** It is also what blocks Q1's
  own confirm clause: `ParkRideChoice.StartOfQueue` (`:165-182`) reads the entry cell's `Neighbours`
  and returns nought when no bit is set, so `CanBeOffered` refuses and no guest can ever be sent.
  Decode first, then build.
  **Three things measured for it on 2026-09-22, so this item starts further along.**
  (1) **The way in and the way out are typed now** - `ParkBuilding` marks the entry cell `type 9` and
  the exit `type 10`, each with the heading the shipped park carries, so Q3 no longer has to do that.
  Read back out of a running park after a buy: `cell (42,23) type 9 direction 0x01`, against the
  shipped Belly Bounce's own `(52,23) type 9 direction 0x01`.
  (2) **Typing it is not enough, and this is the real gap.** Laying a path beside that cell links the
  PATH (`cell (42,22) type 1 neighbours 0x01`) but leaves the entrance at `neighbours 0x00` - that
  `0x01` points north at the pre-existing path, which links unconditionally as type 1, not at the
  type-9 cell, whose rule is `nb.Direction & bit` and does not fire on that step. In the shipped park
  the entrance's bit is **authored in the save**, never computed. `park.md:940` says exactly how to
  approach that: *"Validate any implementation by replaying creation order, never by evaluating a
  predicate over the finished map"*, and the generator is `FUN_005348d0`, decoded in
  `park-engine.md` under "Building and deleting paths and queues".
  (3) **Queue may not be laid over path on the last cell of a run** - the game refuses it
  (`queue: (42,22) is type 1, which queue may not be laid over on the last cell of a run`), so
  "lay path, then queue over it" is not a way round this.
  (4) **The placement-time join is BUILT and still does not link, and the contradiction is the whole
  of what is left.** `FUN_00528a70`'s `case 9` turns the entrance's heading by the placement angle,
  steps to the cell it faces, and calls the neighbour rule there only where that cell is type 1.
  `ParkBuilding.JoinToWhateverIsThere` reproduces exactly that. Measured with the path laid **first**
  and the ride built beside it: the path stayed `neighbours 0x01` (pointing north at the pre-existing
  path) and the entrance stayed `neighbours 0x00`. The arithmetic says why - `Cardinal` steps
  `(0,+1)` = bit `0x10` and the type-9 arm tests `nb.Direction & bit`, so `0x01 & 0x10 = 0`.
  **SETTLED 2026-09-22, and the answer is that `Cardinal` is right.** `FUN_005348d0` is decompiled
  in `park-engine.md`: for each cardinal step it tests `neighbour.Direction & (the bit of the step
  taken toward it)` - north `& 0x01`, south `& 0x10`, east `& 0x04`, west `& 0x40` - which is
  `ParkPathNeighbours.Cardinal` exactly. So the heading is the half that does not fit: the shipped
  entrance carries `0x01` with its queue on the `-y` side, and a cell laid there steps south and
  tests `& 0x10`, so **that link could never have been earned under the rule either**. Its bit is
  authored, not computed.
  **So the one question left is what authors it.** Two leads are already **refuted**, and both are
  the obvious ones, so start past them. It is not the placer's `case 9`, which only re-runs the rule
  on an adjacent path. And it is **not** `FUN_00532fc0`'s ops `0x81`, `0x85` or `0x86`, which the
  placer calls right after each `FUN_005348d0`: that worker is decompiled in `park-engine.md` and
  those three retile, do track bookkeeping and notify the thing. `mNeighbours` is written in exactly
  one place - inside `FUN_005348d0`, through paired `FUN_00522700` calls.
  **Which leaves one reading worth testing first:** that the generator is run on the ENTRANCE cell
  itself at some point, stepping toward its queue, since the symmetric link would then give the
  entrance the step's bit (`0x01` north) and the queue the opposite (`0x10`) - which is exactly the
  pair the shipped park carries. Nothing found so far runs it there; find what does.
  `FUN_004d8c20` is decoded as a bit rotate if the heading needs re-deriving: it left-rotates the
  shape grid's per-cell direction by log2 of the angle's base bit. **Q1 ticks behind this and needs
  no further Q1 code.**
- [x] **Q1b. A bought thing has no entry cell.** DONE 2026-09-22, same branch as Q1's first half. `ParkQueues.cs:224-257` draws `Pieces[TileIndex]`, so every laid queue cell is piece 0 at
  0 degrees, and `CellEdge.Blocked` refuses it. Confirm: lay a queue to the ride from Q1, screenshot the
  pieces joined, `peeps` census showing a guest walking it.
- [ ] **Q4. Sell leaves the ride's script bound and scheduled.** `ParkBuilding.Sell`
  (`ParkBuilding.cs:115-160`) removes the model and the state object; `ParkRides` has no unbind. Add
  it, and drop queue cells keyed to the sold thing. Confirm: sell a running ride, `rides` census no
  longer lists it, no errors in the log, screenshot.
- [ ] **Q5. Console Move is Sell then Buy.** `ParkBuilding.cs:163-177`. A refused cell loses the
  object and banks the refund. Do Sell then Carry, as `ParkObjectWindow.Move` does. Return a result
  value, not a string the caller parses (`:171`). Confirm: move to a cell that refuses, screenshot the
  object still in the hand.
- [ ] **Q6. Placing a carried staff member destroys the candidate before the hire can fail.**
  `ParkStaffPool.PlaceCarried` (`ParkStaffPool.cs:95-100`): Take, then Carrying = 0, then Hire, which
  can return 0. Use the console's own safe order. Confirm: put down on an off-map cell, candidate still
  in the list, screenshot.
- [ ] **Q7. Leaving a park does not empty the hand.** `Level.Unload` (`Level.cs:862-890`) forgets the
  cameras and the build mode but not `ParkBuilding.Carrying` or `ParkStaffPool.Carrying`. Confirm:
  leave mid-carry, enter another theme, click, nothing placed, log line.
- [ ] **Q8. The island keys work during the fly-in, and the fly-in state survives the lobby.**
  `LobbyCameraMode.cs:288-292` answers next/previous island whenever the player is not Instant Action.
  `LeaveForPark` (`:439-447`) guards only its own re-entry. `ForgetIsland` (`:761-768`) clears
  `CurrentIsland` and `_wandering` but not `_leaving`, `_whenArrived` or the three leave numbers. Block
  the island keys while leaving; clear all the leave state in `ForgetIsland`. Confirm: press next-island
  during the fly-in, the camera keeps flying into the island you chose; screenshot mid-flight and the
  log line.
- [ ] **Q9. Stopping one ride's scream releases the scream effect every ride in that band shares.**
  `ParkAudio.StopScream` (`ParkAudio.cs:557`) calls `_kids.Release( effect )`, which sets the shared
  effect's `AvailableAt` to minus infinity (`SoundCategory.cs:169-175`). With two rides in one band, one
  stopping makes the other scream again at once instead of after its declared delay. Make the claim
  per ride, not per effect. Confirm: needs the second ride from Q1; capture the mix over one stop.
- [ ] **Q10. The camcorder's blocked-cell cache is static and never forgotten.**
  `ParkCamcorderCameraMode.cs:443-458` keys it on the `ParkWorld`; `Forget` (`:213-219`) clears stand,
  yaw and pitch only. The previous park's whole save stays alive through the lobby. Clear it in
  `Forget`. No game run needed; a test that enters two parks and checks the reference is released.
- [ ] **Q11. Small fixes.** `git am ~/Downloads/opentpw/small-fixes-v2.patch` (six commits: ride state
  3 named, COAST message, `ReadInt16` reads two bytes, `SoundFile` overrides instead of hides,
  `Material.ClearBoundResources` deleted with its two call sites, `Rotation ==` can be true). Then by
  hand: the six doc comments in `~/Downloads/opentpw/doc-comments.patch` (it no longer applies; read it
  and move each comment to the member it describes). Then check whether the guard described at
  `RideScript.cs:802` already ends a spinning `CRIT_LOCK` turn; if it does not,
  `~/Downloads/opentpw/crit-cap.patch` shows the intent. Confirm: build and test; load the lobby and a
  park once and screenshot each, because the `SoundFile` and `Material` commits touch the load path.
- [ ] **Q12. Four hollow tests.** Each stays green with its fix reverted: `VoicePlacementTests` (nothing
  pins `HoldPlaced` or its call at `ParkAudio.cs:709`), `TextureSamplerTests` (nothing pins the copy at
  `Texture.Cache.cs:90-94`), `ParkCamcorderWalkTests` (nothing pins that `Step` calls `Slide`),
  `LobbyLeaveForParkTests` (nothing pins the wiring, and `RadiusDecay` can change freely). Make each
  fail when its fix is reverted. No game run.

## B. Docs and comments

- [ ] **Q13. STATUS diet, and track the loose files.** `docs/STATUS.md` is 1,163 lines; "Recent" alone is
  over 1,000. Cut to under 120: "Recent" is five entries of ten lines, everything older is the git log.
  Commit this `QUEUE.md` under `docs/`. Move `docs/CLEANUP-PLAN.md` (every item closed) into
  `docs/history/` and commit it there. Commit `docs/REVIEW-2026-09-21.md` and
  `docs/REVIEW-2026-09-22.md`. No game run.
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
- [ ] **Q21. Entering a park: hide the front end, pace the gate.** `IslandPanel.EnterPark` (`:280-336`)
  closes only the island panel and swings the gate at once. Hide the rest of the front end for the
  fly-in; open the gate as the camera arrives. Confirm: screenshot burst from the click to the loading
  screen.

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
