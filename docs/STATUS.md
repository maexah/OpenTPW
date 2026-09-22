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
- The `meter.wct` mapping behind the happiness gauge is wrong - the last fault Alexah found by playing.
- A queue laid on **bare ground** joins only the way the run walked into it. The mutual bits along a run
  are earned while the cells are still path, so the original's own gesture is lay path, then queue over
  it; what would author a bare-ground run's bits is undecoded and counted.

## Next

`docs/QUEUE.md`, from the top. **Q1, Q1b, Q2 and Q3 are ticked.** Next is **Q4**: `Sell` leaves a sold
ride's script bound and scheduled, and leaves the queue cells keyed to a thing that is gone.

`docs/PLAYER-GAPS.md` still holds gaps **4, 5 and 7**. `docs/CLEANUP-PLAN.md` has all nine items closed
and is still untracked, so it exists on this machine only; Q13 moves it into `docs/history/`.

## Not verified on screen

- **The RIDER on a ride bought this session.** The boarding is measured five times over; the bought ride
  is photographed standing, animating and queued for, but its rider sits at z 10.3 while the camcorder's
  eye is 5.0 at pitch 0 and the console has no pitch argument, so the ride's own body hides him.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: called from `ParkPeople`, neither pinned by the
  suite - unwiring either leaves it green. They rest on the decode, not on coverage.
- Nothing puts a staff member in a cell's occupancy list *as they walk*.

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **901**, 0 fail, 0 skip with the game | 2026-09-22, five of them for the queue Q3 built |
| Tests without the game | **not remeasured** - last read 426 ran / 470 skipped of 896 | the game was present for every run this session; take it fresh |
| Build warnings | 125 | 2026-09-22 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-22 - a queue laid to a ride bought in play joins up and draws.** Branch
`alexah/114-a-laid-queue-joins-up`, `docs/QUEUE.md` Q3. **Three defects, and the item named one of
them.** The placer's link is a PAIR and only one end existed, so the entrance named a neighbour that
never named it back and `CellEdge.Blocked` refused the step in. `Retile` returned for anything not a
path, so a laid queue cell kept the ground's own tile index - **55, not piece 0 as the item said** -
which is outside the game's eight models, so it drew nothing. And `RotateBit` turned a compass bit the
wrong way round, with a test asserting the inverted value: `FUN_004d8c20` is a right-rotate of two per
quarter and `MapDelta::Rotate` agrees, but the two senses differ only at 90 and 270 and nothing had
ever built a turned thing. **A fourth was found by looking at the screenshot**: two mutual path links
bump the tile index past the model table, and since the ground leaves a queue cell alone, the sky
showed through a hole. Measured, each predicted first: (42,22) `0x10` from placement alone against a
baseline `0x00`; three cells `index 5/5/5` against `55`; `drawn` 4 pieces to **7**; thing 43 `cells 1`
to **3**; guest 35 `InQueue at (44.385,22.498)`, guest 29 `Riding`. Mutations **2, 2, 3, 1, 1** red and
one named survivor. `park-engine.md` carries the refuted `mDirection` half and the rotate's sense.

**2026-09-22 - what authors an entrance's queue link is decoded.** Branch
`alexah/112-decode-what-authors-an-entrance-link`, no code changed. The decode the entry above builds
on, and the one that corrected "`mNeighbours` is written in exactly one place": `FUN_00522700` has six
callers. It is in `docs/exe/park-engine.md`.

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

**2026-09-22 - four earlier items, kept now only in the git log.** `alexah/108` photographed the two
Confirm clauses that were owed pictures; `alexah/107` swung the camera onto the gate and flew it into the
island before a park loads, against a decode that said the original showed nothing and about which Alexah
was right; `alexah/106` measured the lobby's attract camera and refuted all three of that item's claims,
leaving only a deviation Alexah chose; `alexah/105` stopped the camcorder at a ride instead of walking
through it, 4 of 4 either way round.

Everything older is the git log.
