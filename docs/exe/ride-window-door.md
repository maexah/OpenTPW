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
- Condemned, missing/unfinished track, maintenance and breakdown statuses
  precede ordinary closure. Exit-disconnected status 24 can also precede it.
  A closed flag alone does not prove the original would show code 1.

## Implementation and boundaries

`ParkObjectWindow` routes the real toggle callback through
`ParkPeople.SetRideClosed` and the existing `ParkRideOperation.Close/Open`.
The operation updates the object, script and hoardings; closing clears the nominee.
The callback stays unguarded, while `WindowStack.Release` refuses a disabled button.
The door and warning refresh immediately after a click or a change of selected ride,
and each frame, including queue changes that leave `CanLoad` unchanged.

`ParkClosedStatus` supplies ordinary non-track status 1/23 and its colour. Known
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
