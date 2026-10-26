# Work queue

Begun 2026-09-22 against `main` at `9b0ebab`; items from Q35 on were filed against later tips. File and doc line
numbers in Q13-Q34 are from `9b0ebab` and have drifted (`park-engine.md` by up to ~1,100 lines): find the member or
the heading, never the line.

**How to use this file.** One item per session. Take the first unticked item and do only that one.
Tick it in the commit that lands it, with the number that proves it; the pre-commit hook then moves it to the end of
its section in `history/queue-done.md`, which holds every finished item. A page citing a Q-number that is not here
means that finished entry. If an item turns out to be two,
split it into two lines here and stop after the first. Alexah may reorder; nobody else does.
A line **Needs Alexah (date):** at the end of an item is a question an unattended run left (`WORKFLOW.md`,
"Unattended runs"): the item waits for the answer, and the session Alexah answers takes the line out.

**Every item, no exceptions.**

- Start from the repo root. Read `CLAUDE.md`, `docs/STATUS.md`, then this file's head and its first unticked item:
  the first hundred lines or so. The rest of the queue is other sessions'; grep it for a Q-number when a note
  is to be filed under one.
- Branch from `main`, named for the item, next free number: `alexah/N-<what-it-changes>`.
- "Confirm" means the running game: a screenshot **and** the census or log line that goes with it,
  both. Predict the number before reading it. A test is added for regression only. It is never the proof.
- Put the bug back and re-run the new test. If it stays green, the test is hollow. Fix the test.
- Build and test the commit alone in a throwaway worktree.
- Facts found go in `docs/exe/`, and bytes in a shipped file in the FileFormats clone (rule 14). The commit
  message says why. The code comment says what the code does now. No "used to say", no dates, no `>>>`
  banners in code.
- Update `docs/STATUS.md` in the same commit. It stays under 120 lines.
- When confirmed, fast-forward merge into `main`. Commit locally. **Pushing `main` still needs Alexah's
  yes** (rule 1). Say what is ready and stop. Do not start the next item.
- End with four lines: what is ready, what was confirmed on screen, what was not, what the next item is.

**Where the detail is.** Q72-Q75 come from the 2026-09-12 review, whose three
artifacts are listed in `docs/history/README.md`. Q208-Q217 come from the 2026-10-04 review of the commits of
2026-10-03 and 2026-10-04; an id such as `u6-hoardings-1` names a finding in its findings file (`CLAUDE.local.md`
says where). Q219-Q221 come from the 2026-10-04 effort audit of the commits of 2026-09-29 to 2026-10-01; an id such
as `r-f1` names a verdict in its results file (`CLAUDE.local.md` says where). Q222 comes from Q107's comparison with
the original. A note marked "fork review 2" comes from the 2026-10-16 review of another fork, every lead checked in
Ghidra; `verify-<area>.md` and a lead id name its evidence (`CLAUDE.local.md` says where). A note marked "fork watch"
comes from the daily look at the same fork's new commits (the first, 2026-10-16:
`~/.cache/tpw-harnesses/fork-review-2/watch/2026-10-16/`, `verify-<area>.md`; `docs/TOOLING.md`, "Recipes", the daily
fork watch).

---

## A. Bugs first

**Ordered at Alexah's word (2026-10-21), after their own play of Lost Kingdom:** showing the original's art as well as it can
be shown comes first, and improvements over the original are ON by default, each said at the site as a deviation (full
texture depth, the research pumps' smooth scroll, flags easing between winds, a more legible font); a "faithful" graphics toggle that
brings the original's look back comes last (Q645). Q647 first (a console.log per launch, Alexah 2026-10-22, for the play-test fixes after it). Then the display: Q616 (eight bits a channel by default, reversing
Q700's 2026-10-18 choice), Q617 and Q618 (the ground's cells at different sharpness, then the paths, queues and ride
borders; decode how the original draws and filters the ground first), Q619 (the river and the rock not darkened), Q620
(the boundary wall, split from Q214), Q621 (the advisor's colours), Q158 (the buy screens' 3D preview), Q622 (the seaplane
invisible), Q623 (the fountain's flash). Then what a player hits: Q624 (the map screen), Q31b (the sideshow and toilet
windows), Q625 (the camera leaves the park), Q626 (leaving the camcorder), Q25 (its crosshair and its speed), Q51 (staff
stacked at the staff room), Q627 (vehicles pick up nearly non-stop), Q628 (the staff click range), Q55 (staff walking on
the spot), Q543 (the advisor's opening lines),
Q27 (push scroll), Q631 (WASD and the arrow keys), Q632 (path sounds), Q633 (queue previews), Q634 (the cursor behind the
preview), Q635 (Ctrl+click), Q419 (the hire screen's picture), Q636 (its skill meter), Q637 (the gadget's aerial), Q525
(the price's "$"), Q638 (the tag's sparkle), Q639 (the golden ticket's centring), Q640 (the pumps' stutter), Q641 (the
money's arrows). Last the chosen improvements: Q675 (the clips at the frame's own time) and Q677 (its second part, the pumps' smooth scroll), Q643 (flags easing), Q644 (a legible font),
then Q645. **Alexah's decisions (2026-10-21):** note 1 reverses the 2026-10-18 choice (Q700: four bits by default); W, A, S
and D all move the camera, and the screen shortcuts on A and S move to other free keys, said at the site (Q631). Their notes
are in `~/.cache/tpw-harnesses/playtest-2026-10-21/notes.md`, with five screenshots.
**Ordered at Alexah's word (2026-10-20):** the ferry, seaplane and bus made on demand (Q26c, which Q557's rest waits
on), then the rides, shops and sideshows that run for guests (Q580, Q581), then the research screen and golden tickets
(Q705 to Q709), then the sounds; the optional sound measurements Q575, Q576 and Q578 stand at the queue's end.
**Before that, at Alexah's word (2026-10-08):** what a playable Lost Kingdom park needs most comes first (saving a
park, the calendar, the shop's window, the gauge, the research lab), then the rest of this section in its old order.
The review of the run of 2026-10-16/17 put Q444 to Q446 first (done; its findings are
`~/.cache/tpw-harnesses/handoff/review2/q<N>/FINDINGS.md`). Q452 to Q465 come from the week-end audit of 2026-10-17
(`~/.cache/tpw-harnesses/audit-2026-10-17/`, `LEDGER.md`; `CLAUDE.local.md` says where).
The review of the run of 2026-10-17/18 (its findings are `~/.cache/tpw-harnesses/handoff/review3/`, `VERIFY-<n>.md`)
filed the items that cite it.

- [ ] **Q663. A bought thing's build draws no sparkles. Decode first.** From Q623. The original's Round Fountain, placed
  in `late30.TPWS`, rises from its bare floor among yellow and green sparkles for its whole build (`q623/orig/a/appear.png`,
  film `original/shots/q623-a.mkv` frames 87 to 120); ours rises with none, and nothing counts it. Which effect it is,
  who starts it (the placer, the build path or the item's own effect nodes) and when it ends are not decoded. Decode
  it, count it where ours reaches it, then build it. Confirm: the same placement filmed in both games.
- [ ] **Q664. The map's switches, pickers and overlay. Decode first.** From Q624. The original's map (stream `0x00774da0`,
  `hud.md`, "The map screen's stream") has six layer switches (rides, sideshows, shops, miscellaneous, tracks, visitors),
  the staff picker (four portraits on the right) and the metric picker (three faces, bottom right), none built here, and
  over the picture paints the park's own land (bright, outlined), each thing's footprint, the people, a rectangle about
  the camera's view and a highlight (`q624/orig/a-1-open.png`), from the 128 x 128 code grid `FUN_005f2050` fills and
  `FUN_005f2380` paints (Q450 (2): the colours, the ramp; `hud.md`, "Sprite sheets", is UNRESOLVED). Counted `MAP_OVERLAY` as the map opens. Decode the painter,
  the six sprite sheets and the view rectangle, then build the switches and the pickers with what they show. Confirm: each
  switch and picker beside the original's, the same park.
- [ ] **Q665. The map's hover and its click on a hovered record.** From Q624. On the viewport's `0x10001` the original keeps
  the nearest record of its list `+0x20080` within 5 of the pointer's picture point (`0x005f198e`..`0x005f1a9a`), and a
  click on the map with one kept hands its `+0x18` to `FUN_00486920` after the close (`0x005f1de8`; with none, a word
  `+0x20064` and the table at `0x007cfb90`, `0x005f1df5`). Not read: what the list holds and what `FUN_00486920` does.
  Counted `MAP_HOVER`. Wants Q664's list. Decode, then build. Confirm: a hover and a click in both games.
  - **Amended (Q624's review, run 9):** the hover's 5 is SCREEN pixels: at `0x005f198e`..`0x005f1a1b` the pointer is
    taken as (x * W / 2048, (0x600 - y) * H / 1536), y from the bottom, and `0x005f4180` writes each record's `+0xc`/`+0x10`
    as the cell centre * 480 / cells * zoom + W / 2, so the reach varies with the zoom in picture terms. What the hover
    shows: `0x005f1bab`..`0x005f1cfc` builds the text of control `0x991` (through `0x0065d55a`/`0x0065d7d0`, its vtable
    `+0x24`): the thing's name (`0x00506860`) for kinds 4 to 8, row `0x1c8` or `0x1c9` (`0x006acd60`) for kind 1, or the
    `0x007cfb90` table's entry (`0x004e2780`) for the word `+0x20064` under the pointer. The click's hand-off is counted
    `MAP_CLICK_HANDOFF` (`ParkMapScreen.CentreAndClose`) beside `MAP_HOVER`.
- [ ] **Q666. The map's advisor line 0x130.** From Q624. The map's opener posts the advisor's line `0x130`
  (`FUN_00486b00`, `0x005f100d`) and its close withdraws it (`FUN_00486b40`); here the map posts nothing (counted `MAP_ADVISOR_LINE`). Read the row
  and its response, build it as the buy screen's own line is, and confirm what he says in both games, the same park.
- [ ] **Q667. The orbit camera's point stops at the heightfield's edge, as the original's (Q625's build).**
  `park-engine.md`, "The point of interest's edge": keep a scroll term per axis, eased as the original's
  (`(sqrt( zoom ) x 0.02 x input + term) x 0.5` a frame, divided by `1 + 0.01 dt` with no input), and where the point
  stood past an edge at the frame's start (x < 0, y < 0, x > cellsX x cellSizeX, y > cellsY x cellSizeY of
  `HeightfieldFile`) drop a push further out: the term becomes the old one over `1 + 0.01 dt`. A per-frame recurrence
  on milliseconds: put it on `Time.Delta` (CLAUDE.md rule 10) and say the deviation at the site. The keys, the speed
  and the term are built (Q631: `ParkOrbitCameraMode.Scroll`, `StepScroll`), with no bound: this item adds the bound. Past the edge the original draws flat grass (`q625/orig/b-right-2-after.png`) and
  ours the sea: read what that ground is before building anything there. Confirm: a pan held against each of the four
  edges in both games (`original/campoi.py`, `q625/orig/run.sh`), the same stopping cell (x 97 or -2, y 86 or -2).
- [ ] **Q668. From a crowd, guests go home faster than the original's. Measure first.** From Q627. Loaded with
  `q416/guard.TPWS` (212 inside), over the same 2420 sweeps 204 went home here (`People: guest N went home`) and 155 in
  the original (`original/vehicles.py`'s `GONE`). **Not like for like: ours admitted 48 in four loads (13, 12, 12, 11),
  the original 33 (13, 4, 12, 4, the 4s Q222's; 34 with the guest its load made), so count the setters-off among the
  loaded 212 apart from the arrivals.** The original's census, a minute apart: 225, 220, 200, 165, 141, 126, 115, 108,
  107, 91. Ours' `guests` reply runs 5 above its own count of guests at every poll (what the five are is not read), so
  take the log's count, as the sweep-for-sweep series: ours 220, 194, 119, 96, 57 against the original's 220, 165, 126,
  108, 91 at the same sweeps (`q627/run2-crowd/run.log`, `q627/orig/b-load.log`): level at two minutes, slower at four,
  faster from six on. A guest sets off home only from the
  deciding turn: happiness under 1, the park shut, or the day exactly nought (Q109; `ride-operation.md`, the leaving
  row). Count the setters-off by cause in both games on that file (their happiness and `mExitLevel` at the turn)
  before building anything. Confirm: the census a minute in both games (ours by the log's count, not the reply's), and
  the causes, predicted first.
- [ ] **Q669. Draw the key puff at park entry (Q630's build).** `IslandPanel.EnterPark` starts effect 98 with the panel as
  its owner after closing it, so it is never drawn (`lobby.md`, "The key puff at park entry"). Start it with no owner,
  as the original's, and say at the site why the owner rule (`lobby.md`, "Deliberate departure") does not bite: no window
  is open from the Enter to the park (`windows: none` at 0.5 s, `q630/run4`), and Escape cancels the flight before a
  menu can open (a cancel shows the panel again under a puff still alive, as the original's). Confirm: `lib/lobbyentry.py` (`particles screen` reads "owner no window"), a frame at +0.35 s
  beside `q630/orig-a/film/e-025.png`, the burst's patch brighter than `q630/run4`'s at the same moment.
- [ ] **Q670. The park-entry flight is twice as quick as the original's. Measure first.** From Q630. Filmed on Lost
  Kingdom from the same island panel: the original's loading screen comes 7.3 s after the entering click, its turn onto
  the gate taking about 4 s of it and its fade (Q61) running from +4.3 to +6.8 s (`q630/orig-a/film-3.0-8.5s.png`); ours'
  comes 3.0 to 3.8 s after Enter, the camera already at the open gate at +2.9 s (`q630/run3`). The decode reckons 3.1 s
  of flying in (`lobby.md`, "The derived vtable"), and state 1's turn "within half a step". Read the original's camera
  (`+0x14`, `+0x18`, `+0x20` of the island camera) a frame at a time over a flight, and its frame rate, before changing
  anything: a per-pass rate under the 30 fps cap would make the flight's length the frame rate's. Confirm: the same film
  in both games, the three phases' times predicted first.
- [ ] **Q671. The path and queue tools' click sound `0x65` (Q632's build).** Every click of the path tool or the queue
  tool that the red latch has not refused plays `cat_ui` effect `0x65`, `rj_laypath_2`, flat at its variation's volume
  100, once, before the click's arm runs (`0x005271b7`..`0x005271ea`; `park-engine.md`, "The laying sound"): the arming
  click, each run laid, a click on the anchor, and each click of the queue tool. Ours counts it in `RunPath` only
  (`PATH_TOOL_CLICK_SOUND_0x65`) and `RunQueue` not at all. Unlike `0xaf` it has no gate on the press (`0x005271b7`
  tests only the layout replay, `[0x00785a2c]`), so it sounds where the press went to an open park screen too (by the
  listing, not run: the sound itself beside an open screen was not heard). The
  coaster bar's track tool plays it as well (mode `0x15`, `0x00526423`), unbuilt with that bar. Confirm:
  `q632/confirm.py`'s two clicks, and a queue click, which is new work in both games (neither harness has one; the
  original's wants a breakpoint on `0x0051bfc0` for `0x65` with the queue tool armed), `rj_laypath_2` matched once a click
  in ours' mix, and in the original's capture, which is on its own scale (its gains for it were about 0.04, `q632/orig`'s
  `b-match.txt`, `c-match.txt`): compare the match's score and its presence between the games, not the gain.
- [ ] **Q672. Ctrl+click on a placed thing buys another (Q635's build).** The original's world click on a placed object
  with Ctrl down (`GetAsyncKeyState( 0x11 )`, either Ctrl) opens no window: where the item's control-table price is at most
  the bank it puts the item in the hand as the buy screen does, turned to the clicked thing's angle, nothing charged until
  the put-down; a dearer one plays `cat_ui` `0x1D` and leaves the hand empty (`park-engine.md`, "Ctrl+click buys another of
  a placed thing"). Ours opens the window (`Level.ClickWorldAt`, no Ctrl arm; the click is counted
  `OBJECT_CLONE_CTRL_CLICK`, window still opened); the put-down's Ctrl half
  is built (`ParkBuilding.PlaceCarried`). Build the arm before `OpenObjectWindow`, on Ctrl held whatever Shift and Alt
  are (the original asks `VK_CONTROL` alone, not the modifier word that `Input.ControlAlone` and the `Clone` binding test), with the refusal through
  `ParkBuyScreen.RefusedSound`'s sound (`cat_ui` `0x1D`, as the buy screen's refusal; not `ParkBuildMode.RefusedSound`, `0xaf`).
  Remove the report when the arm is built. Do not reuse `ParkBuilding.Carry` as it is: it applies the golden-ticket test
  to a mystery item (`GoldenTickets.CanBuyRide`) and refuses an item no buy tab lists (`UiType` outside 0..`Feature`),
  neither of which the original's clone makes (it asks the price against the bank alone, `0x00487b14`..`0x00487b44`), and
  it passes angle 0: `Hold( catalogueId, angle )` takes the angle but is private, so the clone needs a public entry that
  passes the clicked thing's angle. A Ctrl+click with an item already in the hand goes, by the listing
  (`0x00487a3d`..`0x00487a57`), to the mode's put-down, not to the clone (read, not run). Open: a mystery ride's clone asks for cash only (read, not run), and a fixed item
  with no catalogue row (the gate's booths) reaches category 13 or 14, not 4. Confirm: `q635/confirm.py` (U1 becomes the
  Belly Bounce in the hand, item 1100, no window; then put down for 500 and the queue tool), twice, and the bank at 50
  refused with the sound, beside `q635/orig/b-run.out` and `c2-run.out`.
- [ ] **Q673. A tag put on the aerial shows at once here, about half a second after the top lifts in the original.**
  Seen by Q637 in the original's film (`q637/orig/film-on-sheet.png`: the top lifted at frame 212, the tag's picture
  from frame 228, 0.53 s on, with sparkles about it). The mast's `0x100001` arm links the tag and posts it message 6
  with 1 to arrive 500 ms later (`FUN_00658df4`, `0x004a1292`; `hud.md`, "The aerial is built down"); what message 6
  does to the tag and what is shown before it is not read. Here `ParkGadget.BuildTags` shows the tag at once. Decode
  message 6 in the tag's handler (`FUN_0048dae0`), then build it. Confirm: the put-on filmed in both games.
- [ ] **Q690. Keypad 5 and Home reset the park camera, as the original's.** From the Q631 review (run 9). Camera-table
  rows 19 and 24 (`lib/bindtables.py`: actions `0x14` key `0x0065`, `0x18` key `0x2400`) run `0x0042a500`, which sets bit
  `0x100` of `[0x00790aac]`; the update answers at `0x0042b518`: zoom `[0x007909ec]` = 110.0, asked rotation
  `[0x00790a38]` = 0.0, the point `[0x007908f0]`/`[0x007908f4]`/`[0x007908f8]` = 475.0/0.0/175.0 (`0x006fdd40`..
  `0x006fdd50`), then the bit and gui_CameraFlags' follow bit `0x1000` cleared. Ours counts a press `CAMERA_HOME_RESET`
  (`ParkOrbitCameraMode.Update`, `ResetInput`) and does nothing else. Build it: zoom 110, yaw 0, point (475, 175),
  `Scroll` noughted, a locator's follow let go; no modifier, behind the same gate as the other camera keys. Confirm in
  both games: the original under Proton with `campoi.py` (Home and keypad 5, from a panned, zoomed and turned camera;
  keypad 5 hangs on Q720's question, which Q625's keypad pans suggest is answered), ours by the log and a photograph;
  then retire the count. `park-engine.md`, "The point of interest's edge".
- [ ] **Q692. Do the game menu and the options screen switch the camera table off?** From the Q631 review (run 9),
  decode. `WindowStack.CameraTableOff` holds the camera keys off under a message box and the Save and Load screens only,
  whose openers call `FUN_00486b70` (`0x0047efa0`, `0x0049f406`). The game menu (`0x0048c830`) and the options screen
  (`0x004a3a30`) also pause but are not among the `0x0040cfa0` callers read. Read their handlers, measure one key held
  behind the menu in the original, then widen `CameraTableOff` or say why not.
- [ ] **Q693. The ride window's four condition gauges stand in 10 from their rows' top and bottom, as the original's.**
  From the Q636 review (run 9; VERIFY-10). The ride window's stats fill `0x004ad890` (the ride vtable `0x006ffbe8`
  `+0xc`, called by the shared opener at `0x0048cf55` on every ride window) calls `FUN_00485e30( gauge, 0, 10 )` on
  `0x3e16`, `0x3e18`, `0x3e19` and `0x3e17` (`0x004adc69`, `0x004adcc7`, `0x004add25`, `0x004add83`): 10 off the top and
  the bottom in the stream's 2048 x 1536 units, none off the sides, so three rows of 45 draw 25 tall and `0x3e18`'s 44
  draws 24. Ours (`ParkObjectWindow.cs`, the gauge rows about 79-82, built about 252-254) fills the whole row. Build the
  inset as Q636 did the hire screen's; a test that fails with it removed or doubled; confirm a ride window beside the
  original's (`original/uitree.py` reads the original's rects).
- [ ] **Q598. Every clip's scale track posed.** From Q597. The scale track (`0x80|0x100`; FileFormats `models.md`,
  "Scale") is in 644 tracks of 192 clips, 128 of them in 54 Lost Kingdom clips of 19 wads (`lib/scalescan.py`):
  the shops `fries`, `steak`, `cost_shp`, the rides `lookout`, `monkey`, `porkpie`, `tourride`, `tvsim`, `volcano`,
  `wateride`, `bouncy`, the sideshows `hyenas`, `junspray`, `arc2x3`, `squark`, the features `bigpalm`, `supbog`, `toilet`.
  `AnimationFile` reads them (Q599) and only the advisor poses them; `LobbyModel.Pose` counts the rest, `ANIM_SCALE_TRACK`.
  Three clips carry nothing else (Lost Kingdom's `tvsime` and `tvsims`, fantasy's `sign1c`): `TryLoad` reads them as no
  clip (`IsValid`), while the rides' and features' direct reads take them (`tvsime` and `tvsims` reach `NodePoser`).
  Before a track's first key no scale is set and the node keeps the lengths its matrix holds after the turn (21 tracks
  start past frame 0, three in Lost Kingdom: `volcanol` node 1, `wateridec` nodes 15 and 16; `models.md`, "Scale"). Pose them on every model a clip moves (`NodePoser`, `MeshRotator`) and confirm one of each kind beside
  the original's frames.
- [ ] **Q609. Build the advisor's row 19, message 0x13 (Q602's rest).** From Q602 (`advisor-park.md`, "The mechanics'
  patrol areas"). A topic on the polled rows' machinery: months from tick nought by 30 days at least 13, mechanics at least
  3, a mechanic with an area (not the whole map), the share of `mType` 9 cells inside a mechanic's rectangle, ( 100 -
  share ) x 5, nought below 300; three lines from response 55, mode 2, props (5, 0). Full Simulation only (the row's
  `+0x0c` is 0: `FullSimulationOnly`); a park with no `mType` 9 cell has a share of nought (`0x004cd6df`), so it scores
  500 and is said (no divide). Take `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` off row 19. Confirm on Q708's load (Alexah's jungle
  `New Save`, `q707/gms-none.dat`, loaded on 19004): `poll message=19 ... score=325 ... tick=19084` and sample 57 in the
  hard hat. The hint `0x13a` left waiting as the original's needs row 257 too (Q713: its sample 525 holds the original's
  advisor from 19040 to 19076, `q602/orig-R1/line.log`), so with row 19 alone this game still says `0x13a` on 19059 and
  sample 57 later than 19084: say so, or build after Q713 (run 7 review, `review7/q602/FINDINGS.md`).
  - **Note (Q713, built):** on that load this game's row 257 scores 24 on 19040 (sideshow 50's satisfaction 76, where the
    original said it), so it is not said and does not hold the advisor (`q713/run3-fs.out`).
