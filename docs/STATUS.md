# Status

Last updated: 2026-10-01. **This header names no branch and no sha, deliberately**: a line in the commit that moves
the tip cannot name it. Read the current state from the repository, which cannot lag: `git log --oneline -1`.
**`docs/QUEUE.md` is the work queue**, one item per session, taken from the top unless Alexah reorders.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate, and the attract camera
  flying around all four islands with all four heard at once. Enter swings the camera onto the gate, opens it and flies
  in before the loading screen; **the island keys wait for that flight**, and **Escape cancels it** (the camera orbits
  again, the gate shuts, the panel comes back). **Enter, the arrows and Escape act on their release**, and
  **a left press on the lobby's view enters the park**.
- Park: ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (5 of 6), **lit per
  vertex as the original lights it** (ambient colour + clamped sun; the lobby keeps the old lighting). The
  camcorder walks the original's sweep pass for pass, **draws the peeps from their ground-level `.FPC` pictures**, and
  **a quick right click leaves it** (RMB cancel on). Leaving one lets go of all of it: nothing of a left park is held in the lobby, nor of any left scene's interface.
- Building and staffing: purchase menu and hire screen, both reachable from Buy. Things bought, sold, moved, carried;
  staff hired, fired, picked up, put down. **Selling or moving a thing puts its riders and queuers off where they
  stand**, staff resting there get up, and its script goes with it. **Cutting a queue puts out whoever stands past its
  new end.** A staff drop the park refuses keeps the candidate. **The hand holds one thing and lets go of it the
  original's ways** (quick right click with RMB cancel on, Escape, Delete, the camcorder, a new pickup, leaving); a moved
  thing stays in it until a cell takes it. A placed ride's window opens from a click **anywhere on its footprint**.
- Information and money: Info and Money open all-staff, all-items, all-visitors and entry-price screens; **the all-visitors list keeps its rows and its scroll, rewritten in place, a row added and removed as a guest comes and goes** (Q200b). **The
  entry-price door shuts the park and every ride a guest may be offered**, drawn down when shut: a shut ride turns its
  queue away one head a sweep, for 15, and opening the door or editing its queue opens it again. **The bank moves as the original's** (Q177c, Q96): a charge is banked and a sale's cost of goods withdrawn, so a drink nets the park 10. **The month's change trains, runs the bank's turn and pays each wage** (Q198b): the shipped park's month costs 538.
- Building by POINTING - click to anchor, click to commit, no drag, because both of the original's drag slots are bare
  `RET` stubs. A click on grass or path picks up the PATH tool (20 a cell) with its own squares and cursors; Backspace
  takes the last run up, Escape puts the tool away. QUEUE is 75, refunded. **A bought thing starts at its own price.**
- **Placing a ride lays its queue's first cell before the entrance and hands over the queue tool there**, with the
  original's coloured squares where a click will lay it; a click onto a path lays and joins it. Guests queue and ride. **The catalogue is Instant Action's**: each item's `Easy_` file laid over its own, 50 items where the theme has 67 (Q178b); **the buy list lists only the researched, the save's flags** (Q201b).
- Spending: guests choose, queue for and buy from the Drinks Shop and the Jungle Spray; short of the price, they walk.
  **Each arrival is one of eight kinds, each with its own liking**, and **the choice is the original's whole score**:
  the kind just left is worth nothing, a new thing five times more for 184 sweeps, dear and golden-ticket rides more,
  shelter in rain, a track ride 60% of its level plus its track's, an upgraded one on its own tier, no unclosed coaster.
  **A visit's excitement moves happiness by the kind's liking, and illness by how full they are**; **a shop's ingredient
  too** (Q177d). **A Balloon Shop gives a balloon in the guest's own colour, held until it bursts** (Q177e); **a Costume
  Shop dresses a guest, and a second visit gives back their arrival's child** (Q177f). **Every twelfth walking turn a
  guest may turn aside for a nearer thing, then go on; a toilet empties the need.** **A visit is counted as the original's** (Q177b): a guest's rides, purchases and sideshows, a thing's customers, served and satisfaction in thirty-day rings rolled daily; shown as Users last month and Rides Ridden.
- People: guests and staff read from the save, drawn (an arrival as the child its id gives, Q177f), walking, a guest at their own speed (Q177d), running for the bus as it pulls away (Q199), paying, queueing,
  boarding, interpolated between the 248 ms steps. **Queuers walk to their own places, in a line**; one needing the
  toilet, or lost to the walk, is out. **A guest on a cell with no links wanders to the nearest path.** **Guests arrive
  and staff take turns on the original's clock**, the save's `mGameTick`: a load 126 s in, then every 150 s or so; a
  guard idles 11 sweeps and sets off on the clock's low bits. Let off, they leave by the exit.
