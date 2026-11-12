# Work queue

Begun 2026-09-22 against `main` at `9b0ebab`; items from Q35 on were filed against later tips. File and doc line
numbers in Q13-Q34 are from `9b0ebab` and have drifted (`park-engine.md` by up to ~1,100 lines): find the member or
the heading, never the line.

**How to use this file.** One item per session. The maintainer's sessions take the first unticked item and do only
that one, and tick it in the commit that lands it, with the number that proves it; the maintainer's pre-commit hook
then moves it to the end of its section in `history/queue-done.md`, which holds every finished item. A contributor
takes an item that has no `Claimed:` line (the ones tagged "good first" are the gentlest), claims it as `WORKFLOW.md`,
"Claims" says, and leaves the tick to the maintainer, who ticks it when the work is merged. An agent takes an item
only when its person names it (`AGENTS.md`, "How a task runs"). A page citing a Q-number that is not here means that finished
entry. If an item turns out to be two, split it into two lines here and stop after the first. The maintainer may
reorder the queue; nobody else does. A line **Needs Alexah (date):** at the end of an item is a question an unattended
run left (`WORKFLOW.md`, "Unattended runs"): the item waits for the answer, and the session that gets it takes the
line out.

**Every contributor, on every item, no exceptions.**

- Start from the repo root. Read `AGENTS.md` (or `CLAUDE.md`, which loads it), `docs/STATUS.md`, then this file's head
  and your item: the first hundred lines or so. The rest of the queue is other sessions'; grep it for a
  Q-number when a note is to be filed under one.
- Branch from `main`, one short branch per item, named `<you>/<what-it-changes>`.
- "Confirm" means the running game: a screenshot **and** the census or log line that goes with it, both. Predict the
  number before reading it. A test is added for regression only. It is never the proof.
- Put the bug back and re-run the new test. If it stays green, the test is hollow. Fix the test.
- Build and test the commit alone in a throwaway worktree.
- Facts found go in `docs/exe/`, and bytes in a shipped file in the FileFormats clone (`AGENTS.md` rule 14). The
  commit message says why. The code comment says what the code does now. No "used to say", no dates, no `>>>`
  banners in code.
- The maintainer's sessions update `docs/STATUS.md` in the same commit (it stays under 120 lines). A contributor's pull
  request leaves STATUS and the tick to the maintainer, and says in its description what the item made work.
- Commit locally and stop. Do not start the next item.
- End with four lines: what is ready, what was confirmed on screen, what was not, what the next item is.

**The maintainer's own steps (Alexah's; a contributor outside the project skips them).**

- They tick the item and update `docs/STATUS.md` when a contributor's work is merged.
- Their branches are `alexah/N-<what-it-changes>`, N the next number after the highest local `alexah/*` branch.
- They reorder this file, and say so in a block headed "Ordered at Alexah's word".
- When an item is confirmed they fast-forward merge its branch into `main`. Pushing `main`, and opening or merging
  any pull request, still needs their yes (`AGENTS.md` rule 1): the session says what is ready and where it would go,
  and waits.

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

**Ordered at Alexah's word (2026-10-28), before any weekly build goes out:** Q798 (ready for public builds) first, then
Q249 (saving a park made fresh; its build is Q783), then Q782 (a player's data folder) and Q800 (contributors by
default, Alexah's second pre-weekly list), then the rest in the order below.

**Ordered at Alexah's word (2026-10-29):** Q804, Q805 and Q806, found while photographing Alexah's Full Simulation
park for the website, go first. Q804 stopped the photographs: no picture with the blue lines will be used.

**Ordered at Alexah's word (2026-10-30):** Q954 (the memory ours holds) first, before the weekly builds go out: that much memory would shut out a player on a lower-end computer. Then the rest in the order below.

**Ordered at Alexah's word (2026-10-30, later):** the memory work is done (Q954 and its pieces Q827-Q832, Q834, Q836);
its leftovers Q826, Q835, Q833 and Q959 move to the end of this section, and Q820 is first again, then the rest in the
order below. Memory and frame time are checked before each weekly build instead (`WORKFLOW.md`, the Sunday routine, step
0).

**Ordered at Alexah's word (2026-10-31, later), before the first weekly build:** the lobby first, as a new player sees
it: Q950 (the attract flight's sound and camera, back to the original's), Q770 (the extra bed) and Q749 (the eye after a
quit); then the block below.

**Ordered at Alexah's word (2026-10-31), before the first weekly build:** Q849 (saving a played park, the round trip
never run), Q216 (settings and profile saves: back up, then overwrite), Q957 (shipped builds watch the shader files),
Q247 (a loaded park built again behind the loading screen; its scrollbar, Q918, is done) first; then Q820 and the rest in the order below.

**Ordered at Alexah's word (2026-10-31, Windows), first of all, before the first weekly build:** what a Windows player
meets first, found on a Windows 11 PC by a Remote Control session, jobs w1 to w4 (`~/.cache/tpw-harnesses/handoff/run-2026-10-31-win/log.md`; its files are on that PC under
`C:\Users\alexa\tpw-work`): Q976, Q978, Q981, Q979, Q980, Q982, Q977, in that order; then the blocks below. A
confirm on Windows is done on that PC, through the "OpenTPW Windows" session.