- [ ] **Q587. The karts' and the water ride's track: laid, walked and opened.** From Q581 (`park.md`, "The karts' and
  the water ride's track, buoys and race"). Replace `PLACED_TRACK_RIDE_FIRST_TRACK_CELLS`: the purchase lays the station
  (0xd or 0x12) and both mouths (0xb or 0x10) on the track grid and arms the queue; a click on a piece ("Left-click to
  edit this track") arms mode 0xb or 0x10 with it the anchor; each click lays a straight run, sound 0xce; the walk sets
  `+0x2c` and makes the sections, their collision objects and buoys (`ParkTrackRideTable`); a closed loop with the open
  guard opens the ride, and a later queue edit opens a closed valid one (the tail `ride-operation.md` cites). Decode
  first what it leaves unread: which `gk_trck*`/`wr_trck*` model a piece draws, a piece's cost and refund,
  `FUN_0052c6f0`/`FUN_0052ccb0`. Confirm on `q581/karts.TPWS` by the same clicks as `q581/orig` (bought on preview
  cells (32,25) and (20,20), whose station sections are (30,25)/(32,25) and (18,19)/(20,19); loops to (34,29)/(28,29) and (22,23)/(16,23)): sections and buoys against `q581/orig/A-track.log` and
  `D-track.log`, and the pieces photographed beside the original's (`B-5-closed.png`, `D-13-boats.png`).
  From Q633: carried, a kart or water ride also lays its first track cells' squares (`FUN_005286a0`, `0x00529b35`),
  counted `CARRY_PREVIEW_TRACK_START`; build them with the track.
  - **Note (fork watch 2026-10-25, verify-all.md H1):** the pieces are not rigid: a kart or water piece (load flag `0x10422`, `0x00522b1e` and
    `0x00522cde`) is draped vertex by vertex at its placing, every vertex ending at its authored height above the ground
    under it. The chain: `0x004629d0` turns load bit `0x2` into loader flag `0x40`; `0x00461f10` sets `0x20000` on every node
    (`0x00462614`); the instancer `0x00463060` (flags `0x33a`, kart `0x522b90`, water `0x522d50`) calls `0x00469940` (`0x00463347`), which calls
    `0x00469a80` per node (cell triangle by flag `0x4`, Q398's note). Draw them so, not as flat plates. Not proven: whether the cars' sampler reads the draped copy.
- [ ] **Q588. Dino Karts race for guests.** After Q587. The -4 record (`Bumper_LayRide`'s grid and start buoys, the
  template's 3 laps, 2048 arrival), `BUMP 7` to state 1 and the race's first buoy, `BUMP 4` a kart a rider (mesh
  `cars % 4`), the retarget's grid and race arms with the thrust bonus, the buoy arm's laps, state 3 and the end at the
  unload point, `BUMP 14` and `BUMP 3`, the step's clips 5 and 0xc by speed, and the track's objects in the point test,
  the rim and the obstacles (quarters, rectangles, 0x40 circles). Not read, to decode or count: the seat a rider takes
  (`FUN_00549c60`), a kart's height over the track. Confirm against `q581/orig/C-track.log` (the grid, 9 to 16 and 8,
  the timer -3 to 0, state 3, the next kart at the centre) and a frame of a kart on the track (`C-4-race.png`). Moves the
  website's rides list.
  - **Note (fork watch 2026-10-21, verify-world-other L6):** a loaded ride gets the race's first buoy back from the track-ride
    loader's close chunk, type 6 (`0x00543928`, `saves.md`, "The close"): for BumperType -4 (this ride), after the handle check, it walks
    the buoy chain from `+0xa8` to the first buoy after the start buoy (flag 4) not flagged 2, wrapping, into `+0xa4`. Build that
    on load of a ride saved open; the writer never saves `+0xa4`.
  - **Note (fork watch 2026-10-25, verify-all.md N2):** the loader sets header flag `0x80` at run time (`0x004625f4`) on a kart or water track piece, so
    the model's height there is the landscape's under the point (mode 1, `park.md`), which is what a kart's height over the
    track reads. Not proven: that the cars' sampler uses the draped copy.
- [ ] **Q589. Splish Splash runs for guests.** After Q588. The -5 record (one start buoy, unload point a third of the
  way to it, 512 arrival, 64 boats), `BUMP 7` straight to state 2, a boat carrying the whole boarding list straight to
  the race, one lap, removed at the unload point with the ride left at 2, and the boat's spin (`+0x58` moved at a rim or
  an obstacle: p, ±48 × a term not read, when the old spin has p's sign, else old + p; × 0.998 a tick, turn `+0x58 / 128`; the term the contact sets is not read). Confirm against
  `q581/orig/D-track.log` and `D2-track.log` and a frame of a ring in the channel (`D-13-boats.png`). Moves the
  website's rides list.
  - **Note (fork watch 2026-10-21, verify-world-other L6):** as Q588's: BumperType -5 takes the same close-chunk arm
    (`0x00543928`, `saves.md`, "The close"), the race's first buoy into `+0xa4` for a ride loaded open.
  - **Note (fork watch 2026-10-25, verify-all.md N2):** as Q588's: the water kit's pieces (`0x10422` at `0x00522cde`) carry the run-time flag `0x80`
    (`0x004625f4`), the landscape's height under a boat (mode 1), and are draped per vertex (Q587's note).
- [ ] **Q726. The karts' and the water ride's track add-ons and hills.** From the Q581 review (`park.md`, "The karts'
  and the water ride's track, buoys and race", "Not read" lists the add-on sections (types 12 and 13) and the hills
  (`gk_trckH*`)). Lost Kingdom ships three track add-ons in `jungle/upgrades`, each `UpgradeType 1`: `lavajump` 1500 and
  `mammtunn` 1501 for Dino Karts (`AddOn.UpgradesId 1150`), and `watertun` 1502 for Splish Splash (1160). Each lays a
  section 12 or 13 (`TrackRide_AddSection`, jump-table arms `0x0054b7e4` and `0x0054b86d`, each `mov ebp,0x100` then the
  straight's arm), so its rectangle carries flag 0x100, which drives `Bumper_StepCar`'s add-on arm (the object's `+0x10`
  == `0xdac`, `FUN_00548e80` at `0x00548d11`, `0x00548daa`, `0x00548e1c`). Q587 to Q589 do not ask for it, and Q512 and
  Q513 cover only the buy list, the snap and the window. Decode the add-on arm and the hills, build them (or count them
  with `Unimplemented.Report`), and confirm one of each against the original.
  - **Note (fork watch 2026-10-25, verify-all.md H1):** the add-on and hill pieces come from the same kits (`0x522aa0`, `0x522c60`), so they are draped
    per vertex like the rest (Q587's note); build them so.
- [ ] **Q592. The cheat table's twelve rows.** From Q591 (`park-engine.md`, "The cheat table"): each is counted on its key's
  release with Alt alone (`Level.CheatRows`: `CHEAT_RESEARCH`, `CHEAT_MONEY`, `CHEAT_LOCAL_TICKET`, `CHEAT_GLOBAL_TICKET`).
  Build them as the original's after Q720 settles whether the keypad's digits reach their rows and the cheat flag's
  first-key clear is run in the original; whether this game's R binding also opens the research screen under Alt
  (the original's row needs no modifier, matched by `==`) is to be checked in the same session.
- [ ] **Q594. A loaded coaster's track and pylons are drawn.** From Q706 (`q706c/side-by-side-coaster.png`: the
  original's `q706b/O2/o-t19061.png` beside ours `q706c/run3-A/A-coaster.png`, both at 310,355 zoom 130 rotation 0, Alexah's
  jungle `New Save`). The original draws the Temple Of Gloom's track over its 38 pylons; ours draws neither, though Q706
  rebuilds the track's knots, curve and samples from the save (`ParkCoasterHeights`, `ParkCoasterPylon`). Build the
  pylons (each piece's `StdPylon` posed by its loft, rotate, incline and bank clips at its cell, as `ParkCoasterPylon`
  poses the knot) and the track's mesh from the samples and the `coaster.sam` cross sections (FileFormats `sam.md`, "A
  `coaster.sam`'s tables"; the mesh builder not decoded: decode first). Confirm by the same two frames side by side.
- [ ] **Q605. The deletes this game never makes.** From Q582 (`ride-operation.md`, the `FUN_0050b780` row): the
  original also deletes a guest a guard throws out (`FUN_004feb50`, from the guard's catch `FUN_004d6790`), one in
  state `0x11` whom no guard holds (`FUN_005019f0`) and a thing an online message names (`FUN_004e6e80` case `0x26`);
  the first two wait on the guard's chase, unbuilt. The full thing table, its other half, is Q610.
- [ ] **Q557. A script's sounds of the other voice classes, and of a script with no nodes here.** From Q553, which built
  the one-shot and the plain class, Q558, which built the chain of `0x4` alone, Q559, the held chain `0x404`, Q561,
  the music's class `0x406`, Q563, a voice out of range, Q564, a ride's scream out of range, and Q565, a script's
  one-shot out of range (`audio.md`, "A script's sounds"). Counted and not started:
  `_6BE680` and `_6BE330` (none in Lost Kingdom), type 10 (`RIDE_EFFECT_SOUND_ITEM_BANK`, no shipped script) and a
  placed sound with nowhere to stand here (`RIDE_EFFECT_SOUND_NO_PLACE`: the ferry's 172 and the seaplane's 173, ADDOBJ 8
  on node 1). Both are of the plain class (flags `0x6`), already built: what they lack is nodes, as `ParkRides`' second
  pass bound the vehicles the file does not name with no `Nodes`. **Since Q26c** both are made at their first summons
  through `ParkRides.BindNew`, which gives them nodes: in `q26c/run-B2` and `run-C` the log reads "Park audio: script
  16's effect 172, sample 'mk_ferry_1.mp2' ... plays" and "effect 173, sample 'mk_plane_6.mp2' ... plays", and main's
  runs counted `RIDE_EFFECT_SOUND_NO_PLACE`; not heard and not set beside the original's: confirm that here.
- [ ] **Q577. The sound commands 2 and 3: a script voice's drop suspended on focus loss and by the P key's pause.** From the
  run 6 review of Q565 (VERIFY-19, finding 3). The window losing focus (`0x0046b73c` -> `0x0051bcf0` -> message `0x00700b64`
  -> `0x006b5640`, type 2) and the P key's pause (`Game_TogglePause` `0x00409350`, its pause branch `0x004093af`) send
  the sound service command 2; their resumes (`0x0046b70a` -> `0x0051bd30` -> `0x006b5670`, type 3; `0x00409381`,
  `Game_Resume` `0x0040932b`) send 3 (`0x006bcab0`). Command 2 suspends a voice's drop only while its `+0x3c` is nought
  (`0x6bcafd`/`0x6bcb02`); command 3 leaves `+0x3c` and flag `0x2000` set, so the drop stays far out until a channel
  start rewrites `+0x3c` (`audio.md`, "A script's one-shot"). Ours sends neither: its script one-shots are dropped by
  their time through a focus loss and a P pause. Decode what each command does to the other voice classes, then build
  them where ours has the two paths (find first whether ours pauses on focus loss at all). Confirm: in the original,
  a one-shot away from the camera kept past its length + 250 ms across an alt-tab (`oneshots.py`).
  - **Note (fork watch 2026-10-24):** the P key's row and the overlay it puts up are read (`hud.md`, the pause
    overlay); building the overlay is Q739.

- [ ] **Q741. A park file's other voices put back (Q612's rest).** From Q612 (`audio.md`, "A park file's voices"). Put
  back the classes Q612 counts: the one-shots (`PARK_FILE_SOUND_VOICE_ONE_SHOT`: the Fountain's looped 107, the level's
  placed 7, 8 and 181), the plain voices (`_PLAIN`: the Litter Bin's 93, the rectangle voices global ambient 2 and 3), the
  music's class (`_MUSIC_CLASS`: the jungle music under the sound block's word `+0x0`, kids 91 the crowd under `+0x4`,
  whose beats `0x0051e790` and `0x0051e7b0` reach it, and the rides' 204, 216, 217), the chain of any variation
  (`_CHAIN`), and the screams' held chains (`_SCREAM`, `ParkScreams`' own; find first whether a screaming script's
  `+0xd0` is saved). Decide, and say at the site, how the land's own placed sounds started at the entry meet the file's
  copies, which the original's reader stops first. Then the writer: write the live voices (`Sound_SaveModule`,
  `0x0051c350`) where today it carries the loaded file's list less the voices let go (`ParkVoices.Without`, not counted).
  Confirm: the stock park's Fountain heard from the entry (`q565/orig/` run b), and a save and load round trip.
- [ ] **Q719. Does a loaded park play at the save's sound volumes? (one game check)** From the fork watch, 2026-10-20
  (`verify-world.md` M2; `saves.md`, "Sound"). The loader `0x0051c400` overwrites the live sound words at `0x00803aa0` with
  the save's (the volumes and the three on-flags; all 558 park files on hand hold 100/75/60/75 and on/on/on) and sets
  nothing else; the tick `0x0051bd70` pushes them. The profile applier `0x00423dd0`, which would put the player's options
  back, is reached only from `GameOptions_Construct`, the options screen and two table references, and from no load
  path found. If so, a loaded park overrides the player's volume options until they are touched. Confirm in the original
  under Proton: set music off in the options, load `Easymode.TPWI` (or a copy), and listen or record the mix
  (`record.sh`); then decide whether OpenTPW's load should do the same (it keeps the player's options today) and say the
  result at the site. Nothing is built before the check.
- [ ] **Q742. The avoid-repeat step for the park's own voices.** From Q723. The plain-class and music-class voices ask the draw with 1 for every sample after the first (`0x006bde33`; the first asks with 0, `0x006bbfe3`, and the pick is stored on every draw from a list of two or more, `0x006bc747`): the crowd's voice, the land's following voices, the park music, a script's plain and music sounds (the End's global rides 200, eleven samples) and the interface's loops (`cat_ui` 154 and 155, started by `PlayInterfaceSound` for the research screen). Here `SoundCategory.PickFrom( ..., avoidRepeat: !first )` counts `SAMPLE_NO_REPEAT` where the variation holds more than two samples, and plays whatever the roll gives. Keep each voice's last pick (the voice's `+0x46`, which a variation change leaves alone: read it in `FUN_006bc680`'s callers before choosing where it lives) and pass it to `SoundCategory.Draw`; confirm by the chosen voice's log (`Crowd voice: sample ...`, the land's following lines) never naming one sample twice running within a variation of three or more, the bug put back.

- [ ] **Q562. The bus at its stops beside the original's.** From Q561. Q583 built what this asked for, for every route
  clip: the open route's segment count and the facing along the route (`park.md`, "Where a route key puts its node");
  the bus now stands at (529.1, 64.4) and (439.9, 64.8) facing along the road (`q583/bus-stops.png`, `vehicles`), where
  the original's bus voice stood at (529, 64) and (439, 65) (`q561/side-by-side.png`). Left: the original's bus
  photographed at both stops at one camera beside ours (`q583/confirm.py ... bus`, camera 485,45 zoom 130), and its
  matrix there read from its memory, its nose against ours (-1.00, 0.00, 0.04) and (-1.00, -0.00, -0.04).
- [ ] **Q613. Cull what the original culls (Q608's build).** From Q608 (`render-states.md`, "Which draws the test
  covers"). Take `DisableCulling` off `UiMesh`, `ParkGround`, `ParkPaths`, `ParkSea` and `ParkBuildMarkers`, keeping the
  side the original keeps: for the UI, a face clockwise in the file's (x, y), y up (`lib/uiwinding.py` names the 422
  faces that go); for the sheets, the upper side, whatever their winding here. Put `DisableCulling` on `WeatherSprites`,
  two-sided in the original (`Batch_AddPolygon`), though its quads face the camera. Confirm on Lost Kingdom's island
  panel (`original/lobbypanel.sh`, `q608/confirm.py`): the "1" of "x 1" with no inner edge, as `q608/orig/run-a/a-08.png`,
  and the ground, paths, sea and markers unchanged from above (a before/after pair, `VERIFYING.md` rule 90).
  - **Note (fork watch 2026-10-21, verify-audio-ui M2):** the object window's preview is culled in both. The original puts it
    in render list 1 with diag(1, -1, 1, 1) (flag `0x200`, `0x0045bed0`; `0x0057a260`), its entries carry `0x2000000`
    (`0x0057a890`) and take `ClipToScreen` mode 2 (`ui.md`) after the same face test as every model. Ours is a `LobbyModel`
    (`ParkObjectPreview.cs:250`), culled since Q586, and by orientation keeps the same side; not photographed. Settle it with
    a preview beside the original's, a ride window's asymmetric `P` model (Lost Kingdom's `Pcoaster1` or `Pspider`). The
    advisor is the same path (`0x00468dc1`, `0x00468ded`).
- [ ] **Q614. The island panel's "x N" is drawn larger than the original's.** Seen in Q608 (`q608/PREDICTION.txt`, "Seen,
  not predicted"; `goldspan.py`): beside a key of the same size (the original's 432 units wide, ours 433), the original's
  "1" stands 118 units high and its "x" 126 wide, ours 254 and 177 (`q608/orig/run-a/a-08.png`, `q608/run1/panel-1.png`).
  Ours fits `s_X1`'s part to the control `0x1e0eb` (`IslandPanel`, 1045,191 to 1405,416). Read what bounds the
  original fits (`UiControl_PlaceMesh`, `0x00476f10`: the node `0x00476e80` answers, `+0x78..+0x8c` through its chain)
  and how the "x N" part is chosen, then fit as it does; confirm "x 1" beside the original's.
- [ ] **Q148. The music and the happiness gauge count guests the original leaves out.** Found by the 2026-09-26
  staleness audit. The original's crowd count behind the music (`FUN_004c7fa0`) and its happiness average both take a
  guest only where `FUN_004fa990` passes, and that is decoded: it packs the guest's cell from bytes `+5` and `+7`
  (`0x004fa994`) and passes when the cell's kind is 0, 1, 3, 9 or 10 (`FUN_00536390`, read first-hand, and
  `FUN_00536310` to `FUN_00536350`, `hud.md`, "The cell codes"; `park-engine.md`, the PICK UP button), so a guest on a
  ride's footprint (kind 4) or outside the park is left out. `ParkAudio.CrowdLevel` and `ParkPeople.AverageHappiness`
  count every guest, and `CrowdLevel`'s remark reads the test as a dword at `thing + 8` whose meaning is not
  established, which the disassembly does not bear out. Build the filter once for both, and for `HeadingForExit`'s
  change of mind when it is built. Confirm: the Belly Bounce full, the music level and the gauge read before and
  after, predicted first; a screenshot of the gauge.
- [ ] **Q150. Scripts and the thing sweep take a frame's ticks in two loops, where the original takes both per tick.**
  Found by the 2026-09-26 staleness audit. The original's park loop runs the scripts (`0x0054f56b`) and the thing
  sweep (`0x0054f7bb`) inside one loop over the frame's ticks. `ParkRides` and `ParkPeople` each loop over
  `GameClock.TicksDue` on their own, rides first (`ParticleSystem` is a third), so on a frame with two ticks due every
  script takes both before any guest takes one. `GameClock.TicksDue`'s remark says this must become one loop once
  guests react to what rides did in the same tick, and they do now: a shut ride turns its queue away (`c54e848`). Run
  the park's systems from one loop, one tick at a time, in the original's order, keeping the three-sweep cap
  (`ParkPeople.SweepsAFrame`). Confirm: a long frame
  forced, the log's ride and guest lines interleaved tick by tick.
  From Q238: inside one sweep the original walks ONE list, every kind of thing in it, the newest first
  (`FUN_00516270`; `ride-operation.md`, "Where a ride's turn comes from"). `ParkPeople` sweeps the guests, then the
  staff, then the rides, each in that order among its own, so a hire or a thing bought after a guest still takes
  its turn after every guest.
- [ ] **Q570. The calendar compared after each sweep, and the weather ticked inside it.** From the run 6 review of Q149
  (`review6/VERIFY-7.md`, Q149 1 and the hunt's new finding). The original's `Calendar_Update` (`0x004f8260`) runs at
  the tail of every sweep (`0x00516695` -> `0x004d7b3b`), so in a frame of two or three sweeps whose day turns before
  the last, the objects' rings roll and the month's bank and staff work turn between two sweeps; ours compares once a
  frame after the entity walk (`Level.Update`, `GameCalendar.Update`), so the later sweeps count into the closing day.
  And `ParkWeather` ticks off `GameCalendar.Counter`, which moves only after the walk, so its ticks for a frame's sweeps
  run in the next frame and read `GameCalendar.Days` as compared after the last of them; the original's
  `FUN_00512880` runs inside its sweep, before that sweep's compare. Both are said at the site. The sweep is split
  across the entities (`ParkPeople.OnUpdate`, `ParkRides`, `ParticleSystem` each loop over `GameClock.TicksDue`), so the
  fix is one sweep loop that runs every entity's sweep work, then the weather's tick, then the compare and
  `RollTheDay` / `TurnTheMonth` / `TurnTheYear`, per sweep. The weather's day phase is moot once Q529 schedules on
  instants; its one-frame lag is not. Test: a frame of three sweeps whose day turns on the second rolls the rings
  before the third sweep's settle-up (put the once-a-frame compare back and see it fail). Reached only in a frame
  owing 16 or more ticks (a hitch); no game run shows it unless a hitch is forced (the console's `pause`/`step` or a
  stopped process, as `park-engine.md`'s "+5, +5, +6" measurement did).
- [ ] **Q151. A park's sky draws the lobby's horizon band. Decode first.** Found by the 2026-09-26 staleness audit.
  `FUN_005863c0` gives the band's four rings, per half, 0.99, `c08` + 0.5, `c0c` + 0.5, `v` + 0.5 and then 0.49,
  `c08`, `c0c`, `v`, with `c08`, `c0c`, `v` 0.34, 0.17, 0.01 (`0x00701f88`) by default and 0.19, 0.01, 0.49
  (`0x00701f94`) while `DAT_008bcbc8 & 0x2000000` is set (read first-hand). `Sky.UpperV` and `LowerV` hold the second
  set, 0.99, 0.69, 0.51, 0.99 over 0.49, 0.19, 0.01, 0.49, which is the lobby's, and `Sky.Band` draws it for every
  sky; `_tintRamp`'s remark, "the whole difference between the two skies", is true of the code only. Settle which
  scene sets the flag (it also gates a mirrored copy of the dome), write the draw to `docs/exe/`, then give a park the
  default set, 0.99, 0.84, 0.67, 0.51 over 0.49, 0.34, 0.17, 0.01. Confirm: a camcorder frame of Lost Kingdom's
  horizon before and after.
- [ ] **Q152. Every lightning bolt lasts 0.42 s.** Found by the 2026-09-26 staleness audit. `Lightning.Duration` is a
  constant, and neither `LobbyWeather` nor `ParkWeather.PlaceBolt` passes a lifetime to `Strike`. The original's bolt
  draw takes one (`FUN_00580320`, argument 7, in milliseconds): 500 in the lobby and `LightningTime` in a park, 2000
  in the balance file, which nothing here reads (`weather.md`, "The bolt"). Pass each scene's own. Confirm: `bolt` in
  the lobby and in Lost Kingdom, frames grabbed every 100 ms, the bolt up for 0.5 s and 2 s.
  - **Note (fork review 2, 2026-10-16, `verify-audio-lips-video.md` lead 3):** the bolt's shape is the PC's own: the
    constructor (`0x0057d2ab`..`0x0057d2e9`) sets roughness 0.6, a stop test of 6 ± 2, a ribbon half-width of 1.3 /
    (depth + 1) in screen units and branch odds of 9 in 512; the midpoint step is `0x0057e4b0` (its offset is not
    perpendicular: it uses |mid|², and its cross term is × 0.0), the branch heading `0x0057e810`, the colour pulse
    `0x0057ee30`, whose grey can pass 255 and wrap to near black. `Lightning.cs` draws one quad 9 wide.
- [ ] **Q153. Editing a queue parts from the original's walk twice.** Found by the 2026-09-26 staleness audit.
  `ParkPathBuilding.EditQueue` and `QueueEnds` both cite `FUN_00530120`, and they differ. With no link on the entry
  cell, `EditQueue` steps along the entry cell's raw `Direction` byte, where the original takes the side the thing's
  angle names (`0x005303f1`), as `QueueEnds` does with `AngleSide`; and its no-queue arm cuts nothing, where the
  original takes the path's bit when the entry cell faces a path directly (`ride-operation.md`, "The sale's drain",
  the list). Neither is said at the site. Build `EditQueue` on `QueueEnds`' walk. Confirm: a Belly Bounce bought and
  turned, its window's queue button pressed before any queue is laid, `tool` and `cell` on the anchor, predicted
  first; a screenshot.
- [ ] **Q154. The queue retile does not ask the track layer.** Found by the 2026-09-26 staleness audit.
  `ParkPathBuilding.PathLinks`, the original's `FUN_00535dd0` counting a queue cell's mutual path links, leaves out
  its fourth test, `FUN_0053ad20` of the track record beside the cell, and counts `QUEUE_TILE_TRACK_FLAGS_GATE` on
  every link instead. The save's track layer is read now (`ParkWorld.MapCell`'s `Track` fields), and
  `CellEdge.TrackCloses` answers `FUN_0053ad20` for the walk. Whether the retile's test is exactly `TrackCloses`,
  which also applies `TrackCounts`' five types (`FUN_005363f0`), is not established: read the call first, then ask it
  and write it to `park-engine.md`. Confirm: the Belly Bounce's queue relaid, `cell` on each queue cell and the
  counter's absence in `unimplemented`, the tiles predicted first.
- [ ] **Q155. Every script's speed word is 50, where a placed thing's is its own. Decode first.** Found by the
  2026-09-26 staleness audit. The object constructor pushes the item's operating speed into its script (`FUN_0055a300`
  at `0x004db534`), and the ride window's speed slider writes it (`FUN_004dd6e0`); `docs/exe/park.md`, "The clock, the
  speed word, and WAIT". `RideScript` keeps no speed word: `WAIT`'s divisor is counted as `RIDE_SPEED_SCALES_WAITS`,
  and `ParkAudio.ScriptSpeed` is a constant 50 in `ScreamVolume` and `ScreamGridIndex`, where the speed is half the
  answer. `ParkScreamTests` calls the Belly Bounce's 35 "the real park's number", which holds only if its speed is 50.
  Decode what a loaded object's script holds (the constructor's push from descriptor `+0x1a8`, and whether the save's
  `mOperatingSpeed` reaches the script after it), measure the Belly Bounce's, then keep the word per script and read
  it in `WAIT` and in both scream readings. Confirm: `rides` showing the Belly Bounce's word, and the scream's level
  by capture.
  From Q166: every trigger hands the divisor (`[ESP+0x14]`, 0.5 + 0.01 × speed, `0x00551cdc`) to the channel as its
  play rate - `TRIGANIM` `0x00552952`, `WAITANIM` `0x00552a9f`, `LOOPANIM` `0x00552be4`, `TRIGWAITANIM` `0x00552d2f`,
  `TRIGANIM_CH` `0x00553190`, `WAITANIM_CH` `0x00553325`, `LOOPANIM_CH` `0x00553465`, `TRIGWAITANIM_CH` `0x00553624` -
  where `StartAnimation` passes 1.0 (said at the site); the deadlines of `TRIGANIM` `0x005529ac`, `WAITANIM`
  `0x00552acd`, `TRIGWAITANIM` `0x00552d94`, `TRIGANIM_CH` `0x005531ee` and `TRIGWAITANIM_CH` `0x00553682` divide by
  it, and none of these divisions is counted (`RIDE_SPEED_SCALES_WAITS` fires only from the ride window). The unbuilt
  `TRIGANIMSPEED` passes its rate / 1000 × the divisor (`0x00552f6e`), and its deadline, the length × 1000 / its rate,
  does not divide by it. The length a trigger answers does not depend on it (`FUN_00472f60`,
  `0x0047323e`..`0x00473258`).
  From Q174: `+0xe4` is the play rate in thousandths. After every turn of a script with a model the scheduler writes
  `(0.5 + 0.01 × speed) × +0xe4 / 1000` into every channel's queued speed `+0x30` (`0x00551789`), which a promotion
  and a loop's replay start at; OpenTPW's promotion keeps the speed the clip was queued at. `TRIGANIMSPEED` leaves its rate
  there (jungle `Gates` 4000). The Easymode Belly Bounce was saved looping at 1.1, and OpenTPW's second trigger at its
  word 43 (`park.md`, "Where OpenTPW's animation state parts from the engine's", difference 4) replaces it at 1.0. With a divisor other than 1, a clip under 300 ms or no model makes
  `WAITANIM`'s qword sum wrap `+0xa0` (`park.md`, "`WAITANIM` is NOT `TRIGANIM` with a wait").
- [ ] **Q156. The staff screen's two happiness meters fill upward. Decode first.** Found by the 2026-09-26 staleness
  audit. `UiMeter` fills every meter from the bottom, a choice its remarks justify by the gadget's gauge housing being
  taller than wide (59 by 224); the staff screen's two `happygrad.wct` meters are 376 by 45 (`ParkStaffScreen`,
  `0x32d` and `0x32c`). What paints a meter is the two sub-objects `FUN_0066d885` hands the value to (`+0x98`,
  `+0x9c`, their vtable `+0xc`), untraced. Trace their geometry, write it to `hud.md`, and fill each meter the
  original's way. Confirm: the staff screen at a known happiness, photographed. Note (Q244): `UiMeter` is now the
  gadget's gauge alone and its fill direction is read (`hud.md`, "The happiness gauge's two quads"); the remark cited
  above is gone.
- [ ] **Q157. Guests' and rides' reached arms that count nothing.** Found by the 2026-09-26 staleness audit. Each is
  reached in Lost Kingdom, unbuilt, and calls no `Unimplemented.Report` (`CLAUDE.md` rule 4). In `PeepBehaviour`:
  `Entering` lacks `FUN_004ffb20`'s guard (park shut or gate not open, back to the gate), reachable since the
  entry-price door shuts a running park; `HeadingForExit` has no
  change of mind (`FUN_00500a50`), which a saved guest can take. `CellReroute` never
  runs `FUN_005108a0`'s diagonal pass after a scan that splices nothing, which is how most routes end.
  `Peep.Tick` leaves out the cell's `RegionFX` term (`FUN_00501650`) on every needs turn.
  (`ParkRideOperation.Invite`'s `RunsContinuously` is built, Q304.) Count each where the original tests it, and
  put each build without an item in the queue. Confirm: `unimplemented` over a timed run, each name present, predicted
  first.
  - **Note (fork review, 2026-09-30):** `EntertainerConstsPerGrade[g].HappinessEffectOnCell` (4, 8, 12, 16, 20; `Standard.sam`)
    has no reader in our decode; find it before building the entertainer's half of the region term. Review item peeps-v3.
  From Q126: `FUN_0055a470` (`0x0054f828`, every eighth step, a dropped sweep's too) finds the ride the camera is
  on (`FUN_0042a4e0`, the list at `[0x008791b0]`) and hands its `+0xd8`, or the default `[0x0078578c]`, to
  `FUN_0051c8d0`, a sound setting not decoded. Nothing here has it, counted or not.
  - **Note (fork watch 2026-10-18, `verify-other-a.md` H2, `verify-audio-fx.md` F2):** decoded, `audio.md`, "The fade, the SoundInfo
    settings and the reverb". `FUN_0051c8d0` is the reverb preset holder (`[0x00803ab4]`, posts `0x700b30` only on a
    change), and `[0x0078578c]` is the level key `Sound.Reverb` (0 in `Standard.sam`, 3 in hallow's, none in jungle's,
    so Lost Kingdom's is 0). A negative ride `+0xd8` sends nothing. The preset reaches EAX 2.0 hardware only; reverb
    itself stays cut (`audio.md`).
  - **Note (Q580, 2026-10-20):** the Sun God reaches `SETREVERB` (`incagod` at 162 and 188, in every run of it), counted
    at the dispatch default as `Inca God: SETREVERB at 162`. Building it is the store of its operand at the script's
    `+0xd8` for the preset holder above; with reverb cut nothing would read it, so a name of its own in the count is all
    it needs (`q580fs/run-S2`).
- [ ] **Q158. Five park-screen readouts are stand-ins, and nothing counts them.** Found by the 2026-09-26 staleness
  audit (`CLAUDE.md` rule 4). `ParkObjectWindow`'s Age row prints a bare number: its wording, `FUN_006acd60` with
  `0x1b1`, is not decoded. Its scrap row prints the build price, where `FUN_004e2400` scales it by the age bucket's
  percentage, `SCRAP_VALUE_DEPRECIATION`, counted only when something is sold. `ParkEntryPriceScreen`'s heading
  `0x4f3af` is empty, and its remark says "left empty and counted"; the original titles it with the name of the thing
  at world `+0x1da732`, `mParkGates` (`ParkWorld`), and its help row 191 reads "Park name - left-click to edit", so
  the title is the park's name and its edit is Q28's. The name is lettered by `UI_SetTitle` (`0x00485d20`, called at `0x00498e4c`): yellow, with the edge, in
  the stream's rect widened by half its width either side (749..1221 to 513..1457), where ours letters the empty heading white (Q676). `ParkGadget.ShowDate` prints the machine's short date (Q527). `ParkBuyScreen`'s description panel `0x1ea` draws its frame and the footprint picture, and neither the item's turning model nor its name row `0x1ec`. Count each
  where it is drawn. Confirm: `unimplemented` after opening a ride's window, the entry-price screen and the buy
  screen.
  From Q130e, seen beside the original's lists: the hire list's wages are bare numbers too ("$ 63" there); a buy
  row with no state shows an empty box there and nothing here; Buy Land's price is `[0x0078560c]`, 10 there and 0
  here; and the headings' lettering is larger there than the rows', here the same.
  From Q188: the buy screen's panel is the object window's preview (`ParkObjectPreview`, which wants a placed thing
  today: give it an item); in the original it is (437,162) 360 by 363. The footprint picture at its lower left is
  built (Q233b) and is drawn in front of the model; the name row `0x1ec` above it is not built.
  - **Note (play test 2026-10-21, note 8):** the buy screens show no 3D preview, a black panel. Q158's panel is the
    object window's preview, (437,162) 360 by 363 (QUEUE:408-411); the object window's preview is built (Q188) but
    `ParkObjectPreview` wants a placed thing (QUEUE:409). Q450 (1) is the buy stats panel (QUEUE:2398-2402): settle
    there which of the two owns the buy screen's picture.
- [ ] **Q159. `SdtArchive.GetFile` matches a truncated name the wrong way round.** Found by the 2026-09-26 staleness
  audit. It tests `x.Name.StartsWith( name )`, the stored name against the one asked for. A `.sdt` name field is 16
  bytes and a longer name is cut to fit, so asking for "TP SCREECH 11.mp2", stored as "TP SCREECH 11.m", never
  matches, which is what its summary says `StartsWith` is there for; and "TP SCREECH 1" would find "TP SCREECH 11.m".
  Jungle's `LobbySfxHD.sdt` and `AmbientHD.sdt` store eight such names. No caller opens a `.sdt` entry by name yet
  (the archive handler `Game` registers reaches it through `OpenFile`). Match an equal name, or, for a stored name
  that fills its field, the request's prefix of that length. A test on the eight. No game run.
- [ ] **Q160. Two park tests do not test what their names say.** Found by the 2026-09-26 staleness audit.
  `ParkFootprintOccupancyTests.LeavingACellTheThingIsNotOnKeepsWhoeverStandsBehindIt` passes whatever `LeaveCell`
  does: `LeaveCell( 58, 15, Ride )` already unlinks the ride from (58,16)'s list, `LeaveCell( 58, 16, Guest )` then
  leaves its `Occupant` nought, and `ParkPicking.ThingOn` falls back to the cell's parent and answers the ride anyway.
  Assert the cell's `Occupant`; if it then fails, check `FUN_004d9280` before making `LeaveCell` ignore a thing not on
  the cell asked about. `ParkRideJoinTests.NoQueueGrowsPastTheRoomItHas` takes the room as the save's
  `QueueSizeInCells` times four, nought on the Drinks Shop and the three toilets, where the gate measures their walked
  cells (`ParkRideChoice.QueueCellsFor`); a run that queues a guest at a toilet fails it falsely. Use the walked
  count. Put the bug back for each (`CLAUDE.md` rule 6). No game run.

- [ ] **Q210. Hoardings: the original's terrain rectangle and its base-vertex gate.** Found by the 2026-10-04 review
  (`u6-hoardings-1`, `-2`, `-4`; `docs/exe/ride-hoardings.md`). `ParkRideHoarding.Ground` samples `x0 + gx` where the
  original samples `x0 + trunc( gx * (w - 1) / w + 0.1 )` inside the model's inclusive rectangle (`+0xc0`, `+0xc4`,
  `FUN_00467030` `0x004670c1`, `FUN_00454190`); counted `HOARDING_TERRAIN_RECTANGLE`. `ParkObjects` gates on
  `Nodes.Count > 1` and reads `Meshes[0]` where the original counts base vertices (`FUN_00469a80`, `0x0046a838`) and
  reads the mesh with the most (`0x0046a82c`). Port both, and write the test's expected value from the executable's
  formula, not from the code. Confirm: a ride bought on sloping ground, closed, photographed with `hoardings`; then
  the same panels beside the original's under Proton (their height, which rises first, where the sign sits).
- [ ] **Q211. Tests that run the callers, not the helpers.** Found by the 2026-10-04 review: fifteen bugs put back
  left all 1,764 tests green. Nothing runs `ParkBuilding.Buy` (taking `BindOperation` out of `Build`); `ParkObjects`'
  hoarding gate, its saved-slot `Restore`, `ParkRideHoarding.Fill`'s sliding V, the panels' `OrderBy`, the `+ 0.1`
  truncation; `Advisor.PlayParkResponse` (its `Busy` guard, the end clip's length); `SayOnlyOnce`'s `history.Said`;
  `Level`'s `DrawStatus`; `SetupParticles` before `SetupParkEntities`; `FreshPark`'s goods and chance on the two fixed
  objects; the prankery id's `& 0xffff`; the cash's `Math.Max( 0, ... )`; `Players.Load`'s read-only stand-in. And
  `ArchiveDamageTests.EveryInstalledWadEntryStillDecodes` prints its hash and count without asserting either. Write
  each test, put its bug back, see it fail (`CLAUDE.md` rule 6). No game run.
  From the run review (2026-10-17): three more callers whose tests stop at the helper. `ParkBuildMarkers` hands
  `RecordHeight( square.Lift )` to the strip's `Over`; put back `square.Lift` and every test passes, and the game's `strip`
  census prints `Over - Lift`: assert a built strip's `Over` for a lifted cell (21.5), or print `Over` (Q435).
  `ParkBuildMarkers.Fill` needs no GPU and runs before `Build`'s first GPU call, so Q436's mutation m12 (the vertex count)
  is not "game-only": move the count into a testable `VertexCount( squares, walls )`. `CameraLensTests.
  TheLobbyDrawsItsFilesAngle` asserts 100, which `LobbyCameraMode.ReadSettings( TextReader )` also falls back to, so a wrong
  path or an unread stream passes: open `lobby/lobby.txt` through `FileSystem`, assert it is found, parse that stream (Q441).
  `ParkStaffCellListTests` (Q446) demands a relink from a member in the Held state, where the original relinks only in
  its walk tick and a placement: drive a one-cell walk in Walking instead, so a relink inside `Walked` passes.
  `Level.cs`'s hand-over of the gadget's locator to the level (Q444) is reached by no test (`ParkLocatorTests` sets
  `level.Locator` by hand); build the test level through `ParkFrontEnd`, or add its deletion to Q444's mutations as
  caught by the game alone. `ParkStaffWindowTests`' refused-sound test (Q442) reaches the path and queue refusals beside a
  park screen by clicks the game refuses (`Level.WorldClick` returns on `KeptFromThePark`), so `RunPath( cellX, cellY, true )`
  at the `RunBuildMode` call survives: arm the tool before the window opens (`ParkBuildMode.Arm( ParkBuildMode.Path )`; the
  queue by `ParkPathBuilding.EditQueue( 13 )`), then click, reword the summary to "armed", and put that bug back.
  `Level.cs` hands `state.RandomSeed` to `ParkFileWriter.Running` with no test on the wiring (Q443; `q443/mutations.py` declares
  `LV` and uses it for no mutant): take the argument out and the suite passes; assert the written header's seed.
  The person windows (Q430 to Q432): Ctrl+V over the visitor's window (`ParkFrontEnd`'s case 14 branch; drive the action,
  assert the locator is on the guest and the window closed, put the branch back out); `Level.ClickWorldAt` with a staff
  thing (deleting its branch survives every test); `ParkStaffWindow.Open` with the window already up (it stays on its
  member and posts `0xc0` again); and PICK UP's "not drawn", shown by the census predicate alone (the draw loop's
  `InTheHand` skip in `ParkGuestSprites` survives; show his cell on screen).
- [ ] **Q212. The ride window's other statuses. Decode first.** Found by the 2026-10-04 review (`u7-ride-door-6`,
  `-7`, `-9`, `-10`). `FUN_00485f60` answers (80/100/255) 5..9 for a ride waiting for an upgrade, `0xb`..`0xf` for a toilet's
  call to the handyman and `0x11`..`0x15` for a mechanic called (`+0x64`), `0x15` (UITEXT 385, "REPAIRS IN PROGRESS")
  only while the worker is at work; 2 (UITEXT 366) for state 1 broken down with its assignee not at work, whether or
  not one is assigned; 24 for an exit not connected, asked whatever the open word, so an open ride with its exit cut
  off shows 24 (ours returns 0 for an open ride before that arm). The order and the tests are in
  `ride-window-door.md`, "The statuses behind Q212" (fork watch 2026-10-19, verify-advisor-ui.md L2, verify-world.md
  L1). `ParkClosedStatus` answers 1, 22 and 23 only, shows grey CLOSED for a mechanic call, and
  `ParkObjectWindow` shows BROKEN DOWN from `VAR_BROKEN` with no decode behind it. `OBJECT_WINDOW_EXIT_CONNECTION_STATUS`
  is reported every frame a window is open (9,631 in one run; `QUEUE_VERDICT_CORNER_RULE`, the other, is built since,
  Q538): report on a change. Confirm: each status photographed beside the original's.
- [ ] **Q213. The status box's lettering is smaller than the original's.** Seen 2026-10-04 with the two side by side
  (Q208): the original's "LINE NOT CONNECTED" fills the striped box in two lines of large type, OpenTPW's is small.
  Decode the font and size `FUN_004ade40` draws with. Confirm: the box photographed beside the original's.
- [ ] **Q214. A fresh Full Simulation park's four gaps.** Found by the 2026-10-04 review (`u1-startup-1` to `-4`).
  `ParkStaffPool.Roll` gives every candidate costume 0 where `FUN_00507600` stores `FUN_00541f70( kind )` at record
  `+9`, so every hired entertainer and mechanic wears bank 0 (`guests`: `bank 0+0` on all five kinds). The land
  boundary (track types 25 and 12, from `Hoardings.sam`) is written into the cells and its pieces drawn as the original's
  walk chooses them (Q656).
  The catalogue's emitters are registered and the per-item slot table (item `+0x4dc`, `0x0041455a`) is thrown away.
  `FreshParkBoundary.Apply` throws where the original stops at its 64th entry (`0x00532517`), and a test pins the
  throw. Confirm: several entertainers hired in a fresh park, more than one costume on screen. (The boundary
  beside the original's fresh park was confirmed by Q656.)
  From Q494: the picker's object march meets the boundary's hoarding in the original (ho_fos1.MD2, its track kind 25
  anchors (43,17) and (51,17), at 7 pixels right of the gate in `q494/orig/a-scan.log`); ours counts
  `PICK_OBJECT_MARCH_TRACK_THING` on every cell of kind 11, 12, 16, 17 or 25 and answers the ground there. The
  hoarding is drawn now (Q620, `ParkBoundaryHoarding`): `ParkPicking.CandidateOn` is to ask its model first
  (`FUN_0053bf30`'s `+4`).
  - **Note (play test 2026-10-21, note 40):** the generated wall between the park and the unbought land is invisible;
    its border shows as a dark line. Moved to Q620 so the display items come first; the costume and emitter gaps here
    stay.
- [ ] **Q215. The park advisor's tick: three paths neither built nor counted, and the original never watched.** Found
  by the 2026-10-04 review (`u5-advisor-door-2`, `-3`, `-9`). `FUN_0059a550` polls a metadata row each tick
  (`FUN_0059c680( 1 )`); `FUN_0059b620` posts a message after `Advisor_SayResponse` (`FUN_0040f740`, `FUN_0040fb10`,
  response groups 150 and 151 for the door); `Advisor_SayResponse` answers 0 for a sample of no length; event 10 keeps
  the cooldown stamp. Count each, then build. Confirm: press the door in the original under Proton and hear whether
  he speaks (Q90 predicted silence from the scores and never ran it), then the same in OpenTPW.
- [ ] **Q216. The save-preservation policy. Alexah's call first.** Found by the 2026-10-04 review (`u9-foundation-2`,
  `-5`). OpenTPW will not rewrite a gms.dat or Config.tcf it did not read whole; the original overwrites. A Config.tcf
  of version 0, of another version or with a tail blocks saving the options for good, and nothing on screen says a
  save was refused (rule 11: say it in the game's own style). Decide whether to keep it. Either way, measure the
  stricter `ExpandedMemoryStream.ReadBytes` over all 47 `.sdt`, the 42 `.str` and the saves: a short read it used to
  pad now throws.
- [ ] **Q217. Three leftovers among the park's people.** Found by the 2026-10-04 review (`u3-staff-paths-1`,
  `u4-gate-1`, `u2-arrivals-4`). Staff are kept on paths by a fence the original does not have; its own recovery is
  the no-links arm (`0x004f95c0`), built for staff (Q112): ask Alexah whether the fence stays. `ParkPeople` holds two
  versions of `FUN_004fa990`: `InsideGateCensus` reads the position's cell as the original does (bytes `+5`, `+7`),
  `OnACountingCell` reads the occupancy link first; make them one. `ParkCatchUpOrderTests`' row (16, 1) asserts the
  open Q150 defect as the expected result and will go red when Q150 is fixed: say so in the test.
- [ ] **Q218. Five gaps left by the Q208 and Q209 corrections.** Found 2026-10-04 by a second read of those four
  commits, each checked against the code. (1) `RideHoardingTests.TerrainEndpointsFollowTheOriginalNormalizedQuarterTurns`
  still says by its name that it follows the original, while `docs/exe/ride-hoardings.md` says it checks OpenTPW's
  larger rectangle: rename it now, and Q210 writes the real one. (2) `ride-hoardings.md` says `Meshes[0]` is the mesh
  with the most base vertices in every shipped outline; the corpus test reads `Meshes[0]` and never counts them.
  Measure it over all 129 outlines and assert it, or take the sentence out. (3) `ProfilePreservationTests` has no
  profile that reads at `Load` and fails at `Select`: the unreadable one is unreadable before `Load`, the cached one
  is deleted. Write that case (readable, then mode 0, then restored) and put the `CanWrite` revoke back out to see it
  fail. (4) `ParkBoughtClosedTests.ALoadedDisconnectedRideKeepsItsSavedDoor` builds a new ride and rebinds it; no save
  is read. Load one, or name the test for what it does. (5) `docs/exe/ride-operation.md` says the remeasure tail
  `FUN_004de1f0` leaves at once when `+0x68` is non-zero; `ParkRideOperation.ReopenAfterRemeasure` clears
  `AssignedStaff` whatever was decided and cites `0x004de48c`. Read the tail in Ghidra and make the two agree. Also
  two sentences: `docs/exe/saves.md`'s "None of this is the original's behaviour" follows a sentence that says the
  first save is the original's, and `STATUS.md`'s "cached profiles stay read-only" is true of an unreadable file
  only. Put the bug back for each test (`CLAUDE.md` rule 6). No game run.

- [ ] **Q219. Light the models from the normals the file stores. Decode first.** Found by the 2026-10-04 effort
  audit (`r-f1`, `r-c2`). The original's model lighter `FUN_00574660` takes one normal per vertex-order entry from the
  table at mesh `+0x64` (stride 12, `+0x5e` entries); OpenTPW lights every placed model from smoothed normals it
  computes, and `ModelFile.ReadFaceNormals` reads only as many entries as the faces index. Count the table's entries
  across all 2,129 `.md2`, compare the stored normals with the computed ones, then read and use the stored ones. The
  FileFormats models page gets the count. Confirm: one ride and one shop photographed beside the original's under
  Proton, the same sun.
- [ ] **Q220. Tests for the wiring the effort audit found untested.** Found by the 2026-10-04 effort audit: seventeen
  bugs put back left all 1,766 tests green. `Level.Update`'s day roll, month turn (its order: training, the bank,
  wages) and year turn; `ParkRides.Resume`'s `MoveKeptReadings` and `BindNew`'s `RideNodes`; the walk leg's x/z form
  (a leg a hair from a whole unit); the Hot Pot's `ParkBumperBoats.Put` and its drawn heading, the speed word's
  placement wiring, the seating friction 7/10 and the broken ride's 1000, the car's turn, the engine fade's length,
  `TowardsCamera`; `ParkGuestSprites.DrawBalloons` (the bob's rate and write-back); the camcorder's
  `UseFirstPersonPictures` on enter and leave; `ParkObjectWindow`'s 30 days and 4 s; the buy list's features tab.
  Write each test, put its bug back, see it fail (`CLAUDE.md` rule 6). No game run.
- [ ] **Q221. Eight leftovers from the effort audit, each dead by content today.** Found by the 2026-10-04 effort
  audit, each read in the listing. (1) `ParkScriptStates` reads the tick and next handle with `header > dword * 4`; it
  should be `>= (dword + 1) * 4`. (2) `ParkBuyScreen.Listed` applies two of the original's four filters: add the
  upgrade (`+0x284`) and model-failed (`+0x508`) tests or count them (`FUN_004aaf70`); the upgrade test is reached
  since Q349 (Alexah's Full Simulation saves), so it is Q452. (3) `ParkAudio.ScreamEffectFor`
  silences a negative band; `FUN_00551130` plays `0x48` for one. (4) `RideScript.NextRandom` answers 0 for every
  bound + 1 at or below nought; only -1 divides by nought (`0x00553994`). (5) `ParkItemHeights.Over` floors the
  cell's middle where `0x00452bb0` truncates corner offsets: measure it on raised ground. (6) The original rolls a
  rider's head sprite to its node (`FUN_0044b510`, `0x0044ba04`); `DrawHead` does not, and it is counted (`RIDER_HEAD_ROLL`): build
  it. (7) `Peep.Pace` works at 24 bits and `ShopCostOfGoods` at 53 under one control word: settle which, the pinned
  18 may be 19 (the shop window's amount slider now sets an amount off the steps of 50, Q31). (8) Two docs lines not yet re-measured: `park.md`'s selector table rows 8 and 9 leave out the
  `0x4000` and type gates of `FUN_00544c80` and `FUN_00544e50`; FileFormats `sound-categories.md` says the masks use
  bits 1, 2 and 4 beside 645 speech variations holding 100.
  - **Note on (7) (fork watch 2026-10-18, `verify-world-a.md` H2; `park-engine.md`, "Which rounding is live is not
    settled"):** the cause of the 24-bit reading is not settled, and our own 53-bit reading stands beside it: a breakpoint
    at `0x0059e17b` in the running original read the control word `0x27f` on ticks 970 and 1125
    (`advisor-park.md`, "The product is a double of the float key"), while the game-clock accumulator is added at 24 bits
    (measured). DirectDraw's cooperative level is set with `0x851` (`0x00563914`) and `0x808` (`0x00563b92`), both with
    FPUSETUP and neither with FPUPRESERVE; the renderer's own 24-bit switch is `0x00591220` (a failed frame leaves it in
    force, `0x00577433`). Which one a given calculation sees is still to be read, in the original, at that calculation.
- [ ] **Q222. The original's guests come up empty-handed, and ours do not. Measure first.** Found by Q107's
  comparison with the original (`ride-operation.md`, "Q107"). The same stock Lost Kingdom park left alone for 240 s:
  the original's chooser named nothing 112 times and a thing 72 times (Jungle Spray 27, Drinks Shop 18, Belly Bounce
  16, toilets 11), with at least fifteen arrivals; OpenTPW's named nothing never and a thing 16 times (Belly Bounce 14,
  Jungle Spray 1, Drinks Shop 1), with one arrival (Q26). In the original seven of the empty hands were at (52,29),
  where the Jungle Spray turned guests away fourteen times; here `why` scores the Belly Bounce 10 or more for a fresh guest anywhere inside
  the park. So a score, an offer gate or the clock the chooser reads (Q132) differs, and Q104's and Q105's scores were
  never compared with the original. Read one guest's scores in the original at a known cell (a break in
  `FUN_004fcc30`, or its inputs from memory) beside `why` for the same guest and cell, find the term that differs,
  then file its build. Instruments: `q107/orig/watch.py` and `watch1.log`, `q107/base`.
  From Q252 (built since, Q304: `Invite` reads `RunsContinuously`, and it carries five): **OpenTPW's Belly Bounce carried one rider at a time.** Every `rides` census of every run to hand
  reads bouncing 0 or 1, with fifteen queueing (`q252/run2`); the original, given the same file, had five aboard
  twelve seconds after its load, a guest boarding every three (`q252/orig/a-load.log`), and its riders ride 121
  sweeps, or 32 more. So the crowd on the queue is boarding's, not the chooser's alone: read the boarding turn first.
  From Q26b: the crowd on the Belly Bounce now sizes the loads. With thirteen arrivals its sixteen places fill, the
  offer gate refuses it, and a load called on that sweep is 4, not 12 (one run of two, `q26b/gate`); the original's
  passed the gate on every sweep of two runs.

