# Status

Last updated: 2026-09-22.

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
  moved, carried; staff hired, fired, picked up, put down. Clicking a placed ride **anywhere on its
  footprint** opens its window - the save's own and ones bought this session alike.
- Information and money: Info and Money open all-staff, all-items, all-visitors and entry-price screens.
- Building by POINTING - click to anchor, click to commit, no drag, because both of the original's drag
  slots are bare `RET` stubs. Laying and lifting PATH (20 a cell) and QUEUE (75, which refunds).
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
- **The PATH tool is still console-only.** The queue tool is reachable now - the ride window's queue
  button arms it, and clicking a queue cell re-arms it - but nothing in the UI arms path laying.
- The `meter.wct` mapping behind the happiness gauge is wrong - the last fault Alexah found by playing.

## Next

`docs/QUEUE.md`, from the top. **Q1, Q1b, Q2 and Q3 are ticked.** Next is **Q4**: `Sell` leaves a sold
ride's script bound and scheduled, and leaves the queue cells keyed to a thing that is gone.

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
| Tests | **914**, 0 fail, 0 skip with the game | 2026-09-22, after Q3's node |
| Tests without the game | **not remeasured** - last read 426 ran / 470 skipped of 896 | the game was present for every run this session; take it fresh |
| Build warnings | 125 | 2026-09-22 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-22 - a placed ride lays its queue's first cell, and the player lays the rest from it.** Branch
`alexah/115-a-placed-ride-lays-its-queue-node`, `docs/QUEUE.md` Q3, which Alexah reopened twice: *"the node
never shows up"*. Decoded before building, with a refuter per claim: the placer stamps a NOMODIFY queue
cell before a queued ride's entrance and a path before its exit, and the commit arms the queue tool on
that cell. **The `Info.Shape` alphabet was wrong** - `2` is the entrance, `S` an exit, rows read upside
down - so twelve of the jungle's seventeen rides had no entrance or the wrong one. The queue run's link
pass, the join onto a path, the tool putting itself away, mode `0x14` behind the ride window's queue
button, and the marker squares are all built from the decode. A ride-end cell drawing nothing is the
original's own behaviour. Selling a queued ride drains its queue, node included, as the demolisher
does. Confirmed through the player's route; a guest queued for and rode it.

**2026-09-22 - a player can build a queue, and it joins the paths around it.** Branch
`alexah/114-a-laid-queue-joins-up`, the first half of Q3: the placer's link is a pair, a laid queue cell
is retiled, `RotateBit` turned the wrong way, the ride window's queue button arms the tool, and an exit
retiles the path it joins. Six defects; `docs/QUEUE.md` Q3 and the git log carry them.

**2026-09-22 - what authors an entrance's queue link is decoded.** Branch `alexah/112`, no code
changed; the decode the entry above builds on, written up in `docs/exe/park-engine.md`.

**2026-09-22 - a thing the save placed can be clicked anywhere on it.** Branch
`alexah/110-click-a-thing-the-save-placed`. The item blamed unset occupancy; the truth is that a placed
thing is on **one** cell's list - its anchor - and owns the rest of its footprint through `mParentID`.
Stamping occupancy across the footprint, as the item prescribed, was tried and lost a guest: those links
are keyed per thing, so the twelfth cell overwrites the first. `ParkPicking.ThingOn` now asks whoever is
standing there, then the owner. Measured in a running park, every count predicted first: the save's Belly
Bounce **12 of 12** cells (was 1), Jungle Spray **9 of 9**, Drinks Shop **4 of 4**, open ground 0 of 4, a
ride bought this session 12 of 12, and 0 of 12 once sold. Clicking (52,25) printed `Ride window: showing
'Belly Bounce' (thing 13)`, photographed against a control frame with no window on it.
**The instrument was measuring the wrong thing**: `worldclick` read the cell's occupant itself, never
entering the picking code. Closing a twice-surviving mutation then exposed a second defect - `Sell` swept
`LeaveCell` and orphaned the guest under a **turned** thing - now `Unstamp`. `VERIFYING.md` 113-114.

**2026-09-22 - a thing bought this session joins the park, and a guest rides it.** Branch
`alexah/109-bought-things-join-the-park`, Q1 and Q1b. Four causes, not the one the item named; the
detail is in `docs/QUEUE.md` Q1 and the git log. **The rider is still not photographed** - z 10.3
against a 5.0 eye with no pitch argument (`VERIFYING.md` 111-112).

**2026-09-22 - four earlier items, kept now only in the git log.** `alexah/108` photographed two owed
Confirm clauses; `107` flew the camera into the island; `106` refuted all three of its item's claims;
`105` stopped the camcorder at a ride.

Everything older is the git log.
