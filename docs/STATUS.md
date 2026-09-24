# Status

Last updated: 2026-09-24.

**This header names no branch and no sha, deliberately.** A line written inside the commit that moves
the tip cannot name it, so every attempt went stale the instant it was written. Read the current state
from the repository, which cannot lag: `git log --oneline -1`.

**`docs/QUEUE.md` is the work queue.** One item per session, taken from the top unless Alexah reorders.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate, and the attract camera
  flying around all four islands with all four heard at once. Enter swings the camera onto the gate, opens it and flies
  in before the loading screen; **the island keys wait for that flight**, and **Escape cancels it** (the camera orbits
  again, the gate shuts, the panel comes back). **Every lobby key acts on its release**, and **a left press on the
  lobby's view enters the park**.
- Park: ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (5 of 6). The
  camcorder walks the original's sweep pass for pass: it slides along what is shut and never goes into a ride.
  Leaving one lets go of all of it: nothing of a left park is held in the lobby.
- Building and staffing: purchase menu and hire screen, both reachable from Buy. Things bought, sold, moved, carried;
  staff hired, fired, picked up, put down. **Selling or moving a thing puts its riders and queuers off where they
  stand**, staff resting there get up, and its script goes with it. **Cutting a queue puts out whoever stands past its
  new end.** A staff drop the park refuses keeps the candidate. **The hand holds one thing and lets go of it the
  original's ways** (quick right click with RMB cancel on, Escape, Delete, the camcorder, a new pickup, leaving); a moved
  thing stays in it until a cell takes it. A placed ride's window opens from a click **anywhere on its footprint**.
- Information and money: Info and Money open all-staff, all-items, all-visitors and entry-price screens. **The
  entry-price door shuts the park and every ride a guest may be offered**, drawn down when shut: a shut ride turns its
  queue away one head a sweep, for 15, and opening the door or editing its queue opens it again.
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
  from its own shape picture. See `docs/QUEUE.md` Q1 and Q1b.

## Does not

- No finances, litter, day ending, saving a park back, video, networking. Research is inert and has nothing behind
  it. In a park the advisor says the gadget's opening line and nothing after it (`docs/PLAYER-GAPS.md` gap 4).
- Eight of the nine per-object windows are unbuilt. Setting a staff member's patrol area is not built, deferred by
  Alexah; staff keep to the areas the save gives them. A walking member of staff is not entered in the cells they
  cross; only hiring and putting down place one.
- A guest put off on cleared ground no neighbour connects to leaves only by going home (Q53, decode first). Of the
  seven ways out of a queue, four are counted, not built (Q50c-f). An arriving guest's happiness is 0 and stays (Q85).
- The park's door moves neither the gate (Q89) nor the advisor (Q90), nor a shut ride's model (Q91); the ride window's
  door shows a shut ride but is not a button (Q92), and a bought queued thing starts open (Q93).
- A right press over a panel still cancels (Q56), and the park's Escape acts on the press, not the release (Q57).
- Nothing shows what the hand holds, a thing (`CARRY_PREVIEW_MARKERS`) or a candidate (`STAFF_CARRY_PREVIEW`), and
  any cell on the map takes a candidate; the original's rule is decoded (Q40).
- The fly-in's fade to black is not drawn (Q61). Escape over the player slots opens the game menu (Q64); Ctrl+H acts
  on its press and F8 is not built (Q65); a disabled button still takes the pointer (Q66).
- The happiness gauge draws wrong: two copies of the bar, split down the middle (`docs/PLAYER-GAPS.md` gap 5;
  cause not yet measured).
- Every other sound still waits out a per-effect "repeat delay" that is really a priority (Q43).
- Guests may arrive eight times as often as the original's, and staff may idle for an eighth of its time: its timers
  read the thing sweep, ours the 31 ms tick (Q68, Q82, decode first).