- [ ] **Q224. A happy guest does not jump on their deciding turn.** Found by Q111 (`ride-operation.md`, "The state-6
  turn, in order", (a)). More than 100 sweeps past `+0x208` and happiness above 80: spot animation 5 and the turn ends
  (`0x004fecd2`..`0x004fecea`). Every part is built (`PeepBehaviour.PlaySpotAnimation`, `SpotHappy`); build it where
  `DECIDE_HAPPY_SPOT_ANIMATION` is counted. The original's stock park made one in 964 sweeps (guest 33 at 83).
  Confirm: a guest at happiness 90 deciding, set 5 for eleven sweeps and no sooner than 101 sweeps again; a screenshot
  of the jump, beside the original's.
- [ ] **Q225. A guest full of litter neither walks to a bin nor drops it. Decode first.** Found by Q111, arm (c).
  Litter 90 or more: the nearest `HoldsLitter` thing within three cells that routes, `MajorDest` and state 9, whose
  turn `FUN_004fff20` walks there, sets the bin's script variable 0 and zeroes the litter; none, litter of a drawn
  type on the cell (`FUN_004d93b0`) and the level zeroed. Decode `FUN_004d93b0` and what a cell's litter is drawn as,
  and check `FUN_004fff20` whole; then build both where `DECIDE_LITTER_BIN_ERRAND` and `DECIDE_LITTER_DROPPED` are
  counted. Confirm: a guest at litter 90 beside the bin at (44,29) walks to it, `peeps` showing litter 0; one far
  from it drops, `cell` showing the litter; a screenshot of each.
  - **Note (fork watch 2026-10-18, `verify-other-b.md` L2):** `FUN_004d93b0` is read whole in `ride-operation.md`, "A cell's
    litter": it returns at once online and when the cell already holds litter; the kind picks one of eight sprite programs
    (kinds 1 to 5 their own, 6 and the "Dodgy litter type" log share one, 7 and 8 stamp region effects 2 and 5 first, 8 also
    particle `0x32`); the cell keeps the sprite (`+0x28`), the kind (`+0x2c`) and nought (`+0x30`); and **it reseeds the park
    generator with the cell id**, then draws twice for the place in the cell (`10 x cell + 1 + r % 9` on each axis), so a cell's
    litter always stands in the same spot and every drop resets the shared generator, which the generator mirror must copy.
- [ ] **Q226. Nobody is sick.** Found by Q111, arm (b). Illness exactly 100 on a third of deciding turns: spot
  animation 7, litter type 7 on the cell, event `0x12`, sound `0xcc`, illness nought, and the turn ends. After Q225,
  which builds a cell's litter. Build it where `DECIDE_VOMIT` is counted. Confirm: `need vomit 100`, a guest sick
  within a few turns, `peeps` showing illness 0; a screenshot.
  - **Note (fork watch 2026-10-18, `verify-other-b.md` L2):** the sick are litter kind 7: record `0x0074f140`, region effect 2
    stamped first, then the sprite placed by the cell-id reseed and two draws (`ride-operation.md`, "A cell's litter"). The drop
    does nothing on a cell that already has litter, so a second sickness there leaves the first.
- [ ] **Q227. Nobody stops to watch the entertainer.** Found by Q111, arm (e). An entertainer on the nine cells
  around a deciding guest, and the nearest one performing (staff state `0xe`): event `0xe`, the guest turned to face
  them, and the turn ends. The original's stock park did it eight times in 964 sweeps. After Q133d, which builds the
  performance; counted meanwhile as `DECIDE_ENTERTAINER_BESIDE`, whoever the entertainer is doing. The fireworks half
  (`DECIDE_WATCH_FIREWORKS`) is dead by content in Lost Kingdom. Confirm: a guest beside the performing entertainer
  facing them; `facing 1` and a screenshot.
- [ ] **Q228. A prankster plays no pranks.** Found by Q111, arm (f). Below happiness 15, on a draw under the
  prankery: a stink bomb (litter type 8, sound `0xcf`) on `PeepInfo.StinkbombLikelihood` of a hundred, litter, or
  the balloon of another guest on the cell let go; event `0xf`, a type-`0xe` bus message and happiness up one. After
  Q225. Decode what the bus message's receivers do with it before building. Counted as
  `DECIDE_PRANK_STINK_BOMB`, `DECIDE_PRANK_LITTER` and `DECIDE_PRANK_BALLOON`. Confirm: `need prankery 102` beside a
  guest holding a balloon, the balloon let go; a screenshot.
  - **Note (fork review 2, 2026-10-16, `verify-economy-guests-paths.md` L3):** the receiver: the guard sent is the
    idle or walking one (state 0 or 1) nearest the staff HQ's own cell; `0x005099c0` never reads the prankster's cell,
    and a tie goes to the newest hire. The HQ sits at (0, 0) in Easymode and in all five Full Simulation saves, so it
    is the guard nearest the map's corner wherever the prank is. Not read: anything else acting on message `0xe`, the
    escort `0x004d6a50`'s arms.
  - **Note (fork watch 2026-10-16, `verify-advisor.md` lead B):** the advisor's half of the bus message is decoded
    (`advisor-park.md`, "Rows settled from the bytes"): `ReceiveMessage` arm `0x0059b221` takes the guest's id and
    packed cell and switches on the message's `+0xc` (the guest's id % 3): 0 posts advisor message `0x7d`, 1 `0x7e`, 2
    `0x7f` (rows 125-127, getters `0x0059f360`, `0x0059f370`, `0x0059f380`, keys `PrankSbomb`, `PrankLitter` and
    `PrankBalloon` `.Score`, 35 each, above the 25 cut). The prank done is `mPrankeryIndex` 100 + id % 3, so the
    advice names the prank done for a guest the constructor made.
- [ ] **Q229. A member of staff put down sets off on the next sweep, where the original's stands eleven. Decode
  first.** Found by Q112's measurement of the original (`ride-operation.md`, "OpenTPW takes the arm"). Its guard, put
  down from the staff window's PICK UP, reads state 7 to 0 on the click, 0 to 1 with a destination a sweep later, 0
  again inside that same tick, and walks eleven or twelve sweeps on (ticks 1206, 1207, 1207, 1218; 1732, 1733, 1733,
  1745). `ParkPeople.DropStaff` sets Idle from Carried, which stamps 0, so ours decides on the next sweep and keeps
  walking. What writes the second 0 is not read: the hand mode's uninstall puts a worker it still names down again
  (`0x0046cdc0`, `FUN_00505ea0`), and state 0 from a walk stamps the clock, which would fit. Decode the drop's order,
  then build it. In the second run the walk also stopped on grass at (43,27) eight sweeps in and the no-links arm ran
  again from there: read why. Confirm: `staff` after `putstaff`, `idleSince` and the sweep the walk starts, predicted
  first; a screenshot.
  From Q133b: the original's handyman, put down two cells from a dirty toilet, read `0xa` on the tick after the
  put-down and the toilet's stamp was written again a tick later (1389, then 1390: `q133b/orig/a.log`), so the
  second decide runs for him too; from `0xa` the idle between stamps nought, and he does not stand eleven.
- [ ] **Q231. Under an open park screen: F1, the wheel and the release. Decode first.** Found by Q115
  (`park-engine.md`, "A park screen is open"). The screens' key handler answers a plain F1 let go by calling
  `FUN_005194d0` on the world and `FUN_0059ab50` on what it answers (`0x00488bfb`), not traced; nothing here reads F1 in
  a park. Where the wheel's message goes with the pointer over a screen's body is not decoded: here the wheel zooms
  the camera wherever the pointer is, unless a list or a slider takes it. And an armed tool's click beside a screen is
  taken on the press here, where the original skips the press and commits on the release (`Level.KeptFromThePark`),
  which is `RunBuildMode`'s standing difference. Decode the two, count each where it is reached, then build. Confirm:
  F1 and the wheel over the buy screen beside the original's, read from memory.
- [ ] **Q232. A list opens with no row selected, where the original's opens with one highlighted. Decode first.**
  Found by Q117 (`hud.md`, "A right click on a list"). The original's add selects the first row as a list fills
  (`FUN_0066525c( 0 )` in `FUN_0066403b`), and its visitors list opened with a row highlighted every time: the first
  on one opening, the second and the tenth on two others, so something else moves it (the all-items builders call
  `FUN_00664d2a` and `FUN_00665739` with a thing; the two-second rewrite and the sorted insert are the other
  candidates). `UiList.FirstRow` counts the add's selection and builds none (`LIST_FIRST_ROW_SELECTED`), so a right
  click that misses every row before one is chosen goes nowhere here (`LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED`),
  and `Remove` drops the selection on the `0x100` branch though all five list trees have the bit clear
  (`LIST_RESELECT_PAST_THE_WINDOW`). Decode which row each of the five lists opens on and what the buy and hire
  lists do, then build it. Confirm: the visitors list opened beside the original's, the highlighted row predicted
  first; a screenshot.
  From Q233b: the buy list now selects the row under a moving pointer (`UiList.SelectsUnderPointer`, the flag `0x80`);
  the hire list carries the flag too and does not set it, and the three list screens' trees carry it (`0x291`). With
  no first row selected, the buy screen's panel stays empty until the pointer has been over a row, where the
  original's shows the top row half a second after the list fills.
  From Q495 (`hud.md`, "The buy screen's help rows"): the fill's pick is also the buy screen's help item, so in the
  original the first row's help row is posted as soon as the pointer comes onto the row area; ours posts nothing before
  a row is selected. After a tab's click the original's bar was hidden with the pointer on the tab (`q495/orig/`,
  O9), where ours keeps the tab's row: look at it with the pick.
  From Q356 (`hud.md`, "The first row is picked as a list fills"): the pick is built for the hire list
  (`UiList.PicksFirstRow`, its `0x401` posted and taken by the screen's `Update`); a list sets it once its screen's
  handler of `0x401` is read. `gmove.py` glides diagonally and the pointer over a list picks: check the pointer's
  path in the original's visitors runs (the second and tenth rows) before looking for anything else.
  From Q375 (`hud.md`, "The miscellaneous tab: a row an object, and each tab's sort"): the pick is built for the
  four all-items lists too (their handler has no `0x401` arm); the all-staff and visitors lists still count it.

- [ ] **Q247. A loaded park is built again behind the loading screen, and the list has no scrollbar.** Left by
  Q241c (`saves.md`, "OpenTPW's Load Park"). The original reads a park file over the running park, with no loading
  screen and no change of scene (`FUN_00414d40`: twelve teardown calls, the modules, `FUN_00415140`), where
  `ParkLoadScreen.Load` asks `Game.RequestParkLoad` for the whole scene again; Restart Park is the same
  (`ParkFrontEnd.RestartPark`). And the stream's scrollbar (control 1: `!slider`, `b_up`, `b_scroller`, `b_down`)
  is built on no list here: a list longer than its window is counted as `LOAD_PARK_LIST_SCROLLBAR` on this screen
  and the wheel scrolls it. Decode the teardown first, then decide with Alexah whether the park is rebuilt in
  place. Confirm: a load timed against the original's 1.3 s, and a folder of fourteen saves scrolled by the bar.
  From Q241f: no load here reads the camera module, which the writer now writes (`ParkWorld.Camera` holds it,
  `FUN_0042cec0`; `saves.md`, "Loading the modules", Camera): a loaded park opens on the default view, where the
  original's opens where the save's camera stood. The file's rotation is the orbit camera's yaw negated.
  - **Note (fork watch 2026-10-19, `verify-audio-fx.md` M2; `hud.md`, "Which window the click sound is for"):** the bar's
    arrows are repeating buttons (flag `0x21`): first repeat after 500 ms, then every 125 ms (`+0x13c`, `+0x140` of every
    button). An arrow clicked without repeating plays `Select3` (`0xbd`, the bar's flags `0x10`); one held past 500 ms
    plays **nothing**, neither at the repeats nor on release (the repeat messages are not `0x100`, and the release posts
    none after a repeat). The thumb and the track play nothing.

- [ ] **Q249. A park that was never loaded from a file has nothing to carry.** From Q241e. The fresh world (a
  Full Simulation park, or a player whose folder holds no park file) is made by `FreshPark`, so the writer's
  carried modules have no source. Decode what each module holds in a `restart.INTS` written at tick nought
  (three are in Alexah's saves), make each afresh, and take the legal text from the level's shipped park file.
  After Q241j. Confirm: a fresh Full Simulation park saved and loaded by the original under Proton.
  From Q241f: `Level.WritePark` counts such a park as `SAVE_PARK_WITH_NO_FILE` and writes nothing.
  From Q656: the boundary's anchors are `FreshPark.Boundary`, in the walk's order (the original's model slots 3 to
  109 in Lost Kingdom), each with the mask, direction, flags, row and turn its track sub-record holds (`mHoardingNeighbours`,
  `mDirection`, `mFlags`, `mTileData` set 5) and `ParkBoundaryHoarding.PieceOf` its record (FileFormats `saves.md`,
  "A boundary piece's record"); write them as the original's `restart.INTS` holds them.
- [ ] **Q680. How often a fresh park's boundary laying draws the generator (from Q656), decode first.** `FreshParkBoundary`
  chooses each anchor's piece once, in the walk's order, which gives the original's pattern of straights' variants
  (34 fresh parks), and counts `FRESH_BOUNDARY_LAYING_DRAWS`: the original's laying (`Boundary_LayFresh` `0x005323f0` →
  `FUN_00524960` → `FUN_0053aac0` → `FUN_005370e0`, then `FUN_005311c0`'s 0x81 arm and `FUN_0052ba50`) calls
  `Track_RetileCell` on anchors more than once, and each choice of a straight draws the park's generator, so the park
  starts on a state further on. Read those callers and count the choices per anchor (a watch on `0x00820aec` under
  `watch.sh` in a fresh park counts them), then draw as often, or keep the deviation said at the site.
- [ ] **Q744. An item with a map offset (the Huge Hollow Rock, the Dino Karts Tunnel) is marked, refused and sounded on the anchor's cells, not the model's. (from the run 11 review of Q649.)**
  Q649 made the floor blend use the cells the model covers (`ParkObjects.FloorFootprint`: `FootprintAt` of `ModelCell`);
  every other footprint of such an item is still the anchor's. `ParkBuilding` stamps (`:83` -> `Stamp` `:157`), refuses
  (`PlacementRefusal` `:193`) and clears on a sale (`:483` -> `Unstamp` `:511`) over `FootprintAt( anchor )`, and
  `TrySoundPlace` takes `BoxMiddle( Standing.Footprint )` (`ParkObjects.cs:151`, set at `:388` from the anchor's footprint).
  The original's marked cells and its sound place are the model's rectangle, with the anchor as the cells' owner. Evidence:
  Alexah's Full Simulation jungle saves (`autosave.TPWS`, `New Save.TPWS`; read through `lib/parkfile.py`): the rock,
  thing 252, item 1427, anchor (19,27) angle 0, `mTopLeft` 3348, its model record's place (19,26); its mType-4 cells are
  x 19..23, rows 26..30 (row 31 is all path), every `mParentID` (19,27); the tunnel, thing 404, item 1501, anchor (17,21)
  angle 90, `mTopLeft` 2705, record place (16,21); its mType-4 cells are x 16..19, rows 19..21 less its two `.` cells,
  owner (17,21). The sound place: `FUN_00556af0` (`0x00556b04`) calls `FUN_00466b70`, which reads the instance's
  `+0xc0`/`+0xc4` (`0x00466b9a`, `0x00466ba0`), the rectangle `FUN_00467030` makes from the model's place. In OpenTPW a
  rock bought in Full Simulation marks rows 27..31 under a model standing on 26..30; selling the loaded rock unstamps
  27..31 by owner and leaves row 26 mType 4 for a thing that is gone; its sound place stands a row off.
  **Read the original's marking code first** (what writes the mType-4 cells, and from which rectangle; the cells in the
  files are the evidence so far, the code was not read), then give the build marks, `PlacementRefusal`, the carry squares,
  the sale's `Unstamp` and `TrySoundPlace` one helper for an item's covered cells (`ParkObjects.FloorFootprint`), the owner
  staying the anchor. Confirm in Full Simulation (research and buy the rock and the tunnel, both left out of Instant
  Action): the cells the buy marks read with `parkfile.py` against the original's own save of the same buy, a sale
  clearing exactly those, and the sound place beside the original's.
- [ ] **Q569. Where the original puts down a worker held at a save, decode first.** From the run 6 review of Q241j (VERIFY-3,
  "anything the first reader missed"; unsettled). The world writer installs the idle mode over the hand
  (`FUN_0046c350` at `0x00516d13`), which runs the outgoing mode's uninstall: for a worker picked up, the type-6
  mode's vtable `+0x2c`. Ours puts them back on the middle of the cell they stand in, the one they were picked up
  from (`ParkHand.LetGo` -> `ParkPeople.PutBack` -> `DropStaff`), and writes them there. Neither `docs/exe/` nor the
  commit shows that the original does the same (`PutBack`'s comment names `0x0046cdc0` as that uninstall, unshown). Decode the type-6 mode's `+0x2c` in Ghidra (vtable `0x006fea70`):
  where the worker goes (their pick-up cell, the cell under the pointer, or left where they hung), whether they are
  set idle and whether a mechanic goes to his job search there too. Write it in `docs/exe/park-engine.md`, "The
  hand's ways out", and only then change `PutBack` if it differs. Confirm: the original under Proton saving with a
  worker in the hand, read back with `parkfile.py` (their position and state), against ours
  (`ParkSaveScreenTests.AWorkerInTheHandIsPutDownBeforeTheParkIsWritten` holds ours).
- [ ] **Q571. What the original's autosave holds when its window is closed under the End.** From the run 6 review of Q552
  (`handoff/review6/VERIFY-12.md`, Q552 3). Ours writes `autosave.TPWS` on a window close with the End up
  (`Game.cs` -> `Level.WriteParkOnLeaving`), with no End in it and its two scripts not written
  (`SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD`), said at `ParkEndFeature` and in `park-engine.md`, "OpenTPW builds it". The
  original's End is a whole object (`0x004db090`, registered by `0x0050b350`, handle `0x007cf4f4`) and nothing found in
  objdump skips it in the state-`0xb` save. Measure: in the original under Proton, `q550/alldone576.TPWS` to tick 800
  (the End up), close the window, then read its autosave with `parkfile.py` (is thing 43, catalogue 1605, in the object
  list; are `End.RSE` and `AnimCtrl.RSE`'s states written). If it holds the End, decide with Alexah whether ours writes
  it too (and what loading a park with the End in it does in the original); otherwise say at the site that both leave
  it out.

- [ ] **Q270. The park music's level counts every guest, where the original counts only those on five kinds of cell.**
  From the week-end audit (stage 4; read twice from the listing, not yet measured in the original). `FUN_004c7fa0`
  (`0x004c801f`) asks `FUN_004fa990`, which takes the guest's cell and passes only a cell of type 0, 1, 3, 9 or 10
  (`scenes.md`, "The level"). `ParkAudio`'s level takes `Peeps.Count` with no cell test, and its comment says the
  five tests read the thing's `+8` and that the field is not established; `Peep.CountsOn` is already that test.
  Build the count through it. Q148 and Q217 touch the same filter. Confirm: `music` read with guests standing on a
  queue cell and off one, beside the original's level for the same file.

- [ ] **Q271. The model under a point misses the park gate's row.** From the week-end audit (stage 4; read twice
  from the listing). `FUN_00527e80` (called at `0x00452a82`) answers the gate thing the world keeps at `+0x1da732`
  for every cell of the entrance's row, from one cell before EntranceA's x to one past EntranceB's, without reading
  the cell, and only otherwise goes to `FUN_00527d60` (`park.md`, "The scene's height under a point").
  `ParkSceneHeight.StandingOn` answers null there. Measure first whether the gate thing's type byte is 3
  (`0x00452ac8`): without it the original answers nought there too. Confirm: the height read under a point on the
  gate's row in both games.

- [ ] **Q272. A mechanic arriving at a ride that waits for an upgrade is sent away.** From the week-end audit
  (stage 4; read twice from the listing). `FUN_004da740` puts him to work on any of three: the ride's state 1, its
  state 2 (`FUN_004e03e0`, `0x004da7ca`) or the ride's `+0x64` call, each going to `FUN_004da370` with `0xd`
  (`0x004da7fa`). `StaffBehaviour.ArriveAtTheRide` tests state 1 and the call only, sends a state 2 ride's mechanic
  back to his decide and counts nothing; its summary gives the two tests as the original's. Build the third, or
  count it. Confirm: a ride put to state 2 with a mechanic sent to it, his state read in both games.

- [ ] **Q273. A node's direction is normalised in another order and width than the original's.** From the week-end
  audit (stage 4; read twice from the listing). `FUN_00556b90` sums (x*x + y*y) + z*z, keeps the root as a float
  and divides each element by that float (`0x00556cba`, `0x00556cc6`, `0x00556ccc`). `RideNodes` sums
  (z*z + y*y) + x*x and divides by the double root, and its comment says the build takes the third element's
  square first. The last bit of a saved direction can differ. Confirm: a written model record's direction words
  compared bit for bit with the original's save of the same park.

- [ ] **Q274. A sprite the writer makes takes its first turn at once, where the original's waits one interval.**
  From the week-end audit (stage 4; read twice from the listing). The constructor `FUN_004758f0` writes `0x3e` to
  `+0x80` (`0x004759c4`) and the clock plus that `0x3e` to `+0x7c` (`0x004759df`). `ParkWorld.People`'s made sprite
  leaves `+0x7c` nought, says the constructor does, and writes no `+0x80`. Confirm: a guest made here and saved,
  the two words read in the file and beside the original's for a guest it made.

- [ ] **Q275. A lookup record's flag `0x10` is set on records that have no position.** From the week-end audit
  (stage 4; read twice from the listing). `FUN_0044a870` tests the node word's `0x400` (`0x0044a924`) only inside
  the branch its file flags' `0x30` opens (`0x0044a904`). `ParkModelTables` sets `0x10` from the node's `0x400`
  alone. Confirm: the records of every model in the nine park files compared with the tables built here, and a
  node with `0x400` and no position named.

- [ ] **Q276. A rider's head is always drawn from their own bank.** From the week-end audit (stage 4; read twice
  from the listing). `FUN_004fcac0` gives the rider's own `mSpriteID` only where a costume bank (kind 3) is loaded
  (`FUN_00541f60( 3 )`, `0x004fcac6`); where none is, it stores the rider's id at the world's `+0x1da708` and
  answers a draw among the kind 1 banks (`0x004fcae5`..`0x004fcb04`). `ParkGuestSprites` and `ParkFileWriter` give
  the rider's own bank in every case. Confirm: a park with no costume bank loaded, a rider's head bank read in
  both games.

- [ ] **Q277. Starting a clip binds every other channel's clip again.** From the week-end audit (stage 4; read
  twice from the listing). `FUN_00472f60` takes the outgoing clip's marks off (`FUN_00472310`, `0x004730a9`) and
  binds the new one (`FUN_00472d70`, `0x00473117`), nothing else. `ParkModelTables`' `Running.Started` also binds
  every other channel's clip again, so a track bit two channels' clips share stays set here where the original
  drops it; its summary gives the rebinding as the original's. Confirm: a model with two channels sharing a track,
  its node words after one clip is replaced, against the original's save.
- [ ] **Q572. An additive model's rotation key (`ANIM_ADDITIVE_ROTATION`).** From the run 6 review of Q554 (`handoff/review6/VERIFY-13.md`,
  Q554 5). On a model whose header word `0x30` carries `0x4` the engine composes a rotation key with the node's own turn
  (`FUN_00470d10`) rather than replacing it. Ours counts it, `ANIM_ADDITIVE_ROTATION`, in two places: `NodePoser.Pose`
  (one rotation track in the 22 additive clips that carry a position track) and `LobbyModel.Pose` for a clip that only
  turns, which `MeshRotator` would pose as a replacement (49 such clips, 50 tracks: the coaster pylons, Lost Kingdom's
  `coaster1` StdPylonI/R, `coaster3` looppylonr1/r2 and stdpylonb/i/r, `minecart` LoopPylonR1/R2 and StdPylonB/I/R among
  them). `ParkCoasterPylon` (Q706) loads the pylon models for Global 0's measure and poses its knot's turns itself, where
  the knot and its parent have no turn of their own. `FUN_00470d10` (`Anim_ComposeAdditiveTurn`) is a 3 x 3 product of the
  key's matrix (`Anim_QuatToMatrix3`, `0x00474120`) and the node's own rows; decode the order exactly and whether the
  additive restore `FUN_00473c70`/`0x00473d0c` puts the turn back every advance, then build it in both posers when the
  coaster pylons are drawn (Q594). Confirm by a pylon's turn beside the original's.
- [ ] **Q573. A node a clip's route moves, followed (`RIDE_NODE_ON_A_ROUTE`).** From the run 6 review of Q555 (`VERIFY-14.md`, Q555 5).
  `RideNodes.Follow` poses a thing's node tree for its particle records through `NodePoser`, which since Q561 carries a
  node along its route and since Q583 turns it to face along it, and through which `LobbyModel.Pose` now draws every
  route clip; `RideNodes.Stored` still counts a carried find of a node on a route, or under one, as
  `RIDE_NODE_ON_A_ROUTE`; the count is stale, as the node found is now carried and turned along its route. Every Lost
  Kingdom run reaches it: the first reach is that run's vehicle's sound start (the bus's `mk_bus_1`, the seaplane's
  `mk_plane_1`, the ferry's `mk_ferry_3`; 116, 178 and 94 counts in Q583's runs), not a particle record; whether any
  other clip with a route carries a particle record is not measured. Measure it across the shipped
  scripts and clips (`cliptracks`, the `.RSE` corpus); then drop the count, so the gap census no longer overstates the
  unbuilt work.
- [ ] **Q574. One node tree for every channel of a drawn model.** From the run 6 review of Q554 (`VERIFY-13.md`, Q554 2). The engine
  keeps one matrix per node for all of a model's channels; ours poses a clip that moves nodes through `NodePoser` and
  places every mesh from it (`LobbyModel.PlacePosed`), and a clip that only turns through `MeshRotator`, so a
  rotation-only clip on a channel posed before a node-moving one is drawn at rest until it is posed again, the next frame
  (said at `LobbyModel.Pose`). No Lost Kingdom model shows it: the Inca Totem's rotation-only channels are 1 to 3, above
  its node-moving channel 0; other themes not swept. Sweep every multi-channel script's roles against its clips; where a
  model is reached, pose every clip of a model that has a node-moving clip through the one tree.
- [ ] **Q725. The Lost Kingdom speakers' raw vertex frames are skipped.** From the upstream look, 2026-10-19
  (`verify-models.md` L3; FileFormats `models.md`, "Raw vertex frames"). `AnimationFile` skips every track that sets bit
  `0x4000` (30 in the game); Lost Kingdom has the 8 of `speaker1c` to `speaker4c` (4 x 16 vertices and 4 x 8). The data is
  read: each track's positions are whole frames, the descriptor's `+0x0C` plus one of them (30 of 30), indexed by the track's
  index table. Classify first (rule 3): GAP if a placed Lost Kingdom thing plays a speaker construction clip, otherwise dead
  by CONTENT. **Not settled:** whether the original interpolates between the whole frames (the second fork review reads
  `0x00471aa1` and `0x00471943` as blending one address with t and 1 - t; not re-read here) and whether `+0x0C` or the
  block's last frame bounds the frames (equal in all 30). Re-read both in Ghidra into `docs/exe/` before building; until then
  count the unbuilt path `RAW_VERTEX_FRAMES` (rule 4). Confirm: a speaker's construction beside the original's.

- [ ] **Q278. An entertainer whose bank has no state group performs on animation 0.** From the week-end audit
  (stage 4; read twice from the listing). `FUN_00541fa0`'s count is tested for not 1 (`0x004d47cc`), so nought
  takes the modulo arm and queues `0xd` plus a draw modulo `0xffffffff` (`0x004d47d8`..`0x004d47e1`).
  `StaffBehaviour` takes that arm only above 1, counts `ENTERTAINER_BANK_WITHOUT_A_STATE_GROUP` for nought and
  starts on animation 0, which its remarks give as a deviation no shipped bank reaches. Dead by content today:
  build it as the original's or leave the count, on Alexah's word.

- [ ] **Q279. `Level.WrittenCells` writes a footprint cell on tile 8 only where the cell joined a footprint or
  changed its type or parent.** So a file's thing moved and put back on its own anchor (sell then buy) goes out
  with its unchanged cells on bare ground's tile 55, which the sale left and `ParkBuilding.Stamp` keeps. Every
  footprint cell should go out on tile `(0, 8, 0)` as every footprint cell of seven park files is
  (`docs/exe/saves.md`, "OpenTPW's writer, a thing bought and a thing sold"); a test that sells the Drinks Shop,
  buys item 1203 on (43,30) again and reads `WrittenCells` for that cell's `TileIndex` would show 55 From the
  week-end audit (stage 6, the second reading of commit `8422bffa`; read from the code, not reproduced in the
  game). `OpenTPW/World/Level.cs`, line 907.

- [ ] **Q280. A file's bumper ride with a peep on its leaving or boarding list and no car is written over where it
  lies once the running lists are empty (`Level.WritePark`.** `Take`: only `CarsOf` is asked of the file), so the
  file's type 7 and 8 chunks stay and the next load puts those peeps back on the lists. It should be taken out and
  written whole whenever the file holds any chunk but the record and the close under its handle, as `FUN_005428e0`
  writes the lists as they stand; a test that loads the commit's own "Boarder" file, empties the list and writes
  again would show the stale chunk From the week-end audit (stage 6, the second reading of commit `05e54382`; read
  from the code, not reproduced in the game). `OpenTPW/World/Level.cs`, line 587.

- [ ] **Q281. A member of staff who reaches the picket keeps the facing their walk ended on, and turns on the spot
  from there.** The original's state setter writes nought to the facing as state 5 is entered (FUN_005054d0 case
  5, 0x00505582, beside the speed and animation 0x13 that Staff.SetActivity already takes from it), so a striker's
  turns start from 0; a test that walks a member to the picket and reads PeepWalk.Heading would show it. From the
  week-end audit (stage 6, the second reading of commit `0db41100`; read from the code, not reproduced in the
  game). `OpenTPW/World/Park/StaffBehaviour.cs`, line 432.

- [ ] **Q282. A handyman's thought bubble is taken away only on his own sweep in four, as the other kinds' are, so
  it lasts 13 to 16 sweeps.** The original's handyman pre-step FUN_004d7060 ends by calling the expiry
  FUN_0050be40 ungated on every sweep (0x004d70ee; docs/exe/ride-operation.md, the pre-step table's note on
  0x004d70f1), so his lasts exactly 13: a test that gives handyman 25 a bubble on mGameTick 1001 and steps the
  park to 1014 would show it still there. From the week-end audit (stage 6, the second reading of commit
  `738789d6`; read from the code, not reproduced in the game). `OpenTPW/World/Park/ParkPeople.cs`, line 2144.

- [ ] **Q283. `ParkParticles.Edit.Start` passes a directed start's `Direction` on to the linked effect when the
  link does not follow (`its = spawn`, then `Start( its with { Template = linked } )`).** So the linked emitter is
  written aimed and with no draw of the generator. `Particles_SpawnFull` starts its linked emitter with the plain
  `Particles_Spawn` (`0x00521cf2`), so the linked start must carry no direction; a test starting effect 51 (links
  54, not following) with a direction and holding emitter 54's +0x38 to the template's (0, 31, 0) and the seed
  three draws on would show it From the week-end audit (stage 6, the second reading of commit `d490bc90`; read
  from the code, not reproduced in the game). `OpenTPW.Files/Formats/Save/ParkParticles.cs`, line 376.

- [ ] **Q284. The console's `particles <slot>` reads a slot that is not a number as slot 0 and prints that
  emitter's cloud without saying so (`DebugConsole.cs`, `Argument( 1 )` falls back to 0).** It should reply that
  the slot was not understood, as it does for a slot out of range; `particles abc` in a park with an emitter in
  slot 0 shows it. From the week-end audit (stage 6, the second reading of commit `03326e26`; read from the code,
  not reproduced in the game). `OpenTPW/Client/Diagnostics/DebugConsole.cs`, line 1804.