- Rides: every placed thing runs its script; 77 of 106 opcodes built, the rest counted. A ride screams at the band its
  rider count asks for, as the original's chain: a fresh sample every 1-3 s on **its own clock**, so a second ride in
  the same band is neither held up by it nor set off by its stop. **No queue grows past its ride's longest.** **A ride
  loops again after its ride's end** (the Aztec Mayhem). **A load resumes each wait and clip where its save left it, each script on its saved handle and tick** (Q180), **and each deadline it keeps in a variable** (Q181b). Each tick keeps its own instant and a stall runs 64 ticks, where the engine's run one instant a frame and 65: kept by decision (Q182b). **Every script's walks are stepped once a tick, after the scripts**, where the engine's are once a frame: kept by decision (Q183).
  **A rider walks on and off for their two model nodes' distance**, as the original's (Q175b); a head on a moving part is counted at rest. **The Hot Pot floats a boat per unit of capacity in its pot, a rider's head seated in each, and lets them off after `VAR_DURATION` × 30 track ticks** (Q179b); **in a go the boats with riders steer through the buoys and chase, and bump each other off the rim** (Q179d). **`ADDHEAD` hangs a rider's head on a random free head node and `DELHEAD` takes it off**, drawn on the tentacle or car as it moves, and saved (Q190).
- **A thing bought this session is a member of the running park**: it takes its turn, appears in every census, joins the
  object chain the original keeps live, and carries the entry and exit cells derived from its own shape picture (Q1b).
- The original runs under Proton as a reference (Q168, `docs/TOOLING.md`); the console's `admit`/`send` place a guest (Q184).

## Does not

- No screen sets the training budgets or buys a loan; six months in the red is counted, not an end (Q198b). No litter, day ending, saving a park back, video,
  networking. Research is inert. **A Full Simulation player is handed the Instant Action park**, counted (Q186). In a park the advisor says the gadget's opening line and no more (`docs/PLAYER-GAPS.md` gap 4).
- Eight of the nine per-object windows are unbuilt. Setting patrol areas is deferred by Alexah; staff keep the save's.
  A walking member of staff is not entered in the cells they cross; only hiring and putting down place one.
- Unbuilt: Q102-Q105, five queue-turn arms (the unhappy one held for Q85), spot animation (Q98), Q112's walk to path.
- The park's door moves no gate (Q89), advisor (Q90) or shut ride's model (Q91); the ride window's door is no button
  (Q92), and a bought queued thing starts open (Q93).
- Nothing shows what the hand holds (`CARRY_PREVIEW_MARKERS`, `STAFF_CARRY_PREVIEW`); any cell takes a candidate (Q40).
- The fly-in's fade to black is not drawn (Q61). Keys: Escape over the player slots opens the game menu (Q64) and closes
  no park screen (Q119); C (Q118) and Ctrl+H act on the press, F8 is not built (Q65); modifiers count as the frame ends
  (Q120). A disabled button still takes the pointer (Q66); presses the original stops reach the park (Q113, Q115, Q116).
- The happiness gauge draws two copies of its bar, split down the middle (`docs/PLAYER-GAPS.md` gap 5; unmeasured).
  Every other sound still waits out a per-effect "repeat delay" that is really a priority (Q43).
- With no work the mechanic, handyman and entertainer stand where the original's walk about (Q133); staff make no
  sound (Q135). Guests and rides read `GameClock.Ticks / 8`, not `mGameTick` (Q132); a load brings one guest (Q26); the bus waits (Q131).
- Counted, not built: the isles' random clips (Q76), the idle repeat (Q77), a ride walked into in first person, a coaster's
  excitement and level, the charge's sound, the Hot Pot's wake, a ride head's picture turn (Q190).
- The camcorder is entered where the orbit looks, not by a click on the ground, so it can start off the park, where it
  cannot move, and leaving keeps the walk where the original's throws it away (Q25). A held right button there does not
  walk (Q121), and a park screen stays open over it (Q122). It walks onto entrances the original shuts (Q140).

## Next

