# Status

Last updated: 2026-09-21.

**This header names no branch and no sha, deliberately.** A line written inside the commit that moves
the tip cannot name it, so every attempt to do so here went stale the instant it was written — three
times in one day, once. Read the current state from the repository, which cannot lag:

    git log --oneline -1

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves, the island gate
  swinging open as you enter that park, and — with nobody playing — the camera flying itself around
  all four islands with all four heard at once, each from its own island.
- Park: enter from the lobby; ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (5 of 6 buttons).
- Building and staffing: the **purchase menu** and the **hire screen** both open from the gadget's Buy button and reach each other. Things can be bought, sold, moved and carried; staff hired, fired, picked up and put down. **Clicking a placed ride opens its management window**, which cycles between rides, deletes and moves.
- Information and money: the gadget's **Info** and **Money** buttons open real screens — **all staff** (five tabs, live wages and happiness meters), **all items** (four tabs over three different list shapes), **all visitors**, and the **entry price**, whose spinner moves the gate fee and whose `b_door` switch opens and shuts the park.
- **Building by POINTING**, through the original's own shape: there is **no drag** - both drag slots
  of its build mode are bare `RET` stubs - so a run is **click to anchor, click to commit**, with the
  target snapped to the dominant axis and the anchor then advancing to that snapped target, which is
  what lets an L be laid click by click. An armed mode **consumes** the click, so a player mid-run
  cannot open a ride's window by overshooting; disarmed, the same click opens it. Confirmed by console
  in a running park - **not yet confirmed on screen**, see the capture note below.
- **Laying and lifting QUEUE**, at 75 a cell, which **refunds** where a path does not - the original's
  own asymmetry. A queue cell records the object it serves and a flow direction that is the opposite
  of the step taken into it, which is what makes the queue measurable at all. **The queue rewalk is
  hooked up** (`FUN_004de1f0`): deleting a middle cell took the Belly Bounce from **4 cells ending
  2866 to 2 ending 2868** while the save's own cached count sat unchanged at 4, which is the whole
  point of the hook.
- **Laying and lifting PATH**, at 20 a cell from the theme's own `Costs.PathCell`. The cell joins itself to its neighbours by the original's own incremental rule and picks its art from the executable's own tile tables, so a run draws straights, ends, corners, T-junctions and a crossroads as the shape demands. Confirmed in the running game by census **and** by screenshot.
- Spending: guests choose, queue for and **buy from the Drinks Shop and the Jungle Spray**, are charged on leaving, take the item's effects, and a sideshow winner is paid its prize.
- People: 13 guests and 5 staff read from the save, drawn, walking, paying at the gate, queueing, boarding. **A walking peep is drawn between the simulation's 248 ms steps rather than jumping four times a second** — position interpolated per frame, facing snapping, as the original does both.
- Rides: every placed thing runs its script; 74 of 106 opcodes implemented, the rest counted by `Unimplemented`. **A ride screams with a different sample each pass**, at the band its own rider count asks for.

## Does not

