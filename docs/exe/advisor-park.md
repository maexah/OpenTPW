# Park advisor (original executable)

The park advisor is a THING like the weather, ticking on the same beat, but it does not poll the world: the park **posts a message** to it, the message is accepted or rejected by a gate, and an accepted message occupies one of **eight priority slots**. Each tick the advisor says the highest-scoring slot, picks a line within that response, and plays a sample. Two separate tables sit behind this — a static response table at `0x00768fb8` that maps a response id to a sample, lip file and gesture, and a runtime-filled metadata table at `0x0076e300` that holds the per-message score function, category and line count. The scores' coefficients live in `data/Advisor/Advisor.sam`, whose schema is compiled into the executable as 60-byte descriptors. This page is about the park advisor's message and scoring system; the on-screen advisor model and his interruption are in `ui.md`.

## The thing

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_0050b360` | — | THING dispatcher; advisor is type `0x0b`, weather is `0x0f` | Same dispatcher, same construction pattern as `ParkWeather` (see `weather.md`) |
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

The sample is `FUN_0059c240(response) + index`, so a response's lines are a **contiguous run of sample ids**. Sample `0x266` (614) means "say nothing".

## The accept gate

`FUN_0059a940` calls `FUN_0059abc0( advisor, id, forced )` before taking a slot. **The gate does not consult world state.** It rejects for exactly five reasons, none of them about staff, visitors, rides, research or money:

| # | Reason | Where |
|---|---|---|
| 1 | A global flag `DAT_0078d90d` gating a class of messages | `FUN_0059c610` |
| 2 | A per-message cooldown: elapsed vs a compiled threshold, tested only when `FUN_0041a980` is non-zero | `FUN_0059c390` / `FUN_0041a980` / `FUN_0041a990` |
| 3 | When NOT forced: "say once" already said (counter `+0xe8`), and a repeat limit (counter `+0xec`) | `FUN_0059c410`, `FUN_0059c570` |
| 4 | A cap on how many copies of one id may sit in the eight slots | `FUN_0059c320` |
| 5 | A rotation check against counter `+0xe4`, and mode 0 running out of lines | `FUN_0059c490` / `FUN_0059c2b0` |

Every test reads either a compiled per-message property or a per-message counter on the advisor. **So a screen-open line needs only the advisor, its tables and a HUD — nothing from the simulation.**

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

`+0xe0` holds the time he last said this message, so `FUN_0041a980` non-zero means "he has said it before" and the cooldown only applies after a first airing. The `>> 2` on both sides strips **two flag bits** from a packed field; other code confirms this with `TEST byte ptr [.. + 0x1da70c], 0x3`. The running counter is `DAT_0080239c + 0x1da70c`, in the same large global structure as `FUN_005194d0`'s `+0x1da71c`. `DAT_0080239c` is zero in the image and filled at runtime.

### The per-message record: `advisor + id * 0x10`

The disassembly computes `advisor + id*0x10 + 0xe0` outright (`LEA EAX,[EDI + 0xe]; SHL EAX,0x4; LEA ESI,[EAX + EBP*1]`), so the record starts at `+0xe0`.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `+0xe0` | — | When he last said it (packed; `>> 2` for the time) | Receiver of `FUN_0041a980` / `FUN_0041a990` |
| `+0xe4` | — | Rotation counter (gate test 5) | `FUN_0059c2b0` |
| `+0xe8` | — | Said-once flag (gate test 3) | `FUN_0059c410` |
| `+0xec` | — | Times said (gate test 3) | `FUN_0059c570` |

## The two tables — do not confuse them

### `AdvisorResponseTable` at `0x00768fb8` — static and walkable

610 rows, stride `0x20`, terminated by **9999** (walked to its terminator). Ghidra already names the symbol. `Advisor_SayResponse` walks it linearly and matches the row whose first dword is the response id — the table is **not** sorted by id.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00768fb8` | `AdvisorResponseTable` | 610 rows, stride `0x20`, `9999` terminator | Walked to the terminator; Ghidra symbol |
| `+0x00` | — | Response id | Matched linearly by `Advisor_SayResponse` |
| `+0x04` | — | **Sample id** — what `Sound_PlayEffect` is handed | |
| `+0x08` | — | Lip file number → `\Speech\lips\sp_%03d.lip` | Format string in the exe |
| `+0x0c` | — | Gesture / anim sequence, `-1` for none | |
| `+0x1c` | — | A message index — **see the refuted section below** | |

### Response metadata at `0x0076e300` — runtime-filled

