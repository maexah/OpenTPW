# UI: on-screen advisor, loading screen, menu and options

What the original does for the four pieces of user-facing furniture OpenTPW has rebuilt: the animated advisor who rises into the lower right and talks, the way he is cut off mid-line (including the "ouch" he cries), the loading screen and its bar, and the Escape game menu with its Game Options screen. Addresses are from `/testme.exe` in headless Ghidra unless a row says otherwise. Three findings here reach much further than the screens they were found on and are called out where they sit: the morph quantisation box is **per track, not the mesh bounding box** (the same `.md2` format carries ride and guest animation); the sound-category length check needed a **100 ms floor** (park sound reads the same `.map`/`.sdt` pairs); and uniform blocks must be **per draw**, which is a renderer property that bites any new screen.

## The advisor on screen

He is placed in screen space in the lower right from the engine's own numbers, and he draws after the HUD. Head and arm node rotations, antennae/hand/eye morphs, `.lip`-driven mouth switching and the costume defaults (hats hidden, antennae and hands on) all run and are confirmed in the running game.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00598b20` | — | The gesture chain: clip 14, then a random clip 1-10 covering the sample plus 500 ms, then clip 15 | Ghidra; matched in game |
| clip 14 / clip 15 | — | Rise and drop. Position channel `0x1` raises him from below in 14 and drops him in 15; he is hidden once 15 finishes | Ghidra + animation data |
| channel `0x1` | — | Position keys. Documented in `AnimationFile`, not in the advisor's own files | Animation data |
| channel `0x20000` | — | Visibility keys — this is what blinks him. Also documented in `AnimationFile` | Animation data |
| 30 fps | `AnimationFile.FramesPerSecond` | Every `.md2` clip plays at 30 fps. Engine-confirmed, everywhere, not just here | Ghidra |
| `Sphere_Black2`, material flag `0x2` | — | His head and body are flat discs in a see-through material that writes no depth | Model data |

**Draw order.** Because of the see-through discs, the model draws in two passes: every solid half first, then every see-through half in file order. Drawing mesh by mesh let a hand behind him show through. Any future screen-drawn model with see-through parts needs the same two passes.

**Lighting** is tuned against Alexah's reference screenshots in `content/ReferenceScreenshots/` (untracked) by measuring glove and eye brightness in captures, never by eye. It uses the shader's per-draw ambient and world-normals uniforms.

**Engine gotcha:** creating entities inside an `OnUpdate` crashes, because `Entity.All` is being enumerated. Load models in constructors.

### Morph quantisation is per track

| Offset | What it is | Evidence |
|---|---|---|
| descriptor `+0x14` | Centre of that morph track's own quantisation box | `0x00470e90` decodes with it |
| descriptor `+0x20` | Step of that morph track's own quantisation box | `0x00470e90` decodes with it |
| `0x00470e90` | The engine's morph decode | Ghidra |

It is **not** the mesh's bounding box. Both the code and the format docs once claimed that, generalised from a single example, and the result was antennae at half height in clip 14. `AnimationFile.MorphTrack.DecodePosition` carries the game-wide numbers. Ride and guest animation use this same format, so the mistake is still live for anyone decoding morphs elsewhere.

### Animation rate: the lobby object ticks 10/s, not 25/s

Two different rates are in play and must stay apart. `.md2` clips play at 30 fps, engine-confirmed. The **lobby's per-tick data rates are 10/s**, derived below. A third system, the sky, genuinely runs at 25/s. They are deliberately separate per-system constants, not one shared constant, and that separation is the point of this section.

