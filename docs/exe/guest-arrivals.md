# A new guest's initial values (Q85)

Decoded and implemented, 2026-10-03 (Q85, Q85b). **The original starts happiness at 50, not zero.** It also draws
thirst, hunger and toilet need before the guest takes a turn. The normal arrival caller does
not replace those values. A suitable ride can subsequently raise happiness; the old Q50
observation that arrivals stayed at zero is not a measurement of today's OpenTPW.

## Evidence and limits

Read afresh in headless Ghidra from `/testme.exe`, image base `0x00400000`, SHA-256
`cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`. This matches both local
reference executables. A private copy of the existing Ghidra project was checked by hashing
all 19 files before and after copying; the original session and project were left alone.
The query harness is `ghidra_query.py`; its outputs are `initial-decode.txt`,
`arrival-decode.txt`, `queue-decode.txt`, `identity.txt` and `program-info.txt` in the Q85
session workspace's `evidence/` directory. They contain the decompiler output and instruction
listings, not an imported third-party interpretation.

Q85 was decode-only. Q85b implements the arrival meters, cash/exit variation, prankery
and unhappy queue exit. Runtime and regression evidence is recorded below; static decoding
alone does not satisfy the screenshot and predicted-census gate.

## Construction and arrival

`FUN_004cf720`, the normal arrival manager's one-guest factory, computes the spawn cell,
allocates `0x22c` bytes, calls `FUN_004faec0` at `0x004cf7a4`, and returns. There is no later
meter write in this caller. The other direct constructor call is `0x00516791` in
`FUN_00516700`, a 25-guest creation loop; it likewise adds no post-constructor meter write.
The transport and spawn-cell selection remain documented in [park.md](park.md#arrivals-who-comes-on-what-and-how-often).

The constructor first zeroes its guest fields, then overwrites the values below. Its final
state selection is separate from those assignments: `FUN_004fa990` tests the cell under the
guest. Types 0, 1, 3, 9 and 10 pass; this branch takes the next visitor number into
`+0x1d8` (`FUN_0051aaf0` at `0x004fb1df`: world `+0x1da714` incremented, logged as "Your park has received its
%dth visitor!") and enters state 6. Otherwise two more random draws select and vary a destination;
`FUN_004fa5f0` succeeding enters state 0, failing enters state 6. Neither constructor state
arm in `FUN_00501db0` writes happiness or the needs. This does not imply those meters stay
unchanged through subsequent game turns.

| Guest field | Value at construction | Instruction evidence |
|---|---|---|
| `+0x19c`, happiness | **50.0**, float bits `0x42480000` | `0x004fb075` |
| `+0x1a4`, thirst | `uint(draw) % 50`, converted to float: **0..49** | draw `0x004fb085`, `DIV` `0x004fb091`, store `0x004fb09f` |
| `+0x1a8`, hunger | `uint(draw) % 50`, converted to float: **0..49** | draw `0x004fb0ab`, `DIV` `0x004fb0b7`, store `0x004fb0c5` |
| `+0x1ac`, toilet | `uint(draw) % 30`, converted to float: **0..29** | draw `0x004fb0d1`, `DIV` `0x004fb0dd`, store `0x004fb0fd` |
| `+0x1b0`, illness; `+0x1b4`, litter; `+0x1b8`, tiredness | **0.0** | `0x004fb0e3`..`0x004fb0ef` |
| `+0x1bc`, exit level | `ExitLevel + signed(draw) % (2 * ExitLevelVar) - ExitLevelVar` | `0x004fafe1`..`0x004fb00d`; signed `IDIV` `0x004fb001` |
| `+0x1f0`, kind | Low byte of `signed(draw) % PeepTypesCount` | `0x004fb01f`..`0x004fb027`; signed `IDIV` |
| `+0x1a0`, cash | `max(0, trunc(((signed(draw) % (2*v+1) - v + 100) * StartingCash[kind]) / 100))`, `v = StartingCashVarPc` | `0x004fb03f`..`0x004fb06f`; signed arithmetic, low 32-bit product |
| `+0x1c0`, prankery | `100 + ThingId % 3` when `uint(draw) % 100 < PrankeryLikelihood`, else 0 | `0x004fb114`..`0x004fb152` |

The balance-key mapping is the executable's descriptor table, described in
[park-engine.md](park-engine.md#how-a-key-finds-its-global). In particular the constructor
reads exit level/variation/cash variation at `0x0078504c/50/54`, the selected kind's cash
at `0x007850e8 + 12*kind`, and the kind count at `0x007851d4`. The formula, not a fixed
cash amount or exit timer, belongs in the implementation. Zero divisors are not guarded
in the original; do not silently claim an invented fallback reproduces it.

The guest ID used for prankery and the reseed is the zero-extended 16-bit object ID
(`FUN_0050b350`, then `0x004fb146` and `0x004fb194`); preserve that narrowing.

## The visitor count (Q146)

`FUN_0051aaf0` adds one to world `+0x1da714` (`mNumberOfVisitorsToDate`) and answers the new count. It has two
callers, and each keeps the answer at the guest's `+0x1d8`, the visitors list's first column:

| Caller | When |
|---|---|
| `0x004fb1df`, the constructor `FUN_004faec0` | the guest is made on a cell that counts as the park's (`FUN_004fa990`) |
| `0x004ffb55`, `FUN_004ffb20`, the `Entering` turn | the walk through the gate has ended (`FUN_004fa2a0` answers nought), just before state 6 |

So every guest is counted once. An arrival is made at the stop, outside the park, and takes the constructor's other
arm: no number until the gate.

Measured in the original (stock Lost Kingdom, read-only, `q146/orig/a.log`, 2026-10-08): the count is 0 at the load;
all thirteen saved guests take 1 to 13 as state 5 becomes 6 (ticks 763 to 822); the first load's thirteen are made on
ticks 1312 to 1324 with `+0x1d8` nought and the count still 13; each takes the next number at the gate (ticks 1386 to
1482); 26 at the end, 1 to 26 with no gap.

OpenTPW: `ParkPeople.Admit` numbers a guest only where `Peep.CountsOn` passes, and `PeepBehaviour`'s `Entering`
numbers the rest. The console's `admit` makes a guest on a park cell, so it is numbered by the first.

## Draw order and the reseed

The eight direct draws before the guest constructor's sprite setup are:

| Order | Call | Use |
|---|---|---|
| 1 | `0x004faff8` | Exit-level variation |
| 2 | `0x004fb01f` | Kind |
| 3 | `0x004fb046` | Cash variation |
| 4 | `0x004fb085` | Thirst |
| 5 | `0x004fb0ab` | Hunger |
| 6 | `0x004fb0d1` | Toilet |
| 7 | `0x004fb109` | Result discarded; generator still advances |
| 8 | `0x004fb114` | Prankery |

These are **not the complete constructor call tree's draws**. The person base constructor
`FUN_004f8940` has already drawn the base speed at `0x004f89e1`, modulo five. After the eight,
`0x004fb19e` calls `FUN_00516370` with the guest's ID: it writes the world's generator state
`+0x1da708`. The child-bank picker `FUN_00541f70(0)` then draws through that same world's
other pointer, `0x007cf83c`, at `0x00541f85`, and uses `(uint(draw) >> 2) % bankCount`.
`0x007cf83c` and `0x0080239c` name the same world through `FUN_00407d80` / `FUN_00515660`.
When the cell test fails, direct calls `0x004fb201` and `0x004fb21c` supply the destination
choice and its subcell variation after this reseed. Do not treat the sprite selection as
an isolated random source when claiming exact whole-world sequence parity.

`FUN_00516330` advances with 32-bit wrap (`state * 0x19660d + 0x3c6ef35f`), rotates right
13, saves that state, then returns its signed absolute value. The exceptional word
`0x80000000` remains negative under signed interpretation; the constructor's signed
`IDIV` for exit/kind/cash differs from its unsigned `DIV` for needs/prankery. Do not infer
that every raw return is a positive signed integer. The need ranges above remain valid.

## Rides and the unhappy queue arm

A ride can raise happiness. Freshly traced: `FUN_004fd970` gates the effects on byte
`+0x1f1`; the nonzero branch calls `FUN_004fe1e0`, which calls `FUN_004fdcc0` at
`0x004fe259` before applying the item's effects. The excitement match uses the guest
kind's preference, adds the appropriate balance-key value for gaps below 5, 15 or 40,
and clamps the changed happiness to 0..100. Zero excitement returns; a gap of 40 or more
adds nothing. The zero-byte settlement branch instead docks happiness. Full formulas and
other effects have one home: [ride-operation.md](ride-operation.md#the-excitement-match--fun_004fdcc0).

`ParkRideOperation.SettleUp` calls `MatchTheExcitement` on its successful branch, and the
latter changes happiness. Q85b leaves that existing gain in place and initializes the
arrival's own meters through `ParkPeople.Admit`.

`FUN_004ffff0` reaches the mood branch only after its earlier queue guards and unsigned
`mGameTick - [+0x208] > 30` (`0x00500308`). Happiness is truncated by `__ftol`, then its
low byte is tested: **below 10** posts thought `0xb` (`0x00500338`..`0x0050033f`) and jumps
to the common leave path (`0x00500382` → `0x0050049e`). At 10..19 it starts animation 4.
The other queue arms and common leave path remain in
[ride-operation.md](ride-operation.md#the-inqueue-turn---fun_004ffff0).

## OpenTPW implementation and limits (Q85b)

`ParkPeople.Admit` consumes the base-speed draw and then all eight direct guest draws in
the order above, including the discarded draw. Happiness starts at 50; the need remainders
are unsigned, exit/kind/cash remainders signed, the cash product wraps at 32 bits, and its
integer division truncates before the nonnegative clamp. Prankery uses the ID's low word
and is retained by `Peep` for both a new guest and a loaded guest. Construction logging
records these values before any turn can change them; `peeps` includes prankery.

Remaining deviations are explicit at the code sites:

- The arrival values still draw a separate `System.Random`, not the shared world generator.
  Its normal outputs exclude the original's exceptional signed-negative return; regression
  inputs exercise that arithmetic boundary anyway. `ChildOf` independently reproduces the
  ID-reseeded child bank, without changing subsequent world draws.
- The kind count remains the shipped eight; the general balance-parser row count is Q54.
- Guests start at the admission sequence's `AtGate`, skipping the original outside walk and
  its conditional destination draws (Q128). This does not claim whole-call-tree draw parity.
- Guest tiredness remains unrepresented in `GuestState`/`Peep`; no tiredness behavior is built.
- Thought `0xb` is thought there (Q110b; `ride-operation.md`, "Thoughts and their pictures").
  The unhappy queue arm itself runs the common leave path after its existing guards and
  unsigned gap greater than 30: slot release, unlink, happiness dock and return to deciding.

`ParkGuestArrivalTests` exercises the real constructor with scripted raw draws: exact
fields/order, endpoints, the discarded draw, two successive guests, prankery threshold,
the signed-negative boundary, and cash truncation with a forced kind that still consumes
its draw. Queue tests exercise 9/9.9 versus 10/10.9, low-byte wrapping, 30/31, and the shared
unlink/dock path. Choice fixtures now explicitly remove competing needs when comparing
kind preference, history or shelter.

Restoring zero initialization fails all four arrival tests; restoring the held queue arm
fails `AnUnhappyQueuerLeavesBelowTenAfterTheMoodGap`. The full regression suite and live
confirmation are recorded in the verification results below. Overflow/clamp cash content,
a guest ID above 16 bits and loaded nonzero prankery are source-reviewed, not separately
exercised by these new tests.

## Verification results (Q85b, 2026-10-03)

The Q85b session workspace holds `evidence/runtime/`: `predictions.txt`, `run.log`,
`cohort-summary.json`, the eight `after-<seconds>s.txt` censuses, and screenshots. The
prediction before `load 30` was **30 new guests, each happiness 50**; all thirty IDs
43..72 matched, with all initial needs, cash, exit and prankery inside the decoded bounds.
The screenshot `after-30s.png` shows the crowd entering; its paired census has **43** guests,
the thirteen saved guests plus thirty arrivals. Every arrival in this run drew prankery 0;
nonzero prankery is demonstrated by the scripted constructor tests, not this screen.

Censuses every thirty simulated seconds retain IDs rather than treating a shrinking
population as a mood improvement:

| Seconds | All guests present | Of the original thirty | Of those thirty departed |
|---|---:|---:|---:|
| 30 | 43 | 30 | 0 |
| 60 | 43 | 30 | 0 |
| 90 | 38 | 30 | 0 |
| 120 | 32 | 26 | 4 |
| 150 | 25 | 22 | 8 |
| 180 | 18 | 15 | 15 |
| 210 | 8 | 6 | 24 |
| 240 | 5 | 3 | 27 |

The first prediction of a positive cohort ride gain by 120 seconds was refuted: arrival
51's gap was 40, so it gained zero; the earlier +5 belonged to saved guest 31. By 150 seconds,
arrival **66** had ridden Belly Bounce: excitement 40 against preference 65, gap 25,
predicted **+5**, logged **50 to 55**, and census `after-150s.txt` records happiness 55
and one ride. Arrival **56** later got the predicted **+15**, **0 to 15**, for gap 10;
its maxed needs subsequently docked it back to zero. `after-120s.png` and `after-240s.png`
were inspected; the individual gain magnitudes are log/census evidence, not numerals
visible in the park screenshots.

Arrival **60** naturally left the queue unhappy between 150 and 180 seconds. A focused
instrumented check then admitted guest **75**, sent it to Belly Bounce, and confirmed
`InQueue` at 50 before setting happiness to 9. Predicted **one additional unhappy exit**
and thought count **1 to 2**; after sixteen frames the census shows `Deciding`, destination
0, no queue place and happiness 0, and the log names its unhappiness exit. The census counts
`QUEUE_TURN_THOUGHT_0xB` exactly twice and has no `QUEUE_TURN_UNHAPPY`. Paired screenshots
`unhappy-before.png` and `unhappy-after.png` were inspected: the state change initially
leaves the guest at its feet. A power interruption lost the subsequent walk-away capture;
it is not used as evidence. The preserved four-minute run and immediate-exit checks remain
valid. The only save-file change recorded before that interruption was `opentpw.cfg`, the
loading-bar cache; the other original save files retain their run-start hashes.

The final source build has **0 errors, 121 warnings**; the full suite with real game data
has **1641 passed, 0 failed, 0 skipped** (`final-source.trx`). The two defect-restoration
runs are `mutation-zero.trx` and `mutation-held.trx`.

The separate post-outage recovery run is `evidence/runtime-recovery/`. It reproduced a
single predicted unhappy exit for guest **44**: `unhappy-before.txt` has `InQueue`,
happiness 9; `unhappy-after.txt` has `Deciding`, destination 0, no place, happiness 0,
and thought count **1**. Five seconds later `walked-away.txt` has `Wandering` at
(50.557,22.209), away from (52.523,22.946); `walked-away.png` was inspected against the
queue screenshot. It is movement out of the queue, not proof that the guest left the park.
A different saved guest (35) reached the same unhappy arm during those later five seconds;
it is separate from the one-exit, sixteen-frame measurement.

The recovery run exited cleanly; its before/after save-file hashes are identical.