Stride `0x38`, validated by `row+0x04 == id`, with a linear-scan fallback bounded at `0x772fcc` (351 rows). Ids `0x15f` and `0x160` are excluded outright. **The table reads as zeros in the image and Ghidra shows no static writer** — presumably a computed pointer. That is also where the name-to-response-id mapping must be resolved.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0076e300` | — | Metadata table, stride `0x38`, runtime-filled | Zeros in the image; no writer found |
| `0x772fcc` | — | Upper bound of the linear-scan fallback (351 rows) | |
| `+0x04` | — | Id (used to validate a row) | |
| `+0x08` | — | Category | |
| `+0x0c` | — | Game-mode filter: `2` always, `0` game type 0, `1` game type 2 | |
| `+0x10` | — | **Function pointer** that computes the score | Called via `FUN_0059c0a0` and `FUN_0059bf20`, subject to the `+0x0c` filter |
| `+0x20` | — | Max instances | |
| `+0x24` | — | First sample | |
| `+0x28` | — | Line count | |
| `+0x2c` | — | Selection mode (see line selection above) | |

**The score is code; the coefficients are data.** Because `+0x10` is a called function pointer, the triggers are engine behaviour and cannot be driven purely from a file.

## The content: `data/Advisor/Advisor.sam`

19,863 bytes, ASCII, **382 keyed lines, 205 sections**, commented by the original developers ("was 30 - annoying"). It needs no new format work — `.sam` is already read everywhere, and subscripted keys were proven by the weather work.

    GeneralAdvisor.MinTimeAnyMessage         5
    GeneralAdvisor.MinTimeSameMessage        120
    GeneralAdvisor.MinScoreForConsideration  25     <- this IS DAT_00fb3560
    MessageGroups[0..9].MinTimeSameMessage / SayOnlyOnce / DiscardAfterSlaps

`MinScoreForConsideration` being a file key is why `DAT_00fb3560` has three readers and no writer. "Slaps" is the player slapping him. Field names across all messages are dominated by `Score` (160 uses) and `ScorePerPoint`, then per-message conditions: `ValueBetterThan`, `SatisfactionWorseThan`, `FilthierThan`, `ScorePerUnpatrolledPct`, `DontBotherIfPatrolledUp`, `MonthsIntoGameForConsideration`.

### The schema is compiled into the exe

60-byte descriptors — kind at `+0`, name at `+4` in a 44-byte field, array count at `+48` (`MessageGroups` reads 10) — the same pattern the weather schema uses.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0072e210` | — | Table of 60-byte `.sam` schema descriptors, 205 sections, ordinals 0-204 | Walked: `GeneralAdvisor` is 0, `WaitingTimes` is 204 |
| `0x0073964c` | — | The `WaitingTimes` descriptor, the last one; anything past here is garbage | End of the walk |
| kind `1` | — | Section | |
| kind `3` | — | Array section | |
| kind `4` | — | Score field | |
| kind `5` | — | Time / int | |
| kind `6` | — | Threshold | |

205 descriptors matches the file's 205 distinct prefixes exactly — two independent sources agreeing.

**The file's order is NOT the exe's order.** `ClosePark` is ordinal 4 in the exe but sits at line 575 of the file, far from its neighbours at lines 47-57. Nothing is missing from either side. **Ordinals must come from the exe, never from file order.**

## Verified by transcript

Speech transcripts already exist — grep `/home/alex/ghidra/notes/global-speech-transcripts.tsv` before transcribing anything. Each row below is also corroborated by the message's own `.sam` field names.

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

### A screen's line cannot be recovered statically

`FUN_00486b00( id )` makes an advisor message with this id and posts it; `FUN_00486b40( id )` withdraws it. Both first call `FUN_005194d0` to fetch the advisor THING.

**That id is a *message* id, not a response id.** `FUN_00486b00` does `MOV EAX,[ESP+0x30]; LEA ECX,[ESP+0x14]; PUSH EAX; CALL 0x0059b590`, and `FUN_0059b590` writes that argument to `*(undefined4 *)(param_1 + 6)` on an `undefined2 *` — byte offset `0x0c`, the subject, the very field the accept gate keys its per-message counters on. It is **not** the response id that indexes `0x00768fb8`.

Checked against the data: the map screen (`FUN_005f0b40`) pushes `0x130` = 304, and response 304's sample is 338, "there's a litterbug running amok" — plainly not a map line. Message ids resolve through the metadata table at `0x0076e300`, which is filled at runtime and reads as zeros in the image, with no static writer. **So do not try to reproduce screen-posted lines; pick lines by transcript instead.** The 40 call sites' constants were extracted anyway and sit in a contiguous `0xbc`-`0xd5` / `0x11c`-`0x135` span, if that is ever useful once the loader is found.

### The message-to-sample rule

Refuted. There is no rule mapping a message to its sample that can be applied statically; see both sections above.

## Every HUD screen posts a message

The ids seen so far: `0xbc` when buy opens, `0xbd` when hire opens, `0xc6` and `0x11f` in the staff/visitor locator, and `0x131`, which the camcorder/postcard sub-panel **withdraws**. So the advisor comments on the screen you just opened.

## Still unknown

| What | State |
|---|---|
| The loader that fills `0x0076e300` | **Never found.** Ghidra shows no writer; presumably a computed pointer. The name-to-response-id mapping must be resolved there too |
| The u16 at message `+0x00` | Unknown — it is written by `FUN_0059c0a0` but is not the gating id |
| `Welcome` (section 2) | Looks like a **stub**: group 2 holds four rows, all `sample=1, anim=16`, and sample 1 is an *OpenPark* line. The park welcome probably has no audio of its own |
| Responses 399-402 | All carry sample 1, an `OpenPark` line — the same stub shape |
