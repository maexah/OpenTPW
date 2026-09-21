# Status

Last updated: 2026-09-21 on branch `alexah/95-what-a-park-costs`. **Nothing on this branch is
pushed.**

**No sha is written here any more, and that is deliberate.** This line used to name the tip, and it was
wrong every time — a line inside the commit that moves the tip cannot name it, so it was stale the
instant it was written and went stale three times in one day besides. The count of commits ahead is
left out for the same reason. Read both from the repository, which cannot lag:

    git log --oneline -1
    git log --oneline origin/alexah/95-what-a-park-costs..HEAD | wc -l
    git ls-remote origin alexah/95-what-a-park-costs

The tip is the newest `alexah/N` branch and has everything. Confirm with
`git branch -r --sort=-committerdate | head -3`.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate
  swinging open as you enter that park, and — with nobody playing — the camera flying itself around
  all four islands with all four heard at once, each from its own island.
- Park: enter from the lobby; ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (3 of 6 buttons).
- Building and staffing: the **purchase menu** and the **hire screen** both open from the gadget's Buy button and reach each other. Things can be bought, sold, moved and carried; staff hired, fired, picked up and put down. **Clicking a placed ride opens its management window**, which cycles between rides, deletes and moves.
- Spending: guests choose, queue for and **buy from the Drinks Shop and the Jungle Spray**, are charged on leaving, take the item's effects, and a sideshow winner is paid its prize.
- People: 13 guests and 5 staff read from the save, drawn, walking, paying at the gate, queueing, boarding.
- Rides: every placed thing runs its script; 72 of 106 opcodes implemented, the rest counted by `Unimplemented`.

## Does not

- No **paths or queues** to build or delete, no finances, litter, saving a park back, video, networking. Three gadget buttons (Info, Money, Research) are still inert. The two global income pools, the balloon and costume arms, and the litter-bin errand (guest state 9) are named and unbuilt.
- **Nothing is placed by POINTING yet.** The buy and hire screens put an item or a person in the hand and the console's `put` and `hire` finish the job; counted as `PLACE_BY_POINTING` and `PLACE_STAFF_BY_POINTING`. Eight of the nine per-object windows are unbuilt (only the ride's), as are the ride window's stats table, preview and slider commit.
- The `meter.wct` mapping behind the happiness gauge is wrong — the last fault Alexah found by playing that is still open.
- 34 opcodes unimplemented. Three README lines and `RideScriptFile.cs:99` still quote older counts.

## Next

`docs/PLAYER-GAPS.md` — the **eight** gaps a player meets, in the order they meet them. **Four are done**
(1, 3, 6, 8); **four remain** (2, 4, 5, 7). Alexah sets which one is the goal; one per session.

**The current goal, set 2026-09-20, is item 2 — the management gadget's buttons**, starting with the
purchase menu and building toward paths, queues, and placing, moving and managing objects. Alexah chose
the *large* half of that item deliberately; the warning on it is a statement of size, not a veto. The
purchase menu is `FUN_004acc70` and the dispatch is already decoded in `docs/exe/hud.md` — do not
re-derive it.

**Where it stands, 2026-09-21.** The half Alexah named in this session's goal is done and confirmed on
screen: the purchase menu, the hire screen, and a placed ride's management window, with buy, sell,
move, carry, hire, fire, pick up and put down all working underneath. **Item 2 is NOT ticked**, and
should not be until the rest of it lands: **paths and queues** (step 2 of Alexah's own dependency
order, and the half that still needs the `FUN_004de1f0` queue invalidation hooked up) and the other
three category buttons, Info, Money and Research.

## Not verified on screen

- ~~**The ride loop completing.**~~ **CONFIRMED ON SCREEN 2026-09-20** — four runs of the park showed
  guests reaching `Riding`, the Belly Bounce's queue holding 2 of its 16 places, and the whole chain
  landing as money: the sideshow's till reached 900 and the shop's 1110. This bullet said "no run of the
  game was made this session", which outlived the sessions that ran one.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: both are called from `ParkPeople`, and neither is
  pinned by the suite — unwiring either leaves it green. They rest on the decode, not on coverage.
- Staff never enter the cell-occupancy lists (`ParkState.StandOn` is called only from `PeepBehaviour`).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | 72 implemented of 106 | 2026-09-20, `case Opcode.` labels vs enum members |
| Tests | 818 total, all of them run **with** the game and 0 skip | 2026-09-20, measured at `5c66d2e` |
| Tests without the game | 379 ran, 411 skipped — **of 790, and not re-measured since** | 2026-09-19 review |
| Build warnings | 126 (71 are CS8618 nullable) | 2026-09-20, measured at `5c66d2e` |

