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
  moved, carried; staff hired, fired, picked up, put down. Clicking a placed ride opens its window.
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
- **Nothing can be joined to a thing built during play**, so no guest boards one. Its way in is typed and
  headed correctly now; what is missing is the entrance's own `Neighbours` bit, which the shipped park
  has **authored in the save** and no rule here computes. `docs/QUEUE.md` Q3, whose written prescription
  is unsafe - see the warning there.

## Next

`docs/QUEUE.md`, from the top. **Q1 is unticked and Q1b is done**: everything Q1 names has landed and is
confirmed by census, and its own confirm clause - a guest boarding - now waits on **Q3** alone. Finishing
Q3 ticks Q1 without another line of Q1's own code.

`docs/PLAYER-GAPS.md` still holds gaps **4, 5 and 7**. `docs/CLEANUP-PLAN.md` has all nine items closed
and is still untracked, so it exists on this machine only; Q13 moves it into `docs/history/`.

## Not verified on screen

- **A guest boarding a ride bought this session.** Blocked by Q3, not by anything Q1 names; measured
  three times in a running park, the last with the way in typed 9 and a path laid against it.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: called from `ParkPeople`, neither pinned by the
  suite - unwiring either leaves it green. They rest on the decode, not on coverage.
- Nothing puts a staff member in a cell's occupancy list *as they walk*.

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **888**, 0 fail, 0 skip with the game | 2026-09-22, seven for the object chain and eight for the entry cell |
| Tests without the game | **418** ran, **470 skipped**, of 888 | 2026-09-22, measured either side rather than computed |
| Build warnings | 125 | 2026-09-22 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-22 - a thing bought this session joins the park.** Branch
`alexah/109-bought-things-join-the-park`. Q1 had one named cause and turned out to have four. The ride
sweep, the rider-seat lookup and the `rides` and `spend` censuses all read the save's immutable list, so
a bought ride took no turn and no census could even see it; the offer walk followed the file's
`mFirstObject` chain, which nothing splices a bought thing into; and `Buy` left `CanLoad` and `Flags` at
the record's defaults, so `Invite` bailed and nothing was visitable. `ParkState` now owns a live chain -
the original links at the HEAD (`FUN_00519d80`) and unlinks on demolish (`FUN_00519dc0`), one call site
each - and `Buy` sets `CanLoad` and the two flag bits whose descriptor key is established, counting the
rest. Measured in a running park: `objects` 14 → 15, and thing 44 in `rides` with `capacity 5 duration
30` and its script cycling `role 2`, where before it printed no line at all. Thing 13 still carries
riders, which is the two-sided control. **And its entry and exit cells are derived** the original's way:
`FUN_00413410` reads the item's shape grid for the cells holding **9** and **10**, and `bouncy.sam`'s
`S` at column 1 row 0 and `2` at column 1 row 3 land exactly on the save's own `mEntryPos` 2997 = (52,23)
and `mExitPos` 3381 = (52,26) for the shipped Belly Bounce - two records meeting on one cell, neither
enough alone. That closes `park-engine.md`'s "whether mType 9 and 10 really are entrance and exit".
**Its way in and out are typed too** - entrance `type 9`, exit `type 10`, both headings measured off the
game rather than derived (`cell (42,23) type 9 direction 0x01` after a buy, against the shipped ride's
own `(52,23)` reading), because this tree holds two compasses that disagree by name.
**Still no guest boards it.** The placement-time join is built and measured not to link: `Cardinal`
steps bit `0x10` while the entrance carries `0x01`, so its type-9 test can never pass, and the shipped
park's own bit could not have been earned under that rule either. **Either the authored heading or that
test's sense is wrong** - that, and two further findings, are recorded on Q3.
Mutations, each called in advance: **7**, **3**, **1** and **1** red; three survivals named at their
tests, because nothing in the suite buys anything.

**2026-09-22 - the two Confirm clauses that were owed photographs have them.** Branch
`alexah/108-photograph-the-two-confirms`, no code changed. All four camcorder stops read `type 4`,
4 of 4 where predicted and 0 of 4 inside the footprint; and the gate burst showed "Lost KINGDOM" legible
at 7.4 s and the gateway at 8.7 s before `Loaded jungle in 1745 steps`.

**2026-09-22 - the camera swings onto the gate and flies into the island before a park loads.** Branch
`alexah/107-camera-flies-into-the-park`. Alexah remembered the original showing something between the
click and the loading screen and the decode said it showed nothing; **Alexah was right** - the reading
had stopped at the first of five steps, and `+0x14` is the lobby camera's own state machine. Homing
5.63 s predicted and measured; fly-in 2.88 s predicted and measured.

**2026-09-22 - the lobby's attract camera was measured against the original and is faithful.** Branch
`alexah/106-attract-camera-aim`. All three of that item's claims refuted: the wander box is the
original's field for field, the aim does not swing (5.31 against 5.59 °/s), nothing jerks. What remained
was a deviation request, and Alexah chose to fly closer - the lever is the **height**, not the box.

**2026-09-22 - the camcorder stops at a ride instead of walking through it.** Branch
`alexah/105-camcorder-stops-at-objects`. The original sweeps the step cell by cell and asks `FUN_004d8750`
at mode 2 at each boundary. Ended inside the footprint 4 of 4 before, 0 of 4 after; ended where predicted
0 of 4 before, 4 of 4 after, with heading drift 0.000 either side.

Everything older is the git log.