- No finances, litter, saving a park back, video, networking. **One** gadget button (Research) is still inert, and it is the one with nothing behind it to build: its screen is six effort sliders over research groups, and this game has no research, no researchers and no groups. The two global income pools, the balloon and costume arms, and the litter-bin errand (guest state 9) are named and unbuilt.
- Eight of the nine per-object windows are unbuilt (only the ride's). Its stats table fills **four of seven** rows — Users last month, Excitement and Reliability are counted gaps. Patrol areas are dead, deferred by Alexah.
- The `meter.wct` mapping behind the happiness gauge is wrong — the last fault Alexah found by playing that is still open.
- 32 opcodes unimplemented. **The stale counts this line used to flag are fixed**: `README.md` said 63 in one place and 57 in another, and `RideScriptFile.cs:99` said 63; all three now say 74, measured the same day. `docs/history/` still carries older figures and is left alone on purpose — history is verbatim.

## Next

`docs/PLAYER-GAPS.md` — the **eight** gaps a player meets, in the order they meet them. **Five are done**
(1, 2, 3, 6, 8); **three remain** (4, 5, 7). Alexah sets which one is the goal; one per session.

**And `docs/CLEANUP-PLAN.md`, which is a second queue and is deliberately untracked** — nine things a
player sees, in Alexah's order 9, 6, 4, then 1, 5, 3, 8, 2, then 7. **Item 9 is DONE and closed**: a
park load went 23,298 ms → 2,488 ms, about 9.4x, with the worst phase now `terrain` at 718 ms.
**Item 6 is DONE and closed too**: opening a park's menu now drops the mix by **34.96 dB** against a
**0.00 dB** floor, where before it moved by −0.01 dB. **And item 4 is DONE and closed**: the lobby's
sea is served out of the texture cache on the way back from a park, and it now comes back carrying the
**AnisotropicWrap** sampler it asked for instead of the default **AnisotropicRepeat** — which mirrors,
and had been drawing the ocean as a diamond lattice. **And items 5 and 3 are DONE and closed too** — Alexah
picked 5 ahead of item 1 on 2026-09-21, then picked **3** ahead of it as well. **Item 1 then closed on
2026-09-22**, so **9, 6, 4, 5, 3 and 1 are closed and 8, 2 and 7 remain**. **No goal is set now.** None
of the six closed items is in `PLAYER-GAPS.md`, and nothing there was ticked by any of them.

**Because that file is untracked it does not exist in a fresh clone.** It lives only on this machine;
if it is lost, the three remaining items go with it, and so does the record of the six that are done.

**Where it stands, 2026-09-21. ITEM 2 IS DONE and is ticked.** All four of its parts landed and every
one was confirmed in a running park: the purchase and hire screens with buy, sell, move, carry, hire,
fire, pick up and put down underneath; **laying and lifting path**; **laying and lifting queue**, with
the `FUN_004de1f0` invalidation hooked up; **building by pointing**, anchor-then-commit with no drag
because both drag slots are bare `RET` stubs; and the **Info and Money categories**, whose four
buildable screens all drew.

**Only Research still does nothing, and that is not deferred work.** It is not a category —
`FUN_004a0840`'s case `0x2b` goes straight to `FUN_004aa480`, as the map does — and its screen is six
effort sliders over research groups. This game has no research, no researchers and no groups, so
there is nothing to put behind the button; it is counted with that reason named at the site.

**No goal is set as of 2026-09-21.** `docs/CLEANUP-PLAN.md` item 5 *was* the goal named here and is now
closed, as is item 3 after it. Of `PLAYER-GAPS.md`'s own three remaining — 4, 5 and 7 — none is picked,
and of the cleanup plan's four remaining — 1, 8, 2, 7 — none is picked either. Its item 5
gained a proper description from Alexah on 2026-09-21: the gauge's **bar renders in the wrong place and
repeats, split down the middle**, which makes it a rendering fault rather than the arithmetic one that
entry had assumed.

## Not verified on screen

- ~~**The ride loop completing.**~~ **CONFIRMED ON SCREEN 2026-09-20** — four runs of the park showed
  guests reaching `Riding`, the Belly Bounce's queue holding 2 of its 16 places, and the whole chain
  landing as money: the sideshow's till reached 900 and the shop's 1110. This bullet said "no run of the
  game was made this session", which outlived the sessions that ran one.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: both are called from `ParkPeople`, and neither is
  pinned by the suite — unwiring either leaves it green. They rest on the decode, not on coverage.
- ~~Staff never enter the cell-occupancy lists (`ParkState.StandOn` is called only from `PeepBehaviour`).~~
  **Stale as written**: `StandOn` has **four** call sites — `PeepBehaviour.cs:779` and `ParkPeople.cs:397`,
  `:490` and `:604` — so staff do reach it on the hire and put-down paths. What remains true is narrower:
  nothing puts a staff member in a cell's list *as they walk*.

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | **74** implemented of 106 | 2026-09-21, `case Opcode.` labels vs enum members — `SINGLESCREAM` and `SCREAMLEVEL` added |
| Tests | **858** total, all of them run **with** the game and 0 skip | 2026-09-22, measured on `alexah/104` — ten added for the saved-state restore |
| Tests without the game | **391** ran, **467 skipped**, of 858 | 2026-09-22, measured fresh rather than computed — all ten new tests read the shipped park, so the ran count is unchanged and every one of them lands in the skip column |
| Build warnings | 125 | 2026-09-21, measured at `3fb2d9c` — one fewer than 126 since the refpack reflection went |
| Park load | **2.5 s**, worst phase `terrain` at 0.72 s | 2026-09-21, three jungle runs, per phase, `LoadTimer` |
| Other themes | fantasy 1.0 s, hallow 1.1 s, space 1.2 s | 2026-09-21, one run each, first time ever timed |

## Recent

**2026-09-22 — a loaded park's scripts resume where the save left them, so nothing builds itself
again, and `docs/CLEANUP-PLAN.md` item 1 is closed.** Branch `alexah/104-built-not-building`.

Every ride, shop and feature script started at its own first instruction. For the Belly Bounce the
**second** one — `Bouncy.RSE` body word **4**, after the `NAME` at word 0 that every one of these
scripts opens with — is `WAITANIM 0 0`, role 0 entry 0, the clip that builds the ride, and `WAITANIM`
starts a clip as well as waiting on it. So `ParkObjects.Sweep` posed the
construction clip every frame and `LobbyModel.Pose` re-applied its visibility tracks over the built
pose: the ride hatched out of its egg on every entry to Lost Kingdom, for five seconds, then settled.
`PoseAsBuilt` was never the fault — it hides the egg correctly and still does.

**The original has no guard against this anywhere.** `FUN_00415270` restores seventeen modules in
order, and two of them end it: `FUN_004647a0` overwrites every animation channel from the saved record,
and `FUN_005597a0` reads each script's whole 244-byte struct back from the file, **program counter
included**. A loaded park therefore resumes mid-flight; the same build path (`FUN_00463060`) that plays
role 0 for a thing the *player* builds is simply overwritten on load. Built here: a reader for the
save's `RSSE` module restoring each script's counter, variables and declared name.

**Both halves are restored, and taking only the first one was a worse bug than the one being fixed.**
Restoring the counter alone left every thing whose steady-state loop holds no animation instruction
unable ever to reach the `LOOPANIM` in its prologue: **ten of the fourteen placed things stood frozen
for the whole session**. An adversarial review caught it; the whole-park census then measured it.

| `rides` census, whole park | control | script only | **both halves** |
|---|---|---|---|
| 13 Belly Bounce | role **0**, 150-frame clip | role 2 | **role 2 from the first sample** |
| 24 Fountain Feature | role 5 | **idle for ever** | **role 5** |
| 21–23 Small Toilets | role 5 HELD | **idle for ever** | **role 5 HELD** |
| 14 Jungle Spray, 3 lanes | role 2 HELD ×3 | **idle ×3** | **role 2 HELD ×3** |
| 12 Traffic Lights | role 5, cycling | **idle for ever** | **role 5, cycling** |
| 11 Gates, 16 Coconut, 17 Litter Bin | animating | **idle for ever** | **animating** |
| 20 Staff Room | role 0 HELD | idle | **idle — what its record saves** |
| screenshot at 2.0 s | a speckled egg, no dinosaur | — | the built ride, fence and name board |

The construction replay is gone and nothing is frozen. The only role 0 left anywhere is the Litter
Bin's, which is exactly what its own record stores. The Staff Room ends up *more* faithful than the
control: the save says idle, and the control's role 0 HELD was itself the accidental prologue.

**Two adversarial reviews, and each found something the suite and the game both missed.** The first
found the regression above. The second found that a saved channel's flag word is the engine's own
**internal** field and was being handed in as a **caller** flag: the two collide on `0x4`, which
internally marks a channel held on its last frame and to a caller asks to keep the pose, and the
channel's `Start` clears `0x6` on the way in — so the hold was dropped for **eleven of the fifteen**
restored channels. It read correctly only because starting each channel at time nought backdates it
into the held state anyway: right by accident, resting on a second oddity, and it would have come back
the moment the clock was corrected — including the Litter Bin, which is saved on role 0. Now
translated properly, with `0x1` and `0x8` carried across and held and frozen re-entered through roles
14 and 13 the way the engine does it. The same review found the channel half had **no test at all**,
and that any reader failure degraded silently back to the frozen park; both are covered now. The counter is not guessed: the struct's length field
equals the following body block's word count for all fourteen scripts, which pins the alignment; every
one of the fourteen counters then lands on an exact instruction boundary, and on a `BRANCH`,
`BRANCH_Z`, `TEST` or `WAIT`. Bouncy's 46 holds a `WAIT 500` and is branched to from words
**36 and 41** — 33 and 38 are the `LOOPANIM`s those branches follow. The fact lives once, in
`docs/exe/ride-operation.md`; this line said 33 and 38 until the two pages were made to agree. The saved variables agree with the **object records** — a separate part of the file —
on capacity for all six things that declare one. **The mutation was run**: disabling the single call
site turned the suite red on `Expected:<46>. Actual:<0>`, while the nine new format tests stayed
green, which is what says they cover the reader rather than the wiring.

Byte layout went to the FileFormats clone's `saves.md`; the executable decode to
`docs/exe/ride-operation.md`. **Three** instrument rules came out of it — `VERIFYING.md` **104**, **105**
and **106** — and 106 is the one this branch paid for: a before/after on the thing you changed cannot
see something else going missing.

**2026-09-21 — a guest is drawn between the simulation's steps instead of jumping, and
`docs/CLEANUP-PLAN.md` item 3 is closed.** Branch `alexah/103-guests-walk-smoothly`.

The walk turns once every eight ticks — 248 ms, about four times a second — and the drawing took that
position and held it until the next one. The original does not: `FUN_004f9f00` is
`prev + (cur - prev) * t` per axis, driven per **frame** by `FUN_00518f90` at `0x0054fa85`, which sits
past the 31 ms catch-up loop's back edge. `t` is time since the last thing sweep over **248 ms**
(`0x0054fa5c`, times `[0x00700f94]` = 1/248.000007, baseline re-stamped inside the every-8th gate at
`0x0054f683`), clamped to [0, 1].

| the same guest, one line of difference | control | after |
|---|---|---|
| distinct drawn positions **within one 248 ms tick** | median **2** | median **30** |
| `alpha` distinct values | **1** (constant) | **101** (0.000 → 1.000) |
| tick period from alpha wraps | — | **247 ms** |
| navigator `peeps at`, first twelve | identical | identical |

**The navigator matched wherever the two runs were compared, which is the two-sided control**: the
first twelve positions identical to three decimals, 48 against 47 distinct cells over 111 samples — not
byte-identical, which two separate launches of a live park cannot be. The interpolation is
entirely in the drawing, so smoothing the *simulation* — the wrong fix — fails exactly there. And the
whole-run distinct count (48 against 1498) is the figure that would have lied, since a guest moves four
times a second on either build; only positions *within* a tick separate a slide from a jump.

**X and Z only, and the facing deliberately snaps.** Height is forced to nought at `0x004f9ffd` and the
octant is copied raw from `[ESI+0x1c]` at `0x004fa015` while the positions either side of it are blended,
so the original's own guest glides and turns in eight discrete steps. Both are reproduced.

**Two of the item's own premises were wrong.** `mLastPosX`/`mLastPosY` (save 430/434) are **not** the
previous position — they are a trailing sprite's last placement, and OpenTPW neither reads nor stores
them; the real pair is `mPreviousX`/`mPreviousY` at `+0x190`/`+0x194`. And the period is 248 ms, not the
31 ms tick.

**Where the stamp lives is load-bearing.** `FUN_004fa870` is the first call of *every* person kind's tick
handler, ahead of the guest handler's own `(id & 3)` stagger — so it runs whatever state a peep is in.
Stamping inside the walk instead would leave a guest who had **stopped** holding two positions for ever
and swing them between the two on every frame. There is a test for it, and **deleting that wiring still
passes all 848**, because nothing in the suite drives a stopped guest through a running park — said at
the test, per rule 48. Six mutations, both survivals called in advance; every restore md5-verified.

**Two instrument faults of mine, both now rules.** Rule **102**: Ghidra's `run_python` reported **no
memory block** at `0x00700f94` from all three of its readers, while the `read_memory` tool and the file
on disk both read it fine — `list_segments` prints PE section headers, not Ghidra blocks. I was one step
from recording "the probes fabricated these constants". Rule **103**: the first harness segmented thing
ticks by watching the interpolation fraction **wrap**, which cannot work on a control whose fraction
never moves — the run collapses to one segment and the broken build scores twenty times better than the
fixed one. **The verdict would have inverted.**

**2026-09-21 — a ride screams with a different sample each pass, and `docs/CLEANUP-PLAN.md` item 5 is
closed.** Branch `alexah/101-screams-vary-and-single-scream`. One harness on two builds differing in
**one line**, `save/` unchanged within both runs:

| the Belly Bounce, by the new `rides` census | control (one clip looped) | after (replayed) |
|---|---|---|
| effect 71, passes within one held scream | **1** | **8** |
| effect 72, passes within one held scream | **1** | **6** |
| distinct scream samples heard, whole park | 7 | **22** |

**`plays` is the statistic, and `distinct` would have lied.** The control still reaches 7 distinct
samples, because the ride starts and stops screaming about seven times in four minutes and each *start*
picks a fresh clip — so "distinct went up" was satisfied by the broken build too. What separates them is
passes *within one held scream*: 1 against 8. Predicted before the run.

**Looping is not a parameter of the original's play call at all.** `Sound_PlayEffect( handle, category,
effect, x, y, z )` takes no such argument and `STARTSCREAM` and `SINGLESCREAM` make the *identical*
call — only the kept handle differs — so whether effect `0x47` repeats is decided below it, in
`QMixer.dll`, which is not in the Ghidra project. **The shipped data settles it**: the four scream
effects declare **4 variations over 25, 50, 60 and 59 samples behind a 2700 ms repeat delay**, with
samples averaging ~800 ms. A repeat delay is meaningless for a seamless loop, and a loop leaves 24 of
effect 71's 25 samples unreachable. It is the same shape as a park's music, which this file already
records as replayed rather than looped.

**The variety is the SCRIPT's, not the engine's**, and that is the decode that reframed the item:
`Bouncy.RSE` calls a subroutine on every pass of its ride loop that re-issues `STOPSCREAM` +
`STARTSCREAM` whenever the rider count changes band. Both effects 71 and 72 appear in one run, so the
count really did cross a band edge — the decode seen happening.

**Four corpus counts in `docs/exe/ride-operation.md` were wrong and are corrected.** Measured over all
306 wads and all 308 scripts: `STARTSCREAM` **40**, `STOPSCREAM` **80**, `SINGLESCREAM` **46**,
`SCREAMLEVEL` **81 uses in 36 scripts** — against the 7 / 7 / 9 / 6 that stood there. They were
**jungle-only**, and short even for jungle, because **`Monkey.rse` has a lower-case extension** that a
case-sensitive `.RSE` match drops silently.

**Two clauses of the item's own "Confirm" could not be met as written.** "Show the level moving" is
`SCREAMLEVEL`, and **no placed ride in Lost Kingdom calls it** — Bouncy has none — so the level cannot
move in the shipped park; it is built because it is the third-most-used member of the family
corpus-wide. And counting distinct samples *from the mix* would mean correlating against 25 candidate
clips, so the game reports the count itself instead, which rule 89 prefers anyway.

**Mutation-checked, including one expected to survive** (rule 48): `0x6c → 0x6b` fails exactly the
negative-branch test; the grid base `0x4f → 0x4e` fails two, the second because `0x4f` then becomes
reachable as a first-column id. **Removing the replay itself passes all 842**, said in advance — the
pump needs an audio device and a loaded category, which a test run has neither of, so it rests on the
capture, as items 4 and 6 did.

**Two instrument faults of mine, both now rules.** Rule **100**: the first mix comparison took its
threshold from each run's own 35th percentile, which put the two baselines **39 dB apart** and produced
three confident numbers measuring different things. Against *fixed* thresholds the same captures
answered cleanly — 93.3% against 52.4% sounding above −60 dBFS, and a 10th percentile of −55.1 dBFS
against **−180**, true digital silence. Rule **101**: a `trap … EXIT` restore with a *relative* path
died once the script had `cd`'d away and printed `restored:` with an empty md5 — a failure that reads
as success, which left the mutation on disk for the next build.

**2026-09-21 — the lobby's sea comes back from a park drawn right, and `docs/CLEANUP-PLAN.md` item 4
is closed.** Branch `alexah/100-cache-hit-keeps-its-sampler`.

`Water.Spawn` asks for `TextureFlags.Wrap`. Coming back from a park that request is served out of the
texture cache — and a hit returned **before** the sampler, the size and the path were ever assigned, so
the sea was drawn with the field's own default `AnisotropicRepeat`, which is `SamplerAddressMode.Mirror`.
Every other tile was flipped in both axes, each wave line met its own reflection at the seam, and the
ocean came back as a **diamond lattice**.

| the sea, by the new `water` census | first lobby | back from a park, before | back from a park, after |
|---|---|---|---|
| `sampler` | AnisotropicWrap | **AnisotropicRepeat** | **AnisotropicWrap** |
| `size` | 128x128 | **0x0** | **128x128** |
| `path` | `…/jri_lak3.wct` | **empty** | `…/jri_lak3.wct` |

One harness on both builds, the camera pinned to `cam=452,353,32` in all four shots, `save/` unchanged
within both runs. The island, the jetty, the Dino and the sky are alike either side — they ask for
`Repeat`, which already **is** the default — and that is the two-sided control.

**A hit is now refused outright when its flags differ from the request**, because the flags decide the
pixels as well as the sampler — `PinkChromaKey` rewrites them in place. **It cost nothing**:
`texture=542` and `distinct=535` are identical before and after, so nothing in the game asks for one
path under two sets of flags. Predicted before the run, then measured.

**The scalar could not see it and the picture could not miss it.** Same crop, mean 55.69 → 55.62 and
variance 221.83 → 222.79 — under half a percent, because mirroring changes *where* texels land and not
*which* they are. A verdict resting on that number would have reported "no change" about a defect
covering half the screen. `docs/VERIFYING.md` rule 99.

**One instrument fault of mine cost a run.** The first pass photographed two quite different viewpoints
while the console answered `island 0 'Lost Kingdom'` both times: with nobody playing the lobby camera is
in **attract** mode, so `island`, `orbit` and `settle` are read only by an orbit branch that `Update`
returns before reaching — and `ForgetIsland` reseeds the wander on every lobby build, so two lobbies in
one run can never stand in the same place. A debug-only `attract off` forces the orbit branch.
`lobbyshot.py`'s own recipe had gone stale the same way, without anyone touching it. Rule 98.

**Mutation-checked, including one expected to survive** (rule 48): breaking `SamplerFor(Wrap)` fails
**exactly** the three tests named for it; **removing the fix itself passes all 839**, because every road
to a cached texture runs through `CreateTexture` and a test run has no graphics device. That is written
at the test — the wiring rests on the capture, as item 6's audio wiring does.

**A decode fact that changes none of it.** Of all **104** calls through the device vtable's `+0xa0`
(`SetTextureStageState`), the only **two** writing `D3DTSS_ADDRESS` both write **`D3DTADDRESS_CLAMP`**,
and nothing writes `ADDRESSU` or `ADDRESSV` — the original never wraps anything. It must **not** be
ported onto OpenTPW's sea, which is a 10,000-unit plane whose UVs the shader computes from position:
clamping would stretch one texel over the whole ocean, where the original's sea is a mesh with authored
UVs. `docs/exe/render-states.md`, and it becomes live when the sea is re-sourced from that mesh.

**2026-09-21 — a park's menu silences what a park's menu should, and `docs/CLEANUP-PLAN.md` item 6 is
closed.** Branch `alexah/99-pause-holds-sound`. Open the menu mid-ride and the mix drops **34.96 dB**;
close it and the same scream carries on from where it was.

| | music only | floor | A shut | B **MENU OPEN** | C shut | A → B |
|---|---|---|---|---|---|---|
| before | −38.3 | 0.04 dB | −24.1 | −24.1 | −23.9 | **−0.01 dB** |
| after | −56.3 | **0.00 dB** | −25.2 | **−60.1** | −25.2 | **+34.96 dB** |

Same harness both runs, with the game clock **proven held** in each (`ticks 2840→2840` and
`1975→1975`, +0 in 1.2 s). `save/` unchanged within both.

**The item said to do it through the listener, and that could not have worked.** Its build line reads
"hold every voice that has a position … through the listener, so it needs no per-voice bookkeeping".
The rule is right; the route is inert here, twice over. A park never sets `Audio.ReferenceDistance` —
the only two writes in the tree are both in `LobbyAudio`, and one of them zeroes it on unload — so
`AudioListener.AttenuationTo` returns 1 before it measures anything; and `Voice.Locate` returns
immediately for a voice with no position, which is three of the five kinds a park can have sounding.
A ported listener lift would have silenced **nothing** and read exactly like a broken harness. It is
built on `Voice.Pause` instead, which already existed with a 10 ms fade and has been carrying the
advisor for weeks. **The walk lives in `Audio`, not `ParkAudio`** — because `ParkAudio` never keeps
thunder's voice handle at all, and an engine-side walk also catches ride sounds when they arrive.

**Three of this item's own premises were wrong, and the decode is what corrected them.**
- *"Music is not placed, so it carries on"* — **false**. `FUN_0051e730` plays the park's music through
  `Sound_PlayEffect(…, 2, 0, 0, 0)`, at the origin. Nothing in the original is unplaced. The
  conclusion survives; the reason does not. What is safe to assert is structural: the pause acts
  through the **listener**, and a listener cannot reach a sound never placed against it.
- *"Every placed sound attenuates to nothing"* — **not established, in either direction**, and it is
  not written anywhere any more. `SetDistanceMapping` has exactly one call site (`0x006c581b`), gated
  on a request bit with **no writer anywhere in the image**, so QMixer's default mapping probably
  governs and that lives in `QMixer.dll`. OpenTPW holds to **silence** — a choice standing in for a
  curve nobody has measured, said at the site.
- *"(screams, rain, thunder, the fountain)"* — wrong on **two of four**. Rain is played flat, and
  there is no fountain voice of any kind. **Rain is left sounding**, which all three of the project's
  own criteria agree on: the rule as written selects on having a position, the original's mechanism
  cannot reach a flat sound, and the shipped data marks rain like the music. One line to flip.

**`FUN_0051c1d0` is the per-frame listener update** — three call sites in `Game_StateMachine` — and a
pause replaces its **height** with 10000.0. `docs/exe/audio.md` had recorded that search as stopped
and not found; it is answered, along with the QMixer wrapper vtable at `0x00711920`. Two pages were
**wrong** rather than merely stale: `park-engine.md`'s "every call site passes (0,0)" (the window
proc passes `(1,1)`, so the voice-pausing path **is** taken offline — on alt-tab), and `scenes.md`'s
"the camera as the listener" (a park has **two** listener sites, and the other is a midpoint).
`AudioListener`'s own remarks asserted the refuted `MINRADIUS`→`SetDistanceMapping` binding **in
code**, and that is corrected too.

**Two instrument faults, both mine, both caught before they became findings.** The first baseline's
floor read *digital silence*, which looks exactly like a device that never opened — it is not: a park's
music is **replayed, not looped**, so the mix is genuinely silent for over half of every cycle and a
3 s window fell in a gap; and with no floor my own verdict code called +0.70 dB a DROP. And
`pkill -f "OpenTPW.dll"` **kills the shell running it**, because `-f` matches that shell's own command
line — it killed the mutation check at its first line. Match on `pgrep -x dotnet` instead.

**Mutation-checked, including one expected to survive** (rule 48): inverting `Voice.IsPlaced` fails
**3 of 4** new tests; **deleting the wiring passes all four**, because `Audio.HoldPlaced` and
`Audio.Play` both need an audio device that a test run has none of. That is written at the test rather
than papered over — the wiring rests on the capture, which is what the item always said it would.

**2026-09-21 - a park loads in 2.5 s where it took 23.3 s, and `docs/CLEANUP-PLAN.md` item 9 is
closed.** Branch `alexah/98-decode-each-texture-once`, stacked on `alexah/97-load-time`. About **9.4x**.

**The cause was found by timing inside `ParkTerrain`, and it killed two theories on the way.** The
16,465 ms split into **read 9 ms, textures 16,390 ms, buildmodels 60 ms** - so 99.5% was the texture
slot loop, and the idea that building 812 materials and their resource sets was expensive died at
60 ms. Counting gave the rest: `base.MD2` makes **1,047 texture loads of 57 distinct**, 18.4:1.

**`Texture`'s path constructor decoded the file and then asked the cache.** `CreateTexture` consults
the cache at the very end of the constructor - by which point the .wct has been decompressed and put
through the whole wavelet decode, and the answer is thrown away for the copy already in memory. So
the cache saved the GPU upload and none of the work. **Asking first is behaviour-identical**: the set
of hits does not change, a hit already returned before sampler, size and path were assigned, and
`PreprocessTextureData` and `IsGraded` only touch the local pixel array. Predicted **892 ms** from
16,383 × 57/1047 before running it; measured **683 ms**.

| phase | 2026-09-21 first measure | after the three fixes | now |
|---|---|---|---|
| **terrain** | 19,627 / 20,333 ms | 16,335 / 16,348 ms | **718 / 736 / 720 ms** |
| objects | 1,428 / 1,547 ms | 1,147 / 1,150 ms | **656 ms** |
| **total** | **22,888 / 23,709 ms** | **18,819 / 18,823 ms** | **2,488 / 2,520 / 2,502 ms** |

**All four themes were loaded and timed, which had never been done** - and it closes the "only jungle"
gap the previous entry left open: **fantasy 1,016 ms**, **hallow 1,124 ms**, **space 1,229 ms**. The
lobby gained too (Jun_isle 412 → 259 ms).

**Also removed:** a `.ToList()` in `TryGetCachedTexture` that built a list of every texture in the
game on every lookup. It stays a scan rather than a dictionary on purpose - `Delete` removes from
`Asset.All`, which is the list being scanned, so the cache self-invalidates and a dictionary would
need invalidating by hand for no measurable gain.

**`TextureDecodeTests` is new, and it is regression cover rather than proof.** The buffer resize in
`8c74ead` changed how much scratch the decode allocates and nothing anywhere pinned it; this decodes
every texture `base.MD2` names and checks each against its own header. What proves the work right is
the park on the screen in all four themes, with `save/` unchanged within every run.

**Its mutation check turned up a fact worth keeping.** The mutation the test was written for -
dropping the row buffer's extra `size*size/2` - **survived**, and that is correct rather than a hole:
the headroom is indexed only when a texture is alpha **and** half-scale, and **of the 656 `.wct` files
across the four themes' terrain and shared directories, 0 are half-scale** (211 carry alpha, all
full-scale). That path is **dead by CONTENT**, so nothing tested against shipped art can pin it; it
stays because it is right for a texture the decoder supports and the game never ships. Two mutations
on paths the art does reach - either buffer at `size*size/2` - both fail, so the test is not hollow.
It is recorded at the test, per `docs/VERIFYING.md` rule 48.

**A first attempt at that test was hollow for a duller reason**, and it is the kind that repeats: it
selected textures with `FileExists`, which **does not look inside archives**, so it found 0 of them
and passed its own filter vacuously. `LobbyModel.LoadTexture`'s doc comment says exactly this and was
read earlier the same session. Use `GetSize` for anything inside a `.wad`.

**(superseded the same day — see the entry above, which took it to 2.5 s and closed the item. Kept for
its method and its measurements, not as a statement of where things stand.)**
**2026-09-21 - a park load is measured phase by phase, and costs 18.8 s where it cost 23.3 s.**
`docs/CLEANUP-PLAN.md` item 9, part done at that point. Branch
`alexah/97-load-time`, four commits, local and unpushed.

**The measurement came first, and it overturned the item's own prediction.** The log already stamped
every line, but only to the **second** - and with a U+202F narrow no-break space before the AM/PM,
which defeats a naive parse. Mining all **141** park loads on this machine with that stamp put the
stall between the queue message and the object message (median **22 s** of a **23 s** load) and could
go no finer, because `base.MD2` logs nothing while it loads. `LoadTimer` then reported milliseconds
per phase:

| | before (2 runs) | after (2 runs) |
|---|---|---|
| **terrain** | 19,627 / 20,333 ms | **16,335 / 16,348 ms** |
| objects | 1,428 / 1,547 ms | 1,147 / 1,150 ms |
| staff pool | 187 / 191 ms | **5 / 4 ms** |
| **total** | **22,888 / 23,709 ms** | **18,819 / 18,823 ms** |

Run-to-run spread on the untouched build was **821 ms**, taken as a before/before pair before any
before/after was quoted (`docs/VERIFYING.md` rule 90, which is about pixels and applies just as well
to clocks).

**`ParkTerrain` is 86% of the load, and the plan's guess of "ParkObjects took 9,400 ms" was wrong by
an order of magnitude** - objects costs 1.4 s. The plan's third suspect is refuted for this item too:
`ParkStaffPool` ran **187 ms**, so the BFMU string cost is a 40x win worth 0.18 s, not a load-time fix.

**Three fixes landed, and they are Alexah's own**, supplied as a patch and applied with `git am` so
their authorship and messages survive: the texture decode buffers sized from the indices used rather
than the cube of the side (`size³` floats **twice** per decode - 134 MB for a 256-pixel texture),
the refpack command list no longer rebuilt by reflection on every decompress, and the BFST lookup
table opened lazily and decoded once. **The branch they were said to be on does not exist in this
clone** - `claude/opentpw-code-review-e9vkqf` is absent and all three SHAs are invalid objects here.

**NOT DONE AS OF THIS ENTRY, and fixed by the entry above on the same day:** `terrain` was **16,333 ms**. It is inside the `.wct`
decode. `Texture.UpdateFromWct` decodes before `CreateTexture` consults its cache, so the cache saves
only the GPU upload - but measured at **≥2,000 decodes for ≥510 distinct textures**, that redundancy
is only ~4:1 and **does not explain 16 s on its own**, which refuted my own first reading of it.
`TryGetCachedTexture` rebuilding a list of every texture per construction is a real defect worth
milliseconds. So the decode itself is slow, and timing it is the next session's first step.

**Confirmed on screen and by census, both.** The loading bar was photographed *while* the stall was
happening - no previous harness could, since they all wait for `Loaded jungle` first, by which point
the bar is gone - with the instrument's own line written underneath it by the bar's status strip.
`assets total=2176 distinct=535`, `rides 15`, `peeps 13`, `staff 5`, `unimplemented 1` in every run,
and `save/` unchanged within all five.

**2026-09-21 - the rides panel is finished: three sliders that save, a live stats table, and the ride
itself spinning in its preview.**

**The sliders are the ride's, not the window's.** Ranges come from the item - `UsageInfo.Min/MaxSpeed`,
`Min/MaxCapacity`, `Min/MaxDuration` - and values from the thing, at `+0x58` as a dword and `+0x5d` /
`+0x5c` as bytes; `Upgrades[0].Init*` is what a newly bought ride is stamped with, so one no longer
opens carrying nobody for no time. Each hides only where the original hides it: capacity on
`min == max`, duration on `DurationUnit` nought - which is how a coaster says its length comes from
its track - and **never speed**, where the original leaves a dead control standing. The commit is
vtable `+0x3c`, which both cycle arrows and the close path call. Measured: capacity **5 of 1..10**
moved to **6** by one click and read back as 6 after the window was closed and reopened.

**`mOperatingSpeed` is at save offset 1036, and the offset is derived rather than guessed.**
`FUN_004db7d0` is the object's own serialiser and writes capacity, duration, then speed; carrying its
order on from 1036 lands exactly on **1054**, where this project already read `PricePerUse`. That
agreement is what makes it an offset. `ParkRides` had recorded this as "not established" and refused
to guess it.

**The stats table's labels were already ours, and four of its seven rows now carry numbers.** The
builder gives its seven left cells UITEXT rows 17-23 - Users last month, Age, Excitement, Reliability,
State of repair, Remaining life, Scrap value - and `UIStrings` names every one, so nothing needed
inventing. Age is real elapsed days over the built stamp (a shipped save reads in the thousands
because its rides really are 26 years old) and **Scrap value is the build price**, with depreciation
still counted.

**The four condition rows are GAUGES, and that is why they were blank.** `FUN_004ade40` hands
Excitement, Remaining life, Reliability and State of repair each `((v & 0xff) << 10) / 100` - a 0..100
percentage onto a 0..1024 bar - while the rows either side of them take a string. Setting Text on a
gauge renders nothing, which is why the labels looked perfect and the values showed nothing at all.

**Two of the four are filled, from floats the save was not reading.** `FUN_004db7d0` writes three
unnamed floats after `mQueueSizeInCells` with the tag `'pv'` and no field name, in the order `+0x4c`,
`+0x48`, `+0x44` - so from 1062 they land on **1066, 1070 and 1074**, and carrying the order on lands
`mTotalTakings` on **1090**, where it is already read. Which is which came from two directions rather
than adjacency: `FUN_004df8f0` stores `0x42c80000` - `100.0f` - into `+0x44` and never touches `+0x48`,
while the breakdown does `*(float *)(this + 0x48) - k`. Measured: Belly Bounce **repair 100, life 100**.

**Excitement and Reliability are counted, not fitted, and the reason is recorded.** Their whole
arithmetic is now read - base `item[+0x13c] * 60 / 100` scaled by two ratios each clamped to
**0.75..1.25** - but the ratios divide by the descriptor's `+0x1a8` and `+0x1a0`, and `park.md` already
records that **which `.sam` key feeds either is unproven and must not be guessed**. Having the formula
does not supply its inputs. Users last month needs the record's ring buffers, which `ParkWorld`
deliberately does not read. And **Age's wording is not decoded**: `0x1b1` read as a UITEXT row is
empty, and sweeping 428..438 in the running game gives the slider-duration family, so that number is
not a row index and a bare figure is the honest output.

**The preview is the model standing in the park, drawn again rather than moved.** `DrawOverlay` takes a
transform now, so the ride is shown mid-animation without dragging it across the park; it is drawn in
`Level.Render`'s depth-cleared overlay pass, after the HUD, which is where the advisor already draws.
`LobbyModel` gained a real bounding box because `Radius` is a distance from the ORIGIN: Belly Bounce
reported 100.2 while its true footprint is 3x4 cells, so a preview sized by it drew the ride at about
a fifth of its panel.

**And then it orbited, which took a burst of frames to see at all.** Two frames apart proved the panel
changed; they could not say whether the ride turned in place or swung round a point beside itself. A
burst of sixteen showed the centroid tracing a **circle of constant radius**, which is a rigid body
rotating about the wrong point - not a wobble and not the animation. The cause was `LobbyModel`'s
per-mesh box: it swizzled and scaled a mesh's raw bounds and added its offset but never put the mesh's
own rotation through, and its own comment called that "a little loose". It is not loose, it is
**displaced**, and a box displaced by a constant gives a centre displaced by a constant. Belly Bounce's
first mesh keeps its bulk 25 units from its own origin, so the centre came out about nineteen units
wide of the geometry and the spin swung the leftover. All eight corners now go through the very linear
transform the geometry is drawn with. Measured, same park, same ride:

| | before | after |
|---|---|---|
| centroid spread | across 0.387, down 0.299 | **across 0.040, down 0.024** |
| centre | (20.2, 34.2, 9.1) | (15.0, 20.0, 9.1) |
| half / perUnit / span | 39.9 / 1.945 / 79px of 194 | 25.1 / 3.099 / **93px of 194** |

**Four wrong hypotheses came before the right one**, each tested through pixels rather than through the
terms themselves: a stale `PlacedOrigin`, a double-counted mesh offset, a double-applied park heading,
and a mismatch between the drawn set and the boxed set. That last one was built and changed the lit
pixel count by about twenty and the spread by 0.002 - it was refuted outright. What ended it was
logging each mesh's live position beside its boxed centre, which made the two spaces comparable
instead of arguable.

**The ride sits low in its panel and that is NOT a framing fault.** The drawn box's centre and the fit's
centre agree to **(0.0, 0.0, 0.0)**, with every mesh's live position equal to its rest offset, and the
camera is orthographic and aimed at the origin - so a centred box cannot land off-centre. The
lit-pixel centroid still reads 0.591 down the panel because that is **where the pixels are**: the wide
wooden base carries far more of them than the thin figure on it. Centring the silhouette instead would
be a deviation from the engine, which fits from the model's box (`FUN_004689f0`), so it is left alone.

**One caveat, and one correction.** The spin **stops while the clock is held**: the original differences
a real-time clock and keeps turning through a pause, and nothing here exposes wall-clock time while
paused - `Now`, `Delta` and `RawDelta` freeze together - so adding one for a spinning model was judged
wider than it is worth. **The scissor is no longer unproven**: this file called it untested because the
model fitted inside its panel, and then Alexah reported a *"harsh cutoff"* across the bottom of the
ride - which is the scissor clipping, observed. It works.

**Four instrument faults, and every one of them failed working code.** A stats check demanded MORE ink
after opening, when a dark panel replacing bright park makes the crop darker - it read 90% shut and 2%
open and full. The preview harness cropped the panel `0x3e24` while the model lands in the chevron
`0x3e25`, which reaches further left and higher. A `difference` helper was used without being defined.
And the spin was measured with the park **paused**, which freezes the very clock the spin runs on -
bit-identical frames, 0.00% to two decimals, were the tell, because a slow rotation gives a small
number and never an exact zero. Two of my own diagnoses were wrong before the evidence corrected them:
"nothing is drawn" when a smudge was plainly there, and "a unit error" resting on a multiplication I
had got wrong.

**2026-09-21 - the Info and Money buttons open four real screens, and item 2 is finished.** allstaff
(`0x750e10`), allitems (`0x7508e0`), allpeeps (`0x7506c8`) and entryprice (`0x751798`), built from
their own layout streams. **The HUD is six CATEGORY pickers, not seventeen buttons**, and
`FUN_004a0810( category, screen )` at the head of every builder confirms the 1 / 3 / 10 seeding from
the opposite direction to `FUN_004a0940`'s globals.

**Walking the streams was not enough, and that is the transferable part.** A column heading is not in
the layout data at all: each builder fetches the header child by id `0x10 + index` and hands it a
UITEXT row, so a stream walk yields the right number of unnamed boxes. Reading the four sub-builders
gave the rows - rides 82-86, shops 87-91, sideshows 92-97, miscellaneous 98-99, staff 101-105,
visitors 113-118 - and revealed that **allitems loads three different list trees for its four tabs**
(5, 5, 6 and 2 columns). **UITEXT 117 is literally `"?"`**: the original ships that visitor column
unnamed too.

**Mesh names were resolved by hash rather than guessed**, and the method is worth keeping: the stream
asks for a mesh as `h = (c ^ h) * 47` over the model's first NODE name, so hashing `ui.wad`'s table
resolves them - but **only the 278 `.md2` entries are candidates**, because `UiMesh.Get` loads
`ui/<name>.md2`. Matching all 1202 entries instead picked up two textures and produced names that
loaded nothing (`b_pkinfo`, `b_sresrcher`, whose models ship as `b_parkinfo` and `b_sresrhcer`). The
arithmetic was checked against two known pairs first. It also refuted the reading that entryprice's
three right-hand buttons are a spinner: they are `b_staffcost`, `b_loans` and `b_finance`, and the
handler sends them to this category's other three screens.

**Predicted, then measured.** Fee **25**, moved **25 -> 26 -> 24**; gate **open -> shut -> open**;
cleaner's wage **63** and scientist's **125** (`BaseWage * PayMultiplier` on the easy numbers, 7x9 and
5x25); All Miscellaneous Items counted **Small Toilet 3**, which the save's own `VisitableFlag` census
independently records as three. Both buttons were pressed through the real hit test, not opened behind
their backs: *"the interface took (162,558)"* and *"(100,609)"*.

**Four defects the screenshots caught that every green check passed.** Two were invisible at 4:3 and
only appeared on a 16:9 window, both from the same cause - a control **one pixel outside its parent**
stops following it and resolves its own anchor: a column heading landed 160px clear of its column, and
a tab strip parented to one of three swappable lists **vanished with it**, stranding the screen. One
was a transposition: the executable's kind->label switch is deliberately out of order (case 2 takes
`0x6e`, case 3 `0x6d`), so a plain 107-111 run drew guards under *"Entertainers' Happiness"*. And the
visitor list rebuilt **every frame** - caught as **260** gap reports from a single visit, fixed to the
original's own 2000ms timer.

**What stays blank stays blank.** Shops, sideshows, "Users Last Month" and "Excitement", and visitors'
Time In Park and Rides Ridden are counted rather than filled: putting an object's gross takings under
"Total Profit" would be a different quantity wearing that label, which is the mistake the hire
screen's own remarks already warn against.

**2026-09-21 - the screens have their FRAMES, and five had been drawing without one.** Alexah looked at
the result and said it seemed to be floating info panels. It was: every one of these screens, and the
buy and hire screens built before them, had no backdrop, because the tree recorded their frame mesh as
unresolvable. `ParkBuyScreen` had said so for months - `0xf76e4200` matched "no name in the executable
or any shipped file, after a search of all 2,488 of them" - while its own next sentence noted that the
hash is over a model's first NODE name, which need not be a filename at all. **The search was never
widened to node names.**

Reading them needs decompression: all **278** of ui.wad's `.md2` members are refpack-compressed, so
every scan of raw bytes - including the `meshhash.py` resolver this project already had - was searching
compressed noise. A throwaway harness over the tree's own `WadArchive` + `ModelFile` yields **1,563**
node names, and every outstanding hash falls out at once:

| Hash | Node | File | Wanted by |
|---|---|---|---|
| `0xf76e4200` | `window4` | `w_big.MD2` | buy, hire, allstaff, allitems, allpeeps |
| `0xf76e42eb` | `window1` | `w_small.MD2` | entryprice |
| `0x257b71f9` | `varibox` | `f_varibox.MD2` | the fee spinner |
| `0xaaee5929` | `b_ride it!` | `b_rideit.MD2` | the ride window's last unresolved button |

The frames are a family - `window1` `w_small`, `window2` `w_med` (the ride window, already known),
`window3` `w_park`, `window4` `w_big` - and the two root hashes differ by `0xEB` = **47 x 5**, the
signature of two names differing in one trailing character, which was visible before either was named.
`b_ride it!` carries a **space and an exclamation mark**, which is why a token scan would have missed
it even uncompressed.

**Adding the artwork broke something only a screenshot could catch.** The entry-price label sits inside
the spinner's rect, and was added before it, so the moment the spinner gained an opaque mesh it painted
over "Ticket Price" and the words vanished. The stream's own order closes the spinner before opening
the label; matching it puts the text back.

**2026-09-21 - a park can be built, staffed and managed.** The purchase menu, the hire screen and a
placed ride's management window all open, and the verbs under them work: buy, sell, move, carry, hire,
fire, pick up, put down. Three screens, one new widget (the original's control **type 7**, a scrolling
multi-column list, which nothing in the tree had), and the first world-click path this project has had.

**Clicking a ride opens its window, and there are NINE such windows, not one.** They share base opener
`FUN_0048cea0` and are dispatched by `FUN_00486920( thing )` on the thing's kind byte at `+2` and then
on the item's `WhichUIType`. The ride's is built from the stream at `0x00755150`, walked to a balanced
op 5. **Every verb was identified twice over**: `FUN_0048cd10` sets map tool **0x33** (demolish) and
wears `b_erase`; `FUN_0048cfa0` demolishes and then carries with **0x3b** and wears `b_move` - which
independently confirms that the original's move IS demolish-then-carry, the shape `ParkBuilding.Move`
had already been given from the other direction.

**Three corrections the screenshots caught and the region checks did not.** The hire screen's
"Balance" row was showing the park's cash; `FUN_0049bdd0` fills it from three monthly ring buffers as
cash-in minus total costs, which is last month's NET - a different quantity wearing that label, put on
screen twice. It is blank now, with only the staff bill answered. Its two COST rows are **red**
(`FUN_0065c5d5( 0xff, 0, 0, 0xff )` is called on those two and never the other two). And nothing is
selected when that screen opens - `FUN_0049b5b0` fills the list and never calls the select - so a
build that opened with a filled info panel would be wrong.

**Four instrument faults are worth more than the features.** A region check passed on the buy screen
while every tab inside it was missing, because the list's backing panel covered the rectangle. Crops
anchored by their own centre landed on park grass and reported its colour as a label's - a control's
anchor is **inherited from the window root**, not computed per rect. An ink detector measuring
luminance scored a cell containing "538" identically to the blank cell beside it, because the change
under test was to make that text **red** and pure red is L=76. And a title check "passed" at 98.97%
ink with the window **shut**, because the title sits over sky. Each was replaced by a claim that can
fail: lettering arriving in a cell that was empty, a cycle reaching a different **thing id**, a census
count dropping by exactly one - every one of them paired with a line the game itself wrote.

**Confirmed in a running park, by capture and by console.** The hire screen: clicking a row fills the
info panel with *"Duke Mighten  grade 1  36 a month"*, 0.00% ink to 6.39% against a 0.00% floor. The
ride window: opens over a 1.89% floor at 97.23%, its own close button returns it to 2.65%, the cycle
arrows reach a second ride and the title follows (19.25%), and delete takes the census 15 -> 14 with
the game reporting *"sold 'Belly Bounce' (thing 13) for 500"*. `save/` unchanged within every run.

**Synthetic pointer motion never reaches the game** - a warp with no real movement behind it reaches X
and not SDL, measured twice - so `WindowStack.ClickAt` and a `click x y` console command exist to enter
the real press-and-release path. Only SDL is skipped: the hit test, the row arithmetic and both
handlers are the real ones. `openthing` and `catalogue` were added for the same reason.

**Placing by POINTING, and the thing-id collision it exposed.** Both screens put an item or a person
into the hand and a click on the park puts them down - `Level.WorldClick`, where **anything in the hand
goes down before any window opens**, which is the original's order: a place mode consumes the click and
only an idle mode (type 0 or 1) opens windows. Proved with clicks alone, no console placement verb: a
ride built for **exactly** its 500 and a cleaner hired, the object census and the staff count each up
one, and `PLACE_BY_POINTING` and `PLACE_STAFF_BY_POINTING` both gone from the `unimplemented` census.

**And what is picked up can be put back.** The right button cancels a carry, which is the original's
way out of a place mode: nothing is charged for picking something up, so cancelling gives nothing back
and takes nobody out of the pool. **The button's own edge cannot be driven by any harness here** - no
more than the left's can - so the console's `drop` reaches the same `Level.CancelCarried` rather than a
copy of it, and that shared body is what is measured: carry a ride, drop it, click the park, and
nothing is built. Two copies would have been free to drift, with only one of them ever tested.

**The MOVE button, measured last of all.** It was the one clause of the goal still resting on its
parts rather than on a run: `Sell`, `Carry` and `PlaceCarried` each had measurements behind them, but
the composition did not. Clicking `b_move` on a ride's window refunds 500 and fills the hand, and the
next click on the park rebuilds it - the object count goes **14 -> 13 -> 14**, the balance
**87987 -> 88487 -> 87987** for a net of **zero**, and the ride lands on a different cell,
**(51,23) -> (38,15)**. That last assertion is the one that separates "moved" from "sold and rebuilt
exactly where it stood", and a half-done move - sold and never replaced - fails all three.

**The first run of that harness failed, for a reason worth keeping.** It read a net of **+25** and
called a correct move broken. The harness stepped the game clock ten ticks between two balance
readings, so guests paid at the gate mid-measurement - while its own docstring claimed the clock was
stopped. `pause` stops the clock and `step` restarts it, and the debug console is polled once a
**frame**, which runs regardless, so nothing in that test ever needed the clock to advance at all.
The two amounts are now also read from the game's own lines, which no gate takings can move. This is
the same trap recorded against the refund check earlier in the project, hit again in a new harness.

**That end-to-end run caught what 818 green tests could not.** Objects and people share **one**
thing-id numbering, and there were **two allocators** over it: `ParkState.NextThingId` rescanned the
save's objects and people, so it never saw a hired staff member, while `ParkPeople` kept a private
counter that never saw a bought object. Both were seeded correctly from the same maximum and drifted
apart on the first allocation - **a ride and a cleaner in one run were both handed thing 43**.
`ParkState` now counts rather than rescans and is the only thing that issues an id; counting also stops
one being re-issued after the highest-numbered object is sold, which a rescan would do. Seeing it at
all needs a buy **and** a hire in the same run, which is why building one feature at a time never
would have.

**2026-09-20 — guests buy things: `docs/PLAYER-GAPS.md` item 8, both halves.** A filled park took
**1110 at the Drinks Shop** (37 sales at 30) and **900 at the Jungle Spray** (45 at 20), with the new
`spend` census showing the cause before the effect and a screenshot showing guests queued at the
kiosk. `save/` unchanged in all four runs.

**The shop's "structural blocker" was a misread field.** The offer filter compares a queue's length
against the object's `+0x40`, and this project read that as `mQueueSizeInCells` out of the save —
nought for the shop and all three toilets, so nothing could pass. It is not that field as loaded:
`FUN_004de130` **overwrites** `+0x40` by walking real type-3 cells off the map whenever `mBackOfQueue`
is nought, and `FUN_004dd920` calls it *before* reading the count. `+0x40` is a cache, and the save's
copy is the cached answer to that walk — which the implementation proves by **reproducing both cached
pairs the save already holds**: the Belly Bounce recomputes to 4 cells ending at 2866, the Jungle
Spray to 1 ending at 3765, and neither number was put in. The argument that should have raised the
doubt years earlier is that the three toilets are in the identical position: under the old reading no
toilet in any park could ever be visited.

**And a shop needed no new mechanism at all.** `docs/exe/ride-operation.md` said a shop takes its money
through LIMBO; `Coconut.RSE` declares **zero** limbo and walk slots and uses neither family, running
the same `VAR_LETMEON` → `WAIT 1000` → `VAR_LETMEOFF` handshake a ride runs. That page contradicted its
own list of limbo users, and the list was right. Four claims on it are corrected, including
`FUN_004e1a10`, which is **`mCostOfGoods`** and not the chance-of-winning accessor it was called twice.

**What made a visit do anything was the win roll, which nothing had ever written.** `FUN_004e2670`
rolls as a guest enters and writes the result into `mQueuePos`; the settle-up splits on that byte, so
with it always nought **every visit in the park took the losing arm** — which is why a sideshow charged
twenty and did nothing else. The chance is `100 - UsageInfo.InitChanceOfLoosing`: a shop declares none,
so its chance is 100 and a drink is always served; the Jungle Spray declares 75, so 25.

**Two things got predicted wrong and measured right.** The Jungle Spray's prize is **50**, not the 5
two separate readings transcribed — which flips winning from costing 30 happiness to gaining 19, and a
test caught it. And the first explanation of why no shop sale appeared in a live run (thirst) was
refuted by the instrument built to test it: `thirstMax 100` with 21 guests over 50 and still no sale.
The real reason is that thirsty guests are rarely still *deciding* — of 148 samples at thirst 50+,
**73 were HeadingForExit and only 11 Deciding** — so a `thirst` console command was added to create
the condition, justified exactly as `load` is for the seaplane.

**2026-09-20 — all three vehicles drive, and `TRIGWAITANIM` is the whole of why they did not.** The
ferry and the seaplane stood still for one reason: `Ferry.RSE` and `seaplane.RSE` start every animation
with `TRIGWAITANIM` where `bus.RSE` uses plain `TRIGANIM`, and that opcode had no case — so it fell to
the counted default and the one instruction that would have started their route was stepped over.
Setting script variables could never have moved them: `ParkObjects.Sweep` poses a stood thing from its
animation channel, and a channel nothing triggers does not advance.

It was left counted on purpose — with no model bound it compares against its **raw third operand** and
parks the script for ever — and `ParkFixedItems` binding models is what made it safe to build. Measured
across every shipped script rather than taken from the note that claimed it: **133 uses in 48 scripts,
132 with operand three differing outright and the last a variable, none equal**, so a faithful
model-less path would hang all 48. The model path is reproduced exactly (`0x552c1a`: mark `+0xbc` with
the role plus one, rewind four words *without* giving up the slice, re-entry compares channel nought's
role plus one, clear at `0x5535f4`); the model-less path is a declared deviation that steps over,
counted — which is why the test pinning the old behaviour still passes **untouched**.

Confirmed in a live park with all three moving in one run: bus `619.3 → 510.5`, seaplane
`491.9 → 483.5 → 461.4`, ferry `791.9 → 601.7 → 554.2`. Guests now step off *after* the aircraft lands
rather than before — `peeps` holds at 13 through about nine seconds of approach, then 16 → 21 → 25 → 30.
**A first run read that pause as a stall and it was not:** the probe had stopped polling too early.

**And then the park emptied itself, which was this session's own defect.** Every vehicle script parks
**three** times a circuit - `bus.RSE` at instructions 42, 87 and 117 - each setting a status and then
spinning on `TEST VAR_TRIGGER / ENDSLICE / BRANCH_Z` onto itself. Releasing only the first, which is
what sending a spent load away did, left the bus stopped at the second: measured with a new `vehicles`
console census as pc 90, `VAR_STATUS` 4, `VAR_TRIGGER` 0, unchanged from 69s to 169s, with one guest
still owed. Arrivals are gated on the vehicle reporting 2, so after the very first load nobody else
ever came and the park drained to nought.

The tail of `FUN_004cf3e0` at `LAB_004cf4b6` is what was missing: it runs on **every** tick, not only
while a load is being dropped, and re-triggers the vehicle whenever its status is -1, 0 or 4 (and on 2
with an empty load). Summoning, releasing and sending away are all the **same write** - `FUN_0051a2f0`
ends by setting variable nought, `VAR_TRIGGER`, to one on the vehicle already standing; only a freshly
*created* thing is different, getting `VAR_STATUS` = 1. Status 6 additionally makes `FUN_0051a690`
forget the vehicle so the next load picks afresh. With `ParkPeople.StepVehicle` added the bus cycles
through statuses 1, 2, 3, 4 and 5, arrivals recur, and the population moves both ways again -
13 → 15 → … → 9 → 10 → 7 over three unattended minutes, 5 in and 10 home.

**All three were then watched round a full circuit**, with the `vehicles` census rather than by eye:
the bus through statuses 1, 2, 3, 4 and 5; the seaplane pc 15 → 29 → 41 → 81 → 29, looping, statuses
1, 2, 3 and 6, with `VAR_TRIGGER` caught at 1 on the status-6 release; the ferry pc 17 → 24 → 38 → 17,
looping, while `peeps` climbed 13 → 49 → 109 → … → 258. **The ferry's statuses 4, 5 and 6 were never
sampled and that is polling, not absence:** returning to pc 17 is reachable only through `BRANCH ->2`
at instruction 69, which lies past both the status-4 spin at 42-45 and the status-6 spin at 64-67, so
the loop closing is itself the proof it traversed them. Polls were 23.6s apart and those stages take a
second or two.

**One probe error worth keeping, because it nearly read as a defect.** An earlier attempt had the
ferry apparently stuck at pc 24 for seven polls. `parkprobe` re-sends every command each round, so
`load 70` reset the outstanding count to 70 every 14.4s while only about 58 thing ticks fit in that
window - the load could never empty, and pc 24 is the spin released by emptying it. `load 40` cleared
comfortably, which is why the seaplane cycled and the ferry appeared not to. Widening the round to 20s
showed the circuit at once. A vehicle starved by the instrument looks exactly like a vehicle that is
stuck.

**And then they were looked at, which the censuses above are not a substitute for.** A moving mesh-0
position proves a transform is being updated, not that anything is drawn - the distinction this project
has already been caught by once, reading an empty road as "never drawn" when it was a capture that
settled at 2.5s while the bus arrived at 13s. `vehicleshot.py` in the harness cache takes a burst of
frames while polling `vehicles`, and writes the pc and status into each file name, so no picture stands
on its own. Thirty-nine frames, every one presenting (mean brightness 103-119, never black):

| | at `VAR_STATUS` 2, unloading | after it pulls away |
|---|---|---|
| bus | at the stop between the two shelters, by the crossing | moved well left along the road (status 3) |
| seaplane | on the water in the bay, floats down | moved and swung round, heading changed (status 5) |
| ferry | alongside the quay | moved along the quay (status 3) |

One frame carries all three at once - seaplane on the water, bus on the road above, ferry to the right.
The arriving crowd is visible too: the entrance path is empty at the start of the seaplane run and a
column of guests is streaming down it by the time the load is spent, which is the pick-up half of the
loop seen rather than counted. Frames are in `~/.cache/tpw-harnesses/shot-{bus,seaplane,ferry}/` and
deliberately not committed - this repo ships no game content.

**Two things about that are honest rather than flattering.** The arrival gaps measured 18.9s and then
32.0, 38.7, 38.7 - so the rate is no longer `TimeBetweenArrivals` alone but the timer **or the vehicle's
circuit, whichever is slower**, which is what gating on the vehicle must mean. And the park is still
net-negative, because the headcount is floored at `Arrival.MinPeople` = 1 while `FUN_004c8240` stays
undecoded; that deviation predates this work and is unchanged by it.

**One approximation is named and not built:** `FUN_0051a9d0` chooses between two sets of states by
asking whether a peep is standing at the stop, on four cells around `FUN_004d8650`'s first cell. Which
balance-file pair that getter returns is **still unproven** - the `+1` among `{c, c+1, c-0x100, c-0xff}`
means the pair is adjacent, which rules out `BusStopA/B` at (42,5) and (53,5) and leaves
`CrossingBSSideA/B` at (47,5) and (48,5) as the likeliest reading rather than an established one. An
attempt to settle it by cross-reference failed both ways: all four globals are READ-only from
`FUN_004d8650` itself, and the executable holds **no** `BusStop` or `CrossingBSSide` strings, so the
balance loader matches those keys without them. Every state the original ever nudges is nudged here
instead, which changes which arm fires and never whether a vehicle moves.

**Two corrections worth keeping.** The `paths` census's "of the route" figure is static clip data, not
live progress — it reads 98.8 and 104.0 on every poll while the models are visibly travelling, so a
reading of "parked at the end of its route" drawn from that column was wrong; mesh 0's position is the
only honest signal. And the first test written for the mark was **hollow**: a mark left standing still
falls through, so asserting the instructions after it passed under mutation. It now asserts that the
second trigger actually *queued*, which is the thing that stops happening.

**2026-09-20 — the guest loop closes: they arrive by themselves, and they go home.** A park left alone
now runs `peeps 13 → 14 → 15 → 13 → 12 → 11` without anything typed — seven arrivals and ten
departures over two and a half minutes.

`ParkPeople.StepArrivals` is the manager (`FUN_004cf3e0`): it waits out `Arrival.TimeBetweenArrivals`,
picks a vehicle by how big the crowd is, and then drops **one guest per thing tick** until the load is
spent. The period is in quarter-ticks, so 150 is 600 of the 31 ms ticks — **18.6 s predicted, 18.9 and
18.8 measured**, which is the clearest evidence yet that it is decoded rather than tuned.

Departures are `ParkPeople.Depart`, triggered by `ExitLevel` running out — a countdown that had been
ticking since the save was first read with **nothing anywhere reading it**. It reverses everything
`Admit` wires, including `ParkState.Forget`, which is new: `LeaveCell` unlinks a cell's own chain but
leaves `_cellOf` naming a cell the thing is no longer on, and that entry decides what a later `StandOn`
undoes. A guest a ride or a queue is holding is refused, which is the original's own condition.

**The deviation is in what an unbuildable state means, not in a transition that works.** A leaver walks
`HeadingForExit → PickingACellOutside (19) → AtTheBusStop (21) → Leaving (17)`, and 19 and 21 both read
a balance-file cell pair that is still unproven. Rerouting `HeadingForExit` was tried first and three
tests that pin that transition said no — correctly. So 19 is treated as the end of the walk instead,
which leaves every tested transition untouched.

**(superseded the same day) Not done: the ferry and the seaplane still do not move.** They are stood and
they have ids, but scripts are bound by walking the save's object list, and the save names neither — so
nothing drives them. Both halves of that were then fixed: a second binding pass gives them scripts, and
`TRIGWAITANIM` gives those scripts something to drive. See the entry at the top of this file.

**2026-09-20 — a guest who was never in the save arrives and walks to the gate.** `ParkPeople.Admit`
makes one: a thing id above everything the file used, a `GuestState` whose cash comes from
`PeepTypes[x].StartingCash` and whose exit level comes from `PeepInfo.ExitLevel`, and a navigator at
the centre of the cell. Five things then have to learn about them, each failing quietly on its own if
missed — the simulation list, the by-id index, the walk, the sprite, and the cell's occupancy list,
without which the gate cannot see them. Driven by hand from the console (`arrive`) because nothing yet
runs the timer.

Confirmed in a live park: `peeps` 13 → 14 → 15, the newcomer at exactly the cell centre, and then
`AtGate` → `HeadingForGate` and walking east toward the ticket booths at ~0.12 cells a tick with a real
two-waypoint route.

**One deviation, declared at the site.** The engine constructs a guest in `Deciding` and walks them in
from outside through `WalkingOutside` and `AtTheBusStop` — both of which take their cells from a
balance-file pair that is not proven, so `PeepBehaviour` deliberately answers neither. Left in
`Deciding` out there a guest stands for ever, because `Decide` looks for somewhere inside the park and
they are outside it. So this starts them at `AtGate`, the head of the admission sequence the original
joins them to anyway, and skips the walk in. It goes back the moment that cell pair is measured.

Also corrected: two comments in `PeepBehaviour` argued from "no bus thing runs a script in this
project", which this session made false. The behaviour they guard is unchanged — the other half of
each argument still stands — but the reasoning no longer rests on something untrue.

**2026-09-20 — the arrival mechanism, decoded end to end.** How a park gets new guests is now written
down in `docs/exe/park.md`: `FUN_004cf3e0` waits out a timer, asks `FUN_004cf5b0` for a headcount,
summons a vehicle, and then makes **one guest per tick** through `FUN_004cf720` until the load is
spent. Three findings worth the space. **The vehicle is chosen by how big the crowd is** — under 36
the bus, up to 60 the seaplane, beyond that the ferry — and the save's own field names,
`mArrivalVehicle_Size1..3`, say the same thing from the other side. **The vehicle thing is made on
demand**, which is why the shipped park places a bus and neither of the others: its small-crowd slot
holds the bus and the other two have never been needed. And **nobody rides in anything** — the guest
is constructed at a cell near the stop, so the vehicles are mechanism rather than transport.

`ParkWorld` now exposes the four header fields it had been parsing and discarding —
`ArrivalVehicleForSmallCrowd`, `…MediumCrowd`, `…LargeCrowd` and `CurrentArrivalVehicle`. A test pins
the small-crowd slot to the bus **by catalogue number rather than by the 15 it happens to hold**. That
test was written asserting all four were nought, which is what the code comment claimed; it failed,
and both the comment and the claim were wrong in a way that explained the ferry's absence better than
the original guess did.

Still not established: the arrival **period**. Nothing in the executable writes the three globals the
rate comes from, and the timer counts quarter-ticks of the game clock rather than seconds.

**2026-09-20 — the bus drives its route.** `LobbyModel.Pose` applies channel `0x200`: it samples the
clip's percentage, finds the point that far along the model's closed Bézier route, and moves the node
the track names — carrying whatever hangs off it, so the wheels ride on the body. Positions are written
onto the entities and never into `Offsets`, which stays the rest pose, so the `Rest()` → `Place()`
restore every clip change already relies on still puts everything back.

The route's points are **parent-local**, which cost a run to learn: measuring the shift against the
composed `Offsets` instead of the node's own translation dropped the spline root's (480, 0, 170) — the
same value on all three vehicles — and drove a correctly-shaped route 480 west and 170 south of where
it belonged. Against the local rest instead, the bus reaches **(510.5, 65.5, 0.0)**, predicted to the
decimal before it was read. It comes to rest between `BusStopA` and `BusStopB` (cells 42,5 and 53,5 —
world 425,55 and 535,55) without anything in the code knowing where those are.

**Two things it does not do yet.** It drives its route once and parks, because the clip holds at
220.0/220.0 and nothing loops it. And it does not turn to face the way it is going — the engine samples
the route's first derivative for that (`0x400`, on 62 of the game's 71 route tracks), which is unbuilt
and counted as `ANIM_PATH_FACING`. The second one is obvious on screen: the bus arrives at the stop
sitting diagonally across the crossing, keeping whatever heading it was parked with.

**Looked at, not only counted.** The first attempt photographed an empty road and read as "the bus is
never drawn" — it was a timing miss, because `shotat.py` settles for 2.5 s and the bus does not reach
the stop until about 13 s after the park loads. Caught at the right moment it is plainly there, on the
road mid-route and then at the shelter. Every render gate was checked rather than assumed on the way
past: `Position` is the final translation in `ModelMatrix`, `DrawnByOwner` is set nowhere but the
advisor, and the wheels carry no opaque model but do carry a translucent one.

**2026-09-20 — animations say how far along its route a thing is.** Channel `0x200`, the last sizeable
undecoded one, is **path progress**: a record of `{ float start, count, 0, offset }` and then one float
per frame, the values immediately behind their own header on all 71 of the game's tracks. The value is a
**percentage of the route**, not a distance — space's slide runs exactly 0 to 100 over 121 frames, the
haunted house 0 to 200, which is two laps — and it does not always rise, because the ferry travels its
route backwards. `0x200` is read on its own: `0x400` accompanies it on 62 of the 71 and `0x1000` on the
other nine, and neither changes the record. **Confirmed in a live park:** `bus: 3 progress scalar(s),
42.4..142.5, 100.0 of the route` — three clips that chain into one lap. This still does not move the
bus; it is the number that will.

**2026-09-20 — models read the routes they carry.** The `uint` at model file `0xac` is an array of
16-byte records naming a route's points, and `ModelFile` reads it: the bus, ferry and seaplane have one
each, the haunted house four, and twenty-four of the game's 2,118 models have any at all. Nothing in the
file gives a count — every `u16` in `0x90..0xc0` was measured against the known counts and none is one —
so the number of records comes from the nodes that index them at `+0x52`, floored at one because three
models have a route no node names. **Confirmed in a live park, not from the suite:** the new `paths`
console command answered `paths 3`, with gates and lights routeless and `bus: route 0 type 2 bezier 45
points, first (207.4, 0.0, -247.8)` — every figure predicted before it was read. **The bus still does not
move**: its script runs to `220.0/220.0 HELD`, because reading a route and following one are different
steps. Format written up in the FileFormats clone.

**2026-09-20 — positional lobby audio while the camera flies.** All four parks sound at once, each
heard from its own island — the marked emitter node where there is one, the island itself where there
is not — so the blend between parks is distance rather than a cross-fade. With somebody playing it
collapses to the single island on show, flat or at its node, exactly as before. **A deviation, not a
restoration:** the engine's 3D is the original's (`Sound_PlayEffect` really takes x, y, z, and the game
delay-loads QMixer for it), but the original spent none of it in the lobby, which plays everything at
(0,0,0) — corroborated four ways. Measured by disk capture rather than assumed: `sounding=4` on 17 of
20 polls, peak 0.2499 (−12.0 dBFS), muted control exactly 0.0, so four parks summing does not clip and
the levels calibrated for one park stand. The capture also caught a gap a green build could not: the
one-shots were still flat on three of the four islands.

**2026-09-20 — the attract camera: the lobby flies itself around all four islands.** Lobby plan item
8. With no player selected the camera wanders a box and aims at whichever island is nearest, which is
the first branch of the original's `FUN_005e0470`; with a player it orbits as before. Every number is
read from the lobby object's constructor `FUN_005dfcd0` — box centre (500, 75, 500), extents
(400, 50, 400) full-size, speed 1.0 a tick, arrival radius 10, look cap 2.0 a tick — and the box turns
out to be the lobby's own geometry, which is what says the axes were read the right way round. One
number was chosen rather than read: the look-speed ramp, which the original applies per frame with no
delta. Decode in `docs/exe/lobby.md`. Confirmed in the running game over three minutes: seven island
changes against a predicted six, all four islands reached, and the sound following the camera round.

**2026-09-20 — `docs/PLAYER-GAPS.md` item 1: the gate opens as you enter a park.** It idles shut and
plays its opening clip once, and the park is asked for when the doors finish. The original does none of
this — its entry beat is three calls and a UI close — so it is a rule 11 gap-fill, said at the site.
**Not every gate is a pair of hinged doors**: fantasy's is a worm with no rotation tracks, and taking
the play length from the rotator alone left it inert, so a gate is played for as long as it *moves*.
Confirmed in the running game, jungle and fantasy, against predicted durations; the tests that pin the
rule are mutation-checked, and the two that only pin the file data are recorded as hollow against it.

**2026-09-20 — the memory diet, steps 1 to 5 of `docs/MEMORY-DIET.md`.** Claude Code's project memory
went from **56 files / 1.9 MB to 18 files / 332 KB**. Four commits, no source file touched:

| | |
|---|---|
| `31071c9` | The fourteen rule files become `CLAUDE.md`, which loads in full every session. `CLAUDE.local.md` takes the machine paths and is gitignored. The index drops from 55 lines to 34. |
| `859f577` | **The executable decode moves into `docs/exe/`** — thirteen pages from twenty memory files. This is the one that mattered: those facts existed on one machine, unversioned. Accepted mechanically, 1,042 normalised addresses and 451 `FUN_` names, zero missing. |
| `37decdb` | The two archives and the branch ledger move to `docs/history/`, verbatim and md5-verified. Four workflow traps that lived only in the ledger are lifted into `WORKFLOW.md`. |
| `61640a9` | 1,848 lines of verification postmortems distil to `docs/VERIFYING.md` — 404 lines, one per rule, grouped by symptom. The source's numbering is kept because forty rule numbers are cited from other files. |

**Still owed**, and deliberately not done — these are table rows in the diet, not numbered steps:
`docs/DECISIONS.md` (one paragraph each for the three graphics-migration files), `docs/TOOLING.md`
(the capture and console recipes), the review's phase list, `ride-vm-may-be-replaced.md`, the
docs-clone notes, and the three open-item files. The memory folder still holds all of them.

**Earlier, 2026-09-18.** The live tracker was split: the plan stays in `current-task-progress.md`, its
7,852 lines of dated history became `current-task-archive.md`, now `docs/history/`.