| Address / value | What it is | Evidence |
|---|---|---|
| `FUN_005d5c50` | The lobby object's per-frame update. Sets its delta field to `clamp(now_ms - last_ms, <=500) * _DAT_007029cc` | Ghidra |
| `_DAT_007029cc` (`0x007029cc`) = `0.01` | The multiplier — so one unit of that delta field is **100 ms**, i.e. 10 units a second | Ghidra, read first-hand |
| `FUN_005d9b50` | Flyer step, `pos += dir * speed * delta` — a consumer of that field | Ghidra |
| `FUN_005e1210` | Flyer turn and both camera eases — consumers of that field | Ghidra |
| `0x00702c7c` = `0.1` | Camera ease rate, one of the two | Ghidra |
| `0x00702ca4` = `0.2` | Camera ease rate, the other | Ghidra |
| `FUN_005e0470` state 0 | Orbit advance, `angle += delta * SPINSPEED` — a consumer of that field | Ghidra |
| `island[0x5c]` | Lightning roll, `(island[0x5c] & rand) == 1` | Ghidra |

Consequences of the 10, all read first-hand from the four consumers: butterflies are **15 u/s** and bats **25 u/s**, not 37.5 and 62.5 — flyers once ran 2.5x too fast and now carry the right figures. The camera's true continuous rates are **1.0/s** and **2.0/s**. `SpinSpeed` 0.2 rad/s is exactly SPINSPEED (0.02) x 10, faithful by luck.

**The trap that makes the distinction matter.** Lightning and the ambient one-shot roll are drawn once per *frame* and are **not** delta-scaled, yet both OpenTPW sites once converted themselves through a shared ticks-per-second constant. Editing that one constant would silently re-rate the storm and the ambience on reasoning that does not apply to them. Keep per-system rates per system.

### Timing, lip sync and the queue

His clips run on a `ClipSequence` timeline, his mouth changes on `Time.NextBeat`, and lip sync reads `Voice.Position` — the audio device clock, not the game clock.

Between queued lines he rests off screen for `CooldownSeconds` = **1.5 s**. This is a deliberate difference from the original, at Alexah's request ("at least 1-2 seconds"): `AdvisorQueue_Tick` (`0x005d5f80`) says the next line on the first free tick, so the original pops him straight back up. A flushed line skips the rest of the cooldown.

The lobby greeting is only samples **465** and **466**; **471** is the welcome-back line after Select New Player. Anything else heard in a session came from a test run.

**Known gap in the animation reader.** `AnimationFile.FirstFrame/LastFrame/IsValid` ignore position and visibility keys, so clips carrying only those channels read as having no span. Measured over the shipped data: **159 clips disagree** with the declared span, of which **114 read as no span whatever**. `DeclaredFirstFrame`/`DeclaredLastFrame` are the engine-matching span and are the ones to use.

## Interrupting the advisor

### Advisor_StopSpeaking — the cry

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x005994e0` | `Advisor_StopSpeaking` | Does nothing unless he is busy. Stops his voice, lifts the speech duck, kills UI particle channel 0, hides the model, frees the lip data, then maybe cries | Ghidra |
| `0x005d6070` | `AdvisorQueue_Clear` | One of the two reachers of StopSpeaking; the other is `AdvisorQueue_Add` with flush. `FrontEnd_ClosePlayerSlots` calls Clear whoever is picked | Ghidra |
| `0x0051c2c0` | `Sound_Stop` | A plain stop — no fade — for his voice | Ghidra |
| `0x00429d60` | `Advisor_KillModel` | Hides the model ("Kill advisor") | Ghidra |
| `0x00f79680` | — | The sound handle. **The cry plays only when this is non-zero** | Ghidra |
| effects 639/640/641 | `z_z_ouch1`, `z_z_ouch2`, `z_z_Ouch3` | The cry, chosen `rand()%3`, played through the same category and call as his lines. 0.37 s, 0.26 s and 0.37 s, no words | Bank entries |
| `0x00599880` | `Advisor_Update` | Starts the sample after the lead-in and clears the handle on the frame the sample finishes | Ghidra |

The handle is zero through the **800 ms lead-in**, because `Advisor_Update` starts the sample later, and zero again after the sample ends while he finishes his clips. So cutting a line **in its lead-in, or after his voice while he is still gesturing, is silent**; cutting it while he is actually speaking cries.

### Advisor_StopQuietly — the same teardown, no cry

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x005996d0` | `Advisor_StopQuietly` | Identical teardown with no cry. Its argument picks the fading stop over the plain one | Ghidra |
| `0x0051c300` | `Sound_StopFading` | Passes 60 to the sound's vtable `+0x30`. Taken as milliseconds — **unit untraced** | Ghidra |
| `0x004a3a30` | `OptionsScreen_Open` | Caller, with argument 1. The Game Options screen quietens him | Ghidra |
| `0x0055035a` | `Game_StateMachine` | Caller. **Not checked against OpenTPW** | Ghidra |
| `0x004a9380` | — | Caller (the postcard screen); also plays UI sound `0x95` and calls `ButtonGlintStop`. **Not checked against OpenTPW** | Ghidra |
| `0x005f0b40` | — | Caller: the park map screen, which loads UI tree `0x774da0`. OpenTPW's park map screen and park front end both route through the quiet stop | Ghidra |

