# Status

Last updated: 2026-09-23.

**This header names no branch and no sha, deliberately.** A line written inside the commit that moves
the tip cannot name it, so every attempt went stale the instant it was written. Read the current state
from the repository, which cannot lag: `git log --oneline -1`.

**`docs/QUEUE.md` is the work queue.** One item per session, taken from the top unless Alexah reorders.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate, and the
  attract camera flying itself around all four islands with all four heard at once. Clicking Enter swings
  the camera onto the gate and flies into the island before the loading screen; **the island keys wait for
  that flight**, and a lobby that ends mid-flight forgets it.
- Park: ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (5 of 6).
- Building and staffing: purchase menu and hire screen, both reachable from Buy. Things bought, sold,
  moved, carried; staff hired, fired, picked up, put down. **Selling or moving a thing puts its riders and queuers
  off where they stand**, and staff resting there get up. **A staff drop the park refuses keeps the
  candidate on the cursor and in the pool.** **Leaving a park lets go of whatever is in the hand.** **A sold thing's script goes with it**, and
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
  at the band its rider count asks for, as the original's chain: a fresh sample every 1-3 s on **its own
  clock**, so a second ride in the same band is neither held up by it nor set off by its stop.
- **A thing bought this session is a member of the running park**: it takes its turn, appears in every
  census, joins the object chain the original keeps live, and carries the entry and exit cells derived
  from its own shape picture. See the top entry under Recent.

## Does not

- No finances, litter, saving a park back, video, networking. Research is inert and has nothing behind it.
- Eight of the nine per-object windows are unbuilt. Patrol areas are dead, deferred by Alexah.
- A guest put off on ground no neighbour connects to only leaves by going home: every failed wander restamps
  the thinking gap (Q53). Leaving a queue any way but a sale costs no happiness (Q50).
- Any right press empties the hand, which for a moved thing is a sale; Escape opens the menu over it (Q39).
- Nothing shows a carried candidate, and any cell on the map takes one; the original's rule is decoded (Q40).
- Escape during the park-entry fly-in opens the game menu over it; the original's cancels the fly-in (Q41).
  The lobby's keys act on the press, not the release, and Enter does not enter a park (Q42).
- The `meter.wct` mapping behind the happiness gauge is wrong - the last fault Alexah found by playing.
- Every other sound still waits out a per-effect "repeat delay" that is really a priority (Q43).
- A left park stays in memory through the lobby, held by `ParkState.Current` and `ParkRides.Current` (Q44).
- The VM charges `CRIT_LOCK` against a script's budget, so a section reached with one unit left runs over two turns,
  unlocked; the original runs it whole (Q45).
- By a probe, not yet the game: the camcorder slips through a shut side at exactly 45 degrees, and is trapped at the
  east edge of the map (Q48).

## Next

`docs/QUEUE.md`, from the top. **Q1 to Q12, Q35 and Q36 are ticked.** Next is **Q39**: the hand's ways out.
Q4 filed Q36-Q38, Q5 Q39, Q6 Q40, Q8 Q41-Q42, Q9 Q43, Q10 Q44, Q11 Q45-Q46, Q12 Q47-Q49, Q36 Q50-Q54.

`docs/PLAYER-GAPS.md` still holds gaps **4, 5 and 7**. `docs/CLEANUP-PLAN.md` has all nine items closed
and is still untracked, so it exists on this machine only; Q13 moves it into `docs/history/`.

## Not verified on screen

- **The RIDER on a ride bought this session.** Measured five times over; not photographed, because the
  rider sits at z 10.3 against a 5.0 camcorder eye at pitch 0 and the console has no pitch argument.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: unwiring either leaves the suite green.
- Nothing puts a staff member in a cell's occupancy list *as they walk*.
- The critical-section cap trips only in a test: nothing the game ships can reach it (Q11).
- The staff half of a sale: nobody in Lost Kingdom rests in the first minutes, so it is tested, not seen (Q36).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **993**, 0 fail, 0 skip with the game | 2026-09-23, after Q36 |
| Tests without the game | **447** ran, **546** skipped, of 993 | 2026-09-23, after Q36 |
| Build warnings | 123 | 2026-09-23, after Q36 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-23 - selling a thing lets the people go.** Branch `alexah/128-selling-lets-the-people-go`, `docs/QUEUE.md`
Q36. Decoded first: the two happiness keys are `PeepInfo.SmallHappinessChange` and `MediumHappinessChange`, by the
balance table's slot order. The destructor's type-10 message now reaches the park's people: a rider or anyone bound
for the thing loses 5 and decides again where they stand, a queuer loses 20 and their queue links, staff resting
there get up, and a rider's `0x80` plays at the seat. A tired staff member's search walks the live chain. Confirmed
against a control on `main`, every sale reading predicted and photographed; the miss was two queuers left stranded
on cleared cells (Q53). Filed Q50 to Q54.

**Earlier items, kept now only in the git log.** `alexah/126` made four hollow tests fail with their fixes reverted
(Q12, filing Q47-Q49); `125` landed the small fixes and capped a looping critical section (Q11, filing Q45-Q46);
`124` let the camcorder forget a left park's save (Q10, filing Q44); `123` gave each ride's screams their own clock (Q9, filing
Q43); `122` made the island keys wait for the fly-in (Q8, filing Q41-Q42); `121` emptied the hand as a park is left (Q7);
`120` kept a refused staff drop's candidate on the cursor (Q6, filing Q40); `119` kept a moved thing in the
hand until a cell takes it (Q5, filing Q39); `118` made a sold thing take its script down and leave bare
ground (Q4, filing Q36-Q38); `115` made a placed ride lay its queue's first cell and hand the player the queue
tool there, fixing the `Info.Shape` alphabet (Q3), and `116` made its squares wave; `117` picked up the path
tool from the park, with Backspace (Q35); `114` let a player build a queue that joins the paths around it;
`110` made a thing the save placed clickable anywhere on its footprint; `109` made a thing bought this session
join the park (Q1, Q1b - the rider is still not photographed); `112` decoded what authors an entrance's queue
link; `108` photographed two owed Confirm clauses; `107` flew the camera into the island; `106` refuted all
three of its item's claims; `105` stopped the camcorder at a ride.

Everything older is the git log.
