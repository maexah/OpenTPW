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
| `0x0065fd58` | `UI_ParseTreeStream` | Walks a layout stream. Op `0` = control type/flags/id/rect, `1` = mesh by hash, `3` = text rect, `0x11` = UIHELPTEXT row, `5` = end | Ghidra `run_python` dumper over every call site |
| `0x0065fd0e` | `UI_LoadTree` | Loads a tree | Call sites of the stream walker |
| `0x0047ed80` | `UI_LoadModalTree` | Loads a tree as **modal**; adds a LOLIGHT dimmer | As above |
| `0x753c68` | — | Tree: player slots | Dumped call site |
| `0x753f50` | — | Tree: new player dialog | Dumped call site |
| `0x74f920` | — | Tree: message box | Dumped call site |
| `0x757f60` | — | Tree: island panel | Dumped call site |
| `0x7581a0` | — | Tree: the **online globe's hover readout** — two labels, a name and "Parks: N" (UITEXT 462), both blanked on null, created at lobby start beside the globe panel | Dumped call site; the labels' own text ids |
| `0x7596b0` | — | Tree: online globe panel, 14 controls. Loaded on the lobby path, unbuilt | Dumped call site |
| `0x774c18` | — | Tree: the lobby's **root control** `0xbf431`, full screen, callback `0x005d58b0`, given the focus by `FrontEnd_Init` - every key and press the lobby takes goes through it ("The lobby's keys act on the release"). Its one child is the mail badge `0xbf432`, mesh `i_mail`, unbuilt | Dumped call site; stream re-read |
| `0x1e0ec` / `0x1e0ed` / `0x1e0ee` | — | The player's golden-key count, part of the **island panel** | Control ids in the island panel's stream |
| `h = (c ^ h) * 47` | — | Mesh-name hash, over the node name of the model's **first** mesh — e.g. `wdialogw`, `islandlob` | Reproduced against every ui.wad mesh name |

`0x7581a0` is **not** a key count, and no second key counter should be built from it. The lobby has
**at least seven trees, not five**. The two unbuilt ones, the globe's readout and panel, belong to the online world,
which is a permanent dead end here; so may the root's unbuilt mail badge (see the Unsettled list under "The lobby's
keys act on the release").

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
| `0x005afc30` | PlayerProgress_AddKey | Adds the golden key. A four-byte body, so a thunk or stub onto the real routine. Called by `FrontEnd_ClosePlayerSlots`, and only in Full Simulation | Ghidra body `005afc30-005afc33`. A separate function from `FrontEnd_ClosePlayerSlots`, which calls it |
| `0x004b9340` | IslandPanel_Refresh | Refreshes the island panel | Disassembly |
| `0x004b9840` | — | Hides the price (`0x1e0ea`) and the held key count (`0x1e0ec`) and **disables** both island arrows (`0x1e0f0`, `0x1e0f1`) — all four together, while the mode is 2 | Disassembly |
| `0x0065da8d` | — | The enable/disable setter: puts flag **`0x2`** on the control at `+0x48` | Disassembly |
| `0x00668820` | — | Part picker: draws **part 1**, the disabled part, for a control carrying `0x2` | Disassembly |
| `0x0065d9dd` | UI_SetVisible | The hide. **Never a vtable entry** — so a virtual call at slot `+0x14` is always the enable, never the hide. That is how the two operations tell apart in a decompile | Vtable scan |
| `0x005e1ee0`, `0x005e1f40` | — | The handlers behind the island arrows, **next** and **previous**. Both return having done nothing while the camera is leaving for a park (`[this+0x14]` non-zero, their first test), and unless the type is something other than 2 — see "The island keys wait for the fly-in" | Disassembly |
| `0x005e1cc0` | `IslandLobby_EnterPark` | Enter this park. In Instant Action it goes straight in without counting keys | Disassembly |
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
| `0x00485780` | `UI_MessageHook_ClickSound` | UI_Init's click hook, which plays the above | Disassembly |
| `0x005e1e30` | `IslandLobby_LeaveForPark` | Enter park, affordable: panel hidden, globallobbysfx effect 4, particle 98 - neither for an Instant Action player (`0x004b8fd0` returns on game type 2, `0x004b9020`). Locked, Enter this park plays nothing itself; a click on its button still plays the UI click through the hook | Disassembly |
| `0x005e1bd0` | `IslandLobby_ViewOnlineWorld` | The island camera's `+0x20` deactivate, which the panel's View button (`0x1e0e9`) calls before the online-world child's `+0x1c`. Whether a connection test stands in front, and what the child shows offline, is not decoded (`docs/QUEUE.md` Q62) | Disassembly |

Measured lobby mix: the goldkey click read back a gain of 0.090 = 0.484 x master 0.5 x duck 0.38.

## Particle system

