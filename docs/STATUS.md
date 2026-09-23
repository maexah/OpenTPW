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
  moved, carried; staff hired, fired, picked up, put down. **A staff drop the park refuses keeps the
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
- Selling a thing lets nobody go: its riders stay aboard and its queuers stay put (`docs/QUEUE.md` Q36).
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

`docs/QUEUE.md`, from the top. **Q1 to Q12, and Q35, are ticked.** Next is **Q36**: selling a thing lets nobody go.
Q4 filed Q36-Q38, Q5 Q39, Q6 Q40, Q8 Q41-Q42, Q9 Q43, Q10 Q44, Q11 Q45-Q46, and Q12 Q47-Q49.

`docs/PLAYER-GAPS.md` still holds gaps **4, 5 and 7**. `docs/CLEANUP-PLAN.md` has all nine items closed
and is still untracked, so it exists on this machine only; Q13 moves it into `docs/history/`.

## Not verified on screen

- **The RIDER on a ride bought this session.** Measured five times over; not photographed, because the
  rider sits at z 10.3 against a 5.0 camcorder eye at pitch 0 and the console has no pitch argument.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: unwiring either leaves the suite green.
- Nothing puts a staff member in a cell's occupancy list *as they walk*.
- The critical-section cap trips only in a test: nothing the game ships can reach it (Q11).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **979**, 0 fail, 0 skip with the game | 2026-09-23, after Q12 |
| Tests without the game | **447** ran, **532** skipped, of 979 | 2026-09-23, after Q12 |
| Build warnings | 123 | 2026-09-23, after Q12 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-23 - four hollow tests.** Branch `alexah/126-four-hollow-tests`, `docs/QUEUE.md` Q12. The texture cache,
camcorder walk, lobby fly-in and park-pause tests each stayed green with their fix reverted; each now fails, and so
does every smaller piece of those fixes that a 28-agent review's mutation hunters found surviving: 46 mutations, each
predicted red and red. The tests reach the wiring through stand-ins made without a constructor (a cached texture, a
level holding Lost Kingdom), `Audio.Ready` set for a test, and the lobby sequence stepped at two frame rates - one
rate cannot tell `* Time.Delta` from `/ 60` (VERIFYING rule 120). No behaviour changed; the item asked for no game
run, and the game confirmed it all the same, every reading predicted and photographed: the sea adopted with its Wrap
sampler, the fly-in's stepped readings, the camcorder stopped at the Belly Bounce, and a new `voices` census holding
the placed thunder behind the menu. Filed Q47 (two more hollow tests), Q48 (three holes in the camcorder's sweep), Q49.

**2026-09-23 - the small fixes.** Branch `alexah/125-small-fixes`, `docs/QUEUE.md` Q11, eight commits: the six
patches from `~/Downloads/opentpw/`, each checked against the executable or the compiler and corrected where it
overstated; six doc comments moved to their members; and a cap on a critical section. The original's turn loop has
no bound (`docs/exe/park.md`), so a `CRIT_LOCK` that loops hangs it; `RideScript.CriticalStepCap` ends that turn
instead. None of the 150 shipped sections loops; the longest runs 23 instructions. Confirmed against a control on `main`: the
park frame at load is pixel-identical and the sound banks log the same; after 60 s `rides` read `critical longest
5 cap 10000 reached 0`, as predicted. Photographed. Filed Q45 (the VM charges `CRIT_LOCK`) and Q46.

**Earlier items, kept now only in the git log.** `alexah/124` let the camcorder forget a left park's save (Q10,
filing Q44); `123` gave each ride's screams their own clock (Q9, filing
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
