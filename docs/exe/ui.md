# UI: on-screen advisor, loading screen, menu and options

What the original does for the four pieces of user-facing furniture OpenTPW has rebuilt: the animated advisor who rises into the lower right and talks, the way he is cut off mid-line (including the "ouch" he cries), the loading screen and its bar, and the Escape game menu with its Game Options screen. Addresses are from `/testme.exe` in headless Ghidra unless a row says otherwise. Three findings here reach much further than the screens they were found on and are called out where they sit: the morph quantisation box is **per track, not the mesh bounding box** (the same `.md2` format carries ride and guest animation); the sound-category length check needed a **100 ms floor** (park sound reads the same `.map`/`.sdt` pairs); and uniform blocks must be **per draw**, which is a renderer property that bites any new screen.

## The advisor on screen

He is placed in screen space in the lower right from the engine's own numbers, and he draws after the HUD. Head and arm node rotations, antennae/hand/eye morphs, `.lip`-driven mouth switching and the costume defaults (hats hidden, antennae and hands on) all run and are confirmed in the running game.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00598b20` | — | The gesture chain: clip 14, then a random clip 1-10 covering the sample plus 500 ms, then clip 15 | Ghidra; matched in game |
| `0x00790398` | — | His record array, stride `0xb0`, the model handle at `+4` (`0x0079039c`). `FUN_00429ba0` builds record N: loads `Advisor.md2` (or `Advisor_%s.md2`), runs the channel reset `FUN_00473e30( model, 1, 1, 1 )`, sets `model+4 \|= 4` and `model+200` = record `+8`. Every reference to `0x0079039c` is in `0x00429ba0`..`0x00429ff3` | Ghidra, Q194 |
| `FUN_00429e90` | — | Plays the chain's **first** clip: logs "Initial advisor call", then `FUN_004732a0( model, role 5, clip − 1, flags 0x10, 1.0, channel 0 )`, or returns 1000 with no model. Its one caller, `Advisor_SayResponse` (`0x005993d5`), passes the clip word at `0x00f79718` masked by `0xff80ffff`. `Advisor_Update` plays each later clip itself (`0x005999e3`..`0x00599a85`): when channel 0 has nothing queued (`FUN_00473ff0` answers `0xc`) it calls the trigger with flags 0, so each clip queues behind the running one by the trigger's ordinary rule (`park.md`); a masked word of 0 ends the list. What flag `0x10` means is not decoded | Ghidra, Q194 |
| clip 14 / clip 15 | — | Rise and drop. Position channel `0x1` raises him from below in 14 and drops him in 15; he is hidden once 15 finishes | Ghidra + animation data |
| channel `0x1` | — | Position keys. Documented in `AnimationFile`, not in the advisor's own files | Animation data |
| channel `0x20000` | — | Visibility keys — this is what blinks him. Also documented in `AnimationFile` | Animation data |
| 30 fps | `AnimationFile.FramesPerSecond` | Every `.md2` clip plays at 30 fps. Engine-confirmed, everywhere, not just here | Ghidra |
| `Sphere_Black2`, material flag `0x2` | — | His head and body are flat discs in a see-through material that writes no depth | Model data |

**How he reaches the screen** (Q556). `FUN_00429ba0` marks his model's data `+0x30 |= 0x200` (`FUN_0045bed0`), the
flag that sends his renderable down the screen-space arm of its draw (`ModelRenderable_Draw`, `0x0057a750`: his own two
matrices, `[0x008bb624] |= 0x2000000`). A clip started on him (`FUN_004732a0`, `0x004732dd`) puts that renderable in
render list 1 of the manager `[0x0087a6d0]` (`Model_ShowInRenderList`, `0x0045bd30`, with 1: `RenderList_Add( 0x10 )`,
node word `|= 0x11`); `FUN_0045bdd0` takes it out (`Advisor_KillModel` calls it), and in the End it was out a tick
after each line (839 after 838's end); which caller does that was not traced. The park has one view, made by
`FUN_00576310` at the manager's `+0x48` with flags `0x1f31`; `Render_DrawView` (`0x00576a00`) draws lists 1 to 6 under
its `0x20`, and neither his draw nor the lists' walk reads `gui_CameraFlags` (`Render_DrawView`'s one read,
`0x00576a9f`, `& 0x16`, only picks the ground's cell set), so the ride view (the End's, `0x0b04`) draws him as the
orbit does. His draw stamps the renderable's `+0x90`/`+0x94` with the frame counter `[0x008bcc40]` (`FUN_00583790`)
and the views drawn in it `[0x008bcc44]`; `original/advdraw.py` reads that. Measured in the End
(`q556/PREDICTION.txt` rounds 1 to 3): the view `0x1f31` throughout; each line's slot in list 1 and his draw entered
at every poll of it (`advdraw.py` polls every 0.05 s and takes a stamp up to two frames old; the stamp is written before
the draw's return on the renderable's flag `0x4`, `0x0057a85f`, so it says the draw was entered, not that he was
drawn), and seen in the film (`q556/orig/c-film-21.0`, `24.0`, `29.0`).

