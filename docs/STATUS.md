# Status

Last updated: 2026-10-30. **This header names no branch and no sha, deliberately**: a line in the commit that moves the tip cannot name it. Read the current state from the repository, which cannot lag: `git log --oneline -1`. **`docs/QUEUE.md` is the work queue**, one item per session, taken from the top unless Alexah reorders.

## Works

One line per system; the detail, item by item, is `docs/WORKS.md` (grep it by area or Q-number, never read it whole).

- Lobby: the four islands, the front end, players and their saves, options, the advisor, weather, the island gate and the fly-in.
- The park's world: ground (each cell cut on its own diagonal, Q821), paths, queues, placed and fixed objects, a played coaster's pylons (Q817), people, balloons, the build markers' glide and the camcorder's eye on the original's height samplers (Q823), the pointer's pick on the original's two planes a cell (Q824), the boundary wall, sky, sea, weather, music, the calendar on the park's own clock, lighting, the camcorder.
- Building: the buy and hire screens, paths and queues laid by pointing, a ride carried over its squares and placed with its queue, a bought thing joining the running park with the original's next id, a kart or water ride's station and mouths and every track piece drawn, and its track laid run by run with the track tool, taken back a run at a time with Backspace, its circuit walked as the loop closes, each section with its collision objects and buoy, its open end pulsing red while the loop is open, each run shown by its squares before the click, lifted and walled as the original's, a run over a feature taking it down, an add-on's end caps shown and hidden by the put-down's walk (seen once in ours: the Tunnel's caps hid at the click), an add-on put down on its own ride's track by the original's snap, the hand turned to nought after it, the pieces' clips after a close and an add-on's put-down.
- Guests: eight kinds arrive, choose by the original's score, queue, ride, buy and pay; 23 of Lost Kingdom's 31 rides, shops and sideshows run for them (the website's ride list).
- Staff: all five kinds hired and working their kind's decide (cleaning, performing, researching), strikes, their voices.
- Rides: every placed thing runs its script, the rest of the opcodes counted; screams, particles, hoardings; the ride window closes and reopens its ride.
- Running the park: the gadget, the info and money screens, the entry price and the gate, the research lab and its screen, golden tickets, the advisor's messages.
- Saving: Save, Load, Restart Park, the quicksave keys; a played park with a coaster's pylons loads whole, its gate's doors open and its boundary wall drawn (Q810); a Full Simulation park started empty, saved too (Q783: its file at tick nought, which the original loads).
- The original runs under Proton as the reference instrument.

## Does not

One line per system; the detail is `docs/DOES-NOT.md` (grep it by area or Q-number, never read it whole).

- Park and saving: no loan screen, litter or day's end; a written park holds no kart, water or tracked ride, and some voices, puffs and the music voice are not written (Q420, Q803); forty-four of the advisor's 155 polled messages are built.
- Map and windows: the map's overlay, hover and click are decoded, not built (Q684, Q685); five of the nine object windows are unbuilt (Q31b).
- Guests: the deciding turn's vomit, litter, fireworks and pranks and four queue turns are counted (Q224 to Q226, Q228); a failed route is not revived (Q104); queues lose more happiness than the original's (Q746).
- Building: a carried member of staff shows nothing; a bought thing's sparkles are not drawn (Q683); the buy screen's panel has no model (Q158); a played coaster's track is not drawn (Q594), nor a stacked or loop pylon (Q820); the face normals the original works out again at a placing light nothing (Q819), but its pose walk recomputes a morphing parent's face normals before a face-anchored head reads one, which ours does not (Mumbo's heads off by 1.1 to 3.1 units in his morph clips, by decode; Q848).
- Lobby, keys and interface: the fly-in's fade (Q61) and F8 (Q65) are unbuilt; some keys and lists act differently from the original's (Q120, Q231, Q232); some meshes are drawn both-sided (Q613). No video or networking.
- Sound: a per-effect repeat delay that is really a priority (Q43); most of a park file's live voices are counted, not put back (Q741).
- Staff: the handyman's litter search and the guard's chase are unbuilt; a strike's posts are counted (Q212, Q229).
- Rides: the isles' random clips, the idle repeat, a coaster's excitement and the Hot Pot's wake are counted (Q76, Q77); a water ride's pieces flow only after an add-on's put-down (Q448).
- Camcorder: entered where the orbit looks, not by a click, and walks at 40 units a second where the original's measures about 21.8 (Q25); three of the four ride views are counted (Q246).

## Next

`docs/QUEUE.md`, from the top; its order, and who set it, is there. Every ticked item is in `docs/history/queue-done.md`.

## Not verified on screen

Only the item just landed is here. Every earlier item's account is in `docs/history/not-verified.md`, newest first, moved there by the maintainer's pre-commit hook: grep it by Q-number.

- Q827 (sounds as 16-bit words). Measured on the final binary (dll md5 `90170100`), `q827/run3` and `q827/audio-final`: the desk only, the stock park and Alexah's Full Simulation park 30 s in, a frame of each (`run3/*/park.png`: mean 85.2 and 83.2, the same as the build before's 85.0 and 83.1); the advisor's `sp_203` in the game's own mix, 1.20 times quieter at its peak than the build before's (`audio-old`, `audio-final`), its rms the same; the mixer's scale put back out in a copy reads peak 1.0 and a gain of 4.3 (`audio-mutant-v1`). Not verified: off-screen under lavapipe (lane B's display; Q954's 0.88 and 0.95 GB are from before this); a Release build; the sound itself by ear (the capture is its measure); every one of the 336 clips past full scale in a mix (the test proves the stored words, one clip the mix); the same clips in the original's mix (its sample pass was not captured here, the clamp is the decode at `0x006c9270`); the four themes' parks other than Lost Kingdom.



## Numbers (take counts fresh; these go stale within a day)

| | | measured |
|---|---|---|
| Opcodes | **78** of 106 | 2026-10-20, Q553 (`FADEOBJ`), `case Opcode.` labels vs enum members |
| Tests | **4388**, 0 fail, 2 skip with the game (the macOS and Windows tests of `UserFoldersTests`, run on Linux) | 2026-10-30, Q827 |
| Tests without the game | **1692** ran, **2696** skipped, of 4388 | 2026-10-30, Q827 |
| Build warnings | **0**, `--no-incremental` (none silenced: the nullable ones fixed at their cause, ModKit and its ImageSharp advisories removed, the test SDK past its Newtonsoft.Json advisory); 115 before | 2026-10-29 |
| Memory, jungle park, 30 s in | **0.49 GB** resident (peak 0.54 at the load) on the desk, stock park (0.56 and 0.62 before Q827's 16-bit sounds), with 1,033 pipelines of 13 distinct states, the game's GPU account 161 MB, video memory 0.28 GB and `mem vulkan` 204 MB; Alexah's Full Simulation park **0.52 GB** (peak 0.56; 0.59 and 0.63 before Q827). Off-screen under lavapipe, stock **0.82 GB** (peak 0.88; **6.81 GB** before Q954's pipeline sharing) and Full Simulation **0.92 GB** (peak 0.95; over 12 GB before). Live managed objects 271 MB in the Full Simulation park, 73 MB of them decoded sounds as 16-bit words (`q827/run3/fs/heapstat.txt`; 345 MB and 146 MB of floats before), the `Byte[]` 146 MB (Q826); the off-screen figures here are from before Q827 | 2026-10-30, Q954 (`q954/F1`-`F4`) and Q827 (`q827/run1`, `run3`, desk, final binary; `mem`, `lib/memmap.py`) |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |
| Park load, Release | **3.1 to 3.4 s**, worst phase `audio` 1.04 s (`ground` 0.52 to 0.57, `terrain` 0.46 to 0.55, `objects` 0.35 to 0.47) | 2026-10-26, three runs, `perf/bench.sh` |
| Frame, stock jungle park, Release | **1.23 ms** a frame uncapped (about 810 fps, 18 guests) on the RX 6700 XT, the display's cap 143.9 fps held; **16.3 KB** allocated a frame (54.5 KB before the allocation pass), a gen 0 collection about every 1.3 s uncapped (every 0.37 s before), so about every 7 s at the display's 144 fps; worst frame about 24 ms; the CPU is the limit (89% of the frame on-CPU): 62% `Model.Draw` (NeoVeldrid's `FlushNewResourceSets` 23% of the frame), 11% `Level.Update`. The pass took the walk-head iterator, the boxed object enumerators, the furthest-first sort's copies, the picker's strings and `Unimplemented.Report`'s lock out of the frame and did not move the frame time (seven runs each: 1.230 ms, SD 0.005, before; 1.234 ms, SD 0.028, after); what is left to allocate is the shadow arrays and the bubble's closures (about 2.5 KB), `NodePoser.Pose`, the UI meshes, `ParkFlags.Grid` and the materials' resource sets | 2026-10-27, `perf/ab.sh` (`docs/TOOLING.md`) |

## Recent

Each finished item's account is its ticked entry in `docs/history/queue-done.md`; the dated accounts this section held
until 2026-10-28 are in `docs/history/status-recent.md`.
