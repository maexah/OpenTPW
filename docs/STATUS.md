# Status

Last updated: 2026-09-20 on branch `alexah/94-every-state-answered`, tip `88cd6de`.

The tip is the newest `alexah/N` branch and has everything. Confirm with
`git branch -r --sort=-committerdate | head -3`.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate
  swinging open as you enter that park, and — with nobody playing — the camera flying itself around
  all four islands with all four heard at once, each from its own island.
- Park: enter from the lobby; ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (2 of 6 buttons).
- People: 13 guests and 5 staff read from the save, drawn, walking, paying at the gate, queueing, boarding.
- Rides: every placed thing runs its script; 71 of 106 opcodes implemented, the rest counted by `Unimplemented`.

## Does not

- No buying, building, hiring, finances, shops serving, litter, saving a park back, video, networking.
- The `meter.wct` mapping behind the happiness gauge is wrong — the last fault Alexah found by playing that is still open.
- 35 opcodes unimplemented. Three README lines and `RideScriptFile.cs:99` still quote older counts.

## Next

`docs/PLAYER-GAPS.md` — the seven gaps a player meets, in the order they meet them. Alexah sets which one
is the goal; one per session.

## Not verified on screen

- **The ride loop completing.** The boarding chain is wired and its arithmetic is covered, but only one
  test method drives it through the production entry point, and no run of the game was made this session.
  Treat "guests ride" as tested, not as confirmed in the game.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: both are called from `ParkPeople`, and neither is
  pinned by the suite — unwiring either leaves it green. They rest on the decode, not on coverage.
- Staff never enter the cell-occupancy lists (`ParkState.StandOn` is called only from `PeepBehaviour`).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | 71 implemented of 106 | 2026-09-20, `case Opcode.` labels vs enum members |
| Tests | 805 total, all of them run **with** the game and 0 skip | 2026-09-20, run repeatedly |
| Tests without the game | 379 ran, 411 skipped — **of 790, and not re-measured since** | 2026-09-19 review |
| Build warnings | 126 (71 are CS8618 nullable) | 2026-09-20, unmoved by three commits |

## Recent

**2026-09-20 — the guest loop closes: they arrive by themselves, and they go home.** A park left alone
now runs `peeps 13 → 14 → 15 → 13 → 12 → 11` without anything typed — seven arrivals and ten
departures over two and a half minutes.

`ParkPeople.StepArrivals` is the manager (`FUN_004cf3e0`): it waits out `Arrival.TimeBetweenArrivals`,
picks a vehicle by how big the crowd is, and then drops **one guest per thing tick** until the load is
spent. The period is in quarter-ticks, so 150 is 600 of the 31 ms ticks — **18.6 s predicted, 18.9 and
18.8 measured**, which is the clearest evidence yet that it is decoded rather than tuned.

Departures are `ParkPeople.Depart`, triggered by `ExitLevel` running out — a countdown that had been
ticking since the save was first read with **nothing anywhere reading it**. It reverses everything
`Admit` wires, including `ParkState.Forget`, which is new: `LeaveCell` unlinks a cell's own chain but
leaves `_cellOf` naming a cell the thing is no longer on, and that entry decides what a later `StandOn`
undoes. A guest a ride or a queue is holding is refused, which is the original's own condition.

**The deviation is in what an unbuildable state means, not in a transition that works.** A leaver walks
`HeadingForExit → PickingACellOutside (19) → AtTheBusStop (21) → Leaving (17)`, and 19 and 21 both read
a balance-file cell pair that is still unproven. Rerouting `HeadingForExit` was tried first and three
tests that pin that transition said no — correctly. So 19 is treated as the end of the walk instead,
which leaves every tested transition untouched.

**Not done: the ferry and the seaplane still do not move.** They are stood and they have ids, but
scripts are bound by walking the save's object list, and the save names neither — so nothing drives
them. The vehicle a crowd's size selects is computed and currently discarded.

**2026-09-20 — a guest who was never in the save arrives and walks to the gate.** `ParkPeople.Admit`
makes one: a thing id above everything the file used, a `GuestState` whose cash comes from
`PeepTypes[x].StartingCash` and whose exit level comes from `PeepInfo.ExitLevel`, and a navigator at
the centre of the cell. Five things then have to learn about them, each failing quietly on its own if
missed — the simulation list, the by-id index, the walk, the sprite, and the cell's occupancy list,
without which the gate cannot see them. Driven by hand from the console (`arrive`) because nothing yet
runs the timer.

Confirmed in a live park: `peeps` 13 → 14 → 15, the newcomer at exactly the cell centre, and then
`AtGate` → `HeadingForGate` and walking east toward the ticket booths at ~0.12 cells a tick with a real
two-waypoint route.

