# The park engine, from the executable

What `testme.exe` says about park loading, terrain, the camera, the clock, the save container and the park's interaction modes. The body of it comes from a six-agent Ghidra pass over `/testme.exe` on 2026-09-13 (7/7 agents, 0 errors, ~1.42M tokens, 627 tool calls) plus an adversarial cross-check agent, extended by targeted traces from 2026-09-14 to 2026-09-25; most later sections name their own date or `docs/QUEUE.md` item. Confidence labels are the agents' own with the cross-check's corrections applied; where something is unverified or refuted, it says so, and **that is part of the fact**. Read this beside the park data-layout page (`docs/exe/park.md`), which holds what was measured off the game's own files — this page holds what the executable says.

---

## base.lnd is not the heightfield

`base.lnd` is **procedural-texture source data**, and it is **optional**.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `DAT_007a1a8c & 0x2000` | — | Gate on loading `base.lnd` at all (`FUN_004504c0`, `0x004507a7`); with the flag off the call is skipped and nothing is logged. `"Procedural textures not allowed"` is `FUN_0056dfe0`'s, when `DAT_008bd550 & 0x10000000` is set, and logged only under the debug bit `DAT_008bd508 & 0x2000000` ("The blocking unknowns", 6) | Ghidra, Q194 |

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
| `0x0001` | **Hides the cell from the terrain pass.** `FUN_0056f670` skips any cell carrying it (`TEST byte [EAX],0x1` at `0x0056f9ce`). Set in `base.MD2` on exactly the 1,159 jungle-grid cells of mType 2, 30 and 7, and at run time on every cell the tile rule answers (0,8) — footprint, entrance, exit and queue cells (`FUN_0046df50`, `OR 0x101` at `0x0046e012`). A second, optional pass (`FUN_00570d90`, gated on `DAT_008bcbc8 & 0x10000`) has no such test | Confirmed 2026-09-22 |
| `0x0002` | Preserved by the footprint stamper; meaning not recorded | — |
| `0x0004` | Preserved by the footprint stamper. The RE pass said it selects the triangle diagonal when `0x0800` is set; neither terrain pass tests it | **Not borne out** - see below |
| `0x0008` / `0x0010` / `0x0020` | Rotation, one field of exactly four states (`flags & 0x38`) | Confirmed |
| `0x0040` | Mirror, independent of rotation | Confirmed |
| `0x0080` | Marks a path cell; in a footprint entry it means "this cell of the footprint is used" | Confirmed |
| `0x0200` | Set by the footprint stamper | Confirmed |
| `0x0400` | **Darken / shadow bit** — halve this corner's colour | Confirmed at three separate sites |
| `0x0800` | "This cell is not planar." **Computed at load, never stored on disk** | Confirmed |

### The diagonal-choice bit has not been located

The RE pass reported "`0x0800` set means choose the triangle diagonal from `0x0004`". The `0x0800` half is right about the bit but wrong about where it comes from (it is computed at load — see below), and **the `0x0004` half is not borne out: neither terrain pass (`FUN_0056f670`, `FUN_00570d90`) tests the cell's `0x0004`.**

Hunting the terrain renderer `FUN_0056f670` for it found 16 sites pairing `TEST ?H,0x8` with a test of `0x4` in the cell's high byte (`TEST byte ptr [reg+0x1],0x4`, or `TEST AH,0x4` at `0x0056fb0e` and `0x0056fd67`), which looks exactly like the rule. It is not. All 16 sites were traced and every one has the same shape:

    MOV  EDX, dword ptr [0x008bcbc8]   ; the RENDER-STATE global, reloaded right here
    TEST DH, 0x8                       ; so this is render-state 0x0800, never a cell
    JNZ  skip
    MOV  EDX, dword ptr [ESP + 0x14]   ; NOW the cell record (stashed at 0056f9ca)
    TEST byte ptr [EDX + 0x1], 0x4     ; cell flag 0x0400
    JZ   skip
    MOV  EDX, [ESI + EDX*4 + 0x2b68]   ; a per-corner colour (0x2b68, 0x2b6c, ... consecutive)
    SHR  EDX, 1
    AND  EDX, 0x7f7f7f                 ; halve it

So the rule is **"render-state `0x0800` clear AND cell `0x0400` set → halve this corner's colour"**, and the 16 repeats are the four corners unrolled across several vertex-emission paths. The tempting second hypothesis — that `DX` might hold the cell flags, making `TEST DH,0x8` the cell's `0x0800` — is **refuted** by the reload two instructions earlier.

**So the diagonal choice has NOT been located, and the cell's `0x0800` has no consumer found anywhere yet. Do not implement it from the RE pass's report, and do not assume `0x0004` or `0x0400` is it — both have been checked and neither is.** The remaining lead is the triangle emission itself inside `FUN_0056f670` (5793 bytes, heavily unrolled), which would need a full decompile.

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
- **Mirror (`0x40`) is set on 84% of cells**, so it is the norm rather than the exception and must be implemented, not skipped.
- **40.5% of horizontally adjacent cells have different texture indices.** The ground genuinely alternates between 27 and 57, so a correct render looks like a check, not a field. **Do not "fix" that.**

This distribution is itself corroboration of the `{flags, texture}` split rather than the four-byte reading.

### `0x0800` is computed at load, not stored

`0x0800` is absent from every shipped park because the loader derives it. After loading `base.md2`, `FUN_004504c0` walks every cell, builds the normals of its **two triangles** from the four corner heights, and where their dot product falls below `_DAT_006fe39c = 0.99990` (**0.81 degrees** of divergence — a very tight test, so most non-flat cells trip it) it does

    pbVar3 = (byte *)(cells + 1 + (y * cellsX + x) * 4);   *pbVar3 |= 8;

Byte `+1` of the 4-byte record is the HIGH byte of the flags u16, so `|= 8` there is `0x0800` in the word. It means "this cell is not planar"; nothing that reads it has been found (above). **A loader must derive this bit itself; reading it off disk gives zero everywhere.**

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

Mirror is a **diagonal reflection** (it exchanges the two off-diagonal corners), not a flip in x or y, and it happens **before** the rotation. The rotation bits are one/two/three quarter turns of the corner list, which agrees exactly with the `0 -> 0x08 -> 0x10 -> 0x20 -> 0` cycle the footprint stamper `FUN_00463060` uses. The three bits are mutually exclusive (they are a field, `flags & 0x38`), so the tests read as if/else even though they are written as three ifs. Mirror is set on 84% of jungle's cells, so **none of this is skippable**.

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

`data/levels/Standard.sam` is read first for defaults, then the theme's own file as an **override onto the same dictionary** (`FUN_005156a0`), then, in game type 2 (Instant Action) only, a third `Easy_Standard.sam` pass (`0x00515811`..`0x0051585c`). **A loader must merge, not replace** — values absent from the theme file keep the global value. The format is `Group[idx].Field value` with `#` comments.

### How a key finds its global

Decoded 2026-09-23 (`docs/QUEUE.md` Q36), put to two refuters. **The address comes from the key's place in the executable's own table, not from the file's order.** Each balance family is one static object whose vtable slot 0 returns record `i` of a table in `.rdata` (stride `0x3c`: a type dword, then the name) and whose slot 1 (`0x005b0d60`) returns `this + 8`. `FUN_00401030` gives every value record (types 4 to `0xb`) the next dword, starting at slot 1, so record `i`'s global is `this + 8 + 4*slot`. The parser `FUN_004017a0` resolves `Group.Field` through `FUN_00401280`, which searches back from the table's end for the group and then for the field, case-sensitively. Types: 0 section start, 1 section end with the group name, 2/3 an array, 4 int, 5 int ≥ 0, 6 bounded int, 7 float, 8 float ≥ 0, 9 bounded float, `0xa` string, `0xb` a multi-line block, `0xc` the end. A float value (from `0x00401d8b`, Q173) is an optional `-`, then digits with at most one `.`, then whitespace or the line's end, under 100 characters; anything else is a bad line, and whatever follows the whitespace is ignored. It is converted with `atof` and stored as a float (`0x00401e22`).

The `PeepInfo` object is at `0x00785040` (built at `0x00402ae0`, table `0x0073fc70`): `0x0078504c` ExitLevel, `0x00785050` ExitLevelVar, `0x00785054` StartingCashVarPc, **`0x00785058` SmallHappinessChange, `0x0078505c` MediumHappinessChange, `0x00785060` BigHappinessChange**, then **`0x00785064` PerfectRide, `0x00785068` GoodRide, `0x0078506c` OKRide, `0x00785070` RideVomitDivisor**, read off the table itself (record by record, Q169): the file lists RideVomitDivisor straight after BigHappinessChange, so fitting file order to addresses goes wrong from there, and the table does not. `FUN_004fdcc0`, the excitement match, is the four's only reader (`ride-operation.md`, "The excitement match"). `FUN_004fe980` confirms the three: its level 0, 1 and 2 return the bytes at `0x00785058`, `0x0078505c` and `0x00785060`. Every reader loads those three as a byte; the four ride keys are read as whole ints. Lost Kingdom offline takes 5, 15 and 25 from `data/levels/Standard.sam`; no jungle file overrides them (online is 3, 10, 25).

A line may name several fields, each taking the next value (up to sixteen; OpenTPW reads these, `BalanceFieldTests`). Two more parser behaviours, measured and not yet compared with OpenTPW's reader: **the first bad line ends the file** - an unknown key, a bounded value out of range, or a negative in a type-5 field returns nought and no later line is read; and **an array's count is the highest index the files wrote, plus one**, not the table's size - so Lost Kingdom's guest type is `rand % 8`.

**An item's description is read the same way, one file laid over another** (Q171's verify, `wf_727b3f26-329`;
Q178's decode, `wf_8a4195bd-544`, two Opus skeptics upholding every claim). The catalogue is built afresh at every
park load: `Game_StateMachine`'s state 9 calls `FUN_00407e00` (`0x0054ed3f`), which builds it (`FUN_00413140`) before
the balance (`FUN_005156a0`). `FUN_00413140` walks Rides, Features, Upgrades, Shops and Sideshow in that order, each
through `FUN_00413930`, which takes every subfolder and `.wad` in the folder and gives each a descriptor
(`FUN_0041dc80`). An item without `<stem>.sam` in its own wad is dropped in every game type (`FUN_0041f170`,
`0x00413b44`). **In game type 2 (`DAT_00fb3b7c`) an item is catalogued only if its own wad holds
`Easy_<stem>.sam`** (`FUN_0041f190`: `"Easy_"` from `0x00747930`, the stem, `".sam"` from `0x00747928`); otherwise
its descriptor is deleted and never catalogued (`0x00413ac4`..`0x00413b3a`). The wad lookup lowercases both sides, so
`Easy_fries.sam` matches `fries`. A type-2 duplicate name is neither catalogued nor freed.

`FUN_00413c10` then parses into the descriptor (`FUN_00412c20`, the schema at `0x00744b30`), in order: its folder's
category file, whose parse first zeroes every slot (`FUN_004013e0`, flag nought at `0x00413e02`); in game type 1
`Online_<Category>.sam`; the item's own `.sam` (`0x00413f06`); then, if the wad holds it, `Online_<stem>.sam` in type 1
or `Easy_<stem>.sam` in type 2 (`0x00413ffe`..`0x0041404d`). Every store overwrites, so a key declared twice keeps the
later value. The zeroing gives an unbounded key nought and a bounded one its lower bound (`0x0040153a` for an int,
`0x0040156b` for a float; the bound is `[lo, hi)`), so `Info.NewAttractionDecayTime` is 1 where no file sets it and
`UsageInfo.ExciteFactor` 50. **A parse that fails in any item's file** (a refused value: out of its bounds, or negative
in a type-5 or type-8 key) makes `FUN_00413c10` return nought (`0x00414061`), which ends the category's walk and
`FUN_00413140`, and state 9 returns 2: the game quits, with an error box naming the file at exit (`FUN_00405710` code
3, `FUN_00405750`). So does a 331st item (`0x00413cfa`, "Too many rides loaded").

Lost Kingdom, measured across all its 70 item wads: 50 hold an `Easy_` file and 20 do not. The 20 are Chac Atak, Gorilla
Thrilla, Sun God, Jurassic Tours, Eruption (`coaster1`, `coaster3`, `incagod`, `tourride`, `volcano`), the Gift Shop, the
Steak Restaurant, the Arcade (`arc2x3`), nine features (`5x5rck`, `5x5rck2`, `lavspurt`, `lure`, `mamfount`,
`speaker2`-`4`, `statue2`) and the three upgrades. The 12 rides' `Easy_` files set `Upgrades[0..2].WearRate` (3, 2, 1;
1, 1, 1 for `minecart` and `wateride`) and `Upgrades[1..2].CostOfResearch` 0, and `minecart`'s `Research.Group` 2; the
other 38 hold comments only. `Easymode.TPWI` places none of the 20. The original runs that park only in type 2 (Q178's
note, `FUN_005c8190`; its `DAT_00fb3b7c` reads 2 in that park, measured under Proton, Q178b).

**The item schema at `0x00744b30`**, read record by record (Q178b): 131 records to the type-`0xc` end, a record's int
bounds at `+0x24` and `+0x28`, its float bounds at `+0x2c` and `+0x30`, an array's count at `+0x34` (`Upgrades` 3,
`SupplementalMeshes` 8, `Attraction` 3, `SignTextures` 2). No key is type 8 or 9. Only two bounded keys have a lower
bound above nought: `Info.NewAttractionDecayTime` `[1, 1000)` and `UsageInfo.ExciteFactor` `[50, 200)`; every other
type-6 key's is nought. **`FUN_004017a0` refuses a line** whose key the schema does not name (`FUN_00401280`,
case-sensitive), whose whole-number value is not an optional `-` and digits, whose float is not digits with at most one
`.`, which is negative in a type-5 key, or which is outside a type-6 key's `[lo, hi)`; a block's rows are its value.
None of the 128 item descriptions in Lost Kingdom's item folders (five category files and `Online_Rides.sam`, and in
the wads 70 own, 50 `Easy_`, 2 `Online_`) holds a refused line. The `Coaster.sam` in `coaster1`, `coaster3` and
`minecart` is not one: it names track textures (`asTextureData[i].pcTextureFilename`), keys this schema lacks.

OpenTPW (Q178b): `Level.InstantAction` gives both the balance's `Easy_Standard.sam` and the catalogue's gate; in it
`ParkItemCatalogue` leaves out an item whose wad has no `Easy_` file and lays the file over the item's own
(`ItemDescriptionFile.Overlay`), 50 items. `Info.NewAttractionDecayTime` falls back to 1. `Upgrades[0].CostOfResearch` is
read (the researched seed, Q201b); the other research keys and the wear keys are counted (`ITEM_RESEARCH_KEYS`,
`ITEM_WEAR_RATE`). **A refused line leaves out what its file describes - the item,
or the whole folder for a category file - logged with the file's name and counted once (`ITEM_VALUE_REFUSED`), where
the original quits** (Alexah, 2026-09-30).

---

## The state machine

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0054e360` | `Game_StateMachine` | Switches on `DAT_0087906c` | Decompiled |
| `DAT_0087906c` | — | The **current** state | Decompiled |
| `DAT_00879088` | — | The **pending request**, not the state; only read inside the park-run case | Decompiled |

States: **1** lobby load, **2** lobby run, **3** lobby unload, **9** park LOAD, **0xa** park RUN, **0xb** park UNLOAD, **0xc** whole-game shutdown; **5/6** and **7/8** are movies, **0xe** is save/load, **0xf** is the post-load first frame. Pending values: **1** = load from save, **2** = exit to lobby, **3** = quit.

The lobby is **torn down completely** at the lobby-to-park boundary (front end, lobby object, advisor, all sound, particles, all sprite banks) and state 9 rebuilds each. The **park run loop is a fixed 31 ms step with a 2000 ms catch-up clamp**. The park loading bar's budget is **500 steps**.

Two steps of the park load each do two things:

| Address | What it does |
|---|---|
| `FUN_00457a90` | Loads the terrain model, `base.md2` under `%s\Terrain` (`FUN_004504c0`), and the advisor's models (`FUN_00429ba0` on `%s\Global\Advisor` and `%s\Advisor\%s`), then resets the advisor (`FUN_005989c0`) |
| `FUN_00457c30` | Loads `Base.map` (`FUN_00450900`, `%s\Terrain\Base.map`) and sets the viewport and screen globals |

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

With 4:3 the rays are `(+/-1.333, +/-1.0, 1)`: **vertical half-angle 45, horizontal 53.13 → vfov 90, hfov 106.26.** OpenTPW's `Camera` takes a camera mode's `FieldOfView` as its vertical angle at 4:3, and both park camera modes ask for 90.

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

The reason is structural: separating a vertical 90 from a horizontal one means locating vanishing points one to seven thousand pixels *outside* an 800x600 frame, from edges whose directions differ by one or two degrees, in JPEG. The tooling (`lines.py` detector — proven good, 0.87 deg mean miss against truth; `fit.py`, `group.py`, `joint.py`, `truth.py`, `view.py`, and `capture_control.py` for the control frame) lived in that session's scratchpad and is gone; rebuild it from this description if the question is reopened.

**The qualitative signal is withdrawn.** "The original shows no sky and OpenTPW does" is explained by **content, not lens**: the original parks are full of rides, shops and trees that stop the eye at the horizon, and OpenTPW's park, when this was measured (2026-09-13), was bare ground plus `base.MD2` scenery. It is not evidence about the FOV.

Two cautions from how this went. An earlier check listed the two reference folder *names* and concluded from them that there were no park shots; that was luck rather than judgement — **open the files**. And a control with a known answer is what caught all four failures above — **build the control first**.

---

## The save container

The preamble's fields are laid out in FileFormats `saves.md`, "Header", by the loader's own reads (`FUN_00416240`;
`saves.md` here, "Preamble"), and all nine park files here agree with it. Measured on the shipped
`data/levels/jungle/Easymode.TPWI` (38,479 bytes): `BILZ` at `1549 = 0x60D`, `+28` header = `0x629` where the zlib
stream (`78 9c`) starts; the dword at `0x611` is 1,608,309, the inflated size; `1549 + 36930 = 38479`, the file
length. **The 28-byte header *includes* the 4-byte tag** — an easy off-by-four. The four dwords at `0x619` read
15, 9, 0, 0, meaning unknown.

**No height array is stored in a save.**

`mTileData` is reachable: emulating `FUN_005179c0` against the real payload shows the World block reads a **per-tile map array** after its 26-field header and the 5,522 bytes of object controls, staff pool, clock and arrival block (`0x005181e7`, `0x00518202`; the map reader is `FUN_004d7ea0` at `0x00518221`), thousands of records of

    mType | mDirection | mFlags | mMeshInstance | mNeighbours | mOverlapCounter | mParentID |
    mTileData | mHoardingNeighbours | mLitterScript | mLitter | save_status_byte

so `mTileData` is one named field per tile. **It is three dwords - tile set, tile index, rotation in degrees - and carries no deformation** (`park.md`, "`mTileData` is three dwords"); the tracer reports the exact byte offset every field is read from.

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

What OpenTPW's orbit camera shows past the ground's edge is the **fog-cleared background**, measured at exactly jungle's `ThemeEngine.FogColour` **4774136** (`0x48D8F8`) with **one unique colour and zero standard deviation** across the strip. Not sky.

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
| `FUN_004033a0` | — | Raw time source for the clock scripts and channels read: last raw reading `+0x10`, a double accumulator `+0x18`, each delta scaled by the rate double at `+0x20` (`0x004033c5`) |
| `FUN_00402f40` | — | Raw time source for the second stopwatch, on `clock+0x50`: last raw `+0x50`, accumulator `+0x58`, unscaled |
| `0x00403030`, `0x00402f80` | — | The two pause call sites inside `GameClock_Pause` |
| `FUN_004030d0` | — | "Elapsed": `raw - 0x2c` running, `0x28 - 0x2c` paused — i.e. **frozen** |
| `FUN_004030c0` | — | IsPaused |
| `FUN_00402ea0(rate)` | — | Enters the fixed-step latch: `+0x40 = 1000 / rate`, `+0x3c = elapsed + 0x44` |
| `FUN_00402ef0` | — | Advances `+0x3c` by `+0x40` |
| `FUN_00402ed0` | — | Leaves the latch, setting `+0x44` so time is continuous across the switch |
| `FUN_00402e60` | — | The save's snapshot: `+0x4c` = the clock's reading (`0x00402e6b`), `+0x74` = the second stopwatch's (`FUN_00403150`, `0x00402e73`); the `KOLC` module holds both |
| `FUN_004031f0` | — | Makes the clock read `+0x4c`: `+0x48 = +0x4c - FUN_00402f10()` (`0x004031fd`). A load reads `+0x4c` and `+0x74` back (`FUN_00402e30`: `0x004031c2`, `0x00403132`) and calls this through `FUN_00402e80` (`0x00415193`), which also makes the second stopwatch read `+0x74` (`FUN_00403160`, `0x0040316d`), unless `FUN_00414d40` runs in mode 1 (`0x004150ae`), so a saved script's deadlines (`+0xa0`, `+0xa4`, `+0xc4`) and a channel's stamps keep their meaning (`park.md`, difference 4) |
| `0x0054f40b`..`0x0054f44f` | — | The first park frame after such a load: with `[0x00879070]` set (`0x00415189`) it re-bases the tick loop's `last` to the restored clock (`0x0054f425`) and clears the flag, so the jump runs no catch-up |
| `0x0040c3b0`, `0x0040c3c0`, `0x0040c3d0` | — | Scale the clock's rate by 0.8 or 1.25, clamped to 0.25..2.0, or set it to 1.0 (through `0x00403340`, `0x004032f0`, `0x004032d0`); reached from the "game" key table at `0x00748028`, entries 6 to 8. Whether a shipped key reaches them is not established. The rate is not saved |

Three layers of clock state:

    +0x10/+0x18/+0x20, +0x28/+0x2c/+0x30   the clock, over FUN_004033a0    } paused together by GameClock_Pause
    +0x50/+0x58, +0x60/+0x64/+0x68, +0x70  a second stopwatch, FUN_00402f40 }
    +0x38/+0x3c/+0x40/+0x44   the fixed-step latch

Stopwatch fields: `+0x30` = paused, `+0x28` = the time captured at the pause, `+0x2c` = accumulated offset (`FUN_004030d0`); the second stopwatch's are `+0x68`, `+0x60` and `+0x64` (`FUN_00403010` on `clock+0x50`), with its offset `+0x70`. Both are the same code over different sources, but only the clock's source is scaled by the rate, so the two part whenever the rate is not 1. While latched, the clock READS `+0x3c` instead of real time.

**The park enters the latch only while `[0x00878128]` is set** (at `0x0054f455`; otherwise `0x00402ed0`). That global is written at `Boot_Init` `0x0054defc`. Almost certainly recorded/replayed play; **nothing offline sets it**.

### The beat is 31 ms and it is derived, not rounded

`1000 / rate` is integer division and the park passes **`0x20` = 32** (`PUSH 0x20` at `0x0054f45f`), so the step **truncates to 31**, not 31.25. Both loops then add `0x1f` by hand.

    lobby 0x0054e74f   now = Clock_Read(); if (now - last > 0x1f4)  last = now - 0x1f4   (500ms)
    park  0x0054f47a   now = Clock_Read(); if (now - last > 0x7d0)  last = now - 0x7d0   (2000ms)
    then:              while (now > last) { last += 0x1f; <tick> }

**A pause is nothing but a frozen clock — neither loop tests a paused flag.** A clock that stops reporting new time leaves `now > last` false, so no tick runs and everything the tick drives stops together without any of it knowing why. This is the single most useful fact on this page.

### The park clock loses precision with uptime

The park loop's clock sample (`0x0054f47f`, stored at `0x008786bc`) comes from the game object `0x00785970` through
`0x00402d70` → `0x004031e0` → `0x00402f10` → `0x004030d0` → `0x004033a0` (the pause layers sit in the middle).
`0x004033a0` reads the raw clock, takes the difference from `+0x10`, stores the new raw value there, and then
`FILD` difference, `FMUL [+0x20]`, `FADD [+0x18]`, `FSTP [+0x18]`: **an accumulator kept as a double, added to at the
FPU's current precision.**

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x004033a0` | — | Game-clock step: `+0x18 += (raw - +0x10) * +0x20` in x87 | objdump of `testme.exe` (Ghidra cross-check owed) |
| `0x00785970+0x18` (`0x00785988`) | — | The accumulator (double). Read live: 558,586,560 at 6.5 days of uptime, a multiple of 64 | `/proc/pid/mem` |
| `0x00785970+0x20` (`0x00785990`) | — | Scale (double), 1.0 in a park | `/proc/pid/mem` |
| `0x005f5f10` | — | Raw clock: `(QPC - start) / divisor + offset` by 64-by-32 `IDIV`; `timeGetTime()` when QPC fails | objdump |
| `0x00fa71e0` | — | Its object: `+0x0` QPC flag, `+0x4` divisor (10000 for a 10 MHz counter), `+0x8` offset (`timeGetTime` at start-up), `+0x10` start QPC | `/proc/pid/mem` |

**The consequence:** the raw clock is milliseconds since boot, and so is the accumulator. Direct3D without
`DDSCL_FPUPRESERVE` puts the thread's x87 unit at 24-bit precision (Wine does the same), so the accumulator can only
hold multiples of 2^(e-23), where 2^e ≤ its value: 64 ms from 6.2 to 12.4 days of uptime. A frame's time much
shorter than that is rounded away, and a slightly shorter one is rounded up. Measured at 6.5 days of uptime, with the
tick counter `0x00877d34` against the wall clock: uncapped, about 275 fps, 0 ticks in 30 s (the park froze); at 30 fps,
44.5-49.6 ticks/s, the clock at 1.38-1.54 times real time; at about 4.5 fps, 32.9 ticks/s (real time). A second
thread writes the same accumulator at full precision, so the values are not always multiples of 64, and the
measured speeds fall short of a pure rounding model. The 24-bit mode itself is
inferred from the multiple-of-64 value and Wine's documented ddraw behaviour; it was not read from the FPU.
OpenTPW's `GameClock` does not copy this, and should not (`CLAUDE.md`, "A deviation from the original is said at
the site").

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

**Every SCREEN-DRIVEN call site passes `PUSH 0x0; PUSH 0x0`** — six of them (`0x0047f26c`, `0x0049f29f`, `0x004a93d7`, `0x0048c87e`, `0x0049efba`, `0x004a3a6c`) — so for a menu, a message box or the options screen the quiet flag is 0 and the sound half of a pause is `Advisor_PauseVoice()` + `FUN_0051c1c0(1)` (which only writes `DAT_00803ad2`). **So a park's music keeps playing under the menu.**

**But the window procedure passes `(1, 1)`.** `FUN_0046b600`'s `WM_ACTIVATEAPP` branch calls `Game_Pause(1,1)` at `0x0046b74c`, and arg2 non-zero takes the **voice-pausing** path `FUN_0051bcf0` and never touches the listener. So that path **is** taken offline, on **alt-tab**. (One site, `0x005f0b7f`, pushes `EBP` twice and its value was not established.)

`FUN_0051bcf0` / `FUN_0051bd30` post commands differing only in payload address (`0x0070a268` vs `0x0070a270`), whose handlers `0x006b8e40` / `0x006b8eb0` are byte-identical but for one operand — `CALL [EDX+0x38]` against `CALL [EDX+0x3c]`, two adjacent virtual slots on the same sound object. That shape is a **suspend-all / resume-all pair**. Which matters for more than accuracy: **the original owns a primitive that holds the whole mix, spends it on losing focus, and pointedly does not use it for the park menu** — the menu takes the listener branch instead. That is the best evidence available that a park menu was never meant to silence everything.

