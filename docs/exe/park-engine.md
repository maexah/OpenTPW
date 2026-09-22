# The park engine, from the executable

What `testme.exe` says about park loading, terrain, the camera, the clock, the save container and the park's interaction modes. The body of it comes from a six-agent Ghidra pass over `/testme.exe` on 2026-09-13 (7/7 agents, 0 errors, ~1.42M tokens, 627 tool calls) plus an adversarial cross-check agent, extended by targeted traces on 2026-09-14 and 2026-09-16. Confidence labels are the agents' own with the cross-check's corrections applied; where something is unverified or refuted, it says so, and **that is part of the fact**. Read this beside the park data-layout page (`docs/exe/park.md`), which holds what was measured off the game's own files — this page holds what the executable says.

---

## base.lnd is not the heightfield

`base.lnd` is **procedural-texture source data**, and it is **optional**.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `DAT_007a1a8c & 0x2000` | — | Gate on loading `base.lnd` at all; with the flag off the engine logs `"Procedural textures not allowed"` | Three agents concluded this independently |

**Skip it entirely for a first renderer.** Rendering its second section looks like stacked texture tiles rather than a park, which is the symptom of treating it as terrain.

---

## The heightfield lives inside base.MD2

A 0x30-byte block header, located by invariants rather than a fixed offset.

| Offset | What it is |
|---|---|
| `+0x04` | u16 `vertexCount` |
| `+0x06` | u16 `cellCount` |
| `+0x10` | float `cellSizeX` (10.0) |
| `+0x14` | float `cellSizeY` (10.0) |
| `+0x18` | u32 `cellsX` |
| `+0x1c` | u32 `cellsY` |
| `+0x28` | u32 offset → float32 height array, stride `(width + 1)` |
| `+0x2c` | u32 offset → 4-byte-per-cell record array, indexed `y*width + x` |

Jungle: **96x85 cells, 97x86 vertices, 8342 heights, 8160 cell records.** Validated arithmetically on disk: `0x13ceb0 - 0x134c58 = 4*8342` and `0x144e30 - 0x13ceb0 = 4*8160`. Derived independently by two agents and corroborated by the camera's clamp code.

**Self-check any reader with `cellSize == 10.0` and `vertexCount == (cellsX+1)*(cellsY+1)`.**

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00450950` | — | Confirms the header independently: allocates `(u16@+4 + u16@+6) * 4 + 0x30`, copies 0x30 bytes of header, then `vertexCount` dwords from the pointer at `+0x28` and `cellCount` dwords from the pointer at `+0x2c`, into a **private copy** (`DAT_0079fc54`) so the loader's flag edits never touch the loaded model | Decompiled |
| `FUN_004504c0` | — | The load-time pass that computes the non-planar bit (below) | Decompiled |
| `FUN_00450b00` | — | Heightfield derivation, one of the four routines the 96x85 figure came from | Decompiled |
| `FUN_00467030` | — | Heightfield derivation, one of the four routines the 96x85 figure came from | Decompiled |
| `DAT_0079fc54` | — | The loader's private copy of the heightfield block | `FUN_00450950` |

### The cell record

**The cell record is `{ u16 flags; u16 textureIndex }`** — NOT four independent bytes. The cross-check flags the reading "byte[0] and byte[2] are small indices, byte[1] is a runtime flags byte" as a **misread** that will make anyone treat mirror/rotation bits as a texture id.

| Flag bit | What it is | Confidence |
|---|---|---|
| `0x0001` | **Buildability, NOT visibility.** Do not cull on it | Confirmed |
| `0x0002` | Preserved by the footprint stamper; meaning not recorded | — |
| `0x0004` | Selects the triangle diagonal when `0x0800` is set | Stated by the RE pass, **still UNVERIFIED** |
| `0x0008` / `0x0010` / `0x0020` | Rotation, one field of exactly four states (`flags & 0x38`) | Confirmed |
| `0x0040` | Mirror, independent of rotation | Confirmed |
| `0x0080` | Marks a path cell; in a footprint entry it means "this cell of the footprint is used" | Confirmed |
| `0x0200` | Set by the footprint stamper | Confirmed |
| `0x0400` | **Darken / shadow bit** — halve this corner's colour | Confirmed at three separate sites |
| `0x0800` | "This cell is not planar." **Computed at load, never stored on disk** | Confirmed |

### The diagonal-choice bit has not been located

The RE pass reported "`0x0800` set means choose the triangle diagonal from `0x0004`". The `0x0800` half is right about the bit but wrong about where it comes from (it is computed at load — see below), and **the `0x0004` half is still unverified.**

Hunting the terrain renderer `FUN_0056f670` for it found 32 sites pairing `TEST ?H,0x8` with `TEST byte ptr [reg+0x1],0x4`, which looks exactly like the rule. It is not. All 32 sites were traced and every one has the same shape:

    MOV  EDX, dword ptr [0x008bcbc8]   ; the RENDER-STATE global, reloaded right here
    TEST DH, 0x8                       ; so this is render-state 0x0800, never a cell
    JNZ  skip
    MOV  EDX, dword ptr [ESP + 0x14]   ; NOW the cell record (stashed at 0056f9ca)
    TEST byte ptr [EDX + 0x1], 0x4     ; cell flag 0x0400
    JZ   skip
    MOV  EDX, [ESI + EDX*4 + 0x2b68]   ; a per-corner colour (0x2b68, 0x2b6c, ... consecutive)
    SHR  EDX, 1
    AND  EDX, 0x7f7f7f                 ; halve it

So the rule is **"render-state `0x0800` clear AND cell `0x0400` set → halve this corner's colour"**, and the 32 repeats are the four corners unrolled across several vertex-emission paths. The tempting second hypothesis — that `DX` might hold the cell flags, making `TEST DH,0x8` the cell's `0x0800` — is **refuted** by the reload two instructions earlier.

**So the diagonal choice has NOT been located, and the cell's `0x0800` has no consumer found anywhere yet. Do not implement it from the old note, and do not assume `0x0004` or `0x0400` is it — both have been checked and neither is.** The remaining lead is the triangle emission itself inside `FUN_0056f670` (5793 bytes, heavily unrolled), which would need a full decompile.

| Address | What it is |
|---|---|
| `FUN_0056f670` | The terrain renderer, 5793 bytes, heavily unrolled |
| `0x008bcbc8` | The render-state global (bit `0x0800` tested as `DH & 8`) |
| `0x0056f9c7` | The cell fetch — anchor into `FUN_0056f670` |
| `0x0056f9ca` | Where the cell record is stashed on the stack (`[ESP+0x14]`) |
| `0x0056f9ce` | The skip test `TEST byte ptr [EAX],0x1` |
| `0x0056fa13` | The UV permutation call |

### Rotation and mirror, confirmed by the footprint stamper

`FUN_00463060` stamps a ride or feature's footprint onto the terrain. It masks the rotation field as **`flags & 0x38`** — one field, exactly **four** states, not eight — and rotates it by remapping:

    90 deg  (0x5a):  0 -> 0x08 -> 0x10 -> 0x20 -> 0
    180 deg (0xb4):  0 -> 0x10,  0x08 -> 0x20,  0x10 -> 0,     0x20 -> 0x08
    270 deg (0x10e): 0 -> 0x20,  0x10 -> 0x08,  0x20 -> 0x10,  0x08 -> 0
    0 deg   (0):     flags & 0x78 - rotation AND mirror kept as they are

**`0x40` (mirror) is carried through every rotation untouched** (`uVar13 = uVar1 & 0x40` before the switch), so mirror and rotation are independent — **a renderer must apply both.** The write is `*cell = (existing & 6) | rotationAndMirror | 0x281`: it preserves bits `0x2` and `0x4`, and sets `0x0001`, `0x0080` and `0x0200`. `cell[1] = footprint[1]` copies the **texture index**, which is the cleanest possible confirmation that the record really is `{ u16 flags; u16 textureIndex }`. The footprint's own entries are `{u16 flags; u16 texture}` pairs too, with **`0x80` meaning "this cell of the footprint is used"**.

### The frame table the texture index points into

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_0046e130` | — | The `.tct` material appender; rewrites the frame count, the records pointer and the stride table when it merges materials in | Decompiled |
| `+0x36` | — | u16 frame count | `FUN_0046e130` |
| `+0x54` | — | Pointer to the frame records | `FUN_0046e130` |
| `+0x50` | — | The 8-byte-stride table | `FUN_0046e130` |

### What the jungle's 8,160 records actually contain

Counted off the shipped file:

    textureIndex  27 x5260 (64%)   57 x1376   0 x1159   59 x110   60 x90   58 x86   56 x79
    flags       0x42 x4183 (51%)  0x44 x2599  0x1 x1159  0x4 x70  0x62 x65  0x2 x45  0x64 x39

Three things fall out.

- **`flags == 0x1` occurs exactly 1,159 times and so does `textureIndex == 0`, and they are the same cells.** Index 0 is not a ground texture at all but the holes in the ground, and those cells trace out the river and the paths — they match the blank shapes in `2dmap.tga`.
- **Mirror (`0x40`) is set on 83% of cells**, so it is the norm rather than the exception and must be implemented, not skipped.
- **40.5% of horizontally adjacent cells have different texture indices.** The ground genuinely alternates between 27 and 57, so a correct render looks like a check, not a field. **Do not "fix" that.**

This distribution is itself corroboration of the `{flags, texture}` split rather than the four-byte reading.

### `0x0800` is computed at load, not stored

`0x0800` is absent from every shipped park because the loader derives it. After loading `base.md2`, `FUN_004504c0` walks every cell, builds the normals of its **two triangles** from the four corner heights, and where their dot product falls below `_DAT_006fe39c = 0.99990` (**0.81 degrees** of divergence — a very tight test, so most non-flat cells trip it) it does

    pbVar3 = (byte *)(cells + 1 + (y * cellsX + x) * 4);   *pbVar3 |= 8;

Byte `+1` of the 4-byte record is the HIGH byte of the flags u16, so `|= 8` there is `0x0800` in the word. It means "this cell is not planar", and the renderer then picks the triangle diagonal from `0x0004`. **A loader must derive this bit itself; reading it off disk gives zero everywhere.**

| Address | What it is |
|---|---|
| `_DAT_006fe39c` | 0.99990 — the planarity dot-product threshold (0.81 deg) |
| `_DAT_006fe384` | 10.0 — the cell run |
| `_DAT_006fe390` | the **double** -100.0, so `sqrt(d*d - that)` is `sqrt(d*d + 100)`, the hypotenuse over one cell |
| `FUN_004508b0` | A plain cross product |
| `FUN_0043a9a0` | Normalise |
| `FUN_0043a970` | Dot product |

### The UV rule

`FUN_0056f4f0` is the UV permutation, and it is four lines. It takes a **4-byte array of corner indices** and permutes it in place from the cell's flag word:

    if ((flags & 0x78) == 0) nothing happens at all
    if (flags & 0x40)  swap p[1] and p[3]        <- MIRROR, and it is applied FIRST
    if (flags & 0x08)  p = [p3, p0, p1, p2]      <- one quarter turn
    if (flags & 0x10)  swap p0/p2 and p1/p3      <- two
    if (flags & 0x20)  p = [p1, p2, p3, p0]      <- three

Mirror is a **diagonal reflection** (it exchanges the two off-diagonal corners), not a flip in x or y, and it happens **before** the rotation. The rotation bits are one/two/three quarter turns of the corner list, which agrees exactly with the `0 -> 0x08 -> 0x10 -> 0x20 -> 0` cycle the footprint stamper `FUN_00463060` uses. The three bits are mutually exclusive (they are a field, `flags & 0x38`), so the tests read as if/else even though they are written as three ifs. Mirror is set on 83% of jungle's cells, so **none of this is skippable**.

### How a cell's textureIndex becomes an actual texture

Solved from the data, with no reverse engineering — the RE pass had called this the largest hole and it did not need one. A cell's high u16 indexes the model's own frame table: `frameCount` is a u16 at `0x36`, the frame records start at the offset in `0x54` and are **16 bytes** each, and the name pointer is the dword at `+12` of the record. The name is a `.tga`; the file on disk is the same stem with `.wct`, in `levels/<theme>/terrain/textures/`.

Verified in all four parks — **28 of 28 indices resolve to files that exist**:

    jungle   27->jgr_bas1  57->jgr_bas2  59->jgr_bas3  58->jgr_bas4  60->jgr_bas5  56->jgr_bas6
    fantasy   5->jgr_bas1  36->jgr_bas2  39->jgr_bas3  38->jgr_bas4  37->jgr_bas5  35->jgr_bas6
    hallow   15->hrk_bas1  42->hrk_bas2  43->hrk_bas3  45->hrk_bas4  46->hrk_bas5  44->hrk_bas6
    space     7->sfl_bas2  43->sfl_bas1  45->sfl_bas3  46->sfl_bas4  44->sfl_bas5  42->sfl_bas6

Every theme uses **exactly six ground-base textures** (`jgr`/`hrk`/`sfl` = jungle ground / hallow rock / space floor), and fantasy shares jungle's set. The indices themselves differ per theme, so they are positions in that model's table and nothing more — **never hard-code one**.

**Index 0 is the null slot, not a texture — confirmed by rendering it.** It resolves to whatever frame 0 happens to be (`grd_ctr1` in jungle, `jho_fnt1` in the other three) and sits on exactly the cells whose flag word is `0x1` — the river and the paths. Drawn, jungle's `grd_ctr1` turns out to be the **road centre texture, black with yellow markings**, and it paved the river bed and every path with tarmac. A texture the road scenery uses is not the ground under a river. **Do not draw index-0 cells as ground at all.**

**Skipping them does not leave holes — it uncovers the park.** `base.MD2` already carries the river with its flowing water and stone banks, the waterfall under the bridge, and the brick of the paths; a ground quad over a cell marked 0 was putting a lid on all of it. So index 0 means "the scenery draws this cell's surface", not "nothing is built here". The first guess — that these were gaps waiting on unimplemented water and path tiles — was wrong, and the render is what corrected it.

---

## Cell size 10.0, and `world = cell * 10 + 5`

One grid cell is **10.0 x 10.0 world units**; heights are raw float32 world units (vertical scale 1.0). Confirmed four ways, independently:

| Source | What it shows |
|---|---|
| Landscape | `cellSize` floats are 10.0 in all four themes |
| Terrain render | `worldX = cellX * [+0x10] + [+0x08]` |
| Park camera | Tile centres at `5.0 + 10.0*col` |
| `.sam` consumers | Same scale |

This matches the value derived from the data alone. The camera's default POI **(475, 0, 175) = tile (47,17)**, the park Entrance, is the cleanest single corroboration.

**MapInfo and FixedItemInfo are integer GRID CELLS, not world units.**

| Address | What it is | Evidence |
|---|---|---|
| `FUN_004d8610` | MapInfo / FixedItemInfo coordinate encode | Proven from the encode/decode pair |
| `FUN_004dd500` | MapInfo / FixedItemInfo coordinate decode | Confirmed empirically: `base.map`'s `0x08`/`0x90`/`0x94` bits land exactly on the FixedItemInfo coordinates in all four themes |

---

## base.map / terrain.map: the header is 72 bytes, not 80

    'TP2M' + 32-byte description + 'MAP ' + u32 len 16412 + u32 128 + u32 128 + five reserved u32
    -> payload at 0x48 (72), 16384 bytes, then an 8-byte 'END ' trailer = 16464 total

**"16464 = 128*128 + 80" is wrong**, and the cross-check calls it "the single most quotable wrong number in this set... wrong in exactly the way that produces a silently misaligned grid."