**His projection is affine** (Q597). The screen-space arm hands `ModelRenderable_DrawNodes` (`0x0057a530`) the
renderable's two matrices, A at `+0x10` and B at `+0x50`; each node's vertices go through its chain of local matrices
times A in the plain 4-wide transform (`XformVerts_Fpu`, `0x00579250`, no divide), and B's chain goes to the lights
(`Light_ModelSetup`). Where the park's arm uses the draw matrix `[0x0087b088]` (the camera inverted, the projection, the
`0x0087b0d0` scale: `park-engine.md`), A is the advisor record's `+8` matrix (`0x00429ba0`: identity, translation 0.6,
-0.6, 0.2) times diag(1, -1, 1, 1), written by `Renderable_SetMatrix` (`0x0057a260`, mode 1, its only mode in use) from
`Model_ShowInRenderList`. Every matrix in it is affine, so w is 1 and `ClipToScreen_Fpu`'s divide by w (`0x00579690`)
changes nothing: how far toward the viewer a node stands changes no size, as in `AdvisorModel.ScreenProjection`. His
depth, about 0.2 (the root's 0.001 a unit about it), is written in `ClipToScreen` mode 2, not the "ui" range's: the draw entry's
flag below picks the mode (`(entry+8 >> 24) & 2`, `0x00581065..0x00581092`), and the mode 2 arm (`0x00579733..0x0057979a`) stores
z x `[0x008bcc20]` (0.1, written once at `0x00582ddf`) with no range test; the `[0x008bcc28]` (0.311) range test is mode 0's arm
(fork watch 2026-10-21, verify-audio-ui M2). The
flag `[0x008bb624] |= 0x2000000` rides into each mesh's draw entry (`DrawList_AddMesh`, `0x00581cd0`), where
`DrawList_Flush` (`0x00581e9b`) files the entry in its batch's unordered list. Read in the running original (`q707/p597/PREDICTION.txt`,
`original/advnodes.py`): A = 1 0 0 0 | 0 -1 0 0 | 0 0 1 0 | 0.6 0.6 0.2 1 and B the identity throughout, every node's
w column (0, 0, 0, 1), the root's axes 0.0112 (`advnodes.py` prints four places), 0.001, 0.015.

