# The park door and gate (Q89, Q89b)

**Implemented and confirmed in Lost Kingdom (Q89b).** The entry-price door opens and closes
the gate through ordinary dispatch. Populated closure waits for the position-cell census;
the 30-world-sweep retry additionally waits for live staff outside. Screenshot and predicted
census evidence is recorded below. Q90's advisor messages remain counted.

## What writes the command

`FUN_00519ef0` takes two stack arguments after its world `this`: the requested open flag,
and the end-of-park flag. The world stores `mParkClosed` at `+0x1da710` and the gate thing
handle at `+0x1da732`. Resolve the gate's script through the thing's `+0x24` handle.

| Address / offset | Original name (if known) | What it is | Evidence |
|---|---|---|---|
| `0x00519f17`..`0x00519f66` | — | Nonzero second argument: write gate `VAR_COMMAND = 2`, then return without the ordinary park/ride/message arms. The direct caller is the end-of-park routine `FUN_005168f0`, at `0x00516ada`. | Ghidra instructions and callers |
| `0x00519f76`..`0x00519fc0` | — | Open request changes a closed park to open and writes command **1**. An already-open park skips the write. | Flag guard and `PUSH 1; PUSH 0` before script lookup |
| `0x0051a091`..`0x0051a161` | — | Close request changes an open park to closed, then writes command **0** only if `FUN_004c9130() == 0` and gate `VAR_STATUS == 1`. An already-closed park skips this immediate check. | Flag guard, census, status read, `PUSH 0; PUSH 0` |
| `FUN_0055a070` / `FUN_0055a0b0` / `FUN_0055a390` | — | Lookup takes one handle; write takes `(script, index, value)`; read takes `(script, index)`. Values and indices pushed before lookup survive its `ADD ESP,4`. | Callee instructions, especially write `0x0055a0d1`..`0x0055a0d8` |