So: **options and the park map take the quiet stop; a flush, a queue clear and leaving the front end take the crying one.**

Two more stop paths, listed for completeness: `FUN_0059aa70` (reached through `0x00486b40` and `0x0052f200`/`0x0052f580`) stops him when a matching in-park message goes away — **not traced further**.

### What OpenTPW does differently

`Advisor.StopSpeaking` mirrors the original, and both `Add(flush)` and `Hush` (= `AdvisorQueue_Clear`) go through it. The cry plays at `SpeechVolume` with `respectDelay` false, the same call as his lines. Deliberate differences:

- his voice fades over 10 ms (`CutOffSeconds`) instead of stopping dead, so it does not click;
- the duck is still held across a flushed line (a pre-existing difference);
- when cut off, OpenTPW drops a pending cue. The original leaves its cue timer (`0x00f79778`) set and adds the next line's start time to it. **Not checked further.**

Verified by disk capture at both cuts: the logged cry matched at 0.997 and uniquely; the greeting matched 1.000 up to the cut and is digital silence where it would have continued; the voice was gone within 10 ms of the cry; the next line started 0.836 s later; the cry's gain was 0.26 (= `SpeechVolume` x master).

### Pausing him

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004092a0` (ECX `0x786b68`) | pause helper | Does nothing unless "park running" is 1. Stops the game clock, holds his sample, moves the 3D listener away. Rename refused by the permission classifier, so still a `FUN_` name | Ghidra |
| `FUN_00409300` | resume | The matching resume. Also still a `FUN_` name for the same reason | Ghidra |
| `0x00786ba4` | `g_ParkRunning` (was `DAT_00786ba4`) | "Park running". Lobby sets it to 0 at startup (`0x0054e682`); a park load sets it to 1 (`0x0054ea4c`) | Ghidra |
| `0x00409350` | `Game_TogglePause` | — | Ghidra |
| `0x00402d90` | `GameClock_Pause` | Stopped by the pause helper | Ghidra |
| `0x00402db0` | `GameClock_Resume` | — | Ghidra |
| `0x00598960` / `0x00598990` | `Advisor_PauseVoice` / `Advisor_ResumeVoice` | Use flag `0x00f797c4`. A stop resumes first | Ghidra |
| `0x0051c1c0` | — | Moves the 3D listener away while paused | Ghidra |
| `0x005e184c` | — | The lobby arms a 90-second repeat of response `0x18a`/`0x18b`. **Not built in OpenTPW** | Ghidra |

His clips, lead-in and cue all use the game clock, so a pause carries them on without a jump. In a park, `GameMenu_Open`, `MessageBox_Open` and `OptionsScreen_Open` all call the pause. **In the lobby the original pauses nothing**: he talks on over the Escape menu, and the options screen only quietens the current line while the queue keeps ticking.

OpenTPW deviates here at Alexah's request ("The Advisor shouldn't show in the Options/pause menus"): he is paused in the lobby exactly as a park pauses him whenever a `GameMenu`, `MessageBox` or `OptionsScreen` is open; he is hidden while paused; the quiet stop still runs on options. `Advisor.Paused` runs his own clock, and `Voice.Pause`/`Resume` fade over 10 ms and keep held time out of `Position`. Verified by disk capture: sample 465 held at 1.45 s and resumed 3.66 s later from the same point, and nothing queued started over during options.

### Let the cry bleed through when leaving the lobby

**This is intended behaviour, approved explicitly. Do not "fix" it.**

The front end's end hushes him, as `g_FrontEnd`'s stop (`0x005e4140`) does through `AdvisorQueue_Clear`, so he cries if his voice was sounding. `Level.Unload` then fades every voice over 0.09 s (read from the 90 at `0x0051bcb0`; the unit is inferred). Measured in a disk capture of a reload: the cry starts ~80 ms after the request and the mix is at digital silence 100 ms later, so **about a tenth of a second of "ouch" is audible before the lobby goes**.

Asked whether to keep it or make the teardown silent, Alexah said: "Let the advisor cry bleed through when leaving the lobby, that's fine." A clipped "ouch" during a park load looks exactly like a bug, which is why this is written down.

### The SoundCategoryFile length-check bug

Found on the way to the cry, and it reaches well past the advisor: **park sound work reads the same `.map`/`.sdt` pairs.**

`SoundCategoryFile.TryReadSample` checked the stream length against the map's length with a tolerance of `max(60 ms, 6%)`. Effect 639's record (map 288 ms, stream 366 ms) failed that check, so **the next record silently stood in for it**: 639 played `ouch2`, 640 played `Ouch3` and 641 played nothing at all, with no error reported anywhere.

Across all **31 shipped categories** the stream runs **26-83 ms longer than the map**, so the floor is now **100 ms** (`LengthSlackMilliseconds`). The fix was validated by dumping every category from an unfixed tree and from the fixed tree and diffing: **only those three effects moved.** A silent substitution like this is invisible from code alone — dump and compare, do not reason about it.

## Loading screen

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00587380` | `LoadingScreen_Begin` | Called with (language, `"data\\init"`, steps). **Every call site passes 500 steps** | Ghidra |
| `0x0054dcf0` | — | Boot init — a `Begin` call site | Ghidra |
| `0x0054e360` | main state machine | States 1, 3 and 9 are the other `Begin` call sites | Ghidra |
| `0x005879c0` | `LoadingScreen_End` | Holds the first screen until 3 s after it appeared | Ghidra |
| `0x00587c80` | `LoadingScreen_Step` | Runs only while the screen is up | Ghidra |
| `0x00587db0` | texture-set loader | Calls `Step` | Ghidra |
| `0x00579e00` | mesh loader | Calls `Step` | Ghidra |
| `0x00587a70` | bar draw | Geometry at 640x480: x 14, y 446, height 11, length 318. Clamps at 100% | Ghidra |
| `0x800000` | — | Dark red. Bands: dark red for the top fifth, bright red to four fifths, dark red to the bottom | Ghidra |