`docs/QUEUE.md`, from the top: **Q1-Q12, Q34-Q36, Q39, Q41, Q42, Q44-Q50h, Q53, Q53b, Q56, Q57, Q59, Q67-Q71, Q82-Q84,
Q88, Q96, Q101, Q165, Q166, Q168, Q169-Q177f, Q184, Q178, Q178b, Q198, Q198b, Q199, Q200, Q200b, Q201, Q201b, Q179, Q179b, Q179c, Q179d, Q180, Q181, Q181b, Q182, Q182b, Q183, Q185, Q186, Q187, Q189, Q190 are ticked**; next **Q188** (the object windows' P model), which Alexah wants worked together, not
started alone. Until then Q191 onwards, the rest of the fork review's Q185-Q197. Gaps 4, 5, 7: `docs/PLAYER-GAPS.md`. Q13: `CLEANUP-PLAN.md`.

## Not verified on screen

- **The RIDER on a ride bought this session**: measured five times, not photographed (the console has no pitch).
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: unwiring either leaves the suite green.
- Nothing the game ships reaches the critical-section cap (Q11), Q68b's ferry and seaplane let-go, Q82b's stamp ahead
  of the clock, state 6's wait or Q83b's stack errors: tested only; nor a sale's staff half, as nobody rests yet (Q36).
- Tested, not run in the game: the Delete key's and a sale's let-go of a candidate (Q39), Escape before the gate opens
  (Q41), the name box's two releases in one frame, a park whose global.sam will not load (Q42), `Rotation.From` (Q71).
- The camcorder's tie and four of Q48b's put-backs move the viewer under 0.2 units: the census's, not a photograph's.
- Q50's slot let go, its nominee and `EnteringRide` kept, and a queue walk giving up at a stale link: tested only.
- Q50b's reopen by an edit of the queue, a head forced on, the guard's refusals and a bought ride's bit: tested only.
  Q50c's refusal on worth (none at these prices) and negative cash passing: tested; its kids' sound: neither. Q50d's
  lost place, invited guest who is not the nominee, broken ride and car track: tested only. Q50g's re-aim at a moved
  back of queue, its three failed walks, the refused door's walk, a dodgy direction and a place past the cells: tested.
  Q50h's corner past a node, which a measure between runs can reopen, and its other arms: tested only. Q53b's five
  tries, a lone path cell, a failed probe route and the wrap: tested only; the probes from a sold ride find row 21.
- Tested only: Q165c's histories cleared by a sale, refusals aging and a save's histories; Q166's literal `RAND`, `MOD`,
  `SUB`, `GETREMOTEVAR` and `COAST 2 0`; and a lock taken on the last unit running its section whole, which the stock park
  never reaches (the Hot Pot's capacity cut mid-ride, resting on `BUMP` unbuilt, Q45). Q170b's cleared illness, second switch,
  stale major, sale's clear and save's fields; Q171's bounds; Q172b's saved track ride, tier and coaster (Q167) and a Hot Pot
  rider's 42 (Q179b); Q173's tier 3 and zero divisors. Q174b's raw re-entry and last-frame trigger (no run) and loop (census
  only); Q174c's saved mark, timer and queue; Q175b's Rat Race; Q176's `0x80000000` draw (its let-go in a throwaway build);
  Q177b's walk-away and a played save's counts; its satisfaction, census only (nothing here shows it). Q177c's withdrawals off, red stamp, a sold coaster's nought, the ticket count and the year's change. Q177d's docks, fat, salt, the speed's hold and floor and a hire's speed. Q177e's balloon built again after a later visit, put away on boarding, deleted on going home, and read from a save; its bob and trail (census only). Q177f's low detail (two kids, one staff bank) and a theme with more than one costume. Q198b's promotion, loans, batch and six months in the red. Q200b's selection edges and the wheel's clamp of a short list. Q201b's mystery row and a played save's research. Q178b's refused line (no shipped file has one). Q179b's boat yaw sense (needs the original) and a saved Hot Pot (its cars not restored, counted). Q179c's bump impulse and rim reflection: read, not replayed against the original's log; Q179d builds them, tested only. Q180's newest-first order within a tick: tested only. Q181b's split pair, the gift shop's timer and a Monkey timed out (it filled): tested only. Q190's heads restored from a save and its draw stop: tested only.

## Numbers (take counts fresh; these go stale within a day)

| | | measured |
|---|---|---|
| Opcodes | **77** of 106 | 2026-10-01, `case Opcode.` labels vs enum members, after Q190 |
| Tests | **1559**, 0 fail, 0 skip with the game | 2026-10-01, after the heads followed |
| Tests without the game | **639** ran, **920** skipped, of 1559 | 2026-10-01, after the heads followed |
| Build warnings | 123 | 2026-10-01, after the heads followed |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-10-01 (Q190).** `alexah/228-heads-follow-the-ride`: the heads follow the tentacles. **Earlier:** `227` (Q190), `226` (Q189), `225` (Q187), `224` (Q186), `223` (Q185), `222` (Q183), `220` (Q182b), `218` (Q182), `217` (Q181b), `216` (Q181), `215` (Q180), `214` (heads), `213` (Q179d), `212` (Q179c), `209`-`211` (Q179b), `208` (Q179c queued), `207` (Q179), `206` (Q201b), `205` (Q201), `204` (Q178b), `203` (Q178), `202` (Q200b), `201` (Q200), `200` (Q199), `199` (Q198b), `198` (Q198), `197` (Q177f), `196` (Q177e), `195` (Q177d), `194` (Q177c, Q96), `193` (the fork review), `192` (Q177b), `191` (Q177), `190` (Q176), `189`, `188` (Q184), `187` (Q175b), `186` (Q175), `185` (Q168), `184` (FileFormats rules); each QUEUE.md entry names its branch: `183` (Q174c) back to `118` (Q4), less `127`, `133`, `159`, `160`, `162`, `166`, `169`; `117`, `109`.
