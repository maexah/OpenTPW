# Status

Last updated: 2026-09-28.

**This header names no branch and no sha, deliberately.** A line written inside the commit that moves
the tip cannot name it, so any name there is stale the instant it is written. Read the current state
from the repository, which cannot lag: `git log --oneline -1`.

**`docs/QUEUE.md` is the work queue.** One item per session, taken from the top unless Alexah reorders.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate, and the attract camera
  flying around all four islands with all four heard at once. Enter swings the camera onto the gate, opens it and flies
  in before the loading screen; **the island keys wait for that flight**, and **Escape cancels it** (the camera orbits
  again, the gate shuts, the panel comes back). **Enter, the arrows and Escape act on their release**, and
  **a left press on the lobby's view enters the park**.
- Park: ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (5 of 6). The
  camcorder walks the original's sweep pass for pass, and **a quick right click leaves it** (RMB cancel on). Leaving
  one lets go of all of it: nothing of a left park is held in the lobby, nor of any left scene's interface.
- Building and staffing: purchase menu and hire screen, both reachable from Buy. Things bought, sold, moved, carried;
  staff hired, fired, picked up, put down. **Selling or moving a thing puts its riders and queuers off where they
  stand**, staff resting there get up, and its script goes with it. **Cutting a queue puts out whoever stands past its
  new end.** A staff drop the park refuses keeps the candidate. **The hand holds one thing and lets go of it the
  original's ways** (quick right click with RMB cancel on, Escape, Delete, the camcorder, a new pickup, leaving); a moved
  thing stays in it until a cell takes it. A placed ride's window opens from a click **anywhere on its footprint**.
- Information and money: Info and Money open all-staff, all-items, all-visitors and entry-price screens. **The
  entry-price door shuts the park and every ride a guest may be offered**, drawn down when shut: a shut ride turns its
  queue away one head a sweep, for 15, and opening the door or editing its queue opens it again.
- Building by POINTING - click to anchor, click to commit, no drag, because both of the original's drag slots are bare
  `RET` stubs. A click on grass or path picks up the PATH tool (20 a cell) with its own squares and cursors; Backspace
  takes the last run up, Escape puts the tool away. QUEUE is 75, refunded.
- **Placing a ride lays its queue's first cell before the entrance and hands over the queue tool there**, with the
  original's coloured squares where a click will lay it; a click onto a path lays and joins it. Guests queue and ride.
- Spending: guests choose, queue for and buy from the Drinks Shop and the Jungle Spray; short of the price, they walk.
  **Each arrival is one of eight kinds, each choosing by its own preferred excitement**: a bought Totem is ridden.
  **The choice is the original's whole score**: the kind a guest has just left is worth nothing to them, a thing bought
  is new for 184 sweeps, dear and golden-ticket rides count more, shelter more in rain; the Spray works out its 30.
- People: guests and staff read from the save, drawn, walking, paying, queueing, boarding, interpolated between the
  248 ms steps. **Queuers walk to their own places, in a line**; one needing the toilet, or lost to the walk, is out.
  **A guest on a cell with no links, such as a sold thing's cleared ground, wanders to the nearest path.** **Guests
  arrive, and staff take their turns, on the original's clock**, the save's `mGameTick`: a load 126 s in, then about
  150 s after each; a guard idles 11 sweeps and sets off on the clock's low bits.
- Rides: every placed thing runs its script; 74 of 106 opcodes built, the rest counted. A ride screams at the band its
  rider count asks for, as the original's chain: a fresh sample every 1-3 s on **its own clock**, so a second ride in
  the same band is neither held up by it nor set off by its stop.
- **A thing bought this session is a member of the running park**: it takes its turn, appears in every census, joins the
  object chain the original keeps live, and carries the entry and exit cells derived from its own shape picture (Q1b).

## Does not

- No finances (a charge never reaches the bank, Q96), litter, day ending, saving a park back, video, networking.
  Research is inert. In a park the advisor says the gadget's opening line and no more (`docs/PLAYER-GAPS.md` gap 4).
