---
name: project-history-archive
description: "Verbatim record of OpenTPW work already finished and pushed (branches 20-35, the two audits, the review) - split out of current-task-progress 2026-09-13 so the read-first tracker stays cheap. Read ONLY when you need the detail of a specific past task."
metadata:
  node_type: memory
  type: project
  originSessionId: f4cf68d5-1d56-4918-adad-a3f0095ef62d
  modified: 2026-09-13T22:18:18.343Z
---

**Split out of [[current-task-progress]] on 2026-09-13**, which had grown to 1,150 lines (~22k tokens) while
being the file flagged "read FIRST after any compaction" - so every compaction paid for all of it. Nothing
here is lost: this is the same text, verbatim.

**Do NOT read this file wholesale.** Grep it for the branch or task you want. What is still live - where the
project stands and what is next - is in [[current-task-progress]]; the durable per-branch ledger is
[[contribution-branch-layout]]; the cross-cutting method lessons have their own memories
([[verifying-rendering-by-capture]], [[verifying-audio-by-capture]], [[post-neoveldrid-audit]]).

## >>> WHAT THE AUDIT WAS, 2026-09-13 (finished) <<<

**THE ACTIVE TASK IS [[post-neoveldrid-audit]] - read that file, it holds the seven items and the method.**
Alexah, after the migration landed: *"Do a post-NeoVeldrid migration codebase audit/review... No jank, and do
related performance profiling+fixes as well... Ensure "drop in and run without fuss" is still true. Ensure
documentation and README are also correct and updated proper... the original plan... also needs to be
re-evaluated since we had a huge backend change. Nothing should be a band-aid."*

### The migration itself is DONE - do not redo it

**[[neoveldrid-migration-plan]] is COMPLETE.** Branch **`alexah/35-neoveldrid`**, **five commits** over
`1c80df9`, **not pushed at the time** - pushed since, re-checked 2026-09-13:

    4f6da17  the five projects to net10.0, alone, still on Veldrid
    5768015  two dead references (SharpText.Veldrid, a stray `using Newtonsoft.Json.Linq;`)
    13a0155  the NeoVeldrid swap, 37 files
    846dd0f  audio onto the one SDL - a regression the swap itself created
    e2aaab1  native search directories taken from the host, not built from the RID (fixes macOS)

Measured: **136 warnings / 0 errors** `--no-incremental`, unique code-warning set identical to the branch
point, tests **59/59**, the game runs with no environment tricks, **one** SDL, lobby **3214/3214**, `save/`
byte-identical, island frames within the flyer noise floor.

### What is still open, and what is known-broken

- ~~**Linux arm64 does NOT work**~~ **BLOCKER REMOVED 2026-09-13** - branch `alexah/36-no-in-game-editor`,
  commit **`b2aca43`**, at Alexah's word ("We don't need an in game editor at the moment and it's blocking
  progress"). The editor is no longer built into the game, so no `cimgui` is asked for: `OpenTPW.deps.json`
  names no ImGui at all, and an `LD_DEBUG=libs` run to the lobby shows **zero** cimgui. **Still NOT claimed
  to run on arm64** - there is no arm64 machine here, so only the blocker is proven gone. macOS/Windows
  arm64 are covered. See [[contribution-branch-layout]] and [[post-neoveldrid-audit]].
- **macOS is UNTESTED** - there is no Mac here. MoltenVK instead of Metal has never been run.
- **The README is comprehensively stale** - .NET 8, net8.0 paths, "Metal on macOS", "Apple Silicon needs
  Rosetta 2", "install your distribution's SDL2", the libdl paragraph. Nearly every platform claim is wrong.
- **The review plan (phases A/B/D/E/F) is still PAUSED** - [[codebase-review-and-plan]]. Note **Phase A's
  "stray Newtonsoft using" is already done** (`5768015`) and **its 109 -> 106 warning prediction is dead**;
  the basis is now 136.
- **[[lobby-finishing-plan]] must be re-priced before it resumes** (item 5 of the audit). Three of its
  thirteen are already done - tick rate (branch 28), lighting (29), field of view (30) - and its item 7
  (loading bar on a rebuild, 3214 vs ~2789) collides with review Phase D, whose gating measurement is the
  `Texture.Missing` count now being taken.

**The one lesson worth carrying into everything below: a green build proved nothing.** The migration compiled
clean and passed 59/59 while the game could not start at all. Launch it.

## DONE 2026-09-12, PUSHED: the first of the review work (`alexah/34-review-notes`)

Alexah, answering the two decisions the plan raised: *"Phase C just label them. Just add a warning to phase D.
If you have anything to commit then feel free to go ahead and push. To my repos."*

**Two commits over 966b6c4, tip 1c80df9, PUSHED to maexah/OpenTPW**, one named ref, **no PR**, verified
local == remote; upstream untouched at 453e779. **That yes is spent ([[ask-before-github]]).**

- **6f47f3a** labels `ParkCameraMode`, `Ride.cs` and `RideInfo.cs` - the three files that read as though park
  work had begun. **Labelled, not deleted, at his direction; everything under `VM/` kept.**
- **1c80df9** warns at `Texture.Missing` that every read builds another texture, and **says the cost is
  unmeasured** - the Phase D optimisation itself is NOT done.

**Comments only: 71 insertions, 0 deletions, every added line a doc comment.** Both commits built ALONE in
throwaway worktrees at **109/0** (census identical to the branch point; no CS1574, so the new `<see cref>`s
all resolve), tests **59/59**. **The game was not launched** - nothing executable changed, so there was no
load to re-check; that is the one place this departs from the usual verification.

**Every claim written into those comments was verified by me first**, not taken from the review agents:
no `new Ride(` anywhere; `RideScriptFile` exists only as a commented line and the type is absent;
`ParkCameraMode` is referenced nowhere and the sole `SetCameraMode` call is Level's; `Input.Forward`/`Right`
and RotateLeft/Right are read only there; `Texture.Cache.cs:9-10` refuses an empty path; `Texture.cs:245`
returns before registering a step; `LobbyModel.cs:86-94` is the 16-slot loop. **`Texture.Missing` has FIVE
call sites, not one** - Sky and WeatherSprites take it as a one-off fallback, UiMesh caches it, only
LobbyModel loops.

## DONE 2026-09-12: whole-CODEBASE review (investigation only, NO code changed)

Alexah: "Do a codebase review please. Ensure everything matches our end goal and design principals... Anything
not data driven like we're planning for should be considered as well... If something could be optimized while
staying maintainable and compatible, note it as well please."

**Report published: https://claude.ai/code/artifact/96d78485-ad02-4774-b0dd-c481a347e3d5**
("The Engine Under the Lobby"). 27 agents / 13 dimensions / one adversarial verifier per dimension + a
coverage critic. **130 findings raised, 4 refuted, 84 adjusted, 0 HIGH, 20 medium, 106 low.** Build at
966b6c4 rebuilt clean (`--no-incremental`): **109 warnings / 0 errors**.

**All 126 findings, published 2026-09-12: https://claude.ai/code/artifact/1a052a3a-307a-479f-969d-f197b5504096**
("All 126 Findings") - the full set with evidence, including the ~86 the report had no room for. Raw data kept
at `~/.claude/projects/-home-alex-repos-OpenTPW/review-2026-09-12/`.
**The durable standing record of all of this is [[codebase-review-and-plan]]** - kept out of this file on
purpose, so it survives this tracker being pruned.

**THE PLAN, published 2026-09-12: https://claude.ai/code/artifact/bcc2595c-715d-4ae8-8732-5b32ca28e807**
("Order of Repairs"). Alexah, after reading the review and my prioritisation: *"I agree with everything except
CI. Make a phased plan to implement your suggestions, please."* **NOTHING IS BUILT YET - the plan is a
document; no code has been changed.** ~15 of the 130 findings, five phases, each stacked per
[[contribution-branch-layout]]:

- **A `alexah/34`** small true bugs, one commit each: `Rotation.operator==`, `SoundFile` `override`,
  `Entity.cs:11` -> `= new();`, delete `ClearBoundResources` + its two call sites, `BFSTReader.cs:38`
  double-parse, the stray Newtonsoft using. **Predict warnings 109 -> 106** (CA2021 + CS0114 +
  `Material.cs:112` CS0162 go; FrameProfiler's and OptionsScreen's remain), tests **59 -> 60**.
  **Risk: fixing `==` is the one behaviour change** - grep every `==`/`!=` on a Rotation first, and check
  `Rotation.Angle`'s shortcut, which currently never fires and afterwards will.
- **B `alexah/35`** values from data. **Correct the false `LobbyIsland.cs:37-46` comment as its own commit
  first.** Then names from `THEMENAMES.str`, `DUCKINGLEVEL` from sound.sam, and the `ISLAND()` directory +
  model names (retiring the `themeName[0..3]` rule duplicated in three files). **In all three the data equals
  the constant, so "nothing looks different" IS the proof** - park name measured with branch 27's
  `scratchpad/parkname` harness. `ISLANDCAMERAPOSITION` deliberately deferred to E.
- **C `alexah/36`** retire the park-shaped placeholders: `ParkCameraMode` (+ `Input.Forward`/`Right` and the
  RotateLeft/Right bindings, its only readers) and `Ride.cs`/`RideInfo.cs`. **Keep everything under `VM/`.**
  **Decision owed from Alexah: delete or label.** I recommend delete.
- **D `alexah/37`** the load path. **Step one is a MEASUREMENT, not a change** - my claim that
  `Texture.Missing` dominates the 3214 steps is reasoning (cache hits return before `Register()`), not
  measurement; **if the count is small, drop the phase**. Then share one blank texture, cache pipelines by
  (shader, flags, output), key the texture/shader caches by path. **This WILL change `Game.LobbyLoadSteps` -
  same commit** ([[keep-loading-steps-current]]). Risk: confirm nothing deletes/mutates a shared blank.
- **E rides with PARK ENTRY (lobby-plan item 4)** - lift `SetupEntities`/`SetupHud` into a `Lobby` builder,
  `Sky`/`Water` take content by constructor, drop the unread `Global` + `levelName`, write the per-scene
  statics checklist (a comment, NOT a framework), fold in `ISLANDCAMERAPOSITION`. Deliberately last because
  doing it with park entry makes it a relocation rather than a guess.