Effects come from `Data\Particle\Tp2.plb` (`.plb` = 320-byte effect records and 104-byte effector
records), with sprites out of `esprites.wad` (`.ESP` banks, `.TPC`/`.FPC` packs, row RLE over a
BGRA palette).

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0051f370` | `Particles_LoadPlb` | Loads `Tp2.plb`; its `PTCL:` strings name it | Strings + disassembly |
| `FUN_00521e60` | — | Spawns a particle effect | Disassembly |
| `0x0051ef30` | Particles_Render | Draws the screen particles. **The only caller of Sprites_LookUp** | `get_xrefs_to` |
| `0x0057c620` | SpriteBatch_DrawParticles | The batch draw, where the screen mapping is applied | Disassembly |
| `0x005423a0` | Sprites_LookUp | Sprite set lookup, keyed `bank<<4 \| set` | Disassembly |
| `0x00540d90` | SpriteBank_Load | Picks between a `.TPC` and the `.FPC` beside it from the ESP byte at `0x10C`. The selecting constants **read back EMPTY in Ghidra and are NOT identified** | Disassembly; attempted constant read |
| `0x00582170` | — | Sprites are flushed here, after all UI models, at depth 0, LESSEQUAL, with no depth write | Disassembly |
| `+0x14` | — | A particle's position within its record | Field read in the step routine |
| `0x0078d90e` | — | The glint gate byte = **Popup Help** (options case `0x1d4c3`, label UIStrings 327) | Options screen wiring |
| `0x005ed920` | `UIParticles_ButtonGlintStart` | Spawns the button hover glints (effect 35) when that option is on | Disassembly |
| `0x007858c8` | — | Particle density global. **Only ever read**; the options start on medium (`0x00423690` writes quality 1 at `0x0042369f`), and how PARTICLEDENSITY from that preset's `.sam` reaches this global is not traced | `get_xrefs_to`; `med.sam` has `GameOptions.PARTICLEDENSITY 1000` |

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

Say so if asked: particle density comes from the detail file for the options' graphics quality (`low.sam`,
`med.sam` or `high.sam`, read as a level loads), and that quality starts at medium, as `GameOptions_Construct`
(`0x00423690`) sets it. On wide windows, effects pin like a control at their spawn x, spawned-by-effect
inherit the pin, and glints take the button's anchor. Handles are never 0 (the original's first
handle can be 0). Keys look near-white because the art is additive gold over a light sky.

### World sprites (decoded; the park's people are drawn, the rest is not built)

Drawing effects **in the world** is a whole subsystem, not a variant of the screen path — nothing in
it reaches `Sprites_LookUp`, whose only caller is `Particles_Render`.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `DAT_007b49f0` | — | Table of world sprite instances, **0x118 bytes** each | Disassembly |
| `FUN_00475a10` | — | Allocates an instance | Disassembly |
| `FUN_00475360` | — | Steps an instance | Disassembly |
| `FUN_00475010` | — | Runs the instance's program: fetches `*(code **)(base + pc * 4)` and calls it — opcode **function pointers**, which is why a person record carries `mSpriteScript` | Disassembly |
| `+0x88` / `+0x8c` / `+0x90` | — | World position, three floats, ten world units to a cell. `+0x8c` is an **offset above the ground**, not a height. (A thing's own `mX`/`mY` agrees to within a third of a unit and is still the better source, being what the park saved) | Field read |
| `+0xac` / `+0xb0` | — | Sprite kind and bank | Field reads |
| `+0xb4` | — | **Not** a variant: a packed bank offset (high bits) and set (low four bits) | Field read |
| `TPCS` | — | The save block world sprites persist in | Save reader |
| `0x40` | — | Visibility byte on the world path | Disassembly |

OpenTPW draws the park's people this way (`ParkGuestSprites`, reading the save's `TPCS` table) and runs the four
sprite-script ops their scripts use (`SpriteScript`). Litter, balloons, thought bubbles and the other fourteen ops
are not built.

The advisor's clip glints are not world sprites and not the lobby's: they are UI particle channel 0 (effects 49 and 44),
started by clip-word flags that only gesture rows 1 and 13 carry (`scenes.md`, "Gesture table"), and only the park
advisor's golden-ticket lines use those rows. Nothing here awards a golden ticket, so nothing reaches them, and they have
no counter.
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

The gate and the lights are **scripted things in the original** — the archives ship `Gates.RSE` and `lights.RSE` beside the
models — so their movement belongs to an animation player that a script triggers.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| thing ids 11, 12 | `mParkGates`, `mTrafficLights` | The fixed items **do** carry thing ids, named by the save header itself | Save header field names |
| `FUN_005156a0` | — | Finds the two **by name** among the item descriptions and builds each through the ordinary object constructor — the engine's own arrangement | Disassembly |
| `FUN_00519ef0` | — | Opening or closing the park. **The only thing that ever writes `VAR_COMMAND`**, which `Gates.RSE` idles on | `get_xrefs_to` on the variable write |
| `VAR_COMMAND` 1 / 2 / 0 | — | What `FUN_00519ef0` writes to the gate's script (`mParkClosed` itself is 0 for an open park). **Opening the park writes 1.** Closing it writes **0**, and only when nobody is in the park (`FUN_004c9130`) and the gate's `VAR_STATUS` reads 1 (`0x0051a0e8`..`0x0051a161`). **2** is written only on the second-argument path, whose one caller is the end-of-park routine `FUN_005168f0` (`0x00516ada`). Read off the **disassembly** (`0x00519f40`, `0x00519fb3`, `0x0051a155`): the decompiler hangs each value on `FUN_0055a070` instead of on the `FUN_0055a0b0` write, though the values themselves read true. What `Gates.RSE` does with 0 against 2 is not decoded | Disassembly, against the decompile |

The command variable is resolved **by the name the script itself declares**, and a write that lands
nowhere is **reported as a miss** — because a command that goes nowhere looks exactly like a gate
nobody commanded. A park saved open now draws open.

**The traffic lights are bound and provably inert:** `lights.RSE` loops unconditionally, but both
clips it loops carry **zero tracks**, so nothing on screen can move. That is why the old
suffix-matching loop was only ever visible on the gate. **Do not read the silent crossing as a
posing bug.**

### The lobby island gate — driven by the park-entry flight

The gate idles shut, opens **once** as the park-entry flight's camera finishes swinging round onto it, and
shuts again if Escape cancels the flight after that. Both are the original's: the island's gate is a model
instance of its own (`island+8`), and the camera's state 1 plays its M1 on arrival (`0x005e06e4`) while the
cancel plays its M2 (`0x005e18ab`). See "Escape cancels the fly-in, and the gate is the flight's" below.

`IslandLobby_LeaveForPark` itself touches no gate: its three steps are to set the camera leaving,
`IslandPanel_KeyPuffAndEnterSound`, and message **6** with 0 to the panel tree (`0x004b8ec0`), which hides the panel
rather than destroying it. The clip is played
later, by the camera, when the homing arrives.

**Not every gate is a rotation animation.** Measured across all four, not inferred from the jungle's
hinged pair, which cannot show that a gate may open without turning:

| gate | M1 carries | keys cover | declares | movement ends | how it opens |
|---|---|---|---|---|---|
| `Jun_gate` | rotation 2 | 0–60 | 0–60 | 57 | two doors on hinges |
| `Hal_gate` | rotation 2 (3 clips) | 0–600 | 0–60 | 60 | two rails; the keys run 10× past the span |
| `Spa_gate` | rotation 1 | 0–100 | 0–100 | 60 | the hatch |
| `Fan_gate` | **morph 2, rotation 0** | 0–100 | 0–100 | — | the worm; it gets no `MeshRotator` at all |

Every gate's M2 is its M1 played backwards. The engine plays the span a clip **declares**, so each gate clip
lasts two seconds for the jungle and hallow and 3.33 for fantasy and space, and the hallow keys past frame 60
are never reached.

The rest of the lobby's clips are still a stand-in: the Dino's play on a loop because nothing sequences them yet (the
original's isle clips are picked by the camera update's tail loop; see "Not sound"). The butterflies' loop is the
original's: each flyer ships one clip, which its constructor loops (`0x005d986b`).

## The lobby camera has two modes, and both are built

`FUN_005e0470` is the whole lobby camera update. It branches at the top on whether anybody is
playing: **no islands, or no player selected** (`FUN_0048bcd0()` → `+0x60 == -1`), **or no current
island** (`[3] == 0`) takes the **attract** path; otherwise the **globe** state machine runs on `[5]`.

OpenTPW builds both, branching on the same condition in `LobbyCameraMode.Update`: attract is `Attract`, and
the island state machine is the orbit (state 0), `LeaveForPark` and `StepLeaving` (states 1 and 2, with the
gate's M1 on arrival) and `CancelLeave` (`0x005e1890`), all in front of the shared `FUN_005e1210` ease. Not
built: state 2's darkening of the screen (`LOBBY_FLY_IN_FADE`, `docs/QUEUE.md` Q61).

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

Per frame, with the lobby's delta (`0.01 × ms`, ten units a second — see `LobbyScript.TicksPerSecond`):

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
| 1 | Homes the angle onto the island's own heading `island[+0x14] + π`, shortest way round, at `0.05 × delta`; on arrival plays the gate's M1 once (`FUN_005d83f0(0,0)` on `island+8`, `0x005e06e4`) and goes to 2 |
| 2 | Locks that heading, then decays radius `[8]` at **0.07** and vertical `[9]` at **0.6** per delta — the `GLOBERADIUSOUT`→`GLOBERADIUSIN` pull-in — and when the radius falls below **8.0** calls vtable `+0x48`. It also darkens the screen as the radius closes (see "Escape cancels the fly-in") |

`+0x48` is `__amsg_exit(0x19)` — MSVC's **pure virtual** stub — in this vtable, so the running object
is a derived class that overrides it.

### The derived vtable, found 2026-09-22 — and it is the park-entry animation

**The derived vtable is at `0x00702ec0`**, and it was
located by searching for the pointer bytes of `IslandLobby_EnterPark` rather than by scanning for the
base's update. Its slots:

| Slot | Address | What it is |
|---|---|---|
| `+0x08` | `FUN_005e1830` | The island lobby's **own update** — and its first instruction is `CALL 0x005e0470`, so the base camera update really does run here |
| `+0x38` / `+0x3c` | `0x005e1ee0` / `0x005e1f40` | The island arrow handlers, overriding the base's next (`0x005e1640`) and previous (`0x005e1730`) island |
| `+0x40` | `IslandLobby_EnterPark` `0x005e1cc0` | Checks `[5] == 0` and the key count, then calls `+0x44` |
| `+0x44` | `IslandLobby_LeaveForPark` `0x005e1e30` | Sets `+0x14` to **1**, key puff and sound, hides the panel |
| `+0x48` | `FUN_005e1e50` | The override of the pure-virtual slot above |
| `+0x4c` | `0x005e2750` | SetIsland: writes `[3]` and puts `+0x14` back to **0**. The table's last slot — `0x00702f10` is a list class's own vtable (stored by `FUN_005e4020`, `FUN_005e40a0`, `FUN_005e4320`), so a `+0x58` read past `+0x4c` is that class's |

**`+0x14` is `param_1[5]`** — the update takes `int *`, so `param_1[5]` is byte offset `0x14`. So
`IslandLobby_LeaveForPark` does not merely hide the panel: **it puts the camera into globe state 1**,
and the whole sequence above is what happens next. The park is asked for when the **camera** arrives:

1. `+0x40` → `+0x44`, which sets state **1** and hides the panel;
2. state 1 turns the orbit onto `island[+0x14] + π` at `0.05 × delta`, arriving within half a step;
3. state 2 locks it and decays radius at **0.07** and vertical at **0.6** per delta;
4. below radius **8.0** it calls `+0x48` = `FUN_005e1e50`, which ends `MOV [EAX+0x14], 2` on the scene
   object at `DAT_00f82884`;
5. `FUN_005d5cf0`, the state-3 teardown, **returns that field** — the documented "choice 2 means play
   a park".

From SPINRADIUS 70 at 0.7 a second that is `ln(70/8) / 0.7` ≈ **3.1 s** of flying in, with the vertical
offset collapsing about ten times faster. **So the original is not blank between Enter and the loading
screen** — it swings the camera round onto the gate side, opens the gate, and flies it into the island.
`FUN_005d83f0`, called on the 1 → 2 transition, is `__thiscall` on the island's **gate** instance (`island+8`)
and plays its M1 once; the tail loop's random pair is the isle's, `island+4`.

### The island keys wait for the fly-in, and the fly-in dies with the lobby

Decoded 2026-09-23 for `docs/QUEUE.md` Q8, every claim put to two refuters.

**Every way the player asks for another island ends in the two handlers, and both refuse while the camera
is leaving.** `0x005e1ee0` (`+0x38`, **next**) and `0x005e1f40` (`+0x3c`, **previous**) are identical but
for the mover they call. Each opens `MOV EAX,[ESI+0x14]` / `TEST` / `JNZ` to its `RET` (`0x005e1ee3`,
`0x005e1f43`) before anything else — before the one-time guard that builds the game-type object at
`0x00fb3b7c` (`0x00550ca0`) and before the Instant Action test. The movers, `0x005e1640` and `0x005e1730`,
test `+0x14` again, and change the island only through `+0x4c`. The routes in:

| Route | How it reaches the handlers |
|---|---|
| The panel's arrows | `IslandPanel_Callback`, message `0x100`: `0x1e0f1` (right) calls `+0x38`, `0x1e0f0` (left) calls `+0x3c`, both on `[[0x00f85614]+4]` |
| Cursor Right / Left | On key **up** (`0x1000b`), as `0x2700` / `0x2500` (an extended key's byte shifted by 8): `IslandLobby_OnKey` hands every key to each active child's `+0x14`, and the camera's `0x005e2310` maps them to `+0x38` / `+0x3c`. It also maps Enter (`0xd`) and a left button down (`0x10005`) to `+0x40` |

**There are no bracket keys.** No `[` or `]` (nor `0x5b`, `0x5d`, `0xdb`, `0xdd`) is compared anywhere in
the lobby's code, and no key-binding table has an island row. OpenTPW's `[` and `]` are its own.

**No other route was found that moves the island mid-flight** — a route search, not a proof. The update
writes `[3]` only in the attract branch, which runs only with no islands, no player or no current island.
`0x005e1fa0` writes it ungated, but its one caller is `FrontEnd_ClosePlayerSlots`, whose slots open from the
game menu — which Escape cannot open during a leave (below) — and from `FrontEnd_Init` (`0x005d5bec`) when
nobody is playing. Enter cannot start a leave while those first slots are up: they hide the lobby's root and take the
focus, and their callback drops every key ("The lobby's keys act on the release", below).

**Escape during a leave cancels it**, and so the game menu cannot open over a flight. The route and the
cancel are the next section's.

**The leave state lives and dies with the camera.** The island camera is a heap object (0xe8 bytes, built
by `g_FrontEnd`'s `+4`, `0x005e3dc0`, in `FrontEnd_Init`, which state 1 calls on every entry to the lobby)
and the state-3 teardown deletes it (`0x005d5cf0` → `0x005e4140`). Its six writers of `+0x14`: the
constructor (0, `0x005dfd2d`), `IslandLobby_Start` (0, `0x005e199f`, with `[3]` and `+0x10`),
`IslandLobby_LeaveForPark` (1), the update (1 → 2, `0x005e06e9`), the Escape cancel (0), and SetIsland (0,
`0x005e275b`). Nothing writes it on arrival: state 2 calls `+0x48` once the radius is below 8 — once in
practice, since that call sets the scene's choice and the lobby loop leaves on the same pass (`0x0054e7a2`).

**The park is chosen at arrival, not at Enter.** `FUN_005e1e50` reads the current island `[3]` when it
runs: it assigns the island's name (`+0x30`) to the static string at `0x00f85480` — which the next
`IslandLobby_Start` uses to put the lobby back on that island, and `0x005e1fa0( 1 )` empties — selects the
level entry by that name (`0x00409480` on `0x786b68`), and sets the scene's choice to 2.
`IslandLobby_EnterPark` stores nothing about the park.

**OpenTPW.** `LobbyCameraMode.Step` is the pair of handlers: it refuses while leaving (logging `Lobby
camera: staying on island N`) and while held to one island, and the panel's arrows, its cursor keys and
the bracket keys all ask through it. `ForgetIsland`, part of the lobby's unload, clears the whole leave.
Confirmed in the game with a real `]` mid-flight. Which park is fixed at Enter in a closure, where the
original reads it at arrival; nothing a player can reach moves the island in flight, so the two cannot be
told apart. The cursor keys act on their release, as the original's do ("The lobby's keys act on the release",
below); the bracket keys, OpenTPW's own, on the press.

### Escape cancels the fly-in, and the gate is the flight's

Decoded 2026-09-23 for `docs/QUEUE.md` Q41: four decoders, each put to a refuter, every claim re-read in disassembly.

**The route.** The lobby acts on Escape's **release** (`0x1000b`, key `0x1b`), never its press. The UI gives a key to one
control only (`0x006698e6`): an accelerator's target, else the focus if it is visible, else the last control any
press reached. Escape is never an accelerator, since the one table (`0x0077c4b8`) holds only End, Home, Up and Down. So with the game menu open the key is the
menu's (`0x0048bd40` closes it; `MenuList_Show` took the focus, `0x00493197`), and with a message box open it is the
box's (`UI_LoadModalTree` takes the focus, `0x0047ee67`), and neither reaches the lobby. Otherwise the lobby root's
callback forwards it to `IslandLobby_OnKey` (`0x005e41c0`, g_FrontEnd's `+0x14`), which asks **every** active child's
`+0x18` - no early exit, the answers ORed (`0x005e41d8`-`0x005e4201`) - and opens the game menu (`GameMenu_Open( 1 )`)
only if none answered. In the island lobby the active children are the island camera and the advisor queue
(`FrontEnd_Init`, `0x005d5b81`-`0x005d5c12`), and the queue's `+0x18` answers 0, so the camera decides.

**The cancel, `0x005e1890`.** In state 1 or 2 it puts `+0x14` to 0 and returns 1; from state 2 it first plays clip 1 on
`[[camera+0xc]+8]` (`0x005d83f0( 1, 0 )`, `0x005e18ab`). In every state, 0 included, it then shows the island panel
(`0x004b8ea0`: message 6 with 1 to the panel tree, then the mail badge rule `0x004bbbd0`) and refreshes the panel's
price and name for the island on show (`0x004b90f0`). In state 0 the panel is already visible, `UI_SetVisible` returns
at once (`0x0065da13`) and it returns 0, so a plain Escape changes nothing and the menu opens.

**What the show does.** The panel was hidden, so `UI_SetVisible( 1 )` sends `0x11` with 1 to `IslandPanel_Callback`,
which runs `IslandPanel_Refresh` (`0x004b9340`): the key count is looked at again and the key sparkle (effect 97)
started if the park is affordable - the leave's hide sent `0x11` with 0, which killed it (`0x004b8fa5`). `0x004b90f0`
then spawns the sparkle only if none is running, so there is one.

**What the cancel leaves alone, and why nothing more is needed.**
- The orbit angle is **one field**, `+0x18`: state 0 advances it, state 1 homes that same field onto `island[+0x14] + pi`
  (`0x005e06be`, `0x005e06cf`), and state 2 writes it every update (`0x005e04d5`). Nothing saves an angle at Enter, so
  the orbit carries on from wherever the leave had turned it - the gate side, from state 2.
- States 0 and 1 copy SPINSPEED, SPINRADIUS and VERTICALOFFSET into `+0x1c`, `+0x20` and `+0x24` every update
  (`0x005e06fd`-`0x005e0724`), so the wanted point jumps straight back out and the body eases out to it at `0.1 x delta`
  (`FUN_005e1210`); the aim was never moved.
- Entering spends nothing: `IslandLobby_LeaveForPark` writes only `+0x14`, and `IslandLobby_EnterPark` only compares
  `PlayerProgress_CountKeys` (`floor( T / 3 ) + [+0x20]`, `0x005af680`, which writes nothing) against the cost. The cue
  (`Sound_PlayEffect( 0, [0x00803a48], 4, ... )`) and the puff (effect 98) keep no handle, and nothing stops them.
- So Enter this park works again at once (`0x005e1ce0` tests `+0x14 == 0` first).

**The gate is the flight's, and the original does animate it.** An island (`FUN_005dfa60`, 0x74 bytes, one per
`ISLAND(` line of each park's lobby script) holds two model instances built by `0x005d8870`: its **isle** at `+4` and its
**gate** at `+8` - the line's third and fourth names. Both are built with flags `0xc0` and differ only in how many
animation channels they get, two for the isle and one for the gate: that argument reaches the role loader
`FUN_00461f10` as its fifth, which sizes the model record as `0xfc + n x 0x38` (`0x00462520`), `0x38` being a channel.
Every instance has an animation handle (`0x00463060`, `0x005d898c`), so both play. `0x005d83f0( entry, flags )` plays role-M entry `entry` (M1, M2, ...)
on an instance through `0x004732a0( model, 5, entry, flags, 1.0, channel 0 )`; flags 0 plays it once, 1 loops it, and
1.0 is the speed. It has exactly seven callers:

| Caller | On | Plays |
|---|---|---|
| `0x005e06e4`, state 1's arrival | the gate, `+8` | M1 once - **the gate opens as the camera faces it** |
| `0x005e18ab`, the island camera's cancel | the gate | M2 once - **it shuts again** |
| `0x005e11f7`, the update's tail loop | the isle, `+4` | M1 or M2 at random, once, whenever it is idle - not built (Q76), counted `LOBBY_ISLE_RANDOM_CLIP` |
| `0x005e136a`, `0x005e237a`, `0x005e2683` | the gate | M2 once - the same cancel in the base camera and in a sibling class (`0x00702dd0`, not identified) |
| `0x005d986b` | a flyer, in its constructor `FUN_005d9830` | M1 looped |

Measured in `lobby.wad`, every gate's M2 is its M1 backwards. A clip asked for while channel 0 is part-way through one is
**queued** (one deferred clip, `0x0047334a`) and starts when that one ends; it starts at once when the channel is idle,
finished or held at its end (`0x00473315`-`0x00473326`). The engine plays the span a clip **declares**, not the one its
keys cover: 60 frames for the jungle's and hallow's gate clips (hallow's keys run to 600), 100 for fantasy's and space's,
at 30 a second. So an Escape less than two seconds into the fly-in finds the jungle's gate still opening, and it opens
fully before it shuts.

**And the fly-in darkens the screen.** State 2 writes a target to the render camera's `+0x60` (`0x00f83778`, built in
`FrontEnd_Init`): `clamp( (1 - (r - 8) / (SPINRADIUS - 8)) x 65536, 0, 65535 )` each update while the radius is 8 or more
(`0x005e052f`-`0x005e05b4`), and `0x10000` just before `+0x48`. States 0 and 1 and the attract flight write 0. The render
camera's update (`0x005d8cb0`) eases `+0x5c` toward it by an eighth of the gap per lobby pass (`SAR 3`, not scaled by
the delta), and while it is above 0 sets bit `0x80000` of `0x008bcbc8` and writes a black colour with alpha
`min( +0x5c >> 8, 255 )` to `0x008bd4e4`, which `FUN_00576a00` hands to the renderer's `+0x28` with the screen's size -
a black rectangle over the view. The cancel does not touch it; the next state-0 update sets the target to 0 and it lifts
over about 42 passes. **Not built** (`LOBBY_FLY_IN_FADE`, `docs/QUEUE.md` Q61).

**Unsettled.**
- Whether the engine's idle default replays a lobby gate's M1 by itself. `FUN_0044e410( 3 )` in the lobby loop calls
  `FUN_00473c70( model, 0, 8 )` on every model with `[+0x14] != 0` and `!(+4 & 0x3000)`, which replays role 5 entry 0
  when channel 0 has finished, unless `(model+4 & 0x8004) != 0`. The lobby instances' `+4` was not established
  (`docs/QUEUE.md` Q63).
- Whether a lobby channel holds a once-clip's last frame (the end-of-clip rule reads `model+4 & 0x18`).
- Which lobby mode the sibling camera class `0x00702dd0` serves. It runs the same state machine (`0x005e22d0` calls
  `0x005e0470` first) and its Enter opens a confirm dialog first (`0x005e26a0`, island `+0x70`).
- Nothing here was observed in the running original.

**OpenTPW.** `FrontEnd.MenuKey` leaves Escape to the game menu and to a modal window as the focus does, then asks
`LobbyCameraMode.CancelLeave`, and opens the menu only if it did not answer; a cancel opens the island panel again,
whose `Shown` looks at the keys. `CancelLeave` shuts the gate from state 2, carries the orbit on from the leave's angle,
and clears the leave. The gate's M1 is played from `StepLeaving`'s arrival, not at Enter, and `LobbyGate` plays each clip
once over its declared span, holds its last frame, and keeps one clip queued; the holding, and idling shut
between the two calls, are our reading while the Unsettled points above are open. `IslandPanel.EnterPark` tests the camera's
state, as `0x005e1ce0` does. Escape acts on its release, as every key the lobby takes does (next section).

### The lobby's keys act on the release, and a press on the view enters the park

Decoded 2026-09-23 for `docs/QUEUE.md` Q42: four decoders, each put to a refuter; the press route, the root's stream and
the polygon test re-read by hand.

**Every key the island lobby takes acts on its release, and none on its press.** The window procedure `FUN_0046b600`
posts every WM_KEYDOWN and WM_SYSKEYDOWN, auto-repeats included, as UI message `0x1000a` (`FUN_00658c38`), and every
WM_KEYUP and WM_SYSKEYUP as `0x1000b` (`UI_PostKey`, `0x00658c67`) - one per release. The executable holds one `PUSH` of
each and synthesises neither. None of the lobby's readers reads `0x1000a`: the island camera's `0x005e2310`,
`IslandLobby_OnKey` (`0x005e41c0`), the root's `0x005d5dd0`, `IslandPanel_Callback`, the slots' and the dialog's callbacks
and the advisor queue (`RET 0xc`). So a held key does nothing until it is let go, and then once.

**The key code** is built the same way at both edges. The keypad's `+ - * / .` give `vk << 8`. Any other key gives
`MapVirtualKeyA( vk, 2 )` when that is non-zero, else the vk, and an extended key (lParam bit 24) becomes
`(code & 0xff) << 8`. So the main Enter is `0x0d` and the keypad's `0x0d00`; the cursor keys are `0x2700` and `0x2500`,
the keypad's arrows `0x27` and `0x25` with NumLock off. Only the main Enter and the cursor keys match the camera. The
modifiers ride in the flags word, and nothing in the lobby looks at them.

**The route is the lobby's root control.** `FrontEnd_Init` loads stream `0x774c18` with callback `0x005d58b0` and gives it
the focus (`0x005d59b7`). The stream is one full-screen type-1 control, id `0xbf431`, rect (0,0,2048,1536), flags 1, with
the mail badge `0xbf432` its only child. The UI pop `0x006698e6` hands a key to one control: an accelerator's target (the
one table, `0x0077c4b8`, is a list's End, Home, Up and Down), else the focus if it is visible, else the last control any
press reached, if visible. A control that does not handle a key drops it; the base proc `0x0065f6d1` passes nothing to a
parent. The root's callback hands every message to `0x005d5dd0`, which hands it to each registered object's `+0x14`. Only
g_FrontEnd's, `IslandLobby_OnKey`, does anything: after its Escape (the previous section) it hands the message to each
active child's `+0x14`, and the island camera's `0x005e2310` takes three keys. Enter (`0x0d`) is Enter this park
(`+0x40`), `0x2700` the next island (`+0x38`) and `0x2500` the previous (`+0x3c`).

**Who has the keys.**

| While | The keys go to | So |
|---|---|---|
| a player is in the lobby, the panel up | the root, which holds the focus. A click on a panel button makes the button the last pressed but leaves the focus alone | Enter, Left, Right and Escape act |
| the fly-in | the root. The panel is hidden (`0x004b8ec0`, message 6) | Escape cancels; Enter and the arrows reach the camera, which refuses on `+0x14` |
| the game menu | the menu (`MenuList_Show`, `0x00493197`). Escape's release closes it (`0x0048c095`), as does entry 0 of the shortcuts table, which is Escape too; other keys are dropped | on closing, `0x004862a0` gives the root the focus back |
| a message box | the box's root (`UI_LoadModalTree`, `0x0047ee67`), whose callback drops every key | only its buttons close it |
| the options screen | nothing: the root keeps the focus but is hidden (message 6, `0x004a3ae0`), so the pop skips it, and the last-pressed control drops the key | |
| the player slots, nobody playing | the slots: `FrontEnd_ShowPlayerSlots` hides the root (`0x004a65a8`) and gives the slots' root the focus (`0x004a65d6`), and its callback `0x004a5fc0` drops keys | **no key acts, and Escape opens no menu** |
| the new player dialog | its name box (`0x004a6f8e`), which edits on the press and takes Enter (the main one) and Escape on the release (`0x00667fee`), sending `0x802` or `0x804` to the dialog synchronously | the tick's whole close runs inside that one key-up's delivery, so its release never reaches the root |

**A left press on the lobby's view enters the park, on the press.** WM_LBUTTONDOWN posts `0x10005`, with the button (0
left, 1 right, 2 middle) as its first argument, to the control the pointer is over (`FUN_00658af1`, the hover
`[0x00faa5e0]`). There is no hit test at the press: the hover is worked out on a move, and by `FUN_006589f9`, which
hit-tests at the last pointer position unless a control holds the capture `[0x00faa5e8]`. Its ten callers are a control
built visible (`0x0065bff4`), destroyed (`0x0065c267`), shown or hidden (`UI_SetVisible`, `0x0065da61`), linked
(`0x0065f51a`) or unlinked (`0x0065f5ae`), given a new rect (`0x0065cc93`, `0x0065cd86`), its skip flag set
(`0x0065db16`), the capture let go (`0x0065e589`) and the focus killed (`0x0065e62c`). Off every
window it is the root `0xbf431`, whose callback hands the press on the same way, and `0x005e2310` answers button 0 with
Enter this park (`0x005e234f`); the right and middle buttons it ignores. The window class has no `CS_DBLCLKS` (style 0,
`RegisterClassA` at `0x0044e0de`), so a double click is two presses, and the second is refused because the first set
`+0x14`. The panel's button acts on the release of a click instead: the button class (`0x00668f9c`) posts `0x100` to the
panel on the left release, and only if the press was its own (`0x006690d6`), and `IslandPanel_Callback` calls the same
`+0x40` (`0x004b8bf8`). **With the game menu up no press reaches the root**: both scenes build the menu as a full-screen
panel (`MenuList_Create`, `0x00492ef0`; the lobby's call is `0x0048c655`) that `MenuList_Show` attaches last to the UI
root, so a press off its items lands on the panel (`park-engine.md`, "Whose a right press is").

**The island panel's outline.** A hit test walks down from the UI root, children first; a control flagged `0x2`
is skipped with everything under it, and a control answers for itself when its region holds the point - its rect, or a
polygon (stream op 4, sub-op 4). The island panel's root `0x1e0e7` has a 23-point polygon, the green L. A press inside it
and off the buttons is the panel's, and its callback hands it nowhere. A press in the corner of the panel's rect that the
L leaves bare, or on the price, the key count or the park's name (flags 3), reaches the root and enters the park. The
test is `0x0066c5a4`, the crossings count from Graphics Gems in whole virtual units: starting from the last vertex, a
vertex is "above" when its y is at least the point's, and each edge whose two ends differ flips the answer when
`(y1 - y) * (x0 - x1) >= (x1 - x) * (y0 - y1)` agrees with its new vertex's flag.

**Enter this park, `0x005e1cc0`,** tests in this order, each a silent return:
1. the camera already leaving (`+0x14`, `0x005e1ce2`);
2. no island (`[3]`, `0x005e1ced`);
3. an Instant Action player (`[0x00fb3b7c] == 2`) goes straight to `+0x44`;
4. the theme's record not loading;
5. `PlayerProgress_CountKeys` (`0x005af680`) - the keys the player holds now, not the count the panel last showed - less
   than `Keys.CostToEnter` (`0x005e1df0`).

Nothing is played or said for a park the player cannot afford, and an Instant Action player's leave plays neither the
puff nor the cue (`0x004b8fd0` returns on game type 2, `0x004b9020`). CountKeys loads each theme's `global.sam` on
demand (`0x005b11c0`) and writes nothing else.

**The system table's keys, in either scene.** The window procedure also matches the binding table at `[0x0078718c]` on
every key itself - `FUN_0040c900` on the press, which only latches, and `FUN_0040c990` on the release, which runs the
row's handler - and no other table; the game, camera, shortcuts, cheat and coaster tables are run only by a park's
controls. Its live rows are `P` (pause, `0x0040bf70`, which does nothing in the lobby), Ctrl+H (Popup Help,
`0x0040c5d0`), F8 (a screenshot, `0x0040c470`) and Ctrl+Shift+Alt+F8 (`0x0040c480`); boot rewrites the first two rows'
keys from text strings 0x14 and 0xf. `FrontEnd_ShowPlayerSlots` switches five of the six tables off, all but `coaster` (`0x0040cfa0`; `park-engine.md`,
"Keyboard bindings and the shortcut tables") and
`FrontEnd_ClosePlayerSlots` back on (`0x0040cf60`). A message box closed over the slots can switch them on early:
`MessageBox_Open` saves and restores the byte flag `[0x007c24d0]` (`0x0047f218`), which the slots never set.

**While the game's window is inactive the UI posts nothing at all**, keys or presses: `[0x0077c488]` is set as the window
comes active (WM_ACTIVATEAPP, `0x00659333` from `0x0046b6fc`) and cleared as it goes (`0x006593c5` from `0x0046b72e`), and
the posters test it first. Windows sends no key-up to a window that has lost the focus besides, so a key held through a
switch of window is never let go, as far as the lobby knows.

**Unsettled.**
- A press made after a window opens, before the pointer moves, can go to the old hover. Closing never leaves it stale:
  destroying, hiding and unlinking a control all refresh it. Opening can, narrowly: the factory `FUN_0065dd7b` links a
  control into its parent's list only after its constructor has refreshed the hover (`0x0065bff4`, for a control built
  visible), so the last visible control a tree builds is not the hover until the next refresh or move. Which of the
  lobby's opens end without one is not read whole: `NewPlayerDialog_Open` (`0x004a6e40`) sets the focus partway through
  (`FUN_0065e59b`, at `0x004a6f8e`), which does not refresh it, and none of the calls after it reaches the refresh. **OpenTPW differs, and does not count it**: `WindowStack.OnUpdate`
  hit-tests every frame, so a press here always goes to what the pointer is over, kept as a fix at Alexah's word
  (`docs/DECISIONS.md`; said at the site). Its code has no
  branch where the original's stale press would be, and the nearest stand-in, a press with the pointer unmoved since a
  window opened or closed, would count mostly presses the original also sends to the right control.
- Whether the mail badge `0xbf432` can show offline. `0x004bbbd0` hides it while `g_Players+0xc4`, a count, is 0; if it can
  show, a press on it is the badge's and does not enter the park.
- What a key or a press does in the online modes' children (`+0xc`, `+0x10`, and the sibling camera `0x00702dd0`, whose
  Enter opens a confirm dialog); theirs read the arrows on the press. Offline none is started.
- Nothing here was observed in the running original.

**OpenTPW.** `Input.KeysReleased` holds the keys let go in a frame. SDL lets go of every held key as the focus leaves, so a
frame in which the focus left is dropped whole, even if the focus came back within it, and so is every frame without it
(`Renderer.Update`, `Window.TakeFocusLeft`): a key-up is acted on only where the original's window would have had one. The stack hands the keys to the scene only when no box has the focus
(`WindowStack.KeysWithoutFocus`), so a key the name box took never reaches the lobby, and the box takes the first of
Enter (the main one) and Escape to come up. `FrontEnd.LobbyKeys` is the root: Escape goes to `MenuKey`, and Enter, Right
and Left to the camera while someone is playing and nothing but the island panel is in front. A left press the window
system sent (`Input.MouseInfo.LeftWentDown`) that no window takes goes to `WindowStack.ViewPressed`, which is
`FrontEnd.ViewPressed`: Enter this park while someone is playing. `UiControl.Outline` is the polygon region, with
`0x0066c5a4`'s test, and the island panel's root has the L. `IslandPanel.EnterPark` makes the original's five tests in its
order, `LobbyIsland.GlobalLoaded` standing for the record, and counts the keys held. Three differences are said at their
sites: Escape over the player slots opens the game menu here (`docs/QUEUE.md` Q64), Ctrl+H acts on its press and F8 is not
built (Q65), and a disabled button still takes the pointer (Q66).

### Island sound is one island at a time, and the previous one is stopped

Three functions manage the per-island pair. The first two move the island and are gated on `[5] == 0`;
the third is neither:

| Address | Vtable slot | What it is |
|---|---|---|
| `FUN_005e1640` | `+0x38` | **Next** island — walks the forward link `[1]` |
| `FUN_005e1730` | `+0x3c` | **Previous** island — walks the back link `[2]` |
| `FUN_005e14a0` | — | Stops the current island's pair and starts the **head** island's. No `[5]` test, and `[3]` is left as it was. Called by `FrontEnd_ShowPlayerSlots` (`0x004a6a33`) |

The two movers stop the outgoing island's two voices with `Sound_StopFading( island[+0x1c] )` and
`Sound_StopFading( island[+0x20] )`, zero both, pick the neighbour through vtable `+0x4c`, then
start the incoming island's **effect 1** from the sfx category (`DAT_00803a4c + 4 + idx*8`) and
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

Measured by disk capture over 60 s of flying (the `lobbyaudio.py` harness), on the build
that shipped: **`sounding=4` on 17 of 20 readings** — the three dips to 3 are a voice inside its
effect's repeat delay, visible as `bed=-` or `theme=-` in the same line — with **peak 0.2499
(−12.0 dBFS)** and **rms 0.0280 (−31.0 dBFS)**, and **zero `placed=flat` while flying**. Four parks
summing therefore does **not** clip: the levels calibrated for one park still stand, against a
documented worst case of 1.18 before master volume. A muted control measured exactly 0.0, which is what
proves the capture is the game's own mix. See `LobbyAudio.KeepPlaying`.

### Not sound: `FUN_005d83f0` / `FUN_005d8440`

They call `FUN_004732a0` / `FUN_00473f50` over the handle table at `DAT_007a4610` — the **animation**
player, not audio. The tail loop of `FUN_005e0470` walks every island and, where its animation is not
playing, starts a random one of two clips. That is the isle's M1 or M2 (`island+4`). The gate (`island+8`) is
played only by the park-entry flight and its cancel - see "Escape cancels the fly-in". The draw is the C runtime's
`rand() & 1` (`0x0067b5c0`), not the scene's own generator, and "not playing" is `FUN_005d8440` answering nought or
less for the time left on channel 0. Every isle ships exactly an M1 and an M2 in `lobby.wad`. **OpenTPW** plays each
isle's clips in turn, M1 first (`LobbyModel.Update`), and counts the unbuilt draw as `LOBBY_ISLE_RANDOM_CLIP`, once for
each isle as its first choice (`LobbyIsland.OnUpdate`): four for each lobby built. The later draws are not counted.

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

`LobbyIsland.DisplayNames` still holds the four displayed names by hand. `ParkFixedItems.ParkDisplayName` already
reads them from `THEMENAMES.str` for the gate's sign; the lobby's table should read them the same way.

**`BFSTReader` can only decode English.** Its lookup table is a **static** field initialised to
`Language/English/MBToUni.dat` — fixed for the life of the process and hardcoded to English.
Decoding the other shipped language through it shifts every letter by one:

    american/THEMENAMES.str -> "Mptu Ljohepn"  "Ibmmpxffo Xpsme"  "Xpoefs Mboe"  "Tqbdf [pof"

Two languages ship (English, american), **21 `.str` files each**. The localisation path also
hardcodes `Language/English/UITEXT.str` and `UIHELPTEXT.str`. Making the lookup table per-language
and non-static is the other half of the fix.

**How to redo the measurement:** the local `strdump` harness does it (`CLAUDE.local.md`; it mounts the file system
first, and `dotnet run` rebuilds it against the current tree). By hand: make a throwaway console project referencing `OpenTPW.Files.dll` and
`OpenTPW.Common.dll` out of the build output, add an `AssemblyResolve` handler pointing at that same
folder, then set the two statics the readers need before touching them —
`OpenTPW.Common.GlobalNamespace.Log = new Logger()` and
`.FileSystem = new BaseFileSystem( "<install>/data" )` — and construct
`new StringFile( "Language/English/THEMENAMES.str" )`, reading `.Entries`. **The file system must be
mounted first even if `StringFile` is handed a raw `Stream`**, because the reader opens its lookup table
(a static `Lazy<BFMUReader>`) through the global file system on the first string it decodes. Without it that first
decode throws, which looks like "the file is unreadable".
