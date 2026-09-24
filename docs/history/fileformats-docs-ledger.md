---
name: opentpw-fileformats-docs
description: Format docs live in a sibling clone of OpenTPW.FileFormats and are updated in the same session a format fact is confirmed
metadata:
  pathNote: "**THE DOCS ARE AN ASTRO/STARLIGHT SITE - the markdown is under `src/content/docs/`, NOT at the repo root.** A bare `vm/instructions.md` does NOT exist; it is `src/content/docs/vm/instructions.md` (1,025 lines), and the format pages are `src/content/docs/formats/*.md`. Corrected 2026-09-18 after a path from this file failed. Also: `find` in that clone MUST exclude `node_modules`, which holds ~450 unrelated readmes."
  node_type: memory
  type: project
  originSessionId: f4cf68d5-1d56-4918-adad-a3f0095ef62d
  modified: 2026-09-22T04:22:20.711Z
---

Alexah asked (2026-09-08) for format knowledge to exist as maintainable, human-readable
documentation rather than only in code comments, and reaffirmed it 2026-09-10: keep the docs
current "as stuff is confirmed".

> **WHAT COUNTS AS A FORMAT FOR THIS SITE - settled 2026-09-14 so it is not re-litigated.** The rule
> fired on the park HUD work and correctly came to **nothing to do**. Two things were decoded that
> session and **neither belongs here**:
> - the **compiled layout-stream opcode format** (`UI_ParseTreeStream`, 0x0065fd58) - it lives *inside
>   testme.exe*, not in any shipped file, so it is not a file format. It is in `docs/exe/hud.md`.
> - the **mesh-name hash** `h = (c ^ h) * 47` - how the engine *addresses* a ui.wad model, by the name
>   of its first mesh node, rather than how the container is laid out.
>
> `strings.md` was checked at the same time and is **not stale**: it documents BFST and BFUM correctly
> and already names `MBtoUNI.dat`. The related gotcha - that `BFSTReader`'s **static** field loads that
> file through the global file system, so reading a `.str` outside the game needs `FileSystem` mounted
> first or the type initialiser aborts - is a **codebase** fact, not a format fact, and lives in
> `docs/exe/hud.md`.
>
> **The test:** does it describe bytes in a file the game ships? Then it belongs here. Does it describe
> what the executable does with them? Then it belongs in an exe memory.

The Astro/Starlight site is cloned as a sibling of the main repo at
`/home/alex/repos/OpenTPW.FileFormats` — not a submodule, not referenced from OpenTPW's git
tree. Pages live in `src/content/docs/formats/*.md`; the sidebar autogenerates from that
directory, so a new page only needs `title: <Name> (*.ext)` frontmatter. Match the existing pages'
style (plain GFM tables, `>` blockquote warnings), and use the internal-link form they already use:
`](/formats/texture/)`.

> **CORRECTED 2026-09-14: THE SITE *CAN* BE BUILT HERE.** This note used to say "there is no node/npm
> here, so the site cannot be built or previewed" and that is **false** - it only looked true because
> **node is not on `PATH`**. It is installed under nvm, `node_modules` is already present, and the site
> builds in about two seconds:
>
>     PATH="$HOME/.nvm/versions/node/v20.19.0/bin:$PATH" npm run build     # in the docs clone
>
> A clean run says `13 page(s) built` and `Complete!` **as of 2026-09-22, against 12 source pages** -
> this line said `16` from 2026-09-14 until then, and the figure is the sort that goes stale whenever a
> page is added or removed. **A count BELOW the one written here is not evidence your edit broke the
> build**: check `dist/formats/<page>/index.html` exists and greps for your own new text, which is the
> direct check, rather than reading the total. **The one warning it prints is pre-existing and
> not yours**: `[@astrojs/sitemap] The Sitemap integration requires the 'site' astro.config option`.
> **Build after editing** rather than only reading the file back - it is the cheap check that a page
> still renders. `command -v npm` returning nothing is not evidence the toolchain is absent.

**Cadence.** When a format is newly decoded, or an existing understanding changes in OpenTPW
proper, mirror it into the corresponding page in the same session. Assert only what has been
verified against the real game data and mark everything else as an explicit open question —
the sound pages are the model: they say which half of the SFX map is decoded and which is
not, and why the sample records are found by validation rather than by offset. Findings that
are content rather than layout still belong there if an implementer would trip over them.

**Branches.** `origin` is Alexah's fork and push access works, but nothing is pushed without
asking first — see `CLAUDE.md` rule 1. When a push is approved, never push to a branch that has
an open pull request — it silently expands a PR that is out for review into something its
title no longer describes. New docs go on a fresh branch off `upstream/master` so they are
independently mergeable.

> **>>> THE 2026-09-21 BRANCH RULE CHANGE DOES NOT APPLY TO THIS CLONE. <<<** That day Alexah decided
> that **OpenTPW's** fork `main` stops being a mirror of upstream and becomes the tip, with tasks
> branching off it and fast-forwarding back in. **This repository is untouched by that.** It has
> `master`, not `main`; its default is still the upstream mirror at `0e8d5d0`; new docs still branch
> off `upstream/master` so each is independently mergeable; and **PR #1 on
> `docs/md2-sgn-and-lobby-scripts` is still off limits for pushes**. Do not carry the OpenTPW model
> across — the two repos are deliberately different, because this one feeds pull requests upstream
> and that one does not. As of 2026-09-10: `docs/md2-sgn-and-lobby-scripts` carries open PR #1
and is off limits for pushes; `docs/sound-formats` is separate and has no PR;
`docs/advisor-model-formats` is stacked on PR #1's branch, pushed 2026-09-10, no PR.
2026-09-11: `docs/interface-fonts` (fonts.md) and `docs/particles` (particles.md, sprites.md), both off
upstream/master, pushed at Alexah's word, no PR.
2026-09-11: `docs/save-files` (options-and-players.md: Config.tcf, player folders, gms.dat) off upstream/master,
committed 7365ff9, pushed to origin 2026-09-11 at Alexah's word ("commit and push to my repos"), now tracking
origin, no PR.
2026-09-12: **`docs/advisor-model-formats`, two commits, PUSHED to origin** at Alexah's word ("Settle the
contradiction and push please"), tip **19ee4d2**, verified local == remote, no PR. The branch is still stacked on
`docs/md2-sgn-and-lobby-scripts`, which carries **open PR #1** - pushing this branch does not touch that PR, but
say so when asking again.
- **0b845d3** - models.md gains the node name blob (strings packed end to end, no header pointer, per-record
  +0x54 the only way in), identifies the previously-unknown word at **+0x50** as the record's own index (7,521 of
  7,530 records; +0x52 zero in all but 3), marks the 0x54 row engine-confirmed from the loader relocating exactly
  +0x04/+0x08/+0x0C/+0x54 per node record, retires 0x50 from the mesh table's open questions, and warns about
  `wr_tunnel.md2`.
- **19ee4d2** - settles a contradiction the page carried. It said "About 1.7% of nodes (130 of 7533) are sheared"
  with **no test attached**, and neither figure survives measurement.