The runtime gameplay grid is 128x128 with **cell id = `y*128 + x + 1`** (1-based, 0 = no cell) into 68-byte (`0x44`) records; `16384 * 0x44 = 0x110000` exactly. Four independent derivations.

The attribute array on disk packs the other way round — see "Axis and handedness" below.

---

## Standard.sam is a generic Bullfrog balance file, loaded twice and merged

`data/levels/Standard.sam` is read first for defaults, then the theme's own file as an **override onto the same dictionary** (`FUN_005156a0`), then a third `Easy_Standard.sam` pass. **A loader must merge, not replace** — values absent from the theme file keep the global value. The format is `Group[idx].Field value` with `#` comments; a generic parser suffices and the schema table is not needed.

---

## The state machine

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0054e360` | `Game_StateMachine` | Switches on `DAT_0087906c` | Decompiled |
| `DAT_0087906c` | — | The **current** state | Decompiled |
| `DAT_00879088` | — | The **pending request**, not the state; only read inside the park-run case | Decompiled |

States: **1** lobby load, **2** lobby run, **3** lobby unload, **9** park LOAD, **0xa** park RUN, **0xb** park UNLOAD, **0xc** whole-game shutdown; **5/6** and **7/8** are movies, **0xe** is save/load, **0xf** is the post-load first frame. Pending values: **1** = load from save, **2** = exit to lobby, **3** = quit.

The lobby is **torn down completely** at the lobby-to-park boundary (front end, lobby object, advisor, all sound, particles, all sprite banks) and state 9 rebuilds each. The **park run loop is a fixed 31 ms step with a 2000 ms catch-up clamp**. The park loading bar's budget is **500 steps**.

**Caution from the cross-check: the park-load call ORDER is well evidenced but the agent's parenthetical LABELS are not, and at least two are wrong.**

| Address | What it actually is | What it was mislabelled as |
|---|---|---|
| `FUN_00457a90` | The scene init that loads terrain | "advisor" |
| `FUN_00457c30` | Loads `base.map` | "viewport/camera" |

---

## The park camera

Separate from the lobby island camera. Save module `"SAD_CAMERA"`, chunk magic `'KAME'`, code `0x0042a190`-`0x0042d130`. Persisted state is six globals.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_0042cec0` | — | Loads the camera save block; also persists `gui_CameraFlags` and zeroes the flags immediately after loading them | Decompiled |
| `FUN_0042cdc0` | — | Saves the camera save block | Decompiled |
| `DAT_00790ab0` | `gui_CameraFlags` | Camera flags word, saved under that name | `FUN_0042cec0` |
| `FUN_0042b1c0` | — | The camera-mode dispatcher, `gui_CameraFlags`' heaviest consumer | Decompiled |
| `0x00748158` | — | The 25-entry `camera` binding table (unnamed) | `FUN_0040cb80` |

    POI default (475, 0, 175)          zoom 110, clamped [70, 130]
    pitch = lerp(45deg, 65deg, (zoom - 70) / 60), clamped [0, 84.2673deg]
    eye = POI + rotateY( yaw, (0, sin(pitch)*zoom, -cos(pitch)*zoom) )
    eye Y additionally raised by the one-pole-smoothed (alpha 0.5) terrain height under the POI
    projection near 0.1, far 1000.0, FOV 90

The camera persists a *saved* POI and *saved* Y rotation as well as the *required* ones — the shape of a camera that leaves its position and is put back (camcorder mode does exactly that).

Terrain-following is probably real, but **the sampler's identity is disputed between agents**.

### The 90 is VERTICAL

`FUN_00578be0` does write `m[0] == m[5]` with no aspect term, and on its own that leaves the convention open. **The culling frustum closes it**, because it must frame the same volume the matrix draws or geometry would pop at the screen edge.

| Address | What it is |
|---|---|
| `FUN_00578be0` | The projection-matrix builder: `m[0] == m[5]`, no aspect term. Its `DAT_008bcbc8 & 0x200` branch swaps `m[11] = sin(h)` for `m[15] = 1.0`, i.e. orthographic; the default flags word is `0x00410100`, so that bit is clear and **the park is perspective** |
| `FUN_0056bb00` | Builds four corner rays `(+/-fVar1/DAT_008bcbcc, +/-fVar1, 1)` |
| `FUN_0056b790` | Twin of the above |
| `_DAT_007018b8` | pi/180 |
| `_DAT_007018c0` | The double 0.5 |
| `_DAT_0074c9b8` × `_DAT_006fdd80` | `(pi/2) * (180/pi)` = **90 exactly**, computed, not a literal |

With 4:3 the rays are `(+/-1.333, +/-1.0, 1)`: **vertical half-angle 45, horizontal 53.13 → vfov 90, hfov 106.26.** OpenTPW was already right about this and nothing changed.

**The one inferred link:** `DAT_008bcbcc` is written at runtime through a struct pointer (the only static write, at `0x00582da8`, is the defaults initialiser zeroing `[base+4]` of the render struct at `0x008bcbc8`), so its 0.75 is **inferred, not read**. Corroboration: the picking code `FUN_0045bf90` carries 0.75 as a literal **double** at `_DAT_006fe640` and takes `atan(0.75)` to build the same frustum; and `FUN_00449ec0`'s mode table is 160x120 / 320x240 / 400x300 / 512x384 / 640x480 / 800x600 / 1024x768 / 1280x1024 / 1600x1200 / 2048x1536 — **every mode 4:3 except 1280x1024**, which is what `Camera.ReferenceAspect` already says.

### Also mapped on the way

| Address | What it is |
|---|---|
| `FUN_00449ec0` | Sets the resolution, then `DAT_007a0c9c = width * 0.5` and `DAT_007a162c = height * 0.5` — the viewport half-extents |
| `FUN_0045bf90` | Screen-to-world ray unprojection; normalises the mouse by `2*halfWidth` / `2*halfHeight` |
| `FUN_00578d20` | A plain matrix-transform helper with five modes |

### Do not try to measure the FOV from the reference screenshots

Alexah supplied park screenshots found online. They are saved, untracked, at `content/ReferenceScreenshots/Park/`:

| File | Size | What it shows |
|---|---|---|
| `LostKingdom_SunGod_800x600.jpg` | 800x600 (4:3) | The jungle — Sun God statue, gorilla ride, the river with rocky banks top right |
| `HalloweenWorld_Dragon_800x600.jpg` | 800x600 (4:3) | Hallow — dragon skeleton, cauldron, stone paths |
| `ParkInterior_Polish_cropped.jpg` | 940x529 | A Polish build, cropped, 16:9 |

The two 800x600 ones are at exactly the 4:3 the game was designed at, which is what makes them measurable at all.

**Measuring the FOV from them was attempted properly and it cannot be done.** The question was attacked with vanishing points — the one method that does *not* need the zoom, since where parallel lines converge depends only on camera direction and focal length, so the FOV/zoom confound is irrelevant. Four estimators were built, each gated against a frame OpenTPW rendered at a known angle (park entrance, 800x600, yaw 45):

1. Algebraic vanishing points + greedy RANSAC grouping → said *anamorphic* on the control. **Wrong**, and it had split one world direction across two families (two "perpendicular" axes 2.7 deg apart).
2. Same, grouped against the analytically-known true vanishing points → said *horizontal 90*. **Wrong**, because the algebraic null-space fit is biased: it put one family's vanishing point 277 px from its true place.
3. Geometric (angular) vanishing-point fit + seeded grouping → recovered *vertical 90* correctly at 97% bootstrap, **but only from a clean 3-line seed**. A 2-line seed, or a seed with one line from the wrong family, returned *anamorphic* at **100% bootstrap**. **Bootstrap confidence is not a reliability measure here**; it only says a wrong answer was stable.
4. Seedless joint search over grouping and hypothesis together → honest, and **undecided**: the three candidates are separated by 1-5% of explained edge length on the control, on Halloween and on the Sun God, and the control's own answer flips with the tolerance.

The reason is structural: separating a vertical 90 from a horizontal one means locating vanishing points one to seven thousand pixels *outside* an 800x600 frame, from edges whose directions differ by one or two degrees, in JPEG. The tooling is kept in the session scratchpad (`lines.py` detector — proven good, 0.87 deg mean miss against truth; `fit.py`, `group.py`, `joint.py`, `truth.py`, `view.py`, and `capture_control.py` for the control frame).

**The qualitative signal is withdrawn.** "The original shows no sky and OpenTPW does" is explained by **content, not lens**: the original parks are full of rides, shops and trees that stop the eye at the horizon, and OpenTPW's park is bare ground plus `base.MD2` scenery. It is not evidence about the FOV.

Two cautions from how this went. An earlier check listed the two reference folder *names* and concluded from them that there were no park shots; that was luck rather than judgement — **open the files**. And a control with a known answer is what caught all four failures above — **build the control first**.

---

## The save container

Measured against the shipped file `data/levels/jungle/Easymode.TPWI` (38,479 bytes).

Container: a fixed **1549-byte preamble**, then a **`'BILZ'`-tagged zlib block**. `1549 = 0x60D`, `+28` header = `0x629` where the zlib stream (`78 9c`) starts; the dword at `0x611` is 1,608,309, the inflated size; `1549 + 36930 = 38479`, the file length. **The 28-byte header *includes* the 4-byte tag** — an easy off-by-four.

| Offset | What it is | Confidence |
|---|---|---|
| `0x000` | u32 **version** = **400**. It is a version, not a "magic number - F4 01 00 00"; `OpenTPW.FileFormats`' `saves.md` repeats that mistake. Requiring 500 rejects the shipped file | Measured |
| `0x004` | A zero pad byte before the copyright | Measured |
| `0x005` | The copyright text, **UTF-16LE**: `54 00 48 00 45 00 ...` = "THE SAVE GAME DATA" wide. `ReadChars(824)` treats those as 8-bit and yields 824 half-characters interleaved with nulls — **824 is a byte count, not a character count** | Measured |
| `0x604` | The type, `00 01 22 19` | Measured |
| `0x608` | The version byte `0x85` = 133 | Measured |
| `0x60D` | `"BILZ"` tag | Measured |
| `0x611` | u32 **uncompressed** size, 1,608,309 | Measured |
| `0x615` | u32 **total block size including the 28-byte header**, 36,930 | Measured |
| `0x619` | 15 — **unknown field** | Measured, meaning unknown |
| `0x61d` | 9 — **unknown field** | Measured, meaning unknown |
| `0x621` | 0 — **unknown field** | Measured, meaning unknown |
| `0x625` | 0 — **unknown field** | Measured, meaning unknown |
| `0x629` | Start of the zlib stream (`78 9c`) | Measured |

Bytes `0x600..0x60C` are `00 00 00 00 00 | 01 22 19 85 | 00 00 00 00`.

**One recorded defect is withdrawn.** The claim that a reader "reads the copyright and type fields one byte early — they start at 5 and 0x605, not 4 and 0x604" is **wrong about the type**: the type is at `0x604` and the version byte at `0x608`, exactly where `SaveReader` already reads them. **Only the copyright is shifted, by the pad byte at 4.** The container walk — `"BILZ"` at `0x60D`, then dword, dword, 16 bytes, landing at `0x629` — is therefore correct.

**No height array is stored in a save.**

`mTileData` is reachable: emulating `FUN_005179c0` against the real payload shows the World block reads a **per-tile map array** straight after its 27-field header, thousands of records of

    mType | mDirection | mFlags | mMeshInstance | mNeighbours | mOverlapCounter | mParentID |
    mTileData | mHoardingNeighbours | mLitterScript | mLitter | save_status_byte

so `mTileData` is one named field per tile. **Whether it carries per-corner deformation is still open**, but the tracer reports the exact byte offset every field is read from.

---

## The sky

| Address | What it is |
|---|---|
| `FUN_005852b0(this, path)` | Loads `<path>\sky\sky.tga`, `sky_rgb.tga` and `sky_cyl.tga` |
| `FUN_00585690(x, h, z)` | A three-field setter — writes x, height and z to `+0x1f64`/`+0x1f68`/`+0x1f6c` and calls the rebuild. It does **not** load `levels/fantasy` and does not itself set height 180 |
| `FUN_00585720(this)` | The rebuild: a 16x16 drooped grid plus a 16-column skirt |
| `FUN_00585ce0(this)` | Samples `sky_rgb` on a 16x16 grid into the dome's vertex colours |
| `FUN_00584ef0(this)` | The object's init — centre (0,0), height 300 (`0x43960000`), extent `0x960` |
| `FUN_005856c0(this)` | Puts centre and height back to those defaults |
| `FUN_00572780` | Engine init; the step printing `" (tload/rain/sky ok)"`, and the only caller of the rebuild besides the setter |

**The texture loader has two callers**: the lobby's `FUN_005d8b50` at `0x005d8bac`, which passes `Data\Levels\fantasy` and then centres at (0, **180**, 0); and a park's level load `FUN_00407f20` at `0x00407f95`, reached from state 9 through `FUN_00407e00` at `0x0054ed3f` (which also loads `\Scape.omp`). **So a park does load its own sky art.**

Every theme ships `sky/` with those three files and an `ssky/` low-detail set beside it (4,140 bytes against 262,188 — that belongs to SKYQUALITY, not here). Hallow and space also ship `moon.tga`/`moon2.tga`: those are the global `Standard.sam`'s `SkyObjects[n].Filename` entries, and jungle and fantasy ship none.

**Nothing re-centres the sky for a park.** The setter's only caller is the lobby, and the rebuild's only other caller is engine init. A park's sky therefore sits at the object default: **centre (0,0), height 300**.

**A park's sky colour comes from its own art, not from a key.** `SKYCOLOUR(r,g,b)` is parsed only by the lobby's island-script reader `FUN_005e3210` — which also handles `LIGHTNING(n)` and `RAINY(n)`, storing them into the weather object at `+0x64`/`+0x68`/`+0x6c` as `value << 16`, `+0x5c` and `+0x60`. **No theme balance file carries a sky colour key at all** (checked in all four). The colour is `sky_rgb.tga`.

**The ORBIT camera never shows the sky.** Its pitch is 45-65 degrees against a vertical FOV of 90, so the top of the frame sits at elevation `45 - pitch` — 0 at best, never above the horizon. All three reference screenshots agree: ground to every edge. **Camcorder mode does show it**; from the ground the sky fills the upper half of the frame. The argument above is about the orbit camera, and for that it holds exactly.

This also sharpens the withdrawn note about OpenTPW "showing sky where the original does not": what it shows there is the **fog-cleared background past the ground's edge**, measured at exactly jungle's `ThemeEngine.FogColour` **4774136** (`0x48D8F8`) with **one unique colour and zero standard deviation** across the strip. Not sky.

**Bit `0x80` set by `FUN_0042afd0` is NOT the first-person flag.** Its only caller is `FUN_005d8de0`, a look-at basis builder (it normalises a direction, derives right/up, stores a position) reached from the **lobby's** `FUN_005d8b50` — so `0x80` of `DAT_00790ab0` marks a **scripted lobby camera**. The camera-mode dispatcher worth reading instead is `FUN_0042b1c0`.

---

## The game clock, and what a pause actually is

