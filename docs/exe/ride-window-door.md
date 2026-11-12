# The ride-window door (Q92)

Implemented 2026-10-03. This is the ordinary ride's door and its closed display;
the complete object-status engine is not implemented.

## Executable evidence

Read in a private copy of the `SimThemePark` Ghidra project, with all
20 source files hash-verified. The reference `/testme.exe` SHA-256 is
`cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`, equal to
`~/repos/game/testme.exe`. The original project and its locks were left alone.

- `FUN_004af600`, control `0x3e38`, calls `FUN_0048ccf0( down == 1 )`.
  Nonzero calls `FUN_004df300` (close); zero calls `FUN_004df390` (open).
  The handler does not guard either call. It then refreshes the window.
- `FUN_004ad4e0` disables a closed ride's door when `FUN_004df290` refuses
  reopening. Its switch position follows `mCanLoad`. The control's help rows
  are 13 (close) and 12 (open), as recorded in the ride-window layout.
- `FUN_004ade40` asks `FUN_00485f60( object, 2 )` for the status.
  Ordinary closure is code **1**, UITEXT **365**. A queued ride whose back is
  disconnected is code **23**, UITEXT **389**, when closed, and **22**, UITEXT **388**,
  while still open: cutting a queue off closes nothing (`ride-operation.md`, Q93).
- The colour table at `0x0074fb50` contains `0xff808080` for 1 and
  `0xff1e96ff` for 22 and 23. `FUN_004861e0` unpacks red from the low byte, then
  green and blue: **128/128/128** and **255/150/30**. The text table starts
  at `0x0074fbc0`; `FUN_004861d0` supplies the list's colour.