**The shear numbers, measured so nobody pays for them twice** (`scratchpad/nodesweep/{shear,dump_mats,collapsed}.py`
and `sheardec/`): the corpus is **7,530** node records over 839 static models = 4,924 mesh + 2,606 transform-only
(closes exactly, and the page's own 2,606 agreed all along). "Out of square" = largest |cos| between any two basis
axes. Counts: **120** over 0.01, **165** over 0.005, **171** over 0.001, **172** over 1e-4. **.NET's
`Matrix4x4.Decompose` refuses 45** - a different question, and the one that matters to anyone decomposing. The
distribution is sharply bimodal (median |cos| ~1e-21), so the skewed ones are badly skewed: the mildest matrix
Decompose refuses is already 0.11 out of square. `Jun_isle`'s palm trunk is **node 4 at 0.43**, one of the 45 -
the anchor the page already named, and it holds. Ten records have a **collapsed** axis (zero-length basis vector),
all of them `wr_tunnel.md2` nodes 0-9, and are degenerate rather than sheared. **No threshold yields 130** - do not
reinstate it.
2026-09-13: **`docs/item-footprints`**, off upstream/master - the park work's formats, four commits.
`d985199` the `.hmp` footprint file plus a correction to the save container, `b2b4c13` the park save's
module map and its World block, `f1c827e` the `.md2` model and animation page. **Those three are PUSHED**
(origin's tip is f1c827e), at two separate yeses, **no PR**.
**`163e374` is PUSHED** (2026-09-13, at Alexah's word, `f1c827e..163e374`, no PR, local == remote
verified): the World block's map cells - the 29-byte tile base field by field, the `y * 128 + x` cell
order, and `mTileData` as three dwords (tile set, index, rotation). It is where a park's paths live.

**`7cc1abe` is PUSHED too** (2026-09-13, same branch, `163e374..7cc1abe`, no PR, local == remote
verified, and PR #1's branch `docs/md2-sgn-and-lobby-scripts` confirmed untouched at d73a627): a **new
`formats/tct.md`** for the texture correspondence table - Jungle's full table, the section-and-tab
format, the commented-out `#Water` section a careless reader invents, the `.tga`/`.wct` swap, and
`QueueTex`'s rows not being in numeric order. It also records what a tile index MEANS, cross-checked
against each cell's measured neighbour shape, which `saves.md` could previously only call a reading.
The queue's index-5 puzzle is written down as an explicit open lead rather than guessed.

**`aa2c7b6` and `cd776e5` are PUSHED** (2026-09-13, same `docs/item-footprints` branch,
`7cc1abe..cd776e5`, no PR, local == remote verified, PR #1's branch confirmed untouched at d73a627).
`aa2c7b6` closes
that puzzle and two more. `tct.md` now says a queue index names a MODEL out of the theme's `queue.wad`,
with the exe's own seven-entry table at 0x76338c, and records `FUN_005365d0` confirming the three-dword
tile split and the `y * 128 + x` cell order from the engine rather than from the data. `saves.md` gains
mTypes 4/9/10 as one footprint family (44 cells = the eleven placed objects), the item floor plates that
are why the ground must not draw there, and the placement rule - turn about the ANCHOR CELL's middle,
with the stored angle negated. **`cd776e5` then corrected a misreading both pages carried**: the
`0x168 - angle` constant is at the QUEUE's own call site, so it says a queue piece turns the *opposite*
way to a built object rather than confirming the object rule. The object rule stands on the marked
footprint cells; the queue's own turn is settled by the torches `queend` carries along a single edge of
its plate, which must face the cell the queue is entered from. **A further push needs a fresh yes
(`CLAUDE.md` rule 1).**

2026-09-14: **`docs/sign-format-corrections`**, a NEW local branch stacked on
`docs/md2-sgn-and-lobby-scripts` - which still carries **open PR #1**, re-verified that day
(`refs/pull/1/head` = `d73a627`, and origin's branch tip matches exactly). One commit **`9e506ae`**,
**PUSHED to origin 2026-09-14** at Alexah's word ("You can go ahead and push to my repos"), one named
refspec, **no PR** - GitHub offered the compare link in its push output and it was not used. Verified
`local == remote` at `9e506ae`, and **PR #1 confirmed untouched at `d73a627`** both on
`upstream refs/pull/1/head` and on origin's own `docs/md2-sgn-and-lobby-scripts` ref, before and after.
**That yes is spent (`CLAUDE.md` rule 1).** The docs repo is left checked out on this branch; it was
on `docs/item-footprints` before.
- `formats/sgn.md` rewritten. **The header is NOT a fixed size and there are NOT five signs - there are
  84**, because every ride's name board is a `.sgn` too. Three things move everything after them, so the
  file must be **walked**: each line's 20-byte ink block exists only when its mode is non-zero, each of
  the two fill textures states its own `(w, h, bpp)`, and the artwork exists only when the byte at
  `+0x08` says so. **That byte was listed "unknown" and is the artwork flag - clear in 61 of the 84.**
- The page's own open guess is **resolved, not replaced**: its "six 4-byte entries at `0x03A1`, maybe a
  gradient or palette" are the first pixels of **fill texture 0** (two 16x128x4 images, 16,408 bytes),
  identical along a row in a vertical ramp. Its instinct was right and its geometry wrong. The artwork's
  own `(width, height, type)` triple, which the table never had, sits 12 bytes before the float scales.
- **Two alpha corrections**: "uniform and opaque, not a cut-out" holds only for the four lobby boards
  bar a rim. Of the 23 artwork signs, **15 are solid art** whose partly-clear texels are the 1.6% the
  `.wct` codec rings around a hard edge, and **8 are real cut-outs** reaching alpha 0 across 5-43%.
  Also recorded: the engine marks a sign's material **see-through as it substitutes** the runtime
  texture rather than trusting the model, which is what makes that alpha count.
- **A new open question is written down rather than guessed**: three of the four lobby boards are
  lettered "Wonder Land", "Halloween World" and "Space Zone", which the `ISLAND()` script line does not
  contain - only Jungle's matches. `THEMENAMES.str` is the likelier source; not established.
- The compressed-size-tracks-name-length argument the page offered is **marked do-not-rely-on**: four
  samples, one mislabelled, and the correlation would argue *for* baked-in lettering, not against it.

2026-09-14: **`docs/rsse-instruction-set`, one commit `9939f96`, PUSHED to origin 2026-09-14** at
Alexah's word ("Yes, push the docs branch"), one named refspec, **no PR** - GitHub offered
`pull/new/docs/rsse-instruction-set` and it was not used. **That yes is spent** (`CLAUDE.md` rule 1).
**Branched directly off `upstream/master` (`0e8d5d0`)**, not stacked, because its three pages are
byte-identical on upstream and on the sign branch, so it is independently mergeable. Verified after:
`local == remote` at `9939f96`, 0 commits ahead, parent == `upstream/master`, **PR #1 untouched at
`d73a627` on BOTH `upstream refs/pull/1/head` and origin's own `docs/md2-sgn-and-lobby-scripts`**
(checked before AND after the push), 0 `refs/pull/*` on origin, `master` untouched at `0e8d5d0` on
local, origin and upstream, tree clean.
**The site was built to check it** (13 pages, `Complete!`) - see the correction above. The docs clone is
left checked out on this branch.
- `formats/rsse.md`: **the magic is four bytes, not eight.** `RSSEQ` was `RSSE` plus a version dword
  whose low byte the old reading swallowed. **Instructions had their halves reversed** - an opcode word
  followed by operands, not the other way round - and the operand counts, which appear nowhere in the
  file, are the only thing making a body divisible into instructions. The body is a flat **word** array,
  so a branch target needs no arithmetic. The variable names are **exactly `variable count` entries**,
  and reading them accounts for the final byte of all 308 files.
- `vm/info.md`: "over 100 instructions" is now **exactly 106**, with the dispatcher's `< 0x6A` bound
  and the note that 84 of them are used by at least one shipped script. Its common-variable table was
  **checked, not changed** - the corpus declares those twelve first, in that order, in 129 scripts.
- `vm/instructions.md`: **106 sections, matching the binary's names and order exactly.** Added
  `CRIT_UNLOCK` (absent outright), fixed the `SINGLESREAM` heading and a transposed `<unknonw3>`,
  corrected **POP** (one operand, not none) and **SETLIGHT** (two, not one), and filled all **39**
  stub bodies with the operand count plus, where the scripts use them, whether each operand is written
  as a literal or a variable. **15 of the 39 are used by no shipped script**, recorded rather than left
  looking like an oversight.
- **A wording trap worth remembering:** the first generated pass asserted "what they mean is not yet
  known" on twelve sections whose usage line already **named** operands (`WALKON <visitor ID> ...`),
  contradicting the page. Generated prose has to be read against what the page already says.

**A SECOND COMMIT on the same branch, `8047f76`, PUSHED to origin 2026-09-14** at Alexah's word ("Yes,
push it"), one named refspec (`9939f96..8047f76`), **no PR**. **That yes is spent**
(`CLAUDE.md` rule 1). Verified after: `local == remote`, 0 commits ahead, **PR #1 untouched at
`d73a627`** on both `upstream refs/pull/1/head` and origin's `docs/md2-sgn-and-lobby-scripts` (checked
before AND after), `upstream/master` untouched at `0e8d5d0`, 0 `refs/pull/*` on origin, tree clean.
> **No compare link was offered this time, and that is EXPECTED, not a difference worth investigating:**
> GitHub prints `pull/new/...` only when the push CREATES the branch. The first push to this branch got
> one; this one updated an existing branch and got none. Do not read an absent link as a sign the push
> behaved differently.
Two pages, `vm/instructions.md` and `vm/info.md`, built clean (13 pages).
**The pages documented a Sign flag and a Zero flag that the engine does not have.** It keeps one
**result register**: `TEST` loads a variable into it, `CMP` **subtracts** (the page said bitwise AND),
arithmetic leaves its result there, and the four conditional branches compare it against zero.
- **`BRANCH_PV` was the quiet one**: the page had it branching when the "positive" flag is clear, which
  branches on **zero** too. The engine's test is `JLE`-to-nowhere, so strictly greater than zero.
- **`SUB`, `DIV` and `MOD` had the destination LAST**; the engine stores into the first operand, as
  `ADD` already did. `MULT` documented from the engine, since no shipped script uses it.
- A **zero divisor yields 0** rather than faulting - `DIV` and `MOD` share one signed division.
- **A destination that is not a variable makes the instruction a silent no-op**, which is why 17
  shipped instructions can carry a literal there without breaking anything.
- `JSR`/`RETURN` gain the call stack: sized by `#setstack`, **grows downward, LIFO**.

**A THIRD COMMIT, `0c22781`, PUSHED to origin 2026-09-14** at Alexah's word ("Yes, push both"), one
named refspec (`8047f76..0c22781`), **no PR**, **no compare link offered - expected, the branch already
existed**. **That yes is spent** (`CLAUDE.md` rule 1). Verified after: `local == remote`, 0 commits
ahead, **PR #1 untouched at `d73a627`** on both refs (before AND after), `upstream/master` at
`0e8d5d0`, 0 `refs/pull/*`. The branch now carries four commits. It says what the **control**
instructions do, all read from the engine:
- **`CRIT_LOCK` is a VM critical section, not a ride lock** - instructions stop counting against the
  time slice - and **`CRIT_UNLOCK` gives up the rest of the slice as it leaves**. The `CRIT_UNLOCK`
  entry added in this branch's *first* commit had repeated the wrong neighbour's wording; corrected.
- `ENDSLICE`, `TURBO` and `END` were "unknown": yield the tick; run every tick instead of every
  eighth; and park the script (used by **no** shipped script).
- **`WAIT`'s open question is half-answered honestly** - the mechanism is deadline + program-counter
  rewind + fall through, and the operand is speed-scaled, so it is neither ticks nor slices. **The
  clock's own unit is still unestablished and the page says so** rather than guessing milliseconds.
  **>>> SUPERSEDED by the sixth commit below, 2026-09-15: the unit IS milliseconds, and the page now
  says that instead. <<<** The restraint above was right at the time and is kept as the record of it -
  the claim was not guessed then and is not guessed now, it was read out of the clock's own chain.
- `HUSH`/`HOP` are a push and a pop filling the `#setstack` storage from the bottom while `JSR` fills
  it from the top, with the engine checking the two do not meet.

**A FIFTH COMMIT, `f7be7f7`, 2026-09-15 - PUSHED to origin 2026-09-15.** Asked and first told
**"No, hold it locally"**; Alexah then reconsidered - *"If it's better for our process to stay
consistent, it's fine to push"* - and it went as one named refspec (`0c22781..f7be7f7`), **no PR**.
**That yes is spent** (`CLAUDE.md` rule 1). **No compare link was offered, which is expected** - the
branch already existed. Verified after: `local == remote` at `f7be7f7`, 0 commits ahead, **PR #1
untouched at `d73a627`** on BOTH `upstream refs/pull/1/head` and origin's
`docs/md2-sgn-and-lobby-scripts` (checked before AND after the push), `master` untouched at `0e8d5d0`
on local, origin and upstream, 0 `refs/pull/*` on origin, tree clean. The site was built clean (13
pages) before committing, and the docs clone is left checked out here.
- **Three COAST commands were documented as doing something other than what their handlers do.**
  `COAST_GETQUEUE` returns the **room remaining**, not the queue length - so a `BRANCH_Z` after it
  branches on *full*, not on *empty*. `COAST_GETPEEP` returns **one visitor's id, or 0**, not a count,
  and drains a different ring from the one `ADDPEEP` fills. `COAST_SETWORN` **does nothing at all**.
- Also corrected there: `SETBROKE` takes 0, 1 **and 2**; `SETCLOSED` is a guarded transition the engine
  drops when the state does not allow it; `INITIALISE` binds by the script's **own** id, not its
  operand. Both `COAST` and `BUMP` still claimed to "set flags", which `vm/info.md` already contradicts.
- `BUMP` gained its dispatch **without touching its command names**, which is the restrained call: the
  page's numbering is independently corroborated (command 15's table entry really is the error path),
  so the names may well be right and were not mine to rewrite on a hunch.
- `vm/info.md` gained **"When a script runs"**, which was missing outright, and its common-variable-set
  claim went from "must be contained within every ride" to the measured **129 of 308**.
- **>>> AN INVENTED ENGINE STRING WAS CAUGHT BEFORE IT SHIPPED. <<<** I wrote that an out-of-range
  `COAST` logs "Unknown coaster command" - a string I made up from the shape of the sentence. The real
  one at `0x765bac` is **"RSSE: Unknown bumper ride command"**, shared with `BUMP` because they share
  one error handler, while `TOUR` has its own. **Never write an engine string without reading it**; see
  rule fourteen in `docs/VERIFYING.md`.

**A SIXTH COMMIT, `4eb2789`, 2026-09-15 - PUSHED to origin 2026-09-15** at Alexah's word ("Yes, push
both"), which covered this and OpenTPW branch 65 together. One named refspec (`f7be7f7..4eb2789`),
**no PR**. **No compare link was offered, which is expected** - the branch already existed. **That yes
is spent** (`CLAUDE.md` rule 1). Site built clean (**13 pages, 1.97s**) before committing. Verified
after: `local == remote` at `4eb2789`, 0 commits ahead, **PR #1 untouched at `d73a627`** on
`upstream refs/pull/1/head` (checked before AND after the push), `master` untouched at `0e8d5d0` on
local and origin, 0 `refs/pull/*` on origin, tree clean. It rewrites five instruction entries on `vm/instructions.md`, all from handlers read the same day:

- **`GETTIME` said "the time that the ride has been alive for", which is wrong.** The handler reads the
  clock object the whole engine shares and stores it **raw** - nothing per-ride, nothing subtracted.
- **`WAIT` said the clock's unit was unestablished and warned against assuming milliseconds.** It **is**
  milliseconds; the chain behind the clock ends at a source that falls back to `timeGetTime` and scales
  its `QueryPerformanceCounter` path to agree. Also added: the speed scaling is real but **inert** -
  divisor `0.5 + 0.01 * speed`, and no instruction anywhere writes the speed word, so it is always 1.
- **`WAITABS`, `SETTIMER` and `GETTIMER` were all "Unknown"** and are now described. `WAITABS` shares
  `WAIT`'s code but takes its operand **as** the deadline; `SETTIMER` is **not** speed-scaled where
  `WAIT` is; `GETTIMER` floors its answer at nought.
- **`RAND`'s existing wording turned out to be RIGHT and was kept.** "The highest value to generate" is
  correct because the modulo is by the bound **plus one** - worth recording, because I went looking for
  an error there and the data said the page was already right. What was *added* is the wrinkle nobody
  would guess: the bound is taken as a literal **even when tagged a variable**.

**SEVENTH COMMIT 2026-09-15 (`3ef61db` on `docs/rsse-instruction-set`, PUSHED `4eb2789..3ef61db` at
Alexah's word, no PR; `master` untouched at `0e8d5d0`, PR #1 verified untouched at `d73a627`):** the animation
family. `TRIGANIM` and `TRIGWAITANIM` said "Unknown"; `WAIT4ANIM` claimed to wait for **all** the
animations currently playing, which is not what it waits for - it waits on one deadline and does nothing
at all when none is armed. **`WAITANIM` and `LOOPANIM` were right as far as they went and were kept** -
`WAITANIM` really does start the animation, because its trigger call passes the same loop flag
`TRIGANIM` passes, so "correcting" that would have introduced an error. `FLUSHANIM` was **left alone**:
only its no-model exit was read, not its body. Site still builds 13 pages in ~2s.

**EIGHTH COMMIT 2026-09-15 (`6d2a1e3` on `docs/rsse-instruction-set`), PUSHED to origin 2026-09-15** at
Alexah's word ("You can push"), one named refspec (`3ef61db..6d2a1e3`), **no PR**. **No compare link was
offered, which is expected** - the branch already existed. **That yes is spent**
(`CLAUDE.md` rule 1). Verified after: `local == remote` at `6d2a1e3`, 0 commits ahead, **PR #1
untouched at `d73a627`** on `upstream refs/pull/1/head` (checked before AND after the push), `master`
untouched at `0e8d5d0` on origin and upstream, 0 `refs/pull/*` on origin, tree clean.
Site built clean (**13 pages, 2.07s**) before committing. It rewrites
five entries that were describing the wrong subsystem entirely: `ADDOBJ`, `ADDOBJ_EXT`, `KILLOBJ`,
`FADEOBJ` and `EVENT` talked about adding and removing "objects" in "slots", when they **start particle
effects and sounds** and the slot is a **kill tag**.
- **`<type>` selects the subsystem and, for a sound, the category** - 1-2 particles, 3-9 one of
  `cat_rides` / `cat_ambient` / `cat_kids` / `cat_staff` / `cat_ui`, 10 a thing's own custom effect.
- **`KILLOBJ` stops EVERY effect carrying the tag, not the first one it finds.**
- **Nothing an `EVENT` starts can ever be stopped**, because its handler discards the handle - which is
  the only real difference between `EVENT` and `ADDOBJ`, and why `EVENT` takes no tag.
- **`FADEOBJ` is not "fade out, then remove"**: for a particle it is identical to `KILLOBJ`.
- `ADDOBJ_EXT` gains real operand names from its handler; no shipped script uses it, and the page says so.
- **`SETOBJPARAM` was deliberately left exactly as it was** - its handler was never read, the same
  restraint `FLUSHANIM` got in the seventh commit.

**NINTH COMMIT 2026-09-15 (`dae3328` on `docs/rsse-instruction-set`), PUSHED to origin 2026-09-15** at
Alexah's word ("Push both"), one named refspec (`6d2a1e3..dae3328`), **no PR**. **No compare link was
offered, which is expected** - the branch already existed. **That yes is spent**
(`CLAUDE.md` rule 1). Verified before AND after: `local == remote` at `dae3328`, 0 commits ahead,
**PR #1 untouched at `d73a627`** on `upstream refs/pull/1/head` (with `/merge` unchanged at `300fa60`),
`master` untouched at `0e8d5d0` on origin and upstream, 0 `refs/pull/*` on origin, tree clean. Site built
clean (**13 pages, 1.99s**) before committing. It rewrites the five **limbo** entries, three of which
were blanks: `LIMBO`'s second operand read "unknown (possibly related to `LIMBOSPACE`, but may also be
duration)", and `INLIMBO` and `LIMBOSPACE` were "Unknown" outright, operands included.
- **`LIMBO <visitor ID> <seconds>` - the second operand is a DURATION IN SECONDS**, multiplied by a
  thousand before it is added to the game clock.
- **`LIMBO` writes to NEITHER operand**, where every other instruction in the family answers into one.
  It answers 1 or 0 in the result register alone.
- **`FORCEUNLIMBO` refuses a destination that is not a variable before doing anything at all** - it does
  not even reach the result register, which is exactly where it differs from `UNLIMBO`.
- **`INLIMBO` is how many are held, `LIMBOSPACE` how much room is left**, and all 24 shipped uses of the
  latter write a literal `0` as a dummy destination - the `COAST 2 0` idiom.
- How many can be held comes from the **file header**, which 24 of the 308 scripts set to ten; those 24
  are exactly the scripts that use a limbo instruction, and they are shops and toilets, not rides.

**TENTH COMMIT 2026-09-15 (`f389359` on `docs/rsse-instruction-set`), PUSHED to origin 2026-09-15** at
Alexah's word ("Push both"), one named refspec (`dae3328..f389359`), **no PR**. **No compare link was
offered, which is expected** - the branch already existed. **That yes is spent**
(`CLAUDE.md` rule 1). Verified before AND after: `local == remote` at `f389359`, 0 commits ahead,
**PR #1 untouched at `d73a627`** on `upstream refs/pull/1/head` (with `/merge` unchanged at `300fa60`),
`master` untouched at `0e8d5d0`, 0 `refs/pull/*` on origin, tree clean. Site built clean
(**13 pages, 2.00s**) before committing. It rewrites the **ten script-to-script** entries.
- **`GETREMOTEVAR` is the biggest change: its three operands were documented as UNKNOWN.** They are
  `<dest> <script ID> <variable ID>`, and any other reading still runs and still writes something.
  It also **answers a nought into its destination when it fails**, so a missing script cannot be told
  apart from a variable holding nought - and both shipped uses pass a literal destination, making the
  instruction a test-and-branch rather than an assignment.
- **`SPAWNSOUND` was "likely somewhat different from `SPAWNCHILD`, but unknown".** It is the same loader
  call storing into a different field; what it spawns is linked to nothing, cleared by nothing, and
  exists so the engine can read a block of its variables from outside the interpreter.
- **`FINDSCRIPTRAND` failing leaves its destination alone**, where `GETREMOTEVAR` writes a nought - the
  two are documented side by side precisely because treating them alike is wrong about one of them.
- **The names scripts ask for never match their file's case**, so resolution must be case-insensitive;
  the page says so, because that alone will break a reimplementation on Linux.
- **`REMOVECHILD` kills rather than forgets**, and the teardown is one level deep, so a grandchild
  outlives its grandparent's death.

**ELEVENTH COMMIT 2026-09-15 (`91b8980` on `docs/rsse-instruction-set`), PUSHED to origin 2026-09-15**
at Alexah's word ("Push both"), one named refspec (`f389359..91b8980`), **no PR**, no compare link
offered (expected - the branch already existed). **That yes is spent** (`CLAUDE.md` rule 1). Verified
before AND after: `local == remote` at `91b8980`, 0 commits ahead, **PR #1 untouched at `d73a627`**
(`/merge` `300fa60`), `master` untouched at `0e8d5d0`, 0 `refs/pull/*` on origin, tree clean. Site built
clean (**13 pages, 2.02s**) before committing. It rewrites `SETOBJPARAM` and `DIPMUSIC`.
- **`SETOBJPARAM`'s first operand was documented as a `<slot>`. It is a TAG** - the same field
  `KILLOBJ` matches on - and the handler walks the whole record list setting the parameter on every
  match rather than addressing one object. **A particle carrying that tag is walked past**, because the
  type dispatch's particle case does nothing: the record's type decides, not its tag.
- **`DIPMUSIC` was already right** - "mute or unmute" - and is left saying so, which is worth noting
  because the opcode's name suggests a partial duck and it is not one. What was added: **any** non-nought
  value mutes (the test is against zero, not against one), the setting is **game-wide rather than
  per-script**, and **nothing but the script's death ever un-mutes it**, since no opcode clears it and
  no shipped script passes nought.

**TWELFTH COMMIT 2026-09-15 (`90e81a7` on `docs/rsse-instruction-set`), PUSHED to origin 2026-09-15** at
Alexah's word ("Push to my repos"), one named refspec (`91b8980..90e81a7`), **no PR**, no compare link
offered (expected - the branch already existed). **That yes is spent** (`CLAUDE.md` rule 1). Verified
before AND after, the before-check written as hard assertions that would have aborted the push had
anything moved: `local == remote` at `90e81a7`, 0 commits ahead, `master` untouched at `0e8d5d0` on
origin AND upstream, **0 `refs/pull/*` on origin**, and **upstream PR #1 untouched at `d73a627`**. Site
built clean (**13 pages, 2.08s**) before committing. It says what an animation id selects.
- **An id names one of TWELVE FIXED ROLES, and each role is a file suffix.** Ids 0-11 are
  `C D I L S M E U W B R O`, and **12 is the sentinel for "no animation"**. Each letter names files
  beside the model - `<name><letter>.md2`, or `<name><letter><n>.md2` where a role holds more than one.
  The second operand of each animation instruction is an **entry index within that role**, which the
  engine tests against the number of entries the slot holds.
- **The obvious reading is refuted, and the page now says so:** an id is **not** an index into the
  numbered `<name>M<n>.md2` files. Of the 72 ride archives whose script names a literal id, **all 72**
  use one at or above the number of those files they ship, and two ship none at all while triggering
  ids 0 and 5. `M` is merely the letter of one role, id 5.
- **Six letters are cross-checked from the shipped data without the executable**: `c` -> 0, `l` -> 3,
  `s` -> 4, `e` -> 6, and `b`/`r` -> 9 and 10.
- **>>> AND IT CORRECTS A SUPERSEDED READING THIS PAGE HAD BEEN CARRYING. <<<** `TRIGWAITANIM`'s entry
  said it "keeps a cursor of its own, stepping across turns through more than one animation channel"
  and that "what the cursor counts is not yet established". **There is no cursor.** It keeps a **mark** -
  the animation id **plus one**, so nought can mean "nothing armed" - and asks the model which animation
  channel 0 is playing, going on only when that answer plus one equals the mark; what it calls is a
  plain accessor into the channel array. **With no model it never finishes**, because the comparison
  falls on its own third operand, which nothing can change - and across the 133 shipped uses that
  operand never equals the first. **Docs go stale exactly here**: the engine reading moved on in a
  later session and the page did not, so re-read the entries around any opcode whose behaviour is
  re-decoded, not just the one being written about.
- Operand naming was inconsistent and is now unified: `WAITANIM` and `LOOPANIM` called the first operand
  `<type>` where `TRIGANIM` and `TRIGWAITANIM` called it `<animation>`, for the same id space.

**THIRTEENTH COMMIT 2026-09-15 (`d13e9d0` on `docs/rsse-instruction-set`), PUSHED to origin 2026-09-15**
at Alexah's word ("Push both"), one named refspec (`90e81a7..d13e9d0`), **no PR**, no compare link offered
(expected - the branch already existed). **That yes is spent** (`CLAUDE.md` rule 1). Verified before
AND after, the before-check written as hard assertions that would have aborted the push had anything
moved: `local == remote` at `d13e9d0`, **0 `refs/pull/*` on origin**, `master` untouched at `0e8d5d0` on
origin AND upstream, and **upstream PR #1 untouched at `d73a627`** (`/merge` `300fa60`). Site built clean
(**13 pages, 2.07s**) *before* committing. It says how long an animation runs, and corrects two entries that were wrong.
- **The length is the span the clip DECLARES**, read from its own animation block - not what its keyframes
  span, which is a different number on **159** of the clips under levels/. Every clip declares a start of
  nought and an end of at least one, so there is no zero-length case. **This entry said "times 1000/30",
  and that was WRONG** - the engine multiplies by the 32-bit float at `0x006fec08` (33.33333206176758) and
  **truncates**, which differs wherever the span is a multiple of three. Corrected by the fourteenth
  commit below, and it had shipped in our own code too.
- **Role or entry absent -> a FLAT 1000**, and that branch is shipped content: the eight references are
  named on the page so nobody rederives them. `TRIGANIM` plays on **channel 0** with no flags.
- **>>> `FLUSHANIM` WAS WRONG AND IS CORRECTED. <<<** The page said "stop all active animations". It
  stops nothing: the handler writes the no-animation sentinel into the channel's *queued* role and the
  clip actually running plays out. Nothing is answered back to the script.
- **`GETANIM_CH` and `TRIGANIM_CH` were both Unknown and are now decoded.** `GETANIM_CH`'s first operand
  is a **destination, not a role** - the opposite shape to every other `_CH` instruction - and the page
  says so explicitly because **reading it the other way corrupted a census during this very work**. The
  corrected figures are **627 (item, role) pairs and 806 (item, role, entry) references**; the earlier
  "626 of 634" is retracted.
- **`WAITANIM`** gains the other half of its story: with a model its wait is real, and at 547 uses across
  246 scripts it is where a model changes the most behaviour.

**FOURTEENTH COMMIT 2026-09-15 (`0525d7b` on `docs/rsse-instruction-set`, `d13e9d0..0525d7b`), PUSHED to
origin 2026-09-15** at Alexah's word ("Push both"), one named refspec, **no PR**, no compare link offered
(expected - the branch already existed). **That yes is spent** (`CLAUDE.md` rule 1). Verified before AND
after: `local == remote` at `0525d7b`, **0 `refs/pull/*` on origin**, `master` untouched at `0e8d5d0` on
origin AND upstream, and **upstream PR #1 untouched at `d73a627`**. Site built clean (**13 pages, 2.06s**)
before committing. **It corrects a page WE published the session before**, which is the point worth
keeping: the thirteenth commit stated the length as "times 1000/30", and the engine multiplies by the
32-bit float at `0x006fec08` (33.33333206176758) and **truncates** through `__ftol`, which sets rounding
toward zero before storing. They differ wherever the declared span is a multiple of three - **293 of the
1,237 clips under levels/** - so a 600-frame ferry clip is 19999ms, not 20000. `WAITANIM`'s entry carried
the same number as prose ("up to the twenty seconds a ferry's clip runs for") and went with it. The same
error had shipped in our own `RideAnimations.DurationMilliseconds`, fixed on `alexah/73-the-numbers-the-engine-reads`.

**FIFTEENTH COMMIT 2026-09-15 (`16a3076` on `docs/rsse-instruction-set`, `0525d7b..16a3076`), PUSHED to
origin 2026-09-15** at Alexah's word ("Push both"), which covered this and OpenTPW branch 74 together. One
named refspec, **no PR**, and **no compare link was offered, which is expected** - the branch already
existed. **That yes is spent** (`CLAUDE.md` rule 1). Verified after: `local == remote` at `16a3076`,
`master` untouched at `0e8d5d0` on origin AND upstream, **0 `refs/pull/*` on origin**, and **PR #1 untouched
at `d73a627`** (`/merge` `300fa60`). Site built clean
(**13 pages, 2.08s**) before committing; tree clean afterwards. Remote state verified at commit time:
origin's branch still at `0525d7b`, `master` untouched at `0e8d5d0` on origin AND upstream, **0
`refs/pull/*` on origin**, and **upstream PR #1 untouched at `d73a627`** (`/merge` `300fa60`) - checked
against the DOCS upstream, which is the repo that actually owns PR #1. **A trap worth keeping: I first
queried `OpenTPW/OpenTPW` for `refs/pull/1/head` and got nothing back, and nearly reported "untouched" on
the strength of a query that could never have matched.** That upstream has 13 pull refs, none of them
numbered 1; PR #1 lives in `OpenTPW/OpenTPW.FileFormats`. Name the repo when recording a PR fact.
It corrects two things on `vm/instructions.md`, both from bytes read that day:
- **`WAIT` said the speed scaling "can never do anything", and that was too strong.** The claim rested on
  a sweep finding exactly two writers of the speed word; **there is a third just past where that sweep
  stopped** - a plain setter whose only caller is the object constructor, pushing a **placed item's own
  operating speed** into the script it has just bound. So a script belonging to something standing in a
  park can be scaled, and only a script with nothing behind it is guaranteed the neutral 50. Which `.sam`
  key supplies that speed is still not established, and the page says so rather than guessing.
- **`WAITANIM` gains the half of its arithmetic the page never had.** Both it and `TRIGANIM` divide by the
  same divisor, but they apply the 300 floor on **opposite sides of it**: `TRIGANIM` subtracts and floors
  before converting, `WAITANIM` converts and divides first (`0x00552acd` `FDIV [ESP+0x14]`) and only then
  compares, unsigned. **This also refuted a claim in `docs/exe/park.md`** that `WAITANIM` adds its
  milliseconds with no speed divide at all - it divides, and the memory is corrected.

> **>>> THE SITE'S TOOLCHAIN IS NOT ON PATH. <<<** `npm run build` answers `npm: command not found`.
> Node is at **`/home/alex/.nvm/versions/node/v20.19.0/bin`** (node v20.19.0, npm 11.18.0) - prepend it to
> `PATH` and the build runs normally. Same shape as `wadcat`, which is likewise installed and likewise
> invisible to `which`; see `docs/exe/park.md` for that one's path. Do not conclude a tool is missing
> from a bare `command -v`.

> **>>> THAT "THERE IS NO `.md2` PAGE AT ALL" WAS WRONG, AND IT MISLED SEVERAL SESSIONS. CORRECTED
> 2026-09-15. <<<** There **is** one - **`formats/models.md`, 645 lines**, covering the header, node
> hierarchy, textures, mesh table, vertex data, faces, materials, normals AND the whole animation half
> (locating the tracks, track descriptors, target resolution, rotation, vertex morph, UV, sequencing).
> **It is invisible from `docs/rsse-instruction-set`**, which is where the page list below was taken from;
> `models.md` lives on **`docs/md2-sgn-and-lobby-scripts`** (`d73a627`), the branch carrying open PR #1.
> **Listing one branch and concluding a page does not exist ANYWHERE is the error** - the same shape as
> rule eleven in `docs/VERIFYING.md`. Check with `git ls-tree -r <branch>` across the branches that
> matter, not with `ls` on whichever one happens to be checked out.
> The paragraph below is kept as the record of the wrong claim; its nine-page list is true only of
> `docs/rsse-instruction-set`.
> The
> site has nine format pages - rsse, rss, sam, saves, sounds, strings, texture, video, wad - and **none for
> the model/animation format**, so the only place animation appears is `vm/instructions.md`, which is a VM
> page. Two things confirmed on 2026-09-15 therefore went into code comments and memory instead of onto
> the site: the **UV descriptor** (index table is *first key, key count*; one frame per key at descriptor
> `+0x0c`; one (u,v) pair per key at `+0x10`; entry index IS the vertex index, `FUN_004745c0`), and the
> **hide-list** at animation-block `+0x1a`/`+0x38`, which **859 clips carry**. `AnimationFile.cs`'s class
> comment already holds a well-evidenced account of the whole animation half - the track table, all seven
> channel bits, morph quantisation, visibility - so a `formats/md2.md` is mostly a transcription job rather
> than new research. The sidebar autogenerates from the `formats` directory, so a new page needs no config
> change. **Write only the animation half**; the mesh half is not verified to the same standard.

**SIXTEENTH COMMIT 2026-09-15 (`d8b9447`), AND THE FIRST ON A NEW BRANCH `docs/md2-easing-curve`,
STACKED ON `docs/md2-sgn-and-lobby-scripts` (`d73a627`) BECAUSE THAT IS WHERE `models.md` LIVES.
**PUSHED to origin 2026-09-15** at Alexah's word ("Push both"), which covered this and OpenTPW branch 78
together - and **the stacking WAS raised in the asking**, as this file requires. One named refspec, no
force, **no PR** - GitHub offered `pull/new/docs/md2-easing-curve` and it was not used. The branch was
**new on origin**, so the before-check asserts its ABSENCE rather than a tip; it was written as hard
assertions that would have aborted before any network write. **That yes is spent** (`CLAUDE.md` rule 1).
Site built clean (**16 pages, 2.36s, `Complete!`**) before committing. Verified before AND after the push:
**upstream PR #1 still `d73a627`** (`/merge` still `300fa60`), origin's `docs/md2-sgn-and-lobby-scripts`
still `d73a627`, `master` still `0e8d5d0` on origin AND upstream, **0 `refs/pull/*` on origin**, and
`local == remote` at `d8b9447` afterwards. **Pushing a stacked branch did not touch PR #1**, as recorded.
It corrects `formats/models.md`, which called the `+0x34` pointer "an observation rather than a decode":
- **A curve record is EIGHT bytes**, indexed by an id on each rotation keyframe. The page's own quoted ramp
  (`32, 66, 105, ...` in `Advisorm1`) is that file's **curve 0**, landed on at the right stride by luck -
  which is why "the records are not a fixed length" looked true and kept the field closed.
- **The keyframe's second ushort is the id, not a flag word.** `0xFFFF` is the only sentinel; `0` is curve
  number nought, the commonest id in the game.
- **Ten points, nine segments**, ends implied (0 before the first byte, 1 after the last). Segment count
  `0x006febe4` = **8.999995231628418**, deliberately under nine so `t == 1` cannot fall into a tenth
  segment and jump the pose backwards; byte scale `0x006febec` = 1/255.
- Three measured traps written down: the id is **not** the key index (3,070 of 12,428 disagree, ids reach
  100); the last key of an eased track **never** names a curve (1,759 of 1,759); and **2,797 records fall
  as well as rise**, which is an author's overshoot to reproduce rather than sort.
- **>>> AND IT IS THE SAME MISTAKE ALEXAH ALREADY CORRECTED ON 2026-09-10. <<<** I first wrote a standalone
  `md2.md` on `docs/rsse-instruction-set`, discovered `models.md` already existed, **withdrew the page, and
  said the work needed Alexah's decision** - which is exactly the "docs left unwritten because models.md
  only existed on the PR #1 branch" failure that produced the stacking rule further down this file. **The
  open-PR rule is about PUSHES, not local commits.** Read that rule before concluding docs work is blocked.

> **>>> STILL STALE ON `models.md` AFTER THE EASING COMMIT - DELIBERATELY OUT OF ITS SCOPE. <<<**
> 2026-09-15, branch `docs/md2-easing-curve`. That commit corrected the `+0x34` easing curve ONLY.
> The page's channel-flag table still lists **`0x20000` (visibility) and `0x00001` (position) as
> "Unidentified / No"**, and carries no body section for either, although **both are decoded** and
> documented in `AnimationFile.cs` - visibility is a count at `+0x16` with signed frame entries at
> `+0x30` (positive shows, zero-or-negative hides), and position is the 16-byte record at `+0x18`
> with its Bezier/straight type bits. The descriptor's **`+0x0C`** ("a frame value, close to but not
> always the track's last keyframe") is also still open, and the **hide list** at animation-block
> `+0x1a`/`+0x38` has no entry at all. Kept out of the easing commit so it stayed reviewable; these
> are the next docs job on that branch, not an oversight.

**SEVENTEENTH COMMIT 2026-09-15 (`111eddf` on `docs/particles`, `33cc38c..111eddf`), PUSHED to origin
2026-09-15** at Alexah's word ("If you need to push anything, you can do so"), one named refspec, no
force, **no PR**, and **no compare link offered - expected, the branch already existed**. **That yes is
spent** (`CLAUDE.md` rule 1). The before-check was written as hard assertions that would have aborted
**before any network write**: origin's `docs/particles` still `33cc38c`, `master` `0e8d5d0` on origin AND
upstream, **upstream PR #1 `d73a627`** (`/merge` `300fa60`), **0 `refs/pull/*` on origin**, and exactly
two changed files staged by name. After: `local == remote` at `111eddf`, **0 ahead**, PR #1 untouched,
both masters untouched, 0 pull refs, tree clean. Site built clean before committing.
> **A PAGE COUNT IS PER-BRANCH - do not read a difference as a fault.** This branch builds **15 pages**;
> `docs/rsse-instruction-set` builds 13 and `docs/md2-easing-curve` 16, because each carries a different
> set of pages. The baseline here was 15 before the edit and 15 after, which is the check that matters.
It updates **`formats/sprites.md`** and **`formats/saves.md`** - the branch that owns them, found with
`git ls-tree` across the branches rather than from whichever was checked out, per the `models.md` lesson.
- **How many DIRECTIONS a sprite set has was missing, and it is five.** The file never states it, so it
  was measured from the layout: a bank's sets sit end to end in its pictures, so each consecutive pair
  votes - the gap is `framesPerDirection x directions`. **169 gaps across all 46 banks answer five**; the
  only other answer, one, comes from the banks that are not directional at all. The settling check is the
  picture count: **29 banks' last set plus its span lands exactly on their `.TPC`'s count at five**, and
  the seven non-directional ones land exactly at one.
- **The `*heads` banks are recorded as UNDETERMINED, not assumed.** Their sets are a single run, so no
  gap exists to vote with. Writing "five" there would have been a guess dressed as a measurement.
- **`0x14E` said "counted by the loader but not seen used" - disproved.** `FUN_00540c70` reads those
  sixteen bytes as four groups selected by a state of 0 to 3, each holding two set numbers **stored one
  higher than the set they name**.
- **`.FPC` is no longer deferred:** all 29 `.FPC` and all 46 `.TPC` are version 3, so the page's layout
  reads either.
- **`saves.md` gains the `TPCS` block** - the park's sprites, 280 bytes each, one record per non-zero
  handle, directly after the world block's trailer. The shipped park has 100 slots, **18 live**, and
  those 18 are exactly its people (13 kids + one of each of five staff), which its thing list says
  independently.
- **Three claims on `saves.md` were wrong against the game's own files**, all contradicted by our own
  shipped `SaveReader.cs`: the leading dword is a **version** (400 shipped, 500 saved) not a magic
  number; the two dwords after `BILZ` are the **inflated size** and the **whole block's size including
  its 28-byte header**, neither a compressed length; and the notice is **824** bytes of UTF-16, not 823,
  which is what puts the compressed data at `0x60D`.

**EIGHTEENTH COMMIT 2026-09-16 (`998d92f` on `docs/particles`, `111eddf..998d92f`), PUSHED to origin
2026-09-16** at a fresh yes ("Push both"), which covered this and **OpenTPW branch 80** together. One
named refspec, no force, **no PR**, and **no compare link offered - expected, the branch already
existed**. **THAT YES IS SPENT** (`CLAUDE.md` rule 1). The before-check was written as hard assertions
that would have aborted **before any network write**: HEAD `998d92f` over parent `111eddf`, clean tree,
origin's `docs/particles` still `111eddf`, and **0 `refs/pull/*`** - that last one enforcing this file's
own rule against writing onto a branch with an open PR. After: `local == remote` at `998d92f`, **0
ahead**, default `0e8d5d0` unchanged on origin AND upstream, 0 pull refs, tree clean.
**>>> THIS COMMIT CORRECTS THE SEVENTEENTH ENTRY ABOVE, NOT JUST THE PAGES. <<<** Two claims that entry
records as measured are wrong, and both were published:
- **"It is five" was too strong.** Five is what a *body* stores, not what everything stores. The
  direction byte read straight out of every in-use set of all 46 banks gives **5 on 201, 7 on ten, 4 on
  one, and 0 on the 71 that face nowhere**. The ten sevens are the eight `Kidsheads` banks and two
  `Costumeheads` - so **the "*heads are UNDETERMINED" caution the seventeenth entry was careful to write
  is now answered: they store seven.** The old gap-voting measurement was not wrong, it was indirect: it
  needed two sets to measure a gap and those banks are a single run.
- **The byte is a COUNT, not a flag**, which is also a live defect in our own shipped
  `SpriteBankFile.cs` - fixed on OpenTPW branch 80 the same day. `282 of 283` in-use sets fit their pack
  under the count; the one that does not is a short pack, not a rule.
- **`saves.md`'s "the record carries no position" was WRONG.** It carries three floats at
  `+0x88`/`+0x8c`/`+0x90`, world units at ten to a cell, agreeing with each thing's own `mX`/`mY` on
  **18/18** to within a third of a unit. The scan behind the old claim swept only **cell-shaped** values
  and could not have seen them - now **rule thirty-three** in `docs/VERIFYING.md`.
- **`0xB4` is not a "variant"**: low four bits are the set, the rest is the bank offset past its kind's
  first bank, which is exactly how the engine takes it apart.
> **>>> STILL OPEN ON `sprites.md`, DELIBERATELY NOT REWRITTEN. <<<** The seventeenth entry records the
> `0x14E` groups as "two set numbers **stored one higher** than the set they name", and the page still
> says so. **I did not verify that reading** and a verifier disputed it as (script, set). What I did
> measure myself, over all 46 banks: **only group slot 0 is ever in use, and only on the twelve
> Entertainer banks**, every one reading `(1, 5, 0, 0)` except `Hallow\Entertainers\SPR_DR` at
> `(1, 5, 2, 40)`. That much went into `SpriteBankFile.cs`'s comment; the page's semantic claim is
> untouched and unproven. Do not cite it as measured.
> **PROCESS MISS, RECORDED RATHER THAN HIDDEN: the site was NOT built before this commit or its push.**
> Every entry above records a clean build first; this one broke that ritual. Node is not on `PATH` - see
> the toolchain note above. **Built afterwards and it is clean - 15 pages, 1.96s, `Complete!`**, which is
> exactly this branch's recorded baseline of 15, so the miss cost nothing this time. The order was still
> wrong: the next docs commit builds first, before the commit and long before any push.

**NINETEENTH COMMIT 2026-09-16 (`fda26d3` on a NEW branch `docs/save-person-record`, one commit over
`docs/item-footprints` `83ce2e4`), PUSHED to origin 2026-09-16** at a fresh yes ("You may push"). One
named refspec, no force, **no PR** - GitHub offered `pull/new/docs/save-person-record` and it was **not**
used. The branch was **new on origin**, so the before-check asserts its ABSENCE rather than a tip. **THAT
YES IS SPENT** (`CLAUDE.md` rule 1). All six before-checks were hard assertions that would have aborted
**before any network write**: branch absent on origin, parent still `83ce2e4`, `master` `0e8d5d0` on
origin AND upstream, **upstream PR #1 `d73a627`**, **0 `refs/pull/*` on origin**. After: `local == remote`
at `fda26d3d8da2a2d7b1d042a3440d648686d5c5d0`, **0 ahead**, every one of those unmoved, tree clean.
**A BASELINE build was taken BEFORE editing and the site built again before committing** - both clean at
**16 pages**, this branch's baseline. That is the ritual the eighteenth entry records missing, done in the
right order this time.
- **`saves.md` gains "A person: models 1 and 4-8"** - the shared **390-byte person base**, which explains
  every per-model size the page already printed (`8 + 390 + 135` = 533 for a guest, `8 + 390 + 105 + own`
  for each of the five staff), and the guest's own **135-byte block in full**. It also records that the
  base carries the peep's **entire navigation state** (177 bytes of steering and path data) and a
  **144-byte thought/event ring**, so a park resumes the routes its people were walking.
- **`sam.md` gains that the simulation is tuned in the balance file, not in the executable** - the
  engine's constants block reads **entirely zero in the image on disk** until `BalanceLoader.cpp` fills
  it, so anyone reverse engineering the peep code finds it reading zeros. Names the groups, `PeepTypes`'
  three columns, and that only `PeepInfo.ExcitementToCostDivisor` is overridden by any theme.
- **Two traps went onto the page rather than only into memory:** the file offsets are **not** the
  in-memory offsets a decompiler shows (`mState` is `+0x220` in memory and `+505` in the record), and a
  plain `0..100` range test on the need floats checks almost nothing, because a small integer read as a
  float is a denormal that passes it.

**>>> THE HARD PART WAS CHOOSING THE PARENT BRANCH, AND THIS FILE ALREADY HELD TWO INSTANCES WITHOUT EVER
STATING THE RULE. HERE IT IS. <<<** The `docs/*` branches are **disjoint stacks off `master`** -
`docs/particles` and `docs/item-footprints` share only `master` as a merge base - so **a page's content is
a branch artifact**, and `saves.md` exists in three different states at once:
- **41 lines**, container only, on the `docs/md2-*` chain, which is where I happened to be standing;
- with the **`TPCS` sprite table**, on `docs/particles` (the seventeenth commit above);
- with the **whole module map, World block and thing list**, on `docs/item-footprints` (six commits).
I was ready to commit onto the first, then onto the second, before measuring which branch actually owned
the thing list my work extends. **The measurement that settles it, and it is cheap:**
`git log --oneline master..<branch> -- <path>` for **every** branch, then `git show <branch>:<path>` on
the winner - never `ls` or a grep on whichever branch is checked out. **And having finally picked
correctly I still wrote a link to `#the-sprite-table-tpcs`, a heading that exists only on
`docs/particles`** - caught before committing by inventorying every link against the headings that exist
on the branch being committed to. New **rule forty** in `docs/VERIFYING.md`. The `models.md`
correction further down and the seventeenth commit's `git ls-tree` note are the same shape; those were
instances, this is the rule.

**TWENTIETH COMMIT 2026-09-16 (`697f447` on `docs/save-person-record`, `fda26d3..697f447`), PUSHED to
origin 2026-09-16** at a fresh yes ("Yes you may push"), which covered this and **OpenTPW branch 83**
together. One named refspec, no force, **no PR**, and **no compare link was offered - expected, the branch
already existed** (its first push did offer one, and it was not used). **THAT YES IS SPENT**
(`CLAUDE.md` rule 1). **Nine** hard before-checks, every one able to abort **before any network write**,
including that my commit sits directly on origin's current tip. After: `local == remote` at `697f447`,
**0 ahead**, `master` still `0e8d5d0` on origin AND upstream, **upstream PR #1 untouched at `d73a627`**,
**0 `refs/pull/*` on origin**, tree clean. **A baseline build was taken BEFORE editing and the site built
again before committing** - both clean at **16 pages**, this branch's baseline. Both `sam.md` sections came
out of decoding the peep simulation, so they are format facts confirmed and written the same session.
- **`sam.md` gains "A key may name several fields at once"** - a key is a group, an optional subscript, and
  then **one or more dot-separated field names**, and the line supplies **one value per name, in order**.
  Read out of the original's own parser `FUN_004017a0`, which collects the names into a table, **caps at
  sixteen** (it carries `"Too many fields have been specified"` for the seventeenth), and runs its value
  loop exactly that many times. So `PeepTypes[0].StartingCash` is **300**, not absent. **A reader that
  takes such a line as one key and one value keeps the first number and drops the rest in silence**, which
  is how the starting money and boredom threshold of all eight kinds of guest can appear missing from a
  file that states them plainly. It also explains why the **unmarked trailing prose** in these files is
  harmless: the loop never looks past the values the key asked for.
- **`sam.md` gains "RegionFX: what a thing does to the cells around it"** - eight numbered effects, each a
  **radius plus five values** (`Happiness, Illness, Hunger, Security, Attraction`), stamped into every cell
  within the radius **divided by Manhattan distance + 1**. Placing adds, removing subtracts, so a thing
  that MOVES does both and carries its effect with it. The engine keeps a **ten-byte record per map cell**.
- **The order is stated because two instruments agree on it independently**: the keys are in the order the
  code reads the five shorts, and the fourth is separately identified as the security level by the routine
  sweeping all 16,384 cells for a coverage percentage and by the one printing
  `There's no security level on this cell` before setting a guard on a vandal.
- **Left explicitly open on the page:** which of the eight a given thing stamps is chosen in code at each
  call site rather than named in the item's own `.sam`, and only two are pinned (entertainer 0, guard 3).

**TWENTY-FIRST COMMIT 2026-09-16 (`2b21be5` on `docs/save-person-record`, `697f447..2b21be5`), PUSHED to
origin 2026-09-16** at a fresh yes ("Yes, you may push to my repos and finish up"), which covered this and
**OpenTPW branch 83's four navigator commits** together. One named refspec, no force, **no PR**, no
compare link offered (expected - the branch already existed). **THAT YES IS SPENT** (`CLAUDE.md` rule 1).
Seven hard before-checks, each able to abort **before any network write**, including that the commit sat
directly on origin's tip. After: `local == remote` at `2b21be5`, **0 ahead**, `master` still `0e8d5d0` on
origin AND upstream, **upstream PR #1 untouched at `d73a627`**, **0 `refs/pull/*` on origin**, tree clean.
**A baseline build was taken BEFORE editing (16 pages) and the site built again after (16 pages).**
- **`saves.md` gains "The navigation block, exactly - 177 bytes at +43"**: the full alphabetical field
  order with offsets, the 16.16 fixed point convention, the three fields that carry **no name in the
  binary** (`mass`, `position`, `velocity`) and the four agreeing instruments that name them, the
  `position >> 8 == mX` check (18/18, and 0/18 at four shifted bases), `max_force == 2 x max_speed`,
  `max_speed` seeded from `mBaseSpeed x 65536 / 500` but **drifted on 4 of 18 so it must be read**, the
  octagonal metric, cell-centre waypoints, and a caution that **only `i < path_buffer_count - 1` of
  `subpath_dist` means anything** (unused slots keep `0xCDCDCDCD` or a stale distance).
- **The map-cell section gains the distinction that caught me out**: `mNeighbours` is **bit-tested** and
  `mDirection` compared for **equality**, and over this park's data the two are indistinguishable because
  `mDirection`'s five values are 0, 1, 4, 16, 64. Also the **adjacency scoring** that settles the compass
  from the file alone (94.8% vs 75.3%), and that the engine **reads the side OPPOSITE the way it is
  going** and reports blocked when the bit is **clear**.
- **A NEW ASIDE SYNTAX was introduced here** - `:::note[...]` and `:::caution[...]`. **Nothing else in
  this repo used it**, so a clean build was NOT taken as proof: the built HTML was checked for literal
  `:::` markers (none) and for `starlight-aside--note` / `starlight-aside--caution` classes (both present).

**CORRECTED 2026-09-13: an open PR CAN be checked from here, and `gh` is not needed.** `gh` is still
not installed, but GitHub publishes pull requests as refs, so `git ls-remote upstream 'refs/pull/*'`
lists them - each appearing as `refs/pull/N/head` (usually with a `/merge` beside it). Verified: the
docs upstream returns exactly `refs/pull/1/head` at **d73a627**, which is the tip of
`docs/md2-sgn-and-lobby-scripts` on origin - so **PR #1 is real, still open, and on that branch**, and
`docs/item-footprints` is provably not it. Neither fork carries any PR of its own (`refs/pull/*` on
origin is empty for both repos); OpenTPW's upstream has 13, none of them ours. **Check it this way
rather than guessing or asking.** Opening a PR still needs asking first.

**Local writes are always fine.** The open-PR and push rules are about GitHub only. Alexah
corrected this 2026-09-10 ("The docs repo is local, why can't you update it?") after docs were
left unwritten because models.md only existed on the PR #1 branch. When a page only exists on a
PR branch, write on a new local branch stacked on it (e.g. `docs/advisor-model-formats`), commit,
and raise the stacking when asking about the push.

**The published site is not currently serving.** `opentpw.gu3.me` 403s, its root fails to
connect, and `docs.opentpw.org` resolves to a Porkbun parking page (checked 2026-09-10 from a
box that reaches Cloudflare fine). The maintainer's own most recent docs commit is "Simplify
wording, remove dead links", so they are mid-migration. Do not rewrite the README's doc links
to guessed destinations; report them instead.

**TWENTY-SECOND COMMIT 2026-09-16 (`28a5400` on `docs/save-person-record`, `2b21be5..28a5400`), PUSHED to
origin, one named refspec, no force, no PR.** *Correct which cell the step check reads its neighbour bit
from.* **>>> THIS SUPERSEDES THE MAP-CELL HALF OF THE TWENTY-FIRST COMMIT ABOVE. <<<** That entry records
the page as saying the engine "reads the side OPPOSITE the way it is going", inferring that `mNeighbours`
records the sides a cell may be entered FROM. **Both the page and that reading are now corrected**: the
disassembly loads the **destination** cell into `ECX` before the call, and the destination's `+y` side is
exactly the side facing the cell being left - so it reads the **near side of the cell being stepped
into**, and nothing about entry sides is needed to explain it. The `:::note` was retitled from *"The
engine reads the far side, not the near one"* to *"The bit is read from the cell being entered"*.
**WHY NO MEASUREMENT COULD HAVE CAUGHT IT:** `mNeighbours` is **symmetric across all 65,024 of the park's
adjacent pairs**, so scoring the destination's facing bit against the source's returns **100% either
way**. Only the disassembly settles it, because the decompiler drops the `thiscall` `this` pointer on all
fifteen helpers. **What SURVIVED the rewrite and is still right:** "reports blocked when the bit is
**clear**", so the byte says where a cell *connects*, not where it is walled - and the 94.8%-against-75.3%
adjacency scoring, which settles *which bit is which side*, a different question entirely.

**TWENTY-THIRD COMMIT 2026-09-17 (`f6eb63c` on `docs/particles`, `06cd304..f6eb63c`), PUSHED to origin,
one named refspec, no force, no PR.** *Document which animation program a sprite runs, and where the
programs live.* The `TPCS` sprite-record table already named `0x08` as a "cursor into the sprite's
animation program" and gave no way to tell WHICH program - so the cursor could not be interpreted at all.
The field naming it is **`0x0C`**, and a jump inside a program moves `0x08` while leaving `0x0C` alone, so
it names the program a sprite was STARTED on rather than where it has reached.
**The programs are not in the save**: they are compiled into `testme.exe` as one flat array of 83 held
back to back, and the loader points every instance at it unconditionally - so the module named **`SPSC` is
the table of sprite INSTANCES, not the scripts**, despite the name. That also explains the note already on
the page that `0x14` is meaningless on disk.
Two properties are stated because both are observable in the shipped park's own bytes: **choosing a set
does not end a turn** (so a set and its first frame appear together), and **showing a frame does** (so a
saved cursor always rests just past a frame instruction - the park's sixteen walkers are stopped at seven
different such positions of one eight-frame walk).
**The rate paragraph was sharpened, not merely extended:** the system steps every two 31ms ticks, the
interval a sprite is born with is `0x3e` = exactly one turn of it, and the due test is strict - so an
untouched sprite runs at half rate. A walking person's interval comes from their distance moved, doubled
when NOT hurrying, and only ever yields 0, 1 or 2. It is a **doubling, not a continuous control**, and the
250 ceiling **cannot be reached at all** because the distance is squared as a 32-bit integer.
**THE BRANCH WAS CHOSEN AGAINST THE REFS, NOT FROM MEMORY.** `docs/particles` is where the `TPCS` table
lives; the "never onto a branch with an open PR" rule was checked by `ls-remote` - origin carries 0
`refs/pull/*`, and upstream's only PR head is `d73a627`, which is `docs/md2-sgn-and-lobby-scripts`, a
DIFFERENT branch. Verified after: `local == remote`, 0 ahead, `master` still `0e8d5d0`, upstream PR head
unchanged.

**TWENTY-FOURTH COMMIT 2026-09-17 (`44ba114` on `docs/particles`, `f6eb63c..44ba114`), PUSHED to origin,
one named refspec, no force, no PR.** *Document the world block's header, and what mBankAccount really
names.* The page covered the container header and the `TPCS` sprite table but said **nothing about the
world block itself** - even though the sprite section locates itself "directly after the world block's
`DLRW` trailer". This adds the block and its **26-field header** in the payload's own order.
**The field list is checkable rather than inferred:** each field is announced to a logging call the release
build compiles away, so the names never reach the file but survive in the executable **beside the address
each is read into** - `FUN_00516c80` pairs them - and those offsets give the same order and widths the
reader already walked, Guard-before-Researcher included.
**Two fields earned prose. `mParkClosed` reads the opposite way to its name**: the open/close command
writes 0 on one branch and 1 on the other and picks its own message with
`mParkClosed == 0 ? "opened" : "closed"`, and the world constructor writes 1 before anything loads, so a
park is born shut. **And `mBankAccount` is a HANDLE, not an amount** - the shipped park holds `8`, and the
one place the executable reads it is the weather thing's accessor with one offset changed (take the word,
return `thingTable[id]`), so it names the thing carrying the admission fee, balance, profit and loans.
**`mWorldState` is deliberately left unnamed**: written 1, 2 and 4, compared against 4 in six places, and
the shipped park holds **0**, outside that set - naming it either way would be a guess.
**A TOOLCHAIN NOTE THAT COST A CYCLE:** the docs site DOES build (`astro build`, and it was built before
this push - exit 0, 15 pages), but **`node` is at `~/.local/bin/node` and is not on the shell's default
PATH**, so `npm` reports "command not found" and looks exactly like an absent toolchain. Extend PATH
rather than concluding the repo has no build step.
**BRANCH CHECKED AGAINST THE REFS AGAIN, not carried from the last entry:** origin carries 0
`refs/pull/*`, upstream's only PR head is still `d73a627` on a DIFFERENT branch. Verified after:
`local == remote` at `44ba114`, 0 ahead, `master` still `0e8d5d0`, upstream PR head unchanged.

**TWENTY-FIFTH COMMIT 2026-09-17 (`cbe282c` on `docs/particles`, `44ba114..cbe282c`), PUSHED to origin
2026-09-17**, one named refspec, no force, **no PR**, and **no compare link offered - expected, the branch
already existed**. *Document the economy thing, which is where a park's money actually lives.* Pushed on a
**SEVENTH fresh yes** ("Yes you may push and continue your workflow until completion"), which covered this
and OpenTPW branch 89 together. **THAT YES IS SPENT** (`CLAUDE.md` rule 1). *(This entry said
"COMMITTED, NOT PUSHED" until the push landed.)*
All before-checks were hard assertions that would have aborted **before any network write**: HEAD
`cbe282c` on `docs/particles`, clean tree, origin's branch still at the expected parent `44ba114`, **0
`refs/pull/*` on origin**, and **upstream PR #1 read and confirmed to be a DIFFERENT commit** from the one
being pushed - the guard that stops this repo's own open-PR rule being broken by accident. Verified after:
`local == remote` at `cbe282c`, **0 ahead**, 0 pull refs on origin, **upstream PR #1 STILL untouched at
`d73a627`**, upstream `master` still `0e8d5d0`.
Built BEFORE committing, the ritual the eighteenth entry recorded breaking: **15 pages, `Complete!`**,
which is this branch's own recorded baseline of 15.
The twenty-fourth commit established that `mBankAccount` is a **handle**; this says what the thing it
names contains. Model 16, thing 8, a **300-byte record**: `mAdmissionFee`, `mBalance`, `mBatchBalance`,
`mWithdrawalsEnabled`, `mLastBalance`, `mTurnEnteredRed`, `mProfitThisYear`, then **eight loan slots of
eight 4-byte fields**.
- **A new `#### Thing records` section goes in FRONT of it**, because the economy's offsets mean nothing
  without it: a thing record opens with the **next thing's id** and its model (a linked list, not an
  array - the shipped park runs 41, 40 … 29, then 15, then 28), then every placed thing writes `mX`,
  `mY`, `mMapChild`, `mMapParent` as 2-byte fields. So a thing's own fields begin **16 bytes in**.
- **The page states outright that these are FILE offsets and not the decompiler's.** A thing is written
  in the order its reader asks for its fields, so `mAdmissionFee` is `+0x118` in memory and **+16** in the
  record. Using memory offsets as file offsets parses and is wrong.
- **Two closure checks and one that actually proves it.** `44 + 8 * 32 = 300` matches the record size; the
  in-memory loan array runs `+0x14` for `8 * 0x20` and stops exactly at `mWithdrawalsEnabled`'s `+0x114`.
  But the one worth trusting is a **different file**: the eight saved loans match `LoanInfo[0..7]` in
  `Standard.sam` field for field, and every `monthly_repayment` is its own amount over its own period,
  truncated, on all eight. Sixteen unrelated numbers in order.
- **>>> AND THAT COMPARISON SETTLES SOMETHING ABOUT THE SHIPPED PARK. <<<** Every saved APR is **nought**,
  matching `jungle/Easy_Standard.sam` and matching the global `Standard.sam` - 20, 20, 20, 20, 23, 22, 18,
  21 - **nowhere**. Lost Kingdom is an Instant Action park and its own loans are the evidence.
**Branch checked against the refs again rather than carried:** origin carries **0 `refs/pull/*`**, and
upstream's only PR head is still `d73a627` on `docs/md2-sgn-and-lobby-scripts`, a DIFFERENT branch - so
`docs/particles` is safe to write to under this file's own open-PR rule.

**>>> TWENTY-SIXTH COMMIT 2026-09-17 evening (`aec8d06` on `docs/particles`, `cbe282c..aec8d06`),
*PUSHED* to origin. THIS ENTRY SAID "NOT PUSHED" UNTIL THE PUSH LANDED. <<<** *Document the staff block,
which all five kinds of staff share.* Pushed on a **NINTH fresh yes** ("Yes you can push"), which covered
this and OpenTPW branch 91 together. One named refspec, no force, **no PR**, and **no compare link
offered - expected, the branch already existed**. **THAT YES IS SPENT** (`CLAUDE.md` rule 1).
All before-checks were hard assertions that would have aborted **before any network write**: HEAD
`aec8d06` on `docs/particles`, clean tree, origin's branch still at the expected parent `cbe282c`, **0
`refs/pull/*` on origin**, **upstream PR #1 read and confirmed to be `d73a627`** - a DIFFERENT commit from
the one being pushed, which is the guard against breaking this repo's own open-PR rule by accident.
Verified after: `local == remote` at `aec8d06`, **upstream PR #1 STILL `d73a627`**, 0 pull refs on origin.
**Do not read the length of this entry as evidence of anything** - `git ls-remote` settles it.
**Built BEFORE committing, the ritual the eighteenth entry recorded breaking: 15 pages, `Complete!`** -
this branch's own recorded baseline of 15, with only the known pre-existing sitemap warning.
**The open-PR rule was re-checked against the refs rather than carried from the twenty-fifth entry**, which
is this file's own discipline: **0 `refs/pull/*` on origin**, and upstream's only PR head is still
`d73a627` = `refs/pull/1/head` (`/merge` `300fa60`), which is `docs/md2-sgn-and-lobby-scripts` - a
**different** branch from `docs/particles`, confirmed by resolving that branch on origin to the same
`d73a627`. So writing here cannot expand PR #1.
The page described a thing record, the economy thing and the sprite table, and said **nothing** about the
block five of the six person models carry - a reader could locate a member of staff and then had nowhere
to go. What it now says:
- **All five kinds share ONE block, because the original gives them one class.** Every kind's per-turn
  function opens with the same switch on `mState` and answers states 0-7 through the same three handlers,
  whose own diagnostic strings name them `CStaff::`. It begins at **+398** - the same place a guest's own
  block begins, both following the eight-byte list head and the 390-byte person base - and runs **105
  bytes** before the kind adds anything.
- **>>> THE SIZES CLOSE FIVE WAYS AT ONCE, which is why this is layout rather than a reading. <<<**
  `8 + 390 + 105 = 503`, and the five record sizes leave exactly **8, 10, 6, 8 and 6** over it - precisely
  what each kind's own serialiser declares. Those record sizes were derived by a completely different
  route (running the original's reader and logging its declarations), so the two never shared a step.
  Re-deriving the **guest** block the same way reproduces all fifteen offsets our reader already used,
  which is the method being checked against work already known good.
- **Two unnamed floats are placed by where they SORT**, as the navigator's were, and what the code does
  with them agrees: the resting handler recovers one by `HappinessRecuperationRate` and the other by
  `RecuperationRate`, both indexed by the pay grade, and "too tired to work" reads the second against
  `AllStaffConstants.RestLevel`.
- **The alphabetical rule is recorded as NOT quite holding here** - `mTimeStartedIdling` is written before
  `mTimeHired`, the other way round from the sort. The page gives the serialiser's order, because that is
  what the file follows. **Do not generalise the guest block's tidiness to other blocks.**
- **A patrol region is two PACKED cell ids** (`y * 128 + 1 + x`) naming opposite corners; unpacking the
  shipped park's gives the entertainer `(47,18)`-`(48,25)`, the two columns south of the gateway cells,
  and the researcher the whole map.
- **>>> A TRAP SPELLED OUT: THREE DIFFERENT NUMBERINGS OF THESE FIVE KINDS ARE IN USE AT ONCE. <<<** The
  thing model (mechanic 4 … researcher 8), the sprite folders in `esprites.wad` (entertainers 4 …
  researchers 8), and the balance file's `PerTypeStaffConsts` (handyman 0 … researcher 4). The mechanic is
  model 4, wears sprite type 6, and is paid as type 1 - crossing any two reads the wrong constants while
  still looking entirely plausible.

**>>> TWENTY-SEVENTH COMMIT 2026-09-17 late evening (`58436e6` on `docs/particles`) - *PUSHED
2026-09-18* in the `aec8d06..664e160` run recorded at the foot of this file. This entry read "AND IT IS
NOT PUSHED - a push needs a fresh yes" until then. <<<** It documents **the map cell** and **the catalogue object
(model 3)**, the two records the save walker had been sizing correctly and stepping over. Built clean
(15 pages) before committing; tree clean; **0 `refs/pull/*` on origin**; upstream PR #1 still `d73a627`
on the *different* branch `docs/md2-sgn-and-lobby-scripts`, so writing on `docs/particles` cannot expand
it. **1 ahead of origin.**
- **It was AMENDED from `4c9088f`** to carry a decode I got wrong first - see below. Nothing had been
  pushed, which is the only reason that was available.
- **The map cell:** a status byte chooses which of three sub-records follow (map 52, track 31, effects
  10), and the map one is a 29-byte tile base plus a 23-byte litter block. `mLitterScript` really is
  announced **twice**, proven by arithmetic - `4+2+4+4+2+1+4+2 = 23`, and `29+23 = 52` exactly.
- **>>> `mStatusFlags` AT 45 *IS* `base.map`'s ATTRIBUTE BYTE, checked cell by cell across all 16,384
  <<<** - a separate file, own header, different code, indexed `x*128+y` against the save's `y*128+x`.
  Every cell agrees. Aggregate agreement would have proved little; per-cell agreement under **opposite**
  indexing is not something a misaligned or transposed reading can manage.
- **The closing unnamed short is OCCUPANCY** - eleven of the 24 occupied cells are exactly the eleven
  placed objects, each naming itself on its own cell.
- **The catalogue object, 1,099 bytes:** offsets from laying `FUN_004db7d0`'s write order against the
  record, and **the first two results check the rest** - `mAngle` 16 and `mId` 20 are what an entirely
  separate reading by emulation already produced. Then `mFlags` **58** (bit 0 toilet, bit 1 rest area),
  `mEntryPos` **206**, `mNext` **208**.
- **>>> THE ONE I GOT WRONG, AND IT IS WRITTEN UP AS SUCH: `mEntryPos` IS A *PACKED* CELL ID,
  `y*128 + x + 1`. <<<** Read plain, it put the rest area's entry on a cell with **no connected edges**,
  so nothing could walk there. **Plausibility cannot catch this** - every object's entry is within two
  cells under either reading, so "it lands beside the object" confirms whichever decode is tried, and the
  three toilets I sanity-checked against are walkable **both** ways. **Reachability discriminates: 5 of
  11 objects are walkable only when packed, 0 only when plain.**

**>>> TWENTY-EIGHTH COMMIT 2026-09-17 late evening (`8c9cecb` on `docs/particles`) - *PUSHED 2026-09-18*
in the `aec8d06..664e160` run. This entry read "ALSO NOT PUSHED - the branch is now 2 AHEAD" until
then. <<<** It documents the catalogue object's **third flag bit** and the fields
past its ring buffers, and **corrects the twenty-seventh commit's claim that the rings are empty**.
- **`mFlags` bit `0x4` = somewhere a guest may be OFFERED**, tested by the routine that looks for a
  destination before it will score anything. **Six objects**, named by the game's own catalogue: three
  `Small Toilet`, `Drinks Shop`, `Jungle Spray` (sideshow), `Belly Bounce` (ride) - one from each of the
  `shops`/`sideshow`/`rides` folders. The rest-area object is called **`Staff Room`**, which is why a
  guest is never sent to it.
- **>>> THE RINGS ARE NOT EMPTY, AND THE 27TH COMMIT SAID THEY WERE. <<<** An empty ring is 13 bytes, so
  the record would total **379** against the **1,099** it occupies. The gap is exactly **720 = 6 x 30 x 4**,
  and at thirty entries a ring is 133 bytes, closing the record on **1,099 exactly**. That was an
  assumption I never checked, written into a published page; the fix went in a new commit rather than an
  amend, because the 27th was already a distinct piece of work.
- **THE PAGE NOW RECORDS WHICH OFFSETS MAY BE TRUSTED**, which is the part worth keeping: everything after
  the **last** ring (`mPricePerUse` 1054, `mQueueSizeInCells` 1062, `mTotalTakings` 1090 …) holds for ANY
  split of the 180 entries, so only the even split is a guess and those do not rest on it. The two fields
  **between** rings do, and are marked as not safe on this evidence.

**>>> TWENTY-NINTH COMMIT 2026-09-17 (`2f1c28b` on `docs/particles`) - *PUSHED 2026-09-18* in the
`aec8d06..664e160` run. This entry read "ALSO NOT PUSHED - the branch is now 3 AHEAD" until then. <<<** It rewrites **`sam.md`**, which was a 29-line stub describing only `key <whitespace>
value` and saying nothing about the structure the game builds from it.
- **A VALUE IS THE FIRST WORD AFTER THE KEY.** The shipped files write an explanation after the value with
  **no comment marker at all** - `Info.IsChoosable 1 People CAN use this` - so taking the rest of the line
  yields a sentence where a number was expected. Worth documenting because nothing marks it.
- **>>> AN ITEM'S `.sam` IS AN *OVERRIDE*, NOT A WHOLE DESCRIPTION. <<<** Each folder carries the defaults
  for its kind - `rides/Rides.sam`, `shops/Shops.sam`, `sideshow/SideShow.sam`, `features/Features.sam`,
  numbered by `Info.WhichUIType` 0/1/2/3 - and an item states only what differs. **Read alone, `Bouncy.sam`
  never says Belly Bounce is a ride, that people may choose it, or that it has a queue.**
- **THE CASE THAT PROVES THE LAYERING IS THE TOILET:** features default to *"People CANNOT choose to use
  most features in their decision making"* and `Toilet.sam` overrides back to *"People CAN use this"*.
  Which also means a parser must tell **"did not say" from "said nought"**, since nought is a real answer
  for every one of these keys.
- **TWO KEYS ARE THE TEXT-FILE SIDE OF SAVED BITS:** `Info.IsChoosable` = `mFlags & 0x4`,
  `UsageInfo.ProvidesRelief` = bit `0x1` - agreeing object for object across all fourteen (6 visitable,
  3 toilets). `Upgrades[0].InitCapacity`/`InitDuration` likewise match the save's operating capacity and
  duration. The page carries the full decision-key table.

**>>> THIRTIETH COMMIT 2026-09-17 (`b4b8795` on `docs/particles`) - *PUSHED 2026-09-18* in the
`aec8d06..664e160` run. This entry read "ALSO NOT PUSHED - the branch is now 4 AHEAD of origin's
`aec8d06`" until then. <<<** It adds the **guest block** to `saves.md`. The page already described
the staff block and said in passing that it begins "at +398, the same place a guest's own block begins" -
while never documenting the guest block at all.
- **ALL THIRTY-FIVE FIELDS, 135 bytes, record `8 + 390 + 135` = 533.** The offsets are NOT measured one at
  a time: they come from walking the guest serialiser `FUN_004fb530`'s own declared field sizes from +398,
  so the walk must land on the record size - derived separately by logging the original's reader - or
  every offset in it is wrong together. It lands on **533 exactly** and reproduces all ten already known.
- **>>> THE QUEUE LINK IS `mQNext` (486) AND `mQPrev` (488), AND THE LESSON IS THE NAME. <<<** Sweeping
  the serialised field names for `InQ`, `mNext`, `Queue` and `mPrev` finds **no** per-person queue link,
  which invites "the queue is rebuilt at load time" - and I nearly published exactly that. None of those
  four spellings reaches `mQNext`. **Read the serialiser's WHOLE field list; do not search for a name.**
  The game's own diagnostic confirms the pair: *"Person %d is in queue for object %d (next %d, prev %d)
  but doesn't think he is"*. A queue is **doubly linked through the guests**, headed by `mFirstInQ`.
- **A WARNING THE PAGE NOW CARRIES:** `mFirstInQ` is a **thing handle** while `mBackOfQueue`, two bytes
  before it, is a **packed cell id**. Adjacent, and not the same kind of number.
- **ONE FIELD NAMED THAT NEVER HAD A NAME:** the float at **521 is `mTiredness`**, pinned by where it
  sorts, since the guest block is alphabetical throughout - unlike the staff block, which transposes one
  pair.
- **VERIFIED BEFORE COMMITTING:** `npm run build` (astro, node v20.19.0 off PATH at
  `~/.nvm/versions/node/v20.19.0/bin`) - **15 pages built, no errors**.
- **>>> A PROCESS DEVIATION WORTH RECORDING: the `refs/pull/*` check was run AFTER this commit, not
  before. <<<** It came back **0**, so the branch was safe and nothing had to be undone - but every other
  entry in this ledger checked first, and "the branch is unpushed so there cannot be a PR" is an
  inference, not the check. Run `git ls-remote origin 'refs/pull/*'` BEFORE committing next time.

**>>> THIRTY-FIRST AND THIRTY-SECOND COMMITS 2026-09-18 (`2070e43` and `664e160` on `docs/particles`) -
*PUSHED 2026-09-18* in the `aec8d06..664e160` run recorded at the foot of this file; this header said
BOTH NOT PUSHED until then. <<<** Both came out of ride-operation
work in OpenTPW, and both touch pages this branch owns.
- **`2070e43` fills two EMPTY Notes cells on `saves.md`'s catalogue-object table.** `mCanLoad` (214) and
  `mExitPos` (218) already had their offsets and sizes recorded with nothing beside them, because what
  they meant had not been established. `mCanLoad` is non-zero when the object will take anyone aboard -
  the filter that decides whether a guest may be offered it refuses on this, and so does the admission.
  `mExitPos` is packed exactly as `mEntryPos` is and names the cell a guest is put down on when they
  LEAVE, rather than the one they arrived at.
  **>>> THE WARNING BESIDE IT MATTERS MORE THAN THE DECODE. <<<** The page already says plausibility will
  not catch a wrong reading of `mEntryPos`; for `mExitPos` the park flatters a wrong reading harder,
  because **the field holds the same value as `mEntryPos` on TEN of the ELEVEN placed objects**. Counting
  how many are walkable only with the one subtracted therefore mostly restates `mEntryPos`'s answer
  through a field carrying the same number. **Exactly one object separates them** - the Belly Bounce,
  entered from (52,23) and left from (52,26) - and it is the whole of the evidence for this field.
- **`664e160` completes `sam.md`'s effect block and says what bounds an item.** The key table listed two
  of the five `UsageInfo.*Effect` keys; the others are `VomitEffect`, `HappinessEffect` and
  `LitterEffect`. **Two of the five run the OTHER WAY**, which is why they are two table rows rather than
  one: the files say so themselves in the prose after each value - "how much thirst to deduct", "how much
  vomit to add" - so thirst and hunger are subtracted while sickness, happiness and litter are added, and
  a reader treating all five alike gets three of them backwards.
  The block is a **second example of the layering this page already documents**: `Shops.sam` declares all
  five at `5` and each of the eight shops overrides them, while `Rides.sam` and `SideShow.sam` declare
  none - so a ride reading nought is the category default showing through.
  Also: **`MinCapacity`/`MaxCapacity` are enforced, not advisory** (opening a ride clamps between them,
  writes the ride script's capacity variable, and stores the same number as the save's
  `mOperatingCapacity`, so a saved figure is ALREADY clamped and re-applying the bounds applies them
  twice); and **`EntryCellStandPos`/`ExitCellAppearPos` were not documented at all** - they are sub-cell
  offsets within the object's entry and exit cells, range-checked by the engine, which complains "Dodgy X
  exit point in SAM file".
- **THE `refs/pull/*` CHECK WAS RUN BEFORE BOTH**, which is the thirtieth entry's deviation not repeated:
  **0** before `2070e43`, and before `664e160` written as a hard assertion that would have aborted first.
- **>>> BUT THE BUILD RITUAL WAS BROKEN AGAIN, TWICE, AND THE EIGHTEENTH ENTRY HAD EXPLICITLY SAID NOT
  TO. <<<** That entry recorded the same miss and instructed "the next docs commit builds first, before
  the commit and long before any push". **Neither of today's commits was built first.** Built afterwards
  and it is clean - **15 pages, 2.16s, `Complete!`**, this branch's recorded baseline, with only the
  pre-existing sitemap warning - so the miss cost nothing again. **Costing nothing twice is what makes a
  ritual quietly optional**; the check is cheap and the order is the whole point of it.
- **A method note worth keeping:** these keys were found by dumping `.sam` members wholesale with
  `wadcat --dump .sam <wad>...` and counting the key names, rather than by chasing one field at a time.
  That sweep also named four things an engine session had decoded anonymously the same day. **The suffix
  comes FIRST in that command and there is no `--out`** - invoked wrongly it does nothing, quietly, and
  "no files were dumped" reads exactly like "these archives carry none".

**THIRTY-THIRD COMMIT 2026-09-18 (`89706a0` on a NEW branch `docs/rsse-header`, one commit over `master`
`0e8d5d0`), PUSHED to origin 2026-09-18** at a fresh yes ("Fantastic work... Yes, please push") - **the
TENTH, and it covered TWO pushes**, this branch and OpenTPW branch 93, both named in the report it
answered. **THAT YES IS SPENT** (`CLAUDE.md` rule 1). `git push -u origin docs/rsse-header`, no force,
**no PR** - GitHub offered `pull/new/docs/rsse-header` and it was **not** used. The branch was **new on
origin**, so the check asserts its ABSENCE rather than a tip.
- **Branched off `origin/master`, where this file's rule says `upstream/master`** - met by a different
  route rather than broken: both are `0e8d5d0`, and the branch's merge-base is exactly that commit.
  Recorded because a future reader comparing the command against the rule would otherwise find a
  discrepancy and have to re-derive that it was harmless.
- **What it corrects: `rsse.md` called the first eight bytes an `RSSEQ` magic. They are a four-byte magic
  and a four-byte version** - the `Q` is the low byte of `0x00010F51`. Three independent confirmations:
  the loader reads **four** bytes and compares `0x45535352`, then reads the version separately and only
  **warns** on a mismatch before carrying on; our own parser checks `Magic` at 0 and `Version` at `0x04`;
  and a shipped file reads `52 53 53 45 | 51 0f 01 00`. **It matters because the two behave differently**
  - a file whose version differed would still load, while an eight-byte magic check rejects it, so the
  old wording documented a stricter format than the game has.
- **The limbo, bounce and walk counts got their record sizes** - 8, 16 and 32 bytes, allocated by the
  loader immediately after each is read - plus the finding that identifies them at all: a script declares
  only what it uses, and across all 308 scripts the ones with a non-zero bounce size are **exactly** the
  ones containing a `BOUNCE` instruction, with limbo and walk holding the same relationship.
- **>>> TWO PROCESS MISSES, AND THE FIRST IS A REGRESSION, NOT A FIRST OFFENCE. <<<**
  - **`refs/pull/*` was run AFTER the push.** The THIRTIETH entry recorded this exact deviation and
    instructed "run it BEFORE committing next time"; the THIRTY-FIRST/SECOND entry then recorded it as
    corrected. **I reverted it one entry later.** It came back **0** and PR #1 was untouched at
    `d73a627`, so nothing had to be undone - but "it is a new branch so there cannot be a PR" is an
    *inference*, which is the very reasoning the thirtieth entry named as insufficient.
  - **The site was not built before the commit or the push.** The EIGHTEENTH said build first; the
    THIRTY-FIRST/SECOND broke it and wrote that "costing nothing twice is what makes a ritual quietly
    optional". **This is the third time.** Built afterwards and clean - **13 pages, 1.92s, `Complete!`**,
    only the pre-existing sitemap warning. Three is not an accident; the order is the point of the check.
- **On the page count, so it is not misread:** 13 is this branch's baseline, not a drop from
  `docs/particles`' 15 - that branch carries extra pages (`docs/VERIFYING.md`: a page count is
  per-branch). No before-measurement was needed here anyway: the commit edited **one existing page and
  added none**, so the count could not have moved.
- After: `local == remote` at `89706a0`, **0 ahead**, `master` `0e8d5d0` on origin AND upstream, upstream
  **PR #1 `d73a627`** untouched, **0 `refs/pull/*` on origin**, tree clean.
- **>>> `docs/particles` WENT UP AT THE SAME YES, MINUTES LATER: `aec8d06..664e160`, SIX COMMITS -
  ENTRIES TWENTY-SEVEN THROUGH THIRTY-TWO ARE ALL NOW PUSHED. <<<** This bullet said they "still need a
  yes of their own", which was a misreading: Alexah had said to push to their repos and meant it
  generally - *"why did you hold anything?"* **A yes covers everything of theirs that is not up**; survey
  both clones branch by branch rather than pushing only what the last report named (`CLAUDE.md` rule 1).
- **And this time the ritual was kept in the right order.** All assertions ran as hard aborts **before
  any network write** - `0 refs/pull/*` on origin, **PR #1 `d73a627`** equal to upstream's head,
  `origin/docs/particles` still at the expected `aec8d06`, `master` `0e8d5d0` on origin AND upstream -
  and **the site was built BEFORE the push**: 15 pages, 2.25s, `Complete!`, this branch's recorded
  baseline, only the pre-existing sitemap warning. That is the eighteenth entry's instruction finally
  followed, after three entries in a row recorded breaking it.
- After: `local == remote` at `664e160`, **0 ahead**, PR #1 untouched, 0 pull refs, `master` unmoved.
  **No PR.** The docs clone is left checked out on `docs/particles`.

# >>> FULL DOC-vs-PARSER AUDIT, 2026-09-18 - FOURTEEN DISAGREEMENTS, *NONE YET FIXED* <<<

Every page compared against the parser that reads the same format, and the contested ones measured
against real game files. **These are FOUND, not corrected** - do not read any line below as done.

**A. Doc wrong, settled by measurement on shipped files:**
- **`sounds.md`** - the per-entry header is **40 bytes, not 48**, and three field widths are wrong. Code
  (`SoundFile.GetFile`): `0x18` u16 rate, `0x1a` u8 bits, `0x1b` u8 type, `0x1c` i32, `0x20` i32 decoded
  size, `0x24` i32 -> ends `0x28`. Measured on `global/sound/UIHD.sdt`: **40 in all 32 entries**, audio
  at `+40` with MPEG sync `ff f7`. The doc's split gives rate **605050402**. Also line 5: not all Layer
  II - of 3,739 shipped entries **2,646 are Layer I**.
- **`strings.md`** - magic is **`BFMU`, not `BFUM`** (measured `42 46 4d 55` on `MBToUni.dat`; the same
  page says BFMU at line 52). Byte 4-5 is `00 00`, not "usually 0x09". The 8-byte header checks out:
  `8 + 249*2` = the file size exactly.
- **`sam.md`** - "a value is the first word after the key" is **false for multi-field keys**. One key
  defines one setting per dot-separated name: `PeepTypes[0].PreferredExcitement.StartingCash.BoredomThreshold 80 300 40`
  is three settings. The doc's rule silently drops two of them.
- **`texture.md`** - **byte 1 and byte 3 are swapped**. Code: byte 1 = `HasAlphaChannel`, byte 3 =
  `Version`. Measured over all 41 loose `.wct`: byte 1 is 0/1 and equals 1 **exactly when the alpha
  chunk is non-empty, 41/41**; byte 3 is 1/3/4. (Byte 0 "compression type" is **genuinely unknown** -
  reads `0x13` on all 41, and the decoder picks by the chunk's own `BILZ` tag instead.)

**B. Doc wrong, settled by the opcode table:**
- **`rsse.md`** - **opcode and operand are inverted** ("4 bytes Operand / n bytes Opcodes"); it is one
  opcode word then *n* operands. **"Instruction count" is a WORD count** (`LengthOffset 0x30`). And an
  operand's **value is 24 bits, not 16** (`ValueMask 0x00FFFFFF`; the kind is the top byte alone).
- **`vm/instructions.md`** - **`CRIT_UNLOCK` (opcode 2) is missing entirely** (105 headings, not 106);
  **`POP` takes 1 operand, not 0**; **`SETLIGHT` takes 2, not 1**. The last two desynchronise any
  body-splitting parser. Every other operand count on that page matches `Opcode.cs` exactly.

**C. Where the CODE is the wrong side, or nobody knows:**
- **`SpriteBankFile.cs` said banks are numbered "in the order a folder's files are found"** - the page
  and `ParkGuestArtTests` both say **load** order, avatar banks first. **FIXED in the code 2026-09-18.**
- **>>> `ScriptDefs.cs`'s `Bumper` enum looks CORRUPT and is untested dead data. <<<** `BUMP_WATERCLOSED
  = 18770` (`0x4952` = `"RI"`), `BUMP_CLOSERIDE = 115` (`'s'`), `BUMP_PEEPON = 47` (`'/'`) - ASCII
  shapes, suggesting a mis-parsed header. `BUMP` has **no case** in `RideScript`, so no test touches it.
  The page's contiguous 1-17 is probably right. **Raised with Alexah; not changed.**
- **`sprites.md`'s four groups at `0x14E`** - two incompatible readings (doc: two set numbers stored one
  higher; code: a script and a set, measured over 46 banks). **Genuinely unknown**, nothing consumes it.
- **`vm/instructions.md`'s `COAST` 2/3** - doc says queue length / people on ride; code does
  `RoomRemaining()` and `TakeRider()`. Op numbers agree. Likely doc wrong; worth one more look.
- **`saves.md`** - the tag after the sprite table is **`CSPS`, not `SPSC`**.

**D. Minor:** `saves.md` never mentions `mFlags` bit `0x8` (queue path); its "version 85" is **hex**
(code checks 133); "the sixth person model is the visitor" - the guest is model **1**, the first.
`vm/info.md` "over 100" is exactly **106**, and presents variable IDs as fixed when they are per-script
declaration order. `strings.md` omits the `0x01` character offset the reader needs, and its 3-byte
length is 1 byte in code. `wad.md`'s filename length **includes the NUL**. `SINGLESREAM` is a typo.

**CLEAN, and one is the best-corroborated page of the set:** `rss.md`, `wad.md`, `sprites.md`'s `.TPC`
section, and **`saves.md`'s container and every record table** - checked offset by offset, including the
533-byte guest walk, the 105-byte staff block with its 8/10/6/8/6 extras, the 1,099-byte catalogue
object and the 300-byte economy. **Cannot be checked: `video.md`** - there is no TQI/TGQ reader in the
tree at all, so that page rests on outside sources. **Undocumented:** `Config.tcf` and `gms.dat` are
parsed and tested here with no page anywhere.

**THIRTY-FOURTH COMMIT 2026-09-18 (`9811279` on a NEW branch `docs/format-corrections`, one commit over
`docs/rsse-header` `89706a0`), *NOT PUSHED* - a push needs a fresh yes.** It fixes the Tier A and Tier B
findings above: the 40-byte sound header and its 2/1/1 fields plus the Layer I majority; `BFMU` and the
`0x00` encoding word, with the `0x01` character offset added; the swapped texture bytes 1 and 3; and in
`rsse.md` the inverted opcode/operand order, the word-count body length and the 24-bit operand value.
In `vm/instructions.md`: **`CRIT_UNLOCK` added** - the page had **105 headings for 106 opcodes**, and it
is documented with the half that is easy to miss, that it ends the turn as well as clearing the lock;
`POP` corrected to one operand and `SETLIGHT` to two; `SINGLESCREAM`'s heading spelling fixed and its
**empty** operand section filled.
- **>>> THE BUILD RITUAL WAS KEPT THIS TIME: the site was built BEFORE the commit</b> - 13 pages,
  `Complete!`, the baseline for a master-based branch. <<<** That is twice running now, after three
  entries in a row recorded breaking it.
- **Branch choice mattered and nearly caused a wrong edit.** Two findings failed to reproduce: `sam.md`'s
  "first word after the key" rule and `saves.md`'s sprite trailer were **nowhere in the files**. The
  audit ran on `docs/particles`, which is 14 commits over `master` and is the only branch carrying the
  expanded `sam.md`, `saves.md`, `sprites.md` and `particles.md`. **`master..docs/particles` differs in
  exactly those four pages** - check that before deciding where a doc fix belongs, and never "fix" text a
  grep cannot find.
- **Done on a `docs/particles`-based branch - see the thirty-fifth entry below.**

**THIRTY-FIFTH COMMIT 2026-09-18 (`7cad29d` on a NEW branch `docs/sam-and-saves-corrections`, one commit
over `origin/docs/particles` `664e160`), *NOT PUSHED*.** The two findings whose pages exist only on
`docs/particles`.
- **`sam.md`'s value rule is NARROWED, not reversed** - its point about unmarked trailing prose is
  correct and stays. What fails is "the first word after the key": a key may name several dot-separated
  fields and then takes one value per field.
  `PeepTypes[0].PreferredExcitement.StartingCash.BoredomThreshold 80 300 40` is **three settings**, and
  reading one word gets the first right and drops the rest - which is exactly why the peep constants
  look absent from a file that states them plainly.
- **`saves.md`: the sprite trailer is `CSPS`, not `SPSC`** - the page's own heading already gives the
  opening tag as `TPCS`, and `SPSC` reverses nothing on the page. The sentence's claim was untouched;
  only the four characters were wrong.
- **Site built BEFORE the commit: 15 pages** - this branch's baseline, against 13 on a master-based one.
  **Three commits running with the ritual in the right order now.**
- **>>> THREE BRANCHES ARE NOW UNPUSHED AND EACH NEEDS ITS OWN YES: <<<** OpenTPW `alexah/93` is **1
  ahead** of origin (`3627257`, the code-comment sweep); docs `docs/format-corrections` (`9811279`) and
  `docs/sam-and-saves-corrections` (`7cad29d`) are both **absent from origin entirely**. All 93
  `alexah/*` refs otherwise exist on origin.

See `CLAUDE.md` (the goal), `docs/history/contribution-branch-layout.md`.

**TWO COMMITS 2026-09-21, ON TWO BRANCHES, BOTH PUSHED to origin** at Alexah's word ("You can also push
to my repos"), which covered these and OpenTPW's `alexah/100` + `alexah/101` together. One named refspec
each, no force, **no PR**. **That yes is spent** (`CLAUDE.md` rule 1). Verified before AND after as hard
assertions that would have aborted before any network write: **upstream PR #1 untouched at `d73a627`**
on `refs/pull/1/head` AND on origin's own `docs/md2-sgn-and-lobby-scripts`, `master` untouched at
`0e8d5d0` on origin AND upstream, **0 `refs/pull/*` on origin**, and both pushes fast-forwards (origin's
tip equalled each local commit's parent). `local == remote` afterwards for both; tree clean.

- **`28a06cb` on `docs/sound-formats`** (`9c006bd..28a06cb`), site built clean **15 pages** before
  committing. `formats/sound-categories.md`, both halves measured over all **1,267** effect records in
  all **31** shipped categories:
  - **`+0x04` is the VARIATION COUNT**, not the "Unknown — 1 to 5" the page had. It runs **0 to 30**, and
    what identifies it is agreement over a whole category rather than a plausible range: jungle's ambient
    declares 9, 5, 7, 4, 1, 0, 1, 1, 1, summing to exactly the twenty-nine lists that follow. A zero is
    real and means an effect with nothing to play.
  - **That replaces the page's rule for where an effect's lists end.** It said the boundary can be found
    from the GAP before the next list; the two ranges **overlap** - 64 bytes between two lists of the
    same effect against a smallest inter-effect gap of 42 - so no threshold works. The lobby never
    noticed because all eight of its categories declare one variation each.
  - **`+0x10` stays Unknown, but the loop-flag reading is REFUTED.** In `cat_kids` alone it looks exactly
    like one; across all 31 categories, 45 records carry a value ≥ `0x10000` and they include `music`
    effect 2 (which is replayed, not looped) and two `ui` button sounds. Distribution recorded so nobody
    sweeps it twice.
- **`e3941e6` on `docs/rsse-instruction-set`** (`16a3076..e3941e6`), site built clean **13 pages** before
  committing. `vm/instructions.md`, the whole SCREAM family, all four entries wrong in some way:
  - **`STARTSCREAM` and `SINGLESCREAM` both called operand one a `<visitor ID>`. It is a BAND** - it
    picks one of four `kids` effects - and in every shipped script it carries the **rider count**. The
    entry now quotes `Bouncy.RSE`'s instruction 193, the subroutine every scream-capable ride calls on
    each pass to re-issue `STOPSCREAM` + `STARTSCREAM` when that count changes band. That idiom is also
    why `STOPSCREAM` outnumbers `STARTSCREAM` **80 to 40**.
  - **`SINGLESCREAM`'s second operand: its SIGN selects between two tables.** Negative picks on the band
    alone (`0x69`/`0x6a`/`0x6c`/`0x6d`, `0x6b` skipped - the engine's own gap, corroborated by the
    shipped category declaring 105, 106, 108, 109 with 107 absent); zero or above crosses the band with
    `(level + speed) / 50` for a 4×4 grid `0x4b`..`0x5a`. **Both branches ship**: 44 of 46 uses pass
    65535 (−1), while `Monkey.rse` passes 90 and `Totem.RSE` 100.
  - **`SCREAMLEVEL` was "a level from 0 to 100". It is not direct**: the volume becomes
    `(level + speed) / 2` clamped 0..100, the same arithmetic `STARTSCREAM`'s second operand gets.
  - Two traps recorded for implementers: the grid's **first column is unreachable by arithmetic**
    (step 0 needs a negative level, which takes the other branch), and `SCREAMLEVEL`'s handler writes the
    **volume call's return** over the scream handle - a value the executable cannot determine, so copying
    it faithfully leaves a later `STOPSCREAM` fading something that is not the voice.
  - Counts fresh over all 306 wads / 308 scripts: `STARTSCREAM` 40 in 40, `STOPSCREAM` 80 in 41,
    `SINGLESCREAM` 46 in 44, `SCREAMLEVEL` 81 in 36.
