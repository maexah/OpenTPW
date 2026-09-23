# Status

Last updated: 2026-09-23.

**This header names no branch and no sha, deliberately.** A line written inside the commit that moves
the tip cannot name it, so every attempt went stale the instant it was written. Read the current state
from the repository, which cannot lag: `git log --oneline -1`.

**`docs/QUEUE.md` is the work queue.** One item per session, taken from the top unless Alexah reorders.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate, and the
  attract camera flying itself around all four islands with all four heard at once. Clicking Enter swings
  the camera onto the gate and flies into the island before the loading screen.
- Park: ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (5 of 6).
- Building and staffing: purchase menu and hire screen, both reachable from Buy. Things bought, sold,
  moved, carried; staff hired, fired, picked up, put down. **A sold thing's script goes with it**, and
  its ground is left bare. **A moved thing stays in the hand until a cell takes it**, facing the way it stood. Clicking a placed ride **anywhere on its
  footprint** opens its window - the save's own and ones bought this session alike.
- Information and money: Info and Money open all-staff, all-items, all-visitors and entry-price screens.
- Building by POINTING - click to anchor, click to commit, no drag, because both of the original's drag
  slots are bare `RET` stubs. A click on grass or path picks up the PATH tool (20 a cell) with its own
  squares and cursors; Backspace takes the last run up, Escape puts the tool away. QUEUE is 75, refunded.
- **Placing a ride lays its queue's first cell before the entrance and hands the player the queue tool
  there**, with the original's coloured squares showing where a click will lay it; one click onto a path
  lays and joins the queue. Guests queue in it and ride.
- Spending: guests choose, queue for and buy from the Drinks Shop and the Jungle Spray.
- People: guests and staff read from the save, drawn, walking, paying, queueing, boarding. A walking peep
  is interpolated between the simulation's 248 ms steps rather than jumping four times a second.
- Rides: every placed thing runs its script; 74 of 106 opcodes built, the rest counted. A ride screams
  with a different sample each pass, at the band its rider count asks for.
- **A thing bought this session is a member of the running park**: it takes its turn, appears in every
  census, joins the object chain the original keeps live, and carries the entry and exit cells derived
  from its own shape picture. See the top entry under Recent.

## Does not

- No finances, litter, saving a park back, video, networking. Research is inert and has nothing behind it.
- Eight of the nine per-object windows are unbuilt. Patrol areas are dead, deferred by Alexah.
- Selling a thing lets nobody go: its riders stay aboard and its queuers stay put (`docs/QUEUE.md` Q36).
- Any right press empties the hand, which for a moved thing is a sale; Escape opens the menu over it (Q39).
- The `meter.wct` mapping behind the happiness gauge is wrong - the last fault Alexah found by playing.

## Next

`docs/QUEUE.md`, from the top. **Q1 to Q5, and Q35, are ticked.** Next is **Q6**: placing a carried staff
member takes the candidate before the hire can fail. Q4 filed Q36-Q38 (eviction, terrain rule, effects).

`docs/PLAYER-GAPS.md` still holds gaps **4, 5 and 7**. `docs/CLEANUP-PLAN.md` has all nine items closed
and is still untracked, so it exists on this machine only; Q13 moves it into `docs/history/`.

## Not verified on screen

- **The RIDER on a ride bought this session.** Measured five times over; not photographed, because the
  rider sits at z 10.3 against a 5.0 camcorder eye at pitch 0 and the console has no pitch argument.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: unwiring either leaves the suite green.
- Nothing puts a staff member in a cell's occupancy list *as they walk*.

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **943**, 0 fail, 0 skip with the game | 2026-09-23, after Q5 |
| Tests without the game | **431** ran, **512** skipped, of 943 | 2026-09-23, after Q5 |
| Build warnings | 125 | 2026-09-23, after Q5 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-23 - a move keeps the thing in the hand until a cell takes it.** Branch
`alexah/119-move-keeps-the-thing-in-the-hand`, `docs/QUEUE.md` Q5. Decoded first, every claim put to two
refuters: the original's move is Delete's demolish, refund banked, then the move tool holding the item **and
the thing's own angle**; a red cell keeps it in the hand, and money reddens a cell only at put-down.
`ParkBuilding.PickUp` is the one body for the window's Move and the console's `move`, which is that and one
click; the sale answers a value the pickup reads, where Move parsed a string. Confirmed in the game, every
number predicted: a refused `move` left the Belly Bounce in the hand (`carry` says so; nothing draws a carried
thing), scripts 16 to 15, balance 87987 to 88712; the next click stood it back up, through the window too after
a refused click; the Staff Room came back turned 90, frame identical. Its review filed Q39, the hand's ways out.

**2026-09-23 - a sold thing takes its script down, and leaves bare ground.** Branch
`alexah/118-a-sold-thing-takes-its-script-down`, `docs/QUEUE.md` Q4. Decoded first, every claim put to a refuter:
the destructor tears the script down with mode 7, whose bits spawn the item's death particle and take
`ADDHEAD` heads off the model. `Sell` now unbinds through the scheduler's one-level `Destroy`, and counts
the particle, the demolish sound and the eviction of riders and queuers it does not build (Q36-Q38).
**The item's Confirm was hollow**: the `rides` census drops a sold thing either way, so its header now
prints `scripts N bound M`. **Its unmeasured finding was real**: a sold save-placed ride left a sky-blue
hole in its footprint and refused a rebuild; `Unstamp` writes the original's cleared cell now. Confirmed
through the ride window's Delete: scripts 16 to 15, back on its own spot, then 15, 14, 13 and still 13.

**2026-09-22 - a placed ride lays its queue's first cell, and the player lays the rest from it.** Branch
`alexah/115-a-placed-ride-lays-its-queue-node`, `docs/QUEUE.md` Q3, which Alexah reopened twice: *"the node
never shows up"*. Decoded before building, with a refuter per claim: the placer stamps a NOMODIFY queue
cell before a queued ride's entrance and a path before its exit, and the commit arms the queue tool on
that cell. **The `Info.Shape` alphabet was wrong** - `2` is the entrance, `S` an exit, rows read upside
down - so twelve of the jungle's seventeen rides had no entrance or the wrong one. The queue run's link
pass, the join onto a path, the tool putting itself away, mode `0x14` behind the ride window's queue
button, and the marker squares are all built from the decode. A ride-end cell drawing nothing is the
original's own behaviour. Selling a queued ride drains its queue, node included, as the demolisher
does. Confirmed through the player's route; a guest queued for and rode it. The squares then took
the original's wave (`alexah/116`), decoded once Alexah confirmed they were see-through and waving.

**2026-09-22 - nine earlier items, kept now only in the git log.** `alexah/117` made the path tool picked
up from the park, with Backspace taking a run back up (Q35); `alexah/114` let a player build a queue that
joins the paths around it (the first half of Q3); `alexah/110` made a thing the save
placed clickable anywhere on its footprint (`ParkPicking.ThingOn`, `VERIFYING.md` 113-114); `109` made a
thing bought this session join the park (Q1, Q1b - the rider is still not photographed); `112` decoded
what authors an entrance's queue link; `108` photographed two owed Confirm clauses; `107` flew the camera
into the island; `106` refuted all three of its item's claims; `105` stopped the camcorder at a ride.

Everything older is the git log.