- The ride window's status label (`0x3e25`) does not take that colour. It is made once by the window's vtable
  slot `+0x10` (ride `0x004ad720`, shop `0x004afaa0`, sideshow `0x004b0ed0`): a text control on the base's `+0x1c`,
  aligned (1, 1), font slot 8 (`0x00485a70( 8 )`, the list headings' slot), colour (0, 0, 0, 255), then hidden.
  `FUN_004ade40` sets its text only, never its font or colour (fork watch 2026-11-12, verify-ui-hud.md H3; Q213).
- Condemned, missing/unfinished track, maintenance and breakdown statuses
  precede ordinary closure. Exit-disconnected status 24 also precedes it, and it
  is asked whatever the open word: the routine reads its second argument (every
  caller pushes 2) only after the exit test, so an OPEN ride with its exit cut
  off shows 24 too (`0x00486191`..`0x004861b9`; fork watch 2026-10-19,
  verify-world.md L1). A closed flag alone does not prove the original would
  show code 1.

### The statuses behind Q212 (fork watch 2026-10-19, verify-advisor-ui.md L2, verify-world.md L1)

`FUN_00485f60`, the first that applies (the item record's `Info.WhichUIType` `+0x4ac` is 0 for a ride):

1. State `+0x19c` 4 (`0x004e0380`): 3, UITEXT 367, condemned.
2. Track descriptor `+0x9c` 1 or 2 with `+0x2c` 0: `0x19` (386); 3: `0x00441910` 0 gives `0x19`, `0x00441940` 0 gives `0x1a` (387).
3. State 2, waiting for an upgrade (`0x004e03e0`): no mechanic (world `+0x1da73e` nought) 5 (372); mechanics on strike
   (`0x00519450( 1 )`, `0x00509990`) 7 (371); nobody assigned (`0x004e0220`) 6 (370); assigned and at work 9 (373), else 8 (369).
4. A service requested (`+0x64`, `0x004e03d0`): a toilet (`+0x32` bit 0) asks the handyman (`+0x1da73c`): none `0xb` (378),
   on strike (`0x00519450( 0 )`) `0xd` (377), unassigned `0xc` (376), at work `0xf` (379), else `0xe` (375); otherwise the
   mechanics: `0x11` (384), `0x13` (383), `0x12` (382), at work `0x15` (385), else `0x14` (381).
5. State 1, broken down (`0x004e0370`): the assignee at work `0x15` (385), **else 2 (366), whether or not someone is assigned**.
6. `+0x32` bit 3 and not `0x004de4a0`: `0x16` + (`+0x68` 0) (388/389); a ride with no exit (`0x004e2710`), `+0x288` not 2 and
   not `0x004de5a0`: `0x18` (390); then by mode.

"At work" is `0x005065d0`, reached through `0x004e02f0` (which answers 0 when `0x004e0220` finds nobody): a mechanic (thing
kind 4) whose `+0x218` is the object in state `0xd`, or a handyman (kind 5) whose `+0x21a` is it in state `0xb`; another
kind asserts and answers 0. So the toilet statuses are `0xb`..`0xf` and the mechanic's `0x11`..`0x15`, the upgrade's are
5..9, and `0x15` is shown only while the worker is at work. Statuses 4, `0xa` and `0x10` ("Waiting to be ...", 368, 374,
380) are never answered by this routine. Colours (`0x0074fb50`): blue `0x5064ff` for 4..`0x15`, orange `0xff961e` for
`0x16`..`0x1a`, red `0xff321e` for 2 and 3, grey for 1.

**The exit test `FUN_004de5a0`, status 24 (UITEXT 390, "EXIT NOT CONNECTED", 255/150/30, the colour of 22 and 23).**
The cell is `[0x008023a0] + mExitPos * 0x44 - 0x44` (`mExitPos` `+0x38`, `mEntryPos` `+0x36`). The side is that cell's
`mDirection` (`+0xd`), made opposite when `mExitPos` equals `mEntryPos`, as ExitRide does for its destination
(`ride-operation.md`, ExitRide). `FUN_004d97e0` steps to the cell beyond the exit. For each of the other three sides (the
byte table 0x01, 0x04, 0x10, 0x40, skipping the one looking back at the exit) the edge test `FUN_004d8750` is asked from
that cell in walking mode 0; the first side that answers open logs "Connected in direction %d" and the exit is connected.
None: status 24. Its only effect is the window's status; nothing closes the ride. Not measured: whether any shipped Lost
Kingdom exit answers 0.

## Implementation and boundaries

`ParkObjectWindow` routes the real toggle callback through
`ParkPeople.SetRideClosed` and the existing `ParkRideOperation.Close/Open`.
The operation updates the object, script and hoardings; closing clears the nominee.
The callback stays unguarded, while `WindowStack.Release` refuses a disabled button.
The door and warning refresh immediately after a click or a change of selected ride,
and each frame, including queue changes that leave `CanLoad` unchanged.

`ParkClosedStatus` supplies ordinary non-track status 1/23 and its colour; the ride window letters
every status in font 8, black, and leaves the colour to the all-items rows (Q213). Known
states 1/2/4 and track rides remain counted as `OBJECT_WINDOW_HIGHER_PRIORITY_STATUS`,
retaining their previous display rather than incorrectly labelling them CLOSED.
The pre-existing script `VAR_BROKEN` warning takes precedence in the ride window.
`UiList.Row` carries an optional colour; every reused text cell resets to that colour
or white. The all-items screen applies it when constructing its rows.

The warning is drawn over the model preview in the overlay pass, restricted to the
frontmost visible window. This corrects an observed case where the model covered
all the CLOSED lettering. The existing model-preview pass can still draw a covered
ride over another window; Q92 does not repair that broader preview layering issue.
The ordinary closed row was photographed after dismissing the ride window.

The existing coaster-opening guard remains incomplete and counted. Detailed
maintenance/track messages and exit-disconnected status 24 remain outside this
implementation; `ParkClosedStatus` answers 1, 22 and 23. The unresolved exit check is counted
as `OBJECT_WINDOW_EXIT_CONNECTION_STATUS`; ordinary CLOSED is the fallback.
No complete status parity is claimed.
All-items rows are refreshed on opening/changing tab, not continuously.

## Verification

Evidence lives in `~/.cache/tpw-harnesses/q92/`: private Ghidra function/table dumps,
`PLAN.md`, `confirm.py`, mutation logs and isolated-commit gate results.
Final source captures/log: `runtime-release/` (`door-closed.png`,
`closed-all-items.png`, `door-reopened.png`, `covered-ride-warning.png`, `run.log`),
with predictions/assertions in `runtime-release-summary.log`. Logical mutations:
`mutations.json`; screen mutation: `render-mutant-runtime.log`. Exact-commit
build/test/save results: `exact-result.json` and `exact-build.log`/`exact-test.log`.

The running game used the actual data through a private installation with its own
empty save directory, silent audio, and real X11 pointer clicks. The predictions
were **CanLoad 1 -> 0 -> 1**, with **13 peeps** unchanged while paused. The `objects`
and `peeps` logs matched at each step; screenshots show the CLOSED warning, grey
Belly Bounce row, and the warning gone on reopening. The warning's help changes
from close to open and back. Original game saves are hash-checked unchanged.

Seven regression cases exercise actual `WindowStack.Release` callbacks, nominee
and script changes, disabled releases, queue reconnection while still closed,
state/service refusals and rendered list-cell colour/reset. Restoring the entire
original ride-window bug fails all seven. Five further logical mutations fail:
disable removed (5), open unwired (2), row tint unwired (1), disconnected status
removed (1), higher-priority exclusion removed (3).

The initial screen check found the occluded label despite green unit tests. A
pixel regression now predicts **216** grey glyph pixels in the fixed 1280x720
CLOSED-text region. Restoring the render-order defect produces **0** and fails;
restoring the overlay passes. This is a local capture regression, not a portable
font/rasterizer invariant. Full-suite and exact-commit results are recorded in the
live plan and STATUS numbers.

Queue-disconnected wording/disabled-door
cases and other guard refusals are tested, not confirmed on screen. Paused peep
censuses do not demonstrate queue draining or a rider boarding after reopening.