### What the 31 ms tick drives

The park's loop runs from `0x0054f4bf` onward, with the tick counter at `[0x00877d34]` and each section bracketed by the profiling timer `[0x006fd1d0]`. It calls `Particles_Tick` first and then about fourteen more.

| Address | Original name | What it is | Cadence |
|---|---|---|---|
| `0x00520130` | `Particles_Tick` | The *entire* body of the lobby's tick loop, and first in the park's | Every tick |
| `FUN_00546c80` | — | The track-ride tick, gated on `DAT_00877b58 == 0x4a454647`: each live car's events (`FUN_005474b0`, the timer and unload: `park.md`, "How a bumper ride ends a go") and step (`FUN_00547f50`), then a pairwise avoidance pass over the stride-`0x2b` car pool | Every tick |
| `FUN_005516b0` | — | The RSSE thing/script engine (named by its own assert string) | Every tick |
| `FUN_0051e790` | — | The crowd-driven music level, called at `0x0054f870` | **Every 32nd tick** (`TEST [0x00877d34],0x1f`, `0x0054f82d`) |
| `FUN_00475360` | — | A clock-driven task scheduler: reads the clock, runs entries whose `+0x7c` is past, reschedules `+0x7c = +0x80 + now`. Its own default interval `+0x80 = 0x3e = 62`, so a default sprite steps once per call | **Every 2nd tick** |
| `FUN_0055abf0` | — | **The flying cars** — named by its own strings, `"FLY: Failed to trigger a flying car anim (%ld)"` at `0x007660e8` and `"FLY: No headnodes on car mesh"`. It is **not** the guest/peep simulation | **Every 2nd tick** |

`0x0054f5c0` reloads the tick counter from `[0x00877d34]` and `TEST AL,0x1` / `JNZ` skips the last two on odd ticks, so they run **every 2nd 31 ms tick = every 62 ms**.

The real peep module is `0x004f9000`-`0x00512000`, **281 functions / 95,152 bytes**, plus a queue module at `0x004dd000`-`0x004e2000` (89 functions / 19,684 bytes).

**Which tick drives the peeps, in both halves.** The peeps are **simulated** off the every-8th-tick thing sweep — `FUN_00516380` → `FUN_0050b360` behind the gate at `0054f668` — and they are **placed for drawing once per FRAME** by `FUN_00518f90`, called from `0x0054fa85`, which lies past the 31 ms catch-up loop's back edge at `0x0054f8da`. So neither answer alone is right: the position is stepped on the 248 ms beat and interpolated to the frame. Full decode in `ride-operation.md`, "Where a WALKING peep is drawn". **`mGameTick` (`[0x0080239c] + 0x1da70c`) counts those sweeps**: `FUN_00516380` increments it (`0x00516394`) and is called at `0x0054f7bb`, inside the block the every-8th gate skips (checked 2026-09-23, Q36). So every peep comparison against it - a guest's 30-sweep thinking gap in `FUN_004fec90` among them - is in thing sweeps, and so is every one a member of staff makes (`ride-operation.md`, "The staff turn").