- The camcorder is entered where the orbit looks, not by a click on the ground, so it can start off the park, where it
  cannot move, and leaving keeps the walk where the original's throws it away (Q25).

## Next

`docs/QUEUE.md`, from the top. **Q1 to Q12, Q35, Q36, Q39, Q41, Q42, Q44, Q45, Q47, Q48, Q48b, Q50 and Q50b are
ticked.** Next is **Q50c**: the door's price opinion. Q4 filed Q36-Q38, Q5 Q39, Q6 Q40, Q8 Q41-Q42, Q9 Q43, Q10 Q44, Q11
Q45-Q46, Q12 Q47-Q49, Q36 Q50-Q55, Q39 Q56-Q60, Q41 Q61-Q63, Q42 Q64-Q66, Q44 Q67, Q45 Q83-Q84, Q48 Q48b, Q50 Q50b-Q50f
and Q85-Q88, Q50b Q89-Q94, and the 2026-09-24 staleness audit and its review Q68-Q82 (Q70-Q75 from the 2026-09-12
review, section G from the lobby plan).

`docs/PLAYER-GAPS.md` still holds gaps **4, 5 and 7**. `docs/CLEANUP-PLAN.md` has all nine items closed
and is still untracked, so it exists on this machine only; Q13 moves it into `docs/history/`.

## Not verified on screen

- **The RIDER on a ride bought this session.** Measured five times over; not photographed, because the
  rider sits at z 10.3 against a 5.0 camcorder eye at pitch 0 and the console has no pitch argument.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: unwiring either leaves the suite green.
- The critical-section cap trips only in a test: nothing the game ships can reach it (Q11).
- The staff half of a sale: nobody in Lost Kingdom rests in the first minutes, so it is tested, not seen (Q36).
- Tested, not run in the game: the Delete key's and a sale's let-go of a candidate (Q39), Escape before the gate opens
  (Q41), the name box's two releases in one frame and a park whose global.sam will not load (Q42).
- The camcorder's tie and four of Q48b's put-backs move the viewer under 0.2 units: the census's, not a photograph's.
- Q50's slot let go, its nominee and `EnteringRide` kept, and a queue walk giving up at a stale link: tested only.
- Q50b's reopen by an edit of the queue, a head forced on, the guard's refusals and a bought ride's bit: tested only.
- A lock taken on the last unit running its section whole: tested only. Nothing the stock park runs arrives there; the
  one route is the Hot Pot with its capacity cut mid-ride, and it rests on `BUMP` being unbuilt (Q45).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** of 106 | 2026-09-21, `case Opcode.` labels vs enum members |
| Tests | **1083**, 0 fail, 0 skip with the game | 2026-09-24, after Q50b |
| Tests without the game | **475** ran, **608** skipped, of 1083 | 2026-09-24, after Q50b |
| Build warnings | 123 | 2026-09-24, after Q50b |
| Park load | **2.5 s**, worst phase `terrain` 0.72 s | 2026-09-21, three jungle runs |

## Recent

**2026-09-24 - a closed ride turns its queue away (Q50b).** Branch `alexah/139-a-closed-ride-turns-its-queue-away`.
The park's door takes an OPEN flag, so down is shut and ours was drawn the wrong way round. Pressing it in the game shut
all six rides and put out the Belly Bounce's seven queuers one a sweep, 50 to 35; all held but one guest's.

**Earlier items.** Each ticked item's whole account is its entry in `docs/QUEUE.md`: `alexah/138` (Q50), `137` (Q48b),
`136` (Q48), `135` (Q47), `134` (Q45), `133` (the staleness audit), `132` (Q44), `131` (Q42), `130` (Q41), `129` (Q39),
`128` (Q36), `126` (Q12) to `118` (Q4), `115`-`116` (Q3), `117` (Q35), `109` (Q1, Q1b - the rider is not photographed).
Before them, `114` built a queue that joins the paths, `110` a click anywhere on a footprint, `112` the queue link.

Everything older is the git log.