## Recent

**2026-09-21 - a park can be built, staffed and managed.** The purchase menu, the hire screen and a
placed ride's management window all open, and the verbs under them work: buy, sell, move, carry, hire,
fire, pick up, put down. Three screens, one new widget (the original's control **type 7**, a scrolling
multi-column list, which nothing in the tree had), and the first world-click path this project has had.

**Clicking a ride opens its window, and there are NINE such windows, not one.** They share base opener
`FUN_0048cea0` and are dispatched by `FUN_00486920( thing )` on the thing's kind byte at `+2` and then
on the item's `WhichUIType`. The ride's is built from the stream at `0x00755150`, walked to a balanced
op 5. **Every verb was identified twice over**: `FUN_0048cd10` sets map tool **0x33** (demolish) and
wears `b_erase`; `FUN_0048cfa0` demolishes and then carries with **0x3b** and wears `b_move` - which
independently confirms that the original's move IS demolish-then-carry, the shape `ParkBuilding.Move`
had already been given from the other direction.

**Three corrections the screenshots caught and the region checks did not.** The hire screen's
"Balance" row was showing the park's cash; `FUN_0049bdd0` fills it from three monthly ring buffers as
cash-in minus total costs, which is last month's NET - a different quantity wearing that label, put on
screen twice. It is blank now, with only the staff bill answered. Its two COST rows are **red**
(`FUN_0065c5d5( 0xff, 0, 0, 0xff )` is called on those two and never the other two). And nothing is
selected when that screen opens - `FUN_0049b5b0` fills the list and never calls the select - so a
build that opened with a filled info panel would be wrong.

**Four instrument faults are worth more than the features.** A region check passed on the buy screen
while every tab inside it was missing, because the list's backing panel covered the rectangle. Crops
anchored by their own centre landed on park grass and reported its colour as a label's - a control's
anchor is **inherited from the window root**, not computed per rect. An ink detector measuring
luminance scored a cell containing "538" identically to the blank cell beside it, because the change
under test was to make that text **red** and pure red is L=76. And a title check "passed" at 98.97%
ink with the window **shut**, because the title sits over sky. Each was replaced by a claim that can
fail: lettering arriving in a cell that was empty, a cycle reaching a different **thing id**, a census
count dropping by exactly one - every one of them paired with a line the game itself wrote.

**Confirmed in a running park, by capture and by console.** The hire screen: clicking a row fills the
info panel with *"Duke Mighten  grade 1  36 a month"*, 0.00% ink to 6.39% against a 0.00% floor. The
ride window: opens over a 1.89% floor at 97.23%, its own close button returns it to 2.65%, the cycle
arrows reach a second ride and the title follows (19.25%), and delete takes the census 15 -> 14 with
the game reporting *"sold 'Belly Bounce' (thing 13) for 500"*. `save/` unchanged within every run.

**Synthetic pointer motion never reaches the game** - a warp with no real movement behind it reaches X
and not SDL, measured twice - so `WindowStack.ClickAt` and a `click x y` console command exist to enter
the real press-and-release path. Only SDL is skipped: the hit test, the row arithmetic and both
handlers are the real ones. `openthing` and `catalogue` were added for the same reason.

**2026-09-20 — guests buy things: `docs/PLAYER-GAPS.md` item 8, both halves.** A filled park took
**1110 at the Drinks Shop** (37 sales at 30) and **900 at the Jungle Spray** (45 at 20), with the new
`spend` census showing the cause before the effect and a screenshot showing guests queued at the
kiosk. `save/` unchanged in all four runs.

**The shop's "structural blocker" was a misread field.** The offer filter compares a queue's length
against the object's `+0x40`, and this project read that as `mQueueSizeInCells` out of the save —
nought for the shop and all three toilets, so nothing could pass. It is not that field as loaded:
`FUN_004de130` **overwrites** `+0x40` by walking real type-3 cells off the map whenever `mBackOfQueue`
is nought, and `FUN_004dd920` calls it *before* reading the count. `+0x40` is a cache, and the save's
copy is the cached answer to that walk — which the implementation proves by **reproducing both cached
pairs the save already holds**: the Belly Bounce recomputes to 4 cells ending at 2866, the Jungle
Spray to 1 ending at 3765, and neither number was put in. The argument that should have raised the
doubt years earlier is that the three toilets are in the identical position: under the old reading no
toilet in any park could ever be visited.

