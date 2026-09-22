# Lobby and front end

The lobby is the island scene plus the front end drawn over it: the four player slots, the new
player dialog, the message/quit box, and the island panel that picks a park. None of that layout
lives in the data — every window is a stream of 16-bit opcodes compiled into `testme.exe`. This page
also holds the four facts that were measured in the lobby but are not about the front end: the
particle system, the `.md2` material flag word, the two animated gates, and the park-name string
table. Two of those carry warnings that outlive their subject — the particle owner rule and "the
lobby is not representative of the rest of the game's art".

## Compiled layout streams

Each window is a stream of 16-bit opcodes compiled into the executable and walked by a reader, not
a file under `data/`.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0065fd58` | — | Walks a layout stream. Op `0` = control type/flags/id/rect, `1` = mesh by hash, `3` = text rect, `0x11` = UIHELPTEXT row, `5` = end | Ghidra `run_python` dumper over every call site |
| `0x0065fd0e` | — | Loads a tree | Call sites of the stream walker |
| `0x0047ed80` | — | Loads a tree as **modal**; adds a LOLIGHT dimmer | As above |
| `0x753c68` | — | Tree: player slots | Dumped call site |
| `0x753f50` | — | Tree: new player dialog | Dumped call site |
| `0x74f920` | — | Tree: message box | Dumped call site |
| `0x757f60` | — | Tree: island panel | Dumped call site |
| `0x7581a0` | — | Tree: the **online globe's hover readout** — two labels, a name and "Parks: N" (UITEXT 462), both blanked on null, created at lobby start beside the globe panel | Dumped call site; the labels' own text ids |
| `0x7596b0` | — | Tree: online globe panel, 14 controls. Loaded on the lobby path, unbuilt | Dumped call site |
| `0x774c18` | — | Tree: mail badge, mesh `i_mail`. Loaded on the lobby path, unbuilt | Dumped call site |
| `0x1e0ec` / `0x1e0ed` / `0x1e0ee` | — | The player's golden-key count, part of the **island panel** | Control ids in the island panel's stream |
| `h = (c ^ h) * 47` | — | Mesh-name hash, over the node name of the model's **first** mesh — e.g. `wdialogw`, `islandlob` | Reproduced against every ui.wad mesh name |

`0x7581a0` is **not** a key count, and no second key counter should be built from it. The lobby has
**at least seven trees, not five**; all three unbuilt ones belong to the online world, which is a
permanent dead end.

## Meshes and fonts

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| — | ui.wad `.md2` | UI meshes authored on the same 2048x1536 virtual screen. A part (node) is stretched to fill its control rect; frame + state picks the part (buttons: normal, disabled, hilite, hidown, helddown, down) | Mesh node names and the part picker |
| `0x00477310` | — | 9-slice path, taken for `!`-prefixed meshes | Disassembly |
| `0x00419710` | — | `.bf4` font loader: ids out of `Language\English\residx.dat`, 13 slots x 4 resolution sets | Disassembly; the `.bf4` format is written up in the FileFormats docs |

## Instant Action vs Full Simulation

The mode is one global, and it is the reason this project loads only one park file.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `gms.dat +0x24` | — | The Instant Action byte, read when a player is picked. **0 = Full Simulation, 2 = Instant Action.** A third type `1` exists and is **not identified** | Save layout; the value handed to SetGameType |
| `0x005c83b0` -> `0x00550d80` | SetGameType | Sets the mode. Its assert string "Invalid GameType in SetGameType" names it | Assert string in the binary |
| `DAT_00fb3b7c` | — | The mode object's first dword — what every reader tests | Every call site reads this dword |
| `0x004a6a50` | FrontEnd_ClosePlayerSlots | Closing the player slots. In Full Simulation a new player gets **1 golden key** here, by calling `PlayerProgress_AddKey`; in Instant Action it skips that call and queues response 394 in place of the advisor tour | Disassembly; Ghidra body `004a6a50-004a6b76` |
| `0x005afc30` | PlayerProgress_AddKey | Adds the golden key. A four-byte body, so a thunk or stub onto the real routine. Called by `FrontEnd_ClosePlayerSlots`, and only in Full Simulation | Ghidra body `005afc30-005afc33`. **These two were recorded as one routine with "both entry points"; they are separate functions in a caller/callee relationship** |
| `0x004b9340` | IslandPanel_Refresh | Refreshes the island panel | Disassembly |
| `0x004b9840` | — | Hides the price (`0x1e0ea`) and the held key count (`0x1e0ec`) and **disables** both island arrows (`0x1e0f0`, `0x1e0f1`) — all four together, while the mode is 2 | Disassembly |
| `0x0065da8d` | — | The enable/disable setter: puts flag **`0x2`** on the control at `+0x48` | Disassembly |
| `0x00668820` | — | Part picker: draws **part 1**, the disabled part, for a control carrying `0x2` | Disassembly |
| `0x0065d9dd` | UI_SetVisible | The hide. **Never a vtable entry** — so a virtual call at slot `+0x14` is always the enable, never the hide. That is how the two operations tell apart in a decompile | Vtable scan |
| `0x005e1ee0`, `0x005e1f40` | — | The handlers behind the island arrows. Both return having done nothing unless the type is something other than 2 | Disassembly |
| `0x005e1cc0` | — | Enter this park. In Instant Action it goes straight in without counting keys | Disassembly |
| `0x005e1fa0` | — | Puts the lobby back on an island. Called with `1` it selects the **first** island and forgets the remembered park; called with `0` it walks the list matching a name | Call sites |
| — | global.sam | `Keys.CostToEnter` per park: jungle 1, hallow 1, fantasy 3, space 5 | Shipped `global.sam` |

Closing the player slots always returns to the first island, which is **Lost Kingdom** — the only
park shipping an `Easymode.TPWI`, the pre-built park an Instant Action game is handed, and the
reason that mode is Lost Kingdom only.

**Verifying a mode needs the real screens.** Nothing on the debug console can create a player, so
the dialog has to be driven with XTEST. Use the game's own `Lobby camera: moving to island N` log
line as the evidence that an island did or did not change, and **do not use the `island` console
command to ask which island is showing — it SETS it, and bare `island` selects island 0.** It is a
probe that mutates what it measures, and it fails silently.

Island panel button centres, for a harness clicking at 1280x720: world view (68, 479), enter park
(68, 558), left arrow (81, 645), right arrow (158, 645). `x = 144` misses the arrows.

Other cues on this path: the panel only shows the new player's key when the advisor's tour
(response 393 -> sample 468) hits its 17000 ms cue (gesture table `0x0076dc18`, row 12).
Response 570 -> sample 588 explains the dialog. The radio group starts with nothing chosen —
`0x004a6e40` asks for child `0x7a17`, which it lacks.

## Anchoring

Each control pins to the **third of the virtual screen it was laid out in**. A window wider than 4:3
leaves width over, so a left-pinned control sits half that slack left of where centring would put it
— 240 px at 1920x1080 — while 4:3 looks perfect.

**When something looks right at 4:3 and wrong elsewhere, suspect an anchor**, and compute the
expected error from `Scale = min(w/2048, h/1536)` before measuring.

The park name (rect 477..1571, own middle 1024 = screen middle) is the worked example: an explicit
left pin welded it to the panel as one bottom strip, which only reads at 4:3, and the original's own
screenshots centre the name. It is centred.

## Lobby sounds and cues

`FUN_00521e60` is **not** a sound call — it spawns particle effects. The real lobby sounds are few.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00803a2c` | cat_ui | The UI sound category: effect 31 `BUTTON01`, 189 `Select3` on button clicks; 198 `goldkey` at the tour cue | Sound map |
| `0x00485780` | — | UI_Init's click hook, which plays the above | Disassembly |
| `0x005e1e30` | — | Enter park, affordable: panel hidden, globallobbysfx effect 4, particle 98. Locked plays nothing at all | Disassembly |
| `0x005e1bd0` | — | Online world. Does nothing offline — a permanent dead end | Disassembly |