- [ ] **Q943. The update check and its click on Windows and macOS, and against a real newer release (Q941's leftovers).** Counted, not
  seen: on Windows and macOS the opener (`SDL_OpenURL`), the `updatecheck off` line and the label `local - <commit>` were not run; and the
  pick has met only replies made up in tests and the real GitHub's empty list, because no weekly release exists yet. Once the first weekly
  build is out and a second follows, run the first with the network on and see the line appear (and its click open the page), on Linux and
  on the Windows PC (`docs/TOOLING.md`, "The update check, and a click on its line"). Also look at the line's gold (the lobby's `(236, 162, 4)`, which replaced the teal that read
  faintly on the lobby's teal sea in `q941/runs/fake-final/lobby-with.png`) in the lobby and both Escape menus: photograph it once, which the first look has not.

- [ ] **Q902. Where the original's saves went on a PC without the permission fix.** Alexah's call, from Q982: whether the
  original's saves went to Windows' VirtualStore (`%LOCALAPPDATA%\VirtualStore\Program Files (x86)\SimTheme
  Park\save`) when `Program Files (x86)` was not writable, and whether OpenTPW should read them there. Not tested: the
  PC tried has a third-party `PermissionFix.exe` (Everyone full control), so the original wrote straight to `save\`.
  **Alexah (2026-11-02):** read nothing from the VirtualStore and keep the notice; look at this again only if a player meets it. Q984 left it open: a `save\`
  that holds players but cannot be written is read by nothing, and nothing is copied out of it to the player's folder
  (whether to copy or read them is this item's); the game says so once on screen (`YOUR GAME SAVES IN A NEW FOLDER`, remembered as `told moved`, Q922).
- [ ] **Q903. A save lost as the window closes is told in the log only.** The window's own close with a park up writes
  the autosave and the player, then the process ends: no scene is left to open a box (Quit Game's question says
  beforehand, Q982). Counted, not built: a close that asked first would be a deviation the original does not have;
  the log's `Leaving the park: autosave is not written` is all there is. Look at it only if a player meets it. The same for a
  backup made as the window closes (Q998): the first write of a file this game did not read whole, if it is the closing one, is
  told in the log (`was not read whole by this game, so it is copied to ...`) and in no box.
- [ ] **Q904. Two cards of one maker cannot be told apart on Windows.** The loader's driver filter works per manifest file and
  `MESA_VK_DEVICE_SELECT` is a Mesa layer's, so an AMD built-in GPU beside a Radeon, or an Intel one beside an Arc, share one
  manifest and `GpuChoice.Levers` cannot put the second first: the log warns and the first device is drawn on (README and
  `GpuChoice.Levers` say so). A way to name one of them (a bus or LUID id) needs NeoVeldrid to take a device, which it does not
  (`VkGraphicsDevice.CreatePhysicalDevice`: "Just use the first one"). Look at it only if a player has such a PC.
- [ ] **Q905. What Q978's box could not settle (leftovers).** (1) A box with no parent window: with `opentpw.cfg` on full screen it
  may open behind a topmost window on Windows, leaving a frozen loading screen and a process waiting on an unseen box (hide the
  window first, or add it to Q850's confirm). (2) A non-ASCII path in the box on Windows (`C:\Users\Zoë`): which encoding Silk
  marshals for `ShowSimpleMessageBox` is not read. (3) After a load that fails once `Audio.Init` has run, nothing stops the sound
  behind the box (`Audio.Shutdown` is not on the catch's path). (4) `ConsoleLog.Path` empty after a failed `Open`: the catch opens
  `OpenWithoutGame` a second time on the same folder, which fails again and prints a second `[console log]` line. None seen.

- [ ] **Q906. Two failing save-folder writes are not told (Q982's review, F6).** `SaveFolder.DeletePlayer` catches a failure and logs
  it, then `Players.Delete` empties the slot, so the player looks gone and is back at the next launch (it is also reached from
  `Players.Create` over a used slot); `PLAYER NOT SAVED` is the wrong words for a delete, so it wants its own. `ScanPlayers` makes the
  theme folders of each player inside its loop: a player folder lacking one throws in a folder that cannot be written and the
  outer catch ends the scan, so the players after it are missing for the run (rare: a folder made whole has every theme; it runs
  before any scene, so `SaveTrouble` could tell it at the lobby's first frame). Counted, not built; look at it when a player meets it.

- [ ] **Q247. A loaded park is built again behind the loading screen.** Left by
  Q241c (`saves.md`, "OpenTPW's Load Park"). The original reads a park file over the running park, with no loading
  screen and no change of scene (`FUN_00414d40`: twelve teardown calls, the modules, `FUN_00415140`), where
  `ParkLoadScreen.Load` asks `Game.RequestParkLoad` for the whole scene again; Restart Park is the same
  (`ParkFrontEnd.RestartPark`). The teardown is decoded (Q854; `saves.md`, "The in-place load, call by call", whose last
  paragraph sets out the choice). **Alexah (2026-11-02):** time a load in both games first and bring the two numbers; whether the park is rebuilt in place is decided on them. Confirm: a load timed in both games the same
  way (the original's "1.3 s" is a poll bound, not a timing).
  **Split (Q918, run 18, lane A):** the list's scrollbar and the saved camera on a load, which did not wait on that choice, are done and
  ticked as Q918; the other lists' scrollbars are Q919.
  **Split (Q921, run 18, lane A):** the timing is done and ticked as Q921, same way in both games (`saves.md`, "Load Park timed in both games"): a Load Park from inside a park
  takes **1.9 to 2.3 s** in ours (off-screen and on the desk's GPU; a loading screen from 0.05 to 0.09 s after the release; Restart Park 1.8 to 2.0 s) and **0.14 to 0.17 s** in the original (no loading
  screen; Restart Park 0.10 to 0.14 s), on the shipped stock park's file. Ours spends 1.2 s of its 2.0 s on `audio`. No build was made. **Alexah (2026-11-02):** C: keep the rebuild and cut the time first (the audio read once a theme, or no loading bar); in place can still follow. (`saves.md`, "What Alexah needs to decide".) **What this route measured (Q921):** after the first load of a launch `audio` is 1.32 to 1.43 s of the 1.75 to 1.85 s the game logs and `ground` 0.22 to 0.26 s (the first load's `ground` is 0.50 s; Q832's 2.1 s of 3.1 s was a lobby re-entry in an older build). With `audio` gone about 0.7 to 0.9 s remains, still behind a loading screen, about five times the original's 0.14 to 0.17 s. "No loading bar" saves a picture, not much time: the bar is capped at 30 frames a second (`LoadingScreen.cs:57`) and the load's total is the same on lavapipe and the GPU (1.83 and 1.81 s), so its saving is unmeasured. "The audio read once a theme" must keep the decoded clips across `Level.Unload` (`SoundBank.Load` reads the `.sdt` and decodes clips below the 16 MB limit), not only save the read; untested.
  **Split (Q924, run 18, lane A):** C's first half is done and ticked as Q924: the banks a park decodes whole are kept across a Load Park and a Restart Park (`SoundBank.Kept`; the lobby's start lets them go), so a load from inside a park is 0.80 to 1.01 s from the
  release on the desk (before 1.88 to 2.16 s) and a Restart Park 0.72 to 0.83 s (before 1.89 to 1.95 s), the same picture, the game's memory the same (`saves.md`, "Load Park after Q924"). What C leaves is Q925 (the rest of the rebuild's time); what B would change, against a rebuild now about five times the original's 0.14 to 0.17 s, is for Alexah to decide on those numbers, so Q247 stays open.

- [ ] **Q913. What Q910 and Q911 left unseen.** What `Worn.RSE`'s and `EventMap.RSE`'s effects look and sound like after a load in both games (the six companions are started and run; only the TV Sim's torches were seen and compared, and the engine's voice and the original's mixer were measured, not heard); and a load and a save on Windows. (A bought kart's and water ride's companion saved, Loaded and run on, in ours and the original's list, is Q939: done.) A bought Hot Pot's engine after a load, in both games, is Q929 (done); the Hot Pot sold after a load and saved is Q926 (done); the Volcano closed after a load and saved is Q927 (done); effect 31's start at height -4 against the original's is Q928 (done: a start keeps the node's fraction); the bought TV Sim's ride window closing and opening it once its queue is joined to a path, in both games on the same file, is Q1104 (done: the original drew that TV Sim at `setcam.py 430,250 75 180`, `q1104/orig/b-1-cam.png`, its screen face black in the off-screen frame where ours shows a picture, not looked at on the desk). Still open from
  Q1104: the original's TV Sim screen face, black in every off-screen frame (`q1104/orig/b-1-cam`, `b-4-*`, `b-7-*`) where
  ours shows the tunnel picture, looked at on the desk beside ours'.
- [ ] **Q820. What Q818, Q821 and Q822 left: the stacked and loop pylons, the original's picture of a flipped diagonal, and the heights by the original's samplers.** From Q818 (run 14, lane A;
  its piece (3), the face normals and the control word, is Q819; its piece (1), the ground's per-cell diagonal, is Q821, which ticks Q398; the decode
  of piece (4) and the float note are Q822). (2) Left by Q817: the pylon under a stacked piece is drawn with the alone-on-its-cell arms (the bottom of a stack that is not its top shows the `0x400` node and hides the `0x800` one, `0x00432ff8`..`0x0043304d`; ours hides `mc_bridge` on it); a stacked pylon stands on the
  stack-height record (`StackHeightDummy` on `StdPylon`), the type flag `0x10` keeps its floor, a loop pylon is another model:
  none is in her park, so each needs a park that holds one (a coaster built in the original, then loaded in both games). Run 19 (Q948) swept the 60 park files on
  this machine (`parkfile.py -K`): every coaster is the Temple of Gloom's 38 pieces, stack 0, loop 0, so none holds one; a coaster with a piece over another is built by the
  original's track tool, driven by hand (Q406 is ours' own).
  (3) Done by Q837 and Q914 (the original's picture of a flipped diagonal: Q837, with its camera and clock held, `0x0804` -> `0x0802` on four bent cells changes 7,327 pixels in four patches and putting it back returns the frame in 0; Q914, the picture follows `0x2` alone (`0x0806` changes the same 7,327, `0x0800` changes 0), and a flat cell's two off-diagonal corners raised 15 units draw a ridge with `0x2` clear and a valley with it set, the other pair the other way round: `0x2` set draws (x,z)-(x+1,z+1), `park-engine.md`). Nothing is left of it.
  (4) Built by Q823 (`alexah/766-heights-by-the-originals-samplers`): `0x800` at the load, the sprites, balloons and the build markers' glide on
  `ScapeHeight`, the camcorder's eye on `CameraHeight` + 5 each frame. The pick's ground as the original's two planes a cell is Q824. The person's particles'
  sampler, the sprites carrying `0x1000000` and the eye's further term are decoded and measured in Q825 (`park-engine.md`, "The three words Q822 left unread": the
  particle's offset is nought, the bit is the head sprites' alone and ours already stands them absolutely, the eye's term is nought in first person; the
  puffs themselves are Q900). The markers' corners at a flagged cell are read in the original's own mesh and equal ours (Q838), and a person's drawn height at a flagged cell is read in the original
  and equals ours (Q839: 279 points of 31 flagged cells (20 ground, 11 holes) on the stock park's two ridges and the cells by them, a guest held at each). A balloon at a flagged cell and ours' own person drawn on one in a frame beside the original's are done as Q948 (the balloon takes the walker's sampler; ours' `pin` puts a guest there). The feet's pixel row against the projected ground point and a balloon's drawn height above its holder in both games' frames at one camera: **measured as Q1106** (the feet stand 6.0 and 6.1 of the original's pixels below the projected point, ours' 6.1 at a frozen eye, about 5.6 settled; at a settled camera on Q1107's binary 386.1 of 768 against 387; the balloon's picture stands where the original's does about its anchor, within a row, measured at a settled camera as Q1108; ours' camera aimed at height 0 over raised ground where the original lifts its aim with its eye: done as Q1107). Nothing else is left of (4).
  - **Note (fork watch 2026-10-31, verify-world M8, verify-other M1, M4):** the shared wrap bits: `0x00575ea1` and `0x00575eb3` set `0x10000` and `0x20000` from the slot's `0x10` and `0x20`; the reuse in `0x00575c60` ORs both when the slot has either; `0x00590fa0` clears `0x4000` only; the object dies
    when its last holder goes (`0x00575f00`, `0x00575ad0`), so the bits accumulate only while one model holds it; the reader
    `0x00590fb0` tests both together and answers only when `[0x008bd524] == 1`. Whether this explains Q968's seams is not
    shown. The loop bit (`0x0042fd57`) is read under the definition's `[ebx+0x10]`. The pylon's ground node is already in
    `park-engine.md` (bottom node `0x00432f2f` to `0x00469940`); "four ground vertices on every PC pylon" is unproven.
- [ ] **Q1111. The ride window greys the move button on a kart or water ride. Decode first (from Q1105).** The original draws Dino Karts' `b_move` greyed (`q1105/orig/c-2-kartwindow.png`), ours draws it live (`q1105/run1/window-karts.png`, the same file). `FUN_004ad4e0` enables it only when `FUN_0052c2b0( thing )` answers non-zero (`0x004ad6df`..`0x004ad6fd`): it starts at 1 and, for `WhichTrackType` 1 and 2 alone, tests a track cell beside the station chosen by the angle `+0x10` (`FUN_00522790`, `0x0052c2c1`..`0x0052c35f`); the rest is unread. Decode it, then disable the button the same way. Confirm: a Dino Karts' and a Splish Splash's window in both games, and a click on the greyed button doing nothing.
- [ ] **Q1112. The ride window's other status settings: the condemned hide, the door at the track's statuses, the call-mechanic button held down (from Q1105).** `FUN_004ad4e0` (`park-engine.md`, "The ride window's verbs, shown and hidden") hides seven verbs at status 3 (condemned, `0x004ad509`..`0x004ad5a8`), disables the door at `0x19` and `0x1a` (`0x004ad5bc`..`0x004ad5c4`) and sets `b_callmech` down at `0x11`..`0x15` (`0x004ad684`..`0x004ad6ac`). Ours' `ParkClosedStatus` answers none of those statuses (counted `OBJECT_WINDOW_HIGHER_PRIORITY_STATUS`), so its window shows all eight and never holds the call button down. Build them on the statuses `FUN_00485f60` answers. Confirm: a condemned ride's window and a ride with a mechanic called, in both games.
- [ ] **Q885. What Q949, Q886, Q887 and Q888 left: the stampers ours does not make, the other themes, a path over a stamped queue cell (from Q949, Q886, Q887, Q888).** Ours' stamp is confirmed at the load's live map (437 cells, the original's set; `q949-a3/final3/`) and in play, in both games, for a Litter Bin and a Staff Room turned 0 and 270 (Q886, `q885/`); the load's rule is read item by item and its tests are written (Q887, `q887/`); the original's own purchase of the Huge Hollow Rock is read (Q888, `q888/`). Not done: (1) The track kit's and hoardings' pieces (`0x33a`) and a coaster's pylons (`0x3fa`) were thought to stamp in the original while ours stamps none: **settled by Q891** for the kits and hoardings (the original stamps none either, in play and at load; ours' same stamps), the coaster's pylons in play are Q840 (done: they stamp nothing; what it left is Q1001, done, and Q1013). (2) A track add-on's put-down (`ParkTrackAddOn.Stamp`) runs no tile pass in ours: **done as Q843** (the original's Tunnel put-down retiles all ten typed cells in the placer's commit pass, op `0x81` at `0x0052912c`: type 4 gives tile (0,8), which ORs `0x101` and keeps the texture, `0x0046e012`. So the eight track cells gain `0x1` and keep their textures, and nothing is given back. The stamp retextures only the two body cells (11,16) and (11,17), which ours stamps the same, and ours' type 4 leaves the ground undrawn as the `0x1` does; `park-engine.md`, "The Large Rock Pillar's purchase and the Dino Karts Tunnel's put-down, measured"). A turned Huge Hollow Rock was measured at 90, 180 and 270 in Q842 (`park-engine.md`, "A turned rock anchors, types and stamps"; Q888 ran turn 0: it stamped 14 and typed the model's 23 cells; ours does the same at turn 0 since Q744). (3) Another theme, and a ground with `PROCEDURALTEXTURING 0`: ours stamps nothing there (`ParkGround.StampSplit`, tests), and the original's stamp needs the compositor (`0x00463354`, `0x4000`) so it stamps nothing either and a toggle in play neither stamps nor gives back (`FUN_0046ded0`, Q884's second reading, from the disassembly); neither is run in either game. (4) What clears `0x100` after a purchase and a sale in the original (the restore arm ORs it, `0x0046e06f`, yet every restored cell reads without it; the cleared set is not one box: 258 cells in Q886's first run, 406 in its second, x 35..76, y 13..37), and the cell beside the footprint that gains a bit ((64,17) went `0x0104` to `0x0084`; (67,19) for the turned Staff Room): the Sentinel's first reading of Q886 (not second-read) takes it for the entrance path the placer lays (the path arm ORs `0x180`, `0x0046e0da`; `park-engine.md`, `0x005298d5`; ours lays one too), so compare each purchase's entrance-path cell in both games. The `0x0100` row of `park-engine.md` may answer "what clears `0x100`": the compositor clears it only on a cell without `0x1` (`0x0056f184`, `0x0056f1f9`), and Q840 saw a camera move alone clear it on 233 cells (`cells-base` to `cells-pre`), so the restored cells' missing `0x100` may be that clear, untested (a run that holds the camera away from a sold cell would settle it); ours has neither `0x100` nor `0x80` in its cell words and no sampler reads them, so look only if a later item reads them. (5) A path laid over a stamped queue cell: **done as Q841** (the original keeps the cell without `0x800`, `park-engine.md`, "A path keeps it"; ours gave the load's flag back and no longer does, and a lifted path gives a stamp back as the clear's retile does). **The lift: done as Q846** (the original, by Backspace on each cell: (31,18) `0x028a` to `0x0802`, `0x800` back and `0x200` gone; (31,17), never flagged, `0x028a` to `0x0002`, `0x200` gone, no `0x800`; ours' lift equals both at 8,160 of 8,160, nothing built; `park-engine.md`, "A path keeps it"; the delete tool (Clear Land) is not built and was not run). **Not done from it:** a queue made a loose end by the game's own take-back, not by a byte of the file (Q841's cells are two bytes of a copy of the New Save, not a played state); the path run's price over a queue cell (ours charged $20 a cell, the original's bank cannot be read through the income, `q841/orig/run2`); and a queue laid over a path cell the stamp would hold, in either game.
- [ ] **Q889. What Q744 left: a turned rock in the original, the voice's place, the Large Rock Pillar (from Q744).** Ours marks, refuses, carries, sells and sounds an item on the model's cells since Q744 (`ParkObjects.CoveredCells`: the picture's non-empty cells swept from `mTopLeft`'s cell; the rock bought at (69,24) turned 0 types the original's 23 and stamps its 14, `q744/run-final`). Not done: (1) a Huge Hollow Rock turned 90, 180 and 270 in the original: **done as Q842** (the anchors (70,27), (73,26) and (72,23) for the pointer on (71,25), the 23 typed cells less the two corners the turn moves, the 14 stamped, all as ours does; `park-engine.md`, "A turned rock anchors, types and stamps"). (2) Whether the original's purchase runs a give-back tile pass: done as Q890 (it runs none: the placer skips a blank picture cell, retiles a marked one to tile 8, and the clear on a ground cell does nothing; no catalogue item has a table cell its picture leaves unmarked, so no separating case exists in Lost Kingdom; `park-engine.md`, "A purchase runs no tile pass over the table's cells"; the other themes' tables are not swept). (3) The rock's and the pillar's voice place: **done as Q844** (no script of the rock or the pillar sounds, so nothing is heard in either game; the place a voice would have is read instead, the model record's rectangle `FUN_00466b70` answers, in the running original after a real purchase: the rock turned 0 and 90 reads (69,23)..(73,27), middle (715,255), the pillar anchored at (74,24) reads (74,24)..(77,27), middle (760,260), base height -8.5 at all three, the ground's height there, and ours' log line gives the same; `park-engine.md`, "A bought rock's and pillar's voice place"). (4) The Large Rock Pillar (1422): **done as Q843** (bought in the original from its row once marked researched: the pointer on (71,25) anchors it at (69,23), it types 14 cells, the 4 x 4 less its two bottom corners, and stamps the ring of 10; ours' click gives the same). (5) The Dino Karts Tunnel: **done as Q843** for the put-down (the original types the add-on's 10 cells, stamps (11,16) and (11,17) alone, and its commit pass retiles all ten to tile (0,8) (`0x0052912c`, `0x0046e012`: `0x101` ORed, no texture changed); ours matches the typing and the stamps, `park-engine.md`). **The pillar's and the add-on's sale by themselves: done as Q1000** (the pillar sold from its window in the original gives back 573 and 437, plays the sale sound `0x98` once, and ours gives back the same words, 8,160 of 8,160; **the original has no route that sells only an add-on**: its cells have no hover category and a click there does nothing, Clear Land skips them, the ride's window has no button for one; ours opened the Tunnel's own window with a bomb there and no longer does; `park-engine.md`, "An add-on has no window and no sale of its own"). **Not done from it:** the stamped cells' texture indices, which are read in the original only (the terrain word's `0x1` is ours' type 4 under `ParkObjects.CoversGround`, by the code; not compared in a frame).
- [ ] **Q1013. What Q1001 left: a stack that is committed, a loop pylon, the other edit tools, a closed track and the other coasters, made in play in the original (from Q1001).** Q1001 stacked a pylon by carrying one onto another's cell and read the cell word and map type held, but every stack it made was drawn red (the no-entry cursor, a negative price; `0x4000` on the carried node and its `+0x68` neighbour, set when `FUN_00434740` answers 0) and reverted at the release. Not run: (1) **a committed stack**: what makes the stacked track valid (a longer coaster, a raised loft, a stack on a cell the track crosses over rather than meets, the swap-through-a-stack buttons), and then its cell word, map type and the Backspace of it; (2) why a carry off a stack left (5,9) and (10,12) at `0x0004` and not the station's (7,12): read the word during a carry off a stack and (7,12)'s cell record `+4` (the candidate is `Coaster_MoveNodeToCell`'s unstack, `FUN_00452d30` on the cell unless `+4` has `0x2`; `FUN_00436490` is not tied to the move tool: its one caller is `Game_StateMachine`, `0x0054fb8e`; which of the carry and the release cleared the word was not measured either); (3) a loop pylon (the loop button is off until the first upgrade, `0x004980c9`); (4) the raise-or-lower, rotate, pivot and the two swap tools on a pylon; (5) a track closed on its start, the Gorilla Thrilla (`coaster3`, item 1190), the Temple Of Gloom (`minecart`, item 1180) and another theme's coaster. (6) the carry gate: a carry onto another coaster's pylon cell (refused by `+0x14`, `0x00443c41`) and a third node onto a stack of two (the `+0xcc` cap); (7) a click of the track tool on a cell that holds a pylon (Q1001 read only the cursor). Recipe: `docs/TOOLING.md`, "A coaster bought and its nodes laid in the original", "Moving a pylon, and stacking one"; `original/trackcarry.py`. Ours cannot take any of these (Q594), so the question is only whether the original ever stamps.

- [ ] **Q925. What a Load Park and a Restart Park still spend (from Q924).** After Q924 a load from inside a park is 0.7 s in the game's log and 0.7 to 1.0 s in the film (0.14 to 0.17 s in the original, no loading screen). The third Load's phases (`q924/after2/run.log`): `ground` 0.21 s,
  `objects` 0.17, `terrain` 0.14, `guest sprites` 0.05, `queues` 0.03, `audio` 0.07 (the 189 MB music bank and the 17 MB kids bank are read again, because they decode on demand and were not kept), the rest 0.02 or under each; the film adds about 0.1 s to the log's total. Where each could
  be kept across a load (`ground`'s textures and meshes, `terrain`, `objects`' models, the guest sprites) or done off the loading screen, and what keeping costs in memory (the park's 0.70 GB resident), are not looked at; the first load of a launch is 0.2 s slower. Do it only if Alexah wants the load shorter than 0.8 s; Q247's choice on building in place now sets a rebuild of 0.7 to 0.8 s against 0.14 to 0.17 s.
- [ ] **Q1002. What Q917 left: the Ape's fitted turn on OnAMesh, role 4, and the ride proper's clips (from Q917).** Q917's heights agree to about 0.1 for one or two heads at a time in role 5
  (all seven clips) and one at role 6; a fitted turn needs three paired heads in one pause, and the original draws its riders' heads by a generator of its own, so a file's four heads are
  not the original's (`q917/`, seventeen pauses held at most two of ours' four). Ways in, not tried: poll the original's records read-only (`headpause.py`'s `read`) and pause only when it
  holds three of ours; or a much longer series of ours across many rides (ours holds a new set of heads each ride) against the original's role 5 pauses when three coincide. Role 4 never
  came in either game's role-5 runs (ours reached it once, 63 frames, in `q916/ape4`'s 1400 samples from a fresh ride); the ride proper's clips and a face-anchored head on a non-morphing
  parent (`LobbyModel.cs:609`) are also unpaused. Low value: every head compared agrees to about 0.1 and no difference has shown.

- [ ] **Q1014. The leaf in the first running pass after Mumbo's `c`, in the original.** From Q1010. The original was polled through `c` only (`q1010/orig/run3/`: the new Mumbo had no queue and no guests, so its channel stayed on role 0, frame 150). That `jm_leaf` holds `c`'s turn (the key) through frames 0 to 14 of the first `s`, `m` or `e` pass after it is read from the mask of `FUN_00472310` (both clips turn the leaf, so nothing is put back) and from Q1004's `m` to `e` measurement, and ours does it (`MeshRotator.TakeHeld`, `NodePoser.Keep`); it was not seen. Measure it: in the original, buy a Mumbo as `q1010/orig/run.sh` does, lay its queue to a path and send guests (`q885/orig/lib.sh`'s clicks; the new ride's entrance cell is the anchor's offset (1, -1) at turn 0 in ours), `leafpoll.py PID new:1109 SECS jm_leaf` from before the put-down, and set the rows below frame 14 of the first role 4, 5 or 6 pass against `c`'s key (`q1010/analyse.py`). One session; if the queue cannot be laid in the original, say so and leave the decode as the evidence.
- [ ] **Q1011. The Crazy Ape's heads ours never holds in clip `l`, against the original's (what Q1008 left).** From Q1008. The original's non-16 pictures in role 3 are two heads ours never has aboard
  there: Head03 (record 3, picture 8 over frames 62 to 102, `q940/orig-ape`) and Head11 (record 17, 8 at frames 78.3 and 94.9, `q1008/orig2`, and at the four pauses of `orig3`, frames 95.8 to 96.5); six fresh runs of ours (`q1008/ours1`, `ours-d3`, `ours3`, `final`, `final-d3`, `ours2`)
  held the same eight heads, Head1, 02, 06, 09 and 12 to 15, and `Q848_DELAY` did not change the set. By the table walk (`headframe.py`, 157 of 157 against the original's sprites) Head03 and Head11 pick 8 there from the original's
  matrix; whether ours' drawn turn for them does (the turn, `headresidual.py`'s measure, then the picture) is not measured. Ways in: a debug seam that names a rider's head (`ADDHEAD`'s draw), or a longer run of ours that draws other heads (the
  riders on later loops, not the first), then `headpicmatch.py ... --map lib/ape-records.json` against `q1008/orig-all.jsonl`. Low value: the rule is the same for every head and agrees with all 29 pairs.
- [ ] **Q1012. The Sun God's, Rocky Racers', Twister's and Eruption's heads against the original's stored matrices (what Q1009 left).** From Q1009. No clip of the game turns a head node or one between it and its mesh
  (`RideNodesTests.NoClipTurnsAHeadNodeOrOneBetweenItAndItsMesh`: all 80 models with heads, every head on a mesh, the only track on a head node a visibility track), and the Ape's role 5 heads, on a mesh an arm clip turns, agree with the
  original's stored matrices to 0.84 degrees by `headresidual.py --interp` (`q1009/`); the four rides' own heads (`incagod`, `porkpie`, `spider`, `volcano`; their meshes turn by `MeshRotator` or `NodePoser`, Racers' under a
  turned dummy (`pp_dum`), the Twister's under a moved one (`Dummy01`, a position track only)) have not been paused in the original, so their drawn turns are not measured against its stored matrices. Ways in: `headpause.py` on each ride bought in the original (or loaded from a park file made by
  `headseries.py ITEM NAME --save-role R`), `headpair.py` for a record map, `headresidual.py --interp --map`. Low value: the mechanism is the Ape's, which agrees, and no difference has shown.
- [ ] **Q1007. A seated rider's sound on raised ground, in both games (Q1005's "less the ground").** From Q1005. The Bouncy Dino stands on ground 0.0 in ours; in the original `+0xe8` (the draw's height) equalled `+0x8c`, which is ground 0.0 or the sprite's `+0xc4` flag `0x1000000` (not read), and ours subtracts 0.0, so a rider's put-off height "less the ground" (`0x004f9e91`, ours' `PutOffPoint`) is not told from the point itself. Wanted: a ride with riders aboard on ground above nought (a bought ride whose queue joins a path over raised ground, or the Bouncy Dino moved onto a slope with its queue connected; the stock plateau, cells (40..64, 58), has no path to a queue) and the same sale in both games: the original's `+0xc4` and `+0xe8` less `+0x8c` against the ground there (`groundheight.py`), ours' log's z against the node less the ground.

- [ ] **Q946. A person's first step from the entry and from the exit, and the stand point's consumer.** From Q945; its exit-point piece is Q873 (done). Left: (1) the board step routes a guest to the stand point (`FUN_004dedf0( 0 )`, then `FUN_004fa5f0`, state 13, `ride-operation.md`, "The boarding chain, end to end"); ours sends them at the entry cell (`PeepBehaviour.SendTo`) and nothing reads `ParkItemCatalogue.Item.StandPointBytes`, which is built and pinned and unused; build the target from it once (2) is measured. (2) Measure a rider's first step from the entry and from the exit in both games (the first sprite place after the put-down and after the board, polled in the original under Proton, `puffpoll.py`'s sprite table; the same in ours): the original's side needs the original running. (3) The Spray and the Kiosk's till (Q611) is read at the put-down's place, which is now the item's point, not the cell's middle: Q611's numbers ((525, 305) at the Spray, (435, 305) at the Kiosk in ours) have moved with it, re-take them before building Q611.
- [ ] **Q802. What Q800 left for a person or a machine it did not have.** From Q800 (run 13, lane A). Done by the
  coordinator (2026-10-29): `opentpw.maintainer` set in `~/repos/OpenTPW-laneB`, and the Saturday audit runs
  `python3 tools/check_docs_paths.py`. The Linux quick start is marked "tested on the maintainer's Linux machine only"
  and the Windows and macOS launch steps "not tested" (Alexah, 2026-10-28: no container). Left: (1) the first four good-first items
  (Q159, Q160, Q284, Q288) were all done by 2026-11-02, so their drafts file was deleted at Alexah's word (2026-11-03);
  new ones are tagged "Good first" in their queue items when chosen, and making them GitHub issues is Alexah's.
  (2) "A place to ask questions" is issues titled "Question: ..." for now; GitHub Discussions are Alexah's to switch
  on. (3) A test for `tools/check_docs_paths.py`: done by Q877 (`CheckDocsPathsTests`); the checker's gaps it found were Q878 and Q879 (done); what is left is Q1021.
- [ ] **Q1021. What `tools/check_docs_paths.py` leaves unmeasured or unread (what Q879 left).** From Q879 (run 21, lane B).
  (1) **Not measured**, so read from the documented rules and left: GitHub's id for `_word_` in a heading (we read the
  rendered `word`; the render shows `<em>`), a heading meeting a literal `same-1` (the checker numbers as github-slugger does),
  a letter with a combining mark (Python's `\w` drops the mark; JavaScript's would keep it), a heading over two lines (the
  line break is dropped), a heading inside a quote or a list item (left out: GitHub numbers it, how is unmeasured), an entity
  (`&amp;`) or a reference link (`[text][ref]`) in a heading. Find a public page that has each with `lib/ghids.py fetch`, read
  the id, then fix with a row that fails first. (2) **Not read**: HTML blocks (`<details>`, a comment over several lines), a
  code span, destination or title over two lines, a table's cells; a link inside a spaced code span (`a [y](docs/x.md) b`) is
  still checked as a path. (3) `lib/mdfuzz.py blocks` still finds a few pages in 3000 that differ from markdown-it in the
  nesting of a quote, a list and a lazy line; none of our pages has one. Low value: do only if a page ever needs it.

- [ ] **Q857. A park made fresh's file, off Lost Kingdom.** Split from Q803 (run 17, lane B); waits, Lost Kingdom first. **The other three themes' files**
  (`FRESH_PARK_FILE_OTHER_THEME`: the flyers' values, the music's and the land's voices and its emitter, the generator's seed, the ride system's counts). The advisor's slot
  0 `mValid` is Q543's (the welcome). Confirm by the diff against the original's file (`lib/freshcmp.py`).
  Also owed from Q803: the original loading a fresh file and keeping its music (the whole basis of the file's music voice, read in the disassembly and not run: load the file in the original under Proton and listen or poll the music voice), and the desk's photograph of the gates' flags at 90 in the first 93 sweeps of a fresh park.

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

- [ ] **Q587. The karts' and the water ride's track: edited, crossed (the rest).** From Q581 (`park.md`, "The
  karts' and the water ride's track, buoys and race"). Q688 lays the station and mouths and draws the pieces; Q762 arms
  the track tool and lays its runs; Q763 walks the circuit as a loop closes (the sections, `+0x2c`, the opening); Q765
  takes a run back with Backspace; Q766 makes each section's collision objects and buoy; Q767 walks from the mouths and
  pulses the open end; Q768 draws the armed tool's preview squares and its `c_end`; Q773 answers blue over a feature of priority
  below 3 and tears it down at the click (op `0x87`); Q775 lifts and walls the preview squares; Q776 lifts a path or queue tool's square over a piece; Q777 lifts a square over the boundary's hoardings by their `.hmp`; Q778 shows and hides an add-on's end caps in the
  walk and lays a loaded add-on's from its file; Q780 puts an add-on down on its ride's track (the snap, the verdict, the
  stamp, `mUpgradeParent` and the walk); Q781 plays the pieces' clips after a close and an add-on's put-down and makes a piece at
  rest. What is left: the add-on put-down's passes over the track about it
  (`TRACK_ADD_ON_PASSES`: `FUN_00523230`'s ops 0x85, 0x86 and 0x81, cells not read), the sale of an add-on (`SALE_TRACK_ADD_ON`: the original has no route that sells one by itself, so this is the console's only (Q1000); the demolisher's tail
  `0x00528512`..`0x0052852d` is read; ours leaves its track cells flagged, so no piece stands there again), the bit `0x01000000` a
  freshly made model's cap words carry in the original (not read); the put-down's check of the mouths, op 0x20b or
  0x210, whose refusal refuses the placement (`PUT_DOWN_TRACK_OVER_TRACK`: ours lays no anchor over track instead); a take-back's other cell or
  add-on (`TRACK_CLEAR_PIECE_CELL`, `TRACK_CLEAR_ADD_ON`); a closed loop's two-click edit (`TRACK_EDIT_CLOSED_LOOP`, its pieces' clips with it: `0x00526be4`);
  a run over path or queue mid-run, an end or straight laid there as a hill and a bend or junction plain in the original
  (`TRACK_RUN_OVER_PATH`; the river takes a plain piece, built), a crossing or junction
  (`TRACK_RUN_CROSSING`) and a run into the station (`TRACK_RUN_INTO_STATION`; when built, gate op 0x87's `TearDownUnder` on the last priority stored, which the station's 5 shuts), the verdict's rest; op 0x87's
  clear of a path or queue under the last anchor (with `TRACK_RUN_OVER_PATH`); a hill's reshaping by the mouths' walks (`TRACK_WALK_HILL_SHAPE`) and the walks of the closed loop's edit
  (`0x00526b7a`) and of `FUN_00532fc0` (`0x0053426f`); the carried
  ride's squares and their check (`CARRY_PREVIEW_TRACK_START`);
  a sale of a ride with track laid (`SALE_TRACK_TEARDOWN`: the walks `FUN_00530830` and `FUN_00530b70`);
  a written park's piece records (`SAVE_PARK_TRACK_PIECE_MODEL`); and a put-down at 90 or 270, tested and not seen in
  either game. Not measured in the original (the review of Q687, `review12/VERIFY-2.md`): the hill rows 9 to 18 and the
  walks' reshaping, the water refund, the sale's two-piece withdrawal (`0x00528179`), a refund below 100, the
  over-balance refusal, the karts' B2 (row 19), which walk pulses at the put-down (op 0x86's, `0x00538ca0`, by the
  reading; a breakpoint would settle it), the Full Simulation price (400 and 500 from `data/levels/Standard.sam`, where
  Instant Action's `Easy_Standard.sam` gives 200 and 250), a water piece's other cells (0xc by the disassembly), and a
  crossing or junction piece (`X`, `X2`). Hold the pieces against `q587/orig/A-load.log` and `W-cells.log`
  (`original/trackcells.py`). Confirm on `q581/karts.TPWS` by the same clicks as `q581/orig` (bought on preview cells
  (32,25) and (20,20), whose station sections are (30,25)/(32,25) and (18,19)/(20,19); loops to (34,29)/(28,29) and
  (22,23)/(16,23)): sections and buoys against `q581/orig/A-track.log` and `D-track.log`, and the pieces photographed
  beside the original's (`B-5-closed.png`, `D-13-boats.png`).
  - **Note (fork watch 2026-10-25, verify-all.md H1):** the pieces are not rigid: a kart or water piece (load flag `0x10422`, `0x00522b1e` and
    `0x00522cde`) is draped vertex by vertex at its placing, every vertex ending at its authored height above the ground
    under it. The chain: `0x004629d0` turns load bit `0x2` into loader flag `0x40`; `0x00461f10` sets `0x20000` on every node
    (`0x00462614`); the instancer `0x00463060` (flags `0x33a`, kart `0x522b90`, water `0x522d50`) calls `0x00469940` (`0x00463347`), which calls
    `0x00469a80` per node (cell triangle by flag `0x4`, Q398's note). Built by Q808 (`ProceduralConform`; `TRACK_PIECE_DRAPE` is gone). Not proven: whether the cars' sampler reads the draped copy.
    The same call also conforms every flag-8 object's base vertices to the terrain corners (Q750; fork watch 2026-10-27, verify-world H1).
  From the run 13 review (`review13/q781`, finding 3): no test reaches `PlayClip`, `_playing` or the clip census on a real
  water piece (`ParkTrackPieces` builds `LobbyModel`s, which no test makes without a window); test it when the pieces' clips are
  next touched.
  - **Note (run 22 review of Q758):** once a run may end on its own track at a junction or crossing, a closing run can leave
    the mouth's walk open, and `RunOn` then walks the circuit back towards the other mouth (`laid.MouthOpen`, op 0x86's
    `ShapeFromOtherMouth`, `0x0052cd48`; `docs/exe/park.md`, "The walk"). That is the original's rule and is built; this
    item's confirm should check the sections a lasso lays against the original (only a spur set up by hand is unit-tested:
    `ParkTrackToolTests.ARunThatMeetsASpurWithTheMouthsWalkOpenWalksBackTowardsTheOtherMouth`).
- [ ] **Q774. A feature beside a cell the original's verdict never reaches.** From Q773 (`park.md`, "A run over scenery"). In
  the original the blue answer returns at once, before the other cells of the anchor's two by two are judged, so its piece
  is laid over a path, a queue, another ride's track, a map type 7 or 0x15 cell or a ride's south cell beside the feature (op
  `0x87` tearing the south cell's thing down too, the compare at `0x00538cfd` standing open); ours refuses the run red and
  counts `TRACK_RUN_OVER_SCENERY_MIXED`. Build the rest when Q587's path hill (`TRACK_RUN_OVER_PATH`) and its crossings are
  built, for the stamp over such a cell is theirs; the south cell's tear-down is decoded and not seen, so run it in the
  original (a feature on an anchor's east cell with a placed ride's footprint on its south cell) before building it.

- [ ] **Q757. Whether the original's closing click of the karts' and water ride's track tool hands over the queue tool, and
  whether a count is owed.** From the Q762 review (the unsettled note; `docs/exe/park.md`, "The track tool"). The
  closing click (`0x00526eff`..`0x00526f83`, then the `0x80` stores at `0x00526fa7`) reads `[0x008186c0]`, the queue node
  `FUN_0052a050` hands the put-down (`0x00524dd7`, `0x00525269`; the move's `0x00525446`, `0x005258a7`); where it is
  non-zero the click installs mode 3 at the entrance's front (`0x00526f84`..`0x00527004`, `FUN_0052f580( 3, 0 )`) and
  clears it (`0x00527056`) instead of putting the tools away. Its eleven writers are all in `FUN_00524960` and the right
  click (`FUN_0052f200( 0, 1 )`, `0x00488434`) writes none by the listing, so a kart put-down, the right click that
  ends the queue tool, then the track tool armed and closed may hand the queue tool back. `ParkTrackTool.RunOn` always
  puts the tool away and counts nothing there (its comment says so). Watch `0x008186c0` in the original under Proton
  (`original/watch.sh`: the writers and the value) through a kart put-down, the right click, the arming click and the
  closing click, and settle it: if it stays set, build the hand-over (the queue tool anchored at the entrance's front,
  `ParkBuildMode.ArmAt( Queue, ... )`) or count it (`TRACK_TOOL_CLOSE_TO_QUEUE`) where ours reaches that state; if the
  right click clears it by another path, say so in `park.md` and drop this note. `A-load.log` closed to mode 0 at 221.95 s
  after its queue tool had gone with no charge (144.90 to 153.94 s), which argues the pointer was nought there.
  - **Note (fork watch 2026-10-31, verify-ui-hud UI-031):** the line tools: with a start pending, `0x00526f95`/`0x00526fb3`
    install the queue tool (`0x00527004`) and clear the pending start (`0x00527056`), and a path start ends it (`0x0052712f`); with none
    pending, `0x00527146` keeps the tool on exactly Ctrl and otherwise calls `0x0052f200( 0, 0 )`. The Ctrl half is now in
    `park-engine.md`, the paragraph on carrying a purchase.

- [ ] **Q588. Dino Karts race for guests.** After Q587. The -4 record (`Bumper_LayRide`'s grid and start buoys, the
  template's 3 laps, 2048 arrival), `BUMP 7` to state 1 and the race's first buoy, `BUMP 4` a kart a rider (mesh
  `cars % 4`), the retarget's grid and race arms with the thrust bonus, the buoy arm's laps, state 3 and the end at the
  unload point, `BUMP 14` and `BUMP 3`, the step's clips 5 and 0xc by speed, and the track's objects in the point test,
  the rim and the obstacles (quarters, rectangles, 0x40 circles). Not read, to decode or count: the seat a rider takes
  (`FUN_00549c60`), a kart's height over the track. Confirm against `q581/orig/C-track.log` (the grid, 9 to 16 and 8,
  the timer -3 to 0, state 3, the next kart at the centre) and a frame of a kart on the track (`C-4-race.png`). Moves the
  website's rides list.
  - **Note (run 19 review, q939 finding 1; built in Q883, the note below is what it fixed):** `BUMP 5` (`0x00554836`: the object's `+0x2c`, `mIsTrackRideValid`, into the
    register; the object from `0x4cd300`, no bumper record) is not answered for a kart or water ride here: `RideScript.Bump`
    returns at its no-record test before the selector, so a bought Dino Karts with its loop closed and its queue joined loops at
    `GoKarts.RSE` words 15 to 18 (`BUMP 5, 0`, `BRANCH_Z ->15`) from its first tick, counted `Go Kart: BUMP at 15`, and never
    admits a guest; the original given the saved file runs on past it (`q939/run3`, `orig3/scripts.log`). Like Q448's BUMP 17 it
    reads the thing, not a record, so it can be answered before the race (Splish Splash's is word 20, past its closed test at 8).
    Q993 (2)'s spin at word 15 is the same loop.
  - **Note (Q883's run, `q883/fix2.out`):** with `BUMP 5` answered, a bought Dino Karts' script admits the queue's guests (`VAR_ONRIDE` 1 to 3 at 40 s, `VAR_LETMEON` taken: `fix.out` reads 0 and `fix2.out` 31) and the five guests sent are then `Riding dest 43` for the rest of the run, with no kart car to seat or return them: the script's `BUMP 1`, `4`, `2`, `3`, `14` (words 74, 79, 142, 189, 106, 109) and `BUMP 7` (20, 45) are counted no-ops here. That is this item's build, and it now holds the guests aboard instead of in the queue.
  - **Note (fork watch 2026-10-21, verify-world-other L6):** a loaded ride gets the race's first buoy back from the track-ride
    loader's close chunk, type 6 (`0x00543928`, `saves.md`, "The close"): for BumperType -4 (this ride), after the handle check, it walks
    the buoy chain from `+0xa8` to the first buoy after the start buoy (flag 4) not flagged 2, wrapping, into `+0xa4`. Build that
    on load of a ride saved open; the writer never saves `+0xa4`.
  - **Note (fork watch 2026-10-25, verify-all.md N2; corrected fork watch 2026-10-28, verify-world C1):** the loader sets header flag `0x80` at run time
    (`0x004625f4`) on a kart or water track piece, so the model's height there is the landscape's under the point (mode 1, `park.md`) for a flat piece;
    the hill pieces (`gk_trckH`, `_d`, `_u`, `wr_trckh`, `_d`, `_u`) carry a `0x7` lookup record and the instance flag `0x8`, which `0x450ea0` tests first
    (`0x450eb1`, before `0x450ecf`): mode 2, draped faces (loader `0x44a870`, `0x44a9ed`). Not proven: that the cars' sampler uses the draped copy.
  - **Note (run 12 review, q766 finding 4):** a car's buoy (`+0x7c`) is a slot of the `DAT_00877b78` pool, read at `0x0054a4ed`,
    `0x00547991`, `0x0054a9a2` and `0x00542f22` (D-track.log's "car 4 ... buoy 18" is that slot, and the grid's "9 to 16 and 8" above
    are slots too). Ours' `Buoy` keeps no slot, and the original's one `+0xa8` chain is two lists in ours: the track rides'
    section buoys (`ParkTrackRideTable`) and the bumper rings (`ParkBumperCars.Ride.Buoys`). Build the pool's index with the race.
  - **Note (fork watch 2026-10-28, verify-world W3):** the directed-edge face test a kart's height over the track uses is
    `0x4514cf` (`0x4514c7`..`0x451536`): edges V2 -> V0, V1 -> V2, V0 -> V1, the value dz x px - (dz x Bx + dx x Bz) + dx x pz
    with dz = Az - Bz, dx = Bx - Ax and p the third and fourth arguments, outside when it is below nought (nought is inside;
    `fcomp [0x6fe3dc]`, `test ah,1`, `0x45152a`). `md2surf.py`'s `face_height` carries the same test; `ParkSceneHeight.cs`'s
    counted `SCENE_HEIGHT_SURFACE_MESHES` is the site.
- [ ] **Q589. Splish Splash runs for guests.** After Q588. The -5 record (one start buoy, unload point a third of the
  way to it, 512 arrival, 64 boats), `BUMP 7` straight to state 2, a boat carrying the whole boarding list straight to
  the race, one lap, removed at the unload point with the ride left at 2, and the boat's spin (`+0x58` moved at a rim or
  an obstacle: p, ±48 × a term not read, when the old spin has p's sign, else old + p; × 0.998 a tick, turn `+0x58 / 128`; the term the contact sets is not read). Confirm against
  `q581/orig/D-track.log` and `D2-track.log` and a frame of a ring in the channel (`D-13-boats.png`). Moves the
  website's rides list. Splish Splash's `BUMP 5` at word 20 is now answered too (Q883, from `mIsTrackRideValid`); it was not run.
  - **Note (fork watch 2026-10-21, verify-world-other L6):** as Q588's: BumperType -5 takes the same close-chunk arm
    (`0x00543928`, `saves.md`, "The close"), the race's first buoy into `+0xa4` for a ride loaded open.
  - **Note (fork watch 2026-10-25, verify-all.md N2; corrected fork watch 2026-10-28, verify-world C1):** as Q588's: the water kit's pieces (`0x10422` at `0x00522cde`) carry the run-time flag `0x80`
    (`0x004625f4`), the landscape's height under a boat (mode 1) for a flat piece, mode 2 for the hill pieces (`wr_trckh`, `_d`, `_u`: Q588's corrected note), and are draped per vertex (Q587's note).
  - **Note (run 12 review, q766 finding 4):** as Q588's: the boats' buoys are slots of the same `DAT_00877b78` pool (`0x0054a4ed`, `0x00547991`,
    `0x0054a9a2`, `0x00542f22`), which ours' `Buoy` does not keep.
  - **Note (fork watch 2026-10-28, verify-world W3):** the edge test is Q588's note (`0x4514cf`).
- [ ] **Q726. The karts' and the water ride's track add-ons and hills.** From the Q581 review (`park.md`, "The karts'
  and the water ride's track, buoys and race", "Not read" lists the add-on sections (types 12 and 13) and the hills
  (`gk_trckH*`)). Lost Kingdom ships three track add-ons in `jungle/upgrades`, each `UpgradeType 1`: `lavajump` 1500 and
  `mammtunn` 1501 for Dino Karts (`AddOn.UpgradesId 1150`), and `watertun` 1502 for Splish Splash (1160). Each lays a
  section 12 or 13 (`TrackRide_AddSection`, jump-table arms `0x0054b7e4` and `0x0054b86d`, each `mov ebp,0x100` then the
  straight's arm), so its rectangle carries flag 0x100, which drives `Bumper_StepCar`'s add-on arm (the object's `+0x10`
  == `0xdac`, `FUN_00548e80` at `0x00548d11`, `0x00548daa`, `0x00548e1c`). Q587 to Q589 do not ask for it, and Q512 and
  Q513 cover only the buy list and the window (the snap and the put-down are Q780's). Decode the add-on arm and the hills, build them (or count them
  with `Unimplemented.Report`), and confirm one of each against the original.
  - **Note (fork watch 2026-10-25, verify-all.md H1):** the add-on and hill pieces come from the same kits (`0x522aa0`, `0x522c60`), so they are draped
    per vertex like the rest (Q587's note); build them so.
  - **Note (fork watch 2026-10-28, verify-world C1 and W3):** the hill pieces (`gk_trckH`, `gk_trckH_d`, `gk_trckH_u`, `wr_trckh`,
    `wr_trckh_d`, `wr_trckh_u`) take height mode 2, not mode 1: `0x450ea0` tests the instance block's flag `0x8` first
    (`0x450eb1`) and only then the header's `0x80` (`0x450ecf`); the loader `0x44a870` sets `0x8` when any lookup record
    (`[model+0x7c]`, 20 bytes each) carries `0x2` (`0x44a9ed`..`0x44a9f2`). `md2surf.py` on jungle: those six carry a `0x7` record
    and `+0xa0` = 0.0; the flat pieces (`gk_trckB`, `B2`, `Q`, `S`, `v`, `X` 3.1; `wr_trckb`, `b2`, `s`, `s1`, `x`, `x2` 3.254)
    are mode 1, `gk_trckH_a` 7.9 and `wr_trckh_a` 8.097 mode 1, `wr_trckQ` 0.0 with no record mode 1 by `0x80`. The edge test is
    Q588's note (`0x4514cf`). The same finding is relayed to the session building Q587.
- [ ] **Q592. The cheat table's twelve rows.** From Q591 (`park-engine.md`, "The cheat table"): each is counted on its key's
  release with Alt alone (`Level.CheatRows`: `CHEAT_RESEARCH`, `CHEAT_MONEY`, `CHEAT_LOCAL_TICKET`, `CHEAT_GLOBAL_TICKET`).
  Build them as the original's after Q720 settles whether the keypad's digits reach their rows and the cheat flag's
  first-key clear is run in the original; whether this game's R binding also opens the research screen under Alt
  (the original's row needs no modifier, matched by `==`) is to be checked in the same session.
- [ ] **Q594. A loaded coaster's track is drawn (its pylons are Q817).** From Q706 (`q706c/side-by-side-coaster.png`: the
  original's `q706b/O2/o-t19061.png` beside ours `q706c/run3-A/A-coaster.png`, both at 310,355 zoom 130 rotation 0, Alexah's
  jungle `New Save`). The original draws the Temple Of Gloom's track over its 40 pylons; ours draws the pylons (Q817) and not the
  track, though Q706 rebuilds the track's knots, curve and samples from the save (`ParkCoasterHeights`, `ParkCoasterPylon`). Build
  the track's mesh from the samples and the `coaster.sam` cross sections (FileFormats `sam.md`, "A
  `coaster.sam`'s tables"; the mesh builder not decoded: decode first). Confirm by the same two frames side by side.
  - **Note (fork watch 2026-10-31, verify-world M7):** the scroll rule: `0x00435adf` gates on `[[ctl+4]] & 4`; delta = (now
    - `+0x138`) x `[ctl+0x2c]` x 0.001f (`0x00435af8`..`0x00435b1a`); the floats at `[ctl+0x13c]` add it and wrap past 1.0,
    the wrapped amount coming off delta too (`0x00435b28`..`0x00435b76`); faces of a texture with flag `0x20` get V -= delta
    (`0x00435c8b`; U untouched). The stored `fScrollRate` is not read in the loop. What `+0x2c` is (the fork's "power
    factor") is unproven: no writer found.
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
  - **Note (fork watch 2026-10-27, verify-ui-hud M1):** the advisor's one voice is paused and resumed by the same two commands:
    `Game_Pause` with arg2 nought (`0x004092d1`) -> `0x00598960` -> `0x0051c060` -> `0x006b5bd0` -> `0x006b5c50` -> `0x006b8a10`,
    to the voice's slot `+0x18`, `0x006bcab0`, with commands 2 (functor `0x0070a5b8`) and 3 (`0x0070a5c0`); the resume adds the paused
    span to the voice's start (`0x006bcafd`..`0x006bcb46`), as `Advisor.cs` holds. The `+0x3c` quirk above reaches him too: a second pause
    of the same sample does not stamp again, so its resume adds the time since the first (effect on him unproven). The window procedure's
    `(1, 1)` does not take this path: it suspends all (`0x0051bcf0`). Unproven: what the driver does to the DirectSound buffer.
  - **Note (fork watch 2026-11-12, verify-audio-fx.md F2):** the sound wrapper's vtable is `0x00711920`: slot `+0x38` (`0x006d25a0`) is
    `QSWaveMixPauseChannel( session, 0, 1 )` and `+0x3c` (`0x006d25c0`) `QSWaveMixRestartChannel( session, 0, 1 )`; the
    per-channel `+0x2c` and `+0x30` (`0x006d2500`, `0x006d2540`) pass flag 0. What flag 1 does inside `QMixer.dll` is not read.

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
  - **Note (Q243's census, 2026-11-12):** counted in `ParkAudioFileVoices.cs` and named by no item, so they are this item's too:
    the other classes `PutBack` does not put back, `PARK_FILE_SOUND_VOICE_CLASS_2`, `_HELD_CHAIN_CHILD_TYPE`, `_HELD_CHAIN_NO_KEY`,
    `_HELD_CHAIN_OWN_RANGE`, `_HELD_CHILD_NO_ZONE`, `_NO_CATEGORY` and `_NO_SAMPLE`, and the list read short,
    `PARK_FILE_SOUND_VOICE_CLASS_10` (`audio.md`) and `PARK_FILE_SOUND_VOICE_UNREAD`.
- [ ] **Q719. Does a loaded park play at the save's sound volumes? (one game check)** From the fork watch, 2026-10-20
  (`verify-world.md` M2; `saves.md`, "Sound"). The loader `0x0051c400` overwrites the live sound words at `0x00803aa0` with
  the save's (the volumes and the three on-flags; all 558 park files on hand hold 100/75/60/75 and on/on/on) and sets
  nothing else; the tick `0x0051bd70` pushes them. The profile applier `0x00423dd0`, which would put the player's options
  back, is reached only from `GameOptions_Construct`, the options screen and two table references, and from no load
  path found. If so, a loaded park overrides the player's volume options until they are touched. Confirm in the original
  under Proton: set music off in the options, load `Easymode.TPWI` (or a copy), and listen or record the mix
  (`record.sh`); then decide whether OpenTPW's load should do the same (it keeps the player's options today) and say the
  result at the site. Nothing is built before the check.

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

- [ ] **Q151. A park's sky draws the lobby's horizon band. Decode first.** Found by the 2026-09-26 staleness audit.
  `FUN_005863c0` gives the band's four rings, per half, 0.99, `c08` + 0.5, `c0c` + 0.5, `v` + 0.5 and then 0.49,
  `c08`, `c0c`, `v`, with `c08`, `c0c`, `v` 0.34, 0.17, 0.01 (`0x00701f88`) by default and 0.19, 0.01, 0.49
  (`0x00701f94`) while `DAT_008bcbc8 & 0x2000000` is set (read first-hand). `Sky.UpperV` and `LowerV` hold the second
  set, 0.99, 0.69, 0.51, 0.99 over 0.49, 0.19, 0.01, 0.49, which is the lobby's, and `Sky.Band` draws it for every
  sky; `_tintRamp`'s remark, "the whole difference between the two skies", is true of the code only. Settle which
  scene sets the flag (it also gates a mirrored copy of the dome), write the draw to `docs/exe/`, then give a park the
  default set, 0.99, 0.84, 0.67, 0.51 over 0.49, 0.34, 0.17, 0.01. Confirm: a camcorder frame of Lost Kingdom's
  horizon before and after.
  - **Note (fork watch 2026-10-28, verify-hz-world H3):** render word `[0x008bcbc8]` bit `0x2000000` (band coordinates
    0.19/0.01/0.49 and the mirrored dome) is written only by `Lobby_Start`, the globe page's start (`0x005dd072`, cleared at
    `0x005dd2e2`, `0x005dd762`, `0x005dda22`; no other writer of that bit to the global), so the island pages draw the DEFAULT
    band set (0.34, 0.17, 0.01) as a park does, most likely: this item's "which is the lobby's" is the globe's only. A live read
    of `[0x8bcbc8]` on Lost Kingdom's island page settles it. Also: in the lobby the rebuild `FUN_00585720` writes random greys
    0x80..0xff into all four cloud colour tables after the load, so the lobby's three untinted layers are not the `sky_rgb`
    gradient (`Sky.cs`), and the first layer's flood is the island colour at SKYQUALITY above 1.
- [ ] **Q859. A queue piece whose index is 8 or more draws what the original draws (decoded in Q895; the build waits on the confirm below).** From Q154. A queue cell's
  tile index is `2` for a straight and `3` for a corner, `+1` on the four bumped corner pairs, `+3` per mutual path link, and the
  table of queue models holds eight; the original's index runs on into the kart kit's table (`park-engine.md`, "An index past the
  queue table"): at 9 it draws `gk_trckQ`, at 10 `gk_trckS`, at 11 `gk_trckB`, at 14 `gk_trckX`, with the row's end-cap mask and
  the queue's angle; at 8 it draws no model over the ground's filler, and a park saved with an 8 faults its load. OpenTPW drops
  links until the index fits (`ParkPathBuilding.Retile`, counted `QUEUE_TILE_INDEX_OUTSIDE_TABLE`; its comment still says
  undecoded). Build: from 9 on draw the kart row's model (`rides/gokarts.wad`) with its mask; at 8 the ground alone, as the
  original; and never write an 8 into a park file, which the original's load faults on: write the cell as `Retile` draws it today,
  index 5, the end piece (a straight is the only shape that reaches 8, `2 + 3 + 3`; one link dropped), with the end piece's
  record, count it, and say at the writer that the original writes the 8 and cannot load it (rule 11). A file of the
  original's holding an 8 loads here and draws the ground alone (a fix: the original faults). Save: a cell at 9, 10, 11 or 14
  is written with the model record the original's `FUN_005229e0` makes for it: the kart row's type (item 17100 + index - 8, as
  `TrackKit_LoadKartPieces` registers it; inferred, since `FUN_005229e0` passes only the handle), flags `0x33a`, the one-cell
  footprint `0x00816d68`, 360 less the angle, the row's mask; its bytes are read from the file the original saves in the
  confirm (`parkfile.py`), not made up. `SAVE_PARK_QUEUE_CELL_MODEL` stops counting those cells. Load: the rebuild claims that
  record by its handle, flags, cell and angle (`FUN_005366e0` -> `FUN_00463ab0`). First confirm in the original (lane A): lay a queue to a path, lay a path over the queue cell before its end
  (the route `park-engine.md` reads, not run), and photograph the end cell as a straight (8) and as a corner (9): where the kart
  piece stands against the cell is not established (`FUN_005229e0` lays it on the queue cell itself with the one-cell footprint, `0x00522a0d`..`0x00522a15`, where a kart track lays its piece one cell on with the 2 x 2 footprint `0x008179f8`: the photograph settles where the model's own origin puts it); then Save and Load each through the in-park Load list (`FUN_0049e880` -> `FUN_005ac650`) (predicted, read not run: the 8 faults at `0x00463b18` and the game ends through the C runtime's filter; the 9 loads and claims its `gk_trckQ` record, unexamined). `tools/harness/lib/queuetile.py`
  gives the original's index for any mask. Also counted, never reached: `QUEUE_TILE_TRACK_PARENT_UNNAMED` (a type 12 or 17 queue
  record naming no parent reads the record `0x6cde` records past the world's base, the last `0x28` bytes of map cell (127,127)'s
  record: decodable; a unit test covers the counted branch). The gate's refusing branch is covered by unit tests and mutants only.
  A played park can put the flags nibble on a queue cell: op `0x85` (`0x00538b0b`..`0x00538bc5`) sets `[record+0xe]` to 1 where a
  2 x 2 meets map type 1 or 3, so a kart or water ride's end or straight laid over a queue gives it (the other three cells through
  the parent swap, inferred); OpenTPW's track tool refuses that run (`TRACK_RUN_OVER_PATH`, Q587), so when Q587 builds the hill,
  cross-link it here. What the original shows with it set is not established.
- [ ] **Q942. The suite has an unnamed one-off failure.** From the Q154 review (run 17). A full-suite run with the game has failed once,
  not named and not repeated, on 2026-10-20, 2026-10-24 and 2026-11-01 (the third on Q154's run; one rerun and the gate passed).
  **Measured 2026-11-01, after a reboot: 4 of 7 full suites failed once each, on fixed data:** `AudioClipTests.EveryShippedClipIsItsFloatDecodeTakenTo16BitsAndHeldToTheRange` twice (one sample of `MusicHD.sdt:level4cc.mp2` differed), `FullSimulationCreatesTwelveThingsWithRegularLoans` and `EveryTileOfEveryParkIsTheOriginals` (space, 16). The clip test alone passed 30 of 30. Read it as machine memory until memtest clears it: the kernel faulted that morning on a pointer one bit off NULL.
  Suspects, none shown: simulations that are not seeded (238 of 260 `new ParkPeople(`; `new Random()` at `ParkPeople.cs:201-202` and
  `ParkAudioScripts.cs:38`) and machine load under the real-clock zone. Keep every full-suite run's output (`capped.sh` ... `| tee`
  into a file under `~/.cache/tpw-harnesses/`) so the next failure is named when it comes; then seed or fix what it names, and
  prove it by putting the cause back (`VERIFYING.md`, the flaky-test rule).
  Piece Q866 (ticked) ran sixteen suites (ten plain, six with the generators a lost scratch patch seeded; not every generator in the product: `ParkAudioScripts.cs:38`, `ParkAudio`, `ParkCarSounds`, `Advisor`, `LobbyAudio`, `LobbyFlyer` stood unseeded) and stored the loop; what is left is the failure itself, still unnamed. Untested: the real-clock zone under a
  machine load heavier than three suites at once. Next failure anywhere: `tools/harness/lib/suiteloop.sh OUTDIR [N]` keeps every run's output, so its file names the test.
  **One of the one-offs is named (2026-11-04):** `ScriptSoundTests.AHeldParksHeldChainMakesItsChildrenUnheardAndNoneBurstsOnResume` failed once in a full suite at `b6531222` (line 1672, "not before the newest child's time", 2 samples where 1), and passed 60 of 60 runs of `ScriptSoundTests` alone and the next full suite; the output is `~/.cache/tpw-harnesses/q942/named-2026-11-04/`. The gap is drawn from `ParkAudioScripts`' unseeded `_scriptRandom`. Piece Q1022 (ticked) found the cause and seeded `ScriptSoundTests`' park audio; Piece Q1023 (ticked) swept the nine files that make a `ParkAudio`: none fails for any seed tried; Piece Q1024 (ticked) swept the other test classes' unseeded generators (135 rows of 127 methods over seeds 0 to 507 or more: none fails; the deeper seeds are Q1025), Piece Q1025 (ticked) took those rows and 5 new ones to seed 1150 (none fails; the deeper seeds are Q1128). `ConsoleLogTests.AFileThatStopsBeingWritableEndsTheFileAndNeverTheTerminal` failed once in a full cloud run on 2026-11-12 and passed alone; it draws from no generator the sweep routes, so no seed reaches it. It and the clip, Full Simulation and `EveryTileOfEveryParkIsTheOriginals` failures of 2026-11-01 stay unnamed.
- [ ] **Q1128. The unseeded generators' rows are swept to a few thousand seeds (the sixth piece of Q942).** From Q1025. The 140 rows (`q1025/only-light.txt`, `only-mid.txt`, `only-mon.txt`, `only-new5.txt`) have passed seeds 0 to 1150, which rules out only a failure commoner than about 1 seed in 384; Q1022's was about 1 in 470. Take them from 1151 to 3000 or more (no failure in 3,000 seeds rules out 1 in 1,000): a seed costs 10.1 s (the 121 rows), 7.4 s (the 13), 15 to 16 s (the 14 s row) and under 0.1 s (the 5), so 1151 to 3000 is about 17 h of one process, 8.5 h in two: a run of its own. `q1025/chain.sh` is the template (`docs/TOOLING.md`, "Finding which tests a default generator reaches"). Re-run the control and re-profile first, sweeping any new drawing row from seed 0; seed or branch any row that fails, proving it by a seed that fails without it.

- [ ] **Q155. The other readers of the speed word, and the play rate (decoded in Q897; Q875 built the word and its first readers).**
  Found by the 2026-09-26 staleness audit. Q875 keeps the word per script and reads it in `WAIT`, the three scream readings, the
  bumper ride's performance and the wear (`park.md`, "What a loaded script holds"). What reads the word and does not yet here:
  the `TOUR` record's `+0x50` (`0x005517c0`, `FUN_0055d9b0`; `TOUR` is itself unbuilt and counted),
  `COAST`'s ride (`0x005517e1` and `INITIALISE`'s `0x00554aab`, `FUN_0043b2b0`; `COAST_RIDE_SPEED`), the bounce riders' sine (`0x00557c03`, `ride-operation.md`,
  "The bounce riders' height") and the walk placer's divisor (`0x0055823e`) (both `WALK_RIDER_SPEED_DIVISOR`); check each against the files before building it. With a divisor
  of 1.1 a `WAITANIM` of a clip under 300 ms waits for weeks in the original, and waits one turn here by Alexah's decision (2026-11-04; counted `WAITANIM_SPEED_WRAP`; `park.md`, "`WAITANIM` is NOT `TRIGANIM` with a wait"). Confirm: the Belly Bounce's clips at their rate in the running game against the original's.
  From Q166: every trigger hands the divisor (`[ESP+0x14]`, 0.5 + 0.01 × speed, `0x00551cdc`) to the channel as its
  play rate (built in Q1039: `RideScript.StartAnimation` passes `DivisorOf( Speed )`, where it passed 1.0) - `TRIGANIM` `0x00552952`, `WAITANIM` `0x00552a9f`, `LOOPANIM` `0x00552be4`, `TRIGWAITANIM` `0x00552d2f`,
  `TRIGANIM_CH` `0x00553190`, `WAITANIM_CH` `0x00553325`, `LOOPANIM_CH` `0x00553465`, `TRIGWAITANIM_CH` `0x00553624` -
  of which the six built ones (all but the two unused `_CH` waits) hand it; the deadlines of `TRIGANIM` `0x005529ac`, `WAITANIM`
  `0x00552acd`, `TRIGWAITANIM` `0x00552d94`, `TRIGANIM_CH` `0x005531ee` and `TRIGWAITANIM_CH` `0x00553682` divide by
  it; Q1060 built `TRIGANIM`'s, `TRIGWAITANIM`'s and `TRIGANIM_CH`'s (`RideScript.TriggerDeadline`, the floored clip-less-300 divided and cut; the `ANIM_TRIGGER_SPEED_DEADLINE` count is gone), `WAITANIM`'s was built before, and `TRIGWAITANIM_CH`'s, which no shipped script uses, is unbuilt (the ride window's
  `RIDE_SPEED_SCALES_WAITS` count went with Q875's `WAIT`). `WAITANIM` is built in the original's order (`RideScript.AnimationWait`: `fdiv`
  `0x00552acd`, `_ftol`, unsigned compare with 300 at `0x00552ad6`; at word 60 a 400 ms clip waits 300, a 1000 ms clip 636), a clip under 300 ms waits one turn here where the original waits days (Alexah's decision, 2026-11-04;
  `WAITANIM_SPEED_WRAP`, counted by Q876). The speed push of an upgrade's completion (`0x004dfabc`, when the new tier's `InitSpeed` is above nought)
  is counted `RIDE_UPGRADE_COMPLETION` and unbuilt: build it with that item or here. `COAST`'s ride is counted (`COAST_RIDE_SPEED`); the scheduler's channel speed is built (Q1061); whether a Lost Kingdom coaster reaches `COAST` here is unchecked. The unbuilt
  `TRIGANIMSPEED` passes its rate / 1000 × the divisor (`0x00552f6e`), and its deadline, the length × 1000 / its rate,
  does not divide by it. The length a trigger answers does not depend on it (`FUN_00472f60`,
  `0x0047323e`..`0x00473258`).
  From Q174: `+0xe4` is the play rate in thousandths. After every due turn of a script with a model the scheduler writes
  `(0.5 + 0.01 × speed) × +0xe4 / 1000` into every channel's queued speed `+0x30` (`0x00551789`), which a promotion
  and a loop's replay start at (built in Q1061 with the factor fixed at 1000: `RideScript.WriteChannelSpeeds`). `TRIGANIMSPEED`, unbuilt and counted, would leave its rate
  there (jungle `Gates` 4000), and the factor would then be a field. The Easymode Belly Bounce was saved looping at 1.1; its `LOOPANIM 2, 0` @43 skips on the saved key
  (`park.md`, "Where OpenTPW's animation state parts from the engine's", difference 4), and its later triggers hand 1.1 too since Q1039. With a divisor other than 1, a clip under 300 ms or no model makes
  `WAITANIM`'s qword sum wrap `+0xa0` (`park.md`, "`WAITANIM` is NOT `TRIGANIM` with a wait").
  - **Note (Q243's census, 2026-11-12):** beside `COAST_RIDE_SPEED`, the scheduler counts `TRACK_RIDE_SPEED_PUSH` (`RideScriptScheduler.cs`):
    `FUN_00545180`'s push into a kart's or a water ride's record of the track-ride table, where only the bumper rides' are built.
- [ ] **Q1336. A bought channel's queued speed before its first visit, and a loaded one's nought, in the original (from Q1064).** Q1064 writes a loaded
  thing's `+0x30` as it stands (`ParkRides.WrittenChannel`, `seeded` true), nought after a promotion or a start-at-once trigger (`0x004738af`, `0x00473408`), and
  keeps a bought thing's fallbacks: 1.0 started, nought never started (`seeded` false). Neither was read in the original (Q1064 ran in a cloud container: no Proton, no Ghidra).
  Read a bought ride's `+0x30` in the running original before its first due visit (the model maker, `docs/exe/saves.md`, "A park made fresh"; the just-bought Hot Pot of
  `q257r` hotpot-bought-shut holds its divisor 1.1), and save the original's loaded park within 248 ms of a promotion and read the saved `+0x30` (`tools/harness/dotnet/queuedspeed/`);
  predict both first. Where the bought value is the divisor, the made fallback becomes `RideScript.ChannelSpeedOf` of the script's word.
- [ ] **Q156. The staff screen's two happiness meters (decoded in Q898: they fill from the left).** Found by the 2026-09-26 staleness
  audit. Nothing is left to build: the original paints each meter with its stat-bar skin alone and fills it from the left
  (`hud.md`, "What paints the all-staff meters"), and this game has drawn them so since Q369 (`ParkStaffScreen.Meter`, a
  `UiStatBar` in `happygrad.wct` inset 8). The confirm asked for, the staff screen at a known happiness photographed, is
  Q369's (`q369/run-fix2/1-handymen.png` on the desk, `q369/orig/b-1-handymen.png` in the original, both on `q369/skill.TPWS`), made
  before this decode. The maintainer closes it on those frames, or names a fresh photograph.
- [ ] **Q863. The route improver's diagonal pass (`CELL_REROUTE_DIAGONAL_PASS`).** From Q157, which counts it where a no-splice scan
  leaves a one-cell diagonal step. `FUN_005108a0` from `0x00510a1e` goes over the route's steps and breaks a one-cell diagonal in two
  when a shut edge of the corner decides it (`0x00510b15`..`0x00511020`, the inserted corner at `0x00511040`, and no more than 251 waypoints:
  `0x0051104e` skips the insert when the count plus one reaches 252, and 252 is the splice's own limit at `0x00510978`). The decision (read by
  the second reader of Q157's review, `0x00510b34`, `0x00510c87`, `0x00510dbe`, `0x00510efb`, not decoded again here): it asks the two edges of
  `to` back along each axis and the two forward edges of `from`, and writes only when exactly one of a pair is shut, through the open corner,
  the inserted waypoint at `to` plus the offset (`0x0051107d`..`0x00511090`). Counted by Q157 without that test: any one-cell diagonal; a
  pass that follows nine splices does not run at all (`NineSplicesEndTheRouteWithNoDiagonalPass`);
  `CellReroute.Run` stops where it starts. Decode the corner cases (the other sign combinations from `0x00510c73`), build the pass, and
  run the routes of the stock park through the old and the new to see how many steps change. Confirm: a guest's route over a bent path
  in both builds, the waypoints printed, predicted first.
- [ ] **Q1236. Count the reverb handoff with no ride under the camera, or say why not.** From Q864 (a cloud lane, no
  Ghidra); its two Ghidra questions were settled from the listing by the run 22 review. `FUN_0042a4e0` answers
  `[0x00790a44]` only with bit 4 of `gui_CameraFlags` (`[0x00790ab0]`) set and `[0x00790988]` non-zero, both written by
  `FUN_0042a560` (`0x0042a636`, `0x0042a673`), the "Ride it!" entry the End takes; first person leaves bit 4 clear, so 0
  there. With 0, no script list or no matching script, `FUN_0055a470` still calls `FUN_0051c8d0` with `[0x0078578c]`
  (`Sound.Reverb`; `0x0055a477`, `0x0055a4c5`): the handoff is made every eighth step in every view, and only a script's
  negative `+0xd8` sends nothing (`0x0055a4b9`). `ParkLoop.HandTheReverb` counts `CAMERA_RIDE_REVERB` only in a ride's
  view. The one choice left: count the no-ride handoff too (rule 4: a reached, unbuilt call, every eighth step of every
  park), e.g. a second name for it beside the ride arm's, or write in `docs/exe/audio.md`, "The reverb", why it stays
  uncounted. Also from Q580 (2026-10-20, left by Q864): the Sun God reaches `SETREVERB` (`incagod` at 162 and 188, in every
  run), counted at the dispatch default as `Inca God: SETREVERB at 162`; building it is the store of its operand at the
  script's `+0xd8`, and with reverb cut a name of its own in the count is all it needs (`q580fs/run-S2`). Confirm with
  `endfeature make` and `unimplemented`, predicted first.

- [ ] **Q158. Four park-screen readouts are stand-ins; each is counted where it is drawn (Q865).** Found by the 2026-09-26 staleness
  audit (`AGENTS.md` rule 4). `ParkObjectWindow`'s scrap row letters the build price as money (row 449, "$ 500"), where `FUN_004e2400`
  scales it by the age bucket's percentage (`FUN_004e2290`: `percent x +0x1b8 / 100`, the original's "$ 250"), `SCRAP_VALUE_DEPRECIATION`, counted
  where something is sold, at the shop window's scrap row and at the object window's. The percentage is built since Q1052
  (`ParkScrap.Percent`, `ItemDescriptionFile.ScrapValueAt`: the item's four-year `ScrapValueYear1`..`4` picked by the age, 100 only under 30 days with no customer yet); the row
  and the sales that still counted `SCRAP_VALUE_DEPRECIATION` were Q1054's (built 2026-11-12; the photograph is Q1344's).
  (The object window's Age row is built: UITEXT 433, "{P8} days".) `ParkEntryPriceScreen`'s heading
  `0x4f3af` is empty, and its remark says "left empty and counted"; the original titles it with the name of the thing
  at world `+0x1da732`, `mParkGates` (`ParkWorld`), and its help row 191 reads "Park name - left-click to edit", so
  the title is the park's name and its edit is Q28's. The name is lettered by `UI_SetTitle` (`0x00485d20`, called at `0x00498e4c`): yellow, with the edge, in
  the stream's rect widened by half its width either side (749..1221 to 513..1457), where ours letters the empty heading white (Q676). `ParkGadget.ShowDate` prints the machine's short date (Q527). `ParkBuyScreen`'s description panel `0x1ea` draws its frame and the footprint picture, and neither the item's turning model nor its kind row `0x1ec`.
  **Counted since Q865** (each where it is drawn: `SCRAP_VALUE_DEPRECIATION` at the object window's scrap row, gone since Q1054,
  `ENTRY_PRICE_TITLE_PARK_NAME`, `GADGET_DATE_UITEXT_448`, `BUY_SCREEN_ITEM_MODEL` and `BUY_SCREEN_ITEM_KIND_ROW`, the last two for every row the panel is filled for, land and mystery rows too); **what is left is each build**,
  and a count goes when its build lands: scale the scrap row by `FUN_004e2400`; letter the
  park's name in the heading (`UI_SetTitle`); write the date from UITEXT 448; turn the item's model in the buy panel and write its kind row `0x1ec` (`ITEMTYPES.str`, item `+0x90`). Three rows are model-and-string pairs of their own (`FUN_004ab1b0`, `0x004ab55f`..`0x004ab654`, `FUN_00486410` then `FUN_00485b00`): a mystery ride builds item 100 and writes UITEXT `0x89` "??? Mystery Ride! ???"; Buy Land (row -1) builds item 101 and writes `0x86` "Buy Land"; Clear Land (row -2) builds item 102 and writes `0x87` "Clear Land".
  Confirm each: its name leaves `unimplemented` after opening a ride's window, the entry-price screen and the buy screen.
  From Q130e, seen beside the original's lists: the hire list's wages are bare numbers too ("$ 63" there); a buy
  row with no state shows an empty box there and nothing here; Buy Land's price is `[0x0078560c]`, 10 there and 0
  here; and the headings' lettering is larger there than the rows', here the same.
  From Q188: the buy screen's panel is the object window's preview (`ParkObjectPreview`, which wants a placed thing
  today: give it an item); in the original it is (437,162) 360 by 363. The footprint picture at its lower left is
  built (Q233b) and is drawn in front of the model; the kind row `0x1ec` above it is not built.
  - **Note (play test 2026-10-21, note 8):** the buy screens show no 3D preview, a black panel. Q158's panel is the
    object window's preview, (437,162) 360 by 363 (QUEUE:408-411); the object window's preview is built (Q188) but
    `ParkObjectPreview` wants a placed thing (QUEUE:409). Q450 (1) is the buy stats panel (QUEUE:2398-2402): settle
    there which of the two owns the buy screen's picture.
  - **Note (play test 2026-10-31):** the buy screens still show no model in the description panel. The model and the kind row are counted (Q865). Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.

- [ ] **Q210. Hoardings: the original's terrain rectangle and its base-vertex gate.** Found by the 2026-10-04 review
  (`u6-hoardings-1`, `-2`, `-4`; `docs/exe/ride-hoardings.md`). `ParkRideHoarding.Ground` samples `x0 + gx` where the
  original samples `x0 + trunc( gx * (w - 1) / w + 0.1 )` inside the model's inclusive rectangle (`+0xc0`, `+0xc4`,
  `FUN_00467030` `0x004670c1`, `FUN_00454190`); counted `HOARDING_TERRAIN_RECTANGLE`. `ParkObjects` gates on
  `Nodes.Count > 1` and reads `Meshes[0]` where the original counts base vertices (`FUN_00469a80`, `0x0046a838`) and
  reads the mesh with the most (`0x0046a82c`). Port both, and write the test's expected value from the executable's
  formula, not from the code. Confirm: a ride bought on sloping ground, closed, photographed with `hoardings`; then
  the same panels beside the original's under Proton (their height, which rises first, where the sign sits).
- [ ] **Q1035. The bolt's shape is the PC's own, and OpenTPW draws one quad.** From Q152's note (fork review 2, 2026-10-16, `verify-audio-lips-video.md` lead 3): the constructor (`0x0057d2ab`..`0x0057d2e9`) sets roughness 0.6, a stop test of 6 ± 2, a ribbon half-width of 1.3 /
  (depth + 1) in screen units and branch odds of 9 in 512; the midpoint step is `0x0057e4b0` (its offset is not
  perpendicular: it uses |mid|², and its cross term is × 0.0), the branch heading `0x0057e810`, (the colour pulse
  `0x0057ee30` is built, Q1044). `Lightning.cs` draws one quad 9 wide. Decode first
  (the shape against a run of the original's bolt, `record.sh`), then build the jagged ribbon and its branches.
  - **Note (Q1037, 2026-11-05):** measured on the desk, ours' column is a soft pale band, half-peak width 27 px of a 1280 x 720 frame with a white core 12..14 px wide (pixels whose smallest channel is over 215), reaching 255; the original's lobby bolt (`q1037/orig/run1/fr/f033.png`, 1024 x 768) is a jagged white ribbon with branches, core 4 px median over its rows, with a pale glow beside it. Both reach 255 and both go near-black on the wrapped frames. Not like for like: ours at the front end's player list with the strike 72 units off the camera's axis, the original at a picked player's island panel with the strike wherever it fell, and its film scaled from 1920 x 1440; the shape (one band against a branched ribbon) is the finding, the 3x width is not a target.
- [ ] **Q1050. The lobby's strike inverts the 3D scene for one frame, and ours does not (Q715's film, made; folds Q715; low priority: the lobby, Halloween's island only, not Lost Kingdom's park).** From Q1037 (`q1037/orig/run1`, `q1037/side-by-side-lobby-bolt.png`). The original's lobby, Lost Kingdom's island, filmed at 30 fps while its own roll struck (`tools/harness/original/lobbystrike.py`): 13 strikes in 26 s and 13 flash frames, each one frame after its strike's first read (`orig/run1/bolt.log` against `means.txt`; a strike inside a live bolt's 0.5 s flashes again, frame 36 inside the strike at 26, frame 42 inside the one at 36), and no other frame moves: the picture's mean rises 21..28 grey levels for one frame and falls back. The flash inverts the 3D scene (sky, sea, island, flyers, the key's sparkles) and not what is drawn after it: the bolt (white over the inverted scene, `orig/run1/fr/f036.png`) and the UI (the panel, the key, its text, the version). Ours' frame mean moves at most 1.4 across a strike: no inversion, and no `LOBBY_BOLT_FLASH` count either (`Unimplemented.Report`, rule 4: still unwritten, a gap since Q715). Build `Unimplemented.Report( "LOBBY_BOLT_FLASH" )` first, then the quad as `lobby.md`, "The lobby's bolt inverts the screen for one frame", and Q715's note read it (key 0.001, state `0x9a1`, after the queued entries of key above 0.001, before the bolts and rain, before the UI), once per lobby bolt call (`0x005e1100`, flag `0x0057dc70`; a park's bolt has none). Also drop ours' sun lift on a lobby strike (`LobbyWeather.UpdateSky`'s `sun.Color`, `Lightning.Flash`/`MaxFlash`): ours lifts the island 47.9 -> 59.1 grey, where the original's holds 66.1-66.4 across its live bolts (`orig/run1/fr`, `ours/run1/strike-01`, q1037). Not seen: the rain's place in the flash (none falls on Lost Kingdom's island) and the lobby's device word. Confirm as Q1037 did (`lib/lobbybolt.py`, `original/lobbystorm.sh`): `strike`, `step`, the whole-picture mean per frame, and a frame, beside `orig/run1`.
- [ ] **Q1045. The figure of a picked-up member at the pointer (`STAFF_PICKED_UP_PREVIEW`).** The type-6 mode's sprite (vtable `0x006fea70`;
  `docs/exe/hud.md`, "The person's windows"): the original hangs a figure of the picked-up member at the pointer (`q1032/orig/a-2-pickup.png`); ours
  counts `STAFF_PICKED_UP_PREVIEW` (ParkPeople.cs) and keeps the plain arrow. Counted since Q432, never queued. Decode first whether type 6 shares the carried
  figure's sprite path (`0x0046c480` / `0x0046c730`, Q40's fork-watch note H2, which is `STAFF_CARRY_PREVIEW`'s item) or has its own; fold into Q40 if it does.
  Listed in `docs/DOES-NOT.md` beside `STAFF_CARRY_PREVIEW`.
- [ ] **Q1046. The queue tool's unanchored click (Q860's second read).** In the original an unanchored click in mode 3 first asks
  `FUN_00535670( 3 )` of the cell (`0x00527297`..`0x005272a5`; anchors nothing when it answers 1, not decoded), then empties
  the pending list (`0x005272be` `FUN_0052fbd0`), pushes the click (`0x005272cd` `FUN_0052fbf0`), anchors, resets the click
  counter (`0x00527316`) and lays nothing. `ParkPathBuilding.RunQueue` has no such arm: `ParkBuildMode.Arm` sets the anchor to
  (-1,-1) but leaves the list, so the console's `tool queue` and EditQueue's EDIT_QUEUE_WITH_NO_ENTRANCE arm push the first
  click onto a stale list, and a later Backspace can pop into it and clear a line between unrelated cells. Decode
  `FUN_00535670( 3 )` at `0x00527297` first, then build the arm as RunPath's (lines ~900-910) and test it.
- [ ] **Q1047. The queue tool's third-click advisor `0xd6` (pre-existing).** The click counter `[0x8186dc]` is shared by modes 1 and 3
  (`0x0052732f`..`0x0052736d`): it counts every anchored click and posts advisor `0xd6` at the third; its only reset is the
  unanchored first click. `RunQueue` now counts `QUEUE_TOOL_ADVISOR_MESSAGE_0xD6` at the third click (rule 4); build the advisor
  post itself with the path tool's `PATH_TOOL_ADVISOR_MESSAGE_0xD6`, and the reset with the unanchored arm above. A refused run's click is not counted (the latch, `0x00524a63`; built). **Also a deviation:** ours zeroes the counter and the pending list at a park unload (`Level.ForgetPark` -> `ParkBuildMode.Forget()`); the original keeps both across a load (`q1038` run2 b3 3 to run3 c0 3, c5 6 to run4 d0 6), which moves the counted `0xd6` after a reload whose next arming is an edit.
- [ ] **Q1053. A sideshow's or feature's window, when built, must not offer the queue button (Q1038's tail).** The queue button is the ride window's (`FUN_004af200( 0 )`,
  `park-engine.md`, "Editing a queue"). Not checked in the original: the stock park holds no sideshow to open, and the windows are unbuilt (Q31b, Q960, Q961). When one is
  built, open a bought sideshow's window in the original and read whether it has the button.
- [ ] **Q1344. The scrap row's figure photographed beside the original's (Q1054's split piece).** Since Q1054 the object window
  (`ParkObjectWindow`, `0x3e23`) and the shop window (`ParkShopWindow`, `0xc07d`) letter `FUN_004e2400`, the build price at
  `ParkScrap.Percent`, and tests pin the stock Belly Bounce's "$ 500" and a new unused thing's "$ 1000". Not photographed: Q1054
  ran in a cloud container with no display to look at and no original. Open the same file's Mumbo window with 8 users and 19 days
  (the original's `Scrap value $ 500`, `q1052/orig/run3/21-window.png`) and with none and 4 days (`$ 1000`, `run4/09-window.png`)
  and photograph ours beside them; then a stock shop's window beside the original's.
- [ ] **Q1018. The original's bolt is updated a second time in some frames, 4 to 6 ms after the first.** Found by Q1044's poll (`q1044/orig/run1/a-load.log`, `run2/a-load.log`): of the poll lines, 197 (run 1) and 150 (run 2) show `FUN_0057ff60` called again with `dt` at `+0x9d9c` of 4 to 6 ms, after 997 and 567 of 25 ms or more: about one frame in five. Each call takes the clock's ms since the last call off every bolt, packs a fresh grey and builds the bolt (`0x0057ec50`). Not decoded: who calls it a second time (`0x00576dab`'s caller, or another), and whether the second call draws the bolt again, an additive second quad in the same frame that would make such a frame brighter than its one polled grey says. Ours draws one grey a frame and ages a bolt once. Decode first (`watch.sh` on `+0x9d9c`'s writers; `boltpoll.py`, `boltaim.py`), then film with `record.sh` and read whether a frame with a short second call is brighter.
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
  Q538): report on a change. Status 24 seen in the original (Q1104, `q1104/orig/b-2-window.png`): an open bought TV Sim whose exit's
  path stub joins nothing shows EXIT NOT CONNECTED over its preview; ours showed nothing for the same file (`q1104/run2/window-open.png`). Confirm: each status photographed beside the original's.
- [ ] **Q213. The status box's lettering is smaller than the original's.** Seen 2026-10-04 with the two side by side
  (Q208): the original's "LINE NOT CONNECTED" fills the striped box in two lines of large type, OpenTPW's is small.
  Decode the font and size `FUN_004ade40` draws with. Confirm: the box photographed beside the original's.
  - **Note (fork watch 2026-11-12, verify-ui-hud.md H3):** `FUN_004ade40` sets no font or colour. The label is made once by the window's
    vtable slot `+0x10` (ride `0x004ad720`, shop `0x004afaa0`, sideshow `0x004b0ed0`): a text control on the base's `+0x1c` (the
    ride build passes `0x3e25`), aligned (1, 1), font slot 8 (`0x00485a70( 8 )`, the list headings' slot), colour (0, 0, 0, 255),
    then hidden. Ours letters `0x3e25` in font 6, white (`ParkObjectWindow.cs`, said at the site): build it so, then the photograph.
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
- [ ] **Q217. Three leftovers among the park's people.** Found by the 2026-10-04 review (`u3-staff-paths-1`,
  `u4-gate-1`, `u2-arrivals-4`). Staff are kept on paths by a fence the original does not have; its own recovery is
  the no-links arm (`0x004f95c0`), built for staff (Q112): ask Alexah whether the fence stays. `ParkPeople` holds two
  versions of `FUN_004fa990`: `InsideGateCensus` reads the position's cell as the original does (bytes `+5`, `+7`),
  `OnACountingCell` reads the occupancy link first; make them one. The third, `ParkCatchUpOrderTests`' row (16, 1), was
  settled by Q150, which made that test the order's own.
  - **Piece taken (2026-11-12, lane c53):** the second (the two versions made one) and the third (settled by Q150; its
    test runs) are Q1332, done; the first, the fence, stays here.
  - **Needs Alexah:** does the staff's path fence stay, or does the original's no-links arm (`0x004f95c0`) alone keep
    staff on the paths?

- [ ] **Q1333. A position off the map reads another row's cell in the original (`COUNTING_CELL_OFF_MAP`).** From Q1332.
  `FUN_004fa990` packs the thing's bytes `+5` and `+7` as they are (`0x004fa994`..`0x004fa9b6`), so a person at x 128
  or more would read a cell of the next row; `PeepBehaviour.OnACountingCell` counts nobody there and counts the arm.
  Dead by content in the stock Lost Kingdom park (none in a 90 s off-screen run, Q1332): measure whether anything
  placed reaches it (the bus stop's approach is on the map) before building the wrapped read.

- [ ] **Q1240. The remeasure tail's `+0x68` exit against `ReopenAfterRemeasure` (piece (5) of Q218, which needs Ghidra).**
  `docs/exe/ride-operation.md` says the remeasure tail `FUN_004de1f0` leaves at once when `+0x68` is non-zero;
  `ParkRideOperation.ReopenAfterRemeasure` clears `AssignedStaff` whatever was decided and cites `0x004de48c`. Read the tail
  in Ghidra and make the two agree. Put the bug back for each test (`AGENTS.md` rule 6). No game run.

- [ ] **Q219. Light the models from the normals the file stores. Decode first.** Found by the 2026-10-04 effort
  audit (`r-f1`, `r-c2`). The original's model lighter `FUN_00574660` takes one normal per vertex-order entry from the
  table at mesh `+0x64` (stride 12, `+0x5e` entries); OpenTPW lights every placed model from smoothed normals it
  computes, and `ModelFile.ReadFaceNormals` reads only as many entries as the faces index. Count the table's entries
  across all 2,129 `.md2`, compare the stored normals with the computed ones, then read and use the stored ones. The
  FileFormats models page gets the count. Confirm: one ride and one shop photographed beside the original's under
  Proton, the same sun.
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
  - **Piece taken (2026-11-12, lane c2):** (1), (3) and (4) are Q1147, done; (2), (5), (6), (7) and (8) stay here.
  - **Note on (7) (fork watch 2026-10-18, `verify-world-a.md` H2; `park-engine.md`, "Which rounding is live is not
    settled"):** the cause of the 24-bit reading is not settled, and our own 53-bit reading stands beside it: a breakpoint
    at `0x0059e17b` in the running original read the control word `0x27f` on ticks 970 and 1125
    (`advisor-park.md`, "The product is a double of the float key"), while the game-clock accumulator is added at 24 bits
    (measured). DirectDraw's cooperative level is set with `0x851` (`0x00563914`) and `0x808` (`0x00563b92`), both with
    FPUSETUP and neither with FPUPRESERVE; the renderer's own 24-bit switch is `0x00591220` (a failed frame leaves it in
    force, `0x00577433`). Which one a given calculation sees is still to be read, in the original, at that calculation.
  - **Note on (7) (fork watch 2026-10-31, verify-world H2; `park-engine.md`, "Which rounding is live is not settled"):** the
    likely cause is now read. DX6.1's D3DIM (`0x56664f77`..`0x56664f9e`, in the disc's `Redist/DIRECTX/`) sets the control word to
    `0x7f` on a DP2 HAL device (class 2); Proton's ddraw and wined3d set nothing. The clock add runs inside the renderer's
    24-bit window (`0x00576fa7` to `0x00577310`) on any platform, so under Proton it is added at 24 bits (if the `0x100` view flag is set in a park frame) while a park tick reads `0x27f`: our two measurements, no failed frame needed. Prediction for the run: `original/brk.sh` on `0x004033c8`
    reads `0x7f` through `0x0057ff79` and `0x27f` through `0x0054f47f` (not run; whether the `0x100` view flag is set in a
    park frame is not read). Unproven: that the machine "the original" means ran class 2; the fork's "and any later runtime"; whether a HAL
    driver writes the word. **Alexah's call:** every `0x27f` we measured is a Proton fact, so the pinned 53-bit choices
    (`ride-operation.md` shop cost, `park-engine.md` conform) are faithful to Proton, not to DX6.1 on a DP2 driver. **Alexah (2026-11-02):** keep matching Proton's 53-bit; no code change.
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

- [ ] **Q1340. The deciding guest's happy jump, photographed.** Split from Q224, built in a cloud lane with no display
  (`docs/exe/ride-operation.md`, "Q224"). A guest at happiness 90 deciding in Lost Kingdom plays spot animation 5 for
  eleven sweeps (the log's `Person N: spot animation 5 from Deciding at tick T` then `spot animation over at tick
  T+11, back to Deciding`). Confirm on a desk: a screenshot of that guest's jump (set 12, both arms up), beside the
  original's at the same point; `happy 90` in the console makes one within a few seconds.
- [ ] **Q1288. A cell's litter drawn, placed and written (the rest of Q225).** From Q225, split in cloud lane c42.
  Guests drop litter into the running cells now (`ParkState.DropLitter`), and nothing draws it. Build, as `ride-operation.md`
  says in "A cell's litter" and "What it is drawn as": the sprite of `SPR_LI`'s set kind - 1 flat on the ground, depth tested
  and not written, never picked, at the place the reseed of the park's one generator with the cell id and its two draws give
  (and the reseed's effect on that generator, or the departure said at the site); a loaded file's littered cells drawn the
  same (their sprites are the file's sprite table's); and a park file written after a drop carrying the cell's litter and its
  sprite (the writer writes the cells' records, not the running litter). Counted until then: `LITTER_SPRITE_AND_RESEED` at
  each drop (`ParkState.DropLitter`), `SAVE_PARK_RUNNING_LITTER` at a save whose running litter differs from the file's
  (`Level.WritePark`). Confirm: the drawn litter photographed on the desk
  beside a frame the original draws of the same file (`q253/orig/sold-by-the-original.TPWS`, its four littered cells (41,21)
  and (39,22) to (39,24)), and a drop's sprite at the reseed's place. Not tested here: a park with two bins in reach (the
  nearest and the tie), which no Lost Kingdom file holds.
- [ ] **Q226. Nobody is sick.** Found by Q111, arm (b). Illness exactly 100 on a third of deciding turns: spot
  animation 7, litter type 7 on the cell, event `0x12`, sound `0xcc`, illness nought, and the turn ends. After Q225,
  which builds a cell's litter. Build it where `DECIDE_VOMIT` is counted. Confirm: `need vomit 100`, a guest sick
  within a few turns, `peeps` showing illness 0; a screenshot.
  - **Note (fork watch 2026-10-18, `verify-other-b.md` L2):** the sick are litter kind 7: record `0x0074f140`, region effect 2
    stamped first, then the sprite placed by the cell-id reseed and two draws (`ride-operation.md`, "A cell's litter"). The drop
    does nothing on a cell that already has litter, so a second sickness there leaves the first.
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
- [ ] **Q229. A member of staff put down with the pointer on them sets off on the next sweep, where the original's
  stands eleven (decoded in Q869).** Found by Q112's measurement of the original (`ride-operation.md`, "OpenTPW takes
  the arm"). The original's second 0 is most likely the hover's stop (by elimination; `FUN_00505f50` from `FUN_00486d90`;
  `ride-operation.md`, "The put-down itself differs"; inferred, Q229 measures it); ours (`Level.WorldHelpRow`) names
  them before the first sweep, idle from idle with the stamp nought, so the walk is not stopped. The hand-off of the draw's
  latch is `FUN_00540b30` (`0x00540b79`, `0x00540b7e`), reached through `0x00475430` at `0x0054e345`, read as `0x007b05ac`.
  **Measure first, in the original** (Q112's put-downs, `q112/orig/put1.log`, `put2.log`: the stock jungle park, the guard
  picked up from the staff window and put down on (42,24) (put 1) and (42,26) (put 2) with a real click, the pointer left where
  the click put it, said in the PREDICTION; ID his thing id, M = `*(u32*)(0x007cfb90 + 4*ID)`, mGameTick =
  `*(u32*)(*(u32*)0x0080239c + 0x1da70c)`).
  **Run A, the writer:** `BRK_COND='$ecx == M' original/brk.sh 0x0048720d,0x005054d0,0x004d6410 60 120` started before the click.
  `0x0048720d` is the hover's call of `FUN_00505f50` (a naming), `0x005054d0` every `SetState` (the first stack word its caller:
  `0x00505f9e` the hover, `0x00505f3a` the drop body, `0x004d672b`/`0x004d674d` the guard's own), `0x004d6410` his turn once a
  sweep. Predictions, written first: H-late, no `0x0048720d` hit between the drop and his first `0x004d6410` (T1), then
  `0x004d674d` at T1, a `0x0048720d` hit and `0x005054d0` from `0x00505f9e` at T1, walk at T1 + 11 (+ 12 on a multiple of four);
  H-rename, a `0x0048720d` + `0x00505f9e` pair at T0 < T1 and a second at T1 after `0x004d674d`; another writer, the `SetState( 0 )`
  at T1 with another first stack word. On put 2, thirty more hits through the walk: at the stop on (43,27), a `0x0048720d` hit then
  `0x00505f9e` is the hover, `0x004d672b` with no naming the walk's give-up (`FUN_004fa2a0` answers 2 on `+0x18c`) or a failed
  `FUN_004f9490`.
  **Run B, why** (`tpwmem.py` every 15 ms after Run A's gdb is detached, the same put-down): mGameTick; M `+0x19c`, `+0x200`,
  `+0x18c`, `+0x198`, cell bytes `+5`, `+7`; his sprite S = `*(u32*)(*(u32*)0x007b49f0 + 4*handle)`, handle = `*(u32*)(M + 0xc)`:
  S `+0x114`, `+0xc4`, `+0x7c`, `+0x08`, `+0xe0`; the latches `[0x008bd4fc]`, `[0x00877350]`, `[0x007b05a8]`, `[0x007b05ac]`; the
  hover's `[0x007c24f8]` (last named), `[0x007c24f4]` (category, 8 to 12 staff, 0 over nobody), `[0x007c24fc]` (pointer in the
  view) and `[0x007c24c8]` (must be 0). H-late predicts `[0x007b05ac]` != M until after T1's sweep; H-rename `[0x007b05ac]` = M and
  `[0x007c24f8]` = M before T1, then a poll after T1's decide with `[0x007b05ac]` != M or `[0x007c24f4]` = 0, then M again.
  **Control:** the same put-down with the pointer moved onto the panel at once (`[0x007c24fc]` cleared, `0x0048887b`): the hover
  predicts the walk on T1 with no second 0 and no stop on (43,27); another writer predicts the second 0 unchanged.
  **Hover rate:** `original/brk.sh 0x0048849f 40 10`, no condition, hits per mGameTick (the handler `0x004881a0` is registered at
  `0x0048a0d4`; "once a drawn frame" is unread).
  **The build waits on Run A:** under H-rename ours' early naming is already right and the difference is in the latch's continuity
  at the walk's start; under H-late build the first naming as measured. Confirm: `staff` after `putstaff` with the pointer
  moved onto the member (`docs/TOOLING.md`, "A member put down under the pointer"), `idleSince` and the sweep the walk
  starts beside the original's, predicted first; a screenshot.
  From Q133b: the original's handyman, put down two cells from a dirty toilet, read `0xa` on the tick after the
  put-down and the toilet's stamp was written again a tick later (1389, then 1390: `q133b/orig/a.log`), so the
  second decide runs for him too; from `0xa` the idle between stamps nought, and he does not stand eleven.
- [ ] **Q231. Under an open park screen: F1, the wheel and the release.** Found by Q115 (`park-engine.md`, "A park
  screen is open"); decoded in Q870. Build: (1) F1 let go with no modifier says the kept tutorial line again, screen
  or no screen: keep the last category-1 raise beside the slots (also when the gate refuses it), forget it when its id
  is withdrawn, and on F1 post it forced at `ContextHelpScore` (`advisor-park.md`, "F1: the kept tutorial line said
  again"); here only the buy screen's `0xbc`, the hire screen's `0xbd`, the tag's `0x11c` and the polled `0xb9` and
  `0x11a` are built, so F1 has those to repeat, and the kept record goes into the advisor's park-file record
  (`+0x16d0`, "The slots in a park file"). (2) The wheel over a park screen's rectangle is taken (`WindowStack`), a
  list or slider under it still scrolling; beside it the park still zooms (`hud.md`, "The wheel over a park screen").
  (3) An armed tool's click beside a screen is taken on the press here, where the original skips the press and
  commits on the release (`park-engine.md`, "A park screen is open", the pointer; `Level.KeptFromThePark`), which is
  `RunBuildMode`'s standing difference.
  Confirm, the original (Q930's `q930/orig.sh` way: `loadfile.sh` the stock easymode.TPWI, off-screen 1024 x 768):
  (a) `advisor.py` gains a `kept` argument printing the kept record (+0x16d0 thing, +0x16d8 line, +0x16dc message,
  +0x16e0 score, +0x16e4 mValid, +0x16e5 forced, +0x16e6 overridden; FUN_0059a940 0x0059a988, FUN_0059ab50
  0x0059ab9b..0x0059aba3); run `advisor.py PID SECS 0xbc kept` and `screens.py PID SECS` from before the first click;
  the Tutorial byte [0x0078d90d] read first and held at 1 (`bytehold.py`) if it is not. (b) `setcam.py PID 725,265 100
  0 off`, `camera.py PID` reads zoom [0x007909ec] = 100. (c) Open the buy screen (Buy, (113,590)): predict a slot with
  0xbc and the kept record message 0xbc, mValid 1, forced 0, overridden 0. (d) After 0xbc's record reads said 1 and its
  slot is gone, F1 (`orig.sh key F1`) with the pointer on the screen: predict kept score 1000, forced 1, overridden 1, a
  slot 0xbc at score 1000 and a new action start (he says it again). F1 again while that line plays settles
  `advisor-park.md`'s open "whether a slot is emptied when its line starts" (refused by test 4 if not). (e) The stats
  panel's pixel from `uitree.py PID 0x007c24c8` (the child with ours' 0x1ed's rectangle, 2048 x 1536 halved),
  `gmove.py` there, `wheel.sh PID -1 2`, `camera.py`: predict 100; `setcam` back to 100, a point right of the screen's
  root, `wheel.sh PID -1 1`: predict 110. (f) Escape or the cross closes it: predict mValid 0; F1 then: no slot, the
  kept record unchanged. A frame (`orig.sh shot`) at (c), (d), (e) and (f). Ours the same steps by `q870/run1.py`
  (TOOLING), the log's `Park advisor: post message=188` lines and `camera`'s zoom= beside them.
- [ ] **Q232. A list opens with no row selected, where the original's opens with one highlighted.** Found by Q117
  (`hud.md`, "A right click on a list"); decoded in Q871 (`hud.md`, "What moves the highlight after the fill's
  pick"). Build: (1) the add's pick (`UiList.PicksFirstRow`) on the visitors, buy and Load and Save Park lists, which
  count it (`LIST_FIRST_ROW_SELECTED`); the buy screen's `0x401` arm takes it, the others' handlers have none, so there
  it is the highlight alone. (2) After an opener's fill, a list the pointer rests on selects the row under it (`0x10001`, taken
  at the next pump, after the add's pick). The fill's first add hides the list's scroll bar and that re-runs the hover
  test with the list linked. A point over no row leaves the selection.
  (3) The cursor cluster's End, Home, Up and Down (`0x2300`, `0x2400`, `0x2600`, `0x2800`; not the keypad's), with no
  modifier, on each key down including the held key's auto-repeats (`0x1000a`; the release does nothing), go to the
  list that last took the accelerator. A list with flag `0x200` takes it on its own `0x15` at the pump after it is
  built and on `0x1a` when it becomes the last pressed control, but not while it holds the focus (`FUN_0065e640`). It
  keeps it while visible, ahead of the focus and the last pressed (`0x0066997e`..`0x006699eb`), until it is destroyed
  (`0x0065c325`). Home selects index 0, End the last row, Up the selected index less one, Down plus one
  (`FUN_00665b51`), through `FUN_00665739`: a negative index clears the selection (Up on the first row, or with none
  selected); an index past the end is the last row (Down on the last row changes nothing and posts nothing); a row
  outside the window becomes the top row, the top no more than count less visible rows; the scroll bar follows; the
  screen gets `0x401` when the selection changes and `0x405` when the top row moves. The visitors, all-staff,
  all-items and hire lists (`0x291`); not the buy (`0x91`) or Load/Save Park (`0x81`) lists. Until built, ours counts
  the pick at rest and the keys where they are reached (a counter added with the build). (4) The buy list's handed item (`TAG_BAR_BUY_ITEM_ROW`). Confirm: the visitors list opened by V under a
  pointer resting on row 3 beside the original's, then Down and Home, the highlighted row predicted first and read
  from memory (`+0x14c`); a screenshot each.
  From Q233b: the buy list now selects the row under a moving pointer (`UiList.SelectsUnderPointer`, the flag `0x80`). With
  no first row selected, the buy screen's panel stays empty until the pointer has been over a row, where the
  original's shows the top row half a second after the list fills.
  From Q495 (`hud.md`, "The buy screen's help rows"): the fill's pick is also the buy screen's help item, so in the
  original the first row's help row is posted as soon as the pointer comes onto the row area; ours posts nothing before
  a row is selected. After a tab's click the original's bar was hidden with the pointer on the tab (`q495/orig/`,
  O9), where ours keeps the tab's row: look at it with the pick.
  From Q356 (`hud.md`, "The first row is picked as a list fills"): the pick is built for the hire list
  (`UiList.PicksFirstRow`, its `0x401` posted and taken by the screen's `Update`); a list sets it once its screen's
  handler of `0x401` is read.
  From Q375 (`hud.md`, "The miscellaneous tab: a row an object, and each tab's sort"): the pick is built for the
  four all-items lists too (their handler has no `0x401` arm).

- [ ] **Q754. The lobby's build label letters one pixel right of the original's.** From the Q752 review (finding 2).
  GAME8.bf4's 'v' has left 0, and the original's "v 2.0" has its ink at 751 to 772 of the 800 x 600 picture
  (`q752/orig-a`), so its pen stands at 751, 1.3 px left of its box's 752.3 (1926 x 800 / 2048). Ours rounds the placed
  x (`UiText.cs:72`, `MathF.Round( placed.X )`), puts the pen at 752 and the ink at 752 to 773. Read the plain label's
  draw, `0x0048f830`'s rectangle (and `0x006ae550` if it is the text's own pen), to settle where the original puts the
  pen inside its box, then make ours do the same (or say at the site why not). `hud.md` 3922 calls a one-pixel
  difference "possible and unmeasured"; here it is measured. Compare under Proton at 800 x 600 and 1024 x 768 with the
  text "v 2.0" (`VersionLabel` letters this build's name, so also read a longer text's pen).
  - **Note (fork watch 2026-10-28, verify-ui-hud H3, verify-other O2):** two verifiers differ, both agree the quad's own rounding
    is not the cause (the box 752.34 rounds to 752 by `fistp`, `0x58A15F`, `0x58A1C6`: the same as ours) and the plain skin's left
    arm puts the pen at bitmap column 0 (`0x48F9A3`). (a) ui-hud: the clip-to-screen map `0x00582EB0` (scale `[+0x38]` = (W -
    0.999) / 2, offset `[+0x28]` = (W - 0.999) / 2 + 0.24975; `ClipToScreen_Fpu` applies it, `0x5796D7`..`0x5796EA`) puts a UI
    quad's left edge at p x (W - 0.999) / W + 0.24975 = 751.31 for p = 752 at W = 800, which a rasteriser with pixel centres at .5
    lights at 751 (the measurement) and D3D's integer centres at 752; not traced: that the sprite batch reaches `ClipToScreen`,
    and Proton's pixel-centre rule. Test: a label near the screen's LEFT edge should letter at p (or p + 1) where one at the right
    letters at p - 1. (b) other: the offset lies inside the bitmap (`0x48F830`), not read. `hud.md`'s "truncates the drawn
    position" is corrected: only the in-bitmap alignment truncates.
- [ ] **Q755. Home and keypad 5 in the original's first person (and its ride view) reach the camera's reset; ours counts
  `CAMERA_RESET_FIRST_PERSON` and moves nothing.** From the Q690 review (finding 1; `park-engine.md`, "The camera's
  reset"). By the listing the update answers the reset bit in every mode (`0x0042b3fc`, `0x0042b40c`, `0x0042b46c`) and
  the viewfinder layer runs the camera table on the key-up (`0x00488af8`), so a release in first person puts the viewer
  (`0x007908f0`/`f8`) at (475, 175), asked rotation 0, zoom 110 (the walk's speed is the zoom's `sqrt` term; leaving
  puts the orbit's saved point and yaw back but not the zoom, so the orbit returns at zoom 110). Measure it first
  under Proton (`campoi.py` in first person: the viewer, its heading and the zoom before and after a Home and a
  keypad 5 release, the walk's speed after, and the orbit's zoom on leaving), then build it in
  `ParkCamcorderCameraMode.Update` behind `ParkOrbitCameraMode.CameraTableOn`, replacing the count, and confirm both
  games. Settle the ride view's arm too (reached at `0x0042b40c`; what the player sees is unsettled: neither the view's
  entry nor its exit saves or restores `0x007908f0`, so by the listing the orbit would come back at (475, 175), and
  back in first person the ride under the moved point is looked up again, `0x0042a340`). The End's view, the only ride
  view this game reaches, takes keys in its own handlers (`0x0048a740`, `0x0048a970`): read them first.
- [ ] **Q756. The original's camera coast and turn ease run at about 3% speed behind every pausing window; ours run on
  `Time.Delta`.** From the Q692 review (finding 1; `park-engine.md`, "The game menu and the options screen hold the
  keys"). `ParkCamera_Update` (`0x0042b1c0`) steps the stopwatch's move plus 1 ms a frame (`inc eax`, `0x0042b2a6`), so
  behind the game menu, the options screen, a message box and the Save and Load screens (the pause stops the stopwatch)
  the original's pan coast and the drawn turn's ease crawl at 1 ms a frame, where ours (`ParkOrbitCameraMode`) keeps
  easing on `Time.Delta` and finished a coast behind the menu (Q692 `ours-b` U3: 341.6 to 351.3). Measure the original's
  coast and turn ease behind the game menu under Proton (`keylatch.py` and `setcam.py`: release a pan or a turn as the
  menu opens, read the point and yaw over a few seconds behind it and after the close), then decide whether to hold ours
  to the same crawl (a deviation said at the site if not) and build it.
- [ ] **Q761. Jurassic Tours' construction beside the original's (the ride kind of Q598).** From Q598: its scale keys
  are posed (`TourrideC`'s rocks from y 0.0028, `q598/run-ride-final`), but the original's frames were not taken: on a
  fresh Full Simulation park (`freshfs.sh`, `q709/gms-two.dat`, `q598/orig/ride.sh`) all ten spots tried were refused, red
  signs on the footprint, so the poll read only the carried preview. Find where the original lets it stand (the
  park's owned land, or a park file with the ride researched and land bought), then poll `modelnodes.py =tourride --axes
  --poll` from the placing click, film it, and check the readings with `scalecurve.py ... tourride C`.
- [ ] **Q764. The Splish Splash's hut (`wr_hut`), shown in one of our runs and not in another.** From Q763's runs. The
  same bought Splish Splash at (18,18), its loop closed by the same clicks, draws mesh 5 `wr_hut` south of its footprint
  while its channel 0 runs (`q763/probe3`: frame 1.9, bound Start 2, after a Dino Karts was opened) and hides it while the
  channel is held (`q763/probe`: frame 0.0, flags 0x2, the water ride alone). The original draws the hut over the
  station (`q587/orig/W-8-closed.png`). Read which clip shows and places `wr_hut` and what starts it, then compare its
  place and time with the original's frames.
- [ ] **Q569. Where the original puts down a worker held at a save: measure it in the original (decoded in Q874).** From
  the run 6 review of Q241j (VERIFY-3). Q874 read `0x0046cdc0` (`park-engine.md`, "The hand's ways out", "A worker held
  at a save"): the worker goes to the middle of their pickup cell (unless message 10 has taken them out of state 7 meanwhile, `park-engine.md`, "Open"), idle with `mTimeStartedIdling` 0, every kind but the
  mechanic (Q60), which is what ours writes (`ParkPeople.PutBack`), so nothing is to be built. Confirm in the original
  under Proton: a guard picked up from the staff window's PICK UP while walking, the park quicksaved with Alt+S while he is in the hand
  (`saves.md`, "The quicksave and quickload keys"; the menu cannot be opened with a worker held, since its Escape lets go
  first, `0x0040c35f`, so `savepark.sh` cannot be used), the file read back from the player's `jungle.TPWS` with
  `parkfile.py` (his `mX`, `mY` against his cell before the pickup, `mState` 0, `mTimeStartedIdling` 0, thing 1's
  `mStaffMemberPickedUp` 0), predicted first, against ours
  (`ParkSaveScreenTests.AWorkerInTheHandIsPutDownBeforeTheParkIsWritten`, `q874/run1`; ours' Alt+S reaches the same
  put-down: `Level.QuickSaveKey` -> `WritePark` -> `ParkHand.LetGo`). Recipe, lane A (the original under Proton,
  off-screen): (1) take the `save/` sum; move any `jungle.TPWS` out of the reference player's jungle folder, since Alt+S
  overwrites it; load the stock park (`loadfile.sh load q325/stock.TPWS`, as `q432/orig/run.sh`) and let it run a few
  seconds. (2) Open a guard's staff window by `q432/orig/run.sh`'s `window()` (Ctrl+S, the list, a row); thing 28,
  "Mike Man", is a guard who walks in the stock park; not the mechanic (26). (3) Predict and write down before reading:
  read his thing with `tpwmem.py` (`q432`'s `person()`: cell bytes `+5`/`+7`, words `+4`/`+6`, state `+0x19c`); the file
  should hold `mX` = cx*256+128, `mY` = cy*256+128, `mState` 0, `mTimeStartedIdling` 0, `mStaffMemberPickedUp` 0.
  (4) PICK UP (q432's point (308,385)); the window closes by itself; do not click the park and do not press Escape or
  Delete (a left click drops him on the hovered cell, `0x0046cc40`). Nothing may take the keys between PICK UP and
  Alt+S: no park screen, menu, message box, map or options screen. (5) While he is held, read twice a few seconds
  apart: `+0x19c` is 7 and `+4`/`+6` unchanged; `mStaffHQ`'s `+0x60` low word is his id; `hand.py` prints vtable
  `0x6fea70`. (6) Alt+S by `q545/orig/run.sh`'s `chord s` (Alt_L down, s down, s up, Alt_L up). (7) After: live, `+0x19c`
  0, the `+0x60` low word 0, `hand.py` vtable `0x6fea10`; the file, copy the player's `jungle.TPWS` out and read it with
  `parkfile.py` (TOOLING.md's Q874 recipe); compare with the prediction and with ours. (8) Optionally the mechanic (26):
  the file holds the state his decide chose, not 0, and the rides' claims are cleared (Q60). (9) Restore: take the
  quicksave out, put back any `jungle.TPWS` moved, and compare the `save/` sum.
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

- [ ] **Q270. The music's level and the gauge, set beside the original's.** From the week-end audit (stage 4) and Q148, which built
  both counts through `FUN_004fa990`'s filter (`ParkPeople.GateGuestCensus`; `Peep.CountsOn`) and saw them in ours alone (`q148/`: the
  stock park paused as it loads, then guests reach the path). What is left is the original's side: read `FUN_004c81e0`'s level and the
  gauge for the same file with guests standing on a queue cell and off one (the bus stop's type 30 included), in the original under
  Proton (`music.py` of `rv4/orig/` reads the voice), and compare the counts with ours (`music` prints the guests by cell type).
  If they agree, tick it; if not, the filter's cell read (`PeepBehaviour.OnACountingCell`, the position's cell, one rule since
  Q1332) is the first suspect.

- [ ] **Q1320. The scene's height on the gate's row, in the original.** Filed by Q271 (cloud lane c50, no
  Ghidra, no original). Ours now answers the gate on (46..49,17) and keeps the caller's height, counting
  `SCENE_HEIGHT_OWN_MESH` (the gate's model takes mode 4: no `0x2` record, no `0x80`, float nought). Not checked:
  that `0x00452ac8` tests the type byte the save's record word 3 is, and what the original answers there (mode 4
  reads the instance's `+0x2c` mesh, whose writer is not read). Confirm: the original under Proton, a height read
  under a point on (47,17) at a Ghidra breakpoint or by its caller, beside the console's `scene 475 175`.

- [ ] **Q1292. A mechanic arriving at a ride that waits for an upgrade does the upgrade's work (from Q272).** Q272 gave
  `StaffBehaviour.ArriveAtTheRide` the arrival's third test (`FUN_004da740`, `0x004da7ca`) and counts it,
  `MECHANIC_UPGRADE_ARRIVAL`, sending him to his decide. What state `0xd` counts down for an upgrade is read (`ride-operation.md`, "State `0xd`"):
  the descriptor's dword at `+0x1fc + 0x40 x` the ride's byte `+0x50` (`0x004da409`), times `WorkDuration`, not divided
  by 100. Name that descriptor field (0x3c into tier l + 1's record if the records start at `Upgrades[0].CostOfResearch`,
  `+0x180`) and read it in `ItemDescriptionFile`, then put him to work for it and let `FUN_004df8f0`'s
  upgrade arm finish it (`RIDE_UPGRADE_COMPLETION`); the search's own `MECHANIC_UPGRADE_JOB` belongs with it. Nothing
  here puts a ride in state 2 (the Buy Upgrades order is not built, and the stock park is Instant Action), so the path
  is reached only by a file that holds one. Confirm: a ride put to state 2 with a mechanic on his way, his
  `mDurationOfRepair` and the ride's level read in both games.

- [ ] **Q1268. A node's direction words compared with the original's (the rest of Q273).** From Q273, split in cloud lane
  c37 (no original there). A type 2 particle record's emitter words, written by ours and by the original from the same
  park at the same tick, compared bit for bit; and a node posed by a clip, which Q273's census (rest poses only) did not
  reach, checked for a word the earlier rule would have changed. Under 24-bit precision the sum and root would round
  otherwise: the precision in force at `0x00556cba` is not measured (`park-engine.md`, "Which rounding is live is not
  settled").

- [ ] **Q1272. A made guest's sprite `+0x7c` and `+0x80` beside the original's.** The rest of Q274: Q274 built the
  constructor's two words from the decode (`FUN_004758f0`, `0x004759c4` and `0x004759df`) and read them in a file this game
  wrote (`+0x7c` = `CLOK` + `0x3e`), but no original ran. Confirm: a guest the original makes and saves at once, its
  sprite's `+0x7c` less the file's `CLOK` reading (`parkfile.head`, `parkfile.clock`), beside ours' 62; and whether the
  original, loading our file, holds the made guest's first turn back one interval.

- [ ] **Q276. A rider's head is always drawn from their own bank.** From the week-end audit (stage 4; read twice
  from the listing). `FUN_004fcac0` gives the rider's own `mSpriteID` only where a costume bank (kind 3) is loaded
  (`FUN_00541f60( 3 )`, `0x004fcac6`); where none is, it stores the rider's id at the world's `+0x1da708` and
  answers a draw among the kind 1 banks (`0x004fcae5`..`0x004fcb04`). `ParkGuestSprites` and `ParkFileWriter` give
  the rider's own bank in every case. Confirm: a park with no costume bank loaded, a rider's head bank read in
  both games.

- [ ] **Q1276. Q277's shared track bit, in a running park and against the original's save.** Q277 made a clip's start
  bind only the new clip (`ParkModelTables.Running.Started`), checked by tests alone. Of the jungle items only the Inca
  Totem (1110), of four channels, has two clips that share some nodes and not others (`(5,0)` nodes 2, 5, 6; `(9,1)` nodes 5, 6, 8,
  10); bought in the stock park under a cloud lane it stayed on its build clip for 150 s (channels `0/0`, `12/0` x3),
  so the case was not reached; which channels its script puts those clips on was not read. Confirm: the totem operating with riders until one channel's clip is replaced while
  another's shares its nodes, saved, its node words read (`parkfile.models`) beside the original's save of the same.
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
  coaster pylons are drawn (Q594). Confirm by a pylon's turn beside the original's. (Q817 draws the pylons by taking each rotation key as the
  node's turn, which is the composition on the nodes the file leaves unturned, as `ParkCoasterPylon` does for the knot; the general order stays here.)
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


- [ ] **Q1316. The handyman loaded walking to litter or sweeping: his walk off photographed, and the job itself (from Q1154).**
  Q1154 made one loaded in state 8 or 9 fall to idle (`StaffBehaviour.Step`'s default arm, counted as
  `HANDYMAN_LITTER_STATE_8` and `_9`, a deviation said at the site) and confirmed it by census only, off-screen in a cloud
  container. Left: (1) a photograph of him walking off, on the desk: Alexah's `jungle/New Save.TPWS` (four such
  handymen) loaded, the `staff` census before and after a few seconds, one of them photographed as he sets off; (2) the
  original's own arms, which carry on with the walk or the sweep (`FUN_004d73c0`'s cases 8 and 9, `0x004d741f`,
  `0x004d742a`) from the saved `mTargetLitterCell` (+503, read by nothing yet), built with the litter search
  (`HANDYMAN_LITTER_SEARCH`), which they need. Read from the listing (run 22 review): the load keeps state 8/9, `+0x218`
  and `+0x214` (`FUN_004d6d60`); case 8, `FUN_004d7580`, tests the cell's saved `mLitterCollector` against his id
  (`0x004d75a5`) and decides at once when it is not his; case 9, `FUN_004d76a0`, waits `mTimeStartedCleaning +
  WorkDuration` (`0x004d76c9`), cleans his own cell's litter (`FUN_004d9620`, not read) and decides
  (`ride-operation.md`, the handyman's "Where OpenTPW differs"). Still to read: `FUN_004d9620`, and whether the litter
  search (`FUN_004c8ed0`) passes over a claimed cell, since falling to idle in state 8 leaves the cell's
  `LitterCollector` naming him.

- [ ] **Q287. ParkHireScreen.Selected previews a highlighted candidate's wage with ShowMoney( person.Wage ), and
  Update() calls ShowMoney() with no wage on every frame (WindowStack.OnUpdate).** So the staff-costs preview is
  overwritten a frame later and the player never sees it. ShowMoney also never takes the previewed wage off the
  balance cell, which its own parameter remark says the original does (message 0x401); known by reading lines
  351-417 and WindowStack.cs:330, not run, and no test covers the cell From the week-end audit (stage 6, a sweep
  of the source settled by a verifier; read from the code, not seen in the game).
  `OpenTPW/UI/Park/ParkHireScreen.cs`, line 417. Confirm: a candidate highlighted on the hire screen, the
  staff-costs figure photographed.

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
    of docs/QUEUE.md (a held copy) finds no line for separation or crowding.
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

- [ ] **Q1308. The queue remeasure's stamp in the original.** Left from Q290 (cloud lane c47, no Ghidra, no original).
  Whether a remeasure with no back cell takes a counter value is answered by the listing (no branch from `0x004de215` to
  `0x004de233`; `FUN_004d8c60` returns on a nought cell, `0x004d8c60`..`0x004d8c67`), as ours does
  (`AQueueWithNoBackCellTakesItsCounterAndStampsNothing`). Left: the item's own confirm, the back cell's block stamp read
  in the original under Proton after one queue cell laid on the Belly Bounce's back, (49,23) entered from (49,22), beside
  ours (`cell 49 23` prints `block stamp S of counter C`; ours read stamp 10723, 5 cells, back 2994). The stamp's value
  cannot match across the two games (the counter is a logical clock that each runs its own way); compare that it is the
  remeasure's own value, the newest counter at the lay, and lands in the back cell's block.

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
  - **Note (fork watch 2026-10-28, verify-hz-world M5):** the sign lettering rasteriser is `FUN_005eab70` (callers `0x5ecc49`,
    `0x5ecc64`): an 8-bit grey DIB of 2w x 2h (`biWidth = w << 1`, `biHeight = -2h`), the font from the record's LOGFONT
    (`+0x14c`), Uniscribe `ScriptStringAnalyse` with per-character widths x `+0x144` / 100 truncating; when the text is 2w wide or
    wider a binary search on `lfWidth` from (hi - 1) / 2 + 1 (hi = lfWidth, or -lfHeight when 0) until the bracket is under 3 and
    it fits; white on black at x = 256 - width / 2 (a hard-coded `0x100`), y = `+0x148`; then each mask texel is the sum of its 2
    x 2 samples >> 2 (truncating), then the first and last non-empty rows. Ours has the width percent and the `lfWidth` search;
    new: the 2x supersample, the truncating 2 x 2 average, the centring and the search bounds.
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
  - **Note (play test 2026-10-31):** Aztec Mayhem (`tvsim`) shows its screen's picture while idle; Alexah remembers a black screen until a go starts. Settle which texture frame the original shows at rest and while running. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.

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
  `RefusedSound`), not yet caught by the disk driver; `FUN_004af480` is not built; the Clone's refusal is built (Q672, `Level.ClickWorldAt`,
  the same `RefusedSound`), its sound asked for ("a voice", `q672/run5`), not caught by the disk driver either.
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

- [ ] **Q403. The balance's colour and the cost in hand.** From the fork review 2, 2026-10-16 (`verify-ui-hud.md` L5).
  The HUD letters the balance in one of six colours (table `0x00752e30`, index (balance + 999) / 1000 clamped to 0..5:
  red at nought or below, white over 4000), and control `0x30` shows the cost of the item in hand (`0x004a0d38`..`0x004a0d94`),
  only for mode type 3 (no mode object returns 4; fork watch 2026-10-31, `verify-other.md` M5), and 8 when `[0x0079c658] & 1` is set (-9999 hides it, -999 resets it). The keys counter is skipped in Instant Action,
  the tickets counter runs in every mode. Read what this game does first. Confirm: the balance at 500 and 5000 beside
  the original.
  - **Note (fork watch 2026-10-31, verify-other M5):** no mode object returns type 4: slot `+0x24` of the seven vtables
    `0x006fe980`..`0x006fead0` returns 7, 3, 1, 5, 6, 8, 9, so the test at `0x004a0ce4` is dead. -9999 is written at
    `0x00523c18` (Clear Area hover, mode `0x3a`) and `0x00523d68` (`0x36`); the tool start zeroes at `0x0052f087`. Bit 0 of
    `[0x0079c658]` is set only at `0x00441a5d` (a scan of the writes, context only). The price from the validator
    `0x00532303`: only the loop starts there, the store is `0x00532374`; the price itself is unproven.

- [ ] **Q419. The hire screen's candidate box is empty.** From the run review, 2026-10-16. On the same file the
  original's hire screen shows the chosen candidate's figure where this game's box is black (`q412/run-fix2/3-screen.png`
  beside `q412/orig/a-button.png`; the same in `q415`). The candidates' names differ too, which may be the pool's
  top-up (Q356's note); not looked into. Decode what the original draws there first. Confirm: the box beside the
  original's on the same file, a screenshot and the census of what is drawn.
  - **Note (fork watch 2026-10-16, `verify-audio-fx.md` O4):** the figure is made at `0x0049bb4a` to `0x0049bb64`:
    `0x00475a10( 0x0074f758, kind, bank, 1, x, y, z )`, the sprite kind by role from the table at `0x0049bb90` (roles 0
    to 4: 5, 6, 4, 7, 8), the bank the candidate's byte `+9`, set 1, the handle kept at `[0x007ca310]`, then
    `0x004755d0( handle, 8, 8.0f )`, register 8 the draw scale. The script is read (the note of 2026-11-12, below).
  - **Note (play test 2026-10-21, note 18):** the hire screen shows no staff picture. Figure made at `0x0049bb4a` to
    `0x0049bb64`, sprite script `0x0074f758`, frames 0 to 7 (QUEUE:1170-1173).
  - **Note (play test 2026-10-31):** the same missing figure is reported on the placing cursor (Q40); settle whether this box and Q31b's `STAFF_WINDOW_PREVIEW` share one decode. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
  - **Note (fork watch 2026-11-12, verify-ui-hud.md H1):** the script `0x0074f758` is SetLocal 16 = `0x51000`, SetSet 1, SetLocal 15 (the
    facing) = 5, Frame 0 to 7, Jump back to its start. `0x0049ba00` (one caller, `0x0049b97b`, in the hire handler) returns at
    once with no candidate, and remakes the figure only when the candidate's dword `+0` (the role) or byte `+9` differs from
    `[0x007523e8]`/`[0x007523ec]`; message `0x14` (`0x0049b7b4`) deletes it and sets both to -1. It stands on control `0x2484`'s
    rect: x = ( left + ( right − left ) / 2 − `0x3f8` ) / 1024, y = ( `0x300` − bottom + ( bottom − top ) / 4 ) / 768, z =
    1 − ( depth + 1 ) / 40 (`0x00476a70`); the sprite steps every 62 ms (`0x004759c4`).
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
- [ ] **Q455. A click on the tab already down lifts it.** From the week-end audit (D4, `VERIFY-15a.md` Q364 1).
  `WindowStack` flips the radio button up and `Put` returns before setting it down; the original's group with style
  0x10 sets it down again (`0x0066a455`..`0x0066a468`; frames `q364/orig/b-*.png`). `ParkLocator` already works round
  it. Read each screen group's style first.
- [ ] **Q1356. The entry price screen's disabled Loans button, photographed.** Split from Q456 (built in a cloud lane with no
  display). `ParkEntryPriceScreen` disables `0x4f3ac` in Instant Action, so it draws `b_loans`'s disabled frame. Confirm on a
  desk: `park jungle`, `screen 10` in an Instant Action player's park, a screenshot of the button beside the staff costs
  screen's (`screen 9`, `0x2dd`, already disabled there), and the same in a Full Simulation player's park, enabled.
- [ ] **Q1612. The all-items satisfaction bar past its ends, photographed.** Split from Q457 (built in a cloud lane with no
  display and no original). `ParkItemsScreen.SatisfactionBar` hands the bar `(value << 10) / 100` unmasked and
  `UiStatBar.Units` holds it to 0..0x400, so a satisfaction of -1 is an empty bar and 256 a full one (the masked rule drew
  them full and empty). Confirm on a desk: `parkfile.py`'s `ring_set` writes a shop's thirty satisfaction days to -51 and
  another's to 206 (fifty plus the mean: -1 and 256), and both games load the file, the all-items screen (`screen 5` in
  ours, the key `a` in the original) on the shops' tab: the log's `Items list: tab 1, row N ...` ends `-10` and `2621`,
  and each bar is photographed beside the original's.
- [ ] **Q458. The staff screen's meters average as the original's.** From the week-end audit (D7, `VERIFY-16b.md`
  Q369 1). The original averages each member's `ftol(h) & 0xff`: the kind's meter an integer mean then
  `(m << 10) / 100` (`0x004c9cc0`), the all-staff meter a float mean times 1024 times 0.01f (`0x004c7cc0`, `0x00496a7b`),
  in double (`advisor-park.md`, the FPU's precision). Ours scales the raw mean: 896 against 892 on `q369/skill.TPWS`.
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
- [ ] **Q1312. The gate's retry comes after the advisor's turn.** From Q462. `FUN_00516380` walks the things (the
  advisor, thing 3, among the last) and only then tries the delayed close (`0x0051643b`..`0x00516639`, every 30 ticks;
  `docs/exe/park-gate.md`, "The later close check"); `ParkPeople.Sweep` calls `RetryGateClose` before the advisor's
  `Tick`. No row the advisor reads is known to follow the gate's command on the same tick, so first find what (if
  anything) the order changes; then move it after his turn and before `FreeDeletedThings`, with a test whose values
  the old order gets wrong, or say at the call that nothing can tell the two apart.
- [ ] **Q1170. A purchase's advisor post, `0xc9` or `0xce`: decode which, then build.** From Q465, which counts it
  (`BUY_SCREEN_PURCHASE_ADVISOR_POST` in `ParkBuyScreen.Chose`, once a click that puts the item in the hand). The
  original posts `0xc9` where the item's `+0x4c` is nought and `FUN_004824d0( 0x80, 0 )` answers nought, else `0xce`
  (`0x004ac67e`..`0x004ac7d9`, read in the decompiler alone; `docs/exe/hud.md`, "The click"; `advisor-park.md`'s
  buy-screen section). The item's `+0x4c` is not named in `docs/exe/`. `FUN_004824d0` is the used-thing list's next
  link for a class mask (`hud.md`, "The staff and visitor locators" and the person's windows' `b_ardown`, masks `0x40`
  and 1), switching on its first argument (`0x004824d7`..`0x004824f2`, then `0x0048268f`..`0x004828a9`), so
  `( 0x80, 0 )` asks for the first thing of class `0x80` from the list's head; only its `0x80` case (`0x004827d5`) is
  not read. Rows `0xc9` and `0xce` (201 and 206) are category 1 (`advisor-park.md`, "F1: the kept tutorial line said
  again") and the mode setter withdraws both on any change of mode (`hud.md`, "The setter withdraws ten messages on any
  change of mode", `0x0052f245`..`0x0052f326`): build the withdrawal with the post. Decode the field, the `0x80` case and
  both rows' text in Ghidra, then post the row the original posts, and confirm with a purchase in both games.
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
  - **Note (fork watch 2026-10-28, verify-hz-other O1 (answers "which lobby states fog")):** all three island-camera classes fog
    50 to 250 at their start (`FUN_005d96a0( 0xff44ddff, 50.0, 250.0 )` straight after `FUN_005d9680`, `0x005e1421` in the base
    island camera `0x00702cb0`, `0x005e199a` in the derived island lobby `0x00702ec0`, `0x005e24bc` in the sibling `0x00702dd0`,
    whose screen is lobby.md's open question); `Lobby_Start`, the globe page's state, turns the fog OFF (`FUN_005d9690`,
    `0x005dd04c`); the lobby camera's init `FUN_005d8b50` leaves it off (50 / 300 with bit `0x2000` clear, saved and restored at
    `0x005d8da8`..`0x005d8dca`). So wherever fog is on it runs 50 to 250; the fork's 300 exists only with the bit clear. The
    remaining work is the photograph and the build.
- [ ] **Q503. The build markers load from `textures` at every texture quality.** The original's `FUN_0053d790` calls
  `FUN_0054dcc0` (0x0053d7b4) and takes `STexture` (`0x00763ee4`) for the three dynamic meshes where it answers 1
  (texture quality 0 or 1), as `ParkSea` and `ParkFlags` already do through `ParkSea.SmallTextures`. `ParkBuildMarkers.Load`
  always reads `generic/dynamic/textures/{name}.wct`; `blue.wct` (493 against 211 bytes) and `red.wct` (340 against 122)
  differ between the folders, `m_link` and `m_end` do not. Pass the quality in, load `stexture` at 0 and 1, and confirm
  it in the running game at low detail (a screenshot of a build square, before and after). Said at the site in
  `ParkBuildMarkers.MarkerNames`.
  - **Note (fork watch 2026-10-31, verify-other M8):** `orange.wct` is in `textures` and `stexture` of all four levels'
    `dynamic.wad`; `data/generic/dynamic` has none, so the markers must load from the level's. `0x0053567a`..`0x00535681`
    returns 2 for `0x1f` (slot 2 is `orange`); the call with `0x11f` is `0x00524234`/`0x00524453`. The face's path through
    `0x00532fc0` is not traced.
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
- [ ] **Q1232. What Q509 left: the stop-all's 90, the boot's sound detail, the fade heard beside the original.** Split from Q509 (cloud lane c28,
  2026-11-12). (1) The stop-all at a level's end (`0x0051bcb0`, handed 90, `Level.StopAllSeconds` 0.09 s): trace whether the 90 is the same kind of
  percentage before changing it. (2) At boot the library's detail is `DEFAULTQUALITY`, or half of it (25) at graphics detail 0 (`0x0054de0a`..`0x0054de22`),
  so a low-detail start has fading off until the options' tick; whether a saved `SoundQuality` is applied at boot was not traced. Ours starts with fading on
  and changes it only at the options' tick. (3) Whether `0x006bb860`'s mask bit 1 (gain[0], perhaps a master level) is set for any category: ours folds in
  only the group's level and the duck. (4) Confirm by ear and capture: the advisor quietened (options screen) and the bumper cars' unload, the mix captured
  at detail 50 and at 30 beside the original at the same detail; Q509's run read the census only (`voices`, `quieten`, `sounddetail`), no sound was heard.
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
  - **Note (fork watch 2026-10-27, verify-other L1):** on the per-frame path (`FUN_0056bb00`) the park view's far plane is the farthest of
    the four view rays' ground points from the camera, at least 10.0 (`0x0056bc5c`..`0x0056bc9f`, handed to `FUN_00578ca0` at
    `0x0056bd3e`), each ray's parameter capped at `[0x008bd4c0]` / 2 (`FUN_0056b940`). At zoom 300 a capped corner point leaves the
    ground and the hull shrinks, so the original may show black at the frame's top: predict it before pressing the zoom
    (arithmetic, not seen).
- [ ] **Q720. Do the keypad digits reach the cheat and camera rows? (one key press in the original)** From the fork watch,
  2026-10-20 (`verify-other-ui-audio.md`, "Miss found here"; `park-engine.md`, "The cheat table"). Both key edges
  (`0x0046b9c0`..`0x0046b9f5`, `0x0046bab8`..`0x0046baeb`) replace the vk by `MapVirtualKeyA( vk, 2 )` when it answers
  non-zero, excluding only the keypad's `* + - . /`; the digits `0x60`-`0x69` are not excluded. If the call answers `'1'`..`'9'`
  for `VK_NUMPAD1`..`9`, every row keyed `0x61`-`0x69` is unreachable (cheat rows 3-11, the Local 1-5 and Global 0-3 ticket
  cheats; camera rows 13-22, the keypad pan) and Alt+Num1 fires the ten-thousand cheat. If it answers 0 the rows work as the
  tables say. Confirm: in the original under Proton, in a park, NumLock on, hold keypad 4 (or 6) and see whether the camera
  pans; with `Easymode.TPWI`'s cheat flag possibly cleared by the first key (the same section), press a harmless first key, then
  Alt+Num1 and read the money. (The camera's keypad 4, 6, 2, 8 and 5 are reached under Proton: Q625, Q690.) Then correct the keypad rows in `park-engine.md` (the camera's pan keys, near line 394, and the cheat table) and OpenTPW's keypad bindings if they differ.
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
  - **Note (play test 2026-10-31):** Belly Bounce riders stand still and never jump (`RIDER_BOUNCE` reached). Q22, an older copy of this item, is folded into it (Alexah, 2026-10-31); its notes on the posed node and Q585 are in its finished entry. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
  - **Note (Q1005, 2026-11-04; run 21 review):** the sale's put-off sounds at the lifted height too: one rider's `+0x8c` ran 8.49 to 24.0 over 200 frames (six peaks 23.99 to 24.00, troughs 8.49 to 10.73: the node's own y as the floor; `q1005/orig/l-brk.log`, `riderheight.sh`), on top of a moving node (x 522.33 to 522.84, z 248.13 to 248.81), and a sale handed (522, 19, 248) and (522, 10, 248); ours' `ParkPeople.SeatOn` takes the placed node (10.3, 9.5, 11.9 for ever), so the lift is wanted in `SeatOn` as well as in the draw. Q1006, the same question, is folded in here.
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
  - **Note (play test 2026-10-31):** the upgrade screen is missed (`RIDE_WINDOW_UPGRADES` reached). The log also says `Could not load texture 'ui/textures/b_callmechg.wct'` and `'b_upgradeg.wct'` (the greyed buttons, lines 327-328); check whether those exist in the game's data. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
- [ ] **Q513. The window a click on an add-on opens.** **Answered by Q1000 (2026-11-04): there is none to open.** In the original a click on a Lost Kingdom
  add-on's body cell does nothing (the hover gives its cell no category, `AddOn.UpgradesId` non-zero and `AddOn.UpgradeType` 1) and a click on its track
  cell arms the track tool; the world click hands `Thing_OpenWindow` no type-1 add-on (a type-2 one opens `[0x007cc3e0]`'s window, `0x004b6ae0`, and
  returns before the `+0x30` test at `0x00486978`; no Lost Kingdom item is type 2). The `+0x30` arm, which opens the
  add-on's ride's window, is reached from the all-items list's add-on row (`0x004954ca`), which ours built in Q1041 (measured in both games; `docs/exe/park-engine.md`, "An add-on has no window and no sale of its own"). Ours opened a ride window for the Tunnel and now opens nothing
  (`ParkObjects.OpensNoWindow`, `ParkAddOnClickTests`). What is left of this item is the other themes' type-2 add-on (space's `zob2`, 3502, on 3109):
  its click would open the window of `[0x007cc3e0]` (`0x004b6ae0`), not read here; ours counts it, `ADD_ON_UPGRADE_TYPE_2_WINDOW` (Q1041).
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
  - **Note (fork watch 2026-10-28, verify-ui-hud H1):** the letters are answered (`ui.md`, "The two-pass text class"): a text
    control's bitmap is made in the texture cache's own format (`0x4925D0` copies the record `0x8BD558 + 32 x [0x8BD554]`, the
    shipped game's A4R4G4B4; `0x588940` allocates it), and the letters pass through the blitter `0x6B21F0` (picked by `0x6B4AA0`
    from the target's pixel format `[0x00FA76E8]`; its A8R8G8B8 twin is `0x6B24B0`): the coverage nibble is stored as the alpha
    nibble (lossless: the glyph's coverage is already 4-bit), the glyph colour is cut to its top nibble a channel
    (`0x6B2266`..`0x6B2289`), nothing is written at coverage 0, and no read of the target. So the letters are a texture of the
    cache, cut to A4R4G4B4. The title class's grey copy is nibble 8 (0.533 x the control's colour, not 0.5) at alpha 17 x floor( c
    x 128 / 255 ), and the string's texels replace the copy's wherever c > 0 where `UiText` composites Over: build both with this
    item. The loading screen and the pointer are not answered here.
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
  next change (filmed, `q528/orig/e-rain-film.png`), and its voice's handle is the saving process's. A park with no file stands as the constructor leaves it (Q803). A file at tick
  nought, as Restart Park reads, holds a nought stamp, so its load forecasts on sweep 1 where this game's applies a roll
  (`docs/exe/weather.md`, "A park's first days"), and the instants move a fresh park's first delivery from sweep 93 to the original's 94. Confirm: q528's `run3/q528-fix2.TPWS`
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
- [ ] **Q1364. Q535's empty-hand latch, watched in the original.** From Q535 (built in a cloud lane with no original).
  On the stock park, bank held at 19 (`thingpoke.py`, `TOOLING.md`, "Recipes", Q542), click bare (10,10) with the hand
  empty and read `[0x00816d50]` at `q532/orig/a-run.out`'s addresses: predicted 1; at 20, predicted 0 after the anchor.
  Then open the mechanics' locator: predicted texture 1 after the refusal (red edges), beside OpenTPW's `strip`
  `| patrol 48` markers and a screenshot of each.
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
  that display: `FUN_004a0ab0` reads the total (`0x004a0d38`) into control `0x30`, for interaction-mode type 3 (no mode object returns 4, so the test for 4 is dead; fork watch 2026-10-31, `verify-other.md` M5)
  (8 only when `[0x0079c658] & 1`), -999 skipping the show and -9999 (the patrol tool's `FUN_0052fb90`, and the Clear Area hover, `0x00523c18`) hiding it
  (`0x004a0d68`..`0x004a0d74`); carrying a mystery ride sets it to nought (`0x0052360a`), so the original shows "$ 0"
  (`q709/orig/a-place.png`; Q709 built the carry's skipped money test and not this display); the build tools are type 3 (`park-engine.md`, "The build-tool interaction mode is
  object type 3"). Build it with Q403 or after it; what is left to read is how often message `0x1e` comes against the pass.
  From Q542: the total is `DAT_0081af4c` (`FUN_0052fba0` adds, `FUN_0052fbc0` reads); the path and queue passes never
  zero it, `FUN_004a0db0`'s message `0x1e` does (`0x004a0dee`), calling `FUN_004a0ab0` first on every third one (the
  counter `[0x007cb2e0]`, reset at `0x004a0de2`, which message `0x15` also zeroes, `0x004a0e02`). A red square's second
  verdict adds to it (`park-engine.md`, "The path tool"), so the label may show more than the click charges: read it
  beside `strip`'s `run total`.
  - **Note (fork watch 2026-10-31, verify-other M5):** the display's mode test is for type 3 alone (no mode object returns
    4); see Q403's note. Buy Land (`0x004ac851`, `0x0046c580`) is type `0x39`.
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
  - **Note (play test 2026-10-31):** the advisor still says no opening lines in a fresh Full Simulation park: polls from tick 1 log `result=no-score`, row 281 plays at tick 612. Nothing counts the missing welcome; add the count with the fix. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
  - **Note (run 22 review, 2026-11-12):** the poster is **event 0** (a type-0x13 event, id 0), handled at `0x0059ae43`: message 0, then 0x143 when
    `[0x00fb3b7c] == 2`. It is sent at the end of the world's setup, FUN_005156a0 (`0x00515d3e`, dispatched at once by `0x00515da4`),
    on every park start, Full Simulation included, before the save's load; and again after event 10 on an Instant Action
    player's first park (`0x0054f2d7`/`0x0054f33b`). Both sends are counted now as `PARK_ADVISOR_EVENT_0_NOT_BUILT`
    (`Level.Advise`, `ParkAdvisorMessages.FirstPark`). Still to read: whether the save's load replaces the setup's post in
    the slots, what 0x143 says, and whether a duplicate post of message 0 is refused (`advisor-park.md`, "Still unknown").
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
- [ ] **Q1220. The swear filter's byte beside the original's.** From Q731 (cloud lane c25, no Ghidra); the rule and the
  short read were settled from the listing by the run 22 review (`docs/exe/saves.md`, the `+0x25` row). Confirm gms.dat
  offset 0x13 after a park save equals the original's on the same folder, a disc copy (0) and an installed one (1, after
  Q733's overlay), with a byte read as 0 among them.
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
- [ ] **Q1182. Q735's saved walk riders beside the original's, and a hidden one in a running park.** From Q735 (cloud
  lane c12, 2026-11-12, no original and no Ghidra there). Ours now writes a walk's shown rider at the placer's point, its
  height less the ground and the slot's facing, and a hidden one with no sprite (`ParkPeople.Written`, `docs/exe/saves.md`,
  "A walk's rider is saved where its placer left them"); seen off-screen on a bought Mayan Spinner (18 riders carried
  under action 6, 18 of 18 facing the slot, each at the console's `WALK at` point) and the Jungle Spray (one walking off,
  on the leg), and by a test for the hidden one. Not done: (a) the original's sprite table on a park holding Spinner and
  Spray riders, set beside ours' (`walkriders` reads both); (b) a hidden rider (action 1 or 4: the Totem, the TV Sim) in a
  park ours saved: guests admitted and sent to a bought TV Sim (`buy 1104 44 25 180`, queue (43,27) to (43,28)) turned
  for the exit before boarding (`HEADING_FOR_EXIT_CHANGE_OF_MIND` counted 274 times), so none was saved hidden.
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
  - **Note (fork watch 2026-11-12, verify-ui-hud.md M2):** the table `0x00752ed0` is (-2, -6, 2, 2) twice, (2, 6, -2, -2) twice, then the
    end word -9997, a loop: the banner swells 2 left, 6 up, 2 right and 2 down over two steps and back over two, 35 ms a
    step, started at `0x004a274e` after the pivot `0x0065f345( right / 2, bottom / 2 )` (`0x004a2740`).
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
  the exit cell's centre (until Q873; now the item's point in it, so re-take these): (525, 305) at the Spray, (435, 305) at the Kiosk, against the original's (525, 300) and
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
  texture again whenever a path changes: now written in place, Q829), and cut it where it is cheap: `Lay` per tile in parallel
  (it is already, 730 ms whole at 32, Q829's `base/run.log`). Confirm with the `[load]` step times before and after, and the frame rate at
  32 beside 16 (not yet measured).
- [ ] **Q660. Does the original free or hide a boundary record no anchor claims? (from Q620.)** At load the original clears
  each anchor's saved handle and re-claims its piece by matching item, cell and angle (`FUN_0053b1a0` through
  `FUN_00522f20( ..., 1 )` and `FUN_00463ab0`, which marks a match `0x10000`); `ParkBoundaryHoarding` draws every
  173xx record without that claim. No park file to hand tells them apart (933 anchors in seven files, every one has
  its record, no record lacks an anchor), but Q783's fresh-park writer and any edited file could. Read the users of
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
- [ ] **Q745. A start of a role the model lacks does not stop the drawn channel, and several starts between two poses are drawn as the last one. (from the run 11 review of Q658.)**
  The drawn side (`RideAnimations.PoseChannel`, `PoseOnto`; `StartOn`) sees only `BoundOn( index )` at pose time, so two
  things the original does at each start are not built. (1) The role-12 stop: `FUN_00472f60` on a role the model lacks
  restores the outgoing clip at the stop unless its flags carry `0xc` (`0x00473121`..`0x00473139`) and parks the channel
  at role 12, so the next start finds outgoing role 12 and restores nothing (`0x00472fbb`). Here `StartOn` binds nothing
  for a null clip and `PoseOnto` skips the idle channel: nothing is put back at the stop, the last pose stays drawn, and
  the next start rests by its own flags. (2) Several starts between two poses collapse into the last: a script's start
  (flags 0) followed in one `Advance` by the clip's loop restart (bound `0x9`, only when the clip is shorter than the time
  since the trigger) loses that start's rest and hide; a start followed by a promotion rests the clip before the first
  start straight into the promoted one. Both are said at `PoseChannel`. Reach in Lost Kingdom is unproven: park.md:326
  counts eight shipped references to a lacking role and does not say which; no collapsed case was found.
  **Find a reach first** (which of the eight references, which model; a clip shorter than a script's wait); where one
  exists, record the stop as a bind with a null clip so the target rests at the stop by the stop's flags, and queue the
  binds per channel and replay them in `PoseChannel`. If none is reached, close this item with the reason.
- [ ] **Q746. Ours' queues lose more happiness than the original's. Measure first which state's loss differs. (from the run 11 review of Q668.)**
  From a crowd (`q416/guard.TPWS`, 2420 sweeps) ours' queuers lose about -344 happiness against the original's about -195:
  ours `InQueue` -280, with `SteppingUpQueue` -17 and the spot animations entered from `InQueue` -47 (`q668/run2.out`
  LEDGER); the original's queue states 0xb, 0xb>0x6, 0xc, 0xb>0x8 and the rest sum to about -195 (`q668/orig/b-load.log`
  LEDGER). Nothing is built on this: measure the queue turn's docks in both games (`original/leavers.py`; `Peep.HappinessLedger`,
  the console's `happiness`) by the state at the write, find which state's loss differs, and read that state's listing
  before changing anything. Confirm: the two ledgers' queue rows side by side, same file, same sweeps.
- [ ] **Q662. Run the original on the made file for the advisor's row 257 (from Q713; beside Q743's file B).** The docs now
  say the original's tick 19040, sample 525 and props (8, 0) are from the unmodified `New Save` (`q707/orig/O2`); the
  original has never loaded `files/outlets-D.TPWS` (sideshow 50's thirty satisfaction days made 40). Load it in the
  original with `loadfile.sh` and read row 257's record beside file B (row 248). Also read which sideshow the
  original's row 257 picked on the unmodified file and its figure (the docs say only "some sideshow at 77 or more").
  - **Note (Q743):** on file B the original slotted `0x101` on 19040 at 72 (a sideshow at 88 worked back by 70 + 72 / 4,
    not read; which one was not read) and had not said it by 19062: `0x13a` was said then and the 19100 awards replaced
    the slot. It slotted `0x101` again at 40 on 19350 and it was still unsaid at the last advisor read, 19389
    (`q743/orig/O/load.log`).
  - **Note (Q609):** on the unmodified `New Save` this game's row 257 scores 24 on 19040 where the original's says sample
    525, so this game's advisor is free for the hint `0x13a` on 19059 and row 19's line (said by the original on 19084)
    waits behind it and the 19100 awards to 22142 (`q801/run3`). Which sideshow and figure the original's row 257 read
    there decides it.
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

- [ ] **Q760. ALT-V in a park shows "Ver 2.0", as the original's (decode first).** From Q752. The key-table row at
  `0x00748140` (id 14, key 0x56 'V', modifier word 0x30, ALT) runs `0x0040c820`, which calls `0x00481b70`, a jump into
  undisassembled code at `0x0048f090` that formats `L"Ver %d.%d"` (2, 0) (`boot.md`, "Which build this is"). Read where
  and how long it shows, in what font and colour, then build it with this build's name (`LaunchReport.BuildName`, as the
  lobby's `VersionLabel`), the deviation said at the site; confirm beside the original under Proton.
- [ ] **Q769. The lobby is lit by the original's camera-following light and ambient, not a fixed sun and a flat 0.4 (decode done;
  confirm beside the original).** From the fork watch 2026-10-28 (verify-hz-world H1 and C1, verify-hz-advisor A1; `ui.md`, "The
  lobby's light"). The original lights the lobby's models by one directional light, `FUN_005d92e0` (list 7, the park sun's list,
  colour byte x 1/255), and the ambient `FUN_005d9230`: on an island page ambient 0x444444 and light 0xeeeeee, on the globe page
  0x202020 and white. Every pass `FUN_005d8de0` aims it from P = eye + right x 8 + up x 2 (globe: right -300, up +300) toward the
  camera's target, flattened (y 0) and then y -0.3 on an island page, not flattened on the globe's. The advisor is not lit by it:
  his draw carries `0x2000000`, which skips list 7 (`0x0057aa96`), so ours stands. Ours lights the lobby by `Level.SunLight` at a
  fixed (500, -3500, 2800), said there to be a choice, and by the shader's flat 0.4 ambient (`content/shaders/test.shader`,
  `g_flAmbient` 0), which was unmarked and now carries a deviation comment. Build the original's: the ambient colour and one
  direction per page, lit per vertex as a park is (`park-engine.md`, "The lighting model"), the direction rebuilt each frame from
  the lobby camera's eye, target, right and up. Do Q681's measure first, so the light is judged on the file's own normals. Confirm
  beside the original under Proton: one island, the camera frozen (`X` or `freeze`) at two orbit angles, a lit face's colour
  against the original's; predict it first (ambient 68/255 plus 0xee/255 x max( 0, -d.n ) before the texel), and put the bug back.
- [ ] **Q771. A text control's bitmap is its box, and the original cuts the text at it; ours never cuts.** From the fork watch
  2026-10-28 (verify-ui-hud H2, verify-other O2; `ui.md`, "The two-pass text class"). Every text control of the original makes its
  bitmap once (guard `+0x44`) at its box's size: width (x1 - x0) x `[0xFAA5C4]` / 2048 toward nought (`0x48F47B`..`0x48F49E`,
  `+0x3C`), height (y1 - y0) x `[0xFAA5C0]` / 1536 toward nought (`0x48F4A2`..`0x48F4C5`, `+0x3E`), allocated by slot `+0x28` =
  `0x48F3D0` (`0x588940`, texels of the cache's format) in the plain class (vtables `0x6FF320`, `0x6FF500`) and the title class
  (`0x6FF530`). The glyph blitter `0x6B4AA0` clips each glyph against that bitmap, so text past the box is cut: a line wider than
  its box loses its end, a block taller than it its lower lines; right alignment's bound is the bitmap's width (`0x48F8BA`),
  centre's half of it (`0x48F943`), left 0; the title class's grey copy, moved (-2, +2) inside the same bitmap, loses its two left
  columns on a left-aligned string. Ours (`UiText.Build`) sizes its texture to the text plus its margin, so it never cuts: it
  differs wherever a string is longer than its box (a longer build name in `VersionLabel`, a long player or park name, a
  translated string). Count first over Lost Kingdom's screens which controls' text the original does cut (the box tree against the
  strings' widths, `uitree.py`), then size `UiText`'s texture to the box by the same two divisions and clip at its edge, the grey
  copy inside it. Confirm beside the original under Proton: one control whose string is wider than its box, predicting the last
  visible column, and the bug put back.
- [ ] **Q772. A toggle that is on shows its op 0x12 help row, and the new player dialog's chosen toggle then shows none.** From
  the fork watch 2026-10-28 (verify-hz-ui-hud L3; static read, never seen on screen: no frame of the dialog exists). The button
  class's help getter `0x00669410` answers `+0x94` (op 0x12) when its state byte `+0x138` is exactly 1 (on, not held) and `+0x90`
  (op 0x11) otherwise (Q684's note has the chain). The new player dialog's tree `0x753f50` gives every control op 0x12 -1 and the
  two mode toggles (`0x70d` Instant Action, `0x70e` Full Simulation, flags `0x11`) op 0x11 358 and 359, so the original's help bar
  shows 358 or 359 over the toggle that is OFF (and while one is held down) and the empty text over the chosen one. Ours
  (`NewPlayerDialog.cs`, `HelpText` 358 and 359 on both) shows the row on either, now marked at the site. Build: the help row of
  the chosen toggle empty. Confirm: first shoot the original's dialog with the pointer on each toggle in each mode (the help bar's
  text), predicting the chosen one empty; then ours the same, the bug put back.
- [ ] **Q951. A sold or moved coaster's pylons stay standing.** From an outside review of `1efacc58` (Alexah, 2026-10-30;
  source-established, checked by the Sentinel against `c67821bf`). `ParkCoasterPylons` (Q817) builds its pylon entities once, when a
  park loads (`Level.cs:473`), and takes them down only on its own `OnDelete` at unload (`TakeDown`). A sale (`ParkBuilding.Demolish`)
  frees the coaster's track ride and object and rebuilds `ParkSurfaces`, and a move goes through the same `Demolish`
  (`PickUp`); neither touches `ParkCoasterPylons`, the only other reader of which is the console's `pylons`. Partly run
  (`~/.cache/tpw-harnesses/rev-1030/`, off-screen, her Full Simulation park as Q817's `ours.py`): `pylons 40` before, `sell 236`
  sold the Temple Of Gloom for 10000 and it left `objects`; the count after was not read (run1's replies came one command late,
  run2 went over its 12 GB cap under lavapipe: ours on that park peaked at 11.1 GB off-screen), so run it on the desk. Decode
  first what the original's demolisher does with a coaster's track and pylons (`FUN_00527ee0`'s track arm for `WhichTrackType` 3,
  which Q449 says withdraws nought; whether `FUN_00530830` / `FUN_00530b70` or another walk drops the pylon instances), then
  take the pylons of the sold coaster down (and on a move, put them back where the original does). Related, not the same:
  Q607 (the file's id of a sold object), Q594 (the track drawn), `SALE_TRACK_TEARDOWN`. Confirm: `pylons` before and after a
  sale and a move, with a frame of the empty spot, against the original selling the same coaster; a test that fails with the
  teardown put back.
- [ ] **Q955. The UI's draws: a resource set made each time the shared UI material's texture changes, and every quad's index
  upload ending the render pass.** From an outside audit (Alexah, 2026-10-30), checked against `8a566e88` by the Sentinel's Opus
  reader (`~/.cache/tpw-harnesses/rev-1030/VERIFY-gpt-perf.md`, P1, D, E; source only, not measured). Q954 (`3bc9708e`) since made
  materials share their pipeline: read `Material.cs` there first. (1) One `Material.UI` is rebound to each text's, control's and
  cursor's texture in turn (`UiText.cs:71`, `UiControl.cs:507`, `UiList.cs:905`, `Cursor.cs:73` and nine more); a changed texture
  marks its sets dirty and the next draw makes a new `ResourceSet[]` and queues the old (`Material.cs:352-371`, `:413-434`, called
  per quad at `Graphics.cs:82`): every frame, each time two quads in a row differ. Keep a set per texture. (STATUS's 23% is
  `FlushNewResourceSets`, the per-draw bind every model pays, not this.) (2) `Graphics.Quad` uploads the same six indices with
  every quad (`Graphics.cs:75-76`; the comment at `:63-65` says they are not rebuilt); in NeoVeldrid each `UpdateBuffer` is a
  staging copy that ends the render pass, so each UI quad ends and restarts the 4x MSAA pass. Upload the indices once. (3)
  `UiMesh` keeps a `Model` and `Material` per new frame size for ever (`UiMesh.cs:44,55,108-121`): bounded by the layouts' sizes;
  count them. Measure first (sets made a frame, render passes a frame, frame time), then cut; the picture must not change (rule 6:
  a frame before and after).
- [ ] **Q956. A guest's arrival or departure rebuilds the whole crowd's model, material and shadows.** From an outside audit
  (Alexah, 2026-10-30), checked against `8a566e88` (`VERIFY-gpt-perf.md`, P4, A). `ParkPeople.cs:393, 616, 765` (add) and `:981,
  2096` (remove) call `ParkGuestSprites.Build` (`:520-558`): a new index array, `new Material`, `new Model` (static buffers, then
  `EnableFrequentUpdates` makes a dynamic one, `Model.cs:112-125`) and `BuildShadows` (`:577-602`) the same again; `:880` for
  balloons. Since Q954 the materials share a pipeline, so measure what each rebuild still costs (buffers, sets, the `Asset.All`
  scan in `Shader.Cache.cs:12`) over a Full Simulation park's turnover before changing it. Also `ParkGuestSprites.cs:859`: a new
  `Vector3[4]` per drawn shadow per frame (debug data; STATUS names "the shadow arrays"). Direction: buffers with spare room that
  grow when needed. Fidelity first: the crowd drawn the same (a frame), the census the same.
  - **Note (week-end audit 2026-10-31, L-F2):** the shadow's pipeline is not shared in this path. `ParkGuestSprites.cs:579-580` deletes
    `_shadowModel` before `:597` makes the new material; `Model.Delete` releases the material's `SharedCache` entry at once
    (`Material.cs:176-184`, `SharedCache.cs:45-48`), the silhouette key has one user (`ParkGuestSprites.cs:597` is its only use), so the entry
    goes and the new material compiles a pipeline in that frame, at each `Build` with med.sam's `SPRITESHADOW 1`. `Build`'s own order
    (`:548-553`, new material first, old model after) keeps its pipeline. So "Since Q954 the materials share a pipeline" is untrue of this
    one. Measure it with the rest; the cheap fix is the same order.
- [ ] **Q958. World models outside the camera's view are drawn: the original culls them.** From an outside audit (Alexah,
  2026-10-30), checked against `8a566e88` (`VERIFY-gpt-perf.md`, C). `Entity.cs:87-105` draws every entity in each pass and
  `ModelEntity.cs:66-79` draws whenever it has a model; nothing in `World/` or `Render/` tests the view. The original builds a
  culling frustum each frame (`park-engine.md`, `FUN_0056bb00`, row near line 865): decode what it culls and by what bounds (whole
  object, node, sprite), then cull the same, keeping the draw order. Confirm: the census of drawn models at a few cameras against
  the original's, frames before and after the same, frame time measured. (Q613 is back-face culling, a different thing.)
- [ ] **Q826. What Q954, Q827, Q828, Q829 and Q830 left: textures kept by path, and NeoVeldrid's chunks.** The managed heap's `Byte[]` was 145.6 MB (9,624
  objects, `q827/run2/fs/heapstat.txt`, Full Simulation; 145.7 MB in 9,659 on the final binary, `run3/fs`; a gcroot in `q827/dump1`, stock on the unchanged build, of 15 of its 683 arrays of 30 KB or more, 118 MB): the ground's kept pixels, 33 MB
  (gone with Q829: a changed tile is written in the texture on the card, the pixels kept only at four bits a channel), the sound banks' compressed bytes (the park's music bank 33 MB and another 10 MB, kept by design:
  `SoundBank._encoded`), and the WAD archives, whose files Q828 no longer holds (22.7 MB gone) and whose entries Q830 keeps only from their second ask and Q831 counts from the level's start (`mem wads`: 75 entries, **1.9 MB** stock;
  168 and 3.1 MB Full Simulation; was 11.7 and 15.2). What is left, each measured before and after as Q954, Q827, Q828, Q829 and Q830 did: `Refpack`'s growing list and its copy are Q836's (the decode writes once into an array of the declared size;
  a stock park's 1,247 reads at its entry (1,249 by 30 s; the lobby's are not counted since Q831) take 114 to 130 ms where they took 388 to 406).
  Textures loaded by path are kept for the life of the process, and Q832 measured what that costs and buys (see its entry in `docs/history/queue-done.md`): after a park the lobby holds 337 more textures, 13.5 MB on the card, and a park entered again loads in 3.1 s against 5.0 s; kept by design, `docs/DECISIONS.md`. NeoVeldrid's Vulkan chunks and its staging pool are read and settled by Q834 (`docs/DECISIONS.md`, "NeoVeldrid's memory": the library never gives a chunk back, the desk's three chunks are 384 MB for the whole run, and the 37.3 MB staging pool its first park leaves is Q835). Q830's saving lasted one park load (the ask counts were never reset): Q831 resets them, and frees the kept entries, at each level's end.
  Also left by Q829: the load's `Lay` (730 ms of the park's 5 s load, the 33 MB built once and handed to the card) and, at four bits a channel (`graphics faithful`), a path laid still makes the whole texture again, 140 to 167 ms (`CutLevels` over the box).
- [ ] **Q835. Trim NeoVeldrid's staging pool after a park (Q834's find): 37.3 MB of staging textures kept for the device's life.** The pool (`VkGraphicsDevice._availableStagingTextures`) holds 5.4 MB in the fresh lobby and 37.3 MB (9
  textures) after the first park on the desk, 35.8 MB (3) under lavapipe, host-visible memory (the mapped chunks' used bytes rise by the same), and the library gives none of it back (`docs/DECISIONS.md`, "NeoVeldrid's memory"). Measure what a trim
  would save where device memory is process memory (`OFFSCREEN=1`, `lib/roundtrip.py`'s `mem vulkan` and resident size) before building anything: predict first (VmRSS in the lobby after a park falls by about the pool's growth, 30 MB, at most). Ways: a
  trim of the pool at a level's end by reflection (takes the library's `_stagingResourcesLock`, disposes the textures not in `_submittedStagingTextures`; fragile across an upgrade, which a test must guard by name, as `VulkanMemoryTests` does for the pool; the lock and `_submittedStagingTextures` are not guarded yet), or uploading a
  large texture without a staging texture. Decide in `docs/DECISIONS.md` from the figures; the park's next load re-makes the pool (the second park's load time must not move). (Filed by Q834.)
- [ ] **Q833. A trip to the park and back leaves a little each time: about one texture and 10 MB of managed heap (Q832's find).** Q832's four trips (`q832/runs/final-2`, `final-3`; stock park, `mem gc` then `mem compact`
  at each stage): the lobby's textures 583, 584, 586, 587 (6 new and 2 gone from the first return to the fourth: three `21x11` textures, one more at every lobby entry and none at a park entry, which nothing deletes, maker not yet found (a `UiText` is the likely kind); and the `Cpic` cursor, one of about 20 pictures in `/data/ui/cursors/` loaded by path, so a plateau, not a leak. The two `Stream` textures, remade at every stage, are not part of the creep; the
  console's `assets list` names them and `lib/assetdiff.py` counts them), its resident size 346, 362, 369, 374 MB (final-2: 342, 355, 369, 366), the managed heap committed 75.7 MB in the fresh lobby and 95.4 and
  106.4 MB after the first and second return though the managed total falls (75.4, 69.1, 59.0 MB: the fragmented figure 0.3, 16.9, 27.7 MB, so what stands between the live objects is not given back, and whatever
  pins it is not found), and the park's peak 485, 501, 501, 506 MB. The steps shrink (+16, +7, +5), so this may be a plateau; the question is where it ends and what pins the heap. Do first: ten trips, to see
  whether the lobby's resident size stops, and a `dotnet-dump` pinned census at the third return (`roundtrip.py DUMP=1`, which keeps the .dmp under `~/.cache/dn-tmp`; run `gcroot` on it by hand, it needs an object address); then the `21x11`'s owner (origin-tag the textures' makers). Measure before and after; do not release the textures of Q832's decision (the park's load needs them).
- [ ] **Q959. Re-measure a release of the path textures at a level's end (Q832's decision rests on a wrong reason).** Q832 kept
  the textures loaded by path for the life of the process, saying a release would not lower the park's peak; its probe refused
  cache hits and released nothing, and VmHWM is CPU memory, which does not count the textures. The lobby's 234 textures alive in
  the park (the 135 lobby/terrain ones 6.9 MB with mips, up to 8.6 MB with the interface's and the advisor's) are video memory, or
  system memory under lavapipe or integrated graphics. Build a probe (never committed) that `Delete`s every texture registered by
  path at `Level.Unload`, the lobby's end included, and clears the eleven statics that hold textures (`UiMesh.Loaded` and
  `_blank`, `UiMeter.Skins`, `ParkShopWindow.Loaded`, `ParkObjectWindow.Skins`, `_green` and `_red`, the three `_highlight`s,
  `ParkFootprintPicture._textures`). Run `tools/harness/lib/roundtrip.py` with `TRIPS=1 OFFSCREEN=1` (lavapipe, where device
  memory is process memory) on it and on the unchanged binary, on the same display. Predict first: park1's `mem gpu` Texture
  account falls by 6.9 to 8.6 MB from 108.8 MB, VmHWM falls by about as much, `mem vulkan` (204.5 MB) falls by less or not at all
  (the pool may reuse the space); the second park loads in about 4.8 to 5.0 s against 3.1 s. Add `memmap.drm` vram on the desk if
  wanted. Decide the keep or release in `docs/DECISIONS.md` from the figures (about 1.75 s a re-entry against the saving). (Filed by the Sentinel from the run 15 review of Q832, VERIFY-2.)
- [ ] **Q1120. The music's level and the crowd's voice count the guests once a frame, after all its sweeps (from Q150).**
  The original's every-32nd block (`0x0054f82d`..`0x0054f8c8`) runs in the 32nd tick's own step of the park's loop, after
  that step's sweep. `ParkAudio.OnUpdate` asks `SetsMusicLevel` once a frame, after `ParkLoop`, so in a frame owing a 32nd
  tick and a sweep after it (nine ticks or more: a hitch) the level and the crowd's voice count the guests as the later
  sweep left them. Said at the site. Ask the block from `ParkLoop`'s step, after the sweep, keeping `Audio.Ready`'s guard
  and the pump outside. Test: a frame from tick 25 to 40 sets the level from the guests the sweep of 32 left (a guest the
  sweep of 40 admits is not counted). Reached in play only by a hitch (`lib/hitch.py`).
- [ ] **Q1121. One sweep walks one list of every thing, newest first (from Q150, Q238's note).** Inside one sweep the
  original visits one list holding every kind of thing, the newest first (`FUN_00516380`, `FUN_00516270`;
  `ride-operation.md`, "Where a ride's turn comes from"). `ParkPeople.Sweep` takes the guests, then the staff, then the
  rides, each in that order among its own, so a hire or a thing bought after a guest still takes its turn after every
  guest. Q150 put the sweep in the original's step and left this order. Read which of the sweep's other calls are things
  in that list (the advisor is, model 11) and which are the sweep's own, then walk one list. Confirm: a member of staff
  hired after a guest takes their turn before that guest's in the same sweep, logged.

**Filed from Alexah's play test of 2026-10-30/31 (Full Simulation, Lost Kingdom), at the end of the bugs at their word.**
Their notes, the console.log and the screenshots are in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`; each report was checked against the code, the log and
the docs before filing. The reports already queued got a note in their own item (Q27, Q31b, Q40, Q55, Q148, Q158, Q400,
Q408, Q419, Q511, Q512, Q543, Q648, Q950). The research advisor's "built all of the available features" line was checked
and matches the decode (row 80, `advisor-park.md`); not filed.
- [ ] **Q960. The sideshow's window.** Split from Q31b at Alexah's word (2026-10-31). A click on a sideshow counts
  `SIDESHOW_WINDOW` and opens nothing (`Level.cs`, the object click's UI type 2). Q31b's notes have the decoded figures
  and the inset bars (`hud.md`, "The shop, sideshow and ride windows"). Confirm: the Strength Bird's window beside the
  original's, a screenshot of each.
- [ ] **Q961. The feature window (the Security Camera's).** Split from Q31b at Alexah's word (2026-10-31). A click on a
  feature counts `FEATURE_WINDOW` and opens nothing (UI type 3). Decode `FUN_00499eb0` and its handler `0x00499bb0`
  (`park-engine.md`, the nine object windows) first, and whether it has the camera's view Alexah wants to ride
  (`FUN_0042a440`'s views, Q246). Confirm beside the original, a screenshot of each.
- [ ] **Q962. Joining a queue to a path plays no click.** Counted, never filed: `QUEUE_TOOL_LINK_SOUND_0x8B`
  (`ParkPathBuilding.cs`, the join) and `PATH_LINK_SOUND_0x8B` (`ParkPathNeighbours.cs`, a queue retiled where it is
  joined). The original plays cat_ui `0x8b` at `0x00527587` (`park-engine.md`, the queue tool's finishing link). Check
  at a breakpoint that both sites play in the original (Q671's method), then build. Confirm by the disk driver's mix.
- [ ] **Q963. A queue left unjoined says nothing.** Counted, never filed: `QUEUE_MODE_ADVISOR_MESSAGE_0x151`
  (`ParkPathBuilding.cs`, the anchor click and a run ending on its own queue). The original's op `0x81` ends the tool
  through `FUN_0052f200( 0, 0 )` and posts advisor message `0x151` when the queue is not joined (`park-engine.md`).
  Likely global sample 623, "you didn't connect the line to a path..." (inferred, not read): read message `0x151`'s
  record and how it is withdrawn (`hud.md`, the mode setter) first.
- [ ] **Q964. Does an unjoined queue pulse red outside the queue tool? Check the original first.** Alexah remembers it;
  no `docs/exe/` page says so. The track's open end pulses (`Model_PulseOff`, `park.md`; Q767). Film an unjoined queue
  in the original with no tool held. If it pulses, find the producer (likely through `FUN_004de4a0`, the back of queue
  joined) and build it; if not, close this item with the film.
- [ ] **Q965. Turning a carried object with , and . before it is put down.** Alexah is sure the original has it
  (2026-10-31); `park-engine.md` found no manual rotate on any traced path. `InputButton.TurnBlueprintLeft` and
  `TurnBlueprintRight` are bound and nothing reads them; every purchase is put down at angle 0. Find the key in the
  original first (press , and . and their Shift forms while carrying; then the row in the key tables and the angle
  write, perhaps near `0x00523804`). Build it, counted until then. Confirm: a shop turned twice and put down, beside
  the original.
- [ ] **Q966. The Clear Land tool.** The buy screen's Clear Land row counts `BUY_LAND_TOOL` and logs "the land tools
  are not built"; Delete counts `CLEAR_LAND_TOOL`. Alexah wants the tool built (2026-10-31): it should at least remove
  paths. Decode mode `0x3a` and its click (`park-engine.md`) for what it removes and what it costs, then build.
  Q1000 measured one thing of it in the original: a drag from (69,22) to (73,27) over a Large Rock Pillar sold it, and the
  same drag over a Dino Karts Tunnel sold nothing (`park-engine.md`, "An add-on has no window and no sale of its own").
  Backspace over a path still deletes it. Confirm beside the original.
- [ ] **Q967. Putting a bought object down makes no sound.** Not counted. The original's commit `FUN_00524960` reads
  `DAT_008186d8` at `0x00524a77`, which gates a sound (`park-engine.md`), and the script loader has a sound zone at
  `0x00558fda` (`park.md`, not decoded). Name the effect at a breakpoint for a ride, a shop and a feature, then build.
  Confirm by the disk driver's mix.
- [ ] **Q968. The ground, paths and queues look blurred and show their cell squares.** VisibleGridLines1.png and
  VisibleGridlines2.png: faint lines along cell edges, and the ground softer than the walls and models beside it.
  `GroundTiles` lays every tile side by side in one atlas whose mips average across tile edges, sampled
  `AnisotropicWrap` (`ParkGround.cs`, `Material.cs`); the original gives each tile its own clamped texture, magnified
  LINEAR, minified POINT, one mip level (`render-states.md`, "Texture filtering" and "Texture addressing"). Q15 is
  the wrap half on paths. Measure first: the lines at tile or cell edges, and whether clamping or per-tile mips (or
  padding) removes them; then the sharpness against the original at the same view (Q618 settled 32-px tiles).
  - **Note (fork watch 2026-11-12, verify-world.md H2):** the texture page manager does place textures: the per-class limit `+0x1818` is
    written each frame by `FUN_0058ccc0` (through `[cache+0x1800]+0x18`, `0x0058cdf3`, `0x0058ce48`), called from the frame step
    `FUN_0058cf40` (`0x0058cf70`) before the placer `0x0058f940`. Which textures share a page, and how many pages a class holds
    at run time, are not read, so "each tile its own clamped texture" above is not established by the page manager either way.
- [ ] **Q969. The clear colour shows through near the back mountain.** BlueClipping.png: long straight lines of
  (72, 216, 248), the frame's clear colour, on open ground and on the rock face, away from any plot rim (Q807 and
  Q808 closed the rims). Find the camera, measure with `lib/clearseam.py`, find which edge opens (ground against the
  scenery's rock, or a cell's diagonal since Q821), then close it. Confirm: the clear-colour count at that view.
  - **Note (fork watch 2026-11-12, verify-ui-hud.md M6):** the original's park view clears to black: the clear colour is `[0x008bd47c]`
    (the fog) only while the render word `[0x008bcbc8]` has `0x2000` or `0x4000000`, else 0 (`0x005737aa`..`0x005737d5`), and
    the park's word reads `0x1444100` (`park-engine.md`, measured). Ours clears to (72, 216, 248), so a seam that shows light
    blue here would show black there. Decide what ours clears to; a colour other than black is a deviation said at the site.
- [ ] **Q970. A guest on a toilet is drawn in its doorway.** BathroomClipping.png: the person shows through the hut's
  door while inside. Read what the original does with a toilet's user (Q52's flag `0x20` test hides a rider's sprite,
  or the toilet's script), then build. Confirm beside the original.
- [ ] **Q971. A broken-down ride keeps its idle animation. Check the original first.** Alexah remembers a broken look
  until it is repaired. Ours raises the broken hoardings and closes it (`ParkPeople.BreakDown`, counted
  `RIDE_BROKEN_DOWN_EVENT`); `ride-operation.md` says only that. Film a broken Belly Bounce in the original; read the
  scripts' `VAR_BROKEN`, `VAR_BREAKSTAT` and the `LOOPANIM` after a break (`park.md`). Build what differs.
- [ ] **Q972. Closing a ride with riders and a queue: how fast both empty. Check the original first.** Alexah remembers
  both emptied at once. Ours follows the decode: one head turned away a turn (`FUN_004e0450`,
  `ride-operation.md`), riders let off by the script reading `VAR_RIDECLOSED`. Close a full Belly Bounce in both games
  and count riders and queue after 10 s. If the original empties faster, the decode is incomplete.
- [ ] **Q973. A fresh Full Simulation park's first guests. Check the original first.** Alexah remembers a decent load
  dropped at the first stop while the park is still closed. Ours dropped 4 (then 2): `(NewParkBonus 20 + worth 0) x 1.2
  / PointsPerVisitor 6` (`guest-arrivals.md`; only the stock park's 13 is measured). Read the original's first load
  and its tick in a fresh Full Simulation park, and which `.sam` block it reads.
- [ ] **Q974. Choosing a park in the lobby cuts the advisor off and the island sound stutters.** Not counted. The log
  (line 180-196): a line cut mid-way, a cry (sample 641, `Advisor.StopSpeaking`, as `0x005994e0` does) and a new line
  in the same second as the island's music changes. Capture both games' mixes at the choice by the disk driver; say
  whether the original cries out there, and whether the mix clamps (32 voices, the hard clamp in `Audio.cs`). Q950 (1)
  is the attract sound; do this after it.
- [ ] **Q975. The lobby's shoreline ripples flicker.** The islands' `Post Ripples01` mesh, a UV scroll drawn in the
  translucent half, writing depth, unsorted (`LobbyModel.cs`, `Entity.cs`). Film the ripples beside the original's,
  then compare the draw's depth write and blend with `render-states.md`.
- [ ] **Q988. Placed and flat voices against `AudioListener.PanTo`: measure a placed voice's law before any build.** From the fork watch
  2026-10-31 (`verify-other.md` H1, `verify-audio-fx.md` H2; the fork's own claim that every voice is mode `0x1000000` is refuted).
  The stream's flag word is the request's `+0xc` (`0x006bb3f5`..`0x006bb3ff`), not its field mask `+0x14`: a flat voice
  has bit 0 (`0x006bc52d`) and takes mode `0x1000000`, a placed voice is clear and takes `0x2000000` (`0x006d23ab`..`0x006d23bb`);
  the measured channels read `0x1110` flat and `0x1111` placed, which fits. A flat voice reaches QMixer as `SetPosition`
  (command 6): azimuth 0, range 9.16e-5, gain 1 (`0x18007b16`, `0x1800931c`; q264 reads distance 0.00, gain 1.0000). A placed voice is a
  DirectSound 3D buffer (`SetAllParameters`, `0x18005971`; our `QMixer.dll` md5 `01de62e5`) or, without the `0x20000` hardware-streaming
  ask, QMixer's software class (`0x1800b130`). Neither is `SetPan`. So the fork's SetPan law reaches flat voices only (pan 0) and does not overturn `AudioListener.PanTo`, whose comment stands; PanTo's dot product against QSound's real output is unproven. Unproven: DirectSound 3D's rolloff, whether QMixer sets min and max, which class
  a real run uses (`[0xfb201c]`, whether that caps struct is `DSCAPS`), and our dot product against QSound's real output. Capture the original's mix of a placed voice at several bearings (`mixer.py` gives the gain only, pan is unread), then compare `PanTo`; build nothing before. Never copy a QMixer address from the fork: its `QMixer.dll` is another build. Doppler needs nothing (off on every voice
  measured). Decoded in `audio.md`, "The rest of that word".
  - **Note (fork watch 2026-11-12, verify-audio-fx.md F5):** checked again and the reading above stands: `0x006bc61b` ORs bit 0 into the
    request's field mask `+0x14`, and the mode word is the stream's `+0x14`, copied from the request's `+0xc` (`0x006bb3f5`..`0x006bb3ff`).
- [ ] **Q989. The Finance graph's series.** From the fork watch 2026-10-31 (`verify-ui-hud.md` L3); Q714 is the Park Status screen's, this is the
  other caller of the same draw. Each series is `0x0047fe90`'s line (oldest sample first, x in steps of width / count so a short history
  stops part way and starts at 0, a hollow 3 x 3 box at each segment end only while count is under `0x90` (`0x0047ffd5`; the 144-month period draws none), y 32-bit) and ends in its newest value printed `%d` in the
  series colour; Finance has eight series (`0x0049aa61`..`0x0049ab70`). Written in `hud.md`, "A series' line". Ours has no Finance screen (`FINANCE_SCREEN`, `ParkCategoryScreens.cs`); build this with it, or say what it waits on. No open item asks for that screen, so this item is larger than it reads. Unproven: whether a reopened Finance grid stays at 12 samples (no
  `0x0047fb10` after its setup, `0x0049a734`); the label's alignment word. Confirm: the screen beside the original's on one file, a
  history shorter than the period.
- [ ] **Q990. The guard's chase counters.** From the fork watch 2026-10-31 (`verify-other.md` L7, confirmed by a glance only). `0x004c74f0`
  raises `+0x21bfc` and `+0x21c04`, and `+0x21c00` on argument 0; `0x004d691b` passes 2, `0x004d6a1f` passes 0; `0x004c94a0` reads
  `+0x21c00`; `0x004c79eb` zeroes the month words. What the arguments mean (caught, got away) is the fork's, not read. The advisor's guard messages wait on these; the chase that raises them is unbuilt (Q605). Decode the meaning first, then build with that item.
- [ ] **Q991. The coaster demolish preview's marks.** From the fork watch 2026-10-31 (`verify-world.md` M6, partly confirmed; Q951 is the nearest).
  `0x00444090` sets `0x1000000` on [low, high) and `0x00444040` clears `0x1004000` (mask `0xfeffbfff`); `0x00443ee0` finds the pylon
  through `0x007a4610`; `0x0043f4e0` tests the flag (`0x0043f50b`, `0x0043f57b`); `[0x007925d0]` picks a material index stored on the faces
  (`0x0043f5a5`..`0x0043f5bf`), written 1 at `0x00446c1f` and 3 at `0x00446df6`, and a `0x4000` node takes 2. Unread: which texture each index
  names (the fork says `m_erase` and a blue moved-pylon one; unproven). Read these, then build with Q951.
- [ ] **Q992. A script's graph records: the parent copy and the head table's draw.** From the fork watch 2026-10-31 (`verify-world.md` M11,
  confirmed). `0x0055516b` takes the parent id and copies `+0xac`, `+0x9c`, `+0xb4`, `+0xc8`; `0x00555255` stores only `[parent+0x14]`; the
  constructor clears `0x3d` dwords (`0x005588a8`); `ADDHEAD` (`0x00554c8f`..`0x00554cd3`) asks for a free slot first, redraws until it gets
  0 and does no duplicate test; `NAME` is `0x0055429a`, with -1 at `0x00558c4c`. Compare with `RideScript` and `park.md`, "The head
  table", and file what differs.
- [ ] **Q993. What a written park still leaves out: a bought coaster, a kart's cars and pieces' models, and the counted names.** From the
  week-end audit 2026-10-31 (S-D1, S-S1, the Standing count; N8 of the docs sweep). **Done as Q858:** a bought kart or water ride is written whole
  (the object, script, model, footprint cells, the ride's record and sections) and the Standing count is of the objects the file holds
  (`ParkState.WrittenBuilt`: what is left out is not counted in it, a sold object the file keeps is). **Left:** (1) **A coaster bought**
  (`item.TrackType` 3: `Level.WritesTrackKind`) is counted (`SAVE_PARK_OBJECT_BOUGHT`) and not written, with its pylon chain (Q727(4)) and its
  track pieces. (2) **A loaded kart with cars** (`SAVE_PARK_TRACK_RIDE_AS_THE_FILE`, `review17/q849/SECOND.md` D3) stays the file's with its cars
  while every `BUMP` is a no-op for a kart here, so the script walks the riders off and spins at word 15 (`BUMP 5`'s loop, Q588's note; answered since Q883, so the spin is gone, not re-measured on her file) where the file held it at word 197 (`VAR_ONRIDE`
  4, `VAR_RUNNING` 1): the written file contradicts itself (`saves.md:1522`, "a file agrees with itself"). Write the kart's record from what runs, with
  its cars, or run the kart's script as the original does; the original ran on with the file, whether the four seated riders ever get off there is not
  read. (3) **The kart's and the water ride's pieces' models** (`SAVE_PARK_TRACK_PIECE_MODEL`, 16 for the two loops of Q858's run): a track piece is a
  model record like a queue piece's (`TrackKit_LoadKartPieces`, `TrackKit_LoadWaterPieces`, "OpenTPW's writer, a queue cell's model"), which a
  written park does not hold, so the original loading the file draws no piece, by reading. **Also left (review of Q858):** the three track add-ons (1500 to 1502, TrackType 1 in Upgrades.sam) stay out of the writer, counted `SAVE_PARK_OBJECT_BOUGHT`, a sold file add-on kept, and a file add-on left naming a kart (`mUpgradeParent`) that is sold, uncounted (`saves.md`, "The track add-ons are left out"); the object's `+0x2c` word follows the running object where the ride stays the file's (`SAVE_PARK_TRACK_RIDE_AS_THE_FILE`), not the file's; each anchor's
  `mSegmentNumber` is written `0xffff` where the original's walk stores its step (`SAVE_PARK_TRACK_SEGMENT`; the closed-loop edit compares it,
  `saves.md`, "OpenTPW's writer, a kart's or water ride's record"), keep the walk's step on the anchor and write it; a file's kart sold bare leaves its
  piece models (`SAVE_PARK_TRACK_PIECE_MODEL_LEFT`); an opened kart's state, duration and cars, and a speed set in the ride's window, are not written as the
  original holds them. (4) A boat's sound and wake (the two rides' companion scripts are written since Q911 and run in a bought kart and water ride since Q939), a coaster's or a tour's
  new head and the riders' puffs (`DOES-NOT.md`). (5) **The counted names with no owner**, by a raw name match against this file:
  `PARK_FILE_SOUND_WRITER_LIVE_VOICES`, `SAVE_PARK_ARRIVAL_VEHICLE`, `SAVE_PARK_CAR_NO_HEIGHT`, `SAVE_PARK_CAR_SMOKE`,
  `SAVE_PARK_EMITTER_DIRECTED`, `SAVE_PARK_EMITTER_ITEM_EFFECT`, `SAVE_PARK_EMITTER_NO_PLACE`, `SAVE_PARK_EMITTER_NOT_STARTED`,
  `SAVE_PARK_FOOTPRINT_CELL`, `SAVE_PARK_GUEST_ON_A_THING`, `SAVE_PARK_HANDLE_TO_AN_UNWRITTEN_THING`, `SAVE_PARK_MADE_MODEL_TABLES`,
  `SAVE_PARK_OBJECT_BOUGHT`, `SAVE_PARK_QUEUE_CELL_MODEL`, `SAVE_PARK_SPRITE_SET_BYTE` (an item may carry one in words; Q420's leftovers
  Q539 are the refusal latch's, not the writer's; Q587 the piece model, Q607 and Q734 cite the gap, Q727(4) the pylon chain: none owns it).
  Sort them: build each, or say at the site why it is not. The original writes the object whole in its park file (`saves.md`, "OpenTPW's
  writer", the object module; its controls are `FUN_004d3aa0`'s). Dead by GAP (AGENTS.md rule 3): placed content reaches it, and Lost Kingdom at
  100% cannot close without it. Confirm: (2) load her Full Simulation file, save, and read the kart's script and its cars' riders in the file against the original's (whether the four seated riders get off or board again there was not read), in both games; (1), (3) a coaster, and a kart
  bought here, loaded in the original. **Needs the desk (2026-10-31):** the original given a file ours wrote with a bought kart (Q858's
  `q858/rebased-fix/autosave.TPWS`: Dino Karts and Splish Splash, each looped): does it load, does the ride run and its sections draw without the
  pieces' models? Lane B may not run the original, so Q858 saw ours only.
- [ ] **Q1248. One copy: a subst drive to a game folder takes a second lock.** From Q1174. `OneCopy.WithoutLinks` follows
  symbolic links and junctions, but a drive made by `subst` (or a mapped drive to a local share) is not a link: `S:\` and the
  folder it stands for hash to two names. Map such a drive to its folder before hashing (`QueryDosDevice`, or the handle's
  final path, `GetFinalPathNameByHandle`), on Windows only, with a test that runs only there. Not measured: whether anyone
  starts the game that way. Confirm: on Windows, a copy started on `subst S: <game>` while another runs on the folder is refused.
- [ ] **Q996. A failed route is not revived; a binding read on its press is rebuilt as the frame ends.** From the week-end audit 2026-10-31
  (D1, D2 of the docs sweep). (1) The original's walker revives a failed route when the ground near the guest has changed since the last plan
  and walks them to the loser's back of queue (`0x0050ed79`); `PeepBehaviour.cs:2459` says so, `docs/exe/ride-operation.md:4557` and STATUS
  say "not revived", and Q104's Done text records it as not built, but no `Unimplemented.Report` and no open item carries it. Here `Walked`
  gives up before its step when no route is held. Count it (`Unimplemented.Report`), then build it. Whether placed content reaches it is
  not measured. (2) `Input.cs:228`: a binding read on its press or as a held state is rebuilt as the frame ends; Q120's decode covers
  key-ups only, so whether a press-time reading differs from the original at all is not established. Decode it first. Confirm: (1) a route
  blocked and cleared, in both games; (2) two events in one frame, both games.
- [ ] **Q1200. A park read in part says PARK NOT SAVED; the display line reads the window after its resize.** From Q997 (S-M2, ST-F5 of
  the week-end audit 2026-10-31), not built there because each is seen only on a screen. (1) S-M2: a park read in part is refused by the
  writer (`ParkFileWriter.Body`, "was not read whole") and `Level.WritePark` logs a warning only (`catch ( InvalidOperationException )`),
  with no `SaveTrouble.Failed`, so Save Park, Alt+S and the autosave lose the play in silence; the original has no such refusal. Tell it
  in the game's own box (rule 11; a text of its own, not "The save folder cannot be written"), Asked or once a visit as `SaveTrouble`
  does. Confirm: a truncated copy loaded, Save Park, the box photographed. (2) ST-F5: `Display.Apply`'s line and the log's head read the
  window's size before SDL's resize event is pumped (`console.log:53814`: `FullScreen at 2560 x 1440, window 2560x1359`); the
  `LaunchReport` line (`LaunchReport.LogGraphics`, after `Display.Apply` in `Game.cs`) is inferred to do the same. Read the size after a
  pump, or say in the line that it is the size asked for. Confirm: a full-screen start on a real display, the two lines against the
  screen's size.
- [ ] **Q1201. The advisor gaps left by Q420 and Q443, and the save folder's `.TPWS.tmp`.** From Q997 (the week-end audit 2026-10-31),
  not built there because each needs the original or Ghidra (none in a cloud lane). (1) The theme's advisor's golden-ticket clips are not
  built. (2) A rider standing on a node a clip moves faces as it does at rest. (3) The advisor's lines that wait thirty days from the newest build
  still come before the original's. (4) The line about an unjoined queue passes over the ride the queue tool is armed for and no other,
  where the original's passes over the builder's object, whose life is not read (Q1160 is the same line's tag, not this). Count each
  (`Unimplemented.Report`) where placed content reaches it, then decode and build. (5) A leftover `*.TPWS.tmp` (a kill during
  `BaseFileSystem.WriteAllBytes`) is not taken for a park here, but the original's own `.TPW*` search would take it, the folder being
  shared, and nothing deletes it: decide whether the next start deletes it (it is ours, never the player's park) and say so in the log.
  Confirm: (1) to (4) in both games; (5) a planted `.tmp`, a start, and the original's Load list after.
- [ ] **Q1264. Rows 86-89, 140 and 141: the thing each score leaves reaches its tag.** From Q1160 (its "when they are
  built" part). The speech writes the thing a row's score leaves into the record's thing (`FUN_0059b620`, `0x0059b6cf`;
  `ParkAdvisorMessages.ThingOf`), which Q1160 built for row 113 alone. Rows 86-89 (`0x0059e4b2`..) and 141
  (`0x0059f54e`) leave a handle, and row 140 bus stop B's packed cell (`0x0059f4dd`), which its tag's type-4 button
  turns into a camera move (`FUN_004868d0`, `hud.md`); none of the six is built. As each is built, give `ThingOf` its
  case (and the dword at the balance object's `+8`, the record's `+4`, where its score leaves one), with a test whose
  said event carries it.
- [ ] **Q1161. The camcorder's keys turn, and its turn rate and walk speed are the original's.** From the fork watch 2026-11-12
  (`verify-other.md` L2, `verify-world.md` M3, M4). The original's first person is read in `park-engine.md`, "The walk's keys,
  turn and speed": Left and keypad 9, Right and keypad 7 turn at 1.2 radians a second; Up, Down and keypads 1 and 3 walk; the
  pointer turns 2.4 radians a second a unit past ±0.4; the walk settles at `sqrt( zoom ) x 0.002` units a millisecond (21 a
  second at zoom 110) by a half step a frame; the orbit's pan velocity carries in. Ours (`ParkCamcorderCameraMode`) turns
  2.0 a unit, walks 40 a second and has no key turn (its strafe is gone, Q17); each is said at the site. Build them on
  `Time.Delta` (rule 10: the per-frame half step becomes its time-based equivalent, said at the site). Confirm under Proton
  first (`campoi.py` in first person: the yaw after a held Left of known length, the position after a held Up at zoom 110),
  then ours' `camcorder` census against it.
- [ ] **Q1162. The bump cars' particle 15: its name, the removal's skip and the seating beam's gate.** From the fork watch
  2026-11-12 (`verify-world.md` M2). The launch's draw spawns particle `0xf` at the car (`park.md`, "Where a bumper ride's cars
  float"), which `ParLib` names `P_EFFECT_BeamUpCar`, not the splash (12); ours counts it as `BUMPER_CAR_SPLASH`. The removal
  `FUN_0054ae50` spawns `0xf` too, but not when the ride's `+0x50` is 4 (`0x0054aeaa`..`0x0054aed5`); ours counts
  `BUMPER_CAR_REMOVED_PARTICLE` on every removal. The seating `FUN_00549c60` spawns `0x13` per rider only for a rider whose seat
  node is found and only when its second argument is not nought (`0x00549cbd`, `0x00549d04`); ours' `Seat` counts per rider
  whatever the node, and passes `particles: false` at the launch, where the original passes the launcher's own second
  argument (`0x00549ff3`, not traced). Decode what `+0x50` 4 is and the launcher's argument, give the counts names that say
  what they are, then build them with `WorldParticles`. Not read: whether the draw `0x00546280` runs for a ride out of view,
  and which families reach `FUN_00549db0`. Confirm: Dino Karts or the Hot Pot beside the original's, the cars launched and
  removed, `particles` against `original/emitters.py`.
- [ ] **Q1163. A text button's lettering changes with its state.** From the fork watch 2026-11-12 (`verify-ui-hud.md` H2). A
  button of class `0x00705a50` letters its label by state (`ui.md`, "A text button's lettering by state"): white at (0, 0)
  plain or checked, black at (1, 1) with the pointer on it, white at (2, 1) pressed, unchanged disabled; a heading's caller
  sets both colours to its own (255, 239, 0). Ours' `UiControl.TextColour` is one colour with no offset by state, so the
  finance and park-info toggles and the list headings' buttons letter one way always. First read which of Lost Kingdom's
  buttons are of the class (their layouts' controls), then build the states. Confirm: hover and press one toggle in both
  games, photographs side by side.
- [ ] **Q1165. The arrival manager reads this sweep's rain (from Q570).** From the run 22 review of Q570 (`review22/q570`,
  finding 1). In the original the weather (thing 7) turns in the sweep's walk, before the tail, and the tail's arrival
  manager then reads its drops (`FUN_004cf5b0`, `0x004cf609` World_Weather, `0x004cf60e` `mov ecx,[eax+0x30]`, k 0.8 or 1.2
  at `0x00700368`/`0x00700364`), so a load is sized with this sweep's rain. Ours runs the arrival manager in
  `ParkPeople.Sweep` (`StepArrivals`, `Raining()`) before `ParkLoop.Tail`'s `weather.Turn()`, so on the sweep where the drops
  cross nought a load is sized with the previous turn's rain (a third more or fewer guests). Said at `ParkLoop.Tail` and in
  `weather.md`. A possible fix: turn the weather in `ParkPeople.Sweep` before `StepArrivals`, its day read from the date of
  `State.GameTick` and not from the compared `GameCalendar.Days`. Test: a sweep whose turn starts or stops the rain sizes
  its load with the new drops' k. Confirm: the weather's and the arrivals' log lines on the sweep the rain starts, read
  against the original's drops and load at that sweep.
- [ ] **Q1166. A looped Layer I voice's later passes start one frame in.** From the run 22 review of Q659 (`review22/q659`,
  `VERIFY-6.md` F1). In the original a looped voice of an in-memory bank (BANK byte +9 bit 0x1; `0x006ba690`,
  `0x006ba725`) rewinds through `0x006c84a0`, which for Layer I decodes and drops 0x300 bytes (`0x006c84b5`..`0x006c84cc`),
  so pass 1 plays decode bytes [0, D) and every later pass [0x300, 0x300 + D); the builder clears request 0x40 before the
  start (`0x006ba59d`, `0x006ba93a`), so the start's own skip (`0x006c8358`) does not fire for a count > 1. A streamed
  bank's voice (`0x0070ae90`, rewind `0x006c8b10`) never skips. OpenTPW wraps every pass to frame 0 (`Voice`, said there),
  so Lost Kingdom's river and waterfall dip ~17 ms each 1.7-1.8 s (`docs/exe/audio.md`, "A looped Layer I voice's later
  passes start one frame in"). Build: keep 384 / ch more frames of a Layer I clip, carry the layer and the bank's flag
  (the category's BANK record, byte +9), and wrap a looped in-memory Layer I voice to 384 / ch. Test: a looped clip's
  second pass starts at 384 / ch, a streamed bank's at 0, a one-shot's length unchanged. Confirm in both games by a
  capture of the fall's channel. Open: the half-rate switch `0x00fb2708`'s default; which plain/music effects (count 1
  with 0x40) take the start's skip.
- [ ] **Q1167. Speech opens with one feed of silence.** From the run 22 review of Q659 (`review22/q659`, `VERIFY-6.md` F2).
  The MPEG read's first call on a Layer II 48 kbit/s voice (the 644 speech entries) returns zeros for the whole request
  (`0x006c83e1`..`0x006c8426`); the channel's feed asks 0x2400 bytes (`0x006bb5a0`, `0x006bb632`), about 209 ms
  (`docs/exe/audio.md`, "Speech opens with one feed of silence"; said at `Advisor.Step`). Check no stage between resizes
  it (the 0x4000 stage, `0x006ba486`), measure a line's onset in the original under Proton against its play call, then
  delay OpenTPW's speech voice start by the same 4,608 samples (lip sync included).

## B. Docs and comments

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
- [ ] **Q75. The 2026-09-12 review's Phase F, what is left.** Opportunistic, never alone. Left: `content/textures/test.png`; `.editorconfig`'s `end_of_line = crlf` with no
  `.gitattributes`; and, to LABEL as dead by CODE rather than delete (rule 3), `Public/ModelFile.cs` and
  `Public/MapFile.cs` (both unreferenced), `Render/Primitives/Cube.cs` and `ReadUIntN`. `Singleton.cs` is used by
  `UI/Cursor.cs`, though nothing reads its `Instance`. Left to Alexah's call, not to be started: `.gitignore` for `.claude/`,
  `.vscode/` and `.mcp.json`.
  Also left (the 2026-09-26 staleness audit): the `PreferredBackend` setting in `App.config` and `Settings.settings`,
  which nothing reads (`Settings.Default` is read for `GamePath` and `GameWindowSize` only). Label it as dead by CODE
  at its sites; remove it only on Alexah's word.
- [ ] **Q204. Load the area-only notes only in their area.** Claude Code loads a `.claude/rules/*.md` file whose
  `paths:` front matter matches an open file, and no other time. Move the sections of `CLAUDE.local.md` (and any of
  `CLAUDE.md`) that serve one area only - the capture recipe, the original under Proton, the 2.0 patch's evidence - into
  such files, keyed to the source and harness paths they serve, so a session that never opens them never loads them.
  Measure both files' size before and after, and confirm in a fresh session that a matching file brings its rule in
  and a non-matching one does not. Borrowed from Rootstock (github.com/Mazhron/rootstock-os); nothing else of it is
  taken. Alexah's yes 2026-10-01, for after the weekly reset. No game run.

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
  From Q690 (`park-engine.md`, "The camera's reset"): Home and keypad 5 write the asked rotation 0 and the drawn one eases
  there, 1.42 s from 135 and, from 270, the long way down through 180 (both kept in 0..2 pi, `q690/orig/a-kp5.log`);
  `ParkOrbitCameraMode.Reset` sets the yaw at once. The ease built here carries it.
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
  - **Note (fork watch 2026-11-12, verify-world.md M1):** `FUN_00469410`, run each frame after the pulse tick (`0x0054e306`), writes
    `0xffffffff` into every vertex colour (the `+0x64` table) of each mesh without flag `0x4000` of the highlighted model
    `[0x007a1ce0]`, the one the swap `FUN_00469180` stores: the hovered model is drawn full white, not pulsed (its
    `GetTickCount` is dropped, `0x00469424`). It fits Q478's brighter Belly Bounce; not measured against it.
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
  - **Note (play test 2026-10-31):** still broken: about 1,000 `Camera: pan by key` lines in the log and not one from the pointer. Nothing counts the gap; add the count with the fix. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
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
  - **Note (fork watch 2026-10-28, verify-hz-world M4):** the lobby's island title (`FUN_005b12e0`, one caller `0x4b90f0`): when
    `+0xb8` is clear `FUN_005b11c0` loads `data\levels\%s\global.sam` (`0x7733b4`) into `+0x1c`; the title is the item name
    (`FUN_00412fb0`, id `0x414670`, item `+0x4f4`) of `[+0x1c]+0x18` (jungle `global.sam` line 16, `ParkName.GateObjectId 1601`;
    that `+0x18` is that key is the fork's reading, not re-read); a load failure gives an empty string. Join: first line empty
    gives the second alone; first ending in '-' drops it and joins with `"%s%s"` (`0x749850`); second empty gives the first alone;
    else `"%s %s"` (`0x75c0c0`).
  - **Note (Q243's census, 2026-11-12):** the two clicks this builds are counted: `RENAME_PARK` (`ParkEntryPriceScreen.cs`, the
    heading) and `RENAME_OBJECT` (`ParkObjectWindow.cs`, `ParkShopWindow.cs`, the title; `hud.md`).
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
  - **Note (Q672):** ours refuses any path cell under a footprint (`ParkBuilding.Refusal`, "is a path"), where this item says
    the original allows bare ground or path. Seen: the Belly Bounce with the pointer on (41,22) is refused here, "will not
    fit at (40,20) - (40,21) is a path" (`q672/run3`), and taken by the original from the same pointer cell, 88181 to
    87701 (`q635/orig/b-run.out` 1-4-put). Which of its cells may stand on a path there is not read.
- [ ] **Q38. What building and selling look and sound like.** Counted, not built: the death particle
  (`DESTROY_PARTICLE_EFFECT`, `Info.DestroyParticleEffect`, 75-78, a world effect spread over the
  footprint) and the demolish sound (`DEMOLISH_SOUND`, `0x96`-`0x99` by size; Q1000 caught the pillar's sale in the original playing `0x98` once, category word 3, X 40 = 10 x the picture's width, `park-engine.md`, "The demolisher's per-cell tail, and its sale sound"). The build side's particle is
  counted (`CREATE_PARTICLE_EFFECT`, Q663) and its build is Q683; not yet counted: the `+0xd4` sound zone (an EAX 2.0 occlusion zone) the script
  loader makes when an item has one (`park.md`, "What building a thing gives off"). The world pass to draw them in is `WorldParticles`.
  - **Note (fork watch 2026-10-16, `verify-audio-fx.md` A6):** particle caller ids read in the listing: `0x004d95bf`
    50, `0x00544dd8` 2, `0x005480fc` 96, `0x0055dba3` 39, `0x0055dc22` and `0x0055dca1` 40, `0x005999ad` 87,
    `0x0048de1a` 91; also `0x00546ac7` and `0x0054aed5` (15), `0x00549d04`, `0x0054ad24`, `0x00557334`, `0x00557f0e`
    (19), `0x004b8f6e` to `0x004b961c` (97, 98), `0x00548481`, `0x00548a4b` (1), `0x005488d4` (10). Which fire for a
    Lost Kingdom build or sale is not read.
  - **Note (fork watch 2026-11-12, verify-audio-fx.md F1):** the `+0xd4` zone is an EAX 2.0 occlusion zone (`park.md`, "What building a
    thing gives off"): a placed voice inside one gets obstruction and occlusion by a factor, only while EAX is on
    (`[0x00fb2050]`). Not read: the factor `0x006c4c80`, which of the five arguments are x, y and z, and the teardown's release.
    Inferred, unproven: with no EAX 2.0 (a modern DirectSound, Proton) the zones are built and never heard.
- [ ] **Q40. The place-staff mode's cell rule and its carry preview.** Found by Q6's decode (`park-engine.md`,
  "Putting a candidate down: the type-5 mode"), so this is a build. The original takes a worker only on a
  cell of `mType` 0, 1, 3 or 9, not flagged `0x40`, whose track record after the 12/17 parent redirect is
  not track type 11, 13, 16, 18 or 25; here any cell on the map takes one (`STAFF_PLACEMENT_CELL_RULE`).
  While a candidate is carried the original shows cursor 9 (`c_carry.ani`), hangs a sprite of their kind
  in their costume under the pointer and puts a red square on a cell the click would refuse; here the
  pointer is plain (`STAFF_CARRY_PREVIEW`). Confirm: carry a candidate over a cell the rule refuses, click,
  still in the hand and the red square photographed; then a path cell, hired.
  - **Note (play test 2026-10-31):** still no figure at the pointer while placing a hired member (`STAFF_CARRY_PREVIEW` reached). Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
  - **Note (fork watch 2026-10-31, verify-other H2):** the carried figure: `0x0046c480` takes the point `0x007a1c90`, height
    less -5.0, heading 4, into `0x00475480`; `0x0046c730` maps type 4 to 8 to kinds 6, 5, 4, 7, 8 (`0x0046c87c`); the sprite
    script (`0x004766d0`..`0x00476940`, entry `0x15` of `0x0075a418`) stores `0x1200`, sets 6 and plays frames 0 to 3.
    `0x00542010` adds the terrain (`0x004527f0`) to a hit height that is already absolute, so the original counts the ground
    twice and the sprite stands at terrain + height + 5: where that lands on screen is unproven, so capture before building
    and do not "fix" it unseen. Our gap is not the `.FPC` (we pick it only in first person; the preview is simply unbuilt,
    `ParkStaffPool.cs`); set 6 of 16 `.FPC` banks is a clear 2 x 2 placeholder (FileFormats `sprites.md`).
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
    (`0x40`), passing the group-enable gate `0x006bc760` (the device's enable bytes `+0x44`..`+0x47` against the voice's mask `+0x47`; a closed group stops the voice and frees its handle), in range or flagged `0x8` (`0x006bca10`), and keyed strictly above
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
  - **Note (fork watch 2026-10-28, verify-hz-audio-fx A1):** N follows the sound-detail ladder `FUN_0051b810( q )` (from
    `FUN_0051b920`, at boot `0x0054de22` with DEFAULTQUALITY or half of it, and from the options `0x00423850`): sound.sam's
    `ChannelInfo[3]` { CHANNELS, SWITCH } = { 5, 0 }, { 12, 50 }, { 13, 70 } (globals `0x00785938`..`0x0078594c`; CHANNELS[2] has
    no reader). q above 70: (q - 50) x (M - 12) / 50 + 12 with M the device's capability (message `0x00700b40`); q in 51..70: 12;
    else (q - 0) x (12 - 5) / 50 + 5. N is posted (`0x00700b3c` -> `FUN_006b84f0`, `+0x1c`) and the pool is min( N, 30 )
    (`FUN_006b6140`, `0x006b6145`; no lower clamp). The default q 50 gives 12; a boot at graphics detail 0 (q 25) gives 8. Ours
    (`Audio.cs` `MaxVoices` = 32, drop the quietest non-loop) has no counterpart.
  - **Note (fork watch 2026-10-31, verify-audio-fx M2):** an any-distance voice ranks from world (0, 0):
    `0x006bc515`..`0x006bc525`, `0x006bbda0`..`0x006bbda4` (only while a sample plays), `0x006bb9f0`; scale 0.01
    (`0x0070a838`). The key is `0xFFFF - d^2 / 100` and is stored unless d^2 x 0.01 passes 65535 (d above 2,560; a park's
    far corner is 1,810 away), so a stale key is unreachable in a park. Its high word is the priority, so it decides only
    between equal priorities. No voice is pooled unranked (`0x006b62b0` runs `0x006bca10`, which calls slot `+0x1c`, before
    `0x006b6bc0`).
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
  - **Note (play test 2026-10-31):** staff still sometimes walk in place; which state they are in was not caught (use the `staff` census). Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
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
- [ ] **Q1204. A balance file's first refused line ends it. Decode first.** Found by Q54 (`park-engine.md`, "How a key
  finds its global"). `FUN_004017a0` stops a file at the first line it refuses - a key its table does not name, a
  bounded value out of range, a negative in a type-5 key - but which balance key is which type, and its bounds, are
  not decoded. Read every balance family's table record by record (the `PeepInfo` object's at `0x0073fc70` and the
  rest), as Q178b read the item schema at `0x00744b30`. Ten of the eleven shipped balance files hold
  negatives (the jungle's `Easy_Standard.sam` none; `WeatherEffects.QualityForSnowLow -1`, `RegionFX[n].Hunger`, `ThemeEngine.ProceduralTexturingXOffset`,
  `LightNormal.Y`, `WaterStartPos.Y`); if one is type 5 the original drops the rest of that file. Then build the stop
  in `ParkBalance.Layer` (the deviation is said there) and count it. Confirm: a test per shipped balance file naming
  the line it stops at, or none, and the keys a loaded jungle park loses.
- [ ] **Q58. A purchase faces whatever the hand last held. Decode first.** Found by Q39's decode. The original keeps
  one rotation, `DAT_0081d7a4`, and `FUN_0052f200` writes it only for tool 0 (`0x0052f3b6`), so an item bought over a
  carried move faces the moved thing's way; a candidate's or worker's pickup leaves it too. Here a purchase always
  starts at nought, said at `ParkBuilding.CarryingAngle`. Decode what a successful put-down leaves in it (the light
  setter `FUN_0052f580( 0, 0 )`) before building one rotation. Confirm: move a ride turned 90, buy over it, put the
  purchase down, `objects` showing its angle.
  - **Note (fork watch 2026-10-18, `verify-ui-hud.md` U6):** a put-down that reaches `0x005253b8` leaves the rotation at
    0 (`0x0081d7a4`, by `0x0052f1b0( 0, 0 )`) unless exactly Ctrl is held, which keeps it (`park-engine.md`, the paragraph
    on carrying a purchase). Ours already keeps the item in the hand on exactly Ctrl (`ParkBuilding.cs:1319-1324`). Read since
    fork watch 2026-10-31 (`verify-ui-hud.md` UI-031): the branch at `0x00524db1` is a thing whose sub-record's `+0x40` is nought, and the queue tool's
    commit (`0x005275b1`..`0x005275f2`) ends the tool with angle 0 whatever the modifier (`park-engine.md`, the paragraph on carrying a purchase).
  - **Note (fork watch 2026-10-31, verify-ui-hud UI-031):** the put-down keeps the tool and the angle on exactly Ctrl only
    (the whole-word compare, so Ctrl+Shift fails it), at `0x005253b8` and the Move twin `0x005259e2`; the add-on's
    `0x00524d6d` keeps the tool but always resets the angle. The modifier word comes from `0x00486aa0` (GetAsyncKeyState) through `0x004815c0` and the live caller `0x0046c610`, not `0x0046b420` as far as traced (`0x00523f6a`, the replay and four callers of `0x0046b420` not traced). Ours: the put-down arm at `ParkBuilding.cs` (`Input.ControlAlone`); whether a
    Move put-down reaches it was not checked.
- [ ] **Q60. A mechanic put down should look for work at once. Decode first.** Found by Q39's decode. The type-6
  put-down (`FUN_00505ea0`) sets every kind idle but the mechanic, whose claims are cleared (`FUN_004dad20`) and whose
  job search runs there and then (`FUN_004da5b0`); here a mechanic goes idle like the rest, counted as
  `MECHANIC_PUT_DOWN_JOB_SEARCH`. Decode whether idle here reaches the same search a tick later, and what differs.
  Confirm: pick up a mechanic, put them down, `staff` on the next few ticks.
  - **Note (Q874):** `FUN_004dad20` clears more than his own claims: it walks the whole object chain and zeroes the
    assigned member of every object broken down, calling or waiting for an upgrade, whoever held it, with no toilet
    test, and reads neither argument (`park-engine.md`, "A worker held at a save"). The same runs when a mechanic held
    at a save is put back.
- [ ] **Q61. The park-entry fly-in darkens the screen, and nothing here draws it.** Found by Q41's decode
  (`docs/exe/lobby.md`, "Escape cancels the fly-in"). State 2 sets the render camera's `+0x60` to
  `clamp( (1 - (r - 8) / (SPINRADIUS - 8)) x 65536, 0, 65535 )` (`0x005e052f`), states 0 and 1 set it to 0, and
  `0x005d8cb0` eases `+0x5c` toward it by an eighth of the gap per lobby pass and draws a black rectangle over the
  view with alpha `min( +0x5c >> 8, 255 )` (`0x008bd4e4`, drawn at `0x00576e54`). So the flight fades to black as it
  closes on the gate, and a cancel lifts it. Counted as `LOBBY_FLY_IN_FADE`. The ease is per pass, not per second:
  convert it through `Time.SmoothingFactor` and say so (rule 10). Confirm: `step` into the flight, screenshots at
  three radii with mean brightness falling, then Escape and the brightness back within a second.
  - **Note (play test 2026-10-21, note 2):** no golden key animation at entry; the key puff is drawn since Q669, so
    what is left of the note is this fade.
- [ ] **Q62. What View the online world does offline. Decode first.** Found by Q41's decode. `IslandPanel_Callback`'s
  case for button `0x1e0e9` (`0x004b8c18`-`0x004b8c2f`) calls the island camera's `+0x20`, its deactivate
  (`0x005e1bd0`, which Ghidra names `IslandLobby_ViewOnlineWorld`), and then the online-world child's `+0x1c`, which is
  `0x005dd650`, the globe page's activate, with no connection test in front (fork watch 2026-10-28, note below);
  `IslandPanel.ViewOnlineWorld` does nothing, saying the original does nothing offline, and the original does open the
  globe page. Still to read: what that page shows with no connection (its city-list request `FUN_005b6070` is undecoded).
  Confirm: the button pressed, `windows` and a screenshot.
  - **Note (fork watch 2026-10-28, verify-hz-ui-hud L2/M2, verify-hz-advisor A3):** the island panel's View case
    (`IslandPanel_Callback` `0x004b8b70`, message 0x100, jump table `0x4b8c7c`/`0x4b8c90`: `0x1e0e9` -> `0x4b8c18`) calls
    `[[0xf85614]+4]` vtable `+0x20` (`0x005e1bd0`) then `[[0xf85614]+0xc]` vtable `+0x1c`, nothing in front. `[0xf85614]` (built
    by `0x005e3c20` / `0x005e3dc0`): `+4` the island camera (vtable `0x702ec0`), `+8` (`0x702dd0`), `+0xc` the globe child
    (`0x702e70`, size `0x7c`), `+0x10` a second globe child (`0x702e20`), `+0x14` the advisor queue object. The `+0xc` activate
    `0x005dd650`: if not active (`+0x24`), `FUN_004c4fb0( 0 )` (tree `0x7596b0`: enables 0x16007, label skins 0x16008..0x1600c,
    font 6, `Button_SetRepeatDelays( 0, 10 )` on 0x16001, 0x16002, 0x16004, 0x16005, callback `FUN_004c56e0`), `FUN_004b9a70()`
    (tree `0x7581a0`, the hover readout), `Lobby_Start` `0x005dcfe0`, then `0x005d6060` on `[0xf85614]+0x14`; the trees are
    created here, not at lobby start. `Lobby_Start` is the globe scene's start: FOV 60, ambient 0x202020, a white directional
    light, `[0x008bcbc8] |= 0x2000000`, model `data\lobby\globe`, global lobby effects 2 and 3 (ours plays them nowhere since Q770, the globe page being unbuilt: start both at (0, 0, 0) here and stop them at the deactivate), the online object
    `[0x00f7d88c]` made if absent (`FUN_005b50b0`), `FUN_005b6070()` and `FUN_005bc7d0( 0x5dcfc0, this )`, and the fog off
    (`0x005dd04c`, Q502). The return, the panel callback `0x004c53f0` case `0x004c5568`: globe `+0x20`, island `+0x1c`, then
    `0x005d6110( 0x258, 1 )` on `[0xf85614]+0x14` (`0x004c5589`): advisor response 600 queued; the globe's deactivate `0x005dd690`
    stops both voices and clears `0x2000000`. The lobby opens on the globe only when `[0x00f8288c]` is set (`0x005d5b8b`; written
    1 by the menu item 0x13 path `0x0048b9f9`) or `[[0x00f7d88c]+0x108]` is (`0x005d5b81`), else on the island (`0x005d5bf1`).
    Unproven: what the city-list request does offline; whether button `0x1e0e9` is ever disabled (no code names it by id).
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
  Until F8 and the chord are built, count each where it is let go (`AGENTS.md` rule 4): nothing binds either, so the
  press reaches nothing and nothing says so (the 2026-09-26 staleness audit).
- [ ] **Q66. A disabled button takes the pointer.** Found by Q42's review. The original's hit test (`0x0065db25`) skips a
  control flagged `0x2` - a disabled button, `0x0065da8d` - with everything under it, so the pointer passes over it to
  whatever is behind: no hover, no help row, no glint. Here `UiButton.TakesMouse` is true whatever `Enabled` is, said at
  the site; the island panel's greyed arrows for an Instant Action player are the case in the lobby, and a press there
  still ends at the panel's outline, so nothing but the hover differs. Before changing the hit test, check every park
  window whose root takes no pointer: a press on a greyed button there would fall through to the world. Confirm: an
  Instant Action player's pointer on a grey arrow, no help row; a screenshot.

- [ ] **Q240. The postcard. Decode first.** From Q139. The gadget's postcard button is counted
  (`POSTCARD_BUTTON`), Ctrl+P under the full-screen view is counted (`FULL_SCREEN_VIEW_POSTCARD`), Ctrl+P over the
  park (the shortcuts' row 15) is counted (`POSTCARD_SHORTCUT`, `ParkFrontEnd.cs`), and Ctrl+P in first person
  (`FUN_00488a00`, action 15) is read by nothing: count that first. All four reach `FUN_004a9380`: sound `0x95`, the game paused, the interface hidden, a screen chosen by
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
  - **Note (fork watch 2026-10-31, verify-world M10):** the ride camera's basis is not renormalised: the blend
    (`0x0042ca91`..`0x0042cb7d`, nine words) goes to `0x00578bc0` (`0x0042cc93`) with no normalise (`0x0046f150` is an
    identity of a local, `0x004702d0` a plain multiply). A frame whose matrix differs from the last by under 1e-5
    (`0x0074c980`, the sum of twelve) is not copied (`0x0042b070`). Our `ParkRideViewCameraMode` was not compared.

## E. Large
- [ ] **Q31b. The other three object windows: the toilet's, the staff room's and the upgrade's.** After Q31's shop window: the sideshow's, the toilet's and the rest
  of `park-engine.md`'s nine, one window per session. Q31 carries the decoded figures and the `UIStrings` rows.
  From Q251: the staff window's days employed are `FUN_00505b70`, the calendar's now less `mTimeHired` over a
  day, handed to UITEXT `0x1bb` by `FUN_004b58b0` (`saves.md`, "`mTimeHired`"); `Staff.TimeHired` holds it.
  - **Note (fork review 2, 2026-10-16, `verify-ui-hud.md` L3):** the staff window is decoded whole: handler
    `FUN_004b5cb0` (fire, with UITEXT `0x18d`'s box when the options ask; pick up; the patrol toggle, tool `0x36`;
    locate; all staff), arrows walking all staff newest first and wrapping (hidden under two staff), stats refreshed
    every 4000 ms (`FUN_004b58b0`), advisor lines `0xc0`, `0xc1` and `0xd4` posted and withdrawn.
  From the run review (2026-10-17): both person windows are built (Q430, Q431), and the sideshow's and the misc item's are Q960 and Q961, so three of the seven are left, and their
  leftovers belong here; Q420's notes from Q430 and Q431 list them: the visitor's preview (`VISITOR_WINDOW_PREVIEW`) and a
  guest gone (`VISITOR_WINDOW_GUEST_GONE`), the lists' lines `0x11e` and `0x11d`, the staff member's name edit, preview and
  member gone (`STAFF_WINDOW_NAME_EDIT`, `STAFF_WINDOW_PREVIEW`, `STAFF_WINDOW_MEMBER_GONE`), the staff room's row
  (`0x004b5267`) and the patrol switch's second help row (87). New: the base handler `FUN_0048d200` turns the cursor Up and
  Down keys (`0x2600`, `0x2800`) into `b_arup` and `b_ardown` (`hud.md`, "The person's windows"); decode the focus to see
  whether a key reaches the window, then build it or count it (`Unimplemented.Report( "OBJECT_WINDOW_CURSOR_KEYS" )`).
  Duplicates: `ParkLocator.OpenOnMember` copies `OpenOnVisitor`, where the original's is one function, `FUN_004b3f60`,
  branching on the type byte (`0x004b3f84`, table `0x004b4258`): make one `OpenOn( thing )`; and
  `ParkStaffWindow.DaysEmployed` is a third copy of the `( FileTimeOf( now ) - stamp ) / TicksPerDay` divide beside
  `ParkState.DaysSinceStaffHired` and `DaysSinceObjectBuilt`: one `DaysSince( stamp, now )`. Their callers' tests are
  written (Q1031; `docs/history/queue-done.md`, Q211).
  - **Note (fork watch 2026-10-16, `verify-ui-hud.md` H1):** the sideshow's and the ride's windows are decoded too
    (`hud.md`, "The shop, sideshow and ride windows"). Sideshow: price always, then chance of winning (`+0x190`, 0 to
    100) and prize (`+0x188`, 0 to 10000) only when `NonWinnableSideshow` (`+0x10c`) is 0, which no shipped `.sam`
    sets (that branch is dead by content); apply to all is `0xa0a9` with mask `0x100`, the same-item rule. Ride: the
    speed, duration and capacity controls and the apply `0x004af440`.
  - **Note (play test 2026-10-21, notes 12 and 17):** a click on a sideshow, and on a toilet, opens no window. Counted
    `SIDESHOW_WINDOW` and `FEATURE_WINDOW` (`source/OpenTPW/World/Level.cs:2732-2733`).
  - **Note (Q636's review, run 9):** the sideshow window's bars `0xa094`, `0xa096`, `0xa099`, `0xa09e`, the toilet window's
    `0x15bbb` and the resting-staff list `0x1e7b` are inset (0, 10) by `FUN_00485e30` in the original: build them so.
  - **Note (play test 2026-10-31):** clicking a walking member's window shows no figure (`STAFF_WINDOW_PREVIEW` reached). The sideshow window is split out as Q960 and the feature window (the Security Camera) as Q961; this item keeps the rest. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.
  - **Note (fork watch 2026-11-12, verify-other.md L6, verify-ui-hud.md M4):** a guest's delete closes the visitor window when it shows that
    guest and takes the locator panel off the arm when it holds that guest (`hud.md`, "A guest arriving or going": `FUN_004818c0`,
    `0x004b7a00`, `0x004b3620`). Ours closes the visitor window already: retire `VISITOR_WINDOW_GUEST_GONE` into that, and fold
    the locator's arm for a deleted guest (`ParkLocator`, said at the site). The relay does nothing for staff; whether another
    original path closes the staff window on a deleted member is not read, so `STAFF_WINDOW_MEMBER_GONE` stays. The portrait clone
    is one, shared by both person windows (handle `[0x007c24a0]`, person `[0x007c24d8]`; staff 8.0 at depth + 1, `0x004b537a`;
    visitor 12.0, `0x004b6bda`); the staff vtable's `+0x20` (`0x004b6b80`, `jmp 0x00486630`) deletes it.
- [ ] **Q681. Light every model by its file's own normals, as the original does (from Q657). Measure first.** The
  original's lighter takes entry *i* of a mesh's `+0x64` table for vertex-order entry *i* (`Light_ModelVertexColours`
  `0x00574660`, the one routine for every model); OpenTPW lights the advisor by it (Q657) and every other model, the
  park's things, the land's meshes (`TerrainHalfLight`) and the lobby's islands, by its own averaged triangle normals
  (`ModelFile.CalculateNormals`), which on the advisor stood a median 33 degrees off on his hands and 90 on the bow tie.
  Measure first: a placed thing's live normals in the original (a walk like `advlight.py`'s, on its model) against its file's
  table (`lib/normdump`, `lib/normcmp.py`), and count over all 2,129 `.md2` whether every mesh's table covers its vertex
  order (Q657 counted the five advisors alone: FileFormats `models.md`, "Normals"). Then light them by it; confirm a
  park thing's shading beside the original's.
  - **Note (fork watch 2026-10-28, verify-hz-data D1, verify-hz-world H2):** the `+0x64` table is two runs: measured over all
    2,129 `.md2` (847 static models, 4,923 meshes), in 4,707 of the 4,918 meshes with a table it is exactly 12 x (
    vertexOrderLength + faceCount ) bytes (the other 211 have a longer gap, none shorter; 5 have none), one normal per
    vertex-order entry (what `FUN_00574660` lights by) then one per face; every face's first word (low 15 bits) is below
    vertexOrderLength + faceCount in all 4,918 (FileFormats `models.md`, "Normals"). Mesh flag `0x1000000` makes
    `Light_ModelSetup` (`0x0057aa30`, `0x0057aa66`) skip the relight and `DrawList_AddMesh` (`0x00581d56`) read mesh `+0x64` as
    the draw's colour stream; it is set at run time only (`FUN_00469580`, the hover tint `FUN_00469180`), 0 of 4,923 meshes and
    2,606 transform records carry it. A mesh with it must not be lit by normals. The lobby's light is Q769.
- [ ] **Q683. Start a bought thing's create effect (Q663's build).** The script loader starts the item's
  `Info.CreateParticleEffect` (`ItemDescriptionFile.CreateParticleEffect`) once as a bought or moved thing's script is made (a move's put-down
  rebuilds the object through the same constructor: `0x00525417`, `FUN_00528a70`, `FUN_004db090`; `park-engine.md`), never
  at a load or a layout replay: `Particles_Spawn` at the middle of the model's box (`FUN_00466b70`: the model's own
  cells `+0xc0`/`+0xc4` x 10, not the anchor's footprint, Q744's rule) at its base height, then `Particles_SetArea` with
  half the box across and along and a quarter of its height in place of the template's area (`park.md`, "What building
  a thing gives off"). Here `ParkRides.BindNew` counts `CREATE_PARTICLE_EFFECT`; `ParticleSystem` takes no area but the
  template's. Build both, with the same box for the destroy effect's (Q38). Leave the `+0xd4` sound zone alone: uncounted,
  reached exactly with `CREATE_PARTICLE_EFFECT` (`0x00558e81`, the box tests `0x00558ea5` and `0x00558f4d`), an EAX 2.0
  occlusion zone (`park.md`, "What building a thing gives off"; Q38's note), its factor `0x006c4c80` not read. Confirm: the Round Fountain bought in both games, `particles` against `original/emitters.py` (effect 81,
  life 30, most 146 at density 500, about 1.5 s) and the build's first frames side by side (`q663/side-by-side.png`).
- [ ] **Q684. The map's overlay, its six switches and two pickers (Q664's build).** From Q664 (`hud.md`, "The map's
  overlay"; FileFormats `park-map.md`, "The overlay's six sprites"). Build the six layer switches (`0x987`, `0x993`, `0x994`,
  `0x982`, `0x989`, `0x983`; toggles, flags `0x11`, their help pairs) and the two radio groups (`0x98e`, `0x98f`, `0x98c`,
  `0x98d`; `0x998`, `0x997`, `0x999`) on the original's three words, each starting at 1 and kept across openings; the code
  grid painted into the picture at its pixels, with its fills, ramp and outlines (black for a thing that cannot load);
  the patrol rectangle of each shown member; the coaster lines; and the sprite records with their filters, tints,
  `RegionFX` scales and depths, placed again whenever the view moves. Satisfaction's value (`FUN_004e1e30`) is not
  worked out here (its days are, `ParkObjectRings.Satisfaction`): read it, or count `MAP_SATISFACTION` where the metric
  is 3. Replace `MAP_OVERLAY` by what stays unbuilt. Confirm: `q664/orig/run.sh`'s fourteen steps in both games, each
  step's census against `original/mapgrid.py`'s and the picture's diff against `q664/orig/a-*.png` (`original/diffcells.py`).
  - **Note (fork watch 2026-10-28, verify-hz-ui-hud L3):** the rule for a toggle's help lives in the button class, not per screen:
    the base control's vtable slot `+0x2c` returns `+0x90` (op 0x11, `0x0065bc70`), and the button class overrides it
    (`0x00669410`) with `byte [+0x138] == 1 ? [+0x94] : [+0x90]`; `+0x94` (op 0x12) defaults to -1 and is written by `0x00661850`.
    `+0x138` is the state byte (press `0x10005` ORs 2, the release `0x10004` of a toggle, flags `0x11`, flips bit 1 and re-asks
    the help, `0x00669172`, the radio group sets 1 on its pick `0x0066a468`), so a toggle that is on (state exactly 1) shows its
    op 0x12 row and one held down (3) its op 0x11 row; a row of -1 posts the empty text (`HelpBar_PostControlRow` `0x00492070`).
    The map's six switches carry two rows each (`hud.md`); `hud.md`'s staff switch (`b_route`, second help 87) was measured NOT shown (`q431/confirm.py`): reconcile that run with this static reading (state byte exactly 1 while on). The new
    player dialog's own case is Q772.
  - **Note (fork watch 2026-11-12, verify-other.md L1):** the coaster lines are drawn by one of four routines picked by the picture's bits a
    pixel (`0x005f2fd0`, `( [map+0x3c] + 7 ) >> 3`: `0x005edad0`, `0x005ee580`, `0x005ef040`, `0x005efb10`); the picture takes the
    texture cache's format record `0x008bd558 + 32 x [0x008bd554]` (`0x005f1f81`..`0x005f1fcc`), so at the shipped `TEXTURE32 0`
    (`textures.md`) they are `0x005ee580`'s, into A4R4G4B4, with the 32-bit routine's blend (`v = s x c + d x ( 255 − c )` a
    channel, `( ( v >> 8 ) + v + 1 ) >> 8`, `0x005ee79e`..`0x005ee825`), each channel then cut to four bits. The option-to-entry
    link is `textures.md`'s, not re-read; the end point, a zero-length line and the neighbour order are not read.
- [ ] **Q685. The map's hover and its click (Q665's build).** From Q665 (`hud.md`, "The map's hover and click"); needs
  Q684's records and code grid. On each pointer move over the map keep the nearest shown record within 5 game pixels
  (in x and in y, then by distance, the first of equals) and the cell's thing id; repaint the picture when a member is
  hovered or left (his patrol rectangle in (25, 25, 250)); letter `0x991` with the member's name, UITEXT 456 "Visitor
  #{P24}" or 457 "New Visitor" for a guest, else the cell's thing's two name lines joined (`Thing_JoinedName`, as the
  status's row 16), else empty; a record with no thing (a camera's glow, a litter sprite) names the cell's thing as with no record. A click: the record's thing, else the cell's, to the window opener after the close
  (`Level.OpenObjectWindow` for a placed thing, the visitor's and the staff member's windows for people). Replace
  `MAP_HOVER` and `MAP_CLICK_HANDOFF`. Confirm: `q665/orig/run.sh` and `runc.sh`'s points in both games, each hover's
  `mapgrid.py hover` against the console's, the label's crop, and each click's window and camera point.
- [ ] **Q686. Ours' park-entry fly-in looks nearer the island than the original's from about +5 s. Measure first.**
  From Q670 (`q670/side-by-side.png`, columns +5.0 to +6.8 s): the same turn and the same arrival time in both, but from
  the facing on ours' dino fills more of the frame. Not measured. Read the original's eased eye `+0x28..+0x30` and look
  point `+0x40..+0x48` of the island camera (`FUN_005e1210`: both eased at `0.1 x delta` and `[0x00702ca4] x delta`)
  over a flight with `original/lobbycam.py` (add the fields), against ours' `PositionRate` and `LookAtRate`
  (`LobbyCameraMode`) and the island's camera target; the fade (Q61) darkens the original's frames from +4.3 s, so
  compare positions, not pictures. Confirm: the eye's distance a frame at a time in both games.
- [ ] **Q747. A second Escape within about 0.7 s of the park-entry cancel shows the key puff over the game menu. (from the run 11 review of Q669.)**
  Small. `IslandPanel.EnterPark` starts the puff ownerless; the first Escape cancels the flight and reopens the panel
  (`LobbyCameraMode.CancelLeave`), so a second Escape goes to `FrontEnd.MenuKey` (`FrontEnd.cs`), which opens a `GameMenu`
  while the puff is still falling (32, 27, 14, 8 particles for about 0.7 s, `q669/run2/run.log`), and the ownerless puff is
  drawn over the dimmed menu: the case the owner rule was made for. Derived from the code, not observed. Build: keep the
  puff's handle in `EnterPark`; when the cancel reopens the panel make the panel the owner of the live puff, with a small
  `ParticleSystem.SetOwner( int handle, UiWindow? owner )` beside `Hide`, so it is still drawn over the panel and the menu's
  dimming then covers it. Said at the site (`IslandPanel.cs`) and in `lobby.md` meanwhile. Confirm: Enter then Escape twice
  in quick succession, a screenshot of the menu with no keys over it.
- [ ] **Q748. The key sparkle's last particles at park entry are drawn, as the original's. Decode first. (from the run 11 review of Q669.)**
  At the Enter the original's sparkle (effect 97, the handle at `0x7cc4bc`) is stopped with life -2 and its last particles
  fall on screen: 4 still falling at 0.1 s, counts 3, 2, 1, 1 over 0.1 to 0.4 s, gone by 0.5 s (`q630/orig-a/poll.txt`).
  Ours is stopped by the panel's `Closed` (`ShowSparkle( false )`), but its particles stay the closed panel's, so
  `ScreenParticles` does not draw them (`q669/run1/run.log`: 3, 2, 1, 1 "closed (not drawn)"). Decode first what stops the
  original's 97 at the Enter: not `0x004b8fd0`, and not the panel's message `0x14` case (`0x004b8c37`). Then let the stopped
  sparkle's particles go unowned for the Enter alone, as the puff. The owner rule's limit is the same as the item above's (a
  second Escape opens the menu), so settle that first. Confirm: `lib/lobbyentry.py`'s `particles screen` reads effect 97
  "owner no window (drawn over all)" at 0.1 to 0.4 s, and a frame at +0.1 s shows the twinkles at the price beside
  `q630/orig-a/film`.
- [ ] **Q32. Graphics tiers.** `Level` reads the detail files (`low.sam`, `med.sam`, `high.sam`) for `GameOptions.PARTICLEDENSITY`
  and `NUMKIDS` (the kid and staff bank caps) and for three `GraphicalOptions.*` keys: `LIGHTING` (a counted no-op),
  `TEXTUREQUALITY` (the sea's and the flags' folder) and `PROCEDURALTEXTURING` (the ground's tile size). `SPRITESHADOW` is read too (Q480: medium's silhouette built, the own
  picture counted). Nothing reads the rest (texture filtering, sky, the sky's and models' shadows, fog, mipmaps, view distance); `SKYQUALITY` and `FIRSTPERSONVIEWDISTANCE` are named only in comments. The live detail file is loaded once, at boot
  (`FUN_0044dc60`, `lobby.md`'s `0x007858c8` row); `0x00423bc0` (`OptionsScreen`'s restart note) only reads a new
  quality's file into a local to compare it and flag a restart. Decode what it does with each key - the lobby plan's item 11 names three: the `stexture` set,
  `SKYQUALITY` and the particle low-detail byte - then build low / medium / high.
  - **Note (fork watch 2026-10-28, verify-hz-world H3, M1, M2; verify-hz-other O3):** SKYQUALITY: `DetailFile_Apply`
    (`0x0044d9f0`..`0x0044da17`) copies it to the sky's `+0x2ec8` for 0, 1, 2 and 4 only (3 is not stored: the object default
    stays, 4 unless the renderer `[0x8bd524]` is 4, `FUN_00584ef0`); `FUN_005863c0` returns at once for 0 (neither clouds nor
    band), else the layer count is that value (1 when render word `0x8`); shipped low 1, medium 2, high 4; the layers' alphas are
    0xcf, 0x97, 0x5f, 0x27. LOBBYOBJECTS (`GameOptions`, `[0x7858dc]`): the lobby flock's constructor `FUN_005d9db0` (`0x5d9e22`)
    sets its quota `+8` = count x LOBBYOBJECTS / 100 (signed, truncating), clamped to 1..count; ours reads none (`LobbyFlyer.cs`
    spawns `mesh.Count`). The flock's update `FUN_005da020` makes one actor per frame while live `+0xc` is below the quota,
    stepping the lobby generator `[0xf82884]+0x34` (`x*0x19660d + 0x3c6ef35f`, stored rotated right 13) and stopping when the
    rotated value `& 7 == 1` (and not drawing for the last actor).
- [ ] **Q33. UI scale.** The UI has one fixed virtual size (`VirtualScreen`, 2048 by 1536). Add Auto / small / medium
  / large. The Video Card row it once named is taken (Q983), so where it goes is Alexah's call.
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
- [ ] **Q73. Cache pipelines and assets by key (the rest of the review's Phase D).** The pipeline half is done (Q954: materials of one shader and flags share one,
  `SharedCache`); what is left is the texture and shader caches that scan `Asset.All`
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
    1] before the acos. Not read: the bend's tables, `0x00435570`, the per-pylon tag counts (`0x00432df0` is read: `park-engine.md`, "A coaster's pylons").

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
    by the ride's sample count (`+0xa4`; `+0xa8` is a float coordinate, fork watch 2026-10-31, `verify-world.md` M4). Build it with that gate, the one `audio.md`, "What an EventMap's slots feed",
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
  - **Note (fork watch 2026-10-28, verify-world W2):** `SetTrainSoundState` `0x439780`: `0x4392a0` stores EventMap slots 5..8 at
    train `+0x54`, `+0x58`, `+0x5c`, `+0x60` (`0x4392c4`..`0x4392ff`), plays slot 0 into `+0x64` (`0x43932f`) and slot 1 into
    `+0x68` (`0x439363`) and, when its third argument is nought, calls `0x439780( train, 2, 100 )` (`0x439376`). `0x439780` maps a
    new state 1, 2, 4, 8, 0x10 to 15, 30, 70, 90, 50 and sets parameter id `[+0x5c]` (slot 7) on the voice `+0x68` to it; every
    call then sets parameter id `[+0x60]` (slot 8) on `+0x68` to its third argument; it plays nothing, and is skipped while
    `[0x785a2c]` is set. Its one per-frame caller `0x43d7d8` (in `0x43ce20`): train `[+0] & 0x10` gives state 8 with ( `fistp(
    [ebp-0x38] )` + 1 ) x 50; else no counted cars or `[coaster+0x2c] == 0.0` gives state 0x10 with 100; else `[train+0x78] /
    [coaster+0x34]` above 0.3 (`[0x6fe078]`) gives state 4, at or below state 2, each with count x 100 / `[[train+4]+4]`; state 1
    is never passed. The saver writes `+0x38`, `+0x64`, `+0x68` (`0x43780a`..`0x437821`) and the loader puts them back raw
    (`0x4383fe`..`0x438412`). `coaster1`'s EventMap slot 7 = `VAR_PAR2` 19, slot 8 = `VAR_PAR3` 0 (`coaster3`, `minecart`: 19 and
    22). Not read: what `[ebp-0x38]`, `[+0x78]`, `[+0x34]` and `[+0x2c]` are; slots 5 and 6.
  - **Note (fork watch 2026-10-31, verify-audio-fx H1):** the scream pass is `0x004393e0`, its position pass `0x00439570`,
    its teardown `0x00439670`; the rating step `0x0043dc50` sets sample bit 4 (normal) and 2 (terror) from
    `fNormalScreamStartExciteDelta` and its three siblings (`+0x34c`..`+0x358`, defaults 0.5, 1.0, 10, 20: no shipped
    coaster `.sam` sets them), so flag 2 implies flag 4 and the "started again on every pass" one-shot (`0x004394e8`) is
    dead by CONTENT. Live: one one-shot at the stretch's first sample, moved while it lives, started afresh when it ends
    (`0x0051c270`'s return, `0x0043946f`) while flag 2 holds. `0x00551320( 1, 100, 100 )` is step 3 of band 1, kids effect `0x4E` (`0x005513be`); the chain is
    `0x47` for 1 rider, `0x48` for 2 to 5, `0x49` for 6 to 13, `0x4A` from 14, parameter 6 = 75
    (`0x00551149`..`0x0055122e`). Saves keep train flag `0x100` (`0x004377f4`, `0x004383da`) but not `+0x6c`, `+0x70`.
    Unproven: whether Temple Of Gloom's samples carry flags 2 and 4 (it needs a running park); which car train `[+8]` holds.
  - **Note (fork watch 2026-10-31, verify-world H1, M1, M3, M4, M5):** a bend car starts no clip: `0x0043bed2`..`0x0043bed6`
    tests the car type's flag bit 0 and only otherwise calls `0x004732a0( model, 5, 0, 1, 1.0, 0 )` (`0x0043bef2`, the only
    caller in `0x430000`..`0x44c000` (a range)); the idle default cannot start it (the car is made with flags `0x101`,
    `0x00436e86`, so `0x00473c70` skips role 5). Not excluded: a script's `TRIGANIM` arms (`0x00551cb0`, `0x0055aa80`). The bend stores vertices only (no store but stack in `0x00438800`..`0x00438a00`; `0x00438a16`..`0x00438a22`), then `0x0044ab30` (`0x00438a53`); its callees `0x00438ab0`, `0x00438a70`, `0x004314f0`, `0x00431570` were not read in full. The release walk `0x0043c1f0`
    wraps by fmod of the float `+0xa8` (`0x0043c395`) and its sample index by the integer `+0xa4` (`0x0043c315`). A load
    keeps train flag `0x100`; the ring `+0xb0` is unsaved (`0x00437300` reads no `+0xb0`..`+0xe4`); no loader caller of
    `0x0044b410` hangs heads. `0x0043bd20` is not a list setter (it walks model tags 1 to 255 calling `0x0044b4c0`, from
    `0x00437068`, `0x00437202`). The constants of `0x0043c450` are `0x0043c52d`..`0x0043c657`; the collision exchange is (T
    + 2O) / 3 and flag `0x40` goes on the other train only (`0x0043cde8`). The oracle's 231 rows were not run.
- [ ] **Q406. Coaster building: the track tool.** From the fork review 2, 2026-10-16 (`verify-coasters-build.md`
  L1-L11). A piece costs `fCostPerHeight` × H × 0.1 + `fCostPer2DLength` × length × 0.1, plus `fCostPerUnit` only on
  placing a pylon (`0x00447830`, `0x00435020`; the cursor's preview `0x00435d50`); placing takes trunc( cost ), a
  height or move release gives back trunc( old ) and takes ceil( new ); finishing, removing and deleting the last move
  no money, and nothing tests the balance. The edit handlers (height 0.005 a unit, rotate 0.02 within
  ±`fMaxRotnAngleDeviance`, bank and incline 0.02, loop tilt 0.01 in [0.15, 0.85]), auto-bank ((±π - turn) × v / 4.0
  over the neighbours' distances; `fAutoBankDivisor` is read by nothing), the sampler (0.05 steps, spacing 3.0) and
  the closing pair, the end node, the place distance (3 to 8 cells, 3 to 9 for a loop pylon, `0x0042fd90`). A click of
  the track tool plays `cat_ui 0x65` before `0x00446300` (mode `0x15`, `0x005263f1`..`0x00526423`; `park-engine.md`,
  "The laying sound"). The validator `0x00434740` never refuses a click. Open: what hides the loop button on `coaster1`; whether Backspace
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
  - **Note (fork watch 2026-10-31, verify-ui-hud L2, verify-other M1 to M3):** the coaster panel rides the arm: `0x004988c0`
    (`0x00498909`..`0x0049891b`) calls `0x004a25f0( 1 )` then `0x004a2590( [0x007ca1c8], 0 )`, only while `[0x00785a2c]` is
    nought (always, offline); `0x004989d0` is the close, same gate; the arm ends at left 486, top 1145. Any arm takeover
    ends the tool: the other `0x004a2590` callers (`0x0048dab3`, `0x00498bb8`, `0x004b3fae`, `0x004b42a3`) and every
    `0x004a25f0` go through the arm's message `0xb`, which sends the carried panel `0x001001d8` (`0x004a17dc`); its arm
    (`0x00498688`..`0x00498702`) runs `0x00446fa0( 0, 4 )`, `0x00497f00`, `0x004474d0( 0, 0 )`, clears `[0x007ca1cc]` and
    calls `0x004862a0`. Help rows: `0x134` with `cb_trough`, else `0x132` with `cb_loop` (`0x004987f0`; the rows' text not
    read). The definition's loop bit is read at `0x0042fd57`; the enable is mask `0x00445910` bit 2, set from definition bit
    `0x80` (`0x0042fa4a`) and cleared when the controller's `+0x3c` has bit 0 without bit 1 (`0x00445a4a`..`0x00445a57`).
    Range marks: `0x00444090` sets `0x1000000` on [low, high), `0x00444040` clears with `0xfeffbfff`; which texture each
    variant takes (1, 3) is unread.

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
  - **Note (play test 2026-10-31):** Jurassic Tours still does not run (`TOUR at 9/12/15/18 ...` reached). Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.

- [ ] **Q409. Full Simulation's loans and challenges.** From the fork review 2, 2026-10-16
  (`verify-economy-guests-paths.md` L1, L5). A loan offer passes when its amount is at most the net worth less what
  the bought loans still owe, signed, kept in the offer and rebuilt on opening and each tab (`0x0049fa00`,
  `0x004a06b0`, `0x004d06b0`). A won challenge with a follow-up pays no prize, and the follow-up is marked won as it
  is offered, so one declined or lost never returns; declining a challenge that is not Independent pays half its
  prize; the pacing reads `DaysAfterCompletedChallenge` and `DaysAfterDeclinedChallenge`; nothing raises the declined
  count, so `DeclinesToForfeit` is never reached (`0x004d2b60`, `0x004d2fb0`, `0x004d1a00`, `0x004d3140`,
  `0x004d1ae0`, `0x004d2ec0`). Neither screen is built.
  - **Note (Q243's census, moved here by the run 22 review):** the counted names this item owns: `LOANS_SCREEN`
    (`ParkCategoryScreens.cs`; `hud.md`) and the challenges' posts `GATE_FEE_CHALLENGE_POST`, `COST_OF_GOODS_CHALLENGE_POST`
    (`ParkState.cs`; `ride-operation.md`), `CHARGE_CHALLENGE_POST` (`ParkRideOperation.cs`) and
    `RESEARCH_FINISHED_TO_THE_CHALLENGES` (`ParkResearch.cs`; `park-engine.md`).
  - **Note (fork watch 2026-10-16, `verify-world.md` L13, `verify-ui-hud.md` M1):** a shop's sale feeds the challenges
    by its `ShopType` 1 to 6 (`0x004e18e4`: types 5, ingredient, 8, ingredient, 7, 6) and `SpecialIngredient` 1 to 4
    (`0x004e18fc`: types 2, 1, 3, 4), posted by `0x004d27a0`; `0x004ca160` is the mean over toilets of 100 less a
    float's low byte, only for `0x004d2860`. The loans window: row pitch `(h x 0x600) / [0x00faa5c0] + 6` from a font
    of 7; summary labels UITEXT `0x165`, `0x168`, `0x167`, `0x169`; take `0x0049f4a0` and repay `0x0049f460` refill
    only the list `0x0012c4bd`; the tab is remembered for the session (`[0x007cb260]`, control `0x0012c4bf` or
    `0x0012c4c0`).
  - **Note (fork watch 2026-10-27, verify-ui-hud M2):** the loans window opens three pausing message boxes, each a call of
    `FUN_0047f020`: UITEXT 181 at `0x0049f66a` ("There is ${P1} outstanding on this loan. Pay the outstanding balance ?", its tick
    `0x0049f460` -> the pay-off `0x004d0850`), 182 at `0x0049f6fe` ("You do not have enough money to pay the outstanding balance of
    ${P1}", no tick) and 183 at `0x0049f7ad` ("Take out a loan for ...", its tick `0x0049f4a0` -> the drawdown `0x004d0750`). Not Lost
    Kingdom: Instant Action runs only in type 2, where the loans opener returns at once (`0x0049fb8e`).
  - **Note (fork watch 2026-10-18, `verify-advisor.md` A1):** the credit test `0x004d06b0` is read in `ride-operation.md`, "The
    loan helpers and the money rows": limit = net worth x 1.0 less the total still owed (instalments left x instalment over the
    taken offers), an offer passing when its amount is at most it, and the pass flag **written into the offer** on every call, so
    an advisor poll of row 102 rewrites the flags. A loan taken counts as cash in for the advisor's row 107. Rows 100 and 107
    are polled in Instant Action too (Lost Kingdom reaches them); 101 and 102 are Full Simulation's alone.
  From Q327: the original said row 107's `0x6b` (score 90) on tick 3633 of `q327/FSOPEN.TPWS` and `FSFRESH80.TPWS`,
  its fresh Full Simulation park saved on 3513 (no loan, $48,180, $1,400 spent); this game counts the row
  (`PARK_ADVISOR_POLLED_ROW_NOT_BUILT`).
  - **Note (fork watch 2026-10-31, verify-ui-hud L1):** an offer's tag removal declines it: tag message 5 reads property 6
    and, at `0x1b`, calls `0x0048e0f0( 0x1b, 0, 0 )` (`0x0048db8b`..`0x0048dbab`) for the 33 rows 247 to 279 (life 90000).
    Every route reaches it: the timer `0x80080` (`0x0048dd53`), the right click (`0x0048e079`), an answer's result 2
    (`0x0048e544`) and the top's delete-all `0x100003`. **So an offer left alone for 90 s pays half its prize when not
    Independent** (`0x004d2f1d`..`0x004d2f33`) and sets the next date; an answered offer's decline is a no-op
    (`0x004d2e2b`). A full tube strands an offer: `0x004d2c50` answers 1 whether or not a tag was made, `0x0048ec11` refuses
    an eleventh, so `+0x3da` is set (`0x004d2b49`) with no tag, and `0x004d22bf`, `0x004d2a8c` then stop every later offer
    (by the listing, not run; an original defect). `0x0048ee20` sends every held tag message 5 at the load and at teardown:
    whether that lands on a live challenge object is unproven. `0x0048ede0`'s branch is dead by CODE.

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
  - **Note (Q781, 2026-10-28):** the two walks are decoded and built (`park.md`, "The pieces' clips";
    `ParkTrackTool.PlayPieces`, f = 1 plays role 5 looped, 0 rests): BUMP 17 is the walks from both mouths after its
    `+0xe6` test (`0x00554a1a`..`0x00554a47`), and it reads the thing itself, not a bumper record (`esi`, `0x0055470b`), so it
    can be built before the rest of the water ride's `BUMP`. Until it is, Splish Splash's water never flows by its script.

- [ ] **Q449. Building over scenery, and tearing down a water track.** From the fork watch, 2026-10-16
  (`verify-world.md` C2 and C3). (1) The placement check (`0x00535d66` to `0x00535d73`) passes a new thing over
  another's cells only when its `Info.OverwritePriority` is **strictly greater** than the owner's (`0x00818c10`,
  `0x00818c14`). Every category file declares 5; of the item files, features alone set it, 45 at 1 and 48 at 0
  (FileFormats `sam.md`, which lists Lost Kingdom's); the path and queue tools write 2 and 3 (`0x00527200`,
  `0x00527218`). Our catalogue reads the key (`ParkItemCatalogue`'s `Item.OverwritePriority`, run 12 review)
  and the track tool answers blue over scenery below 3 and takes it down (Q773); nothing else uses it: decide what a click on scenery with a ride or shop selected does here, then
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
  unbuilt (`ParkMapScreen.cs`; its view, zoom and scroll are built, Q624, and the overlay's build is Q684); its colours are immediates in `0x005f0700` (`hud.md`, "The map's colours"); the
  channel order (D3D, B, G, R, A in memory) and which record colours which code, sprite and line are settled by Q664
  (`hud.md`, "The map's overlay"), so (2) is Q684's. (3) The frame routine brackets the
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
  of `THOUGHTS.str` the frame plus one, refreshed every 1000 ms. An icon hidden for no thought stays hidden until the window is
  rebuilt (fork watch 2026-10-31, `verify-ui-hud.md` L6; `hud.md`, "The icons"). Confirm: the screen photographed beside the original's in the shipped park.
  - **Note (fork watch 2026-10-20, `verify-other-ui-audio.md` A-L1):** the graph's backdrop is decoded in `hud.md`, "The graph's
    backdrop": the plot box (the control's size less 20, from 4), the colours, ten grid rows, the columns' step of 1, 3 or 12 for 12,
    36 or 144 samples (any other count leaves the step unset, an original defect), the zero axis when ymin is below nought, and the
    `%d` labels. Not read: the `0x7d00` added to each label's place.
  - **Note (fork watch 2026-10-31, verify-ui-hud L3, L6):** a series' line is `0x0047fe90`, oldest sample first (not
    newest), x from the plot's left in steps of width / count, so a line shorter than the period stops part way and starts
    at 0; the newest value is printed `%d` in the series colour at its end; written in `hud.md`, "A series' line". The
    period buttons call `0x0047fb10( 0, count - 1, count )`. The Top 3 icon stays hidden until the window is rebuilt
    (`hud.md`). Unproven: whether a reopened Finance grid stays 12. The Finance series is Q989.

## F. Then

Back to the advisor in a park; saving a fresh park is Q783, in section A. Also litter and the day ending, whose deferral reasons expired
(`docs/REVIEW-2026-09-21.md` section 6).
- [ ] **Q648. Halloween World: guests stick at the crosswalk.** Alexah's own play, 2026-10-22: the park otherwise ran.
  Out of scope until Lost Kingdom is whole (`CLAUDE.md`, the goal); kept so it is not lost. Reproduce in `hallow` with a
  `guests` census at the crossing, then find what the walk asks of the crosswalk cells there.
  - **Note (play test 2026-10-31):** still seen in a short look at Halloween World; not the priority. Notes and log in `~/.cache/tpw-harnesses/playtest-2026-10-30_2026-10-31/`.

- [ ] **Q1190. Counted, not built, and owned by no item: the interface (21 names, from Q243).** Filed by Q243's census,
  2026-11-12: `python3 tools/gap_census.py` found each name below counted by `Unimplemented.Report` and named by no other item
  here. A session takes one name or a few: read the site and the `docs/exe/` page given, classify the path (`AGENTS.md`
  rule 3; only a site's own "dead by" words were read, given below, and the rest are not classified), then build it or
  file it as its own item and take the name out of this list. The census drops a name once an item names it. Each name:
  the file of each call, then the `docs/exe/` pages that name it.
  - `BOOT_SPLASH` (`Game.cs`; `boot.md`, `ui.md`); cut by the lobby plan (section G), counted so the census names it: not to build
  - `BOOT_LEGAL_SCREEN` (`Game.cs`; `boot.md`, `ui.md`); cut by the lobby plan (section G), counted so the census names it: not to build
  - `INTRO_MOVIE_BULLFROG` (`Game.cs`; `boot.md`); cut by the lobby plan (section G), counted so the census names it: not to build
  - `INTRO_MOVIE_PARK` (`Game.cs`; `boot.md`); cut by the lobby plan (section G), counted so the census names it: not to build
  - `*_*_READ_BARE` (`Localization.cs`)
  - `LIST_HEADING_SORT_NOT_BUILT` (`UiList.cs`; `hud.md`)
  - `LIST_SCROLLBAR_NOT_BUILT` (`UiList.cs`; `hud.md`)
  - `LIST_CLICK_MISS_SELECTION_NOT_UNDER_POINTER` (`UiList.cs`; `hud.md`)
  - `LIST_ROW_ACTIVATED_UNANSWERED` (`UiList.cs`; `hud.md`)
  - `LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED` (`UiList.cs`; `hud.md`)
  - `ALL_STAFF_SCREEN` (`ParkCategoryScreens.cs`); dead by CODE at its site: the work is removing the unreachable arm (`AGENTS.md` rule 3), not building it
  - `ALL_VISITORS_SCREEN` (`ParkCategoryScreens.cs`); dead by CODE at its site: the work is removing the unreachable arm (`AGENTS.md` rule 3), not building it
  - `UNKNOWN_CATEGORY_SCREEN` (`ParkCategoryScreens.cs`); dead by CODE at its site: the work is removing the unreachable arm (`AGENTS.md` rule 3), not building it
  - `TAG_BAR_BUTTON` (`ParkGadget.cs`; `hud.md`, `advisor-park.md`)
  - `STAFF_PORTRAIT_ACTOR` (`ParkHireScreen.cs`)
  - `RIDE_EXCITEMENT_BAR` (`ParkObjectWindow.cs`; `park-engine.md`)
  - `RIDE_RELIABILITY_BAR` (`ParkObjectWindow.cs`; `park-engine.md`)
  - `STAFF_LIST_ADVISOR_LINE` (`ParkStaffScreen.cs`; `hud.md`)
  - `VISITORS_LIST_ADVISOR_LINE` (`ParkVisitorsScreen.cs`; `hud.md`)
  - `TEXTURE_WRAP_SHARED` (`LobbyModel.cs`; `render-states.md`)
- [ ] **Q1191. Counted, not built, and owned by no item: guests' turns and events (37 names, from Q243).** Filed by Q243's census,
  2026-11-12: `python3 tools/gap_census.py` found each name below counted by `Unimplemented.Report` and named by no other item
  here. A session takes one name or a few: read the site and the `docs/exe/` page given, classify the path (`AGENTS.md`
  rule 3; only a site's own "dead by" words were read, given below, and the rest are not classified), then build it or
  file it as its own item and take the name out of this list. The census drops a name once an item names it. Each name:
  the file of each call, then the `docs/exe/` pages that name it.
  - `SAVED_CURRENT_ARRIVAL_VEHICLE` (`ParkPeople.cs`; `saves.md`, `park.md`)
  - `VEHICLE_MADE_NO_ITEM` (`ParkPeople.cs`; `saves.md`)
  - `SPRITE_STATE_ANIMATION_NOT_STARTED` (`ParkPeople.cs`; `ride-operation.md`)
  - `RIDE_CONDEMNED_EVENT` (`ParkPeople.cs`; `saves.md`, `ride-operation.md`, `advisor-park.md`)
  - `PARK_DOOR_COMMANDS_THE_GATE` (`ParkPeople.cs`; `park-gate.md`)
  - `SETTLE_UP_EVENT_HISTORY` (`ParkRideOperation.cs`; `ride-operation.md`)
  - `SETTLE_UP_ANALYSER_SAMPLE` (`ParkRideOperation.cs`)
  - `SETTLE_UP_SIDESHOW_EVENT` (`ParkRideOperation.cs`; `ride-operation.md`)
  - `SETTLE_UP_COSTUME_EVENT` (`ParkRideOperation.cs`; `ride-operation.md`)
  - `SETTLE_UP_BALLOON_EVENT` (`ParkRideOperation.cs`; `ride-operation.md`)
  - `SETTLE_UP_TOILET_EVENT` (`ParkRideOperation.cs`; `ride-operation.md`)
  - `SETTLE_UP_TOILET_ILLNESS_EVENT` (`ParkRideOperation.cs`; `ride-operation.md`)
  - `OPEN_GUARD_COASTER_TRACK_RECORD` (`ParkRideOperation.cs`; `ride-operation.md`)
  - `RIDE_EXCITEMENT_COASTER_TRACK` (`ParkRideScore.cs`; `ride-operation.md`)
  - `RIDE_EXCITEMENT_UPGRADE_TIER` (`ParkRideScore.cs`; `ride-operation.md`)
  - `GOING_TO_RIDE_STUCK_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `DECIDE_CHOSEN_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `DECIDE_NOTHING_CHOSEN_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `DECIDE_WATCH_FIREWORKS` (`PeepBehaviour.cs`; `ride-operation.md`); dead by CONTENT at its site
  - `DECIDE_WATCH_ENTERTAINER_EVENT` (`PeepBehaviour.cs`)
  - `LEAVE_ROUTE_MODE_1_RETRY` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `ARRIVAL_BACK_OF_QUEUE_LOST_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `ARRIVAL_NO_ROOM_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `ARRIVAL_TOO_EXCITING_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `ARRIVAL_NOT_EXCITING_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `ARRIVAL_TOO_LONG_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `QUEUE_PLACE_DODGY_DIRECTION` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `QUEUE_TURN_COASTER_TRACK_RECORD` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `QUEUE_TURN_HEADING` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `QUEUE_TURN_BOREDOM` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `DOOR_EVENT_HISTORY` (`PeepBehaviour.cs`)
  - `QUEUE_CAPACITY_UNKNOWN_ITEM` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `QUEUE_CAPACITY_UPGRADE_TIER` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `MINOR_DECISION_EVENT` (`PeepBehaviour.cs`; `ride-operation.md`)
  - `DOOR_PRICE_ANALYSER_SAMPLE` (`PeepPriceOpinion.cs`; `ride-operation.md`)
  - `NEEDS_THOUGHT_4_SOUND` (`Thoughts.cs`; `ride-operation.md`)
  - `NEEDS_THOUGHT_LITTER_SHARE` (`Thoughts.cs`; `ride-operation.md`)
- [ ] **Q1192. Counted, not built, and owned by no item: staff, strikes, research and the advisor's posts (14 names, from Q243).** Filed by Q243's census,
  2026-11-12: `python3 tools/gap_census.py` found each name below counted by `Unimplemented.Report` and named by no other item
  here. A session takes one name or a few: read the site and the `docs/exe/` page given, classify the path (`AGENTS.md`
  rule 3; only a site's own "dead by" words were read, given below, and the rest are not classified), then build it or
  file it as its own item and take the name out of this list. The census drops a name once an item names it. Each name:
  the file of each call, then the `docs/exe/` pages that name it.
  - `PARK_ADVISOR_SAVED_SLOT_NOT_BUILT` (`ParkAdvisorMessages.cs`; `advisor-park.md`)
  - `TAG_ROW_NOT_LISTED` (`ParkTags.cs`; `advisor-park.md`)
  - `TAG_TEXT_KIND` (`ParkTags.cs`; `saves.md`, `advisor-park.md`)
  - `SAVED_STAFF_CANDIDATE_OF_NO_KIND` (`ParkStaffPool.cs`)
  - `SAVED_STAFF_CANDIDATE_ON_POINTER` (`ParkStaffPool.cs`)
  - `ROLLED_STAFF_COSTUME` (`ParkStaffPool.cs`; `park-engine.md`)
  - `ADVISOR_PARK_ENDED_IN_THE_RED` (`ParkState.cs`; `ride-operation.md`)
  - `STRIKE_CALLED_OFF_TO_THE_ADVISOR` (`ParkStrikes.cs`; `ride-operation.md`)
  - `STRIKE_WARNING_TO_THE_ADVISOR` (`ParkStrikes.cs`; `ride-operation.md`)
  - `STRIKE_TO_THE_ADVISOR` (`ParkStrikes.cs`; `ride-operation.md`)
  - `STRIKE_HANDYMEN_CELL_RATIO` (`ParkStrikes.cs`; `ride-operation.md`)
  - `HANDYMAN_LITTER_SEARCH` (`StaffBehaviour.cs`; `staff-wandering.md`, `ride-operation.md`, `advisor-park.md`)
  - `HANDYMAN_TOILET_AIM_LEFT_ON_A_LATER_TOILET` (`StaffBehaviour.cs`; `ride-operation.md`)
  - `STAFF_TRAINING_NO_POINT_COST` (`ParkPeople.cs`; `ride-operation.md`)
- [ ] **Q1193. Counted, not built, and owned by no item: paths, queues, building, markers and hoardings (30 names, from Q243).** Filed by Q243's census,
  2026-11-12: `python3 tools/gap_census.py` found each name below counted by `Unimplemented.Report` and named by no other item
  here. A session takes one name or a few: read the site and the `docs/exe/` page given, classify the path (`AGENTS.md`
  rule 3; only a site's own "dead by" words were read, given below, and the rest are not classified), then build it or
  file it as its own item and take the name out of this list. The census drops a name once an item names it. Each name:
  the file of each call, then the `docs/exe/` pages that name it.
  - `FRESH_PARK_PATH_ART_VARIANT` (`FreshPark.cs`; `saves.md`, `park-engine.md`)
  - `BOUNDARY_HOARDING_UNMADE` (`ParkBoundaryHoarding.cs`; `park.md`)
  - `BOUNDARY_HOARDING_ROW` (`ParkBoundaryHoarding.cs`)
  - `MARKER_RIPPLE_SHADING` (`ParkBuildMarkers.cs`)
  - `MARKER_ICON_TURNS_WITH_CAMERA` (`ParkBuildMarkers.cs`)
  - `MARKER_LIFT_OVER_TRACK_THING` (`ParkBuildMarkers.cs`, `ParkItemHeights.cs`; `park.md`)
  - `SHAPE_KIND_*_PLACEMENT` (`ParkBuilding.cs`)
  - `FIREWORKS_SPENT_REGION_EFFECT` (`ParkBuilding.cs`; `saves.md`, `ride-operation.md`)
  - `QUEUE_STUB_OVER_SOMETHING` (`ParkBuilding.cs`)
  - `PATH_STUB_OVER_SOMETHING` (`ParkBuilding.cs`)
  - `QUEUE_MODE_ADVISOR_MESSAGE_0xCB` (`ParkBuilding.cs`)
  - `COASTER_PYLON_BANK_TURNED_NODE` (`ParkCoasterTrack.cs`)
  - `ITEM_VALUE_REFUSED` (`ParkItemCatalogue.cs`; `park-engine.md`)
  - `HMP_REBUILD` (`ParkItemHeights.cs`)
  - `QUEUE_RUN_OVER_A_LOW_PRIORITY_THING` (`ParkPathBuilding.cs`)
  - `PATH_RUN_OVER_A_LOW_PRIORITY_THING` (`ParkPathBuilding.cs`)
  - `PATH_TOOL_ADVISOR_MESSAGE_0xB8` (`ParkPathBuilding.cs`)
  - `PATH_RUN_OVER_TRACK` (`ParkPathBuilding.cs`)
  - `BACKSPACE_DELETE_SOUND_0x5F` (`ParkPathBuilding.cs`)
  - `EDIT_QUEUE_ADVISOR_0xCB` (`ParkPathBuilding.cs`)
  - `QUEUE_DRAIN_ADVISOR_0xCB` (`ParkPathBuilding.cs`; `ride-operation.md`)
  - `QUEUE_DRAIN_DEBIT_DEPRECIATION` (`ParkPathBuilding.cs`; `ride-operation.md`): built by Q1054, the count gone
  - `QUEUE_END_WALK_UNBOUNDED` (`ParkPathBuilding.cs`; `ride-operation.md`)
  - `QUEUE_DRAIN_CLEARS_ANOTHER_KIND` (`ParkPathBuilding.cs`; `ride-operation.md`)
  - `BACKSPACE_QUEUE_ADVISOR_0xCB` (`ParkPathBuilding.cs`; `ride-operation.md`, `park-engine.md`)
  - `BACKSPACE_QUEUE_CLEARS_ANOTHER_KIND` (`ParkPathBuilding.cs`)
  - `PATH_TILE_NO_TABLE_ROW` (`ParkPathTiles.cs`)
  - `SCENE_HEIGHT_OWN_MESH` (`ParkSceneHeight.cs`; `park.md`)
  - `HOARDING_DEGENERATE_BASE` (`RideHoardingGeometry.cs`; `ride-hoardings.md`)
  - `SHAPE_CHARACTER_NOT_IN_ALPHABET` (`ItemDescriptionFile.cs`)
- [ ] **Q1194. Counted, not built, and owned by no item: sound (7 names, from Q243).** Filed by Q243's census,
  2026-11-12: `python3 tools/gap_census.py` found each name below counted by `Unimplemented.Report` and named by no other item
  here. A session takes one name or a few: read the site and the `docs/exe/` page given, classify the path (`AGENTS.md`
  rule 3; only a site's own "dead by" words were read, given below, and the rest are not classified), then build it or
  file it as its own item and take the name out of this list. The census drops a name once an item names it. Each name:
  the file of each call, then the `docs/exe/` pages that name it.
  - `PARK_LOOP_FUN_0055AB50` (`ParkAudio.cs`; `park-engine.md`)
  - `RIDE_EFFECT_SOUND_CLASS_6BE330` (`ParkAudioScripts.cs`)
  - `RIDE_EFFECT_SOUND_CLASS_6BE680` (`ParkAudioScripts.cs`)
  - `RIDE_EFFECT_SOUND_HELD_CHAIN_NO_KEY` (`ParkAudioScripts.cs`; `audio.md`)
  - `RIDE_EFFECT_SOUND_PARAMETER_OTHER_CLASS` (`ParkAudioScripts.cs`; `audio.md`)
  - `SOUND_PLACE_NO_CELL_RECTANGLE` (`ParkObjects.cs`; `ride-operation.md`)
  - `SOUND_GAP_BY_PARAMETER` (`ParkScreams.cs`)
- [ ] **Q1195. Counted, not built, and owned by no item: ride scripts: particles, walks, model nodes and loaded scripts (31 names, from Q243).** Filed by Q243's census,
  2026-11-12: `python3 tools/gap_census.py` found each name below counted by `Unimplemented.Report` and named by no other item
  here. A session takes one name or a few: read the site and the `docs/exe/` page given, classify the path (`AGENTS.md`
  rule 3; only a site's own "dead by" words were read, given below, and the rest are not classified), then build it or
  file it as its own item and take the name out of this list. The census drops a name once an item names it. Each name:
  the file of each call, then the `docs/exe/` pages that name it.
  `STARTSCREAM`, `SCREAMLEVEL`, `SPAWN`, `DIPMUSIC`, `SETOBJPARAM` and `SETBROKE` are a built instruction's message (the census's
  MESSAGE): the counted case is the one the site's message names (wrong operands, nothing to work on, a value not handled).
  - `PARK_PARTICLE_ITEM_EFFECT` (`RideScript.cs`; `park.md`)
  - `PARK_PARTICLE_NO_PLACE` (`RideScript.cs`; `park.md`)
  - `PARK_PARTICLE_NO_DIRECTION` (`RideScript.cs`; `park.md`)
  - `PARK_PARTICLE_NOT_STARTED` (`RideScript.cs`, `ParkBumperBoats.cs`, `ParkPlacedObjects.cs`, `PersonPuffs.cs`; `park.md`)
  - `LOADED_EMITTER_LIFE_PART_RUN` (`RideScript.cs`; `park.md`)
  - `WALK_FACING_REST_POSE` (`RideScript.cs`; `saves.md`, `ride-operation.md`)
  - `WALK_FACING_ON_A_FACE` (`RideScript.cs`)
  - `WALK_FACING_NODE_MISS` (`RideScript.cs`)
  - `WALK_NODES_NO_MODEL` (`RideScript.cs`; `ride-operation.md`)
  - `WALK_LEG_REST_POSE` (`RideScript.cs`; `ride-operation.md`)
  - `WALK_NODE_ON_A_FACE` (`RideScript.cs`; `ride-operation.md`)
  - `WALK_NODE_MISS` (`RideScript.cs`; `ride-operation.md`)
  - `WALK_NODE_UNPOSED` (`RideScript.cs`; `ride-operation.md`)
  - `WALK_NODE_NEGATIVE_ID` (`RideScript.cs`; `ride-operation.md`)
  - `STARTSCREAM` (`RideScript.cs`; `park.md`, `addresses.md`, `ride-operation.md`, `audio.md`)
  - `SCREAMLEVEL` (`RideScript.cs`; `park.md`, `addresses.md`, `ride-operation.md`, `audio.md`); dead by CONTENT at its site
  - `SPAWN` (`RideScript.cs`)
  - `DIPMUSIC` (`RideScript.cs`; `park.md`, `park-engine.md`, `audio.md`)
  - `SETOBJPARAM` (`RideScript.cs`; `saves.md`, `park.md`, `addresses.md`, `audio.md`)
  - `ADDHEAD_DRAWS_EXHAUSTED` (`RideScript.cs`; `park.md`)
  - `WALK_RIDER_NODE_MISS` (`ParkGuestSprites.cs`; `ride-operation.md`)
  - `RIDER_HEAD_BANK_MISSING` (`ParkGuestSprites.cs`)
  - `SCAPE_OMP_SOUND_TYPE_3` (`ParkPlacedObjects.cs`; `park.md`)
  - `SCAPE_OMP_SOUND_TYPE_1` (`ParkPlacedObjects.cs`; `park.md`)
  - `LOADED_COMPANION_NOT_STARTED` (`ParkRides.cs`; `saves.md`)
  - `SAVE_PARK_SCRIPT_ENDED` (`ParkRides.cs`; `saves.md`)
  - `WALK_RIDER_ACTION_UNPLACED` (`WalkRiders.cs`; `ride-operation.md`)
  - `WALK_RIDER_HEIGHT_FROM_THE_SURFACE` (`WalkRiders.cs`; `ride-operation.md`)
  - `WORLD_PARTICLE_ADDED_WITHOUT_ALPHA` (`WorldParticles.cs`; `park.md`)
  - `MODEL_NODES_AFTER_ROLE_NOUGHT_ENDED` (`AnimTimeControl.cs`; `saves.md`)
  - `SETBROKE` (`RideState.cs`; `park.md`)
- [ ] **Q1196. Counted, not built, and owned by no item: track rides and bumper rides (15 names, from Q243).** Filed by Q243's census,
  2026-11-12: `python3 tools/gap_census.py` found each name below counted by `Unimplemented.Report` and named by no other item
  here. A session takes one name or a few: read the site and the `docs/exe/` page given, classify the path (`AGENTS.md`
  rule 3; only a site's own "dead by" words were read, given below, and the rest are not classified), then build it or
  file it as its own item and take the name out of this list. The census drops a name once an item names it. Each name:
  the file of each call, then the `docs/exe/` pages that name it.
  - `BUMPER_CAR_NO_MESH` (`ParkBumperBoats.cs`)
  - `BUMPER_CAR_NO_SMOKE_NODE` (`ParkBumperBoats.cs`; `park.md`)
  - `BUMPER_CAR_ROCK` (`ParkBumperBoats.cs`)
  - `BUMPER_CAR_WAKE` (`ParkBumperBoats.cs`; `saves.md`)
  - `SAVED_TRACK_CAR_MODEL_NOT_ITS_OWN` (`ParkBumperBoats.cs`; `saves.md`)
  - `BUMPER_TEMPLATE_*` (`ParkBumperCars.cs`)
  - `BUMPER_CAR_OTHER_TARGET` (`ParkBumperCars.cs`)
  - `BUMPER_CAR_UNJOINED_TARGET` (`ParkBumperCars.cs`)
  - `BUMPER_RIDER_PARTICLE` (`ParkBumperCars.cs`; `park.md`, `park-engine.md`)
  - `SAVED_TRACK_RIDER_WITH_NO_CAR` (`ParkTrackRideTable.cs`; `saves.md`)
  - `SAVED_TRACK_RIDE_OUTSIDE_THE_TABLE` (`ParkTrackRideTable.cs`; `ride-operation.md`)
  - `TRACK_SECTION_OUT_OF_OBJECTS` (`ParkTrackRideTable.cs`; `park.md`)
  - `TRACK_SECTION_UNKNOWN_TYPE` (`ParkTrackRideTable.cs`; `park.md`)
  - `TRACK_SECTION_TEMPLATE_*` (`ParkTrackRideTable.cs`)
  - `TRACK_RIDE_TABLE_FULL` (`ParkTrackRideTable.cs`; `ride-operation.md`)

## G. The lobby plan's open items

From the lobby plan Alexah agreed on 2026-09-12, "Finishing the Lobby" (its artifacts are listed in
`docs/history/README.md`). Alexah asked that its report be kept in use: when an item here lands or the order changes,
update it with the Artifact tool's `url` (https://claude.ai/code/artifact/48cf6fc2-0874-404c-b7bf-a05fe4b16470), never
a second copy. Items 1-4,
6-8 and the gate half of 5 landed; item 11 is Q32. Cut from the lobby that day and still cut: the intro movies and
splash, the three online UI trees, cones, reverb and Doppler (the original switches Doppler off on every voice measured, so nothing is cut there: fork watch 2026-10-31, `verify-audio-fx.md` M1), the message box's third button, and the original's full
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
  - **Note (fork watch 2026-11-12, verify-advisor.md A4):** the front end's setup noughts the stamp on every entry and the clock is ms since
    Windows started (`ui.md`, "The lobby's idle repeat"), so an arm that begins on a free frame queues the nag at once. Q77's
    sitting tests it: the lobby with no player, the greeting over, pick a player, time the first 469/470. Whether picking a
    player makes him speak (which would stamp) is not read.
- [ ] **Q78. The lobby's sea from `lobby/Terrain/base.md2` (plan item 9). Decode first.** The lobby has a sea: a
  `Water` entity (`Level`) that `Sky` seals the horizon against. The item is re-sourcing it from `base.md2` through the
  park terrain loader - but `Sky`'s own note says the original's lobby has no sea below its islands, which contradicts
  the premise. Settle whether the original draws `base.md2`'s sea before building anything.
  - **Note (fork watch 2026-10-28, verify-hz-world M3):** the lobby's scene constructor calls the park terrain loader
    `FUN_004504c0( 1, 0 )` at `0x5d8503` after naming the "lobby" folder (`0x5d84c7`, `0xf816c0`), which loads `%s\Terrain`
    `base.md2` (`0x450523`) into `[0x79fcb4]`, the terrain base model a park uses: the original's lobby loads it as its land
    model. Not read: whether its sea meshes are drawn each frame.
- [ ] **Q1208. Ease the render rate and the music while the window is unfocused (Q79's leftover).** From Q79's split.
  Input is dropped while unfocused (Q42), but the loop still draws as fast as it may and the music plays on at full
  volume. Settle first what the original does on `WM_ACTIVATEAPP` (its frame pacing and its sound) from the disassembly,
  then match it, or say the deviation at the site.
- [ ] **Q1212. A save round trip against a real game-written file (the rest of Q80, plan item 13).** From Q80, split in
  cloud lane c23. A park file the original wrote, read by ours and written back, compared with the original's bytes
  where ours claims to keep them; it wants a sitting with the original to make the file.
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
  the opened gate. **Measured (Q670):** the island camera's `+0x18` at state 2 on Lost Kingdom is 4.71239 and the island's
  `+0x14` 1.57080 (`q670/orig-b`, `orig-c`, `original/lobbycam.py`), and the original's frame at its facing shows the
  isle from the same side as ours' at ours' pi (pier to the left, gate square on, `q670/side-by-side.png`), which is the
  isle turned by its heading. **It also carries the flight's length:** the turn ends on pi where the original's ends on the
  island's heading + pi, so until this is built Lost Kingdom's entry is up to 3.14 s off for the same wait at the panel
  (ours shorter for waits of 3.93 to 19.63 s mod 31.42 s, by the whole 3.14 s for 7.85 to 15.71 s, longer otherwise), and
  the panel's orbit at a given wait shows the isle from 90 degrees away (the original's view angle relative to its isle is
  the orbit's less 1.5708, ours' the orbit; one facing pair measured, not at other angles). Then turn the isles and gates, set `GateHeading` from the island, and fix the sun in
  `Level.SetupEntities`, which stands in front of gates that all face one way. Lost Kingdom's island first. Confirm: the
  lobby with the island and gate of Lost Kingdom beside the original's, and the leave's "turning" and "arrived ... after"
  log lines after a fixed wait at the panel equal the original's (`lobbycam.py`) from the same wait.
- [ ] **Q575. A plain voice's first silence, measured in the original.** From the run 6 review of Q563 (`handoff/review6/VERIFY-18.md`,
  Q563 2). Read from the listing and built (fix group P): every start of a plain or music voice, the first and a start
  in range again included, draws its variation's silence (`FUN_006bc410` -> slot `+0x48` `0x006bdf00` -> `0x006c5220`),
  where Q302/Q553 had the first followed at once (`audio.md`, "The plain class"). Predict before the run: the land's gulls
  (global ambient 2, one variation, wait 0 to 10000 ms) in the stock park, with the camera on the harbour, put their
  first sample's channel and their second's 0 to 10 s apart beyond the first's length, not at once; and the bus's voice
  back in range (waits nought) unchanged. Confirm in both games (`mixer.py`, `voices.py`; ours `scriptsounds` and the
  `Park audio: the land's own ambient 2 ... ms of silence after it` lines, which now print on the first too).
- [ ] **Q1244. 188's set-up draw in the running game.** From Q576 (a cloud lane, no Ghidra). The listing question is
  settled by the run 22 review: a held chain, fresh or put back, takes the nought at its first child (`0x006bbe93` clears
  the flags, `0x006bdb29` is the only writer of `0x20`); a keyed chain of any variation would range by nought after it
  (`0x006bc650` -> `0x0070a834` = 0.0), and none ships (ours' deviation is said at `ParkAudioScripts.NextChainChild`).
  Confirm 188 in the running game: Q576's stock-park runs (190 s, camera on the Staff Room) never started it, so
  start it (a staff in the Staff Room) with the listener 87 to 89 from it and read its "its own variation N of range R"
  start line against whether its first child comes.
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
- [ ] **Q694. Move to the newest NeoVeldrid (1.3.1 on 2026-10-26).** Alexah, 2026-10-25: 1.3.0 released 2026-10-24, 1.3.1 by
  2026-10-26 (`lib/nvwatch.py` reports newer ones); we are on 1.2.1 (`OpenTPW.Common`'s
  and `OpenTPW`'s references). 1.3.0's notes list Native AOT and trimming support (#104) and a backend-aware
  window creation in StartupUtilities (#92), with no breaking change and nothing in Vulkan, SDL2, SPIRV, texture formats,
  samplers or present (https://github.com/jhm-ciberman/neo-veldrid/releases/tag/v1.3.0; the compare diff not read).
  Read the compare diffs `v1.2.1...v1.3.0` and `v1.3.0...v1.3.1` (and the 1.3.1 notes, not yet read) first; bump every NeoVeldrid package together; build, test, and photograph
  the lobby and a park before and after at one camera (pictures equal within a frame's own spread).
- [ ] **Q696. The lobby's rain count follows the camera's distance to the rainy islands.** From the fork watch 2026-10-25 (verify-forks.md
  section 1; low priority: the lobby, not Lost Kingdom's park). In `FUN_005e0470` (`0x005e0e29` to `0x005e0f18`) the original works
  the drop count out every frame: over every island whose RAINY (`+0x60`) is not zero (RAINY is a flag; its value is never
  read), it takes the mean m of `dx^2 + dz^2` from the camera (`[0x00f83778] + 8`, `+0x10`) to the island (`+0x3c`, `+0x44`),
  then `v = 1e17 / m^4` (`[0x00702c88]`), 0 under 20, at most 400, and hands the count to `FUN_0057d910`. Rain starts about 91.7 units
  away and is full inside about 63.1 (arithmetic); at the orbit's SPINRADIUS 70 it is about 173 drops (arithmetic, not
  measured). Only hallow is RAINY, so it is the distance to Halloween's island whichever island is current; lightning
  still follows the current island (`0x005e0f28`). Ours is a flat 900 drops (`Rain.DropsPerLevel`) whenever Halloween is current,
  said at `LobbyWeather`. Build the original's count; confirm in the running lobby (the live drop count at one camera against
  the number predicted from the camera's distance), and in the original if it can be read. Pairs with Q1050 (the flash) and Q1035 (the bolt's shape).
  - **Note (fork watch 2026-10-28, verify-hz-audio-fx A3):** the lobby's bolt (all from `FUN_005e0470`'s weather tail,
    `0x005e0f1d`..`0x005e11b2`): the sky point is (cx + U x 100 - 50, 500, cz + U x 100 - 50) with U = (r & 0x3fffffff) x 2^-30
    (`0x00702c58`, 100 `0x00702c94`, 50 `0x00702c98`) and the island's `+0x3c` centre, the ground point (sky.x + U x 20 - 10, 0,
    sky.z + U x 20 - 10) (`0x00702c8c`, `0x00702ca0`), then `FUN_00580320( ground, sky, 500 ms, 1 )`, the flash `0x0057dc70` and
    thunder `FUN_0051bfc0( 0, [0x00803a48], 1, 0, 0, 0 )`. A seeded bolt's stroke brightness (`0x0057eed1`..`0x0057ef61`): phase =
    (lifeLeft % 500) x 0.002; phase below 0.9: b = 255 - (int)( (0.9 - phase) x 1.11 x 64 ); phase 0.9 or over (a bolt's first 50
    ms): b = 192 + (int)( (1 - phase) x 640 ); then b += `rand() & 63`; the colour is 0xFF000000 | b << 16 | b << 8 | b with no
    mask (b reaches 318 and spills into the next channel); an unseeded bolt is white. The drop cube and the stroke jitter use the
    C runtime's `rand()` (`0x0067b5c0`); the strike, ambient and pick draws use the scene's generator `[[0xf82884]+0x34]`.
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

- [ ] **Q855. What Q749's fix left in the lobby's first seconds: the passes' clamp, and the seed's picture.** (1) The lobby's other passes
  take ours' 0.1 s frame clamp where the original's is 0.5 s (Q749's review, finding 2). (2) The original draws the seed's picture, the
  sea from straight above, for its first frames before the island camera's first pass (`docs/exe/lobby.md`, "The orbit's seed"; its film's
  first panel); ours' first drawn lobby frame is already the first pass, eye half way and look on the island (`q749-fix/after/quit-sheet-fullrate.png`,
  the loading screen then the island at pitch 28 degrees), where the build before the fix drew the sea (and then panned slowly). To match, the first
  frame would be drawn at the seed and the cap taken by the pass after it, the step counted from the build's end.
  On (1): the original clamps every lobby pass's milliseconds at `0x1f4` (`0x005d5c6f`) and multiplies by 0.01, so a pass after a
  long frame steps up to 0.5 s; Q749's fix gives an orbit's first pass after a build that cap (`LobbyCameraMode.FirstPassStep`,
  `min( Time.RawDelta, 0.5 )`, blends linear), but the attract flight's own first pass (a launch's first) and every later pass
  of either mode still take `Time.Delta`, clamped at 0.1 s (`Time.LongestFrame`). It shows only after a stall over 0.1 s
  (the flight, at 10 units a second, steps 1 unit where the original's steps 5 after a half-second hitch); nothing placed reaches it in a normal
  run. Decide whether the lobby, as the one scene that clamps at 0.5 s, takes its own limit (`docs/exe/lobby.md`, "The
  orbit's seed"); a measurement of the original's flight across a stall comes first.
  (3) The first lobby frame after a quit, found by the review of Q749's fix (`q749-fix/after/quit.mkv`, about 1.6 s in, lavapipe, about 0.1 s): two film
  frames read "Park Name" over a flat cyan sky with no clouds, the next "Lost Kingdom" and the normal sky; the original's first panel reads Lost
  Kingdom and its film has no such change. The label is proven in code: `Level.Unload` (`Level.cs`, `ForgetIsland`) nulls `CurrentIsland`,
  `IslandPanel` reads it in `Level.Update`, and the camera sets it only later, in `Level.Render`; the control shows the same label. The cloudless
  sky is newly seen because the fix's first frame looks at the sky where the control's looked at the sea; its cause is not traced (the sky's or
  weather's first update). Give the panel the island before the first `Update` when a player is current (the lobby build setting `CurrentIsland`
  from `IslandIndex`), and find which pass sets the sky's tint.

- [ ] **Q845. Another theme's gates and lights are written as placed objects (from the Q887 review, finding 2).** `Level.cs` (about line 950) passes the fixed cell only when `ParkFixedItems.IsGatesOrLights` (`ParkFixedItems.cs`, ids 1601 and 1603 only), so hallow's gates and lights (2601, 2603) are saved as ordinary placed objects with `PlacedObjectFlags` `0x32f` (`ParkThingStates.Made.cs`); the 2026-10-31 hallow saves under `~/Games/TPWorld/save/users/2FullSim Test/hallow/` hold three such records. The original's own files carry these items as `0x361`, and `saves.md` (about 1149) records that the original drew no gate for such a record (`q783/orig/l2`). Key the fixed items by theme (fantasy's 4600 to 4603 too), deferred by scope until a theme other than Lost Kingdom is taken up; confirm by saving a hallow park and reading the records' word.