**One deviation, declared at the site.** The engine constructs a guest in `Deciding` and walks them in
from outside through `WalkingOutside` and `AtTheBusStop` — both of which take their cells from a
balance-file pair that is not proven, so `PeepBehaviour` deliberately answers neither. Left in
`Deciding` out there a guest stands for ever, because `Decide` looks for somewhere inside the park and
they are outside it. So this starts them at `AtGate`, the head of the admission sequence the original
joins them to anyway, and skips the walk in. It goes back the moment that cell pair is measured.

Also corrected: two comments in `PeepBehaviour` argued from "no bus thing runs a script in this
project", which this session made false. The behaviour they guard is unchanged — the other half of
each argument still stands — but the reasoning no longer rests on something untrue.

**2026-09-20 — the arrival mechanism, decoded end to end.** How a park gets new guests is now written
down in `docs/exe/park.md`: `FUN_004cf3e0` waits out a timer, asks `FUN_004cf5b0` for a headcount,
summons a vehicle, and then makes **one guest per tick** through `FUN_004cf720` until the load is
spent. Three findings worth the space. **The vehicle is chosen by how big the crowd is** — under 36
the bus, up to 60 the seaplane, beyond that the ferry — and the save's own field names,
`mArrivalVehicle_Size1..3`, say the same thing from the other side. **The vehicle thing is made on
demand**, which is why the shipped park places a bus and neither of the others: its small-crowd slot
holds the bus and the other two have never been needed. And **nobody rides in anything** — the guest
is constructed at a cell near the stop, so the vehicles are mechanism rather than transport.

`ParkWorld` now exposes the four header fields it had been parsing and discarding —
`ArrivalVehicleForSmallCrowd`, `…MediumCrowd`, `…LargeCrowd` and `CurrentArrivalVehicle`. A test pins
the small-crowd slot to the bus **by catalogue number rather than by the 15 it happens to hold**. That
test was written asserting all four were nought, which is what the code comment claimed; it failed,
and both the comment and the claim were wrong in a way that explained the ferry's absence better than
the original guess did.

Still not established: the arrival **period**. Nothing in the executable writes the three globals the
rate comes from, and the timer counts quarter-ticks of the game clock rather than seconds.

**2026-09-20 — the bus drives its route.** `LobbyModel.Pose` applies channel `0x200`: it samples the
clip's percentage, finds the point that far along the model's closed Bézier route, and moves the node
the track names — carrying whatever hangs off it, so the wheels ride on the body. Positions are written
onto the entities and never into `Offsets`, which stays the rest pose, so the `Rest()` → `Place()`
restore every clip change already relies on still puts everything back.

The route's points are **parent-local**, which cost a run to learn: measuring the shift against the
composed `Offsets` instead of the node's own translation dropped the spline root's (480, 0, 170) — the
same value on all three vehicles — and drove a correctly-shaped route 480 west and 170 south of where
it belonged. Against the local rest instead, the bus reaches **(510.5, 65.5, 0.0)**, predicted to the
decimal before it was read. It comes to rest between `BusStopA` and `BusStopB` (cells 42,5 and 53,5 —
world 425,55 and 535,55) without anything in the code knowing where those are.

**Two things it does not do yet.** It drives its route once and parks, because the clip holds at
220.0/220.0 and nothing loops it. And it does not turn to face the way it is going — the engine samples
the route's first derivative for that (`0x400`, on 62 of the game's 71 route tracks), which is unbuilt
and counted as `ANIM_PATH_FACING`. The second one is obvious on screen: the bus arrives at the stop
sitting diagonally across the crossing, keeping whatever heading it was parked with.

**Looked at, not only counted.** The first attempt photographed an empty road and read as "the bus is
never drawn" — it was a timing miss, because `shotat.py` settles for 2.5 s and the bus does not reach
the stop until about 13 s after the park loads. Caught at the right moment it is plainly there, on the
road mid-route and then at the shelter. Every render gate was checked rather than assumed on the way
past: `Position` is the final translation in `ModelMatrix`, `DrawnByOwner` is set nowhere but the
advisor, and the wheels carry no opaque model but do carry a translucent one.

**2026-09-20 — animations say how far along its route a thing is.** Channel `0x200`, the last sizeable
undecoded one, is **path progress**: a record of `{ float start, count, 0, offset }` and then one float
per frame, the values immediately behind their own header on all 71 of the game's tracks. The value is a
**percentage of the route**, not a distance — space's slide runs exactly 0 to 100 over 121 frames, the
haunted house 0 to 200, which is two laps — and it does not always rise, because the ferry travels its
route backwards. `0x200` is read on its own: `0x400` accompanies it on 62 of the 71 and `0x1000` on the
other nine, and neither changes the record. **Confirmed in a live park:** `bus: 3 progress scalar(s),
42.4..142.5, 100.0 of the route` — three clips that chain into one lap. This still does not move the
bus; it is the number that will.

