# Park advisor (original executable)

The park advisor is a THING like the weather, ticking on the same beat. It hears in two ways: the park **posts a message** to it, and **each tick it polls one row** of the metadata table below, calling that row's score function (`FUN_0059a550` calls `FUN_0059c680( 1 )` on the static at `0x00fb3540`, built by `FUN_0059bea0`, whose `+0x20` is `MinScoreForConsideration` (`DAT_00fb3560`); it steps its cursor at `+0` over the 351 rows to the next whose `+0x00` is nought, 155 of them (row 0's is 1 in the image, `0x0076e300`), calls that row's score function and keeps a score above nought that the accept gate `FUN_0059abc0` would take; the tick raises it when `FUN_0059bf20` answers above `MinScoreForConsideration`, 25). Either way the message is accepted or rejected by a gate, and an accepted message occupies one of **eight priority slots**. Each tick the advisor considers the highest-scoring slot, speaking only if its score passes the threshold (Q90 below), then picks a line within that response and plays a sample. Two separate tables sit behind this — a static response table at `0x00768fb8` that maps a response id to a sample, lip file and gesture, and a runtime-filled metadata table at `0x0076e300` that holds the per-message score function, category and line count. The scores' coefficients live in `data/Advisor/Advisor.sam`, whose schema is compiled into the executable as 60-byte descriptors. This page is about the park advisor's message and scoring system; the on-screen advisor model and his interruption are in `ui.md`.

## The thing

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_0050b360` | — | THING dispatcher; advisor is type `0x0b`, weather is `0x0f` | Same dispatcher, same construction pattern as the weather thing (`weather.md`, "Weather is a thing (model 15)") |
| `DAT_00fb3b7c` | game type | Global the advisor's case is gated on (`!= 1`) | Read by `Game_StateMachine`, `IslandPanel_Refresh`, `GameMenu_BuildPark` |
| `FUN_0059b060` | `CAdvisor::ReceiveMessage` | Maps an incoming park message to a response and raises a topic | The error string inside names the method |
| `FUN_0059b590` | — | Builds the 0x18-byte message/topic record | Writes its argument to byte offset `0x0c` |
| `FUN_0059a940` | — | Raises a record into one of the eight slots | Walks `advisor + 0x28` at stride `0x18`, 8 iterations |
| `FUN_0059a550` | — | The tick: picks the highest-scoring slot and says it | Compiled member names `mLastActionStarted`, `mMessagePlaying` |
| `FUN_0059abc0` | — | The accept gate; can drop a raise outright | Called by `FUN_0059a940` before a slot is taken |
| `FUN_0059aa70` | — | Withdraw: clears every slot with a matching id | Calls `Advisor_StopSpeaking()` and bumps a counter if that id is speaking now |
| `FUN_0059c500` | — | Routes some responses to a separate sticky slot at `+0x16d0` | |
| `FUN_005194d0` | — | Fetches the advisor THING; indexes a THING table at `DAT_007cfb90`, stride 5 dwords | Asserts "Thing has been allocated in Thing..."; the decompiler hides the call because the result is passed in ECX by `__fastcall` |

Concrete message ids seen in the dispatch (the values are **advisor message ids**, the metadata rows' ids, stored at the record's `+0xc`; they are not response ids): message `8` → advisor message `0x102`, row 258 `RideCondemned` (responses 498-500); message `0x0e` sub 0/1/2 → advisor messages `0x7d`/`0x7e`/`0x7f`, rows 125-127 (responses 302, 304, 306; `0x0059b393`, `0x0059b315`, `0x0059b297`); message `0x11` (research) categories 0-4 → advisor messages `0x5d`, `0x5f`, `0x60`, `0x61`, `0x5e`, rows 93-97 (`Research*Researched`). Fork watch 2026-10-16, `verify-advisor.md`, "Our committed docs found wrong" 3.

## Q90 — park opening and closing (decode only, 2026-10-03)

**The shipped settings suppress both announcements.** The door posts advisor messages, but their score is
**20** and the tick requires **strictly more than 25**. Silence alone therefore does not prove a missing
feature. Q90b implements this message/gate path while preserving the decoded stock silence (below).
No game was launched for Q90's decode and no speech was
confirmed on screen. The following runtime expectations are predictions from executable instructions and data.

### Door event, message, response and sample are different numbers

`FUN_00519ef0( open, 0 )` posts a type-`0x13` record through `FUN_0040f630` (vtable `0x006fd810`,
type accessor `0x0040f650` returns `0x13`): event **3** for open
(`0x0051a031`), **4** for close (`0x0051a1c1`), outside each state-change guard. `FUN_0059b060` case `0x13`
passes that event to `FUN_0059ae20`. Cases 3/4 construct messages **`0x80`/`0x81`**, with line index **−1**,
forced **0**, score override **−1**, then call `FUN_0059a940` (`0x0059af04`..`0x0059af53`).
`FUN_0059b590` derives the score from the message's metadata, marks it occupied, and leaves score-overridden
false. Thus repeating the same door state still posts; it does not force speech or reset its history.

Fresh reconstruction of all **5,280** initializer instructions at `0x005a0a50`..`0x005a86fb` gives:

| Event | Advisor message / metadata address | Score function | First response | Lines / selection |
|---|---|---|---|---|
| 3, open | `0x80` / `0x0076ff00` | `0x0059f390`: `[ECX + 0x3e0]` | 308 (`0x134`) | 2 / mode 2 |
| 4, close | `0x81` / `0x0076ff38` | `0x005b96a0`: `[ECX + 0x3e4]` | 310 (`0x136`) | 2 / mode 2 |

Both rows are posted-only (`+0 = 1`), category **0**, valid for either game mode (`+0xc = 2`), with at most
**one pending instance of each message** (`+0x20 = 1`). Neither is the category-1 sticky help message.
The first-response writers are `0x005a385d` and `0x005a3939`; the pointer writers are `0x005a3710` and
`0x005a3883`. Mode 2 advances the message's own history and wraps at its two-line count in `FUN_0059a550`.
Only a successful playback-helper return advances that history; the open and close messages have separate histories.

| Response / table address | Sample and lip number | Existing transcript's meaning |
|---|---|---|
| 308 / `0x0076b638` | **342** | The park is now open; the transcript's first word is uncertain |
| 309 / `0x0076b658` | **343** | Attention please: the park is open for business |
| 310 / `0x0076b678` | **344** | The park is closed; nearly time to turn out the lights and lock up |
| 311 / `0x0076b698` | **345** | Closed for business; time to end the day |

These are meanings from the existing `global-speech-transcripts.tsv`, not freshly heard quotations (its
342 and 345 rows contain apparent transcription errors). All four response records have gesture **−1**, model/bank
word **0** (global speech bank), props **9/0**, the ticket hat. Their trailing values **150/151** are not the message
IDs or `.sam` ordinals. The advice to open a closed park (samples 1–3) is a different message.

### Why the stock door is silent, and when these lines can speak

The score functions above run with `ECX = 0x00fb3540`. The compiled schema, not the text file's order,
resolves the three relevant keys:

| Key | Descriptor / runtime address | Shipped value |
|---|---|---|
| `GeneralAdvisor.MinScoreForConsideration` | `0x0072e1d4` / `0x00fb3560` | **25** |
| `ParkNowOpen.Score` | `0x00734e94` / `0x00fb3920` | **20** |
| `ParkNowClosed.Score` | `0x00734f48` / `0x00fb3924` | **20** |

`FUN_0059bea0` builds the balance sub-object at receiver **+0xc**, then reads `data/advisor/advisor.sam`.
Its vtable `0x006fd958` returns the schema through `0x00415e90` and the storage base (`this + 8`) through
`0x005b0d60`. `FUN_00401030` starts at slot 1. The ten-entry `MessageGroups` array consumes **30 value
slots plus one extra dword** (`0x0040115b`, `0x0040119b`); omitting that extra dword misidentifies both
score fields. The schema walk reproduces the direct readers' `+0x3e0`/`+0x3e4` exactly.

The accept gate `FUN_0059abc0` can admit score-20 messages: **it does not test the score threshold**.
They occupy ordinary slots. In the tick, `0x0059a6d5` compares the best occupied slot's stored score with
`[0x00fb3560]`; `JLE 0x0059a8e8` skips speaking for **20 and also 25**. That tail only reads elapsed time
through `FUN_0041a990` and returns. **This tick retains the low-score slot; it does not discard it.**
A second copy of that message then fails the instance cap. A higher-priority message can replace it when
all eight slots are full; explicit withdrawal `FUN_0059aa70` can clear it. No opposite-message withdrawal
is present in the two door-event arms.

If a message was posted with a score **above 25**, the tick can select it after any current action's wait
and the one-shot suppression flag. The non-overridden playback helper `FUN_0059b620` then clears occupancy
and re-evaluates its score: its separate check at `0x0059b6c3` / `JL 0x0059b6c9` requires **at least 25**.
That weaker second check does not rescue a newly posted score-25 message from the tick's first check.
A successful helper return sets the wait to the response's duration plus **1,000 ms** and records the history.
That return is not proof of audible output: the helper does not test `Advisor_SayResponse`'s return value.
Higher-scoring pending messages take precedence; pressing the door does not interrupt them immediately.

Category 0's shipped `MinTimeSameMessage` is **120**, `SayOnlyOnce` **0**, `DiscardAfterSlaps` **0**.
After a successful helper return, and only when `lastSaidTick >> 2` is nonzero, the accept gate compares the same message's elapsed
`(mGameTick >> 2) - (lastSaidTick >> 2)` with 120; this is **480 world sweeps** when both stamps are aligned
(about **119 seconds** at eight 31-ms ticks per sweep), not a wall-clock timer. Opening and closing do not
share this cooldown. The general five-second and 120-second keys are not read, as Q194 established in "The content" below.

### Evidence and next-session confirmation

A private copy of the Ghidra project, hash-checked against the original and not altered;
program `/testme.exe`, image base `0x00400000`, executable SHA-256
`cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`, matching the reference executable.
The `q90/` harness directory holds `initial-decode.txt`, `filler.txt`, `loader-schema-door.txt`,
`allocation-and-clear.txt`, `tick-constructor-withdraw.txt`, `tail-xrefs.txt`, and the reproducible
`check_tables.py` / `table-check.json`. Both metadata rows also match the earlier Q177 extraction.
No new shipped-file layout was discovered; these are executable mappings of already documented data.

**Q90b must predict zero spoken door announcements with the stock scores**, then confirm the real door
with a screenshot and message/score/threshold log. To exercise the audible branch, use a clearly labelled,
private configuration fixture raising only both door scores to **26** before posting, leaving the threshold at 25.
Read or explicitly reset the two histories before predicting the response/sample sequence: event 10 in
`FUN_0059ae20` sets every rotation index to −1 and every record's said and slaps to nought, keeping its tick, so each
message then selects line 0, then 1, then 0. It is sent once, on a player's first park (`0x0054f24e`; Q454).
A saved history may begin elsewhere; Q90b traces the fresh constructor below. Confirm both the actual door action and
advisor on screen with its matching log. Cover alternating lines, same-state posting, duplicate suppression,
the strict 25 boundary, per-message cooldown and queue priority in regression tests; restore the missing
route and threshold defects separately and require the new tests to fail. Never hard-code an unconditional
`Say(342)`/`Say(344)` or change the shipped scores to satisfy the old confirmation wording.

## The message record (0x18 bytes)

Confirmed two ways: `FUN_0059a940` reads the response/subject as `param_2[3]` (byte `0x0c`), and `FUN_0059b620` reads the same byte as `param_1[6]` in *short* units.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `+0x00` | — | u16 lead field, written by `FUN_0059c0a0`. **Not** the id the gate uses | The gate's slot scan compares `+0x0c`, not this |
| `+0x04` | — | int lead field | |
| `+0x08` | — | int line index; `-1` means "choose one" | |
| `+0x0c` | — | dword subject / response id — **this** is the id the gate keys its per-message counters on | `FUN_0059a940` passes `param_2[3]`; `FUN_0059b590` writes its argument here |
| `+0x10` | — | int score / priority; defaults to `FUN_0059bf20(subject)` when the caller passes `< 0` | |
| `+0x14` | — | byte, occupied (`= 1`) | |
| `+0x15` | — | byte, "forced" — skips part of the accept gate | |
| `+0x16` | — | byte, `1` if `+0x10` was given, else derived from the subject | Really "did the caller override the priority" |

**Eight slots, ranked.** A raise fills the first free slot; when all eight are taken it finds the lowest-priority occupant and overwrites it **only if the newcomer's priority (`message[4]`) is higher**. Slots live at `advisor + 0x14 + slot * 0x18`; the scan walks `advisor + 0x28` at stride `0x18` and copies 6 dwords (24 bytes). The tick then says the **highest**.

**The tick** (`FUN_0059a550`) returns early while `now < lastSpoke + wait`, consumes a one-shot suppress byte at `+0x16e8`, scans the eight and says the best. On success **wait = the line's length + 1000 ms**. The compiled member names confirm the fields: `mLastActionStarted` = `+0xd8`, `mLastActionDuration` = `+0xdc`, plus `mMessagePlaying`, `mHistory[i]`, `mSoundId`.

## Line selection

`FUN_0059c490` gives the per-response selection mode:

| Mode | What it is |
|---|---|
| `0` | Cycle **and stop at the last line** — the accept gate refuses the response once the stored index reaches count-1 |
| `1` | Advance and clamp |
| `2` | Advance |
| `3` | Random, `FUN_00516330() % count` |

The response is `FUN_0059c240( message ) + index` (`0x0059b798`..`0x0059b79f`), so a message's lines are a **contiguous run of response ids**; `Advisor_SayResponse` finds the response by the table's `+0x00` and plays its `+0x04`, the sample (`0x005990a7`..`0x005990cb`). Response `0x266` (614) means "say nothing" (`0x0059b7a1`).

## The accept gate

`FUN_0059a940` calls `FUN_0059abc0( advisor, id, forced )` before taking a slot. **The gate does not consult world state.** It rejects for exactly five reasons, none of them about staff, visitors, rides, research or money:

| # | Reason | Where |
|---|---|---|
| 1 | The options screen's Tutorial switch off (`DAT_0078d90d` nought, written from control `0x1d4c2` at `0x004a3704`) refuses a message whose row's category is 1, the tutorial messages | `FUN_0059c610` |
| 2 | A per-message cooldown: elapsed vs `MessageGroups[category].MinTimeSameMessage` from `Advisor.sam`, tested only when `FUN_0041a980` is non-zero | `FUN_0059c390` / `FUN_0041a980` / `FUN_0041a990` |
| 3 | When NOT forced: the category's `SayOnlyOnce` is set and the message's said byte `+0xe8` is; then the category's `DiscardAfterSlaps` is not nought and is at or under the message's `+0xec`, how often a withdraw has cut him off saying it (category 10, a row not found, is never refused for it) | `FUN_0059c410`, `FUN_0059c570` |
| 4 | A cap on how many copies of one id may sit in the eight slots | `FUN_0059c320` |
| 5 | A rotation check against counter `+0xe4`, and mode 0 running out of lines | `FUN_0059c490` / `FUN_0059c2b0` |

Every test reads a per-message property, its category's `MessageGroups` settings in `Advisor.sam`, the advisor's own counters and slots, or (test 1) a flag the options screen writes. **So a screen-open line needs only the advisor, its tables and a HUD — nothing from the simulation.**

### Test 2 is a per-message cooldown, not a level check

The two helpers are tiny, and both are `__fastcall` with the receiver hidden by the decompiler (the same mangling that hides `FUN_005194d0`):

    FUN_0041a980( p )  =  *p >> 2
    FUN_0041a990( p )  =  (*(DAT_0080239c + 0x1da70c) >> 2) - (*p >> 2)

The disassembly settles what `p` is:

    LEA EAX,[EDI + 0xe]     ; EDI = message id
    SHL EAX,0x4             ; (id + 14) * 16
    LEA ESI,[EAX + EBP*1]   ; EBP = advisor  ->  ESI = advisor + id*0x10 + 0xe0
    MOV ECX,ESI
    CALL 0x0041a980         ; non-zero if rec[0] >> 2 is set
    MOV ECX,0xfb3540
    CALL 0x0059c390         ; EBX = this message's threshold
    MOV ECX,ESI
    CALL 0x0041a990         ; EAX = (global[+0x1da70c] >> 2) - (rec[0] >> 2)
    CMP EAX,EBX
    JNC accept              ; elapsed >= threshold accepts; less rejects

`+0xe0` holds the time he last said this message, so `FUN_0041a980` non-zero means "he has said it before" and the cooldown only applies after a first airing. The `>> 2` on both sides counts in fours of thing sweeps, about a second each: the running counter `DAT_0080239c + 0x1da70c` is `mGameTick`, one count a sweep, and the tick stores it at `+0xe0` when he says the message (`FUN_0041a960`, `0x0059a847`; `park.md`, "Arrivals: who comes, on what, and how often"). The counter is in the same large global structure as `FUN_005194d0`'s `+0x1da71c`. `DAT_0080239c` is zero in the image and filled at runtime.

### The per-message record: `advisor + id * 0x10`

The disassembly computes `advisor + id*0x10 + 0xe0` outright (`LEA EAX,[EDI + 0xe]; SHL EAX,0x4; LEA ESI,[EAX + EBP*1]`), so the record starts at `+0xe0`.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `+0xe0` | — | When he last said it: `mGameTick` then, stored by `FUN_0041a960` (`0x0059a847`) | Receiver of `FUN_0041a980` / `FUN_0041a990` |
| `+0xe4` | — | Rotation counter (gate test 5) | `FUN_0059c2b0` |
| `+0xe8` | — | Said-once flag (gate test 3) | `FUN_0059c410` |
| `+0xec` | — | Times a withdraw has cut him off saying it (gate test 3, against `DiscardAfterSlaps`) | `FUN_0059c570`; bumped by `FUN_0059aa70` (`0x0059ab34`) |

## The two tables — do not confuse them

### `AdvisorResponseTable` at `0x00768fb8` — static and walkable

610 rows, stride `0x20`, terminated by **9999** (walked to its terminator). Ghidra already names the symbol. `Advisor_SayResponse` walks it linearly and matches the row whose first dword is the response id — the table is **not** sorted by id.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00768fb8` | `AdvisorResponseTable` | 610 rows, stride `0x20`, `9999` terminator | Walked to the terminator; Ghidra symbol |
| `+0x00` | — | Response id | Matched linearly by `Advisor_SayResponse` |
| `+0x04` | — | **Sample id** — what `Sound_PlayEffect` is handed | |
| `+0x08` | — | Lip file number → `\Speech\lips\sp_%03d.lip` | Format string in the exe |
| `+0x0c` | — | Gesture: `-1` builds a random animation sequence (`FUN_00598b20`); any other value is a row of the gesture table `0x0076dc18` at value `- 0x10` (`FUN_00598bf0`) | `0x005990e3`, `0x00599383` |
| `+0x10` | — | Model slot in the low half, bank in the high half (`slot \| bank << 16`). Slot 1 is the park's own advisor model (137 rows). Bank 0 plays through the global speech category (`DAT_00803a34`), any other value through the park's (`DAT_00803a40`); five rows carry bank 1 (ids 1 and 399-402) | `0x005990dd`, `0x00599317` |
| `+0x14` | — | First prop: a costume id of his model, put on for the line ("What he wears for a line", below) | `0x005990fa`, `0x00598e18` |
| `+0x18` | — | Second prop, nought for none | `0x00599106`, `0x00598e2d` |
| `+0x1c` | — | Grouping values — **see the refuted section below**; no instruction reads it through this table's base | xrefs |

### Response metadata at `0x0076e300` — filled before `WinMain`

Stride `0x38`, validated by `row+0x04 == id`, with a linear-scan fallback bounded at `0x772fcc` (351 rows). Ids `0x15f` and `0x160` are excluded outright. **The table reads as zeros in the image, except row 0: `+0x00` = 1 and `+0x0c` = 2 at `0x0076e300` and `0x0076e30c`, which the filler never writes (fork watch 2026-10-16, `verify-advisor.md` lead A). Its filler is straight-line code at `0x005a0a50`** (`MOV` and `XOR` between four `PUSH`es and `POP`s, ending in the `RET` at `0x005a86fb`; in no Ghidra function), a C++ static initializer the C runtime runs before `WinMain` through the thunk `0x005a0a40` listed at `.data` `0x0072bda8` (Q177's analyser decode; the complete emulated table is `~/.cache/tpw-harnesses/q177/skeptic-analyser/meta_full.json`, made by `emu2.py` there). Row 91, for one: polled, id `0x5b`, category 0, mode 2, score function `0x0059e730`, one instance, first response 244, two lines, selection mode 2.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0076e300` | — | Metadata table, stride `0x38`, filled by the static initializer at `0x005a0a50` | Zeros in the image but row 0's `+0x00` = 1 and `+0x0c` = 2 |
| `+0x00` | — | Nought: the row is polled, one of 155, a turn every 155 ticks; otherwise posted only | `FUN_0059c680` |
| `0x772fcc` | — | Upper bound of the linear-scan fallback (351 rows) | |
| `+0x04` | — | Id (used to validate a row) | |
| `+0x08` | — | Category | |
| `+0x0c` | — | Game-mode filter: `2` always, `0` game type 0, `1` game type 2 | |
| `+0x10` | — | **Function pointer** that computes the score | Called via `FUN_0059c0a0` and `FUN_0059bf20`, subject to the `+0x0c` filter |
| `+0x20` | — | Max instances | |
| `+0x24` | — | First **response** id: `FUN_0059c240` returns it, the line's index is added, and `Advisor_SayResponse` matches the sum against the response table's `+0x00`, whose `+0x04` is the sample (response 244 is sample 242) | `0x0059c29f`, `0x0059b79f` |
| `+0x28` | — | Line count | |
| `+0x2c` | — | Selection mode (see line selection above) | |

**The score is code; the coefficients are data.** Because `+0x10` is a called function pointer, the triggers are engine behaviour and cannot be driven purely from a file.

## The content: `data/Advisor/Advisor.sam`

19,863 bytes, ASCII, **382 keyed lines, 205 sections**, commented by the original developers ("was 30 - annoying"). It needs no new format work — `.sam` is already read everywhere, and subscripted keys were proven by the weather work.

    GeneralAdvisor.MinTimeAnyMessage         5
    GeneralAdvisor.MinTimeSameMessage        120
    GeneralAdvisor.MinScoreForConsideration  25     <- this IS DAT_00fb3560
    MessageGroups[0..9].MinTimeSameMessage / SayOnlyOnce / DiscardAfterSlaps

`MinScoreForConsideration` being a file key is why `DAT_00fb3560` has three readers and no writer. **The other two
`GeneralAdvisor` keys are loaded and never read** (Q194). The balance object is the sub-object at `0x00fb354c` (vtable
`0x006fd958`; built by `FUN_00401030`; `0x00415e70` is its destructor thunk), its member calls made with `ECX = 0x00fb3540`, and `FUN_00401030`'s rule puts
`MinTimeAnyMessage` at `0x00fb3558` and `MinTimeSameMessage` at `0x00fb355c`. No operand of the program falls in
`0x00fb3554`..`0x00fb355f` and no byte search finds either address, where the same search finds `0x00fb3560` at exactly
its three reads; none of the 15 member functions nor any of the 201 score functions (thiscall on `0x00fb3540`, every
row's adjustment `+0x14` 0; 433 this-relative accesses) touches `+0x18` or `+0x1c`. So the only pacing is the tick's
wait ("The tick", above) and each category's own `MinTimeSameMessage` in the gate; **there is no
global gap between lines.** `MessageGroups` is set for groups 0-5 and 9 only; 6-8 keep the loader's zeros. "Slaps" is the player slapping him. Field names across all messages are dominated by `Score` (160 uses) and `ScorePerPoint`, then per-message conditions: `ValueBetterThan`, `SatisfactionWorseThan`, `FilthierThan`, `ScorePerUnpatrolledPct`, `DontBotherIfPatrolledUp`, `MonthsIntoGameForConsideration`.

The golden-ticket keys are score coefficients, not lines: `GoldTicketNearToFirstOne`, `NearToXPeeps`,
`NearToXHappiness` and `NearToXProfit` (multipliers 0.65, 0.9, 0.85, 0.9) with `GeneralWonGoldenTicket`,
`GoldTicketExplanation`, `WonFirstGoldenTicket` and `GoldTicketCluesForRemainder`; no other shipped `.sam` has them.
Which rows read them, and when the original says them, is `ride-operation.md`, "Golden tickets" (Q141).

### The schema is compiled into the exe

60-byte descriptors — kind at `+0`, name at `+4` in a 32-byte field, bounds at `+0x24`/`+0x28`, array count at `+0x34` (`MessageGroups` reads 10) — the same pattern the weather schema uses.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0072e120` | — | Table of 60-byte `.sam` schema descriptors, returned by the advisor balance object's slot 0 (`0x00415e90`); 205 sections, ordinals 0-204, the first, `GeneralAdvisor`, closing at `0x0072e210` | Walked: `GeneralAdvisor` is 0, `WaitingTimes` is 204 |
| `0x0073964c` | — | The `WaitingTimes` descriptor, the last section; the next record, kind `0xc`, ends the table | End of the walk |
| kind `0` | — | Section start, unnamed (204) | `park-engine.md`, "How a key finds its global", has every kind |
| kind `1` | — | Section end, carrying the section's name (204) | Same |
| kind `2` | — | Array start, unnamed (1, at `0x0072e24c`); `FUN_00401030` scans on to its kind `3` and logs "Missing TABLE_END" without one | Q194, walked |
| kind `3` | — | Array end, carrying the name (1: `MessageGroups`, `0x0072e33c`) | Same |
| kind `4` | — | Int (290): every `Score` and `ScorePerPoint`, and most conditions | `park-engine.md`, "How a key finds its global" |
| kind `5` | — | Int ≥ 0 (15): `MinTimeSameMessage`, `MinScoreForConsideration` and `DiscardAfterSlaps` among them | Same |
| kind `6` | — | Bounded int, bounds at `+0x24`/`+0x28` (50): `ThirstierThan`, `WorseThan` and `SayOnlyOnce` among them | Same |
| kind `7` | — | Float (9): `ShopsPerRideRatio`, `SideshowsPerRideRatio`, `AverageQueueLongerThan`, `Margin`, `WagesTimesHigherThanIncome`, `PeopleMultiplier` (twice), `HappinessMultiplier`, `ProfitMultiplier`; the file sets all nine (0.15, 0.2, 2.5, 1.2, 2, 0.65, 0.9, 0.85, 0.9) | Q194, walked: 774 records to the terminator |

205 section descriptors match the file's 205 distinct prefixes exactly — two independent sources agreeing.

**The file's order is NOT the exe's order.** `ClosePark` is ordinal 4 in the exe but sits at line 575 of the file, far from its neighbours at lines 47-57. Nothing is missing from either side. **Ordinals must come from the exe, never from file order.**

## Verified by transcript

Speech transcripts already exist — grep the transcripts TSV named in `CLAUDE.local.md` before transcribing anything. Each row below is also corroborated by the message's own `.sam` field names.

| Group | Samples | Transcript | Section |
|---|---|---|---|
| 3 | 1-3 | "your park is closed... open up" | `OpenPark` |
| 4 | 4-6 | "not much to do... close it" | `ClosePark` |
| 5 | 7-9 | "thirsty visitors... drink shops" | `VisitorsThirsty` |
| 6 | 10-12 | "getting hungry... food shops" | `VisitorsHungry` |
| 7 | 13-15 | "rides wearing out, no mechanics" | `StaffHireMechanics1` |
| 8 | 16-18 | "rides breaking down" | `StaffHireMechanics2` |
| 9 | 20-22 | "not enough mechanics" | `StaffHireMechanics3` |
| 10 | 23-25 | "getting messy, no janitors" | `StaffHireHandymen1` |

Lines come in **threes** (three takes). Sample 19 is skipped — consistent with roughly 80 silent stub entries.

## REFUTED — do not reuse these

### `+0x1c` is NOT the descriptor ordinal

The response table's `+0x1c` matches the section ordinal early and then **drifts in accumulating steps**. The section list was walked out of the exe (205 sections at `0x0072e210`), the response table was grouped by `+0x1c`, and the two were joined against the transcripts:

- The match holds far further than the first handful — all the way to **message group 24**, which really is `StaffGuardBuildCamera` ("if you build some security cameras your guards would be more efficient"). Two dozen exact matches.
- **The offset is not constant and it is not +5.** It accumulates: **+1** by group 31 (`StaffTrainHandymen`, section 30), **+3** by group 64, **+4** by 68, and only at group **70** does it reach **+5** (`RidesBreakdownNoMechanics`, section 65). It stays +5 through group 82 — sampling it in that band mistakes a waypoint for the rule.
- Groups **20-23 and 25-29 have no responses at all**, which is the shape of the gaps.

**There is no arithmetic that recovers `+0x1c`. Do not derive a sample from a section name by counting — in either direction.** This has been got wrong twice, in both directions. Take the sample from the response table and confirm the line by its transcript.

### A screen's line is a message id, resolved through the metadata table

`FUN_00486b00( id )` makes an advisor message with this id and posts it; `FUN_00486b40( id )` withdraws it. Both first call `FUN_005194d0` to fetch the advisor THING.

**That id is a *message* id, not a response id.** `FUN_00486b00` does `MOV EAX,[ESP+0x30]; LEA ECX,[ESP+0x14]; PUSH EAX; CALL 0x0059b590`, and `FUN_0059b590` writes that argument to `*(undefined4 *)(param_1 + 6)` on an `undefined2 *` — byte offset `0x0c`, the subject, the very field the accept gate keys its per-message counters on. It is **not** the response id that indexes `0x00768fb8`.

Checked against the data: the map screen (`FUN_005f0b40`) pushes `0x130` = 304, and response 304's sample is 338, "there's a litterbug running amok" — plainly not a map line. Message ids resolve through the metadata table at `0x0076e300`, filled by the static initializer at `0x005a0a50`: the row whose id is the message gives the first response at `+0x24`, the line's index is added, and the response table's `+0x04` is the sample. The map's `0x130` is response 564, sample 582 ("the map shows an overview of the park"); buy's `0xbc` is 407, sample 380; hire's `0xbd` is 408, sample 381; the pylon button's `0x125` is 553, sample 565. The 40 call sites' constants sit in `0xbc`-`0xd5` / `0x11c`-`0x135`, every one a posted-only row (`+0x00` 1) of one line (`+0x28` 1) (Q177's analyser skeptic, `~/.cache/tpw-harnesses/q177/skeptic-analyser/meta_full.json`; each sample checked against its transcript).

### The message-to-sample rule

Message → its metadata row's `+0x24` (the first response) + the line's index → the response table's `+0x04` (the sample); see both sections above.

**The golden-ticket messages** (`ride-operation.md`, "Golden tickets", posts them; Q194, from `meta_full.json` and the
transcripts): every row posted-only, model slot 1, bank 0. `0xd7`-`0xd9` (Local) have 6 lines each from responses 434,
446, 458; `0xdc`-`0xde` (Global) 4 from 440, 452, 464; `0xe1`-`0xe3` (Secret) 2 from 444, 456, 468; `0xea`-`0xed` one
each, 474-477. The lines rotate three groups of samples: "ticket only" rows samples 293-295 ("and the winner is... you've won a
golden ticket"), "ticket and key" sample 446 ("...and now you've got another golden key"), "ticket, key and park" samples 604 and 622
("...now you can open a new park"). The "moved" rows are samples 533-536: the rollercoaster, go-kart, water-ride and park-size
awards moved here. Said in a Full Simulation park since Q595 (`ride-operation.md`, "The advisor's ticket lines"), each line as its post
asks; the lip file of every "ticket only" line is 293's (the response's `+0x08`), which `ParkAdvisorMessages.LipOf` reads.

## Every HUD screen posts a message

The ids seen so far: `0xbc` when buy opens, `0xbd` when hire opens, `0xc6` and `0x11f` in the staff/visitor locator, and `0x131`, which the camcorder button posts as it goes down and withdraws as it comes up or a button on its panel is clicked. So the advisor comments on the screen you just opened. The locators' two (response 417, sample 389; response 547, sample 564) are posted as each opens and withdrawn as the arm folds either away; built (Q425, `hud.md`, "The staff and visitor locators"). The camcorder's (response 565, sample 583) is built (Q428, `hud.md`, "The camcorder button's line").

## Still unknown

| What | State |
|---|---|
| The loader that fills `0x0076e300` | **Found** and emulated: the static initializer at `0x005a0a50` (above); the 40 screen-posted ids are posted-only rows of one line each |
| The u16 at message `+0x00` | Unknown — it is written by `FUN_0059c0a0` but is not the gating id |
| `Welcome` (section 2) | Row 0 of the metadata table is posted only in the running game (the emulated table has it polled), its score function (`0x0059c8c0`, `MOV EAX,[ECX + 0xa0]`) is `Welcome.Score`, and the shipped park's file holds message 0 as said on tick 1; its poster is not read. A park made with no file in the original holds it in a slot at 100,000 on tick 0 and says it on tick 1 for 31.3 s ("A fresh Full Simulation park", Q327; Q543). Its line looks like a **stub**: group 2 holds four rows, all `sample=1, anim=16`, and sample 1 is an *OpenPark* line. The park welcome probably has no audio of its own (read from the response table alone; no code that posts `Welcome` was traced) |
| Responses 399-402 | All carry sample 1, an `OpenPark` line — the same stub shape |

## Q90b — the posted door path (2026-10-03)

`ParkState.SetParkClosed` posts on every request, including an unchanged state. `ParkAdvisorMessages`
reads the two scores, strict consideration threshold and category-0 cooldown/say-once setting from
`Advisor/Advisor.sam`. Its eight slots retain low scores, cap each door message at one pending copy,
replace only a strictly lower minimum when full, and select the first highest slot. Opening does not
withdraw a pending close, or vice versa. Each message rotates its own two responses only after the
helper succeeds; a busy/disabled speaker returning zero still consumes the message and records history.
The existing speaker owns the model, voice, gestures and pause. No arbitrary sample is queued at the door.

Fresh Ghidra follow-up in the same private, identity-checked Q90 project resolves two implementation details:

- `FUN_00599e70` clears eight occupied flags and all 351 histories: last-said tick 0, rotation −1,
  said flag 0, slap count 0. The implementation starts the door histories this way.
- `FUN_005989c0` fills the clip-length array through indexed writes starting at `0x00f796c8`, then zeros
  **clip 14 only** (`0x00f796fc`). `0x00f79700` holds **clip 15's duration**, despite being zero in the image.
  `FUN_00598b20` includes it in the gesture-chain sum, and `Advisor_SayResponse` adds it **again** at
  `0x005994ac`, plus 1,000 ms. The scheduler adds another 1,000 ms. The park speaker preserves both additions.
- `FUN_0059c610` tests metadata category 1, so the global class gate does not suppress the category-0 doors.

The per-message cooldown reads the incremented `ParkState.GameTick`, shifted by two. The response wait
reads one `GameClock.Now` instant for all catch-up sweeps in a frame. That clock inherits OpenTPW's
existing clamped-frame timing deviation; it is not the synthetic tick number multiplied by 31 ms.
A park file's records are read at a load ("The records in a park file", below). Other posted park
messages, the analyser and withdrawal/slap paths are outside Q90b; no claim of a complete park advisor.

Evidence is in `~/.cache/tpw-harnesses/q90b/`; fresh executable queries remain in `q90/q90b-*.txt`.
The private stock and score-26 game views use copied saves. Only two digits differ in the latter's
advisor configuration: both door scores 20 → 26. The shipped configuration and original saves remain
unchanged. Each confirmation presses the entry-price door with XTEST pointer input, after predicting
its result; the scene and speaker use the real game paths and `SDL_AUDIODRIVER=dummy`.

Predicted and observed in the running game (`runtime-stock-final/`, `runtime-score26-final/`):

| Fixture / real door press | Message / score / threshold | Response / sample | Result |
|---|---|---|---|
| Stock open, then close | 128 / 20 / 25; 129 / 20 / 25 | none | 0 attempts, 2 pending |
| Score-26 open | 128 / 26 / 25 | 308 / 342 | world tick 99, history 0 |
| Score-26 close | 129 / 26 / 25 | 310 / 344 | world tick 153, history 0 |
| Score-26 open after cooldown | 128 / 26 / 25 | 309 / 343 | world tick 711, history 1 |
| Score-26 close after cooldown | 129 / 26 / 25 | 311 / 345 | world tick 757, history 1 |

The six PNGs show the real door states; all four score-26 frames show the advisor. Each has its
message/score/threshold census and the four have `voicePlaying=True` plus `sp_342/344/343/345.mp2`
speech voices in `run.log`. No sound was sent to the desktop speakers. The stock control waits for the
unrelated screen-help line to finish before pressing the door. `runtime-results.json` independently
checks all four actual response durations against each logged gesture chain plus the shipped ending
clip's 666 ms plus 1,000 ms. Example: 8,166 → 9,832 ms, then the scheduler adds 1,000 ms.

Regression: 20 new cases cover stock data, actual `SetParkClosed` routing and unchanged states,
duplicate retention, strict and configurable thresholds, the helper's inclusive recheck, shifted
cooldown boundaries, independent rotating histories, zero speaker returns, priority/ties/eight-slot
replacement, waits and real catch-up ticking. All 14 restored defects fail (`mutations.json`,
`review-mutations.json`); restored full suite: **1,699 passed, 0 failed, 0 skipped** with game data.
Independent applied-code review found the extra end-clip duration and catch-up-clock errors; both
were corrected and their restored defects fail. `SimulationClockScope` isolates the new tick tests.

Not confirmed on screen: full-slot eviction, the exact cooldown boundary,
rotation back to line 0, and zero playback-return consumption. Those applicable to the new path are
regression-tested; the actual busy/disabled speaker guards are code-reviewed only. The first score-26 capture was partial because
its harness deadline was too short for the cooldown step; the completed final run replaces that proof.

## A ride about to break down (message `0x42`, Q305)

**Who posts it.** A ride's wear, `FUN_004df670`, on the turn its State of repair's byte falls below 10 with the
byte plus the wear not below it (`ride-operation.md`, "A ride's wear"). The call is `0x004df8be`:
`FUN_0059b590( 0x42, [thing + 0], [thing + 0xe], -1, 0, -1 )`, so the record's `+0x00` is the thing's id, its
`+0x04` the thing's item, no line is asked for, it is not forced and the score is the message's own; then
`FUN_005194d0` and `FUN_0059a940`, the same gate and slots as the door's.

**Its row** (`0x0076e300 + 66 × 0x38`, from the emulated table, `meta_full.json`):

| Field | Value |
|---|---|
| `+0x00` | 1: posted only, never polled |
| `+0x08` category, `+0x0c` game mode | 0, 2 (either) |
| `+0x10` score | `0x0059dc60`: `MOV EAX,[ECX + 0x248]`, so `0x00fb3788`, which the schema walk names `RidesBreakdownImminent.Score` (descriptor `0x0073199c`); the file sets **100** |
| `+0x20` copies in the slots | 255, where a door's is 1: every worn ride has a slot of its own |
| `+0x24` first response, `+0x28` lines, `+0x2c` mode | 194, 3, 2 |

Responses 194, 195 and 196 (`0x0076a7f8`, read in the image) are samples and lip files **191, 192, 193**, gesture
-1, model and bank word 0, props 5 and 0, the hard hat. The transcripts: "we've got trouble boss, a ride is about to break
down", "look out, she's gonna blow...", "one of your rides is about to break down, take cover...". Mode 2 takes the
line after the last and starts again past the count (`0x0059a796`, `0x0059a7fa`). The three rows after it in the
`.sam` are the same shape: `RidesBreakdownNoMechanics` 105, `BusyMechanics` 107, `MechanicOnIt` 110 at
`0x00fb378c` to `0x00fb3794`. The last is message `0x46`'s ("A mechanic on his way", below); the first two are
messages `0x44` and `0x45` ("A ride broken down and no mechanic to come", below).

**What follows a line said.** `FUN_0059b620`, after `Advisor_SayResponse`, builds an event of type `0x18`
(`FUN_0040f740`, vtable `0x006fd828`, whose first slot answers `0x18`) from the response's group, the record's
thing id and its `+0x04`, and sends it (`FUN_0040fb10`), for every message, a door's too. Its one hearer is the
tag system: "The tag a line leaves on the aerial", below.

**The original** (`q305/orig/`, `original/advisor.py`, Q265's `bounce.TPWS` left alone, the computer on seven
days so ticks alone are counted): the advisor is thing 3. Message `0x42`'s record read last said 0, line -1, said
0 from the load until tick **4800**, the tick the Belly Bounce's State of repair fell from 12.025 to 9.97 (the
byte 9, and 9 + 2.055 is not under 10); on that poll it read last said **4800**, line **0**, said 1, and the last
action's duration 6665 ms. It stayed so through five more falls to 2.4. His slots held message `0x80` at score 20
the whole run, unsaid, as "Why the stock door is silent" says. In the frame taken 1.5 s on (`a-told.png`) he is up
at the bottom right **wearing a yellow hard hat**. The small arrow beside him in that frame is the game's
pointer, holding the path tool's plank; it is there with no line said (`q306/orig/a-end.png`).

**OpenTPW** (`ParkAdvisorMessages.RideWornOut`, `ParkRideOperation.WearByRunning`; `q305/confirm.py`, the stock
park left alone on the desktop, 4 predictions of 4): the wear line of tick 5504 took the Bounce from 11.89 to
9.83, and the log read `post message=66 subject=13 score=100 threshold=25 tick=5504 result=queued pending=1`, then
on tick 5505 `attempt=1 ... response=194 sample=191 line=0` with `playback=started` and a duration of 5665 ms,
6665 with the scheduler's second, the original's. `advisor` read `saying sp_191.mp2` and
`message=66/score=100/line=0/lastTick=5505`; he is up in `run-fix1/2-told.png`. The five later lines under 10
posted nothing. Main's build, off-screen, posted nothing and counted `RIDE_WORN_OUT_MESSAGE` once on its own line
of the rule (`run-control`).

| | The original | OpenTPW |
|---|---|---|
| The post | from the wear, with the thing and its item | built; the item is not kept, and nothing reads the subject but the log and `advisor` |
| When it is said | the tick of the post | the same: "The advisor's turn in the sweep", below |
| The hard hat | drawn | built: "What he wears for a line", below |
| The event of type `0x18` after a line | sent, and heard by the tag system | built: "The tag a line leaves on the aerial", below |
| Lines 195 and 196, a second ride's own slot, the 480 sweeps before the same message again | the listing's | built and held by the tests alone |

## A mechanic on his way (message `0x46`, Q306)

**Who posts it.** The mechanic's decide, `FUN_004da5b0`, once the search has named a ride and a route to its
entry is found (`ride-operation.md`, "The mechanic's repair"): after the assignment (`FUN_004e01f0`), when the
ride's `+0x64` (called for, `FUN_004e03d0`) is nought and it is not waiting for an upgrade (`FUN_004e03e0`). The
call is `0x004da6bf`: `FUN_0059b590( 0x46, [mechanic + 0x218], [ride + 0xe], -1, 0, -1 )`, the same shape as
message `0x42`'s: the record's `+0x00` is the ride's id, its `+0x04` the ride's item, no line asked for, not
forced, the score the message's own; then `FUN_005194d0` and `FUN_0059a940`.

**Its row** (`0x0076e300 + 70 x 0x38` = `0x0076f250`, `meta_full.json`): posted only, category 0, either game
mode, score `0x0059dc90` (`MOV EAX,[ECX + 0x254]`, so `0x00fb3794`, `RidesBreakdownMechanicOnIt.Score`; the file
sets **110**), 255 copies in the slots, first response 204, 3 lines, mode 2. Rows 67 to 69 beside it are posted
only too, 255 copies each: 67 one line from response 197, 68 three from 198, 69 three from 201 (68 and 69: "A
ride broken down and no mechanic to come", below).

Responses 204, 205 and 206 (`0x0076a938`, read in the image) are samples and lip files **202, 203, 204**, model
and bank word 0, props 5 and 0, group `0x48`. The gesture of 204 and 205 is -1, the random chain; **206's
is 26**, row 10 of the gesture table. The transcripts: "one of your rides is broken down but a mechanic is on the
way to fix it", "don't be alarmed ... a mechanic is on the way", "don't panic ... mechanics are on the way".

**The original** (`q306/orig/run.sh`: Q265's `bounce.TPWS`, `scriptvar.py PID 13 4 1` twenty seconds after the
load, `advisor.py` and `object.py` polled together; ticks alone are counted): `VAR_BREAKSTAT` written on tick
1194, the Bounce's state 1 on 1198 with nobody assigned, and on the poll of tick **1201** its assigned member
read 26 and message `0x46`'s record `(1201, 0, 1, 0)`: last said that tick, line 0, said. The last action's
duration read 7498 ms. Message `0x42`'s record stayed never said. The frame 1.5 s on (`a-told.png`) has him up
in the hard hat, as for `0x42`. The ride was open again on tick 1313.

**OpenTPW** (`ParkAdvisorMessages.MechanicOnHisWay`, `StaffBehaviour.Decide`; `q306/confirm.py`, the stock
park's Belly Bounce broken by `scriptvar 13 VAR_BREAKSTAT 1` 25 s in, 4 predictions of 4): the log read `post
message=70 subject=13 score=110 threshold=25 tick=887 result=queued pending=1`, then `mechanic 26 found ride 13`
on the same tick, and on tick 888 `attempt=1 ... response=204 sample=202 line=0` with `playback=started` and a
duration of 6498 ms, 7498 with the scheduler's second, the original's. `advisor` read `saying sp_202.mp2` and
`message=70/score=110/line=0/lastTick=888`; he is up in `run-fix1/2-told.png` as the mechanic walks to the shut
ride. Main's build, off-screen, posted nothing and counted `MECHANIC_ON_HIS_WAY_MESSAGE` once (`run-control`).

| | The original | OpenTPW |
|---|---|---|
| The post | from the mechanic's decide, with the ride and its item | built; the item is not kept |
| When it is said | the tick of the post | the same: "The advisor's turn in the sweep", below |
| Response 206's own gesture row | played (`FUN_00598bf0`) | the same: row 10, clips 14, 5, 1, 11, 6, 15 (Q596) |
| The hard hat | drawn | built: "What he wears for a line", below |
| Lines 205 and 206, a second ride's own slot, a called ride's silence | the listing's | built and held by the tests alone |

## A ride broken down and no mechanic to come (messages `0x44` and `0x45`, Q307)

**Who posts them.** Not the ride: the mechanics' base, hearing the ride's event. SetState's arm 1
(`FUN_004e0e60`, `ride-operation.md`, "A ride broken down, in both games") builds an event of vtable
`0x006fd7c8`, whose first slot (`0x0040f3a0`) answers **7**, with the ride's id at `+8`, and sends it
(`FUN_0040fb10`) before the state is stored. The send is a call of `FUN_0050b550` on each subscriber of the type
in turn, then and there. The condemned arm's event is type 8 (`0x006fd7d0`), the upgrade arm's 9 (`0x006fd7d8`).
Three things subscribe to 7 (`FUN_0040f9e0`): the mechanics' base (`FUN_004da920`, the constructor of kind 10,
thing 2, the thing whose `+0xc` is the search's cursor), the tag system (`FUN_00509b40`, whose hearer
`FUN_00509bf0` only logs a type it does not know) and the kind-17 relay (`FUN_0050c9b0`, whose hearer hands 7
and 8 to `FUN_00481530`, not read).

**The base's hearer, `FUN_004dadc0`** (`FUN_0050b550`'s case 10):

1. `mFirstMechanic` (world `+0x1da73e`) nought: **message `0x44`** (`0x004dae62`).
2. Otherwise along the mechanics' chain (each one's `+0x210`):
   - a mechanic whose `+0x218` (`mObjectToRepair`) is the ride ends the look: **message `0x46`** when the ride's
     `+0x64` (called for, `FUN_004e03d0`) is nought (`0x004daf78`), nothing when it is set;
   - one whose `+0x218` is nought and whose state class (`FUN_005064f0`) is neither 3 (states 2 and 3, going to
     rest and resting) nor 4 (states 4 and 5, going on strike and on strike) is free, and ends the look with
     nothing said (`0x004daec5`);
   - any other (another job, or none and resting or striking) is passed.
3. The chain's end: **message `0x45`** (`0x004daff0`).

Each post is `FUN_0059b590( message, the event's thing id, [ride + 0xe], -1, 0, -1 )`, then `FUN_005194d0` and
`FUN_0059a940`: the shape of `0x42`'s and `0x46`'s. So a break with a free mechanic says nothing at the break,
and "a mechanic is on his way" follows from his own decide.

**Their rows** (`meta_full.json`; the score functions read in the image):

| | Row 68, `0x44` (`0x0076f1e0`) | Row 69, `0x45` (`0x0076f218`) |
|---|---|---|
| `+0x00`, `+0x08`, `+0x0c` | 1 posted only, category 0, either game mode | the same |
| `+0x10` score | `0x0059dc70`: `MOV EAX,[ECX + 0x24c]`, `RidesBreakdownNoMechanics.Score`, **105** in the file | `0x0059dc80`: `[ECX + 0x250]`, `RidesBreakdownBusyMechanics.Score`, **107** |
| `+0x20` copies | 255 | 255 |
| `+0x24`, `+0x28`, `+0x2c` | first response 198, 3 lines, mode 2 | 201, 3, 2 |
| Responses (`0x0076a878`, `0x0076a8d8`) | 198 to 200 = samples **196, 197, 198**, gesture -1, props 5 and 0, group 70 | 201 to 203 = samples **199, 200, 201**, gesture -1, 5 and 0, group 71 |
| The transcripts | "a ride is broken down and you don't have any mechanics, you should hire some right away" and two more | "... has broken down and the mechanics are all busy, maybe you should hire some more" and two more |

Row 67 (message `0x43`, one line, response 197, sample 193) has a score function that answers nought
(`0x005d60c0`: `XOR EAX,EAX`), and no call of `FUN_0059b590` names `0x43` as a constant: it cannot be said.

**The original** (`q307/orig/run.sh`, `advisor.py`, `object.py`, `thingwords.py` polled together; ticks alone
are counted):

- *No mechanic* (`none`: this game's own file, saved after the stock park's one mechanic was fired; the Bounce
  broken by `scriptvar.py PID 13 4 1` 20 s in): `VAR_BREAKSTAT` written on tick 950, and on the poll of tick
  **951** the Bounce's state read 1 and message `0x44`'s record `(951, 0, 1, 0)`: last said that tick, line 0,
  said. `0x45` and `0x46` stayed never said. He is up in the hard hat in `none-told.png`.
- *Every mechanic busy* (`busy`: the original's own `hotpot-later2.TPWS`, its pot, thing 43, broken 3.5 s in
  and its Bounce six seconds on): the pot's break said nothing; on tick 1827 mechanic 26's state read `0xc`,
  his job 43, and `0x46`'s record `(1827, 0, 1, 0)`. On the poll after the Bounce's break (tick 1853) a slot
  held **`(13, 1100, -1, 0x45, 107)`**: the ride, its item, no line asked for, the message, the score; on tick
  1870 `0x45`'s record read `(1870, 0, 1, 0)` and the action's duration 8331 ms. `0x44` stayed never said. He
  mended the pot, took the Bounce on tick 1907, and `0x46` was not said again.

**OpenTPW** (`ParkPeople.MessageForABreak`, called from `ParkPeople.BreakDown`; `q307/confirm.py`, 5
predictions of 5):

- `none` (the stock park, `fire 26`, `scriptvar 13 VAR_BREAKSTAT 1`): `post message=68 subject=13 score=105
  threshold=25 tick=876 result=queued pending=1` beside the break's own line, then on tick 877 `attempt=1 ...
  response=198 sample=196 line=0`, `playback=started`, duration 9832 ms; `advisor` read `saying sp_196.mp2` and
  `message=68/score=105/line=0/lastTick=877`. He is up in `run-none1/2-told.png`.
- `busy` (a Hot Pot bought and broken in its go, the Bounce broken as mechanic 26 walked to the pot): no post
  at the pot's break; `post message=70 subject=43` on tick 912, the tick he found it; `post message=69
  subject=13 score=107 ... tick=924 result=queued` at the Bounce's break; on tick 944, the first line over,
  `attempt=2 ... response=201 sample=199 line=0`, duration 7331 ms, the original's 8331 less the scheduler's
  second. He is up beside both shut rides in `run-busy1/2-told-busy.png`.
- Main's build (`control`, off-screen): the same break with no mechanic posted nothing.

| | The original | OpenTPW |
|---|---|---|
| The post | from the mechanics' base, hearing the break's event, with the ride and its item | from `BreakDown`, where the event is sent; the item is not kept |
| The order mechanics are looked at | the chain from `mFirstMechanic` | the staff list's; it decides only between a mechanic already naming the ride and a free one |
| When it is said | `0x44` on the post's tick; `0x45` 43 ticks after the line before it began | on the post's tick ("The advisor's turn in the sweep", below); `0x45` was 31 sweeps after the line before it began with his turn taken first, not measured again (why the original waits twelve more is not read) |
| The event's other two hearers, and a condemned ride's event | the tag system's log, the relay's `FUN_00481530` | counted, `RIDE_BROKEN_DOWN_EVENT`, `RIDE_CONDEMNED_EVENT` |
| The hard hat | drawn | built: "What he wears for a line", below |
| The second and third lines of each, a mechanic resting or striking, a mechanic already naming the ride | the listing's | built and held by the tests alone |

## The advisor's turn in the sweep (Q318)

The thing sweep (`FUN_00516380`) walks the used list from its head and hands each thing to `FUN_0050b360`, whose
arm for model `0xb` is the advisor's tick, `FUN_0059a550`: the poll's one row, then the best slot. A thing made is
linked in at the list's head (`FUN_00516270`), so the sweep runs newest first, and the advisor is thing 3. The
shipped park's list is 42 to 29, 15, 28 to 4, **3**, 2, 1; things 2 and 1 are models 10 (the mechanics' base) and
9, and the dispatcher has no arm for either. **His turn is the last a sweep takes**: every guest, member of staff,
placed object and manager has had its own, so a message any of them posts is said on the tick of the post. A park
made with no file makes its twelve reserved things in the same order, and whatever is bought or hired later goes
in at the head.

**The original** (`q318/orig/run.sh`, Q306's run again, twice): Q265's `bounce.TPWS`, its Belly Bounce broken
through its script 20 s after the load. The advisor's record for `0x46` read (0, -1, 0, 0) and then
(1165, 0, 1, 0), and the Bounce's assigned member read 0 on tick 1163 and 26 on 1165; the second run read
(1164, 0, 1, 0) and 26 on 1164. That the two ticks are one was predicted first and held in both; the tick itself
was guessed at 1190 to 1215 for the first run and missed, and reasoned at 1159 to 1205 for the second
(`q318/PREDICTION.txt`). The run of 2026-10-11 read 1201 and 1201.

**OpenTPW** (`ParkPeople.OnUpdate`): the messages' turn is the sweep's last call, after the guests', the staff's,
the arrivals', the staff pool's, the rides' and the gate's retry. `q318/confirm.py`, the stock park's Belly Bounce
broken 25 s in, predicted first (`q318/PREDICTION.txt`; the first round's `pending` counts were Q306's, wrong since
Q314, and were predicted again before the second run): the log read `mechanic 26 found ride 13 ... on mGameTick
885`, `post message=70 subject=13 score=110 threshold=25 tick=885 result=queued pending=2` and `attempt=1
message=70 subject=13 ... response=204 sample=202 line=0 tick=885`; `advisor` read `pending=1 attempts=1` and
`message=70/score=110/line=0/lastTick=885`, and he is up in the hard hat in `run-fix2/2-told.png` (the first run
read 886 three times). Main's build, off-screen, found and posted on 894 and said it on 895 (`run-control`).

| | The original | OpenTPW |
|---|---|---|
| A message posted on a thing's turn | said on that tick | the same |
| A message posted between sweeps (the door's, a tag's opening) | said at the next tick's advisor turn | the same, at that sweep's end |
| The poll's row | asked on his turn, after every other thing's | the same; the two built rows score by the park's age alone |
| Things 2 and 1 | after him, with no turn of their own | not things here |

Not run in the game: a ride's own post (the wear's `0x42`, the break's `0x44` and `0x45`; the tests hold the
wear's on its tick), and whether `0x45`'s 31 sweeps after the line before it moved. Not listened to in either game.

## What he wears for a line (Q308)

**The response's `+0x14` and `+0x18` are props, not face nodes.** `Advisor_SayResponse` keeps them at `0x00f79684`
and `0x00f79688`, each read once, by `FUN_00598dc0`. That routine is called from `FUN_00599800` after the line's
first clip is started (`0x00f79690`, set by the say) and again each time channel 0 takes the next clip
(`0x00f7968c`, set by `Advisor_Update` at `0x00599a73`), because **every clip of his hides the model's ten add-on
pieces as it starts**: all fifteen clips of `Advisor.md2` carry the same hide list, nodes 14 and 16 to 24, the
spatula, the eight hats and the bow tie (`hidelists.py`). `FUN_00598dc0` on the line's model slot (the response's
`+0x10` low half):

1. hides his five mouths (`0x00f796a0`, found by name by `FUN_0044b2e0`);
2. calls `FUN_00598ca0` with the first prop, and with the second where it is not nought;
3. where the clip's word carries `0x10000`, hides the node named `bug head`; and starts the clip word's
   particles (`0x3e0000`). The random chain's words carry no flag (`FUN_00598b20` stores bare clip numbers).

`FUN_00598ca0( n )` shows and hides pieces by costume id, through `FUN_00429ee0` (show: clears the node's `0x10`)
and `FUN_00429f60` (hide), which find the node with `FUN_0044b220( model, 0x400, id )`:

| `n` | What it switches |
|---|---|
| 0 | shows 19 and 20, his antennae |
| 1, 2, 4, 5, 6, 7, 9, 10, 11, 13 | hides 19 and 20 |
| 14 | hides 21, his right hand |
| -1 | the theme's costume: for each of four pairs filled by `FUN_004080e0` from `ThemeAdvisorCostumes[i]` (`0x00f797a4` the `AddOns`, `0x00f797b4` the `SubOffs`), shows the add-on and hides the sub-off |
| any but 0 | then shows the piece with id `n` itself, unless the clip's word carries `0x10000` |

His own parts are in no hide list, so antennae hidden for a hat stay hidden until a line whose prop is nought.

**The model's ids** (`md2surf.py`, the lookup records of `Advisor.md2`): 5 `Hardhat`, 6 `Security Hat`, 7
`Handyman Hat`, 8 `Kiosk Hat`, 9 `Ticket Hat`, 10 `Research Hat`, 11 `Bowler Hat`, 12 `Bowler_BTie`, 13 `Fast
Food Hat`, 14 `Spatula`, 19 and 20 the antennae, 21 and 22 the hands. The kiosk hat, 8, is the one hat that leaves
his antennae on.

**The table's props** (`responses.py`, all 610 rows read from the executable on disk):

| Slot | Props | Rows |
|---|---|---|
| 0, `Advisor.md2` | 0, 0 | 151 |
| 0 | 5, 0 the hard hat | 77 |
| 0 | 6, 0 | 47 |
| 0 | 7, 0 | 48 |
| 0 | 8, 0 | 8 |
| 0 | 9, 0 the ticket hat | 34 |
| 0 | 10, 0 | 58 |
| 0 | 11, 12 the bowler and its tie | 32 |
| 0 | 13, 14 the fast-food hat and the spatula | 18 |
| 1, the theme's `Advisor_JUN.md2` | -1, 0 | 129 |
| 1 | 0, 0 | 8 |

Slot 1 is a second model, built by `FUN_00457a90` from the level's own `advisor.wad` (`Advisor_` and the first
three letters of the theme): "The theme's own advisor and its costume", below. The built park topics' rows are
slot 0 but rows 79's and 81's: the door's 308 to 311 carry (9, 0), the rides' 194 to 206 (5, 0). The lobby's 394
carries (0, 0).

**OpenTPW** (`AdvisorProps`, `AdvisorModel.Wear`, `ParkAdvisorMessages.Topic.Prop` and `PropsOf`): as a line
begins the nodes of its first clip's hide list are hidden and the response's props put on, once a line rather
than once a clip, which comes to the same pieces since no clip of his keys an add-on and clips 1 to 15 of each
model carry one list. The speaker's queue, the lobby's lines and the console's
`say` wear (0, 0). `q308/confirm.py`, the stock park's Belly Bounce broken through its script: the line the
mechanic's decide posts began `Advisor: sample 202 ... wearing 5,0`, `advisor` read `wearing=5,0`, and in
`run-fix1/2-told.png` he is up in the yellow hard hat with no antennae, as the original's in `q306/orig/a-told.png`
(`side-by-side.png`); main's build read no `wearing` and drew him bare-headed with both antennae.

| | The original | OpenTPW |
|---|---|---|
| The props of a line | put on after every clip's start | put on again at every clip's start (Q595) |
| The hat's colour | its texel at most (his draws never carry state bit `0x10000`, `MODULATE2X`), a full yellow (`ui.md`, "How he is lit": the ambient and twice a white light, clamped) | the same law (Q657): the hard hat's lit mean 153.1 against the original's 153.0 to 155.9, its blue fuller (an eight-bit texture) |
| The theme's costume, prop -1, and the theme's own model | slot 1's 137 rows: in Lost Kingdom the fly in an explorer's hat, no antennae (`q344/orig/a-told.png`) | the same, lit as the original's (Q657; "The theme's own advisor and its costume", below) |
| A clip word's `0x10000` (no prop, the head hidden) and its particles | a gesture row's | the same (Q595): only rows 1 and 13 carry flags |

## The theme's own advisor and its costume (Q346)

**Two models, one on the screen.** `FUN_00457a90`, a park's start, calls `FUN_00429ba0( folder, textures, stem,
slot )` twice: slot 0 from `Global\Advisor` with no stem (`Advisor.md2`), and slot 1 from the level's own
`Advisor` folder with the stem `Advisor_` and the theme's first three letters (`_strncat( ..., 3 )`), its
textures looked for in the global advisor's folder too. `FUN_00429ba0` indexes everything by `slot * 0xb0`, so
both models get the one placing (translate 0.6, -0.6, 0.2), the one root scale and the one light. The lobby makes
slot 0 alone.

`Advisor_SayResponse` keeps the response's `+0x10` low half at `0x00f79674` (`0x00599100`) and starts the line's
first clip on that slot's model (`FUN_00429e90( clip, slot )` at `0x005993d5`); every later read of the model is
through `FUN_00429fe0( [0x00f79674] )`: the next clip (`0x005999fa`), the mouths (`0x00599b5a` on; the five
mouths and the head are found by name for each slot by `FUN_005989c0`, at `0x00f796a0 + slot * 20` and
`0x00f79698 + slot * 4`), the props (`FUN_00598dc0`, `FUN_00598ca0`) and the kill (`Advisor_KillModel( slot )`).
So a line's model is its response's, for the whole line, and the other is not drawn.

**The clip lengths are the park's model's.** `FUN_005989c0` fills the twenty lengths at `0x00f796c8` once, from
slot 1's model where there is one and slot 0's otherwise, and sets clip 14's to nought. The random chain
(`FUN_00598b20`) picks every line's clips by that table, whichever slot the line is on. In Lost Kingdom it makes
no difference to a line of clips 1 to 15: the two models' fifteen lengths are the same to the millisecond (1666,
2500, 3333, 4166, 5000, 7500, 9166, 10000, 10833, 12500, 3333, 5000, 11000, 1000, 666; the game's own log line
at a park's start, `q346/run-fix1`).

**Lost Kingdom's model.** `Advisor_JUN.md2` is 29 nodes, 18 of them meshes, with twenty clips. It is a whole
fly of its own with no antennae: eyes, eyelids, five mouths of the same names, body, hands, and `Ticket`, `MAP`,
`Ticket2`, `Pith Helmet` and `Mutant Eye`. Its lookup records (`md2surf.py`): 21 and 22 the hands, **4 the pith
helmet** and 18 the mutant eye in the costume space `0x400`; 1 to 3 `T_Particle1` to `3` and 4 to 6 `Rotate1` to
`3` in the space `0x100`. Clips 1 to 15 each hide the same twelve nodes as they start: `Ticket`, `MAP`,
`Ticket2`, `Pith Helmet`, `Mutant Eye`, the six particle and rotate nodes and `Wave01` (`hidelists.py`); 16 to
20, the golden ticket's, differ. Its helmet and map textures are in the level's wad (`Pith_Brown`, `Pith_Side`,
`Map`, `Mapback`), the rest in the global advisor's.

**The costume.** `FUN_004080e0` copies four pairs from the balance object, `0x007856f4 + 8i` to `0x00f797a4 +
4i` (the `AddOns`) and `0x007856f8 + 8i` to `0x00f797b4 + 4i` (the `SubOffs`): the keys
`ThemeAdvisorCostumes[i].AddOns` and `.SubOffs` of the theme's `Standard.sam`. `FUN_00598ca0( -1 )` walks the
four in turn: an add-on that is not nought is shown (unless the clip's word carries `0x10000`), then a sub-off
that is not nought is hidden, both on the line's own slot in the space `0x400`; it then asks for the piece -1,
which no model has. Lost Kingdom's file: `[0].AddOns 4`, `[0].SubOffs 19`, `[1].SubOffs 20`. So on slot 1 the
pith helmet goes on, and the two sub-offs, the lobby model's antennae, find no node in `Advisor_JUN.md2`. All 129
rows of the table with the prop -1 are slot 1's.

**OpenTPW** (`Advisor`'s `_themeFigure`, `_speaker` and `_costume`, `AdvisorProps.CostumeOf` and `Switched`,
`ParkAdvisorMessages.SlotOf`): a park's advisor loads `levels/<theme>/advisor/Advisor_<THE>.md2` beside the
lobby's model, a line plays on the model its response's slot names and that one alone is drawn, the prop -1 puts
on the balance's four pairs, and the chain's clip lengths are the theme's model's where there is one. A built
topic's slot is 1 where its prop is -1.

`q346/confirm.py fix` (`q345/nofeat30.TPWS`, the stock park with no feature; all four predictions held, the
guess about the clip lengths among them): at the park's start `Advisor: the theme's own model is
levels/jungle/advisor/Advisor_JUN.md2, its costume 4/19 0/20 0/0 0/0`; row 81's line on tick 1593 began
`Advisor: sample 224 ... wearing -1,0 on model slot 1`, `advisor` read `saying sp_224.mp2 ... wearing=-1,0
slot=1`, no `ADVISOR_THEME_COSTUME` was counted, and `run-fix1/1-line.png` shows the fly in the pith helmet with
no antennae, as the original's saying the same line of the same file (`q345/orig/a-g4.png`; `side-by-side.png`).
The line over and the Belly Bounce broken through its script, the mechanic's line began `... wearing 5,0 on model
slot 0` and `3-hardhat.png` shows the lobby's fly in the hard hat. Main's build (`run-control2`): `wearing -1,0`,
`1x ADVISOR_THEME_COSTUME`, the fly bare-headed with both antennae. Helmet-coloured pixels in the head's box:
5862 against the control's nought.

| | The original | OpenTPW |
|---|---|---|
| Who says a slot 1 line | the level's own model, alone on the screen | the same |
| The costume | four pairs of the theme's `Standard.sam`, put on after every clip's start | the same pairs, put on again at every clip's start (Q595) |
| The helmet's colour | tan, with a grey band | the same (Q657): median (151, 118, 56) against (151, 116, 49); its band 122 and 138 against 117, caught mid-squash by the ticket's clips of this game's line 434 (`ui.md`, "How he is lit") |
| The chain's clip lengths | the park's model's, for both slots | the same |
| Clips 16 to 20, the map, the tickets and the mutant eye | the golden ticket's lines (`scenes.md`, "Gesture table") | clips 17 to 20 played by the ticket lines (Q595); the big ticket of 18 to 20 grown by its scale track (Q599; `ui.md`, "The big ticket is a scale track"); clip 16 (row 0's) by no park line |

Not run: the original, this session (its frames of this file are Q344's and Q345's); a slot 0 line straight
after a slot 1 line in the original; the three other themes' models and costumes (not measured); Full
Simulation (the model is the level's in either mode: not asked). Not heard in either game.

## The tag a line leaves on the aerial (Q309)

**Who hears a said line.** The event of type `0x18` (`FUN_0059b620`, above) carries the response's group at `+8`,
the record's thing at `+0xc`, -1 at `+0x10` and the record's `+0x04` (the thing's item) at `+0x14`. One thing
subscribes to `0x18`: the tag system, kind `0xc`, whose constructor `FUN_00509b40` subscribes it to types 7, `0xc`,
`0x14`, `0x18`, `0x19` and `0x13` (each `PUSH type`, `FUN_00415e00`, `FUN_0040f9e0`). Its hearer `FUN_00509bf0`
(`FUN_0050b550`'s case `0xc`) acts on three: `0x13` with a detail of 7, 8 or 9 (the postcard's three ends), `0x18`
and `0x19`; any other only logs. For `0x18`:

1. `FUN_0050af40( group )` finds the group's row in the **tag table**, `0x0075eca8` to `0x00761c68`, 382 rows of
   `0x20` bytes: nothing for `0x17e` and `0x17f`, else the row at the id's own place where it holds the id, else
   the first holding it. No row, no tag.
2. The tag's life is the event's `+0x10`, or the row's `+0x18` where that is under nought (always, from a line).
3. `FUN_00509e10` writes the tag's text from the row's string (`+0x0c`) by the row's kind (`+0x1c`): kind 0 the
   string alone, kind 2 the string with the item's name (`FUN_00412f50`);
   the other kinds, 5 to 8 the Local tickets' numbers among them, are in "The text, `FUN_00509e10`" below.
4. `FUN_00481580` hands the messages control the tag: its id, the row's `+0x04`, the text, the life, the row's
   picture (`+0x08`), the row's two button types (`+0x10`, `+0x14`), the thing and the item.

**A row** (`lib/tagtable.py`, read from the executable on disk):

| Offset | What | Over the 382 rows |
|---|---|---|
| `+0x00` | id | 114 stand at their own place |
| `+0x04` | key: the id of the tag this one takes the place of | 107 name themselves |
| `+0x08` | picture, a short: the part of `msgtag.md2` shown | 0 to 5 |
| `+0x0c` | string | |
| `+0x10`, `+0x14` | the message bar's two buttons (`FUN_0048e610`: 3 and 4 `mb_locate`, 9 to `0x11` `mb_buy`, `0x12` `b_loan`, `0x13` `mb_resrch`, `0x14` `b_pkinfo`, `0x15` `b_staffcost`, `0x17` `mb_callmech`, `0x18` `b_entry`, `0x1a` `mb_accept`, `0x1b` `mb_decline`) | the second is 0 or 27 |
| `+0x18` | life, ms | 0 (121 rows), 30000 (228), 90000 (33) |
| `+0x1c` | text kind | 0 to 36 |

Of the response table's 149 groups, 148 have a row; the one without is `0x17f`, the lobby's lines'. The built
topics' rows:

| Group | Lines | Key | Picture | String | Buttons | Life | Kind |
|---|---|---|---|---|---|---|---|
| 68 | a ride about to break down (194 to 196) | 383, none | 1 | 71 | 3, 0 | 30000 | 2 |
| 70 | no mechanics (198 to 200) | 68 | 1 | 72 | 3, 0 | 30000 | 2 |
| 71 | the mechanics all busy (201 to 203) | 68 | 1 | 73 | 3, 0 | 30000 | 2 |
| 72 | a mechanic on his way (204 to 206) | 68 | 1 | 74 | 3, 0 | 30000 | 2 |
| 3 | a shut park people wait outside (2 to 4) | 4 | 2 | 7 | 24, 0 | 30000 | 0 |
| 150 | the park open (308, 309) | 151 | 1 | 149 | 0, 0 | 30000 | 0 |
| 151 | the park shut (310, 311) | 150 | 1 | 150 | 0, 0 | 30000 | 0 |
| 102 to 113 | the golden tickets' lines (434 to 469, each line its own group; `ride-operation.md`, "Golden tickets") | 383, none | 3 | 180 to 191 | 0, 0 | 0 | 5, 6, 0, 0, 7, 8, then 0 |

The ticket rows' tags have no life, so they stand until taken off. Their group is the line's: Local ticket *i* says
line *i* and leaves 102 + *i*, Global *i* 108 + *i*, the Secret 112 (113, the Secret's index 1, is never awarded).

**The messages control's list, `FUN_0048eb20`** (through `FUN_0048ede0`; nothing is done with the interface down,
`[0x007cb2ac]` nought). The ten tags are controls made once by `FUN_0048eaa0` (`FUN_0048ea00`: 70 by 65, mesh
`msgtag`, handler `FUN_0048dae0`, help row 475, "Message - left-click to open, right-click to delete"), with ten
records of `0x208` bytes at `0x007c26b0` (the control, in use, the text).

1. Every tag standing whose id is the new one's **key**, about the same thing and item: where the new id is its
   own key the standing tag takes the new text and nothing more is made; otherwise it is taken off at once
   (`0x100002` with 1). So a mechanic's line takes off the about-to-break tag of its ride, and the door's two take
   each other's.
2. Otherwise, with fewer than ten held (`[0x007ca09c]` against `[0x00750610]`), the first free record's control
   takes the picture (`FUN_0065d3a3`), the text, timer `0x80080` at the life and timer `0x80081` five seconds
   before it (both only for a life over nought), and its data; the mast is sent `0x100001` with it, and the
   interface's sound `0x1e` plays.

**The mast, `LAB_004a11e0`** (control `0x2d`, `[0x007cb2c4]`; `hud.md`, "The management gadget"):

- `0x100001`, a tag put on: the tag is enabled and shown half a second on (message 6, `0x004a1292`; `hud.md`, "A
  tag's effects follow its visibility"), the mast's top edge goes up by the tag's height and ten
  (`FUN_0065cd95( 0, -h, 0, 0 )`), the tag is put across the mast's middle with its top eight under the mast's
  new top, and the red top `0x2e`, which follows the mast's top edge, plays the stream at `0x00752e48` at 50 ms a row,
  unless one plays on it already (`0x004a135b`; `hud.md`, "The aerial's squash").
- `0x100002`, a tag taken off: hidden, then message 5 to it, at once with a 1 and half a second on otherwise.
- `0x100003`, the top's right click (`FUN_004a1190`): message 5 to each tag from the first, 100 ms after the one
  before, the first 100 ms on.
- A tag's message 5 (`FUN_0048dae0`): its particle killed, its timers stopped, its record freed, the mast's top
  put down by its height and ten, each later tag moved down the same and played the stream at `0x007502f0` at 20 ms
  a row unless one plays on it (`0x0048dcda`), the red top the same at 50 ms, started over (`0x0048dd06`), and the message bar retracted where it showed this tag.

**A tag's own handler, `FUN_0048dae0`:** timer `0x80080` hides it and sends the mast `0x100002` with nought, so
it is off half a second after its life ends; timer `0x80081` starts particle effect `0x5b` over its middle, once (and its
first show starts `0x5a` there: `hud.md`, "A tag's effects follow its visibility");
a left click plays sound `0xc0` and shows its message on the bar (`FUN_0048d9c0`, which posts advisor message
`0x11c`), or retracts the arm where it is the one on show; a right click, unless its message is the one being
shown (`[0x007ca094]`), hides it and sends `0x100002` with nought. The tags are written to a park file
(`FUN_0048ee50`, records of `0x21c` bytes) and read back (`FUN_0048ef70`): `saves.md`, "OpenTPW's writer, the
aerial's tags".

**The text, `FUN_00509e10`** (the tag system's buffer at `+0xc`, 0x400 characters; its answer is not looked at by
its caller): a switch on the row's kind. Kind 0 formats the row's string alone from the table at `[0x0078ba90]`,
the running language's `TAG_SYSTEM.str` (332 rows). Kind 2 formats it with one argument, number 0, the item's
name: `FUN_00412f50` on the item manager `0x007890a0`, the wide string at the item's record `+0x410` (what fills
it is not read: it reads "Belly Bounce" for 1100 and "The Hot Pot" for 1140, each item's `Info.Name`); with the
event's item nought it logs "Object id is IDNULL in CTagSystem" and leaves the buffer as it stood. Kind 1 writes
"A string needs to be put in here" for a thing that exists, kind 4 "String and number goes here". **Kinds 5 to 8,
the Local tickets' rows,** format the park's `GoldenTicketLocal` globals (`ride-operation.md`, "Golden tickets"), each
a number variable (`Varm_Construct` `0x004a77a0` makes one; its `+8` is the string's parameter number, and the number
writer `0x006add40` letters `"%d"` of the size and never the sign): kind 5 `Visitors` (`0x00785790`), kind 6
`PeopleInPark` (`0x00785794`) and kind 7 `ProfitYear` (`0x007857a0`), each as parameter 1; kind 8 `RecentVisitors`
(`0x007857a4`) as 1 and `RecentVisitorMonths` (`0x007857a8`) as 2. Kind 3 formats the event's number; kinds 9 to `0x24`
format numbers and names from the event's record, not read further.

**The original** (`q309/orig/run.sh`: Q265's `bounce.TPWS`, `scriptvar.py PID 13 4 1` twenty seconds after the
load, `original/tags.py`, `advisor.py` and `object.py` polled together): with no tag the mast's rectangle read
(94, 976, 144, 984) and none held. On the poll of tick 1198, 21.47 s in, where message `0x46`'s record read said,
one was held: its control at **(84, 909, 154, 974)**, the mast **(94, 901, 144, 984)**, its text "Your Belly Bounce
ride has broken down, and a mechanic is on his way to fix it." At 52.01 s, tick 1368, none was held and the mast
read (94, 976, 144, 984) again: **30.54 s** on, the life and the half second. The frames (`a-told.png`,
`a-end.png`) show the red top lifted over a red block with a white picture, then back down.

**OpenTPW** (`ParkTags`, `ParkAdvisorMessages.Topic.Group`, `ParkGadget.PutTags`; `q309/confirm.py`, the stock
park's Belly Bounce broken through its script 25 s in, 5 predictions of 6 and the control's one): the line began
and the log read `Park tags: tag 72 picture 1 subject 13 life 30000 standing 1` and `Park gadget: aerial:
mast=94,901,144,984 top=77,773,161,901 tag900=84,909,154,974/frame=1/shown=1`, the original's rectangles; `tags`
read `standing=1 tag=72/picture=1/subject=13/age=1945/hidden=0`; the red top's upper edge stood at window row 363
where it had stood at 398, the 35 rows 75 units are at 720 down; 28 s on the tag stood, and 32.5 s on `tags` read
`standing=0` and `mast=94,976,144,984 top=77,848,161,976`. `side-by-side.png` holds both games' aerial before,
with the tag and after. The prediction that missed was a pixel rule set without the picture in mind: the red top
at rest covers the tag's box, so the box was 14.6% red before the line where under 5% was predicted, and 19.3%
with the tag where over 30% was. Main's build said the same line, moved nothing and counted
`ADVISOR_MESSAGE_SAID_EVENT`.

| | The original | OpenTPW |
|---|---|---|
| The rows | 382 in the executable | those the built topics' groups name, the golden tickets' 102 to 113 among them; any other group with a row is counted, `TAG_ROW_NOT_LISTED` |
| The match of a standing tag | id, thing and item | the same; the item is the subject's catalogue id |
| The times | the interface's timers, real milliseconds | the same clock (`Time.WallMilliseconds`), from the gadget's update |
| The red top and the later tags as one comes or goes | put down at once, then squashed and sprung back (streams `0x007502f0`, `0x00752e48`; `hud.md`, "The aerial's squash") | the same (`ParkGadget.Squash`), by the same rule: a row on the first frame more than the interval after the last, so the length is the frame rate's in both (1.00 s in the original at the 30 fps cap, about 0.8 s here, `q637/run3`) |
| The tag's first show | half a second after the mast lifts, effect `0x5a` over the tag, not while paused | the same (`ParkGadget.TagEffects`) |
| The particle five seconds before the end | effect `0x5b` over the tag, hidden and shown with it, moved down with it, killed as it is opened or goes | the same |
| A left click | sound `0xc0`, the message bar on the arm with the text and the buttons | the same (`hud.md`, "The message bar"), and an opened tag's timers stop there as here |
| The tag's text | written from the row's string by its kind (`TagSystem_WriteText` `0x00509e10`), 37 kinds | kinds 0, 2 and 5 to 8, those the listed rows use: the row of `TAG_SYSTEM.str`, for kind 2 the item's `Info.Name` where its parameter 0 stands, for 5 to 8 the balance's `GoldenTicketLocal` keys; any other kind, and a kind 2 about no item, is counted, `TAG_TEXT_KIND`, and empty. Shown on the message bar, written to a park file and read by `tags` (a line break as `\n`) |
| The sounds' loudness | not measured | each at its variation's own volume, as the boot's |
| The tags in a park file | written and read | written and read (`saves.md`, "OpenTPW's writer, the aerial's tags") |
| The other two events (`0x19`, the postcard's) | heard | nothing sends them here |

**The golden tickets' tags, both games (Q707; `q707/p707t/`).** Alexah's jungle `New Save` with her Full Simulation
profile less Locals 0 and 1 (Standard keys: `Visitors` 100, `PeopleInPark` 200). The original (`orig/O`, `tags.py`):
at the `0xd8` line, tick 19115, a tag "YOU'VE WON A GOLDEN TICKET!", a blank line, "Congratulations! You've won a golden
ticket for getting 100 visitors to your park. Keep up the good work!" at (84, 909, 154, 974); at the `0xd7` line, 19162,
"...for having 200 people in your park at one time. Keep it up!" at (84, 834, 154, 899), in record 0, which the file's
ticket-price tag had freed at its life's end; both stood 77 s on, at the poll's end, and the frames show the golden
ticket picture on each (`g-20`, `g-22`). OpenTPW (`run2`): `Park tags: tag 102 picture 3 subject 0 life 0 standing 2`
and `tag 103 ... standing 2`, the same texts, slots, rectangles and picture; a click on 102 letters both paragraphs on
the bar with no buttons (`T-bar.png`). Each tag comes with its line, and ours says the `0xd8` line on 19101 where the original said
it on 19115 (Q595's account, `ride-operation.md`, "Golden tickets").

## The line of an opened tag message (message `0x11c`, Q312)

**The post.** Showing a tag's message on the bar (`FUN_0048d9c0`, `hud.md`, "The message bar") calls
`FUN_00486b00( 0x11c )`: `FUN_0059b590( 0x11c, thing 0, 0, line -1, not forced, score -1 )`, then `FUN_005194d0`
and `FUN_0059a940`, the same gate and slots as every post. The bar's turn as the arm starts in with it
(`FUN_0048e2f0`, `0x1001d8`) calls `FUN_00486b40( 0x11c )`, the withdrawal. `FUN_004a2590`, which sends the arm
in before it sends it out, comes before the post in `FUN_0048d9c0`, so a second tag opened over the first
withdraws and then posts.

**Row 284** (`0x00772120`, from the emulated table): posted only, category **1**, either game mode, score
`0x0059e8c0` (`MOV EAX,[ECX + 0x328]`, so `0x00fb3868`), one copy in the slots, first response 544, one line, mode
2. Response 544 is sample 561, lip 561, the random gesture chain, model slot 0, no prop, group 383 (no tag):
"these messages help you stay on top of new developments; to look at related information, or to close or delete
the message, click on the buttons".

**The score is `HelpMessage.Score`, 1000 in the file.** In the schema's descriptor list (`0x0072e210`, 60 bytes
a row) a section's fields come BEFORE its type 1 row, a type 0 row after it; counting four bytes a field from
`RidesBreakdownImminent.Score` at `+0x248` (descriptor `0x0073199c`) puts `HelpMessage.Score` at `+0x328` and
`HelpMessage.ContextHelpScore` at `+0x32c`, and `FUN_0059ab50` reads the second as `DAT_00fb386c`. Both are 1000
in the file, so the slot's score in the running original does not tell the two apart; the walk does.

**The category** is `MessageGroups[1]`, "Tutorial messages": `MinTimeSameMessage` 0, `SayOnlyOnce` 1,
`DiscardAfterSlaps` 3. The gate refuses it with the Tutorial option off, once he has said it, and once he has
been cut off saying it three times (the table under "The accept gate").

**The withdrawal, `FUN_0059aa70( id )`.** (1) Where the advisor's byte `+0x16e4` is set, the message's category is
1 and `+0x16dc` is the id, the byte is cleared: the kept message at `+0x16d0`, which `FUN_0059ab50` raises again,
forced, at `HelpMessage.ContextHelpScore` (its one caller `FUN_00488ba0`). The byte is the kept record's own `mValid`: a raise of a message `FUN_0059c500` answers 1 for copies the whole record there before the gate is asked (`FUN_0059a940`). (2) Every
occupied slot whose message is the id is emptied. (3) The message being said is `+0x16ec` while the clock
(`FUN_00402d80`) is under `+0xd8 + +0xdc`, the last line's start and its length and second, and `0x160`
otherwise; where that is the id: `Advisor_StopSpeaking`, the message's said byte `+0xe8` cleared, its `+0xec` one
up, and `+0xdc` nought, so the next sweep may say a line. The tick stores `+0x16ec` with each line said
(`0x0059a86a`).

So he explains the message bar the first time a tag is opened; let finish, he never says it again in that park;
shut up by the bar folding away, he starts again at the next opening, and after three such cuts not at all.

### The original (`q312/orig/run.sh a|b`, `original/advisor.py`; predicted first, five of five)

Q265's `bounce.TPWS`, its Belly Bounce broken 20 s after the load, the mechanic's tag clicked four seconds after
it came. **Let finish (a):** 7.2 s after the mechanic's line began slot 0 read (thing 0, line -1, `0x11c`, score
**1000**); 0.47 s on, as that line's 7498 ms ended, the record read (tick 1210, line 0, said 1, slaps 0) and the
action 13332 ms; the tags held stayed one. The bar folded 25 s after the click and opened again: no slot held
`0x11c` in the 14 s after and the record did not change. **Cut off (b):** the bar folded 10 s into the line:
record (1202, 0, **said 0, slaps 1**), the action's length **0**. Opened again: in a slot and said 0.1 s on (tick
1262, said 1, slaps 1); folded: slaps 2; opened, said on tick 1303, folded: slaps 3; a fourth opening: no slot
held it in 12 s. In the frame he is up beside the bar with no hat (`q312/side-by-side.png`).

### OpenTPW (Q312: `ParkAdvisorMessages.TagMessage`, `Withdraw`, a topic's `Category`; `ParkGadget.WithdrawBarLine`)

`q312/confirm.py a|b|off|control`, the stock park's Bounce broken 25 s in and the tag clicked four seconds after
the mechanic's line began; predicted first, seven of seven (a twice, the first run's third lost to the script's
own log window, the log itself as predicted). The click logged `Park advisor: post message=284 subject=0
score=1000 threshold=25 tick=891 result=queued pending=1`; 2.8 s on, `attempt=2 message=284 subject=0 score=1000
threshold=25 response=544 sample=561 line=0 tick=905 duration=12332`, a second under the original's 13332 as
every line's is (the second is added to the wait); `advisor` read `saying sp_561.mp2` and
`message=284/.../said=1/slaps=0`. Folded after the line: `withdraw message=284 cleared=0 cut=0 slaps=0`; opened
again: `result=said-once`. **Cut (b):** folded three seconds into the line: `withdraw message=284 cleared=0 cut=1
slaps=1`, `Advisor: cut off mid-line`, `advisor` reading `quiet` and `said=0/slaps=1` a second on; each of the
next two openings said it again on the next sweep (attempts 3 and 4, `playback=started`), cut to slaps 2 and 3;
the fourth opening logged `result=slapped`. **`tutorial off`** in the console: `result=tutorial-off` and no
attempt. Main's build named message 284 nowhere and counted `TAG_MESSAGE_BAR_ADVISOR_LINE`.

| | The original | OpenTPW |
|---|---|---|
| The said byte and the cuts | the advisor thing's records, in the park file | the same, for the built messages ("The records in a park file") |
| The kept message (`+0x16d0`, `FUN_0059ab50`) | forgotten by a withdrawal of its id | kept by nothing here |
| The Tutorial option | read at each post | the same; the console's `tutorial on|off` sets it in memory |
| The cut | `Advisor_StopSpeaking` | the same call (`Advisor.StopSpeaking`), with its cry where his voice had started |
| The other 39 screens' lines (`0xbc` to `0xd5`, `0x11d` to `0x135`) | posted as each opens | the buy screen's `0xbc` and the hire screen's `0xbd` are ("The buy screen's own line", "The hire screen's own line"); the other 37 are not posted |

## The records in a park file (Q313)

The advisor is a thing: `FUN_005156a0` (`0x00515920`) allocates `0x16f0` bytes, constructs it with `FUN_00599e70`
and keeps its id at the world's `+0x1da71c`, which `FUN_005194d0` answers. So every message's record ("The
per-message record", above) is written with the thing and read back with it, and what he has said, the line he is
on, the tick and the cuts all last across a save. In a park file he is thing 3, model 11, 5,846 bytes, and message
`id`'s record is at the thing record's `+0xcc + id * 0x10`, memory's `+0xe0` (FileFormats `saves.md`, "The advisor
(model 11)"): 6,318 records in eighteen stored files of the original, none odd (`lib/parkfile.py -a`).
`q257x/orig/broken-by-the-original.TPWS`, saved by the original after its mechanic's line, holds `0x46` as
(1815, line 0, said 1, slaps 0); Alexah's Lost Kingdom park holds `0x11c` as said 0, slaps 3: cut off three times
and never to be said again there.

### OpenTPW (`ParkWorld.AdvisorHistoryOf`, `PutAdvisorHistories`; `ParkAdvisorMessages.Restore`, `Written`)

A load hands each built message's record to the park's messages (`Level`, logged `Park advisor: restored
message=...`), and `savepark` writes the seven built messages' records over the file's, the three bytes after a
said byte and every other message's record left the file's. `PARK_ADVISOR_SAVED_HISTORY` is gone.

`q313/confirm.py fix|control`, predicted first, four of four twice and the control one of one twice: the stock
park's Bounce broken, the mechanic's line said on tick 880 and the tag's line on 911, the bar folded, `savepark`.
The save logged `advisor message 70 written: line 0 lastTick 880 said 1 slaps 0` and `advisor message 284 written:
line 0 lastTick 911 said 1 slaps 0`; the file's thing 3 held those two records and every other byte of its 5,846
was the shipped file's. Loaded from the Load Park list: `restored message=70 line=0 lastTick=880 said=1 slaps=0`,
the same for 284, `advisor` reading both, and the tag clicked 23 s on logged `post message=284 ...
result=said-once` with the bar out and nobody beside it. Main's build wrote both records fresh, counted
`PARK_ADVISOR_SAVED_HISTORY`, and after its load said the line again, the advisor up beside the bar
(`q313/ours-side-by-side.png`).

**The original, given that file** (`q313/orig/run.sh`, predicted first, two of two): `advisor.py`'s one line in
90 s read `'0x11c': (911, 0, 1, 0), '0x46': (880, 0, 1, 0)` from the load on; the tag clicked 27 s on put the bar
out, no slot ever held `0x11c` and no advisor came up (`orig/fix-clicked.png`). Its control is Q312's run a, a file
with no record of `0x11c`: a slot held it at the click and he said it.

| | The original | OpenTPW |
|---|---|---|
| The records of the seven built messages | read and written with the thing | the same |
| The other 344 records | the same | carried: the file's own bytes are written back |
| The eight slots | written with the thing | the same ("The slots in a park file", below) |
| The last action's start and length, the message playing, the kept message | written with the thing | carried as the file's |
| A park made with no file | the constructor's | the same |

Not in a game run: a rotation carried across a load (the test holds line 1 after the file's 0), the cooldown from a
file's tick, a file with cuts, Alexah's own parks. The two calls in `Level` are held by the game runs alone.

## The slots in a park file (Q314)

**The serialiser, `FUN_00599f60`,** names every field it writes, and writes them in this order after the thing's
sixteen bytes (`FUN_0050b090`): `mTimeSig` (`+0x0c`, by `FUN_0041a860`), `mSoundId` (`+0x10`), the eight slots
(`+0x14` on, each by `FUN_0059ba30`), `mNextAvailableQueuePos` (`+0xd4`), `mLastActionStarted` (`+0xd8`),
`mLastActionDuration` (`+0xdc`), the 351 `mHistory[i]` (`+0xe0`, sixteen bytes each, whole), the kept message
(`+0x16d0`, by `FUN_0059ba30` again), `mRequestedLastTutorial` (`+0x16e8`, one byte) and `mMessagePlaying`
(`+0x16ec`). `FUN_0059ba30` writes a message record ("The message record", above) as twenty-one bytes, not
memory's twenty-four: `mThingId` (two bytes), `mSecondary`, `mMessage`, `mScore`, `mValid` (one byte),
`mResponseNo`, `mOverrideOnlyOnce` and `mScoreOverridden` (a byte each). So the file's record is 5,846 bytes
where memory's is `0x16f0`, a slot is at `24 + 21 n` of it, and the histories begin at 204 (FileFormats `saves.md`,
"The advisor (model 11)").

**The two times are readings of the second stopwatch** (`FUN_00402d80` is `FUN_00403150`), which the clock module
saves and a load sets again (`park-engine.md`, the clock's table): `Easymode.TPWI`'s action began at 114,820,087
and lasted 10,832 ms, and its file's stopwatch reads 114,876,286, so the wait a file holds is over or not by the
file's own clock, whatever the computer's.

**What the files hold** (`q314/sweep.py`, 21 stored files of the original): twenty hold one taken slot and one
holds none; the taken slot is always message `0x80`, the park's opening, at a score of 20, line -1, neither byte
set, in slot 2 (sixteen files) or slot 0 (four). It is under `MinScoreForConsideration`, 25, so the tick never
says it and never lets it go, and while it stands the gate's count of copies (test 4, one copy for `0x80`)
refuses every later post of the park's opening. A slot let go keeps its other bytes (the shipped file's slots 0
and 1 read messages `0xbc` and `0x15a` at 1000 with `mValid` nought); slots never used are `CD` throughout.
`mTimeSig`, `mSoundId` and `mNextAvailableQueuePos` are nought in all 21.

### OpenTPW (`ParkWorld.AdvisorSlotOf`, `PutAdvisorSlots`; `ParkAdvisorMessages.RestoreSlots`, `WrittenSlots`)

A load puts each taken slot's message into the same slot of the park's messages with its file's score, subject and
second word (`Level`, logged `Park advisor: restored slot=...`), and `savepark` writes the eight slots back: a
waiting message whole, as `FUN_0059b590` builds an unforced post with no score given, its second word the
subject's item; of an empty slot the `mValid` byte alone. A taken slot this game cannot say (a message of no built
topic, a line asked for by number, a forced post, a score the poster gave) is counted
`PARK_ADVISOR_SAVED_SLOT_NOT_BUILT`, keeps its slot, is said by nothing and is written back as the file's: no
stored file holds one.

`q314/confirm.py fix|control`, predicted first, four of four and the control one of one. Entering the stock park
logged `restored slot=2 message=128 subject=0 secondary=0 score=20` and `advisor` read `pending=1`. A Hot Pot
bought and broken, the Belly Bounce was broken as the mechanic's line for the pot was being said: `post message=69
subject=13 score=107 ... result=queued pending=2`. `savepark` under the pause logged `advisor slot 0 written:
message 69 thing 13 secondary 1100 score 107` and `advisor slot 2 written: message 128 ...`; the file's slot 0 read
`0d00 4c040000 45000000 6b000000 01 ffffffff 00 00`, the record the original's own memory held in Q307's run
(thing 13, 1100, line -1, `0x45`, 107), and every other byte of the eight slots was the shipped file's. Loaded
from the Load Park list: both slots restored, and on the first sweep `attempt=1 message=69 subject=13 score=107
... response=201 sample=199 line=0 tick=861`, tag 71 on the aerial at (84,834,154,899) beside the pot's
(`run-fix1/2-loaded.png`); the speaker refused the sample, the gadget's opening line having him. Main's build read
`pending=0` on entry, wrote no slot (its file's slot 0 the shipped file's) and after its load made no attempt and
left one tag.

**The original, given that file** (`q314/orig/run.sh`, predicted first, three of three): from the load, tick 860,
its slots read `(0, 13, 1100, -1, '0x45', 107)` and `(2, 0, 0, -1, '0x80', 20)` and its action the file's; a
quarter of a second on, tick 861, the record of `0x45` read `(861, 0, 1, 0)`, slot 0 was let go, the action was
8,331 ms long and a second tag stood at (84,834,154,899) reading "The Belly Bounce ride has broken down and all
your mechanics are busy. Maybe you should hire some more?". Its control is Q313's load of a file with slot 2
alone: no `0x45` in any slot or record.

| | The original | OpenTPW |
|---|---|---|
| A waiting message of a built topic | read and written with the thing | the same |
| A taken slot of any other kind | the same | counted, kept taken, written back as the file's |
| A slot let go | its other bytes stand as they were | the file's bytes stand; a message said here leaves the file's older bytes, not its own |
| The shipped park's opening message at 20 | waits for ever and refuses the next | the same |
| The last action's start and length, `mMessagePlaying` | on the second stopwatch, saved and set again | read and written: "The last action in a park file", below |
| The line said at once after a load | heard | heard: no line is said at a park's entry any more ("The poll, and the control panel's line", below) |

Not in a game run: the door shut and opened again after a load (the test holds `duplicate`), a taken slot of an
unbuilt message, a full eight slots. The two calls in `Level` are held by the game runs alone.

## The last action in a park file (Q315)

**Three fields time his wait, and all three are saved.** The tick (`FUN_0059a550`) returns while the second
stopwatch (`FUN_00402d80`) reads under `mLastActionStarted + mLastActionDuration` (`+0xd8`, `+0xdc`), an unsigned
compare; a line said stores the stopwatch's reading, the line's length and 1000, and the message's id in
`mMessagePlaying` (`+0x16ec`). The withdrawal (`FUN_0059aa70`) cuts him off only inside that wait and only where
`mMessagePlaying` is its message, and then stores nought in the duration alone: the start stands. The constructor
(`FUN_00599e70`) stores nought in the start and the duration and does not write `mMessagePlaying`. The serialiser
writes the three with the thing ("The slots in a park file", above), and the clock module holds the stopwatch
they are read against, which a load makes read the file's again (`park-engine.md`, the clock's table,
`FUN_00402e80`): **a park saved as he speaks goes on waiting after its load for what was left of the line.**

**What the files hold** (`q315/sweep.py`, the same 21 stored files of the original): two were saved inside the
wait, `q257x/orig/broken-by-the-original.TPWS` (start 115,150,244, wait 7,498, playing `0x46`, stopwatch
115,156,783: 959 ms left) and a Full Simulation autosave (28,196 ms left, playing 0); in the other nineteen the
wait was over, by 134 ms to fifteen minutes. Five hold a wait of nought, a withdrawal's cut.

### OpenTPW (`ParkClock.Second`, `SecondAt`; `ParkWorld.AdvisorActionOf`, `PutAdvisorAction`; `ParkAdvisorMessages.RestoreAction`, `WrittenAction`)

The messages keep the start as a moment of this game's clock, the wait and the message playing. A load moves the
file's start onto this clock by its distance from the file's stopwatch (`Level`, logged `Park advisor: restored
action started=... left=...`), as a saved script's deadline is moved (`ParkRides.Moved`); `savepark` writes the
start back as a reading of the stopwatch the file is written with, which `ParkClock.Put` moves with the clock.
An advisor with no file's action who has said nothing writes the constructor's noughts.

`q315/confirm.py theirs|fix|control`, predicted first: one of one, three of three and the control one of one.
The original's own file entered: `restored action started=115150244 duration=7498 playing=70
stopwatch=115156783 at=6556 left=959`. The stock park entered: `started=114820087 duration=10832 playing=282 ...
left=-45367`. Q314's scene (a Hot Pot broken, and the Belly Bounce broken as the mechanic's line for the pot is
said), saved under the pause 1,507 ms into the line: `Save: advisor action written: started 114902772 duration
7498 playing 70 stopwatch 114904279`, the file's three fields and its `KOLC` second dword the same, and no other
byte of thing 3 outside the slots and the records moved. Loaded from the Load Park list: `restored action ...
left=5991`; a second in one tag stood (`run-fix1/2-inside-the-wait.png`); the attempt of `0x45` came 6,206 ms
after the load's moment, on tick 893, 25 ticks after the load's 868, and tag 71 stood beside the pot's
(`3-after-the-wait.png`). Main's build wrote the stock file's action, attempted `0x45` on the first tick after
its load and had both tags a second in.

**The original, given that file** (`q315/orig/run.sh`, predicted first; the length was predicted 8,331 and read
8,332): from the load, tick 868, its action read `(114902772, 7498)`, its slots `(0, 13, 1100, -1, '0x45', 107)`
and the opening's, the record of `0x45` `(0, -1, 0, 0)`, one tag held. 6.21 s of wall time on, tick 893, the
record read `(893, 0, 1, 0)`, the action `(114910475, 8332)`, a start 205 ms after the saved wait's end, and the
second tag stood at (84,834,154,899). Q314's file, saved with the stock's action, was said a quarter second after
its load.

| | The original | OpenTPW |
|---|---|---|
| A file saved inside the wait | waits what was left, then says the best slot | the same; the same tick in the run above |
| The message playing | read by a withdrawal inside the wait | the same |
| A withdrawal's cut | the duration nought, the start as it was | the same |
| The stopwatch | its own, unscaled; parts from the clock when the clock's rate is not 1 | the file's second dword moved with the clock: this game's clock has one rate |
| The control panel's line | a polled message through the slots, so it starts an action | the same ("The poll, and the control panel's line", below) |

Not in a game run: a withdrawal inside a file's wait (the tests hold it), a file with a wait of nought, a park
made fresh saved before any line, Alexah's own parks. The two calls in `Level` are held by the game runs alone.
Not heard in either game.

## The poll, and the control panel's line (message `0xb9`, Q316)

**The poll.** The tick's first act, before its wait is looked at, is `FUN_0059c680( 1 )` on the balance object
(`0x0059a58b`, `ECX = 0x00fb3540`): from the row its first dword names, the cursor, nought again past row `0x15e`,
it walks the metadata table until it has scored ONE row whose `+0x00` is nought, and leaves the cursor on the row
after. A row is scored by its function (`+0x10`) where its mode (`+0x0c`) fits the game, and -1 otherwise; a score
above nought of a message the gate (`FUN_0059abc0`, not forced) lets by makes that message the answer, and
anything else answers `0x160`, no message. The tick then scores the answer again (`FUN_0059bf20`, -1 for `0x160`)
and, where that is above `MinScoreForConsideration` (`0x0059a5f5`, `JLE` past), posts it as any post: about thing
nought, line -1, not forced, no score given (`FUN_0059b590`, `FUN_0059a940`). So a polled message waits in the
slots with the posted ones, and is said on the same tick where no wait is on.

**155 of the 351 rows are polled**, read from the running original (`original/advisortable.py`): 1-32, 58-65,
71-82, 85-92, 98, 100-105, 107-124, 140-141, 144-160, 169-173, 185, 238-257, 259-265, 267-269, 274-276, 278-282,
314, 316-317 and 347-350. A round is 155 ticks. **The emulated table is wrong in one row:** `meta_full.json` has
row 0's `+0x00` and `+0x0c` nought, and the live table holds 1 and 2 there (posted only, either mode); its other
4,912 dwords are the live table's. Every row's id is its row.

**The cursor is in the park file.** `FUN_0059bea0` stores nought in it as the balance object is first built; the
save's advisor-scoring module is that one dword (`FUN_0059c890`, between the cheats' `STHC` and its own `CSDA`),
and the load reads it back into the same place (`FUN_0059c860`, from `FUN_00415270` at `0x00415d21`). So a park
goes on from the row its file was saved on, and a park made with no file from wherever the program's cursor
stands. `Easymode.TPWI` holds 277, the row after the last its makers' park scored: its record of `0x11a`, row
282, is said on tick 605, and 150 ticks on, the file's tick 755, the 150th polled row after it is row 276.

**Row 185, message `0xb9`:** polled, category 1 (the tutorial messages: said once, refused with the Tutorial
option off, dropped after three cuts), either game mode, one copy, first response 404, one line. Response 404 is
sample 377, lip 377, the random gesture chain, model slot 0, no prop, group 383 (no tag): "see the control panel
over there at the bottom left part of the screen...". 109 polled rows stand before it. Nothing withdraws it: each
of the 53 calls of the withdrawal (`FUN_00486b40`, `FUN_0059aa70`) pushes a constant, and none is `0xb9`.

**Its score, `0x005a06c0`** (in no Ghidra function): `FUN_004f8800`'s time since `mGameTick` nought
(`ride-operation.md`, "The 24 months") over `0xC92A69C000` by `__alldiv`, whole days; `JLE` to nought against
`[ECX + 0x5dc]`, `JGE` to nought against `[ECX + 0x5e0]`, else `[ECX + 0x5e4]`. Walked from the schema
(`lib/advisorschema.py`) those are `TutorialUIPanel.AfterTime`, `.CutoffTime` and `.Score`: 10, 60 and 110 in
`Advisor.sam`. A tick is 3,750 funny seconds, a day 23.04 ticks: **the line is owed from tick 254 to tick 1382 of
a park**, and is said on the first of those ticks the cursor stands on row 185. Row 282's (`0x005a0720`, message
`0x11a`) is the same function on `TutorialExplainTooltips` (20, 80, 100), built in "The tips' line", below, and
row 281's (`0x005a0780`, message `0x119`) is `GoldTicketExplanation.Score` while the arrival loads ring holds one load
(`ride-operation.md`, "Golden tickets", "The hints"), built for Q708.

**What the shipped park holds.** `Easymode.TPWI`'s record of `0xb9` is (257, line 0, said 1, slaps 0): its makers'
park said the line on its eleventh day, and the file begins on tick 755. So **Instant Action's Lost Kingdom never
says it**: every poll of row 185, the first on tick 877, scores 110 and is refused by the gate as said once.

### OpenTPW (`ParkAdvisorMessages.Poll`, `ScoreOf`, `Refusal`, `PollCursor`, `RestoreCursor`; `ParkWorld.AdvisorPollCursorOf`, `PutAdvisorPollCursor`; topic `ControlPanel`)

Each sweep's `Tick` polls first, from the cursor's row, which is the program's (`ParkAdvisorMessages.ProgramCursor`,
handed to every park by `Level`), set from a loaded file's dword (`Park advisor: restored poll cursor row=...`)
and written to `savepark`'s file. The built polled rows are the topics `ParkAdvisorMessages` lists, each with its section below; every other polled
row is counted `PARK_ADVISOR_POLLED_ROW_NOT_BUILT`. A topic's score is its function's (`ScoreOf`: the age's bounds
for the control panel's, each built row's own reading of the facts, its section's), asked at the poll, at the post and at the tick's recheck
(`FUN_0059b620` calls `FUN_0059c0a0`). The poll logs a line a change of its answer: `Park advisor: poll
message=185 row=185 days=... score=... tick=... result=...`. **The line a park was entered with is gone:**
`UI.ParkLines.ExplainGadget` said sample 377 past the messages at every entry and every load, which the original
never does.

**How this was reached** (`q316/PREDICTION.txt`, five rounds, each written before its runs). The first build
counted the cursor in polled rows from nought at the program's start, of 156, and said the fresh-record file's
line on tick 866. Its predictions of that build held, and two things failed: the line's length (guessed under
18.5 s, read 19 to 21 s), and, in the fourth round, the original's tick after its Tutorial switch was held off
through one turn (`orig/run5.sh`, `original/bytehold.py`): predicted 1033, read **1032**. That one tick was the
round, 155 and not 156, which the live table then showed (row 0), and the same reading explained why the original
had said the fresh-record file's line on tick 877 after every load: its cursor came from the file. The build was
corrected and everything run again.

**The corrected build** (`q316/confirm.py stock|fresh|nofile|cutoff|early`, the fifth round, thirteen of
thirteen; `run5-*`). **The stock park** (four of four): `restored poll cursor row=277`, `restored message=185
line=0 lastTick=257 said=1 slaps=0`, one poll line, `row=185 days=38 score=110 threshold=25 tick=877
result=said-once`; no `sample=377`, `advisor` quiet and nobody beside the gadget four seconds in
(`run5-stock/1-four-seconds-in.png`); the Bounce broken at four seconds, the mechanic's line was heard,
`duration=6498` and `playback=started`. Main's build before the item (the control, `run-control`): `saying
sp_377.mp2` four seconds in, him up, and the same mechanic's line `duration=0`, `playback=refused`. **The stock
file with the record of `0xb9` made fresh** (`parkfile.advisor_forget`; four of four): the post and `attempt=1
message=185 ... response=404 sample=377 line=0` on tick **877**, the original's own tick for that file, the
length 20,664 ms; him up with no hat and no tag (`run5-fresh/1-saying-it.png`); saved under the pause, the
record `(877, 0, 1, 0)`, the message playing 185, the advisor-scoring dword 257, the census's `poll=`, and no
other byte of thing 3 moved; loaded back, the record and the cursor restored and the next poll of it `tick=1032
days=44 result=said-once`. **A park made with no file** (the second player, whose jungle folder is empty; one of
one): `row=185 days=4 score=0 tick=110 result=no-score`, then `days=11 score=110 tick=265 result=post` and the
line, him up (`run5-nofile/1-saying-it.png`). **The Tutorial option off, and the cutoff** (three of three):
`tutorial off` two seconds in, `tick=877 result=tutorial-off`, nobody beside the gadget; no line through the
turns on 1032, 1187 and 1342; `tutorial on`, and `days=64 score=0 tick=1497 result=no-score`, no attempt of 185
in the run (`run5-cutoff/2-past-the-cutoff.png`). **Saved before the line** (one of one): paused on tick 800,
the file's dword 59, the census's.

**The original** (`q316/orig/`, `advisor.py ... 0xb9,0x11a poll`). Its stock park (`run.sh`): the record `(257,
0, 1, 0)` and no slot holding `0xb9` for 85 s; its cursor read 59 on tick 800, as this game's does. The
fresh-record file: said on tick 877 in each of three programs (`run.sh`, `run2.sh`, `run3.sh`; the third
predicted before its run), the wait 19,164 to 20,832 ms, him up at the lower right with his antennae and no hat
(`fresh2-t05.png`), as here. The Tutorial switch held off (`run5.sh`): no line on tick 877 and no slot; switched
on on tick 901, the line on tick 1032. A file of this game's saved on tick 1506 with the record fresh
(`run4.sh`): silent for 85 s, past day 60. **This game's file saved on tick 800 before the line** (`run7.sh`,
predicted before its run): said on tick **877** exactly; with the stale 277 the build before wrote, it would
have come 122 ticks after the save.

| | The original | OpenTPW |
|---|---|---|
| The poll | one polled row a tick, of 155, from the cursor's row | the same |
| The cursor | the program's dword, saved with a park and read back | the same: `ProgramCursor`, the file's advisor-scoring dword |
| The control panel's line | row 185: days 11 to 59 of a park, said once | the same |
| The stock park | never said: its file holds it as said | the same; the row's first turn on tick 877 in both |
| A file with the record fresh | said on tick 877 | said on tick 877 |
| The Tutorial option off | not posted; said a round on once it is on | not posted (the turn after was run past the cutoff) |
| Past day 60 | not said | not said |
| The other polled rows | scored each round | the topics `ParkAdvisorMessages` lists built (below); the rest counted, `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` |
| The line's wait | the response's length and a second | the same; the length differs a run in both, as the gesture chain does |

Not in a game run: a line found by the poll inside another's wait (the test holds it). No case in either game:
the line cut off. Not heard in either game.

## The tips' line (message `0x11a`, Q317)

**Row 282, message `0x11a`:** polled, category 1 (the tutorial messages), either game mode, one copy, first
response 542, one line. Response 542 is sample 559, lip 559, the random gesture chain, model slot 0, no prop, group
383 (no tag): "if you're curious about a button or an icon just point at it with the cursor and check out the tip
that appears at the bottom of the screen". 147 polled rows stand before it in a round. The image holds no
`PUSH 0x11a` (`68 1a 01 00 00`; the same search for `0x11c` finds its three), so nothing posts or withdraws it by
a constant: the poll is its one way in.

**Its score, `0x005a0720`** (in no Ghidra function), is the control panel's listing byte for byte but its three
offsets: whole days since `mGameTick` nought, `JLE` to nought against `[ECX + 0x5e8]`, `JGE` to nought against
`[ECX + 0x5ec]`, else `[ECX + 0x5f0]`: `TutorialExplainTooltips.AfterTime`, `.CutoffTime` and `.Score`
(`lib/advisorschema.py`), 20, 80 and 100 in `Advisor.sam`. **The line is owed from tick 484 to tick 1843 of a
park**, days 21 to 79.

**What the shipped park holds.** `Easymode.TPWI`'s record of `0x11a` is (605, line 0, said 1, slaps 0), and it is
the message its last action names as playing. From the file's cursor, 277, row 282 is the fifth row scored: tick
760, then every 155 ticks. So Instant Action's Lost Kingdom, loaded by a player who has played a park before, never says it either: it scores
100 and is refused as said once, and from tick 1845 scores nought. A new player's first park clears the said bytes
first (event 10, `0x0054f24e`; not built here, Q454), so there it can be said.

### OpenTPW (`ParkAdvisorMessages.ExplainTooltips`, a second topic with `Polled` and an `AgeSection`)

A row of `ParkTopics` and nothing else: the poll, the score by the park's age, the gate, the records, the slots
and the action are Q312's to Q316's. `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` was counted for 153 rows a round then; it is counted for the polled rows no topic builds, 108 a round since Q530.

**Predicted first** (`q317/PREDICTION.txt`, `q317/confirm.py stock|control|fresh|nofile|cutoff`; twelve of
twelve, every number written before any build or run). **The stock park** (four of four): `restored message=282
line=0 lastTick=605 said=1 slaps=0`, `poll message=282 row=282 days=32 score=100 threshold=25 tick=760
result=said-once`, row 185's line on tick 877 as before, no `sample=559`, `advisor` quiet and nobody beside the
gadget four seconds in (`run-stock1/1-four-seconds-in.png`), and the gap's count 43 at a pause on tick 799, one a
sweep less the one turn of row 282. **Main's build** (the control): no poll line of 282, and 44 on tick 799.
**The stock file with the record of `0x11a` made fresh** (`parkfile.advisor_forget`; four of four): `result=post`,
`post ... result=queued pending=2` and `attempt=1 message=282 subject=0 score=100 threshold=25 response=542
sample=559 line=0 tick=760 duration=10665` on one tick; three seconds on `saying sp_559.mp2`, `playing=282`, no
tag, and him up with his antennae and no hat (`run-fresh1/1-saying-it.png`); saved under the pause on tick 782,
the record (760, 0, 1, 0), the action's duration 11,665, playing 282, no slot holding it, no other byte of thing
3 moved; loaded back, the record restored and `days=39 ... tick=915 result=said-once`, no second attempt. **A park
made with no file** (one of one): `days=6 score=0 tick=148 result=no-score`, nothing logged on its turns of ticks
303 and 458, then `days=26 score=100 tick=613 result=post` and the line, after the control panel's on tick 265.
**The Tutorial option off, and the cutoff** (two of two, on the private display): after `tutorial off` on tick
772, `tick=915 result=tutorial-off` and nothing through tick 1705; switched on, `days=80 score=0 tick=1845
result=no-score`, the score asked before the gate.

**The original** (`q317/orig/run.sh`, `run2.sh`; `advisor.py ... 0x11a,0xb9 poll`). The fresh-record file, in two
programs: the record (0, -1, 0, 0) and the cursor 277 at the load, tick 755, and **(760, 0, 1, 0) on tick 760**,
as predicted, the cursor then 283; the action's duration 11,665 in one program, this game's own number, and 12,498
in the other. Him up at the lower right with his antennae and no hat 3.3 s after the line began
(`orig/pic-g05.png`). This game's file saved as he said it (`saying-it.TPWS`): the record (760, 0, 1, 0), the
action (114877440, 11665) and the cursor 16 from the load on, tick 782, and unchanged with no slot holding
`0x11a` for 75 s.

| | The original | OpenTPW |
|---|---|---|
| Row 282 | polled; days 21 to 79 of a park; said once | the same |
| The stock park | never said: its file holds it as said on tick 605 | the same; the row's first turn on tick 760 |
| A file with the record fresh | said on tick 760 | said on tick 760 |
| The line's wait | 11,665 and 12,498 ms in two programs | 11,665 ms in each of three runs |
| Past day 79 | not read in the original | no score, said or not |

Not in a game run of the original: the Tutorial switch off at this row's turn, a park past day 79, a park made
with no file. Not heard in either game. The line's end was not seen.

## A shut park people wait outside (message 1, Q319)

**Row 1, message 1:** polled, category 0 (the same message no sooner than 120 quarter-ticks on), either game mode,
one copy, first response 2, three lines in turn. Responses 2 to 4 are samples 1 to 3, the random gesture chain,
model slot 0, prop 9 (the ticket hat), group 3. It is the first polled row of a round: from the shipped park's
cursor, 277, its turns are ticks 768, 923, 1078 and every 155 on.

**Its score, `0x0059c8d0`** (a function made in Ghidra; `ECX` the balance object, `0x00fb3540`):

1. The park open (`FUN_004c9050`, `mParkClosed` nought): nought.
2. `FUN_004ca8e0( 0, 0 )` and `FUN_004ca8e0( 2, 0 )` both under 1: nought. The routine walks the world's thing
   list and counts the things of type 3 whose item's `+0x4ac` (`Info.WhichUIType`) is the kind asked for, 0 a
   ride and 2 a sideshow (for a ride, only at the level asked for or over: the thing's byte `+0x50`, against
   nought here). So a shut park with nothing to ride is told nothing.
3. Otherwise `FUN_004c91c0()` times `[ECX + 0xa4]`, `OpenPark.ScorePerWaitingPerson` (`lib/advisorschema.py`), 20
   in `Advisor.sam`. `FUN_004c91c0` takes the two ticket booths' cells (`FUN_004d8610( 0 )` and `( 1 )`), walks
   each cell's own list of things (the cell's `+0x24`, then each thing's `+0xa`) and counts those of type 1 whose
   `mState` (`+0x220`) is 3, waiting for the opening; it leaves the count in the analyser's `+0x1f0d4` and sets
   the byte after it. A guest who has paid and one who has not are both in state 3 there.

`MinScoreForConsideration` is 25, so **one guest waiting is not enough and two are**. The tag its line leaves is
row 3 of the tag table: key 4 (it takes the place of row 2's tag, "close your park"), picture 2, string 7 ("People
want to come in but your park is closed. You should think about opening up."), the bar's first button of type
`0x18`, no second, thirty seconds.

**Row 2, message 2, `ClosePark`**, is "An open park with nothing to ride", below.

**Rows 259 to 263 are the same shape** (functions made in Ghidra): rows 260 to 262 are read and built ("Shut
shops, sideshows and toilets", below); row 259 (`0x005a02f0`) is "An open park's shut rides", below;
row 263 (`0x005a0490`) is "A shut staff room", below.

### OpenTPW (`ParkAdvisorMessages.OpenPark`, `ParkFacts`, a topic's `PerWaitingGuest`; `ParkPeople.WaitingToOpen`, `AdvisorFacts`; tag row 3; `ParkGadget.BarButton`'s type `0x18`)

A third polled topic, whose score asks the park: `ParkPeople` hands each sweep's `Tick` its `AdvisorFacts`, the
door, the things standing of a ride's item and of a sideshow's (`ParkState.ObjectsInChainOrder`, the catalogue's
`UiType`), and `WaitingToOpen`, the guests in `PeepState.WaitingForOpening` whose position's cell is either
booth's. The console's `gate` ends with `waiting N`. `ParkTags.Rows` lists row 3. **The bar's button of type
`0x18` is built** (`hud.md`, "The message bar"): it opens the entry-price screen and takes its tag off as the
erase button does.

**The original first** (`q319/PREDICTION.txt`; `q319/orig/run.sh`, `original/waiting.py` beside `advisor.py`,
`tags.py` and `bar.py`). This game's file of the stock park with its door shut on the entry-price screen on tick
787 and saved on tick 803 (`q319/scene1/shut.TPWS`): on **tick 923** the analyser's word went from -999 to 4, the
live count of state 3 on the booths' cells read 4, and a slot took message 1 at **80**. It was **said on tick
942**, not 923: the action's start had moved on tick 899 with a length of 10,832 ms, row 260's turn, and that
wait ended as tick 942 began. The record then read (942, 0, 1, 0), one tag stood at (84, 909, 154, 974) with the
mast at (94, 901, 144, 984) and string 7 word for word, and he was up in the green ticket cap
(`orig/a-told.png`). The tag clicked: the bar's data read thing 0, buttons 24 and 0, item 0 and the text, the
handle 331 to 1151 (`orig/a-bar.png`). The bar's first button clicked: a slot took `0x12f` at 1000 (the
entry-price screen's own line), the screen is in the frame (`orig/a-button.png`), the handle went back to 331 and
no tag was held 0.53 s on. **The first file twice more** (`orig/c-load.log`, `d-load.log`; my first prediction had missed the count, 5 or
6 for 4, and the tick, so both were made again before each run): the count 4 and the same four guests each time,
rows 261 and 262 on ticks 900 and 901 each time, and the record said on **tick 939** in both, where the line
begun on tick 899 read 9,998 ms long and round 1's 10,832 (a line's length is drawn). A fixed tick, 940 to 944,
missed by one; the relation held on the third run: the tick is 899 and the line's length over 252 ms a tick, and
the new action began 10,066 ms after the old. **The same scene saved past row 262's turn** (this game's file saved on tick 906,
`q319/run-fix1/shut.TPWS`; the original meets none of rows 260 to 262 before row 1): the record went from
(0, -1, 0, 0) to **(923, 0, 1, 0) on tick 923**, the analyser's word to 3 and the live count 3 (guests 29, 35
and 42), the tag on the same poll line.

**This game** (`q319/confirm.py fix|control`; five of six by the script on the first run, the sixth its own
fault: it read `advisor`'s last line where `wearing=9,0` is on the line before; six of six with the same numbers
on the final code, `run-fix2`; `side-by-side.png` holds the original's three frames over this game's). The stock park, the door shut on tick 785: the
poll of tick 768 read `score=0 ... result=no-score`; saved under a pause on tick 906 with `waiting 3`; then `poll
message=1 row=1 days=40 score=60 threshold=25 tick=923 result=post`, `post ... result=queued` and `attempt=1
message=1 subject=0 score=60 threshold=25 response=2 sample=1 line=0 tick=923` on one tick, `saying sp_001.mp2
... wearing=9,0`, him up in the green cap (`run-fix1/1-told.png`), `tags` reading `tag=3/picture=2/subject=0`
with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked (`click 56 441`): `message bar:
tag 3 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=b_entry/help=236 second=hidden
erase=mb_erase` and the text (`2-bar.png`). The button clicked (`click 265 549`): `button type 24 opens the
entry-price screen and takes tag 3 off`, `windows` listing `ParkEntryPriceScreen`, `standing=0` and `out=0` 0.7 s
on, no `TAG_BAR_BUTTON` counted (`3-screen.png`). **Main's build** (one of one, on the private display): no poll
line of message 1, no attempt, no tag at tick 941.

| | The original | OpenTPW |
|---|---|---|
| Row 1 | polled; nought with the park open or nothing to ride; 20 a guest waiting at the booths | the same |
| The same file, three waiting | queued and said on tick 923 | the same |
| The tag and the bar | row 3, part 2, string 7; button `0x18` opens the entry-price screen, tag off, arm in | the same |
| A shut park's other lines | rows 260 to 262 score, and a line of row 260's delayed row 1's by 19 ticks | the same ("Shut shops, sideshows and toilets", below) |
| Row 2, `ClosePark` | 99 in an open park with no ride and no sideshow | the same ("An open park with nothing to ride", below) |
| The count's place | left in the analyser's `+0x1f0d4` | not kept |

**Not built or not seen.** Rows 259 and 263. Lines 3 and 4 (samples 2 and 3) were in no game run, nor the
refusal inside 480 ticks, a second load of guests (a bus does come to a shut park: Q327's fresh park, tick 604 in this game; in the original only before `0x119`'s turn on 612, not read to the tick),
a park opened while he says it (whether anything withdraws message 1 was not looked for), or a tag 3 taking the place of a tag 4. The entry-price screen's own
line, `0x12f`, is not posted here (`hud.md`). Not heard in either game.

## Shut shops, sideshows and toilets (messages `0x104` to `0x106`, Q320)

**Rows 260, 261 and 262:** polled, category 0, either game mode, one copy, three lines in turn each. Their first
responses are 504, 507 and 510, samples 487 to 489 ("a lot of your shops are closed..."), 490 to 492 (the
sideshows) and 493 to 495 (the bathrooms): the random gesture chain, model slot 0, prop 9 (the ticket hat), group
383, so **a line of theirs leaves no tag**. From the shipped park's cursor, 277, their turns are ticks 899, 900 and
901, and every 155 on.

**Their scores** (`0x005a0370`, `0x005a03d0`, `0x005a0430`; `ECX` the balance object):

| Row | The things standing | Those of them shut | Over | Score |
|---|---|---|---|---|
| 260 | `FUN_004ca8e0( 1, 0 )`: things of type 3 whose item's `WhichUIType` is 1, a shop | `FUN_004ca9b0( 1 )` | `ShopsClosed.MoreThanThisPercentClosed` (`+0x588`), 25 | `ShopsClosed.Score` (`+0x58c`), 75 |
| 261 | `FUN_004ca8e0( 2, 0 )`: kind 2, a sideshow | `FUN_004ca9b0( 2 )` | `SideshowsClosed...` (`+0x590`), 25 | `SideshowsClosed.Score` (`+0x594`), 70 |
| 262 | `FUN_004c9ee0`: things of type 3 with `+0x32 & 1`, a toilet (left in the analyser's `+0x1f0e0`) | `FUN_004c9f90` | `ToiletsClosed...` (`+0x598`), 20 | `ToiletsClosed.Score` (`+0x59c`), 75 |

With none standing the score is nought. Otherwise `shut * 100 / standing`, a whole number (`IDIV`), must be more
than the bound (`JLE` to nought), and the score is the key's. **Shut is `mCanLoad` nought** (`FUN_004e0440`: the
object's `+0x68`), the flag the ride window's door and the park's door clear (`ride-operation.md`, "The park's
door"). **The park's door is not asked:** a shop shut by itself in an open park scores the same. The park's door
shuts every object a guest may be offered, so in the shipped park shut it is one shop of one, one sideshow of one
and three toilets of three, and all three rows score.

**So a shut park hears four lines.** The shops' line is said on its poll's tick, 899; the sideshows' and the
toilets' are queued on 900 and 901 and row 1's on 923, and at each wait's end the highest score waiting is said:
with four at the booths row 1's 80, then the toilets' 75, then the sideshows' 70; with three, row 1's 60 comes
last, and its score is taken again as its turn comes (`FUN_0059b620`), from whoever still waits then.

### OpenTPW (`ParkAdvisorMessages.ShopsClosed`, `SideshowsClosed`, `ToiletsClosed`, a topic's `ShutShare` and `ShutSection`; `ParkFacts.Of`; `ParkPeople.AdvisorFacts`)

Three more polled topics (`ParkAdvisorMessages.ShutTopics`), each naming the things it counts and the section its
bound is in. `ParkPeople.AdvisorFacts` counts, along the object chain, the things of a shop's item, of a
sideshow's and those with the toilet flag, and of each those whose `CanLoad` is nought. A row's poll line ends
` shut=N/M`. A waiting message whose second score is under the threshold as its turn comes is let go, as before,
and now says so: `Park advisor: dropped message=...`.

**The original first** (`q320/PREDICTION.txt`, five rounds, each written before its runs; `q320/orig/run.sh`,
`advisor.py ... 0x104,0x105,0x106,0x1 poll` beside `tags.py` and `waiting.py`). Q319's file of the stock park, its
door shut on tick 787 and saved on 803 (six of six): record `0x104` went (0, -1, 0, 0) to **(899, 0, 1, 0) on tick
899**, with no slot ever holding it; a slot took `0x105` at 70 on tick 900, `0x106` at 75 on 901 and `0x1` at 80 on
923; the records then read (943, 0, 1, 0) for row 1, **(977, 0, 1, 0) for `0x106` and (1021, 0, 1, 0) for
`0x105`**, each the tick before it plus its line's length over 252 ms, within one (10,832, 8,332 and 10,832 ms).
One tag stood in the whole run, row 1's, from tick 943 for 30.54 s. He is up in the green ticket cap with a bare
mast 1.5 s after tick 899 (`orig/a-told.png`). The rows' next turns, ticks 1054 to 1056, moved nothing.

**This game** (`q320/confirm.py fix|control|open`). The stock park, the door shut on tick 785 (`run-fix3`, on the
desktop, eight of eight): `poll message=260 row=260 days=39 score=75 threshold=25 tick=899 result=post shut=1/1`,
its post queued and `attempt=1 message=260 subject=0 score=75 threshold=25 response=504 sample=487 line=0
tick=899` on one tick; `poll message=261 ... score=70 ... tick=900 result=post shut=1/1`, queued with three
waiting messages; `poll message=262 ... score=75 ... tick=901 result=post shut=3/3`, with four; `saying sp_487.mp2`
and `wearing=9,0`, no tag, and him up in the green ticket cap beside a bare mast (`run-fix3/1-shops-line.png`).
Row 1 was queued at 60 on tick 923 with `waiting 3`. Then `attempt=2 message=262 ... response=510 sample=493
line=0 tick=943` and `attempt=3 message=261 ... response=507 sample=490 line=0 tick=987`, each within a tick of
the wait before it; `gate` read `waiting 2` from early in the third line, and `attempt=4 message=1 ... score=40
... tick=1045` left tag 3, the only tag of the run (`4-after-the-third-wait.png`). On ticks 1054 to 1056 each of
the three rows read `result=cooldown`. The count of `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` at three pauses was 96,
157 and 302 on ticks 853, 919 and 1070: each the sweeps since tick 755 less the built rows' turns by then. **Main's
build** (`run-control2`, off-screen): no poll line of the three, message 1 `attempt=1` on tick 923, and its counts
exact by the same rule with three turns fewer. **The door left open** (`run-open`, off-screen): `score=0 ...
result=no-score shut=0/1`, `0/1` and `0/3` on ticks 899 to 901, and no line.

**The original on this game's own file** (`run-fix2/shut.TPWS`, shut on tick 785 and saved on 853;
`orig/b-load.log`, three of three): `0x104` said on tick 899, `0x105` queued at 70 on 900 and `0x106` at 75 on
901; on tick 923 the analyser's word and the live count read 3 (guests 29, 35 and 42) and a slot took `0x1` at 60;
`0x106` was said on **tick 943**, `0x105` on **986**, and row 1 on **1043**, with the live count 2 since tick 1029
and one tag from that tick. This game's run that wrote the file said them on 899, 943, 984 and 1042, and its
next on 899, 943, 987 and 1045, row 1 at 40 each time.

**The predictions that missed** (three of seven held on the first game run, `run-fix1`). The count of unbuilt
rows: my arithmetic left out row 185's turn on tick 877 and row 282's second on 915; `q320/probe.py` then read the
count against the tick at seven pauses, exact each time. Row 1's line after the other three: on the first run it
was never said, and nothing in the log said why (the `dropped` line was added for it); I then predicted it would
be let go, and on the second run it was said, at 40. Made again as a relation, it is said if two or more still
wait as the third wait ends, and held on the third run and on the original's.

| | The original | OpenTPW |
|---|---|---|
| Rows 260 to 262 | polled; the key's score where more than the bound's per cent of the kind are shut | the same |
| The stock park shut | `0x104` said on tick 899; `0x105` queued on 900, `0x106` on 901 | the same |
| The same file, three waiting | said on ticks 899, 943, 986 and 1043: shops, toilets, sideshows, row 1 | 899, 943, 984 or 987, 1042 or 1045: the same order |
| Four waiting | row 1 before the toilets' (943, 977, 1021) | not in a run (the tests hold the order by score) |
| A tag | none | none |
| Row 259 | scores by the rides | built ("An open park's shut rides") |
| Row 263 | scores by `StaffroomClosed`'s counts | built ("A shut staff room", below) |

**Not built or not seen.** The toilets' count's place in the analyser (`+0x1f0e0`, and the
`FUN_0041a960` it is handed to) is not kept. In no game run: a shop, a sideshow or a toilet shut by itself in an
open park (no window shuts a shop or a toilet here: the tests hold the score), a share between nought and the
whole, each row's second and third lines, a row said again past its 120 quarter-ticks, four waiting in this game.
What the first run did with row 1's slot is not known. The date on the gadget is not the same in the two games'
frames (the calendar's, Q149). Not heard in either game.

## An open park with nothing to ride (message 2, Q321)

**Row 2, message 2, `ClosePark`:** polled, category 0, either game mode, one copy, first response 5, three lines
in turn. Responses 5 to 7 are samples 4 to 6 ("there isn't much to do in your park yet, you might want to close it
until you've built some more stuff" and two longer ways of saying it): the random gesture chain, model slot 0, prop
9 (the ticket hat), group 4. It is the poll's second row of a round: from the shipped park's cursor, 277, its turns
are ticks 769, 924, 1079 and every 155 on.

**Its score, `0x0059c960`** (`ECX` the balance object): with the park open (`FUN_004c9050` answering other than
nought, the test row 1 leaves on) and `FUN_004ca8e0( 0, 0 )` and `FUN_004ca8e0( 2, 0 )` both under 1, neither a
ride nor a sideshow standing, `[ECX + 0xa8]`, `ClosePark.Score` (`lib/advisorschema.py`), 99 in `Advisor.sam`;
otherwise nought. Nobody is counted: shops, toilets and whoever waits or walks about change nothing.

**Its tag** is row 4 of the tag table: key 3 (it takes the place of row 1's tag, as that takes its), picture 2,
string 8 ("There isn't much to do in your park yet. You might want to close it until you've built some more
things."), the bar's first button of type `0x18`, no second, thirty seconds.

### OpenTPW (`ParkAdvisorMessages.ClosePark`, a topic's `NothingToRide`; tag row 4)

A seventh polled topic, scored from the same `ParkFacts` as row 1: its key's value where the door is open and the
facts count no ride and no sideshow. Its poll line ends ` closed=N rides=N sideshows=N`. `ParkTags.Rows` lists row
4; its bar's button is row 3's, built in Q319.

**The original first** (`q321/PREDICTION.txt`; `q321/orig/run.sh`, `advisor.py ... 0x2,0x1,0x12f,0x11c poll`
beside `tags.py` and `bar.py`). This game's file of the stock park, open, with the Belly Bounce and the Jungle
Spray sold under a pause on tick 917 and saved there (`q321/scene1/bare.TPWS`), twice (`orig/a-load.log`,
`b-load.log`; five of six, then two of two): the record went from (0, -1, 0, 0) to **(924, 0, 1, 0) on tick 924**,
with no slot ever holding it; on the same poll line one tag stood at (84, 909, 154, 974) with the mast at (94, 901,
144, 984) and string 8 word for word; six seconds after the load he is up in the green ticket cap
(`orig/b-line-6s.png`; the first run's frames came after the line, so that prediction was run again). The tag
clicked: the bar's data read thing 0, buttons 24 and 0, item 0 and the text, the handle 331 to 1151
(`orig/a-bar.png`). The bar's first button clicked: a slot took `0x12f` at 1000, the handle went back to 331 and
tag 4 was gone. The record never moved again through tick 1089, past the row's next turn on 1079.

**Not predicted: the original says a second line.** On tick 959, row 62's turn, a slot took message `0x3e` at 73,
and it was said on tick 968 in both runs, as row 2's wait ended, in the hard hat (`orig/a-2-loaded.png`), leaving a
second tag above the first ("Your park has no rides in it. Perhaps now is a good time to build some?"). Row 62 is
"A park with no ride in it", below.

**This game** (`q321/confirm.py fix|control|ride`; eight of eight on the first run, `run-fix1`, on the desktop).
The stock park left open, the two sold under a pause on tick 916: `poll message=2 row=2 days=33 score=0
threshold=25 tick=769 result=no-score closed=0 rides=1 sideshows=1` before the sale, then `poll message=2 row=2
days=40 score=99 threshold=25 tick=924 result=post closed=0 rides=0 sideshows=0`, the post queued and `attempt=1
message=2 subject=0 score=99 threshold=25 response=5 sample=4 line=0 tick=924` on one tick; `saying sp_004.mp2 ...
wearing=9,0`, him up in the green ticket cap over a park with neither (`run-fix1/1-told.png`), `tags` reading
`tag=4/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked:
`message bar: tag 4 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=b_entry/help=236 second=hidden
erase=mb_erase` and the text (`2-bar.png`), the bar's own line `0x11c` queued behind his. The button clicked:
`button type 24 opens the entry-price screen and takes tag 4 off`, `windows` listing `ParkEntryPriceScreen`,
`standing=0`, `withdraw message=284 cleared=1 cut=0` (`3-screen.png`). No other line by tick 1000, and `poll
message=2 ... score=99 ... tick=1079 result=cooldown`. The count of `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` under the
sale's pause was 153, the rule's (`TOOLING.md`, recipes) with tick 769 a built turn. **Main's build**
(`run-control`, off-screen): no poll line of message 2, no attempt, no tag by tick 960, the count one more. **The
Bounce left standing** (`run-ride`, off-screen): tick 769's poll line alone, no attempt.

| | The original | OpenTPW |
|---|---|---|
| Row 2 | polled; `ClosePark.Score` in an open park with no ride and no sideshow | the same |
| The same scene | said on tick 924, line 0, in the ticket hat | the same |
| The tag and the bar | row 4, part 2, string 8; button `0x18` opens the entry-price screen, tag off, arm in | the same |
| The line after it | row 62's, `0x3e` at 73, queued on tick 959 and said on 968 with a tag of its own | the same, said as row 2's wait ends ("A park with no ride in it", below) |

**Not built or not seen.** In no game run: lines 2 and 3 (samples 5 and 6; the test holds the turn), the
line said again past its 120 quarter-ticks, a tag 4 taking a tag 3's place or the other way, the park shut while he
speaks (whether anything withdraws message 2 was not looked for), a ride or a
sideshow sold by the player's own window. The original was not given a file with one of the two left standing. Not
heard in either game.

## A park with no ride in it (message `0x3e`, Q322)

**Row 62, message `0x3e`, `RidesBuildRides`:** polled, category 0, either game mode, one copy, first response
182, three lines in turn. Responses 182 to 184 are samples 179 to 181: the random gesture chain, model slot 0, prop
5 (the hard hat), group 64. It is the poll's 35th row of a round: from the shipped park's cursor, 277, its turns
are ticks 804, 959, 1114 and every 155 on.

**Its score, `0x0059dad0`** (a function made in Ghidra; `ECX` the balance object):

1. `FUN_004ca7b0( 0, 1 )` on the park analyser (`FUN_00519510`) other than nought: nought. The routine (in no
   function of the listing; read as disassembly) walks the catalogue's items (`FUN_00412e60`, `FUN_00412e70` on
   `0x007890a0`) and counts those whose `Info.WhichUIType` (`+0x4ac`) is the kind asked for, 0 a ride, and whose
   control record (`FUN_004d3d80`; `hud.md`, "What the buy list actually filters on") counts at least one standing
   (`+0x18` above nought); its second argument, 1 here, leaves the item's `+0xc4`, its `GoldenTicketCost`,
   unasked. So one ride of any item standing is enough for nought. The record's `+0x18` is the object
   constructor's and destructor's (`0x004db6d0`, `0x004dd0e1`): the runs below have it fall to nought when the
   park's one ride is sold.
2. Otherwise `[ECX + 0x230]` plus `[ECX + 0x234]` times the park's age in whole days (`FUN_004f8800` over
   `0xC92A69C000`, as the control panel's line counts them): `RidesBuildRides.Score` and
   `RidesBuildRides.AdditionalPerDay` (`lib/advisorschema.py`), **-50 and 3** in `Advisor.sam`, whose own note
   reads "This is negative so that it won't start nagging you straight away".

The door is not asked, nor the sideshows, the shops or anybody. A day is 23.04 ticks and
`MinScoreForConsideration` 25, so **a park with no ride is told from its 26th day on** (28; the 25th day's 25 is
not above 25): from tick 600. On tick 959 it is 73, on tick 1114 it is 94.

**Its tag** is row 64 of the tag table: key 383 (it takes no tag's place), picture 2, string 67, the bar's first
button of type 9, no second, thirty seconds. **Button types 9 to `0xc`** are `FUN_004acc70( type - 9, item )`,
the buy screen's opener: with the screen open already it fills the list with that tab's items (`FUN_004aaf70`) and
puts the tab's button down (`0x1fb`, `0x1fd`, `0x1fa`, `0x1fc`); otherwise it builds the screen on that tab; either
way, where the item is not nought it selects that item's row (`FUN_00664d2a`, `FUN_00665739`), and the fresh
build posts the screen's own line, `0xbc`. The act answers 2: the tag is taken off and the arm folded away.

**String 67 differs between the two language folders.** `Language/English/TAG_SYSTEM.str` reads "Perhaps now's a
good time to build some?" and `Language/american`'s, which the reference install reads (`saves.md`, "The list is the
folder"), "Perhaps now is a good time". `strdump` prints both. OpenTPW reads the English folder.

### OpenTPW (`ParkAdvisorMessages.BuildRides`, a topic's `PerDayWithNoRide`; tag row 64; `ParkGadget.BarButton`'s types 9 to `0xc`)

An eighth polled topic, scored from the same `ParkFacts` as rows 1 and 2: where the facts count no ride, its
key's value plus the per-day key's for each day of `DaysSinceTheFirstSweep`. The facts count the ride things
standing, where the original counts the ride items with one standing: the same nought. Its poll line ends
` rides=N`. `ParkTags.Rows` lists row 64. The bar's buttons of types 9 to `0xc` open `ParkBuyScreen` on the tab
the type names (`openOn`), or pick it on the one open (`PickTab`), and take the tag off as the entry-price
button does; a tag that names an item counts `TAG_BAR_BUY_ITEM_ROW` (no listed row does). The console's
`windows` names the buy screen's tab.

**The original first** (`q322/PREDICTION.txt`, round 1; `q322/orig/run.sh <file>`, `advisor.py ...
0x3e,0x2,0xbc,0x11c poll` beside `tags.py` and `bar.py`), on two files of this game's it had not been given,
eight of eight. *The Belly Bounce alone sold* under a pause on tick 916, the Jungle Spray standing
(`q322/scene-a/ride-sold.TPWS`; `orig/a-load.log`): message 2's record never moved; `0x3e`'s went from (0, -1, 0,
0) to **(959, 0, 1, 0) on tick 959** with no slot seen holding it, one tag with it at (84, 909, 154, 974) and the
mast at (94, 901, 144, 984); half a second into the line he is up in the yellow hard hat (`orig/a-2-loaded.png`,
`loadfile.sh`'s own frame: mine was taken too soon). The tag clicked: the bar's data read thing 0, buttons 9 and
0, item 0 and the text. The button clicked on tick 1060: "Buy Ride", the rides tab down, four rides listed
(`orig/a-button.png`), no tag held by tick 1062, and a slot took `0xbc` at 1000, said on tick 1061. *Both sold
on tick 1074*, past row 62's turn on 959 (`scene-b/late.TPWS`; `orig/b-load.log`): row 2's line on tick 1079; **on
tick 1114 a slot held `0x3e` at 94**, the score's number for the park's 48th day; said on tick 1123 as row 2's
wait ended (length 10832), a second tag at (84, 834, 154, 899) and the mast up to 826.

**This game** (`q322/confirm.py ride|both|spray|control`). *The Bounce alone sold* (`run-ride2`, on the desktop,
six of six; `run-ride1` four of six, both misses the prediction's: the census scores a topic on the tick it is
asked, 76 on the 42nd day, and the bar's words are the English folder's): `poll message=62 row=62 days=34 score=0
threshold=25 tick=804 result=no-score rides=1` before the sale, then `poll message=62 row=62 days=41 score=73
threshold=25 tick=959 result=post rides=0`, the post queued and `attempt=1 message=62 subject=0 score=73
threshold=25 response=182 sample=179 line=0 tick=959` on one tick; `saying sp_179.mp2 ... wearing=5,0`, him up in
the yellow hard hat (`run-ride2/1-told.png`), `tags` reading `tag=64/picture=2/subject=0` with
`mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 64 subject 0 item 0
bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=521 second=hidden erase=mb_erase` and the text
(`2-bar.png`). The button clicked, the buy screen having been left on its shops tab (`windows` read `ParkBuyScreen
tab 1`): `button type 9 opens the buy screen on tab 0 and takes tag 64 off`, `windows` reading `ParkBuyScreen tab
0`, `standing=0`, neither gap counted, and the frame "Buy Ride" with the original's four rides (`3-screen.png`).
`PARK_ADVISOR_POLLED_ROW_NOT_BUILT` under the sale's pause on tick 917 was 153, the rule's (`TOOLING.md`, recipes)
with tick 804 a built turn. *Both sold* (`run-both`, off-screen): row 2's line on tick 924 (duration 8165), `0x3e`
queued at 73 on tick 959 and said on tick 961, the tick row 2's wait ended, then two tags, tag 4 at
(84,909,154,974) and tag 64 at (84,834,154,899), the mast (94,826,144,984). *The Spray alone sold* (`run-spray`,
off-screen): tick 804's poll line alone, no attempt. **Main's build** (`run-control`, off-screen): no poll line of
message 62, no attempt, no tag by tick 1000, the count one more.

| | The original | OpenTPW |
|---|---|---|
| Row 62 | polled; nought with a ride standing, else -50 and 3 a day of the park's age | the same |
| The ride sold, the sideshow left | said on tick 959, line 0, in the hard hat | the same |
| After row 2's line | queued on its turn, said as row 2's wait ends, a second tag above tag 4 | the same (968 and 961: the wait is the drawn line's) |
| The tag and the bar | row 64, part 2, string 67; button 9 opens the buy screen on its rides tab, tag off, arm in | the same |
| The buy screen's own line | `0xbc` posted at 1000 as the screen is built, and said | the same ("The buy screen's own line") |
| The ride count | the items' control records' `+0x18` | the ride things standing |

**Not built or not seen.** The row of a tag's item selected on the buy screen
(`TAG_BAR_BUY_ITEM_ROW`). In no game run: lines 2 and 3 (samples 180 and 181; the test holds the turn), the line
said again past its 120 quarter-ticks, button types `0xa` to `0xc` and the button with the buy screen open (the test
holds `0xb` and the open screen), a ride bought after the line. Not heard in either game.

## Worn rides and no mechanic (message 5, Q331)

**Row 5, message 5, `StaffHireMechanics1`:** polled, category 0, either game mode, one copy, first response 14,
three lines in turn. Responses 14 to 16 are samples 13 to 15: the random gesture chain, model slot 0, prop 5 (the
hard hat), group 7. It is the poll's fifth row of a round: from the shipped park's cursor, 277, its turns are
ticks 772, 927, 1082 and every 155 on.

**Its score, `0x0059ca30`** (a function made in Ghidra; `ECX` the balance object):

1. `FUN_004c9960( 4 )` on the park analyser (`FUN_00519510`) above nought: nought. The routine walks the thing
   list and counts the things whose type byte (`+2`) is the one asked for, of the five staff kinds, and whose
   `+3` is nought (`park-gate.md`: a fired member is marked there until it is taken out). Type 4 is the mechanic.
2. Otherwise `0x004ca3a0( [ECX + 0xc0] )` times `[ECX + 0xbc]`: `StaffHireMechanics1.PoorerStateThan` and
   `StaffHireMechanics1.ScorePerWornRide` (`lib/advisorschema.py`), **25 and 5** in `Advisor.sam`. The routine
   (in no function of the listing; read as disassembly) walks the object chain (the world's `+0x1da746`, each
   object's `+0xc`) and, of the objects whose item's `Info.WhichUIType` (`+0x4ac`) is nought, a ride, counts
   each whose State of repair (`+0x44`) cut to a whole number and its low byte is **under** the limit, and each
   that is broken down whatever its repair (`FUN_004e0370`: `mState` 1), once.

The door is not asked, nor whether a ride is open. `MinScoreForConsideration` is 25, so **five worn rides are not
enough and six are** (30).

**Its tag** is row 7 of the tag table: key 383 (it takes no tag's place), picture 2, string 11, the bar's first
button of type 14, no second, thirty seconds. String 11 reads the same in both language folders: "Your rides are
starting to wear out but you don't have any mechanics. You should think about hiring some."

**Button types `0xd` to `0x11`** are `FUN_0049bdd0( type - 0xd )`, the hire screen's opener ("The hire screen's
own line"): the tabs in order are the cleaners', the mechanics', the entertainers', the guards' and the
scientists'. The opener first destroys the park screen that is open, the hire screen too, so the screen is
always built again on the tab asked for and posts its own line, `0xbd`. The act answers 2: the tag is taken off
and the arm folded away. Fifteen rows of the tag table carry one: rows 7 to 9 and 152 and 154 type 14, rows 10
to 12 type 13, rows 13 to 15 type 16, row 16 type 15, rows 17 to 19 type 17 (`lib/tagtable.py`).

**The two rows after it.** Row 6 (`0x0059ca80`, message 6, tag 8): "Broken rides and no mechanic", below. Row 7
(`0x0059cac0`, message 7, tag 9): "More rides than the mechanics can keep", below.

### OpenTPW (`ParkAdvisorMessages.HireMechanics`, a topic's `PerWornRideWithNoMechanic`, `WornBelow`; `ParkFacts.Mechanics`, `RidesWorn`; tag row 7; `ParkGadget.BarButton`'s types `0xd` to `0x11`; `ParkHireScreen`'s `openOn`)

A twelfth polled topic, scored from the same `ParkFacts`: where the facts count no mechanic, its key's value for
each ride they count worn. `ParkPeople.AdvisorFacts` counts the staff list's mechanics (a fired member leaves
that list at once) and, of the ride things, each whose State of repair's whole number is under the advisor's
`WornBelow` or whose state is 1. Its poll line ends ` worn=N mechanics=N`. `ParkTags.Rows` lists row 7. The
bar's buttons of types `0xd` to `0x11` open a new `ParkHireScreen` on the tab the type names (`openOn`; the
stack closes the park screen that is open) and take the tag off as the buy buttons do. The console's `windows`
names the hire screen's tab.

**The scene** (`q331/scene.py fired|kept`, `q331/worn.py`): the stock park paused on tick 915, mechanic 26 fired
(`fired`), a Crazy Ape on (41,22), an Aztec Mayhem on (57,23) and three more Crazy Apes on (70,40), (30,60) and
(60,60) bought as things 43 to 47 and left shut, saved; then the six rides' State of repair written into the
file (the float at an object's record's `+1074`): `worn6.TPWS` six at 24, `worn5.TPWS` five at 24 and thing 47
at 25, `kept6.TPWS` the mechanic kept and six at 24.

**Predicted first** (`q331/PREDICTION.txt`), both games on files neither had been given.

**This game** (`q331/confirm.py fix|five|kept|control`). *`worn6`* (`run-fix1`, on the desktop, six of six on its
first run): `poll message=5 row=5 days=40 score=30 threshold=25 tick=927 result=post worn=6 mechanics=0`, `post
message=5 subject=0 score=30 threshold=25 tick=927 result=queued pending=2` and `attempt=1 message=5 subject=0
score=30 threshold=25 response=14 sample=13 line=0 tick=927 duration=7332`; `saying sp_013.mp2 ... wearing=5,0`,
him up in the yellow hard hat (`1-line.png`), `tags` reading `tag=7/picture=2/subject=0` with
`mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 7 subject 0 item 0
bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=526 second=hidden erase=mb_erase` and the text
(`2-bar.png`). The button clicked, in a program whose hire screen had never been opened: `button type 14 opens
the hire screen on tab 1 and takes tag 7 off`, `windows` reading `ParkHireScreen tab 1 ... left on 2,4,10`,
`standing=0`, `withdraw message=284 cleared=1 cut=0`, `post message=189 ... result=queued`, no `TAG_BAR_BUTTON`
counted, and the frame "Hire Mechanics" with the second tab down (`3-screen.png`). *`worn5`* (`run-five`,
off-screen): `score=25 ... result=under-threshold worn=5 mechanics=0`, no attempt and no tag on tick 950.
*`kept6`* (`run-kept`, off-screen): `score=0 ... result=no-score worn=6 mechanics=1`, no attempt, no tag.
**Main's build** on `worn6` (`run-control`, off-screen): no line of message 5, no attempt, no tag by tick 950.

**The original** (`q331/orig/run.sh <file>`, `advisor.py ... 0x5,0x6,0x7,0xbd,0x11c poll` beside `tags.py`,
`bar.py` and `screens.py`; four programs' loads). *`worn6`* (runs a and b): message 5's record went from (0, -1,
0, 0) to **(927, 0, 1, 0) on tick 927** in both, one tag with it at (84, 909, 154, 974), the mast at (94, 901,
144, 984), its text string 11; four and a half seconds after the load's click he is up in the yellow hard hat
(`orig/b-g4.png`). The tag clicked: the bar's data read thing 0, buttons 14 and 0, item 0 and the text
(`orig/a-bar.png`). The button clicked: `screens.py` went from `left 1,3,10 ... hire 0x0 hiretab 0` to `left
2,3,10 screen 0x46c1ec0 ... hire 0x46c1ec0 hiretab 1` on tick 1050, the frame "Hire Mechanics" with the second
tab down (`orig/a-button.png`), a slot took `0xbd` at 1000 and it was said on tick 1051. *`worn5`* (run c) and
*`kept6`* (run d): message 5's record fresh through tick 1053 in each. **Row 7 did not speak in any of the
four**, where I had predicted, from a part-read listing and marked as the weakest, that six rides and no
mechanic would have it queued at 60 on tick 929: its days are counted from the analyser's `mLastStaffHired`
("More rides than the mechanics can keep"). The prediction was made again from that (fresh in runs b, c and d
too) and held.

| | The original | OpenTPW |
|---|---|---|
| Row 5 | polled; nought with a mechanic, else 5 for each ride under 25 or broken down | the same |
| Six rides at 24, no mechanic | said on tick 927, line 0, in the hard hat | the same |
| Five at 24 and one at 25 | not said | the same (`under-threshold`) |
| Six at 24, the mechanic kept | not said | the same (`no-score`) |
| The tag and the bar | row 7, part 2, string 11; button 14 builds the hire screen on its mechanics' tab, tag off, arm in | the same |
| The hire screen's own line | `0xbd` posted at 1000 as the screen is built, and said | the same (said on tick 961, as row 5's wait ended) |
| Rows 6 and 7 | scored on their turns (nought here) | each built since, in its own section below |

**Not built or not seen.** The last staff row, 17, is not built (rows 8 to 16 are,
below). In no game run: lines 2 and 3 (samples 14 and 15; the test holds the
turn), the line said again past its 120 quarter-ticks, a broken ride counted (the test holds it, and a ride
breaking with no mechanic posts `0x44` besides), a ride worn under 25 by running, button types `0xd` and `0xf`
to `0x11` and the button with the hire screen open (the test holds each), a mechanic hired from the screen after
the line, Full Simulation. In this game's run the tag was clicked while he still spoke and in the original's
after: the bar's own line, `0x11c`, was said there (tick 1033) and withdrawn unsaid here. Not heard in either
game.

## Broken rides and no mechanic (message 6, Q332)

**Row 6, message 6, `StaffHireMechanics2`:** polled, category 0, either game mode, one copy, first response 17,
three lines in turn. Responses 17 to 19 are samples 16 to 18, model slot 0, prop 5 (the hard hat), group 8; the
first and third are the random gesture chain and the second, response 18, names gesture value 21, row 5 (the caller takes `0x10` off). It
is the row after row 5: from the shipped park's cursor its turns are ticks 773, 928, 1083 and every 155 on.

**Its score, `0x0059ca80`** (loose code, read as disassembly; `ECX` the balance object):

1. `FUN_004c9960( 4 )` on the park analyser above nought, a mechanic in the park: nought, as row 5's.
2. Otherwise `0x004ca310` on the analyser times `[ECX + 0xc4]`, `StaffHireMechanics2.ScorePerBrokenRide`
   (`lib/advisorschema.py`), **10** in `Advisor.sam`. The routine (loose code) walks the object chain as
   `0x004ca3a0` does and counts each object whose item's `Info.WhichUIType` (`+0x4ac`) is nought, a ride, and
   which `FUN_004e0370` answers for: `mState` 1, broken down. The State of repair is not read.

The door is not asked. `MinScoreForConsideration` is 25, so **two broken rides are not enough and three are**
(30). Row 5 counts the same three at 5 each, 15, and is not said for them.

**Its tag** is row 8 of the tag table: key 383, picture 2, string 12, the bar's first button of type 14 (the hire
screen on its mechanics' tab, "Worn rides and no mechanic"), no second, thirty seconds. String 12 reads the same
in both language folders: "The rides are breaking down but you have no mechanics. You should hire some!"

### OpenTPW (`ParkAdvisorMessages.HireMechanicsBroken`, a topic's `PerBrokenRideWithNoMechanic`; `ParkFacts.RidesBroken`; tag row 8; `PollFacts`)

A thirteenth polled topic, scored from the same `ParkFacts`: where the facts count no mechanic, its key's value
for each ride they count broken. `ParkPeople.AdvisorFacts` counts, of the ride things, each whose state is 1.
Its poll line ends ` broken=N mechanics=N` (`PollFacts` makes every polled row's ending). `ParkTags.Rows` lists
row 8. The second line plays its gesture row, row 5 (Q596), as the mechanic's third plays row 10.

**The scene** (`q332/scene.py <out> <bin> <name> <N>`): a ride bought and never opened does not break when its
script is told to, so the three broken rides are three open ones. The stock park, mechanic 26 fired; two Crazy
Apes bought and left shut, so that row 259's line is said on tick 898 and is inside its cooldown afterwards,
then sold; a Hot Pot on (41,23) and an Aztec Mayhem on (57,23) bought, joined and ridden; past tick 1086 the
Belly Bounce, the pot and the Mayhem broken by their scripts (`VAR_BREAKSTAT`), the first break's line
(`0x44`) left to end, and the park saved under a pause on tick 1231, seven ticks before row 6's turn.
`broken3.TPWS` holds three broken rides, no tag and message 6 never said; `broken2.TPWS` two, saved on tick 1230.

**Predicted first** (`q332/PREDICTION.txt`), both games on files neither had been given. One thing was seen
before the predictions were written, and the file says so: while the scene was being found, this game's build
polled row 6 at 30 in a live park and said response 17.

**This game** (`q332/confirm.py fix|two|control`). *`broken3`* (`run-fix1`, on the desktop, six of six on its
first run; `run-fix2` on the commit's build): `poll message=5 row=5 days=53 score=15 threshold=25 tick=1237
result=under-threshold worn=3 mechanics=0`, `poll message=6 row=6 days=53 score=30 threshold=25 tick=1238
result=post broken=3 mechanics=0`, `post message=6 subject=0 score=30 threshold=25 tick=1238 result=queued
pending=2` and `attempt=1 message=6 subject=0 score=30 threshold=25 response=17 sample=16 line=0 tick=1238
duration=7332`; `saying sp_016.mp2 ... wearing=5,0`, him up in the yellow hard hat (`1-line.png`), `tags`
reading `tag=8/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag
clicked: `message bar: tag 8 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=526
second=hidden erase=mb_erase` and the text (`2-bar.png`). The button clicked: `button type 14 opens the hire
screen on tab 1 and takes tag 8 off`, `windows` reading `ParkHireScreen tab 1 ... left on 2,4,10`,
`standing=0`, and the frame "Hire Mechanics" (`3-screen.png`). *`broken2`* (`run-two`, off-screen): `score=20
... result=under-threshold broken=2 mechanics=0` on tick 1238, no attempt and no tag on tick 1273. **Main's
build** on `broken3` (`run-control`, off-screen): no line of message 6, no attempt, no tag by tick 1273.

**The original** (`q332/orig/run.sh <file>`, `advisor.py ... 0x5,0x6,0x7,0x44,0x103,0xbd,0x11c poll` beside
`tags.py`, `bar.py` and `screens.py`). *`broken3`* (run a): message 6's record went from (0, -1, 0, 0) to
**(1238, 0, 1, 0) on tick 1238**, its wait 8,331 ms where this game's is 8,332, one tag with it at (84, 909,
154, 974), the mast at (94, 901, 144, 984), its text string 12; three and six seconds after the load's click he
is up in the yellow hard hat with the tag on the mast (`orig/a-g2.png`, `a-g4.png`). The tag clicked: the bar's
data read thing 0, buttons 14 and 0, item 0 and the text. The button clicked: `screens.py` went from `left
1,3,10 ... hire 0x0 hiretab 0` to `left 2,3,10 ... hire 0x47be440 hiretab 1` on tick 1370, the frame "Hire
Mechanics" with the second tab down (`orig/a-button.png`). Message 5's record stayed fresh throughout, and no
other message took a slot before tick 1238. *`broken2`* (run b, a fresh program): message 6's record fresh
through tick 1394.

**Seen in the original and not predicted.** In both runs it said a line on tick 1317 that this game does not
build: the poll's cursor stood on row 125 after it, so row 124's (`0x0059f220`, responses 300 and 301, sample
334, "the buzz about your park has started to die down"; its score is not read, and its two responses name
model slot 1 and prop -1, the theme's own advisor and costume). And row 7 took a slot at 30
on tick 1394 in run a and was said on tick 1447, after the hire screen's line; in run b it was said on tick
1394 itself ("More rides than the mechanics can keep", below).

| | The original | OpenTPW |
|---|---|---|
| Row 6 | polled; nought with a mechanic, else 10 for each ride broken down | the same |
| Three rides broken, no mechanic | said on tick 1238, line 0, in the hard hat | the same |
| Two broken | not said | the same (`under-threshold`) |
| The tag and the bar | row 8, part 2, string 12; button 14 builds the hire screen on its mechanics' tab, tag off | the same |
| Row 7, later in that park | nought on tick 1239, said on tick 1394 | the same, since Q333 (`q333/confirm.py b3`) |
| Row 124, later in that park | said (tick 1317) | not built: counted with the other polled rows |

**Not built or not seen.** Row 124. In no game run: lines 2 and 3 (samples 17 and 18; the test holds the
turn and the lookup of the second's gesture row, and a seam test that the row reaches the speaker's call (Q596); said by its topic in no run, only through `sayas`), the line said again past its 120
quarter-ticks, a mechanic kept (he mends the rides before a turn; the test holds `no-score`) or hired from the
screen after the line, a broken sideshow beside the rides (the test holds it), Full Simulation: the mode is not
asked by row 6's score. A second file loaded into the original with the hire screen left open did not load
(the first run b); it was run again in a fresh program. Not heard in either game.

## More rides than the mechanics can keep (message 7, Q333)

**Row 7, message 7, `StaffHireMechanics3`:** polled, category 0, either game mode, one copy, first response 20,
three lines in turn. Responses 20 to 22 are **samples 20 to 22** (the bank's sample 19 is no response's of
these rows, so the samples are not one less than the responses as rows 5 and 6's are), the random gesture chain,
model slot 0, prop 5 (the hard hat), group 9. It is the row after row 6: from the shipped park's cursor its turns
are ticks 774, 929, 1084 and every 155 on.

**Its score, `0x0059cac0`** (loose code, read as disassembly; `ECX` the balance object):

1. `FUN_004c90d0` on the park analyser under `[ECX + 0x630]`, `WaitingTimes.DaysForStaff`, **30** in
   `Advisor.sam` (a signed `JGE`): nought.
2. Otherwise the rides, `FUN_004ca8e0( 0, 0 )`, less the mechanics, `FUN_004c9960( 4 )`, times `[ECX + 0xc8]`,
   `StaffHireMechanics3.IdealNumberOfRidesPerMechanic`, **4**; nought unless that is above nought, and then it
   times `[ECX + 0xcc]`, `StaffHireMechanics3.ScorePerExtraRide`, **10**.

The door is not asked, nor whether a ride is open, worn or broken. `MinScoreForConsideration` is 25, so **three
rides over what the mechanics keep are enough and two are not**: with no mechanic three rides, with one seven.

**The days are counted from the newest hire.** `FUN_004c90d0` (loose code) is the calendar's now (`FUN_004f8690`
on world `+0x2a0`) less the analyser's `+0x21c38` (`FUN_005fc550`), over 864,000,000,000 (`0x0067c320`), the
low dword answered. The analyser's serialiser `FUN_004c5d70` names `+0x21c38` **`mLastStaffHired`**: the last of
six eight-byte stamps at the record's end (`mLastStrike` `+0x21c10`, `mLastPuke`, `mLastPrank`,
`mParkLastOpened`, `mLastObjectBuilt` `+0x21c30`, then it), before `mMostPaidForTicket`, `mLargestBusLoad` and
`mLongestStay`; in a park file it is the eight bytes at 73,524 of thing 5's record (the FileFormats saves page,
"The park analyser"). Two things write it: the analyser's constructor `FUN_004c58c0`, which stamps all six with
the calendar's now as the park is made, and `FUN_004c76d0`, whose one caller is the staff constructor's tail
`FUN_00504b90`, the routine that stamps the member's own `mTimeHired` (`saves.md`). A load makes every thing
again, each by its constructor and then its record, and **the stamp left standing after a load is the file's**,
not the load's own now: the original's memory read the file's after each of four loads (`thingwords.py ...
5:0x21c38:Q`; in which order the load's staff and its analyser are made is not read).

**Measured in files**, nine the original wrote (`q333/tail.py`): `mLastStaffHired` equals the newest `mTimeHired`
of the file's staff in all eight that hold staff, Alexah's Full Simulation parks with their own hires among
them, and is the calendar's start in the ninth, saved on tick 4 with nobody hired. The shipped park's is
2000-01-29 03:00, its researcher's, tick 648 at 3,750 calendar seconds a tick. A day is 23.04 ticks, so **692
ticks after a hire is the thirtieth day and 691 is the twenty-ninth**: the shipped park's row 7 can score from
tick 1340, which is why Q332's park heard nought on tick 1239 and 30 on tick 1394.

**Its tag** is row 9 of the tag table: key 383, picture 2, string 13, the bar's first button of type 14 (the hire
screen on its mechanics' tab, "Worn rides and no mechanic"), no second, thirty seconds. String 13 reads the same
in both language folders: "There aren't enough mechanics to service all the rides. Maybe you should think about
hiring some more."

### OpenTPW (`ParkAdvisorMessages.HireMoreMechanics`, a topic's `RidesAMechanic`, `DaysForStaff`; `ParkFacts.DaysSinceStaffHired`; `ParkState.LastStaffHired`, `StampStaffHired`, `DaysSinceStaffHired`; `ParkWorld.LastStaffHired`, `PutLastStaffHired`; tag row 9)

A fourteenth polled topic, scored from the same `ParkFacts`, which now carry the whole days since the newest
hire. `ParkState` keeps the stamp: the file's at a load (`ParkWorld.LastStaffHired`), the calendar's start in a
park made with no file, and the calendar's now at every hire (`ParkPeople.Hire` calls `StampStaffHired`, which
logs `Park: mLastStaffHired stamped <date> on mGameTick N`). The writer puts it back into the analyser's record
(`ParkFileWriter.Running.LastStaffHired`); the rest of the analyser is still the file's. The poll line ends
` rides=N mechanics=N sincehire=N`. `ParkTags.Rows` lists row 9.

**The scene** (`q333/scene.py`, `q333/mk.py`): the stock park paused on tick 915, mechanic 26 fired, two Crazy
Apes bought on (41,22) and (70,40) as things 43 and 44 and left shut, saved: `base.TPWS`, three rides, no
mechanic, the shipped stamp. From it, the stamp written into the file (`parkfile.thing_set( src, dst, 5, 73524,
'<q', filetime )`): `days30.TPWS` the stamp of tick 237, 692 ticks before row 7's turn on tick 929, and
`days29.TPWS` the stamp of tick 238. And `hired.TPWS`, written by this game: `days30` loaded, a guard hired
under the load's pause on tick 916, saved (`q333/confirm.py mkhired`).

**Predicted first** (`q333/PREDICTION.txt`), both games on files neither had been given; every prediction held,
in one round. Not a prediction, and said there: the original's two answers on `q332/broken3.TPWS`, seen in Q332.

**This game** (`q333/confirm.py fix|d29|base|hired|b3|control|mkhired`). *`days30`* (`run-fix1`, on the desktop,
six of six on its first run): `poll message=7 row=7 days=40 score=30 threshold=25 tick=929 result=post rides=3
mechanics=0 sincehire=30`, `post message=7 subject=0 score=30 threshold=25 tick=929 result=queued pending=2` and
`attempt=1 message=7 subject=0 score=30 threshold=25 response=20 sample=20 line=0 tick=929`; `saying sp_020.mp2
... wearing=5,0`, him up in the yellow hard hat (`1-line.png`), `tags` reading `tag=9/picture=2/subject=0` with
`mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 9 subject 0 item 0
bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=526 second=hidden erase=mb_erase` and the text
(`2-bar.png`). The button clicked: `button type 14 opens the hire screen on tab 1 and takes tag 9 off`,
`windows` reading `ParkHireScreen tab 1`, `standing=0`, and the frame "Hire Mechanics" (`3-screen.png`).
*`days29`* (`run-d29`, off-screen): `score=0 ... tick=929 result=no-score rides=3 mechanics=0 sincehire=29`, then
`days=47 score=30 ... tick=1084 result=post ... sincehire=36`, queued on that tick and said on tick 1094, as
row 259's line ended. *`base`* (`run-base`): `no-score ... sincehire=12` on tick 929, nothing by tick 962.
*`mkhired`* (`run-mkhired`): `hire: 'Scott Newman-Bale' is thing 45`, `Park: mLastStaffHired stamped 9/2/2000
18:10:00 on mGameTick 916`, and the file's analyser's stamp that `FILETIME`, the new member's own `mTimeHired`,
its other five stamps and three dwords the file's. *`hired`* (`run-hired`): the clock starts on 916 and
`no-score ... sincehire=0` on tick 929. *`q332/broken3.TPWS`* (`run-b3`): `no-score ... sincehire=25` on tick
1239 and `days=60 score=30 ... tick=1394 result=post ... sincehire=32`, said on that tick. **Main's build** on
`days30` (`run-control`): no line of message 7, no attempt, no tag by tick 962.

**The original** (`q333/orig/run.sh <file>`, `advisor.py ... 0x5,0x6,0x7,0x71,0x103,0xbd,0x11c poll` beside
`thingwords.py ... 5:0x21c38:Q:lasthire`, `tags.py`, `bar.py` and `screens.py`; two programs, four loads).
*`days30`* (run a): the analyser's stamp read 125920471500000000, the file's; message 7's record went from (0,
-1, 0, 0) to **(929, 0, 1, 0) on tick 929**, one tag with it at (84, 909, 154, 974), the mast at (94, 901, 144,
984), its text string 13; six seconds after the load's click he is up in the yellow hard hat with the tag on the
mast (`orig/a-g4.png`). The tag clicked: the bar's data read thing 0, buttons 14 and 0, item 0 and the text
(`orig/a-bar.png`). The button clicked: `screens.py` went from `left 1,3,10 ... hire 0x0 hiretab 0` to `left
2,3,10 ... hire 0x47bb3c0 hiretab 1` on tick 1050, the frame "Hire Mechanics" with the second tab down
(`orig/a-button.png`). *`days29`* (run b, a fresh program): the stamp 125920509000000000; message 7's record
fresh on tick 929; **a slot took message 7 at 30 on tick 1084** and it was said on tick 1096. *`base`* (run c):
the stamp 125935884000000000, the shipped one; record fresh and no slot through tick 1053. *`hired`* (run d):
**the stamp 125945934000000000, tick 916's, this game's own**; record fresh and no slot through tick 1053. In
all four it said row 113's line on tick 996 and, where the run reached it, row 259's on tick 1053, as this game
does.

| | The original | OpenTPW |
|---|---|---|
| Row 7 | polled; nought until 30 days after the newest hire, then 10 for each ride over four a mechanic | the same |
| Three rides, no mechanic, day 30 | said on tick 929, line 0, in the hard hat | the same |
| Day 29 | nought on tick 929; a slot at 30 on tick 1084, said on 1096 | the same, said on 1094 |
| The shipped stamp (day 12) | not said | the same (`no-score`) |
| A file saved after a hire here | reads this game's stamp; not said | the same (`sincehire=0`) |
| The tag and the bar | row 9, part 2, string 13; button 14 builds the hire screen on its mechanics' tab, tag off | the same |
| The analyser's other stamps | `mParkLastOpened` at each open request; strike, puke and prank never move | `mLastObjectBuilt` kept and written ("The newest build", below), `mParkLastOpened` too (`hud.md`, "The analyser's other stamps"); the other three the file's |

**Not built or not seen.** `mParkLastOpened` is kept (Q397); `mLastStrike`, `mLastPuke` and `mLastPrank` never move
in the original and go out as the file's (`hud.md`, "The analyser's other stamps"). `mLargestBusLoad` and
`mLongestStay` are not kept here and go out as the file's; what reads each is not read. `mLifetimeVisitors` and `mMostPaidForTicket` are kept (Q389; `hud.md`, "The lifetime visitors"). `mLastObjectBuilt` is "The newest build", below. Whether a staff candidate the original makes for the hire
screen's preview, or anything but a hire, runs the staff constructor is not read (the eight files agree with a
hire alone). In no game run: lines 2 and 3 (samples 21 and 22; the test holds the turn), the line said again past
its 120 quarter-ticks, a mechanic kept (three rides are under his four: the test holds seven rides and one
mechanic, and eleven and two), a mechanic hired from the screen after the line (the stamp then silences the row
for thirty days, the test's last step), a hire made in the original by hand, a park made with no file, Full
Simulation: the mode is not asked by row 7's score. This game said the line of `days29` two ticks before the
original (1094, 1096): the wait after row 259's line, not measured further. Not heard in either game.

## Litter and no handyman (messages 8 and 9, Q417)

**Rows 8 and 9, messages 8 and 9, `StaffHireHandymen1` and `2`:** both polled, category 0, either game mode (`+0x0c`
2), one copy, three lines in turn (selection 2). Row 8's responses are 23 to 25, row 9's 26 to 28, **samples the
same**, the random gesture chain, model slot 0, **prop 7, the handymen's cap**, groups 10 and 11. From the shipped
park's cursor their turns are two and one before row 10's: **ticks 775 and 776**, then every 155 on. The transcripts
(`global-speech-transcripts.tsv`): row 8 "yak i just stepped in something unspeakable the park is getting messy and you
don't have any janitors maybe you should hire some", "this place is becoming a mess and you don't have any janitors
have you thought about hiring some", "the park is getting pretty dirty and you don't have anybody to clean it up maybe
you should hire some janitors"; row 9 "the park is filthy and you don't have any janitors you should hire some", "this
park is a mess i'm talking dirty ... the park is a mess and you should hire some janitors", "there aren't any janitors
in this place is a mess you need to hire some".

**Their scores, `0x0059cb40` and `0x0059cbb0`** (undefined bytes; read with Ghidra's `PseudoDisassembler`, nothing
written to the project; `ECX` the balance object, kept in `ESI`):

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. The handymen, `FUN_004c9960( 5 )` on the analyser: above nought (`JLE` passes nought), nought.
3. The litter share, `FUN_004ccdf0` on the analyser (below), L.
4. **Row 8:** L over `[ESI + 0xd8]`, `StaffHireHandymen1.LitterPointCutoffHigh`, **25** (`JLE` passes 25), nought;
   otherwise `[ESI + 0xd0]`, `ScorePerLitterPoint`, **3**, times L, less `[ESI + 0xd4]`, `LitterPointCutoffLow`, **3**.
   **Row 9:** L under `[ESI + 0xe0]`, `StaffHireHandymen2.LitterPointCutoffLow`, **26** (`JGE` passes 26), nought;
   otherwise `[ESI + 0xdc]`, its `ScorePerLitterPoint`, **3**, times L, less the same 26. Nothing holds row 8's at
   nought: a share of nought scores -3, which the poll does not take.

`MinScoreForConsideration` is 25, so **row 8 speaks from a share of 10 (27) to 25 (72), and row 9 from 26 (52) on**;
the two never score together. The keys' names are the file's: row 8's "low" cut-off is taken off, not compared.

`FUN_004ccdf0`, `FUN_004c81b0` and `FUN_00508380` are named by address: each is a call target in the listing that begins at a function prologue, and the Ghidra copy of 2026-10-22 held no function at any of them (`citecheck.py`, Q646).

**The litter share, `FUN_004ccdf0`**, walks every cell of the map (`[0x0080239c] + 0x2d8 + (y x 128 + x) x 0x44`, x and
y from 127 down). A cell is **counted** where `FUN_00536310` (`mType` 1, path) or `FUN_00536320` (3 or 9) passes **or**
its `mLitter` (`+0x2c`) is not nought, and **littered** where `mLitter` is not nought, whatever its kind; the answer is
littered x 100 over counted (`IDIV`, cut towards nought), nought with none counted. **Only rows 8 and 9 call it** (`0x0059cb84`, `0x0059cbf4`: every `CALL` rel32 of `.text`, scanned on disk). The shipped park counts **90** (78
of path, 4 of queue, 8 of type 9) and holds no litter. A littered cell carries the sprite handle its drop made
(`FUN_004d93b0` writes it to `+0x28`; the file's `mLitterScript`); the share does not read it.

**Their tags** are rows 10 and 11 of the tag table: key 383, picture 2, strings **14** and **15**, the bar's first
button of type 13 (the hire screen on its cleaners' tab), no second, thirty seconds. `English`: "You've no cleaners and
the park is starting to get untidy. Maybe you should hire some?" and "You've no cleaners and the park is very untidy.
You should hire some!"; `american` the same with "You have no janitors".

### OpenTPW (`ParkAdvisorMessages.HireHandymen`, `HireHandymenFilthy`, a topic's `LitterWithNoHandyman`, `Filthy`; `ParkState.LitterShare`; `ParkFacts.LitterShare`; tag rows 10 and 11)

`ParkState.LitterShare` is `FUN_004ccdf0` over the running cells' litter (the file's, as nothing here drops or sweeps
it: the guests' drop and the handyman's litter search, `HANDYMAN_LITTER_SEARCH`, are not built). The handymen are the
kind's members rows 28 to 32 already read. The poll line ends ` closed=N handymen=N litter=N`. `ParkTags.Rows` lists rows
10 and 11.

**The scenes** (`q417/mkscenes.sh`, `lib/stockscene.py`, `parkfile.litter_set`): the stock park paused on tick 768
with its handyman, thing 25, dismissed (769 kept), saved, then litter of kind 1 written on the first N path cells
(`mLitter`, the sprite handle left nought, so neither game draws it): `messy.TPWS` 12 cells, a share of 13; `filthy.TPWS`
27, 30; `tidy.TPWS` 8, 8; `swept.TPWS` 27 with the handyman kept (`parkfile.py -L`).

**Predicted first** (`q417/PREDICTION.txt`). Every prediction of either game held. Void, mine: one run of this game
(the checker took the library's name `score`), one of the original (no process at the poll's start) and one whose
pollers logged no tick; the original's stills 2 s after the line missed the advisor, and its first clicks came past the
tag's thirty seconds: filmed and clicked again.

**This game** (`q417/confirm.py fix8|fix9|tidy|swept|control8|control9`, on the desktop): *`messy`* (`run-fix8b`):
`poll message=8 row=8 days=33 score=36 threshold=25 tick=775 result=post closed=0 handymen=0 litter=13`, said on that
tick (`attempt=1 message=8 ... response=23 sample=23 line=0 tick=775`), `saying sp_023.mp2 ... wearing=7,0`, the tag on
the aerial (`1-line.png`); on 776 message 9 `score=0 ... result=no-score ... litter=13`. The tag clicked: `message bar:
tag 10 ... first=mb_buy/help=525 second=hidden erase=mb_erase` and string 14 (`2-bar.png`). The button: `button type 13
opens the hire screen on tab 0 and takes tag 10 off`, `ParkHireScreen tab 0`, "Hire Cleaners" (`3-screen.png`).
*`filthy`* (`run-fix91`): on 775 message 8 `score=0 ... no-score ... litter=30`; on 776 `poll message=9 row=9 days=33
score=64 threshold=25 tick=776 result=post closed=0 handymen=0 litter=30`, sample 26, tag 11, string 15, the cleaners'
tab. *`tidy`* (`run-tidy1`): `score=21 ... tick=775 result=under-threshold ... litter=8`, silent to 814. *`swept`*
(`run-swept1`): both `no-score closed=0 handymen=1 litter=30`. **Main's build** on `messy` and `filthy`: no line of
messages 8 or 9, no tag, to 814 and 811.

**The original** (`q417/orig/run.sh <file>`, `advisor.py ... 0x8,0x9,0xa poll` beside `tags.py`, `bar.py`,
`screens.py` and the new `litter.py`, which reads its own share; a fresh program each, off-screen). `litter.py` read
**walked 90 and the file's share** (13, 30, 8, 30) at each load. *`messy`* (runs a, a2): message 8's record from (0, -1,
0, 0) to **(775, 0, 1, 0) on tick 775**, 9's unmoved, one tag lettered string 14 ("You have no janitors..."), him in the
handymen's cap (`orig/a2-film-cap.png`, filmed); the tag clicked, the bar's buttons 13 and 0; the button, `hiretab 0`,
"Hire Janitors" (`orig/a-button.png`). *`filthy`* (runs b, b2): 9's record to **(776, 0, 1, 0) on 776**, 8's unmoved,
string 15, the cap (`orig/b-film-cap.png`), buttons 13 and 0, `hiretab 0`. *`tidy`* (run c3) and *`swept`* (run d): both
records unmoved to ticks 1151 and 1143, no tag. In `swept` **the original's handyman swept the litter**, a cell every 15
or 16 ticks from 877 (27 cells to 12 by 1143), which this game's does not. `q417/side-by-side.png` sets the two lines
side by side.

| | The original | OpenTPW |
|---|---|---|
| Rows 8 and 9 | polled; nought shut or with a handyman; 3 a point of the litter share less 3 up to 25, and 3 a point less 26 from 26 | the same |
| The litter share | cells littered over cells path, queue, type 9 or littered, x 100, cut | the same |
| 13, no handyman | row 8 said on 775, line 0, in the handymen's cap | the same |
| 30, no handyman | row 9 said on 776; row 8 not | the same |
| 8, no handyman | neither said | the same (`under-threshold`, 21) |
| 30, one handyman | neither said; he sweeps the litter | neither said; he does not sweep |
| The tags and the bar | rows 10 and 11, part 2, strings 14 and 15; button 13 builds the hire screen on its cleaners' tab | the same |

**Not built or not seen.** In no game run: lines 2 and 3 (samples 24, 25, 27 and 28; the test holds the turn), a share
of nought (-3, tested only), a share over 25 under row 8 or under 26 under row 9 at the cut-offs themselves (tested
only), litter dropped or swept in play (neither is built here), two handymen or more, a handyman hired after the line,
Full Simulation (not asked: the rows are either mode's), the other three themes. Not heard in either game. The guests'
drop (`FUN_004d93b0`) and the handyman's litter search, which move the share in the original, are counted gaps; until
they are built the share here is the file's.

## A park too big for its handymen (message `0xa`, Q411)

**Row 10, message `0xa`, `StaffHireHandymen3`:** polled, category 0, either game mode, one copy, first response 29,
three lines in turn. Responses 29 to 31 are **samples 29 to 31**, the random gesture chain, model slot 0, prop 7 (the
handymen's cap), group 12. From the shipped park's cursor its turns are three after row 7's: ticks 777, 932 and every
155 on. The transcripts (`global-speech-transcripts.tsv`): "your janitors are working hard but there aren't enough for
a park this size have you considered hiring more", "you could use more janitors and a park this big...", "you don't
have enough janitors to handle the park the size and things are getting a little out of control...".

**Its score, `0x0059cc20`** (loose code, read as disassembly; `ECX` the balance object, kept in `EDI`):

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. The handymen, `FUN_004c9960( 5 )` on the park analyser (`FUN_00519510`): none, nought.
3. The developed cells, `FUN_004c81b0( 0 )` on the analyser, under `[EDI + 0xec]`,
   `StaffHireHandymen3.MinCellsForConsideration`, **300** (a signed `JGE` passes 300): nought.
4. The cells less the handymen times `[EDI + 0xe4]`, `IdealDevelopedCellsPerHandyman`, **400**: nought unless that is
   above nought, and then it times `[EDI + 0xe8]`, `ScorePerExtra10Cells`, **2** (`0x0059ccab`). **Nothing divides by
   ten**, whatever the key's name: every cell over counts 2.

`MinScoreForConsideration` is 25, so **thirteen cells over what the handymen keep are enough and twelve are not**: with
one handyman 413 developed cells. The minimum asks nothing a handyman's 400 does not already ask, at the shipped values.

**The developed cells, `FUN_004c81b0( age )`**, walk the analyser's 1,024 region records (`+0x20` to `+0x1efa4`, `0x7c`
bytes each, the loop from the last down) and add `FUN_004c8c40( age )` of each. The constructor `FUN_004c58c0` sets a
region four cells square (`+0xc`), 32 by 32 of them (`+0x10`, `+0x14`) over a map of 128 (`+0x18`, `+0x1c`), and
`FUN_004c7a90` gives region k the cells (k % 32 x 4, k / 32 x 4), four by four (`+0x0` to `+0xc`): **the regions tile
the whole map, inside the park and out**. `FUN_004c8c40` counts the cells of its rectangle (`[0x008023a0] + (y x 128 +
x) x 0x44`) for which any of `FUN_00536310` (`mType` 1, path), `FUN_00536320` (3 or 9), `FUN_00536340` (9),
`FUN_00536350` (10), `FUN_005d60c0` (answers nought whatever the cell) or `FUN_00536380` (4, a footprint) answers:
**a cell of type 1, 3, 4, 9 or 10**. It keeps the count at `+0x68`, with a stamp of `mGameTick` at `+0x64` and a
set byte at `+0x6c` (`FUN_0041a960`), and answers the kept count while the quarter ticks since the stamp
(`FUN_0041a990`) are no more than `age`; row 10 asks with nought, so only the same quarter tick reuses one. The shipped
park has **126**: 78 of path, 4 of queue, 35 of footprint, 8 of type 9, one of type 10 (`q411/cells.py`).

**Its tag** is row 12 of the tag table: key 383, picture 2, string 16, the bar's first button of type 13 (the hire
screen on its cleaners' tab, "Worn rides and no mechanic"), no second, thirty seconds. String 16 reads "Your park is
filthy because you don't have enough cleaners. You should hire some more."

### OpenTPW (`ParkAdvisorMessages.HireMoreHandymen`, a topic's `CellsAHandyman`; `ParkFacts.DevelopedCells`; `ParkState.DevelopedCells`, `IsDeveloped`; tag row 12)

A topic scored from the same `ParkFacts`, which now carry the developed cells: `ParkState.DevelopedCells` walks the
whole map's records and counts the five types, afresh at each ask where the original may reuse a count within the
same quarter tick. The handymen are the kind's members rows 28 to 32 already read (`KindTraining`, `FUN_004c9960`).
The poll line ends ` closed=N handymen=N cells=N`. `ParkTags.Rows` lists row 12.

**The scenes** (`q411/mkscene.py`): the stock park paused on tick 765 and the first N cells of the 17 x 17 block of
bare grass at (60,15) laid as path with the console's `path`, saved: `big.TPWS` 289 cells, 415 developed with its one
handyman; `under.TPWS` 286, 412 (`parkfile.py -d`).

**Predicted first** (`q411/PREDICTION.txt`), both games on files neither had been given. One miss, mine: I carried
Q333's help row of the type-14 button (526) to the type-13 one, which reads 525; predicted again and held.

**This game** (`q411/confirm.py fix|under|control`; `run-fix2` the final run, on the desktop): *`big`*: `poll message=10
row=10 days=33 score=30 threshold=25 tick=777 result=post closed=0 handymen=1 cells=415`, queued, and `attempt=1
message=10 subject=0 score=30 threshold=25 response=29 sample=29 line=0 tick=777`; `saying sp_029.mp2 ... wearing=7,0`,
him up in the handymen's cap (`1-line.png`), `tags` reading `tag=12/picture=2/subject=0` on the mast at
`94,901,144,984`. The tag clicked: `message bar: tag 12 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ...
first=mb_buy/help=525 second=hidden erase=mb_erase` and string 16 (`2-bar.png`). The button clicked: `button type 13
opens the hire screen on tab 0 and takes tag 12 off`, `windows` reading `ParkHireScreen tab 0`, the frame "Hire
Cleaners" (`3-screen.png`). *`under`* (`run-under1`): `score=24 ... tick=777 result=under-threshold ... cells=412`, no
attempt, no tag by tick 812. **Main's build** on `big` (`run-control1`): no line of message 10, no attempt, no tag by
tick 811.

**The original** (`q411/orig/run.sh <file>`, `advisor.py ... 0x7,0xa,0x71,0x103,0xbd,0x11c poll` beside `tags.py`,
`bar.py` and `screens.py`; a fresh program each). *`big`* (runs a and a2): message `0xa`'s record went from (0, -1, 0,
0) to **(777, 0, 1, 0) on tick 777**, one tag at (84, 909, 154, 974) on the mast at (94, 901, 144, 984), its text string
16 of `Language/american`, "Your park is filthy because you don't have enough janitors. You should hire some more."
(this game reads `English`, whose string 16 says "cleaners"); him up in the handymen's cap with the tag on the aerial
(`orig/a2-g4.png`). The tag clicked: the bar's data thing 0, buttons 13 and 0, item 0 and the text. The button clicked:
`screens.py` went to `left 2,3,10 ... hire 0x46b89c0 hiretab 0` on tick 899, the frame "Hire Janitors" with the first
tab lit (`orig/a-button.png`). *`under`* (run b): no change of message `0xa`'s record, the slots or the tags in 200
seconds after the load, the park run to 27 February 2000 (about tick 1313, past row 10's turns 777, 932, 1087 and
1242), no tag (`orig/b-end.png`). `q411/side-by-side.png` sets this game's line beside the original's.

| | The original | OpenTPW |
|---|---|---|
| Row 10 | polled; nought shut, with no handyman, under 300 cells; then 2 for every developed cell over 400 a handyman | the same |
| 415 cells, one handyman | said on tick 777, line 0, in the handymen's cap | the same |
| 412 cells | not said | the same (`under-threshold`, 24) |
| The developed cells | the whole map's cells of type 1, 3, 4, 9 and 10, a region's count reused within its quarter tick | the same, counted afresh |
| The tag and the bar | row 12, part 2, string 16; button 13 builds the hire screen on its cleaners' tab, tag off | the same |

**Not built or not seen.** In no game run: lines 2 and 3 (samples 30 and 31; the test holds the turn), the line said
again past its 120 quarter-ticks, two handymen or more (the test holds three), a handyman hired after the line, the
minimum of 300 (it asks nothing a handyman's 400 does not, at the shipped values: the test holds it with 100 a
handyman), a park made with no file, Full Simulation: the mode is not asked by row 10's score. Not heard in either
game. Rows 12 and 13 are built since ("A park with no guard", "A park too big for its guards", below); the last staff
row, 17, is not (Q420; rows 8, 9, 11 and 14 to 16 are built, "Litter and no handyman", "A busy park with no guard", "A busy park with no entertainer", "Most or every kind of ride built, and no researcher").

## A busy park with no guard (message `0xb`, Q416)

**Row 11, message `0xb`, `StaffHireGuards1`:** polled, **category 9**, either game mode (the row's `+0x0c` is 2), one
copy, first response 32, three lines in turn (selection 2). Category 9 is `MessageGroups[9]`, "Stuff to be said only
once": `MinTimeSameMessage` 0, **`SayOnlyOnce` 1**, `DiscardAfterSlaps` 0, so once said the accept gate refuses it for
good (the message's said byte `+0xe8`; a withdraw that cuts him off clears it). Responses 32 to 34 are **samples 32 to
34**, the random gesture chain, model slot 0, prop 6 (the guards' cap), group 13. From the shipped park's cursor its
turns are one before row 12's: ticks 778, 933, 1088, 1243, 1398, 1553 and every 155 on. The transcripts
(`global-speech-transcripts.tsv`): "pardon me excuse me ... it's getting busy you know you don't have any guards do you
think you should hire some", "sorry i'm late the traffic is murder the parks really getting busy and you don't have any
guards maybe you should hire some", "the park is getting busy and you don't have any guards maybe you should hire some".

**Its score, `0x0059ccb0`** (undefined bytes; read with Ghidra's `PseudoDisassembler`, nothing written to the project;
`ECX` the balance object, kept in `ESI`):

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. **The days since the newest hire**, `FUN_004c90d0` on the analyser, under `[ESI + 0x630]`,
   `WaitingTimes.DaysForStaff`, **30** (a signed `JGE`): nought. Rows 7 and 13 ask the same.
3. The guards, `FUN_004c9960( 7 )` on the analyser: above nought (`JLE` past), nought.
4. **The crowd**, `FUN_004c81e0` (half the guests inside, from nought to 100; the music's level, `ParkAudio`), less
   `[ESI + 0xf0]`, `StaffHireGuards1.AtLeastThisBusy`, **70**, times `[ESI + 0xf4]`, `ScorePerExtraBusyPoint`, **1**
   (`0x0059cd1e`). **Nothing holds it at nought**.

`MinScoreForConsideration` is 25, so **a crowd of 96, 192 guests inside, is enough and 95 is not**, with no guard, 30
days after the newest hire. The most it can score is 30, a crowd of 100. The shipped park holds one guard (thing 28) and
its 13 guests stand outside the gate, so it scores nought; its newest hire is the researcher's, tick 648, whose 30 days
come on tick 1340, so **the first turn that can score is 1398**.

**Its tag** is row 13 of the tag table: key 383, picture 2, string 17, the bar's first button of type 16 (the hire
screen on its guards' tab, the fourth), no second, thirty seconds. String 17 reads "The park is getting busy and you
don't have any guards. Maybe you should hire some?" in both language folders.

### OpenTPW (`ParkAdvisorMessages.HireGuardsBusy`, a topic's `BusyWithNoGuard`, `SaidOnce`; tag row 13)

A topic scored from the same `ParkFacts` as rows 13 and 14: `DaysSinceStaffHired` (rows 7 and 13), the guards (rows 28
to 32's members) and `GuestsInside` with `ParkAudio.CrowdLevel` (row 14's crowd). Its category is `SaidOnce`, 9, the
first built row of it; the accept gate's said-once refusal is the tutorial rows' (`Refusal`). The poll line ends
` closed=N guards=N inside=N crowd=N sincehire=N`. `ParkTags.Rows` lists row 13.

**The scenes** (`q416/mkscenes.py`, `q416/mkguard.sh`, both on `lib/parkrun.py` off-screen): the stock park paused on
tick 1236, then guests admitted half on (52,26) and half on (52,34) and saved (`parkfile.py -g`): `under.TPWS` the guard
dismissed (`fire 28`) and 168 admitted, 178 inside, a crowd of 89; `busy.TPWS` 32 more, 210 inside, a crowd of 100;
`guard.TPWS` the guard kept and 200 admitted, 212 inside. The newest hire's 30 days come on tick 1340, so the turn on
1243 scores nought in all three and the one on 1398 is the test.

**Predicted first** (`q416/PREDICTION.txt`), both games on files neither had been given. All held. One run of this game
was void, mine (`run-fix1`: the script waited on a pattern where the harness takes plain words; its log, read, held).

**This game** (`q416/confirm.py fix|under|guard|control`, on the desktop; `run-fix2` the final run): *`busy`*: `poll
message=11 row=11 days=53 score=0 threshold=25 tick=1243 result=no-score closed=0 guards=0 inside=210 crowd=100
sincehire=25`, then `poll message=11 row=11 days=60 score=30 threshold=25 tick=1398 result=post closed=0 guards=0
inside=212 crowd=100 sincehire=32`, queued and said on that tick (`attempt=1 message=11 subject=0 score=30 threshold=25
response=32 sample=32 line=0 tick=1398`, `saying sp_032.mp2 ... wearing=6,0`), him in the guards' cap with the tag on
the aerial (`1-line.png`). The tag clicked: `message bar: tag 13 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453`,
`first=mb_buy/help=528 second=hidden erase=mb_erase` and string 17 (`2-bar.png`). The button clicked: `button type 16
opens the hire screen on tab 3 and takes tag 13 off`, `windows` reading `ParkHireScreen tab 3`, "Hire Guards"
(`3-screen.png`). **On 1553 the row scored 30 again and the gate refused it**: `... tick=1553 result=said-once ...
inside=219 crowd=100 sincehire=39`, one attempt of message 11 in the run (the run's other, `attempt=2 message=189` on 1452, is the hire
screen's own line), the census `message=11/score=30/line=0/lastTick=1398/said=1
/slaps=0`. *`under`* (`run-under1`): on 1398 `... score=22 threshold=25 tick=1398 result=under-threshold closed=0
guards=0 inside=184 crowd=92 sincehire=32` (six walked in), silent to tick 1571. *`guard`* (`run-guard1`): on 1243 `...
result=no-score closed=0 guards=1 inside=212 crowd=100 sincehire=25`, no later line (the poll writes a changed answer
only), the census `message=11/score=0/line=-1/lastTick=0/said=0/slaps=0`, silent to 1573. **Main's build** on `busy`
(`run-control1`): no line of message 11, no tag 13, to 1573.

**The original** (`q416/orig/run.sh <file>`, `advisor.py ... 0xb,0xc,0xd,0x108 poll` beside `tags.py`, `bar.py`,
`screens.py` and `inside.py`; a fresh program each, off-screen). *`busy`* (run a): `inside 210 ... crowd 100` after the
load, **message `0xb`'s record from (0, -1, 0, 0) to (1398, 0, 1, 0) on tick 1398**, one tag at (84, 909, 154, 974)
lettered string 17, him in the guards' cap (`orig/a-told.png`; filmed, `shots/q416a.mkv`). The tag clicked: the bar's
thing 0, buttons 16 and 0, item 0 and the text. The button clicked: `hiretab 3` on tick 1468, "Hire Guards" with the
fourth tab lit (`orig/a-button.png`). **The record stood at (1398, 0, 1, 0) to tick 2212**, the last record read (`inside.py` ran to 2270):
said once, shown by the turns of 1553 and 1708, which came with the crowd at 100 and would have scored 30 (the
later turns' crowd, 93 down to 69, could not pass 25). *`under`* (run b): `inside 180 ... crowd 90` on 1398, `0xb`'s record unmoved to tick 1979, no tag of string 17. *`guard`* (run c): `inside 211 ... crowd 100` on 1398, unmoved to 2092, no tag. `q416/side-by-side.png` sets this game's line beside the original's.

| | The original | OpenTPW |
|---|---|---|
| Row 11 | polled, category 9; nought shut, before 30 days since a hire, with a guard; then the crowd less 70 | the same |
| 210 inside, no guard | said on tick 1398, line 0, in the guards' cap | the same |
| The next turns | not said again (said once) | the same (`said-once`) |
| 178 inside, no guard | not said (a crowd near 90) | the same (`under-threshold`, 22) |
| 212 inside, one guard | not said | the same (`no-score`, `guards=1`) |
| The tag and the bar | row 13, part 2, string 17; button 16 builds the hire screen on its guards' tab, tag off | the same |

**Not built or not seen.** In no game run: lines 2 and 3 (samples 33 and 34; said once, the original never reaches
them but by a withdraw that cuts the first; the test holds the turn with the key off), a crowd past 100 or under 70
(tested only), a guard hired after the line, the said byte kept in a file and loaded (the save's histories are
written and read as every row's), Full Simulation (not asked: the row is either mode's), the other three themes. Not
heard in either game. The guards' list behind the button held one candidate here and four in the original: the pool's
top-up, not this row (Q356's note).

## A park with no guard (message `0xc`, Q413)

**Row 12, message `0xc`, `StaffHireGuards2`:** polled, category 0, either game mode (the row's `+0x0c` is 2), one
copy, first response 35, three lines in turn. Responses 35 to 37 are **samples 35 to 37**, model slot 0, prop 6 (the
guards' cap), group 14; the first two take the random gesture chain and the third names gesture value 22, row 6 (the caller takes `0x10` off, `0x0059939f`; played, Q596). From the shipped park's cursor its turns are one before row
13's: ticks 779, 934, 1089 and every 155 on. The transcripts (`global-speech-transcripts.tsv`): "the park doesn't
have any guards and some of the visitors are getting out of hand, you should hire some soon", "if you don't knock it
off right now I'll turn this park around... some of the visitors are misbehaving and you don't have any guards...",
"stop that right now, you don't have any guards and the visitors are misbehaving...".

**Its score, `0x0059cd30`** (undefined bytes; read here with Ghidra's `PseudoDisassembler`, nothing written to the
project; `ECX` the balance object, kept in `ESI`):

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. The guards, `FUN_004c9960( 7 )` on the analyser: above nought (`JLE` past), nought.
3. **This month's misbehaving kids**, `FUN_004c94a0( 0 )` on the analyser (`+0x21bfc`, caught and not caught alike;
   `hud.md`, "The misbehaving kids"), times `[ESI + 0xfc]`, `AdditionalPerMisbehavingKid`, **5**, plus `[ESI + 0xf8]`,
   `StaffHireGuards2.Score`, **10** (`0x0059cd89`).

No days since a hire and no cells are asked. `MinScoreForConsideration` is 25, so **a park with no guard is told only
with four kids or more this month**: three score 25, which the poll does not take as above it. The analyser's one
adder of the kids is the guard's own chase (`FUN_004d6790`), so in the original a park with no guard counts none
either: the count reaches row 12 from a park file, or from a guard dismissed after a chase that month. The close of a
month zeroes it.

**Its tag** is row 14 of the tag table: key 383, picture 2, string 18, the bar's first button of type 16 (the hire
screen on its guards' tab), no second, thirty seconds. String 18 reads "The park doesn't have any guards and some of
the visitors are misbehaving. You should hire some soon!" (`Language/american`'s is the same).

### OpenTPW (`ParkAdvisorMessages.HireGuards`, a topic's `KidsWithNoGuard`; `ParkFacts.MisbehavingKids`; tag row 14)

A topic scored from the same `ParkFacts`, which now carry this month's kids (`ParkState.MisbehavingKids.Month`, the
file's at a load since Q395). The guards are the kind's members rows 28 to 32 already read. The poll line ends
` closed=N guards=N kids=N`. `ParkTags.Rows` lists row 14. Nothing here counts a kid (the chase waits on the pranks,
`DECIDE_PRANK_*`), so in this game too the count comes from the file.

**The scenes** (`q413/mkkids.py`): `noguard.TPWS`, the stock park paused on tick 765 with its guard dismissed
(`lib/pathscene.py ... 760 0 -- "fire 28"`), then the kids written with `parkfile.thing_set` (thing 5, `+73468`):
`four.TPWS` 4, 0, 0; `three.TPWS` 3, 9, 0 (nine got away, which the row does not read); `guard.TPWS` the shipped file
with its guard and 4, 0, 0.

**Predicted first** (`q413/PREDICTION.txt`), both games on files neither had been given. Missed, both mine and in the
original's harness: row 264's line (below) predicted said on 903 where my own click had opened the hire screen on
899, whose line was being said, so it was said on 975 (posted on 903, as predicted; held in runs b and c); and him in
the cap, which the stills came too late for (filmed in run a2, held).

**This game** (`q413/confirm.py fix|under|guard|control`, on the desktop; `run-fix1` the final run): *`four`*:
`money` reading `misbehaving kids 4 got away 0 ever 0`, then `poll message=12 row=12 days=33 score=30 threshold=25
tick=779 result=post closed=0 guards=0 kids=4`, queued and said on that tick (`attempt=1 message=12 subject=0
score=30 threshold=25 response=35 sample=35 line=0 tick=779`), `saying sp_035.mp2 ... wearing=6,0`, him in the guards'
cap with the tag on the aerial (`1-line.png`). The tag clicked: `message bar: tag 14 subject 0 item 0 bar: out=1
rect=486,1112,1101,1453`, `first=mb_buy/help=528 second=hidden erase=mb_erase` and string 18 (`2-bar.png`). The
button clicked: `button type 16 opens the hire screen on tab 3 and takes tag 14 off`, `windows` reading
`ParkHireScreen tab 3`, "Hire Guards" (`3-screen.png`). *`three`* (`run-under1`): `... score=25 threshold=25 tick=779
result=under-threshold closed=0 guards=0 kids=3`, no other line of row 12 to tick 962, the census
`message=12/score=25/line=-1/lastTick=0/said=0/slaps=0`. *`guard`* (`run-guard1`): `... score=0 ... result=no-score
closed=0 guards=1 kids=4`, the same silence. **Main's build** on `four` (`run-control1`): no line of message 12 to
tick 961.

**The original** (`q413/orig/run.sh <file>`, `advisor.py ... 0xa,0xc,0xd,0x108 poll` beside `tags.py`, `bar.py` and
`screens.py`; a fresh program each, off-screen). *`four`* (runs a and a2): **message `0xc`'s record from (0, -1, 0, 0)
to (779, 0, 1, 0) on tick 779**, one tag at (84, 909, 154, 974) lettered string 18; filmed with `record.sh`, him in the
guards' cap from the load on (`orig/a2-told.png`, film 3.00 s). The tag clicked: the bar's thing 0, buttons 16 and 0,
item 0 and the text. The button clicked: `hire 0x46ba750 hiretab 3` on tick 899, "Hire Guards" with the fourth tab lit
(`orig/a-button.png`). *`three`* (run b) and *`guard`* (run c): `0xc`'s record unmoved, no tag of string 18, the
park run to 20 March (`b-end.png`, `c-end.png`). **In all three files the original also posts message `0x108` on tick
903** (row 264, `LotsOfPranks`, 100 for more than two kids this month; said on 903 in b and c), built since ("Lots of
pranks", below). `q413/side-by-side.png` sets this game's line beside the original's.

| | The original | OpenTPW |
|---|---|---|
| Row 12 | polled; nought shut or with a guard; else 10 and 5 a misbehaving kid this month | the same |
| No guard, four kids | said on tick 779, line 0, in the guards' cap | the same |
| No guard, three kids (nine got away) | not said (25) | the same (`under-threshold`) |
| One guard, four kids | not said | the same (`no-score`, `guards=1`) |
| The tag and the bar | row 14, part 2, string 18; button 16 builds the hire screen on its guards' tab, tag off | the same |
| Row 264, more than two kids | posted on 903 | the same since Q414 ("Lots of pranks") |

**Not built or not seen.** In no game run: lines 2 and 3 by the topic itself (samples 36 and 37; the third's gesture row, row 6, is played, Q596, through `sayas`; the test
holds the turn), a kid counted in play (nothing here counts one), two guards or more, a guard hired after the line,
Full Simulation (not asked: the row is either mode's), the other three themes. Not heard in either game. The last staff
row, 17, is not built (Q420; rows 8, 9, 11 and 14 to 16 are, above and below).

## Lots of pranks (message `0x108`, Q414)

**Row 264, message `0x108`, `LotsOfPranks`** (the emulated table, `meta_full.json`, at `0x00771cc0`): polled, category 0,
either game mode (`+0x0c` 2), score function `0x005a04f0`, one copy, first response 514, **one line**, selection 2. Response
514 (the response table's row 512) is **sample 505**, lip 505, the random gesture chain, model slot 0 on the global bank,
prop 6 (the guards' cap), no second prop, group `0x17f`, which no row of the tag table holds: **the line leaves no tag.**
From the shipped park's cursor its turn is 124 polled rows after row 12's: ticks 903, 1058, 1213 and every 155 on. The
transcript (`global-speech-transcripts.tsv`, 8.2 s): "you've got some visitors pulling pranks in your park and frankly
there a bad influence you should put a stop to it before this kind of behavior spreads" (as transcribed).

**Its score, `0x005a04f0`** (`ECX` the balance object, kept in `ESI`): `FUN_004c94a0( 0 )` on the analyser
(`FUN_00519510( 0 )` on the world), **this month's misbehaving kids** (`+0x21bfc`, caught and not caught alike; `hud.md`,
"The misbehaving kids"), against `[ESI + 0x5a8]`, `LotsOfPranks.MorePrankstersThan`, **2**: more (`JLE` past), and the
score is `[ESI + 0x5ac]`, `LotsOfPranks.Score`, **100**; else nought. **Nothing else is asked**: not `mParkClosed`, not the
guards, not the days. The two keys are the schema's records at `0x007384f4` and `0x00738530`, in that order (so slots
`+0x5a8`, `+0x5ac`), set by `Advisor.sam`'s lines 646 and 647 (the section's heading, line 554, is a comment).

### OpenTPW (`ParkAdvisorMessages.LotsOfPranks`, a topic's `MorePranksters`)

A topic scored from `ParkFacts.MisbehavingKids` (Q413's), and nothing else of them. The poll line ends ` closed=N
guards=N kids=N`, the facts beside the score though it reads only the kids. Its group is the topic's default, `0x17f`, so
`ParkTags` leaves no tag. Nothing here counts a kid (the chase waits on the pranks, `DECIDE_PRANK_*`), so the count comes
from the file.

**The scenes**: Q413's `three.TPWS` (no guard, 3 kids, 9 got away) and `guard.TPWS` (the shipped file, its guard kept, 4
kids), and `q414/two.TPWS` (Q413's `noguard.TPWS` with 2, 9, 0; `q414/mkkids.py`).

**Predicted first** (`q414/PREDICTION.txt`), every prediction held; one thing seen and not predicted (below).

**This game** (`q414/confirm.py fix|guard|under|control`, on the desktop; `run-fix1` the final run): *`three`*: `money`
reading `misbehaving kids 3 got away 9 ever 0`, then `poll message=264 row=264 days=39 score=100 threshold=25 tick=903
result=post closed=0 guards=0 kids=3`, said on that tick, the first line of the run (`attempt=1 message=264 subject=0
score=100 threshold=25 response=514 sample=505 line=0 tick=903 duration=11498`), `saying sp_505.mp2 ... wearing=6,0`, him in
the guards' cap, no tag before or after (`1-line.png`). *`guard`* (`run-guard1`): `... score=100 ... result=post closed=0
guards=1 kids=4`, said on 903 in the cap. *`two`* (`run-under1`): `... score=0 ... result=no-score closed=0 guards=0 kids=2`
and silent to tick 962. **Main's build** on `three` (`run-control1`): no line of message 264 to tick 962.

**The original** (`q414/orig/run.sh <file>`, `advisor.py ... 0xc,0x108 poll` beside `tags.py`; a fresh program each,
off-screen). *`two`* (run a): `0x108`'s record (0, -1, 0, 0) throughout, the poller silent for its 420 s (`a-end.png`, 16
April). *`three`* (run b): **`0x108`'s record from (0, -1, 0, 0) to (903, 0, 1, 0) on tick 903**, the action 12,498 ms (this
game's 11,498 and the helper's 1,000), no tag made, him in the guards' cap (`orig/b-told.png`). Q413's runs b and c saw the
same post on 903 for three and for four kids with a guard. `q414/side-by-side.png` sets the two games' lines side by side.
**Not predicted**: in both runs on a file with no guard the original said a line and raised its tag on tick 828, "Some of
the visitors are misbehaving and your security hasn't seen it. Make sure your guards are patrolling all areas of the park,
or build security cameras.", gone on 946. It is **the polled row 98, message `0x62`**, its score `0x0059e840`: this month's
kids who got away (`FUN_004c94a0( 1 )`, `+0x21c00`) over `GeneralLaxSecurity.MoreThanThisGotAway` 1, times
`ScorePerGetAway` 20, the guards' cap, group 100. Both files hold nine got away, `guard.TPWS` none: the got-away count, not
the missing guard, raised it. Built since ("Kids who got away", below).

| | The original | OpenTPW |
|---|---|---|
| Row 264 | polled; 100 where this month's kids are more than 2, else nought; neither the door nor the guards asked | the same |
| No guard, three kids (nine got away) | said on tick 903, in the guards' cap, no tag | the same |
| One guard, four kids | said on 903 (Q413's run c) | the same (`guards=1`) |
| No guard, two kids (nine got away) | not said | the same (`no-score`) |

**Not built or not seen.** In no game run: a shut park (the test holds it scoring), the line said again past its 120
quarter-ticks (the test holds it), a kid counted in play (nothing here counts one), Full Simulation (not asked: the row is
either mode's), the other three themes. Not heard in either game. Row 98, which the original said on tick 828 on both files with no guard, is built (Q418, below).

## Kids who got away (message `0x62`, Q418)

**Row 98, message `0x62`, `GeneralLaxSecurity`** (the emulated table, `meta_full.json`, at `0x0076f870`): polled,
category 0, either game mode (`+0x0c` 2), score function `0x0059e840`, one copy, first response 257, **two lines**,
selection 2. Responses 257 and 258 (`0x0076afd8`, read in the image) are **samples 291 and 292**, model slot 0, prop 6
(the guards' cap), no second prop, group 100; the first takes the random gesture chain and the second names gesture value
27, row 11 (played, Q596). From the shipped park's cursor its turn is 75 polled rows before
row 264's: ticks 828, 983 and every 155 on. The transcripts (`global-speech-transcripts.tsv`): "some of the visitors are
getting a little wild and your security hasn't noticed make sure your guards are patrolling the whole park or build some
security cameras" (9.5 s), "hey did you see what that guy just did some of the visitors or misbehaving and security
hasn't noticed..." (13.4 s), as transcribed.

**Its score, `0x0059e840`** (undefined bytes, read by hand from `read_memory`; nothing written to the project; `ECX` the
balance object, kept in `ESI`): `PUSH 1`, the world (`[0x007cf6ec]`) through `FUN_00519510`, then `FUN_004c94a0` on the
analyser (`RET 4`: the `1` is its argument), **this month's kids who got away** (`+0x21c00`; `hud.md`, "The misbehaving
kids"), against `[ESI + 0x314]`, **`MoreThanThisGotAway`, 1**: more (`JLE` past), and the score is the count asked again
(`0x0059e871`) times `[ESI + 0x318]`, **`ScorePerGetAway`, 20** (`IMUL`); else nought (`0x0059e87f`). **Nothing else is
asked**: not `mParkClosed`, not the guards, not the caught kids. The two keys are the schema's records at `0x00733328` and
`0x00733364`, `0x3c` apart and in that order, ahead of the section's own record at `0x007333a0` (so slots `+0x314`,
`+0x318`), set by `Advisor.sam`'s lines 580 and 581 (its heading at line 273 is a comment). The other order is refuted by
the original's post on nine got away: nine is not more than 20.

**Its tag** is row 100 of the tag table (`lib/tagtable.py`): key 383 (it takes no tag's place), picture 1, string 119,
no button on the bar, thirty seconds, kind 0. String 119 reads "Some of the visitors are misbehaving and your security
hasn't seen it. Make sure your guards are patrolling all areas of the park, or build security cameras."
(`Language/american`'s is the same but for its straight apostrophe).

### OpenTPW (`ParkAdvisorMessages.LaxSecurity`, a topic's `GotAwayMoreThan`; `ParkFacts.KidsGotAway`; tag row 100)

A topic scored from `ParkFacts.KidsGotAway`, which `ParkPeople.AdvisorFacts` fills from
`ParkState.MisbehavingKids.GotAwayThisMonth` (the file's at a load since Q395, zeroed at a month's close). The poll line
ends ` closed=N guards=N gotaway=N`, the facts beside the score though it reads only the count. `ParkTags.Rows` lists row
100. Nothing here counts a kid who got away (the chase waits on the pranks, `DECIDE_PRANK_*`), so the count comes from the
file.

**The scenes**: `q414/two.TPWS` (no guard, 2 kids, 9 got away; row 264 silent on it), and `q418/mkkids.py`'s
`guardaway.TPWS` (the shipped file, its guard kept, 0 kids, 3 got away) and `one.TPWS` (Q413's `noguard.TPWS` with 2, 1, 0).

**Predicted first** (`q418/PREDICTION.txt`), every prediction held.

**This game** (`q418/confirm.py fix|guard|under|control`, on the desktop; `run-fix1` the final run): *`two`*: `money`
reading `misbehaving kids 2 got away 9 ever 0`, then `poll message=98 row=98 days=35 score=180 threshold=25 tick=828
result=post closed=0 guards=0 gotaway=9`, said on that tick, the first line of the run (`attempt=1 message=98 subject=0
score=180 threshold=25 response=257 sample=291 line=0 tick=828 duration=13998`), `saying sp_291.mp2 ... wearing=6,0`, him
in the guards' cap, tag 100 on the aerial (`1-line.png`). The tag clicked: `message bar: tag 100 subject 0 item 0 bar:
out=1`, `first=hidden second=hidden erase=mb_erase` and string 119 (`2-bar.png`). Row 264 on 903: `score=0 ...
result=no-score ... kids=2`. *`guardaway`* (`run-guard1`): `... score=60 ... tick=828 result=post closed=0 guards=1
gotaway=3`, said on 828 in the cap. *`one`* (`run-under1`): `... score=0 ... result=no-score closed=0 guards=0 gotaway=1`
and silent to tick 961. **Main's build** on `two` (`run-control1`): no line of message 98 to tick 961.

**The original** (`q418/orig/run.sh <file>`, `advisor.py ... 0x62,0x108 poll` beside `tags.py`; a fresh program each,
off-screen). *`two`* (run a): **`0x62`'s record from (0, -1, 0, 0) to (828, 0, 1, 0) on tick 828**, the action 14,998 ms
(this game's 13,998 and the helper's 1,000), one tag lettered string 119 from 828 to 946, him in the guards' cap
(`orig/a-told.png`), `0x108` unmoved to the end (17 April). *`guardaway`* (run b): the same on 828 with the guard on the
staff, the action 14,164 ms (this game's 13,165), the same tag. *`one`* (run c): nothing moved from the load to 16 April,
no tag (`orig/c-end.png`). `q418/side-by-side.png` sets this game's bar beside the original's line.

| | The original | OpenTPW |
|---|---|---|
| Row 98 | polled; where this month's got-away kids are more than 1, that count times 20; neither the door nor the guards asked | the same |
| No guard, nine got away | said on tick 828, line 0, in the guards' cap; tag 100, string 119 | the same (180) |
| One guard, three got away | said on 828 | the same (60, `guards=1`) |
| No guard, one got away | not said | the same (`no-score`) |

**Not built or not seen.** In no game run: the second line by the topic itself (sample 292; its gesture row, row 11, is played, Q596, through `sayas`; the test holds the
turn), a kid getting away in play (nothing here counts one), a shut park (the test holds it scoring), Full Simulation
(not asked: the row is either mode's), the other three themes. Not heard in either game.

## Kids who got away from a guard (message `0x16`, Q423)

**Row 22, message `0x16`, `StaffGuardBuildCamera`** (the emulated table, `meta_full.json`, at `0x0076e7d0`): polled,
category 0, either game mode (`+0x0c` 2), score function `0x0059d3b0`, one copy, first response 64, **three lines**,
selection 2. Responses 64 to 66 are **samples 64 to 66**, the random gesture chain, model slot 0, prop 6 (the guards'
cap), group 24. Every row from 14 to 29 is polled, so from the shipped park's cursor its turn is five after row 17's:
tick 789, then every 155. The transcripts (`global-speech-transcripts.tsv`): "if you build some security cameras your
guards would be more efficient" (3.7 s), "your guards are not as efficient as they could be you should think about
installing some security cameras" (5.5 s), "i'm confident you could boost the productivity of your guards by taking
advantage of the latest technological advancements that means build some security cameras" (8.7 s).

**Its score, `0x0059d3b0`** (undefined bytes, read with Ghidra's `PseudoDisassembler`, nothing written; `ECX` the balance
object, kept in `ESI`): `PUSH 7`, the park analyser (`FUN_00519510`), **the guards, `FUN_004c9960( 7 )`**: none (`JZ`),
nought. Then `PUSH 1`, the analyser again, **this month's kids who got away, `FUN_004c94a0( 1 )`** (`+0x21c00`, the count
row 98 reads), against `[ESI + 0x15c]`, **`AcceptableTrouble`, 4**: not more (`JLE`), nought; otherwise `[ESI + 0x160]`,
**`Score`, 45**, whatever the count. **Nothing else is asked**: not `mParkClosed`, not the caught kids. The keys
(`lib/advisorschema.py`) are `StaffGuardBuildCamera.AcceptableTrouble` (schema kind 5, an integer compared signed here)
and `.Score`, `Advisor.sam`'s lines 119 and 120. So a park whose guards let five kids get away in a month is told to build
security cameras, and row 98 (more than one got away, with or without a guard) asks the same count.

**Its tag** is row 24 of the tag table (`lib/tagtable.py`): key 383, picture 0, string 28, the bar's first button of type
12 (the buy screen on its fourth tab, the features, where the Security Camera is), no second, thirty seconds. String 28
reads "If you built some security cameras it would help your guards do their job."

### OpenTPW (`ParkAdvisorMessages.BuildCameras`, a topic's `GotAwayWithAGuard`; tag row 24)

A topic scored from `ParkFacts`: the guards (`Staff.Guards.Members`) and `KidsGotAway`, both already read by rows 12 and 98.
The poll line ends ` closed=N guards=N gotaway=N`. `ParkTags.Rows` lists row 24. Nothing here counts a kid who got away
(the chase waits on the pranks, `DECIDE_PRANK_*`), so the count comes from the file.

**The scenes** (`q423/mkkids.py`, the analyser's three counts written as Q418's): `five.TPWS` (the shipped file, its guard
kept, five got away), `four.TPWS` (the same, four) and `noguard.TPWS` (Q413's `noguard.TPWS`, five got away).

**Predicted first** (`q423/PREDICTION.txt`). Every prediction held, in both games.

**This game** (`q423/confirm.py fix|under|noguard|control`, on the desktop; the final build `86e3d6d2`): *`five`*
(`run-fix1`): `poll message=22 row=22 days=34 score=45 threshold=25 tick=789 result=post closed=0 guards=1 gotaway=5`,
said on that tick, the first line of the run (`attempt=1 message=22 subject=0 score=45 threshold=25 response=64 sample=64
line=0 tick=789`), `saying sp_064.mp2 ... wearing=6,0`, him in the guards' cap (`1-line.png`). The tag clicked: `message
bar: tag 24 subject 0 item 0 bar: out=1`, `first=mb_buy/help=524 second=hidden` and string 28 (`2-bar.png`). The button
clicked: `button type 12 opens the buy screen on tab 3 and takes tag 24 off`, "Buy Miscellaneous Items" listing the
Security Camera at 250 (`3-screen.png`). *`four`* (`run-under1`): `score=0 ... tick=789 result=no-score closed=0 guards=1
gotaway=4`, silent to 961 but for row 98's line on 828. *`noguard`* (`run-noguard1`): `... no-score closed=0 guards=0
gotaway=5`, the same. **Main's build** on `five` (`run-control1`): no line of message 22 to 963.

**The original** (`q423/orig/run.sh <file>`, `advisor.py ... 0x16,0x62 poll` beside `tags.py`; a fresh program each,
off-screen). *`five`* (run a): **`0x16`'s record from (0, -1, 0, 0) at the load to (789, 0, 1, 0) on tick 789**, one tag
lettered string 28 from 789, him in the guards' cap (`orig/a-told.png`); row 98's on 828. *`four`* (run b): the record
unmoved to tick 960, no tag of string 28; row 98's on 828. *`noguard`* (run c): the same, to 16 April (`orig/c-end.png`).

| | The original | OpenTPW |
|---|---|---|
| Row 22 | polled; with a guard, 45 where this month's got-away kids are more than 4; neither the door nor the caught kids asked | the same |
| A guard, five got away | said on tick 789, line 0, in the guards' cap; tag 24, string 28 | the same (45) |
| A guard, four got away | not said | the same (`no-score`) |
| No guard, five got away | not said | the same (`guards=0`) |
| The tag and the bar | row 24, picture 0; button 12, the buy screen's features | the same (the Security Camera listed) |

**Not built or not seen.** In no game run: lines 2 and 3 (the test holds the turn), a shut park (the test holds it
scoring), a kid getting away in play (nothing here counts one), two guards or more (tested only), Full Simulation (not
asked: the row is either mode's), the other three themes. Not heard in either game.

## A park too big for its guards (message `0xd`, Q412)

**Row 13, message `0xd`, `StaffHireGuards3`:** polled, category 0, either game mode (the row's `+0x0c` is 2), one
copy, first response 38, three lines in turn. Responses 38 to 40 are **samples 38 to 40**, the random gesture chain,
model slot 0, prop 6 (the guards' cap), group 15. From the shipped park's cursor its turns are three after row 10's:
ticks 780, 935, 1090, 1245, 1400 and every 155 on.

**Its score, `0x0059cd90`** (undefined bytes until this item disassembled them; `ECX` the balance object, kept in
`EDI`). Row 10's twin with two changes:

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. **The days since the newest hire**, `FUN_004c90d0` on the analyser, under `[EDI + 0x630]`,
   `WaitingTimes.DaysForStaff`, **30** (a signed `JGE`): nought. Row 7 asks the same ("More rides than the mechanics
   can keep"); row 10 does not.
3. The developed cells, `FUN_004c81b0( 0 )`, under `[EDI + 0x108]`, `StaffHireGuards3.MinCellsForConsideration`,
   **500**: nought.
4. The cells less the guards, `FUN_004c9960( 7 )`, times `[EDI + 0x100]`, `IdealDevelopedCellsPerGuard`, **750**:
   nought unless that is above nought, and then it times `[EDI + 0x104]`, `ScorePerExtra10Cells`, **1**. No division.

**No guard is no exit**, where row 10 answers nought with no handyman: a park with none scores one for every developed
cell from the 500th, so the minimum is what holds the line back. With one guard 776 cells are enough and 775 are not
(`MinScoreForConsideration` 25). The shipped park holds 126 developed cells and one guard, thing 28. Its newest hire is
the researcher's, tick 648, so its days reach 30 on tick 1340: **the first turn that can score is 1400.**

**Its tag** is row 15 of the tag table: key 383, picture 2, string 19, the bar's first button of type 16 (the hire
screen on its guards' tab, the fourth), no second, thirty seconds. String 19 reads "You need more guards to deal with
a park of this size. Have you considered hiring more?" in both language folders.

### OpenTPW (`ParkAdvisorMessages.HireMoreGuards`, a topic's `CellsAGuard`; tag row 15)

A topic scored from the same `ParkFacts` as row 10's, with `DaysSinceStaffHired` (row 7's) before the cells and no
exit for a park with no guard. The guards are the kind's members rows 28 to 32 already read. The poll line ends
` closed=N guards=N cells=N sincehire=N`. `ParkTags.Rows` lists row 15.

**The scenes** (`lib/pathscene.py`): the stock park paused on tick 1233, the first N cells of the 19 x 20 block of bare
grass at (60,15) laid as path, saved. `big.TPWS` 374 cells and the guard dismissed (`fire 28`), 500 developed with no
guard; `under.TPWS` 373 and the guard dismissed, 499; `guard.TPWS` 374 with the guard kept, 500 (`parkfile.py -d`).
Both 500-cell files are 100 over the one handyman's 400, so row 10 speaks first in both games, on tick 1242.

**Predicted first** (`q412/PREDICTION.txt`), both games on files neither had been given. Missed, all mine: two cells
of the first block were a track's ((79,31) and (79,32), `mType` 0, so `parkfile.bare_blocks` passed them); one file
spoiled by building beside it; the attempt's number (it counts every attempt of the run, and row 10's came first); and
the controls' line on 1400, which the poll does not write (it writes a row's answer only when the answer changes).

**This game** (`q412/confirm.py fix|under|guard|control`, on the desktop; `run-fix2` the final run): *`big`*: `poll
message=13 row=13 days=54 score=0 threshold=25 tick=1245 result=no-score closed=0 guards=0 cells=500 sincehire=25`,
then `poll message=13 row=13 days=60 score=500 threshold=25 tick=1400 result=post closed=0 guards=0 cells=500
sincehire=32`, queued and said on that tick (`response=38 sample=38 line=0 tick=1400`), him in the guards' cap with
the tag on the aerial (`1-line.png`). The tag clicked: `message bar: tag 15 subject 0 item 0 bar: out=1
rect=486,1112,1101,1453`, `first=mb_buy/help=528 second=hidden erase=mb_erase` and string 19 (`2-bar.png`). The
button clicked: `button type 16 opens the hire screen on tab 3 and takes tag 15 off`, `windows` reading
`ParkHireScreen tab 3`, the frame "Hire Guards" (`3-screen.png`). *`under`* (`run-under2`) and *`guard`*
(`run-guard2`): on 1245 `... cells=499 ...` and `... guards=1 cells=500 ...`, no other line of row 13 to tick 1442,
the census `message=13/score=0/line=-1/lastTick=0/said=0/slaps=0`, no tag 15. **Main's build** on `big`
(`run-control1`): no line of message 13, no attempt, no tag to tick 1442.

**The original** (`q412/orig/run.sh <file>`, `advisor.py ... 0x7,0xa,0xd,0x11c poll` beside `tags.py`, `bar.py` and
`screens.py`; a fresh program each, off-screen). *`big`* (run a): message `0xa`'s record (1242, 0, 1, 0) on tick 1242
and **`0xd`'s from (0, -1, 0, 0) to (1400, 0, 1, 0) on tick 1400**, one tag at (84, 909, 154, 974) lettered string 19,
him in the guards' cap (`orig/a-told.png`). The tag clicked: the bar's thing 0, buttons 16 and 0, item 0 and the
text. The button clicked: `hire 0x46bae50 hiretab 3` on tick 1445, "Hire Guards" with the fourth tab lit
(`orig/a-button.png`). *`under`* (run b) and *`guard`* (run c): `0xd`'s record unmoved, no slot of `0xd`, no tag of
string 19, to ticks 1864 and 1863, past the turns 1400, 1555 and 1710. `q412/side-by-side.png` sets this game's bar
beside the original's.

| | The original | OpenTPW |
|---|---|---|
| Row 13 | polled; nought shut, before 30 days since a hire, under 500 cells; then 1 for every developed cell over 750 a guard, none too | the same |
| 500 cells, no guard | said on tick 1400, line 0, in the guards' cap | the same |
| 499 cells, no guard | not said | the same (`no-score`) |
| 500 cells, one guard | not said | the same (`no-score`, `guards=1`) |
| 1245, 25 days since the hire | not said | the same (`no-score`, `sincehire=25`) |
| The tag and the bar | row 15, part 2, string 19; button 16 builds the hire screen on its guards' tab, tag off | the same |

**Not built or not seen.** In no game run: lines 2 and 3 (samples 39 and 40; the test holds the turn), a park with a
guard over his 750 (776 cells; the test holds it), two guards or more, a guard hired after the line, Full
Simulation (not asked: the row is either mode's), the other three themes. Not heard in either game. The guards' list
behind the button held one candidate here and three in the original: the pool's top-up, not this row (Q356's note).
Row 12 is built since ("A park with no guard", below).

## A busy park with no entertainer (message `0xe`, Q415)

**Row 14, message `0xe`, `StaffHireEntertainers1`:** polled, category 0, either game mode (the row's `+0x0c` is 2),
one copy, first response 41, three lines in turn (selection 2). Responses 41 to 43 are **samples 41 to 43**, the random
gesture chain, **model slot 1, prop -1**: the theme's own advisor in its costume, as the entertainers' training line
(row 31) and the features' rows 79 and 81 ("The theme's own advisor and its costume"); group 16. From the shipped
park's cursor its turns are one after row 13's: ticks 781, 936, 1091 and every 155 on. The transcripts
(`global-speech-transcripts.tsv`): "all the world's a stage and you don't have any players the park is getting busy so
maybe you should hire some entertainers", "it's really hopping out there and you're a little short on entertainers
actually you don't have any entertainers maybe you should hire some", "here's one for you how many entertainers does it
take to screw in the light bulb ... you don't have any and the park is getting busy have you considered hiring some".

**Its score, `0x0059ce30`** (undefined bytes; read with Ghidra's `PseudoDisassembler`, nothing written to the project;
`ECX` the balance object, kept in `ESI`):

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. The entertainers, `FUN_004c9960( 6 )` on the analyser: above nought (`JG`), nought.
3. **The unhappy guests' share**, `FUN_004c9750( 0x1e )`: the used thing list walked for things of kind 1 (guests) on
   whose cell `FUN_004fa990` passes, the guests inside as `FUN_004c7fa0` counts them; of those, the ones whose happiness
   (the float `+0x19c`, `__ftol` and `& 0xff`) is under the argument; the answer is that count times 100 over the guests
   inside, in whole numbers, nought with none. Not above **30** (`CMP EAX,0x1e`, `JLE`, a literal, no key): nought.
4. **The crowd**, `FUN_004c81e0` (half the guests inside, from nought to 100; the music's level, `ParkAudio`), less
   `[ESI + 0x10c]`, `StaffHireEntertainers1.AtLeastThisBusy`, **35**, times `[ESI + 0x110]`, `ScorePerExtraBusyPoint`,
   **3** (`0x0059ce91`). **Nothing holds it at nought**: a crowd under 35 scores below nought, which the poll does not take.

`MinScoreForConsideration` is 25, so **a crowd of 44, 88 guests inside, is enough and 43 is not**, with more than 30 per
cent of them under 30 happiness and no entertainer. The shipped park holds one entertainer (thing 27) and its 13 guests
stand outside the gate on the road (`mType` 30), so it scores nought.

**Its tag** is row 16 of the tag table: key 383, picture 2, string 20, the bar's first button of type 15 (the hire
screen on its entertainers' tab, the third), no second, thirty seconds. String 20 reads "You've no entertainers and the
park is starting to get busy. Maybe you should hire some?" (`Language/american`'s, "You have no").

**The other staff rows** (read for the order of the next pieces): rows 8 and 9 ("Litter and no handyman", above) and
row 11 ("A busy park with no guard", above) are built, and rows 15 and 16 ("Most or every kind of ride built, and no
researcher", below), and row 17 ("Research too slow, and researchers to hire", below).

### OpenTPW (`ParkAdvisorMessages.HireEntertainers`, a topic's `BusyWithNoEntertainer`; `ParkFacts.GuestsUnhappy`; tag row 16)

A topic scored from the same `ParkFacts`, which now carry the guests inside under 30 happiness (`ParkPeople.AdvisorFacts`,
each one's happiness cut to its low byte, beside `GuestsInside`, row 82's count). The crowd is `ParkAudio.CrowdLevel`, the
music's `FUN_004c81e0`. The entertainers are the kind's members rows 28 to 32 already read. The poll line ends ` closed=N
entertainers=N inside=N unhappy=N crowd=N`. `ParkTags.Rows` lists row 16.

**The scenes** (`lib/stockscene.py`, `q415/mkscene.py`): the stock park paused on tick 765 with its entertainer, thing 27,
dismissed and 90 guests admitted on (52,26) and (52,34) (93 inside, `parkfile.py -g`), then each happiness written
(`+422`, 90, or 5 for the first N inside): `busy.TPWS` 40 unhappy; `even.TPWS` 30; `entkept.TPWS` the same scene with the
entertainer kept, 40. **Five of the stock guests on the road walk through the gate by tick 781 in both games**, so the
poll counts 98 inside: 40 of them is 40 per cent, a crowd of 49, (49 - 35) x 3 = **42**; 30 of 98 is a whole 30.

**Predicted first** (`q415/PREDICTION.txt`). Missed, mine: round 1 took the file's 93 inside, though I had named the walk
through the gate (`run-fix1` read 98, score 42); predicted again and held. In the original, the stills came after the line,
as Q413's did (filmed, below).

**This game** (`q415/confirm.py fix|even|ent|control`, on the desktop; `run-fix2` the final run): *`busy`*: `poll
message=14 row=14 days=33 score=42 threshold=25 tick=781 result=post closed=0 entertainers=0 inside=98 unhappy=40
crowd=49`, queued and said on that tick (`attempt=1 message=14 subject=0 score=42 threshold=25 response=41 sample=41
line=0 tick=781`), `Advisor: sample 41 ... wearing -1,0 on model slot 1`: Lost Kingdom's explorer up, the tag on the
aerial (`1-line.png`). The tag clicked: `message bar: tag 16 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453`,
`first=mb_buy/help=527 second=hidden erase=mb_erase` and string 20 (`2-bar.png`). The button clicked: `button type 15
opens the hire screen on tab 2 and takes tag 16 off`, `windows` reading `ParkHireScreen tab 2`, "Hire Entertainers"
(`3-screen.png`). *`even`* (`run-even2`): `... score=0 ... result=no-score closed=0 entertainers=0 inside=98 unhappy=30
crowd=49`, silent to tick 961. *`entkept`* (`run-ent2`): `... result=no-score closed=0 entertainers=1 inside=98
unhappy=40 crowd=49`, silent to 962. **Main's build** on `busy` (`run-control1`): no line of message 14, no tag, to 962.

**The original** (`q415/orig/run.sh <file>`, `advisor.py ... 0xc,0xd,0xe,0x108 poll` beside `tags.py`, `bar.py`,
`screens.py` and `inside.py ... 60 30`; a fresh program each, off-screen). It loads this game's made guests and counts
them as this game does: `inside 93 ... unhappy 40 share 43 crowd 46` on tick 765, and `inside 98 ... unhappy 40 share 40
crowd 49` from 773, the same five through the gate. *`busy`* (runs a and a2): **message `0xe`'s record from (0, -1, 0, 0)
to (781, 0, 1, 0) on tick 781**, one tag at (84, 909, 154, 974) lettered string 20 of `Language/american`, "You have no
entertainers and the park is starting to get busy. Maybe you should hire some?"; filmed with `record.sh`, the explorer
in the pith helmet up from about 5 s, as the record moved (`orig/a2-told.png`, film 6.50 s). The tag clicked: the bar's
thing 0, buttons 15 and 0, item 0 and the text (`orig/a-bar.png`). The button clicked: `hire 0x46be7d0 hiretab 2` on
tick 899, "Hire Entertainers" with the third tab lit (`orig/a-button.png`). *`even`* (run b): 30 of 98 (a whole 30),
`0xe`'s record unmoved to tick 2283, no tag. *`entkept`* (run c): unmoved to tick 1937, no tag. `q415/side-by-side.png`
sets this game's line beside the original's.

| | The original | OpenTPW |
|---|---|---|
| Row 14 | polled; nought shut, with an entertainer, or with 30 per cent or fewer of the guests inside under 30; then (half the guests inside, to 100, less 35) x 3 | the same |
| 98 inside, 40 under 30, no entertainer | said on tick 781, line 0, Lost Kingdom's own advisor | the same (42) |
| 98 inside, 30 under 30 | not said | the same (`no-score`) |
| 98 inside, 40 under 30, one entertainer | not said | the same (`no-score`, `entertainers=1`) |
| The tag and the bar | row 16, part 2, string 20; button 15 builds the hire screen on its entertainers' tab, tag off | the same |

**Not built or not seen.** In no game run: lines 2 and 3 (samples 42 and 43; the test holds the turn), a crowd under 35
(a score below nought, tested only), a crowd past 100 (tested only), two entertainers or more, an entertainer hired after
the line, Full Simulation (not asked: the row is either mode's), the other three themes. Not heard in either game. The
last staff row, 17, is not built (Q420; rows 8, 9, 11, 15 and 16 are, "Litter and no handyman", "A busy park with no guard", "Most or every kind of ride built, and no researcher").

## Most or every kind of ride built, and no researcher (messages `0xf` and `0x10`, Q421)

**Rows 15 and 16, messages `0xf` and `0x10`, `StaffHireResearchers1` and `2`** (the emulated table, `meta_full.json`):
both polled, category 0, either game mode (`+0x0c` 2), one copy, selection 2; row 15's score function `0x0059cea0`, first
response 44, **three lines**; row 16's `0x0059cf50`, first response 47, **two lines**. Responses 44 to 48 are **samples 44 to
48**, the random gesture chain, model slot 0, prop 10 (the research hat: the scientist's white hair), groups 17 (row 15's)
and 18 (row 16's). From the shipped park's cursor their turns are one and two after row 14's: ticks 782 and 783, then every
155. The transcripts (`global-speech-transcripts.tsv`): row 15's "you've built most of the available rides and you don't
have any wonder research new ones maybe you should hire some researchers", "you don't have any researchers and you've
already built most of the available rides...", "you've already built most of the available rides maybe you should hire
some researchers to devise new ones"; row 16's "you've already built all of the available rides and you don't have anyone
to develop new ones you should hire some researchers", "there aren't any more rides available you've built all of them
already...".

**Their scores** (undefined bytes; read with Ghidra's `PseudoDisassembler` in the private copy, nothing written to the
project; `ECX` the balance object, kept in `EDI` and `ESI`), each step on the park analyser (`FUN_00519510`):

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. The researchers, `FUN_004c9960( 8 )`: above nought (`JG`), nought.
3. **The lab's projects of a ride's kind left undone, `0x004ca630`** (loose code): `FUN_00503b30( 0 )` on the lab
   (`FUN_00519550` on the world, the lab its `+0x1da722` names; "Every feature built, and more to research", below, reads the same of kind 3). None
   (`JZ`): nought.
4. Row 15: the kinds of ride on offer, `0x004ca450( 0 )` ("A poor selection of rides", below); none, nought. The kinds
   standing, `0x004ca7b0( 0, 0 )`; **as many as on offer, nought** (`JZ` at `0x0059cf1f`). Then standing x 100 over on
   offer (`IDIV`, whole numbers) against `[EDI + 0x114]`, **`StaffHireResearchers1.Most`, 70**: not more (`JLE`), nought;
   else the share less 70 times `[EDI + 0x118]`, **`PointsPerExtraPct`, 5**.
5. Row 16: the kinds standing, then on offer (the other order, `0x0059cfa8`, `0x0059cfc0`); **as many**
   (`0x0059cfc5`), `[ESI + 0x11c]`, **`StaffHireResearchers2.Score`, 90**; else nought. Nothing more is asked, so none on
   offer and none standing are as many and score 90 where steps 1 to 3 let by.

`MinScoreForConsideration` is 25, and Instant Action's Lost Kingdom offers four kinds of ride ("Four kinds of ride in an
Instant Action park", `TOOLING.md`): **row 15 can never be said there with the shipped keys** (three of four is 75, five
over 70, 25, not over 25; four of four is nought), and row 16 is said with all four built. The shipped park has a
researcher (thing 30) and eight ride projects undone in its lab, so it is silent on both.

**Their tags** are rows 17 and 18 of the tag table: key 383, picture 2, strings 21 and 22, the bar's first button of type
17 (the hire screen on its researchers' tab, the fifth), no second, thirty seconds. Strings 21 and 22 read "You've built
most of the available rides and you don't have anyone to research new ones. Maybe you should hire some scientists?" and
"You've no scientists and you've built all the available rides. You should hire some!" (`Language/american`'s, "researchers"
and "You have no researchers").

### OpenTPW (`ParkAdvisorMessages.HireResearchers`, `HireResearchersAllBuilt`, a topic's `MostRidesBuilt`, `EveryRideBuilt`; `ParkFacts.RideProjectsUnfinished`; tag rows 17 and 18)

Two topics scored from the same `ParkFacts`, which now carry the lab's ride projects not done (`ParkResearch.Unfinished`
asked for kind nought, the lab's projects as they stand, Q701) beside the researchers, the kinds on offer and the
kinds standing rows 60 and 80 already read. The poll line ends ` closed=N researchers=N unfinished=N kinds=S/O`.
`ParkTags.Rows` lists rows 17 and 18; the bar's button of type 17 was built for the hire screen (Q331).

**The scenes** (`lib/stockscene.py`, paused on 768): `all4.TPWS`, the stock park with its researcher (thing 30) dismissed
and the Crazy Ape, the Rocky Racers and the Aztec Mayhem bought beside the Belly Bounce (four kinds of four); `three.TPWS`,
dismissed, the Crazy Ape and the Rocky Racers (three of four); `kept.TPWS`, the researcher kept, all three bought. Each
holds the stock lab's eight ride projects undone (`parkfile.py -l`). Row 15 is said only with `Most` changed to 50 in both
games (three of four, 25 over 50, times 5, **125**: "A balance key changed in both games", `TOOLING.md`).

**Predicted first** (`q421/PREDICTION.txt`). Every prediction of this game held, in one round.

**This game** (`q421/confirm.py fix|three|most|kept|control`, on the desktop; the final build `49ff42fc`): *`all4`*
(`run-fix1`): `poll message=15 row=15 days=33 score=0 threshold=25 tick=782 result=no-score closed=0 researchers=0
unfinished=8 kinds=4/4`, then `poll message=16 row=16 days=33 score=90 threshold=25 tick=783 result=post ... kinds=4/4`,
said on that tick, the first line of the run (`attempt=1 message=16 subject=0 score=90 threshold=25 response=47 sample=47
line=0 tick=783`), `saying sp_047.mp2 ... wearing=10,0`: him up in the scientist's white hair (`1-line.png`). The tag
clicked: `message bar: tag 18 subject 0 item 0 bar: out=1`, `first=mb_buy/help=529 second=hidden erase=mb_erase` and string
22 (`2-bar.png`). The button clicked: `button type 17 opens the hire screen on tab 4 and takes tag 18 off`, `windows`
reading `ParkHireScreen tab 4`, "Hire Scientists" with the fifth tab lit (`3-screen.png`). *`three`* (`run-three1`): row 15
`score=25 ... result=under-threshold ... kinds=3/4` on 782, row 16 `no-score` on 783, silent to tick 963. *`three`* with
`Most` 50 (`run-most1`, a private folder's `Advisor.sam`): row 15 `score=125 ... tick=782 result=post ... kinds=3/4`, said
(`response=44 sample=44 line=0`, `sp_044.mp2`, `wearing=10,0`), tag 17's bar lettered string 21 with the same button
(`2-bar.png`). *`kept`* (`run-kept1`): both rows `no-score ... researchers=1 unfinished=8 kinds=4/4`, silent to 961.
**Main's build** on `all4` (`run-control1`): no line of message 15 or 16 to 963. The bought rides' queues are joined to no
path, so row 113 is said on tick 841 too (`run-three1`, `run-kept1`; the original's run a, below).

**The original** (`q421/orig/run.sh <file>`, `advisor.py ... 0xf,0x10 poll` beside `tags.py`; a fresh program each,
off-screen; `SAM=most50` writes the key into the reference install's `Advisor.sam` for the run and puts the shipped file
back). *`all4`* (run a): **`0x10`'s record from (0, -1, 0, 0) to (783, 0, 1, 0) on tick 783**, one tag lettered string 22
of `Language/american`, "You have no researchers and you've built all the available rides. You should hire some!", `0xf`
unmoved; on 841 row 113's tag beside it, and on 1403 `0x10`'s second line, (1403, 1, 1, 0), its first turn past the
category's 120 quarter-ticks. The stills came after the line; filmed (run a2, `record.sh`), him up in the white hair from
about 5.6 s with the tag on the aerial (`orig/a2-hair.png`). *`three`* (run b) and *`kept`* (run c): neither record moved
to tick 1461, no tag of rows 17 or 18 (row 113's alone). *`three`* with `Most` 50 (run m): **`0xf`'s record to (782, 0, 1,
0) on 782** and its second line, (1402, 1, 1, 0), on 1402, the tag lettered string 21, "You've built most of the available rides and you don't have anyone to research
new ones. Maybe you should hire some researchers?". `q421/side-by-side.png` sets this game's line beside the original's.

| | The original | OpenTPW |
|---|---|---|
| Rows 15 and 16 | polled; nought shut, with a researcher, or with no ride project left in the lab; row 15 (standing x 100 / on offer, less 70) x 5 past 70 and short of all; row 16 90 with all standing | the same |
| No researcher, four kinds of four | row 16 said on 783, line 0, the scientist's hair; tag 18, string 22 | the same (90; row 15 `no-score`) |
| No researcher, three of four | not said | the same (row 15 25, `under-threshold`) |
| Three of four, `Most` 50 | row 15 said on 782; tag 17, string 21 | the same (125) |
| A researcher, four of four | not said | the same (`no-score`, `researchers=1`) |
| The tag and the bar | rows 17 and 18, part 2; button 17 builds the hire screen on its researchers' tab | the same ("Hire Scientists", the fifth tab) |

**Not built or not seen.** In no game run: row 15 said with the shipped keys (Instant Action's four kinds cannot pass 25;
seen under the threshold), lines 2 and 3 of row 15 and row 16's second line here (the test holds the turn; the original
said row 16's second on 1403 and, with `Most` 50, row 15's on 1402), none on offer (row 16's 90, tested only), a researcher hired after the line, Full
Simulation (not asked: the rows are either mode's), the other three themes. Not heard in either game. The last staff row,
17 (`0x0059cfe0`, category 4), is built (Q422, below).

## Research too slow, and researchers to hire (message `0x11`, Q422)

**Row 17, message `0x11`, `StaffHireResearchers3`** (the emulated table, `meta_full.json`): polled, **category 4** (the
same message no sooner than 900 quarter-ticks on, "Every sideshow built, and more to research", below), either game mode
(`+0x0c` 2), one copy, selection 2, score function `0x0059cfe0`, first response 49, **three lines**. Responses 49 to 51
are **samples 49 to 51**, the random gesture chain, model slot 0, prop 10 (the scientist's white hair), group 19. From the
shipped park's cursor its turn is one after row 16's: tick 784, then every 155. The transcripts
(`global-speech-transcripts.tsv`): "in other news development cycles are lagging behind expectations that means research
is too slow you should hire some more researchers", "the research is taking a long time you might speed things up by
hiring more researchers", "your research is way behind schedule you could speed things up if you hired more researchers".

**Its score** (undefined bytes, read with Ghidra's `PseudoDisassembler`, nothing written; `ECX` the balance object, kept in
`ESI`), each step on the park analyser (`FUN_00519510`) unless named:

1. `FUN_0051a280` on the world, `mParkClosed`: a shut park, nought.
2. **The most a park's researchers could do, `FUN_004c9ed0`** (`StaffPoolInfo.MaxResearchersInPark` times the top grade's
   `ResearchAbility`, "Every sideshow built, and more to research", below), kept in `EDI`: nought (`JZ`), nought.
3. **The months into the game**: `FUN_004f8800` on the park calendar (world `+0x2a0`), the calendar's now less its start,
   which is `mGameTick` times `mFunnySecsPerRealSec` over four, in whole seconds, whatever date the park began on; then
   over `0x1792F8648000` (`0x0067c320`, a signed 64-bit divide), **thirty days**. Under `[ESI + 0x120]`,
   **`MonthsIntoGameForConsideration`, 15** (a signed `JL`): nought. At Lost Kingdom's 3,750 calendar seconds a tick a
   month is 691.2 ticks: **the first ends between ticks 691 and 692, and fifteen are tick 10,368**.
4. The researchers, `FUN_004c9960( 8 )`: none, nought.
5. **The staff pool's researchers to hire, `FUN_004c9e40( 4 )`** (`0x0059d078`, which hands `FUN_00508380` the world `[0x0080239c]`; `ECX` there, the analyser, it ignores; the world's first member is the
   pool: 32 records of `0x14` bytes, each `+0` the kind and `+0xa` `mValid`; the FileFormats saves page, "The staff pool"):
   the slots in use of kind 4. None, nought. A candidate in the player's hand (`mOnPointer`) still counts.
6. **The researchers' ability added up, `FUN_004c9e60`**, times 100, over the most (an unsigned `DIV`): not under
   `[ESI + 0x124]`, **`SlowerThan`, 14** (`JNC`), nought; otherwise `[ESI + 0x128]`, **`Score`, 50**.

So the line asks for research slower than 14 per cent of the most, in a park fifteen months old with a researcher on the
staff and another to hire. **The shipped park is one month old** (tick 768): its researcher of grade 2 is 12 of
Instant Action's 120 (10 per cent) and its pool holds three researchers (slots 0 to 2), so only the months keep it
unsaid. Nothing about the research itself is asked: neither the lab's projects nor whether any is left.

**Its tag** is row 19 of the tag table: key 383, picture 2, string 23, the bar's first button of type 17 (the hire screen
on its researchers' tab, the fifth), no second, thirty seconds. String 23 reads "Your research is taking a long time. You
might speed things up by hiring more scientists." (`Language/american`'s, "researchers").

### OpenTPW (`ParkAdvisorMessages.HireMoreResearchers`, a topic's `SlowResearch`; `ParkStrikes.MonthsSinceTheFirstSweep`; `ParkFacts.ResearcherCandidates`; tag row 19)

The topic is scored from `ParkFacts`, which now carry the staff pool's researchers to hire (`ParkStaffPool.OfKind`, kind
4, of `ParkStaffPool.Current`) beside the researchers, their ability and the most row 76 already reads; the months are
the strike's, `ParkStrikes.MonthsSinceTheFirstSweep` (`ride-operation.md`, "The strike, in both games"), from the poll's
tick, so the console's `strikes` and the poll line say the same months (Q531: `months 1` on 792 and `months 2` on 1422,
the poll's `months=1` on 784 and `months=2` on 1404). The poll line ends ` closed=N months=M
researchers=N candidates=C ability=A/M`. `ParkTags.Rows` lists row 19.

**The scenes**: `stock.TPWS` (`lib/stockscene.py`, the stock park paused on 771, nothing done) and `nohire.TPWS`, the same
with the pool's three researchers' `mValid` cleared (`parkfile.pool_set`). **The line is said in both games only with
`MonthsIntoGameForConsideration` set to 1** ("A balance key changed in both games", `TOOLING.md`): the park's clock would
have to stand past tick 10,368: the run's length, since the pool tops up (the original's run d, below, gained a researcher on 1083).

**Predicted first** (`q422/PREDICTION.txt`). Every prediction held, in one round, in both games.

**This game** (`q422/confirm.py fix|month2|shipped|nohire|control`, on the desktop; the final build `4461798e`): *`fix`*
(the key 1, `run-fix1`): `poll message=17 row=17 days=34 score=50 threshold=25 tick=784 result=post closed=0 months=1
researchers=1 candidates=3 ability=12/120`, said on that tick, the first line of the run (`attempt=1 message=17 subject=0
score=50 threshold=25 response=49 sample=49 line=0 tick=784`), `saying sp_049.mp2 ... wearing=10,0`: him up in the white
hair (`1-line.png`). The tag clicked: `message bar: tag 19 subject 0 item 0 bar: out=1`, `first=mb_buy/help=529
second=hidden erase=mb_erase` and string 23 (`2-bar.png`). The button clicked: `button type 17 opens the hire screen on
tab 4 and takes tag 19 off`, "Hire Scientists" listing the three (`3-screen.png`). *`month2`* (the key 2, `run-month21`):
`score=0 ... tick=784 result=no-score closed=0 months=1 researchers=1 candidates=3 ability=12/120`, silent to 962; run on
(`run-month2l1`), `score=50 ... tick=1404 result=post ... months=2 ... candidates=1`, said on 1404, the first turn past
1383, where the second month of thirty days ends (`1-end.png`). *`shipped`* (`run-shipped1`): the same line, silent to 963. *`nohire`* (the key
1, `run-nohire1`): `... no-score ... candidates=0 ...`, silent to 963. **Main's build** (`run-control1`): no line of
message 17 to 962, the row counted `PARK_ADVISOR_POLLED_ROW_NOT_BUILT`.

**The original** (`q422/orig/run.sh <file>`, `advisor.py ... 0x11 poll` beside `tags.py`; a fresh program each, off-screen;
`SAM=m1|m2` writes the key into the reference install's `Advisor.sam` for the run and puts the shipped file back).
*`stock`* with the key 1 (run a, filmed): **`0x11`'s record from (0, -1, 0, 0) to (784, 0, 1, 0) on tick 784**, one tag
lettered string 23 of `Language/american`, "Your research is taking a long time. You might speed things up by hiring more
researchers."; the film shows him up in the white hair with the tag on the aerial (`orig/a-hair.png`). *`stock`* with
the key 2 (run b), with the shipped key (run c) and *`nohire`* with the key 1 (run d): the record unmoved to tick 960,
no tag of string 23. Past it, not predicted: run b's to (1404, 0, 1, 0) on 1404, as this game's; run d's to (1094, 0, 1,
0) on 1094, the first turn after the pool's top-up on 1083 (which candidates joined is not read); run c's unmoved to its
end frame's 23 June 2000, about tick 4,000. `q422/side-by-side.png` sets this game's line beside the original's.

| | The original | OpenTPW |
|---|---|---|
| Row 17 | polled, category 4; nought shut, with a most of nought, under the key's months of thirty days, with no researcher or none to hire; 50 under 14 per cent | the same |
| The stock park, the key 1 | said on 784, line 0, the scientist's hair; tag 19, string 23 | the same (50, `months=1`) |
| The key 2, or the shipped 15 | not said on 784 | the same (`no-score`, `months=1`) |
| The key 2, run on | said on 1404, the second month's first turn | the same (`months=2`) |
| No researcher to hire, the key 1 | not said | the same (`candidates=0`) |
| The tag and the bar | row 19, part 2; button 17 builds the hire screen on its researchers' tab | the same ("Hire Scientists", the fifth tab) |

**Not built or not seen.** In no game run: the line with the shipped key (fifteen months: tested only), `nohire` past the
pool's top-up (the original said the line on 1094; this game's run ended on 963, so its pool's refill is not shown), research at 14
per cent or more (tested only), a most of nought (tested only), lines 2 and 3 (the test holds the turn), a researcher hired
after the line, Full Simulation (not asked: the row is either mode's), the other three themes. Not heard in either game.

## The newest build (the analyser's `mLastObjectBuilt`, Q334)

**The analyser's `+0x21c30` is the calendar as the newest object was made**, standing still or not. It is the
eight bytes at 73,516 of thing 5's record, the stamp before `mLastStaffHired` (`FUN_004c5d70` names it).

**What writes it.** `FUN_004c7680` (`ECX` the analyser): the calendar's now, `FUN_004f8690` on world `+0x2a0`,
stored to `+0x21c30` and `+0x21c34`. Its one caller is the **object constructor's tail**, `FUN_004db090` at
`0x004db7a3`, on the analyser `FUN_00519510` answers; the tail is reached on both arms of the test before it
(`0x004db716`) and the function has one `RET`, so every object made stamps it. The image holds the displacement
`30 1c 02 00` seven times: the analyser's constructor `FUN_004c58c0` (twice: all six stamps set to now as the
park is made), the serialiser `FUN_004c5d70` (twice), this routine, its reader (below) and `FUN_005179c0`, which
zeroes the six. **Nothing a sale runs writes it.** The object constructor has five callers: the placer
`FUN_00529e10` (a purchase, and a move's put-down), the world's set-up `FUN_005156a0` (twice, the two fixed
items, on tick nought), the vehicle summons `FUN_0051a2f0` (a vehicle whose slot is empty is made at its first
summons, `park.md`, "The vehicle thing is made on demand") and `World_MakeEndFeature` (`0x00516b00`, the "End" feature).

**What reads it.** One routine, loose and undefined bytes at `0x004c9070`: the twin of `FUN_004c90d0`
("More rides than the mechanics can keep"), the whole days since the stamp. Nine calls reach it, each in an
advisor score function: rows 60 (`0x0059d990`), 71 (`0x0059dca0`), 73 (`0x0059dde0`), 74 (`0x0059de80`), 75
(`0x0059df30`), 77 (`0x0059e070`), 78 (`0x0059e110`), 79 (`0x0059e1c0`) and 81 (`0x0059e2e0`), found by their
`CALL` bytes in the file on disk; those functions are undefined bytes too, and of them rows 60's, 71's, 73's and
74's are read ("A poor selection of rides", "A poor selection of shops", "A park with rides and no shop", "Too few
shops for the park's size", below), and so are rows 75's, 77's, 78's and 79's since ("A poor selection of
sideshows", "A park with shops and no sideshow", "Too few sideshows for the park's size", "A poor selection of
features"), and row 81's ("A park with sideshows and no feature").

**Measured in files**, twelve the original wrote: in ten the stamp is the newest standing object's own build
date (`+22` of its record: the shipped park's is thing 15's, tick 604; a park with a ride bought on tick 924
holds tick 924's), and in Alexah's two saves of the longest-played jungle park it is a month newer than every
one of the 124 objects standing, a thing built and since sold.

### OpenTPW (`ParkState.LastObjectBuilt`, `AddObject`; `ParkWorld.LastObjectBuilt`, `PutLastObjectBuilt`; `ParkFileWriter.Running.LastObjectBuilt`)

`ParkState` keeps the stamp: the file's at a load, the calendar's start in a park made with no file, and the
calendar's now each time `AddObject` takes a thing made (a purchase, a move's put-down), which logs `Park:
mLastObjectBuilt stamped <date> on mGameTick N`. A sale leaves it. The writer puts it back into the analyser's
record. **A vehicle the file names no thing for stamps it at its first summons** (`ParkPeople.Summon`,
`ParkPeople.MakeVehicle`; "What moves it with nothing bought", below), once a vehicle: the summons makes the
vehicle's thing through `AddObject`, and a file written after names it, so a load of it makes and stamps nothing
(Q342; `saves.md`, "OpenTPW's writer, a vehicle made at its summons"). Four of the nine rows read it here, rows 60, 71, 73 and 74 ("A poor selection of
rides", "A poor selection of shops", "A park with rides and no shop" and "Too few shops for the park's size",
below).

**Predicted first** (`q334/PREDICTION.txt`), both games; every prediction held in one round. Not predictions,
and said there: the twelve files above.

**This game** (`q334/confirm.py bought|sold|control`). *bought* (`run-bought1`, on the desktop, three of three
on its first run): the stock park paused on tick 804, no stamp line before; `buy 1101 41 22 0` built thing 43
and logged one line, `Park: mLastObjectBuilt stamped 4/2/2000 21:30:00 on mGameTick 804`; the frame shows the
Crazy Ape standing (`1-park.png`); the file's analyser's `+73516` is 125941734000000000, tick 804's and thing
43's own build date, and the record's other bytes are the shipped file's (five bytes differ, 73,517 to 73,521).
*sold* (`run-sold2`, off-screen): bought on tick 803, sold on 825, one stamp line; the file's stamp tick 803's,
its newest object's date tick 604's. *Main's build* (`run-control`): no line, and the file holds tick 604's
beside thing 43's own date of tick 804. (`run-sold1` missed its own script's wait, `step 20` being frames: the
sale came on tick 806; its stamp facts were the same.)

**The original** (`q334/orig/run.sh <file>`, `thingwords.py ... 5:0x21c30:Q 5:0x21c38:Q`; one program, each
file loaded twice, at its start and from the list). *bought*: `+0x21c30` read 125941734000000000 from 0.75 s
after the load's click and the same 512 ticks on, `+0x21c38` the shipped 125935884000000000, and its frame
shows the Ape behind its hoardings (`orig/a-end.png`). *sold*: 125941696500000000, tick 803's, with no such
object in the park. *control*: 125934234000000000, tick 604's. **A load's own object constructors do not stamp
over the file's value**: every load left the file's standing.

| | The original | OpenTPW |
|---|---|---|
| A thing bought or put down | the analyser's stamp is now | the same |
| A sale | leaves it (no writer) | the same |
| A load | the file's | the same |
| A file saved here after a purchase, and after its sale | reads this game's stamp | written at 73,516 |
| A vehicle made at its first summons | stamps it, and its slot holds the new thing | the same (Q342): an object on cell (0,0), written with its slot |
| Which vehicle a leaver's summons draws | `% 3` of the save's seed: the third in every load of one file | one step of the save's seed, which nothing else here draws (Q443): from the stock seed the bus first, every run, stamping nothing |
| The nine rows that read it | scored | rows 60, 71, 73 and 74 built ("A poor selection of rides", "A poor selection of shops", "A park with rides and no shop", "Too few shops for the park's size"); the other five not built, not read |

**Not built or not seen.** Five of the nine rows (row 74 is "Too few shops for the park's size"; rows 60, 71 and 73 are "A poor selection of rides", "A poor
selection of shops" and "A park with rides and no shop", below). In no game run: a move's put-down
(the same `AddObject`; no test puts one down either, a put-down needing the scene's objects), a purchase made in
the original by hand (two of its own files hold one), a sale made in the original (read, and the two saves), a
ferry's first summons (run since: below), a park made with no file, Full Simulation: the mode is not asked by
the constructor's tail.

### What moves it with nothing bought (Q341)

**The arrival vehicle made at its first summons.** In every load of a stock-park file the original's stamp moved
with nothing bought (Q335 to Q340). A breakpoint on `FUN_004c7680` in the running original (`brk.sh 0x004c7680`,
`q340/late30.TPWS`, saved on tick 1082) stopped on tick 1425 with the return addresses `0x004db7a8`, the object
constructor's tail, and `0x0051a583`, the summons `FUN_0051a2f0` after its `CALL FUN_004db090`; under them
`0x004cf52b`, the arrival manager's tail, whose call at `0x004cf526` is the summons at random for a leaver with
no vehicle current (`park.md`, "The spent vehicle"). The world's four vehicle words read beside the stamp
(`worldwords.py ... 0x1da72a 0x1da72c 0x1da72e 0x1da730`, the same file again): at the load `size1` 15, the
bus, and `size2`, `size3` and the current vehicle nought; on tick 1094 `size3` and the current vehicle became
14 and the stamp tick 1094's; on tick 1419 `size2` and the current vehicle became 55 and the stamp tick 1419's;
through tick 2069, with the current vehicle changing six times more, and in the run before through tick 2447,
no slot and no stamp moved again. **So a file moves the stamp at most once for each vehicle it names no thing
for**: the stock file names the bus alone, so twice.

**OpenTPW.** `Summon` makes the thing where the vehicle made current has none (`ParkPeople.MakeVehicle`,
through `ParkState.AddObject`, which stamps); as Q341 built it, the stamp alone was made and
`VEHICLE_MADE_NO_THING` counted the thing, which Q342 makes (`saves.md`, "OpenTPW's writer, a vehicle made at its
summons"). The runs below are Q341's.
**Predicted first** (`q341/PREDICTION.txt`): the original's three rounds held on every line (the second's run was cut at tick 2069 by my next load, not
left to 2700 as written); this game's run held
three of five, the two misses my own arithmetic (the stamp of tick 432 written wrong, and the days from tick
1093 to 1124, one, not nought) and one line the log does not repeat (a poll whose result has not changed).
*This game* (`q341/confirm.py fix`, `run-fix1`, on the desktop): `late30` loaded on tick 1082; `People: the
seaplane summoned on mGameTick 1093, at random` and then `Park: mLastObjectBuilt stamped 17/2/2000 10:32:30 on
mGameTick 1093`, the only stamp through tick 1285; row 77 polled on tick 1124 at `score=0 ... sincebuild=1`
and never said, no tag on the mast (`1-quiet.png`); `1x VEHICLE_MADE_NO_THING`; the file saved after holds
125952571500000000, tick 1093's, at the analyser's `+73516`. *Main's build* (`run-control`): the draw was the
bus, no stamp, and row 77 said on tick 1124 with tag 79 standing (the seaplane drawn and the old gap counted
is Q340's `run-late1`). *The original given this game's file* (`orig/c-load.log`): it read 125952571500000000
at the load on tick 1146, and on tick 1147 made its own third vehicle (thing 14) and stamped that tick's: the
file names no thing for the seaplane this game summoned.

**Not the same in the two games.** The draw: the original's is `% 3` of the save's seed and moved the
stamp on tick 1094 in every load of `late30`, the third vehicle's slot where the slots were read; this game's
generator was its own then (Q341's four runs drew the seaplane twice, the ferry and the bus, each on tick 1093, a tick before the original's); since Q443 it is one step of the save's seed, and a file with the stock seed draws the bus first in every run. A bus drawn
makes nothing, so the thirty-day rows come early in every run. The second vehicle is made here when the
draw or a load's size next names it, in the original on tick 1419 to 1434. The vehicle's thing is made and
written since Q342. **Not run:** a load of sixty-one's own summons in the running game (the test holds it), the
second vehicle's stamp in the running game, a park made with no file, Full Simulation.

## Staff nobody has trained (messages `0x1c` to `0x20`, Q351)

**Rows 28 to 32, `StaffTrainMechanics`, `StaffTrainHandymen`, `StaffTrainGuards`, `StaffTrainEntertainers` and
`StaffTrainResearchers`:** polled, category 0, either game mode (`+0x0c` is 2), one copy, three lines each. From
the shipped park's cursor their turns are five ticks running: 950 to 954, 1105 to 1109 and every 155 on.

| Row | Score | Staff type | Budget | Keys | Responses = samples | Props, slot | Gesture rows | Tag (string) |
|---|---|---|---|---|---|---|---|---|
| 28 | `0x0059d620` | 4, mechanic | HQ `+0x10` | `+0x194`, `+0x198` | 82 to 84 = 80 to 82 | 5 (hard hat), 0 | 82 and 84 (23) | 30 (33) |
| 29 | `0x0059d680` | 5, handyman | HQ `+0x0c` | `+0x19c`, `+0x1a0` | 85 to 87 = 83 to 85 | 7 (a cap with a band), 0 | none | 31 (34) |
| 30 | `0x0059d6e0` | 7, guard | HQ `+0x18` | `+0x1a4`, `+0x1a8` | 88 to 90 = 86 to 88 | 6 (a police cap), 0 | 90 (25) | 32 (35) |
| 31 | `0x0059d740` | 6, entertainer | HQ `+0x14` | `+0x1ac`, `+0x1b0` | 91 to 93 = 89 to 91 | -1 (the theme's costume), **1** | none | 33 (36) |
| 32 | `0x0059d7a0` | 8, researcher | HQ `+0x1c` | `+0x1b4`, `+0x1b8` | 94 to 96 = 92 to 94 | 10 (research hat), 0 | none | 34 (37) |

**The score** (five copies of one listing, loose code, disassembled; `ECX` the balance object):

1. The members of the type, `FUN_004c9960( type )` (a thing of the type whose `+3` is nought). None: nought.
2. **The kind's monthly training budget**, a dword of the staff HQ (`FUN_00519450`, thing 1; `mBudget[0..4]` from
   `+0xc`, the handymen's, mechanics', entertainers', guards' and researchers': `ride-operation.md`, "The
   month's change"). Above nought (a `JG`): nought. A budget is training already.
3. `FUN_004c9c00( type )`: over every thing of the type (its `+3` not asked), the grades (`+0x1e4`) added up,
   times 100, over five times how many there are: the staff's grades as a whole per cent of five a member. Not
   under the section's `WorseThan` (a signed compare, `JGE`): nought. Otherwise the section's `Score`.

`Advisor.sam` gives every section `WorseThan` 1 and `Score` 30. **So a kind is told to train while it has a
member, no budget, and every member of grade 0**: one grade among fewer than twenty-one members is already one
whole per cent. The park's age, its door and the newest hire are not asked.

**The tags** are rows 30 to 34 of the tag table: key 383, picture 2, strings 33 to 37, the bar's first button
of type 21 (`b_staffcost`, help row 533; its act opens the Staff Training Budgets screen), no second, thirty
seconds. The English folder this game reads says "Training your cleaners would make them more efficient." and
"Your scientists could do their job better and faster if you trained them." where the american one the original
reads says "janitors" and "researchers"; the other three are the same words in both.

### OpenTPW (`ParkAdvisorMessages.TrainMechanics` to `TrainResearchers`, a topic's `Untrained`, `StaffKind`; `KindTraining`, `StaffTraining`, `ParkFacts.Staff`; tag rows 30 to 34)

Five polled topics more, thirty-one in all. `ParkPeople.AdvisorFacts` hands the score each kind's members, its
budget (`TrainingBudgets`, the file's) and its members' grades added up. The poll line ends ` members=N budget=N
grades=N share=N`. A response that names a gesture row plays it (Q596): 82 and 84 row 7, 90 row 9.

**The bar's staff-costs button opens the Staff Training Budgets screen and takes the tag off** (Q352;
`hud.md`, "The Staff Training Budgets screen"), as the original's: a budget set there is the one these rows read,
and the mechanics' row was polled `score=0 ... tick=1260 result=no-score members=1 budget=75` after 75 was set
on tick 1110 or so (`q352/run-fix2`). The runs below are of the build before that screen, where the click was
counted (`TAG_BAR_BUTTON`). The handymen's cap is lit as the original's, as the hard hat and the pith helmet are
(`ui.md`, "How he is lit"; Q657).

**The scenes** (`q350/both.TPWS`, the stock park saved on tick 963 with one member of each kind; written with
`parkfile.thing_set`): *all0*, all five members' `mCurrentPayGrade` (the record's `+398`) written 0; *g1all*,
all five written 1; *bud2*, *all0* with the HQ's `mBudget[0]` (thing 1's `+82`, the handymen's) written 25 and
`mBudget[2]` (`+90`, the entertainers') 50; *res0*, `q350/g0.TPWS`, the researcher alone at 0.

**Predicted first** (`q351/PREDICTION.txt`), both games. Round 1: the polls, the scores, who is said and who is
not, the hats and the button all held in both games. **Two of this game's checks missed, both the prediction's
own**: it gave the five lines too few ticks, so it expected five tags standing where the first's thirty seconds
were up before the fifth line (four stood, in both games), and it took the tag's words from the american
folder. Round 2's check of the tag's click then missed on a race of its own (a tag's time ran out between the
reading and the click); round 1's run holds that click.

**This game** (`q351/confirm.py <mode>`; *fix* on the desktop, the rest off-screen). *all0* (`run-fix1`,
`run-fix2`): `poll message=28 row=28 days=47 score=30 threshold=25 tick=1105 result=post members=1 budget=0
grades=0 share=0`, and the same of 29 to 32 on ticks 1106 to 1109; all five said, in the table's order, on
ticks 1105, 1163, 1190, 1217 and 1268 (1265 in the second run: a line's length is its drawn gesture chain's),
`wearing 5,0`, `7,0`, `6,0` on model slot 0, `-1,0 on model slot 1`, `10,0` on slot 0, the first naming
gesture row 7, then counted; the hard hat, the cap, the police cap, the theme's fly in the pith helmet and
the research hat's hair (`run-fix2/hats.png`). Tags 31 to 34 stood a second after the fifth, the newest on top.
The top tag clicked (`run-fix1`): `message bar: tag 34 subject 0 item 0 bar: out=1 ... first=b_staffcost/help=533
second=hidden erase=mb_erase` and the words (`4-bar.png`); the button clicked: `button type 21 is not built, so
nothing more happens`, `TAG_BAR_BUTTON` counted once. *res0*: rows 28 to 31 `score=0 ... result=no-score
members=1 budget=0 grades=3 share=60`, row 32 posted and said on tick 1109, tag 34 alone. *g1all*: five
`no-score ... grades=1 share=20`, nothing said by tick 1138. *bud2*: row 29 `no-score members=1 budget=25
grades=0 share=0`, row 31 `budget=50`; 82, 88 and 94 said (ticks 1105, 1169, 1196), tags 30, 32 and 34. Main's
build on *all0* (`run-control`): no line of messages 28 to 32, no attempt, no tag.

**The original** (`q351/orig/run.sh <file>`: `advisor.py ... 0x1c,0x1d,0x1e,0x1f,0x20,0x11c poll` beside
`thingwords.py` on the five grades and the HQ's five budgets, `tags.py`, `bar.py`, `screens.py`). The grades
and the budgets read as written in every run (`bud0` 25, `bud2` 50 in run c).

- *all0* (runs a, a2): message `0x1c`'s record **(1105, 0, 1, 0) on tick 1105**, the slots taking `0x1d`,
  `0x1e`, `0x1f` and `0x20` at 30 on ticks 1106 to 1109, and their records moving on ticks **1165, 1192, 1219
  and 1269**, the table's order. Each left its tag, the first gone on tick 1226, so four stood at the fifth,
  the newest on top at (84, 684, 154, 749). He wears the hard hat, a brown cap with a maroon band, a police cap
  and the theme's pith helmet (`orig/a-hats.png`). The top tag clicked (a2): the bar's data read thing 0,
  buttons 21 and 0 and the researchers' words; its first button clicked: the Finance category left on 9, a
  screen up, headed "Staff Training Budgets" with five rows of $ 0 (`orig/a2-button.png`), and the tag off.
- *g1all* (run b): all five records fresh and no tag for the 90 s read (`orig/b-end.png`).
- *bud2* (run c): `0x1c` on tick 1105, `0x1e` on 1164, `0x20` on 1191, their three tags; `0x1d` and `0x1f`
  never in a slot and fresh to the end (tick 1314).
- *res0*: Q350's runs a, d and e, the researchers' line on tick 1109 ("Every sideshow built, and more to
  research").

| | The original | OpenTPW |
|---|---|---|
| Every kind at grade 0, no budget | all five said, table order: ticks 1105, 1165, 1192, 1219, 1269 | the same: 1105, 1163, 1190, 1217, 1268 |
| Every kind at grade 1 | none said | the same (`share=20`) |
| A budget for the handymen and the entertainers | those two not said; the other three on 1105, 1164, 1191 | the same: 1105, 1169, 1196 (`budget=25`, `budget=50`) |
| The researcher alone at grade 0 | the researchers' line on 1109 | the same |
| What he wears | hard hat, cap, police cap, the theme's fly in its helmet, research hat | the same, lit as the original's (Q657) |
| The tags | rows 30 to 34, four standing at the fifth line, the newest on top | the same |
| The bar | button 21 and the bomb | the same (`b_staffcost`, help 533) |
| The bar's button | opens the Staff Training Budgets screen, the tag off | the same since Q352; in these runs counted, nothing opened |

**Not built or not seen.** In no game run: lines 2 and 3 of any row, a line again past its 120 quarter-ticks' wait, two members
of a kind (the tests hold the sum and the share), twenty-one members with one grade, a budget below nought, a
budget set on the original's own screen, the researchers' line's hat in this item's frames of the original
(Q350's `a2-g3.png` shows it), a park made with no file, Full Simulation (not asked: the rows are either
game's and read neither). Run b printed no tick after the load, as nothing it read changed: that the poll
passed tick 1109 there is taken from its 90 s, not read. Not heard in either game.

## A poor selection of rides (message `0x3c`, Q335)

**Row 60, message `0x3c`, `RidesPoorSelection`:** polled, category 0, either game mode, one copy, first response
176, three lines in turn. Responses 176 to 178 are samples 173 to 175 ("I don't mean to be critical, but you
don't have a very good selection of rides in the park"): the random gesture chain, model slot 0, no prop, group
62. It is two polled rows before row 62: from the shipped park's cursor its turns are ticks 802, 957, 1112 and
every 155 on.

**Its score, `0x0059d990`** (loose code, disassembled for this; `ECX` the balance object), each step on the park
analyser (`FUN_00519510`):

1. The kinds of ride on offer, `0x004ca450( 0 )`: the catalogue's items (`FUN_00412e60`, `FUN_00412e70` on
   `0x007890a0`) whose `Info.WhichUIType` (`+0x4ac`) is nought, a ride, whose control record (`FUN_004d3d80`) is
   researched (`+0x10`) and whose `UsageInfo.GoldenTicketCost` (`+0xc4`) is nought; its argument, nought here,
   would count the golden-ticket rides too. None: nought.
2. The whole days since the newest build, `0x004c9070` ("The newest build", above), under `[ECX + 0x634]`,
   `WaitingTimes.DaysForObjects`, **30** (a signed `JL`): nought.
3. The rides standing, `FUN_004ca8e0( 0, 0 )`, under `[ECX + 0x224]`, `RidesPoorSelection.MoreRidesThan`, **3**:
   nought. So three rides are enough, whatever the key's name.
4. The kinds standing, `0x004ca7b0( 0, 0 )`: the ride items of step 1's two tests whose control record counts at
   least one standing (`+0x18` above nought; the object's constructor and destructor keep it, `0x004db6d0` and
   `0x004dd0e1`), researched or not. Times 100 over step 1's count, an unsigned whole division, not under
   `[ECX + 0x220]`, `RidesPoorSelection.PoorerThan`, **30**: nought.
5. Otherwise `[ECX + 0x228]`, `RidesPoorSelection.Score`, **30**.

The door is not asked, nor whether a ride is open. `MinScoreForConsideration` is 25, so the row speaks wherever
it scores. **The shipped park offers four kinds** (the Belly Bounce, the Crazy Ape, the Rocky Racers and the
Aztec Mayhem: the regular catalogue's Eruption and Jurassic Tours are researched and cost a golden ticket, the
rest are not researched), in Instant Action's catalogue and the regular one alike, so there one kind standing is 25 and poor,
and two are 50 and not.

**Its tag** is row 62 of the tag table: key 383, picture 2, string 65, the bar's first button of type 9 (the buy
screen on its rides tab, "A park with no ride in it"), no second, thirty seconds. String 65 reads the same in
both language folders: "Your selection of rides isn't so good. You should build some more."

### OpenTPW (`ParkAdvisorMessages.PoorSelection`, a topic's `PoorSelection`, `DaysForObjects`; `ParkFacts.RideKindsOnOffer`, `RideKindsStanding`, `DaysSinceObjectBuilt`; `ParkState.DaysSinceObjectBuilt`; `ParkPeople.RideKinds`; tag row 62)

A fifteenth polled topic, scored from the same `ParkFacts`, which now carry the kinds of ride on offer (the
catalogue's ride items with no golden ticket that `ParkResearch` holds researched, the save's word first), the
kinds standing (those `ParkState.BuiltOf` counts one of, the control record's own count) and the whole days since
`ParkState.LastObjectBuilt`, the first reader of that stamp. The poll line ends ` kinds=K/T rides=N
sincebuild=N`. `ParkTags.Rows` lists row 62; the bar's button of type 9 was built for row 62's message.

**The scene** (`q335/scene.py`, `q335/mk.py`): the stock park paused on tick 916 with two Belly Bounces bought
on (41,22) and (70,40) as things 43 and 44 and left shut, saved: three rides of one kind, the mechanic kept. A
purchase stamps the analyser with now, so the stamp is written into copies of the file afterwards
(`parkfile.thing_set( src, dst, 5, 73516, '<q', filetime )`): `poor30.TPWS` the stamp of tick 265, 692 ticks
before row 60's turn on tick 957, and `poor29.TPWS` the stamp of tick 266. Beside them `offer3.TPWS`, `poor30`
with the Aztec Mayhem's control record's researched byte nought (`original/research.py ... 1104 0`: three on
offer); `two30.TPWS`, Q333's park of the Bounce and two Crazy Apes with the same stamp (two kinds); and
`rides2.TPWS`, one Bounce bought (two rides).

**Predicted first** (`q335/PREDICTION.txt`), both games on files neither had been given; every prediction held,
in one round. Not predictions, and said there: the four kinds on offer, read from a test's printout.

**This game** (`q335/confirm.py fix|d29|offer3|two|rides2|control`). *`poor30`* (`run-fix1`, on the desktop, six
of six on its first run, and `run-fix2` on the commit's build): `poll message=60 row=60 days=41 score=30
threshold=25 tick=957 result=post kinds=1/4 rides=3 sincebuild=30`, `post message=60 ... result=queued` and
`attempt=1 message=60 subject=0 score=30 threshold=25 response=176 sample=173 line=0 tick=957`; `saying
sp_173.mp2 ... wearing=0,0`, him up with his antennae (`1-line.png`), `tags` reading `tag=62/picture=2/subject=0`
with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 62 subject 0
item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=521 second=hidden erase=mb_erase` and the text
(`2-bar.png`). The button clicked: `button type 9 opens the buy screen on tab 0 and takes tag 62 off`, `windows`
reading `ParkBuyScreen tab 0`, `standing=0`, and the frame "Buy Ride" listing the four on offer
(`3-screen.png`). *`poor29`* (`run-d29`, off-screen): `score=0 ... tick=957 result=no-score kinds=1/4 rides=3
sincebuild=29`, then `days=48 score=30 ... tick=1112 result=post ... sincebuild=36`, said on that tick after
rows 113's and 259's lines (996, 1053). *`offer3`*: `no-score kinds=1/3 rides=3 sincebuild=30`. *`two30`*:
`no-score kinds=2/4 rides=3 sincebuild=30`. *`rides2`*: `no-score kinds=1/4 rides=2 sincebuild=30`; none of
the three said it by tick 975. **Main's build** on `poor30` (`run-control`): no line of message 60, no attempt,
no tag by tick 975.

**The original** (`q335/orig/run.sh <file>`, `advisor.py ... 0x3c,0x3e,0x7,0x71,0x103,0xbc,0x11c poll` beside
`thingwords.py ... 5:0x21c30:Q:lastbuilt`, `tags.py`, `bar.py` and `screens.py`; two programs, five loads).
*`poor30`* (run a): message `0x3c`'s record went from (0, -1, 0, 0) to **(957, 0, 1, 0) on tick 957**, one tag
with it at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text string 65; he is up with no hat
(`orig/a-told.png`). The tag clicked: the bar's data read thing 0, buttons 9 and 0, item 0 and the text
(`orig/a-bar.png`). The button clicked: `screens.py`'s buy screen went from `0x0` to a pointer on tick 1053, the
frame "Buy Ride" listing the same four rides (`orig/a-button.png`). *`poor29`* (run b2, a fresh program): the
record fresh through ticks 996 and 1053 and **(1112, 0, 1, 0) on tick 1112**. *`offer3`* (run c): the stamp
read 125921521500000000, the file's; record fresh and no slot through tick 1115. *`two30`* (run d) and
*`rides2`* (run e): fresh and no slot through tick 1114. Each said row 113's line on tick 996 and, with three
rides, row 259's on 1053, as this game does. (Run b loaded nothing: run a had left the buy screen up, and the
list's click does nothing under it.)

| | The original | OpenTPW |
|---|---|---|
| Row 60 | polled; nought with no kind on offer, until 30 days after the newest build, and under three rides; then 30 where the kinds standing are under 30 per cent of those on offer | the same |
| Three Bounces, four kinds on offer, day 30 | said on tick 957, line 0, no hat | the same |
| Day 29 | nought on tick 957; said on tick 1112 | the same |
| Three kinds on offer (33 per cent) | not said | the same (`kinds=1/3`) |
| Two kinds standing (50 per cent) | not said | the same (`kinds=2/4`) |
| Two rides | not said | the same (`rides=2`) |
| The tag and the bar | row 62, part 2, string 65; button 9 builds the buy screen on its rides tab, tag off | the same |

**Not built or not seen.** Rows 71 and 73 are "A poor selection of shops" and "A park with rides and no shop",
below, row 74 "Too few shops for the park's size" and row 75 "A poor selection of sideshows"; rows 77 and 78 are "A park with shops and no sideshow" and "Too few sideshows for the park's size"; rows 79 and 81 are "A poor selection of features" and "A park with sideshows and no feature". **The original's stamp moved with nothing bought**: in run a's
park, left running, `mLastObjectBuilt` went to tick 1400's on tick 1400 (read in run b's log, before its load);
what was made then is not read (the vehicle summons and the "End" feature are the constructor's callers, "The
newest build"), and here nothing moves it, so past such a moment this game's row 60 would speak again where
the original's waits thirty days more. In no game run: lines 2 and 3 (samples 174 and 175; the test holds the
turn), the line said again past its 120 quarter-ticks, a golden-ticket ride standing or on offer, a kind
researched in the park (research is built since Q701; not run here), a thing built after the line (the stamp then silences the row
for thirty days, the test's last step), the bar's button with the buy screen open, a park made with no file,
Full Simulation: the mode is not asked by row 60's score, and its catalogue offers the same four on the shipped
save. Not heard in either game.

## A poor selection of shops (message `0x47`, Q336)

**Row 71, message `0x47`, `ShopsPoorSelection`:** polled, category 0, either game mode, one copy, first response
207, two lines in turn. Responses 207 and 208 are samples 205 and 206 ("I've been meaning to have a talk with
you ... it's about your shops, the selection isn't very good right now"; "Have you considered building more
shops?"): the random gesture chain, model slot 0, no prop, group 73. It is six polled rows after row 60 (rows 66
to 70 are not polled): from the shipped park's cursor its turns are ticks 808, 963, 1118 and every 155 on.

**Its score, `0x0059dca0`** (loose code, disassembled for this; `ECX` the balance object) is row 60's with the
shops' counts, each step on the park analyser (`FUN_00519510`):

1. The kinds of shop on offer, `0x004ca510` (its one argument is not read): the catalogue's items whose
   `Info.WhichUIType` (`+0x4ac`) is 1, a shop, and whose control record is researched (`+0x10`). **It does not
   ask the golden ticket**, where row 60's count of rides does. None: nought.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30**: nought.
3. The shops standing, `FUN_004ca8e0( 1, 0 )` (the things of type 3 whose item's `WhichUIType` is 1; its second
   argument is read for a ride alone), under `[ECX + 0x25c]`, `ShopsPoorSelection.MoreShopsThan`, **2**: nought.
4. The kinds standing, `0x004ca7b0( 1, 0 )`: the shop items whose control record counts at least one standing
   (`+0x18`) **and whose `GoldenTicketCost` is nought** (its second argument, nought, asks that), researched or
   not. Times 100 over step 1's count, an unsigned whole division, not under `[ECX + 0x258]`,
   `ShopsPoorSelection.PoorerThan`, **25**: nought.
5. Otherwise `[ECX + 0x260]`, `ShopsPoorSelection.Score`, **50**.

The door is not asked, nor whether a shop is open. **The shipped park offers three kinds of shop** (the Drinks
Shop, the Burger Shop and the Balloon Shop: of the regular catalogue's eight and Instant Action's six no other
is researched, and none costs a golden ticket), so there one kind standing is 33 and the row never scores, however
many shops stand: it needs five kinds on offer for one standing to be poor (20), or nine for two.

**Its tag** is row 73 of the tag table: key 383, picture 2, string 75, the bar's first button of type 10 (the buy
screen on its shops tab), no second, thirty seconds. String 75 reads the same in both language folders: "Your
selection of shops isn't great. You should build some more."

### OpenTPW (`ParkAdvisorMessages.PoorShopSelection`, a topic's `PoorOf`; `ParkFacts.ShopKindsOnOffer`, `ShopKindsStanding`, `Kinds`; `ParkPeople.ShopKinds`; tag row 73)

A sixteenth polled topic. Row 60's topic field `PoorSelection` names its section, and a new `PoorOf` says whose
kinds are asked: `ParkFacts.Kinds` answers the kinds on offer and standing, the things and the section's
least-number key (`MoreRidesThan`, `MoreShopsThan`) for either. `ParkPeople.ShopKinds` counts the two from the
catalogue's items, the research flags and the park's own count of each item standing, with the original's two
different tests of the golden ticket. The poll line ends ` kinds=K/T shops=N sincebuild=N`. `ParkTags.Rows` lists
row 73; the bar's button of type 10 was built with row 62's message's.

**The scene** (`q336/scene.py`, `q336/mk.py`): the stock park paused on tick 917 with a second Drinks Shop
bought on (41,22) as thing 43, saved (it stands open). Into copies of the file two more shops are marked
researched (`original/research.py`: the Ice Cream Shop, 1206, and the Fries Shop, 1212, five on offer) and the
stamp written: `shop30.TPWS` the stamp of tick 271, 692 ticks before row 71's turn on tick 963, and `shop29.TPWS`
the stamp of tick 272. Beside them `offer4.TPWS` with the Ice Cream Shop alone marked (four on offer: 25, not
poor); `two30.TPWS`, a Burger Shop bought in place of the Drinks Shop (two kinds of five); and `shops1.TPWS`,
nothing bought (one shop).

**Predicted first** (`q336/PREDICTION.txt`), both games on files neither had been given, the scene runs
included. Not predictions, and said there: the three kinds on offer, read from a test's printout.

**This game** (`q336/confirm.py fix|d29|offer4|two|shops1|control`). *`shop30`* (`run-fix1`, on the desktop, six
of six on its first run): `poll message=71 row=71 days=41 score=50 threshold=25 tick=963 result=post kinds=1/5
shops=2 sincebuild=30`, `post message=71 ... result=queued` and `attempt=1 message=71 subject=0 score=50
threshold=25 response=207 sample=205 line=0 tick=963`; `saying sp_205.mp2 ... wearing=0,0`, him up with his
antennae (`1-line.png`), `tags` reading `tag=73/picture=2/subject=0` with `mast=94,901,144,984` and
`tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 73 subject 0 item 0 bar: out=1
rect=486,1112,1101,1453 ... first=mb_buy/help=522 second=hidden erase=mb_erase` and the text (`2-bar.png`). The
button clicked: `button type 10 opens the buy screen on tab 1 and takes tag 73 off`, `windows` reading
`ParkBuyScreen tab 1`, `standing=0`, and the frame "Buy Shop" listing the five on offer (`3-screen.png`).
*`shop29`* (`run-d29`, off-screen): `score=0 ... tick=963 result=no-score kinds=1/5 shops=2 sincebuild=29`, then
`days=48 score=50 ... tick=1118 result=post ... sincebuild=36`, said on that tick. *`offer4`*: `no-score
kinds=1/4 shops=2 sincebuild=30`. *`two30`*: `no-score kinds=2/5 shops=2 sincebuild=30`. *`shops1`*: `no-score
kinds=1/5 shops=1 sincebuild=30`; none of the three said it by tick 990.

**Main's build** on `shop30` (`run-control`): no line of message 71, no attempt, no tag by tick 992.

**The original** (`q336/orig/run.sh <file>`, `advisor.py ... 0x47,0x48,0x49,0x3c,0x3d,0x3f,0x40,0x41,0x71,0x104,0xbc,0x11c
poll` beside `thingwords.py ... 5:0x21c30:Q:lastbuilt`, `tags.py`, `bar.py` and `screens.py`; two programs, five
loads). *`shop30`* (run a): message `0x47`'s record went from (0, -1, 0, 0) to **(963, 0, 1, 0) on tick 963**,
one tag with it at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text string 75; he is up with no hat
(`orig/a-g10.png`, `a-g12.png`). The tag clicked: the bar's data read thing 0, buttons 10 and 0, item 0 and the
text (`orig/a-bar.png`). The button clicked: `screens.py`'s buy screen went from `0x0` to a pointer on tick 1052
and the tag was gone on 1054, the frame "Buy Shop" listing the same five shops (`orig/a-button.png`). *`shop29`*
(run b, a fresh program): the record fresh until **(1118, 0, 1, 0) on tick 1118**. *`offer4`* (run c): the stamp
read 125921746500000000, the file's; record fresh and no slot through tick 1136. *`two30`* (run d): the same
through tick 1135. *`shops1`* (run e): fresh and no slot through tick 1085 at the least. No other message of the
twelve read was said in any run: rows 61, 63 to 65 and 72 stayed fresh.

| | The original | OpenTPW |
|---|---|---|
| Row 71 | polled; nought with no kind of shop on offer, until 30 days after the newest build, and under two shops; then 50 where the kinds standing are under 25 per cent of those on offer | the same |
| Two Drinks Shops, five kinds on offer, day 30 | said on tick 963, line 0, no hat | the same |
| Day 29 | nought on tick 963; said on tick 1118 | the same |
| Four kinds on offer (25 per cent) | not said | the same (`kinds=1/4`) |
| Two kinds standing (40 per cent) | not said | the same (`kinds=2/5`) |
| One shop | not said | the same (`shops=1`) |
| The tag and the bar | row 73, part 2, string 75; button 10 builds the buy screen on its shops tab, tag off | the same |
| A golden-ticket shop | on offer once researched; never a kind standing | the same (`ParkPeople.ShopKinds`; by test alone) |

**Not built or not seen.** **The original's stamp moved again with nothing bought**: in run e's park, the one
with no purchase, `mLastObjectBuilt` went to tick 1085's on tick 1085, and in runs c and d it stood through
ticks 1136 and 1135 (in Q335's park it moved on tick 1400); what was made is still not read ("The newest
build"), and here nothing moves it. In no game run: line 2 (sample 206; the test holds the turn), the line said
again past its 120 quarter-ticks, a golden-ticket shop (no Lost Kingdom shop costs one: the two tests of it are
held by the unit test alone), a kind researched in the park (research is built since Q701; not run here, the
files' bytes were written), a thing built after the line, the bar's button with the buy screen open, a park made with no file,
Full Simulation: the mode is not asked by row 71's score, and the regular catalogue's two more shops (the Steak
Restaurant and the Gift Shop) are not researched on the shipped save. The shipped park cannot reach the row at
all without research: three kinds on offer. Not heard in either game.

## A park with rides and no shop (message `0x49`, Q337)

**Row 73, message `0x49`, `ShopsBuildShops`:** polled, category 0, either game mode, one copy, first response
211, two lines in turn. Responses 211 and 212 are samples 209 and 210 ("The park hasn't got any shops, have you
considered building some?"; "Your park hasn't got any shops, have you thought of adding some? ..."): the random
gesture chain, model slot 0, **prop 5, the hard hat**, group 75. It is two polled rows after row 71: from the
shipped park's cursor its turns are ticks 810, 965, 1120 and every 155 on.

**Its score, `0x0059dde0`** (loose code, disassembled for this; `ECX` the balance object), each step on the park
analyser (`FUN_00519510`):

1. The kinds of ride standing, `0x004ca7b0( 0, 0 )`: the ride items whose control record counts at least one
   standing and whose `GoldenTicketCost` is nought. None: nought. A park with no ride is row 62's.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30**: nought.
3. The kinds of shop standing, `0x004ca7b0( 1, 0 )`, the same count of the shop items: any, nought. A shop
   that costs a golden ticket is no kind standing, so a park whose only shops are such is told it has none.
4. Otherwise `[ECX + 0x268]` plus `[ECX + 0x26c]` times the park's age in whole days (`FUN_004f8800` over
   `0xC92A69C000`, as row 62 counts them): `ShopsBuildShops.Score` and `ShopsBuildShops.AdditionalPerDay`
   (`lib/advisorschema.py`), **50 and 2** in `Advisor.sam`.

The door is not asked, nor the sideshows, nor what is on offer. The score is never under 50 once it is anything,
so the thirty days are its whole wait: 132 on tick 965 (day 41), 146 on tick 1120 (day 48).

**Its tag** is row 75 of the tag table: key 383, picture 2, string 77, the bar's first button of type 10 (the buy
screen on its shops tab), no second, thirty seconds. String 77 reads "You haven't built any shops yet, and your
visitors are just itching to buy things."

### OpenTPW (`ParkAdvisorMessages.BuildShops`, a topic's `PerDayWithNoShop`; tag row 75)

A seventeenth polled topic. The topic field `PerDayWithNoShop` names the per-day key; the score reads
`ParkFacts.RideKindsStanding`, `ShopKindsStanding` and `DaysSinceObjectBuilt`, all kept for rows 60 and 71, and
the park's age as row 62 does. The poll line ends ` ridekinds=N shopkinds=N sincebuild=N`. `ParkTags.Rows` lists
row 75; the bar's button of type 10 was built with row 62's message's.

**The scene** (`q337/scene.py`, `q337/mk.py`): the stock park paused on tick 914 with its one shop, the Drinks
Shop (thing 16), sold, saved by main's build. Into copies of the file the stamp is written: `noshop30.TPWS` the
stamp of tick 273, 692 ticks before row 73's turn on tick 965, and `noshop29.TPWS` the stamp of tick 274. Beside
them `shop30.TPWS`, nothing sold, and `noride30.TPWS`, the Belly Bounce (thing 13) sold too, each with tick
273's stamp.

**Predicted first** (`q337/PREDICTION.txt`), both games on files neither had been given, the scene runs
included; one round, and every line of it held.

**This game** (`q337/confirm.py fix|d29|shop|noride|control`). *`noshop30`* (`run-fix1`, on the desktop, six of
six on its first run; `run-fix2` on the commit's build): `poll message=73 row=73 days=41 score=132 threshold=25
tick=965 result=post ridekinds=1 shopkinds=0 sincebuild=30`, `post message=73 ... result=queued` and `attempt=1
message=73 subject=0 score=132 threshold=25 response=211 sample=209 line=0 tick=965`; `saying sp_209.mp2 ...
wearing=5,0`, him up in the yellow hard hat (`1-line.png`), `tags` reading `tag=75/picture=2/subject=0` with
`mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 75 subject 0 item
0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=522 second=hidden erase=mb_erase` and the text
(`2-bar.png`). The button clicked: `button type 10 opens the buy screen on tab 1 and takes tag 75 off`,
`windows` reading `ParkBuyScreen tab 1`, `standing=0`, and the frame "Buy Shop" listing the three on offer
(`3-screen.png`). *`noshop29`* (`run-d29`, off-screen): `score=0 ... tick=965 result=no-score ridekinds=1
shopkinds=0 sincebuild=29`, then `days=48 score=146 ... tick=1120 result=post ... sincebuild=36`, said on that
tick. *`shop30`*: `no-score ridekinds=1 shopkinds=1 sincebuild=30`. *`noride30`*: `no-score ridekinds=0
shopkinds=0 sincebuild=30`; neither said it by tick 994.

**Main's build** on `noshop30` (`run-control`): no line of message 73, no attempt, no tag by tick 993.

**The original** (`q337/orig/run.sh <file>`, `advisor.py ... 0x49,0x47,0x48,0x4a,0x4b,0x3c,0x3e,0x3f,0x2,0x71,0xbc,0x11c
poll` beside `thingwords.py ... 5:0x21c30:Q:lastbuilt`, `tags.py`, `bar.py` and `screens.py`; four programs, four
loads). *`noshop30`* (run a): message `0x49`'s record went from (0, -1, 0, 0) to **(965, 0, 1, 0) on tick 965**,
one tag with it at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text string 77; he is up in the
yellow hard hat (`orig/a-g10.png`). The tag clicked: the bar's data read thing 0, buttons 10 and 0, item 0 and
the text (`orig/a-bar.png`). The button clicked: `screens.py`'s buy screen went from `0x0` to a pointer on tick
1055 and the tag was gone on 1057, the frame "Buy Shop" listing the same three shops (`orig/a-button.png`).
*`noshop29`* (run b): the record fresh until **(1120, 0, 1, 0) on tick 1120**, the same tag with it.
*`shop30`* (run c): no record of the twelve moved and no slot changed through tick 1158. *`noride30`* (run
d): row 62's record went to (959, 0, 1, 0) on tick 959 with its own tag, as "A park with no ride in it" has it,
and `0x49`'s stayed fresh through tick 1170. Rows 72 and 74 (`0x48`, `0x4a`) stayed fresh in all four.

| | The original | OpenTPW |
|---|---|---|
| Row 73 | polled; nought with no kind of ride standing, until 30 days after the newest build, and with a kind of shop standing; then 50 and 2 for each day of the park's age | the same |
| The Drinks Shop sold, day 30 | said on tick 965, line 0, the hard hat | the same (score 132) |
| Day 29 | nought on tick 965; said on tick 1120 | the same (score 146) |
| A shop standing | not said | the same (`shopkinds=1`) |
| No ride either | not said; row 62's line on tick 959 | the same (`ridekinds=0`) |
| The tag and the bar | row 75, part 2, string 77; button 10 builds the buy screen on its shops tab, tag off | the same |
| A golden-ticket shop or ride | no kind standing | the same (`ParkPeople.ShopKinds`, `RideKinds`; by test alone) |

**Not built or not seen.** **The original's stamp moved with nothing bought twice more**: to tick 1158's on
tick 1158 in run c's park and to tick 1170's on tick 1170 in run d's, the last line of each log (Q336's moved
on 1085, Q335's on 1400); what was made is still not read ("The newest build"), and here nothing moves it, so
past such a moment this game's row 73 would go on speaking where the original's waits thirty days more. In no
game run: line 2 (sample 210; the test holds the turn), the line said again past its 120 quarter-ticks, a
golden-ticket shop or ride standing (no Lost Kingdom shop costs one), a shop built after the line (the test's
last step), the bar's button with the buy screen open, a park made with no file, Full Simulation: the mode is
not asked by row 73's score. Not heard in either game.

## Too few shops for the park's size (message `0x4a`, Q338)

**Row 74, message `0x4a`, `ShopsBuildMoreShops`:** polled, category 0, either game mode, one copy, first response
213, two lines in turn. Responses 213 and 214 are samples 211 and 212 ("You haven't gotten many shops considering
the size of your park, have you thought about building some more?"; "A park this size could use a few more
shops ..."): the random gesture chain, model slot 0, **prop 5, the hard hat**, group 76. It is the polled row
after row 73: from the shipped park's cursor its turns are ticks 811, 966, 1121 and every 155 on.

**Its score, `0x0059de80`** (loose code, disassembled for this; `ECX` the balance object), each step on the park
analyser (`FUN_00519510`):

1. The kinds of shop standing, `0x004ca7b0( 1, 0 )` ("A park with rides and no shop"), kept in a local. None:
   nought. A park with no shop is row 73's.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30**: nought.
3. **`0x004ca7b0( 1, 0 )` again, the kinds of shop standing a second time** (`0x0059dec7`: `PUSH 0`, `PUSH 1`,
   the first call's own arguments), times the float `[ECX + 0x270]`, `ShopsBuildMoreShops.ShopsPerRideRatio`,
   **0.15**, compared with step 1's count (`FILD` the first, `FILD` the second, `FMUL`, `FCOMPP`, `TEST AH,0x41`,
   `JNZ`): not more than it, nought. By the key's name the second count was meant for the rides (`0x004ca7b0( 0,
   0 )`, as row 73 asks it); as compiled no ride is counted, and both sides are one number.
4. The bank's balance (`FUN_005195d0`'s thing's `+0xc`, `FUN_006ad810`) not over `[ECX + 0x274]`,
   `ShopsBuildMoreShops.WealthMoreThan`, **20000** (a signed `JLE`): nought.
5. Otherwise `[ECX + 0x278]`, `ShopsBuildMoreShops.Score`, **70**.

**So with the shipped `Advisor.sam` the row can never score**: step 1 leaves at least one kind, and a number of
kinds times 0.15 is never more than itself. The two lines, their tag and its string are in the game's files and
are heard and seen in no park. Under a ratio above one the row scores 70 in any park with a shop, thirty days
after its newest build and with more than 20000 in the bank, whatever its rides. The door is not asked.

**Its tag** is row 76 of the tag table: key 383, picture 2, string 78, the bar's first button of type 10 (the buy
screen on its shops tab), no second, thirty seconds. String 78 reads "A park of this size should have more shops
in it. Maybe you should build some more."

### OpenTPW (`ParkAdvisorMessages.BuildMoreShops`, a topic's `ShopsPerRide`; `ParkFacts.Balance`; tag row 76)

An eighteenth polled topic, scored as the original scores it, the kinds of shop on both sides, so it is silent
under the shipped ratio and speaks under one above one: **the original's own slip is kept, not mended**. The
topic field `ShopsPerRide` names the section; the score reads `ParkFacts.ShopKindsStanding` and
`DaysSinceObjectBuilt`, kept for rows 71 and 73, and `Balance`, new, `ParkState.Balance` handed in by
`ParkPeople.AdvisorFacts`. The ratio is read as a float and the product taken in a double, as the x87 takes it. Step 1 is no test
of its own here: with no kind standing step 3 answers nought whatever the ratio, and a bug put back there could
not be told from the rule.
The poll line ends ` shopkinds=N ratio=R balance=N sincebuild=N`. `ParkTags.Rows` lists row 76.

**The scene**: Q337's `shop30.TPWS`, the stock park paused on tick 915 with nothing sold and the stamp of tick
273 written (`q338/shop30.TPWS`), whose bank holds 88,112 on tick 966; and `shop29.TPWS`, the same with the stamp
of tick 275, 691 ticks before row 74's turn. **Three `Advisor.sam`**: the shipped one; *ratio15*,
`ShopsPerRideRatio` 1.5; *rich*, 1.5 and `WealthMoreThan` 999999. This game reads a changed one from a private
game folder (`lib/gamefolder.py`, `parkrun.py`'s `GAME_FOLDER`); the original's own `Data/Advisor/Advisor.sam` is
swapped for a run and put back (`q338/orig/run.sh`, which refuses to start on a file that is not the shipped
one).

**Predicted first** (`q338/PREDICTION.txt`), both games, before any run. Every prediction of the game held on
its first run; one run missed by the file I made for it, said there as round 1b: `shop29` first held the stamp of
tick 274, Q337's number for row 73's turn, which is day 30 on tick 966, and the game said the line as its rule
has it (`run-d29-wrongfile`). Not predictions, and said there: the listing, the stock balance in a test, Q337's
silent runs of both games on this file.

**This game** (`q338/confirm.py stock|fix|d29|rich|control`). *The shipped ratio* (`run-stock1`, on the desktop):
the clock starts on 915; `poll message=74 row=74 days=41 score=0 threshold=25 tick=966 result=no-score
shopkinds=1 ratio=0.15 balance=88112 sincebuild=30`, no attempt and no tag by tick 996, nobody beside the gadget
and the mast bare (`1-end.png`). *ratio15* (`run-fix1`, on the desktop, five of five on its first run; `run-fix2`
on the commit's build): `poll message=74 row=74 days=41 score=70 threshold=25 tick=966 result=post shopkinds=1
ratio=1.5 balance=88112 sincebuild=30`, `post message=74 ... result=queued` and `attempt=1 message=74 subject=0
score=70 threshold=25 response=213 sample=211 line=0 tick=966`; `saying sp_211.mp2 ... wearing=5,0`, him up in
the yellow hard hat (`1-line.png`), `tags` reading `tag=76/picture=2/subject=0` with `mast=94,901,144,984` and
`tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 76 subject 0 item 0 bar: out=1
rect=486,1112,1101,1453 ... first=mb_buy/help=522 second=hidden erase=mb_erase` and the text (`2-bar.png`). The
button clicked: `button type 10 opens the buy screen on tab 1 and takes tag 76 off`, `windows` reading
`ParkBuyScreen tab 1`, and the frame "Buy Shop" (`3-screen.png`). *ratio15 on `shop29`* (`run-d29b`,
off-screen): `score=0 ... tick=966 result=no-score ... sincebuild=29`, then `days=48 score=70 ... tick=1121
result=post ... sincebuild=36`, said on that tick. *rich* (`run-rich`, off-screen): `score=0 ... tick=966
result=no-score shopkinds=1 ratio=1.5 balance=88112 sincebuild=30`, not said by tick 994.

**Main's build** under ratio15 (`run-control`): no line of message 74, no attempt, no tag by tick 995.

**The original** (`q338/orig/run.sh <file>` with `SAM=stock|ratio15|rich`, `advisor.py ... 0x4a,0x49,0x48,0x4b,...
poll` beside `tags.py`, `bar.py` and `screens.py`; five programs, each started on its own `Advisor.sam`, all on
`shop30`). *The shipped ratio* (runs a and a2): message `0x4a`'s record stayed (0, -1, 0, 0), no slot took it and
no tag stood, through tick 1111 (a2, its ticks stamped by three guests' places, lines on 966 and 979 among them).
*ratio15* (run b): the record went from fresh to **(966, 0, 1, 0) on tick 966**, 13.94 s after the load's
click; one tag with it at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text string 78; he is up in
the yellow hard hat (`orig/b-g11.png`), the bank reading 88,112 as this game's. The tag clicked: the bar's data
read thing 0, buttons 10 and 0, item 0 and the text. The button clicked: `screens.py`'s buy screen went from
`0x0` to a pointer on tick 1049 and the tag was gone on 1051, the frame "Buy Shop" (`orig/b-button.png`).
*rich* (runs c and c2): the record fresh, no slot and no tag through tick 1078 (c2). **So the float is read as
written, the comparison is "more than", the two counts are one, and the bank's limit is asked last.** Rows 72,
73 and 75 (`0x48`, `0x49`, `0x4b`) stayed fresh in all five.

| | The original | OpenTPW |
|---|---|---|
| Row 74 | polled; nought with no kind of shop standing, until 30 days after the newest build, unless the kinds of shop times the ratio are more than the kinds of shop, and unless the bank is over 20000; then 70 | the same |
| The shipped ratio, 0.15 | never said | the same (`score=0 ... ratio=0.15`) |
| A ratio of 1.5, day 30 | said on tick 966, line 0, the hard hat | the same (score 70) |
| Day 29 | not run | nought on tick 966; said on tick 1121 |
| `WealthMoreThan` over the bank | not said | the same |
| The tag and the bar | row 76, part 2, string 78; button 10 builds the buy screen on its shops tab, tag off | the same |
| The rides | not counted | the same (by test: fourteen rides beside one shop score nought) |

**Not built or not seen.** In no game run: line 2 (sample 212; the test holds the turn), the line said again
past its 120 quarter-ticks, day 29 in the original, a ratio of exactly one (the test: equal is not more), no
shop standing under a ratio above one (the test; row 73's park), a golden-ticket shop standing, a park made
with no file, Full Simulation: the mode is not asked by row 74's score. What the line would have been worth with
the rides counted, as its key names them, is not built: that would be a mend of the original, and is not this
game's to make without Alexah's word. Not heard in either game.

## A poor selection of sideshows (message `0x4b`, Q339)

**Row 75, message `0x4b`, `SideshowsPoorSelection`:** polled, category 0, either game mode, one copy, first response
215, two lines in turn. Responses 215 and 216 are samples 213 and 214 ("You don't have a very good selection of
sideshows ... maybe you should build some more"; "You don't have the best selection of sideshows I've ever seen.
Have you given any thought to building more?"): the random gesture chain, model slot 0, no prop, group 77. It is
the polled row after row 74: from the shipped park's cursor its turns are ticks 812, 967, 1122 and every 155 on.

**Its score, `0x0059df30`** (loose code, disassembled for this; `ECX` the balance object) is rows 60's and 71's
with the sideshows' counts, each step on the park analyser (`FUN_00519510`):

1. The kinds of sideshow on offer, `0x004ca570` (its one argument is not read; loose code, the twin of
   `0x004ca510` with a 2 for the 1): the catalogue's items whose `Info.WhichUIType` (`+0x4ac`) is 2, a sideshow,
   and whose control record is researched (`+0x10`). It does not ask the golden ticket. None: nought.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30** (a signed `JL`): nought.
3. The sideshows standing, `FUN_004ca8e0( 2, 0 )`, under `[ECX + 0x280]`,
   `SideshowsPoorSelection.MoreSideshowsThan`, **2**: nought.
4. The kinds standing, `0x004ca7b0( 2, 0 )`: the sideshow items whose control record counts at least one
   standing and whose `GoldenTicketCost` is nought, researched or not. Times 100 over step 1's count, an unsigned
   whole division, not under `[ECX + 0x27c]`, `SideshowsPoorSelection.PoorerThan`, **15**: nought.
5. Otherwise `[ECX + 0x284]`, `SideshowsPoorSelection.Score`, **45**.

The door is not asked, nor whether a sideshow is open. **No Lost Kingdom park can score it under the shipped
15**: the theme has five sideshows (the Jungle Spray 1303, the Laughing Hyenas 1304, the Strength Bird 1305, the
Giant Puzzle 1307 and the Arcade 1309, which Instant Action's catalogue leaves out for want of an `Easy_` file)
and none costs a golden ticket, so with everything researched one kind standing is 20 per cent, and with no kind
standing there is no sideshow to be two of. The shipped save has two researched, the Jungle Spray and the
Strength Bird: one kind standing is 50.

**Its tag** is row 77 of the tag table: key 383, picture 2, string 79, the bar's first button of type 11 (the buy
screen on its sideshows tab; help row 523, "Left-click to buy sideshows"), no second, thirty seconds. String 79
reads "Your park doesn't have a great selection of sideshows. You should build some more."

### OpenTPW (`ParkAdvisorMessages.PoorSideshowSelection`, `PoorOf: Shut.Sideshows`; `ParkFacts.SideshowKindsOnOffer`, `SideshowKindsStanding`; `ParkPeople.Kinds`; tag row 77)

A nineteenth polled topic, rows 60's and 71's rule asked of the sideshows: `ParkFacts.Kinds` answers the
sideshows' kinds on offer and standing, the sideshows and the least-number key `MoreSideshowsThan`.
`ParkPeople.Kinds` (the old `ShopKinds`, now asked with the kind) counts them from the catalogue's items, the
research flags and the park's own count of each item standing, with the original's two tests of the golden
ticket. The poll line ends ` kinds=K/T sideshows=N sincebuild=N`. `ParkTags.Rows` lists row 77; the bar's button
of type 11 was built with row 62's message's.

**The scene** (`q339/scene.py`, `q339/mk.py`): the stock park paused on tick 916 with a second Jungle Spray
bought on (41,22) as thing 43, saved. Into copies the Laughing Hyenas and the Giant Puzzle are marked researched
(`original/research.py`: four on offer, all Instant Action's catalogue has) and the stamp written: `four30.TPWS`
the stamp of tick 275, 692 ticks before row 75's turn on tick 967, and `four29.TPWS` the stamp of tick 276.
Beside them `two30.TPWS`, the stamp alone (two on offer); `bird30.TPWS`, a Strength Bird bought in place of the
Spray (two kinds of four); and `one30.TPWS`, nothing bought (one sideshow). **Three `Advisor.sam`**: the shipped
one; *poor26*, `SideshowsPoorSelection.PoorerThan` 26, under which one kind of four, 25, is poor; *poor25*, under
which it is not. This game reads a changed one from a private game folder (`lib/gamefolder.py`); the original's
own is swapped for a run and put back (`q339/orig/run.sh`, `SAM=`).

**Predicted first** (`q339/PREDICTION.txt`), both games, before any run of either, the scene runs included; every
prediction held, in one round. Not predictions, and said there: the listing, and the two kinds on offer and the
theme's five sideshows, read from a test's printout and the wads.

**This game** (`q339/confirm.py fix|edge|stock|d29|two|bird|one|control`). *poor26 on `four30`* (`run-fix1`, on
the desktop, five of five on its first run; `run-fix2` on the commit's build): `poll message=75 row=75 days=41
score=45 threshold=25 tick=967 result=post kinds=1/4 sideshows=2 sincebuild=30`, `post message=75 ...
result=queued` and `attempt=1 message=75 subject=0 score=45 threshold=25 response=215 sample=213 line=0
tick=967`; `saying sp_213.mp2 ... wearing=0,0`, him up with his antennae (`1-line.png`), `tags` reading
`tag=77/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked:
`message bar: tag 77 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=523 second=hidden
erase=mb_erase` and the text (`2-bar.png`). The button clicked: `button type 11 opens the buy screen on tab 2
and takes tag 77 off`, `windows` reading `ParkBuyScreen tab 2`, and the frame "Buy Sideshow" listing the four on
offer (`3-screen.png`). The rest off-screen. *poor25* (`run-edge`) and *the shipped 15* (`run-stock`), both on
`four30`: `score=0 ... tick=967 result=no-score kinds=1/4 sideshows=2 sincebuild=30`, not said by ticks 998 and
995. *poor26 on `four29`* (`run-d29`): `no-score ... sincebuild=29` on 967, then `days=48 score=45 ... tick=1122
result=post kinds=1/4 sideshows=2 sincebuild=36`, said on that tick. *`two30`*: `no-score kinds=1/2 sideshows=2
sincebuild=30`. *`bird30`*: `no-score kinds=2/4 sideshows=2 sincebuild=30`. *`one30`*: `no-score kinds=1/4
sideshows=1 sincebuild=30`; none of the three said it by tick 995.

**Main's build** under poor26 on `four30` (`run-control`): no line of message 75, no attempt, no tag by tick 997.

**The original** (`q339/orig/run.sh <file>` with `SAM=stock|poor26|poor25`, `advisor.py ...
0x4b,0x4c,0x4d,0x4a,0x49,0x47,0x3c,0x3e,0x71,0xbc,0x11c poll` beside `tags.py`, `bar.py`, `screens.py` and, in the
silent runs, `thingwords.py` on three guests' places to stamp the ticks; seven programs, each started on its own
`Advisor.sam`). *poor26 on `four30`* (run a): message `0x4b`'s record went from (0, -1, 0, 0) to **(967, 0, 1, 0)
on tick 967**, 13.54 s after the load's click; one tag with it at (84, 909, 154, 974), the mast at (94, 901,
144, 984), its text string 79; he is up with no hat (`orig/a-g10.png`). The tag clicked: the bar's data read
thing 0, buttons 11 and 0, item 0 and the text. The button clicked: `screens.py`'s buy screen went from `0x0` to
a pointer on tick 1060 and the tag was gone by 1062, the frame "Buy Sideshow" listing the same four
(`orig/a-button.png`): **the original keeps a sideshow's researched byte as written.** *poor26 on `four29`* (run
b): the record fresh on ticks 971 and 984 and **(1122, 0, 1, 0) on tick 1122**, the tag with it. *poor25 on
`four30`* (run c): fresh, no slot and no tag through tick 1240. *poor26 on `two30`* (run d), *`bird30`* (run e)
and *`one30`* (run f): the same through ticks 1238, 1241 and 1230. *The shipped 15 on `four30`* (run g): the same
through tick 1242. Rows 76 and 77 (`0x4c`, `0x4d`) stayed fresh in all seven, and so did the rest read, but the message bar's and the buy
screen's own lines at run a's two clicks (`0x11c` on tick 1041, `0xbc` on 1061), as this game posts them.

| | The original | OpenTPW |
|---|---|---|
| Row 75 | polled; nought with no kind of sideshow on offer, until 30 days after the newest build, and under two sideshows; then 45 where the kinds standing are under 15 per cent of those on offer | the same |
| The shipped 15, one kind of four | never said | the same (`score=0 ... kinds=1/4`) |
| `PoorerThan` 26, two Jungle Sprays, four on offer, day 30 | said on tick 967, line 0, no hat | the same (score 45) |
| `PoorerThan` 25 (25 per cent is not under 25) | not said | the same |
| Day 29 | fresh on tick 967; said on tick 1122 | the same |
| Two on offer (50 per cent) | not said | the same (`kinds=1/2`) |
| Two kinds standing (50 per cent) | not said | the same (`kinds=2/4`) |
| One sideshow | not said | the same (`sideshows=1`) |
| The tag and the bar | row 77, part 2, string 79; button 11 builds the buy screen on its sideshows tab, tag off | the same |
| A golden-ticket sideshow | on offer once researched; never a kind standing | the same (`ParkPeople.Kinds`; by test alone) |

**Not built or not seen.** The row is said in no Lost Kingdom park under the shipped key, in either game: every
run in which it speaks is under a changed `Advisor.sam`. The four other rows that read the newest build (77 to
79 and 81) are built since, each under its own heading below, and row 76 (`SideshowsResearchMore`, which stayed
fresh in every run here) is "Every sideshow built, and more to research". Whether the original's
`mLastObjectBuilt` moved in these runs was not polled ("A poor selection of shops", above, has it moving with
nothing bought). In no game run: line 2 (sample 214; the test holds the turn), the line said again past its 120
quarter-ticks, a golden-ticket sideshow (no Lost Kingdom sideshow costs one), the Arcade (the regular
catalogue's fifth: one kind of five is 20), a kind researched in the park (research is built since Q701; not
run here, the files' bytes were written), a thing built after the line, the bar's button with the buy screen open, a park made with
no file, Full Simulation: the mode is not asked by row 75's score. Not heard in either game.

## A park with shops and no sideshow (message `0x4d`, Q340)

**Row 77, message `0x4d`, `SideshowsBuildSideshows`:** polled, category 0, either game mode, one copy, first
response 219, two lines in turn. Responses 219 and 220 are samples 217 and 218 ("Your park doesn't have any
sideshows ... have you thought about building some?"; "Tell me it's not true, your park has no sideshows. Do you
think perhaps you should build some?"): the random gesture chain, model slot 0, **prop 5, the hard hat**, group
79. It is two polled rows after row 75: from the shipped park's cursor its turns are ticks 814, 969, 1124, 1279
and every 155 on.

**Its score, `0x0059e070`** (loose code, disassembled for this; `ECX` the balance object) is row 73's a kind
on, each step on the park analyser (`FUN_00519510`):

1. The kinds of shop standing, `0x004ca7b0( 1, 0 )`: the shop items whose control record counts at least one
   standing and whose `GoldenTicketCost` is nought. None: nought. The rides are not asked.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30** (a signed `JL`): nought.
3. The kinds of sideshow standing, `0x004ca7b0( 2, 0 )`: any, nought.
4. Otherwise `[ECX + 0x28c]` plus `[ECX + 0x290]` times the park's age in whole days (`FUN_004f8800` over
   `0xC92A69C000`, as rows 62 and 73 count them): `SideshowsBuildSideshows.Score` and
   `SideshowsBuildSideshows.AdditionalPerDay` (`lib/advisorschema.py`), **-70 and 2** in `Advisor.sam`.

The door is not asked, nor what is on offer. The score starts seventy in debt, so the thirty days are not its
whole wait as they are row 73's: it is nought or under until day 35, 14 on tick 969 (day 42), which is not above
`MinScoreForConsideration`, 25, and **26 on tick 1124 (day 48)**, the first of its turns it can be said on; 40 on
tick 1279 (day 55).

**Its tag** is row 79 of the tag table: key 383, picture 2, string 81, the bar's first button of type 11 (the buy
screen on its sideshows tab; help row 523), no second, thirty seconds. String 81 reads "Your park has no
sideshows yet. Now might be a good time to invest in some." in the English folder this game reads, and "Your
park doesn't have any sideshows yet. ..." in the american one the original reads.

**Row 76, message `0x4c`, `SideshowsResearchMore`,** is "Every sideshow built, and more to research", below.

### OpenTPW (`ParkAdvisorMessages.BuildSideshows`, a topic's `PerDayWithNoSideshow`; tag row 79)

A twentieth polled topic. The topic field `PerDayWithNoSideshow` names the per-day key; the score reads
`ParkFacts.ShopKindsStanding`, `SideshowKindsStanding` and `DaysSinceObjectBuilt`, all kept for rows 71, 73 and
75, and the park's age as row 62 does. The poll line ends ` shopkinds=N sideshowkinds=N sincebuild=N`.
`ParkTags.Rows` lists row 79; the bar's button of type 11 was built with row 62's message's.

**The scene** (`q340/scene.py`, `q340/mk.py`): the stock park paused on tick 914 with its one sideshow, the
Jungle Spray (thing 14), sold, saved. Into copies of the file the stamp is written: `noside30.TPWS` the stamp of
tick 277, 692 ticks before row 77's turn on tick 969, and `noside29.TPWS` the stamp of tick 433, 691 before its
turn on 1124. Beside them `side30.TPWS`, nothing sold (saved on 915), and `noshop30.TPWS`, the Drinks Shop
(thing 16) sold too, each with tick 277's stamp.

Two more for the original: `late30.TPWS`, the same park paused and saved on tick 1082 with the
stamp of tick 432, 692 before the turn on 1124, and `late29.TPWS`, tick 433's.

**Predicted first** (`q340/PREDICTION.txt`), both games, before any run of either on each round's files, the
scene runs included; three rounds. This game's lines held in every round. The original's missed in rounds 1 and
2, each time by the line named weakest, the stamp moving with nothing bought; round 3's held.

**This game** (`q340/confirm.py fix|d29|side|noshop|control|late|late29`). *`noside30`* (`run-fix1`, on the
desktop, seven of seven on its first run): `poll message=77 row=77 days=42 score=14 threshold=25 tick=969
result=under-threshold shopkinds=1 sideshowkinds=0 sincebuild=30`, nothing posted; then `days=48 score=26 ...
tick=1124 result=post shopkinds=1 sideshowkinds=0 sincebuild=36`, `post message=77 subject=0 score=26 ...
result=queued` and `attempt=1 message=77 subject=0 score=26 threshold=25 response=219 sample=217 line=0
tick=1124`; `saying sp_217.mp2 ... wearing=5,0`, him up in the yellow hard hat (`1-line.png`), `tags` reading
`tag=79/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked:
`message bar: tag 79 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=523 second=hidden
erase=mb_erase` and the text (`2-bar.png`). The button clicked: `button type 11 opens the buy screen on tab 2
and takes tag 79 off`, `windows` reading `ParkBuyScreen tab 2`, and the frame "Buy Sideshow" listing the two on
offer, the Jungle Spray and the Strength Bird (`3-screen.png`). *`late30`* (`run-late1`, on the desktop, six of
six on its first run): the clock from 1082, `days=48 score=26 ... tick=1124 result=post ... sincebuild=30`, the
same line, hat, tag, bar and screen. The rest off-screen. *`noside29`* (`run-d29`): `no-score ... tick=969 ...
sincebuild=23`, no line on 1124, then `days=55 score=40 ... tick=1279 result=post ... sincebuild=36`, said on
that tick. *`late29`*: `no-score ... tick=1124 ... sincebuild=29`, then the same on 1279. *`side30`*: `no-score
shopkinds=1 sideshowkinds=1 sincebuild=30` (its first try loaded nothing, "0 saved parks", the harness's
miss; run again). *`noshop30`*: `no-score shopkinds=0 sideshowkinds=0 sincebuild=30`; neither said it by tick
1151.

**Main's build** on `noside30` (`run-control`): no line of message 77, no attempt, no tag by tick 1152.

**The original** (`q340/orig/run.sh <file>`, `advisor.py ... 0x4d,0x4c,0x4b,0x4e,0x4a,0x49,0x47,0x3c,0x3e,0x71,0xbc,0x11c
poll` beside `thingwords.py ... 5:0x21c30:Q:lastbuilt`, `tags.py`, `bar.py` and `screens.py`; eight loads).
**Its stamp moved with nothing bought in every load**: in the four files saved on tick 914 or 915 to tick
1089's on tick 1089 (run a, `noside30`), to 1139's and then 1408's (run b, `noside29`), to 1094's (run c,
`side30`) and to 1069's (run d, `noshop30`); in the two saved on tick 1082 to tick 1094's on tick 1094, twelve
ticks after the load (runs a2 and b2), so it is an event of the park and not of the load. Message `0x4d`'s
record stayed fresh (0, -1, 0, 0) with no slot through every one of those six, tick 969's 14 among them: the
thirty days begin again at each move. *`late30`, nothing held, 370 s* (run n): the stamp to tick 1094's and to
**tick 1434's on tick 1434**; the record fresh on its turns of ticks 1124 to 2054 and **(2209, 0, 1, 0) on tick
2209**, its first turn 692 ticks past the last move (score 120 on day 95), predicted as one of two; the tag
with it reading the american string 81, him up in the yellow hard hat (`orig/n-told.png`). *`late30` with the
stamp held* (run h; `thingpoke.py ... 13 5:0x21c30:Q=<the file's>`, a write to the original's memory: the move
on tick 1094 written back within 20 ms): the record **(1124, 0, 1, 0) on tick 1124**, one tag at (84, 909, 154,
974), the mast at (94, 901, 144, 984), the hard hat (`orig/h-g10.png`). The tag clicked: the bar's data read
thing 0, buttons 11 and 0, item 0 and the text. The button clicked: `screens.py`'s buy screen went from `0x0`
to a pointer on tick 1217 and the tag was gone on 1219, the frame "Buy Sideshow" listing the same two
(`orig/h-button.png`). Rows 76 and 78 (`0x4c`, `0x4e`) stayed fresh in all eight.

| | The original | OpenTPW |
|---|---|---|
| Row 77 | polled; nought with no kind of shop standing, until 30 days after the newest build, and with a kind of sideshow standing; then -70 and 2 for each day of the park's age | the same |
| Day 42, the Jungle Spray sold, day 30 of the stamp | 14: fresh, no slot | the same (`under-threshold`) |
| Day 48, day 30 of the stamp | said on tick 1124, line 0, the hard hat (the stamp held) | the same (score 26) |
| Day 29 of the stamp | not said on tick 1124 (the stamp moved on 1094) | `no-score ... sincebuild=29`; said on 1279 |
| A sideshow standing; no shop | not said | the same (`sideshowkinds=1`; `shopkinds=0`) |
| The tag and the bar | row 79, part 2, string 81; button 11 builds the buy screen on its sideshows tab, tag off | the same |
| **The stamp, nothing bought** | **moves near tick 1094 and again near tick 1434, so the line is first said on tick 2209** | moves where a leaver's summons draws the ferry or the seaplane (tick 1093 in `late30`), and not where it draws the bus: said on tick 1124 then ("What moves it with nothing bought") |
| A golden-ticket shop or sideshow | no kind standing | the same (`ParkPeople.Kinds`; by test alone) |

**Not built or not seen.** The original's stamp moves with nothing bought, twice in 370 s, 340 ticks apart, in
every load: the ferry and the seaplane, each made at its first summons, which stamps here too ("What moves it
with nothing bought"). **Where this game's draw is the bus it still says row 77's line 1,085 ticks before the
original does** (1124 for 2209 in `late30`), and rows 60, 71, 73 and 75 the same way. Row 76 is "Every sideshow built, and more to research", below; row 78 is "Too few sideshows for the park's size", below; rows 79 and 81 are "A poor selection of features" and "A park with sideshows and no feature", below. In no game run: line 2
(sample 218; the test holds the turn), the line said again past its 120 quarter-ticks, a golden-ticket shop or
sideshow standing (no Lost Kingdom one costs a ticket), a sideshow built after the line (the test's last step),
the bar's button with the buy screen open, a park made with no file, Full Simulation: the mode is not asked by
row 77's score. Not heard in either game.

## Too few sideshows for the park's size (message `0x4e`, Q343)

**Row 78, message `0x4e`, `SideshowsBuildMoreSideshows`:** polled, category 0, either game mode, one copy, first
response 221, two lines in turn. Responses 221 and 222 are samples 219 and 220 ("You don't have many sideshows
for a park this size, maybe you should build some more"; "... I'm worried that you could be looking at widespread
sideshow shortages in the future. Don't laugh, it could happen ..."): the random gesture chain, model slot 0,
**prop 5, the hard hat**, group 80. It is the polled row after row 77: from the shipped park's cursor its turns
are ticks 815, 970, 1125 and every 155 on.

**Its score, `0x0059e110`** (loose code, disassembled for this; `ECX` the balance object) is row 74's with the
second count the one its key names, each step on the park analyser (`FUN_00519510`):

1. The kinds of sideshow standing, `0x004ca7b0( 2, 0 )`, kept in a local. None: nought. A park with no
   sideshow is row 77's.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30** (a signed `JL`): nought.
3. **The kinds of ride standing, `0x004ca7b0( 0, 0 )`** (`0x0059e157`: `PUSH 0`, `PUSH 0`), times the float
   `[ECX + 0x294]`, `SideshowsBuildMoreSideshows.SideshowsPerRideRatio`, **0.2**, compared with step 1's count
   (`FILD` the sideshows' kinds, `FILD` the rides', `FMUL`, `FCOMPP`, `TEST AH,0x41`, `JNZ`): not more than it,
   nought. Where row 74 asks for the shops twice, this row does count the rides.
4. The bank's balance (`FUN_005195d0`'s thing's `+0xc`, `FUN_006ad810`) not over `[ECX + 0x298]`,
   `SideshowsBuildMoreSideshows.WealthMoreThan`, **20000** (a signed `JLE`): nought.
5. Otherwise `[ECX + 0x29c]`, `SideshowsBuildMoreSideshows.Score`, **80**.

**The product is a double of the float key.** A breakpoint on the `FCOMPP` (`0x0059e17b`) in the original
running the stock park (`q343/orig/fpu-load.log`) stopped on ticks 970 and 1125 with the x87's control word
**`0x27f`**, 53-bit precision, `ST0` 0.20000000298023224 (one kind of ride times the float nearest 0.2) and `ST1`
1 (one kind of sideshow). So five kinds of ride times that float are 1.0000000149, which **is more than one kind
of sideshow**; rounded to a float the product would be one, and not more. Under the shipped ratio the row scores
at five kinds of ride to each kind of sideshow.

**An Instant Action park scores the row only once research brings a fifth kind of ride**: its catalogue holds twelve
kinds, four of them on offer at the start (items 1100, 1101, 1102 and 1104), and the lab's research tick tests no
game type (`0x00503430`, completion `0x00504630`), so a fifth can come; four times 0.2 is under one. The Full Simulation catalogue holds seventeen, and there
five kinds of ride beside one kind of sideshow, thirty days after the newest build and with more than 20000 in
the bank, score 80. The door is not asked.

**Its tag** is row 80 of the tag table: key 383, picture 2, string 82, the bar's first button of type 11 (the buy
screen on its sideshows tab), no second, thirty seconds. String 82 reads "You haven't got many sideshows for a
park of this size. Maybe you should build some more?" in `Language/English` and "You don't have many sideshows
..." in the american folder the reference install reads.

### OpenTPW (`ParkAdvisorMessages.BuildMoreSideshows`, a topic's `SideshowsPerRide`; tag row 80)

A twenty-first polled topic, scored as the original scores it. The topic field `SideshowsPerRide` names the
section; the score reads `ParkFacts.SideshowKindsStanding`, `RideKindsStanding`, `DaysSinceObjectBuilt` and
`Balance`, all kept for earlier rows. The ratio is read as a float and the product taken in a double, as the x87
takes it. The poll line ends ` sideshowkinds=N ridekinds=N ratio=R balance=N sincebuild=N`. `ParkTags.Rows` lists
row 80.

**The scenes**: Q338's `shop30.TPWS` (the stock park on tick 915, one kind each of ride, shop and sideshow, the
stamp of tick 273); `show29.TPWS`, the same with the stamp of tick 279, 691 ticks before row 78's turn; Q340's
`noside30.TPWS` (the Jungle Spray sold); and three made for this with `lib/stockscene.py`, each with the stamp of
tick 278: `noride30` (the Belly Bounce sold), `three30` (a Crazy Ape and a Rocky Racers bought and left shut:
three kinds of ride) and `four30` (an Aztec Mayhem too: four, all the mode offers). **Five `Advisor.sam`**: the
shipped one; *1.5*; *rich*, 1.5 and `WealthMoreThan` 999999; *0.33333334*, the float `0x3eaaaaab`, whose three
times is 1.0000000298 as a double and one as a float; *0.25*, a float exactly. This game reads a changed one from
a private game folder; the original's own is swapped for a run and put back (`q343/orig/run.sh`).

**Predicted first** (`q343/PREDICTION.txt`), both games, in two rounds, each written before its runs; round 2
after the breakpoint's one look at the original. Every prediction held on its first run in both games. Not
predictions, and said there: the listing, the control word, the catalogue's four kinds.

**This game** (`q343/confirm.py <mode>`; the first two on the desktop, the rest off-screen). *The shipped ratio*
(`run-stock1`): `poll message=78 row=78 days=42 score=0 threshold=25 tick=970 result=no-score sideshowkinds=1
ridekinds=1 ratio=0.2 balance=88112 sincebuild=30`, no attempt and no tag by tick 999, nobody beside the gadget
and the mast bare (`1-end.png`). *1.5* (`run-fix1`, five of five): `poll message=78 row=78 days=42 score=80
threshold=25 tick=970 result=post sideshowkinds=1 ridekinds=1 ratio=1.5 balance=88112 sincebuild=30`, `post
message=78 ... result=queued` and `attempt=1 message=78 subject=0 score=80 threshold=25 response=221 sample=219
line=0 tick=970`; `saying sp_219.mp2 ... wearing=5,0`, him up in the yellow hard hat (`1-line.png`), `tags`
reading `tag=80/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag
clicked: `message bar: tag 80 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=523
second=hidden erase=mb_erase` and the text. The button clicked: `button type 11 opens the buy screen on tab 2 and
takes tag 80 off`, `windows` reading `ParkBuyScreen tab 2`, and the frame "Buy Sideshow" (`3-screen.png`). *1.5 on
`show29`* (`run-d29`): `score=0 ... tick=970 result=no-score ... sincebuild=29`, then `days=48 score=80 ...
tick=1125 result=post ... sincebuild=36`, said on that tick (the leavers' summons of tick 1093 drew the bus; a
ferry or a seaplane made there would have stamped the build and kept it silent, as the prediction says). *rich*
(`run-rich`): `score=0 ... ratio=1.5 balance=88112 sincebuild=30`. *1.5 on `noride30`* (`run-noride`): `score=0
... sideshowkinds=1 ridekinds=0 ratio=1.5 balance=88857`. *1.5 on `noside30`* (`run-noside`): `score=0 ...
sideshowkinds=0 ridekinds=1 ratio=1.5 balance=89862`. *The shipped ratio on `four30`* (`run-four`): `score=0 ...
sideshowkinds=1 ridekinds=4 ratio=0.2 balance=82872`. *0.33333334 on `three30`* (`run-third`): `score=80 ...
tick=970 result=post sideshowkinds=1 ridekinds=3 ratio=0.33333334 balance=85362 sincebuild=30`, said on that
tick. *0.25 on `four30`* (`run-quarter`): `score=0 ... ridekinds=4 ratio=0.25`. None of the silent runs said the
line or left tag 80 by tick 998.

**Main's build** under 1.5 (`run-control`): no line of message 78, no attempt, no tag by tick 998.

**The original** (`q343/orig/run.sh <file>` with `RATIO=` and `WEALTH=`: `advisor.py ... 0x4e,0x4d,0x4c,0x4f,...
poll` beside `tags.py`, `bar.py`, `screens.py` and two guests' places for the ticks; eight programs, each started
on its own `Advisor.sam`). *The shipped ratio, `shop30`* (run a): message `0x4e`'s record stayed (0, -1, 0, 0),
no slot took it and no tag stood, through tick 1159. *1.5* (run b): the record went from fresh to **(970, 0, 1,
0) on tick 970**, 14.95 s after the load's click; one tag with it at (84, 909, 154, 974), the mast at (94, 901,
144, 984), its text the american string 82; he is up in the yellow hard hat (`orig/b-g11.png`), the bank reading
88,112 as this game's. The tag clicked: the bar's data read thing 0, buttons 11 and 0, item 0 and the text. The
button clicked: `screens.py`'s buy screen went from `0x0` to a pointer on tick 1049 and the tag was gone on 1052,
the frame "Buy Sideshow" with the Jungle Spray and the Strength Bird (`orig/b-button.png`). *rich* (run c): fresh
through tick 1149. *1.5 on `noride30`* (run d): fresh through tick 1194, row 62's line about a park with no ride
said on 959: **the rides are counted**, where the sideshows set against themselves would have spoken. *1.5 on
`noside30`* (run e): fresh through 1217. *The shipped ratio on `four30`* (run f): fresh through 1200. *0.33333334
on `three30`* (run g): **(970, 0, 1, 0) on tick 970**, the tag with it: **the product is not rounded to a float**,
and a ride bought and left shut is a kind standing. *0.25 on `four30`* (run h): fresh through 1200: equal is not
more. In f and h a tag stood from tick 996, row 113's, the bought rides' queues joined to nothing.

| | The original | OpenTPW |
|---|---|---|
| Row 78 | polled; nought with no kind of sideshow standing, until 30 days after the newest build, unless the kinds of ride times the ratio are more than the kinds of sideshow, and unless the bank is over 20000; then 80 | the same |
| The shipped ratio, 0.2, one kind of ride | not said | the same (`score=0 ... ratio=0.2`) |
| The shipped ratio, four kinds of ride | not said | the same (`ridekinds=4`) |
| A ratio of 1.5, day 30 | said on tick 970, line 0, the hard hat | the same (score 80) |
| The same with the ride sold, or the sideshow | not said | the same |
| Three kinds of ride under 0.33333334 | said on tick 970 | the same |
| Four kinds under 0.25 | not said | the same |
| Day 29 | not run | nought on tick 970; said on tick 1125 |
| `WealthMoreThan` over the bank | not said | the same |
| The tag and the bar | row 80, part 2, string 82; button 11 builds the buy screen on its sideshows tab, tag off | the same |
| The tag's words | the american folder's, "You don't have many sideshows ..." | the English folder's, "You haven't got many sideshows ..." |

**Not built or not seen.** In no game run: **five kinds of ride under the shipped ratio**, the one park in which
the shipped game says the line, which needs Full Simulation and its research (the test holds it, and the
breakpoint and run g are its evidence in the original); line 2 (sample 220; the test holds the turn), the line
said again past its 120 quarter-ticks, day 29 in the original, a golden-ticket ride or sideshow standing, a
sideshow built after the line, the bar's button with the buy screen open, a park made with no file. Row 79 is
"A poor selection of features", below, and row 81 "A park with sideshows and no feature"; row 76 is "Every
sideshow built, and more to research". Not heard in either game.

## A poor selection of features (message `0x4f`, Q344)

**Row 79, message `0x4f`, `FeaturesPoorSelection`:** polled, category 0, either game mode, one copy, first
response 223, two lines in turn. Responses 223 and 224 are samples 221 and 222 ("You don't have a very good
selection of features, you know: trees, fountains, statues, things to make your park look nice. Maybe you need to
build some more. You can find them in the miscellaneous items list on the buy screen"; "The park has precious few
features. It's like Nebraska with a gate ..."): the random gesture chain, **model slot 1 and prop -1, the theme's
own advisor and its costume**, group 81. It is the polled row after row 78: from the shipped park's cursor its
turns are ticks 816, 971, 1126 and every 155 on.

**Its score, `0x0059e1c0`** (loose code, disassembled for this; `ECX` the balance object) is row 75's with a 3
for each 2, each step on the park analyser (`FUN_00519510`):

1. The kinds of feature on offer, `0x004ca5d0` (loose code, the twin of `0x004ca570` with a 3 for the 2): the
   catalogue's items whose `Info.WhichUIType` (`+0x4ac`) is 3, a feature, and whose control record is researched
   (`+0x10`), a golden ticket's or not. None: nought.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30** (a signed `JL`): nought.
3. The features standing, `FUN_004ca8e0( 3, 0 )`, under `[ECX + 0x2a4]`, `FeaturesPoorSelection.MoreFeaturesThan`,
   **5** (a signed `JL`): nought.
4. The kinds of feature standing, `0x004ca7b0( 3, 0 )`, times 100 and divided by step 1's count (`DIV`, whole
   numbers), not below `[ECX + 0x2a0]`, `FeaturesPoorSelection.PoorerThan`, **20** (`JNC`): nought.
5. Otherwise `[ECX + 0x2a8]`, `FeaturesPoorSelection.Score`, **20**.

**So the shipped game never says the line, in any park**: a polled row is posted only where its score is over
`GeneralAdvisor.MinScoreForConsideration`, 25, and the row's whole score is 20. The stock park does not reach the
score either: eight kinds of feature are researched in its file (items 1401, 1402, 1403, 1406, 1411, 1413, 1425
and 1428, the same eight in both catalogues), five of them stand (the toilet, 1402, three times, 1413 twice,
1403, 1406 and 1411), eight things, and five of eight are 62.

**Its tag** is row 81 of the tag table: key 383, picture 2, string 83 ("If you built some more features your
park would look better, and your visitors might be happier."), the bar's first button of type 12 (the buy screen
on its fourth tab, the features'), no second, thirty seconds.

### OpenTPW (`ParkAdvisorMessages.PoorFeatureSelection`, `PoorOf: Shut.Features`; `ParkFacts.Features`, `FeatureKindsOnOffer`, `FeatureKindsStanding`; tag row 81)

A twenty-second polled topic, scored as the original scores it: row 60's topic field with `PoorOf` the features,
whose section's least number is `MoreFeaturesThan`. `ParkPeople.AdvisorFacts` counts the things of a feature's item
and asks `ParkPeople.Kinds` for the kinds, as it does for the shops and the sideshows. The poll line ends `
kinds=K/T features=N sincebuild=N`. `ParkTags.Rows` lists row 81.

**Who says it.** The responses' model slot 1 and prop -1 are the theme's own advisor and costume: in Lost
Kingdom the fly in the explorer's hat, no antennae (`q344/orig/a-told.png`), in both games since Q346 ("The
theme's own advisor and its costume"). This item's own runs, below, were of the build before that, which said the
line with the lobby's model, bare-headed, and counted `ADVISOR_THEME_COSTUME`.

**The scenes**: Q338's `shop30.TPWS` (the stock park on tick 915, the stamp of tick 273: day 30 on tick 971) and
`feat29.TPWS`, the same with the stamp of tick 280 (`q344/mk.py`). **Seven `Advisor.sam`**, the section's three
keys: the shipped one (20, 5, 20); *63* (`PoorerThan` 63, `Score` 26); *62* (62, 26); *Score 25* and *Score 20*
under 63; *eight* and *nine* (`MoreFeaturesThan` 8 and 9 under 63 and 26). This game reads a changed one from a
private game folder; the original's own is swapped for a run and put back (`q344/orig/run.sh`).

**Predicted first** (`q344/PREDICTION.txt`), both games, written before any run. Not predictions, and said there:
the listing, the keys, this game's own count of eight, five and eight. **Three misses, each mine and none the
rule's**: the buy button's help text on the features' tab is 524 where I wrote 523; the leavers' summons in the
day-29 run came on tick 1139 where I wrote 1093, so that run's check was written again and the run made again;
and who is up in the original is the fly in the theme's hat, where I guessed another figure.

**This game** (`q344/confirm.py <mode>`; the first two on the desktop, the rest off-screen). *63*
(`run-fix1`, four of five, the miss the help text's number): `poll message=79 row=79 days=42 score=26
threshold=25 tick=971 result=post kinds=5/8 features=8 sincebuild=30`, `post message=79 ... result=queued` and
`attempt=1 message=79 subject=0 score=26 threshold=25 response=223 sample=221 line=0 tick=971`; `saying
sp_221.mp2 ... wearing=-1,0`, `1x ADVISOR_THEME_COSTUME`, him up with no hat (`1-line.png`), `tags` reading
`tag=81/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked:
`message bar: tag 81 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=524 second=hidden
erase=mb_erase` and the text. The button clicked: `button type 12 opens the buy screen on tab 3 and takes tag 81
off`, `windows` reading `ParkBuyScreen tab 3`, and the frame "Buy Miscellaneous Items" (`3-screen.png`). *The
shipped keys* (`run-stock1`): `score=0 ... tick=971 result=no-score kinds=5/8 features=8 sincebuild=30`, nobody
up and the mast bare by tick 1000 (`1-end.png`). *62* (`run-p62`): the same nought. *Score 25* (`run-s25`):
`score=25 ... result=under-threshold`, no post. *Score 20* (`run-s20`): `score=20 ... result=under-threshold`.
*Eight* (`run-m8`): `score=26 ... result=post`, said on tick 971. *Nine* (`run-m9`): `score=0 ... no-score`.
*63 on `feat29`* (`run-d29`): `score=0 ... tick=971 result=no-score ... sincebuild=29`, then `days=48 score=26 ...
tick=1126 result=post ... sincebuild=36`, said on that tick; no vehicle was made before it. None of the silent
runs said the line or left tag 81 by tick 999.

**Main's build** under 63 (`run-control`): no line of message 79, no attempt, no tag by tick 1001.

**The original** (`q344/orig/run.sh <file>` with `POORER=`, `MORE=` and `SCORE=`: `advisor.py ...
0x4f,0x4e,0x4d,0x50,... poll` beside `tags.py`, `bar.py`, `screens.py` and two guests' places for the ticks; six
programs, each started on its own `Advisor.sam`, all on `shop30`). *63* (run a): message `0x4f`'s record went
from fresh to **(971, 0, 1, 0) on tick 971**, 14.6 s after the load's click; one tag with it at (84, 909, 154,
974), the mast at (94, 901, 144, 984), its text string 83; he is up in the explorer's hat (`orig/a-told.png`).
The tag clicked: the bar's data read thing 0, buttons 12 and 0, item 0 and the text. The button clicked:
`screens.py`'s buy screen went from `0x0` to a pointer on tick 1066 and the tag was gone on 1068, the frame "Buy
Miscellaneous Items" with the same ten rows as this game's (`orig/a-button.png`). *62* (run b): fresh through
tick 1221: **five kinds of eight, as this game counts them**. *The shipped keys* (run c): fresh through 1096.
*Score 25* (run d): fresh through 1226: 25 is not over 25. *Eight* (run e): (971, 0, 1, 0) on tick 971. *Nine*
(run f): fresh through 1219: **eight features**.

| | The original | OpenTPW |
|---|---|---|
| Row 79 | polled; nought with no kind of feature on offer, until 30 days after the newest build, with fewer than `MoreFeaturesThan` features, and where the kinds standing are `PoorerThan` per cent or more of those on offer; then `Score` | the same |
| The shipped keys (20, 5, 20) | not said: the stock park's share is 62, and 20 is not over 25 | the same (`score=0`; `score=20 ... under-threshold` under 63) |
| `PoorerThan` 63, `Score` 26, day 30 | said on tick 971, line 0 | the same (score 26) |
| 62 | not said | the same |
| `Score` 25 | not said | the same (`under-threshold`) |
| `MoreFeaturesThan` 8, and 9 | said on tick 971, and not said | the same |
| Day 29 | not run | nought on tick 971; said on tick 1126 |
| Who says it | the fly in the theme's explorer's hat, no antennae | the same since Q346 (row 81's line seen, not this one) |
| The tag and the bar | row 81, part 2, string 83; button 12 builds the buy screen on its features tab, tag off | the same |

**Not built or not seen.** In no game run: line 2 (sample
222; the test holds the turn), the line said again past its 120 quarter-ticks, day 29 in the original, no kind of
feature on offer, a golden-ticket feature, a feature built after the line, the bar's button with the buy screen
open, a park made with no file, Full Simulation (the row asks neither mode: not asked). Row 81 is "A park with sideshows and no feature", below, and row 76 is "Every
sideshow built, and more to research". Not heard in either game.

## Every feature built, and more to research (message `0x50`, Q348)

**Row 80, message `0x50`, `FeaturesResearchMore`:** polled, **category 4** (`MessageGroups[4]`: the same message
no sooner than 900 quarter-ticks on, "Fifteen minutes between each time you're told to go and research more
stuff" in `Advisor.sam`'s own words), **game mode 0: Full Simulation's alone**, one copy, first response 225,
**one line**. Response 225 is sample 223 (the transcripts file has it "the build all of the available features
maybe you should research some more"): the random gesture chain, model slot 0, props 10 and 0, the lobby's fly
in the research hat (costume id 10, FileFormats `models.md`), group 82. It is the polled row after row 79: from
the shipped park's cursor its turns are ticks 817, 972, 1127 and every 155 on.

**The mode is the poll's test, not the score's** (`FUN_0059c680`, "The poll", above): a row whose `+0x0c` is
nought is scored only where the game type `0x00fb3b7c` reads 0, and answers -1 where it reads 2, Instant
Action's. Thirty-one polled rows are so: 18 to 21, 23 to 27, 61, 63 to 65, 72, 76, 80, 101, 102, 171, 274 to 276,
278 to 281, 314, 316, 317, 347 and 348 (`meta_full.json`); the twenty-four built before this one are all mode 2.
**So the shipped Lost Kingdom park, an Instant Action one, never hears this line**, whatever stands in it.

**Its score, `0x0059e260`** (loose code, disassembled for this; `ECX` the balance object), each step on the park
analyser (`FUN_00519510`):

1. The researchers, `FUN_004c9960( 8 )` (the count rows 5 to 7 ask with 4 for the mechanics: a thing whose type
   byte is 8 and whose `+3` is nought). None: nought.
2. The kinds of feature standing, `0x004ca7b0( 3, 0 )` (row 79's count). None: nought.
3. The lab's projects of a feature's kind left undone, `0x004ca690` (loose code): the lab is the thing the
   world's `+0x1da722` names (`FUN_00519550`), and `FUN_00503b30( 3 )` on it counts, of its first `mNumProjects`
   (`+0x145c`) projects of twenty bytes from `+0xc`, those whose `mCat` (`+4`) is 3 and whose `mResearchDone`
   (`+0x10`) is under their `mCost` (`+0xc`), two floats set against each other on the x87 (`0x00503b55`). Asked
   with 5 it counts every kind's. None: nought.
4. The kinds of feature on offer, `0x004ca5d0( 0 )` (row 79's count). Not as many as stand (a `JNZ` at
   `0x0059e2ca`): nought. Otherwise `[ECX + 0x2ac]`, `FeaturesResearchMore.Score`, **60** in `Advisor.sam`
   (`lib/advisorschema.py`).

The park's age, the newest build, the door and the bank are not asked. Row 76's function (`0x0059dfd0`, "Every
sideshow built, and more to research", below) is this one of the sideshows with one test more, and is mode 0 too.

**The lab's projects are in the park file** (the lab's serialiser `FUN_00502dc0`: 256 projects by
`FUN_00504210`, five kinds' records by `FUN_00504760`, then `mNumProjects`, `mWorkLoad`,
`mMostExpensiveProject` and `mLabActive`; the bytes are FileFormats `saves.md`, "The research lab"). The shipped
Lost Kingdom park's lab holds **24 projects: 8 of rides, 3 of shops, 2 of sideshows and 11 of features**, none
done, and the eleven are exactly the features of the Instant Action catalogue its controls hold unresearched;
the eight researched are items 1401 to 1403, 1406, 1411, 1413, 1425 and 1428, of which five stand.

**The original's lab runs in an Instant Action park**: project 8, a feature (the Stone Head, cost 250), read 7.5
points on tick 962 and gained 1.5 every 20 ticks through every run below (`thingwords.py ... 6:0xbc:f`), 30.0 on
tick 1260.

**Its tag** is row 82 of the tag table: key 383, picture 2, string 84, **no bar button** (both nought), thirty
seconds. String 84 reads "All the available features have been built. Maybe you should research some more?" in
the English folder this game reads and in the american one the original reads.

### OpenTPW (`ParkAdvisorMessages.ResearchMoreFeatures`, a topic's `EveryFeatureBuilt`, `FullSimulationOnly`; `InstantAction`, `InstantActionHeld`; `ParkFacts.Researchers`, `FeatureProjectsUnfinished`; `ParkWorld.ResearchProjects`; `ParkResearch.Unfinished`; tag row 82)

A twenty-fifth polled topic, and the first that is one game's alone: the poll does not score a
`FullSimulationOnly` row in Instant Action and logs `score=-1 ... result=other-game`. `ParkWorld` reads the lab's
used projects from the file, `ParkResearch.Unfinished( kind )` counts those not done, and
`ParkPeople.AdvisorFacts` hands the score the researchers on the staff and the features' count. The poll line
ends ` researchers=N kinds=K/T unfinished=N`. `ParkTags.Rows` lists row 82.

**The lab's projects are the file's at the load, and the researchers' hand-outs finish them** (Q701,
`park-engine.md`, "The lab at work"), so a kind comes on offer as its projects are researched. **A park made with no file has the projects the original's
world setup gives it** (Q349; `park-engine.md`, "A fresh world's research lab"): 48 in Lost Kingdom, 20 of them
features', so the row speaks in the one kind of park whose game it is, on tick 50 of a fresh one with a
researcher hired and all eight kinds of feature built.

**The console's `advisorgame full|instant|own`** holds the poll's game type, whatever the player's is. It is an
instrument, the twin of holding the original's `0x00fb3b7c`: no screen of either game does it.

**The scenes** (`lib/stockscene.py`; the stock park paused on tick 962 to 964): *all8*, a Small Bush (1401), a
Small Tree (1425) and a Tiny Rock (1428) bought, so that all eight kinds on offer stand; *seven*, the first two
alone; *nores*, all8 with the researcher fired; *done*, all8 with the eleven feature projects' `mResearchDone`
written up to their `mCost` (`parkfile.thing_set` on thing 6).

**Predicted first** (`q348/PREDICTION.txt`), both games, two rounds. **Round 1 missed in the original, and the
miss is what found the mode**: built without it, this game said the line on tick 972 of *all8* as predicted,
and the original given the same file kept message `0x50`'s record fresh through tick 1260, over two turns
(`orig/round1/a-load.log`). The row's `+0x0c` was read after that. Round 2 was written before its runs, and
every line of it held in both games.

**This game** (`q348/confirm.py <mode>`; the first two on the desktop and off it, the rest off-screen). *all8*
with nothing held (`run-instant`): `poll message=80 row=80 days=42 score=-1 threshold=25 tick=972
result=other-game researchers=1 kinds=8/8 unfinished=11`, no attempt and no tag by tick 1003. *all8* with
`advisorgame full` (`run-fix2`, four of four on its first run): `score=60 ... tick=972 result=post researchers=1
kinds=8/8 unfinished=11`, `post message=80 ... result=queued` and `attempt=1 message=80 subject=0 score=60
threshold=25 response=225 sample=223 line=0 tick=972`; `saying sp_223.mp2 ... wearing=10,0 slot=0`, him up in
the white spiked hair of the research hat (`1-line.png`), `tags` reading `tag=82/picture=2/subject=0` with
`mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked: `message bar: tag 82 subject 0 item
0 bar: out=1 rect=486,1112,1101,1453 ... first=hidden second=hidden erase=mb_erase` and the text
(`2-bar.png`). Held the same way: *seven* (`run-seven`) `score=0 ... result=no-score researchers=1 kinds=7/8
unfinished=11`; *nores* (`run-nores`) `no-score researchers=0 kinds=8/8 unfinished=11`; *done* (`run-done`)
`no-score researchers=1 kinds=8/8 unfinished=0`. The stock park with nothing held (`run-stock`): `score=-1 ...
tick=817 result=other-game researchers=1 kinds=5/8 unfinished=11`. None of the silent runs said the line or
left tag 82.

**Main's build** on *all8* (`run-control`): no line of message 80, no attempt, no tag by tick 1001.

**The original** (`q348/orig/run.sh <file>`: `advisor.py ... 0x50,... poll` beside `thingwords.py` on the lab
and the researcher, `tags.py` and `bar.py`; with `HOLD=1` `bytehold.py` leaves the game type's low byte at 2
until `mGameTick` passes 1112, holds it at 0, and puts 2 back on tick 1148: a write to its memory, over the
row's second turn). *all8* untouched (round 1's run a): fresh over ticks 972 and 1127, through 1260. *all8*
held (run e): fresh over tick 972, the byte written on tick 1113, and message `0x50`'s record **(1127, 0, 1, 0)
on tick 1127**; one tag with it at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text string 84;
he is up in the same white spiked hair (`orig/e-g4.png`). The tag clicked: the bar's data read thing 0, buttons
0 and 0, item 0 and the text, and the bar is drawn with the bomb button and no other (`orig/e-bar.png`).
Message `0x11c` waited in a slot at 1000 from tick 1140. Rows 63, 64, 65, 72 and 76, Full Simulation's too and
polled inside the hold, stayed fresh. *seven* held (run f): fresh through tick 1240. *nores* held (run g; the
researcher's thing reads no type): fresh through tick 1151. *done* held (run h; project 8 read 250.0 of 250.0):
fresh through tick 1144.

| | The original | OpenTPW |
|---|---|---|
| Row 80 in an Instant Action park | never scored: fresh over two turns with all eight kinds built | the same (`score=-1 ... result=other-game`) |
| Row 80 with the game type held to Full Simulation's | `Score` where a researcher is on the staff, a kind of feature stands, a feature's project is left undone and the kinds standing are as many as those on offer | the same |
| All eight kinds, a researcher, eleven projects | said on the row's turn (tick 1127, the first inside the hold) | the same (tick 972, score 60) |
| Seven kinds of eight | not said | the same (`kinds=7/8`) |
| No researcher | not said | the same (`researchers=0`) |
| Every feature project done | not said | the same (`unfinished=0`) |
| Who says it | the lobby's fly in the research hat | the same |
| The tag and the bar | row 82, part 2, string 84; no button but the bomb | the same |
| The lab's projects | the file's, and worked on from the load, 1.5 points every 20 ticks on project 8 | the file's, read and never changed |

**Not built or not seen.** A Full Simulation park, the row's only game, is run in both games since (Q327, "A fresh
Full Simulation park", below: the original's own fresh park said it on tick 2755, and its file said it on 3615 in
both); in no game run: the line again past its
900 quarter-ticks (the test holds it), a kind standing that is not on offer, a project finished by the
original's own research and what that does to the kinds on offer, the other three themes' labs. Of the lab's
record this game reads the projects, the five kinds' records (Q701), `mWorkLoad` and `mMostExpensiveProject` (Q590);
`mLabActive` is named and not read. The hold's turn in the two games
is not the same turn (972 here, 1127 there): the original's byte was not held through its load, which reads
the game type for its catalogue. Row 76 (`SideshowsResearchMore`) is built since, below; the other twenty-nine
polled rows of mode 0 are not. **Rows 83 and 84 are posted and no poster is found**: row 83's score is the stub
`FUN_005d60c0`, which answers nought, row 84's (`0x0059e410`) answers `FeaturesFarToToilets.Score` alone, and
neither `0x53` nor `0x54` is handed to `FUN_0059b590` or `FUN_0059bf20` by any defined code, nor stored into a
record on the stack by a `MOV` of either number; an event of type `0x19` carries a message's number too
(`FUN_0040f7a0`, heard at `FUN_0059b060`'s case `0x19`), and its four builders are the challenge manager's. That
nothing posts them is not proven. Not heard in either game.

## Every sideshow built, and more to research (message `0x4c`, Q350)

**Row 76, message `0x4c`, `SideshowsResearchMore`:** polled, category 4 (the same message no sooner than 900
quarter-ticks on), **game mode 0: Full Simulation's alone** ("Every feature built, and more to research", above),
one copy, first response 217, **two lines**. Responses 217 and 218 are samples 215 and 216 ("You've pretty much
built all of the sideshows that are currently available. Have you thought about researching some more?"): the
random gesture chain, model slot 0, props 10 and 0, the lobby's fly in the research hat, group 78. It is the
polled row four before row 80: from the shipped park's cursor its turns are ticks 813, 968, 1123 and every 155 on.

**Its score, `0x0059dfd0`** (loose code, disassembled; `ECX` the balance object) is row 80's of the sideshows
with one test more:

1. The researchers, `FUN_004c9960( 8 )`. None: nought.
2. The kinds of sideshow standing, `0x004ca7b0( 2, 0 )`. None: nought.
3. The lab's projects of a sideshow's kind left undone, `0x004ca670` (`FUN_00503b30( 2 )` on the lab). None:
   nought.
4. **How much of the lab's work the sideshows are getting, `FUN_00503980( 2 )` on the lab. Above nought (a `JA`
   at `0x0059e03a`): nought.**
5. The kinds of sideshow on offer, `0x004ca570`. Not as many as stand: nought. Otherwise `[ECX + 0x288]`,
   `SideshowsResearchMore.Score`, **75**.

**The rate, `FUN_00503980( kind )`**, in unsigned whole numbers throughout:
`output x ( ability x 100 / most ) / 100`.

- `output` is the kind's record's second dword, the lab's `+0x1410 + 16 x kind`. A kind's record is sixteen
  bytes from `+0x140c`: effort, output, current project, highest group (thirteen in the file; FileFormats
  `saves.md`, "The research lab"). The outputs are made by the kinds' turn `FUN_00503330`, whose one caller is
  the lab's setup (`park-engine.md`, "A fresh world's research lab"): the effort's share of the five efforts in
  whole per cent, times `mWorkLoad`, over a hundred. The shipped park's are 49, 6, 14, 11 and 0.
- `ability` is `FUN_004c9e60`: over the world's list of researchers (`mFirstResearcher`, `+0x1da742`, each
  member's next at `+0x210`), the sum of `ResearcherConstsPerGrade[grade].ResearchAbility` (`0x0078545c + 12 x`
  the member's `+0x1e4`), the points a researcher hands the lab every 20 ticks.
- `most` is `FUN_004c9ed0`: `StaffPoolInfo.MaxResearchersInPark` (`0x007852f8`) times the top grade's ability
  (`0x0078548c`, the table's row 4). Nought is tested first: `test esi,esi` at `0x00503998` returns nought before the division.

So the line is for a lab whose sideshow research is getting **less than one whole unit**. In Instant Action's
Lost Kingdom (`Easy_Standard.sam`: abilities 6, 9, 12, 16 and 20, six researchers at the most, so 120) the
sideshows' 14 is nought for one researcher of grade 0 (5 per cent) or 1 (7 per cent) and 1 from grade 2 (10 per
cent): **the shipped park's own researcher, of grade 2, keeps the line unsaid in any game**. In Full Simulation
(`Standard.sam`: 2 to 6, ten at the most, so 60) it is nought until the abilities on the staff add up to 5.
**The rate is not the research**: with the one researcher of grade 0 the original's lab put 0.9 points to its
sideshow project every 20 ticks through every run below while the rate read nought and the line was said.

**Its tag** is row 78 of the tag table: key 383, picture 2, string 80, the bar's first button of type `0x13`
(`mb_resrch`, help row 531; its act is `FUN_004aa480`, the research screen), no second, thirty seconds. String
80 reads "All the available sideshows have now been built. Perhaps it's time to research some more?" in the
English folder this game reads and in the american one the original reads.

### OpenTPW (`ParkAdvisorMessages.ResearchMoreSideshows`, a topic's `EverySideshowBuilt`; `ParkFacts.SideshowProjectsUnfinished`, `SideshowResearchRate`, `ResearchAbility`, `MostResearchAbility`; `ParkResearch.Rate`, `FreshKinds`; `ParkWorld.ResearchKinds`; tag row 78)

A twenty-sixth polled topic and the second that is Full Simulation's alone. `ParkWorld` reads the lab's five
kinds' efforts and outputs from the file, a fresh park makes them as the kinds' turn does, `ParkResearch.Rate`
is `FUN_00503980`, and `ParkPeople.AdvisorFacts` hands the score the sideshows' rate with the two numbers it
was made of. The poll line ends ` researchers=N kinds=K/T unfinished=N rate=R ability=A/M`. `ParkTags.Rows`
lists row 78, and the bar wears the research button.

**The bar's research button** opens the research screen in Full Simulation with a researcher (Q590, `hud.md`, "The
research opener and its two boxes"). **The outputs change with the screen**: its sliders set a kind's effort and the
work load, and the lab's outputs follow.

**The scenes** (`lib/stockscene.py`; the stock park paused on tick 962 to 964): *both*, a Strength Bird (1305)
bought beside the stock Jungle Spray, so that both kinds on offer stand, the researcher (thing 30) of grade 2 as
shipped; *g1* and *g0*, the same with his `mCurrentPayGrade` written 1 and 0 (`parkfile.thing_set`, the staff
record's `+398`); *one0*, grade 0 with no Bird; *done0*, *g0* with the lab's two sideshow projects written done;
*nores*, *both* with him fired.

**Predicted first** (`q350/PREDICTION.txt`), both games, before any run. Every prediction of this game held on
its first run. Of the original's, the scores all held; **two things about run a were not foreseen and are said
below**: a second line that made row 76's wait, and my click landing on that line's tag.

**This game** (`q350/confirm.py <mode>`, `advisorgame full` given paused; *fix* on the desktop, the rest
off-screen). *g0* (`run-fix1`): `poll message=76 row=76 days=42 score=75 threshold=25 tick=968 result=post
researchers=1 kinds=2/2 unfinished=2 rate=0 ability=6/120`, `post message=76 ... result=queued` and `attempt=1
message=76 subject=0 score=75 threshold=25 response=217 sample=215 line=0 tick=968`; `saying sp_215.mp2 ...
wearing=10,0 slot=0`, him up in the research hat's white spiked hair (`1-line.png`), `tags` reading
`tag=78/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked:
`message bar: tag 78 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_resrch/help=531
second=hidden erase=mb_erase` and the text, the bar drawn with the bulb button over the bomb (`2-bar.png`). The
button clicked: `button type 19 is not built, so nothing more happens`, `TAG_BAR_BUTTON` counted once.
*g1* (`run-g1`): the same line at `ability=9/120`, said on 968. *both* (`run-g2`): `score=0 ... result=no-score
researchers=1 kinds=2/2 unfinished=2 rate=1 ability=12/120`. *one0*: `no-score ... kinds=1/2 ... rate=0
ability=6/120`. *done0*: `kinds=2/2 unfinished=0`. *nores*: `researchers=0 ... ability=0/120`. *g0* with nothing
held (`run-instant`): `score=-1 ... result=other-game`. None of the silent runs said the line or left tag 78.
Main's build on *g0* (`run-control`): no line of message 76, no attempt, no tag.

**A fresh Full Simulation park, the row's own game** (`q350/fresh.py`, `lib/freshpark.py`; no `advisorgame`
given): the park's first line ends `the kinds' efforts [100,15,30,15,10] and outputs [49,6,14,6,4]`, the
original's own fresh park's. With the pool's scientist of grade 1 hired and both sideshows on offer bought
(`run-fresh-one`, on the desktop): `poll message=76 row=76 days=1 score=75 threshold=25 tick=46 result=post
researchers=1 kinds=2/2 unfinished=3 rate=0 ability=3/60`, said, tag 78. With the pool's second, of grade 3,
hired as well (`run-fresh-many`): `score=0 ... result=no-score researchers=2 ... rate=1 ability=8/60`, silent.
Main's build (`run-fresh-control`): no line of message 76.

**The original** (`q350/orig/run.sh <file>` with `HOLD=1`: `advisor.py ... 0x4c,... poll` beside `thingwords.py`
on the lab's sideshow output and project 22 and on the researcher's type and grade (`30:0x1e4`), `tags.py`,
`bar.py`, `screens.py`; `bytehold.py` holds the game type's low byte at 0 from tick 1113 to about 1145, a write
to its memory, over the row's turn on tick 1123). The researcher's `+0x1e4` read the grade written in every
run, and the lab's `+0x1430` 14.

- *g1* (run b2): fresh over tick 968; message `0x4c`'s record **(1123, 0, 1, 0) on tick 1123**, one tag with it
  at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text string 80.
- *both*, grade 2 (run c): fresh through tick 1240, no tag.
- *g0* (runs a, a2): on tick 1123 a slot took `0x4c` at 75, and the record moved on tick 1144 and 1142, **after
  another line**: on tick 1109, before the hold, the original said "Your researchers could do their job better
  and faster if you trained them." (tag string 37, the bar's button 21; `StaffTrainResearchers`, row 32: "Staff
  nobody has trained"), and row 76's line waited for it. The second tag
  stood above the first, at (84, 834, 154, 899). He is up in the same white spiked hair (`orig/a2-g3.png`).
  The second tag clicked (a2): the bar's data read thing 0, buttons 19 and 0, item 0 and the text, drawn with
  the bulb button over the bomb (`orig/a2-bar.png`). The bulb clicked, with the game type back at Instant
  Action's: a box reading "Research is automatic in Instant Action mode." (`orig/a2-button.png`), and the tag
  went half a second on.
- *one0* (run d), *done0* (run e; project 22 read 700.0 of 700), *nores* (run f; thing 30 reads no type): fresh
  through ticks 1229, 1229 and 1141. Runs d and e said the training line on tick 1109 too.

| | The original | OpenTPW |
|---|---|---|
| Row 76 in an Instant Action park | never scored (fresh over tick 968 in every run) | the same (`score=-1 ... result=other-game`) |
| Both kinds, one researcher of grade 1, two projects | said on the row's turn (tick 1123, the first inside the hold) | the same (tick 968, score 75, `rate=0 ability=9/120`) |
| The same, grade 0 | scored 75 on its turn, said 19 to 21 ticks on, after the training line | said on its turn in Q350's runs, before the training line was built (Q351) |
| The same, grade 2, the shipped researcher | not said | the same (`rate=1 ability=12/120`) |
| One kind of two | not said | the same (`kinds=1/2`) |
| Every sideshow project done | not said | the same (`unfinished=0`) |
| No researcher | not said | the same (`researchers=0`) |
| Who says it | the lobby's fly in the research hat | the same |
| The tag and the bar | row 78, part 2, string 80; the research button and the bomb | the same |
| The research button | the research screen's opener: in Instant Action a box of one line, and the tag off | the same, built since (Q410; `hud.md`, "The research opener and its two boxes"); in no game run here |
| A fresh Full Simulation park | not run | said on tick 46 with one scientist, silent with two whose abilities make 8 of 60 |

**Not built or not seen.** The training line of a park whose researchers are poorly graded, which the original said in every run with
the grade-0 researcher (built, Q351: "Staff nobody has trained"). In
no game run: line 2 (sample 216; the test holds the turn), the line again past its 900 quarter-ticks, the row in
the original's own Full Simulation park (its turn was reached only by holding the game type in an Instant
Action one), a kind's effort changed on the research screen and the outputs after it, a `most` of nought. The
hold's turn in the two games is not the same turn (968 here, 1123 there), as for row 80. What the lab's own
turn does with a kind's output (`FUN_00503430`: 0.9 points every 20 ticks to project 22 from a researcher of
ability 6, 1.8 at grade 2 by run c) is not read. Not heard in either game.

## A park with sideshows and no feature (message `0x51`, Q345)

**Row 81, message `0x51`, `FeaturesBuildFeatures`:** polled, category 0, either game mode, one copy, first
response 226, **one line**. Response 226 is sample 224 ("Imagine a world without features: no trees, fountains,
statues. That's what your park is like, it has no features. Maybe you should build some"): the random gesture
chain, **model slot 1 and prop -1, the theme's own advisor and its costume**, as row 79's two, group 83. It is two
polled rows after row 79 (row 80, `FeaturesResearchMore`, is "Every feature built, and more to research", above): from the
shipped park's cursor its turns are ticks 818, 973, 1128, 1283, 1438, 1593, 1748 and every 155 on.

**Its score, `0x0059e2e0`** (loose code, disassembled for this; `ECX` the balance object) is row 77's a kind
on, each step on the park analyser (`FUN_00519510`):

1. The kinds of sideshow standing, `0x004ca7b0( 2, 0 )`. None: nought. The rides and the shops are not asked.
2. The whole days since the newest build, `0x004c9070`, under `[ECX + 0x634]`, `WaitingTimes.DaysForObjects`,
   **30** (a signed `JL`): nought.
3. The kinds of feature standing, `0x004ca7b0( 3, 0 )`: any, nought. A toilet, a bin, a camera and a staff room
   are each a feature (`Info.WhichUIType` 3).
4. Otherwise `[ECX + 0x2b0]` plus `[ECX + 0x2b4]` times the park's age in whole days (`FUN_004f8800` over
   `0xC92A69C000`, as rows 62, 73 and 77 count them): `FeaturesBuildFeatures.Score` and
   `FeaturesBuildFeatures.AdditionalPerDay` (`lib/advisorschema.py`), **-40 and 1** in `Advisor.sam`.

The door is not asked, nor what is on offer. The score starts forty in debt and gains one a day: 15 on tick 1283
(day 55) and 22 on tick 1438 (day 62) are not above `MinScoreForConsideration`, 25; day 66 is the first that is,
and **29 on tick 1593 (day 69)** the first of its turns it can be said on; 35 on tick 1748.

**Its tag** is row 83 of the tag table: key 383, picture 2, string 85, the bar's first button of type 12 (the buy
screen on its fourth tab, the features'; help row 524), no second, thirty seconds. String 85 reads "Your park
isn't as pretty as it might be. It would look better if you put down some features." in the English folder this
game reads, and "... as it could be. ..." in the american one the original reads.

### OpenTPW (`ParkAdvisorMessages.BuildFeatures`, a topic's `PerDayWithNoFeature`; tag row 83)

A twenty-third polled topic, scored as the original scores it: `PerDayWithNoFeature` names the per-day key, and
the facts it reads (`SideshowKindsStanding`, `FeatureKindsStanding`, `DaysSinceObjectBuilt`) were kept already.
The poll line ends ` sideshowkinds=N featurekinds=N sincebuild=N`. `ParkTags.Rows` lists row 83.

**Who says it.** Response 226 names the theme's own advisor and costume: the fly in the Lost Kingdom's
explorer's hat, no antennae (`q345/orig/a-g4.png`), in both games since Q346 ("The theme's own advisor and its
costume", whose run is of this line). Unlike row 79's, **this line is said under the shipped keys**, from day 66
of a shipped park with a sideshow and no feature. This item's own runs, below, were of the build before, which
said it with the lobby's model, bare-headed, and counted `ADVISOR_THEME_COSTUME`.

**The scenes** (`q345/scene.py`, one run of the stock park; `lib/stockscene.py` for the last; `mk.py`, `mk2.py`):
every feature sold (things 17 to 24) on tick 1434 and the park saved then (*early*) and on tick 1584 (*nofeat*);
a Litter Bin bought back (*feat*); the bin and the Jungle Spray sold (*noside*); and the features sold on tick
1276 (*early2*). The stamp is written into copies: `nofeat30` tick 901's (day 30 on tick 1593), `nofeat29` tick
902's, `feat30` and `noside30` 901's, `early30` 746's (day 30 on tick 1438), `early2-30` 591's (day 30 on 1283).

**Predicted first** (`q345/PREDICTION.txt`), both games, two rounds, each written before its runs. Not
predictions, and said there: the listing, the keys, the row, the tag, the strings. **One miss, of the check and
not of the rule**: in `early30` the spent bus is summoned again on tick 1438, the row's own turn, and both games
drew a larger vehicle there, which stamps the newest build before the tick's poll (this game's ferry, `no-score
... sincebuild=0`; the original's stamp moved on tick 1438 too). Round two's file is saved between two
summonses.

**This game** (`q345/confirm.py <mode>`; the first on the desktop, the rest off-screen). *nofeat30* (`run-fix1`,
six of six on its first run): `poll message=81 row=81 days=69 score=29 threshold=25 tick=1593 result=post
sideshowkinds=1 featurekinds=0 sincebuild=30`, `post message=81 ... result=queued` and `attempt=1 message=81
subject=0 score=29 threshold=25 response=226 sample=224 line=0 tick=1593`; `saying sp_224.mp2 ... wearing=-1,0`,
`1x ADVISOR_THEME_COSTUME`, him up with no hat over a park with no feature in it (`1-line.png`), `tags` reading
`tag=83/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked:
`message bar: tag 83 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=524 second=hidden
erase=mb_erase` and the text. The button clicked: `button type 12 opens the buy screen on tab 3 and takes tag 83
off`, `windows` reading `ParkBuyScreen tab 3`, and the frame "Buy Miscellaneous Items" (`3-screen.png`).
*early2-30* (`run-early2`): `days=55 score=15 threshold=25 tick=1283 result=under-threshold sideshowkinds=1
featurekinds=0 sincebuild=30`, nothing posted or tried by tick 1312. *feat30* (`run-feat`): `score=0 ...
tick=1593 result=no-score sideshowkinds=1 featurekinds=1 sincebuild=30`. *noside30* (`run-noside`): `no-score
sideshowkinds=0 featurekinds=0`. *nofeat29* (`run-d29`): `score=0 ... tick=1593 result=no-score ...
sincebuild=29`; the seaplane was then made on tick 1600 and stamped, so nothing was said on tick 1748, as the
prediction's "unless" has it: **a day short and said a turn later was not seen in this game** (the test holds
it). None of the silent runs said the line or left tag 83 by tick 1610.

**Main's build** on `nofeat30` (`run-control`): no line of message 81, no attempt, no tag by tick 1621.

**The original** (`q345/orig/run.sh <file>`: `advisor.py ... 0x51,0x52,0x50,0x4f,... poll` beside
`thingwords.py` on the stamp, `tags.py`, `bar.py` and `screens.py`; seven loads of this game's files, the shipped
`Advisor.sam`). *nofeat30* (run a): message `0x51`'s record went from fresh to **(1593, 0, 1, 0) on tick 1593**,
2.9 s after the load's click; one tag with it at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text
the american string 85; he is up in the explorer's hat (`orig/a-g4.png`); its stamp moved on tick 1600, a
vehicle made, as this game's summons falls. Run a's clicks came as the tag's thirty seconds ended and opened
nothing; run a2 clicked sooner: the bar's data read thing 0, buttons 12 and 0, item 0 and the text, `screens.py`'s
buy screen went from `0x0` to a pointer on tick 1703 and the tag was gone on 1705, the frame "Buy Miscellaneous
Items" with the same ten rows as this game's (`orig/a2-button.png`). *nofeat29* (run b): fresh on tick 1593; its
stamp moved on tick 1600 and the run ended fresh. *nofeat29 with the stamp held at the file's value* (run g;
`thingpoke.py`, **a write to the original's memory**): fresh on tick 1593, **(1748, 0, 1, 0) on tick 1748** with
the same tag. *feat30* (run c) and *noside30* (run d): fresh through the runs' ends; run d left a tag on tick
1744, row 77's turn. *early30* (run e): the stamp moved on tick 1438 and the record stayed fresh. *early2-30*
(run f): fresh, no tag and the file's stamp on tick 1318, the pollers' first reading; the stamp moved on tick
1435. Message `0x52` (row 82, `FeaturesNoToilets`) stayed fresh in every run, with no toilet in six of them.

| | The original | OpenTPW |
|---|---|---|
| Row 81 | polled; nought with no kind of sideshow standing, until 30 days after the newest build, and with a kind of feature standing; then `Score` and `AdditionalPerDay` a day of the park's age | the same |
| No feature, a sideshow, day 30, tick 1593 | said on tick 1593, the one line | the same (score 29) |
| Tick 1283 | not said | the same (`score=15 ... under-threshold`) |
| A feature standing, and no sideshow | not said | the same (`no-score`) |
| Day 29 | not said on tick 1593; with the stamp held, said on tick 1748 | nought on tick 1593; tick 1748 not seen (a vehicle's stamp on 1600) |
| A vehicle made on the row's own turn | the stamp moves and the row is silent | the same |
| Who says it | the fly in the theme's explorer's hat, no antennae | the same since Q346, the helmet lit as the original's since Q657 (`q657/run2/theme.png`) |
| The tag and the bar | row 83, part 2, string 85; button 12 builds the buy screen on its features tab, tag off | the same |
| The tag's words | the american folder's, "... as it could be. ..." | the English folder's, "... as it might be. ..." |

**Not built or not seen.** In no game run: the line said again past its 120 quarter-ticks (the test holds that it is the one line
again), score 22 on tick 1438 (a summons falls on that tick), day 29 said a turn later in this game, a
golden-ticket sideshow or feature, a feature built after the line, the bar's button with the buy screen open, a
park made with no file, Full Simulation (the row asks neither mode: not asked). Row 82 is silent in those parks
because few of their guests need a toilet ("A park with no toilet", below). Row 80 is "Every feature built, and
more to research", above, and row 76 "Every sideshow built, and more to research". Not heard in either game.

## A park with no toilet (message `0x52`, Q347)

**Row 82, message `0x52`, `FeaturesNoToilets`:** polled, category 0, either game mode, one copy, first response
227, **three lines**. Responses 227 to 229 are samples 225 to 227 ("You don't have any bathrooms in the park and
visitors are leaving when they need to go ...", "You don't have any bathrooms, and when visitors need to go
they're just, well, going elsewhere ...", "Your park is really hopping, and I don't mean busy ..."): the random
gesture chain, model slot 0 and props 0 and 0, the lobby's fly bare-headed, group 84. It is the polled row after
row 81: from the shipped park's cursor its turns are ticks 819, 974, 1129, 1284, 1439, 1594 and every 155 on.

**Its score, `0x0059e380`** (loose code, disassembled for this; `ECX` the balance object), each step on the park
analyser (`FUN_00519510`):

1. The door, `FUN_004c9050` (`mParkClosed` nought, `FUN_0051a280`). A shut park: nought.
2. The toilets, `FUN_004c9ee0` (row 262's count: an object's `+0x32 & 1`). Any, open or shut: nought.
3. The guests inside whose toilet need is over a level, `0x004c9290` (loose code) handed the low byte of
   `[ECX + 0x2b8]`: every thing of type 1 that `FUN_004fa990` passes (a cell of type 0, 1, 3, 9 or 10, as
   `park-gate.md` has it) whose need, the float at `+0x1ac` cut to a whole number (`0x0067a830`) and to its low
   byte, is above that byte (an unsigned `JBE` at `0x004c9305`).
4. The guests inside, `FUN_004c9130`. None: nought.
5. The first count times 100 over the second, a whole-number `IDIV`: at or under `[ECX + 0x2bc]` (a signed
   `JLE`), nought; otherwise `[ECX + 0x2c0]`.

The three keys are `FeaturesNoToilets.NeedingMoreThan`, `.MorePeopleThanThisNeeding` and `.Score`
(`lib/advisorschema.py`), **60, 25 and 90** in `Advisor.sam`. The park's age, the newest build and what else
stands are not asked: an open park with no toilet scores 90 on the row's turn whenever more than a quarter of
the guests inside it are over 60, and 90 is well above `MinScoreForConsideration`.

**Why it is rarely heard.** A guest is made with a need of 0 to 29 (`guest-arrivals.md`), only a guest in four
gains a point every sixteen ticks, and one over 80 sets off out of the park (`ride-operation.md`). In Q345's runs,
whose files had toilets until tick 1434 or 1584, the record stayed fresh: this game's poll of the same
`q345/nofeat30.TPWS` read `needing=0/22` on tick 1594 (`q347/run-old`).

**Its tag** is row 84 of the tag table: key 383, picture 2, string 86, the bar's first button of type 12 (the buy
screen on its fourth tab; help row 524), no second, thirty seconds. String 86 reads "Your park needs some
toilets, so your visitors have somewhere to go." in the English folder this game reads, and "... some bathrooms
..." in the american one the original reads.

### OpenTPW (`ParkAdvisorMessages.NoToilets`, a topic's `NeedingToiletWithNone`, `ToiletNeedOver`; `ParkFacts.GuestsInside`, `GuestsNeedingToilet`; tag row 84)

A twenty-fourth polled topic, scored as the original scores it. `ParkPeople.AdvisorFacts` counts the guests on a
counting cell (`GateGuestCensus`'s test) and those of them whose `Toilet`, cut to a whole number's low byte, is
over the advisor's level; with no advisor there is no level and none is counted. The poll line ends ` closed=N
toilets=N needing=N/N`. `ParkTags.Rows` lists row 84.

**The scenes** (`q347/scene.py`, `mkscenes.sh`; the stock park paused on tick 962 to 965, thirteen guests inside,
the console's `sell` and `toilet`): *need13*, the three toilets sold and every guest's need 70; *need3* and
*need4*, the toilets sold, every guest at 10 and three or four at 70; *lvl60*, the toilets sold and every guest at
60; *stand*, the toilets standing and every guest at 70; *shut13*, need13 with the door shut by a click on the
entry-price screen. **The console's `toilet` writes a need no play has made**: the files are instruments, read by
both games.

**Predicted first** (`q347/PREDICTION.txt`), both games, three rounds, each written before its runs. Not
predictions, and said there: the listing, the keys, the row, the tag, the strings.

**This game** (`q347/confirm.py <mode>`; the first on the desktop, the rest off-screen). *need13* (`run-fix1`,
five of five on its first run): `poll message=82 row=82 days=42 score=90 threshold=25 tick=974 result=post
closed=0 toilets=0 needing=13/13`, `post message=82 ... result=queued` and `attempt=1 message=82 subject=0
score=90 threshold=25 response=227 sample=225 line=0 tick=974`; `saying sp_225.mp2 ... wearing=0,0 slot=0`, him
up bare-headed with his antennae over a park with no toilet (`1-line.png`), `tags` reading
`tag=84/picture=2/subject=0` with `mast=94,901,144,984` and `tag900=84,909,154,974/frame=2`. The tag clicked:
`message bar: tag 84 subject 0 item 0 bar: out=1 rect=486,1112,1101,1453 ... first=mb_buy/help=524 second=hidden
erase=mb_erase` and the text. The button clicked: `button type 12 opens the buy screen on tab 3 and takes tag 84
off`, `windows` reading `ParkBuyScreen tab 3`, and the frame "Buy Miscellaneous Items" (`3-screen.png`).
*need3* (`run-need3`): `score=0 ... tick=974 result=no-score closed=0 toilets=0 needing=3/13`, 23 per cent.
*need4* (`run-need4`): `score=90 ... result=post ... needing=4/13`, 30 per cent, said on tick 974. *lvl60*
(`run-lvl60`): `no-score ... needing=0/13`; left to tick 1304 (`run-long`) it said nothing. *stand*
(`run-stand`): `no-score closed=0 toilets=3 needing=13/13`. *shut13* (`run-shut`): `no-score closed=1 toilets=0
needing=13/13`. None of the silent runs said the line or left tag 84 by tick 990.

**Main's build** on *need13* (`run-control`): no line of message 82, no attempt, no tag by tick 1003.

**The original** (`q347/orig/run.sh <file>`: `advisor.py ... 0x52,0x51,... poll` beside `thingwords.py` on four
guests' needs, `tags.py`, `bar.py` and `screens.py`; ten loads of this game's files, the shipped `Advisor.sam`).
*need13* (run a): message `0x52`'s record went from fresh to **(974, 0, 1, 0) on tick 974**, 3.7 s after the
load's click; one tag with it at (84, 909, 154, 974), the mast at (94, 901, 144, 984), its text the american
string 86; he is up bare-headed with his antennae (`orig/a-g3.png`). Run a2 clicked the tag and the bar's
button: the bar's data read thing 0, buttons 12 and 0, item 0 and the text, `screens.py`'s buy screen went from
`0x0` to a pointer on tick 1091 and the tag was gone on 1093, the frame "Buy Miscellaneous Items" with "Small
bathroom" unticked (`orig/a2-button.png`). *need3* (run b2): fresh through tick 1184. *need4* (run c): **(974, 0,
1, 0) on tick 974** with the same tag. *stand* (run e): fresh through tick 1104. *shut13* (run f): fresh through
tick 1184, message `0x81` waiting in a slot. The first load of *need3* (run b) loaded nothing: run a2 had left
the buy screen up and the load's clicks went into it.

*lvl60*, four loads. Guests 32, 36 and 40 read 61 from tick 976 and the others 60 throughout, in each. Run d:
fresh on ticks 974 and 1129 and **said on tick 1284**, which was not predicted; run d2: fresh through tick 1479.
Runs d3 and d4 were made with `original/inside.py`, written for this, which counts the guests inside and those
over 60 as the listing above has the analyser count them: 13 inside and none over on tick 964, as this game's
`gate` census and poll line read of the same file; 3 of 13 from tick 976; **3 of 12, a share of 25, at the turn
of tick 1129, and the record stayed fresh**; 2 of 10 and 2 of 11 at tick 1284, fresh; 1 of 13 and 1 of 15 at
tick 1439, fresh; never over 25 and never said, through ticks 1528 and 1538. What stood in run d on tick 1284 was
not counted: guest 32 read 80 there, so three were over 60, and the rule needs eleven or fewer inside. The
tool's first two runs are void (`orig/round3-wrong-cell/`): it read the cell beside each guest's, the runtime
cell array being indexed by the packed id less one (`0x004fa9b6`).

| | The original | OpenTPW |
|---|---|---|
| Row 82 | polled; nought in a shut park, with a toilet standing and with nobody inside; then `Score` where the guests inside over `NeedingMoreThan` are more than `MorePeopleThanThisNeeding` per cent of them | the same |
| No toilet, 13 of 13 at 70, tick 974 | said on tick 974, line 1 | the same (score 90) |
| 4 of 13 (30) | said on tick 974 | the same |
| 3 of 13 (23) | not said | the same (`no-score ... needing=3/13`) |
| 3 of 12 (25) | not said (runs d3, d4, tick 1129) | the test holds one of four; not seen in a run |
| Every guest at 60 | not said on tick 974; three guests then gain a point every sixteen ticks, and one run of four said it on tick 1284 | not said on tick 974, nor through tick 1304 in one run |
| The toilets standing, the park shut | not said | the same (`toilets=3`, `closed=1`) |
| Who says it | the lobby's fly, bare-headed | the same |
| The tag and the bar | row 84, part 2, string 86; button 12 builds the buy screen on its features tab, tag off | the same |
| The tag's words | the american folder's, "... some bathrooms ..." | the English folder's, "... some toilets ..." |

**Not built or not seen.** In no game run: lines 2 and 3 and the first again past the 120 quarter-ticks (the test
holds them), a guest outside the park (the test holds that they are neither count), a need past 255, a park made
with no file, Full Simulation (the row asks neither mode: not asked). **Left alone, this game's guests and the
original's do not come and go alike**, so a long run of one file need not speak on the same turn in both: only
the turn straight after a load was compared. Rows 83 and 84 are posted, not polled, and who posts them is not
read: row 83's one response, 230, is sample 227 again with group 85, and row 84's score, `0x0059e410`, is
`FeaturesFarToToilets.Score` alone (60); what was looked for of their posters is in "Every feature built, and
more to research", above, with row 80. Not heard in either game.

## Thirsty and hungry guests (messages 3 and 4, and row 85, Q530)

**Rows 3 and 4, messages 3 and 4, `VisitorsThirsty` and `VisitorsHungry`:** polled, category 0, either game mode, one
copy, three lines in turn (selection 2). Row 3's responses 8 to 10 are **samples 7 to 9** and row 4's 11 to 13 **samples
10 to 12**: the random gesture chain, model slot 0, **props 13 and 14, the fast-food hat and the spatula** (the table's
only pair with a second prop but the bowler's), groups 5 and 6 (`advisorrow.py 3 4`). From the shipped park's cursor
their turns are the two before row 5's: ticks 770 and 771 in Q415's scene, and every 155 on. The transcripts
(`global-speech-transcripts.tsv`): "lots of your visitors are thirsty maybe you should build some more drinks shops"
(sample 8), "people are getting hungry and there aren't enough food shops do you think you should build some more"
(sample 10), and four more in the same vein.

**Their scores, `AdvisorScore_VisitorsThirsty` `0x0059c9d0` and `AdvisorScore_VisitorsHungry` `0x0059ca00`** (named
2026-10-19): the analyser (`FUN_00519510` on the world, which takes no argument and returns it) is asked
`Analyser_CountGuestsThirstierThan` `0x004c93f0` (`Analyser_CountGuestsHungrierThan` `0x004c9340`) with the byte
`[ECX + 0xb0]`, `VisitorsThirsty.ThirstierThan` (`[+0xb8]`, `VisitorsHungry.HungrierThan`), and the count is multiplied
by `[+0xac]`, `ScorePerThirstyPerson` (`[+0xb4]`, `ScorePerHungryPerson`). The count walks the used thing list for things
of kind 1 whose cell `FUN_004fa990` passes, the guests inside, and counts each whose float `+0x1a4` (`+0x1a8`), `__ftol`
and its low byte, is above the key (`CMP AL`, `JBE`: unsigned): the code of row 82's
`Analyser_CountGuestsNeedingToiletMoreThan` `0x004c9290` (made a function then) with another field. **No door, no
division by the guests inside and no age is asked.** Shipped 99 and 1 each: with `MinScoreForConsideration` 25, 26
guests at 100 are needed (the needs are held to 0..100; FileFormats `saves.md`, the guest record).

**Row 85, `FeaturesNotEnoughToilets`, `AdvisorScore_NotEnoughToilets` `0x0059e420`** (defined and named 2026-10-19):
`mParkClosed` (`FUN_0051a280`); in an open park the toilets' average queue (`FUN_004cca40`) is taken and dropped
(`FSTP ST0` at `0x0059e444`); either way `XOR EAX,EAX` and `RET` at `0x0059e446`. **Always nought**: neither
`.AverageQueueLongerThan` nor `.Score` is read, and responses 233 and 234 (samples 231 and 232, "you don't have enough
bathrooms for a park this size...") are never said.

**Their tags** are rows 5 and 6 of the tag table: key 383, picture 2, strings 9 and 10, the bar's first button of type
10 (the buy screen, row 73's), no second, thirty seconds. English's string 9 reads "Lots of your visitors are thirsty.
Maybe you should build some more drinks shops?" (`Language/american`'s "drink shops"), string 10 "People are getting
hungry and there aren't enough food shops. Do you think you should build some more?". Row 85's group, 87, has its row in the tag
table too (`0x0075f788`): key 84, picture 2, string 88, the bar's first button of type 12, no second, thirty seconds.
No line leaves that tag, because row 85 is never said, so `ParkTags.Rows` does not hold it.

### OpenTPW (`ParkAdvisorMessages.VisitorsThirsty`, `VisitorsHungry`, `NotEnoughToilets`; a topic's `PerThirstyGuest`, `PerHungryGuest`, `AlwaysNought`, `SecondProp`; `ParkFacts.GuestsThirsty`, `GuestsHungry`; tag rows 5 and 6)

`ParkPeople.AdvisorFacts` counts the guests inside over `ThirstierThan` and `HungrierThan` (each key's low byte), each
need cut to its whole number's low byte, beside row 82's count. Rows 3 and 4 score the count times the key; the poll line
ends ` thirsty=N/inside over=N` (` hungry=`). A topic's `SecondProp` is the response's `+0x18`, and `PropsOf` answers both.
Row 85 is a topic that answers nought, so it leaves the `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` census (108 rows a round,
from 111). The console's `need hunger N` sets every guest's hunger, as `thirst N` the thirst.

**The scenes** (`q530/mkscene.py`): Q415's `ent.TPWS` (93 inside, the entertainer kept) with every guest inside at thirst
100 (`thirsty.TPWS`, the record's `+509`) or hunger 100 (`hungry.TPWS`, `+426`).

**This game** (`q530/confirm.py thirsty|hungry|control`, on the desktop, the final build): *thirsty* (`run1-thirsty`):
`poll message=3 row=3 days=33 score=93 threshold=25 tick=770 result=post thirsty=93/96 over=99`, said on that tick
(`attempt=1 message=3 ... response=8 sample=7 line=0 tick=770`), `Advisor: sample 7 ... wearing 13,14 on model slot 0`,
tag 5 lettered string 9 (`1-line.png`: the white paper hat with its logo, the red spatula in his gloved right hand, no
antennae); row 4 on 771 `score=0 ... hungry=0/97`; row 85 on 820 `score=0 ... result=no-score`. *hungry* (`run1-hungry`):
`poll message=4 ... score=93 ... tick=771 result=post hungry=93/97 over=99`, sample 10, the same props, tag 6. **Main's
build** on *thirsty* (`control1`): no line of message 3, 4 or 85, nothing said, no tag, to tick 950.

**The original** (`q530/orig/run.sh <file>`, `advisor.py ... 0x3,0x4,0x55 poll` beside `tags.py` and `thingwords.py`
on guest 34's `+0x1a4` and `+0x1a8`, filmed): *thirsty*: message 3's record from (0, -1, 0, 0) to (770, 0, 1, 0) on
tick 770, the tag "Lots of your visitors are thirsty. Maybe you should build some more drink shops?", guest 34's thirst
100.0; *hungry*: message 4's to (771, 0, 1, 0) on 771, its tag, hunger 100.0. In each film the advisor wears the white
paper hat and holds the red spatula, as ours (`q530/side-by-side.png`). Its live response table holds 13 and 14 for
responses 8 to 13, as the file's. The stills came after the lines: `loadfile.sh` returns about 16 s after its click.
No difference was seen.

## An open park's shut rides (message `0x103`, Q323)

**Row 259:** polled, category 0, either game mode, one copy, three lines in turn. Its responses are 501 to 503,
samples 484 to 486 ("a lot of your rides are closed to the public and, well, there just aren't enough to satisfy
the visitors"): the random gesture chain, model slot 0, prop 9 (the ticket hat), group 383, so **its line leaves
no tag**. From the shipped park's cursor, 277, its turns are tick 898, the one before row 260's, and every 155 on.

**Its score** (`0x005a02f0`; `ECX` the balance object):

1. nought where the park is shut (`FUN_004c9050` answers nought);
2. the rides standing are `FUN_004ca8e0( 0, 0 )`, things of type 3 whose item's `WhichUIType` is 0; nought with
   none, and nought with fewer than `[ECX + 0x57c]`, `RidesClosed.AtLeastThisManyRides`, 3 (`JL`);
3. those of them shut are `FUN_004ca9b0( 0 )`, `mCanLoad` nought as for rows 260 to 262; `shut * 100 / rides`, a
   whole number (`IDIV`), must be more than `[ECX + 0x580]`, `RidesClosed.MoreThanThisPercentClosed`, 50 (`JLE`
   to nought);
4. the score is `[ECX + 0x584]`, `RidesClosed.Score`, 80.

So where rows 260 to 262 do not ask the door, this one does: the park's door shuts every ride, and a shut park
hears of its shops, sideshows and toilets and not of its rides. **A ride bought and left with its queue not
joined to a path is shut** (`ride-operation.md`, "Opening a ride"), and counts.

### OpenTPW (`ParkAdvisorMessages.RidesClosed`, a topic's `OpenWithAtLeast`, `Shut.Rides`; `ParkFacts.RidesShut`)

A ninth polled topic, the first of `ShutTopics`: the same share as rows 260 to 262, of the rides, with one more
field, `OpenWithAtLeast`, the key of the least count, which also has the door asked. `ParkPeople.AdvisorFacts`
counts the rides whose `CanLoad` is nought. Its poll line ends ` shut=N/M closed=N`.

**The scene.** The stock park, open, has one ride, the Belly Bounce. Under a pause on the first tick at or past
893 a Crazy Ape and an Aztec Mayhem are bought and left as bought (`buy 1101 41 22 0`, `buy 1104 57 23`): three
rides, two shut, 66 per cent. The park is saved there, so the original starts five ticks or fewer before the
row's turn and none of its other rows has seen the new rides.

**This game** (`q323/PREDICTION.txt`, written before any run; `q323/confirm.py fix|control|two|shut`). *Two
bought* (`run-fix2`, on the desktop, six of six; `run-fix1` five of six, the miss the script's own: it named day
43 for tick 1053, a number the prediction did not hold, where the log says 45): paused on tick 895, things 43
and 44 `canload 0` and thing 13 `canload 1`; `poll message=259 row=259 days=38 score=80 threshold=25 tick=898
result=post shut=2/3 closed=0`, the post queued and `attempt=1 message=259 subject=0 score=80 threshold=25
response=501 sample=484 line=0 tick=898` on one tick, the run's only attempt; 1.5 s on `saying sp_484.mp2 ...
wearing=9,0`, `tags: standing=0`, and him up in the green ticket cap beside a bare mast, the two bought rides
behind their hoardings (`run-fix1/1-rides-line.png`); rows 260 to 262 `result=no-score shut=0/1`, `0/1` and
`0/3` on ticks 899 to 901; on tick 1053 `poll message=259 ... days=45 score=80 ... result=cooldown`.
`PARK_ADVISOR_POLLED_ROW_NOT_BUILT` read 135 on tick 895 and 291 on tick 1064, the rule's (`TOOLING.md`,
recipes) with ticks 898 and 1053 built turns. *The Mayhem alone bought* (`run-two`, off-screen): `score=0 ...
tick=898 result=no-score shut=1/2 closed=0`, no attempt. *The door shut on tick 805, then both bought*
(`run-shut`, off-screen): `score=0 ... tick=898 result=no-score shut=3/3 closed=1`, and row 260's line on tick
899, the toilets' on 940 and the sideshows' on 981. **Main's build** (`run-control`, off-screen): no poll line
of message 259, no attempt, the counts 135 and 293.

**The original on each run's own file** (`q323/orig/run.sh <file>`, `advisor.py ... 0x103,0x104,0x105,0x106
poll` beside `tags.py`, the display grabbed every 1.2 s from the Load Park list on; five of five).
*Two bought* (`run-fix1/rides-fix.TPWS`; `orig/a-load.log`): record `0x103` went from (0, -1, 0, 0) to **(898,
0, 1, 0) on tick 898** with no slot seen holding it, the action's length 10,832; no tag until tick 996; he is up
in the green ticket cap beside a bare mast, the Crazy Ape and the Aztec Mayhem behind their hoardings
(`orig/a-g05.png`). *The Mayhem alone* (`run-two/rides-two.TPWS`; `orig/b-load.log`): the record unmoved at tick
996. *The door shut* (`run-shut/rides-shut.TPWS`; `orig/c-load.log`): `0x103` unmoved; `0x104` said on tick
899, `0x106` on 939, `0x105` on 979.

**Seen and not predicted.** In all three the original then spoke of the unjoined queues: on tick 996, row 113's
turn (message `0x71`), a slot took `0x71` at 87 where he was speaking, and where he was free a tag stood from
that tick, "Somewhere in your park, a line is not connected to a path." (the mast at (94, 901, 144, 984)). Row
113 is read and built in "A queue not joined to a path", below.

| | The original | OpenTPW |
|---|---|---|
| Row 259 | polled; `RidesClosed.Score` in an open park with at least three rides, more than half of them shut | the same |
| Three rides, two bought and left shut | said on tick 898, line 0, in the ticket hat, no tag | the same |
| Two rides, one shut | not said | not said |
| The door shut, three rides shut | not said; the shops' line on tick 899 | the same |
| Row 113, an unjoined queue | queued or said on tick 996, with a tag | the same ("A queue not joined to a path", below) |

**Not built or not seen.** In no game run: lines 2 and 3 (samples 485 and 486; the test holds the
turn), the line said again past its 120 quarter-ticks (the test holds it), a ride shut from its own window, a
share of exactly half, four rides or more. Not heard in either game.

## A queue not joined to a path (message `0x71`, Q324)

**Row 113:** polled, category 0, either game mode, up to 255 copies, two lines in turn. Its responses are 288 and
289, samples 312 and 313 ("you have a line that isn't connected to a path, visitors can't get to it"; "you have a
place to line up but no way to get to it, it's not connected to a path"): the random gesture chain, model slot 0,
no prop, group 135. From the shipped park's cursor, 277, its turns are ticks 841 and 996, and every 155 on.

**Its score** (`0x0059ef60`; `ECX` the balance object): `FUN_004ccb00( &id, 0 )` on the park analyser
(`FUN_00519510`), the thing it answers stored in the balance object's `+4`, and where that is not nought the score
is `[ECX + 0x388]`, `QueueUnconnected.Score`, 87. Neither the park's door nor any count is asked.

**`FUN_004ccb00( &id, all )`** walks the world's thing list (`FUN_00516130`, `FUN_00516160`) and answers the first
thing that is all of:

1. of type 3 (`+2`);
2. one a guest may choose, with a queue laid on the ground: `+0x32 & 4` and `+0x32 & 8` (`ride-operation.md`, the
   flag byte);
3. not connected at the back of its queue: `FUN_004de4a0` answers nought (`ride-operation.md`, "the back of the
   queue connected");
4. where `all` is nought, as the score asks: not the builder's object, `DAT_008186ac` (`FUN_00527930`).

Nought where there is none.

**The builder's object, `DAT_008186ac`.** Three places write it: the build dispatcher's click on a queue cell
(`0x00524c3d`: the object that cell's queue serves), `FUN_005276d0` (the thing the interface has selected,
`DAT_007c2654`, or the thing its own entry record names where that item's `+0x288` is 1; it stores nought where
nothing is selected), and the map's start, which noughts it (`FUN_0052f050`). `FUN_005276d0` has nineteen
callers, in the dispatcher, the footprint applier and the object windows; **they are not read**, so when the word
goes back to nought after a queue is laid is not known. It read nought for 75 s of a park loaded from a file.

**The line is about no thing.** Nothing was found to read the balance object's `+4`: no instruction names
`0x00fb3544`, and the tick's post of a polled answer pushes thing nought (`0x0059a5fd`, "The poll", above). The
original's slot held `0x71` with thing nought at 87 (`q323/orig/c-load.log`). So the tag is row 135's with no
thing: key 383, picture 0, string 134 ("Somewhere in your park, a queue is not connected to a path." in the
English folder, "a line" in the american one), the bar's first button of type 3, no second, 30 seconds. **Its
button does nothing:** type 3 puts the camera on the tag's thing (`FUN_004867b0`), which returns at once for
thing nought.

### OpenTPW (`ParkAdvisorMessages.QueueUnconnected`, a topic's `QueueNotJoined`; `ParkFacts.UnjoinedQueue`; tag row 135)

A tenth polled topic. `ParkPeople.AdvisorFacts` names the first object of the chain that a guest may choose, has
a queue path and whose back is not connected (`ParkRideOperation.BackOfQueueConnected`), and the score is the
key's value where it names one. Its poll line ends ` unjoined=N`, the thing. **A deviation:** the object passed
over is the one the queue tool is armed for (`ParkBuildMode.Serves`), from the tool's arming to its putting
away; the original's word is also set from the interface's selected thing and may outlast the tool, which is not
read. The console's `buy` arms no tool, as a load in the original leaves the word nought.

**The scene.** The stock park, open; under a pause on the first tick at or past 893 an Aztec Mayhem is bought
and left as bought (`buy 1104 57 23`, thing 43), its queue of one cell joined to nothing, and the park saved.

**This game** (`q324/PREDICTION.txt`, written before any run; `q324/confirm.py fix|control|joined|armed`). *The
Mayhem bought* (`run-fix2`, on the desktop, five of five): paused on tick 895, thing 43 `canload 0`; `poll
message=113 row=113 days=36 score=0 threshold=25 tick=841 result=no-score unjoined=0`, then `poll message=113
row=113 days=43 score=87 threshold=25 tick=996 result=post unjoined=43`, the post queued and `attempt=1
message=113 subject=0 score=87 threshold=25 response=288 sample=312 line=0 tick=996` on one tick; 1.5 s on
`saying sp_312.mp2 ... wearing=0,0`, `tags: standing=1 tag=135/picture=0/subject=0`, the mast at 94,901,144,984
and the tag at 84,909,154,974, and him up with his antennae and no hat, the tag on the raised mast and the
Mayhem behind its hoardings (`run-fix2/1-queue-line.png`); the tag clicked, `message bar: tag 135 subject 0
item 0 bar: out=1 ... first=mb_locate/help=516 second=hidden erase=mb_erase` with the English words on the bar
(`2-bar.png`), and its first button's click logged no camera line and left the bar out.
`PARK_ADVISOR_POLLED_ROW_NOT_BUILT` read 134 on tick 895 and 248 on tick 1018, the rule's (`TOOLING.md`,
recipes) with ticks 841 and 996 built turns. **The first run** (`run-fix1`, three of five) missed twice for one
reason, the prediction's own: it left out the row's turn on tick 841, so it named one poll line where there are
two and each count one too many; the line, the tag and the bar were as predicted, and the prediction was made
again before `run-fix2`. *Nothing bought* (`run-joined`, off-screen): the one poll line of 113 is tick 841's,
no attempt, 248 on tick 1018. *The queue tool armed for thing 43 under the pause* (`run-armed`, off-screen): no
line on tick 996, no attempt before `tool off` on tick 1022, then `poll message=113 row=113 days=49 score=87
threshold=25 tick=1151 result=post unjoined=43` and attempt 1 there. **Main's build** (`run-control`,
off-screen): no poll line of 113, no attempt, the counts 137 on tick 897 and 249 on tick 1017.

**The original on each run's own file** (`q324/orig/run.sh <file>`: `advisor.py ... 0x71 poll`, `tags.py` and
`tpwmem.py watch 008186ac` in one log, the display grabbed every 1.4 s from the Load Park list on; five of
five). *The Mayhem bought* (`run-fix2/queue-fix.TPWS`; `orig/a-load.log`): record `0x71` went from (0, -1, 0, 0)
to **(996, 0, 1, 0) on tick 996**, the action's length 8,331; one tag from that tick at (84, 909, 154, 974), the
mast at (94, 901, 144, 984), "Somewhere in your park, a line is not connected to a path.", gone 30.54 s on; he is
up with his antennae and no hat, the tag on the raised mast (`orig/a-g21.png`); `[0x008186ac]` read nought
throughout. *Nothing bought* (`run-joined/queue-joined.TPWS`; `orig/b-load.log`): the record unmoved and no tag
in 75 s.

| | The original | OpenTPW |
|---|---|---|
| Row 113 | polled; `QueueUnconnected.Score` where a choosable thing's queue is not joined at its back | the same |
| A ride bought and its queue left unjoined | said on tick 996, line 0, no prop, tag 135 about no thing | the same |
| Every queue joined | not said | not said |
| The tag's words | the american folder's, "a line" | the English folder's, "a queue" |
| The tag's camera button | returns at once on thing nought (read, not clicked) | does nothing |
| The object passed over | the builder's object, `DAT_008186ac` | the queue tool's thing while the tool is armed |

**Not built or not seen.** When the builder's object goes back to nought (the nineteen callers of
`FUN_005276d0`); a ride bought with the mouse in the original, its queue tool put away, and whether the line is
then held back. What reads the balance object's `+4`. In no game run: line 2 (sample 313; the test holds the
turn), the line said again past its 120 quarter-ticks (the test holds it), a queue cut off from an open ride
(`ride-operation.md`: the ride stays open), two rides unjoined, the tag's button clicked in the original, the
line posted while he speaks (255 copies). Not heard in either game.

## A shut staff room (message `0x107`, Q325)

**Row 263:** polled, category 0, either game mode, one copy, **one line**. Its response is 513: the random
gesture chain, model slot 0, props 0 and 0, group 383, so no hat and no tag, and lip file 0 where a spoken line's
is its sample's number. **Its sample is 638, the speech bank's `z_error.mp2`**: 0.435 s at an rms of 0.47 and a
peak of full scale, where a spoken line's rms is 0.12 to 0.14. It is the bank's stand-in for a line never
recorded, and fourteen responses name it: 0, 429, 513, 517 to 519, 550, 572, 573, 588 to 591 and 599 (the
samples after it, 639 to 641, are his three cries). From the shipped park's cursor, 277, the row's turns are tick
902, the one after row 262's, and every 155 on.

**Its score** (`0x005a0490`; `ECX` the balance object):

1. the staff rooms standing are `FUN_004ca030`: things of type 3 with `+0x32 & 2`; nought with none;
2. those of them shut are `FUN_004ca0c0`: the same things for which `FUN_004e0440` answers, `mCanLoad` nought;
3. `shut * 100 / standing`, a whole number (`IDIV`), must be more than `[ECX + 0x5a0]`,
   `StaffroomClosed.MoreThanThisPercentClosed`, 25 (`JLE` to nought);
4. the score is `[ECX + 0x5a4]`, `StaffroomClosed.Score`, 90. The park's door is not asked.

**Nothing a player does shuts a staff room, so the row is dead by content.** The staff room's window
(`FUN_004b4fc0`) calls neither the door `FUN_0048ccf0`, whose four callers are the ride's, the shop's, the
sideshow's and the toilet's windows, nor the mechanic's call `FUN_0048ce50` (the ride's and the toilet's). The
park's door closes only things with `+0x32 & 4`, and the Staff Room's flags are `0x22`. It is in state 3, never
offered, so it takes no state-0 turn, breaks down for nobody and asks for no upgrade (`ride-operation.md`, "Where
an object's state comes from"). The constructor closes only a thing with a queue. That leaves a park file, whose
`mCanLoad` both games read as written: which fits a line that was never recorded.

### OpenTPW (`ParkAdvisorMessages.StaffroomClosed`, `Shut.StaffRooms`; `ParkFacts.StaffRooms`, `StaffRoomsShut`)

An eleventh polled topic, the last of `ShutTopics`: the same share as rows 260 to 262, of the things
`ParkWorld`'s `IsRestArea` names. `ParkPeople.AdvisorFacts` counts them and those whose `CanLoad` is nought. Its
one line plays `z_error.mp2` as the original's does.

**The scene** is a file: the stock `Easymode.TPWI` with one byte changed, thing 20's `mCanLoad` (its record's
`+214`) from 1 to 0 (`parkfile.thing_set`; `q325/room-shut.TPWS`), loaded from the Load Park list. Its clock
starts on tick 755.

**This game** (`q325/PREDICTION.txt`, written before any run; `q325/confirm.py fix|control|open`; three desktop
runs, three, five and six of six). `objects` read thing 20 `state 3 canload 0 ... flags 0x22` and the thirteen
others `canload 1`; `poll message=263 row=263 days=39 score=90 threshold=25 tick=902 result=post shut=1/1`, the
post queued and `attempt=1 message=263 subject=0 score=90 threshold=25 response=513 sample=638 line=0 tick=902
duration=3998` on one tick, the run's only attempt; `Advisor: sample 638 in 0.8s through clips [14, 1, 15]
(2332ms) ... wearing 0,0`; 1.2 s on `wearing=0,0`, `tags: standing=0`, and him up with his antennae and no hat
beside a bare mast (`run-fix3/1-room-line.png`); rows 259 to 262 `no-score` on ticks 898 to 901; on tick 1057
`poll message=263 ... days=45 score=90 ... result=cooldown shut=1/1`. `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` grew
by 234 between ticks 756 and 1006, the ticks less the sixteen built turns. **The game's own mix** (`AUDIO_FILE`,
`mixmatch.py`) holds `z_error.mp2` once in each run, matched at 0.998 with a gain of 0.261, 0.6 s after the
script first read tick 898 or later, where the three seconds before tick 890 match at 0.23. **Main's build**
(`run-control`, off-screen, the same file): no poll line of 263, no attempt, the count one more. **The stock park
as shipped** (`run-open`, off-screen): `score=0 ... tick=902 result=no-score shut=0/1`, no attempt.

**The original on the same file** (`q325/orig/run.sh`: `advisor.py ... 0x107 poll`, `tags.py` and `object.py`
on thing 20, the display grabbed every 1.4 s; six of six, the length on its second prediction). Thing 20 read
`state 3, canload 0` from the load on and nothing opened it; record `0x107` went from (0, -1, 0, 0) to **(902,
0, 1, 0) on tick 902**, 37.35 s after the load, with no slot seen holding it; the action's length read 4,998,
this game's 3,998 and the wait's thousand; no tag in 80 s; he is up with his antennae and no hat beside a bare
mast 2.3 s after the tick (`orig/a-g29.png`). The same original left running read the record still (902, 0, 1,
0) on tick 1251, past the row's turn on 1057. **The stock file itself** (`stock.TPWS`; `orig/d-load.log`):
`canload 1`, and the record unmoved with the log stamped to tick 964.

**The predictions that missed.** The line's length: I wrote 4,998 and 3,332 for this game, taking clip 15 for
1,666 ms by subtracting an old log's chain from its duration and forgetting the thousand
`Advisor.ParkResponseDuration` adds; clip 15 is 666 ms and clip 1 1,666, so the chain is 2,332 and the duration
3,998. Made again before the second run, and before any run of the original, whose 4,998 it then gave. The
sound's window: first a bound I had no measurement for (the quiet window read 0.31 where I wrote under 0.3), then
a window opened too late, since the script learns of a tick by a console reply up to a second after it; opened
from tick 898, it held.

| | The original | OpenTPW |
|---|---|---|
| Row 263 | polled; `StaffroomClosed.Score` where more than a quarter of the staff rooms are shut | the same |
| The stock park, its Staff Room's `mCanLoad` nought in the file | said on tick 902, line 0, no prop, no tag, a wait of 4,998 | the same, a wait of 4,998 |
| The stock park as shipped | not said | not said |
| The line's sound | response 513's sample, 638, `z_error.mp2` (read, not heard) | `z_error.mp2`, in the mix |
| A staff room shut by the player | no screen does it | no screen does it |

**Not built or not seen.** A staff room's own window is not built, in which the original has no door either.
The response's lip file 0 is not read: the sample's own number is looked for here and none is found. What a shut
staff room does to a tired member of staff was not looked for in either game (here `GoAndRest` does not ask
`CanLoad`). In no game run: a share between nought and the whole (the test holds the bound), the line said again
past its 120 quarter-ticks (the test holds it; the original stayed silent on tick 1057). A staff room bought into
Q327's Full Simulation park scored nought in both. The original's sound was not recorded or measured; neither game was heard by ear.

## A fresh Full Simulation park (Q327)

**No listed row's score asks the game type.** Every reference to `0x00fb3b7c` was set against the score functions of
rows 1, 2, 62, 80, 113, 185, 259 to 263 and 282 and every function each calls, three calls deep: none reads it (row
185's `0x005a06c0` is undefined in Ghidra, and byte for byte row 282's but its call displacements and its three
offsets). The game type enters at the poll alone, by a row's `+0x0c` (row 80's nought, "The poll", above). What a
Full Simulation park changes is what the scores read: a park made with no file is shut, holds nothing but its
gates and lights, and starts its clock and the poll's cursor at nought; its catalogue is the regular one.

**Its own lines, in both games** (`q327/confirm.py`, `orig/freshfs.sh make` with `advisor.py` from the click; a
fresh program, nothing done). Each row's turn is its place among the 155 polled rows from tick 1:

| Row | Turns | Answer | OpenTPW | The original |
|---|---|---|---|---|
| 185 | 110, 265 | 0 on day 4, 110 on day 11 | said on 265 | said on 265 |
| 282 | 148 to 458, 613 | 0 to day 19, 100 on day 26 | posted on 613, said on 707 | posted on 613, said on 775 |
| 62 | 502, 657 | 13 (day 21, under 25), 34 (day 28) | posted on 657, said on 755 | posted on 657, said on 861 |
| 1, 2 | every 155 from 1, 2 | 0: shut, nothing to ride | never said | never said |
| 80, 113, 259 to 263 | | 0: no researcher, no queue, nothing to be shut | never said | never said |

The order is the same, and the scores in the original's slots are this game's poll lines. **One line this game
does not build comes first there.** Message 0 (row 0, `Welcome`; response 1, sample 1, slot 1) is in a slot at
100,000 on tick 0 and said on tick 1, for 31.3 s, in every fresh park the original made (two of two); its poster is
not read (Q543). Message `0x119` (row 281, `GoldTicketExplanation`) is said on 612 in both, its turn, the first bus's
load of four having come on 604 in both (Q708, `q708/orig/O/loads.log`), his action about 24 s in each (24,164 ms there,
23,330 here, the random chain's gestures drawn anew), so
row 282's line waits to 775 there and 707 here, and row 62's to 861 and 755 (`q708/run3-G1`). A line's length in
sweeps is not compared: the original's sweep ran about 6.5 a second here and this game's 4.8.

**Row 80 in the original's own fresh park.** Its buy screen's misc tab bought one of each of the eight features on
offer and its hire screen one researcher: `0x50` was said on tick 2755 in the spiky hair (`orig/b/r-01.png`).
Saved from its menu on tick 3513 (cursor 160) and loaded by a Full Simulation player in both games, with `0x50`'s
record made fresh (`parkfile.advisor_forget`), row 80 was said on 3615 in both, score 60, line 0, and row 62 again
on 3912, score 457, line 2, refused on 3602 and 3757 by the same-message wait (`side-L.png`). The same file with
`mParkClosed` written nought (the fresh park opened with nothing to ride) had row 2 said on 3567 in both, score 99,
in the green ticket cap with tag 4 (`side-O.png`). The original said row 107's `0x6b` there on 3633 (score 90)
and one line no named message accounts for on 3825; neither is built here. In every one of these frames the
gadget's counters differ, the original's 3 tickets and 2 keys against this game's 0 and 0, because the players
differ, not the games: `freshfs.sh` copies Alexah's Full Simulation `gms.dat` into the original's player, and
`freshpark.py` makes this game's with none, picked from the list, which is given no first key (`FrontEnd.cs`,
`ClosePlayerSlots( newPlayer: false )`; only a player made in the slots is). This game with the same profile was not run.

## The buy screen's own line (message `0xbc`, Q328)

**The post.** The buy screen's opener, `FUN_004acc70( tab, item )`, has two halves. With the screen open
(`DAT_007cc1f8` not nought) it fills the list again, puts the tab's button down, selects the item's row and
returns: nothing is posted. Otherwise it builds the screen and its last act, at `0x004ad1e2`, is
`FUN_00486b00( 0xbc )`, reached even where the layout did not load. Its seven callers are the bar's four button
types, the gadget's button (`FUN_004a0940`), `FUN_0049b650` and one in no function (`0x004816ea`).

**The withdrawal.** The screen's handler, `FUN_004ac270`, on event `0x14` (the screen destroyed) noughts
`DAT_007cc1f8` and calls `FUN_00486b40( 0xbc )`: the same withdrawal as the message bar's ("The line of an opened
tag message"), so a screen closed while he says the line cuts him off, clears the said byte and counts a cut.
Every way the screen goes is that event: its exit button, a purchase (the handler closes the screen itself),
another park screen opened over it.

**Row 188** (`0x00770c20`) is row 284's twin: posted only, category 1, either game mode, score `0x0059e8c0`
(`HelpMessage.Score`, 1000), one copy, first response 407, one line. Response 407 is sample 380, lip 380, the
random gesture chain, model slot 0, no prop, group 383 (no tag); the transcript's row 380, 16.0 s: "this is where
you can buy stuff to building your park just click on something on the list to buy it click those buttons above
the list to choose from rides sideshows shops and miscellaneous items to hire staff click the button on the right
edge of the panel".
`Easymode.TPWI` holds no record of it (it holds the hire screen's, `0xbd`, as cut off once on tick 55), so the
shipped park says it at the screen's first opening.

**Also in the handler, read and not built.** A row's click that buys closes the screen and posts `0xc9` where
the item's `+0x4c` is nought and `FUN_004824d0( 0x80, 0 )` answers nought, and `0xce` otherwise; the two land
rows post `0x15a` (row -1) and `0x159` (row -2). Read in the decompiler alone.

### The original (`q328/orig/run.sh <file> a|b|c`, `original/advisor.py ... 0xbc,0xbd`, `tags.py`; predicted first, six of six)

The stock file (`q325/stock.TPWS`), the gadget's buy button clicked at (114, 590) and the screen's exit button at
(965, 430). **Let finish (a):** a slot read (thing 0, line -1, `0xbc`, 1000) on tick 862 and 0.18 s on the
record read (863, 0, 1, 0) with an action of 20831 ms; no tag was held; in the frame he is up at the lower right
with his antennae and no hat, "Buy Ride" open (`orig/a-speaking.png`). Closed 27 s on and opened again: no slot
held `0xbc` and the record did not change in 10 s. **Cut off (b):** said on tick 864; closed 6 s into the line:
(864, 0, **0, 1**), the action's length 0. Opened: in a slot on tick 912 and said on 913 (19998 ms); closed:
slaps 2; said on tick 959; closed: slaps 3; a fourth opening: nothing in 10 s. **This game's file (c)**, saved
after it said the line on tick 784 (`q328/run-saved/Q329.TPWS`): the record read (784, 0, 1, 0) and an action
of 19997 from the load, the click opened the screen, no slot held `0xbc` in 11 s and nobody is up
(`orig/c-open.png`).

**Row 200**, the research screen's (`ResearchScreen_Open` `0x004aa480` posts it last, its destroy, event `0x14`,
withdraws it; `q177/skeptic-analyser/meta_full.json` at `0x00770ec0`): posted only, category 1, **game mode 0, Full
Simulation alone** (the screen opens only there), score `0x0059e8c0` (`HelpMessage.Score`), one copy, first response
419, one line; response 419 is sample 391 (`hud.md`, "Opening and closing", has its words). Built as
`ParkAdvisorMessages.ResearchScreen` (Q590): said on the first tick the clock ran with the screen open,
`attempt=1 message=200 ... response=419 sample=391` (`q705/run3`).

### OpenTPW (`ParkAdvisorMessages.BuyScreen`; `ParkBuyScreen.Shown`, `Closed`)

A second posted tutorial topic, row 284's with response 407 and sample 380. `ParkBuyScreen.Shown` posts it the
first time the screen is shown, to the running park's advisor, and `Closed` withdraws it from that park's; a
screen already open is asked for nothing more, as the opener's first half asks nothing. Its record is read from
and written to a park file with the other built messages' ("The records in a park file").

`q328/confirm.py a|b|off|saved|control`, the stock park, both buttons clicked for real; predicted first
(`q328/PREDICTION.txt`), eleven of eleven on `run-a2`, `run-b`, `run-off`, `run-saved` and `run-control`
(`run-a1` two of three: the script's own click missed the exit button, which is centred with its screen in a
window wider than 4:3). The click logged `Park advisor: post message=188 subject=0 score=1000 threshold=25
tick=783 result=queued pending=2`, the file's waiting `0x80` the other; then `attempt=1 message=188 subject=0
score=1000 threshold=25 response=407 sample=380 line=0 tick=784 duration=18998` (the three said in `run-b`
18998, 19831 and 20665, where the original's three actions were 20831, 19998 and 19997: the chain is drawn, and
the original's number holds a second more); `advisor` read `saying sp_380.mp2 ... wearing=0,0` and
`message=188/score=1000/line=0/lastTick=784/said=1/slaps=0`, `tags` `standing=0`, `windows` `ParkBuyScreen tab
0`, and in the frame he is up at the lower right with no hat beside "Buy Ride" (`run-a2/1-speaking.png`).
Closed after the line: `withdraw message=188 cleared=0 cut=0 slaps=0`; opened again on tick 880:
`result=said-once`. **Cut (b):** closed three seconds in: `withdraw message=188 cleared=0 cut=1 slaps=1`,
`advisor` reading `quiet` a second on and the frame without him (`run-b/2-cut.png`); the next two openings said
it again 0.1 s on (ticks 818 and 840), cut to slaps 2 and 3; the fourth logged `result=slapped`. **`tutorial
off`:** `result=tutorial-off`, no attempt. **Saved and loaded back:** `restored message=188 line=0 lastTick=784
said=1 slaps=0`, and the click on tick 881 `result=said-once`. **Main's build:** the screen opens and no log line
names message 188.

| | The original | OpenTPW |
|---|---|---|
| The post | the opener, on a screen it has just built | `Shown`, the first time |
| The withdrawal | the screen's destroy (event `0x14`) | `Closed` |
| The stock park's first opening | said a tick after the click, no hat, no tag | the same |
| Closed in the line | cut off, said 0, a cut counted; three cuts and no more | the same |
| A purchase's `0xc9` and `0xce`, the land rows' `0x159` and `0x15a` | posted | not posted |
| The other 38 screens' lines | posted as each opens | the hire screen's `0xbd` is ("The hire screen's own line"); the other 37 are not posted |

**Not built or not seen.** The handler's four other posts. In no game run: a purchase or another screen's
opening cutting the line (the test holds the close, whatever closes it), the bar's buy button posting it (the
same `Shown`; Q322's run of the original saw it), a park left with the screen open, Full Simulation (the row's
mode word is row 284's, either mode; another screen's opening cutting it is run in "The hire screen's own
line"). Not heard in either game.

## The hire screen's own line (message `0xbd`, Q329)

**The post.** The hire screen's opener, `FUN_0049bdd0( tab )`, builds the screen where none is open
(`DAT_007ca308` nought), where the tab asked for is -1 (the one last left on) or where it is the tab shown, and
its last act on a built screen, at `0x0049c800`, is `FUN_00486b00( 0xbd )`. Where the layout does not load it
returns before the post. Asked for another tab with the screen open it first destroys the open screen (`FUN_00485b40` at `0x0049bdec`, whose
event 0x14 clears `DAT_007ca308` at `0x0049b7bd`), so it always builds the screen again and posts 0xbd; the tabs' own clicks are the handler's and never come here. Its eight
callers are the bar's five button types (`0xd` to `0x11`, a tab each), the gadget's button (`FUN_004a0940`, -1,
where the category was last left on hire), the buy screen's handler (`0x004ac4b2`, its button to hire) and one
in no function (`0x00481702`). Every opener begins with `FUN_00485b40`, which destroys the park screen that is
open (`DAT_007c24c8`), so the buy screen's button to hire withdraws `0xbc` before `0xbd` is posted.

**The withdrawal.** The screen's handler, `FUN_0049b650`, on event `0x14` (the screen destroyed) noughts
`DAT_007ca308`, frees what `DAT_007ca310` holds (`FUN_00475550`) and calls `FUN_00486b40( 0xbd )` at `0x0049b7e6`: the buy
screen's shape.

**Row 189** (`0x00770c58`) is row 188's twin: posted only, category 1, either game mode, score `0x0059e8c0`
(`HelpMessage.Score`, 1000), one copy, first response 408, one line. Response 408 is sample 381, lip 381, the
random gesture chain, model slot 0, no prop, group 383 (no tag); the transcript's row 381, 15.3 s: "this is
where you come to get staff for your park to hire someone just click their name on the list click the buttons
above the list to see different types of employees if you want to buy something you need to click the button on
the right hand side of the panel".
`Easymode.TPWI` holds its record as (55, 0, said 0, slaps 1): cut off once on tick 55. So the shipped park says
it at the screen's first opening, and two cuts, not three, silence it.

The two other `PUSH 0xbd` beside a call (`0x004b7cc3`, a string's lookup in `FUN_004b7b00`; `0x00499fd8` and
`0x004a4688`, a colour's bytes) are not the message.

### The original (`q329/orig/run.sh <file> a|b|c`, `original/advisor.py ... 0xbc,0xbd`, `tags.py`; predicted first, six of six)

The stock file (`q325/stock.TPWS`), the gadget's button clicked at (114, 590), the buy screen's hire button at
(958, 317) three seconds on and the hire screen's exit button at (965, 430). **Let finish (a):** `0xbc` said on
tick 862; at the hire button's click its record read (862, 0, **0, 1**) and a slot (thing 0, line -1, `0xbd`,
1000) on tick 888, and 0.16 s on the record of `0xbd` read (889, 0, 1, 1) with an action of 19165 ms; no tag was
held; in the frame he is up at the lower right with his antennae and no hat, "Hire Janitors" open
(`orig/a-speaking.png`). Closed 28 s on and the gadget's button clicked again: **the hire screen opened** (the
category is remembered), no slot held `0xbd` and the record did not change in 10 s (`orig/a-again.png`).
**Cut off (b):** said on tick 892; closed 7 s into the line: (892, 0, **0, 2**), the action's length 0. The
gadget's button: in a slot on tick 939 and said on 940 (19997 ms); closed: slaps 3; a third opening: nothing in
10 s (`orig/b-third.png`). **This game's file (c)**, saved after it said the line on tick 794
(`q329/run-saved/Q329H.TPWS`): the records read (781, 0, 0, 1) and (794, 0, 1, 1) from the load; the buy button
said `0xbc` on tick 1001, the hire button cut it to slaps 2 and opened the screen, no slot held `0xbd` in 10 s and
nobody is up (`orig/c-open.png`).

### OpenTPW (`ParkAdvisorMessages.HireScreen`; `ParkHireScreen.Shown`, `Closed`)

A third posted tutorial topic, row 188's with response 408 and sample 381. `ParkHireScreen.Shown` posts it the
first time the screen is shown, to the running park's advisor, and `Closed` withdraws it from that park's. A
screen is made new each time it is opened here, and opening it closes the park screen that is open, so the buy
screen's button withdraws `0xbc` first, as the original's. Its record is read from and written to a park file
with the other built messages' ("The records in a park file"), so the shipped park's cut stands from the start.

The gadget's button opens whichever of the two was last left on, as the original's (`hud.md`, "The six round
buttons"; Q330), so a second opening of the hire screen goes straight to hire. The runs below were made before
that, when the button always opened buy and a second opening went through the buy screen.

`q329/confirm.py a|b|off|saved|control`, the stock park, the three buttons clicked for real; predicted first
(`q329/PREDICTION.txt`), fifteen of fifteen on `run-a1`, `run-b`, `run-off`, `run-saved` and `run-control`, and
eight of eight again on the commit's build (`run-a2`, `run-b2`). Entering logged `Park advisor: restored
message=189 line=0 lastTick=55 said=0 slaps=1`. The hire button's click logged `withdraw message=188 cleared=0
cut=1 slaps=1` and `post message=189 subject=0 score=1000 threshold=25 tick=793 result=queued pending=2`, the
file's waiting `0x80` the other; then `attempt=2 message=189 subject=0 score=1000 threshold=25 response=408
sample=381 line=0 tick=794 duration=18165` (18998, 18164 and 19832 in the other runs, where the original's
actions were 19165, 19164 and 19997: the chain is drawn, and the original's number holds a second more);
`advisor` read `saying sp_381.mp2 ... wearing=0,0` and `message=189/score=1000/line=0/lastTick=794/said=1/slaps=1`,
`tags` `standing=0`, `windows` `ParkHireScreen`, and in the frame he is up at the lower right with no hat beside
"Hire Cleaners" (`run-a1/1-speaking.png`). Closed after the line: `withdraw message=189 cleared=0 cut=0
slaps=1`; reached again: `result=said-once`. **Cut (b):** closed three seconds in: `withdraw message=189
cleared=0 cut=1 slaps=2`, `advisor` reading `quiet` a second on (`run-b/2-cut.png`); reached again it was said
0.1 s on and cut to slaps 3; the third time `result=slapped`, the buy screen's own line cut to slaps 3 on the
way. **`tutorial off`:** `result=tutorial-off`, no attempt. **Saved and loaded back:** `restored message=189
line=0 lastTick=794 said=1 slaps=1`, and the click `result=said-once`. **Main's build:** the hire screen opens
and no log line names message 189.

| | The original | OpenTPW |
|---|---|---|
| The post | the opener, on a screen it has built | `Shown`, the first time |
| The withdrawal | the screen's destroy (event `0x14`) | `Closed` |
| The stock park's first opening | said a tick after the click, no hat, no tag | the same |
| Closed in the line | cut off, said 0, a cut counted; the file's cut and two more, and no more | the same |
| The buy screen's button to hire | cuts the buy screen's line, then the hire screen's is said | the same |
| The gadget's button after hire was open | opens hire | opens buy (the deviation) |
| The other 37 screens' lines | posted as each opens | not posted |

**Not built or not seen.** In no game run: a candidate chosen while he speaks (here the same `Closed`; what the original's
handler does to the screen at a row's click was not read for this item), the bar's
five hire buttons (types `0xd` to `0x11`; built since, "Worn rides and no mechanic"), the opener asked for the tab shown with the screen open
(a rebuild, reached by a bar button alone), a park left with the screen open, Full Simulation (the row's mode
word is row 188's, either mode). The American strings name the first tab "Hire Janitors", the English "Hire
Cleaners": the reference install reads the American. Not heard in either game.

## The map's own line (message `0x130`, Q666)

**The post and the withdrawal.** `Map_Open` (`FUN_005f0b40`) pauses the park (`FUN_004092a0( 0, 0 )`, `g_ParkRunning`
nought), stops him with the quiet stop (`Advisor_StopQuietly( 1 )`) and ends on `FUN_00486b00( 0x130 )` at `0x005f100d`.
The root's handler, on event `0x14` (`0x005f1176`), puts `g_ParkRunning` back to 1 (`0x005f120c`) and calls
`FUN_00486b40( 0x130 )` at `0x005f123c`. Every way the map goes is that event: OK and a click on the map through
`Map_CloseOnTheView` (`0x005f1050`), and Escape (`hud.md`, "The map's view"). The third `PUSH 0x130` (`0x004c01df`) is
an argument to `FUN_00485b00`, not the message.

**Row 304** (`0x00772580`) is row 188's twin but for its response: posted only, category 1, either game mode, score
`0x0059e8c0` (`HelpMessage.Score`, 1000), one copy, first response 564, one line. Response 564 is sample 582, lip 582,
the random gesture chain, model slot 0, no prop, group 383 (no tag); the transcript's row 582, 26.4 s: "the map shows an
overview of the park and some other important information the plus minus and arrow buttons down near the bottom left
let you soon in and out and scroll the map ...".

**He never says it.** He says a slot only on his turn in the sweep ("The advisor's turn in the sweep"), and the map
holds the park still from before the post to after the withdrawal, so the slot is emptied unsaid and the record is never
written. The sample is in the game's data and no path of the park reaches it.

### The original (`q666/orig/run.sh`, `original/advisor.py ... 0x130,0xbc`; predicted first, four of four)

`q325/stock.TPWS` off-screen, Space, 10 s, OK, 10 s; Space, 3 s, Escape (`q666/PREDICTION.txt`, round orig-a). The slot
(thing 0, line -1, `0x130`, 1000) was taken on tick 915 beside the file's waiting `0x80`; 12 s later OK emptied it, the
tick still 915; posted again on tick 970 (the park ran between) and Escape emptied it on 970. Message `0x130`'s record
read (0, -1, 0, 0) throughout; over the map nobody is up (`orig/a-2-open-later.png`). `save/` the same.

### OpenTPW (`ParkAdvisorMessages.MapScreen`; `ParkMapScreen.Shown`, `Closed`)

The buy screen's shape: `Shown` posts it the first time the map is shown, to the running park's advisor, and `Closed`
withdraws it from that park's. The map pauses the game clock, so the sweep whose last call is his turn does not run.
Its record is read from and written to a park file with the other built messages'.

`q666/ours.py`, the stock park entered from the lobby, the desktop, the final dll (`293ca0b0`), predicted first, five of
five (`run1`): Space logged `Park advisor: post message=304 subject=0 score=1000 threshold=25 tick=788 result=queued
pending=2`; 9 s on `sweeps` read mGameTick 788, `advisor` held `slot=304`, no attempt (`run1/1-open-later.png`, the map
and nobody up; `q666/side-by-side.png` beside the original's); OK: `withdraw message=304 cleared=1 cut=0 slaps=0
pending=1`, and 10 s on, tick 835, `message=304/score=1000/line=-1/lastTick=0/said=0/slaps=0`; Space again posted on
tick 845 and Escape withdrew it (`cleared=1 cut=0`); `unimplemented` names no `MAP_ADVISOR_LINE`. **Main's build**
counted `MAP_ADVISOR_LINE` at the opening and logged no line naming message 304 (`q665/run3`).

| | The original | OpenTPW |
|---|---|---|
| The post | the opener's last act | `Shown`, the first time |
| The withdrawal | the root's destroy (event `0x14`): OK, a click, Escape | `Closed` |
| Said | never: the park stands while the map is up | the same |

**Not built or not seen.** A map opened while he says another line (the quiet stop is Q624's), Full Simulation (the row
is either mode), a park saved with the map up. Not heard in either game.

## A project finished, and a group raised (messages `0x5d` to `0x61` and `0x85` to `0x88`, Q702)

The posters are the lab's message `0x11` (`park-engine.md`, "The lab at work"). **The rows** (the metadata table,
`meta_full.json`; the response table `0x00768fb8`, 32 bytes a response; the tag table `0x0075eca8`):

| Row | Message | Poster | Mode | Copies | Responses | Samples | Group | Tag (key, string, button, kind) |
|---|---|---|---|---|---|---|---|---|
| 93 `ResearchRideResearched`, 150 | `0x5d` | `mCat` 0 | both | 255 | 248, 249 | 282, 283 | 95 | 63, 114, 9, 2 |
| 94 `ResearchAddonResearched`, 130 | `0x5e` | `mCat` 4 | both | 255 | 250 | 284 | 96 | 65, 115, `0x13`, 2 |
| 95 `ResearchShopResearched`, 130 | `0x5f` | `mCat` 1 | both | 255 | 251, 252 | 285, 286 | 97 | 74, 116, `0xa`, 2 |
| 96 `ResearchSideshowResearched`, 110 | `0x60` | `mCat` 2 | both | 255 | 253, 254 | 287, 288 | 98 | 78, 117, `0xb`, 2 |
| 97 `ResearchFeatureResearched`, 90 | `0x61` | `mCat` 3 | both | 255 | 255, 256 | 289, 290 | 99 | 82, 118, `0xc`, 2 |
| 133 to 136 `NewResearchGroup{Ride,Shop,Sideshow,Feature}`, 40 | `0x85` to `0x88` | a raise, kinds 0 to 3 | Full Simulation | 1 | 318, 320, 322, 324 (two each) | 432 to 439 | 155 to 158 | none, 154, `0x13`, 0 |

Every row is posted only, category 0, the research hat (prop 10), no gesture row; each score function is a bare
`MOV EAX,[ECX+off]` of its key (`0x0059e7f0`..`0x0059e830`, `0x0059f3d0`..`0x0059f400`). The finish's post
(`Advisor_OnMessage` `0x0059b0c6`) is about no thing, the item the record's second word, so the tag's text (kind 2)
names it: string 114 "Your scientists have researched the {P0} ride!". The raise's post (`Lab_RaiseHighestGroup`
`0x00503c8e`) hands nought for both. **A Full Simulation row posted in Instant Action is slotted at -1**: `AdvisorPost_Make`
(`0x0059b590`) takes the score from `Advisor_RowScore` (`0x0059bf20`), -1 for a row of the other game, and `Advisor_Post`
(`0x0059a940`) slots it past the gate all the same; the tick never says a slot at or under the threshold. The samples were
checked against `global-speech-transcripts.tsv` (282 "the researchers have come up with a new ride", 432 "a new set of rides
has become available to research").

**Measured** (`q702/orig`, the original off-screen, `advisor.py 0x5d,0x85`): the stock park's finish on tick 820 said
`0x5d` on the same tick, line 0, the action 7498 ms, and held `0x85` at -1 to the end of the run. **OpenTPW**
(`ParkAdvisorMessages.ResearchedOf`, `RideResearched` and the eight beside it, `NewRideGroup`; `Post`'s `secondary`;
`FilteredScoreOf`; tag rows 95 to 99 and 155 to 158; `q702/run1`, `run2`): `0x5d` said on tick 820, line 0, `0x85` held at -1, and sp_282 found in its own mix. The action's
length is the speaker's and was not compared (6498 + 1000 ms in run1, 5665 + 1000 in run2).

## Rows settled from the bytes by the fork watch (2026-10-16)

Read from the listing of `testme.exe` (`verify-advisor.md`, `~/.cache/tpw-harnesses/fork-review-2/watch/2026-10-16/`); no
game run. Row numbers are metadata rows; the rows' keys are in the Advisor.sam sections the schema walk names.

| Row | Callback | What it scores | State here |
|---|---|---|---|
| 3 `VisitorsThirsty` (message 3) | `0x0059c9d0` | the count of park guests whose thirst float `+0x1a4`, cut by `__ftol` to its low byte, is above `VisitorsThirsty.ThirstierThan` (`+0xb0`), times `.ScorePerThirstyPerson` (`+0xac`); the count is `0x004c93f0`, ECX ignored | built, Q530 ("Thirsty and hungry guests") |
| 4 `VisitorsHungry` (message 4) | `0x0059ca00` | the same with hunger `+0x1a8` (`0x004c9340`), `HungrierThan` `+0xb8`, `ScorePerHungryPerson` `+0xb4` | built, Q530 |
| 85 `FeaturesNotEnoughToilets` | `0x0059e420` | **always nought**: it runs the average toilet queue (`0x004cca40`) and discards it (`FSTP ST0` at `0x0059e444`; `XOR EAX,EAX; RET` at `0x0059e446`), reading neither `.AverageQueueLongerThan` nor `.Score`. Its lines (responses 233-234) are never said by the original | built as a nought, Q530 |
| 125-127 prank advice | `0x0059f360`, `0x0059f370`, `0x0059f380` | the keys `PrankSbomb.Score`, `PrankLitter.Score`, `PrankBalloon.Score` (`+0x3d4`, `+0x3d8`, `+0x3dc`), 35 each in the shipped file (above the 25 cut) | unbuilt |
| 167 (message `0xa7`) | `0x0059f9a0` | the constant 10000; posted when the last research project is finished, at `0x00503ee9` in Instant Action and at `0x00503f8d` in Full Simulation once every park of the player's is all researched (`park-engine.md`, "The lab at work") | built, Q550: posted about no thing, responses 380-381 (samples 452-453) on the theme's own advisor, group `0x17f` (no tag); said first on the end's tick in both games |
| 168 (message `0xa8`) | `0x0059f9b0` | the constant 9999; nothing found posts it | unbuilt |

- Rows 3 and 4 ask for no door (`0x0051a280` is not called, unlike rows 82, 85 and 86), no guests-inside division and no age:
  "Thirsty and hungry guests", above, where they are built and run in both games (Q530).
- Rows 125-127: the post (`0x004ff2c7` to `0x004ff307`) sends the guest's id twice and `id % 3` (`DIV 3` at `0x004ff2f8`) in the
  record's `+0xc`, vtable `0x007007a8`. The receiver `ReceiveMessage` `0x0059b060` arm for message type `0xe` (`0x0059b221`) builds a record
  with the guest's id (message `+8`) and the guest's packed cell (`byte[+7] << 7 + byte[+5] + 1`, `0x0059b25c` to `0x0059b26e`) and
  switches on `+0xc`: 0 posts advisor message `0x7d`, 1 `0x7e`, 2 `0x7f`; anything else posts nothing. The prank done is
  chosen by `mPrankeryIndex` = 100 + id % 3 (`guest-arrivals.md`), so for a guest the constructor made the advice names the
  prank done; they could part only for a loaded guest whose `+0x1c0` disagrees with its id % 3 (not read).
- Row 291 (`HelpMessage`, message `0x123`, getter `0x0059e8c0`) names response 551, the only id from 540 to 612 that no
  row holds: `Advisor_SayResponse` finds nothing, returns 0 and plays nothing. Nothing was found that posts message `0x123`.
- Row 86 (`0x0059e450`) asks for no handyman first (`FUN_004c9960( 5 )` above nought gives nought), then bounds the filth
  by `FeaturesDirtyToilets3.FilthierThan` (`+0x2e0`) as well as `DirtyToilets1`'s: a fact for whoever builds rows 86 to 89.
- Fifteen callbacks' keys, taken from the schema walk, were each checked against their disassembly and first response's transcript and
  agree (rows 15, 16, 17, 86, 92, 100, 107, 114, 140, 141, 169, 172, 238, 258, 323); a key is the first `MOV` load of the function,
  not always the first load.

## Rows settled from the bytes by the fork watch (2026-10-18)

Read from the listing of `testme.exe` (`verify-advisor.md`, `~/.cache/tpw-harnesses/fork-review-2/watch/2026-10-18/`); no game run.

- **Rows 100, 101, 102 and 107** (the money rows, `0x0059e8d0`..`0x0059edb9`) and the loan helpers they ask are in
  `ride-operation.md`, "The loan helpers and the money rows" (fork watch 2026-10-18, `verify-advisor.md` A1). Row 102 fires at a balance
  below nought, the `JGE` at `0x0059e9c8` (A7).
- **Role coverage, `0x004cd340( role )` and `0x004cd500( role, mode )`** (fork watch 2026-10-18, `verify-advisor.md` A2). `0x004cd340`
  clears byte `+0x3c` of all 128 x 128 cell records (stride `0x44`, world `+0x2d8`), then for every thing whose type byte (`+2`) is in
  4 to 8 and equals the role and that has a patrol area (`0x00506eb0`: `+0x20a`/`+0x20c` not (1, `0x4000`)) writes 1 to `+0x3c` of
  every cell of the rectangle `0x00506e70` gives: a flag, not a count. Its one caller is `0x004cd50a`. `0x004cd500` then counts cells
  by mode (jump table `0x004cd708`; the cell's type is `+8`): mode 0 every cell **not** type 2, 7 or `0x1e`; mode 1 types 1, 3, 9,
  10, 4, `0x15`, or `0x005363f0`'s test (not read); mode 2 type 9 only; mode 3 types 1, 3, 9 (path, queue or entrance:
  `ride-operation.md`). The share is covered x 100 / count (`IDIV`), nought with no cells. Callers: `0x0059d218` (role 4, mechanics,
  mode 2), `0x0059d383` (role 5, handymen, mode 3), `0x0059d5f3` (role 6, entertainers, mode 1); each scores ( 100 - share ) x
  `[+0x140]` / `[+0x158]` / `[+0x190]` and gives nought when that is below `[+0x13c]` / `[+0x154]` / `[+0x18c]` (the `Advisor.sam`
  comment on `StaffMechanicCoverage.Score` and its kin); the mechanics' one first needs months at least `[+0x138]`, mechanics
  (`0x004c9960( 4 )`) at least `[+0x12c]` and `0x004c9b40( 4 )` non-zero. These rows are **Full Simulation's alone** (the mode-0 list
  above holds 18 to 21 and 23 to 27), so the shipped Lost Kingdom park never reaches them; the guard's coverage callbacks
  (`0x005d60c0`, registered at `0x005a1238` and `0x005a1257`) were never measured. The mechanics' row (19) is measured in
  "The mechanics' patrol areas", below.

## The mechanics' patrol areas (message `0x13`, row 19, Q602, decode)

**Row 19, message `0x13`:** polled (`+0x00` nought), category 0, game mode `+0x0c` nought (Full Simulation alone, as the
role-coverage bullet above says), one copy, first response 55, three lines, selection mode 2 (advance). The three
responses are samples 55, 56 and 57, each with no gesture row, model slot 0, props (5, 0), the hard hat, and group 383
(no tag): "there's never a mechanic around when you need one...", "the mechanics are patrolling all of the areas in your
park...", "there are areas in your park without a mechanic patrolling them" (`global-speech-transcripts.tsv`). The
second says the opposite of the row's condition; it is said as the cycle comes to it.

**Its score, `0x0059d180`** (`Advisor_Score_MechanicCoverage` in Ghidra, made for Q602; in no function before), ECX the balance object:

1. Months: `FUN_004f8800`'s time since `mGameTick` nought over `0x1792F8648000` (30 days) by `__alldiv`; below `[+0x138]`
   (`StaffSetMechanicArea.MonthsIntoGameForConsideration`, 13): nought.
2. Mechanics, `0x004c9960( 4 )` on the park analyser: below `[+0x12c]` (`StaffSetMechanicArea.AtLeastThisMany`, 3): nought.
3. `0x004c9b40( 4 )`, the staff of kind 4 to 8 that equal the role (or every one for role nought) and have a patrol area
   (`0x00506eb0`: corners not (1, `0x4000`), the whole map): nought answers nought.
4. The share, `0x004cd500( 4, 2 )` (the bullet above): the cells of `mType` 9, the entrances (`park-engine.md`), inside any
   mechanic's area, x 100 over all of them, cut. The score is ( 100 - share ) x `[+0x140]`
   (`StaffMechanicCoverage.ScorePerNonCoveredPercent`, 5), nought below `[+0x13c]` (`StaffMechanicCoverage.Score`, 300):
   **said while the mechanics' areas hold 40% of the entrances or fewer**, scoring 300 to 500. A mechanic patrolling the
   whole map counts in 2 and not in 3 or 4. `StaffSetMechanicArea.DontBotherIfPatrolledUp` and `.ScorePerUnpatrolledPct`
   (`+0x130`, `+0x134`) are not read here.

**When it is said.** As any polled row (above, "The poll"): on the tick the cursor stands on row 19, posted and said on that
tick where the advisor is free, else it waits in a slot. Measured in the original, off-screen (`q602/run.sh`, Q707's O2
setup: Alexah's jungle `New Save` loaded on tick 19004 by her Full Simulation profile with no ticket; `brk.sh` on the four
compares): the poll stood on row 19 on tick **19084** (`advisor.py`'s `poll=20`), and every reading was the file's
(`q602/predict.py`, `parkfile.py -V`: five mechanics, each with an area, 25 of 71 entrances inside them): `eax 1b` (27 months), `eax 5`,
`eax 23` (share 35; ECX 25, EDX 15 the remainder), `eax 145` (score 325 against 300). The same four on the same tick
again, the tick's rescore of its answer (`FUN_0059bf20`). Said at once: sample 57 in the hard hat, props (5, 0)
(`orig-R1/line.log`, `g-10.png`), the record from (18464, 1, 1, 0) to (19084, 2, 1, 0): line 2 after the file's line 1, and
`said` a flag that stays 1. Not measured: the line after the third (mode 2's turn back to line 0), and a park whose
share or mechanics fail a test.

**OpenTPW** does not build it: the row is counted `PARK_ADVISOR_POLLED_ROW_NOT_BUILT`, so on that load it says no line on
19084. Its advisor says the hint `0x13a` on 19059, which the original's leaves waiting because row 257's sample 525 holds
it from 19040 to 19076 (`q602/orig-R1/line.log`; `ride-operation.md`, "The hints"), and row 257 is not built either (Q713).
The row is Full Simulation only (`+0x0c` 0), and with no `mType` 9 cell the share is nought (`0x004cd6df`), the score 500.
The build is Q609.

## Shop and sideshow value and satisfaction, rows 238 to 257 (fork watch 2026-10-19, Q713)

Read from the listing of `testme.exe`, every callback whole (fork watch 2026-10-19, `verify-advisor-ui.md` L1; rows 239
to 241 and every helper read whole by Q713, `q713/gd/`). The score callback of row N is the dword at
`0x0076e300 + N x 0x38 + 0x10` (the stores `0x005a5f7f`..`0x005a6527`). Each callback first asks its helper for a qualifying
object (a handle, kept at the balance object's `+4`, which nothing reads: "A queue not joined to a path") and its
figure `v` (kept at `+8`), nought if none. So the line is about no thing, as every polled line is.

| Row | Callback | Helper (group) | Scores | Log |
|---|---|---|---|---|
| 238 | `0x0059fb60` | `0x004cadf0` (2) | `v` below `[+0x4d8]`: ( `v` - it ) x `[+0x4dc]` | "Food poor value (%d)" |
| 239 | `0x0059fbd0` | `0x004cb640` | `v` below `[+0x4e0]`: ( `v` - it ) x `[+0x4e4]` | "shops poor value (%d)" |
| 240 | `0x0059fc40` | `0x004cadf0` (3) | `v` below `[+0x4e8]`: ( `v` - it ) x `[+0x4ec]` | "restaurants poor value (%d)" |
| 241 | `0x0059fcb0` | `0x004cadf0` (4) | `v` below `[+0x4f0]`: ( `v` - it ) x `[+0x4f4]` | "drinks poor value (%d)" |
| 242 | `0x0059fd20` | `0x004cc090` | `v` below `[+0x4f8]`: ( `v` - it ) x `[+0x4fc]` | "sideshows poor value (%d)" |
| 243 | `0x0059fd90` | `0x004cb200` (2) | `v` below `[+0x500]` | "Food poor satisfaction" |
| 244 | `0x0059fe00` | `0x004cba90` | `v` below `[+0x508]` | "shops poor satisfaction" |
| 245 | `0x0059fe70` | `0x004cb200` (3) | `v` below `[+0x510]` | "restaurant poor satisfaction" |
| 246 | `0x0059fee0` | `0x004cb200` (4) | `v` below `[+0x518]` | "drinks poor satisfaction" |
| 247 | `0x0059ff50` | `0x004cc090` | `v` below `[+0x520]`: ( `v` - it ) x `[+0x524]` | "sideshows poor satisfaction" |
| 248 | `0x0059ffc0` | `0x004caae0` (2) | `v` above `[+0x528]`: ( `v` - it ) x `[+0x52c]` | none |
| 249 | `0x005a0010` | `0x004cb300` | `v` above `[+0x530]` | none |
| 250 | `0x005a0060` | `0x004caae0` (3) | `v` above `[+0x538]` | none |
| 251 | `0x005a00b0` | `0x004caae0` (4) | `v` above `[+0x540]` | none |
| 252 | `0x005a0100` | `0x004cbba0` | `v` above `[+0x548]` | none |
| 253 | `0x005a0150` | `0x004cb100` (2) | `v` above `[+0x550]` | none |
| 254 | `0x005a01a0` | `0x004cb980` | `v` above `[+0x558]` | none |
| 255 | `0x005a01f0` | `0x004cb100` (3) | `v` above `[+0x560]` | none |
| 256 | `0x005a0240` | `0x004cb100` (4) | `v` above `[+0x568]`: ( `v` - it ) x `[+0x56c]` | none |
| 257 | `0x005a0290` | `0x004cbec0` | `v` above `[+0x570]` | none |

Every row scores ( `v` - its line ) x its second word where its comparison holds (the poor rows `JGE` past, the great
`JLE`, both signed), and nought where the helper finds nothing (the handle's word nought). **The settings words are
`Advisor.sam`'s keys** (`lib/advisorschema.py`, the schema walked from the executable): `+0x4d8`/`+0x4dc`
`FoodPoorValue.ValueWorseThan`/`.ScorePerPoint`, then in steps of eight `ShopsPoorValue`, `RestaurantPoorValue`,
`DrinksPoorValue`, `SideshowsPoorValue`, the five `...PoorSatisfaction.SatisfactionWorseThan`, the five
`...GreatValue.ValueBetterThan` and the five `...GreatSatisfaction.SatisfactionBetterThan`, each with its
`ScorePerPoint`, to `+0x574`, in the table's order. Shipped: the poor lines 50 at 10 a point, the great value 80 at 5, the
great satisfaction 70 at 4. **Row 247 is the sideshows' poor satisfaction by name and judges them by the walk-away share**,
the helper row 242 calls (`0x0059ff69`); there is no lowest-satisfaction helper for the sideshows. The metadata rows
(`advisorrow.py`): every one polled, either game, one copy, one line, selection 2, no tag (group 383); response the row
plus 240, sample the row plus 268 (`global-speech-transcripts.tsv` says each line's words: 506 "visitors don't think your
food is a very good deal", 525 "people who have played your sideshows don't feel cheated"); the food's, restaurants' and
drinks' lines in the fast-food hat and the spatula (13, 14), the shops' and sideshows' in the kiosk hat (8, 0); rows 238
to 247 category 0, rows 248 to 257 category 3, the congratulations (`MinTimeSameMessage` 1800, so a row said waits 7,200
sweeps before it is said again).
- **The groups are the log strings' words.** `UsageInfo.ShopType` (`+0x164`): 2 food, 3 restaurant, 4 drinks; the shops
  `0x004cba90` and `0x004cb980` take types 1, 5 and 6; the sideshows are `Info.WhichUIType` (`+0x4ac`) 2 (`0x004cc090` at
  `0x004cc121`, `0x004cbec0` at `0x004cbf4d`). Satisfaction is `FUN_004e1e30` (`ride-operation.md`, "Customer
  satisfaction"); the helpers `0x004cb100`, `0x004cb200`, `0x004cb980` and `0x004cba90` keep the best or worst of the group by
  `FUN_004e1e30`, strictly, the first of equals winning.
- **"Value" is the walk-away percentage.** `0x004caae0` and `0x004cc090` take the things of the group (type 3) older than
  30 days (`0x004dd670` above `0x1e`) whose customers ring (`+0x1a8`, the last min( filled, 30 ) days through
  `0x00495cf0`) sums above nought (`0x004cac23`, `0x004cc1e0`), and make `v` = the sum of the walk-aways ring (`+0x230`)
  x 100 / the sum of the customers, an unsigned `DIV`; `0x004caae0` keeps the highest (strict), `0x004cc090` the lowest
  (strict), the first of equals. **So the "great value" rows 248 to 252 fire for a shop whose walk-aways pass 80% of its
  paying customers** (shipped `ValueBetterThan` 80, `ScorePerPoint` 5: 86% scores 30). It is the original's quirk, kept as
  it is when built. Walk-aways are counted only on a price refusal (`ride-operation.md`, "Walk-aways").
- **What the helpers keep.** The share's helpers (`0x004caae0`, `0x004cadf0`, `0x004cb300`, `0x004cb640`, `0x004cbba0`,
  `0x004cc090`) and the satisfaction ones (`0x004cb100`, `0x004cb200`, `0x004cb980`, `0x004cba90`, `0x004cbec0`) walk the
  world's things (`FUN_00516130`), type 3, the group's, more than thirty days old (signed). Only the sideshows' best
  satisfaction (`0x004cbec0`) also asks for customers in the last thirty days; the other satisfaction helpers take an
  outlet that had none, at 50. `UsageInfo.ShopType` is in every one of the 32 shops of the four themes and in nothing
  else (1 four times, 2 twelve, 3 to 6 four each; FileFormats `sam.md`).
- **The poor rows never play.** Rows 238 to 247 score ( `v` - line ) x `ScorePerPoint` only when `v` is below the line, so
  never above nought with the shipped positive `ScorePerPoint`.
- **In Lost Kingdom** rows 248 to 257 are reachable once a shop or sideshow is older than 30 days; 238 to 247 score
  below nought and are never said, in both games.

### OpenTPW (`ParkAdvisorMessages.OutletTopics`, `Outlet`, `OutletOf`, `OutletJudgement`, `OutletFigures`, `Pick`, a topic's `Judges`; `ParkFacts.Outlets`; `ItemDescriptionFile.ShopType`)

Twenty polled topics built on the poll's machinery. `ParkPeople.AdvisorFacts` lists every shop and sideshow in the chain's
order with its outlet (`OutletOf`: the item's `ShopType`, else a sideshow's `WhichUIType`), its age
(`ParkState.AgeInDays`) and its rings' last thirty days (customers and walk-aways added unsigned, satisfaction
`SatisfactionOfLastDays`); `Pick` keeps the helpers' best or worst, and the score is the callback's. The poll line ends
` outlet=... thing=N figure=V line=L age=A customers=C walkaways=W satisfaction=S`, the outlet picked; the console's
`outlets` prints the list. **Said as the original's quirk:** a great value row is said of an outlet whose walk-aways pass
80% of its customers (the code's own comment says so at `OutletTopics`).

**This game** (`q713/PREDICTION.txt`, rounds A to D, each predicted before its run; `q713/confirm.py`, off-screen in lane
B, Alexah's jungle `New Save` loaded on tick 19004 by Q707's F player, as the original's `q707/orig/O2` was). *The file
as it is* (`run3-fs`): rows 238 to 256 polled on ticks 19021 to 19039, each score and outlet as predicted from the load's
census (238 -500 on food 257, ... 254 `cooldown` 40 on shop 47, its record said on 11907); row 257 on 19040 scored 24,
`under-threshold`: sideshow 50's satisfaction stayed 76 through the rings' rolls on 19008 and 19032, where the original said it
(some sideshow of its at 77 or more; which one, and its figure, were not read). The miss is the guests' timing, not the row:
guest 467's visit (`Happiness changed by 300 since using object 50`, `run3-fs/run.log:3061`) landed after the 19032 roll,
and the sideshow reached 86 at 19055. *Made files* (`parkfile.ring_set`): with food
stall 193 given a day of seven walk-aways to its seven customers and row 248's record made fresh (`run4-made`), `poll
message=248 ... score=100 ... tick=19031 result=post outlet=Food thing=193 figure=100 line=80` and the line said on that
tick, response 488, sample 516, in the fast-food hat and the spatula (13, 14); with sideshow 50's thirty satisfaction days
40 (`run6-made`, read 87 at a gate on 19034), `poll message=257 ... score=68 ... tick=19040 result=post outlet=Sideshows
thing=50 figure=87 line=70` and `attempt=1 message=257 ... response=497 sample=525 line=0 tick=19040`, in the kiosk hat
(8, 0), no tag: the original's tick, sample and props as it said them on the unmodified `New Save` (`q707/orig/O2`).

**The original on the made file** (Q743, `q743/PREDICTION.txt`, `q743/orig/run.sh`: `fsload.sh` by the same player,
`advisor.py ... 0xf8,0x101 poll`, `advisorline.py`, `thingwords.py` on stall 193's `+0x1a0` and `+0x1a4`). Row 248's
record went from (0, -1, 0, 0) to (19031, 0, 1, 0) on tick 19031, said at once: sample 516, props (13, 14), the action
7,498 ms (clips 4,832 + 2,666; this game's `waitUntil` less its start is 7,498 too, the log's `duration=` being the
speaker's 6,498 before its 1,000). The original run read only stall 193's lifetime counts (`+0x1a0`, `+0x1a4`), which stood at 194 and 12 from the load to
19049: no sale and no walk-away in that time. It did not read the 30-day rings the poll reads (`+0x1a8`, `+0x230`), so
what the 19008 roll left in them is not measured; the score was not read (no poll ever saw `0xf8` in a slot: posted and
taken on one tick); and which food outlet the original judged is not read either (the helper keeps the highest-share
food thing; "stall 193" is this game's pick, `q743/runG3.out`, assumed for the original). What the reading supports is
that some food outlet's share passed the line, since 248 was said on 19031. Row 257 was slotted on 19040 at **72** (a
sideshow at 88, worked back by the formula 70 + 72 / 4, not read; this game's read 78 and slotted 32; which one is
Q662's) and not said by then: `0x13a` (this game's 314, posted on 19059 at 150) took the free advisor on 19062, and the
19100 awards replaced the slot. `0x101` was slotted again, at 40, on 19350 and was still unsaid at the last advisor read,
19389 (`q743/orig/O/load.log`; the readers stopped near 19430). This game on the same file (`q743/runG2`, `runG3`, the
desk) says 248 on 19031 and leaves 257 unsaid behind 314 alike, but says 314 on **19067**, five sweeps after the original.
Both desk runs ran below 10 frames a second (every clock stamp in their `run.log`s sits on a 100 ms grid; about 75
clamped frames in the 8.93 s), so `GameClock.Now` advanced 0.1 s a frame while the sweeps took the raw time: the
clamped-frame deviation above gives a lag of about 1.5 s (7.500 s of `Now` over 36 sweeps of 0.248 s). Whether the lag
shows at a normal frame rate is Q679.

| | The original | OpenTPW |
|---|---|---|
| Rows 238 to 257 | polled; the outlet's figure against its line | the same |
| Row 257 on Alexah's `New Save` | said on 19040, sample 525, props (8, 0) | scored 24 on 19040 (76); with the file's satisfaction made 87, said on 19040, sample 525, (8, 0) |
| Row 254 on it | slotted at 28 on 19347, after its 7,200-sweep wait from 11907 | `cooldown` on 19037 at 40 (not run to 19347) |
| A food stall walked away from by all its customers (made file B) | row 248 said on 19031, sample 516, (13, 14) | the same, at 100 |
| Row 257 on made file B | slotted on 19040 at 72, not said by 19062 (`0x13a` said then), slotted again at 40 on 19350, unsaid at 19389 | slotted at 32, never said (314 said on 19067 in desk runs below 10 frames a second, Q679) |
| The poor rows | below nought, never said | the same |

**Not built or not seen.** The original's score for row 248 (taken on its tick). A great row said a second time past its 7,200 sweeps. The
poor rows only as unit tests (no shipped setting makes them speak). Not heard (off-screen).

## Row 172 and row 72 read whole (fork watch 2026-10-19)

- **Row 172** (`0x0059fb10`, record store `0x007708b0`): all staff (`0x004c9a10`, things of kind 4 to 8) above `[+0x4d0]`
  (strict) **and** the headquarters thing's (`0x00519450`) byte `+0x20` nought scores `[+0x4d4]`. That byte is
  `mHaveEverTrained` (set by the training budget screen at `0x004b25d0`, saved by `0x0050c640`; `hud.md`, "The Staff Training
  Budgets screen"; FileFormats `saves.md`, byte 102): staff above the limit and no budget ever set. Shipped, the limit is 15
  and the score 50 (`Advisor.sam`; the key names `TrainStaffGeneral.MinStaffForMessage` and `.Score` are the fork's, offsets
  not tied): sixteen staff and never a budget (fork watch 2026-10-19, `verify-advisor-ui.md` L5).
- **Row 72** (`0x0059dd40`, record store `0x0076f2d0`) reads, in order: researchers `0x004c9960( 8 )` non-zero; `0x004ca7b0(
  1, 0 )` = n non-zero; `0x004ca650` non-zero (the shop-research test: the lab `0x00519550( 1 )` into `0x00503b30`, kind 1, the
  twin of `0x004ca630`, `0x004ca670` and `0x004ca690` for kinds 0, 2 and 3); the research rate `0x00503980` of kind 1 nought
  (a `JA` leaves otherwise); `0x004ca510( 0 )` equal to n; then `[+0x264]`, else nought. Not proven: what `0x004ca7b0( 1, 0 )`
  and `0x004ca510( 0 )` count (the fork says shop types built and available) (fork watch 2026-10-19, `verify-advisor-ui.md` L6).
