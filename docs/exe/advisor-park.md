# Park advisor (original executable)

The park advisor is a THING like the weather, ticking on the same beat. It hears in two ways: the park **posts a message** to it, and **each tick it polls one row** of the metadata table below, calling that row's score function (`FUN_0059a550` calls `FUN_0059c680( 1 )` on the static at `0x00fb3540`, built by `FUN_0059bea0`, whose `+0x20` is `MinScoreForConsideration` (`DAT_00fb3560`); it steps its cursor at `+0` over the 351 rows to the next whose `+0x00` is nought, 156 of them, calls that row's score function and keeps a score above nought that the accept gate `FUN_0059abc0` would take; the tick raises it when `FUN_0059bf20` answers above `MinScoreForConsideration`, 25). Either way the message is accepted or rejected by a gate, and an accepted message occupies one of **eight priority slots**. Each tick the advisor considers the highest-scoring slot, speaking only if its score passes the threshold (Q90 below), then picks a line within that response and plays a sample. Two separate tables sit behind this — a static response table at `0x00768fb8` that maps a response id to a sample, lip file and gesture, and a runtime-filled metadata table at `0x0076e300` that holds the per-message score function, category and line count. The scores' coefficients live in `data/Advisor/Advisor.sam`, whose schema is compiled into the executable as 60-byte descriptors. This page is about the park advisor's message and scoring system; the on-screen advisor model and his interruption are in `ui.md`.

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

Concrete message-to-response pairs seen in the dispatch: message `8` → response `0x102`; message `0x0e` sub 0/1/2 → `0x7d`/`0x7e`/`0x7f`; message `0x11` (research) categories 0-4 → `0x5d`, `0x5f`, `0x60`, `0x61`, `0x5e`.

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
word **0** (global speech bank), face fields **9/0**. Their trailing values **150/151** are not the message
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

Private Ghidra project `q90-codex-project`, copied and hash-checked from the original without altering it;
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
`FUN_0059ae20` sets every rotation index to −1, so each message then selects line 0, then 1, then 0.
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
| 1 | A global flag `DAT_0078d90d` gating a class of messages | `FUN_0059c610` |
| 2 | A per-message cooldown: elapsed vs `MessageGroups[category].MinTimeSameMessage` from `Advisor.sam`, tested only when `FUN_0041a980` is non-zero | `FUN_0059c390` / `FUN_0041a980` / `FUN_0041a990` |
| 3 | When NOT forced: "say once" already said (counter `+0xe8`), and a cap on how often a withdraw has cut him off saying it (counter `+0xec` against the category's `DiscardAfterSlaps`) | `FUN_0059c410`, `FUN_0059c570` |
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
| `+0x14` | — | Face node A | `0x005990fa` |
| `+0x18` | — | Face node B | `0x00599106` |
| `+0x1c` | — | Grouping values — **see the refuted section below**; no instruction reads it through this table's base | xrefs |

### Response metadata at `0x0076e300` — filled before `WinMain`

Stride `0x38`, validated by `row+0x04 == id`, with a linear-scan fallback bounded at `0x772fcc` (351 rows). Ids `0x15f` and `0x160` are excluded outright. **The table reads as zeros in the image; its filler is straight-line code at `0x005a0a50`** (`MOV` and `XOR` between four `PUSH`es and `POP`s, ending in the `RET` at `0x005a86fb`; in no Ghidra function), a C++ static initializer the C runtime runs before `WinMain` through the thunk `0x005a0a40` listed at `.data` `0x0072bda8` (Q177's analyser decode; the complete emulated table is `~/.cache/tpw-harnesses/q177/skeptic-analyser/meta_full.json`, made by `emu2.py` there). Row 91, for one: polled, id `0x5b`, category 0, mode 2, score function `0x0059e730`, one instance, first response 244, two lines, selection mode 2.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0076e300` | — | Metadata table, stride `0x38`, filled by the static initializer at `0x005a0a50` | Zeros in the image |
| `+0x00` | — | Nought: the row is polled every 351-row cycle; otherwise posted only | `FUN_0059c680` |
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
`0x006fd958`; built at `0x00415e70`), its member calls made with `ECX = 0x00fb3540`, and `FUN_00401030`'s rule puts
`MinTimeAnyMessage` at `0x00fb3558` and `MinTimeSameMessage` at `0x00fb355c`. No operand of the program falls in
`0x00fb3554`..`0x00fb355f` and no byte search finds either address, where the same search finds `0x00fb3560` at exactly
its three reads; none of the 15 member functions nor any of the 201 score functions (thiscall on `0x00fb3540`, every
row's adjustment `+0x14` 0; 433 this-relative accesses) touches `+0x18` or `+0x1c`. So the only pacing is the tick's
wait ("The tick", above) and each category's own `MinTimeSameMessage` in the gate; **there is no
global gap between lines.** `MessageGroups` is set for groups 0-5 and 9 only; 6-8 keep the loader's zeros. "Slaps" is the player slapping him. Field names across all messages are dominated by `Score` (160 uses) and `ScorePerPoint`, then per-message conditions: `ValueBetterThan`, `SatisfactionWorseThan`, `FilthierThan`, `ScorePerUnpatrolledPct`, `DontBotherIfPatrolledUp`, `MonthsIntoGameForConsideration`.

The golden-ticket keys are score coefficients, not lines: `GoldTicketNearToFirstOne`, `NearToXPeeps`,
`NearToXHappiness` and `NearToXProfit` (multipliers 0.65, 0.9, 0.85, 0.9) with `GeneralWonGoldenTicket`,
`GoldTicketExplanation`, `WonFirstGoldenTicket` and `GoldTicketCluesForRemainder`; no other shipped `.sam` has them.

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
awards moved here.

## Every HUD screen posts a message

The ids seen so far: `0xbc` when buy opens, `0xbd` when hire opens, `0xc6` and `0x11f` in the staff/visitor locator, and `0x131`, which the camcorder/postcard sub-panel **withdraws**. So the advisor comments on the screen you just opened.

## Still unknown

| What | State |
|---|---|
| The loader that fills `0x0076e300` | **Found** and emulated: the static initializer at `0x005a0a50` (above); the 40 screen-posted ids are posted-only rows of one line each |
| The u16 at message `+0x00` | Unknown — it is written by `FUN_0059c0a0` but is not the gating id |
| `Welcome` (section 2) | Looks like a **stub**: group 2 holds four rows, all `sample=1, anim=16`, and sample 1 is an *OpenPark* line. The park welcome probably has no audio of its own |
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
Saved advisor histories remain unbuilt and loads count `PARK_ADVISOR_SAVED_HISTORY`. Other posted park
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

Not confirmed on screen: saved advisor histories, full-slot eviction, the exact cooldown boundary,
rotation back to line 0, and zero playback-return consumption. Those applicable to the new path are
regression-tested; the actual busy/disabled speaker guards are code-reviewed only, and saved histories
remain counted. The first score-26 capture was partial because
its harness deadline was too short for the cooldown step; the completed final run replaces that proof.
