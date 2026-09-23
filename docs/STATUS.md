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
  with a different sample each pass, at the band its rider count asks for.
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

## Next

`docs/QUEUE.md`, from the top. **Q1 to Q8, and Q35, are ticked.** Next is **Q9**: stopping one ride's scream
releases the effect every ride in its band shares. Q4 filed Q36-Q38, Q5 Q39, Q6 Q40, and Q8 Q41-Q42.

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
| Tests | **953**, 0 fail, 0 skip with the game | 2026-09-23, after Q8 |
| Tests without the game | **435** ran, **518** skipped, of 953 | 2026-09-23, after Q8 |
| Build warnings | 125 | 2026-09-23, after Q8 |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-23 - the island keys wait for the fly-in.** Branch `alexah/122-island-keys-wait-for-the-fly-in`,
`docs/QUEUE.md` Q8. Decoded first, every claim put to two refuters: the original's next and previous handlers
refuse while its camera is leaving for a park, and every lobby builds that camera afresh with the leave at
nought. `LobbyCameraMode.Step` is the pair; the bracket keys (ours) ask through it; `ForgetIsland` clears the
leave. Confirmed in the game with a real `]` mid-flight, every number predicted: on `main` the camera turned
to Wonder Land and the jungle loaded anyway, and a rebuilt lobby flew on and loaded it unasked; with the fix
`staying on island 0`, the flight ran on into Lost Kingdom's gate, photographed, and a rebuilt lobby read
`leave=No`. The decode found the original's Escape cancels the fly-in (Q41) and its keys act on release (Q42).

**2026-09-23 - leaving a park empties the hand.** Branch `alexah/121-leaving-a-park-empties-the-hand`, `docs/QUEUE.md`
Q7. Decoded first, every claim put to three refuters: the original's park end takes its interaction mode down while
the park stands - in the save it makes on leaving, or online in its teardown - so a moved thing stays sold and a
candidate goes back. `Level.ForgetPark`, part of `Unload`, lets both hands go through their own `Drop`. Confirmed in the game by the
player's routes, every number predicted: on `main` a moved Belly Bounce outlived Exit To Lobby and the jungle's next
first click built a second one for 500, and a candidate outlived it into Wonder Land and was hired back in the jungle;
with the fix the hand came back empty and the click only picked up the path tool, 87987 and candidates 22, photographed.

**2026-09-23 - a refused staff drop keeps the candidate.** Branch `alexah/120-a-refused-drop-keeps-the-candidate`,
`docs/QUEUE.md` Q6. Decoded first, every claim put to a refuter: the original's place-staff click answers a
refused cell with an inert log line and nothing else, and the pool loses a carried candidate only on an
accepted click. `ParkStaffPool.Hire` is now the one body for the click and the console's `hire`, hire first
and take second. Confirmed in the game, every number predicted: on `main` a drop off the map printed `hired Duke Mighten
as thing 0` and his row left the hire screen, 22 candidates to 21 with 5 staff; with the fix he stayed, 22
and 5, and the next click hired him as thing 43, 21 and 6, photographed. The cell rule and the carry preview
are counted and filed as Q40.

**Earlier items, kept now only in the git log.** `alexah/119` kept a moved thing in the hand until a cell
takes it (Q5, filing Q39); `alexah/118` made a sold thing take its script down and leave
bare ground (Q4, filing Q36-Q38); `alexah/115` made a placed ride lay its
queue's first cell and hand the player the queue tool there, fixing the `Info.Shape` alphabet on the way
(Q3), and `116` made its squares wave; `alexah/117` made the path tool picked up from the park, with
Backspace taking a run back up (Q35); `alexah/114` let a player build a queue that
joins the paths around it (the first half of Q3); `alexah/110` made a thing the save
placed clickable anywhere on its footprint (`ParkPicking.ThingOn`, `VERIFYING.md` 113-114); `109` made a
thing bought this session join the park (Q1, Q1b - the rider is still not photographed); `112` decoded
what authors an entrance's queue link; `108` photographed two owed Confirm clauses; `107` flew the camera
into the island; `106` refuted all three of its item's claims; `105` stopped the camcorder at a ride.

Everything older is the git log.