**2026-09-20 — models read the routes they carry.** The `uint` at model file `0xac` is an array of
16-byte records naming a route's points, and `ModelFile` reads it: the bus, ferry and seaplane have one
each, the haunted house four, and twenty-four of the game's 2,118 models have any at all. Nothing in the
file gives a count — every `u16` in `0x90..0xc0` was measured against the known counts and none is one —
so the number of records comes from the nodes that index them at `+0x52`, floored at one because three
models have a route no node names. **Confirmed in a live park, not from the suite:** the new `paths`
console command answered `paths 3`, with gates and lights routeless and `bus: route 0 type 2 bezier 45
points, first (207.4, 0.0, -247.8)` — every figure predicted before it was read. **The bus still does not
move**: its script runs to `220.0/220.0 HELD`, because reading a route and following one are different
steps. Format written up in the FileFormats clone.

**2026-09-20 — positional lobby audio while the camera flies.** All four parks sound at once, each
heard from its own island — the marked emitter node where there is one, the island itself where there
is not — so the blend between parks is distance rather than a cross-fade. With somebody playing it
collapses to the single island on show, flat or at its node, exactly as before. **A deviation, not a
restoration:** the engine's 3D is the original's (`Sound_PlayEffect` really takes x, y, z, and the game
delay-loads QMixer for it), but the original spent none of it in the lobby, which plays everything at
(0,0,0) — corroborated four ways. Measured by disk capture rather than assumed: `sounding=4` on 17 of
20 polls, peak 0.2499 (−12.0 dBFS), muted control exactly 0.0, so four parks summing does not clip and
the levels calibrated for one park stand. The capture also caught a gap a green build could not: the
one-shots were still flat on three of the four islands.

**2026-09-20 — the attract camera: the lobby flies itself around all four islands.** Lobby plan item
8. With no player selected the camera wanders a box and aims at whichever island is nearest, which is
the first branch of the original's `FUN_005e0470`; with a player it orbits as before. Every number is
read from the lobby object's constructor `FUN_005dfcd0` — box centre (500, 75, 500), extents
(400, 50, 400) full-size, speed 1.0 a tick, arrival radius 10, look cap 2.0 a tick — and the box turns
out to be the lobby's own geometry, which is what says the axes were read the right way round. One
number was chosen rather than read: the look-speed ramp, which the original applies per frame with no
delta. Decode in `docs/exe/lobby.md`. Confirmed in the running game over three minutes: seven island
changes against a predicted six, all four islands reached, and the sound following the camera round.

**2026-09-20 — `docs/PLAYER-GAPS.md` item 1: the gate opens as you enter a park.** It idles shut and
plays its opening clip once, and the park is asked for when the doors finish. The original does none of
this — its entry beat is three calls and a UI close — so it is a rule 11 gap-fill, said at the site.
**Not every gate is a pair of hinged doors**: fantasy's is a worm with no rotation tracks, and taking
the play length from the rotator alone left it inert, so a gate is played for as long as it *moves*.
Confirmed in the running game, jungle and fantasy, against predicted durations; the tests that pin the
rule are mutation-checked, and the two that only pin the file data are recorded as hollow against it.

**2026-09-20 — the memory diet, steps 1 to 5 of `docs/MEMORY-DIET.md`.** Claude Code's project memory
went from **56 files / 1.9 MB to 18 files / 332 KB**. Four commits, no source file touched:

| | |
|---|---|
| `31071c9` | The fourteen rule files become `CLAUDE.md`, which loads in full every session. `CLAUDE.local.md` takes the machine paths and is gitignored. The index drops from 55 lines to 34. |
| `859f577` | **The executable decode moves into `docs/exe/`** — thirteen pages from twenty memory files. This is the one that mattered: those facts existed on one machine, unversioned. Accepted mechanically, 1,042 normalised addresses and 451 `FUN_` names, zero missing. |
| `37decdb` | The two archives and the branch ledger move to `docs/history/`, verbatim and md5-verified. Four workflow traps that lived only in the ledger are lifted into `WORKFLOW.md`. |
| `61640a9` | 1,848 lines of verification postmortems distil to `docs/VERIFYING.md` — 404 lines, one per rule, grouped by symptom. The source's numbering is kept because forty rule numbers are cited from other files. |

**Still owed**, and deliberately not done — these are table rows in the diet, not numbered steps:
`docs/DECISIONS.md` (one paragraph each for the three graphics-migration files), `docs/TOOLING.md`
(the capture and console recipes), the review's phase list, `ride-vm-may-be-replaced.md`, the
docs-clone notes, and the three open-item files. The memory folder still holds all of them.

**Earlier, 2026-09-18.** The live tracker was split: the plan stays in `current-task-progress.md`, its
7,852 lines of dated history became `current-task-archive.md`, now `docs/history/`.