Measured lobby mix: the goldkey click read back a gain of 0.090 = 0.484 x master 0.5 x duck 0.38.

## Particle system

Effects come from `Data\Particle\Tp2.plb` (`.plb` = 320-byte effect records and 104-byte effector
records), with sprites out of `esprites.wad` (`.ESP` banks, `.TPC`/`.FPC` packs, row RLE over a
BGRA palette).

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0051f370` | — | Loads `Tp2.plb`; its `PTCL:` strings name it | Strings + disassembly |
| `FUN_00521e60` | — | Spawns a particle effect (not a sound call, as it was once read) | Disassembly |
| `0x0051ef30` | Particles_Render | Draws the screen particles. **The only caller of Sprites_LookUp** | `get_xrefs_to` |
| `0x0057c620` | SpriteBatch_DrawParticles | The batch draw, where the screen mapping is applied | Disassembly |
| `0x005423a0` | Sprites_LookUp | Sprite set lookup, keyed `bank<<4 \| set` | Disassembly |
| `0x00540d90` | SpriteBank_Load | Picks between a `.TPC` and the `.FPC` beside it from the ESP byte at `0x10C`. The selecting constants **read back EMPTY in Ghidra and are NOT identified** | Disassembly; attempted constant read |
| `0x00582170` | — | Sprites are flushed here, after all UI models, at depth 0, LESSEQUAL, with no depth write | Disassembly |
| `+0x14` | — | A particle's position within its record (an earlier note said `+0x10`; it is `+0x14`) | Field read in the step routine |
| `0x0078d90e` | — | The glint gate byte = **Popup Help** (options case `0x1d4c3`, label UIStrings 327) | Options screen wiring |
| `0x005ed920` | — | Spawns the button hover glints (effect 35) when that option is on | Disassembly |
| `0x007858c8` | — | Particle density global. **Only ever read** — which detail preset the original starts on was not found | `get_xrefs_to`; `med.sam` has `GameOptions.PARTICLEDENSITY 1000` |

**Screen mapping** (from Particles_Render and SpriteBatch_DrawParticles): across
`((x - 3125) * 16) / 49`, down `(z * 16 - 37500) / 37`, both `/1024` into -1..1; `size >> 6 / 2048`
is the half height; the half width is multiplied by the sprite's w/h in the wider across units — a
4:3 stretch, which is why reference twinkles are wider than tall.

**Blend:** the state word's low byte is `(SRCBLEND << 4) | DESTBLEND`; draw flags `0x4 + 0x2000`
give `0x52` = SrcAlpha/One.

**Sprites:** set = `bank << 4 | set`. Particles are `Generic\Particles` `SPR_PA` (bank 0) and
`SPR_PB`; set 12 is a 16-frame twinkle, set 15 the key picture. Named lobby effects: key sparkle 97,
key burst at park entry 98, cue burst 87, button hover glints 35.

The simulation is integer, mirroring the original: MSVC LCG seeded 1, a `sin * 256` 512-entry table,
and a 31 ms tick taken from the game clock rather than its own accumulator.

### Deliberate departure: effects must name their owner

The original draws **every on-screen effect over every window** — sprites are flushed after all UI
models at depth 0 with no depth write, and nothing hides them when a menu or box opens. Alexah
reported that as a bug: the key sparkle and the key ring shone through the menu dimmer and the quit
box.

So OpenTPW deviates on purpose. An emitter or effector carries an `Owner` (a window), inherited by
linked, end and death spawns, and each window's effects are drawn right after that window.

**Any new on-screen effect for a window must pass its owner, or it draws over everything.** Do not
"restore" the original order — that reintroduces the reported bug.

### Decisions taken without proof

Say so if asked: particle density comes from `med.sam`, since the original's starting detail preset
was not found. On wide windows, effects pin like a control at their spawn x, spawned-by-effect
inherit the pin, and glints take the button's anchor. Handles are never 0 (the original's first
handle can be 0). Keys look near-white because the art is additive gold over a light sky.

### World sprites (decoded, not built)

Drawing effects **in the world** is a whole subsystem, not a variant of the screen path — nothing in
it reaches `Sprites_LookUp`, whose only caller is `Particles_Render`.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `DAT_007b49f0` | — | Table of world sprite instances, **0x118 bytes** each | Disassembly |
| `FUN_00475a10` | — | Allocates an instance | Disassembly |
| `FUN_00475360` | — | Steps an instance | Disassembly |
| `FUN_00475010` | — | Runs the instance's program: fetches `*(code **)(base + pc * 4)` and calls it — opcode **function pointers**, which is why a person record carries `mSpriteScript` | Disassembly |
| `+0x88` / `+0x8c` / `+0x90` | — | World position, three floats, ten world units to a cell. `+0x8c` is an **offset above the ground**, not a height. (A thing's own `mX`/`mY` agrees to within a third of a unit and is still the better source, being what the park saved) | Field read; an earlier "no position" finding came from a scan that swept only cell-shaped values |
| `+0xac` / `+0xb0` | — | Sprite kind and bank | Field reads |
| `+0xb4` | — | **Not** a variant: a packed bank offset (high bits) and set (low four bits) | Field read |
| `TPCS` | — | The save block world sprites persist in | Save reader |
| `0x40` | — | Visibility byte on the world path | Disassembly |

Also still unbuilt: advisor clip glints (channel 0, effects 49 and 44, flags `0x40000` / `0x20000`).
`.TPC` and `.FPC` are **not** open questions — all 46 `.TPC` and all 29 `.FPC` files in
`esprites.wad` are version 3 (second word 3); an `.FPC` and the `.TPC` beside it share a picture
count and differ only in size.

Ghidra names added along the way: `Particles_Tick`, `Particles_Emit`, `Particles_EmitRing`,
`Particles_StepParticles`, `Particles_StepEffectors`, `Particles_SpawnEffector`, `Particles_Move`,
`Particles_SetHidden`, `SpriteBatch_DrawParticles`, `Sprites_LookUp`, `SpritePack_Load`,
`UIParticles_PlaceChannels`.

## The `.md2` material flag word

Measured first across `lobby.wad` and then across every `.md2` in the game: 12,951 material uses in
841 material-bearing mesh files, from 312 unique WADs. (`Data/` is an exact case-duplicate of
`data/` — do not double-count it.)

The word is the first four bytes of the 8-byte frame-table entry a material's `FrameOffset` points
at, so it belongs to the texture *as used by that model*. **Only the low byte is ever used** (max
`0x73`, 15 distinct values game-wide).

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x1` | — | Set on every material in the game. Says nothing | Whole-game count |
| `0x2` | — | **"Draw see-through."** One-directional: bit set → the texture has an alpha channel 87% of the time; texture has alpha → bit set only 56%. An **authoring decision**, not a property of the texture format | Cross-count against every `.wct` |
| `0x10` / `0x20` | — | **Unknown**, and **not a pair** — alone 1,209 and 1,246, together 2,640 | Whole-game count |
| `0x40` | — | 159 materials. Meaning unknown | Whole-game count |
| `0x80` | — | Never set | Whole-game count |
| header `0x30` | — | The engine propagates both `0x40` and `0x80` into this header field | Disassembly |