**Which screen shows when.**

- **Picture:** `Data\Init\<screen width>\welcome.tga`, from 400, 512, 640, 800 or 1024, else 640. Stretched over the screen.
- **First call ever:** `splash_<lang>.tga` (the Bullfrog logo) for at least 2.5 s.
- **First screen:** pastes `legal_<lang>.tga` over the picture's bottom, draws **no bar**, and `End` holds it until 3 s after it appeared.
- **Later screens:** paste `welcome_<lang>.tga` at y = 224/480 (the American data has none) and **do** draw the bar.
- The whole boot sequence — movies, lobby, park loads — is in `boot.md`.

**OpenTPW** shows only the later, bar kind. It keeps the picture 4:3 and pillarboxed, choosing the narrowest folder at least as wide as it is drawn, and writes the last log line under the bar. Loading is synchronous, so frames are drawn from steps and log lines, at most 30 a second, plus one forced frame on close. The percentage is worked out in **integers**: the original's float `steps * (100f / expected)` comes out a hair under 100 for some counts, so its bar never quite filled.

**Not implemented:** the splash, the legal screen, the movies, the `welcome_<lang>` overlay.

### A step is exactly one Asset.Register

A step is one `Asset.Register` — Texture, Shader, Material, Model. Nothing else is a step. Audio clips are not steps, so adding sound adds no steps.