- [ ] **Q285. Seven unbuilt paths the game reaches count nothing.** From the week-end audit (stage 6, the second
  reading of each commit; read from the code). `CLAUDE.md` rule 4 asks each for an `Unimplemented.Report`; build
  the path where it is small. One bullet a path.
  - A park loaded from a file with a camera module opens on the default view and counts nothing:
    `ParkWorld.Camera.Saved` is read from the file and used by no load, and `Level` reports only
    `PARK_ADVISOR_SAVED_HISTORY` beside it. The original's loader `FUN_0042cec0` puts the zoom, rotation and point
    back (`saves.md`, "Loading the modules", Camera), so until Q247 builds it the load should call
    `Unimplemented.Report` under a name of its own, shown by the `unimplemented` census after a Load Park.
    (`OpenTPW/World/Level.cs`, line 282; commit `2bdbd7fd`)
  - Built by Q241j: `Level.WritePark` writes `gms.dat` first and installs the idle mode before the park, as the
    original's save does on every call; nothing of this bullet is left.
  - `Peep.Tick` leaves the cell's region-effects term out of every needs turn and counts nothing, though the park
    has kept the cells since Q257e (`ParkState.Effects`). The original adds the cell's happiness, illness and
    hunger words there (`FUN_00501650` through the getter `FUN_004d8410`), so until it is built the turn should
    call `Unimplemented.Report`, as rule 4 asks and Q157 already lists. (`OpenTPW/World/Park/Peep.cs`, line 716;
    commit `8b70a8d8`)
  - A handyman loaded from a park file in state 8 or 9 (the walk to litter and the sweep, FUN_004d73c0's cases 8
    and 9, set by FUN_004d7330's table at 0x004d73a4) takes `StaffBehaviour.Step`'s `default:` arm, stands for the
    rest of the session and is counted nowhere. It should at least call `Unimplemented.Report` and fall to idle so
    he decides again, until the litter job is built; Alexah's `jungle/New Save.TPWS` and `jungle/autosave.TPWS`
    each hold four such handymen, which a load and the `staff` census would show stuck at `st 0x8` and `st 0x9`.
    (`OpenTPW/World/Park/StaffBehaviour.cs`, line 579; commit `757c46a6`)
  - The name box of Save Park and of the new player dialog ignores Delete, Left, Right, Home and End and counts
    nothing (`WindowStack.Keyboard` hands a `UiEdit` typed text and Backspace alone, and the caret is always at
    the end). The original's box answers each in `FUN_00667a72` (`0x2e00`, `0x2500`, `0x2700`, `0x2400`, `0x2300`,
    Shift extending the selection), so either they are built or the keys are counted where they are dropped.
    (`OpenTPW/UI/WindowStack.cs`, line 745; commit `d2052d84`)
  - A load drops the cars, riders and two lists of a saved track ride whose BumperType has no record here (every
    type but the Hot Pot's -1) and counts none of them (`Open` counts the ride alone, as `BUMPER_TEMPLATE_n`, and
    only for the family's -3, -6, -11 and -14), where the build before Q257p counted each car chunk as
    SAVED_TRACK_RIDE_CARS. It should count each dropped car, rider and listed peep, since the loader's car arm
    (FUN_00543560, chunk 5, docs/exe/saves.md "OpenTPW's reader, a track ride's cars") restores a car for any ride
    its handle names; a test with a type -4 ride and one car chunk would show the census unmoved.
    (`OpenTPW/World/Park/ParkBumperCars.cs`, line 522; commit `a96db38d`)
  - A guest's judgement of the entry fee pushes no sample to the park analyser and counts nothing:
    `ParkAdmission.OpinionOf` returns its band and `PeepBehaviour.Judge` acts on it. The original's `FUN_004ff5b0`
    calls `FUN_004c74b0( 0, byte )` on each of the four compared arms (`0x004ff693`, `0x004ff6b5`, `0x004ff6f0`,
    `0x004ff715`; not on the early-out), and kind 0 is the one ring anything reads, the advisor's messages 91 and
    92 (`ride-operation.md`, "The analyser's samples"), so it should be built or call `Unimplemented.Report`.
    (`OpenTPW/World/Park/ParkAdmission.cs`, line 303; commit `542b67e4`)

- [ ] **Q286. Seven tests cannot fail for what their names say.** From the week-end audit (stage 6, the second
  reading of each commit). Each passes with the bug it is named for put back; strengthen each and prove it with
  `mutate.py`. One bullet a test.
  - ASavedStampIsKept reads a stamp of nought from the shipped park and then checks only that the Staff
    constructor copies a value handed to it, so the entertainer's read of mTimeStartedEntertaining at record +503
    can be moved and the suite stays green. It should write a park with a performing entertainer whose stamp is
    not nought and read it back, as ParkMechanicRepairTests does for the mechanic's +503, the layout being
    FileFormats saves.md's entertainer row (mTimeStartedEntertaining 4, mNext 2).
    (`OpenTPW.Tests/ParkEntertainerPerformanceTests.cs`, line 263; commit `63660f76`)
  - No test reaches a whole purchase, so the one line that stamps a bought toilet's, camera's or fireworks' region
    effect (`ParkBuilding.Build`, the `StampEffects` call after `AddObject`) can be deleted with the suite green;
    `ParkRegionEffectsTests.Bought` calls `StampEffects` itself. A test should buy a Security Camera through
    `ParkBuilding.Build`'s own path with objects that place, and read security 20 on its cell, which is what
    `0x004db458` does and what q257e's mutation run showed only the game run catches.
    (`OpenTPW.Tests/ParkRegionEffectsTests.cs`, line 143; commit `8b70a8d8`)
  - TheSavedStampIsReadAndKept asserts that no other kind of staff reads a researcher's stamp, but the shipped
    save holds nought at +503 for all four other members, so the assertion passes with the model guard in
    ParkWorld.ReadStaff removed (mutation m38 and m38b of q134/mutations.txt survived). It should read a record
    built with a non-zero +503 for a mechanic, a handyman, an entertainer and a guard (bytes patched in a copy of
    Easymode.TPWI, or a park written by ParkWorld.People's writer with an entertainer's stamp set) and assert
    nought, which m38 would then fail. (`OpenTPW.Tests/ParkResearcherResearchTests.cs`, line 109; commit
    `50516f7c`)
  - No test holds the range a bumper ride's engine voice is started with: taking `range: rides.RangeOf( effect, 0
    )` off `ParkCarSounds.Play` fails nothing (`q265/mut-run1.out`, m21 survives), and only the game run
    `q265/engine.py` showed it. A test should stand a track ride's script far enough to start the engine voice and
    read its range as the variation's own, 100 for the Hot Pot's (`docs/exe/audio.md`, "Measured here,
    2026-10-10"). (`OpenTPW/World/Park/ParkCarSounds.cs`, line 92; commit `a069af13`)
  - `TheBootsSoundIsPlayedAtItsVariationsVolume` asserts `UiSounds.BootVolume` alone, and `UiSounds.Boot`, which
    hands that volume to `SoundCategory.Play` for effect 210, is called by no test, so a `Boot` that plays at
    volume 1 or plays the click's effect passes the suite. `Boot` answers nothing and plays only once sound is up
    (`Audio.Ready`), so it wants a seam first and then a test through it (the voice played: its clip and its
    volume of nought), which is known by putting `1f` in the `Play` call at
    `UiSounds.cs:73` and seeing the suite stay green. (`OpenTPW.Tests/ParkCountedPathsTests.cs`, line 205; commit
    `da9443ff`)
  - The test `ARideBoughtFacingAPathKeepsItsLinkToItsQueue` builds its purchase by hand and calls
    `ParkBuilding.LayEnds` itself, so no test fails when `Build` stops calling `LayEnds` (`ParkBuilding.cs:164`)
    or calls it before `Stamp`. A test should drive `Build` (or `Buy`/`PlaceCarried` on a level) for a Crazy Ape
    on (41,22) and read cell (42,22) links `0x01` and a queue of one cell; putting the bug back at line 164 shows
    the gap. (`OpenTPW.Tests/ParkFileWriterBoughtAndSoldTests.cs`, line 647; commit `2dec6b66`)
  - The test `AThingsVoiceIsSoundedAtTheMiddleOfItsCells` checks `ParkObjects.BoxMiddle` alone, so putting the
    scream back at the thing's corner in `TrySoundPlace`, or not keeping the footprint in `Place`, fails no test
    (q266/mutations.py m08 to m10 fail none). A test should stand a thing in a `ParkObjects` and read
    `TrySoundPlace` for it, the Belly Bounce at (51,23) answering (525, 250), so the wiring is held by the suite
    and not by one game run. (`OpenTPW.Tests/ParkFootprintTests.cs`, line 141; commit `9ec9a7a9`)

- [ ] **Q287. ParkHireScreen.Selected previews a highlighted candidate's wage with ShowMoney( person.Wage ), and
  Update() calls ShowMoney() with no wage on every frame (WindowStack.OnUpdate).** So the staff-costs preview is
  overwritten a frame later and the player never sees it. ShowMoney also never takes the previewed wage off the
  balance cell, which its own parameter remark says the original does (message 0x401); known by reading lines
  351-417 and WindowStack.cs:330, not run, and no test covers the cell From the week-end audit (stage 6, a sweep
  of the source settled by a verifier; read from the code, not seen in the game).
  `OpenTPW/UI/Park/ParkHireScreen.cs`, line 417. Confirm: a candidate highlighted on the hire screen, the
  staff-costs figure photographed.

- [ ] **Q288. Names and log lines that say what the code does not.** From the week-end audit (stage 6, a sweep of
  the source settled by a verifier). The audit changes no code, so each is left as it is: rename or reword, one
  bullet each. No game run.
  - ModelFile.Mesh.VertCnt is assigned the mesh's material count (the ushort at mesh +0x5A), while the real vertex
    count is in VertexCount, and nothing in the source reads VertCnt. Rename it MaterialCount or label it dead by
    CODE; known from ModelFile.cs lines 197, 414 and 490 and a grep of source/.
    (`OpenTPW.Files/Formats/Model/ModelFile.cs`, line 197)
  - ParkParticles.OnScreenAt (0xc1) names the template byte Particles_Spawn tests at 0x00521e98 to answer nought
    while the header's +0x08 is set; it is not the on-screen flag, which is +0x05, and ParticleLibraryFile already
    calls the same byte Suppressible. Tp2.plb sets it on 93 of 101 named effects, world and screen alike, so
    rename it to match (and OnScreenOnlyAt, the header's +0x08 the same test reads, with it).
    (`OpenTPW.Files/Formats/Save/ParkParticles.cs`, line 76)
  - ParkParticles.HoldsParticlesAt (+0x05) is the emitter's drawn-on-the-screen byte, which Start reads only to
    give a world emitter an empty box and a screen one six noughts. The name says something else (particles are
    held at +0x06/+0x08); FileFormats particles.md 0x05 and ParticleLibraryFile.OnScreen name the byte, so rename
    it. (`OpenTPW.Files/Formats/Save/ParkParticles.cs`, line 52)
  - SaveReader.ReadFile throws 'File version is not 133.' when the byte at 0x608 is not 0x85, and that byte is the
    last of the big-endian magic 0x01221985 at 0x605, not a version. The file's own comment above the read and the
    FileFormats saves page (header table, 0x605) both say so, so a bad file is reported under the wrong label and
    three of the magic's four bytes are never checked. (`OpenTPW.Files/Formats/Save/SaveReader.cs`, line 94)
  - SaveReader.ReadFile names the dword at 0x604 `fileType` and the byte at 0x608 `fileVersion`, and never uses
    the first. The comment above them and the FileFormats saves page say they are the zero byte with the magic's
    first three bytes and the magic's last byte, so the names contradict the file's own account; one piece of work
    with the bullet above (read the magic whole at 0x605 and check it, or rename).
    (`OpenTPW.Files/Formats/Save/SaveReader.cs`, line 90)
  - PutPeople throws 'thing N, an object bought, is in the file or the park already' for four conditions, one of
    which is a made record whose length is not the object record's size. That is read from the one `if` at
    ParkWorld.People.cs 400-401; the length test wants its own message (MadeObjectRecord always makes the right
    length, so it is a guard that cannot name itself when it fires).
    (`OpenTPW.Files/Formats/Save/ParkWorld.People.cs`, line 402)
  - SaveFolder.LoadConfig logs "Saves: read the options from <path>" after ConfigFile.Read returns null for a
    version-0 Config.tcf, when nothing was applied and the defaults stand, and equally when a file of an unknown
    version was applied but left _unreadConfig set. In both cases a later SaveConfig refuses to write and the
    load's log never says why; known from SaveFolder.cs:57-75 and ConfigFile.cs:61-66, by code only.
    (`OpenTPW/Client/SaveFolder.cs`, line 75)
  - SaveFolder.DeletePlayer logs "Saves: deleted the player '<name>' in slot N" whether the folder was absent or
    DeleteFolder left it behind because it held a dot-named entry, which its own doc comment says happens. Known
    from SaveFolder.cs:477-480 and 509-530, by code only; the log should say when the folder was left and what was
    in it. (`OpenTPW/Client/SaveFolder.cs`, line 480)
  - The console's `bumpers` reply heads its report with `bumpers N`, where N is `report.Count`: one line per
    bumper ride plus one per live car (DebugConsole.cs:807, :818, :833). The label reads as a count of rides and
    is not one; known by reading the code, not run. (`OpenTPW/Client/Diagnostics/DebugConsole.cs`, line 833)
  - The debug console's camcorder reply (ParkCamcorderCameraMode.State) prints `eye=` as the raw ground under the
    stand plus EyeHeight, while the camera is placed at the eased _groundHeight plus EyeHeight. The label names a
    height the eye is not at while the ease catches up or the clock is paused, known from reading State() against
    Place( GroundUnderStand() ), not from a run. (`OpenTPW/World/Park/ParkCamcorderCameraMode.cs`, line 830)
  - SoundCategoryFile.Variation.GapByParameter holds the first controller's whole mask (the u16 at +0x18: 1
    volume, 2 pitch, 4 the wait), and ParkAudio.Controlled, ParkAudio.NamesOnVolume and ParkCarSounds test its
    volume and pitch bits beside SecondMask, so the name describes one bit of three. This is known from
    SoundCategoryFile.cs's own FirstKey parameter note and from 0x006c3d8a, which tests the word against a passed
    bit; a rename (to pair with SecondMask) touches ParkAudio, ParkCarSounds, ParkScreams and four test files.
    (`OpenTPW/World/Park/ParkAudio.cs`)
  - NodesFor's catch branch warns that a thing "walks or seats riders ... every leg is the shortest and no head is
    hung" for any script whose model will not read, including one that reaches it only through UsesEffectNodes
    (ADDOBJ), which walks nobody and hangs no head. That is known from the code: the early return lets an
    ADDOBJ-only script through, and the null-model branch below guards the same message with `WalkSlots != 0 ||
    UsesHeads` while the catch does not. (`OpenTPW/World/Park/ParkRides.cs`)
  - PaceLocal (local 16, the instance's +0xc4) and PaceValue (0x1200) hold the draw's flags word, which the
    DrawFlags property returns and line 508 writes from a parameter called flags. The names say pace, which
    nothing in the file decodes or uses; this is known from the constants' own comments and the three uses at
    lines 321, 373, 468 and 508, so they should be renamed DrawFlagsLocal and DrawFlagsValue.
    (`OpenTPW/World/Park/SpriteScript.cs`)

- [ ] **Q289. Two members only tests call, which a built path needs.** From the week-end audit (stage 6, the
  dead-code census). Neither is dead by code: placed content reaches the path each belongs to, and the path is
  unbuilt.
  - The constant is read only by PeepSteeringTests:29 (whose message is "a tenth, for crowding - not built"); no
    production code applies it and no other 0x1999 literal exists. Its doc and PeepWalk.cs:36-42 say the original
    registers a third steering behaviour, separation, at FUN_0050fdf0 and that leaving it out "makes crowds walk
    through one another" - every walking peep in a placed park reaches that path, so the unbuilt thing is a GAP,
    not a dead member. Not checked in Ghidra here. Two things the finder may want: (1) both comments give as the
    reason that the project keeps no per-cell lists of who stands where, but ParkState now keeps exactly that
    (_onCellNext, ParkState.cs:1294-1369, "the original's thing +10"), so the stated reason looks stale; (2) grep
    of docs/QUEUE.md and docs/PLAYER-GAPS.md (held copies) finds no line for separation or crowding.
    (`OpenTPW/World/Park/PeepSteering.cs`, line 47, `SeparationWeight`)
  - Called only by RideStateTests:85 and RideScriptRunTests:280, and it is the only writer of _finished. The
    reader is live: RideScript.cs:2291 stores Ride.TakeRider() for COAST 3. ParkRideOperation.cs:335-338 says so
    at the site: "COAST 3 is implemented and its coasters still never dismiss anybody, because it drains the
    finished-rider queue that nothing fills ... a missing simulation, not a missing opcode", and
    docs/exe/ride-operation.md:4263 lists COAST 3 as how Coaster1, Coaster3 and Minecart dismiss - all three are
    Lost Kingdom rides (levels/jungle/rides in the .RSE corpus). So content reaches the path and the filler is
    unbuilt. Unproven: I did not check whether Easymode.TPWI places one of the three or whether they are only
    buyable. FinishRider's own doc calls it "the seam where a ride simulation will connect" but does not say only
    tests call it. (`OpenTPW/World/Ride/RideState.cs`, line 138, `FinishRider`)
  - **Note (fork review 2, 2026-10-16, `verify-coasters-run.md` L16):** the filler is the coaster's station step
    `0x0043b8b0`. The ring COAST 3 drains has three writers: `0x0043b480` unloads a train's riders as it arrives in
    the station (from the station step, from the state setter `0x00435730` on 4 or 8 to 2, and from `0x0043ba20`),
    only in state 4 or 8 with mode `0x40` clear; `0x0043b390` moves the admission queue across when the ride closes;
    `0x0043b130` refills it from a loader. Seating draws the game's own generator at `DAT_007b0958`; a rider the full
    ring cannot take is taken off the car and queued nowhere. The rest of the coaster's running is Q405.
  - **Note (fork watch 2026-10-16, `verify-world.md` L14):** the separation pass is `0x005102f0`, called directly
    (`0x0050e166`) with radius 2 x `[thing+4]` and callback `0x0050dfc0`: a window of +-ceil( radius / `0x10000` )
    cells (3 x 3 only for a radius of one cell or less), rows then columns, one thing list per cell (`cell+0x2b8`,
    next `+10`) in its own order; kinds 1, 4, 5, 6, 7, 8 and `0x12`; itself excluded; distance non-zero and strictly
    inside the radius; it stops after the **fifth** neighbour. Whether guests reach it is not read.

- [ ] **Q290. The queue's stamp is taken before the queue is measured, where the original measures first.** From
  the week-end audit (stage 8, the older decode read a second time from the listing; verified once, not reproduced
  in the game). `ParkState` takes `NextCounter()` before `InvalidateQueue` and the back-cell lookup, and its
  comment says so, where the executable measures the queue first and takes the counter after (`ride-operation.md`,
  the row for the queue's remeasure). `OpenTPW/World/Park/ParkState.cs`, line 1688. Confirm: the back cell's stamp
  read in both games after one queue edit.

- [ ] **Q398. Each ground cell is split on its own diagonal.** From the fork review 2, 2026-10-16 (`verify-formats.md`
  H1, checked again in Ghidra by the main session). `ParkGround` splits every cell from (x, y) to (x + 1, y + 1) and
  says so at the site; the original draws each cell by its flag `0x2` and takes heights by `0x4`, the same split
  (`park-engine.md`, "The triangle diagonal"; FileFormats `models.md`, "A cell's diagonal"). In Lost Kingdom 2,708 of
  the 7,001 ground cells take the other diagonal. Fix the mesh and this game's height under a point (the camera's, a
  draped model's, and `FUN_004527f0`'s users, which split only the cells the load marks `0x800`). Confirm: a slope
  cell carrying `0x4` shot beside the original's, the same camera.

  - **Note (fork watch 2026-10-25, verify-all.md H1, N2):** a draped model's height is taken per vertex (`0x00469a80`): the vertex goes through the
    instance matrix, the cell is (x - grid `+8`) / grid `+0x10` and the same for z, the cell flag byte `& 4` picks the triangle
    (`0x0046a34c`: clear, fz <= fx takes h00, h10, h11, else h00, h01, h11; set, 1 - fz <= fx takes h01, h11, h10, else h00, h10, h01),
    and the vertex moves by h - H, H the instance's height. The kart, water, queue and boundary-hoarding kinds also carry the
    run-time header flag `0x80` (`0x004625f4`), the landscape's height (`FUN_004527f0`), so they take this split too.
- [ ] **Q399. Ride boards lettered as the original letters them.** From the fork review 2, 2026-10-16
  (`verify-objects-signs-other.md` H1, H3, M1). `ParkObjects.BuildSign` letters the `.sam`'s `Name`, split; the
  original letters the item's `OBJECT_NAMES` rows (FileFormats `sgn.md`): in English Lost Kingdom only Dino Karts
  differs, "DinoKarts" on one line. `SignFile.cs` reads neither the width percent (`+0x144`, not 100 on 16 of Lost
  Kingdom's 17 boards) nor the line's y (`+0x148`); the original fits a line by a binary search on `LOGFONT.lfWidth`
  (`0x005eab70`). The ink block's four unknown fields are the grow or shrink radius, the blur's sigma, dx and dy (a
  Gaussian of 3 + trunc( 2.5σ ) taps). Confirm: Dino Karts' board and one other beside the original's.
  - **Note (upstream look 2026-10-19, `verify-images.md` L1):** the ink block is the letters' EDGE, not their colour. The
    letters are filled from the line's 16 x 128 fill (bytes 1 to 3 are R, G, B, tiled, `FUN_005eb8a0`; `park.md`), the ink
    shaped (radius, sigma, dx, dy) under them. Photo: the Wonder Land board's "Wonder" is lime and "Land" yellow-orange,
    both edged olive, and `Fan_gate.sgn`'s two ink blocks are the same olive over two different fills. FileFormats
    `sgn.md` "Line ink" and the `SignFile`/`SignTexture` comments now say so; `SignTexture` still letters in the flat ink
    colour. Settle in Ghidra first, in `FUN_005ecb40`, for modes 1 and 2: the call order of `0x5EBF10` (shape the ink mask),
    `0x5E7F60` (blend the ink through it) and `FUN_005eb8a0` (fill the unshaped glyph mask); whether `0x5EB8A0` blends by
    coverage or overwrites and what it writes into byte 0; how `+0x24`, `+0x28`, `+0x3c` map the 128 fill rows onto the line
    (its height or the whole board); and in mode 2 (`0x5E77C0`, `0x5E7EF0`) which line's fill and ink are used (19 of the 27
    `(2,2)` signs have two different fills; Space's photograph looks like fill 1, unproven). 162 ink blocks ship, 101 with a
    non-zero offset. A hypothesis only: 47 of the 168 fills have byte 0 = 0 throughout (the rest 255), among them both of the
    jungle park gate's (`gates.sgn`), `Jun_isle`'s and five jungle ride boards'; a fill byte 0 of 0 landing in the
    A4R4G4B4 alpha nibble would blank the letters, a rival to `textures.md`'s blame of Wine's GDI fonts for the original
    under Proton lettering no name on the jungle gate. No Proton frame of a byte-0-is-255 board's lettering was found to
    split the two; film one.
- [ ] **Q722. A park's name split over the sign's two lines at the space nearest the middle.** From the upstream look,
  2026-10-19 (`verify-images.md` L4). `SignTexture.SplitName` splits at the FIRST space; the original searches outward from
  the middle of the string for a space, alternating (`FUN_00467a80`; FileFormats `sgn.md`). Lost Kingdom reaches it: "Tom
  Tom Twister" is "Tom" / "Tom Twister" here and "Tom Tom" / "Twister" by the centre rule (the space at index 7 of 15). Q399
  covers where the text comes from and its widths, not the split. **Not settled:** the tie. For a name whose two nearest
  spaces are equally far from the middle ("The Hot Pot": spaces at 3 and 7 around 5) the left gives "The" / "Hot Pot" and
  the right "The Hot" / "Pot"; read `0x00467b41` in Ghidra for the start index (len / 2 or not) and which side is tested
  first. The comment on `SplitName` says the deviation. Confirm: a three-word name on a gate or a ride board beside the
  original's.

- [ ] **Q400. Animated textures.** From the fork review 2, 2026-10-16 (`verify-formats.md` H2). 19 texture slots with
  several frames in 9 jungle wads (the queue fire, the 5x5 rock's waterfall, the monkey's face, `tvsim`, the hyenas,
  the puzzle, `squark`) and 49 jungle clips with frame tracks; nothing here shows a frame past the first. The layout
  is FileFormats `models.md` (the frame count at `+0x0A`, "Texture frame tracks"). The engine: sampler `FUN_00470c90`
  from the clip update `FUN_004721f0` and the channel advance `FUN_004735d0`, when the block's flag `0x2` is set and
  `[0x007b4980] & 8` clear; loader `FUN_00575c60`, `FUN_00575e10`; the draw reads the frame at `0x0057a47e`,
  `0x005805ed`, `0x005818c5` and `0x00581e48` while `DAT_008bd550 & 0x40`, which comes from `DAT_00785884`, very
  likely `GraphicalOptions.ANIMATINGTEXTURES` (inferred from the order in `FUN_0044d8f0`: read it first). Confirm: the
  queue fire burning beside the original's.

- [ ] **Q401. Self-lit materials.** From the fork review 2, 2026-10-16 (`verify-formats.md` M3). A material whose word
  `+0xC` carries `0x1` (tested at `0x00580c51`) gives every vertex the colour mask itself, white in play, in place of
  its lit colour: 929 of 12,960 materials, on signs, water, fire and lava. Read and drawn since Q652
  (`ModelFile.MaterialData.IsUnlit`, `LobbyModel.UnlitFlag`, `test.shader`'s `FLAG_UNLIT`), confirmed on the river's
  water alone. Confirm: a sign and the waterfall shot beside the original's, and the other LobbyModel draws the bit now reaches: the
  advisor's five mouths and his tickets (Q657), the queue torches (`quebin1`, `quebin2`, `queend`), the shops and the
  features (giftshop, icecream, coconut, burger, balloon, lavspurt, supbog, statue2) and the rides' (incagod, volcano, spider,
  bumper, junspray), each beside the original's. Seen at Q652: the bridge's waterfall
  curtain (`falls02`, `watersource`) is solid bright cyan here and faint and foamy in the original's frame
  (`q652/side-by-side.png`, top row), in main too; its texture's frames are Q400's question.

- [ ] **Q402. A refused purchase makes the original's error sound.** From the fork review 2, 2026-10-16
  (`verify-audio-lips-video.md` lead 5). The original plays `cat_ui` `0x1D` when the buy screen refuses a row
  (`FUN_004ac270` at `0x004ac7f6`: a mystery row short of tickets, a row dearer than the balance), when a Ctrl+click
  Clone is dearer than the balance (`FUN_004879d0`, the call at `0x00487b56`), and from `FUN_004af480` (`0x004af5d6`, a control of
  window stream `0x00755750` not identified). The buy screen's refusal, both arms, is built (Q709, `ParkBuyScreen.Chose`,
  `RefusedSound`), not yet caught by the disk driver; `FUN_004af480` is not built, and Clone has nothing behind it (Q672).
  Confirm: the sound caught by the disk driver on a refused row.
  - **Note (fork watch 2026-10-16, `verify-audio-fx.md` A3):** the list rows' click sound is built (`UiSounds`,
    `WindowStack`'s hook `0x00485780`): the control's message `0x100` handler (`0x004857b8`) plays `Select3` (189,
    `0xbd`) when style bit `0x10` of `[this+0x48]` is set (`0x004857cb`), else `0x1f`. Open: which park list rows the
    original's factories make with style `0x10` (sites `0x00493986`, `0x004939b0`, `0x00494628`, `0x0049463d`,
    `0x00496cea`, `0x00496cff`, `0x004b4c70`, `0x006604a1`, `0x0066051a`) and whether ours set `Toggles` for the same
    rows; which of them receive `0x100` is not read.
  - **Note (fork watch 2026-10-18, `verify-ui-hud.md` U3, `verify-world-b.md` L1):** the control of stream `0x00755750` is the ride
    window's **Buy Upgrades list** (`FUN_004ae560`, handler `0x004ae430`, rows from `FUN_004ae140`), and the `0x1D` it plays
    (`0x00486ae0`) is for an **add-on** row dearer than the balance only: a level row dearer than the balance gives no cue at
    all (`0x004af4e9`, strictly below). `ride-operation.md`, "The ride window's Buy Upgrades list". Q512 builds the list.
  - **Note (fork watch 2026-10-21, verify-audio-ui M1):** no list row receives `0x100`, so a row click is silent in the
    original, as ours. The control factory's table (`0x0065e4f2`, types 1 to 13) builds a button for type 2 only
    (`0x006688b3`, one call, `0x0065deae`); `0x100` goes out at nine places in the image: the button release
    (`0x006691f9`) and eight keyboard paths (`0x0048d83b`, `0x0049973e`, `0x0049ec53`, `0x004a6df5`, `0x004b640a`,
    `0x004b9f06`, `0x004bb020`, `0x004bfc29`); the row templates build types 1, 6 and 9 only (18 calls). The style `0x10` those
    rows carry is never read by the click hook (`0x00485780`). So the 2026-10-16 note's open sentence ("which of them
    receive `0x100`") is answered: none; nothing to build for rows. Our rows go through `LeftClickedAt`, silent
    (`WindowStack.Release` clicks only for a `UiButton { Clicks: true }`).
- [ ] **Q711. A list heading clicks Select3 (`0xbd`) in the original; ours clicks `BUTTON01` (`0x1f`).** From the fork watch,
  2026-10-19 (`verify-audio-fx.md` M2, "MISS (live in our code)"; `docs/exe/hud.md`, "Which window the click sound is for").
  **Insert next to Q402**, whose 2026-10-16 note asks which list rows make style `0x10` and whether ours set `Toggles`: this
  answers it for the headings. The click hook (`0x00485780`) plays on every message `0x100` by the RECEIVER's own style word,
  bit `0x10` (`0x004857cb`) giving `Select3` (`0xbd`), else `0x1f`. A list heading is a button (op `0xc`,
  `( 2, 1, n+0x10 )`, `0x006606a8`..`0x006606b2`) whose parent is the list, and every sorted list record carries `0x10` (walked
  `0x00750ab8`, `0x00750ba0`, `0x00750ca0`, type 7, flags `0x291`; the buy list `0x91`, the rest `0x291`). So a
  heading click plays `Select3`. Ours: the headings (`UiList.AddHeading`, `AddFramedHeading`; hire, buy, all-staff, all-items,
  visitors) are plain `UiButton`s with `Toggles` false and parent `UiList`, so `WindowStack.Release` plays
  `UiSounds.Click( toggle: false )` = `0x1f` (said at both sites). Build the rule the original has, not a per-button flag: toggle
  when the receiver (the parent) is a list flagged `0x10`. A radio group's children already toggle (`UiRadioGroup`). Not heard in
  either game yet. Confirm: a heading clicked on the hire list, caught by the disk driver, beside the original.

- [ ] **Q403. The balance's colour and the cost in hand.** From the fork review 2, 2026-10-16 (`verify-ui-hud.md` L5).
  The HUD letters the balance in one of six colours (table `0x00752e30`, index (balance + 999) / 1000 clamped to 0..5:
  red at nought or below, white over 4000), and control `0x30` shows the cost of the item in hand (`0x004a0d38`..`0x004a0d94`),
  only for mode types 3 and 4, and 8 when `[0x0079c658] & 1` is set (-9999 hides it, -999 resets it). The keys counter is skipped in Instant Action,
  the tickets counter runs in every mode. Read what this game does first. Confirm: the balance at 500 and 5000 beside
  the original.

- [ ] **Q419. The hire screen's candidate box is empty.** From the run review, 2026-10-16. On the same file the
  original's hire screen shows the chosen candidate's figure where this game's box is black (`q412/run-fix2/3-screen.png`
  beside `q412/orig/a-button.png`; the same in `q415`). The candidates' names differ too, which may be the pool's
  top-up (Q356's note); not looked into. Decode what the original draws there first. Confirm: the box beside the
  original's on the same file, a screenshot and the census of what is drawn.
  - **Note (fork watch 2026-10-16, `verify-audio-fx.md` O4):** the figure is made at `0x0049bb4a` to `0x0049bb64`:
    `0x00475a10( 0x0074f758, slot )` with the slot by role from `0x0049bb10` (5, 6, 4, 7, 8), the handle kept at
    `[0x007ca310]`, then `0x004755d0( handle, 8, 8.0f )`. The facing and frames 0 to 7 are inside the sprite script
    `0x0074f758`: not read.
  - **Note (play test 2026-10-21, note 18):** the hire screen shows no staff picture. Figure made at `0x0049bb4a` to
    `0x0049bb64`, sprite script `0x0074f758`, frames 0 to 7 (QUEUE:1170-1173).
- [ ] **Q404. A park six months in the red ends.** From the fork review 2, 2026-10-16
  (`verify-economy-guests-paths.md` L4, M2). Counted, `BANK_PARK_ENDS_IN_THE_RED`. What the end does is
  `ride-operation.md`, "The end of the park": world state 4, the end screen, `VAR_BREAKSTAT` 1 on every ride guests
  choose, the pending deletes flushed, the gate closed; the clock and the bank go on. The path tests no game type, so
  Lost Kingdom's Easymode reaches it. Confirm: a park held in the red to its end, beside the original.

- [ ] **Q452. Buy Rides lists a researched ride upgrade.** From the week-end audit, 2026-10-17 (`LEDGER.md` D1,
  `VERIFY-13a.md` Q349 1; `CLAUDE.local.md` says where). `ParkBuyScreen.Listed` lacks the original's `+0x284` skip
  (`0x004aaffc`), so a Full Simulation park with 1500 or 1501 researched lists Dino Karts Jump (750) and Dino Karts
  Tunnel (900), and a row puts one in the hand. Seen in the game on a copy of Alexah's Full Simulation autosave
  (`gp-fs.out`). It is Q221 (2), no longer dead by content since Q349. Confirm: the list without them, beside the original.
- [ ] **Q453. A park file's advisor poll cursor out of range stalls the game.** From the week-end audit (D2,
  `VERIFY-2a.md` Q316 1). `RestoreCursor` refuses only a negative cursor; at `0x7FFFFFFF` the first poll's `row + 1`
  overflows and walks 2^31 rows, about 87 s. The original sets any cursor of 351 or more to nought first (`0x0059c6a8`).
  Put the wrap back before the loop (the line removed as mutant r09) with an `int.MaxValue` test.
- [ ] **Q454. A new Instant Action player's first park clears the advisor's records.** From the week-end audit (D3,
  `VERIFY-1b.md` Q313 1). Event 10 (`RESET_FOR_EASY_MODE`, sent `0x0054f24e`, handled `0x0059af63`) sets every record's
  line to -1 and its said and slaps to nought, keeping the tick, on the player's first park only (the gms.dat byte
  read `0x005b0597`, written `0x005b0109`). Ours keeps Easymode.TPWI's said bytes, so 0xb9 and 0x11a are never said.
  `PlayerFile.FirstParkToCome` is never cleared here. Confirm: a new player's first park in both games, tick 877.
- [ ] **Q455. A click on the tab already down lifts it.** From the week-end audit (D4, `VERIFY-15a.md` Q364 1).
  `WindowStack` flips the radio button up and `Put` returns before setting it down; the original's group with style
  0x10 sets it down again (`0x0066a455`..`0x0066a468`; frames `q364/orig/b-*.png`). `ParkLocator` already works round
  it. Read each screen group's style first.
- [ ] **Q456. The entry price screen's Loans button in Instant Action.** From the week-end audit (D5, `VERIFY-16a.md`
  Q366 1). The original disables it in game type 2 (`0x00499008`..`0x00499030`), as staff costs' (`ParkStaffCostsScreen.cs`
  does it); ours takes the click, closes the screen and opens nothing.
- [ ] **Q457. The satisfaction bar is masked to a byte.** From the week-end audit (D6, `VERIFY-16a.md` Q367/Q368).
  `ParkItemsScreen` runs satisfaction through `Bar()`'s `& 0xff`; the original does not (`0x00494031`, `0x00494199`,
  `0x00495888`, `0x00495959`) and the setter clamps 0..0x400 (`0x0066d885`). A test with -1 and 256.
- [ ] **Q458. The staff screen's meters average as the original's.** From the week-end audit (D7, `VERIFY-16b.md`
  Q369 1). The original averages each member's `ftol(h) & 0xff`: the kind's meter an integer mean then
  `(m << 10) / 100` (`0x004c9cc0`), the all-staff meter a float mean times 1024 times 0.01f (`0x004c7cc0`, `0x00496a7b`),
  in double (`advisor-park.md`, the FPU's precision). Ours scales the raw mean: 896 against 892 on `q369/skill.TPWS`.
- [ ] **Q459. The happiness gauge keeps the mean's fraction.** From the week-end audit (D8, `VERIFY-4b.md` Q392 2).
  `FUN_004c7bb0` ends `fidiv` (`0x004c7c96`); the gauge is trunc(mean x 10.24) of 1024 (`0x004a1d3f`..`0x004a1d5a`).
  The units are built (Q244, `ParkGadget.GaugeUnits`); the mean handed to them is still whole, `AverageHappiness`'s
  comment calls `FIDIV` an integer division, and `TheMeanIsWholeAndRoundsTowardsNought` pins it.
  `GuestsInsideHappiness` is the exact routine.
- [ ] **Q460. Zlib textures round the chroma terms twice.** From the week-end audit (D9, `VERIFY-13b.md` Q355 1). The
  original stores the Cb and Cr terms as float32 at 1.5 x 2^23 (`0x00594f5e`, `0x00594f87`), integers before Y is added;
  `Held` rounds once: 933 of 986 files differ by 1. Green's rounding (the FPU mode) needs a breakpoint at `0x00594f72`.
  Q489 saw it through type `0x13` too: the running original's 16-bit surfaces of eleven jungle `pathtex` files hold
  each channel's top four bits, and where OpenTPW's byte ends in `0xf` about half (19,265 of 35,399) are one higher
  (`q489/PREDICTION.txt` a3, `conv3.py`); hold those files' nibbles to the original's once fixed.
- [ ] **Q724. A 0x13 image's channels rounded, as the original rounds them.** From the upstream look, 2026-10-19
  (`verify-images.md` L4). `textures.md` says `FUN_00595920` (the LZSS codec) indexes its channel tables "by the channel's
  rounded value"; `TextureFile.Decode` cuts each float channel with `(byte)Clamp( 0, 255 )`, a truncation, which is low by
  up to one level (mean 0.48 a channel, 2.6% of nibbles after the A4R4G4B4 cut, over 120 random 0x13 files). Reached by all 7,407 files
  that carry type `0x13`. **Settle first:** read the column pass and colour
  step of `FUN_00595920` in Ghidra (where the rounding falls: after the column pass, or on the colour step; the 12582912.0
  constant at `0x007026ac` is the codec's own), then match the original's decode of a few files, as Q483 matched the
  ground's. The comment at the cut says the deviation. Confirm: three Lost Kingdom `.wct` of type `0x13` beside the
  original's decode, pixel by pixel.
- [ ] **Q461. The tag bar's meshless buttons are disabled in the original.** From the week-end audit (D10,
  `TIEBREAK-Q311.md`). A type-0 tag's second button and a decline's erase button are shown and disabled (vtable +0x14
  with 0 at `0x0048e991`, `0x0048e43c`); the hit test skips a disabled control (`0x0065db3b`). Q427 made them take the
  pointer (`ParkGadget.cs`, `hud.md`, two tests). Needs Q66 (`HitTest` reads no `Enabled`) first, or the difference said.
- [ ] **Q462. The advisor's turn comes before the sweep's tail.** From the week-end audit (D11, `VERIFY-8b.md` Q318 1).
  The original walks every thing (his turn inside the walk), then `0x004d7b20` (`0x00516695`): arrivals, staff pool,
  calendar. Ours runs the arrivals and the pool before his `Tick`, so row 0x11 can be said a poll round early or late.
- [ ] **Q464. A written corner cell's made vehicles run newest first.** From the week-end audit (D13, `VERIFY-2b.md`
  Q342 1). With ferry and seaplane both made, `ParkWorld.People.cs` writes cell (0,0)'s chain oldest first; the
  original's file (`q257x/orig/fixed-by-the-original.TPWS`) and our running list run newest first. No effect on reading
  back was found.
- [ ] **Q465. A purchase's advisor post is not counted.** From the week-end audit (`VERIFY-10a.md` Q328 3). Every
  purchase reaches the original's 0xc9 or 0xce post (`0x004ac67e`..`0x004ac7d9`); ours neither builds nor counts it
  (`ParkBuyScreen.Chose`). The line is filed under Q420; add the `Unimplemented.Report` (rule 4).
- [ ] **Q501. A rider's head, a balloon and a thought bubble are drawn the original's size: shown.** From the review of Q470
  (`handoff/review3/VERIFY-6.md`, finding 4): Q470 drew every sprite through `Extent` (the span over
  `Camera.ClipPerWorld`), and the head (`HeadScale` 0.685), the balloon and the thought bubble go through the same
  `ParkGuestSprites.Corners`, but only a person's body was photographed on the fixed build, and no census of the
  original's sprite list held a head, balloon or bubble (they hold people alone; whether any of those carries `0x200` rests
  on the programs' static reading). Confirm in both games at one camera: a rider's head on a ride, a balloon and a thought
  bubble, their rows and columns against the decode (`original/spritepick.py` census with one of each on the list).
- [ ] **Q502. The lobby's fog is linear, 50 to 250, in the original; ours is `exp`. Decode first, then build.** From the review of Q481
  (VERIFY-9 Q481 1): the three lobby states that call `FUN_005d9680` (`OR AH,0x20` on render-word `0x008bcbc8`, at
  `0x005e1407`, `0x005e1980`, `0x005e24a2`) then `FUN_005d96a0( 0xff44ddff, 50.0, 250.0 )`, which writes `0x008bd47c`,
  `0x008bd490`, `0x008bd494`; the lobby's own initialisation (`0x005d8bef` to `0x005d8c10`) sets 50 to 300 with the bit
  clear. Ours is `Level.LobbyFogDensity` 0.025 through the shaders' `exp( depth * 0.01 )`, which has no start or end
  (`test.shader`, `water.shader`), and the lobby has not been photographed against the original's since parks stopped using
  the global fog. Decode which lobby states fog and which do not, photograph the lobby in both games at the same
  camera, then give the lobby the original's linear start and end (first person's fog, Q485, is the same linear fog,
  `FirstPersonFog` and the shaders' `g_vLinearFog`, worked out per vertex in the original's clip z: share that path).
- [ ] **Q503. The build markers load from `textures` at every texture quality.** The original's `FUN_0053d790` calls
  `FUN_0054dcc0` (0x0053d7b4) and takes `STexture` (`0x00763ee4`) for the three dynamic meshes where it answers 1
  (texture quality 0 or 1), as `ParkSea` and `ParkFlags` already do through `ParkSea.SmallTextures`. `ParkBuildMarkers.Load`
  always reads `generic/dynamic/textures/{name}.wct`; `blue.wct` (493 against 211 bytes) and `red.wct` (340 against 122)
  differ between the folders, `m_link` and `m_end` do not. Pass the quality in, load `stexture` at 0 and 1, and confirm
  it in the running game at low detail (a screenshot of a build square, before and after). Said at the site in
  `ParkBuildMarkers.MarkerNames`.
- [ ] **Q504. The sea's and the flags' phase is a float that grows without bound, and no one wrap keeps both seamless.**
  `ParkSea.Phase` gains 12 a second off `Time.Delta`; at a high refresh rate the step (12 x Delta) is rounded against the
  float's ulp after hours (numpy float32 at 144 Hz: rate x1.031 from 1.5 h, x0.9375 from 3 h, x1.125 from 6 h, x0.75
  from 12 h, x1.5 from 24 h, stopped at about 48 h). The original's is a float32 too (`DAT_00871f60`, `DAT_00871ed0`)
  but steps 0.4 a frame at 30 fps, so it is within 1.6% only to 12.1 h (phase 2^19) and stops at 2^23 (about 194 h).
  The sea's wave is periodic in the phase every 4 pi (s = ... - 0.5 phase; the table index moves by 4,096) but the flags'
  `Grid` is periodic every 20 (D = ... - 0.6 phase; the joint period of 3 and 4 in D is 12), and `ParkFlags` takes the
  sea's phase, so wrapping the shared phase at either breaks the other at the wrap. Fix: keep two phases (the sea's
  wrapped at 4 pi, the flags' at 20, both stepped alike from the same `Advance`), say it as a deviation at the site,
  and test that a phase either side of each wrap gives the same wave. Said at `ParkSea.Phase`.
- [ ] **Q505. The picker stands still in first person, a ride's view and the view of `0x10`.** From the review of Q472b (`handoff/review3/VERIFY-13.md`, q472b 1): the original skips the picker and the packer while
  `[0x790ab0] & 0x16` is set (`0x0054e2c0` `test al,0x16`, then `jne`; first person is `0x202`), so its cell stays where it
  was. Ours runs `ParkPicking.Update` every park frame, counted `PICK_IN_FIRST_PERSON` in `Level.Update` in first
  person and the End's ride view (`Level.InsideView`), so the crowd voice (`ParkAudio.cs`, the original's `0x0054f875`) counts guests round a cell that follows the pointer. Decode which
  modes set `0x4` and `0x10`, then hold the picker under all three bits. Confirm in both games: the crowd voice's count
  in first person against the cell before it.
- [ ] **Q506. A frame's second press: acted on at the park, or not (`PARK_PRESSES_IN_ONE_FRAME`).** From the review of Q476
  (`handoff/review3/VERIFY-14.md`, finding 2): `Level.WorldClick` counts a frame's second left press and does not act on
  it (`Unimplemented.Report( "PARK_PRESSES_IN_ONE_FRAME" )`), because the first may open a window the second would land on
  and the interface has dealt the frame's presses out by then. Two presses inside one 31 ms frame is rare with a mouse. Decode
  what the original does with two presses delivered between two park ticks (the cursor callback `0x00486c70` posts each
  through `FUN_00658af1`; where the park's mouse proc `FUN_004879d0` runs against the drain), build it or record that one is
  enough, and drop the counter or say why it stays.
- [ ] **Q507. Does the original send a frame's second press to the control the first press's click left?** From the review of
  Q476 (VERIFY-14, finding 4; UNSETTLED). `WindowStack.Update` hit-tests the hover once and gives every step of
  `LeftSteps` (press, release, click) to that one control, so a second press in the same frame reaches the control the first
  press clicked even if that click closed or replaced it. The original's press poster `FUN_00658af1` addresses the hover current at
  posting (`0x00658b1a mov eax,[0x00faa5e0]`, then `0x00658ccc`), so if the cursor callback posts the second press before the
  drain handles the first click, the original's second press also goes to the old control. Settle by how `0x00486c70`'s events
  are delivered against the message drain (Ghidra), or by a two-press-in-one-frame run in the original on a button that closes
  its own window; then keep the comment at `WindowStack.Update` ("whether the original sends a frame's second press ... is not
  decided") or replace it with the answer.
- [ ] **Q508. A pulled body against an unpulled sprite's order (heads, bubbles, balloons).** From the review of Q500
  (VERIFY-14, finding 3; UNSETTLED, narrowed). The premise now holds: each group's flush (`0x0058aa3b call 0x5658c0`, ecx
  `[0x00879ab0]`, the batch object `0x00566220` adds to) reaches `FUN_00565590` (`0x00565917`), so sprite batches go through the
  per-triangle sort, which keys on the screen z the pull wrote. Still unread: the mask `[esi+0xc]` tested at `0x0056570b` (the
  doc's `0x3000`, or `0x1000` where the device probe `0x0056728c` finds the caps bit `0x40` missing: fork watch 2026-10-26, L1) and `[0x008bd524]` in play, and whether bodies share a group (texture) with heads, bubbles and balloons;
  across groups the batch key (mean z) also sees the pulled z. Settle by a live read of those two words in the original, or a
  photograph of a balloon's fringe against a body in both games; then say whether `FurthestFirst`, which orders every quad
  here by its unpulled depth, matches the original's order for a pulled body against an unpulled sprite, and whether the
  first-person view (render bit `0x80`, ungrouped; `docs/exe/park-engine.md`, "What hides a person below their feet") sorts at all.
- [ ] **Q509. Sound_StopFading: the original's fade law, and FADEOBJ as a cut at low detail.** From the fork watch,
  2026-10-18 (`verify-other-a.md` H1, `verify-audio-fx.md` F1; `docs/exe/audio.md`, "The fade, the SoundInfo settings
  and the reverb"). The original's `Sound_StopFading` (`0x0051c300`) and `FADEOBJ`'s sound arm are a **x0.6 volume step
  per fade step** (the 60 is a percentage kept, not milliseconds; a worker thread makes the steps, 100 ms apart, not
  the service pass) while sound detail is above
  `SoundInfo.FADETOGGLE` (30, `sound.sam`; stock detail is `DEFAULTQUALITY` 50, and 25 at boot on low graphics detail,
  `0x0054de17`), and a **hard stop** (the same as `KILLOBJ` / `Sound_Stop`) at 30 or below. At 0, or with no channel, the
  voice is stopped on the next service pass. OpenTPW stands a flat 0.06 s ramp in for both, said at the site in
  `Advisor.QuietenSeconds`, `ParkAudio`'s rain and crowd fades, `ParkCarSounds` and `FADEOBJ`'s (`ParkAudio.ScriptFadeSeconds`, Q553). Build: (1) the fade as a geometric
  decay on the voice's volume, one step every 100 ms (250 ms before the first when nothing was fading), each step
  `own = trunc( 60 x round( own x group product ) / 100 )` with the group gains folded in at each step, ending in a stop
  at 0 (the rate is read, not to be measured: `audio.md`, "The fade", fork watch 2026-10-19, `verify-other-b.md` H1); (2) `FADEOBJ` and `Sound_StopFading` as a cut when sound detail is
  30 or below, the detail being the options' sound quality slider (`0x00423850`), so the options screen's slider must
  reach it; (3) the stop-all at a level's end (`0x0051bcb0`, handed 90, `Level.StopAllSeconds`): trace whether the 90 is
  the same kind of percentage before changing it. Confirm: the advisor quietened (options screen) and the bumper cars'
  unload with a capture of the mix at detail 50 and at 30, beside the original at the same detail; predict the decay
  per pass first. Predicted per step: at group product 1.0 the volume runs 100, 60, 36, 21, 12, 7, 4, 2, 1, 0 (nine steps, about 0.9 s
  after the first step's wait of up to 250 ms); at the effects' measured 0.75 the device gets 75, 45, 20, 9, 4, 1, 0
  (six steps, about 0.6 s). (Fork watch 2026-10-19, `verify-other-b.md` H1; `audio.md` holds the chain.)
- [ ] **Q510. The hidden wide zoom: press it in the original, then build. Confirm first.** From the fork watch
  (2026-10-18, `verify-world-a.md` H1; `docs/exe/park-engine.md`, "A hidden wide zoom"). Camera row `0x11` (`0x00748298`,
  key `0x20`, modifier `0x3f`, handler `0x0042a520`) flips bit `0x200` of `[0x00790aac]`; by our key decode that is
  Shift+Ctrl+Alt+Space, and with the bit set the zoom clamp is [40, 300] (`0x0074c9d8`, `0x0074c9dc`) where it is [70, 130]
  otherwise. The clamp runs only while bit `0x80` is set; the park-load reset `FUN_0042a990` keeps `0x200` and sets `0x80`
  (`0x0042aa64`-`0x0042aa75`), so the wide zoom survives a load. The key table is the handler's only reference, and the press has never been
  made in a running original: this is decode, not observation. Confirm by that press in the original under Proton (read
  `[0x00790aac]` bit `0x200` with `tpwmem.py`, then zoom out with the mouse wheel or keys and read the zoom at its limit,
  predicting 300 and 40 first), also after a park load. Then build it: the key, the bit kept across a load, the two ranges.
  The pitch is computed from the zoom by the same expression and extrapolates past the range; read the camera's pitch clamp
  ([0, 84.27] in `park-engine.md`) at zoom 300 in the original before building it. Q23 is the camera's other work.
- [ ] **Q720. Do the keypad digits reach the cheat and camera rows? (one key press in the original)** From the fork watch,
  2026-10-20 (`verify-other-ui-audio.md`, "Miss found here"; `park-engine.md`, "The cheat table"). Both key edges
  (`0x0046b9c0`..`0x0046b9f5`, `0x0046bab8`..`0x0046baeb`) replace the vk by `MapVirtualKeyA( vk, 2 )` when it answers
  non-zero, excluding only the keypad's `* + - . /`; the digits `0x60`-`0x69` are not excluded. If the call answers `'1'`..`'9'`
  for `VK_NUMPAD1`..`9`, every row keyed `0x61`-`0x69` is unreachable (cheat rows 3-11, the Local 1-5 and Global 0-3 ticket
  cheats; camera rows 13-22, the keypad pan) and Alt+Num1 fires the ten-thousand cheat. If it answers 0 the rows work as the
  tables say. Confirm: in the original under Proton, in a park, NumLock on, hold keypad 4 (or 6) and see whether the camera
  pans; with `Easymode.TPWI`'s cheat flag possibly cleared by the first key (the same section), press a harmless first key, then
  Alt+Num1 and read the money. Then correct the keypad rows in `park-engine.md` (the camera's pan keys, near line 394, and the cheat table) and OpenTPW's keypad bindings if they differ.
- [ ] **Q511. A bouncing rider is lifted on a sine.** From the fork watch, 2026-10-18 (`verify-world-b.md` L4). The Belly
  Bounce's riders are drawn standing on their node here (`ParkGuestSprites.Seated`); the original's positioner
  `FUN_00557ab0` lifts each on a sine (`0x00557c75`): the phase is `( trunc( elapsed x ( 0.8 + 0.005 x the short +0xc0 ) )
  mod 1000 ) x 0.003125`, the index `trunc( phase x [0x007b095c] ) & [0x007b0960]` into the table `0x007b0968`, and the height
  `( ( visitor mod 7 ) + 12.0 ) x sine + the instance word + BOUNCESETBASE's word` (`+0x6e`), or the node's own y when that
  is larger; the sprite is placed by `FUN_004f9e60` with `elapsed / 500` and template 2 (`docs/exe/ride-operation.md`, "The
  bounce riders' height"). Counted, `RIDER_BOUNCE`, at the draw. Lost Kingdom's stock park carries the Belly Bounce, so placed
  content reaches it. Not read: whether a4 is a heading sector, and `[ [+0x1c] + 0x1c ]` (template 3 and `visitor mod 7`).
  Read those first. Confirm in both games: a rider's height over a few seconds on the same ride, predicted from the formula,
  beside the original (`record.sh`); a screenshot of each.
- [ ] **Q512. The ride window's Buy Upgrades list.** From the fork watch, 2026-10-18 (`verify-world-b.md` L1,
  `verify-ui-hud.md` U3). The ride window's upgrades button (`RIDE_WINDOW_UPGRADES`, counted) opens the original's list
  (stream `0x00755750`; `FUN_004ae560`, rows `FUN_004ae140`, choice `FUN_004ae430` and `FUN_004af480`): the add-on rows
  (types whose `+0x284` is the ride's type and whose control has `+0x10`), then one row for the next level, UITEXT 29 with
  the price `CostOfUpgrade[level+1]`, or UITEXT 30 "cancel" while an upgrade waits (state 2), else UITEXT 26 or 28; nothing in
  Instant Action (UITEXT 27). A level row is ordered only when its cost is strictly below the balance, with no error cue; an
  add-on row at most the balance builds a tool-4 carry shell and closes the window, and a dearer one plays `cat_ui` `0x1D`
  (`docs/exe/ride-operation.md`, "The ride window's Buy Upgrades list"). Full Simulation only: Lost Kingdom's stock park is
  Instant Action. Neighbours: Q272 (a mechanic and a waiting upgrade), Q452 (Buy Rides lists a researched upgrade), Q402 (the
  sound). Confirm in Full Simulation: a ride at level 0 and at level 2, the list's rows and prices, an order and its cancel,
  beside the original; a screenshot of each.
- [ ] **Q513. The add-on's snap and the window a click on an add-on opens.** From the fork watch, 2026-10-18
  (`verify-world-b.md` L2 and L3, `verify-world-a.md` M5). Lost Kingdom's add-ons (1500 to 1502, `AddOn.UpgradeType` 1) are
  the items. Hovered over one of the five track types, the tool-4 preview snaps the add-on to anchor + 2 x floor( d / 2 ) on
  both axes, turns it by the cell's flow (flow 1 gives 0, 4 `0x10e`, `0x10` `0xb4`, `0x40` `0x5a`), applies the item's
  North/West/South/East adjust and moves the cell by the ride's own angle; the placement stores the word `+0x30` as the
  builder's object (the ride the buy row was chosen on); a click on the add-on opens that ride's window when the word is
  non-zero (`docs/exe/park-engine.md`, "The add-on's snap, its open ends and the window a click opens"). Its open ends light
  the dummy nodes by a bit accumulator that is never cleared after the start. Read what this game does first (`HOVER_*` and
  the add-on hover counters; since Q478 a placed add-on of type 1 posts no hover row and keeps the thing, `Level.GroundArmAt`). Reached through Q512's add-on rows, so after it. Confirm in Full Simulation: an add-on dragged
  over a track piece at each flow, its snapped cell and angle, and a click on the placed add-on opening the ride's window,
  beside the original; a screenshot of each.
- [ ] **Q514. The hover's rows over a track and type `0x15`.** From Q478 (`park-engine.md`, "The rows over a queue and
  an object", "The arms that post nothing"): ours posts the empty row and counts `HOVER_TRACK_CELL_CATEGORY` (track types
  11 and 16, and 12 under a parent not 25) and `HOVER_TYPE_15_ROW` (map type `0x15`). Type `0x15` is read: category 6,
  UIHELPTEXT 448 (`0x1c0`, "Left-click to edit this track") posted on a change of category, the thing forgotten,
  `FUN_00469180`, pointer 11 `c_pylon` (`0x00487728` to `0x004877a2`). The track's arm (`0x004873d5` to `0x004874fe`) is
  category 5 and 446 (`0x1be`) with pointer 6 or 10 by `+0x2c` of the thing `FUN_004d99e0` answers for the track
  record's `+0x10` cell (none: the thing forgotten, nothing posted, `0x00487441`). On a cell of map type 4, 9 or 10
  (`FUN_00536380`, `FUN_00536350`, `FUN_00536340`) the arm returns with nothing when the cell has no thing
  (`0x004873f8`) and goes on to the map-type switch when its thing is no add-on (`0x0048740e`), so such a cell takes the
  object's 550, where ours counts the track first. The stock park reaches neither
  (map types 0, 1, 2, 3, 4, 7, 9, 10, 30 and track types 7, 12, 25, `parkfile.show_map` on `q325/stock.TPWS`); that a
  built track ride is what makes them is inferred from the rows' words and `c_pylon`: build after the track rides. Confirm in both games over a built coaster's track and its pylons.