**Two traps, both of which caught the first pass:**

1. Compare against the `.wct` header's **alpha-channel byte (offset 1), not its bit depth (offset
   2)**. `sen_ant1` is 32-bit and declares no alpha. Conflating the two is what made an earlier
   count say 7 disagreements instead of 8.
2. **The lobby is not representative of the rest of the game's art.** There the bit agrees 224/232
   (97%); game-wide it is 83%. Any claim measured on `lobby.wad` alone must say so.

`0x2` also does **not** separate cut-out art from blended art. In the lobby most of what carries it
is cut-out foliage wanting an alpha *test* — palm fronds, grass, bushes, bats, butterflies; only the
ripple rings, the sea and the antenna cone are true gradients.

**What the renderer does with it.** The original never reads this bit at draw time at all: it
classifies from the texture's pixels and picks one of two alpha references, 16 or 240 of 255. See
`render-states.md`. OpenTPW keeps the bit as the gate for *whether* a surface is see-through —
taking every texture's alpha at face value ate holes in geometry the game draws whole — and uses the
pixels only to choose the reference. Measured over the lobby, **every** see-through material
classifies as graded and takes the low reference, because the `.wct` codec rings partly-clear texels
around each hard edge and those are exactly what the original counts.

## Two gates, in different states

There are two animated gates and they are **not** the same problem. Establish which scene is meant
before answering anything about "the gate cycling".