Do not read the decompiler's extra lookup arguments as real parameters. The surviving stack
values feed the subsequent read/write. The script declares command and status at indices 0
and 1, but OpenTPW must resolve them by name. Ride closing/opening and the unconditional
advisor messages remain described in [ride-operation.md](ride-operation.md#the-closed-ride---fun_004e0450).

## Which people count as inside

`FUN_004c9130` walks the world's linked thing list with `FUN_00516130` and
`FUN_00516160`, resolving each handle through the 20-byte thing table. It counts exactly
those with **kind byte `+2 == 1`** (guests) and a nonzero `FUN_004fa990` answer.
It does **not** separately filter the thing's `+3` byte, guest state, admission flag, queue membership,
ride membership or a cell's occupancy list. Staff are not counted here.

`FUN_004fa990` reads the integer cell coordinates from position bytes `+5` (x) and `+7`
(y), makes `y*128+x+1`, and indexes the 68-byte runtime cell array at `0x008023a0`.
It asks the type dword at cell `+8`; the accepted set is **{0, 1, 3, 9, 10}**:

| Address / offset | Original name (if known) | What it is | Evidence |
|---|---|---|---|
| `0x004c9186`..`0x004c9196` | — | Require kind 1 and the position-cell predicate, then increment count. | Instructions in `FUN_004c9130` |
| `0x004fa994`..`0x004fa9b6` | — | Byte coordinates, packed cell id and runtime cell stride. | Instructions, no cell-list traversal |
| `FUN_00536310` | — | Type **1** (path). | Compare at `0x00536315` |
| `FUN_00536320` / `FUN_00536340` | — | Types **3 or 9**, then a redundant type-9 test. | Compares `0x00536323/28/45` |
| `FUN_00536350` / `FUN_00536390` | — | Types **10**, then **0**. | Compare `0x00536355`, zero test `0x00536395` |

This is a terrain-type predicate, not a park-boundary flag test. Type-30 approach cells,
type-4 object footprints and every other type fail. A guest whose stored position is on
terrain type 0 counts even without path links; a rider is decided by that same stored
position, not automatically counted because they are riding.

## The later close check

**The door is not the only command writer.** `FUN_00516380` increments the world's
`mGameTick` (`+0x1da70c`) and attempts a delayed close after the thing-sweep stage
(which is skipped when world `+0x1da738 == 4`). The attempt requires a nonzero gate handle,
the incremented tick divisible by **30**, a closed park, gate status **1**, and the same
guest census **0**.

It then scans the thing list for a blocker: **`+3 == 0`, kind 4, 5, 6, 7 or 8 (staff),
and `FUN_004fa990 == 0`**. Any one prevents closure. With none, it writes gate command
**0** at `0x00516639`. Thus the retry waits for staff outside the accepted terrain types;
**the immediate door-close arm does not perform this staff scan**.

Evidence: tick and guards `0x0051643b`..`0x00516503`; staff scan
`0x00516567`..`0x0051658e`; blocker branch `0x005165d3`; two zero pushes
`0x0051662b/2d`. The ordinary game-loop caller is `0x0054f7bb`; a second call exists at
`0x005166f2`. The period is 30 world sweeps, not 30 script ticks or rendered frames.
No elapsed-time measurement was made.

## What 0 and 2 mean to the script

The shipped script-content reference is **FileFormats `vm/park-gates.md`**, checked against
all four themes' `features/gates.wad`; it holds the word indices and exact sequence.
In Lost Kingdom, command **0** with status 1 plays the normal closing clip and resets
status to 0, then returns to the dispatch loop. With status already 0 it simply idles.
Command **1** takes the ordinary opening path (as does any nonzero command other than 2).

Command **2** bypasses that normal status guard: music mute, child creation, two special
animation waits, then a faster closing clip, and finally a permanent `ENDSLICE`/`BRANCH`
loop. It never returns to command dispatch. Writing 1 afterwards cannot reopen that
same script instance without resetting its control flow. This is an end sequence,
**not an interchangeable ordinary close command**.
These are decoded script effects, not observed animation timing or screenshots.

## Implementation and verification (Q89b)

`ParkPeople.DoorMoved` commands the gate before its ride loop. The guest census reads live
navigator positions and `ParkState.Record` cell types, independent of occupancy, guest activity
and admission. `RetryGateClose` runs after the thing sweep, against the incremented unsigned
world tick modulo 30. The console's `gate` command is a pure getter for command, status,
closed flag, world tick, guest census and outside-staff count. A missing bound gate script
still reports `PARK_DOOR_COMMANDS_THE_GATE` when its door is moved.

**The staff byte is a lifecycle flag, not `Staff.Activity`.** The base constructor writes zero
at `0x0050aff1`; the successful base load resets it at `0x0050b340`. Firing calls the thing's
deletion (`FUN_00505790` → `FUN_0050b780`), which writes `+3 = 1` at `0x0050b8e9`, queues
deletion and changes kind to 2. OpenTPW's live staff list loads/hire-adds live staff and removes
fired staff immediately. Iterating that list with kinds 4–8 implements the lifecycle guard
for these paths; it must remain so if deferred destruction is introduced. Non-idle staff
outside block the retry too. Primary Q89b artifacts: `thing-init-ins.txt`, `thing-load.txt`,
`thing-delete.txt`, `fire.txt` and `deferred-close.txt`.

A fresh closed gate starts with command 0 and its closed base pose. A successfully resumed
gate keeps its saved command, program counter, variables and animations, including a pending
opening in a closed park. A missing or invalid saved script falls back to initializing the gate command from the park flag.
Command 2 is never substituted for normal closure. For malformed off-map managed positions,
the census returns false as a safety guard; the decoded native byte-coordinate lookup has
no equivalent bounds guard. The existing omission of world-state-4 sweep suppression is not
changed here; the retry follows every supported world sweep.

**Running game, predicted before reading.** Both completed runs used the entry-price screen's
real door button through `click`, with muted audio. The admitted guest and staff placement
are console setup instruments; the door, departure walk, periodic retry and animations are
production paths. `confirm.py` and `staff-confirm.py` produced these observations:

| Scenario | Command / status | Guest census / staff outside | Evidence |
|---|---|---|---|
| Fresh closed park, tick 4 | 0 / 0 | 0 / 0 | `runtime2/fresh-closed.png` |
| Door opened, tick 20 | 1 / 1 | 0 / 0 | `runtime2/opened.png` |
| Empty door closed, tick 36 | 0 / 0 | 0 / 0 | `runtime2/closed.png` |
| Reopened, tick 53 | 1 / 1 | 0 / 0 | `runtime2/reopened.png` |
| One inside guest, door closed | 1 / 1 | 1 / 0 | `runtime2/populated-close.png` |
| Last guest outside, retry tick 60 | 0 / 1, then 0 / 0 | 0 / 0 | `runtime2/run.log`, `last-guest-left.png` |
| Empty census but staff outside, ticks 780/810/840 | 1 / 1 | 0 / 1 | `staff-runtime2/run.log`, `staff-blocks-empty-gate.png` |
| Staff moved inside, retry tick 870 | 0 / 1, then 0 / 0 | 0 / 0 | `staff-runtime2/run.log`, `staff-inside-gate-closed.png` |
| Reopened after deferred close, tick 904 | 1 / 1 | 0 / 0 | `staff-runtime2/deferred-reopened.png` |

All paths in that table are below `~/.cache/tpw-harnesses/q89b/`; each run's `run.log`
contains the matching census. Prediction lines are in `runtime2-summary.log` and
`staff-runtime2-summary.log`. The close-up images visibly show both gate leaves closed,
open, and reopened. An unchanged open-frame pair was also captured. Both completed runs
left game-save file hashes unchanged after removing only the verification player created
by the fresh-park run. Earlier exploratory runs changed only loading-count cache
`save/opentpw.cfg`, which was reported and left intact. One harness used the wrong hex
OK-button ID; one staff fixture had no inside guests and therefore correctly took the
immediate close. Both were corrected before the completed runs.

**Regression limits.** `ParkGateTests` covers ordinary script close/reopen, changed-door and
status guards, census types and guest activities, exact world-sweep cadence through the real
update, staff-kind and non-idle activity guards, fired-staff removal, fresh closed startup,
missing saved gate state and saved closed commands 0/1 without counter/variable clobbering.
Saved-closed restoration, missing saved state and every staff kind were test-verified, not
runtime-verified; the photographed outside blocker was the mechanic. Invalid program-counter
fallback and malformed positions were source-reviewed only. No advisor behavior was implemented.

Primary artifacts: `q89/door-census.txt`, `census-followup.txt`, `cell-types-writer.txt`,
`tick-opcode-table.txt`, `gates-listings.txt`, `content-check.json`, `corpus.log`, in the
harness scratch directory. The private Ghidra project was hash-checked against the original
(20 copied files); `/testme.exe` SHA-256 was
`cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`, matching both the source
reference and installed `TP-nodisc.exe`. All **106** opcode name/arity entries matched the
fresh Ghidra table; **308/308** corpus scripts parsed and **2664/2664** branch targets fell
on instructions. Four freshly extracted gate scripts matched the corpus byte for byte.
No binary layout was changed or inferred from one file. These Q89 artifacts establish the decode;
Q89b runtime evidence is above.

Q89b mutation checks restored eleven faults: omitted door and retry calls, command 2 for ordinary
close and fresh closed startup, saved-command overwrite, total guest count, occupancy-based
count, a 29-sweep period, omitted staff guard, activity mistaken for lifecycle, and the staff
guard incorrectly applied to immediate closure. Each failed the new tests. The occupancy
mutation initially survived because the fixture had no separate occupancy mapping; the test
now explicitly stands its subject on type-30 cell (2,2) while its navigator is at (1,1), and
that mutation fails. `mutations.json` records the final failures; first-attempt results were
retained. Source and binary were restored and the full suite passed: **1679**, no failures or
skips, **121** build warnings and no errors. Independent Astra review assessed supplied primary
excerpts and applied code; child filesystem access failed, so direct file/log/runtime checks
were performed by the parent. The review restored the explicit zero-script-handle guard.
