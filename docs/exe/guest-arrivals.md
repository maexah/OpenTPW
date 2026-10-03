# A new guest's initial values (Q85)

Decode only, 2026-10-03. **The original starts happiness at 50, not zero.** It also draws
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
listings, not an imported third-party interpretation. An independent Astra review rechecked
the relevant instructions and applied documentation (`verifier-decode.txt`, `verifier-review.md`).

This session changes documentation only. No new arrival values, queue behavior, regression
test or mutation are implemented. No running-game screenshot or census is claimed. Q85b
must obtain both, with its prediction recorded before observing the count, and must restore
the defect to demonstrate that its new test fails. Static decoding does not meet that gate.

## Construction and arrival

`FUN_004cf720`, the normal arrival manager's one-guest factory, computes the spawn cell,
allocates `0x22c` bytes, calls `FUN_004faec0` at `0x004cf7a4`, and returns. There is no later
meter write in this caller. The other direct constructor call is `0x00516791` in
`FUN_00516700`, a 25-guest creation loop; it likewise adds no post-constructor meter write.
The transport and spawn-cell selection remain documented in [park.md](park.md#arrivals-who-comes-on-what-and-how-often).

The constructor first zeroes its guest fields, then overwrites the values below. Its final
state selection is separate from those assignments: `FUN_004fa990` tests the cell under the
guest. Types 0, 1, 3, 9 and 10 pass; this branch records the current arrival vehicle in
`+0x1d8` and enters state 6. Otherwise two more random draws select and vary a destination;
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

## Rides and the held unhappy queue arm

A ride can raise happiness. Freshly traced: `FUN_004fd970` gates the effects on byte
`+0x1f1`; the nonzero branch calls `FUN_004fe1e0`, which calls `FUN_004fdcc0` at
`0x004fe259` before applying the item's effects. The excitement match uses the guest
kind's preference, adds the appropriate balance-key value for gaps below 5, 15 or 40,
and clamps the changed happiness to 0..100. Zero excitement returns; a gap of 40 or more
adds nothing. The zero-byte settlement branch instead docks happiness. Full formulas and
other effects have one home: [ride-operation.md](ride-operation.md#the-excitement-match--fun_004fdcc0).

In today's source, `ParkRideOperation.SettleUp` calls `MatchTheExcitement` on its successful branch, and the
latter changes happiness. Therefore Q85 does **not** require rebuilding the excitement
match, and the older nine-minute observation cannot establish that it currently never
raises an arrival's happiness. The remaining constructor defect is explicit in
`ParkPeople.Admit`: zero happiness/needs, fixed cash/exit level and zero prankery.
Its separate `System.Random` source and kind-before-speed ordering also differ from the
original. These are source observations, not a runtime measurement.

`FUN_004ffff0` reaches the mood branch only after its earlier queue guards and unsigned
`mGameTick - [+0x208] > 30` (`0x00500308`). Happiness is truncated by `__ftol`, then its
low byte is tested: **below 10** posts thought `0xb` (`0x00500338`..`0x0050033f`) and jumps
to the common leave path (`0x00500382` → `0x0050049e`). At 10..19 it starts animation 4.
The other queue arms and common leave path remain in
[ride-operation.md](ride-operation.md#the-inqueue-turn---fun_004ffff0).

Q85b must set the decoded arrival values first, then replace `QUEUE_TURN_UNHAPPY` with
this arm and turn around
`ParkQueueTurnTests.AnUnhappyQueuerStaysUntilArrivalsHaveTheOriginalsHappiness`.
Use the real arrival constructor in regression coverage, exercise both sides of 10 and
the 30-tick guard, and prove the tests fail when the zero initialization and held queue
arm are restored. Confirm `load 30` with `peeps` over a few minutes and a screenshot from
the same run; distinguish the initial value from later ride gains and departures.
