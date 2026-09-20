---
name: current-task-archive
description: "Verbatim archive of current-task-progress - the old reading header, the 2026-09-16/17 reassessments, every previous plan, and the dated findings back to 2026-09-11. Split out 2026-09-18. Grep it; never read it whole."
metadata:
  node_type: memory
  type: project
  originSessionId: f4cf68d5-1d56-4918-adad-a3f0095ef62d
  modified: 2026-09-18T19:57:22.000Z
---

**Split out of [[current-task-progress]] on 2026-09-18**, which had reached 8,109 lines while being the
file flagged "read FIRST after any compaction" - so every compaction paid for all of it. **Nothing
here is lost or reworded: this is the same text, verbatim, in the same order.**

**Do NOT read this file wholesale.** Grep it for the plan, branch, date or decode you want. What is
still live - where the project stands and what is next - is [[current-task-progress]]. The durable
per-branch ledger is [[contribution-branch-layout]]; the earlier archive is
[[project-history-archive]].

**Things other memories point AT in here:**
- the **42-thing Lost Kingdom census** - grep `THE PARK IS FULLY DECODED`. [[opentpw-format-goal]]
  calls this the project's measure of done.
- the **guest-meter table** - cited as `~2630`/`~2636` from [[park-ride-operation]].
- the **peep key mapping** ([[park-data-layout]]), the **branch-49 state block** and
  **Known-broken** ([[park-engine-from-exe]]), the **guests block** ([[lobby-particles]]).

**WARNING, and it is the reason this file exists rather than a delete:** the material below was
written across 2026-09-11 to 2026-09-18 and is **frozen**. Several blocks were already superseded
when the split happened - notably the "OpenTPW is not yet a game" verdict, the 302- and 545-test
counts, and the old header's own line numbers. **Treat every claim here as dated, never as current.**

## >>> THE OLD READING HEADER, kept for its record of how this file overran <<<

Kept at Alexah's request (2026-09-11) so progress survives auto-compaction - see
[[track-progress-externally]]. Compressed 2026-09-13 after the first park landed; the finished work is
in the commits and in the two park memories, not repeated here.

> **HOW TO READ THIS FILE - it no longer fits in one go.** It is **7,934 lines** (643KB, re-measured 2026-09-17 after the SECOND trim, measured not estimated - and the estimate this replaced was out by
> fifty lines within one session of writing it, which is what the re-measure warning below is for - and
> growing - re-check with `wc -l` rather than trusting this number), and the Read
> tool **refuses anything over 256KB**, which this passed on 2026-09-14. Reading it whole fails with
> "File content exceeds maximum allowed size" and costs a turn. Instead:
> **`Read( offset: 1, limit: 600 )`, then page on in ~600-line steps**, gives the live status block below,
> which is the only part that is always current - then **grep for what you actually need**.
> **>>> A SECOND AND TIGHTER CAP EXISTS, AND IT BIT ON 2026-09-16: Read refuses anything over 25,000
> TOKENS. 2,200 lines of this file is about 82,000 tokens, and even 640 lines was refused at 25,387. So
> the `limit: 2200` this note used to prescribe CANNOT BE FOLLOWED - it fails outright rather than
> truncating, which is at least loud. <<<** The byte cap and the token cap are different limits and the
> token one now binds first, so raising the line limit no longer helps: page through, or grep. **The line limit used to be set far past the
> block's end on purpose**, because the block grows at its TOP and pushes its own tail out of any snug
> window - **but that headroom trick is now moot, since the token cap binds long before any line limit
> does. Page through instead of widening.** Find the block's end by **the first heading titled
> `THE PREVIOUS PLAN`**, never by a line number, which drifts whenever anything is added above it.
> **>>> THAT SENTINEL WAS STALE AND IS CORRECTED, 2026-09-17. <<<** This said to find the end by "the
> `SPAWNSOUND` sentence" and that the block ends "where the `alexah/66-` entry begins". **Neither is true
> any more**: `SPAWNSOUND` now occurs only in this header and then at lines ~3291 onwards, and
> `alexah/66-` is at ~3501 - so a reader obeying the old instruction would have pulled **nine times** the
> live block. The live block is lines **53 to ~374**, which a 600-line read still covers comfortably.
> Worth knowing HOW it was caught: an `awk` check I wrote reported "SPAWNSOUND is within the first 600
> lines" and **passed by matching this header's own description of the sentinel rather than the sentinel
> itself** - a filter finding its own instructions, which is rule sixty-three in [[verify-every-ordering]]. That cost FOUR rounds of nudging in one
> session (70 -> 80 -> 110 -> 120 -> 150), each raise invalidated by the very edit that made it, and
> then 200 and 260 went the same way. **On 2026-09-15 it finally OVERRAN**: the block reached 283
> against a 260 limit, so the documented read silently truncated its own oldest 23 lines - the exact
> failure this note exists to prevent. The cause is that a step moves this number TWICE (once when the
> work lands, once when the push is recorded) and a reassessment can add thirty more; the block went
> 146 -> 283 in ONE session. Hence 400 rather than another nudge. **Re-check this number whenever you
> add to the block, re-check it AGAIN after recording a push, and if the correction itself changes the
> line count, re-measure before trusting the new figure** - the only edits that end the chase are ones
> that replace the same number of lines. Raise the limit in BOTH this header and `MEMORY.md`, and
> raise it far, not by a little.
> **>>> THE BLOCK REACHED 598 OF 600 AND WAS TRIMMED ON 2026-09-17 EVENING. THE FIX IS WORTH KNOWING. <<<**
> It hit fourteen lines of headroom, and then the warning written to prevent an overrun **consumed twelve
> of them** - the exact chase this header describes, where a step moves the number twice and each raise is
> invalidated by the edit that made it. **Raising the limit was NOT available**: the token cap binds first,
> and 640 lines of this file was once REFUSED at 25,387 tokens, so ~600 lines is a hard ceiling.
> **What was done, and it moved no content at all:** the `DEFERRED, each with its reason:` paragraph - the
> thing that DEFINES where the block ends - was moved UP to sit immediately after the live plan. Everything
> below it became history by definition, with not a line of text relocated. The terminator dropped from 598
> to roughly 370. **Prefer moving the boundary to moving the contents**; it is one edit and it cannot
> scramble the file.
> **>>> FIND THE TERMINATOR BY A LINE THAT STARTS WITH IT, NOT BY THE PHRASE ANYWHERE. <<<** The phrase
> occurs THREE times - twice in this header explaining the convention, once as the real thing. The real one
> is the only one at the START of a line; the header's two sit mid-sentence behind a blockquote mark. Use
> `awk '/^\*\*DEFERRED, each with its reason:/ {print NR; exit}'`. The older `NR>62` guard gets the right
> answer only because those quotes happen to sit above line 62 - luck, not construction; rule sixty-three.
> The live block ENDS at the **`DEFERRED, each with its reason:`** paragraph, which since 2026-09-17
> evening sits **immediately after the live plan's P5 entry** - find it by those words, never by a line
> number, which drifts whenever anything is added above it. **>>> IT NO LONGER SITS JUST BEFORE
> `THE PREVIOUS PLAN`, WHICH IS WHAT THIS SENTENCE SAID UNTIL THE TRIM - that heading is now hundreds of
> lines further down, with the finished P1-P5 evidence between them. <<<** (**Older wordings named the
> `SPAWNSOUND` note and the `alexah/66-` entry, and both are long gone from the block.**) Check that line is still inside the limit, and raise it WITH HEADROOM in both this file and
> `MEMORY.md` when it is not. Reading a little too much is free; reading too little hides the newest state. (Do not write the marker characters as prose anywhere in this file - the
> balance check counts them, and a quoted one reads as a real marker with no partner. **MEASURED
> 2026-09-17: a whole-file count is off by exactly TWO, and both are innocent** - this sentence and one
> other paragraph mention a backtick-quoted marker while explaining the convention. So pair the markers
> by WALKING them, not by counting: two unclosed openers and zero strays is the healthy state, and
> anything else is real. Do not spend a round 'fixing' the two.) The `>>>` markers flag live claims; the
> numbered "Priority 1 to 4" headings are an OLD list kept as a record and are **not** the plan's
> P1-P5. Everything below "Findings worth keeping" is history, not instructions.

# >>> THE 2026-09-16 AND 2026-09-17 REASSESSMENTS - SUPERSEDED BY THE 2026-09-18 PLAN <<<

*(These were the first two top-level blocks of the live window. The 2026-09-18 plan in
[[current-task-progress]] overtakes them. Kept for the measurements, which were real on the day.)*


# >>> WHERE THINGS STAND, 2026-09-16. READ THIS FIRST. <<<

**>>> FULL PROJECT REASSESSMENT, 2026-09-16, AT ALEXAH'S REQUEST - EVERY FIGURE BELOW WAS MEASURED THIS
SESSION, NOT INHERITED. <<<** They asked for it explicitly because a plan built on stale claims is worse
than none: *"There's no point in making a plan and or reassessing project status on false information."*
Two read-only sweeps plus my own measurements. **Nothing was built or committed; the tree is unchanged.**

- **HEALTH, measured:** clean `--no-incremental` build **127 warnings / 0 errors**; suite **302 passed /
  0 failed / 0 skipped**; `save/` byte-identical either side (`e49e4197...`). Log:
  `~/.cache/tpw-harnesses/reassess-2026-09-16.log`. **The tracker's quoted numbers were CORRECT.**
- **GIT, measured by `ls-remote` not tracking refs:** all **81** `alexah/*` branches and all **10** docs
  branches are on origin at matching hashes; `main` is `453e779` on local, origin AND upstream - identical;
  **0 `refs/pull/*` on ORIGIN**, which is the check that matters - a PR made from my work would appear
  there. **>>> "ON EITHER REPO" WAS WRONG AND IS CORRECTED, 2026-09-16: UPSTREAM CARRIES 13. <<<** They
  are upstream's OWN project history (PRs 2-30 - dependency bumps, a code of conduct, its old engine
  merges), and **none of them is mine**: every pull head was checked against my branch tips and no tip
  appears. `refs/pull/*` lists every PR a repo has EVER had, merged and closed included, so a non-zero
  count on a long-lived upstream is normal and is not evidence of anything. **This file already had it
  right once** - the branch-42 push record says "0 on origin (upstream's 13 are ...)" - and a later
  summary flattened it to "either repo". [[ask-before-github]] states the real gate correctly as
  `git ls-remote origin 'refs/pull/*'`. Nothing is owed. `main..alexah/81` is **260 commits, 232 files,
  +43,409 / -944**, a **perfectly linear stack with zero ancestry breaks**, spanning 2026-09-08 to 09-16.
  One stray: local `wf-review-56` (`363346d`) was never pushed.
- **UPSTREAM IS DORMANT** - its newest commit is `453e779` (2026-09-07) and the one before that is
  **2025-02-20**. One commit in about seven months. So whether these 81 branches are a fork or a
  contribution is a real open question, and it is **Alexah's to answer, not mine to assume**.
- **>>> THE HONEST VERDICT: OpenTPW is a high-fidelity asset loader, renderer and front end. It is not
  yet a game. <<<** **>>> THAT VERDICT WAS TRUE ON 2026-09-16 AND IS NO LONGER - PAST-TENSED RATHER THAN
DELETED, BECAUSE IT RECORDS WHAT A SWEEP FOUND ON THE DAY. SEE THE LIVE BLOCKS BELOW: guests now route
across the real map, walk it on the thing engine's own beat, age, animate from the executable's own sprite
scripts, and face correctly as the camera turns. <<<** As measured THEN, there was **no simulation layer
at all** - zero repo-wide hits for
  `Pathfind`/`AStar`/`Navigate`/`Waypoint`, for `Funds`/`Income`/`Admission`/`Rating`, for
  `Breakdown`/`Nausea`/`Safety`, for `Bulldoz`/`Terraform`. `ParkBalance` loads all **362** economy keys
  and its only consumer is `ParkWeather.cs:250`. Nothing walks, operates, earns, spends, builds or saves
  a park back. `World/Level.cs:180-183` says so in the tree itself.
- **WHAT IS GENUINELY BUILT:** the lobby end to end (slots -> dialog -> island panel -> park entry, which
  **IS** done); park terrain, ground, paths, objects, cameras, weather (a real simulation) and audio;
  the UI framework; 36 format readers; `DebugConsole` (~30 commands) behind `OPENTPW_DEBUG_CONSOLE`.
- **WHAT IS A SHELL, and this is the correction that matters most:** the **park HUD**. `ParkGadget.cs` is
  513 lines with a live date and key counts, but **4 of its 6 buttons are log-only `NotYet(...)`**
  (`:279/:297/:312/:315`), the happiness gauge has no mesh and no value (`:248-252`), and `ParkFrontEnd`'s
  Load/Save/Publish are log-only (`:168/:169/:174`). **[[park-hud-from-exe]] said "no clock, no cash, no
  toolbar, no advisor" and that was FALSE** - which is why "do the HUD next" was bad advice.
- **RIDE SCRIPTS, counted by me:** `source/OpenTPW/VM/RideScript.cs` is 1,783 lines with exactly **57 of
  106** opcodes (`Opcode.cs:144` declares 106), falling to a **silent** `++NotImplemented` at `:764`.
  **`RideVM.cs:42` hardcodes `totalCount = 210`** and the README repeats it, so every coverage percentage
  the game logs is wrong by roughly half.
- **DEFECTS VERIFIED THIS SESSION, worth acting on:** (1) **197 base models ship only a bare `{stem}M.md2`
  and `LobbyModel.LoadAnimations` never loads it** - **RE-MEASURED 2026-09-16 and the old figures here were
  wrong: it is 197 over 312 archives / 445 base models carrying at least one role**, not "191 over 306 wads
  / 1,219 models". Re-run `~/.cache/tpw-harnesses/rolecensus.py`, which applies the engine's own rule.
  `LobbyModel.LoadAnimations` probes `M1` upwards with **no bare fallback**, which `RideAnimations.cs:404`
  got in branch 72. **The `TryLoad` half of this claim is REFUTED: of the 197, exactly ZERO are
  position-and-visibility-only, so `:742` never fires on them** - 160 carry rot/morph/uv and 37 are EMPTY
  files carrying no track of any kind. See the item-1 block below for what that leaves. (2) **Park button
  glints are allocated, ticked and killed unseen** - `SetupParticles()` runs for parks (`Level.cs:94`) but
  `SetupParkHud` adds no `ScreenParticles`, so `ScreenParticles.Current` is null at the only draw site.
  (3) **The cursor is stuck on one type** - `Input.CursorType` has no reader and no writer, so 21 of 22
  are unreachable. (4) **22 of 41 input bindings are never consumed**; 7 options are stored and ignored.
- **MEMORY AUDIT: 21 of ~95 checkable claims were STALE or FALSE, and all are now corrected.** Two
  patterns caused most of them - a **UI folder reorganisation** left dead paths in four notes, and **park
  work overtook the lobby notes**, so park entry, the park gadget and the park-side quiet stop were all
  built while five notes still called them pending. Also fixed: an **orphaned artifact** ("Leaving
  Veldrid") referenced by no memory file at all.
- **>>> ONE SWEEP FINDING I REFUTED MYSELF - do not propagate it. <<<** The inventory called
  `content/SimThemePark.iso` "a 497 MB blob **committed to the repo**". It is **not tracked**
  (`git ls-files --error-unmatch` fails) and `.git` is only **64 MB**, so it was never in history - it is
  an untracked local file. The real tracked-blob item is **`content/textures/test.png` at 13 MB**.
- **STILL OWED, and flagged rather than done:** the **README** is stale (line 182 "hundred and forty-three
  unit tests" vs 302; line 157 repeats the 210). *(An actual launch of fantasy/hallow/space was listed here
  too and Alexah has since DEFERRED it - see the decisions below.)*

**>>> ALEXAH ANSWERED ALL THREE OPEN DECISIONS, 2026-09-16. THESE ARE STANDING NOW - see
[[opentpw-format-goal]], which was rewritten to carry them. <<<**
1. **FIDELITY: recreate the original as closely as possible**, unless there is a *real genuine burden*.
   *"That's the entire goal of this project - get the original game running on modern computers, add in
   some fixes that are nice, and eventually a mod API once the game is 100% complete. The modding API is
   not currently the focus."* **This kills the "write our own path-follower" idea I had proposed** - the
   peep simulation gets DECODED, not approximated. Large-but-decodable is a cost, not a burden; the ride
   VM proved that over thirteen branches. **And I had justified that idea by citing
   [[ride-vm-may-be-replaced]], which was a MISREADING** - that permission is about abandoning inherited
   OpenTPW *scripting code*, not about deviating from the original's behaviour. Do not repeat it.
2. **UPSTREAM: fork for now, merge hoped for eventually.** *"Eventually, I'm hoping the maintainer will
   merge all of this into the upstream repo, but in the meantime, this is my own fork."* Keep the stack
   mergeable exactly as [[contribution-branch-layout]] already does. Upstream being dormant changes
   nothing about how branches are kept, and the hope of a merge is **not** consent to open a PR.
3. **SCOPE: 100% Lost Kingdom first.** *"No need to check other parks at the moment, let's 100% Lost
   Kingdom, first."* So fantasy/hallow/space verification is **deferred, not owed**, and the measure of
   progress is the shipped jungle park's **42-thing census** (further down this file, "THE PARK IS FULLY
   DECODED"): 11 placed catalogue objects, 3 sentinel fixed items (Gates/Lights/Bus, placed from
   `Standard.sam` not the save), 5 staff, 13 guests, and 10 singleton managers (models 9-19).

**>>> THE LOST KINGDOM CHECKLIST - what the park CONTAINS vs what we reproduce, 2026-09-16. <<<**
The 42-thing census names every thing by id and model. What it does NOT do - and this was overstated as
"every one identified" - is say what each **manager model** IS. Established so far:
- **model 2 does not exist** - its case prints `"Unknown thing model number loaded"`.
- **model 9 = strike system** (`mForceStrike`, `mStaffMemberPickedUp`, `mStrikeLevel[i]`). Nothing built.
- **model 10 = the object list** (`mNextObject`, via `FUN_004da960`). We draw the objects; the list itself
  is not modelled.
- **models 12 and 17 delegate to `FUN_0050b090` (`mMapChild`/`mMapParent`)** - the same map-tree base as
  model 10, so they are *placed* things, not managers. `FUN_00509bc0` sits beside
  `CTagSystem::ReceiveMessage` (`FUN_00509bf0`), so the message centre is the likely neighbourhood.
- **model 15 = weather** - confirmed twice, and the ONE manager we implement (`ParkWeather`).
- **>>> model 16 = THE ECONOMY, and its saved state is sitting in Lost Kingdom's file unread. <<<**
  `FUN_004cf920` (2,148 bytes) names `mAdmissionFee`, `mBalance`, `mBatchBalance`, `mLastBalance`,
  `mWithdrawalsEnabled`, `mTurnEnteredRed`, `mProfitThisYear`, and a whole `mLoans[loan].*` family
  (`loan_available`, `amount_available`, `APR_in_percent`, `repayment_period_in_months`,
  `monthly_repayment`, `loan_bought`). So "OpenTPW has no economy" is only half true: **the park's balance,
  admission fee and loans are IN THE SAVE and nothing reads them.** `ParkGadget.cs:63`'s "a park keeps no
  balance and no price" is standing in for exactly this.
- **model 18 = online persons**, refused on load (`"Should not be able to load online persons"`).
- **models 11, 13, 14 and 19 are UNIDENTIFIED.** Sizes only: model 11 allocates **0x16f0 = 5,872 bytes**,
  model 14 **0x146c = 5,228**. Model 13's case block is the largest (60 instructions).
**HOW TO NAME THE REST, and how NOT to.** The models 1-10 table was built from each reader's field-name
strings. Models 11/13/14/19 have thin readers (13-36 bytes) with **no CALLs at all**, so that trick does
not reach them. **Their case blocks pass a FUNCTION POINTER to `FUN_00401000`**, which is a 42-byte loop -
`FUN_00401000(ptr, size, int count, code *fn)` calls `(*fn)()` count times. **I misread those pointers as
descriptor tables and dumped `.text` as if it were data** - every "entry" was machine code (`90909090`
padding, `c3` RET) and one "field name" was a byte coincidence. New addendum in [[verify-every-ordering]].
The way in is the **callback** each block passes, not a table.
**WHAT WE REPRODUCE TODAY, against the census:** the 11 placed catalogue objects draw and are posed as
built, and their scripts bind and tick; the 3 fixed items place from `Standard.sam`; **the 13 guests walk,
animate, and face the way they are going** - routed across the real map, stepped on the thing engine's own
one-in-eight beat, their pictures advanced by the executable's own sprite scripts on a separate one-in-two
beat, and turned BY the camera rather than against it; **the 5 staff draw correctly dressed and facing but
are not simulated at all** - no walk, no animation, `script none` in the census, and that is by design
rather than an oversight; of the ~11 manager models we implement **one** (weather). Nothing reads the
economy, the strike system or the object list.
**>>> THIS PARAGRAPH SAID "never move" UNTIL 2026-09-17 <<<** - three lines above a block announcing that
they do, and 170 above the banner recording it. **That is the THIRD self-contradiction found in this file
in one session, and all three were caught by READING THE BLOCK BACK, not by any automated check** - a
substring search cannot tell a live claim from a quotation of a retired one. When a fact changes, hunt
down the older sentences that state it; appending a newer one leaves a reader free to believe either.

# >>> REASSESSMENT INPUTS MEASURED 2026-09-17, BEFORE ANY PLAN IS WRITTEN. <<<

Alexah asked for a reassessment, a reprioritisation and an updated plan. These are the figures the plan
will be built on, **measured this session rather than carried forward** - the same discipline they asked
for in so many words on 2026-09-16 (*"There's no point in making a plan and or reassessing project status
on false information."*). A code-subsystem survey was run separately; what is here is the git and grep
half, kept apart so the plan block references it instead of restating it.

- **HEALTH:** clean `--no-incremental` build **127 warnings / 0 errors**; suite **545 passed / 0 failed /
  0 skipped**. Both taken from the isolated per-commit worktree builds at the push, not from the in-tree
  build - so each of branch 87's five commits is independently known good (530/535/541/541/545).
- **THE STACK:** `main..alexah/87` is **297 commits, 282 files, +55,545 / -953**, **perfectly linear -
  zero merge commits** - spanning 2026-09-08 to 2026-09-17. (Was 260/232/+43,409 at the 2026-09-16
  reassessment, so this session's arc is roughly a fifth of the whole stack.)
- **BRANCHES: 87 local `alexah/*`, 87 on origin, and every one matches** - checked branch by branch, not
  by counting. Docs: **12 local, 12 on origin**, all pushed. **0 `refs/pull/*` on either origin.**
- **>>> THE ONE STRAY IS RESOLVED AND IT IS SAFE TO DROP: `wf-review-56` (`363346d`). <<<** It was the
  last local branch not on origin, and it is a **superseded duplicate**, not lost work. Its single unique
  commit is "Give a park the advisor the lobby already has" - and the SAME work is on the stack as
  **`1894084`**, which `git log --diff-filter=A` names as the commit that actually introduced
  `UI/Park/ParkLines.cs`; that file is present on the tip at 96 lines and `ParkFrontEnd` holds the advisor
  through a window pause. **Deleting a branch in Alexah's repo is theirs to decide, so it is recommended
  and not done.**
  **A METHOD NOTE, because the first instrument was the wrong one:** comparing `363346d`'s files against
  the tip printed "DIFFERS" for all three, which means nothing - that commit is based on `453e779`, so
  every file differs from a tip 297 commits later. "Content missing" and "content present plus 297
  commits of other change" look identical to that test. What settled it was asking which commit ADDED the
  file.
- **>>> PLAN ITEM 4 IS CONFIRMED LIVE, AND WORSE THAN RECORDED. <<<** `README.md:182` says "Sixty-two of
  the hundred and forty-three unit tests" - **143 against an actual 545**, and the 62/81 split is long
  dead too. **It is spelled out in words, which is why a search for digits came back empty** and once
  nearly had the claim reported as unfounded. `README.md:157` says of ride scripts "there is no parser...
  against a claimed total of 210 instructions" - **wrong twice**: `VM/RideScript.cs` is 1,783 lines, and
  the `Opcode` enum has **106** members, counted. `VM/RideVM.cs:42` still hardcodes `totalCount = 210`.
  **>>> AND THE CONSEQUENCE I FIRST WROTE HERE WAS WRONG, CAUGHT BY THE SURVEY'S OWN CRITIC: the game does
  NOT log a wrong percentage every run - IT CANNOT LOG ONE AT ALL. <<<** `RideVM.cs:45`'s `Log.Info` is
  reachable only through the constructor, whose sole construction site is `World/Ride.cs:33`, and
  **`new Ride(` has zero hits in the tree** - the whole type is unreachable. I asserted a RUNTIME
  consequence from a STATIC read without checking reachability, which is the error this very file
  catalogues a dozen times over, committed while writing up a session about it. The live runtime is
  `VM/RideScript.cs` (**63 of 106** opcodes as of 2026-09-18; this said 57 before the bounce family), a
different type; `RideVM` and `VM/Handlers/*` are dead, as
  is `World/Ride.cs`. `Formats/Script/RideScriptFile.cs:97-99` ("There is no ride runtime in the tree") is
  stale for the same reason and must be corrected alongside `README.md:157`.

# >>> ALEXAH PLAYED IT AND FOUND THREE THINGS, 2026-09-17. READ THIS BEFORE THE PLAN. <<<

**Ten seconds of looking beat 560 green tests, for the third time in this project.** Reported verbatim:
(1) *"The staff still aren't navigating."* (2) *"The park gate isn't open or able to be opened currently,
kids/guests walk right into the mesh, disappear, and stop."* (3) *"Guests stopped at the admission gates
seem to animate their walking cycle while standing still."*

**(3) WAS A REGRESSION I CAUSED, AND IT IS FIXED - `5746db0`.** `Peep.SetState` recorded the animation in
`Animation`; `ParkPeople.Apply` reads **`NextAnimation`**. Two fields, nothing between them, so arriving
queued nothing and the sprite kept its walk script for ever. `FUN_00501db0` **queues** via `FUN_004217f0`,
which is a bare `*(person + 4) = value`. **And the same commit that broke it took credit for retiring the
line that had been covering it** - `WalkOn`'s stand-on-arrival. New rule sixty-two.
**THE TEST THAT SHOULD HAVE CAUGHT IT ASSERTED `Peep.AnimationFor( peep.State )`** - a lookup over an enum
that never touches a sprite - **in a test named for guests standing rather than striding on the spot.**
That is the THIRD time in one day I wrote an assertion that could pass with the feature deleted, the first
two being rule fifty-eight's own subject. Rewritten to read `SpriteFor( id ).IsOn( Stand )` through a real
`ParkPeople`, with a frame-STABILITY guard rather than an unmeasured frame-0 literal.

**(2a) THE GATE IS FIXED - `e692790`, AND IT NEEDED P2 TO EXIST.** `Gates.RSE` idles on a dispatch loop
reading `VAR_COMMAND`; nothing in the tree could write a script variable from outside. `FUN_00519ef0` looks
the gate up from `mParkGates` and writes **variable 0**: **1 opens, 2 shuts, 0 clears** - read off the
DISASSEMBLY, because those call sites push an argument that survives one call and is consumed by the next,
so the decompile renders the argument lists wrongly and gives "1 opens, 0 shuts", **which is what I had
written down before checking**. `RideScript.Set` resolves by the script's own declared name and **reports a
miss**, because a write that goes nowhere looks exactly like a gate nobody commanded.

**>>> (2b) IS EXPLAINED, MEASURED, AND NOT FIXED - DO NOT LET THE GATE COMMIT IMPLY OTHERWISE. <<<**
Measured (`ea63731`, and `~/.cache/tpw-harnesses/gate-approach-measured.txt`): **all seven `Entering`
guests target the EXACT CENTRE of one of the gate's two entrance cells** - (47.50,17.50) or (48.50,17.50)
against `FixedItemInfo.EntranceA/B` at (47,17) and (48,17). The half is the pathfinder's own cell centre,
so they are not overshooting and no route pushes them in: **the middle of the gateway is where the file was
sending them.** The five still heading for the gate stop at row 13, four cells short, and their targets are
*not* cell centres. **So opening the gate changes what it LOOKS like and moves nobody on** - arriving puts
them in `Deciding`, the unbuilt hub that would choose somewhere inside the park. **I twice wrote this
mechanism down as a "reading" and twice deferred measuring it, and nearly told Alexah the gate fix cured
it.**

**(1) STAFF ARE UNBUILT, NOT BROKEN - and here is the actual size of it.** `ParkPeople.PeepsIn` filters on
`Guest != null`, so only model 1 is simulated at all. `FUN_0050b360` switches on the thing's model byte and
gives **every** person-kind the same shape as a guest, a needs call then a behaviour call: model 3
`FUN_004e0b90`/`FUN_004e0e00`, 4 `thunk_FUN_00505490`/`FUN_004da490`, 5 `FUN_004d7060`/`FUN_004d73c0`,
6 `FUN_004d4660`/`FUN_004d4810`, 7 `FUN_004d6360`/`FUN_004d6410`, 8 `FUN_00502960`/`FUN_005029f0`.
**>>> MEASURED SIZES, AND THEY ARE MUCH SMALLER THAN THE GUEST'S. THIS PARAGRAPH FIRST SAID "five or six
INDEPENDENT state machines, each structurally parallel to the guest one", WHICH IS TRUE OF THE SHAPE AND
OVERSTATES THE SIZE - written before the measurement, which is rule sixty catching me on a sentence I had
just typed. <<<** Instructions per function: the guest's own behaviour switch is **345** (42 calls, and the
22-case jump table), and its needs **259** - **by far the biggest pair in the table**. The five staff kinds
run **68, 128, 108, 127 and 154** for models 4, 5, 6, 7 and 8, with needs of **22, 52, 36, 36 and 44**.
**All five staff behaviour switches together come to about 585 instructions - under twice the single guest
one.** So staff are five separate machines rather than one feature, but each is a fraction of the size of
the thing already half-built, and "five state machines" should not be read as "five times the work".
(Model 3 is the odd one: 165 instructions of needs against 22 of behaviour, the opposite shape.)



*(Terminator MOVED here, seventh trim, 2026-09-18: the block had reached 720 against a 600-line read. The
five live ride-operation constraints were hoisted above it first; nothing below was relocated.)*

**>>> RIDE OPERATION IS SCRIPT-DRIVEN, AND ADMIT / DISMISS / BOARDING ARE ALL BUILT NOW (`ae4fbe4`,
`9f2e9b4`). This header said "THE ADMIT IS NOT" - it is. Compacted, sixth trim. <<<**
[[contribution-branch-layout]] carries the whole decode under branch 93: the tick map (`FUN_0050b360`
case 3 -> `FUN_004e0b90` + `FUN_004e0e00` on `+0x19c`), the six script-variable names confirmed against
the engine's indices, and the admit/dismiss mechanics. **What must stay in front of anyone resuming:**
- **REACH SCRIPT VARIABLES BY NAME, NEVER BY INDEX** - a ride archive's COMPANION scripts (`child.RSE`,
  `effects.RSE`, `EventMap.RSE`) declare none of the common twelve, so `RideVariables` is a name list and
  not a layout. `ParkRides` has always done this for the gate.
- **OBJECT FIELDS:** `+0x24` script handle, `+0x19c` state, `+0x3c` `mFirstInQ`, `+0x6c` person being
  loaded (now `ParkState.PersonBeingLoaded`), `+0x48` wear, `+0x20` model slot.
- **>>> `+0x68` IS `mCanLoad`, AND IT IS SERIALISED. THIS FILE CALLED IT "UNKNOWN" IN THREE PLACES AND
  `ParkRideOperation.cs` SAYS "has not been established" - ALL OF THOSE ARE NOW STALE. <<<** It is the one
  refusal both `AdmitPerson` and `Invite` used to decline to reproduce - **BOTH NOW DO, `2670e70`.**
  **`mExitPos` IS SERIALISED TOO**, and the dismissed guest's walk is built on it in that same commit.
  **HOW, and it is the whole-field-list method again rather than a name search:** `FUN_004db7d0` is the
  catalogue-object serialiser and its WRITE path states every size explicitly. Walking it from
  `mEntryPos`, with the sizes it declares:
  `mEntryPos` **206** (2) -> `mNext` **208** (2) -> `mAssignedStaffMember` **210** (2) ->
  `mBackOfQueue` **212** (2) -> **`mCanLoad` 214 (FOUR bytes)** -> **`mExitPos` 218 (2)** ->
  `mFirstInQ` **220** (2).
  **>>> THE EVIDENCE IS THAT IT CLOSES BETWEEN TWO ANCHORS THIS FILE ALREADY MEASURED BY A DIFFERENT
  ROUTE. <<<** `mBackOfQueue` 212 and `mFirstInQ` 220 came from the ring arithmetic; the only way to span
  the eight bytes between them is 4 then 2 then the 2 of `mFirstInQ`, in exactly the order the serialiser
  calls them.
  **>>> AND THEN I FOUND THE TREE ALREADY READS BOTH, AT EXACTLY THOSE OFFSETS. I HAD WRITTEN "DERIVED,
  NOT YET READ OUT OF THE PARK - measure before building on it", AND THAT WAS FALSE WHEN I WROTE IT. <<<**
  `ParkWorld.CatalogueObject` declares `CanLoad` and `ExitPos` (and the reader takes `ExitPos` from
  `start + 218`, commented "packed like mEntryPos"). So the serialiser walk **confirmed** the offsets by a
  second independent route rather than discovering them - which is worth more as evidence and less as news.
  **THE REAL FINDING IS A CONTRADICTION INSIDE OUR OWN REPO, AND IT IS WORSE THAN A NAME:**
  **`ParkRideChoice.cs:77` ALREADY CONSUMES IT** - `if ( item.CanLoad == 0 ) return false;`, commented
  "the original tests it for non-zero rather than for a particular value, and so does this", and pinned by
  `ParkRideChoiceTests.cs:99`. So a file in the SAME FOLDER uses the field correctly by name while
  `ParkRideOperation.cs` says of `+0x68` that "what that field is has not been established", and this file
  called it UNKNOWN in three places. **The decode was never missing. The doc comments were stale, and my
  "three places call it unknown" framing was itself the stale claim rather than a discovery.**
  **A SAFETY WORRY THIS ALSO RETIRES:** I feared adding the refusal might freeze every boarding if
  `mCanLoad` were nought park-wide. It cannot - `CanBeOffered` already refuses on it, and guests do choose
  the Belly Bounce and join its queue in `ParkRideJoinTests` against a REAL park, so it is already non-zero
  there. That is inference from existing behaviour, so confirm it, but the risk is small.
  **`mExitPos` WAS THE ACTUAL GAP - read at 218, consumed nowhere. IT IS BUILT NOW (`2670e70`):**
  `ExitCellX`/`ExitCellY` match `EntryCellX/Y`, both admit paths refuse on `mCanLoad`, and `Dismiss` walks
  the guest to the exit. **This paragraph described that as work to do; it is evidence now, not a to-do.**
  **>>> MEASURED 2026-09-18, harness `~/.cache/tpw-harnesses/park-exits` (C#, reads the .TPWI straight off
  disk - `ParkWorld` needs only a stream, so no test-only `GameData` helper). <<<**
- **`mCanLoad` IS 1 ON ALL FOURTEEN OBJECTS** - one distinct value, nought nowhere. So the refusal cannot
  freeze boarding, which was the risk; **but it can never FIRE in this park either**, which is P4's
  green-feature-that-changes-nothing trap. The difference from P4's litter arm is that `ParkRideChoice`
  ALREADY carries the identical refusal, so reproducing it in the admit paths is consistency with a
  decision already taken rather than a new speculative layer. **Decide it deliberately; do not slide past it.**
- **>>> THE 5-PACKED / 0-PLAIN COUNT IS *NOT* INDEPENDENT EVIDENCE FOR THE EXIT DECODE, AND I NEARLY
  REPORTED IT AS IF IT WERE. <<<** `mExitPos` **equals `mEntryPos` on TEN of the ELEVEN** placed objects,
  so that count is mostly P4's entry-cell result restated through a field holding the same number - a
  verified table vouching for its unverified neighbour, which this file catalogues as a standing rule.
- **THE SOLE DISCRIMINATING OBJECT IS THING 13, THE BELLY BOUNCE** - the one ride, and the one that matters
  for dismissal. Entry **2997 -> (52,23)**; exit **3381 -> (52,26)**. Packed lands on a cell with **16
  connected edges**, plain on (53,26) with **NONE**. That single object confirms the decode, and it does it
  properly. **The feature is not inert:** entry and exit are three cells apart on opposite sides of the ride,
  so dismissing to the exit is a visible difference rather than a tidy no-op.
- **PRICES, WHICH RESHAPE THE SPENDING ARC:** Drinks Shop (thing 16, cat 1203) **30**; Jungle Spray sideshow
  (thing 14, cat 1303) **20**; **Belly Bounce (thing 13) ZERO**; `mTotalTakings` nought everywhere.
  The charge is gated on `price != 0`, **so it would never fire for the park's only RIDE** - spending's
  payoff is the shop and the sideshow. Decide that before building it, exactly as P4's litter arm taught.
- **CROSS-CHECK:** the harness reproduces P4's rest area exactly (thing 20, entry 1979 -> (58,15), 64
  edges), so it agrees with `ParkRestAreaTests` rather than measuring through a different lens.
  **Two independent corroborations fell out of the same listing:** `mPricePerUse` is `+0x194` (and the
  serialiser CLAMPS it to 0..500), which is exactly what the charge reads; and `mTotalTakings` is `+0x180`,
  exactly what `FUN_004e16b0` credits. The spending decode above is confirmed from a second direction.
- **>>> NOW CONFIRMED, 2026-09-18: our `PeepState` 15 `OnRide` / 16 `Riding` ARE THE WRONG WAY ROUND. <<<**
  This said "SUSPECTED, NOT PROVEN" and asked for a check before renaming; the check is done.
  `FUN_005014e0` is **ExitRide** (its own line: "Person %d: ExitRide, leaving rid...") and it ends
  `FUN_00501db0(0xf)` - **state 15 is set on LEAVING**, while 16 is set on admission. So 15 is the
  walk-away-from-the-ride state and 16 is the actually-aboard one, and our names say the reverse.
  **THE BEHAVIOUR IS RIGHT EITHER WAY** - `Dismiss` already sets 15 on letting a guest off, matching the
  original exactly, and the walking classification holds (15 walks, 16 does not). **Only the NAMES are
  wrong.** A rename touches a public enum across many files, so it is a deliberate separate change and
  **not** something to slip into a ride-operation commit.
- **AN EIGHTH FLAGS BIT: `0x80`**, tested in the pre-tick. `FUN_004e0e60` is SetState: for 1/2/4 it
  flushes the admit step, logs "Closing...", clears `+0x68` and `+0x6c` ("person being loaded was %d"),
  sets script var 6 and raises a message. **`FUN_004e1220` IS NOW READ AND BUILT** as
  `ParkRideOperation.Invite` (`f579ba8`) - it was the head of the boarding chain, and this line listing it
  as unread outlived that by a session. **`FUN_004dedf0` IS NOW READ TOO** - it answers a point on a thing,
  the exit at `+0x38` for a non-zero argument and the stand point at `+0x36` for nought, both PACKED cells,
  with the sub-cell offset coming from the item descriptor's floats (`+0xdc`/`+0xe0`, `+0xd4`/`+0xd8`).
  **STILL UNREAD:** `FUN_00454550`.
- **>>> THE ADMIT TRIGGER IS SETTLED, AND MY RECORDED HYPOTHESIS WAS WRONG. <<<** I wrote here that
  `VAR_LETMEON` "holds whoever was last let on". **It does not.** Read out of `Bouncy.RSE` itself:
  `88 CRIT_LOCK / 89 TEST VAR_LETMEON / 91 BRANCH_Z ->99 / 93 BOUNCE VAR_LETMEON, VAR_DURATION /
  96 COPY VAR_LETMEON, 0 / 99 CRIT_UNLOCK`. **It is an engine->script INBOX**: normally nought, the engine
  puts a person handle in it, the script rides them for `VAR_DURATION` and **zeroes the slot to
  acknowledge**, under a critical section. That is why `FUN_004e0450` admits when it DIFFERS from
  `mFirstInQ` - the slot is empty while the head waits. Just before it: `BOUNCING VAR_TEMP` (riders now)
  then `CMP VAR_CAPACITY, VAR_TEMP` - the capacity gate. Branch polarity pinned from our own interpreter
  (`RideScript.cs`: `TEST` loads `Result`; `BRANCH_Z` jumps on nought).
- **>>> THE DISMISS SIDE IS RESOLVED: `VAR_LETMEOFF` IS AN *OUTBOX*, WRITTEN BY THE SCRIPT. <<<**
  `UNBOUNCE`/`FORCEUNBOUNCE` **WRITE their operand** - proven from the interpreter, not inferred. The
  dispatcher is `FUN_00551cb0`: operand kind `0x80000000`, bounds `0x6a` = **106 opcodes**, jumping through
  a handler table at **`0x005567d8`**. Opcode numbers come from the {name, arity} descriptor table based at
  **`0x00765280`** (NOP = 0, rows 8 bytes): BOUNCE **72**, UNBOUNCE **73**, FORCEUNBOUNCE **74**,
  BOUNCING **75**. Both unbounce handlers fall into a shared tail at **`0x005558e0`** that does
  `AND 0xff000000 / CMP 0x40000000 / XOR / MOV [vars + idx*4], EAX` - the **same store** `BOUNCING`
  (a known writer) ends with, and the opposite of `BOUNCE`, which **loads** from the variable.
  **So the handshake is producer/consumer in BOTH directions:** engine fills `VAR_LETMEON` and the script
  clears it; **script fills `VAR_LETMEOFF`** (with whoever came off, or nought) and the **engine** clears
  it after walking them to the exit. That is why the script skips while the slot is non-zero - the engine
  has not collected them yet - and why the polarity looked inverted.
- **>>> THE WRITE SITE IS `FUN_004e0900` = AdmitPerson ("Object %d: AdmitPerson - person b..."). <<<**
  It **refuses** if the ride's `+0x19c` state is **1** or **4**, or `+0x68` is nought ("cannot admit");
  asserts the person matches `+0x6c` ("admitting wrong person - check d..."); clears `+0x6c`; and then
  writes the handle into `VAR_LETMEON` **only if that slot currently reads NOUGHT**, returning 1 - else it
  refuses. **So clearing the slot is how the script says it is ready for the next rider.** Full cycle:
  `+0x6c` nominates -> `AdmitPerson` fills var 0 when empty -> script `TEST`/`BOUNCE`/`COPY 0` consumes ->
  `FUN_004e0450` sees empty-vs-head and completes (queue removal, state **16**).
  **`FUN_004e0db0` sets `VAR_BREAKSTAT` = 1 and `FUN_004e03f0` sets it 0** - the breakdown request/clear
  pair, confirming var 4 is engine->script too. **NOTHING ABOUT ADMIT IS NOW UNKNOWN.** **The listing harness that answered this is `~/.cache/tpw-harnesses/rse-listing`** (dotnet,
  references `OpenTPW.Files`; `rselisting <file.RSE> [VAR_NAME]`), with clean members dumped by
  `wadcat --dump` - **never `--cat`, which concatenates several and was what produced a false finding.** **>>> THE "DO NOT BUILD AN ADMIT ON IT UNTIL IT IS" WARNING THAT SAT HERE IS RETIRED - THE TRIGGER WAS
  SETTLED AND THE ADMIT SHIPPED IN `ae4fbe4`. <<<** It survived as an orphaned fragment of an older
  paragraph and read as a live instruction not to build something already built, which is the single worst
  thing this file can do to whoever reads it next. `FUN_00500870` (the guest
  side) re-checks the same condition, removes them from the queue, asserts `mQNext` is nought and sets
  state **16**; the dismiss side is clear by comparison (var 1, asserted in state 16, exit cell, state 15).

**>>> P5's COMMITS 2-4 (`2226fa5` 633, `ae85a22` 637, `ff1d344` 641) - COMPACTED OUT OF THIS BLOCK,
2026-09-17. <<<** [[contribution-branch-layout]] carries all three verbatim under branch 93 and is kept
current. **They had also gone STALE here, which is why they went rather than merely being long:** three
separate paragraphs still said the filter, the scorer and the chooser were "STILL TO BUILD" / "DELIBERATELY
NOT BUILT" and that the descriptor type was unread - every one of those is false since `ae85a22`,
`291e158`, `618d270` and `a6315a6`. Two facts are kept because they live nowhere else: **ride age =
(now - `mBuiltWhen`) / 864,000,000,000** (FILETIME units) against `DecisionVariable1` = 7, and the
**key-name table avenue IS closed** - the dword before `WhichUIType` and before `WhichTrackType` is **6 in
both**, so that table tags keys rather than locating them.

*(The `DEFERRED` paragraph that stood here was MOVED UP on 2026-09-18 - the seventh trim - to sit
immediately after the spending block, so the live block ends inside the 600-line read this file
prescribes. Its content is unchanged; only its position moved, which is what demotes everything from here
down to history without relocating a single line of it. Do not re-add a terminator here.)*

**Everything in the previous plan (P1-P4) is DONE and PUSHED** - `origin/alexah/88-what-the-header-already-knew`
= `ea63731`, nine commits, all built ALONE in throwaway worktrees at 127/0 (546, 555, 555, 555, 558, 560,
560, 561, 562). Docs pushed too (`44ba114`). **The sixth push yes is spent; the next push needs a new one.**
**Measured this session, not carried:** 306 commits ahead of `main`, 287 files, **+56,798 / -954**, **zero
merge commits**, 88 branches all on origin, **562 tests / 127 warnings / 0 errors**, 329 C# files and
64,987 lines, 77 test files. One stray remains: local `wf-review-56` (`363346d`), the superseded duplicate
- **Alexah's to delete, recommended, not done.**

**>>> P1 IS BUILT AND *PUSHED*, 2026-09-17 - `origin/alexah/89-what-the-park-charges` = `8278871`, TWO
COMMITS. THIS PARAGRAPH SAID "COMMITTED, NOT PUSHED" UNTIL THE PUSH LANDED. <<<**
Pushed on a **SEVENTH fresh yes** ("Yes you may push and continue your workflow until completion"), one
named refspec, no force, **no PR** - GitHub offered
`pull/new/alexah/89-what-the-park-charges` and it was not used. **THAT YES IS SPENT**
([[ask-before-github]]). Before-checks were hard assertions that would have aborted before any network
write; verified after: `local == remote`, **0 ahead**, **0 `refs/pull/*` on origin**, `main` still
`453e779b…` on local/origin/upstream, branch 88 still `ea63731e…`. The docs commit went with it - see
[[opentpw-fileformats-docs]].
`0db6741` "Read what a park charges and whether each guest has paid" (**569 tests** alone) and `8278871`
"Let the guests at the ticket booths pay their way in" (**579 tests** alone). Both built AND tested alone
in throwaway worktrees at **127 warnings / 0 errors**; file sets deliberately DISJOINT, because
`ParkWorld.cs` and `Level.cs` each carry two stories and non-interactive git cannot stage half a file.
`save/` byte-identical either side of every run (`faa07273...` throughout).
**A commit-message error the build-alone gate caught:** both messages first claimed "579 tests"; commit 1
alone is **569**. A count in a message is a claim about that commit STANDING ALONE. Corrected by
`reset --soft HEAD~2` + re-staging (never `checkout --`).
- **WHAT IS BUILT:** `ParkWorld.EconomyState` (model 16, 300-byte record), `GuestState.PaidAdmission` /
  `ParkOpeningWait` (file **+460** / **+464**), `ParkBalance`'s third easy-mode layer, `ParkAdmission`
  (`FUN_004ff5b0`), `ParkRides.GateStatus` (the gate's own `VAR_STATUS`), and `PeepBehaviour`'s
  **`JudgingTheFee` in full** plus **two of `WaitingForOpening`'s three arms**.
- **>>> THE PARK IS AN EASY-MODE PARK, AND ITS OWN SAVE PROVES IT - this was NOT a preference. <<<** All
  eight saved loan APRs are **0**, matching `jungle/Easy_Standard.sam` exactly and the global file's
  20/20/20/20/23/22/18/21 **nowhere**. The original reads that third file only when its mode word
  `DAT_00fb3b7c == 2`, building the name as `sprintf("%s\%s%s", theme, "Easy_", "Standard.sam")`. It
  changes **33** keys and introduces **none** (68 keys, all already in the global file - measured with a
  proper join after my first `grep`-based diff silently dropped every subscripted key, because `[0]` is a
  character class).
- **AND IT DECIDES WHAT ALEXAH SEES.** Fee is **25**, ideal price **20**. Standard `AveragePriceMultiplier`
  1.25 puts the line at exactly 25, so `25 <= 25` is TRUE and the guests find it expensive and leave;
  easy's 1.5 puts it at 30 and they pay. **I nearly deferred this question as "noted, not chased" and that
  was wrong** - measuring the fee is what revealed it mattered.
- **>>> WHAT IS DELIBERATELY NOT BUILT, AND IT IS A FIELD RATHER THAN AN OMISSION. <<<**
  `WaitingForOpening`'s **paid** arm waits until the cell underfoot NAMES THAT GUEST: a short at
  **`+0x24` of the cell's RUNTIME record** - read first-hand in `FUN_004ff7f0` 2026-09-18 as
  `[DAT_008023a0 - 0x20 + packedCell * 0x44]`, and compared against the thing's own id (`FUN_0050b350`).
  **>>> TWO THINGS THIS BULLET SAID ARE NOW FALSE, CORRECTED 2026-09-18. <<<** (a) "the offset cannot be
  translated, and nothing here keeps a mutable map cell" - **P2 built mutable cells**, and `+0x24` is
  almost certainly `mOccupant` (file 50), which `ParkState.RuntimeCell` ALREADY carries. (b) **"The next
  lead is `FUN_004d8480`" IS WRONG and cost a decode**: that is the **`RegionFX` stamper**, as
  ***this same file already says at line ~2798*** - a correct fact and a contradicting lead 2,000 lines
  apart, only the first 600 of which are ever read. **The array is `world + 0x2d8`, stride `0x44`, indexed
  `(packedId - 1)`; `FUN_00515660` is its only writer.** Known fields: `+8` type, `+0xc`/`+0x22` two edge
  sets, `+0xd` direction, `+0x24` the occupant. **Three probes came back NEGATIVE - do not repeat them:**
  `FUN_004dd0a0` destroys a thing, `FUN_0050afe0` constructs one, `FUN_004fa990` reads a CELL TYPE.
  **>>> SOLVED 2026-09-18: `+0x24` IS THE HEAD OF THE CELL'S THING LIST, NOT A RESERVATION. <<<**
  `FUN_004d91f0` (put a thing on a cell) ends `cell[0x24] = thingId`; `FUN_004d9280` (take it off)
  repairs it. The links live on the THING - `+8` prev, `+10` next - and the list is LIFO. So the paid arm
  asks **"am I the first thing standing on this cell?"**, which IS the one-guest-at-a-time gate. Maintained
  on exactly three events: `FUN_0050b6a0` (a thing moves cell), `FUN_0050afe0` (created), `FUN_0050b980`
  (destroyed). **We already have the field**: `MapCell.Occupant`, file 50, and `ParkDecodedRecordTests:128`
  + `ParkStateTests:91` both assert it holds the thing id of the object on that cell - it is simply never
  MAINTAINED. (`FUN_00501db0` was my lead for an hour and writes no cell field at all.)
- **A PROPERTY OF THE DATA WORTH KEEPING:** at an ideal price equal to `MinimumEntryFee`, the **cheap band
  is unreachable** - the cheap line (0.75 x 20 = 15) falls below the floor of 20 and the early-out
  swallows it. Nobody can feel they got a bargain until the park is worth more than the floor. Found by
  two of my own tests failing.
- **AN OPEN QUESTION, REASONED BUT NOT SETTLED:** `ReadGuest` calls file **+529** `Illness`. The named
  guest fields are strictly alphabetical and each unnamed float sits where its name WOULD sort; +529 sits
  after `mToilet`, which "mIllness" cannot. With `PeepInfo.VomitCapacity` in the balance file, +529 is
  likelier **vomit**. Off P1's path - recorded, not changed.
- **STILL OWED FROM P1:** the HUD's cash/price readouts, which now have real data to show
  (`Economy.Balance + PeepBehaviour.Takings`, and `Economy.AdmissionFee`).

**>>> P2 IS BUILT AND *PUSHED*, 2026-09-17 - `origin/alexah/90-what-a-guest-does-next` = `ed38b4c`, ONE
COMMIT. THIS PARAGRAPH SAID "COMMITTED, NOT PUSHED" UNTIL THE PUSH LANDED - THE THIRD TIME THIS SESSION A
PUSH MADE MY OWN RECORD STALE WITHIN A MINUTE. <<<**
Pushed on an **EIGHTH fresh yes** ("Yes you may push"), one named refspec, no force, **no PR** - GitHub
offered `pull/new/alexah/90-what-a-guest-does-next` and it was not used. **THAT YES IS SPENT**
([[ask-before-github]]). Before-checks were hard assertions that would have aborted before any network
write; verified after: `local == remote`, **0 ahead**, **0 `refs/pull/*` on origin**, `main` still
`453e779b…` on local/origin/upstream, branch 89 still `8278871`. **The whole stack is now on origin: 90
local `alexah/*` branches and 90 on origin.**
Built AND tested ALONE in a throwaway worktree at **127 warnings / 0 errors, 584 tests, 0 skipped** -
and because it is one commit on top of an already-pushed branch, "alone" and "the tree" coincide, so the
message's figures were sound rather than lucky. 5 files, +484 / -8. `save/` byte-identical.
- **`FUN_004fec90` IS ONE ROLL TAKEN ONCE AT THE TOP**, not a cascade: `rand % 3` gives wander / be offered
  a ride / do nothing. **The wander arm is BUILT; the ride arm is DEFERRED on a whole subsystem**, which is
  the honest reason. `FUN_004fcb10` walks the object list from `mFirstObject` and scores each candidate
  with **`FUN_004fcc30`**, which wants the queue cell, queue length, price, indoor-in-the-rain, newness,
  the guest's preferred excitement against the ride's, thirst/hunger relief and the guest's **own last four
  rides** (`mPreviousRides[4]`, dividing the score by 5/4/3/2), all weighted by the seven
  `PeepInfo.DecisionVar…Weight` keys - and every candidate must be **open for business with queue room**.
  Nothing here operates a ride. It also requires a score **above 9** and proves reachability by calling
  SetDest before accepting a candidate.
- **`Wandering` is INLINE - case 7 of `FUN_005019f0`, five lines.** Arrive or give up (verdicts 0 and 2,
  the pair `SteppingUpQueue` already treats alike) → **one-in-four** chance of another random destination
  and staying, else back to `Deciding`. That loop is what keeps a park moving.
- **`FUN_004fa530` (SetDest) CONFIRMED `WaypointCentre` to the bit:** cell → `(cell & 0x7f) << 8 | 0x80`,
  then `<< 8` → `cell * One + One/2`. So the measured (47.50,17.50) targets are right for the engine's own
  reason. It also revealed **`+0x198`, a "stranded" stamp** - a cooldown that refuses new routing for a
  while after a guest gives up. Not reproduced (no thought system, no stamp field).
- **>>> TWO THINGS REPRODUCED RATHER THAN TIDIED. <<<** (1) Candidate cells are tested in the engine's bit
  order **0x10, 0x04, 0x01, 0x40**, which `CellEdge.BitFor` - derived separately, from the map - gives to
  **North, West, South, East**; that ordering is exactly what makes the original's "do not turn back" test
  `(slot + 2) & 3` correct, so two independently-derived mappings agree. (2) **A wander destination is a
  random point INSIDE the cell, not its centre**: `rand & 0x7f` clamped to **5..123** of 256 sub-units, so
  every wander target sits in the **near half** of its cell. A centre would have been tidier and is not
  what the engine does; the tests assert the band AND that no target lands on a centre.
- **AN OPEN QUESTION THAT RESOLVED IN THE CODE'S FAVOUR, and it was a real one:** whether the gateway cells
  are walkable at all (they carry the park-entrance attribute, 8). They are - all seven guests left the cell
  centre the save sent them to. The test asserts **POSITION, not state**, because a guest handed a
  destination who never moved would satisfy any state check.
- **STILL NOT BUILT after P2:** the ride arm above; the need-driven arms at the top of `FUN_004fec90`
  (they fire on a need this project does not score); and the two conditions on the leave path that read
  **unidentified** fields (`+0x1bc` and a value behind a float conversion) - only the shut-park half is
  reproduced, because guessing the rest would be inventing behaviour.

**>>> P3, P4 AND P5 ARE ALL DONE AND *PUSHED*, 2026-09-17 EVENING - `origin/alexah/91-what-the-staff-were-doing`
= `db935a4`, TEN COMMITS. THIS PARAGRAPH SAID "NOT PUSHED" UNTIL THE PUSH LANDED. <<<**
Pushed on a **NINTH fresh yes** ("Yes you can push. I trust your judgment on the two questions"), one
named refspec, no force, **no PR** - GitHub offered `pull/new/alexah/91-what-the-staff-were-doing` and it
was not used. **THAT YES IS SPENT** ([[ask-before-github]]). Before-checks were hard assertions that would
have aborted before any network write; verified after: `local == remote`, **0 ahead**, **0 `refs/pull/*`
on origin**, `main` still `453e779`, branch 90 still `ed38b4c`. The docs commit went with it - see
[[opentpw-fileformats-docs]].
`eea6f8f` staff reader (**593** alone), `9b69f15` staff behaviour (**602**), `6f68a27` logger quiet flag,
`246cc0f` Graphics.Quad + Panel per-frame work, `2f38ce3` Terrain.cs deleted, `19a4d83` Localization guard,
`f59d71c` InputButton note, `8eb0caf` the seventh guest float written down, `edd33d5` the dead flycam
deleted, `db935a4` the `Illness` field renamed `Vomit` (**602** each). **ALL TEN BUILT AND TESTED ALONE IN
THROWAWAY WORKTREES** -
127 warnings / 0 errors each, and every message's count matches its commit standing alone. `save/`
byte-identical (`faa07273...`) throughout. Measured after: **319 commits ahead of `main`, 0 merges, 91
local `alexah/*`, 298 files, +60,359 / -1,041, 397 C# files, 0 `refs/pull/*` on origin.**

**>>> ALEXAH HANDED ME BOTH OPEN JUDGMENT CALLS AND BOTH ARE NOW SETTLED, 2026-09-17. <<<** Their words:
*"I trust your judgment on the two questions."*
1. **`ParkCameraMode.cs` IS DELETED (`edd33d5`).** It asked in its own comment to be kept "so the flycam
   is not written twice" - written when parks did not exist. **That reason has been served**: the real
   park cameras are built and the same comment says not to use this as their starting point. What was
   left is a dev flycam with **zero call sites**, which is tooling ([[no-tooling-in-the-codebase]]).
   **THE CREF WENT IN THE SAME COMMIT AND THAT MATTERED** - `ParkOrbitCameraMode.cs:6` held a
   `<see cref>` to it, so deleting alone would have been **CS1574** and moved the 127 baseline.
2. **`Illness` IS RENAMED `Vomit` (`db935a4`).** The argument is **structural, not statistical**: the
   guest block is strictly alphabetical, `mIllness` would sort between `mHunger` (+426) and `mLastPosX`
   (+430) which are **adjacent with no gap**, and it is nowhere in the person base either. The middle of
   the three trailing floats (+525) is independently known to be `mToilet` - the only one that varies -
   which **brackets +529 on both sides**. `PeepInfo.VomitCapacity` exists; no illness LEVEL key does.
   **`RegionFX[i].Illness` and `DecisionVarIllnessWeight` still say illness and are left alone**, because
   those are the balance file's own key names for the same meter. **+521 is STILL not named** - correcting
   a demonstrably wrong name is a different act from inventing a plausible one.

**P3. STAFF - DONE, AND THE SHAPE WAS THE FINDING.** "Five kinds of staff" is not five state machines:
every kind's per-turn function opens with the same switch on `mState` and answers states **0-7 through the
same three shared handlers**, whose own diagnostics name them `CStaff::`. One machine, five small
extensions.
- **THE 105-BYTE `CStaff` BLOCK IS DECODED** (`FUN_00504de0`), at file **+398**, same place a guest's own
  block starts. Fields in file order: `mCurrentPayGrade` 4, **mHappiness** 4 (unnamed), `mJobsDone` 4,
  `mName[0..32]` 66, `mPatrolRegionBL` 2, `mPatrolRegionTR` 2, `mPercentageThroughGrade` 1, `mRestArea` 2,
  `mState` 4, `mTimeStartedIdling` 4, `mTimeHired` 8, **mTiredness** 4 (unnamed). **Sums to exactly 105**,
  and `8 + 390 + 105 = 503` leaves 8/10/6/8/6 for the five kinds - which is exactly what
  `RecordSizes` (derived years earlier by emulation, a different route) says. **Five independent
  agreements.** Per-kind extras decoded and deliberately unread: `mDurationOfRepair`/`mObjectToRepair`
  (mechanic), `mTargetLitterCell`/`mTimeStartedCleaning`/`mToiletToClean` (handyman),
  `mTimeStartedEntertaining`, `mPerp`/`mProsecutionTimestamp` (guard), `mTimeStartedResearching`.
- **THE GUEST BLOCK'S ALPHABETICAL RULE DOES NOT FULLY GENERALISE** - the staff block writes
  `mTimeStartedIdling` BEFORE `mTimeHired`, which sorts the other way. Use the serialiser's order, not the
  sort.
- **>>> THREE DIFFERENT NUMBERINGS OF THE FIVE KINDS EXIST AND CROSSING ANY TWO LOOKS PLAUSIBLE. <<<**
  Thing model 4 mechanic / 5 handyman / 6 entertainer / 7 guard / 8 researcher; **sprite folder** 4
  entertainers / 5 handymen / 6 mechanics / 7 guards / 8 researchers; **`PerTypeStaffConsts`** 0 handyman /
  1 mechanic / 2 entertainer / 3 guard / 4 researcher. The mechanic is model 4, wears sprite type 6, is
  paid as type 1. `StaffState.PayTypeOf` exists so crossing them is a test failure.
- **BUILT:** all eight shared states, plus the decide arm of the guard and researcher (whose decide arm is
  itself inline in the shared switch) - tiredness check, then 3-in-4 roll into `SetRandomDest`. The staff
  half of `FUN_004f9490` is a DIFFERENT function from the guest half: outside your patrol area sends you
  back in first, candidates outside it are struck out, and the fallback is the patrol roll
  (`FUN_00506f30`, 30 tries, cell CENTRE) where a guest instead gets the stranded stamp.
- **DEFERRED WITH NAMED REASONS:** handyman/mechanic/entertainer work-finding (`FUN_004d7100`,
  `FUN_004da5b0`, `FUN_004d46d0` - want litter cells, a broken ride, guests to entertain); the strike arms
  (need the staff union thing running a script, the `GateStatus` question); rest areas (a flag on an
  object's record the reader does not read).
- **>>> TWO DEFECTS THIS FOUND, AND THE FIRST WOULD HAVE SHIPPED THE WHOLE FEATURE INERT. <<<** (1) Every
  saved idle stamp is a reading of `mGameTick`, which this save left at **755**, and a loaded park counts
  from **0** - so "have I idled long enough" was false for hundreds of turns and the guard stood exactly
  where the file put him while every state was reachable, correct and never reached. **The original has the
  rule already**: `FUN_004f9490` zeroes `mStrandedTime` whenever it reads ahead of the clock. Caught ONLY
  because the test asserts POSITION, not state. (2) Staff must be handed the **GAME tick, not the thing
  tick** - `FUN_004d6410` tests against `[DAT_0080239c + 0x1da70c]` = `mGameTick`; the thing tick made them
  idle 8x too long. **Still unestablished and named in code:** whether `mGameTick` advances per 31ms step
  or per thing turn.
- **CONSTANTS READ OUT OF THE EXE** (code constants, not balance keys): walking costs `(6 - grade)` x
  **0.012** rest and x **0.005** mood per turn; clamps are [0,100] (`0x70082c` = 100.0f, `0x700830` = 0).
- **PERSON-BASE DECODE RETIRED TWO OLD UNKNOWNS:** `+0x198` is **`mStrandedTime`** and `+0x1bc` is
  **`mExitLevel`** - both were "unidentified fields" in the P2 notes.

**>>> THE `Illness` RENAME WAS INVESTIGATED AND DELIBERATELY NOT DONE - THE MEASUREMENT CAME BACK
UNINFORMATIVE, WHICH IS NOT THE SAME AS CONFIRMING. <<<** The ordering argument is strong: the guest block
holds exactly **seven** unnamed floats (+422, +426, +438, +509, +521, +525, +529), six are named, three
fall after `mTimeStartedIdling` (fits mTiredness/mToilet/mVomit), and **`mIllness` would sort between
`mHunger` and `mLastPosX`, which are ADJACENT with no gap** - nor is it anywhere in the 390-byte person
base. `PeepInfo.VomitCapacity` exists; no `PeepInfo.Illness`. **BUT**: I predicted +521 would hold
plausible 0-100 values and MEASURED it - **+521 and +529 both read NOUGHT on all thirteen guests**, while
+525 varies 3-27. All-zero is equally what an unused stat looks like and what a wrong offset landing in
padding looks like, so the only available test could not tell them apart. **A failed corroboration, not a
success** - the rename is recorded in `ReadGuest`'s comment and NOT made, and the temporary harness was
deleted.

**P4. THE ENGINE SURVEY'S ITEMS - DONE, five commits, 2026-09-17 evening.** (The good news that framed it
still stands: the camera layer, the audio runtime and `GameClock` are clean and unusually well
self-audited.) What was actually done, against what the survey claimed:
  - **`World/Terrain/Terrain.cs` DELETED** (`2f38ce3`) - 14 lines, `new Terrain` had zero hits, superseded
    by `ParkTerrain`. **The FOLDER stays: `Water.cs` beside it is live** (`Level.cs:139`), so "delete the
    Terrain folder" would have taken a working feature with it.
  - **Logger quiet flag FIXED** (`6f68a27`) - it raised `QuietLog` then fell through to the console with no
    return and no else, and `QuietLog` has zero subscribers. Also removed a redundant `Info` overload: a
    bare `Info(x)` bound to the one-argument method, so the defaulted one was reachable only from the
    single positional call site (`BFMUReader.cs:85`).
  - **`Graphics.Quad` and `Panel.Draw` FIXED** (`246cc0f`) - Quad allocated two Lists then `ToArray()`d
    both, **four** allocations per quad per frame, not two; indices are now a static and vertices a reused
    buffer. `Panel.Draw` built a `modelMatrix` nothing reads, every frame, for every HUD child.
  - **`Localization` GUARDED** (`19a4d83`) - unguarded `Enum.Parse<UIStrings>`; an unknown `#Token` now
    renders as its own name.
  - **`InputButton` NOTE ADDED** (`f59d71c`) - **32 of 41** unconsumed, with the two counting traps
    recorded (`OpenPark`'s only production hit is prose saying it is unconsumed; `Clone` is modifier-only
    so `Input.Pressed` can never see it).
  - **`Material.ClearBoundResources` NOT touched** - see the correction below; the claim was wrong.

**>>> P5. THE LOBBY GATE - ESTABLISHED, AND THE ANSWER IS A NEGATIVE. DO NOT BUILD IT. <<<** The stated
blocker IS gone: `LobbyGate.cs` says the doors loop "until park entry exists", and `IslandPanel.EnterPark`
(`:280`) now exists and ends at `Game.RequestParkLoad`. **But a new blocker replaces it.**
`RequestParkLoad` only sets `_parkAsked`; `ReloadLobbyIfAsked` runs on **PostUpdate, between frames**, and
calls `Level.Current.Unload()` then `LoadPark` immediately. **So the lobby is destroyed on the very next
post-update and there are NO FRAMES in which an opening gate could be seen.** Building it would produce an
animation nobody can ever watch. Also measured: `MeshAnimator`/`MeshRotator` expose `Update` (loops),
`Pose(animation, frame)` and `Pause()` - **there is no play-once-and-hold API**, so the honest lever would
be driving `Pose` by hand. The real remaining work is a **held transition** in the scene state machine,
which is a bigger change than the gate and is **Alexah's to want or not**.

**>>> RESOLVED - `ParkCameraMode.cs` WAS DELETED IN `edd33d5`; see the entry above. The paragraph below
is the REASONING that held while the decision was open, and its present tense is historical. <<<** The survey
said "delete with `Terrain.cs`". It IS dead - `new ParkCameraMode` has zero hits, and the two park cameras
derive from `CameraMode` directly, not from it. **But the file's own comment says it is kept ON PURPOSE**
("kept only so the flycam is not written twice") and carries a paragraph warning that an earlier stale
sentence there was "a trap rather than merely stale" for someone about to delete it. This file is already
named in [[verify-every-ordering]] as the one whose comment exists to prevent a wrong deletion. `Terrain.cs`
had no such note and was deleted; **the folder stays, because `Water.cs` beside it is live.**
**FOUR OF THE SIX P4 CLAIMS WERE WRONG AND WERE CORRECTED BY VERIFICATION BEFORE ANY EDIT:** the `Material`
path is `Render/Assets/Material.cs` and "scheduled every `Set()`" is FALSE (two texture-bind sites only;
the hot uniform overload was already fixed); `Localization`'s crash path is NOT reachable (nothing calls
`Parse`); in `Panel.cs` only `modelMatrix` is unread, `position`/`size` are read *solely* to compute it;
and the binding count is **32 of 41**, not 28 of 39.

*(The deferred list that used to sit here has moved UP to the end of the live plan, which is what now ends
the live block - see the header. Everything from this point on is finished, pushed work kept for its
evidence rather than its ordering.)*

# >>> THE PREVIOUS PLAN, 2026-09-17 - ALL FOUR ITEMS DONE AND PUSHED. Kept for its evidence, not its
ordering. <<<

Built from a six-way read-only survey plus an adversarial critic - 7 agents, 459 tool calls, 1.19M tokens.
Raw at `~/.cache/.../tasks/wmu0cu0re.output` (payload is at `result.surveys` / `result.critic`, NOT at the
top level - reading the wrong path returns empty defaults and prints a confident zero). Per-agent returns
are in that run's `journal.jsonl`.
**THE CRITIC CONTRADICTED THE SURVEYS ON EIGHT POINTS AND WAS RIGHT EVERY TIME IT CHECKED - INCLUDING
AGAINST THIS FILE**, which is the strongest argument for keeping the verify stage: see the corrected 210
claim in the block above. **This ordering is MINE and is OFFERED, NOT AGREED** - Alexah agreed the
2026-09-16 plan, not this one.

**WHAT CHANGED:** the old item 1 was already done, and **item 2 - the spine - is now essentially done**:
guests route across the real map, walk on the thing engine's own beat, age, animate from the executable's
own sprite scripts, and face correctly as the camera turns. So this is a genuinely new ordering, not the
old one with boxes ticked.

**>>> P1 AND P2 ARE SWAPPED, 2026-09-17, AND IT IS A FINDING RATHER THAN A PREFERENCE. P2 IS DONE AND THE
STATE MACHINE IS NOW SECOND. <<<** `FUN_005019f0` **delegates** states 2, 3 and 5 to handlers, and those
three are the **only** states any Lost Kingdom guest is saved in. `FUN_004ff730` (HeadingForGate),
`FUN_004ff7f0` (WaitingForOpening) and `FUN_004ffb20` (Entering) each consult `FUN_0051a280` - which the
executable's own field-name table pairs with **`mParkClosed`** - before they decide anything at all. So
the state machine could not have moved one guest until that header field existed, and doing it first
would have shipped a green feature that changed nothing on screen: **the same shape as the
four-walking-states reading that made the walk inert.**

**>>> P1 IS DONE, 2026-09-17, ON `alexah/88-what-the-header-already-knew` - AND PUSHED. THIS SAID
"COMMITTED, NOT PUSHED" UNTIL THE PUSH. <<<** `origin/alexah/88-what-the-header-already-knew` = `ea63731`,
0 ahead, on a **SIXTH fresh yes** ("yes you can push to my repos"). **THAT YES IS NOW SPENT**
([[ask-before-github]]) - it covered the nine commits that existed when it was given.
`PeepBehaviour` is the original's `FUN_005019f0`, and `ParkPeople.OnUpdate` calls it instead of the old
`WalkOn`, which is deleted. **127 warnings / 0 errors, 555 tests** (546 before), `save/` byte-identical.
Five of the twenty-two cases are built: Walking→AtGate, HeadingForGate→JudgingTheFee-or-WaitingForOpening,
Entering→Deciding with a visitor number, SteppingUpQueue→InQueue on BOTH verdicts, WalkingOutside→
AtTheBusStop. **Measured on the shipped park: the five heading for the gate reach JudgingTheFee, the seven
entering reach Deciding and take visitor numbers 1-7 one each, thing 33 is untouched, and all thirteen end
in a state that stands.** Deferred with reasons named in code: WaitingForOpening (needs the gate thing's
script state and two gate-cell globals), JudgingTheFee (needs the admission fee, which lives on thing 8),
Deciding (the hub), and the "bus is coming" speed of 50 (needs the bus's script state).
**BOTH DOCUMENTED DEPARTURES ARE RETIRED:** standing-on-arrival is gone, because arriving now enters a
state that queues the standing animation through `AnimationFor` as the original does. (Planning a route
inside the walk remains, and is still named in `PeepBehaviour.Walked`.)
**>>> A SELF-CAUGHT ERROR WORTH MORE THAN THE FEATURE. <<<** All eight tests written first build their own
walks and their own behaviour and call `Step` directly - **none of them constructs `ParkPeople`, so every
one would have passed with the wiring removed.** That is the unit-versus-wiring trap this file already has
a rule for, committed one turn after writing rule fifty-eight about vacuous confirmation. It was found
because a **predicted failure did not arrive**: control-109 predicted at least one existing test would
break and said in writing that no breakage "is worth investigating rather than celebrating". Investigating
showed the wiring WAS covered - by `ParkTickTests.TickingTheParkMovesItsGuests`, which asserts twelve
guests move and would have failed had the wiring been wrong - but that was luck, not design. Fixed by
`TickingARealParkCarriesItsGuestsThroughTheStateMachine`, which drives the real object on the real clock
for 140 thing ticks. **Why the old tests never noticed: they run about EIGHT thing ticks, and a guest needs
up to 108 to finish the longest route** - long enough to see movement, far too short to see an arrival.
Original text follows:
**P1 (NOW SECOND). ENTER THE STATE MACHINE - call the already-built `Peep.SetState` from the tick that already exists.**
Four of six surveys converged on this independently. `Peep.SetState` (`Peep.cs:299`) and `AnimationFor`
(`:347`) were written, documented against the disassembly and covered by six tests, with **ZERO production
callers** - **that was true until 2026-09-17 and is not any more**; `Peep.State` was written once in the
constructor and never again while the game ran. That makes
it **the most misleading thing in the tree** - `IsAWalkingState` already admits GoingToRide,
SteppingUpQueue, BeingAdmitted and OnRide as walking states that can never be entered. It **retires both
documented departures at once** (planning a route inside the walk; standing on arrival), and it is the
gate in front of queueing, riding, spending, leaving, every ride opcode that needs a guest handle, and
both consumers in P3. **DO NOT scope it as "the inline states only", which is what this said until 2026-09-17 and which
would have made it inert** - the inline cases of `FUN_005019f0` are 0, 7, 8, 12, 14, 16, 17 and 20, and
**no guest in Lost Kingdom is in any of them.** The reachable work is the arrival transitions of the
three DELEGATING states, which need only the walk verdict and `mParkClosed`: HeadingForGate arriving goes
to JudgingTheFee when the park is open and WaitingForOpening when it is not; Entering arriving takes a
visitor number (`FUN_0051aaf0`) and goes to Deciding; Walking goes to AtGate; SteppingUpQueue goes to
InQueue on BOTH verdicts; WalkingOutside goes to AtTheBusStop. Deferred for needing a world: the gate
thing's script state (`FUN_0051a290`), choosing a destination (`FUN_004fa530` + `FUN_004d8610`, which
returns a CELL id `y*0x80 + 1 + x` from two gate-cell globals), and everything Deciding does. Fold in **deleting or joining the dead duplicates** in `PeepNavigator`
(`StepTowards`/`ToleranceFor`/`RecordStep`/`GiveUp` - all public, all tested, none reachable, because the
live walk uses `PeepJourney`'s and `PeepSteering`'s own copies of the same two rules).

**>>> P2 IS DONE, 2026-09-17, ON `alexah/88-what-the-header-already-knew` - AND PUSHED (`ea63731`, 0
ahead). THIS SAID "NOT PUSHED - THERE IS NO LIVE PUSH PERMISSION" UNTIL A SIXTH YES WAS GIVEN, AND THAT
YES IS NOW SPENT. <<<** 127 warnings / 0 errors, **546 tests** (545 at the branch point),
`save/` byte-identical across the run. Measured on Lost Kingdom: **closed 0, tick 755, bank 8, visitors 0,
state 0** - so the park is saved **OPEN**, which is what makes P1's transitions go the interesting way.
**The field list is now CONFIRMED rather than derived:** `FUN_00516c80` pairs each name string with the
struct offset it serialises - mRandomSeed `0x1da708`, mGameTick `0x1da70c`, mParkClosed `0x1da710`,
mNumberOfVisitorsToDate `0x1da714`, mWeather `0x1da724`, mBankAccount `0x1da726`, mCurrentArrivalVehicle
`0x1da72a`, mParkGates `0x1da732`, mTrafficLights `0x1da734`, mWorldState `0x1da738`, mFirstObject
`0x1da746` - giving the same order and widths the reader already walked, Guard-before-Researcher included.
**`mWorldState` is the only one of the five that is a VALUE**: written 1, 2 and 4, compared against 4 in
eight places - and the save holds **0**, which is outside that set and is left unnamed rather than guessed.
Original text follows:
**P2. STOP DISCARDING THE FIVE HEADER FIELDS THE READER ALREADY PARSES.** `ParkWorld.ReadHeader`
(`:620-630`) reads all 26 header fields into a local array and keeps five - `mBankAccount`, `mGameTick`,
`mParkClosed`, `mNumberOfVisitorsToDate` and `mWorldState` are read off disk and thrown away when the
local goes out of scope. **Five assignments beside the five already there**, and the highest unlock per
line in the whole set. `mParkClosed` alone gives first readers to four dead things: Gates.RSE's
VAR_COMMAND idle, the advisor's Welcome line, `InputButton.OpenPark`/`ClosePark`, and
`UIStrings.ParkClosed`. It also hands over the economy thing's handle, which turns decoding model 16 from
a search into a bounded read - so it comes BEFORE the economy work, not after it.

**>>> P3 IS DONE IN FULL, 2026-09-17. (b) `20d5cb5`, (a) `2d1ff0f`, (c) `2435f6e` - ALL PUSHED. THIS SAID
"ALL COMMITTED, NONE PUSHED" UNTIL A SIXTH YES WAS GIVEN AND ACTED ON. <<<**
**THIS BANNER SAID "(a) AND (c) ARE DELIBERATELY NOT DONE AND ARE ALEXAH'S TO DECIDE - DO NOT FINISH THEM
WITHOUT ASKING" FOR PART OF ONE SESSION, AND IT IS CORRECTED RATHER THAN APPENDED TO.** Leaving it would
have been the dangerous direction: a fresh session would either redo work that exists or refuse to touch
a file it may. What changed my mind on both was re-reading my own reasons. **(a)** was not a taste
judgement at all - fidelity is the standing default, an empty park being silent is the original's DESIGN
rather than a defect, and `ParkAudio`'s own note named the exact condition for ending the deviation
("when guests exist"), which is an instruction. **(c)** was not blocked by the open question I thought it
was: "what should the gadget do underneath a modal" is a different question from "a window's `Update`
must not overwrite a flag another system owns", and only the second one is this bug.
**(b) DONE.** One line adding `ScreenParticles` to `SetupParkHud`. **I doubted this item and the doubt was
wrong** - a grep showed `SetupParkHud` at `Level.cs:352` and the particles at `:380` and it looked already
done; reading it showed `SetupParkHud` is 352-365 (stack, front end, cursor) and `:380` is inside
`SetupHud`, 367-383, which is the LOBBY's. So a park really did allocate, tick and kill glints nothing
could draw. **NO TEST, stated rather than hidden:** `SetupParkHud` is private and wants a `Level` with a
graphics device, and nothing in this suite covers draw-time wiring.
**(a) IS DONE - `2d1ff0f`.** The level is `clamp(counted/2, 0, 100)` (`FUN_004c81e0`), asked every pass
because `FUN_0051e790` re-counts from the park loop rather than being told.
**>>> AND IT COUNTS *GUESTS*, NOT PEOPLE. THIS PARAGRAPH SAID `clamp(people/2, ...)` AND THAT WAS MY OWN
ERROR, CAUGHT BY DECODING IT RATHER THAN BY ANY TEST. <<<** `FUN_004c7fa0` counts a thing only where the
model byte is 1 - a guest - so the five staff are excluded; **I was about to wire this to the park's
eighteen people and would have made a park twice as loud as the original.** Thirteen guests give **6 out
of 100**, so Lost Kingdom's music now plays at about a sixteenth of what it did. That is the original's
design - the music swells as a park fills, and an empty park really is silent - and it is a change of
FEEL that no assertion can judge, so Alexah's ear is still wanted even though the arithmetic is pinned.
**ONE TERM NAMED AND OMITTED:** the original also filters each guest through `FUN_004fa990`, five
predicates that all read one dword at `thing + 8` against 0, 1, 3, 9 and 10. That field is unestablished
(not the person type, which runs 0-7; not the state, which is `+0x220`), so our count is an **upper
bound** on the original's.
**(c) IS DONE - `2435f6e` - AND IT IS A REAL BUG, CHECKED RATHER THAN ASSUMED.** The decisive question was
whether `WindowStack` updates hidden windows at all; if it skipped them there would be no bug. It does not
skip them: `OnUpdate` is `foreach ( var window in _windows.ToArray() ) window.Update();` with **no guard**,
and `UiWindow.Update` is a bare virtual. So a put-away window is still updated, and `ParkGadget.Update()`
un-hides itself on the next frame.
**THE SHORT FIX WOULD HAVE BROKEN SOMETHING ELSE:** not updating hidden windows stops the clobber and
**permanently strands `ParkViewfinder`**, which is constructed hidden and relies on its own `Update` to
return when first person starts. Two owners were sharing one flag, so they now have two - `Hidden` is the
window's, `PutAway` is the front end's (the original's message 6), and `WindowStack` honours either when
drawing and hit-testing. It also fixed a second fault: `OptionsScreen.Closed` used to show every window it
found HIDDEN, which is not the set it put away.
**>>> STILL UNVERIFIED, AND DO NOT LET THE GREEN SUITE SAY OTHERWISE: THE ON-SCREEN EFFECT. <<<** The test
deliberately builds no `WindowStack` (its constructor preloads bitmap fonts off an unmounted file system),
so the render and hit-test guards are verified by READING, and nobody has yet watched a park with the
options screen open. Mechanism confirmed, flags pinned, screen unwitnessed.
The question I thought blocked this - "what should the gadget do underneath a modal", left open on purpose
above `ParkGadget.cs:469` - **is a different question and is still open**; this bug was never that.
Original text follows:
**P3. WIRE THE CONSUMERS ALREADY WRITTEN AS "WAITING FOR A NUMBER THAT NOW EXISTS".** (a) crowd-scaled
park music - `ParkAudio.cs:14-28` names the original's `clamp(guests/2,0,100)`, calls the fixed level a
deliberate deviation, and names the exact condition for ending it: guests, which now exist. (b) **One
line** adding `ScreenParticles` to `SetupParkHud`, mirroring `Level.cs:380` - today a park allocates,
ticks and kills button glints that **nothing can draw**. (c) The latent `Hidden` bug in those same files:
`ParkGadget.cs:469` and `ParkViewfinder.cs:71` assign `Hidden` every frame from conditions that omit "the
options screen is open", so in a park the gadget reappears under the options screen on the next frame -
**mechanism confirmed by reading, on-screen effect NOT yet verified, and I can verify it by capture where
the survey agents could not.** This is the first time the simulation is visible without typing `peeps`
into the console, which matters for verification as much as for feel.

**>>> P4 IS DONE, 2026-09-17, COMMITTED AS `ed75ecf`. EVERY NUMBER RE-MEASURED RATHER THAN CARRIED. <<<**
`README.md` "sixty-two of the hundred and forty-three" -> **350 run / 205 skipped of 555**, measured by
running the suite with `OPENTPW_GAME_PATH` unset. `README.md`'s "there is no parser... a claimed total of
210" -> the parser exists, every shipped script reads AND runs, and the opcode table declares **106**
members (counted in the tree) of which the runtime carries **57** (counted: 57 distinct `case Opcode.`).
`RideVM.cs:42`'s 210 -> 106. `RideScriptFile.cs:97-99`'s "there is no ride runtime in the tree" -> corrected;
it was right about `Ride`/`RideVM` and wrong about the tree, and **`new Ride(` has 0 hits while
`new RideVM(` has exactly 1, inside `Ride`'s own constructor**, so both are dead and the tracker's claim
survived checking.
**>>> AND THE PLAN'S OWN REASON FOR THE LAST ITEM WAS WRONG - SEE RULE SIXTY. <<<** This file said
`ParkCameraMode`'s "only reader" sentence is a trap because "`ParkOrbitCameraMode` and
`ParkCamcorderCameraMode` both read them". **Measured: `Input.Forward`/`Right` have THREE readers and
`RotateLeft`/`RotateRight` have TWO** - the camcorder turns with the pointer and reads neither. Writing
the plan's version would have replaced one inaccurate sentence with another, in the one file whose comment
exists to stop somebody deleting the dead flycam and believing a park's rotation went with it.
Original text follows:
**P4. THE STALE NUMBERS - they RIDE ALONG on the next branch rather than leading it, because they unlock
nothing.** `README.md:182` (143 tests against 545, spelled out in words); `README.md:157` ("there is no
parser" - false - plus the 210); `RideVM.cs:42`; `RideScriptFile.cs:97-99`; and `ParkCameraMode.cs:24-26`,
which claims to be the only reader of `Input.Forward`/`Right` and RotateLeft/Right when
`ParkOrbitCameraMode` and `ParkCamcorderCameraMode` both read them - **a trap, because anyone deleting the
dead flycam on the strength of that sentence would believe rotation died with it.**

**DEFERRED DELIBERATELY, each with its reason:** model 16's economy decode (behind P2 for its handle and
P1 for anyone to spend money); `separation` (needs the per-cell thing lists the engine keeps and we do
not - and with 13 guests the payoff is small, so it becomes worth doing the moment guests spawn); staff
simulation (their five state machines are unread in Ghidra); writing a park back (**no .TPWS has ever been
READ**, so the reader must not be assumed to generalise); the remaining 49 ride opcodes; the HUD's four
dead buttons.

**>>> A KNOWN GAP IN THE REASSESSMENT ITSELF, STATED RATHER THAN BURIED. <<<** The critic found **eight
whole areas nobody surveyed**: the camera layer (how the player sees a park at all, and
`ParkOrbitCameraMode` declares two of its own parts unfinished), the renderer entirely, the audio runtime,
`Global/` including **`GameClock`, which gates the whole simulation**, the park's scene furniture
(including `World/Terrain/Terrain.cs`, a dead 14-line class with zero references that no dead-code list
caught), the advisor and lobby world, client bootstrap and all of `OpenTPW.Common`, and several UI files.
**So this reassessment covers the park and its data well, and the engine underneath it barely at all.**
A second pass over those eight is worth a session of its own.
**>>> SETTLED 2026-09-17: THING 8 IS THE ECONOMY. THIS LINE SAID "UNPROVEN" AND THE CRITIC WAS RIGHT THAT
THE TREE COULD NOT SETTLE IT - IT DID NOT NEED TO, BECAUSE THE HEADER NAMES THE THING. <<<** The header's
`mBankAccount` (`world + 0x1da726`) holds **8** in the shipped park, and it is a **handle, not an amount**:
`FUN_005195d0`, the only place the executable reads it, is the weather thing's accessor character for
character with one offset changed - take the word, return `thingTable[id]`. That accessor has **at least
forty** callers (the xref listing was capped at forty and returned forty, so forty is a FLOOR and is
written that way in the code), one of them inside `FUN_004ff9d0`, the state in which a guest judges the
admission fee. Model 16 carries `mAdmissionFee`, `mBalance`, `mProfitThisYear` and the loan table.
**HOW CLOSE THIS CAME TO SHIPPING WRONG, recorded because no test caught it:** the doc comment as first
written said "what the park has in the bank" and contrasted it with the economy thing's wider `mBalance` -
a confident, specific, FALSE claim about the very field it documented, and all 546 tests passed with it in
place. What caught it was **the value looking wrong** - 8 is not a bank balance. A field's NAME is not
evidence of its meaning; `mBankAccount` names the thing that owns the account, exactly as `mParkGates`
names the thing that is the gates.

# >>> THE 2026-09-16 PLAN, SUPERSEDED BY THE BLOCK ABOVE - kept for its detail, and because Alexah agreed
THIS one ("I agree with your plan"). Its items 1 and 2 are DONE. <<<

**Published as an artifact (Alexah's own - update with `url`, never republish a second copy):
"Where OpenTPW Stands" - https://claude.ai/code/artifact/7a7bc033-32f1-432d-82f9-418fd3530a23**
**UPDATED IN PLACE TO VERSION 4, 2026-09-16**, after the edge test and the path walk went up - same URL,
no second copy, favicon left alone so it stays the page Alexah recognises. What changed: the figures
(**398 tests, 274 commits ahead, 253 files, +48,151, 84 branches**, 127/0 and 0 PRs unmoved); a new
"what this round corrected" section; and **two claims the page itself had got wrong** - it said the
engine "reads the side opposite the way it is going", and it called the pathfinder "a self-contained
search" when it is a straight walk with two wall-followers. **"They still do not walk" was the sentence
that mattered then, and it is gone.
>>> THE ARTIFACT WAS REPUBLISHED IN PLACE TO VERSION 10 ON 2026-09-17 - same URL, no second copy, favicon
and icon left alone so it stays the page Alexah recognises. THIS BLOCK SAID THE UPDATE WAS "OWED" UNTIL IT
WAS DONE. <<<** It now carries the measured figures (**545 tests, 297 commits, 282 files, +55,545, 87
branches**; 127/0 and 0 PRs unmoved), the reprioritised four-item plan, both facing faults including the
unreported cancelling-corrections one, and - said on the page rather than buried - that these notes
contradicted themselves five times in a day and that the survey's own critic caught this author in a false
claim. To update it after compaction: `action: "read"` with that `url`
FIRST (a publish to an artifact the conversation has not read is refused), then republish with `url`.
Do the four in this order. Everything is scoped to **Lost Kingdom** ([[opentpw-format-goal]]), and
**fidelity is the default** - recreate the original, do not approximate it.

**>>> REVISED 2026-09-16 AND APPROVED - ALEXAH: "I agree with your plan". THIS WAS THE LIVE ORDERING
UNTIL 2026-09-17 AND IS NOT ANY MORE - the reprioritised plan above replaces it; everything from here down
is kept for its DETAIL and its evidence, not for its ordering. <<<**
**Marking the heading superseded did not retire the sentences inside the block** - this paragraph went on
saying "THIS IS THE LIVE ORDERING" two lines under a banner saying it was superseded, which is rule
fifty-seven at a finer grain than I first applied it: the claims inside a block need retiring one by one,
not just the sign over the door.
Item 1 **DONE** (`4f307bb`). Item 2 **WAS "IN PROGRESS" AND IS NOW DONE** - guests walk, animate and face
correctly; the counts below (517 tests, eighteen commits) are the figures OF THAT ROUND and are superseded
by 545 tests over 297 commits. Original text follows:
Item 2 **IN PROGRESS:
thirteen commits pushed to `alexah/84-whether-a-cell-may-be-left` (tip `b196e3d`), plus FIFTEEN on
`alexah/85-the-slack-in-a-route` (tip `3d33a04`) - `8fdd0f7` the straightener, `580b0b6` the search,
`678f375` the reroute pass, `e83a523` the fixed-point arithmetic, `89b8dd0` route-following,
`67c954a` the ray/circle solver, `104ce02` wall avoidance, `3da45b6` the steering step, `30f2e72`
the consolidation, `78639eb` the navigator join, `c9c9d4c` the live map binding, `80986a5` the
correction of my own overclaim in `78639eb`, `415df20` the FOURTH site that same sweep missed, and
`66e7562` the position a guest stands at, and `3d33a04` planning a route against the real
map, **and ONE on `alexah/86-the-tick-that-moves-anybody` (tip `5940fbc`) - the walk tick itself.**
**517 tests** (492 at `3d33a04`; 500 at `5940fbc` - 7 for the walk plus 1 closing a gap a control
exposed; 513 at `916f2a7` - 7 for the heading table and 4 for where a guest is drawn, the latter written
only because a control proved the drawing was pinned by nothing at all; **517 at `160d151`** - the walk's
RATE and the needs share, both added only after Alexah looked at a park and found what no test could).
**>>> ALL EIGHTEEN ARE PUSHED. `origin/alexah/86-the-tick-that-moves-anybody` IS AT `160d151`, 0 AHEAD. <<<**
`5940fbc` went on a third yes; `916f2a7` and `160d151` went together on a FOURTH ("Yes push it please",
2026-09-17), after Alexah had refused an earlier one ("No. No push.") - **so the refusal was real, it was
honoured, and it was later lifted by a fresh yes rather than by my re-reading the old one.**
**THAT FOURTH YES IS NOW SPENT** ([[ask-before-github]]): each permission covers only the commits that
existed when it was given.
Both commits were built **ALONE** in throwaway worktrees before the push - `916f2a7` at 127/0 with
513/513 and `160d151` at 127/0 with 517/517, warning counts captured from the ISOLATED builds this time
(the previous push's capture swallowed them, and that gap is now closed). One named refspec,
fast-forward, no force; GitHub offered no PR link and none was used. After: `local == remote`, 0 ahead,
**0 `refs/pull/*` on origin**, `main` still `453e779` on local, origin and upstream, one worktree.
Fourteen went 2026-09-16 on "Yes push please!"; `3d33a04` went the same day on a SECOND fresh yes
("Yes you can push to my repo. Good work.").
**ALL THREE YESES ARE SPENT - the next push needs a new one** ([[ask-before-github]]), and each
permission covered only the commits that existed when it was given, not everything after. The third
was given as "You can push the repo as well, I mean to specify", clarifying a "yes" that had answered
a different question - **an ambiguous yes was NOT treated as a push authorisation, and should not be.**
Verified AFTER the 85 push: `local == remote`, 0 ahead, 0 `refs/pull/*`, `main` still `453e779` on
local, origin AND upstream, one worktree. That tip was built **ALONE** in a throwaway worktree before
it went, at **127/0 and 485/485** with `save/` byte-identical. `5940fbc` HAS had the build-alone-in-a-worktree gate run on it, at the push: a detached worktree at
that commit built with 0 errors and ran **500/500, 0 skipped**. **One honest gap: the `tail -3` on that
build captured the error line and not the warning count**, so 127 for this commit rests on the in-tree
`--no-incremental` build rather than on the isolated one. Widen the capture next time.
**>>> GUESTS WALK. This line said "GUESTS STILL DO NOT WALK" until `5940fbc`. <<<** Measured on the
shipped park: all thirteen plan a route and all thirteen ARRIVE, in 1 to 32 ticks, some travelling more
than four cells. What they do NOT do is decide anything on arrival - they stop, because that is the
state machine.
**(a) DONE and pushed. (b)** needs only the deferred diagonal tail. **(c) IS COMPLETE** - arithmetic,
route-following, ray solver, wall avoidance and the steering step all built and controlled.
**(d) IS DONE IN CODE AND NOT YET SEEN ON SCREEN**, which is the whole of what remains of it.
**(d)'s FOUR STEPS ARE ALL COMMITTED:** `78639eb` joined the navigator to the guests, `c9c9d4c` bound
`CellEdge` to a live `ParkWorld` via `CellEdge.For( park, mode )`, `3d33a04` planned the route, and
`5940fbc` ticks the walk. **WHAT IS LEFT INSIDE (d):**
**(i) IS DONE - `3d33a04`** gives a guest a ROUTE: `PeepNavigator.NavigateTo` runs
`CellSearch`/`CellReroute` against `CellEdge.For( park, mode )` on the live map.
**>>> (ii) IS DONE TOO - `5940fbc` ON `alexah/86-the-tick-that-moves-anybody`. GUESTS WALK. <<<**
**IT IS PUSHED - `origin/alexah/86-the-tick-that-moves-anybody` = `5940fbc`, 0 ahead**, on a THIRD fresh
yes the same day ("You can push the repo as well, I mean to specify"). **THAT YES IS NOW SPENT TOO.**
`PeepWalk` is the original's `FUN_004fa2a0`: it asks the behaviours, runs `PeepSteering.Step`, writes
the result back to the navigator, and reports Arrived / Walking / CannotReach. `ParkPeople.OnUpdate`
gives every guest in a walking state a turn of it **every tick**. Measured on the shipped park: all
thirteen plan a route and all thirteen ARRIVE, in 1 to 32 ticks, moving up to four cells.
**(iii)** is still on-screen verification, **WHICH ONLY ALEXAH CAN DO** - so item 2(d) cannot be
closed out by me alone, and that should be said plainly when reporting rather than implied.
**A NOTE THAT WAS TRUE AND IS NOW NOT, past-tensed rather than deleted:** an earlier entry here said
"there is no `PeepNavigator.NavigateTo` - that name never existed, do not plan against the invented
name". **That was correct when written and `3d33a04` then built it.** The method exists and is the
route planner. Left visible because a flat denial of something that now exists reads as current and
would send the next session looking for a different name.
**>>> TWO HARD FACTS ABOUT WHAT A GUEST HAS, ESTABLISHED 2026-09-16 - READ BEFORE PLANNING (i). <<<**
**(1) THE SAVE'S WAYPOINTS DO NOT EXIST IN THIS CODEBASE AND THAT IS DELIBERATE.**
`ParkWorld.ReadNavigator` reads **twenty scalar fields and performs no indexed read at all**;
`subpath_buffer[]`/`subpath_dist[]` are refused because `SetDest` fills only `path_buffer_count - 1`
of them, so unread slots hold `0xCDCDCDCD` or a stale distance from an earlier route. **So a route
cannot be RESUMED from a save - it must be PLANNED.** Guests will start walking by having a route
computed for them, not by continuing one.
**(2) THE POSITION WAS PARSED BUT DROPPED - FIXED IN `66e7562`.** `PeepNavigator` now carries
`Position`, `Velocity`, `Target`, `MaxSpeed` and `MaxForce`. `Mass` and `NavMode` stay dropped on
purpose (the steering loop divides by a literal 1.0, and NavMode's meaning is unestablished).
**(3) EVERY GUEST ALREADY HAS A DESTINATION AND IT COMES FROM THE SAVE.** `Target` is the original's
`path_target_pos`, and `ParkNavigatorStateTests.EveryoneIsWalkingSomewhereAndNobodyHasGivenUp` pins
that **no shipped person has a zero target**. So routing does not need destination-choosing behaviour
(which is undecoded and part of the deferred 22 states): **route from `Position.Cell` to
`Target.Cell`** and the data is the game's own.
**>>> THE `mode` ARGUMENT IS SETTLED - AND SINCE `5940fbc` SO IS ITS VALUE. IT IS 0. <<<**
This banner said "DO NOT HARDCODE 0/1/2" until the field was measured; see the two bullets at the end.
`CellEdge`'s doc said the fourth argument is "passed 0, 1, 2, a field of the thing doing the asking,
and a global, depending on the caller" and that only the numbers were known. For the PEEP path it is
now known exactly:
  - The mode global is **`0x007cf3c0`**, initial value 0, read by the four pathfinder functions
    (`FUN_005108a0` 32x, `FUN_005110d0` 5x, `FUN_00511470` 6x, `FUN_00511c90` 2x) and **written in only
    two places**, both search ENTRY POINTS rather than setters: `FUN_00511420(route, mode)` and
    `FUN_00511ef0(route, mode)`. Each stores arg2 into the global, then pushes **`0xea60` = 60000**
    (the same budget `CellReroute.Budget` already records) and calls `FUN_00511470`. `FUN_00511420`
    additionally calls `FUN_005108a0`. **So the mode is handed to the top-level "find a route" call and
    stashed in a global for the whole search to read** - it is not chosen per question.
  - **`FUN_00511420`'s ONLY caller is `FUN_0050f8e0`** - the peep route planner already known here as
    the owner of the deferred diagonal tail (item 2(b)). At `0050f931` it does
    `MOV ECX,[ESI + 0xb4]` / `PUSH ECX` / `PUSH 0x7cf108` - so **arg1 is a static route buffer at
    `0x7cf108` and the mode is the field at `this + 0xb4`.**
  - **`FUN_0050f8e0` IS A THISCALL and Ghidra hides it:** it reports `undefined FUN_0050f8e0(void)`,
    convention unknown, **param count 0**, while the disassembly opens `MOV ESI,ECX`. `this` carries the
    position at `+0x8`/`+0xc` (both `SAR ...,0x10`, so 16.16). Read the disassembly, as ever.
  - **>>> SETTLED BY MEASUREMENT 2026-09-16. THIS BULLET USED TO READ "NOT ESTABLISHED". <<<** Both
    open questions are answered. **Which object owns `+0xb4`: the NAVIGATOR.** `FUN_0050f3b0` reads
    `[param_1 + 0xb4]` on the same object that carries position `+0x8`, velocity `+0x10`, MaxForce
    `+0x18`, MaxSpeed `+0x1c`, LastProgress `+0xac` and the stuck record `+0xb0` - so it is not a
    steering object wrapped around the navigator, it IS the navigator. **And its value: 0.** A scan of
    all **881,521 instructions** in the image for a write to `+0xb4` finds 101; **exactly one lands on
    a navigator** - `0051009f`, inside the constructor `FUN_0050ffe0`, writing zero. The other hundred
    belong to particles, interface objects and `[ESP+0xb4]` stack frames. It is still NOT one of the
    twenty fields the save reader parses, and must still not be conflated with `NavigatorState.NavMode`.
  - **CONSEQUENCE, REVISED: 0 is a REPRODUCTION, not a hardcode.** `ParkPeople.WalkingMode = 0` carries
    that provenance in its own doc comment, and `CellEdge`/`NavigateTo`/`PeepWalk` all still TAKE the
    mode rather than assuming it - so nothing was given up. The old wording ("take `mode` as an explicit
    parameter; hardcoding 0, 1 or 2 would be a silent deviation") was correct for what was known then.
    Mode genuinely changes behaviour (on 2 the approach stops being freely passable), which is why the
    argument stays even though its value is now known.
**>>> ALL OF (d) IS DONE: (ii-a) `3d33a04`, (ii-b) `5940fbc`, THE DRAWING `916f2a7`, AND (iii) HAPPENED
ON 2026-09-17 - ALEXAH LOOKED. See the block further down for what that found. <<<**
**(iii) was on-screen verification, which only Alexah could do, and doing it was worth more than every
test written for this feature.** Ten seconds of watching a park found two faults that 515 green tests had
not: guests moving eight times too fast, and every facing dash pointing backwards. Both fixed in
`160d151`. **The lesson is recorded rather than implied: a suite that drives a unit directly cannot see
how often anything calls it, and no test here covers debug-only drawing at all.**
**>>> GUESTS NO LONGER SLIDE. DONE 2026-09-17 ON `alexah/87-the-picture-that-advances` - FIVE COMMITS,
ALL PUSHED, 0 AHEAD. <<<**
**A SELF-CONTRADICTION LIVED HERE FOR PART OF ONE SESSION AND IS RECORDED RATHER THAN QUIETLY TIDIED:**
this banner went on saying "FOUR COMMITS, COMMITTED AND NOT PUSHED" while the paragraph six lines below
it recorded the push in full, because a fifth commit and a push were each added by APPENDING a new banner
instead of correcting this one. Same rot as the branch-49 entry that said both "not pushed" and "every one
is pushed" within a session. **When a fact changes, edit the sentence that states it - do not append a
newer sentence and leave the older one standing**, and re-read the whole block afterwards, which is the
only reason this was caught.
`4a4638d` the VM and the executable's own script data; `48f6ad1` the two save fields that say which script
a sprite is on and where in it; `679041b` the wiring; `0d59909` a test hardened after a control exposed it;
and **`d0e7d73`, the camera-relative facing fix Alexah asked for after looking.**
**127 warnings / 0 errors, 545 tests (517 at the branch point), `save/` byte-identical throughout.**
**>>> PUSHED 2026-09-17 ON A FIFTH FRESH YES. `origin/alexah/87-the-picture-that-advances` = `d0e7d73`,
0 AHEAD. THIS BLOCK USED TO SAY "NOT PUSHED". <<<** Alexah's words were *"You may mush to my repos"* -
a typo for "push", in a message that also asked for a reassessment and compaction prep; recorded literally
so a later session can judge the permission for itself rather than take my paraphrase. **THAT YES IS NOW
SPENT** ([[ask-before-github]]): it covered the five commits that existed when it was given and nothing
after.
**THE BUILD-ALONE GATE RAN ON ALL FIVE, and the per-commit numbers are worth keeping** because the point
of the gate is that each commit stands up by itself: `4a4638d` 530/530, `48f6ad1` 535/535, `679041b`
541/541, `0d59909` 541/541, `d0e7d73` 545/545 - every one at **127 warnings / 0 errors** in a detached
throwaway worktree, all worktrees removed afterwards, `save/` byte-identical.
**ONE NAMED REFSPEC, NO FORCE, NO PR** - GitHub offered `pull/new/alexah/87-the-picture-that-advances`
and it was **not** used. After: `local == remote`, 0 ahead, **0 `refs/pull/*` on origin**, `main` still
`453e779` on local/origin/upstream, branch 86 unmoved at `160d151`, one worktree, tree holding only the
four never-commit paths.
**DOCS PUSHED THE SAME DAY:** `docs/particles` `06cd304..f6eb63c` on maexah/OpenTPW.FileFormats -
fast-forward, one refspec, no force, no PR; `master` still `0e8d5d0`; upstream's only PR head is still
`d73a627`, which is a DIFFERENT branch, so the "never onto a branch with an open PR" rule was checked
against the actual refs rather than assumed.
**A HARNESS DEFECT WORTH NOT REPEATING:** my first gate script extracted the test summary with
`sed 's/.*- //'`, which matched the LAST ` - ` on the line and threw away the very counts it existed to
capture - leaving only the exit code. Recovered from the saved logs. Same family as the `tail -3` that
swallowed a warning count before an earlier push: **when a capture exists to record a number, print the
number back and look at it.**
**>>> THE RATE CONTRADICTION IN THIS FILE IS RESOLVED, AND BOTH HALVES OF IT WERE WRONG. <<<** This block
used to say the original "drives the animation RATE from distance moved"; the sprite-VM block further down
said the same chain was **inert**, a "teleport clamp, not a rate control". A reader could believe either.
**It is neither: it is a DOUBLING.** Measured from the instructions: the doubling at `004fa42e` is gated on
the HURRY flag (`TEST EBX,EBX / JNZ` skips the `FADD ST0,ST0`), so an unhurried peep gets the doubled value
and a hurrying one the plain one. The sprite system runs **once every two game ticks** (62ms), gated at
`0054f5d7` - `TEST AL,0x1 / JNZ` - which is a DIFFERENT gate from the thing engine's eight at `0054f668`,
and it is reached FIRST, at `0054f5fb`. The interval a sprite is born with is `0x3e` = **62, exactly one
turn**, and the due test is a strict `>`; so an untouched sprite comes due every OTHER turn and a walking
one every turn. The arithmetic only ever yields 0, 1 or 2, and **0 means "leave the interval alone"**
(`FUN_004d4190` tests it against zero). The 250 clamp is UNREACHABLE - the square would have to be some
five thousand times what an `int` holds.
**WHAT IS PINNED AND WHAT IS NOT:** the two-tick period is pinned by a GAP test, not a parity one - an
earlier draft asserted "changes only on even ticks", which a gate of eight also satisfies. The
already-playing guard is pinned only by the ORDER of pictures (a restart steps to the first picture from
anywhere; a loop steps to it from the last). **Three controls, predictions written first
(`control-106-prediction.txt`): Q and R exact, P an UNDER-COUNT** - predicted one failure, got two, the
extra one being a before-and-after test aliased by the very cycle it watched. **Control 107, on the camera
facing, was a THIRD under-count** - two failures predicted, three seen, and the unpredicted one was a
SECOND REAL DEFECT rather than test noise (see the facing block below). Session tally now **36 exact,
5 exact-set-one-value-wrong, 3 under-counts, 1 wrong, of 45.**
**>>> (iii) HAPPENED: ALEXAH LOOKED AND CONFIRMED IT. "Yeah they move and animate." <<<** This banner said
"HAS NOT HAPPENED" until they did. They also found the one remaining visual fault, which is now fixed -
see the facing block below. **The one departure in this work** stands and was not queried: a guest who
arrives is put on the standing animation by `ParkPeople.WalkOn`, because the original does that through its
22-state machine, which is not built; without it an arrived guest strides on the spot for ever. It is
licensed by exactly the argument that already licenses planning a route inside the walk.
**>>> AND `d0e7d73` FIXED THE FACING, WHICH WAS **TWO** FAULTS, ONLY ONE OF THEM REPORTED. <<<**
Alexah: *"when rotating the camera they end up not staying facing their intended direction, and always face
opposite of the camera."*
  - **THE SIGN.** A person's octant runs CLOCKWISE (`OctantOf` over `PeepHeading`: N 0, E 2, S 4, W 6); the
    camera's runs ANTICLOCKWISE (N 0, W 2, S 4, E 6), because it is taken from the orbit camera's yaw.
    Subtracting them applies the camera's turn twice instead of cancelling it - wrong by exactly
    `2 * cameraOctant`, hence right at north and south and backwards at east and west. **It is a PLUS**,
    which is also what this file's own branch-80 note said (`(cameraOctant + facing) & 7`) and what I had
    not reconciled with the code.
  - **THE CONDITIONING, unreported and would have outlived the other fix.** `CameraOctant` read
    `floor( ((yaw - PI/8) / TAU * 8) + 0.5 )`, in which the two corrections **cancel exactly** - PI/8 is a
    sixteenth of a turn, so scaling it gives back the same 0.5 that is added. What remained was a plain
    FLOOR of the yaw in eighths, and the orbit camera only ever sits at multiples of PI/4 - so **every
    position it can hold lands exactly on a boundary**, decided by the last bit of a float. Due south came
    back as 3.99999989 and floored to 3. Fixed by putting the half AFTER the scaling.
  - **HOW IT WAS SETTLED, because screenshots could not.** At camera octants 0 and 4 the right and wrong
    arithmetic are IDENTICAL; at the odd octants they differ only by the picture being MIRRORED. So the
    readable frames prove nothing and the discriminating frames are a fifteen-pixel profile. The invariant
    does it with no park, no device and no eyesight: **turn the camera and the person together and the
    drawn picture must not change.** It failed on the old code by exactly two per eighth.
  - **THEN CONFIRMED ON SCREEN ANYWAY**, because an invariant cannot see a CONSTANT OFFSET. Subject chosen
    so the reading could not be ambiguous: **thing 28, the guard** - stationary, in the open, facing west -
    must show a left profile, his back, a right profile and his face at yaw 0/90/180/270. All four met,
    predictions written first, yaw read back from `camera` rather than trusted. Harness
    `~/.cache/tpw-harnesses/facingfixed.py`; record in `control-107-outcome.txt`.
  - **STILL NOT PROVEN, and stated rather than implied:** that our zero heading matches the original's. A
    constant offset on every person would satisfy every test and every frame above.
**DOCS: DONE THE SAME SESSION** ([[opentpw-fileformats-docs]]) - `saves.md` on **`docs/particles`** (which
carries no PR; upstream's PR #1 is `d73a627` on a different branch, and origin has 0 pull refs) gains the
`0x0C` field and a new "animation programs" section. **PUSHED 2026-09-17 - `f6eb63c..44ba114`,
fast-forward, 0 ahead; this said "Committed, not pushed" until it went.** The site was BUILT first
(`astro build`, exit 0, 15 pages) - node is at `~/.local/bin/node` and is not on the shell's default PATH,
which is worth knowing because `npm` simply reports "command not found" and looks like an absent toolchain.
**STILL DELIBERATELY ABSENT:** the model-7 special animation (`0x17`, gated on the thing's `+0x19c == 0x12`,
a field nothing parses); staff animations (staff are not simulated at all); and the other 62 scripts in the
array, which belong to things this project does not draw.

**>>> WHAT `5940fbc` ("Let the park's guests walk to where they were going") ACTUALLY DID. <<<**
`PeepWalk` is the original's `FUN_004fa2a0`. Each tick it lets the stuck history ask for a fresh route,
asks the behaviours, runs `PeepSteering.Step`, writes back, and reports Arrived / Walking / CannotReach.
`ParkPeople.OnUpdate` gives every guest in a walking state a turn **every tick**. 8 files, +767/-17.
- **THE NAVIGATOR IS THE RECORD OF TRUTH; journey and steering are views seeded from it and written
  back.** They must be RE-SEEDED mid-tick, because `Refill` fires inside the force query and replaces
  the route underneath it. `PeepNavigator`'s `Cursor`/`BufferedDistance`/`Finished`/`StuckBits` became
  `internal set` for exactly this, and `PeepSteering.StuckBits` gained a setter.
- **WHICH STATES WALK: ELEVEN, NOT FOUR - and the four-state reading made the feature INERT.**
  `FUN_005019f0` calls the walk from four cases in its own body; seven more cases jump to a handler
  that calls it one level down (`FUN_004ff730` HeadingForGate, `FUN_004ffb20` Entering, `FUN_004fff20`,
  `FUN_004ffbc0`, `FUN_005006b0`, `FUN_00500900`, `FUN_00500a50`). Reachability over all 22 handlers
  gives **0, 2, 5, 7, 9, 10, 12, 13, 15, 18, 20**. Every guest in Lost Kingdom is in `HeadingForGate`,
  `Entering` or `WaitingForOpening`, and the first two were in the seven that were missed - so the
  first build was green and moved NOBODY. Cross-check: `Peep.AnimationFor`'s walk group, written in an
  earlier session from the other direction, agrees on ten of the eleven (it omits `OnRide`, which is
  right for it - that list is about pictures, and `FUN_00500900` logs "successfully left ride", so 15
  is a guest walking OFF a ride). New rule material in [[verify-every-ordering]].
- **PROGRESS IS MEASURED TO `Waypoints[Cursor]`, FROM WHERE THE STEP LANDED.** `FUN_0050fd40` loads
  `ECX` with `[ESI + cursor*8 + 0x64]` - the waypoint, not the destination, even on the last leg - and
  reads position from `[ESI + 8]`, which `FUN_0050f3b0` has already written the step into. BOTH halves
  were wrong here at first and **no test caught either**; a control that deliberately PASSED is what
  exposed it.
- **MEASURED ON THE SHIPPED PARK:** all 13 plan and all 13 ARRIVE, 1 to 32 ticks, up to four cells
  travelled. The longest route in the network, (57,15)->(43,29), arrives at tick 108 through 27 cells
  and is the ONLY case anywhere that exercises `Refill`. Arrival is to the target POINT, not its cell -
  things 39 and 31 stop one cell short and are correctly arrived.
- **DELIBERATELY ABSENT, each named in code:** `separation` (needs per-cell thing lists; the original
  really does register it at `0x1999` - `FUN_0050fdf0` looks all three up by name, `avoid_walls`
  `0xe666`, `follow_path` `0x8000`, `separation` `0x1999`); the ground-staleness check at the front of
  `follow_path` (needs the stamp table at `world + 0x2d8` and navigator `+0x48`, neither parsed); and
  everything a guest does ON ARRIVAL, which is the 22-state machine. So guests walked and then stopped - **TRUE WHEN WRITTEN AND NOT ANY MORE: five of those states are built (`de7f8d0`), so arriving now enters one.**
- **FIVE CONTROLS, each with its prediction written first** (`control-102-prediction.txt` /
  `-outcome.txt`): the refill's `addCurrent` (exact set, one value wrong - it failed on the cell count,
  not the tick count, because passing a waypoint you already stand on costs ZERO ticks); the
  walking-state list (exact, `Expected:<12>. Actual:<0>`); and three on the progress measurement.
- **PUSHED** to `alexah/86-the-tick-that-moves-anybody` on a third fresh yes. Five gates ran before any
  network write (clean tree, branch absent on origin, 85 unmoved, `main` unmoved on all three, and the
  worktree build), one named refspec, no force. GitHub offered `pull/new/...` and it was NOT used.
  After: `local == remote`, 0 ahead, **0 `refs/pull/*` on origin**, `main` still `453e779`.

**>>> (iii) HAPPENED 2026-09-17: ALEXAH LOOKED, AND FOUND TWO FAULTS THAT 515 GREEN TESTS DID NOT. <<<**
Both were invisible to the suite and obvious on screen within seconds. Neither was in the walk itself -
`PeepWalk`, `PeepJourney`, `PeepSteering`, `PeepNavigator` and `PeepHeading` were all correct.

**FAULT 1 - THE THING ENGINE RUNS ONCE IN EIGHT GAME TICKS, NOT EVERY TICK. Guests moved 8x too fast**
("they ran from the bus stop to the gate at Mach Jesus"). The park loop gates it at **`0054f668`**:
`TEST byte ptr [0x00877d34],0x7` / `JNZ 0x0054f82d`, and **BOTH** routes to `FUN_00516380` lie inside
that block - the direct call at `0054f7bb` and `FUN_005166b0` at `0054f760`. The counter is the loop's
own tick: incremented read-modify-write at `0054f4cd`/`0054f4d6`, zeroed at scene entry
(`0054edb0`, `0054f443`). A second gate at `0054f82d` tests `& 0x1f` for something else.
**INDEPENDENT ARITHMETIC THAT CONFIRMS IT, and this is what makes it certain rather than plausible:**
`FUN_00510190(this, float factor)` sets `MaxForce = ftol(factor * 26214.4)` and
`MaxSpeed = ftol(factor * 13107.2)`, both floored at `0x28f`, factor clamped to 2.0 - and
**13107.2 is exactly `0.2 * 65536`**. So a factor of one is a fifth of a cell per THING tick. The
shipped park's guests carry `MaxSpeed` 15728, which is **factor 1.2 exactly**. At eight game ticks per
thing tick that is **0.96 cells a second** - a walking pace. At one it is 7.7.
It is called every needs tick from `FUN_004fa870`, which blends three shorts at `+0xc0/+0xc2/+0xc4`
against `PTR_DAT_0075c7fc` and smooths through `+200` - **so speed is recomputed per person per tick and
is NOT static**, which this project does not yet model.
**A TRAP INSIDE THE FIX:** `Peep.Tick`'s `(id & 3) == (tick & 3)` share must count **THING** ticks. Eight
divides four, so feeding it game ticks makes the test true only for guests whose id divides four and
starves the other three quarters of their needs for ever. The original reads a separate counter
(`DAT_0080239c + 0x1da70c`) for that test rather than the loop tick it is gated on.
**MEASURED IN THE RUNNING GAME**, consecutive-frame changed pixels over the ground at
0.25/0.75/1.25/2/3/5/8s: **before `16073, 5415, 1766, 442, 1577, 2168`** - all over by three seconds;
**after `13177, 13785, 14178, 11500, 9251, 4160`** - sustained across the whole eight. The 2-to-3s
interval went from 442 to 11500.

**FAULT 2 - THE FACING DASHES POINTED EXACTLY BACKWARDS.** `WriteGroundDash` built its direction as
`(sin(turn), cos(turn))`, i.e. angle 0 towards **+y**; `PeepHeading`'s zero is **-y** (its cardinals are
-y 0, -x 0x200, +y 0x400, +x 0x600, from `FUN_006e7074`'s branch structure). Every dash in the park was
half a turn out. **The dash's own comment had predicted this exactly** - "a convention chosen here, not a
measurement... if every dash in the park is wrong by the same amount, that is the answer this was drawn
to show" - and it could not be read until people moved. Fixed by negating both components.

**WHY NEITHER WAS CAUGHT, and it is the same shape twice:** every test drove `PeepWalk.Step()` directly,
so nothing measured the RATE at which anything called it, and nothing covers debug-only rendering at all.
A test asserting guests moved passes at any speed. **The rate now has a test that measures the PERIOD**
(`TheThingEngineTurnsOnceEveryEightGameTicks` records which game ticks the position changes on and
asserts every gap is eight). **The dash is still pinned by nothing** - it is debug-only drawing, and that
is stated rather than hidden.
**AND THE DEBUG CONSOLE COULD NOT SEE ANY OF IT:** `peeps` printed needs, `guests` printed the SAVE's
cell. Both censuses now print the live position, the drawn position, the cell size and whether a walk was
found - which is what turned a week of theorising into one measurement.
**BOTH FAULTS ARE FIXED IN `160d151` ("Tick the thing engine once in eight, as the park loop does"),
COMMITTED AND PUSHED.** 4 files, +353/-15. `ParkPeople.ThingTickEvery = 8` gates `OnUpdate`, and the
needs share is handed `tick / ThingTickEvery` rather than `tick`.
**WHAT IS PINNED AND WHAT IS NOT, because the difference is the whole lesson of this round:**
- `TheThingEngineTurnsOnceEveryEightGameTicks` measures the PERIOD - it records which game ticks the
  position changes on and asserts every gap is eight. Deleting the gate fails it, `Expected:<8>
  Actual:<1>`, with the series reading `1, 2, 3, ... 44`. A test asserting guests MOVED would not have
  caught it, and that is exactly why three of them did not.
- `GuestsInEveryTickSlotGetTheirNeedsTicked` exists only because a control proved the starvation trap was
  guarded by a code comment and nothing else. It ages sixteen thing ticks and asserts every guest's
  ExitLevel fell, behind a first assertion that all four id slots really are represented in the shipped
  park. Feeding the game tick then fails it: "guest 42 is in slot 2 and did not age at all: 142 -> 142".
- **THE DASH FIX IS PINNED BY NOTHING AND THAT IS DELIBERATE.** Negating it back breaks no test. It is
  debug-only drawing, off unless the console turns it on, and what found the fault was Alexah looking.
  Stated rather than implied.

**>>> THIS BLOCK IS DONE - `916f2a7` IS THE SPRITE JOIN. WHAT IT SAYS BELOW WAS TRUE FOR ABOUT AN
HOUR. KEPT BECAUSE THE DECODING IN IT IS STILL THE RECORD. <<<**
`ParkGuestSprites.Standing` now draws each guest from `PeepWalk.Position` scaled by the heightfield's
own `CellSizeX`/`CellSizeY`, and turns them by `PeepHeading`, falling back to the save when there is no
simulation or no ground yet. **A guest still SLIDES rather than walks** - `sprite.Frame` comes from the
save and the sprite-script VM that would advance it is not built, so the animation never plays.
What follows described the state before that:
**The simulation moved and the PICTURES DID NOT.** `ParkGuestSprites` builds `_people` once in its
constructor as a list of `(ParkWorld.Person, ParkWorld.Sprite)` **record structs copied out of the
save**, and `OnRenderTranslucent` draws each quad at `sprite.X`/`sprite.Y` from that copy. Nothing
writes to it. `ParkPeople` moves `Peep.Navigator.Position`, and **there is no link between the two**,
so a park on screen today shows thirteen guests standing exactly where the file left them while the
simulation walks them about underneath.
**THE ORIGINAL DOES BOTH IN ONE FUNCTION, so this is not a separate concern it invented:**
`FUN_004fa2a0` reads the position, runs the steering step, then shifts the navigator's 16.16 right by
**8** into the thing's `mX`/`mY` via `FUN_0050b6a0` - which is exactly `NavigatorState.RawX` -
and writes the heading from `FUN_006e7074(oldX - newX, newY - oldY)`, an eleven-bit turn off a
256-entry table at `0x006e71b4`. It also maintains the per-cell thing lists as a guest changes cell
(remove at `FUN_004d9280`, add at `FUN_004d91f0`) - which is the very index `separation` needs.
**THAT COMMIT IS `916f2a7`.** Units, as built: a sprite's X/Y are WORLD units and the navigator's are
16.16 cells, so `world = (nav / 65536) * CellSize` with `CellSizeX/Y` off the heightfield.
**A MEASUREMENT WORTH KEEPING, because it is why the save's sprite coordinates are NOT the calibration:**
comparing the shipped park's `sprite.X`/`sprite.Y` against the thing's own `mX`/`mY`, x agrees to about
one part in ten thousand (ratio 9.9986-10.0029) but **y wanders by a third of a per cent** (9.9676-
10.0006), and the one guest whose y agrees exactly is the only one NOT walking. So the sprite record is
written at a different moment from `mX`/`mY`. The ground's own `CellSize` is what a guest is placed
with, which is what keeps feet on terrain rather than merely near it.
**THE HEADING:** `FUN_006e7074` is a TABLE arctangent - 257 words at `0x006e71b4`, `0x800` to the turn,
exactly `trunc(atan(i/256)*2048/2pi)` on all 257 under TRUNCATION (only 135 under rounding). The table
is copied into `PeepHeading` rather than computed: at `i=256` the true value is exactly 256 and a
`Math.Atan` returning the nearest double below `pi/4` truncates to 255.
**ORDERING:** position is written EVERY tick, the heading ONLY while still walking - `FUN_004fa2a0`
returns on gave-up and on arrived before computing one.
Items 3 and 4 **not started**, and every one of item 4's
claims was **re-verified true this session** (see the note at its end).
**ITEM 2'S REMAINDER, IN DEPENDENCY ORDER - this is the next work, and the order is not arbitrary:**
- **>>> (a) THE EDGE PREDICATE IS DONE AND PUSHED, 2026-09-16. <<<**
  `68e2dfe` `CellEdge.cs` + `CellEdgeTests.cs` (the whole ladder, 15 tests) and `5b17432` the track
  record (`ParkWorld.MapCell` gains `TrackType`/`TrackFlags`/`TrackParentId`, 3+3 tests). Docs:
  `28a5400` on `docs/save-person-record`. **127/0, 382 tests, 0 skipped, `save/` byte-identical.**
  **>>> THE PUSH, 2026-09-16, at a fresh yes ("Yes you may push to my repos"). THAT YES IS SPENT -
  the next push needs a new one ([[ask-before-github]]). <<<** Branch
  **`alexah/84-whether-a-cell-may-be-left`** carries all four code commits (`68e2dfe`, `5b17432`,
  `09f937c`, `b196e3d`) over the already-pushed `3084eb6`. **ONE named refspec each, no force, no PR.**
  The branch was **NEW on origin**, so the before-check asserted its ABSENCE rather than a tip; every
  check was a hard assertion that would have aborted before any network write. GitHub offered
  `pull/new/alexah/84-whether-a-cell-may-be-left` and it was **NOT used**; the docs push offered no
  link, which is expected for a branch that already existed. After: `local == remote` on both, **0
  ahead**, **0 `refs/pull/*` on origin** (upstream's 13 are its own history, not mine - see the
  correction in the reassessment block above), `main` still `453e779` on local/origin/upstream, docs
  `master` still `0e8d5d0`, one worktree, tree holding only the four never-commit paths.
  **The docs rule was checked rather than assumed:** the docs upstream DOES carry an open PR #1, but it
  is from **`docs/md2-sgn-and-lobby-scripts`** at `d73a627`, a DIFFERENT branch - verified still
  `d73a627` after the push. `docs/save-person-record` carries no PR.
  **>>> A MEMORY STALENESS AUDIT RAN AFTER THE PUSH, 2026-09-16 - 22 agents, 318 claims judged, 16
  verified adversarially, 10 CONFIRMED STALE AND ALL TEN FIXED. <<<** What it caught, so the pattern is
  visible: **four notes still named `LobbyAdvisor`**, a class renamed in `9a31c20` that now greps to
  nothing; **`opentpw-fileformats-docs.md` still carried the superseded "reads the side OPPOSITE"
  reading** and now has a TWENTY-SECOND entry for `28a5400`; **`opentpw-debug-console.md` had all three
  of its `DebugConsole.cs` line numbers drifted** by `1197d0e` and omitted the `peeps` command entirely;
  **`post-neoveldrid-audit.md` restated 302/302 over 292 declarations** - a bullet whose own rule is
  "never quote a test count from a memory file", caught by its own rule twice; **`MEMORY.md` said
  `ParkFrontEnd.cs:272` "calls" StopQuietly** when it only routes there via `OptionsScreen.Open`; and two
  finished action items (`ParkGround`'s doc comment, the `SharpText` deletion) still read as owed.
  **THREE claims were REFUTED by the adversarial pass and deliberately NOT changed** - the `ParkFrontEnd`
  *routing* wording, the NeoVeldrid "four commits" line, and `park-hud-from-exe`'s ScreenParticles claim -
  so the verify stage earned its keep rather than rubber-stamping the auditors.
  **>>> A KNOWN GAP, NOT BURIED: 19 further claims were called stale and were NOT verified <<<** - the run
  verified at most three per slice. They are in the run's own journal at
  `f4cf68d5-.../subagents/workflows/wf_dc1c4841-bae/journal.jsonl`, worth a second pass some session.
  Four things worth keeping:
  - **THE DECOMPILER DROPS `this` ON ALL FIFTEEN CALLS, AND TWO DIFFERENT CELLS ARE BEING ASKED** -
    `EBP` is the cell being LEFT, `EDI` the cell being ENTERED. Which one gets each question is most of
    the meaning. Read the disassembly, never the decompiled form.
  - **THE "OPPOSITE SIDE" NOTE WAS WRONG AND IS NOW CORRECTED** in `MapStep.cs`, `saves.md` and below.
    The bit is read from the DESTINATION, about the side facing the source. **And no measurement could
    ever have caught it**: `mNeighbours` is symmetric across all **65,024** adjacent pairs, so scoring
    destination-vs-source returns **100% either way**. The old 94.8%/75.3% figure was comparing two
    bit-to-axis PAIRINGS, which is a different question and still stands.
  - **TWO PREDICTIONS REFUTED BY MEASUREMENT**, both recorded in the commit: the 572 track cells are
    open ground and scenery, **not** built footprints; and the reopening flag is set on **exactly one
    cell in the park**, so the branch closes **568 of 572**. Drawn out those cells are nested
    rectangular frames two cells thick - **only 2 of the 572 touch a path cell**, so the branch is real
    in code and nearly inert for walking.
  - **MY OWN HARNESS PRINTED "HOLDS" FOR A PREDICTION IT DID NOT TEST** - pass condition "not zero"
    against a prediction of "set on at least the 572". One cell satisfied it. New rule material.
  - **STILL PARAMETERS, deliberately:** the thing-on-the-cell-ahead branch (mode 2 only; needs per-cell
    thing lists the engine has and we do not). Harnesses: `edge-predicate.py`, `track-record.py`,
    `track-geography.py` in `~/.cache/tpw-harnesses/`.
- **>>> (b) THE PATHFINDER IS NOT A SEARCH - THE PLAN'S WORDING WAS WRONG. <<<** It was called a
  "self-contained search"; there is **no open list, no cost, no frontier and no heap anywhere in it**.
  `FUN_00511470` (budget 60000) walks **straight at the goal**, and when the edge test refuses it sets
  **two wall-followers going, one turning each way**, and takes whichever rejoins the line first.
  `FUN_00511420` is only its wrapper; `FUN_005108a0` is the outer loop over waypoint pairs (9 rounds).
  **TWO OF THE FOUR PIECES ARE BUILT AND PUSHED (see the push record under (a)):**
  - **`09f937c` `CellLine.cs` - the straight walk** (`FUN_00511350` + the step choice). Bresenham with
    ONE accumulator shared between the axes; the error seeds at **half of each distance ADDED**, not
    half the larger; and the degenerate cases are **asymmetric** - no-across borrows the down direction,
    THEN no-down borrows the across one, so a line going nowhere ends with both vertical. 7 tests.
  - **`b196e3d` `CellTrace.cs` - the wall-follower** (`FUN_00511c90`, written inline a second time in
    `FUN_00511470` with the turn reversed). Field offsets READ OFF THE DISASSEMBLY, because Ghidra's
    stack naming there is shifted and I had started deducing the struct from it: **+0x04/+0x05 goal,
    +0x200 facing-or-sentinel, +0x204 turn sense, +0x208/+0x209 current, +0x20a/+0x20b start**.
    Sentinels **0x32 rejoined / 0x33 full**, room **0xfb**. Shut ahead -> turn AGAINST the sense, record,
    do not move; clear ahead -> move, then take the turn TOWARDS the hand only if open, recording only
    then - so **a straight run along a wall records nothing and only corners come back**. The two jump
    tables are ONE predicate used twice: is the goal further along this direction than we are, asked of
    the turned direction and then of the REVERSE of the facing. 9 tests.
  - **>>> THE STRAIGHTENER IS DONE - `8fdd0f7`, `CellRoute.cs` + `CellRouteTests.cs`, 14 tests. PUSHED. <<<** New branch **`alexah/85-the-slack-in-a-route`** stacked on `b196e3d`.
    **>>> CORRECTED 2026-09-16: this said "NOT PUSHED - the last yes is spent and a fresh one is owed"
    while its OWN header two words earlier said PUSHED. The branch is fully out -
    `origin/alexah/85-the-slack-in-a-route` is `3d33a04`, 0 ahead. <<<** A self-contradicting entry is
    worse than a merely stale one: a reader can believe either half. Same rule as the branch-49 rot -
    write push state in the PAST tense with a date. It jumps **4, then 3, 2, 1**
    waypoints ahead from an anchor, drops what a clear line reaches, and loops whole passes until one
    changes nothing. **THE CORNER CLAUSE IS NOW PINNED BY HAND, AND THE WARNING WAS JUSTIFIED**: the
    frame is `SUB ESP,0x34` plus four pushes, so ESP is `entry-0x44` through the body, and against that
    `[ESP+0x44]` is x, `[ESP+0x41]` is y, and **the two `[ESP+0x30]` reads four instructions apart are
    the previous y and the previous x** - the same literal offset naming two different variables,
    because a `PUSH EDI` sits between them. **Ghidra's `CallDepthChangeInfo` is NOT importable in this
    build** (tried three module paths); the depth was computed from the prologue instead. The same trap
    recurs in `FUN_005108a0`'s tail, where `[ESP+0x3c]`/`[ESP+0x30]` written after four pushes are read
    back as `[ESP+0x2c]`/`[ESP+0x20]` - so the method generalises and is worth reusing.
  - **THE RULE THE CLAUSE STATES**, now a finding rather than a reading: at every turn the two ways of
    taking it **one cell early and one cell late** are each tried, and **a way open at its first step
    must be open at its second**; a way shut at its first step is held against nothing. That is stricter
    than `MapStep.CanStep`'s "either route will do". The consequence reads as wrong and is real:
    **shutting one more side can make a line legal that was refused**, because the side that refused it
    now sits behind a turn nobody can start. There is a test pinning exactly that.
  - **THE ROUTE RECORD HAS FOUR PARTS**, confirmed from three sites (`FUN_00511420` starts the search
    from `+2`, `FUN_00511470` tests arrival against `+4`, `FUN_005108a0` builds a scratch copy of the
    same shape): count at `+0`, **start at `+2`**, **goal at `+4`**, waypoints from `+6`. That settles
    what looks like an off-by-one in the original - the anchor for the first waypoint is the route's own
    start, because no waypoint precedes it.
  - **TWO CONTROL RUNS, PREDICTIONS WRITTEN FIRST, BOTH EXACT**
    (`~/.cache/tpw-harnesses/control-85-prediction.txt`). A asked the early turn about the cell being
    stood on rather than the one just left - the reading the decompiled form invites - and failed exactly
    `TurningEarlyMustNotBeHalfOpen`, with all three tests named as plausible collateral still passing.
    B dropped the two first-leg guards and failed exactly the **second** assertion of
    `ACornerShutAtItsFirstStepIsHeldAgainstNothing`. Restored from an md5-verified COPY, never
    `git checkout --`.
  - **Gates:** branch point measured FIRST (**127/0, 398/398**, `~/.cache/tpw-harnesses/baseline-85.log`);
    after, **127/0, 412/412, 0 skipped**; `8fdd0f7` built **ALONE** in a throwaway worktree at those same
    numbers; `save/` byte-identical at `23a4922d...` across every run, both controls included.
- **>>> THE SEARCH IS DONE - `580b0b6` `CellSearch.cs` + `CellSearchTests.cs`, 7 tests. PUSHED. <<<**
  Second commit on `alexah/85-the-slack-in-a-route`. **127/0, 419/419, 0 skipped, built ALONE in a
  throwaway worktree**, `save/` byte-identical. `FUN_00511470`: walk at the goal, and when the way is
  shut set two wall-followers going and take whichever rejoins first, then straighten, re-aim from the
  last waypoint kept, and carry on. Both followers are built on `CellTrace` - the original writes one
  inline and calls the other, which is why it reads as two algorithms and is one.
  - **THE TIE-BREAK IS REAL, NOT INCIDENTAL:** at `005118d0` the inline follower's state is tested
    before the other's, and **the loop-detector at `005118de` is asked BEFORE the second is looked at at
    all** - so a follower that rejoins on the same tick the pair are found to be chasing each other does
    not get to count. First draft had that ordering wrong and it was corrected against the disassembly.
  - **>>> AN OVERCLAIM OF MINE, CAUGHT AND CORRECTED BEFORE COMMITTING. <<<** I wrote that the search's
    two ways round a corner are "exclusive" where the straightener's are "independent", which reads as a
    difference in behaviour. **It is not one.** In the search the early question can only ever be
    answered yes: the cell it measures from plus one step in the new direction **is** the entered cell,
    and it is reached only when that side is already known shut; its other half asks about the edge just
    crossed, which is open by construction. Both edge tests are kept (the original makes them) but the
    if/else is an optimisation, not a second opinion. The real differences are **when** (after the step,
    not before) and **what for** (records a waypoint, does not reject a line).
  - **TWO KINDS OF FAILURE, AND THEY DIFFER:** giving up part way leaves the cell reached on the end of
    the route; **running out of turns writes nothing** unless it actually arrived.
  - **THE FRAME TRAP RECURRED AND A LINEAR DEPTH ANNOTATION LIES HERE:** control reaches `00511b62` by a
    `JNZ` at depth `-1084`, not by falling through the `ADD ESP,0x10` before it, so a tool that walks
    addresses in order prints the wrong depth from there on. Resolved by hand; that is what identifies
    `[ESP+0x1c]/[ESP+0x1d]` as the position saved **before** the step.
  - **TWO MORE CONTROL RUNS, PREDICTIONS FIRST, BOTH EXACT** (`control-86-prediction.txt`). C gave the
    tie to the wrong follower and failed exactly the THIRD assertion of
    `WhenBothTracesRejoinAtOnceTheFirstOneWins` - that wall is built so **both followers rejoin on the
    very same tick**, the only way to observe which is preferred. D gutted the corner rule's late branch
    and failed exactly `ATurnThatCannotBeRoundedIsWrittenDown`, whose whole route is pinned rather than
    a `Contains`, so it covers both branches. **A third mutation was reasoned through and deliberately
    NOT run** because it could not fail anything - recorded in the prediction file rather than
    discovered by burning a run. **Four controls this session, four exact.**
- **>>> THE ASSEMBLY'S MAIN LOOP IS DONE - `678f375` `CellReroute.cs` + tests, 8 tests. PUSHED. <<<**
  Third commit on `alexah/85-the-slack-in-a-route`. **127/0, 427/427, built ALONE in a worktree**,
  `save/` byte-identical.
  - **>>> A REAL GAP, CAUGHT BY ASKING "WHAT WOULD THIS CONTROL BREAK?" BEFORE RUNNING IT. <<<** The
    **seven tests written first ALL PASSED against a `CellReroute` that never splices anything**, because
    straightening alone reaches the same answer in every one of them. The splice - the whole point of the
    function - was unpinned. An eighth test (a wall that makes a straight line useless) was added and
    control E confirms it now fails if the splice stops. **This is the "passes for a reason that has
    nothing to do with its name" failure mode, caught before committing rather than after.**
  - **>>> TWO PREDICTION MISSES THIS ROUND, BOTH RECORDED RATHER THAN WAVED THROUGH. <<<**
    Outcomes in `~/.cache/tpw-harnesses/control-87-outcome.txt`, kept separate so the prediction file
    stays as written. **E** was exact on its failure set but said "still 127 warnings" and got **128** -
    the unconditional `return` left the loop below unreachable, a CS0162 the mutation itself created; the
    prediction accounted for behaviour but not for the SHAPE of the mutation. **F** predicted one failure
    and caused **none**.
  - **>>> WHY F CAUSED NONE IS THE FINDING: TWO OF THE THREE SPLICE DETAILS ARE UNOBSERVABLE, NOT MERELY
    UNTESTED. <<<** (1) Splicing over **one** waypoint instead of two leaves `waypoints[i+1]` directly
    after an answer that already ends on that cell, so the route holds it twice - and the straightener
    that runs after every splice always jumps a **zero-length** line from a cell to itself (arrival is
    tested at the top of the loop, before the map is asked anything), so the duplicate is dropped every
    time. (2) The guard against splicing in an answer that runs back through the waypoint being cut out:
    re-splicing the same answer yields the same route and only spends rounds. **Both are the original's
    economies, not correctness conditions.** Both are built anyway because the original has them, and
    said plainly here rather than left looking tested. **Running tally: A B C D exact, E half, F wrong.**
- **>>> WHAT THE MAIN LOOP DOES, pinned from the disassembly. <<<** For each
  waypoint, search between its two neighbours (`Before(i)` to `waypoints[i+1]`); if that succeeds, does
  not run back through `waypoints[i]` (checking its cells **except the last**), and `count + found - 2`
  fits in `0xfc`, splice it in over **two** waypoints, back up **four**, re-straighten, and **restart the
  scan from zero**. **Nine splices maximum**, and the round counter is spent only by a splice. A scan
  that changes nothing falls into the diagonal tail; nine splices return **without** it.
  **ONE DECOMPILER TRAP WORTH KEEPING:** the memmove length is built as `(-i << 31) - i + count` and then
  doubled, and that `<< 31` term is `0x80000000` for odd `i` - **which vanishes when doubled mod 2^32**.
  It is a compiler idiom for `2*(count - i - 2)`, not a real term. Read literally it is nonsense.
  **THE DIAGONAL TAIL (`00510a34` onward, 32 edge probes in four quadrant blocks) IS DEFERRED** to its
  own commit: it breaks a diagonal step into two by inserting a cell, and is separable from walking.
- **(c) THE STEERING STEP.** `FUN_0050f3b0`: sum the behaviours, clamp to `max_force`, divide, clamp to
  `max_speed`, commit the position **only if the step is legal**, shift the stuck bits twice.
  **>>> READ IN GHIDRA 2026-09-16 - THE BEHAVIOUR NAMES AND WEIGHTS ARE NOW MEASURED, NOT INHERITED. <<<**
  `FUN_0050fdf0` builds the list by matching three literal name strings against a pointer table at
  `PTR_DAT_00761ec0` and pushing `(weight, object)` pairs of **stride 8** (weight at `+0`, object at
  `+4`): **`avoid_walls` = `0xe666` (0.9)**, **`follow_path` = `0x8000` (0.5)**, **`separation` =
  `0x1999` (0.1)**. So the old note "separation is weight 0.1" is CONFIRMED by measurement. The table's
  seven entries are runtime-constructed objects whose name field reads empty statically, so only those
  three registrations can be recovered without running the game.
  **>>> A CORRECTION: "DIVIDE BY `mass`" IS WRONG - THERE IS NO MASS FIELD. <<<** At `0050f46a` the
  divisor pushed is the **literal `0x10000`**, and `FUN_0050e080` computes `(v << 16) / k`, so with
  `k = 1.0` the step is an **identity**. It is kept because the original keeps it, but nothing is
  divided by anything. `+0x18` is `max_force` (used twice - once per behaviour, once on the sum) and
  `+0x1c` is `max_speed`.
  **THE DECOMPILER DROPS `this` ON EVERY ONE OF THESE HELPERS**, so the chain was read off the
  disassembly: `ESI` is the peep (`MOV ESI,ECX` at `0050f3ce`); each behaviour is a **virtual call**
  `object->vtbl[0](out, peep)`; `FUN_0050f600(this, out, limit)` clamps a vector to a length;
  `FUN_0050df70(this, out, k)` scales by 16.16 `k`; `FUN_0050ece0(this, out, other)` adds;
  `FUN_005104b0(out, x, y)` is `(x >> 16, y >> 16)`.
  **TWO HELPERS TURN OUT TO BE ALREADY BUILT:** `FUN_00510870(x, y)` is `(y*0x80 + 1 + x) & 0xffff` -
  **exactly `MapStep.CellId`** - and `PTR_FUN_00762190` resolves to **`FUN_00510510`**, the one-cell step
  check `MapStep.CanStep` already models, read by nothing but this function.
  **ONE MORE DECOMPILER ARITY TRAP:** the decompiled form shows `FUN_00510870` taking FOUR arguments. It
  takes two. The extra pushes belong to the legality call that follows, interleaved by the compiler.
- **>>> THE ARITHMETIC IS DONE - `e83a523` `FixedVector.cs` + tests, 13 tests. PUSHED. <<<** Fourth
  commit on `alexah/85-the-slack-in-a-route`. **127/0, 440/440, built ALONE in a worktree**, `save/`
  byte-identical. The ~10 vector helpers around `0x0050cd80`-`0x0050f870` that everything steering is
  built from, on their own so they can be pinned before anything sits on them.
  - **>>> THE SQUARE ROOT WOULD HAVE GONE SILENTLY WRONG. <<<** Decompiled, `FUN_004d49a0` reads as
    `__ftol(param_1, 0)` - a float-to-int conversion and nothing else. Disassembled it is
    **`FILD qword` / `FSQRT` / ftol**. Taken at face value the length function would have had **no square
    root in it at all** and would still have returned plausible numbers. Nine callers across the module.
  - **The three precision ranges keep the squares in range**, not speed: under `0x1000` the squares stand;
    wider values shift down 8 or 18, measure, shift back. The root's argument is **unsigned** (the
    original zeroes the high half of a stack qword before `FILD`) - reproduced, but **no guarded range
    needs the 32nd bit**, and a draft comment of mine that implied otherwise was corrected before commit.
  - **TWO THINGS THAT MATTER FOR (c):** dividing by `One` is an **identity**, and the steering step does
    exactly that every tick - so **"divide by the mass" divides by nothing and there is no mass field**;
    and the eight-sided measure `|x| + |y| - (smaller >> 1)` is now a method rather than an idiom repeated
    in four places.
  - **Controls G and H, predictions first, BOTH EXACT.** G shifted the middle range **back** by 7 and
    failed exactly the two tests reaching it - including `TheEasyLengths`' SECOND assertion while its
    first still passed, zero being in the nearest range. **Choosing it took a check first:** shifting
    **down** by the wrong amount changes nothing, because every test value is a 3-4-5 triangle scaled by
    a power of two and stays exact either way. H read the root signed and failed exactly its one test,
    0 against 65535. **Tally: six exact (A B C D G H), one half (E), one wrong (F) out of eight.**
- **>>> THE JOIN BETWEEN THE PATHFINDER AND A PEEP IS `FUN_0050f8e0` - and it confirms the route record a
  FOURTH time. <<<** It fills a record with **count at `+0`, start at `+2`, goal at `+4`**, calls
  **`FUN_00511420`** (the entry now built as `CellSearch` + `CellReroute`), and on failure sets
  `+0x60 = 1` (arrived) and `+0xb8 = 1` (gave up). On success it copies the route into the peep at
  `+0x64 + i*8` as **`(cell << 16) + 0x8000` - cell CENTRES, not corners** - and carries only
  **`min(count, 5)`** waypoints at a time (`+0x54` full count, `+0x58` carried). `+0xa0` is the whole
  route's octagonal length, `+0xa4` the carried part with each leg at `+0x8c + i*4`, `+0xa8` the rest.
  **`+0x48` IS A VERSION STAMP, NOT A HEIGHT:** `FUN_004d8c50` is just `DAT_007cdb98++` and
  `FUN_004d8cd0` reads a 33x33 grid of stamps, so "The ground changed under this peep" is a staleness
  check. **The peep's stuck bits at `+0xb0` are a 15-tick history** - `follow_path` counts them and
  renavigates above `0x6665` (~40% of the last 15 ticks), logging "A peep became stuck and renaviga..."
  and, if that fails, "Poople flops! That peep seems to...".
- **>>> ROUTE-FOLLOWING IS DONE - `89b8dd0` `PeepJourney.cs` + tests, 9 tests. PUSHED. <<<** Fifth
  commit on `alexah/85-the-slack-in-a-route`. **127/0, 449/449, built ALONE in a worktree**, `save/`
  byte-identical. The **back half** of `follow_path`: which waypoint to aim at, when to step on, when to
  call it arrived, and how to close on the end.
  - **THREE FACTS UNREADABLE FROM THE DECOMPILED FORM**, all three `thiscall` helpers with `ECX`
    dropped: aiming at a waypoint loads `ECX` with the **target** (so the force is target-minus-position,
    not the reverse); the last leg does the same then scales; and with no route left `ECX` is the
    **velocity**, so the force is a **brake**. Reversing the first gives a person who walks away from
    every waypoint.
  - **TWO THRESHOLDS, DELIBERATELY DIFFERENT:** a middle waypoint is passed at **2 radii**
    (`0x20000`), the destination reached at **1.6 radii** (`0x19999`), both by the eight-sided measure.
    **Arriving does not stop the force being computed** - the flag is set and it falls straight through,
    so the arriving tick still gets a real force and only the next one brakes.
  - **STANDING EXACTLY ON THE END GIVES NO FORCE, NOT A BRAKE** - the original writes two zeroes and
    returns before reaching the subtraction. Control J exists because that is the single most likely
    thing to be "tidied" into subtracting the velocity like everywhere else.
  - **>>> A DEFECT OF MINE CAUGHT BY HAND-TRACING A TEST BEFORE RUNNING IT. <<<** The original carries
    at most **five** waypoints and navigates again when they run out - and navigating again restocks the
    list **and resets the index to zero**. I had given the index a private setter, so nothing standing in
    for that call could reset it, and the re-read straight after would have walked off the end. Fixed in
    the model (settable index, named defensive guard), not in the test.
  - **Controls I and J, predictions first, BOTH EXACT** (`control-89-outcome.txt`). I reversed the delta
    and failed exactly three tests, **each on the assertion the prediction named** - two of them on their
    LAST assertion, with the index and leg-length checks before it still passing, because stepping on is
    decided by a distance with no sign. J failed exactly one test at the exact predicted value.
    **TALLY: EIGHT EXACT (A B C D G H I J), ONE HALF (E), ONE WRONG (F), out of ten.**
  - **NOT BUILT, named rather than half-done:** the front of the same function - the ground-staleness
    check, the speed factor, and the 15-tick stuck history that decides to renavigate.
- **>>> `avoid_walls` (`FUN_0050e4a0`, 2,100 bytes) IS DECODED BUT NOT BUILT - this is the next piece.
  <<<** It takes the **lookahead** point `FUN_0050f870(peep, out, 1.0)` = **position + velocity**, takes
  its offset from the current position, normalises it (`FUN_0050e080` unless the length is zero), and
  derives four sign flags. For each axis it probes **`FUN_004d8750` - the edge test already built as
  `CellEdge`** - pushing the `StepDirection` (`0`/`1`/`2`/`3`); when a side is shut it builds the wall
  line with `FUN_005104e0` (`cell << 16`), backs off by the peep's radius `+0x04`, and if the peep is
  past it repels by **twice the overlap** (`SHL ECX,0x1`). Then four diagonal cases, each probing two
  edges of the diagonal neighbour before falling to **`FUN_0050cc20`**, which is a **ray/circle
  intersection** - it solves `b^2 - 4ac`, takes `FUN_004d49a0` of the discriminant, and answers the
  nearer root halved, or `-0x80000000` when there is no real root.
- **>>> THE RAY/CIRCLE SOLVER IS DONE - `67c954a` `RayCircle.cs` + tests, 6 tests. PUSHED. <<<**
  Sixth commit on `alexah/85-the-slack-in-a-route`. **127/0, 455/455, built ALONE in a worktree**,
  `save/` byte-identical. Three things about it are not what anyone would write, and all three are
  reproduced rather than tidied:
  - **IT HALVES INSTEAD OF DIVIDING BY `2a`**, so it is only correct for a **unit** direction. Its one
    caller does normalise first, so the assumption holds where it is used - but it is an assumption.
  - **>>> ITS CALLER TESTS THE WRONG VALUE FOR A MISS - THE TEST IS DEAD CODE. <<<** The solver answers
    **`0x80000000`** (`0050cd2a`), and `avoid_walls` compares against **`0x7fffffff`** (`0050e83b`),
    which it can never be. It costs nothing ONLY because the very next check, `TEST EAX,EAX; JLE`,
    catches the negative anyway. Know this before deciding the comparison "looks wrong" and fixing it.
  - **THE SQUARE ROOT'S SCALING DOES NOT MATCH WHAT IT IS ADDED TO.** Every product is shifted back by
    16 so the discriminant is fixed-point, but the root is taken of it directly. Measured, not asserted:
    a line fired at a circle one cell across whose centre is four cells away answers **261888 = 3.996
    cells**, where the near edge is at three. **Whether that is intended is NOT established**, so the
    test pins the number and says plainly it is not the geometric answer.
  - Also: it answers the **nearer** root (which may be behind you - the caller rejects that), and a
    **graze counts as a miss** because the discriminant must be strictly positive.
  - **Controls K and L, predictions first, BOTH EXACT.** K let a graze count as a hit and failed exactly
    the one test whose discriminant is precisely zero (262144 against the miss). L took the further root
    and failed exactly the one test pinning a value (262400 against 261888), on its first assertion. **A
    third was worked out and NOT run:** `>> 1` versus `/ 2` differ only for negative **odd** numbers, and
    the only negative answer here is even. **TALLY: TEN EXACT (A B C D G H I J K L), ONE HALF (E), ONE
    WRONG (F), out of twelve.**
- **>>> THE FOUR DIAGONAL QUADRANTS OF `avoid_walls` ARE NOT A MIRROR OF ONE ANOTHER - CHECKED, NOT
  ASSUMED. <<<** Dumping every `FUN_004d8750` call with the direction constant it pushes gives:
  **axis probes `1`(E), `3`(W), `2`(S), `0`(N) in that order**; then
  **`+x+y`: `0,3` then `0,3`** &middot; **`-x+y`: `0,1` then `0,1`** &middot;
  **`-x-y`: `2,1` then `1,2`** &middot; **`+x-y`: `2,3` then `3,2`**. The last two list their second pair
  in the opposite order. **>>> AN OVERCLAIM OF MINE, CAUGHT WHILE DESIGNING A CONTROL FOR IT AND
  CORRECTED IN BOTH THE CODE AND HERE: that swap is NOT observable. <<<** I had written that deriving
  them by symmetry "would have pushed a person the wrong way round two corners in four". It would not:
  **both probes in that second pair are refusals, so the pair is an AND and the order cannot change the
  answer**; the first pair is an OR and is equally order-blind. What would really go wrong is getting a
  **cell** or a **side** wrong - so those are what the tests pin. Also: the sign flags make the quadrants
  **mutually exclusive**, so although the original cascades through four blocks, exactly one can ever run.
  **Designing that control also exposed a real gap:** the first ten tests all exercised only the
  **down-right** quadrant, so nothing covered an upward one at all. A test was added before controlling -
  **and it immediately caught a REAL BUG in my own code.**
- **>>> THE CELL A CORNER BELONGS TO IS NOT WHERE THE CORNER IS - I had three of four wrong. <<<** A
  cell's position is its **low-numbered** corner, so the point shared with a diagonal neighbour is that
  neighbour's own position **only when the neighbour is the higher-numbered one** - going down and to the
  right. Going up, or left, the shared point belongs to the cell being stood in. The rule, verified
  against all four quadrants: **`x = west ? cellX : cellX+1`, `y = north ? cellY : cellY+1`**, which is
  **not** the diagonal cell's coordinates. The original makes this plain if you look: the down-right case
  builds the point with two `INC`s (`0050e806`), while the up-left case at **`0050eab3` uses the standing
  cell's own coordinates with no adjustment at all**.
  **WHY IT WAS INVISIBLE:** getting it wrong puts the corner a whole cell away, and it is then judged
  **out of reach** and silently produces no push - a silence, not a wrong shove. The failing assertion
  was `AreNotEqual(Zero)` getting Zero, with `along` = 104,216 against a reach of 92,672. **Only the
  down-right quadrant was covered, and that is the one case where the wrong rule happens to be right.**
- **>>> WALL AVOIDANCE IS DONE - `104ce02` `WallAvoidance.cs` + tests, 11 tests. PUSHED. <<<**
  Seventh commit on `alexah/85-the-slack-in-a-route`. **127/0, 466/466, built ALONE in a worktree**,
  `save/` byte-identical. Look one step ahead, work in that direction, ask `CellEdge` about the four
  sides of the cell being stood in, and if the side being walked towards is shut and the person is
  nearer than their own radius, push them out by **twice** the overlap. **First push wins** (east, west,
  south, north) - a person wedged in an inside corner is answered about one wall only per tick. Only if
  none pushes is a corner considered, and **at most one can ever run** because the sign flags are
  exclusive. A corner needs one of the diagonal cell's near sides shut, **both** ways past it open, and
  the ray to meet the circle within the look-ahead.
  - **THE DEAD `0x7fffffff` TEST IS DELIBERATELY NOT REPRODUCED** - see the `RayCircle` entry. Keeping
    it would suggest it did something.
  - **Controls M and N, predictions first, BOTH EXACT.** M put the corner back at the diagonal cell's
    position - the bug exactly as it was - and failed **only** the one test that goes the other way
    round, with every down-right test untouched, which is the prediction's whole point. N tried south
    before east and failed **only** the test built to observe that order, at both exact vectors.
    M's warning count was reasoned out beforehand (unused **parameters** draw no C# warning, unlike
    unused locals) after E was wrong about exactly that kind of side effect earlier.
    **TALLY: TWELVE EXACT (A B C D G H I J K L M N), ONE HALF (E), ONE WRONG (F), out of fourteen.**
- **>>> A CORRECTION TO THIS FILE'S OWN DESCRIPTION OF THE STEERING STEP: IT DOES NOT ALWAYS "SHIFT THE
  STUCK BITS TWICE". <<<** Re-read at `0050f54d`-`0050f5d6`: on the **illegal** path it does
  `bits = (bits << 1) | 1` and then shifts again at the end, so twice. On the **legal** path the first
  shift does not happen at all - the position is simply committed - so it shifts **once**. The second
  shift, with bit 0 set when `FUN_0050fd40`'s progress measure did **not** improve (`JG` skips the
  `OR`), always happens. So a walking person accumulates one bit per tick and a blocked one accumulates
  two, which is what makes the 15-tick history reach 40% so quickly when a peep is truly stuck.
- **>>> THE STEERING STEP IS DONE - `3da45b6` `PeepSteering.cs` + tests, 12 tests. PUSHED.
  ITEM 2(c) IS NOW COMPLETE. <<<** Eighth commit on `alexah/85-the-slack-in-a-route`. **127/0,
  478/478, built ALONE in a worktree**, `save/` byte-identical.
  - **THE CLAMP COMES BEFORE THE WEIGHT**, so a behaviour cannot buy extra say by asking for more than
    the limit. **That ordering is nearly invisible:** both orders agree at full weight AND for any force
    already under the limit, so telling them apart needs a force **above** the limit at a weight
    **below** one. Nothing covered that until control O was designed and found to break nothing - a
    twelfth test was added first. **Third time this session a control's design exposed a coverage gap.**
  - **`separation` is named and NOT built** (weight 0.1; needs per-cell thing lists) - a known gap
    rather than a silent zero. The two that are built are the two that matter for one person walking.
  - **Controls O and P, predictions first, BOTH EXACT.** **TALLY: FOURTEEN EXACT (A B C D G H I J K L
    M N O P), ONE HALF (E), ONE WRONG (F), out of sixteen.** Outcomes in `control-92-outcome.txt`.
- **>>> ALL EIGHT PUSHED 2026-09-16 at a fresh yes ("Yes, please push and continue your workflow").
  `alexah/85-the-slack-in-a-route` = `3da45b6`, NEW on origin. THAT YES IS SPENT. <<<** **ONE named
  refspec, no force, no PR** - GitHub offered `pull/new/alexah/85-the-slack-in-a-route` and it was
  **NOT used**. The branch was **new on origin**, so the before-check asserted its **ABSENCE** rather
  than a tip, and every check was a hard assertion that would have aborted before any network write:
  branch absent (0), base `b196e3d` present on origin (1), exactly 8 commits over it, 0 `refs/pull/*`,
  `main` identical on local/origin/upstream, tree holding only the four never-commit paths, one
  worktree. After: **`local == remote` at `3da45b6`, 0 ahead, 0 `refs/pull/*`**, `main` still `453e779`
  everywhere, **branch 84 untouched at `b196e3d`**, one worktree.
  **NOTHING IS OWED TO THE DOCS REPO:** checked rather than assumed - `OpenTPW.FileFormats` is clean,
  **0 ahead** on `docs/save-person-record` (`28a5400`), `master` still `0e8d5d0`, and **upstream PR #1
  is still on `docs/md2-sgn-and-lobby-scripts` at `d73a627`**, untouched. **No file format was
  confirmed this session** - everything decoded was in-memory behaviour, not a disk layout - so the
  same-session docs rule was not triggered.
- **(d) WIRE IT UP - >>> THIS ENTRY SAID "states 0, 7, 0xc, 0x14". IT IS **ELEVEN** STATES, AND THIS
  LINE IS WHERE TODAY'S WORST BUG CAME FROM. CORRECTED `5940fbc`. <<<** The eleven are **0, 2, 5, 7,
  9, 10, 12, 13, 15, 18, 20**. **GUESTS NOW WALK.**
  **WHY THE FOUR WERE WRONG, because the shape of the mistake is the reusable part:** 0x00, 0x07, 0x0c
  and 0x14 are the states whose blocks call `FUN_004fa2a0` **inside the body of `FUN_005019f0`
  itself**. The other seven do not call it there - they jump to a handler that calls it one level
  down: `FUN_004ff730` (2 HeadingForGate), `FUN_004ffb20` (5 Entering), `FUN_004fff20` (9),
  `FUN_004ffbc0` (10), `FUN_005006b0` (13), `FUN_00500900` (15), `FUN_00500a50` (18).
  **"Mapping the 22 blocks" is NOT the same as computing what each block can REACH**, and this entry's
  own note claiming it had mapped all 22 is exactly how the error survived a correction. The right
  instrument is a reachability scan over the handlers, which took ten lines.
  **THE COST WAS TOTAL, NOT PARTIAL:** every guest in the shipped park is in `HeadingForGate`,
  `Entering` or `WaitingForOpening`, and the first two are in the missed seven - so the first build of
  the walk was green and moved **0 of 13**. Cross-check that would have caught it instantly:
  `Peep.AnimationFor`'s walk group, written in an earlier session from the other direction, lists ten
  of the eleven. **0x07 additionally calls `FUN_004f9490`**, which is what navigates.
  **>>> A CORRECTION TO A CORRECTION OF MINE, AND THE WORSE ERROR WAS THE SECOND ONE. <<<** I briefly
  wrote here that this item was "REFUTED" because no state calls `FUN_0050f8e0` (renavigate) directly.
  **That narrow fact is true and the refutation drawn from it was wrong** - the states reach movement
  through wrappers, which is not the same as not reaching it. Caught by mapping all 22 blocks instead
  of stopping at one reference list. **Do not re-derive "the states do not navigate" from the
  renavigate call sites alone.**
  **THE CHAIN, as established:** state -> `FUN_004fa2a0` -> `thunk_FUN_0050f3b0` -> the steering step;
  and state 0x07 -> `FUN_004f9490` -> `FUN_00510100` = `peep->NavigateTo(target, addCurrent: 0)` ->
  renavigate. **Renavigate's own four call sites are three inside `follow_path`** (`0050ed79`
  ground-staleness, `0050ee79` stuck, `0050efce` carried waypoints exhausted) **and one in
  `FUN_00510100`** - so it is reached by the walking half itself, not by the state machine.
  **THE STATE MACHINE:** `FUN_005019f0`, 860 bytes, jump table at **`0x501d4c`**, bounded by
  `CMP EAX,0x15` - **22 states, 0..0x15**, every target an inline block inside the same function. It is
  called from `FUN_0050b360` (`0050b3d8`), the per-thing dispatch.
  **`FUN_004fa2a0` (436 bytes) does more than step:** it steps, converts the position for the renderer
  (`FUN_0050b6a0`), asks the speed factor, works out a facing with `FUN_006e7074`, and picks a walk
  sound by distance. Only the first of those is built.
  **>>> THE THREE `NavigateTo` CALL SITES IN `FUN_004f9490`, AND THE ONE THING THAT REALLY MATTERS. <<<**
  At `004f99db`, `004f9b91` and `004f9d0b`, each stores a 16-bit destination into `+0x18`/`+0x1a`,
  **sign-extends** it (`MOVSX`, so negative values survive), shifts left by **8**, and calls with
  **`ECX = peep + 0xd4`**. Each then tests the answer: zero means the search failed, and the caller
  branches on it.
  **>>> THE NAVIGATOR IS A SUB-OBJECT AT `peep + 0xd4`, AND EVERY OFFSET RECORDED ABOVE AS A "PEEP
  FIELD" IS RELATIVE TO IT, NOT TO THE THING. <<<** `FUN_0050f8e0` does `MOV ESI,ECX` and then reads
  the position from `[ESI+8]`; the call sites pass `peep + 0xd4` as that `ECX`. So `+0x08` position,
  `+0x10` velocity, `+0x18` max_force, `+0x1c` max_speed, `+0x4c/0x50` target, `+0x54/0x58/0x5c`
  counts, `+0x64+` waypoints, `+0xa0/a4/a8` distances, `+0xac` progress, `+0xb0` stuck bits and `+0xb4`
  the edge-test flag are all **navigator-relative**. The built code is unaffected - none of it uses
  absolute offsets - but the field map is mislabelled wherever it says "the peep's".
  **>>> THE SCALE IS SETTLED: `+0x18`/`+0x1a` HOLD THE DESTINATION IN 8.8, CELL IN THE HIGH BYTE. <<<**
  Each word is assembled from two byte stores: the **high** byte is a cell coordinate and the **low**
  byte a sub-cell fraction. `CL = (byte [ESP+0x28] - 1) & 0x7f` and `DL = (dword [ESP+0x28] - 1) >> 7`
  are exactly **`MapStep.CellAt`** - index from one, `x = index & 0x7f`, `y = index >> 7` - while `BL`
  and `AL` are separately clamped to **5..0x7b**. `MOVSX` then `SHL ...,8` promotes 8.8 to the **16.16**
  that `FUN_0050f8e0` reads back with `SAR ...,0x10`, and the sign extension is what lets a negative
  survive. Pushed **y then x**, so x is the first argument - matching `FUN_00510100` and the
  `*param_2`/`param_2[1]` order at the far end.
  **>>> HOW THIS WAS NEARLY GOT WRONG THREE TIMES, worth keeping as method. <<<** (1) I first called the
  two scales contradictory; (2) then asserted 8.8 as settled when nothing supported it; (3) then named
  `FUN_004fa670` as the decisive read - **it is not, it is the "stranded" logger** (`+0x198` is a
  stranded timestamp, log string *"Peep %d stranded at time %d"*). What actually settled it was reading
  the stretch by eye: **a linear stack-depth annotation LIED here**, showing depth -16 at the writes and
  -20 at the reads, because it does not model **callee cleanup** - the single `PUSH EBP` between them is
  balanced by `FUN_004fa670` being `__thiscall` and cleaning its own stack argument. The slots line up.
  **A depth tool that ignores `RET n` will mis-pair every write and read across a thiscall.**
- **>>> I REBUILT WORK THAT ALREADY EXISTED. `PeepNavigator.cs` WAS ALREADY IN THE TREE. <<<** It was
  built in an earlier session and is already pushed on branch 84. **I listed that folder at the start of
  this session and did not read it.** Measured extent, stated precisely rather than dramatically:
  - **ONE exact code duplication:** the octagonal metric, character-for-character identical at
    `FixedVector.cs:92` and `PeepNavigator.cs:122`.
  - **ONE duplicated constant:** `0x19999` as both `PeepJourney.ArriveWithin` and
    `PeepNavigator.LastLegTolerance`. `0x20000` is written as `2 * One` in the older file, so it is the
    same quantity expressed twice rather than a literal clash.
  - **NOT duplicated, and I first said it was:** `WaypointCentre`. Only `PeepNavigator` implements it;
    I recorded `(cell << 16) + 0x8000` as a finding and never wrote a second copy.
  - **Conceptual overlap only** (different code, same job): `StepTowards` against `PeepJourney`'s
    cursor advance, and `StuckBits` in both `PeepNavigator` and `PeepSteering`.
  - **ONE CONTRADICTION BETWEEN TWO FILES IN THE TREE:** `PeepNavigator`'s comment says the stuck word
    is shifted "**twice** per step". The disassembly says once on a taken step and twice on a refused
    one. Its *code* shifts once per call, so only the comment is wrong - **fix the comment, not the
    code**.
  - **BOTH HALVES ARE DORMANT:** `PeepNavigator` has **zero** references outside its own file and tests,
    and `ParkPeople` still constructs only `Peep`. So nothing is broken by the overlap today.
  - **CORROBORATION WORTH KEEPING:** the save reader's `NavigatorState.SubpathSlots = 5` independently
    confirms the "carries at most five waypoints" finding taken from `FUN_0050f8e0`, from a different
    session and a different source.
  - **THE LESSON, and it is the cheap one:** *list* is not *read*. Grep the target folder for existing
    work before decoding a function, not after building a replacement for it.
- **>>> THE DUPLICATION IS RESOLVED - `30f2e72` "Keep one copy of the measure both halves were using".
  PUSHED, one ahead of origin. <<<** **127/0, 478 before AND after** (the point: nothing observable
  changes), built ALONE in a worktree, `save/` byte-identical. Three edits:
  - **`PeepNavigator.Distance` now delegates to `FixedVector.OctagonalLength`** - one implementation.
    **The delegation runs the OLDER way round on purpose**, so the older file's tests cover the shared
    code: they pin the metric against Manhattan **and** Euclidean with truncating cases, and against
    three real legs from Lost Kingdom's save while stating plainly that all three run along an axis and
    therefore cannot exercise the halving term. That honesty beats my newer single-diagonal test.
  - **`PeepJourney.ArriveWithin`/`PassWithin` now NAME `PeepNavigator.LastLegTolerance`/
    `MidPathTolerance`** instead of repeating the literals. **Checked before unifying that they are the
    same QUANTITY and not merely the same NUMBER:** `NavigatorState.One = 65536`, so `2 * One` is
    exactly `0x20000`. **`SlowingOver` is deliberately NOT unified** though it is also `0x20000` - it is
    the last-leg divisor, a different thing sharing a value, and collapsing those is its own bug.
  - **The "twice per step" comment is corrected** (`0050f54d`-`0050f5d6`). **THAT OWED ITEM IS NOW
    CLOSED** - the tree no longer contradicts itself.
  - **CORROBORATION WORTH KEEPING:** `NavigatorState`'s own comment on `DefaultMass` already said the
    steering loop "divides the summed force by a literal 1.0 rather than by this field" - written in a
    different session from a different reading, and it independently confirms this branch's
    divide-by-one finding.
  - **Controls Q and R, predictions first. Because the commit changes nothing observable, both break
    the SHARED thing and predict failures on BOTH sides** - which is what proves the unification took.
    **Q exact** (one failure in each test class; every axis-only test survived, including the
    shipped-legs test exactly as its own documentation predicts of itself). **R: failure set exact, but
    one number in my prediction was wrong** - I wrote the tolerance would read 19661 and it reads
    **19660** (`(0x18000 * 13107) >> 16` floors from 19,660.5). Noticed and said BEFORE running, and the
    prediction file still carries the wrong figure as written.
  - **A RESTORE METHOD WORTH REUSING:** control Q mutated `FixedVector.cs`, which was **not** in that
    round's backup. Rather than `git checkout --` (banned here), the exact substitution was **reversed**
    and then **proved** with `git diff --quiet` against HEAD - a positive check needing no backup.
  - **TALLY AT THAT POINT: fifteen fully exact, TWO exact-set-one-value-wrong (E, R), one wrong (F), of
    eighteen.** (Now seventeen of twenty - see the join below.)
  **AN INSTRUMENT THAT PROVED WORTHLESS, said so it is not repeated:** scanning the whole image for
  16-bit accesses at `+0x18`/`+0x1a` returned UI code and unrelated stack slots. Nothing was drawn from
  it; a field offset means nothing without knowing which object it is an offset into.
- **>>> THE NAVIGATOR JOIN IS DONE - `78639eb` "Give every guest the route the save gave them".
  PUSHED, TWO ahead of origin with `30f2e72`. <<<** **127/0, 478 -> 479**, built ALONE in a
  throwaway worktree, `save/` byte-identical at `23a4922d649e799d6dbfa089e9168a72`. Five files:
  `Peep.cs`, `ParkPeople.cs` and three test files.
  - **What it fixes:** `PeepNavigator` had been built, tested and **completely dormant** - nothing in
    the tree constructed one from real data, and `ParkPeople` was dropping `person.Navigator` on the
    floor while building each guest's running copy.
  - **The navigator is REQUIRED, not optional, and that was the one real design call.** The save
    reader parses one for every person unconditionally - its own comment says "every person has one,
    staff included" - so a guest without one is a state the file cannot produce. Optional would have
    spared two test helpers at the cost of modelling something that does not exist; defaulting would
    have invented state. The two helpers gained an inline `StandingStill` navigator each, deliberately
    NOT shared between the files: it is test data, and data reads better at the point of use.
  - **The test quotes no outside measurement.** Every field is checked against the save reader's own
    `Navigator` for the same person (rule forty-two), so what is pinned is that the value travelled
    intact rather than that it matches a figure written down elsewhere. Plus a relation the code
    derives for itself - `BufferedFor(TotalWaypoints) == BufferedWaypoints` - and a distinct-object
    count.
  - **Controls S and T, predictions first, both exact.** **S** gave every guest a navigator from
    nowhere: predicted the radius assertion, 13107 against 0, and **deliberately did not predict the
    guest id** because the loop runs over a dictionary whose order nothing had established. It came
    out 42. **T** gave every guest the NEXT guest's navigator - every one real and distinct, only the
    pairing wrong - so only the SHAPE was predictable: a field assertion, and **not** the radius one,
    since every person carries the same `DefaultRadius`. It failed on "guest 42 route length", 545989
    against 270172.
  - **T ANSWERED AN OPEN QUESTION I HAD FLAGGED BEFORE RUNNING IT:** I wrote that if nothing failed,
    that would itself be the finding - it would mean the shipped guests' navigators are
    indistinguishable and the per-field assertions prove far less than they look like they do.
    Something did fail, so those routes genuinely differ and the assertions discriminate.
  - **SAID IN ADVANCE AND STILL TRUE:** the "no two guests share a navigator object" assertion
    **cannot be isolated by a control**. Any mutation that shares one object also mismatches fields,
    and a field assertion runs first, so such a control would fail for the wrong reason.
  - **TALLY NOW: SEVENTEEN fully exact, TWO exact-set-one-value-wrong (E, R), one wrong (F), of
    TWENTY.** Full record: `~/.cache/tpw-harnesses/control-94-outcome.txt`.
- **>>> THE LIVE MAP BINDING IS DONE - `c9c9d4c` "Ask the real park whether a way is shut".
  PUSHED, THREE ahead of origin. <<<** **127/0, 483 -> 484**, built ALONE in a throwaway worktree,
  `save/` byte-identical. Two files: `CellEdge.cs` and the new `CellEdgeOnTheMapTests.cs` (5 tests).
  - **What it fixes:** `CellEdge` had existed since branch 84 **with no caller anywhere but its own
    tests** - it takes the map as a parameter and nothing was passing one. `CellEdge.For( park, mode )`
    binds `ParkWorld.CellAt` **and** the track question.
  - **A STALE DOC SENTENCE CORRECTED, and the file had been contradicting itself.** The class remarks
    said the track record was one "the save reader does not yet parse". It does - `ParkWorld.cs:663-679`
    reads `TrackType`, `TrackFlags` and `TrackParentId` whenever the status byte says a track record
    follows - and another paragraph of the SAME file quoted measured counts over all 16,384 cells that
    could only have come from the parsed data the first paragraph denied having.
  - **`queueAhead` is still declined** and still answers `NothingThere`: it needs per-cell thing lists
    the engine does not have, and inventing which things stand where would be worse than not answering.
  - **A DERIVED CHAIN THAT HELD ON THE FIRST RUN** - 429 cells of track type 12 each naming one of 143
    of type 25, three apiece (572 reached); one cell carries the reopening nibble, freeing itself and
    its three children, leaving exactly the documented **568 closed**. Shape as well as totals: every
    parent named really is a counting cell, each named exactly three times, and no counting cell names
    a parent.
  - **>>> A COVERAGE GAP IN MY OWN TEST, FOUND BY DESIGNING THE CONTROL AND FIXED BEFORE COMMITTING.
    <<<** `TheTrackRecordClosesAllButTheOneFlaggedFamily` builds the bound edge, asserts it is not
    null, then calls `CellEdge.TrackCloses` with **its own local `byId`** - it never asks the bound edge
    anything. It proves the RULE and says nothing about whether `For` WIRES the rule in, which is the
    commit's entire claim. **Control V would have passed a fully green suite.** Closed by a fifth test,
    `BindingTheParkChangesWhatTheEdgeTestAnswers`. **This is the FOURTH time this session that designing
    a control exposed a gap rather than the control finding a bug.**
  - **MEASURED, NOT PREDICTED, AND SAID SO IN ADVANCE: 2,265** edges where the binding changes the
    answer. I could not derive it, and the prediction file set out beforehand what each outcome would
    mean - including that **zero** would have meant the binding was unobservable and the claim should
    not be made. Related but **NOT explained**: 568 x 4 = 2,272, seven more. Which seven is unknown; the
    test pins 2,265 and asserts only the bound that follows without knowing. It also pins that the track
    record may only ever **shut** a way, never open one.
  - **Controls U and V, predictions first, BOTH EXACT.** **U** made the parent lookup forget that saved
    references count from one: predicted to fail only the new test, on the count and not the invariant,
    "near 568 but near is the honest word" - it came out **exactly 568** (142 counting cells x 4, no
    wrong parent landing on a counting cell). **V** removed the track question from the factory:
    Expected 2265 Actual 0, exactly as predicted.
  - **>>> A NUMERICAL TRAP: control U's 568 and the binding's 568 CLOSED CELLS are the same number by
    unrelated routes** - 142x4 edges against 572-4 cells. Neither is evidence for the other. <<<
  - **TALLY NOW: NINETEEN fully exact, two exact-set-one-value-wrong (E, R), one wrong (F), of
    TWENTY-TWO.** Full record: `~/.cache/tpw-harnesses/control-95-outcome.txt`.
- **>>> AN OVERCLAIM OF MINE, FOUND AND CORRECTED - `80986a5` "Say what a guest actually carries, which
  is not the route". PUSHED, FOUR ahead. <<<** **127/0, 484 before AND after** (docs and one test
  name only), built ALONE in a worktree, `save/` byte-identical.
  - **What was wrong:** `78639eb`'s subject was "Give every guest the route the save gave them" and its
    test was `EveryGuestArrivesCarryingTheRouteTheSaveGaveThem`. **The waypoints ARE the route and they
    are not parsed** - so both named the one thing that is not there.
  - **What was NOT wrong:** every field that test checks is real, travels intact, and is still checked
    against the reader's own record for the same person. Rule forty-two was followed exactly and both
    controls were exact. **The assertions were soundlish; the LABEL was false.**
  - **Corrected in three places that repeated it:** `Peep.Navigator`, `PeepNavigator`'s class remarks
    (whose opening line said "the route a person is following" while a later paragraph of the SAME
    comment correctly said the pathfinder that fills the waypoints was still to come), and the field
    docs for `Cursor` and `BufferedWaypoints`, which described an index and a count as though the things
    they index and count were present. Test renamed
    `EveryGuestArrivesKnowingHowFarAlongTheirRouteTheyWere`.
  - **HOW I FOUND IT:** not by review, but by planning the NEXT step and asking where a guest's
    waypoints would come from. **Recorded as rule forty-six** in [[verify-every-ordering]]: rigour about
    the fields is not rigour about the claim, and the danger sign is a test whose assertions are all
    metadata - counts, lengths, indices, flags - while its name promises the payload.
  - **>>> CLOSED BY THE PUSH OF 2026-09-16 - DO NOT REOPEN. <<<** `78639eb`'s wrong subject is now in
    **published** history. Alexah was told before the push that pushing would settle this by default,
    and answered "Yes push please!" - so the record stands as the wrong subject with `80986a5` and
    `415df20` as visible corrections on top of it. **Rewriting published history to tidy this away would
    be worse than an honest correction trail: do not propose it.** Safety pointer
    `safety/95-before-correction` was removed once the correction was committed and an ancestor of HEAD.
- **>>> AND THE SWEEP THAT FIXED IT WAS ITSELF INCOMPLETE - `415df20` "Stop claiming a save can resume
  a walk". PUSHED, FIVE ahead. <<<** **127/0, 484 before AND after**, built ALONE, `save/`
  byte-identical. One file, one test name and its doc.
  - `80986a5` said it had fixed "the three places that repeated the claim". **There was a fourth**, and
    its wording was worse: `PeepNavigatorTests`' `ANavigatorStartsFromTheRouteTheSaveGaveIt` had the
    summary *"which is what lets a park resume mid-walk"* - **not a loose label but a false statement
    about a capability that cannot exist**, since there are no waypoints to resume along.
  - **WHY THE SWEEP MISSED IT, and it is dull and repeatable:** I searched for the spaced prose
    `the route the save gave`; the surviving site was a **C# method name in CamelCase, which has no
    spaces**, so the pattern could never match however often it ran. The same day a check of my own
    notes reported a phrase missing that was present, because I had written a word in `*emphasis*`
    markup mid-phrase. **Both are rule forty-three: a pattern blind to the FORMATTING of the text it
    searches.** Recorded as an addendum to that rule. Search every casing convention, or grep one
    distinctive word and read the hits.
  - **`ParkNavigatorStateTests` WAS CHECKED AND IS LEFT ALONE - it is honest.**
    `EveryRouteIsAsLongAsTheSaveSaysItWas` is accurate (`TotalDistance` IS the saved route length), and
    that file is the most careful of the set: it **marks its own tail/buffered assertions as NOT
    evidence**, because a column of zeros agrees with a misread block as readily as a correct one. It
    also corroborates control T from a different reading, quoting the same 545989 and 270172 for guests
    42 and 41. **I read it rather than assuming, and the answer was "fine" - which is the point.**
- **>>> A GUEST CAN SAY WHERE IT STANDS - `66e7562` "Let a guest say where they are standing".
  PUSHED, SIX ahead. <<<** **127/0, 484 -> 485**, built ALONE, `save/` byte-identical.
  `PeepNavigator` gains `Position`, `Velocity`, `Target`, `MaxSpeed`, `MaxForce`.
  - **THE COVERAGE CHECK THAT SHAPED THE TESTS, done BEFORE writing them** (the "list is not read"
    lesson applied): `ParkNavigatorStateTests` **already** pins position, velocity, max speed and max
    force against the shipped file in five tests - but every one asserts about
    **`ParkWorld.NavigatorState`, the READER's record**, and none about the running copy. So the new
    assertions compare the running copy to the reader's own record and **deliberately do not restate
    the file's numbers**, which would test the reader twice and the running copy not at all.
  - **Control W exact:** zeroing the carried position failed both new tests on their first position
    assertion, `(0,0)` against the real vector. Guest id again **not predicted** - it came out 42 for
    the third control running, which is three agreeing observations and **still not** knowledge of the
    dictionary's order.
  - **THE FALSIFICATION CONDITION I SET IN ADVANCE, AND WHAT IT SETTLED:** I wrote that if only ONE of
    the two failed they were not independent, and that if the synthetic one failed alone it would mean
    some guest really does stand at the origin. **Both failed**, so neither carries the other and no
    shipped guest is at the origin. The five `ParkNavigatorStateTests` passed untouched, which is what
    confirms the coverage argument rather than merely asserting it.
  - **A FIXTURE FACT WORTH NOT LOSING:** guest 42 stands in cell **(47, 9)**, and
    `PeepNavigatorTests`' synthetic `Saved()` helper carries a *different point in that same cell*. Its
    "made up" coordinates were taken from a real person in the shipped park - do not treat them as
    arbitrary and renumber them.
  - **TALLY NOW: TWENTY fully exact, two exact-set-one-value-wrong (E, R), one wrong (F), of
    TWENTY-THREE.** Record: `~/.cache/tpw-harnesses/control-98-outcome.txt`.
- **>>> ROUTE PLANNING IS DONE - `3d33a04` "Let a person plan a route across the park they are standing
  in". PUSHED 2026-09-16 at a fresh yes, `66e7562..3d33a04`, 0 ahead. <<<** **127/0, 485 -> 492, 0 skipped**,
  built **ALONE** in a throwaway worktree, `save/` byte-identical at `23a4922d...`. Three files:
  `PeepNavigator.cs`, `Peep.cs`, and the new `PeepRoutePlanningTests.cs` (7 tests). **This closes (ii-a).**
  - **It is `FUN_0050f8e0`, and it goes on `PeepNavigator` because that is what `this` IS** - the call
    sites pass `peep + 0xd4`, and every field it writes is already a field of that class. **The invented
    `PeepNavigator.NavigateTo` from an earlier session now exists for real**; the warning further up this
    file about planning against that name was right at the time and is no longer live.
  - **>>> THE VERDICT BELONGS TO THE SEARCH, NOT THE STRAIGHTENING PASS - and the call graph suggests
    otherwise. <<<** `FUN_00511420` calls the search, hands its answer to `FUN_005108a0` **as an
    argument**, and returns what that gives back - and `FUN_005108a0` returns that same argument
    **untouched at BOTH of its RETs** (`[ESP+0x244]` at `00510a1d` and `005110cc`). `0x70000000` is
    written only inside `FUN_00511470` itself (`00511bff`, `00511c5c`). **So `CellReroute.Run` returning
    `void` is FAITHFUL, not a gap** - which is what I had gone looking to check, expecting a gap.
  - **`addCurrent` IS LIVE, not dead: 2 of the 4 call sites pass 1.** `follow_path` passes 1 for
    ground-staleness (`0050ed74`) and for stuck (`0050ee74`), 0 for exhausted waypoints (`0050efc9`);
    `FUN_00510100`, which the state machine reaches, passes 0. All three `follow_path` sites pass
    `ESI+0x4c` - the navigator's OWN stored target - so renavigating re-uses the recorded destination.
  - **THE THREE DISTANCES SPLIT A ROUTE, they do not each measure it:**
    `Total = firstLeg + Buffered + Tail`. Only the total includes the walk from where the person stands
    to the first waypoint. The first leg is `waypoint - position`, settled from `ECX` at
    `0050fa41`-`0050fa49` (the decompiler drops it) - **the same reversal that had to be read by hand for
    `PeepJourney`**.
  - **MEASURED ON LOST KINGDOM, NOT PREDICTED.** All **13** guests route to the destination the save
    already carried, so routing needs **no destination-choosing behaviour**. Over all **6,006** pairs of
    path cells: **mode 0 reaches 5,951** with **9** routes past the five slots; **mode 2 reaches 5,905**
    with **none**. That difference is the evidence for taking `mode` as a parameter, and it runs the way
    `CellEdge` describes - mode 2 opens path-to-open-ground, so people cut across grass and need fewer
    corners.
  - **>>> A WEAK RESULT CAUGHT BEFORE IT WAS PINNED AS A STRONG ONE. <<<** All 13 guests come back with
    **one waypoint** - they are queued in the entrance avenue on open approach cells, and a clear run
    records no corners. So that test proves the WIRING reaches real data and says nothing about
    cornering, and it asserts the count of 1 so the limitation is pinned rather than discovered later.
    The buffering split is exercised by a separate route across the park, (57,15) to (43,29): 7
    waypoints, 5 carried, `TD=1769472 BD=1605632 TL=98304`, first leg 65536.
  - **ONLY THREE ROUTES IN THE WHOLE NETWORK HAVE A NON-ZERO TAIL**, and the test pins **3** rather than
    "some route does". Nine routes exceed five waypoints but **six of those end on a repeated waypoint
    whose leg is zero** - the same duplicate-goal behaviour `CellSearchTests` already pins. An
    identity of three numbers is satisfied trivially when two are zero, so the guard matters more here
    than the identity.
  - **A STALE DOC OF MY OWN, CORRECTED IN THE SAME COMMIT:** `Peep.Navigator` said the navigator does
    **NOT** hold the route. True of a navigator restored from a save, and made half-false by this very
    commit. Rewritten to draw the line where it falls: **the save carries bookkeeping without places, and
    planning is what puts places there.** Found by sweeping for the claim before shipping the capability
    that contradicts it - the `78639eb` failure mode run in reverse, caught this time.
  - **Controls X and Y, predictions written first, BOTH EXACT including WHICH assertion failed.** X
    stopped the give-up path clearing the old route: failed only that test, `Expected <0> Actual <4>`,
    proving it really destroys a 4-waypoint route rather than finding one absent. Y made `addCurrent`
    append instead of prepend: **passed** the count assertion and failed the next one,
    `Expected <(57, 15)> Actual <(56, 15)>` - which is the whole point, since appending also lengthens
    the route. Restores proved by **md5 of the file**, not `git diff --quiet`, because the tree
    legitimately differs from HEAD here. **A third mutation was reasoned out and NOT run:** reversing the
    first-leg difference changes nothing, as its only consumer takes absolute values - **so the first
    leg's SIGN is not covered by any test and cannot be**, said plainly.
  - **TALLY NOW: TWENTY-TWO fully exact, two exact-set-one-value-wrong (E, R), one wrong (F), of
    TWENTY-FIVE.** Records: `~/.cache/tpw-harnesses/control-99-{prediction,outcome}.txt`.
- **(e) THEN:** `separation`, which needs **per-cell thing lists - engine infrastructure OpenTPW does not
  have** - and then the 14 delegating states.
**IF ITEM 2 BLOCKS, FALL TO ITEM 3** rather than idling: it is pure Ghidra, and it opens the economy that
is already sitting in Lost Kingdom's save.

1. **>>> ITEM 1 IS DONE AND PUSHED, 2026-09-16 - branch 82 (`4f307bb`), and Alexah confirmed it on screen
   ("the river and the waterfall look fantastic!"). NEXT SESSION STARTS AT ITEM 2. <<<** Full record in the
   branch-82 entry below. What it was: the bare `{stem}M.md2` a model ships and `LobbyModel` never loaded -
   and **three claims this item used to make were wrong, all three measured on the day**: `LobbyModel.LoadAnimations`
   (`LobbyModel.cs:555-569`) loops `for (int suffix = 1; ; ++suffix)` over `{stem}M{suffix}.md2` and breaks
   at the first miss - **with no bare `{stem}M.md2` fallback**, which `RideAnimations.cs:404` got in branch
   72 and this never did. That part stands. What did not:
   - **The count is 197 over 312 archives / 445 base models**, not 191 over 306/1,219. `rolecensus.py` is
     the instrument and the older artifact's 197 was right all along.
   - **`AnimationFile.TryLoad` IS NOT IMPLICATED.** Of the 197, **0 are position-and-visibility-only**, so
     `:742` never turns one away. **160 carry rot/morph/uv** and would load through `TryLoad` untouched;
     **37 are EMPTY** - no track of any kind - so loading them changes nothing on screen, exactly like the
     two `lights.RSE` clips already on record. **So the fix is the bare fallback ALONE**, and it does not
     need the direct-read path `RideAnimations.Read` uses. (`~/.cache/tpw-harnesses/bare-m-tracks.py`.)
   - **"Lost Kingdom's Round Fountain is one of the 191" IS STALE.** The fountain is drawn by
     `ParkObjects.cs:180`, which has **supplied `RideAnimations.AllClips` since branch 75**, so it never
     reaches `LoadAnimations` and Alexah confirmed it moving on screen. Same for `ParkFixedItems.cs:141`.
   **WHAT ACTUALLY REACHES THE BROKEN PATH:** of the eight `new LobbyModel(...)` sites, only
   `ParkTerrain.cs:53` and `ParkQueues.cs:180` load park content without supplying clips (the other park
   sites supply; the three lobby sites and the advisor are unaffected - **0 of the 197 sit outside
   `levels/`**). For **Lost Kingdom that is ONE model: the terrain's `base`, which gains 11 UV tracks**
   (`basem.md2`, frames 0..100). Its queue gains nothing - jungle's `quebin1m`/`quebin2m`/`queendm` are all
   EMPTY, and `questra`/`quebnd2` (two of its four placed cells) have no `M` role at all. Space's queue
   clips ARE real, so the queue half pays off in another theme, not this one.
   **THE FIDELITY CASE IS VERIFIED IN GHIDRA, not assumed:** `FUN_00461f10` owns **both** role format
   strings - `'%s%s%c.md2'` at `0x004623df` and `'%s%s%c%d.md2'` at `0x004623b3` - so it really is the
   role-prober; and `FUN_004504c0` builds `'%s\Terrain'` and calls it with stem **`"Base"`** (`0x0074cf58`,
   read with `memory.getInt`), falling back to `"TestBase"`. So the original DOES probe the terrain and
   does load `basem.md2`. **A wrong turn on the way, worth not repeating: `FUN_004504c0` also passes
   `'base.md2'` to `FUN_00461a60`, and I briefly read "this function calls the prober" as "the prober is
   applied to base.md2". It is not - `FUN_00461a60` is quickload machinery. Co-occurrence in one function
   is not evidence.** Verify on screen; a park frame is deterministic ([[verifying-rendering-by-capture]]).
2. **THE PEEP SIMULATION. >>> STAGES 1-3 DONE AND PUSHED 2026-09-16: NINE commits, tip `3084eb6`, 361
   tests. Read the peep block below first. GUESTS DID NOT WALK AT THIS TIP - they do as of `5940fbc`;
   this entry records stage 1-3 and is left describing its own moment. <<<**
   `0x004f9000-0x00512000` = **281 functions / 95,152 bytes**, plus the queue module
   `0x004dd000-0x004e2000` = **89 / 19,684**. Entry is the thing tick **`FUN_00516380`** (mode-gated on
   `DAT_00fb3b7c`, every tick in a normal park), which walks the thing list calling `FUN_0050b360` per
   thing - and that is a **switch on the thing's MODEL byte at `+2`**, two calls per model: for a guest
   (model 1) `FUN_00501650` the needs tick, then `FUN_005019f0` the 22-state machine.
   **DO NOT write our own walker** - Alexah settled that on 2026-09-16 and I had proposed it by misreading
   [[ride-vm-may-be-replaced]]. Stage it the way the ride VM was staged over thirteen branches.
   The display half is already done: the sprite VM is fully decoded (block further down), the walk script
   is entry **42**.
   **TWO THINGS THIS ITEM USED TO SAY ARE WRONG, both corrected on the day.** `FUN_00510510` is **NOT
   "walk-speed"** - it is the one-cell step legality check (it owns "A peep is walking so fast that they
   tried to cross > 1 cell in one step"). And "peeps are virtual-dispatched" was right about the
   *navigator* and wrong about the tick: the tick dispatch is an ordinary static switch on the model
   byte. The genuine virtual dispatch is `FUN_0050f3b0` walking a **steering-behaviour list** at
   `DAT_007cf078..07c`.
3. **THEN: name the four unidentified manager things** - models **11, 13, 14, 19**. Cheap Ghidra work and
   it is what stands between us and **reading Lost Kingdom's economy, which is already in the save**
   (model 16: `mBalance`, `mAdmissionFee`, `mProfitThisYear`, `mLoans[...]`). **Their thin readers have no
   CALLs; the way in is the CALLBACK each case block passes to `FUN_00401000`** - and do NOT dump that
   pointer as a table, which is the mistake recorded in [[verify-every-ordering]]'s rule-36 addendum.
4. **ALONGSIDE, as filler:** review-plan phases **A and B** (verified still unbuilt - see
   [[codebase-review-and-plan]]; phase A's precondition "take a fresh `--no-incremental` baseline" is
   **satisfied: 127/0 on 2026-09-16**), plus three small truths - park button glints spawned/ticked/killed
   unseen (`Level.cs:94` vs `:347-360`), the cursor stuck on 1 of 22 types (`Input.CursorType` has no
   reader and no writer), and the **README is stale** (line 182 says 143 tests vs 361; line 157 repeats the
   210 that `RideVM.cs:42` divides by, contradicted at 106 by `OpenTPW.Files/Formats/Script/Opcode.cs`).

**>>> NOT DOING, and both were measured rather than assumed: <<<** the park HUD's four dead buttons (they
open screens needing economy data nothing can read yet) and the remaining 49 ride opcodes (the single-opcode
ladder is exhausted - the best one left unblocks two scripts).

**DO NOT READ THIS PARAGRAPH AS THE CURRENT STATE - the live block is the one marked `>>>` below.**
The "Priority 1 to 4" headings further down are the **OLD numbered priorities** (sky/weather/sound, the
game clock, scene release, the ride's name board), finished on branches 44 to 48 and kept only as a
record. **They are NOT the plan's P1-P5**, which are a different list entirely and are the ones that
matter now - a collision worth knowing about, because P4 has just been finished too and the two are
easily confused. This paragraph once ended "everything through branch 52 is now pushed", which was
eight branches out of date while sitting above the block that was current.
**Branches 44 through 48 are on origin** - 46, 47 and 48 went 2026-09-14 at Alexah's word ("You can go
ahead and push to my repos"), one named refspec each, **no PR**: GitHub offered a compare link for every
one and none was used. **That yes is spent ([[ask-before-github]]).**

**BRANCHES 49 TO 52 WERE PUSHED 2026-09-14** at Alexah's word ("Yes please push to my repos"), four
named refspecs one at a time, **no PR** - GitHub offered a compare link for branch 49 and it was not
used. **That yes is spent ([[ask-before-github]]).** Verified after: `local == remote` for all four
(`af157d8`, `7b0abf4`, `2018166`, `cd6894b`), **`main` still 453e779 on local, origin AND upstream**,
and **`refs/pull/*` on origin is empty**, so the fork still carries no pull request of its own. Every
commit on every one was built **ALONE** in a throwaway worktree at **127/0** with **143/143** first.

**>>> BRANCH 74 IS DONE AND PUSHED, 2026-09-15: `alexah/74-the-channel-a-script-triggers` = `96485b7`,
two commits over `c81bbde`. <<<** Pushed at Alexah's word ("Push both"), **one named refspec**, **no PR** -
GitHub offered `pull/new/alexah/74-the-channel-a-script-triggers` and it was **not** used. The branch was
**new on origin**, which is why the before-check asserts its ABSENCE rather than a tip. **That yes is spent -
the next push needs a fresh one** ([[ask-before-github]]). The before-check was written as hard assertions
that would have aborted both pushes had anything moved; after: `local == remote` at `96485b7`, **0 commits
ahead**, `main` still `453e779` on origin AND upstream, **0 `refs/pull/*` on origin**, one worktree, tree
holding only the four never-commit paths.
- **`f9897d7`** - `AnimTimeControl`, the engine's own name for the record (`FUN_00464580` prints every field
  by name), one per placed thing, hung off the `RideAnimations` instance `ParkRides` already loads per
  placement. Time is a **stamp, never an accumulator**, because continuity is expressed by moving the start
  stamp backwards (`FUN_00472bc0`). Strict finish, stale `DeferredFlags`, and a missing role **stops** the
  channel. One named departure: the engine bounds-checks no channel index and this refuses one.
- **`96485b7`** - `TRIGANIM`/`WAITANIM`/`LOOPANIM`/`FLUSHANIM` driven through it. **`WAITANIM` now starts
  the clip it waits for**, which three of Lost Kingdom's eight items depend on entirely, and a trigger onto
  a busy channel answers **remaining + new**, so the number a script is told stops equalling
  `DurationMilliseconds`.
**280/280, 0 skipped, 127/0, `save/` byte-identical.** Each commit built **ALONE** in a throwaway worktree
on `/home`. **Four control runs, predictions written first: 2, 3, 1, 1 - all exact after a correction.**
**TWO TEST-QUALITY DEFECTS FOUND BY THE CONTROLS, both mine:** control B's first run exposed a **tolerance
wider than the effect** (±0.05 against a 0.03 difference), so the clamp test would have passed under the
very mutation it exists to catch - tightened to ±0.005; and reasoning through control D's *code path*
showed the `WAITANIM` re-entry guard had **no test that could catch its removal**, because dropping it does
not restart the clip (the channel is busy, so the trigger queues) - the test now pins an **empty queue**
rather than the frame, which reads the same either way.
**Measured branch-point baseline: 269/269 passed, 0 skipped, 127 warnings / 0 errors, `save/` byte-identical
at `6135d2f4...`** (`~/.cache/tpw-harnesses/baseline-74.log`). **The suite is 269 and a delegate's
"actually 259" was wrong** - see new **rule twenty-four** in [[verify-every-ordering]].
**THE SCOPE IS THE STATE MACHINE, NOT THE POSING.** Branch 74 builds `AnimTimeControl` (the engine's own
name for the channel), one per placed thing, and drives `TRIGANIM`/`WAITANIM`/`LOOPANIM`/`FLUSHANIM`
through it. **Posing a clip onto geometry is branch 75** and is deliberately not attempted here: it needs
`MeshRotator` split into clock + `Pose` the way `MeshAnimator` already is, `LobbyModel` loading roles
through `RideAnimations`, a rest-pose restore that does not exist anywhere in the tree, a decision about
precedence against `ParkObjects.PoseAsBuilt`, and the two reader defects named in [[park-data-layout]].
**What MOVED when it landed:** `WAITANIM` starts a clip rather than only waiting (three of Lost
Kingdom's eight items run nothing else), and a trigger onto a busy channel answers remaining-time plus the
new clip, so what a script is told stops equalling `RideAnimations.DurationMilliseconds`.
**Verification target is the SECURITY CAMERA** - it self-drives a ~15s cycle for ever with no peeps and no
ride state. Two traps: the Drinks Shop's perpetual loop is **UV-only**, and Jungle Spray's three idle
channels play clips with **zero tracks**, so a motionless spray is correct.
**>>> ALEXAH CONFIRMED BRANCH 75 ON SCREEN, 2026-09-15: "EVERY animation works, the game looks GREAT!
Fountain looks great, security cameras look great, belly bounce looks great!" <<<** That is the one check
this project cannot run for itself - `LobbyModel` needs a device and **nothing outside `Level.cs` ever
constructs `ParkObjects`** - so the poser, the role binding and the per-frame sweep are confirmed end to
end by the only instrument that could confirm them. **Do not re-open the question of whether posing works.**

**>>> BRANCH 80 IS DONE AND PUSHED, 2026-09-16 - `alexah/80-the-park-draws-the-people-it-saved` =
`ef4f091`, FOUR commits over `aeab328`. TWO PUSHES, both recorded below. <<<**
**PUSHED at a fresh yes ("Push both"), ONE named refspec, no force, no PR** - GitHub offered
`pull/new/alexah/80-the-park-draws-the-people-it-saved` and it was **not** used. The branch was NEW on
origin, which is why the before-check asserts its ABSENCE rather than a tip; every check was a hard
assertion that would have aborted before any network write. After: `local == remote` at `1b91b27`,
**0 commits ahead**, `main` still `453e779` on origin AND upstream, **0 `refs/pull/*`**, one worktree,
tree holding only the four never-commit paths. The docs half went in the same breath -
`111eddf..998d92f` on `docs/particles`, one named refspec, no force, no PR, default `0e8d5d0` unchanged
on origin and upstream. **THAT YES IS SPENT - the next push needs a fresh one** ([[ask-before-github]]).
**>>> TWO FURTHER COMMITS, BOTH VERIFIED ON SCREEN AND BOTH PUSHED 2026-09-16 -
`alexah/80-the-park-draws-the-people-it-saved` = `ef4f091`, `1b91b27..ef4f091`. <<<**
Pushed at a fresh yes ("yes, please go ahead and push to my repos"), **ONE named refspec, no force, no
PR** - and **no compare link was offered, which is expected because the branch already existed** (the
first push of this branch did offer `pull/new/...` and it was not used). The branch EXISTS on origin now,
so unlike that first push the before-check asserts its **current tip** (`1b91b27`) rather than its
absence; every check was a hard assertion that would have aborted before any network write. After:
`local == remote` at `ef4f091`, **0 ahead**, `main` still `453e779` on origin AND upstream, **0
`refs/pull/*`**, one worktree, tree holding only the four never-commit paths. **Each commit was built
ALONE in a throwaway worktree first - both 127/0 and 299/299** - which mattered here because `ef4f091`
renames `_sprites` to `_people`, so a missed reference would have left `242fb8b` broken in the history
while the tip still built. **THAT YES IS SPENT - the next push needs a fresh one** ([[ask-before-github]]).
- **`242fb8b` - the people face the camera.** The quads were built the way `WeatherSprites` builds rain:
  world up plus a horizontal across, which faces the camera **only in plan**. A park camera looks down
  45-65 degrees, so that cost every person between 29% and 58% of their height and showed the sprite's
  edge. **Alexah reported it from the original's behaviour** after I had diagnosed it wrongly three
  times - now **rule thirty-four** in [[verify-every-ordering]]. The basis is built from `Forward` and
  world up, NOT from `Rotation.Right`/`Up`, which carry `LookAt`'s arbitrary roll (`Audio` and
  `AudioListener` both record that trap). Verified by capture with one variable changed and the earlier
  frames kept as a control: every sprite ~1.4x taller, which is `1/cos(45)`.
- **`ef4f091` - a debug view, at Alexah's suggestion.** `guests` prints the census (thing, model, cell,
  slot, type, bank, set, frame, facing); `facing` draws a ground dash per person coloured by kind. Both
  behind `OPENTPW_DEBUG_CONSOLE`, one call site, and **registered in `DebugConsole`'s own "to remove
  entirely" list**. Path and state deliberately omitted - no simulation exists to report. Labels over
  heads were rejected: **there is no world-to-screen projection anywhere in the tree** and every font
  draws in the interface's virtual screen, so text would have meant new engine surface for a diagnostic.
- **>>> I CAN SCREENSHOT-VERIFY PARK WORK MYSELF, AND WRONGLY HANDED IT TO ALEXAH. <<<** The "ask Alexah
  to look" rule is about **lobby flyers**, which reseed per launch; [[verifying-rendering-by-capture]]
  records the opposite for parks - "a PARK frame is deterministic in a way a lobby frame never is".
  Harness: `~/.cache/tpw-harnesses/guestshot.py` and `facingshot.py` (enter park, drive `camera`, Right
  arrow = 45 degrees = one octant, grab from the ROOT window by `_NET_WM_PID`).
- **`content/ReferenceScreenshots/Park/` holds three shots of the ORIGINAL** - an oracle my own render
  can never be for itself. They are **developed parks with hundreds of guests**, so they evidence how a
  person LOOKS and how big one is, never how many or where.
- **Census facts:** `BankOffset` is 0 on all 18, so the composite's high bits are unexercised here;
  `frame` is scattered across the walk cycle; **11 of 13 guests carry facing 0**, so they really do all
  face alike. Sprite sizes from the art: kid 5.78 units, staff 7.0-8.4, entertainer 10.78 - **staff are
  genuinely bigger than guests and the renderer reproduces that for free**, from each picture's own size.
- **`SPR_FA`/`SPR_OM` are the FAT and slim mechanics** (Alexah named it; the art confirms 64px vs 41px
  wide). Our park's mechanic is bank 1, the slim one. `Generic\Handymen\SPR_HA` **is** a person - an
  elderly caretaker in a tan coat with a broom - which I misread as scenery from its d=0 back view.
- **THE `no-tooling` RULE WAS MIS-SUMMARISED IN `MEMORY.md` as "out of commits"** and nearly cost this
  feature. The real rule: **one off-by-default switch, one call site** (`FrameProfiler` is the pattern);
  only throwaway scripts stay out of the repo. Corrected 2026-09-16.

**>>> BRANCH 82 IS DONE AND PUSHED, 2026-09-16 -
`alexah/82-the-animation-a-model-ships-unnumbered` = `4f307bb`, ONE commit over `8bd02b0`, NEW on origin. <<<**
**PUSHED at a fresh yes ("Yes, the river and the waterfall look fantastic! ... Go ahead and push to my
repos"), ONE named refspec, no force, no PR** - GitHub offered
`pull/new/alexah/82-the-animation-a-model-ships-unnumbered` and it was **not** used. The branch was NEW on
origin, which is why the before-check asserts its ABSENCE rather than a tip; every check was a hard
assertion that would have aborted before any network write. After: `local == remote` at
`4f307bb12744ce23e52f7470b2a34038e59bbdf5`, **0 ahead**, `main` still `453e779` on origin AND upstream,
**0 `refs/pull/*`**, one worktree, tree holding only the four never-commit paths. **THAT YES IS SPENT -
the next push needs a fresh one** ([[ask-before-github]]).
**>>> ALEXAH CONFIRMED IT ON SCREEN: "the river and the waterfall look fantastic!" <<<** That is the one
check this project cannot run for itself, and it is what the confounded pixel measure below could not
supply.
This is **agreed-plan item 1**, and three of the claims that item used to make were wrong - see the
re-scoped item 1 above, which now carries the corrections. The fix itself is four lines:
`LobbyModel.LoadAnimations` takes the bare `{stem}M.md2` **only where the numbered run came back empty**,
which is the engine's own condition.
- **THE CORPUS, RE-MEASURED BY ME (`rolecensus.py`, `bare-m-tracks.py`):** **197 bare-only-M over 445 base
  models in 312 archives** - not the 191/306/1,219 this file used to say. Of the 197, **160 carry a
  rotation, morph or UV track**, **37 are EMPTY** (no track of any kind), and **0 are
  position-and-visibility-only**, so `AnimationFile.TryLoad` never turns one away and the fix needed no
  direct-read path. **0 of the 197 sit outside `levels/`, so the lobby is untouched.**
- **THE LIVE BLAST RADIUS IS MUCH SMALLER THAN "197 MODELS".** Of the eight `new LobbyModel(...)` sites,
  only `ParkTerrain.cs:53` and `ParkQueues.cs:180` load park content without supplying clips;
  `ParkObjects` and `ParkFixedItems` have supplied `RideAnimations.AllClips` since branches 75/77. **For
  Lost Kingdom the whole win is ONE model - the terrain** - and its queue gains nothing, because jungle's
  three bare queue clips are all empty. Space's queue clips are real and do gain.
- **VERIFIED ON SCREEN, and this is the evidence that matters:** entering the park now logs
  `levels/jungle/terrain/base.MD2: animating 11 mesh(es) - 'surface16', 'surface15', 'surface14',
  'surface13', 'surface22', 'surface23', 'surface25', 'surface27', 'falls02', 'surface17', 'Object12' -
  with 1 animation(s)`. Eleven meshes, matching the eleven UV tracks, and they are the water - ten
  surfaces and **`falls02`, the waterfall**. That line could not exist for the terrain before.
- **>>> THE PIXEL MEASURE IN `terrainanim.py` IS CONFOUNDED - DO NOT CITE IT. <<<** Whole-frame
  difference came back 1.5-2.1 mean on every viewpoint **including the `gates` shot chosen as a non-water
  control**: a running park moves anyway (weather, sky, clock, people). The harness now says so in its own
  docstring. To be worth anything it would have to crop to one named water mesh.
- **FIDELITY VERIFIED IN GHIDRA rather than assumed:** `FUN_00461f10` owns **both** role format strings
  (`'%s%s%c%d.md2'` `0x004623b3`, `'%s%s%c.md2'` `0x004623df`), so it is the prober; and `FUN_004504c0`
  builds `'%s\Terrain'` and calls it with stem **`"Base"`** (`0x0074cf58`, read with `memory.getInt`).
- **TWO CONTROL RUNS, PREDICTIONS WRITTEN FIRST** (`~/.cache/tpw-harnesses/control-82-prediction.txt`),
  **BOTH EXACT**, each naming the tests AND the assertions AND the expected/actual pairs. A (fallback
  deleted): predicted 2 failed / 304 passed, got exactly that on the two named assertions. B (fallback
  made unconditional): predicted 1 failed / 305 passed, got `Expected:<2>. Actual:<3>.` on the mamfount
  test - **so the `Count == 0` condition is load-bearing**, which control A alone could never have shown.
  Restored from the **COPY** at `~/.cache/tpw-harnesses/branch-82-backup/`, md5-verified, never
  `git checkout --`.
- **Gates:** branch-point baseline measured FIRST (**127/0, 302/302**, `~/.cache/tpw-harnesses/baseline-82.log`);
  after, **127/0, 306/306, 0 skipped**; and `4f307bb` built **ALONE** in a detached throwaway worktree at
  those same numbers. `save/` byte-identical at `23a4922d...` across every single run.
- **`LoadAnimations` is now `internal`** (the assembly already had `InternalsVisibleTo( "OpenTPW.Tests" )`),
  because a `LobbyModel` needs a graphics device and a test asserting the shipped files instead would pass
  with the fallback deleted - which is exactly what control A proves it does not.
- **THE DOCS HALF WENT UP ON THE SAME YES: `b69a294` on `docs/md2-bare-animation`**, a new branch stacked
  on `docs/md2-easing-curve` (`d8b9447`, local == origin), one file, +40/-1. One named refspec, no force,
  no PR - a compare link was offered and **not** used. After: `local == remote` at
  `b69a294f23df4e384082f1e4b9cfa81e15ab12bf`, base unmoved at `d8b9447`, default `master` still `0e8d5d0`,
  **0 `refs/pull/*` on origin**, and **upstream PR #1 verified untouched at `d73a627`** before AND after. **The site was built
  BEFORE committing - clean, 16 pages.** `models.md` stated the `M1`/`M2` convention and never said the
  number is optional; a new section records the probe order, the twelve role letters, mamfount, and that
  37 of the 197 files are empty. **One correction on the way:** its Open questions still listed `+0x34`
  as an unowned mystery slot while the same page decodes it as the easing curve table. `models.md` lives
  only on that branch, which is why the earlier grep on `docs/particles` found nothing - a branch artifact,
  not an absence. Upstream **PR #1 is open** at `d73a627` and was verified untouched; nothing was pushed.
  **`npm` is not on PATH in this shell - node is at `/home/alex/.nvm/versions/node/v20.19.0/bin`**, which
  is the same toolchain that built `dist/`; prepend it rather than concluding the site cannot be built.
- **>>> ONE SMALL DEFECT FOUND ON THE WAY AND DELIBERATELY NOT FIXED HERE - it is a free next win. <<<**
  `ParkFixedItems.cs:42-43`'s class comment says the gate archive holds `gatesm1`, `gatesm2` and
  `gatesm3`. Measured across all four themes: **fantasy ships NO numbered gate clip at all** (bare
  `gatese/gatesi/gatesm/gatess.md2`) and **space ships FOUR** (`gatesm1..m4`); only jungle and hallow match
  the comment. The code is right and only the comment is wrong - fixed items load through
  `RideAnimations`, which handles both forms - so this is a comment fix, not a behaviour fix. It was left
  out of branch 82 because that branch is about `LobbyModel` and the gate never reaches it.

**>>> BRANCH 81 IS DONE AND PUSHED, 2026-09-16 -
`alexah/81-the-four-banks-the-game-loads-first` = `8bd02b0`, ONE commit over `ef4f091`, NEW on origin. <<<**
**PUSHED at a fresh yes ("yes, please push"), ONE named refspec, no force, no PR** - GitHub offered
`pull/new/alexah/81-the-four-banks-the-game-loads-first` and it was **not** used. The branch was NEW on
origin, which is why the before-check asserts its ABSENCE rather than a tip; every check was a hard
assertion that would have aborted before any network write. After: `local == remote` at `8bd02b0`,
**0 ahead**, `main` still `453e779` on origin AND upstream, **0 `refs/pull/*`**, one worktree, tree
holding only the four never-commit paths. **THAT YES IS SPENT - the next push needs a fresh one**
([[ask-before-github]]). It closes BOTH provisional choices branch 80 shipped, and closing one of them
uncovered a real bug in already-pushed code.
- **>>> EVERY GUEST IN THE PARK WAS WEARING THE WRONG CHILD. <<<** `Sprites_LoadFolder` matches a table
  of four names at `0x764030` - **SPR_BI, SPR_KI, SPR_TA, SPR_SU, in that order** - against a folder's
  files BEFORE sweeping it, and does so for **kids and kidsheads alone**. The archive lists
  `Generic\Kids` as BE, BI, CH, FR, KI, SA, SU, TA, so a plain sweep makes bank 0 the BE child where the
  game makes it BI and pushes BE out to 4. All 13 guests wear kids banks, spanning **0, 2, 4, 5, 6, 7**.
  Now **rule thirty-five** in [[verify-every-ordering]]. Staff are unaffected - no avatar pass, and the
  mechanic still reads bank 1 as the slim one.
- **THE WORLD SIZE CONSTANT IS CLOSED AND ITS PREMISE WAS WRONG.** `20/128` is EXACT: `0x0070200c` holds
  **20.0 in read-only `.rdata`**, the vertical factor `0x00768ab4` is **1.0 with zero writers anywhere in
  the image**, the call site `FUN_00589990` passes **1.0f twice**, and all 18 sprites carry scale
  1.0/1.0. The "second factor filled in at device setup" `0x008bcbcc` is **not a world term at all - it
  is 1/aspect**: `FUN_0056b790` builds frustum corners at unit depth as `y = 0.5*tan(fov/2)` and
  `x = y / 0x008bcbcc`, and `x = y*aspect` for every camera. Corroborated twice over (`FUN_005860d0`
  scales only NDC X by it; `FUN_005890e0` applies it only to a default X size), and a byte scan found
  **all 15 absolute references are FLD/FMUL/FDIV with zero stores**, so it is written through a `this`
  pointer at device setup - which is exactly why it reads 0.0 statically. **Our projection matrix already
  carries aspect; applying it again would squash every person by it.**
- **THEME SCOPING IS CLOSED: a park sees its own theme and no other.** The table at `0x763f88` is
  **fourteen bare kind names with no theme among them**; each kind is swept under `generic\` and the
  current theme in turn, and the archive keeps those disjoint (Generic holds the eleven; each theme holds
  only costumes, costumeheads, entertainers). `FolderFor` was right all along.
- **A DETAIL-DRIVEN CAP EXISTS AND IS DELIBERATELY NOT IMPLEMENTED:** `FUN_0041a9d0` returns **2/4/6/8**
  kid banks and `FUN_0041aa40` **1 or 2** staff banks, off `DAT_007858d0` from
  `GameOptions_LoadDetailFile`. Our park's guests reach bank 7, so it was saved with all eight loaded. It
  only ever loads FEWER banks, so ignoring it cannot mis-number anything.
- **Gates:** branch-point baseline measured FIRST (**127/0, 299/299**,
  `~/.cache/tpw-harnesses/baseline-81.log`); after, **127/0, 302/302, 0 skipped**, `save/` byte-identical
  at `d48d5c74...` across every run, and `8bd02b0` built **ALONE** in a throwaway worktree at those same
  numbers.
- **CONTROL RUN, prediction written first** (`~/.cache/tpw-harnesses/control-81-prediction.txt`): emptying
  `AvatarFirst` predicted **2 failed / 300 passed / 0 skipped**, naming both tests AND which assertion
  each would fail on. Got exactly that, failing at **index 0** of the folder order and **index 1** of the
  children worn - which is precisely where the predicted expected/observed lists first diverge. The
  mechanics control passed. Restored from a **COPY** verified by md5, never `git checkout --`.
- **Docs PUSHED in the same breath: `998d92f..06cd304` on `docs/particles`** - one named refspec, no
  force, no PR, no compare link offered (expected: the branch already existed), default `master` still
  `0e8d5d0` on origin, **0 `refs/pull/*`**, `local == remote` after. **The site was built BEFORE
  committing this time - clean, 15 pages** - which is the ritual missed last session and recorded as a
  miss in [[opentpw-fileformats-docs]]. `sprites.md` said *"Banks are numbered in the order a folder's
  files are found"*, now corrected; `0x08`/`0x0A` stop being "not identified" (they are the 128 the
  engine divides by); two new sections - how a folder is swept, and how big a sprite is in the world.

**>>> PEEP SIM: STAGE 1 DECODED; STAGE 2 PUSHED as `70d522c`, five commits; STAGE 3 (THE NAVIGATOR) IS
FOUR COMMITS PUSHED 2026-09-16 - `44c3e6d`, `af62661`, `7867ed6`, `3084eb6` = tip, 361 pass. GUESTS DO NOT WALK. <<<**
**`92b1c37` the guest reader** - `ParkWorld.ReadPerson` reads the whole 135-byte guest block as 14
fields, staff excluded because their block differs; 306 tests -> 309. **`78c06a9` the balance-key rule** -
a key names one or more dot-separated fields and the line supplies one value each, which the original's
own parser `FUN_004017a0` states outright (it collects the names, caps at 16 with "Too many fields have
been specified", then runs its value loop exactly that many times). So `PeepTypes[n].StartingCash`
resolves at last, and all 13 guests' cash lands inside their kind's band; 309 -> 313. Both 127/0,
`save/` byte-identical, baseline measured BEFORE editing (`~/.cache/tpw-harnesses/baseline-83.log`,
306/306 clean). **`36f39f1` the needs tick** - `Peep`, the running copy of a guest, and one turn of
`FUN_00501650`: the 1-in-4 slot gate, the mExitLevel countdown, the 16th-tick drift, the four maxed-need
happiness penalties and the toilet hurry flag, with the cell/RegionFX term deliberately absent and named
where it would go. Pure arithmetic - every constant is hardcoded in the exe, so its tests need neither
game files nor a device; 313 -> 323. **`1197d0e` the park runs its guests** - `ParkPeople : Entity`
builds a `Peep` per guest and ticks them from `GameClock`, with the tick NUMBER worked back as
`Ticks - TicksDue + 1 + i` (a local counter would put guests in the wrong `id & 3` slots), plus a `peeps`
console command beside `guests`; 323 -> 326.
**>>> VERIFIED IN A LIVE PARK, not just in tests (`~/.cache/tpw-harnesses/peeps-live.py`). <<<** Over 194
real ticks the exit-level falls grouped by slot as **{0:48, 1:49, 2:49, 3:48}**, matching the ticks per
residue in that exact window - the slot arithmetic reproduced to the tick, boundary included - and
**exactly the three guests whose id divides by four (40, 36, 32)** drifted, each hunger +24, thirst +24,
toilet +12, which is 194/16 = 12 drifts at the documented 2/2/1. **MY OWN CONTROL WAS WRONG FIRST:** it
predicted every guest falling by the SAME amount, which only holds for a window aligned to four, and the
assert aborted the run before it tested the drift at all - a prediction stronger than the truth is still a
broken instrument. Corrected to "turns depend only on `id & 3`", which is the stronger check anyway.
**>>> BOTH REPOS PUSHED 2026-09-16 AT A FRESH YES ("Yes you may push"). THAT YES IS NOW SPENT. <<<**
`alexah/83-the-state-a-guest-was-saved-in` = **`70d522c`**, **NEW on origin**, five commits over
`4f307bb`; **ONE named refspec, no force, no PR** - GitHub offered
`pull/new/alexah/83-the-state-a-guest-was-saved-in` and it was **not** used. Docs:
`docs/save-person-record` **`fda26d3..697f447`**, one named refspec, no force, no PR, and **no compare
link was offered, which is expected because that branch already existed**.
**Every one of the five commits was built ALONE in a throwaway worktree first** - 127/0 with
**309 / 313 / 323 / 326 / 332** passing, 0 failed and 0 skipped, the counts stepping up exactly as each
commit message claims. **Eight** hard before-checks on the code repo and **nine** on the docs repo, every
one able to abort before any network write. After: `local == remote` on both, **0 ahead**, parent
`4f307bb` unmoved, `main` still `453e779` on origin AND upstream, `master` still `0e8d5d0` on both,
**upstream PR #1 untouched at `d73a627`**, **0 `refs/pull/*` on either origin**, one worktree each, trees
holding only the four never-commit paths. **THE NEXT PUSH NEEDS ANOTHER FRESH YES** ([[ask-before-github]]).
Working copy of this decode: `~/.cache/tpw-harnesses/` is for scripts, so the long form lives in the
session scratchpad `peep-decode.md`. Everything below was read this session, not inherited.

**THE DOORWAY.** `FUN_0050b360` is a **switch on the thing's model byte at `+2`**, two calls per model:
1 guest `FUN_00501650`+`FUN_005019f0`, 3 queue `FUN_004e0b90`+`FUN_004e0e00`, 4 mechanic, 5 handyman,
6 entertainer, 7 guard, 8 researcher, **0xb advisor** `FUN_0059a550`, **0xf weather** `FUN_00512880`,
0x12/0x13 online-only. **That 0x0b is the advisor and 0x0f is weather is what PROVES the byte is the
thing model** - [[park-advisor-from-exe]] and [[park-weather-from-exe]] each recorded those
independently, so this is two prior memories agreeing with a third instrument, not an assumption.

**THE GUEST STRUCT, named by the game's own save reader** (`FUN_004fb530`, person base `FUN_004f8b10`):
`+0xc0` mBaseSpeed, `+0xc2` mPurposeSpeed, `+0xc4` mAdjustorSpeed, `+0x18`/`+0x1a` mAccurateDestX/Y,
`+0x1a0` mCash, `+0x1bc` mExitLevel, `+0x1c0` mPrankeryIndex, `+0x1dc` mMajorDest, `+0x1f0` mPersonType,
`+0x1f1` mQueuePos, `+0x208` mTimeOfLastSpotAnim, `+0x210` mBalloonScript, **`+0x220` mState**,
`+0x224` mSavedState, `+0x228`/`+0x22a` mQNext/mQPrev, `mPreviousRides[4]` at `+0x1e0`.

**>>> THE SEVEN NEED METERS ARE NAMED, AND THREE INDEPENDENT INSTRUMENTS AGREE. <<<** They are floats
clamped to 0..100 and are the ONLY fields the save writes under an unnamed tag - all seven share the
literal **`'pv'`** at `0x0075b444`, so the save reader cannot name them and something else had to.
| `+0x19c` **happiness** (starts **50.0**) | `+0x1a4` **thirst** | `+0x1a8` **hunger** |
`+0x1ac` **toilet** | `+0x1b0` **illness** | `+0x1b4` **litter carried** | `+0x1b8` **still unnamed** |
The three instruments: (1) in `FUN_004fcc30` each `FLD [EDI+off]` sits immediately before its own
`printf` - thirst, hunger, toilet, illness in that order; (2) `FUN_004fe1e0` applies an object's effect
block `+0x144..+0x154` to exactly those fields and prints **"Litter gone up by %d, is now %d"** over
`+0x1b4`, and the sideshow-win path prints "happiness up %d points to %d" over `+0x19c`; (3) the balance
file's key names, below. **A fourth instrument was REJECTED and that matters:** a scan of
`0x004d0000-0x00512000` for the literal offsets returned 66 functions for `+0x19c` alone, including
`CStaff::SetState` and "Object %d: repairing fully" - **objects and staff have their own fields at the
same offsets**, so the scan is a lead generator and nothing more. New **rule thirty-nine** in
[[verify-every-ordering]]. Its two controls (mCash on SUB/ADD in the money functions, mExitLevel on the
decrement) did work, which is how I know the scan ran rather than silently matching nothing.

**THE NEEDS TICK** `FUN_00501650` is gated `(thingId & 3) == (tick & 3)` - **each peep updates one tick
in four**. It decrements mExitLevel; adds the peep's **CELL's** three shorts (`FUN_004d8410`) to
happiness, illness and hunger, so **scenery and terrain feed the needs**; every 16th tick grows toilet
by 1 and hunger and thirst by 2; clamps everything 0..100 through **`FUN_004fb4f0(need*, delta)`**, the
one helper every need change in the module funnels through; and sets **mPurposeSpeed** to 0 or 25, which
is the hurry flag `FUN_004fa2a0` reads to pick a *different walk script*.

**THE 22-STATE MACHINE** `FUN_005019f0`, setter `FUN_00501db0`, animation queued by `FUN_004217f0(n)`
(n=1 walk, 2 hurried walk, 3 stand): 0 walking; 1 at a gate cell; 2 heading for the gate ("The bus is
coming! RUUUN!"); 3 waiting for opening; 4 judging the admission fee; 5 entering; **6 THE DECISION
STATE**; 7 wandering; 8 playing a spot animation then restoring mSavedState; 9 minor destination;
0xa walking to a chosen ride; 0xb standing in queue; 0xc stepping up the queue; 0xd being admitted;
0xe entering the ride; 0xf on the ride; 0x10 riding; 0x11 leaving/deleted; 0x12 heading for the exit;
0x13 picking a cell outside the gate; 0x14 walking outside; 0x15 at the bus stop. **A new guest is
constructed in state 6.**

**THE DECISION.** State 6 `FUN_004fec90` -> `FUN_004fcb10` walks the object list scoring each with
**`FUN_004fcc30`** (the game calls it **COS**, "Calculating option score"), keeps the best over 9 that it
can reach, writes mMajorDest. The score is a weighted mean of distance, queue, excitement, thirst,
hunger, toilet and illness, then multiplied for new / indoor-in-rain / expensive / GT-only, then
**divided by 5, 4, 3 or 2 if the ride is 1st..4th in `mPreviousRides`** - the anti-repetition rule.
Minor destinations come from `FUN_00500dc0` (nearest object with flag `+0x32 & 0x40`).

**NAVIGATION.** The map is **128 x 128** (`FUN_0050b6a0` asserts `y*0x80 + x < 0x4001`), cell id
`y*128 + x + 1`. A thing's mX/mY are u16 in **256ths of a cell**; the navigator's own position is
**256x finer again = 1/65536 of a cell**, proven twice - `FUN_004fa2a0` does `>>8` to reach mX/mY and
`FUN_004fa5f0` (SetDest) does `<<8`. `FUN_004fa2a0` returns **0 arrived, 1 walking, 2 stuck**. The
navigator is a **Reynolds steering system**: its own strings name `arrival`, `separation`,
`avoid_obstacle`, `repel_obstacle`, `max_speed`, `max_force`, `radius`, `local_xaxis`, `formation_pos`,
`force`, `nav_mode` and a `path_*` family at `0x00761ee9-0x0076208c`.

**>>> THE RECORDED BLOCKER IS RESOLVED, AND ITS PREMISE WAS WRONG RATHER THAN ITS DIRECTION. <<<** This
file said: distance moved -> `FUN_004d41d0` clamp 0..250 -> `FUN_00475610` -> sprite `+0x80`, "which
reads as more distance -> slower animation, which is backwards. **Do not implement the rate from this
chain until the direction is explained.**" Every link is real (`FUN_004d4190` applies a 3-dword pending
record: `[0]` spriteId, `[1]` anim index through `DAT_0075a418`, `[2]` interval). But the **units make
the whole chain inert**: `FUN_00510510` forbids crossing more than one cell per step, so the distance is
at most 65536 nav units; `65536 * 3.8296e-05 = 2.51`, and `__ftol` truncates to **0, 1 or 2** against a
sprite step that runs every **62 ms**. So it is a **teleport clamp, not a rate control**, and the x2 for
a non-hurrying peep is inert for the same reason. Real walk speed is mBaseSpeed / mPurposeSpeed /
mAdjustorSpeed, which the old note never accounted for. **Falsifier, cheap to re-check before building:**
a step over ~24 cells reaching `+0x80`, or `+0x80` turning out not to be milliseconds.

**>>> THE SIMULATION'S TUNABLES ARE DATA, NOT CODE - AND WE ALREADY PARSE THE FILE. <<<** The constants
block at `0x00785000` reads back **all zeros** statically (exit level, cash spread, prank chance, person
type count, and the whole 7-entry COS weight table) - the same trap as `SpriteBank_Load`'s constants.
It is filled by a text parser: `BalanceLoader.cpp` is named in the binary, beside `'Balance file %s
parse error'`, `'InitialiseBalanceValues internal failure'` and `'No map_type specified'`. The file is
**`data\levels\Standard.sam`** overlaid by `data\levels\%s\global.sam`, and **`ParkBalance` in our own
tree already parses exactly that pair**. The base file's `PeepInfo` block names the tunables outright -
`ExitLevel 120`, `ExitLevelVar 60`, `StartingCashVarPc 15`, `Small/Medium/BigHappinessChange 5/15/25`,
`RideVomitDivisor`, `ToiletDesparate 100`, `VomitCapacity 100`, `PrankeryLikelihood 5`,
`StinkbombLikelihood 25`, `DecisionVariable1/2/3`, `PerfectRide/GoodRide/OKRide 25/15/5`, the four
`OpinionFor*` and the price multipliers - **33 keys, plus `PeepTypes[0..7]`**, 8 rows of
`PreferredExcitement . StartingCash . BoredomThreshold` against a stride-12 table at `0x007850e4` whose
two readers agree (`+0` excitement in the scorer, `+4` cash in the constructor).
**THE DECISIVE CROSS-CHECK:** `DecisionVarDistWeight, QueueWeight, ExcitementWeight, ThirstWeight,
HungerWeight, ToiletWeight, IllnessWeight` is **seven keys in exactly the order `FUN_004fcc30`
multiplies its seven terms** - so the need naming above is confirmed by a text file the developers
wrote, independently of my read of the disassembly. Only `PeepInfo.ExcitementToCostDivisor` is
overridden by any theme (fantasy and space, 4 -> 5); jungle and hallow override nothing.
**Nothing in OpenTPW reads a `PeepInfo` key today** - measured, not assumed.

**>>> STILL OPEN, AND NOT TO BE GUESSED: `+0x1b8`, the seventh need. <<<** The constructor zeroes it and
`FUN_004fd970` only takes its address; the scorer's `FILD [EAX+0x1b8]` is the **object's** field, not the
peep's. Candidates from the balance file that are so far unmapped include the boredom threshold.
Also unread: the five staff state machines. **`FUN_004e0b90` is NOT the queue's tick** as this block first
guessed from the dispatch table - it is the **object's** tick (breakdown requests, a 0..100 float at
`+0x48`), and its `+0x19c` is the OBJECT's state: a live instance of rule thirty-nine inside the very
session that wrote it. `FUN_0050ed10` and `FUN_005102f0` **are** now read - see the navigator note below.

**THE NAVIGATOR, the two functions with no static callers.** `FUN_0050ed10` is a registered path/stuck
behaviour: it keeps a **15-step "was I blocked" bitmask at `+0xb0`** (shifted in by `FUN_0050f3b0`), and
when more than **0x6665 - about 40% of the last fifteen steps - are blocked** it renavigates, falling
back to setting `mCantReachDest` (`+0xb8`) and logging *"Poople-flops!"*. It also advances the waypoint
cursor `path_count` (`+0x5c`) through `subpath_buffer[]`, with an arrival tolerance of `radius` **x 2.0
mid path, x 1.6 last leg - CORRECTED 2026-09-16, this file had those two SWAPPED**. `FUN_005102f0` is the
**separation query**: it sweeps cells in radius, accepts models 1 and 4-8, skips itself by comparing
`param_1` against `thing + 0xd4` - **so a peep's steering object lives at `thing + 0xd4`** - and invokes
a callback per neighbour, **capped at five per step**.

**>>> THE GUEST'S DISK LAYOUT IS DERIVED AND VERIFIED, AND IT IS THE THING TO BUILD ON. <<<** The offsets
above are **in-memory**; this project already refuted using those as disk offsets. On disk a thing is a
sequence of individually named fields, so a field's disk offset is **the sum of the sizes before it**,
taken from each reader's WRITE branch. Completing that needed two readers this file listed as unknown:
**`FUN_0050d150` = 177 bytes** (the navigator's saved state - `force`, `radius`, `max_speed`, `nav_mode`,
`path_*`, `subpath_buffer[5]`/`subpath_dist[5]`, so **a peep's navigation is persisted**) and
**`FUN_0050bad0` = 144** (`mActionHistIndex`, `mEventHistory[32]`, `mLastThought`, `mThoughtScript`,
`mTimeBubbleShown`). With those the layout is:

    record + 0    [u32 Used_Thing_Next][u32 thingmodel]                      8
    record + 8    the person base, in read order                           390
    record + 398  the guest's own block, in read order                     135   = 533

**FIVE numbers already on record agree and NONE of them was an input to the derivation:** 390 and 135 are
this file's own figures from a different route; **`RecordSizes[1] = 533` in `ParkWorld.cs` was MEASURED
from this very save** by the shipped walk; `mSpriteAngle` lands at record-relative **`0xf2`**, exactly
where the shipped, tested `ParkWorld.ReadPerson` already reads it; and all five staff sizes
(511/513/509/511/509) are `8 + 390 + 105 + own`. The fields worth having, record-relative:
**mCash 414, mExitLevel 418, happiness 422, hunger 426, litter 438, mMajorDest 442, mPersonType 468,
mPrankeryIndex 469, mQueuePos 494, mSavedState 501, mState 505, thirst 509, toilet 525, illness 529.**

**VERIFIED AGAINST THE SHIPPED PARK** (`~/.cache/tpw-harnesses/guest-state-fields.py`, log beside it):
the walk closes on `DLRW`; **13 guests examined, 5 staff skipped**; and the values are not merely
in-range but *specific*. **Happiness is exactly 50.00 on all thirteen** - the constructor's
`0x42480000`. **`mSavedState` is 6 on all thirteen** - the constructor's default. **`mState` is only ever
2, 3 or 5** (heading for the gate / waiting for opening / entering), which is an independent match for
branch 80's cell census putting these same guests **on the bus road**. And **every guest's `mCash` falls
inside PeepInfo.StartingCashVarPc (15%) of `PeepTypes[mPersonType].StartingCash`** - 13/13, tying two
offsets derived from the exe to a column mapping derived from a text file, so it cannot be an artefact of
the offsets alone. Illness and litter are 0.00 on all, `mMajorDest` and `mQueuePos` 0: nobody has chosen
a ride yet, which is what "arriving" should look like.

**>>> THE CONTROL CAUGHT MY OWN CHECK BEING VACUOUS, AND THAT IS THE PART TO REMEMBER. <<<** The harness
re-reads every field at the base shifted -8, -4, +4, +8. On the first run **shift -8 passed 13/13 beside
the real base**, because the only float test was `0.0 <= need <= 100.0` and **a small INTEGER field read
as a float is a denormal of about 1e-43, which passes any 0..100 range test**. The map was never actually
in doubt - the cash-versus-`PeepTypes` agreement and the states are what carry it, and neither came from
the range check - but the instrument was claiming a power it did not have. Replaced with specific tests
(needs must be WHOLE numbers, `mSavedState` must be exactly 6, `mCash` must sit in its type's band), after
which the control reads **13/13 at the derived base and 0/13 at all four shifts**. Addendum to rule
thirty-nine in [[verify-every-ordering]].

**THE READER WAS THE FIRST BUILDABLE INCREMENT AND IT IS DONE** - `92b1c37`, exactly as branch 80 did it
(`b331950` the reader, then `1b91b27` the renderer). It reads mState, mSavedState, mPersonType, mCash,
mExitLevel, the six needs, mMajorDest, mQueuePos and mPrankeryIndex, pinned against all 13 measured rows.
**THE NEEDS TICK IS THE NEXT INCREMENT, and the disassembly of `FUN_00501650` corrected two things my
own notes had wrong** (the decompiler drops the FPU operands - read the instructions, not the C):
- **All four "a need is at exactly 100" checks subtract 1.0 from HAPPINESS**, not from the need. `EDI` is
  loaded once at `LEA EDI,[ESI+0x19c]` and every one of the four `CALL FUN_004fb4f0` passes it. A maxed
  illness, hunger, thirst or toilet costs happiness 1.0 a tick. The note said only that 100 fires the helper.
- **The hurry flag is TOILET**, hardcoded `CMP AL,0x50`: toilet > 80 sets mPurposeSpeed 25, else 0
  (`DAT_0075c7f0` = 0, `DAT_0075c7f2` = 25). Nothing to do with the balance file's `ToiletDesparate 100`.
- Drift confirmed: every 16th tick toilet `-(-1.0)`, hunger and thirst `-(-2.0)`, so they GROW.
- The cell term is **three SIGNED shorts** at `+0`/`+2`/`+4` of a **10-byte runtime cell record**
  (`FUN_004d8410` = `map + 0x1b1104 + (cellId-1)*10`), added to happiness, illness and hunger.
  **`+6` of that record is the SECURITY LEVEL** - `FUN_004cd720` sweeps all 16,384 cells counting
  `+6 > 0` and returns a percentage, and `FUN_00508a30` reads it before siccing a guard on a pranker
  ("There's no security level on this...").
**>>> THE CELL RECORD IS `RegionFX`, AND THE BALANCE FILE NAMES ALL FIVE SHORTS IN CODE ORDER. <<<**
The writer is **`FUN_004d8480`**: placing an object walks every cell within a radius and adds five shorts
each divided by **Manhattan distance + 1**, sign `((-(add) & 2) - 1)` so removing subtracts the same.
The eight 12-byte entries at `map + 0x1d9104 + i*0xc` are filled by **`FUN_004d7bb0`** from the balance
block at `0x00785610` (stride 0x18) - and the global `Standard.sam` group **`RegionFX[0..7]`, 48 keys =
8 x 6**, is exactly that: **`.Radius .Happiness .Illness .Hunger .Security .Attraction`**. That order IS
the memory order (`+0` happiness, `+2` illness, `+4` hunger, `+6` security, `+8` attraction), and
`.Security` landing on `+6` is an **INDEPENDENT** match for the two functions above - a text file the
developers wrote agreeing with the disassembly, the same class of corroboration as `DecisionVar*Weight`.
So **`+8` is ATTRACTION**, the last unnamed short. Radii 1-6; happiness -3..2; hunger is negative and the
file says why ("// decreases hunger"). **No theme overrides RegionFX**; `Online_Standard.sam` keeps its
own copy (RegionFX[2].Illness 2 rather than 1), which is a trap if the wrong file is ever layered.
**THE INDEX IS A LITERAL AT EACH CALL SITE, and two of the eight are pinned to things we already draw.**
The wrappers are `FUN_004d8440` = apply (`FUN_004d8480(1,..)`) and `FUN_004d8460` = remove (`(0,..)`),
**13 callers each**, and a thing that MOVES calls remove-then-apply, so staff carry their influence around
the park with them. Reading the immediate pushed at each site: **the entertainer tick `FUN_004d4660` uses
0** - RegionFX[0] is Radius 3, Happiness +2, an entertainer cheering the cells around them - and **the
guard tick `FUN_004d6360` uses 3** - Radius 3, Security 10, exactly what the security-level readers above
wanted. Also seen: 0 at `FUN_004d4250`/`004d4340`/`004d4620`, 1 at `FUN_004dfd80`/`004dd0a0`, 2 at
`FUN_004d93b0`/`004d9620`, 3 at `FUN_004d5de0`/`004d5eb0`, 4 at `FUN_004db090`/`004dd0a0`, 5 at
`FUN_004d93b0`/`004d9620`, 6 at `FUN_004e2440`/`004dfd80`, 7 at `FUN_004db090`/`004dd0a0` and the object
tick `FUN_004e0b90`. **Two sites push a REGISTER**, so not every source is a literal.
What the eight look like from their values alone: 0 entertainer (happy), 3 and 4 security (10 at radius 3,
20 at radius 5), 5 the worst (Happiness -3, Illness +3, radius 3), 7 an attraction (Happiness +2,
Attraction +10, radius 6). **Naming the remaining owners is cheap Ghidra and is NOT done - do not guess
them from the values.** **The needs tick is buildable without the cell term**, and that is how it should be
staged: every other term in `FUN_00501650` is fully specified, and an unmodelled term that is documented is
the ride VM's discipline, not a silent zero.
**>>> THE STATE MACHINE IS READ, AND THE SETTER IS THE HALF WORTH HAVING. <<<** `FUN_005019f0` is a plain
switch that mostly DELEGATES (state 1 `FUN_004ff520`, 2 `004ff730`, 3 `004ff7f0`, 4 `004ff9d0`, 5
`004ffb20`, 6 `004fec90`, 9 `004fff20`, 0xa `004ffbc0`, 0xb `004ffff0`, 0xd `005006b0`, 0xf `00500900`,
0x12 `00500a50`, 0x13 `00500ad0`, 0x15 `00500bd0`) - and every one of those needs the navigator, so they
are NOT buildable yet. The states with inline logic: **0** navigate, arrived -> sprite angle `0x400` and
state 1, stuck -> "Big problem"; **7** navigate, arrived or stuck -> one tick in four tries SetRandomDest
and stays 7, else 6; **8** if `tick > +0x208 + 10` end the spot animation; **0xc** navigate, arrived or
stuck -> 0xb; **0x14** arrived -> sprite angle `0x7ff` or `0x400` then 0x15, stuck -> "Got stuck walking
around outside park"; **0x11** sweeps the staff list for a guard and otherwise deletes the thing.
**`FUN_00501db0` (the setter) IS self-contained and is the next increment.** It writes `+0x220` then
queues an animation - `FUN_004217f0(1)` **walk** for 0, 2, 5, 7, 9, 0xa, 0xc, 0xd, 0x12, 0x14 and
`FUN_004217f0(3)` **stand** for 1, 3, 4, 6, 0xb, 0xe, 0x13, 0x15 (several by falling through to the tail
call), and **nothing** for 8, 0x10 and 0x11. On-entry effects that need no world: **3** sets
`mParkOpeningWaitingTime = rand % 150 + 200`; **8** stamps `mTimeOfLastSpotAnim`; **0xb** stamps
`mTimeStartedIdling` and sets `mQueueMoveDelay`; **0xc** sets `mPurposeSpeed = 0` so a guest shuffling up a
queue never hurries; **0x11** and **0x12** clear `mMajorDest`. The rest (0xe, 0xf, 0x10, 0x15) reach into
ride state, the balloon and the event ring, and are deliberately out of scope.
**BUILT as the fifth commit: `PeepState` (22 states, 0-21, no gaps - a test pins that), `PeepAnimation`,
`Peep.SetState` and `Peep.AnimationFor`.** The animation split is **10 walk / 8 stand / 4 none = 22**, and
the three counts are asserted beside the table so a state moved quietly between groups fails even if its
own row moves with it. **Guests still DO NOT MOVE**: 14 of the 22 states delegate to functions that all
need the navigator, so the rest of the machine is not buildable yet. 326 -> 332.
**>>> ITEM 2 STAGE 3 - THE NAVIGATOR - IS FOUR COMMITS AND PUSHED, 2026-09-16. <<<** On
`alexah/83-the-state-a-guest-was-saved-in` over `70d522c`: **`44c3e6d`** saved state, **`af62661`**
arithmetic, **`7867ed6`** step geometry, **`3084eb6`** the edge-test correction. 332 -> 338 -> 352 -> 361
**passing, 0 failed, 0 skipped, 127/0**; each built ALONE in a worktree; `save/` byte-identical either side.
**THE BLOCK.** `FUN_0050d150` is 177 bytes at **record +43** and EVERY person has one, staff included -
the person base reads it for all six models. Field order is **alphabetical**, sizes come from the write
branch (`FUN_00416e10(size, name, 0)` states each outright) and sum to exactly 177. Record-relative:
mass 75, max_force 79, max_speed 83, mCantReachDest 87, nav_mode 91, path_buffer_count 95, path_count 99,
path_finished 103 (ONE byte), path_stuck_buffer 108, path_subpath_dist 112, path_tail_dist 116,
path_target_pos 120/124, path_total_count **132**, path_total_dist 136, position 140/144, radius 148,
subpath 152..211, velocity 212/216. **Three fields carry NO name in the binary and are named by four
instruments agreeing**: the alphabetical slot each must occupy, their use in `FUN_0050f3b0`, the Reynolds
vocabulary, and the constructor **`FUN_0050ffe0`**, which writes 1.0/0.2/0.4/0.2 to **mass +0x00,
radius +0x04, max_force +0x18, max_speed +0x1c**, a byte 1 to path_finished +0x60, and builds
`subpath_buffer` as **5 x 8 bytes at +100**. So: **+0x00 mass, +0x08 position, +0x10 velocity.**
**THE PROOF THE BLOCK IS WHERE IT IS CLAIMED:** the navigator keeps position in 65536ths of a cell and the
engine reaches mX/mY by `(pos >> 8) & 0xffff`. mX/mY sit at +8/+10 and were read and tested long before
this. **18/18 agree across 17 distinct positions; 0/18 at shifts -8, -4, +4, +8.** The far anchor holds
too: continuing past the block puts mSpriteAngle at **242 = 0xf2**, where the shipped reader already reads.
**MEASURED, NOT ASSUMED:** mass is 65536 (1.0) and radius 13107 (0.2) on all 18. **max_force is exactly
2 x max_speed on all 18.** max_speed is seeded `mBaseSpeed * 65536 / 500` - but **4 of the 18 have drifted
off that seed**, so it is live state and is READ, not derived. `force` and `formation_pos` are (0,0) on
everyone (scratch); `local_xaxis` is a near-unit vector tracking normalised velocity (derived).
**THE ARITHMETIC, all verified.** Distance is **OCTAGONAL, not Euclidean**: `ax + ay - min(ax,ay)>>1`.
Waypoints are **cell centres**, `cell * 65536 + 32768`. `path_buffer_count = min(waypoints, 5)` - a long
route **STREAMS** through five slots, so buffer_count == total_count is a fact about THIS park, not a law.
Progress = `1 - (tail + buffered + distance-to-waypoint) / total`. Stuck = **popcount of the low 15 bits
of path_stuck_buffer, as `n * 65536 / 15` against `0x6665`** - which is exactly **6 of 15** (5 -> 21845, no;
6 -> 26214, yes). Tolerances `(radius * k) >> 16`: **k = 0x20000 (x2.0) MID PATH, 0x19999 (x1.6) LAST LEG**
= 26214 and 20971 at the shipped radius.
**>>> THREE THINGS THIS FILE HAD WRONG, ALL CORRECTED ON THE DAY. <<<** (1) The arrival tolerances were
recorded **SWAPPED** - it is looser at a corner and TIGHTER at the destination, not the other way round.
(2) **`path_timestamp` is NOT a clock**: `FUN_0050ed10` compares it with `FUN_004d8cd0(pos.x >> 20,
pos.y >> 20)`, a coarse region index, and a mismatch logs *"The ground changed under this peep"* - it is a
map-change generation stamp, set from `FUN_004d8c50()`. (3) The **`subpath_dist` semantics ARE settled**
now, where this file said they were not: `FUN_0050f8e0` (SetDest) writes only **buffer_count - 1** of them,
so a one-waypoint route writes NONE and the slot keeps the last route's leftovers - which is why one
person reads `0xCDCDCDCD` and another a stale real distance at the same buffer count. Only
`i < buffer_count - 1` means anything, and the array is deliberately still unread for that reason.
**WHAT `FUN_0050ed10` ACTUALLY IS:** a REGISTERED behaviour reached through the virtual list, which is why
it has no static callers - almost certainly `follow_path`, NOT "arrival". Mid-path it steers at full
speed; on the last leg it ramps down by `min(max_speed, distance / t)`. `FUN_0050f3b0` shifts
path_stuck_buffer **TWICE per step** - once for a move the map refused, once for no progress - so 15 bits
cover rather fewer than 15 steps.
**>>> WHAT WAS LEFT WHEN THIS WAS WRITTEN. BOTH ARE NOW BUILT AND GUESTS WALK (`5940fbc`). <<<**
Left in its original wording because the analysis under it is still the record of how each was read.
Two things, both needing the map: (a) **the
steering step** - the behaviour list at `DAT_007cf078..07c`, the separation query `FUN_005102f0` (capped
at five neighbours, steering object at `thing + 0xd4`), and the one-cell legality check `FUN_00510510`;
(b) **the pathfinder** - `FUN_00511420` is only a wrapper (zeroes the count byte, stashes nav_mode) around
**`FUN_00511470`, budget 60000**, with `FUN_005108a0` rebuilding the waypoint list. The scratch buffer's
LOW BYTE is the waypoint count and waypoints are **byte pairs of cell coordinates from `0x7cf10e`**, which
SetDest and the wrapper agree on independently.
**>>> BOTH OF THOSE ARE NOW READ, 2026-09-16 - and each corrects something above. <<<**
**`FUN_00510510` (step legality)** takes two CELL IDS, undoes `id = y*128 + x + 1` with `-1` then `>>7`
and `&0x7f`, and refuses any step with `|dx| > 1` or `|dy| > 1` (that is the "walking so fast" log).
It then decodes the move into four direction flags - **0 north, 1 east, 2 south, 3 west** - and tests each
set one with **`FUN_004d8750(x, y, dir, ctx)`**, an edge-blocked predicate. **Exactly two flags set means a
DIAGONAL**, and it is allowed only if one of the two L-shaped ways round the corner is clear. A
`fromY == 0 && toY == 0x3ff` pair is the off-map guard ("A peep has enquired as to the po...").
**`FUN_005102f0` (separation)** - three corrections. It accepts **model 0x12 as well as 1 and 4-8**, not
just "1 and 4-8" as recorded above. It measures with **EUCLIDEAN SQUARED** (`dx*dx + dy*dy < r*r`), NOT the
octagonal metric the paths use - so the navigator uses two different distance measures and mixing them up
would be silent. And it sweeps cells over the radius ROUNDED UP (`(r + 0xffff) >> 16`), skipping exact
overlap, capped at five neighbours.
**IN-MEMORY LAYOUT, which is NOT the disk layout** (this file already warns about confusing the two): a
map cell record is **0x44 = 68 bytes** at `*DAT_007cf6ec + 0x2b8 + cellId * 0x44`, and a THING in memory
has model at `+2`, mX at `+4`, mY at `+6`, and its next-in-cell link at `+10`.
**>>> THE EDGE PREDICATE `FUN_004d8750` LOOKED LIKE A WALL AND IS NOT - I nearly recorded it as one. <<<**
Decompiled, its body is fifteen calls that take **no arguments at all**, which reads as impassable. That
is the DECOMPILER DROPPING `ECX` (the `this` pointer), not the code being opaque: disassembled, fourteen
of the fifteen are 4-27 byte accessors. **Nearly all test the cell's TYPE dword at in-memory `+8`** against
a literal - `FUN_00536390` == 0, `310` == 1, `300` == 2, `340` == 9, `350` == 10, `380` == 4,
`3a0` == 0x1e, `320` == 3 or 9, `3c0` == 5 or 7 or 2. `FUN_005363b0` is `(word[+0xe] >> 10) & 1` and
`FUN_0053ad20` is `(byte[+0xe] & 0xf) != 0`, so **`+0xe` is a flag word**. `FUN_00522770` returns
`byte[+0xc]` (or `byte[+0x22]` for one thing type) and **`FUN_00522850` returns `byte[+0xd]`**, which the
tail of `FUN_004d8750` tests **PER DIRECTION: west 0x04, east 0x40, north 0x10, south 0x01** - a wall
mask. Only `FUN_005363f0` (72 bytes), `FUN_00536440` and `FUN_0042a440` (153) are substantial.
**Also fully specified and implementable now: the four map-boundary guards** - `x == 0` blocks west,
`y == 0` blocks north, `x == 0x7f` blocks east, `y == 0x7f` blocks south - and the per-direction edge
index `x + y*128` shifted by **north `-0x7f`, east `+2`, south `+0x81`, west `+0`**.
**>>> THE TRAP BEFORE BUILDING ANY OF IT: those are IN-MEMORY offsets. <<<** Our `ParkWorld.MapCell`
reads DISK offsets (`CellType = 24`, `CellFlags = 1`, `CellNeighbours = 7`, `CellDirection = 0`). The
correspondence between in-memory `+8`/`+0xc`/`+0xd`/`+0xe` and those is **NOT established** and must not
be assumed - this project has already been burned once by using memory offsets as file offsets, which
parses and is wrong. Establish it before writing a line of the predicate.
**>>> STAGE 3 INCREMENT 3 IS BUILT: `7867ed6`, the STEP GEOMETRY, 352 -> 361. <<<** `MapStep.cs` +
`MapStepTests.cs`: cell id <-> (x,y) both ways, the `|dx| > 1 || |dy| > 1` refusal, the four boundary
guards, and **the diagonal corner rule**. A diagonal is two steps round a corner and either L-route will
do; the original tries the **LOWER-NUMBERED direction first** - N before E, N before W, S before W, but
**E before S**. Three of the four look like "vertical first" and generalising from them puts south-east
the wrong way round, so all four are pinned and that wrong rule fails. **The edge predicate is a
PARAMETER, not modelled**, for the memory-vs-disk reason directly above. The `fromY == 0 && toY == 0x3ff`
off-map guard is **left out and recorded as unexplained** - as decompiled the destination is masked to 16
bits before the shift, capping it at `0x1ff`, so `0x3ff` cannot occur and no reading of it is honest yet.
**>>> WHAT EVERYTHING LEFT NOW DEPENDS ON: THE MAP CELL REPRESENTATION. <<<** The real edge predicate
needs the in-memory/disk correspondence; the separation query needs **per-cell thing lists**, which
OpenTPW does not maintain; the pathfinder needs walkability. All three funnel through the same thing, so
**the map cell reader in the exe is the next decode**, not any of the three.
**THE WAY IN, found 2026-09-16 by xref rather than by guessing.** **`FUN_004d7ea0` is the CELL WRITER** -
it owns all three of `"Saved %d (%d %%) map cells as default"`, `"...track cells..."` and
`"...RE cells..."` (so **"RE" is the effects record**, matching our own `MapRecord 0x1 / TrackRecord 0x2 /
EffectsRecord 0x4` at 52 / 31 / 10 bytes). A write branch states every size outright, which is how the
person base was sized, so **that function is the route to the disk cell layout**.
**`FUN_0050fdf0` owns the string `id_walls`** and sits inside the navigator module, so it is very likely
where the per-direction wall mask at in-memory `+0xd` is read - i.e. the edge predicate's actual data
source. **`FUN_004d0b30` owns `mTileData`** (twice) and `FUN_004c5d70` owns `mMapSquaresPerCell`.
**>>> A DEAD END, RECORDED SO IT IS NOT CHASED AGAIN: `FUN_004d1660` IS NOT THE MAP LOADER. <<<** It has
**14 references to the map base `DAT_007cf6ec`, more than any other function**, which is exactly why it
looked like the loader and why I said so before reading it. It is a **goal / objective evaluator**: a
switch over ~33 cases comparing counts and percentages (`param_1[2] <= *param_2`), with a staff-type
mapping at case `0x1d` and the give-away string *"There really ought to be a staff..."*. Reference COUNT
is not evidence of ROLE.
**>>> THE CELL CORRESPONDENCE IS ESTABLISHED, 2026-09-16 - AND THERE WAS NO WALL. <<<** `FUN_004d7ea0` is
the whole-map save/load: **16,384 cells**, a **one-byte status** each, bits **1 map / 2 track / 4 effects**
(so "RE" = effects), delegating to **`FUN_004d8dd0` map, `FUN_0050c200` track, `FUN_00502490` effects**.
IN-MEMORY strides: **map 0x44 = 68, track 0x28 = 40, effects 10**; its effects base `param_1 + 0x1b1104`
is **exactly** the RegionFX address this file recorded by a different route.
**`FUN_004d0b30` is the 29-byte TILE BASE** and its write branch names every field with its size, disk
order alphabetical as always - mDirection 1, mFlags 2, mMeshInstance 4, mNeighbours 1, mOverlapCounter 2,
mParentID 2, mTileData 12, mType 4, mHoardingNeighbours 1 = **29**. `FUN_004d8dd0` adds the litter tail -
mLitter 4, mLitterCollector 2, mLitterScript 4, mLitterScript 4, mPylonIndex 2, mStatusFlags 1,
mTimeMarkedForLitterCollection 4, unnamed 2 = **23**. **29 + 23 = 52 = our `MapCellSize`**, and both halves
match the wording `ParkWorld.cs` already carried.
**>>> OUR FIVE DISK OFFSETS ARE CONFIRMED BY NAME AGAINST THE EXE'S OWN READER. <<<** Cumulative disk
offsets are 0, 1, 3, 7, 8, 10, 12, 24, 28 - so `CellDirection = 0` IS mDirection, `CellFlags = 1` IS
mFlags, `CellNeighbours = 7` IS mNeighbours, `CellTileData = 12` IS mTileData, `CellType = 24` IS mType.
**THE MEMORY/DISK MAP, which was the blocker:** memory **`+8` = mType** (what every `FUN_00536xxx`
predicate compares), **`+0xd` = mDirection** (the mask `FUN_00522850` returns, tested **west 0x04, east
0x40, north 0x10, south 0x01**), **`+0xc` = mNeighbours** (`FUN_00522770`, or `+0x22`
mHoardingNeighbours for one thing type), **`+0xe` = mFlags** (`(>>10)&1` and `&0xf`). **So every field the
edge predicate needs is ALREADY PARSED by `ParkWorld.MapCell`** - the caution was right to demand the
correspondence, and the correspondence turned out to be exact. Cell `+0..+3` is runtime-only (the thing
list head `FUN_005102f0` reads), which is why the saved fields start at `+4`.
**>>> THE STEERING BEHAVIOUR LIST IS READABLE AFTER ALL - I had written that it was not. <<<**
**`FUN_0050fdf0` BUILDS IT**: it clears `DAT_007cf078..DAT_007cf07c` (the exact bounds `FUN_0050f3b0`
walks) and pushes three behaviours looked up **BY NAME** from a registry at `PTR_DAT_00761ec0..0x761ee0`:
**`avoid_walls` at 0xe666 = 0.9, `follow_path` at 0x8000 = 0.5, `separation` at 0x1999 = 0.1.** The full
vocabulary in the binary is `seek, arrival, separation, avoid_obstacle, repel_obstacle, avoid_walls,
follow_path` - only those THREE are registered for peeps. **Which FUNCTION implements which NAME is NOT
pinned**: the registry entries point at `0x7cf0xx` and are populated at runtime, so they read empty
statically. Do not assert a mapping from that table.
**>>> I GOT THIS WRONG AND COMMITTED IT. THE COMMIT `92a6c09` IS REVERTED - do not rebuild it. <<<**
I read `mDirection` as a WALL BITMASK and wrote `MapCell.WallNorth/East/South/West`, `HasWall` and
`MapStep.WallBlocks` on top of it. **It is not a mask and it is not the field the predicate bit-tests.**
The disassembly of `FUN_004d8750`'s tail is unambiguous: at `004d897c` it calls **`FUN_00522770`, which
returns `[ECX+0xc]` = `mNeighbours`**, and bit-tests THAT - `AND AL,0x10` north, `AND AL,0x1` south, and
`DEC BL; NEG BL; SBB BL,BL; AND BL,0xc4; ADD BL,0x40` giving 0x40 east / 0x104 -> 0x04 west. The trailing
`NEG AL; SBB EAX,EAX; INC EAX` means it returns **true when the bit is CLEAR** - so it is a CONNECTIVITY
mask with INVERTED sense: no neighbour that way means you cannot go that way. Separately, at
`004d8ab0-004d8aed`, **`FUN_00522850` returns `[ECX+0xd]` = `mDirection`** and is compared by **EQUALITY**
(`CMP AL,0x4 / 0x40 / 0x10 / 0x1`), never bit-tested.
**THE MEASUREMENT SAYS THE SAME THING** (`~/.cache/tpw-harnesses/cell-attributes.py`, `neighbours.log`):
`mNeighbours` has **38 distinct values with 82 cells carrying more than one bit** (0x44, 0x11, 0x1f, 0xf1,
0xf5...) - a mask; `mDirection` has **5 values and NEVER two bits** - a single value. **My "80 walled
cells, never two sides, walls are one-sided" was just restating that `mDirection` is single-valued**,
dressed up as a fact about walls. The tests passed only because over 0/1/4/16/64 a single value and a
one-bit mask are indistinguishable. **The docs were already right**: `saves.md` calls `mDirection` and
`mNeighbours` a shared compass, and this session's cell work was CORROBORATION of that page, not discovery
- it already carries the 29+23 split, the full field table, and the same `mType` counts, and it even names
`+50 u16 mWho`, which this file still had as "unnamed 2". **16384/16384 cells carry a map record**, which
does agree with the shipped reader.
**>>> THE COMPASS IS SETTLED BY ADJACENCY, AND THE PREDICATE READS THE OPPOSITE SIDE'S BIT. <<<**
(`~/.cache/tpw-harnesses/neighbour-compass.py`, log beside it.) Two candidate bit/side pairings were
scored against adjacency a cell REALLY has - for each path cell, is the neighbour on that side also path?
**The docs' pairing wins 94.8% to 75.3%, and on all four sides separately** (north 77/5 vs 68/14, east
81/1 vs 55/27, south 74/8 vs 67/15, west 79/3 vs 57/25), far outside the 5% band I had set as "this
discriminates nothing". So in `mNeighbours`: **0x01 is `-y`, 0x04 is `+x`, 0x10 is `+y`, 0x40 is `-x`.**
**THE DIRECTION NUMBERING IS ABSOLUTE** - `FUN_004d8750`'s boundary guards refuse `x==0` going 3, `y==0`
going 0, `x==0x7f` going 1, `y==0x7f` going 2, so **0 = `-y`, 1 = `+x`, 2 = `+y`, 3 = `-x`**.
**PUT TOGETHER: asked about direction 0 (`-y`) the predicate tests 0x10, which is the `+y` bit** - so it
consults **the side OPPOSITE the way it is going**, which reads naturally if `mNeighbours` records which
sides a cell may be entered FROM. Both facts are measured; the join between them is the inference.
**NORTH/SOUTH IS NOT A QUESTION THIS PARK ANSWERS.** The original names its directions 0-3 and nothing
else. Jungle's entrance is at cells **(47,17)/(48,17)** and its **bus sits outside the map at (29.7,-11.5)**
(`ParkFixedItems.cs`), so guests walk up from low `y` - which fixes the geometry without fixing a compass.
`MapStep`'s `StepDirection` names are therefore LABELS over the axes, and `7867ed6` is behaviourally
correct whichever way they are read.
**mType in the park:** 7 (9077), 0 (6875), 2 (240), 1 (78), 30 (66), 4 (35), 9 (8), 3 (4), 10 (1) - nine
values, and **every one is inside the ten literals the predicate compares against**, zero outside.
**mFlags:** `(>>10)&1` set on 14 cells; `&0xf` set on **none**.
**STILL NOT MODELLED, and named rather than guessed:** the middle of `FUN_004d8750` - the `mType`
comparison chain and the branch that asks a catalogue object on the cell whether it is in the way.
`FUN_005363f0` and `FUN_0042a440` both lose their `this` in the decompiler and reach into object scripts
(`FUN_0055a070/360/570`, `FUN_00436840/80`, `FUN_0044b220`). **The wall term alone is exact and is what is
built**; the code says outright that it is one term and not the whole test, because the failure mode there
is a step wrongly ALLOWED.
**>>> THE `save/` BASELINE MOVED AGAIN, 2026-09-16 - do not read it as damage. <<<** It was
`23a4922d649e799d6dbfa089e9168a72` and is now **`533d83248759b506c4633c12f99ff62c`**. Only
**`save/opentpw.cfg`** changed - OpenTPW's OWN settings file, which says so in its own header - because
launching the real game makes the loading screen persist the step counts it
learns ([[loading-screen]]). It has since gained `hallow.first` and both `the-lobby` lines. **The
original's `Config.tcf` is byte-identical at `6b3db4f13060d07b4603daad200e9217` and is the thing to assert
on**; those are the only two files in `save/`. The test suite still writes nothing there - the aggregate
moved because a game launch sat between two measurements. Commit gates from here assert BOTH the new
aggregate and `Config.tcf` separately, so a real change to the original's data still aborts.

**>>> THE DOCS HALF IS DONE AND PUSHED, 2026-09-16: `docs/save-person-record` = `fda26d3`, NEW on origin,
ONE commit over `docs/item-footprints` (`83ce2e4`). <<<** Pushed at a fresh yes ("You may push"), **ONE
named refspec, no force, no PR** - GitHub offered `pull/new/docs/save-person-record` and it was **not**
used. **THAT YES IS SPENT - the next push needs a fresh one** ([[ask-before-github]]). All six
before-checks were hard assertions that would have aborted before any network write. After:
`local == remote` at `fda26d3d8da2a2d7b1d042a3440d648686d5c5d0`, **0 ahead**, parent `83ce2e4` unmoved,
`master` still `0e8d5d0` on origin AND upstream, **upstream PR #1 untouched at `d73a627`**, **0
`refs/pull/*` on origin**, tree clean. **A BASELINE site build was taken before editing and the site was
built again before committing** - both clean at 16 pages, so a failure would have been attributable to my
edit rather than inherited. `saves.md` gains "A person: models 1 and 4-8" - the 390-byte person base, the
guest's 135-byte block in full, and the four specific checks a reader should use; `sam.md` gains that the
simulation's constants live in the balance file and read zero in the executable.

**>>> CHOOSING THE PARENT BRANCH WAS THE HARD PART AND I GUESSED WRONG TWICE. <<<** The docs branches are
**disjoint stacks off `master`**, so a page's content is a **branch artifact**: `saves.md` exists in three
different states at once - 41 lines on the `docs/md2-*` chain, with `TPCS` on `docs/particles`, and with
the whole World block and thing list on `docs/item-footprints`. I was about to commit onto the first, then
onto the second, before checking which branch actually owned the material; only `docs/item-footprints`
already documented the thing list, which is what my work extends. **And I then wrote a link to
`#the-sprite-table-tpcs`, a heading that exists only on `docs/particles`** - caught before committing by
inventorying every link against the headings that actually exist on this branch. New **rule forty** in
[[verify-every-ordering]]; the topology is recorded in [[opentpw-fileformats-docs]].

**>>> THE WORLD-SPRITE SCRIPT VM IS FULLY DECODED, 2026-09-16. NOTHING BUILT, NOTHING COMMITTED. <<<**
Analysis only - the tree is still at `8bd02b0` with 0 ahead and only the four never-commit paths. Read
this before touching sprites; it answers three things the tracker previously listed as unknown.
- **THE SCRIPTS ARE COMPILED INTO `testme.exe`, NOT IN THE GAME DATA.** `DAT_0074dab8`, **1,857 dwords =
  7,428 bytes = 83 scripts**, every one a loop. `FUN_00475730` sets every instance's `+0x14` to that
  address unconditionally and reads **no** bytecode from the save. **The save's `SPSC` module is the
  `TPCS` INSTANCE TABLE, not the scripts** - `4 + 4 + 4 + 400 + 18*280 = 5,452`, the module's stated size
  to the byte. (I nearly recorded "there is no SPSC block" because I searched for the ASCII spelling of a
  little-endian tag - now **rule thirty-six** in [[verify-every-ordering]]. The tracker was right.)
- **A SCRIPT IS: select a set, step frames, jump back.** 18 opcodes, four of which cover 828 of the 874
  handler dwords. The two that matter write **exactly the two fields `ParkWorld.Sprite` already reads**:
  `LAB_004768b0` **SETSET** -> instance `+0xb4` (`SpriteNumber`, so `Set` and `BankOffset`), and
  `LAB_00476940` **FRAME** (581 uses) -> instance `+0xb8` (`Frame`), which then sets `+0x114` via
  `FUN_00540b90` and parks the instance at state **2** - the state all 18 shipped sprites are in.
  `LAB_00476020` **JUMP** reads a pointer and sets pc to `(ptr - base) >> 2`. Others: `LAB_004766d0`
  SETLOCAL (local index 1-6 stored as FLOAT via FILD, else raw int), `LAB_004768d0` FRAME via local,
  `LAB_00476870` local:=local, `LAB_004766c0` bare yield, `LAB_004762f0`/`LAB_00476050` loop close/open
  (`+0x70` depth, max 10), `LAB_004763b0`/`LAB_00476340` gosub (`FUN_00475230` pushes onto a 20-deep
  stack at `+0x20`, depth `+0x1c`), `LAB_004769f0` clears `+0xf8`/`+0x114`, `LAB_00476a10` adds 0.01 to
  local `+0x24` (the constant at `0x006feda0` is **-0.01** and the op is FSUB), `FUN_005da3c0` END.
- **THE PERSON ANIMATION TABLE IS `DAT_0075a418`**, index -> script: **[1] entry 42 = set 1, 8 frames =
  THE WALK; [3] entry 90 = set 0, 1 frame = THE STAND; [9] entry 2 = set 1, 16 frames = a half-speed
  walk**; [2] set 2, [4] set 14, [5] set 12, [6] set 11, [7] set 6, [8] set 4, [17]-[24] sets 4/4/5/3/6/7/
  8/9. Entries **[13]-[16] are the literals 0,1,2,3**, the state indices `FUN_00475a10` routes through
  `PTR_PTR_0074f7c8` (four entries only, the string `"Bad test code in IF stat..."` sits immediately
  after it). For a state 0-3 the initial set comes from the ESP's four groups at `0x14E` via
  `FUN_00540c70`; otherwise the caller passes a script pointer outright.
- **WHICH SCRIPT AN INSTANCE RUNS IS ITS OWN `+0x0c`**, not `mSpriteScript`. Sixteen of the shipped
  park's eighteen carry `+0x0c` = **42** (mid-walk, pcs 49/51/53/55/57/59/63 scattered across that frame
  list, which is why branch 80 measured "frame scattered across the walk cycle") and two carry **90**
  (standing). Changing animation goes `FUN_004217f0(this,n)` -> `this[1] = n` (pending), applied later by
  `FUN_004d4190` -> **`FUN_00475b80`**, which writes `+0x08` AND `+0x0c` and resets `+0x1c`, `+0x70`,
  `+0x74`, `+0x78`. `FUN_00475c50` is a **predicate** ("is this sprite on that script?"), not a setter.
- **THE STEP:** `FUN_00475360` runs an instance when `now > +0x7c`, then sets `+0x7c = +0x80 + now`;
  `+0x80` is the interval, **0x3e = 62 ms** at construction, 44-103 across the shipped eighteen. The
  budget is `DAT_007b4a10 = 0x32` = **50 opcodes per call** (`FUN_00475290`).
- **>>> UNRESOLVED, AND IT MUST BE SETTLED BEFORE ANY OF THIS IS BUILT. <<<** `FUN_004fa2a0` computes
  `SQRT(dx*dx + dy*dy)` - the distance the peep moved that step - times `_DAT_007006e8` = **3.8296e-05**
  (doubled on one branch), and `FUN_004d41d0` clamps it to **0..250** into `this[2]`, which
  `FUN_004d4190` hands to **`FUN_00475610`**, which writes the instance's **`+0x80`**. Every link is
  measured. But a larger `+0x80` means a LONGER wait, so this reads as *more distance -> slower
  animation*, which is backwards. Either a unit is wrong or a link is. **Do not implement the rate from
  this chain until the direction is explained.**

**>>> WHAT COMES AFTER STANDING GUESTS - READ THIS WHEN RESUMING. <<<** Branches 80 AND 81 are finished
and pushed; the people stand, face correctly, and now wear the art the save actually named. **Nothing is
owed to either repo** - both were verified `local == remote` after pushing, with 0 pull refs on each.
Three things are worth doing next, in the order I would take them:
1. ~~Close the two provisional choices in shipped code.~~ **DONE on branch 81 (`8bd02b0`) - see the
   entry above.** Both are closed and neither answer was the one expected: the span is exactly `20/128`
   and the unpinnable second factor is the **viewport's aspect**, not a world term; and a park numbers
   **only its own theme**, so `FolderFor` was right. Closing them uncovered the kid-bank ordering bug,
   which is what branch 81 actually fixes.
2. **GUESTS WALKING IS RE-PRICED AND DEMOTED, 2026-09-16 - ITS PREMISE WAS WRONG TWICE OVER.** The
   sprite-script VM is now **fully decoded** (block below), and it is the *display* half of a system whose
   input is the peep simulation - **so it cannot be built on its own.** Sixteen of the park's eighteen
   people are parked mid-**walk** (script entry 42, set 1, eight frames), so running the VM alone would
   stride them **on the spot**, which is visibly worse than the frozen pose they have now and is
   demonstrably not what the original does: `FUN_004fa2a0`, in the peep walk path, asks `FUN_00475c50`
   whether the sprite is already on the walk script, then computes `SQRT(dx*dx + dy*dy)` - **the distance
   the peep actually moved that step** - and hands it to `FUN_004d41d0`, which clamps it to 0..250 as the
   animation **rate**. Animation is driven BY locomotion. And the cost is not the "5,712 bytes" this list
   used to quote (that was `FUN_0055abf0`, which is the **flying cars**): it is the peep module at
   **281 functions / 95,152 bytes** plus the queue module at **89 / 19,684**. The `guests` and `facing`
   debug commands from `ef4f091` still exist to make it legible when it is built.
3. **>>> THIS RECOMMENDATION IS WITHDRAWN - DO NOT ACT ON IT. See "THE AGREED PLAN" above. <<<** I
   recommended the park HUD here on the strength of [[park-hud-from-exe]]'s line "no clock, no cash, no
   toolbar, no advisor", **and all four of those were FALSE** - `ParkGadget.cs` is 513 lines with a live
   date and the six-button toolbar, built on branches 57-60, and the advisor runs in a park
   (`Level.cs:289`). The gadget is a **shell** (4 of 6 buttons are log-only `NotYet(...)`), and the screens
   those buttons open need economy data nothing reads yet. Kept as the record of a bad call, not as advice.
   The original wording follows. Independent of
   everything above, and the only one of the three that is shovel-ready: [[park-hud-from-exe]] holds the
   layout-stream opcode format **fully decoded**, the mesh-name hash cracked 39/39, and every park panel's
   stream transcribed with ids, rects and artwork; [[park-advisor-from-exe]] records that the "needs
   simulation" blocker is **WRONG** for screen-open lines, because the acceptance gate consults no world
   state - so that class waits only on the HUD.
**Do NOT re-open:** whether the sprites should face the camera (settled by Alexah from the original, and
by capture), whether guests render at all, whether I can screenshot-verify park work myself (I can), the
world-size constant (`20/128` exactly, measured), the aspect factor (the viewport's, never ours to
apply), or the theme scoping (own theme only). All four were closed on branch 81.
- **`b331950` - the reader.** `ParkWorld` now keeps the park's **people** (models 1, 4-8) and reads the
  **`TPCS` sprite table** that follows the World trailer by four bytes, closing on its own `CSPS` tag the
  same self-proving way the World walk closes on `DLRW`. `SpriteBankFile`'s fourth set byte was a `bool
  Directional` and is now a **direction COUNT** (5 on 201 in-use sets, 7 on ten - the `*heads` - 4 on one,
  0 on 71). Nothing consumed the old flag, so nothing was visibly wrong; it just said less than it knew.
- **`1b91b27` - the renderer.** New `ParkGuestSprites` + `content/shaders/guests.shader`: world-space
  camera-facing quads, depth-tested, alpha-blended, atlas-textured, wired into `Level` after `ParkGround`.
- **THE LINK, which was the one real blocker: a person's `+0x10` (`mSpriteScript`) is their sprite SLOT.**
  Not a pointer (the table's handles are stale heap addresses appearing nowhere else) and not positional
  (the two orders disagree). 18 people carry 18 distinct slots = exactly the 18 live ones. A verifier's
  20,000 guest-only reshuffles scored **0** on the bank column.
- **FACING IS MEASURED AND MINE: 18/18.** Heading is an **11-bit angle at disk `+0xf2`**;
  `((angle - 0x380) & 0x7ff) >> 8` reproduces the octant the sprite stores at `+0xc0` on every person, on
  a walker that self-proved first (42 things, 18 people, closes on `DLRW`). Not vacuous: five distinct
  octants. Index is **camera-relative** - `(cameraOctant + facing) & 7` - so it is recomputed every frame.
- **>>> TWO PUBLISHED DOC CLAIMS WERE WRONG AND ARE NOW CORRECTED (docs commit `998d92f`). <<<** The
  record **does** carry a position (floats `+0x88`/`+0x8c`/`+0x90`) - my old scan swept only cell-shaped
  values, which is now **rule thirty-three**; and `+0xb4` is a packed bank-offset+set, not a "variant".
- **ATLAS HAZARD, measured:** all 39 person banks = 5,316 pictures = an atlas **1024x13,885px**, past the
  8192 limit many devices enforce. The pool therefore loads **only the banks the park's people wear**.
- **PROVISIONAL, and said so in the code:** the world-size constant (its second factor is filled in at
  device setup and reads 0 statically), the theme-scoping of entertainers/costumes, and whether our
  camera's zero yaw matches the original's - if not, every sprite is off by the same number of eighths.
- **Gates all met:** clean build **127/0**, **299/299** (295 baseline + 4 new), 0 skipped, `save/`
  byte-identical across every run, and **each commit built ALONE in a throwaway worktree** - `b331950`
  also passing 299/299 by itself, which proves the reader commit needs nothing from the renderer.

**>>> BRANCH 79 IS DONE AND PUSHED, 2026-09-15 - `alexah/79-the-advance-belongs-in-the-sweep` =
`aeab328`, ONE commit over `544bb7e`, NEW on origin. <<<**
**PUSHED at a fresh yes ("Push it"), ONE named refspec, no force, no PR** - GitHub offered
`pull/new/alexah/79-the-advance-belongs-in-the-sweep` and it was **not** used. **THAT YES IS SPENT - the
next push needs a fresh one** ([[ask-before-github]]). The before-check was written as hard assertions
that would have aborted before any network write, and because the branch was **new on origin** it asserts
its ABSENCE rather than a tip. After: `local == remote` at `aeab328`, **0 commits ahead**, `main` still
`453e779` on origin AND upstream, **0 `refs/pull/*`**, branch 78 unchanged at `544bb7e`, one worktree,
tree holding only the four never-commit paths.
`RideScript.Turn` brought its thing's animation channels up to date at the top of every turn; the engine
does not, and the advance now lives only in the frame sweep. **295/295, 0 skipped, 127/0, `save/`
byte-identical**; `aeab328` built **ALONE** in a detached throwaway worktree at those same numbers.
**Branch-point baseline measured first: 294/294, 127/0** (`~/.cache/tpw-harnesses/baseline-79.log`).
**THE ENGINE EVIDENCE, TRACED BY ME THIS SESSION RATHER THAN INHERITED FROM THE NOTE THAT CLAIMED IT.**
In `Game_StateMachine`'s park case the whole script system `FUN_005516b0` runs INSIDE a do/while that
adds `0x1f` (31ms) per pass. The advance is not in that loop: **`FUN_004735d0` (advance + pose) has
exactly ONE caller, `FUN_00473c70` at `00473d2e`**, and **not one of that function's ten call sites is
`FUN_005516b0` or sits inside the loop** - the park's are `FUN_0044e410(2)`, `FUN_00429df0(0)` and
`FUN_00429df0(1)`, all past the back edge, all consuming the single clock snapshot `FUN_00473440` takes
above it.
**TWO CANDIDATES CHECKED AND CLEARED, WHICH IS WHY THIS IS NOT A GUESS.** `FUN_00473440` sits in the
animation module and runs once per frame ABOVE the loop, so it looked like the sweep - it is not, it
computes the frame delta into `DAT_007b497c`. `FUN_00475360` DOES run inside the loop every 2nd tick and
sits at `0x00475xxx` - also not a sweep, but a periodic-task scheduler (`+0x7c` due time, `+0x80`
interval) that is not among `FUN_00473c70`'s callers. **Had either been the sweep, this branch would have
been BACKWARDS.**
**WHAT MOVES AND WHAT DOES NOT.** End-of-frame state does not move: `ParkRides` hands the sweep
`Ticks * 31`, which is exactly the instant its last tick ran at. What changes is mid-catch-up - with
three ticks due, a clip ending on the first had its queued successor promoted there and then, and the
second tick's instructions could see it running, which the engine cannot reach. **Scripts stay correct
without the per-tick advance because every path that reads channel state to answer one goes through
`RideAnimations.Trigger`, and that calls `MoveTo` on the channel itself first** (`RideAnimations.cs:233`).
**ONE CONSEQUENCE NAMED RATHER THAN LEFT TO BE FOUND:** anything bound through `ParkRides.cs:196`'s
fallback - a placed thing that is not STANDING, so its clips are read afresh - is no longer advanced at
all, because `ParkObjects.Sweep` walks only `_standing`. That matches the engine (a thing not in the
world's table is not swept) and nothing draws those, but it is a real change.
**TWO CONTROL RUNS, PREDICTIONS WRITTEN FIRST TO `~/.cache/tpw-harnesses/control-79-prediction.txt`, BOTH
HIT.** A (the advance put back) predicted 1 named failure AND the value `~31.86, not 631.86`, reasoning
that the promotion carries the overshoot and backdates the start stamp; got that exact test and assertion
at **31.83** - the three hundredths mine, because `MillisecondsFor( 30 )` truncates to 999ms. B (the sweep
line this branch ADDED to an existing test, deleted) predicted 1 named failure observing **0**, and got
exactly that - so that line is load-bearing rather than decoration.
**>>> A DEFECT IN MY OWN TEST, CAUGHT BY THE GUARD I HAD PUT THERE FOR IT. <<<** The new test first waited
on `Lit( 60000 )`, and a literal is **sign-extended from its low sixteen bits**, so it arrived as
**-5536**: the deadline was already past, the script fell through to `END` and STOPPED. `Turn` leaves at
its first line once stopped, so every other assertion in that test would have passed trivially. Only
`Assert.IsTrue( script.Running, ... )` caught it. New **rule twenty-seven** in [[verify-every-ordering]].
**Backup of both files at `~/.cache/tpw-harnesses/branch-79-backup/`; both controls restored from the COPY
and verified by md5, never `git checkout --`.**

**>>> BRANCH 78 IS DONE AND PUSHED, 2026-09-15 -
`alexah/78-the-easing-curve-on-a-rotation-key` = `544bb7e`, ONE commit over `64c0842`, NEW on origin. <<<**
**PUSHED at a fresh yes ("Push both"), ONE named refspec, no force, no PR. THAT YES IS SPENT** ([[ask-before-github]]).
A rotation key's second ushort was read as "flags (0 or 0xFFFF)" and ignored; it is an **easing curve id**,
and `0` is not a cleared flag but **curve number nought**, the commonest id in the game - which is exactly
why the field looked like a flag only ever set or clear. **294/294, 0 skipped, 127/0, `save/`
byte-identical**; `544bb7e` built **ALONE** in a throwaway worktree on `/home` at those same numbers.
**Branch-point baseline measured first: 288/288, 127/0** (`~/.cache/tpw-harnesses/baseline-78.log`).
**THE DECODE, RE-VERIFIED IN GHIDRA BY ME RATHER THAN INHERITED FROM THE NOTE.** The remap is
`0x00471c83`-`0x00471d32`, just before the rotation sampler `FUN_00474490`. A curve is **8 bytes** at track
descriptor `+0x34`, indexed by the id; the bytes are the **inner** points of a ramp whose ends are IMPLIED
(0 before the first, 1 after the last), so it is **ten points and NINE segments**. Segment count is the
float at `0x006febe4` = **8.999995231628418**, deliberately under nine: the engine truncates, so an exact
nine would push `t == 1` into a tenth segment with no upper point and answer `curve[7]/255` - a jump
BACKWARDS on the last frame. Byte scale `0x006febec` = 1/255.
**FOUR THINGS THE OLD NOTE DID NOT SAY, each measured:** the id belongs to the **LOWER** key of the pair;
the id is **NOT** the key's index (**9,358 of 12,428 agree, 3,070 do not**, ids reach **100**) so the table
is indexed, never walked; **every eased track's last key is `0xFFFF`**, 1,759 of 1,759; and **2,797 of
12,428 curve entries are non-monotonic**, so a pose really does travel back the way it came mid-blend -
reproduced, not sorted. The old "a third are not monotonic" was measured at no fixed stride; at eight it is
about 22%. The old "byte ramp (Advisorm1: 32, 66, 105, ...)" is **curve 0 of the advisor's first clip**,
read at the right stride by luck.
**A QUESTION CLOSED ON THE WAY:** `FUN_004742a0` (real slerp) is skipped for a plain component lerp when
`DAT_007b4980 & 2`. Its **only** writer is `FUN_004740d0`, whose **only** caller is `FUN_00457a90`, which
passes **0** - so the bit is never set and our `Quaternion.Slerp` is right. Do not re-open this.
**TWO CONTROL RUNS, PREDICTIONS WRITTEN FIRST TO `~/.cache/tpw-harnesses/control-78-prediction.txt`, BOTH
EXACT.** A (easing disabled) predicted **4 named** failures and got exactly those 4 - and the prediction was
**revised in writing before running**, because my first count of 3 forgot the two `Ease` lines I had just
added to the id-indexing test. B (index `Curves[key]` not `Curves[id]`) predicted **1 named** and got it -
that one exists because Advisorm1's ids happen to equal their key indices, so every test built on that clip
is blind to the mutation by construction and a second clip was needed.
**>>> A MISTAKE THAT COST WORK: `git checkout --` AS A CONTROL RESTORE DESTROYED UNCOMMITTED WORK. <<<**
Control A restored with `git checkout -- <file>`, which reverts to **HEAD** - and the implementation was not
committed yet, so it wiped all five edits, not just the mutation. The md5-after check caught it
(`3e39a48f` -> `2116d07b`) and the work was rebuilt from the edits. **Restore from a COPY, and commit before
running a control.** New **rule twenty-six** in [[verify-every-ordering]]. The control's own result was
still valid - it ran on implemented+mutated code and only the restore was wrong.
**Backup of both files kept at `~/.cache/tpw-harnesses/branch-78-backup/`.**
**THE DOCS HALF IS COMMITTED: `d8b9447` on `docs/md2-easing-curve`, STACKED ON PR #1's BRANCH `d73a627`**
(site built clean, 16 pages; PR #1 verified untouched after). A standalone
`md2.md` was written and then **withdrawn uncommitted**: the `.md2` page is not missing after all -
`models.md` already documents the format on the branch carrying open PR #1, and it is that page which
carries the stale easing claim this branch disproves, so a second page would have duplicated it. The draft
is kept at `~/.cache/tpw-harnesses/md2-easing-page-draft.md` as source material. **PUSHED on the same yes;
PR #1 verified untouched at `d73a627` before AND after the push. That yes is spent.**

**>>> BRANCH 77 IS DONE AND FULLY PUSHED, 2026-09-15 - `alexah/77-fixed-items-run-their-own-scripts` =
`64c0842`, TWO commits over `c9f4c34`. <<<** `b1182d3` went up alongside branch 76 on one yes ("Push both 76
and 77"); **`64c0842` went up on a SECOND, separate yes** ("I meant to specify yes to push... Please push to
my repos"), as **one named refspec, no force, no PR**. **BOTH YESES ARE SPENT - the next push needs a fresh
one** ([[ask-before-github]]). That second commit scopes a claim the first one overgeneralised (see the
three-themes-out-of-four note below) and measures **288/288, 0 skipped, 127/0, `save/` byte-identical**,
built **ALONE** in a throwaway worktree on `/home` at those same numbers.
**THE UPDATE PUSH ASSERTED ANCESTRY, NOT ABSENCE** (`git merge-base --is-ancestor b1182d3 64c0842`), because
it was the first push here to a branch **origin already had**: that is what proves a fast-forward and that
nothing on origin is discarded. After: `local == remote` at `64c0842`, 76 unchanged at `c9f4c34`, `main`
still `453e779` on origin AND upstream, **0 `refs/pull/*`**. The docs fork needed nothing - already in sync
at `16a3076`, with upstream PR #1 untouched at `d73a627`. **Branch 76 (`c9f4c34`) went up on
the same yes.** Both were **new on origin**, which is why the before-check asserts their ABSENCE rather than a
tip; it was written as hard assertions that would have aborted before any network write had `main`, the pull
refs or either local tip moved. After: `local == remote` for both, `main` still `453e779` on origin AND
upstream, **0 `refs/pull/*`**, one worktree, tree holding only the four never-commit paths. **THAT YES IS
SPENT - the next push needs a fresh one** ([[ask-before-github]]).
The gate and the traffic lights now bind the scripts they ship and are swept from the very registry the
placed things are. **287/287, 0 skipped, 127/0, `save/` byte-identical**; `b1182d3` built **ALONE** in a
throwaway worktree on `/home` at **127/0 with 287/287**.
**>>> IT IS DELIBERATELY INVISIBLE, AND BOTH REASONS ARE MEASURED. DO NOT "FIX" EITHER. <<<**
`Gates.RSE` **idles**: with its variables at nought it cycles FIVE instructions round its dispatch loop and
reaches no animation at all, because the only thing that ever writes `VAR_COMMAND` is opening or closing the
park (`FUN_00519ef0`: 1 = open, 0 = shut, 2 = a ONE-WAY branch that self-loops and never obeys another
command, so never poke a 2 to "test" it). **OpenTPW has no park open/close concept**, and `RideScript`
exposes **no public way to write a variable from the world** - that is the real next step this exposed.
`lights.RSE` is the opposite - an unconditional `LOOPANIM 5 0` as its SECOND instruction - **but both clips
it loops declare ten frames and carry ZERO tracks**, so a correctly wired crossing spins a channel for ever
while nothing on screen can move. That is also why the old suffix-matching loop was only ever visible on the
gate. Whatever changes the lamps is NOT in those clips (two lamp textures and a mesh flag `0x0401` we do not
read are where to look).
**>>> THE GATE HALF IS THREE THEMES OUT OF FOUR, NOT A RULE - MEASURED AFTER THE COMMIT, AND IT CAUGHT MY
OWN DOC COMMENT OVERGENERALISING. <<<** All four themes ship a DIFFERENT `Gates.RSE` (four md5s). Jungle,
fantasy and hallow open `ENDSLICE` / `TEST VAR_COMMAND` and idle; **space opens `LOOPANIM_CH 5 2 1`
unconditionally** at word 2, before any test. It still holds still in OpenTPW, but for a SECOND reason:
**`LOOPANIM_CH` appears nowhere in `source/OpenTPW/`**, so it is counted as unimplemented rather than obeyed -
and implementing it would set that gate moving. **`lights.RSE` IS byte-identical across all four themes**, so
the lights claims generalise and the gate ones do not. Corrected in the doc comment and pinned by a test.
**THE ENGINE NEVER READS THESE TWO OUT OF THE SAVE'S PLACEMENTS.** `FUN_005156a0` searches the item
descriptions **BY NAME** for "gates"/"lights" and builds each through the ordinary object constructor
`FUN_004db090`, which ends in the script binder `FUN_004dcf90` - no flag, no branch, no position test. So
relaxing `ParkRides`'s `IsPlaced` filter was the **WRONG** shape: it would also bind the bus (no model) and
break 3 of 6 `ParkRidesTests`. `ParkFixedItems` registers them instead, via a new `ParkObjects.Stand`, and
`ParkRides` binds a script for whatever the park has **STANDING** rather than for what it placed.
**A CLAIM THIS BRANCH KILLED: "a fixed item carries no thing id".** They carry thing ids **11 and 12**, named
by the save header's own `mParkGates`/`mTrafficLights`, and catalogue ids 1601/1603 - **jungle only**, each
theme bands its own (hallow 2600s, space 3600s, fantasy 4600s), so anything hardcoding 1601 is jungle-only.
**TWO CONTROL RUNS, PREDICTIONS WRITTEN FIRST, BOTH EXACT.** Reverting the binding filter failed **0** tests -
**that is a stated coverage gap, not a pass**: `LobbyModel` needs a device, so nothing headless constructs
`ParkObjects`, and both `Stand` and the filter are unreachable from the suite. Removing `LOOPANIM`'s start
call failed **exactly 1**, the new lights test - the existing `LOOPANIM` test covers only the `WAIT4ANIM`
deadline, not the clip. Both files `md5`-identical after restore.
**FOUR NEW TESTS, all headless because a role is pure data:** the save names both things and both ship a
script; the gate idles with its three clips proven present first (anti-vacuity); the lights DO start a clip
unasked; and the lights' two clips carry no track of any kind - **pinned so that nobody later reads the
silent crossing as a posing bug**.
**>>> AND `strings` LIED TO ME: I REPORTED TWO FILES THAT DO NOT EXIST. <<<** I said `lights.wad` ships
`7lightsm1.MD2`/`7lightsm2.MD2`. It does not - a binary `0x37` byte renders as `7` flush against the next
name in a hexdump, and the archive holds **three** MD2s. **Read archive contents with `wadcat`
(`/home/alex/.cache/tpw-wadcat/bin/Release/net10.0/wadcat`, NOT on PATH), never out of `strings`.**

**>>> BRANCH 76 IS DONE AND PUSHED, 2026-09-15 - `alexah/76-fixed-items-stop-animating-themselves` =
`c9f4c34`, ONE COMMIT OVER `8417653`. It went up alongside 77 on a single yes - see the branch-77 block
above for the before- and after-checks, and note that yes is now SPENT. <<<** Made at Alexah's
request ("Might be a good time to remove the endless park gate animation cycling"). `ParkFixedItems` drove
`gates` and `lights` through `LobbyModel.Update`, so a park's gate swung open and shut for ever with
nothing asking it to - **the same defect branch 75 fixed for placed things, in the one place branch 75
could not reach**. Both are scripted things in the original (`Gates.RSE` and `lights.RSE` ship beside the
models), so the entity now has **no `OnUpdate` at all** and the two hold the pose they were built in.
**283/283, 0 skipped, 127/0, `save/` byte-identical**, and `c9f4c34` built **ALONE** in its own throwaway
worktree on `/home` at **127/0 with 283/283**.
**THE LOBBY ISLAND GATE IS DELIBERATELY STILL LOOPING** - it stands in for a park-entry event that does not
exist, and removing its loop would leave that gate shut with nothing able to open it. There are **two
gates and they are now in different states**; [[lobby-animation-triggers]] tells them apart.

**>>> BRANCH 75 IS DONE AND PUSHED, 2026-09-15: `alexah/75-putting-the-clip-on-the-model` = `8417653`,
THREE COMMITS OVER `96485b7`. <<<** Pushed at Alexah's word ("Push branch 75"), **one named refspec**,
**no PR** - GitHub offered `pull/new/alexah/75-putting-the-clip-on-the-model` and it was **not** used. The
branch was **new on origin**, which is why the before-check asserts its ABSENCE rather than a tip; those
checks were hard assertions that would have aborted before any network write had `main`, the base branch
or `refs/pull/*` moved. After: `local == remote` at `8417653`, **0 ahead**, `main` still `453e779` on
origin AND upstream, **0 `refs/pull/*`**, one worktree, tree holding only the four never-commit paths.
**THAT YES IS SPENT - the next push needs a fresh one** ([[ask-before-github]]).
Every commit was also built **ALONE** in its own throwaway worktree on `/home` at **127/0**: `3d204dd`
**280/280** (it predates the three new tests), `9de036e` **283/283**, `8417653` **283/283**, `save/`
byte-identical, no worktree left behind.
Branch point `96485b7` measured first: **280/280, 0 skipped, 127/0, `save/` byte-identical**
(`~/.cache/tpw-harnesses/baseline-75.log`). Final state **283/283, 0 skipped, 127/0, `save/`
byte-identical**.
- **`3d204dd`** - `MeshRotator` split into clock + `Pose( animation, frame )`, the `9e8700a` pattern.
  Measured at **127/0 and 280/280, exactly the baseline**, which is what a pure extraction must produce.
- **`9de036e`** - `LobbyModel.Pose` and `Rest`, `RideAnimations.Clip`/`AllClips`, `MeshAnimator.Rest`, and
  **three new tests**. One clip drives every kind of track it carries, as `FUN_004721f0` into
  `FUN_00471860` does, instead of a clock per half.
- **`8417653`** - the join. A placed thing now owns its model AND its twelve roles, as the engine keeps
  both on one record, and the script is handed **its own thing's player** rather than a second reading of
  the same archive - which also ends the double parse of every ride's clips. The sweep runs from
  `ParkRides.OnUpdate` **after** the tick loop at `GameClock.Ticks * 31`, which is the engine's order and
  the same millisecond the last tick ran at.
**TWO CONTROL RUNS, PREDICTIONS WRITTEN FIRST, BOTH EXACT (1 and 1):** `AllClips` cut to role 5 alone
failed only `EveryRolesClipsAreWhatAModelBindsAgainstAndTheNumberedRunIsNotThem`; `Clip` with its entry
bound dropped failed only `TheClipARoleNamesIsTheOneItsLengthIsAnsweredFor`. **Each run asserted the
mutation had actually landed before building**, so neither could pass as a dead control, and the file was
`md5`-identical after both restores.
**A VISIBLE BEHAVIOUR CHANGE TO EXPECT:** a placed thing whose player is idle now **stands still** where it
used to animate. That is the engine's behaviour on a freshly loaded park - the loader parks every channel
at the sentinel so the idle default cannot fire - and what used to move was a clock this program invented.
**FIVE THINGS DECODED THAT THE PLAN DID NOT HAVE, all verified in Ghidra myself:**
- **The poser is `FUN_00471860`, one call per track**, from `FUN_004721f0` (whole clip) and the sweep. Its
  FIRST act is `if ( TotalAnimFrames < AnimFrame ) return 0` - **the same strict predicate already shipped
  as `IsFinished`**, so a finished channel poses nothing. Track kinds: `0x1` position, `0x8|0x80` rotation,
  `0x1000` morph, `0x10000` UV (`FUN_004745c0`, our own UV reader), `0x20000` visibility.
  **THAT GROUPING WAS WRONG IN TWO PLACES AND WAS CORRECTED 2026-09-15 BY COUNTING THE CORPUS.**
  `0x200` is **not** a second spelling of position: it is a separate, TIME-WINDOWED path that ignores the
  `+0x18` descriptor entirely, reads `+0x24` as a (start, length) window and resolves through the parent
  node - it occurs **71 times and NEVER once with `0x1`**. And `0x400` is **not** rotation: it sits INSIDE
  the position arm deriving a heading from travel, occurs **62 times, never with `0x1`**, and every one of
  those 62 carries `0x200` - so it belongs to the `0x200` mechanism. Our reader handles `0x1` only, which
  means **71 tracks go unread**, and that is a separate gap from the posing one.
- **`FUN_00472310`'s arguments are (model, OLD clip, NEW clip)**, not the other way round - `uVar10` is built
  from `channel+0x04`/`+0x08`, what is playing NOW. So it walks the OUTGOING clip's hide list, the mask
  arithmetic is **old minus new**, and the copy is master to live. Gated `(flags & 0xc) == 0`.
- **>>> THE RESTORE MASK NEVER TESTS `0x20000`. <<<** It tests `0x289`, `0x1000` and `0x10000` only, so
  **the engine does not put a visibility track back on a role change**: what a clip hid stays hidden until a
  later clip says otherwise. **That settles the `PoseAsBuilt` precedence question** - the two compose in time
  order and are not rivals. The tracker's own note already said the hide list "is what `PoseAsBuilt`
  hand-rolls"; this is why that is safe.
- **`FUN_00472bc0` puts no first-frame bias anywhere** - total is the declared span and the stamps come only
  from the snapshot - so `AnimFrame` runs 0 to span while our samplers take absolute frames biased by the
  MEASURED `FirstFrame`. Reconciled by the recorded, already-confirmed fact that **every clip declares first
  frame 0**, so the two are the same number on shipped data while being different rules.
- **`FUN_00472d70` is the show half** (suppressed by `KeepShownFlag`): it hides the incoming clip's hide-list
  nodes, and its `clip & 0x4` arm means those entries are **node ids, not indices**.
**TWO THINGS DELIBERATELY OUT OF SCOPE, each with its reason:**
- **Position tracks are not posed.** A position key is parent-local exactly as a rotation key is, and
  `LobbyModel` composes no per-mesh node tree to put one into (`AdvisorModel` keeps one and is the pattern).
  **MEASURED, AND IT CORRECTED ME.** The Security Camera's main clip carries **rotation 0, position 0,
  morph 1, UV 0, visibility 0** - it **MORPHS**, it does not turn, and I had written the opposite into a
  doc comment before measuring. So the position gap provably cannot reach the verification target, and the
  camera is driven through `MeshAnimator`, the most mature path in the tree.
  **THE NEW RISK THAT FINDING EXPOSES:** an animator is bound only where the morph track's channel count is
  the mesh's vertex count **+2**, and the engine's recorded rule is +2 only where the track descriptor's
  bit `0x2` is set and **+1 otherwise**, where ours adds two unconditionally and has **never been
  measured**. A test now pins it on this very item, because if it bit, the camera would stand still and
  that would look exactly like posing being broken rather than like one clip read a channel short.
  **MEASURED: IT HOLDS HERE.** The camera's morph track carries **16 channels against a 14-vertex mesh**,
  so +2 is right for the verification target and the risk does not bite it. **That is one item, not the
  rule** - the engine's bit-`0x2` condition is still unverified in general, and our reader does not hardcode
  either number: it derives `ChannelCount = maxChannel + 1`, so the +2 lives in the BINDING GATE
  (`LobbyModel.BindVertexAnimations` and `MeshAnimator.Pose`), not in the reader.
- **`RideScript.Turn` KEEPS its `Advance` call.** `MillisecondsAt( TicksDue-1 )` is exactly `Ticks * 31`, so
  the last script tick and a frame sweep land on the same millisecond and end-of-frame state is identical -
  but mid-catch-up a per-tick advance can promote a queued clip earlier than the engine's once-per-frame
  sweep would. That is a semantic change and gets **its own branch and its own control run** rather than
  riding along inside a posing branch.
**THE VERIFICATION TARGET IS ITS OWN EVIDENCE:** `levels/jungle/features/camera` ships `camerac`, `cameras`,
`cameram` and `camerae` - **every one bare, not one numbered** - so `LobbyModel`'s own `{stem}M{n}` probe
binds NOTHING there, which is the whole argument for loading roles through `RideAnimations`.
**A COVERAGE GAP TO STATE RATHER THAN HIDE:** `LobbyModel` builds `Model`/`Texture`/`ModelEntity` and cannot
be constructed headlessly, and **nothing outside `Level.cs:254` ever constructs `ParkObjects`** - the test
files use only its statics. So the wiring commit is verified by the suite not regressing plus data-level
tests, **not** by a test of the wiring, and the moving camera needs Alexah's eyes.

**>>> WHAT TO DO NEXT, AS OF 2026-09-15, WITH BRANCHES 76 AND 77 DONE. <<<**
**EVERYTHING THROUGH BRANCH 77 IS ON ORIGIN - `c9f4c34`, `b1182d3` AND `64c0842`. NOTHING IS OUTSTANDING.
Both push yeses are spent; the next push needs a fresh one ([[ask-before-github]]).**
**>>> THE ROTATION EASING CURVES ARE DONE AND PUSHED - BRANCH 78, `544bb7e` ON ORIGIN. <<<**
Chosen 2026-09-15 after I recommended it over the structural items: **the easing-curve reader defect at
track `+0x34`, 457 of 1,166 clips**, was the only queued item with a **visible** payoff, and it lands on
exactly the animations Alexah confirmed on screen ("EVERY animation works ... Fountain looks great,
security cameras look great, belly bounce looks great!").
**>>> NEXT IS POSITION TRACKS. ITEM 2 IS DONE - IT IS BRANCH 79, ABOVE. START HERE. <<<**
Item 2, **the advance belonging in the SWEEP rather than in `RideScript.Turn`**, was agreed by Alexah on
2026-09-15 ("Great work! I agree with your plan") and is now `aeab328` - committed, measured, controlled
twice, and **PUSHED to origin**. The list then continues:
**POSITION TRACKS WERE MEASURED 2026-09-15 AND HAVE NO VISIBLE PAYOFF. DO NOT BUILD THEM YET.**
The reader is already RIGHT - `ReadPositionChannel` matches `FUN_00471860` bit for bit (descriptor at
`+0x18`, `*type & 2` bezier, `& 8` straight, neither = a third sampler used by nothing), the key stride
is confirmed by `FUN_00470b60` (position 4, rotation 0x14, UV 0x10), and the constants at `0x006fecb8`
(1, 3, -3, -6) expand `FUN_00474840` to the plain cubic Bernstein form our `Sample()` already evaluates.
**What is missing is only the POSING, and nothing that can currently be shown would move:**
- **The lobby carries NONE.** All 26 clips in `lobby.wad`, read through our own `AnimationFile` via
  `wadcat --anim`, report **`pos 0`**. Not one island, gate, flyer, ticket booth or globe.
- **Of the ELEVEN objects Lost Kingdom places, only `junspray` has position tracks reaching geometry** -
  all 18 of them in `JunsprayC`, the **construction** clip - and every one ends on the node's authored
  local translation (EXAMINED=18 SKIPPED=0 AGREE=18, delta 0.00000), so posing them changes the built
  pose by nothing. Corpus-wide 90 of 302 construction tracks DO differ, but not one belongs to a placed item.
- **`bouncy`, the one ride, moves a node named `'camera'` with ZERO descendants and no geometry** - an
  on-ride camera path, not a part of the model. `camera`, `fountain` and `toilet` carry none at all.
- `PoseAsBuilt` applies **visibility only**, so position was never in that path anyway.
- Jungle is the only park that ships a SAVE, so nothing else has objects placed in it to show the rest.
  **CAREFUL - do not restate this as "the only loadable park". Alexah corrected exactly that on
  2026-09-15:** *"It's the only park that exists in a save file, specifically an Instant Action (NOT Full
  Simulation) save file. All four parks exist, and in theory at this point should be loadable empty/new
  state without a save file."* So the gate on the other three is a save, not their existence - which
  means this "no payoff" verdict holds for what is on screen TODAY and is **weaker than it sounds**: an
  empty park plus build mode would put fantasy, hallow and space items in reach. (That they load empty is
  Alexah's expectation, flagged by him as an assumption, and is UNVERIFIED - worth testing directly.)
**The cost is real and was measured too:** 291 of 1,215 resolved tracks target transform-only nodes, which
`MeshRotator`'s meshCount-sized descendant table structurally cannot carry, and the lobby drives animation
through `_model.Update()` while the park drives it through `LobbyModel.Pose` - **two paths, not one**.
**Revisit when build mode exists** (construction clips would then play) or when another park can load.
**>>> SO THE NEXT ITEM IS THE HIDE LIST <<<** (`+0x1a`/`+0x38`, **854 of 1,166 clips carry one**),
chosen by Alexah 2026-09-15 over position tracks and guests. **MEASURE ITS PAYOFF BY MESHES, NOT BY
ENTRIES - a raw entry count overstates it by an order of magnitude and I made exactly that mistake when
recommending this branch.** Of **12,872 entries, 12,090 name TRANSFORM-ONLY nodes** - `'destroy'`,
`'smoke'`, `'position01'`, `'kid_pos03'`, `'entrance'`, spawn and effect markers that carry no geometry
and have no `ModelEntity`, so hiding them shows nothing. **Only 778 name a real MESH** - but all 778 are
worth having, because **not one of them is also named by a visibility track in the same clip**, so the
hide list is the ONLY thing that would ever put them away. Just **4** entries fall past `nodeCount`.
**635 of the 778 (82%) are on the ADVISOR models** (hallow 192, global 150, jungle 117, space 98,
fantasy 78) - every `Advisorm*` clip hides the same ten costume pieces (`Spatula`, six named hats,
`Hardhat`, `Bowler_BTie`). **I BRIEFLY RECORDED THAT AS "THE PAYOFF". IT IS NOT, AND THE MEASUREMENT
THAT KILLED IT IS WORTH KEEPING:** `AdvisorModel.Dress()` already hides all ten (his real ids come from
the 20-byte table at file `0x7C`, NOT node record `+0x50` - antennae 19/20, hands 21/22, hats 5..14 - so
`FirstOwnPartId = 19` is exactly right), and **no visibility track anywhere ever SHOWS a mesh that a hide
list hides** - the only meshes visibility tracks touch on him are eyes and eyelids, the blink. With the
hats hidden once and visibility never restored, the list changes nothing.
**AND IT CHANGES NOTHING THAT IS PLACED EITHER, MEASURED:** across the eleven objects Lost Kingdom
places there are **218 hide entries, of which 10 name a mesh** - the Belly Bounce's `shell06` and `egg` -
**and `PoseAsBuilt` already resolves both to hidden.** The other 143 mesh entries in the game sit on
`monkey`, `slaser`, `demon`, `hoverbot`, `bug` and friends, **none of which Lost Kingdom places**.
**SO THE HIDE LIST HAS NO VISIBLE PAYOFF TODAY, FOR THE SAME STRUCTURAL REASON POSITION TRACKS HAVE
NONE:** one park ships a save, it places eleven objects, and their visible state is already correct (see
the caveat above - "only loadable park" is Alexah-corrected and this verdict is about TODAY). It becomes
load-bearing the moment COSTUMES exist (the costume code at `0x00598ca0` shows hats by id) or more
things are placeable. `FUN_004726d0` also shows hide state is recomposed across **all twelve channels**
every refresh, a concept this program has no equivalent of.
**THE BELLY BOUNCE GAINS NOTHING, MEASURED:** its 10 mesh entries are `shell06` and `egg`, and
`PoseAsBuilt` already resolves both to hidden at the end-of-build frame 144 (`egg` `[0,2,-94]`,
`shell06` `[0,94,-131]`), with no clip ever showing them again and visibility never restored.
**ENGINE SHAPE, READ MYSELF:** `FUN_00472d70` hides the INCOMING clip's list at clip start, resolving
each entry by the same `idx < meshCount ? mesh : node` rule `ModelFile` uses and setting flag `0x10`
unless `0x80000000`; `FUN_00472310` clears `0x10` over the OUTGOING clip's list, then restores `0x289`
nodes from the master. **The `& 4` id-search arm is a flag on the MODEL record, not on the clip** - the
tracker used to say the clip chose it - and the DEFAULT path is index-addressed, which is all that
shipped data needs.

**>>> AND THEN ALEXAH CHOSE GUESTS, 2026-09-15, AFTER I RETRACTED THE HIDE-LIST RECOMMENDATION. START
HERE. <<<** I recommended the hide list on an entry count, measured it properly, found it buys nothing
visible either, told him so, and he picked **guests** - the plan's step 3, and what P5 was always for.
**WHAT THE SCOUTING ALREADY FOUND, so it is not re-derived:**
- **The park really does contain guests.** The 42-thing census (further down this file) reads
  **ids 29 and 31-41 = twelve guests**, standing at x47-48, y9-15 **on the bus road**, plus the head
  record (a guest with no id), and staff 25 handyman / 26 mechanic / 27 entertainer / 28 guard /
  30 researcher. A guest is **thing model 1**, read by `FUN_004fb530`, **525 bytes on disk** (390 shared
  + 135 own): mCash, mPaidAdmission, mNumRides/Shops/Sideshows, mQueuePos, mPersonType, mQNext/mQPrev,
  mPreviousRides[4], mLastPosX, mLastPosY. **`ParkWorld` reads none of them** - it exposes catalogue
  objects, cells, gates, seed, weather and `ThingCount` only, so the guest records are unread.
- **>>> `FUN_0055abf0` IS NOT THE PEEP SIMULATION - REFUTED 2026-09-16, IT IS THE FLYING CARS. <<<** Its
  own strings say so: `FLY: Failed to trigger a flying car anim (%ld)` at `0x007660e8`, beside
  `FLY: No headnodes on car mesh` and `FLY: Load Fail`. It sweeps **100 groups x 20 members** (stride 0x39
  ints), integrates X and Z off a 4096-entry angle table, drives Y through a climb rate clamped to
  +/-0x200, floors target height at a ground query plus 5000, teleports members **45,000 units off the map
  edge**, and runs a pairwise separation pass - flocking flight, not walking. The four map bounds
  `0x007660b8/bc/c0/c4` are each written exactly once, all in `FUN_0055ab50`, the flying-car setup.
  **The real peep simulation is `0x004f9000-0x00512000`: 281 functions, 95,152 bytes**, plus the queue
  module `0x004dd000-0x004e2000` at 89 functions / 19,684 bytes. Named by its own strings -
  `Poople-flops!  That peep seems to be stuck good'n'proper` (`FUN_0050ed10`), `Peep %d collided with
  Peep %d` (`FUN_005102f0`), `Peep can't SetRandomDest anywhere` (`FUN_004f9490`). Entry is the thing-list
  tick **`FUN_00516380`** (bracketed by its own `GetTickCount` pair in `Game_StateMachine`), which walks
  the list calling `FUN_0050b360` per thing; `FUN_0050ed10` and `FUN_005102f0` have **zero static
  callers**, so peeps are driven by virtual dispatch. **Its gate is a MODE switch, not a frequency
  divider** - read from the listing at `0054f6c3`-`0054f754`: `DAT_00fb3b7c == 0` jumps to the call,
  `== 2` jumps to the same call, and `== 1` falls through to `FUN_005166b0` instead. So it runs **every
  tick** in a normal park (0) and in Instant Action (2), and the ONLINE mode (1) is the one branch that
  does *not* call it. **[[original-boot-sequence]] says "Every 8th: online 0x00516380" and that is WRONG
  on both counts** - corrected there 2026-09-16; the "every 8th" was most likely one of the three
  ONE-SHOT init guards at `0054f691`/`0054f6d8`/`0054f719`, which OR bit 0 into `[0x00fb34a8]` and fire
  once, misread as a divider. **Deferring to that note over my own trace would have introduced the error,
  not avoided it.**
  What IS frequency-gated: `0054f5c0` reloads the tick counter `[0x00877d34]` and `TEST AL,0x1` / `JNZ`
  skips **`FUN_0055abf0` and `FUN_00475360`** on odd ticks, so the sprite step runs **every 2nd 31ms tick
  = 62ms**, which is exactly its own default interval `+0x80 = 0x3e`. Every tick: `Particles_Tick`,
  `FUN_005516b0` (RSSE), and **`FUN_00546c80`, which is the TRACK-RIDE tick, not peeps** - its magic
  `DAT_00877b58 == 0x4a454647` ("GFEJ") is read all over `0x00542000-0x0054b000`, written only by
  `FUN_00544360`, and that region's `FUN_00543560` is the save's **TRAK** module loader. **This line used to price guests-walking at
  "5,712 bytes"; the true figure is ~115,000 across 370 functions, and the plan Alexah agreed rests on
  the wrong one.**
- **>>> GUESTS ARE SPRITES, NOT MODELS. <<<** There is no peep `.md2` anywhere - searching the whole of
  `data/` for peep/guest/person/people returns NOTHING. They live in **`esprites.wad`** as sprite sets:
  `Generic\Kids\SPR_BE/BI/CH/FR/KI/SA/SU/TA` (eight, the guests), `Generic\Kidsheads` (eight),
  plus Guards, Handymen, Mechanics, Researchers, per-theme Entertainers and Costumes, Balloons,
  Litter, Thoughts, SpecialFX. 121 members: **46 `.ESP`, 46 `.TPC`, 29 `.FPC`**.
- **MOST OF THE READER ALREADY EXISTS** (branch 12, for lobby particles): `Formats/Sprite/SpriteBankFile.cs`
  reads the `.ESP` (magic `ESP_FILE2.00`, 256-byte name, two flags at `0x10C`, sixteen sets of
  `First`/`FramesPerDirection`/`Directional`) and `SpritePackFile.cs` reads the `.TPC` (row RLE on a
  BGRA palette). **A guest bank uses 8 of its 16 sets; a Kidsheads bank uses 1.**
- **AND THE ONE NOTE THAT SAID OTHERWISE IS WRONG - measured.** [[lobby-particles]] listed "`.FPC`/TPC v2
  sprite packs" as unfinished. **Every one of the 46 TPC and 29 FPC is version 3**, so `SpritePackFile`
  should already decode them all. An `.FPC` and the `.TPC` beside it share a picture count (Kids: 175
  each) and differ only in bytes. **`.FPC` is read by nothing in the tree**, and which of the pair the
  game loads is chosen by `SpriteBank_Load` (0x00540d90) from the ESP byte at `0x10C` - its selecting
  constants (`0x008757a8`, `0x00875598`, `0x00875fe8`, `0x00876270`) **read back EMPTY in Ghidra, so they
  are runtime-populated and NOT identified. Do not guess them; find the writer.**
**>>> THE PAYOFF WAS MEASURED 2026-09-15 AND THIS ONE IS REAL - 18 SPRITES, AND IT RECONCILES WITH THE
CENSUS EXACTLY. <<<** Unlike position tracks and the hide list, which both measured to nothing visible,
guests put the park's whole population on screen. Every figure below is measured, not inherited.
- **THE SAVE ALREADY CARRIES THE SPRITES.** `FUN_00475730` is not a per-frame step - it is the sprite
  table's LOADER, reading tag `0x53435054` (`TPCS`), a fixed record size **0x118 (280)**, a slot count,
  that many u32 handles, then one record per non-zero handle. The shipped park has that block at
  **0x16d1aa of the inflated payload, immediately after the World trailer `DLRW` at 0x16d1a6**: 100
  slots, of which **18 are live**. Loader has exactly ONE caller (`FUN_00415270`).
- **>>> 18 = 13 kids + one each of the five staff, which is the 42-thing census to the record. <<<**
  The census reads 12 guests (ids 29, 31-41) plus a head guest with no id, plus staff 25 handyman /
  26 mechanic / 27 entertainer / 28 guard / 30 researcher. **13 + 5 = 18.** Two instruments that share
  no code agreeing to the record is the check that makes this trustworthy.
- **The sprite TYPE table is `DAT_00763f88`, stride 12 = [name ptr][first bank][bank count]**, 14 types:
  **0 kids, 1 kidsheads, 2 costumes, 3 costumeheads, 4 entertainers, 5 handymen, 6 mechanics, 7 guards,
  8 researchers, 9 thoughts, 10 balloons, 11 litter, 12 specialfx, 13 particles.** Verified independently:
  the balloon-purchase path in `FUN_004fe1e0` calls `FUN_00541f70(10)`, and 10 is `balloons`.
- **Instance record fields, read from the code that WRITES them** (`FUN_004758f0`, `FUN_00475360`):
  `+0x08` program counter, `+0x10` current instruction, `+0x14` script base, `+0x18` state (4 = free me),
  `+0x7c` due time, `+0x80` interval (0x3e at construction), `+0x84` locals, `+0xa0` alpha, `+0xa4`/`+0xa8`
  scale x/y, **`+0xac` type, `+0xb0` bank, `+0xb4` variant**, `+0xbc` cached frame count. All 18 are
  state 2, alpha 255, scale 1.0/1.0.
- **>>> THE INSTANCE CARRIES NO WORLD POSITION - MEASURED, AND THE NEAR-MISS IS WORTH KEEPING. <<<** A
  scan of every offset for a value shaped like a cell coordinate "found" eight, and **every one was an
  artifact**: `+0xa6`/`+0xaa`/`+0xfe`/`+0x102` all read 63.5 on all 18, and 63.5 x 256 = 0x3F80, the HIGH
  HALF of the float `1.0f` at `+0xa4`/`+0xa8`. Real positions would differ sharply between guests on the
  bus road and a guard at (39,28); these barely differ at all. **Position comes from the THING's mX/mY**
  (256ths of a cell), which `ParkWorld` already walks. New rule in [[verify-every-ordering]].
- **`mPersonType` does NOT pick the bank, and that retires the open question.** `FUN_00541f70(type)` is
  `random % bankCount` and `FUN_00541fd0(type, bank)` is `random % *(u16*)(bank + 0x21E)` - the art is
  drawn at random when the person is made, and the CHOICE is then persisted at `+0xb0`/`+0xb4`. So a
  reload must read the saved bank, never re-roll it, or every guest changes clothes on load.
- **>>> WORLD SPRITES ARE A SCRIPTED VM, AND THE OPCODES ARE CONFIRMED CODE. <<<** `FUN_00475010` fetches
  `*(code**)(scriptBase + pc*4)` and CALLS it, bumping the pc, until the callee is `FUN_005da3c0` (the log
  function used as a terminator), then sets state 4. Disassembled to be sure rather than inferred:
  `LAB_00476940` reads the next inline dword operand through that same pc and stores it to locals `+0x34`;
  `LAB_004763b0` calls `0x00475230` and bumps `+0x78`. Scripts live back to back in ONE dword array at
  `DAT_0074dab8` (`PTR_LAB_0074f480` is 1,650 dwords in). **This is why the person base carries
  `mSpriteScript`.** A standing guest does not need the VM; a walking one does.
- **>>> AND IT RECONCILES A BRANCH-79 LOOSE END. <<<** `FUN_00475360` - the periodic-task scheduler
  (`+0x7c` due, `+0x80` interval) that branch 79 checked and CLEARED as "not the animation sweep" - is the
  SPRITE step: it frees state-4 instances and runs `FUN_00475010` on the rest. It was right to clear it.
- **THE ART IS ALREADY READABLE AND THE DIRECTION COUNT IS FIVE.** Measured by making every consecutive
  set-gap in all 46 `.ESP` banks vote: **169 votes for 5**, the only dissent being 1 from the packs that
  are not directional at all (Balloons, Litter, Particles, Thoughts, SpecialFX), and **29 packs land
  EXACTLY on their own picture count at D=5** with 7 more exact at D=1. A Kids bank is 8 sets over 175
  pictures: **set 0 stand (1 frame/dir), set 1 walk (8), set 2 (8), sets 4/6/11/12 (4), set 14 (2)**.
  **The `*heads` packs use a single set, so no gap can vote - their direction count is UNDETERMINED by
  this method and must not be assumed to be five.**
- **A DEFECT IN OUR OWN SHIPPED DOC COMMENT, to fix when this branch lands.** `SpriteBankFile.cs` calls
  the 16 bytes at ESP `0x14E` "four groups of four flags the loader counts but nothing found uses".
  `FUN_00540c70` reads exactly those, as four groups of (setA-1, setB-1, byteC, byteD) selected by a state
  0..3, and `FUN_00475b80`/`FUN_00475c50` drive it. They are used.
- **THE RENDERER IS THE COST - BUT "NOTHING IN THE TREE HAS IT" WAS TOO STRONG, CORRECTED 2026-09-15.**
  `ScreenParticles` is clip-space (`Position` z = 0, `DisableDepth`), yet `World/Weather/WeatherSprites.cs`
  **IS** a world-space camera-facing quad pool - depth-TESTED with `DisableDepthWrite`, on `weather.shader`'s
  model/view/proj block, collapsing unused quads so buffers never resize. It cannot serve as-is: UVs are
  hardcoded 0..1 (no atlas), the tint is per-DRAW not per-quad, and it is `Additive`. The guest pool is its
  **sibling** - `ScreenParticles`' atlas and per-quad colour over `WeatherSprites`' world quads - not a new path.
- **BOTH OF THESE ARE NOW ANSWERED (2026-09-16) - see the sprite-VM block in the live status above.**
  `FUN_0055abf0` is the flying cars, not the walk/queue simulation; and a sprite's script is **not** named
  by `mSpriteScript` at all - it is the instance's own **`+0x0c`**, written as
  `(scriptPointer - scriptBase) >> 2` by `FUN_004758f0` at construction and **again by `FUN_00475b80`
  whenever the script is switched** (which also writes `+0x08` and resets `+0x1c`, `+0x70`, `+0x74`,
  `+0x78`, so a switch restarts the whole VM state). **I first wrote "never written again anywhere in the
  image" here, and that was WRONG** - a grep for the literal `0x74dab8` missed `FUN_00475b80` entirely,
  because it reads the base from the instance's own `+0x14` rather than from the constant. Third instance
  of rule thirty-six in one session.
**Harnesses:** `~/.cache/tpw-harnesses/guest-sprites.py`, `guest-directions.py`, `save-sprite-scan.py`,
`save-sprite-instances.py`, `sprite-instance-fields.py` - all self-checking, all print EXAMINED/SKIPPED.
**>>> THE DOCS HALF IS DONE AND PUSHED: `111eddf` on `docs/particles` (`33cc38c..111eddf`). <<<** Pushed
2026-09-15 at a fresh yes ("If you need to push anything, you can do so"), one named refspec, no force,
**no PR**. **THAT YES IS SPENT - the next push needs a fresh one** ([[ask-before-github]]). `sprites.md`
gains the five directions and loses the "not seen used" claim about `0x14E`; `saves.md` gains the `TPCS`
block and has three claims corrected that our own `SaveReader.cs` already contradicted. Full detail and
the before/after checks are in [[opentpw-fileformats-docs]]. **Nothing is owed to the docs repo.**
**>>> ALEXAH AGREED THE PLAN 2026-09-15: *"Great work, I agree with your plan."* <<<** So the next branch
is **guests, scoped to standing and facing correctly** - the world-sprite draw path plus reading the
`TPCS` block - and the sprite-script VM is deliberately left for when they need to walk. **NOTHING IS
PUSHED OR COMMITTED IN OpenTPW ITSELF for guests yet: HEAD is `aeab328`, 0 ahead, tree holding only the
four never-commit paths.**
**>>> BRANCH 78 HAS NOW BEEN CONFIRMED ON SCREEN BY ALEXAH, 2026-09-15: "I checked the animations,
everything that currently displays and animates looked fine to me still." <<<** That closes the one check
this project cannot run for itself. The easing curves changed rotation blends on **457 of 1,166 clips**,
the advisor's own among them, and nothing regressed. **Do not re-open whether the easing looks right.**
Branch 79 deliberately changes nothing visible - end-of-frame state is identical by construction - so it
does not need the same look before it goes out.
The old order, kept as the record:
1. **DONE on branch 77 - the fixed items run the scripts they ship.** See the branch-77 block above for what
   it bought (the structure, and the engine's own identity rule) and what it deliberately did not buy (any
   visible motion), with both reasons measured. **What it exposed is the real next step: nothing in this
   program can write a script variable from the world, and there is no park open/close concept at all - so
   nothing can ever write the gate's `VAR_COMMAND`.** That is what would make the gate open *because
   something asked*, and it is the same missing event the lobby island gate is standing in for.
2. **DONE on branch 79 - the advance belongs in the sweep, not in `RideScript.Turn`.** `aeab328`, on
   origin. See the branch-79 block above for the call-graph evidence, the two candidates checked and
   cleared, and the two control runs.
3. **Position tracks are not posed.** Parent-local like rotation keys, and `LobbyModel` composes no
   per-mesh node tree; `AdvisorModel` keeps one and is the pattern to copy.
4. **The two reader defects are still unfixed**: rotation easing curves at track `+0x34` (**457 of 1,166
   clips**) and the clip hide-list at `+0x1a`/`+0x38` (**854 of 1,166**) - the latter now fully decoded,
   see the branch-75 block.
5. **Then the plan's step 3 - guests**, which is what P5 was always for.
**>>> "THERE IS NO `.md2` PAGE ON THE DOCS SITE" WAS WRONG, AND IT WAS WRONG FOR SEVERAL SESSIONS. <<<**
Corrected 2026-09-15 on branch 78. There **is** one - `src/content/docs/formats/models.md`, **645 lines**,
covering the header, node hierarchy, textures, meshes, materials, normals AND the whole animation half. It
is invisible from the branch I kept checking: it lives on **`docs/md2-sgn-and-lobby-scripts`** (`d73a627`),
which is the branch carrying **open upstream PR #1**, and it is NOT on `docs/rsse-instruction-set`
(`16a3076`), which is the branch I had been listing pages from. Listing one branch and concluding a page
does not exist anywhere is the same shape of error as rule eleven.
**AND IT IS NOW STALE IN EXACTLY THE PLACE BRANCH 78 CORRECTS.** Its lines 452-455 still say the `+0x34`
pointer "would be an easing curve, but the records are not a fixed length and a third of them are not
monotonic", quoting the `Advisorm1` bytes - the very claim measured and replaced today.
**NOT BLOCKED - THE PROCEDURE WAS ALREADY SETTLED AND I NEARLY REPEATED THE MISTAKE IT WAS WRITTEN FOR.**
I first held the work back as "needs Alexah's decision". It does not: [[opentpw-fileformats-docs]] records
Alexah settling this on 2026-09-10, in answer to this exact failure - *"The docs repo is local, why can't
you update it?"*, said **after docs were left unwritten because `models.md` only existed on the PR #1
branch**. The rule is that **the open-PR restriction is about PUSHES, not local commits**: when a page
exists only on a PR branch, **write on a new local branch STACKED on it, commit, and raise the stacking
when asking about the push**. So the correction goes on **`docs/md2-easing-curve`**, stacked on `d73a627`.
The withdrawn `md2.md` draft stays at `~/.cache/tpw-harnesses/md2-easing-page-draft.md` as source material;
it is not a page to add, because `models.md` already covers that ground.

**The list below was branch 75's prerequisite list and every one of them is now discharged except the
reader defects - kept as the record of what it took.**
**>>> WHAT BRANCH 75 HAD TO DO FIRST, WRITTEN BEFORE IT STARTED. <<<** Branch 74 gave a model a
channel that knows what is playing; **nothing yet puts that clip on screen**. Branch 75 is what makes the
park move, and every prerequisite is named rather than guessed:
- **`MeshRotator` must be split into clock + `Pose( animation, frame )`** the way `MeshAnimator` already is -
  commit `9e8700a` did exactly that to the animator and is the pattern to copy. Its sampling, its Y/Z
  conjugation, its rest-inverse removal and its descendant carry are all inline in `Update` today, and
  duplicating them is the trap.
- **`LobbyModel` must load roles through `RideAnimations`, not the other way round.** Its own probe tries
  only `{stem}M{n}.md2` from 1, so the Round Fountain, both Security Cameras and the Drinks Shop - **four of
  the eleven placements** - would load no main clip at all, and three of those are the ones meant to move
  for ever. Routing `RideAnimations` through the probe instead drops 114 clips and eleven of the twelve roles.
- **A rest-pose restore exists nowhere in the tree**, and the engine calls one on every role change
  (`FUN_00472310`). Without it the first clip looks right and every later one keeps whatever the previous
  clip left behind on any node it does not itself name.
- **Precedence against `ParkObjects.PoseAsBuilt` must be decided**, or a played clip's visibility track puts
  the Belly Bounce's egg back around the dinosaur - which `ParkItemAppearanceTests` pins from the file and
  therefore cannot catch.
- **Two reader defects are recorded and deliberately NOT fixed here**: rotation keys carry an easing curve
  we ignore (**457 of 1,166 clips**) and the clip hide-list at block `+0x1a`/`+0x38` is never read
  (**854 of 1,166**). See [[park-data-layout]].
**Verify with the SECURITY CAMERA** - a ~15s cycle for ever, no peeps, no ride state - and not with the
first thing placed. After the posing, the plan's **step 3 - guests**, which is what P5 was always for.

**>>> BRANCH 73 IS DONE AND PUSHED: TWO NUMBERS THE ENGINE READS THAT WE READ WRONGLY. <<<**
`alexah/73-the-numbers-the-engine-reads`, two commits over `f43da8a`. **Both were shipped defects found by
the verification pass, not by the work itself** - which is the argument for running that pass before
writing code rather than after.
- **`54da1ef`** - a clip's length is `trunc(frames * 33.33333206176758f)`, the float at `0x006fec08`, and
  **not** `frames * 1000 / 30`. `__ftol` (`0x0067a830`) sets rounding toward zero (`OR AH,0xc`) before its
  `FISTP`, so the product falls to the millisecond below. The two differ wherever the span is a multiple
  of three: **293 of the 1,237 clips** under levels/. The ferry is 19999, not 20000. It reached the
  scripts - `TRIGANIM`/`WAITANIM` take 300 off this and arm a deadline with it - so **four shipped
  assertions moved**.
- **`c81bbde`** - a UV entry is **one vertex and its own run of keys**, not a run of components sliding
  from a start to an end. The engine (`FUN_004745c0`) reads (first key, key count), one frame per key at
  descriptor `+0x0c` and one (u,v) pair per key at `+0x10`, and writes entry `e` to `(e>>2)*0x20 +
  (e&3)*4` - so the entry index IS the vertex index. The old reading coincides **only for two-key
  entries**, which is why it passed every bounds check: a two-key entry packs so its first key index is
  twice its entry index. **4,391 of 29,723 entries are not two-key, in 289 of 670 tracks (43%)**, running
  to 105 keys. On those it put v values into u.
**Built ALONE in throwaway worktrees on `/home` at 127/0 with 81 warning identities**: `54da1ef` with
**264/264**, `c81bbde` with **269/269**, nothing skipped, `save/` byte-identical at `6135d2f4...`.
**Three control runs**: duration reverted (**4**, predicted 4, exactly the four named), middle keys
skipped (**1**, predicted 1), coordinates de-interleaved (**2**, predicted 1). That last miss is **new
rule twenty-three** in [[verify-every-ordering]] - the extra failure was the test's own anti-vacuity
guard firing, which is the guard working rather than a fault.
**The docs commit `0525d7b` goes with it** (`d13e9d0..0525d7b`), correcting a page WE published last
session that stated the 1000/30 formula. Site builds 13 pages in 2.06s.
**>>> BOTH PUSHED 2026-09-15** at Alexah's word ("Push both"), one named refspec each, **no PR** - GitHub
offered `pull/new/alexah/73-the-numbers-the-engine-reads` and it was **not** used. **That yes is spent -
the next push needs a fresh one** ([[ask-before-github]]). The before-check was written as hard assertions
that would have aborted the push had anything moved, and after it: `local == remote` at `c81bbde` and
`0525d7b`, **0 commits ahead**, `main` still `453e779` on origin AND upstream, docs `master` still
`0e8d5d0` on both, **0 `refs/pull/*` on both forks**, and **upstream PR #1 untouched at `d73a627`**. The
branch was **new on origin**, which is why the before-check asserts its absence rather than its tip. **<<<**
**The plan is Alexah's own artifact and is current as of branch 72:**
**https://claude.ai/code/artifact/38c58b2c-66a2-48b0-9fb9-be979930a38f** - **update it with `url`, never
publish a second copy**; its source is kept at `~/.cache/tpw-harnesses/wiring-up-the-rides.html` so it
can be edited without reading the published page back first.

**>>> THE PLAN'S STEP 2 IS DONE: A PLACED THING'S SCRIPT CAN SEE ITS OWN MODEL'S ANIMATIONS. <<<**
`alexah/72-hand-a-ride-script-its-model` (`f43da8a`, **two commits over `1049ad5`**), **PUSHED 2026-09-15**
at Alexah's word ("Push both"), one named refspec, **no PR** - GitHub offered
`pull/new/alexah/72-hand-a-ride-script-its-model` and it was **not** used. **That yes is spent - the next
push needs a fresh one** ([[ask-before-github]]). Verified by `git ls-remote` before AND after, the
before-check written as hard assertions that would have aborted the push had anything moved:
`local == remote` at `f43da8a`, **0 commits ahead**, `main` still `453e779` on origin AND upstream, **0
`refs/pull/*`**, one worktree, tree holding only the four never-commit paths.
- **`e3d412e`** gives `AnimationFile` the frame span a clip **declares** in its own animation block
  (`+0x04`/`+0x08` of the block at file offset `0x98`, read as **integers** - as floats they give denormal
  nonsense). Added **beside** the computed span rather than replacing it, so `MeshAnimator`, `MeshRotator`
  and the advisor's transcript-verified gesture timing all keep the number they were written against.
- **`f43da8a`** adds `RideAnimations` (the twelve roles) and hands every bound script its thing (`+0xac`)
  and those roles (`+0xc8`), so the animation opcodes answer real clip lengths.
- **THE DOCS COMMIT `d13e9d0` WENT WITH IT** on `docs/rsse-instruction-set` (`90e81a7..d13e9d0`), pushed
  2026-09-15 on the same yes, **no PR**, `master` untouched at `0e8d5d0` on origin AND upstream, **0
  `refs/pull/*` on origin**, and **upstream PR #1 untouched at `d73a627`** before AND after. Site builds
  13 pages in 2.07s. See [[opentpw-fileformats-docs]].
- Built **ALONE** in throwaway worktrees on `/home`: `e3d412e` at **127/0 with 249/249**, `f43da8a` at
  **127/0 with 263/263**, nothing skipped, **warning identities unchanged at 81**, `save/` byte-identical.
**>>> IT IS ADDITIVE BY CONSTRUCTION, WHICH IS WHY NO EXISTING TEST MOVED. <<<** With no model the engine
does its arithmetic on nought and the floor catches it at 300 - so every model-less answer the suite
already pinned is the general case evaluated at zero. A model that carries no such role is a **third**
answer again (a flat 1000 before the subtraction, so 700), and eight shipped role references take it.
**THE DECODE WAS ADVERSARIALLY VERIFIED BEFORE ANY CODE WAS WRITTEN**: 8 claims, one skeptic each, plus a
completeness critic - **7 confirmed, 1 refuted**, and the critic found eleven things an implementation
would otherwise get wrong. Full detail in [[park-data-layout]]. **Two decisions would have been silently
wrong without it**: roles must be found by **probing** numbered-then-bare rather than listing the archive
(`mamfount` is the only item in the game that separates the two readings), and clips must be read **raw**
rather than through `AnimationFile.TryLoad`, which rejects **114** of the game's clips - exactly the ones
carrying only positions and visibility, among them the longest animations there are.
**1,085 clips across all 308 scripts, counted twice by independent routes** - probed through the file
system in the test, and read from the archives' own tables in a separate harness.
**Four control runs**, each breaking one thing: 2, 4, 1 and **5** failures. The last was **predicted as 1**
and the four extras are tests older than this branch - independent agreement that `WAITANIM`'s floor is
unsigned, and **new rule twenty-two** in [[verify-every-ordering]] about predictions that name only the
obvious test.
**>>> TWO RENDERING DEFECTS FOUND AND DELIBERATELY NOT FIXED HERE - AND THE "88" IS SUPERSEDED. <<<**
`LobbyModel.LoadAnimations` probes only `{stem}M{n}.md2` from 1, so models shipping a bare `{stem}m.md2`
animate nothing - the Round Fountain among them; and it stops at the first clip `TryLoad` rejects, taking
`ferry` and `lights` in **all four themes** down to zero clips. **The count is 197, not 88**: the 88 came
from an instrument that keyed one item per archive and so could not see any archive holding several base
models - which includes `terrain.wad`, whose `basem.MD2` is the jungle river. See [[park-data-layout]] for
the measurement and for what switching each one on would actually show. **Neither touches the lobby** -
zero bare-only models sit outside `levels/`. Both belong to branch 74, with the channel.

**>>> THE PLAN'S STEP 1 IS DONE: EVERY THING LOST KINGDOM PLACES NOW RUNS ITS OWN SCRIPT. <<<**
`alexah/71-give-a-placed-ride-its-script` (`1049ad5`, one commit over `add0362`) **WAS PUSHED 2026-09-15**
at Alexah's word ("Push branch 71"), one named refspec, **no PR** - GitHub offered
`pull/new/alexah/71-give-a-placed-ride-its-script` and it was **not** used. **That yes is spent - the next
push needs a fresh one** ([[ask-before-github]]). Verified by `git ls-remote` before AND after, the
before-check written as hard assertions that would have aborted the push had anything moved:
`local == remote` at `1049ad5`, **0 commits ahead**, `main` still `453e779` on origin AND upstream, **0
`refs/pull/*`**, 14 `alexah/6*`+`alexah/7*` branches on origin, one worktree.
**THE DOCS COMMIT WENT WITH IT**: `90e81a7` on `docs/rsse-instruction-set` (`91b8980..90e81a7`), pushed
2026-09-15 on the same yes, **no PR**, `master` untouched at `0e8d5d0` on origin AND upstream, **0
`refs/pull/*` on origin**, and **upstream PR #1 untouched at `d73a627`** before AND after. It documents
the twelve animation roles and **corrects a superseded reading**: `TRIGWAITANIM` keeps no cursor, which
that page had said - see [[opentpw-fileformats-docs]]. 13 pages build in 2.08s.
Built **ALONE** in a throwaway worktree on `/home` at **127/0** with **249/249 and nothing skipped**,
`save/` byte-identical at `6135d2f4...`. New `ParkRides` entity; `Level` builds the item catalogue once and
shares it with `ParkObjects`; `RideScript` gains `Directory` (the engine's `+0x38`); two new test classes.
**ALL ELEVEN placed things in the jungle bind a script and not one is scriptless** - and the test reaches
that eleven **twice by separate routes**, opening the files itself where `ParkRides` loads them through
the scheduler, because a binding that skipped a whole folder would still look tidy in its own log.
**The original's own binder is `FUN_004dcf90`**, called from the object constructor at `0x004db517`: it
builds `"%s\\%S%s"` out of the item's directory, its name and `".rse"` (`DAT_00700540`), opens it, hands
it to the loader `FUN_005587f0` and keeps the **id** at the thing's `+0x24`. **A missing script is
ordinary data, not an error**: the original logs "Ride script not located for %s - using placeholder" and
loads `Data\TestScript\test.rse`, **which does not ship**, so its own fallback fails to open too and the
loader simply answers nought. The placeholder is deliberately not copied.
**`Directory` is what makes a spawn unambiguous**: all 48 spawn sites in the corpus name a file in the
asking script's own folder, and 28 ask for `EventMap.rse`, of which one copy ships per item. **Nothing
Lost Kingdom places spawns anything**, so a unit test pins the prefix rather than the park.
**>>> AND A RECORDED CLAIM WAS CORRECTED: THE SPEED WORD IS NOT ALWAYS 50. <<<** Branch 65's scan for
writes to `+0xc0` swept `0x00551000`-`0x0055a000` and found two; **the third is at `0x0055a30d`**, just
past the top of that window - `FUN_0055a300`, whose only caller is the object constructor, pushing the
item's own operating speed into the script it has just bound (and `FUN_0055a0b0` writing variable 3,
`VAR_DURATION`). **Neither is implemented here**, because which `.sam` key feeds them is **NOT**
established - the constructor reads its record through a two-byte pointer, so the printed offsets are
element offsets and they disagree with where `FUN_004db7d0` parses `mOperatingSpeed`. See
[[park-data-layout]] and **new rule twenty-one** in [[verify-every-ordering]].
**>>> STEP 2 IS DONE - SEE THE BLOCK AT THE TOP OF THIS FILE. <<<** What follows is the research that
preceded it, kept because it is the decode the code was built from. It is **not** outstanding work.
**>>> AND IT DID NOT FREE THE 92 SCRIPTS, WHICH THIS PARAGRAPH USED TO IMPLY IT WOULD. <<<** This said the
model was "the one thing the 92 scripts behind `TRIGWAITANIM` and `REPAIREFFECT` are waiting on, 24 of
which need nothing else at all". Handing the model over was **necessary and is not sufficient**: both
opcodes need an animation to actually be *playing*. `TRIGWAITANIM` re-enters and asks the model which
role channel 0 is running, and **nothing here models a channel at all**; `REPAIREFFECT` dereferences the
model handle through a runtime table three levels deep. Both are still counted rather than guessed, and
both now wait on the renderer's half of this work - the two defects noted at the top of the file - rather
than on the binding. **Do not read "step 2 is done" as "the 92 are unblocked".**
**>>> STEP 2'S RESEARCH WAS NOT THE JOB THE PLAN'S WORDING IMPLIED. <<<** Read from the
binary 2026-09-15, full decode in [[park-data-layout]]:
- **It is "hand the script its THING", not its model.** The loader `FUN_005587f0` takes the thing id as its
  **second argument**, stores it at `+0xac`, and then sets `+0xc8` to `thingTable[thing]->+0x20`
  (`0x00558d2e`-`0x00558d68`, table `DAT_007cfb90`, stride 20). **`+0xc8` is never written by the world
  later** - it is seeded once at load. `ParkRides` calls `Spawn(path)` with no such argument, and that is
  exactly the gap.
- **The binder also has a teardown half we do not model**: `FUN_004dd0a0`, the object's own destructor,
  calls the script teardown `FUN_00559060` with the id from `+0x24` and a mode of 0, 4 or 7. Nothing in
  `ParkRides` destroys a script when its thing goes.
- **Animation ids are twelve fixed ROLES, each a file suffix** - `C D I L S M E U W B R O` for ids 0-11,
  from a table at `0x006fe6bc` - **not** indices into the `M<n>` clips, which is refuted 72 wads out of 72.
  **Two roles are already loaded by our own code**: `{stem}c.md2` (id 0, the construction clip
  `ParkObjects.PoseAsBuilt` already reads) and `{stem}M{n}.md2` (id 5, which `LobbyModel.LoadAnimations`
  already reads). Six of the twelve letters were confirmed from shipped data alone, without the binary.
- **Our `MeshAnimator` cannot yet do what a role needs**: it is a looping player with a private `_current`
  that cycles every clip in turn, and there is no way to select one by id and hold it. That, plus the
  id-to-role indirection, is the shape of the work - not "build a models subsystem", which remains wrong.

**>>> EVERYTHING THROUGH BRANCH 71 IS PUSHED. NOTHING IS OUTSTANDING. HEAD IS `1049ad5`. <<<**
`alexah/70-the-sound-a-script-changes` (`add0362`, one commit over `d9b7f8d`) and the docs commit
`91b8980` on `docs/rsse-instruction-set` (`f389359..91b8980`) both went 2026-09-15 at Alexah's word
("Push both"), one named refspec each, **no PR** - GitHub offered
`pull/new/alexah/70-the-sound-a-script-changes` and it was **not** used. **That yes is spent - the next
push needs a fresh one** ([[ask-before-github]]). Verified by `git ls-remote` before AND after, the
before-check written as hard assertions that would have aborted the push had anything moved:
`local == remote` at `add0362` and `91b8980`, **0 commits ahead on both**, branches 61-69 all still
present, **`main` still `453e779` on local, origin AND upstream**, **0 `refs/pull/*` on both forks**,
docs `master` untouched at `0e8d5d0`, and **PR #1 untouched at `d73a627`** (`/merge` `300fa60`).
`add0362` was built **ALONE** in a throwaway worktree on `/home` at **127/0** with **243/243 and
nothing skipped**, `save/` byte-identical at `6135d2f4...`, and the docs site builds 13 pages in 2.02s.

**>>> P5 STEP 10 IS DONE: `DIPMUSIC` AND `SETOBJPARAM`, AND THE LADDER'S LEADER WAS REFUSED AGAIN. <<<**
The corpus stands at **199 of 308 running start to finish**, **272 clean through their first six**, and
**1,270 unimplemented instructions (10.7%)**. `DIPMUSIC` is a **mute, not a partial duck** - the mixer
tests one game-wide setting against nought and drives the music group to zero - and **any** non-nought
value does it, since the test is against zero rather than one. Its per-script half is the marker byte
`+0xb9`, touched by exactly two instructions in the subsystem, and **nothing but the script's death ever
un-mutes**. `SETOBJPARAM`'s first operand is a **tag, not the slot the docs called it**, and a particle
carrying that tag is **walked past** - the record's type decides, not its tag. Four control runs each
broke one thing and failed exactly the guarding test, the type check correctly taking down two.
An existing effect test was **updated rather than deleted**: it asserted `FADEOBJ` and `SETOBJPARAM`
were both counted, and now pins that `FADEOBJ` still is while `SETOBJPARAM` is not.

**>>> EVERYTHING THROUGH BRANCH 69 IS PUSHED. HEAD OF THE PUSHED STACK IS `d9b7f8d`. <<<**
`alexah/69-the-scripts-a-script-reaches` (`d9b7f8d`, one commit over `89c9e2d`) and the docs commit
`f389359` on `docs/rsse-instruction-set` (`dae3328..f389359`) both went 2026-09-15 at Alexah's word
("Push both"), one named refspec each, **no PR** - GitHub offered
`pull/new/alexah/69-the-scripts-a-script-reaches` and it was **not** used; the docs push offered no
compare link, which is **expected** for a branch that already existed. **That yes is spent - the next
push needs a fresh one** ([[ask-before-github]]). Verified by `git ls-remote` before AND after (rule
fifteen), with the before-check written as hard assertions that would have aborted the push had
anything moved: `local == remote` at `d9b7f8d` and `f389359`, **0 commits ahead on both**, branches
61-68 unchanged, **`main` still `453e779` on local, origin AND upstream**, **0 `refs/pull/*` on both
forks**, docs `master` untouched at `0e8d5d0`, and **PR #1 untouched at `d73a627`** (`/merge`
`300fa60`). `d9b7f8d` was built **ALONE** in a throwaway worktree on `/home` at **127/0** with
**234/234 and nothing skipped**, `save/` byte-identical at `6135d2f4...`, and the docs site builds its
13 pages in 2.00s.

**>>> P5 STEP 9 IS DONE: THE SCRIPT-TO-SCRIPT FAMILY, AND IT WAS VERIFIED BEFORE IT WAS BUILT. <<<**
Ten instructions - `SPAWNCHILD`, `SPAWNSOUND`, `REMOVECHILD`, `SETVARINCHILD`, `GETVARINCHILD`,
`SETVARINPARENT`, `GETVARINPARENT`, `GETREMOTEVAR`, `SETREMOTEVAR`, `FINDSCRIPTRAND`. After it the
corpus stood at **191 of 308 running start to finish**, **268 clean through their first six**, and
**1,298 unimplemented instructions (10.9%)** - superseded by branch 70 above. `RideScriptScheduler` became the registry the
engine's one global list is (`Find` newest-first, `NewestFirst`, `Spawn`, `Destroy`), because **every
one of these reaches another script by id and no script ever holds a pointer to another**.
**The decode was adversarially verified BEFORE any code was written** - 12 claims, one skeptic each
prompted to refute, plus a completeness critic: **8 confirmed, 4 partly, 0 refuted**, kept at
`~/.cache/tpw-harnesses/script-to-script-verdicts-2026-09-15.json` with 18 critic gaps. **Two verdicts
changed the code**: teardown is **exactly one level deep** (a grandchild is never killed) where I had
written a recursion, and **`SPAWNSOUND` stores unconditionally** where `SPAWNCHILD` guards, so a failed
load empties the slot. Four control runs each broke one thing and failed exactly the guarding test.
**What would have been got wrong by a plausible reading:** the loader answers an **id, not a pointer**,
from a counter that starts at 1 and never recycles; `GETREMOTEVAR`'s operands are **dest, script,
variable** (the docs said all three were unknown) and it **writes a nought on failure** where
`FINDSCRIPTRAND` **leaves its destination alone**; `FINDSCRIPTRAND` matches on what `NAME` stored and
the loader seeds that field to **-1** so a script that never named itself can never be found (31 do
not); and **the eleven names scripts spawn never match their file's case**, so resolution must be
case-insensitive or every spawn silently answers nought. Full decode in [[park-data-layout]].

**>>> EVERYTHING THROUGH BRANCH 68 IS PUSHED. HEAD OF THE PUSHED STACK IS `89c9e2d`. <<<**
`alexah/68-the-guests-a-shop-holds` (`89c9e2d`, one commit over `eb5ae21`) and the docs commit `dae3328`
both went 2026-09-15 at Alexah's word ("Push both"), one named refspec each, **no PR** - GitHub offered
`pull/new/alexah/68-the-guests-a-shop-holds` and it was **not** used; the docs push offered no compare
link, which is **expected** for a branch that already existed. **That yes is spent - the next push needs
a fresh one** ([[ask-before-github]]). Verified by `git ls-remote` before AND after (rule fifteen):
`local == remote` at `89c9e2d` and `dae3328`, **0 commits ahead on both**, branches 61-67 unchanged,
**`main` still `453e779` on local, origin AND upstream**, **0 `refs/pull/*` on both forks**, docs
`master` untouched at `0e8d5d0`, and **PR #1 untouched at `d73a627`** (`/merge` `300fa60`).
`89c9e2d` was built **ALONE** in a throwaway worktree on `/home` at **127/0** with **214/214 and nothing
skipped**, `save/` byte-identical at `6135d2f4...`, and the docs site builds its 13 pages in 2.0s.
**Everything through branch 67 IS pushed.** Branch 67 and `6d2a1e3` went 2026-09-15 on that now-spent
yes, one named refspec each, **no PR** - GitHub offered `pull/new/alexah/67-the-effects-a-ride-starts`
and it was not used. Verified then by `git ls-remote` (the server's own answer, rule fifteen):
`local == remote` at `eb5ae21` and `6d2a1e3`, **0 commits ahead on both**, branches 61-66 unchanged,
**`main` still `453e779` on local, origin AND upstream**, **0 `refs/pull/*` on both forks**, docs
`master` untouched at `0e8d5d0`, and **PR #1 verified untouched at `d73a627` before AND after**.
(Branch 67 was `9e1ea12` before an amend carrying two comment corrections; that SHA no longer exists,
and the amend happened **before** the push, so no remote ever saw it.)

**>>> P5 STEP 8 IS DONE: THE LIMBO FAMILY. MEASURING FIRST OVERTURNED THE CANDIDATE A THIRD TIME. <<<**
After branch 68 the corpus stands at **182 of 308 scripts running start to finish**, **228 clean through
their first six**, and **1,393 unimplemented instructions left (11.7%)**. The step-8 ladder named
`TRIGWAITANIM` the leader at 11 completions; reading its handler turned that into a **permanent no**,
and the limbo family - which that ladder scored at *zero* single-opcode unlocks - took 17 scripts.

**>>> `TRIGWAITANIM` IS A SETTLED NO, NOT A DEFERRAL - AND THE LADDER THAT CROWNED IT WAS BLIND. <<<**
The handler (`0x552c1a`) triggers exactly as `TRIGANIM` does, marks `+0xbc` with the **animation id plus
one** (the `INC` is what makes 0 mean "not armed"), rewinds four words onto itself and returns
**without** ending the slice; on re-entry it asks the model for channel 0 - `FUN_00473fb0`, which is a
plain accessor at `*(model+0x10) + channel*0x38`, **NOT the "channel cursor" this file used to call it** -
and goes on only when that answer plus one equals the mark. **With no model the query is skipped and the
comparison is made against the RAW THIRD OPERAND**, which nothing can ever change, so it parks the script
for ever unless operand three equals operand one. **Across the 133 shipped uses it never does: 132 differ
outright and the last is a variable.** Implementing it faithfully would have hung **56** scripts rather
than completing 11. It waits on models existing, not on effort - see rule nineteen in
[[verify-every-ordering]].
**>>> SUPERSEDED: THE CEILING WITHOUT MODELS IS 216 OF 308, NOT 252. <<<** The 252 was 308 minus the 56
locked behind `TRIGWAITANIM` alone, and was true when written. `REPAIREFFECT` was then found to be
model-gated too (it dereferences the model handle unguarded and would FAULT), and it locks **70**, of
which 34 also use `TRIGWAITANIM` - so **92 scripts are locked behind the two of them**, and 216 is what
every remaining non-gated opcode could reach. **24 of those 92 need nothing else at all** and would
complete the moment models exist.

**>>> SUPERSEDED AS A NEXT ACTION - AND MIND THE TWO NUMBERINGS. <<<** This heading said "NEXT IS P5
STEP 11", which is the **opcode-by-opcode** numbering from branch 70 and **not** the wiring-up plan's
steps. Steps 1 and 2 of that plan are done (branches 71 and 72) and its step 3 is guests; nothing below
is the current next action. The ladder measurement itself is still true and still worth reading before
anyone proposes another opcode, which is why it is kept:
**>>> THE SINGLE-OPCODE LADDER IS EXHAUSTED. <<<**
Measured 2026-09-15 after branch 70, with `TRIGWAITANIM` and `REPAIREFFECT` both banned. **Nothing left
frees more than two scripts on its own**, and after three rounds nothing frees any:

    TRIGANIMSPEED     4 uses,  4 scripts, unlocks 2   <- the whole of the remaining ladder
    LOOPANIM_CH                           unlocks 1
    SPARK             1 use,   1 script,  unlocks 1
    then NOTHING frees a script alone. Single opcodes cap out at 203 of 308.
    walk family     146 uses, +6  -> 205   <- the ONLY group above zero
    scream 247 uses +0 | head 62 +0 | anim _CH 79 +0 | bounce 46 +0 | light 0 uses
    BUMP 199 uses in 12 scripts, FADEOBJ 113 in 39 - both +0 on their own

**So the world-free phase of P5 is nearly finished**: 199 now, 216 is the ceiling, and the gap is 17
scripts of which the walk family is 6. Read handlers before building, **follow their exits**, and now
also ask what each one dereferences and who guarantees that pointer ([[verify-every-ordering]] rules
nineteen and twenty).

**>>> THE FINDING THAT COST THE MOST TO GET - AND BRANCH 71 HAS NOW CLOSED IT. <<<**
Alexah asked for a reassessment on 2026-09-15 and it turned up something ten branches had hidden:
**nothing in the game constructed the interpreter.** Searching the whole tree outside the test project
for `new RideScript` / `new RideScriptScheduler` / `new RideState` / `new RideEffects` returned **two
constructor declarations and one COMMENTED-OUT line in the dead `RideVM.cs`, and nothing else**.
`Level.SetupParkEntities` built nine park entities - ground, paths, queues, terrain, fixed items,
objects, audio, weather, advisor - and **not one of them was a ride**. So "199 of 308 run" was measured
in a test harness, and **in a running park the number was nought**.
**THAT IS NOW FIXED.** `Level.SetupParkEntities` builds **ten**, the tenth being `ParkRides`, and all
eleven things Lost Kingdom places run a script. This paragraph is kept in the past tense rather than
deleted, because it is why branch 71 exists - and because the blindness it records, **a subsystem
finished, verified, and wired to nothing**, is a thing to go looking for again rather than to assume
was a one-off.
**>>> AND "WE NEED MODELS" IS WRONG - DO NOT GO AND BUILD A MODELS SUBSYSTEM. <<<** `ModelFile`,
`AnimationFile`, `Model`, `ModelEntity` and `MeshAnimator` are all built and shipping, and
`ParkObjects` already places every catalogued item as a real model from `{Directory}/{Stem}.MD2`. A
ride's `.RSE` lives in that same per-item archive - which is exactly how the corpus test finds all 308 -
and `ParkItemCatalogue.Item` already carries the `Directory` and `Stem` to build the path. **What was
missing was a REFERENCE, not a subsystem** - and branch 71 supplied it. What remains is the model handle
at `+0xc8`, which the engine does **not** let the world write later: its loader seeds it once, from the
**thing id** it is handed as its second argument. `ParkRides` passes no thing id yet, and that one
omission is what the 92 scripts behind `TRIGWAITANIM` and `REPAIREFFECT` are still waiting on.
**THE PLAN IS PUBLISHED AS ALEXAH'S OWN ARTIFACT:**
**https://claude.ai/code/artifact/38c58b2c-66a2-48b0-9fb9-be979930a38f** ("Wiring Up the Rides").
**Update it with `url` rather than publishing a second copy** - the same discipline as the review and
lobby plans ([[codebase-review-and-plan]], [[lobby-finishing-plan]]). Its order is: (1) give a placed
ride its script, (2) hand the script its model - up to 92 scripts, 24 of which need nothing else at all,
(3) guests, which is what P5 was always for. Alongside and independent: the advisor's screen-open lines
(**that blocker has expired** - the gate consults no world state and the HUD was built on 57-60, but the
ids are MESSAGE ids so the exact lines cannot be recovered statically - pick by transcript), code-health
phases A and B, and lobby item 4 with phase E riding along.
**>>> ALEXAH APPROVED THE PLAN 2026-09-15: *"I fully agree with and trust your plan."* START AT STEP 1
- GIVE A PLACED RIDE ITS SCRIPT. <<<** He also handed the `RideVM` question back to me and it is now
settled - **`VM/` stays**, see [[ride-vm-may-be-replaced]], and do not re-ask it.
**>>> STEP 1'S RESEARCH IS ALREADY DONE AND IS 700 LINES BELOW - HERE ARE THE THREE RULES. <<<** Found
again while clearing deferrals; the full passage sits near the P5 step-3 research block, above
"Reading the handlers rather than the names was not optional".
- **`ParkItemCatalogue` already reads every ride's `.sam` and already knows each item's `Directory` and
  `Stem`**, so most of the join plumbing exists. `ParkObjects` already places the model from
  `{Directory}/{Stem}.MD2` and keeps the list.
- **A ride's script is `<stem>.RSE` in whatever case the wad spells it - 75 of the 76 ride wads** - and
  the case varies wildly (`B_DRIP`, `Bigapple`, `GoKarts`, `mBUGGY`), so **the lookup must be
  case-insensitive**, the same trap that would break every `SPAWNCHILD`.
- **The one wad without a script is `mystery.wad`, in all four themes, and it is NOT a ride**
  (`Info.Id 100`, `Info.IsChoosable 0`, `WhichUIType 4` "Not to be shown in UI"). So **"this item has no
  script" is legitimate data, not an error to handle** - do not log it as a failure.

**>>> `REPAIREFFECT` WOULD FAULT, NOT PARK - AND THAT IS A THIRD CATEGORY OF UNBUILDABLE. <<<**
Handler `0x005562a6`, one operand, shipped exactly 70x value 1 and 70x value 0. It does **not** block:
no rewind, no `+0x98`, and its only odd exit is `NOP`. Its first call, `FUN_00556de0`, is properly
gated - the spawn sits behind the thing handle `+0xac != 0` and stores its particle at **`+0xcc`**, a
frame field the destructor cleans up - so with no world that call is honestly a no-op. **But both
paths after it are UNGATED**: each does `thingTable[frame+0xc8]` (`0x7a4610`, filled at runtime, and
our model handle is always nought) and hands the result to `FUN_00556a80`, which dereferences it
**three levels deep with no guard**. So it would take an access violation, not idle. The sound calls
beyond it are safe - `FUN_0051bfc0` is `Sound_PlayEffect` and is guarded - but that is not enough.
The value==0 path also needs the script to declare **more than five variables** and reads variable 5
to choose a sample. **It waits on models existing, exactly as `TRIGWAITANIM` does.**

**What makes the other two buildable, both fully decoded:**
- **`DIPMUSIC` (`0x005564c0`)** resolves its operand, stores the low byte at frame **`+0xb9`**, and
  calls `FUN_0051e710`, which is only `DAT_00803ac8 = 1; DAT_00803ad0 = value` - a request flag and a
  level, no world at all. **The frame byte is the lifetime**: `FUN_00558500` (the flat destructor)
  tests `+0xb9` and calls `FUN_0051e710(0)`, so **death un-dips the music**. All 8 shipped uses pass a
  literal 1 - nothing ever dips back down explicitly.
- **`SETOBJPARAM` (`0x005524d9`)** is `<tag> <param> <value>` and walks **`+0xb0`, which is exactly the
  record list `RideEffects` already models**, matching on the record's tag at `+0x18`. Its type
  dispatch has only two cases (byte map `0x5569c4`, table `0x5569b8`): **particle types 1-2 do nothing
  at all**, sound types 3-10 call `FUN_0051bc40(handle, param, value)` and store the answer back into
  the record's handle. **`FUN_0051bc40` returns 0 when the sound system is down** - its two flags
  `DAT_00802bc8`/`DAT_00802bd4` are written only by the sound init and its teardown - so with no world
  the handle is set to nought, and that nought is **the engine's own answer, not an invented one**.
  All 20 shipped uses are in `bus.RSE` - **one copy per theme, so four scripts, not one** - as
  `(tag 1, param 20, value 0)` x12 and `(1, 20, 75)` x8. `DIPMUSIC` is likewise `End.RSE` and
  `Gates.RSE`, one of each per theme, which is eight.
**The adversarial verdicts behind branch 67 are kept** at
`~/.cache/tpw-harnesses/effect-opcode-verdicts-2026-09-15.json` - **11 claims, 3 refuted, plus 13 gaps
from a completeness critic**, all with addresses, so none of it has to be re-derived by re-running five
agents. **Read the critic's 13 before touching the effect opcodes again**: nothing in them invalidates
what shipped, and several confirm it, but four are findings worth having up front.
- **G1: a record is a LIVE BINDING, not spawn-and-forget.** `FUN_005516b0` walks the `+0xb0` list every
  tick for every script, re-reads the model at `+0xc8`, re-resolves the node through the cached index at
  `+0x14`, and pushes the position into the particle/sound system through the handle. That is what glues
  an effect to a moving ride, and it is the shape a world-side adapter has to take.
- **G12: a tag is a GROUP label, not an object id.** 95 of 308 files give the same tag to more than one
  `ADDOBJ` - `AnimCtrl.RSE` uses tag **1 at 24 separate sites** - so "kill every match" is the common
  case rather than an edge case, and a machine killing only the first would be visibly wrong at once.
- **G5: a script's death frees its records, and the trigger is a NEGATIVE PC** (-10000, set by every
  operand-fetch overrun), not `END`. Our `RideEffects` outlives its script instead; harmless while
  nothing consumes the records, and a deliberate difference to close when something does.
- **G11: types 6 and 10 never occur in a shipped `ADDOBJ` at all**, and `EVENT` never uses 7-10, so the
  custom-sound-bank path is entirely unexercised by shipped content. **Tag 0 is legitimate data and must
  never be used as a "no tag" sentinel.**
Two engine defects are deliberately NOT copied: the node cache at `+0x14` is used as an array index
without testing for its own `0xffffffff` failure value (**G3**, an out-of-bounds read), and nothing ever
ages or reaps a record whose effect has ended (**G10**), which the original survives only because a
handle is index-plus-generation and every consumer validates it. There is also **no cap** on the record
count anywhere (**G13**) - do not invent one. Re-run the instrument rather than trusting these:
**`~/.cache/tpw-harnesses/step7weigh.py`** parses the implemented set out of `RideScript.cs`'s own
switch, so it cannot drift from the code it measures, and self-checks against branch 69's measured
**1,298 unimplemented / 191 complete / 268 clean-first-six** (branch 68's were 1,393 / 182 / 228,
branch 67's 1,494 / 165 / 223 and branch 66's 2,900 / 109 / 155). If those three do
not come back, believe nothing it prints - and **update them in the same change that adds opcodes**, or
the check cries wolf every run and stops being one.
**Its docstring now carries rule nineteen's warning above its own output**: what it measures is
COVERAGE, never liveness, so it is blind to any opcode whose faithful behaviour is to block. The name
still says "step7" because the tracker and memory point at it by that name; it is not step-7-specific.
**The corpus it reads is NESTED one directory deep** - `<corpus>/<flattened-wad-name>/<script>.RSE` -
so `Path(...).iterdir()` finds **zero** `.rse` files and silently measures nothing; use `rglob`. And
**basenames are not unique**: the 56 files using `TRIGWAITANIM` collapse to 48 distinct names, so count
paths, never `set(p.name)`. Both of those cost a wrong measurement on 2026-09-15.
**>>> THE BASENAME TRAP BIT TWICE IN ONE DAY, AND HERE IS WHY IT KEEPS HAPPENING. <<<** The warning
above lives beside `step7weigh.py`, which counts correctly - so it is no protection at all when the next
harness is a NEW file, which is exactly what happened on the step-10 measurement. **The reason it bites
so hard is the corpus's shape: the game ships the same script name once per THEME**, so a basename count
collapses by up to four. It reported `DIPMUSIC` in 2 scripts (really **8** - `End.RSE` and `Gates.RSE`
in each of fantasy, hallow, jungle and space), `SETOBJPARAM` in 1 (really **4** - `bus.RSE` per theme),
`FADEOBJ` 34 (really 39), `REPAIREFFECT` 60 (really 70), `BUMP` 5 (really 12), `TOUR` 3 (really 4).
Wrong figures reached this file and [[park-data-layout]] before a recount caught them. **Any new corpus
script must key on the full path**, and any per-opcode "scripts" figure that looks suspiciously like a
multiple of four should be recounted before it is written down.
**`SPAWNSOUND` IS NOT A SOUND OPCODE** - decoded 2026-09-15: it concatenates a prefix at `+0x38` with its
string operand and calls **`FUN_005587f0`, the RSSE loader itself**, storing the handle at `+0x14`. It
**loads another script**. Anything planning around it as "play a sample, sound already exists" is wrong.
**`alexah/66-the-animations-a-ride-waits-on` gives `RideScript` the animation family** - `FLUSHANIM`,
`TRIGANIM`, `WAITANIM`, `LOOPANIM`, `WAIT4ANIM` - P5's **step 6**, done 2026-09-15. Two commits:
`22d7c61` fixes a latent order-dependence in branch 65's own tests, `5c24463` is the opcodes. Both built
**ALONE** in throwaway worktrees on `/home` at **127/0**, 181/181 and **189/189 with NOTHING SKIPPED**,
`save/` unchanged. **PUSHED 2026-09-15** at Alexah's word ("Yes please push to my repos"), one named
refspec, **no PR** - GitHub offered `pull/new/alexah/66-the-animations-a-ride-waits-on` and it was not
used. **That yes is spent - the next push needs a fresh one** ([[ask-before-github]]). Verified by
`git ls-remote` (the server's answer, rule fifteen): `local == remote` at `5c24463`, 0 commits ahead,
branches 61-65 unchanged, **main still `453e779` on local, origin AND upstream**, **0 `refs/pull/*`**,
one worktree, tree holding only the four never-commit paths.
**THE DOCS COMMIT WENT WITH IT**: `3ef61db` on `docs/rsse-instruction-set` (`4eb2789..3ef61db`), same
yes, **no PR**, `master` untouched at `0e8d5d0` and **PR #1 verified untouched at `d73a627`** before AND
after - see [[opentpw-fileformats-docs]].
**What it settled:** unimplemented instructions **3,916 -> 2,900**, scripts running start to finish
**29 -> 109**, scripts clearing their own opening six **39 -> 155**. Every handler tests the model handle
at `+0xc8` first, so the no-model path is the engine's own rather than a stand-in - full decode in
[[park-data-layout]]. **`WAITANIM` costs ONE TURN, not the 300ms it looks like**: an unsigned floor lets
its negative duration through where `TRIGANIM`'s signed one catches it. **I first wrote 300ms, which was
invented**; re-reading the bytes caught it before it shipped. **`TRIGWAITANIM` was deliberately NOT
implemented** - 133 uses but it unlocks no further script (109 either way), and its channel cursor at
`+0xbc` is unread. **>>> THAT ARITHMETIC IS SUPERSEDED BY BRANCH 67, which was true when written and is
not now: with the effect opcodes in, `TRIGWAITANIM` completes 11 and LEADS the remaining candidates.
Implementing opcodes changes what the others are worth - see the step 8 ladder above. <<<** Four control runs, each breaking one thing: 2, 1, 1 and **5** tests - the five
including four coaster tests that predate the branch, which is independent agreement on `WAITANIM`'s sign.
**>>> AND THE SUITE WAS MISLEADING IN TWO WAYS - [[verify-every-ordering]] rule sixteen. <<<** Branch
65's nine clock tests passed only because another class built the logger first; alone, eight threw. And
a run without `OPENTPW_GAME_PATH` silently skipped **76** tests, including every corpus guard. Both
fixed; the suite was **189/189 with 0 skipped** at that point - it is **249/249** now, see the live block.

**>>> EVERYTHING THROUGH BRANCH 65 IS PUSHED. HEAD OF THE PUSHED STACK IS `56ff8ad`. <<<**
**`alexah/65-the-clock-a-script-keeps` (`56ff8ad`) gives `RideScript` its four remaining world-free
instructions - `GETTIME`, `SETTIMER`, `GETTIMER`, `RAND`** - done 2026-09-15. Two paths: `RideScript`
and a new `RideScriptClockTests`. Built **ALONE** in a throwaway worktree on `/home` at **127/0** with
**181/181** (172 plus 9). **PUSHED 2026-09-15** at Alexah's word ("Yes, push both"), one named refspec,
**no PR** - GitHub offered `pull/new/alexah/65-the-clock-a-script-keeps` and it was not used. Verified
after: `local == remote` at `56ff8ad`, 0 commits ahead, branches 61/62/63/64 unchanged, **main still
453e779 on local, origin AND upstream**, **0 `refs/pull/*`**, one worktree, tree holding only the four
never-commit paths. **That yes is spent - the next push needs a fresh one** ([[ask-before-github]]).
**THE DOCS COMMIT WENT WITH IT**: `4eb2789` on `docs/rsse-instruction-set` (`f7be7f7..4eb2789`), same
yes, **no PR**, with **PR #1 verified untouched at `d73a627`** before AND after - see
[[opentpw-fileformats-docs]].
**What it settled, all read from the binary rather than from names:** the engine clock is
**milliseconds** (the chain ends at a source falling back to `timeGetTime`), which *answers* the
question [[park-data-layout]] had flagged "do not claim milliseconds"; `GETTIME` stores that clock
raw and is **not** "how long the ride has been alive" as the docs said; `SETTIMER` is **not**
speed-scaled where `WAIT` is; `GETTIMER` floors at nought; and `RAND` is inclusive of its bound and
takes that bound as a **literal even when tagged a variable**. All three claims that could silently
be wrong were proved by **control runs** - each broke exactly one test of 181, and the named one.
**`WAITABS` was deliberately NOT implemented**: zero uses in all 308 scripts.
**And `WAIT`'s speed scaling is deliberately absent** - the divisor is `0.5 + 0.01 * speed`, and a
scan of the whole RSSE subsystem found **exactly two** writes to the speed word, neither an opcode,
so it is 50 always and the divisor is exactly 1.

**>>> EVERYTHING THROUGH BRANCH 64 IS PUSHED. HEAD OF THE PUSHED STACK IS `d2564c0`. <<<**
**`alexah/64-give-each-script-its-turn` (`d2564c0`) is `RideScriptScheduler`, plus a one-line fix to
`RideScript` - P5's step 4, done 2026-09-15.** Built **ALONE** on `/home` at **127/0** with
**172/172**. **PUSHED 2026-09-15**: Alexah first answered "No, hold it locally", then reconsidered
(*"If it's better for our process to stay consistent, it's fine to push"*) and it went as one named
refspec, **no PR** - a compare link was offered and not used. Verified after: `local == remote`, 0
commits ahead, branches 61/62/63 unchanged, **main still 453e779 on local, origin AND upstream**,
**0 `refs/pull/*`**, one worktree. **That yes is spent - the next push needs a fresh one**
([[ask-before-github]]).
**THE DOCS COMMIT WENT WITH IT**: `f7be7f7` on `docs/rsse-instruction-set`, pushed 2026-09-15 on the
same reconsidered yes, **no PR**, with **PR #1 verified untouched at `d73a627`** before AND after -
see [[opentpw-fileformats-docs]].
**>>> BRANCH 63 (`c425264`) IS PUSHED AND VERIFIED TOO. <<<**
**`alexah/63-the-ride-a-script-drives` (`c425264`) is `RideState`, the ride a script drives** - P5's
step 3, done 2026-09-14. Five paths, built **ALONE** on `/home` at **127/0** with **165/165** (153 plus
8 on the ride itself and 4 driving it through real scripts). Everything through branch 62 is pushed;
**AND SO IS 63**, pushed 2026-09-14 at Alexah's word ("Yes, push branch 63"), one
named refspec, **no PR** - a compare link was offered and not used. Verified after: `local == remote`
(`c425264`), 0 commits ahead, **main still 453e779 on local, origin AND upstream**, **0 `refs/pull/*`**,
branches 60/61/62 unchanged. **That yes is spent - the next push needs a fresh one**
([[ask-before-github]]). Detail in [[park-data-layout]]; ledger in
[[contribution-branch-layout]].
**THE .RSE CORPUS NOW LIVES AT `/home/alex/.cache/tpw-rse-corpus`** - 308 scripts from 262 wads,
re-extracted 2026-09-14 because the previous extraction was in a temp directory that did not survive.
Re-run `rsewalk.py` against it rather than writing another parser; it imports as a module
(`rsewalk.Script( path, bytes )` exposes `.instructions` as `(address, opcode, [(tag, value)])`).
**>>> IT IS NESTED ONE DIRECTORY DEEP** - `<corpus>/<flattened-wad-name>/<script>.RSE` - **so
`Path(...).iterdir()` finds ZERO `.rse` files and silently measures nothing.** Use `rglob`, and assert
the file count is 308 before believing anything downstream. **Basenames are also NOT unique**: the 56
files using `TRIGWAITANIM` collapse to 48 distinct names, so count paths and never `set(p.name)`. Both
cost a wrong measurement on 2026-09-15, and the second disagreed with a correct instrument, which is
what exposed it. **<<<**
**>>> EVERYTHING THROUGH BRANCH 62 IS PUSHED. HEAD OF THE PUSHED STACK IS `81b0652`. <<<**
**`alexah/62-run-a-rides-script` (`81b0652`) is `RideScript`, the interpreter** - P5's step 2, done.
Four paths, built **ALONE** on `/home` at **127/0** with **153/153**. **BRANCHES 61 AND 62 WERE PUSHED
2026-09-14**, 62 at Alexah's word ("Yes, push both"), one named refspec each, **no PR** - a compare
link was offered for each and neither was used. Verified after: `local == remote` for both, 0 commits
ahead, **`main` still 453e779 on local, origin AND upstream**, **0 `refs/pull/*`**. **That yes is
spent - the next push needs a fresh one** ([[ask-before-github]]). **The docs clone is also fully
pushed**, four commits on `docs/rsse-instruction-set` ending at `0c22781`, with PR #1 verified
untouched at `d73a627` - see [[opentpw-fileformats-docs]].
**`alexah/61-read-a-rides-script` (`570c08b`) is `RideScriptFile` - P5's first step, done.** It was
built **ALONE** on `/home` at **127/0** with **149/149** (143 plus 6 new). **BRANCH 61 WAS PUSHED
2026-09-14** at Alexah's word ("Yes, push branch 61"), one named refspec, **no PR** - GitHub offered
`pull/new/alexah/61-read-a-rides-script` and it was not used. Verified after: `local == remote`
(`570c08b`), 0 commits ahead, **`main` still 453e779 on local, origin AND upstream**, **0
`refs/pull/*`**, and branch 60 unchanged at `ccf626c`. **That yes is spent - the next push needs a
fresh one** ([[ask-before-github]]). Detail in the P5 section below and in
[[park-data-layout]]; the ledger entry is in [[contribution-branch-layout]].
The stack on the fork reads **53 `5c1543a` -> 54 `6045e6c`/`75727e3` -> 55 `0f79b5e` -> 56 `1894084` ->
57 `2951323` -> 58 `9cd8b6d` -> 59 `d1148ef` -> 60 `ccf626c`**. **BRANCH 60 WAS PUSHED 2026-09-14** at
Alexah's word ("Yes, push branch 60"), one named refspec, **no PR** - GitHub offered a compare link
(`pull/new/alexah/60-the-park-map`) and it was not used. Verified after: `local == remote`
(`ccf626c`), **`main` still 453e779 on local, origin AND upstream**, **0 `refs/pull/*`**, and 0 commits
ahead of the remote. It was built **ALONE** on `/home` at **127/0** with **143/143** and verified
**9 of 9** in the game first. **That yes is spent - the next push needs a fresh one**
([[ask-before-github]]).
**P1, P2 AND P3 ARE DONE, AND SO IS AS MUCH OF P4 AS CAN BE HONEST** - the park map's terrain half is
built on branch 60; everything else behind those six buttons is simulation. **P5 IS NOW UNDER WAY** -
the simulation itself, guests, staff and rides that operate, which Alexah chose when asked. **Its first
SIX steps are built and pushed** - the `.RSE` reader (61), the interpreter (62), `RideState` (63), the
scheduler (64), the clock and chance opcodes (65) and the animation family (66) - every one of them
additive. **Step 6 was chosen by measurement, and it was NOT what this
file had suggested**: the `BOUNCE` family measured 46 uses across 4 scripts and unlocked nothing, so the
animation family took the step instead. It is what
unblocks the map's overlay, park status, the finance screens, the message bar and the gadget's
balance all at once: every one of those has been refused for the want of it.
**>>> P5's FIRST FIVE STEPS ARE DONE (61 to 65, all pushed). The paragraph below is the BRANCH-61-ERA
plan, kept for its reasoning - do NOT read it as what to do next. <<<** `RideScriptFile` EXISTS
(branch 61, `570c08b`, pushed). It reads
all **308** shipped scripts to the last byte, and the format is in [[park-data-layout]] - **read that
before writing an interpreter**, because the body is a flat dword array and a branch target is an
absolute word index needing no arithmetic. **(SUPERSEDED - this said "THE NEXT STEP IS THE INTERPRETER
CORE", and that core landed on branch 62 as `RideScript`.)** The ~30 opcodes
that touch no world at all - COPY, TEST, CMP, the five branches, JSR/RETURN, the arithmetic, RAND,
WAIT and the timers - are **all implemented as of branch 65**; they were where the corpus spends most
of its words and were fully testable with no
guests, staff or rides in existence. The world-touching opcodes (ADDOBJ, TRIGANIM, EVENT, WALKON) wait
for something to act on. Alexah has given standing permission to abandon the inherited ride VM: read
**[[ride-vm-may-be-replaced]]** first, because "abandon" means the broken runtime and NOT the verified
106-name opcode table. **My judgement, now evidence-backed: the inherited `RideVM` should be replaced
rather than repaired** - its constructor throws, `BranchTo` converts targets that need no conversion,
and `JSR`/`RETURN` push and pop a `Queue<int>`, which returns from the FIRST call rather than the
most recent.
(This line has twice said a branch was unpushed after it had gone out - first 53, then 55. Both were
pushed the same day. Check the branch table under "WHAT TO DO NEXT" against `git ls-remote origin`
before believing any push claim in this file. **Branch 59 very nearly became a third, and the way it
was caught is the actual lesson.** Straight after pushing it I amended the two blocks I remembered
writing, and declared the discipline had held. It had not: a shape sweep for "59 OR d1148ef near
not-pushed/local-only" then found **two more** stale lines - the stack note and the P3 heading - both
written minutes before the push. **After a push, sweep the whole file for the branch name and hash.
Do not amend the blocks you happen to remember and call it done**, and do not write down that a rule
held until something other than your own memory has checked it.)

    alexah/46-a-park-with-weather          8e1f181   a park's weather      PUSHED
    alexah/47-a-rides-name-board           8da19c7   ride name boards      PUSHED
    alexah/48-release-what-a-scene-loaded  37df07e   scene release         PUSHED
    alexah/49-camcorder-mode               af157d8   first-person camera   PUSHED
    alexah/50-a-chord-fires-one-shortcut   7b0abf4   one chord, one action PUSHED
    alexah/51-name-what-a-scene-...        2018166   the residue, closed   PUSHED
    alexah/52-place-a-camera-when-...      cd6894b   no frame at the origin PUSHED
    alexah/53-say-what-the-game-...        5c1543a   README made true      PUSHED
    (this table stops at 53 - for 54 through 58, all pushed, see the branch table further down)

Verified after the push: `local == remote` for all three, `main` still **453e779** on origin *and*
upstream, **no `refs/pull/*` on the fork**, branches 40-45 unmoved, tree clean but for the four untracked
tooling paths. The docs clone's `docs/sign-format-corrections` (`9e506ae`) went too, and **PR #1 was
confirmed untouched at `d73a627`** before and after - see [[opentpw-fileformats-docs]].

**The build baseline is 127/0, not 128** - `Shader.OnRecompile` was declared nullable on branch 48; see
[[contribution-branch-layout]], which also says why the historical "128/0" records must stay as they are.
Tests **143/143 none skipped** as of branch 51, and `OPENTPW_GAME_PATH` **must** be set or many of them
skip while the run still reports green. (The "61 skip" figure was measured against 120 tests and has
**not** been re-measured since; treat it as indicative, not current.) Tree clean apart from the four
untracked local-tooling paths. The docs clone carries `docs/sign-format-corrections` (`9e506ae`),
stacked on PR #1's branch - **pushed 2026-09-14, `local == origin`**, verified again 2026-09-14. (This
line used to say "local only", contradicting the paragraph above it.)

**THE README IS CURRENT AS OF BRANCH 55** (2026-09-14) - corrected twice more since this paragraph was
written, on branch 53 and again on branch 55, which is the one that says a park has an interface. What
follows describes the **branch 53** pass only, and is kept for its evidence:

Its test split was **re-measured both ways**
rather than adjusted - `81 ran / 62 skipped of 143` with no game visible, `143/143` with
`OPENTPW_GAME_PATH` set - replacing a stale "58 ran and 57 did not of 115". The Controls table gained
`C` (stand on the ground in a park), the arrow keys and the wheel; Status gained a camcorder-mode
paragraph; and **two claims it carried are corrected** - it said a park's sky and the full height of
its lightning could not be seen "from the park camera... it is there for the first-person view", which
was written before that view existed.

# >>> WHAT TO DO NEXT, IN ORDER. THE PLAN AS OF 2026-09-14 EVENING. <<<

**A park now has an interface** (branch 54, both commits pushed). That changes what is blocked and what
is not, so this list replaces the old "After those, in rough order" section further down, which is kept
only for the evidence in it.

### P1 - THE ADVISOR IN A PARK. >>> BUILT, VERIFIED AND PUSHED 2026-09-14, branch 56 `1894084`. <<<

**`alexah/56-the-advisor-in-a-park`**, stacked on branch 55 at `0f79b5e`. `Level.SetupParkEntities`
builds him last among a park's entities; `ParkFrontEnd` holds him while a window pauses and hushes him
on teardown; **`source/OpenTPW/UI/Park/ParkLines.cs`** is the content seam beside `FrontEndLines`.

**He says ONE line: response 404, sample 377** - the tutorial line describing the bottom-left control
panel, its buttons and its happiness meter, which is the gadget branch 54 built, and which ends *"if
there are any visitors that is"*. Two roads that look open are NOT, both recorded in
[[park-advisor-from-exe]]: **Welcome is a stub** (responses 399-402 all carry sample 1, an *OpenPark*
line about the park being closed) and **screen-posted lines are unrecoverable** (`FUN_00486b00` takes a
*message* id, resolved through the runtime-filled table at 0x0076e300).

**Verified in the game, 16 checks, harness `~/.cache/tpw-harnesses/advisorshot.py`**: says 377 in a park
with the console naming `sp_377.mp2`; **the lobby says it ZERO times, so the predicate can fail**; the
clip total read from the bank at run time covers the 15.4s a transcript measured weeks ago with another
tool (a stub could not); Escape -> `paused=True`, close -> `paused=False`; duck 0.38 -> 1.00 and
`shown=False` at the end; no console command refused. **Screenshot shows him on screen in a park.**

**THE RESIDUE INVARIANT STILL HOLDS, re-measured not assumed** (a park now builds a second advisor
figure): `park1 == park2` at entities **393**, models **560**, assets **1543**, and
`lobby2 == lobby3` at **260 / 347 / 1127**. A park's entities rise from 367 by **exactly 26** - his 25
mesh entities plus himself. VmRSS still noisy and unexplained (529-911MB), unchanged.

**Build 127/0, tests 143/143 none skipped.**

> **REVIEWED: 6 lenses, 28 agents, 11 findings - and THE WORKFLOW REPORTED `raised: 0`, WHICH WAS A LIE
> OF MY OWN MAKING.** The verify stage was written `parallel([ agent(...), agent(...) ])` and
> **`parallel()` takes thunks**, so every verification threw and the findings were discarded on the way
> out. Recovered from `journal.jsonl`. The full lesson is rule eight in [[verify-every-ordering]] - had
> I believed the summary I would have reported a clean review of work nobody reviewed.
>
> **FOUR FIXED, all of them wrong claims I had written in load-bearing comments:**
> 1. `ParkFrontEnd.OnDelete` said he "goes quietly" - **`Hush()` is the CRYING path** (StopSpeaking,
>    samples 639-641). "Quietly" is a term of art for `StopQuietly`, which only the options screen takes.
> 2. The pause comment said all three windows hold his sample. **The options screen throws the line
>    away** - `OptionsScreen.cs:154` calls `StopQuietly()` before it pauses. All three DO set `Pauses`
>    (GameMenu 59, MessageBox 35, OptionsScreen 165), so the list was right and only the consequence wrong.
> 3. The trigger claimed the constructor "would start talking into the loading screen". **It could not** -
>    `Add` only queues and nothing speaks outside `Advisor.OnUpdate`, which does not run during a load.
> 4. `ParkLines` filed the line under `MessageGroups[1]` as fact. **Response 404 carries message group
>    383 - the no-group value** - so the filing is an inference from what the line says. My own dump
>    disproved my own comment.
>
> **ONE RAISED AND REFUTED, and it was the one I predicted:** `_explained` is set before the line is
> queued, so a line dropped because the advisor is switched off still spends the "once". **That is the
> original's behaviour** - `Advisor_SayResponse` returns 0 with the switch off and the tick writes the
> counters regardless, spending the once on the offer. Two verifiers refuted it independently. **Fixing
> it would have been the departure.**
>
> **ONE CONFIRMED THAT IS ALEXAH'S CALL, NOT MINE - see the owed-decisions list, which already had it.**
> Being paused takes the advisor OFF SCREEN (`OnRenderOverlay` draws only while `!_paused`). **The
> original does not:** I checked it myself in Ghidra rather than trusting the reviewer - the pause
> `FUN_004092a0` calls `Advisor_PauseVoice` and the clock stop and nothing else touching him, and
> `Advisor_KillModel` (0x00429d60) has **exactly three callers** (`Advisor_StopSpeaking` x2,
> `Advisor_StopQuietly`), none of them the pause. Putting it right means changing `Advisor`, which the
> lobby shares and where the same behaviour is a documented choice. Recorded as a known departure in
> `ParkFrontEnd`; **ask before touching it.**

**AMENDED to `1894084`** to carry the four fixes, and **re-verified building ALONE** in a throwaway
worktree afterwards (amending changes what the commit contains): **127/0 with 143/143 none skipped**,
worktree removed, no stray `worktree-*` branch left. Pre-push checks clean: **`main` is 453e779 on
local, origin AND upstream**, **0 `refs/pull/*`** on the fork, and both 55 and 56 have no remote ref.

**BRANCHES 55 AND 56 WERE PUSHED 2026-09-14** at Alexah's word ("Yes, push both"), two named refspecs,
**no PR** - GitHub offered a compare link for both and neither was used. Verified after:
`local == remote` for both (`0f79b5e`, `1894084`), **`main` still 453e779 on local, origin AND
upstream**, and **0 `refs/pull/*`** on the fork. **That yes is spent ([[ask-before-github]]).**

### >>> BRANCH 57 - the advisor stays on screen while paused. PUSHED. <<<

**`alexah/57-hold-the-advisor-on-screen`**, stacked on 56. Alexah chose "keep him on screen, frozen"
when asked, so `Advisor.OnRenderOverlay` no longer tests `!_paused`. **This was the review finding I
deliberately did not fix myself** - it changes `Advisor`, which the lobby shares.

**It is exactly right rather than approximately**: what still removes him is `Dismiss` clearing
`_shown`, which both stops go through, so the options screen (which quietens him as it opens) still
takes him away - reproducing all three of `Advisor_KillModel`'s callers rather than approximating them.
**The old behaviour rested on a false premise**: the class remarks said he is hidden in the lobby "as a
park pauses him", written when a park had no advisor to check it against.

**Commit `2951323`, one file, +16/-3. Build 127/0, tests 143/143 none skipped. Harness now 17/17.**
Verified two independent ways: from the preserved pre/post screenshots (his corner **18.20%**, sky
control **0.00%**) and again in a fresh run by the repaired harness (**18.74%** / **0.00%**). Both match
the measured on-screen-versus-gone difference of 16.91%, and a screenshot shows him standing behind the
dimmed menu, frozen mid-gesture.

> **THE FIRST VERSION OF THAT CHECK WAS WORTHLESS AND SAID SO LOUDLY - rule five recurred the same day I
> wrote it down.** Comparing the menu frame against the mid-line frame scored **99.61% before the fix
> and 85.70% after**, because the menu lays a full-screen dimming backdrop over everything. The repair
> was not a better threshold but **holding the confound constant**: two frames that BOTH have the
> dimming, differing only in whether he is in them, with a control patch that must not move. Full lesson
> in [[verify-every-ordering]].

**`~/.cache/tpw-harnesses/advisorshot.py`** is the harness for all of this - 17 checks, a lobby negative
control that proves the predicate can fail, and a cross-source check of the clip total against the
transcript. Re-run it rather than writing another.

**Built ALONE in a throwaway worktree at `2951323`: 127/0 with 143/143 none skipped**, worktree removed,
no stray `worktree-*` branch left. **PUSHED 2026-09-14** at Alexah's word ("Yes, push branch 57"), one
named refspec, **no PR** - GitHub offered a compare link and it was not used. Verified after:
`local == remote` (`2951323`), **`main` still 453e779 on local, origin AND upstream**, **0
`refs/pull/*`** on the fork. **That yes is spent ([[ask-before-github]]).**

**The stack on the fork now reads 55 `0f79b5e` -> 56 `1894084` -> 57 `2951323`.** Everything through 57
is pushed; **nothing is outstanding.** Next is **P2**, the gadget's three unbuilt clusters.
*(AS OF BRANCH 57 ONLY - this paragraph is a dated record, not a statement about now. Branches 58, 59
and 60 came after it. The block at the top of this file is the only one that describes the present, and
this shape - a confident "nothing is outstanding" buried mid-file - is exactly what made a branch look
unpushed twice before.)*

> **PUT THROWAWAY WORKTREES UNDER `/home`, NEVER IN THE SCRATCHPAD.** `/tmp` here is a **3.2G tmpfs**
> and the session scratchpad lives on it; `/home` has 359G. The first alone-build of branch 56 died with
> **79 errors that were all `MSB3021`/`MSB3491` "No space left on device"** and read exactly like a
> broken commit. `/home/alex/.cache/tpw-tip-alone` works. **Never read a build failure's error COUNT
> without reading the error TEXT.**

> **A TRAP THAT COST A WHOLE BUILD: DO NOT PUT A THROWAWAY WORKTREE IN THE SCRATCHPAD.** The first
> alone-build failed with 79 errors, all `MSB3021`/`MSB3491` **"No space left on device"** - and it read
> exactly like a broken commit until the errors were actually looked at. **`/tmp` here is a 3.2G tmpfs**
> (88% full, 391M free) and the session scratchpad lives on it, while **`/home` has 359G**. A
> `--no-incremental` build of the whole solution needs well over a gigabyte of bin/obj. Put the worktree
> somewhere under `/home` - `/home/alex/.cache/tpw-tip-alone` worked - and never read a build failure's
> error COUNT without reading the error TEXT.

**Nothing blocked it any more.** [[park-advisor-from-exe]] has the mechanism in full: a THING of type
0x0b on weather's own tick, message-driven with eight scored slots, `Advisor.sam` parsed by the
existing `SettingsFile`, and `AdvisorResponseTable` at 0x00768fb8 dumping statically to 512 rows. The
acceptance gate `FUN_0059abc0` consults **no world state at all** - only cooldowns, repeat limits, slot
caps and a rotation counter - so the class of lines that needed a HUD to exist is now open, and his
speech is already scene-independent.

**Why first:** visible *and* audible, so Alexah can check it the way he has checked everything else;
it closes a real asymmetry (the lobby has him, a park does not); and it is the largest thing that needs
no simulation.

> **THE TRAP, WRITTEN DOWN ALREADY: the message-to-sample rule I derived held for eight consecutive
> messages and then drifted by +5. Re-derive it; do not reuse it.**

Related: [[advisor-on-screen]], [[advisor-interruption]], [[identifying-speech-by-transcription]],
[[verifying-audio-by-capture]].

### P2 - >>> THE KEYS-AND-TICKETS CLUSTER IS BUILT, branch 58 `9cd8b6d`. The other two are REFUSED. <<<

**`alexah/58-the-gadgets-keys-and-tickets`**, stacked on 57. Built ALONE at **127/0** with **143/143**.
Three files, +120: `ParkGadget` gains control **0x33** with two rows - ticket `0x35`/count `0x37` above,
key `0x34`/count `0x36` below - `ParkFrontEnd` preloads `gkey`/`gtick`, and `Player.Tickets` is added
beside `Player.Keys` out of `PlayerFile.CountTickets`, which was already public.

**IT NEEDED NOTHING INVENTED** - both counts already live in gms.dat. Look and format come from
`FUN_004a1d70`: white labels, **font slot 2**, seeded `"0 x"` (the string at 0x00752f24), `0x34` at
**frame 3** (the four `gkey0-3.wct`), and the **key row hidden outright in Instant Action**. All three
match what `IslandPanel` already does with the same model from a different direction - two independent
sources agreeing.

**The anchoring is load-bearing:** the cluster is added to the window root, NOT the gadget body, because
`UiControl.Add` only lets a child follow its parent's anchor when the parent's rect *contains* it. At
(1688,48)-(1963,278) it resolves its own - centre 1825 across, 163 down - and pins **top-right**.

**THE OTHER TWO ARE DELIBERATELY NOT BUILT, for the fold-away arm's reason.** The aerial (`0x2d`/`0x2e`)
is the *messages* control (help 476) with no message bar behind it - it belongs with **P3**. The bank
balance (`0x2f`-`0x32`) has no artwork but a currency icon, and a park has no balance and no price:
`0x30` is the cost of the item in your hand, which is why the original paints it yellow and starts it
hidden. Either would be a control with nothing behind it.

**Two corrections made while reading, both to my own claims:** help rows **463/464/465 are bound by NO
layout stream anywhere in the image** - I read an adjacency in the string table as a binding - so the
balance naming rests on *use* (`FUN_005234d0` compares it against an item's cost to refuse a build), not
on labels. And **op 0x10 is ten bytes, not four**; a static walker **must track op 5**, which is why the
gauge mesh binds to `0x1e`. Full opcode table in [[park-hud-from-exe]].

**Verified in the game**: screenshot shows both rows top-right reading `0 x` (no player picked via the
console - the documented null path), and the advisor harness still passes every P1 check with no
regression.

**PUSHED 2026-09-14** at Alexah's word ("Yes, push branch 58"), one named refspec, **no PR** - GitHub
offered a compare link and it was not used. Verified after: `local == remote` (`9cd8b6d`), **`main` still
453e779 on local, origin AND upstream**, **0 `refs/pull/*`** on the fork. **That yes is spent
([[ask-before-github]]).**

> **THE WHOLE STACK IS ON THE FORK AND NOTHING IS OUTSTANDING (as of branch 60):**
> 55 `0f79b5e` -> 56 `1894084` -> 57 `2951323` -> 58 `9cd8b6d` -> **59 `d1148ef`** ->
> **60 `ccf626c`, both PUSHED 2026-09-14**.
> **P3 is done** (branch 59, the arm) **and P4's honest half is done** (branch 60, the park map). The
> aerial stays unbuilt, and now for a *proven* reason: see the P3 block below.

### P2 (original plan) - Finish the gadget's three unbuilt clusters

Branch 54 built only `0x1d`'s subtree. The stream at 0x00752940 has **three more top-level controls**,
all dumped and attributed in [[park-hud-from-exe]]:

- **`0x2d`/`0x2e` - the aerial** (`aerial`, `aerialtop`), help **476 "Right-click to delete ALL
  messages"**. So the aerial is the *messages* control, which is why it pairs with P3.
- **`0x2f`/`0x30`/`0x31`/`0x32` - a numeric readout, top left** (`cashtrend`, `i_dollar`). Driven by
  `FUN_004a0ab0`: a value that may be negative, banded 0-5 by thousands into a colour from the table at
  0x00752e30, with a sign frame on `0x32`. **It reads like money but that is not established.**

  > **THE REASON THIS NOTE USED TO GIVE FOR NOT NAMING IT IS DEAD (traced 2026-09-14).** It said "the
  > source is `FUN_006ad810`, which is also the gate inside `FUN_0048c8d0`". **`FUN_006ad810` is a
  > one-line `__fastcall` getter - `return *(int *)(this + 0xc)` - called on about 25 different
  > receivers.** The readout does `MOV ECX,[0x007cf6ec]; MOV ECX,[ECX]; CALL FUN_005195d0; MOV ECX,EAX`
  > (a THING), while the gate does `MOV ECX,[0x007c2534]` and reads `+0xc` of **that** object. They share
  > a getter, not a value, so the coincidence meant nothing. Ghidra hides the receiver because it is
  > `__fastcall` - the same mangling recorded in [[park-advisor-from-exe]] for `FUN_005194d0`.
  >
  > **What to trace instead:** `+0xc` of the THING behind `[0x007cf6ec]`. The corroboration is already
  > strong - signed, banded red-to-white by thousands, an `i_dollar` sign frame, and `b_money` beside it -
  > so this is close to settled, but name it from the object, not from the shape of the display.

  **The colour bands, read out of the image** (6 x 4 bytes RGBA at 0x00752e30): 0 red (255,0,0),
  1..1000 (250,90,0), 1001..2000 (255,175,0), 2001..3000 (255,200,0), 3001..4000 (255,255,150),
  4001+ white. `DAT_00752e2c` caches the last band (999 = none yet) and `DAT_007cb2dc` the last value.

  **`0x30` is a SECOND readout, not part of the first** - the decode table omitted it. Its value is the
  plain global `DAT_0081af4c` (`FUN_0052fbc0` just returns it), and `FUN_004a0ab0`'s second half shows or
  hides `0x30` with `UI_SetVisible`: **-9999 hides it, -999 means leave it alone.**

  > **>>> WHAT UIHELPTEXT SAYS, AND THE MISTAKE I MADE WITH IT (2026-09-14). <<<** These three rows exist
  > and are obviously about these three things:
  >
  >     463  Number of golden tickets you currently have
  >     464  Number of golden keys you own
  >     465  Your bank balance
  >
  > **I wrote that they bind to `gtick`, `gkey` and the numeric readout. THE STREAM SAYS THEY DO NOT.**
  > Scanning 0x00752940 for help-row constants, the ONLY rows the gadget binds are **477** (gauge), **478**
  > (date), **481** (the arm), **494** (research/Instant Action) and **476** (delete all messages).
  > **463, 464 and 465 appear nowhere in it.** I read an adjacency in the string table as a binding -
  > which is the `gauge.md2` error of branch 54 exactly, made again one paragraph after warning about it.
  >
  > **So the balance naming is PLAUSIBLE BUT STILL UNPROVEN from the help table.** What IS proven is the
  > mechanism below - the affordability test in `FUN_005234d0` compares this value against an item's cost,
  > which is far better evidence than a help row anyway. Find where 463-465 are really bound before
  > claiming them.
  >
  > **`DAT_0081af4c` is THE PRICE OF THE ITEM BEING PLACED.** `FUN_005234d0` does
  > `FUN_0052fb90( item->+0x1b8 )` then `balance = FUN_006ad810(); cost = FUN_0052fbc0(); if (balance <
  > cost) refuse`. The branches that stop placing call `FUN_0052fb90( 0xffffd8f1 )` = **-9999**, which is
  > exactly the hide sentinel. **So `0x30` shows what the thing in your hand costs, and vanishes when your
  > hand is empty.** The top-left cluster is *balance + price*, not one readout.
  >
  > **And the gate is just "is the game menu open".** `DAT_007c2534` is written by `GameMenu_BuildPark`
  > and `GameMenu_BuildLobby`, so `FUN_0048c8d0` reads `+0xc` of the **menu object** - nothing to do with
  > the balance. `DAT_007cf6ec` is written once, `= 0x7cf83c`, the common THING table, so the balance is
  > `+0xc` of that THING.
  >
  > **Still to confirm before building: which help row the STREAM binds to which control id.** Do not
  > assume 463/464/465 land where they look like they should - attributes bind to the *parent* after an
  > `op 5`, which is how `gauge.md2` was misattributed on branch 54. See [[park-hud-from-exe]].
- **`0x33`..`0x37` - top right** (`gkey`, `gtick`). Golden key and golden ticket, most likely; unproven.

**Cheap and fully specified**, but some of it wants numbers a park does not keep yet - so build the
chrome and leave the values honestly blank rather than inventing them, as the gauge already does.

### P3 - >>> DONE AND PUSHED, branch 59 `d1148ef`. AND IT WAS NOT WHAT THIS PLAN SAID. <<<

**`alexah/59-the-gadgets-arm`**, stacked on 58. The plan called P3 "the message bar, and the arm that
uncovers it". **Both halves of that were wrong**, and the binary said so before a line was written.

**THE ARM IS A CARRIER, NOT A LID.** `FUN_004a2590` is the only way anything reaches it, and each of
its **five** callers hands it a panel of its own. So it is how the gadget shows *any* sub-panel. The
one whose contents exist today is the **camcorder panel** (stream 0x00751720): `FUN_004a0840` case
0x27 calls `FUN_00498bb0` on the button going down and `FUN_00498bd0` on it coming up, and either of
the panel's buttons closes the arm behind it (`FUN_00498ad0` -> `FUN_004a25f0(1)`, that argument being
what lifts the camcorder button).

**So the camcorder button no longer enters first person itself** - and the game says so in its own
words: **UIHELPTEXT 474**, the button's own row, is *"Left-click to use camcorder mode or send a
postcard"*. Two choices; id 99 lives on the carried panel. One more click than before, and the
original's. The C key still goes straight there.

**THE MESSAGE BAR CAN NEVER BE FED, so it is deliberately NOT built.** Every message arrives through
`FUN_00481580`, whose **one** caller is `CTagSystem::ReceiveMessage` (`FUN_00509bf0`), and the only
branch of that whose contents are readable is **postcard status** - sent / to outbox / failed, ids
0x17b-0x17d - which is out of scope. The other two take an event id from a simulation that does not
exist. Cap is **ten** messages (`DAT_00750610`). **The "pairs with P1" note was also wrong**:
`FUN_00486b00` runs into the *advisor* message system (`FUN_0059b590`/`FUN_0059a940`), which is a
different thing from the bar's own list (`DAT_007cb2c8`). They talk - showing a bar message posts
advisor line 0x11c - but advisor lines do **not** land in the bar. Full decode in
[[park-hud-from-exe]].

**Verified in the game, 9 of 9, `~/.cache/tpw-harnesses/armshot.py`** - a new harness because
`gadgetshot.py` proves the gadget *draws* and this needed a different predicate. Re-run it rather than
writing another. Build **127/0**, tests **143/143**, built ALONE on `/home`.

> **THE DRAW ORDER WAS WRONG AND ONLY A SCREENSHOT COULD SAY SO.** The first build made the arm a child
> of the body and it drew a **hard seam straight across the panel**. The depths are legible only in the
> disassembly - `FUN_0065f16b` is `__fastcall` and Ghidra hides the receiver, the `FUN_006ad810` trap
> again - and they read **arm 4, end 4, b_retract 7, body 8, handle 9, six buttons 10**. Higher is
> nearer the front (the buttons sit at 10 inside a body at 8 and draw over it), so the arm belongs
> BEHIND the body. Nothing here can draw a child behind its parent, so the window root became a bare
> container with the arm added first. Every anchor survived, because a whole-screen root already gives
> `_followsParent = false`.
>
> **THREE OF MY OWN CLAIMS CORRECTED:** `b_retract.md2`'s six nodes are `b_retract, disable, hilite,
> hidown, helddown, down` - UiButton's own order - so **part 0 is the NORMAL look** and branch 54's
> "it showed the disabled part" was wrong; a close button simply looks like a cross. The arm's handler
> is on **0x21**, not 0x23 (`MOV ECX,ESI` at 0x004a2387; 0x23 is only remembered in `DAT_007cb2d0`).
> And **op 4 sub-op 4 is a hit-test region, not a motion path** - `handle`'s 16 points and the body's
> 23 are click masks, and reading them as a path would have animated the arm along nothing.
>
> **NEARLY A FABRICATED FINDING:** I read my own screenshot as showing a blue panel interior where the
> original's is green, and was about to record a pre-existing `mainpanel` texture fault in branch 54's
> work. **The interior is green in both frames.** I had misread my own picture. Look twice before
> writing down a discrepancy in someone else's finished work - including my own.

**Not reproduced, and marked in the source:** the original **slides** the arm, retracting it to a
30-wide sliver behind the body - `FUN_004a1d70` sets `right += left(0x21) - left(0x23)`, so
`1006 + 331 - 976 = 361`. The travel and its timing are not traced, so the arm is here or it is not,
rather than given an invented motion. `LAB_004a14f0` is the state machine (messages 0xa-0x100, state
in `+0x134`, states 3 and 4 meaning "still moving").

### P4 - The first real screen behind a gadget button

**Five of the six buttons say why nothing happens** - the sixth, the camcorder, opens the arm's panel
since branch 59. The most self-contained to make real is the **map** (`b_map` -> `FUN_005f0b40`,
stream 0x00774da0, handler `LAB_005f0b00`).

> **>>> THE OPEN QUESTION IS SETTLED, 2026-09-14: IT IS BOTH, NOT EITHER. <<<** This note used to ask
> "whether the original draws that file or renders live" and say it was not established. It does
> **both**: it loads the park's own **512x512 `2dmap.tga` as the base image** and paints a **live
> 128x128 classification grid** over it. Full working in [[park-hud-from-exe]] under "the park map".
> The arithmetic closes: the loaded image's width and height go to `FUN_005777b0`, which rounds up to
> 64 - `(512+63)>>6 = 8`, so **8x8 = 64 tiles of 64x64**, exactly the shipped TGA.
>
> **So the map is a HEAT MAP, not a picture.** Each cell gets a code 1-12; codes 9-12 are terrain and
> take fixed descriptors, codes 1-8 are things and are coloured by `FUN_005f09a0`, which is a
> **three-stop colour ramp** over a per-thing metric clamped 0-100. The six toggle buttons and two
> radio groups the screen builds are **layer filters and metric pickers** - each code only paints if
> its bit in `+0x18` (or the radio in `+0x1c`/`+0x20`) says so. It also outlines each footprint by
> comparing a cell with its four neighbours.
>
> **What this means for scope: it is NOT small.** It needs the base image drawn, a cell classifier over
> terrain and things, a colour ramp, six filters, two metric modes, footprint outlining and a selection
> highlight - and the metrics themselves are simulation values that do not exist yet. **The heat-map
> half is P5 work wearing a UI**, which is the very thing this plan says to avoid. A first cut could be
> the base image plus terrain codes 9-12 only, with the thing layers honestly absent.
>
> Also traced: the screen **pauses the game** (`FUN_004092a0`, `g_ParkRunning = 0`) and calls
> `Advisor_StopQuietly(1)` - the quiet path, not the crying one - so unlike the gadget it is a window
> that legitimately sets `Pauses`. It posts advisor line **0x130** as it opens.

> **>>> BUILT ANYWAY, THE HONEST HALF: branch 60 `ccf626c`, the park map. <<<** Alexah chose "map,
> terrain half only" when asked. What shipped is the screen, the park's own `2dmap.tga` in the
> viewport, working zoom and scroll, the panel backings and `b_okay` - pausing the park and quietening
> the advisor as the original does. The overlay below is refused, and so is the terrain colouring: the
> original's land descriptors live in runtime-filled globals, and the shipped picture already shows the
> terrain, so painting cells in colours chosen here would be decoration. Full decode of the stream,
> the layer taxonomy and the cell-to-pixel mapping are in [[park-hud-from-exe]].
>
> **>>> THE FALLBACK IS ALSO BLOCKED (traced 2026-09-14). BOTH P4 CANDIDATES NEED THE SIMULATION. <<<**
> This note used to say "alternative if the map proves deep: **park status** (`FUN_004a52a0`, stream
> 0x007537d0)". The map did prove deep - and park status is no better. **Its whole content is meters
> fed by simulation queries**: `0x4a68cc` and `0x4a68ca` from `FUN_004c9130()` and `FUN_004c94c0()`,
> and two more - `sad_undec.wct` on `0x4a68d0` and `hap.wct` on `0x4a68cf` - from `FUN_004c9750(0x21)`
> and `FUN_004c9670(0x42)`, scaled `(x<<10)/100` and `((100-x)*0x400)/100`, which are **visitor
> happiness percentages**. Everything else on it is font-6 black labels captioning those meters
> (string ids 0x97-0x9e, 0x16a-0x16c), five toggles restored from `DAT_00753c48` and a three-way radio
> from `DAT_007cb39c`. It posts advisor line **199** as it opens and sets the remembered tab with
> `FUN_004a0810(2,3)`. **With no simulation every meter reads zero and every label captions nothing.**
>
> **So P4 as written has no unblocked candidate left.** What IS honest today is the map's *terrain*
> half alone - the base `2dmap.tga` plus cell codes 9-12 - with the thing layers, the colour ramp and
> the metric pickers absent. Avoid the four finance screens and `hire` for the same reason, only more
> so; those were always P5 work wearing a UI.

### P5 - The simulation: guests, staff, rides that operate >>> NEXT. START HERE. <<<

The whole of what "not playable yet" means. Needs the **`.RSE` ride scripts** and the game clock
(built, branch 45). This is the big one and everything above is small beside it.

> **>>> STEP 1 IS DONE: `RideScriptFile` IS BUILT AND PUSHED.** Branch 61, `570c08b`, in
> `source/OpenTPW.Files/Formats/Script/` beside the `Opcode.cs` that moved there with it.
> It reads all **308** shipped scripts; 6 new tests, 149/149, built ALONE at 127/0. The whole format is
> in [[park-data-layout]] and the harness is `~/.cache/tpw-harnesses/rsewalk.py`. **<<<**
>
> **>>> STEP 2 IS DONE: `RideScript` RUNS THE SHIPPED SCRIPTS.** Branch 62, `81b0652`, **pushed**,
> in `source/OpenTPW/VM/RideScript.cs`. **All 308 run 40 turns without stopping**;
> Coaster1 names itself and settles in its main loop; a discovered script holds its `WAIT` and resumes
> when the clock passes. 4 new tests, 153/153, built ALONE at 127/0. The semantics are all in
> [[park-data-layout]]. **`RideVM` was left untouched** - removing it is a separate question to put to
> Alexah, and [[ride-vm-may-be-replaced]] records why the 27 handlers are not the verified part. **<<<**
>
> **>>> STEP 3 IS DONE (branch 63, `c425264`) - THE RIDE STATE A SCRIPT TALKS TO (the `COAST` family). <<<** The machine
> currently has **no consumer** and every world-touching opcode is a counted no-op. The interface
> between a script and its ride is `COAST <op> <arg>`, and `VM/Includes/ScriptDefs.cs` already names
> the ops from the original's headers: `COAST_ADDPEEP` 1, `COAST_GETQUEUE` 2, `COAST_GETPEEP` 3,
> `COAST_SETBROKE` 4, `COAST_SETCLOSED` 5, `COAST_SETCAPACITY` 6, `COAST_SETWORN` 7,
> `COAST_INITIALISE` 8. Coaster1's whole loop is those calls plus `VAR_LETMEON`/`VAR_LETMEOFF`/
> `VAR_ONRIDE`, so a small ride object holding capacity, wear, broken, closed and a queue would let a
> real script actually *do* something and would be testable with no rendering at all.
>
> **>>> THE RESEARCH FOR STEP 3 IS DONE, AND IT RE-SCOPES THE WORK. IT IS ALL IN [[park-data-layout]].
> <<<** All eight `COAST` ops are read from the binary, none inferred. **`COAST` itself is small - what
> it calls into is not.** The ride object has **two ring buffers**, a **state machine** behind
> `SETCLOSED`, and a **capacity clamped by two independent limits**. So step 3 is a subsystem, not
> eight opcodes.
>
> **>>> THE SCOPE DECISION IS MADE - DO NOT RE-ASK IT. <<<** 2026-09-14, by me, under
> [[modular-data-driven-engine]]'s "no speculative layers": **build the smallest ride object the
> shipped scripts actually need**, not a faithful model of the original's structure. The twelve
> coaster scripts only ever ask for seven things - **add a guest, take a guest, how much room is left,
> open, close, set capacity, and mark broken** - so that is the whole object. The original's fuller
> shape (two ring buffers, the state machine behind `SETCLOSED`, the doubly-clamped capacity) is
> written down in [[park-data-layout]] for when something actually demands it. **Alexah was told this
> was my call rather than asked to make it, and may overrule it** - if he has not said otherwise,
> build the minimal version.
>
> **>>> STEP 4 IS DONE (branch 64, `d2564c0`, pushed): THE SCHEDULER. <<<** Researched and built
> 2026-09-15, out of
> `FUN_005516b0`. Nothing gives scripts turns yet: `RideScript.Turn` exists and `TicksBetweenTurns = 8`
> is written down, but the caller decides when to call it and nothing does. The whole of the original's
> rule is **`turbo != 0 || ((id ^ tick) & 7) == 0`** against one global tick counter, with the id being
> the script's own field 2. Field map and the rest in [[park-data-layout]]. It is small, it needs no
> world, and it is testable by running all 308 and counting turns per script.
>
> **>>> AND IT FOUND A REAL BUG IN BRANCH 62's `RideScript` - FIXED ON BRANCH 64. <<<** The engine
> resets its critical flag (`DAT_0087919c = 0`) at the **start of every turn**; ours only clears
> `_critical` on `CRIT_UNLOCK`. A script that runs `CRIT_LOCK` and then ends its turn without
> unlocking leaves our budget permanently un-decremented, so the next `Turn` **spins forever**.
> **No shipped script reaches it** - all 308 run 40 turns - so no test caught it and no test could
> have; only reading the loop did. One line in `Turn` fixes it, and it wants a test that constructs
> the case deliberately rather than hoping the corpus contains it. **That was done**: the test builds
> its own .RSE file, and a **control run** with the reset commented out failed **exactly one** test of
> 172 - at word 5 and stopped, where the fixed machine is at word 3 and still running - so the test
> discriminates and the fix changes one outcome and no others.
>
> **>>> STEP 5 WAS PLANNED AS "A RIDE THAT LOADS AND RUNS ITSELF" AND THAT PLAN WAS REJECTED ON
> EVIDENCE, 2026-09-15. Do not simply pick it back up - read why first. <<<** The plan was to join the
> four pieces against real game data: read a ride's `.wad`, build the state, register the script, tick
> it. Four measurements killed it as a *milestone*, though every one of them is worth having:
>
> - **Only ONE park SAVE ships in the whole game** - `jungle/Easymode.TPWI`, and it is an **Instant
>   Action** save, not Full Simulation. Fantasy, hallow and space ship no save file.
>   **THIS LINE USED TO END "so Lost Kingdom is the only park that can be loaded" AND ALEXAH CORRECTED
>   THAT ON 2026-09-15:** all four parks exist, and he expects each to be loadable in an empty/new state
>   without a save - flagged by him as an assumption, so treat it as UNVERIFIED and test it rather than
>   quoting it. The distinction matters because "only one park loads" was used as the backbone of two
>   separate "no visible payoff" verdicts (position tracks, the hide list).
>   **>>> ALEXAH RE-STATED THIS 2026-09-16, AND THE CODE HALF IS NOW VERIFIED. <<<** Their words:
>   *"Lost Kingdom should NOT be the only playable park. All four parks should be playable, that's just
>   the only one that has an easy mode saved file existing. Which shouldn't change anything."* **The code
>   already does this** - `Level.ReadPark` (`Level.cs:309-317`) tests `FileSystem.FileExists` first and,
>   with no save, logs *"no park file ships with this theme, so nothing is placed in it"* and returns
>   null rather than throwing; its own doc comment says the other three "get their ground and their gate
>   and nothing else - which is a fact about the game's data, not a failure". `IslandPanel.EnterPark`
>   passes `island.ThemeName` to `Game.RequestParkLoad`, so any island can be asked for; the
>   Lost-Kingdom-only behaviour is **Instant Action** alone, which disables the island arrows.
>   The data agrees: fantasy, hallow and space ship the identical eight wads, `Standard.sam`,
>   `scape.omp`, `2dmap.tga` and every folder jungle has - jungle's only extras are `Easymode.TPWI` and
>   `Easy_Standard.sam`. **STILL OWED: an actual launch of fantasy, hallow and space.** I phrased this
>   wrongly again on 2026-09-16 ("the sole playable park") from the save-file fact, which is the SECOND
>   time - so it is a recurring error of mine, not a one-off: the save decides what is PLACED in a park,
>   never whether the park loads.
> - **Lost Kingdom places exactly one ride**: catalogue **1100**, the **Belly Bounce** (confirmed by
>   decompressing `jungle/rides/bouncy.wad`'s `Bouncy.sam` - `Info.Id 1100`, `Info.Name "Belly
>   Bounce"`).
> - **That ride's script contains ZERO `COAST` opcodes.** It drives a `BOUNCE` family instead
>   (`BOUNCESETBASE`, `BOUNCING`, `BOUNCE`, `UNBOUNCE`, `FORCEUNBOUNCE`) plus `EVENT`, `TRIGANIM`,
>   `LOOPANIM`, `STARTSCREAM`, `REPAIREFFECT`. **`RideState`, built for `COAST`, is not the ride this
>   ride needs** - wiring it up would construct a state object nothing ever touches.
> - **39% of `Bouncy.RSE` is unimplemented** (35 of 90 instructions), so the join would show a park
>   whose one ride burns four instructions in ten on `NotImplemented`.
>
> **So the join would have looked like success and demonstrated nothing.** It is still the right
> eventual shape; it is not the right *next* step.
>
> **>>> WHAT STEP 5 BECAME: the clock and the chance a script keeps - branch 65, `56ff8ad`. <<<**
> World-free, additive, and backed by handlers read the same day: `GETTIME`, `SETTIMER`, `GETTIMER`,
> `RAND`. See the live block at the top of this file. Corpus effect: unimplemented instructions
> **4035 -> 3746** of 11,913.
>
> **>>> STEP 6 IS DONE, AND THE ANSWER WAS NEITHER CANDIDATE BELOW. <<<** Measured 2026-09-15 *before*
> building anything: the **`BOUNCE` family is 46 uses across 4 scripts**. It moves unimplemented
> instructions 3,746 to 3,700 and leaves fully-implemented scripts at **29/308 - it unlocks nothing** -
> and it needs guest handles that nothing supplies. The **animation family** instead: 1,016 uses,
> 3,916 -> 2,900, **29 -> 109 scripts complete**, and `WAITANIM` alone is the first thing to block
> **183 of the 308**. Built as branch 66. **The lesson: this file's own suggestion was wrong and one
> measurement settled it** - the same shape as step 5's rejection, and worth doing again before step 7.
>
> **What step 7 should weigh**, in the order the corpus actually spends words:
> - **`ADDOBJ` (644 uses / 157 scripts)**, now the first blocker for **89** scripts. Needs objects.
> - **`EVENT` (527 / 99)**, first blocker for 18. Needs the message system.
> - **`SPAWNSOUND` (28 / 28)**, first blocker for **28** - much the smallest of the three to build, and
>   it needs sound only, which already exists.
> - **`TRIGWAITANIM` (133 / 56)**, whose channel cursor at `+0xbc` is the one unread part of a family
>   that is otherwise finished.
> The two older candidates still stand on their own evidence:
> - **The `BOUNCE` family** (opcodes 70-75), what the only placed ride drives once there are guests.
> - **The park join**, once there is something for it to join *to*.
>
> **Two corrections to this plan's own assumptions, found while measuring:**
> - **`RideInfo.cs` does NOT name the fields a ride runtime needs.** It covers the `Info.*` namespace
>   only; **capacity lives in `UsageInfo.MinCapacity`/`MaxCapacity` and `Upgrades[N].InitCapacity`/
>   `RedLineCapacity`**, which `RideInfo` does not mention at all. The earlier note calling step 5 "a
>   mapping job, not a format job" was right about the difficulty and wrong about where the fields are.
> - **`ParkItemCatalogue` already reads every ride's `.sam`** and already knows each item's directory
>   and stem, so much of the "join" plumbing exists already.
>
> **Two data rules worth keeping:** a ride's script is `<stem>.RSE` in whatever case the wad spells it -
> **75 of the 76 ride wads**, the case varying wildly (`B_DRIP`, `Bigapple`, `GoKarts`, `mBUGGY`); and
> the one exception, `mystery.wad` in all four themes, **is not a ride at all** (`Info.Id 100`,
> `Info.IsChoosable 0`, `Info.WhichUIType 4 "Not to be shown in UI"`), which is why it carries no
> script.
>
> **Do NOT open by deleting `Ride.cs` or `RideVM`.** **SETTLED 2026-09-15: `VM/` STAYS** - Alexah handed
> me that decision and it is closed ([[ride-vm-may-be-replaced]]); every step of P5 has been additive,
> and that is why none of it ever needed the standing permission.
>
> **Reading the handlers rather than the names was not optional - it changed three answers:**
> **`COAST_SETWORN` calls NOTHING** (twelve scripts use it; implementing it from `ScriptDefs` would
> invent a wear system the original lacks); **`GETQUEUE` returns room REMAINING**, which inverts the
> sense of Coaster1's `COAST 2 0` / `BRANCH_Z`; and **`SETCLOSED` is a state transition**, not a flag.
> **`BUMP` (0x005546f5) is the bigger interface** - 199 uses, 16 selectors - and is what to read next.
>
> **(superseded, kept for the reasoning) STEP 2 WAS: THE INTERPRETER CORE - the opcodes that touch no world.** About
> thirty of the 84 the corpus uses: `COPY`, `SETLV`, `TEST`, `CMP`, the five branches, `JSR`/`RETURN`,
> `ADD`/`SUB`/`MULT`/`DIV`/`MOD`, `RAND`, `PUSH`/`POP`, `WAIT`/`WAITABS`, `SETTIMER`/`GETTIMER`,
> `NAME`, `NOP`, `END`. They are where the corpus spends most of its words, they need **no guest, no
> staff and no ride**, and they can be tested by running a real shipped script until it blocks. The
> world-touching opcodes - `ADDOBJ`, `TRIGANIM`, `EVENT`, `WALKON`, the `LIMBO` and `SCREAM` families -
> wait until there is something to act on, and should be honest no-ops until then rather than guesses.
>
> **BUILD IT RATHER THAN REPAIR THE INHERITED ONE - and this is now evidence, not taste.** Three
> separate defects, any one of which is a rewrite of the class it sits in: the constructor throws
> (`Variables[VAR_RIDECLOSED]` into an empty list); `BranchTo` multiplies a target by 4 and adds the
> first instruction's offset, when a target is an absolute word index the PC takes unchanged, as 2,664
> of 2,664 branches show; and `JSR`/`RETURN` push and pop a **`Queue<int>`**, so a nested call returns
> to the FIRST caller rather than the most recent. Alexah's standing permission covers this -
> [[ride-vm-may-be-replaced]] - and it does **not** cover throwing away `Opcode.cs`'s 106 verified
> names or the 27 handlers, which are real work and stay.
>
> **What a first honest milestone looks like:** load `Coaster1.RSE`, run it, and show it reaching its
> own `WAIT` loop with `VAR_RIDECLOSED` set - no rendering, no park, one test.
>
> **The format is verified, not sampled** (312 wads scanned, 308 scripts, **308 of 308** agreeing):
>
>     magic  "RSSE" (0x45535352 LE)      version dword 0x00010F51
>     then a count (13 for Coaster1, 10 for EventMap), then 0, then 50
>     loader FUN_005587f0 reads counted blocks: 4-byte records at +0x23 and +0x15,
>     8-byte at +0x16, 16-byte at +0x19, 32-byte at +0x1f, four skipped dwords,
>     the SCRIPT BODY at +0x14 (count x 4) and a STRING BLOB at +0x24
>
> **The instruction set is verified by hand:** `FUN_00551cb0` requires a dword whose top byte is 0x80
> and dispatches while `(word ^ 0x80000000) < 0x6a` - **106 opcodes** - through the jump table at
> `0x5567d8`. The table at `0x765280` is `{name*, operandCount*}` where the count is an **ASCII digit
> string**, decoding in exactly `Opcode.cs`'s order. Full detail in [[park-data-layout]].
>
> **>>> ALEXAH'S STANDING DECISION, 2026-09-14: THE INHERITED VM MAY BE ABANDONED. <<<** His words:
> *"If the original maintainers work on the ride-VM-scripting-thing will mess you up, we can just
> abandon that and use our own implementation since that's worked so well so far for everything else."*
> **Read [[ride-vm-may-be-replaced]] before acting on it** - the permission is conditional on my
> judgement, it is scoped to the ride VM alone, and "abandon" means the broken runtime and **not** the
> verified research: `Opcode.cs`'s 106 names match the binary and 27 handlers are implemented. Keep
> those. `RideVM.cs:42`'s claim of 210 opcodes is simply wrong and must not be carried forward.
>
> **What is inherited and still does NOT run:** nothing constructs a `Ride`; `RideVM`'s constructor
> would throw, writing `Variables[VAR_RIDECLOSED]` (index 6) into a list it just created empty; and
> `BranchTo` and `JSR`/`RETURN` are both wrong (see step 2 above). **A class named RideVM is not a
> VM.** **The one thing that HAS changed: `RideScriptFile` is no longer a commented-out line** - it is
> built, on branch 61. The commented line in `RideVM.cs` still cannot simply be uncommented, because it
> passes `this` to a constructor the new reader does not have and never should.
>
> **Where the scripts live:** never loose on disk - inside
> `data/levels/<theme>/{rides,shops,sideshow,features,upgrades}/*.wad`. Case is inconsistent
> (`Coaster1.RSE`, `Monkey.rse`, `child.RSE`), so any lookup must be case-insensitive. `EventMap.RSE`
> appears in **28** of them, a minority - an earlier note said "most" and was corrected the same day.

### P6 - Writing a park back (`.TPWS`)

Until it exists, **Restart Park cannot mean what it says** - it reloads the file rather than resetting
a state, which is a traced departure recorded in `ParkFrontEnd`.

### Open items to carry, none of them blocking

- **VmRSS is unproven and asset counting will not settle it.** Branch 51 showed counts flat while RSS
  moved 230MB; it wants a managed-heap instrument (`GC.GetTotalMemory`) or the renderer's live-resource
  counts. **Do not go looking for more leaked assets** - there are none.
- **A park has no `ScreenParticles`**, so `ButtonGlint` starts emitters that tick and can never draw.
  `SetCount` is bounded and kills them on exit, so it is waste, not a leak. Revisit when a park gets
  one - and note the glints would then appear on the gadget's buttons.
- **Nothing stops the mouse reaching the world through a HUD control.** The park camera reads
  `Input.Mouse.Wheel` directly, so the wheel over a gadget button also zooms. Latent before branch 54;
  reachable now.
- The release-edge decision (the original fires shortcuts on key **up**, OpenTPW on press) remains
  **Alexah's, not mine**.

> **THE PARK HUD RESEARCH IS FINISHED, 2026-09-14. IT IS ALL IN [[park-hud-from-exe]] - READ THAT
> BEFORE WRITING ANY OF IT.** **(This said "Nothing is built yet" until 2026-09-15; it is BUILT on
> branches 57-60 - `ParkGadget`, `ParkViewfinder`, `ParkMapScreen` and `ParkLines`. What is left is
> deliberate or blocked: the message bar, the aerial cluster, and the bank-balance cluster.)**
> What is settled: the **compiled layout-stream
> opcode format** (0x0065fd58, fully decoded, and it self-checks - every stream starts on op 0 and
> ends on a balanced op 5); the **mesh-name hash cracked 39 of 39** (`h = (c ^ h) * 47` from 0, over
> the model's **first node name**, untruncated); **every park panel's stream** with control ids,
> rects and artwork; and the assembly point **`FUN_00489f50`**, which builds two full-screen layers on
> the same 2048x1536 virtual screen the lobby uses and then six panels, all hidden.
>
> **THE FINDING THAT CHANGES THE SHAPE OF THE WORK: the gadget is SIX CATEGORY PICKERS, NOT 17
> BUTTONS.** `b_buy`, `b_info`, `b_money` each open *the screen that category was last left on* via
> `FUN_004a0940( n )` and a remembered-tab global; `b_map`, `b_resrch` and `b_camera` go straight to
> one thing. That is why the 17 shortcut actions never reconciled with the gadget's button count -
> **they were never meant to.** The six are confirmed twice over, from the disassembly and from the
> game's own UIHELPTEXT rows 469-474.
>
> **Two corrections this produced**, both already applied: 0x00751530 is the **coaster builder**
> (every button a `cb_*` mesh; help rows 296-304 are all pylons and track), not "the build toolbar";
> and 0x0074fa98 is the **camcorder viewfinder**, not a park HUD root.
>
> **A survey of OpenTPW's own UI machinery is in that memory too** - a park's HUD today is only
> `WindowStack` + `ParkFrontEnd` + `Cursor`, `IslandPanel` is the template to follow, there is **no
> layout-stream loader and should not be one yet**, a HUD panel must leave `Pauses` false, and
> nothing currently stops the mouse wheel reaching the park camera through a HUD control.

**Harnesses added 2026-09-14**, all in `~/.cache/tpw-harnesses/` and none of them in the repo
([[no-tooling-in-the-codebase]]): **`assetprobe.py`** - lobby, park, lobby, park, lobby, reporting
entities/models/assets/VmRSS at each build; this is the reproducible measurement for any further work on
the leak residue or the unproven VmRSS, so re-run it rather than writing another. **`signprobe.py`** -
proves a ride's board paints. **`signorbit.py`** - orbits a lobby island to bring a sign into view.

**Added later the same day, for the gadget and the map - re-run these rather than writing another:**
**`armshot.py`** (17 checks) - the advisor, and then the arm: it clicks real controls through XTEST,
because no console command can click one. **`mapshot.py`** (9 checks) - the park map: opens it from the
gadget's button, proves the park PAUSES end to end by reading `advisor`'s `paused=` (which
`ParkFrontEnd` drives from `_stack.AnyPausing`, so it tests the window's `Pauses` flag for real), zooms,
scrolls, closes, and demands the asset count come back EXACTLY where it started. **`mapleak.py`** - the
discriminator that saved a wrong fix: when the map read +1 asset over four open/close cycles it
measured an idle period, then one cycle, then three more (+0, +1, +0), proving a **one-off on first
open** rather than a per-open leak - and `assets list` then NAMED it as the help bar's first line of
text. **Reach for this shape before "fixing" any leak**: an idle control period tells you whether the
thing you changed is responsible at all.

**Added 2026-09-14 for the ride scripts: `rsewalk.py`** - the instrument that established the `.RSE`
format and the one to re-run after any change to `RideScriptFile`. It carries the layout, the 106-entry
opcode table with operand counts, and every check that could fail: all 308 files parsed to the last
byte, 2,664 of 2,664 branch targets landing on an instruction start, 330 of 330 string operands
resolving, one version and one pad variant. **It also carries the extraction recipe**, which matters:
keying the output directory by the wad's own folder name loses the theme - every script lives in a
`rides`/`shops`/`features` folder - so 262 wads collapse into 203 and silently overwrite each other.
That cost a run, and the fix is a per-wad subdirectory built from the path relative to `data/`.

> **EVERY ONE OF THESE READS CONSOLE REPLIES, WHICH ARE WRITTEN `[dbg] {message}`** by
> `DebugConsole.Reply`. A filter anchored on `asset ` at column 0 matches **none** of them and reports
> it as "nothing changed" - that cost a whole game run. See [[verify-every-ordering]] rule eleven.

## Priority 1 - A park that feels like a park: sky, weather, sound

> **STATUS 2026-09-14: ALL THREE DONE.** Sky and music landed on branch 44 and are **pushed** - the sky
> is invisible at the park camera's angles, which is a finding and not a bug, because it is there for the
> first-person view. **Weather is BUILT and PUSHED, on branch 46 (`8e1f181`)** - see its state
> block below and **[[park-weather-from-exe]]**. The deferral reasoning further down is kept only for its
> evidence and is **superseded**: it says weather is blocked and unbuilt, and neither is true any more.

The park draws correctly now but is silent and sits under the lobby's sky. **`Sky` takes its colours from
whichever lobby island the camera is on**, which is meaningless in a park - that is a real wrongness, not
just a gap. The park's own sound categories (ambient, rides, speech, music) are never started; the
original loads them in state 9 via **FUN_0051ec50**, and the scopes are already traced in
[[original-lobby-park-sharing]]. The sky loader is shared and takes a path and a height (FUN_00585690;
the lobby passes `levels/fantasy` and 180, the object default is 300).

**Why first:** bounded, already traced, needs no new file format, and it is visible and audible - Alexah
can check it the way he has checked everything else. Park sounds are **positional with the camera as the
listener**, unlike the lobby's flat (0,0,0), so [[original-positional-audio]] and
[[verifying-audio-by-capture]] both apply.

## Priority 2 - A game clock, and a park pause that means something

> **STATUS 2026-09-14: DONE on branch 45, `9c3aee4` - PUSHED. See the state block below.**
> The clock exists, the beat is the original's 31ms, a park's menu stops it and the lobby's does not.
> **This unblocks weather** (Priority 1's third unit), which was deferred behind it.

**The menu's `Pauses` is inert in a park today** because there is no clock to pause - `UiWindow.Pauses`
is honoured by the lobby holding its advisor, and a park has nothing. The original ticks **31ms in both
loops** with at most 500ms of catch-up in the lobby and 2000ms in a park, and **only parks pause the
clock** ([[original-boot-sequence]], [[original-lobby-park-sharing]]). Deferred as "a pausable game clock
and a shared 31ms ticker" since before parks existed.

**Why second:** foundational. Nothing simulated - guests, staff, rides, money - can exist without it, and
it closes a hole branch 42 opened by shipping a menu that claims to pause.

## Priority 3 - Release what a scene loaded - **DONE 2026-09-14, branch 48. PUSHED.**

One commit **`37df07e`** on **`alexah/48-release-what-a-scene-loaded`**, stacked on branch 47 at
`8da19c7`. Verified to build **ALONE** in a throwaway worktree at **127/0** with 120/120 tests, none
skipped. **The warning baseline is now 127, not 128** - see below.

> **WHAT A SCENE UNIQUELY OWNS IS EXACTLY WHAT IS NOT CACHED.** Shaders and textures are kept by path
> and shared across scenes - which is why a rebuilt lobby registers 404 assets against 829 - while
> **models and materials are built fresh every scene and shared with nothing.** So the ownership
> question the audit left open answers itself: free models and materials, touch nothing else. That
> also sidesteps the double-free hazard entirely, because the hazard lives in `Texture`.

- **`Level.Unload` already existed and already worked** - it deletes every entity between frames, and
  entity counts return to exactly 260 (lobby) / 367 (park) build after build. What it never did was let
  go of what those entities were *drawing with*. An earlier worry that the old scene kept updating was
  **wrong**, disproved by measurement.
- The chain is `Unload -> Entity.Delete -> OnDelete`, which already existed. `ModelEntity.OnDelete` now
  releases both halves; `Model.Delete` releases its buffers and its material; `Material.Delete` releases
  pipeline, scratch buffer, layouts and resource sets - all through `Render.ScheduleDelete`, the queue
  the renderer already retires superseded resource sets on. **No new architecture.**
- **Left alone deliberately, all shared**: the `Shader` (cached by path, outlives every material), the
  `static readonly Samplers`, and the textures (cached by path, with sharers `Asset.All` cannot even
  enumerate - a cache hit returns *before* `Register()`). Disposing a resource set does not touch what
  the set binds, which is what makes freeing sets safe. The two static materials refuse, as
  `Texture.Missing` already does.
- **A material must unsubscribe from `Shader.OnRecompile`.** The shader is cached and never released, so
  the event held a strong reference to every material ever made, and a recompile would rebuild the
  pipeline of a disposed one. The delegate is kept in a **field**: the subscription is a closure, and
  `-=` with an equivalent lambda removes nothing.

**Measured, twice, with identical numbers both runs** (lobby, park, lobby, park, lobby):

    models  295 ->  484 ->  299 ->  488 ->  303     (was 295, 681, 880, 1266, 1465)
    assets  845 -> 1385 -> 1036 -> 1418 -> 1069     (was 845, 1779, 2198, 2974, 3393)

Residue **4 models and 33 assets a cycle, against 585 and 1,195**. No exception or validation error in
any run; `save/` untouched. **CLOSED on branch 51 (`2018166`) - it is now zero; see that state block.**

> **THE RESIDUE IS NOW IDENTIFIED (2026-09-14, branch 51).** A new `assets` console command lists
> `Asset.All` by kind and path, and `assetdiff.py` diffs the multisets between successive builds of
> the same scene. Measured **+33 assets / +4 models per cycle, identical for lobby2->lobby3 and
> park1->park2** - so the old count was right, only unnamed. What it is made of:
>
>     21  Texture       no path at all
>      4  Texture       "Stream <hash>"
>      4  Model         no path
>      2  Material      no path
>      2  Material`1    no path
>
> **29 of the 33 have an EMPTY `Path`, and the other 4 have a meaningless one.** Nothing with a real
> path leaks - those are shared by design. The mechanism is single: the cache finds an asset by
> scanning `Asset.All` for a matching `Path`, so `Path == ""` can never hit, and every scene build
> registers a fresh one. The `Stream <hash>` four come from `Texture( Stream )`, which names itself
> `$"Stream {stream.GetHashCode()}"` - **reference identity, not content**, so it can never match
> either. **VmRSS is still unproven; this is about counts, not bytes.**
>
> *Two harness traps found the hard way, both of which made the first run under-report: a generic type
> prints as `Material\`1` so `\w+` drops it, and the pump `rstrip()`s each line so a pathless
> `asset Texture ` loses the space the regex matched on. See [[verify-every-ordering]] for the family.*

> **WHO OWNS THE RESIDUE (read 2026-09-14; the fix is NOT written yet).** Two hypotheses I wrote down
> earlier are **refuted**, and both would have caused damage:
> - **`UiMesh` does NOT leak.** `UiMesh.Get` keeps a static `Loaded[name]` cache - meshes load once
>   and are meant to outlive scenes, like textures by path. Releasing one would pull geometry out from
>   under every other scene still drawing it.
> - **"The UI never releases what it owns" is wrong.** `UiControl.ReleaseText()` recurses into its
>   children, `WindowStack.OnDelete` closes every window and the help bar, `ScreenParticles.OnDelete`
>   frees its atlas, and `Level.Unload` already walks `Hud.Children`. `UiText` even reuses its texture
>   when the size is unchanged and deletes the old one when it is not.
>
> The real owners, each confirmed by reading:
> - **`Sky` has NO teardown at all.** It is a plain `Entity` (not `ModelEntity`), overrides only
>   `OnUpdate`/`OnRender`, `Entity.OnDelete` is an empty virtual - and `Level` builds one per lobby
>   (`Level.cs:137`) and one per park (`Level.cs:229`). It creates pathless textures at three sites:
>   a 1x1 tint ramp, a gradient ramp, and the dome image.
> - **`ScreenParticles.Layer` is never deleted.** Each `Layers` holds two (`Blended`, `Added`), each
>   building one `Material` + one `Model`; `OnDelete` frees only `_atlas`. `_unowned` alone is the
>   `Material=2` seen every cycle.
> - **`WeatherSprites.LoadTexture` is the only `new Texture( stream )` in the codebase**, so it is
>   definitively all four `Stream <hash>` textures. `ModelEntity.OnDelete` frees its model, never this.
> - **Sign textures** (`SignTexture.TryBuild`) are handed out two at a time as override dictionaries to
>   `LobbyIsland`, `ParkFixedItems` and `ParkObjects`, with no owner tracking them for release.
>
> Releasing these is safe by branch 48's own rules: `Texture.Delete` refuses only `Missing`,
> `Material.Delete` only `Default`/`UI`, and `Model.Delete` frees its buffers **and** its material.

> **THE RESIDUE, ITEMISED AND ATTRIBUTED** (2026-09-14, after `assets list` was taught to print a
> pathless texture's size). Identical every cycle, lobby->lobby and park->park alike:
>
>     12  Texture  128x128        SIX SIGNS x two panels - SignTexture.PanelSize = BoardHeight = 128
>      4  Texture  256x256        still unattributed - see below
>      3  Texture  1x1            Sky's tint ramp - `new Texture( _tintPixel, 1, 1 )`, _tintPixel is 4 bytes
>      2  Texture  16x16          Sky's gradient ramp - `new Texture( ramp, GridSize, GridSize )`, GridSize = 16
>      4  Texture  Stream <hash>  WeatherSprites.LoadTexture, the only `new Texture( stream )` there is
>      4  Model + 2 Material + 2 Material`1   ScreenParticles.Layer - two per Layers (Blended, Added)
>
> Sizes read from the source, not guessed: `Sky.GridSize` is `const int 16`, `SignTexture.PanelSize`
> is `BoardHeight` which is 128, and `Model.Delete` frees a model's material with it, which is why the
> models and materials arrive in step.
>
> **THE 4 x 256x256 ARE THE SKY ART**, measured off disk: `data/levels/<theme>/sky/sky.tga`,
> `sky_rgb.tga` and `sky_cyl.tga` are each **256x256** (the `ssky/` low-detail set is 32x32 and the
> moons are 128x128). `Sky` decodes them at line 407 into
> `new Texture( image.Data, image.Width, image.Height, TextureFlags.Wrap )`. **Every cell of the
> residue is now attributed.**
>
> **The twelve sign panels are six signs, named by the run log**: four per lobby build (`Fan_gate`,
> `Spa_gate`, `Jun_isle`, `Hal_isle`) and two per park build (the park gate and `Belly Bounce`), two
> panels each.

> **FIRST FIX LANDED: 33 -> 24 a cycle.** `Sky` now keeps the four textures it builds and releases
> them in an `OnDelete` it never had (it is a plain `Entity`, so it was being deleted and nothing
> happened). The nine that go are exactly the nine predicted - 4x256x256, 3x1x1, 2x16x16 - for a
> rebuilt lobby and a rebuilt park alike. `Texture.Missing` is skipped rather than deleted, because
> `Texture.Delete` refuses it loudly and the sky would say so once a scene.
>
> **WHAT IS LEFT, all named by shader** (`assets list` now prints a pathless model's or material's
> shader path):
>
>     12  Texture 128x128                     the six signs' panels - SignTexture, no owner tracks them
>      4  Texture Stream <hash>               WeatherSprites.LoadTexture
>      2  Model + 2 Material   particles.shader   ScreenParticles.Layer - _unowned's two layers
>      2  Model + 2 Material`1 test.shader        STILL UNIDENTIFIED - one per scene
>
> **ANSWERED, AND FIXED: 33 -> 4 a cycle.** The `test.shader` pair was `ParkGround` and `ParkPaths`
> **overriding `OnDelete` without calling `base.OnDelete()`** - and `ModelEntity.OnDelete` is the one
> override in the tree with a body, the very thing branch 48 added to free `Model` and
> `TranslucentModel`. A sweep found **13 of 13 overrides skip base**, but only those two matter: every
> other base (`Entity.OnDelete`, `Panel.OnDelete`) is empty. **Do not "fix" the other eleven** - the
> emptiness of the base is what makes them harmless, and chaining everywhere would blur the signal
> that found this.
>
> Five fixes, measured with `assets list` before and after:
>
>     12 x Texture 128x128    signs      LobbyIsland / ParkFixedItems / ParkObjects now own and
>                                        release the panels they paint                      GONE
>      2 Model + 2 Material`1  test.shader  ParkGround / ParkPaths now chain to base          GONE
>      4 x Texture Stream      weather    WeatherSprites keeps its art and releases it    NET ZERO
>                                        (the diff shows +2/-2: two released, two fresh with
>                                         different object hashes - identity churn, not growth)
>      9 x Texture             sky        Sky.OnDelete, earlier this session                 GONE
>      2 Model + 2 Material    particles.shader   ScreenParticles.Layer            GONE (2018166)
>
> **`ScreenParticles` was the last of them, and it is done**: `OnDelete` now releases `_unowned` and
> every `_windowLayers` entry before the atlas (commit `2018166`). The residue is zero a cycle.

> **TWO THINGS THIS DID NOT SETTLE, and they must not be written up as solved.**
> 1. **VmRSS is unproven.** It still reads 534-915MB across the five builds with no clear downward
>    trend. Freeing through the deletion queue need not return anything to the OS, and managed heap
>    growth is GC-timed. The counts are decisive; the footprint is not.
> 2. ~~**The residue is unidentified**~~ **SETTLED on branch 51 (`2018166`).** The instrument exists
>    and shipped: the `assets` / `assets list` console command names every asset by kind, a pathless
>    texture by its size and a pathless model or material by its shader. The residue was attributed in
>    full - signs, sky, weather art, ParkGround/ParkPaths, ScreenParticles - and is now **zero a
>    cycle**. Only the VmRSS half of this pair is still open, and branch 51 showed asset counts were
>    never what drove it.

**Two pre-existing defects found and deliberately NOT touched** (out of scope, both real): `Terrain`
mutates the shared `Material.Default` through `material.Set( "Color", texture )`, and `SetupResources`
overwrites `Pipeline` and `ScratchBuffer` on every hot-reload recompile **without disposing the old
ones**. Also noted: `ClearBoundResources` is a documented deliberate no-op, **not** a bug - it "returns
immediately and has done for as long as it has been in the tree".

## Priority 4 - The ride's name board - **DONE 2026-09-14, branch 47. PUSHED.**

Two commits on **`alexah/47-a-rides-name-board`**, stacked on branch 46 at `8e1f181`. Both verified to
build **ALONE** in a throwaway worktree at 128/0 with 120/120 tests, none skipped:

    f104e0a  Read every sign the game ships, not just the painted ones
    8da19c7  Letter a ride's name onto its board rather than onto black   <- TIP

> **THE FILE IS READ IN ORDER AND MUST BE WALKED, NOT INDEXED.** Three things move everything after
> them: each line's 20-byte ink block exists only when that line's mode (`+0x09`, `+0x0D`) is non-zero;
> each of the two fill textures states its own `(w, h, bpp)`; and the artwork header is present only
> when the byte at `+0x08` says so. Walked that way the parse lands exactly on the end of **all 84**
> signs through the game's own file system - BILZ hit **23/23**, end-of-file **61/61**.

- **The old "16, 128, 4 looks like image metadata" puzzle is SOLVED**: those ints are the `(w, h, bpp)`
  header of the **first of two 16x128x4 fill textures** - `2 * (12 + 8192) = 16,408`, which is the whole
  16 KB span that would not reconcile against 64x64x4. They are each line's **fill pattern**, resampled
  to the text height and tiled over the glyph mask (`FUN_005eb8a0`). We letter in the ink block's flat
  colour instead, an approximation of what is often a vertical gradient. **Not a thumbnail - `SignFile`'s
  old "64x64 BGRA" remark was invented, and is gone.**
- **The 40-byte header difference is the two conditional ink blocks** (2 x 0x14). Three signs set both
  modes to zero - `jelly`, `zob`, `C_SCAT` - so both blocks are absent and everything shifts up.
- **The artwork is stated 256x128 but encoded at 256x256.** The colour chunk cannot separate those
  readings (`256*128*3` and `256*256 + 2*128*128` are **both** 98304) - **the alpha chunk can, and it
  inflates to 65536 = 256*256.** So the decode stays 256x256 and only the top 128 rows are board, which
  is what `SignTexture` already cropped and had until now only guessed at.
- **What a chunk-less sign letters onto is answered: nothing.** With `+0x08` clear the original clears
  the board to **transparent black** (`0x005ecd09`) and skips the artwork blit (`0x005ecd18`) - the name
  floats on the ride. `FUN_00467a80` substitutes a runtime 128x128 texture per face, which is why ride
  WADs ship no sign art, and `FUN_005ecb40` has **exactly one caller in the whole binary**, so rides and
  the lobby share one painter and one 256-wide-split layout.

> **THE SECOND HALF WAS A RENDER BUG, NOT A FORMAT BUG.** With the parser fixed the board still drew
> **solid black**: the world's shader only honours alpha on a see-through material, and a ride's sign
> material is authored solid. The original does not trust that flag either - it **ORs 0x2 in as it
> substitutes the texture** (`0x00467d00`, `0x00467d60`). `LobbyModel` now does the same for any
> overridden material, in **both** places the bit is read (per-vertex `MatFlags`, and the solid/
> translucent triangle split). **Only a screenshot caught this - the log said "painted" either way.**

- **It also repairs 8 painted boards.** Of the 23 with artwork, **8 are shaped cut-outs** reaching alpha
  0 across 5-43% (`bumper` 42.6%, `candy_c` 32.9%, `cat_co` 17.6%, `fwheel`, `tourride`, `coaster1`,
  `coaster3`, `b_drip`) and were being drawn as **opaque rectangles**. The other **15** are solid art
  whose only partly-clear texels are the **1.6%** the `.wct` codec rings around a hard edge - the same
  figure on every one of them, unrelated artwork included - so nothing visible changes for those.
- **Verified in the game**: Lost Kingdom's Belly Bounce paints its own sign with no complaint, and its
  name reads on the ride's own carved plank with the timber showing through around the letters. `save/`
  untouched except `opentpw.cfg` re-learning `steps jungle.first` (940), which is by design.
- **84 is the real count** (23 painted / 61 bare). A working figure of 159/46/113 was an **extraction
  artifact** - the same sign pulled out under several paths - caught by `SignFileTests`, which walks the
  game's own file system rather than a scratchpad.

### The old diagnosis, kept because it was wrong three ways

> It read: *"every ride `.sgn` is 17,337 bytes, 36 short of the 17,373 the gates and lobby islands
> use... It is a second variant."* **Not every ride. Not a variant. Not about rides.** The 36 bytes are
> exactly one record - the artwork's own header - which a board with a picture has and a bare one does
> not.

**All 84 shipped `.sgn` files were extracted per-theme and measured** (`wadcat --dump sgn` into per-WAD
directories - a flat dump silently overwrites same-named signs across themes, and that is exactly how
"every" came to be believed).

> **THE RULE, structural rather than a table of sizes: THE HEADER ENDS WHERE THE CHUNK MAGIC `BILZ`
> BEGINS.** Chunk sizes sit at **BILZ-8**, the four dequantisation floats at **BILZ-24**. Verified
> `BILZ + colour + alpha == filesize` **EXACTLY, 23 of 23, zero failures.**

    23 signs HAVE artwork   -> contain BILZ.  Two header sizes: 0x43DD = 17,373 (21 files)
                               and 0x43B5 = 17,333 (2 files: jelly.sgn, zob.sgn).
    61 signs have NONE      -> no BILZ anywhere.  60 are 17,337 bytes and 1 is 17,297 -
                               exactly 36 less than each of the two header variants.

- Both header variants carry the **identical** floats `[6.0, 10.0, 2.0, 6.0]` at BILZ-24, so the 24-byte
  trailer is common and only what precedes it differs, by exactly **40** bytes.
- **`(+8 & 1)` predicts artwork perfectly, 84/84** - odd means chunks, even means none. The old note's
  "+4 and +8 read 0,1 on a gate and 1,0 on a ride" was pattern-matching on two examples: among the
  chunk-less files +4 is 0 **or** 1 and +8 is 256 **or** 512.
- **ELEVEN RIDE SIGNS ALREADY READ FINE TODAY** at the 17,373 header - Brainb, bumper, b_drip, fwheel,
  cat_co, coaster1, coaster3, tourride, candy_c among them. **Rides appear in BOTH groups.**

**So `SignFile`'s real fault WAS refusing ANY sign that has no artwork**, demanding a 17,373-byte header
before ever asking whether an image is present. **FIXED ON BRANCH 47 (`8da19c7`)** - the reader walks the
file in order now (conditional ink blocks, two fill textures, optional artwork), all 84 signs read, and a
bare board is cleared to transparent so the lettering sits on the ride. The plan below is kept only as a
record of how it was approached: find BILZ and read the
trailer relative to it; the open design question is what a chunk-less sign should letter *onto*, since
`SignTexture.CropBoard` needs a board and there is none - see `ParkFixedItems`, where a gate whose theme
ships no sign artwork is already handled.

**Still open, and NOT needed for the fix:** the thumbnail geometry does not reconcile. Pixel-looking data
starts at **0x03AD** in every file, and 0x03AD-to-BILZ is 16,432 (17,373 variant) or 16,392 (17,333
variant) - neither is 64x64x4 = 16,384. The ints at 0x03A1/0x03A5/0x03A9 read **16, 128, 4** identically
in both variants and look like image metadata rather than the padding `SignFile`'s remarks assume.

## After those, in rough order

**Camcorder mode - the first-person view - is BUILT, on branch 49 (`af157d8`), PUSHED.** See its
state block above. Alexah supplied the name and the trigger and both checked out in the exe: it is
**shortcuts action 16, key 'C'**, and also **HUD button id 99** on the park management gadget
(`FUN_00498ad0`, message 0x100), entering through **`FUN_00481a10`**. Full chain, the 20-byte
binding-entry layout, the whole shortcuts list and a **correction to the branch-44 guess about
`gui_CameraFlags` bit 0x80** are in [[park-engine-from-exe]] under "Camcorder mode".

> **This paragraph used to end "it wants the park HUD, which does not exist yet", and that was wrong.**
> The HUD only ever gated the *button*; the **'C' key is an independent route** and needed no HUD at
> all, which is how branch 49 shipped without one. Do not re-derive the block. It also claimed "this is
> the view a park's sky exists for" - true in spirit, overstated in fact: captures show the orbit camera
> does render sky, just as a margin round a top-down view rather than as anything you would call a sky.

**And the button beside it, id 100, is POSTCARD** - Alexah's call again, and proven: its shortcut thunk
`0x00481500` is a bare `JMP` to **`FUN_004a9380`**, the very function that button calls. It pauses,
clears `g_ParkRunning`, hides the HUD and selects a screen, and the game ships `data\Postcard.wad`,
`postcard.jpg` and an HTML postcard template, so it writes a picture out. **Explicitly NOT in scope now
- Alexah flagged it as needed later for 100% parity.** Both are in [[park-engine-from-exe]].



**The advisor in a park - RESEARCHED 2026-09-14, and BLOCKED. Nothing built.** Read
[[park-advisor-from-exe]] before touching it: the mechanism is fully mapped (a THING of type 0x0b on
weather's own tick, message-driven, eight scored slots, `Advisor.sam` parsing with the existing
`SettingsFile`, and `AdvisorResponseTable` at 0x00768fb8 dumping statically to 512 rows). **THAT "BLOCKED ON CONTENT" CLAIM IS NOW WRONG FOR ONE WHOLE CLASS** (corrected 2026-09-14): the
acceptance gate `FUN_0059abc0` consults **no world state at all** - only per-message cooldowns, repeat
limits, slot caps and a rotation counter - and the park HUD's own buttons post advisor lines directly
(`FUN_00486b00( id )`, withdrawn by `FUN_00486b40( id )`). So **screen-open lines are blocked on the
park HUD, not on simulation.** What remains blocked on content is everything keyed to staff, visitors,
rides, research or money. His speech is already scene-independent, so wiring
him up later is one line. **One trap is written down there: the message-to-sample rule I derived held for
eight consecutive messages and then drifted by +5 - do not reuse it without re-deriving.** Then a
**park HUD** proper - **RESEARCHED 2026-09-14 and no longer a blank sheet.** [[park-engine-from-exe]]
now carries: all 17 management-gadget buttons resolved to real functions, a **54-row map of every
screen in the game** to its compiled layout stream and message handler, the nine-button **coaster
builder** bar (`FUN_00498790`, stream 0x00751530, handler `FUN_004982a0`) which is also **where park
keyboard shortcuts are dispatched** - it was called a "build toolbar" here until 2026-09-14, and the
stream dump shows every button on it is a `cb_*` (coaster) mesh - and the **interaction-mode table** in which camcorder is mode 9 and the
build tools are mode 8. Start there rather than from the exe. Then the simulation itself - **guests, staff, rides operating** - which needs
priority 2 and the **.RSE ride scripts**, and is the whole of what "not playable yet" means. **Writing a
park back (.TPWS)** belongs near there too: until it exists, Restart Park cannot mean what it says.

## Known-broken and must not be lost

- ~~**`Input.Pressed` matches on raw keys with NO modifier exclusion**~~ **FIXED on branch 50 in
  `7b0abf4`** (an older note says `82e9470`; that is a pre-amend hash on no branch) - do not re-fix it. A chord used to fire the plain-key binding too (Ctrl+C was both
  `ClosePark` and `CamcorderMode`; Ctrl+O/H/S/V/P likewise, and all six also fired `Clone`). The
  binding table now splits each `[DefaultKey]` into its modifiers and its keys and compares the held
  modifiers for **equality**, which is what the original does. Detail in [[park-engine-from-exe]]
  under "How a key is matched".
- ~~**The park orbit camera scrolls the wrong way for any yaw but zero.**~~ **FIXED on branch 49 in
  `af157d8`** - do not re-fix it. Its own comment said forward means forward *on the screen*, but the
  basis it built was the mirror of the one its eye offset uses, so at yaw `pi/2` forward scrolled the
  view backwards. **This bullet used to say the fix was "deliberately not bundled" with the camcorder
  work, and that reasoning reversed on purpose:** the bug was only ever *latent* because the rotate
  keys move yaw in multiples of `pi/4`, and camcorder mode is precisely what makes any yaw at all
  reachable - so it stopped being a separable pre-existing fault and became something the camcorder
  commit itself would expose. Bundled, and said so in the commit message.

- **`wadcat` lives at `/home/alex/.cache/tpw-wadcat/bin/Debug/net10.0/wadcat` and is NOT on `PATH`** -
  `which wadcat` finds nothing, which looks like a missing tool and is not one (checked 2026-09-14).
  Its verbs: `--list|--id|--bounds|--meshes|--anim|--field|--heights <wad>...` and `--cat|--dump <suffix>`.
- **`wadcat --meshes` is broken** - reached for every model, sees the right mesh counts, prints nothing,
  in every archive. Not diagnosed. The floor-plate finding is credited to it; that survives on other
  evidence, but re-check anything else sourced to it. **`--anim` now prints node world translations** and
  is the working way to ask where a node sits.
- **Restart Park is a traced departure**: the original restarts in place (exit reason 1 -> 0xd -> 0xe ->
  back to 10, no teardown); OpenTPW reloads the level. Fix when a park can be saved.
- **A toilet's door swings for ever** - the park loops an item's `M` clips as a stand-in for the `.RSE`
  script that should trigger them. Honest cost of having no ride runtime.
- **N1's triangle diagonal** is deferred on purpose - two candidates checked and both wrong; finding it
  means decompiling FUN_0056f670 whole (5,793 bytes, heavily unrolled) for a detail visible only on
  non-planar cells' silhouettes. **Do not guess at it again.**
- **`saves.md` calls version 500 a "F4 01 00 00 magic"** - fix in the save task.

## Decisions owed by Alexah, not by me

- **The loading bar's seeds** (829/918) are deliberately unmaintained, so a fresh install's very first bar
  is approximate. The alternative is shipping a measured file. His call.
- Whether a PR is ever opened upstream. **Never without asking.**
- Whether a park pause hides the advisor or only holds him; whether the 1.5s rest applies to park lines;
  the 0.1s frame clamp against a 2s catch-up; shipping exe-transcribed tables.
- Renaming Ghidra's 0x004092a0 (Game_Pause) and 0x00409300 (Game_Resume) was refused by the permission
  classifier and he has not answered.
- **The original fires its keyboard shortcuts on key RELEASE, and OpenTPW fires them on press.**
  Established 2026-09-14 from the window proc `FUN_0046b600`: WM_KEYDOWN posts 0x1000a into
  `FUN_0040c900`, which reads a handler at `+0x10` that is **null in all 83 rows of all six tables**
  and only sets the row's down-latch; WM_KEYUP posts 0x1000b into `FUN_0040c990`, which reads `+0x0c`,
  where **every handler in the game actually lives**. OpenTPW's 12 `Input.Pressed` call sites all act
  on the press edge. Matching the original would change how every shortcut in the game feels, so it is
  **not being done unilaterally** - see [[park-engine-from-exe]] under "How a key is matched".
  **A claim that used to hang off this was WRONG and is withdrawn**: it said moving to the release
  edge would also settle branch 49's "one frame renders from world origin on every toggle". It would
  not have. That had nothing to do with which edge toggles - see the branch-52 block below for what
  actually caused it.

# >>> END OF PLAN <<<

## >>> STATE: THE PARK'S MANAGEMENT GADGET - branch 54, `6045e6c` + `75727e3` - DONE. PUSHED 2026-09-14. <<<

**`alexah/54-a-park-hud`**, stacked on branch 53 at `5c1543a`. **One commit, `6045e6c`, PUSHED
2026-09-14** at Alexah's word - one named refspec, **no PR** (GitHub offered a compare link and it was
not used), `local == remote` verified, **`main` still 453e779 on local, origin AND upstream**, and
**0 `refs/pull/*`** on the fork. **That yes is spent ([[ask-before-github]]).** Builds **ALONE** in a
throwaway worktree at **127/0** with **143/143 none skipped** (`OPENTPW_GAME_PATH` set).

    alexah/53-say-what-the-game-...          5c1543a  README made true          PUSHED
    alexah/54-a-park-hud                     6045e6c  the management gadget     PUSHED
    alexah/54-a-park-hud                     75727e3  the camcorder viewfinder  PUSHED
    alexah/55-say-a-park-has-an-interface    0f79b5e  README made true again    PUSHED
    alexah/56-the-advisor-in-a-park          1894084  the advisor in a park     PUSHED
    alexah/57-hold-the-advisor-on-screen     2951323  he stays on screen paused PUSHED
    alexah/58-the-gadgets-keys-and-tickets   9cd8b6d  keys and tickets, live    PUSHED  <- HEAD

**BRANCH 55 WAS PUSHED 2026-09-14** at Alexah's word ("Yes, push both"), alongside branch 56, one named
refspec each and **no PR**; that yes is spent ([[ask-before-github]]). This paragraph used to say it was
unpushed and needed a fresh yes, which it did until it got one. It is one README commit, stacked on 54
at `75727e3`, built **ALONE**
in a throwaway worktree at **127/0** with **143/143 none skipped**. It corrects three sentences that
stopped being true when the gadget landed: a park's interface being "a single menu", the gadget's
camera button "does not exist here yet", and "a park has no interface at all - no HUD and no advisor".
The Controls table gains the mouse and its Escape row now says that Escape leaves the ground-level view
before it reaches the menu.

**Branch 54 carries TWO commits**, both built **ALONE** in throwaway worktrees at **127/0** with
**143/143 none skipped**, both pushed 2026-09-14 at Alexah's word, one named refspec each, **no PR**
(GitHub offered a compare link both times and neither was used). **Both yeses are spent
([[ask-before-github]]).** After each push: `local == remote`, **`main` still 453e779 on local, origin
AND upstream**, **0 `refs/pull/*`** on the fork, and no worktree or stray `worktree-*` branch left
behind.

> `3d7837b` was the pre-amend hash and is on no branch. The commit was amended to carry the five
> review fixes and the Escape change, and **re-verified building alone afterwards**, because amending
> changes what the commit contains.

**A park has a HUD now.** `ParkGadget` (`source/OpenTPW/UI/Park/ParkGadget.cs`) is the bottom-left
management gadget, transcribed control by control from the layout stream at **0x00752940** - see
[[park-hud-from-exe]] for the decode. `ParkFrontEnd` builds it and opens it in the window stack, the
way `FrontEnd` opens the lobby's `IslandPanel`, and its ten meshes are preloaded in `ParkFrontEnd
.Meshes` so a park does not hitch.

**VERIFIED IN THE RUNNING GAME, two ways.** A screenshot shows it in the corner with the six buttons
in the original's staggered arrangement. Then `gadgetshot.py` scrolled the park camera between two
frames: **the middle of the screen moved 75%, the gadget's interior moved 0.5%** - the world scrolls,
the HUD does not. **And the date proved itself live in the same pair**: it read `1/1/2000` in the
first frame and `1/2/2000` in the second, a day having turned across a ~4s gap, which is the ~5.7s
game day [[park-engine-from-exe]] records. That is the readout working, not noise.

- **Not modal, `Pauses` left false** - a pausing window here would stop the clock, the calendar,
  the particles and every model animation.
- **The date is live**, from `GameCalendar.Now`, in font slot 3 (DATETINY/DATESMALL/DATEBIG - the
  date's own slot in all four resolution sets).
- **Five of the six buttons say why nothing happens** - there is no buy, hire, information, map or
  finance screen - following `ParkFrontEnd`'s own `NotYet` idiom for menu choices it cannot answer.
  **The sixth, camcorder, works**, and follows the mode rather than keeping its own answer, because
  the C key reaches the same mode without going through the button.
- **The gauge is NOT live and must not be written up as if it were.** Its help row is "Happiness of
  the park visitors" and **there is no happiness value anywhere in the codebase** - every grep hit
  was a `UIStrings` label, never a number - so it rests rather than being given an invented figure.
- **The fold-away arm (0x21/0x24) is deliberately not built.** It exists to uncover the message bar
  and the rest of the interface, none of which is built, so it would fold away nothing. Drawn once
  during development its retract button showed as **a red cross floating clear of the panel** - the
  disabled part of `b_retract` - which is what a control with nothing behind it looks like.
- **Anchoring is automatic and correct**: every gadget control's centre is in the left third and the
  bottom third, so the panel pins bottom-left as one piece, which is the corner the original wants.

> **A HARNESS OF MINE REPORTED THE EXACT OPPOSITE OF THE TRUTH, and the lesson is in
> [[verify-every-ordering]] as a third rule.** `gadgetshot.py` first hid the HUD with `key F2` and
> diffed the corner - but **`key` is not a debug console command**, so both frames were the same
> frame, the difference was 0.00, and it printed "NOTHING DREW THERE" about a gadget that was
> drawing correctly. It also centred the crop when the panel is bottom-left anchored, putting the box
> 160px off. **Two independent errors, either of which alone gives the same false negative.** It now
> scrolls the park camera instead - the middle of the screen must change, the corner must not - and
> prints any command the console refused.

**THE RESIDUE INVARIANT STILL HOLDS - re-measured, not assumed.** An always-open window holding a
`UiText` texture is exactly what could have reopened branch 51's zero-a-cycle result, so
`assetprobe.py` was run again over five scene builds:

    lobby1  845 assets    park1  1474    lobby2  1110    park2  1474    lobby3  1110

**`park1 == park2` and `lobby2 == lobby3`, identical in every column** - still zero a cycle. The
counts sit about **107 above** branch 51's (1366 / 1003) because the gadget's ten meshes are cached
by name in `UiMesh.Loaded` and outlive the scene **by design**, the same way textures are cached by
path; that is also why the *lobby* gains them once a park has been entered, and it is the same +107
the loading bar relearned (940 -> 1047). Growth would have shown as park2 > park1, and it does not.
**VmRSS remains noisy and unexplained** (507/693/905/676/911MB) - unchanged, and still wanting a
managed-heap instrument rather than asset counting.

> **REVIEWED: 66 agents, six lenses, 20 raised and 5 CONFIRMED** (2026-09-14, every agent in its own
> worktree per [[workflow-agents-share-the-tree]]). **Three of the five were corrections to claims I
> had written** - the failure mode earlier passes on this stack caught six times.
>
> 1. **The date-format claim was refuted.** I had written that the original formats through
>    `GetLocaleInfo`, inferred from finding no printf format string. Wrong: `FUN_004a0e30` builds the
>    text with `FUN_006acca0` from **compiled resource 0x1f of the bundle at `DAT_0078ba94`**, handed
>    day/month/year as numbered arguments typed 0x10/0x11/0x12. The exe imports no `GetDateFormat`,
>    and `GetLocaleInfoA/W` appear only in CRT code. **The reviewer's replacement - "UITEXT row 0x1f" -
>    is ALSO unsupported**: row 31 of all 21 shipped .str files was dumped and none is date-shaped. The
>    comment now says only what is known and calls the short date a stand-in.
> 2. **`gauge.md2` was on the wrong control** - it binds to the housing `0x1e`, not the meter `0x1f`.
>    A **decoder** error, not a typo; the binding rule is now in [[park-hud-from-exe]].
> 3. **The date should be black.** Confirmed from disassembly at 0x004a2529 - control 0x20 gets font
>    slot 3 then colour `(0,0,0,0xff)`. **The finding cited the wrong function** (`FUN_004a0ab0`, which
>    colours the *cash* readout by band); the evidence is in `FUN_004a1d70`.
> 4 and 5. **Camcorder mode steers the view from where the pointer IS**, and every gadget control sits
>    in that band at the bottom left, so reaching for a button swept the view about a quarter turn and
>    `Leave()` handed that yaw to the orbit camera. **Fixed by putting the gadget away in first
>    person** - which Alexah's screenshot of the original then confirmed is what it actually does.
>
> **Two lenses found nothing** (lifecycle, input): no teardown leak, and Escape still opens and closes
> the menu with a non-modal window permanently open.

**ESCAPE NOW LEAVES CAMCORDER MODE** before opening the menu. `ParkFrontEnd`'s doc excused this with
"neither HUD panels nor camera modes with something to leave exist yet", which stopped being true on
branch 49. The original's `FUN_00488a00` answers a key-up on **VK_ESCAPE and on action 16 (camcorder)
identically**, falling through to `FUN_0065f11d(1)` - a switch back to UI page 1. **That page change is
on the way OUT of the mode, not into it**, which is why it is absent from `FUN_00481a10` and why I
could not find it there.

**THE GADGET IS PUT AWAY IN FIRST PERSON, and it is measured**: normal against camcorder mode, the
corner differs in **46,053 of 46,060 pixels**, no console command refused, `save/` untouched. Alexah's
screenshot of the original shows the same - an empty bottom-left corner, a viewfinder surround, and the
eject button in the far corner where stream 0x0074fa98 puts it.

**ALL FIVE FINDINGS ARE CLOSED, and the last two by measurement after my own eye got them wrong:**
- **The date IS black.** I looked at the crop, saw white, and nearly recorded the fix as not having
  taken. Sampling the date's text rect instead: ink **rgb(37,82,40)** on a plate of
  **rgb(114,255,107)**. What read as white was the *plate*. **The instrument was my eye, and at a
  188x245 crop it is not one** - see [[verify-every-ordering]].
- **The gauge artwork moved to the housing.** It spans virtual x **60..164**, which is `0x1e`
  (67..163, width 96) and not `0x1f` (85..144, width 59). A first attempt to measure this was
  **saturated and proved nothing** - every column read 100% "dark" because the test rejected the whole
  housing rather than just the artwork; the working version compares against the panel's own body
  colour, `rgb(13,89,15)`.

## >>> THE CAMCORDER'S VIEWFINDER - branch 54, commit `75727e3` <<<

`ParkViewfinder` (`source/OpenTPW/UI/Park/ParkViewfinder.cs`), from stream **0x0074fa98**: the
`f_viewfinder` surround at (170,170)-(1878,1366) and a `b_eject` button at (1916,1404)-(2018,1506)
carrying help 468, *"Left-click to exit camcorder mode (C)"*. `ParkFrontEnd` opens it after the gadget
so it draws over it, and both meshes are preloaded.

**It is the mirror of the gadget**: `Hidden = !Active` here against `Hidden = Active` there, so the two
swap and are never up together. `WindowStack` honours `Hidden` for drawing *and* for the pointer, so
one flag governs both. Verified in the game - the viewfinder's brackets appear, the gadget's corner
goes back to bare park, and **a real XTEST click on the eject button leaves the mode**.

> **THE CROP THAT SHOWED GRASS, AND THE ANCHOR RULE BEHIND IT.** The first capture cropped the eject
> button with the *gadget's* anchor and got a picture of grass 320px away. `VirtualScreen.AnchorFor`
> takes a rect's **centre** against the thirds: the gadget is bottom-**left**, but the eject button's
> centre x is 1967, past 2048*2/3, so it is bottom-**RIGHT**. `gadgetshot.py` now works the anchor out
> per rectangle. **Never assume one anchor for a whole screen** - see [[park-hud-from-exe]].
>
> **AND THAT RUN'S PERCENTAGE TEST WAS WORTHLESS.** Switching modes moves the camera, so every candidate
> crop read 99-100% changed. A metric that saturates everywhere discriminates nothing; the picture
> settled it, and the click was settled by the camera's own height.

Also noted, not a defect: a park has **no `ScreenParticles`**, so hovering a gadget button starts
`ButtonGlint` emitters that tick and can never draw. `SetCount` is bounded and kills them on exit, so
it is waste rather than a leak - revisit when a park gets `ScreenParticles`.

## >>> STATE: THE README MADE TRUE - branch 53, `5c1543a` - **DONE. PUSHED 2026-09-14.** <<<

**`alexah/53-say-what-the-game-does-now`**, stacked on branch 52 at `cd6894b`. **One commit, PUSHED
2026-09-14** - one named refspec, no PR, `local == remote` verified; that yes is spent
([[ask-before-github]]). Builds
**ALONE** at **127/0** with **143/143**.

A **staleness audit** (8 agents, four lenses over the README and every memory file) raised **42 claims
and confirmed 36**. The README's six became this commit:

- **Test counts re-measured both ways, not adjusted**: 143 tests, 62 of which skip without an
  installation - so a fresh clone sees **81 run / 62 skipped**, against the "58 and 57 of 115" it had
  carried since branch 46.
- **Camcorder mode described**, and two claims retired with it: a park's sky being there "for the
  first-person view" as though none existed, and its lightning only ever being a diagonal near the
  ground. Both were true of the orbit camera and were written as though true of the game.
- **The sign format is no longer half-read**: no second variant, all 84 signs read (23 painted / 61
  bare), and the two "unidentified" header regions named as the per-line fill textures. What is
  actually approximated - lettering in a flat colour rather than tiling the fill - is said instead.
- **`F1` dropped from the controls table**: nothing reads `InputButton.Help` and `git log -S` shows
  nothing ever has. `Ctrl+H` takes its place, which has a real consumer.

**The other 30 confirmed claims were memory, and are all fixed** - across this file, the two exe
notes, `MEMORY.md` and eight small memories. The one worth remembering:

> **The audit corrected a correction I had made the same day.** I had recorded a binding row's
> `+0x06` as an "enabled flag". It is not: `FUN_0040c900`/`FUN_0040c990` gate on the **table**
> object's `+0x06` and then merely WRITE the row's field - 1 on key-down, 0 on key-up - and neither
> matcher ever reads it. So a row's `+0x06` is a held latch, and "the original can turn a single
> shortcut on and off" was never supported. Also: `FUN_0040cfa0` covers **five** of the six tables -
> `coaster` is touched by neither it nor `FUN_0040cf60`.

## >>> STATE: A CAMERA PLACED ON ARRIVAL - branch 52, `cd6894b` - **DONE. PUSHED.** <<<

**`alexah/52-place-a-camera-when-it-arrives`**, stacked on branch 51 at `2018166`. **One commit, PUSHED
2026-09-14** - `local == remote`, no PR; that yes is spent ([[ask-before-github]]). Builds
**ALONE** at **127/0**
with **143/143**.

**THE CAUSE WAS NOT WHAT BRANCH 49 GUESSED.** Both park cameras worked out the eye position in
`Update`, and `Camera.SetCameraMode` builds a **fresh instance** with `Activator` - so its `Position`
was the `CameraMode` default and its `Rotation` was identity. The toggle happens *inside* a mode's
`Update`, and `Camera.Update` is:

    CameraMode.Update();      // the OLD mode runs, calls Enter/Leave, and swaps the instance
    CalcViewProjMatrix();     // ...this then builds the matrices from the NEW, unplaced one

So the view matrix, `Audio.SetListener` in `Level.Render`, and everything drawn that frame - sky haze
by camera height, rain by distance, flyer culling by radius - read `(0,0,0)` for one frame on **every
toggle, in both directions**. Branch 49 recorded this as being about `Input.Pressed` re-consuming the
keypress; **that was wrong**, and moving to the release edge would not have touched it.

Each mode now places itself in its constructor as well as in `Update`, through one `Place()`. Safe
because: both `Enter` and `Leave` set their statics **before** the swap; both ground helpers return
`0f` when `ParkGround.Current?.Heightfield` is null; `Level` builds the ground (line 239) long before
it asks for the camera (line 258); and neither placement reads input or `Time.Delta`. **The easing is
unchanged** - each helper takes its first sample outright rather than lerping into it, and a fresh
instance was always going to take that first sample; this only moves it one step earlier.

**Argued from `Camera.Update`'s order, not measured** - one frame is not something a harness can
catch, and that is said in the commit too.

> **A REVIEW CAUGHT A REGRESSION IN THIS VERY COMMIT. BOTH FIXES ARE NOW AMENDED IN** (hence
> `cd6894b`, not `0439f99`), and the amended commit was re-verified building ALONE at 127/0 with
> 143/143, carrying all five files. **The false sentence was removed from the commit message**, not
> left standing beside a fix that contradicts it.
> 9 agents over five lenses, **15 raised, 3 confirmed**. The important one: placing the camera in its
> constructor **spent the one un-eased ground sample at the wrong place**. `Enter()` sets
> `Stand = ParkOrbitCameraMode.PointOfInterest`, the constructor latched `_groundSampled` there, and
> the debug console assigns the real `Stand` only *after* `Enter()` returns - so `camcorder x y` eased
> from the old height, and **paused, `Time.Delta` is 0 so the ease never advances at all**. The commit
> message's own defence ("a fresh instance was always going to take that first sample - this only
> moves it one step earlier") **is false for that path**: moving it earlier changes *which position*
> is sampled.
>
> **WHY MY VERIFICATION COULD NOT HAVE CAUGHT IT, which is the lesson.** `ParkCamcorderCameraMode
> .State()` re-samples the ground at the *new* `Stand`, so the console reports the correct eye height
> while the camera renders from the stale one. "The positions it reports are the same as before" was
> an instrument reading the same state as the thing it was checking. See [[verify-every-ordering]].
>
> **The fix**: constructors now PEEK the ground (a direct read that latches nothing) and leave the one
> outright sample to the first `Update`. A new `groundprobe.py` compares the camcorder's reported
> `eye` against `state`'s `cam=` - two independently computed numbers - at four spots with different
> terrain, paused. **It fails on the old code and passes on the new**: all four now agree to
> `delta=0.00`, including a spot twenty units up that read `cam z=10.0` against `eye=25.0` before.
>
> **That probe then found a SECOND defect, pre-existing and not from this commit**: a repeat
> `camcorder x y` while already Active skips `Enter()`, so no new instance is built, `_groundSampled`
> is already true, and the eased branch cannot advance while paused - the eye stayed at the first
> spot's height for every later spot. Fixed by making the console's two-argument form a **teleport**
> (`StandAt`) that snaps the ground rather than easing to it; walking still eases.
>
> **The reassuring result: the `regression` lens returned an EMPTY list** - no use-after-free and no
> double-free anywhere in branch 51's seven release paths, which is the dangerous class that counting
> assets could never have tested.

## >>> STATE: WHAT A SCENE LEAVES BEHIND - branch 51, `2018166` - **DONE. PUSHED.** <<<

**`alexah/51-name-what-a-scene-leaves-behind`**, stacked on branch 50 at `7b0abf4`. **Six commits,
PUSHED 2026-09-14** - `local == remote`, no PR. That yes is spent ([[ask-before-github]]). Every one builds **ALONE** at
**127/0** with **143/143**.

    3618a30  Say what a scene leaves behind, not just how much      (assets / assets list)
    9afa26f  Tell one pathless asset from another by its size
    19872a0  Name a leaked model by the shader it draws with
    04babbb  Let a sky go of the textures it made
    b7208b4  Chain to the base that lets a model go                 (ParkGround, ParkPaths)
    ebc2557  Let the weather go of the art it drew with
    6ac8821  Let a sign go with the scene that painted it
    2018166  Let the interface go of its particle pools             <- TIP

**THE RESIDUE BRANCH 48 LEFT IS CLOSED: 33 assets and 4 models a cycle, to none.** Five scene builds,
total assets at each:

    before   845  1385  1036  1418  1069      climbing, never once falling
    after    845  1366  1003  1366  1003      park1 == park2, lobby2 == lobby3

**Identical in every column**, not merely level overall - models 482/295, textures 392/403. (`lobby1`
is lower only because a park's path-cached textures have not loaded yet; those are shared by design.)

**AND THIS SEPARATES VmRSS FROM THE RESIDUE, WHICH THE OLD NOTE RAN TOGETHER.** Same run, with
resident memory beside the counts:

    lobby1   845 assets   618 MB
    park1   1366 assets   581 MB
    lobby2  1003 assets   900 MB
    park2   1366 assets   811 MB    <- identical assets to park1, and 230 MB more
    lobby3  1003 assets   905 MB    <- identical assets to lobby2, and 5 MB more

**Asset counts are flat and VmRSS is not**, so VmRSS was never being driven by `Asset.All` - pairing
the two in one open item was the wrong assumption, not just an unproven one. The lobby pair moving 5
MB while the park pair moves 230 suggests allocation timing rather than retention, and **.NET does not
return heap to the OS eagerly**, so RSS is a poor instrument for the question at all.

**So: the residue half of that Priority 3 item is CLOSED; the memory half stays open, and wants a
different instrument** - managed heap (`GC.GetTotalMemory`) or the renderer's own live-resource
counts. **Do not go looking for more leaked assets to explain the memory**; there are none left to
find.

**The four causes, each its own commit:**
- **`ParkGround` and `ParkPaths` overrode `OnDelete` without `base.OnDelete()`**, which is where
  `ModelEntity` releases its model - so the override replaced branch 48's release instead of adding
  to it. **13 of 13 overrides in the tree skip base; only these two were wrong for it**, because
  every other base (`Entity.OnDelete`, `Panel.OnDelete`) is empty. **Do not "fix" the other eleven.**
- **`Sky` had no `OnDelete` at all** - a plain `Entity`, built once per lobby and once per park,
  making four textures each time.
- **Signs and weather art are bound into materials, and a material leaves bound textures alone** on
  purpose, because they are normally cached by path. These are not: they are painted or streamed for
  one scene. Their owners now keep and release them.
- **`ScreenParticles` released only its atlas**, never the model and material behind each layer pool.

## >>> STATE: ONE CHORD, ONE SHORTCUT - branch 50, `7b0abf4` - **DONE. PUSHED.** <<<

**`alexah/50-a-chord-fires-one-shortcut`**, stacked on branch 49 at `af157d8`. **One commit, PUSHED
2026-09-14** - `local == remote`, no PR. That yes is spent ([[ask-before-github]]). Builds **ALONE** in a throwaway
worktree at **127/0** with **138/138** tests, none skipped.

- **The bug:** `[DefaultKey]` was a flat key list, matched by requiring every named key to be down and
  asking nothing about the keys that were *not* named - so a chord satisfied its own binding and every
  shorter binding inside it. Six chords each fired two or three buttons.
- **The rule, from the exe:** `FUN_0040c900`, `FUN_0040c990` and `FUN_0040c870` all test
  `entry.key == key && entry.mod == mods` - **equality, not containment** - which is how the shortcuts
  table carries S and Ctrl+S, and V and Ctrl+V, as different commands.
- **The modifiers are three**, from `FUN_0046b420`: `GetKeyState` of VK_SHIFT/VK_CONTROL/VK_MENU, the
  **combined** virtual keys, so left and right are one modifier and the Windows key is not one. Right
  Ctrl now works where only left was bound - the original's behaviour, not an addition.
- **Non-modifier keys are deliberately NOT held against a binding** - camcorder mode is entered while
  walking, so a "nothing else may be held" rule would be unusable. There is a test for exactly this.
- **Table order and consumption are NOT reproduced** (the original consults six tables and stops at
  the first match). With modifiers compared exactly there is nothing left to resolve, so building it
  would be a speculative abstraction. Recorded in [[park-engine-from-exe]] instead.
- **`Clone` is a choice, marked as one**: it is Ctrl with no key, so it means "Ctrl is down", and it
  stands during Ctrl+C. The original has no `clone` row in any table - it reads the key directly. It
  is also never *pressed* - no key to go down - so `Down` is the question to ask of it.
- **COMPARING MODIFIERS IS ONLY HALF THE RULE.** The original matches inside its key-down handler,
  against the key just pressed; this rebuilds every binding from what is held each frame. Let go of
  Ctrl while C is down and the plain-C binding matches for the first time, which read as a fresh
  press - so **Ctrl+C still entered camcorder mode, on the release**. `Pressed` now also asks that one
  of the binding's own keys is among this frame's key-downs, which is what `WindowStack` had been
  doing by hand for Escape.
- **`Input.ForgetHeldKeys()` is part of this, not a separate tidy-up.** The held set is pruned only by
  key-ups that reach `UpdateFrom`, and two paths pump the window then discard the snapshot
  (`Renderer.DrawLoadingFrame`, every frame of a load; and the minimised branch). A modifier released
  inside one stayed held for the session. That was inert under the old containment rule and **fatal
  under exact matching** - one stuck modifier stops all 34 bindings that name no modifier, the Escape
  menu with them - so this commit is what made it worth fixing.

**Verified in the running game** with real keys at the real window (XTEST, `chordprobe.py`), from a
known camcorder-off baseline each time, **in BOTH release orders**: plain C enters camcorder mode,
neither Ctrl+C does anything, plain C still works afterwards. `save/` unchanged.

> **AN EARLIER VERSION OF THIS BLOCK CLAIMED "Ctrl+C does nothing at all" AND WAS WRONG.** The first
> probe only ever released the letter before the modifier - the order that was never broken. See
> [[verify-every-ordering]].

> **THE REVIEW EARNED ITS KEEP FOR THE THIRD TIME ON THIS STACK.** 12 agents over six lenses, **22
> findings raised, 9 confirmed**, in three groups: the release-order hole (3 findings, one cause), the
> stuck-modifier lockout (3 findings, one cause), and three overclaims of my own - the test docstring
> saying every test failed before the change when 9 of 17 cases would have passed, the commit message
> saying six chords when it was five, and the `Binding` record saying "one or more keys" when Clone
> has none. **Both defects survived a green 138/138 test run AND an in-game probe**; the reviewers
> found them by driving the matcher over two consecutive frames, which nothing in my suite did.
> Three refutations were also right, including one agent reporting the working tree had reverted the
> fix - it had, briefly, because **another agent in the same run was editing the tree** ([[workflow-agents-share-the-tree]]).

**AND THE TESTS WERE PROVED TO DISCRIMINATE**, which "138/138 green" does not show on its own. In a
throwaway worktree the rule was weakened by **one line** - `held == required` back to
`(held & required) == required`, which is exactly the old semantics - and **7 tests went red**: five
of the six chords, plus `AModifierNobodyAskedForStopsABinding` and `CloneIsTheControlKeyBeingDown`.
The pattern confirms the suite is built right: **Ctrl+O does not fail** because no plain-O binding
exists to collide with (it is the control row), `APlainKeyFiresItsOwnShortcutAndNotTheChordOne` does
not fail because a plain key never satisfied a Ctrl binding under either rule, and
`AKeyThatIsNotAModifierDoesNotStopABinding` does not fail because it guards against a *wrong fix* -
one that held non-modifier keys against a binding - rather than against the old rule. **Do this to any
test suite whose value is unclear**: revert the rule, not the API, and see what goes red.

## >>> STATE: CAMCORDER MODE - branch 49, `af157d8` - **DONE. PUSHED.** <<<

**`alexah/49-camcorder-mode`**, stacked on branch 48 at `37df07e`. **Two commits, BOTH PUSHED 2026-09-14** - the
2026-09-14 yes was spent on 46/47/48 and the docs branch, and no new one has been asked for
([[ask-before-github]]). Both build **ALONE** in a throwaway worktree at **127/0** with **121/121**
tests, none skipped.

    7b28de3  Face the way LookAt was asked to, even looking straight back
    af157d8  See a park from the ground, the way camcorder mode does   <- TIP

- **`Rotation.LookAt` was broken for one direction and nothing had ever reached it.** Looking exactly
  opposite `Forward` turns about the up axis, and it built that quaternion with `W = MathF.PI` where a
  half turn needs `W = cos(pi/2) = 0` - about **80 degrees out**. The lobby and orbit cameras always put
  the eye above what they look at, so their direction is never exactly horizontal; a first-person
  camera's is. `RotationTests` now covers it, including the ~4e-8 near miss a quarter turn produces.
- **Camcorder is a second park camera, not a subsystem.** The two hand over inside their own `Update`,
  the way `LobbyCameraMode` takes its keys. `InputButton.CamcorderMode` (C) already existed with nothing
  behind it. State is static (`SetCameraMode` builds a fresh instance) and `Level.Unload` calls
  `Forget()` so a scene cannot leave the next one thinking it is still at ground level.
- **Eye is ground + 5** (the exe's `-5`, applied as `ground - (-5)`; half a cell). **Look is steered by
  where the pointer IS past a dead zone, not by how far it moved** - `+/-0.4` with scale `2.0`, the pair
  that actually go together in the exe. **Pitch is assigned, not accumulated**: the exe writes it with
  no frame delta and hands it to the same argument the orbit branch fills with an angle.
- **A park's sky is finally visible.** Branch 44 built one and nothing could see it; from the ground it
  fills the upper half of the frame.

> **THIS WAS REVIEWED AND THE REVIEW EARNED ITS KEEP.** A 27-agent adversarial pass found **20 confirmed
> findings**, one of them high: **the yaw steering turned the view AWAY from the pointer**. It also
> caught that the `+/-0.4` band had been paired with the `3.5` scale belonging to the `+/-0.2` band -
> two constants the exe never uses together - and that **eight of my own comments claimed more than was
> established**. Verified afterwards in the running game: pointer right takes yaw `0.000 -> -1.950 ->
> -3.030`, and pitch held at the top reads `37.20` twice a second apart, which is the `0.54 * 1.2 rad`
> the mapping predicts. **Two findings were deliberately NOT fixed here, and BOTH have since been
> fixed**: the Ctrl+C modifier collision on branch 50 (`7b0abf4`), and the one-frame-at-the-origin on
> branch 52 (`cd6894b`; `0439f99` was the pre-amend hash and is on no branch) - though not for the reason this branch recorded, see that block.
>
> A **second** pass over the fixed code (17 agents, **11 confirmed**) caught the rest: an orbit-camera
> state leak across parks, and an orbit scroll basis that was the mirror of its own eye placement, so
> forward scrolled the view backwards at a quarter turn - unreachable while yaw only came in multiples
> of `pi/4` from the rotate keys, and reachable the moment a walk could hand it any yaw at all. **Both
> passes' fixes were folded INTO the tip commit, not carried as a follow-up** - the branch had not gone
> out yet, so it could still read as finished work rather than as a repair of itself. The tip was
> amended (hence `af157d8`, not `3475e89`), and **both commits were re-verified building ALONE
> afterwards**, because amending changes what the second commit contains.
>
> **>>> CORRECTED 2026-09-16: THIS SAID "the branch is unpushed" IN THE PRESENT TENSE, AND HAD BEEN
> FALSE FOR WEEKS. <<<** Branch 49 is **PUSHED** - `af157d8` is on `origin/alexah/49-camcorder-mode`.
> **The staleness was not cosmetic:** this sentence is the stated JUSTIFICATION for amending fixes into
> a tip instead of carrying them as follow-up commits, so a later session could have read it as licence
> to amend an already-PUBLISHED branch. **Write push state in the past tense with a date** - see the
> matching rule in [[contribution-branch-layout]], where four such claims had rotted the same way.

## >>> STATE: RELEASE WHAT A SCENE LOADED - branch 48, `37df07e` - **DONE. PUSHED.** <<<

New branch **`alexah/48-release-what-a-scene-loaded`**, stacked on branch 47 at `8da19c7`. **One commit,
PUSHED to origin 2026-09-14** at Alexah's word, one named refspec, no PR, `local == remote` verified.
**That yes is spent ([[ask-before-github]]).** Builds **ALONE** in a
throwaway worktree at **127/0** with 120/120 tests none skipped, no worktree left behind. **THE WARNING
BASELINE IS NOW 127, NOT 128** - `Shader.OnRecompile` is declared nullable, which is what the compiler
had been asking for at that line all along. A future session reading 127 should not take it for a
regression. Full detail under **Priority 3** above.

## >>> STATE: A RIDE'S NAME BOARD - branch 47, `8da19c7` - **DONE. PUSHED.** <<<

New branch **`alexah/47-a-rides-name-board`**, stacked on branch 46 at `8e1f181`. **Two commits, PUSHED
to origin 2026-09-14** at Alexah's word, one named refspec, no PR, `local == remote` verified.
**That yes is spent ([[ask-before-github]]).** Both
build **ALONE** in a throwaway worktree at 128/0 with 120/120 tests none skipped, no worktree left
behind, tree clean apart from the four untracked paths. Full detail under **Priority 4** above.

## >>> STATE: A PARK WITH WEATHER - branch 46, `8e1f181` - **DONE. PUSHED.** <<<

New branch **`alexah/46-a-park-with-weather`**, stacked on branch 45 at `9c3aee4`. **Eight commits,
PUSHED to origin 2026-09-14** at Alexah's word, one named refspec, no PR, `local == remote` verified
([[ask-before-github]]). **Every one verified to build ALONE** in a
throwaway worktree at 128/0:

    0a9c7dc  Give a park a calendar of its own
    ee64c1f  Give a park weather, and let it be heard
    09e5d5e  Make a park's weather something you can measure
    17b3c9a  Take the rain and the bolt out of the lobby
    0736ee4  Let a park's rain fall and its lightning strike
    9dcf4c3  Say that a park has weather, and re-measure the test counts
    8fbd1f4  Correct four comments about what a pause is
    8e1f181  Set a park's rain to the level the mixer says   <- TIP

**README is current**, and its test split is **MEASURED not adjusted**: with no installation visible,
**58 ran / 57 skipped of 115** - cross-checked two ways in one run (the summary line, and an
independent count of distinct skipped test names). With `OPENTPW_GAME_PATH` set, 115/115 none skipped.

> **EVERY COMMIT BUILDS ALONE** in a throwaway worktree, checked - and that check earned its keep:
> `d2b770f` (since amended into `17b3c9a`) **did not compile**, because `git mv` stages a rename using
> the file's INDEXED content and left my class renames sitting unstaged. `RM` in `git status` was
> saying exactly that. **A green working-tree build is NOT evidence that a commit compiles.**

**128 warnings / 0 errors, tests 102 -> 115 none skipped, `save/` byte-identical throughout.** The full
research is in **[[park-weather-from-exe]]** - read that first, not this block.

**What is done:** `Global/GameCalendar.cs` (the date, counted in TICKS so the original's uncorrected
~0.8% drift survives - a day is ~5.71s, so weather turns every ~40 real seconds);
`World/Park/ParkWeather.cs` (quality -> rain/snow/lightning/wind, the 3-day forecast, the ~4Hz weather
tick, thunder that closes in as a storm builds); `ParkAudio` extended with the **global** cat_ambient
category (effect 32 thunder, positional at the bolt's top; effect 33 the rain bed); debug levers
`weather <quality>` and `bolt`, and `state` reporting date/season/quality/drops/storm/snow/forecast.

**IT DRAWS NOW.** `LobbyRain`/`LobbyLightning` became `Rain`/`Lightning` in `World/Weather/` - their own
docs already said the sprite pool and the bolt are engine, and the original agrees, using ONE shared
object (`DAT_008bcbac`) for both scenes. `Lightning` gained a **two-point `Strike`** because a park
picks its own endpoints across the whole map. Rain keeps the lobby's constants **unchanged and
deliberately un-parameterised** - they read correctly, and whether the density suits a park is
**Alexah's judgement, not arithmetic**.

> **MOST OF A PARK'S BOLT IS ABOVE THE SCREEN** - camera at (475,117,94), ~57 degrees down, so the top
> of frame is ~12 degrees BELOW horizontal and only z 0..48 of a 0..300 bolt is ever in shot. Same
> geometry that hides branch 44's sky. A bolt arrives as a **diagonal from a top corner**, not a
> column, and so does the rain. Full detail in [[park-weather-from-exe]].

> **FOUR RUNS WERE LOST TO THE INSTRUMENT, NOT THE CODE.** The rendering was right the whole time.
> **When an instrument reports nothing, establish that it COULD have detected something before
> believing it** - see [[park-weather-from-exe]] under "THE INSTRUMENT LESSON". `boltprobe.py` times
> its own round-trip (7ms against a 420ms bolt) and is the one that worked.

**THE FALSE COMMENTS ARE FIXED - `8fbd1f4`, and the fix corrected MY correction too.** Four of them, in
`GameClock`, `Level`, `UiWindow` and `ParkCameraMode`. The root error was **conflating two globals one
hex digit apart**: `0x00786ba4` = Game+0x3c, the **pause-permission gate** (Ghidra calls it
g_ParkRunning), against `0x00786b84` = Game+0x1c, the **paused flag**. Full settled account in
[[park-weather-from-exe]] under "Corrections this trace forces on branch 45's notes" - including that
**the lobby's game menu never reaches the pause test at all** (the scene-argument branch at 0x0048c83a),
which neither the shipped comment nor my planned correction knew.

**`RainVolume` IS NOW MEASURED: 0.13, not the 0.30 first guessed.** Derived then confirmed, which is
branch 44's own pattern ([[audio-levels-from-measurement]], [[verifying-audio-by-capture]]):

- **Samples:** `RAIN.mp2` pulled from `AmbientHD.sdt` measures **-16.9 dBFS** peaking at full scale;
  the three thunders beside it are -19.9 / -14.7 / -16.1, median **-16.1**. So rain and thunder are
  recorded within a decibel of each other.
- **Anchor:** `LobbyAudio`'s scale - music 0.50, thunder 0.40, one-shots 0.14, a scene's bed **0.13**,
  the global bed 0.07. Rain is a scene's bed.
- **Confirmed from the mixer** (new harness `~/.cache/tpw-harnesses/rainaudio.py`, dry window against
  wet by **power subtraction**, since a park's music plays throughout and the console has no per-bus
  mute): dry -37.0, wet -33.6, **rain alone -36.3 dBFS**, adding 3.4 dB at the heaviest rain. Rate
  measured at **22,186Hz** - 22050 plus reader lag, so the old sdl2-compat 44100 path is NOT back.

> **A METHOD TRAP WORTH KEEPING: gains do not compare across samples of different density.** I
> predicted from the gain ratio that rain would sit BELOW the music; measured, they are level.
> `RAIN.mp2` is a continuous loop peaking at full scale where music has dynamics and gaps, so the same
> RMS falls out of a far lower gain. **Anchor on the CAPTURED LEVEL, never on another layer's gain.**

**Snow rendering is BLOCKED**, not skipped: hallow is the only theme that can snow and ships no saved
park, so there is no way to test it.

> **DRIVEN AND GREEN 2026-09-14** - new harness **`~/.cache/tpw-harnesses/weatherprobe.py`**, all
> fourteen checks pass. The measurements and the traps are in [[park-weather-from-exe]] under "VERIFIED
> BY DRIVING THE GAME". Headlines: **the weather ticks 3.99/s against 4.03 predicted** (the figure that
> says it is paced by the calendar's counter, not by frames or the game tick - on the tick it would run
> **eight times too fast**); the ramp 7.3s against 7.2s; the calendar 4 days in 23s; weather turns on
> its own, quality 50 -> 87 inside a minute. **NEW: `MaxRaindrops` is a cap that can never be reached -
> the heaviest rain is 580 drops, not 600.**
>
> **Two traps recorded there, both of which cost a run:** a forced quality is replaced by the park's own
> forecast within ~40s unless it is made to hold (`DebugQuality` now does); and **the probe's closing
> summary prints nothing because `state()` drains the queue - that is a harness artifact, NOT evidence
> of a quiet run.** Read the log directly.

**A TEST WRITTEN TO CONFIRM SOMETHING OVERTURNED IT, and three recorded facts were wrong.** See
[[park-weather-from-exe]]: weather **IS** per-theme; **hallow SNOWS** (0..25); space never rains; and
the coin flip in `FUN_00512f00` is **not** dead code - hallow's snow and rain overlap at 15..25, which
is exactly what it resolves. No code changed - `ParkBalance` already layers themes - only documentation.

**THREE THINGS DELIBERATELY LEFT, all to be settled by MEASUREMENT not argument:**

1. ~~**`RainVolume` 0.30 is PROVISIONAL**~~ **MEASURED: it is 0.13**, confirmed against a mixer capture (-36.3 dBFS). Old text: and says so where it is declared. Every other level in this game
   was set by capturing the mixer ([[audio-levels-from-measurement]]); this one has not been.
2. **One rain voice where the original starts two.** `mRainSoundHandle` (a named, SAVED field) plus the
   global handle, no dedupe, only the global one ever given a level. The departure first rested on "snow
   is unreachable" - **hallow killed that** - and now rests only on **hallow having no saved park file**,
   so it cannot be played yet. When it can, snow and rain will sound identical here where the original
   distinguishes them.
3. ~~**A KNOWN-FALSE COMMENT IS IN SHIPPED CODE.**~~ **CORRECTED in `8fbd1f4`** ('Correct four comments about what a pause is'), which distinguishes the 0x00786ba4 permission gate from the 0x00786b84 flag and names the flag's single reader. Old text: `Level.PausedByWindow()`'s doc says `Game_Pause` "does
   nothing unless a park is running: it opens with `if ([0x00786ba4] == 1)`", and `GameClock`'s doc says
   "neither loop tests a paused flag". **Both are wrong** - the latch is a re-entrancy guard, and
   `FUN_00486b90` at 0x00486bce reads the paused flag to suppress particle spawning. The *behaviour* is
   verified correct by measurement and stays; the justification needs re-deriving, **in its own commit**.

## >>> STATE: A GAME CLOCK AND A PARK THAT PAUSES - branch 45, `9c3aee4` - **PUSHED** <<<

New branch **`alexah/45-a-game-clock-and-a-park-that-pauses`**, stacked on branch 44 at `e8ec811`.
**Three commits, ALL PUSHED:**

    5fd4a7d  Give the game a clock of its own
    50788d3  Hold a park's own things still while its world is held
    9c3aee4  Say that a park's menu stops it, and re-measure the test counts   <- TIP

**AN ELEVENTH PUSH, 2026-09-14**, at a fresh yes ("Then you may push to my repos"): the branch was
**created** on maexah/OpenTPW at `9c3aee4`, named refspec to the **fork**, **no PR** - GitHub offered
the pull-request link and it was not used. Verified after: local == remote, **0 unpushed** measured
against the explicit `origin/<branch>..HEAD` rather than `@{u}`, `refs/pull/*` **0** on the fork,
`main`/`origin/main`/`upstream/main` all still `453e779`, branch 42 `96134bf`, branch 43 `b70c006`,
branch 44 `e8ec811`. The docs repo had nothing to push (clean, 0 unpushed, 0 PRs) because this work
touched no file format. **That eleventh yes is SPENT - the next push needs another
([[ask-before-github]]).**

**README also re-measured**: the test split without a game installation is **52 run / 50 skip of 102**,
where the text had said 44 of 90. Measured, not adjusted - a green run prints either way.

**Both built ALONE in throwaway worktrees at 128/0 with 102/102 tests, none skipped** (97 -> 102); no
worktree left behind; `save/` byte-identical across every run; park still **932** steps, lobby **829**
(a clock registers no assets).

**This is the plan's Priority 2, and it unblocks weather.** New `Global/GameClock.cs`.

**The mechanism, all read out of the exe.** The original's clock is the global at **0x785970**
(`GameClock_Pause` 0x00402d90, `GameClock_Resume` 0x00402db0) - see [[park-engine-from-exe]] under
"The game clock" for the full field map and the three layers.

- **The beat is 31ms and the odd number is derived, not rounded.** `FUN_00402ea0` sets the step to
  `1000 / rate` in whole ms and the park passes **0x20 = 32** (0x0054f45f), so it **truncates to 31**,
  not 31.25. Both loops then add `0x1f` by hand (lobby 0x0054e780, park 0x0054f4c4). **This reconciles
  the old "31ms" note with the `PUSH 0x20` instruction - they were never in conflict.**
- **Catch-up caps: lobby 0x1f4 = 500ms (0x0054e768), park 0x7d0 = 2000ms (0x0054f493).**
- **Pause is NOTHING BUT A FROZEN CLOCK.** Neither loop tests a paused flag; each steps while it is
  behind, so a frozen clock leaves it nothing to do. Copied exactly: `TicksDue` comes out 0 by itself.
- **Only a park pauses, and this is now PROVEN not inherited.** `Game_Pause` (0x004092a0) opens with
  `if ([0x00786ba4] == 1)`, and `0x786b68 + 0x3c = 0x786ba4` - the guard inside is the very test each
  call site does first. `MessageBox_Open` (0x0047f251) also wants `[0x00f82884] == 0`, no front end.
- **Every call site passes `PUSH 0x0; PUSH 0x0`**, so the quiet flag is always 0: pause holds the
  advisor's voice and sets `DAT_00803ad2`, and **does NOT stop the music**. ParkAudio needed no change.

**TWO FINDINGS THAT WERE MY OWN BUGS, both caught before shipping:**

1. **The catch-up caps must count the UNCLAMPED frame or they can never act.** `Time.Delta` is held to
   0.1s, so a clock fed from it could never see more than 100ms of arrears and neither cap would mean
   anything. **`ParticleSystem`'s own `LongestCatchUp = 0.5f` had therefore never once acted** - a
   documented behaviour that was inert. Hence new **`Time.RawDelta`**.
2. **A scene must not be billed for its own loading.** Clearing arrears does not help: what is owed
   arrives with the NEXT frame. The 8-second load frame would have run a whole capful at once - **64
   ticks in a park where the old code ran 3**. `Rebase()` discards the following frame, which is what
   the original does entering a park (0x0054ed7c). Caught by re-reading my own diff, not by a test.

**What the tick actually drives** (so nothing is over-claimed): **0x00520130 is `Particles_Tick`** - it
is the *entire* body of the lobby's tick loop and the first call in a park's. A park's loop then calls
~14 more (`FUN_005516b0` is the **RSSE** thing engine, `FUN_00475360` is a **clock-driven scheduler**,
and `FUN_0051e790` at 0x0054f870 is branch 44's crowd-driven music level - it sits on this same beat).

**VERIFIED BY DRIVING THE GAME, not by reading code.** New harness
**`~/.cache/tpw-harnesses/pauseprobe.py`**: the debug console's `state` now reports `game=` and
`ticks=`, so a pause is *measured*. All five checks pass - lobby idle ticking, **lobby menu open STILL
ticking** (the original's rule), park ticking, **park menu open at +0 ticks**, park running again after.
**The rate confirms the beat itself: +38/+39 per 1.2s = 31.7-32.5/s against the 31ms step's predicted
32.26/s.**

> **The frame check, and the trap in it.** Two shots of a running park differ over **0.34%** of pixels;
> two of a held park over **0.020%**. That 0.020% is NOT zero and its bounding box sits *inside* the
> running one's, so geometry alone could not clear it. Cropped and looked at: it is **the mouse
> cursor**, drawn by the HUD on frame time, which is meant to keep moving. **Do not read a small
> non-zero difference as "nothing moved" or as a leak - go and look at the pixels.**

## >>> STATE: A PARK'S OWN SKY AND ITS OWN MUSIC - branch 44, `e8ec811` - **PUSHED** <<<

New branch **`alexah/44-a-park-that-feels-like-a-park`**, stacked on `alexah/43-a-loading-bar-that-learns`
at `b70c006`. **Four commits:**

    5650409  Put a park under its own sky
    228c284  Read how many lists an effect picks between instead of guessing
    cd04daf  Give a park its own music
    e8ec811  Say that a park has its own sky and its own music   <- TIP

**A TENTH PUSH, 2026-09-14**, at a fresh yes ("Yes you may push to my repos"): the branch was
**created** on maexah/OpenTPW at `e8ec811`, named refspec to the **fork**, **no PR** - GitHub offered
the pull-request link and it was not used. Verified after: local == remote, **0 unpushed** measured
against the explicit remote ref rather than `@{u}`, `refs/pull/*` **0 on origin** (upstream's 13 are
other people's and were not touched), `main`/`origin/main`/`upstream/main` all still 453e779, branch 42
96134bf, branch 43 b70c006. **That tenth yes is SPENT ([[ask-before-github]]).**
**Every one of the four built ALONE in a throwaway worktree at 128/0** - 93/93 at the sky commit,
97/97 from the grouping fix onwards - and no worktree was left behind.

This is **the plan's Priority 1, two of its three units**. Weather is deliberately NOT done - see below.
`save/` unchanged throughout, apart from the one expected `opentpw.cfg` write as the loading bar learns
the park's new count (932, up from 918).

**What it is.** `Sky` was built for the lobby and said so in constants - `levels/fantasy/sky`, a lobby
centre, height 180, and always a SKYCOLOUR flooded into its first cloud layer. Those are now constructor
parameters whose defaults are the lobby's, so `new Sky()` in the lobby is untouched, and a park builds
one from its own theme at the original's own placement. The whole function map and the exe evidence are
in [[park-engine-from-exe]] under "The sky" - **including a correction: `FUN_00585690` is a setter, not
the loader.**

> **IT IS NOT VISIBLE, and the commit message says so rather than claiming a win.** The park camera's
> pitch is 45-65 against a vertical FOV of 90, so the top of the frame never rises above the horizon,
> and the original's own three park screenshots show no sky either. **Alexah places it in first person
> mode (2026-09-14)** - which does not exist yet. Kept because the original loads a park's sky art too,
> because it takes a lobby assumption out of an engine class, and because it is already correct the
> moment anything lowers that pitch.

**The lobby was the real risk here and it is clean**: still 829 steps, island 1 pixel-identical, and
every other differing pixel a reseeded flyer proven by component shape against a same-build noise floor.
That method is new and is written up in [[verifying-rendering-by-capture]], where it **partly overturns**
that file's own "differencing cannot work in the lobby".

**A park now loads in 932 steps against 918.** Do NOT re-measure the seed - [[keep-loading-steps-current]].

## The sound half - `228c284` and `cd04daf`

**A park plays its theme's music, and that is all it plays.** The original's state 9 registers four
categories (`FUN_0051ec50` at 0x0054ec92), re-applies the group volumes (`Sound_ApplyGroupVolumes`
0x0051bd70 at 0x0054ec9a) and then `FUN_0051e730` at 0x0054ec9f plays **effect 2 of cat_music** - the
only effect that category declares in all four themes. `Level` builds `ParkAudio` in that same order.
Only music is loaded: cat_ambient's effects are emitters placed from the level's **`scape.omp`** (the
`OBJ_` chunk `FUN_00550e00` reads - record count, record size, dispatch on field[0], **type 1 is a
placed sound**), cat_rides wants a ride runtime, cat_speech wants a park advisor. cat_rides alone would
decode 306 samples for nothing audible.

> **THE DEVIATION, and it is deliberate: the original's park music is SILENT in an empty park.**
> `FUN_0051e730` sets the voice's level to 0 on the next line (`FUN_0051bc40( voice, 4, 0 )` - op 4 is
> set-level) and the park loop drives it every pass (`FUN_0051e790` at 0x0054f870) from
> `clamp( things / 2, 0, 100 )` clamped again to 89, counting things that pass one of five type tests
> (`FUN_004c81e0` -> `FUN_004c7fa0` -> `FUN_004fa990`) - people. With no guests, this plays at a fixed
> level instead. **That one line is what has to start asking how many guests there are when they exist.**

**The gain is measured, not chosen: 0.33.** Jungle's 129 arrangements sit at a median **-16.4 dBFS**
against the lobby's park themes at -19.8, so `0.50 * 10^(-3.4/20)` = 0.338 puts park music exactly where
lobby music sits. Captured back out of the mixer (SDL `disk` driver, f32le **22050Hz stereo** - the
game's own rate, not 44.1k) it came to **-36.3 dBFS over 18 sounding seconds**: that target less the
**6 dB of `MasterVolume`, which defaults to 0.5** and sits under every one of these figures. **The -26
dBFS target in `LobbyAudio` is a level BEFORE master volume** - comparing it against a capture is what
made my prediction miss by 10 dB.

**`228c284` was a prerequisite, not a tidy-up.** `SoundCategoryFile.ReadSamples` grouped sample lists by
measuring the gap before each one (`NewEffectGap` 32). **No gap can work** - jungle's ambient runs 64
bytes between two lists of the SAME effect while the smallest gap between DIFFERENT effects is 42. The
count was in the file: **the second int of each 20-byte effect record is the variation count**, verified
by me across **all 31 shipped categories**. Jungle ambient reads `[9,5,7,4,1,0,1,1,1]` = 29, its music
`[6]`. Before the fix jungle's nine ambient effects came out as 26 and **every park ambient effect
played one of effect 177's beasts**; music took one of six lists, so five arrangements were unreachable.
**The lobby is unchanged and that was measured** - 11 byte-identical category log lines - because its
categories declare one variation each. Tests 93 -> 97.

## >>> WEATHER IS NO LONGER DEFERRED - RESEARCHED 2026-09-14, see [[park-weather-from-exe]] <<<

**The deferral is over and its central reason turned out to be WRONG.** The clock exists (branch 45),
and the blocking unknown - how long a game day is - is answered: the original has **no day counter** at
all. It runs an accelerated wall clock ("funny time"), `mFunnySecsPerRealSec` defaults to **15000**, and
a day is 86400/15000 = 5.76s nominal - **~5.714s in fact**, because the counter advances every 8th 31ms
tick (248ms) into a formula that divides by 4 as if it were 250ms, and **nothing compensates**.

> **So `DaysBetweenChanges` 7 is a weather change every ~40 REAL SECONDS.** The old note below says
> building weather would give "a park that never rains and no correct way to observe it". That is
> **false**: jungle's 78 is only the STARTING quality, it is re-rolled every 7 game days from the
> season table, and the whole cycle is observable inside a minute. The deferral was right about the
> clock and wrong about observability.

Also corrected below: **snow is fully implemented**, not absent - it has its own branch, particle mode,
renderer and `snow.tga` in both `generic/weather/` and the low-detail `generic/sweather/`. It is
disabled by data using **-1, which the compiled schema declares as a legal bound** (-1..100), i.e. the
format's own "never" sentinel. And **`mWeather` in the save header is the weather thing's HANDLE**, not
a quality - the quality lives at +0x0c inside the 104-byte model-15 thing.

**THREE CORRECTIONS TO BRANCH 45'S OWN NOTES** are recorded in [[park-weather-from-exe]] and must not be
lost: "neither loop tests a paused flag" is **literally false** (one reader, `FUN_00486b90` at
0x00486bce, suppresses particle spawning while paused); **`0x00786ba4` is a re-entrancy latch, not "park
running"**, so "only a park pauses" does NOT follow from it and needs re-deriving; and there is an
**unmodelled alt-tab gate** at 0x0054f4d4 that skips the simulation body *after* incrementing the tick
counter. The frozen-clock model itself is CONFIRMED four times over and stands.

### The original deferral note, kept for its evidence, partly superseded

Do not build park weather before Priority 2. Read from the shipped balance files: rain needs weather
quality **0..40**, lightning **0..15**, snow is **-1/-1** (never). The seasons average **75, 80, 90, 50**
with tolerances 25, 20, 10, 15 - **no season's normal band reaches lightning at all**, and only season 3
dips into rain. Pacing is in **days** (`DaysBetweenChanges` 7, `DaysOfWarning` 4, `SpeedOfChange` 20), so
it needs a calendar. The weather keys are **identical in all four themes** - weather is not per-theme.
**^ THAT SENTENCE IS FALSE, disproved by a test 2026-09-14.** Every theme restates 26 of the 27 keys
and five differ: **hallow snows** (band 0..25, so snow is NOT unreachable), rains harder (15..45) and
throws more lightning (0..25); **space never rains** (-1/-1). See [[park-weather-from-exe]].
Jungle's shipped park is **quality 78** (thing id 7, model 15, 99 bytes at inflated offset 0x158a3e), so
it starts dry and essentially never storms. Building it now would give a park that never rains and no
correct way to observe it - the same trap the sky turned out to be, caught before spending the effort.

Engine side when it is time: `LobbyRain`, `LobbyLightning` and `WeatherSprites` name no lobby-only type;
only `LobbyWeather` is lobby content. The park's controller is **0x00512880**; `LightningTime` is
milliseconds (a 2000ms bolt against the lobby's 500), `LightningCountdownSpeed`/`From` are
weather-update steps, and the park's bolt geometry differs (`FUN_00512c50`: top y 300, base across the
whole map, lean rand*100-50).

## >>> STATE: THE LOADING BAR LEARNS - `b70c006` - **PUSHED** <<<

New branch **`alexah/43-a-loading-bar-that-learns`**, stacked on `alexah/42-leaving-a-park` at `96134bf`.
Built **ALONE in a throwaway worktree at 128 warnings / 0 errors, 93/93 tests none skipped** (90 -> 93).
**A NINTH PUSH, 2026-09-14**, at a fresh yes ("Fantastic work, yes please push to my repos"): the branch
was **created** on maexah/OpenTPW at `b70c006`, named refspec to the **fork**, **no PR** - GitHub offered
the link and it was not used. Verified after: local == remote, **0 unpushed** against the explicit
`origin/<branch>..HEAD`, `refs/pull/*` **0 on both forks**, `main` 453e779, branch 41 2063da6, branch 42
96134bf, docs repo clean. **That ninth yes is SPENT ([[ask-before-github]]).**

Alexah asked 2026-09-14: *"Can we make the loading bar dynamic instead of having to hardcode it, that way
it works regardless of the situation the screen is used for?"*

**The bar now measures itself.** New `UI/LoadStepCounts.cs` keeps a count per **situation** - the scene
plus whether it has been built before this run: `the-lobby.first`, `the-lobby.again`, `jungle.first`,
`jungle.again`. `LoadingScreen` asks it what to expect and records the truth on dispose. Counts live in
**`save\opentpw.cfg`** beside the display settings; that file already passes over lines it does not know,
so it absorbed this with **no new format**. Keys have spaces replaced with `-` because the file is
space-separated - "the lobby" would have read as two fields and put the count where the key belongs.

**Why two constants could never be right: there are FOUR situations.** Nothing releases what a scene
loaded, so a rebuild registers only what is genuinely new. Lobby **829 cold / 404 again**, park **918
cold / 758 again** - and since branch 42, both rebuild paths are ordinary play (Exit To Lobby, Restart
Park), so the two the constants were wrong about are the ones a player meets.

**PROVEN ACROSS PROCESSES, which is the whole claim.** Run 1, nothing learned: `the-lobby.again` reported
404 against a seeded expectation of **829**, `jungle.again` 758 against **918**. Run 2, a *different
process*: both expected **404** and **758**. And `opentpw.cfg` was **byte-identical between the two
runs**, because it is written only when a count actually moves.

> **DO NOT RE-MEASURE THE CONSTANTS.** `Game.LobbyLoadSteps` (829) and `ParkLoadSteps` (918) are now only
> seeds for a first-ever run on a fresh install, and their own comments say they are unmaintained.
> Letting them drift is the point. See the rewritten [[keep-loading-steps-current]].

**Trap for later: a new `LoadingScreen` call site that passes a raw number brings the whole problem
back.** The constructor is `(what, seedSteps)`, so it is easy to do by accident.

**`save/` is no longer byte-identical on a first run on a new machine** - the counts are written there. It
settles after one run. Checksum rituals should expect `opentpw.cfg` to change once and then stop.

**Deliberately NOT done, both recorded in the commit message.** The bar does not grow its own denominator
when an estimate is low (learning makes that rare, and a first run corrects itself). The counts do not go
through `ApplicationSettingsBase` even though `GamePath` does - **nothing in the project has ever called
`Settings.Default.Save()`**, and per-machine config would not travel with the game folder.

## >>> The previous state: LEAVING A PARK IS DONE (the plan's N4) - `922fe43` + `f2f266c` + `96134bf` - **PUSHED** <<<

New branch **`alexah/42-leaving-a-park`**, stacked on `alexah/41-park-floors-and-queues` at `2063da6`.
**AN EIGHTH PUSH, 2026-09-14**, at a fresh yes ("Great job, yes go ahead and push to my repos please"):
the branch was **created** on maexah/OpenTPW at **`96134bf`**, named refspec to the **fork**, **no PR** -
GitHub offered the pull-request link and it was not used. Verified after: local == remote, **0 unpushed**
measured against the explicit `origin/<branch>..HEAD` rather than `@{u}`, `refs/pull/*` **0 on both
forks**, `main` still 453e779, branch 41 still 2063da6. The docs repo had nothing to push (clean, 0
unpushed) because this work touched no file format. **That eighth yes is SPENT - the next push needs
another ([[ask-before-github]]).**
All three commits built **ALONE in throwaway worktrees at 128 warnings / 0 errors, 90/90 tests none skipped**;
`save/` byte-identical (`eabc27f8...`) across every run; no worktrees left behind.

**What it is.** A park had no way out but the console's `lobby`, and Escape did nothing there because
`SetupParkHud` built a cursor and no `WindowStack`. It now builds the stack and a new
`UI/Park/ParkFrontEnd.cs` - the park's build of the menu the lobby also opens, which is the split the
original already has (GameMenu_Open -> GameMenu_BuildLobby 0x0048c600 or GameMenu_BuildPark 0x0048c150).

**The menu, read out of the exe.** `GameMenu_BuildPark` adds them in this order, ids from the handler
**`FUN_0048b6a0`, which had to be CREATED in Ghidra** - `MenuList_Create(&LAB_0048b6a0, ...)` leaves it
undisassembled, so xrefs miss it and `decompile` refuses the address until a function exists:

    Load 1 | Save 2 | Restart Park 3 | Publish Park 4 | [Go Offline 7, only while online] |
    [Options 5, unless FUN_005b6230 finds an online object busy] | Resume Game 0 |
    Exit To Lobby 6 | Quit Game 8

**`FUN_00485b00`'s first argument is the UIText id and its second the item id** - that is how the mapping
was read: 3,4,10,5,2,6,7,12,8 lines up exactly with `UIStrings`, and every text already existed. Neither
online test can be true here, so Go Offline is left out and Options always shown - **that is what the
original's own tests come to offline, not a simplification of them.**

**Exit To Lobby asks NOTHING first** - case 6 is `FUN_005508b0(2)`, which is nothing but
`DAT_00879088 = param`, then close. State 0xb ends the park and state 1 builds the lobby: exactly the
pair `Game.RequestLobbyReload` already did for the console. Restart and Quit DO ask (UITEXT 11 and 9).

**Restart Park is a DEPARTURE, and traced.** `FUN_0048b5b0` writes exit reason **1**, not 2, and reason 1
routes 10 -> 0xd -> 0xe -> **back into 10**: the original restarts a park **in place, with no teardown**.
What it resets is NOT established - `FUN_005ac5f0` forwards to `FUN_005ac650`, whose decompile is
unrecovered (`unaff_EBX`/`unaff_ESI`), so nothing is claimed. OpenTPW reloads the level instead, which is
indistinguishable only while nothing is written back. **Change it when a park can be saved.**

**A park's menu starts TEN units down where the lobby's starts at five** (`MenuList_AddItem(iVar1 * 2)`,
`iVar1` = 5), so `GameMenu.FirstTop` became a constructor parameter and each caller passes its own.

**BOTH load-step constants re-measured, BOTH right UNCHANGED** ([[keep-loading-steps-current]]):
`ParkLoadSteps` **918** (the lobby has already registered the fonts, sounds and 15 meshes the new stack
wants, so nothing new registers) and `LobbyLoadSteps` **829**, the COLD count. A rebuild costs **404**,
and the standing note says in terms **never to bring the constant into line with a reload** - I was one
step from "fixing" it and the memory stopped me. **NEW, and Alexah's call: that inaccuracy is now
player-visible**, because Exit To Lobby IS a rebuild - the bar fills to about half and waits. Left
measured rather than faked with steps that register nothing; said so in `Game.cs`'s own doc.

**Verified by DRIVING it, not by reading the code.** New harness `~/.cache/tpw-harnesses/menushot.py`
presses keys and clicks through XTEST and screenshots (`shots-menu`, `shots-exit`, `shots-options`):
Escape opens the menu over the park and closes it again; the eight choices read in the right order with
the hover ramp working (Exit To Lobby drawn white under the pointer); **Options opens its screen over a
park**, which is the path that quietens an advisor a park does not have; clicking Exit To Lobby logs
`Park menu: Exit To Lobby` then `Loaded the lobby in 404 steps`. **The click frame caught the LOADING
SCREEN and was dim - the log is what proves the lobby arrived, not the brightness.** Reading that frame
as "the lobby is too dark" would have been the third pixel misreading of this job.

**Restart Park exercised too, both halves** (`shots-restart`, `shots-restart2`): clicking it opens the
game's own box over the still-open menu - "RESTART PARK / Are you sure you want to delete everything in
this park and start again ?", which is UITEXT 11 and not a line of ours - with the tick and cross in the
two lower places and the top one empty, and the menu's other choices still showing around it, which is
where the original leaves them until the box is answered. Ticking it logs `Park menu: Restart Park -
loading jungle again` and the park rebuilds. **Two stacked backdrops dim twice: 128 park -> 66 menu ->
31 box.**

> **A REBUILT PARK COSTS 758 AGAINST ITS COLD 918**, exactly as a rebuilt lobby costs 404 against 829.
> `ParkLoadSteps` **stays 918** - [[keep-loading-steps-current]] forbids matching a constant to a reload.
> **This was MISSED in `922fe43`**, whose note calls the inaccuracy a lobby problem reached by Exit To
> Lobby; Restart Park makes a *park's* own bar wrong the same way. Found by carrying on testing after
> committing, and recorded in a follow-up rather than by amending the commit to look prescient.

## >>> The previous state, still true: N4's floors/placement/queues is DONE and **PUSHED** - `364b822` + `2063da6` <<<

**Alexah's three reported gaps turned out to be one job, exactly as he guessed.** On a NEW branch
`alexah/41-park-floors-and-queues` stacked off `alexah/40-park-paths`. Tests **87 -> 90** with none
skipped, **128 warnings / 0 errors**, `ParkLoadSteps` **885 -> 918** (re-measured, the game agrees with
itself), ground 6,923 -> 6,875 cells drawn, `save/` byte-identical.
**A SEVENTH PUSH, 2026-09-13**, at a fresh yes ("Push to my repos, then I'll manually compact"):
`alexah/41-park-floors-and-queues` was **created** on maexah/OpenTPW at `2063da6`, and
`7cc1abe..cd776e5` went to maexah/OpenTPW.FileFormats `docs/item-footprints`. Named refspecs to the
**forks**, never upstream, **no PR** - GitHub offered the pull-request link for the new branch and it was
not used. Verified after: local == remote on both, **0 unpushed**, `refs/pull/*` **0 on both forks**,
`main` still 453e779, branch 40 still 6cff7e0, docs `master` still 0e8d5d0, PR #1's branch still
d73a627. **That 0-unpushed figure is trustworthy because it was taken against an explicit
`origin/<branch>..HEAD` whose SHA `ls-remote` had just confirmed** - not `@{u}`, which silently reports
nothing for a brand-new branch and produced a false clean bill in an earlier session.
**That seventh yes is SPENT - the next push needs another ([[ask-before-github]]).**

1. **Floors.** Every buildable item opens its model with a **flat floor plate exactly as wide as its
   footprint** - `J_WC` toilet, `wf_floor` fountain, `js_base` staff room, `cn_floor01` drinks shop,
   `jb_floor` belly bounce and litter bin, `jc_base` camera. The save's **mType 4, 9 and 10 are ONE
   family**: 44 cells, exactly the eleven placed objects' footprints, nothing left over. All 44 carry a
   REAL ground index, so `ParkGround` was building grass over them at the same heights and the grass won
   the depth fight. It now skips them - **the same fault the paths had, in a second place.**
2. **Placement.** A rotated item was turned about its **footprint's** middle; it must turn about its
   **ANCHOR CELL's** middle, with the angle **NEGATED**. Forced by the save marking the cells
   independently: staff room anchored (58,16)@90 is marked (58,15)-(59,16), fountain (57,19)@90 marked
   (57,17)-(59,19). The old reading put the fountain on (55,19)-(57,21). The exe agrees - `0x168 - angle`
   in `FUN_005229e0`. **This retires "which way the angle turns is NOT settled" in [[park-data-layout]].**
3. **Queues.** A queue cell's tile index **names a MODEL, not a texture row** - which is why index 5
   could never address a four-row `QueueTex`. Seven one-cell models in the theme's own `queue.wad`, from
   the exe's table at **0x76338c**: 0 quedead, 1 quedead, 2 questra, 3 quebnd2, 4 quebnd1, 5 queend,
   6 quebin1, 7 quebin2. Lost Kingdom's four cells read **5,2,2,3 = end, straight, straight, bend**,
   matching their stored neighbour masks 4/4. New `ParkQueues` entity.

### >>> CORRECTION 2026-09-13, reported by Alexah from the screenshot: THE QUEUE TURNS THE WRONG WAY <<<

He saw two things, and they are one fault: **the bend turned the wrong way off the ride**, and **the
start-of-line torches sat in the middle of the queue instead of at the end that meets the path.**

**The torches are the diagnostic and they settle it.** `queend`'s two flame nodes sit at model z ~ 0.6 -
along ONE edge of its 1x1 plate, which is the cell's NORTH edge unrotated (model z maps to engine y).
That piece is at (49,22) and the path it serves is at (48,22), due WEST. North -> west is a CLOCKWISE
quarter turn. The turn applied in `364b822` sends it EAST, onto the boundary with (50,22) - the middle of
the run, exactly what he saw. A straight is symmetric, which is why only the end and the bend show it.

**I MISREAD THE EXE AND RECORDED THE MISREADING IN THREE PLACES.** `0x168 - angle` is at the **queue's
own call site** (`FUN_005229e0`), so what it says is that **a queue piece turns the OPPOSITE way to a
built item** - it is the DIFFERENCE between the two, not a shared convention. I cited it as corroboration
for the item negation. **The item rule is unaffected and still stands on its own evidence** (the fountain
and staff room footprint cells, which the game's own report confirmed); only the queue was wrong.

    item   turn = Turn(angle)          measured from the marked footprint cells
    queue  turn = Turn(360 - angle)    the exe's own 0x168 - angle, folded 360 -> 0

`ParkObjects.Turn(d)` applies a CLOCKWISE rotation of d. **Pass the same angle to `OriginFor` as to
`Turn`** - for a 1x1 piece the cell covered is the same either way, but the origin is not, so a mismatch
offsets the model.

**The lesson repeats the one above it: asymmetric art is what proves a rotation.** The footprint cells
could never have caught this, because a queue piece is one cell square. The paths were settled the same
way, by stone edging. **When a turn is in doubt, find the asymmetric detail and check where it points.**

**FIXED IN `2063da6` AND VERIFIED ON SCREEN 2026-09-13.** `ParkQueues.TurnOf` now returns `(360 - angle) % 360` and
that same angle goes to **both** `OriginFor` and `Turn` - pass different ones and the piece is turned
about one point and stood at another, which on a 1x1 shows as a piece sitting slightly off its square
rather than in the wrong cell. Screenshots at `~/.cache/tpw-harnesses/shots-queue2/`: the two torches
stand at the **west** end of the run against the north-south path, and the bend's railing wraps its north
and east sides while opening west and south - exactly its stored `0x50` mask - turning into the ride's
steps. 128/0, 90/90, `save/` byte-identical, still 918 steps.

**RE-VERIFIED BY MEASUREMENT 2026-09-14**, after Alexah asked whether these were really finished. I had
begun to doubt the wording ("z ~ 0.6 is the NORTH edge") on the grounds that a 0..10 plate makes z~0.6 the
edge at ZERO - **the doubt was unfounded and the original wording is right.** `queend.MD2`'s torch nodes
measure **flame02 (2.6, 9.3, 0.6)** and **flame03 (7.2, 9.4, 0.6)**: both at z~0.6, spread along x, 9.3
high - two torches along the z~0 edge. Model z maps to engine y, and **+y is SOUTH**, which the data says
independently: the bend at (52,22) carries mask `0x50` whose south link is the ride's cell at **(52,23)**,
a HIGHER y. So the z~0 edge is the cell's **north** edge, north -> west (the path at (48,22)) is a
CLOCKWISE quarter turn, that is `Turn(90)`, and `TurnOf(270)` = 90. **Code, commit message and docs all
stand, now measured instead of read off a pitched camera.**

> **`wadcat --meshes` IS BROKEN - do not trust anything sourced to it.** Probed 2026-09-14: it is reached
> for every `.MD2`, sees exactly the mesh counts `--bounds` sees (`queend` = 4 in both), throws nothing,
> exits 0, and writes **nothing at all** to stdout, in every archive tried. Not diagnosed further. Note
> the floor-plate finding above is attributed to `--meshes`; it survives on other evidence (the render
> fixed, and `--bounds`/`--anim` both show the plates), but **re-check any other claim resting on it**.
> **`--anim` now prints each node's world translation** (`at (x,y,z)`), which is the working way to ask
> where a node sits - it is what settled this.

**Docs corrected: `cd776e5` on OpenTPW.FileFormats `docs/item-footprints`, PUSHED,** on top of
`aa2c7b6`. Both `tct.md` and `saves.md` had repeated the misreading.

**Docs: `aa2c7b6` on OpenTPW.FileFormats `docs/item-footprints` - PUSHED (see the seventh push above).**

**THE METHOD LESSON, and it cost most of this job: make the GAME report its own footprints.** Screenshots
kept giving contradictory answers because at a pitched camera "further north" and "further away" look
identical, and I twice reasoned myself into the wrong conclusion from pixels. `ParkObjects` now logs the
cells each thing covers as it places them, which settled it in one run. **Do that first next time.**

## >>> The previous state, still true: N3 IS DONE - P1, P2 AND P3 - AND PUSHED. The park has its paths. <<<

**P1 landed 2026-09-13 on a NEW branch, `alexah/40-park-paths`** (branch 39 was complete and pushed, and
paths are a new coherent unit - the convention in [[contribution-branch-layout]]). `3579ebd` reads each
of the 16,384 World-block map cells instead of stepping over it - `mType`, `mFlags`, `mNeighbours`,
`mDirection`, `mTileData` - as a 128x128 grid indexed **`y * 128 + x`**. Tests **80 -> 84** with none
skipped, built **ALONE in a throwaway worktree at 128 warnings / 0 errors**, the park loads **872/872**
(unchanged - reading cells registers no asset), `save/` byte-identical. `163e374` on
**OpenTPW.FileFormats** `docs/item-footprints` writes the cell layout into `formats/saves.md`.
**A FIFTH PUSH, 2026-09-13**, at a fresh yes ("Good job, yes, go ahead and push to my repos, please"):
`alexah/40-park-paths` **created** on maexah/OpenTPW at `3579ebd`, and `f1c827e..163e374` on
maexah/OpenTPW.FileFormats `docs/item-footprints`. Each a named refspec, **no PR** - GitHub offered the
pull-request link for the new branch and it was not used. Verified after: local == remote on both,
`refs/pull/*` still 0 on both forks, `main` 453e779 and the docs `master` 0e8d5d0 untouched, branch 39
still 9956df2. That fifth yes is spent.

**A SIXTH PUSH, 2026-09-13**, at a fresh yes ("Great job, yes please push to my repos... Ensure README
and documentation is updated as well and push those updates"): **`3579ebd..6cff7e0`** on maexah/OpenTPW
`alexah/40-park-paths` - `726dea7` the paths drawn and `6cff7e0` the README catching up - and
**`163e374..7cc1abe`** on maexah/OpenTPW.FileFormats `docs/item-footprints`, which adds a new
`formats/tct.md`. Named refspecs, **no PR**. Verified after: local == remote on both, **0 unpushed**,
`refs/pull/*` still 0 on both forks, `main` 453e779 and docs `master` 0e8d5d0 untouched, branch 39 still
9956df2, PR #1's branch `docs/md2-sgn-and-lobby-scripts` untouched at d73a627, and origin now carries
**40** `alexah/*` refs. The tip `6cff7e0` built ALONE at 128/0 with 87/87 before it went.
**That sixth yes is SPENT - the next push needs another ([[ask-before-github]]).**

### >>> P2 IS LARGELY ANSWERED ALREADY, which was not expected <<<

`mTileData` is **three dwords, not one opaque run: tile set, tile index, rotation in degrees.** The set
is **1 on all 78 path cells, 2 on all 4 queue cells and 0 on the other 16,302** - exactly the split the
theme's `.tct` draws between `PathTex` and `QueueTex` - and the rotation is **0, 90, 180 or 270 on every
one of the 16,384 cells** and never an arbitrary angle. So **a path cell carries the tile it draws and
the turn it takes**, and the neighbour-mask-to-tile rule P2 was going to have to derive may not be needed
at all. **CONFIRMED 2026-09-13 and P2 IS EFFECTIVELY DONE**: `Jungle.tct` was read out of `terrain.wad`
and every index cross-checked against the shape its cell's neighbours actually make - straights are
degree 2 and collinear, corners degree 2 and bent, T-junctions degree 3, the one crossroads degree 4
with mask `0x55`, and the avenue's edge tiles carry `0x1f`/`0xf1`, all connections on one side. Full
table and the confirmed bit order are in [[park-data-layout]]. **It is an AREA tileset** (edge/centre/
square), because a walkway can be wider than one cell - the entrance avenue is two. Queue cells still
read an index (5) outside `QueueTex` 0..3, so the queue set is NOT indexed the same way; that is open.

**Deliberately NOT chased: the rule that generated `mNeighbours`** (a sweep tops out at 67/78; the
dissenters involve mType 9, 3 and 10, so mType alone does not decide connectability). **Drawing a saved
park does not need it** - mask, tile and angle are all stored. It is an *editing* problem, for whenever
placing new paths exists.

### >>> P3 IS DONE: `726dea7`, the paths are on screen, and it is PUSHED <<<

`ParkPaths` draws the 78 cells, `TextureTableFile` reads the theme's `.tct` (the only place a path tile
is named), `ParkGround` skips the cells the save gives a tile to, and **`Level` now reads the park file
ONCE** and hands it to the ground, the paths and the objects - `ParkObjects` used to read it privately.
Tests **84 -> 87**, 128/0 built ALONE in a worktree, `save/` untouched.

**`ParkLoadSteps` is 885**, re-measured and the game agrees with itself. Eleven of the thirteen new
steps are the path tiles; the other two arrive with the surface and were not traced.

**The ground MUST skip path cells - this is the load-bearing fact.** A path cell is ordinary drawn
ground, not index 0 (all 82 path/queue cells carry a real ground index; all 66 fixed-approach cells are
0), so without the skip both surfaces are built at identical heights over identical cells and fight for
the same depth. See [[park-data-layout]].

**The quarter turn was verified by the ART, not by argument**, because nothing in the data settles it:
each walkway carries stone edging along its sides, and in the render that edging runs along every
straight and turns at every corner. A turn the wrong way puts it across the path and through its own
junctions. **Repeat that check if paths ever look wrong** rather than reasoning about it again.

**Queues are deliberately NOT drawn** - see the queue note in [[park-data-layout]]. That is P4.

## >>> THAT JOB IS DONE - `364b822`, see the state block at the top. Kept for its evidence. <<<

> *"Queue lines don't show still, and the 'floor' for shops, sideshows, etc. aren't showing. They show
> bare grass below them, while in the original game they did not. The staff room, fountain, and
> bathrooms also are not connecting to the paths as they should. All are probably related somewhat."*

**He is very likely right that they are one job**, and the map cells already say why. The relevant
`mType` values sit on exactly these things:

    mType 4    35 cells   a built object's FOOTPRINT - the cells under a shop, ride or feature
    mType 3     4 cells   the queue
    mType 9     8 cells   on the Belly Bounce's near end, AND on the drinks shop and the litter bin
    mType 10    1 cell    the Belly Bounce's far end

**The key measurement already taken, and it is the obstacle:** footprint cells (mType 4) carry
`mTileData` of `(0, 8, 0)` - **tile set 0**, the same as ordinary ground. So a shop's floor is *not*
drawn by the path tile mechanism, and `ParkPaths` cannot simply be pointed at them. Something else
supplies that surface. **Do not assume it is another tile set** - find it.

**ANSWERED: candidate 1 was right, first try.** `wadcat --meshes` on the items showed every one of them
opens with a flat floor plate as wide as its whole footprint. The obstacle above dissolves with it - a
shop's floor was never a tile at all, which is why tile set 0 on a footprint cell meant nothing was
missing. The ground simply had to stop drawing there.

**ANSWERED: "not connecting to the paths" was two different faults wearing one face.** For the toilets
it was the missing floor - a grass gap between item and path. For the staff room and the fountain it was
that a rotated item was turned about its footprint's middle instead of its anchor cell's, which left
each of them a couple of cells adrift of the path. mType 9 turned out not to mark a connection at all:
it is a footprint cell like 4 and 10.

**ANSWERED: the queue was never blocked on `QueueTex`.** The index names a model, not a texture row.
See the state block at the top and [[park-data-layout]].

**The tools for this job are already built and are OFF the tmpfs, in `~/.cache/tpw-harnesses/`:**

    pathcells.py     every cell's type/flags/neighbours/direction, the mType tally, a named footprint
    pathtopology.py  cross-tabs tile index against the shape a cell's neighbours actually make
    pathrule.py      sweeps member sets x diagonal rules against the stored mNeighbours (tops out 67/78)
    mapcheck.py      the falsifiable test that killed the "2dmap shows the paths" idea
    parkload.py      loads a park headless and prints the game's own loading-step line
    pathshot.py      screenshots the path network - the frames that verified the quarter turn
    field.txt        base.MD2's per-cell texture index and flag word, already dumped

`~/.cache/tpw-wadcat/` gained **`--field`** today (every heightfield cell's texture index and flags,
which is how "a path cell is ordinary drawn ground" was measured) alongside `--list`, `--cat`, `--dump`,
`--meshes`, `--bounds` and `--anim`. **`--meshes` on an item's `.wad` is the cheapest first move for the
missing floors** - it lists each mesh with its bounds, so a floor-shaped mesh at the bottom of
`coconut`, `staff`, `toilet` or `fountain` would show up at once.

## >>> The previous state, still true: the two reported visual bugs are FIXED and everything before P1 is PUSHED <<<

**Alexah reported both after looking at the furnished park (2026-09-13), and both are fixed, tested
and verified on screen.** `209f9c7` - the Jungle Spray's Lion and Elephant faced the wrong way while
the Eagle was right, because `MeshRotator` took a rotation key as an orientation in the MODEL when it
is the orientation inside the PARENT. `6510cdd` - the Belly Bounce stood inside its unhatched egg,
because nothing played the one-shot clip that builds an item. Tests **77 -> 80**, 128 warnings /
0 errors, `save/` untouched. The mechanisms are written up in [[park-data-layout]] under the three new
animation sections - **read those before touching animation again**.

**Still wrong and NOT fixed, same family:** a toilet's door swings open and shut for ever, because the
park loops an item's `M` clips as a stand-in for the `.RSE` script that should trigger them. Nothing
reported it; it is the honest cost of having no ride runtime.

    alexah/39-park-terrain   9956df2   17 commits, ALL PUSHED   <- TIP
    alexah/38-game-path-search  ffa8936   tagged slice-1-lobby
    alexah/35..37                         NeoVeldrid, editor removal, README

**The first ten of those went up earlier that day** - `9a5ea22..a67dc22` on maexah/OpenTPW, at Alexah's word ("Yes, go
ahead and push then continue work"), one named refspec to the **fork** and not upstream, **no PR**, and
local == remote verified at `a67dc22`. **That yes is spent too ([[ask-before-github]]).**

**A SECOND push followed later the same day**, at a fresh yes ("Go ahead and push to my repos and
ensure the README reflects properly"): `1711f1d` (the gate and traffic lights) and `72916e4` (the
README saying what a park now does), plus the docs branch `docs/item-footprints` at `d985199` to
maexah/OpenTPW.FileFormats. **That yes is spent as well - no PR was opened in either repo.**

**A THIRD push, same day**, at a fresh yes ("ensure the README is up to date, then push to my repos"):
`eada774` (the park's objects) and `6d1f125` (the README) to **maexah/OpenTPW**
`alexah/39-park-terrain`, and `b2b4c13` to **maexah/OpenTPW.FileFormats** `docs/item-footprints` - the
save's module map, the World-block walk, the footprint bounding-box correction and the shared-texture
arrangement. Verified after: local == remote on both, 0 unpushed, **no PR on either**, and
`main`/`origin/main`/`upstream/main` all still `453e779`. **That yes is spent too.**

**A FOURTH push, 2026-09-13**, at a fresh yes ("Go ahead and push anything that needs pushed to my
repos"): `209f9c7`, `6510cdd` and `9956df2` to **maexah/OpenTPW** `alexah/39-park-terrain`
(`6d1f125..9956df2`), and `f1c827e` to **maexah/OpenTPW.FileFormats** `docs/item-footprints`
(`b2b4c13..f1c827e`) - the two visual-bug fixes Alexah reported, the README catching up with them, and
a new `.md2` format page. Each commit built ALONE in a throwaway worktree at 128/0 with 80/80 tests
before it went up. **That yes is spent as well - no PR was opened in either repo.**

**Every commit builds ALONE in a throwaway worktree at 128 warnings / 0 errors**, tests **87/87 with 0
skipped** (64 before the save tests, 68 before the fixed-item tests, 71 before the objects, 77 before
the two bug fixes, 80 before the map cells, 84 before the paths), `save/` byte-identical after every
run. **Without `OPENTPW_GAME_PATH` 43 of the 87 SKIP and the run still prints a green "Passed! 87
total"** - always set it, or the park tests silently do not run.
`main`, `origin/main` and `upstream/main` all still **453e779**, checked individually after the push. `upstream/main` and fork `main` are both still
**453e779**; **no PR anywhere** (`refs/pull/*` is 0). **All FOUR push yeses are SPENT - the next one needs a
fresh yes** ([[ask-before-github]]). Never a PR to OpenTPW's repos without asking.

**The plan artifact** (update with `url`, never publish a second):
https://claude.ai/code/artifact/0c077c91-6835-4016-9efe-1313fab7994d

    57d7112  M1    Lost Kingdom's scenery on screen - 272 meshes, no new format
    922c4d9  M2    the ground, from the heightfield block inside base.MD2
    9460ff9  M3+4  balance files merged (380 keys), theme fog and sun, + the normals bug
    d36e515  M5    the camera rides the ground beneath it
    9541362  M6    the attribute map, pinned by tests (59 -> 64)
    65b6536  M7    the ground drawn with the park's own textures
    9a5ea22  M8    "Enter this park" works, verified by clicking it with XTEST
    3ac3dc4  N0    the park FOV settled as VERTICAL 90 - comment only, no behaviour change
    e99fb60  N1a   each cell's ground texture laid the way its flags say (mirror + rotation)
    a67dc22  N2a   the shipped park file reads instead of being rejected, + 4 tests (64 -> 68)
    1711f1d  N2b   the gate and the traffic lights stand in the park, + 3 tests (68 -> 71)
    72916e4        README: what a park now does, Map Data is implemented, test counts re-measured
    eada774  N2c  the park's own objects stand where its save puts them, + 6 tests (71 -> 77)
    6d1f125        README: what a park is furnished with; test counts re-measured (44 ran, 33 skipped of 77)
    209f9c7        a rotation key is the orientation INSIDE THE PARENT, + 2 tests (77 -> 79)
    6510cdd        an item is posed at the end of its construction clip, + 1 test (79 -> 80)
    9956df2        README: what a built item looks like; counts re-measured (44 ran, 36 skipped of 80)

### >>> N2 IS DONE. The walk is ported and the park is furnished. <<<

**`ParkWorld.cs` IS the spec now - read it, do not re-derive any of this.** The whole World block
parses: ActionRec (8 + recording) -> header (26 fields, 64 bytes) -> mObjectControls[150] x 32 ->
mNumObjectControls 4 -> mPreviousSearchKey 2 -> 32 x 20-byte records -> a 76-byte arrival/clock tail ->
**16,384 map cells, each measured by its status byte and SKIPPED** - that byte gates three optional
sub-records, one bit each (map 52, track 31, effects 10), which in this park gives 3 -> 84 bytes and
7 -> 94; they sum to 1,378,756, matching the region exactly -> `Used Thing Head` -> 42 things -> `DLRW`. **It lands on the
trailer to the byte**, and the id chain is an exact permutation of 1..42 ending on a null terminator.
The map is stepped over rather than decoded, which is the whole reason the reader is short.

**CORRECTION - it is ELEVEN placed objects, not twelve.** This file said twelve while its own list
enumerated eleven; the data says eleven (3 toilets, fountain, staff room, 2 cameras, litter bin, drinks
shop, jungle spray, belly bounce). Bus/Gates/Lights are the three unplaced sentinels, 14 objects total.

**CORRECTION - an item's footprint is the BOX its `Info.Shape` is drawn in, not the cells marked in
it.** 4x4rock draws 14 stars in a 4x4 box, 5x5rck 23 in 5x5, `ground` draws none at all - and every
jungle item's `.hmp` says width x height. Gates is the one exception and uses its
`EngineFootprintWidth/HeightOverride` (6x3 = its 18 cells); it is the ONLY jungle item with overrides.

**Placement:** an item is authored with its footprint's CORNER at its own origin (toilet floor 0..10,
fountain 0..30, staff room 0..20) - read from node **WorldTransform**, never bounds. Rotation is about
the footprint's middle. **Which way the angle turns is NOT settled** and nothing can settle it here: the
attribute map marks only the entrance, roads and booths, so every object cell reads 0. Every rotated
item in this park is square, so the choice cannot move one off its ground.

**NEW, and it matters for every item: items ship only their OWN art and take the rest from the theme's
`sharetex.wad`.** Without that fallback 40 distinct textures were missing and the park was
sand-coloured boxes; 68 failures -> 1 (the pre-existing lobby `signgrab`). `LobbyModel` now takes an
optional second directory; the lobby passes none. It also DEDUPLICATED 4 textures named by two items
each, which is why `ParkLoadSteps` went 876 -> **872** (measured, matches).

**KNOWN GAP, diagnosed, not guessed: a ride's name board does not paint.** `SignFile` reads the layout
the gates and lobby islands use (header to `0x43DD` = 17,373); every ride `.sgn` is **17,337 bytes, 36
short**, so it is refused with "too short to hold a 17373-byte header". It is a second variant - the
flags at offsets 4 and 8 read 0,1 on a gate and 1,0 on a ride - and 36 is not a whole number of any
record in the header, so it needs the original's loader read. The painting code is in place and correct
for that day. Until then the Belly Bounce wears the shipped magenta "SIGN1/SIGN2" placeholder.

**WHAT IS NEXT - N3, PATHS.** Agreed with Alexah 2026-09-13 and planned out in the N3 section below,
where the research is already done. After it: N4 leaving a park, N5 the park's HUD/advisor/weather/
sound, N1's triangle diagonal (deferred on purpose), the ride-sign `.sgn` variant, and the toilet door
that swings for ever. **P1 is done and pushed** (see the state block at the top), as is everything
before it, and **the next push needs a fresh yes** ([[ask-before-github]]).

**The formats are settled - do NOT re-derive them.** [[park-data-layout]] (measured from the game's own
files) and [[park-engine-from-exe]] (a six-agent Ghidra pass, 7/7, plus an adversarial cross-check).

## Traps that cost real time - all recorded in the two park memories

Generated normals must be handed over **with Y and Z already exchanged** (the shader swaps them for
.md2 geometry; engine-space normals light flat ground as a wall, and it is invisible in a build).
`Time.SmoothingFactor` is `1-exp(-rate*Delta)`, so the console's `pause` **freezes every ease** - move
while running, settle, then pause. Correcting `ParkLoadSteps` must change **the constant**, not only its
doc comment. **Index 0 means "something covers this cell"** - never draw it as ground. Normalise a theme
name **at the way in** (`Game.RequestParkLoad`), because the loading screen is captioned before a Level
exists. **Leave the capture harness's log checks case-sensitive** - they caught that twice.

**The park FOV convention is SETTLED (2026-09-13): the 90 is VERTICAL and the code was already right.**
Settled from the exe's culling frustum, not from the three screenshots Alexah supplied - those turn out
to be incapable of deciding it, which was itself proven rather than assumed. See N0 and
[[park-engine-from-exe]]. **Do not reopen this.**

## >>> NEXT STEPS, in order. Agreed with Alexah 2026-09-13. <<<

### N0. Settle the park FOV. ***DONE 2026-09-13 - the answer is "no change". Do not reopen it.***

**The 90 is VERTICAL, so `ParkOrbitCameraMode` was already correct and no code changed** beyond its
comment, which now carries the derivation. It was settled from the **exe**, not the screenshots: the
culling frustum has to frame what the matrix draws or geometry would pop at the screen edge, and
FUN_0056bb00 builds its corner rays `(+/-1.333, +/-1.0, 1)` at 4:3 -> vfov 90, hfov 106.26. The one
inferred link (`DAT_008bcbcc` = 0.75, written at runtime through a struct pointer) and its
corroboration are in [[park-engine-from-exe]].

**The screenshots cannot answer it, and that was proven, not assumed.** Four estimators were built and
every one was gated against a control frame OpenTPW rendered at a *known* angle. Two answered
confidently and wrongly; the third was right only from a clean 3-line seed and returned a wrong answer
at **100% bootstrap** from a 2-line or slightly contaminated one; the fourth, a seedless joint search,
is honest and says **undecided** - 1-5% separation between candidates that differ by 33% in focal
length. The qualitative "no sky in the original" signal is **withdrawn**: that is content (their parks
are full of rides and trees, ours is bare ground), not lens.

**The lasting lesson, and it cost most of a session: build a control with a known answer BEFORE
trusting any measurement pipeline.** It caught all four failures; without it the first confident wrong
answer would have shipped. See [[verifying-rendering-by-capture]].

### N1. Cell UVs - finish the ground. Small. Needs a narrow Ghidra pass.

Each cell is mapped corner to corner today, so the art tiles once a cell and every cell faces the same
way. The original carries **rotation in 0x08/0x10/0x20 and a mirror in 0x40**, and mirror is set on
**83% of jungle's cells**, so it is not skippable. Flag words actually present in jungle: `0x1, 0x2,
0x4, 0x42, 0x44, 0x62, 0x64`.

**STARTED 2026-09-13, and the first finding corrects a recorded belief.** `0x0800` is missing from every
shipped park because **the loader computes it** - `FUN_004504c0` builds each cell's two triangle normals
from its four corner heights and sets `0x0800` when they diverge by more than **0.81 degrees**
(threshold 0.99990), meaning "not planar"; the renderer then chooses the diagonal from `0x0004`. So
OpenTPW must derive the bit at load too. Full mechanism, constants and the independent confirmation of
the heightfield header from `FUN_00450950` are in [[park-engine-from-exe]].

**The UV rule is FOUND: `FUN_0056f4f0`**, twelve lines, and it is written into `ParkGround.PermuteCorners`.
It permutes an array that starts as the **identity 0,1,2,3** (the call site writes `0x03020100` onto the
stack right before calling), so what moves is *which corner supplies which texture coordinate*. Mirror
(`0x40`) is a **diagonal** reflection - swap entries 1 and 3 - and is applied **before** the rotation;
`0x38` is one three-bit rotation field giving one/two/three quarter turns. Full detail in
[[park-engine-from-exe]]. The call site also does `TEST byte ptr [EAX],0x1 / JNZ` - independent proof
that flag-0x1 cells are skipped, which is what `ParkGround` already did.

**Correction to the old "done when": this will NOT make the ground stop looking like a uniform grid.**
Counted across all four parks 2026-09-13, mirror is set on **98.4-100% of drawn cells** and rotation is
0 degrees on **98.5-99.9%** (jungle 6886/7001 mirrored, 104 rotated at 270; every rotated cell is also
mirrored). So the net effect is a near-uniform diagonal transpose of every cell, not per-cell variety -
the variety comes from the texture index, which already works. Judge this change by correctness, not by
how different the ground looks.

**One thing is NOT settled and is marked at the site:** which corner the original calls 0 and which way
its ring runs. The permutation is exact, but a different start corner or winding would turn a quarter
turn into three quarters. That affects ~100 cells per park and no mirrors, so the dominant effect is
right either way.

**Verified by render, and the check was worth doing.** Same camera before and after
(`capture_control.py jungle 475 425 110`, one turn), park frames being deterministic with the clock
paused. 44% of pixels changed appreciably, spread over every band and the full width - and the changed
pixels land on the **ground cells only**: the river, its stone banks, the waterfall, the rock ledges,
the palms and the sky are untouched, which is exactly right because index-0 cells are drawn by the
scenery and their UVs never moved. Tools: `scratchpad/fov/compare.py`, which writes the changed pixels
as red over a dimmed copy.

**Remaining for N1: the triangle diagonal - DEFERRED on purpose, and do not guess at it again.**
`0x0800` is computed at load (above), but *which* bit then picks the diagonal is **not known**. Two
candidates have been checked and both are wrong: `0x0004` (the original RE note, unsupported) and
`0x0400` (which is a **darken** bit - see [[park-engine-from-exe]] for the traced proof). Finding it
means decompiling `FUN_0056f670` whole, 5793 bytes and heavily unrolled, for a detail that only shows
on non-planar cells' silhouettes. **That is a poor trade against N2**, so it is parked with its anchors
recorded, and `ParkGround` splits every quad the same way and says so at the site.

### N2. Park features from the save - this is where the gate comes from. Large; research first.

**The two holes either side of the gate are where the park gate goes** (Alexah, from screenshots of the
original). So index 0 means "covered", by *either* geometry already in base.MD2 (river, paths) *or* **a
feature placed from the park save**, which nothing loads yet.

Decode the inflated TPWI payload: 1,608,309 bytes, leading LE dwords `0, 1171, 231`, **119 `tButton`
records at a clean 0x140 (320-byte) stride from 0x16e989**, and plain feature paths from ~0x185bf9
(`data\levels\jungle\Features\gates\`, `\Features\bus\`, `\Shops\coconut\`, `\Rides\bouncy\`, ...).
Container already proven byte-for-byte: **1549-byte preamble, `BILZ` at 0x60D, 28-byte header, zlib at
0x629**, and `1549 + 36930 = 38479` is the file length exactly.

**STARTED 2026-09-13. `SaveReader` is fixed and the container is proven end to end** - it really does
inflate to 1,608,309 bytes, matching its own header dword. The three recorded defects were re-measured
against the shipped file and **one of them was not real**; see [[park-engine-from-exe]] for the byte
level. What was actually wrong: the version check (400 vs 500, and it is a version not a magic), and
the copyright being **UTF-16** rather than single bytes. The type at 0x604 and version byte at 0x608
were already correct. Only ONE file of this shape ships (jungle's, version 400), so 500 is an untested
allowance.

**The feature catalogue decodes.** Strings are `u32 length INCLUDING the terminator` then the bytes,
and a record runs `[u32 nameLen][names][16 zero bytes][u32 pathLen][path]["OBJ " tag]`. The name field
can hold two NUL-terminated strings - `"Bus\0Traffic Lights\0"`. Fifteen paths sit at 0x185bf9-0x1886ad:
**Gates**, Bus/Traffic Lights, Fountain Feature, Small Toilet x3, Staff Room, Security Camera x2,
Litter Bin, Coconut Kiosk, Traffic Lights, a bouncy ride, a junspray sideshow, plus music/sound/speech
category paths that are not features.

**But the catalogue does NOT carry positions, and two hypotheses for where they are have been
refuted.** (1) Packed cell ids before the name block: the dwords there are 0, 1, 2 or 4 - only the bus
had 6366 and it does not generalise. (2) Differencing the three identical `Small Toilet` records to
isolate placement: the early differing fields are a descending instance counter (12, 11, 10) and
**stale runtime heap pointers saved verbatim** (`0x04ce86f4` and the like - which is why only 3 of the
4 bytes differ). Also note **the records are variable length**, so fixed-offset differencing runs off
the end into the next record; anything past about +290 in such a diff is noise. There is no `47,17`
pair anywhere in the payload.

**RETRACTED: "the payload is not a tagged-chunk format" was WRONG.** It IS tagged, and I rejected the
idea for an avoidable reason - **the tags are little-endian dwords, so they read BACKWARDS in a byte
dump**, and I dismissed them as single-occurrence noise when one-per-module is exactly what a chunk
table looks like. The `DLRW` at 0x16d1a6 that I called "one string the detector slid across" is
literally the **`WRLD` trailer**. If a four-letter run looks like gibberish, reverse it.

(4) *A 12-byte-per-tile array* remains unsupported, and that refutation stands: byte-equality at a
fixed stride returns ~83% for **every** stride from 4 to 68 because the payload is mostly zeros, so the
test has no discriminating power - do not repeat it. Segmenting by long zero runs gives four regions
(2,832 / 1,489,515 / 99,214 / 12,811 bytes) and none is 128*128*N.

## >>> THE PAYLOAD IS SOLVED: 17 modules, each with a checked trailer tag <<<

`FUN_00415270` is **the park save loader**, and it reads the payload as a fixed sequence of module
blocks. After each module's bytes it reads a dword and **checks it against that module's tag** - which
is precisely what detects "the SAD_X module did not load the same number of bytes that it saved".
Tags are little-endian dwords (so reversed in a dump) and the table of all 17 sits at **0x006fd8b8**.
**Every tag was then found in the shipped file, in this exact order** - the structure is confirmed
against real bytes, not just read off the code:

    tag   module            trailer at    block bytes   load function
    WRLD  World             0x0016d1a6       1495462    FUN_005179c0   <- the map, the bulk
    SPSC  Sprite scripts    0x0016e6f6          5452    FUN_00475730
    PART  Particles         0x001810d6         76252    FUN_0051f7a0
    MESS  Message centre    0x001811e4           266    FUN_0040fd60
    CLOK  Clock             0x001811f0             8    FUN_00402e30
    VANT  Vanilla time      0x001811f8             4    FUN_00403260
    GSYS  Game system       0x00181220            36    FUN_005506e0
    RSYS  Ride system       0x0018569a         17526    FUN_004647a0
    TRAK  Track rides       0x001856ca            44    FUN_00543560
    FLYR  Flying rides      0x00185892           452    FUN_0055e020
    RSSE  RSSE scripts      0x001882fe         10856    FUN_005597a0   <- the feature catalogue
    KAME  Camera            0x0018832a            40    FUN_0042cec0
    COAS  Coasters          0x0018833e            16    FUN_00437b60
    ADVS  Advisor           0x001884b6           372    FUN_00599d00
    SOUN  Sound             0x00188a5f          1445    FUN_0051c400
    CHTS  Cheat             0x00188a65             2    FUN_00405570
    ADSC  Advisor scoring   0x00188a6d             4    FUN_0059c860
    (then 4 trailing bytes - the UI block, which has no tag)

An ActionRec block comes FIRST, before World, and carries no tag either.

**Correction to an earlier note in this file: the feature catalogue is in RSSE, not World.** The paths
at 0x185bf9-0x1886ad sit inside the RSSE block (0x185896..0x1882fe), and the exe writes the `OBJ ` tag
inside `FUN_005597a0`, which is RSSE's own loader. So **RSSE holds the object records that name the
gate, and World holds the map** - placement is far likelier to be in World.

### The World block's header, field by field - and it has an `mParkGates`

`FUN_005179c0` reads 29 named fields in a fixed order before anything else. **The names are debug
labels only**: they are passed to `FUN_00416e10`, which in the release build is `return 0;`, so they
never reach the file and the header cannot be found by searching for them. Read them in THIS order,
sizes in bytes:

    version 4 | mArrivalVehicle_Size1 2 | _Size2 2 | _Size3 2 | mBankAccount 2 |
    mCurrentArrivalVehicle 2 | mGameTick 4 | mMechanicHQ 2 | mParkAnalyser 2 | mParkClosed 4 |
    mNumberOfVisitorsToDate 4 | **mParkGates 2** | mTrafficLights 2 | mRandomSeed 4 |
    mResearchLab 2 | mStaffHQ 2 | mTagSystem 2 | mUIMsgReceiver 2 | mWeather 2 | mWorldState 4 |
    mFirstHandyman 2 | mFirstMechanic 2 | mFirstEntertainer 2 | mFirstGuard 2 | mFirstResearcher 2 |
    mFirstObject 2 | Used_Thing_Head 4 | Used_Thing_Next 4 | thingmodel 4

Each also has a destination offset in the world struct, which is worth keeping because it identifies
the same fields elsewhere: **mParkGates is a u16 at struct +0x1da732**, mTrafficLights +0x1da734,
mRandomSeed +0x1da708, mGameTick +0x1da70c, mParkClosed +0x1da710, mNumberOfVisitorsToDate +0x1da714,
mStaffHQ +0x1da718, mMechanicHQ +0x1da71a, mTagSystem +0x1da71e, mParkAnalyser +0x1da720,
mResearchLab +0x1da722, mBankAccount +0x1da726, mCurrentArrivalVehicle +0x1da72a,
mArrivalVehicle_Size1/2/3 +0x1da72c/2e/30.

`mFirstObject` plus `Used_Thing_Head` / `Used_Thing_Next` / `thingmodel` confirm from the exe what the
stale heap pointers already hinted: **the park's objects are a linked list of "things"**, each with a
model number ("Unknown thing model number loaded" is its own error). That list, not the RSSE catalogue,
is where a placed gate must live.

**NOT YET DONE, and do not assume otherwise: the World header has NOT been located in the file.**
Decoding it at payload offset 0 gives `mParkGates = 0` and almost every field 0 - that is an
**artifact**, not a reading. The World block does **not** start at offset 0: the untagged **ActionRec**
block is loaded first (`FUN_00403780`) and its length is still unknown, so World begins somewhere after
it. The leading dwords (0, 1171, 231) belong to ActionRec, not to `mArrivalVehicle_Size1/3`.
**ActionRec is now decoded (`FUN_00403780`), so the World block's start is DERIVED, not guessed:**

    u32  mLoadedPublishedPark
    u32  recording_size
    byte[recording_size]  recording        -> the block is 8 + recording_size bytes

In the shipped jungle park that reads `0` and **1171**, so ActionRec occupies 0..1178 and **the World
block starts at 1179 (0x49B)**. This is worth more than the earlier guesswork because the structure
*predicted* that the dword at offset 4 would be a byte count and 1171 is what is actually there - a
check that could have failed and did not.

### >>> THE WORLD HEADER IS DECODED, AND `mParkGates` = 11 <<<

Read at the derived 0x49B it comes out clean, and **23 of the 29 fields are non-zero** - the spread
that the two failed tests above never had:

    version 2 | mArrivalVehicle_Size1 15 | mBankAccount 8 | mGameTick 755 | mMechanicHQ 2 |
    mParkAnalyser 5 | mParkClosed 0 | mNumberOfVisitorsToDate 0 | **mParkGates 11** |
    mTrafficLights 12 | mRandomSeed 1061252026 | mResearchLab 6 | mStaffHQ 1 | mTagSystem 4 |
    mUIMsgReceiver 9 | mWeather 7 | mWorldState 0 | mFirstHandyman 25 | mFirstMechanic 26 |
    mFirstEntertainer 27 | mFirstGuard 28 | mFirstResearcher 30 | mFirstObject 15 |
    Used_Thing_Head 1100 | Used_Thing_Next 500 | thingmodel 5

**Why this one is trustworthy where the guesses were not.** `mRandomSeed` holds a genuinely large
pseudorandom value, which a misaligned read does not produce; `mParkClosed` and
`mNumberOfVisitorsToDate` are 0, correct for an unplayed template; and the five staff heads are
**consecutive integers 25, 26, 27, 28, 30**, which alignment by luck essentially cannot manufacture.
The header ends at **0x4E7**, then 4 zero bytes, then more structure at 0x4EB
(`1, 2, 1, 15, 1140, 2000, 5`).

**These u16 fields are OBJECT IDS, not positions.** So **the gate is object id 11**, and `mFirstObject`
= 15 heads the object list. The remaining job for N2 is the thing/object table those ids index, which
is what carries the placement.

**Two corrections to the header, from brace depth in the decompile.** The `while(true)` thing loop
opens at depth 28. `Used_Thing_Head` is read at depth **27 - in the header**, but `Used_Thing_Next`
and `thingmodel` are at depth **29 - INSIDE the loop**. So the header is 27 fields ending at
**0x4DF**, and each iteration begins `[u32 Used_Thing_Next][u32 thingmodel]` - an **8-byte prefix per
thing, not 4**. On that reading the first thing's model is **5**.

### The thing stream: what it is NOT, and what it is

**REFUTED: the malloc sizes are not the on-disk record sizes.** Walking the stream as
`[u32 model][alloc bytes]` with the switch's constants (556, 1104, 544, 544, 536, 536, 536, 104, 5872)
gets **exactly one thing** before hitting an invalid model number, from every one of 128 candidate
starts. That test had real power - a correct stride would have walked hundreds - so this is a clean
refutation, not an inconclusive result. Those constants are in-memory struct sizes only.

**A thing is a list of individually NAMED fields, not a flat struct.** `FUN_004fb530` (model 1, a
guest) reads each field through a size-specific helper, and its write branch states every size
outright:

    FUN_004fae10 -> 4 bytes | FUN_004d37e0 -> 2 bytes | FUN_00424c20 -> 1 byte
    plus direct FUN_005f5e20(..., dest, N) raw reads

Its fields include **`mLastPosX` and `mLastPosY`** - so things DO carry position - alongside mCash,
mPersonType, mQueuePos, mPaidAdmission, mQNext/mQPrev, mPreviousRides[4] and mState. That is a guest,
though, so **the gate is a different model with a different reader**. Per-type readers, from the
switch: 1 FUN_004fb530, 3 FUN_0051ade0, 4 FUN_004da110, 5 FUN_004d6d60, 6 FUN_004d4460,
7 FUN_004d6000, 8 FUN_00502760, 9 FUN_00508bb0, 10 FUN_004da960. **Do not decompile all nine** - they
are huge; count their sized-helper calls instead to get each type's on-disk length.

### What each thing model IS - named by its own debug strings

The same trick that cracked the World header works here: every reader carries its field names.
**Models 4-10 do not read directly, they DELEGATE** - a first probe that counted helper calls in the
nine readers found zero for all but model 1, which meant "look one level down", not "reads nothing".

    1   GUEST        mCash, mPaidAdmission, mNumRides/Shops/Sideshows, mQueuePos, mPersonType,
                     mQNext/mQPrev, mPreviousRides[4], mLastPosX, mLastPosY     (>=111 bytes)
    2   does not exist - there is no case 2
    3   READ BY FUN_004db7d0, not by FUN_0051ade0. Emulating `FUN_0051ade0` alone gives 0 fields
                     and 0 reads - it is a constructor - but a model-3 record in the real stream is
                     16 fields / 54 bytes, read by **FUN_004db7d0**, which owns mAngle, mId, tv[t].
                     (An earlier note here said model 3 "is not a reader at all"; that was true of
                     FUN_0051ade0 and wrong about the model.)
    4   MECHANIC     mDurationOfRepair, mObjectToRepair, mNext
    5   HANDYMAN     mTargetLitterCell, mTimeStartedCleaning, mToiletToClean, mNext
    6   ENTERTAINER  mTimeStartedEntertaining, mNext
    7   GUARD        mPerp, mProsecutionTimestamp, mNext
    8   RESEARCHER   mTimeStartedResearching, mNext
    9   STRIKE SYS   mForceStrike, mStaffMemberPickedUp, mStrikeLevel[i]
    10  OBJECT       mNextObject                                   <- the park's placed things

Models 4-8 share **`FUN_00504de0`** (>=25 bytes): mCurrentPayGrade, mJobsDone, mName[i],
mPatrolRegionBL, mPatrolRegionTR, mPercentageThroughGrade, mRestArea, mState, mTimeStartedIdling,
mTimeHired. Models 9-10 share **`FUN_0050b090`**: **mMapChild, mMapParent**.

**Two things corroborate this and one is the way in.** The five staff models line up exactly with the
header's five `mFirst*` heads (mFirstHandyman 25, mFirstMechanic 26, mFirstEntertainer 27,
mFirstGuard 28, mFirstResearcher 30) - five types, five heads, which is not a coincidence. And model
10's `mNextObject` matches the header's `mFirstObject` = 15.

**`mMapChild` / `mMapParent` are MAP-TREE LINKS - that is the placement mechanism**, and objects
carry them. So the gate is very likely a **model-10 object with id 11** (`mParkGates` = 11). Treat that
as an INFERENCE until the object list has actually been walked to id 11 and the gate's record read.

### A model-10 OBJECT is only 10 bytes on disk - five u16 fields

`FUN_0050b090`, the shared base for models 9 and 10, reads four u16s in this order, and
`FUN_004da960` then adds a fifth:

    u16 -> struct +4    name string at DAT_00761cbc  (unnamed in Ghidra)
    u16 -> struct +6    name string at DAT_00761cb8  (unnamed in Ghidra)
    u16 -> struct +10   mMapChild
    u16 -> struct +8    mMapParent
    u16 -> struct +0xc  mNextObject                  (model 10 only)

So **10 bytes per object**, and with the per-iteration `[u32 Used_Thing_Next][u32 thingmodel]` prefix,
**18 bytes per object record in the stream**. Note the struct offsets are not in file order - the file
order is what matters, and it is the order above.

**THE TWO UNNAMED FIELDS ARE `mX` AND `mY`.** The strings at 0x00761cbc and 0x00761cb8 read exactly
that, so the file order of a model-10 object is:

    u16 mX | u16 mY | u16 mMapChild | u16 mMapParent | u16 mNextObject

**Every object carries its own map cell.** That is the placement mechanism N2 was looking for, and it
is what puts the gate in the two holes beside the entrance. Note the struct offsets (+4, +6, +10, +8,
+0xc) are NOT in file order - read them in the order above.

### The staff base `FUN_00504de0`, exactly - 105 bytes

From its write branch, which states every size outright:

    mCurrentPayGrade 4 | (unnamed float) 4 | mJobsDone 4 | mName[i] 2 x 0x21 = 66 |
    mPatrolRegionBL 2 | mPatrolRegionTR 2 | mPercentageThroughGrade 1 | mRestArea 2 |
    mState 4 | mTimeStartedIdling 4 | mTimeHired 8 | (unnamed float) 4     = 105 bytes

**`mName[i]` is a 33-entry array**, which is precisely why counting helper calls could never have given
this size - always read the write branch for exact sizes. Both the guest reader (`FUN_004fb530`) and
the staff base call **`FUN_004f8b10` first**, so there is a common person base.

**`FUN_004f8b10` (the person base) calls `FUN_0050b090` FIRST**, exactly as model 10 does. So **every
map thing, people included, begins with `mX, mY, mMapChild, mMapParent`** and each record in the
stream opens with a fixed **16-byte prefix**: `[u32 Used_Thing_Next][u32 thingmodel][u16 mX][u16 mY]
[u16 mMapChild][u16 mMapParent]`. The person base then adds mAccurateDestX/Y 2+2, mAdjustorSpeed 2,
mBaseSpeed 2, mCount 1, mESPSprite 4, mLastRecordedMapId 2, mPreviousSpeed/X/Y 4+4+4, mStrandedTime 4,
mPurposeSpeed 2, mSetDestSuccessfully 4, mSpriteAngle 4, mSpriteID 4, mSpriteUnderRideCtrl 4, and
nests further.

**The nested readers, resolved:** `FUN_004d3ee0` is **12 bytes** - mSpriteScript 4, mNextAnim 4,
mNextServiceInterval 4 - and **`FUN_004f9440` is a pure forwarder to `FUN_0050d150`**, adding nothing
of its own. So the person base in READ order is

    FUN_0050b090 8 | FUN_004d3ee0 12 | mAccurateDestX/Y + mAdjustorSpeed + mBaseSpeed 8 |
    mCount 1 | mESPSprite 4 | mLastRecordedMapId 2 | FUN_0050d150 ? |
    mPreviousSpeed/X/Y + mStrandedTime 16 | mPurposeSpeed 2 |
    mSetDestSuccessfully + mSpriteAngle + mSpriteID + mSpriteUnderRideCtrl 16 | FUN_0050bad0 ?

= 69 bytes plus two nested readers, and **both are now resolved**:

**`FUN_0050d150` = 177 bytes** (steering/navigation): force 8, formation_pos 8, local_xaxis 8,
local_yaxis 8, unnamed 4, max_force 4, max_speed 4, mCantReachDest 4, nav_mode 4, path_buffer_count 4,
path_count 4, path_finished **1**, path_last_progress 4, path_stuck_buffer 4, path_subpath_dist 4,
path_tail_dist 4, path_target_pos 8, path_timestamp 4, path_total_count 4, path_total_dist 4,
unnamed 8, radius 4, then **5 x (subpath_buffer[i] 8 + subpath_dist[i] 4) = 60**, then a final 8.

**`FUN_0050bad0` = 144 bytes** (thoughts/history): mActionHistIndex 4, **32 x mEventHistory[i] 4 =
128**, mLastThought 4, mThoughtScript 4, mTimeBubbleShown 4.

### The size table so far

    person base FUN_004f8b10 = 69 + 177 + 144 = 390 bytes
    model 1  guest   = 390 + 135 = 525      (malloc says 556 - the gap is runtime-only fields)
    model 10 OBJECT  = 8 + 2     = 10       <- the gate
    models 4-8 staff = 390 + 105 + per-type (per-type blocks still to size)
    model 9  strike  = 8 + its own

Model 1's own 135: mArrivalDate/Index/BalloonScript/BeenAdmitted/Cash/ExitLevel 4 each, 2 floats,
mLastPosX 4, mLastPosY 4, float 4, mMajorDest 2, mNumRides/Shops/Sideshows/SideshowsWon 4 each,
mPaidAdmission 4, mParkOpeningWaitingTime 4, mPersonType 1, mPrankeryIndex 1,
**4 x (mPreviousRides 2 + mPreviousTemporaryRides 2) = 16**, mQNext 2, mQPrev 2, mQueueMoveDelay 4,
mQueuePos 1, mRemainingBalloonLife 4, mSavedMajorDest 2, mSavedState 4, mState 4, float 4,
mTimeOfLastSpotAnim 4, mTimeStartedIdling 4, 3 floats 4 each.

**The per-type blocks, all decompiled:**

    model 4 mechanic     mDurationOfRepair 4 + mObjectToRepair 2 + mNext 2            =  8
    model 5 handyman     mTargetLitterCell 2 + mTimeStartedCleaning 4 +
                         mToiletToClean 2 + mNext 2                                   = 10
    model 6 entertainer  mTimeStartedEntertaining 4 + mNext 2                         =  6
    model 7 guard        mPerp 2 + mProsecutionTimestamp 4 + mNext 2                  =  8
    model 8 researcher   mTimeStartedResearching 4 + mNext 2                          =  6
    model 9 strike sys   FUN_0050b090 8 + mForceStrike 4 + mStaffMemberPickedUp 2 +
                         5 x mStrikeLevel[i] 12 = 60, then FUN_0050c640               = 74 + ?

### >>> THE COMPLETE SIZE TABLE <<<

    per-iteration prefix                [u32 Used_Thing_Next][u32 thingmodel]  =   8
    model 1  guest        390 + 135                                           = 525
    model 4  mechanic     390 + 105 +  8                                      = 503
    model 5  handyman     390 + 105 + 10                                      = 505
    model 6  entertainer  390 + 105 +  6                                      = 501
    model 7  guard        390 + 105 +  8                                      = 503
    model 8  researcher   390 + 105 +  6                                      = 501
    model 9  strike sys   74 + FUN_0050c640                                   =   ?
    model 10 OBJECT       8 + 2                                               =  10
    model 3               constructor only - still unaccounted for

`FUN_0050c640` also resolved: **5 x mBudget[i] 4 + mHaveEverTrained 1 = 21**, so **model 9 = 95**.

The header ends at **0x4DF**, so the first iteration reads `Used_Thing_Next` = 500 and `thingmodel`
= **5** (a handyman).

### >>> THE WALK FAILED - and the size table is the suspect <<<

Walking from 0x4DF with the table above managed **exactly one thing** (the model-5 handyman) and then
hit model **0** at 0x6E0.

**RETRACTED 2026-09-13.** This section originally concluded "505 is not the handyman's on-disk size,
and by extension the other computed sizes are wrong too". **That was wrong.** Emulating the readers
later confirmed **every** hand-derived size exactly - handyman 505, guest 525, mechanic 503,
entertainer 501, guard 503, researcher 501, person base 390. The sizes were right all along, so the
walk's failure lies in the **stream layout** - the 8-byte `[Used_Thing_Next][thingmodel]` prefix, or
where the header really ends - and not in the record sizes. See the emulator section at the end of
this file.

**The likely cause, and it undermines every size derived above: Ghidra's parameter recovery in these
readers is not reliable.** Every reader is gated on `(-1 < param) && (param < 2)` - a version/mode
parameter that can cause it to return without reading anything - and `FUN_004fb530` is *called* as
`(iVar6, 0, 1)` while its load branch tests `param_3 == 0`, which does not line up. So the read/write
branch split that every field size was read from cannot be trusted as it stands. Resolve the calling
convention and that gating parameter FIRST; do not recompute sizes on the current reading.

**Scoreboard for N2, to save the next session from repeating any of it.** Five approaches have now
failed: (1) 12-byte stride test, (2) header plausibility scan, (3) model-10 signature scan,
(4) pulling field sizes from `FUN_00416e10` immediates, (5) the sequential walk. What has actually
worked, every time, is reading the exe properly - the module map, deriving World's start from
ActionRec, type names and `mX`/`mY` from debug strings. **The next step is not another increment on
the walk**; it is to settle how these readers are actually called (the mode parameter and the
this-pointer), because that is what decides which fields are on disk at all.

`FUN_00416e10(size, name, dir)` does precede every field, but **extracting those sizes from the
instruction stream DOES NOT WORK and is not a shortcut** - tried and refuted. Scanning back from each
call for a pushed immediate found **0 fields** for `FUN_0050b090`, whose decompile plainly shows eight
such calls, and 3 for the person base where the truth is about thirty-four. It only catches sizes that
happen to be a nearby `PUSH` immediate; where the compiler keeps the value in a register or hoists it,
the extraction misses silently. Treat any number it produces as an unreliable lower bound.

**The pattern across N2 is now unmistakable and worth obeying.** Every success here came from reading
the exe properly - the 17-module map, deriving World's start from ActionRec, the thing-type names from
their debug strings, `mX`/`mY` from the string table. Every failure came from trying to shortcut that
with a statistical test over the bytes: stride equality, header plausibility, the model-10 scan, and
now the immediate extractor - **four in a row**. Decompile the reader; do not measure the bytes.

### FAILED: scanning the World block for model-10 objects

Searching the World block for a dword reading 10 and decoding the next 10 bytes as an object found 18
candidates, 14 with mX and mY inside the grid - **and it is wrong**. Every one has **mY = 0**, mX spans
only 0..7, and the map links are junk (1280, 3072, 12160). Objects spread over a 96x85 park cannot all
sit on row 0.

**I also quoted a bogus significance figure and withdraw it**: "random data would pass this filter only
0.0004% of the time" assumes random bytes, but this region is mostly **zeros**, which sail through a
"two small u16s" test. **That is the THIRD powerless filter in one session** (stride equality, header
plausibility, now this). The tell is always the same - a suspiciously uniform result across candidates.
**Objects must be reached by WALKING the stream from the header, not by scanning for model numbers.**

**A methodological warning, because I made this mistake TWICE in one session.** Both the 12-byte stride
test and a "plausibility score" over candidate header offsets returned **the same answer for every
candidate** - ~83% for every stride from 4 to 68, and a tied top score for every even offset from 10 to
62 - because the payload is mostly zeros and every criterion was trivially satisfied by zeros. A test
whose output does not VARY across candidates has no discriminating power and its "winner" means
nothing. **Check the spread before believing the maximum.**

**What the payload actually is: a serialised MEMORY IMAGE, not a portable format.** Each `OBJ ` is
followed by `(0 or 1, 28)`, then **two pointers** (`0x04ce86f4`, `0x04cea284` - live heap addresses
saved verbatim) and a descending index, i.e. prev/next links of a linked list. That is why no packed
coordinate encoding turns up and why identical records differ in three of four bytes.

**So the next step is the exe, not more archaeology** - find the routine that writes a park save and
read the structure off it, the way the terrain and camera were done. The 119 `tButton` records (clean
**320-byte stride** from 0x16e989, first one `"tButton\0Tag2\0"`) are the park's UI buttons, not
features, so they are not the way in.

Feature models live under `data/levels/<theme>/Features/<name>/`, `Shops/`, `Rides/`, `Sideshow/`.
**Done when** the gate stands in its holes.

### >>> N3. PATHS - AGREED AS THE NEXT JOB (Alexah, 2026-09-13). Research is DONE. <<<

Alexah asked for this one by name: *"everything currently sits in an empty field, when there are
definitely paths connecting everything."* He is right, and the park file has them.

**The old note here was WRONG and has been replaced.** It said `base.map`'s 0x80 marks path cells and
that the neighbour mask must be computed. Both are wrong - see [[park-data-layout]], which now carries
the measurements. **The paths are in the World block's map cells**, the 1.38 MB region `ParkWorld`
currently measures and steps over, and `mNeighbours` is stored rather than computed.

**Everything needed to parse them is already in hand**: the field log names every field of all 16,384
cells, the two record sizes match `ParkWorld`'s measured skip to the byte, and the cell order
(`y * 128 + x`) is proven by drawing it against base.map.

**P1. Make `ParkWorld` parse the map cells instead of skipping them. *** DONE 2026-09-13, `3579ebd`,
PUSHED.*** Done as planned, as a `readonly record struct MapCell[]` (no per-cell object), with four
tests: the 16,384 cells and the mType tally, the entrance avenue, the Belly Bounce's footprint, and the
tile fields. **A check worth keeping: measuring all 16,384 cells individually lands on the next cell's
status byte every single time** - sharper than the walk's trailer test, which a pair of compensating
errors would still pass.

**What the tests pinned, so it need not be re-derived:** the entrance avenue is `x=47` and `x=48`, each
**unbroken from y17 to y28**. The Belly Bounce's 3x4 box at (51,23) is mType 4 on ten of its twelve
cells; the middle column's two ends are **mType 9 at (52,23) and mType 10 at (52,26)**, and the four
mType-3 queue cells lie in the row beyond the 9 at `x49..52, y22`. That arrangement reads as a way in
and a way out, **but naming them entrance and exit is a guess and the test asserts the arrangement
instead** - mType 9 has 8 cells park-wide and some sit on the drinks shop and litter bin, so it is not
simply "ride entrance".

**P2. Settle which tile a cell draws. *** DONE 2026-09-13 - the tile is STORED, not derived.*** `<Theme>.tct`
gives `PathTex` 0..21 by name (`jpa_squ1` square, `jpa_end1` end, `jpa_str1/2` straight, `jpa_cnr1/2`
corner, `jpa_tju1..4` T-junction, `jpa_xrd1/2` crossroads, `jpa_edg1/2` edge, `jpa_ctr1` centre) -
a classic neighbour-mask tile set. **The old claim of a "49-record path table, exact match then
subset" is UNSOURCED** - it appears nowhere but that deleted note, so treat it as a lead and either
find it in the exe or derive the mapping from the 78 path cells against the art names. Do not adopt a
rule that fits one cell.

**P3. Draw them. *** DONE 2026-09-13, `726dea7`, PUSHED.*** `ParkGround` already skips index-0 cells and builds per-cell quads with a
16-slot material, so a path layer is the same shape of work: quads at the cell's corners following the
heightfield, textured from `terrain.wad`'s `pathtex/` (names are `.tga` in the .tct and `.wct` on
disk). **Mind the two traps already paid for**: normals must be handed over with Y and Z exchanged, and
`Game.ParkLoadSteps` must be re-measured because every new texture is an `Asset.Register` step
([[keep-loading-steps-current]]).

**P4. Queues, later and separately.** `queue.wad` holds **models, not textures** - `questra`,
`quebnd1/2`, `queend`, `quedead`, `quebin1/2` as `.MD2` - so a queue is placed like an item rather than
painted like a path. `QueueTex` 0..3 exists too. Keep it out of P1-P3.

**How to verify a render - CORRECTED 2026-09-13, the old answer here was WRONG.** This used to say each
park's `2dmap.tga` "shows the real network" and was "the original's own answer". **It does not show the
paths.** It is a 512x512 terrain-relief minimap - the river, the cliffs, the volcano as a round blob,
the arrival structure at the top - and nothing in it resembles the rectangular loop and double-wide
avenue the save actually describes. Measured rather than eyeballed: sampling it per cell and comparing
path cells against the rest separates them by at most **0.60 standard deviations** over six
orientations, where a minimap that drew paths would separate by several. (Caveat kept for honesty: that
test assumes the image maps the 128 grid linearly, and `base.MD2` spans a much larger scenic island, so
a misalignment could weaken it - but a loop and a two-cell-wide avenue would be *visible as shapes* at
any alignment, and none is.) **So verify a path render against the decoded cell data and by looking,
and ask Alexah to compare with the original** - he has it running and has caught two visual bugs that
way already.

### N4. Leaving a park through the front end. ***DONE and PUSHED 2026-09-14 - `922fe43`, `f2f266c`, `96134bf`.***

See the state block at the top for what landed. **The hazard this section warned of never bit, and why
is worth keeping**: it feared that "release what a scene loaded" would double-free, because textures
share GPU handles through the cache and since `bb897f8` one blank instance is held by every empty
material slot. But **the debug console's `lobby` command had been exercising that exact teardown on every
reload for ages**, so park -> lobby was a well-trodden path before this change made it a player's, and
nothing about ownership had to be decided. Park state 0xb's order is traced in [[park-engine-from-exe]];
ownership itself is still undecided and still matters for [[post-neoveldrid-audit]]'s asset release.

### N5. The smaller park gaps, once the above are in.

A park's HUD is **the game menu and the pointer, and nothing else** since branch 42. No advisor, no
weather or sky (`Sky` takes its colours from whichever lobby island the camera is on, so it needs a park
answer), and the park's own sound categories are not started.

**`Game.ParkLoadSteps` no longer needs re-measuring, and this paragraph used to say it did.** Since
`b70c006` the bar learns each situation's count for itself and the constant is only a first-run seed -
see [[keep-loading-steps-current]], which now describes the mechanism instead of the chore.

## Findings worth keeping

- **The shipped park file is the original's INSTANT ACTION mode** (Alexah, 2026-09-13). `Easymode.TPWI`
  is that mode's park, which is why only jungle ships one. **He suggests this may be what a version or
  mode flag distinguishes** - the candidates are the container's `400` (with `500` an untested
  allowance), the World header's own `version` = 2, and ActionRec's `mLoadedPublishedPark` = 0, none of
  which is settled. **Full Simulation saves may be laid out differently.** He will supply one when he
  can but said plainly not to rely on getting it - **do not block on it**, and do not assume the reader
  generalises to a `.TPWS` until one has been read.
- **Particle handles:** the lobby starts ~5 effects making a player, 4 at the tour's key, none in a minute of
  sparkles and ~5 per button hovered, so generation 0x8000 is thousands of hovers away there; parks will reach it.
- **Thunder:** a forced strike within the last thunder's clip length plays nothing - `SoundCategory.Play`'s repeat
  delay. Expect 3-4 matches out of 5 strikes spaced 4.5s apart, not 5.
- **The advisor under a box reads `shown=True`** (he is simply not drawn while paused).
- **Reload cost:** each rebuild logs 2789 steps, models +194 and assets +2788 per load, VmRSS swinging 365-896MB
  over ten cycles without climbing steadily. Asset release is still deferred.

## Deferred, each with its trigger

Park states 9/10/0xb and splitting the lobby build out of Level's constructor (first park load); preloading every
ui.wad mesh at boot (first park window); the park world as a full-screen UI
control with key bindings as data; MessageBox button places chosen by the caller; a pausable game clock and a
shared 31ms ticker; advisor park inputs - response table 0x00768fb8, gesture rows 0x0076dc18, TrySay, model slots,
park speech; sound category scopes with a bank cache, positional voices, the held layer; a sky settings record and
scene-set fog/light; neutral names for LobbyModel, MeshAnimator, MeshRotator, SignTexture, WeatherSprites,
LobbyRain, LobbyLightning; world-space particles and the sprite bank split; SAMParser multi-field rows with format
tests; player progress out of PlayerFile, theme folders, .TPWS; Model/Material/archive release per scene; camera
island on return from a park; the island panel under the slots on entry.

**From the resolution work:** `WeatherSprites.WriteQuad` billboards to the camera position rather than the view
plane; `LobbyLightning`'s fade band is calibrated at 4:3; remembering the window size between runs;
HiDPI/drawable-size; the cursor stays 32 device pixels.

**From the portable-path work:** SoundBank.Resolve and SaveFolder.Find are now belt-and-braces over the case
layer and could go; `GetItem<T>` in WadArchive still uses `.First()` (throws) where its sibling returns null;
MSTest 2.2.7 / Test.Sdk 16.11.0 are old and work, but a bump is its own job; Cursor still bypasses
BaseFileSystem entirely (GameDir.GetPath + File.ReadAllBytes).

**From the positional-audio work:** `Rotation.LookAt` has **no roll control** - `.Right` and `.Up` off a
LookAt rotation are meaningless (`.Forward` is fine). Left alone on purpose: one caller, and nothing
takes a side vector from it. Trigger: the first time something other than the audio listener does.

**Later decisions for Alexah:** shipping exe-transcribed tables; whether a park pause hides the advisor or only
holds him; whether the 1.5s rest applies to park lines; the 0.1s frame clamp against a 2s catch-up.
**Carried over:** OpenTPW.FileFormats' saves.md calls version 500 a "F4 01 00 00 magic" - fix in the N2 save task.
**Open from before:** renaming Ghidra's 0x004092a0 (Game_Pause) and 0x00409300 (Game_Resume) was refused
by the permission classifier; Alexah has not answered.

## >>> N2's REAL BREAKTHROUGH: emulate the readers, do not infer them <<<

Alexah authorised installing tools on 2026-09-13 and that unblocked N2 completely. **Run the reader
and watch it.** The original declares every field before reading it - `FUN_00416e10(size, name,
direction)` - so a trace of that ONE call IS the record layout: named, in order, and including
everything the nested readers contribute. No more deriving sizes from decompiles whose parameter
recovery is unreliable.

    venv    /home/alex/.cache/tpw-emu        unicorn 2.1.4 + pefile, on the REAL disk (the
                                             session scratchpad is a 3.2GB tmpfs and fills)
    script  scratchpad/fov/trace.py          trace.py <function-address-hex> [mode]
    binary  /home/alex/Games/TPWorld/testme.exe   PE32, image base 0x400000

Map the PE's sections, stub only the LEAF primitives (`FUN_005f5e20` read, `FUN_005f5e50` write,
malloc `FUN_0067acc0`/`FUN_005f5660`, free, tell, logger `FUN_005da3c0`, `__ftol`), and let every
reader in between run for real - the nesting is exactly what hand-derivation kept getting wrong.

**Three traps, each of which cost a run.**
1. **Calling convention. `FUN_00416e10` is stdcall - `RET 0xC`.** A stub that pops only the return
   address leaks 12 bytes a call; after four the function's own RET jumped to the address of a
   *string*. Fix: do NOT emulate its return - log the arguments and let the real `return 0;` retire
   with its own convention. **I then made this exact mistake a second time** with a hand-written
   stub for `FUN_004fa5f0`, and the symptom was a RET to a null (`FETCH_UNMAPPED at eip=0x0`).
   **Check every stub's terminating RET before writing it.** The measured table, so nobody has to
   derive it again:

       FUN_00416e10 decl      RET 0xc   stdcall, 3 args   (do NOT stub - let it run)
       FUN_004fa5f0 renav     RET 0x8   stdcall, 2 args   (stub must pop 8)
       FUN_004f8b10 person    RET 0x10
       FUN_0050bad0 thoughts  RET 0x10
       FUN_00541f60 divisor   RET       cdecl (stub pops 0)
       FUN_005f5e20 read      RET       cdecl (stub pops 0)

   A second independent check on any of these: look at the call site. `ADD ESP,N` after the CALL
   means cdecl; its absence means the callee cleaned up.
4. **Name a stub after the RIGHT function.** `trace.py` carried `F_FTOL = 0x0067b280` for a while;
   `__ftol` is actually at **0x0067a830** (FSTCW/FISTP, 39 bytes). 0x0067b280 is an unrelated
   337-byte function that calls 0x00686080. So the real `__ftol` ran unstubbed *and* something else
   was silently forced to return 0 on every call. Check the disassembly before trusting a label.
5. **Do NOT stub anything that reads the stream itself.** Of `FUN_004db7d0`'s callees,
   `FUN_004ced70` (34 calls), `FUN_004cee50` and `FUN_004e2c10` all call the read primitive - stub
   them and they would swallow real fields and corrupt the very layout being measured. Only
   `FUN_005fc660` (RET 0x24) and `FUN_005fc5c0` (RET 0x20) are pure computation and safe to stub.

**A tool improvement worth keeping: `trace.py` now records a trail of recently executed addresses**
and prints it with any fault. Every failure so far reported only a wild destination (eip=0x3248b2,
0x0, 0x1f46) which says nothing; the trail turns that into the call site that jumped there.

**It paid for itself immediately.** The crash trail read
`0x5fc5ff -> 0x5fc604 -> ... -> 0x5fc619`, i.e. inside **`FUN_005fc5c0`** - which is pure
computation (`RET 0x20`, no stream reads), so stubbing it with `pop=0x20` is safe. **Guessing would
have got this wrong**: by call frequency the obvious suspect was `FUN_004ced70` (34 calls), and
stubbing that would have silently swallowed real fields, because it reads the stream.

### >>> `mParkGates` is an IDENTITY TOKEN, not an index <<<

`FUN_004e2950` uses it like this:

    sVar4  = *(short *)(DAT_0080239c + 0x1da732);   // mParkGates
    psVar5 = (short *)FUN_0050b350(&thing);
    if (*psVar5 == sVar4) { ... }                    // EQUALITY, not indexing

So **11 is matched against a value taken out of a thing** - it is not "the 11th object" and walking
the list to index 11 would be wrong. `FUN_0050b350` is the accessor that extracts the comparable
value from a thing, so that is the function that resolves the gate.

`mParkGates` sits in a family of identical handles - `mTrafficLights`, `mStaffHQ` (+0x1da718),
`mMechanicHQ` (+0x1da71a), `mResearchLab` (+0x1da722) - each with a one-instruction getter
(`FUN_00519450`, `FUN_00519490`, `FUN_00519550`). It is also **written by `FUN_005156a0`, the
Standard.sam balance loader**, so the field is seeded from the balance file and not only from a save.

**What it is compared against: the thing's FIRST u16, at struct offset +0.** `FUN_0050b350` is 13
bytes - `MOV EAX,[ESP+4]; MOV CX,[ECX]; MOV [EAX],CX; RET 4` - so it just copies `thing->[+0]` out.

**And offset +0 is NOT serialised.** The map-thing base writes +4 (mX), +6 (mY), +8 (mMapParent),
+10 (mMapChild), and model 10 adds +0xc (mNextObject); nothing writes +0. It is assigned at load
time by **`FUN_0050b080`, which is literally `*param_1 = param_2`** - a 2-byte store into offset 0,
called from the World loader's switch right after each thing is allocated.

**So the gate cannot be found by walking to the 11th record.** Its id is a runtime number handed out
during loading, and matching `mParkGates` means reproducing that numbering.

**And the numbering is NOT stream order either - that would have been the next wrong guess.** All 17
`FUN_0050b080` call sites in the World loader share the same preamble:

    *(undefined1 *)(thing + 2) = (undefined1)thingmodel;   // the model goes at thing+2
    uStack_5c = CONCAT22(uVar7, uVar8);                    // uVar8 = (undefined2)piVar1[1]
    FUN_0050b080();                                        // *thing = that u16  -> thing+0

`piVar1` is not a counter. It walks a **pre-existing slot table**: `piVar1 = &DAT_007cfb90 +
iStack_24 * 5` with `piVar1 = piVar1 + 5` to step, and list links written into `piVar1[2]`,
`piVar1[3]`, `piVar1[4]`. So each thing is handed a **5-dword slot**, and **its id is that slot's
`[1]` field** - which is what `mParkGates = 11` refers to.

Also worth keeping: **the model number is stored at `thing + 2`** as a byte, and the id u16 at
`thing + 0`, so a loaded thing carries both without either being read back from the file.
`FUN_0050b080` in full: `MOV AX,[ESP+4]; MOV word ptr [ECX],AX; RET 0x4` - stdcall, one argument,
`this` in ECX.

**The slot INDEX does come from the file, though.** The loader reads 4 bytes and then does
`piVar1 = &DAT_007cfb90 + iStack_24 * 5`, so the per-thing u32 prefix - the field whose debug name is
`Used Thing Next` - is the **slot index**, and the thing's id is that slot's `[1]`. The table itself
is BSS (`DAT_007cfb90` reads all zeros statically), seeded at runtime by **`FUN_00515f30`**, which
`FUN_005179c0` calls before it loads anything.

### >>> THE NUMBERING: a thing's id IS its slot index <<<

`FUN_00515f30` is trivial and settles it:

    puVar1 = &DAT_007cfbb4;  iVar2 = 1;
    do {
        *puVar1   = puVar1 + 1;     // link to the next slot
        puVar1[-3] = iVar2;         // slot[1] = counter, starting at 1
        puVar1 = puVar1 + 5;  iVar2 = iVar2 + 1;
    } while (puVar1 < &DAT_00801b8c);

**Slot N's `[1]` field is just N.** The table spans 0x7CFBA4..0x801B8C, about **10,239 slots**. So:

    thing id  ==  slot index  ==  the u32 "Used Thing Next" prefix in the file

### >>> RETRACTED: the prefix is NOT the thing's id. It is the NEXT pointer. <<<

The rule just above is **wrong** and everything derived from it below was wrong with it. The field's
debug name says exactly what it is - `Used Thing Next` - and the stream is a **singly linked list**:

    record i stores the id of record i+1;  a thing's OWN id is the value stored in the PRECEDING record

Two things prove it and neither is a single sample:

1. **The last record's prefix is 0** - a null terminator. Under "prefix == own id" some thing would
   have to be id 0, and nothing references id 0.
2. **The prefix is not monotonic.** It runs 41, 40 ... 30, 29, then **15**, then 28, 27, 26 ... 1, 0.
   A self-id would not jump; a next-pointer in list order does, and 15 is exactly where the Bus is
   spliced in.

Reading it as a next-pointer makes **eight** header handles land on semantically right objects at
once - `mParkGates`->Gates, `mTrafficLights`->Lights, `mFirstObject`->Bus, and the five staff heads
onto five distinct staff models. Under the old rule every one of them was off by one and two of them
were nonsense (the "gate" was the Lights, the "traffic lights" were a Belly Bounce ride).

**Lesson, and it is the same one as the `>>7` mistake: a rule that fits one sample is not a rule.**
Here a debug name was telling me the truth in plain English and I read past it.

### >>> THE WHOLE WORLD BLOCK NOW PARSES, TO THE BYTE <<<

With the `__ftol` address corrected and `FUN_005fc5c0` stubbed, the trace runs with **no fault at
all** and the arithmetic closes exactly: the block starts at **0x49B (1179)** and the run consumes
**1,494,283** bytes; 1179 + 1,494,283 = **1,495,462 = 0x16D1A6**, which is precisely the `WRLD`
trailer. Reading a 1.5MB variable-length structure end-to-end and landing exactly on the next tag is
the strongest confirmation available that every record size in it is right. 495,997 fields, one page
mapped on demand.

    trace.py 0x005179c0 0 <inflated-payload> 0x49b     -> fields_005179c0.txt (495,997 rows)

**But only ONE model-10 object turned up** (0x16D135, mX=128, mY=128, child=1, parent=3, next=18),
and 128/128 is the same pair the model-3 record carries, so it looks like a sentinel rather than a
position. One object is far too few for a park containing a gate, bus, fountain, three toilets, two
cameras, a staff room, a litter bin, a kiosk, traffic lights, a ride and a sideshow - so **the
features are not all model 10**, and the right lookup is by **slot index, not by model**.

### >>> THE PARK IS FULLY DECODED: 42 things, every one identified <<<

`mId` (the u16 at record + 20) is the catalogue **`Info.Id`** from the item's own `.sam`, inside its
`.wad`. The wads are Refpack-compressed, so **grepping a .wad for `Info.Id` returns garbage** - it
reads straight through compression tokens and yields the first two digits only. Decompress properly.

    HARNESS (not in the repo): /home/alex/.cache/tpw-wadcat  - a console app that uses OpenTPW's own
    WadArchive/Refpack. Built Release so it never touches bin/Debug.
        dotnet build -c Release
        ./bin/Release/net10.0/wadcat --id   <wad>...      Info.Id + Name per wad
        ./bin/Release/net10.0/wadcat --cat  .sam <wad>... print a member
        ./bin/Release/net10.0/wadcat --list <wad>...      every member

Catalogue bands: 1_1xx rides, 1_2xx shops, 1_3xx sideshows, 1_4xx features, 1_5xx upgrades,
**1_6xx the fixed items** (1600 Bus, 1601 Gates, 1602 Seaplane, 1603 Lights, 1604 Ferry, 1605 End).

    id model what              cell        item
     1-10  9..19  managers     sentinel    one of each model
       11  3   catalogue obj   sentinel    Gates            <- mParkGates
       12  3   catalogue obj   sentinel    Lights           <- mTrafficLights
       15  3   catalogue obj   sentinel    Bus              <- mFirstObject
       13  3   catalogue obj   (51,23)     Belly Bounce
       14  3   catalogue obj   (51,30)     Jungle Spray
       16  3   catalogue obj   (43,30)     Drinks Shop
       17  3   catalogue obj   (44,29)     Litter Bin
    18,19  3   catalogue obj   (55,29),(40,29)  Security Camera x2
       20  3   catalogue obj   (58,16)     Staff Room
    21-23  3   catalogue obj   (55,17),(55,16),(55,15)  Small Toilet x3
       24  3   catalogue obj   (57,19)     Round Fountain
       25  5   handyman        (49,28)
       26  4   mechanic        (47,25)
       27  6   entertainer     (47,25)
       28  7   guard           (39,28)
       30  8   researcher      (48,17)     standing ON EntranceB
    29,31-41  1 guests         x47-48, y9-15, on the bus road

**model 1 = guest, 3 = catalogue object, 4 mechanic, 5 handyman, 6 entertainer, 7 guard,
8 researcher, 9..19 = the singleton managers.** The head record (a guest) has no id in the chain.

### >>> THE TWO HOLES BESIDE THE ENTRANCE ARE ANSWERED - and not from the save <<<

**Bus, Gates and Lights carry the sentinel 128/128 because fixed items are NOT positioned by the
save at all.** They are placed from the theme's balance file, `data/levels/jungle/Standard.sam`:

    # The origin from which fixed items (buses, gates) are placed
    MapInfo.FixedItemOriginX 48   MapInfo.FixedItemOriginY 17
    FixedItemInfo.EntranceAPos   (47,17)   EntranceBPos   (48,17)   <- THE TWO HOLES
    FixedItemInfo.TicketBoothAPos(47,13)   TicketBoothBPos(48,13)
    FixedItemInfo.CrossingParkSideA/B (47,9),(48,9)   CrossingBSSideA/B (47,5),(48,5)
    FixedItemInfo.BusStopAPos    (42,5)    BusStopBPos    (53,5)
    MapInfo.HeightfieldWidth 95  HeightfieldHeight 84     <- independently confirms the 96x85 extent

So the gate mesh is `features/gates.wad` (Info.Id 1601). `mParkGates` is the handle used to ask
"is this thing the gate", not a position - which is why it never had one.

**And "two holes" was the wrong mental model. `Gates.sam` (inside the wad) overrides its own
placement:**

    Info.Id 1601   Info.Name "Gates"   Info.WhichUIType 4   (not shown in UI)
    Info.EngineMapOffsetOverrideX     45     Info.EngineFootprintWidthOverride   6
    Info.EngineMapOffsetOverrideY     16     Info.EngineFootprintHeightOverride  3
    Info.DontApplyOffset 1   "a fixed item whose animation should be played relative to
                              world (0,0), not the object pos"
    UsageInfo.ConstrainCamera 1

So the gate is **one 6x3 structure over cells x 45..50, y 16..18**, centred on the entrance cells
(47,17)/(48,17) and offset from `MapInfo.FixedItemOrigin` (48,17) by (-3,-1). The footprint says
which cells it OCCUPIES; it is not a mesh position.

**All six fixed items (Bus 1600, Gates 1601, Seaplane 1602, Lights 1603, Ferry 1604, End 1605)
carry `DontApplyOffset 1` and `WhichUIType 4`; only Gates carries the offset/footprint overrides.**

### >>> RETRACTED: they ARE world-authored. Load them at the origin. <<<

**I corrected a right answer into a wrong one here, and the node transforms refute it.** The section
below argued from `wadcat --bounds` that the fixed items must be placed at their cells. That is
wrong. `Mesh.BoundsMin/Max` are **node-local** - they say how big a mesh is, not where it stands -
and the placement is in `Mesh.WorldTransform`, which carries real world coordinates:

    gates.MD2   door01  -> cell (47.0, 17.2)     door02 -> cell (49.0, 17.2)
                gateway -> world x 466.8
    lights.MD2  tl01 (47.1, 9.2)   tl02 (48.9, 9.2)   tl04 (47.1, 5.9)   tl03 (48.9, 5.9)

**door01/door02 bracket the two entrance cells** (47 and 48; world 470 and 490 are those cells'
outer edges), and the four traffic lights land on `CrossingParkSideA/B` (47,9),(48,9) and
`CrossingBSSideA/B` (47,5),(48,5) - **4 out of 4 against `Standard.sam`**. The terrain's own
`ticket_booths` node sits at cell (46.6, 13.5), between TicketBoothA/B (47,13),(48,13).

So `DontApplyOffset 1` means what it appeared to mean, and a fixed item loads exactly the way
`ParkTerrain` loads `base.MD2`:

    new LobbyModel( $"levels/{theme}/features/gates/gates.MD2",
                    $"levels/{theme}/features/gates/textures", Vector3.Zero )

### BUILT: `ParkFixedItems.cs` (new, on alexah/39-park-terrain, NOT yet committed)

`source/OpenTPW/World/Park/ParkFixedItems.cs`, constructed in `Level.cs` right after `ParkTerrain`.
Loads **gates and lights only** - all four themes ship both. The other four fixed items are
deliberately excluded: bus (29.7,-11.5), ferry (90.0,-17.6) and seaplane (-0.2,17.4) are off the map
and `end.wad` is three aircraft 59 units up. They are vehicles at their spawns, driven by the `.RSE`
scripts whose runtime is unbuilt - standing them still on the ground would be worse than omitting them.

**Runtime-verified**: the park loads with no exception and the log reads
`levels/jungle/features/gates/gates.MD2: rotating 2 mesh(es) with 3 animation(s)` - the doors really
do animate. `save/` unchanged. Tests went **68 -> 71**, 0 skipped.

**The gate carries the park's NAME BOARD, and missing it showed up as two not-found textures.**
The first run logged `Could not load texture .../gates/textures/sign1.wct` and `sign2.wct` - and
neither file exists in jungle, fantasy or space (only hallow ships them). That is not a missing file:
`LobbyModel`'s `textureOverrides` exists precisely for this, and `SignTexture.TryBuild` paints the
park's name onto the artwork in the gate's own `gates.sgn`, cutting it into the two 128x128 panels the
gateway mesh draws as `sign1` and `sign2`. Overrides are taken ahead of the textures on disk
(`LobbyModel` checks the dictionary before building a `Texture` path), so offering it is safe for
hallow too - and `TryBuild` returning false falls back to whatever the model ships.

The park name is read from `Language/English/THEMENAMES.str` through `StringFile`, **not** copied from
`LobbyIsland.DisplayNames`. Correcting the lobby's hardcoded copy and its false comment is a separate
errand and was deliberately left alone here - see [[theme-names-and-locale-tables]].

**`ParkLoadSteps` was 671 at this point** (up from 637; **it is 872 now** - see above), measured twice
and identical both times - before the sign and
after it, so painting the board adds no step. Changed in the same commit, with the constant's history
prose extended rather than overwritten. `LobbyLoadSteps` stays **829** - the run logged "Loaded the
lobby in 829 steps - the loading bar expects 829", so it needed nothing. See
[[keep-loading-steps-current]].

**Runtime proof the sign works, and a second confirmation of the THEMENAMES order:**

    [3:55:14] jungle: painted 'Lost Kingdom' onto the park gate's sign
    [3:55:15] levels/jungle/features/gates/gates.MD2: rotating 2 mesh(es) with 3 animation(s)

Index 0 of THEMENAMES.str really is the jungle, which is what the header analysis predicted. The two
`features/gates/textures/sign1.wct`/`sign2.wct` warnings are gone.

### >>> SEEN ON SCREEN, not just in the log <<<

Captured five 1280x720 frames at the entrance (`scratchpad/gateshot/shoot.py`, adapted from
`hand_capture.py` - that older script points at **net8.0** and predates the mandatory game path, so it
must not be copied as-is). Mean brightness 84.7 to 128.6, so the game was presenting; `save/` unchanged.

**The gate renders correctly.** A stone Mayan arch straddles the brick path with its two doors open on
the pillars, carved head-pillars either side, and the board on top reads **"Lost KINGDOM"** legibly. It
meets the ground at the head of the path - not sunk, not floating, no z-fighting - with the hedges and
the ticket booths lining up beneath it. This is the thing the two empty cells beside the entrance were
always missing.

The camera probe re-confirms the attribute map from a third direction: `attr=8` at the entrance,
`attr=144` on the bus road, `attr=148` at the ticket booths.

**Two things NOT confirmed, and not to be claimed:**
- **`orbit <radians>` does nothing to the park camera** - every shot reported `yaw=0.00`, including the
  one sent `orbit 1.571`. So there is still only ONE viewing angle of this work. `camera x y zoom`
  does work. Whatever turns the park camera, it is not that command.
**The traffic lights ARE confirmed on screen** (this supersedes a note here saying they were not).
Magnified 3x from `4_crossings.png`, four light units stand one on each corner where the brick path
crosses the road - two at the park-side crossing and two at the bus-side, the near pair with their
**green lenses** visible, each on a grey post with a black base plate set into the paving. That is the
2x2 arrangement at (47,9),(48,9) and (47,5),(48,5) the test asserts from the node transforms. At the
wide zoom they were genuinely ambiguous and were correctly NOT claimed; the crop settled it.

**The sign is clean at 3x**: "Lost" over "KINGDOM", centred, inside the board's edges, no clipping.
The board carries a faint vertical seam down its middle - that is the join between the `sign1` and
`sign2` panels - and **the lettering crosses it with the glyphs aligned**, which is the real proof the
256x128 board was cut in half and UV-mapped correctly. No original screenshot of a park gate exists to
compare against (the three park reference shots are Dragon, SunGod and ParkInterior), so the seam is
recorded as observed, not as a defect.

**One pre-existing warning is NOT mine and should not be chased here:**
`Could not load texture 'lobby/terrain/textures/signgrab.wct'` fires during the LOBBY load, before any
park is entered, and happened before this change too.

### New test: `OpenTPW.Tests/ParkFixedItemsTests.cs` (3 tests, all passing)

    TheGateStandsOverTheTwoEntranceCells      door01 at EntranceA*10, door02 at (EntranceB+1)*10
    TheTrafficLightsStandOnTheFourCrossingCells   floor(x/10),floor(z/10) of tl01..tl04 == the four
                                                  crossing cells Standard.sam declares
    AnItemsFootprintFileGivesTheCellsItOccupies   (hmp size - 48) / 27, checked against items whose
                                                  own names state their size (1x1east, 2x2rck,
                                                  4x4rock, 5x5rck) and the gate's 6x3

These are worth having because the positions are invisible twice over - absent from the save, and
absent from the models' bounding boxes - so only the node transforms carry them, and only a separate
file (`Standard.sam`) can say whether they are right.

### COMMITTED - >>> SUPERSEDED: THIS WENT OUT LONG AGO. The heading below used to read "local only -
### NOTHING PUSHED", and that stopped being true the same week. <<<

**`alexah/39-park-terrain` is ON THE FORK at `9956df2`** (checked against `git ls-remote origin`
2026-09-14), and the `1711f1d` below is a **pre-amend hash that is on no branch**. Everything through
branch 58 is pushed - see the block at the top of this file. The text that follows is kept for its
evidence about what the commit contained, not as a statement about where it lives.

- **OpenTPW** `alexah/39-park-terrain`: **1711f1d** "Stand the gate and the traffic lights in the park"
  - 4 files, +328: `ParkFixedItems.cs` (new), `ParkFixedItemsTests.cs` (new), `Level.cs` (one line),
    `Game.cs` (ParkLoadSteps 637 -> 671 and its history prose).
- **OpenTPW.FileFormats** `docs/item-footprints`, a FRESH branch off `upstream/master` so it is
  independently mergeable and clear of `docs/md2-sgn-and-lobby-scripts`, which carries **open PR #1**:
  "Document the item footprint file, and correct the save container"
  - `hmp.md` NEW - the 48 + 27*cells rule, the 71-item check, contents marked undecoded.
  - `saves.md` CORRECTED - the three faults this session proved: version-not-magic (400 ships, the page
    demanded 500), the copyright at 824 UTF-16 bytes from offset 5, and the two BILZ dwords. The
    clincher for the copyright is that the old table summed to **1538** where the file type sits at
    **0x604 = 1540**; 4 + 1 + 824 + 711 closes exactly, and the page's 711 was right all along.
  - `sam.md` EXTENDED - where SAM files live, the layering, `Info.Id` bands, the fixed-item keys, the
    footprint overrides, the approach cells, and the RefPack warning.

**Standalone build DONE for 1711f1d**: detached worktree under `/home/alex/.cache/` (never the tmpfs
scratchpad), **128 warnings / 0 errors**, and `git show --stat` confirms the commit carries all four
files including both new ones - so nothing was leaning on an unstaged file. Worktree removed after.
The docs commit needs no build. See [[contribution-branch-layout]].

**PUSHED 2026-09-13 at Alexah's word** ("Go ahead and push to my repos"):

- **OpenTPW.FileFormats** `docs/item-footprints` -> **origin** (github.com/maexah/OpenTPW.FileFormats),
  tip **d985199**, verified present on the remote, now tracking `origin/docs/item-footprints`.
  **No PR opened** - GitHub offered the link and it was deliberately not taken; opening one needs
  asking separately. The branch is off `upstream/master`, so it is clear of PR #1 on
  `docs/md2-sgn-and-lobby-scripts` and is independently mergeable.
- **OpenTPW** `alexah/39-park-terrain` -> **origin** (github.com/maexah/OpenTPW), `a67dc22..72916e4`,
  pushed by named refspec. **Verified after the push**: local and remote both
  `72916e4d7ac60a1c712e69a0dca384ebb7851226`; `main`, `origin/main` and `upstream/main` all still
  **453e779**; `git ls-remote origin 'refs/pull/*'` empty, so **no PR exists on the fork**.
  Both new commits were built ALONE in throwaway worktrees first (128/0, and 71/71 at the tip).

**Push to `origin` explicitly in both repos.** `upstream` is the OpenTPW org on both remotes, and the
docs branch was tracking `upstream/master` at the time - a bare `git push` there would have aimed at
the wrong repository.

### The emulator's field log is the SPEC, and it now lives on real disk

    /home/alex/.cache/tpw-emu-artifacts/fields_005179c0.txt      12 MB, 495,997 rows
    /home/alex/.cache/tpw-emu-artifacts/fields_005179c0.txt.gz   1.6 MB
    /home/alex/.cache/tpw-emu-artifacts/trace.py                 the emulator itself
    /home/alex/.cache/tpw-harnesses/objshot.py                   screenshots the park's placed objects
    /home/alex/.cache/tpw-harnesses/thing_layout.py              derives the thing layout from the field log
    /home/alex/.cache/tpw-wadcat/                                --id / --cat / --list / --bounds / --meshes

**All of these live on real disk on purpose.** The session scratchpad is a 3.2GB tmpfs that has hit
100% and silently truncated a background job that still reported exit 0. Anything that cost hours goes
to `~/.cache`.

**Copied out of the scratchpad deliberately.** That tmpfs hit 100% earlier today and silently
truncated a background job's output while it still reported exit 0 - and this log is hours of
emulation work that specifies the entire World block. It must not live only on a volume that can fill.

Scratchpad cleanup 2026-09-13: removed `fw`, `foliagecap`, `advisor-probe`, `reflcheck`, `light`,
`repo`, `repo2`, `src`, `nsd` - all finished, derived, regenerable (capture frames, throwaway console
projects with bin/obj, copies of the source tree). **90% -> 46% used, 1.8 GB free.** Deliberately KEPT:
`asr` (a Vosk venv - rebuilding needs a network download), `foliage` (8,337 extracted .wct), `park`,
`fov` (the emulator scripts) and `gateshot` (today's screenshots).

**GOTCHA that nearly hid the verification:** `dotnet test` without `OPENTPW_GAME_PATH` **silently skips
every test that reads real game files** - 24 of 68 when this was written, **36 of 80 as of 2026-09-13** -
including all the park tests, and still prints a green "Passed!" with the full total. Always
run it as `OPENTPW_GAME_PATH=/home/alex/Games/TPWorld dotnet test`. See [[opentpw-local-launch]].

The gate doors should animate unasked: `LobbyModel.LoadAnimations` probes `{stem}M{n}.md2` and the
archive holds `gatesm1/m2/m3.MD2`, which match case-insensitively - unlike the terrain's own
`basem.MD2`, whose bare `m` nothing picks up.

**The cause of the mistake is worth more than the fact:** I read a node-local quantity as a
model-space one. When a model format has a node hierarchy, bounds and placement are different
questions - ask `WorldTransform` for where, and bounds only for how big.

(`Bus.MD2` sits at cell (29.7,-11.5), off the map on negative z - it is parked at its spawn and
drives in, so an off-map position is correct rather than a bug.)

Everything from here to the end of this section is the RETRACTED reasoning, kept only so the error
is not repeated:

**Corrected reading of `DontApplyOffset`.** It was tempting to read it as "load at the origin", the
way `ParkTerrain` loads `base.MD2`. That is wrong, and the mesh bounds say so (`wadcat --bounds`):

    base.MD2  (known world-authored)  x -728.0 .. 1184.3     spans the whole island
    gates.MD2                         x  -16.9 ..   43.3     60 units wide == its 6-cell footprint
    Bus.MD2                           x   -4.1 ..    4.1
    lights.MD2                        x   -1.9 ..    0.9     y -21.0 .. -11.0

The key's own comment says it exactly: the **animation** is played relative to world (0,0), "not the
object pos". It is about animation space, not about where the mesh goes. `gates.MD2` is a
local-origin mesh sized to its own footprint and has to be placed at its cells like anything else.

**Five of the six are modelled about their own origin; Gates is the one that is not:**

**MIND THE AXES: in model space `y` is HEIGHT; the ground plane is x (across) and z (depth).**
`lights.MD2` settles it - a traffic light is x 2.8 by z 2.6 on the ground and **y 10 tall**. So a
model's footprint is its x and z extents, and y never enters a cell calculation.

    model         meshes   x (across)        z (depth)         y (height)     ground centre (x,z)
    gates.MD2        3     -16.9 ..  43.3    -12.8 ..  31.4    -34.2 .. 10.7    ( 13.2,  9.3)
    Bus.MD2          3      -4.1 ..   4.1    -15.4 ..  16.1     -4.0 .. 11.2    (  0.0,  0.3)
    lights.MD2       8      -1.9 ..   0.9     -1.3 ..   1.3    -21.0 ..-11.0    ( -0.5,  0.0)
    FERRY.MD2        1     -10.3 ..  10.3    -37.0 ..  37.0     -0.0 .. 31.4    ( -0.0, -0.0)
    Seaplane.MD2     5     -19.9 ..  19.8    -15.0 ..  20.5     -1.8 .. 12.1    ( -0.0,  2.7)
    End.MD2         38     -14.3 ..  14.2    -10.8 ..  14.7     -3.5 ..  8.7    ( -0.0,  2.0)

Every ground centre is ~(0,0) except **Gates at (13.2, 9.3)** - and Gates is precisely the one item
carrying `EngineMapOffsetOverride`. The mesh offset and the override are very likely two halves of
one arrangement, so the other five probably anchor simply at their own cell.

### >>> THE .hmp FORMAT IS SOLVED: an item's FOOTPRINT AREA, exactly <<<

Every item wad carries a `<name>.hmp`, and its size alone gives the footprint:

    size = 48 + 27 * cells          (48-byte header, 27 bytes per cell)

**Tested against all 71 item wads in the jungle - every one fits, no exceptions**, with cell counts
1, 2, 4, 6, 8, 9, 12, 16, 18, 20 and 25. And the filenames confirm the areas independently:

    1x1east, 1x1rck1, 1x1rck2  -> 1 cell      2x1log -> 2      2x2rck -> 4
    4x4rock                    -> 16          5x5rck, 5x5rck2 -> 25
    gates                      -> 18 cells = its declared 6x3

That last line matters: **the gate's 6x3 footprint is now confirmed by a file that knows nothing
about `Gates.sam`.** This is the falsification test the `>>7` and "prefix == id" errors lacked - it
could have failed on any of 71 items and did not.

(Only the AREA is known this way, not the aspect - 12 cells could be 4x3 or 6x2. The `.sam`'s
`Info.Shape` ASCII grid and the Engine*Override keys are where the aspect comes from.)

### THEMENAMES.str: the order is CONFIRMED, and readable without the decoder

[[theme-names-and-locale-tables]] said the four park names are in the data and gave their order.
**Verified from the file's own header, no BFST decoding needed** - `data/Language/English/THEMENAMES.str`,
112 bytes, count 4, offsets 0x10/0x24/0x3c/0x50:

    record len  space at   only name that fits
      0     12      4      "Lost"      + "Kingdom"    -> jungle
      1     15      9      "Halloween" + "World"      -> hallow
      2     11      6      "Wonder"    + "Land"       -> fantasy
      3     10      5      "Space"     + "Zone"       -> space

All four lengths AND all four space positions match, and the character indices are self-consistent
across records (`o`=0xa5, `e`=0xaf, `a`=0xb3 recur in the right places). So the order is
**jungle, hallow, fantasy, space** - which is NOT alphabetical and not the lobby's island order.

This is the cheap way to sanity-check any BFST file: the header's length table is plain, even though
the payload is indices into `MBToUni.dat` and shows nothing to `grep`.

### Mesh bounds are NODE-LOCAL - placement lives in the node hierarchy

`ModelFile.Mesh.BoundsMin/Max` say how big a mesh is, **not where it stands**. A query for terrain
meshes "near the entrance" by bounds returned 0 of 272, which was my error and not an empty region.
The placement is `Mesh.WorldTransform` (the mesh's own transform with every ancestor's applied).

**The terrain's mesh names are meaningful and usable**: `road_center`, `arrival_roof`,
`arrival_base`, `arrival_roof_support`, `depart_roof`, `depart_base`, `flag_pole07` - the bus
shelters and the approach road, by name. That is the route to calibrating cell->world against
something whose cells are already known.

**Gates' x extent is 60.2 units = exactly its 6-cell width**, which is the one solid link between
the mesh and the declared footprint. Its z extent is 44.2 against a declared 3-cell (30 unit) depth,
so the arch overhangs the cells it blocks - a footprint is what an item OCCUPIES, not its bounding box.

**An UNADOPTED candidate, recorded so it is not rediscovered as if new:** anchoring at
`FixedItemOrigin` (480,170) minus the mesh centre (13.2,-11.7) puts the mesh at x 449.9..510.1,
matching the footprint's 450..510 to 0.1 units. **Do not use this yet.** The y span does not land as
cleanly, and Gates is the ONLY item with overrides, so nothing can refute it from the .sam side -
a one-sample rule of exactly the kind that produced `>>7` and "prefix == id".

**The falsification test to run instead:** `base.MD2` carries the entrance road and the ticket booths
as world-positioned meshes, and the booths' cells are known independently ((47,13)/(48,13), attribute
148). Dumping per-mesh names and bounds (`wadcat --meshes`) calibrates cell->world against an item
whose answer is already known, which either confirms the arithmetic or kills it.

**Still open: the exact anchor.** The footprint is cells x 45..50, y 16..18 (world x 450..510,
y 160..180) but the mesh spans x -16.9..43.3 about its own origin, so the anchor is neither the
footprint corner nor the mesh centre without an offset. Do not guess this one - the `>>7` and
"prefix == id" mistakes both came from adopting the first plausible rule. Settle it against the
original (the terrain's own entrance road in `base.MD2` is the reference) or by rendering and
comparing with the reference screenshots.

Worth knowing before touching any of this:
- **`RideInfo.cs` already lists every one of these .sam keys** under the original's names, and
  `Ride.cs` is the unbuilt ride runtime. Both say plainly that nothing reads them. Do not mistake
  either for a foundation.
- **`ParkTerrainTests` already proves the fixed-item positions** - `FixedItemsLandOnTheirOwnAttributes`
  and `TheEntranceAndTheBusRoadReadBackTheirKnownValues` (entrance 8, bus road 144, booths 148,
  axes-swapped lookup asserted 0). That work is done and passing; do not re-add it.
- `ParkBalance` already layers global + theme `Standard.sam`, so `Balance.Int("FixedItemInfo.
  EntranceAPosX")` works today. It deliberately skips `Easy_Standard.sam`, which is correct - that
  file overrides only four `LoanInfo` lender names and no positions.

This also **re-confirms `>>8` from a second direction**: the guests sit on the road between
CrossingParkSide (y=9) and the Entrance (y=17), passing the TicketBooths (y=13), and the researcher
stands exactly on EntranceB (48,17). A wrong unit does not put people on a corridor named by a
different file.

**Correction: the thing-model list was incomplete.** The histogram runs to **model 19** (models 11-19
each appear once), so the earlier "models 1, 3-10, no case 2" came from a truncated decompile window,
not the whole switch.

### The complete thing table - SUPERSEDED

**Everything that was in this section was built on two mistakes** - `cell = mX >> 7` and
"prefix == slot index" - so both its cell ranges and its slot numbering were wrong. The corrected,
verified table is the one under "THE PARK IS FULLY DECODED" above. Do not reconstruct this one.

**RETRACTED: `cell = mX >> 7` is WRONG.** It was "proved" on a single sample (slot 12) and that was
too thin. Checking it against `base.map` refutes it: the attribute map's non-zero cells span exactly
**x 0..95, y 0..84** - the terrain's own 96x85 extent - and the entrance (47,17) reads attribute
**8** as expected, but the things' `>>7` cells mostly read attribute **0** and several (103, 111,
115, 117) fall **outside the populated region altogether**. A correct unit cannot put objects off the
map.

**`>>8` is CONFIRMED** (see the shift sweep and the balance-file cross-check below; mX/mY are in
**1/256 of a cell**): the guests read
mX 12108..12396, and `12228 >> 8 = 47` - exactly the entrance's cell x - with mY 2491..4411 giving
y 9..17, which reads as guests walking from the bus road up to the entrance at y=17. The sentinel
also becomes a cleaner `(0,0)` instead of `(1,1)`.

**Lesson worth keeping: one sample is not a proof of a unit.** Test a unit by whether EVERY value it
produces lands somewhere legal, and cross-check against an independent file - here `base.map`, whose
populated extent is itself the answer key.

### >>> CONFIRMED: positions are in 1/256 of a cell, `cell = value >> 8` <<<

A shift sweep discriminates properly, which is what the single-sample test never did:

    shift   inside 96x85   on a non-zero attribute
      5        0/29                0
      6        0/29                0
      7       15/29                8      <- the old, wrong claim
      8       29/29               14      <- everything legal AND most meaningful
      9       29/29                2      (fits only by compressing toward the origin)
     10       29/29                0

**And the semantics clinch it.** Every guest stands on attribute **144 or 148 - the bus road** -
clustered at x 47..48, y 9..15, walking up toward the entrance at **y=17**; and the researcher
(slot 29, model 8) sits at **(48,17) on attribute 8, the entrance itself**. That is exactly where
those people belong in a park about to open, and it is a result `>>7` could not produce.

So a thing's world position is `mX/256` cells, and a cell is 10 world units - i.e. one mX unit is
10/256 world units. The sentinel 128/128 becomes cell (0,0).

**The gate (slot 11) stays at the sentinel**, which is consistent with `mParkGates` being a handle to
the gate SYSTEM rather than to placed geometry. Slots 12-23 are the model-3 things that DO carry real
positions - those are the candidates for placed features, and the next question is what identifies
which feature each one is.

**Why the model-3 record kept crashing the emulator:** `FUN_005fc5c0` is 158 bytes whose only calls
are `SystemTimeToFileTime` and 0x006fd150 - a **Win32 API unicorn cannot provide**. It performs no
stream reads, so stubbing it (pop 0x20) is safe and cannot swallow fields.

**With all three traps fixed the tracer runs clean - no stops, 0 pages mapped on demand - and it
CONFIRMED EVERY HAND-DERIVED SIZE EXACTLY:**

    person base FUN_004f8b10   390      model 5 handyman    FUN_004d6d60   505
    model 1 guest FUN_004fb530 525      model 6 entertainer FUN_004d4460   501
    staff base FUN_00504de0    495      model 7 guard       FUN_004d6000   503
    model 4 mechanic FUN_004da110 503   model 8 researcher  FUN_00502760   501

**This forces a RETRACTION recorded further up this file.** After the sequential walk failed I wrote
that "505 is not the handyman's on-disk size, and by extension the other computed sizes are wrong
too". **That was wrong - every size was right.** So the walk's failure is in the STREAM LAYOUT, not
the sizes: the 8-byte `[Used_Thing_Next][thingmodel]` prefix, or where the header actually ends.

**The way to settle it: serve the REAL payload bytes to the tracer** (`trace.py <func> <mode>
<payload-file> <start-hex>`). Fed zeros, a reader takes the branches zeros dictate; fed the actual
file it takes the branches the DATA dictates - which matters enormously here, because the thing loop
is a `switch` on a model number that has to come out of the file. Trace `FUN_005179c0` (the World
loader) that way and it parses the stream itself, giving the header length, the prefix, and every
object's `mX`/`mY` - including the gate's.
2. **Divisor helpers must return non-zero.** The person base does `mSpriteID %= FUN_00541f60(...)`;
   against zeroed memory that is a divide by zero (seen as a generic CPU exception at `DIV ECX`,
   0x4f93b9).
3. **SEH needs a TIB.** MSVC's try/except prologue opens `MOV EAX, FS:[0]`; with no FS base that
   faults (0x510107). Map a page and set `UC_X86_REG_FS_BASE`. Mapping pages on demand can never
   fix this - it is an FS-relative access, not a linear address.

**Gated on known answers before being believed**, as the FOV work was: `FUN_0050b090` must give
mX/mY/mMapChild/mMapParent = 4 fields / 8 bytes, and `FUN_004da960` those plus mNextObject = 5 / 10.
Both reproduce exactly, every run.

**It has independently CONFIRMED five hand-derived numbers** - `FUN_004d3ee0` 12, `FUN_0050bad0` 144,
`FUN_0050d150` **177**, model 9 **95**, and the **person base 390** - and settled model 3:
`FUN_0051ade0` declares 0 fields and reads nothing, so it really is a constructor.

**Still open:** the four outer readers (`FUN_004f8b10`, `FUN_00504de0`, `FUN_004fb530`,
`FUN_004d6d60`) all stop at the person base's end (92 fields / 390 bytes) because of trap 1 above.
Fix that stub's pop and they should continue into their own fields, giving every model's exact
on-disk size - and then the thing stream can finally be walked to object id 11, the gate.

### >>> WHY ALL FIVE WALK ATTEMPTS FAILED: the MAP comes first <<<

Tracing `FUN_005179c0` against the real payload settled it, and the emulator reports the **file
offset of every field**, so this is measured rather than inferred:

    rows  0-25   0x0049b..0x004db   the header - the 26 fields already decoded, mParkGates at 0x4b9
    rows 26-175  0x004db..0x0179b   mObjectControls[i] x 150, 32 bytes each = 4800 bytes
    row  176     0x0179b            mNumObjectControls  4
    row  177     0x0179f            mPreviousSearchKey  2
    row  178+    0x017a1..          the MAP begins

**That `mObjectControls[i]` array is why "the header ends at 0x4DF" was wrong** - 150 entries sit
between `mFirstObject` and everything after it. The header decode itself was right.

**The map is NOT a fixed-stride tile array - tiles are POLYMORPHIC, exactly like things.** Every tile
record ends with a common tail and begins with a head chosen by `mType`:

    common tail   mDirection 1 | mFlags 2 | mMeshInstance 4 | mNeighbours 1 |
                  mOverlapCounter 2 | mParentID 2 | mTileData 12
    52-byte kind  mType 4 | mHoardingNeighbours 1 | mLitter 4 | mLitterCollector 2 |
                  mLitterScript 4 x2 | mPylonIndex 2 | mStatusFlags 1 |
                  mTimeMarkedForLitterCollection 4 | mWho 2 | <tail>
    32-byte kind  mType 4 | mHoardingNeighbours 1 | mSegmentNumber 2 | save_status_byte 1 | <tail>

In the shipped park those two **strictly alternate** (52, 32, 52, 32, ...) after an opening run of
**31 records of 20 bytes** - `mType 4 | mName 4 | mPayGrade 1 | mSubType 1 | mValid 1 | mOnPointer 1 |
mTimeSig 4 | mTimeoutTime 4` - which are staff/job records, not tiles, and then one 121-byte record.

**`mTileData` is 12 bytes**, confirming the figure the old save note carried.

**The readers behind each of those, from the exe - and they agree with the trace exactly:**

    FUN_004d0b30   the shared TILE BASE - mDirection, mFlags, mMeshInstance, mNeighbours,
                   mOverlapCounter, mParentID, mTileData. Both kinds call it, which is precisely
                   the common tail the trace measured.
    FUN_004d8dd0   the 52-byte kind (owns mPylonIndex, mLitterScript, mLitter, ...)
    FUN_0050c200   the 32-byte kind (owns mSegmentNumber)
    FUN_004d3aa0   the mObjectControls[i] array, mNumObjectControls, mPreviousSearchKey - the
                   150 x 32-byte block sitting between the header and the map

Two independent methods landing on the same structure - the emulated field trace and the string
xrefs - is the strongest confirmation available here, and it is worth more than either alone.

### >>> THE MAP LOOP IS LOCATED AND COUNTED: 16,384 cells, 0x001A6D to 0x1523DD <<<

Measured from the field log, not inferred: **exactly 16,384 `save_status_byte` rows**, the first at
**0x001A6D** and the last at **0x1523DD**. So the map occupies almost the whole span between the
World header and the thing stream at 0x152435, and the 0x4000 bound is confirmed by the data as well
as by the code.

`FUN_004d7ea0`'s READ path decompiles to exactly this, which is the whole structure:

    do {
        read 1 byte -> b                  // "save_status_byte"
        if (b & 1) FUN_004d8dd0(...)      // map cell
        if (b & 2) FUN_0050c200(...)      // track cell
        if (b & 4) FUN_00502490(...)      // RE cell
    } while (++i < 0x4000);

**Two variants account for every cell in the shipped park:**

    gate 0x03   84 bytes   16,133 cells    map + track
    gate 0x07   94 bytes      250 cells    map + track + RE
    (+ the final cell, which has no successor to measure against)   16,133 + 250 + 1 = 16,384

So the **RE cell is 10 bytes** (94 - 84), and no cell in this park has bit 1 or bit 2 clear.

**RESOLVED - the track cell is 31 bytes, not 32.** The field-level split settles it, and the earlier
"52 and 32" note was wrong on the second figure. The full per-cell spec, which is what a port needs:

    u8 save_status_byte
    if (gate & 1)  map cell    52 bytes
    if (gate & 2)  track cell  31 bytes
    if (gate & 4)  RE cell     10 bytes   (5 x u16 mpEffect[i])

**Map and track share a 29-byte prefix**, which is why they are easy to confuse:

    shared 29:  mDirection 1, mFlags 2, mMeshInstance 4, mNeighbours 1, mOverlapCounter 2,
                mParentID 2, mTileData 12, mType 4, mHoardingNeighbours 1
    map adds 23:  mLitter 4, mLitterCollector 2, mLitterScript 4, mLitterScript 4, mPylonIndex 2,
                  mStatusFlags 1, mTimeMarkedForLitterCollection 4, mWho 2      -> 52
    track adds 2: mSegmentNumber 2                                              -> 31

1 + 52 + 31 = 84 and +10 = 94, matching both measured variants exactly.

### >>> `Used Thing Head` = 42, at 0x152431 - the last gap is closed <<<

The map's span did not quite account for itself: 0x1A6D to the first thing at 0x152435 is 1,378,760
bytes, but 16,134 x 84 + 250 x 94 = 1,378,756. **The missing 4 bytes are a u32 `Used Thing Head`
immediately before the thing stream**, and in the shipped park it reads **42**.

    16,134 x 84  +  250 x 94  +  4  =  1,378,760      exactly the measured span

That is the head of the linked list, and it closes the one hole left in the thing model: the first
record (a model-1 guest at 0x152435) has own id **42** and stores `next = 41`, so the ids run
42, 41, 40 ... down to 1 with the Bus (15) spliced in, and the last record's `next = 0` terminates.
**42 things, ids 1..42, every one accounted for.**

It is also a THIRD independent confirmation that the per-thing prefix is a *next* pointer rather than
an id: a file that stored each thing's own id would have no reason to carry an explicit head pointer.

### The complete World-block walk, as a port would need it

**COMPLETE, and every boundary closes exactly** - checked one at a time, no gaps and no guesses:

    0x00049B  version u32, then 25 more header fields              -> 0x0004DB
              (mParkGates and mTrafficLights are two of them, u16 thing ids)
    0x0004DB  mObjectControls[150], 32 bytes each = 4800 (0x12C0)  -> 0x00179B
    0x00179B  mNumObjectControls u32, mPreviousSearchKey u16       -> 0x0017A1
    0x0017A1  vacancy record x32, 20 bytes each = 640 (0x280)      -> 0x001A21
              mType 4, mName 4, mPayGrade 1, mSubType 1, mValid 1,
              mOnPointer 1, mTimeSig 4, mTimeoutTime 4
    0x001A21  group x5, 5 bytes each = 25                          -> 0x001A3A
              mPeopleInCat[i] 4, mStopProducing[i] 1
    0x001A3A  mTimeSig 4, mOpeningStaffPoolGenerated 1, mFunnyTimeStart 8,
              mSessionStart 8, mMonthAtLastUpdate 4, mDayAtLastUpdate 4,
              mFunnySecsPerRealSec 4, mArrivalRate 4        = 37    -> 0x001A5F
    0x001A5F  mTimeSig 4, mTargetVehicleCapacity 4, mPeopleOnBus 4,
              mOffloading 1, mGatesOpen 1                   = 14    -> 0x001A6D
    0x001A6D  16,384 x { u8 gate; &1 -> 52B map cell; &2 -> 31B track cell;
                         &4 -> 10B RE cell }                        -> 0x152431
    0x152431  u32 Used Thing Head = 42                              -> 0x152435
    0x152435  the thing list - 16-byte common header, then model-specific fields
    0x16D1A6  the trailer, stored as `DLRW`

**Correction to an earlier note in this file: the record at 0x17A1 is 20 bytes, not 22.** 32 x 20 =
640 lands exactly on 0x1A21; 22 does not. Quick arithmetic in a message is not a measurement.

That is everything a reader needs. The remaining work is writing it into `OpenTPW.Files` (legacy
format at the boundary), exposing only what placing objects needs - id, model, mX, mY, mAngle, mId -
and asserting the walk ends on the tag.

### Thing record sizes: what is PROVEN and what is only observed

A size census printed "all fixed", and that **overstates it**. Only two models have enough instances
to demonstrate anything:

    model 1   13 instances   all 533 bytes    <- genuinely fixed
    model 3   14 instances   all 1099 bytes   <- genuinely fixed
    every other model (4,5,6,7,8,10,11,12,13,14,15,16,17,19): exactly ONE instance

Fifteen of the seventeen sizes are therefore **single observations, not proofs** - the same shape of
evidence that produced the `>>7` and "prefix == id" mistakes. Treat them as measurements of this file.

There is specific reason to doubt one of them. **Model 13 is 73,544 bytes and opens with
`mMapSquaresPerCell, mGridSizeX, mGridSizeY, mMapSizeX, mMapSizeY`** - its length is very likely
DERIVED from the grid, and fixed only because every park is 96x85. Models 11 (5,846) and 14 (4,190)
read as fixed-capacity arrays. And only one save file exists to test against, so generality cannot be
checked here at all.

**The safeguard is built in: the walk must land exactly on the `WRLD` trailer.** Every size in the
chain feeds one running offset, so a port that ends on the tag had every size right, and one that is
a single byte out cannot. Make that an assertion in the reader rather than a hope - it is the same
kind of end-to-end check that `SaveReader` already uses for the container (block length reaching the
end of the file, payload inflating to its declared size).

**VERIFIED: the gap is 0.** The last field of the block - `mHaveEverTrained`, the final byte of the
model-9 record - ends at exactly **0x16D1A6**, where the tag sits. The property is real, not hoped for.

> **The tag is stored BYTE-REVERSED, and this will waste someone's afternoon.** The four bytes at
> 0x16D1A6 are `44 4C 52 57`, which read as **"DLRW"** in file order. Searching the payload for
> `b"WRLD"` finds **nothing**. The next module's tag follows immediately at 0x16D1AA as
> `54 50 43 53` = "TPCS", i.e. `SCPT` reversed. So every module trailer is a little-endian dword of
> its four-character name, exactly as recorded - but do not grep for the readable spelling.

**The modules butt directly against one another**, with no length prefix or padding between them: at
0x16D1A6 the bytes run `DLRW` `TPCS` and then straight into SCPT's own body (`0x118`, `0x64`, then a
run of pointer-shaped dwords `0x04cf8580`, `0x04cf1480`, ...). So a reader finishes one module exactly
on its tag and the next module begins at tag+4 - which is why landing on the tag is the only check
needed, and why being one byte out corrupts every module that follows rather than just the current one.

### The map loop is `FUN_004d7ea0`, and its bound is a LITERAL 0x4000

It calls both tile readers (`FUN_004d8dd0` at 0x4d8024 and 0x4d8234, `FUN_0050c200` at 0x4d8060 and
0x4d8251) and its loop control is **`CMP EBX,0x4000` / `JGE`** at 0x4d80b5 and 0x4d8289.
**0x4000 = 16,384 = 128 x 128** - the gameplay grid's tile count, hard-coded, confirmed from the loop
itself rather than inferred from the map's shape.

**RETRACTION: "the tile loop over-iterates under emulation" was WRONG.** A bound that is a literal
cannot run long because engine globals are zeroed.

**And the follow-up guess of "two passes over the map" was wrong too.** Decompiling `FUN_004d7ea0`
shows ONE loop of 0x4000, and the doubled call sites are just its save branch and its load branch.
The load branch is:

    do {
        read 1 byte -> save_status_byte
        if (b & 1)  FUN_004d8dd0(...)    // map cell    - the 52-byte kind
        if (b & 2)  FUN_0050c200(...)    // track cell  - the 32-byte kind
        if (b & 4)  FUN_00502490(...)    // "RE" cell   - a THIRD kind
    } while (++i < 0x4000);

**So every one of the 16,384 cells writes exactly one status byte, and then only the sub-records its
bits call for.** Default cells write nothing else at all - the save side logs "Saved %d (%d%%) map
cells as default", and the same for track and RE cells. That is the real source of the variable
length, and it is why no fixed stride was ever going to walk this.

### >>> CORRECTED: the thing stream starts at 0x152435, not 0x154143 <<<

**0x154143 is only the first MODEL-3 record**, several things into the list - the stream itself begins
at **0x152435** with a model-1 guest. Re-derived from the field log by taking the first
`Used Thing Next` row rather than the first record that happened to be an object.

**And "the map is at 0x17A1" is wrong too.** The rows at 0x17A1 are a repeating 22-byte record -
`mType(4) mName(4) mPayGrade(1) mSubType(1) mValid(1) mOnPointer(1) mTimeSig(4) mTimeoutTime(4)` -
which is a staff-vacancy-shaped array, not a map cell. Do not build a walk on that offset.

### >>> EVERY THING SHARES A 16-BYTE HEADER <<<

Confirmed across all seventeen models present in the shipped park (1, 3-17, 19):

    +0  4  Used Thing Next     the NEXT thing's id (see the retraction above)
    +4  4  thingmodel
    +8  2  mX                  1/256 of a cell
    +10 2  mY
    +12 2  mMapChild
    +14 2  mMapParent

After that they diverge completely, and the sizes are wildly uneven: models 12 and 17 are **16 bytes**
(header only), model 3 is **1,099**, model 11 is **5,846**, model 14 is **4,190**, and model 13 is
**73,544**. So a reader cannot stride the stream - it has to parse each model, which is why the walk
has to be ported rather than guessed.

Model 3, the placeable catalogue object, continues `mAngle(4)` at +16 and `mId(2)` at +20 - exactly
the offsets used to identify all fourteen of them.

### (superseded heading) THE THING STREAM IS FOUND

The uncapped trace read **exactly 16,384 `save_status_byte`s** - the map loop completed in full - and
then went straight into the things. The first record reads:

    0x0154143  4  Used Thing Next
    0x0154147  4  thingmodel
    0x015414b  2  mX          0x015414d  2  mY
    0x015414f  2  mMapChild   0x0154151  2  mMapParent
    0x0154153  4  mAngle      0x0154157  2  mId      0x0154159  4  tv[t] x8 ...

**This confirms the 8-byte `[Used Thing Next][thingmodel]` prefix** that was derived from brace depth,
and it places the stream immediately after the map rather than at 0x4DF.

**A naming trap that cost a grep: the debug name is `Used Thing Next` with SPACES**, not
`Used_Thing_Next`. Searching for the underscored form reports zero and looks like the stream is
absent when it is right there.

Note `mAngle`, `mId` and `tv[t]` are **not** model 10's fields, so these first records are some other
model - which is why `trace.py`'s object detector (which matches mX, mY, mMapChild, mMapParent,
mNextObject consecutively) correctly found none. The run died ~15 things in, inside `FUN_004db7d0`.

**The 15 things it did reach, decoded from the log against the payload:**

    13 x model 1 (guest)      533 bytes each, from 0x152435
     1 x model 8 (researcher)  509 bytes, at 0x153d31
     1 x model 3               54 bytes, at 0x154143  (mId = 1600)

**Those in-stream sizes are the isolated traces PLUS the 8-byte prefix** - 525+8 = 533 and 501+8 =
509 - which confirms the per-record sizes and the prefix at the same time, by two routes.

**CAUTION: `mX`/`mY` are NOT cell indices.** The guests read mX ~12100-12400 and mY ~2500-4400, far
outside a 0..127 grid, so they are sub-cell or world units. A shift looks plausible (12228 >> 7 = 95,
2544 >> 7 = 19) but is **unverified** - do not treat "every object carries its map cell" as meaning a
plain cell number until the unit is pinned down.

**This variable length is why no fixed stride ever walked the stream**, and it applies to the map as
much as to the things. Anything that assumes a constant record size here is wrong by construction.

**So the error behind every failed walk was POSITION, not size.** The header decode was right, every
record size was right (all confirmed by emulation), and the single bad assumption was that the things
began at 0x4DF. They do not: `Used_Thing_Head` / `Used_Thing_Next` / `thingmodel` are not sitting at
the header's end in the file, and reading the dwords there as a thing prefix was meaningless.

This also retires an old open question: **`mTileData` is no longer "untraced"** - it is a named field
read once per tile, inside that group.

The uncapped run consumed **1,391,838 bytes** and declared **462,384 fields** before dying on a wild
jump (`FETCH_UNMAPPED at eip=0x3248b2`), and it never read `thingmodel` once.

**I first called this "the tile loop OVER-ITERATES under emulation", reasoning that its bound came
from zeroed engine globals. That was WRONG** - see the map-loop section below: the bound is a literal
`0x4000`, and the real structure is one loop over 16,384 cells in which each cell may write up to
three optional sub-records. The record count is explained by that, not by any fault.

**Corroborating that: the last ~102KB of the World block is `cd cd cd cd ...`** - MSVC's
uninitialised-heap fill - so that tail is not a thing stream, it is never-initialised memory
serialised verbatim, which fits the payload being a memory image. Walking it as things yields exactly
one record.

**So the fix is to initialise the map-dimension globals before running**, not to keep hunting the
bytes. Find what bounds the tile loop in `FUN_005179c0`, set it, and the loop will stop where the real
game stops - after which `trace.py` pulls the model-10 records out itself by matching the five
consecutive fields `mX, mY, mMapChild, mMapParent, mNextObject` and printing their real values, which
is the gate.

The record of everything before the park is [[project-history-archive]] - **grep it, never read it whole**.