- [ ] **Q517. Do the loading screen, the pointer and the letters pass the four-bit cut? Decode first.** From Q700
  (`textures.md`, "The texture cache's surface format"): ours keeps them at eight bits (`TextureFlags.OwnPixels`) because
  how the original puts them on the screen is not read: the loading screen's picture (`LoadingScreen_Begin`,
  `0x00587380`), the `.ani` pointer, and the `.bf4` letters. Find whether each is a texture of the cache (cut to
  A4R4G4B4) or a blit to the display, then cut what the original cuts. Confirm by a frame pair of the help bar's letters
  and the pointer in both games.
- [ ] **Q518. The 16-bit display.** From Q700 (`q700/PREDICTION.txt`, P3, P9): with the textures cut, the sea at camera
  B still reads red 3.1 against the original's 1.3, and the shipped presets hold `RENDER32 0`, a 16-bit display
  (`boot.md`, `0x0044e20b`), where ours draws eight bits a channel. Decode first what the original's frame keeps at 16
  bits (5-6-5, and whether the device dithers or cuts), measured on its own frames at its own resolution, then decide
  with Alexah whether ours draws the same.
- [ ] **Q519. A texture's mip levels: four, and how the original fills them.** From Q700 (`textures.md`, "Four levels a
  texture"): the original gives each texture four levels where the cache has room (`TexCache_MeasureSpace`,
  `0x0058d400`); ours makes every level down to one texel, each the rounded mean of the eight-bit level above, cut, a
  filter not read. Read the filler (what writes a level below the first: the codecs, or a downsample of the surface),
  then build it. Confirm by a far camera over the sea and the brick in both games.
  - **Note (run 6 review, Q244):** confirm by the happiness gauge too, a fixed screen-space target: its faces are
    sharp in ours and blurred in the original's frames (`q244/side-by-side.png`; `hud.md`, "The happiness gauge's two
    quads"). If the mip levels do not explain it, the filter states are Q29's.
  - **Note (Q617):** the filter states are decoded (`render-states.md`, "Texture filtering"): a clamped texture is
    minified by its nearest texel at the mip level the start chose, LOD bias 0.