**And a shop needed no new mechanism at all.** `docs/exe/ride-operation.md` said a shop takes its money
through LIMBO; `Coconut.RSE` declares **zero** limbo and walk slots and uses neither family, running
the same `VAR_LETMEON` → `WAIT 1000` → `VAR_LETMEOFF` handshake a ride runs. That page contradicted its
own list of limbo users, and the list was right. Four claims on it are corrected, including
`FUN_004e1a10`, which is **`mCostOfGoods`** and not the chance-of-winning accessor it was called twice.

**What made a visit do anything was the win roll, which nothing had ever written.** `FUN_004e2670`
rolls as a guest enters and writes the result into `mQueuePos`; the settle-up splits on that byte, so
with it always nought **every visit in the park took the losing arm** — which is why a sideshow charged
twenty and did nothing else. The chance is `100 - UsageInfo.InitChanceOfLoosing`: a shop declares none,
so its chance is 100 and a drink is always served; the Jungle Spray declares 75, so 25.

**Two things got predicted wrong and measured right.** The Jungle Spray's prize is **50**, not the 5
two separate readings transcribed — which flips winning from costing 30 happiness to gaining 19, and a
test caught it. And the first explanation of why no shop sale appeared in a live run (thirst) was
refuted by the instrument built to test it: `thirstMax 100` with 21 guests over 50 and still no sale.
The real reason is that thirsty guests are rarely still *deciding* — of 148 samples at thirst 50+,
**73 were HeadingForExit and only 11 Deciding** — so a `thirst` console command was added to create
the condition, justified exactly as `load` is for the seaplane.

**2026-09-20 — all three vehicles drive, and `TRIGWAITANIM` is the whole of why they did not.** The
ferry and the seaplane stood still for one reason: `Ferry.RSE` and `seaplane.RSE` start every animation
with `TRIGWAITANIM` where `bus.RSE` uses plain `TRIGANIM`, and that opcode had no case — so it fell to
the counted default and the one instruction that would have started their route was stepped over.
Setting script variables could never have moved them: `ParkObjects.Sweep` poses a stood thing from its
animation channel, and a channel nothing triggers does not advance.

It was left counted on purpose — with no model bound it compares against its **raw third operand** and
parks the script for ever — and `ParkFixedItems` binding models is what made it safe to build. Measured
across every shipped script rather than taken from the note that claimed it: **133 uses in 48 scripts,
132 with operand three differing outright and the last a variable, none equal**, so a faithful
model-less path would hang all 48. The model path is reproduced exactly (`0x552c1a`: mark `+0xbc` with
the role plus one, rewind four words *without* giving up the slice, re-entry compares channel nought's
role plus one, clear at `0x5535f4`); the model-less path is a declared deviation that steps over,
counted — which is why the test pinning the old behaviour still passes **untouched**.

Confirmed in a live park with all three moving in one run: bus `619.3 → 510.5`, seaplane
`491.9 → 483.5 → 461.4`, ferry `791.9 → 601.7 → 554.2`. Guests now step off *after* the aircraft lands
rather than before — `peeps` holds at 13 through about nine seconds of approach, then 16 → 21 → 25 → 30.
**A first run read that pause as a stall and it was not:** the probe had stopped polling too early.

**And then the park emptied itself, which was this session's own defect.** Every vehicle script parks
**three** times a circuit - `bus.RSE` at instructions 42, 87 and 117 - each setting a status and then
spinning on `TEST VAR_TRIGGER / ENDSLICE / BRANCH_Z` onto itself. Releasing only the first, which is
what sending a spent load away did, left the bus stopped at the second: measured with a new `vehicles`
console census as pc 90, `VAR_STATUS` 4, `VAR_TRIGGER` 0, unchanged from 69s to 169s, with one guest
still owed. Arrivals are gated on the vehicle reporting 2, so after the very first load nobody else
ever came and the park drained to nought.

The tail of `FUN_004cf3e0` at `LAB_004cf4b6` is what was missing: it runs on **every** tick, not only
while a load is being dropped, and re-triggers the vehicle whenever its status is -1, 0 or 4 (and on 2
with an empty load). Summoning, releasing and sending away are all the **same write** - `FUN_0051a2f0`
ends by setting variable nought, `VAR_TRIGGER`, to one on the vehicle already standing; only a freshly
*created* thing is different, getting `VAR_STATUS` = 1. Status 6 additionally makes `FUN_0051a690`
forget the vehicle so the next load picks afresh. With `ParkPeople.StepVehicle` added the bus cycles
through statuses 1, 2, 3, 4 and 5, arrivals recur, and the population moves both ways again -
13 → 15 → … → 9 → 10 → 7 over three unattended minutes, 5 in and 10 home.

