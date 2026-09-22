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
- A laid queue cell still has **no tile piece** and the neighbour bit it needs is authored here rather
  than earned, because the engine step that writes it is undecoded - `docs/QUEUE.md` Q3, whose written
  prescription is unsafe; see the warning there.

## Next

`docs/QUEUE.md`, from the top. **Q1, Q1b and Q2 are ticked**; the next unticked item is **Q3**, a laid
queue cell having no neighbours and no tile piece - whose written prescription is unsafe, and whose entry
carries two refuted leads and what this work measured for it.

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
| Tests | **896**, 0 fail, 0 skip with the game | 2026-09-22, six of them for footprint picking |
| Tests without the game | **426** ran, **470 skipped**, of 896 | 2026-09-22, measured either side rather than computed |
| Build warnings | 125 | 2026-09-22 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-22 - a thing the save placed can be clicked anywhere on it.** Branch
`alexah/110-click-a-thing-the-save-placed`. The item blamed unset occupancy; the truth is that a placed
thing is on **one** cell's list - its anchor - and owns the rest of its footprint through `mParentID`.
Stamping occupancy across the footprint, as the item prescribed, was tried and lost a guest: those links
are keyed per thing, so the twelfth cell overwrites the first. `ParkPicking.ThingOn` now asks whoever is
standing there, then the owner. Measured in a running park, every count predicted first: the save's Belly
Bounce **12 of 12** cells (was 1), Jungle Spray **9 of 9**, Drinks Shop **4 of 4**, open ground 0 of 4, a
ride bought this session 12 of 12, and 0 of 12 once sold. Clicking (52,25) printed `Ride window: showing
'Belly Bounce' (thing 13)`, photographed against a control frame with no window on it.
**The instrument was measuring the wrong thing**: `worldclick` read the cell's occupant itself and never
entered the picking code, so every reading through it would have been 1 of 12 whatever the fix did.
Closing a twice-surviving mutation exposed a second defect - `Sell` swept `LeaveCell` across the whole
footprint and orphaned the guest under a **turned** thing - now `Unstamp`, tested. `VERIFYING.md` 113 and
114 carry what the two blind spots cost.

**2026-09-22 - a thing bought this session joins the park.** Branch
`alexah/109-bought-things-join-the-park`. Q1 had one named cause and turned out to have four. The ride
sweep, the rider-seat lookup and the `rides` and `spend` censuses all read the save's immutable list, so
a bought ride took no turn and no census saw it; the offer walk followed the file's `mFirstObject` chain,
which nothing splices a bought thing into; and `Buy` left `CanLoad` and `Flags` at the record's defaults,
so `Invite` bailed and nothing was visitable. `ParkState` now owns a live chain - the original links at
the HEAD (`FUN_00519d80`) and unlinks on demolish (`FUN_00519dc0`) - and `Buy` sets `CanLoad` and the two
flag bits whose descriptor key is established, counting the rest. Measured in a running park: `objects` 14 → 15, and thing 44 in `rides` with `capacity 5 duration
30` and its script cycling `role 2`, where before it printed no line at all. Thing 13 still carries
riders, which is the two-sided control. **And its entry and exit cells are derived** the original's way:
`FUN_00413410` reads the shape grid for the cells holding **9** and **10**, and `bouncy.sam`'s `S` and
`2` land exactly on the save's own `mEntryPos` 2997 and `mExitPos` 3381 for the shipped Belly Bounce -
two records meeting on one cell, neither enough alone, which closes `park-engine.md`'s open question on
mType 9 and 10. Both ends are typed and their headings measured off the game rather than derived
(`cell (42,23) type 9 direction 0x01` after a buy), this tree holding two compasses that disagree by name.
**And a guest RIDES it.** The last cause was `PeepBehaviour.Chosen`, resolving `MajorDest` against the
FILE's list: a bought ride is not in it, so `JoinTheQueue` gave up the instant a guest arrived - they
chose it, walked the whole way, gave up silently and chose it again. One line, and it un-blinds all five
of `Chosen`'s callers. Measured in **five** runs - `bouncing 1` on the ride's own node at (425.4,252.6),
a different scream each time, `save/` unchanged within every one. Mutations called in advance: **7**,
**3**, **1**, **1** red and **four** survivals said at their tests. **The rider is not photographed**: he
rides at z 10.3, the camcorder's eye is 5.0 and the console has no pitch argument (`VERIFYING.md` 111-112).

**2026-09-22 - four earlier items, kept now only in the git log.** `alexah/108` photographed the two
Confirm clauses that were owed pictures; `alexah/107` swung the camera onto the gate and flew it into the
island before a park loads, against a decode that said the original showed nothing and about which Alexah
was right; `alexah/106` measured the lobby's attract camera and refuted all three of that item's claims,
leaving only a deviation Alexah chose; `alexah/105` stopped the camcorder at a ride instead of walking
through it, 4 of 4 either way round.

Everything older is the git log.
