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

- Q838 (no code changed; the build markers' corners at a flagged ground cell, the original's own mesh against ours, Q820's piece (4) in part). Measured in the original under Proton (off-screen, stock park, `markerup.sh`, two launches with the pointer on (63,32) and (65,30); `markercorners.py`, the clock held, the glide words held at nine offsets each; `q838/orig/run1`, `run2`): the drawn corners' height is wave + the sampler + 1.5 (`park-engine.md`), and the sampler so read is ours `ScapeHeight` at all 81 corner points in the flagged cells met (worst 0.0000; `ours5`, `ours6`, desktop, binary `OpenTPW.dll` md5 `6c5ba840` = main `3c93bbce`), the near corner's plane (the rule before Q823) off by up to 0.50 at 44 of them. Ours at the same camera: `strip` and a frame mid-glide (`q838/ours4`), the original's with the glide held (`run1/frame-B-glide5_5.png`, 39,348 pixels from the rest frame). Not verified: a person or a balloon at a flagged cell in a frame (Q820 keeps it); a square over a cell with a lift; other themes; `ScapeHeight` against `CameraHeight` (equal on every point seen; they differ only on 14 unflagged bent cells, none in view); the heights of ours' own drawn vertices were not read (the console's `ground` at the original's points, and Q823's unit test of `CornerGround`). 1 of 6 predictions missed, and the second launch's three held (a 0.001 tolerance against a 0.0014 wave-table step, cause checked), in `PREDICTION.txt`.


## Numbers (take counts fresh; these go stale within a day)

| | | measured |
|---|---|---|
| Opcodes | **78** of 106 | 2026-10-20, Q553 (`FADEOBJ`), `case Opcode.` labels vs enum members |
| Tests | **4414**, 0 fail, 2 skip with the game (the macOS and Windows tests of `UserFoldersTests`, run on Linux) | 2026-10-30, Q836 |
| Tests without the game | **1716** ran, **2698** skipped, of 4414 | 2026-10-30, Q836 |
| Build warnings | **0**, `--no-incremental` (none silenced: the nullable ones fixed at their cause, ModKit and its ImageSharp advisories removed, the test SDK past its Newtonsoft.Json advisory); 115 before | 2026-10-29 |
| Memory, jungle park, 30 s in | **0.42 GB** resident (peak 0.50 at the load) on the desk, stock park (0.43 and 0.51 before Q830's WAD entries kept from their second ask; 0.46 and 0.53 before Q829's ground pixels dropped; 0.49 and 0.56 before Q828's WAD files read on demand; 0.56 and 0.62 before Q827's 16-bit sounds), with 1,033 pipelines of 13 distinct states, the game's GPU account 161 MB, video memory 0.28 GB and `mem vulkan` 204 MB live in 384 MB of chunks the library never gives back, with a staging pool of 37 MB after the first park (Q834, `docs/DECISIONS.md`); a park keeps 75 WAD entries, 1.9 MB (2.4 before Q831, which no longer counts the lobby's asks toward it), and 77 / 1.9 MB when entered again after the lobby (356 and 9.7 MB before Q831; `q826/new-stock`); the lobby after a park keeps the park's 337 textures, 13.5 MB on the card, and stands 48 MB resident above its start, compacted (Q832, kept by design: `docs/DECISIONS.md`); Alexah's Full Simulation park **0.45 GB** (peak 0.51; 0.46 and 0.53 before Q830, 0.50 and 0.56 before Q829, 0.52 and 0.59 before Q828, 0.59 and 0.63 before Q827). Off-screen under lavapipe, stock **0.82 GB** (peak 0.88; **6.81 GB** before Q954's pipeline sharing) and Full Simulation **0.92 GB** (peak 0.95; over 12 GB before). Live managed objects 271 MB in the Full Simulation park before Q828's 22.7 MB of WAD files, 73 MB of them decoded sounds as 16-bit words (`q827/run3/fs/heapstat.txt`; 345 MB and 146 MB of floats in `q954/run12/heapstat.txt`, an earlier binary), the `Byte[]` 146 MB (Q826; 33 MB of it, the ground's pixels, gone with Q829: managed total 198 to 165 MB stock, 219 to 190 MB Full Simulation; the WAD entries cached 11.7 to 2.4 MB stock and 15.2 to 3.5 MB Full Simulation with Q830: 166.4 to 155.7 and 189.9 to 176.5 MB); the off-screen figures here are from before Q827, Q828, Q829 and Q830 | 2026-10-30, Q954 (`q954/F1`-`F4`, `run12`), Q827 (`q827/run1`, `run3`) Q828 (`q828/base-stock`, `new-stock`, `base-fs`, `new-fs`) Q829 (`q829/base`, `new3`, `fs-base`, `fs-new3`) and Q830 (`q830/base-stock2`, `new-stock`, `base-fs`, `new-fs`, desk, final binary; `mem`, `lib/memmap.py`) |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |
| Park load, Release | **3.1 to 3.4 s**, worst phase `audio` 1.04 s (`ground` 0.52 to 0.57, `terrain` 0.46 to 0.55, `objects` 0.35 to 0.47) | 2026-10-26, three runs, `perf/bench.sh` |
| Frame, stock jungle park, Release | **1.23 ms** a frame uncapped (about 810 fps, 18 guests) on the RX 6700 XT, the display's cap 143.9 fps held; **16.3 KB** allocated a frame (54.5 KB before the allocation pass), a gen 0 collection about every 1.3 s uncapped (every 0.37 s before), so about every 7 s at the display's 144 fps; worst frame about 24 ms; the CPU is the limit (89% of the frame on-CPU): 62% `Model.Draw` (NeoVeldrid's `FlushNewResourceSets` 23% of the frame), 11% `Level.Update`. The pass took the walk-head iterator, the boxed object enumerators, the furthest-first sort's copies, the picker's strings and `Unimplemented.Report`'s lock out of the frame and did not move the frame time (seven runs each: 1.230 ms, SD 0.005, before; 1.234 ms, SD 0.028, after); what is left to allocate is the shadow arrays and the bubble's closures (about 2.5 KB), `NodePoser.Pose`, the UI meshes, `ParkFlags.Grid` and the materials' resource sets | 2026-10-27, `perf/ab.sh` (`docs/TOOLING.md`) |

## Recent

Each finished item's account is its ticked entry in `docs/history/queue-done.md`; the dated accounts this section held
until 2026-10-28 are in `docs/history/status-recent.md`.