### The park gate — driven, not looped

`ParkFixedItems` once loaded `gates` and `lights` as plain models and drove them with a model
update, so the gate swung open and shut for ever with nothing having asked it to. Both are
**scripted things in the original** — the archives ship `Gates.RSE` and `lights.RSE` beside the
models — so their movement belongs to an animation player that a script triggers.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| thing ids 11, 12 | `mParkGates`, `mTrafficLights` | The fixed items **do** carry thing ids, named by the save header itself (an earlier claim that they carry none was wrong) | Save header field names |
| `FUN_005156a0` | — | Finds the two **by name** among the item descriptions and builds each through the ordinary object constructor — the engine's own arrangement | Disassembly |
| `FUN_00519ef0` | — | Opening or closing the park. **The only thing that ever writes `VAR_COMMAND`**, which `Gates.RSE` idles on | `get_xrefs_to` on the variable write |
| `mParkClosed` 1 / 2 / 0 | — | **1 opens, 2 shuts, 0 clears.** Read off the **disassembly**: the decompiler renders those call sites with the wrong argument lists and yields a plausible, wrong "1 opens, 0 shuts" | Disassembly, against the decompile |

The command variable is resolved **by the name the script itself declares**, and a write that lands
nowhere is **reported as a miss** — because a command that goes nowhere looks exactly like a gate
nobody commanded. A park saved open now draws open.