**Work that takes real time but registers nothing must NOT be papered over by calling `Step()`.** Doing that makes the bar's *rate* a lie rather than its *length*, which is the worse of the two failures. Report the gap instead. The known example: the per-park sound preload occupies about half a second of an ~8 s load (5.93 s to 6.39 s) while registering no assets, so the bar holds still there even though its total is right. The status line under it still moves, because a log line pumps a frame too.

### The seeds are deliberately unmaintained

The bar **learns**: each situation — the scene, plus whether it has been built before in this run — keeps the count it last measured, in `save\opentpw.cfg`. That file is the truth.

The constants in the code are only seeds for a first-ever run. **Do not re-measure them, and do not "fix" them in a commit.** This reverses an earlier standing instruction, and other notes may still invite the old chore; they are stale. Any step figure written in prose anywhere should be read as dead.

### Measured costs

- The first frame takes ~450 ms, almost all of it the UI shader compile, which used to land on the lobby's first frame instead.
- After that, ~0.85 ms a frame: ~90 ms over a 6 s load.
- Turning vsync off during the load made no difference, so it was taken back out.

## Escape game menu

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0048c830` | `GameMenu_Open` | Opens the menu | Ghidra |
| `0x0048c600` | — | The lobby's menu build | Ghidra |
| `0x005e41c0` | `IslandLobby_OnKey` | On Escape's release asks each active child's `+0x18`, and opens the menu only if none answered - the island camera's cancel is `lobby.md`, "Escape cancels the fly-in" | Ghidra |
| `0x0048bd40` | — | The menu's own key handler; closes on Escape | Ghidra |
| msg `0x11` | — | Show. Sets the resting colour (0, 175, 190) | Ghidra |
| — | `MenuChoice_TickColour` | Hover ramp: grey 33, +32 per tick, up to white. The tick is per frame in the original; **taken as 30/s here — unproven** | Ghidra |
| vtable `+0x24` | — | The front end's "is someone playing", which gates showing Select New Player. The reading is an interpretation | Ghidra |
| cat_ui effect 193 | — | The click. Source measures -12.4 dBFS RMS / -0.2 peak by disk capture — much louder than BUTTON01 — so gain 0.132 puts it at -30 like the other clicks | Disk capture |

It is built in code, not from a layout tree: a LOLIGHT full-screen control plus `MenuList_AddItem` items, font 0 with the purple skin, centred, first item at y = 5 and each next at y + h + 5, where `h = (line height + 5) * 0x600 / screen height`.

Go Online is a dead end — it only closes. Escape over a message box goes to the box: `UI_LoadModalTree` gives it the focus (`0x0047ee67`) and a key goes to the focus alone (`0x006698e6`), so it never reaches `IslandLobby_OnKey` (`lobby.md`, "Escape cancels the fly-in"). Over the options screen no key reaches the lobby: the screen hides the lobby's root (`0x004a3ae0`), which keeps the focus, so a key goes to the last control pressed, which drops it (`lobby.md`, "The lobby's keys act on the release").

The scratch `sdt.py`/`levels.py` tools are broken (`sdt.py` was overwritten), which is why the click level was measured by capture rather than from the file.

## Game Options

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x004a3a30` | `OptionsScreen_Open` | Opens the screen; also quietens the advisor | Ghidra |
| `752f30` | — | The screen's UI tree | Ghidra |
| `0x004a2bf0` | — | The screen's callback | Ghidra |
| `-1` / `b_okay` | — | OK | Layout stream |
| `-2` / `b_exit` | — | Cancel | Layout stream |
| `0x0078d8d8` | GameOptions object | The live options object | Ghidra |
| `0x00423690` | `GameOptions_Construct` | Where the defaults come from | Ghidra |
| — | `Sound_ApplyGroupVolumes` | Speech volume 0 disables the duck — **proven** here | Ghidra |
| `0x1d4c8` | — | The rendering row's control | Layout stream |
| `0x1d4d2` | — | That row's `b_on2` arrow | Layout stream |
| `0x1d4d5` | — | The resolution slider | Layout stream |

