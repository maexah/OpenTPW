# Staff wandering into queues and outside the park (Q206)

Alexah reported this while playing, 2026-10-03. Implemented on
`alexah/257-staff-wander-boundaries`, based on `f7567d0`.

## Cause and executable evidence

`StaffBehaviour.SetRandomDest` asked whether the route could cross an edge, without the original's
separate linked-cell destination filters. The general edge test permits both connected queues and the
park approach. `PatrolRoll` also omitted the path-only destination check.

The canonical decode is [SetRandomDest](ride-operation.md#setrandomdest---fun_004f9490) and
[the staff turn](ride-operation.md). Rechecked this session in a private headless Ghidra
copy: `0x004f95c6`–`0x004f9826`, the cell accessors `0x00522770`, `0x00522810`, `0x00522850`,
`0x00536310`–`0x00536350`, and the patrol target test at `0x00506fdb`. The direction vectors are
runtime-initialized; their disk bytes are zero. Initializers at `0x004d9a65`, `0x004d9b05`,
`0x004d9b95`, `0x004d9c35` establish the outgoing cardinal directions. `CellEdge.BitFor` describes the
entered side, so the source mask uses its opposite.

The private project identified `/testme.exe`, x86 LE 32-bit, base `0x00400000`, SHA-256
`cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`, matching the reference executable.
The original project and its locks were untouched. No new file-format layout was inferred.

## The fix and its deliberate containment rule

Staff choose from the live source links, reject queue/entrance destinations when standing on path,
reject the forward direction of an ordinary queue, and reject ride exits. Entrance sources retain
their linked directions. Patrol fallback destinations must be ordinary path cells.

**Destination filtering alone did not fix the report.** A regression run with those filters still crossed
from row 17 onto row 16 while aiming at an allowed point `(47.0195,17.0195)`. On seed 0, tick 1031,
the researcher reached `(47.198,16.968)`. The unbuilt no-links recovery then lets wandering continue outside.

**Deliberate deviation:** apply the same linked-cell limits to route planning, wall avoidance and physical
steps during staff's free-walking state. This additional containment is not claimed as original behavior.
`PeepWalk` scopes the extra predicate to one operation and restores it in `finally`; refills within that
operation share it. Rest and strike walks retain their general routing. Guests are unchanged.

The original multi-cell random walk and slot-selection order are built for staff inside their area
(`LinkedWander`, [ride-operation.md](ride-operation.md), "Q108"); the zero-link recovery is built too
(Q112, the same page, "OpenTPW takes the arm"), and job finding remains Q133. A worker deliberately dropped on
unlinked terrain or already outside is not the linked-path containment case verified here. No blanket restriction was added to hiring,
placement, rest or strike travel.

## Running-game evidence

Artifacts are under `~/.cache/tpw-harnesses/staff-wander/`: `runtime.py`, each run's `run.log`, screenshots,
summary logs, and before/after save hashes. Runs use the real Lost Kingdom park and dummy audio.

- Baseline queue: predicted at least one excursion, observed researcher 30 at `(49.058,22.358)`,
  then on the Belly Bounce queue/entrance throughout 950 census samples. The first camera was scaled
  incorrectly; those photographs are **not visual proof**. A corrected-camera repeat did not reproduce
  entry within 120 seconds, recorded rather than discarded.
- Baseline gate: predicted at least one excursion after placing researcher 30 at `(47,17)`.
  Observed `(48.119,16.958)` after 10.39 seconds; `baseline-gate/outside.png` shows the researcher on
  the approach. Over 120 seconds the researcher visited rows 7 through 18, with 848 forbidden-position
  rows across 1,900 guard/researcher census rows. This was sustained wandering, not just a boundary twitch.
- Fixed queue: predicted **zero** queue/approach excursions with staff still moving. Observed zero across
  1,918 guard/researcher census rows over 120 seconds. The researcher visited 16 distinct cells and the
  guard 17. `fixed-queue/start.png` shows the researcher beside the queue on the ordinary path;
  `fixed-queue/end.png` and the final census show the continuing park run.
- Fixed gate: predicted **zero**, observed zero across **1,922** guard/researcher census rows over
  120 seconds. The researcher visited 14 distinct cells and the guard 25. `fixed-gate/start.png`
  shows the researcher on the park side of the gate; `fixed-gate/end.png` shows the approach free of staff.
  Final log lines 6206–6207 give researcher `(47.974,23.376)` and guard `(56.378,25.174)` inside the park.
  The matching queue-run lines 6207–6208 give `(48.412,20.316)` and `(39.183,21.567)`.

Runtime assembly SHA-256: `2ba87282cbb7f6d5c69c1bf14a34ca5503166a0e8fbde183059f0f4ef1cd108e`.
All completed baseline and fixed runs left the real `save/` file hashes unchanged. Sampling is approximately
0.12 seconds plus console latency, not an assertion about every rendered frame.

## Regression and review

`ParkStaffBehaviourTests` adds 13 cases: both staff kinds at the queue, both gate columns for the researcher,
32 seeds × 400 sweeps per placement, live-cell patrol type checks, queue direction/entrance escape,
asymmetric source links, exit rejection, rest travel on the same walk, and restoration after a thrown
constraint. Assertions measure actual positions and require movement; standing still cannot pass.

Restoring the complete baseline `StaffBehaviour` causes **9 failures**. Removing just movement containment
causes **3 failures**; removing just the patrol type gate causes **3 failures**, one each for queue,
entrance and approach destinations that are otherwise reachable. Logs: `mutation-summary.log` and
`mutations.json`. Restored build: **121 warnings, 0 errors**; full suite **1,654 passed, 0 failed, 0 skipped**
with the real game data. Exact-commit isolation is recorded in the task's live plan after committing.

Rest/exception controls and synthetic direction edges are tested, not photographed. The original game was
not replayed to compare its transient overshoot; no such equivalence is claimed.