**A sweep that cannot run is dropped, not owed.** The step counter and the baseline are moved before anything is tested (`0x0054f4c7`, `0x0054f4d6`), so nothing makes a lost sweep up later. At most three sweeps run in a rendered frame: `[0x00879064]` counts them (`0x0054f680`, `0x0054f696`), is reset each frame (`0x0054fc2a`) and in the routine the park's load registers at `0x0054ecbc` (`0x0054e32f`), and a step past the third jumps to `0x0054f828`, which still runs `0x0055a470` and the every-32nd block. The loop keeps no more than 2 s of backlog (`0x0054f49b`). And an inactive full-screen window skips each whole step (`0x0054f4d4`-`0x0054f4e5`; `weather.md`), which in an ordinary park, paused on losing focus (`0x0046b74c`), costs only the steps still owed. The every-30th test at `0x00516453` and the every-100th at `0x004d7b4f` count sweeps too. **OpenTPW** (Q126): `ParkPeople.OnUpdate` counts the frame's sweeps and drops a step that comes due with `SweepsAFrame`, three, already run; the tick number moves on and `ParkState.GameTick` does not. Measured, the game's process stopped 2.5 s and the park clock read before it and half a second after, three times each: the original `+5`, `+5`, `+5` (`mGameTick` at world `+0x1da70c`); OpenTPW `+5`, `+5`, `+6`, its log "3 run and 5 dropped in a frame of 64 ticks"; before the cap `+10`, `+11`, `+10`. The sweep is all the skip passes over: between `0x0054f68f` and `0x0054f828` are a first-use guard, the game mode's dispatch (`[0x00fb3b7c]`: the online mode's sweep goes through `FUN_005166b0`) and a timing ring around the call. `[0x00878a1c]`, the peeps' drawing baseline, is moved before the test (`0x0054f683`), so a dropped step still restarts the interpolation, as `ParkPeople.ThingTickFraction` does. The scripts and the sprites, which run before the gate, are capped in neither game. **Still parted**: the original's calendar, weather and day's, month's and year's work are inside the sweep (`FUN_004d7b20` at its end, `FUN_00512880` from the thing turn) and are dropped with it, where `GameCalendar` carries its advances over and so now runs five ahead of `ParkState.CalendarNow` after a 2 s stall (Q149); the tick number handed to the things jumps over a dropped step (Q132); and `FUN_0055a470`, which runs on every eighth step dropped or not and picks a sound setting by the ride the camera is on, is not built (Q157).

**The music's level** (Q129, the three instructions re-read first-hand). On a step whose counter has its low five bits nought (`TEST [0x00877d34],0x1f`, `0x0054f82d`), every 992 ms of game time and never while no step runs, the loop takes the crowd's level (`FUN_004c81e0`, half the counted guests, to 100), holds it to 89 (`CMP EAX,0x59`, `0x0054f84e`), makes it nought while the world's state `+0x1da738` is 4 (`0x0054f860`) and hands it to `FUN_0051e790`, which sets parameter 4 of the music's voice (`FUN_0051bc40`, into the sound library through `FUN_006b5b80`'s virtual call). **OpenTPW**: `ParkAudio.MusicLevel` and `SetsMusicLevel`, asked in `OnUpdate` of every tick the frame ran; the voice starts at nought, as the original turns it down as it starts it. Measured in Lost Kingdom: thirteen guests, level 6; 20 sets in 646 ticks; `load 190`, the level half the guests up to 89 at 179 and 89 still at 203, where before it was set every frame and read 96 and 100. Not measured in the original: its level is a field of the sound library's voice, behind that virtual call, which is not traced, and nothing in the stock park takes it past 6. Which guests are counted is Q148's.

**Entering a park re-bases the baselines**: `0x0054ed7c` reads the clock three times into `[0x00878c74]`, `[0x0087879c]` and **`[0x00878a1c]`**, so the seconds spent loading are not owed as ticks. *(`0054eda4` is `a3 1c 8a 87 00` = `MOV [0x00878a1c],EAX`. `0x008786bc` is the per-frame clock SAMPLE all three alphas are measured against, not a baseline, and `0x008786c0` — one along — is written at `0054edb6`. The three baselines pair with the three rates 1/31, 1/62 and 1/248.)*

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
| `FUN_0040cfa0` | Switches **five of the six** tables off: zeroes each table object's `+0x06` enable (`0x0040cfb0`, `0x0040cfd7`, `0x0040cffe`, ...) and walks its rows at stride `0x14` zeroing each row's latch — system, game, camera, cheat and shortcuts. **`coaster` (`DAT_00787294`) is touched by neither it nor `FUN_0040cf60`**, so coaster bindings survive a "take the keys away". `FrontEnd_ShowPlayerSlots` calls it (`0x004a6599`) |
| `FUN_0040cf60` | Sets `+0x06` of each *table object* back to 1. `FrontEnd_ClosePlayerSlots` calls it (`0x004a6a98`) |
| `FUN_00486b60` | The "give the keys back" path: `FUN_0040cf60(); DAT_007c24d0 = 0;`. Its siblings are `FrontEnd_ClosePlayerSlots` and four unnamed sites near `0x0048a8xx` |

**OpenTPW has no table to switch off**, which matters before blaming a binding that does not fire: each reader asks
what the gate would have said. While one of a park's screens is open the camera, cheat and game tables are off and only
the shortcuts' is run ("A park screen is open"), which here is `WindowStack.ParkScreenOpen`, asked by `Level.BuildKeys`,
the orbit camera's keys and `ParkFrontEnd.ToggleFullScreen`.

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

Both posters are called from the one window proc `FUN_0046b600`, which then matches the **system** table
(`[0x0078718c]`) itself and no other (`0x0046ba89`, `0x0046bb7f`); the other tables are run by a park's controls:

    WM_KEYDOWN 0x100 / WM_SYSKEYDOWN 0x104  ->  FUN_00658c38 (0x1000a)  ->  FUN_0040c900  (+0x10, latch 1)
    WM_KEYUP   0x101 / WM_SYSKEYUP   0x105  ->  UI_PostKey   (0x1000b)  ->  FUN_0040c990  (+0x0c, latch 0)

So the system table's four live rows - `P`, Ctrl+H, F8 and Ctrl+Shift+Alt+F8 - act on the release in either scene
(`lobby.md`, "The lobby's keys act on the release").

Since `+0x10` is null in every row, the down path invokes nothing and only latches; **every handler in the game hangs off `+0x0c`, which only the key-UP path calls.** The Alt chords work because Windows sends them as SYSKEY messages, which route to the same two paths. Keys reach the table as **ASCII, via `MapVirtualKeyA(vk, 2)`** (so `'C'` is `0x43`, Escape `0x1b`, Backspace `0x08`), and extended keys are stored shifted: `(vk & 0xff) << 8`, which is what the `0x2600`/`0x2800`/`0x6d00` entries in `camera` and `game` are.

`FUN_0040c870(key, mods)` returns the action id, and two callers cross-check the table decode independently: `FUN_00488a00` tests `!= 0x10` (action 16, camcorder) and `== 0xf` (action 15, postcard, which then calls `FUN_004a9380`).

**Deliberate deviation in OpenTPW:** each `[DefaultKey]` is split into `Modifiers` and `Keys`, `BindingMatches` compares held modifiers for **equality**, and `PressedFrom` also requires one of the binding's own keys among the frame's `KeysPressed`. `Clone` being Ctrl alone is a deliberate held state, not a binding bug.

### The full-screen view: F3, `FUN_004a29d0`

Decoded for `docs/QUEUE.md` Q114, and pressed in the original.

**The key is F3.** `game` row 4 is key `0x72`, modifier 0, handler `0x0040c4b0` (the row at `0x00748078`, on disk
`04 00 72 00 00 00 ...`). `0x72` is `VK_F3`: a function key has no character, so `MapVirtualKeyA( vk, 2 )` answers
nought and the key code is the vk itself (`lobby.md`, "The key code"), as the `system` table's F8 is `0x77`. The handler
is the usual 16-byte thunk into `0x00481490`, a bare `JMP FUN_004a29d0` (`e9 3b 15 02 00`) that no cross-reference
names. **F2 (`0x71`) is bound in none of the six tables.** No gadget button and no menu row reaches the toggle.

**What `FUN_004a29d0` does.** Nothing unless the park's interface is up (`DAT_007cb2ac`). Its state is `DAT_007cb2e8`
(`FUN_004a2a90` reads it).

- **Off to on.** Refused while `gui_CameraFlags & 0x16` (`0x004a29e6`): first person, a ride view. Otherwise it makes a
  full-screen control on the UI root, id `0xddf22`, (0,0)-(2047,1535) (`FUN_0065dd7b( 1, 1, ... )`, kept in
  `DAT_007cb2b4`), gives it the handler `0x004a2840` and the focus (`FUN_0065e59b`), and hides layer 0
  (`UI_SetVisible( layer 0, 0 )`, `0x004a2a6b`). Layer 0 only: the gadget, the money counter, the help bar and any
  park screen on it go; the pointer stays.
- **On to off.** Deletes the control (its vtable `+8`), shows layer 0, and hands the focus back (`FUN_004862a0`).

**While it is on, the control's handler `0x004a2840` is all that hears the player.**

| Message | What it does |
|---|---|
| every one | to the camera's mouse reader `FUN_0042a760` first (`0x004a287d`) |
| a press `0x10005`, a release `0x10004` | button 1 takes and lets go of the pointer capture; the button goes to the `camera` table as a key (`button - 0x10`), where no row binds one; then the base proc |
| key down `0x1000a` | latches the `camera` table (`FUN_0040c900`) |
| key up `0x1000b` | runs the `camera` table (`FUN_0040c990`). Then, by lookup (`FUN_0040c870`, which does not read a table's enable gate): `game` action 4, or Escape with no modifier, turns the view off (`0x004a2990`); else `shortcuts` action 15, Ctrl+P, takes a postcard (`FUN_004a9380`) |

So the camera keys, F3, Escape and Ctrl+P work, and nothing else: no shortcut opens a screen, and no press reaches
`Park_MouseMessageProc`, so nothing is picked, built or armed. **Escape there puts the interface back and opens no
menu.** While the world's state (`+0x1da738`) is 4, the end of the park, the key-up arm reads neither F3 nor Ctrl+P:
a plain Escape opens the game menu (`GameMenu_Open( 0 )`, `0x004a2942`) and the view stays on.

**Everything else that calls it turns it off, or belongs to the end of the park.**

| Caller | What it does |
|---|---|
| `FUN_004815d0`, from the save loader `FUN_00414d40` (`0x00415088`) | off, if on |
| `FUN_004a9180`, the postcard screen's close (`0x004a923a`) | off, if on, when `DAT_00fb3b7c` is not 1 |
| `FUN_0048ac40` (`0x0048aca4`), from the end-of-park routine `FUN_005168f0`, which sets the world's state to 4 and passes the park gates | off, if on. Then it leaves first person or a ride view, calls `FUN_004e15b0( 0 )`, takes the keys away (`FUN_0040cfa0`), closes the open screen, hides layers 0 and 1 itself (message 6) and puts up a full-screen control, id `0x7472`, handler `0x0048a740`, holding stream `0x0074fb20`: one control, id `0x5a`, (174,500)-(1874,1000), mesh `0xf3a7aff4` |
| `FUN_0048ae70` (`0x0048aed4`), from `FUN_00516b00`, which makes the "End" feature | the same, with stream `0x0074faf0` (id `0x321`, (102,536)-(1946,940), mesh `0x706de86b`) and handler `0x0048a970` |
| the handler `0x0048a740` (`0x0048a7f3`, `0x0048a8d8`) | **on.** A right click with RMB cancel on, 2000 ms or more after the control was made, or a plain Escape: it sends its control message 4, leaves the ride view (`FUN_0042a190`), turns the full-screen view on, loads `0x0074fb20` again into the view's own control, and gives the keys back (`FUN_0040cf60`) |
| `FUN_0048adb0` (`0x0048addc`), from `FUN_004815e0` in the load's rebuild `FUN_00415140`, when the saved state is 4 | **on**, if off, and `0x0074fb20` loaded into it |

So an ended park is left in the full-screen view under its banner, and by the handler's state-4 arm nothing brings
the interface back. The two meshes' names are not resolved, and the end of a park was not run.

**In the original** (the reference park under Proton, the state read from memory after each key let go, a frame
grabbed with it): F2 changed nothing; F3 took `[0x007cb2e8]` 0 to 1, `[0x007cb2b4]` from nought to a control, and bit
0 of layer 0's `+0x48` 1 to 0, the gadget, the money and the help bar gone from the frame; F3 again put all three
back; F3 then Escape put them back with the game menu (`[0x007c2534]`) still shut; F3 with the camcorder tool armed
turned it on all the same. Each as predicted from the listing. The refusal in first person was not reached (the
click that enters it missed twice), so it is the listing's alone.

**Under the view in the original** (`docs/QUEUE.md` Q114b; the reference park under Proton, each predicted from the
listing and read from memory after the key or the click, a frame with it): B opened no screen (`[0x007c24c8]` stayed
nought, where the same key with the view off set it); a left click on grass installed no tool (the mode object
`[0x007b05d8]` and its vtable unchanged, where the same click with the view off installed the path tool, vtable
`0x006fe9e0`); C changed neither the mode nor `gui_CameraFlags`; the Left arrow turned the camera a quarter turn; Escape
took the view off with the menu shut. With the path tool armed first, F3, a click and Escape left its mode object
standing, and the next Escape put it away. In those frames the pointer is the plain arrow, the armed tool's square is
still drawn, and the advisor is drawn while he speaks: he is not on layer 0.

**OpenTPW builds it** as `ParkFrontEnd.ToggleFullScreen` over `WindowStack.Covered`: F3 let go with no modifier
toggles it, refused in first person; on, nothing of the window stack is drawn or pointed at, every press is the
cover's, and `Level.BuildKeys` and the orbit camera's C are not heard; the camera's keys and wheel are; F3 or a plain
Escape takes it off and is spent doing it; Ctrl+P is counted, `FULL_SCREEN_VIEW_POSTCARD`. The pointer wears the plain
arrow. Where it parts from the original:

- **Over a window that has the keys F3 does nothing**, and that is the original's: the game menu, a message box, the
  options screen and the map are not on layer 0 and keep their keys, and a park screen, which is on layer 0, has the
  focus, with the `game` table off and a handler that runs the `shortcuts` table alone ("A park screen is open"). F3
  over the entry-price screen left `[0x007cb2e8]` at 0 in the original.
- **The armed tool's squares still follow the pointer** under the view, since the pointer's cell is picked every frame;
  the original's mouse proc hears nothing there, so its square should stand still. Not measured.
- **The end of a park** turns the view on and off and gives Escape the game menu under it; no park ends here.

**F2 stays, at Alexah's word** (`docs/DECISIONS.md`): `InputButton.HideUI`, OpenTPW's own key beside F3. Measured
before the build, each predicted from the code: it acts on the press, in the lobby too; it hides the pointer with the
interface; under it B does nothing, a left click on grass arms the path tool, Backspace and a quick right click reach
the park, C enters first person, Escape does nothing at all (not even leaving first person), and F2 in first person
hides the viewfinder. `RootPanel.Hidden` stops the interface drawing and updating, and `Level.RightPressTaken` gives
the park every right press while it is set.

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
| `research` | `FUN_004aa480` |
| `map` | `FUN_005f0b40` (ECX = `0x00F86DB8`) |
| `staffloc` | `FUN_004b3f60([0x007c2658])` if `[0x007cc3c0]`, else `FUN_004b3f60(0)` |
| `peeploc` | `FUN_004b3f60([0x007c2658])` if `[0x007cc418]`, else `JMP FUN_004b4280` |
| `postcard` | `FUN_004a9380` |
| `camcorder` | `FUN_00481a10` |

**`-1` is a REMEMBERED TAB, not "nothing selected".** `FUN_0049bdd0` rebuilds the screen when `param_1 == -1`, then does `if (param_1 == -1) param_1 = DAT_007ca30c;` and switches five ways on that (cases 0-4) to choose both a title string (`0x8b`-`0x8f`) and a control id (`0x2490`/`0x2493`/`0x2492`/`0x2494`/`0x2491`). So hire has **five tabs** and `-1` means "open on the tab it was left on"; passing a real index when the screen already exists only re-titles and re-selects instead of rebuilding. **An earlier guess that `-1` meant "no item selected" was wrong** and is recorded here so it is not made again.

**The staff and visitor locators are ONE shared panel**, `FUN_004b3f60`, differing only in which global they test (`0x007cc3c0` vs `0x007cc418`, `0x58` apart — sibling objects). It switches on a type byte at `param_1 + 2` for its title (`0x95e`..`0x964`) and treats `param_1 == 0` as nothing selected. It also builds `DAT_007b05e8` exactly as `FUN_00481a10` does, which corroborates that `DAT_007b05e8` is **not** a camcorder-only global.

**`FUN_004b4280` is `peeploc` with nothing selected**, and the staffloc/peeploc asymmetry is cosmetic. It is a specialised copy of what `FUN_004b3f60(0)` does for the visitor flavour: sets `DAT_007cc30c = 6` and `DAT_007cc2f0 = 2`, hides control `0x148` and shows `0x14a` — the exact inverse of the `DAT_007cc30c == 6` branch inside `FUN_004b3f60` — and ends on `FUN_00486b00(0x11f)`, the same id that branch uses.

**REFUTED: `0x007b51f0` is NOT the gadget's object.** Every one of the 17 thunks does `MOV ECX,0x007b51f0` before its call, which looks like the park management gadget's `this` — and it is not. It has **98 references from 44 functions and NOTHING writes it**, and the referrers include `Boot_Init`, `Game_StateMachine`, `Game_Shutdown`, `Game_TogglePause`, `Game_Pause` and `Game_Resume`. It is a global game/application object; the thunks pass it as a generic receiver and the second-layer stubs mostly ignore it. **So it is not a route to how the HUD is built — do not start there again.**

**Trap, hit twice:** none of these stubs is a function to Ghidra, so `decompile` refuses them and xrefs find nothing — the same trap as `FUN_0048b6a0`. Worse, **a hand decode of the displacements got `buy` wrong** (read as `0x0044cc70`, actually `0x004acc70`). Run `api.disassemble(addr)` on the stub and read the target back off the disassembler; **do not compute rel32 by hand.**

---

## Camcorder mode — the first-person view

Camcorder mode is why a park loads a sky the orbit camera shows only as a margin.

| Address | What it is | Evidence |
|---|---|---|
| `0x007485c0` | The string `'camcorder'` | Raw byte search |
| `0x0040c5c0` | Camcorder's first-layer thunk; `CALL 0x00481a10` | Disassembled |
| `FUN_00481a10` | Installs the camcorder interaction mode | Decompiled |
| `FUN_00498ad0` | The camcorder/postcard sub-panel's UI message handler: on message `0x100` it dispatches on the control id, and **id 99 calls `FUN_00481a10`** | Decompiled |

**Note on finding the string:** a scan of Ghidra's *defined* strings missed `'camcorder'` outright; a **raw byte search (`api.findBytes` for `[Cc]amcord`) found it at once. Do that first.**

It is **shortcuts action 16, key `'C'` (`0x43`), no modifier**, and it has two ways in — that key and the gadget button id 99.

The key acts on its **release**, both ways. In orbit the park runs its tables on a key-up (`FUN_0040c990`; `scenes.md`, "The park Escape route"). In first person the viewfinder layer's `FUN_00488a00` latches on the key-down (`0x1000a`, `FUN_0040c900`) and on the key-up (`0x1000b`) leaves first person when the key is `0x1b` or `FUN_0040c870( key, modifiers )` finds action 16; action 15 calls `FUN_004a9380` instead.

`FUN_00481a10` is:

    FUN_00485b40();               // 0x00481a2b: close the open park screen (message 5 to DAT_007c24c8)
    uVar2 = FUN_0046cff0();       // build a mode object, vtable 0x006fead0
    if ( DAT_007b05e8 == 0 ) { ... ensure the holder exists, install the default mode 0x006fea10 ... }
    FUN_0046c350( uVar2 );        // make it the current interaction mode

**The close comes first and is unconditional** (`CALL 0x00485b40` at `0x00481a2b`, straight after the prologue's
pushes), so C or the gadget's button with a management screen or an object window up takes the screen away before the
pick is installed. In the original, read from memory with a frame each: the buy screen open, `[0x007c24c8]`
`0x46bf8d0`, C let go, 0, the mode's vtable `0x6fea10` to `0x6fead0`; a visitor's window, `0x46cad30` to 0, the same;
C with no screen, the same vtable and the global still 0. OpenTPW: `ParkCamcorderCameraMode.Enter` calls
`Level.CloseParkScreen` (`WindowStack.CloseParkScreen`) before anything else, and logs it when one was open.

The "operator new twice behind SEH" part is a **shared prologue**, not camcorder's own work — `FUN_00497bc0` (the coaster builder bar) has it verbatim.

**The handler chain is all read-only.** Entry 16's handler `0x0040c5c0` is one of 23 identical **16-byte thunks** (`MOV ECX,<object>; CALL <handler>; MOV EAX,1; RET`) among the key handlers at `0x0040c3a0`..`0x0040c820`. **None of them is a function to Ghidra** — they are only ever reached through the table's pointer, so `decompile` refuses them and xrefs find nothing. Disassemble them with `api.disassemble(addr)` (the trap above), or create the function; do not compute rel32 by hand. Camcorder's thunk calls `FUN_00481a10`, which happens to be the one call target of the 23 that Ghidra *does* have as a function.

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

**The edge test takes its cell as bytes, and guards the map's edge by equality.** `FUN_004d8750( x, y, dir,
mode )` uses the low bytes of x, y and dir. Its only map-edge guards are x 0 going west, y 0 north, x 127 east
and y 127 south, each answering 1, shut (`0x004d875c`..`0x004d878e`, `0x004d8af6`). The id it reads is
`(y & 0xff) * 128 + (x & 0xff) + 1` in 16 bits, and the far cell's is that −128, +1, +128 or −1 for directions 0
to 3. So a cell off the map is not refused as such: x 128 reads the record of (0, y + 1) as the cell left and
(127, y) as the one west of it, and answers from what they hold (for y 127 the record read lies past the last
cell's). The sweep never carries a viewer there - the x 127 guard shuts the east side, and steps 6 and 7 below
put back every change of cell nobody asked about - and the entry below never puts one there.

**The sweep, pass by pass** (`0x0042bdd8`..`0x0042c5df`; put to five refuters and three judges for
`docs/QUEUE.md` Q48, then re-read by hand). The step is `dt * velocity` for each axis, `dt` being the frame's time
step (one above 200 is taken as 100, and one of 0 or less as 1: `0x0042b2c3`..`0x0042b2ed`). Each axis is first zeroed if it lies within ±1e-4, and if both are then nought
nothing moves. X's lower edge is tested on `dt * velX` still on the x87 stack (`FST` then `FCOMP`,
`0x0042bdf2`..`0x0042bdf6`), every other edge on the stored float. **Ten world units to a cell**, `_DAT_006fdd58` = 0.1. Each pass:

1. **The cell** stood in is `__ftol( position * 0.1f )` for each axis (`0x0042be86`..`0x0042beba`; `0x0067a830`
   truncates), taken afresh every pass and never clamped to the map.
2. **The reach** of each axis - the fraction of what is left of its step that meets the next boundary - comes
   from `frac`, the fractional part of `position * 0.1f` by `modf` (`0x0067b280`): `|frac / (s * 0.1f)|` going
   negative, `|(1 - frac) / (s * 0.1f)|` going positive, **2.0** for an axis not moving. `s * 0.1f`, the
   positive branch's `frac` and each reach are stored as floats. The product `position * 0.1f`, `1 - frac` and
   the division stay on the x87 stack and round at whatever precision is in force (below).
3. **A tie is broken toward Y.** If the two stored reaches are equal and both axes move, the X step is divided by
   `0x0074c9c8` = **1.01** and its reach multiplied by it, and both are kept (`0x0042bff8`..`0x0042c043`). Below a
   reach of 1 this makes Y the side asked, with X stopping about 1% short of its own boundary. The tie is broken
   before the whole step is weighed (4), so a tie at 1 or more takes the whole step with the shorter X. The shorter
   X step lasts the rest of the frame, and shrinks again at another tie.
4. **The side asked** is X's when its reach is strictly the smaller, else Y's. If that reach is 1 or more, the
   **whole step** is taken instead and nothing is asked (`0x0042c460`).
5. **Asked and shut** (`FUN_004d8750` non-zero): that axis is parked in the cell being left - at `cell * 10`
   going negative, `cell * 10 + 9.999` going positive - and its step zeroed. There is no epsilon on the negative
   side, since `cell * 10` is still the cell by the truncation in 1. **This is what makes a viewer slide along a
   wall** rather than stick to it: the other axis carries on.
   **Asked and open:** that axis advances by its reach. If that left the cell unchanged - a landing on the
   boundary itself - it is nudged by `0x0074c9d0` = 0.001 the way the rest of the step goes. Without the nudge a
   step going negative, which lands on `cell * 10`, would measure nought to the same side on the next pass and
   never arrive.
6. **The other axis advances by the same fraction and may not change cell.** If its cell changed, it is put back
   at the `cell * 10` or `cell * 10 + 9.999` of the cell it was in. Its side is not asked and its step is not
   zeroed, so a later pass asks it (`0x0042c197`..`0x0042c24c` after X, `0x0042c389`..`0x0042c42d` after Y).
7. **The whole step keeps the same rule** on both axes: an axis whose cell changed is put back, whether or not that
   side is open, and the loop ends (`0x0042c460`..`0x0042c543`).
8. **The position** is written back (`0x007908f0`, `0x007908f8`; the height `0x007908f4` is zeroed), and the loop
   goes round while either step remains. There is no cap on the passes.

**So every change of cell in the sweep is one the edge test was asked about and allowed**: in any pass only the
asked axis may leave its cell.

| Constant | Value | What it is |
|---|---|---|
| `_DAT_006fdd58` | 0.1 | World units to cells |
| `_DAT_006fdd7c` / `_DAT_006fdd5c` | 10.0 / −10.0 | Cells back to world units |
| `0x0074c9c4` | **9.999** | A positive-going axis is parked, or put back, at `cell * 10 + 9.999` |
| `0x0074c9c8` | **1.01** | The tie-break: the X step divided by it, its reach multiplied |
| `0x0074c9d0` | **0.001** | The nudge after an open crossing that left the cell unchanged |
| `_DAT_006fde00` / `_DAT_006fde04` | ∓1e-4 | A step inside this band is zeroed before anything is swept |

**Which rounding is live is not settled.** The CRT starts the FPU at 53-bit precision (`0x006804da`). The frame
renderer `FUN_00576ec0` switches to 24-bit at `0x00576fa7` and has three exits. On success it restores the saved word
(`0x00577310`), or, when the `0x2` bit of `[0x008bd508]` is set (`TEST AL,0x2` at `0x005772f6`), the saved word with the precision bits set,
64-bit (`0x00577304`, `FUN_00591250` ORs `0x300`); no writer found sets that bit (it starts `0x3000141` at `0x00583555`,
the other writers OR in other bits or put back saved words), so that arm is probably never taken, though an indirect
write is not ruled out. Its failure exit (`0x00577433`) restores nothing, so one failed frame leaves 24-bit in force
for the rest of the run. The thing sweep runs beside the renderer, never inside it, so what it computes sees whatever
the last exit left (Q173). DirectDraw is set up with `DDSCL_FPUSETUP` (`0x00563914`, `0x00563b92`), whose effect belongs to the
runtime, not the executable. It matters only at the margin: at 53 bits a positive step landing exactly on a
boundary, `245 + 5`, has reach 0.99999928 and is asked; at 24 bits its reach is 1.0, it is taken whole, and step 7
puts it back. The cell in step 1 is the same either way. The walking speed's ease (`FUN_004fa870`, `ride-operation.md`,
"Where a WALKING peep is drawn") leaves a fingerprint in the saves. In Alexah's two played Lost Kingdom saves no guest
needs 53 or 64 bits: 93 in each sit on single-precision-only fixed points (1.200000286, 0.600000143), the 21 newest
arrivals all fit single precision (against 15 and 13 at 53 or 64 bits), about 178 fit every precision, and 6 and 7
hurrying guests fit no simple history at any. The shipped `Easymode.TPWI` holds both kinds: its base-140 people at
1.399999857, a 53/64-bit fixed point only, and its mechanic and handyman at 1.200000286, single only. So the played
saves point to single precision and the shipped file's base-140 people do not; OpenTPW eases in single precision
(`Peep.Pace`). Logging the control word at `FUN_0042b1c0`'s entry would settle it.

**Nothing clamps the position; the bound on walking is soft, and it is the heightfield's.** Every writer of
`0x007908f0`/`0x007908f8` was read and none clamps it; the cell clamps (`FUN_0042cd40`, and the one in
`FUN_0042ae70`) are for lookups. The bound is on the velocity (`0x0042b674`..`0x0042b6fb`,
`0x0042bca0`..`0x0042bd5f`): when the position at the start of the frame is below 0 or past the extent and the new
velocity points further out, that component becomes the previous one divided by `1 + 0.01 * dt`. It keeps its sign
and decays, so the drift past the line totals `100 * |v|`, about 2 units for a walker at the default zoom. The
extent is `[[0x007a0854] + 0x6c]`, the heightfield block of `Terrain\base.md2`: `+0x18 * +0x10` across and
`+0x1c * +0x14` down, **960 by 850** in all four parks (96 by 85 cells), not the 128-cell map.

**One branch is decoded but not built here: walking onto a ride's entrance rides it.** Every pass of the sweep, the
whole-step one included, ends at `0x0042c545`: the cell of where the pass left the viewer, `(int)(x * 0.1)` and
`(int)(y * 0.1)` clamped to the map (`FUN_0042cd40`), is handed to `FUN_0042a340` (`0x0042c587`). That answers only for
an **entrance**, a cell of type 9 (`FUN_00536340`, `[cell+8] == 9`): it reads the cell's `mParentID` (`+0x10`), walks
the thing chain of that owner's cell (`+0x24`, the next at the thing's `+0xa`), and returns the first catalogue object
(kind byte `+2` is 3) whose item's `+0x118` is nought. `+0x118` is **`UsageInfo.CannotRide`**, row 11 of the descriptor
key table at `0x00745940` (rows of `0x3c` bytes, row 0 at `+0xec` and four bytes a row, which rows 21, 27 and 28
confirm as `+0x140`, `+0x158` and `+0x15c`); its values in the shipped files are FileFormats `sam.md`'s, on its `docs/sam-and-saves-corrections` branch. On a hit the loop runs `FUN_00412e90( 0x7890a0, item )`, whose
answer it drops, then the thing's `FUN_004e15b0( 1 )`, the ride window's "Ride it!" (`hud.md`) entered from first
person, and ends the sweep (`0x0042c5b6`); the position is still written back. **OpenTPW** has no ride view: `Slide`
asks the same question after every pass (`ParkCamcorderCameraMode.RideAt`, the objects anchored on the owner's cell
standing for the chain) and counts each yes as `FIRST_PERSON_WALK_INTO_RIDE`, twice as the viewer crosses in and then once each frame they step
in the cell, moved or stopped by a shut side, and walks on. In Lost Kingdom the one such cell is the Belly Bounce's entrance
(52,23).

**Nothing of the edge test is kept, and the world it reads dies with the park.** Read for `docs/QUEUE.md` Q10 and
put to a refuter, then re-read by hand. `FUN_004d8750` writes no global, and neither do the functions it calls.
On every call it reaches the world afresh, by three routes:
- `[[0x007cf6ec]]`, in its own body (`0x004d87d1`, `0x004d888e`, `0x004d89f7`). The one writer of `0x007cf6ec`,
  `0x00515350`, runs once from the startup initialiser table and points it at `0x007cf83c`, the world pointer.
- `[0x008023a0]` = world + `0x2d8`, in `FUN_004d0af0` (`0x004d0af5`, called at `0x004d89db`). It adds
  `0x10ffd8`, which reaches the `0x28`-byte records at world + `0x1102b0`.
- `[0x007cf83c]` directly, in `FUN_005363f0` (`0x0053640c`, called at `0x004d89c5`).

Its only other data read is the thing array at `0x007cfb90`.

The world is made at park load: `FUN_00407d80` in state 9 allocates `0x1da748` bytes and stores them at
`0x00407dc8`. `FUN_00515660` (`0x00407de0`) then copies the world into `0x0080239c`, world + `0x2d8` into
`0x008023a0`, and the pair {world, `0x1da748`} into `0x007cdb30` (`FUN_004d0a70` at `0x0051568b`, which stores its
second and third arguments at `this` + first `* 8`; its fourth, the label "World Struture", is not stored). The
world is destroyed on leaving: `FUN_00409180`, called at `0x0054ff91`, runs the teardown, frees it (`0x004091b0`)
and zeroes `0x007cf83c` (`0x004091b8`) - the order is under "Leaving a park with something in the hand". So the
world a left park ran on does not outlive the lobby. The three copies each have that one writer and are never
zeroed. They dangle through the lobby, keeping nothing alive, and are rewritten at the next load before anything
can step.

**OpenTPW** builds the edge test once per park (`ParkCamcorderCameraMode.EdgeTest`), which the original does not
do. `Forget`, part of `Level.Unload`, lets it go. What else a left park lets go of is under "Leaving a park with
something in the hand".

### A fresh Full Simulation world

Lead: Aluzed's OpenTPW-decomp fork, review gap3-2 through gap3-11 (Q197). The 2.0 executable
(`testme.exe`, MD5 `c7c07bbded605cc45f922afa75fd71a7`) and the original's jungle `restart.INTS`
are the two checks; the restart is a reference, not a template to load when making a new park.

`FUN_00515540` constructs the staff pool, calendar and arrival block, 16,384 map records of `0x44`
bytes, 16,384 track records of `0x28`, and the other world tables. `world+0x1da710` starts at 1
(park closed). `world+0x1da738` is a separate state word. `FUN_00515660` calls `FUN_00515f30`, which
resets the free thing list to id 1. `FUN_005156a0` loads global then theme `Standard.sam`, adding
`Easy_Standard.sam` only in game type 2; it zeros `mGameTick`, resets the map (`FUN_004d7bb0`),
and creates manager models 9, 10, 11, 12, 13, 14, 15, 16, 17 and 19 in that order. Thus the advisor
is thing 3 and the challenge manager thing 10. Gates and lights follow, as things 11 and 12,
found by their catalogue stems. The new world contains twelve things and no people or placed rides.
The object chain is lights 12 → gates 11 → 0. Unplaced does not mean absent from the cell lists:
`FUN_0050afe0` inserts a thing through `FUN_004d91f0`; the restart's cell 1 has occupant 12.

The bank constructor `FUN_004cf7c0` takes cash and admission from `BankAccountInfo.InitialCash`
and `InitialAdmissionFee`. Its eight loans take amount, APR, period and lender from the merged
balance. Availability compares the amount **unsigned** against 10,000; bought and months repaid
start at zero. Its x87 sequence at `0x004cf85c` uses unsigned dword inputs, computes
`amount * pow(APR * 0.01 + 1, period * (1.0 / 12.0) * 0.5) / period`, and truncates toward zero
(`0x0067a830` sets the rounding-control bits before `FISTP`, then restores them). Constants
`0x00700378/380/388/390` are 0.01, -1, 1/12 and 0.5. All eight jungle restart repayments agree:
3651, 1825, 912, 365, 922, 1282, 2320 and 2749. Its fee is 20. Its cash is 9,999,999; this
reference is not the initial-cash oracle. The shipped regular balance starts at 50,000.

A fresh map is not all bare ground. `FUN_00450900` loads `terrain/base.map` through the MAP handler
at `0x004d8cf0`; `FUN_0052f050` visits the 128×128 map, calling `FUN_00536490` for the map record
and `FUN_0053af00` for the separate track record. The buildable-area test `FUN_00450420` reads
bit 0 of the terrain model's cell word. Attributes then select the fixed approach, solid cells
and the initial paths; `FUN_005365d0` selects their tiles. The jungle restart has 6,991 type-0,
9,077 type-7, 66 type-30, 10 type-1 and 240 type-2 cells. Its game tick is zero and its park is closed.

The terrain initializer continues through `FUN_005323f0`: it replays `Hoardings.sam`'s
`HoardingClicks` relative to entrance B plus `(3,0)` until X=512, in two-cell steps
(`FUN_00524960` → `FUN_0053aac0`). Each anchor is track type 25 and its other three 2×2
cells become type 12 with the anchor as parent (`FUN_0052ba50`). `FUN_005311c0` clears
map flag 0x40 inside; `FUN_005370e0`'s 0x81 arm includes all four cells of an inward
corner, and the final gate pass clears two rows between entrance A−2 and entrance B+3.
Its 0x85 arm marks straight hoarding records crossing map types 2, 7 or 30 with flag 1;
corner records skip that test. `FreshParkBoundary` expresses this initial, simple polygon
as an interior fill; it is not the general hoarding editor. `mStatusFlags` is the attribute byte, as already documented in FileFormats.
The four reference path tiles using art index 20 instead of 10 depend on the carried
RNG coin in `FUN_00535dd0`; fresh initialization retains the existing path builder's
base-art policy and counts `FRESH_PARK_PATH_ART_VARIANT`.

**Q197 verification, 2026-10-03.** A scratch copy of jungle `restart.INTS` (SHA-256
`84887ACABAF850CD06E77B11034845941C47227570159CAD7DC9A2A94FA05DB9`) supplied the
reference. All twelve identities and both complete exposed object records agree. Across all
16,384 cells every exposed field agrees except four tile indices (20 versus base 10). The
normalized cell JSON SHA-256 is `CE6D735C46A27C7FB16458974BEE6F1DED0CE55598161A2C0534091A8150D7B2`,
pinned by `LevelFullSimulationParkTests`. The copied restart is never a runtime input.
`q197confirm.py` created a temporary Full Simulation player through the real lobby, entered Lost
Kingdom, and captured `K0-empty-full-simulation.png`: empty grass and the fixed entrance, $50,000.
The prediction preceded entry: twelve things, two unplaced objects, no people, 16,384 cells,
closed 1, fee 20, and the eight APRs/repayments above; the startup log agreed. Live censuses read
zero guests and zero staff. Item emitters registered into slots 101 and 102. The run deleted only
its own player files; the existing `opentpw.cfg` changed loading-bar counts and was retained.
Restoring the old always-Instant-Action decision makes the new test fail (a `ParkWorld` and
42 things rather than a `FreshPark` and twelve). The restored full suite passes 1,636 tests,
zero skips. Review of the applied core used exact relayed excerpts because the verifier's shell
sandbox could not start; its missing-terminator finding was fixed and tested. Other consumer
changes were checked mechanically as type-only edits. Park saving, synthetic arrival-vehicle
render objects, and the cosmetic RNG sequence remain separate existing limitations.

### Entering and leaving first person: a click on the ground

**'C' puts the viewer nowhere.** `FUN_00481a10` only installs mode 9, whose `OnInstall` (`0x0046d590`) sets cursor
`0x13`. The viewer is placed by a **left click** in that mode: slot `+0x04`, `FUN_0046d0d0`, tests the hovered cell
(`DAT_007b05cc`), calls `FUN_0042ae70` at `0x0046d381`, and then installs the idle mode. A refused click sets
cursor 8. Its cell tests (`FUN_00535db0( 0x40 )`, `FUN_00535db0( 0x80 )` with `FUN_005363a0`, then
`FUN_004d0af0`, `FUN_0053ad90` and `FUN_0053ad60`) are not named, nor is its other branch: a thing on the cell
whose `+0x4ac` is nought gets `FUN_004e15b0( 0 )` instead.

`FUN_0042ae70` is the first-person toggle:
- **Entering** (`gui_CameraFlags & 0x16` clear): it takes the cell of the pick point (`0x007a1c70`, `0x007a1c78`),
  clamped to the map, and goes on only if that cell's type (record `+0x8`) is **0, 1, 3, 9 or 30** - nothing,
  path, queue, a ride's end, the approach (`0x0042aeec`..`0x0042af14`). Then `FUN_0042ab20( 2, 1, 0, 0, 0, 0 )`
  sets flag `0x02` and clears `0x04`..`0x20` and `0x80`; the point of interest is saved to `0x00790ad0` and the yaw
  `0x00790a38` to `0x00790a9c`; and the viewer is **stood at the pick point**, height 0
  (`0x0042af37`..`0x0042af80`). The yaw stays as the orbit had it.
- **Leaving** puts the saved point of interest and yaw back (`0x0042af8c`..`0x0042afb4`). **The walk is thrown
  away**: the orbit camera returns to where it was before first person.
- **The sprites swap.** Entering ends by calling `FUN_00542420` (`0x0042af85`), leaving by calling `FUN_00542640`
  (`0x0042afba`). `FUN_00542420` runs only while `DAT_00763f80` is 1, sets it to 0, and reloads every loaded bank
  whose `+0x205` (the `.ESP` byte `0x10C`) is set from its `.fpc` (the static at `0x00875fe8`, built from
  `0x00747970` at startup); `FUN_00542640` is its mirror, run only while the flag is 0, back to `.tpc`
  (`0x00876270`). Each frees the bank's old pictures and reloads them through `SpritePack_Load`. `DAT_00763f28`,
  which picks `.tpc`/`.fpc` over `.tps`/`.fps` (`0x008757a8`, `0x00875598`), is 1 in the image and nothing writes
  it, so the `.tps`/`.fps` arms are never taken. An `.FPC` picture is the same figure seen from ground level
  (FileFormats, "Sprites"). The ride view's `FUN_0042a560` also ends in the swap (`0x0042a6ee`): to `.FPC` when its
  second argument is 1 or flag `0x02` is set, otherwise back to `.TPC`. Its callers' second argument is not traced,
  and OpenTPW reaches no ride view.

The pick point is written only by `FUN_0045bf90` (called at `0x0054e2d5`, and only while `& 0x16` is clear). It is
a terrain hit inside its own cell's rectangle, with the cells clamped to the 96 by 85 heightfield, or a point on a
thing it hit; with no hit the old point stays. So the original's viewer always starts on a walkable cell inside the
park. A save never resumes in first person: loading zeroes `gui_CameraFlags` (`0x0042d04c`).

### Where OpenTPW's camcorder differs

`ParkCamcorderCameraMode.Slide` runs the sweep above pass for pass, steps 1 to 8, with the original's constants and
its stores: `double` between them for the x87 at 53 bits, a `float` at each store (`docs/QUEUE.md` Q48b). It differs
in these places, of which a walk in the park reaches only `FUN_0042a340`'s branch:
- **Off the map every side is shut.** `CellEdge` holds no cell there; the original's test reads its neighbours'
  records instead (step 1's cell is never clamped). Only an entry off the park gets there (below).
- **The passes are capped at 1024**, where the original loops until the step is spent. Every pass spends part of a
  step, zeroes one or ends the sweep, so only a step that is not a number reaches the cap.
- **The rest of X's step after an X pass is the stored float.** The original keeps it unrounded on the x87 stack
  (`FST` at `0x0042c12e`) for the nudge's direction (`0x0042c14d`) and the loop's test (`0x0042c24c`); the two
  differ only if it underflows.
- `FUN_0042a340`'s branch is not built (above): walking onto a ride's entrance is counted, `FIRST_PERSON_WALK_INTO_RIDE`.
- **At the margins**, read by a review of the port (`docs/QUEUE.md` Q48b). `Slide` is handed the stored float step,
  so a step that rounds to exactly -1e-4 is zeroed where the original may keep it (the dead band above). Every test
  of a step against nought reads C3, which an unordered compare also sets, and every "smaller" reads C0, likewise:
  a NaN step is kept by the dead band, has a reach of 2 and ends the loop, where C#'s compares read it as nothing
  and only the cap ends it. The put-back loads its cell with `FILD` over a zero high word (`0x0042c0b0` and the
  others), so a cell below 0 reads as unsigned and lands past 4e10; and `__ftol` keeps the low word of a
  position past 2e10, where C# saturates. None of it is reached on the map with a finite step.

Which precision is live is open (above). At 24 bits two of `ParkCamcorderWalkTests`' cases come out otherwise: a
step of 5 from 245 is taken whole and put back at 249.999 instead of crossing onto 250, and the tie from (45, 45)
leaves X at 54.9020 instead of 54.9010.

**Entry and the edge are Q25's.** `Enter` stands the viewer at the orbit's point of interest, wherever it is; the
original needs a click on a cell of type 0, 1, 3, 9 or 30 inside the heightfield. `Step` clamps to 1..1280, which
is ours: the original's bound is the soft one at 960 by 850. Entered at (1300, 245), the viewer is clamped to 1280,
cell 128, and held there. Entered at (1100, 245) - type 7, solid, which in Lost Kingdom is every one of the 8,224
cells beyond the 96 by 85 park - it cannot move at all. `Leave` hands the orbit camera the walked position and yaw,
where the original restores its own.

---

## The interaction modes

There is **one "current interaction mode" object** in the game, and camcorder is one of them.

| Address | What it is |
|---|---|
| `DAT_007b05e8` | The interaction-mode holder — **not a camcorder global** |
| `DAT_007b05d8` | The current mode object |
| `FUN_0046c350` | The setter: tears the old mode down (vtable `+0x2c`, then the destructor at `+0x00`), installs the new one and enters it (`+0x28`), reading a type id from `+0x24`. In an online game (`DAT_00fb3b7c` is 1, `0x0046c3b4`) a mode whose type is 1 is replaced by one built from `0x006fe980`. ~110 call sites — modes are switched all over the game; `Game_StateMachine` (`0x00550317`) is one of them |

A mode reports its type from **vtable `+0x24`**, which is a one-instruction `MOV EAX,imm32; RET` in every case but one:

| vtable | type | constructor | What it is |
|---|---|---|---|
| `0x006fea10` | 1 | `FUN_0046c6a0` | The default / idle mode |
| `0x006fe9b0` | — | `FUN_0046ced0` | The **abstract base** — its getter is pure virtual |
| `0x006fe9e0` | 3 | `FUN_0046c580` / `FUN_0046c5a0` | The carry shell: an item bought (tool 4) or moved (tool `0x3b`) |
| `0x006fea40` | 5 | `FUN_0046c6d0` | Place staff: a candidate off the hire screen |
| `0x006fea70` | 6 | `FUN_0046cbc0` | An employed worker picked up |
| `0x006fe980` | 7 | — | What `FUN_0046c350` substitutes for a type-1 mode, online only |
| `0x006feaa0` | 8 | `FUN_0046cfc0` | The coaster builder: its one constructor call is in the coaster bar's `FUN_00497bc0` (`0x00497c84`) |
| `0x006fead0` | 9 | `FUN_0046cff0` | **CAMCORDER** |

**Camcorder is mode 9**, which explains the staff/visitor locator's `if (iVar1 == 9)` on the current mode's type: it puts the idle mode (`FUN_0046c6a0`, through `FUN_0046c350`) over the camcorder's pick mode before it points at anyone, and does not leave first person, which runs under the idle mode ("Entering and leaving first person"). The coaster builder bar's `FUN_00497bc0` tests type 8 the other way: when the mode is not 8 it installs its own (`FUN_0046cfc0`).

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

**The thing under a sprite** (Q194): `FUN_00532bd0` stores the pointer at `0x008bd4f0`/`0x008bd4f4` as x / 1024 − 1 and
y / 767.5 − 1. When the sprite draw has latched a sprite under it (`DAT_008bd4fc`: cleared at each pass by
`FUN_0058a770`, set at `0x0058931a` by `FUN_005890e0`'s squared-distance test), it takes that sprite's script
(`FUN_00475ef0`) and walks the hovered cell, then its eight neighbours (pairs at `0x0081ed40`, filled by the initializer
`0x00532b30`), returning the first thing of kind 1, 4, 5, 6, 7, 8 or `0x12` on the cell's thing list whose `+0x0c`
script is the sprite's. Its one caller, `FUN_00540b30` (from `FUN_00475430`), stores the answer at `0x00877350` and hands
the previous one to `FUN_00423b50` on `0x007b05a8`.

**The type-3 handlers do NOT test the cell for zero** before unpacking it; the out-of-range value is
caught downstream by the bounds test inside the callees instead.

### The verb space of mode type 3

Type 3 is a 20-byte shell - `{vptr, 1, verb, carrying, u16 itemId}` - that publishes its verb to the one
global current-tool `DAT_0081ae2c` and forwards three mouse events into the map-tool layer. **All the
real work is a switch on that global**, not in the mode.

    0    idle/clear        1    build path        3    build queue
    4    PLACE a purchased item                   0xe/0x13  track kinds
    0x14 recompute the selected object's queue    0x15/0x16 coaster bar: cb_track / cb_move
    0x1a link track        0x33 DEMOLISH/SELL     0x36 staff patrol rectangle
    0x39 Buy Land          0x3a Clear Land        0x3b MOVE an existing object
    0x32 clear one cell, armed for one apply by Backspace          0x34 delete a line (Backspace's undo)
    0xb, 0x10, 0x19, 0x35, 0x38  also live

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
| `0x84` | `cell[+0x40] = DAT_008186bc++` (`0x005340fa`..`0x005340ff`). The `+0x20` word is instead the **re-stamp counter**, which the stamp bumps when a cell is stamped with the type it already is |
| `0x85`, `0x86` | track bookkeeping and the thing notification |
| `0x87` | the teardown arm, into `FUN_005367a0` / `FUN_00527ee0` |
| `0x32` | `ClearCell` plus a direction-driven relink of what is left |

`FUN_005348d0` writes `mNeighbours` through paired `FUN_00522700` calls — the cell being laid gains
the step's bit and the neighbour gains the opposite, which is the symmetric link
`ParkPathNeighbours.Cardinal` already performs.

**It is NOT the only writer.** `FUN_00522700` is called from `FUN_00524960`, `FUN_00528a70`, `FUN_0052a050`,
`FUN_0052fc80`, `FUN_0053b280`, `FUN_0053bc60` and `FUN_00539220` as well
as from inside the rule. The placer's two are the ones that matter — see "What authors an entrance's
`mNeighbours`" below.

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
| 3 (queue) | forms no link at all; it retiles it and plays effect `0x8b` — only when the queue cell already carries the bit pointing back, the retile flag is set, and (for the sound) `DAT_00785a2c` is nought (`0x00534a76`..`0x00534ad2`) |

The four blocks make the bit explicit: stepping north (`DAT_007cdba0` = (0,−1)) tests `& 0x01`,
south (`…ba8`) `& 0x10`, east (`…bc8`) `& 0x04`, west (`…bd0`) `& 0x40`. **So the tested bit is the
direction FROM the cell being laid TO the neighbour**, and `ParkPathNeighbours.Cardinal` already
matches it exactly — its sense is right.

**The consequence is worth stating, because under this rule alone a thing built in play could never be queued.**
A path laid on the `-y` side of an entrance steps south, so it tests `& 0x10`; the shipped Belly
Bounce's entrance at (52,23) carries `direction 0x01` with its queue on that same `-y` side. Under
this rule that link could never have been earned — **so the shipped entrance's `mNeighbours` bit is
authored, not computed**, which is what `park.md`'s "replaying creation order" warning (under "`mNeighbours`, `mDirection` and the compass") is about.
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
`ECX = EDI` against `ECX = ESI` separates them.

**The shipped park confirms both cells at once.** The Belly Bounce is anchored (51,23) at angle 0, so
the base bit is 1 and the rotate is the identity. Its entrance at (52,23) carries `direction 0x01`,
which is `Opposite(H)`, so `H = 0x10`; the entrance table sends `0x10` to (x, y−1) = **(52,22)**, which
is its queue cell, and that cell is given `mNeighbours |= 0x10`. The save reads (52,22) as `0x50`,
which carries that bit. Both halves were predicted from the disassembly before the park was consulted.

So the bit a queue needs is authored at placement time, on both cells together, and never earned by the
neighbour rule — which is exactly why no replay of `FUN_005348d0` and no predicate over the finished
map can reproduce it.

**The faced cell does not merely gain a bit: it is stamped a QUEUE cell there and then**, and that cell
is what a player sees as the node to lay a queue from - see "What the placer builds in front of a thing"
below. The exit's faced cell is a different arm altogether, a path stamp with no direction write, so the
shipped (52,27)'s `direction 0x00` is what the exit arm writes and says nothing against the entrance pair.

The pair is reached only when the build flag (`param_5`) is set, the test flag (`param_6`) is clear, and
`FUN_0052fab0()` is non-zero — that gate is `DAT_008187f8 != 0 ? 0 : DAT_0081b0cc`, i.e. not an add-on
and the item has a queue.

| Address | What it is |
|---|---|
| `FUN_00522700( cell, bit )` | `cell.mNeighbours \|= bit` (`+0x0c`) |
| `FUN_005227e0( cell, b )` | `cell.mDirection = b` (`+0x0d`) |
| `FUN_00522850( cell )` | reads `cell.mDirection` |
| `FUN_004d8c00( b )` | `Opposite` — 0 → 0, `< 0x10` → `<< 4`, else `>> 4` |

**`FUN_00522700` has a second arm that does not touch `mNeighbours` at all.** When `DAT_0081b4cc` is
non-zero *and* the cell's `+2` byte is `2`, the bit is OR'd into a shadow mask at `+0x22` instead. That
flag is raised only inside editing functions, each setting it and clearing it around its own work -
`FUN_005323f0` around a prefab build (`0x00532408`/`0x00532622`), `FUN_005327e0` (`0x00532814`/`0x005328cb`) and
`FUN_0053af00` (`0x0053af51`/`0x0053af60`), with one more clear at `0x0052f06d` (whether every exit of the last two
clears it is not traced) - so it is 0 for anything a player places — but a reimplementation that mirrors this setter must
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
three quarters can tell a wrong one apart. OpenTPW's `ParkBuilding.RotateBit` turns this way, and its test
pins the quarter turn: turned the other way, a thing built at a quarter turn has its way in pointing 180
degrees from its own entry cell, back across its own footprint, where nothing can ever be joined to it.

### What the placer builds in front of a thing

Decoded 2026-09-22 and checked cell by cell against the shipped park. **After its sweep, and only when
building, `FUN_00528a70` lays one cell in front of each end of a thing**, and for a thing with a queue
that cell is the player's starting point for the rest of it.

**A thing with a queue** (`FUN_0052fab0() != 0`: not an add-on, and the descriptor's `+0x40` —
`Info.HasQueue` by key order — set when the build tool was installed, `FUN_0046d5a0`):

- the entrance takes `mNeighbours |= Opposite(H)` and `mDirection = Opposite(H)` (`0x005297f0`,
  `0x005297f8`), where `H` is the entrance character's bit turned by the angle (`DAT_00818c30`);
- **the cell it faces is stamped a queue cell** (`0x00529808`..`0x00529890`, every call `ECX = ESI`): op
  `0x87`, **op 3** through the stamp `FUN_005346d0`, `mNeighbours |= H`, `mDirection = H`, the
  entrance's owner copied into `+0x10` (`FUN_00539210`), **`mFlags = 0x20` assigned** (`FUN_0053a510` is
  a `MOV` into `+0x0e`), then ops `0x81` (retile — one link, so `quedead`), `0x85`, `0x86`, and the
  queue is rewalked (`FUN_004de1f0`);
- **the cell the exit faces is stamped a PATH** (`0x005299cb`..`0x00529b18`): op `0x87` unless it is
  already path, op 1, the neighbour rule, ops `0x81`/`0x85`/`0x86`, `mFlags = 0x20` — and **no direction
  write**. That is (52,27)'s `direction 0x00`.

**A path already on that cell is destroyed**, with no refund: the queue stamp runs with the last-cell
flag clear, so the gate allows queue over path, and the stamp force-clears it (`DAT_0081d7a8`, which
bypasses NOMODIFY) whatever op `0x87` did. Its neighbours lose their bits toward it — the entrance
among them, until the commit's `FUN_0052a050` puts that bit back.

**The placer's test pass refuses the whole placement** (every marker red) in three cases. An end
facing off the map makes it return null (`0x00529744`..`0x00529757` for the entrance,
`0x005299e2`..`0x005299f5` for the exit). A queued entrance's faced cell is tested as op 4
(`FUN_00532fc0( 0x204, 3, … )`), whose verdict passes only bare ground or a path without NOMODIFY — so
another queue, another thing's placer cell, water or rock all refuse. Every other end — a queued exit,
or the entrance of a thing with no queue — is tested as op 1 (`0x201`), which accepts a NOMODIFY path and
refuses another thing's cells, red unless that thing's `+0x54` (`Info.OverwritePriority`, 5 on every
shipped category) is below the placed item's.

**A thing without one** gets a single path cell in front of its entrance (`0x005298d5`..`0x005299aa`),
the same path stamp, and then relinks the paths round it; its entrance keeps `H` as its direction, where
a queued thing's takes `Opposite(H)`. No exit cell.

**All of it is free.** The commit raises `DAT_008186d4` before the placer runs (`0x00524b4a`) and the
stamp debits nothing while it is set. The affordability test still runs as `cash - 0 >= 0`, so with the
balance below nought the queue stamp is refused — and the placer ignores the refusal and writes the
rest anyway.

**The shipped park carries every one.** Its nineteen NOMODIFY cells are the ten-cell avenue the loader
rebuilds from the design map (x 47..48, y 17..21) and these nine: the Belly Bounce's queue cell (52,22)
— type 3, `neighbours 0x50 direction 0x10 flags 0x0020 owner 2996`, the only one of the park's four
queue cells so flagged — its exit's path (52,27), and a path before each of the seven other entrances,
(56,15) (57,15) (56,16) (56,17) (44,28) (43,29) (52,29).

### The commit hands the player the queue tool

**Placing a queued thing drops the player straight into mode 3, anchored on that queue cell.** In
`FUN_00524960`'s mode-4 arm, after the placer returns `&DAT_00818c20` (the entrance's x, y and, at
`+0x10`, `H`):

    0x00525264  FUN_0052a050( &DAT_00818c20, &DAT_0081ede4, &DAT_0081ede8 )
                  entrance |= Opposite(H); anchor = the entrance stepped by H's table
                  (0x01 -> y+1, 0x04 -> x-1, 0x10 -> y-1, 0x40 -> x+1); faced.mDirection = H
    0x0052526e  MOV DX,[EAX]              the returned cell is read at once, with no null test
    0x00525296  FUN_0052fbd0()            the pending list emptied, then the anchor pushed
    0x0052529e  FUN_0052f580( 3, 0 )      mode 3 - the setter that KEEPS the anchor

`FUN_0052f580(3)` withdraws ten advisor ids, posts advisor message **`0xcb`** and sets cursor **4**,
`c_queue.ani` (`FUN_00489720` registers it). Ops `0x85`, `0x86`, `0x83` and `0x81` then run on the
anchor as a one-cell line, the queue is rewalked, and `DAT_00763ac0`/`ac4` get the `0x80` sentinel so
the first click links nothing back. If the anchor reads as a path at that point, `FUN_0052f200(0,0)`
ends the tool — unreachable in practice, because the placer has already stamped it a queue.

A thing without a queue goes idle through `FUN_0052f580(0,0)` instead — **unless Ctrl alone is held**
(the modifier word `0x0c`, from `GetAsyncKeyState` in `FUN_00486aa0`), which keeps mode 4 for another
of the same. Karts and the water ride (`WhichTrackType` 1 and 2) lay their first track cells and then
fall into the same seeding; coasters (3) skip the track step. An add-on (`DAT_008187f8`) seeds nothing.
**Moving** a thing (verb `0x3b`) runs the same sequence at `0x005258a7`/`0x005258e1`.

### A queue run, and how the tool puts itself away

**The ops are applied op-major**: `FUN_00524960` calls `FUN_00536100` once per op over the whole line —
`0x87`, the stamp, `0x80`, `0x82`, `0x85`, `0x86`, `0x83`, `0x81` (`0x005273fe`..`0x00527517`) — and uses
only the last pass's answer. `DAT_00820aa4` marks the first cell of each pass, `DAT_00820ab8` the last.

**Op `0x80` is `FUN_005348d0` with the laid type**, and for type 3 it takes its queue arm
(`0x0053522d`..`0x00535597`), which makes **two links at most and none by neighbour type**:

- **back to the previous cell of the line** — both bits — only when that cell's type equals the laid
  type, fewer than two of its eight bits are set, and its owner is `DAT_00818698` or nought. On a pass's
  first cell the step is the previous call's, saved in `DAT_00763ac0`/`ac4`. When the cell being linked
  is a path, its two diagonals flanking the link are cleared (N `0x02|0x80`, E `0x02|0x08`, S
  `0x08|0x20`, W `0x80|0x20`), gated on the type test alone;
- **to the ride's entrance**, on the first cell only: the neighbour must BE the object's `mEntryPos` cell
  and its direction byte must EQUAL the bit pointing back (N `0x10`, E `0x40`, S `0x01`, W `0x04`).

No link to a path, an exit or another queue beside the run is ever made, so a queue laid alongside a path
stays apart from it. The path arm (laid type 1) is the only one that demotes a queue cell to path, and
it writes the type alone — not the direction.

**The run's last cell may be a path, and that is how a queue is finished.** The stamp refuses queue over
path on the last cell (`FUN_00535600` with `DAT_00820ab8` set) and raises `DAT_00820abc`, so the cell stays
a path; `DAT_00816d64` still holds 3, so op `0x80`'s queue arm joins it to the last queue cell; op `0x82`
gives it a flow byte if it had none; **op `0x83` gives it the ride as owner**; and op `0x81` answers
nought, which ends the tool through `FUN_0052f200(0,0)` — sound `0x8b` if the preview called the end a
link (`DAT_00816d60`), advisor message `0x151` if not. (48,22), where the Belly Bounce's queue meets the
path, is the only one of the park's 78 path cells with an owner, 2996. **Replaying placement and one
click from (52,22) to (48,22) reproduces all five cells of the shipped queue field for field.**

The tool also ends when the click lands **on the anchor itself** (the pending cell is laid first), when
**the preview had flagged a red cell** (`DAT_00816d48`: nothing is laid, sound `0xaf`,
`0x00524a63`..`0x00524acd`), and on **a right click under 200 ms and 8 interface units** — but only with the Options
switch "RMB cancel" on (`DAT_0078d911`, control `0x1d4c5`, UITEXT 331; `0x0048842b`..`0x00488434`) - which is on by
default, and Alexah confirms from playing that a right click puts the tool away. The build tool's own
right-button slots are bare `RET 8`. Escape (`0x0040c180`, which calls `FUN_0052f200( 0, 1 )` at `0x0040c368`) and
Delete (`FUN_0040c5e0`, Clear Land) put it away too - see "The hand's ways out". Otherwise the anchor
moves to the snapped target, so an L is laid a click at a time.

### Editing a queue: mode `0x14`, and the ride window's queue button

**The ride window's queue button is `FUN_004af200( 0 )`** — `FUN_004af600`, the ride window's handler,
sends control `0x3e34` there, and `0x3e2a` to `FUN_004af200( 1 )`, the track twin. It selects the
window's thing, installs the build shell with verb `0x14` (`FUN_0046c580( 0x14 )`) and runs the apply
dispatcher at once (`FUN_00524960( 0, 0, 0, 0 )`). **Clicking an existing queue cell installs the same
`0x14`.**

The dispatcher's `0x14` arm (`0x005260f5`..`0x00526120`) calls `FUN_00530120( object )`, which empties the pending
list, pushes the cell the entrance's one link faces, walks from the entrance along the queue pushing each corner
(two perpendicular cardinal links, `FUN_0053ae00`), **unlinks the queue's last cell from the path it reaches** —
both bits cleared, both cells retiled — and anchors on that last cell. The walk in full, with its other ends, is
`ride-operation.md`, "The sale's drain". The arm then rewalks and enters mode 3 with `FUN_0052f580(3,0)`. So editing
a queue always carries on from its end, and finishing the run joins it up again.

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
result, which is the trap `CellEdge.cs` warns of for `FUN_004d8750` and this page for `FUN_00522700` ("What authors an
entrance's `mNeighbours`").

**The deltas come from the item's shape picture, and `FUN_00413410` is what reads them.** It walks the
grid at descriptor `+0x18` **column by column**, each column from row 0, and stores the first cell of
kind **9** into `+0x494`/`+0x498` as the entrance, with that cell's compass bit at `+0x49c`; then the
first kind **10** into `+0x4a0`/`+0x4a4` as the exit, bit at `+0x4a8`. Two fallbacks matter and both are
reproduced rather than tidied: **no 10 leaves the exit on the entrance**, bit and all, which is why
`mExitPos == mEntryPos` on ten of Lost Kingdom's eleven placed objects; and **no 9 leaves both at
nought**, the anchor cell itself, with bits `1` and `0x10`. `+0x4b0`/`+0x4b4` is copied straight from
`+0x30`/`+0x34`.

**The characters are the executable's own alphabet, and `S` is NOT the entrance.** The shape reader
`FUN_00402720` looks each character up in a nineteen-row `{ character, kind, bit }` table at
**`0x007396c8`** (twelve bytes a row, passed at `0x00401a46`):

    .  0        *  4        Q  3        @  1        +  0x0b      #  0x10
    8  9/0x01   6  9/0x04   2  9/0x10   4  9/0x40   O  9/0x01
    N 10/0x01   E 10/0x04   S 10/0x10   W 10/0x40   X 10/0x01
    D  0x17     >  0x17/0x04            <  0x17/0x40

— a numeric keypad: `8 6 2 4` are the entrances and `N E S W` the exits. **And the reader turns the
rows upside down** once it reaches the closing fence (`0x00402938`..`0x004029ab`, row `i` swapped with
row `rows-1-i`), so row 0 of the grid is the last row drawn. It skips a space without making a cell,
counts a blank line as a row, and refuses the whole picture on a character outside the table
("Illegal map character").

**The obvious reading — `S` the entrance, rows as drawn — agrees with this one on the Belly Bounce**,
whose `S` and `2` are mirror images of each other. They part on the Staff Room (`**` / `*2` at 90
degrees from (58,16)) and the Jungle Spray (`***` / `***` / `*2*` at (51,30)): the executable's reading
predicts `mEntryPos` and `mExitPos` for **all fourteen** object records in the shipped save - the eleven
placed things and the fixed bus, gates and lights at (0,0) - and the other reading gets twelve. Measured over all **274** shipped shape blocks: every
entrance is a `2` (137, one per item at most), the 72 exits are 44 `S`, 26 `N` and 2 `E` (one per item
at most, always beside an entrance), and no picture uses a space, a blank line or an unknown character.
Every one of the 72 items with `Info.HasQueue` has both.

**Rotation is never changed by a user input on any traced path.** It is zeroed when the tool ends
(`FUN_0052f200( 0, … )`, `0x0052f3b6`), snapped for an add-on hovered over a track type by the tool-4 preview
(`FUN_0052f1b0` at `0x005237f7`), and set to the thing's own angle when a move picks it up
(`FUN_0052f1b0( [thing + 0x10], 1 )`, "Moving a thing"). What a successful put-down leaves in it is open ("The
hand's ways out"; `docs/QUEUE.md` Q58). Whether the original has a manual rotate is an open question.

### Placement feedback: the `m_*` textures are the game's own vocabulary

The per-cell verdict `FUN_00535670` returns a **marker texture index** into a 20-entry table at
`0x00763b38`: 0 blue (allowed), 1 red (refused), 2 orange, 3 `m_enter`, 4 `m_exit`, 5 `m_direct`,
6 `m_front`, 7 `m_inout`, 8 `m_link`, 9 `m_break`, 10 `m_cross`, 11 `m_end`, 12 `m_nocash`,
13 `m_erase`. **`m_cross` and `m_nocash` have no producer** - no path passes either index. And
`m_nopath.tga` ships in `data/generic/dynamic/textures` but its name appears **nowhere** in the
executable.

**The markers are flat squares in a procedural mesh, not models.** `FUN_0053bfe0` builds three
"dynamic faces" meshes through `FUN_0053d790` — `BlueprintMesh` (these markers), `WaterMesh` and
`FlagsMesh` — each with the twenty textures named at `0x00763b38` (`blue, red, orange, m_enter, m_exit,
m_direct, m_front, m_inout, m_link, m_break, m_cross, m_end, m_nocash, m_erase, red, gby_sur1-3,
gte_lgo1` twice), searched in `Data\Generic\Dynamic\Textures` (or `STexture` when `FUN_0054dcc0()`) and
then the level's own `Dynamic` folder, where each theme's `dynamic.wad` supplies only `orange.wct`.
`FUN_0053c8d0` appends one square to the face list at `0x00820b00` (`0xa0` bytes a record, count
`DAT_00871ee0`, cap `0x806`, shared with the polygon faces of `FUN_0053c9c0`).

A square covers one cell, 10 by 10 — face 0 of the face builder `FUN_0053df30`, which also picks the UV
base (5 for textures 8..14) and turns the corners by the square's orientation byte. **Each corner sits
at the terrain height there + a lift + 1.5 + `sin( phase + x + z )`**, x and z being the corner's own
world coordinates. `FUN_0053ddd0` reads it from a 4,096-entry table `FUN_004708d0` fills with
`sin( 2π·i/4096 )` (index scale `4096 / 2π` at `0x007b095c`, mask 4095 at `0x007b0960`), so the amplitude
is one world unit, a tenth of a cell. The phase `DAT_00874fc0` gains 0.1 each rendered frame unless the
clock is paused (`FUN_0053c3f0`, `0x0053c755`..`0x0053c773`, gated on `FUN_004030c0`, IsPaused). The same
sine scales the corner's up vector by `(sin + 1) / 2` (`0x00700e28` is −1.0, `0x00700de4` 0.5), so the
squares also brighten and dim as they wave. Alexah, who played the original: *"they were translucent.
They waved like a flag/water."*

**Over a built cell the square is lifted** (`FUN_00532fc0`, `0x005330ca` and its siblings). The record's height is
`ceil10(trunc(FUN_00452ae0(x, y, null)))` + 1.5, where ceil10 is `(t + 9) / 10 * 10.0` in integer division. It is
lifted only over a cell of mType 4, 9, 10, 7 or `0x1e`, or of track kind 11, 12, 16, 17 or 25; elsewhere the lift is
nought. `FUN_00452ae0` finds the thing on the cell in two steps. First it reads the cell's track-layer record
(`FUN_0053bf30`): its object at `+4`, or for kind 12 or 17 the object of the cell its `+0x10` names. Failing that, it
reads the cell's thing id (`FUN_00527e80`), which must be an object (type byte 3). From that object it reads the
`.hmp` at `+0xcc` and the root node's matrix (model `+0x78`; translation `+0x40`, `+0x44`, `+0x48`). It carries the
cell's corner back into the object's unturned space by its rotation at model `+0x10`, a quarter turn at a time
(`0x00452bb0`..`0x00452c1d`). It answers the `.hmp`'s cell-grid byte × 1/2.55 plus the root's world height. With no
object, it answers the ground height at the cell's corner (`FUN_004527f0`). **The wave branch of `FUN_0053ddd0` then
adds the ground height at each corner again**, so on raised ground the ground counts twice: once in the object's base
or the fallback, and once here.

Measured in the original under Proton on 2026-10-01 (the face list at `0x00820b00`, BlueprintMesh `DAT_00871f64`'s
positions at `+0x64`):
- The Belly Bounce's (53,23) and (53,24) carry a record height of 21.5 (lift 20), and their corners are drawn at
  20.5 to 22.4 over ground 0.
- (93,21) and (94,21), track kinds 25 and 12, lift 20 too. Their object is a 2×2 thing of the track layer: object 42,
  root at ground 0, turned 180, with its own `.hmp`. The layer's kind-25 cells anchor such things (the roots of the two over (64,40) and
  (76,40) stand at 5.0), and OpenTPW builds none of them.
- A lifted square also gets walls down to a lower neighbour, records at 11.5 and 1.5 on the same cell.

The double count on raised ground was not caught on one cell. The original's strip ends at the raised out-of-park
cells before it draws a square there. **Red blinks**: `FUN_0053c8d0` drops texture 1 while `DAT_00763c98` is nought, which
toggles on a counter of rendered, unpaused frames: seven on, two off (`ride-operation.md`, "The red square under a
stranded person"). Textures 8 to 14 take UVs turned by
the camera's yaw (`DAT_00790a38`), so `m_link` and `m_end` stay upright on screen. The squares are
see-through, by Alexah's own memory of the original; the blend state itself was not traced.

**In mode 3 the queue tool draws a strip every UI tick** (`0x1e` → `FUN_0046c2a0` → `FUN_0046c660` →
`FUN_005234d0`): the list is cleared, the hovered cell is snapped to the anchor's longer axis, and
`FUN_00536100( 0x103, anchor, target )` appends one square a cell with the verdict `FUN_00535670( 3 )`,
which answers only **0 blue, 1 red, 8 `m_link`** (the last cell is a path — cursor `c_link`) or
**11 `m_end`** (the last cell is this ride's own queue end — cursor `c_end`). A foreign queue cell is
red; a ride, entrance or exit cell is blue only when its item's `+0x54` is below 3, and every jungle
ride's is 5; after a refusal every later cell is red, and any red switches the cursor to
`c_noplace.cur` (`FUN_0052f950`). **The anchor is always the first square**, so the cell before a new
ride's entrance is marked the moment it is placed.

**While a thing is carried** (mode 4) the placer runs twice a tick, a test pass that turns everything red
on any failure, and a draw pass: footprint row 0 shows `m_front`, other footprint cells the verdict, the
cell before the entrance `m_enter` (turned to face it; `m_inout` for a thing with no queue), the cell
before the exit `m_exit`, and `m_direct` goes on the cell a kind-`0x17` cell points to. These squares
glide, following a smoothed cursor.

**`data/generic/dynamic/garrow.MD2` and `rarrow.MD2` are not markers and are never drawn.** They are a
model version (`0x18`/`0x17`) the one `.md2` reader `FUN_0046d6d0` refuses — it frees and returns null
below `0xdd` unless flag bit 0 is set, and neither of its two call sites (`0x0046282b`, `0x0046dd06`)
sets it — and no `qickload.txt` read and no string in the image names them.

### The object window's stats panel

`FUN_004ade40` fills the placed object's own window. It is worth reading as a whole because the seven
rows are **three different kinds of control**, and treating them alike is why a reimplementation can
render the labels perfectly and leave every value blank.

| Control | Row | Kind | Filled from |
|---|---|---|---|
| `0x3e21` | Users last month | value | The customers ring's last min( filled, **30** ) finished days (`MOV EDI,0x1e`, `0x004ade83`; `FUN_00495d40` gives the filled count when fewer), today's excluded; printed `"%d"` by a number painter in font 6 (`FUN_0048fde0`, `0x0048ff8f`). Its `"CHistory - You asked for the sum of more elements than there is defined history for…"` (`0x004ade8a`, when `mNumEntries` is under 30) goes to the bare `RET` logger and stops nothing ("Every diagnostic string goes to a bare `RET`") |
| `0x3e1b` | Age | **text** | `FUN_004dd670`, formatted through `FUN_006acd60` with id **`0x1b1`** (433) and a `VARM` placeholder - not a UITEXT row, whose 433 is empty; has an explicit negative-sign limb |
| `0x3e16` | Excitement | **gauge** | `FUN_004e0560( object, speed, duration, capacity )` |
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
window caches speed, capacity and duration at `+0x2c`, `+0x30`, `+0x34` and passes all three to both
functions. `FUN_004e0560` divides the speed by the item's per-upgrade `+0x1a8` and the duration by
`+0x1a0`, clamps each ratio to **0.75..1.25** (the doubles at `0x007005b8` and `0x007005c0` — as
`f32` they read `0.0`, which is a trap) and multiplies them; a sideshow (`+0x4ac == 2`) returns
`20 + trunc( 0.08 × mChanceOfWinning × √clamp( mCostOfGoods − mPricePerUse, 0, 100 ) )` instead, the object's own
three (`ride-operation.md`, "What a thing is worth to a guest"). `FUN_004df450` compares against `+0x19c` and `+0x1ac`, the **red line** figures, and
drops the result by a further factor past them — so the red line on a slider marks where reliability
starts falling away.

The closing multiply the decompiler hides in a bare `__ftol` **is** readable in the instructions:

    004e0729: LEA EAX,[EBX + EBX*0x2]    ; EBX*3
    004e072c: LEA ECX,[EAX + EAX*0x4]    ; *5   -> EBX*15
    004e0734: SHL ECX,0x2                ; *4   -> EBX*60
    004e0737: IMUL 0x51eb851f / SAR 5    ; /100
    004e0743: ADD EDX,EDI                ; + the track term, clamped 0..0x28
    ...       clamp 0..100
    004e0838: FILD dword ptr [ESP + 0x30]  ; that base, as an integer
    004e083c: FXCH                         ; the clamped duration ratio on top
    004e083e: FMUL float ptr [ESP + 0x2c]  ; x the clamped speed ratio
    004e0842: FSTP float ptr [ESP + 0x28]  ; the product, stored as a float
    004e0846: FMUL float ptr [ESP + 0x28]  ; the base x that product, then __ftol

So excitement is `item[+0x13c]` scaled by the two ratios. When the object's `mTrackRideHandle` (`+0x28`) is
non-zero the base is `(item[+0x13c] * 60 / 100 + track term)` clamped 0..100 instead: the test at `0x004e06ce`
jumps, for a handle of nought, past the scale, the track term and the clamp. The track term is `3 × crossings + the
longest straight + 2 × bends` from `FUN_00545310`, clamped 0..40 (`ride-operation.md`, "A coaster's, a track ride's
and an upgraded ride's excitement"), and a coaster (`+0x9c` == 3) is `trunc( 50 + f / 2 )` of its node's rating,
whatever the sliders say.

**The inputs are named.** The compiled `.sam` schema (`0x744b30`, sixteen dwords to an `Upgrades` tier, the `0x40`
stride) puts `Upgrades[0].InitSpeed` at `+0x1a8` and `InitDuration` at `+0x1a0` (tier *l* `0x40 × l` beyond), with
`InitCapacity` `+0x198`, `RedLineCapacity` `+0x19c` and `RedLineSpeed` `+0x1ac`; the constructor copies tier nought's
speed, when above nought, into the object's `mOperatingSpeed` (`+0x58`, `0x004db54c`) and the low byte of its
duration, when above nought and clamped to Min/MaxDuration (`+0x12c`/`+0x130`) whatever those are, into
`mOperatingDuration` (`+0x5c`, `0x004db64f`), so a ride at its starting settings has both ratios 1 wherever its
duration lies inside those bounds, as every jungle ride's does (Q165). Between them, the low byte of `InitCapacity`,
when above nought, goes through the setter `FUN_004dd7f0` (`0x004db560`), which clamps it to Min/MaxCapacity
(`+0x124`/`+0x128`) only when the two sum above nought and stores `+0x5d`; the shops declare both nought. All three
start nought (the constructor's head), and each branch also pushes its value into the script. Earlier in the same
constructor `mPricePerUse` (`+0x194`) is copied unclamped from `UsageInfo.InitPricePerUse` (`+0xe4`, `0x004db3ad`).
OpenTPW builds all four fields (`ParkBuilding.Constructed`, Q171) and pushes the capacity and duration
(`ParkRides.BindNew`); the speed's push is not built, as `RideScript` keeps no speed word (Q155). `+0x13c`, which supplies the base above,
is `UsageInfo.ExcitementLevel`: the compiled `.sam` schema puts it one slot before `InitCostOfGoods` (`+0x140`), in a
UsageInfo group from `+0xc4` (`GoldenTicketCost`) through `+0x170` (`NumSimultAnims`) (`ride-operation.md`, "At the door"). `RIDE_EXCITEMENT_BAR` and
`RIDE_RELIABILITY_BAR` stay counted until they are built.

### Sell, move and the scrap value

**MOVE (verb 0x3b) is demolish-then-buy-again**, literally: the object is destroyed and refunded at
pickup, the footprint fully released, and the full price re-charged at put-down by the same constructor
a fresh purchase uses. Nothing in the construction path distinguishes a move from a purchase, and the
one thing the old object passes on is its facing - see "Moving a thing".

**DEMOLISH (verb 0x33) refunds `price * percent / 100`**, where percent is `FUN_004e2290` - a per-item,
per-build-state, **per-age-bucket** field selected from a four-year scrap table at
`Upgrades[level]+0x08..+0x14`, returning a literal **100** only while the object is under 30 days old, has had no
customer (`mNumCustomers` `+0x1a0` nought, `0x004e2390`) and its item's descriptor `+0x188` (level 0's first-year figure,
whatever the object's level) is above nought (`0x004e23b2`); otherwise the first year's figure. So a new object's first customer lowers it. It is *not* a flat
half. The same expression is packaged as `FUN_004e2400` and shown on the object's own window before the
player sells, which is UITEXT 23 **"Scrap value"**.

**Delete does NOT go through the interaction-mode system** - `FUN_0048cd10` calls `FUN_0052f200(0x33,1)`
and then the map-click apply directly. Of the window's sell and move, only MOVE builds a mode (the ride window's
queue button builds one too; see "Editing a queue").

**Nothing refuses to sell a ride with guests queueing or riding, and it lets them all go at once** -
see "Selling and the people on it" below.

There IS a confirmation box, UITEXT 396, **gated on an options checkbox** (`DAT_0078d90f`) - the same
flag gates the staff dismissal box, UITEXT 397.

**Every caller of `FUN_004de1f0` is in the cell-editing family**, as this project suspected: five
functions, eight call sites: the map-click apply `FUN_00524960` (four), the footprint applier `FUN_00528a70`,
the stamp `FUN_005346d0`, `ClearCell` `FUN_005367a0` and the backtrack `FUN_0052fe50`.

#### Demolishing a queued thing

Decoded 2026-09-22, every claim re-derived by a refuter. **Selling a thing with a queue clears the whole queue, the
placer's NOMODIFY node included**, before its footprint goes (`FUN_00527ee0`, when the item's `+0x40` is set and the
object's cached queue length `+0x40` is above nought): `FUN_00530120` lets the queue's end go of its path, the force
flag `DAT_0081d7a8` is raised, and `FUN_0052fe50` drives op `0x32` — `ClearCell` under force, which bypasses
NOMODIFY, unlinks no neighbour, and zeroes the cell's mask, direction, owner and flags. **Each cell refunds
`Costs.QueueCell * pct / 100`, and then one cell's worth is debited** (`FUN_004d01f0`), so a queue of N cells
returns N−1 — the node the placer laid for nothing. The Belly Bounce's four return 225, while the bank's `+0x114`,
which gates the debit (`0x004d01f3`), is non-zero. A cell refunds only while its owner cell is typed (`0x00536a37`),
which the drain, coming before the footprint passes, always finds. Each pop measures the queue again and puts out
whoever stands past its end: `ride-operation.md`, "The sale's drain".

**The paths before its ends go back to ordinary path.** A first footprint pass looks past each end — the
entrance along the opposite of its direction byte, the exit along it, which for a queued entrance leads
into its own footprint and so finds nothing — and a NOMODIFY path found there loses the flag
(`FUN_0053a530( 0x20 )` is `AND NOT` on `+0x0e`), unless it still serves two or more ends or is
design-map path (bit 8 of its `+0x26` seed, which in the jungle marks exactly the ten-cell avenue). The
second pass clears the footprint, unlinking the entrance from its four cardinals and the exit from all
eight, so each such path also loses its bit toward the end. The path the queue joined keeps the ride as
its owner, pointing at an empty cell. **Move is the same call with the same flag**, after one extra walk
of the queue whose saved corner list nothing reads - see "Moving a thing".

#### The demolisher's order, and the cells it leaves

Decoded 2026-09-23, every claim re-derived by a refuter unless marked. **`FUN_00527ee0( x, y, reentered )`**
resolves the thing through `FUN_00527d60`, which answers only for a cell typed 4, 9 or 10
(`0x00527db3`..`0x00527dc7`), and nothing refuses an occupied thing. In the Lost Kingdom save the gates,
the lights and the vehicles stand on no such cell (measured over the whole map), so the demolisher is
**inferred** never to reach them; whether anything else deletes one in play is untraced. The third
argument is 1 only from the track-record clear `FUN_0053b280` (`0x0053b703`); Delete and the move pickup
both pass 0 (`0x00525e7f`).

Then, in order: only for a queued thing, the queue drain above (force raised around it and lowered
after); a first footprint pass letting the ends' paths go; **a second footprint pass
(`0x0052842b`..`0x00528570`) that calls `FUN_005367a0( 0, 0 )` on every on-map cell of the shape except
its empty `.` ones** (skipped at `0x00528462`), with the force flag at nought (`0x00528506`); the BUMP
physics world freed unless re-entered (`FUN_00545610`); then `FUN_0050b780` at `0x00528590`, which sends
message `0x1b` and runs the object destructor `FUN_004dd0a0`; and last, unless a layout is being
replayed, a demolish sound - **`0x96`, `0x97`, `0x98` or `0x99`** for fewer than 4, 8 or 16 body cells,
or more.

**A footprint cell of type 4 goes straight to the clear's reset** (`0x00536bc9`), the block its queue arm
also ends in; the path arm writes the same fields itself (`0x0053696a`..`0x00536994`) and joins at
`0x00536bf3` for the type and the retile. The reset leaves `mType` 0, `mNeighbours` 0, direction 0, **the
whole `mFlags` word** 0, owner 0, tile `(0, 55, 0)` - the tile every bare cell of the shipped park carries -
and the mesh freed. The overlap counter is **kept** on type 4 and **zeroed** on the ends (the queue arm at
`0x00536abf` for type 9, the path arm at `0x00536834` for type 10), which also unlink their cardinals and
all eight neighbours respectively. The ground cell is restored from the snapshot at `DAT_0079fc54`,
keeping `0x0100`.

**So a sold thing leaves bare in-park ground.** The build pass clears each cell before it stamps it
(`FUN_005367a0` at `0x0052910c`), `FUN_00535600` allows type 4 only over type 0 or 1, and the placement
verdict `FUN_00535670` refuses `0x40` land (`0x005357c7`). That is what the cells held unless a path ran
there, and that path does not come back. Whether every placement route reaches that verdict is open -
`FUN_00532fc0` consults it only for ops carrying `0x100` or `0x200` (QUEUE Q37).

**OpenTPW writes that reset** (`ParkPathBuilding.Cleared`) from `Unstamp`, the queue drain (`DrainQueue`), the path
clear (`ClearPathCell`) and the queue-cell lift (`LiftQueue`). `Unstamp` clears only the cells the thing owns, which for a thing the save placed leaves out its
`.` cells as the original does, and it differs twice, both because its placement verdict is unbuilt
(`PLACEMENT_TERRAIN_RULE`) and a thing can stand where the original's cannot: it keeps the `0x40` flag, and
a cell the save records as terrain the original never builds on goes back to the save's own record.
The path and queue verdicts refuse `0x40` land, so the other three writers clear the whole word as the
original does. The demolish sound is counted (`DEMOLISH_SOUND`).

**The object destructor `FUN_004dd0a0`** (one caller, `FUN_0050b780` at `0x0050b866`), in order: the
item's built count down (`FUN_004d3d10`, record `+0x18`); the object chain unlink (`FUN_00519dc0`); **the
type-10 "object removed" message** on the bus at `DAT_00788d3c` (`0x004dd0f0`..`0x004dd150`); the refund,
`price * FUN_004e2290 / 100`; the region effects of flag bits `0x10`, `0x80` (only while script variable 0
reads below 2) and `0x01`; the upgrade sprite at `+0x54`; **the script teardown, mode 0, 4 or 7**
(`0x004dd2c9`, see `park.md`, "What selling a thing does to its script"); the build tool's picked thing
cleared if it is this one; and `FUN_0050b980`, which leaves the anchor cell's list and destroys the model.

#### Selling and the people on it

Decoded 2026-09-23 (`docs/QUEUE.md` Q36), every claim put to two refuters, one reading disassembly and one taking a
second route. **The object destructor's type-10 message puts everyone off at once.** `FUN_004dd0a0` is its only
sender: it builds `{vtable 0x006fd7e0, +8 = the thing's id}` and hands it to the bus `FUN_0040fb10` at `0x004dd150`,
after the chain unlink (`0x004dd0eb`) and before the refund and the script teardown (`0x004dd2c9`). The bus keeps
one `std::set<u16>` of thing ids per message type and calls `FUN_0050b550` on each subscriber in ascending id, which
switches on the thing's kind byte `+2`. The walker base constructor `FUN_004f8940` subscribes every guest (kind 1),
every member of staff (4 mechanic, 5 handyman, 6 entertainer, 7 guard, 8 researcher) and the kind-18 things
(`0x004f8a58`); the kind-17 relay built by `FUN_0050c9b0` subscribes too. Kinds 17 and 18 answer it with an empty
`RET` (the kind-17 relay answers message `0x1b` through `FUN_004818c0`, which acts only on a guest). Delete and the
move pickup both reach it, so **a move puts everyone off as a sale does**.

**A guest (`FUN_004fb360`) answers only when `MajorDest` (`+0x1dc`) is the thing** (`0x004fb383`), in any state:

- **Riding, state `0x10` exactly** (`0x004fb38d`): the sprite is brought back, `+0x28` (the ride holds the sprite)
  is cleared, event `0xd` goes into the guest's event ring, and the kids' effect `0x80` plays at the sprite
  (`FUN_004faa00` at `0x004fb3f5`). No settle-up, no charge, **no move**: the guest stays where their record has
  been since admission, the stand point before the entry. Admission tests the thing's flag bit `0x20`
  (`0x0050212b`): set, it keeps the sprite and sets `+0x28`; clear, it destroys the sprite (`0x00502147`), and the
  eviction makes a new one (`FUN_004d4140(3, ...)` at `0x004fb3cd`) whose position is still nought when the sound
  reads it, so **that sound plays at the world's origin**. Every visitable thing in Lost Kingdom's save carries
  `0x20` (the Belly Bounce's flags are `0x012c`).
- **Queueing** - `FUN_00502430`: state 11 to 14, or state 8 with the saved state 11 to 14 - runs `FUN_005012f0`:
  event 6; the kids' `0x80` only when the guest's own id `& 7` is nought (`0x0050133d`); happiness down by
  `MediumHappinessChange`; `mQPrev`, `mQNext`, `mBeenAdmitted` (`+0x1f8`), `MajorDest` and `mQueuePos` zeroed;
  state 6. **It never tells the thing.** The six other callers of `FUN_005012f0` unlink the guest first
  (`FUN_004ddd20`); the sale does not, so the object keeps its head, and every queuer clears only their own links.
- **Then everyone chosen**: happiness down by `SmallHappinessChange` (`FUN_004fe980(0)`, clamped by `FUN_004fb4f0`),
  `MajorDest` zeroed (`0x004fb444`), state 6 (`0x004fb4a1`), which queues the stand. A queuer loses 20 in all in
  Lost Kingdom, everyone else 5 - unless the demolisher's queue drain, which runs first and re-walks the queue, has
  already put them out for 15 alone - which it does to every queuer from the fifth place back but the nominee and a
  guest in raw state 14 (`ride-operation.md`, "The sale's drain"). A guest walking to it, leaving it, or still
  naming it from a ride they left is stopped the same way.
- **Every guest, chosen or not**, clears the saved major (`+0x1de`, the minor decision's: `ride-operation.md`, "A second
  toilet") naming the thing, and each `mPreviousRides` entry naming it with its `mPreviousTemporaryRides` pair
  (`0x004fb4a6`..).

The event ring (32 entries from guest `+0x34`, in the history block at `+0x30`: `ride-operation.md`, "The settle-up's
bookkeeping") is read only by a debug dump that prints through a logger which is a bare `RET` in this build
(`FUN_005da3c0`), so its entries change nothing a player sees.

**Staff** answer through their kind's handler, and every kind ends in `FUN_00504c70`. A mechanic whose ride job
(`+0x218`) or a handyman whose toilet job (`+0x21a`) is the thing drops it and goes idle, in any state. Then, when
the rest area (`+0x208`) is the thing: **one resting (state 3)** is put out by `FUN_00506d10`, which sends message
15 to the resting-staff list, takes one off the rest area's script variable 0 (`VAR_STAFFIN`, while the script is
still alive), clears `+0x208`, rebuilds the sprite and sets state 0. Then `FUN_00506910` finds the nearest other
object with flag bit `0x02` by squared cell distance along the live chain, **testing no reachability**, and
`FUN_004fa530` routes to its `mEntryPos`; on a route `+0x208` is set to it, **but the state stays 0**
(`0x00504d8f`), so the claim waits for the next time they are sent to rest. **One on the way there (state 2)** has
`+0x208` cleared and goes to state 0. Other states keep the claim.

**Who a sale reaches depends on `MajorDest` being let go of where the original lets it go.** The chooser
`FUN_004fcb10` clears it before it chooses (`0x004fcb21`), chosen or not; a guest leaving a ride keeps it on arrival
(`0x005009f9`) until then, so a sale of the ride just left still puts them off.

**What OpenTPW builds** (`PeepBehaviour.ThingRemoved`, `StaffBehaviour.ThingRemoved`, called through
`ParkPeople.ThingRemoved` from the demolisher): all of the guest's answer but the event ring, which it does not keep -
a saved major naming the thing is let go of by every guest (`Peep.SavedMajorDest`, `0x004fb4a6`..`0x004fb4b3`), and
`mPreviousRides` and the refusal beside it are cleared (`Peep.ForgetThing`, `0x004fb4ba`); the sound, at the seat node, the origin by flag `0x20` (which a thing bought
this session does not carry yet, `BOUGHT_OBJECT_FLAG_BITS`), or - a deviation - at the feet of a walk-on rider, whom
the original's `WALK` stepper places and nothing here does; and the staff rest-area arms. Nothing here holds a job on a
thing. The `VAR_STAFFIN` count and message 15 are unbuilt wherever the original touches them - arriving to rest
(`FUN_00505fe0`), the end of a rest (`FUN_005061d0` at `0x00506286`) and the eviction - and all three count
`REST_AREA_OCCUPANCY`. Entering state 0 queues the stand (`FUN_005054d0` through `FUN_004fa460`), which OpenTPW's
`Staff.AnimationFor` does not. A tired member of staff's search walks the live chain too, so a sold rest area is
never offered.

#### Moving a thing

Decoded 2026-09-23 (`docs/QUEUE.md` Q5), every claim put to two refuters and their corrections folded in.
**The object window's `b_move`, `FUN_0048cfa0`, is Delete's demolish followed by the move tool, and the
put-down is a purchase that keeps the old thing's facing.**

- **The pickup.** It latches the window's thing (`FUN_0048c940` into `DAT_007c2654`) and selects it
  (`FUN_005276d0( 1 )`). For an item whose `+0x40` is set it walks the queue once (`FUN_00530120`, then
  `FUN_0052fc30` copies the corner list to `0x00818d98`); **nothing in the image reads what it saves**
  (`DAT_0081b14c`, `0x00818d98`, `DAT_00820a90`/`a94`), though the walk rewrites the queue cells' `+0x20`
  counters and unlinks the queue's end. Then the demolish, `FUN_0052f200( 0x33, 1 )` and
  `FUN_00524960( x, y, 0, 0 )` at the thing's own cell - with the latch and the select before them, the
  same four calls Delete makes - **but with no confirm box**: the demolisher refunds
  `price * FUN_004e2290 / 100` there and then, before the move tool exists. **The pickup never tests the
  balance** - its five branches are the item's `+0x40` and four null checks - and **never asks whether the
  demolish happened**, which it does not when the red latch `DAT_00816d48` is set or the cell is not typed
  4, 9 or 10.
- **The object is not freed yet.** `FUN_0050b780` marks it dead (`+2 = 2`, `+3 = 1`) and queues its id on
  `0x00801f38`, which the per-tick `FUN_00516380` drains. So the reads that follow are sound: `+0xe` is the
  item id (a key `FUN_00412e90` searches for, not an index) and `+0x10` the angle in degrees.
- **Then the move tool.** `FUN_0046c5a0( 0x3b, itemId )` builds a type-3 shell
  `{0x006fe9e0, 1, 0x3b, carrying 1, itemId}` and `FUN_0046c350` installs it; its OnInstall `FUN_0046d5a0`
  sets tool `0x3b` and the item. Then `DAT_0081b3ac = thing` (**written once, read nowhere**),
  `FUN_0052f200( 0x3b, 1 )` again, `FUN_0052f880( &itemId, 0 )` - the item in hand `DAT_008186e0`, with the
  add-on flag `DAT_008187f8` forced to 0 - and **`FUN_0052f1b0( [thing + 0x10], 1 )`, which makes the
  carried rotation `DAT_0081d7a4` the thing's own angle**. The demolish had zeroed it (the `0x33` arm's
  `FUN_0052f1b0( 0, 0 )` at `0x00525e6c`). Last it zeroes the window's thing `DAT_007c2658` and closes the
  window through vtable `+0x2c`, the close `b_okay` runs - **except that with the thing cleared, the close
  skips the slot that commits the window's buffered sliders** (`+0x3c`, guarded at `0x0048d24a`).
- **The put-down.** The `0x3b` arm of `FUN_00524960` (`0x005253e7`..`0x00525a1b`) calls the placer as a
  purchase does, `FUN_00528a70( x, y, 4, DAT_0081d7a4, 1, 0 )`, which constructs a **new** object through
  `FUN_004db090` and debits the item's full `+0x1b8`. Nothing reads the old object, so **a moved thing
  faces the way it stood and everything else - upgrades, build date and so scrap value, ticket price,
  speed, duration, name - starts afresh**, as a rebuy does. After it, mode 4's sequence: a queued thing
  hands over the queue tool on its new entrance, anything else goes idle unless Ctrl alone is held, which
  keeps the tool and the rotation to build another at full price. The arm differs from mode 4's in running
  op `0x82` at both track steps where mode 4, at the first, writes the cell's direction from the angle; in
  having no add-on branch; and in never clearing `DAT_008186c0`, which mode 4 zeroes after anchoring the
  queue (`0x005252de`).
- **A refused cell keeps the item in the hand.** The commit's only refusal is the preview's red latch
  (`0x00524a63`): sound `0xaf`, then the plain exit `0x0052765b`, which touches neither the tool, the item
  nor the rotation, so the next click tries again. **Money turns a move's cell red**: the `0x3b` preview
  (`0x00523940`) sets the pending cost to the item's `+0x1b8` (`0x0052398b`) and the per-cell verdict
  `FUN_00535670` reddens a cell whose cost is above the balance (`0x005358fc`); equal passes. Unlike mode 4
  there is no up-front cash test and no golden-ticket (`+0xc4`) exception in the move's preview.
- **Abandoned, a move is a sale.** Nothing puts the thing back or pays the difference, whatever the way
  out; the shell's uninstall is a bare `RET`. A quick right click with the option on (`0x00488434`) and
  Escape (`0x0040c368`, unless it first closes an open locator) install the idle mode and call
  `FUN_0052f200( 0, 1 )`, which zeroes the rotation. **With the option off a right click leaves the move in
  the hand** - the shell's right-button slots are `RET 8` - and so does a held or dragged one with it on.
  Another tool is installed straight over it (`FUN_0046c350`), which leaves the rotation as it was, and a
  purchase then inherits it (see "The hand's ways out"). Leaving the park drops the shell too; see "Leaving a
  park with something in the hand".
- **Open.** What a successful put-down leaves in the rotation, and so what the next purchase faces; the
  add-on and `+0xc4` cases; whether a moved thing gets its old thing id back.

**OpenTPW** (`ParkBuilding.PickUp`, one body for the window and the console): the sale, then the item into
the hand at the thing's angle, with no affordability test; `Move` is that and one put-down, which a
refused cell leaves in the hand. **A deviation, declared at the site**: the hand stays empty unless the
sale went through, where the original would carry a thing that still stands. **Counted, not built**: the
refusal's sound `0xaf` (`PLACEMENT_REFUSED_SOUND_0xAF`) and the markers drawn while carrying. Letting a move
go is `ParkHand.LetGo`, the same for every way out; see "The hand's ways out".

### Hiring is a placement verb, and there is no hire fee

**`FUN_00507bf0` does not create a person.** It builds a **type-5** "place staff" mode carrying
(thingType, grade, costume, nameIndex); the staff thing is constructed on the next left click at the
cursor cell. Cancelling returns the candidate to the pool; completing removes it.

**There is no one-off hire fee anywhere on that path.** `StaffPoolInfo.BaseCostPerStaff` (2000) and
`CostPerQualityLevel` (100) exist in the balance file and are **never read by the executable**. The only
money is a monthly wage, `PerGradeStaffConsts[grade].BaseWage * PerTypeStaffConsts[kind].PayMultiplier`,
debited per staff thing on the new-month event - and dismissal charges exactly one further month.

**Type 5 and type 6 are different modes and the difference matters**: type 5 (a fresh hire)
**constructs** a new thing; type 6 (an existing worker picked up) **moves** the existing thing to the
drop cell's centre, and every kind then goes idle but the mechanic, whose job search runs at once (see "The
hand's ways out"). The pickup `FUN_00505c50` never refuses, but the staff window's PICK UP button is enabled
only when `FUN_00505bd0` passes: not in states 4, 5, 7, `0xd` or `0x12`, and not when the worker's cell fails
`FUN_004fa990` (kinds 0, 1, 3, 9 and 10 pass).

The candidate pool is 32 records of 20 bytes:
`{int kind; int nameIndex; byte grade; byte costume; byte occupied; byte takenForPlacement; int createdTick; int lifetime}`.
Grade runs 0..4 and the hire screen draws it as `(grade << 10) / 5`, **so a maximum-grade candidate
fills only 80% of its meter**. Names come from five per-kind tables of 35 entries each; there is no
`MALE_NAMES` table in the executable at all.

**Two staff numberings, and they are different permutations** - the thing type (handyman 5, mechanic 4,
entertainer 6, guard 7, researcher 8) and the sprite bank (entertainers 4, handymen 5, mechanics 6,
guards 7, researchers 8).

#### Fresh staff sprites (Q207)

New staff need no saved employee template. Native constructors call `FUN_004d4140(3, kind, bank)`:
mechanic `004d9eb0` uses kind 6, cleaner `004d6b60` kind 5, entertainer `004d4340` kind 4 and researcher
`00502600` kind 8, each taking the candidate costume's low byte. Guard `004d5de0` instead selects a new kind-7
bank through `00541f70`: `(random >> 2) % loadedCount`. The common sprite constructor `004758f0` initializes
set/height/facing to zero, alpha 255, state 1 and unit scales. `004d4140` uses standing script table entry 3.
These were rechecked in a private hash-matched Ghidra copy of the 2.0 executable.

`ParkSpriteBanks.Read` counts the archive independently of saved people. Kinds 5–8 obey the detail cap;
entertainers (kind 4) are uncapped. The renderer packs all those banks before the first hire. `ParkPeople.Hire`
builds a new picture from the candidate and those counts, and adding the first employee builds its live mesh.
Missing staff banks still refuse hiring. Saved staff retain their own pictures and animation state.

**Verified in a genuinely fresh Full Simulation park:** zero initial people/sprites, then five actual hire-screen
selections and XTEST world drops. Predicted staff count 0→5, kinds 5/6/4/7/8, empty candidate hand and $50,000
unchanged all matched censuses and screenshots. `runtime-final/all-five-hired.png` shows all five together;
`five-after-ticks.png` and the accompanying census show them after 100 more frames. No fake employees were seeded.
The final sprite factory tests also pin nonzero costume bytes and the guard's separate selection.

Evidence: the Q207 working folder, outside the repository and named in the machine notes (`PLAN.md`,
`staff-*-native.txt`, `mutation-results/`, `runtime-final/`, `price-fixture.json`). Original saves are
hash-protected; runtime uses disposable copies and muted audio. Original-hire and original-atlas restorations
fail two and four regression cases; wrong sprite mapping fails one. Original-price and original-profile
restorations fail three and one. Restored full suite: **1,764 passed, zero failed/skipped**.

**Limits retained:** the candidate pool still generates costume 0 rather than native costume variation;
shared RNG parity, staff carry preview, placement-cell filters and the existing Q136 behavior deviations are
outside this repair. Staff bank content/order and detail limits are documented in FileFormats `sprites.md`.

#### Putting a candidate down: the type-5 mode

Decoded for `docs/QUEUE.md` Q6; every claim below was put to a refuter.

**The pick.** The hire list's handler `FUN_0049b650` calls `FUN_00507bf0( slot )`, which builds the mode
with `FUN_0046c6d0` (vtable `0x006fea40`: `+0xc` thing type 4..8, `+0x10` grade, `+0x11` costume, `+0x14`
name index, `+0x8` the preview, `+0x18` a placed flag, zeroed), installs it through `FUN_0046c350`, and only
then marks the record taken (`+0xb`, `0x00507f90`). The screen then closes itself and posts advisor message
`0x135`. `FUN_00507bf0` refuses only a slot of 32 or more and an unoccupied record: not a kind at its
staff cap, not money, not a candidate already carried.

**The slots.** `+0x04` LEFT down `FUN_0046c8e0`, `+0x18` MOVE `0x0046c480`, `+0x24` `0x0040f2d0`
(returns 5), `+0x28` OnInstall `0x0046c730`, `+0x2c` OnUninstall `0x0046c890`. Every other mouse slot is
`0x006b70d0`, a bare `RET 8`. OnInstall sets cursor 9 (`c_carry.ani`) and builds a world sprite of the
candidate's kind in their costume (banks 6, 5, 4, 7, 8 for types 4..8), which MOVE carries under the
pointer.

**The click reads the hovered cell, `DAT_007b05cc`**, never the coordinates the dispatcher passes, and
tests it (`0x0046c928`..`0x0046c974`). **It refuses** when:

1. the cell's track record, after the redirect to the parent for track types 12 and 17, is track type
   11, 13, 16, 18 or 25 (`FUN_005363f0`: `FUN_004d0af0` fetches the cell's own record in the parallel
   `0x28`-byte track layer at `world + 0x1102d8`, `FUN_0053ad90` tests 12/17, `FUN_0053ad30` the five);
2. `mFlags & 0x40` (`FUN_00535db0( 0x40 )`);
3. the redirected track type is 25 (`FUN_00536450`, which has no zero-parent guard; redundant but for a
   12/17 record whose parent is 0);
4. `mType` is not 0, 1, 3 or 9 (`FUN_00536390`, `FUN_00536310`, `FUN_00536320`).

The meaning of those four kinds is `park.md`'s (1 path, 3 queue, 9 a thing's entrance),
not proven again here; `+0x08` is the save's `mType` (see "The runtime cell is not the save cell").

**A refused click does nothing at all.** `0x0046cb76` hands "Cannot place staff member here - the cell
is of type %d" to the inert logger and returns (`RET 8`, `0x0046cb9b`): no sound, no advisor line, no mode
change, `+0x18` still 0, the pool untouched. The candidate stays on the cursor, still taken, and the next
click tries again. The only feedback comes before the click: over a refused cell MOVE draws one red
marker square (texture 1) under the preview.

**An accepted click** constructs the worker on the cell (type 4 `FUN_004d9eb0`, 5 `FUN_004d6b60`, 6
`FUN_004d4340`, 7 `FUN_004d5d40`, 8 `FUN_00502600`), sets `+0x18` to 1, even when the allocation fails,
and installs the idle mode (`0x006fea10`) through the setter. No money moves.

**OnUninstall runs on every ending**, because the setter calls the outgoing mode's `+0x2c` before its
destructor. It frees the preview, calls `FUN_005083b0( placed ? 0 : 1 )` on the park, and withdraws
advisor message `0x135`. `FUN_005083b0` takes no key: it walks all 32 records and, for each one marked
taken, clears `+0xb` and, when placed, also clears `+0xa` (occupied) and deletes the candidate's hire-list
row (`FUN_00481550` → `FUN_0049c970`). **So a carried candidate leaves the pool only on an accepted
click**, even one whose allocation failed; every other ending returns them. While carried they never
expire (`FUN_005084f0` skips a taken record), and a hire list built then still shows them
(`FUN_0049b5b0` never reads `+0xb`).

**Every way out without a drop returns the candidate**: a quick right click with RMB cancel on, which is
the default (`0x0048842b` installs the idle mode whatever the current one is); Escape (`0x0040c180`),
unless the staff/visitor locator is open (`DAT_007cc2f0`), which it closes instead, and the menu does not open; the extended
Delete key, which installs Clear Land (`FUN_0040c5e0`); picking another candidate, if the hire screen can
be reopened while carrying (see Open), the old one returning first, after which every untaken candidate of a kind at its staff cap is purged (`0x00507f9b`..
`0x00507fcd`); and leaving the park (see "Leaving a park with something in the hand"). A
held or dragged right press, or any right press with the option off, leaves the candidate in the hand.

**The picker never hands the mode a cell off the map.** `FUN_0045d560` clamps the pick to an edge cell,
and a ray that hits nothing leaves the last hovered cell standing, so any nonzero id it writes is a map
cell. It writes 0 only if a runtime limit (`DAT_007a0314`, `DAT_007a0374`) is 0 or less, or a grid
dimension and its limit are both above 128; those limits are filled at run time and were not read, so
"never 0" is likely, not proven. The click does not test for 0.

**Open.** What advisor message `0x135` says (it resolves through a table filled at runtime); what puts
the cursor back after a drop or a cancel; whether the staff constructors' later calls make a sound;
whether a single click on a hire row picks, since a double click always does; whether the hire screen
can be reopened while a candidate is carried (no static path from its opener `FUN_0049bdd0` tears the
mode down, but `FUN_00485b40`'s message 5 to an open panel is not covered).

**OpenTPW** (`ParkStaffPool.PlaceCarried`, and `ParkStaffPool.Hire`, which the console's `hire` shares):
the worker goes up first and the pool loses them second, and a refusal leaves the candidate on the cursor
and in the pool. **Its refusals are its own**: a cell off the map, a kind the park packs no picture
for, and any hire while the park has nobody, staff or guest, to copy a walk from. The cell rule is counted, not built (`STAFF_PLACEMENT_CELL_RULE`), and so are the carry cursor, the
preview and its red square (`STAFF_CARRY_PREVIEW`); both are `docs/QUEUE.md` Q40.

### The hand's ways out

Decoded for `docs/QUEUE.md` Q39 (four decoders, each put to a refuter; the corrections folded in).

**There is one hand.** Carrying an item or a moved thing (type 3), a candidate (type 5) and a worker (type 6) are
all the one current mode `DAT_007b05d8`, and the setter `FUN_0046c350` runs the outgoing mode's `+0x2c` and its
destructor before it installs the next, with no test of either type (`0x0046c378`, `0x0046c388`). Every pickup
installs through it and none refuses over what is already held: the buy row (`FUN_004ac270`), the hire row
(`FUN_00507bf0`), the ride window's Move (`FUN_0048cfa0`, `0x0048d0fb`), its queue button (`FUN_004af200( 0 )`,
a type-3 shell for mode `0x14`), the staff window's PICK UP (`FUN_00505c50`, the only way to make a type 6, which
first installs the idle mode if a worker is already carried, `0x00505d6a`), the camcorder (`FUN_00481a10`,
`0x00481ad0`) and the Delete key's Clear Land (`FUN_0040c5e0`).
So two things are never held together, and whatever was held goes the way its uninstall says: a carry shell's
is a bare `RET` (`0x005d1750`), so a bought item is dropped and a moved thing stays sold; a candidate goes back to
the pool (`0x0046c890`, `FUN_005083b0( 1 )`); a worker is put back down. Statically the buy and hire screens open
with a full hand: nothing on their way reads the mode.

**The right button** (`Park_MouseMessageProc`, `0x004881a0`) reads RMB cancel, `DAT_0078d911`, before anything
else (`0x004881ae`). With it off a right press only takes and gives back the mouse capture and goes to the camera.
With it on, the press arms a click (`DAT_007c2500 = 1`, `0x0048833a`), stamps the time in milliseconds
(`DAT_007c2504`) and the point in the interface's 2048x1536 units (`DAT_007b5344`, `DAT_007b9774`), and holds the
press back from the camera. A move of **more than 8 units across or down** (`0x00488265`, `0x0048827f`) or a tick
**more than 200 ms** after the press (`0x0048823f`, `0x00488282`) disarms it and hands the camera the press late,
as a right drag; a move back does not re-arm it. **A release while it is still armed** installs the idle mode
whatever the current mode is - there is no type test - then calls `FUN_0052f200( 0, 1 )` and `FUN_004989d0`,
which closes the coaster bar if it is up (`0x0048842b`..`0x0048843c`). Every mode's right-button slots are
`RET 8`, so nothing else a right press does reaches the hand.

**Whose a right press is** (decoded for `docs/QUEUE.md` Q56: three decoders, each put to a refuter; static only).
The window procedure posts a right press as it does a left one (`0x0046b6a5`, button 1) to the control under the
pointer (`FUN_00658af1`), with no hit test at the press, and the release to that same control wherever the pointer
has gone (`0x00658b88`). Nothing passes a press to a parent: the base proc's `0x10005` case (`0x0065f820`) posts the
press on (`0x11005`, `0x10007`) only to itself, its one other message the focus change's `0x1b` to the old focus. `Park_MouseMessageProc` is installed on the park's layer 0 alone (`DAT_007cb2ac`, built at `0x0048a0bc`),
so **a right press arms the click only when it lands on that layer**, and nothing on its way to the arm reads a
pause, a modal flag or an open screen (`0x004881a0`..`0x0048833a`). What keeps a press off the layer:

| Up | Where a right press goes | Arms |
|---|---|---|
| nothing | the layer, or a gadget control: the gadget's roots are the layer's children (`0x004a1da4`). The body `0x1d` answers inside its 23-point outline, its children over their rects, the aerial `0x2d`/`0x2e` too; the money counter `0x2f` and the key block `0x33` are disabled at build (`0x004a22e1`, `0x004a1e02`) and take nothing | on the layer only |
| the game menu (both scenes) | a full-screen panel `MenuList_Create` makes (`0x00492ef0`) and `MenuList_Show` attaches last to the UI root | never |
| a message box | the full-screen control `UI_LoadModalTree` loads it into (`0x0047eda3`) | never |
| the options or the map screen | anything but the layer, which each hides with message 6 (`0x004a3ad9`, `0x005f0bd6`) | never |
| first person | the viewfinder's full-screen layer 1, whose handler `FUN_00488a00` gives it to the camera table, where no row binds a mouse key, and answers its click with RMB cancel on by leaving first person (`hud.md`, "Four ways out of camcorder mode"): entering (`FUN_0042ab20( 2, 1, ... )`, `0x0042ac7f`) calls `FUN_004a2ac0( 0 )`, which hides layer 0 and shows layer 1 | never |
| a management screen (buy, hire, all staff, visitors, all items, entry price) or any of the nine object windows | built onto the layer by `UI_LoadTree` (`0x004acd62`, `0x0049bf34`, `0x00496643`, `0x0049353e`, `0x00495abe`, `0x00498db5`, `0x0048ceca`), not modal, and none hides the layer. Each root is one plain rectangle - big (186,30)-(2018,1007), medium (248,30)-(1800,1007), small (328,130)-(1720,901) - so the window takes a press anywhere on it | beside the window, and the window stays open; on it, never |

The one test of an open screen on a press's path, `0x00488741`, is in the left and middle buttons' case, after the arm:
it keeps the press from the idle click and the mode's button-down slot ("A park screen is open"). A right
click on a row of the all-staff, visitors or all-items list, or on an object window's preview, moves the camera and
closes the window (the 500 ms click limit `[0x0077c480]`), never touching the hand. The full-screen view, F3
(`FUN_004a29d0`), hides the layer under a full-screen control whose handler `0x004a2840` gives a right press to the
camera and arms nothing ("The full-screen view: F3"). A press made before a window
opens keeps the capture on the layer (`0x004882ba`), so its release can still let go.

**OpenTPW.** `WindowStack.TakesRightPress`: a control under the pointer, a modal window, or a park screen's root
(`UiWindow.ParkScreen`, `WindowStack.OnParkScreen`) takes it, and `Level.RightPressTaken` adds first person (and gives
the park every press while F2 hides the HUD). A left press reads the same three (`WindowStack.PointerTaken`). No park
screen is modal, so the gadget answers beside one. The gadget's body takes a press inside its outline,
and its arm, handle and aerial take theirs (`hud.md`, "The arm and the aerial as built"). A right click on a list row and a click on an object window's
preview move the camera and close the window ("The camera goes to a thing"). `Level.LeftPressTaken` keeps a left press off the park in first person too (Q116): the
viewfinder layer's `FUN_00488a00` gives a press (`0x10005`) to the key table (`FUN_0040c900`) and every message to
`FUN_0042a760`, where a left press only sets bit 1 of the camera's button state `DAT_00790aac` (what reads that bit is
not traced here).

**Escape** is the game table's row 0, `0x0040c180`, run on the key's **release**. If the staff/visitor locator
is open (`DAT_007cc2f0`) it retracts the arm and answers 1 (`FUN_004816b0`). Otherwise, over any mode but 0 or 1,
it installs the idle mode (`0x0040c35f`), calls `FUN_0052f200( 0, 1 )` (`0x0040c368`) and answers 1, which stops
the chain before shortcuts row 0 can open the menu. Over the idle mode it answers 0 and the menu opens. So with a
full hand the first Escape empties it and the second opens the menu. In first person the key goes to layer 1
instead, over a park screen to that screen, which a plain Escape closes and nothing more
(`scenes.md`, "The park Escape route"), and in the coaster bar to the coaster table's `abortcoaster`.

**`FUN_0052f200( 0, 1 )`** records tool 0, withdraws the tool's advisor lines if the tool changed, zeroes the
tool `DAT_0081ae2c`, sets the anchor to -1, copies the rotation `DAT_0081d7a4` to `DAT_0081b134` and **zeroes it**,
installs the idle mode a second time unless the old tool was `0x15` or `0x16`, and clears the red latch. A
candidate's or a worker's pickup (types 5 and 6) calls none of it, so the tool, item and rotation globals stay under
the new mode; a carry shell's OnInstall (`FUN_0046d5a0`) sets the tool with `FUN_0052f200( verb, 1 )` and, when it
carries something, the item with `FUN_0052f880`. **Neither sets the rotation**: `FUN_0052f200` writes
`DAT_0081d7a4` only for verb 0 (`0x0052f3b6`), so a thing bought over a carried move faces the moved thing's way -
unless it is an add-on hovered over one of the five track types, which the tool-4 preview snaps (`FUN_0052f1b0` at
`0x005237f7`, inside the preview's add-on branch; the `0x3b` arm has no snap, so nothing turns a carried move).
What a successful put-down leaves in the rotation is not decoded.

**The worker (type 6)**, vtable `0x006fea70`. The mode stores only the worker's sprite bank and costume; the
worker's id is kept in the world's hand thing (`+0x60`, through `FUN_00509ae0` and `FUN_00509af0`). The pickup
frees the worker's sprite and sets state 7, which every staff machine answers with a bare return, and saves no
position. The drop (`0x0046cc40`) reads the hovered cell and refuses with the type-5 rules ("Can't put staff
here", nothing else). The uninstall (`0x0046cdc0`), when the hand still names a worker, logs "Dropping staff where
he was before he was picked up" and puts them down on **their own current cell** with the drop's body
`FUN_00505ea0`: the centre of the cell, the walk reset, the cell lists relinked, the sprite rebuilt. Every kind
then goes to state 0 **except the mechanic, whose job search runs at once** (`FUN_004da5b0`). The right-button
slots are `RET 8`.

**Open.** What the world's hand thing at `+0x1da718` is; whether anything moves a carried worker (a carried
mechanic or handyman whose ride or toilet is deleted leaves state 7 by message 10 and can then walk; whether
anything deletes one while a worker is carried without first changing the mode is not traced); what sends the park
window message `0x15`, which also disarms the click.

**OpenTPW** (`ParkHand`): the item, the candidate, the worker and the build tool are four pieces of state, and
`ParkHand.LetGo` is the idle mode installed over them. Every pickup - `ParkBuilding.Carry` and `PickUp`,
`ParkStaffPool.Carry`, `ParkPeople.PickUp`, `ParkPathBuilding.EditQueue`, `ParkCamcorderCameraMode.Enter` and the
Delete key (`Level.ClearKey`) - lets go first; a quick right click (`Level.RightButton`), Escape
on its release (`ParkFrontEnd.MenuKey`) and leaving the park (`Level.ForgetPark`, before anything in the park is deleted) let go
and nothing more; and a sale lets go when no item is held and no build tool armed, as the demolisher's restore of
tool 0 does (`0x0052818d`).
`ParkPeople.PutBack` puts a worker down in their own cell through the drop. **Not the original's**: the rotation is not the one
global, so a purchase always starts at nought; and a mechanic put down goes idle (`MECHANIC_PUT_DOWN_JOB_SEARCH`).

### A park screen is open

Decoded for `docs/QUEUE.md` Q115, first-hand, and run in the original.

**One at a time.** The six management screens and the nine object windows are built onto layer 0 and the one that is
open is `DAT_007c24c8`. Every opener first calls `FUN_00485b40`, which sends the open one message 5 (close), zeroes the
global and hands the focus back (`FUN_004862a0`): buy `0x004acd5d`, hire `0x0049bdec`, all staff `0x00496635`, all items
`0x00495ab0`, visitors `0x00493530`, entry price `0x00498da7`, the object windows' shared opener `0x0048cebb`, park
status, finances, loans, staff costs and research among its 22 callers, and beside them the game menu (`MenuList_Show`, `0x00493171`), the
map (`FUN_005f0b40`, `0x005f0bd1`) and first person (`FUN_00481a10`, `0x00481a2b`). The buy opener is the one that
looks first: with its screen already up (`DAT_007cc1f8`) it picks the tab again and returns (`0x004acca0`). The
gadget's category opener `FUN_004a0940` has one gate, the game menu (`FUN_0048c8d0`), so its buttons answer beside an
open screen and the screen they open replaces it.

**What opening one does**, `FUN_00485b70( screen )`, called by each opener once its tree is loaded:

| Step | Site |
|---|---|
| a mode of type 9, the camcorder's pick, is replaced by the idle mode; every other mode stays, the build tools and the hand included | `0x00485c1b` |
| the layer's cursor goes to 0, the plain arrow (`FUN_004a2aa0( 0 )`) | `0x00485cc8` |
| the `camera`, `cheat` and `game` tables are switched off and their rows' latches cleared (`FUN_0040cb50`) | `0x00485ccd`, `0x00485cdb`, `0x00485ce6` |
| the gadget's arm is folded and its camcorder button lifted (`FUN_004a25f0( 1 )`) | `0x00485cf3` |
| `DAT_007c24c8` is the screen, and the focus goes to it (`FUN_004862a0`, `0x0048638e`) | `0x00485d02` |

Closing sends the screen's handler message `0x14`, which calls `FUN_00485b70( 0 )`: the global is zeroed and
`FUN_004862a0` switches the three tables back on (`0x004863af`..`0x004863c5`) and gives the layer the focus.

**The keys go to the screen**, whose handler is `FUN_00488ba0` for every one of them. A key down latches the
`shortcuts` table (`0x00488c2e`). A key up: a plain Escape closes the screen (message 4 to itself, `0x00488bdd`); a
plain F1 calls `FUN_005194d0` and `FUN_0059ab50` (`0x00488bfb`, not traced); anything else runs the `shortcuts` table
and no other (`0x00488c13`). So with a screen open no camera key turns or scrolls the view, Backspace, Delete and F3
(the `game` table's) do nothing, and a shortcut still works, B opening the buy screen over whatever was open.
`FUN_00486b70`, which switches five tables off, is not part of this: its eleven callers are the message box and the
name boxes (the object window's rename `FUN_0048d370`, the park's `FUN_004990f0`, and the like), none of them a
screen's opener.

**The pointer.** A press on the screen goes to the screen (its root takes one anywhere on its rectangle; "Whose a right
press is"). A press on the layer beside it reaches `Park_MouseMessageProc`, whose left and middle case
(`0x0048872c`) latches the camera table and then tests the screen: `CMP [0x007c24c8]` at `0x00488741` leaves the case,
so the hover is not taken (`FUN_00486d90`), the idle click `FUN_004879d0` is not run (no window opens, no path tool is
picked up) and the mode's button-down slot is not called. The hover is skipped the same way on the layer's timer and on
the pointer's entry (`0x00488569`, `0x0048884e`), so the cursor stays the arrow. **The release has no such test**
(its case, `0x004885a5`): it runs the camera table, moves the mode's preview (`FUN_0046c2a0`) and calls the mode's
button-up slot (`FUN_0046c210`, `0x00488841`). For the idle mode that is a `RET`. For a carry shell it is `FUN_00524960`, the commit, whose one read
of the flag the down slot sets (`DAT_008186d8`, `0x00524a77`) gates a sound and nothing else. So with a build tool
armed or something in the hand, a click beside an open screen still lays the run or puts the thing down. The right
button is untouched by any of it.

**In the original** (the reference park under Proton, each predicted from the listing and read from memory after the
click or the key, a frame with it; 13 of 13):

| Done | Read |
|---|---|
| the gadget's Money button | `[0x007c24c8]` nought to a control; the `camera`, `game` and `cheat` tables' `+6` 1 to 0, `shortcuts` still 1 |
| a left click on grass beside the entry-price screen, idle | the mode object and its vtable (`0x006fea10`) unchanged, tool 0; the same click with no screen had installed the path tool (`0x006fe9e0`, anchor (55,24)) |
| F3 | `[0x007cb2e8]` still 0 |
| the gadget's Info button beside the screen | `[0x007c24c8]` a different control, Park Information in the frame, the entry-price screen gone |
| Escape | `[0x007c24c8]` 0, the menu shut, the three tables back to 1 |
| the path tool armed, then Money | the screen open, the mode object and tool 1 as they were |
| a left click on grass beside the screen, four cells from the anchor | the anchor (55,24) to (55,20), the new path in the frame |
| the Left arrow over the screen | the frame as before (1.3% of its pixels differ; the same key with no screen, 51.5%) |

**Escape over a screen, with the path tool armed** (`docs/QUEUE.md` Q119; the same park, each predicted from the
listing and read from memory after the key, a frame with it; 7 of 7). The tool was armed by a click on grass (mode
vtable `0x006fe9e0`, tool `[0x0081ae2c]` 1, anchor (43,23)), and stayed so through every row but the last two:

| Done | Read |
|---|---|
| the gadget's Buy, then Escape | `[0x007c24c8]` a control, then 0; the menu `[0x007c2534]` 0; the mode, tool and anchor as they were |
| Buy, then Shift+Escape | the screen still open, the menu 0 |
| Info, its visitors link, a left click on visitor 3's row, then Shift+Escape | the visitor's window open and unchanged |
| Escape over that window | `[0x007c24c8]` 0, the menu 0, the mode, tool and anchor as they were |
| Escape with no screen | the idle mode (`0x006fea10`), tool 0, the menu 0 |
| Escape again | the menu open |
| the gadget's map button, then Escape (idle) | `[0x007c24c8]` a control while the map is up, then 0, the menu 0, the park back in the frame |

The map's own key-up case is the same test as the screens' (`FUN_005f1130`, `0x005f17ef`: key `0x1b`, `TEST
0xff0000`, message 4 to itself).

**OpenTPW** (`UiWindow.ParkScreen`): no park screen is modal. `WindowStack.Open` closes the open one before a park
screen, the game menu or the map opens (`UiWindow.ClosesParkScreen`); the gadget's Buy does nothing over an open buy
screen; `WindowStack.OnParkScreen` gives the screen's whole rectangle to the interface for both buttons and hides the
controls behind it; `Level.KeptFromThePark` keeps a left press beside a screen from the park unless a tool is armed or
the hand holds something; `Level.BuildKeys`, the orbit camera's keys and F3 are not heard while one is open
(`WindowStack.ParkScreenOpen`); the idle pointer and its help row are the plain ones; `ParkGadget.Update` folds the arm
when a screen opens. A plain Escape let go closes the screen in front, or the map, and is spent doing it; with a
modifier held it does nothing (`ParkFrontEnd.MenuKey`). Where it parts from the original:

- **A click acts on its press here**, so the armed tool's click beside a screen is taken on the press, where the
  original skips the press and commits on the release.
- **First person** leaves the screen open (`docs/QUEUE.md` Q122).
- **F1 over a screen** and **the wheel over a screen's body** are not decoded, and neither is built or counted
  (`docs/QUEUE.md` Q231). The wheel zooms the camera wherever the pointer is.

### The camera goes to a thing

Decoded for `docs/QUEUE.md` Q117, first-hand, and run in the original.

**`FUN_004867b0( thing id )`** does nothing for nought or an id with no thing (`[0x007cfb90 + id * 20]`). Otherwise it
packs the thing's cell from bytes `+5` and `+7` (`0x004867f2`, the whole part of its position), takes the column and
the row back out, multiplies each by ten and converts it to a float (`FILD`, `0x00486831`, `0x00486843`), and hands
the pair to `FUN_0042aab0` with three null pointers. So the camera is put on the **corner of the thing's cell**, in
whole tens, not on the thing's own position and not on the cell's middle.

**`FUN_0042aab0( x, z, a, b, c )`** zeroes the three terms at `0x00790a88`, `0x00790a8c` and `0x00790a90` (the camera
update `FUN_0042b1c0` reads and writes them: its scroll), writes the look-at point `0x007908f0` and `0x007908f8` from the
first two, and writes `0x007909ec`, `0x00790a38` and `0x0074c9bc` only from a pointer that is not null. From here none
is, so the spin and the zoom stay.

**Four callers.** Each of the three list screens' handlers answers the list's `0x402` (`hud.md`, "The messages"): the
list must exist and the row not be -1, the row's record id comes from `FUN_00664c71`, the thing's id word from
`FUN_0050b350`, then the call, then message 4 to the screen, which closes it whether or not the thing was found -
all staff `0x00495feb` (the call `0x0049602f`, the close `0x00496044`), visitors `0x00493483` (`0x004934c5`,
`0x004934da`), all items `0x00495554` (`0x00495584`, `0x00495599`). And the object windows' base `FUN_0048cea0` gives
its preview panel (`a`, `0x3e24` in the ride window) the handler `0x0048d1a0` and the window as its slot 6
(`0x0048cf21`, `0x0048cf31`): on a click `0x10006`, **whatever the button**, it calls `FUN_004867b0` with the id of
the thing shown (`[0x007c2658]`) and then the window's vtable `+0x2c`, `FUN_0048cf70`, which sends the window's tree
message 4. Every other message goes to the control's own proc.

**In the original** (Lost Kingdom, read from memory with a frame each): a quick right click on the visitors list's
third row took the look-at from (475.0, 175.007) to **(560.0, 280.0)**, ten times the cell (56,28) of visitor 3's
thing read 0.5 s either side, the open screen `[0x007c24c8]` to 0, and `0x007909ec`, `0x00790a38`, `0x0074c9bc` as they
were (110, 0, 1). A left click on a visitor window's preview, and a quick right click on another's, did the same: to
(420, 280) and (520, 230), each ten times that visitor's cell, the window shut. A quick right click on the strip
under the tenth row, with no row clicked since the screen opened, went to the **highlighted** row's guest
((480, 250), ten times the cell of visitor 14, the tenth row, highlighted on that opening) and shut the screen.

**OpenTPW.** `ParkOrbitCameraMode.GoToThing` writes the point of interest, the cell from
`ParkOrbitCameraMode.CellOfThing`: a person's from where they walk, a placed object's from its origin. The lists hand
their right click to `UiList.RowRightClicked` (`hud.md`, "A right click on a list") and each screen closes itself;
the object window's preview answers `Clicked` and `RightClicked`. Differences, each said at its site:

- **The scroll terms**: this camera keeps none, so nothing is zeroed.
- **The miscellaneous tab** of the all-items list holds a row for each item type with a count, not a row a thing, so
  its right click goes nowhere: counted, `ALL_ITEMS_MISC_ROW_RIGHT_CLICK`. What the original's rows there are is not
  decoded (`FUN_00495110` fills them through `FUN_00481bc0( 0x400, 0x004941e0 )`).
- **The preview's left click** is the 500 ms click, as the original's (`hud.md`, "Who acts on the click, and who on
  the release").
- **In first person** the original's two words are where the viewer stands, and no screen is open there; here only
  the debug console can put one up, and then only the orbit's point moves.
- **The status label** inside the preview (`0x3e25`) does not take the pointer here, so a click on it is the
  preview's; whether the original's label takes it is not measured.

### Leaving a park with something in the hand

Decoded for `docs/QUEUE.md` Q7; every claim was put to three refuters.

**The hand never outlives the park.** Leaving is state 0xb of `Game_StateMachine`, reached from the park menu's Exit
To Lobby (case 6, `FUN_005508b0( 2 )`) and from `Game_Shutdown` when the state is 0xa. The interaction mode goes
before the park does, by one of two routes:

- **A normal or Instant Action park is saved first** (`0x0054fef5`..`0x0054ff10`, skipped only when the game mode
  `DAT_00fb3b7c` is 1, online). The save reaches the world writer `FUN_00516c80`, which builds the idle mode and
  installs it through the setter (`0x00516d13`) before it writes anything. So the park on disk already has a moved
  thing sold and a carried candidate back in the pool, which lives in the world object.
- **Then `FUN_00409180` (`0x0054ff91`) runs `FUN_00515dd0` on the world before destroying it**, and that opens by
  installing no mode (`FUN_0046c350( 0 )` at `0x00515def`), freeing the holder `DAT_007b05e8` (`0x00515df5`) and
  zeroing it (`0x00515e02`). Online, where nothing was saved, this is what drops the hand.

The setter runs the outgoing mode's `+0x2c` OnUninstall and then its deleting destructor, so leaving is answered as
every other way out is. The carry shell's uninstall is a bare `RET`: nothing is built and nothing refunded, and a
moved thing stays sold. The place-staff mode returns an unplaced candidate (`FUN_005083b0( 1 )`). A picked-up worker's
mode (`0x0046cdc0`) puts them down on their own current cell, which is where they were picked up unless something
moved them (see "The hand's ways out", Open). The next park's entry (state 9, `FUN_0052f050`, which
reaches `FUN_0052f200( 0, 1 )`) installs the idle mode again and zeroes the tool and the rotation. The item global
`DAT_008186e0` is never reset, but every read of it is gated on tool 4 or `0x3b`.

**Every thing goes before the world, and the hiring pool goes with the world.** Read as disassembly for Q44. After
the mode, `FUN_00515dd0` walks all 128 x 128 cells (`FUN_004d8330` on world + `0x2d8`, two calls of `FUN_00536780`
each, not traced), then the used-thing list (first id at `[0x007cf56c]` + 4, next through `FUN_0050b350`). It skips
a thing already marked dead (`+3`, `0x00515e71`) and hands the rest to the per-kind destructor `FUN_0050b780`
(`0x00515e97`), which marks each dead (`0x0050b8e9`) and queues its id (`FUN_00516310`). `FUN_00518ea0`
(`0x00515efc`) then drains that queue, unlinking each thing from the used list, freeing it (`0x00518f6d`) and
zeroing its slot (`0x00518f72`). The world state goes back to 1 (`0x00515f06`; the 2 it ran under is in
`park.md`, "What selling a thing does to its script"), and `FUN_0050f240` and `FUN_00544450` follow, not traced
here. Only then does `FUN_00409180` destruct the world (`0x004091aa`, a thunk to `FUN_00507840`), free it
(`0x004091b0`) and zero `0x007cf83c` (`0x004091b8`). **The pool is the world's first member**: the world's
constructor `FUN_00515540` opens by building it (`FUN_00507800` at `0x00515560`, 32 records of `0x14` at world +
0), `FUN_00507840` destroys the same 32, and the place-staff uninstall reaches it only through the world pointer
(`0x0046c8b7`, handing `[0x007cf83c]` to `FUN_005083b0`).

**In-park loads keep the hand**, a static finding not observed. Restart Park (0xa, 0xd, 0xe and back to 0xa), the
load screen and the Alt+L quick load all load into the running world, none of them through state 0xb or a mode
teardown. Escape, the menu's usual way in, drops the hand first, so the Alt+L quick load is the one route found that
carries a hand into a reloaded park. Two other callers of `GameMenu_Open` (`0x004be537`, `0x004a2942`) were not
traced.

**OpenTPW** (`Level.ForgetPark`, part of `Level.Unload`): `ParkBuildMode.Forget` puts the build tool away, then
`ParkHand.LetGo` lets go of the hand - the item and the candidate through their `Drop`, a worker through
`ParkPeople.PutBack` - logged as `Leaving the park:`. Nothing is saved, and Restart Park reloads through the same `Unload`, so the hand leaves empty
either way. It is `ParkHand.LetGo`, so a picked-up worker is put back down in their cell as well. The rest keeps the
original's order: every entity is deleted, `ParkRides` letting go of `ParkRides.Current` as it goes, and last of
all `Level.ForgetRunningPark` lets go of `ParkState.Current` and `ParkStaffPool.Current`. The console's `parks`
reads a left park and its pool `collected` in the lobby (Q44).

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

#### The RIDE window, walked: `0x00755150`, 1536 bytes, 39 controls

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
      0x3e26 b_arup      (1662,126,1745,210) help 21   cycle PREVIOUS
      0x3e27 b_ardown    (1662,216,1745,299) help 20   cycle NEXT
      0x3e29 b_allthings (1648,583,1750,686) help 17
      id -1  b_okay      (1671,818,1754,901) help 1    close
      three sliders, flags 0x61: 0x3e30 help 5, 0x3e2d help 6, 0x3e2f help 7
        the control's own rect takes the pointer, op 3 is the TRACK, op 8 the thumb (b_scrollera)
        tracks slider_w, slider_w, slider_n ; each of the first two holds a type-9 0x3e2e
      three type-1 panels, flags 0x3, one below each slider's thumb (y 711..753): 0x3e33, 0x3e31, 0x3e32
      the bottom row, left to right:
        0x3e2b b_erase help 15 | 0x3e2c b_move help 14 | 0x3e2a b_track help 10
        0x3e34 b_queue help 9  | 0x3e37 b_rideit help 16    | 0x3e36 b_callmech help 8 (toggle)
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

The ride-window door and its ordinary closed warning are implemented in Q92; see
[ride-window-door.md](ride-window-door.md) for the callback, status colours and verification boundaries.

**The shared base's three ids.** `FUN_0048cea0( stream, handler, a, b, c )` is called here as
`(0x00755150, FUN_004af600, 0x3e24, 0x3e15, 0x3e25)`: `a` is the preview panel, kept at `this[6]` and
filled through vtable `+0x10`; `b` is the stats panel at `this[5]`, filled through vtable `+0xc`; `c`
is the label inside the preview, kept at `this[7]`.

**The sliders are BUFFERED.** `0x800` arms write `DAT_007cc244` / `DAT_007cc248` / `DAT_007cc24c` and
call `FUN_004aec30` with a mask of **1, 2 or 4**; the values reach the ride only when the window closes
or either arrow is pressed, and capacity and duration commit byte-wide where speed is a dword.

**18 of 18 mesh hashes resolve, and three of them only by NODE name** - the root is `window2` inside
`w_med.MD2`, `0x3e25` is `chev` inside `f_chev.MD2`, and **`0xaaee5929` (`0x3e37`, help 16, handler
`FUN_004e15b0(0)` then close) is `b_ride it!` inside `b_rideit.MD2`**.

**Why a scan of stems and raw bytes cannot resolve it.** No member stem hashes to it, and a token scan of
ui.wad's 1202 members' **raw bytes** can never match anything: every one of ui.wad's 278 `.md2` files is
**refpack-compressed**, so such a scan searches compressed noise. The node name also carries a **space and an exclamation mark**
(`b_ride it!`), which a token-splitting scan drops even on decompressed data. Decompress with the
tree's own `WadArchive` + `ModelFile` and read `ModelFile.Nodes[].Name` - 1,563 node names across
ui.wad and lobby.wad, and every outstanding hash falls out at once.

The buy and hire screens' root frame `0xf76e4200` resolves the same way:
it is `window4` inside `w_big.MD2`, one of a family - `window1` `w_small`, `window2` `w_med`,
`window3` `w_park`, `window4` `w_big`. See
`docs/exe/hud.md`. Resolver: a local harness that decompresses each member with `WadArchive` and reads
`ModelFile.Nodes[].Name` (its path is in `CLAUDE.local.md`); a hash of stems and raw tokens cannot see any of this.

**A trap for any re-implementation that anchors controls:** `0x3e25`'s rect (300..828) is **wider than
its parent's** (348..762), so a rule that only inherits a parent's edge when the child sits inside it
will pin this one somewhere else and draw it 160px out of place on a 1280x720 window.

### The object window's preview

**The panel shows a fresh instance of the item's `P` model, not the thing standing in the park** (Q188). The item
loader `FUN_004629d0`, asked with bit `0x20000` of its flags, loads a second model set named `"p%s"` (`0x0074d368`)
into the item record's `+0xd0` (`0x00462bd1`..`0x00462c07`) and gives it the item's own `.hmp` (`+0xcc`). The
catalogue loader passes `0xb24a9` for an ordinary item and `0x50c00`, without the bit, for one whose descriptor `+0x38`
is set (`0x0041413c`, `0x00414192`); the fork review names that field `Info.DontApplyOffset`, the fixed items', which
was not re-read, and no fixed item ships a `P` model. 39 shipped items have one
(jungle 11: `totem`, `lookout`, `mumbo`, `spider`, `tvsim`, `coaster1`, `coaster3`, `minecart`, `gokarts`, `wateride`,
`junspray`; fantasy 6, hallow 10, space 12), each with one clip in role M.

Every window with a model panel builds it the same way. The ride window's fill `FUN_004ad7f0`
hands the panel's rectangle and the item's model index (`FUN_004dd4e0`'s object, `+0x490`) to `FUN_00486410`
(`0x004ad85c`); so do the shop window (`0x004afbdc`), four more windows (`0x004970ca`, `0x0049985c`, `0x004b100c`,
`0x004b650c`) and the buy screen, four times (`FUN_004ab1b0`). `FUN_00486410` turns the rectangle into screen units,
in which the screen is two wide and two high about its middle (`(x - 0x400) / 1024`, `(0x300 - y) / 768`), frees the
instance before (`FUN_004690a0`) and calls `FUN_004689f0( left, bottom, depth, width, height, item )`, keeping its
record at `[0x007b9f4c]`. `FUN_004864f0` runs `FUN_00468e50` on it each frame; `FUN_00486510` frees it.

**`FUN_004689f0` builds the instance and its fit.**

- `FUN_00463060( item, 0xc01, ... )` makes an instance; bit `0x400` takes the record at `+0xd0` where there is one
  (`0x0046309b`), the item's own model otherwise.
- With a `P` model, `FUN_00468950` looks in each of the two models for the materials named `sign1.tga` and
  `sign2.tga`; only where all four are found does the instance's `sign1`, `sign2` take the item's own painted boards
  (`0x00468a64`..).
- The box is the `.hmp`'s six floats (`+0x18`, min then max). With `sx`, `sz` its width and depth:
  `hd = sqrt( (sx/2)^2 + (sz/2)^2 )`, `reach` = the distance from `(middle x, 0, middle z)` to the max corner,
  `diagonal` = from `(min x, 0, min z)` to the max corner. **Both start at height nought, not the box's floor.**
  `scale = min( width * 2/3 / hd, height / diagonal )` (`0x006fe7d4`), applied to the instance (`FUN_0045bf20`).
- The record at `0x007afd08` keeps: `+4` the instance, `+8` its root matrix, `+0x48` the angle, `+0x4c`..`+0x58`
  left, bottom, width, height, `+0x5c` `scale * sx/2`, `+0x60` `scale * sz/2`, `+0x64` `reach / (reach + hd)`,
  `+0x68` `scale * diagonal`, `+0x6c` `scale * reach`, `+0x70` the clock's last reading.
- It triggers role 5 (M), entry 0, looped, at speed 1.0, on channel 0 (`FUN_004732a0`, `0x00468e11`).
- The first angle is the answer of an x87 intrinsic (`FUN_0067b24a`, `0x00468e2f`) whose operands are not traced.

**`FUN_00468e50` turns and places it each frame.** It copies the kept root matrix back, turns it about y by the angle
(`FUN_0046f420`: `x' = x cos a + z sin a`, `z' = z cos a - x sin a`), tips it about x by 45 degrees
(`FUN_0046f240` with 0.7071, -0.7071: `y' = 0.7071 ( y + z' )`), and sets the translation:

```
x = left + width / 2 - 0.75 * ( hx cos a + hz sin a )
y = bottom + height / 2 - 0.35 * share * reach - 0.7 * ( hz cos a - hx sin a )
```

with `hx`, `hz`, `share`, `reach` the record's `+0x5c`, `+0x60`, `+0x64`, `+0x6c`. Then the matrix's x column is
multiplied by 0.75 (the screen's 3:4) and its z column by 0.1 (`0x006fe7fc`, `0x006fe800`). So the model turns about
the middle of its footprint for a box whose low corner is its origin, and that middle stands on the panel's middle
line, under its middle by 0.35 of `share * reach`. The angle then takes `-0.0004` a millisecond off itself
(`0x006fe804`): **0.4 of a radian a second**, on the clock `FUN_004031e0` reads.

**Measured in the original** (2026-10-05, the record read from memory with each window open): the Belly Bounce's
window `+0x4c`.. -0.66015625, 0.25, 0.404296875, 0.5390625 (the panel (348,162)..(762,576)) and `+0x5c`.. 0.1510225,
0.2013633, 0.5574498, 0.5390625, 0.3170543; a bought Aztec Mayhem's 0.1666563, 0.1666563, 0.5989949, 0.5390625,
0.3520546; the buy screen's panel -0.5732422, 0.2643229, 0.3515625, 0.47265625 with the same ride 0.1461262,
0.1461262, 0.5989949, 0.47265625, 0.3086855: each the arithmetic above on the item's `.hmp` box. The angle grew
0.3956 a second. The Belly Bounce, with no `P` model, shows built, on its grass plate, its M clip running.

**OpenTPW builds it** (`ParkObjectPreview`, `ParkPreviewFit`): a fresh `LobbyModel` of `P<stem>.MD2`, or of the
item's own model posed as built, wearing the name board where both models name both halves, its first M clip looped,
fitted and placed by the arithmetic above. Differences, each said at its site: the light is OpenTPW's own; the turn
and the clip run on the frame clock, so the console's `pause` holds them; the first angle is nought
(`OBJECT_PREVIEW_START_ANGLE`). Only the ride window exists here: the shop window's and the buy screen's panels
(Q158) take the same path when they are built. The buy screen also paints a small picture of the item's footprint
at its panel's lower left, which the windows do not: the next section.

### The buy screen's footprint picture

**It is the item's `Info.Shape` picture, a square a cell, painted by the buy screen's own preview fill and by nothing
else** (Q233). `FUN_0052c5b0`, which makes its grid, has one caller, `FUN_004ab1b0` (`0x004ab311`), and that has one,
the buy screen's handler `FUN_004ac270` (`0x004ac44f`). The ride window, the shop window and the four other windows
that call `FUN_00486410` have no such control and paint none. It is not part of the turning model: it is a flat
picture in a control of its own, so it does not turn.

**When.** The list's message `0x401` with a row hands the handler that row's item id (`FUN_00664c71`); a different id
is kept as pending at `[0x007cc1e4]` with the millisecond clock's reading at `[0x007cc1f0]` (`0x004aca16`). On the
frame message `0x1e`, once more than 500 ms have passed (`0x004ac443`), the handler calls `FUN_004ab1b0` with it and
clears it, then steps the model (`FUN_004864f0`). So the panel changes half a second after the pointer settles on a
row.

**Where.** The layout stream (`0x00754cf8`) gives the panel `0x1ea` two children: `0x1eb`, type 1, at
(440,407)-(594,561), and the name `0x1ec`. The builder `FUN_004acc70` gives `0x1eb` a painted surface for a skin
(`FUN_0048f150`, set by `FUN_0065d1c8`, `0x004acf1f`) and hands `FUN_0065f16b` the control's depth word (`+0xe0`)
plus 12 (`0x004acf2d`); the model is handed the panel's plus 10 (`0x004ab2be`). The surface is the control's rectangle in screen
pixels, `154 * screen width / 2048` by `154 * screen height / 1536`, each truncated (`FUN_0048f420`,
`[0x00faa5c4]`, `[0x00faa5c0]`): 48 by 48 at 640 x 480, 77 by 77 at 1024 x 768. Its lock answers the buffer, the
width and the height (`0x0048f650`).

**The grid.** `FUN_0052c5b0( item )` reads the parsed shape at item `+0x18` (width, depth, then eight bytes a cell in
rows of twenty) into a 16 by 16 grid of dwords at `0x00818800`, a column after a column, with the width at
`0x00818c00` and the depth at `0x00818c04`. Row 0 is the last row of the picture as the file draws it ("The
characters are the executable's own alphabet", above). A cell's kind becomes a code:

| Kind | Characters | Code | Painted |
|---|---|---|---|
| 4, `0x17` | `*`, `D`, `>`, `<` | 1 | blue, `1e aa ff` |
| 1 | `@` | 4 | blue, the same |
| 9 | `8 6 2 4 O` | 2 | green, `0f dc 32`: the entrance |
| 10 | `N E S W X` | 3 | brown, `dc 64 0f`: the exit |
| 0, 3, `0x0b`, `0x10` | `.`, `Q`, `+`, `#` | 0 | nothing |

**The paint.** `FUN_004ab1b0` clears the whole surface to nought, then for column `i` and row `j` with a code fills
`( cw * i, H - ch - ch * j - 1 )` to `( cw * ( i + 1 ), H - ch * j - 1 )` through `FUN_005f9a80`, with
`cw = W / 8` and `ch = H / 8`, alpha `0x80` (`0x004ab403`). So the block grows from the surface's lower left, row 0
at the bottom, eight cells fill it each way, and the lowest row of pixels stays clear. A code outside 1 to 4 keeps
the colour of the cell before it (`0x004ab408`); none can arise.

**Rows with no picture.** For a row id below nought (the two land rows) and for an item with a golden-ticket cost
(`+0xc4`) not yet bought with tickets (`FUN_004d4b70`), the fill clears the surface and paints nothing
(`0x004ab4c8`); the model is then item 101 for id -1, item 102 for any other id below 1 and item 100, the mystery
ride, for the rest, and the name row `0x1ec` takes string `0x86`, `0x87` or `0x89` (`FUN_00485b00`). `FUN_0052c5b0` is not called, so the
grid keeps the item before.

**Over every shipped shape** (274 blocks in the 310 archives searched): the widest is 6 and the deepest 5, so none passes the
eight cells; 1,831 `*`, 137 `2`, 44 `S`, 26 `N`, 2 `E`, 12 `>` and 12 `<` (the twelve coasters) are painted, and 65
`.` (eleven archive names) and 76 `+` (ten archive names, Lost Kingdom's `lavajump` among them, all `+`) are not. No shape uses
`@`, `D`, `Q` or `#`.

**Measured in the original** (2026-10-06, 640 x 480 off screen, each row hovered, the grid read from memory,
four predicted first from the item's `.sam`, 4 of 4, the Belly Bounce read beside them): Aztec Mayhem 4 by 4, row 0 `1 2 3 1`; Crazy Ape 4 by 4, row 0
`1 2 1 1`, row 3 `1 1 3 1`; Belly Bounce 3 by 4, row 0 `1 2 1`, row 3 `1 3 1`; Balloon Shop 3 by 3, row 0 `1 2 1`;
Staff Room 2 by 2, row 0 `1 2`; every other cell 1. Buy Land and Clear Land left the grid as it was and the picture
empty. With the ride window open and no buy screen opened since the start, the grid was all nought. In the frames,
scaled 1.6 to 1024 x 768: the Aztec Mayhem's block 38.5 wide and high (4 cells of 6 game pixels), its left edge at
220.7 and its lowest at 278.4, where the arithmetic gives 220.0 and 278.9; the Staff Room's 19.2 wide. Blue reads
(8,89,140), green (0,117,25) and brown (115,53,0) on the panel's black: about half strength.

**The picture is in front of the model, and lets it through.** In the Aztec Mayhem's frame the corner of the model's
grass plate lies under the block's upper right, and reads there as the plate's green under the blue: the picture is
drawn over the model at part strength, as the depth words say (the control's plus 12 against the panel's plus 10).

**The strength.** The nine colour channels measured over black each read 8/15 of the fill's byte, not a half:
`0xdc` 117 (a half is 110), `0xff` 140 on a 16-bit screen (136; a half is 128), `0xaa` 89, `0x64` 53, `0x32` 25. So
the alpha byte `0x80` reaches the screen as 8/15, which is what a surface with four bits of alpha would give.

**What chooses the row.** The buy list carries the list flag `0x80`, and under it the list's proc answers the
pointer's move (`0x10001`, and `0x10003`) through `FUN_006656a0`: the row under the pointer is selected with no press,
which posts `0x401` when the row changes (`hud.md`, "The pointer over a list"). So a row is shown by resting the
pointer on it, and the add's selection of the first row (`docs/QUEUE.md` Q232) shows the top row half a second after
the list is filled.

**OpenTPW** (Q233b): `ParkFootprintPicture` is control `0x1eb`, a child of the panel; its `Grid` is `FUN_0052c5b0`
and its `Squares` the paint's arithmetic, on a surface the control's rectangle in whole pixels, drawn at 8/15.
`ParkBuyScreen.RowSelected` and `Update` keep the waiting row and show it after more than 500 ms;
`UiList.SelectsUnderPointer` is the flag, set on the buy list, and `WindowStack` sends the move. It differs in two
ways, each said at its site (the wait is real time, `Time.WallMilliseconds`, as the original's is); no row is
selected as the list fills, so the panel stays empty until the pointer has been over a row (Q232); and the hire
list, which carries the flag too, does not set it. The model and the name row `0x1ec` are not in the panel (Q158).
The console's `footprint` prints the row shown, the row waiting, the cells and the block's place.

**Confirmed in Lost Kingdom** (2026-10-06, a 1280 by 720 window, the pointer moved by XTEST, each number predicted
first, 12 of 12 on the second run): the pointer on the Aztec Mayhem's row, read at once, the row waiting 174 ms and
the row before still shown; 0.9 s on, 4 by 4, row 0 `1 2 3 1`, the surface (366,190) 72 by 72, a cell 9 by 9, the
block (366,225)-(402,261); the Crazy Ape's exit in row 3; the Staff Room 2 by 2, its block (366,243)-(384,261);
Buy Land, nothing; a row crossed in 0.15 s was shown 500 ms later though the pointer had left it. In the frames
every cell's middle reads its colour at 8/15 over black, blue (16,91,136). Beside the original's three frames, on
the 2048 by 1536 layout: the original's blocks stand at (442,480)-(518,556) and (442,520)-(480,556), ours at
(439.5,480)-(516.3,556.8) and (439.5,518.4)-(477.9,556.8), a pixel of the original's 640 by 480 screen apart. The
first run's colours were not as predicted: the panel's frame was drawn in the stats panel's shape, so the picture
stood on the screen's green (`hud.md`, "A frame worn at two sizes").

Not established: what `FUN_005f9a80`'s fifth byte (nought here) does, and the surface's pixel format, which the
8/15 suggests and no listing gives; a shape with `.` or `+` cells, which no row of the reference park's buy list
has; what the list's `+0x11c`, which must be nought for the move to select, holds; the message `0x10003`.

### Every diagnostic string goes to a bare `RET`

`FUN_005da3c0` is where the build's log and assert calls all land (about 1,700 of them), and they do not share one
shape: some pass a format first (`0x0041973d`), some a number and then a format (a constant 8 at `0x00558dca`), and
some a condition and then a format (the walk node lookup's answer at `0x00556f6e`). **In the retail image its entire body is a single `RET`.** So every diagnostic
quoted anywhere in these pages - "Can't put staff here", "Dropping staff member %d", "SPEED = %d" - is a
stripped no-op that the player never sees. Re-implement them as debug logging, never as UI.

---

## Postcard

    camcorder  shortcut 16 -> thunk 0040c5c0 -> CALL 00481a10      <- button id 99 calls the same
    postcard   shortcut 15 -> thunk 0040c4c0 -> CALL 00481500
                                               00481500 = JMP 004a9380   <- button id 100 calls this

**The trap that makes this look like a mismatch: `0x00481500` is a bare `JMP rel32` thunk** (`e9 7b 7e 02 00`), undisassembled and with no function, so it *looks* like a different handler from the button's `FUN_004a9380` until the jump is resolved (`0x00481505 + 0x00027e7b = 0x004a9380`). **Resolve every thunk before concluding two routes differ** — both of these shortcuts converge on the same function as their button, which is the pattern.

`FUN_004a9380` fits a postcard screen: `UI_PlaySound(0x95)`, then `Game_Pause(0,0)` and **`g_ParkRunning = 0`**, `UI_SetVisible(0)`, `UIParticles_ButtonGlintStop()`, `Advisor_StopQuietly(1)`, and it selects a screen with `DAT_007cc190 = 1; DAT_007cc150 = 3` (`FUN_004a9350` reads that selector). The feature ships real data — `data\Postcard.wad`, `postcard.jpg`, `data\postcard\legal.tga` and an HTML template *"Theme Park World (TM) Virtual Postcard"* — so it writes a picture out. **Not in scope now; Alexah flagged it as needed later for 100% parity.**

---

## Every screen in the game, and the stream it is built from

**`UI_LoadTree( stream, handler )` builds a screen**: `UI_ParseTreeStream( stream )` then `FUN_0065e526( handler )` to install its message callback. **The first argument is a COMPILED LAYOUT STREAM in the exe, not a filename** — reading it as a string gives nothing. (`ridestatbar.wct` and friends are a different thing, loaded by `FUN_00477ff0` — widget skins, not screens.)

**54 calls, from 49 functions and two sites in the handler `0x0048a740`, build every screen in the game.** Each row is `caller -> (stream, handler)`, a blank handler a null one. Not in the table: `UI_LoadModalTree`, the object windows' base `FUN_0048cea0`, the gadget's `FUN_004a1d70`, the handler's two rebuilds of `0074fb20` (`0x0048a808`, `0x0048a8ed`), the all-items screen's four sub-builders `FUN_00494250`, `FUN_00494c70`, `FUN_00494ec0` and `FUN_00495110`, and `FUN_004b6110`:

    FUN_00480b00   0074f9e0 00480d10   FUN_00489f50   0074fa98
    FUN_0048a410   0074fa98            FUN_0048ac40   0074fb20
    FUN_0048adb0   0074fb20 0048a740   FUN_0048ae70   0074faf0
    FUN_0048d370   00750090 0048d640   FUN_0048d8b0   007501a0 0048e2f0
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
    FUN_004b3cf0   00756950, 00756ad8 004b3c10   (the staff/visitor locator, two streams)
    FUN_004b7b00   007579c8 004b7a70   FUN_004b8520   00757d88 004b86a0
    IslandPanel_Create        00757f60 004b8b70
    FUN_004b9a70   007581a0 004b9950   FUN_004bf460   00758ae8 004bd4a0, 00758cd8 / 00758e18 004bed60
    FUN_004bff70   00758f40 004bf9f0   FUN_004c0ae0   00759170 004c0880
    FUN_004c2110   007593e8 004c1a10   FUN_004c23f0   00759548 004c23c0
    FUN_004c4fb0   007596b0 004c53f0
    FrontEnd_Init             00774c18 005d58b0
    FUN_005f0b40   00774da0 005f0b00   (map)

The park management gadget is stream `0x00752940`.

**How to reproduce this table, and a trap in doing so:** walk `references.getReferencesTo(UI_LoadTree)` and read back a few instructions for the `PUSH` immediates. Matching `t.startswith("PUSH 0x0")` **silently produces an EMPTY column for every row**, because an address like `0x754cf8` prints as `PUSH 0x754cf8` with no leading zero. **An extraction that returns nothing for every row is a bug in the extractor, not an empty dataset.**

---

## The park's coaster builder bar

`FUN_00498790` builds it: `DAT_007ca1c8 = UI_LoadTree( &DAT_00751530, FUN_004982a0 )`, then `UI_SetVisible(0)` — **built hidden**, like the camcorder/postcard sub-panel beside it. **It is not the management gadget** (that is stream `0x00752940`).

**Every button on it is a `cb_*` mesh — coaster builder**, dumped from the stream: `cb_incline` (`0x19`), `cb_loft` (`0x1a`), `cb_move` (`0x1b`), `cb_rotate` (`0x1c`), `cb_swapdown` (`0x1d`), `cb_swapup` (`0x1e`), `cb_track` (`0x1f`), `cb_dellast` (`0x21`), with **`0x20` carrying no mesh at all**, and the bar is framed by `!f_plain`. It is the track-laying bar for a coaster, not a general build toolbar, and it sits at (859,916)-(1534,1190) on the virtual screen.

**It also runs keys, through the camera and coaster tables.** `FUN_004982a0` handles `0x1000a` by calling `FUN_0040c900( key, mods )` and `0x1000b` by calling `FUN_0040c990` — the binding matchers — on the camera table (`[0x00787c14]`) and then the coaster table (`[0x00787294]`, rows at `0x00748350`, `abortcoaster` among them). The park's own game and shortcuts tables run from layer 0's `Park_MouseMessageProc` (`scenes.md`, "The park Escape route"). Message `0x14` clears `DAT_007ca1c8` (the panel handle).

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

"mode" is `FUN_004a2aa0(n)`, which is just `FUN_0065f11d(n); _DAT_007cb2d8 = (short)n` — the cursor layer 0 shows (`FUN_0065f11d` stores it at the control's `+0x50`; see `hud.md`). **Message `0x1001d8` cancels the tool** and withdraws all five advisor lines with `FUN_00486b40( 0x125 / 0x127 / 0x126 / 0x121 / 0x124 )`.

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

**A row runs +Z once drawn in 3D**: the terrain lighter `FUN_0056ef10` places row r at heightfield `+0x0c` + r × `+0x14` (10.0) in the original's Z ("The lighting model").

---

## The lighting model

Decoded by the fork review (gap6-7, gap6-8, its refutation rank 4) and checked again for Q189. **The original lights
a park per vertex in software, once per mesh, and hands Direct3D a finished colour.**

**The lighter, `FUN_005741b0`.** It starts from the renderer ambient `0x00879ddc/e0/e4`, which `FUN_00458590` fills
with `ThemeEngine.AmbientLightLevel`'s R, G, B / 255 (0.333, 0.333, 0.408 in the base file and all four themes'
`Standard.sam`; `fantasy/Online_Standard.sam` alone carries 0xFFA0A0B0). For each directional light (`+0x50 & 0xf` =
2) it adds the light's colour times `-(d.n)` when that is not negative. `d` is the light's direction taken into the
model's space: `FUN_005743a0` inverts the model matrix (`FUN_00578b20`), turns `+0x2c` into `+0x38` with no
translation (`FUN_00578d20` mode 3) and normalises it. The park makes one such light, the sun (`FUN_004584d0`, the only
caller of the light constructor `FUN_00457e70`).

**The sign.** `FUN_00458590` normalises `LightNormal` with +1.0 (`0x0074d0d8`) and never negates it, so (0.4, -0.8,
0.4) is **the direction light travels**: the sun stands above, and a level face gets 0.816 of the sun's colour.

**The vertex colour.** Models go through `FUN_00574660` (from `FUN_0057aa10`), the terrain through `FUN_00574530`
(from `FUN_0056ef10`, from the terrain renderer `FUN_0056f670`); both do the same thing: each channel × 255.0
(`0x007016e4`), rounded, clamped to 0..255, alpha 0xFF, ANDed with the channel mask at `[0x0087a248]+0x2c`
(`AND EAX,[ECX+0x2c]` at `0x0057463f`). The mask is 0xFFFFFFFF in play; the frame renderer `FUN_00576ec0` narrows it
for two passes gated on render-state `0x8`. With render-state `0x800` the colour is the mask itself, fullbright.
The model lighter reads its normals from the mesh at `+0x64`, stride 12.

**The terrain's normals are not unit length.** `FUN_0056ef10` builds one per vertex,
`((h[x-1] - h[x+1]) * k, 1.0, (h[r-1] - h[r+1]) * k)`, with the one-sided difference at either end of a row and of
a column, and nothing normalises it. `k` is terrain `+0x18`, set by `FUN_0056e3f0` to 1 / (heightfield `+0x24` -
`+0x20`), the field's authored height range (FileFormats `models.md`, "Heightfield"), or 1/16 when the two are
equal. The jungle's range is -10 to 60, so `k` is 1/70 where the true slope over the two cells either side would be
1/20: **a hill is lit three and a half times flatter than it stands.** The last row takes its other height from
`pfVar8[-0x80]`, 128 floats back, where every other row steps by the field's own width (97); in the jungle that
changes 40 of the row's 97 normals. Rows are placed at `+0x0c + r × +0x14` in the original's Z, so a row runs +Z.

**What it looks like.** With `DirectionalLightLevel` (1, 1, 0.847), any face whose `-(d.n)` is at or above about
0.67 saturates to its texture's own brightness: all level ground, every top, and every face turned toward the sun
(it stands to the low-x, low-y side of the park). Shading shows only below that; a face turned away gets the ambient
alone, (85, 85, 104).

**Set once.** `FUN_004080e0`, called only from the park load at `0x0054ed3f` in state 9, copies the four values to
`0x008bd47c` (fog), `0x007a0b44` (ambient), `0x007a0b3c` (directional) and `0x007a0b30-38` (direction);
`FUN_00458590` applies them at `0x0054ed64`. The sun is constructed earlier, at `0x0054eca4`, from those globals
before they are filled. Nothing time-driven writes any of them; this does not on its own rule out a day-night look
drawn some other way, which only frames of the original a few game days apart would show.

**OpenTPW** lights a park this way in `content/shaders/test.shader` (per vertex, clamped, rounded to a byte) with
`ParkLight` and `ParkGround.NormalAt`. The lobby and anything drawn on the screen keep the shader's old per-pixel
lighting. Not checked: whether the original's mesh normals at `+0x64` are the same smoothed normals OpenTPW
computes (`models.md`, "Normals").

---

## The blocking unknowns, worst first

1. **Which grid size is authoritative**: 96x85 (mesh), 95x84 (`.sam` MapInfo), 95x85 (the engine's own derivation). **Nobody established whether `HeightfieldWidth` counts cells, vertices or cells-minus-one.**
2. ~~The lighting model.~~ Settled: see "The lighting model" above. `LightNormal` is the direction light travels.
3. **The diagonal-choice bit** — see the terrain section. The cell's `0x0800` has no consumer found anywhere, and neither terrain pass tests `0x0004`, the RE pass's candidate.
4. `base.MD2`'s container walking — the heightfield was found by validated signature search, **not** by walking the chunk table.
5. `base.map`'s per-bit semantics beyond `0x08`.
6. **The procedural compositor.** Its entry point (Q194): `FUN_004504c0` (from `FUN_00457a90` on a park's load, and
   from the lobby's `FUN_005d84a0`) passes the gate above, builds `%sbase.lnd` and calls `FUN_0056dfe0`, which (unless
   refused) calls `FUN_0056e550` on `DAT_00879d60`. That needs `DAT_008bd524 == 1` (not identified), logs "Attempting to
   initialize procedural textures", and through `FUN_005723f0` allocates the tile buffers (`FUN_0055f720`,
   `DAT_008798c8`) and reads the file (`FUN_00560ff0`, which refuses it unless byte 0 is 3), then sets
   `DAT_008bd550 |= 4`. What sets `0x2000` from `PROCEDURALTEXTURING` (0, 2, 2 in `low`, `med`, `high.sam`) is not traced.
   - `FUN_0055f780`, a 6,256-byte MMX thiscall, composites one tile (callers `FUN_00572440`, and `FUN_00572630` from
     `FUN_0057ace0`): unless all four corner samples' channel 0 are `0xff` it fills the tile grey (`0x7f7f7f00`);
     otherwise it upsamples channels 1-3 with `FUN_0055e780` into `DAT_008798a8`, `DAT_008798b8`, `DAT_00879398` and
     blends up to thirteen source textures with modulo wrap. Only its first ~180 of 2,681 decompiled lines are read.
   - `FUN_0055e780` is a bilinear **up**sample: one byte channel's 5 × 5 samples to an N × N tile, N = 4 × (w >> 2).
   - `FUN_0056e7e0` is the separate visible-cell pass: a quadtree over map cells that projects each rectangle's height
     box, culls it, and splits it, `FUN_0056e780` keeping each of 128 rows' x span. `FUN_00576a00` calls both
     branches: `FUN_0056ea90` for the spans, `FUN_00576910` → `FUN_0056f090` → `FUN_00572440` → `FUN_0055f780` for
     the tiles. No sprite passes through any of them.
7. The park camera's terrain-following sampler — probably real, identity disputed between agents.

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
the pairs `(0x40,0x50)`, `(0x10,0x14)`, `(0x01,0x41)`, `(0x04,0x05)`. *(Lost Kingdom cannot tell this
reading from its reverse, the base on an L and the bump on a straight: the two
look alike — its single corner carries direction `0x10`, which gives a base of nought either way — so
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
established**. Left unguarded, such a cell draws nothing in OpenTPW, and because
the ground leaves any tile-set-2 cell to the queue renderer, the **sky shows through the hole**. OpenTPW now
drops path links until the index is in the table (`ParkPathBuilding.Retile`, counted `QUEUE_TILE_INDEX_OUTSIDE_TABLE`).

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
  `DAT_0081d7a8` is nought** — so under force, which is exactly a demolished thing's queue drain and
  queue-over-path, **queue cells are torn down with no neighbour unlink at all**. The demolisher's
  footprint pass runs with the flag at nought, so a demolished thing's entrance IS unlinked. The two arms also step by different
  amounts: the path arm walks all eight directions, the queue arm only the four cardinals.
- **Deleting a path refunds nothing; deleting a queue cell refunds** `perCellQueueCost * pct / 100`
  credited through `FUN_004d0190`. The asymmetry is in the code, not in the evidence. **Two
  preconditions the refund carries**: a cell of mType 9 never refunds, and neither does a queue cell
  whose owner cell has mType nought. Demolition drains a queue through this same refund and then
  takes one cell's worth back — see "Demolishing a queued thing" under "Sell, move and the scrap value".
- The one hard refusal is **NOMODIFY, `mFlags & 0x20`**, which `FUN_00536490` sets on each path cell
  it rebuilds from the level's design map. The escapes are the force flag (`0x005367d9`) and a path cell with no
  neighbours, which logs *"Removing path cell with no neighbours but NOMODIFY set"* and clears its own flag.

  **The shipped save's nineteen flagged cells are exactly two sets, measured cell by cell:** the ten-cell
  avenue at x 47..48, y 17..21, which is design-map path, and the nine cells the placer laid before
  things' ends (see "What the placer builds in front of a thing") - eight paths and the Belly Bounce's
  queue node. Lost Kingdom's other sixty path cells carry no flag and may be lifted. Whether
  `FUN_00536490` rebuilds any design-map path beyond the avenue at load is not established.
- **mType 30 is outside the jump table** and is silently untouched.

**The four per-cell costs are zero in the image and come from game data** — which closes here rather
than staying open: `data/levels/Standard.sam` carries `Costs.PathCell` **20** and `Costs.QueueCell`
**75**, and jungle's `Easy_Standard.sam` overrides `MapCell`, `KartTrackCell` and `WaterTrackCell`
but **not** those two.

### A queue is a re-derivable walk, not a stored link

An object caches only `mBackOfQueue` (`+0x3a`) and a cell count (`+0x40`); `FUN_004de1f0` throws both
away and rewalks, tells the people in the queue, and opens a closed ride whose queue is connected again
(`ride-operation.md`, "Every way out of a queue"). Two cell fields make the walk possible: **`+0x0d`, a flow
direction written as the OPPOSITE of the step the run took into that cell, and only if still nought** (first writer wins), and
**`+0x10`, the owning object's packed cell**. The bond to a ride entrance is made only when a queue
cell is orthogonally adjacent to `mEntryPos` and the entrance's own flow byte points at it.

    start    first set neighbour bit of the entrance cell, in the fixed order 1, 0x10, 0x40, 4
    step     fixed probe order N, S, E, W; accept only mType 3 (never 9) whose +0x0d is the
             opposite of the direction probed            <- that fixed order breaks a tie at a fork

**Deleting a queue cell orphans the remainder, and that is correct** — there is no trimming loop
anywhere. Every peep but the one named by `obj+0x6c` is told to re-evaluate, and those past the new
end leave (`position >= count * 4`, unsigned, state not `0xe`). The eight sites and what each guest does are in
`ride-operation.md`, "Every way out of a queue". Deleting the path a queue hangs off clears the
back cell's bit toward it and retiles that cell, so the back of queue answers not connected
(`FUN_004de4a0`) and the ride is not reopened.

**Eight invalidation sites, each with the object in ECX, and two of them fire per cell.** The stamp's
(`0x00534858`) runs inside `FUN_005346d0` as soon as one cell's type is written, before the run's join and
retile passes, and `ClearCell`'s (`0x0053694b`) inside its unlink loop, before the cleared cell is reset.
The queue run's (`0x00527541`) runs once the `0x81` retile pass is done.

### There is no drag: a run of path is click-to-anchor, click-to-commit

**`DAT_0081ae2c` is the MODE**, named by the replay actions its two setters record —
`ACTION_SET_MODE` (`FUN_0052f200`) and `ACTION_SET_MODE_NR` (`FUN_0052f580`), from the dictionary
`FUN_004041d0` prints verbatim. It has exactly **three writers** in the whole image, so the list of
what writes the mode is closed.

Every action the recorder knows, as `FUN_004041d0` prints the dictionary (`Action recorder dictionary:`, strings
`0x00747254` to `0x007475a8`); every name starts `ACTION_`. Each recorder call passes the id first (`FUN_00403d00`, ECX
`0x785a08`):

| Id | Name | Id | Name | Id | Name |
|---|---|---|---|---|---|
| 0 | `SET_MODE` | 55 | `COASTER_PLACENORMAL` | 64 | `COASTER_GENERATETRACKOLD` |
| 5 | `SET_RIDE` | 56 | `COASTER_PLACESPECIAL` | 65 | `COASTER_BACKTRACK` |
| 10 | `SET_RIDE_ROTATION` | 57 | `COASTER_LOFT` | 66 | `COASTER_SETEDITOLD` |
| 15 | `LMB_DOWN` | 58 | `COASTER_ROTATE` | 67 | `COASTER_GENERATETRACK` |
| 20 | `LMB_UP` | 59 | `COASTER_WOBBLE` | 68 | `COASTER_SETEDIT` |
| 25 | `SET_MODE_NR` | 60 | `COASTER_MOVE` | 70 | `SET_RIDE_NAME_A` |
| 30 | `SET_RIDE_TRACK` | 61 | `COASTER_STACKUP` | 75 | `SET_RIDE_NAME_B` |
| 35 | `SET_RIDE_QUEUE` | 62 | `COASTER_STACKDOWN` | 80 | `SET_RIDE_NAME_A_WITH_MAPID` |
| 40 | `LMB_DOWN_MODIFIED` | 63 | `COASTER_DELETEMULTI` | 85 | `SET_RIDE_NAME_B_WITH_MAPID` |
| 45 | `LMB_UP_MODIFIED` | | | 999 | `NULL_DUMMY_PLACEHOLDER_USELESS_ACTION` |
| 50 | `SET_SELECTED_OBJ` | | | | |

The dictionary is not the whole set: `FUN_00447e10` records id 69 (`0x00447e25`), which it does not name. No call site
passes 64, 66, 70, 75 or 999 as an immediate; some ids go in registers, so that does not mean they are unused.

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
`0x81` pass answers non-zero; an answer of 0 means the run's last cell was already path or queue, and
**that puts the tool away** (`DAT_00820abc`, `0x005275a9`..`0x005275f2`). A red run is refused before
any pass runs; see "The path tool" below. The action recorder corroborates it independently: one
record per click carrying a single cell (`AR - %d (%d, %d)`), where a real drag would have to record
every intermediate cell or a start/end pair.

**Per line, one op at a time:** `0x87` clear, `<mode id>` stamp, `0x80` join neighbours, `0x82` flow
direction, `0x85`/`0x86` fix-ups, `0x83` owner, `0x81` retile — each a whole pass of `FUN_00536100` over
the line, and a refusal stops only that pass. See "A queue run, and how the tool puts itself away".

**`FUN_005346d0` is the stamp**, and its order matters: stamping a cell that is *already* that type
just bumps the re-stamp counter at `+0x20` and returns success **having charged nothing**; queue over
path **force-clears the path with no refund**; the affordability test is `cash - price >= 0` and
refuses the cell - which stops only the stamp's own pass, the preview having already turned an
unaffordable run red; the debit happens **after** the type write; and a path
stamped over a queue cell **invalidates the owning object's queue**.

**Refusals are shown as a CURSOR, not as text** (`FUN_0052f950` → `FUN_004a2aa0`): `0x14` is
cannot-afford, `8` is blocked. Off-map is `FUN_004d8300`, `0 <= x,y < 0x80`.

**You do not pick "queue" as a tool.** Mode 3 is never installed directly — it is reached from inside
the commit handler (placing or moving a queued thing), the demolish path, or mode `0x14` ("edit this
ride's queue"), which the ride window's queue button and a click on a queue cell both install. See "The
commit hands the player the queue tool" and "Editing a queue" above. What the UI also installs:
clicking a path cell or bare ground gives mode 1 — the idle world click `FUN_004879d0` constructs
`FUN_0046c580(1)` on hover categories 1 and 2, the only construction with verb 1 (`0x00487cd6`) — and
the **buy window** gives mode 4 for a real item
and **`0x39` / `0x3a` for the pseudo-item ids −1 and −2** — which are exactly the Buy Land and Clear
Land rows.

### The path tool

Decoded 2026-09-22 (Q35) and confirmed in OpenTPW's running game through the player's route. Built in
`ParkPathBuilding.PathStrip` / `RunPath` / `UndoPathRun` and `Level.ClickWorldAt`.

**There is no button and no key for it.** With nothing armed and nothing in hand, a left click on bare
ground (hover category 2) or path (category 1) installs the build shell with verb 1: the press
constructs `FUN_0046c580(1)`, whose `OnInstall` `FUN_0046d5a0` calls `FUN_0052f200(1,1)` (mode 1, no
anchor, cursor 3), and the release applies at the same cell, which anchors. **One click arms and
anchors.** Idle, the pointer already wears `c_path` over those cells, with UIHELPTEXT 441 "Left-click to
build path" over ground and 442 "Left-click to extend this path" over path. What comes before the
categories: staff under the pointer (their window), an active locator, **Shift** (the guest pick), the
gate and ticket booth, and track cells — type `0xb` and `0x10` have categories of their own, and a type
`0xc` cell whose parent is `0x19` has none at all (`0x004873b3`..`0x004873c5`), so the 429 such cells in
Lost Kingdom arm nothing. A guest on the cell does not stop it.

**The preview** (`FUN_005234d0` → `FUN_00536100(0x101, …)`, every UI tick): unanchored, one square
under the pointer, which is both the first and the last cell; anchored, the line from the anchor to the
pointer snapped to the longer axis (a tie runs along X). Off-map cells get no square. The per-cell
verdict `FUN_00535670(1,0)`, in its own order (`0x005357c7`..`0x00535d63`):

| # | Test | Answer |
|---|---|---|
| 1 | `mFlags & 0x40` — land outside the park | red, **no latch** |
| 2 | mType 4, 9 or 10 | red and latch, unless the item's `+0x54` is below 2 (rides and shops are 5) |
| 3 | an earlier latch | red |
| 4 | track type `0x19`, after the parent redirect for 12 and 17 (made inline after `FUN_004d0af0`, which fetches the cell's own record) | red, latch |
| 5 | cost: mType 0 or 3 adds `Costs.PathCell` to the run; cash below the total | red, latch, `c_cash` |
| 6 | mType 1 as the last cell | `m_end` (11) |
| 7 | mType `0x15` | red, latch |
| 8 | track corner (`FUN_0053ae00`: two cardinal links at right angles) or junction (`FUN_0053ae90`: three or more) | red, latch |
| 9 | mType 1 or 0 | blue |
| 10 | mType 3 with exactly one of its eight bits set, toward another queue cell | `m_link` (8); any other queue cell red and latch — **the owner is never asked** |
| 11 | any other mType | red, **no latch** |

NOMODIFY is never read for a path. The cursor (`FUN_0052f950`) is `c_link` if any square was `m_link`,
else `c_end` if any was `m_end`, else `c_path`; a red square overrides with `c_noplace`, a cash refusal
with `c_cash`. Two details are not reproduced: for the one preview after the pointer leaves an anchor it
was resting on, the anchor still shows `m_end` (`DAT_00818688`), and a red strip adds to the run's total
a second time through a verdict call whose answer is thrown away.

**A click** (`0x005271b7`..`0x00527655`, on release at the preview's target): any red square lays
nothing, plays `0xaf` and puts the tool away — the arming click included, so a click outside the park's
land arms and refuses at once. Otherwise sound `0x65`, then: unanchored, set the anchor, empty the
pending list, push the cell, advisor `0xb8`; anchored, count the click (advisor `0xd6` at the third,
`0x00527316`..`0x0052733d`), snap, and if the target is the anchor with a run already laid, put the tool
away; else push the target and run the passes — `0x87`, the stamp, `0x80`, `0x82`, `0x85`, `0x86`,
`0x83`, `0x81`, each over the whole line. A click on the anchor with no run yet lays that one cell,
whose one-cell walk steps +1 in y, so it flows `0x01`. What each pass does to a path:

| Op | On a path run |
|---|---|
| stamp `FUN_005346d0` | bare and queue cells cost `Costs.PathCell` (20); **path over path is free and bumps `+0x20`**; path over queue invalidates the owner's queue |
| `0x80` | the link pass; a queue neighbour already joined back is retiled with sound `0x8b` |
| `0x82` | the opposite of the step, into cells whose direction is 0: −x `0x04`, +x `0x40`, −y `0x10`, +y `0x01` |
| `0x85`, `0x86` | track types 11, 13, 16, 18 only — none in Lost Kingdom |
| `0x83` | owner := the low word of `DAT_00818698`, the selected thing's cell; nought for a plain grass click is inferred |
| `0x81` | retile |

**Leaving the tool**, every exit through `FUN_0052f200(0,…)`: a run that ends on path or queue; a
click on the anchor; a red click; a quick right click with RMB cancel on; **Escape** — game-table row 0,
handler `0x0040c180`, which closes a locator first and otherwise, with a type-3 tool armed, calls
`FUN_0052f200(0,1)` (`0x0040c368`) and returns 1, so the menu does not open; and **Delete** — row 3,
`FUN_0040c5e0`, which swaps whatever is armed for Clear Land (`0x3a`).

**Backspace** is game-table row 5 (key `0x08`), and the coaster table's row 1 `backtrack`, both handler
`0x0040bda0`, fired on key-up. With a type-3 tool armed it calls `FUN_0052fe50(0,1)`: pop the top of the pending
list `0x0081b740` (count `DAT_00820a8c`) as T, take the new top as P, arm `0x34` and apply at T then P — op `0x32`
then `0x86` over the line T..P — then anchor at P and re-arm the mode it found (`FUN_0052f580( mode, 1 )`,
`0x0052ffcd`; for the queue tool, advisor `0xcb` and cursor 4), skipping the apply at P in mode 3 when P is a path
(`0x0052ff9a`). The tool stays armed; no sound; one run per press, back to the first click, whose cell is never
popped but is the far end of the last press's run. Idle with an empty hand and the pointer on a path
(`0x0040bdde`..`0x0040be9c`): sound `0x5f`, `FUN_0052f200(0x32,1)`, one apply at the cell with step (0,+1),
`FUN_0052f200(0,1)`. The game's tutorial (sample 464) mentions only the armed branch.

**The clear's path arm, `FUN_005367a0`** (`0x005367b1`..`0x0053682c`): a NOMODIFY cell with links returns untouched
unless the force flag is up (`0x005367d9`); one without gives up the flag; then the counter at `+0x20` is set to −1
for a step of (0,0) or decremented for any other, and **the cell is removed only once it is below nought**, or at once under
force (`0x00536825`). Nothing is refunded. So Backspace takes up exactly what a run laid fresh and leaves what it crossed.

**`+0x20` is `mOverlapCounter`.** The serialiser `FUN_004d0b30` pairs the string at `0x0075a054` with
`LEA ECX,[ESI+0x20]`; in the save it is record offset `+8` (the FileFormats `saves.md` on its `docs/item-footprints` branch, not yet
merged). The shipped
Lost Kingdom has 14 non-zero path cells, all corners and junctions — eleven at 1, (47,21), (48,21) and
(48,28) at 2 — and the queue node (52,22) at 1.

**`mFlags & 0x40` marks land outside the park** — the meaning is inferred from a census, the writer is
not found (Buy Land, `0x39`, is the likely clearer). 13,878 of Lost Kingdom's 16,384 cells carry it, all
of mType 7, 0, 2 or 30; every path, queue, footprint and entrance is among the 2,506 without it. **Every track record's `mNeighbours` is 0**, so the corner
test never fires on the shipped park.

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

**`FUN_0052fe50` is called by the demolisher `FUN_00527ee0` and by the Backspace handler `0x0040bda0`** — which is
undefined bytes in Ghidra, so a cross-reference search misses it. Each call clears a whole straight run: it pops the
top element, anchors there in the delete-line mode `0x34`, and runs op `0x32` along the line to the next element.
The bottom element is never popped, but is cleared as the far end of the last run - unless the mode was 3 and it is
a path (`0x0052ff9a`) - and the count is left at **1** when the list empties. The line runs along its longer axis
from the popped element, a tie along Y (`0x00536140`), holding that element's other coordinate (`FUN_00536100`).
Every call re-arms the mode it found (`FUN_0052f580( mode, 1 )`), the one that empties the list too (`0x005300a6`),
and mode 3's arm posts advisor `0xcb`. What the drain's pops do to a
queue's people is `ride-operation.md`, "The sale's drain". There is also an **off-by-one in the shipped guard**: the
push rejects only when the count exceeds `0x400`, so element `0x400` is writable and lands exactly on
`DAT_0081d740`, the map-width global. Reproduce the behaviour, not the overrun.

### Still open

- ~~Which `ItemDescription` fields drive the object flag word at `obj+0x32`.~~ **CLOSED 2026-09-23:** the
  constructor `FUN_004db090` builds it from descriptor fields named by the key table at `0x00744b6c`:
  `0x01` ProvidesRelief (`+0xf4`), `0x02` ChillsYouOut (`+0xfc`), `0x04` IsChoosable (`+0x3c`), `0x08`
  HasQueue (`+0x40`), `0x10` ProvidesSecurity (`+0xf0`), `0x20` RideHandlesSprite (`+0x104`), `0x40`
  HoldsLitter (`+0xf8`), `0x80` IsFireworks (`+0x110`); and `+0x33` bit 0 is RunsContinuously (`+0x48`).
- What the low nibble of `mFlags` means — it gates the queue `+3` bump and only `0x20` = NOMODIFY is
  established.
- ~~Whether mType 9 and 10 really are entrance and exit.~~ **CLOSED 2026-09-22: they are.**
  `FUN_00413410` walks the item's shape grid at descriptor `+0x18` testing each cell against the
  literals **9** and **10**, and stores the matching cell's column and row as the entrance and the exit
  — see "Where a built thing's entry and exit cells come from" above. No debug string names them and
  none is needed: the constructor tests the numbers outright.
- mType 2 and mType 5 are unidentified; the decode refused to guess water or rock.
- ~~Whether tool `0x32` is ever armed as a live tool.~~ **CLOSED 2026-09-22:** Backspace's idle branch
  arms it for one apply (`FUN_0052f200(0x32,1)` at `0x0040be7d`). See "The path tool".

## The Ghidra project was changed to get here

A later session will find these already defined in the shared Ghidra project (which auto-backups) — that is why, not a stale note:

| Address | What was done |
|---|---|
| `0x004816e0` | `buy` stub — disassembled from undefined bytes |
| `0x00481790` | `staffloc` stub — disassembled from undefined bytes |
| `0x004817c0` | `peeploc` stub — disassembled from undefined bytes |
| `FUN_004b4280` | Created as a function (483 bytes) |
| `FUN_0067b0c0` | Disassembled from undefined bytes and created as a function |
