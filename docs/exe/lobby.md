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

### The lobby island gate — still loops, deliberately

The lobby gate loops open/shut (`Jun_gateM1` opens, `Jun_gateM2` shuts). Alexah confirmed this is
fine **as a diagnostic only**; the intended behaviour is doors idle shut, opening when the player
enters the park. **Nothing raises a park-entry event yet**, so removing the loop would leave the
lobby gate permanently shut with nothing able to open it — a design change, not a tidy-up. Build the
park-entry trigger first; the gate is the first thing to convert when that lands.

The same "looped because nothing sequences it yet" stand-in still applies to the rest of the lobby:
the Dino and the butterflies.

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