| Address | Original name | What it is |
|---|---|---|
| `0x785970` | — | The clock global object |
| `0x00402d90` | `GameClock_Pause` | Pauses both stopwatches |
| `0x00402db0` | `GameClock_Resume` | Resumes |
| `FUN_00402dd0` | — | Toggle |
| `0x00402d70` | — | Read the clock: `FUN_00402f10() + this[0x48]` |
| `FUN_00402f40` | — | Raw time source for the first stopwatch |
| `FUN_004033a0` | — | Raw time source for the second stopwatch |
| `0x00403030`, `0x00402f80` | — | The two pause call sites inside `GameClock_Pause` |
| `FUN_004030d0` | — | "Elapsed": `raw - 0x2c` running, `0x28 - 0x2c` paused — i.e. **frozen** |
| `FUN_004030c0` | — | IsPaused |
| `FUN_00402ea0(rate)` | — | Enters the fixed-step latch: `+0x40 = 1000 / rate`, `+0x3c = elapsed + 0x44` |
| `FUN_00402ef0` | — | Advances `+0x3c` by `+0x40` |
| `FUN_00402ed0` | — | Leaves the latch, setting `+0x44` so time is continuous across the switch |

Three layers of clock state:

    +0x10/+0x14/+0x18   a stopwatch over FUN_00402f40   } paused together by GameClock_Pause
    +0x28/+0x2c/+0x30   a stopwatch over FUN_004033a0   }
    +0x38/+0x3c/+0x40/+0x44   the fixed-step latch

Stopwatch fields: `+0x30` = paused, `+0x28` = the time captured at the pause, `+0x2c` = accumulated offset. Both stopwatches are the same code over different sources. While latched, the clock READS `+0x3c` instead of real time.

**The park enters the latch only while `[0x00878128]` is set** (at `0x0054f455`; otherwise `0x00402ed0`). That global is written at `Boot_Init` `0x0054defc`. Almost certainly recorded/replayed play; **nothing offline sets it**.

### The beat is 31 ms and it is derived, not rounded

`1000 / rate` is integer division and the park passes **`0x20` = 32** (`PUSH 0x20` at `0x0054f45f`), so the step **truncates to 31**, not 31.25. Both loops then add `0x1f` by hand.

    lobby 0x0054e74f   now = Clock_Read(); if (now - last > 0x1f4)  last = now - 0x1f4   (500ms)
    park  0x0054f47a   now = Clock_Read(); if (now - last > 0x7d0)  last = now - 0x7d0   (2000ms)
    then:              while (now > last) { last += 0x1f; <tick> }

**A pause is nothing but a frozen clock — neither loop tests a paused flag.** A clock that stops reporting new time leaves `now > last` false, so no tick runs and everything the tick drives stops together without any of it knowing why. This is the single most useful fact on this page.

### Only a park pauses

| Address | Original name | What it is |
|---|---|---|
| `0x004092a0` | `Game_Pause` | Opens with `if ([0x00786ba4] == 1)` |
| `0x00409350` | `Game_TogglePause` | |
| `0x786b68` | — | The pause object; `0x786b68 + 0x3c` **is** `0x00786ba4`, the same global each call site tests first |
| `0x0047f26c` | `MessageBox_Open` | Caller; also requires `[0x00f82884] == 0` (no lobby front end) |
| `0x0048c87e` | `GameMenu_Open` | Caller |
| `0x004a3a6c` | `OptionsScreen_Open` | Caller |

Pause object fields: **`+0x1c` = paused, `+0x20` = quiet flag, `+0x3c` = park running.**

**>>> CORRECTED 2026-09-21: "every call site" was wrong, and so was the sentence built on it. <<<**
**Every SCREEN-DRIVEN call site passes `PUSH 0x0; PUSH 0x0`** — six of them (`0x0047f26c`, `0x0049f29f`, `0x004a93d7`, `0x0048c87e`, `0x0049efba`, `0x004a3a6c`) — so for a menu, a message box or the options screen the quiet flag is 0 and the sound half of a pause is `Advisor_PauseVoice()` + `FUN_0051c1c0(1)` (which only writes `DAT_00803ad2`). **So a park's music keeps playing under the menu**, which is the part that stands.

**But the window procedure passes `(1, 1)`.** `FUN_0046b600`'s `WM_ACTIVATEAPP` branch calls `Game_Pause(1,1)` at `0x0046b74c`, and arg2 non-zero takes the **voice-pausing** path `FUN_0051bcf0` and never touches the listener. So that path **is** taken offline — on **alt-tab** — and this page's "never taken offline" is refuted. (One site, `0x005f0b7f`, pushes `EBP` twice and its value was not established.)

`FUN_0051bcf0` / `FUN_0051bd30` post commands differing only in payload address (`0x0070a268` vs `0x0070a270`), whose handlers `0x006b8e40` / `0x006b8eb0` are byte-identical but for one operand — `CALL [EDX+0x38]` against `CALL [EDX+0x3c]`, two adjacent virtual slots on the same sound object. That shape is a **suspend-all / resume-all pair**. Which matters for more than accuracy: **the original owns a primitive that holds the whole mix, spends it on losing focus, and pointedly does not use it for the park menu** — the menu takes the listener branch instead. That is the best evidence available that a park menu was never meant to silence everything.

### What the 31 ms tick drives

The park's loop runs from `0x0054f4bf` onward, with the tick counter at `[0x00877d34]` and each section bracketed by the profiling timer `[0x006fd1d0]`. It calls `Particles_Tick` first and then about fourteen more.

| Address | Original name | What it is | Cadence |
|---|---|---|---|
| `0x00520130` | `Particles_Tick` | The *entire* body of the lobby's tick loop, and first in the park's | Every tick |
| `FUN_00546c80` | — | A pairwise proximity/avoidance pass over a stride-`0x2b` array, gated on `DAT_00877b58 == 0x4a454647` | Every tick |
| `FUN_005516b0` | — | The RSSE thing/script engine (named by its own assert string) | Every tick |
| `FUN_0051e790` | — | The crowd-driven music level, called at `0x0054f870` | Every tick |
| `FUN_00475360` | — | A clock-driven task scheduler: reads the clock, runs entries whose `+0x7c` is past, reschedules `+0x7c = +0x80 + now`. Its own default interval `+0x80 = 0x3e = 62`, so a default sprite steps once per call | **Every 2nd tick** |
| `FUN_0055abf0` | — | **The flying cars** — named by its own strings, `"FLY: Failed to trigger a flying car anim (%ld)"` at `0x007660e8` and `"FLY: No headnodes on car mesh"`. It is **not** the guest/peep simulation | **Every 2nd tick** |

`0x0054f5c0` reloads the tick counter from `[0x00877d34]` and `TEST AL,0x1` / `JNZ` skips the last two on odd ticks, so they run **every 2nd 31 ms tick = every 62 ms**.

The real peep module is `0x004f9000`-`0x00512000`, **281 functions / 95,152 bytes**, plus a queue module at `0x004dd000`-`0x004e2000` (89 functions / 19,684 bytes).

**>>> ANSWERED 2026-09-21: WHICH TICK DRIVES THE PEEPS. <<<** This said it was "NOT yet established — do not assume it is any of the above", and it is now decoded in both halves. The peeps are **simulated** off the every-8th-tick thing sweep — `FUN_00516380` → `FUN_0050b360` behind the gate at `0054f668` — and they are **placed for drawing once per FRAME** by `FUN_00518f90`, called from `0x0054fa85`, which lies past the 31 ms catch-up loop's back edge at `0x0054f8da`. So neither answer alone is right: the position is stepped on the 248 ms beat and interpolated to the frame. Full decode in `ride-operation.md`, "Where a WALKING peep is drawn".

**Entering a park re-bases the baselines**: `0x0054ed7c` reads the clock three times into `[0x00878c74]`, `[0x0087879c]` and **`[0x00878a1c]`**, so the seconds spent loading are not owed as ticks. *(This third one read `[0x008786bc]` and was wrong by one dword: `0054eda4` is `a3 1c 8a 87 00` = `MOV [0x00878a1c],EAX`. `0x008786bc` is the per-frame clock SAMPLE all three alphas are measured against, not a baseline, and `0x008786c0` — one along — is written at `0054edb6`. The three baselines pair with the three rates 1/31, 1/62 and 1/248.)*

---

## Keyboard bindings and the shortcut tables

`FUN_0040cb80` builds six binding tables.

| Table | Global | Rows |
|---|---|---|
| `system` | `DAT_0078718c` | 12 |
| `game` | `DAT_00787a60` | 15 |
| `camera` | `DAT_00787c14` | 25 |
| `cheat` | `DAT_0078733c` | 12 |
| `coaster` | `DAT_00787294` | 2 |
| `shortcuts` | `DAT_0078721c` (rows at `0x00748378`) | 17 |

Each table entry is **20 bytes**:

    +0x00 u16 action id   +0x02 u16 key   +0x04 u16 modifier   +0x06 u16 (see below)
    +0x08 char* name      +0x0c void* handler                  +0x10 unused

The table object itself is `0x18` bytes: `+0x00` rows, `+0x04` u16 count, `+0x06` u16 enabled, `+0x08` a 15-char name.

**`+0x06` is a key-down LATCH on a row and an ENABLE gate on the table — two different fields at the same offset.** Neither matcher ever *reads* a row's `+0x06`: `FUN_0040c900` and `FUN_0040c990` gate on the TABLE object's `+0x06` (`if (*(short *)((int)param_1 + 6) == 0) return 0;`) and then merely *write* the row's field, 1 on key-down and 0 on key-up. So it is a held latch, not a per-shortcut enable, and **the claim that the original can turn a single shortcut on and off is NOT supported** — only whole tables are switched.

| Address | What it is |
|---|---|
| `FUN_0040cfa0` | Walks **five of the six** tables at stride `0x14` zeroing `+0x06` of each row — system, game, camera, cheat and shortcuts. **`coaster` (`DAT_00787294`) is touched by neither it nor `FUN_0040cf60`**, so coaster bindings survive a "take the keys away" |
| `FUN_0040cf60` | Sets `+0x06` of each *table object* back to 1 |
| `FUN_00486b60` | The "give the keys back" path: `FUN_0040cf60(); DAT_007c24d0 = 0;`. Its siblings are `FrontEnd_ClosePlayerSlots` and four unnamed sites near `0x0048a8xx` |

**OpenTPW has no equivalent of the enable gate**, which matters before blaming a binding that does not fire.

### How a key is matched — exact, and first match wins

**The match is `==` on both fields.** `FUN_0040c900` (this=table, key, mods), its twin `FUN_0040c990`, and the action lookup `FUN_0040c870` all compare identically:

    entry.key (+0x02) == key   &&   entry.mod (+0x04) == mods

Not a mask test, not a subset test. **So a row with mod 0 requires NO modifier held**, and a row with mod `0x0c` requires exactly `0x0c`. A table whose `+0x06` is 0 returns 0 without looking at a row.

**Dispatch is a consuming chain, in this order: camera → game → shortcuts → cheat** (the last behind a guard). Each call returns non-zero to mean "consumed" and the chain stops. Sites: key-down at `0x0048888a`..`0x00488917`, key-up at `0x00488925`..`0x00488958`, and a third at `0x004a2951`.

**Ctrl+C cannot fire camcorder in the original, for two independent reasons** — the modifier compare fails, and `game` is consulted before `shortcuts` anyway:

    game      row  9   key 'C' mod 0x0c  closepark   (handler 0040c410)
    game      row 10   key 'O' mod 0x0c  openpark    (handler 0040c3f0)
    shortcuts row 16   key 'C' mod 0x00  camcorder   (handler 0040c5c0)

**Refuted hypothesis, recorded so it is not re-invented:** the only mod values in the whole exe are `0x00`, `0x0c`, `0x30`, `0x3f`, which look like two bits per modifier (a left and a right variant). Under `==` that reading cannot be right — a left-Ctrl-only byte of `0x04` would match no row at all. Confirmed by reading the builder `FUN_0046b420`, which is the whole of it:

    GetKeyState(VK_SHIFT   0x10) < 0  ->  mods  = 0x03
    GetKeyState(VK_CONTROL 0x11) < 0  ->  mods |= 0x0c
    GetKeyState(VK_MENU    0x12) < 0  ->  mods |= 0x30

Two bits per modifier, **both always set as a pair**, read from the COMBINED virtual keys — so left and right are never told apart, and `0x3f` is Shift+Ctrl+Alt together. Same-key pairs that prove the field does real work: shortcuts rows 5/13 are both `'S'` (`allstaff` vs `staffloc`) and rows 6/14 both `'V'` (`allpeeps` vs `peeploc`).

### The original fires its shortcuts on key RELEASE

**`+0x10` is null in all 83 rows of all six tables.** `FUN_0040c900` reads `+0x10` and so never invokes anything — its only effect is setting the `+0x06` latch to 1. `FUN_0040c990` reads `+0x0c`, which is where **every** handler in the game lives, and clears the latch to 0. The two-edge design is real in the code and unused in the data.

Both posters are called from the one window proc `FUN_0046b600`:

    WM_KEYDOWN 0x100 / WM_SYSKEYDOWN 0x104  ->  FUN_00658c38 (0x1000a)  ->  FUN_0040c900  (+0x10, latch 1)
    WM_KEYUP   0x101 / WM_SYSKEYUP   0x105  ->  UI_PostKey   (0x1000b)  ->  FUN_0040c990  (+0x0c, latch 0)

Since `+0x10` is null in every row, the down path invokes nothing and only latches; **every handler in the game hangs off `+0x0c`, which only the key-UP path calls.** The Alt chords work because Windows sends them as SYSKEY messages, which route to the same two paths. Keys reach the table as **ASCII, via `MapVirtualKeyA(vk, 2)`** (so `'C'` is `0x43`, Escape `0x1b`, Backspace `0x08`), and extended keys are stored shifted: `(vk & 0xff) << 8`, which is what the `0x2600`/`0x2800`/`0x6d00` entries in `camera` and `game` are.

`FUN_0040c870(key, mods)` returns the action id, and two callers cross-check the table decode independently: `FUN_00488a00` tests `!= 0x10` (action 16, camcorder) and `== 0xf` (action 15, postcard, which then calls `FUN_004a9380`).

**Deliberate deviation in OpenTPW:** each `[DefaultKey]` is split into `Modifiers` and `Keys`, `BindingMatches` compares held modifiers for **equality**, and `PressedFrom` also requires one of the binding's own keys among the frame's `KeysPressed`. `Clone` being Ctrl alone is a deliberate held state, not a binding bug.

---

## The park management gadget

**The gadget's button list is the `shortcuts` table**, whose 17 rows are the same 17 actions, in order:

`menu, buy, hire, parkstatus, allitems, allstaff, allpeeps, financeinfo, loans, staffcosts, entryprice, research, map, staffloc, peeploc, postcard, camcorder`

`postcard`, `staffloc` and `peeploc` carry modifier `0x0c`; camcorder is a plain `'C'`. **Cross-check that proves the decode: entry 0 is `menu` at key `0x001b` = `VK_ESCAPE`**, the Escape route already traced.

Each row's `+0x0c` is a 16-byte thunk (`MOV ECX,0x007b51f0; CALL x; MOV EAX,1; RET`) into a SECOND layer of stubs, and those are what reach real code:

| Row | Reaches |
|---|---|
| `menu` | `GameMenu_Open(0)` — already named, the decode's control |
| `buy` | `FUN_004acc70(&temp, -1)` |
| `hire` | `FUN_0049bdd0(-1)` |
| `parkstatus` | `FUN_004a52a0` |
| `allitems` | `FUN_00495aa0(0, -1)` |
| `allstaff` | `FUN_00496620(0, -1)` |
| `allpeeps` | `FUN_00493530(0)` |
| `financeinfo` | `FUN_0049ac60` |
| `loans` | `FUN_0049fb30` |
| `staffcosts` | `FUN_004b2750` |
| `entryprice` | `FUN_00498d80` |
| `map` | `FUN_005f0b40` (ECX = `0x00F86DB8`) |
| `staffloc` | `FUN_004b3f60([0x007c2658])` if `[0x007cc3c0]`, else `FUN_004b3f60(0)` |
| `peeploc` | `FUN_004b3f60([0x007c2658])` if `[0x007cc418]`, else `JMP FUN_004b4280` |
| `postcard` | `FUN_004a9380` |
| `camcorder` | `FUN_00481a10` |