- **F** opportunistic only: ImageSharp 3.1.6 -> 3.1.11, remove the 12.8MB `test.png` (**history still carries
  it - this does not shrink a clone**), `.editorconfig` CRLF, LangVersion preview, dead-code deletions, and
  the 42 CS8618 **only when already in the file**.

**NOT doing, and why:** CI (**Alexah refused - never raise it again**, see [[no-tooling-in-the-codebase]]);
the BFSTReader locale bug (real but speculative - no language is asked for); the input chord/WASD bug (cannot
bite in the lobby, belongs with park input); `.gitignore` (his call); ModKit as a whole (his judgement, not a
review finding); the ~100 remaining lows.

**Headline: the lobby is healthy; nearly every real defect is in the shared layer BENEATH it** - `Entity`,
`Material`, `OpenTPW.Common/Utils` and the older format readers. The coverage critic found the richest seam
precisely because no dimension had been pointed at `OpenTPW.Common/Utils`.

**VERIFIED BY ME against real assemblies/install, not taken from an agent:**
- **`Rotation.operator==` can never return true** (Rotation.cs:164-177). `IsEqualUsingDot` uses
  `float.Epsilon` (the smallest *denormal*, 1.4e-45) as a tolerance, so `1.0f - float.Epsilon` rounds back to
  `1.0f` and the test degrades to `dot > 1.0f`. On the shipped OpenTPW.Common.dll: `Identity == Identity` is
  **False** while `.Equals` is **True**. Public value type in the future modding layer.
- **`Entity.cs:11`** `GetTypes().OfType<Entity>()` filters `Type` objects for `Entity` instances - always
  empty (**258 types walked, 16 real subclasses, result 0**). The build's **only CA2021**, at
  `Entity.cs(11,49)`. Also: `Entity.All` has a **public setter**; `Entity.Level` is assigned in every ctor and
  **read nowhere**; `Entity.cs:128` is an IEqualityComparer that implements no interface, has no callers,
  does not override `object.Equals`, and compares entities **by hash code**.
- **THEMENAMES.str + the locale table bug** - see [[theme-names-and-locale-tables]].
- **`DUCKINGLEVEL 38` IS in `data/sound.sam`**, beside SFX 75 / MUSIC 60 / SPEECH 75 / MOVIE 100 which
  `GameOptions.SoundDefaults` already reads from that same file; `Advisor.cs:137` hardcodes `0.38f`.
- `UiFonts.cs:78` bounds-checks `Sets[0].Length` but indexes `Sets[SetIndex]` - latent only because all four
  rows hold 13. `.editorconfig:11` demands `crlf` while **0 tracked files are CRLF** and there is no
  `.gitattributes`. `Material.ClearBoundResources` is `return;` + unreachable body (CS0162 at
  `Material.cs(112,3)`) yet still scheduled at **245 and 268**. `OptionsScreen.cs:93`'s `const VideoCards = 1`
  makes line 351 unreachable. `content/textures/test.png` is **12.8MB tracked and referenced nowhere**.
  `ExpandedMemoryStream.ReadInt16` advances **4 bytes** (probe: position 0 -> 4).

**Top actionable by value:** `Texture.Missing` is a property, so `LobbyModel.cs:86-89` mints a fresh 1x1 GPU
texture + ImmediateSubmit + Register per empty material slot (~14 of 16 per mesh) - plausibly most of the
3214 load steps, and `UiMesh.cs:121` already shows the fix (`_blank ??= Texture.Missing`); a pipeline/layout
cache keyed (shader, flags) (Material.cs:355/386); `Texture.Cache.cs:12`'s `.ToList()` + linear scan;
`Material.UI` resource-set churn per text control per frame.

**HARNESS LESSON - a bug in MY OWN workflow, worth not repeating:** the script joined each verifier's verdict
back to its finding by **exact title match**, but an ADJUSTED verdict usually **rewrites the title** - so
~half the findings and **five whole dimensions** (engine-content, parks-reuse, rendering, hygiene, tests) were
silently dropped. Caught only because the arithmetic did not reconcile (48 survivors against 84 adjustments).
Re-joined from `journal.jsonl` with difflib fuzzy matching. **Join by index or id, never by title.**

**Flagged, deliberately NOT acted on:** `.claude/`, `.vscode/` and `.mcp.json` are untracked but not ignored,
so one `git add -A` would commit them. Alexah's rule is that the shared `.gitignore` is not touched without
asking, so it is his call ([[no-tooling-in-the-codebase]]).

## DONE 2026-09-12, PUSHED: the window wears the game's icon (`alexah/33-window-icon`)

Alexah: "make it so the game uses the original game icon when running... Right now the window icon and the
icon on the taskbar doesn't show anything meaningful." **One commit, 966b6c4**, off a541bf5. Built **alone in
a throwaway worktree at 109 warnings / 0 errors**, tests **56 -> 59** (0 skipped), lobby loads **3214/3214**,
`save/` byte-for-byte unchanged over three launches. **PUSHED to maexah/OpenTPW 2026-09-12** at Alexah's
word ("Good job, thank you. You may push to my repos please"), one named ref, **no PR**, verified
local == remote; upstream untouched at 453e779. **That yes is spent - a further push needs a fresh one
([[ask-before-github]]).**