**How he is lit** (Q621). Per vertex, by the park's own model lighter ("The lighting model", `park-engine.md`), and (if
his draws do not carry the `MODULATE2X` bit, below) not past his textures. Each advisor record
(`0x00790398 + slot × 0xb0`) holds a light at `+0x48`, made by the array's constructor `AdvisorRecord_Construct`
(`0x00429a60`) as type 2 (directional) with colour (1, 1, 1, 1); `Advisor_LoadModel` (`0x00429ba0`) sets its world
direction (`+0x2c`) to (26.667, −26.667, 800), the direction light travels, and files it in the manager `[0x0087a6d0]`'s
list 1 (`RenderList_Add( light, 0x10 )`, `0x00429d15`), where his own renderable goes. His draw carries `0x2000000`, so
`Light_ModelSetup` (`0x0057aa10`) chains the lights of the manager's `+0x30` and the model's own (`FUN_005744d0`) but
skips `+0x2c`, list 7, where the sun is filed (`Light_Create`, `0x00457e70`, case 2, `RenderList_Add( 0x70 )`): **the sun
does not light him.** `Light_SumAtVertex` (`0x005741b0`) starts from the renderer ambient (`0x00879ddc..e4`, in a park the
theme's `AmbientLightLevel` set by `FUN_00458590`, (0.333, 0.333, 0.408) in Lost Kingdom), adds each directional light's
colour × max(0, −(d·n)), and `Light_ModelVertexColours` (`0x00574660`) clamps each channel to a byte. The vertex colour
then multiplies the texel, but by how much is not settled. Stage 0's `COLOROP` is `MODULATE` (4) at `0x00567e59` and
`0x00568625`, `MODULATE4X` (6) at `0x00567dd8` and, on stage 1, `ADDSIGNED` (8) at `0x00568925` (the 13 constant writes of
`SetTextureStageState`). It is also set by the lazy setter `FUN_00567a70`, which `FUN_00567620` (applying the compiled
state word) calls at `0x005678ba`..`0x005678ca` for stage 0: **`MODULATE2X` (5) when the state word has bit `0x10000`**,
`MODULATE` otherwise. The state-word table (`render-states.md`) does not list that bit; the texture-flag table has
source-material bit `0x10` setting texture flag `0x10000` (`0x00575ea1`), and whether that reaches the state word of the
advisor's draws was not traced. So "a lit face is its texel at most" is **not established**: the frames do not rule 2X out
(the gloves' darkest tenth, (144, 145, 152), sits well above what `MODULATE` would give an ambient-only vertex on a texel
of 231, about (77, 77, 94), though the texture's creases make that rough).

**Measured in the running original** (`q621/PREDICTION.txt`, `q621/orig/`, `original/advlight.py`: the stock park,
the buy screen's own line, then `sayas.sh 18,44,41`, read while each line played; two rounds). The ambient read
(0.333, 0.333, 0.408), each record's light type 2, colour 1, direction (26.667, −26.667, 800). Over every lit mesh
of both models, red equals green on every normal and blue less red is exactly 19 wherever blue is under 255: the
ambient and a white light, nothing else (the sun's blue 0.847 would break it). The least lit read (85, 85, 104), the
ambient alone. Fitted mesh by mesh in its own normal space, the light's strength is **2.00**, not 1 (|v| 1.990 to
2.008 over 15 meshes in round 2 (14 in round 1), every normal then within 1 of `clamp( ambient + 2 × max(0, −(d·n)) )`, 0 channels
off of 1,401 in a read; the theme fly's right hand alone does not fit, 4 unsaturated normals of 82, not settled).
Both records' lights are the same, so both reaching him would make the 2. That the record's light lights him is
inferred, not traced: `FUN_00576410` files a light at `[mgr + 0x10 + 4 × (flags >> 4)]` (`0x00576522`..`0x0057652c`), so
`0x10` goes to list 1 (`+0x14`, `0x00429d12`), and `Light_ModelSetup` chains `[mgr+0x30]` (`0x0057aa8e`), `[mgr+0x2c]`
only without `0x3000000` (`0x0057aa96`..`0x0057aab6`) and `FUN_005744d0` (`0x0057aad0`), never `+0x14`; the chain that
hands list 1's lights to it was not traced, nor was the sign of the fitted directions (the hats' fits and the record's
−d differ in y). A face therefore reaches its texel at a third of full facing: his head and body
discs, eyes and mouths read (255, 255, 255) throughout; his hands' normals averaged 153 to 172, the hard hat's 153 to
156, the research hat's 158, the pith helmet's 144.

**In his frames** (`q621/parts.py`, `parts.out`: each part's pixels of its colour class, the original's 1024 × 768
frames against this game's at 1280 × 720, the same four lines; `q621/orig/r1-four.png`, `q621/ours-four.png`):

| Part | Texel (`wctpng.py`) | The original (median; 95th percentile) | OpenTPW (main's light) |
|---|---|---|---|
| Gloves, `White.wct` | 231 | 239; 239, darkest tenth (144, 145, 152) | 255 on 95 per cent, darkest tenth 250 |
| Hard hat, `Hardhat_yellow` | (227, 219, 60) | (229, 219, 49); (239, 237, 105) | (255, 255, 95); (255, 255, 128) |
| Scientist's hair, `Research_Hair` | 234 | 174; 214, darkest tenth 92 | 249, 255 on 46 per cent |
| Pith helmet, `Pith_Side` | (194, 154, 76) | (151, 116, 49); (196, 146, 67) | (255, 232, 108); (255, 255, 130) |
| Its band | grey | (117, 115, 117); (177, 176, 171) | 255 on 97 per cent |

The original's 239 for a texel of 231 is not the light (his head's normals read 255, so a lit texel is the texel); it
fits the texture depth, four bits a channel making it 14 (238) and a 16-bit frame's five bits 29, shown 239, though
the frame's depth (`GraphicalOptions.RENDER32`, `boot.md`) was not read. **OpenTPW lights him past his textures**: `AdvisorModel.Draw` hands the
shader's old per-pixel arm an ambient of 1.4 and a light of 0.3 at a point, unclamped, so every texel is 1.4 to 1.7
times itself, cut at 255: the gloves and hair white and flat, the hard hat lemon, the helmet pale yellow with a white
band. Not measured: the lobby, where the front end calls `Advisor_LoadModel` once (`0x0054e5ec`) and other code writes
the ambient (`FUN_005d8b50`, `FUN_005d9230`), so the strength and the ambient may differ there; whether the original's
normals at `+0x64` are the ones this game computes (`models.md`, "Normals"). Building it is Q657.

**The big ticket is a scale track** (Q597, Q599). Lost Kingdom's `Advisor_JUNm18` keys `Ticket2` (node 12, hung from
`Bug Head`) from 0.748, 0.964, 1.158 at frame 0 to 3.273, 4.214, 5.063 at 38 and 12.093, 11.486, 8.958 at 70; `m19` keys
12.093, 11.486, 8.958 at 0 and 39; `m20` from that to 3.023, 2.872, 2.239 at 25 (`Wave01`, the ticket's glint ring, 0.249
to 0.488 in 18). `Anim_PoseTrack` tests `0x80` (`0x00471d8d`), finds the keys with `Anim_FindKey` type 4 (`0x00471db5`,
16-byte keys; nought before the first key, and then no scale is set: FileFormats `models.md`, "Scale"), lerps the two keys about the frame
(`0x00471ddd`..`0x00471e2a`, the frame's fraction included) and sets the lengths of rows 0, 1 and 2 of the node's matrix to
them (`Matrix_SetAxisLengths`, `0x0046f910`), after the turn and before the place. In the original the ticket's axes rise
through clip 18, read 12.0933, 11.486, 8.9577 at its place -38.226, 0, 42.049 at clip 18's end and through clip 19
(`q707/p597/orig/O/load.log` line 279), and fall in clip 20 to exactly 3.0233, 2.8715, 2.2394. On screen the ticket of clip
19 spans 0.738 of the frame's height across, its middle 0.436 of the height left of his anchor (`g-15.png`, `ticketbox.py`);
the one in his hand about 0.05. Clip 17 squashes his body (1.243, 0.838, 0.838, to 1 at frame 9 and back to 1.2112, 0.8592, 0.8592 at 19) and his head the
other way (1.1018, 0.979, 0.9407 to 0.9002, 1.0997, 1.0997 at 9), clip 18 leaves the head at 0.9947, 1.0427, 1.0249, and clip
16 keys the map. **OpenTPW poses them** (Q599): `AnimationFile.ScaleTracks`, `AdvisorModel.PoseNodes`; the same node lengths and
place at clip 18's end, and the ticket 0.746 of the height across, 0.435 left of his anchor (`q707/p599`, its last run). Every other
model's scale tracks are counted, `ANIM_SCALE_TRACK` (Q598).

**On a screen that is not 4:3 the original stretches him, ticket and all** (Q639). His root's three axis lengths are
constants, 0.01125, 0.001, 0.015, written at `0x00429d1c`..`0x00429d2c` and set by `Advisor_LoadModel`'s call at `0x00429d46` to `FUN_0046fa70`, and his
place is the constant 0.6, -0.6 (`0x00429bcf`, `0x00429bd6`): no screen size enters either, so on any mode he stands 0.8 of
the way across and his x unit is the 4:3 one stretched with the screen, as its interface is (`0x0048f47b`, `0x0048f4a6`).
Read in the running original at its one 5:4 mode, 1280 x 1024 (Res 5; `[0x00faa5c4]` 1280, `[0x00faa5c0]` 1024): the
full-grown ticket of clip 19 spans 251..960, 0.692 of the height across (0.738 x 0.9375) and squeezed, its middle 0.473 of
the width, 0.034 of the height left of the middle (`q639/orig/O1280/f-031.png`, `ticketbox.py`). `Wave01` hangs from node 24,
not from `Ticket2`, which nothing hangs from. **OpenTPW keeps his shape and pins him right** (`AdvisorModel.ScreenProjection`, for the lobby's player
slots), **and draws `Ticket2` toward the middle's pin by its growth** (`AdvisorModel.TicketMove`: its x axis from 1, its rest, to
12.093, the longest key, 0 to 1; the move is that share of the middle's offset less the right's, across and down), a said
deviation; the glints ease between thirds' pins with it (`scenes.md`, "Gesture table"). Measured (`q639/`, the ticket line
of q707's scene): the full ticket's middle 0.028 of the height left of the window's middle at 2552 x 1355 and 1280 x 720,
0.034 at 1024 x 768, where before it stood 0.25 and 0.19 right at the wide sizes; the log's `the ticket 1.000 grown, drawn
-373 px across` at 2552 x 1355.

**Draw order.** Because of the see-through discs, the model draws in two passes: every solid half first, then every see-through half in file order. Drawn mesh by mesh, a hand behind him shows through. Any future screen-drawn model with see-through parts needs the same two passes.

**Lighting** is tuned against Alexah's reference screenshots in `content/ReferenceScreenshots/` (untracked) by measuring glove and eye brightness in captures, never by eye. It uses the shader's per-draw ambient and world-normals uniforms.

**Engine gotcha:** creating entities inside an `OnUpdate` crashes, because `Entity.All` is being enumerated. Load models in constructors.

### Morph quantisation is per track

| Offset | What it is | Evidence |
|---|---|---|
| descriptor `+0x14` | Centre of that morph track's own quantisation box | `0x00470e90` decodes with it |
| descriptor `+0x20` | Step of that morph track's own quantisation box | `0x00470e90` decodes with it |
| `0x00470e90` | The engine's morph decode | Ghidra |

It is **not** the mesh's bounding box. Read in the mesh's box instead, his antennae in clip 14 come out at a little over half their height. `AnimationFile.MorphTrack.DecodePosition` carries the game-wide numbers. Ride and guest animation use this same format, so the mistake is still live for anyone decoding morphs elsewhere.

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

Consequences of the 10, all read first-hand from the four consumers: butterflies are **15 u/s** and bats **25 u/s**, not 37.5 and 62.5 — OpenTPW's flyers move at those rates (`LobbyScript.FlyingMesh.SpeedPerSecond`). The camera's true continuous rates are **1.0/s** and **2.0/s**. `SpinSpeed` 0.2 rad/s is exactly SPINSPEED (0.02) x 10, faithful by luck.

**The trap that makes the distinction matter.** Lightning and the ambient one-shot roll are drawn once per *frame* and are **not** delta-scaled, so OpenTPW converts both through `LobbyScript.AssumedFrameRate` (25), kept apart from the lobby's data rate `LobbyScript.TicksPerSecond` (10): one shared constant would silently re-rate the storm and the ambience on reasoning that does not apply to them. Keep per-system rates per system.

### Timing, lip sync and the queue

His clips run on a `ClipSequence` timeline, his mouth changes on `Time.NextBeat`, and lip sync reads `Voice.Position` — the audio device clock, not the game clock.

Between queued lines he rests off screen for `CooldownSeconds` = **1.5 s**. This is a deliberate difference from the original, at Alexah's request ("at least 1-2 seconds"): `AdvisorQueue_Tick` (`0x005d5f80`) says the next line on the first free tick, so the original pops him straight back up. A flushed line skips the rest of the cooldown.

The lobby greeting is samples **465** and **466** while no slot holds a player, and **471**, the welcome back, while one does, whether the slots open with the lobby or after Select New Player (`FrontEnd_ShowPlayerSlots`, `0x004a6580`). Any other greeting heard in a session came from a test run.

### The lobby's idle repeat

The island lobby's camera update (`0x005e1830`, slot `+0x08` of the derived vtable `0x00702ec0`) runs the base update
`0x005e0470` and then, **on every pass while a player is picked** (`g_Players+0x60` not -1), draws `rand() & 1`
(`0x0067b5c0`, at `0x005e184c`) and arms the front end's advisor queue (`[0x00f85614]+0x14`) with response `0x18b` or
`0x18a` and a period of 90,000 (`FUN_005d61a0`, which only writes `+0x18` = 1, `+0x1c` = the period and `+0x24` = the
response). Response 394 is sample 469, 395 sample 470. `AdvisorQueue_Tick` (`0x005d5f80`) sets `+0x28` while he speaks
(`[0x00f7966c]`, which `Advisor_SayResponse` sets and the line's end or a stop clears) and when it starts a queued line;
on a free tick with the queue armed (`0x005d5feb`) it reads the sound clock `0x005f5fa0` (wall time in milliseconds,
`audio.md`), and restamps `+0x20` if `+0x28` was set, clearing it, or else, once the clock passes the stamp plus the
period (unsigned, `0x005d6016`), restamps and queues the response. So the line comes 90 s of wall time after he last fell
silent, and again 90 s after each time it is said. Read from the disassembly, not heard in the running original.
**OpenTPW** builds none of it (`docs/QUEUE.md` Q77) and counts the arm as `LOBBY_ADVISOR_IDLE_REPEAT`, in
`LobbyCameraMode.Update`, every frame a player is picked.

**Known gap in the animation reader.** `AnimationFile.FirstFrame/LastFrame/IsValid` ignore position and visibility keys, so clips carrying only those channels read as having no span. Measured over the 1,278 clips in the game that carry an animation block: **164 disagree** with the declared span, and the **127** whose keys fall short all read as no span. `DeclaredFirstFrame`/`DeclaredLastFrame` are the engine-matching span and are the ones to use.

## Interrupting the advisor

### Advisor_StopSpeaking — the cry

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x005994e0` | `Advisor_StopSpeaking` | Does nothing unless he is busy. Stops his voice, lifts the speech duck, kills UI particle channel 0, hides the model, frees the lip data, then maybe cries | Ghidra |
| `0x005d6060` | `AdvisorQueue_Clear` | One of the three callers of StopSpeaking; the others are `AdvisorQueue_Add` with flush and `FUN_0059aa70` (below). `FrontEnd_ClosePlayerSlots` calls Clear whoever is picked | Ghidra |
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
| `0x0051c300` | `Sound_StopFading` | Posts the stop to the library, which at sound detail above `SoundInfo.FADETOGGLE` (30) marks the voice fading with 60 (vtable `+0x30`, `0x006b7130`) and each service pass then keeps 60 % of its volume; at 30 or below it is the plain stop. Not milliseconds: `audio.md`, "The fade" (fork watch 2026-10-18, `verify-other-a.md` H1) | Ghidra, disassembly |
| `0x004a3a30` | `OptionsScreen_Open` | Caller, with argument 1. The Game Options screen quietens him | Ghidra |
| `0x0055035a` | `Game_StateMachine` | Caller. **Not checked against OpenTPW** | Ghidra |
| `0x004a9380` | — | Caller (the postcard screen); also plays UI sound `0x95` and calls `ButtonGlintStop`. **Not checked against OpenTPW** | Ghidra |
| `0x005f0b40` | — | Caller: the park map screen, which loads UI tree `0x774da0`. OpenTPW's park map screen takes the quiet stop (`ParkMapScreen`), as its options screen does; a park's front end ending takes the crying one (`ParkFrontEnd.OnDelete` -> `Advisor.Hush`) | Ghidra |

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
| `FUN_004092a0` (ECX `0x786b68`) | pause helper | Does nothing unless the pause gate `0x00786ba4` is 1. Stops the game clock, holds his sample, moves the 3D listener away. Rename refused by the permission classifier, so still a `FUN_` name | Ghidra |
| `FUN_00409300` | resume | The matching resume. Also still a `FUN_` name for the same reason | Ghidra |
| `0x00786ba4` | `g_ParkRunning` (Ghidra's label for `Game+0x3c`) | The pause-permission gate, not "a park is running" (`weather.md`, "Pause, and why the calendar cannot roll while paused"): 1 from boot (`0x00407bd9`), 0 on the lobby load (`0x0054e682`), 1 on an ordinary park load (`0x0054ea4c`) | Ghidra |
| `0x00409350` | `Game_TogglePause` | — | Ghidra |
| `0x00402d90` | `GameClock_Pause` | Stopped by the pause helper | Ghidra |
| `0x00402db0` | `GameClock_Resume` | — | Ghidra |
| `0x00598960` / `0x00598990` | `Advisor_PauseVoice` / `Advisor_ResumeVoice` | Use flag `0x00f797c4`. A stop resumes first | Ghidra |
| `0x0051c1c0` | — | Moves the 3D listener away while paused | Ghidra |
| `0x005e184c` | — | The lobby arms a 90-second repeat of response `0x18a`/`0x18b` ("The lobby's idle repeat", above). **Not built in OpenTPW (`docs/QUEUE.md` Q77); counted as `LOBBY_ADVISOR_IDLE_REPEAT`** | Ghidra |

His clips, lead-in and cue all use the game clock, so a pause carries them on without a jump. In a park, `GameMenu_Open`, `MessageBox_Open` and `OptionsScreen_Open` all call the pause. **In the lobby the original pauses nothing**: he talks on over the Escape menu, and the options screen only quietens the current line while the queue keeps ticking.

OpenTPW deviates here at Alexah's request ("The Advisor shouldn't show in the Options/pause menus"): he is paused in the lobby exactly as a park pauses him whenever a `GameMenu`, `MessageBox` or `OptionsScreen` is open; he is still drawn while paused, held mid-gesture, as the original's pause removes no model; the quiet stop still runs on options, and that does take him off the screen. `Advisor.Paused` runs his own clock, and `Voice.Pause`/`Resume` fade over 10 ms and keep held time out of `Position`. Verified by disk capture: sample 465 held at 1.45 s and resumed 3.66 s later from the same point, and nothing queued started over during options.

### Let the cry bleed through when leaving the lobby

**This is intended behaviour, approved explicitly. Do not "fix" it.**

The front end's end hushes him, as `g_FrontEnd`'s stop (`0x005e4140`) does through `AdvisorQueue_Clear`, so he cries if his voice was sounding. `Level.Unload` then fades every voice over 0.09 s (read from the 90 at `0x0051bcb0`; the unit is inferred). Measured in a disk capture of a reload: the cry starts ~80 ms after the request and the mix is at digital silence 100 ms later, so **about a tenth of a second of "ouch" is audible before the lobby goes**.

Asked whether to keep it or make the teardown silent, Alexah said: "Let the advisor cry bleed through when leaving the lobby, that's fine." A clipped "ouch" during a park load looks exactly like a bug, which is why this is written down.

### The SoundCategoryFile length-check bug

Found on the way to the cry, and it reaches well past the advisor: **park sound work reads the same `.map`/`.sdt` pairs.**

`SoundCategoryFile.TryReadSample` checked the stream length against the map's length with a tolerance of `max(60 ms, 6%)`. Effect 639's record (map 288 ms, stream 366 ms) failed that check, so **the next record silently stood in for it**: 639 played `ouch2`, 640 played `Ouch3` and 641 played nothing at all, with no error reported anywhere.

Across all **31 shipped categories** the stream runs **14-83 ms longer than the map**, so the floor is now **100 ms** (`LengthSlackMilliseconds`). The fix was validated by dumping every category from an unfixed tree and from the fixed tree and diffing: **only those three effects moved.** A silent substitution like this is invisible from code alone — dump and compare, do not reason about it.

## Loading screen

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00587380` | `LoadingScreen_Begin` | Called with (language, `"data\\init"`, steps). **Every call site passes 500 steps** | Ghidra |
| `0x0054dcf0` | — | Boot init — a `Begin` call site | Ghidra |
| `0x0054e360` | main state machine | States 1, 3 and 9 are the other `Begin` call sites | Ghidra |
| `0x005879c0` | `LoadingScreen_End` | Holds the first screen until 3 s after it appeared | Ghidra |
| `0x00587c80` | `LoadingScreen_Step` | Runs only while the screen is up | Ghidra |
| `0x00587db0` | `SpritePack_Load` | Texture-set loader; calls `Step` | Ghidra |
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

**Not implemented:** the splash and the legal screen, counted once a run as `BOOT_SPLASH` and `BOOT_LEGAL_SCREEN` in
`Game.Run` before the first lobby (the movies are `boot.md`'s). The original shows each only on the first call ever
(`DAT_008c4788` and `DAT_008c478c`, which only `LoadingScreen_Begin` writes), and the splash's 2.5 s hold only when its
file loaded. The `welcome_<lang>` overlay is **dead by content**: no Init folder ships one (`Init/400` is empty; 512,
640, 800 and 1024 each hold `Welcome.tga`, `Splash_American.tga` and `Legal_American.tga`), and the paste skips a missing
file (`FUN_00586f40`), so it has no counter.

### A step is exactly one Asset.Register

A step is one `Asset.Register` — Texture, Shader, Material, Model. Nothing else is a step. Audio clips are not steps, so adding sound adds no steps.

**Work that takes real time but registers nothing must NOT be papered over by calling `Step()`.** Doing that makes the bar's *rate* a lie rather than its *length*, which is the worse of the two failures. Report the gap instead. The known example: the per-park sound preload occupies about half a second of an ~8 s load (5.93 s to 6.39 s) while registering no assets, so the bar holds still there even though its total is right. The status line under it still moves, because a log line pumps a frame too.

### The seeds are deliberately unmaintained

The bar **learns**: each situation — the scene, plus whether it has been built before in this run — keeps the count it last measured, in `save\opentpw.cfg`. That file is the truth.

The constants in the code are only seeds for a first-ever run. **Do not re-measure them, and do not "fix" them in a commit.** A new call site constructs `LoadingScreen( what, seedSteps )` with a rough seed and lets `LoadStepCounts` learn the real count; it never passes a hand-measured one. Any step figure written in prose anywhere should be read as dead.

### Measured costs

- The first frame takes ~450 ms, almost all of it the UI shader compile, which would otherwise land on the lobby's first frame.
- After that, ~0.85 ms a frame: ~90 ms over a 6 s load.
- Vsync stays on during the load: turning it off makes no difference.

## The two-pass text class (`0x00491920` / `0x00491ab0`)

Fork watch 2026-10-18 (`verify-ui-hud.md` C1), read from the listing and seen in the original's frames.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00491920` | — | The constructor of a second text-control class. It writes vtable `0x006ff530` (`0x0049199c`). 22 call sites, among them the pause menu's item builder (`0x00485727`), the shared title builder `0x00492f60` (`0x0049309a`), the upgrade list's title (`0x004ae5f2`) and the lobby's park name (`0x004b8e0d`, id `0x1e0ef`, font 0, (252, 232, 7)) | Disassembly |
| `0x006ff530` slot `+0x24` = `0x00491ab0` | — | Its render slot (`0x006ff554`). The eleven other label vtables from `0x006ff320` hold `0x0048f830` there | Disassembly |
| `0x00491da1`-`0x00491e1a` | — | **Pass 1**: glyph colour (`0x80`, `0x80`, `0x80`, `0x80`) through `0x006ad6e0`, the rectangle moved to (x - 2, y + 2, x2 - 2, y2 + 2) (`0x00491dcc`-`0x00491dfb`), and the draw `0x006ae0f0` | Disassembly |
| `0x00491e27`-`0x00491e84` | — | **Pass 2**: colour (`0xff` x 4), the rectangle unshifted, the draw `0x006ae0f0` again | Disassembly |
| `0x0048f830` | — | The plain label render: one draw, white glyphs (`0x0048fa96`) unless `+0xdd` | Disassembly |

So a menu item, a window title or a park name is a grey copy at half strength two pixels left and two down, then the
text at full strength; the shadow is the class, not a flag (the 13 writes of `+0xdb` in `0x00480000`-`0x004c0000` have no
reader). `0x006ae0f0` has four callers: `0x0048fbb0` (the plain label), `0x00491e1a` and `0x00491e84` (this class) and
`0x00492986`. The pause menu ("Load", cyan) and the lobby park name ("Wonder Land", yellow) both show the darker copy
in the original's frames. The options screen's labels do not: `0x004a43b0` and `0x004a4490` build the plain class
(`0x0048f6a0`, `0x004a43f9` and `0x004a44d9`) in (0, 0, 0, 255) and font 6 (`0x007537b4`), and ours matches
(`OptionsScreen.cs`, font 6, `UiColour.Black`, no shadow). Not read: whether the pass's grey replaces the text's own
colour or combines with it; `UiText` halves the colour and draws it at half alpha.

### `UI_SetTitle` (`0x00485d20`), and the buy and hire screens' titles and money

Read 2026-10-25 (Q644, `q644/gd/00485d20.c`). Its arguments are the window, a control id and the text. **Once per
control**, while the control has no skin yet (`+0x98` nought): the rect is widened by half its width either side, the
skin is the static two-pass object `0x007c2118` (built by `PurpleSkin_Construct` `0x00491920` at `0x00485722`, its
destructor queued at `0x00485740`), the font is slot 5 of the current set (`0x0078b5c4 + set * 0x138`, which is
`FUN_00485a70( 5 )`'s entry), the text is centred, and the colour is (234, 239, 102) (`UI_SetTitleColour`,
`0x00492d20`). Then, every call, the text. The buy screen's opener calls it on `0x1fe` (`0x004ad1b8`), the hire
screen's on `0x2480` (`FUN_0049bdd0`); their money controls `0x200` and `0x2496` take a purple skin of their own in
white, font 5, from the right (`0x004ace70`; `FUN_0049bdd0`), both lettered "Cash  $ N" or "Cash  -$ N" from UITEXT
`0x1ca` or `0x1cb`. The original under Proton, the stock park: both titles' brightest pixels (239, 239, 99), each with
the darker copy, the hire screen reading "Cash  $ 88112" (`q644/orig/a-1-buy.png`, `a-2-hire.png`; its "Hire Janitors" is the reference
install's `american` table, where ours reads the English "Hire Cleaners").

**OpenTPW letters both screens larger** (Q644, a deviation at Alexah's word, 2026-10-25, `UiFonts.ParkScreenLettering`):
the same `.bf4` fonts drawn 1.2 times the original's size where each control's box holds it, as large as it holds
where not, never under the original's (`UiText.SizeIn`); a wrapping text breaks at its width at that size. 1.2 is what
a list row holds at the 768 set (font 7's 25-pixel line in a 30-pixel row at 2552 x 1355); at 1280 x 720 the rows are
held to 1.172. Every string of every tab of both screens stayed inside its box at all three sizes (`OPENTPW_TEXT_CENSUS=1`,
`q644/Y/census-*.txt`, `Y7/`, `Y10/`). A face other than the original's was tried and not chosen
(`q644/sheet.png`).

**Why the original's letters looked bolder on that sheet** (Alexah's question, 2026-10-25): not another font and no
shadow on the rows (their label skin draws once). At the same set and scale, both at 1024 x 768, the two games' rows and
labels carry the same ink, within 1.1 per cent, every ink pixel at 255 (`q644/w-crops.png`). The reference install runs
at 800 x 600, so it letters set 2's smaller GAME9 and its frame was stretched to 1024 and then 1.76 times: a one-pixel
stroke 2.25 pixels wide at full strength. Ours drew set 3's GAME10 1.76 times through the linear sampler, which spreads a
stroke over two pixels and dims it. **OpenTPW now copies a text's pixels by nearest to the whole number at or under its
drawn scale before the sampler enlarges it** (`UiText.PrescaleFor`, 1 to 4; a deviation of its own, the original never
scales a font): at 2552 x 1355 the rows' strokes went from a share over 200 of 0.136 to 0.481, the original's sheet tile
0.199 (`q644/y-rows.png`); under a scale of 2 nothing changes. Never more than the scale, so a text is never drawn
smaller than its texture: `Texture.UpdatePixels` rewrites the first level alone, and a shrunk text would be read from
its stale mip maps.

## Escape game menu

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0048c830` | `GameMenu_Open` | Opens the menu | Ghidra |
| `0x0048c600` | `GameMenu_BuildLobby` | The lobby's menu build | Ghidra |
| `0x005e41c0` | `IslandLobby_OnKey` | On Escape's release asks each active child's `+0x18`, and opens the menu only if none answered - the island camera's cancel is `lobby.md`, "Escape cancels the fly-in" | Ghidra |
| `0x0048bd40` | — | The lobby menu's own key handler, installed by `GameMenu_BuildLobby` (the park menu's is `0x0048b6a0`, `scenes.md`); closes on Escape | Ghidra |
| msg `0x11` | — | Show. Sets the resting colour (0, 175, 190) | Ghidra |
| `0x0048b220` | `MenuChoice_TickColour` | Hover ramp: grey 33, +32 per tick, up to white. The tick is per frame in the original; **taken as 30/s here — unproven** | Ghidra |
| vtable `+0x24` | — | The front end's "is someone playing", which gates showing Select New Player. The reading is an interpretation | Ghidra |
| cat_ui effect 193 | — | The click. Source measures -12.4 dBFS RMS / -0.2 peak by disk capture — much louder than BUTTON01 — so gain 0.132 puts it at -30 like the other clicks | Disk capture |

It is built in code, not from a layout tree: a LOLIGHT full-screen control plus `MenuList_AddItem` items, font 0 with the purple skin, centred, first item at y = 5 and each next at y + h + 5, where `h = (line height + 5) * 0x600 / screen height`. That is the lobby's; in a park `GameMenu_BuildPark` (`0x0048c150`) puts the first at 10, except where `FUN_005b6450()` answers non-zero, which puts it at 0 and each next at y + h.

Go Online is a dead end — it only closes, counted as `GO_ONLINE`. Escape over a message box goes to the box: `UI_LoadModalTree` gives it the focus (`0x0047ee67`) and a key goes to one control only, the focus while it is visible (`0x006698e6`), so it never reaches `IslandLobby_OnKey` (`lobby.md`, "Escape cancels the fly-in"). Over the options screen no key reaches the lobby: the screen hides the lobby's root (`0x004a3ae0`), which keeps the focus, so a key goes to the last control pressed, which drops it (`lobby.md`, "The lobby's keys act on the release").

The click's level was measured by disk capture rather than read from the `.sdt`.

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
| `0x00423740` | `GameOptions_CheckChanges` | Compares each option with its value when the screen opened, raising a restart level at `+0x94`: for a new graphics quality, `0x00423bc0` reads that quality's detail file into a local only to compare it with the values loaded at boot (`FUN_0044dc60`, the one loader of the live detail file; `lobby.md`'s `0x007858c8` row), and a difference raises the level | Decompile |
| `0x004237f0` | `GameOptions_Accept` | The tick: calls `CheckChanges` (`0x00423814`), reads `+0x94`, and shows RESTART GAME (UITEXT 403, pushed at `0x0042387d`) instead of closing when a restart is due. The box's tick (`GameOptions_RestartBox_Tick` `0x00424bb0`) leaves a park through the autosave (`saves.md`, "Leaving a park: the autosave"). OpenTPW's `OptionsScreen` skips that check; its display and resolution take effect on the tick, with no restart | Disassembly |
| — | `Sound_ApplyGroupVolumes` | Speech volume 0 disables the duck — **proven** here | Ghidra |
| `0x1d4c8` | — | The rendering row's control | Layout stream |
| `0x1d4d2` | — | That row's `b_on2` arrow | Layout stream |
| `0x1d4d5` | — | The resolution slider | Layout stream |

**Meshes are found by node-name hash:** `b_on` (switch, down = off), `b_on2` (arrows), `f_optpanel`/`f_optpanel2`/`f_optpanel3` (rows), `b_scroller` (thumb), `f_screen` (backdrop), `!f_plain` (frame).

Volume and quality defaults come from `sound.sam` `DefaultVolume.*` and `SoundInfo.*`, by name. Everything but the display and resolution applies live; cancel restores the snapshot. Options persist as the original's do: machine options in `save\Config.tcf` on the tick, player options in the player's `gms.dat` when the player is saved — see `saves.md`.

Deliberate differences: cancel re-applies volumes (the original does not); OK's RESTART GAME and audio-quality checks are skipped.

**Two rows are no longer the original's**, at Alexah's word:

- the **rendering row** is now "Display: Windowed / Full screen / Borderless full screen". The original has no such row and no UITEXT for one, so its three words are OpenTPW's own. Taking over an existing row rather than adding one keeps the layout the compiled stream's. **GPU vs Software is no longer reachable**, though `CardRendering` is still read, written back, and still caps graphics quality.
- the **resolution slider** steps the display's real modes — its `Maximum` is one less than the mode count, one step per mode, not 100 — and only decides anything in exclusive full screen. It is switched off in the other two, where it reports what is in use. A switched-off slider had to refuse the wheel as well as the hit test, because the thumb takes the pointer like any button.

Both apply on the tick and only when they actually changed, so no RESTART GAME box is needed. What OpenTPW is really set to lives in `save\opentpw.cfg`; `Config.tcf` keeps the nearest of the original's three, so it stays a file the original can read.

Earlier rulings that still stand: the settings screen is 1:1 for now, video card is dead, and GPU/software is dead and unreachable (its row carries the display mode). Resolution is the exception, and is real, as above. Give the dead rows a meaning only with Alexah's word; `docs/QUEUE.md` Q33 proposes UI scale for the video card row.

### Uniform blocks must be per draw

**A renderer property, not an options-screen one. Any new screen that writes many materials a frame will hit this.**

`Material.SetInFrame` wrote one shared uniform buffer through the command list between draws. With the options screen's ~26 writes a frame, a draw now and then used the *previous* draw's value — a panel landed on the row above, hiding its text and thumb — on about a third of frames. `Device.WaitForIdle` did not help. The fix is a uniform block per draw (two rounds, used on alternate frames); 34 of 34 frames were stable afterwards. The player slots and the name dialog never showed the fault, because they write few materials.

### What was verified, and what was not

Verified with a silent XTEST harness at 1280x720 (the options screen's virtual x maps as `160 + x * 720/1536`): menu open and close over the slots and over the panel, and blocked by a dialog; the hover ramp; options open quietening the advisor; the switch; the rendering cap on quality; drag, wheel and page click; the volume-off text; cancel reverting and keep keeping; popup help off killing the help bar and the glints; the quit box over the menu; Select New Player returning to the slots with the player kept and the advisor's welcome-back line (471). By disk capture: a muted lobby is digital silence apart from the 0.30 s menu click; effects off with the music slider at 0 gives 100% exact zero samples; cancel brings sound back. Frame time with the options screen open was 6.95 ms at 144 Hz vsync, the same as without it.

**Not verified / unproven / untraced**, collected:

- the percent-to-loudness curve past the mixer's door — on the game's side it is a straight multiply (a slider's percent times 0.01 is its group's level, `0x006b59d0` and `0x006b5a30`, the float at `0x0070a280`; `audio.md`, "A channel's volume"), which percent / default keeps in proportion; how the mixer applies the number to a sample was not read;
- anything against the original actually running: no reference screenshots of either screen exist locally;
- the menu's hover tick rate taken as 30/s;
- the 60 passed by `Sound_StopFading` is a per-pass volume step, not milliseconds (`audio.md`, "The fade"); how often the library's service pass runs is not measured;
- the `0.09 s` voice fade inferred from the 90 at `0x0051bcb0`;
- `Game_StateMachine` `0x0055035a` and the postcard caller `0x004a9380` as quiet-stop callers, neither checked against OpenTPW;
- the original's cue-timer behaviour after a cut (`0x00f79778`);
- `FUN_0059aa70`, not traced further;
- the lobby's 90-second repeat of response `0x18a`/`0x18b`, not built (counted, Q77).