**The traffic lights are bound and provably inert:** `lights.RSE` loops unconditionally, but both
clips it loops carry **zero tracks**, so nothing on screen can move. That is why the old
suffix-matching loop was only ever visible on the gate. **Do not read the silent crossing as a
posing bug.**

### The lobby island gate — driven by park entry

The gate idles shut and plays its opening clip **once**, when the player enters that park
(`LobbyGate.Open`, called from the front end's `EnterPark`). It used to loop open/shut as a
diagnostic, because nothing raised a park entry; that trigger now exists, so the loop is gone.

**The original does not animate this gate at all**, and that is measured. Its whole entry beat is
`IslandLobby_LeaveForPark` (`0x005e1e30`): set the lobby leaving, `IslandPanel_KeyPuffAndEnterSound`,
then `FUN_004b8ec0`, which posts UI message **6** to the island panel's own tree (`DAT_007cc4b4`) —
a message `IslandPanel_Callback` does not handle, so it falls through to the default and is the
generic close. The state-3 teardown behind it (`FUN_005d5cf0`, "choice 2 means play a park") only
tears down. So the swing is **ours**, under `CLAUDE.md` rule 11, and is marked as a deviation at the
call site.

**Not every gate is a rotation animation.** Measured across all four, not inferred from the jungle's
hinged pair — the model that cannot catch the mistake:

| gate | M1 carries | frames | movement ends | how it opens |
|---|---|---|---|---|
| `Jun_gate` | rotation 2 | 0–60 | 57 | two doors on hinges |
| `Hal_gate` | rotation 2 (3 clips) | 0–600 | 60 | two rails; the clip runs 10× past the movement |
| `Spa_gate` | rotation 1 | 0–100 | 60 | the hatch |
| `Fan_gate` | **morph 2, rotation 0** | 0–100 | — | the worm; it gets no `MeshRotator` at all |

So a gate is played for as long as it **moves**: the rotation movement's end where it has one, and
the clip's own span otherwise. Taking the length from the rotator alone leaves fantasy with nought
and its park loads with no animation; playing to `LastFrame` instead would hold a player entering
Halloween World in the lobby for twenty seconds.

The same "looped because nothing sequences it yet" stand-in still applies to the rest of the lobby:
the Dino and the butterflies.

## The lobby camera has two modes, and only one of them is built

`FUN_005e0470` is the whole lobby camera update. It branches at the top on whether anybody is
playing: **no islands, or no player selected** (`FUN_0048bcd0()` → `+0x60 == -1`), **or no current
island** (`[3] == 0`) takes the **attract** path; otherwise the **globe** state machine runs on `[5]`.

OpenTPW builds neither. It orbits whichever island is on show and eases between them, which is
`FUN_005e1210` — the shared tail both modes call — without either mode in front of it.

### The object

Built by `FUN_005dfcd0`, vtable `0x00702cb0`. Its `[2]` is a 0x10-byte island list (vtable
`0x00702cac`, head at `+4`, count at `+0xc`) which the update walks; `[3]` is the current island.

| Field | Value at construction | What it is |
|---|---|---|
| `[0x16..0x18]` | **500, 75, 500** | Wander box centre |
| `[0x19..0x1b]` | **400, 50, 400** | Box extents, the **full** size — a point is `centre + rand*extent − extent/2`, so X/Z **300–700** and height **50–100** |
| `[0x1c]` | **1.0** | Wander speed, per lobby tick — **10 u/s** at ten ticks a second |
| `[0x1d]` | **100.0** | Arrival threshold, a **squared** distance, so radius **10** |
| `[0x1e..0x20]` / `[0x21..0x23]` / `[0x24..0x26]` | random / random / normalised | Camera position, its target, and the unit direction between them |
| `[0x2a]` / `[0x2b]` / `[0x2c]` | **2.0** / seeded at 2.0 / **50.0** | Look-speed cap, current look speed, and its **squared** distance threshold |
| `[0x2d..0x2f]` / `[0x30..0x32]` / `[0x33..0x35]` | random / nearest island / normalised | Look-at position, its target, and the unit direction |

The box is the lobby's own geometry: the four islands stand at 400 and 600 in X and Z, centred on
(500, 500), and the camera wanders a 400×400 box around them between heights 50 and 100.

### Attract: fly the box, aim at the nearest island

Per frame, with the lobby's delta (`0.01 × ms`, ten units a second — see `TicksPerSecond`):

1. Take `target − position`, normalise it (a zero vector becomes `(1,0,0)`), ease the **stored
   direction** toward it at `0.1 × delta`, and re-normalise.
2. Step the position along that direction by `[0x1c] × delta`.
3. If the position is within `[0x1d]` of the target, **roll a new target** in the box.
4. Walk the island list for the **nearest** island to the camera (seeded `9999999.0`, squared
   distances) and store it in `[3]` and `[0x30..0x32]`.
5. Ease the **look direction** at the same `0.1 × delta`, re-normalise, and step the look-at along it
   by `[0x2b] × delta`.
6. Ramp the look speed: further than `[0x2c]` → `speed += cap × 0.05` clamped to the cap; nearer →
   `speed −= cap × 0.05` floored at nought.

**Step 6 is per FRAME and is not delta-scaled**, the same trap as the lightning roll — do not convert
it through a ticks-per-second constant.

#### Every constant of it, read out of `.rdata` 2026-09-22

Taken with the `read_memory` tool rather than `run_python`, per rule 102. These are the numbers the
whole flight is made of, and they are recorded here because a later reading of the box as a *half*
width would halve the camera's distance and look like a fix:

| Where | Value | What it is |
|---|---|---|
| `_DAT_00702c58` | 2⁻³⁰ | Scales the 30-bit masked random to 0..1 |
| **`_DAT_00702c5c`** | **0.5** | The roll is `centre + rand*extent − extent*0.5`, so **the extent is the FULL width** |
| `_DAT_00702c7c` | 0.1 | The ease on **both** stored directions, per delta |
| `_DAT_00702c78` / `_DAT_00702c84` | **+0.05 / −0.05** | The look-speed ramp: far subtracts the negative (ramps up, clamped to the cap), near subtracts the positive (decays, floored at nought) |
| `FUN_005dfcd0` `[0x16..0x18]` | 500, 75, 500 | Box centre, middle component vertical |
| `[0x19..0x1b]` | 400, 50, 400 | Box extents, full size |
| `[0x1c]` / `[0x1d]` | 1.0 / 100.0 | Wander speed per tick; arrival threshold, **squared** |
| `[0x2a]` / `[0x2b]` / `[0x2c]` | 2.0 / 2.0 / 50.0 | Look-speed cap, look speed seeded at the cap, arrival threshold **squared** |

**OpenTPW matches every one of them**, and the flight was then measured in the running game to confirm
it rather than only the constants: over 60 s the camera stayed inside the box on all three axes
(z 55–79 of the 50–100 the box allows), a median of **101.5 units** from the nearest island, and
three island changes. The per-frame movement independently re-derives the speed: 10 units a second
over a 6.95 ms frame is 0.0695 units, against a measured median step of **0.0694**.

**The aim is eased, and it does not snap.** A change of nearest island moves only the *target*
`[0x30..0x32]`; the look **point** `[0x2d..0x2f]` then travels toward it at a ramped speed with its
direction eased at the same 0.1. Measured over 150 s and 8 island changes, the view turns **no faster**
near a change than away from one — median 5.31 against 5.59 degrees a second, with a *lower* maximum
(15.62 against 19.07) — and over 120 s **no frame at all** stepped more than five times the median
per-frame step. There is no swing and no jerk to find.

### Globe: spin, home, pull in

| State `[5]` | What it does |
|---|---|
| 0 | Orbit advance, `angle += delta × SPINSPEED`, wrapped against 2π (`0x00702c18` = π) |
| 1 | Homes the angle onto the island's own heading `island[+0x14] + π`, shortest way round, at `0.05 × delta`; on arrival calls `FUN_005d83f0(0,0)` and goes to 2 |
| 2 | Locks that heading, then decays radius `[8]` at **0.07** and vertical `[9]` at **0.6** per delta — the `GLOBERADIUSOUT`→`GLOBERADIUSIN` pull-in — and when the radius falls below **8.0** calls vtable `+0x48` |

`+0x48` is `__amsg_exit(0x19)` — MSVC's **pure virtual** stub — in this vtable, so the running object
is a derived class that overrides it.

### The derived vtable, found 2026-09-22 — and it is the park-entry animation

**That derived vtable was previously recorded as "not found". It is at `0x00702ec0`**, and it was
located by searching for the pointer bytes of `IslandLobby_EnterPark` rather than by scanning for the
base's update. Its slots:

| Slot | Address | What it is |
|---|---|---|
| `+0x08` | `FUN_005e1830` | The island lobby's **own update** — and its first instruction is `CALL 0x005e0470`, so the base camera update really does run here |
| `+0x38` / `+0x3c` | `0x005e1ee0` / `0x005e1f40` | The island arrow handlers, overriding the base's next/previous island |
| `+0x40` | `IslandLobby_EnterPark` `0x005e1cc0` | Checks `[5] == 0` and the key count, then calls `+0x44` |
| `+0x44` | `IslandLobby_LeaveForPark` `0x005e1e30` | Sets `+0x14` to **1**, key puff and sound, closes the panel |
| `+0x48` | `FUN_005e1e50` | The override of the pure-virtual slot above |
| `+0x58` | `0x0067b0c0` | Still pure virtual |

**`+0x14` is `param_1[5]`** — the update takes `int *`, so `param_1[5]` is byte offset `0x14`. So
`IslandLobby_LeaveForPark` does not merely close the panel: **it puts the camera into globe state 1**,
and the whole sequence above is what happens next. The park is asked for when the **camera** arrives:

1. `+0x40` → `+0x44`, which sets state **1** and closes the panel;
2. state 1 turns the orbit onto `island[+0x14] + π` at `0.05 × delta`, arriving within half a step;
3. state 2 locks it and decays radius at **0.07** and vertical at **0.6** per delta;
4. below radius **8.0** it calls `+0x48` = `FUN_005e1e50`, which ends `MOV [EAX+0x14], 2` on the scene
   object at `DAT_00f82884`;
5. `FUN_005d5cf0`, the state-3 teardown, **returns that field** — the documented "choice 2 means play
   a park".

From SPINRADIUS 70 at 0.7 a second that is `ln(70/8) / 0.7` ≈ **3.1 s** of flying in, with the vertical
offset collapsing about ten times faster. **So the original is not blank between Enter and the loading
screen** — it swings the camera round onto the gate side and flies it into the island. `FUN_005d83f0`,
called on the state transitions, is *not* a gate animation: it is `__thiscall` on the island and plays
the ISLE model's clip 0 or 1, the same pair the update's tail loop picks between.

### Island sound is one island at a time, and the previous one is stopped

Three functions manage the per-island pair, all the same shape and all gated on `[5] == 0`:

| Address | Vtable slot | What it is |
|---|---|---|
| `FUN_005e1640` | `+0x38` | **Next** island — walks the forward link `[1]` |
| `FUN_005e1730` | `+0x3c` | **Previous** island — walks the back link `[2]` |
| `FUN_005e14a0` | — | Reset to the head of the list |

Each one stops the outgoing island's two voices with `Sound_StopFading( island[+0x1c] )` and
`Sound_StopFading( island[+0x20] )`, zeroes both, picks the neighbour through vtable `+0x4c`, then
starts the incoming island's **effect 1** from the sfx category (`DAT_00803a4c + 4 + idx*8`) and
**effect 2** from the music category (`DAT_00803a4c + idx*8`), keeping the voices in those same two
fields, and calls `Sound_ApplyGroupVolumes`. So the original plays **one island's theme and ambience
at a time and explicitly stops the previous** — voices do not accumulate. OpenTPW's `LobbyAudio.MoveTo`
already does the same thing, with a crossfade where the original cuts.

**The attract path never touches `[+0x1c]` or `[+0x20]`.** These are the button and key handlers. So
while the camera is wandering, no per-island theme is started or stopped by the camera at all; the
only per-island audio it produces is the **ambient one-shot** — `Sound_PlayEffect` of effect 3, 4 or 5
by `rand % 3`, gated on `((rand >> 13) & 0xf) == 1` — drawn from whichever island is **nearest that
frame**. That, rather than any accumulation of themes, is what the attract path makes audible.

**OpenTPW deviates here, deliberately and at Alexah's word (2026-09-20), and the deviation is larger
than a fade.** While its attract camera is flying, **all four parks sound at once**, each positioned at
its own island — the marked emitter node where there is one, the island itself where there is not — so
the blend between parks is **distance**, not a cross-fade. With somebody playing it collapses to the
single island on show, flat or at its node, exactly as before.

The engine end of that is the original's own: `Sound_PlayEffect( handle, category, effect, x, y, z )`
really is positional, and the game delay-loads QMixer (QSound) for it. What the original never did was
spend any of it in the **lobby**, which is why this is an improvement rather than a restoration.

Measured by disk capture over 60 s of flying (`~/.cache/tpw-harnesses/lobbyaudio.py`), on the build
that shipped: **`sounding=4` on 17 of 20 readings** — the three dips to 3 are a voice inside its
effect's repeat delay, visible as `bed=-` or `theme=-` in the same line — with **peak 0.2499
(−12.0 dBFS)** and **rms 0.0280 (−31.0 dBFS)**, and **zero `placed=flat` while flying**. Four parks
summing therefore does **not** clip: the levels calibrated for one park still stand, against a
documented worst case of 1.18 before master volume. A muted control measured exactly 0.0, which is what
proves the capture is the game's own mix. See `LobbyAudio.KeepPlaying`.

### Not sound: `FUN_005d83f0` / `FUN_005d8440`

They call `FUN_004732a0` / `FUN_00473f50` over the handle table at `DAT_007a4610` — the **animation**
player, not audio. The tail loop of `FUN_005e0470` walks every island and, where its animation is not
playing, starts a random one of two clips. That is the ISLE's clips 0/1, and it is the only place the
lobby sequences an island's animation.

## Park names and the locale tables

The four park display names **are** in the shipped data, measured with OpenTPW's own
`StringFile`/`BFSTReader` against a real install rather than a hand-rolled parser.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `data/Language/English/THEMENAMES.str` | — | 112 bytes, magic `BFST`, header declaring **4 entries**, decoding to `[0] "Lost Kingdom"`, `[1] "Halloween World"`, `[2] "Wonder Land"`, `[3] "Space Zone"` | Read through `StringFile` |
| `01 <len> 00 00` + `len` bytes | — | A BFST record. **The bytes are character INDICES into `MBToUni.dat`, not text** — which is why `strings`, `grep -a` and any plain text search find nothing but the magic | Byte-level read |
| lengths 12 / 15 / 11 / 10 | — | The four name lengths, visible in the header table, matching the four names exactly, with byte `0x0c` (the space) in the right place in each | Header table |
| `English/MBToUni.dat` | — | 506 bytes, count byte `0xf9` = **249** characters | Byte read |
| `american/MBToUni.dat` | — | 504 bytes, count byte `0xf8` = **248**. They differ at byte 7, the count field read after `Seek(6)` — one fewer character shifts every index past it | Byte read |

A code comment in the tree claims the displayed names are not in the shipped data at all, having
searched all 312 WADs, the loose language files and the executable for them. **That comment is
wrong**, for the index-not-text reason above; when touching the lobby island names, the front end's
park name, or anything localisation-shaped, read `THEMENAMES.str` and delete or correct the comment,
because left standing it will convince the next reader not to look.

**`BFSTReader` can only decode English.** Its lookup table is a **static** field initialised to
`Language/English/MBToUni.dat` — fixed for the life of the process and hardcoded to English.
Decoding the other shipped language through it shifts every letter by one:

    american/THEMENAMES.str -> "Mptu Ljohepn"  "Ibmmpxffo Xpsme"  "Xpoefs Mboe"  "Tqbdf [pof"

Two languages ship (English, american), **21 `.str` files each**. The localisation path also
hardcodes `Language/English/UITEXT.str` and `UIHELPTEXT.str`. Making the lookup table per-language
and non-static is the other half of the fix.

**How to redo the measurement** (the harness was scratchpad-only and is gone; the technique is the
durable part): make a throwaway console project referencing `OpenTPW.Files.dll` and
`OpenTPW.Common.dll` out of the build output, add an `AssemblyResolve` handler pointing at that same
folder, then set the two statics the readers need before touching them —
`OpenTPW.Common.GlobalNamespace.Log = new Logger()` and
`.FileSystem = new BaseFileSystem( "<install>/data" )` — and construct
`new StringFile( "Language/English/THEMENAMES.str" )`, reading `.Entries`. **The file system must be
mounted first even if `StringFile` is handed a raw `Stream`**, because the reader resolves its lookup
table in a *static* field initialiser; without it the probe dies in a type initialiser and looks
like "the file is unreadable".