**All three were then watched round a full circuit**, with the `vehicles` census rather than by eye:
the bus through statuses 1, 2, 3, 4 and 5; the seaplane pc 15 → 29 → 41 → 81 → 29, looping, statuses
1, 2, 3 and 6, with `VAR_TRIGGER` caught at 1 on the status-6 release; the ferry pc 17 → 24 → 38 → 17,
looping, while `peeps` climbed 13 → 49 → 109 → … → 258. **The ferry's statuses 4, 5 and 6 were never
sampled and that is polling, not absence:** returning to pc 17 is reachable only through `BRANCH ->2`
at instruction 69, which lies past both the status-4 spin at 42-45 and the status-6 spin at 64-67, so
the loop closing is itself the proof it traversed them. Polls were 23.6s apart and those stages take a
second or two.

**One probe error worth keeping, because it nearly read as a defect.** An earlier attempt had the
ferry apparently stuck at pc 24 for seven polls. `parkprobe` re-sends every command each round, so
`load 70` reset the outstanding count to 70 every 14.4s while only about 58 thing ticks fit in that
window - the load could never empty, and pc 24 is the spin released by emptying it. `load 40` cleared
comfortably, which is why the seaplane cycled and the ferry appeared not to. Widening the round to 20s
showed the circuit at once. A vehicle starved by the instrument looks exactly like a vehicle that is
stuck.

**And then they were looked at, which the censuses above are not a substitute for.** A moving mesh-0
position proves a transform is being updated, not that anything is drawn - the distinction this project
has already been caught by once, reading an empty road as "never drawn" when it was a capture that
settled at 2.5s while the bus arrived at 13s. `vehicleshot.py` in the harness cache takes a burst of
frames while polling `vehicles`, and writes the pc and status into each file name, so no picture stands
on its own. Thirty-nine frames, every one presenting (mean brightness 103-119, never black):

| | at `VAR_STATUS` 2, unloading | after it pulls away |
|---|---|---|
| bus | at the stop between the two shelters, by the crossing | moved well left along the road (status 3) |
| seaplane | on the water in the bay, floats down | moved and swung round, heading changed (status 5) |
| ferry | alongside the quay | moved along the quay (status 3) |

One frame carries all three at once - seaplane on the water, bus on the road above, ferry to the right.
The arriving crowd is visible too: the entrance path is empty at the start of the seaplane run and a
column of guests is streaming down it by the time the load is spent, which is the pick-up half of the
loop seen rather than counted. Frames are in `~/.cache/tpw-harnesses/shot-{bus,seaplane,ferry}/` and
deliberately not committed - this repo ships no game content.

**Two things about that are honest rather than flattering.** The arrival gaps measured 18.9s and then
32.0, 38.7, 38.7 - so the rate is no longer `TimeBetweenArrivals` alone but the timer **or the vehicle's
circuit, whichever is slower**, which is what gating on the vehicle must mean. And the park is still
net-negative, because the headcount is floored at `Arrival.MinPeople` = 1 while `FUN_004c8240` stays
undecoded; that deviation predates this work and is unchanged by it.

**One approximation is named and not built:** `FUN_0051a9d0` chooses between two sets of states by
asking whether a peep is standing at the stop, on four cells around `FUN_004d8650`'s first cell. Which
balance-file pair that getter returns is **still unproven** - the `+1` among `{c, c+1, c-0x100, c-0xff}`
means the pair is adjacent, which rules out `BusStopA/B` at (42,5) and (53,5) and leaves
`CrossingBSSideA/B` at (47,5) and (48,5) as the likeliest reading rather than an established one. An
attempt to settle it by cross-reference failed both ways: all four globals are READ-only from
`FUN_004d8650` itself, and the executable holds **no** `BusStop` or `CrossingBSSide` strings, so the
balance loader matches those keys without them. Every state the original ever nudges is nudged here
instead, which changes which arm fires and never whether a vehicle moves.

**Two corrections worth keeping.** The `paths` census's "of the route" figure is static clip data, not
live progress — it reads 98.8 and 104.0 on every poll while the models are visibly travelling, so a
reading of "parked at the end of its route" drawn from that column was wrong; mesh 0's position is the
only honest signal. And the first test written for the mark was **hollow**: a mark left standing still
falls through, so asserting the instructions after it passed under mutation. It now asserts that the
second trigger actually *queued*, which is the thing that stops happening.

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

**(superseded the same day) Not done: the ferry and the seaplane still do not move.** They are stood and
they have ids, but scripts are bound by walking the save's object list, and the save names neither — so
nothing drives them. Both halves of that were then fixed: a second binding pass gives them scripts, and
`TRIGWAITANIM` gives those scripts something to drive. See the entry at the top of this file.

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