- Eight of the nine per-object windows are unbuilt. Setting a staff member's patrol area is not built, deferred by
  Alexah; staff keep to the areas the save gives them. A walking member of staff is not entered in the cells they
  cross; only hiring and putting down place one.
- Q102-Q105 and five queue-turn arms are unbuilt, the unhappy one held for Q85. No spot animation (Q98). A guard or
  researcher on a cell with no links does not look for path (Q112). After one toilet a guest may choose no other,
  where the original's go on to the next (Q170); a bought thing charges nothing (Q171).
- The park's door moves neither the gate (Q89) nor the advisor (Q90), nor a shut ride's model (Q91); the ride window's
  door shows a shut ride but is not a button (Q92), and a bought queued thing starts open (Q93).
- Nothing shows what the hand holds, a thing (`CARRY_PREVIEW_MARKERS`) or a candidate (`STAFF_CARRY_PREVIEW`), and
  any cell on the map takes a candidate; the original's rule is decoded (Q40).
- The fly-in's fade to black is not drawn (Q61). Keys: Escape over the player slots opens the game menu (Q64) and closes
  no park screen (Q119); C (Q118) and Ctrl+H act on the press, F8 is not built (Q65); modifiers count as the frame ends
  (Q120). A disabled button still takes the pointer (Q66); presses the original stops reach the park (Q113, Q115, Q116).
- The happiness gauge draws two copies of its bar, split down the middle (`docs/PLAYER-GAPS.md` gap 5; unmeasured).
- Every other sound still waits out a per-effect "repeat delay" that is really a priority (Q43).
- With no work the mechanic, handyman and entertainer stand where the original's walk about (Q133); staff make no
  sound (Q135). Guests and rides read `GameClock.Ticks / 8`, not `mGameTick` (Q132); a load brings one guest (Q26); the bus waits (Q131).
- Counted, not built: the isles' random clips (Q76), the idle repeat (Q77), riding a ride walked into in first person,
  and a coaster's, a track ride's and an upgraded ride's excitement (Q172).
- The camcorder is entered where the orbit looks, not by a click on the ground, so it can start off the park, where it
  cannot move, and leaving keeps the walk where the original's throws it away (Q25). A held right button there does not
  walk (Q121), and a park screen stays open over it (Q122). It walks onto entrances the original shuts (Q140).

## Next

`docs/QUEUE.md`, from the top. **Q1-Q12, Q34, Q35, Q36, Q39, Q41, Q42, Q44, Q45, Q47, Q48, Q48b, Q50-Q50h, Q53, Q53b,
Q56, Q57, Q59, Q67, Q68, Q68b, Q69-Q71, Q82, Q82b, Q83, Q83b, Q165, Q165b, Q165c, Q166, Q46, Q49, Q84, Q88, Q101
are ticked.** Next: **Q169**. Which item or audit filed each open one is its entry's "Found by" (Q166 filed Q174-Q176).
`docs/PLAYER-GAPS.md` holds gaps **4, 5 and 7**. The untracked `docs/CLEANUP-PLAN.md` (all nine closed) is Q13's.

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
- Tested only: Q165c's histories cleared by a sale, refusals aging, the too-long gate and a save's histories; Q166's
  literal `RAND`, `MOD`, `SUB`, `GETREMOTEVAR` and `COAST 2 0`; and a lock taken on the last unit running its section
  whole, which the stock park never reaches (the Hot Pot's capacity cut mid-ride, resting on `BUMP` unbuilt, Q45).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **1248**, 0 fail, 0 skip with the game | 2026-09-28, after Q166 |
| Tests without the game | **512** ran, **736** skipped, of 1248 | 2026-09-28, after Q166 |
| Build warnings | 123 | 2026-09-28, after Q166 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-28 (Q166).** `alexah/173-count-the-literal-destinations`. **Earlier:** each QUEUE.md entry names its branch:
`172` (Q165c) back to `118` (Q4), less `127`, `133`, `159`, `160`, `162`, `166`, `169`; `117`, `109`. Older: git log.