**Where the icon is: `TP.ico` at the install ROOT, beside TP.exe** - not under `data\`, so it is opened with
`GameDir.GetPath("TP.ico")` and `File.OpenRead`, not through the mounted file system. Its four images are
**byte for byte the icon resource inside TP.exe, TP.ICD and testme.exe** (11942 = 11872 + a 70-byte
ICONDIR + 4 entries; all four md5s match), so the loose file is the game's own icon rather than a stray.
The head with the orange hair and the tongue out. **`content/icon.ico|png|tga` and the csproj's
`ApplicationIcon` are the OpenTPW project's own logo, NOT the game's** - untouched, and do not confuse them.

**The trap that makes a naive reader wrong: TP.ico leaves planes and bit count at ZERO in all four directory
entries.** Every image must be measured from its own BITMAPINFOHEADER instead. Also: the stored height is
**double** (colour rows then a 1-bit AND mask, bottom-up, rows padded to 4 bytes), and **the mask is the only
transparency these images have** - a set bit is transparent, and that is what becomes alpha. Images are
32x32@4, 32x32@8, 32x32@24 and 64x64@8; `Best` takes largest area then deepest, so the 64x64 wins.
**32-bit and PNG-compressed entries are deliberately NOT read** (reported and passed over) - a 32-bit icon
with an empty alpha channel and the shape in its mask decodes to nothing, and the game ships no such file to
test against.

**Veldrid 4.8.0 binds none of SDL_SetWindowIcon, SDL_CreateRGBSurfaceFrom or SDL_FreeSurface**, so all three
go through `Sdl2Native.LoadFunction`, the same way `SDL_SetWindowMinimumSize` and Display's four mode calls
already do. **The pixels must be pinned across the pair** - `SDL_CreateRGBSurfaceFrom` borrows where
`SDL_CreateRGBSurface` copies - and the masks are little-endian byte order (R lowest). Applied while the
window is still **hidden** (it is built with `startHidden: true`), so it never shows wearing the desktop's.

**MEASURED by asking the window manager for `_NET_WM_ICON`** - the property both the taskbar and the title
bar read, and the right instrument here, because a screen grab of the window cannot see its own icon:

    before (a541bf5, built in a throwaway worktree)   the property is ABSENT - no icon at all
    after                                            64x64, 4096/4096 pixels identical to TP.ico's 64x64
                                                     2847 opaque, 1249 fully transparent
    after, vs each 32x32                             0/4096 - so the largest was chosen, not the first

Compared against a decoder written independently of the C# one, and the same opaque counts (749/748/748/2847)
are asserted in `IconTests`. Harness: `scratchpad/icon/verify.py` (finds the window by **_NET_WM_PID**, waits
for the game's own "Loaded the lobby" line). Files: `OpenTPW.Files/Formats/Image/IconFile.cs`,
`OpenTPW.Common/Client/Window.cs` (`SetIcon`), `OpenTPW/Client/GameIcon.cs`, `OpenTPW.Tests/IconTests.cs`,
one call in `Renderer`.

## DONE 2026-09-12: Alexah's two rulings on phase one

He answered the two decisions phase one raised. **Both are instructions, not suggestions.**

> "1. I agree let's make sure it's authentic and good looking, I think the original game was lit from
> the front, facing park gates. 2. I think the old field of view was better."

**RULING 1 - DONE, commit `a309292` on `alexah/31-lobby-sun`** (off b372e3d; built alone at 109/0,
tests 56/56). `Level.SunLight.Position` (0,100,100) -> **(500, -3500, 2800)**.

**The gates all face -Y, and that was measured, not assumed.** A gate is authored in its island's own
model space and is **never rotated** - `LobbyGate` only sets a position - so the offset from an island's
centre to its gate's centre is the direction that gate faces. Through the engine's own parser
(`WadArchive.OpenFile` -> `new ModelFile(Stream)`; harness at `scratchpad/gatedir`), the bearings are
**187 / 152 / 187 / 198 degrees** for jungle / fantasy / hallow / space - all the island's **-Y** side.
Fantasy only reads off because its gate is a **421-vertex worm** whose bulk drags the centroid; its parts
sit at Y -8.5 and -13.3 with the rest. So ONE light can face all four gates. **`LobbyScript.Heading` is
about globe placement, not gate facing** - it is still read by nothing, correctly.
**Distance matters because the shader takes the sun as a POINT** (`L = normalize(g_vLightPos - vPosition)`):
the islands span 283 units corner to corner, so the direction spread is `atan(283/d)` - 4 degrees at 4000,
which reads as a sun. Height = that distance at 35 degrees elevation.

**MEASURED** on all four islands, gate side and behind, clock paused, HUD hidden, vs the same shot before:
island bodies **73.9->97.7 (+23.8)**, **70.1->98.1 (+28.0)**, **63.1->70.0 (+6.9, muted - it is under
storm)**, Space **124.8->124.2 (-0.5)**. Space's island mean does not move because its gold cliffs were
already near saturation and dominate the mask, **but its gate plainly does** - confirmed at full
resolution. Views from behind barely change, which is right (they always fell to the flat 0.4 ambient).
The sea reads the same light and is **unharmed** - contrast rose 1.19x / 1.04x / 1.07x.
**A choice, not a recovery** - the original's world geometry arrives pre-transformed with per-vertex
DIFFUSE/SPECULAR (FVF 0x1c4), so D3D does no lighting for it and there may be no sun to recover.
**Worth knowing:** the gates face away from **orbit 0, where the lobby opens**, so the camera spends much
of its time behind them and the benefit shows as the orbit comes round.

**RULING 2 - DONE, commit `b372e3d`** (branch 30 reset to 423e9ff, e1d50c0 dropped before any push; built
alone at 109/0). `FieldOfViewDegrees` stays **60**, with the comment recording what ISLANDFOV is
(island lobby copies it at `0x005e13fb`, +4 per FUN_005e2cc0; 60 is the **globe** lobby's own value at
`0x005dd034`; 100 horizontal = 83.58 vertical at 4:3) and labelling 60 **a deliberate deviation, not a
derivation**, so nobody "corrects" it back by wiring the file up. No ISLANDFOV plumbing left behind.

**State when he ruled:** four commits, nothing pushed, each built alone at 109/0, tests 56/56 -
`5d9b334` (28 tick rate), `4253f62` + `423e9ff` (29 lighting), `e1d50c0` (30 FOV, **to be replaced**).
Working tree clean but for the four untracked tooling paths. Upstream untouched at 453e779.
**The plan artifact will need republishing** once these two land (same file path keeps the URL) -
https://claude.ai/code/artifact/48cf6fc2-0874-404c-b7bf-a05fe4b16470 - and so will the branch ledger
entry for 30 in [[contribution-branch-layout]], which currently describes the 83.58 version.

## DONE 2026-09-12: PHASE ONE of the lobby finishing plan ([[lobby-finishing-plan]])

Alexah: "Go ahead and begin phase one, I believe in you." All three items are built, measured and
committed, plus both of his follow-up rulings and a retaken README screenshot - **six commits on five
stacked branches (28-32), each built ALONE in a throwaway worktree at 109 warnings / 0 errors**, tests
**56/56** throughout, `save/` byte-for-byte unchanged across every run (md5s `6b3db4f1…` / `4831a539…`).

**PUSHED to maexah/OpenTPW 2026-09-12** at his word ("commit any changes and push to my repos please"),
five named refs, **no PR**, verified local == remote on all five, `upstream/main` untouched at 453e779.
**That yes is spent - a further push needs a fresh one ([[ask-before-github]]).**

    alexah/28-lobby-tick-rate        5d9b334
    alexah/29-world-lighting         4253f62, 423e9ff
    alexah/30-island-field-of-view   e1d50c0

**Two decisions owed to Alexah, raised and deliberately NOT taken.** (1) Where the sun should stand:
`Level.SunLight.Position` is (0,100,100), a leftover of the old test scene, which only became meaningful
once 4253f62 landed - it is now a lamp low over one corner of a world whose islands sit at 400-600, so
the lobby reads flatter than before. That is the fix working, not failing, but the sun wants moving and
it is his call. (2) Whether the wider, faithful framing (60 -> 83.58 vertical) looks right to him.
**Also still undone** from the tick-rate work: reading SPINSPEED from lobby.txt (three lines in the method
that already parses the file; guard `DebugOrbit`'s divide against a modded SPINSPEED(0)).

### Item 1 DONE: `alexah/28-lobby-tick-rate`, commit **5d9b334**

Built **alone in a throwaway worktree at 109/0**; tests 56/56; `save/` byte-for-byte unchanged.
`Time.TicksPerSecond` **deleted**, not re-rated - the binary holds **three bases** and one global was a
false generalisation: lobby **10/s** (`lobby[+8] = min(ms,500) * 0.01`, FUN_005d5c50, 0x007029cc = 0.01),
sky **25/s** (FUN_00585f10 does `frameTime * 25.0` from 0x00701f7c, dropping steps past +/-25), particles
**31ms** (0x00520130). Each got its own named constant: `LobbyScript.TicksPerSecond = 10f`, a private
`Sky.TicksPerSecond = 25f`, and `LobbyScript.AssumedFrameRate = 25f` for the two **per-frame rolls**
(lightning, ambient one-shot - rolled once per rendered frame in FUN_005e0470, scaled by no delta, so
they MUST NOT be re-rated; keeping the bases apart is what protects them).
**MEASURED on the running lobby:** butterflies 37.5 -> **15.0** u/s, bats 62.5 -> **25.0**, Halloween
**strikes/s 0.391 -> 0.391 (the guard, unchanged)**, load 3214 against 3214 expected. Camera eases
1.32/2.79 -> **1.0/2.0** (the old derivation was wrong twice: wrong rate AND `-25*ln(1-f)` applied to a
factor the original already delta-scales). `LobbyWeather.SkyRate` 1.32 -> 1.0, a hardcoded duplicate of
the camera's body rate. **SPINSPEED 0.2 rad/s was faithful by luck** (0.02 x 10) - unchanged, so the
orbit is the control.

### Item 2 DONE: `alexah/29-world-lighting` - **4253f62** then **423e9ff**

**MEASURED** on Space Zone (no flyer swarm), clock paused, camera settled at two orbit angles, against
the same build captured twice as the floor: **island crop 6.0 and 7.1 mean |diff|, ~16% of pixels moved,
floor 0.35 / 0.08**; control panel **max channel difference 5, 0.00% moved**; sky band **0.20 / 0.09**.
So world geometry is relit and the interface and sky are not. The two commits, in this order:

1. **frame of reference** (shader edits applied, not yet committed): `test.shader` wrote a VIEW-space
   position into `vWorldPosition` and line 128 subtracted it from a WORLD-space `g_vLightPos`, so the
   sun rode with the camera. Renamed the varying to `vViewPosition` (**fog legitimately uses it** at the
   end of the fragment stage - do NOT repoint fog), and lit from `vs_out.vPosition`, which line 43
   already writes as true world space.
2. **world normals**: `ModelEntity.OnRender`/`OnRenderTranslucent` must pass `worldNormals: true`.
   **Do NOT make this unconditional in the shader** - `UiMesh`'s model matrix is a screen projection
   with a zeroed third row, and `mat3` of it collapses normals to zero -> NaN across the front end.

**Both are no-ops for the advisor and the whole front end**, for two independent reasons each: both draw
with an **identity view matrix** (so `g_mView * pos == pos`), the advisor already passes
`worldNormals: true`, and `AdvisorModel` sets `DrawnByOwner = true` so his draws never reach the two
edited call sites at all. **He must come out pixel-identical - that is the regression check**, and he
does follow the paused clock.

**Corrected figures to use (the verifier disproved the plan's own):** NaN collapse is **730 of 10231
(7.1%)**, ten meshes at 100% including **LOLIGHT** (not 2034/8071 = 25.2%). Mean normal displacement
**Jungle 92.6, Fantasy 101.1, Hallow 77.6, Space 80.3 degrees**, worst mesh Fantasy `Blade04` 140.7.
**Verification recipe:** test on **Space (island 3)** - it has no FLYINGMESH swarm, so the flyer noise
floor collapses to zero - and **`pause` BEFORE `orbit`**, or the angle drifts by the pipe round-trip and
differs between builds. Sequence: `island 3` / `pause` / `orbit 0` / `settle` / `step 2` / capture, then
`orbit 3.14159` / `settle` / `step 2` / capture. `state`'s `flyers=` counts FLYINGMESH *lines*, not bats.
**Do not quietly move `Level.SunLight.Position` (0,100,100)** - it is the compensating constant, becomes
meaningful only once commit 1 lands, and is its own decision to raise with Alexah.

### Item 3 DONE: field of view - **e1d50c0**

**MEASURED with the prediction computed first.** On-screen size goes as `1/tan(vertical/2)`, so 60 ->
83.58 degrees predicts `tan(30)/tan(41.789)` = **0.646**. With the **HUD hidden via F2** (XTEST; there is
no console command) so the interface could not fall inside the crop: **width 250/394 = 0.635, height
243/379 = 0.641** - both within 1.7% of the prediction and 1% of each other. Three earlier attempts read
0.730 / 0.753 / 0.690 because the buttons' gold text is warm and merged with the island in the mask; see
[[verifying-rendering-by-capture]]. The reasoning behind the change:

The code reading is solid and independently confirmed: the original has **two lobbies**, and our 60 is
the **globe** lobby's hardcoded value (`0x005dd034`), while **IslandLobby_Start copies the file value**
(`0x005e13fb`, FLD [ECX+4] / FSTP [EAX+4], where FUN_005e2cc0 stored ISLANDFOV at settings+4). So the
island lobby should take 100, read from lobby.txt at the same boundary as SPINRADIUS/VERTICALOFFSET.
**But do NOT claim a measurement:** the independent re-measurement (projecting 1775 real vertices, not a
billboard approximation) brackets the horizontal angle at **80-95 degrees and cannot separate 100 from
the high 80s**. The vertical reading IS excluded. Write the comment as a reading of the code, with the
measurement cited only for what it excludes. 100 horizontal -> **83.58 vertical** at 4:3.
Also still open from the tick-rate work: reading SPINSPEED from lobby.txt (recommended, 3 lines in the
method that already parses it; guard `DebugOrbit`'s divide).

## DONE 2026-09-12: whole-lobby health audit (investigation only, NO code changed)

Alexah: "Give an overall health report of the lobby please... Are we missing anything else for the lobby
vertical slice? Anything still not understood? I'm not asking for code changes, just investigation."

**Report published as an artifact: https://claude.ai/code/artifact/2877b10e-2953-45b6-9675-376ee2e537c0**
("Lobby vs testme.exe"). Ten parallel surveys + four adversarial verify passes (~56 claims) + a coverage
critic; **no reported gap turned out to be a false gap**. Build at HEAD b379232: 109/0, tests 56/56.

**Alexah then asked for a priority list to finish the lobby before parks. Plan published as a second
artifact: https://claude.ai/code/artifact/48cf6fc2-0874-404c-b7bf-a05fe4b16470** ("Finishing the Lobby") -
13 items ordered by DEPENDENCY, not severity. Phase 1 (tick rate, the two lighting bugs, FOV from the
reference screenshots) first because everything later is judged against them; Phase 2 closes the hole
(park entry, gate/animation sequencing, asset release, the loading bar on rebuild); Phase 3 is the visible
gaps (attract camera, sea, advisor nag); Phase 4 makes graphics quality mean something (texture set +
SKYQUALITY + particle low-detail byte, one job); Phase 5 robustness + tests. **One sitting with the
original running answers three unknowns at once** - the park-entry beat/gate doors, a real save file to
test against, and when the 90s nag arms - so do that before building items 4, 10 or 13. Explicitly cut
before parks: movies/splash, the three online trees, world-space particles, cones/reverb/Doppler, the
message box's third button, the full state machine.

**The headline finding, verified first-hand and now recorded in [[advisor-on-screen]]: `Time.TicksPerSecond`
should be 10, not 25** - so the flyers run **2.5x too fast** today. Do not change that one constant
mechanically: lightning and the ambient one-shot roll are per-FRAME and not delta-scaled, yet both convert
themselves through it.

**The slice has ONE real hole: park entry.** `IslandPanel.EnterPark` gates correctly then logs a line.
The lobby's own half is six steps (re-entrancy guard, close panel, globallobbysfx effect 4 - which ships
with no samples, so silent in the original too - KeyPuff 98 on **`_price` (0x1e0ea), NOT `_priceNumber`**,
skip both entirely in Instant Action, then teardown). `Level.Unload` already does state 3's order and is
reachable only from the debug console's `reload`.

**Other ranked gaps:** the attract camera (with nobody playing the original wanders and aims at the nearest
island - that's the player-slots shot, and we orbit island 0 instead); world lighting wrong two ways
(view-space light dir, model-space normals - which blocks any screenshot comparison meaning anything); the
sea is a flat plane against a real heightfield model `lobby/Terrain/base.md2`; asset release (the reload
path IS the park-return path); ISLANDFOV(100) vs our 60; the advisor's 90s idle nag (sample 470 never
said); robustness; no tests over any of it.

**Data read out of lobby.wad this session:** `lobby.txt` carries **ten** ISLANDCAMERAPOSITION slots (we use
four, and our hardcoded positions ARE a faithful transcription); the keyword vocabulary is closed at twelve;
`qickload.txt` is a dead end - six copies, one per asset directory, each a preload manifest made redundant
by the wad's own table. Don't investigate it twice.

## DONE 2026-09-12, PUSHED: park name off-centre (branch `alexah/27-park-name-centred`)

Alexah: "Park names aren't always centered between the advisor and control panel. It stays pinned towards
the control panel. Seems proper at resolutions that are 4:3."

Branch off `87b436e`. **One commit, b379232**, built ALONE in a throwaway worktree at **109/0**, tests
**56**. **Pushed to maexah/OpenTPW 2026-09-12**, no PR, verified local == remote.

**Cause:** `IslandPanel`'s park name carried `PinAcross = Anchor.Left` - **the only `Anchor.Left`
override in the whole interface**. The virtual screen keeps its 4:3 shape inside the window, so a wider
window has width left over and a left-pinned control sits **half that slack** left of centre. At 4:3 the
slack is zero and all three anchors coincide, which is exactly why it looked right there. The rect
(477..1571 of 2048) has its own middle at **1024 = the middle of the virtual screen**, so the layout
already asked for centre; the override was deliberate (to keep the name running on from the panel as one
strip) and is what Alexah was seeing. Fix: `Anchor.Centre`.

**MEASURED** (`scratchpad/parkname/measure2.py`), name middle minus window middle:

    1024x768 (4:3)   +0.0  ->  +0.0     control - unchanged pixel for pixel (same 2323 name px)
    1280x720        -160.5 ->  -0.5
    1600x900        -200.5 ->  -0.5
    1920x1080       -240.5 ->  -0.5
    900x1100 (tall)  +0.0  ->  +0.0     control - no horizontal slack, nothing to move

The -0.5 is an even-width glyph span, identical at every size. The original agrees: both
`content/ReferenceScreenshots/ParkPicker/*.jpg` have the name centred on the window (800-wide shot,
"Wonder Land" centre ~402 of 400; 640-wide, "Space Ghost Coast 2 Coast" centre ~319 of 320).

**The measurement trap, and how it was caught:** a first detector just looked for the name's yellow along
the bottom and read **+169px on a 4:3 window, where the answer must be 0** - jungle's butterflies and the
glint sparkles are yellow too, and they move. Fixed by grabbing each size **twice with the clock paused,
once with F2 hiding the HUD, and subtracting**, so only interface pixels count. See
[[verifying-rendering-by-capture]].

## DONE 2026-09-12, PUSHED: Instant Action has no keys (branch `alexah/26-instant-action-keys`)

Alexah: "Instant Action shouldn't have any keys at all, in the original game I believe Instant Action is
only Lost Kingdom which should be unlocked by default. I don't think Instant Action even had Golden
tickets/keys."

Branch off `1a663a6`. **One commit, 87b436e**, built ALONE in a throwaway worktree at **109/0**, tests
**56**. **Pushed to maexah/OpenTPW 2026-09-12** with 24 and 25, no PR, verified local == remote.

**They were right on both counts.** OpenTPW already withheld the key itself, but nothing else followed:
the island panel still showed the park's price (greyed, since they held none and never would), Enter this
park refused every island, and the arrows and cursor keys walked all four. The original hangs it all off
one flag - the **game type**, DAT_00fb3b7c, set from the Instant Action byte at +0x24 of gms.dat. The full
rule set and every address is in [[lobby-front-end]]; the short form is no key (0x004a6a50), no price and
no count and **greyed** arrows (0x004b9340 / 0x004b9840 - disabled, not hidden), dead arrow handlers
(0x005e1ee0, 0x005e1f40), and entry without a key check (0x005e1cc0). Closing the slots also snaps the
lobby to the first island (0x005e1fa0 with 1) = Lost Kingdom, the only park with an **Easymode.TPWI**.

**MEASURED** (`scratchpad/instant/drive.py`, both modes made through the real dialog with XTEST, because
nothing on the console can create a player):

    three ways to change island     Instant Action        Full Simulation
    panel arrow / Right / ]         no move logged x3     Wonder Land, Halloween World, Space Zone

The evidence is the game's own `Lobby camera: moving to island N` line, not the pictures - it names every
change and says nothing when nothing moves. Pictures confirm the panel: no key and no count for Instant
Action, price "x 1" on Lost Kingdom and "x 3" on Wonder Land for Full Simulation, arrows dull against
bright. `save/` restored after both runs (the player each created was removed; Config.tcf and opentpw.cfg
md5s unchanged).

**The trap worth remembering:** `UI_SetVisible` (0x0065d9dd) is **never a vtable entry** - visibility is
always a direct call - so the virtual call at slot +0x14 the arrows get is a different operation
(enable), which is what proves they are greyed rather than hidden. Also: **`island` on the debug console
SETS the island** (bare `island` selects 0), so it cannot be used to ask which one is showing.

## DONE 2026-09-12, PUSHED: island-switch stutter (branch `alexah/25-island-switch-stutter`)

Alexah: "Small but noticeable stutter when switching between islands for the first time."

Branch off `e704941` (branch 24's tip). **One commit, 1a663a6**, built ALONE in a throwaway worktree at
**109/0**, tests **56**. **Pushed to maexah/OpenTPW 2026-09-12**, no PR, verified local == remote.

**Cause:** a park's two lobby sound categories (`cat_locallobbymusic`, `cat_locallobbysfx`) were built
lazily on arrival by `LobbyAudio.ParkFor`, from inside `Entity.Update` - about 1MB of MPEG per park
decoded to ~10MB of float, on the frame the camera got there. The `_parks` cache is exactly why only the
FIRST visit ever paid it. Fix: `LobbyAudio`'s constructor preloads every `LobbyIsland`'s theme, behind
the loading screen already up (`Level.cs:73` builds it right after the four islands; `Entity.All` is
filled by `Entity()` itself at `Entity.cs:51`), gated on `Audio.Ready` so the no-device path is unchanged.

**The original does the same, so this is a restoration and not a convenience.** `IslandLobby_Start`
walks the island list and calls the island-script parser `FUN_005e3210` once per island (0x005e1a44);
that parser loads the pair inline via `FUN_0051e8f0` and appends it to one table (`DAT_00803a4c`, count
`DAT_00803a50`). Selecting an island only ever PLAYS from that table, by the index the island kept.
`FUN_005e27b0` is a second loop of the same shape. **Neither caller is a next/previous-island handler** -
that was checked rather than assumed.

**MEASURED** (`scratchpad/stutter/switch.py`, every island visited twice in one launch, clock running):

    worst frame, first arrival    isl0    isl1     isl2     isl3
    before                       16.24   71.84   116.33   103.60 ms
    after                         7.22   12.52    10.16     7.44 ms
    repeat arrivals, after        9.55   11.15     7.45    12.30 ms   <- the control

Repeat arrivals measured the same either side of the change, which is what shows nothing else moved.
Jungle (isl0) is cheap before the fix only because the lobby opens on it. Launch to lobby was 8s both
ways, and the game's own log moved all eight "Sound category" lines to BEFORE "Loaded the lobby".
`save/` byte-for-byte unchanged across all four launches.

**The measurement trap that mattered:** `Time.Delta` is clamped to `LongestFrame` = 0.1s and
`DebugConsole` records `Time.Delta`, so `stats worst=100.00ms` means ">= 100ms" and cannot size a stall.
A throwaway probe build (`Time.RawDelta`, recorded in place of `Time.Delta`) was needed to read 116.33.
Also: the `island` command's ROUND TRIP stayed flat at 6.8ms throughout, because the load runs in
`LobbyAudio.OnUpdate` on a LATER frame rather than in `DebugSelect` - round-trip timing alone would have
found nothing at all.

**Noted and deliberately NOT fixed** (same defect class, different symptom, and not what was reported):
`Advisor.cs:245` `_speech ??= new SoundCategory(...)`, `UiSounds.cs:52` `_category ??=`, and
`LobbyAudio.StartGlobal` all load from an update on first use. Those land as the lobby appears, not on a
switch.

## DONE 2026-09-12, PUSHED: frame ghosting (branch `alexah/24-frame-ghosting`)

Alexah: "There seems to be a lot of motion blur/ghosting, particularly on the cursor, but it's
noticeable other places like island switching and butterflies/bats."

Branch off `364633d`. **Two commits, 377acca and e704941**, each built ALONE in a throwaway worktree
at 109/0, tests 56. **Pushed to maexah/OpenTPW 2026-09-12**, no PR, verified local == remote.

**Two separate defects**, each with its own evidence:

- **377acca - the blit blended instead of copying.** `Renderer.CreateBlitPipeline` used
  `BlendStateDescription.SingleAlphaBlend`, and the swapchain image is NEVER cleared, so wherever the
  finished frame's alpha fell below one that much of the frame from two or three presents ago came
  through with it. Veldrid's alpha blend is SourceAlpha/InverseSourceAlpha for the **alpha channel
  too** (probed directly against the shipped Veldrid.dll - `scratchpad/blendprobe`), so any
  alpha-blended draw leaves `dstA = srcA^2 + dstA(1 - srcA)`, below one for any srcA < 1.
- **e704941 - two cursors on screen.** `Input.UpdateFrom` called `SDL_ShowCursor(1)` as its first
  statement EVERY frame while the game also drew its own sprite, so the drawn one trailed the
  composited one by a frame and a present. The original hides it: `UI_Init` calls `ShowCursor(0)` at
  **0x00489de1**, and its own cursor is a DirectDraw surface blitted around the flip at
  **FUN_00564210** (erase before, draw after), from the same **128x32 four-frame .tga strips**
  (magenta key) sitting beside the `.ani` files in `data/ui/cursors`.

**The measurement that worked, and the two that did not.**

- **WORKED:** clear the swapchain to **magenta** immediately before the blend in a throwaway build -
  surviving magenta is exactly the area whose alpha is below one, and it needs NO motion. Four
  islands, clock paused: **21.69 / 22.01 / 21.38 / 20.69%** exposed; control (same build photographed
  twice) **0.08 / 0.00 / 1.04 / 0.12%**; fixed build **0.02 / 0.00 / 1.31 / 0.06%** = the floor.
  Mostly **sky** (four alpha-blended cloud layers on an open dome), then UI gradients, foliage cut
  edges, ripple rings and the flyers. Water and terrain are opaque and were never affected.
- **FAILED - a moving-cursor sweep.** The cursor's chroma key makes its alpha 0 or 1, so it barely
  exposes at all. I had reasoned that out and then built the experiment around the cursor anyway; the
  numbers sat inside the floor (fixed scored *higher* on changed pixels than both baselines).
- **FAILED - calling a wide blob in those diffs "capture tearing".** It was the "Create New Player"
  button's **hover highlight**, appearing only on grabs where the pointer was at that end of its
  travel. Retracted the same turn.
- **Root-window grabs do not composite the pointer**, so no screen capture can see the cursor fix.
  **XFixes** answers it: `xfixes_query_version()` first (python-xlib will not dispatch otherwise),
  then `xfixes_get_cursor_image` - **10x16 opaque=94/160 before, 1x1 opaque=0/1 after**, which is how
  SDL hides one.

**Frame rate is NOT a factor** - clock running, `stats` reads **143.9 fps at 1280x720, 143.6 at
1920x1080, 143.9 at 2560x1440**, mean 6.95ms, p99 7.0-7.4ms over 240 frames. Vsync is hardcoded on
(`SyncToVerticalBlank = true`, no toggle anywhere in the tree). **With a PAUSED clock `stats` reads
fps=10.0 / mean=100.00ms / frames=2 - that is the `Time.LongestFrame` 0.1s clamp, an artefact. Never
quote it.** This also disposes of the worry that 087b3d9's two-sided drawing had cost performance.

Harnesses: `scratchpad/ghost/{sweep,alpha}.py` and `scratchpad/blendprobe`. Noted and deliberately
NOT fixed (no unrelated refactoring): **`Input.CursorType` is dead** - nothing reads it, `Cursor.cs`
keeps its own `_cursorType`.

## DONE 2026-09-12, PUSHED: lobby see-through rendering

Alexah reported four sightings: the Hallow tree rendering behind its gate seen from the back plus odd
culling round its trunk; Fantasy grass blades behind the sunflower and through the worm gate's hat;
Jungle palm leaves through each other; the Space "trees" with the stem tip over the bloom.

Branch **`alexah/23-see-through-rendering`** off `2a7be70`. **Three commits**, each built ALONE in a
throwaway worktree at 109/0. **Pushed to maexah/OpenTPW 2026-09-12** at Alexah's word, tip
**364633d**, verified local == remote, no PR. Branch 22 still at 2a7be70. A further push needs a
fresh yes ([[ask-before-github]]).

- **b2e7d05** see-through surfaces write depth, and take the original's two alpha references.
- **087b3d9** stop culling back faces (D3DCULL_NONE) - the half that visibly repairs the lobby.
- **364633d** the advisor's eyes, below.

### The advisor's eyes (364633d) - tests 53 -> 56

Alexah: "Sometimes on the player creation screen, the advisors eyes are stuck shut for a few seconds
before suddenly opening/showing." Almost certainly his eyes MISSING on a black head, not eyelids
drawn over them.

**The cause is an asymmetry in the shipped data**, measured by driving all fifteen clips through
`AnimationFile` itself (`scratchpad/blink/probe`, a C# harness against the built OpenTPW.Files.dll -
**do that rather than hand-rolling a .md2 parser**; my hand-rolled one reported 65535 tracks and mesh
targets of 104 on a 25-mesh model, and I nearly reported its garbage as a finding).

- Every **eyelid** track opens with a key at frame 0 (clip 3: `[0, 80, -84]`), so entering any clip
  re-establishes it.
- The **eye** tracks carry no frame-0 key (clip 3: `[-80, 84]`). `VisibleAt` returns null before a
  track's first key and `AdvisorModel` leaves the mesh alone then, so the eyes INHERIT.
- Every clip's eye track ends on a show key, so a clip that plays out is always safe. **Only an
  interrupted line strands them** - `Dismiss` hides him without touching any mesh - until the next
  clip's first blink: 0.33s (clip 8, frame 10) to **2.7s** (clip 3, frame 80).

Fix: `AdvisorModel.OpenEyes()`, `Dress()` delegating to it, `Advisor.ResetFace()` called from
`Dismiss` and at line start. `ResetFace` also zeroes `_mouth`, because `SetMouth` only tells the model
anything when the shape CHANGES and clip 13 hides all five mouth meshes partway through and never
shows one again.

**Not reproduced on demand, and the three new tests pin the DATA, not the fix** - they would pass with
it reverted. If Alexah sees it again that matters. **Visibility in the original is a runtime bit 0x10
on the mesh record** (Advisor_Update 0x00599880 does it for mouths), never authored into a file -
which is why it never appeared in the game-wide mesh-flag sweep. See [[original-render-states]].

- **b2e7d05** see-through surfaces write depth, and take the original's two alpha references.
- **087b3d9** stop culling back faces (D3DCULL_NONE) - the half that visibly repairs it.

**The correction I got wrong for most of the session.** I framed this as a draw-order bug and spent
the investigation there. Measured with one variable changed at a time, **culling is what repairs the
visible faults**: Fantasy blades 5.0%, Space canopies 5.6%, both on a **0.00%** noise floor, against
0.4%/0.6% for the depth change. Alexah's own words named it ("strange culling behavior to the mesh");
I latched onto their "render ordering issue maybe?" instead. The depth change is still correct and
evidenced, just small - do not oversell it.

**All four reports are fixed.** Two of them - Fantasy and Space - by measurement; the other two by
**Alexah's own eyes**: "Jungle and Halloween are confirmed fixed" (2026-09-12). I could not measure
those two at all: Hallow seeds fifty bats per run and Jungle its butterflies, from
`Environment.TickCount`, giving frame-difference noise floors of **5%** and **20%**, far above any
effect this makes, and pausing the clock does not help because the seed is per launch. The code
comments and commit messages still say those two were reasoned rather than measured, which was true
when written and was left standing rather than rewriting a pushed branch. See
[[verifying-rendering-by-capture]] for the method lesson.

**A regression I introduced and fixed mid-flight - the reusable lesson.** `test.shader` is shared with
`UiMesh`, which marks **every** interface vertex see-through (`UiMesh.cs:139`) and classifies no
textures. I first flagged *gradients*, so the whole front end fell through to the 240/255 cut-out
reference and lost the soft edge off every button. **The unmarked case must be the gentle one** - flag
cut-outs, not gradients. Any future per-vertex flag in that word has the same trap.

### Verification harness (scratchpad, session-scoped)

`foliagecap/cap2.py` pauses the clock the instant the lobby loads and advances only via `step`, then
grabs the window by pid; `analyse.py` does the three-way attribution. **Always capture a control run of
the same build**, and measure the noise floor on the **same crop** as the signal - a whole-island floor
of 0.13% became **20%** on a palm-tight crop. The advisor is animated and his phase is NOT controlled
even by `pause`, so every apparent change to him was run variance; exclude him or claim nothing.

## Audio remains at a deliberate stopping point (2026-09-12)

**Alexah weighed carrying on with audio and decided against it:** "I agree that the audio system is
good for now. We will revisit anything further with it later." **Do not re-propose source cones or the
distance-model recovery as the next task** - the reasoning below is why, and it has not changed.

The lobby has pan and distance, both measured and pushed (branches 21 and 22, tip **2a7be70**). What
follows is groundwork already paid for, kept so that picking it up later is cheap. **Its trigger is
park loading**, which is the real blocker under both. Next branch, whenever there is one, is
`alexah/23-*` on top of 22; a push needs a fresh yes ([[ask-before-github]]).

**Why cones wait rather than being cheap and nice.** The lobby's only emitter is `ant_emitter` on a
dish that turns a full revolution every ~3.3s - but the NODE does not move. `MeshRotator` rewrites the
*mesh's* `LinearTransform`, and `_descendants` holds mesh indices only, so node 27 has no entity at
all. A cone taken from `Node.WorldTransform` would point one fixed way while the dish visibly turns
under it, which is worse than having no cone. And `ant_emitter` has **no id record**, so the original
never resolved it as a cone source in the first place - `FUN_00556b90` finds nodes by id AND
capability mask. The real article is the in-park gate's `sound node` (idFlags 0x211). Likewise
`MINRADIUS` is tiered against *park* distances, so recovering it now would mean building a model
nothing can sanity-check.

### 1. Source cones

A cone is a third gain factor alongside pan and distance, so it folds into `_targetGainLeft/Right` in
`Voice.Locate` exactly as attenuation did - **`MixInto` needs no change**. `Node.WorldTransform` is
already published (d07435b), so a node's orientation is reachable, and QMixer's `SetSourceCone` is one
of the 27 resolved imports (`SetSourceCone2` is not).

**The traced offsets reconcile - reasoned out 2026-09-12, NOT yet confirmed in Ghidra.**
[[original-positional-audio]] records `FUN_00556b90` taking position from `+0x30/+0x34/+0x38` and the
cone direction from `+0x20/+0x24/+0x28`. That matches neither the file record (matrix 16 floats at
**+0x10**, translation at **+0x40/+0x44/+0x48**, verified to 0.000E+000 this session and now documented
in `models.md`) nor a matrix-relative reading of it. It fits exactly, though, for a **runtime** object
whose 4x4 begins at **+0x00**: row 0 at +0x00, row 1 at +0x10, **row 2 at +0x20 = the direction**,
**row 3 at +0x30 = the position**. So the cone axis would be the node's own third basis axis, and the
position its translation row, both consistent. **Treat as a hypothesis** - confirm against a node
whose facing is obvious in the model before building on it.

### 3. The original's park distance model

Goal: replace our *chosen* falloff with the original's actual one, for parks. The recovery technique
is now known and written up in [[original-positional-audio]] - search for the **RVA of the
IMAGE_IMPORT_BY_NAME record** (`nameHintAddress - 0x400000`), not the string and not a VA.

Where it got to: `SetDistanceMapping`'s IAT slot **006fd2a4** is called once, at **006d2963**, from a
thin wrapper at **006d2930** that copies three caller-supplied dwords into `{cbSize=0x10, a, b, c}`.
The three values come from one level further up. The wrapper's own address appears exactly once, at
**00711980**, which looks like a vtable slot - that is the next thread to pull. Ghidra has that whole
region undefined as functions, so `disassemble_at` may be needed (**it writes to the project** - look
before modifying). `data\sound.sam` carries `RadiusInfo[0..2].MINRADIUS` = 100.0 / 0.5 / 0 against
`SWITCH` 25 / 50 / 75, tiered by sound detail; "MINRADIUS" is in the exe at **00746fc4** (found by
`search_bytes`; `list_strings` misses it as it is not a defined string), so it is live, not vestigial.

### Scratchpad harnesses that exist today (session-scoped - they vanish with the session)

Under `scratchpad/`: `pancap/{capture,analyse,distcap,distanalyse}.py` (SDL disk-driver capture and
burst analysis - **the file is 44100Hz whatever the game's log says**, and SDL_DISKAUDIOFILE must be
absolute, see [[verifying-audio-by-capture]]); `nodedump/` (node tree and world positions through the
engine's own ModelFile); `catdump/` (a sound category's effects and sample names); `camerabasis/`
(camera basis, pan and distance around the lobby orbit); `nodesweep/*.py` and `nodenames/tpw.py` (a
throwaway WAD/RefPack reader driving whole-corpus sweeps).

## DONE 2026-09-11..12, PUSHED: positional audio in the lobby

Alexah: "Begin making a plan for making positional audio work now... The original game had it, we
should too. Space Zone may be a good test island for this, I clearly remember the 'chatter' sound
effect emitting from the spinning antenna at the gate."

**Status: DONE and pushed - branch `alexah/21-positional-audio`**, created 2026-09-12 off
`alexah/20-portable-paths` (tip 32e755e). Alexah: "Begin work on locational audio as per your plan...
I did some testing myself earlier, some settings may have been changed. Resetting everything to
default is fine and should be the behavior for testing anyway." **Pushed - see the block below.**

**STEP 1 OF 6 DONE - node names.** `ModelFile.Node.Name` is read from record **+0x54**, the same word
the mesh path has always read as `nOff`; the 88-byte transform-only node records carry it too and it
was simply never read for them. **`w+0x50` is NOT a pointer** - it is the node's own index (0..n),
which is why reading it as a string gives garbage; do not go down that road again. Verified two ways:
the engine's parser now reproduces an independent python dump exactly for `Spa_isle.MD2` (29 nodes)
and space's in-park `gates.MD2` (11 nodes) - names, parents, flags, idFlags, ids and translations all
identical. Three install-gated tests added in `source/OpenTPW.Tests/ModelTests.cs`; suite goes 31 -> 34
passed, 0 skipped, with `OPENTPW_GAME_PATH` set. Committed as **0ae2c04** - c8baa6e was the pre-amend
hash and no longer exists in the repo.

Two further confirmations, both run by me rather than taken from a subagent:
- **The engine treats +0x54 as a pointer.** Its .md2 loader `FUN_0046d6d0` relocates stored offsets at
  load; the node loop (count `nodeCnt(0x42) - meshCnt(0x44)`, base `piVar8[0x1d]`=0x74, stride `0x16`
  ints = 88 bytes) relocates exactly four words - `piVar4[-1]`=+0x04, `*piVar4`=+0x08, `piVar4[1]`=+0x0C
  and `piVar4[0x13]`= 8+76 = **+0x54**. The mesh loop relocates that same `piVar4[0x13]` as the name.
- **Whole-game sweep** (`scratchpad/nodesweep/sweep.py`, my own): 312 wads, **839 static models, 1279
  animation files, 7530 node records**. Name pointer resolves to a NUL-terminated ASCII string in 7520,
  index word matches in 7521 - and **every one of the 19 failures is the single malformed
  `wr_tunnel.md2`** (levels/jungle/rides/wateride.wad; mesh and node tables share one bogus offset, name
  pointers read as float bit patterns). 6 names are legitimately empty, longest is
  `StackedTrackOutgoingDummy` at 25 chars, zero non-ASCII, zero unterminated. This is why `NameAt`
  bounds-checks and reads to the terminator instead of taking a fixed width.

**STEPS 2-6 DONE - the listener, the pan, and the antenna placed.** Three commits on
`alexah/21-positional-audio`: **0ae2c04** node names, **6f4eeb9** listener + per-voice stereo gains,
and the placed lobby ambience. Build 109 warnings / 0 errors throughout (the branch-point baseline),
tests **31 -> 48 passed, 0 skipped**.

- `AudioListener` (position + level right ear), `Audio.SetListener( position, FORWARD )` called once a
  frame from `Level.Render` right after `Camera.Update()`, recomputing every placed voice under one
  lock acquisition. `Voice` carries `Vector3? _position` and per-ear gains stepped **per sample** like
  the ducking ramp. A flat sound is bit-identical to before: gains 1/1, steps 0, `value * 1.0f`.
- Pan law: `gainL = 1 - max(pan,0)`, `gainR = 1 + min(pan,0)`, so centre is 1/1 and gains only ever
  reduce (the mix already sums to 1.18 pre-master). **No distance attenuation and no cones in pass one.**
- `ModelFile.Node.WorldTransform` published; `LobbyModel.TryGetNode` / `LobbyIsland.TryGetNode` look a
  node up by name; `LobbyAudio` caches the island's marked place on arrival and passes it to the
  one-shot roll. `SoundCategory.Play` takes an optional position and hands it straight on.
- New debug command **`place [x y z]`** (behind OPENTPW_DEBUG_CONSOLE) plays effect 2 anywhere.

**MEASURED 2026-09-12** (`scratchpad/pancap`, SDL disk driver, predictions computed BEFORE the run):

    placed                    predicted   measured   L/R dB
    hard right                    +1       +1.000    left exactly 0
    hard left                     -1       -1.000    right exactly 0
    straight ahead                 0       +0.000    0.00
    antenna, orbit 90 deg     -0.146       -0.146    +1.37
    antenna, orbit 270 deg    +0.143       +0.143    -1.34
    (control: lobby audio before muting)   +0.001    -0.01

Burst spacing 12.25/12.25/14.30/14.30s matched the command spacing exactly, and durations
7.60/7.60/7.65/7.65/6.65s matched the samples the log said were picked (space2_lp1 7.58s x4,
space2_lp2 6.59s x1). See [[verifying-audio-by-capture]] for the two traps hit doing it.

**PUSHED to maexah/OpenTPW 2026-09-12** at Alexah's word ("Yes, please push to my repos"), tip
**d07435b**, no PR - GitHub offered the link and it was not used. All three commits were built alone in
throwaway worktrees first (109 warnings / 0 errors each), and `upstream` was left untouched. A further
push needs a fresh yes ([[ask-before-github]]).

**Format docs updated and pushed the same session** ([[opentpw-fileformats-docs]]): `models.md` on
`docs/advisor-model-formats`, **two commits, tip 19ee4d2, pushed to origin 2026-09-12** at Alexah's word,
verified local == remote, no PR. 0b845d3 documents the node name blob and identifies the
previously-unknown +0x50 as the record's own index; 19ee4d2 settles a contradiction the page carried -
"130 of 7533" sheared nodes, stated with no test attached, where the corpus is 7,530 and **no threshold
reproduces 130** (120/165/171/172 by perpendicularity at 0.01/0.005/0.001/1e-4, and 45 by
`Matrix4x4.Decompose`). Each count on the page now carries the test that produces it.

## DONE 2026-09-12, PUSHED: distance attenuation (branch `alexah/22-distance-attenuation`)

Alexah: "Go ahead and implement distance attenuation then, please." Branch created off
`alexah/21-positional-audio` (d07435b). **Committed as 2a7be70, builds 109/0, tests 48 -> 53, built alone
in a throwaway worktree and PUSHED to maexah/OpenTPW 2026-09-12** at Alexah's word ("yes, please go ahead
and push it"), no PR. Verified local == remote; branch 21 unchanged at d07435b; upstream untouched.

**MEASURED 2026-09-12** (`scratchpad/pancap/{distcap,distanalyse}.py`): twelve sounds placed along the
camera's own forward from a frozen orbit 0, at 40 / 80 / 160 units, so pan is zero at every one and
only distance differs - and the capture read **0.00 dB between the channels on all twelve**, which is
the control that proves distance was isolated from pan. Ratios taken **only between bursts that drew
the same sample**, because space2_lp1/lp2/lp3 are not equally loud and a cross-sample ratio would
measure EA's mastering:

    40 -> 80     predicted -0.82 dB    measured -0.80, -0.84
    40 -> 160    predicted -6.84 dB    measured -6.83, -6.86
    80 -> 160    predicted -6.02 dB    measured -6.02, -6.02

Worst error over six comparisons: **0.02 dB**. save/ byte-for-byte unchanged either side of the run.

- **The law is ours, not a recovery, and the reason matters:** the original's lobby was entirely flat,
  so there is no original lobby distance behaviour to restore. `AudioListener.AttenuationTo` uses
  **inverse amplitude** - `gain = min(1, reference / distance)` - which is the physical law (amplitude
  falls as 1/r; it is intensity that falls as 1/r^2), clamped so gains only ever reduce.
- Folded into the existing `_targetGainLeft/Right` inside `Voice.Locate`, so **`MixInto` is untouched**
  and the per-sample stepping built for the pan carries distance for free.
- `Audio.ReferenceDistance` **defaults to 0, which means off** - a scene opts in. `LobbyAudio` sets it
  on island arrival from `LobbyCameraMode.NominalDistance` = `sqrt(SPINRADIUS^2 + VERTICALOFFSET^2)`
  ~= **72.80**, and clears it in `OnDelete`. The 70/20 stay in the class that reads lobby.txt.
- **`sound.sam` carries the original's own distance parameter:** `RadiusInfo[0..2].MINRADIUS` =
  100.0 / 0.5 / 0 with `SWITCH` 25 / 50 / 75, tiered by sound detail exactly like `ChannelInfo`'s voice
  counts. "MINRADIUS" **is** in the exe (search_bytes finds it at 00746fc4; `list_strings` misses it
  because it is not a defined string), so it is live rather than vestigial. **Not traced further** -
  it is a park parameter, not a lobby one.
- See [[original-positional-audio]] for the corrected note on finding the QMixer IAT, which this work
  disproved.

**Spa_isle.MD2's chain to the antenna emitter**, read from the data:
mesh 0 `Space Island` (root) -> node 23 `Dummy01` (-0.177, 31.981, -12.588) -> mesh 8 `body`
(0, 0, 2.75) -> mesh 9 `Antennae01` (-0.253, 10.495, 1.054) -> node 27 `ant_emitter`
(0.428, -0.672, 1.831), with node 26 `ant_emitternew` alongside it. **Neither has an id record** - the
id table covers only nodes 13..22, the ten `Dummy0x` path nodes at idFlags 0x111 - so in the lobby they
are named positions only, exactly as [[original-positional-audio]] says. Model space is Y-up here.

**The machine's state when work began** (Alexah had been testing): `save/Config.tcf` decodes to version
1, card rendering, screen resolution **2 (800x600 - the one non-default)**, graphics medium, primary
card, movie sound on at 100, audio quality 50. `save/opentpw.cfg` says `display windowed` and
`fullscreen 2560 1440 144`. **`save/users/` is empty**, so the three group volumes are still at
sound.sam's defaults - measurements are not skewed. Backed up to `scratchpad/savebackup` before any run.

All the traced facts about the ORIGINAL's 3D audio are in [[original-positional-audio]] - read that
first, it is the substance. What follows is only the plan and OpenTPW's side.

### What OpenTPW has today (mapped by subagent, 2026-09-11)

One SDL2 device: **f32, 22050 Hz, stereo, 1024-frame buffer (~46ms), 32 voices**, quietest-non-loop
stealing. `Audio.Play` is the sole entry; `SoundCategory.Play:127` its only caller. **No pan, no
distance, no listener anywhere** - a voice's whole spatial state is one scalar gain at
`Voice.cs:254`, and a mono clip is written identically to both channels (`Voice.cs:256-261`).

- Buses Effects/Music/Speech; master 0.5 by default for headroom; worst-case sum measured at 1.18
  pre-master and the mix never clips, so **attenuation may only ever reduce** - no near-field boost.
- The **ducking ramp is the precedent to copy**: `Audio.cs:342-362` computes duck/duckStep once per
  buffer and `Voice.cs` steps it per sample. A pan applied per buffer would be a staircase of clicks.
- Threading: game thread + SDL's audio thread. `Audio.Lock` guards Voices/BusVolumes/duck and is held
  by the mixer for a **whole buffer**. Do the maths on the game thread; hand the mixer a start gain
  and a per-sample step only.
- `Camera.Position`/`Camera.Rotation` give the listener basis. World is **Z-up, +X forward, -Y right**
  - `Rotation.Right` is (0,-1,0), so mind the sign in the pan dot product.
- **`Camera.Update()` runs in `Level.Render()`, AFTER entity updates** (`Level.cs:177-179`), so a
  sound started during update sees last frame's pose. Update the listener right after Camera.Update.
- Scale: islands at (400,400),(600,400),(600,600),(400,600) - 200 apart; camera orbits at radius 70,
  vertical offset 20. `LobbyLightning.NearCameraFade` (band 25->55 via `FadeBetween`, a clamped
  smoothstep) is the house precedent for a falloff - "a threshold pops".
- The one place a position exists and is thrown away: thunder. `LobbyWeather.cs:137` has the bolt
  origin; `LobbyAudio.Thunder()` takes no argument.
- `MeshRotator` **turns a mesh about its own origin and does not move it** - only descendants get
  their Position rewritten. So a spinning antenna's emitter position is **static**; no per-frame
  position needs to cross the audio-thread boundary for this test case.
- Avoid the name `Emitter` - the particle system owns that word.

### The plan (as presented and accepted in principle)

1. **DONE** - **Parse node names** into `ModelFile.Node.Name`. Was read past, so `ant_emitter` was
   invisible. Small, self-contained, useful beyond audio. See the status block above for the layout.
2. **A listener**, updated once a frame from Camera in `Level.Render()` just after `Camera.Update()`.
3. **Per-voice `gainL`/`gainR` + per-sample steps** in `Voice.MixInto`; 1/1 reproduces today exactly.
4. **Optional `Vector3?` position** on `SoundCategory.Play` and `Audio.Play`; null = flat, so all
   eight UI/speech/music call sites are untouched. Mirrors the original's single entry point.
5. **Gains computed on the game thread**, refreshed for live voices each frame because the listener
   orbits - **one lock acquisition per frame for one pass over all voices**, not one per voice.
6. **Test case:** a sound source at the antenna's world position on the space island playing
   `locallobbysfx` effect 3 (`alien1`/`alien1c` - the chatter Alexah remembers, which OpenTPW already
   plays positionlessly as a random one-shot).

**CORRECTION 2026-09-12 to what this said about the emitter standing still.** The old note claimed
`MeshRotator` turns a mesh about its own origin so the emitter is static. The mechanism is different
from that: `ant_emitter` (node 27) has **no `ModelEntity` at all** - `LobbyModel` makes one entity per
MESH - and `MeshRotator._descendants` is built from **mesh** parent indices only
(`LobbyModel.cs:129-130`), so no mesh has parent 9 and `_descendants[9]` is empty. On the Space island
`MeshRotator` therefore rewrites exactly one field per frame, `Entities[9].LinearTransform`, and moves
nothing. A node hanging off that mesh would sit frozen while the antenna turns under it.
**The conclusion still holds, but for a numeric reason, not a structural one:** the emitter is
(0.428, -0.672, 1.831) from the antenna's origin, about 2.0 units, against a listener orbiting at 70 -
at most ~1.6 degrees of angle, inaudible. If it is ever wanted exactly, the cheap formula is
`swizzle(local translation) * Entities[parentMesh].LinearTransform + Entities[parentMesh].Position`,
which follows the spin for free because the rotator rewrites that LinearTransform every frame.
The antenna's clips are 100 frames at 30fps, so a revolution is ~3.33s (~6.67s for the M1/M2 pair).

**Verification:** `freeze` and `orbit` already exist on the debug console. Park the camera at known
orbit angles, capture the mix through SDL's disk driver ([[verifying-audio-by-capture]]), and measure
the L/R energy ratio.

**CORRECTED 2026-09-12 - "hard left then hard right" was wrong, and measured numbers replace it.**
The antenna emitter sits only ~10.4 units south of the island centre, and the lobby camera always
looks AT that centre, so the emitter is nearly on the view axis at every orbit angle. Measured with
`scratchpad/camerabasis` over the real geometry (island (400,600,0), CameraHeight 35, SPINRADIUS 70,
VERTICALOFFSET 20, emitter (400.570, 589.559, 40.464)):

    orbit angle      0     45     90    135    180    225    270    315
    pan          -0.007 -0.099 -0.146 -0.109 +0.009 +0.120 +0.143 +0.088
    distance      81.75  78.73  71.70  64.27  61.31  65.14  72.80  79.44

So **|pan| peaks at 0.146** - gains 0.854/1.000, about **1.4 dB** - a clean half-cycle negative from
0 to 180 and positive from 180 back round, crossing zero at both. That is measurable by energy ratio
but it is NOT dramatic, and any claim of "hard left / hard right" for this emitter is false.
**Distance is the bigger effect for this source** (61.3 -> 81.8, a factor 1.33, ~2.5 dB under an
inverse law) - which is an argument for a distance term as its own step, kept out of pass one so the
capture isolates the pan alone. A source further off the look axis - an in-park gate's `sound node`
is ~29 units out - would pan strongly; the antenna is a quiet test case, not a showcase.

**BUG FOUND AND FIXED ON THE WAY: `Camera.Rotation.Right` is unusable as a listener basis.**
`Rotation.LookAt` (Rotation.cs:184) builds the shortest arc from `Vector3.Forward` onto the direction
and has **no roll control** - its `up` parameter is only consulted in the antiparallel degenerate case.
So around the lobby orbit `Rotation.Right` tilts out of horizontal by **|Z| up to 0.960** (at 75deg),
and at 90deg it points the **opposite way** to the camera's true right, giving pan +0.146 where the
truth is -0.146. The picture never shows this because `Camera.CalcViewProjMatrix` uses
`Matrix4x4.CreateLookTo(position, Rotation.Forward, Vector3.Up)`, which re-squares the basis against
world up and only ever consumes `Forward`. **`Forward` is trustworthy; `Right` and `Up` off a LookAt
rotation are not.** The listener now derives its own ears as `Forward x Up` (this world's convention:
(1,0,0) x (0,0,1) = (0,-1,0) = `Vector3.Right`), in `AudioListener.Facing`, and `Audio.SetListener`
takes a FORWARD so no call site can re-enter the trap.

**Out of scope, stated to Alexah:** no reverb (EAX, not QMixer - see [[original-positional-audio]]),
no Doppler (the original set listener but never source velocity), no source cones in pass one, and
**non-selected islands stay silent** - distance attenuation could make the other three audible at
~200 units, which would change *what* plays, not just where.

### Space Zone specifics (subagent, verified against the data)

- `space.txt` lives inside `data/lobby.wad` at byte 18636, **stored uncompressed**, and is two lines:
  `ISLAND(3,"data\lobby\terrain","spa_isle","spa_gate","Space",0.0,35.0)` + `SKYCOLOUR(125,8,8)`.
  There is no `data/lobby/` directory on disk. "Space Zone" is OpenTPW's own display name table.
- The lobby **gate** model is hatch + 2 sign panels - **no antenna**. The antenna is on the **island**
  model: `Spa_isle.MD2` mesh 9 `Antennae01`, parent mesh 8 `body`, rotated by Spa_isleM1/M2.
- Space's `locallobbysfx` - **read out of the real .map files 2026-09-12** (`scratchpad/catdump`), one
  bank (`Sound/LobbySfx` -> `levels/space/Sound/LobbySfxHD.sdt`, 14 samples), **four effects, ids 2-5**
  (there is no effect 1 here - the theme comes from the separate `locallobbymusic` category):
  - **2** (delay 2000ms): `space2_lp1` 7580ms / `space2_lp2` 6589ms / `space2_lp3` 8676ms - all real.
  - **3** (delay 1000ms): `alien1` 1240ms, `alien1c` 1445ms, `odd_6` 523ms, **then SIX `blank.mp2` of
    9ms**. Weights are cumulative out of 65535, so the three real samples hold 21843/65529 - **two
    rolls in three play silence.** That is the game's own thinning, and it means the chatter fires far
    less often than the one-in-sixteen-a-frame roll suggests. Any capture must allow for it.
  - **4** (delay 1000ms): spaceship1/8/9 then three blanks - half silence.
  - **5** (delay 1000ms): two variations - space_set1/space_spot2/space6_set1 + 3 blanks, and
    spaceboing + 3 blanks.
  So for measurement use **effect 2**, the only one with no blanks in it.
- **Swept all eight lobby models 2026-09-12** (`scratchpad/nodedump`): Jun/Fan/Hal islands carry only
  `Dummy0..Dummy09` (idFlags 0x111, the particle path nodes), `Spline path`, and jungle's `l_tree1` and
  `l_tree2`. **All four lobby gates carry nothing but a root `Dummy01`.** So **`ant_emitter` on
  `Spa_isle` is the only place-marking node in the whole lobby** - the memory's claim is now verified
  rather than assumed. `Node.WorldTransform` agrees with an independent composition to 0.000E+000 on
  every one of those models.
- There is **no "chatter"** by name anywhere in the game data or the exe.
- `morse2_1..5` (radio chatter, the best name match) are in the in-park **`ambient`** category, which
  the lobby never loads.
- **Deferred bug found on the way:** `cat_ambientSFX.map` declares 9 effects at 0x1C but contains 55
  sample groups, so `SoundCategory`'s "list n belongs to effect n" rule does not generalise. Harmless
  now, will bite when parks load `ambient`.

## DONE 2026-09-11, PUSHED: run on Windows, Mac or Linux without fuss

Alexah: "make a plan to solve the Windows path issue so it can be run on Windows, Mac or Linux without fuss...
Ideally we just have the game files copied from the disc in a non-protected directory... and simply use this in
place of the original executable." Then: "implement as per your plan. Ensure the readme.md gets updated."

Branch **`alexah/20-portable-paths`** on top of 19 (`6d46577`), **nine commits**, tip **32e755e**. Every commit
built alone in a throwaway worktree. **Pushed to maexah/OpenTPW 2026-09-11** at Alexah's word, no PR; a further
push needs a fresh yes ([[ask-before-github]]).

- **d522673** find the installed game rather than being told where it is (GameDir)
- **906bf02** let the engine's own files travel with the binary (ContentDir + csproj)
- **3fb0962** match the game's file names the way Windows matched them (the case layer)
- **099d3e1** find dlopen on a Linux where libdl has been merged into libc
- **aca04ed** choose the graphics backend by platform, and let Veldrid build the shaders
- **d46e55f** let the tests find the game, or skip when there is none
- **068ffa0** stop asking Windows for administrator, and the editor for Explorer
- **11ae4fe** say how to build it, point it at a game, and run it (the README rewrite)
- **32e755e** take the README's screenshot from the game as it is now

The screenshot was staged through the debug console and can be retaken the same way -
`scratchpad/cap/shot.py <bin> <out.png> hallow`: it picks the island by asking the game for its name, settles
the camera, forces the rain, and calls `greet` so the advisor is on screen, leaving the clock running so the
rain falls. 1600x900. Run capture scripts from their own subdirectory with PYTHONSAFEPATH=1 - the scratchpad
holds an `enum.py` that otherwise shadows the stdlib and breaks Xlib.

### What was wrong, and what it is now

- **GamePath** was the only input, defaulting to `C:\Program Files (x86)\Bullfrog\Theme Park World` in three
  files, never written, with args ignored. `GameDir.Find( args )` now takes the first candidate holding the
  game: `--game`, `OPENTPW_GAME_PATH`, the setting if moved off its shipped value, the build's folder + 5
  parents, cwd + 5 parents, the Windows default last. **No registry step** - the original's key
  (FUN_005f8ae0) could not be shown to hold a path at all, so it would have been speculative.
- A folder counts when it holds `data` with `levels` in it, matched case-insensitively. An explicitly named
  folder is **never fallen through** - missing, wrong, or a short-name copy, it says which and stops.
- **Short-name (non-Joliet) copies are detected**: CHALLE~0.SAM / ONLINE~0.SAM / _RESOL~0.SAM present with no
  Challenges.sam. No case layer can repair those - they are different names, not different spellings.
- **content/** (shaders + one font, 232KB) is copied to the output by the csproj and resolved against
  `AppContext.BaseDirectory`. Shader includes resolve relative to the including shader, so 3d.shader and
  water.shader lost their full repo paths. The game no longer cares what directory it starts from.
- **Case layer** in BaseFileSystem: `Resolve`/`RealName`/`ArchiveName`/`Listing`/`Forget`, generalising
  SoundBank.Resolve. Never lower-cases; matches each part against what the parent really holds. Backed by a
  per-directory listing cache - **measured faster than what it replaced**, 13.1us -> 0.9us per lookup.
  `Inside()` checks the whole name, not just the prefix, because basePath no longer has a trailing separator.
- WadArchive builds and reads its tree with ONE ordinal case-insensitive compare (was ordinal-sensitive going
  in, culture-insensitive coming out) and splits on both separators.
- **Game.cs's duplicate `.WAD` registration is gone** - and must stay gone only alongside the listing-based
  probe. Changing the handler comparer alone collapses `.wad`/`.WAD` onto one key and makes lips.WAD
  unreachable. (Proven by the tests design agent.)
- **libdl**: vk.dll declares `[DllImport("libdl")]`; glibc merged libdl into libc at 2.34. A resolver
  registered **against vk.dll** (not ours) maps it to `NativeLibrary.GetMainProgramHandle()`. The hand-made
  `bin/.../libdl.so` symlink is no longer needed.
- **Backend**: Metal on macOS, Vulkan elsewhere incl. Windows. ShaderCompiler's tail replaced with
  `CreateFromSpirv`. **CompileVertexFragment still takes the GLSL source bytes** - compiling to SPIR-V first
  strips debug names and Material then throws "wasn't bound at draw time!" at the first draw.
- Tests: `GameData.Required()` asks GameDir and skips via `Assert.Inconclusive`. New install-free
  `FileSystemCaseTests` (6 tests). app.manifest asInvoker; ModKit uses xdg-open/open/Explorer per platform.

### Verified (evidence, 2026-09-11)

- **Warning set identical to the branch point**: 59 distinct warnings, none new, none gone (diffed against a
  worktree build of 6d46577). Raw totals move with which projects rebuild - 109 vs 113 is an artefact, not a
  change.
- **Tests 31 total**: 25 passed + 6 skipped with no install findable; **31 passed** with OPENTPW_GAME_PATH.
  Was 18 passed / 7 failed.
- **Ran from /tmp** (no content/ nearby): lobby in 3214 steps, 0 exceptions.
- **Ran against the UPPERCASED probe tree** (2488 symlinks, every segment capitalised): identical - 3214
  steps, advisor speaking with lip sync out of LIPS.WAD and speech out of SPEECHHD.SDT.
- **libdl A/B on one binary**, symlink moved aside: without the resolver, TypeInitializationException ->
  DllNotFoundException 'libdl'; with it, clean lobby.
- **Backend proven by pixels**: 4 settled frames (2 per build, camera frozen). Within-build mean |diff| 2.13
  and 3.09; across the change 1.27 and 1.33 - smaller than the noise. Interface region cross mean 0.07, max 5.
- **save/ byte-for-byte unchanged** after every single run.
- Resolver failure paths all exit 1 with their own message; the "not found" case names each place looked in
  5 lines, not 17.

### Gotchas hit this session

- The scratchpad holds an `enum.py` from earlier work; any python script run from that directory shadows the
  stdlib and dies importing Xlib. Run capture scripts from a subdirectory, with PYTHONSAFEPATH=1.
- A `grep -E` over a control run's output matched stack frames and nearly let me report the wrong failure -
  the branch point predates `--game`, so it failed at Game.cs:25, not in Vulkan. Control experiments must
  differ by ONE thing.
- The lobby seeds butterflies per run, so frames are never identical; compare within-build variation first.

### Left for Alexah to decide

- Two junk directories to delete only on their word: `bin/Debug/net8.0/C:\Program Files (x86)\...\Data` (left
  by the old tests) and the orphaned `.opentpw` in the repo root (the cache moved to LocalApplicationData).
- LICENSE.md still says "2022-2025 Alex Guthrie" while the repo carries 2026 work by a second author.