(`research`'s screen builder is `FUN_004aa480`, from the screen-stream table below; its stub target is not among the targets recorded here.)

**`-1` is a REMEMBERED TAB, not "nothing selected".** `FUN_0049bdd0` rebuilds the screen when `param_1 == -1`, then does `if (param_1 == -1) param_1 = DAT_007ca30c;` and switches five ways on that (cases 0-4) to choose both a title string (`0x8b`-`0x8f`) and a control id (`0x2490`/`0x2493`/`0x2492`/`0x2494`/`0x2491`). So hire has **five tabs** and `-1` means "open on the tab it was left on"; passing a real index when the screen already exists only re-titles and re-selects instead of rebuilding. **An earlier guess that `-1` meant "no item selected" was wrong** and is recorded here so it is not made again.

**The staff and visitor locators are ONE shared panel**, `FUN_004b3f60`, differing only in which global they test (`0x007cc3c0` vs `0x007cc418`, `0x58` apart — sibling objects). It switches on a type byte at `param_1 + 2` for its title (`0x95e`..`0x964`) and treats `param_1 == 0` as nothing selected. It also builds `DAT_007b05e8` exactly as `FUN_00481a10` does, which corroborates that `DAT_007b05e8` is **not** a camcorder-only global.

**`FUN_004b4280` is `peeploc` with nothing selected**, and the staffloc/peeploc asymmetry is cosmetic. It is a specialised copy of what `FUN_004b3f60(0)` does for the visitor flavour: sets `DAT_007cc30c = 6` and `DAT_007cc2f0 = 2`, hides control `0x148` and shows `0x14a` — the exact inverse of the `DAT_007cc30c == 6` branch inside `FUN_004b3f60` — and ends on `FUN_00486b00(0x11f)`, the same id that branch uses.

**REFUTED: `0x007b51f0` is NOT the gadget's object.** Every one of the 17 thunks does `MOV ECX,0x007b51f0` before its call, which looks like the park management gadget's `this` — and it is not. It has **98 references from 44 functions and NOTHING writes it**, and the referrers include `Boot_Init`, `Game_StateMachine`, `Game_Shutdown`, `Game_TogglePause`, `Game_Pause` and `Game_Resume`. It is a global game/application object; the thunks pass it as a generic receiver and the second-layer stubs mostly ignore it. **So it is not a route to how the HUD is built — do not start there again.**

**Trap, hit twice:** none of these stubs is a function to Ghidra, so `decompile` refuses them and xrefs find nothing — the same trap as `FUN_0048b6a0`. Worse, **a hand decode of the displacements got `buy` wrong** (read as `0x0044cc70`, actually `0x004acc70`). Run `api.disassemble(addr)` on the stub and read the target back off the disassembler; **do not compute rel32 by hand.**

---

## Camcorder mode — the first-person view

Camcorder mode is why a park loads a sky the orbit camera never shows.

| Address | What it is | Evidence |
|---|---|---|
| `0x007485c0` | The string `'camcorder'` | Raw byte search |
| `0x0040c5c0` | Camcorder's first-layer thunk; `CALL 0x00481a10` | Disassembled |
| `FUN_00481a10` | Installs the camcorder interaction mode | Decompiled |
| `FUN_00498ad0` | The camcorder/postcard sub-panel's UI message handler: on message `0x100` it dispatches on the control id, and **id 99 calls `FUN_00481a10`** | Decompiled |

**Note on finding the string:** a scan of Ghidra's *defined* strings missed `'camcorder'` outright; a **raw byte search (`api.findBytes` for `[Cc]amcord`) found it at once. Do that first.**

It is **shortcuts action 16, key `'C'` (`0x43`), no modifier**, and it has two ways in — that key and the gadget button id 99.

`FUN_00481a10` is:

    uVar2 = FUN_0046cff0();       // build a mode object, vtable 0x006fead0
    if ( DAT_007b05e8 == 0 ) { ... ensure the holder exists, install the default mode 0x006fea10 ... }
    FUN_0046c350( uVar2 );        // make it the current interaction mode

The "operator new twice behind SEH" part is a **shared prologue**, not camcorder's own work — `FUN_00497bc0` (the coaster builder bar) has it verbatim.

**The handler chain is all read-only.** Entry 16's handler `0x0040c5c0` is one of a run of 24 identical **16-byte thunks** (`MOV ECX,<object>; CALL <handler>; MOV EAX,1; RET`) living at `0x0040c3a0`..`0x0040c820`. **None of them is a function to Ghidra** — they are only ever reached through the table's pointer, so `decompile` refuses them and xrefs find nothing. Decode the bytes by hand, or create the function. Camcorder's thunk calls `FUN_00481a10`, which happens to be the one call target in that run that Ghidra *does* have as a function.

### Walking on the ground is swept against the cell edges, by the guests' own test

`FUN_0046cff0` builds only `{vptr, 0}` — the camcorder *interaction mode* holds no position at all, and
its vtable `0x006fead0` is mouse handlers plus `GetType`. **The movement is in the camera update**,
`FUN_0042b1c0`, and it is not the plain integration the orbit branch does.

The orbit branch adds the whole step at once:

    DAT_007908f0 = dt * velX + DAT_007908f0        // gui_CameraFlags & 0x16 clear, or & 0x3c set
    DAT_007908f8 = dt * velZ + DAT_007908f8

The **first-person** branch — `& 0x16` set and `& 0x3c` clear — instead sweeps the step cell by cell,
and at each boundary asks **`FUN_004d8750`**, which is the same edge test every guest walks on:

| Address | Call | Direction pushed |
|---|---|---|
| `0x0042c093` | `FUN_004d8750( EBX=x, EBP=y, ECX=dir, 2 )` | **3** if `velX < 0`, else **1** |
| `0x0042c290` | `FUN_004d8750( EBX=x, EBP=y, EDX=dir, 2 )` | **0** if `velZ < 0`, else **2** |

**Both push mode 2**, the strict mode, not the 0 a guest walks on. The direction numbering matches the
boundary guards already recorded for that function: 0 is `-y`, 1 `+x`, 2 `+y`, 3 `-x`.

Per pass it computes, for each axis, the fraction of the remaining step that reaches the next cell
boundary — `(1 - frac)/(v * 0.1)` going positive and `frac/(v * 0.1)` going negative, where `frac` is the
fractional part of `position * 0.1`. **Ten world units to a cell**, `_DAT_006fdd58` = 0.1. It takes
whichever boundary is nearer, tests that side, and then either advances through it or **zeroes that one
axis while the other carries on** — which is what makes a viewer slide along a wall rather than stick to
it. Both axes always advance by the fraction that was consumed.

| Constant | Value | What it is |
|---|---|---|
| `_DAT_006fdd58` | 0.1 | World units to cells |
| `_DAT_006fdd7c` / `_DAT_006fdd5c` | 10.0 / −10.0 | Cells back to world units |
| `0x0074c9c4` | **9.999** | The far edge of a cell, already carrying the epsilon below |
| `0x0074c9d0` | **0.001** | Parked this far inside the cell a side refused |
| `_DAT_006fde00` / `_DAT_006fde04` | ∓1e-4 | A step smaller than this is zeroed before anything is swept |

**One guard is worth naming because leaving it out stalls the sweep.** Having advanced to a boundary it
re-derives the cell and, *only where that cell has not changed*, nudges by `0x0074c9d0` in the direction
of travel. A step going negative lands exactly on `cell * 10`, whose floor is still the cell being left —
so without the nudge the next pass measures nought distance to the same side, consumes nothing, and never
arrives.

**One branch is decoded but not built here.** Having moved, the loop calls `FUN_0042a340( x, y )`, which
indexes the per-cell thing list at `+0x2a4` by the packed cell id `y * 0x80 + 1 + x` and returns the first
thing whose kind byte at `+2` is **3** and whose `FUN_004dd4e0()+0x118` is nought; on finding one it runs
`FUN_00412e90` and `FUN_004e15b0`. What that does to the viewer is not traced.

---

## The interaction modes

There is **one "current interaction mode" object** in the game, and camcorder is one of them.

| Address | What it is |
|---|---|
| `DAT_007b05e8` | The interaction-mode holder — **not a camcorder global** |
| `DAT_007b05d8` | The current mode object |
| `FUN_0046c350` | The setter: tears the old mode down (vtable `+0x2c`, then the destructor at `+0x00`), installs the new one and enters it (`+0x28`), reading a type id from `+0x24`. A mode whose type is 1 is replaced by one built from `0x006fe980`. ~110 call sites — modes are switched all over the game; `Game_StateMachine` (`0x00550317`) is one of them |

A mode reports its type from **vtable `+0x24`**, which is a one-instruction `MOV EAX,imm32; RET` in every case but one:

| vtable | type | constructor | What it is |
|---|---|---|---|
| `0x006fea10` | 1 | `FUN_0046c6a0` | The default / idle mode |
| `0x006fe9b0` | — | `FUN_0046ced0` | The **abstract base** — its getter is pure virtual |
| `0x006fe9e0` | 3 | `FUN_0046c580` / `FUN_0046c5a0` | |
| `0x006fea40` | 5 | `FUN_0046c6d0` | |
| `0x006fea70` | 6 | `FUN_0046cbc0` | |
| `0x006fe980` | 7 | — | What `FUN_0046c350` substitutes for a type-1 mode |
| `0x006feaa0` | 8 | `FUN_0046cfc0` | The build tools |
| `0x006fead0` | 9 | `FUN_0046cff0` | **CAMCORDER** |

**Camcorder is mode 9**, which explains the staff/visitor locator's `if (iVar1 == 9)` on the current mode's type: it is checking for camcorder and leaving first person before it points at anyone. The coaster builder bar's `FUN_00497bc0` does the same for type 8.

**The odd row is not an unknown mode — it is the base class.** `0x006fe9b0`'s type getter is `FUN_0067b0c0`, which is nothing but `__amsg_exit(0x19)`, the CRT's "pure virtual function called" abort. So the seven concrete modes (types 1, 3, 5, 6, 7, 8, 9) derive from it and `FUN_0046ced0` is the base constructor. **The table is complete; there is no eighth mode to go looking for.**

### The twelve vtable slots, fixed by the two dispatchers

    +0x00 deleting dtor   +0x04 LEFT down    +0x08 LEFT up     +0x0c RIGHT down
    +0x10 RIGHT up        +0x14 (unnamed)    +0x18 MOVE(x,y,leftHeld,rightHeld)
    +0x1c drag-with-left  +0x20 drag-with-right
    +0x24 GetType         +0x28 OnInstall    +0x2c OnUninstall

`FUN_0046c210` dispatches the buttons and `FUN_0046c2a0` the move and drags.

### Type 1, the idle mode, does nothing at all

**Every mouse slot in `0x006fea10` is a RET stub**, and its OnUninstall is a bare RET. So a world click
in idle is not handled by the mode - it is handled by the park's window message proc at `0x004881a0`,
which calls the hover updater `FUN_00486d90` and then the world click `FUN_004879d0`.

**`FUN_004879d0` acts only when the current mode's type is 0 or 1**, which is what makes clicking the
world an idle-mode-only behaviour. It switches on the hover category `DAT_007c24f4`:

    category 4      a placed object  -> FUN_00486920, the per-object window dispatcher
    category 7      a guest          -> FUN_004b79b0
    categories 8-c  the five staff   -> FUN_004b6080, the staff detail window

**A left click reaches the mode's own handler only when `FUN_004879d0` returned non-zero** - i.e. only
when the world click did not consume it. A fixed "click then dispatch" sequence is wrong.

### Picking is a real ray cast, not a grid lookup

`FUN_0045bf90` builds a camera ray from the cursor, walks the terrain height grid testing the two
triangles of each cell, then marches the ray testing object meshes. The hit point becomes a packed cell
id in `FUN_0045d560`:

    DAT_007b05cc = y * 0x80 + 1 + x        -- identical to this project's y*128 + x + 1

Written as a **16-bit** store, 0 when the point is off the map. The "y" is really the **Z** component of
the world hit point. The hovered *thing* is latched separately into the mouse-manager object at
`0x007b05a8`, and a click **snapshots** it (`FUN_0048c960`) rather than re-reading it live.

**The type-3 handlers do NOT test the cell for zero** before unpacking it; the out-of-range value is
caught downstream by the bounds test inside the callees instead.

### The verb space of mode type 3

Type 3 is a 20-byte shell - `{vptr, 1, verb, carrying, u16 itemId}` - that publishes its verb to the one
global current-tool `DAT_0081ae2c` and forwards three mouse events into the map-tool layer. **All the
real work is a switch on that global**, not in the mode.

    0    idle/clear        1    build path        3    build queue
    4    PLACE a purchased item                   0xe/0x13  track kinds
    0x14 recompute the selected object's queue    0x15/0x16 raise/lower land
    0x1a link track        0x33 DEMOLISH/SELL     0x36 staff patrol rectangle
    0x39 Buy Land          0x3a Clear Land        0x3b MOVE an existing object
    0xb, 0x10, 0x19, 0x34, 0x35, 0x38  also live; 0x32 is dispatched but NOT TRACED to a setter

**`FUN_0052f200` is not the only setter** - `FUN_0052f580` has 24 further call sites. And reading "the
literal PUSH before the call" is unsound as a blanket method: several sites pass a register.

Commit is on **button UP**, into `FUN_00528a70( x, y, verb, rotation, apply, second )` - the footprint
validator and applier, which walks every footprint cell, rotates the offsets for the four rotations
`0 / 0x5a / 0xb4 / 0x10e`, and bounds-tests each. Hover calls the same function with apply = 0.

**Money is debited at PLACE time**, inside the object constructor `FUN_004db090`, from the item's
`+0x1b8`. **So right-click cancel needs no refund - nothing was taken.** The mode's OnUninstall is a
bare RET; switching away frees 20 bytes and nothing else.

### `FUN_00532fc0`'s op codes, and what does NOT write `mNeighbours`

Decompiled 2026-09-22. The per-cell op worker switches on its op byte:

| Op | What it does |
|---|---|
| `0x80` | calls `FUN_005348d0` — this op **is** "run the link pass" |
| `0x81` | calls `FUN_005365d0` — the **tile** rule, i.e. retile |
| `0x82` | calls `FUN_005227e0` — sets the cell's **direction** byte |
| `0x83` | `cell[+0x10] = DAT_00818698` — the **owning object** |
| `0x84` | `cell[+0x20] = DAT_008186bc++` — the **crossing counter** |
| `0x85`, `0x86` | track bookkeeping and the thing notification |
| `0x87` | the teardown arm, into `FUN_005367a0` / `FUN_00527ee0` |
| `0x32` | `ClearCell` plus a direction-driven relink of what is left |

`FUN_005348d0` writes `mNeighbours` through paired `FUN_00522700` calls — the cell being laid gains
the step's bit and the neighbour gains the opposite, which is the symmetric link
`ParkPathNeighbours.Cardinal` already performs.

**It is NOT the only writer, and an earlier version of this page said it was.** `FUN_00522700` is
called from `FUN_00524960`, `FUN_00528a70`, `FUN_0052a050`, `FUN_0052fc80` and `FUN_0053b280` as well
as from inside the rule. The placer's two are the ones that matter — see "What authors an entrance's
`mNeighbours`" below — and missing them is what left Q3's central question open for a session.

**A lead recorded as refuted, because it is the obvious thing to chase next and it is wrong.** The
placer calls ops `0x81`, `0x85` and `0x86` immediately after each `FUN_005348d0`, which invites the
reading that one of them authors the entrance's bit. None of them does: they retile, do track
bookkeeping, and notify the thing. Do not spend a session on them.

### `FUN_005348d0`'s cardinal test, in its own terms

Decompiled 2026-09-22. The rule runs on **one cell as it is created** and walks the ring; for each
cardinal step it fetches the neighbour and branches on the neighbour's type:

| Neighbour type | What it does |
|---|---|
| 1 (path) | links unconditionally — sets the step's bit on this cell and the opposite on the neighbour |
| 9 (entrance) | links **only when `neighbour.Direction & (the bit of the step taken toward it)`** |
| 10 (exit) | the same test, on the **opposite** bit |
| 3 (queue) | forms no link at all; it only retiles, and plays effect `0x8b` |

The four blocks make the bit explicit: stepping north (`DAT_007cdba0` = (0,−1)) tests `& 0x01`,
south (`…ba8`) `& 0x10`, east (`…bc8`) `& 0x04`, west (`…bd0`) `& 0x40`. **So the tested bit is the
direction FROM the cell being laid TO the neighbour**, and `ParkPathNeighbours.Cardinal` already
matches it exactly — its sense is right.

**The consequence is worth stating, because it is what blocks queueing for a thing built in play.**
A path laid on the `-y` side of an entrance steps south, so it tests `& 0x10`; the shipped Belly
Bounce's entrance at (52,23) carries `direction 0x01` with its queue on that same `-y` side. Under
this rule that link could never have been earned — **so the shipped entrance's `mNeighbours` bit is
authored, not computed**, which is what `park.md:940`'s "replay creation order" warning is about.
What authors it is the next section.

### What authors an entrance's `mNeighbours` — the placer's post-sweep pair

Decoded 2026-09-22 from `FUN_00528a70( x, y, ?, angle, build, test )`. **It is not `FUN_005348d0` run
anywhere, and it is not an op code.** The placer writes both cells itself, after its footprint sweep
has finished.

The sweep walks the shape grid (stride `0x14`), reading two values per cell out of the descriptor
`DAT_00818c18`: a **kind** at `[i*2 + 2]` and a **direction byte** at `[i*2 + 3]`. The kind drives the
switch at `0x0052916c`; `case 9` is the entrance and `case 10` the exit. During the sweep each arm only
rotates and remembers:

- the direction byte turned by the angle's base bit — `FUN_004d8c20( base, dir )`, base `1`, `0x40`,
  `0x10`, `4` for angle `0`, `0x5a`, `0xb4`, `0x10e` — kept in `uStack_5c` (entrance) / `uStack_50` (exit);
- the coordinates of the cell that heading steps to, kept in `iStack_54`/`iStack_58` (entrance) and
  `iStack_44`/`iStack_48` (exit).

**The two stepping tables are opposite senses**, which is what makes a thing's way in and way out face
apart. Note that the step is the mirror of the direction ring above — heading `0x01` steps to `+y`:

| heading | entrance steps to | exit steps to |
|---|---|---|
| `0x01` | (x, y+1) | (x, y−1) |
| `0x04` | (x−1, y) | (x+1, y) |
| `0x10` | (x, y−1) | (x, y+1) |
| `0x40` | (x+1, y) | (x−1, y) |

Then, **after the sweep**, both kept headings are passed through `FUN_004d8c00` (`Opposite`) and the
pair is written straight into the two cells, at `0x005297f0`..`0x00529837`, where `H` is the rotated
heading:

    entrance cell    mNeighbours |= Opposite(H)    FUN_00522700    0x005297f0
                     mDirection   = Opposite(H)    FUN_005227e0    0x005297f8
    the cell it faces  mNeighbours |= H            FUN_00522700    0x00529826
                       mDirection   = H            FUN_005227e0    0x00529837

The entrance is addressed through the globals `DAT_00818c20` (x) and `DAT_00818c24` (y), written in the
`case 9` arm at `0x005293a1`; the cell it faces through `iStack_54`/`iStack_58`. **Read these four call
sites as disassembly.** The decompiler drops the `this` pointer, so all four print as bare
`FUN_00522700( uStack_5c )` / `FUN_005227e0( … )` with the two *different* cells invisible — only
`ECX = EDI` against `ECX = ESI` separates them. Reading the decompilation alone is what produced the
"written in exactly one place" claim corrected above.

**The shipped park confirms both cells at once.** The Belly Bounce is anchored (51,23) at angle 0, so
the base bit is 1 and the rotate is the identity. Its entrance at (52,23) carries `direction 0x01`,
which is `Opposite(H)`, so `H = 0x10`; the entrance table sends `0x10` to (x, y−1) = **(52,22)**, which
is its queue cell, and that cell is given `mNeighbours |= 0x10`. The save reads (52,22) as `0x50`,
which carries that bit. Both halves were predicted from the disassembly before the park was consulted.

So the bit a queue needs is authored at placement time, on both cells together, and never earned by the
neighbour rule — which is exactly why no replay of `FUN_005348d0` and no predicate over the finished
map can reproduce it.

**The `mDirection` half of the FACED-CELL write and the shipped park CONTRADICT each other, and the
contradiction is not resolved.** The pair above has the faced cell taking `mDirection = H` as well as
the bit, and the disassembly supports it: `FUN_005227e0` is an unconditional `MOV` into `+0x0d`, with
no guard between it and the `FUN_00522700` beside it. Against that, the Belly Bounce's **exit** at
(52,26) carries `direction 0x10`, whose exit-table step sends `0x10` to (x, y+1) = **(52,27)** — and
that cell reads `neighbours 0x39 direction 0x00` in a running park: the bit, and no direction byte.
The entrance side cannot arbitrate, because its faced cell (52,22) is a queue cell whose `0x10` the
queue tool's own flow byte would write anyway.

So there are two readings and neither is disposed of: either the placer's second write is reached
conditionally, or something that later laid (52,27)'s path cleared the byte. **OpenTPW writes the bit
on both cells and the direction on the end cell only** (`ParkBuilding.Mark`,
`JoinToWhateverIsThere`) — the state the shipped park is actually in, since a queue cell's direction
is supplied by the flow byte regardless. An earlier version of this page called the decode "refuted",
which was stronger than the evidence: one shipped cell contradicts it, and the instruction does not.

The arm is reached only when the build flag (`param_5`) is set, the test flag (`param_6`) is clear, and
`FUN_0052fab0()` is non-zero — that gate is `DAT_008187f8 != 0 ? 0 : DAT_0081b0cc`. The exit half
repeats the whole thing for `iStack_44`/`iStack_48` behind a second `FUN_0052fab0()` test.

| Address | What it is |
|---|---|
| `FUN_00522700( cell, bit )` | `cell.mNeighbours \|= bit` (`+0x0c`) |
| `FUN_005227e0( cell, b )` | `cell.mDirection = b` (`+0x0d`) |
| `FUN_00522850( cell )` | reads `cell.mDirection` |
| `FUN_004d8c00( b )` | `Opposite` — 0 → 0, `< 0x10` → `<< 4`, else `>> 4` |

**`FUN_00522700` has a second arm that does not touch `mNeighbours` at all.** When `DAT_0081b4cc` is
non-zero *and* the cell's `+2` byte is `2`, the bit is OR'd into a shadow mask at `+0x22` instead. That
flag is raised only inside `FUN_005323f0`, which sets it on entry and clears it on exit around a prefab
build, so it is 0 for anything a player places — but a reimplementation that mirrors this setter must
not mirror that arm blindly. The same flag is read by the whole setter family
(`FUN_00522730`, `…770`, `…790`, `…810`).

**`FUN_004d8c20( baseBit, direction )` is a bit rotate**, not a lookup: it counts the right-shifts
that reduce `baseBit` to 1 — i.e. its log2 — and left-rotates `direction` by that many places inside
a byte, folding anything past `0x80` back down with `>> 8`. The placer passes the angle's base bit
(1, 0x40, 0x10, 4 for 0, 0x5a, 0xb4, 0x10e), so an entrance's heading is **the shape grid's own
per-cell direction turned by the placement angle**, and at angle 0 it is that value unchanged.

**WHICH WAY it turns, because the mechanism above does not say it and a reimplementation has to
choose.** A quarter turn pairs with base `0x40`, whose log2 is 6, and a left-rotate of six inside a
byte is a **right-rotate of two** — so east `0x04` becomes north `0x01` and the ring runs backwards
against the angle. `MapDelta::Rotate` (`FUN_004d9cc0`) turns a cell delta the same way, sending the
east delta `(1,0)` to `(0,−1)` at `0x5a`, and **the two agree at all four angles**. The base-to-angle
pairing is read from the placer's own dispatch rather than taken from the table above:
`0x00528f8b` writes base `1` for angle 0, `0x0052900f` `0x40` for `0x5a`, `0x00528fe7` `0x10` for
`0xb4`, `0x00528fb7` `4` for `0x10e`. The rotate itself is `0x004d8c2a`..`0x004d8c4b`, and the
`> 0x80` fold means a MULTI-bit input would not rotate correctly — the placer only ever passes one bit.

**A bit and a delta turn the same way at 0 and at 180 whichever sense is chosen**, so only a quarter or
three quarters can tell a wrong one apart. That is how OpenTPW's `ParkBuilding.RotateBit` carried the
inverted sense — with a test asserting the inverted value, and `RotateDelta` beside it correct — until
this was read: a thing built at a quarter turn had its way in pointing 180 degrees from its own entry
cell, back across its own footprint, where nothing could ever be joined to it.

### Where a built thing's entry and exit cells come from

The same constructor derives all three cell handles, at `0x004db2da`..`0x004db36b`, each as a delta
added to the anchor cell and packed the usual way (`y * 0x80 + x`, on the `y*128 + x + 1` ids):

    mTopLeft  (+0x34) = anchor + MapDelta::Rotate( descriptor + 0x4b0, angle + 180 )
    mEntryPos (+0x36) = anchor + MapDelta::Rotate( descriptor + 0x494, angle )
    mExitPos  (+0x38) = anchor + MapDelta::Rotate( descriptor + 0x4a0, angle )

**`FUN_004d9cc0` is `MapDelta::Rotate`** — it names itself in its own assert, *"MapDelta::Rotate:
illegal angle"* — and is a four-arm table on `angle % 0x168`: 0 → `(x, y)`, 0x5a → `(y, -x)`,
0xb4 → `(-x, -y)`, 0x10e → `(-y, x)`, with the negative angles folding onto the same three forms.
Anything else asserts, so quarter turns are the whole of it. **Read the call sites as disassembly**:
the decompiler prints only two of its three arguments and calls it `void` while the code reads a
result, which is the trap this page already warns of for `CellEdge`.

**The deltas come from the item's shape picture, and `FUN_00413410` is what reads them.** It walks the
grid at descriptor `+0x18`, stores the column and row of the cell holding **9** into `+0x494`/`+0x498`
as the entrance, then looks for **10** and stores that into `+0x4a0`/`+0x4a4` as the exit. Two
fallbacks matter and both are reproduced rather than tidied: **no 10 leaves the exit on the entrance**,
which is why `mExitPos == mEntryPos` on ten of Lost Kingdom's eleven placed objects; and **no 9 leaves
both at nought**, the anchor cell itself. `+0x4b0`/`+0x4b4` is copied straight from `+0x30`/`+0x34`.

**Which characters those are was measured, not assumed.** `bouncy.sam` draws `*S*` / `***` / `***` /
`*2*`, putting its `S` at column 1 row 0 and its `2` at column 1 row 3 — and the shipped save gives
that Belly Bounce, anchored at (51,23), `mEntryPos` **2997** = (52,23) and `mExitPos` **3381** =
(52,26), which are exactly anchor + (1,0) and anchor + (1,3). Neither the picture nor the save alone
names a letter; the two meeting on one cell does. Across all four themes **263** items carry a shape
block, **44** an `S` and **137** a `2`, and **every one of the 44 has both** — so 93 items declare an
exit with no entrance and take the second fallback. The rest of the alphabet (`.`, `N`, `<`, `>`, `E`)
is **not** decoded and must not be guessed at.

**Rotation is never changed by a user input on any traced path.** It is reset to 0 on commit,
auto-oriented from the cell's direction bits when re-placing an existing thing, and inherited from the
source on move or clone. Whether the original has a manual rotate is an open question.

### Placement feedback: the `m_*` textures are the game's own vocabulary

The per-cell verdict `FUN_00535670` returns a **marker texture index** into a 20-entry table at
`0x00763b38`: 0 blue (allowed), 1 red (refused), 2 orange, 3 `m_enter`, 4 `m_exit`, 5 `m_direct`,
6 `m_front`, 7 `m_inout`, 8 `m_link`, 9 `m_break`, 10 `m_cross`, 11 `m_end`, 12 `m_nocash`,
13 `m_erase`. **`m_cross` and `m_nocash` have no producer** - no path passes either index. And
`m_nopath.tga` ships in `data/generic/dynamic/textures` but its name appears **nowhere** in the
executable.

### The object window's stats panel

`FUN_004ade40` fills the placed object's own window. It is worth reading as a whole because the seven
rows are **three different kinds of control**, and treating them alike is why a reimplementation can
render the labels perfectly and leave every value blank.

| Control | Row | Kind | Filled from |
|---|---|---|---|
| `0x3e21` | Users last month | value | Sum of **30** (`0x1e`) entries of a ring buffer; short-cuts to `FUN_00495d40` when the park is younger than that. Asserts on `"CHistory: You asked for the sum…"` |
| `0x3e1b` | Age | **text** | `FUN_004dd670`, formatted through `FUN_006acd60` with **UITEXT row `0x1b1` = 433** and a `VARM` placeholder; has an explicit negative-sign limb |
| `0x3e16` | Excitement | **gauge** | `FUN_004e0560( object, speed, capacity, duration )` |
| `0x3e18` | Reliability | **gauge** | `FUN_004df640` = `100 - FUN_004df450( …, 1 )` |
| `0x3e19` | State of repair | **gauge** | `FLD float ptr [object + 0x44]` at `004ae057` |
| `0x3e17` | Remaining life | **gauge** | `FUN_004dd6d0`, which is only `FLD float ptr [ECX + 0x48]; JMP __ftol` |
| `0x3e23` | Scrap value | value | `FUN_004e2400` — see below |

**Every gauge is handed `((v & 0xff) << 10) / 100`** — a 0..100 percentage mapped onto **0..1024** —
through the control's `+0x1c` entry. The compiler emits that divide as `IMUL 0x51eb851f; SAR EDX,5`.
The three non-gauge rows are handed a raw value or a string instead.

**`+0x44` and `+0x48` are two different floats and are easy to swap.** `FUN_004df8f0`
("repairing fully") stores `0x42c80000` — `100.0f` — into **`+0x44`**, and never touches `+0x48`.
The breakdown request in `FUN_004e0b90` does `*(float *)(this + 0x48) - _DAT_007005c8`, clamping up to
`100.0f` and down to zero. So **`+0x48` is the wear a breakdown eats (Remaining life)** and **`+0x44`
is what a repair restores (State of repair)**.

**Both are in the save, as three unnamed floats.** `FUN_004db7d0` writes them immediately after
`mQueueSizeInCells` with the type tag `'pv'` and **no field name**, in the order `+0x4c`, `+0x48`,
`+0x44`. From `mQueueSizeInCells` at 1062 that is **1066, 1070 and 1074**; carrying the serialiser's
order on through `mRequestedService`, `mTimeMarkedForMaintenance` and `mTotalCosts` lands
`mTotalTakings` on **1090**, which is independently where it is read — the same self-check that fixes
`mOperatingSpeed` at 1036. Nothing observed says what the float at 1066 is.

**Excitement and Reliability are computed from the window's own slider values**, not stored: the
window caches speed, duration and capacity at `+0x2c`, `+0x30`, `+0x34` and passes all three to both
functions. `FUN_004e0560` divides the speed by the item's per-upgrade `+0x1a8` and the capacity by
`+0x1a0`, clamps each ratio to **0.75..1.25** (the doubles at `0x007005b8` and `0x007005c0` — as
`f32` they read `0.0`, which is a trap) and multiplies them; a sideshow (`+0x4ac == 2`) returns
`20 - x` instead. `FUN_004df450` compares against `+0x19c` and `+0x1ac`, the **red line** figures, and
drops the result by a further factor past them — so the red line on a slider marks where reliability
starts falling away.

The closing multiply the decompiler hides in a bare `__ftol` **is** readable in the instructions:

    004e0729: LEA EAX,[EBX + EBX*0x2]    ; EBX*3
    004e072c: LEA ECX,[EAX + EAX*0x4]    ; *5   -> EBX*15
    004e0734: SHL ECX,0x2                ; *4   -> EBX*60
    004e0737: IMUL 0x51eb851f / SAR 5    ; /100
    004e0743: ADD EDX,EDI                ; + the crowd term, clamped 0..0x28
    ...       clamp 0..100
    004e0838: FILD dword ptr [ESP + 0x30]  ; that base, as an integer
    004e083e: FMUL float ptr [ESP + 0x2c]  ; x clamped speed ratio
    004e0846: FMUL float ptr [ESP + 0x28]  ; x clamped capacity ratio

So excitement is `(item[+0x13c] * 60 / 100 + crowd)` clamped 0..100, scaled by the two ratios. The
crowd term is `3a + b + 2c` from `FUN_00545310`, clamped 0..40, and is reached only when the object's
`mTrackRideHandle` (`+0x28`) is non-zero.

**It is still not implementable, and the blocker is a mapping this page must not paper over.** The
ratios divide by the descriptor's `+0x1a8` and `+0x1a0`, and `park.md` already records that **which
`.sam` key feeds either of those is unproven and must not be guessed** — the constructor reads its
record through an `undefined2 *`, and that reading disagrees with where `FUN_004db7d0` parses
`mOperatingSpeed` and `mOperatingDuration`. `+0x13c`, which supplies the base above, appears in no
offset table at all; the nearest established pair is `+0x124`/`+0x128` = `UsageInfo.MinCapacity` /
`MaxCapacity` (`ride-operation.md`). Having the arithmetic does not supply its inputs, so
`RIDE_EXCITEMENT_BAR` and `RIDE_RELIABILITY_BAR` stay counted rather than fitted.

### Sell, move and the scrap value

**MOVE (verb 0x3b) is demolish-then-buy-again**, literally: the object is destroyed and refunded at
pickup, the footprint fully released, and the full price re-charged at put-down by the same constructor
a fresh purchase uses. Nothing in the construction path distinguishes a move from a purchase.

**DEMOLISH (verb 0x33) refunds `price * percent / 100`**, where percent is `FUN_004e2290` - a per-item,
per-build-state, **per-age-bucket** field selected from a four-year scrap table at
`Upgrades[level]+0x08..+0x14`, returning a literal **100 for a brand-new object**. It is *not* a flat
half. The same expression is packaged as `FUN_004e2400` and shown on the object's own window before the
player sells, which is UITEXT 23 **"Scrap value"**.

**Delete does NOT go through the interaction-mode system** - `FUN_0048cd10` calls `FUN_0052f200(0x33,1)`
and then the map-click apply directly. Only MOVE builds a mode.

**Nothing refuses to sell a ride with guests queueing or riding, and nothing evicts them**; each peep
discovers it on its own tick and unlinks itself.

There IS a confirmation box, UITEXT 396, **gated on an options checkbox** (`DAT_0078d90f`) - the same
flag gates the staff dismissal box, UITEXT 397.

**Every caller of `FUN_004de1f0` is in the cell-editing family**, as this project suspected: five
functions, eight call sites, all of them the map-click apply, the cell-type setter, the run-step apply
and the footprint stamp.

### Hiring is a placement verb, and there is no hire fee

**`FUN_00507bf0` does not create a person.** It builds a **type-5** "place staff" mode carrying
(thingType, grade, costume, nameIndex); the staff thing is constructed on the next left click at the
cursor cell. Cancelling returns the candidate to the pool; completing removes it.

**There is no one-off hire fee anywhere on that path.** `StaffPoolInfo.BaseCostPerStaff` (2000) and
`CostPerQualityLevel` (100) exist in the balance file and are **never read by the executable**. The only
money is a monthly wage, `PerGradeStaffConsts[grade].BaseWage * PerTypeStaffConsts[kind].PayMultiplier`,
debited per staff thing on the new-month event - and dismissal charges exactly one further month.

**Type 5 and type 6 are different modes and the difference matters**: type 5 (a fresh hire)
**constructs** a new thing; type 6 (an existing worker picked up) **teleports** the existing thing to
the cell centre and sets it idle. Pick-up always proceeds whatever the worker was doing.

The candidate pool is 32 records of 20 bytes:
`{int kind; int nameIndex; byte grade; byte costume; byte occupied; byte takenForPlacement; int createdTick; int lifetime}`.
Grade runs 0..4 and the hire screen draws it as `(grade << 10) / 5`, **so a maximum-grade candidate
fills only 80% of its meter**. Names come from five per-kind tables of 35 entries each; there is no
`MALE_NAMES` table in the executable at all.

**Two staff numberings, and they are different permutations** - the thing type (handyman 5, mechanic 4,
entertainer 6, guard 7, researcher 8) and the sprite bank (entertainers 4, handymen 5, mechanics 6,
guards 7, researchers 8).

### The per-object management screen is nine screens

There is no single one. Nine window classes share one base whose opener is `FUN_0048cea0`. Clicking a
placed object runs `FUN_00486920( thing )`, which switches on the thing's kind byte at `+2` and, for a
placed object, on the item's `WhichUIType`:

| Window | Builder | Stream | Handler |
|---|---|---|---|
| Ride | `FUN_004af980` | `0x00755150` | `FUN_004af600` |
| Shop | `FUN_004b0e30` | `0x00755908` | `FUN_004b0b30` |
| Sideshow | `FUN_004b2430` | `0x00755e80` | `FUN_004b2150` |
| Toilet | `FUN_00497990` | `0x00751190` | `FUN_00497640` |
| Staff room | `FUN_004b5290` | `0x00756ba0` | `FUN_004b4fc0` |
| Misc item | `FUN_00499eb0` | `0x00751a68` | `0x00499bb0` |
| Upgrade | `FUN_004b6ae0` | `0x007573c8` | `0x004b67f0` |
| **Staff** | `FUN_004b6080` | `0x00756f48` | `FUN_004b5cb0` |
| Visitor | `FUN_004b79b0` | `0x007575e0` | `FUN_004b7720` |

**The identification is proven by the UIHELPTEXT rows compiled into each stream** - "delete the ride"
vs "the shop" vs "the sideshow" vs "the toilet" - not inferred.

Five verbs live in the shared base: cycle forward, cycle back, move, delete, close. **The ride's three
sliders are buffered and committed only when the window closes OR either cycle button is pressed**, and
capacity and duration commit **byte-wide** where speed is a dword.

**On the staff window**: FIRE is control **0x50c**, PICK UP is **0x50e**, and the patrol-area toggle is
**0x510** (map tool 0x36), whose "no area" sentinel is the pair (1, 0x4000) - the whole map.

**Ctrl+click on a placed object does not open its window** - it buys another copy of the same item and
puts it in the cursor.

#### The RIDE window, walked: `0x00755150`, 1536 bytes, 31 controls

Builder `FUN_004af980`, handler `FUN_004af600`. The walk starts on op 0 and ends on a balanced op 5
exactly where the next stream begins at `0x00755750`.

    0x3e14 root          (248,30,1800,1007)    mesh -> node "window2" in w_med
      0x3e28 TITLE       (746,74,1219,153)  help 3
      0x3e24 PREVIEW     (348,162,762,576)     mesh !frame
        0x3e25           (300,275,828,462)     mesh -> node "chev" in f_chev ; text (328,304,799,435)
      0x3e15 STATS       (853,162,1565,549)    mesh !frame
        seven rows, two columns at x 876..1273 and x 1315..1550, y 177/228/279/331/382/433/484
        the RIGHT column is a type-9 bar on four rows and text on three, which is why the ids
        interleave: 0x3e20/0x3e21, 0x3e1a/0x3e1b, 0x3e1c/0x3e16, 0x3e1e/0x3e18, 0x3e1f/0x3e19,
        0x3e1d/0x3e17, 0x3e22/0x3e23
      0x3e26 b_arup      (1662,126,1745,210) help 21   cycle NEXT
      0x3e27 b_ardown    (1662,216,1745,299) help 20   cycle PREVIOUS
      0x3e29 b_allthings (1648,583,1750,686) help 17
      id -1  b_okay      (1671,818,1754,901) help 1    close
      three sliders, flags 0x61: 0x3e30 help 5, 0x3e2d help 6, 0x3e2f help 7
        the control's own rect takes the pointer, op 3 is the TRACK, op 8 the thumb (b_scrollera)
        tracks slider_w, slider_w, slider_n ; each of the first two holds a type-9 0x3e2e
      the bottom row, left to right:
        0x3e2b b_erase help 15 | 0x3e2c b_move help 14 | 0x3e2a b_track help 10
        0x3e34 b_queue help 9  | 0x3e37 ??? help 16    | 0x3e36 b_callmech help 8 (toggle)
        0x3e35 b_upgrade help 19 | 0x3e38 b_door help 13 + second help 12 (toggle)

**Every verb is identified twice over, by two independent routes.** `FUN_0048cd10` reads the thing's
cell from the bytes at `+7` and `+5`, sets the map tool to **0x33** (demolish) and applies it there,
raising a confirm box on UITEXT `0x18c` first when `DAT_0078d90f` is set; its control wears
**`b_erase`**. `FUN_0048cfa0` demolishes at the same cell and then takes the item into the hand with
**0x3b** (move an existing object), `FUN_0046c5a0( 0x3b, itemId )`; its control wears **`b_move`**.
**So the original's move IS demolish-then-carry**, which is what makes it cost the full price again on
put-down and need no refund when cancelled. The cycle pair `FUN_0048cbe0` / `FUN_0048caf0` are the
same function but for `FUN_00483770` against `FUN_00483740`, and wear `b_arup` / `b_ardown`; each maps
the window's own kind tag to a class mask - ride 0x80, shop 0x200, sideshow 0x100, feature 0x800,
staff 0x40, visitor 1 - so the arrows walk every thing of that class without closing the window.

**The shared base's three ids.** `FUN_0048cea0( stream, handler, a, b, c )` is called here as
`(0x00755150, FUN_004af600, 0x3e24, 0x3e15, 0x3e25)`: `a` is the preview panel, kept at `this[6]` and
filled through vtable `+0x10`; `b` is the stats panel at `this[5]`, filled through vtable `+0xc`; `c`
is the label inside the preview, kept at `this[7]`.

**The sliders are BUFFERED.** `0x800` arms write `DAT_007cc244` / `DAT_007cc248` / `DAT_007cc24c` and
call `FUN_004aec30` with a mask of **1, 2 or 4**; the values reach the ride only when the window closes
or either arrow is pressed, and capacity and duration commit byte-wide where speed is a dword.

**18 of 18 mesh hashes resolve, and three of them only by NODE name** - the root is `window2` inside
`w_med.MD2`, `0x3e25` is `chev` inside `f_chev.MD2`, and **`0xaaee5929` (`0x3e37`, help 16, handler
`FUN_004e15b0(0)` then close) is `b_ride it!` inside `b_rideit.MD2`** - resolved 2026-09-21.

**Why it read as unresolvable.** This page said it matched "no stem and no printable token inside any
of ui.wad's 1202 members". The token scan it rested on read the members' **raw bytes**, and every one
of ui.wad's 278 `.md2` files is **refpack-compressed**, so that scan was searching compressed noise and
could never have matched anything. The node name also carries a **space and an exclamation mark**
(`b_ride it!`), which a token-splitting scan drops even on decompressed data. Decompress with the
tree's own `WadArchive` + `ModelFile` and read `ModelFile.Nodes[].Name` - 1,563 node names across
ui.wad and lobby.wad, and every outstanding hash falls out at once.

The same scan is what left the buy and hire screens' root frame `0xf76e4200` recorded as a named gap;
it is `window4` inside `w_big.MD2`, one of a family - `window1` `w_small`, `window2` `w_med`,
`window3` `w_park`, `window4` `w_big`. Five screens wore no backdrop because of it. See
`docs/exe/hud.md`. Resolver: `~/.cache/tpw-harnesses/nodenames/`, which decompresses and reads node
names; the older `meshhash.py`, which hashes stems and raw tokens, cannot see any of this.

**A trap for any re-implementation that anchors controls:** `0x3e25`'s rect (300..828) is **wider than
its parent's** (348..762), so a rule that only inherits a parent's edge when the child sits inside it
will pin this one somewhere else and draw it 160px out of place on a 1280x720 window.

### A correction that reaches every string in the game

`FUN_005da3c0` is **not** an assert taking a condition - its first argument is a severity/channel and it
is a printf-style logger. **In the retail image its entire body is a single `RET`.** So every diagnostic
quoted anywhere in these pages - "Can't put staff here", "Dropping staff member %d", "SPEED = %d" - is a
stripped no-op that the player never sees. Re-implement them as debug logging, never as UI.

---

## Postcard

    camcorder  shortcut 16 -> thunk 0040c5c0 -> CALL 00481a10      <- button id 99 calls the same
    postcard   shortcut 15 -> thunk 0040c4c0 -> CALL 00481500
                                               00481500 = JMP 004a9380   <- button id 100 calls this

**The trap that nearly made this look like a mismatch: `0x00481500` is a bare `JMP rel32` thunk** (`e9 7b 7e 02 00`), undisassembled and with no function, so it *looks* like a different handler from the button's `FUN_004a9380` until the jump is resolved (`0x00481505 + 0x00027e7b = 0x004a9380`). **Resolve every thunk before concluding two routes differ** — both of these shortcuts converge on the same function as their button, which is the pattern.

`FUN_004a9380` fits a postcard screen: `UI_PlaySound(0x95)`, then `Game_Pause(0,0)` and **`g_ParkRunning = 0`**, `UI_SetVisible(0)`, `UIParticles_ButtonGlintStop()`, `Advisor_StopQuietly(1)`, and it selects a screen with `DAT_007cc190 = 1; DAT_007cc150 = 3` (`FUN_004a9350` reads that selector). The feature ships real data — `data\Postcard.wad`, `postcard.jpg`, `data\postcard\legal.tga` and an HTML template *"Theme Park World (TM) Virtual Postcard"* — so it writes a picture out. **Not in scope now; Alexah flagged it as needed later for 100% parity.**

---

## Every screen in the game, and the stream it is built from

**`UI_LoadTree( stream, handler )` builds a screen**: `UI_ParseTreeStream( stream )` then `FUN_0065e526( handler )` to install its message callback. **The first argument is a COMPILED LAYOUT STREAM in the exe, not a filename** — reading it as a string gives nothing. (`ridestatbar.wct` and friends are a different thing, loaded by `FUN_00477ff0` — widget skins, not screens.)

**54 callers = every screen in the game.** Each row is `caller -> (stream, handler)`:

    FUN_00480b00   0074f9e0            FUN_00489f50   0074fa98
    FUN_0048a410   0074fa98            FUN_0048ac40   0074fb20
    FUN_0048adb0   0074fb20 0048a740   FUN_0048ae70   0074faf0
    FUN_0048d370   00750090            FUN_0048d8b0   007501a0 0048e2f0
    FUN_00493530   007506c8 00493230   FUN_00495aa0   007508e0 00495290  (allitems)
    FUN_00496620   00750e10 00495da0   (allstaff)
    FUN_00497b20   007514c0 00497a70
    FUN_00498790   00751530 004982a0   <- the coaster builder bar; handler takes KEY messages too
    FUN_00498b50   00751720 00498ad0   <- camcorder/postcard sub-panel, built hidden
    FUN_00498d80   00751798 00498c60   (entryprice)
    FUN_004990f0   00751928 004993e0
    FUN_0049ac60   00751ca8 0049a0b0   (financeinfo)
    FUN_0049bdd0   00751fa8 0049b650   (hire, five tabs)
    FUN_0049fb30   007525a0 0049f4e0   (loans)
    OptionsScreen_Open        00752f30 004a2bf0
    FUN_004a52a0   007537d0 004a4830   (parkstatus)
    FrontEnd_ShowPlayerSlots  00753c68 004a5fc0
    FUN_004a8140   007542b8 004a73d0   FUN_004a8c00   007540d8 004a7d70
    FUN_004aa480   00754490 004a96b0   (research)
    FUN_004acc70   00754cf8 004ac270   (buy, four tabs)
    FUN_004ae560   00755750 004ae430   FUN_004b2750   00756400 004b24b0  (staffcosts)
    FUN_004b3cf0   00756950 / 00756ad8 004b3c10   (the staff/visitor locator, two streams)
    FUN_004b7b00   007579c8 004b7a70   FUN_004b8520   00757d88 004b86a0
    IslandPanel_Create        00757f60 004b8b70
    FUN_004b9a70   007581a0            FUN_004bf460   00758cd8 / 00758e18 004bed60
    FUN_004bff70   00758f40            FUN_004c0ae0   00759170 004c0880
    FUN_004c2110   007593e8            FUN_004c23f0   00759548 004c23c0
    FUN_004c4fb0   007596b0 004c53f0
    FrontEnd_Init             00774c18 005d58b0
    FUN_005f0b40   00774da0 005f0b00   (map)

The park management gadget is stream `0x00752940`.

**How to reproduce this table, and a trap in doing so:** walk `references.getReferencesTo(UI_LoadTree)` and read back a few instructions for the `PUSH` immediates. A first attempt matched `t.startswith("PUSH 0x0")` and **silently produced an EMPTY column for all 54 rows**, because an address like `0x754cf8` prints as `PUSH 0x754cf8` with no leading zero. **An extraction that returns nothing for every row is a bug in the extractor, not an empty dataset.**

---

## The park's coaster builder bar

`FUN_00498790` builds it: `DAT_007ca1c8 = UI_LoadTree( &DAT_00751530, FUN_004982a0 )`, then `UI_SetVisible(0)` — **built hidden**, like the camcorder/postcard sub-panel beside it. **It is not the management gadget** (that is stream `0x00752940`).

**Every button on it is a `cb_*` mesh — coaster builder**, dumped from the stream: `cb_incline` (`0x19`), `cb_loft` (`0x1a`), `cb_move` (`0x1b`), `cb_rotate` (`0x1c`), `cb_swapdown` (`0x1d`), `cb_swapup` (`0x1e`), `cb_track` (`0x1f`), `cb_dellast` (`0x21`), with **`0x20` carrying no mesh at all**, and the bar is framed by `!f_plain`. It is the track-laying bar for a coaster, not a general build toolbar, and it sits at (859,916)-(1534,1190) on the virtual screen.

**It is also where park keyboard shortcuts are dispatched.** `FUN_004982a0` handles `0x1000a` by calling `FUN_0040c900( key, mods )` and `0x1000b` by calling `FUN_0040c990` — the binding matchers — so the keys flow through this panel. Message `0x14` clears `DAT_007ca1c8` (the panel handle).

The nine buttons (`0x101` = clicked; `param_4 == 1` is press, anything else is release):

    id    calls first      mode    tool mask -> FUN_00446fa0   advisor line
    0x19  FUN_00497bc0     0xe     8                           -
    0x1a  FUN_00497bc0     0xc     0x10                        -
    0x1b  FUN_00497e10     -       4                           0x121
    0x1c  FUN_00497bc0     0xd     0x20                        -
    0x1d  FUN_00497bc0     0xf     0x100                       -
    0x1e  FUN_00497bc0     0x10    0x80                        -
    0x1f  FUN_00497d20     -       1                           0x125
    0x20  FUN_00497d20     -       2                           0x126, or 0x127 if bit 0x200 is set
    0x21  FUN_00497bc0     0xa     0x40                        0x124

"mode" is `FUN_004a2aa0(n)`, which is just `FUN_0065f11d(n); _DAT_007cb2d8 = (short)n` — a UI page index. **Message `0x1001d8` cancels the tool** and withdraws all five advisor lines with `FUN_00486b40( 0x125 / 0x127 / 0x126 / 0x121 / 0x124 )`.

**`FUN_00446fa0( mask, state )` is the tool state machine.** `state & 1` enters a tool, `state & 2` leaves the one whose bit is in `mask`, `state & 4` leaves everything. `DAT_0079c638` is the set of active tool bits and `DAT_0079c658 & 0x3f` the one being torn down. Entering maps the button's mask to an internal tool id through `FUN_00444360`:

    mask 4 -> 2    mask 8 -> 4    mask 0x10 -> 8    mask 0x20 -> 0x10
    mask 0x40 -> 0x80    mask 0x80 -> 0x20    mask 0x100 -> 0x40

with masks 1 and 2 handled inline instead (mask 1 ends `FUN_00403be0(0x37)`, mask 2 calls `FUN_004445b0(2)`).

**Independent corroboration for the advisor mechanism.** These are real HUD buttons calling `FUN_00486b00( id )` to post an advisor line and `FUN_00486b40( id )` to withdraw it — reached from the UI side, having been worked out from the advisor side. `FUN_00486b40` is `FUN_005194d0` + `FUN_0059aa70` on an id; `FUN_00498ad0` itself calls `FUN_00486b40(0x131)` and `FUN_004a25f0(1)`.

**`DAT_007b05e8` is the current interaction-mode holder, not a camcorder global.** `FUN_00497bc0` constructs it when null exactly as `FUN_00481a10` does, then asks the current mode object `DAT_007b05d8` for its type through vtable `+0x24` and swaps in a different one when it is not the type wanted — build wants 8, the staff/visitor locator checks 9, and camcorder builds its own.

---

## Axis and handedness

Settled against the game's own minimap, not by inference. Each park ships `2dmap.tga` (512x512), a top-down map of itself: entrance plaza at top, a curved path through the middle, a round blob at lower right. Rendering `base.MD2`'s heightfield (97x86) and cell grid (96x85) reproduces that layout **exactly — no transpose, no flip** — with x across and y down, and low y is the entrance/north edge. **Use `2dmap.tga` as the orientation oracle for any new park.**

The two grids genuinely differ in packing, and both are pinned:

- **`base.map` attributes are X-major: `index = x*128 + y`** (proven 10/10 on FixedItemInfo).
- **`base.MD2` heights and cell records are Y-major: `y*(cellsX+1) + x` and `y*cellsX + x`.**

Mixing them up is exactly the trap the cross-check warned about.

**Still open:** whether a row maps to world +Z or -Z once drawn in 3D. It no longer risks a mirrored island, only a flipped camera convention.

---

## The blocking unknowns, worst first

1. **Which grid size is authoritative**: 96x85 (mesh), 95x84 (`.sam` MapInfo), 95x85 (the engine's own derivation). **Nobody established whether `HeightfieldWidth` counts cells, vertices or cells-minus-one.**
2. **The lighting model.** `LightNormal` was never traced to the shading dot product, so even travels-vs-toward is unconfirmed.
3. **The diagonal-choice bit** — see the terrain section. The cell's `0x0800` has no consumer found anywhere, and `0x0004` is the unverified candidate.
4. `base.MD2`'s container walking — the heightfield was found by validated signature search, **not** by walking the chunk table.
5. `base.map`'s per-bit semantics beyond `0x08`.
6. The procedural compositor.
7. The park camera's terrain-following sampler — probably real, identity disputed between agents.
8. Which tick drives the peeps.
9. Whether `mTileData` carries per-corner deformation.

---

## Building and deleting paths and queues

Decoded 2026-09-21 by a six-dimension pass over `/testme.exe`, each dimension re-derived by a second
agent told to refute it. **The brief that opened the work was wrong about its central function and
says so here**, because the wrong premise is the thing most likely to be re-invented.

### `FUN_00524960` is the build-tool APPLY DISPATCHER, not a placement engine

It is a flat chain of `FUN_0052f860( id )` tests, and `FUN_0052f860` is literally
`return DAT_0081ae2c == id`. It writes no cell field itself; every mutation is delegated. The real
functions are:

| Address | What it is |
|---|---|
| `FUN_005348d0` | **The neighbour rule** — an incremental, order-dependent symmetric link pass |
| `FUN_00535dd0` + `FUN_005365d0` | **The tile rule** — a two-pass mask lookup over two tables, then the write |
| `FUN_00528a70` | The object-footprint placer, and where an entrance/exit cell's `mDirection` is authored |
| `FUN_005367a0` | `ClearCell` — the one per-cell teardown, shared by deletion and demolition |
| `FUN_00532fc0` | The per-cell op worker; `FUN_00536100` applies one op along a line of cells |

### The runtime cell is not the save cell, and there are two parallel arrays

`CMapCell` is **0x44 bytes** at `DAT_007cf83c + 0x294`, indexed by the packed id `y*128 + x + 1`.
Field offsets come from the serialiser's own debug strings:

    +0x08 mType (signed int)   +0x0c mNeighbours   +0x0d flow direction   +0x0e mFlags
    +0x10 owning object's packed cell   +0x14/+0x18/+0x1c tile set / index / angle
    +0x20 crossing counter   +0x24 head thing id   +0x26 the design-map seed

**The `+0x29c` that appears all over the code is not a field** — it is base `0x294` plus `mType` at
`+0x08`. And `CTrackCell` is a **second** array, 0x28 bytes at `base + (cell + 0x6cde) * 0x28`, which
also carries a type dword at `+0x08` and a link at `+0x10`: sharing one struct silently corrupts one
of them.

### The direction ring, and the trap in reusing this project's own helper

Measured three ways (static initialisers, the `FUN_004d97e0` switch, and each initialiser's init-once
guard bit):

    0x01 (0,-1)   0x02 (+1,-1)   0x04 (+1,0)   0x08 (+1,+1)
    0x10 (0,+1)   0x20 (-1,+1)   0x40 (-1,0)   0x80 (-1,-1)      Opposite(b) = b<0x10 ? b<<4 : b>>4

**`CellEdge.BitFor` is the mirror of this and both are right.** It answers about the cell being
*entered*, on the side facing the cell being left, so it reads North as `0x10` where the ring reads
North as `0x01`. `ParkRideChoice.StartSides` already records the same mirror. **Reuse
`CellEdge.Opposite`, which is the generic nibble swap; never reuse `BitFor` for an outward step.**

### Why no sweep could ever reproduce `mNeighbours`

`FUN_005348d0` is **incremental and order-dependent**, keyed on the cell that has just become a path:

- **the cardinal test is type-dependent** — mType 1 links unconditionally; mType 10 only when
  `nb.mDirection & Opposite(D)`; mType 9 only when `nb.mDirection & D`; **mType 3 never forms a new
  link**, it only refreshes the neighbour's tile;
- **diagonals have two non-equivalent rules** — a strict symmetric one on this cell (the diagonal and
  both intervening cardinals all mType 1), and a **weak one-sided** fix-up applied to the neighbour
  whose intervening cardinal need only be "not 3 and not 9". The weak one sets a single bit and never
  its partner, so **`mNeighbours` is legitimately asymmetric**;
- a final **prune loop clears the two diagonals flanking any cardinal that points at an mType 3 or 9
  cell**.

Measured against the shipped park: one member set for cardinals and diagonals alike tops out at
**67/78**; splitting them so cardinals admit `{1,9,10}` and diagonals only `{1}` reaches **73/78**.
Neither can reach 78, and that is the point. **Validate by replaying creation order, never by
evaluating a predicate over the finished map.**

### The tile rule is two tables, and both reproduce the shipped park exactly

`FUN_00535dd0`: path table **49 entries at `DAT_00763138`**, queue table **11 at `DAT_007630b0`**,
each row twelve bytes `{ u32 (set << 16) | index, i32 angle, u8 mask, 3 pad }`. Two passes, and the
**table order is load-bearing** because pass two takes the first match:

    pass 1   first row with  mask == mNeighbours
    pass 2   first row with  (mNeighbours & mask) == mask

Early outs: `mType <= 0` or `== 5` gives `(set 0, index 55, angle 0)`; `abs(mType)` in
`{2,4,7,9,10,0x15,0x1e}` gives `(0, 8, 0)`. **No height or slope term enters the tile rule at all** —
a path on a slope gets the same tile plus separately generated skirt geometry (`FUN_00532fc0` op
`0x100`).

Three modifiers. `DAT_00820ac0` carries **`rand() & 1`** between calls and rewrites **index 2 to 19
and 10 to 20** for tile set 1. A queue's angle takes a base of **0 when its flow direction is `0x40`
or `0x10` and 180 otherwise — and that base applies to a STRAIGHT, not to a corner**: the guard is a
cardinal count of two with the pair opposite (N+S or E+W). The **`+1` bump is the corner case**, on
the pairs `(0x40,0x50)`, `(0x10,0x14)`, `(0x01,0x41)`, `(0x04,0x05)`. *(The first reading of this
attached the base to an L and the bump to a straight, exactly backwards. Lost Kingdom cannot tell the
two apart — its single corner carries direction `0x10`, which gives a base of nought either way — so
the park is not evidence here and the disassembly is what settles it.)* And tile set 2 takes **`+3`
for each cardinal link reaching a path cell**, but only when the link is **mutual** (the neighbour's
own mask carries the opposite bit) and a low-nibble flags test passes **on the TRACK cell** beside it
— a separate `0x28`-stride array, re-targeted through its parent where that cell defers — **not on
the path cell**.

**That `+3` can exceed the MODEL table, and a reimplementation without the gate reaches it.** The queue
models are a table of **eight** (`0x76338c`, walked by `FUN_00522900` and indexed by `FUN_005229e0`),
so an index of 8 or more names nothing at all. A straight with two mutual path links takes
`2 + 3 + 3 = 8`; a corner takes its `+1` first and reaches `3 + 1 + 3 + 3 = 10`. **Lost Kingdom cannot
arbitrate** — its one end piece at (49,22) has a single path link, so no cell in it ever gets past 5 —
and whether the track-cell flags gate is what keeps the original inside the table is therefore **not
established**. Measured in OpenTPW with that gate unreproduced: such a cell drew nothing, and because
the ground leaves any tile-set-2 cell to the queue renderer, the **sky showed through the hole**.

**Queue cells need a filler ground tile as well as a model.** When the set is 2, `FUN_005365d0` frees
any existing mesh, instantiates a per-cell model through `FUN_005229e0`, stores the handle in
`mMeshInstance`, and *then* calls the ground renderer with a hard-coded **(set 0, index 8, angle 0)** —
not the index the table returned.

**Checked against Lost Kingdom, which the executable never saw when the save was written:**

| | result |
|---|---|
| path cells, angle | **78 / 78** exact |
| path cells, index with the variant allowed | **78 / 78** |
| path cells, index exact | 51 / 78 — the other 27 all carry a variant, never anything else |
| queue cells, index (with the `+3`) | **4 / 4** |
| queue cells, angle (with the direction base) | **4 / 4** |

Pass one answered 53 of the 78 and pass two the other 25, so both passes are exercised by real data.
**So a path tile is NOT a function of the neighbour mask** and a test demanding one fixed index for a
straight will be flaky; assert the base or its variant.

**A latent defect in the shipped table, to reproduce rather than fix:** rows 27 and 29 both carry
mask `0x77` and rows 28 and 30 both carry `0xdd`, and the first match wins — so tile 12 at 180 and at
270 can never be selected.

### Deleting is not demolishing

**The demolish tool (`0x33`) cannot delete a path or a queue cell.** `FUN_00527ee0` resolves a thing
through `FUN_00527d60`, which yields one only when `mType` is 4, 9 or 10, and returns at once
otherwise. Paths and queues are cleared by `FUN_005367a0` through ops `0x32` and `0x87`, and object
demolition calls that same function over each footprint cell.

- A deleted path cell resets `mType`, the mask, the flow byte, the flags, the owner cell and the
  crossing counter to nought, then retiles. **The neighbour unlink loop runs BEFORE the reset**,
  while the cell's own mask is still intact — it clears the mirrored bit on each neighbour and retiles
  that neighbour. **That is the PATH arm.** The queue arm's unlink runs **only while the force flag
  `DAT_0081d7a8` is nought** — so under force, which is exactly object demolition and queue-over-path,
  **queue cells are torn down with no neighbour unlink at all**. The two arms also step by different
  amounts: the path arm walks all eight directions, the queue arm only the four cardinals.
- **Deleting a path refunds nothing; deleting a queue cell refunds** `perCellQueueCost * pct / 100`
  credited through `FUN_004d0190`. The asymmetry is in the code, not in the evidence. **Two
  preconditions the refund carries**: a cell of mType 9 never refunds, and neither does a queue cell
  whose owner cell has mType nought. *(An earlier reading that object demolition DEBITS rather than
  credits is withdrawn — the queue cells drained during demolition each run this refund path, so the
  net is not a plain subtraction.)*
- The one hard refusal is **NOMODIFY, `mFlags & 0x20`**, which `FUN_00536490` sets on each path cell
  it rebuilds from the level's design map. The single escape is a path cell with no neighbours, which
  logs *"Removing path cell with no neighbours but NOMODIFY set"* and clears its own flag.

  **That is the RUNTIME flag, and the shipped save does not match it — measured.** Of Lost Kingdom's
  78 path cells, **only 18 carry `0x20` in the save** (19 cells park-wide, the extra one being a
  queue cell). So "the player cannot delete the level's own paths" holds of the original's
  reconstructed state, and a reader that takes its flags from the save — as OpenTPW does — protects
  18 cells, not 78. *A hypothesis, left as one: the 18 are plausibly the author's own fixed paths and
  the other 60 were laid while the scenario was authored. Checking those 18 against `base.map`'s
  design bits would settle it.*
- **mType 30 is outside the jump table** and is silently untouched.

**The four per-cell costs are zero in the image and come from game data** — which closes here rather
than staying open: `data/levels/Standard.sam` carries `Costs.PathCell` **20** and `Costs.QueueCell`
**75**, and jungle's `Easy_Standard.sam` overrides `MapCell`, `KartTrackCell` and `WaterTrackCell`
but **not** those two.

### A queue is a re-derivable walk, not a stored link

An object caches only `mBackOfQueue` (`+0x3a`) and a cell count (`+0x40`); `FUN_004de1f0` exists
solely to throw both away and rewalk. Two cell fields make the walk possible: **`+0x0d`, a flow
direction written as the OPPOSITE of the step the run took into that cell, and only if still nought** (first writer wins), and
**`+0x10`, the owning object's packed cell**. The bond to a ride entrance is made only when a queue
cell is orthogonally adjacent to `mEntryPos` and the entrance's own flow byte points at it.

    start    first set neighbour bit of the entrance cell, in the fixed order 1, 0x10, 0x40, 4
    step     fixed probe order N, S, E, W; accept only mType 3 (never 9) whose +0x0d is the
             opposite of the direction probed            <- that fixed order IS the fork rule

**Deleting a queue cell orphans the remainder, and that is correct** — there is no trimming loop
anywhere. Peeps past the new end leave (`position >= count * 4`, state not `0xe`); the rest are told
to re-evaluate, all but the one named by `obj+0x6c`. Deleting the path a queue hangs off leaves the
queue cells untouched and merely reports the back of queue as not connected, so the ride is not
reopened.

**Eight invalidation sites, and every one fires at the same moment** — immediately after cells have
been written and retiled, with the object in ECX. So a re-implementation needs **one hook at the end
of each cell-write transaction**, not per-cell bookkeeping.

### There is no drag: a run of path is click-to-anchor, click-to-commit

**`DAT_0081ae2c` is the MODE**, named by the replay actions its two setters record —
`ACTION_SET_MODE` (`FUN_0052f200`) and `ACTION_SET_MODE_NR` (`FUN_0052f580`), from the dictionary
`FUN_004041d0` prints verbatim. It has exactly **three writers** in the whole image, so the id table
is closed.

**But the recorded action is NOT what separates the two setters — the ANCHOR is.** `FUN_0052f200`
clears `DAT_0081ede4`/`DAT_0081ede8` to `-1` on every call; `FUN_0052f580` never touches them. That
is the whole reason the "NR" variant exists: the drain can swap the mode to the internal `0x34`/`0x35`
and back **without destroying the run in progress**. Build the pair that way round — an
implementation that distinguishes them only by a log line will wipe its own anchor mid-run.

**Mode 1 is path and mode 3 is queue**, fixed by the cursor table: `FUN_00489720` registers
`c_path.ani` as id 3 and `c_queue.ani` as id 4. **Mode 1 takes cursor 3 only while
`DAT_00816d60` and `DAT_00816d4c` are both nought** — otherwise it shows `c_link.ani` (5) or
`c_end.ani` (0x12) — and cursor 3 is not unique to it, since mode `0x39` shares the same label.

**The build-tool interaction mode is object type 3, vtable `0x006fe9e0`** (constructors
`FUN_0046c580` / `FUN_0046c5a0`), *not* type 8. Type 8 (`0x006feaa0`, ctor `FUN_0046cfc0`) is the
**coaster/track editor**: its mouse slots tail into `FUN_00446300`, which records
`ACTION_COASTER_LOFT`, `_ROTATE`, `_WOBBLE`, `_STACKUP`, `_STACKDOWN` and `_DELETEMULTI`.

**The type id does NOT identify a class, and leaning on it is the COMDAT trap this project has been
caught by before.** `FUN_006b71c0` (`MOV EAX,3`) is the GetType of **four** different vtables, and
`0x0040f3f0` (`MOV EAX,8`) of three. **Identify a mode by its constructor and its non-stub slots,
never by the number its GetType returns.** There are also **four** interaction-mode classes in play,
not two: the one that matters most is the **idle/default** mouse mode (vtable `0x006fea10`, GetType 1,
ctor `FUN_0046c6a0`, ~70 call sites), which is what both setters install when the mode goes to nought.

    +0x04 LEFT down  FUN_0046c5d0 -> FUN_00524790      +0x18 MOVE  FUN_0046c660 -> FUN_005234d0 (preview only)
    +0x08 LEFT up    FUN_0046c610 -> FUN_00524960      +0x1c drag-with-left   RET 8   <- EMPTY
    +0x24 GetType    returns 3                         +0x20 drag-with-right  RET 8   <- EMPTY

**Both drag slots are bare `RET 8`, so there is no drag mechanism at all.** The commit runs on button
**up**. The anchor lives in `DAT_0081ede4`/`DAT_0081ede8` (`-1` = none): the first click stores the
anchor, and the next click **snaps the target to the dominant axis** (the larger of `|dx|`,`|dy|`
wins and the other is forced back to the anchor's value) and walks the line. **The anchor then
advances to that axis-snapped TARGET — not to the cell the run actually reached** — and that is
precisely what lets a player lay an L-shaped run click by click. It advances only when the closing
retile did **not** report failure; a failed run leaves the anchor where it was. The action recorder corroborates it independently: one
record per click carrying a single cell (`AR - %d (%d, %d)`), where a real drag would have to record
every intermediate cell or a start/end pair.

**Per cell, in order:** `0x87` clear, `<mode id>` stamp, `0x80` join neighbours, `0x82` flow
direction, `0x85`/`0x86` fix-ups, `0x83` owner, `0x81` retile. `FUN_00536100` walks the line and
**aborts the whole run** the moment one cell refuses.

**`FUN_005346d0` is the stamp**, and its order matters: stamping a cell that is *already* that type
just bumps the re-stamp counter at `+0x20` and returns success **having charged nothing**; queue over
path **force-clears the path with no refund**; the affordability test is `cash - price >= 0` and
refuses the cell, and therefore the run; the debit happens **after** the type write; and a path
stamped over a queue cell **invalidates the owning object's queue**.

**Refusals are shown as a CURSOR, not as text** (`FUN_0052f950` → `FUN_004a2aa0`): `0x14` is
cannot-afford, `8` is blocked. Off-map is `FUN_004d8300`, `0 <= x,y < 0x80`.

**You do not pick "queue" as a tool.** Mode 3 is never installed from the UI at all — it is reached
only from inside the commit handler, the demolish path, or mode `0x14` ("edit this ride's queue"),
which refills the pending list from the object, rewalks the queue and *then* drops into mode 3.
What the UI does install: clicking a path cell or bare ground gives mode 1, clicking a queue cell
gives `0x14`, and the **buy window** gives mode 4 for a real item and **`0x39` / `0x3a` for the
pseudo-item ids −1 and −2** — which are exactly the Buy Land and Clear Land rows.

### What the adversarial pass overturned, and it is not cosmetic

Each dimension was re-derived by a second agent told to refute it. Two findings change what a
re-implementation must *do*, rather than merely how it is described:

- **You bond a queue to a ride entrance only if you START the run next to it.** The entrance-bond
  block in `FUN_005348d0` is wrapped in `if (DAT_00820aa4 != 0)`, and `FUN_00536100` sets that flag at
  entry and clears it immediately after the **first** cell of the line. So the bond is attempted on
  the first cell only. Built to the unrefuted reading, a re-implementation would bond from any cell of
  any run — a substantive gameplay difference.
- **A cell is not deleted until a counter goes negative.** `cell+0x20` is a signed 16-bit per-cell
  count: `FUN_005367a0` decrements it and then **returns without removing anything** while it is still
  `>= 0` (unless the force flag `DAT_0081d7a8` is set). Queue *corners* are the exception — two
  perpendicular links force `+0x20 = 0xffff`, i.e. immediate removal. Every statement above about what
  deleting a cell does is subject to this.

Also corrected: the ordinary cell-to-cell link additionally requires the neighbour's **degree < 2**
and its owner id to be either the object being built or nought — so the builder largely prevents
forks from ever existing, and the walk's fixed probe order is a tie-break for states the builder does
not produce, not the primary fork rule. And the eight direction statics are laid out in **source
order, not bit order**: six of eight addresses pair differently than a naive reading gives
(`0x7cdba8` is S, `0x7cdbc8` is E, `0x7cdbd8` is NE). The bit→delta table itself survived; only the
address shorthand was wrong. **Cite the initialisers and `FUN_004d97e0`'s jump table, never the
address ordering.**

### Two things these dimensions disagree about, recorded rather than resolved

**Which array the build code mutates — STILL CONTESTED, and it changed sides twice.** One decode read
it as the **0x44-byte** `CMapCell` at `DAT_007cf83c + 0x294`, the other as the **0x28-byte** record at
`DAT_007cf83c + 0x1102B0`, and their field offsets agree exactly (`+0x08` type, `+0x0c` mask, `+0x0d`
direction, `+0x0e` flags, `+0x10` owner, `+0x14/+0x18/+0x1c` tile) — which is what made it look like a
flat contradiction rather than two readings of two arrays.

Where it now rests: the adversarial pass over the placement decode calls the 0x28 reading **backwards
and refutes it four ways**, the clearest being the line walker `FUN_00536100`, which forms the cell
pointer as `SHL ECX,4; ADD ECX,EAX; LEA ECX,[EDX + ECX*0x4 + 0x294]` — id × 17 × 4 = id × `0x44`. So
**the build path writes the 0x44 record**, and the 0x28 array is the separate track layer. Against
that, the adversarial pass over the *tools* decode has `FUN_00522850` fetching the direction byte from
the **0x28** record. Both cannot be right about the same byte, and no third witness has settled it.

**Do not cite either as settled.** Nothing in OpenTPW depends on it — cells are modelled as records
rather than as raw memory — which is exactly why it is safe to leave open rather than guessed.

**Still genuinely open:** who calls `FUN_0052fe50` — one dimension has it as the general pending-cell
drain, the other finds its callers only inside `FUN_00527ee0`.

**And the drain step is not the simple pop it looks like.** Each call commits **up to two cells**, so
interior cells are committed **twice**; the bottom element is never committed and the count is left at
**1**, not nought — `while (step())` therefore terminates with one element still in the list. There is
also an **off-by-one in the shipped guard**: the push rejects only when the count exceeds `0x400`, so
element `0x400` is writable and lands exactly on `DAT_0081d740`, the map-width global. Reproduce the
behaviour, not the overrun.

**Who calls `FUN_0052fe50`.** One dimension has it as the general pending-cell drain step; the other
finds its only callers inside `FUN_00527ee0` (demolition). Unresolved.

### Still open

- Which `ItemDescription` fields drive the object flag word at `obj+0x32`; only bit `0x08` (owns a
  queue, starts closed) is pinned by behaviour.
- What the low nibble of `mFlags` means — it gates the queue `+3` bump and only `0x20` = NOMODIFY is
  established.
- ~~Whether mType 9 and 10 really are entrance and exit.~~ **CLOSED 2026-09-22: they are.**
  `FUN_00413410` walks the item's shape grid at descriptor `+0x18` testing each cell against the
  literals **9** and **10**, and stores the matching cell's column and row as the entrance and the exit
  — see "Where a built thing's entry and exit cells come from" above. No debug string names them and
  none is needed: the constructor tests the numbers outright.
- mType 2 and mType 5 are unidentified; the decode refused to guess water or rock.
- Whether tool `0x32` is ever armed as a live tool — nothing pushes it to either setter.

## The Ghidra project was changed to get here

A later session will find these already defined in the shared Ghidra project (which auto-backups) — that is why, not a stale note:

| Address | What was done |
|---|---|
| `0x004816e0` | `buy` stub — disassembled from undefined bytes |
| `0x00481790` | `staffloc` stub — disassembled from undefined bytes |
| `0x004817c0` | `peeploc` stub — disassembled from undefined bytes |
| `FUN_004b4280` | Created as a function (483 bytes) |
| `FUN_0067b0c0` | Disassembled from undefined bytes and created as a function |