- [ ] **Q520. A person's shadow under a tool's squares.** From Q480 (`park-engine.md`, "A person's shadow"): the
  original's patrol squares (and any tool's) are drawn over the people's shadows, which show through them
  (`q480/orig/a-2-on.png`: Mike's shadow on the squares darkens 0.556, 0.716, 0.835); ours draws `ParkBuildMarkers`
  before the people, writing depth 1.5 over the land, so a shadow 0.6 over it is hidden (`q480/run2/4-on.png`: 0 pixels
  changed with `shadows 0`). Decode where the original draws its marker squares against the sprite list's two passes
  (and whether they write depth over the people's feet, Q477), then order ours to match. Confirm by the locator open on
  Mike in both games, a frame pair with shadows on and off (`lib/shadowdiff.py`).
- [ ] **Q521. A person's shadow at high detail in the orbit view: their own picture, unfogged.** From Q480, narrowed by
  the run 4 review (first person's own-picture shadow, `FUN_00589770`, is built: `ParkPersonShadows.KindFor`). `SPRITESHADOW`
  2 and 3 (`high.sam` sets 2) have `SpriteShadow_Draw`'s non-`0x40000` arm (added with `0x566220`, unfogged) use the
  person's own picture with the darkness word `0x67000000` as its colour and the picture's own blend (no multiply),
  counted here `SPRITE_SHADOW_OWN_PICTURE` (`ParkGuestSprites.WriteShadow`) and drawing none. Build it on first person's:
  add the unfogged orbit kind to `KindFor` and reuse `OwnPictureShadowMaterial`. Confirm at high quality in both games
  (the original by its Options screen) by a frame pair at one camera.
- [ ] **Q522. The weather's drops in first person's fog.** From Q485 (`park-engine.md`, "First person's fog"): first person
  fogs every batch under render-word `0x2000`, but whether the rain's and snow's drops go through that batch (the callers
  of `SpriteBatch_AddQuad` include `FUN_0057be90`, `FUN_0057ed50`, `FUN_005860d0`, `FUN_00577ae0` and `FUN_0058a010`, none
  read) is not decoded; ours draws them unfogged and counts `FIRST_PERSON_FOG_WEATHER` (`WeatherSprites.OnRender`).
  Decode the weather's draw, then fog `weather.shader` the same way or take the count out. Confirm by rain in first
  person in both games with the band held at 20 to 40 (`memhold.py` there, the console's `fog 20 40` here).
- [ ] **Q525. The money's lettering: the entry fee's "$ " and a cash below nought.** From Q524 (`q524/side-by-side.png`):
  the original's entry-price spinner letters the fee "$ 28" where ours letters "28" (`ParkEntryPriceScreen.Show`), and
  its park cash at the top left letters a balance under nought "-$ 796" in red where ours letters "$ -1188" in the
  usual colours (`orig/a-fee.png` beside `run2/3-reloaded-fee.png`). Seen, not decoded: read the spinner's text
  (`hud.md` names the money skin `FUN_00490250`, "$ 75" and "-$ 11750" on the training screen) and the cash display's
  draw, then letter both as the original does. Confirm by the two frames at one fee and one balance under nought.
  - **Note (play test 2026-10-21, note 25):** the ticket price box shows "20", not "$20". The original letters UITEXT
    `0x1c1` "$ " (`hud.md:3294`; QUEUE:1436).
- [ ] **Q527. The gadget's date: the original letters it month.day.year with full stops.** From Q526
  (`q526/side-by-side.png`): on 10 January 2002 the original's gadget reads "1.10.2002" (`orig/a-2-loaded-gadget.png`,
  the poll's day word 10 at that moment), where ours letters .NET's short date, "1/6/2002" on 6 January
  (`ParkGadget.ShowDate`, `ToShortDateString`, so it follows the computer's culture). Decoded (run 6 review):
  `FUN_004a0e30` letters UITEXT row `0x1c0` (448, `{17}.{16}.{18}` in the american tables, `saves.md`, the Load Park
  rows) into a buffer of `0x1f` (the call at `0x004a0ea1`), token 16 the day, 17 the month (1-based, the +1 at
  `0x0048156c`) and 18 the year + 1900: month.day.year with full stops. It is redrawn on a day's change, from
  `0x00481570` inside the day-changed block (the call at `0x004f83ac`), and once at set-up (`0x0048166f`). Ours draws `GameCalendar.Now`,
  the date of the park's tick (Q149), so the two gadgets show one date in two letterings (`q149/side-by-side.png`). Confirm by both
  gadgets on one date.
- [ ] **Q529. A loaded park's weather runs on from the file's.** From Q528 (`saves.md`, "OpenTPW's writer, the weather
  thing"): the original's load reads all of the weather thing over its constructor's (`Weather_Serialise`,
  `0x00512010`) and runs on from it, the rain, the storm, a pending forecast and its due time; ours reads only
  `mWindDir` (`ParkWorld.WindDirection`) and rolls the rest fresh, though `ParkWorld.WeatherRecord` reads it all. Start
  `ParkWeather` from the record, and schedule as the original does, on the park calendar's instants (`mLastChanged` +
  3 days, the forecast's `mTime`; the first tick past each) rather than day edges of `GameCalendar` (Q149), so a
  forecast read from a file falls due on the original's tick. Look first at what the load leaves undrawn: the original
  sets the particles only on a step of the ramp, so a file whose drops stand at their target rains nowhere until the
  next change (filmed, `q528/orig/e-rain-film.png`), and its voice's handle is the saving process's. The constructor does not apply its roll
  (`docs/exe/weather.md`, `FUN_00511f70`), so a park with no file holds the roll unapplied until its first forecast
  arrives. Confirm: q528's `run3/q528-fix2.TPWS`
  loaded here and in the original, the quality, the drops and the forecast's delivery tick (17097) predicted, a frame
  of each.
- [ ] **Q712. The television static on entering or leaving first person.** From the fork watch of 2026-10-19
  (verify-advisor-ui.md L4; `park-engine.md`, "Entering or leaving first person shows 300 ms of television static"),
  with Q485's first person. On either edge of the camera flags `& 0x16` (a ride's view, first person) the original's
  `Game_StateMachine` (`0x0054fabb`..`0x0054fb63`) toggles the fog's render bits and sets a 300 ms timer (`0x0087502c`,
  stamped from `GetTickCount`); while it runs the frame is a 320 x 240 static (`FUN_0053e760`: one `rand()` a pixel, 75%
  black, 12.5% white, 12.5% grey 150) stretched to the screen **instead of** the frame renderer. Lost Kingdom reaches it
  from every ride view and the camcorder. Count `FIRST_PERSON_STATIC` where the swap would go, then build it timed on
  `Time.WallMilliseconds` (it is real time in the original), the frame replaced as the original's is. Not measured: the
  pixels in a running original; the screen the blit is stretched to is read as the screen size, not checked. Confirm:
  the static photographed in the original under Proton on entering first person, then ours beside it; predict the 300 ms
  from the frame count.

- [ ] **Q716. First person's own-picture shadow beside the original's.** From the run 4 review (Q480 1, Q485 1 and 2),
  built and confirmed here against main (`fix-run4/PREDICTION.txt` S1-S5: no streak at -2/-1, 0.592 of the ground under
  the picture's core, a figure where main drew a bar) but not beside the original: its first-person frames (`q485/orig`)
  have nobody near the camera. Put the original in first person with people within the band (`gmove.py`, `memhold.py`
  at the stock band and at -2/-1) and ours at the same stand; predict the shape (the person's own `.FPC` picture laid
  along the sun) and 0x67/255 darkening on opaque texels. Measure the order too: ours draws every shadow after every
  body, the original's 0x80 walk adds each straight after its own body (said at `BuildShadows`).
- [ ] **Q717. The cost of cutting every texture to four bits.** From the run 4 review (Q700 4); since Q616 the cost of
  the option `graphics faithful` (Q645), not of the default: a park loads 0.76 s slower with the cut, and each switch into
  and out of first person packs the guest atlas again (about 0.45 s by the load figure, not measured). Measure a camcorder switch with and without the cut (`graphics faithful` in a private game
  folder's `save/opentpw.cfg`); if it is felt, make `CutLevels` cheaper (a 256-entry table, one row loop a level). Said at
  `Texture.LevelsFor` and in `docs/exe/textures.md`, "What the cut costs".
- [ ] **Q718. A first-person right click let go inside one frame reaches the park after first person is left.** From the
  run 4 review (Q499 2, latent). `Hud.Update` (`Level.cs`) runs the window stack before `WorldClick`: the stack's release
  step calls `ViewRightClick` (`ParkViewfinder.RightClickAnswer`), which leaves first person, and `WorldClick`'s press
  step then reads `RightPressTaken( false )`, arms, and its release runs `QuickRightClick`. The original posts the park
  the press first, in first person, and takes none of it. Harmless today (entering first person lets go of the hand and
  `LetGo` answers null); it bites if anything the quick click does is ever not a no-op in first person. Fix: record at
  the stack's press step whether `ViewRightClick` answered, in a static of its own, and read that in `RightPressTaken`.
  Said at the site (`Level.WorldClick`, "Latent").
- [ ] **Q535. An empty-hand path click takes the latch from its own pass.** From the run 5 review (Q532 1). Between
  the press and the release the original runs mode-1 hover passes over the clicked cell, so an empty-hand click on
  ground whose one cell is unaffordable leaves `DAT_00816d50` 1 (the money arm, `0x00535931`); ours arms (which clears
  the latch, as the install's pass does at `0x0046d5e8`) and leaves it 0. Give `PathStrip` an out `latched`, as
  `ParkPathBuilding.Strip( x, y, out latched )` has, and have `RunPath` store it in `ParkBuildMode.RefusalLatched`
  after `Arm`. Confirm with the original (the stock park, an unaffordable single cell, `d50` read at
  `q532/orig/a-run.out`'s addresses) and a test whose price a wrong rule gets wrong. Said at the site
  (`ParkBuildMode.Arm`).
- [ ] **Q536. A carried item's passes set the latch.** From the run 5 review (Q532 2). Mode 4's pass clears
  `DAT_00816d50` at `0x005234f2` and sets it and `d5c` at `0x0052365c`/`0x00523662` each tick when the item's
  BuildPrice (`+0x1b8`) is above the bank's balance, unless the golden-ticket cost `+0xc4` is positive and
  `FUN_004d4b70` says it is unpaid (then the pending price is 0 and d50 stays clear), and not under the replay
  (`[0x00785a2c]`). The placer (`FUN_00528a70`, `0x0052390e`) also sets d50 at `0x005295e8` and `0x00529730` for an
  entrance or exit facing off the map (trigger not re-derived). Build both in `ParkBuildMarkers.OnUpdate` while
  `ParkBuilding.Carrying != 0`; the player reaches it by a move with the price above the balance, which `Carry`
  refuses but `Move` does not. `Hold` clears the latch for now; said at the site (`ParkBuilding.Hold`).
  From Q539 (its last piece; counted as `CARRY_PASS_REFUSAL_LATCH`; the carried thing's squares are Q633's): confirm beside the original's `[0x00816d50]` and the
  locator's edges; the bank can be held low in the original with `thingpoke.py` (`TOOLING.md`, "Recipes", Q542).
- [ ] **Q537. The patrol pointer after a re-arm, watched in the original.** From the run 5 review (Q534 1). Re-arming
  the patrol tool over itself keeps `c_noplace` here (`ParkBuildMode.Arm`), by the decode (`FUN_004b5b50` ->
  `FUN_0046c350` -> `FUN_0046d5a0`; the setter `FUN_0052f200` sets a cursor for modes 3 and 1 only); not watched in
  either game. In ours the route Ctrl+S, the mechanics' button, the strip did not reopen Mike's window with the tool
  in the hand (`fix-run5/c-control`): find the original's route first (the locator, or the gadget's staff screen),
  then patrol anchor outside the park, the pointer on a park cell (`c_noplace`), the switch down again, the pointer
  back on a park cell, and read the cursor; predict `c_noplace`. Also read the pointer over the gadget's camera
  control with the tool armed (`FUN_004981d0` writes the same control's cursor). Write the result into
  `docs/exe/hud.md`, "The patrol pointer", and drop the "not watched" sentence there and at `Arm`.
- [ ] **Q541. The build tools' run total, shown as Q403's cost in hand.** Seen in Q540's runs: with the queue tool
  anchored on the stock park the original shows a yellow "$ 375" under the balance (`q540/orig/a-16-join28-edges.png`,
  `b-1-before-click.png`), gone after the click; ours shows none (`q540/run2/3-join-edges.png`). Q403 already decodes
  that display: `FUN_004a0ab0` reads the total (`0x004a0d38`) into control `0x30`, for interaction-mode types 3 and 4
  (8 only when `[0x0079c658] & 1`), -999 skipping the show and -9999 (the patrol tool's `FUN_0052fb90`) hiding it
  (`0x004a0d68`..`0x004a0d74`); carrying a mystery ride sets it to nought (`0x0052360a`), so the original shows "$ 0"
  (`q709/orig/a-place.png`; Q709 built the carry's skipped money test and not this display); the build tools are type 3 (`park-engine.md`, "The build-tool interaction mode is
  object type 3"). Build it with Q403 or after it; what is left to read is how often message `0x1e` comes against the pass.
  From Q542: the total is `DAT_0081af4c` (`FUN_0052fba0` adds, `FUN_0052fbc0` reads); the path and queue passes never
  zero it, `FUN_004a0db0`'s message `0x1e` does (`0x004a0dee`), calling `FUN_004a0ab0` first on every third one (the
  counter `[0x007cb2e0]`, reset at `0x004a0de2`, which message `0x15` also zeroes, `0x004a0e02`). A red square's second
  verdict adds to it (`park-engine.md`, "The path tool"), so the label may show more than the click charges: read it
  beside `strip`'s `run total`.
- [ ] **Q568. The second charge's carry flag, settled by the original first.** From the review of run 6 (VERIFY-2, M1,
  UNSETTLED). The second verdict's charge also needs `[0x008186d4]` == 0 (`0x005358df`). The mode-4 and `0x3b` arms of
  the pass (`0x005235d4`, `0x00523967`) and of the click (`0x00524b4a`, `0x005253e9`) set it to 1; only the click
  (`0x005249c2`, `0x00525a06`) and `0x005239fc` clear it, and `FUN_0052f200` (the tool setter) does not touch it. So a
  path or queue tool armed from the toolbar straight after carrying an item might preview with no second charge (and so
  no `c_cash`) until the first world click. `ParkPathBuilding.JudgedAgain` never models the flag. Run the original
  first: carry an item, put it down or drop it, press the path button, and hold the pointer on bare ground past a red
  square with a balance under 20; watch `[0x0081af4c]`, `[0x008186d4]` and the cursor `[0x007cb2d8]`
  (`docs/TOOLING.md`, the path tool's recipe). Build it only if the run shows the flag held; otherwise write in
  `docs/exe/park-engine.md`, "The path tool", that it is cleared before a tool can be armed. Confirm: the same steps in
  ours beside the original's frames.
- [ ] **Q543. The welcome a park made with no file says on tick 1, decode first.** From Q327 (`advisor-park.md`, "A
  fresh Full Simulation park", and "Still unknown", `Welcome`). In both of the original's fresh Full Simulation parks
  message 0 (row 0, posted only; response 1, sample 1, slot 1, `Welcome.Score` by `0x0059c8c0`) stood in a slot at
  100,000 on tick 0 and was said on tick 1 for 31.3 s (the record (1, 0, 1, 0) in `q327/orig/FSFRESH.TPWS`, read with
  `parkfile.py -a`; the slots in `q327/orig/a-restart.INTS` and `q327/orig/o/fresh/proton-restart.INTS`;
  `q327/orig/a/fresh.log` holds the slot and the 31,332 ms action only); this game posts
  nothing there. Find the poster (a post of message 0 at the world's setup, or the park's first tick), whether an
  Instant Action park made with no file and a loaded park post it too, and what sample 1's 31 s is. Then build it.
  Confirm: a fresh park's first seconds in both games, the record and a frame.
  - **Note (play test 2026-10-21, note 3):** the advisor does not say his opening lines in a fresh park. Nothing here
    posts message 0 on tick 1 (`map.md`; Q543 is decode first and unstarted).
- [ ] **Q547. The cursor Up and Down keys step an object window.** From Q31. The base window proc `0x0048d200` takes key
  down `0x2600` and `0x2800` to `0x0048cbe0` and `0x0048caf0` (`hud.md`, "The shop, sideshow and ride windows"), which
  apply and step to the next or the previous object of the class; no object window here hears a key. Build it for the
  ride's and the shop's windows (and the staff member's and the visitor's, if the same proc serves them: read first), and
  confirm in either game: the stock park with a second Drinks Shop bought, Down in the shop's window, the title and the
  `shopwindow` census naming the other shop.
- [ ] **Q548. The ride window's title is white; `UI_SetTitle` letters it yellow.** From Q31: the shop's window takes
  `UI_SetTitle`'s font 5 in (234, 239, 102) with its shadow over the widened rect (`hud.md`, "The shop's window"), as the
  visitor's and the staff member's do; `ParkObjectWindow` letters the ride's title white in the stream's own rect. The
  original letters it through `FUN_0048d290` (`push 0x3e28` at `0x004ae930`, `call` at `0x004ae937`, in `FUN_004ae8e0`, slot 0
  of vtable `0x006ffbe8` on the ride window's object `0x7cc218`; `0x48d290` calls `UI_SetTitle` at `0x0048d2c5`); letter
  `ParkObjectWindow`'s `0x3e28` so, then photograph the Belly Bounce's window beside the original's.
- [ ] **Q549. The researcher tires about 80 sweeps sooner here and rests longer.** From Q701: stock Easymode, left
  alone, our researcher (thing 30) set off for the Staff Room on tick 2210 and 2199 in two runs and ended the rest on
  2553 and 2578, skipping 17 and 19 hand-outs; the original's last hand-out before its rest was on 2280 and it gave the
  next near 2590, skipping 14 (`q701/run1/run.log`, `run2/run.log`, `q701/orig/a-poll2.log`). So Dino Karts' 95th
  hand-out falls on 2980 or 3020 here and 2920 there. Read
  both games' rest level each sweep from tick 755 (`q134/orig/watch.py`'s reader; `staff`) and find where the drain
  or the walk to the room parts (`ride-operation.md`, "The staff turn"; Q326's search, arrival and rest).

- [ ] **Q607. A thing made on the id of a sold object the file keeps.** From Q606. Two reaches. (1) The park file's
  writer cannot take out a sold object it cannot write (a tracked ride, a kart or water ride with cars:
  `SAVE_PARK_OBJECT_SOLD`, Q420's gap), so the file keeps that object's id. (2) A file whose things go out as the file's
  (`SAVE_PARK_THINGS_AS_THE_FILE`: its clock, scripts or models did not read, as the RSYS module of Alexah's jungle Full
  Simulation saves does not walk closed, Q167) keeps every object it holds, sold or not. In both, a guest, a hire, a buy or
  a vehicle made on the id since the sale is counted `SAVE_PARK_ID_KEPT_FOR_A_SOLD_THING` and left out of the file
  (`Level.WritePark`, `ParkState.WrittenObjects`; `saves.md`, "OpenTPW's writer, an id given out again"); the original
  writes it and drops the object. Reach (1) by selling a coaster a loaded park holds, then playing on (not run); reach (2)
  by any sale in a loaded Full Simulation save of Alexah's, then a make on the id (the next make after the sale, or a
  later one once the park grows past the ids freed above it), and it recurs in every save of that park. What is written
  still names the left-out thing as the kept object: the other guests' queue links (both reaches) and, in reach (1), the
  ride's head and nominee, script slots, a handle, the arrival slots, the mechanic's cursor and the region effects; what
  the original or our reader does with such a file is unproven. Reach (2) goes with Q167 (read that module), reach (1)
  with the writer taking such a ride out; until then, confirm a save after the sale and one make counts the gap once and
  loads back without the made thing, and that nothing in the file names the id.
- [ ] **Q727. A coaster's counted gaps (Global 0's measure).** From the Q706 review (VERIFY-6 F2). Q706 built Global 0's
  measure and counted eight reached paths, none of which has a queue line. Chak Atak (`coaster1`) is a shipped jungle
  coaster, so a Lost Kingdom player reaches the first of them; each leaves its coaster with no height (Global 0 reads
  nought for it). (1) `COASTER_PYLON_KNOTS`: a pylon type whose model has more than one knot a node (Chak Atak's three:
  In, Centre and Out) is not posed (`ParkCoasterHeights.Measure`; `ParkCoasterPylon.Posable`); decode which knot the
  track passes through and give the chain its knots from it. (2) `COASTER_LOOP_PYLON`: a loop pylon's knots and handles
  (`Coaster_KnotHandles`' other arm for type flag `0x4000`, its rotation `+0xf8`; `ride-operation.md`, "A coaster's
  highest track point", step 5, "not read"). (3) `COASTER_STACKED_PYLON`: a stacked pylon (a node whose `+4` is not
  nought; second rail, one knot, the height of the node it names plus H plus `+0xc4`). (4) `COASTER_TRACK_UNSAVED`: a
  coaster made here has no model instance in the module, so no saved track; build the chain for a coaster the player
  lays (Q406's track tool) and for one past the module's first. (5) `COASTER_TYPE_UNREAD`: a coaster whose pylon,
  `fCurviness` or station nodes do not read. (6) `COASTER_STATION_NODE`: a station node missing from the coaster's model
  (track nodes 1 to 3). (7) `ANIM_SLERP_OPPOSITE_KEYS`: two rotation keys within 0.001 of opposite take another arm of
  `Anim_SlerpOnSineTable` (`0x004742a0`), which no shipped pylon's clip reaches; blended here by `Quaternion.Slerp`. (8)
  `SAVED_COASTER_HEADER_UNREAD`: a coaster past the module's first, or whose header does not read
  (`ParkCoasterHeights.Measure`, `ParkRideChoice.CircuitClosed`). Also settle the lead VERIFY-6 M3 left open:
  `ParkCoasterHeights.Chain` appends the closing node at the station's start for every saved coaster and never reads
  `SavedCoaster.CircuitClosed`; a save can hold an unclosed coaster (`ParkRideChoice.cs:120-123`), and whether the
  original chains such a track's pieces before the type-7 node was not read (`Coaster_RebuildTrack` `0x0043eb70`).
  Decode it in Ghidra, or read the original's `+0x110` with an unfinished coaster saved. Confirm each built piece
  against the original's `+0x110` (`original/coaster.py`).
  - **Note (fork watch 2026-10-25, verify-forks.md section 4):** (7) read whole: the opposite arm (`0x004743e0`) ignores `b` and blends
    `a * sin( (1 - t) * pi/2 ) + p * sin( t * pi/2 )` with `p = ( -a.y, a.x, -a.w, a.z )`, the sines from the 4096-entry table
    at `0x007b0968`. And the general sampler is a second deviation: `0x004742a0` never tests the dot's sign and never negates
    `b`, so it never takes the shorter arc, where `AnimationFile.RotationTrack.Sample` (`Quaternion.Slerp`, AnimationFile.cs:462) does;
    only 6 key pairs, in `looppylonr1` and `looppylonr2` (the sweep's census, not re-run), have a negative dot. Said at the site; make it the original's
    when the loop pylons are posed (item 2).
- [ ] **Q728. What shows the advisor's head again at a second ticket line (a live read).** From the run 7 review
  (VERIFY-7 Q595 2). The original's clip change (`FUN_00472f60`) shows the nodes the OUTGOING clip's tracks name
  (`FUN_00472310` at `0x004730a9`) and hides the incoming hide list (`FUN_00472d70` at `0x00473117`). Clip 20's hide
  list and tracks name no node 0 (`Bug Head`), which a clip word's `0x10000` hid on clips 19 and 20, yet the original's
  film shows the head in the helmet within the first 633 ms of a second ticket line (`q707/orig/O/film/sheet2.png`). The
  likeliest cause is the animation object's `+4 & 0x8` path, which shows every node (`0x00472345`..`0x00472398`). Read
  `[[0x0079044c]+4] & 8` in the running original (slot 1's object: `FUN_00429fe0` returns `[slot*0xb0 + 0x0079039c]`)
  and settle it. Then, and not before, change `AdvisorModel.StartClip` to the original's rule (show the outgoing clip's
  track nodes, or every node when the bit is set, then hide the incoming list): a pure outgoing-tracks rule alone would
  hide his head for the first 633 ms of the second line, which the film contradicts. Also not read: the original's
  `0x80000000` locks against OpenTPW's unconditional shows, and whether `Dress`'s hidden props include a node in clip
  16's tracks (the map, node 6) on the slot 1 model's first clip, where the original's channel at `0xc` skips
  the call to `FUN_00472310` (the test at `0x00473096`, in `FUN_00472f60`; the call is `0x004730a9`).
- [ ] **Q729. A rotation track's last key, held or wrapped.** From the run 7 review (Q599 4's lead). `Anim_FindKey`
  (`0x00470b60`) takes key 0 as the upper key past a track's last key where its `param_5` is non-zero
  (`0x00470bee`..`0x00470bf9`), and the rotation call passes 1 (`0x00471c45`), so the original's rotation sampler WRAPS
  to key 0 between a track's last key and the clip's declared end, where position and scale (param_5 0) hold the last
  key. `AnimationFile.RotationTrack.Sample` holds the last key (`AnimationFile.cs`, "frame >= FrameIndices[^1]"). Count
  the rotation tracks whose last key falls before their clip's declared end (a corpus scan over all 2,129 `.md2`); where
  there are any, and Lost Kingdom's reach one, make `Sample` wrap and confirm one beside the original's frames;
  otherwise say in `park.md` that none exists and the held last key is dead by CONTENT.
- [ ] **Q730. The advisor line's wiring has no seam.** From the run 7 review (Q595 4's rest). Not tested, for want of a
  seam in a unit test: the three `_glints.SetCount( 0 )` calls in `Advisor` (not Busy, `Say`, `Speak`),
  `AdvisorModel.StartClip`, `ParkPeople` passing the sample's own lip where a row names another, and
  `ParkGoldenTickets.Sweep`'s order against `TakeTheRidesTurns`. They need an `Advisor` built without its model and
  audio, or the park run (`parkrun.py`) with the bug put back (`mutate.py`).
- [ ] **Q731. The swear filter's load after a profile read.** From the run 7 review (Q709 3, VERIFY-11). `FUN_005afd70`
  (`0x005afeba..0x005aff37`): with `+0x25` set, open `Data\Language\<language>\swears.txt` (the language from the
  registry's `Language`, `0x005c0d00`: 0x409 "American", else "English"); when it does not open, clear `+0x25`
  (alloweds.txt's answer is ignored, `0x005d3226`). Build the same test on `PlayerFile.FilterSwearing` after a read. A
  disc copy (the reference install, `~/Games/TPWorld`) has no `Language/american/swears.txt` and writes 0; the US
  installer copies the lists there from `USEnglish/Filter/American` (`USEnglish/Setup/bffl.fls`), so an installed game
  keeps 1. Confirm: gms.dat offset 0x13 after a park save equals the original's on the same folder. (Said at the site:
  `PlayerFile.FilterSwearing`'s comment, "NOT MATCHED". The docs are corrected: `docs/exe/saves.md` `+0x25` row,
  `docs/exe/hud.md` "Spending golden tickets", "The save".)
- [ ] **Q732. A moved ticket ride meets the money test.** From the run 7 review (Q709 4, VERIFY-11). In the original the
  move tool's put-down (`0x00523940..0x005239fc`) has no ticket exemption: `0x52398b call 0x52fb90( [item+0x1b8] )` sets
  the run price to the full price, then the shared cell test (`0x528a70` -> `0x532fc0(0x204,0,0)` -> `0x535670(4,0)`,
  `0x00535905..0x0053592b`) refuses where the price is non-zero and above the bank (latch `0x816d50`, `0x816d5c`). Only
  the carry pass (`0x005235e6..0x00523613`) sets the price to nought for an unbought ticket ride. `ParkBuilding.Build`
  skips the money test for a mystery ride on every put-down and cannot tell a move from a carry. Correction: keep the
  skip to a carry from the buy list (a flag from `PickUp`/`Hold`, the move tool). Not read: which of
  `0x816d50`/`0x81b604` the move put-down's click honours (assumed the carry's, as ours already does for a cash ride's
  move). Reach: only a park holding a ticket ride the current player has not bought (a park file from another profile,
  the console's `buy`); dead by CONTENT in normal play. Confirm: in the original, move an unbought ticket ride with the
  bank under its `+0x1b8`. (Said at the site: the comment in `ParkBuilding.Build`.) Test seam gap:
  `ParkBuilding.Build`'s call of `PayFor` after a ride stands cannot be reached in a unit test, because
  `ParkObjects.Place` needs a graphics device (a null reference inside `LobbyModel`, caught as "would not load"); the
  test pins the click, the failed put-down and `PayFor`, and `Build`'s own call is covered by the game run only. A seam
  (an injectable stand-the-ride step) in `ParkBuilding` would close it.
- [ ] **Q733. `tools/play-the-original/tpw-setup.sh` builds a disc copy, not an installation.** From the run 7 review
  (VERIFY-11, Q709 3's new lead). `tpw-setup.sh:158` does `cp -r "$DISC"/. "$DEST"/` and nothing of the installer's
  overlay: `USEnglish/Setup/bffl.fls` ("LFFB", 726 records of 29 bytes from offset 13, then a 564-entry string table)
  maps `USEnglish\Filter\American\{swears,alloweds}.txt` to `Data\Language\American\` (sizes 0xa06 and 0x414) and
  `USEnglish\Meshes\American\{bankrupt,congrats,paused}.MD2` to `Data\Language\American\` as well (the meshes are
  byte-identical to `Data/Language/English/`'s). The script writes `"Language"=dword:00000409` (`:248`), so the original
  looks under `...\Language\American\`. Effect: the reference install (and `~/Games/TPWorld`) writes the swear byte 0
  where an installed game keeps 1 (Q731). Fix the script to apply the overlay (player-facing: rule 11 territory), and
  re-take any reference that depended on it. **Unsettled lead:** whether the original looks for
  `paused/congrats/bankrupt.MD2` under `Language\American` (a `language\default` string sits at file offset 0x346238);
  if it does, the reference install shows those signs differently from an installed game. Decide it in the listing
  before any claim.
  - **Note (fork watch 2026-10-24, `verify-world-other.md` L1, `verify-audio-ui.md` C1):** the unsettled lead is
    settled: the original looks under `Language\American` (`FUN_004893c0`, Windows language `0x409`; `park-engine.md`, the
    end of a park). The reference install shows no End, end-of-park or pause banner where an installed game shows one;
    re-take Q551's and Q703's banner frames once the overlay is applied.
- [ ] **Q734. A test for `Level.TrackWritable`'s `!state.DeletedSinceLoad.Contains` term.** From the run 7 review of
  Q606 (VERIFY-13, Q606 4). Tried and not written: a Hot Pot (the one jungle ride with a track record) and a kart ride
  bought on a sold shop's id both pass with the term removed, because on Lost Kingdom data a bumper-family ride made
  always has a record here, so the file's judgement (`FileRideWritable`) and the made one agree; the karts are refused
  earlier by the writable predicate. Needs a case where the two differ: a ride made whose record is not in this park's
  table on a reused id (another theme's bumper family, `HasRecord` false), or Q420's teardown. Write it with that case
  and show it failing with the term removed.
- [ ] **Q735. A walk rider's saved sprite.** From Q585 5 (VERIFY-14). `ParkPeople.Written` keeps a sprite for a rider of
  a thing with `mFlags` `0x20` (every walk thing), and `ParkWorld.PutSprite` writes it at the navigator with the
  picture's height and the person's facing. The original's files (`q585/walkriders-saves.out`, 38 walk slots) hold **no
  sprite** for the 23 riders the placer hid (carried under action 1 or 4: `FUN_004f9ed0` frees it, handle 0) and, for
  the other 15, the placer's point with the height above the ground and the slot's facing (`ride-operation.md`, "Where a
  walk's rider is drawn"). Build it: write the sprite from `ParkGuestSprites.WalkedOnRide` (its point, its height less
  the ground, the slot's facing) and none for a hidden rider. Harmless to a load in either game (both re-place every
  frame; the original's hide frees a loaded sprite again; ours ignores the saved place), so this is a byte difference in
  a park file, not a behaviour. Said at `PutSprite` and in `saves.md` ("A walk's rider is saved with a sprite where the
  original saves none or another place, a deviation"). Confirm: a park saved with the Mayan Spinner and the Jungle Spray
  holding riders, the sprite table beside the original's.
- [ ] **Q736. Test seams not closed by Review fixes, group G.** From Q585 6 and Q586 F3 (VERIFY-14). (a)
  `ParkPeople.ThingRemoved`'s own ask of `ParkGuestSprites.WalkedOnRide` (the put-off sound's place for a walk rider)
  has no seam: its result goes only to `ParkAudio.Current?.PutOff( at )`, which needs a device. The pieces are tested
  (`ParkPeople.SoundFor` with `walked`, `ParkPeople.PutOffPoint`), but deleting the ask in `ThingRemoved` (the old
  navigator's-feet point) leaves every test green. A seam (the point returned to a caller, or an injectable sound sink
  on `ParkPeople`) would close it. (b) `ParkGuestSprites`' census line (`Standing`'s `walked is { Hidden: false } w ?
  w.At`, ~1722) is read by the console's `sprites` census only; no test reads it. (c) `Material.SetupResources` hands
  `DepthStateFor` and `RasterizerStateFor` to the device; the call itself is covered by the game run only (a device is
  needed to build a pipeline). (d) `LobbyModel`'s call of `Material.CullModeFor` through `DrawFlags` is tested; that
  every model path goes through `DrawFlags` is not (which draws do not: `render-states.md`, "Which draws the test
  covers", Q608).
- [ ] **Q737. A placed voice out of range releases its output, and a loop comes back with a fresh one.** From the fork
  watch 2026-10-21 (verify-audio-ui L1). OpenTPW plays a placed voice on unheard when it is out of range
  (`audio.md:265`, a recorded gap with no queue line and no `Unimplemented.Report`). The original gives the channel up:
  the range failure sets `0x800` and stops the output (`0x006bca10` -> `0x006bca70`), `0x006bbe40` clears the handle
  (`+0x24`) and the playing bit `0x1` (the terminal `0x10` only without `0x40`, `0x800`, `0x4000`), and the output-end
  callback runs `0x006bb7c0` -> `0x006c6260` -> `0x006c8e50`. A looped placed voice (flags `0x6`), in range again, is
  built a fresh output at the sample's start (`0x006bd800` -> `0x006bc410` -> `0x006b9ab0` with handle 0; measured
  Q565). Reach: Lost Kingdom's looped placed voices (the Round Fountain, `audio.md:1108`). Build it for the placed
  voices that are no script's and no ride's scream (those are already the original's, Q557, Q563-Q565), and count what
  is unbuilt with `Unimplemented.Report`. Confirm: the fountain's loop caught by the disk driver as the camera leaves
  its range and returns, beside the original's (does the sample restart from its start).
- [ ] **Q738. `Advisor.Speak` has no test that it uses its gesture row.** From the run 7 review (fix group H;
  `World/Advisor/Advisor.cs`). Testing it needs the audio (`Audio.Ready`, a `SoundCategory`) and a speaker figure. Ask
  for a seam (the sample length and the clip table handed in) so a test can see that a line given row N plays the row's
  clips, not the random chain. The sweep's call into the speaker is tested (`ParkPeople.SpeakResponse`).
- [ ] **Q739. The pause overlay, and plain P in a park.** From the fork watch 2026-10-24 (`verify-world-other.md` L2,
  M2; `verify-audio-ui.md` L1). Plain P in a park reaches nothing and counts nothing (`ParkFrontEnd.ShortcutRow` has no
  plain-P row; `InputButton.Pause` is read only by `InputBindingTests`): a rule 4 gap; first count it. Then build the
  overlay (`hud.md`, the pause overlay): P's row `0x00747f3c` -> `FUN_0040bf70` -> `Game_TogglePause( 1 )` while
  `[0x00786b68 + 0x3c]` is 1, and the focus loss (`0x0046b74c`), put up `UI_LoadModalTree( 0x00752e00 )`: the LOLIGHT
  dimmer and control 5094 at (174,500)-(1874,900) wearing `s_paused` (`Language/English/paused.MD2` in our tree), moving by
  table `0x00752ed0` every 35 ms (`hud.md`, "The window-animation engine"); every resume takes it down. It replaces
  `FOCUS_LOSS_PAUSE_OVERLAY`; Q577 is its sounds. Decode first what `[0x00786b68 + 0x3c]` is, whether the dimmer takes
  the pointer and whether the banner moves while the timers are held. Confirm: P in Lost Kingdom beside the original's
  (with `paused.MD2` copied into the reference install's `American/`, or after Q733).
- [ ] **Q740. The End's and the end of a park's banners, with their motion.** From the fork watch 2026-10-24
  (`verify-world-other.md` L1, M1; `verify-audio-ui.md` L2). The End's control wears `s_congrats` (`congrats.MD2`) and
  moves by table `0x0074fcf8`; the end of a park's wears `s_bkrupt` (`bankrupt.MD2`) and moves by table `0x0074fc38`,
  each a step every 35 ms (`park-engine.md`, the end of a park; `hud.md`, "The window-animation engine"). Find what
  ours draws in those two controls now, build the engine once (the gadget's tag at `0x007502b8` uses it too) and the two
  banners on it, counting what is not built. Confirm: the End reached in Lost Kingdom beside the original's, with the
  meshes in its `American/` (Q733).
- [ ] **Q651. The till's place: where the original's paying guest stands. Decode first.** From the run 9 review of
  Q611 (`review9/VERIFY-1.md`, Q611 1). Build the original's place for a paying guest's till (kids 208). The original
  sounds it at the person's model position (Person_PlaySound 0x004faa00 hands `[ecx+0xc]` to 0x004754e0, which copies
  the model's +0x88/+0x8c/+0x90, made integers by _ftol before 0x0051bfc0), and ExitRide's teleport (0x004fa930) never
  writes `[esi+0xc]`, so the till is where the guest was last drawn. Ours sounds at the walker after PutDownAtTheExit,
  the exit cell's centre: (525, 305) at the Spray, (435, 305) at the Kiosk, against the original's (525, 300) and
  (434-435, 303-304) (q611/run1/run.log; docs/exe/audio.md, "A guest who pays sounds kids 208"; the deviation is said
  at ParkRideOperation.Charge). First decode which point the model holds at the settle-up: the Spray's (525, 300) is
  its .sam stand point (EntryCellStandPos/ExitCellAppearPos 0.5, 0.1 -> (525, 300.98)) truncated, but the Kiosk's
  434-435, 303-304 is NOT its exit point (Shops.sam's 0.5, 0.5 would give exactly (435, 305)) and varies by guest. The
  source reads neither key anywhere (they are only named in ItemDescriptionFile's key list). Not yet looked at:
  whether 0x50f8e0's deep callees (0x511420, 0x511470, 0x5108a0, 0x511350, 0x4d8750) touch the person's model. Then
  hand the place to Charge through the existing TillPlace seam, truncated to whole units as the original does, and
  confirm in the running game against the original's Sound_PlayEffect hits (q567/orig/brk.sh) for the Spray and the
  Kiosk.
- [ ] **Q653. The cost of the ground at 32-pixel tiles.** From the run 9 review (Q618 M3). The default 32 made the park load
  slower: `[load] jungle: ground took 731 ms` at 16 against `1472 ms` at 32, the whole load 5.6 s against 6.2 s (the same
  dll, `q618/runB` and `runA`), and the atlas is four times the memory (3072 x 2720, 33 MB). Measure where the 0.74 s goes
  (`GroundTiles.Make`'s `Lay` of 8160 tiles, `ProceduralGround.Upsample`, the 33 MB `Texture`, or the `Remake` that makes the
  texture again whenever a path changes), and cut it where it is cheap: `Lay` per tile in parallel, or the texture made
  once and its changed tiles written in place. Confirm with the `[load]` step times before and after, and the frame rate at
  32 beside 16 (not yet measured).
- [ ] **Q654. The screams' chain on a due millisecond.** From the run 9 review (Q612 M3). The original's chain tick extends nothing
  when `now == +0x1c` (`0x006bdb88`, `now > due`) and prunes the newest child (`0x006bdc23`, `now >= due`), so the list is
  empty for a pass and its next child is made as a first (`HeldChain_HeadOrFirstChild`, `docs/exe/audio.md`). Every script
  chain here keeps that pass (`ScriptVoice.ListEmpty`); `ParkScreams.Pump` waits for `now > NextAt` and makes the next
  child as the next of its run, so the kids 71 to 73's chains differ on the one millisecond of a child's time (about one
  child in nine to twenty-five in a script chain's runs, `q612/run5/run.log`). Give `ParkScreams.Chain` the same empty-pass
  rule, with a test that fails when the pass is skipped, and confirm in a run by the first child's variation after a due
  millisecond.
- [ ] **Q655. A sold Loudspeaker's chain carried by a save made without sound.** From the run 9 review (Q612 M4). With no sound
  device `PutBackFileVoices` puts nothing back, so `ParkAudio.FileVoicesLetGo` is empty and `ParkFileWriter` carries every
  voice of the loaded file; a Loudspeaker sold on such a machine is saved with its 177 chain, and that file loaded where
  there is sound puts the chain back under a handle no record names (the thing is gone): `FileOwner` for ever, beasts at an
  empty spot. The original's save with its sound off writes no voice list (FileFormats `saves.md`: records are written when
  both dwords are set). When the park ran with no sound device, leave out every held chain whose handle no surviving script
  record names (or write the list empty, as the original's soundless save does), and say it at `Level`'s save. Confirm:
  buy a Loudspeaker, sell it, save without a device (`SDL_AUDIODRIVER=dummy` is not that; use a test with `Audio.Ready`
  false), load the save with sound, and `PARK_FILE_SOUND_VOICE_*` / the log show no chain for the sold speaker; the bug
  put back fails a test in `ParkVoicesTests`.
- [ ] **Q659. Trim each decoded MPEG clip to the bank's played length (from Q147).** `AudioClip.Decode( name, data )`
  (`AudioClip.cs:54`) takes no length and decodes until `ReadSamples` answers 0, so every MPEG entry plays its whole
  stream, 14.4 to 82.8 ms past the bank's played length (`DecodedSize / (2 x channels x rate)`, the entry's `+0x20`),
  over all 3,739 entries (`Mp2DurationTests`). The original clamps its read to `min( request, [voice+0x30] -
  [voice+0x14] )` before the slot's `+0x5c` (`0x006c8429`..`0x006c8457`, FileFormats `sounds.md` lines 81-84; that
  `+0x30` holds the bank's `+0x20` is FileFormats' claim, not re-traced). Trim each decoded clip to
  `DecodedSize / (2 x channels)` frames. Probably inaudible (encoder padding), so minor. The only user of
  `DecodedSize` outside the tests is `SoundBank.cs:91`'s memory estimate. Confirm: a clip's frame count against the
  played length for the five MPEG-1 entries and a sample of MPEG-2 ones; `docs/exe/audio.md` (the decode, ~line 1444)
  says nothing of the tail and should.
- [ ] **Q660. Does the original free or hide a boundary record no anchor claims? (from Q620.)** At load the original clears
  each anchor's saved handle and re-claims its piece by matching item, cell and angle (`FUN_0053b1a0` through
  `FUN_00522f20( ..., 1 )` and `FUN_00463ab0`, which marks a match `0x10000`); `ParkBoundaryHoarding` draws every
  173xx record without that claim. No park file to hand tells them apart (933 anchors in seven files, every one has
  its record, no record lacks an anchor), but Q249's fresh-park writer and any edited file could. Read the users of
  slot bit `0x10000` (for instance `0x004721b7`, `test [eax+4], 0x10000`) in Ghidra to settle whether an unclaimed
  record is freed or hidden; if it is, build the claim (pair each type 25 cell's `mTileData` with the record at its
  shifted cell and angle, count an anchor with no match) or keep the deviation said at the site.
- [ ] **Q661. Rest at a loop's restart in view (from Q622).** The original restores at a loop's restart in view:
  `0x00473840`..`0x00473848` call `FUN_00472f60` with the same role and entry and flags `1` (`| 8` only off-screen),
  which reaches `FUN_00472310` with the outgoing clip the incoming one: it clears `0x10` on every tracked and listed
  node and re-hides the hide list. Here `RideAnimations.End` restarts a loop with `0x8`, so `PoseChannel` (which since
  Q658 puts back and hides at every start it sees, by the start's flags) shows and hides nothing there. No Lost Kingdom placed thing loops a clip whose visibility track starts after
  frame 0 (the only such clips are three vehicles' `m3`, `coconutc` and `mamfounte`), so nothing differs on screen
  there. Build it where the loop is known, and carry the start's `0x4`/`0x8` with it (Q658's seam). Confirm: a
  looped clip with a visibility track after frame 0 in both games, or a read of the original's node words at the
  restart.
- [ ] **Q682. A loaded park's things drawn as their saved node words hide them (from Q658).** A loaded thing is drawn
  as its construction clip leaves it (`ParkObjects`, the as-built pose: the visibility tracks at the build's end), and
  its channels are restarted with `0x8` (`ParkRides.Resume`), so no hide list is applied and the file's node words'
  bit `0x10` is not read for the drawn meshes. A Fries Shop bought, saved and loaded would show its `chip08` here, where
  the original's running instance holds it hidden (`q658/orig/b/modelnodes.txt`); whether its saved model record keeps
  the bit, and whether its load restores it to the node, is not read. Read it (`parkfile.models()` on a file the
  original saved with a bought Fries Shop, `modelnodes.py PID =fries` after its load), then draw a loaded thing's meshes
  by its words. Confirm: the Fries Shop saved and loaded in both games.
- [ ] **Q662. Run the original on the made file for the advisor's row 257 (from Q713; beside Q743's file B).** The docs now
  say the original's tick 19040, sample 525 and props (8, 0) are from the unmodified `New Save` (`q707/orig/O2`); the
  original has never loaded `files/outlets-D.TPWS` (sideshow 50's thirty satisfaction days made 40). Load it in the
  original with `loadfile.sh` and read row 257's record beside file B (row 248). Also read which sideshow the
  original's row 257 picked on the unmodified file and its figure (the docs say only "some sideshow at 77 or more").
  - **Note (Q743):** on file B the original slotted `0x101` on 19040 at 72 (a sideshow at 88 worked back by 70 + 72 / 4,
    not read; which one was not read) and had not said it by 19062: `0x13a` was said then and the 19100 awards replaced
    the slot. It slotted `0x101` again at 40 on 19350 and it was still unsaid at the last advisor read, 19389
    (`q743/orig/O/load.log`).
- [ ] **Q679. Does the advisor's next line come on the original's tick at a normal frame rate?** From Q743. On
  `q713/files/outlets-B.TPWS` by Q707's F player, after row 248's line on 19031 (a 7,498 ms action in both games), the
  original said `0x13a` on 19062 (7.70 s on at four sweeps a second, `q743/orig/O/load.log`) and this game said it (314)
  on 19067 in two desk runs (`q743/runG2`, `runG3`). That lag is explained: both runs ran below 10 frames a second (every
  clock stamp in their `run.log`s is on a 100 ms grid, 75 clamped frames), and the wait reads `GameClock.Now`
  (`ParkPeople`'s `AdvisorMessages.Tick`), which adds `Time.Delta` clamped to 0.1 s (`Time.cs` `LongestFrame`) while the
  sweeps take `RawDelta`, so `Now` fell about 1.5 s (7.500 s over 36 sweeps) behind: the documented deviation
  (`advisor-park.md`, the paragraph "The per-message cooldown reads...", `ParkPeople.cs:2444`). Still open: does this game say row 314 on 19062
  when it runs at a normal frame rate? Re-run Q743's scene at full rate (no console poll every 0.2 s, no frame grab;
  `q743/confirm.py` polls) and predict 19062 first. If it says 19062, close this; if not, find the cause there.
  Confirm: the same file, `0x13a`'s tick in both games.

## B. Docs and comments

- [ ] **Q14. Comment sweep of the cleanup commits.** Replace history-voice comments with what the code does now,
  found by member because line numbers go stale. Left: `AudioListener.AttenuationTo`'s summary nests a `<para>`
  inside a `<para>`, and `ParkCamcorderCameraMode.DebugWalk` steps a per-frame `WalkSpeed / 60f`; make it use
  `Time.Delta` (rule 10). No game run.
- [ ] **Q81. The FileFormats docs still disagree with themselves and with the game.** From the clone's 2026-09-18
  audit, as the 2026-09-24 staleness audit found it (`docs/history/fileformats-docs-ledger.md`). Most of it is fixed,
  and every branch that held a fix is merged into the clone's master, so no page exists in two versions and the
  `.md2` format has one page (`models.md`). Still wrong: `saves.md`'s `mFlags` `0x8` bit, its "version 85" (hex), and "the sixth
  person model is the visitor"; and `wad.md`'s name length, which includes the NUL. Open on both sides: what `0x14E`
  means (`SpriteBankFile`'s note). The Bumper enum is not a page error: the page's 1-17 is probably right, and it is
  `ScriptDefs.cs`, a labelled leftover left as it is, that looks corrupt. Fix each on one short
  branch off the clone's master (`docs/README.md`, "The FileFormats docs clone"). No game run; the
  site builds.
  Read by the week-end audit (2026-10-11), not re-measured: `wad.md`'s name length and the `mFlags` `0x8` bit read
  corrected. Still to judge: `saves.md`'s version `85`, its "sixth person model is the visitor", `0x14E`, and
  `vm/info.md`'s per-script variable ids.
- [ ] **Q75. The 2026-09-12 review's Phase F, what is left.** Opportunistic, never alone. Left: `SixLabors.ImageSharp` 3.1.6 in
  `OpenTPW.ModKit.csproj`; `content/textures/test.png`; `.editorconfig`'s `end_of_line = crlf` with no
  `.gitattributes`; and, to LABEL as dead by CODE rather than delete (rule 3), `Public/ModelFile.cs` and
  `Public/MapFile.cs` (both unreferenced), `Render/Primitives/Cube.cs` and `ReadUIntN`. `Singleton.cs` is used by
  `UI/Cursor.cs`, though nothing reads its `Instance`. Left to Alexah's call, not to be started: `.gitignore` for `.claude/`,
  `.vscode/` and `.mcp.json`, ModKit as a whole, and the nullable warnings except in passing.
  Also left (the 2026-09-26 staleness audit): the `Zio` 0.17.0 package in `OpenTPW.Common.csproj`, which no source
  file uses and which carries a low advisory (`DECISIONS.md`, "Package advisories"), and the `PreferredBackend`
  setting in `App.config` and `Settings.settings`, which nothing reads (`Settings.Default` is read for `GamePath` and
  `GameWindowSize` only). Label both as dead by CODE at their sites; remove either only on Alexah's word.
- [ ] **Q161. Stale text in code strings, test names and one census key.** Found by the 2026-09-26 staleness audit,
  whose edits were to comments only. `Extensions.GetColor`'s `[Obsolete( "Use Color.Parse" )]` names a method that
  does not exist, and nothing calls `GetColor` (dead by CODE: label it, rule 3). `RideEffects.Trigger` reports
  `effect type {type} (TRIGGEREVENT)`; the instruction is `EVENT` (`Opcode`), beside `(ADDOBJ)`. The console's `load`
  replies "they come one a tick"; `StepArrivals` admits one a thing sweep. Assert messages: `AnimationEasingTests`
  ("an older note here quoted"), `ParkRideJoinTests` ("only 13 and 14 can be"; six pass the filter),
  `ParkStateTests` ("the handyman" of thing
  26, the mechanic), `ParkTickTests` ("neither of which this tree had"), `ParkPathBuildingTests` ("the loader's
  runtime flag is a different thing", which `park-engine.md` calls not established), `PeepWalkTests` ("at 31ms a
  tick"; a step is a thing sweep), `RideAnimationsTests` ("this branch"), `PeepNeedsTests` ("nothing else makes a
  guest hurry"; `HurriesToTheGate`), `RideScriptModelTests` ("where the advance sat"), `PeepNavigatorTests` ("the
  longest route in the shipped park", true of the save only) and `SoundCategoryTests` ("happened to work here"). Test
  names: `AnimationUvTests.ATwoKeyTrackReadsAsItAlwaysDid`,
  `BalanceFieldTests.AKeyThatNamesOneFieldIsReadExactlyAsBefore`,
  `GameCalendarTests.TheDaysKeepTheOriginalsSlightlyFastPace` (it asserts 10 January on tick 230 and 11 January with
  `DayRolled` on 231, the tick's date, and nothing of the 248 ms pace),
  `ParkEvictionTests.EveryQueuerIsPutOutOfTheQueueAndLosesBothChanges` (its summary: the drain has already put out all
  from the fifth place back) and `ParkWeatherTests.TheWeatherTurnsEverySevenDaysAndIsDecidedThreeDaysBefore` (four
  days, `weather.md`). `ParkPathTests`' summary says "SEVEN tests fail" without the `ReferenceEquals` guard, measured
  when sixteen classes built an edge test; twenty do now: take the guard out again in a worktree and write the count
  taken, or drop it. No game run.
- [ ] **Q204. Load the area-only notes only in their area.** Claude Code loads a `.claude/rules/*.md` file whose
  `paths:` front matter matches an open file, and no other time. Move the sections of `CLAUDE.local.md` (and any of
  `CLAUDE.md`) that serve one area only - the capture recipe, the original under Proton, the 2.0 patch's evidence - into
  such files, keyed to the source and harness paths they serve, so a session that never opens them never loads them.
  Measure both files' size before and after, and confirm in a fresh session that a matching file brings its rule in
  and a non-matching one does not. Borrowed from Rootstock (github.com/Mazhron/rootstock-os); nothing else of it is
  taken. Alexah's yes 2026-10-01, for after the weekly reset. No game run.

- [ ] **Q292. Cut `docs/VERIFYING.md` to its rules.** At Alexah's word (2026-10-11). The page is 770 lines, each
  rule with the story of the mistake behind it; `MEMORY-DIET.md` set it 85. Each rule keeps its number and one
  line of why, and its story moves, as written, to `docs/history/`. No rule is dropped or renumbered: every page
  and note that cites a rule by number must still find it (grep `rule [0-9]` first). No game run.

- [ ] **Q293. Three `FUN_` labels Ghidra has not disassembled, and nineteen names the docs do not use.** From the
  week-end audit (stage 1). `ride-operation.md`'s rows on the loans name `FUN_004d0970`, `FUN_004d0a00` and
  `FUN_004d0810`, which are undefined bytes in the Ghidra project, so nobody has read them there: disassemble at
  each, read what it does, and correct the rows. And nineteen functions the docs call `FUN_` have a name in the
  project (the harness folder's `audit-2026-10-10/findings/s1-ghidra-named.tsv`): write the names in the docs.
  Name them and make them functions in Ghidra as the reading finds them (Alexah, 2026-10-18); a name there agrees
  with its `docs/exe/` row. No game run.
  - **Note (fork watch 2026-10-18, `verify-advisor.md` A1):** the three loan helpers were read in the executable's listing, not
    in Ghidra: `FUN_004d0810` (some offer passes and is untaken), `FUN_004d0970` (a taken offer's remaining owed) and
    `FUN_004d0a00` (the total owed), and `ride-operation.md`'s rows on them are corrected ("The loan helpers and the money
    rows"). The Ghidra project was not touched: their functions and names are still to make there.
- [ ] **Q579. The voice list's reader and writer named in the Ghidra project.** From the review of run 6 (VERIFY-17,
  Q559 4; group O's notes). The project's `SoundManager_SerializeVoices` (`0x006b6da0`) only reads the voice list
  (stream slot `+0x8`), and its writer `0x006b6c30` (slot `+0xc`) is unnamed. Rename both in the project (a reader and
  a writer name), then use the new names in `audio.md`'s tables and "A script's sounds" and in `saves.md`'s `SOUN` row, which keep the
  old name today. Docs and names only; nothing to confirm in the game.

- [ ] **Q294. The FileFormats pages changed since 2026-09-26, against the game's files.** Left by the week-end
  audit's stage 8. 1,557 layout statements on 22 pages, `saves.md` and `models.md` most of them, packed into 56
  areas (the harness folder's `audit-2026-10-10/ev/s8a-00.md` to `s8a-55.md`). One `tpw-verify` an area checks
  each against every file of its kind with the readers (`lib/parkfile.py`, `lib/sfxmap.py`, `lib/md2surf.py`,
  `wadcat`, the `.RSE` corpus); then the site is built. No game run.

- [ ] **Q296. Three statements Q291's verifiers could not settle.** Each is said as fact with no measurement
  beside it. `park-engine.md`, the object window's preview: "each with one clip in role M" (the Space zone's
  `megacost.wad` carries `Pmegacostm.MD2` and `PMEGACOSTM1.MD2` both; read which the loader takes). The same
  page, the footprint picture: "the lowest row of pixels stays clear" (rests on whether `FUN_005f9a80`'s
  rectangle takes its far edges: read the fill behind `[0x00fa76d0]`'s slot `+0xc`, or measure a frame of the
  original). `park.md`: "`WALKGET` returns only a rider `WALKOFF` let off (`FUN_00557110`)" (`FUN_00557110`
  answers a slot in state 4 and `WALKOFF` writes 3: trace the walk update's writes at `0x00557e6c` and
  `0x00558018`). Confirm: each settled from the listing, the page saying what it shows. No game run.

- [ ] **Q466. The texture-alpha pages and two FileFormats pages, read again whole.** From the week-end audit,
  2026-10-17 (`VERIFY-14a.md`, `VERIFY-14b.md`, `VERIFY-15a.md`; `CLAUDE.local.md` says where). Q357 to Q362 rewrote the
  alpha rule in turn and left `textures.md` (:19-20, :34-38, :51-58, :86-97, :123-124, :234), `lobby.md` (:215-239) and
  `render-states.md` (:79, :161, :166) contradicting the code and each other: nothing keys black in the interface; the
  loader's `0x800` makes floors opaque; the skirt pass clears the current group's material when the edge byte is nought;
  mode 1 reverses each texel's bytes (`0x005ecf5e`). FileFormats `formats/sgn.md:228-230` keeps the old multiplier's alpha
  figures and `formats/models.md:747` calls bit 0x02 the only one established (rule 14). Rewrite each from the tip's
  code and the listing; no behaviour change.

## C. Alexah's list: cause known, one session each

- [ ] **Q15. Green line between path cells and on ride signs.** Path textures get the default sampler,
  `AnisotropicWrap` = Wrap (`ParkPaths.Build`, through `Material.Set`'s array form). Sign halves are bound the same way
  by `LobbyModel` and wrap or clamp by their frame's bits (`SignTexture.TryBuild`). The original clamps every texture
  whose frame carries no wrap bit (`render-states.md`, "Texture addressing"). With wrap plus mipmaps, a tile's edge
  samples the far edge. Use clamp where the original does: the path tiles' and the ground's textures are not models,
  so read first what the original binds for them. Confirm: screenshot two paths in a column, a crossroads and a ride sign,
  before and after.
  A model's texture clamps unless its frame wraps (Q363). `ParkPaths` and `ParkGround` bind an array, which
  `Material.Set` samples as Wrap whatever the flag; `Texture.SamplerFor`'s Mirror is used only for a texture bound
  alone; the sea keeps its wrap.
- [ ] **Q17. Camcorder mode can strafe.** `ParkCamcorderCameraMode.Walk` passes
  `Input.Right` into `Step`. `park-engine.md`, "Walking on the ground is swept against the cell edges", decodes the sweep by two velocities but not the
  keys. Remove the strafe. Confirm: press the strafe key, the stand position in the `camcorder` census
  does not move sideways, screenshot.
  - **Note (play test 2026-10-21, note 33):** Alexah felt the strafe as part of an arcade feel; see Q25's note. Q631
    decides which of A and D remain in the orbit view; the camcorder's A and D stay strafe-free (the original has none).
    (Inferred from the notes: Alexah's WASD request is stated for the camera scroll, notes 6 and 11, not the camcorder.)
- [ ] **Q18. The advisor draws in front of the dimmed screen.** `Level.Render` draws the HUD, then
  the overlay pass with the advisor. Pausing does not hide him either; only the options screen's quiet stop takes him off (`ui.md`, "Pausing him"). Draw the
  dimmer over him, or hide him while such a window is open. Confirm: screenshot with the buy screen open.
- [ ] **Q19. VSync and a frame limiter.** `Renderer.CreateGraphicsDevice` hard-codes VSync on. `Display.cs` already
  carries each mode's refresh rate. Add an Options row: VSync mode, and a frame limit from 30 up to
  Unlimited, default the monitor's refresh rate on first launch. Confirm: screenshot the options row;
  log the measured frame time at two settings.
- [ ] **Q20b. No bubbles on the drinks shop, no chimney smoke on the staff room.** The scripts ask for the effects and
  `RideEffects` records the request; nothing consumes the records. Join them to `ParticleSystem.Spawn` through Q20a's
  pass (`ParLib.cs` names `Bubbles = 58`, `Smoke = 2`, `SmallSmoke = 13`; `park.md` decodes the node and the per-tick
  push). Confirm: screenshot the drinks shop with bubbles and the staff room with a staff member inside and smoke
  rising; log lines for both spawns.
  - **Note (fork review, 2026-09-30):** part of what Lost Kingdom's effects need comes from the save, not from new script
    requests. The `PART` module (written by `FUN_0051f680`, read by `FUN_0051f7a0` at `0x00415437`) restores the live
    emitters, and `Particles_KillAllOnScreen` (`0x005200b0`) then kills every screen-drawn one, so after loading
    `Easymode.TPWI` two world emitters live on: WaterFall at slot 0 and Bubbles at slot 20. A saved script's `ADDOBJ`
    handle points at them through the `RSSE` module's `OBJ ` records, which `ParkScriptStates` skips today: seed the
    emitters only together with those records, or `Coconut.RSE`'s `KILLOBJ 1` can never stop the bubbles. The theme's
    `.emt` files go into the first empty template slots, 101 and 102 in jungle, never past 104. Layout: Q193. Review
    items fmt-assets-v1, gap3-6, gap5-1..gap5-4.
  Read by the week-end audit (2026-10-11): the bubbles and the seeding with the OBJ records are done (Q257i,
  Q258). What is left is the staff room's chimney smoke, which has no trace in the tree.
- [ ] **Q21. Entering a park: hide the front end for the fly-in.** `IslandPanel.EnterPark` closes only the
  island panel, and the rest of the front end stays up while the camera flies in. Hide it for the flight, and
  say what Escape's cancel (`FrontEnd.MenuKey`, `LobbyCameraMode.CancelLeave`) brings back - today only the
  island panel. The gate half landed with Q41: the flight plays M1 as the homing ends and M2 on a cancel
  (`docs/exe/lobby.md`, "Escape cancels the fly-in"), so do not re-pace it. Confirm: screenshot burst from the
  click to the loading screen.
  - **Note (Q630):** measured on Lost Kingdom, `windows` answers none 0.5 s after Enter, and no frame of the flight
    shows a front-end control (`q630/run4`): check whether anything of the front end still shows before building.

- [ ] **Q295. An object window's preview starts from the angle the last one left.** From Q291. The first angle is
  `fmod( [0x007afd50], 6.2831855 )` (`0x00468e2f`; `park-engine.md`, the object window's preview): the record is
  static and its angle nought in the image, so only the first panel of a run starts from nought. The preview here
  starts every one from nought and counts it (`OBJECT_PREVIEW_START_ANGLE`, `ParkObjectPreview`). Build: keep the
  last panel's angle for the session and start the next from it. Confirm: two ride windows opened one after the
  other in both games, the second's first frame photographed, and the angle in the log.

## D. Alexah's list: decode first, then build (two sessions each)

The decode session writes the finding to `docs/exe/` and stops. The build is the next session.

- [ ] **Q22. Riders sit still on the Belly Bounce.** Seat positions are read once from the model's rest
  pose (the `LobbyModel` constructor) and never from the animated pose. A riding peep's sprite is `None`
  (`Peep.AnimationFor`). Decode: which frame a rider shows, and how the original re-resolves the seat
  node each frame (`ride-operation.md`, "Where a rider is drawn", and `FUN_005580a0` under "The WALK family").
  Then build both. Q175 re-read the lookup by id and flag (`audio.md`, "Node lookup is by id AND a capability flag")
  and decoded where a node's position comes from (the posed matrix, refreshed on screen; `ride-operation.md`, "How
  long a leg lasts, and where its ends are"), and `FUN_005580a0`'s interpolation; which frame a rider shows is still
  open. The engine finds a bounce rider's node by id in `0x800` ("Where a rider is drawn"); `ModelFile.FindNode` is
  that lookup and `RideNodes` the stored positions at rest (Q175b), not the animated pose a rider needs. A walk's riders
  are placed by `FUN_005580a0` from the posed nodes since Q585 (`ride-operation.md`, "Where a walk's rider is drawn").
- [ ] **Q23. Camera rotation snaps by 45 degrees.** `ParkOrbitCameraMode.Update`, the two rotate keys. The 90-degree
  option exists (`GameOptions.NinetyDegreeRotation`, set on the Options screen and kept in the player file) and the camera does not read it. Decode the original's step
  and its easing (`park-engine.md`, "The park camera", has the saved and required rotation, not the rate). Build what
  the decode says, driven by `Time.SmoothingFactor`. Alexah: match the original, do not invent.
  From Q241f: with the options byte `0x0078d912` set the original's update turns a quarter turn a press
  (`0x0042b489`, `0x0042b4a4`) and its loader rounds a saved rotation to a quarter (`saves.md`, "Loading the
  modules", Camera), so a park saved here at an eighth turn opens a quarter away there. Seen beside the original
  at the same zoom and point (`q241f/sheet-ours-beside-original.png`): OpenTPW's view takes in far more ground.
  From Q440 (`park-engine.md`, "The turn"): held, the Left arrow and keypad 9 raise the rotation 0.002 a millisecond
  and the Right arrow and keypad 7 lower it, and the drawn rotation eases after it by min(dt, 140) / 140 a frame (min(dt, 200) / 200 with the 90 degree option
  `[0x0078d912]` set: this item's own option); the
  direction is already the original's (this game's yaw is its rotation negated, and the Left arrow,
  RotateLeft, lowers the yaw). The step and the easing are what differ.
  From Q626 (`park-engine.md`, "Entering and leaving first person"): leaving first person puts back the asked rotation
  and leaves the drawn one at the walk's heading, so the view eases round to the saved one, 1.36 s from 98 degrees off
  (`q626/orig/b-poll.log`); `ParkCamcorderCameraMode.Leave` puts the yaw back at once. The ease built here carries it.
  From the run review (2026-10-17, Q440): W and D scroll the orbit camera (`ParkOrbitCameraMode.cs`) without ending a
  locator's follow, where any pan in the original does (`0x0042ba37`, `0x0042ba92`; `ParkLocator.Following` is cleared only
  at its own fold, a mode change, a person gone and a step that finds nobody), so scrolling with the locator following
  does nothing here: the follow puts the point back each frame. Built for the keys by Q631 (`ParkOrbitCameraMode.Panning`,
  read by `ParkLocator.Update`); the pointer's push (Q27) is not built. **Note (Q631):** keypad 9 and 7 turn in the
  original (camera actions `0x12` and `0x13`) and are not bound here.
  From the fork watch (2026-10-18, `verify-world-a.md` M1, `verify-other-a.md` M1; `park-engine.md`, "A sprite's camera
  sector reads the asked rotation"): the original picks a sprite's camera sector from the **asked** rotation
  `[0x00790a38]`, not the eased one, as `8 - (trunc( (rotation + pi/8) x 8 / 2 pi ) & 7)` (`0x00542010`), and a draw
  with flag `0x40000` gets sector 0 (`0x005421cc`). `ParkGuestSprites.Facing` reads `Camera.Rotation.Forward` (about
  line 571); that is the same today because `ParkOrbitCameraMode` snaps and has no ease, and it becomes a difference when
  this item builds the ease: the sector must then follow the asked rotation. The rounding agrees; the sign against
  `0x00790a38` is read only from the code and its own note says the screen settles it.
- [ ] **Q24. Nothing highlights a thing under the mouse.** The idle pointer already follows the hover category over
  ground and path (`Level.IdleOverPath`). Picking is decoded (`park-engine.md`, "Picking is a real ray cast, not a
  grid lookup"): the hit point comes from a ray against the terrain and then object meshes, and the hovered THING is
  taken from the cell, not by hitting its model (`ParkPicking.ObjectUnderCursor`; a person by their picture, `PersonUnderCursor`). Decode what the hover updater
  `FUN_00486d90` highlights when the hovered thing is an object or a person, then build it. Include the park gate: its
  arm calls `FUN_00469180` (`0x004878ce`, the highlight swap on `[0x007afda0]`), which the ticket booth's arm does not
  (`park-engine.md`, "The gate and the ticket booths"); the hover calls it at six places. Seen in the original (Q478):
  the pointer on the Belly Bounce brightens its model (the frame's box (180,220) to (300,350) reads red 160 and 173 in
  `q478/orig/b-3-ride.png` and `b-4-ride-again.png`, 138 and 141 in `b-2-queue.png` and `b-5-shop.png`), the object
  arm's calls at `0x00487712` (a new object) and `0x004878ce` (the same one). From Q494: `[0x007afda0]` is the model the picker's object march met (`FUN_00469340`), which
  ours keeps as `ParkPicking.Marched` (the thing), whether or not its point beat the ground's.
- [ ] **Q25. The camcorder button should give a crosshair and place the camera where you click.**
  `ParkGadget.EnterCamcorder` enters the mode at once. The original installs a mouse-interaction mode
  (`FUN_00481a10`, `park-engine.md`, "Camcorder mode — the first-person view"). Its click handler was decoded by Q48
  (`park-engine.md`, "Entering and leaving first person"): cursor `0x13`, a left click on a cell of type 0, 1, 3, 9 or
  30 stands the viewer at the picked point, and leaving puts the saved point of interest and yaw back (built by
  Q626). It is also Q48's hole (3). Left to decode: the click's own cell tests and its thing branch. Then build.
  From Q121: measured in the original, one forward term (the Up arrow, or a held right button) walks about 21.8 units a
  second at zoom word 110, both together twice that; `WalkSpeed` is a chosen 40. The velocity is not decoded to a speed
  (`hud.md`, "Four ways out of camcorder mode"): decode it with this, then set the speed. The original's walk keys
  are the zoom's, Up and Down (bits `0x20` and `0x40` of the button word, measured); here W and S walk and the arrows
  do not.
  From Q496 and Q497 (`q496/orig/`, `q497/orig/`, `helpbar.py`): the pick wears `0x13`, `c_cam.ani`, and posts rows of
  its own, 580 "{P2}: Left-click to ride" over a ride and 579 "Left-click to use camcorder mode here" after it, and
  none over ground before (`hud.md`, "The help bar in first person"); build them with the pick. Its
  click on a hoarding (617, 378), and one at (700, 450) or (617, 500), left the pick up (the no-entry cursor, seen
  at (617, 500)); one at (512, 420) or (450, 450) dropped it back to the orbit's mode `0x6fea10` with no first person;
  one on the path inside the gate (290, 395) stood the viewer there.
  - **Note (play test 2026-10-21, notes 32 and 33):** the camcorder button drops straight onto the ground with no
    crosshair to choose where; the camcorder strafes and feels like a fast arcade game with a lazy camera. The crosshair
    is this item's own text (cursor 0x13, QUEUE:1923-1926), the speed is the "chosen 40, the original's 21.8"
    (QUEUE:1927-1930), strafing is Q17 (QUEUE:1832). Note 34 (the camera after leaving) is split into Q626.
  - **Q691 (filed here, from the Q626 review, run 9): the wheel in the original's first person.** The viewfinder layer
    passes every message to `FUN_0042a760` (`0x00488b6a`); the wheel (message `0x1000d`, byte table `0x0042a978` entry
    11 -> 4 -> `0x0042a91d`) adds -10 or +10 to the zoom `0x007909ec` with no flag test, so it changes the walk's speed
    (`sqrt( zoom ) x 0.02`, `0x0042bc2a`) and the zoom the orbit comes back at (clamped 70..130, `0x0042c621`). Read from
    the listing only. Ours (`ParkCamcorderCameraMode.Update`) reads no wheel there and counts nothing. Measure it first
    (`campoi.py` polling the zoom and the walk's speed), then build it or count it `FIRST_PERSON_WHEEL`; confirm both games.
- [ ] **Q27. Pushing the mouse at the screen edge does not scroll.** The "push scroll" option exists and
  is read by nothing. `ParkOrbitCameraMode.Update` scrolls from keys only. Decode the camera
  binding table at `0x00748158` (`park-engine.md`, "The park camera"), then build. **Note (Q631):** the push feeds the
  term the keys now drive (`ParkOrbitCameraMode.PanInput`'s two inputs into `PanTarget` and `StepScroll`, the original's
  `(input - 0.95) x 10`: a key is the pointer at the frame's very edge, 0.5 there, which `PanTarget` takes as 1).
  From Q440 (`park-engine.md`, "The follow"): the original pans at the edge only with `[0x0078d910]` clear, an Options
  toggle (written at `0x004a3882`, control `0x1d4c9`); the update zeroes the pointer's terms when it is set. Which of the
  Options screen's rows it is, and whether it is the "push scroll" row, is not read.
  - **Note (play test 2026-10-21, note 4):** Alexah finds the game unplayable without edge scrolling; push to scroll
    does nothing. The original pans only with `[0x0078d910]` clear (`park-engine.md:412`); nothing here reads the option
    (QUEUE:1938-1939).
- [ ] **Q28. Renaming parks and rides.** The save carries a name per park (`saves.md`, the `gms.dat` theme record's park name). No rename
  control exists; signs read fixed names (`ParkFixedItems.BuildSign`, `ParkObjects.BuildSign`,
  `LobbyIsland.DisplayNames`). Decode where the original edits the name and what it writes, then build the
  control and make the three sign painters read the save's name.
  - **Note (fork review 2, 2026-10-16, `verify-objects-signs-other.md` H2):** `0x004990f0` opens the box (two
    14-character edits, title UITEXT `0xc2`); its OK, `0x004993e0`, sets the gate thing's two lines (`0x004e2950`,
    `0x004e2a50`, 33 wide characters each), repaints the sign (`0x004682b0`, the hardware path only), writes the
    player's `gms.dat` theme record (`0x005b0940`, `0x005b0970`: `mSignNameA`, `mSignNameB`, `mNameChanged`), and
    records the rename as action-recorder entries `0x50` and `0x55` (`0x00403fd0`, replayed by `0x00404f70`), which is
    how it outlives a save. Lines reading "Kevin the" and "Cosmic Cow" turn the cheats on (`0x004053c0( 1 )`). A ride
    board takes its name from `OBJECT_NAMES` until a load (Q399).
  - **Note (fork watch 2026-10-19, `verify-audio-fx.md` M1, `verify-other-b.md` M5; `hud.md`, "Which window the click sound is
    for ... the rename boxes' keys"):** in the two-line boxes (park, object) Enter on the **first line moves the focus to
    the second with no click**, Tab swaps the lines, Enter on the second applies and clicks (`0x1f`), Escape closes silently
    (`0x00658d9f( w, 4 )`). Our generic Enter click (`WindowStack.Keyboard`, `UiSounds.Click( toggle: false )` before
    `front.Accept()`) would click on the first line's Enter: the box must move the focus without it. The **staff rename box**
    (`0x004b6110`, callback `0x004b6320`, table `0x007572e8`): one edit of 32 characters, title UITEXT 195, first text the
    thing's current name, write through `0x00506830` (no length or empty test), refused while a box is open or with no
    selected thing of class 4..8; a box does not open a second time while one is open.
- [ ] **Q29. Parity with the original's hardware renderer.** `render-states.md`, "Texture addressing", counts the
  MIN/MAG/MIPFILTER and LOD-bias sites but does not decode their values; no shade-mode row exists; ZFUNC
  is LESSEQUAL there and Less here. The renderer lights per pixel with 16x anisotropic filtering and
  full mipmaps (`test.shader`'s fragment `main`, `Material.CreateSampler`). Decode the values, then match them. Confirm:
  side-by-side screenshots of the same view.
  - **Note (Q617):** the filter values are decoded (`render-states.md`, "Texture filtering"): MIN POINT but on a wrapped
    texture, MAG LINEAR but under word bit `0x20000`, MIP chosen once from `TEXTUREFILTERING` and the caps, LOD bias 0.
    Which MIP the reference install's caps give is not read live.
- [ ] **Q37. The placement terrain rule.** `ParkBuilding.Refusal` counts `PLACEMENT_TERRAIN_RULE` and lets a
  thing stand on types 2, 7 and 30 and on land flagged `0x40`, outside the park. The original allows a
  footprint only over in-park bare ground or path (`FUN_00535600`, `FUN_00535670` refusing `0x40` at
  `0x005357c7`); whether every placement route goes through that verdict (its op-`0x104` branch) is
  still open. When it is built, `Unstamp`'s two declared deviations go: keeping `0x40` where the original
  clears the whole flag word, and giving terrain back its save's record rather than clearing it.
  - **Note (fork review, 2026-09-30):** the `.hmp` adds nothing here: its mark plane equals `Info.Shape` in all 274 pictured items
    (Q191).
- [ ] **Q38. What building and selling look and sound like.** Counted, not built: the death particle
  (`DESTROY_PARTICLE_EFFECT`, `Info.DestroyParticleEffect`, 75-78, a world effect spread over the
  footprint) and the demolish sound (`DEMOLISH_SOUND`, `0x96`-`0x99` by size). Not yet counted: the
  build side - `Info.CreateParticleEffect` and the `+0xd4` sound zone the script loader makes when an
  item has one (`park.md`, "What selling a thing does to its script"). The world pass to draw them in is `WorldParticles`.
  - **Note (fork watch 2026-10-16, `verify-audio-fx.md` A6):** particle caller ids read in the listing: `0x004d95bf`
    50, `0x00544dd8` 2, `0x005480fc` 96, `0x0055dba3` 39, `0x0055dc22` and `0x0055dca1` 40, `0x005999ad` 87,
    `0x0048de1a` 91; also `0x00546ac7` and `0x0054aed5` (15), `0x00549d04`, `0x0054ad24`, `0x00557334`, `0x00557f0e`
    (19), `0x004b8f6e` to `0x004b961c` (97, 98), `0x00548481`, `0x00548a4b` (1), `0x005488d4` (10). Which fire for a
    Lost Kingdom build or sale is not read.
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
  counterpart, and neither have the replays it times: the lobby's beds and one-shots, the park's weather
  and SINGLESCREAM. The original's repeating voices come from the flags word instead. There are two kinds:
  - `0x0404` chains, built by Q9 for the screams only (staff 188 and the ambient beds are not);
  - the `0x4|0x2` voices whose constructor is not the music's `0x400` one: `0x006be090` (106 records), which hands a variation's wait to the mixer, and `0x006be680` (2). They are undecoded; music 2, kids 91 and ambient 33 are the music's class, which is decoded.

  Also owed:
  - the top-N voice pool, keyed by priority and closeness (N is 12, at most 30);
  - weighted variation picks (4 of 63 effects are uneven, and `SoundCategory.Pick` is even);
  - whether parameter 6 is also a level (OpenTPW hears it as the scream's gain);

  From Q135: in the original's mix the performance's end (`TADA.mp2`, staff `0x87`) was found on five of ten ends
  and two of the others fell where the mix was near silent (`ride-operation.md`, "The staff's sounds, in both
  games"): the voice pool may be why, and it is not read. A placed voice there matches its sample at 0.24 at best.
  Decode the `0x4|0x2` class's `0x006be090` and `0x006be680` constructors first. Confirm: capture the lobby's mix and a park's mix, before and after.
  The class's `0x400` constructor is decoded and the park's music follows it (the 2026-10-06 review's fix 4;
  `audio.md`, "The music's class"): parameter 4 picks the variation and an empty park is not silent. Its other
  constructors are not: `0x006be090` (106 records) and `0x006be680` (2).
  - **Note (fork review 2, 2026-10-16, `verify-audio-lips-video.md` leads 4 and 1):** the pool is rebuilt every
    service pass (`0x006b6ba0`): a voice is admitted when not finished, expired, replaced (`0x100`) or waiting
    (`0x40`), passing the device gate `0x006bc760`, in range or flagged `0x8` (`0x006bca10`), and keyed strictly above
    the pool's minimum, by insertion sort, the earlier voice keeping a tie. The key's low word is `0xFFFF` less round(
    0.01 d² ) over x and z (`0x006bb9f0`), left stale past 65535; only pooled voices are ticked. Screams carry
    priority 2700, the crowd 5300. A script's sound id its category lacks is silent with no fallback (`park.md`):
    check this game plays nothing for jungle `incagod`'s 43 and `Speaker2`-`4`'s 6.
  From the run review (2026-10-17, Q430): `ParkAudio.VisitorWindowOpened` (kids effect `0x81`, played on every build of the
  visitor's window in the original) keeps this throttle, now said at the site: a window opened again within the clip's
  length plus RepeatDelay is silent here. Pass `respectDelay: false` there, as `PutOff` and the yawn do, if the throttle
  stays when this item is done.
  - **Note (fork watch 2026-10-18, `verify-other-a.md` M3):** the pool's tie keeps the **newer** voice: `0x006b6a80` links a new voice at the
    list head, so the rebuild (`0x006b6bc0`, strictly greater than the lowest rank) sees the newest first. This corrects
    "the earlier voice keeping a tie" above (`audio.md`, "The fade, the SoundInfo settings and the reverb").
- [ ] **Q51. Rest-area occupancy is unbuilt at both ends.** Found by Q36's decode. Arriving to rest
  (`FUN_00505fe0`) adds one to the rest area's script variable 0 (`VAR_STAFFIN` in every staff room) and sends
  message 15 to the resting-staff list (UI control `0x1e7b`); leaving (`FUN_00506d10`, from the normal end of a rest
  and from a sale) takes the one back. Neither half is built, and all three sites count `REST_AREA_OCCUPANCY`. Decode
  what the staff-room script does with `VAR_STAFFIN`, and whether a resting member of staff is hidden (entering state
  3 frees the sprite). Confirm: a guard resting, `rides` showing the room's variable.
  - **Note (play test 2026-10-21, note 35):** staff never go into the staff room and vanish; they stack at its entrance.
    Q326's photograph shows the guard at the door (`ride-operation.md:2957-2958`); whether a resting member is hidden is
    the decode this item leaves (QUEUE:2034-2036).
- [ ] **Q55. A member of staff going idle queues no stand.** Found by Q36's review. The original's state-0 setter
  queues animation 3 every time (`FUN_005054d0` case 0, through `FUN_004fa460`: `PUSH 3`, `FUN_004217f0`);
  `Staff.AnimationFor` answers nothing for `Idle`, so a member who stops keeps the cycle they had - a guard put out of
  a sold rest area walks on the spot. Check every state-0 entry the original makes against the kinds' own sprite
  scripts before adding it. Confirm: a guard walking, then idle, `guests` showing the set change, photographed.
  - **Note (play test 2026-10-21, note 21):** staff sometimes walk in place. Q55's guard put out of a sold rest area is
    one cause (QUEUE:2037-2041); the note names no case, so confirm in the play which others there are (Q281, QUEUE:839,
    is the other known one).
- [ ] **Q52. A rider on a thing without flag `0x20` is hidden in the original.** Found by Q36's decode. Admission
  tests the object's flag bit `0x20` (`0x0050212b`) and, without it, destroys the rider's sprite (`0x00502147`);
  OpenTPW never hides a rider. Every visitable thing in Lost Kingdom's save carries the bit, so nothing there shows
  it; a thing bought this session carries the bit its item gives it (`UsageInfo.RideHandlesSprite`, read since
  Q257c), so a bought Crazy Ape, whose item leaves it clear, is the case to run. Then
  build both. Confirm: a guest riding a bought Belly Bounce, `guests`, photographed.
  From Q252: **a rider on a thing WITH the bit is not standing.** In two files of the original's (Alexah's played
  jungle park and `q252/orig/ctl-written-by-the-original.TPWS`) all nine bounce riders' sprites are on program 66,
  the second state script, picture set 2; OpenTPW's rider stands on program 90, and a park file is written so.
  `Peep.AnimationFor`'s remark reads the setter's case `0x10` as "the stand": read it again with these files.
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
  - **Note (fork watch 2026-10-18, `verify-ui-hud.md` U6):** a put-down that reaches `0x005253b8` leaves the rotation at
    0 (`0x0081d7a4`, by `0x0052f1b0( 0, 0 )`) unless exactly Ctrl is held, which keeps it (`park-engine.md`, the paragraph
    on carrying a purchase). Ours already keeps the item in the hand on exactly Ctrl (`ParkBuilding.cs:1319-1324`). Still
    unread: which placements take the branch at `0x00524db1`, and the queue tool's commit.
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
  - **Note (play test 2026-10-21, note 2):** no golden key animation at entry; the key puff is built
    (IslandPanel.cs:325-337`) and filed as Q630.`
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
  Until F8 and the chord are built, count each where it is let go (`CLAUDE.md` rule 4): nothing binds either, so the
  press reaches nothing and nothing says so (the 2026-09-26 staleness audit).
- [ ] **Q66. A disabled button takes the pointer.** Found by Q42's review. The original's hit test (`0x0065db25`) skips a
  control flagged `0x2` - a disabled button, `0x0065da8d` - with everything under it, so the pointer passes over it to
  whatever is behind: no hover, no help row, no glint. Here `UiButton.TakesMouse` is true whatever `Enabled` is, said at
  the site; the island panel's greyed arrows for an Instant Action player are the case in the lobby, and a press there
  still ends at the panel's outline, so nothing but the hover differs. Before changing the hit test, check every park
  window whose root takes no pointer: a press on a greyed button there would fall through to the world. Confirm: an
  Instant Action player's pointer on a grey arrow, no help row; a screenshot.

- [ ] **Q240. The postcard. Decode first.** From Q139. The gadget's postcard button is counted
  (`POSTCARD_BUTTON`), Ctrl+P under the full-screen view is counted (`FULL_SCREEN_VIEW_POSTCARD`), and Ctrl+P over
  the park (the shortcuts' row 15) and in first person (`FUN_00488a00`, action 15) is read by nothing: count those
  two first. All four reach `FUN_004a9380`: sound `0x95`, the game paused, the interface hidden, a screen chosen by
  `DAT_007cc150 = 3`, and a picture written out with `Postcard.wad`, `postcard.jpg` and an HTML template
  (`park-engine.md`). Decode the screen and what is written, to `docs/exe/`; the build is the next session.
  - **Note (fork watch 2026-10-16, `verify-ui-hud.md` M2 and M5):** the address check, inside the send handler
    (`0x004a7f36` to `0x004a7fa2`): the recipients are split on `;`, `,`, space and newline; each needs an `@` not at
    its start and a `.` after it more than one character on; any failure opens the box UITEXT `0xd6`, otherwise
    `0x004a8600` sends (online: skipped). The photo is 640 x 240 (`0x005d485f`), drawn into (`0x142`, `0x5e`, `0x27c`,
    `0xd7`). The alert queue `0x0047f620` (a deque, popped one at a time when no box is up) is fed only by the
    postcard and the online screens (33 call sites of `0x0047f410`): in an offline Lost Kingdom game only the postcard
    posts to it. Not by goals or challenges.
- [ ] **Q242. Go Online and Publish Park. Alexah's call.** From Q139. Both are counted (`GO_ONLINE`,
  `PUBLISH_PARK`) and close the menu; the original starts connecting (`0x005b5cc0`) to servers that are gone. Ask
  Alexah: leave them counted, say so in the game's own style (rule 11), or something else. Then build the answer.
  From Q496: the online visit's own interface (`FUN_0048a410`, stream `0x00758ae8`) and its help rows are decoded
  (`hud.md`, "The online visit's help rows"); they come with whatever Go Online becomes.
- [ ] **Q243. Every counted name against this queue.** Asked by Alexah, 2026-10-08: a thing "counted, not built"
  must have an item here, or it may never be come back to. List every `Unimplemented.Report` name in the source by
  script, find each in this file, and file an item (or add the name to the item it belongs to) for each one that has
  none; names dead by content say so in their item. No game run.
- [ ] **Q246. The ride view from first person, and its three other cameras. Decode first.** From Q140b. Walking
  into an open entrance starts the ride's view in the original (`FUN_0042a340`, then `FUN_004e15b0( 1 )`, the ride
  window's "Ride it!"; `park-engine.md`, "An entrance is shut to the viewer") and ends the walk; here each pass in the
  cell is counted (`FIRST_PERSON_WALK_INTO_RIDE`) and the viewer walks on, out of the Belly Bounce's entrance into its
  queue. `ParkEntranceGate.HasView` builds the fourth of `FUN_0042a440`'s views, the model's node, and counts the
  other three as not holding: `RIDE_VIEW_TRACK_RIDE_LEAD_CAR`, `RIDE_VIEW_TOUR_CAR`, `RIDE_VIEW_COASTER_NODE`, each
  dead by content in Lost Kingdom's stock park (no placed thing has a track-ride handle, a `TOUR` script or a
  coaster's track; which bought things would meet them was not looked at, but jungle's `coaster1`, `Coaster3`,
  `Tourride` and `wateride` models carry no view node, so their entrances are shut here until this is built). Decode the ride view's camera and what a
  coaster node's `+0x124` holds, to `docs/exe/`; the build is the next session.
  - **Note (fork review 2, 2026-10-16, `verify-tours-advisor-rse.md` L12, `verify-ui-hud.md` L13):** the ride view's
    smoothing in `FUN_0042b1c0` weighs the last view by (1000 - dt) × 0.0005, a dt over 200 ms taken as 100 first;
    leaving puts the look-at point on the centre of the ride's `mExitPos` cell (`+0x38`), the ride found through the entrance
    under the look-at point (`0x0042a340`), and the asked rotation `0x00790a38` from that cell's `mDirection & 0x55` (`0x00522850`):
    0x01 pi, 0x04 pi/2, 0x10 0, 0x40 3pi/2, any other value left as it is (`0x0042a1d0`). The tour car's view is Q408's.
  - **Note (fork watch 2026-10-21, verify-world-other L2):** the exit cell, not the entrance cell, is where the camera goes
    (`0x0042a22e` reads `+0x38`; x = `(id-1) & 0x7f`, z = `(id-1) >> 7`, each `10n + 5`, the height untouched). For ten of Lost
    Kingdom's eleven objects `mExitPos` is the entrance cell, so the old words held there; they differ for the Belly Bounce,
    exit (52,26), mType 10, `mDirection` 0x10. Every object with a ride-end `mExitPos` in the four park files read has a
    one-bit cardinal direction, so the yaw is set on every leave. The producer is ours already (`addresses.md`
    `0x005292b2`, `ParkBuilding`). Not proven: whether the look-at point is still on the entrance when the view is left (else
    `0x0042a340` answers nothing and the camera stays), nor `0x0042a560`'s answer that gates the leave.

## E. Large
- [ ] **Q31b. The other seven object windows.** After Q31's shop window: the sideshow's, the toilet's and the rest
  of `park-engine.md`'s nine, one window per session. Q31 carries the decoded figures and the `UIStrings` rows.
  From Q251: the staff window's days employed are `FUN_00505b70`, the calendar's now less `mTimeHired` over a
  day, handed to UITEXT `0x1bb` by `FUN_004b58b0` (`saves.md`, "`mTimeHired`"); `Staff.TimeHired` holds it.
  - **Note (fork review 2, 2026-10-16, `verify-ui-hud.md` L3):** the staff window is decoded whole: handler
    `FUN_004b5cb0` (fire, with UITEXT `0x18d`'s box when the options ask; pick up; the patrol toggle, tool `0x36`;
    locate; all staff), arrows walking all staff newest first and wrapping (hidden under two staff), stats refreshed
    every 4000 ms (`FUN_004b58b0`), advisor lines `0xc0`, `0xc1` and `0xd4` posted and withdrawn.
  From the run review (2026-10-17): both person windows are built (Q430, Q431), so five of the seven are left, and their
  leftovers belong here; Q420's notes from Q430 and Q431 list them: the visitor's preview (`VISITOR_WINDOW_PREVIEW`) and a
  guest gone (`VISITOR_WINDOW_GUEST_GONE`), the lists' lines `0x11e` and `0x11d`, the staff member's name edit, preview and
  member gone (`STAFF_WINDOW_NAME_EDIT`, `STAFF_WINDOW_PREVIEW`, `STAFF_WINDOW_MEMBER_GONE`), the staff room's row
  (`0x004b5267`) and the patrol switch's second help row (87). New: the base handler `FUN_0048d200` turns the cursor Up and
  Down keys (`0x2600`, `0x2800`) into `b_arup` and `b_ardown` (`hud.md`, "The person's windows"); decode the focus to see
  whether a key reaches the window, then build it or count it (`Unimplemented.Report( "OBJECT_WINDOW_CURSOR_KEYS" )`).
  Duplicates: `ParkLocator.OpenOnMember` copies `OpenOnVisitor`, where the original's is one function, `FUN_004b3f60`,
  branching on the type byte (`0x004b3f84`, table `0x004b4258`): make one `OpenOn( thing )`; and
  `ParkStaffWindow.DaysEmployed` is a third copy of the `( FileTimeOf( now ) - stamp ) / TicksPerDay` divide beside
  `ParkState.DaysSinceStaffHired` and `DaysSinceObjectBuilt`: one `DaysSince( stamp, now )`. Their tests that miss their
  bug are in Q211's run-review note.
  - **Note (fork watch 2026-10-16, `verify-ui-hud.md` H1):** the sideshow's and the ride's windows are decoded too
    (`hud.md`, "The shop, sideshow and ride windows"). Sideshow: price always, then chance of winning (`+0x190`, 0 to
    100) and prize (`+0x188`, 0 to 10000) only when `NonWinnableSideshow` (`+0x10c`) is 0, which no shipped `.sam`
    sets (that branch is dead by content); apply to all is `0xa0a9` with mask `0x100`, the same-item rule. Ride: the
    speed, duration and capacity controls and the apply `0x004af440`.
  - **Note (play test 2026-10-21, notes 12 and 17):** a click on a sideshow, and on a toilet, opens no window. Counted
    `SIDESHOW_WINDOW` and `FEATURE_WINDOW` (`source/OpenTPW/World/Level.cs:2732-2733`); `docs/PLAYER-GAPS.md:95`.
  - **Note (Q636's review, run 9):** the sideshow window's bars `0xa094`, `0xa096`, `0xa099`, `0xa09e`, the toilet window's
    `0x15bbb` and the resting-staff list `0x1e7b` are inset (0, 10) by `FUN_00485e30` in the original: build them so.
- [ ] **Q681. Light every model by its file's own normals, as the original does (from Q657). Measure first.** The
  original's lighter takes entry *i* of a mesh's `+0x64` table for vertex-order entry *i* (`Light_ModelVertexColours`
  `0x00574660`, the one routine for every model); OpenTPW lights the advisor by it (Q657) and every other model, the
  park's things, the land's meshes (`TerrainHalfLight`) and the lobby's islands, by its own averaged triangle normals
  (`ModelFile.CalculateNormals`), which on the advisor stood a median 33 degrees off on his hands and 90 on the bow tie.
  Measure first: a placed thing's live normals in the original (a walk like `advlight.py`'s, on its model) against its file's
  table (`lib/normdump`, `lib/normcmp.py`), and count over all 2,129 `.md2` whether every mesh's table covers its vertex
  order (Q657 counted the five advisors alone: FileFormats `models.md`, "Normals"). Then light them by it; confirm a
  park thing's shading beside the original's.
- [ ] **Q32. Graphics tiers.** `Level` reads the detail files (`low.sam`, `med.sam`, `high.sam`) for `GameOptions.PARTICLEDENSITY`
  and `NUMKIDS` (the kid and staff bank caps) and for three `GraphicalOptions.*` keys: `LIGHTING` (a counted no-op),
  `TEXTUREQUALITY` (the sea's and the flags' folder) and `PROCEDURALTEXTURING` (the ground's tile size). `SPRITESHADOW` is read too (Q480: medium's silhouette built, the own
  picture counted). Nothing reads the rest (texture filtering, sky, the sky's and models' shadows, fog, mipmaps, view distance); `SKYQUALITY` and `FIRSTPERSONVIEWDISTANCE` are named only in comments. The live detail file is loaded once, at boot
  (`FUN_0044dc60`, `lobby.md`'s `0x007858c8` row); `0x00423bc0` (`OptionsScreen`'s restart note) only reads a new
  quality's file into a local to compare it and flag a restart. Decode what it does with each key - the lobby plan's item 11 names three: the `stexture` set,
  `SKYQUALITY` and the particle low-detail byte - then build low / medium / high.
- [ ] **Q33. UI scale.** The UI has one fixed virtual size (`VirtualScreen`, 2048 by 1536). Add Auto / small / medium
  / large in the dead Video Card row (`OptionsScreen.cs:71-72`).
- [ ] **Q34b. Pictures for the README.** Animated pictures need a capture tool; none exists in the repo.
- [ ] **Q72. Values from data, not constants (the 2026-09-12 review's Phase B).** The lobby's four island names
  from `THEMENAMES.str` through the reader the gate already uses (`ParkFixedItems.ParkDisplayName`) in place of
  `LobbyIsland.DisplayNames`; the `ISLAND()` directory and model names in place of `themeName[0..3]` (`LobbyIsland`,
  `LobbyGate`); and `DUCKINGLEVEL` from `sound.sam` for the advisor's 0.38 duck - but that mapping to `0x00785914` is
  inferred, not proven (`Advisor`'s note), so decode the global's writer first. For the names and the `ISLAND()`
  fields the data equals the constant, so "nothing looks different" is the regression check (`docs/exe/lobby.md`,
  "Park names and the locale tables"). Confirm: a lobby screenshot before and after, identical.
  - **Note (fork watch 2026-10-18, `verify-other-a.md` lead 3):** `DUCKINGLEVEL` is proven: global `0x00785914`, the table's
    seventh key (`docs/exe/audio.md`, "The fade, the SoundInfo settings and the reverb"), used as a percentage, the
    group level times 38 / 100 (`0x0051bdf9`). The "inferred, not proven" above is settled; read it from `sound.sam`.
- [ ] **Q73. Cache pipelines and assets by key (the rest of the review's Phase D).** One pipeline per material
  (`Material`) where one per (shader, flags, output) would do, and texture and shader caches that scan `Asset.All`
  linearly (`Texture.Cache`, `Shader.Cache`). The shared blank texture landed (`bb897f8`); measure the load before
  and after, as that step did, and do not re-take its measurement.
  - **Note (fork review, 2026-09-30):** add `Renderer.cs`'s per-frame dirty-shader scan of `Asset.All` to the list; time it with the
    frame profiler before changing it. Review item ui-render-platform-2.
- [ ] **Q74. Lift the lobby out of `Level`, or record it as dropped (the review's Phase E). Alexah's call.** Park
  entry shipped without it: `Level` builds the lobby (`SetupEntities`, islands hard-coded, an unread `Global`) beside
  the park (`SetupParkEntities`). Ask Alexah before starting.
- [ ] **Q167. Read Alexah's Full Simulation saves.** Given 2026-09-28: a real player's saves, written by the
  original, in `~/repos/game/saves-fullsim/save/` (read-only; `CLAUDE.local.md` has the path). Jungle has
  `autosave.TPWS`, `New Save.TPWS` and `restart.INTS`; fantasy and hallow have theirs, space is empty. Made with
  `global.sam` edited (starting cash all 9s, one key per park); that changes the data folder, not the save's layout.
  The game crashed during the hallow park and would not load it again, so a hallow file may be damaged. Jungle first:
  load each in OpenTPW, count what reads and what does not, and put every layout fact in the FileFormats docs.
  Q165c read the jungle `autosave.TPWS` and `New Save.TPWS` through `ParkWorld` (`q165cprobe`, read-only): no problem
  reported, and the guests' two histories check out (FileFormats `saves.md`, 470).
  - **Note (Q174c's probe):** the `RSYS` channel module does not walk to its end in the jungle `New Save.TPWS` and
    `autosave.TPWS` (206 channels read, the cursor off the module's end), so `ParkThingStates` refuses it and none of
    their channels is restored; the fantasy and hallow saves and the shipped park walk closed (`q174cprobe/out.txt`).
  - **Note (Q181b):** a park built from the jungle `New Save.TPWS` throws in `ParkGuestSprites` (`People.ToDictionary` by
    `SpriteSlot`: two people share slot 0), after the scripts are bound; the game exits (`q181b/run1/run.log`).
  - **Note (fork review, 2026-09-30):** `restart.INTS` is the park as the theme was first entered (`saves.md`, "Restart
    Park: restart.INTS"), here the original's own fresh park. `FUN_00550b30` writes the new world's state once, on the first state-0xf frame, and Restart Park (`FUN_005ac5f0`) loads it back. Each of the three holds
    twelve things (the ten managers, then the unplaced gates and lights, catalogue objects x601 and x603), no people
    and no placed objects, `mGameTick` 0, the park closed, fee 20 and the shipped `Standard.sam`'s loans (APR 18-23);
    only the cash shows the edited data folder. It is Q197's reference. Review items gap3-2..gap3-11.
  Read by the week-end audit (2026-10-11): the jungle half looks done by Q165c, Q174c, Q181b and Q197. Left, and
  not said above: the RSYS module that does not walk closed in the jungle saves, the `ParkGuestSprites`
  duplicate-slot throw (not run again), and the fantasy and hallow saves, which are outside the Lost Kingdom
  scope.
  - **Note (Q606 review, VERIFY-13 / fix group F): the RSYS module that does not walk closed makes every save of that
    park take `SAVE_PARK_THINGS_AS_THE_FILE`, and with it every sold object is kept, so a guest made on any sold
    object's id is left out as well (`SAVE_PARK_ID_KEPT_FOR_A_SOLD_THING`; Q607's reach 2).)

- [ ] **Q405. Coasters run: their trains, motion and station.** From the fork review 2, 2026-10-16
  (`verify-coasters-run.md` L12-L17, L21). Lost Kingdom's `coaster1`, `Coaster3` and `Minecart`. The train count is
  clamp( trunc( lap / ( `uiTrainReleaseRate` × 0.001 ) ), 1, `uiMaxTrains` ) (`0x0043ca74`), the trains placed by
  release time; the motion tick `0x00435a30` runs once a frame after the 31 ms loop (a hop of half a sample, the (T +
  2O) / 3 collision exchange, f⁴ braking, floors from `fWinchSpeed` and `fMinSpeed`, decay 0.95 below speed 2);
  `coaster1`'s CrocCar is a bend car (`fBendElevationLow` 0.4, `High` 1.4 on samples flagged 8); the station step is
  Q289's note; a loaded coaster keeps its saved trains (flag `0x4000`, `0x00437cd5`) and riders (FileFormats
  `saves.md`, the coasters module; `ParkCoasters` reads only the first header); a car's seats are the capacity split
  over the centre-type cars. The PC's rating step is `ride-operation.md`'s, not the other fork's, which follows the
  Mac. Decode to `docs/exe/` first, then split. Confirm: Temple Of Gloom loaded from Alexah's park, its trains running
  and its riders let off, beside the original.
  - **Note (fork watch 2026-10-16, `verify-world.md` L2 and L3, `verify-audio-fx.md` O1):** `0x00430ed0` is the
    **track knot pass**, not a car update (`addresses.md` said car). Per track node (list `[0x00790fe4]+0x18`, next
    `+0x64`) it works the tangent's yaw and pitch into `+0xd4` and `+0xe0`, poses the node's pylon model
    (`0x00430ca0`), advances its model (`0x00473c70( 0x7a4610[node+0x70], 1, 0 )`, `0x004311f0`) and, for each knot
    record (`node+0x90`, stride `0x58`), takes the posed tag's position and writes the node's roll at `+0xe4`; the
    second rail is the knot plus (0, 1, 0) (non-loop), plus 2.0 x the tag's row (a modelled loop, node flag `0x40`),
    `0x004321c0` last. The sampler `0x00440c10` turns (second rail point less the first) by -yaw and -pitch through
    the sine table `0x007b0968` and takes atan2( -x, y ); `0x00440ad0` walks the knots. The bend `0x00438800` runs
    from `0x0043d730` only for a car type whose flag bit 0 is set (`coaster1`'s `bBendCar 1`), its dot clamped to [0,
    1] before the acos. Not read: the bend's tables, `0x00432df0`, `0x00435570`, the per-pylon tag counts.

  - **Note (fork watch 2026-10-17, `~/.cache/tpw-harnesses/fork-review-2/watch/2026-10-17/verify-save.md` L1):** the coasters module holds each track piece as one 0x22-byte
    record: flags, the mesh instance (to node `+0x70` through `0x00463ab0`), cell x and y (u16), an ordinal (u8), five
    unaligned floats and a link count (u8). The floats go into the node through `0x00444150` (loft, `+0xe8`),
    `0x004441d0` (yaw, `+0xe0`) and `0x00444270` (bank and incline, `+0xd8`/`+0xdc`, or a loop's rotation, `+0xf8`).
    Measured on Temple Of Gloom in Alexah's jungle saves: the pieces end at the module's +1,344, leaving the 506 bytes
    for trains, cars and riders. No loop piece or link in the corpus: those branches are read from code only. The field
    table belongs in FileFormats `saves.md`, "The coasters module".
  - **Note (fork watch 2026-10-18, `verify-audio-fx.md` F3):** the crash sound. `0x0043cd60` exchanges two bumping trains' speeds and, when
    the difference passes 1.0 (`[0x006fe020]`), calls `0x00439840`, which **only while `[0x00785a2c]` is nought** plays
    global rides 12 (`0x0051bfc0( 0, [0x00803a28], 0xc, x, y, z )`, `0x004398cd`) at a place taken from the train's `+0x20`
    by the ride's sample count (`+0xa8`). Build it with that gate, the one `audio.md`, "What an EventMap's slots feed",
    names for slot 0.
  - **Note (fork watch 2026-10-18, `verify-world-b.md` L6 and L7, `verify-world-a.md` M2 and M4):** the rebuild `0x0043eb70` runs
    **every frame** for every track with flag bit 0 (`0x00435a30`, from `0x00435a95`, list `0x00790fe0`, next `+0x18`; event
    `0x43` once, flag `0x2000`), so `0x00435a30` is the per-frame rebuild as well as the motion tick; the edited track and an
    index-picked one run it too (`0x00447d49`, `0x00447d83`). Its order: `0x00430ed0` (knots), `0x00432b50`, `0x00440c10`
    (sampler), `0x0043c450` (roll), `0x00439920`, `0x00439ab0`, `0x0043eac0`, `0x004402d0`. **The winch mark** (node `+8` bit
    `0x800`): `0x00432b50` clears it at every rebuild (unless `0x1000`), `0x0043c5f0` sets it when the clamped value equals its
    limit; not read: that the limit is the floor speed, and "creation parameter 0x20 gives `0x1800`" (no such immediate is
    or-ed into `+8`). **Chop rounding**: `0x00439ab0` (the crossing pass) sets the x87 to chop (`0x00439ab9`..`0x00439ac8`) and
    restores it at `0x00439c30`; every conversion in its callees truncates. **The roll per sample** `0x0043c450` (gated by
    car-type flag 8, run in the rebuild, not per car): the target angle `0x0046f230`, the delta wrapped by (d + 3 pi) mod 2 pi less
    pi, weight |vector| x car-type `+0x6c` held to 1.0, the roll wrapped and stored at sample `+0x18`, past `[track+0x134]`
    scaled by 1 - clamp( (i - start) / (`+0xa8` - start), 0, 1 ); the vector is turned by three sample angles. Not read: that
    `+0x6c` is `ForceMultiplier`.
  - **Note (fork watch 2026-10-20, `verify-world.md` M1):** a loaded coaster's crossing lists hold **chain positions**: after the
    stack pass (`0x0043827b`..`0x004382a3`) `0x0043e520( ctl+0x3c, n )` walks `n` links (`+0x64`) from the head (`ctl+0x54`) and the
    node's `+0xb4+4i` takes the node found. The saver writes each partner's `node+0` (`0x004375ad`..`0x004375b7`). The loader stores
    the counts unbounded (`0x00437d19`, `0x00437f72`); the runtime holds eight (`0x00439fda`, `0x0043a018`). Not traced: who numbers
    `node+0`, and whether deleting a node renumbers the rest. No park file on hand holds a crossing (the two with a coaster have
    counts 0), and the offer gate refuses a self-crossing coaster, so Lost Kingdom meets none. FileFormats `saves.md`, "The coasters
    module", has it.
- [ ] **Q406. Coaster building: the track tool.** From the fork review 2, 2026-10-16 (`verify-coasters-build.md`
  L1-L11). A piece costs `fCostPerHeight` × H × 0.1 + `fCostPer2DLength` × length × 0.1, plus `fCostPerUnit` only on
  placing a pylon (`0x00447830`, `0x00435020`; the cursor's preview `0x00435d50`); placing takes trunc( cost ), a
  height or move release gives back trunc( old ) and takes ceil( new ); finishing, removing and deleting the last move
  no money, and nothing tests the balance. The edit handlers (height 0.005 a unit, rotate 0.02 within
  ±`fMaxRotnAngleDeviance`, bank and incline 0.02, loop tilt 0.01 in [0.15, 0.85]), auto-bank ((±π - turn) × v / 4.0
  over the neighbours' distances; `fAutoBankDivisor` is read by nothing), the sampler (0.05 steps, spacing 3.0) and
  the closing pair, the end node, the place distance (3 to 8 cells, 3 to 9 for a loop pylon, `0x0042fd90`). The
  validator `0x00434740` never refuses a click. Open: what hides the loop button on `coaster1`; whether Backspace
  reaches delete-last `0x00444f30` (`park-engine.md` has Backspace calling `0x0052fe50( 0, 1 )`, its only caller).
  Decode to `docs/exe/` first, then split.
  - **Note (fork watch 2026-10-16, `verify-world.md` L1 and L9, `verify-audio-fx.md` O1):** the loop button's open
    question is answered: control `0x20` is enabled only when the mask's bit 2 holds and the thing's upgrade level
    (`+0x50`, `mUpgradeLevel`) is at least 1 (`0x004980c9`, refresh `0x0049880f`), so a fresh coaster has it off until
    its first upgrade (`coaster1` has `Upgrades[0..2]`); at level 1 or more the icon is `cb_loop`, or `cb_trough` when
    mask bit `0x200` (definition flag `0x100`) is set. `0x004df8f0` raises the level (`0x004df962`) only when thing
    `+0x19c` is 2: not decoded. The loop pair: `0x0079c664` = 2 at `0x00442cf4` (button) and `0x00444677` (key),
    zeroed at `0x004446dd`; `0x006fdeec` = 90000.0 compared at `0x00435e2a`. The closing click `0x00442880` is not
    read. The knot pass is Q405's note.
  - **Note (fork watch 2026-10-18, `verify-world-b.md` L5, `verify-world-a.md` M3):** Q406's H is the pylon height
    `0x004346a0` = ( `[p+0xc0]` - `[p+0xbc]` ) x t + `[p+0xbc]`, t the piece's `+0xe8`. At the cost (`0x00447830`) t is
    **unclamped**, p is `[[piece+0x100]+0x18]` and the result is x 0.1 (`0x006fe1f0`, `0x004478bf`); the cursor routine
    `0x00443f40` clamps t to [0, 1] and skips it when the control's flags & `0x4000`, storing the result at `+0x118`. The rebuild
    every frame, the chop rounding and the per-sample roll are Q405's note.

- [ ] **Q407. Buy Land.** From the fork review 2, 2026-10-16 (`verify-objects-signs-other.md` L1). Counted,
  `BUY_LAND_TOOL`; Lost Kingdom's Features tab offers it (`hud.md`, item 101), and 13,878 of the 16,384 cells carry
  the unowned flag `0x40`. The validator `0x00531a50` refuses a span over 2 cells, gate posts, cell types 7 and
  `0x1e`, a hoarding crossed and a pocket left (a flood fill: grid `0x00532960`, fill `0x005328e0`), and shows the
  can't-afford pointer `0x14` (`DAT_00816d5c`) unless a layout is replayed; the price is `Costs.MapCell` × the unowned
  cells inside; the purchase `0x005327e0` places the hoardings and clears the flag inside (`0x005311c0`); the debit is
  `0x004d01f0`. Confirm: a rectangle bought, beside the original. The purchase also re-marks the ground's half light
  (`Terrain_MarkLandOutsidePark` at `0x005328c3`; Q482): make `ParkGround`'s mask again, and confirm a bought cell's
  light doubling; it also sets the terrain meshes' normals over the bought cells back to 1.0 (Q619; `TerrainHalfLight`, Q652: the
  terrain's model made again with the new mask).

- [ ] **Q408. TOUR rides: Jurassic Tours.** From the fork review 2, 2026-10-16 (`verify-tours-advisor-rse.md` L1-L6).
  `tourride` (Id 1170), Lost Kingdom's golden-ticket ride, Full Simulation only (no `Easy_` `.sam`), runs 19 TOUR
  instructions, which fall to the counted default here. Decoded: the handlers (table `0x005569d0`; FileFormats
  `vm/instructions.md`), the flying car's tick `0x0055abf0` (nine states; take-off at 20000 and 14000; a lattice at
  max( 12000, ground + 5000 ); patrol corners at ±25000; 30000 near stations; bounds from the park, `FUN_0055ab50`), a
  rider's head ((car `+0xd8` + count) mod car `+0xdc`) + 1, TOUR 9's effects (worn particle 39, broken 40, a repair
  sound `0xbb` and clip 10), the draw `0x0055ce60`. Waits on Q706 and Q709.

- [ ] **Q409. Full Simulation's loans and challenges.** From the fork review 2, 2026-10-16
  (`verify-economy-guests-paths.md` L1, L5). A loan offer passes when its amount is at most the net worth less what
  the bought loans still owe, signed, kept in the offer and rebuilt on opening and each tab (`0x0049fa00`,
  `0x004a06b0`, `0x004d06b0`). A won challenge with a follow-up pays no prize, and the follow-up is marked won as it
  is offered, so one declined or lost never returns; declining a challenge that is not Independent pays half its
  prize; the pacing reads `DaysAfterCompletedChallenge` and `DaysAfterDeclinedChallenge`; nothing raises the declined
  count, so `DeclinesToForfeit` is never reached (`0x004d2b60`, `0x004d2fb0`, `0x004d1a00`, `0x004d3140`,
  `0x004d1ae0`, `0x004d2ec0`). Neither screen is built.
  - **Note (fork watch 2026-10-16, `verify-world.md` L13, `verify-ui-hud.md` M1):** a shop's sale feeds the challenges
    by its `ShopType` 1 to 6 (`0x004e18e4`: types 5, ingredient, 8, ingredient, 7, 6) and `SpecialIngredient` 1 to 4
    (`0x004e18fc`: types 2, 1, 3, 4), posted by `0x004d27a0`; `0x004ca160` is the mean over toilets of 100 less a
    float's low byte, only for `0x004d2860`. The loans window: row pitch `(h x 0x600) / [0x00faa5c0] + 6` from a font
    of 7; summary labels UITEXT `0x165`, `0x168`, `0x167`, `0x169`; take `0x0049f460` and repay `0x0049f4a0` refill
    only the list `0x0012c4bd`; the tab is remembered for the session (`[0x007cb260]`, control `0x0012c4bf` or
    `0x0012c4c0`).
  - **Note (fork watch 2026-10-18, `verify-advisor.md` A1):** the credit test `0x004d06b0` is read in `ride-operation.md`, "The
    loan helpers and the money rows": limit = net worth x 1.0 less the total still owed (instalments left x instalment over the
    taken offers), an offer passing when its amount is at most it, and the pass flag **written into the offer** on every call, so
    an advisor poll of row 102 rewrites the flags. A loan taken counts as cash in for the advisor's row 107. Rows 100 and 107
    are polled in Instant Action too (Lost Kingdom reaches them); 101 and 102 are Full Simulation's alone.
  From Q327: the original said row 107's `0x6b` (score 90) on tick 3633 of `q327/FSOPEN.TPWS` and `FSFRESH80.TPWS`,
  its fresh Full Simulation park saved on 3513 (no loan, $48,180, $1,400 spent); this game counts the row
  (`PARK_ADVISOR_POLLED_ROW_NOT_BUILT`).

- [ ] **Q448. Ride script: REPAIREFFECT and BUMP 17.** From the fork watch, 2026-10-16 (`verify-audio-fx.md` A1 and
  C1, `verify-world.md` L6). Both are counted by the dispatch's per-instruction report (`RideScript.cs`: "REPAIREFFECT
  at N", "Wateride: BUMP at N"; `BUMP_17_TRACK_PIECES` is the bumper rides' alone, and the water ride has no bumper
  record, `RideScript.cs:2337-2345`), both reached by Lost Kingdom's own rides. **REPAIREFFECT** (table entry 93,
  `0x005562a6`; FileFormats `vm/instructions.md`): the particle on for a non-zero value and off for nought
  (`0x00556de0`); **show** also plays global rides 111 and 211 at the model; **hide** plays the SINGLESCREAM picker
  `0x00551320` with (`VAR_ONRIDE`, 1, 1), so only column 0 (75, 79, 83, 87 by the number aboard, silent with none),
  and only when the script declares more than five variables. Our `park.md` has the polarity right; the other fork,
  which follows the Mac, has it backwards. Jungle Bouncy runs `REPAIREFFECT 1` and later `0`. **BUMP 17**
  (`0x005549ed`): the operand against `[script+0xe6]`; equal does nothing, otherwise it is stored and the circuit
  walks `0x0052a490` and `0x0052a700` run with f = 1 for an operand of 0 and 0 for any other; reset to -1 at load
  (`0x00559f29`); track edits run the same walks. Jungle's `Wateride.RSE` issues it four times (0, 1, 1, 0). Confirm:
  Bouncy's repair limb and the Wateride, beside the original. Decode `0x0052a490` and `0x0052a700` to `docs/exe/`
  first if they are not there. Building BUMP 17 for the Wateride also needs the water ride's track record, which the
  code does not have (the Q257r paragraph under Q420).
  - **Note (fork watch 2026-10-18, `verify-audio-fx.md` F5):** REPAIREFFECT's two sounds (global rides 111 and 211, `0x0051bfc0` at
    `0x0055631a` and `0x0055634c`) **drop both returned handles**: nothing stores them, so nothing can stop them with the
    owner. Do not tie them to the owner. Whether 111 and 211 loop (effect flag `0x8`) was not read; if they do, the
    original never stops them.

- [ ] **Q449. Building over scenery, and tearing down a water track.** From the fork watch, 2026-10-16
  (`verify-world.md` C2 and C3). (1) The placement check (`0x00535d66` to `0x00535d73`) passes a new thing over
  another's cells only when its `Info.OverwritePriority` is **strictly greater** than the owner's (`0x00818c10`,
  `0x00818c14`). Every category file declares 5; of the item files, features alone set it, 45 at 1 and 48 at 0
  (FileFormats `sam.md`, which lists Lost Kingdom's); the path and queue tools write 2 and 3 (`0x00527200`,
  `0x00527218`). Our code reads the key nowhere (`ItemDescriptionFile.cs` lists it as unread;
  `RideInfo.OverwritePriority` is unused): decide what a click on scenery with a ride or shop selected does here, then
  count it. (2) The demolisher's track arm (`0x0052801d`), for `WhichTrackType` 1 (karts) and 2 (the water ride):
  withdraws 2 x the cell cost x the scrap % / 100, signed, at `0x00528179`, after the per-tile refund of `0x0053b280`
  (cost x % / 100 unsigned; none for cell type `0x19`, a replayed layout or a station tile). `SALE_TRACK_TEARDOWN`
  (`ParkBuilding.cs`) counts it. Lost Kingdom's Wateride (`WhichTrackType` 2) and its go-karts (1) both reach this
  arm; its coasters (3) withdraw nought. Confirm: a Wateride sold with its track, and the go-karts, the bank beside
  the original's.

- [ ] **Q450. Park screens: the buy window's gauges, the map's colours, the hover latch.** From the fork watch,
  2026-10-16 (`verify-ui-hud.md` M3, M4, C3). (1) The buy screen's stats panel is unbuilt (`ParkBuyScreen.cs`); its
  gauges are in `hud.md`, "The shop, sideshow and ride windows" (`0x004abb00`: excitement, text from
  `Upgrades[0].RedLineCapacity`, a gauge from `Upgrades[0].WearRate`; label text not read). (2) The map overlay is
  unbuilt (`ParkMapScreen.cs`; its view, zoom and scroll are built, Q624, and the overlay's build is Q664); its colours are immediates in `0x005f0700` (`hud.md`, "The map's colours"); the
  channel order is inferred, and which record colours which code is not traced. (3) The frame routine brackets the
  draws with `0x0046c100` (on `0x007b05a8`, at `0x0054e310`) and `0x0046c110` (at `0x0054e34f`), which sets `+4`
  (`0x007b05ac`) to the latched thing when its kind byte `+2` is 1, 4, 5, 6, 7, 8 or `0x12`, sets `+8` to the first
  thing on the picked cell (`+0x24`, `0x007b05cc`, by `0x00527e80`) and, when nothing was latched, copies `+8` into
  `+0`; `0x0048c960` snapshots `+0`, `+4` and `+8` (into `0x007c264c`, `0x007c2650`, `0x007c2654`). `ParkPicking` keeps all
  three apart: the raw latch (`Latched`, no copy of `+8` into it when nothing was latched), `PersonUnderCursor` (`+4`)
  and `ObjectUnderCursor` (`+8`; Q474); compare Q444 before building. Confirm each
  beside the original.
  - **Note (fork watch 2026-10-19, `verify-other-b.md` M2):** the map's thing codes 1 to 8 are coloured, by default, by the
    thing's state of repair through the three-stop ramp (`hud.md`, "The park map": the stops, the clamp, the eight sites);
    for codes 1, 2, 3 and 8 the radio's settings 2 and 3 use `FUN_004e0860` and `FUN_004e1e30` instead. Part (2)'s "which record colours which code" is
    answered for those eight codes.
  - **Note (Q636's review, run 9):** the buy window's gauges `0x1ee` to `0x1f0` are inset (0, 10) by `FUN_00485e30` in the
    original: build them so.
- [ ] **Q714. The Park Status screen (`PARK_STATUS_SCREEN`), starting with the Top 3 Thoughts.** From the fork watch,
  2026-10-19 (`verify-other-b.md` H2; `hud.md`, "The Park Status screen's Top 3 Thoughts"). Counted when opened, never
  asked for by an item (the notes under Q420 list the rings it graphs). The screen shows people in the park, arrival rate,
  average happiness, average time, park rating, the Top 3 Thoughts, the happiness gauges (`hap.wct`, `sad_undec.wct`),
  total visitors and the 1/3/12-year graph (`hud.md`, the nine screens). Decode the rest first (labels, the graph's draw
  `0x004a47bf`, the series colours at `0x004a45e0`, the scale `0x004801d0`/`0x004802a0` read in `hud.md`), then build.
  The Top 3 is read: count the peeps' thought (`+0x30`) over those `0x004fa990` allows, the three largest (ties to the
  first, id 3 to `0x16`, none left is `0x17`), slot 0 the rightmost icon, the icon's frame the id less one, its help row
  of `THOUGHTS.str` the frame plus one, refreshed every 1000 ms. Unproven: whether an icon hidden for no thought is shown
  again. Confirm: the screen photographed beside the original's in the shipped park.
  - **Note (fork watch 2026-10-20, `verify-other-ui-audio.md` A-L1):** the graph's backdrop is decoded in `hud.md`, "The graph's
    backdrop": the plot box (the control's size less 20, from 4), the colours, ten grid rows, the columns' step of 1, 3 or 12 for 12,
    36 or 144 samples (any other count leaves the step unset, an original defect), the zero axis when ymin is below nought, and the
    `%d` labels. Not read: how the finance screen's 36 and 144 reach the count word, and the `0x7d00` added to each label's place.

## F. Then

Back to `docs/PLAYER-GAPS.md`: gap 4 (advisor in a park); gap 7 (saving a fresh park) is Q249, in section A. Also litter and the day ending, whose deferral reasons expired
(`docs/REVIEW-2026-09-21.md` section 6).
- [ ] **Q648. Halloween World: guests stick at the crosswalk.** Alexah's own play, 2026-10-22: the park otherwise ran.
  Out of scope until Lost Kingdom is whole (`CLAUDE.md`, the goal); kept so it is not lost. Reproduce in `hallow` with a
  `guests` census at the crossing, then find what the walk asks of the crosswalk cells there.

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
  sound"); here the isle's clips loop. Counted as `LOBBY_ISLE_RANDOM_CLIP`, once an isle (Q69); building it takes the counter
  out. The gate half landed (`3eed966`, `9b0ebab`, Q41); whether the gate also idles
  by itself is Q63. Confirm: `sound`/`state` over a minute of lobby, and a capture of the isle between clips.
- [ ] **Q77. The advisor's 90-second idle nag in the lobby (plan item 10).** Responses 394/395 (`0x18a`/`0x18b`,
  armed at `0x005e184c`, `docs/exe/ui.md`); sample 470 is never heard today. The arm and the queue's timer are read
  (`ui.md`, "The lobby's idle repeat"), and the arm is counted as `LOBBY_ADVISOR_IDLE_REPEAT` (Q69); when the first nag
  comes wants one sitting with the original first. Confirm: the line heard after 90 idle seconds, by capture.
- [ ] **Q78. The lobby's sea from `lobby/Terrain/base.md2` (plan item 9). Decode first.** The lobby has a sea: a
  `Water` entity (`Level`) that `Sky` seals the horizon against. The item is re-sourcing it from `base.md2` through the
  park terrain loader - but `Sky`'s own note says the original's lobby has no sea below its islands, which contradicts
  the premise. Settle whether the original draws `base.md2`'s sea before building anything.
- [ ] **Q79. Robustness (plan item 12, the rest).** Atomic save writes exist (`BaseFileSystem.WriteAllBytes`) and
  input is dropped while unfocused (Q42). Open: top-level error containment (nothing in `Program.Main` or `Game.Run` catches a failure of the load or the run loop),
  and easing the render rate and music while the window is unfocused.
- [ ] **Q80. Two tests the lobby audit asked for (plan item 13, the rest).** A save round trip against a real
  game-written file (it wants a sitting with the original to make one), and `LobbyScript` over the four shipped island
  scripts. Sound categories are covered by `SoundCategoryTests`.
- [ ] **Q715. The lobby's bolt inverts the screen for one frame (low priority: the lobby, not Lost Kingdom's park).** From
  the fork watch, 2026-10-19 (`verify-other-b.md` M1; `lobby.md`, "The lobby's bolt inverts the screen for one frame").
  The park's bolt has none, so only the lobby is affected. After the lobby bolt's call the original sets a flag, and the next
  frame draws a white full-screen quad through INVDESTCOLOR/ZERO blending (1 - destination) once. Not traced: the state
  word's path to the blend states, and no run of the original has shown the frame (the fork's capture saw none either), so
  film the original's lobby in a storm (`record.sh`) before building, and count the unbuilt flash `LOBBY_BOLT_FLASH` (rule 4) until it is. Pairs
  with Q152 (the bolt's lifetime).
  - **Note (fork watch 2026-10-25, verify-all.md M1):** the order is read: the flush `0x00582ad0` sorts descending (`0x005829e0`) unless the device word
    carries `0x20000000` with `0xc` (`0x00566c22` can set it; the reference install's park word, `0x1444100`, lacks it; the lobby's
    word is not measured). The bolts (state `0x200d52`) and the drops key 0, the invert quad 0.001 (state `0x9a1`), and none
    of these three states carries `0x3000`, so all are queued: the quad is drawn before the bolts and rain. Build: draw the quad
    after every queued entry whose key is above 0.001 and before the key-0 bolts and rain; what is drawn at submit (`0x3000`) comes
    before the flush and is inverted. Not proven: the keys of translucent meshes and the sky, and the bolt in play (the film above).
- [ ] **Q721. Each lobby island and gate turned by its ISLAND heading.** From the upstream look, 2026-10-19
  (`verify-sound-ui.md` L2). The island constructor `0x005dfa60` stores `island[+0x14]` = the ISLAND line's heading field
  x pi / 180, hands that radian value twice to `FUN_005d8380` (the isle's model instance `[island+4]`, then the gate's
  `[island+8]`), and calls `FUN_005d8190` on both with ISLANDCAMERAPOSITION; the camera homes onto `+0x14 + pi`
  (`FUN_005e1210`, `lobby.md`, "The island's heading"). Every `*_gate` mesh in `lobby.wad` sits on its model's -Z side, and
  a yaw of `heading` puts the gate where the homing parks the camera, which is why the heading is read as the island's and
  gate's yaw. Ours: `LobbyCameraMode.GateHeading` is one fixed pi, and `LobbyIsland` and `LobbyGate` never turn (only a
  position, `Level.cs`), which agrees with the original only where the heading is 0 (Space Zone). Predicted, unproven: the
  original's Lost Kingdom gate stands at our -X (ours -Y), Wonder Land's at +X and Halloween's at +Y. The comments say the
  deviation now; FileFormats `lobby-scripts.md` and `lobby.md` are corrected to what is known. **Settle first, in Ghidra or
  under Proton:** what `FUN_005d8380` writes into the model instance and how its draw matrix (built by `0x005d8870`, flags
  `0xc0`) turns the float into a rotation (axis, sign, row or column convention), and that the stack argument `[ESP+0x48]`
  at `0x005dfb90` is the ISLAND line's sixth field; or, cheaper, read the island camera's `+0x18` at state 2 after Enter on
  Lost Kingdom with `tpwmem.py` (pi / 2 + pi = 4.712 if the heading is used, as predicted; ours pi) and film one frame of
  the opened gate. Then turn the isles and gates, set `GateHeading` from the island, and fix the sun in
  `Level.SetupEntities`, which stands in front of gates that all face one way. Lost Kingdom's island first. Confirm: the
  lobby with the island and gate of Lost Kingdom beside the original's.
- [ ] **Q575. A plain voice's first silence, measured in the original.** From the run 6 review of Q563 (`handoff/review6/VERIFY-18.md`,
  Q563 2). Read from the listing and built (fix group P): every start of a plain or music voice, the first and a start
  in range again included, draws its variation's silence (`FUN_006bc410` -> slot `+0x48` `0x006bdf00` -> `0x006c5220`),
  where Q302/Q553 had the first followed at once (`audio.md`, "The plain class"). Predict before the run: the land's gulls
  (global ambient 2, one variation, wait 0 to 10000 ms) in the stock park, with the camera on the harbour, put their
  first sample's channel and their second's 0 to 10 s apart beyond the first's length, not at once; and the bus's voice
  back in range (waits nought) unchanged. Confirm in both games (`mixer.py`, `voices.py`; ours `scriptsounds` and the
  `Park audio: the land's own ambient 2 ... ms of silence after it` lines, which now print on the first too).
- [ ] **Q576. A chain's range before its first child, and a chain of any variation's, by the set-up's draw** (optional; reach
  only the staff's 188). From the run 6 review of Q563 and Q564 (`VERIFY-18.md`, Q563 4, Q564 1). The original's chain of any
  variation ranges for its whole life by the variation its set-up drew by weight among all (`0x006bbfc5` ->
  `FUN_006bc810`), its children writing nothing into it, and a held chain does before its first child; ours ranges by the
  newest child's or the effect's first. Said at `ParkAudio.InRange` and `ParkScreams.InRange`. In Lost Kingdom only the
  staff's 188 (a held chain, its fourth variation 85 of weight 1354 of 65532, the rest 90) can differ, and only in its
  start window away. Build: draw the set-up's variation for range alone (kept apart from the first child's variation 1).
- [ ] **Q578. Whether the land's following voices are heard through a pause in the original.** From the run 6 review (group N, Q558 1's decode, `audio.md`, "The pause substitution"): a voice
  with a rectangle (flag `0x1000`) is moved by `FUN_006bd5f0` to the listener's own height from `[manager + 0x60]`
  (`0x006bd5ff`), and the service's range test ignores height. If `[manager + 0x60]` holds the listener the pause lifts
  (`FUN_0051c1d0` -> `0x006b5ba0` -> `0x006b8b60`), the gulls and the sea stay at the listener's distance and are heard
  through a park's menu, where ours holds them (`Audio.HoldPlaced`) and lets their samples pass unheard. Read what
  `[manager + 0x60]` points to and who writes it, then measure the original's mix in a paused Lost Kingdom park
  (`mixer.py`: the sea's channel volume and distance with the menu open).
- [ ] **Q601. The gadget's date is written with the machine's culture; the original's reads "4.13.2002".** Seen in
  Q707's runs (`q707/p707t/side-by-side.png`): ours letters `GameCalendar.Now.ToShortDateString()` (`ParkGadget.ShowDate`),
  "4/18/2002" on this machine, and would letter another order on a machine of another culture; the original's frames read
  "4.4.2002" to "4.29.2002" (`q707/p707t/orig/O/g-*.png`). Decode the original's date writer for the gadget, then write
  the date its way whatever the machine.
- [ ] **Q694. Move to NeoVeldrid 1.3.0.** Alexah, 2026-10-25: released 2026-10-24; we are on 1.2.1 (`OpenTPW.Common`,
  `OpenTPW`, and `OpenTPW.ModKit`'s references). Its notes list Native AOT and trimming support (#104) and a backend-aware
  window creation in StartupUtilities (#92), with no breaking change and nothing in Vulkan, SDL2, SPIRV, texture formats,
  samplers or present (https://github.com/jhm-ciberman/neo-veldrid/releases/tag/v1.3.0; the compare diff not read).
  Read the compare diff `v1.2.1...v1.3.0` first; bump every NeoVeldrid package together; build, test, and photograph
  the lobby and a park before and after at one camera (pictures equal within a frame's own spread).
- [ ] **Q695. Queue pieces and the boundary hoarding draped vertex by vertex, like the track tiles.** From the fork watch 2026-10-25
  (verify-all.md N1; a lead the fork missed). The original loads queue pieces (`FUN_00522900`, flags `0x12423`, or `0x112423` when the
  row's byte `+5` is set: `0x00522954`) and the boundary-hoarding pieces (`FUN_00522e40`, `0x422`: `0x00522eb1`) with load bit `0x2`,
  and makes both with `FUN_00463060` flags `0x33a`, as it does the kart and water tiles. Every node then carries `0x20000`
  and is draped per vertex (`0x00469940`, `0x00469a80`; Q398's note), and the model's header carries the run-time flag `0x80`
  (`0x004625f4`). Ours stands them rigid: `ParkQueues.Place` uses the near corner's height (`ParkObjects.OriginFor`) and
  `ParkBoundaryHoarding.StandOf` the footprint height, so on a sloped cell ours is a flat plate where the original's follows the
  ground. Not proven: that a loaded park's pieces are draped too (they are remade from the save's records, `FUN_004647a0`, flags `0x33a`, from the same
  loaded models; not traced), and how many Lost Kingdom queue or boundary cells lie on sloped ground (not counted). First load
  a park, count those cells, and shoot one beside the original's; then drape the pieces as the track tiles are (with Q587).
- [ ] **Q696. The lobby's rain count follows the camera's distance to the rainy islands.** From the fork watch 2026-10-25 (verify-forks.md
  section 1; low priority: the lobby, not Lost Kingdom's park). In `FUN_005e0470` (`0x005e0e29` to `0x005e0f18`) the original works
  the drop count out every frame: over every island whose RAINY (`+0x60`) is not zero (RAINY is a flag; its value is never
  read), it takes the mean m of `dx^2 + dz^2` from the camera (`[0x00f83778] + 8`, `+0x10`) to the island (`+0x3c`, `+0x44`),
  then `v = 1e17 / m^4` (`[0x00702c88]`), 0 under 20, at most 400, and hands the count to `FUN_0057d910`. Rain starts about 91.7 units
  away and is full inside about 63.1 (arithmetic); at the orbit's SPINRADIUS 70 it is about 173 drops (arithmetic, not
  measured). Only hallow is RAINY, so it is the distance to Halloween's island whichever island is current; lightning
  still follows the current island (`0x005e0f28`). Ours is a flat 900 drops (`Rain.DropsPerLevel`) whenever Halloween is current,
  said at `LobbyWeather`. Build the original's count; confirm in the running lobby (the live drop count at one camera against
  the number predicted from the camera's distance), and in the original if it can be read. Pairs with Q715 and Q152.
- [ ] **Q697. A language selector.** Alexah, 2026-10-25, from PawelWielga/OpenTPW's `polish-support`, which cannot be merged: its
  tree deletes 806 of our files. Our code hard-codes `Language/English` in about a dozen places (`grep -rn 'Language/English'
  source`: `Localization`, `BitmapFont`, `BFSTReader.EnglishCharacterTable`, the park windows' tables, `ParkObjectNames`,
  `ParkTags`, `ParkStaffPool`, `ParkFixedItems`). First decode how the original picks its Language folder (the
  registry or a file; `docs/exe/`), then build the choice on that, with one place that names the folder and the character table
  (`MBToUni.dat`, FileFormats `strings.md`, where a Polish header's bound is unproven). English stays the default; a language folder
  not on the disc is refused with a message. Confirm by a launch with another shipped folder (`american`) lettering the lobby.
- [ ] **Q698. A tag younger than half a second taken off by the top's right click, in the original.** From run 10's review
  (VERIFY-1, Q638's UNSETTLED lead). Settle what the original does when the top's right click (DeleteAll) reaches a
  tag put on less than half a second ago. DeleteAll posts message 5 to each tag at 100 ms steps
  (0x004a13c6..0x004a13e4), while such a tag still has its delayed message 6 (UI_SetVisible( 1 ), posted 500 ms on at
  0x004a1292) pending. If message 5 (0x0048dc0a) detaches the control from the mast and the delayed message 6 is still
  delivered, then FUN_0065d773 on a parentless tag is true and the original would start MessageTag1 (0x5a) there, over
  a tag that is no longer on the mast; ours shows nothing (`ParkTags.ShownAt` needs the tag standing). Settle it with
  Ghidra on the message 5 path (0x0048dc0a) and the delayed-post queue (0x00669c8d), or with a run of the original
  (right-click the top within 500 ms of a tag going on, film the effect list with record.sh); then match it or say it
  at `ParkGadget.TagEffects`. Low priority.
- [ ] **Q699. The park's rain ignores the wind.** From run 10's review (Q643's first reader). The original turns its rain
  with the wind: `mWindDir` is handed to the drop-direction setter `0x0057d920( dir, force )`, called from
  `FUN_00512f00` at `0x005131dd` at each change of weather, and a drop is the quad from `p1` to `p1 + windDirection *
  0.1` and drifts along it (`weather.md`, "The rain"). `source/OpenTPW/World/Weather/Rain.cs:84` drifts every drop by
  a constant `(5.5, 2.5, 0)`. Build the original's: read the setter's body (`0x0057d920`) first, then turn the drops
  by it at each change of weather. Read the **raw** `ParkWeather.WindDirection`, not `ParkFlags`' eased angle: the
  original's rain turns at once, and Alexah's easing (Q643) was asked for the flags only. Confirm: the rain's slant in
  both games across a change of wind (poke the original's `mWindDir` with `thingpoke.py`; Q643's `flagangle.py` reads
  the flags).
- [ ] **Q689. Whether the original draws particles between ticks.** From run 10's review (VERIFY-3, Q642 5).
  `ParticleSystem.cs:17` says "Nothing moves between ticks: the original draws the last tick". Q642's decode did not
  test it: `FUN_0055ce60` (docs/exe/park.md, "What a drawn frame shows between ticks") paces the flying cars' tour
  records, not particles, and the original's particle draw was not read. Read how the original draws a world particle
  between ticks (the sprite pass, `original/spritepass.py`, `particleheights.py`) and whether it eases by the frame's
  own time; if it does, build it (an improvement or the original's own, said at the site), if not, keep the comment.
  Confirm: a particle film (`rectfilm.py`) in both games on an effect whose rate is known.