**Meshes are found by node-name hash:** `b_on` (switch, down = off), `b_on2` (arrows), `f_optpanel`/`f_optpanel2`/`f_optpanel3` (rows), `b_scroller` (thumb), `f_screen` (backdrop), `!f_plain` (frame).

Volume and quality defaults come from `sound.sam` `DefaultVolume.*` and `SoundInfo.*`, by name. Everything applies live; cancel restores the snapshot. Options persist as the original's do: machine options in `save\Config.tcf` on the tick, player options in the player's `gms.dat` when the player is saved (an early guess of `dialog.tcf` was wrong) — see `saves.md`.

Deliberate differences: cancel re-applies volumes (the original does not); OK's RESTART GAME and audio-quality checks are skipped.

**Two rows are no longer the original's**, at Alexah's word:

- the **rendering row** is now "Display: Windowed / Full screen / Borderless full screen". The original has no such row and no UITEXT for one, so its three words are OpenTPW's own. Taking over an existing row rather than adding one keeps the layout the compiled stream's. **GPU vs Software is no longer reachable**, though `CardRendering` is still read, written back, and still caps graphics quality.
- the **resolution slider** steps the display's real modes — its `Maximum` is the mode count, not 100 — and only decides anything in exclusive full screen. It is switched off in the other two, where it reports what is in use. A switched-off slider had to refuse the wheel as well as the hit test, because the thumb takes the pointer like any button.

Both apply on the tick and only when they actually changed, so no RESTART GAME box is needed. What OpenTPW is really set to lives in `save\opentpw.cfg`; `Config.tcf` keeps the nearest of the original's three, so it stays a file the original can read.

Earlier rulings that still stand: the settings screen is 1:1 for now, and video card is dead. GPU/software and resolution were dead by the same ruling but have since become real, as above.

### Uniform blocks must be per draw

**A renderer property, not an options-screen one. Any new screen that writes many materials a frame will hit this.**

`Material.SetInFrame` wrote one shared uniform buffer through the command list between draws. With the options screen's ~26 writes a frame, a draw now and then used the *previous* draw's value — a panel landed on the row above, hiding its text and thumb — on about a third of frames. `Device.WaitForIdle` did not help. The fix is a uniform block per draw (two rounds, used on alternate frames); 34 of 34 frames were stable afterwards. The player slots and the name dialog never showed the fault, because they write few materials.

### What was verified, and what was not

Verified with a silent XTEST harness at 1280x720 (the options screen's virtual x maps as `160 + x * 720/1536`): menu open and close over the slots and over the panel, and blocked by a dialog; the hover ramp; options open quietening the advisor; the switch; the rendering cap on quality; drag, wheel and page click; the volume-off text; cancel reverting and keep keeping; popup help off killing the help bar and the glints; the quit box over the menu; Select New Player returning to the slots with the player kept and the advisor's welcome-back line (471). By disk capture: a muted lobby is digital silence apart from the 0.30 s menu click; effects off with the music slider at 0 gives 100% exact zero samples; cancel brings sound back. Frame time with the options screen open was 6.95 ms at 144 Hz vsync, the same as without it.

**Not verified / unproven / untraced**, collected:

- the percent-to-loudness curve, and the volume mapping generally — percent / default as a linear multiply is an assumption;
- anything against the original actually running: no reference screenshots of either screen exist locally;
- the menu's hover tick rate taken as 30/s;
- the 60 passed by `Sound_StopFading` taken as milliseconds;
- the `0.09 s` voice fade inferred from the 90 at `0x0051bcb0`;
- `Game_StateMachine` `0x0055035a` and the postcard caller `0x004a9380` as quiet-stop callers, neither checked against OpenTPW;
- the original's cue-timer behaviour after a cut (`0x00f79778`);
- `FUN_0059aa70`, not traced further;
- the lobby's 90-second repeat of response `0x18a`/`0x18b`, not built.
