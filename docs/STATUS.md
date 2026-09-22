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
  census, and joins the object chain the original keeps live. See the top entry under Recent.

## Does not

- No finances, litter, saving a park back, video, networking. Research is inert and has nothing behind it.
- Eight of the nine per-object windows are unbuilt. Patrol areas are dead, deferred by Alexah.
- The `meter.wct` mapping behind the happiness gauge is wrong - the last fault Alexah found by playing.
- **A bought thing still has no entry cell**, so no queue can serve it and no guest can board one. That is
  `docs/QUEUE.md` Q1b, and it is a decode task before it is a build.

## Next

`docs/QUEUE.md`, from the top. **Q1 is split and unticked**: its list-reads half has landed and is
confirmed by census, and its own confirm clause waits on **Q1b**, the entry/exit cell derivation.

`docs/PLAYER-GAPS.md` still holds gaps **4, 5 and 7**. `docs/CLEANUP-PLAN.md` has all nine items closed
and is still untracked, so it exists on this machine only; Q13 moves it into `docs/history/`.

## Not verified on screen

- **A guest boarding a ride bought this session.** Blocked by Q1b, not by the object-list work.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: called from `ParkPeople`, neither pinned by the
  suite - unwiring either leaves it green. They rest on the decode, not on coverage.
- Nothing puts a staff member in a cell's occupancy list *as they walk*.

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **880**, 0 fail, 0 skip with the game | 2026-09-22, seven added for the object chain |
| Tests without the game | **411** ran, **469 skipped**, of 880 | 2026-09-22, the skip count unmoved by the seven |
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
riders, which is the two-sided control. **No guest boards it yet** - `EntryPos` is also unset, so the
queue walk has nowhere to start; split out as Q1b with the derivation already read off the disassembly.
Mutations, each called in advance: never-become-head **7 failed**, no-unlink **3 failed**, and the sweep
reverted to the file's list **survived all 880** - said at the test, because nothing in the suite buys
anything.

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
