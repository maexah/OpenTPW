# The park engine, from the executable

What `testme.exe` says about park loading, terrain, the camera, the clock, the save container and the park's interaction modes. The body of it comes from a six-agent Ghidra pass over `/testme.exe` on 2026-09-13 (7/7 agents, 0 errors, ~1.42M tokens, 627 tool calls) plus an adversarial cross-check agent, extended by targeted traces from 2026-09-14 to 2026-09-25; most later sections name their own date or `docs/QUEUE.md` item. Confidence labels are the agents' own with the cross-check's corrections applied; where something is unverified or refuted, it says so, and **that is part of the fact**. Read this beside the park data-layout page (`docs/exe/park.md`), which holds what was measured off the game's own files — this page holds what the executable says.

---

## base.lnd is not the heightfield

`base.lnd` is **procedural-texture source data**, and it is **optional**.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `DAT_007a1a8c & 0x2000` | — | Gate on loading `base.lnd` at all (`FUN_004504c0`, `0x004507a7`); with the flag off the call is skipped and nothing is logged. `"Procedural textures not allowed"` is `FUN_0056dfe0`'s, when `DAT_008bd550 & 0x10000000` is set, and logged only under the debug bit `DAT_008bd508 & 0x2000000` ("The procedural ground") | Ghidra, Q194 |

It is the source of the ground's composited tiles: what the game makes of it is "The procedural ground", its bytes
FileFormats `lnd.md`.

---

## The heightfield lives inside base.MD2

A 0x30-byte block header, located by the MD2 header's pointer at `0x6C` (FileFormats `models.md`, "Heightfield"; the engine reads `[[0x007a0854] + 0x6c]`). Of the 2,129 `.md2` files exactly five set it: the four parks' `base.MD2` and the lobby's `terrain\Base.MD2`, and in each the pointer names the block the invariants below find (jungle `0x144e30`, fantasy `0x115840`, hallow `0x1129c8`, space `0x11a3b0`, lobby `0x1a348`), with no other 4-aligned offset passing them (upstream look 2026-10-19, `verify-terrain.md` #3). `HeightfieldFile` still finds it by the invariants; it does not read the pointer.

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
| `FUN_00467030` | — | The model placement (`ride-operation.md`). Reads the header's `+0x08`/`+0x0c` and cell size `+0x10`/`+0x14` to turn a cell into a world position, cell × size + `+0x08`/`+0x0c` (`0x00467104`..`0x00467113`); it reads neither cell count | Decompiled |
| `DAT_0079fc54` | — | The loader's private copy of the heightfield block | `FUN_00450950` |

### The cell record

**The cell record is `{ u16 flags; u16 textureIndex }`** — NOT four independent bytes. The cross-check flags the reading "byte[0] and byte[2] are small indices, byte[1] is a runtime flags byte" as a **misread** that will make anyone treat mirror/rotation bits as a texture id.

| Flag bit | What it is | Confidence |
|---|---|---|
| `0x0001` | **Hides the cell from the terrain pass.** `FUN_0056f670` skips any cell carrying it (`TEST byte [EAX],0x1` at `0x0056f9ce`). Set in `base.MD2` on exactly the 1,159 jungle-grid cells of mType 2, 30 and 7, and at run time on every cell the tile rule answers (0,8) — footprint, entrance, exit and queue cells (`FUN_0046df50`, `OR 0x101` at `0x0046e012`). The pass rebuilds the ground every frame (called from `Render_DrawView`), so the bit takes effect at once: written on four grass cells of the running original it opened a black hole two cells square on the next frame (Q360, `textures.md`, "What a floor's clear border shows"). A second, optional pass (`FUN_00570d90`, gated on `DAT_008bcbc8 & 0x10000`, off in the park) has no such test | Confirmed 2026-09-22; in the running original, Q360 |
| `0x0002` | **The drawn diagonal.** Set: the terrain pass splits the cell from (x,z) to (x+1,z+1); clear: from (x+1,z) to (x,z+1). Preserved by the footprint stamper | Confirmed 2026-10-16 (Ghidra) - see below |
| `0x0004` | **The height diagonal**, the same split read the other way: clear is (x,z)-(x+1,z+1), set is (x+1,z)-(x,z+1). Every ground cell carries exactly one of `0x0002` and `0x0004`. Preserved by the footprint stamper | Confirmed 2026-10-16 (Ghidra) - see below |
| `0x0008` / `0x0010` / `0x0020` | Rotation, one field of exactly four states (`flags & 0x38`) | Confirmed |
| `0x0040` | Mirror, independent of rotation | Confirmed |
| `0x0080` | Marks a path cell; in a footprint entry it means "this cell of the footprint is used" | Confirmed |
| `0x0100` | **The composited tile is stale**: on the cell's next view `FUN_0056f090` composites it again even from the cache, and clears the bit ("The procedural ground"). Set with `0x0001` by `FUN_0046df50`; every cell caught on its first composite carried it (Q483) | Ghidra; live, Q483 |
| `0x0200` | Set by the footprint stamper | Confirmed |
| `0x0400` | **Land outside the park, drawn at half light**: the terrain pass halves the corner's colour, and the terrain model's meshes over it take normals 0.225 long. `Terrain_MarkLandOutsidePark` (`0x00450d10`) sets it where the map cell's `mFlags` has `0x40` and not `0x80` ("The park view's colours") | Confirmed at three separate sites; measured (Q479) |
| `0x0800` | "This cell is not planar." **Computed at load, never stored on disk**: `FUN_004504c0` sets it (`0x0045075d`) where the cell's two triangles' faces differ (the dot of their cross products under 0.9999; the products are not made unit, see "Which height each asker takes") | Confirmed; the Lost Kingdom set read cell for cell in the original's live heightfield (Q820) |

### The triangle diagonal

A cell's two triangles meet on one diagonal, and the file chooses it per cell: every ground cell (`0x0001` clear)
carries exactly one of `0x0002` and `0x0004`, in all five heightfields (FileFormats `models.md`, "A cell's
diagonal"). The drawing and the heights read the two bits and take the same split.

- **Drawing.** The terrain pass `FUN_0056f670` orders each cell's four corners by `0x0002` (`TEST AL,0x2` at
  `0x0056fac1`, `TEST CL,0x2` at `0x0057033c`) and fans the quad from its first corner (the index writer at
  `0x00570c19`), so `0x0002` set draws (x,z)-(x+1,z+1) and clear draws (x+1,z)-(x,z+1).
- **Heights.** `FUN_00452560` (the camera's height) and `FUN_00469a80` (a model draped on the ground) test `0x0004`
  on every cell (`0x00452719`, `0x0046a34c`): clear splits (x,z)-(x+1,z+1), set (x+1,z)-(x,z+1). The general height
  under a point, `FUN_004527f0` (six callers), takes the plane through the near corner and its two neighbours unless
  the cell carries `0x0800`, and only then splits by `0x0004` (`0x004528f8`, `0x00452901`). In Lost Kingdom 589 cells
  get `0x0800` at load, 158 of them ground cells, 73 of those carrying `0x0004`.

Read 2026-10-16 from a lead in another fork (`meneerkrabs/OpenTPW`), checked in Ghidra and over the five heightfields.

**Built in OpenTPW** (Q821, 2026-10-29). `ParkGround` and `ParkPaths` cut each cell by `0x0002` (`PutCellElements`), and `HeightfieldFile.CameraHeight` is
`FUN_00452560` on every cell (the same planes as `ScapeHeight`'s split arm and `ProceduralConform.DrapeHeight`; ours holds the edge's height off the map where the
original answers nought, and subtracts the grid's origin, which is nought in every park). Lost Kingdom's file, as the original's live memory holds it
(`groundcell.py READ` over all 8,160 cells at 75 s into a load of Alexah's Full Simulation New Save, `q821/orig/poke7/cells75.txt`): of the 7,001 ground cells,
4,293 carry `0x2` and 2,708 carry `0x4`, and the live word's `0x2`/`0x4` equals the file's in every one (the load adds `0x0800` on the cells that are not planar only, 142 of the 6,447 live ground cells there, 158 in the stock park, and the tile's `0x0100` on cells
awaiting a composite, not on all (cell (4,13) reads `0x0004`); it clears the mirror and turn of the cells the compositor covers, and sets `0x0400` outside the park:
`0x0804`, `0x0d02`, `0x0904` for file words `0x44`, `0x42`, `0x44`). A cell
only bends by at most 2 world units there (the corner spread; its twist h00 + h11 - h10 - h01 is at most 1.01, so the two cuts differ by at most half the
twist, 0.505 at the centre; the ground's heights are whole units, apart from a few cells of a ride's floor); the other cut shows on screen as a different warp of the tile across the cell, over the `0x4` cells that are not planar (73 of the file's 2,708; a planar cell draws the same either way) (ours,
`cut old` against `cut new`, 60,864 and 79,293 pixels over two staircases of four `0x44` cells, 0 where no cell differs).

**The original's own picture of a flipped diagonal** (Q837, 2026-10-30, `tools/harness/original/flipdiag.sh`, three launches of the original off-screen, the stock park by `loadfile.sh`,
`q837/run1`, `run4`, `run5`). Camera set at point (50, 150), zoom 70, rotation 0 with `setcam.py ... off` (read back 0.000 units off the formula) and the clock held with
`memhold.py 0x007859a0=1` (tick 882 through the run): two frames 2 s apart differ in **0** pixels over the whole 1,024 x 768 frame (the run-14 poke's drift
was a running clock and a camera that was not held; the mouse arrow changes by itself, so its box is left out). The stock park's cells (3,13), (4,14), (5,15) and (6,16) read `0x0804` in the live heightfield and
each stands on three corners of one height and one a unit lower (`cut X Y`, `run3.out`); `groundcell.py` clearing `0x4` and setting `0x2` on those four cells and holding it changes **7,327** pixels (more than 8 levels in a channel), in four separate
patches, one on each of the four cells' projected squares (1,581, 1,856, 1,213 and 2,677 pixels, `run5/diff_A1_B1.png`) and nowhere else in the frame; two poked frames differ in 0, and writing `0x4` back and clearing `0x2`
returns the first frame in 0 pixels, so the draw reads the cell's diagonal flag every frame and the poke wrote nothing else. The same count and the same four patches in all three launches. What changes in a patch is the tile's warp across the cell (mean brightness +0.04 levels over the changed pixels; the
content at the cell's centre moves 2 to 3 pixels down, flat minima): the cut does not show as a crease. Ours at the same camera (`lib/bothcam.py`, `cut new` against `cut old`, desktop, `q837/run2`) changes the whole staircase of `0x0804`
cells in view, eight patches ((3,13) to (9,19); the original's frame holds the first four) and no flat cell, the control pair (`cut new` twice) differing in 3,311 pixels, all in the top 150 rows (the park's sprites), none over a patch the original shows; in the original's four boxes the two games' changed pixels, each dilated by 5, overlap 0.80 of the smaller set
(21,539 of 26,816; IoU 0.53: ours changes 1.5 to 2.6 times the pixels a cell, its tiles are sharper). **Not seen:** which diagonal each flag state draws: the brightness sign and the centre's shift
did not decide it (both near 0), and a path cell's texture index written onto the four cells (`t@`) did not reach the frame at all (322 pixels, the arrow; why is not found, the ground's tiles are composited, `textures.md`), so `0x2` drawing `(x,y)-(x+1,y+1)` stands on the decode above and
`PutCellElements`, not on a picture of either state.

### Which height each asker takes

Q822 (Ghidra, and the stored disassembly `lib/testme.objdump`). The landscape's height under a point has three readers in
the original, and every caller of the first two is known (`FUN_004527f0`, six calls; `FUN_00452560`, two):

| Who asks | Where | What it answers | OpenTPW |
|---|---|---|---|
| The park camera (orbit) | `FUN_0042b1c0`, `0x0042c817` | `FUN_00452560( x, z, 1 )`: the cell's triangle by `0x4`, every cell | `CameraHeight` (Q821) |
| First person (the camcorder) | `FUN_0042b1c0`, `0x0042c729` | `FUN_00452560( stand x, z, 1 )` less `[0x006fde10]` (-5), stored straight as the eye's height `0x00790aa4` each frame: no easing; the update then adds the stand's y (`[0x00790abc]`, copied from `[0x007908f4]`) at `0x0042c9b8`..`0x0042c9e2`, nought: entering first person sets the stand's y `0x007908f4` to nought (`Camera_ToggleFirstPerson`, `0x0042af76`; leaving puts the saved one back, `0x0042afa9`) and the sweep writes back nought (`0x0042c5cd` stores the local vector's y, which every pass zeroes at `0x0042bee4`), as does the camera block's loader's read of the save (`0x0042cf9e`..`0x0042cfa7`, the file's word), so the term is nought in first person by code (Q825; 1,440 polls read exactly sampler + 5, Q822; `[0x00790abc]` and `[0x007908f4]` read 0 in 160 polls of two parks, orbit camera) | `CameraHeight` + 5, taken each frame (`ParkCamcorderCameraMode.GroundUnderStand`; Q820) |
| Every sprite: walkers, staff, balloons (a held balloon is a sprite of that list: `FUN_004fa030` -> `0x004fe900` -> `0x00475480` writes its `+0x88`/`+0x8c`/`+0x90` in the table `FUN_00475430` walks; its `+0xc4` bit `0x1000000`: below) | `FUN_00542010`, `0x005420ee`, for each sprite of the list (`FUN_00475430`) | `FUN_004527f0( +0x88, +0x90 )` plus the sprite's `+0x8c`; a sprite whose word `+0xc4` carries `0x1000000` stands at `+0x8c` itself (`+0xe8` takes `+0x8c`, `+0xe4`, `+0xec` the place, `0x005420af`..`0x005420e0`; the other arm adds `+0x8c` to the sampler, `0x005420f3`) | `ScapeHeight` (`ParkGuestSprites`: the walk's draw, `Feet`, `DrawBalloon`; Q820) |
| A person put on a sprite | `Person_PlaceSprite`, `0x004f9e8c` | stores the height less `FUN_004527f0` under it in `+0x8c`, so the draw above gives the height back exactly | none: ours reads the saved `+0x8c` and draws it over `ScapeHeight`, so a saved person stands where the original put them |
| A person's particles | `FUN_004fa470`, `0x004fa4dc` | the sprite's place (`FUN_004754e0`) moved in x and z by `FUN_00519410`/`FUN_00519420` (absolute reads of `0x00785584`, `0x00785588`) × `[0x00700698]` (10.0), the height `FUN_004527f0` at the moved place, NOT the sprite's `+0x8c`; all three then × `[0x007006ec]` (1024.0) and truncated for `Particles_Spawn`. Both offsets read 0 (below) | none: ours spawns none of the callers' puffs (`LEAVER_BOARDING_PARTICLE`, `STAFF_DISMISSAL_PARTICLE`; the other three callers' are not counted under a name found), so there is no place to compare (Q900) |
| The build markers' wave | `FUN_0053ddd0`, `0x0053ddea` | `FUN_004527f0` at the corner it is handed, after the glide | `HeightAt` on a corner, `ScapeHeight` gliding (`ParkBuildMarkers.Corner`; Q820; read in the original's own mesh, Q838, below) |
| The scene's height, mode 1; placement feedback | `FUN_004511a0`, `0x004511e6`; `FUN_00452ae0`, `0x00452c89` | `FUN_004527f0` (`park.md`, "The scene's height under a point") | `ScapeHeight` (`ParkSceneHeight.Under`) |
| A person's shadow | `SpriteShadow_BuildQuad` (`0x00589410`) | the near corner's plane, its own sampler, no flag read ("First person's shadow", below) | `CornerPlaneAt` |
| The pointer's pick | `FUN_0045bf90`'s terrain test | its own: two planes a cell by `0x2` (below) | `ParkPickGround.Meet`, the same planes (Q824) |

**`FUN_00452560( x, z, flag )`.** Nought off the map (`x` or `z` below nought, at or past the cells × 10, or the grid
fraction past the cells); the cell's triangle by `0x4` (above). A third argument of nought answers `FLT_MAX`
(`[0x006fe400]`) on a hole (`0x1`); both callers pass 1, so that arm is dead by CODE. First person is
`gui_CameraFlags & 0x16` set and `& 0x3c` clear (`0x0042c6c4`, `0x0042c6f8`); with `& 0x3c` set (a ride's view) the
update skips the sample (`0x0042c96d`). **Measured in the original** (`q822/orig/run.sh`, `original/eyeprobe.py`; the stock park in
first person by Q496's way in, flags `0x202`): the stand held at (0.6, 0.3) inside cell (71,54), word `0x0d42`, corners 19,
19, 20.006, 19, gives the eye 24.0000, the triangle by `0x4` plus 5 to the last digit, where bilinear would give 24.1207
and the near corner's plane 24.3018; moved to cell (78,72), word `0x0d44`, the eye goes from 24.0000 to 17.1000 between
two polls 2 ms apart, with no value between (`a-eyeprobe.out`, frame `a-A5-after-probe.png`).

**`FUN_004527f0( x, z )`** splits only a cell the load flagged `0x800` (above), and takes the near corner's plane on the
rest. `FUN_004504c0` takes, for the two triangles across `(x+1,z)`-`(x,z+1)` (edges of 10 across, `[0x006fe384]`), each
triangle's two edges made unit vectors (`0x43a9a0`, and the same inline sqrt and divide for the first), their cross products
(`0x004506c0`, `0x00450725`) **not** made unit, and flags the cell where the dot of the two products (`0x43a970`) is under 0.9999
(`[0x006fe39c]`, `0x0045073c`..`0x0045075d`). (Q822 first read the products as unit normals and got 566 for Lost Kingdom;
the 23 it lacked were all holes. Q820 took the products as the disassembly has them: 589, and the original's own live
heightfield, the stock park loaded, `q820-a14/orig/cells-stock.txt`, flags those 589 cells and no other, cell for cell.) So on a flat
cell all four corners' planes agree and `FUN_004527f0` answers what `FUN_00452560` does, but **a cell bent under that tolerance
is not flagged**. Measured over the four parks' `base.MD2` (`dotnet/hfsamplers`, `q822/hfsamplers-run2.txt`, a 10 × 10
grid of points in every cell): in Lost Kingdom 589 cells flag (158 ground, 73 of them with `0x4`; the rest are holes; `q820-a14/hfsamplers-run3.txt`: fantasy 273, hallow 365, space 354), and 14 ground cells bend by 0.001 to 0.063 yet do not flag (the 450 others of the 464 that bend at all bend by float rounding, under 1e-5); on the
ground, `FUN_00452560` (ours `CameraHeight`) differs from `FUN_004527f0` on 14 cells by at most 0.06 (fantasy 36, 0.011;
hallow 5, 0.06; space 26, 0.006). Bilinear (ours `HeightAtWorld`) differs from it on 172 ground cells by up to 0.249
(fantasy 196, 0.249; hallow 173, 0.248; space 198, 0.495). **Built (Q820 piece 4):** `HeightfieldFile` sets `0x800` at load by that rule, so `ScapeHeight` splits those cells
(before, it took the near corner's plane on them, by up to 0.5 on the ground, 24.96 on a hole); sprites, balloons, `Feet` and the
build markers' glide stand on it; the camcorder's eye is `CameraHeight` + 5 each frame, not eased. The pick takes its own planes (below; Q824).

**The build markers' corners, read in the original's own mesh** (Q838, 2026-10-30, `tools/harness/original/markercorners.py` and `lib/markerheights.py`, `q838/orig/run1`, `ours5`).
A carried thing's squares are the records `FUN_0053c8d0` appends (x, the height 1.5 for all fourteen of a Belly Bounce's, z; `squares.py`); `FUN_0053df30` shifts a square's x and z by
the glide words `[0x00820afc]`, `[0x00820af8]` less the pointer cell's corner (when the mode `FUN_0052f840` is 4, `0xe` or `0x13`; the words ease toward the corner by `[0x00700de4]`
a frame, `FUN_0053c3f0`, `0x0053c61b`) and hands each corner to `FUN_0053ddd0`, which stores into the mesh's arrays (x, y, z in runs of four, 0x30 bytes a quad at `[mesh + 0x64]`,
the corner count `[mesh + 0x9c]`, the meshes `[0x00871f64]`, `[0x00871ee4]`, `[0x00871ed8]`) a height of **wave + `FUN_004527f0`( x, z ) + the record's height**, the wave being the table
`0x007b0968` at `mask & round( ( [0x00874fc0] + z + x ) * [0x007b095c] )` (a sine of 4,096 entries). Measured: the original paused (`memhold.py 0x007859a0:I=1`; the phase stood at 381.61407 on every read),
the glide words held at nine offsets over the pointer cell (63,32) (0,0), (3,4), (7,2), (1,9), (5,5), (2,2), (8,8), (0,5), (5,0), 248 distinct corner points read, the sampler taken as y - wave - 1.5. In
the four cells of the ridge the points met, (62,33), (63,32), (64,31) and (65,30), all flagged `0x800`, 41 points: the original's sampler is **ours `ScapeHeight` at all 41** (worst difference 0.0000) and `CameraHeight`
at all 41 (the two agree on these cells; they part only on the 14 unflagged bent ground cells, by up to 0.06, none in view), bilinear at 13 (up to 0.25 off), and the near corner's plane, the rule before Q823, at 23 (up to
0.50 off): at (633, 324), inside (63,32) (`0x0842`, `0x4` clear), the original's corner is 1.5 + (-2.0), where the near corner's plane gives -2.3 and bilinear -2.18. The other 207 points,
in unflagged cells, agree with all three to 0.0014, one step of the wave table (four of them read it a step off because their index sits within 0.02 of a half; `PREDICTION.txt` P5). The frame:
with the glide held at (5, 5) the squares are drawn five units down and across from their cells (`q838/orig/run1/frame-B-glide5_5.png` against `frame-A-rest.png`, 39,348 pixels apart). Not seen: a person or a
balloon at a flagged cell (the sprites' sampler is read in Q839), the corners of a square over a cell with a lift, and a flagged cell on any other theme.
A second launch (`markerup.sh`, the pointer on (65,30), phase 177.20161, nine offsets, `q838/orig/run2`, `ours6`) met the flagged cells (64,31), (65,30), (66,29) and (67,28): 40 points, ours `ScapeHeight` and `CameraHeight`
equal to the original's at all 40, bilinear at 4 (up to 0.25 off), the near corner's plane at 14 (up to 0.50 off); its 193 other points agree to 0.0013.

**The sprites' drawn height at a flagged cell** (Q839, 2026-10-30, `original/spritedrawn.py`, `spritehold.py`, `lib/spriteheights.py`, `q839/`). `FUN_00542010` stores the place it drew each sprite at **in the sprite**: x at
`+0xe4` (`0x005420d4`, `0x005422a8`), **y at `+0xe8`** (`fst` at `0x005420ca` for the `0x1000000` arm, `fstp [eax+0x18]` at `0x005422a5` with `eax = esi + 0xd0` for the other) and z at `+0xec` (`0x005420da`, `0x005422af`), so a poll of the
sprite table (`0x007b49f0`) reads what the draw asked without a breakpoint; the unflagged arm's y is `FUN_004527f0( +0x88, +0x90 )` plus `+0x8c` (`0x005420ee`, `0x005420f3`), so **y - `+0x8c` is the sampler**. In the stock
park every walker is on flat ground (1,959 of 2,034 distinct places polled in 20 s read 0.000000, the rest under 0.00001; `q839/orig/drawn-test.txt`) and 400 hits of a `brk.sh` at `0x005420f3` (st0, the sampler) all read 0
with ours' `stands` 0 there, so no guest walks a flagged cell and a poll proves nothing about one. `spritehold.py` writes one guest's `+0x88` and `+0x90` over and over at each of 279 points (a 0.15, 0.5, 0.85 grid in
each of the 31 cells the load flags `0x800` in the stock park: (28,24), (28,33)..(28,37), the diagonal (29,25)..(36,32), (59,35)..(60,37) and the diagonal (61,34)..(70,25)) until the draw's `+0xe4` and `+0xec` are the same, then reads `+0xe8`: **279 of 279 caught in each of two fresh launches
(the points in the two orders; identical to 0.0000), and the sampler is ours `ScapeHeight` at all 279 (worst 0.0001)**, and `CameraHeight` at all 279 (they part only on the 14 unflagged bent cells), the
bilinear at 0 (worst 1.31 off) and the near corner's plane, the rule before Q823, at 134 (worst 2.63 off): at the centre of (59,35), (595, 355), the original draws 2.626502 where the plane gives 0.0 and the bilinear 1.3133.
Held there for 8 s (`--stay 595,355,8`) the draw stood at that place at 890,172 of 891,076 reads, and the frame shows a guest on the raised mound that the frame without it does not (`q839/orig/frame-B-held.png`,
`frame-A-rest.png`: 719 pixels in a 20 x 60 box); ours' ground at the same camera is `q839/ours4/ours-rest.png` (no person: nothing puts one on that cell). Ours stands a person on `ScapeHeight`
(`ParkGuestSprites.StandsOn`, tested in `ParkTerrainTests`). Not seen: a balloon (the same pass, not held), ours' own person drawn on a flagged cell in a frame, the pixel row of the feet against the projected ground point, a
flagged cell on any other theme, and the `0x1000000` arm (the head sprites stand at `+0x8c` itself, Q825).

**The pick's terrain** (`FUN_0045bf90`). Over every cell the ray's line crosses and whose corners' height range it
meets, it intersects the planes of the cell's two triangles cut by **`0x2`** (`0x0045c6b7`): set, the triangles
(x+1,z)-(x,z)-(x+1,z+1) and (x,z+1)-(x+1,z+1)-(x,z), split on (x,z)-(x+1,z+1); clear, the near corner's and the far
corner's, split on (x+1,z)-(x,z+1). Each plane's hit counts **anywhere inside the cell's square** (`0x0045c88e`..
`0x0045c8db`: four bounds, no test of which triangle), and the nearest `|t|` is kept (`0x0045c8e0`). So where a cell
folds up into a ridge, the plane of one triangle carried over the other half is met first, and the pick lands on it,
above the drawn ground. On a ground cell `0x2` and `0x4` name the same diagonal (all four parks, 0 cells otherwise);
every hole carries neither bit (Lost Kingdom 1,159), so on a hole the pick splits on (x+1,z)-(x,z+1) where a flagged
hole's `FUN_004527f0` splits on (x,z)-(x+1,z+1).

**Which cells it tries, and in what order** (Q824, from `FUN_0045bf90` from `0x0045c254`, the rectangle at `0x0045c257`..`0x0045c344`, and the disassembly). The line
(the eye and the ray's direction, taken **both ways**: nothing tests the sign of `t`, so a plane behind the eye is met by its
distance like one ahead) is cut at the heightfield's low and high height (`+0x20`, `+0x24`, jungle -10 and 60), the two points'
cells (truncated, nought below, the last cell above) bound a rectangle of cells, and the rows run outside, the columns inside.
A cell is tried only if the line crosses its square (the line's side of two opposite corners, strictly opposite; the pair is the
one rising across the cell when the line's two horizontal components share a sign, signs read at `0x0045c347`..`0x0045c3c9`, tested at `0x0045c4f4`..`0x0045c515`) and then its heights'
range (the same test in x and height against the least and greatest of the four corners, `0x0045c648`..`0x0045c665`; a plane
hit can fall outside that range, so the test could drop one; the 1,000-ray reference in `ParkPickingTests` finds it drops none
for rays from above the park). Then the cell's two triangles in turn (`0x2` clear: a-b-c, then d-c-b; set: c-d-a, then
b-a-d, with a (x,z), b (x+1,z), c (x,z+1), d (x+1,z+1)), each plane through its first corner with the normal the cross
product of the two edge differences (made unit at `0x0045c7ea`; the quotient does not depend on it), `t` the signed distance
along the line; a hit stays only inside the cell's square (`<=` on all four sides; the z bounds are taken with the x spacing `+0x10`, `0x0045c789`..`0x0045c7af`, where OpenTPW uses the y spacing: every shipped heightfield is 10 x 10) and under the kept `|t|` (`<`, started at
`FLT_MAX`), so the first of two equal hits stands. Ours: `ParkPickGround.Meet`. Not ported: the object march and the choice
between its hit and the land's (`ParkPickMarch`, `ParkPicking` say theirs).

**The three words Q822 left unread** (Q825; Ghidra, the stored disassembly, and the original under Proton, `q825/`).

1. **A person's particle is placed by the sprite's own place, moved by two globals that are nought, at the ground's height.** `FUN_004fa470`
   (`this+0xc` the sprite's slot, an index into `0x007b49f0`) takes the place `FUN_004754e0` copies (`+0x88`, `+0x8c`, `+0x90`), adds `FUN_00519410()` and `FUN_00519420()` × 10.0
   (`[0x00700698]`) to x and z (`0x004fa496`..`0x004fa4d2`), then takes the height from `FUN_004527f0` at that x, z (`0x004fa4dc`) and drops the
   sprite's own `+0x8c`, and passes the three × 1024.0 (`[0x007006ec]`), truncated, to `Particles_Spawn( effect, x, y, z )` (`0x004fa51d`). The
   two accessors return the absolute words `0x00785584` and `0x00785588` (`0x00519410`, `0x00519420`: `mov eax, ds:..; ret`); the listing holds
   only READ references to them, and the file holds no bytes there (past `.data`'s raw data, so the loader zero-fills them: 0). **Measured**: both read 0 at every one of 120 polls in the stock park
   (`orig/spriteflags-stock.txt`, 60 s) and 40 polls in Alexah's Full Simulation park (`orig/fs/spriteflags-fs.txt`), and a `gdb` write watch on
   each (`watch.sh`, 20 s) fired 0 times: a silence in a quiet park with no control that fires, so the static fact (no store in the listing, though
   an object's field written through a pointer would not show as one) is the stronger half. So the offset is nought and a person's puff stands at
   the person's x and z, at the ground's height there by `ScapeHeight`, not at the sprite's `+0x8c` (about nought for a walker, `Person_PlaceSprite`).
   Its five callers: `FUN_00501db0` (`0x00501fce`, `0x005020ed`), `FUN_004faec0` (`0x004fb26e`, the guest's constructor), `FUN_004f7c60`
   (`0x004f7c76`, `0x004f7c81`), `FUN_00500bd0` (`0x00500c35`, the leaver's boarding puff) and `FUN_00505790` (`0x005059c3`, the staff dismissal). Ours
   counts the puffs of two and spawns neither (`LEAVER_BOARDING_PARTICLE`, `STAFF_DISMISSAL_PARTICLE`); the listing gives the effect each spawns: `FUN_004faec0`, `FUN_00500bd0`, `FUN_00501db0` (both) and `FUN_00505790` push `0x13`,
   `FUN_004f7c60` pushes `0x35` or `0x39` by the byte `+0x3a0` (`0x004f7c68`..`0x004f7c81`); whether ours counts them was not looked at; so nothing is placed yet (Q900).
2. **`0x1000000` in a sprite's `+0xc4` is the head sprites' alone.** The word is the sprite script's local 16 (`saves.md`, "a person's sprite's
   drawing flags"); the constructor writes it nought (`0x004758f0`) and the two copies write `& 0xfffffdff | 0x50000` (`0x00475d70`,
   `0x00475ee3`). The one script word that sets local 16 to a value holding `0x1000000` is word 1709 (`0x3000080`), of 55 `SetLocal 0x10` words in
   `DAT_0074dab8` (44 set `0x1200`, 8 `0x80000`, one each `0x80`, `0x51000` and `0x3000080`; no other opcode was looked for): a scan of the table's first `0x3000` words for `0x4766d0, 0x10, v`.
   **Measured** in the original with Alexah's park loaded by a Full Simulation player (`orig/fs/spriteflags-fs.txt`, 40 polls): 65 distinct sprite slots
   carry the bit, every one of kinds 1 and 3 (kids' and costume heads) with the word exactly `0x3000080`; no person's sprite (kinds 0, 2, 4 to 8, usually `0x1200`, a few `0x0` between construction and the script's first word)
   does, and the stock park (no rider on a ride) holds none. A head's `+0x8c` is a world height (6.2 to 57.7 on the 20 the poll printed, the unhung ones nought): the draw
   keeps it, as the table above says, where a person's is the offset above the ground.
   **Ours** stands a ride's head where its node is drawn (`ParkGuestSprites.HeadOnRide`, `TryDrawnNodeOn`, absolute, no sampler added), so the
   rule is already kept by construction; nothing is to build. **Measured in ours** (Q825, her park's copy, the final binary): the `rides` census lists the head NODES of the Tom Tom's 3 of 40 heads, the Inca God's 1 of 32, the Pork Pie's 4 of 8 and the Simulator's 5, each with its rest pose, its model place and its drawn place (`q825/ours3/run.out`); that ours stands the head sprite at the drawn place is the code (`ParkGuestSprites.HeadOnRide`), not a measurement of a sprite's place. The Tom Tom's heads have model place equal to drawn, so its frame (`tomtom-heads.png`) cannot tell them apart; where they differ (the Inca God, the Pork Pie, the Simulator) there is no frame. Not compared head by head with the original's, whose ride is at another moment.
3. **The eye's further term is nought in first person.** `0x007908f4` is the camera's look-at height: `Camera_ToggleFirstPerson` saves it and sets it
   to nought on the way in (`0x0042af76`) and puts the saved one back on the way out (`0x0042afa9`); the walk's sweep writes the stand back with y nought
   (`0x0042c5cd` stores the local vector's y, which every pass of the sweep zeroes at `0x0042bee4`; the helper `0x0042cd80` only reads x and z),
   and the camera block's loader (`FUN_0042cec0`, `0x0042cf9e`..`0x0042cfa7`) reads it from the save file with the look-at point. The orbit camera's resets write it from `0x006fdd4c`, which is 0
   (`0x0042a9ce`, `0x0042b550`, `0x0042a176`). `0x0042c6b9` copies it to `[0x00790abc]` each frame and `0x0042c9b8` adds that to the eye; the add is made in
   both cameras (`0x0042c967`..`0x0042c9e2` is straight-line, and `0x0054f973` also reads `[0x00790abc]`); the term is nought in first person, and in orbit
   it is the reset's 0, the sweep's 0 or the loaded file's word. **Measured** both read 0 at every poll of the two parks (orbit camera); the first-person
   reading is Q822's 1,440 polls (the eye exactly the sampler + 5), not repeated.

### What the render-state `0x0800` tests are

The terrain renderer `FUN_0056f670` has 16 sites pairing `TEST ?H,0x8` with a test of `0x4` in the cell's high byte (`TEST byte ptr [reg+0x1],0x4`, or `TEST AH,0x4` at `0x0056fb0e` and `0x0056fd67`), which look like a diagonal rule. They are not. All 16 sites were traced and every one has the same shape:

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

`0x0800` is absent from every shipped park because the loader derives it. After loading `base.md2`, `FUN_004504c0` walks every cell, builds the cross products of its **two triangles'** unit edges from the four corner heights (not made unit again, Q820), and where their dot product falls below `_DAT_006fe39c = 0.99990` (not an angle: the products are not made unit, so their lengths are the sines of the edges' angle, and a steep planar cell can flag; a cell bent by under about 0.1 passes) it does

    pbVar3 = (byte *)(cells + 1 + (y * cellsX + x) * 4);   *pbVar3 |= 8;

Byte `+1` of the 4-byte record is the HIGH byte of the flags u16, so `|= 8` there is `0x0800` in the word. It means "this cell is not planar"; `FUN_004527f0` reads it (`0x004528f8`, above). **A loader must derive this bit itself; reading it off disk gives zero everywhere.**

| Address | What it is |
|---|---|
| `_DAT_006fe39c` | 0.99990 — the threshold the dot of the two cross products is held to (not an angle) |
| `_DAT_006fe384` | 10.0 — the cell run |
| `_DAT_006fe390` | the **double** -100.0, so `sqrt(d*d - that)` is `sqrt(d*d + 100)`, the hypotenuse over one cell |
| `FUN_004508b0` | A plain cross product |
| `FUN_0043a9a0` | Normalise |
| `FUN_0043a970` | Dot product |

### The UV rule

`FUN_0056f4f0` is the UV permutation, and it is four lines. It takes a **4-byte array of corner indices** and permutes it in place from the cell's flag word:

    if ((flags & 0x78) == 0) nothing happens at all
    if (flags & 0x40)  swap p[1] and p[3]        <- MIRROR, and it is applied FIRST
    if (flags & 0x08)  p = [p3, p0, p1, p2]      <- one quarter turn, and return
    else if (flags & 0x10)  swap p0/p2 and p1/p3 <- two, and return
    else if (flags & 0x20)  p = [p1, p2, p3, p0] <- three

Mirror is a **diagonal reflection** (it exchanges the two off-diagonal corners), not a flip in x or y, and it happens **before** the rotation. The rotation bits are one/two/three quarter turns of the corner list, which agrees exactly with the `0 -> 0x08 -> 0x10 -> 0x20 -> 0` cycle the footprint stamper `FUN_00463060` uses; each returns, so where two are set the first of `0x08`, `0x10`, `0x20` wins.

**The ring** (Q488, `FUN_0056f670`): the array is `0x03020100` (`0x0056fa0b`), permuted for **every** drawn cell before its texture record is looked up, so a composited tile is turned exactly as a cell's own texture. Grid point (x, y) always takes corner byte 0, (x, y + 1) byte 1, (x + 1, y + 1) byte 2 and (x + 1, y) byte 3, whichever diagonal the cell is cut on (only the vertex order changes); byte k names the UV pair k of the table at `ESP+0x4c`: (0, 0), (0, 1), (1, 1), (1, 0). A UV is then `U x record+0x20 + record+0x28`, `V x record+0x24 + record+0x2c`, which `FUN_00590350` sets to place the texture in its page from the first texel's centre to the last's (offset `(pos + 0.5) / page`, scale `(size - 1) / page`; the 0.5 is `[0x008c1628]`, written at `0x00583968`).

**With the compositor on, no ground cell is turned.** `FUN_0046ded0(1)` takes `0x78` off the word of every cell with neither `0x80` nor `0x01` (`& 0xff87`); the park's load runs it once `base.lnd` is read with `[0x007a1a8c]` `0x4000` set (`0x00450813`), and so does `FUN_00457a00`, which turns the compositor on. `FUN_00457a40` turns it off and `FUN_0046ded0(0)` puts the file's bits `0x08` to `0x80` back from the pristine copy `[0x0079fc54]+0x2c`. Live at medium (Q479's dump of 7,980 cells) 11 cells carry the mirror where base.MD2 marks 6,886 of 8,160: the mirror stands only at low detail, where the cells draw their own textures.

### How a cell's textureIndex becomes an actual texture

Solved from the data, with no reverse engineering — the RE pass had called this the largest hole and it did not need one. A cell's high u16 indexes the model's own frame table: `frameCount` is a u16 at `0x36`, the frame records start at the offset in `0x54` and are **16 bytes** each, and the name pointer is the dword at `+12` of the record. The name is a `.tga`; the file on disk is the same stem with `.wct`, in `levels/<theme>/terrain/textures/`.

Verified in all four parks — **28 of 28 indices resolve to files that exist**:

    jungle   27->jgr_bas1  57->jgr_bas2  59->jgr_bas3  58->jgr_bas4  60->jgr_bas5  56->jgr_bas6
    fantasy   5->jgr_bas1  36->jgr_bas2  39->jgr_bas3  38->jgr_bas4  37->jgr_bas5  35->jgr_bas6
    hallow   15->hrk_bas1  42->hrk_bas2  43->hrk_bas3  45->hrk_bas4  46->hrk_bas5  44->hrk_bas6
    space     7->sfl_bas2  43->sfl_bas1  45->sfl_bas3  46->sfl_bas4  44->sfl_bas5  42->sfl_bas6

Every theme uses **exactly six ground-base textures** (`jgr`/`hrk`/`sfl` = jungle ground / hallow rock / space floor), and fantasy shares jungle's set. The indices themselves differ per theme, so they are positions in that model's table and nothing more — **never hard-code one**.

**Index 0 is the null slot, not a texture — confirmed by rendering it.** It resolves to whatever frame 0 happens to be (`grd_ctr1` in jungle, `jho_fnt1` in the other three) and sits on exactly the cells whose flag word is `0x1` — the river and the paths. Drawn, jungle's `grd_ctr1` turns out to be the **road centre texture, black with yellow markings**, and it paved the river bed and every path with tarmac. A texture the road scenery uses is not the ground under a river. **Do not draw index-0 cells as ground at all.**

**Skipping them does not leave holes — it uncovers the park.** `base.MD2` already carries the river with its flowing water and stone banks, the waterfall under the bridge, and the brick of the paths; a ground quad over a cell marked 0 puts a lid on all of it. So index 0 means "the scenery draws this cell's surface", not "nothing is built here".

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
| `FUN_004d8610` | Packs a ticket booth's cell to a packed cell id, `y * 128 + x + 1`: `FixedItemInfo.TicketBoothA` (`0x0078559c`, `0x007855a0`) for argument 0, `TicketBoothB` (`0x007855a4`, `0x007855a8`) otherwise. `FUN_004d8650` (bus stops), `FUN_004d8690` (entrances) and `FUN_004d86d0` do the same for theirs (`park.md`, "Arrivals") | Disassembly: `SHL EAX,0x7` on the Y, then `LEA ..,[EAX + ECX + 1]` (`0x004d8623`, `0x004d8626`). The values are used as cell numbers with no scaling |
| `FUN_004dd500` | Builds a placed thing's model (`FUN_00463060`, or `FUN_00463ab0`, which re-claims an existing instance when its argument is non-zero) at an integer cell: the thing's packed cell id `+0x34` unpacked as `x = (id - 1) & 0x7f`, `y = (id - 1) >> 7` (`0x004dd5d3`..`0x004dd5e5`; `+0x34` is `mTopLeft`, so an item with a map offset is made a row or a column off its anchor, the Huge Hollow Rock a row before it unturned, the one arm every placed object takes, at every angle, so the turn about that cell is every item's (`OriginFor`'s, matched at 0, 90, 180 and 270 degrees on items with no offset, `q257v/census.out`); **OpenTPW builds it**, `ParkObjects.ModelCell`, for the model, its nodes and its `.hmp`), or, for an item whose `+0x38` is set, the item's own `+0x20`/`+0x24` with `MapInfo.FixedItemOriginX`/`Y` (`[0x00785594]`/`[0x00785598]`, the two value slots before `TicketBoothAPosX`'s `0x0078559c` in the balance table's order) where they are nought. It calls none of the FixedItemInfo packers | Disassembly. From the data: `base.map`'s `0x08`/`0x90`/`0x94` bits land exactly on the FixedItemInfo coordinates in all four themes |

### Where a placed object's model stands

**Its height is halfway between the lowest and the highest ground under its footprint.** `FUN_004dd500` hands the model
maker `FUN_00463060` the flags `0x32f` and a place `{ x, y, angle, footprint }`, the footprint being the item's parsed
`Info.Shape` at item `+0x18` ("The buy screen's footprint picture"). Under the flag `0x8` with a footprint
(`0x004632a5`..`0x004632ac`) the height is `FUN_00466d70`'s: for every cell of the shape whose kind's low four bits are
not nought (`FUN_00469930`), its four corners carried through the angle to ground vertices (`FUN_00454050`, 0.1 added and
cut to whole vertices), the lowest and highest of their heights kept (`0x00466ef6`..`0x00466f13`, from 10000 and -10000), and
`(lowest + highest) * 0.5` answered (`0x00466f74`..`0x00466f80`). Without a footprint the height is the ground's vertex
at the model's cell (`0x004632d7`..`0x004632ec`). **Measured:** the 55 node-placed emitters of thirteen park files the
original wrote all stand at their file's height cut to whole units under this rule, and 44 under the vertex at the
model's cell (`saves.md`, "Measured on eighteen files"): the Huge Hollow Rock of Alexah's Full Simulation jungle
autosave, on the slope from -7 to -2 under its rows 26 to 30, stands at -4.5, its water at 12 (`768 / 64`), where the
vertex gives -7 and 9. **OpenTPW builds it** (`ParkObjects.ModelOriginFor`, `BaseHeight`): the model, its nodes and the
base its `.hmp` is laid from. Not separated by any file read: a shape's empty cells, whose corners no file's ground
tells apart from their neighbours'.

**The same flag `0x8` also conforms the model's base vertices to the ground** (Q750; fork watch 2026-10-27, verify-world H1,
verify-other L2). After the model is made, `test bl,8` (`0x00463329`) and `FUN_00469940( [instance + 8], DAT_007a0854, base
height, 0 )` (`0x00463347`) run `FUN_00469a80` in its apply mode, the mode whose drape arm is Q398's and Q695's. Flag `0x8` is in
`0x32f` (a purchase and every thing of a loaded park, `0x004dd64f`), `0x3fa` (the coaster's pylons, made by `Coaster_AddNode`, `0x00434276`; the type's own temporary model at `0x00430883`, in `Coaster_SetupPylonModel`, destroyed at `0x00430a99`) and `0x33a` (the kart, water, queue
and hoarding pieces); a pylon with a floor mesh is conformed again when it is the bottom of its cell's stack and the cell is stood, and when it is moved to another cell (`0x00432f2f`, `0x004432db`; "A coaster's pylons", below). Only the vertices the type load listed are
taken (the record's list at `+0x14`, counted by `ProceduralBase_Build` at `0x0046a808` and copied at `0x0046a853`..`0x0046a87b`,
on nodes the count flagged `0x1000`): the base vertices `ProceduralBase.FloorMesh` already counts, on every node that has one.
Each is carried into world space (the node chain, `0x004702d0`); x and z snap to the grid line when within 0.01 of it
(`0x00469f71`..`0x0046a028`), and the nearest terrain corner (a fraction of 0.5 or more takes the next) gives y outright,
nothing added (`0x0046a332`..`0x0046a33c`; the base height is used only by the drape and the interpolating arms); the point is
carried back through the float LU inverse (`0x00470040`, `0x00469b37`, applied at `0x0046a121`) into the instance node's own
vertices, its box only widened. A corner past the heightfield's last ends that node and skips its children (`0x00469dd7`..
`0x0046a2a0`). So on a slope the floor's rim lies on the terrain's corners while the inner floor stays at the height above.
**The instance's nodes carry the type's flags and their own vertices** (Q812). `FUN_00463060` makes the instance by
`FUN_004557c0` -> `FUN_00454a10` with `0x211f` (`0x231f` under its own `0x800`), which copies each mesh node's 0xa0-byte record
whole (`0x00454d72`), so `0x1000`, `0x20000` and the list pointer at `+0x98` come across; bit `0x4` becomes `0x100` (`0x00454a4e`..`0x00454a67`), which copies the vertex blocks too
(`0x00454f64`, `0x00454fa1`, each node's `+0x60` moved onto the copy from `0x00454fd2`), so the conform writes the instance's own vertices and
reads the type's (`[header + 0x9c]`, the master) at rest. The instancer then sets `0x10000` on every node of every instance it makes and calls `Model_RecomputeStaleFaceNormals`
(`FUN_00473c30`, `0x00463937`), which recomputes the face normals of the nodes so flagged (`Mesh_RecomputeFaceNormals`,
`FUN_00473a40`) and clears header `0x40000`.

**The recomputed face normals never reach the lighting** (Q819). Each node's normals table (`+0x64`, twelve bytes an
entry) is two runs (FileFormats `models.md`, "Normals"): one entry per vertex-order entry, then the faces'. The clone's
`0x8` (in the instancer's `0x211f`) copies the header's normals pool (`+0x5c`, `+0x4a` entries; `0x00454f05`..`0x00454f44`)
and rebases each node's `+0x64` into it (`0x00455012`..`0x0045503f`), so every instance has its own table. `Mesh_RecomputeFaceNormals`
takes each face record (word, a, b, c), its corners through the vertex order (`+0x94`) into the vertices (`+0x60`), and
writes normalise( (c - b) x (a - b) ) at the entry the word's low 15 bits name, or (0, -1, 0) when that is not a number
(`FUN_0067b410`); then clears the node's `0x10000`. The lighter (`Light_ModelVertexColours`, `0x00574660`) reads only the
first run, entry i for vertex-order entry i (`+0x5e` of them), and so does the draw list's normal-made UV
(`DrawList_AddMaterialFaces`, for a texture flagged `0x8000000`: u = (n . r + 1) / 2, scaled and offset, from the view's rows at
`[0x0087afe8]`; the normal-made UV loads `+0x64` at `0x00580691`, `0x00580873` and `0x00581488`, and `0x005806f8`..`0x005807b4` is one),
which index differently: the lighter steps its entries by the order's position (`ebx += 0xc`, `0x00574797`), the UV reads the entry
of the vertex number the order names (`0x00580707`..`0x00580715`). The first run has two more users: the Terrain's `base.md2` scaler
(`FUN_00450b36`, `0x00450b81`, reads and writes it in place, the half-lit land of Q619) and the pulse (below); the highlight swap
(`0x00469288`) copies only the face run. No park model's face names an entry in the first run: of the 4,917 meshes of the 841
version-`0xDD` `.md2` that have a mesh table among the 2,123 under `data/` (the four others with one, `wr_tunnel.md2` and the two
arrow models `garrow`/`rarrow`, are never drawn and the engine refuses to load a version above `0xDD`, `0x0046d768`), 153
have such faces, all in `ui.wad` (`lib/facenormals.py --sweep`), and the vertex count never passes the vertex-order length
(checked over the 4,917 by a scratch script, not stored). **The face entries' readers are three**: the picker
(`PickMarch_MeetModel`, `0x0045d16a`; `PickMarch_AskCell` first recomputes a model's stale nodes, `0x0045ced9`), the
height a point stands on (`FUN_00451390`, `0x0045142b`, when no normals are handed in) and **the pose walk**
(`FUN_0044ab90`, `0x0044ad11`..`0x0044ad2a`: for a node anchored on a face of its parent mesh, flags `0x40040` and the parent
`0x200000`, the face's normal times the anchor's offset is added to the head's place, "Where a head on a morphing face stands",
`park.md`). **Who marks a head's parent stale**: `FUN_0044a870`, the lookup-record pass the item loader calls (`0x004627ce`),
sets `0x10000000` on the parent of every face-anchored record (`or [ecx], 0x10000000`, `0x0044a93c`); the pose walk gives each
visited node with `0x10000000` and `0x10000` the recompute (`FUN_00473a40`, `0x0044afe0`) before its children are walked
(`0x0044afe8`), and the morph (`FUN_00471860`, `0x00471931`; `FUN_004714a0`) sets `0x10000` on the morphed mesh, so **each pose
works a morphing parent's face normals out again from the morphed `+0x60` before a head reads one**. Ours does not
(`ModelFile.PointOnFace` takes the file's entry, `LobbyModel.TryGetDrawnNode` hands it the morphed corners only), so a
head on a morphing face stands off the original's place by the offset times the normal's change: by VERIFY's decode of the
morph tracks (linear keys; not read live), at most 1.1 to 3.1 units for Mumbo's five heads in his morph clips (`mumbom`,
`Pmumbom1`, `mumbos`, `mumboe` 1.08 to 1.71, `mumboc` up to 3.08), 0.24 to 0.37 for the Crazy Ape's `Head1`..`16` and up to 1.03 for
his `Head17`; at rest the file's entries equal the recomputed ones (0.00 degrees on all 30 anchors), so the rest pose agrees. Filed
as a build item, not built. After a conform `FUN_00469940`
marks only the instance's root node `0x10000` and its header `0x40000` (`0x00469a56`..`0x00469a6b`); the instancer marks
every node after it, a pylon's later stand does not, and `Model_AdvanceChannels` recomputes marked nodes for a header
flagged `0x4` (`0x00473dc1`, `0x00473ddf`) and the pose walk those flagged `0x10000000` (`0x0044afe0`); `FUN_00473a40` has three callers (`0x0044afe0`, `0x00473c4f`, `0x00473ddf`). **Measured in the original** (`q819/orig/o3`, `o5`, `original/facenormals.py`; a Full
Simulation player loading a copy of Alexah's `New Save.TPWS`, 200 s; o3's counts, o5's within three meshes): 1,192 meshes (24 of them the unread `cart.MD2` meshes, slots 330 to 353, counted but never compared, so 1,168 read; why they do not read was not found), every table the instance's own; the
first run as the master's in every mesh but two colour-swapped ones (below); no face names a first-run entry; 31,167 of
33,709 faces hold the normal worked out from their instance's own vertices to 1e-4, and the other 2,542 are all in the 60
meshes still marked `0x10000`; 6,774 face entries differ from the master's (`gates.MD2`'s 187 among them, its file
entries lying off their planes; which of the rest are conformed floors was not counted). **A mesh flagged `0x1000000` holds colours at `+0x64`, not normals**: the lighter passes it
over (`Light_ModelSetup`, `0x0057aa10`) and the draw takes `+0x64` as its colour stream (`DrawList_AddMesh`,
`0x00581d56`). `Model_PulseOn` (`0x00469580`) sets it on every node and its tick writes one `0xffffVVVV` word per
vertex-order entry into the table, over the first run (a new pool only where the instance still shares the master's);
`Model_PulseOff` (`0x00469650`) copies the master's first run back. The hover's highlight swap `FUN_00469180` points
`+0x64` at a tint block and keeps the old pointer in `[0x007afd94]`: in the run the two were the pylon under the Load
click's pointer (`[0x007a1ce0]` = slot 202, `mc_pylon` and `mc_bridge`, 160 entries; the pulse list empty), drawn bright
yellow under "Left-click to edit this track" (`q819/orig/o5/o-t19651.png`). OpenTPW's picker takes each triangle's plane from its
placed corners (`ParkPickMarch.MeshTriangles`), the same vector (the cross product before the original normalises), and it lights by its own normals, so **for the lighting and the picker nothing is to build; for a head on a morphing face the pose walk's recompute is a build** (above).

**`FUN_00469940( model, scene, H, mode )` makes two passes** (Q812). The first is `FUN_00469a80( master root, instance root )`
over the whole tree (`0x004699d2`); the second runs it again on each mesh node with a list at `+0x98` (`0x004699eb`), that node
alone (`0x00469b1d`) and in the interpolating mode (tolerances 0.1 and 100, `0x00469a96`). The mode's bit `0x1` is the header's
`0x100`; bits `0x2` (draped nodes only) and `0x4` (no draped node) are never passed by the instancer. In the apply mode, node by
node, children after their parent (a node flagged `0x200` is passed over, its children not, `0x00469b0e`):

- **Drape**, a node flagged `0x20000` (`0x00469ad0`): every vertex is carried into the world, its cell's triangle chosen by the
  cell word's `0x4` (`0x0046a34c`; Q398's note), and the vertex raised by the height there less H, then carried back.
- **Conform**, any other node, and only one flagged `0x1000` (`0x00469af7`): a vertex is taken only where its index is the next
  entry of the type load's list (`[[model + 8] + 0x14]`, `+0xc` entries, consumed in the walk's order), and stands at the nearest
  corner, as above.
- **Interpolate**, the second pass: the vertices `ProceduralBase_Build`'s second count listed for a mesh flagged `0x40` other
  than the floor (`0x0046a8a3`..`0x0046a934`: that node alone, a vertex within 0.1 of a cell line on either axis and within 100 of the model's own zero height (`0x0046a44f`..`0x0046a45e`),
  on a side the footprint's outline runs along; the list `{ count, pointer }` at `+0x98`). The vertex snaps to the line within
  0.1 and rises by the ground interpolated along that cell line, less H, in four arms (`0x0046a0b4`..`0x0046a0ea` and `0x0046a2a7`..`0x0046a32d`).

**Which nodes take which.** A mesh node's run-time `0x40` is its node-lookup record's flag `0x8`, and `0x20000` the record's
`0x10000` (`FUN_0044a870`, `0x0044a942`..`0x0044a954`); `FUN_00461f10` also marks `0x20000` on every mesh of a model loaded with
load bit `0x2`, the queue, kart, water and hoarding pieces (`textures.md`), whose run-time header `0x80` leaves
`ProceduralBase_Build` no outline, so they list nothing. In Lost Kingdom's wads lookup `0x8` is on nine meshes, all of the kart
and water rides (`gk_TRACK01`; `wr_log1`, `wr_log2`, `wr_water3`, `wr_water4`) and their add-ons (`lavajump`'s `gk_brdge`;
`watertun`'s `wr_mwater1`, `wr_mwater2`, `wr_mwater04`), and `0x10000` on one, `mammtunn`'s `gk_mam1` (`q812/lookflags.py`). So
every placed object's floor rim takes the conform; the queue, hoarding and track pieces and `gk_mam1` the drape; those nine
meshes the interpolating pass. The second count also sets `0x1000` on each such mesh that has a listed vertex (`0x0046a516`..`0x0046a523`), which the apply mode needs there (`0x00469af7`), so the first pass visits the mesh too and compares its vertices with the type's list's next word (`0x00469b7c`): a coinciding index would be conformed and would use up an entry meant for a later mesh. `gk_TRACK01` carries `0x40001141` in the original, and 17 of its vertices moved against 16 listed; the 17th is unexplained. A fixed item (`0x361`: the gate, lights, bus, ferry, seaplane) and a vehicle (`0x101`: a ride's
car, a kart, the bird) are made without `0x8` and take none. **A loaded park conforms and drapes again**: `RideSystem_LoadModels`
hands each model record's saved word (`+0x15`) to `FUN_00463060` as its flags (`0x00464942`).

**Measured in the original** (Q812, `q812/orig/orig1/`, `original/basevertices.py`): a Full Simulation player under Proton
loading a copy of Alexah's `New Save.TPWS`, about tick 19300. 917 of the 925 listed base vertices of its placed things were seen, and every one stands
at its terrain corner's height, none off (the other 8 are the floors of the two coaster pylons in slots 202 and 210, which that reader's walk passed over for their `0x80000000`; walked as `FUN_00469a80` walks, all 925 stand on a corner: "A coaster's pylons", below, Q816): the Sun God (`incagod`, made `0x32f`, root at -4.5) 18 of 18 on its `Base` mesh (flags
`0x5101`), all 18 moved from rest; the Tom Tom Twister 20 of 20, the Costume Shop 12 of 12. Its 270 draped pieces (368 meshes, by the walk that passes over a node flagged `0x80000000`; `basevertices.py --as-original` counts 402 and one more corner on slot 173's monkey: 78 queue pieces,
163 hoarding pieces, 28 kart track pieces, `gk_mam1`) each rise by the ground under each vertex less one value, the piece's own
root height, to 0.0001. The interpolating pass lists the kart station's 16 vertices (`gk_TRACK01`; 17 of its vertices differ from the master's) and the
Lava Jump's 20 (`gk_brdge`; 16 differ), each standing by the ground along its cell line less the root height, to 0.0001 (`orig2/bv80.txt`). Things were made `0x32f` (119) or `0x3fa` (40, the coaster's pylons), pieces `0x33a` (269). In its `bv75` the 119 things list 765 base vertices and use 765, the 40 pylons list 160 and use 152 (765 + 152 = 917 used, 765 + 160 = 925 listed); a mesh whose lookup record carries `0x10000` (`mammtunn`'s `gk_mam1`) is skipped by the count (`0x0044a94b`, `0x00469ad0`), so the Dino Karts Tunnel lists 7, all on `gk_tracktop01`. Its frame at
the Sun God camera holds no clear-coloured sliver (`o-t19197.png`, 0); ours at the same camera, unchanged, 5,652 (`q812/ours1`).
**The control word** (Q819): the conform sets none (the executable's `fldcw`s are the CRT's, `__ftol`'s, the frame
renderer's `FUN_00591220` and `FUN_00591250` (called at `0x00576fa7` and `0x00577304`; the three `fldcw` are at `0x00591233`..`0x0059125c`), a class method at `0x0062f9c3` and a coaster routine's own chop at `0x00439ab0`..`0x00439c30`; none lies in `0x469xxx` or `0x473xxx`), so it runs under the one in
force. Under Proton it is `0x27f` (53-bit, nearest, all masked): `original/brk.sh` on `FUN_00469940` and `FUN_00473c30`
read it on the first 40 calls (the cap of the reader's hits; the load makes more) during the load of a copy of Alexah's `New Save.TPWS` (`q819/orig/o2/brk.txt`, every hit at the
file's tick 19004). What Windows' own DirectDraw leaves under `DDSCL_FPUSETUP` cannot be read without real Windows
("Which rounding is live is not settled", below); it moves only the last bits. OpenTPW works the conform in `float` (the corner's
fraction in `double`, `ProceduralConform`), which matches the original's stored vertices to 0.0001 (above).

**A coaster's pylons** (Q816). Each node of a coaster's track that stands on a cell has a pylon, made `0x3fa` by `Coaster_AddNode`
(`FUN_00463060` at `0x00434276`, which conforms it as it is made, as above; or an existing instance re-claimed by `FUN_00463ab0` at
`0x00434257`, which conforms nothing). A cell holds a stack of nodes (`+0x30` count, `+0x34` the nodes, bottom first; each node's
`+4` its level, `+0x100` its cell) and names its pylon type (`+0x18`). **`Coaster_StandCellPylons( type, cell, force, quiet )`
(`0x00432df0`) stands a cell's stack**, node by node, passing over a node flagged `0x2000`:

- **The bottom pylon** (level 0): its root's height is the cell's (`+0x20`, the midpoint of its corners, `FUN_00466d70`), its clips
  are posed (`Model_AdvanceChannels( model, 1, 0 )`), its floor mesh (the instance's `+0x2c`, the mesh `ProceduralBase_Build`
  found, `StdPylon`'s `mc_pylbase`) is shown by taking off `0x10` alone, and `FUN_00469940( [model + 8], scene, cell height, 0 )`
  conforms it (`0x00432f2f`): its listed base vertices stand on the corners, as a placed thing's. Both the show and the conform are skipped when the type has no floor mesh (`0x00432f0a`..`0x00432f0f`).
- **A stacked pylon** (level 1 or more): its root stands at the height of the pylon below's **stack-height record**, the lookup record
  flagged `0x100000` the type's `+0x18` names (`0x00430780`..`0x004307ab`; `StackHeightDummy` on `StdPylon`, not its knot; its `+0x34`;
  nought where the type has none), or, where the node below is flagged `0x2000`, at that node's `+0x50` plus the type's step `+0xc4`, which is
  that record's height less the knot's (`0x004309e3`); its clips are posed; and unless the type's flag word holds `0x10` its floor is hidden, `0x80000010`
  (`0x00432ed9`). It is not conformed here.
- **The type's four named nodes** (`Coaster_SetupPylonModel` walks the lookup records flagged `0x80000` and files each one's index by the record's id 1 to 4 (`0x004307b2`..`0x0043082c`), setting the type flag below; the stand turns the index to a node by adding the header's word `+0x46`, taking `[header + 0x70] + n * 0xa0` below word `+0x44`, else `[header + 0x74] + (n - word) * 0x58` (`0x00432f4e`..`0x00432f8f`); `StdPylon` has one record, `mc_bridge` of id 1, `LoopPylon` none): flag `0x400` and node `+0x1c`, `0x800` and `+0x20`, `0x1000` and `+0x24`, `0x2000` and `+0x28`):
  at the top of the stack `0x400` hides its node and `0x800` shows its; below the top `0x400` shows its (where the node above is
  not `0x2000`) and `0x800` hides its; at the bottom `0x1000` hides and `0x2000` shows; above it `0x1000` shows and `0x2000` hides.
  A hide is `Coaster_HideNode( node, 1 )` (`0x00433220`: `0x80000010`; with nought, `0x10` alone); a show takes off `0x10` alone.

With `quiet` nought it also marks each node, and the node at its `+0x68`, `0x200000`. It runs at a load over every cell of the coaster except one whose `+4` holds `0x1` (`0x00438014`..`0x00438018`)
(`Coaster_LoadModule`, `0x004380c1`, `force` nought, `quiet` 1; while `0x00790afc` is set only a call with `force` runs), when a node
joins a cell (`Coaster_StackNodeOnCell`, `0x00433f54`) or leaves one (`FUN_00436490`, `0x004365ea`, `0x004366fb`), and from the track
tool's `FUN_004424e0`, `FUN_00443f40` and `FUN_00444cb0`. **`Coaster_MoveNodeToCell` (`0x00442f40`)**, the track tool moving a node to
another cell, takes it off its old cell and stands that one again where others are left on it (a cell it holds alone moves with it,
its place and height rewritten), moves the model (`FUN_004671f0` with `0x3fa`), puts it on the new
cell (`Coaster_NewCell` or `Coaster_StackNodeOnCell`, which stands it), poses it (`FUN_004346f0`), sets its header's `+0x30` `0x4000`
and conforms it (`0x004432db`, the new cell's height) whatever its level, a stacked pylon's hidden floor too. A target that is the node's own cell takes none of that: it stands the cell again and leaves (`0x00442f55`..`0x00442f63`, `0x00442fc5`, then `0x004432e0`, stack cleanup only).

**A pylon shown again keeps `0x80000000`**: the stand clears `0x10` and nothing else, and the park file's node word `0x400` (`saves.md`,
"A node word") puts `0x80000000` back at a load. **Measured** (Q816, `q816/orig/orig1`, `orig2`: the original, a Full Simulation player,
a copy of Alexah's `New Save.TPWS`, ticks 19283 and 19322, `original/coaster.py pylons`, `original/basevertices.py`): the Temple of
Gloom's 40 pylons each stand alone on their cell (no stack in her park), type flags `0x5ed` (`0x400` set, `0x10`, `0x800`, `0x1000`
and `0x2000` clear), and every one's `mc_bridge` holds `0x80000010` (39 `0x807408b1`, one `0x817408b1`), hidden (the type's `0x400` node is `mc_bridge`, `Coaster_SetupPylonModel` `0x004307ff`; the saved word `0xda3` already carries it, so the measure does not tell the stand's write from the load's). 38 floors read `0x5101`; the
two of slots 202 and 210 (nodes 4 and 5, cells (29,35) and (32,35), the only two at loft 1, their knots at 100) read `0x80005101`,
`0x10` clear, and their records in the file hold node word `0x408` on `mc_pylbase` where the other 38 hold `0x0` or `0x8`. Both
floors are drawn (`orig2/o-t19442.png`: a sandy square at each tower's foot) and stood by the same walk: walked as `FUN_00469a80` walks
(`basevertices.py --as-original`), 925 of the park's 925 listed base vertices stand on a corner, 4 of 4 on each of the two (the ground
under them is 0, so this measure cannot tell their conformed places from rest; the bottom arm calls the conform whenever the floor mesh exists). How the file came to hold `0x400` there is not recoverable from it; a
hide by the stacked arm (`0x00432ed9`; the type's arms touch only the four named nodes, `mc_bridge` on `StdPylon`, never `mc_pylbase`) then the bottom's show (`0x00432f15`) leaves exactly that word.

**OpenTPW draws the pylons** (Q817, `ParkCoasterPylons`; the plan is `ParkCoasterHeights.Pylons`). One for each of the coaster's station nodes
1 and 3 (the second stands at its track node, with none) and each saved piece: her park's 40, the original's. Each is a `StdPylon` model made on its cell,
its root at the midpoint of the cell's corners and the cell's corner for x and y, **made on a one-cell footprint** (the floor search then lists `mc_pylbase`'s four
corners, the original's 160 over 40; made on none it lists nothing, and the floor is left unconformed), the floor conformed as a placed thing's
(`ProceduralConform.Conform`), then posed by `ParkCoasterPylon.Pose` (the loft clip's position tracks; the rotate and incline and bank clips' rotation keys as
the nodes' turns, which the file leaves unturned there) and morphed (`ParkCoasterPylon.Morph`). **A pylon model is additive (header `0x4`) and its
morph and texture keys are offsets**: `FUN_004714a0` adds each clip's keys to the vertex where it otherwise assigns them (`0x00471bef` passes the
header's `0x4`, `FUN_00470e90`) and the texture keys likewise (the poser hands `FUN_004745c0` the same `+0x30 & 4`, `0x004721c0`..`0x004721cd`, which adds the sampled UV at `0x004746ae` where it otherwise assigns it at `0x004747e4`), so the vertex is its rest plus the loft clip's key (0 at frame 0, 90 up at frame 100 on the column's raised vertices, the foot's none),
the incline's and the bank's (up or down by about 3.5 by the vertex's side, which tips the column's head), and its V the rest plus the loft's 15 at the top (a lattice repeat
to 6 units) and the incline's and the bank's (about ±0.58);
measured on `StdPylonL`, `I` and `B` (`q817/morphprobe.txt`). A pylon alone on its cell is the bottom and the top of its stack, so the type's part flagged
`0x400` (found as `Coaster_SetupPylonModel` finds it, the record of id 1 in space `0x80000`: `mc_bridge`) is hidden and the others the model has are hidden or shown
by the arms above; its floor is shown. **Not built:** a stacked pylon and a loop pylon (counted `COASTER_STACKED_PYLON`, `COASTER_LOOP_PYLON`; their coaster keeps
its other pylons, drawn); `Coaster_MoveNodeToCell`'s conform (a coaster is not edited here; its same-cell path only stands the cell again, `0x00442f55`..`0x00442f63`);
the saved node word's `0x400` (the `0x80000000` it puts back changes nothing drawn); the incline and bank clips' turns of `TrackCentreDummy`
composed (both clips turn that node, which no mesh hangs from: the later replaces the earlier). Not a gap: a morphed pylon's vertex normals stay as made, as the original's do (the lighter reads the first run, which neither the morph nor the recompute writes, Q819).
**Measured in OpenTPW** (Q817, Alexah's New Save, a copy; the final binary): `pylons` lists 40, each knot drawn where `ParkCoasterHeights.OnPylon` puts it (off 0.0000),
each floor 4 listed and 4 on a corner (160 conformed vertices), `mc_bridge` hidden on all 40; the two at cells (29, 35) and (32, 35) (loft 1) stand 100 up.

**OpenTPW builds the conform and the drape** (Q808, `ProceduralConform`): `LobbyModel`'s `stand` hook moves the drawn vertices of the
model's file as it is made, before the meshes are built from them, in the model's own place and turn. The conform takes the base
vertices `ProceduralBase.BaseVertices` lists (the floor search's own walk) of a model with the header's `0x8`, snaps x and y to the
grid line and sets the height to the nearest corner's, nothing added (always, and a corner past the field clamps to its edge, where the original snaps an axis only within 0.01 of a line, `0x00469f71`..`0x0046a028`, and ends the node and its children at a corner past the field, `0x00469daf`..`0x00469efe`; no placed thing reaches the difference); the drape takes every mesh of a queue, hoarding, kart or water
piece, and the meshes a placed object's lookup record marks `0x10000` (`mammtunn`'s `gk_mam1`), and raises each vertex by the
height under it on the cell's triangle (`DrapeHeight`, the flag `0x4` as above) less the model's own height; a source vertex moves
under every drawn vertex that names it, and `SourceVertices` stay the rest the floor search and the hoardings read. The cell's
triangle here is the original's, and the ground mesh is cut on the same diagonal since Q821. Nothing is to build for the control word or for the recomputed face normals' lighting (Q819, "The recomputed face normals never reach the lighting" and "The control word", above); the pose walk's recompute for a head on a morphing face is a build, filed. **Measured in OpenTPW** (Q808, Alexah's New Save, a copy): the Sun God 18 of 18 listed rim
vertices on a corner, the Tom Tom Twister 20 of 20, the Costume Shop 12 of 12 (the original's counts), 1,782 queue vertices and
1,736 track-piece vertices draped, and the clear-coloured sliver pixels at the Sun God camera 5,751 to 11, the Tom Tom Twister's
16,308 to 0, the Costume Shop's 7,844 to 0.

**OpenTPW builds the interpolating pass** (Q815, `ProceduralConform.Interpolate`, run by `ParkObjects.StandOnGround` after the conform and the
drape, on a model with the header's `0x8`). `ProceduralBase.InterpolatedVertices` is the second count: of each mesh the lookup records mark `0x8`
but the floor, the vertices within 0.1 of a cell line on either axis (the immediate at `0x00469a96`) and within 100 of the model's zero height (`0x00469a9e`) in
the model's own space, on a side of the footprint's outline: the count is the conform's, `ProceduralBase.IsBase`, with that tolerance, the
either-axis rule, and each side's bit counted only for the axis the vertex is on a line of (`FUN_00469a80`'s four terms in its count mode: x on a line with the
x-low bit `0x2` or the x-high bit `0x8`, y on a line with `0x1` or `0x4`). The edge table is the one `ProceduralBase_Build` fills from the
item's footprint before either count and leaves in `DAT_007afe20` (the head of the function at `0x0046a5a0`); with no footprint, or the header's `0x80`, it is
cleared; a piece's meshes are drape nodes and count nothing (`0x0046a43d`), and a model with no footprint reads the side bits from before the table (`0x007afdcc`, via `0x0046a489`..`0x0046a48f`), which is unread. `ProceduralConform.Interpolated` stands one listed vertex (`FUN_00469a80`, `in_stack_70` set, apply
mode, not a drape node; the constants `0x006fe850` = 1, `0x006fe860` = 0, `0x006fe864` = -1, `0x006fe868` = 0.5): each axis's grid position
(a negative taken as nought) splits by `modf` into a whole cell and a fraction; the **nearest corner** is the whole, or the whole plus one from a
fraction of 0.5 (`0x00469e40`..`0x00469efb`; a corner past the heightfield's last, row stride `[grid + 0x18] + 1`, ends the mesh); a fraction
within 0.1 of 0 or 1 is **on a line** (`0x0046a028`), and snaps that axis to the nearest corner's line (`(whole - (-1)) * cell + origin` from 0.5,
else `whole * cell + origin`); and the height rises by `blend - H` (H = the made height, `0x0046a0b4`..`0x0046a0ea`, `0x0046a2a7`..`0x0046a32d`)
in four arms, the blend taking the heights of the two corners the cell edge joins at the vertex's fraction `t` (or `s` for y):

| x on a line? | arm | blend |
|---|---|---|
| no | x, fraction under 0.5 | `(1 - t) * h[i] + t * h[i + 1]` |
| no | x, from 0.5 | `t * h[i] + (1 - t) * h[i - 1]` |
| yes | y, fraction under 0.5 | `(1 - s) * h[i] + s * h[i + W + 1]` |
| yes | y, from 0.5 | `s * h[i] + (1 - s) * h[i - W - 1]` |

(`i` the nearest corner's index, `W` the heightfield's width, so `W + 1` its row stride.) A vertex on a line both ways takes the y arms with its
own `s` (near 0 or 1), so a corner a few hundredths off a line is not given the corner's height exactly. The rise is added to the vertex's own
world height; the model is carried back by the float LU inverse as the conform's. **Measured in OpenTPW** (Q815, Alexah's New Save, a copy):
`rim` reads the Dino Karts station `interpolated 16 listed largest rise off 0.0000` and the Dino Karts Jump `interpolated 20 listed largest
rise off 0.0000`, the original's counts and difference (Q812); the rise off is each listed vertex's drawn rise less the ground at its drawn place
(the cell's own triangle, `DrapeHeight`) less H. The original changed 17 of `gk_TRACK01`'s 77 vertices from the master's (by more than 0.0001, `basevertices.py`), so at least one is outside the 16 it lists, and ours writes only the 16 listed (the Jump's `gk_brdge`: 16 of its 20 listed changed in the original); the 17th (which a spurious conform
is a candidate for, unproven: the first pass compares the mesh's vertex indices with the type list's next word, which for the last mesh of the walk is
past its end) is not reproduced.

**What its absence looked like in OpenTPW, before Q808** (Q804, Q807; measured 2026-10-29, `q804/`). Ours stands the plate at its midway height and draws no
ground under a footprint or a queue piece (the compositor's `0x1`), so on a slope the step between the plate's rim (a queue piece's
flat plane at its near corner's height) and the terrain corner beside it is an open face: a ray through it meets nothing and the frame's
clear colour `0x44DDFF` shows, a thin light-blue sliver along the plot's edge (orange once, where the lava under the ground lies behind
it; `pick` on a sliver answers a ground point about a unit short of the cell line, where its ray meets the ground behind the step). The console's `rim` counts the steps: the stock park's 14 things and 4
queue cells stand on level ground (every step 0.00) and draw none; Alexah's Full Simulation jungle (`New Save.TPWS`, tick 19004) has 58
objects over a step above 0.05 (the Sun God: ground -7 to -2, stands -4.5, step 2.5; the Tom Tom Twister 2.5; the Costume Shop 1.5) and
58 of its 78 queue cells (largest 1.00). `lib/clearseam.py` at one camera (160, 270, zoom 90, yaw 240): 5,751 pixels in ours, 0 in the
original's frames of the same file (`orig/sungod/o-t19198.png`, loaded by a Full Simulation player under Proton); a level plot in the same
park, the Belly Bounce and the Aztec Mayhem (step 0.00), draws 0 in ours, the Tom Tom Twister 16,308 and the Costume Shop 7,844. A ground
quad drawn 0.3 under the skipped cells took the sliver count from 12,166 to 2,044 at a close camera but is a step itself, and hid part of
the queue's base: not the cure. The cure is the conform (Q750 for the objects, Q695 for the queue and boundary pieces): the
queue piece is draped vertex by vertex, as above, which the original's frame shows closed.

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

Decoded 2026-09-23 (`docs/QUEUE.md` Q36), put to two refuters. **The address comes from the key's place in the executable's own table, not from the file's order.** Each balance family is one static object whose vtable slot 0 returns record `i` of a table in `.data` (stride `0x3c`: a type dword, then the name) and whose slot 1 (`0x005b0d60`) returns `this + 8`. `FUN_00401030` gives every value record (types 4 to `0xb`) the next dword, starting at slot 1, so record `i`'s global is `this + 8 + 4*slot`. The parser `FUN_004017a0` resolves `Group.Field` through `FUN_00401280`, which searches back from the table's end for the group and then for the field, case-sensitively. Types: 0 section start, 1 section end with the group name, 2/3 an array, 4 int, 5 int ≥ 0, 6 bounded int, 7 float, 8 float ≥ 0, 9 bounded float, `0xa` string, `0xb` a multi-line block, `0xc` the end. A float value (from `0x00401d8b`, Q173) is an optional `-`, then digits with at most one `.`, then whitespace or the line's end, under 100 characters; anything else is a bad line, and whatever follows the whitespace is ignored. It is converted with `atof` and stored as a float (`0x00401e22`).

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
case-sensitive), whose whole-number value is not an optional `-` and digits, whose float is not an optional `-` and digits with at most one
`.`, which is negative in a type-5 key, or which is outside a type-6 key's `[lo, hi)`; a block's rows are its value.
None of the 128 item descriptions in Lost Kingdom's item folders (five category files and `Online_Rides.sam`, and in
the wads 70 own, 50 `Easy_`, 2 `Online_`) holds a refused line. The `Coaster.sam` in `coaster1`, `coaster3` and
`minecart` is not one: it names track textures (`asTextureData[i].pcTextureFilename`), keys this schema lacks.

OpenTPW (Q178b): `Level.InstantAction` gives both the balance's `Easy_Standard.sam` and the catalogue's gate; in it
`ParkItemCatalogue` leaves out an item whose wad has no `Easy_` file and lays the file over the item's own
(`ItemDescriptionFile.Overlay`), 50 items. `Info.NewAttractionDecayTime` falls back to 1. `Upgrades[0].CostOfResearch` is
read (the researched seed, Q201b) and each tier's `WearRate` kept (`ItemDescriptionFile.WearRateAt`, Q303); the
other research keys are read for the lab ("A fresh world's research lab", below). **A refused line leaves out what its file describes - the item,
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
| `FUN_00457a90` | Loads the terrain model, `base.md2` under `%s\Terrain` (`FUN_004504c0`), and the advisor's models (`FUN_00429ba0` on `%s\Global\Advisor` and `%s\Advisor\%s`), then resets the advisor (`Advisor_ResetState`) |
| `FUN_00457c30` | Loads `Base.map` (`FUN_00450900`, `%s\Terrain\Base.map`) and sets the viewport and screen globals |

---

## The park camera

Separate from the lobby island camera. Save module `"SAD_CAMERA"`, chunk magic `'KAME'`, code `0x0042a190`-`0x0042d130`. Persisted state is six globals.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_0042cec0` | — | Loads the camera save block; also persists `gui_CameraFlags` and zeroes the flags immediately after loading them | Decompiled |
| `FUN_0042cdc0` | — | Saves the camera save block | Decompiled |
| `DAT_00790ab0` | `gui_CameraFlags` | Camera flags word, saved under that name | `FUN_0042cec0` |
| `FUN_0042b1c0` | — | The camera-mode dispatcher and update, `gui_CameraFlags`' heaviest consumer (Ghidra `ParkCamera_Update`; its point's edge below, Q625) | Decompiled |
| `0x00748158` | — | The 25-entry `camera` binding table (unnamed) | `FUN_0040cb80` |

    POI default (475, 0, 175)          zoom 110, clamped [70, 130] (a hidden bit widens it to [40, 300], below)
    pitch = lerp(45deg, 65deg, (zoom - 70) / 60), clamped [0, 84.2673deg]
    eye = POI + rotateY( yaw, (0, sin(pitch)*zoom, -cos(pitch)*zoom) )
    eye Y additionally raised by the one-pole-smoothed (alpha 0.5) terrain height under the POI
    projection near 0.1, far 1000.0, angle 90: a 53.13 degree picture top to bottom ("The lens" below)

The projection (0.1, 1000.0, 90) is pushed to `0x00578be0` on the camera `0x0087b110` at `0x0054e86a` (fork watch
2026-10-18, `verify-other-a.md` M3).

**A hidden wide zoom** (fork watch 2026-10-18, `verify-world-a.md` H1). Camera row `0x11` of the `camera` table (row at
`0x00748298`, stride `0x14`: key `0x20`, modifier `0x3f`, handler `0x0042a520` at `0x007482a4`) flips bit `0x200` of
`[0x00790aac]` (`0x0042a520`: `test`, then clear or set, returns 1). By the key decode below (`MapVirtualKeyA`, `0x3f` =
Shift+Ctrl+Alt, fired on release) that is **Shift+Ctrl+Alt+Space**; no code calls the handler and the press has not been
made in a running original (Q510). The zoom clamp (`0x0042c621`-`0x0042c6a1`) runs only with bit `0x80` set (`test bl,0x80`;
clear, no clamp at all) and picks its range by bit `0x200` (`test bh,2`, `0x0042c62c`): clear, [70, 130] (`0x006fde08`,
`0x006fde0c`); set, **[40, 300]** (`0x0074c9d8`, `0x0074c9dc`). The park-load reset `FUN_0042a990` (`0x00457aac`) writes
`[0x00790aac] = ([0x00790aac] & 0x200) | 0x80` (`0x0042aa64`-`0x0042aa75`), so the clamp is armed at every load and the
wide bit survives it; every other writer keeps `0x200` (`and al,0x87`, `0x0042a816`, `0x0042b32b`; `and ecx,0xffffff80`,
`0x0042a827`; `and bh,0xfe`, `0x0042b54a`). In the camera update `0x0042b1c0`-`0x0042c900` bit `0x200` is read only at
`0x0042c62c`, and the pitch comes from the zoom by the same expression, so it extrapolates past 40 and 300 (the [0, 84.27]
clamp above was not re-read there).

**A sprite's camera sector reads the asked rotation** (fork watch 2026-10-18, `verify-world-a.md` M1,
`verify-other-a.md` M1). `FUN_00540ab0` takes the park camera's rotation from `[0x00790a38]`, the **asked** rotation, not
the drawn one: `fmod( .., 2 pi )` (`_CIfmod`, the double at `0x00700e68`), plus 2 pi if negative (`0x00700e64` = 0,
`0x00700e70` = -2 pi), into `0x00876b24`. When `[0x00790ab0] & 0x16` (a free-look camera mode) it takes `FUN_0046f1c0(
[0x00790a98] )` instead: `atan2( m+0x20, m+0x28 )` while `-0.9 <= m+0x24 <= 0.9` (`0x006feb78`, `0x006feb7c`), else
`atan2( -m+8, m+0 )`. `FUN_00542010` (`0x0054218b`) turns it into the sector `8 - (n & 7)` with
`n = trunc( (rotation - (-pi/8)) x (1 / 2 pi) x 8 )` (`0x00700e74`, `0x00700e78`, `0x00700e7c`; `__ftol` truncates a value
that is not negative). The drawn direction is `(sector + heading) & 7`, the heading being `((angle - 0x380) & 0x7ff) >> 8`
(`0x004fa08a`), and mirrored as `8 - d` past the slot's last direction (byte `+0x223`). **A draw whose flags word has
`0x40000` gets sector 0 with no camera term** (`0x005421cc`-`0x005421e1`), and one with `0x2000000`, or a direction byte of
0, gets direction 0 (`0x0054220f`); which things carry `0x40000` is not read.

**Measured** (Q435, `original/camera.py`, twice): at point (484.442, 0, 259.319), zoom 110, rotation 0 the camera's
matrix `0x0087aff0` holds the eye in its last row, (484.44, 93.62, 201.57), the formula's exactly, and its forward
(0, -0.851, 0.525) runs through the point, so the point is the frame's middle. The draw matrix `[0x0087b088]` there
gives x/w = 1.5 tan and y/w = 2 tan of the angle off the eye's axis: 768 pixels a unit of tangent down a 768-high frame,
53.13 degrees top to bottom (`park.md`, the `0x0087b0d0` scale). OpenTPW at the same numbers has the same eye and,
since Q441, the same lens: its 1280 x 720 frame's middle 960 x 720, scaled by 768/720, lays on the original's whole
frame (`q441/side-by-side.png`).

**The turn** (Q440, decoded and measured). The update builds the eye's offset as (0, sin p x zoom, -cos p x zoom)
(`0x00790aa0` nought, `0x00790aa4`, `0x00790aa8`) and turns it by the drawn rotation `0x007909e0`: x' = x cos r + z sin r,
z' = z cos r - x sin r. So **eye = point + (-h sin r, sin p x zoom + ground, -h cos r)**, h = cos p x zoom. Written
into the running original at point (495, 285), zoom 110 (`original/setcam.py`), the camera's matrix read back the eye at
(495, 227.253), (437.253, 285) and (495, 342.747) at rotations 0, 90 and 180, the formula's to three places. OpenTPW's
`EyeAt` swings the other way (point + h sin yaw), so **the original's rotation is OpenTPW's yaw negated**; at
`camera 495 285 110 -R` the same named cells stand at the same pixels of the two pictures through the lens law at all
three (`q440/side-by-side.png`). The rotation asked for is `0x00790a38`; the one drawn, `0x007909e0`, eases towards
it by min(dt, 140) / 140 a frame, or min(dt, 200) / 200 with `[0x0078d912]` set (`0x0042b76d`..`0x0042b7d7`, `0x0042bb41`..`0x0042bb93`; constants 200 and 0.005 at `0x006fdd9c`, `0x006fddcc` against 140 and 1/140 at `0x006fddd0`, `0x006fddd4`), and snaps within 0.0005; both are kept in 0..2 pi. The Left arrow (camera table
action 6) and keypad 9 raise the rotation (`[0x00790aac]` bit 8, `0x0042b355`), 0.002 a millisecond held, or a
quarter turn a press with `[0x0078d912]` set; the Right arrow and keypad 7 lower it. The three scroll terms
`0x00790a88`..`0x00790a90` carry a pan's momentum; the game's own snap (`FUN_0042aab0`) noughts them.

**The point of interest's edge** (Q625, decoded and measured). Nothing clamps the orbit's point `0x007908f0`/`0x007908f8`
either: the soft bound on the walker ("Nothing clamps the position", under "Walking on the ground") is the same block,
`0x0042bc2a`..`0x0042bd65`, run after both the orbit's and the free-look's input, so it holds the orbit too.
- **The pan's input.** The keypad's 8, 2, 4 and 6 (camera rows `0xd`, `0xe`, `0xf`, `0x10` of `0x00748158`, keys `0x68`,
  `0x62`, `0x64`, `0x66`) set the forward and across inputs to +1, -1, -1, +1; otherwise each is the pointer's place,
  -1 to 1 across the frame (`0x00790ac8`, `0x00790acc`), when the push scroll applies (the follow's list above). Each
  counts only past 0.95: `(input - 0.95) x 10` (`0x006fddc0`/`0x006fddc4`, `0x006fdd7c`), so a key is 0.5. The arrows do
  not pan: Up and keypad 1 (rows 0, `0x15`) take the zoom in by 0.05 a millisecond (`0x006fddbc`), Down and keypad 3
  (rows 1, `0x16`) out, Left and keypad 9, Right and keypad 7 turn (`0x0042b32d`..`0x0042b3d5`).
- **The speed.** The inputs are turned by the drawn rotation, `vx = x cos r + f sin r`, `vz = f cos r - x sin r`
  (`0x0042bbe6`..`0x0042bc28`), and each scroll term becomes `(sqrt( zoom ) x 0.02 x input + term) x 0.5` a frame
  (`0x006fddf8`, `0x006fddfc`): a held key settles at `sqrt( zoom ) x 0.01` units a millisecond, 104.9 a second at zoom
  110, 83.7 at 70, 114.0 at 130. With no key held and the pointer not past 0.95, each term is divided by `1 + 0.01 dt` a
  frame (`0x006fddc8` = -0.01, `0x0042b73d`..`0x0042b78f`); with a pan key held, an axis with no input halves a frame
  (`(0 + term) x 0.5`, `0x0042bc60`..`0x0042bc88`, no `dt`). All three terms are noughted together, once all three are under
  0.0001 (`0x0074c988`; `0x0042bd69`..`0x0042bdbc`). The orbit's point moves `dt x term` (`0x0042c5ed`..`0x0042c61b`); `dt` is the frame's milliseconds,
  100 past 200 and 1 at or below nought (`0x0042b2c3`).
- **The edge.** Before the move (`0x0042b668`..`0x0042b716`) the point is flagged past x < 0 (1), z < 0 (4),
  x > `+0x18 x +0x10` (2) and z > `+0x1c x +0x14` (8) of the heightfield `[[0x007a0854] + 0x6c]`: 960 and 850 in Lost
  Kingdom, not the 128-cell map. Where flagged and the new term points further out, the term becomes the frame's old one
  divided by `1 + 0.01 dt` (`0x0042bca0`..`0x0042bd5f`): the push is dropped and the coast decays. Nothing pulls the point
  back, and a pan inward is free. So a held pan stops `100 x |term|` past the edge plus up to one frame's step: 10.5 to
  14 units at zoom 110 and 30 frames a second, in the second cell out (x 97 or -2, z 86 or -2, counting by floor; the
  original's own cell is `(int)( x x 0.1 )`, which truncates, `0x0042c545`, so -11.989 is its cell -1 and the high edge's
  97 and 86 are the same either way; at zoom 70, 8.4 to 11).
- **Measured** (Q625, `original/campoi.py` on the stock park, zoom 110, `q625/orig/a-*.log`, `b-*.log`): keypad 4 from
  x 40 stopped at -11.989; keypad 2 from z 40 at -12.806; keypad 6 from x 920 at 973.517 (and at 970.643 and 973.435 with its release lost); keypad 8
  from z 810 at 863.621; keypad 4 at rotation 90 from z 810 left by z, at 862.013. The held term read 0.10488 a
  millisecond. Released past the edge, the point stayed there. Started past the edge (x 1240), keypad 6 moved nothing.
  Past the edge the original draws flat grass (`q625/orig/b-right-2-after.png`); what that ground is was not read; this game draws the same there (Q667).
- **The arrows driven by key** (Q631, `q631/orig/run.sh`, `original/camrate.py`; the stock park at (480, 400), zoom 110,
  the follow off): Up held went from 110 to 70 at 0.0513 a millisecond and stopped there, Down from 70 to 130 at 0.0519;
  with the reference player's 90 degree option set (`[0x0078d912]` 1) Left raised the asked rotation by 90 once and
  Right lowered it by 90; W and D moved nothing; the point stood through all six. The table's keys, read from the exe
  (`lib/bindtables.py`), by action: 0 and 1 `0x2600` Up and `0x2800` Down, 6 and 7 `0x2500` Left and `0x2700` Right,
  `0xd` to `0x10` the keypad's 8, 2, 4 and 6, `0x15` and `0x16` keypad 1 and 3, `0x12` and `0x13` keypad 9 and 7, `0x14` keypad 5
  (`0x0065`) and `0x18` Home (`0x2400`), both the reset ("The camera's reset", below). No row of the six tables binds
  D, E, J, K, M, N, O, Q, U, W, X, Y or Z with no modifier.
- **OpenTPW** (Q631, `ParkOrbitCameraMode`): the keypad's 8, 2, 4 and 6 pan, and so do W, S, A and D, its deviation
  (`PanInput`; all-items and all-staff move to O and E, `ParkFrontEnd.ShortcutRow`, and the help bar's nine rows say so,
  `HelpBar.OurKeys`); each only with no modifier held. A held pan settles at `sqrt( zoom ) x 10` a second, the term eased
  up by half every 31 ms and coasting out at 10 a second, nought under 0.1 (`PanTarget`, `StepScroll`), and ends a
  locator's follow (`Panning`). Up, Down and keypad 1 and 3 zoom at 50 a second. The edge is the original's rule
  (Q667, `StepPan`, `BoundScroll`, `EdgeOf`): flagged by the point before the move, per axis, strictly past 0 or the
  heightfield's cells times its cell size (960 by 850 in all four themes), an outward new term replaced by the old one
  coasted, by the original's own step, the term over `1 + 0.01 dt`, kept on `Time.Delta` (Alexah, 2026-10-26; the no-key coast
  is the same step), so a pan held at zoom 110 coasts 100 times its term, 10.49, past the edge plus part of its crossing frame,
  in the original's cell at any frame rate (30, 60 and 144: `AHeldPanRestsInTheSameCellAtAnyFrameRate`); the log says `Camera: pan held at
  the edge` once and `Camera: pan at rest, point (x,y) cell (cx,cy)` as the coast ends. Past the edge, at the stop, this
  game draws what the original does at all four edges (`q667/side-by-side.png`: the original stopped at 972.828,
  -13.622, z 863.576, z -12.014, `q667/orig2`); Q625's flat cyan was at x 1727, beyond that ground. Keypad 9 and
  7 do not turn, and the turn's step is not the original's (Q23). Keypad 5 and Home reset the camera ("The camera's reset").

**The camera's reset** (Q690, decoded and measured). Camera rows 19 (keypad 5, `0x0065`) and 24 (Home, `0x2400`), modifier
0, both hand `ParkCamera_ResetKey` (`0x0042a500`, made a function in Q690), which sets bit `0x100` of `[0x00790aac]` and
answers 1; like every handler it runs from the key-up (`FUN_0040c990`, "The original fires its shortcuts on key RELEASE").
The update answers after the zoom keys and before the pan (`0x0042b518`..`0x0042b561`): zoom `[0x007909ec]` = 110, the
asked rotation `[0x00790a38]` = 0, the point `0x007908f0`/`f4`/`f8` = 475/0/175 (the floats at `0x006fdd40`..`0x006fdd50`),
the bit cleared and `gui_CameraFlags`' follow bit `0x1000` with it (`and ch, 0xef`). It writes neither the scroll terms
`0x00790a88`..`0x00790a90` nor the drawn rotation `0x007909e0`, which eases to nought as "The turn" says.
- **Measured** (`original/campoi.py` on `q325/stock.TPWS`, `q690/orig/run.sh`, frames `q690/orig/a-*.png`): Home held
  1.0 s from point (600, 300), zoom 90, rotation 135 moved nothing until its release, then read (475.000, 175.000), zoom
  110, asked rotation 0, flags 0; the drawn rotation eased 135 to 0 in 1.42 s. Keypad 5 the same from (300, 600), zoom
  125, rotation 270, its drawn rotation going 270, 222.75, 151.47 ... 0 in 1.57 s: down through 180, the long way round,
  as both rotations are kept in 0..2 pi. With keypad 6's term still running (its release lost under Wine), Home put the
  point at 475 and the pan carried it on (481.712 at the first read, 763.1 three seconds later), z 175: the terms stand.
- **OpenTPW** (`ParkOrbitCameraMode.ResetReleased`, `Reset`, `WasReset`): Home or keypad 5 let go with no modifier,
  behind the same gate as the other camera keys (no park screen, map, message box or file screen, no text box), sets
  zoom 110, yaw 0 and the point (475, 175) before the pan's step, the pan's term kept; the log says `Camera: reset by
  key, point (x,y) yaw Y zoom Z -> (475,175) yaw 0 zoom 110, scroll S a second kept`, and a locator following lets go at
  its next frame (`Park locator: the camera's reset ends the follow of thing N`). **A deviation:** the yaw is set at
  once, as every turn here is (Q23), where the original's drawn turn eases over about 1.5 s.
- **Reached in the other modes too** (read from the listing, never run). The update answers the bit in every mode it runs
  in (`0x0042b3fc` for the orbit, `0x0042b40c` for a ride view's flags `0x3c`, `0x0042b46c` after the walking keys), and in
  first person the viewfinder layer's `FUN_00488a00` runs the camera table on the key-up (`0x00488af8`). So Home or keypad
  5 let go in first person puts the viewer (`0x007908f0`/`f8`, the walker's own place) at (475, 175), asked rotation 0, zoom
  110, at once; leaving first person puts the orbit's saved point and yaw back (`0x0042af8c`) but not the zoom, so the
  orbit comes back at 110. OpenTPW counts it, `CAMERA_RESET_FIRST_PERSON`, in `ParkCamcorderCameraMode.Update` behind the
  orbit's gate, and moves nothing. **The ride view's arm is unsettled:** it is reached at `0x0042b40c`, and by the
  listing neither the view's entry nor its exit saves or restores `0x007908f0`, so the orbit would come back at (475, 175),
  and back in first person the ride under the moved point is looked up again (`0x0042a340`); what the player sees was not
  traced or run, and the End's view, the only ride view this game reaches, hides both layers and takes keys in its own
  handlers (`0x0048a740`, `0x0048a970`), not read.

The camera persists a *saved* POI and *saved* Y rotation as well as the *required* ones — the shape of a camera that leaves its position and is put back (camcorder mode does exactly that).

Terrain-following is real, and the sampler is `FUN_00452560( x, z, 1 )`. With `gui_CameraFlags & 0x16` clear (`0x0042c6c4`) the update `FUN_0042b1c0` calls it on the look-at point (`0x007908f0`, `0x007908f8`) at `0x0042c817`, adds the last answer (`0x00790ae4`), halves the sum (0.5 at `0x006fddfc`), stores it back and adds it to the eye's height (`0x00790aa4`). `FUN_00452560` interpolates the landscape's corner heights (`+0x28` of `[0x007a0854] + 0x6c`) and answers nought off the map; it is not `FUN_004527f0`, which no camera function calls.

### The follow

`gui_CameraFlags` bit `0x1000` is the whole of it (Q440): set, the locator's frame handler `FUN_004b3640` puts the point on
the locator's person `[0x007cc308]` every frame through `FUN_0042aab0` (`hud.md`, "The staff and visitor locators"), so a point written
then lasts a frame; cleared, the camera stands where the follow left it.

- **Set** at two sites: `FUN_004b3f60` (`0x004b4078`), a person handed to the locator, from Ctrl+S or Ctrl+V with a
  person's window up and from the windows' binoculars (the staff window's `FUN_004b5cb0` case `0x521`, the visitor
  window's `FUN_004b7720` case `0x3737`); and `FUN_004b3790` (`0x004b3b4b`), the locator's button finding somebody.
- **Cleared** by the locator folded (`0x004b3806`) or finding nobody (`0x004b3b16`); by the camera's mode change
  (`FUN_0042ab20`, `0x0042ae4c`; its `and dl, 0x41` and the store, `0x0042ab6d`..`0x0042ab72`, are inside it); by the camera's reset at a park's load (`FUN_0042a990`, `0x0042a9be`, flags nought, called from `0x00457aac`); by the reset key (`[0x00790aac]` bit `0x100`); and by **any pan of the orbit camera**
  (`0x0042ba37`, `0x0042ba92`): the keypad's 8, 2, 4 and 6 (camera table actions `0xd` to `0x10`), the right button's
  drag with `[0x0078d910]` set (its clear at `0x0042b961`), or, with `[0x0078d910]` clear (the update zeroes the pointer's terms when it is set, `0x0042b948`..`0x0042b993`; an Options toggle, written at `0x004a3882`, control `0x1d4c9`), the pointer past 0.95 of the way to an edge while the game is the active
  application (`[0x007a1a14]` bit 8, from `WM_ACTIVATEAPP`) with `gui_CameraFlags` bit `0x100` clear (set by the
  camera's messages `0x10002` and `0x22`, cleared by `0x10001`, the pointer's move). Measured: the pointer resting on the frame's right edge for a second cleared
  the bit and left the point 83 units from the member it had followed.
- **A person's window and the follow never run together.** Opening any park screen (`FUN_0048cea0` ->
  `FUN_00485b70`) folds the gadget's arm (`FUN_004a25f0( 1 )`), which closes the locator and clears the bit; and
  `FUN_004b3f60` ends by closing the open park screen (`FUN_00485b40`), so the binoculars put the window away. Measured:
  Mike Cooper's window opened from the strip left the bit clear and the point still while he walked; his binoculars
  closed the window and the point stood within 1.6 units of him from then on.
- **Written from outside**, clearing the bit stops the follow and setting it starts it again on the locator's person:
  `setcam.py PID off` left the point where it was for 5 s while the member walked 34 units; `on` put it back within
  1.8 units at the next read. With no person on the locator the bit does nothing.

### The lens: 90 handed to the projection, 53.13 drawn

The angle every view hands the projection builder is not the picture's angle. The draw matrix multiplies a fixed
screen scale in after the projection, and that scale narrows every view by the same law.

| Address | What it is |
|---|---|
| `FUN_00578be0` | The projection builder (`ECX` = `0x0087b110`; near, far, angle): `m00 = m11 = cos(a/2)`, `m23 = sin(a/2)`, `m22 = sin(a/2) / (1 - near/far)`; it keeps the three in `DAT_0087b1dc`, `0x00768acc`, `DAT_0087af58`. Its `DAT_008bcbc8 & 0x200` branch is orthographic; the flags word is `0x00410100`, so **the park is perspective** |
| `FUN_005781d0` | Render start (`FUN_00572780`): the screen scale `0x0087b0d0` = diag( `2 x DAT_008bcbcc`, `-2` (`0xc0000000` at `0x0087b0e4`), 1, 1 ); nothing writes it after |
| `FUN_005783b0` | The draw matrix `[0x0087b088]` (`0x0087b158`) = the camera's matrix `0x0087aff0` inverted, times the projection, times the scale. The one path for every view |
| `FUN_00578ca0` | Calls the builder again with the kept angle and a new far. Three call sites: `0x0056bd3e` in `FUN_0056bb00`, and `0x0056eac0` and `0x0056ed79` in `FUN_0056ea90`, which hands it 1000.0, then the depth the cell scan found (`[esi+0x1c]`, 100.0 at `0x0056ead3`, raised as cells are scanned). On the `FUN_0056bb00` path the far is the farthest of the four view rays' ground points from the camera, at least 10.0 (`0x0056bc5c`..`0x0056bc9f`), each ray's parameter capped at `[0x008bd4c0]` / 2 (`FUN_0056b940`): at the wide zoom 300 (Q510) a capped corner point leaves the ground and the hull shrinks, so black may show at the frame's top (arithmetic, not seen; fork watch 2026-10-27, verify-other L1). 460 / `[0x008bd4c0]` is only the scan's range around the eye, never the far; the far measured (126, 128, 112) is neither |
| `FUN_0056b790` | The picture's frustum, made straight after the builder at `0x0042aa5f`, `0x0054e884` and `0x005d8cda`: it multiplies `tan(a/2)` by 0.5 (`0x0070149c`), so its corner rays are `(+/-0.5 tan(a/2) / DAT_008bcbcc, +/-0.5 tan(a/2), 1)`, (+/-0.667, +/-0.5, 1) at 90, the 53.13 x 67.38 picture itself; it writes `[0x00879d54]` |
| `FUN_0056bb00` | The per-frame frustum (from `0x00576add` when `[0x008bd508] & 0x100` is clear, `0x00576aba`), unscaled: corner rays `(+/-tan(a/2) / DAT_008bcbcc, +/-tan(a/2), 1)`, the projection's own 90 degrees, a volume wider than the picture (harmless: it culls less) |
| `FUN_0045bf90` | Screen to world: its corner ray is built from fixed numbers (`0x0074c9b8` pi/2 x `0x0074d13c` 0.885 x 0.5 = a 39.8 degree diagonal, split by `atan( 0.75 )`, `_DAT_006fe640`): half-tangents **0.667 across, 0.500 down**, the park's picture |
| `DAT_008bcbcc` | 0.75 in the running original at 800 x 600 and 1024 x 768 (written through the render struct `0x008bcbc8`; every mode of `FUN_00449ec0` is 4:3 bar 1280 x 1024) |

A point's `x / w` and `y / w` through the draw matrix are the screen as -1 to 1. So a view handed angle `a` draws a
picture whose half-tangent is `tan(a/2) / 2` top to bottom and `tan(a/2) / 1.5` side to side, a plain 4:3 lens of
`2 atan( tan(a/2) / 2 )` vertically. The builder's callers:

| Caller | Angle | Picture, top to bottom x side to side |
|---|---|---|
| The park camera's reset `FUN_0042a990` (pi/2 x 180/pi, `0x0042aa2f`) | 90 | **53.13 x 67.38** |
| `Window_Create` `0x0044e277`, `Game_StateMachine` `0x0054e874` (`0x42b40000`) | 90 | 53.13 x 67.38 |
| The lobby's render camera (the global `[0x00f83778]` points at it; vtable `0x00702a90`, slot 2 `0x005d8cb0`), its `+4` | 115 at `FUN_005d8b50`, then `ISLANDFOV` | 100: **61.58 x 76.93** (`lobby.md`, "The lens") |
| `Lobby_Start` `0x005dcfe0` (the globe page's camera), the same `+4` (60.0, `0x42700000`, at `0x005dd034`) | 60 | by the law 32.2 top to bottom; not measured |

Nothing in a park calls the builder after the reset but `FUN_00578ca0`, so the camcorder's first person draws with
the orbit camera's lens. **Measured** (Q260, `original/lens.py`): the lobby's angle 100.000 (its camera's `+4` the
same), `m00` 0.64279, `m23` 0.76604, scale 1.5 and -2, aspect 0.75, so 61.58 x 76.93; the park's 90.000, `m00` =
`m23` = 0.70711, the same scale, 53.13 x 67.38; the camcorder's pick and its first person the same 90 (the far 126,
128 and 112 as the fog moved). Two cells named at one camera (point (494.975, 192.002), zoom 110, rotation 0) stood at
702,254 and 281,489 of the original's 1024 x 768 frame and at (729, 299) and (532, 409) of OpenTPW's 1280 x 720, which
is (640, 360) + (the original's - (512, 384)) x 360 / 768, each on its named cell at the law's pixel (a cell is about 25 px here; the same-lens pixel lies 90 px or more away): OpenTPW's picture was 90 degrees then, twice the
original's tangent (`q260/side-by-side.png`, `q260/overlay.png`). Rotations 0, 90 and 180 (Q440, `q440/side-by-side.png`).

**Why it was read as 90 before.** The builder alone writes `m00 == m11` with no aspect term, and the culling frustum
is the 90 degree one, so "the culler must frame what is drawn" made 90 the vertical angle. The screen scale breaks
that: the culler frames more than is drawn. Of the two readings of the original's camera at the same numbers, "further back"
(Q257r, repeated by Q264) was the Load Park list's view, 65 units on (Q359), and "closer" (Q357) was the lens: the eye is the
same (Q435), and the original's frame showed the middle half, each way, of what OpenTPW's old 90 degree lens showed, so everything stood twice as large in it (until Q441).

**OpenTPW draws the same lens** (Q441). `Camera.DrawnAngle( a )` is the law, and a camera mode's `FieldOfView` is the
picture's vertical angle at 4:3: both park camera modes take `DrawnAngle( 90 )`, 53.13 (`ParkOrbitCameraMode.Lens`), and
the lobby `DrawnAngle( ISLANDFOV )`. At 4:3 the projection is the original's draw matrix, x/w 1.5 and y/w 2 a unit of
tangent, and the pointer's ray reaches 0.667 across and 0.5 down a unit ahead, `FUN_0045bf90`'s figures; a wider window
keeps the height and sees further to the sides. The log says each mode's lens once (`Camera: ParkOrbitCameraMode lens
53.13 high at 4:3, across 0.6667 there and 0.8889 at 1280 x 720`). Measured (Q441, at point (495, 285), zoom 110,
rotations 0, 90 and 180): the original's cells at 512,588 and 677,588 stood under 640,551 and 795,551 of the 1280 x 720
window, (640, 360) + (the original's - (512, 384)) x 720 / 768, on the first probe; the camcorder at the original's
first-person eye (495.55, 5, 284.35) draws its gate at the same size and place (`q441/camcorder.png`). The culler and
the far plane are not the original's: OpenTPW culls nothing on the processor and its far plane is 10000.

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

**The qualitative signal is withdrawn.** "The original shows no sky and OpenTPW does" is explained by **content and lens** (Q260: OpenTPW's old 90 degree picture reached the horizon at pitch 45, the original's top edge never does, "The ORBIT camera never shows the sky" below): the original parks are full of rides, shops and trees that stop the eye at the horizon, and OpenTPW's park, when this was measured (2026-09-13), was bare ground plus `base.MD2` scenery. It was no measure of the angle: content and lens were mixed in it.

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
| `FUN_00585720(this)` | The rebuild: a 16x16 drooped grid plus a 16-column skirt, and for each of the 16x16 vertices a random grey `c = (rand() & 0x7f) x 0x10101 \| 0xff808080` (the CRT `rand`, `0x0067b5c0`) written to all four colour tables (`+0xf60`, `+0x1360`, `+0x1760`, `+0x1b60`) (fork watch 2026-10-28, verify-hz-world H3) |
| `FUN_00585ce0(this)` | Samples `sky_rgb` on a 16x16 grid into the same four colour tables: at surface format 2 each channel `n<<4\|8`, alpha 0xff, at format 3 copied, at formats 0 and 1 nothing; called only by the loader `FUN_005852b0` (`0x005854de`) |
| `FUN_00584ef0(this)` | The object's init — centre (0,0), height 300 (`0x43960000`), extent `0x960` |
| `FUN_005856c0(this)` | Puts centre and height back to those defaults, then tail-jumps to the rebuild (`0x005856d8`; the lobby's teardown calls it, `0x005d8dcf`) |
| `FUN_00572780` | Engine init; the step printing `" (tload/rain/sky ok)"` (it calls the rebuild at `0x005729b5`; the rebuild's other callers are the setter and `FUN_005856c0`'s tail jump) |

**The texture loader has two callers**: the lobby's `FUN_005d8b50` at `0x005d8bac`, which passes `Data\Levels\fantasy` and then centres at (0, **180**, 0); and a park's level load `FUN_00407f20` at `0x00407f95`, reached from state 9 through `FUN_00407e00` at `0x0054ed3f` (which also loads `\Scape.omp`). **So a park does load its own sky art.**

Every theme ships `sky/` with those three files and an `ssky/` low-detail set beside it (4,140 bytes against 262,188 — that belongs to SKYQUALITY, not here). Hallow and space also ship `moon.tga`/`moon2.tga`: those are the global `Standard.sam`'s `SkyObjects[n].Filename` entries, and jungle and fantasy ship none.

**Nothing re-centres the sky for a park.** The setter's only caller is the lobby, and the rebuild's other callers are engine init and `FUN_005856c0`'s tail jump (`0x005856d8`, the lobby's teardown `0x005d8dcf`; fork watch 2026-10-28, verify-hz-world H3). A park's sky therefore sits at the object default: **centre (0,0), height 300**.

**A park's sky colour comes from its own art, not from a key.** `SKYCOLOUR(r,g,b)` is parsed only by the lobby's island-script reader `FUN_005e3210` — which also handles `LIGHTNING(n)` and `RAINY(n)`, storing them into the island object (`FUN_005dfa60`) at `+0x64`/`+0x68`/`+0x6c` as `value << 16`, `+0x5c` and `+0x60`. **No theme balance file carries a sky colour key at all** (checked in all four). The colour is `sky_rgb.tga`. In the lobby the rebuild that follows the load (setter `0x005d8bd9`) overwrites all four colour tables with random greys 0x80 to 0xff (`FUN_00585720`), so the lobby's three untinted layers are those greys and `sky_rgb` never reaches its screen; a park's load (`0x00407f95`, state 9) samples `sky_rgb` after the rebuilds, so a park keeps it, unless the lobby's teardown runs after state 9's load (order not read: a live read settles it). Layers: `DetailFile_Apply` (`0x0044d9f0`..`0x0044da17`) copies `GraphicalOptions.SKYQUALITY` to sky `+0x2ec8` for 0, 1, 2 and 4 only; `FUN_005863c0` returns at once for 0 (no clouds, no band), else the layer count is that value (1 when render word `0x8`); each layer's colour is `(+0x2f0c - k x +0x2f10) & mask & table k`, alpha 0xcf, 0x97, 0x5f, 0x27. Render word `[0x008bcbc8]` bit `0x2000000` (band coordinates 0.19/0.01/0.49 and the mirrored dome) is written only by `Lobby_Start`, the globe page's start (`0x005dd072`, cleared at `0x005dd2e2`, `0x005dd762`, `0x005dda22`), so the island pages most likely draw the default band, as a park does (not read live).

**The ORBIT camera never shows the sky.** Its pitch is 45-65 degrees against a picture 53.13 degrees high ("The lens"), so the top of the frame sits at elevation `26.57 - pitch`, always below the horizon. All three reference screenshots agree: ground to every edge. **Camcorder mode does show it**; from the ground the sky fills the upper half of the frame. The argument above is about the orbit camera, and for that it holds exactly.

What OpenTPW's orbit camera shows past the ground's edge is the **fog-cleared background**, measured at exactly jungle's `ThemeEngine.FogColour` **4774136** (`0x48D8F8`) with **one unique colour and zero standard deviation** across the strip. Not sky.

**Bit `0x80` set by `FUN_0042afd0` is NOT the first-person flag.** Its only caller is `FUN_005d8de0`, a look-at basis builder (it normalises a direction, derives right/up, stores a position) reached from the **lobby's** `FUN_005d8b50` — so `0x80` of `DAT_00790ab0` marks a **scripted lobby camera**. `FUN_005d8de0` also aims the lobby's camera-following light every pass: with the light and `+0x24` set, P = eye + right x `+0x2c` + up x `+0x28`, L = target - P, with L.y = 0 when `+0x30`, normalised, and when `+0x30` L.y -= 0.3 (`0x00702a48`) and normalised again, written to the light's `+0x2c..+0x34` (`ui.md`, "The lobby's light"; fork watch 2026-10-28, verify-hz-world H1). The camera-mode dispatcher worth reading instead is `FUN_0042b1c0`.

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
| `0x004033a0` | — | Game-clock step: `+0x18 += (raw - +0x10) * +0x20` in x87 | Disassembly (`0x004033a6`..`0x004033cb`) |
| `0x00785970+0x18` (`0x00785988`) | — | The accumulator (double). Read live: 558,586,560 at 6.5 days of uptime, a multiple of 64 | `/proc/pid/mem` |
| `0x00785970+0x20` (`0x00785990`) | — | Scale (double), 1.0 in a park | `/proc/pid/mem` |
| `0x005f5f10` | — | Raw clock: `(QPC - start) / divisor + offset` by 64-by-32 `IDIV`; `timeGetTime()` when QPC fails | objdump |
| `0x00fa71e0` | — | Its object: `+0x0` QPC flag, `+0x4` divisor (10000 for a 10 MHz counter), `+0x8` offset (`timeGetTime` at start-up), `+0x10` start QPC | `/proc/pid/mem` |

**The consequence:** the raw clock is milliseconds since boot, and so is the accumulator. The accumulator is added at 24-bit precision (measured), so it can only
hold multiples of 2^(e-23), where 2^e ≤ its value: 64 ms from 6.2 to 12.4 days of uptime. Which code leaves 24-bit in
force there is not settled: a Direct3D without `DDSCL_FPUPRESERVE` was the first guess (Wine does the same), but the
running original read control word `0x27f`, 53-bit, at a park tick (`advisor-park.md`, "The product is a double of the
float key"), and the renderer's own switch is `0x00591220` with a failure exit that restores nothing (`0x00577433`;
"Which rounding is live is not settled" below). Both states occur (fork watch 2026-10-18, `verify-world-a.md` H2). A frame's time much
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

**Every SCREEN-DRIVEN call site passes `PUSH 0x0; PUSH 0x0`** — seven of them (`0x0047f26c`, `0x0049f29f`, `0x004a93d7`, `0x0048c87e`, `0x0049efba`, `0x004a3a6c`, and the map window's `0x005f0b7f`: its `EBP` is nought, `xor ebp,ebp` at `0x005f0b6c`; fork watch 2026-10-27, verify-ui-hud) — so for a menu, a message box or the options screen the quiet flag is 0 and the sound half of a pause is `Advisor_PauseVoice()` + `FUN_0051c1c0(1)` (which only writes `DAT_00803ad2`). **So a park's music keeps playing under the menu.**

**But the window procedure passes `(1, 1)`.** `FUN_0046b600`'s `WM_ACTIVATEAPP` branch calls `Game_Pause(1,1)` at `0x0046b74c`, and arg2 non-zero takes the suspend-all path `FUN_0051bcf0` (the pair of the next paragraph) and never touches the listener. So that path **is** taken offline, on **alt-tab**. The advisor-voice pause is the other branch, arg2 nought (`0x004092d1`..`0x004092e8`: `0x00598960` pauses his one voice, then `FUN_0051c1c0(1)`).

`FUN_0051bcf0` / `FUN_0051bd30` post commands differing only in payload address (`0x0070a268` vs `0x0070a270`), whose handlers `0x006b8e40` / `0x006b8eb0` are byte-identical but for one operand — `CALL [EDX+0x38]` against `CALL [EDX+0x3c]`, two adjacent virtual slots on the same sound object. That shape is a **suspend-all / resume-all pair**. Which matters for more than accuracy: **the original owns a primitive that holds the whole mix, spends it on losing focus, and pointedly does not use it for the park menu** — the menu takes the listener branch instead. That is the best evidence available that a park menu was never meant to silence everything. The advisor's voice has its own pause (the arg2 nought branch above): per-voice sound commands 2 and 3 through `0x00598960` -> `0x0051c060` -> `0x006b5bd0` -> `0x006b5c50` -> `0x006b8a10` to the voice's slot `+0x18`, `0x006bcab0`, the resume pushing the voice's start on by the paused span (`0x006bcafd`..`0x006bcb46`; `audio.md`, Q577; fork watch 2026-10-27, verify-ui-hud M1).

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

**A sweep that cannot run is dropped, not owed.** The baseline and the step counter are moved before the step's first branch (`0x0054f4c7`, `0x0054f4d6`; the window's `TEST` at `0x0054f4d4` sits between the counter's `INC` and its store, and its `JNZ` at `0x0054f4dc` follows both), so nothing makes a lost sweep up later. At most three sweeps run in a rendered frame: `[0x00879064]` counts them (`0x0054f680`, `0x0054f696`), is reset each frame (`0x0054fc2a`) and in the routine the park's load registers at `0x0054ecbc` (`0x0054e32f`), and a step past the third jumps to `0x0054f828`, which still runs `0x0055a470` and the every-32nd block. The loop keeps no more than 2 s of backlog (`0x0054f49b`). And an inactive full-screen window skips each whole step (`0x0054f4d4`-`0x0054f4e5`; `weather.md`), which in an ordinary park, paused on losing focus (`0x0046b74c`), costs only the steps still owed. The every-30th test at `0x00516453` and the every-100th at `0x004d7b4f` count sweeps too. **OpenTPW** (Q126): `ParkPeople.OnUpdate` counts the frame's sweeps and drops a step that comes due with `SweepsAFrame`, three, already run; the tick number moves on and `ParkState.GameTick` does not. Measured, the game's process stopped 2.5 s and the park clock read before it and half a second after, three times each: the original `+5`, `+5`, `+5` (`mGameTick` at world `+0x1da70c`); OpenTPW `+5`, `+5`, `+6`, its log "3 run and 5 dropped in a frame of 64 ticks"; before the cap `+10`, `+11`, `+10`. The sweep is all the skip passes over: between `0x0054f68f` and `0x0054f828` are a first-use guard, the game mode's dispatch (`[0x00fb3b7c]`: the online mode's sweep goes through `FUN_005166b0`) and a timing ring around the call. `[0x00878a1c]`, the peeps' drawing baseline, is moved before the test (`0x0054f683`), so a dropped step still restarts the interpolation, as `ParkPeople.ThingTickFraction` does. The scripts and the sprites, which run before the gate, are capped in neither game. **Still parted**: the original's calendar, weather and day's, month's and year's work are inside the sweep (`FUN_004d7b20` at its end, `FUN_00512880` from the thing turn) and are dropped with it, and here the calendar reads `ParkState.GameTick` (Q149), so it drops them too, but the day's, month's and year's work run once a frame after the frame's sweeps; the tick number handed to the things jumps over a dropped step (Q132); and `FUN_0055a470`, which runs on every eighth step dropped or not and picks a sound setting by the ride the camera is on, is not built (Q157).

**The music's level** (Q129, the three instructions re-read first-hand). On a step whose counter has its low five bits nought (`TEST [0x00877d34],0x1f`, `0x0054f82d`), every 992 ms of game time and never while no step runs, the loop takes the crowd's level (`FUN_004c81e0`, half the counted guests, to 100), holds it to 89 (`CMP EAX,0x59`, `0x0054f84e`), makes it nought while the world's state `+0x1da738` is 4 (`0x0054f860`) and hands it to `FUN_0051e790`, which sets parameter 4 of the music's voice (`FUN_0051bc40`, into the sound library through `FUN_006b5b80`'s virtual call). **The level picks the music's variation and never its loudness** (`audio.md`, "The music's class": the voice's class draws each next sample from the variation the current one's zones give for parameter 4, and music 2's variations all hold volume 100 with no controller). Lost Kingdom's six variations take 0-14, 15-29, 30-44, 45-59, 60-74 and 75-90; the first sample is always the first variation's, so an empty park plays variation 1 and is not silent. The same block then does two more things (`0x0054f875`..`0x0054f8c8`): the crowd's own voice, kids 91, is handed the guests within four cells of the cell at `[0x007b05cc]`, the cell under the pointer, held to 100 and nought in state 4 (`FUN_004c8d30`, `FUN_0051e7b0`; `audio.md`, "The crowd's voice": the level picks its variation and sets its volume, 14 to 22 of 100), and `FUN_0055ab50` is called. **`FUN_0055ab50` is the flying cars' rectangle** (Q236): it copies the box of the park's boundary walk (`FUN_0052c6c0`; `FUN_0052ba50` keeps the least and greatest cell x and y it visits at `0x008186e4`, `0x008186e8`, `0x00818c0c`, `0x00818c08`) into four words, in the cars' units of 3000 a cell and drawn in 3.5 cells: `0x007660bc` and `0x007660c4` the least x and y × 3000 + 10500, `0x007660b8` and `0x007660c0` the greatest × 3000 − 10500. Every reader is one of the flying cars' routines (`FUN_0055a720`, `FUN_0055abf0`, `FUN_0055c350`, `FUN_0055cc00`, `FUN_0055de70`, `FUN_0055e020`): a new car starts on a random point of one side, 45000 beyond the facing one (`0x0055a720`). Nothing in the stock Lost Kingdom park flies, so the words feed nothing there; they were not read from the running game. **OpenTPW**: `ParkAudio.MusicLevel` and `SetsMusicLevel`, asked in `OnUpdate` of every tick the frame ran; as each sample ends `ParkAudio.NextMusic` draws the next one's variation by the level (`ParkScreams.NextVariation`, the chain's rule), at `MusicVolume` whatever the level. The block's second call is built too (Q236b: `ParkAudio.SetCrowdVoice`; `audio.md`, "The crowd's voice"); its third is counted, not built (`PARK_LOOP_FUN_0055AB50`, which waits for the flying cars). Which guests are counted is Q148's.

**Measured in the original** (review fix 4, 2026-10-06; `rv4/orig/music.py` reads the voice of vtable `0x0070a498` in the running game, `a.log`). Thirteen guests gave level 6, 34 gave 16, and the sample pointer changed every 18 to 19 of the park's ticks, always to a sample of the variation whose zone held the level: the level reached 15 on tick 2091 while a sample of the 0-14 variation was running, and the next sample, on tick 2107, was the 15-29 variation's; it fell to 14 on tick 2443 and the next, on 2445, was the 0-14 one's. The game's sound was captured to a file at the same time (ALSA's `file` plugin over a null device; that device does not pace the mixer, so the file runs about three and a half times faster than the game and only its own time axis is used). Every entry of jungle's `MusicHD.sdt` matched against it by its first eight seconds (`xcorr.py`, `xcorr.txt`): 140 matches, 17.5 s apart, `Level1-*` entries until the file's 1506 s and `Level2-*` from 1530 s to 1838 s, which is where a straight line from the log's clock puts ticks 2107 to 2445. The music's gain in the mix was -14.6 dB (median of 26 matches) at levels 5 and 6 and -13.8 dB (13 matches) at 15 and 16; a loudness that followed the level would have been 8.5 dB apart.

**Measured in OpenTPW** (`rv4/confirm.py`: the game's own mix by SDL's disk driver, the music found in it the same way). With the fix, thirteen guests, level 6: `Level1-1` at score 0.67, gain -21.5 dB; after `load 190`, level 89, variation 6: `Level611` at score 0.85, gain -18.7 dB, 2.8 dB from the first (2.4 dB in the run before; that gap is the match and not the music: a first-variation sample that matched at 0.88, `Level1-5` in `mutate.py`'s M6 run, read -19.6 dB); the log shows the variation stepping 1, 2, 5, 6 as samples ended on ticks 1686, 2247 and 2808, 561 ticks (17.4 s) apart. The build before it (main at `7abf0e0`): at level 6 no entry matched (best score 0.07), at level 89 `level4-1` at 0.83, -19.8 dB: its music was the level as a volume and its variation was drawn evenly. The mix's whole loudness does not show any of this (-32 dBFS in both builds at both levels: the park's other sound is as loud as the music), which is why the first version of this run, measuring that, passed on both builds and was thrown away.

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
the shortcuts' is run ("A park screen is open"), which here is `WindowStack.ParkScreenOpen`, asked by `Level.GameTableKeys`,
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

### The cheat table (`DAT_0078733c`, rows at `0x007484d0`) and the cheat flag

(Fork watch 2026-10-20, `verify-other-ui-audio.md` O-L1, O-L2, O-G; read from the bytes of `testme.exe`; not run.) The twelve
rows are 20 bytes each, every one with modifier `0x30` (**Alt alone**, matched by `==` as above), `+0x10` and the name null:

| Row | Key | Handler | What it does |
|---|---|---|---|
| 0 | `R` (`0x52`) | `0x0040c430` | **research**: `0x00519550` answers the lab (the thing whose id is world `+0x1da722`), and `0x00503ff0( lab )` sets the researched byte `+0x10` to 1 in the control record (`0x004d3d10( world+0x1d943c, id, 0 )`) of every item with an open lab project (project `i` is the word at `lab + 0xc + i*20`, `i` below the count at `lab+0x145c`). It writes nothing else: no project removed, no `mResearchDone`, no `+0x14` tier, no message, so no "recently researched" entry; research carries on as before. `hud.md`, "What the buy list actually filters on", lists the byte's other writers |
| 1 | `1` (`0x31`) | `0x0040c450` | `push 0x2710; call 0x005195d0; call 0x004d0190`: ten thousand to the money (`ride-operation.md`, `0x0040c462`) |
| 2 | `G` (`0x47`) | `0x0040c6c0` | `push 0; call 0x00409410; call 0x005af810`: the Local golden ticket 0 |
| 3-7 | `0x61`-`0x65` | `0x0040c6e0`, `0x0040c700`, `0x0040c720`, `0x0040c740`, `0x0040c760` | the same with Local 1 to 5 |
| 8-11 | `0x66`-`0x69` | `0x0040c780`, `0x0040c7a0`, `0x0040c7c0`, `0x0040c7e0` | `push 0x64; push i; ... call 0x005af940`: the Global golden ticket 0 to 3 |

OpenTPW builds none of the twelve: each is counted on its key's release with Alt alone (`Level.CheatRows`;
`CHEAT_RESEARCH`, `CHEAT_MONEY`, `CHEAT_LOCAL_TICKET`, `CHEAT_GLOBAL_TICKET`; Q591), the build waiting on Q592.

`0x0046b420` builds the modifier bits, and the window proc has the same code inline (`0x0046ba15`, `0x0046bb0b`): the mods ride in the
posted message's bits 16-23, taken at the key-up (`0x0046bb0b`..`0x0046bb47`), so a cheat fires on the **release**, with Alt
held alone. No earlier table holds a cheat chord: `game` has only Alt+L, Alt+S and Alt+V (rows 11, 12, 14), and
the camera's rows 15 and 16 (`0x64`, `0x66`) have modifier 0, so Alt+Num4 and Alt+Num6 never latch the pan.

**Unsettled:** both key edges replace the vk by `MapVirtualKeyA( vk, 2 )` when that answers non-zero, excluding only the keypad's
`*`, `+`, `-`, `.`, `/` (`0x6a`, `0x6b`, `0x6d`, `0x6e`, `0x6f`, which become `vk << 8`; `0x0046b9c0`..`0x0046b9f5`, `0x0046bab8`..`0x0046baeb`).
The digits `0x60`-`0x69` are not excluded. If the call answers `'1'` for `VK_NUMPAD1`, rows 3-11 (and the camera's keypad rows
13-22) are unreachable and Alt+Num1 fires row 1. Bytes cannot settle it: Q720. The camera's keypad rows are reached under
Proton: keypad 4, 6, 2 and 8 pan (Q625) and keypad 5 resets ("The camera's reset", Q690); the cheat rows' Alt+Num were not pressed.

**The cheat flag is cleared by the first key.** The flag is `+0xa` of the object at `0x00fb3520`, whose constructor `0x004053b0`
clears `+9`, `+0xa` and `+0xb`. That constructor is run again behind three separate first-use guard bytes (bit 1 each): `0x00fb3519`
(the loader `0x00415c22`, writer `0x00416c61`), `0x00fb34c4` (rename, `0x004995d5`) and `0x00fb34c8` (both key paths of
`Park_MouseMessageProc`, `0x004888d2` and `0x00488961`). In a fresh launch the first key that falls through the camera, game
and shortcuts tables (one no earlier row consumed) runs the key guard, which re-runs the constructor and clears the flag before
`0x00405560` reads it; the flag set by a loaded `Easymode.TPWI` therefore holds only for the byte, and the cheat rows work from
a later park of the same run (the key guard is spent), or after a rename once a key has already reached the check. Not run in the
original. The one caller of `0x004053c0` is `0x0049960a` (with 1). Message type `0x15` (`0x00405530`, `mov eax,0x15; ret`) is
what `0x004053c0` posts when it turns the flag on (through `0x0040fb10` on `[0x00788d3c]`); its listener was not looked for.

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
| `FUN_004a9180`, the end of the postcard's capture, which the UI pass `FUN_0048a580` calls on the third pass after `FUN_004a9380` and which then opens the postcard's screen (`FUN_004a8c00`) (`0x004a923a`) | off, if on, when `DAT_00fb3b7c` is not 1 |
| `FUN_0048ac40` (`0x0048aca4`), from the end-of-park routine `FUN_005168f0`, which sets the world's state to 4 and passes the park gates | it leaves first person or a ride view, calls `FUN_004e15b0( 0 )` and takes the keys away (`FUN_0040cfa0`); then off, if on. Then it closes the open screen, hides layers 0 and 1 itself (message 6) and puts up a full-screen control, id `0x7472`, handler `0x0048a740`, holding stream `0x0074fb20`: one control, id `0x5a`, (174,500)-(1874,1000), mesh `0xf3a7aff4` |
| `FUN_0048ae70` (`0x0048aed4`), from `World_MakeEndFeature` (`0x00516b00`), which makes the "End" feature | the same, with stream `0x0074faf0` (id `0x321`, (102,536)-(1946,940), mesh `0x706de86b`) and handler `0x0048a970` |
| the handler `0x0048a740` (`0x0048a7f3`, `0x0048a8d8`) | **on.** A right click with RMB cancel on, more than 2000 ms after the control was made, or a plain Escape: it sends its control message 4, leaves the ride view (`FUN_0042a190`), turns the full-screen view on, loads `0x0074fb20` again into the view's own control, and gives the keys back (`FUN_0040cf60`) |
| `FUN_0048adb0` (`0x0048addc`), from `FUN_004815e0` in the load's rebuild `FUN_00415140`, when the saved state is 4 | **on**, if off, and `0x0074fb20` loaded into it |

So an ended park is left in the full-screen view under its banner, and by the handler's state-4 arm nothing brings
the interface back. The two meshes are the loose language meshes' single nodes, `s_bkrupt` (`bankrupt.MD2`) and
`s_congrats` (`congrats.MD2`): `FUN_004893c0` registers every `data\language\<language>\*.md2` through `FUN_00489060`,
ui.wad's mesh registrar, and Windows language `0x409` picks `American` (`0x005c0e13`, table `0x00f7ec28`). The disc ships
the three meshes in `Data/Language/English/` only and its installer copies them into `American/` (Q733). Each banner
moves by the window-animation engine (`hud.md`, "The window-animation engine"), a step every 35 ms: the end of a park's
by table `0x0074fc38` about (937,500), wherever `0x0074fb20` is built (`0x0048ad7a`, `0x0048ae39`, `0x0048a85b`,
`0x0048a935`); the End's by table `0x0074fcf8` about (973,470). Fork watch 2026-10-24, `verify-world-other.md` L1 and M1,
`verify-audio-ui.md` C1. The end of a park was not run.

**The End feature's handler `0x0048a970`** (`EndFeature_OnMessage`; Q703; fork watch 2026-10-19, verify-world.md L2). Message `0x15` stamps
`[0x007c2508]` with the time (`0x0048ac16`). Message `0x1000b`, a key: `0x1b` (Escape) with `arg3 & 0xff0000` clear
(`0x0048a99e`..`0x0048a9af`), with no time gate. Message `0x10006`, a button: button 1, `[0x0078d911]` (RMB cancel) set and
`time - [0x007c2508] > 2000` strictly (`jle` at `0x0048aaf8`). Both arms run the same body, in two copies (Escape
`0x0048a9b5`..`0x0048aac2`, the button `0x0048aafe`..`0x0048ac06`; they differ only in `push 1` against `push edi`, and
`edi` is 1 there; fork watch 2026-10-24, `verify-audio-ui.md` L5): control message 4 to the window,
`World_DeleteEndFeature` (`0x00516c10`; deletes the End feature, asserting on its handle `0x007cf4f4`), message 6 to `[0x007cb2b0]` (0) and
`[0x007cb2ac]` (1), `FUN_0040cf60` (the keys back), byte `0x007c24d0` set to 0, and `FUN_004862a0` (re-focuses a window).
When the game type `[0x00fb3b7c]` is 2 (Instant Action) it also shows UITEXT **471** through `0x006ace60( 0x1d7 )` and
`FUN_0047f020( ..., 2, ... )` (`0x0048aa4f`..`0x0048aac2`): "Congratulations! Now you've completed Instant Action mode
you should try the Full Simulation game! Just return to the Lobby to start." It writes no profile state: the bit it sets at
`[0x00fb34c7]` is the compiler's one-time guard for the mode object at `0x00fb3b7c` (constructed once by `FUN_00550ca0`, its
destructor `0x005d1750` a bare RET registered through `0x0067a7d0`). Message 6 is a control's visibility, its argument
the new state (`FUN_004a2ac0`; `hud.md`, "The help bar in first person"): the body hides layer 1 and shows layer 0, the park's view and the gadget on it.
Not read: `FUN_004862a0`'s effect.

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
toggles it, refused in first person and in the End's ride view; on, nothing of the window stack is drawn or pointed at, every press is the
cover's, and `Level.GameTableKeys` and the orbit camera's C are not heard; the camera's keys and wheel are; F3 or a plain
Escape takes it off and is spent doing it; Ctrl+P is counted, `FULL_SCREEN_VIEW_POSTCARD`. The pointer wears the plain
arrow. Where it parts from the original:

- **Over a window that has the keys F3 does nothing**, and that is the original's: the game menu, a message box, the
  options screen and the map are not on layer 0 and keep their keys, and a park screen, which is on layer 0, has the
  focus, with the `game` table off and a handler that runs the `shortcuts` table alone ("A park screen is open"). F3
  over the entry-price screen left `[0x007cb2e8]` at 0 in the original.
- **The armed tool's squares still follow the pointer** under the view, since the pointer's cell is picked every frame;
  the original's mouse proc hears nothing there, so its square should stand still. Not measured.
- **The end of a park** turns the view on and off and gives Escape the game menu under it; no park ends here. The End
  feature's cover is its own, with its own keys (below, "OpenTPW builds it", under "The End feature's view").

**F2 stays, at Alexah's word** (`docs/DECISIONS.md`): `InputButton.HideUI`, OpenTPW's own key beside F3. Measured
before the build, each predicted from the code: it acts on the press, in the lobby too; it hides the pointer with the
interface; under it B does nothing, a left click on grass arms the path tool, Backspace and a quick right click reach
the park, C enters first person, Escape does nothing at all (not even leaving first person), and F2 in first person
hides the viewfinder. `RootPanel.Hidden` stops the interface drawing and updating, and `Level.RightPressTaken` gives
the park every right press while it is set.

---

**The End feature's view** (Q551). The executable's own names for the pair are `StartWinningSequence` and
`EndWinningSequence` (the assert "Calling EndWinningSequence before StartWinningSequence", `0x00762720`, in
`World_DeleteEndFeature`). The view is **the ride view of the End object itself**: `World_MakeEndFeature` hands the
object it made to `FUN_0048ae70` (through `FUN_00481b60`), which leaves first person or a ride view and calls
`FUN_004e15b0( 0 )` with that object as `this` (`0x0048aeb4`), the "Ride it!" call. `FUN_004e15b0` passes the item's
`+0x80` to `FUN_0042a560` in all three of its arms, and `+0x11c` only in the arm for a thing viewed on its own
model (`0x004e1644`..`0x004e1651`); the track ride's lead-car arm (`0x004e15ee`) and the tour arm (`0x004e1623`)
pass nought there. The catalogue item keeps its `.sam` fields in the order of the executable's field table (records of `0x3c` bytes, `0x00744c60` on): `Info.DontApplyOffset` `+0x38` and `Info.DoHeadProcessing`
`+0x84` sit 19 records apart, and `UsageInfo.GoldenTicketCost` (`+0xc4`, read at `0x004db4a6` by the ticket test)
sits 22 before `UsageInfo.ConstrainCamera`, so **`+0x80` is `Info.FirstPersonSprites` and `+0x11c` is
`UsageInfo.ConstrainCamera`**. `FUN_0042a560` finds the model's first lookup record of id 1 under `0x1000`
(`FUN_0044b220`), marks it stored each frame (runtime bit 4), keeps its matrix at `[0x00790a30]` and the model at
`[0x00790a44]`, and sets the mode through `FUN_0042ab20( 4, 1, model, 0, 0, ConstrainCamera )`: `gui_CameraFlags`
bit `0x4`, and with ConstrainCamera bit **`0x800`** with the look offsets `[0x00790adc]` and `[0x00790ae0]` noughted
(`0x0042abd5`). Entering it also sets `0x200` and turns the compositor off where it is on (`FUN_00457a40`, `0x6000`
of `[0x007a1a8c]`), stops the model highlight (`FUN_0045d660( 1 )`, `[0x007afda4]`), sets bit `0x8000` on each node of
the viewed model whose lookup record carries `0x10000000` and clears it where the record carries `0x20000000`, and
calls `FUN_00481470( 1 )`, whose layer switch `FUN_004a2ac0` does nothing while the End exists (`FUN_00516c00`): so `FUN_0048ae70` hides both layers
itself (`0x0048af00`..`0x0048af21`). It ends in the sprite swap (`0x0042a6ee`): the End, a feature, reads `FirstPersonSprites` 0
(`Features.sam` and `end.sam` do not declare it) and entering the ride view clears flag `0x02`, so it keeps the `.TPC`
pictures. All four themes' `Rides.sam` set it 1 and no other loose `.sam` does, so every ride but the four tours reads 1
(the item's own file wins, and the tours' set 0), and "Ride it!" on one swaps to the `.FPC` pictures.

Each frame the camera update `FUN_0042b1c0`, under `& 0x3c` (`0x0042ca0a`), takes the record's world matrix for the
camera with its turn, the three rows of three, mixed with the last frame's: `(1 - w) x now + w x last`, `w = ( 1000 - t )
x 0.0005`, `t` the milliseconds since the last update (`FUN_00402d80`'s clock, QueryPerformanceCounter or `timeGetTime`;
100 past 200 and 1 at nought or below on whole milliseconds, `0x0042b2c7`..`0x0042b2ed`; then held to 1000 and raised
to 1 below 1, `0x0042ca1b`..`0x0042ca53`; so `w` is 0.4 to 0.4995; `0x0042ca13`..`0x0042cb7d`; the last frame's is the
record's own matrix, `[0x00790948]`, not the mixed one), and the eye copied from the record's own translation, unmixed
(`0x0042cb83`..`0x0042cb96`). It
then turns the picture by the look offsets (`FUN_0046f6f0( ..., 4, ... )`, `0x0042cbc3`). Those are worked from the
pointer only when `uVar13 = ~flags >> 11 & 1` or the right button is held (`[0x00790aac]` bit 4, `FUN_0042a760`'s
button 1), so with ConstrainCamera they stay nought, and the End's handler forwards nothing to `FUN_0042a760` in any
case. **So the End's picture is its `camera` node, still**: End.MD2's record 0, node 38, id 1, flag word `0x1031`, at
world (480.5, 10, 200), three cells inside the jungle gate and ten up, turned 16° up about -X; no clip moves it.
`World_DeleteEndFeature` deletes the thing (`FUN_0050b780`), noughts `0x007cf4f4` and leaves the ride view
(`FUN_0042a190`: `FUN_0042a560` again, which `FUN_0042ab20` turns off; the layers come back by `FUN_004a2ac0( 1 )`),
so the orbit returns where it was.

**What plays in it is the End's own script.** `end.sam` (id 1605, `Info.WhichUIType 4`, `Info.DontApplyOffset 1`,
`UsageInfo.ConstrainCamera 1`) and `end.wad`'s `End.MD2` (eight planes, `_f_endplane01`-`08`, each with its
propellers, two trail nodes, particle records of flags `0x111`, and one sound node, flags `0x211`) with three clips,
`Endm1`-`Endm3` (500 frames; three planes fly towards the view, two cross it, three climb away). `End.RSE` ("End Anim"): `SPAWNCHILD AnimCtrl.rse`, `VAR_ENDTIME` now + 10000,
`DIPMUSIC 1`, `ADDOBJ 9 -1 186 2` (`cat_ui` 186, no node), `ADDOBJ 5 9 200 1` and `ADDOBJ 5 10 201 1` (`cat_rides` on
`fwork05` and `fwork06`, sound ids 9 and 10), then for ever once a second: `EVENT 5 10 202`, and `EVENT 1 17 6`,
`1 18 3`, `1 19 6`, `1 20 3` on `fwork01`, `fwork02`, `fwork04`, `fwork03`: effect 6 `P_EFFECT_Firework2` and 3
`P_EFFECT_Firework1` (OpenTPW's run starts the four so, `q703/run4/run.log`); past the end time
it sets `VAR_STATUS` 1 and carries on (what reads it was not traced). `AnimCtrl.RSE` ("End Anim Control") plays the
clips 0, 1, 0, 2, 1, 2, 0, 2 and over, each by `WAITANIM 5 k` after starting the trails of the planes in it on their
particle ids (`ADDOBJ 1 n 93..95 1`, `P_EFFECT_SmokeTrailR`, `B`, `W`, drawn as red, blue and yellow stars) and
`cat_ambient` 209 on their sound ids, and kills tag 1 after. The sounds were not listened to. The full-screen control
`FUN_0048ae70` puts up holds one control of mesh `0x706de86b` (stream `0x0074faf0`, `lib/uistream.py`): `s_congrats`,
the node of the language folder's `congrats.MD2` (above, the end of a park). The original's frame shows no banner because
the reference install has no `Data/Language/American/*.md2` (Q733); an installed game shows it.

**In the original** (Q551; `q551/orig/run.sh`, `q550/alldone576.TPWS` loaded into the reference install,
`original/camread.py` every 0.1 s, two rounds, `q551/PREDICTION.txt`): on tick 800 the End's id 43 was set and
`gui_CameraFlags` went from `0x0100` to `0x0b04` (`0x4`, `0x800`, `0x200`, the compositor's `0x4000` off), the
ride view's model 91, the eye (480.53, 10.00, 200.00), the look offsets 0 and 0 while the pointer was moved to the
window's four corners, layer 0 hidden, layer 1 hidden, the dip word `[0x00803ad0]` 1 (`[0x00803ac8]` is only the
volumes' dirty flag); the tick ran on, 800 to 1052 in 41 s. The frames (`q551/orig/a-sheet.png`, `b-sheet.png`): a
low view up the path from inside the gate to the sky, the planes trailing yellow, blue and red stars, the bursts,
the pointer still drawn. Escape: `0x0100`, the orbit's eye back, layer 0 shown, the dip 0, UITEXT 471's box over the
park with the gadget, the tick standing under it; stars an `EVENT` made still in the sky.

**OpenTPW builds it** (Q552; `ParkEndFeature`, `ParkRideViewCameraMode`). `Level.EndOfResearch` asks for the End and
`Level.Update` makes it after the frame's entity walk, since its model adds entities the walk cannot take (the
original makes it inside the lab's message). The item is the catalogue's named `End`; the thing takes the park's next id
(43 in the scene, as the original's) and is not put in the park's object list, which only the save reads. No key
reaches a save under the End, but closing the window writes `autosave.TPWS` (`Level.WriteParkOnLeaving`) with no End in
it and its two scripts not written (`SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD`); the original's save as it closes under the
End (its state `0xb`) is not measured. Its model and its nodes stand as the file holds them (`RideNodes.PlaceFixed`): End.MD2's
root, `parent`, keeps its own (480, 0, 170), which a placement at a cell would replace, and that is what puts the view
node at the original's (480.53, 10, 200). `End.RSE` is bound from word 0 (`ParkRides.BindFixed`) and spawns its child
on its first tick. The camera is the constrained ride view only, the general "Ride it!" one not built: the turn mixed as
above with `t` from `Time.Delta`, the first frame's last matrix the record's own, and first person's fog drawn in it
and the people's bodies not pulled toward the eye (`gui_CameraFlags & 0x16`, `Level.InsideView`); the picker still
follows the pointer there, counted `PICK_IN_FIRST_PERSON` (Q505). The full-screen view goes off if it was on, the park screen closes, and a cover with keys and
a right click of its own goes over the park's layer (`WindowStack.CoverKeys`, `CoverRightClick`); F3 is refused in the
ride view as in first person. Escape with no modifier, or a right click with RMB cancel on more than 2000 ms after it was
made, ends it: the thing's scripts (their `ADDOBJ` particles stopped, an `EVENT`'s left), the dip, the model, then the
orbit made again where it was, the cover off and, in Instant Action, UITEXT 471's box with its cross. Run beside the
original's (`q703/PREDICTION.txt`, `q703/side-by-side.png`): on tick 800 the eye (480.53, 200.00, 10.00) in the park's
axes looking (0.000, 0.961, 0.276), the scripts "End Anim" and "End Anim Control", the dip 1, the tick running, the
bursts over the path, the interface gone; after Escape or the right click the orbit's eye as before and the box.
**The planes fly** (Q554, `NodePoser`): Endm1's three come down the park three abreast and over the view, plane 02 from
(480.3, 443.4) to (480.3, 90.4) at height 59, Endm2's two cross from the sides, Endm3's three climb away, as the
original's frames (`q554/PREDICTION.txt`, `q554/side-by-side.png`; the console's `endfeature planes:`; Endm3's in
`q554/run4/run.log` from 24.3 s, plane 06 from height 47 to 240, frames `run4/a-s24`..`a-s33`, beside the original's
`q554/orig/a-e16.png`).
**Their trails follow them** (Q555, `RideScript.SweepEffects`, `RideNodes.Follow`): each tick the emitters AnimCtrl
started on the planes' nodes are put where the players have posed those nodes, so the stars stream behind the planes
as in the original's frames (`q555/PREDICTION.txt`, `q555/side-by-side.png`; the console's `endfeature trails:`, plane
02's two at its x plus and minus 9 and about 7.5 behind it along the park through the whole of Endm1).
**The advisor speaks over it, in both games** (Q556): nothing in the ride view hides him. In the original's film of the
End (`q556/orig/q556c.mkv`, `original/advdraw.py`) row 167 on slot 1, the pith-helmet fly, is drawn in the lower right
through its line (ticks 801 to 838) and row 93 on slot 0 through its (850 to 884), and none between or after
(`ui.md`, "The advisor on screen", his draw); ours says the same two on the same slots and draws them so
(`q556/side-by-side.png`). Frames taken between his lines show none (`q550/orig/a-4`, `a-5`).
**Their trails are as dense as the original's** (Q703): the park runs its particles at its file's density, 500 in
this scene, so each trail makes a star a tick and holds about a hundred in both games (90 to 106 there, 98 to 107 here), the stars the same sizes (`park.md`,
"A park's particles, started and drawn", "The density is the park file's"; `q703d/side-by-side.png`).
**Their sounds play** (Q553): `End.RSE`'s 186 flat, 200 and 201 on its sound nodes 9 and 10 and a 202 each second,
AnimCtrl's 209 on the planes and moved with them, at the original's places, distances, gains and volumes (`audio.md`,
"A script's sounds"; `q553/PREDICTION.txt`; the console's `scriptsounds`).

## The park management gadget

**The gadget's button list is the `shortcuts` table**, whose 17 rows are the same 17 actions, in order:

`menu, buy, hire, parkstatus, allitems, allstaff, allpeeps, financeinfo, loans, staffcosts, entryprice, research, map, staffloc, peeploc, postcard, camcorder`

`postcard`, `staffloc` and `peeploc` carry modifier `0x0c`; camcorder is a plain `'C'`. **Cross-check that proves the decode: entry 0 is `menu` at key `0x001b` = `VK_ESCAPE`**, the Escape route already traced.

Each row's `+0x0c` is a 16-byte thunk (`MOV ECX,0x007b51f0; CALL x; MOV EAX,1; RET`) into a SECOND layer of stubs, and those are what reach real code:

The keys are each row's `+0x02` and `+0x04`, read from the rows at `0x00748378` (Q366); no row of the camera or game
table ahead of them in the chain has the same key and modifier.

| Row | Key | Reaches |
|---|---|---|
| `menu` | Escape | `GameMenu_Open(0)` — already named, the decode's control |
| `buy` | B | `FUN_004acc70(&temp, -1)` |
| `hire` | H | `FUN_0049bdd0(-1)` |
| `parkstatus` | I | `FUN_004a52a0` |
| `allitems` | A | `FUN_00495aa0(0, -1)` |
| `allstaff` | S | `FUN_00496620(0, -1)` |
| `allpeeps` | V | `FUN_00493530(0)` |
| `financeinfo` | F | `FUN_0049ac60` |
| `loans` | L | `FUN_0049fb30` |
| `staffcosts` | T | `FUN_004b2750` |
| `entryprice` | G | `FUN_00498d80` |
| `research` | R | `FUN_004aa480` |
| `map` | Space (`0x20`) | `FUN_005f0b40` (ECX = `0x00F86DB8`) |
| `staffloc` | Ctrl+S | `FUN_004b3f60([0x007c2658])` if `[0x007cc3c0]`, else `FUN_004b3f60(0)` |
| `peeploc` | Ctrl+V | `FUN_004b3f60([0x007c2658])` if `[0x007cc418]`, else `JMP FUN_004b4280` |
| `postcard` | Ctrl+P | `FUN_004a9380` |
| `camcorder` | C | `FUN_00481a10` |

**What each opener does first** (Q366, read up to its `FUN_004a0810( category, screen )`, which every one of the ten
calls): park status (`[0x007cb398]`), finances (`[0x007ca2e8]`), staff costs (`[0x007cc2ec]`, `0x004b2772`) and the
entry price (`[0x007ca210]`, `0x00498d9f`) return at once with their screen up; **loans returns at once with its screen
up or in Instant Action** (`[0x00fb3b7c] == 2`, `0x0049fb8e`), before it closes a screen or notes itself; buy notes itself
first and, with its screen up, only picks the tab (`0x004acca0`); hire, all items, all staff and all visitors close the
open park screen (`FUN_00485b40`) and build their own again. Measured in the original (`q366/orig/a`..`c`, Instant
Action): L opened nothing, and the Money button after F and L opened finances.

**`-1` is a REMEMBERED TAB, not "nothing selected".** `FUN_0049bdd0` rebuilds the screen when `param_1 == -1`, then does `if (param_1 == -1) param_1 = DAT_007ca30c;` and switches five ways on that (cases 0-4) to choose both a title string (`0x8b`-`0x8f`) and a control id (`0x2490`/`0x2493`/`0x2492`/`0x2494`/`0x2491`). So hire has **five tabs** and `-1` means "open on the tab it was left on"; a real index with the screen already open rebuilds it too: the opener destroys the open screen first (`FUN_00485b40`, `0x0049bdec`), so the re-title branch is never reached.

**The staff and visitor locators are ONE shared panel**, `FUN_004b3f60`, differing only in which global they test (`0x007cc3c0` vs `0x007cc418`, `0x58` apart — sibling objects). It switches on the thing's type byte at `param_1 + 2` to pick a role and press that role's radio button, `0x95e`..`0x964` (5 handyman `0x95e`, 4 mechanic `0x95f`, 7 guard `0x962`, 6 entertainer `0x961`, 8 researcher `0x960`, 1 visitor `0x964`; no `0x963`), and treats `param_1 == 0` as nothing selected. The panel's own procedure is `FUN_004b3790` (`hud.md`, "The staff and visitor locators"). It also builds `DAT_007b05e8` exactly as `FUN_00481a10` does, which corroborates that `DAT_007b05e8` is **not** a camcorder-only global.

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

**Which rounding is live is not settled.** The CRT starts the FPU at 53-bit precision (`0x006804da`). In the running
original a breakpoint at `0x0059e17b` read the control word `0x27f`, 53-bit, on ticks 970 and 1125 (`advisor-park.md`,
"The product is a double of the float key"), and at the conform during a file's load 40 times of 40 (Q819, "The
control word" above), beside the 24-bit clock accumulator below: both states occur (fork watch
2026-10-18, `verify-world-a.md` H2). The frame
renderer `FUN_00576ec0` switches to 24-bit at `0x00576fa7` and has three exits. On success it restores the saved word
(`0x00577310`), or, when the `0x2` bit of `[0x008bd508]` is set (`TEST AL,0x2` at `0x005772f6`), the saved word with the precision bits set,
64-bit (`0x00577304`, `FUN_00591250` ORs `0x300`); no writer found sets that bit (it starts `0x3000141` at `0x00583555`,
the other writers OR in other bits or put back saved words), so that arm is probably never taken, though an indirect
write is not ruled out. Its failure exit (`0x00577433`) restores nothing, so one failed frame leaves 24-bit in force
for the rest of the run. The thing sweep runs beside the renderer, never inside it, so what it computes sees whatever
the last exit left (Q173). DirectDraw is set up with `DDSCL_FPUSETUP`: `SetCooperativeLevel` (vtable slot `0x50`) is called with `0x851`
(FULLSCREEN | EXCLUSIVE | ALLOWMODEX | FPUSETUP; `push` at `0x0056390b`, call `0x00563914`) and with `0x808` (NORMAL |
FPUSETUP; `0x00563b7a`, call `0x00563b92`); neither carries FPUPRESERVE `0x1000`. What that does to the control word
belongs to the runtime, not the executable (fork watch 2026-10-18, `verify-world-a.md` H2). It matters only at the margin: at 53 bits a positive step landing exactly on a
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
`(int)(y * 0.1)` held to 0..94 and 0..83 (`FUN_0042cd40`, `MapInfo`'s limits `[0x007a0314]` and `[0x007a0374]`), is handed to `FUN_0042a340` (`0x0042c587`). That answers only for
an **entrance**, a cell of type 9 (`FUN_00536340`, `[cell+8] == 9`): it reads the cell's `mParentID` (`+0x10`), walks
the thing chain of that owner's cell (`+0x24`, the next at the thing's `+0xa`), and returns the first catalogue object
(kind byte `+2` is 3) whose item's `+0x118` is nought. `+0x118` is **`UsageInfo.CannotRide`**, row 11 of the descriptor
key table at `0x00745940` (rows of `0x3c` bytes, row 0 at `+0xec` and four bytes a row, which rows 21, 27 and 28
confirm as `+0x140`, `+0x158` and `+0x15c`); its values in the shipped files are FileFormats `sam.md`'s. On a hit the loop runs `FUN_00412e90( 0x7890a0, item )`, whose
answer it drops, then the thing's `FUN_004e15b0( 1 )`, the ride window's "Ride it!" (`hud.md`) entered from first
person, and ends the sweep (`0x0042c5b6`); the position is still written back. **OpenTPW** has no ride view: `Slide`
asks the same question after every pass (`ParkCamcorderCameraMode.RideAt`, the objects anchored on the owner's cell
standing for the chain) and counts each yes as `FIRST_PERSON_WALK_INTO_RIDE`, twice as the viewer crosses in and then once each frame they step
in the cell, moved or stopped by a shut side, and walks on. In Lost Kingdom the one such cell is the Belly Bounce's entrance
(52,23).

**An entrance is shut to the viewer unless its thing can be ridden and seen from** (`docs/QUEUE.md` Q140; read as
disassembly, then walked in the original). Mode 2 alone has this arm. It sits after the approach's two tests and before the solid types (`0x004d8883`..`0x004d8901`, its
verdict at `0x004d8b00`..`0x004d8b28`), and it is asked of the cell being **entered** (`EDI`):

1. The cell is an entrance, type 9 (`FUN_00536340`). Any other type goes on to the ordinary tests.
2. Its owner's cell is the one `mParentID` (`+0x10`) names, and the things on that cell are walked from its `+0x24`,
   each next at the thing's `+0xa`, to the **first** catalogue object (kind byte `+2` is 3). An owner's cell with no
   such thing goes on to the ordinary tests (`0x004d8901` puts the mode back in `ESI`).
3. That one thing decides, and nothing after it is asked: **shut** if `FUN_0042a440( thing, thing[+0x20] )` answers
   nought, **shut** if its item's `UsageInfo.CannotRide` (`+0x118`, the item by `FUN_004dd4e0`) is not nought,
   otherwise **open** (`XOR EAX,EAX` at `0x004d8b25`).

So neither the cell left nor any `mNeighbours` bit is read: an open entrance is open from grass and from its own
footprint, and a shut one is shut from the path it is linked to. `FUN_0042a340` (above) walks the same chain but
passes over a thing that cannot be ridden and takes the next; the edge test stops at the first.

**`FUN_0042a440( thing, model instance )` is "has this a ride view"**, its one caller the arm above. It answers 1 at
the first of four that holds, and `FUN_004e15b0` ("Ride it!") picks its camera's model in the same order:

| | Asked | What it is |
|---|---|---|
| 1 | `thing[+0x28]` not nought and `FUN_00549d30` of it not nought | The track ride's lead car (`park.md`, "`BUMP` works on the track-ride record"): the `GFEJ` magic, the record `DAT_00877b60 + (h & 0xff) * 0xd0` whose own handle is `h`, its lead car `+0x58` flagged `0x400000`, and that car's model `+8` |
| 2 | the script of `thing[+0x24]` (`FUN_0055a070`), its `+0x9c` (`FUN_0055a360`), and `FUN_0055a570` of that | The TOUR record's first car: for a record number 1 to 99, `[0x008791f8 + 4n] + 0x11c`, the model `FUN_0055cda0` frees for car 0 |
| 3 | `FUN_00436840( instance )` and `FUN_00436880( instance )` | A coaster: the instance's flag `0x40000` (`[0x007a4610 + 4i] + 4`), then the node on `DAT_00790fe0` whose `+0x10` is the instance, its flag `0x800`, and `[[node + 0x124] + 0x34] + 0xc` not nought. What `+0x124` holds is not traced |
| 4 | `FUN_0044b220( [0x007a4610 + 4i], 0x1000, 1 ) != -1` | The model's own view node: a lookup record of id 1 whose flag word has `0x1000` (`audio.md`, "Node lookup is by id AND a capability flag") |

A null thing skips 1 and 2. The queue item's "coaster handle" for `+0x28` was the track ride's, and its "script
camera" the TOUR record's car.

**The view node across the shipped models** (`viewnodes.py`, the raw table of every `.md2`: 2,118 in the wads and
the 11 loose ones, which carry no table). 100 models answer `( 0x1000, 1 )`: 25 in jungle, 20 in fantasy, 26 in
hallow, 29 in space. The flag words carrying `0x1000` are `0x1031` (69 records), `0x10b1` (12), `0x41071` (9) and
eight rarer ones; 101 such records have id 1 and three have id 2. In jungle the models are the rides' own
(`bouncy`, `incagod`, `lookout`, `monkey`, `mumbo`, `porkpie`, `spider`, `totem`, `tvsim`, `volcano`), cars and
pieces (`b_car`, `CrocCar`, `ape`, the four go-karts, `cart`, `Bird`, `wr_ring`), three features (`camera`, `End`,
`gates`) and one sideshow, the Jungle Spray (`Junspray`, `PJunspray`).

**Lost Kingdom's eight entrances, from the original's memory** (`q140/orig/probe.py`, read only). Each owner's cell
holds one catalogue object, with `+0x28` nought on all eight:

| Entrance | Owner's cell | Item | View node | Step in |
|---|---|---|---|---|
| (52,23) | (51,23) | 1100, the Belly Bounce | record 13, `0x1031` | **open** |
| (52,30) | (51,30) | 1303, the Jungle Spray | record 11, `0x1031` | shut: `CannotRide` |
| (43,30) | itself | 1203, the Drinks Shop | none | shut |
| (55,15), (55,16), (55,17) | itself | 1402, a toilet | none | shut |
| (58,15) | (58,16) | 1411, the Staff Room | none | shut |
| (44,29) | itself | 1406, the bin | none | shut |

**Walked in the original** (`q140/orig/a.log`, three predictions written first, three held). In first person, the
position written to `0x007908f0` and `0x007908f8` and a key held: from the path (43,28) the viewer crossed (43,29)
and parked at 299.999 against the Drinks Shop's entrance; from (52,29) they parked at 299.999 against the Jungle
Spray's, which has the view node, so `CannotRide` alone shuts it; and from the Belly Bounce's footprint cell (52,24)
they crossed into (52,23) at 239.29 and `gui_CameraFlags` went from `0x202` to `0x604`, the ride view. A path cell
to the next (the control) is crossed freely.

**OpenTPW** builds the arm (`docs/QUEUE.md` Q140b): the camcorder's edge test hands `CellEdge` a
`ParkEntranceGate`, whose `Ahead` is the three steps above, the placed objects anchored on the owner's cell standing
for the chain. Of `FUN_0042a440`'s four only the fourth is built, the item's own model asked `FindNode( 1, 0x1000 )`
once an item. The first three are counted where the original would ask each and taken as not holding:
`RIDE_VIEW_TRACK_RIDE_LEAD_CAR` for a thing with a track-ride handle, `RIDE_VIEW_TOUR_CAR` for one whose script carries
`TOUR`, `RIDE_VIEW_COASTER_NODE` for an item on a coaster's track; Lost Kingdom's stock park meets none
(`docs/QUEUE.md` Q246). The same three walks here (`q140b/run1`, each predicted first): parked at 299.999 at the
Drinks Shop's entrance and the Jungle Spray's, as the original's, and from the footprint (52,24) across into the Belly
Bounce's, counted `FIRST_PERSON_WALK_INTO_RIDE`. There the two part: the original's ride view starts at 239.29 and the
walk ends, and with no ride view here the viewer walks on, out of the entrance into the queue cell (52,22), which the
ordinary queue rule allows an entrance (16 passes counted, then parked at 220.0). The unchanged build walked into
both shops' entrances, to 309.999, and stopped at 240.0 in the footprint (`q140b/control`).

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

### A fresh world's research lab

Q349. The lab is the thing of model 14 (thing 6 of a fresh world, `FUN_00502d20`, which starts each of the five
kinds at current project -1 and highest group 1). Its setup, `FUN_00502d60`, runs in every world's making, before
any park file is laid over it: no project, the dearest -1, `mWorkLoad` `Research.StartingWorkLoad`
(`[0x00785580]`, 85), `mLabActive` whether a researcher is on the staff, then the fill and the kinds' turn.

**The fill, `FUN_005031a0`**, walks the catalogue (`0x007890a0`, in its own order) and, for each item whose
model loaded (`+0x508` nought) and whose `Info.WhichUIType` (`+0x4ac`) is under 4, gives the lab at most one
project of twenty bytes:

- **Research that costs anything** (`Upgrades[0].CostOfResearch`, `+0x180`, not nought): the item's own project,
  `mCat` its `Research.Category` (`+0x17c`), group the byte of its `Research.Group` (`+0x178`), upgrade level
  0, `mCost` the cost as a float, `mResearchDone` nought.
- **Research that costs nothing**: the control record is marked researched (`+0x10`), and `FUN_00504580` looks
  for an upgrade, unless the item's `Research.Category` is 4. From the tier after the record's (`+0x14`), while
  under 3: a tier whose `Upgrades[tier].CostOfResearch` (`+0x180 + tier * 0x40`) is above nought is the project,
  `mCat` 4, group 0, level the tier, at that cost; a tier that costs nothing is written to the record as
  researched and the next looked at. **So an item with no upgrade is left at tier 2, a ride with one at tier 0
  with its first upgrade in the lab, and an item of kind 4 with no cost untouched.**

`mMostExpensiveProject` (`+0x1464`) is the first project strictly dearer than every one before it. Then
`FUN_00503330`, a kind at a time: `effort` is `ResearchCategories[kind].Effort` (`0x00785568`) capped at 100,
`output` is `effort * 100 / the efforts' sum * mWorkLoad / 100` in whole numbers, the current project the first
of the kind not done whose group is at or under the kind's highest, and `FUN_00503b80` raises the highest group
while the share done of the projects at or under it is at least the dword at `0x00785540 + 4 * group`, which
is `ResearchTech[group + 1].PercentageForThisTech` ("The lab at work", below). The lab also listens for event
`0x11` (`FUN_00503d40`): a project finished asks `FUN_00504580` for the item's next upgrade.

**The catalogue's order** is the item folders `rides`, `features`, `upgrades`, `shops`, `sideshow`, and each
folder's items by their names in capitals (`SUPBOG` before `S_PLANT`): the order of the control records and
the projects in three `restart.INTS` files of the original's (jungle, fantasy, hallow, written on Windows) and
in one it wrote here under Proton. The three items of `upgrades` are catalogued with the rest, `Info.WhichUIType`
0 and `Research.Category` 4, seventy items in Lost Kingdom's Full Simulation.

**The original's own fresh Lost Kingdom park** (Alexah's `restart.INTS`, tick nought; `lib/parkfile.py -l`):
48 projects, 11 of rides, 5 of shops, 3 of sideshows, 20 of features and 9 of kind 4 (six rides' first
upgrades and the three upgrade items), none begun; the kinds' efforts 100, 15, 30, 15 and 10, outputs 49, 6,
14, 6 and 4, current projects 4, 41, 46, 17 and 0, each highest group 1; `mWorkLoad` 85, the dearest project 3
(item 1190, 1300), `mLabActive` 0; 70 control records, 28 researched, 13 of them at tier 2.

**OpenTPW** (`ParkResearch.FreshLab`, `FreshPark.ResearchProjects`, `ItemDescriptionFile.ResearchCategory`,
`ResearchGroup`, `UpgradeResearchCostAt`; `ParkItemCatalogue.Folders`): the fresh park's projects and its
control records' tiers are the fill's, and the catalogue lists the five folders in that order with each
folder's items by their names in capitals, the upgrades among them. The park's first line ends `lab 48
projects, by kind [11,5,3,20,9], 13 items with no tier left to research, the kinds' efforts [100,15,30,15,10] and
outputs [49,6,14,6,4]`. Every item is taken as loaded. **The five kinds' efforts and outputs are made as
`FUN_00503330` makes them** (`ParkResearch.FreshKinds`, Q350; a loaded park's are its file's,
`ParkWorld.ResearchKinds`), for the advisor's row 76, which reads the sideshows' (`advisor-park.md`, "Every
sideshow built, and more to research"). **Each kind's current project and highest group are made as
`Lab_SetupKinds` makes them** (Q701: the first open project of the kind at or under group 1, 4, 41, 46, 17 and 0
in Lost Kingdom, each group 1), and `mWorkLoad` is `Research.StartingWorkLoad`; `mMostExpensiveProject` is the fill's,
project 3 (Q590, `ParkResearch.MostExpensive`, which the research screen's bars are measured against); **not made:
`mLabActive`**, which nothing here reads.

**Predicted first** (`q349/PREDICTION.txt`). The test pins the 48 projects and the 70 control records of
Alexah's file, in order. **This game** (`q349/confirm.py`; a player folder with no `gms.dat`, so a fresh Full
Simulation park, paused on tick 8): the line above, `catalogued 70 items`, neither gap counted. With scientist
#21 hired and one of each of the eight researched features bought (`run-fix1`, the desktop): `poll message=80
row=80 days=2 score=60 threshold=25 tick=50 result=post researchers=1 kinds=8/8 unfinished=20`, queued and said
on tick 50 (`attempt=1 ... response=225 sample=223 line=0`), the fly in the research hat over the empty park
with tag 82 on the aerial (`1-line.png`). **One number of the prediction was wrong, mine: `days=0`**, where
tick 50 is the third day. Nothing bought (`run-bare`): `score=0 ... result=no-score researchers=0 kinds=0/8
unfinished=20`, silent to tick 80. Main's build on the fix scene (`run-control`): `catalogued 67 items`, no lab
in the line, `FRESH_PARK_RESEARCH_PROJECTS` counted, `no-score researchers=1 kinds=8/8 unfinished=0`, silent.

**The original under Proton** (`original/freshfs.sh`'s steps, `q349/orig/`): a player folder holding Alexah's
Full Simulation `gms.dat` and no park file, picked in the lobby, went straight into Lost Kingdom, made the park
(`a-2-park.png`: the empty park, $50000) and wrote `restart.INTS`. Its lab prints line for line as Alexah's
file's, its 70 control records are the same in order, flag and tier, and the lab's record differs only past
byte 784, the 208 unused projects, which are memory never written. Live on tick 175 the lab read 48 projects,
work load 85, dearest 3, inactive, project 0 item 1100 at 250.

| | The original | OpenTPW |
|---|---|---|
| A fresh Lost Kingdom world's projects | 48, in the catalogue's order | the same 48 in the same order |
| Its control records | 70; 13 researched items at tier 2 | the same 70 in the same order, flag and tier |
| The kinds' records, work load, dearest, active | made (above) | not made |
| Row 80 in a fresh Full Simulation park | not run | said on its first turn, tick 50, with a researcher and all eight kinds |

**Not built or not seen.** The original was not driven to hire a researcher and build the eight features, so
row 80's line in its own fresh park is not seen there; no file of this game's was loaded in the original (a
fresh park was not saved then; since Q783 it is). The other three themes' fresh labs are measured in the
original's two other files and not made here in any run. An upgrade item is catalogued and nothing builds one.
Not heard in either game.

### The lab at work: the spending, a project finished, the end of research

Q245, read in the private Ghidra copy (lane B, 2026-10-18); built and run in both games by Q701 (below). The lab is the
thing above. In memory a project is 20 bytes from `+0xc` (`+0` the item's word, `+4` `mCat` a dword, `+8` the
group's byte, `+9` the upgrade level's, `+0xc` `mCost`, `+0x10` `mResearchDone`, both floats) and a kind 16 bytes
from `+0x140c` (`+0` effort, `+4` output, `+8` current project, `+0xc` highest group, a byte); `mNumProjects`
`+0x145c`, `mWorkLoad` `+0x1460`, the dearest `+0x1464`, `mLabActive` `+0x1468`. The kinds are 0 rides, 1 shops,
2 sideshows, 3 features, 4 upgrades.

**Only a researcher spends.** `Lab_SpendPoints` (`0x00503430`) has one caller, the researcher's pre-step
(`0x005029d1`; `ride-operation.md`, "The research, in both games"), with the grade's `ResearchAbility`. It adds
the efforts of the kinds whose current project is set and not done (`0x00503450`..`0x00503466`); with none, it
spends nothing. Then, kind 0 to 4, each kind with an effort and a current project gets effort / that sum ×
`mWorkLoad` × ability × 0.01 (`[0x007007e4]`), worked in the FPU and stored as a float (`0x005034d0`), and
`LabProject_AddPoints` (`0x00504630`) adds it to that project. The sum is taken once, so a kind that finishes
mid-share does not change the others' shares that turn.

**A project finished** (`0x0050466f`: the points at or over the cost, compared before the store (`fcom`
`0x0050466f`, then `fstp` `0x00504672`), so above 24-bit precision a sum that rounds up onto the cost is kept at the
cost and never finishes; the precision the sweep runs at is not read; points past the cost are kept in the
project and carried nowhere): the item's control record (`FUN_004d3d10`) is marked researched (`+0x10`,
`0x005046a1`) and its tier set to the project's level (`+0x14`, `0x005046c0`), and message `0x11` (vtable
`0x006fd800`, the item at `+8`, `mCat` at `+0xc`) is sent at once to its four listeners (`MsgBus_Send`,
`0x0040fb10`, through `Thing_DispatchMessage` `0x0050b550` by the listener's kind byte), in the order of the
listeners' thing ids, as every listener set is walked: in the shipped park set `0x11` is things 3, 5, 6 and 10, the
advisor, the analyser, the lab and the challenge manager (`parkfile.py -x`), so the advisor's post comes before the
lab's raise:

| Listener | On message `0x11` |
|---|---|
| the lab (kind `0xe`, `Lab_OnMessage` `0x00503d40`) | `LabProject_MakeNextUpgrade` (`0x00504580`) writes the item's next tier with a research cost at the table's end and counts it in `mNumProjects` (`0x00503da2`); `Lab_RaiseHighestGroup` (`0x00503b80`) for kind `mCat`; then message `0x12` with `mCat`; then the end test (below) |
| the advisor (kind `0xb`, `Advisor_OnMessage` `0x0059b060`) | by `mCat`: 0 → message `0x5d`, 1 → `0x5f`, 2 → `0x60`, 3 → `0x61`, 4 → `0x5e`: rows 93 to 97 (`ResearchRide`, `Addon`, `Shop`, `Sideshow`, `FeatureResearched` in row order), scores 150, 130, 130, 110 and 90 in `Advisor.sam` (each score function a bare read of its key, `0x0059e7f0`..`0x0059e830`); posted (`AdvisorPost_Make` `0x0059b590`, `Advisor_Post` `0x0059a940`) about no thing, the item the record's second word (`+4`), which the tag's text names; any other kind logs "unknown research category" |
| the park analyser (kind `0xd`, made by `FUN_004c58c0`, subscribed in `FUN_004c7a90`; handler `Analyser_OnMessage` `0x004c7370`) | the item into its tab's ring of the recently researched (`0x004c73ab`): ring `0x18d2` + the item's `Info.WhichUIType`, so at `+0x1f068` + 0x14 × tab, `mTemp` `+0`, three `mData` words from `+2`, `mCurrentEntry` `+8`, `mNumEntries` (3) `+0xc`, `mWrappedAround` `+0x10`; the entry steps on, to 0 and wrapped past the last, and takes the item. Nothing else pushes it. A tab past 3 would write over the next fields; no researched item is of one |
| the challenge manager (kind `0x13`, `ChallengeManager_OnMessage` `0x004d2860`) | for `mCat` 0 only, challenge type `0x10` with 1 (`FUN_004d27a0`) |

**The group.** `Lab_RaiseHighestGroup( kind )` takes the kind's projects at or under its highest group and their
share done in whole per cent; while that is at least the dword at `0x00785540 + 4 × highest` (`0x00503c19`) and
the highest is under 7, the highest goes up one. That table is `ResearchTech[highest + 1]`: q198's schema walk
(`q198/decode/scratch/balschema.txt`) puts `ResearchTech[0]` at `0x0078553c`, slot 317, and both park files bear
it out: each kind's highest group is 1 in Alexah's fresh `restart.INTS` and in `Easymode.TPWI`, where reading
`ResearchTech[1]`, 0, would have raised it at once. So with `Standard.sam`'s 0, 0, 80, 85, 85 a kind's group 1
opens group 2 at 80% done, group 2 opens 3 at 85% and 3 opens 4 at 85%; `ResearchTech[5..]` are set by no file
and were not read. A raise with any of the kind's projects not done posts advisor
message `0x85` + kind for kinds 0 to 3, rows 133 to 136 `NewResearchGroup*`, score 40; kind 4 posts nothing.
Each of the four rows is Full Simulation's alone (the row's `+0x0c` nought): in Instant Action `Advisor_RowScore`
(`0x0059bf20`) answers -1, and `Advisor_Post` slots the message at -1 all the same, never to be said (the original,
`q702/orig/a-advisor.log`: `0x85` at -1 beside `0x80` at 20 from the finish to the end of the run).

**The next project.** Message `0x12` reaches kind `0x11`'s handler (`FUN_0050ca50`), whose case `0x12` runs
`Research_OnKindFinished` (`0x004aaec0`, through `0x00481520`), screen open or not: unless the kind's current
project is set and open (`Lab_KindHasOpenProject`, `0x00504040`), `Lab_PickNextProject` (`0x005034f0`) takes the first project of the
kind, open and at or under its highest group, after the current one; else the first such before it; else none,
the current set to -1. For kind 4 a project is eligible only while the item it upgrades (`AddOn.UpgradesId`,
`+0x284`, or the item itself) is researched and, where that item has a `UsageInfo.GoldenTicketCost`, unlocked
(`FUN_004d4b70`). With the screen open (`[0x007cc1d8]`) the panel is refreshed (`0x004aa120`, `0x004a9bd0`).

**The end of research** (`0x00503e15`..`0x00503fb7`): when every project in the table is done, the player's
progress (`[0x00f7c58c]`, made if absent) marks this park all researched (`PlayerProgress_MarkParkAllResearched`,
`0x005b0ca0`: byte `+0xb9` of its current park's record). In game type 2, Instant Action, the lab then raises
advisor message `0xa7` (row 167, `0x00503ee9`) and the world builds the "End" feature (`World_MakeEndFeature`); in type 0,
Full Simulation, the same only when every park record of the player's is so marked
(`PlayerProgress_AllParksAllResearched`, `0x005b0c40`, `0x00503f8d`); in type 1 nothing more. The current park's
record is the progress's `+0x48`, set on entering a park by its theme's name (`PlayerProgress_SetCurrentPark`,
`0x005b08a0`, which makes one where the player has none). `+0xb9` is the record's `mAllResearchCompleted`, the last
byte of its theme record in `gms.dat` (`0x005b1163`; `saves.md`, "`gms.dat`"); a new record zeroes it
(`0x005b0b4e`), and the all-parks test is its only reader. The tree holds one record a theme, made at the player's
selection (four for the disc's four themes), so in Full Simulation one park finished says nothing. Row 167 is posted about
no thing, as rows 93 to 97 are. **The End feature** (`World_MakeEndFeature`, `0x00516b00`) is the catalogue item
whose name at `+0x390` is `End` (a wide compare with the string object `0x007cf9c0`), made as an object at
(0, 0, 0) (`FUN_004db090`, `0x00516b8b`), its thing id stored at `0x007cf4f4`; with none it logs "Could not find
the %s feature" and stores nought. Then `FUN_0048ae70` takes the screen (above, "Everything else that calls it") with
the handler `0x0048a970` (above, "The End feature's handler") and puts the camera in the End's own ride view, on its
model's `camera` node, while its script flies the planes and fires the fireworks (above, "The End feature's view").

**The automatic mode is the screen refused, nothing more.** The efforts and the work load have one writer
each besides the set-up and the load, `Lab_SetEffort` (`0x00503870`) and `Lab_SetWorkLoad` (`0x00503900`), both
called only by the research screen, `FUN_004a96b0`, which Instant Action never opens (`hud.md`, "The research
opener and its two boxes"). So an Instant Action park researches by its file's efforts and work load for ever,
only while it has a researcher; the screen's arrows are the only other writers of a kind's current project
(`Lab_PickNextProject` forward, `Lab_PickPreviousProject` `0x005036a0` back; `hud.md`, "The research screen's panels"). `mLabActive` is set again when a researcher is made
(`0x00502600`) or removed (`0x00502920`), from the word at world `+0x1da742` (`FUN_005041f0`); a change posts
message `0x13` with 5 or 6, whose handler `FUN_00481af0` does nothing in this build (its one call is the empty logger `0x005da3c0`).

**In Lost Kingdom's Easymode** (`q245/easymode-lab.txt`, `parkfile.py -l`): efforts 100, 15, 30, 25 and 0, work
load 85, and the one researcher, grade 2, has Easy ability 12, so a hand-out gives 6, 0.9, 1.8, 1.5 and nothing:
the file's points, 30, 4.5, 9 and 7.5, are five hand-outs each. Every one of its eight ride projects has
`Upgrades[1..2].CostOfResearch` 0 in its `Easy_` layer, so a finished ride appends no upgrade: tiers 1 and 2 are
marked researched at once, and the end is reachable. **What the decode gives the build to meet** (`lib/labsim.py`,
these rules in float32 over the file, every hand-out taken (the stock researcher is not: his rest, 93 on tick 755 and
0.06 a sweep down in `q134/orig/a.log`, reaches `RestLevel` near tick 2230, when he walks to the Staff Room and rests,
handing nothing out in states 2 and 3; so each tick below is a floor and the hand-out count is the prediction), the
first on tick 760 as Q134 counted them):
Dino Karts (1150) researched on the 95th hand-out, tick 2640, with the rides' group raised to 2; Stone Head
(1414) on the 162nd, tick 3980; Laughing Hyenas (1304) on the 384th, tick 8420; Fries (1212) on the 496th, tick 10660,
its float sum landing on 450 to a rounding; the last of the 24, Super Toilet (1419), on the 1177th, tick 24280, about twelve
minutes in, and with it row 167 and the End. `labsim.py` takes `ResearchTech[5..]` as nought, as the executable's
image holds them (`0x00785550` on, read in Ghidra), which moves only when rows 133 to 136 are posted: no Easymode
project is above group 4. **The precision is the sweep's 53-bit** (the control word `0x27f` read in the running
park, `advisor-park.md`, q343): `labsim.py --fpu 53`, its default, shares in doubles and compares the double sum
before the float store; `--fpu 24` gives the same 24 hand-outs, Fries included.

**The original, stock Easymode left alone** (Q701, `q701/orig/`, `labread.py` polling the live lab; its park
clock ran 1.4 times real time, so only ticks are compared): the file's lab on entering; Dino Karts finished on
tick 2920 at exactly 600 (594 on 2918: 280 ticks, fourteen hand-outs, lost to the researcher's rest), the rides'
current project 1 to 2 and their highest group 1 to 2 on the same sweep; Stone Head on 4660 at 250.5, the
features' current 8 to 9, no raise; Mayan Spinner (1108) on 5000 at 504. On tick 5526 its buy screen lists
DinoKarts at $6500 and Mayan Spinner at $750, each with the recently researched star (`orig/o-buy-rides.png`).

**OpenTPW** (Q701; `ParkResearch.SpendPoints`, one lab the level owns, which the buy list reads and
`StaffBehaviour.Lab` spends into): the share in doubles stored as a float, the double sum compared with the cost
before its float store, the control record's flag and tier, `LabProject_MakeNextUpgrade`'s project appended (the
table held at 256, where the original writes past it), `Lab_RaiseHighestGroup` against `ResearchTech[highest +
1]` (`ParkResearch.GroupOpensAt`), `Lab_PickNextProject` with kind 4's researched and unlocked test, all as above.
`ParkWorld` reads each kind's current project and highest group and `mWorkLoad`, and the park file is written
with the lab's projects, kinds and `mNumProjects` and each control's flag and tier (`ParkWorld.PutLab`). Message
`0x11` reaches the advisor and the analyser before the lab (`ParkResearch.Researched`, `ParkState.HearResearched`;
Q702, below), a raise with work left the advisor (`ParkResearch.GroupOpened`, `ParkState.HearGroupOpened`); counted
where the original sends it: a ride's to the challenges (`RESEARCH_FINISHED_TO_THE_CHALLENGES`). The end of research
is Q550's, below. The console's `research` prints the lab and the analyser's four rings. **In the running game** (`q701/run2/`, the final build; `run1/` the same on the build before a comment): the
file's lab on entering, the first hand-out on 760, Dino Karts finished on the 95th hand-out on tick 3020 (2980 in
run1), at 600, the rides on group 2 and project 2; its row on the buy list's rides tab where it had none on hand-out
60, picked by a click and put down on (69,25) for 6500 (`2-after-rides-tab.png`, `5-dino-karts-built.png`; beside
the original's list, `q701/side-by-side.png`); Stone Head on the 162nd, tick 4760 (4620), at 250.5, the features on
project 9. **The ticks between the two games are the researcher's rest**, not the lab: ours set off on 2199 (2210)
and ended on 2578 (2553), nineteen (seventeen) hand-outs missed against the original's fourteen, its rest from about
2290, so each finish falls on the same hand-out at a different tick (Q549).

**What a finish tells, in both games** (Q702; `q702/`, the stock park with Dino Karts at 576 of 600, lab thing 6
`+44`, so both finish it on the fourth hand-out). The original (`q702/orig/run.sh`, off-screen; `advisor.py`,
`labread.py`, `thingwords.py` on the analyser's ring): on tick 820 Dino Karts at 600, the rides ring from (-1, 3,
unwrapped, `0xCDCD` x 3) to entry 0 holding 1150, the advisor's record of `0x5d` said on the same tick, line 0 (sample
282), and `0x85` in a slot at -1; its buy screen's rides tab stars DinoKarts and ticks the Belly Bounce, and its state
heading sorts the three unmarked, the Belly Bounce, DinoKarts, reversed on a second click (`orig/sorted-pair.png`).
OpenTPW (`ParkAdvisorMessages` rows 93 to 97 and 133 to 136, `ParkResearchedRings`, `ParkBuyScreen.StateOf`,
`ParkWorld.RecentlyResearched` and `PutRecentlyResearched`; `q702/run1`, 9 of 9, and `run2` on the final build): the
same tick, the same order (the post with the item, the ring, the raise's post at -1), the line said on tick 820, sp_282 found in the game's own mix, tag 95 reading "Your scientists have researched
the Dino Karts ride!", the star and the sort as the original's (`q702/side-by-side.png`), and a saved park holding the
ring.

**The end of research, in both games** (Q550; `q550/alldone576.TPWS`, the Q702 file with every other project at its
cost, so only the rides' kind is funded and Dino Karts takes 10.2 a hand-out: `labsim.py` puts the end on the third,
tick 800). The original (`q550/orig/run.sh`, off-screen, started on `dino576` first because it enters the park on
the newest file; `labread.py`, `advisor.py`, `progress.py`, watches on `0x007cf4f4` and `0x007c24d0`): on tick 800
Dino Karts at 606.6, the rides' highest group 1 to 7 and their current project -1; the Instant Action player's four
park records, the jungle one current, and its all-researched 0 to 1 alone; `0xa7` said on 800, line 0, with `0x5d`
waiting at 150 and said on 850; the End feature's thing id 0 to 43 and the keys taken. Its frame is the End
feature: the gadget and the money gone and the view turned up to a blue sky with stars and fireworks over the park's
edge, no banner. Escape put the id back to 0 and showed UITEXT 471's box with its cross over the park, the gadget
back; the keys byte read 1 throughout, as the box takes the keys again itself (`FUN_0047f020`, `0x0047f241`). The
park's tick stood under the box. OpenTPW (`ParkResearch.AllDone` and `MarkAllResearched`, `Level.EndOfResearch`,
`ParkAdvisorMessages` row 167; `q550/run2`, 8 of 8 on the final build): the same tick and figures, the posts of 93,
then the mark ("ref's jungle record marked all researched"), then 167 at 10000, said on tick 800 with sample 452
(found in the game's own mix) and 93 after it; `gms.dat` saved with jungle's byte 1 and the other three 0. The End
feature then follows (above, "OpenTPW builds it"). In Full Simulation (`q550/confirm-fs.py`, the private player's `gms.dat` put to mode 0;
`stone240-fs.TPWS`, Stone Head last, as a ride's finish there appends its costed upgrade and kind 4 has no effort in
the file): with the other three records 0 the jungle one is marked and nothing follows (`run7`), with them 1 row 167
is said on the end's tick (`run8`). The original's Full Simulation arm is the listing's alone.

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
  away**: the orbit camera returns to where it was before first person. Only the rotation asked for is put back: in
  first person the update writes the walk's heading to the asked `0x00790a38` and the drawn `0x007909e0` alike
  (`0x0042bad3`, `0x0042bae9`), so the drawn one is left at the walk's and eases round to the saved one by the orbit's
  rule ("The turn"). **Measured** (Q626, `original/campoi.py`, `q626/orig/b-poll.log`): entered from (450, 165) at 180,
  the copy read 450, 165, 180 and the viewer stood at the pick, (481.897, 166.285); turned to 278.14, asked and drawn
  alike; C put back 180.00 and (450, 165) by the next poll, the drawn rotation then 240.24 and at 180.00 1.36 s later.
  **The zoom is not saved:** the wheel still reaches `FUN_0042a760` in first person (`0x00488b6a`, `0x0042a91d`), which adds
  or takes 10 with no flag test, and the orbit keeps the change (clamped to 70..130 by its update, `0x0042c621`). Read from
  the listing only, never turned in the original's first person. OpenTPW's first person reads no wheel and counts nothing.
- **The sprites swap.** Entering ends by calling `FUN_00542420` (`0x0042af85`), leaving by calling `FUN_00542640`
  (`0x0042afba`). `FUN_00542420` runs only while `DAT_00763f80` is 1, sets it to 0, and reloads every loaded bank
  whose `+0x205` (the `.ESP` byte `0x10C`) is set from its `.fpc` (the static at `0x00875fe8`, built from
  `0x00747970` at startup); `FUN_00542640` is its mirror, run only while the flag is 0, back to `.tpc`
  (`0x00876270`). Each frees the bank's old pictures and reloads them through `SpritePack_Load`. `DAT_00763f28`,
  which picks `.tpc`/`.fpc` over `.tps`/`.fps` (`0x008757a8`, `0x00875598`), is 1 in the image and nothing writes
  it, so the `.tps`/`.fps` arms are never taken. An `.FPC` picture is the same figure seen from ground level
  (FileFormats, "Sprites"). The ride view's `FUN_0042a560` also ends in the swap (`0x0042a6ee`): to `.FPC` when its
  second argument is 1 or flag `0x02` is set, otherwise back to `.TPC`. `FUN_004e15b0`, the "Ride it!" call, passes
  the item's `Info.FirstPersonSprites` (`+0x80`), which every ride but the four tours reads as 1 from its theme's
  `Rides.sam` ("The End feature's view"); the other callers' are not traced. OpenTPW reaches only the End's ride view,
  which keeps the `.TPC` pictures, and nothing here reads `FirstPersonSprites`.

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
- **Three of the four ride views are counted, not built** (above, "An entrance is shut to the viewer"): a track
  ride's lead car, a TOUR record's car and a coaster's node (`docs/QUEUE.md` Q246).
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
cells beyond the 96 by 85 park - it cannot move at all. `Leave` puts back the point and yaw `Enter` saved (Q626), at
once: ours keeps one rotation and eases none (Q23), so the original's turn back from the walk's heading, over about a
second, is not drawn.

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

    category 0      nothing          -> answers 1, the click taken
    category 4      a placed object  -> FUN_00486920, the per-object window dispatcher
    category 7      a guest          -> FUN_004b79b0
    categories 8-c  the five staff   -> FUN_004b6080, the staff detail window

**The person and the object are two values** (Q474). Categories 7 to `0xc` open the person `FUN_0048c920` answers,
`[0x007c2650]`; category 4 opens the object `FUN_0048c930` answers, `[0x007c2654]` (written by `FUN_0048c940`,
`FUN_0048c960`, `FUN_0048c980`), once it has asked the cell `[0x007b05cc]` again (`FUN_00527e80`). The frame's picker
`FUN_0046c110` (`0x0054e34f`) stores the first thing on the picked cell at `[0x007b05b0]` (`0x0046c16e`, `0x0046c1b2`)
whoever the person is, and the click's `FUN_0048c960` (`0x00487a5d`) snapshots it into `[0x007c2654]`; the hover reaches
`[0x007c2654]` by none of its writers and asks the cell only for its own category (`0x0048734c`). So the object is the
cell's, never the sprite pick's person `[0x007b05ac]`, and a guest under the pointer
with no locator and no Shift (who falls through to the cell's categories at `0x00487231`) leaves a click on the ride
beside them to the ride. Measured in the original (`q474/orig/`, `PREDICTION.txt`): the stock park, the pointer at
(460,280) on the Belly Bounce's cell (52,23) read category 4 with guest 51 (kind 1) taken, and its click kept object 13
and opened the Belly Bounce's window. Here `ParkPicking` keeps them apart (`PersonUnderCursor`, `ObjectUnderCursor`)
and `Level.ClickWorldAt` takes the window of each from its own.

**A left click reaches the mode's own handler only when `FUN_004879d0` returned non-zero** - i.e. only
when the world click did not consume it. A fixed "click then dispatch" sequence is wrong.

### Ctrl+click buys another of a placed thing (Q635)

The help row "Hold CTRL and click to buy another" is category 4's other arm (`0x00487a7b`..`0x00487bf4`). With the
cell's object found, `GetAsyncKeyState( 0x11 )` (`0x00487ac0`) (`VK_CONTROL`, either Ctrl; Shift and Alt are not asked here) decides:

- **Ctrl up:** the object's window (`FUN_00486920`), as above.
- **Ctrl down:** the thing's item `[thing + 0xe]` is looked up in the control table (`FUN_004d3d10( item, 0 )`) and its
  price `+4` set against the bank (`FUN_006ad810` on `FUN_005195d0`'s thing). **A price above the bank** plays `cat_ui`
  `0x1D` (`0x00487b56`) and answers 0, with no window and no tool. **Otherwise** it installs the carry shell
  `FUN_0046c5a0( 4, item )` - the buy screen's own purchase, so nothing is charged until the put-down - and
  `FUN_0052f1b0( [thing + 0x10], 1 )`, which turns the hand to the clicked thing's angle. No window opens, and no
  golden-ticket test is made: the clone of a mystery ride asks for its cash price alone (read, not run).

**What happens at the put-down** is `FUN_00524960`'s, whose third argument is the modifier word (Ctrl alone is `0xC`;
"The commit hands the player the queue tool"): a thing with **no queue** stays in the hand under Ctrl alone
(`0x005253b8`; the move's at `0x005259e2`; the add-on arm at `0x00524d6d` also keeps the tool, but always resets the rotation -
`je 0x005259fc` lands on the reset call `0x0052f1b0`, as the path without Ctrl does; by the listing, not run), and a thing **with a queue** always hands the player the
queue tool (`FUN_0052f580( 3, 0 )`), Ctrl or not, so a ride is never placed twice in a row; Ctrl there keeps only the
rotation (`0x005259fa`). A right click puts the queue tool away (`q635/orig/b-run.out` 1-6-rclick and `c2-run.out` 6-rclick, tool 3 to 0; `b-run.out`
2-6-rclick, tool 4 to 0, was a right click on a held ride, the Belly Bounce, item 1100, whose put-down 2-4 had not taken), so a second ride is a second
Ctrl+click on a placed one.

Measured in the original (`q635/orig/`, `PREDICTION.txt`; the stock park off-screen, the camera 470,270 125 180):
Ctrl+click on the Belly Bounce (51,23), category 4, took the tool word `[0x0081ae2c]` from 0 to 4, the item
`[0x008186e0]` to 1100 and the rotation `[0x0081d7a4]` to 0 (the Belly Bounce stands at 0, so the turn is read, not
seen), with no window (`[0x007c2658]` 0) and the bank unmoved. The turn is seen on a Small Toilet (thing 22, (55,16),
standing at 270; `q672/orig/a-run.out`): Ctrl+click took the tool to 4, the item to 1402 and the rotation to **270**, no window,
88112 unmoved; put down plain on (41,24), 88112 to 88012 and the tool to 0 (`a-3-over.png`, `a-4-put.png`);
the plain put-down on (48,31), in a fresh load (`c2`), took 88112 to 87612 and the tool to 3 (`c2-4-put.png`), and in the
first load (`b`) on (41,22) 88181 to 87701 (480; the bank also moves by tens between readings with nothing done, 88201 to 88181
from 1-2 to 1-3, so 500 less the gate's takings is not the whole account). The Drinks Shop (43,30) Ctrl+clicked took item 1203; put down with Ctrl held on
(45,23) the bank fell 650 and the tool stayed 4 with item 1203, and a plain put-down on (45,26) fell 650 again and put
the tool away (`b-3-4-put-ctrl.png`, `b-3-5-put-plain.png`). With the bank held at 50 (`thingpoke.py`) the Ctrl+click
left the tool at 0 and `brk.sh` caught `0x00487b56` once, `esi` 500, the Belly Bounce's price (`b-brk.log`).

**OpenTPW** (Q672) builds the clone in `Level.ClickWorldAt` before the window, on Ctrl whatever Shift and Alt are:
`ParkBuilding.CarryAnother` asks the item's price against the bank alone (no buy tab, no golden tickets) and holds it at the
clicked thing's angle; dearer, `cat_ui` `0x1D` and an empty hand. The put-down's half is `ParkBuilding.PlaceCarried`'s (the
shop kept under Ctrl; a thing with a queue handing over the queue tool). Measured (`q672/run5`, seven of seven): the Belly
Bounce's Ctrl+click holds 1100 at 0, no window, 88112 unmoved; put down on (48,31) for 500, the queue tool, a right click
puts it away; a second clone on (44,24), 500 again; the toilet thing 22 held turned 270 and built on (41,24) as thing 45
turned 270, 100; the bank at 50 refused with the sound and no window; without Ctrl the window. Side by side:
`q672/side-by-side.png` (the views face opposite ways). The put-down on (41,22) that the original's run `b` made is refused
here, "(40,21) is a path", where the original lets a footprint stand on path (Q37).

### Picking is a real ray cast, not a grid lookup

`FUN_0045bf90` builds a camera ray from the cursor, walks the terrain height grid testing the two
triangles of each cell, then marches the ray testing object meshes. The hit point becomes a packed cell
id in `FUN_0045d560`:

    DAT_007b05cc = y * 0x80 + 1 + x        -- identical to this project's y*128 + x + 1

Written as a **16-bit** store. The cell is held to columns 0 to 94 and rows 0 to 83 (`MapInfo`'s 95 and 84), and a ray that meets nothing leaves the last cell standing ("The picker never hands the mode a cell off the map"). The "y" is really the **Z** component of
the world hit point. The hovered *thing* is latched separately into the mouse-manager object at
`0x007b05a8`, and a click **snapshots** it (`FUN_0048c960`) rather than re-reading it live.

**The thing under a sprite** (Q194, Q468): `FUN_00532bd0` stores the pointer at `0x008bd4f0`/`0x008bd4f4` as x / 1024 − 1
and y / 767.5 − 1 of its 2048 x 1536 position (`[0x00faa540]`, `[0x00faa544]`), the screen as −1 to 1, down positive.
When the sprite draw has latched a sprite under it (`DAT_008bd4fc`: cleared at each pass by `SpriteList_Draw`, set at
`0x0058931a`), it takes that sprite's script (`FUN_00475ef0`: the script whose `+0xd0` is the sprite) and walks the hovered
cell, then its eight neighbours (pairs at `0x0081ed40`, filled by the initializer `0x00532b30`: (0,0), then (−1,−1),
(0,−1), (1,−1), (−1,0), (1,0), (−1,1), (0,1), (1,1)), returning the first thing of kind 1, 4, 5, 6, 7, 8 or `0x12` on the
cell's thing list whose `+0x0c` script is the sprite's; nought otherwise, and off the map. Its one caller, `FUN_00540b30`
(from `FUN_00475430`), stores the answer at `0x00877350` and hands the previous one to `FUN_00423b50` on `0x007b05a8`.

**The latch** (`FUN_005890e0`, `0x00589259` to `0x0058931a`, from `Sprite_BuildQuad`'s draw of each sprite of the list
`[0x008c47a0]`, count `+0x2404`). A sprite is tested when its flags `+0x10` carry `0x200` and `[0x008bcbc8]` carries
`0x1000000`. Its place `+0x0` is (x, y, z, w), the draw matrix `[0x0087b088]` times its world place `+0x14`; its frame
`+0x28` gives the width `+0xc`, height `+0xe`, the reference `+0x10`/`+0x12` (128) and the origin `+0x14`/`+0x16`
(signed, negative). With s = `+0x2c` × 20 (`0x0070200c`) and G = `0x008bcbcc` (the screen's height over its width, 0.75):
L = origin x / 128 × s × G and R = (width + origin x) / 128 × s × G, both negated when flag `0x10` reflects it; the
height's span is height / 128 × s. The pointer must lie strictly inside the circle

    (px − (x + L) / w)² + (py − y / w)² < 0.0002 × (span across² + span down²)          (0x0070203c)

about the picture's left edge (its right, reflected) at its origin's height, the radius not divided by w: the same
size on the screen at every zoom. Of two, the one with the smaller w wins (`0x0058930b`: latched only when its w is
below the latched sprite's `+0xc`). w is the eye's depth times sin 45, the projection's `m23` for the 90 degrees the
park hands `FUN_00578be0` ("The lens"). Measured in the original (`q468/orig/`, `original/spritepick.py`, `PREDICTION.txt`):
the latch the decode predicts from every flagged sprite equalled the game's on 164 of 185 polls, the rest a frame
apart; every sprite latched was a person's (kinds 1, 4 to 8), flags `0x10001200` or, reflected, `0x10001210`; G read
0.75 and `[0x008bcbc8]` `0x1444100`; Mike Cooper's w read 77.78 at zoom 110 and his place equalled the matrix times
his world place to the last digit; his r² read 0.014 to 0.019 (pictures 35 to 41 by 50 to 54), guests' 0.006 to 0.009;
across and down the latch went where the decode's d² passed r². With the locator's camera following him and the
pointer resting, the pick named him while his cell went (51,21) to (56,21). Not measured: whether any sprite but a
person's body carries `0x200` (a rider's head on a ride, a balloon, a thought bubble). Here `SpritePick` latches over
the people's bodies as the last frame drew them (`ParkGuestSprites.Drawn`) and `ParkPicking` takes a person only so;
the cell's list answers only for the object, kept apart from the person (`ThingOn( people: false )`; "The person and
the object are two values" above), and a console `worldclick` on a cell takes the list's head as the person where it is one. The console's `circle [thing]` prints each drawn person's circle in window pixels.

**The size a sprite is drawn** (Q470). The same routine lays the quad: each corner is the place (x, y, z, w) with the span
added to x and y, z and w untouched (`param_2[0]`, `[4]`, `[8]`, `[0xc]` across, `[1]`, `[5]`, `[9]`, `[0xd]` down, the
`0x80000` branch on z instead), so the span is in clip space and is divided by w with the place. The projection
`FUN_00578be0` puts cos of half the handed angle on x and y (`m00`, `m11`) and its sine into w (`m23`), the screen scale
then 1.5 on x and 2 on y; so a span s is s / (2 cos 45) = s / 1.414 of the world square on to the eye, at every depth, and
a picture H px high stands H x 20 / 128 / w x 384 = H x 60 / w px of the 1024 x 768 frame (W x 60 / w across, the 0.75
and the 1.5 cancelling). The flag `0x200` is the sprite program's local 16 (`FUN_00542010` copies `+0xc4` into the draw
record's flags): a person's program sets `0x1200`; the rider's head's (`0x0074f558`) sets `0x3000080`; the balloons'
(`0x0074f480`, `0x0074f4c0`) and the thought scripts (from `0x0074f190`) write no local 16, so none of those is pickable.
**Measured** (`q470/`, `PREDICTION.txt`): under the mechanics' locator at zoom 110 (w 77.782) the original drew Mike
Cooper's 41 x 52 picture, origin 21, 45, from row 350 to 389 (the decode's 349.3 and 389.4), and three censuses of the
sprite list held people's sprites alone (18, 17 and 17 of them, each with `0x200`): no head, balloon or thought bubble was
in any, so those three rest on the static reading of their programs, not on a count. OpenTPW (`ParkGuestSprites.Extent`,
`Camera.ClipPerWorld`) draws the span over 1.414 in the world; before Q470 it drew the span itself, the same picture 46 px
over his origin's row of 720 where the original's 34.7 of 768 is 32.5; after it, the same 26 x 51 picture on both builds
went 45 to 32 up, 17 to 12 left and 8 to 5 right of his place. **Not shown** on the fixed build: a rider's head, a balloon
or a thought bubble; all four kinds go through the one `ParkGuestSprites.Corners` and `Extent` (the head with
`HeadScale`), so the change reaches them, but only a person's body was photographed. **The across, at any window shape** (Q628). The span
across is multiplied by G (`0x008bcbcc`, height over width) in the original, which runs only at 4:3, where G is 0.75 and
cancels the screen scale's 1.5; our projection carries the window's aspect itself, so the draw needs no G. The pick
(`SpritePick.Frame`, `ParkPicking.LatchFor`) puts the pointer and the place in the -1 to 1 box of the 4:3 frame the lens
draws the original's picture in (the window's height at 4:3 and wider, its width below) and takes G as 0.75, so its
circle is the original's in pixels at every window shape. Taken in the window's own box with G its height over its
width, as from Q468 to Q628, the circle reached 1.26 to 1.29 times the original's across and 0.94 to 0.97 down at
1280 x 720 (staff 25's 42 x 54 and 28's 30 x 56, `q628/run1`), and below 4:3 its middle stood too far out.
**Measured with two staff stood near** (`q628/`, `PREDICTION.txt`): in the original (`q628/orig/sweep.sh`, `staffpair.py`,
the clock held) Mike Man and Duke Mighten stood 11 px apart, w 68.43 and 68.99, r2 0.0178 and 0.0172 (Mike Man's by
hand 0.01778); on a 12 px grid over the farther's circle the game's latch equalled the decode's at 108 of 108 steps,
every step inside the nearer's circle latched the nearer and the farther only outside it (14 steps). So the reach is
large by the original's own rule: Mike Man's picture is drawn 26 x 49 px of the 1024 x 768 frame and his circle reaches
68 px across and 51 down about its edge, and of two people side by side the nearer takes nearly all the other's circle. Here, after Q628, at 1280 x 720 staff
25's and 28's circles are 66.3 x 49.7 and 64.0 x 48.0 px, the original's for those pictures at 720 high, and 160 of 160
grid steps away from an edge latched as the circles say (`q628/run2`). Below the origin both draw the picture
to its last row (Mike's feet 5 px under the origin), each tested nearer the eye: "What hides a person below their feet",
below (Q477, Q500).

**A reflected picture turns about its origin** (Q475). The quad's left corners (vertices 0 and 3) are x + L and its right
(1 and 2) x + R (`0x0058922d` to `0x00589256`), L and R the signed sides above; flag `0x10` negates both
(`0x005891e5` to `0x00589212`) and nothing in the routine writes a u, so the picture's left column is drawn on the right
at x − L: column c at the origin plus (origin x − c), the origin's column on the person's place both ways round. The
EBX the routine returns (1 for `0x10`, toggled by `0x60` and `0x80000`) only makes the caller OR the primitive's winding
flag 2 (`0x00589f15`). **Measured** (`q475/`, `PREDICTION.txt`), Mike under the mechanics' follow with the game process
stopped for each frame: reflected, his 31 x 52 picture (origin 20) drew from 505 to 527 of the 1024 x 768 frame against
the decode's 503.5 and 527.4, where a reflection about the picture's middle puts it at 496.6 to 520.5. OpenTPW
(`ParkGuestSprites.Across`, `Corners`) draws the same; before Q475 it kept the sides and swapped u, so a person hopped
sideways by W − 2 × origin x picture pixels at each turn between a direct and a reflected facing.

**What hides a person below their feet** (Q477, decoded). The word a world sprite is drawn with is
`Sprite_BuildQuad`'s (`FUN_00589b50`): it takes the picture's texture word (`Texture_BlendStateWord`, `0x00589b91`, into
the quad's `+0xc`) and ORs `0x800`, no depth write, only in its arm for the sprite's flag `0x80000` (`0x00589dd4`,
`or ah,8` at `0x00589e85`). `SpriteList_Draw` (`FUN_0058a770`) works out a word of its own (the texture's, `0x800` for the
sprite's `0x4000`, and `0x200000`, `0x20000` and `0x8000000` for its `0x200000`, `0x20000` and `0x10000`;
`or bh,8` at `0x0058a8c4`) but only as the key it groups the list by (`0x0058a910`, `0x0058a935`, `0x0058a9cb`): texture
and word, each group furthest first by `+8`. The draw loop (`0x0058aa13` to `0x0058aa4d`) hands each sprite alone to
`FUN_00589fd0`, which builds the quad on the stack through `Sprite_BuildQuad` and adds it with `0x00566220` (the batch
at `[0x00879ab0]`, which ORs `0x100000` under render bit `0x2000`). A particle's `0x4000` is its own:
`SpriteBatch_DrawParticles` ORs `0x800` for it (`0x0057c7f4`, `0x0057c7fd`) into the quad it builds and adds with
`0x00565f90`. Under render bit `0x80` (`0x0058a785`; `Game_StateMachine`'s
`xor edx,0x2080`, `0x0054fae2`, sets it with `0x2000` in a ride's view and in first person) the list is not grouped: it is
walked from its end, each sprite through `FUN_00589f90` (`FUN_00589770` after it unless the render word has `0x20000`) and
`0x00565f90`. Whether the batch's own sorts (`render-states.md`, "Three back-to-front sorts") still order the quads there
is unread, so that `FurthestFirst` also sorts in first person is not shown to differ. A person's flags (`0x10001210`) carry
no `0x80000`, so a person is depth tested (`LESSEQUAL`, `render-states.md`) and **written**. People's pictures are graded (texture flags
`0x51201252`, word `0x556`: alpha test at 16, blended), so their batches are sorted and drawn after every opaque and
cut-out one, among the graded ones furthest first (`render-states.md`, "Three back-to-front sorts"). The ground's word
is `0x1256` (`0x101256` fogged; `FUN_0056f670`, `0x0056f753`): drawn first, tested and written. The patrol and build
squares (`BlueprintMesh`, a model of the model lists) are graded too (`blue`, flags `0x510712e2`).

**The pull.** After `FUN_005890e0` lays the quad, `Sprite_BuildQuad` moves every corner's clip z to `z − 0.2 / (z + 1)`
(`0x00589f17` to `0x00589f59`: `[0x00702044]` 0.2, `[0x00702038]` −1.0) when the sprite's flags carry `0x200` (a
person's, "The size a sprite is drawn"), neither `0x40000` nor `0x80000` (their arms, `0x00589ce4` and `0x00589dd4`, end at
`0x00589f5c`, past the pull), and the render word `[0x008bcbc8]` lacks `0x2000` (it does in the park's view,
`0x1444100`; the views that set it are the ride and first-person ones, "The park view's colours"). With the projection's
z = Q (e − near) and w = sin 45 × e, near 0.1 (`0x0042aa4e`), that is `1/e' = 1/e + 0.2 sin 45 / (Q near w (z + 1))`,
about `1/e + 3.95 / e²`: at the locator's zoom 110 the person is tested as if 3.81 units nearer the eye (e 110.000 to
106.185). The draw matrix's z row is built with a far near 1000 (Mike's z 77.719 at w 77.782 gives Q 0.70718), not the
126 to 127 then in `[0x00768acc]`. `ClipToScreen_Fpu` (`0x00579690`, the x87 one of the transform's CPU variants, case 0)
writes the buffer's z as z/w × `[0x008bcc2c]` + `[0x008bcc30]` (4.33 and −3.33 read live), or `[0x008bcc34]` z +
`[0x008bcc38]` for a z under `[0x008bcc28]` (compared as integers; 0.311 read live), the "ui, linear, log" ranges
`FUN_005833c0` logs; the z-buffer is the first 16-bit format `EnumZBufferFormats` offers (the callback `0x00573660`), a
step about 0.43 units at depth 110.

**Measured in the original** (`q477/`, `PREDICTION.txt`; the stock park, Mike under the mechanics' follow at zoom 110,
the clock held by its paused flag `[0x007859a0]` (`0x00785970` + 0x30; not a real pause: `FUN_004030d0` reads the capture of the last real pause, `FUN_00403030`, while it is held, and `FUN_00403050` is not run, so the held span falls due at once when it is put back) so each trio of frames is one pose; `original/spritepick.py PID where 26
depth` reads the word, the pull and the buffer's numbers). As it runs, his shoes reach row 389 of the 1024 x 768 frame,
5 px under his origin's row, over the squares' blue wash. With 0.2 held at 0 (`memhold.py`) the squares wash over his
whole body below his hair (rows 358 to 389) and his shoes still reach 389: the ground 0 to 0.48 units nearer there lies
within about a step of the 16-bit buffer. Held at −0.05 (1.0 units away) the ground hides him from row 373 down
(predicted 372.7), at −0.02 from about 383, at −0.2 wholly. OpenTPW (`ParkGuestSprites.Pull`, `PulledDepth`) moves each
corner of a person's body along its line from the eye to the depth the pull gives (110 to 106.185; none in first person,
the one `0x2000` view built), and the people's quads write depth through the alpha test at 16 (`people.shader`), handed
over furthest first (`FurthestFirst`); the heads, bubbles and balloons drawn with them write depth too, their texture
words not read. **Measured** (`q500/`, `PREDICTION.txt`): Mike's own pixels reach his picture's bottom, up to 8 px under
his origin's row of 720, with the locator open and folded, where before they stopped at the origin and the squares hid
him up to 18 px over it (`q477/run1-main`); with the pull taken out and depth still written they stop at the origin, the
squares now kept off him above it; people behind the park gate stay hidden by its stone.

**A person's shadow** (Q480, decoded and measured). Each frame the ground's draw `Render_DrawView` empties a list of up to
four shadow lights at `0x008c1658` (`ShadowLights_Clear`, `0x00584b10`; `ShadowLights_Init`, `0x00584a70`, makes it at start) and, unless the render word
`[0x008bcbc8]` holds `0x20000`, hands `ShadowLights_Add` (`0x00584be0`) the direction of each light its list gives (`0x00576d1c` to
`0x00576d60`; a park has one, the sun). `ShadowLights_Add` normalises it (L) and, only where L·(0, 1, 0) × -127
(`0x00768ab0`, `0x00701ecc`) is over nought, keeps light i: the across `(Lz, 0, -Lx)` (up × L, unnormalised) at `+0x140 +
12i`, L at `+0x170 + 12i`, the darkness `trunc(-127 Ly) << 24` at `+0x1b0 + 4i`, an angle at `+0x1a0 + 4i` (for the model
shadows, not read here), and the matrix at `+0x40 (i + 1)`: the identity with row 1 `(-Lx/Ly, 0, -Lz/Ly, 0)`, which lays a
point's height flat along the light. The count is `+0x1c0`, the walk's cursor `+0x1c4` (`ShadowLights_First`, `ShadowLights_Next`, `0x00584e30`, `0x00584e50`).
Under render bit `0x80` (first person and a ride's view) there is no second pass: after each body `SpriteList_Draw`
calls `FUN_00589770` (`0x0058a800`; below, "First person's shadow"). In the grouped pass, after the people's pass `SpriteList_Draw` (`0x0058aa4f`) returns under `0x20000`; under `0x40000` it calls `SpriteShadow_Draw` (`0x00589990`)
for every sprite of its depth buckets (the ones being drawn) with the silhouette `[list + 0x2400]` (`alphkid`, loaded by
`0x00550950`), and with neither, with nought (the sprite's own picture); then it flushes the batch.
`SpriteShadow_Draw` takes only a sprite whose flags hold `0x200` and `0x10000000` (a person's body); per light,
`SpriteShadow_BuildQuad` (`0x00589410`) builds the quad in the world: across, the picture's columns `+0x14` to `+0x14 + width` over its `+0x10`
(128) times the instance scale × 20 (`0x0070200c`) × `DAT_008bcbcc` (0.75), along the across from the sprite's place
`+0x14`/`+0x1c`, both sides negated for a reflected sprite (`+0x10 & 0x10`); up, its rows the same way (no 0.75),
shifted so the foot is nought; the four corners put through the light's matrix (the height laid along the light), and each
then set on the land: the plane through its heightfield cell's near corner and that corner's two neighbours (the cell by
truncation, the far corners clamped; not the triangle split `FUN_004527f0` uses), plus 0.6 (`0x008bd4ec`, 0.25 under
render word `0x2000` or renderer 4, `0x00576a72`), the sprite's own height ignored (true only without the sprite flag `0x800`: see
the unreached branches below); then through the draw matrix
`[0x0087b088]`. Corners 0 and 1 carry the texture's top row and are the far end. Under `0x40000` the colour is
`0xff000000 | ~(darkness × 0x010101)` and the state word's low byte `0x13` (SRCBLEND ZERO, DESTBLEND SRCCOLOR: the
ground multiplied by the colour times the texel), with the picture's alpha test (16) kept, `0x800` set (no depth write)
and `0x200` cleared; without it the colour is the darkness word itself and the picture's own blend. **Measured**
(`q480/`, `PREDICTION.txt`; the stock park at medium, `[0x008bcbc8]` `0x1444100`): count 1, darkness `0x67000000`, L
(0.40825, -0.81650, 0.40825), across (0.40825, 0, -0.40825), m10 and m12 0.5, m11 0; with `0x20000` held the count
read 0 and every shadow went. On bare ground a guest's shadow left 0.591 and 0.602 of the red and green
(152/255 = 0.596; the frame is 16-bit, its blue there 4 to 7 of 31 levels, 0.543); Mike's, inside the mechanics' patrol
squares, shows through them (they are drawn over it), 0.556, 0.716, 0.835. Mike's shadow reaches 27 px up and 29 right of
his place (1024 x 768, zoom 110; 26 and 29 reflected): a thin streak up and to the right, the way the sun travels.
**Branches no body reaches** (the flags measured on bodies were `0x10001200` and `0x10001210`): `BuildQuad` negates the heights
under sprite flag `0x20` (`0x00589558`); turns the quad by `-[sprite+0x30]` through `0x00591430` under `0x80`
(`0x005895d7`); under `0x800` adds `[sprite+0x20]` to the lift in place of 0.0 (`0x00702034`, `0x00589613`); and the ground's
draw skips both `ShadowLights_Clear` and the Add when the view's flags lack `0x800` (`0x00576d01`) and adds only lights whose
type slot answers 1 (`0x00576d36`).

**First person's shadow** (Q480 and Q485, second reader): under render bit `0x80`, `SpriteList_Draw` (`0x0058a785`, the
render word measured in first person is `0x01446180`: `0x80` set, `0x20000` clear) walks the list from its end and calls
`FUN_00589770` (`0x0058a800`) straight after each sprite's `FUN_00589f90`. It takes a body only (`0x200`, `0x10000000`:
`0x0058977c`, `0x00589785`), never reads `0x40000` and has no silhouette: the picture is the sprite's own
`[[sprite+0x28]]` (`0x00589790`, `0x005897b4`), the one `Sprite_BuildQuad` draws for the body. The word is that picture's
own (`0x00590c40`) with `0x800` (`0x00589845`, no depth write) and `0x200` cleared (`0x0058993c`); the people's `0x556` is
SRCALPHA / INVSRCALPHA, so it is an alpha blend and no `0x13` multiply. The colour is `BuildQuad`'s return, the light's darkness
word (`0x00589750`; `trunc(-127 Ly) << 24`, `0x00584d97`), black at that alpha, `0x67000000` under the jungle sun. It is added
with `0x00565f90` (`0x0058994e`), which under `0x2000|0x4000` calls `Fog_VertexSpecular` (`0x005747d0`, `0x00566123` to
`0x00566145`), so the vertex is black × (1 - f) with the fog colour as its specular × f: the result is dst × (1 - a) + fog × f × a
with a = darkness/255 × the texel's alpha, and **over ground fogged whole the shadow is the fog colour: it vanishes**. At tier 0
(`0x20000`) the walk draws the sprite alone. The grouped pass's `SpriteShadow_Draw` non-`0x40000` arm is the same own-picture
draw (texture `[[sprite+0x28]]` at `0x005899c1`, colour the darkness word at `0x00589aec`) added with `0x00566220`, unfogged;
only that arm is high detail's in the orbit view (Q521). Nothing yet measured it with a person in the band (code read).
OpenTPW: `ParkPersonShadows` (the geometry, tier, darkness, colour and `KindFor`), `ParkGuestSprites.WriteShadow` (one a body
drawn), `HeightfieldFile.CornerPlaneAt`, `MaterialFlags.Multiply` for the silhouette; in first person (`ParkGuestSprites.FirstPerson`)
the shadow is the body's atlas picture in `OwnPictureColour`, alpha-blended, no depth write, through `people.shader`'s fog, at any
tier but 0; the own picture of tiers 2 and 3 in the orbit view is counted, `SPRITE_SHADOW_OWN_PICTURE` (Q521). **Not the same**
in first person: all the shadows are drawn after all the bodies, where the original's walk adds each after its own body (differs
only where a shadow overlaps another person's graded edge; the walk's batch sort is unread). Measured (`run2`): 0.595 of every channel, his far end 24.5 up and 27.7 right
(scaled to 768) on a reflected pose, 26.7 and 25.6 on the original's unreflected one (`run1`). **Not the same**: a tool's squares are drawn before the people here and write depth 1.5 over the land, so a
shadow under them is hidden (Q520); and every body drawn casts one, where the original's buckets may leave out a person
off the frame whose shadow would show (the buckets' cull is not read).

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
refuses another thing's cells, red unless that thing's `+0x54` (`Info.OverwritePriority`) is below the placed item's (strictly: `0x00535d66` to
`0x00535d73`). The category files all declare 5; an item's own `.sam` overrides it only for features, 45 of the
shipped item files setting 1 and 48 setting 0 (FileFormats `sam.md`; fork watch 2026-10-16, `verify-world.md` C2).

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

**OpenTPW does the first write for every purchase**, the console's `buy` included, which has no commit:
`ParkBuilding.LayEnds` lays the two ends, puts the entrance's bit back (`RejoinEntrance`) and walks the queue
once. The placer's own walk (`0x00529890`) comes before the bit is back, so over a path it finds no queue, and
is left out: a deviation in order, with the same cells and the same count at the end. A Crazy Ape bought on
(41,22), its queue cell laid over the path on (42,21), holds entrance links `0x01`, back of queue 2731 and one
cell, here and in the original's own file of the same purchase (Q256).

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
`+0x30`/`+0x34`, `Info.MapOffsetX` and `Info.MapOffsetY` (`saves.md`, "OpenTPW's writer, a made object's flags
and corner").

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
(`FUN_0052f1b0` at `0x005237f7`; the rule is "The add-on's snap", below), and set to the thing's own angle when a move picks it up
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
squares also brighten and dim as they wave (at medium and high: low's `LIGHTING` 0 gives every corner the mask, so the
scale changes no colour there, Q516). Alexah, who played the original: *"they were translucent.
They waved like a flag/water."*

**Over a built cell the square is lifted** (`FUN_00532fc0`, `0x005330ca` and its siblings; a kart or water piece's cell answers its own `.hmp`, Q776, `park.md`, "A track square's lift and walls"). The record's height is
`ceil10(trunc(FUN_00452ae0(x, y, null)))` + 1.5, where ceil10 is `(t + 9) / 10 * 10.0` in integer division. It is
lifted only over a cell of mType 4, 9, 10, 7 or `0x1e`, or of track kind 11, 12, 16, 17 or 25; elsewhere the lift is
nought. `FUN_00452ae0` finds the thing on the cell in two steps. First it reads the cell's track-layer record
(`FUN_0053bf30`): its object at `+4`, or for kind 12 or 17 the object of the cell its `+0x10` names. Failing that, it
reads the cell's thing id (`FUN_00527e80`), which must be an object (type byte 3). From that object it reads the
`.hmp` at `+0xcc` and the root node's matrix (model `+0x78`; translation `+0x40`, `+0x44`, `+0x48`). It carries the
cell's corner back into the object's unturned space by its rotation at model `+0x10`, a quarter turn at a time
(`0x00452bb0`..`0x00452c1d`). It answers the `.hmp`'s cell-grid byte × 1/2.55 plus the root's world height (the height the model maker gave it,
"Where a placed object's model stands"). With no
object, it answers the ground height at the cell's corner (`FUN_004527f0`). **The wave branch of `FUN_0053ddd0` then
adds the ground height at each corner again**, so on raised ground the ground counts twice: once in the object's base
or the fallback, and once here.

Measured in the original under Proton on 2026-10-01 (the face list at `0x00820b00`, BlueprintMesh `DAT_00871f64`'s
positions at `+0x64`):
- The Belly Bounce's (53,23) and (53,24) carry a record height of 21.5 (lift 20), and their corners are drawn at
  20.5 to 22.4 over ground 0.
- (93,21) and (94,21), track kinds 25 and 12, lift 20 too. Their object is a 2×2 thing of the track layer: object 42,
  root at ground 0, turned 180, with its own `.hmp`. The layer's kind-25 cells anchor such things (the roots of the two over (64,40) and
  (76,40) stand at 5.0); OpenTPW reads their `.hmp` for the lift since Q777 (`park.md`, "The boundary's things lift a square too").
- A lifted square also gets walls down to a lower neighbour, records at 11.5 and 1.5 on the same cell.

**The walls** (decoded and measured 2026-10-17, Q436). After the flat square (face 0), the `0x100` arm asks the four
neighbours through `FUN_004d99c0`/`FUN_004d96f0` in the order north `DAT_007cdba0` (0,−1), east `DAT_007cdbc8` (+1,0),
south `DAT_007cdba8` (0,+1), west `DAT_007cdbd0` (−1,0); off the map there is none. A neighbour's lift is found by the
same rule as the square's. While it is under the square's (north's loop in the arm for a square not refused, `0x0053337a`..`0x005333dc`, and in the refused arm `0x00533ad0`..`0x00533b22`; both truncated), a record goes
down at the neighbour's lift + 1.5 with face byte (`+0x0c`) **1, 4, `0x10` or `0x40`**, the neighbour's direction bit,
then ten higher, until the square's lift is reached: so a neighbour as high or higher gets none, and from 20 to the
ground two records, 1.5 and 11.5. `FUN_0053df30` lays such a face as a quad on that edge of the cell (north the least-z
edge, east x + 10, south z + 10, west the least x), its foot at the record's height and its top **10 higher** (`- −10.0`,
`0x00700e1c`), u along the edge from the corner it lays first (north (x, z), east (x+10, z), south (x+10, z+10), west
(x, z+10)) and v up it; `FUN_0053ddd0` adds the ground and the wave alike to foot and top. A wall's texture is 1 under a
red square (so it blinks with it), 2 under orange, and otherwise 0, blue, under `m_link` and `m_end` too
(`0x00533b07` pushes the 1 in the refused arm; `0x00533214` holds only the orange-or-0 of the others); its orientation byte (`+0x10`) is 0 where the square's is 1; its `+0x98` is `mode != 4`, so in mode 4
the walls glide with the carried thing's squares. Each wall laid under a square not refused **also zeroes the refusal
latches** `DAT_00816d58` and `DAT_00816d50` (`0x005333bd`, `0x0053357a`, `0x00533737`, `0x005338fc`, one site a direction;
the refused arm's walls zero neither). **That changes nothing a path or queue reader sees** (read 2026-10-19, Q540): every
answer of the mode-1 and mode-3 verdict that is not red leaves both at nought, but the queue's join (`m_link`, the strip's
last square, `d58` and `DAT_00816d60` set, `0x00535c78`), on a path cell, which only a track thing's height lifts; the
anchored click's walks (`0x87`, the stamp, `0x80`, `0x82`, `0x85`, `0x86`, `0x83`, `0x81`) carry neither `0x100` nor
`0x200`, the only ops whose arm of `FUN_00532fc0` asks the verdict; the click asks it itself only unanchored
(`0x0052727b` mode 1, `0x0052729d` mode 3, no latch clear first), and a join is always anchored (mode 3 is entered by
`FUN_0052f580(3,0)`, which keeps the anchor); and the other reader of `d58`,
`FUN_00539760`, runs under the track modes alone (`0x0e`, `0x13`, `0x14`, `0x16`, `0x1a`).
So the one wall that zeroes a set latch is a patrol edge the locator draws after a join's pass, over a lifted cell;
the next pass clears `d58` first.
Measured in the original (`q436/orig/a-5-squares.txt`, the stock park, Mike Cooper's patrol outline (51,26)-(54,27)): (52,26)
21.5 then faces 4 at 11.5 and `0x10` at 1.5 and 11.5; (53,26) 11.5 then 4 and `0x10` at 1.5; (51,26) 21.5 then `0x10`
and `0x40` each at 1.5 and 11.5; orientation 1 on the eight squares and 0 on the nine walls. **OpenTPW**
(`ParkBuildMarkers.WallsAt`, `WallCorners`) lays the same nine in the same order (the `strip` census's `| walls`), drawn
under the tops as the original's (`q436/side-by-side.png`). OpenTPW zeroes no latch under a wall: its `d58` lives inside a
pass (`ParkPathBuilding.Latches`), and no square its path or queue verdicts leave unrefused is lifted, on any of the stock
park's cells (`ParkPathToolTests.NoUnrefusedPathOrQueueSquareIsLifted`). Measured in both games: `hud.md`, "The patrol
edges", "Under a wall" (Q540).

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
ride's is 5; after a refusal that latches every later cell is red (which ones latch: `hud.md`, "The patrol
edges"; land outside the park and a type the queue may not be laid over do not), and any red switches the cursor to
`c_noplace.cur` (`FUN_0052f950`). Only bare ground is charged (`FUN_00535600`'s mode 3 with a path left out by its
caller), and the track-corner test is the path tool's, asked at the same site (`0x00535bd9`, `0x00535be8`) of every
cell the type-3 and type-`0x15` tests let through, before the type-0 answer and the path arm: bare ground, a path and
any other type. So a corner under the run's last path is red with `d58`, never `m_link`, and one under any other type
is red with `d58`, never the unlatched "other type" answer. **The anchor is always the first square**, so the cell before a new
ride's entrance is marked the moment it is placed.

**While a thing is carried** (mode 4) the placer runs twice a tick (`FUN_005234d0`): a test pass,
`FUN_00528a70( x, y, 4, angle, 0, 1 )`, whose failure sets `g_PlacerFailed` (`DAT_0081b604`), and a draw pass with both
flags clear (`0x005239ee`). `x, y` is the pointer's cell (`DAT_008186a4` = the preview cell; only an add-on over a track
moves it). Decoded 2026-10-25 (Q633):

- **The picture is centred on the pointer's cell.** The placer starts `w / 2` columns and `d / 2` rows back
  (`0x00528b24`, `0x00528b26`, integer halves of the descriptor's width and depth) and turns the picture about the
  middle of a five-by-five box (`(5 - w) / 2`, `(5 - d) / 2`, from `0x00528b2a`): at angle 0 column `c`, row `r` stands on
  `(x - w/2 + c, y - d/2 + r)`, and the sweep's other three arms (`0x00528f62`..) put it on the same cells as
  `MapDelta::Rotate` from a corner offset by the box. Its uncentred reading, flag `DAT_0081af7c`, is only ever written
  nought (`0x0052f074`). The put-down on the button's release is the same placer, so a thing goes down where its
  squares were.
- **The draw pass, rows from nought, columns within each** (the switch at `0x0052916c`): kind 0 nothing; a path, `+`
  or `#` cell (kinds 1, `0xb`, `0x10`) blue, unturned; any other cell, the entrance and exit among them, `m_front` (6)
  on row 0 turned by the angle's base bit (1, `0x40`, `0x10`, 4 for 0, 90, 180, 270; `0x00529688`) and blue unturned
  elsewhere; a station cell (`0x17`, `<` `>`) blue turned by south's bit `0x10` (rotated), with `m_direct` (5), turned
  the same, on the cell its facing points to (`FUN_004d97e0`), and one pointing off the map refuses. Then, for a picture with an
  entrance or exit, the cell before the entrance (one step against its rotated bit `H`) wears `m_enter` (3, op `0x104`)
  for a thing with a queue (`FUN_0052fab0`, `Info.HasQueue`) and `m_inout` (7, op `0x118`) for one without, turned by
  `Opposite( H )` (`0x005299bf`); for a thing with a queue the cell past the exit (one step along its bit) `m_exit` (4,
  op `0x118`), turned by the opposite of the exit's bit. A kart or water ride (`FUN_0052faf0` 1 or 2) also lays its first
  track cells' squares (`FUN_005286a0`, `0x00529b35`).
- **Any failure reds everything**: with `g_PlacerFailed` set the square arm of `FUN_00532fc0` lays texture 1 without
  asking the cell (`0x00533931`), the ends' cells too. Lifts and walls are any square's ("The walls").
- **A square's texture is turned by its bit** (`FUN_0053df30`): the UV pairs `0x00871ee8` holds, (0,0), (1,0), (0,1),
  (1,1), go on the corners (x, z), (x+10, z), (x, z+10), (x+10, z+10) as indices 0 1 2 3 for north `0x01` (or none),
  2 0 3 1 for east `0x04`, 3 2 1 0 for south `0x10` and 1 3 0 2 for west `0x40`.
- **The squares glide.** `FUN_0053c3f0` moves a point `DAT_00820afc`, `DAT_00820af8` half the way to the preview cell × 10
  every rendered frame (`0x00700de4`, 0.5), and in modes 4, `0xe` and `0x13` every square and wall below texture `0xd`
  is drawn that far from its own place as the point is from the cell (`0x0053df67`), its ground and wave asked at the
  shifted corner (`FUN_0053ddd0`; its 17 callers are 16 in `FUN_0053df30`, `0x0053e083`..`0x0053e444`, and `0x0053e580`). The no-glide flag `DAT_00874fc4` is only ever written nought (`0x005239f6`,
  `0x0053c000`).
- **The pointer** (`FUN_0052f950`, mode 4): cursor 7, `c_place`; 8, `c_noplace`, under the pass's refusal `DAT_00816d48`
  (`FUN_00527950`); `0x14`, `c_cash`, under `DAT_00816d5c`.

Measured in the original under Proton (`q633/orig/c-3-over.squares`, the stock park, a Belly Bounce in the hand by
Ctrl+click, the pointer on (48,31)): fourteen squares in this order, (47..49, 29) texture 6 turn 1, nine texture 0 turn
0 on (47..49, 30..32), (48,28) texture 3 turn 1 and (48,33) texture 4 turn 1, cursor 7; over (51,25), on the stock Belly
Bounce, every square texture 1 with walls over the ride, cursor 8. **OpenTPW** (`ParkBuilding.CarrySquares`,
`AnchorUnder`, `PlaceCarriedUnder`; `ParkBuildMarkers`, `TurnedUvs`, `Glide`; `Level.CarryCursor`) draws the same
fourteen in the same order on the same cells, puts the thing down at the centred anchor, and wears `c_place` and
`c_noplace`; the glide halves the distance each thirtieth of a second rather than each rendered frame, a said deviation.
Not built: a track ride's first cells' squares (`CARRY_PREVIEW_TRACK_START`), and the latch the passes set
(`CARRY_PASS_REFUSAL_LATCH`, Q536).

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

**The four gauges stand 10 in from their rows' top and bottom** (Q693): the ride's own fill `0x004ad890` (the ride
vtable `0x006ffbe8` `+0xc`, called by the shared opener at `0x0048cf55`) calls `FUN_00485e30( gauge, 0, 10 )` on
`0x3e16`, `0x3e18`, `0x3e19` and `0x3e17` (`0x004adc69`, `0x004adcc7`, `0x004add25`, `0x004add83`), so each skin is
handed its row less 10 above and below and none at the sides (`hud.md`, "The skill bar `0x2482`", says what the helper
writes): three rows of 45 draw 25 tall, Reliability's 44 draws 24. The text cells beside them keep the whole row.
**Measured** (`q693/orig/run.sh`, `uitree.py PID 0x007c24c8` on the stock park's Belly Bounce window): each meter's
`+0xc` the stream's (1315, 279, 1550, 324), (1315, 331, 1550, 375), (1315, 382, 1550, 427), (1315, 433, 1550, 478), its
`+0x30` and its kept rectangle (1315, 289, 1550, 314), (1315, 341, 1550, 365), (1315, 392, 1550, 417), (1315, 443, 1550,
468); the 1024 x 768 frame lights rows 144-156, 171-182, 197-208 and 222-234 in columns 658-774.

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
flag gates the staff dismissal box, UITEXT 397. Set, `FUN_0048cd10` opens `0x0047f020( box, 2, 0, 1, 0x0048cde0, 0, 0,
0 )`, Quit Game's shape: the cross does nothing, the tick (`0x0048cde0`) demolishes the thing handed in the box; clear,
it demolishes at once (`0x0048cd6f`..`0x0048cdc1`). **Either way it then zeroes `[0x007c2658]` (`0x0048cdc9`) and
closes**, so the close applies none of the window's buffered settings. Every object window's delete comes here (the
ride's `0x3e2b`, the shop's `0xc068`, and `0x004977fd`, `0x00499cf0`, `0x004b2396`, `0x004b510d`, `0x004b6930`). OpenTPW:
`ParkObjectWindow.AskToDelete`, from the ride's and the shop's windows.

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
`mPreviousRides` and the refusal beside it are cleared (`Peep.ForgetThing`, `0x004fb4ba`); the sound, at the seat node, at a walk-on rider's sprite (the point the `WALK` placer left, the origin where it hid
them: `ParkGuestSprites.WalkedOnRide`), at the origin for a thing without flag `0x20`, and at the feet otherwise; and the staff rest-area arms, after a handyman's toilet job or a mechanic's ride job is dropped, to idle, where it names the
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
  `0x00801f38`, which the thing sweep `FUN_00516380` drains on every 8th 31 ms tick. So the reads that follow are sound: `+0xe` is the
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
sale went through, where the original would carry a thing that still stands. A refused put-down plays `0xaf`
(`ParkBuildMode.RefusedSound`, Q442), the one call the path, queue and patrol tools share. **Counted, not built**: the
markers drawn while carrying. Letting a move
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
shared RNG parity, staff carry preview and placement-cell filters are
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

**What a hire makes of the candidate's costume.** The place-staff routine `FUN_0046c8e0` hands each of the five
constructors the cell, the candidate's grade byte, its costume byte (`mSubType`, `0x0046c9b9` and its kin) and its
name row. Four of them (`FUN_004d6b60` the handyman, `FUN_004d4340` the entertainer, `FUN_004d9eb0` and `FUN_00502600`)
write the costume to the person's sprite bank and make the sprite on it as it is, `FUN_004d4140( 3, kind, costume )`;
the guard draws its own (`FUN_004d5de0`, `FUN_00541f70`). Nothing bounds it: the sprite maker `FUN_00475a10` asserts
only that the kind has a bank at all (`0x00475a71`). A sprite is drawn from **one flat table of loaded banks**,
`[0x00876b48 + (base of its kind + its bank) * 4]` (`FUN_00542010`, `FUN_00541fa0`), and `Sprites_LoadBanks`
(`0x00541d90`) gives the fourteen kinds consecutive bases, each the running count `[0x0087626c]` as its folder is
loaded. So a bank past its kind's count is the next kind's. A candidate rolled in the session cannot hold one (the
roll is modulo the count, `FUN_00541f70`), but one read from a save can: Lost Kingdom's save holds three mechanics
with the second costume, and low detail loads one mechanic bank. **In the original, predicted first** (low detail
set in its options screen, the game restarted, 2026-10-06): the table read entertainers base 6 count 3, handymen 9
and 1, mechanics 10 and 1, guards 11 and 1, researchers 12 and 1; the park's own saved mechanic, saved on bank 1,
came in on bank 0 (the load's reduction, `0x004f93a6`); Alex Cullum, hired from the pool, was thing 43 with sprite
kind 6 **bank 1**, and was drawn as the guard, in the park and in the hire screen's preview. **OpenTPW** (the
review's fix 3) brings the costume within the banks loaded at the hire, as a save and load would: a deviation Alexah
chose (`docs/DECISIONS.md`, "A hired costume is brought within the banks loaded"), said at `ParkPeople.StaffPicture`.
In the game on low detail, Karl Fitzhugh hired as thing 43, sprite kind 6 bank 0, the mechanic's figure; the build
before refused the hire and counted it.

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

**The staff pool's refresh** (Q130, decoded first-hand and run in the original; the build is Q130b). The pool is
32 records of `0x14` bytes at the very start of the world object: `+0` the kind, `+4` the name's row, `+8` the grade,
`+9` the costume, `+0xa` occupied, `+0xb` taken into the hand, `+0xc` the `mGameTick` it was made on, `+0x10` its
lifetime; the pool's own mark is at world `+0x294`. `FUN_005084f0` runs on every thing sweep, from the sweep's tail
after the arrival manager (`0x004d7b30`):

1. **Each record that is occupied, not taken, and older than four times its lifetime in sweeps is dropped**
   (`mGameTick - mark > lifetime * 4`, unsigned, through `FUN_0041a970`), and its hire-list row with it
   (`FUN_00481550`).
2. **When the pool's mark is more than `TimeBetweenStaffUpdates * 4` sweeps old it is topped up.** The budget is
   `MaxNumberOfStaffPerUpdate`, or the free slots if fewer. `FUN_00508000` counts the staff in the park by kind and
   marks a kind full at `Max<Kind>InPark`. Each kind then wants `Max<Kind>` less its candidates in the pool, nought
   when it is full; while the budget and the total wanted last, one draw of the world's generator modulo the total
   picks a kind by those weights, the first free slot takes a new candidate of it (`FUN_00507600`), and that kind's
   want and the total go down one. Then `FUN_00508170` makes up the minimums: for the mechanics, the handymen, the
   entertainers, the guards and the researchers in that order, round and round until none is short, a kind whose
   staff in the park plus candidates in the pool is under `Min<Kind>InPool` gets one more. A short kind that is full,
   or short with no slot free, gets nobody and still calls for another round (`0x005081a3`, `0x005081c8`, both to
   `0x005081d8`), so that loop has no end for it. Last, the pool's mark is set to this sweep.

`FUN_00507600( kind, slot )` makes a candidate: the costume from `FUN_00541f70`, two draws of which the second, modulo
3, plus `AvgGradeOf<Kind>` less one is the grade, held to 4; a name (`FUN_00507580`, a draw modulo 35, fifteen draws at the
most while `FUN_005083f0` finds it in use); occupied; the hire list told; the mark; and the lifetime, a
draw modulo half `StaffTimeoutTime` plus `StaffTimeoutTime`. The keys are the `StaffPoolInfo` block, four bytes each
from `0x00785284`, in the order of the executable's own key table (handymen, mechanics, entertainers, guards,
researchers in every group, rows `0x00740990` on (each name four bytes into its row), where `ChanceToGetGreat<Kind>` heads the block and `BeginningNumberOf<Kind>` is its third group, rows `0x00740be8` and values `0x007852ac`; `Standard.sam` lists `BeginningNumberOf` mechanics first): `AvgGradeOf` at `0x00785298`, `Max<Kind>` at `0x007852c0`,
`Min<Kind>InPool` at `0x007852d4`, `Max<Kind>InPark` at `0x007852e8`, `TimeBetweenStaffUpdates` at `0x00785304`,
`MaxNumberOfStaffPerUpdate` at `0x00785308`, `StaffTimeoutTime` at `0x0078530c`.

The opening pool is `FUN_005087d0`, from the park's creation (`FUN_005156a0`, which has just zeroed `mGameTick`, to
`FUN_004d7af0` and `FUN_00507bb0`): `BeginningNumberOf<Kind>` candidates of each kind in turn through `FUN_00507600`,
so each marked nought with its own lifetime, then the pool's mark; it runs before any save is read.

**In the original**, predicted first, the pool read from memory every 100 ms for 240 s (`pool.py`). The top-up
comes on the first sweep more than 360 past the mark, 361. The keys as
Lost Kingdom's Instant Action loads them: 90, 10 and 120; `Max` 6, 5, 6, 4, 3; `Min` 1, 1, 1, 1, 0; in the park 30,
15, 30, 15, 6. The pool stood at fourteen when the read began on 868 (the save holds sixteen, below), made on `mGameTick` 361 and 722, its mark 722. **Twenty-four
of twenty-four candidates went on the first sweep past four times their lifetime** (one of lifetime 141 made on 361
went on 926, 565 sweeps old), their lifetimes 126 to 176. **The pool was topped up on `mGameTick` 1083 and 1444, 361
sweeps apart, by ten each time**, no kind past its `Max`. Not seen: a kind full in the park, the minimums' pass
adding one, a candidate in the hand outliving its time.

**OpenTPW** (Q130b): `ParkStaffPool.Sweep`, called from the sweep's tail on `ParkState.GameTick`, is the turn
above: `TimedOut`, `UpdateIsDue`, `TopUp` with `PickByWeight`, and `ShortOfTheirMinimum` for the minimums' round;
every candidate carries its mark and its lifetime (`LifetimeFrom`), and the hire screen's list follows the pool while
it is open. Measured in Lost Kingdom, the opening pool of twenty-two made on 755: all twenty-two, and one made
later, went on their mark plus four times their lifetime plus one; topped up on 1116 by two (only two were wanted,
the opening pool standing at or over most kinds' `Max`) and on 1477 by ten; before it, the same twenty-two stood 210 s
on. **Not the original's**: the draws are the pool's own generator, the world's there; a name is drawn again, fifteen
draws at the most, while a candidate in the pool has it or a member of staff in the park does (`FUN_005083f0` walks
the 32 records, then every staff thing, comparing `FUN_00506860`'s text; here `Staff.Name`, the save's `mName` or the
candidate's at a hire, Q238); every rolled costume is nought (Q214, counted as
`ROLLED_STAFF_COSTUME`); a newcomer joins the list after the last, though the slot it is given is the lowest free of the 32 as the original's is (`Candidate.Slot`); a minimums' round that can add nobody is the last, where
the original's would go round for ever.

**A loaded park's pool is the save's** (Q130c). `FUN_00507850` reads the 32 records through `FUN_005070a0`, then the
five `mPeopleInCat` and `mStopProducing` pairs, the pool's `mTimeSig` (`FUN_0041a860`) and
`mOpeningStaffPoolGenerated`, over the opening pool the park's creation has just made; the layout is FileFormats
`saves.md`, "The staff pool". `ParkWorld.StaffPool` and `StaffPoolTimeSig` are that, and `ParkStaffPool` built
with a save takes its occupied records as they stand: kind, the name by its row of the kind's table, grade,
costume, mark and lifetime, and the pool's mark. A park with no save still rolls. Lost Kingdom's file holds
sixteen (the original's log above began on 868 with fourteen: the two of lifetime 124 made on 361 are due on 858
by the rule, before that log began, so those two are not measured). Predicted and read in
OpenTPW: sixteen made on 361 and 722; gone on 858, 858, 926, 934, 946, 1066, 1239, 1259, 1263, 1279, 1327, 1355,
1383, 1387, 1387 and 1403; topped up on 1083 by ten to twenty and on 1444 by ten, from 926 on each the original's
own tick and count; the build before it rolled twenty-two on 755 and topped up on 1116 and 1477. **Beside the original's hire
screen** on `mGameTick` 980 to 1054: the same people at the same wages on four of its five tabs, the guards' not
photographed (Chris Battson 63 and Rajan Tande
45; mechanics at 92, 115, 115; entertainers at 60, 60; researchers at 100, 100). Two differences, neither the
pool's: the reference install reads the `american` name tables, where three of those rows hold other names than
`English`'s (mechanic row 12 is Alex Cullum there, Rob O'Farrell here), and its list stands in name order on all
four tabs where OpenTPW's is in the pool's order: the list sorts itself (`hud.md`, "A list's order"; the build is
Q130e).

**Every way out without a drop returns the candidate**: a quick right click with RMB cancel on, which is
the default (`0x0048842b` installs the idle mode whatever the current one is); Escape (`0x0040c180`),
unless the staff/visitor locator is open (`DAT_007cc2f0`), which it closes instead, and the menu does not open; the extended
Delete key, which installs Clear Land (`FUN_0040c5e0`); picking another candidate, if the hire screen can
be reopened while carrying (see Open), the old one returning first, after which every untaken candidate of a kind at its staff cap is purged (`0x00507f9b`..
`0x00507fcd`); and leaving the park (see "Leaving a park with something in the hand"). A
held or dragged right press, or any right press with the option off, leaves the candidate in the hand.

**The picker never hands the mode a cell off the map** (Q472; measured in the original, `q472/PREDICTION.txt`). Each
frame, unless `gui_CameraFlags` `[0x00790ab0] & 0x16` (a first-person view), `0x0054e2b0` calls `FUN_0045bf90( &P, 60.0,
0x007a1c90, 0 )` and then `FUN_0045d560( &P )`, P the point at `0x007a1c70`. `FUN_0045bf90` writes P only when its ray
meets the terrain or an object (`DAT_007a1cd4`); meeting neither, **P keeps the last hit and the same cell is written
again**. The terrain test walks only the heightfield `[[0x007a0854] + 0x6c]`: origin `+0x8`/`+0xc` (0, 0), spacing
`+0x10`/`+0x14` (10, 10), `+0x18` by `+0x1c` cells (**96 by 85**), so nothing past x 960 or z 850, and nothing at z
below 0, is ground to it. The object march (`FUN_0045cdc0`, steps of 10 along the ray while it is above the terrain's
hit, or above 0 with none) asks the things of the cell under each step, that cell held to 0 to `[0x007a0314] - 1` and 0
to `[0x007a0374] - 1`. `FUN_0045d560` then takes x = (int)((P.x - `+0x8`) / `+0x10`) and y = (int)((P.z - `+0xc`) /
`+0x14`), truncated, holds each to 0 and to the heightfield's cells less one, then to `[0x007a0314] - 1` and
`[0x007a0374] - 1`, and writes `y * 0x80 + 1 + x`; 0 only where `FUN_004d8300` refuses (outside 0 to 127), which these
holds never reach. **The two limits are `Standard.sam`'s `MapInfo.HeightfieldWidth` and `HeightfieldHeight`**, 95 and 84 in
every park: `FUN_00457c30` copies them from `FUN_00519430` and `FUN_00519440` (`[0x0078558c]`, `[0x00785590]`, the
balance block's fields in the order of the names at `0x0074253c`: XStart, YStart, Width, Height; the starts, 0 and 0,
go to `[0x007a01bc]` and `[0x007a029c]`). **So the picker answers columns 0 to 94 and rows 0 to 83 only**: the
terrain met in column 95 or row 84 is held to 94 or 83. Read in the original at the stock park
(`q472/orig/a-run.out`): the heightfield 96 x 85 at (0, 0) by 10, the limits 95 and 84; the pointer on (95,40) read
(94,40) with P.x 958.2, on (40,84) read (40,83) with P.z 847.4; past column 95 (the boundary fence and the field
beyond), past row 84, and over the sea past row 0, P and the cell stood unchanged, the object word 0, and the hover's
row stood with them (`a-A4.png`). The click does not test for 0.

**OpenTPW** (Q472b, `ParkPicking.MeetGround`, `CellOf`, `Meet`): the ray meets the ground only over the heightfield's
cells, the cell is truncated and held to the heightfield's last and then below `MapInfo`'s two limits, and where the ray
meets nothing the last cell and point stand. **Deviation:** the original's word is never nought. P (`0x007a1c70`) lies in
`.data`'s zero tail (raw data ends at `0x00784e00`), so it starts at (0,0,0); `[0x007b05cc]` is written only by `FUN_0045d560`
(`0x0045d5f7`, and `0x0045d601` on `FUN_004d8300`'s refusal, never reached) and by the start-up initializer `FUN_0046c0b0`
(`0x0046c0b0`, with 1); nothing resets either at a park's end. So it reads cell (0,0) before the first hit of a session and
a later park opens on the last park's P. Ours is nought before the first meeting and again after `ParkPicking.Forget` (a park's
end), which shows for a frame or two over (0,0), the sea, with no peeps and no row. (A write through a computed pointer is not
ruled out by the search.) The ground met is the original's two planes a cell
(`ParkPickGround.Meet`, Q824; "The pick's terrain", above). The object march is built (Q494, below). Measured at the three views above (`q472b/`, `PREDICTION.txt`, `side-by-side.png`): (95,40) answers
(94,40) with x 956.1; past the last column (94,40) and the point stand and "Left-click to build path" stands over the
fence; (40,84)'s pixel answers (40,83) and no pixel of its column a row above 83; past the last row (40,83) stands; over
the sea (45,2) stands; the original read the same cells at the same views (round b).

**The picker's object march** (Q494; read in Ghidra, measured in the original, `q494/PREDICTION.txt`). After its
terrain test `FUN_0045bf90` marches the same ray against the models of the cells under it, from the eye (`0x00790aa0`)
in steps of 10 along whichever of the ray's ground axes is longer (x, or z where `|x| <= |z|`), the other axis and the
height following in proportion (`0x0045c99a`..`0x0045c9e0`). It goes on while the ray stands at or above the terrain's
point plus one step's fall, or at or above nought where the terrain met nothing (`0x0045c9f0`..`0x0045ca17`); nothing
else ends it. Each step asks `PickMarch_AskCell` (`FUN_0045cdc0`) about the cell `(int)(x * 0.1)`, `(int)(z * 0.1)`; then, where the other
axis crossed a cell edge during the step and the new x and z are not below nought, it asks the cell at the crossing,
taken 1.01 of the way (`0x006fe668`). The first thing met ends the march.

`FUN_0045cdc0` holds the cell to 0 and to `MapInfo`'s width and height less one and asks `FUN_00452ae0( x, z, &thing )`
("Placement feedback"; `park.md`, "The scene's height under a point"): a track piece's model (`FUN_0053bf30`), else
`FUN_00527e80`, where every cell of the entrance's row from one before `EntranceA` to one past `EntranceB` answers the
gate whatever it holds, and a cell of type 4, 9 or 10 its parent's object. A thing whose model record has no `.hmp`
(`+0xcc`) is not tried; one that has answers its `.hmp` height over the cell plus its root's height. A record flagged
`0x80000` (set by the coaster builder `FUN_00434060` on a track piece's records; none of the stock park's 163 records
carries it) is tried as its piece's list (`[[record + 0xd4] + 0x100]`, `FUN_00447790`, `FUN_004477d0`), last first, each
with its own height. The thing is tried when the ray's height across the cell, from where it enters to where it leaves
along the stepped axis (`modf` of a tenth, or `1 - fmod( v, 10 ) / 10` for a falling coordinate), reaches down to that
height and up to the model's box minimum height (header `+0x84`, which `FUN_00451640` copies from the `.hmp`'s box:
gates.hmp's -0.004). A model flagged `0x40000` and `0x20000` (`+0x30`) first has the face normals of its nodes flagged `0x10000` recomputed (`FUN_00473c30` -> `FUN_00473a40`, which clears header flag `0x40000`); it poses nothing (fork watch 2026-10-27, verify-world M2).

The model test, `PickMarch_MeetModel` (`FUN_0045d090`) `( root node, identity )`, walks the node tree from the header's root (`+0x78`): each node's
matrix composed onto its parent's (`FUN_004702d0`); a node flagged `0x10` or `0x8210` offers no faces, otherwise its
vertices are carried into the world (`FUN_00466c80`) and its faces tried by material in the material table's order
(each material's `+4` its first face, `+6` its count); then its children (`+0xc`, then each `+8`) unless it is flagged
`0x20`. A face is tried by the LINE through the eye, not the ray: the plane through its first corner with its normal
from the table at mesh `+0x64` (turned, normalised), the point inside where the three angles it makes with the corners
sum to 6.2821855 (`0x006fe678`) or more. The first face inside gives the point (`0x007a1cc8`, kept at `0x007a1cb8`), so
the answer is the first face in that order, not the nearest. **The faces' normals in the running game are the
triangles' own**: in gates.MD2's gateway the 196 entries the faces name all lie parallel to their triangles in the
running game, 37 of them already so in the file (`q494/orig/a-gatenormals.txt`); the table's other entries were not
compared; what rewrites the 159 is the instancer's `0x10000` on every node and `FUN_00473c30` (`0x00463937`, Q819: "The recomputed face normals never reach the lighting"). File side: 26 of the 196 lie more than 8 degrees off in the file
(FileFormats `models.md`, "Normals"; `q494/fixB-normals-sweep.py`).

The record met goes to `[0x007a1cd4]` and to `[0x007afda0]` (`FUN_00469340`, the highlight's model, Q24), and the
coaster builder reads it (`FUN_00443340`). The model's point is P where it is strictly nearer the eye than the
terrain's, or the terrain met nothing (`0x0045ccf7`..`0x0045cd35`); else the terrain's point is.

Measured in the original at the stock park, camera (475,155) 110 180 (`q494/orig/a-scan.log`, `hoverscan.py` `pick`):
of 210 pixels over and around the gate, 87 met the gate's record (gates.MD2) and answered cells (46..50,17), (49,18)
and (46,18), P on the arch's faces at z 170.2 to 183.1 and heights 1.9 to 44.0, not always the face nearest the eye; 7
met the boundary's hoarding (ho_fos1.MD2, its track kind 25 anchors at (43,17) and (51,17), `a-tracklayer.txt`); the
gate's live vertices and gateway matrix are the file's, its doors stood turned to identity. The walk replayed on
gates.MD2 with the original's own rays (`walk.py`) gives 87 of its 87 points with each triangle's own plane and 45 with
the file's normals. At (480,265) 130 180 (`c-scan.log`) the march met the Belly Bounce (bouncy.MD2) at 16 points, most
on its base at height 0, and the Jungle Spray at 2.

**OpenTPW** (Q494, `ParkPickMarch`, `ParkPicking.March` and `CandidateOn`, `ParkItemHeights.ThingAnswering`): the march,
the cell's thing, the `.hmp` height about the model's root, the box floor, the walk and the thing met kept as
`ParkPicking.Marched` whether or not its point wins, as `[0x007a1cd4]` keeps it, and its point taken only where strictly
nearer than the ground's, the plane each triangle's own. Deviations: the `.hmp`'s cell is found from the cell's middle (the original
carries the corner by quarter turns: the same cell); the march stops after 400 steps; a cell of the second layer's things
(track kinds 11, 12, 16, 17, 25: the boundary's hoarding) is counted `PICK_OBJECT_MARCH_TRACK_THING` and answers
nothing, as none is built (Q214); `FUN_0053bf30` answers a track-layer object of any kind (`+4`, or the `+0x10` parent's
for kinds 12 and 17; `0x00452b08` uses any non-zero answer) and ours gates on those five kinds alone, so another kind
holding an object is not tested, and so the flagged records of a coaster's pieces are not reached. Handed the
original's rays by the console's `pickray` (`q494/ray2.out`), ours meets 87 of its 87 gate points within 0.2 (on the item's build and again on the
run 4 review's fix build, `fix-run4/r2.out`, where the march keeps the thing met).

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

**Where each test sits** (Q533). The clock is read only by the tick, message `0x1e` (`0x0048821f`..`0x00488244`, then
`0x00488282`); the move test only by a move, `0x10003` (`0x00488246`..`0x0048827f`); the release, `0x10004`
(`0x0048836f`), reads the flag `DAT_007c2500` alone, no clock and no point. A press and a release are not handed to the
layer when the window procedure sees them: `UI_PostButtonPress` and `UI_PostButtonRelease` put them in a queue
(`UI_QueueMessage`, `0x00658ccc` → `FUN_006697e3`, a ring of 16-byte messages). The UI pass `UI_RunPass` (`0x0048a580`) hands
that queue out first (`UI_DispatchQueue( 0x400 )`, `0x00658e42`) and only then sends every control the tick (`UI_TickRoot`,
`0x00658f6c` → `UI_TickTree` →
`FUN_00658d9f( ..., 0x1e )`, called, not queued). So a release that arrived while the game stood still is handed out
before the tick that would find the 200 ms passed, and is still a click; a stop that ends before the pass's tick, with
the button still down, is not. Measured (`q533/orig/run.sh`; the patrol tool armed, a right press, the whole process
stopped 0.605 s with the release sent meanwhile): 3 of 5 put the tool away (mode `0x36` to 0), 2 kept it (by the
decode, a stop that fell between the frame's input pump and its UI pass's tick; not measured); a press held 0.4 s with the game running kept it 2 of 2.

**Whose a right press is** (decoded for `docs/QUEUE.md` Q56: three decoders, each put to a refuter; static only).
The window procedure posts a right press as it does a left one (`0x0046b6a5`, button 1) to the control under the
pointer (`UI_PostButtonPress`, `0x00658af1`), with no hit test at the press, and the release to that same control wherever the pointer
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

The one test of an open screen on a press's path, `0x00488741`, is in the press case all three buttons share (`0x0048872c`), after the arm (a middle press does nothing more in the case with or without a screen: the next compare, `0x0048874d`, sends it out):
it keeps the press from the idle click and the mode's button-down slot ("A park screen is open"). A right
click on a row of the all-staff, visitors or all-items list, or on an object window's preview, moves the camera and
closes the window (the 500 ms click limit `[0x0077c480]`), never touching the hand. The full-screen view, F3
(`FUN_004a29d0`), hides the layer under a full-screen control whose handler `0x004a2840` gives a right press to the
camera and arms nothing ("The full-screen view: F3"). A press made before a window
opens keeps the capture on the layer (`0x004882ba`), so its release can still let go.

**OpenTPW.** `Level.RightButton` arms on the press and tests the move at each of the frame's steps (the frame's pointer
stands for the moves before it); the release reads the flag alone. `Level.RightTick`, run once a frame after the frame's
steps (`Level.WorldClick`), is the tick's clock test. The console's `stall <ms>` stops a frame after its tick: a release
sent meanwhile let go 5 of 5 (`q533/run3`).
`WindowStack.TakesRightPress`: a control under the pointer, a modal window, or a park screen's root
(`UiWindow.ParkScreen`, `WindowStack.OnParkScreen`) takes it, and `Level.RightPressTaken` adds first person (and gives
the park every press while F2 hides the HUD). A left press reads the same three (`WindowStack.PointerTaken`). No park
screen is modal, so the gadget answers beside one. The gadget's body takes a press inside its outline,
and its arm, handle and aerial take theirs (`hud.md`, "The arm and the aerial as built"). A right click on a list row and a click on an object window's
preview move the camera and close the window ("The camera goes to a thing"). `Level.LeftPressTaken` keeps a left press off the park in first person too (Q116): the
viewfinder layer's `FUN_00488a00` gives a press (`0x10005`) to the key table (`FUN_0040c900`) and every message to
`FUN_0042a760`, where a left press only sets bit 0 of the camera's button state `DAT_00790aac` (what reads that bit is
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
A put-down that reaches `FUN_005253b8` (by the `je` at `0x00524db1`) resets the rotation **unless exactly Ctrl is held**
(fork watch 2026-10-18, `verify-ui-hud.md` U6). `FUN_0046b420` reads the modifiers as Shift 3, Ctrl `0xC`, Alt `0x30`. With
the word `0xC` the function jumps to `0x00525a06` and changes nothing. Otherwise it calls `FUN_0052f580( 0, 0 )` (idle) and
`FUN_0052f1b0( 0, 0 )`, which saves the old rotation in `0x0081b134` and writes **0 into `0x0081d7a4`** (`0x0052f1d3`-
`0x0052f1de`); the tail at `0x005259ae` ends with `FUN_0052f1b0( 0, 0 )` again unless Ctrl is held. Not read: which
placements take the `0x00524db1` branch, and whether the last click of a queue re-arms the tool (the queue tool's commit).

**The add-on's snap, its put-down, its open ends and the window a click opens** (fork watch 2026-10-18, `verify-world-b.md` L2
and L3, `verify-world-a.md` M5; the snap, the verdict and the put-down read again for Q780 from `q780/dump` and measured in the
original under Proton, `q780/orig/O1`). Lost Kingdom's add-ons (1500 to 1502) are the items that reach all of it.

- **The hand.** The ride window's Buy Upgrades choice (`0x004af480`) builds the carry shell and calls `FUN_005276d0( 1 )`,
  whose builder's object `[0x008186ac]` is the thing whose window is open (`[0x007c2654]`), or, for an add-on of `+0x288` 1
  with a word `+0x30`, that word's ride (`0x005277b3`..`0x00527831`); its cell (bytes `+5`, `+7`) goes to `[0x007639a4]`,
  `[0x007639a8]`, its angle `+0x10` to `[0x007639ac]`. Measured: (25,21) for Alexah's Dino Karts, the ride's own cell. The
  shell's install (`0x0046d61d`) sets the add-on flag `[0x008187f8]` to the item's `+0x9c` (`Bumper.WhichTrackType`, 1 from
  `Upgrades.sam`) where `+0x288` is 1, else 0, through `FUN_0052f880`.
- **The snap**, the tool-4 preview `0x005236b0`, runs only when the add-on flag is non-zero (`0x0052369d`) and the pointer's
  cell is on the map. For each axis, d = the cell less the anchor; a negative odd d is lowered by one, then halved (`sar 1`):
  the anchor + 2 × floor( d / 2 ), written to `0x008186a4`/`0x008186a8` (`0x005236e3`..`0x0052374d`); off the map, nothing more.
  The cell's track record (`[0x007cf83c] + 40 × (y × 128 + x + 1 + 0x6cde)`) is redirected by `TrackCell_IsPieceCell` (types
  `0xc`, `0x11`) to the anchor `[ + 0x10 ]` names, and where `TrackCell_IsTrack` accepts it (`0xb`, `0xd`, `0x10`, `0x12`, `0x19`)
  the angle is its way on (`+0xd`): through the byte table `0x00524750` and the jump table `0x0052473c`, 1 gives 0, 4 `0x10e`,
  `0x10` `0xb4`, `0x40` `0x5a`, any other keeps the angle (`FUN_0052f1b0`; the rotation `[0x0081d7a4]` persists). Then **the
  adjust** of the angle now held (`0x00523804`..`0x005238a5`): 0 `+0xa4`/`+0xa8`, `0x5a` `+0xbc`/`+0xc0`, `0xb4` `+0xb4`/`+0xb8`,
  `0x10e` `+0xac`/`+0xb0`, which are `Bumper.NorthXAdjust` and Y, `West`, `South` and `East` (the bumper placer `FUN_00529e10`
  pairs `0x5a` with East instead). Then the ride's angle `[[0x008186ac] + 0x10]`: `0x5a` x - 1, `0xb4` x - 1 and y + 1,
  `0x10e` y + 1 (`0x005238ab`..`0x005238d7`). The cell is stored as the preview's `[0x008186b8]`/`[0x008186b4]`, which the
  carry shell's click reads (its fourth argument 1, `0x0046c627`; `0x005249cd`), and the placer's test pass is
  `BuildTool_Placer( x, y, 4, angle, 0, 1 )`, a failure setting `g_PlacerFailed` (`[0x0081b604]`).
  Measured (pointer, preview cell, rotation): (13,17), (12,17), 0 for the Tunnel; for the Jump (13,13), (12,14), 270, (15,13),
  (14,14), 270 and (17,14), (16,14), 270; off the track (3,12), (3,12), 0 and (12,12), (10,12) at a kept 270 (`jump-hovers.txt`).
  Alexah's played Jump (19,9) at 270 and Tunnel (17,21) at 90 are where pointers on (19..20, 9..10) and (17..18, 19..20) put them.
- **The test pass** over an add-on's picture: a track cell (kind `0xb` or `0x10`) asks `FUN_00532fc0( 0x20b or 0x210 )`, which
  is `BuildTool_CellVerdict( 0xb or 0x10 ) != 1`, and is then refused where its map type is 4 (`FUN_00536380`, `0x005294e2`);
  a body cell asks op `0x204`. **The verdict's add-on arm** (`0x0053594e`..`0x005359b3`), past its first answers (land outside the
  park red; a map type 4, 9 or 10 cell blue where its thing's `+0x54` is below the add-on's `[0x00818c10]`, else red; a `0x19`
  track cell red): a track cell is blue only where its map flags lack `0x10`, the track record it names is track of no bend
  (`TrackCell_BendLinks`) and no crossing (`TrackCell_CrossingLinks`), owned by the builder's object (`+0x10` against
  `[0x00818698]`) and its map type is not 3. A body cell over track answers blue for an add-on (`0x00535bc3`). Measured: red
  over a bend and over the crossing, blue over a straight; any red reds every square (`squares.py`).
- **The put-down's arm** (`0x00524b45`..`0x00524d87`, the add-on flag set; the run 13 review of Q778, `review13/VERIFY-3.md`): `FUN_005276d0(
  0 )`; the preview cell's track record is redirected as above, and where it is no track the arm builds nothing and goes idle
  (`0x00524bbe`); otherwise the builder's object becomes the thing on the cell its owner names (`0x00524c3d`), its sections are
  cleared (`TrackRide_ClearSections`, `0x00524c46`), the placer commits (`0x00524c63`), `FUN_00523230` runs ops 0x85, 0x86 and
  0x81 over the track about it, the circuit is walked (`TrackRide_WalkCircuit( 0 )`, `0x00524d0a`) into the ride's `+0x2c`, the
  pieces play (`0x00524d29`, `0x00524d37`) and a closed ride opens under the open guard; then the tool goes idle and the flag is
  cleared unless Ctrl alone is held, and the rotation goes to 0 either way (`0x005259fc`). Measured: the bought Tunnel's caps
  hidden at the click (`gk_end09`, `gk_end08` `0xc1000111`), the bought Jump's shown (`0x41000101`); the `0x01000000` bit is on
  both models made in the session and not read.
- **The commit** (`BuildTool_Placer`, its fifth argument set): every cell of the picture but `.` is stamped, its map record
  (`[0x007cf83c] + 0x294 + 0x44 × id`) by ops `0x87`, the clear `FUN_005367a0`, 4 and `0x81` and owned by the thing's cell
  (`FUN_00539210`), its track record's model let go (`FUN_00536780`) and its flags' low four bits cleared (`FUN_0053a530( 0xf )`);
  a track cell's map and track flags are then both **written** `0x10` (`FUN_0053a510`, `0x0052950d`). The track's type, links,
  way on and owner stay. **The placement** (`0x00529ff5`..`0x0052a023`) with the add-on flag set and the type's `+0x288` not 2
  stores the builder's object's id in the thing's `+0x30` (`FUN_004e0de0`), else 0: the file's `mUpgradeParent`. Measured on the
  bought Tunnel, thing 209 at (11,16) turned 0: its cells (12..13, 15..18) map type 4, flags `0x10` both, owner 2060; `+0x30`
  289, the file's `mTopLeft` 1932 and `mUpgradeParent` 289 (`savepark.sh`); the Jump, thing 598 at (17,13) turned 270, the same.
- **The click** (`0x00486920`): a thing whose byte `+2` is 3 and whose type `+0x4ac` is class 0 (table `0x00486a90`). A type with
  `+0x288` 2 opens the window of `[0x007cc3e0]` (`0x004b6ae0`). Otherwise a non-zero word `+0x30` (`FUN_004e0df0`) opens that ride's
  window (`0x004af980` on `0x007cfb90[id]`); a zero word looks at the type's own `+0x284` (`FUN_004e2710`): zero opens the thing's own
  window, non-zero opens nothing.
- **The open ends**, `0x0052a970`, per tile at `0x0052ade3`: `FUN_00535db0( 0x10 )` raises the counter `[esp + 0x20]`, and the bit
  reset at `0x0052ae1e` runs only when the raised counter is 0. Every other write of the counter in the routine is 0
  (`0x0052a996`, `0x0052abe4`, `0x0052ac3c`, `0x0052ad1e`) and `[esp + 0x2c]` is written only 0 (`0x0052a9ae`) and by the two ORs, so the
  bits accumulate across runs and are never cleared after the start. Ahead `FUN_0050c3e0( flow, 2 )`, behind
  `FUN_0050c3e0( FUN_004d8c00( flow ), 2 )`: type `0xb`/`0x10` (`FUN_0053ad70`) with `+4`, or `0xd`/`0x12`, ORs `0x10`
  (`0x0052af07`) or 1 (`0x0052af4d`). At counter 2 (`0x0052af54`) with the bits at most `0x11`, the byte table `0x0052b38c`
  (`00 01 04 x 14 02 03`) and the jump table (`0x0052af77`, `0x0052afe8`, `0x0052b002`, `0x0052b0cb`, `0x0052b1a7`) choose the
  dummy nodes: bits 0 shows both, 1 hides dummy 1 and shows `0x10`, `0x10` shows 1 and hides `0x10`, `0x11` hides both
  (`FUN_0044b3c0( instance, 1, n )`, the lookup record whose hide word is n; show is `and ~0x10`, hide is `or 0x80000010`;
  the whole rule, where the count goes back to nought and the caps measured: `park.md`, "The walk", Q778). Not read: what
  `[esp + 0x50]` holds in the demolisher's add-on arm (`0x00528512`..`0x0052852d`), `FUN_00523230`'s cells.
**OpenTPW** (Q780, `ParkTrackAddOn`, `ParkBuilding`): the console's `carry <item> <thing>` stands in for the Buy Upgrades row
(Q512); the preview snaps and turns the hand, the squares and the test pass judge by the verdict's add-on arm, the commit stamps
the picture by kind at the model's cell (`mTopLeft`, the original's rule, where other items still mark the anchor's, Q744), the
thing keeps `mUpgradeParent`, and the put-down walks the circuit. Counted: `FUN_00523230`'s passes (`TRACK_ADD_ON_PASSES`) and the
sale (`SALE_TRACK_ADD_ON`); the pieces' clips that follow the walk are Q781's (`park.md`, "The pieces' clips"). The arm's rotation to 0 (`0x005259fc`) is built: the hand's
angle is 0 after the put-down, Ctrl held too. Not built: the window a click on an add-on opens (Q513).

**The worker (type 6)**, vtable `0x006fea70`. The mode stores only the worker's sprite bank and costume; the
worker's id is kept in the world's hand thing (`+0x60`, through `FUN_00509ae0` and `FUN_00509af0`). The pickup
frees the worker's sprite and sets state 7, which every staff machine answers with a bare return, and saves no
position. The drop (`0x0046cc40`) reads the hovered cell and refuses with the type-5 rules ("Can't put staff
here", nothing else). The uninstall (`0x0046cdc0`), when the hand still names a worker, logs "Dropping staff where
he was before he was picked up" and puts them down on **their own current cell** with the drop's body
`FUN_00505ea0`: the centre of the cell, the walk reset, the cell lists relinked, the sprite rebuilt. Every kind
then goes to state 0 **except the mechanic, whose job search runs at once** (`FUN_004da5b0`). The right-button
slots are `RET 8`.

The thing at world `+0x1da718` is `mStaffHQ`, thing 1 (`FUN_00519450`; `ride-operation.md`, "The strike"), whose
`+0x60` the save names `mStaffMemberPickedUp`.
**Measured** (Q432, `q432/orig/run.sh`, the staff window's PICK UP on the stock park's mechanic): `+0x60` read
`0x0073001a` while he was carried and `0x00730000` once he was put down, so the worker is its low word; his `+0x19c` 7,
then 1 after the drop. The dismissal (`FUN_00505790`) installs the idle mode first where the current mode is type 6
(`0x00505830`), so a carried worker is put back before anyone is fired.

**Open.** Whether anything moves a carried worker (a carried
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
map (`FUN_005f0b40`, `0x005f0bd1`) and first person (`FUN_00481a10`, `0x00481a2b`). Most look before that call and return at once when their own screen is up: entry price (`DAT_007ca210`, `0x00498d9f`), finances (`DAT_007ca2e8`, `0x0049ac7d`), park status (`DAT_007cb398`, `0x004a52bd`), staff costs (`DAT_007cc2ec`, `0x004b2775`), loans (`DAT_007cb25c`, `0x0049fb55`), research (`DAT_007cc1d8`, `0x004aa4b2`), `FUN_004b7b00` (`DAT_007cc444`, `0x004b7b20`) and the object windows' shared opener (its own `+0x10`, `0x0048cea9`); hire, all staff, all items and visitors have no such test. The buy opener
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
`FUN_004862a0` switches the three tables back on (`0x004863af`..`0x004863c5`) and gives the layer the focus. With the coaster bar up (`DAT_007ca1cc`, `0x00486396`) it switches on the `camera` table alone and gives the bar the focus (`0x004863a5`, `FUN_00498a80`).

**The keys go to the screen**, whose handler is `FUN_00488ba0` for every one of them. A key down latches the
`shortcuts` table (`0x00488c2e`). A key up: a plain Escape closes the screen (message 4 to itself, `0x00488bdd`); a
plain F1 calls `FUN_005194d0` and `FUN_0059ab50` (`0x00488bfb`, not traced); anything else runs the `shortcuts` table
and no other (`0x00488c13`). So with a screen open no camera key turns or scrolls the view, Backspace, Delete and F3
(the `game` table's) do nothing, and a shortcut still works, B opening the buy screen over whatever was open.
`FUN_00486b70`, which switches five tables off, is not part of this: its eleven callers are the message box and the
name boxes (the object window's rename `FUN_0048d370`, the park's `FUN_004990f0`, and the like), none of them a
screen's opener.

**The pointer.** A press on the screen goes to the screen (its root takes one anywhere on its rectangle; "Whose a right
press is"). A press on the layer beside it reaches `Park_MouseMessageProc`, whose press case for every button
(`0x0048872c`) latches the camera table and then tests the screen: `CMP [0x007c24c8]` at `0x00488741` leaves the case,
so the hover is not taken (`FUN_00486d90`), the idle click `FUN_004879d0` is not run (no window opens, no path tool is
picked up) and the mode's button-down slot is not called. The hover is skipped the same way on the layer's timer and on
the pointer's entry (`0x00488569`, `0x0048884e`), so the cursor stays the arrow. **The release has no such test**
(its case, `0x004885a5`): it runs the camera table, moves the mode's preview (`FUN_0046c2a0`) and calls the mode's
button-up slot (`FUN_0046c210`, `0x00488841`). For the idle mode that is a `RET`. For a carry shell it is `FUN_00524960`, the commit, whose one read
of the flag the down slot sets (`DAT_008186d8`, `0x00524a77`) gates a sound and nothing else. So with a build tool
armed or something in the hand, a click beside an open screen still lays the run or puts the thing down. The right
press goes through the same case and the same test: beside an open screen the mode's right-down slot (`+0xc`) is not called, a `RET 8` in the idle mode and the carry shell (`0x0046c650`), and its arm (`0x0048833a`) is before the case and tests no screen.

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
the hand holds something; `Level.GameTableKeys`, the orbit camera's keys and F3 are not heard while one is open
(`WindowStack.ParkScreenOpen`); the idle pointer and its help row are the plain ones; `ParkGadget.Update` folds the arm
when a screen opens. A plain Escape let go closes the screen in front, or the map, and is spent doing it; with a
modifier held it does nothing (`ParkFrontEnd.MenuKey`). Where it parts from the original:

- **A click acts on its press here**, so the armed tool's click beside a screen is taken on the press, where the
  original skips the press and commits on the release.
- **First person** leaves the screen open (`docs/QUEUE.md` Q122).
- **F1 over a screen** and **the wheel over a screen's body** are not decoded, and neither is built or counted
  (`docs/QUEUE.md` Q231). The wheel zooms the camera wherever the pointer is.

### The game menu and the options screen hold the keys

Q692. **Neither opener switches a key table off.** `GameMenu_Open` (`0x0048c830`; the park's build, `GameMenu_BuildPark`
`0x0048c150`, then `FUN_004a25f0( 1 )` and the pause) and `OptionsScreen_Open` (`0x004a3a30`, which hides the park's
layer with message 6) call neither `FUN_0040cfa0` nor `FUN_00486b70`; the switch's four callers are `FUN_00486b70`
itself, the two inlined copies in `FUN_0048ac40` and `FUN_0048ae70` (`0x0048ac8f`, `0x0048aebf`) and
`FrontEnd_ShowPlayerSlots` (`0x004a6599`). **The keys go to them instead.** `MenuList_Show` (`0x00493130`) closes the
open park screen and shows the menu over the layer, and the park's menu handler `GameMenu_ParkHandler` (`0x0048b6a0`)
takes every key-up (`0x1000b`, `0x0048b6ad`): Escape, or a key the `shortcuts` table's lookup (`FUN_0040c870` on
`[0x0078721c]`, `0x0048bb49`) finds as row 0 ("menu"), resumes the park and closes it; any other goes to
`MenuList_Callback` (`0x00492e00`). The park's layer, whose handler runs the `camera` table, hears none of them.

**What the camera reads is the row's latch.** `Bindings_RowHeld` (`0x0040ca20`, this = the table, the action) returns the
row's `+0x06`, set by a key's down and cleared by its up, and tests no table's enable; `ParkCamera_Update`
(`0x0042b1c0`) reads it for every held action (the pan's `0xd` to `0x10` at `0x0042b99b`..`0x0042b9e9`) and runs
every frame from `Game_StateMachine` (`0x0054fa9e`), the menu up or not, its time from the game clock's second stopwatch
(`FUN_00402d80`, `0x0042b1ff`). So a key pressed behind the menu moves nothing, and a key held as the menu opens keeps
its row latched: its up goes to the menu.

**Measured** (`q692/orig`, `tools/harness/original/keylatch.py` beside each round; the stock park, `setcam.py` (480,
400)): behind the menu (`[0x007c2534]` set, `g_ParkRunning` 0) keypad 8 or Up held a second moved nothing, the camera
table's enable 1 throughout and no row held; behind the options screen (`[0x007cb2fc]` set) keypad 8 held a second the
same (Up not pressed there), and after OK.
Up held from zoom 130 as Escape opened the menu, let go behind it, kept its row (`0x0:0x2600`) held three times of
three: the zoom crept about 1.5 a second behind the menu (112.70 to 109.05 in 2.4 s; 50 a second running; `ParkCamera_Update`
steps the stopwatch's move plus 1 ms a frame, `inc eax` at `0x0042b2a6`, the last reading stored at `0x0042cce7`, so
under the pause the step is 1 ms a frame and the zoom's 0.05 a ms is 0.05 a frame, about 1.5 a second at 30 frames), ran to 70 within a second of the close, and the row
cleared at the next press and release of Up in the park. Up held and let go with no menu cleared its row both times.
(Keypad 8 showed the same, but its control lost its release with no menu at all: the Wine trap, `TOOLING.md`.)

**OpenTPW** (`WindowStack.CameraKeysAway`, read by `ParkOrbitCameraMode`): no camera key pans, zooms, turns or resets
under the game menu or the options screen, as under a message box and the Save and Load screens. The keys are read
live, so a key held as the menu opens is let go there and nothing moves when it closes: **the original's stuck row,
its creep behind the menu and its run after the close are not kept** (said at the site). A related older difference
stays: the original's camera, a pan's coast and the turn's ease, steps by the same stopwatch and so runs at about 3%
speed behind every pausing window, where ours eases on `Time.Delta` and finishes behind the menu (the Q692 run `ours-b`
U3: a coast 341.6 to 351.3 finished behind it); not measured in the original.

### The camera goes to a thing

Decoded for `docs/QUEUE.md` Q117, first-hand, and run in the original.

**`FUN_004867b0( thing id )`** does nothing for nought or an id with no thing (`[0x007cfb90 + id * 20]`). Otherwise it
packs the thing's cell from bytes `+5` and `+7` (`0x004867f2`, the whole part of its position), takes the column and
the row back out, multiplies each by ten and converts it to a float (`FILD`, `0x00486831`, `0x00486843`), and hands
the pair to `FUN_0042aab0` with three null pointers. So the camera is put on the **corner of the thing's cell**, in
whole tens, not on the thing's own position and not on the cell's middle.

**`FUN_0042aab0( x, z, a, b, c )`** zeroes the three terms at `0x00790a88`, `0x00790a8c` and `0x00790a90` (the camera
update `FUN_0042b1c0` reads and writes them: its scroll), writes the look-at point `0x007908f0` and `0x007908f8` from the
first two (pointers as well: a null first skips both words, `0x0042aad4`, and the second is not tested), and writes `0x007909ec`, `0x00790a38` and `0x0074c9bc` only from a pointer that is not null. From here none
is, so the spin and the zoom stay.

**Six call sites.** Two are not followed here: `FUN_0048ce80` (`0x0048ce95`) goes to the thing shown without closing anything, and `FUN_004b5c80` calls it on `0x10006` for the preview of the window `FUN_004b6080` builds (`hud.md`); case 3 of `FUN_0048e0f0` (`0x0048e22c`) passes its second argument. Each of the three list screens' handlers answers the list's `0x402` (`hud.md`, "The messages"): the
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
`ParkPeople.PutBack` - logged as `Leaving the park:`. Exit To Lobby has already written the park as `autosave.TPWS` by then (`Level.WriteParkOnLeaving`,
which lets go of the hand itself; `saves.md`, "Leaving a park: the autosave"), and Restart Park reloads through the
same `Unload` with nothing written, so the hand leaves empty either way. It is `ParkHand.LetGo`, so a picked-up worker is put back down in their cell as well. The rest keeps the
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

Built in OpenTPW: the ride's (`ParkObjectWindow`), the shop's (`ParkShopWindow`, `hud.md`, "The shop's window"), the staff
member's and the visitor's; the other five are counted where they are reached (Q31b).

Five verbs live in the shared base: cycle forward, cycle back, move, delete, close. **The ride's three
sliders are buffered and committed only when the window closes OR either cycle button is pressed**, and
capacity and duration commit **byte-wide** where speed is a dword.

**On the staff window**: FIRE is control **0x50c**, PICK UP is **0x50e**, and the patrol-area toggle is
**0x510** (map tool 0x36), whose "no area" sentinel is the pair (1, 0x4000) - the whole map.

**Ctrl+click on a placed object does not open its window** - it buys another copy of the same item and
puts it in the cursor (when the bank covers the price, else the refusal sound; "Ctrl+click buys another of a placed thing",
above; built, Q672).

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
- With a `P` model, `FUN_00468950` looks in each of the two models for the materials flagged `0x2` with `0x8` and
  `0x2` with `0x10` (`0x00468975`, `0x004689a7`), or failing the flags named `sign1.tga` and
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
- The first angle is the angle the last panel left in the record, wrapped to two pi: `fmod( [0x007afd50], 6.2831855 )`
  (`FUN_0067b24a` is the CRT's `_CIfmod`, `0x00468e2f`; the double is at `0x006fe7d8`). The record is static and its
  angle is nought in the image, so only the first panel of a run starts from nought.

**`FUN_00468e50` turns and places it each frame.** It copies the kept root matrix back, turns it about y by the angle
(`FUN_0046f420`: `x' = x cos a + z sin a`, `z' = z cos a - x sin a`), tips it about x by 45 degrees
(`FUN_0046f240` with 0.7071, -0.7071: `y' = 0.7071 ( y + z' )`), and adds to the turned matrix's translation
(`0x00469029`, `0x0046902f`):

```
x = left + width / 2 - 0.75 * ( hx cos a + hz sin a )
y = bottom + height / 2 - 0.35 * share * reach - 0.7 * ( hz cos a - hx sin a )
```

with `hx`, `hz`, `share`, `reach` the record's `+0x5c`, `+0x60`, `+0x64`, `+0x6c`. Then the matrix's x column is
multiplied by 0.75 (the screen's 3:4; `0x006fe800`, its three rotation rows) and its z column by 0.1 (`0x006fe7fc`, all
four rows). So the model turns about
the middle of its footprint for a box whose low corner is its origin, and that middle stands on the panel's middle
line, under its middle by 0.35 of `share * reach`. The angle then grows by 0.0004 a millisecond: the constant
(`0x006fe804`) is -0.0004 and the routine subtracts it from the angle (`FSUBR` at `0x00469090`): **0.4 of a radian a second**, on the clock `FUN_004031e0` reads.

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
(`OBJECT_PREVIEW_START_ANGLE`). The ride's and the shop's windows draw it (`ParkObjectWindow`, `ParkShopWindow`); the
buy screen's panel (Q158) takes the same path when it is built. The buy screen also paints a small picture of the item's footprint
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
`UiList.SelectsUnderPointer` is the flag, set on every list (`hud.md`, "The pointer over a list"), and `WindowStack` sends the move. It differs in two
ways, each said at its site (the wait is real time, `Time.WallMilliseconds`, as the original's is); no row is
selected as the list fills, so the panel stays empty until the pointer has been over a row (Q232). The model and the name row `0x1ec` are not in the panel (Q158).
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

`FUN_004a9380` arms the postcard and opens no screen: `UI_PlaySound(0x95)`, then `Game_Pause(0,0)` and **`g_ParkRunning = 0`** (game type `DAT_00fb3b7c` 1 calls `FUN_004bcec0` instead of both), `UI_SetVisible(0)`, `FUN_006591e3()`, `UIParticles_ButtonGlintStop()`, `Advisor_StopQuietly(1)`, and `DAT_007cc190 = 1; DAT_007cc150 = 3`. Those two are a pending flag and a countdown: the UI pass `FUN_0048a580` calls `FUN_004a9350`, which takes one off the count each call and answers 1 on the third, with the UI hidden; `FUN_004a9180` then puts `g_ParkRunning` back to 1 and calls `FUN_00409300` (unless the game type is 1), shows the UI again, calls `FUN_004a8c00` and clears the flag. `FUN_004a8c00` is where the postcard's screen opens: where `FUN_005b6460()` answers non-zero it loads the tree `0x007540d8` with handler `0x004a7d70` (`UI_LoadTree`, kept in `DAT_007cc184`); otherwise it calls `FUN_0047f410(0x4e, 0)` and loads nothing. The feature ships real data — `data\Postcard.wad`, `postcard.jpg`, `data\postcard\legal.tga` and an HTML template *"Theme Park World (TM) Virtual Postcard"* — so it writes a picture out. **Not in scope now; Alexah flagged it as needed later for 100% parity.** The gadget's button is counted as `POSTCARD_BUTTON`, Ctrl+P under the full-screen view as `FULL_SCREEN_VIEW_POSTCARD`; Ctrl+P over the park and in first person is read by nothing and not counted.

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
translation (`FUN_00578d20` mode 3) and normalises it. The park makes one such light for the scene, the sun (`FUN_004584d0`, the only
caller of the light constructor `FUN_00457e70`); each advisor record holds one more, built in place, which lights
him alone, and his draw skips the sun's list (`ui.md`, "How he is lit").

**The sign.** `FUN_00458590` normalises `LightNormal` with +1.0 (`0x0074d0d8`) and never negates it, so (0.4, -0.8,
0.4) is **the direction light travels**: the sun stands above, and a level face gets 0.816 of the sun's colour.

**The vertex colour.** Models go through `FUN_00574660` (from `FUN_0057aa10`), the terrain through `FUN_00574530`
(from `FUN_0056ef10`, from the terrain renderer `FUN_0056f670`); both do the same thing: each channel × 255.0
(`0x007016e4`), rounded, clamped to 0..255, alpha 0xFF, ANDed with the channel mask at `[0x0087a248]+0x2c`
(`AND EAX,[ECX+0x2c]` at `0x0057463f`). The mask is 0xFFFFFFFF in play; the frame renderer `FUN_00576ec0` narrows it
for two passes gated on render-state `0x8`. With render-state `0x800` the colour is the mask itself, fullbright
(low quality's `LIGHTING` 0: "What `GraphicalOptions.LIGHTING` 0 turns off").
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

**Set once.** `FUN_004080e0`, called only from the park load `FUN_00407e00` (`0x00407ef2`), which state 9 calls at `0x0054ed3f`, copies the four values to
`0x008bd47c` (fog), `0x007a0b44` (ambient), `0x007a0b3c` (directional) and `0x007a0b30-38` (direction);
`FUN_00458590` applies them at `0x0054ed64`. The sun is constructed earlier, at `0x0054eca4`, from those globals
before they are filled. Nothing time-driven writes any of them; this does not on its own rule out a day-night look
drawn some other way, which only frames of the original a few game days apart would show.

**OpenTPW** lights a park this way in `content/shaders/test.shader` (per vertex, clamped, rounded to a byte) with
`ParkLight` and `ParkGround.NormalAt`. The lobby and anything drawn on the screen keep the shader's old per-pixel
lighting, but the advisor, lit per vertex by his own record light (`ui.md`, "How he is lit"; Q657). The original's lighter
takes the mesh's `+0x64` table, entry *i* for vertex-order entry *i* (`models.md`, "Normals"), not the smoothed normals
OpenTPW computes: OpenTPW lights the advisor by the table and every other model by its own (Q681).

## The park view's colours: no fog, land outside the park at half light, the compositor's tiles

Decoded and measured for Q479 (`q479/PREDICTION.txt`, `q479/orig/`, the stock park at medium detail): one camera's
grass reads (32.8, 65.9, 16.5) in the original and (33.2, 145.1, 45.5) in OpenTPW. Three things make the
difference, each held on and off in the running original (`original/memhold.py`, `groundcell.py`).

**The park's own view has no fog.** The fog is linear, by a vertex's depth: none at or before a start, the fog colour
alone at or past an end, `f = (d - start) / (end - start)` between. The start and end live at `0x008bd490` and
`0x008bd494` (`1 / (end - start)` at `0x008bd498`, written by `FUN_00583280`), and the colour at `0x008bd47c`
(the theme's `FogColour`, "The lighting model"). `FUN_0044d8f0` sets the start and end from
`GraphicalOptions.FIRSTPERSONVIEWDISTANCE` (`0x007858b4`; the detail file, `low`/`med`/`high.sam`): 0 gives 50 to 60,
1 gives 100 to 110, 2 gives 135 to 150 and 3 gives 230 to 250, with `0x008bd4c0` 200, 325, 450 or 700 beside
them; the startup default is 100 to 130 (`0x00582de5`), the lobby's own 50 to 300 (`FUN_005d8b50`, `0x005d8bfc` to `0x005d8c10`), and 50 to 250 in the three lobby states that call `FUN_005d9680` (`0x005e1407`, `0x005e1980`, `0x005e24a2`), each followed by `FUN_005d96a0( 0xff44ddff, 50.0, 250.0 )`. Each frame
`Render_DrawView` hands the three to the device's fog setter (vtable `0x00701330` slot 6, `FUN_00567ad0`), which sets
the colour and, where the card passes the table-fog probe `FUN_005673d0`, linear table fog. Where it fails
(render-word bits `0x4000` or `0x8000`) the game fogs each vertex itself as a batch is flushed (`FUN_00565590`,
`FUN_00565ee0`): under `0x4000` `Fog_VertexSpecular` (`0x005747d0`) scales the vertex colour by `1 - f` and puts the fog colour times `f`
in the specular, with `SPECULARENABLE` on and `FOGENABLE` off for the batch; under `0x8000` `FUN_00574960` writes
`(1 - f) x 255` into the specular's alpha. **The terrain pass asks for any of it only under render-word bit `0x2000`**
(`FUN_0056f670` calls `FUN_00565ee0` behind it). In a park `Game_StateMachine`'s `XOR 0x2080` (`0x0054fae2`) turns
it on and off as `gui_CameraFlags & 0x16` (a ride's view, first person) comes on and goes off; the lobby sets it in
`FUN_005d9680` (`OR AH,0x20`) for its own fog; `FUN_005d9690`, `0x005d8bf9`, `0x00576eff` and the FOG option's
`AND AH,0x9f` in `FUN_0044d8f0` (`0x0044db16`, with `0x4000`) clear it. Of the word's 50 writes those two are the
ones read setting it by a constant; the rest were not each followed. Live
under Proton at medium the word reads `0x01444100`: `0x4000` set, `0x2000` clear, start 100, end 110 (in first person
`0x01446180`, `0x2080` on, Q485). Held at
10000/20000, and at -2/-1 (every vertex past the end), the park view's frames did not change by one level in any
box of ground, path or object; only the animated sea moved. Read and not measured: the frame is cleared to the fog
colour only under either bit of render-word `0x4002000` (`FUN_00573740`, `FUN_00573cc0`; `AND EDI,0x4002000` at
`0x005737b6`), and to black otherwise. Neither is seen in the park's view: at the farthest zoom, 130, in four
rotations the camera still looks down on ground and sea, no run of near-black longer than 9 pixels in the frame's top
strip (Q481, `q481/orig/a-C-*.png`).

**OpenTPW draws the park's view with no fog** (Q481): `Level.FogDensityFor` is nought in a park, so `test.shader`'s
and `water.shader`'s mix adds nothing, and the lobby keeps its own haze (not the original's: its lobby fogs linearly, 50 to 250, where ours is `exp`; the lobby was not photographed after this change). At Q479's cameras every box reads as Q479's
copy with both shaders' fog set to nought (A's grass (29.9, 139.2, 28.6), B's sea (1.0, 53.7, 55.9)), where main's
read (33.2, 145.1, 45.5) and (8.6, 70.9, 76.2) (`q481/side-by-side.png`). First person, fogged in the original, is
fogged as the original's (Q485, below); the frame is still cleared to the fog colour.

### First person's fog

Decoded and measured for Q485 (`q485/PREDICTION.txt`, `q485/orig/`, `q485/run5/`, `q485/side-by-side.png`): the stock
park, the original's eye at (481.9, 5, 166.3) looking (0.279, 0, -0.960), the clock held, the start and end held by
`memhold.py`.

**The depth is the clip z, not the buffer's z.** `Fog_VertexSpecular` compares the float at a batch vertex's `+8`; measured, the
fog sees it before `ClipToScreen_Fpu` turns `+8` into the 16-bit buffer's z (the order itself was not traced), so it is
still the draw matrix's z row, which in first person reads 1.0001 × its w row less 0.0707: **d = 0.70718 (e − 0.1)** at far 1000, e the eye depth along the view (the
projection's sin 45 × F / (F − 0.1), near 0.1; "The pull"). The far F is the eye view's scan's, not 1000 (`0x56ea90` hands `0x578ca0` the
`[esi+0x1c]` the scan `0x56e7e0` raises, measured 126 to 128 in first person, above): d = 0.70767 (e − 0.1) at F = 126. The 1000 was assumed,
not read; the difference is 0.07 % and changes no fog picture (fork watch 2026-10-28, verify-other O3). Held at 20 to 40 and at 40 to 80, the fraction read along
the ground straight ahead follows (d − start) / (end − start) of that d (`q485/orig/b-fogfit.txt`: eye depth 40 under
20 to 40 reads 0.40, the formula 0.41; measured in e it would be whole); in the eye depth itself, medium's 100 to 110 is
141.5 to 155.6. Where the band is narrower than a ground cell the frame departs from the per-point formula (eye depth
50 at the third line: formula 0.76, read 0.66), as a value worked out at each vertex and blended across the triangle
does. Its edge across the flat path is level: d is planar, not a distance.

**The colour is the specular's.** At -2 to -1 every fogged pixel reads (74, 219, 255): the theme's `FogColour`
`0x48d8f8` through the 16-bit frame (5, 6 and 5 bits: 72, 216, 248 shown as 74, 219, 255). Proton honours the specular.

**What is fogged.** Everything the batches carry under `0x2000`: the ground (`FUN_0056f670` through `FUN_00565ee0`), the
models (the booth, the trees, the topiary, the lamps fade at 20 to 40), the sea (`Line01`'s lake behind the fence reads
the fog colour at the stock band and (10, 58, 61) with it held off), the people and particles (`SpriteBatch_AddQuad`,
`0x00566123`..`0x00566145`; its callers include `SpriteBatch_DrawParticles` and the people's `FUN_00589f90`), and the
people's shadows (`FUN_00589770` at `0x0058a800` under render bit `0x80`, through `FUN_00565f90`: the person's own picture,
alpha-blended, black at the darkness alpha, "A person's shadow"; code read: no person stood near enough in the original's frames to
show one. The orbit view's `SpriteShadow_Draw` goes through `FUN_00566220` and is unfogged there). The sky is not: its ramp is the same
at every band. The viewfinder's quads are fogged too (they fade at -2 to -1 and at 0 to 0.3, not at 20 to 40), so at
every shipped band (the nearest starts at 50) they never are. Whether the weather's drops go through the fogged batch
is not read.

**OpenTPW draws it** (Q485): `FirstPersonFog` reads the band as the park loads, `Level.LinearFog` hands it to the world's
draws while first person is up (the ride's view, the other view `0x2000` is set for, is not built), and `test.shader`,
`people.shader` and `guests.shader` work f out at each vertex from the eye depth and mix the theme's `FogColour` in by it.
The sky's shader takes none. Ours draws 32 bits, so the fog colour reads (72, 216, 248) where the original's frame shows
(74, 219, 255). The viewfinder is interface here and takes none (at no shipped band does that show). Weather drops are
drawn unfogged and counted, `FIRST_PERSON_FOG_WEATHER`. Along three lines clear of people, held at 20 to 40 and 40 to 80,
ours reads within 0.03 of the original's at every depth, its departures from the formula included
(`q485/run5/linefit.txt`); at the stock band the pixels the fog changes are the far hills and the far path and fence in
both (4,062 here, 3,593 there). A shadow is built as the original's `FUN_00589770` draws it (`people.shader` is unchanged): black at the darkness alpha, the
body's own picture, fogged, so over ground fogged whole it is the fog colour and the shadow vanishes (predicted; not yet
measured with a person in the band).

**Entering or leaving first person shows 300 ms of television static** (Q712; fork watch 2026-10-19, verify-advisor-ui.md L4).
`Game_StateMachine` (`0x0054fabb`..`0x0054fb63`): on either edge of `([0x00790ab0] & 0x16) != 0` against the latch byte
`[0x00878e84]` it toggles the render word `[0x008bcbc8] ^= 0x2080` (the fog above), re-runs `FUN_0051c700` with
`[0x0078d908]`, and sets the timer `[0x0087502c]` to 300 with its stamp `[0x00875028]` (`GetTickCount`, `0x0054faf7`). While the
timer is above 0 (tested at `0x0054fb20`, before the elapsed time is taken off at `0x0054fb46`..`0x0054fb57`) the frame is
the static `FUN_0053e760` **instead of** the frame renderer (`FUN_00576ec0`; the jump at `0x0054fb63` skips it). The static
is 320 x 240 into `[0x00874fd0]`, x outer and y inner, one `rand()` (`0x0067b5c0`) a pixel: `& 3` non-zero black, else a
second draw, `& 1` white, else grey 150 (75% black, 12.5% white, 12.5% grey); blitted (`0x005f92e0`) from (0,0,320,240) to
(0,0,`[0x007a1968]`,`[0x007a1930]`), taken as the screen size, not checked. The mask `0x16` is the camera flags of a ride's
view and first person (the camcorder, "Camcorder mode"). Read from the executable, not run; not built here.

**Land outside the park is drawn at half light.** `Terrain_MarkLandOutsidePark` (`0x00450d10`) runs on a fresh park's load (`FUN_005323f0`,
`0x00532629`), on a saved park's (`FUN_00415140`, `0x0041517f`) and after Buy Land (`FUN_005327e0`, `0x005328c3`). It
walks the heightfield's cells, 96 by 85 (the terrain's `+0x18` and `+0x1c`), asks each one's map cell held to 0 to
`[0x007a0314] - 1` and 0 to `[0x007a0374] - 1` (`MapInfo`'s 95 and 84, `0x00450d3d` to `0x00450d64`), and sets the
ground cell's `0x0400` where that cell's `mFlags` has `0x40` (land outside the park, "Building and deleting paths and
queues") and not `0x80`, clearing it everywhere else (`Terrain_MarkLandOutsidePark`); after the walk it makes an identity
matrix (`FUN_0046f150`) and calls `Terrain_ScaleNormalsByHalfLight` (`0x00450b30`) on the terrain model's root (`+0x78`)
and `RenderFlags_ForceRelight` (`0x005836b0`) on `0x008bd508`: the models' half, below. **`mFlags` `0x80` is the cell's attribute `0x10`**,
which `FUN_00536490` carries over as the map loads (`0x0053656d`): in Lost Kingdom the 552 cells of the approach before
the gates, rows 0 to 16 of columns 28 to 67, every one also `0x40`, and every one hidden ground (`0x0001`) in the
running original, so the exception draws no pixel of the stock park. The terrain pass halves a `0x0400` cell's corner
light while render-word `0x0800` is clear ("What the render-state `0x0800` tests are"; `GraphicalOptions.LIGHTING` 0
sets it and `0x1000` beside it, `or ah,0x18` at `0x0044dc36`; 1 clears both, `and ah,0xe7` at `0x0044dc49`; `FUN_0044d8f0`). Live, 5,102 of the 8,160 heightfield cells carry it, every row from 40 on and the last column
and row whole,
4,922 of them inside 0..94 x 0..83 and all 180 of the last column and row (Q482, `q482/orig/a-read-*.txt`); the rule
over the stock file gives every one of the 7,980 cells Q479 read. Cleared over one camera's cells, its grass went from
(32.8, 65.9, 16.5) to (69.6, 133.4, 35.6), where OpenTPW's reads (29.9, 139.2, 28.6) with its fog off.

**The terrain model's meshes on that land are half lit too, by their normals** (Q619). The river's beds and banks,
the cliffs and the rock at the back are meshes of `Terrain\base.md2` (`river*`, `newcliff*`, `volcanoe`, the grass
strips beside them; the live tree has 135 meshes with a 0.225 bin, among them the 40 `basic hoard*` hoardings, which are
half lit only in part (Q620), `Object*`, the flowers and palms, `verge03`, the poles, railing, bridge and falls), drawn by the model pass over cells the terrain pass hides (`0x0001`). The model lighter reads no
cell (`Light_ModelSetup` `0x0057aa10`, `Light_ModelVertexColours` `0x00574660`, `FUN_005741b0`: the ambient plus the
scene's lights, nothing by place); the darkening is set once, at the mark. `Terrain_ScaleNormalsByHalfLight` walks the
model's nodes (siblings `+0x8`, children `+0xc`; a transform-only node, `0x200`, has no vertices), poses each mesh's
vertices into `[0x007aecd4]` with the matrix built down the tree (`FUN_004702d0`, `FUN_00466c80`), and for each of its
`+0x5e` normals (`+0x64`, 12 bytes each, the vertex `+0x94` names) takes the cell `(int)(x × 0.1)`, `(int)(z × 0.1)`
(`0x006fe3a0`, truncated by `__ftol`); inside the field (`+0x18` by `+0x1c`) it normalises the normal to **0.225**
(`0x0074cf30`) where that ground cell carries `0x0400` (`TEST byte [ECX+EAX*4+1],0x4` at `0x00450bf2`, `[0x007a0884]`
the cells) and to 1.0 (`0x006fe380`) elsewhere; outside it the normal is left. `RenderFlags_ForceRelight` sets
`0x40000000` in `0x008bd508` with the frame (`[0x008bcc40]`) beside it, and `Light_ModelSetup` lights a mesh again
whenever that bit is set (`0x0057aa47`), so every mesh takes its new length at once. The ambient is not scaled: a level
face turned to the sun, `ambient + 0.816 × sun` (clamped to 1.0 on a lit cell), reads **0.333 + 0.225 × 0.816 = 0.517**
(blue 0.408 + 0.225 × 0.816 × 0.847 = 0.563) on a half-lit one, and a face turned from the sun, the ambient alone,
does not change. Buy Land calls the mark again, so a cell bought brings its meshes back to 1.0.

**The water is drawn unlit, not half lit.** `DrawList_AddMaterialFaces` (`0x00580530`) gives a material's vertices
their lit colours (the mesh's colour cache, ANDed with the channel mask) only where bit `0x1` of the material record's
last word (`+0x0C`, FileFormats `models.md`, "Materials") is clear; where it is set, each vertex takes the channel mask
itself, white. In `base.md2` exactly the ten water materials carry it (every `surface*` mesh's water and `falls02`; they
sit on frame word `0x33`, which three roof and booth materials, `arrival_roof[1]`, `depart_roof[1]` and `ticket_booths[1]`,
share with word 0); the beds under them do not. Game-wide 929 of 12,960 material uses in 656 meshes of the 847 static
models (the 841 under `data/` plus six copies of the three UI meshes in the language folders) carry it, and no use carries
any other value than 0 or 1 (`lib/matword.py`).

**Measured in the original** (`q619/PREDICTION.txt`, `q619/orig/`, the stock park off-screen, camera (500, 500) 130 at
rotation 90, `q619/side-by-side.png`): every normal of `river13` to `river26`, `newcliff01` to `newcliff10`, `volcanoe`
(1,659 of its 1,672 normals; `modelnodes.py` prints only a mesh's four commonest lengths, the 1,659 and three NaN, each
NaN a bin of its own, so ten were not shown; the file holds 13 normals of length 0, which `0x00450b30`, having no zero guard,
turns to NaN) and every water mesh reads
0.225 long, `road_center` (the approach) 1.0, and
`newcliff11`, across the line, 96 at 1.0 and 34 at 0.225 (`original/modelnodes.py`); a water mesh's lit colours read
(132, 132, 144), the decode's level face. Against OpenTPW at the same camera, which lights them in full: the cliff path
0.48 of ours, the grass strip beside the river 0.53 (green), plain rock on the volcano (53.4, 38.4, 46.6) against
(104.4, 76.0, 85.0), 0.51, 0.51, 0.55; the water 0.90. With `0x0400` cleared on the ground under the view the ground
doubled (2.04) and no model moved (mad 0.00): the meshes' light is the load's, not the frame's. With the water's normals
held at 1.0 its lit colours rose to (255, 255, 255) and its pixels did not follow (+4.4% and +1.8%, the frames either
side −1.5% and +5.0%). A face turned from the sun was not found at these cameras, so the ambient-only half is the
code's alone.

**OpenTPW draws it** (Q482): `ParkGround.LandOutsideThePark` makes the mask as the park loads and keeps it through
every rebuild, and `test.shader` floor-halves the corner's byte light under the vertex flag `ParkGround.HalfLight`. At
camera A the grass went from (29.9, 139.2, 28.6) to (14.9, 69.3, 14.2), 0.498 of it, where the original's reads
(32.8, 65.9, 16.5) with its compositor's patches; camera B's approach did not move. Under `low.sam`'s `LIGHTING` 0
nothing is halved, and every corner is white (below, Q516). Buy Land is unbuilt
(Q407); its purchase must make the mask again. Not run in Full Simulation: a fresh park's cells take their `mFlags` from
`FreshPark` (`0x40` unless a path, `0x80` from attribute `0x10`), which by code follows `FUN_00536490`, so the mask is
expected to match, and no fresh park was photographed. **The terrain model's meshes are half lit here too** (Q652):
`TerrainHalfLight` gives each `base.MD2` vertex, posed by its mesh's `WorldTransform`, the cell `(long)(x × 0.1f)`,
`(long)(z × 0.1f)` and a normal 0.225 long over a half-lit cell, 1.0 elsewhere on the field, left alone off it, and
`test.shader`'s lighter, which does not normalise, does the rest. In Lost Kingdom 9,307 vertices of 135 meshes take 0.225
(the log's `terrain meshes made 0.225 long` line); every one of the 272 meshes' counts is the original's live read
(`q619/orig/b-nodes.txt`) once its zero-length normals, which it makes NaN, are counted with them (`volcanoe` 13,
`bridgeend` 64, `bridge_top` 1): this game lights its own loader's normal there, a deviation said at the site. At the
two cameras above the cliff path, the grass strip and the plain rock read 0.524, 0.522 and 0.51 of main's, 1.12, 1.03
and 1.04 of the original's like boxes; the ground did not move (`q652/side-by-side.png`, `q652/PREDICTION.txt`). A
material whose word has `0x1` (`ModelFile.MaterialData.IsUnlit`) carries `LobbyModel.UnlitFlag`, and `test.shader`
draws it white in every model draw, park and lobby alike. The water, see-through, reads darker than main's where its
bed is half lit (0.87 at one box; over the whole river 83.7 against the original's 75.1, main's 91.1); with the shader's
unlit test taken out the same box falls to 0.60. Buy Land (Q407) must make the normals again.

**The brown patches are the procedural compositor's.** `FUN_0056f380` gives every visible cell inside the box at the
terrain object's words `0x8d` to `0x90` a composited tile when `[0x008bd550]` bit 4 is set (the compositor ready,
"The procedural ground", below), and the cell's own texture otherwise. Live it reads `0x146`. Held clear, the patches
went and the plain cell textures showed, the same small flecks in the same places as OpenTPW's at
(13.3, 72.7, 12.9), the half light still on.

With the three set aside, a brick walk and a road agree within 8 a channel ((116, 54, 53) against (112, 61, 61);
(49, 49, 52) against (51, 50, 59)), so neither the light on lit faces nor the frame's gamma is the gap. OpenTPW's
sea, fog off, is (1.0, 53.7, 55.9) against the original's (1.4, 55.0, 69.4): the blue is the original's moving quilt
over the lake, which OpenTPW lays since Q490 ("The park's sea", below); the filters are `render-states.md`, "Texture filtering" (Q617).

### What `GraphicalOptions.LIGHTING` 0 turns off

**The light itself** (Q487). `DetailFile_Apply` (`0x0044d8f0`) sets render-word `0x1800` for 0 (`or ah,0x18` at `0x0044dc36`), clears it
for 1 (`and ah,0xe7` at `0x0044dc49`) and leaves it for any other value. It runs only from `FUN_0044dc60`, which `Window_Create` calls once a start, directly (`0x0044e1cc`) or through
`FUN_0044de20` (`0x0044e1e6`, `0x0044de8b`): the Options screen's OK writes the quality to `Config.tcf` and the
next start applies it. Every reference to `0x008bcbc8` was walked (188); these are all the readers of the two bits:

| Bit | Reader | What it does there |
|---|---|---|
| `0x0800` | the terrain pass `FUN_0056f670`, 16 sites | No corner at half light ("What the render-state `0x0800` tests are") |
| `0x0800` | the vertex colour, `Light_GroundVertexColours` (`0x00574530`) and `Light_ModelVertexColours` (`0x00574660`) | Every vertex takes the channel mask `[[0x0087a248]+0x2c]` (`0xFFFFFFFF` in play) instead of the lighter `FUN_005741b0`'s sum: each face fullbright, its texture's own colour, the sun and the ambient unread ("The lighting model") |
| `0x0800` | the models' light setup `Light_ModelSetup` (`0x0057aa10`) | Skipped whole: the chain's reset `FUN_00576230`, the model's own lights `FUN_005744d0`, the point light `FUN_00574110` (under `0x0400`), the sun turned into the model's space `FUN_005743a0` |
| `0x0800` | `ModelRenderable_Draw` (`0x0057a822`) | Copied as `0x10000000` into a draw record, only where the renderer `[0x008bd524]` is 4 (live 1) |
| `0x1000` | `Light_ModelSetup`, once, inside the `0x0800`-clear branch | Clear, `FUN_005744d0` chains the model's own lights onto `[0x00879dec]`. Its one writer moves it with `0x0800`, so it changes nothing under any value of the key |

`FUN_00576ec0` also sets `0x0800` (with `0x20000`) for each frame when the renderer `[0x008bd524]` is 4 (`or eax,0x20800` at
`0x00576f02`, the word restored at the frame's end; live 1), the renderer-4 arm beside `ModelRenderable_Draw`'s; `FUN_00570d90`'s
`or ah,0x8` (`0x00570e94`) is on a texture-format word, not this one; nothing else sets `0x0800` or tests either bit.

**Measured in the original** (`q487/PREDICTION.txt`, `q487/orig/`: the stock park, the Options screen's quality track
to Low, a restart). The word read `0x01025900` at low against `0x01444100` at medium: `0x1800` set, `0x400000` clear
(`SKYDETAIL` 0), `0x20000` set and `0x40000` clear (`SPRITESHADOW` 0); `[0x008bd550]` `0x102` (no compositor, no
`0x40`), `[0x007a1a8c]` without `0x6000` (`PROCEDURALTEXTURING` 0). At camera A the grass read (23.4, 141.7, 21.5) and
showed no brown patches, where medium's reads (32.8, 65.9, 16.5); byte 1 held at low with `0x18` cleared, (10.6,
70.5, 9.6), 0.497 of it in green. On level ground, then, all LIGHTING 0 changes is the half light, because a level
face saturates when lit ("What it looks like", above). At camera B the gate's banners and the wall's turned face,
grey when lit, are white at low (the banners' box luminance 90.0 to 129.7, the wall's +20.7; the brick and the road
0.0).

**OpenTPW draws it** (Q516): `Level.Lighting` reads the detail file into `ParkLight.Lit`, which keeps the ground from
half light and, off, has `test.shader` give every corner of a park draw (the ground, the paths, the sea, every model)
white in place of the sum. At low, camera B's banner box went from luminance 87.2 to 135.9 on main's build, the
banners and the fence's cloths white as the original's (shot again at low: 132.3, where lit reads 90.0), the brick
and the road 0.0, the sea +4.9; camera A's grass
stayed (29.9, 139.2, 28.6); medium is main's to 0.0 (`q516/PREDICTION.txt`, `q516/side-by-side.png`). Sprites, the
sky, particles and the weather draw through their own shaders and are not changed by it.

---

## The park's sea: a still lake, and a moving quilt over its harbour

Decoded and measured for Q486 (`q486/PREDICTION.txt`, `q486/orig/a-*`, `q486/ours1/`, `q486/side-by-side.png`).

**Two layers.** `base.MD2`'s mesh 191, `Line01` (113 vertices; x -252 to 1209, y -11 to -10, z -621 to 30), is the
lake at large: `jri_lak2` under word `0x31`, wrapped, and moved by nothing; `basem.MD2`'s eleven UV tracks move the
river's `surface*`, `Object12` and `falls02` alone. Over the harbour by the gates the engine lays a sea of its own,
`WaterMesh`, the second of the three dynamic-faces meshes `FUN_0053bfe0` makes ("Placement feedback", above): `0x86`
faces, mesh id 1. No data file describes it but its corner and its three textures.

**The quilt.** Every park frame, in every view (`FUN_0053c3f0`, called at `0x0054e2fc`), `FUN_0053d380( 16, 19, 20.0,
phase )` fills a grid of 16 columns by 19 rows, 20 units apart: the point at x = 20c, z = 20r stands at height
1.7 cos s (`0x00700e04`), with the normal (sin s, 1, sin s) normalised, where
**s = (2π/3)(x cos 10° - z sin 10°) - 0.5 × phase** (`0x00700dd4` × `0x00700e00`; ten times `0x00700dd0`; `0x00700de4`).
Every sine is `FUN_004708d0`'s 4,096-entry table, a cosine read as the sine of the angle less -π/2 (`0x00700df0`),
the ten degrees' too: entry 114, 10.02°, so cos 10° is 0.98475 and sin 10° 0.17398, which moves s by a few hundredths
across the grid from the exact figures.
`FUN_0053cab0` then lays a quad on each cell of two blocks at the corner `0x00870ec8`/`cc`/`d0`: columns 0 to 9 by
rows 0 to 10, and columns 10 to 13 by rows 0 to 5, 110 + 24 = 134 faces, the mesh's whole room. A quad shows its
texture once (UV (0,0), (1,0), (0,1), (1,1)). Its texture is one of 110 words at `0x00871d18`, the second block
taking the first 24 again, each drawn once as the module starts (`FUN_0053bfe0`: `rand() % 3 + 15`, so `gby_sur1`,
`gby_sur2` or `gby_sur3` of the table at `0x00763b38`). `FUN_0053c9c0` puts each face in the face list the build
squares share (`0x00820b00`), leaving the face's `+0x94` nought, so `FUN_0053d9e0` hands mesh 1's faces to
`FUN_0053e530` (the squares take `FUN_0053df30`): it calls `FUN_0053ddd0` for each corner with the wave and ground gate
nought and the face's normal (the call at `0x0053e566`..`0x0053e580`), then writes the face's two triangles, corners
(2, 1, 0) and (1, 2, 3), so a quad folds along the line from [c+1][r] to [c][r+1]. **It is see-through by its
textures**: each `gby_sur` `.wct` carries an alpha chunk under header byte 1 set, so it is graded at 16
(`textures.md`); the material word 3 (`FUN_0053d790`, `param_1[9]`, and the table's own word 3 for entries 15 to 17)
adds nothing to that, and has no `0x10` or `0x20`, so the textures are clamped. **The folder** is
`Data\Generic\Dynamic\STexture` where `FUN_0054dcc0` answers 1, else `Textures`: with no `dialog.tcf` loaded
(`[0x0078d968]` nought, as in the install) that is the detail file's `TEXTUREQUALITY` (`[0x00785874]`) at 0 or 1, so
low quality; with one, its flag word's `0x800`. The same choice picks the global texture folders (`Boot_Init`,
`0x0054dd02`; `boot.md`). Read live at medium: `[0x00785874]` 2, `[0x0078d968]` 0, the three `gby_sur` cached from
`Textures`, three references each (Q490, `q490/orig/b-run.out`).

**Its corner is `WaterStartPos`.** `FUN_004080e0` copies the theme file's `WaterStartPos` (the balance object's
`0x00785718`) to `0x00870ec8`: (380, -5, -180) in all four parks. The quilt covers x 380 to 580 by z -180 to 40, and
x 580 to 660 by z -180 to -60, at y -5 give or take 1.7, six units over `Line01`.

**How it moves.** The phase `0x00871f60` gains 0.4 a rendered frame unless the clock is paused (`FUN_0053c3f0`
subtracts `0x00700de8`'s -0.4 times the clock's rate, `FUN_00403390`, the double at `0x00785970` + `0x20`, which
`FUN_004032b0` makes 1.0), so s falls 0.2 a frame: one wave every 2π / 0.2 = 31.4 rendered frames, counted in frames
as the squares' wave is. Sampled every 20 units, the wave of 3 units aliases into a regular one: s steps 3.55 rad (or
-2.73) a column and 1.00 rad a row (20 × 2π/3 × 0.174, the table's sine, less 2π), the same everywhere, so the pattern seen is a wave about 6.3 rows long running
along z, not the swell of 3 units. The textures do not move; the corners and their light do.

**Its light**, read and not measured apart from the heave: the normals go to the model lighter ("The lighting
model"), so with the sun travelling (0.4, -0.8, 0.4) a corner takes the ambient (0.333, 0.333, 0.408) plus the sun
times (0.816 - 0.816 sin s) / √(1 + 2 sin² s): nought at sin s = 1, 0.943 at -1. Each corner brightens and darkens
once a wave.

**The path tool's see-through preview shares the machinery, not the wave**: the same module, face list, sine table
and pause gate move the build squares (`FUN_0053ddd0`'s wave branch, sin(phase + x + z) on the squares' phase
`0x00874fc0`, 0.1 a frame), and a square's normal (0, (sin + 1) / 2, 0) dims it in time. The sea's formula is its
own: its phase runs four times the squares' (0.4 against 0.1), its wave twice as fast (s 0.2 a frame against 0.1;
31.4 frames against 62.8).

**Measured** (the stock park, `q325/stock.TPWS`; camera B, (450,30) 110 180; the original off-screen at 30 frames a
second): corner and `WaterStartPos` both (380, -5, -180); the 110 words 38, 44 and 28 times 15, 16 and 17; the two
phases gained 36.000 and 9.000 in 3.00 s, 4.0000 to 1, 30.00 frames a second. The sea box (350,150)-(500,300) changed
by 6.07 (mean absolute, `patchmean.py`'s `mad`) in 0.5 s and by 0.00 with the phase held, the brick and road beside
it 0.00, and a film's box repeated every 31.39 film frames (1.046 s). With the corner's y held at -50, under
`Line01`, the box read (0.0, 50.4, 54.0) and was still, the teal of `jri_lak2` (texture mean (1.1, 53.9, 56.1)), green
0.935 of it and blue 0.963, red nought. With the quilt the box moves over a wave: a-B-1's (2.7, 60.0, 75.7) is a
peak, and over one period of the film (frames 30 to 210) it averages (2.2, 55.3, 69.8), frames from (1.5, 50.3, 63.4)
to (2.9, 60.2, 75.8). The blue is the quilt's: the three `gby_sur` average about (14, 80, 112) at alpha 67 to 207
(`q486/textures.png`). The original's textures are A4R4G4B4, each channel cut to its top four bits (`textures.md`, "The
texture cache's surface format", Q492): that leaves the `gby_sur` a mean red of 7.4 to 9.8, not nought, so the cut
alone does not account for the red near nought; the rest is most likely the 16-bit display, not read (Q700).

**OpenTPW lays the quilt over `Line01`** (`ParkSea`, Q490; `ParkTerrain` draws the lake, and `Water` and
`water.shader` are the lobby's sea): the corner from the balance's `WaterStartPos`, the 110 words drawn as the park
loads (.NET's `Random`, not the C library's sequence), the grid, normals, blocks, triangles and table sines as above,
lit by the park's lighter, graded by the textures' alpha and clamped, from `stexture` at `TEXTUREQUALITY` 0 or 1. **The
phase gains 0.4 a frame at 30 frames a second on `Time.Delta`** where the original counts its rendered frames (said at
the site), and holds while the park is paused. Measured at camera B (`q490/run2`, box (254,141)-(394,281) beside the
parked seaplane, the original's (100,150)-(250,300)): at phase 1 and 1 + 2π, held, ours (7.0, 63.2, 77.8) and
(5.4, 55.0, 67.7) against the original's (2.4, 59.8, 75.3) and (1.3, 51.6, 65.0); half a wave apart mad 9.58 against
8.89, a whole wave 0.00 in both; over a wave (2π of s in eight steps) (6.1, 58.4, 72.0) against the film's (1.7, 54.5,
68.8) in that box with eight bits a channel. With the game's art cut to four (Q700) the eight phases read (3.1, 55.0,
69.9) against the original's (1.3, 55.1, 69.4) held the same way (`q700/PREDICTION.txt`, P9): the red left is most likely
the 16-bit display (`textures.md`). Each run takes its own words, so the box differs run to run (k0's mad 1.8 to 4.4
between two runs of one binary); compare the eight phases' mean, never a frame. The phase gained 11.5 a second
by wall clock, held still under the park's menu. The debug console's `sea` prints the corner, phase, folder and words;
`sea phase P` and `sea y H` move them. The gates' flags, the third mesh, are the next section.

## The gates' flags: eight flags of one grid, turned by the wind

Decoded and measured for Q491 (`q491/PREDICTION.txt`, `q491/orig/`, `q491/side-by-side.png`).

**Where.** `FlagsMesh`, the third dynamic-faces mesh (`FUN_0053bfe0`: `0x180` faces, mesh id 3, the same twenty
textures). Every park frame, after the sea, `FUN_0053c3f0` calls `FUN_0053d570( 7, 5, 1.8, phase )` and then
`FUN_0053cda0` once for each of eight points at `0x00763cf4`, 16 bytes a point (x, y, z and a fourth dword nothing reads: `-1` on the first seven, and on the eighth the first four
bytes of the string `gte_lgo1.tga` at `0x00763d70`, which follows the table):
x 411.25, 451.25, 511.25 and 551.25, each at z 38.75 and at z 50.75, y 22. They are fixed in the executable, whatever
the park; nothing writes them. The grid goes into the sea's arrays (corners `0x00874180`, normals `0x00870ed8`, point
[c][r] at c × `0xe4` + r × `0xc`), after the sea's faces have been laid from them.

**The grid.** Point [c][r], c 0 to 6 and r 0 to 4, stands at x = 1.8c, with P the phase:
D = √(x² + (1.8r + 9)²) − 0.6P (`0x00700e08`), the distance from a point 9 above the flag's top at the pole, and
t = min( x / 5.4, 1 ), 5.4 being (7 − 4) × 1.8;
**y = 0.6 t cos(πD/2) − 1.8r − 0.02x²** (`0x00700e10` 0.3 doubled, `0x00700e0c` 0.25 of 2π, `0x00700e18`) and
**z = 0.3 t cos(2πD/3)** (`0x00700dd4` × `0x00700e00`); the normal is (t sin(2πD/3), t sin(2πD/3), 1) normalised, the
same value stored to its x and its y (`0x0053d6f6`). Every sine and cosine is `FUN_004708d0`'s 4,096-entry table, as the
sea's. So the flag runs 10.8 from the pole and 7.2 down, still at the pole (t = 0) and waving whole past 5.4 from it,
drooping 0.02x², 2.33 at its free edge.

**The faces.** `FUN_0053cda0( x, y, z, 0.5, 6, 4, word, mWindDir × π/180 )` lays each of the grid's 6 × 4 cells as two
faces, front then back. The front's corners are [c+1][r], [c][r], [c+1][r+1], [c][r+1] with their normals negated; the
back's are [c][r], [c+1][r], [c][r+1], [c+1][r+1] with the normals as they are. Each corner is moved 0.5 along x, off the
pole, turned about y by the angle a (x' = x cos a − z sin a, z' = x sin a + z cos a, the table's sine and cosine), then
moved to the point. U runs c/6, nought at the pole and 1 at the free edge; v starts at 1.0 for word 18 and 0.55 for word
19 (`0x00700dfc`) and falls 0.55/4 a row. `FUN_0053c9c0` puts each face in the shared face list with `+0x94` nought, so
`FUN_0053d9e0` hands mesh 3's faces to `FUN_0053e530`, triangles (2, 1, 0) and (1, 2, 3) as the sea's. The words
alternate from 18 at the first point: the flags at z 38.75 take word 18, those at z 50.75 word 19. 8 × 24 × 2 = 384 faces,
the mesh's whole room.

**The angle is the wind.** `FUN_00519590`, on the park at `[0x007cf83c]`, answers the thing whose id the park keeps at
`+0x1da724`, `mWeather` (`weather.md`); its `+0x5c` is `mWindDir`, degrees, which `FUN_00512f00` moves by
`(rand % 90) − 45` at each change of weather. Every flag takes the same angle: at 0 they stream to +x, at 90 to +z.

**The texture.** Words 18 and 19 are both `gte_lgo1` (the table at `0x00763b38`, its word 3 as the sea's: clamped; header
byte 1 nought and every texel's alpha 255 as decoded, so opaque): 128 × 128, the SimThemePark logo in its upper half as this game's `TextureFile` decodes it and
Bullfrog Productions' in its lower, the same file in `Textures` and `STexture`. Word 18's flags wear the SimThemePark half
(v 1.0 to 0.45), word 19's the Bullfrog half (v 0.55 to 0), so v 1 is the decoded picture's top row. Both sides are
drawn; from behind a logo reads mirrored.

**How it moves.** The phase `0x00871ed0` is zeroed with the sea's by `FUN_0053bfe0` and stepped beside it, 0.4 a rendered
frame unless the clock is paused, so the two are equal at every read. D falls 0.24 a frame, so the ripples run out from
the pole's top: the z ripple repeats every 3 of D (12.5 frames), the y wave every 4 (16.7 frames), the whole every 12 of D,
20 of phase, 50 frames. The light comes from the normals through the model lighter, as the sea's (read, not measured).

**Measured** (the stock park, `q325/stock.TPWS`, off-screen at 30 frames a second, camera B; `original/dynfaces.py`
against `q491/flagmodel.py`, the model above): the two phases 431.996 to 467.995 in 3.01 s, equal in 298 reads of 298;
384 faces of mesh 3 at list indices 134 to 517, after the sea's 134, words 192 × 18 and 192 × 19 in the predicted order;
at the live wind (139°), held at 0° and at 90°, and with the phase held at 5, 15 and 25, every corner within 3.0e-5 of the
model, every UV within 4.8e-8, every normal within 1.9e-7. Phase 5 and 25 lay the same faces and the same frame (mean
absolute difference 0.00); 5 and 15 the same x and z and y up to 1.1995 apart, the frame 7.4 apart over the nearer flags.
`a-w0.png` and `a-w90.png` show every flag turned a quarter turn between the two winds.

**OpenTPW lays the flags** (`ParkFlags`, Q493): the grid, the eight points, the faces front then back, the UVs and the
turn as above, in this engine's axes the original's y and z swapped, the triangles the sea's, `gte_lgo1` opaque and
clamped from `stexture` at `TEXTUREQUALITY` 0 or 1, lit by the park's lighter. **The faces are culled**, as the original's
two faces a cell say its are: from camera B (on the flags' +z side, the sun travelling +z) both games show the side whose
normal faces away from the sun, ambient alone, the flags' boxes ours (84.5, 65.0, 73.5) and (106.1, 93.7, 101.0) against the
original's (86.0, 63.9, 72.2) and (109.5, 95.5, 101.9) at the wind held at 0 and both phases at 5 (`q493/side-by-side.png`).
The phase is `ParkSea.Phase`, as the original's two are equal at every read; the angle is `ParkWeather.WindDirection`,
which a loaded park takes from the file's `mWindDir` (139 in the stock park; `weather.md`). Phase 5 against 25 moved the
boxes 0.00, 5 against 15 7.36 and 8.14 (the original's 7.44 and 6.98). Ours is sharper than the original's frame, whose logo
reads blurred (not looked into). **The flags turn to a new wind eased, where the original's jump** (Q643, a deviation at
Alexah's word, said at `ParkFlags.TurnRate`): each frame `ParkFlags.WindAfter` moves the drawn angle the shorter way
round towards the weather's by `Time.SmoothingFactor( 1.5 )`, so a 45 degree change is 10 short after a second and 2
after two; a park opens at the file's wind, the turn holds while the game is paused, as the phase does, and the rate is
chosen, the original having none. Under opentpw.cfg's `graphics faithful` (Q645, `ParkFlags.TurnFactor`) the angle is taken at once, as the original's. The debug console's `flags` prints the count, phase, the drawn angle (`at`), the
weather's wind, the frame's `Time.Now` and the folder; `flags wind W` holds the wind and stands the flags at it at once,
`flags wind free` lets it go; the log has a line at each change of the wind.

---

## The procedural ground

Decoded for Q483 and run three ways: read in Ghidra, `FUN_0055f780` run unchanged under unicorn on the game's own file
(`lib/lndtile.py`), and the tiles the running original makes caught as it makes them (`original/tilecap.sh`,
`lndcmp.py`). A Python model written from the reading (`q483/model.py`) equals the emulator byte for byte on every
cell 0..94 x 0..84 of all four parks at all three tile sizes (12 x 8,075 tiles), and the emulator equals the 60 tiles
the original made in the stock park (`q483/orig/a-cmp.txt`, `a-sheet.png`). The file's bytes are FileFormats `lnd.md`.

**Where.** `FUN_00457c30` sets the box once a park loads (`FUN_0056df70`, `0x00457cf5`) on the terrain object
`[0x00879d60]`: `+0x234` x0 and `+0x238` y0 are `MapInfo.XStart` and `YStart` (`[0x00785584]`, `[0x00785588]`), `+0x23c`
x1 = x0 + `HeightfieldWidth` + `ThemeEngine.ProceduralTexturingXOffset` and `+0x240` y1 = y0 + `HeightfieldHeight` +
`ProceduralTexturingYOffset`, each offset held to nought when below 1 (`FUN_004080e0`); every park ships -1, so the box
is 0..95 x 0..84 inclusive, the whole heightfield (read live: 0, 0, 95, 84). Inside it, while `[0x008bd550]` bit 4 is
set, `FUN_0056f090` gives every visible ground cell (`0x0001` clear) a composited tile; outside it, or with the bit
clear, the cell draws its own texture. The cells' coordinates handed on are box-relative.

**The cache.** `+0x230` is a cache of `[0x008c162c]` tiles (`GraphicalOptions.PROCEDURALTEXTURECACHE` 0, 1, 2 give
1,500, 2,000, 2,500, `FUN_0044d8f0`) in a ring kept in last-used order, found through a 128 x 128 table of slots by
cell (`FUN_00572440`). Up to 2,500 visible cells a frame (`0x9c4`) are looked up; a miss takes the least recently used
tile and composites the cell into it, and a hit composites again only when the cell's word has `0x0100`, which
`FUN_0056f090` clears (`AND 0xfeff`). Every cell caught on its first composite carried it (word `0x104`).

**The tile size.** `FUN_00457a90` asks 16 when `[0x0079d86c]` has 4, 32 when it has 2, 64 when 1 and 32 with
none (`GraphicalOptions.PROCEDURALTEXTURING` 1, 2, 3 set 4, 2, 1; 0 turns the compositor off), and `FUN_00572110` cuts
the ask to 16 when `[0x008bd520]` < 48 MB or the texture memory `[0x008c1624]` < bytes-a-pixel x 3 MB, and 64 to 32 under
64 MB `dwTotalPhys` (`0x00572170`) or when the texture memory is under 5 x bytes-a-texel MB (`0x00572178`). **`[0x008bd520]` is `GlobalMemoryStatus`'s `dwTotalPhys`** (`0x0058357b`) and the test is signed
(`JL`, `0x0057215f`): read live over 4 GB (under Proton) it is -1, so the original makes **16-pixel tiles whatever the detail asks**
there. Between 2 and 4 GB is not measured: `testme.exe` is not large-address aware, and if Windows reports
`0x7fffffff` for it the test is not taken and the texture-memory test decides, so medium could ask 32. Read live at medium: `[0x0079d86c]` `0x82`, `[0x008bd520]` `0xffffffff`, tile size 16, 2,000 tiles.
**The original at 32** (Q618). With `[0x008bd520]` written 256 MB as soon as the boot has set it, before the first park
entry (`original/prepoke32.sh`), the running original makes its cache at 32 (`compstate.py`: tile size 32, the lnd
object's set 1) and draws the stock park's paths and grass about twice as sharp as at 16: at the gate, the path 3.23
against 1.79 and the grass 3.64 against 1.74 (`lib/sharp.py`; `q618/side-by-side-gate.png`). Written before a later
load instead, it changes nothing: the cache made at the first entry is kept (`q618/PREDICTION.txt`, P2). **OpenTPW
draws the size asked** (`Level.TileSizeFor`; 32 at the shipped medium and high, which the original is inferred to have drawn on the machines of
its day (no such machine measured), Alexah's choice of 2026-10-24), a deviation from the original as it runs on today's machines; `save/opentpw.cfg`'s
`graphics faithful` takes the 16 (Q645). At 32 the two games read alike at the gate (the path 3.20 here against 3.23, the
grass 3.72 against 3.64), and every 32-pixel tile of the four parks and the stock park's 78 path tiles equal the
original's code (`ProceduralGroundTests`).

**What fills a tile** (`FUN_0055f780`, thiscall on the lnd object; dest, x, y, record). With N the tile size, the
set of textures for N (64, 32, 16 are sets 0, 1, 2), r = 84 - y (the map's rows run the other way) and
`lerp( q, p, w ) = q + ((p - q) x w >> 8)` on each of a texel's bytes 1 to 3 (red, green, blue; the shift arithmetic):

1. **The gate.** Unless byte 0 of the map's four samples at rows 4r+1, 4r+2 and columns 4x+1, 4x+2 are all 255, the tile
   is grey, `0x7f7f7f00`. In the stock jungle file 1,153 of the 96 x 85 cells are grey, all with word `0x0001` (the
   scenery draws them): 240 river cells (mType 2), 847 of mType 7 and 66 of the approach (30). All 78 path cells are
   composited (Q488, `greycells.py`).
2. **Three weights.** Each of the map's bytes 1, 2 and 3 over the cell's 5 x 5 samples (rows 4r..4r+4, columns
   4x..4x+4) is upsampled to N x N (`FUN_0055e780`: each of the 4 x 4 squares bilinear in 16.16 fixed point, the value
   starting at `a << 16` and stepping by `(c - a) << s` a row and `dx` along it, `dx` starting at `(b - a) << s` and
   growing by `(((d - c) << s) - ((b - a) << s)) >> log2(N / 4)` a row, s = 16 - log2(N / 4); the byte is bits 16 to
   23): u1, u2, u3.
3. **Every texture** (Tk is the lnd object's slot k: the file stores the thirteen in the slot order 0, 1, 3, 4, 8, 9,
   2, 5, 10, 6, 7, 11, 12, `FUN_0055f470`; slots 0, 1, 3, 4, 8, 9 are colour, four bytes a texel, the rest one) is
   read at a window of N x N starting at column `(N x x) mod (w - N)` and row `(N x r) mod (h - N)`, so the 64-pixel set's
   widths (192, 212, 222, 242, 282; the live game's 16-pixel set is narrower) make the pattern repeat slowly.
4. **Three layers**, each two colour textures mixed by a ramp of one grey texture times u1:
   A = lerp( T9, T8, R4[ T10 x u1 >> 8 ] ), B = lerp( T4, T3, R2[ T5 x u1 >> 8 ] ), C = lerp( T1, T0, R1[ T2 x u1 >> 8 ] ).
   Rk is the ramp of the file's k-th pair of header bytes (centre c, width w: 0 below c - (w+1)/2, 255 above c +
   (w+1)/2, `(i x 255 - lo x 255) / (w + 1)` between; `FUN_0055e710`, at `0x008795a8`, `0x008796a8`, `0x008793a8`,
   `0x008797a8`, `0x008794a8` for pairs 1 to 5).
5. **Two masks**: mA = max( R5[ T12 x u3 >> 8 ], R5[ T11 x u3 >> 8 ] ), mB = max( R3[ T6 x u2 >> 8 ], R3[ T7 x u2 >> 8 ] ).
6. **The mix**, for each span of 16 pixels of a row by the class of its masks (all 255, all 0, mixed): mA all 255 gives
   A; mA all 0 gives B where mB is all 255, C where it is all 0, else lerp( C, B, mB ) (`FUN_0055ee10`); mA mixed gives
   lerp( B, A, mA ) or lerp( C, A, mA ) where mB is all 255 or all 0, else **lerp( lerp( C, B, mB ), A, mA ) with every
   pixel of the span taking the span's first u1** (`FUN_0055eaa0` never steps that pointer, `[ESP+0x78]`). A span of
   one class takes the layer whole, never lerp( x, y, 255 ), which is one short. The MMX paths and the byte paths agree.
7. Rows are written into the scratch from its last row up, so the tile's row k comes from texture and weight row N-1-k;
   with no record texture the scratch is the tile. `FUN_00572440` then reverses each pixel's bytes into the surface
   (X8R8G8B8 in memory is B, G, R, X), or converts it where the surface is 16-bit (`[0x00701548 + 4 x [0x008bd554]]`
   2; live it is 2, `0x0057257a`; `[0x008bd554]` is the texture cache's surface format index, `2 + TEXTURE32`,
   `textures.md`, "The texture cache's surface format").

In the jungle file C is brown earth (T0, T1 average, in the 16-pixel set the live game draws, (150, 91, 35) and (168, 112, 51); the 64-pixel set's are (147, 88, 32) and (164, 108, 48)), and B and A are two grasses
laid over it where u2 and u3 lift their masks: the brown patches of Q479's frames are where neither covers the earth.

**A path laid over** (Q489; run under unicorn with the record, `lib/lndtile.py`, and 136 path tiles caught from the
running original with their records and texels, `original/tilecap.sh`, all equal: `q489/orig/a-pathcmp.txt`).
`FUN_0056f090` hands each visible cell a record of five dwords (`piVar2[-1]` to `[3]`): the cell's texture where its word
has `0x0080`, else nought; that texture's width and height (`+0x10`, `+0x12` of the surface); the bytes a texel of the
texture cache's surface format, `[0x007014c8 + 4 x [0x008bd554]]` (2 for the formats 0 to 2, 4 for format 3; 2 live, since
`[0x008bd554]` is `2 + TEXTURE32` and the shipped files hold `TEXTURE32 0`: `textures.md`, "The texture cache's surface
format"); and the word.
`FUN_00572440` swaps the texture for its locked bits (`FUN_00590da0`). With a texture, `FUN_0055f780`'s last pass
(`0x00560926` on) writes each pixel of the tile, in order, as `t + ((p - t) x a >> 8)` a channel: t the scratch's
pixel the word's turn picks, p and a the texel at row `k x (h / N)` and column `j x (w / N)` of the texture, read
unturned. The eight cases at the pass's head, by `0x40` then the first of `0x08`, `0x10`, `0x20` set, read the
scratch as it is, turned (`0x08`, `0x10`, `0x20`) or reflected (`0x40` with each), `q489/turns.py`. At 2 bytes a texel
the texel is A4R4G4B4, each nibble as a byte's top four bits, so a path covers at most 240 of 256 (`0xf0`); at 4 it is
A8R8G8B8. A grey tile returns before the pass (`0x0055f841` to `0x0055f874`).

What is laid. A path tile written on a cell (`FUN_00450470` to `FUN_0046df50`, set 1) puts the theme `.tct`'s PathTex
entry in the cell's texture index (61 + the tile in the jungle, 63 to 81 live) and writes its word `& 0xff87 & 0xfffe |
0x0180`, then `0x20` for 90, `0x10` for 180, `0x08` for 270 (the angle compared with `0x5a`, `0xb4`, `0x10e`), so a path
cell is never mirrored; all 78 of the stock park's read so live (`q489/orig/a-words.txt`). The texture is the full
`pathtex` file, 128 x 128 (not `spathtex`'s 32), and its locked surface holds the file's rows bottom up against
OpenTPW's `TextureFile` (the surface's row 0 is its last). Each nibble is that decode's byte `>> 4` on 97.3% of the
caught nibbles; every other one is one higher, and only where OpenTPW's byte ends in `0xf`: the original's own type
`0x13` decode stands one above OpenTPW's on about half of those bytes (Q460). The terrain pass then draws the tile by
the cell's word, as every tile ("Which cell is handed on"), and `FUN_0046ded0(1)` leaves a `0x0080` cell's turn.

**A built thing's floor is blended into the ground's tiles** (Q617, Q649). The model maker `FUN_00463060`, asked with
flag `0x8` (a thing stood on the land), while `[0x007a1a8c]` has `0x4000` (the compositor on) and the model record's
byte 0 (`[instance + 8]`) has `0x40`, stamps each used cell of the record's footprint table (`+0x18`, below) into the
heightfield, turned by the thing's angle (the record's `+0x10`): the word `& 0x6 | mirror | turn | 0x281` (used `0x80`,
stamped `0x200`, and `0x1`, so the terrain pass draws no ground there) and the entry's frame index above it. The walk
(`0x004633c0`) starts from the model's cell less one in x at 180 and 270 and in z at 90 and 180, so in the covered
cells (`+0xc0`/`+0xc4`) table cell (u, v) lands on (left + u, top + v) at 0, (left + v, bottom - u) at 90, (right - u,
bottom - v) at 180 and (right - v, top + u) at 270; the mirror is kept and the turn carried round, 90: nought to `0x08`
to `0x10` to `0x20` to nought (180 and 270 likewise, the rotation table above). It then calls
`ProceduralBase_BlendFloor` (`0x0057ace0`) over the covered rectangle (`0x004635af`), which for each cell holding `0x80`
and `0x200` reads the frame's texture's top level (`FUN_00575fa0`), makes a texture (`0x0057ae95`) and has `TileCache_CompositeCell` (`0x00572630`), on the tile cache
`[[0x00879d60] + 0x230]`, composite into it the cell's ground tile at the cache's own size (`+4`) with the cell's frame's
texture laid over by its alpha, as a path is ("A path laid over", above; the record handed on is that texture's top
level's bits, width, height, bytes a texel and the word; x and y are the heightfield's own, which the tile maker reads as
box-relative: the same in every shipped park, whose box starts at nought). The new texture takes the place of that
frame's texture in the instance's own frame table (`[[instance + 8] + 0x1c] + 8`, `+0x50`, eight bytes a frame; the old
one is kept in a list at `+0x124` to give back), so **the floor's materials draw the composited tile**; a failure logs
"Could not blend base for ride %s" (`0x0074d36c`). A frame two cells shared would draw the later cell's tile; none of the 51
tables measured below shares one.

**The footprint table** is `ProceduralBase_SkirtFlags`' (`0x0046aa10`), run by `ProceduralBase_Build` as the loader
`FUN_004629d0` makes the model (its flag `0x1`), where the model is exported with a procedural base (header `0x8`, else
"Ride %s has not been exported with procedural base") and a floor was found (`[record] + 0x2c`) or the header has
`0x80`; it sets byte 0's `0x40` as it ends. Width x depth entries of `{ u16 flags, u16 frame }`, `{ 1, 10000 }` for
none. For each face of the floor mesh (the root where none was found) it takes the corners' mean in model space (by the
float third, `0x006fe86c`), cut to cells (`__ftol` of x and z over the grid's cell), and the edge of the three most along
x and the one most along z, each as a share of its length; a face is kept for its cell where both shares pass 0.9
(`0x006fe870`) and either passes the kept face's. Then (`0x0046af02` on) a cell is entered where the header has `0x80`,
or where `Footprint_EdgeMask` (`0x0052c420`: a cell of kind 4, 9, 10 or 0x17 not ringed on all four sides by such cells)
has it and its kind is not nought, and a face was kept: the kept face's three corners stand on three corners of the
cell, 0 low x low z, 1 low x high z, 2 high x high z, 3 high x low z (from each edge's low and high end along its axis),
the fourth's UV completes the parallelogram, and the corner nearest the least u and least v of the four gives the turn
(nought, `0x08`, `0x10`, `0x20`), with the mirror `0x40` where u changes more than v from that corner to the next; the
frame is the face's material's. The loader has moved each floor face's UVs by whole numbers into 0..1 first
(`FUN_004679d0`), which leaves the answer alone. **Queue pieces** are loaded with flag `0x2` (`0x12423`), which makes
`FUN_00461f10` set the header's `0x80` and every mesh node's `0x20000` (so no floor is found, `textures.md`, "Which
materials the loader makes opaque"): their one cell is entered whatever its kind, from the root mesh.

**The blended floor's texture clamps.** The UV normaliser `FUN_00467310` (eps 0.02 at every caller; each face's least u and v floored and
subtracted from its corners not yet moved; `0x30` cleared from the word of a material group no counted face uses, `0x00467978`) does not
make the blended floor wrap: the composite is made by `0x00590a80` with flags `0x1000000` and the mip count, no wrap bits
(`0x00590ac4`, `0x00590ad7`, `0x00590b1c`), so `Texture_BlendStateWord` gives it no `0x400000` and the mesh draw (`0x00581790`)
clamps it whatever the material's word says (the per-draw state is inferred; the frame loop is not traced). The clear of `0x30` matters only to a later
reuse through `0x00575c60`. The unmasked call (`0x0046264f`) is gated on loader bit `0x4000`, which no shipped catalogue, queue or
type-3 load carries. Fork watch 2026-10-27, verify-world M1.

**Measured in the running original** (`q617/orig/run.sh`, `q649/orig/run.sh`, `run-items.sh`: `brk.sh` on the call at
`0x0057af4b` as `loadfile.sh`'s poller, and `original/footprints.py`): the stock park's load blends 44 cells, all on tick
755, every texture laid over 128 x 128 at 2 bytes a texel and every word holding `0x281`, at the cache's size, 16 there
and 32 under `prepoke32.sh` (`q617/orig/a-load.log`, `q649/orig/a-load.log`, the same cells and words); by OpenTPW's
`cell` they are 40 footprint cells (types 4, 9 and 10) of eleven things and the four queue cells (type 3) at (49..52, 22)
before the Belly Bounce (`q617/cells.out`). The tables of the 41 Lost Kingdom item models the catalogue loads, of the
eleven placed instances and of the seven queue models (eight loads) are as above, cell for cell (`q649/orig/a-footprints.txt`,
`b-items.txt`; `ProceduralFloorTests`); the two coaster pylon models hold one cell with no entry. Q617 compared the Belly
Bounce (camera (525,240) 110 180) at 16: the original's floor as soft as the ground beside it, sharpness 2.38 against
1.79 (`lib/sharp.py`), OpenTPW's then 10.91 against 1.11, its own texture. **OpenTPW blends a floor as the original**
(`ProceduralFloor`, Q649): the same cells, turns and mirrors (ours drops the cell's own `0x6` bits), and at 32 boxes wholly on the floor read 1.82 and 1.58 times the
ground against the original's 1.55 and 1.19, half as sharp as the floor's own texture, in the original's colours
(`q649/side-by-side-floor.png`, `q649/PREDICTION.txt`). The lobby calls the
same load (`FUN_005d84a0`) but no lobby wad holds a `base.lnd`, so the bit is not set there.

**Which cell is handed on, and how its tile is laid** (Q488). `FUN_0056f090` keys each visible box cell
`(row - [+0x238]) x 0x100 + col - [+0x234]` and `FUN_00572440` hands `FUN_0055f780` x = the low byte, y = the rest, so
the box's last column 95 is composited too: its fifth sample column is the next sample row's first (the map is read as
one run; `ProceduralGroundTests` holds (95, 40) to the emulator). The terrain pass lays the tile as it lays a cell's own
texture ("The UV rule", above): texel centre to texel centre, row 0 at the cell's y edge and column 0 at its x edge, and
with the compositor on unturned, but for a path cell (`0x80`), which keeps its word's turn (`0x0046defd`, `test cl,0x81`; "What is laid", above). Measured in both games at camera A (`lib/cellmatch.py`): the original's frame shows
the cell's own tile on 84 of 122 cells, OpenTPW's on 115.

**Why the ground shows cells of two sharpnesses** (Q617, from Alexah's play test of 2026-10-21). At the park gate
(Q471's camera (475,155) 110 180, `q617/gate-ours-over-original.png`) both games then drew the open ground as 16-pixel tiles
magnified bilinear (render-states.md, "Texture filtering"; OpenTPW has drawn 32 by default since Q618, below), and both draw the strips beside the gate path crisp: those
cells, (45,18) and (50,18) among them, are type 7, hidden ground the scenery of `base.MD2` covers with its own texture,
so **the seam there is the original's own**. Sharpness (the mean absolute Laplacian of luminance), strip against ground:
the original 8.62 against 1.73, OpenTPW 16.12 against 1.20 (`q617/PREDICTION.txt`, P4, P5): at 16, OpenTPW's strip was the
sharper and its ground the softer (at its 32 now the gate grass reads 3.72 against the original's 1.74 at 16, Q618); eight bits a channel (Q616), the anisotropic sampler against the original's nearest
texel and mip, and the two resolutions (1280 x 720 against 1024 x 768) are not separated. The one seam the original has
not is a built thing's floor ("A built thing's floor", above). **The 16-pixel tile is the memory test misreading a large
machine** ("The tile size"): the shipped `med.sam` and `high.sam` ask 32 (`PROCEDURALTEXTURING` 2), which `FUN_00572110`
keeps wherever `dwTotalPhys` reads as a positive 48 MB or more and the texture memory as bytes a texel x 3 MB or more.
Under Proton the texture memory reads 33,554,432 at 2 bytes a texel (clear of that bar) and only the signed `dwTotalPhys`
test cuts the ask (`q618/PREDICTION.txt`, P2); that 32 is what the original drew on "the machines of its day" is an
inference, no such machine was measured. OpenTPW draws that 32 (Q618, "The original at 32", above).

---

## The blocking unknowns, worst first

1. **Which grid size is authoritative**: 96x85 (mesh), 95x84 (`.sam` MapInfo), 95x85 (the engine's own derivation). Settled for the picker (Q472): the terrain is tested over the heightfield's 96x85 cells and the cell is held below `HeightfieldWidth` and `HeightfieldHeight`, 0..94 and 0..83 ("The picker never hands the mode a cell off the map"). The camera sweep's `FUN_0042cd40` holds to the same; what else reads them is not read: of the 22 read instructions of `[0x007a0314]` in 17 functions, Q472 read three functions (`FUN_0042cd40`, `FUN_0045cdc0`, `FUN_0045d560`) and Q479 a fourth (`Terrain_MarkLandOutsidePark`, `0x00450d10`, its clamp at `0x00450d4d`), which leaves 18 sites in 13 functions (`FUN_0055abf0`, with four, among them).
2. ~~The lighting model.~~ Settled: see "The lighting model" above. `LightNormal` is the direction light travels.
3. ~~The diagonal-choice bit.~~ Settled: see "The triangle diagonal" above (`0x0002` drawn, `0x0004` heights).
4. ~~`base.MD2`'s container walking.~~ Settled: the heightfield is the block the header's `0x6C` pointer names ("The heightfield lives inside base.MD2", above); nothing walks a chunk table.
5. `base.map`'s per-bit semantics beyond `0x08`.
6. ~~The procedural compositor.~~ Settled: see "The procedural ground" above. Its entry is `FUN_004504c0` (from
   `FUN_00457a90` on a park's load, and from the lobby's `FUN_005d84a0`): past the gate above it builds `%sbase.lnd` and
   calls `FUN_0056dfe0`, which (unless `[0x008bd550]` has `0x10000000`) calls `FUN_0056e550` on `DAT_00879d60`. That
   needs `DAT_008bd524 == 1` (live 1, not identified), makes the cache (`FUN_00572110`), sizes the scratch
   (`FUN_0055f720`, `DAT_008798c8`) and reads the file (`FUN_00560ff0`, refused unless byte 0 is 3), then sets
   `[0x008bd550] |= 4`. How the terrain pass lays a tile: "The procedural ground", Q488.
7. ~~The park camera's terrain-following sampler.~~ Settled: `FUN_00452560`, called at `0x0042c817`; see "The park camera" above.

---

## Building and deleting paths and queues

Decoded 2026-09-21 by a six-dimension pass over `/testme.exe`, each dimension re-derived by a second
agent told to refute it.

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

A refused release (the latch `0x00816d48` set, tested at `0x00524a63`) ends by the mode's byte in the table at
`0x00527694` (index mode - 1, `0x00524ac7`) through the jump table at `0x0052766c`: modes 1 (path), 3, 0xB and 0x10
(track), 0xE, 0x13 and 0x36 (patrol) put the tool away (`0x005266a3`, `0x0052f200( 0, 0 )`); mode 0x16 has its own exit
(`0x00524ad4`: `0x00446050`, then clears `0x00816d48`, `0x00818c54`, `0x008186d8`, the mode kept); mode 4 (the carry) and
every other mode keep the tool (`0x0052765b`). Fork watch 2026-10-26, `verify-all.md` M2.

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
  its partner, so **`mNeighbours` is legitimately asymmetric**. **The weak one is inside the mType 1 arm
  of each cardinal block alone** (north: `0x00534987`..`0x00534a0d`; the mType 10 and mType 9 arms after
  it, `0x00534a18` and `0x00534a48`, write the two bits and retile): a ride's end a path joins gains the
  one bit toward it and no diagonal. Of the 249 ride ends in the original's park files to hand (the
  shipped Lost Kingdom, the eight Full Simulation files, two saved under Proton) none holds a diagonal
  bit (`q256/ends.py`; its control, a file of OpenTPW's from before Q256, shows one, `0x82`);
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
categories: the person under the pointer and the locators (below), the gate and ticket booths ("The gate and the ticket
booths" below), and track cells — type `0xb` and `0x10` have categories of their own, and a type
`0xc` cell whose parent is `0x19` has none at all (`0x004873b3`..`0x004873c5`), so the 429 such cells in
Lost Kingdom arm nothing. A guest on the cell does not stop it.

**The person under the pointer decides first** (Q444; `FUN_00486d90`, the modifiers by `GetAsyncKeyState`: Shift 3,
Ctrl `0xc`, Alt `0x30`; the locator `[0x007cc2f0]` by `FUN_004b4480` at `0x00486dd6`; the person is the sprite pick's
`[0x007b05ac]`, "The thing under a sprite" above). With the visitors' locator open (2) and no modifier, or Shift alone
and no locator, a guest (kind byte 1) is category 7, the visitor's window (`FUN_004b79b0`), and anything else is
category 0, a member of staff too (`0x00487009` to `0x0048702d`; `0x00486fd0` is the test that the mode is type 0 or 1). Otherwise a member (kinds 5, 4, 6, 7, 8) is category 8 to 12, their
window (`FUN_004b6080`); then with either locator open every later category is skipped (`0x0048721c` to
`0x004878d3`), so a click on ground, path or an object does nothing (category 0 falls out of `FUN_004879d0`'s switch
answering 1). Measured in the original (`q444/orig/`, `PREDICTION.txt`): the staff locator open, two points off any
person read category 0 and their clicks left the mode and the screens as they were; Mike under the pointer read 9 and
the click opened his window and folded the arm; the visitors' locator open, the guest at the middle read 7 and the
click opened the visitor's window; the visitors' locator and Shift together, the locator's own guest read 0; with no
locator Shift alone read 0 on bare ground and 7 on a guest, whose click opened their window.

**What the hover says over a person** (Q447; `FUN_00486d90`). A row is posted (`FUN_004924b0`) only when the category or
the thing `[0x007c24f8]` changes. A guest taken (category 7) posts UIHELPTEXT 553 "Visitor #{P3} : Left-click to view
this person's details", P3 the guest's number `+0x1d8` (a `VARM` value of type 3), or 554 "New Visitor : ..." at nought;
a member (8 to 12) posts 555 "{P2} : Left-click to view this staff member's details", P2 the name at `+0x1a0`
(`FUN_00506860`, type 2), and calls `FUN_00505f50` (`0x0048720d`), which is PICK UP's test `FUN_00505bd0` (the
state not 4, 5, 7, `0xd` or `0x12`, and `FUN_004fa990` passing the member's cell: type 0, 1, 3, 9 or 10) followed by
CStaff's `SetState`, `FUN_005054d0( 0 )`; a guard's `0x12` test after it is reached and can never pass. So a member the pointer
comes onto is set idle: from a walk the stamp `+0x200` is the clock and the wait is the grade's, from anything else
nought (`ride-operation.md`, "The idle wait, and the stamp it counts from"). Either puts up pointer 2, which `FUN_00489720` registers as `c_pick.ani`. Where the
guests decide and nobody is taken the empty row `0x0078a374` is posted, only if the last hover category was not
nought (`0x004870f3` to `0x004870ff`; otherwise nothing is posted and the old row stays; each hover keeps the old
category and zeroes `[0x007c24f4]` at `0x00486de5`, and its arm sets its own), and pointer 0 put up; the tool's mode arm
(`0x00486fd9`) and the switch's default (`0x004877a3`) post it on the same test ("The arms that post nothing" below); with a locator open
otherwise and nobody taken, the jump from `0x0048721c` to `0x004878d3` posts nothing and puts up pointer 0, so the row
last posted stands. Measured in the original: the staff locator open, Mike Cooper under the pointer read category 9
with his row and the hand, and off him category 0 with the row still at the foot and the arrow (`q444/orig/a-4-member-hover.png`,
`a-2-P1.png`); the visitors' locator open, a guest whose `+0x1d8` read 9 showed "Visitor #9 : ..." and the hand, and
bare ground category 0 no row and the arrow (`q447/orig/`). Here `Level.WorldHelpRow` answers the row, its values and
`HoverPointer`, and posts it (`Level.HoverPosted`) only where the original posts, on a change of category or person. Measured in both (Q467,
`q467/`, `PREDICTION.txt`): the visitors' locator opened with Ctrl held read category 0 with "Left-click to build path"
standing, and Ctrl let go it stood on at category 0; off the followed guest (category 7, "Visitor #13") the bar was
emptied.

**The arms that post nothing** (Q469; the listing `objdump` of `0x00486d90` to `0x004879d0`). Every mode type but 0 and
1 reaches `0x00486fd9`, save 8 (the coaster builder) and type 3 with the build mode `0x15` or `0x16`, which go to
`0x004878e4`, and 9 (the camcorder's pick), which posts its own rows `0x244` and `0x243` (`hud.md`, "The help bar in first person"): so the build shell `FUN_0046c580` (vtable `0x006fe9e0`, whose `+0x24`
`0x006b71c0` answers 3, so the path tool and every build tool too), a candidate (5) and a worker (6). There the thing
`[0x007c24f8]` is forgotten, no pointer is put up, and the empty row is posted only if the last category was not nought.
Where no person is taken: no cell (`[0x007b05cc]` nought, `0x00487240`), a type-12 track cell under a type-`0x19` parent
(the hoardings, `0x004873bd`: 429 cells in Lost Kingdom, 426 of them on map type 0), and a cell of map type 4, 9 or 10
with no object (`0x00487667`; the object is `FUN_00527e80`'s, the parent cell's) post nothing and put up pointer 0, the
category nought. An object whose item's `AddOn.UpgradesId` (`+0x284`, `FUN_004e2710`) is not nought posts nothing too
unless its `AddOn.UpgradeType` (`+0x288`) is 2 (`0x0048768d`): Lost Kingdom's three add-ons (1500 to 1502) are all type
1, track-ride add-ons, which nothing here builds. Types 11 and 16, and 12 under any other parent, ask the cell's thing and
the track's (`0x004873d5` to `0x00487441`: two of those ways return with no post and no pointer); none is in Lost Kingdom
(its track types are 7, 12 and 25), and ours counts them (`HOVER_TRACK_CELL_CATEGORY`). Bare ground (`FUN_00536390`,
`0x0048750d`, category 2) is tested before the switch, which it skips (`0x0048758d`); then the map type's switch (table
`0x004879b0`): 1 path (1), 3 a queue (3), 4, 9 and 10 an object (4), `0x15`
(6), and every other the default `0x004877a3`, which posts the empty row only after a category and puts up pointer 0: in
Lost Kingdom map types 7 (9,077 cells, among them the land past the entrance's fence and the gate's two outer cells,
which the gate's test takes first), 2 (240) and 30 (66, the approach, the ticket booths among them, taken first too). Built in `Level.GroundArmAt` and `Level.WorldHelpRow`. **The picker meets no cell only
before its first pick** ("The picker never hands the mode a cell off the map" above): over the sea, and past the
heightfield's last column and row, the original keeps the last cell, so its row stands, and so does ours (Q472b,
`ParkPicking`). Measured in both (`q469/`,
`PREDICTION.txt`, `side-by-side.png`): from (43,16) "Left-click to build path" onto the hoarding (42,16) category 0 and the
row standing, on to (44,16), map type 7, still standing; from (43,16) straight onto (44,16) the bar emptied, and onto the
hoarding still empty; the camera then moved to the sea, the row stood. Not run in either game: the Q467 standing row (or
Shift let go) carried onto a default cell or a hoarding, which only a locator folded with the pointer already there reaches
(an open locator takes `0x0048702d`, never the ground's arms); the rule is the one round b's hoarding at nought then
type 7 measured, and ours has it from tests alone.

**The gate and the ticket booths** (Q471; `FUN_00486d90` `0x004872a4` to `0x0048730d`). After the test for no cell and
before the cell's own categories, the hover compares the cell id `[0x007b05cc]` with the entrances' (`FUN_004d8690( 0 )`
and `( 1 )`, `EntranceA` and `B`), with A's less one and B's plus one (the cells either side on their row, `0x004872be`
to `0x004872f0`), and then with the booths' (`FUN_004d8610( 0 )` and `( 1 )`, `TicketBoothA` and `B`). The gate is
category 14 (`0x00487868`): UIHELPTEXT 459 (`0x1cb`, `0x00487885`) "Park gates - left-click to open/close the park or
change the ticket price (G)"; a booth category 13 (`0x004877d3`): 460 (`0x1cc`, `0x004877f0`) "Ticket booth - ...", the
same words. Each posts its row only when the last category was another, forgets the thing and puts up pointer 2,
`c_pick`; the gate's arm also calls `FUN_00469180` (`0x004878ce`), the booth's does not. `FUN_00469180` is a model
highlight swap: it puts back the material of the model held in `[0x007a1ce0]` (clearing `0x20000` on its `+4`), then, if
`[0x007afda0]` (set by `0x00469340`) holds a model with no `0x20000` and `[0x007afda4]` is nought, tints each of its
meshes (`0x1000000`, a colour block, `0x20000`, `0x400000`) and stores it in `[0x007a1ce0]`. Mesh word bit `0x1000000` is a run-time bit (no shipped mesh carries it: 0 of 4,923 meshes and 2,606 transform records of the 2,129 `.md2`; `FUN_00469580` and this swap set it): `Light_ModelSetup` (`0x0057aa30`, `0x0057aa66`) skips the relight for a mesh with it, and `DrawList_AddMesh` (`0x00581d56`) then takes the draw's colour stream from mesh `+0x64` instead of `+0x9c` + 4 (fork watch 2026-10-28, verify-hz-world H2, verify-hz-data D1). The hover calls it at six
places in all (`0x00486f6b`, `0x004874fe`, `0x00487712`, `0x0048778d`, `0x004878ce`, `0x0048798b`). Which model
`[0x007afda0]` holds over the gate is not read, so whether the gate's model lights up is not known; nothing here
highlights (Q24). The click's switch (table `0x00488168`) sends 13 and 14 to `0x00487c93`, `FUN_00498d80`, the entry price's
builder, as G does: it notes itself as category 3's screen 10 (`FUN_004a0810( 3, 10 )`) and posts the advisor's `0x12f`
(`FUN_00486b00`). In Lost Kingdom the gate is (46,17) to (49,17), the middle two path (map type 1) and the outer two
type 7, and the booths (47,13) and (48,13), type 30. The original's picker answers row 17 over the gate model's face:
at camera (475,155) 110 180 the arch's pixels from y 320 to 540 read gate cells, bar one row at y 480 that read row 16
(`q471/orig/x-scan.log`).
Measured in both (`q471/`, `PREDICTION.txt`): the booth category 13 and its row with the hand, the four gate cells 14 and
the gate's row with the hand, (45,17) and (50,17) category 0 and the bar emptied; the click on (47,17) and on (47,13)
each opened the entry price screen (`[0x007ca210]` set), where main's build posted 442 and armed the path tool
(`q471/control1`). Built in `Level.GateArmAt`, `Level.WorldHelpRow` and `Level.ClickWorldAt`. The rows over a track
(`0x1be`, 446) and type `0x15` (`0x1c0`, 448) are not built: ours posts the empty row there and counts
`HOVER_TRACK_CELL_CATEGORY` and `HOVER_TYPE_15_ROW`, from the hover alone: the click's and the pointer's tool tests ask
the same arm and do not count. The queue's and an object's are built (below).

**The rows over a queue and an object** (Q478; `FUN_00486d90` `0x00487603` to `0x004877a2`). Every row of these arms
is looked up in `[0x0078ba98]`'s table (`FUN_006ace60`) and, with the table's `[0x0078ba80]` set, decoded into
`0x7ba058` by `FUN_006adc80`, the `.str` reader's own character decode (its bytes through the table's lookup), not a
change of words. A **queue** (map type 3) is category 3: UIHELPTEXT 444 (`0x1bc`) is posted only when the last category
was not 3, the thing `[0x007c24f8]` is forgotten (`0x004877b9`) and pointer 4 put up, `c_queue` (`FUN_00489720`
registers it). An **object** (map types 4, 9 and 10; the thing `FUN_00527e80` answers for the parent cell, `0x0048734c`)
is first asked whether it is an add-on: its item's `AddOn.UpgradesId` (`+0x284`, `FUN_004e2710`) not nought and its
`AddOn.UpgradeType` (`+0x288`, `FUN_004dd4e0`'s item) not 2 jumps to `0x004878d3`, nothing posted and the thing kept.
Of the shipped add-ons, the eleven `upgrades/*.wad` items, all are type 1 (a track ride's) but space's `zob2` (3502,
type 2, on 3109): Lost Kingdom's 1500 and 1501 fit 1150, 1502 fits 1160. Otherwise it is category 4, and the row is
posted only when the object is not the thing the hover holds (`0x0048769d`), **whatever the category**: the thing is set
to it, `FUN_004e2780` writes the object's name (its two lines joined, `hud.md`, "The status's row 16") and UIHELPTEXT
550 (`0x226`) is formatted with it as a `VARM` value of type 2, `{P2}` (`FUN_006acd60`, `0x004876fd`), and posted; either
way `FUN_00469180` runs (the model highlight, Q24) and pointer 2, `c_pick`, is put up. So the thing decides: the arms that
leave it (no cell, a hoarding, an object's cell with no object, an add-on, all through `0x004878d3`; nobody taken where
the guests decide, `0x004870f3`, which empties the row after a category; a locator open with nobody taken, `0x0048721c`) let the same object post nothing after them,
and every arm that forgets it (the ground `0x0048757d`, path, queue and the default `0x004877b9`, type `0x15`
`0x00487783`, a track `0x004874f4` (its exit `0x004873f8` keeps it, and `0x0048740e` goes on to the switch), the gate and the booths, a tool armed `0x00486ff7`) or takes a person (`0x00487216`) lets it post again.
Type `0x15` is category 6: 448, posted on a change of category, the thing forgotten, `FUN_00469180`, pointer 11
(`c_pylon`). Built in `Level.GroundArmAt`, `Level.WorldHelpRow` (`ObjectPosted`, `_objectTaken`), `Level.IdleCursor`
(`HoverPointer`) and `ItemDescriptionFile.AddOnUpgradesId`/`AddOnUpgradeType`. Measured in both (`q478/`,
`PREDICTION.txt`, `side-by-side.png`, ours `run4`), the stock park at camera (480,265) 130 180 (the original's categories and things were read live and not stored: `q478/PREDICTION.txt` R4 records the read, `q478/orig/` the frames): over the queue (50,22) category 3,
thing 0, "Left-click to edit this line" and `c_queue` (the reference install reads `Language/american`, whose 444 says
line; ours reads `English`'s "queue"); over the Belly Bounce (52,24) category 4 and "Belly Bounce: Left-click to
view/edit this item's details. Hold CTRL and click to buy another" with the hand, its model lit; over the Drinks Shop
(43,31) category 4, another thing, its row with the hand. Ours posted 444 with `c_queue` and 550 with each name and
the hand, the ride again posting nothing; main's build (`q478/control1`, at zoom 150, the same cells) the empty bar and the arrow. Both set the long row
on two lines, broken before "CTRL" (Q484; `hud.md`, "The help bar's lettering").

**The hover runs only on the park's view** (Q451). `Park_MouseMessageProc` runs `FUN_00486d90` on its timer
(`0x00488582`) only while `[0x007c24fc]` is set. Message `0x10001`, the pointer coming onto the layer from a control,
forgets the thing `[0x007c24f8]`, runs the hover once (no park screen open), then sets the flag and the category
`[0x007c24f4]` to nought (`0x0048884e` to `0x00488870`), so the next hover posts the same row again and, over a member,
sets them idle again. `0x10002`, the pointer leaving for a control, and `0x15` (its sender not traced) clear the flag.
Measured in the original (`q451/orig/`, `PREDICTION.txt`): the flag read 1 on the view and 0 over the mechanics' button.
With the staff locator open, Mike Cooper walking (state 1) went to 0 with stamp 902 on the tick the hover took
him, and walked again at 913 (grade 3: IdleDuration 10, + 1). The locator's camera kept him under the resting pointer,
category 9 throughout, and he was not set again. Over the gadget's buttons the pointer was the arrow, with the button's
own row. Here `WindowStack.PointerOnTheView` (no control or park screen's body under the pointer, no modal window, no
cover) gates `WorldHelpRow`, the return runs it as below, and `Level.IdleCursor` is the arrow off the view. **The set
itself is built on the sprite pick** (Q468; "The thing under a sprite"): `Level.WorldHelpRow` sets the member idle as it
posts their row wherever `ParkStaffWindow.CanBePickedUp` passes. Measured in both (`q468/`, `PREDICTION.txt`): the
original set Mike idle on the take at 907 and he walked again by 924, the pointer resting with category 9 and no second
set; ours set him at 810 and logged "Staff: 26 leaves idle for Walking on mGameTick 821, idle since 810", one set while
the follow carried him over eight cells. In both a member beside him who became the nearer under the pointer was taken
and set in turn, each change a new take. The gate is hit-tested where the hover asks
(`WindowStack.PointerOnTheView`), so the frame the pointer reaches a control takes nothing behind it.

**The hover after a return to the view** (Q473; the bar itself is `hud.md`, "The help bar holds one posted row"). The
control left behind empties the bar (its `0x10002`), and the timer's hover after the layer's `0x10001` reads the cell
picked before the pointer moved, so the hovers of the return are at the last place on the view: the `0x10001` hover with
the old category and the thing forgotten, then, the category nought, one that posts that place's row if its arm posts
one; the next hover is at the pointer's own place. Why the pick lags is not read from code (the timer's pick
`FUN_0046c2a0` at `0x00488557` reads the library's pointer `[0x00faa540]`, `[0x00faa544]`, before the hover at
`0x00488582`). Measured in the original (`q473/orig/`, `PREDICTION.txt`, a single jump from the Buy button onto the
hoarding (42,16); `poll.py`, about 280,000 reads in 2.5 s): with the ground (43,16) left last, the leave hid the label at
556.7 ms, the layer's entry came at 584.9 ms, the category went 2, 0, then 2 with the cell still 2092 and the label shown
with 441, the cell became 2091 at 585.6 ms and the category 0 at 589.6 ms, 441 standing; with the hoarding left last
the category stayed 0 and the label hidden. A park screen's body is a control too: coming off it the category is
nought, so the hover posts again once the screen is shut. Here `Level.WorldHelpRow` keeps the last place on the view
(`_lastHover`) and runs both hovers there on the return, the second set idle of a member from idle, so with the stamp
nought; every arm posts only on a change of category or person (`Level.Posted`), and under a park screen nothing is
posted, so the row stands beside an open screen.

**Who a cell's list names as people walk** (Q446). Every person is relinked into the cell they stand on whenever it
changes: the walk tick `FUN_004fa2a0` calls `FUN_0050b6a0` at `0x004fa2fc` (the placement `FUN_004fa930` at
`0x004fa976`), which compares the thing's cell bytes `+5` and `+7` with the new position's and only then unlinks
(`FUN_004d9280`) and relinks at the head (`FUN_004d91f0`). It serves staff as it serves guests; the pick-up
`FUN_00505c50` unlinks nobody, so a carried member stays on the list of the cell they were taken from. The load keeps
every cell's chain as the file has it (FileFormats `saves.md`, "Thing records"), so a person who heads a cell in the
file and walks off leaves whoever the file chained behind them its head. Measured in the original (`q446/orig/`,
`original/cellhead.py`): the stock park's guard 28, saved alone on (39,28), paced between (39,25) and (39,28) and the
cell's chain read `[28]` while he stood on it and `[]` each time he was off it; with the guards' locator open the
pointer over the empty cell read category 0 and its click opened nothing, and on him category 11 and his window. Here
`ParkState` seeds the lists from the file's chains and `StaffBehaviour.Step` ends with the relink, as
`PeepBehaviour.Step` does for a guest; the pointer takes the person by the sprite drawn under it ("The thing under a
sprite") and the console's `worldclick` the head of the cell's list.

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
| 5 | cost: mType 0 or 3 adds `Costs.PathCell` to the run; cash below the total, asked only when the total is not nought (`0x0053590a`) | red, latch, `c_cash` |
| 6 | mType 1 as the last cell | `m_end` (11) |
| 7 | mType `0x15` | red, `DAT_00816d58`, which the square's second verdict copies into the latch |
| 8 | track corner (`FUN_0053ae00`: two cardinal links at right angles) or junction (`FUN_0053ae90`: three or more) | red, `d58`, copied as row 7's |
| 9 | mType 1 or 0 | blue |
| 10 | mType 3 with exactly one of its eight bits set, toward another queue cell | `m_link` (8); any other queue cell red and latch — **the owner is never asked** |
| 11 | any other mType | red, **no latch** |

NOMODIFY is never read for a path. The cursor (`FUN_0052f950`) is `c_link` if any square was `m_link`,
else `c_end` if any was `m_end`, else `c_path`; a red square overrides with `c_noplace`, a cash refusal
with `c_cash`. One detail is not reproduced: for the one preview after the pointer leaves an anchor it
was resting on, the anchor still shows `m_end` (`DAT_00818688`). **Every red square is judged twice**: the square's arm of `FUN_00532fc0` calls the verdict again with
its third argument 1 (`0x00533927`), throws the answer away and sets `DAT_00816d48` (`FUN_00527940( 1 )`, `0x00533933`). That call
copies `d58` into the latch (`0x00535885`) on the same square: a `d58` refusal latches at once, the last square
included (built, Q538). It skips only the earlier latch's return (`0x00535898`): land outside the park still answers
first, a thing's cell latches (`0x00535d95`), track `0x19` latches, and past them the cell is charged again into the
run's total `DAT_0081af4c` (`FUN_0052fba0`, `0x005358f4`: the mode's price, `FUN_0052fb20` for the path tool and
`FUN_0052fb40` for the queue tool, on a cell `FUN_00535600` lets the mode build on that is not already its type, a path
under the queue tool excepted) and the money is asked again (`0x005358fc`..`0x0053593f`: the total not nought and above
the balance sets `d50`, `d48` and `DAT_00816d5c`). So a red square after a latch is paid for by its second verdict alone,
a red square the first call charged (a corner on bare ground, a cash refusal) is paid for twice, and once the total
passes the balance the cursor is `c_cash` (`FUN_0052f950` asks `d5c` last), where the squares' own verdicts would leave
`c_noplace`. The pass does not reset the total for these two tools: `FUN_004a0db0`, a control's message handler,
sets it to nought on its message `0x1e` (`0x004a0dee`) and on every third one (the counter `[0x007cb2e0]`'s old value has bit 1 set, `0x004a0dcb`; reset at `0x004a0de2`,
and zeroed by message `0x15` too, `0x004a0e02`) first calls `FUN_004a0ab0`; how often
that comes against the pass, and what is drawn of the total, is Q541's. Built (Q542): `ParkPathBuilding.JudgedAgain`, the run's total each square's `Owed`, the console's `strip X Y`
printing it as `run total` and a `$` on a square refused for money.

**A click** (`0x005271b7`..`0x00527655`, on release at the preview's target): any red square lays
nothing, plays `0xaf` and puts the tool away — the arming click included, so a click outside the park's
land arms and refuses at once. Otherwise sound `0x65` ("The laying sound", below), then: unanchored, set the anchor, empty the
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

**The laying sound** (Q632). The commit sends modes 1 and 3 alike to `0x005271b7` (`0x00524b0f`..`0x00524b2d`),
whose first act, with no layout replay running (`[0x00785a2c]` nought), is
`Sound_PlayEffect( 0, cat_ui [0x00803a2c], 0x65, x * [0x00700c9c], 0, y * [0x00700c9c] )` at `0x005271ea`: x and y
are the click's cell and `[0x00700c9c]` is 10.0. It comes after the red refusal (`0x00524a63`, `0xaf`) and before
every arm, so **every click of the path tool or the queue tool that is not refused sounds it once**: the click that
arms and anchors, each click that lays a run, a click on the anchor. The commit's one other play of `0x65` is the
coaster bar's track tool, mode `0x15` (`0x00526423`). `cat_ui` effect `0x65` is `rj_laypath_2` (`sfUiHD.sdt`, volume
100, 123 ms; FileFormats `sound-categories.md`), flagged `0x0201`, as 29 of the category's 45 effects are, so it
sounds flat ("A voice's range" in `audio.md`) and goes through the deferred queue that bit `0x1` names. `original/oneshots.py`'s
walk did not see it (one 25 s walk, two clicks, `0x65` only; nothing in the script filters `0x1` out, and whether a deferred voice
ever joins the list it walks is not shown). The queue tool's finishing link adds
`0x8b`, `connectPath`, at `0x00527587` ("The run's last cell may be a path" above).
Measured in the original (`q632/orig`, the stock park, its music off): the hand empty, a click on bare grass at
(62,18) read mode 1, anchor (62,18), nothing laid; a click on (66,18) laid five cells of type 1 and moved the anchor
there. A hardware breakpoint on `Sound_PlayEffect` for effect `0x65` hit exactly twice in 30 s, once a click, each
returning to `0x005271ef`, category 3 (`[0x00803a2c]`), handle 0, x 620 then 660. In the mix `rj_laypath_2` scored
0.29 to 0.35 in each click's window against 0.16 to 0.19 in the quiet stretch, in three runs; `laysmall` scores nearly
as high there, as the two samples match each other at 0.30, so the breakpoint is what names the effect.
The queue tool's clicks measured in the original (Q671, `q671/orig`, the stock park, its music off, the same breakpoint):
the click on the Belly Bounce's queue cell (51,22) with the hand empty hands the queue tool over (mode 3, anchor (49,22))
and plays nothing; a click on (47,22) lays queue over the path at (48,22), joins the path and puts the tool away, one
hit; a second click on (51,22) hands it back anchored on (48,22), nothing; a click on that anchor puts it away, one hit.
Each hit "handle 0 cat 3 effect 65", returning to `0x005271ef`, x 470 and 480; nothing else in 50 s. In the mix
`rj_laypath_2` scored 0.383 and 0.268 at the two, 0.227 and 0.175 at the handing-over clicks, 0.148 to 0.208 quiet.
OpenTPW plays it in `ParkPathBuilding.LayingSound`, from `RunPath` and `RunQueue` after the red refusal and on the queue
tool's anchor click, at its variation's volume, whatever screen is open; the log says "path: cat_ui 0x65 for the click on
(x,y), a voice" (or "queue: ..."). On the stock park (`q671/run1`) the same four path and queue clicks each scored
`rj_laypath_2` 0.947 to 1.000 at gain 0.50 in the mix, and the handing-over clicks 0.19. The coaster bar's track tool's
play (mode `0x15`) waits on that bar.

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
`LEA ECX,[ESI+0x20]`; in the save it is record offset `+8` (the FileFormats `saves.md`). The shipped
Lost Kingdom has 14 non-zero path cells, all corners and junctions — eleven at 1, (47,21), (48,21) and
(48,28) at 2 — and the queue node (52,22) at 1.

**`mFlags & 0x40` marks land outside the park** — the meaning is inferred from a census. `FUN_00536490` sets it as
the map loads on every cell but the first paths (`0x00536553`, `0x005365b5`), and `FUN_005311c0` tests it (`FUN_00535db0( 0x40 )`) and clears it (`FUN_0053a530( 0x40 )`, first at `0x00531327`), and Buy Land's apply arm reaches that (tool `0x39` tested at `0x00525c4e`, `FUN_005327e0` at `0x00525d52`, the call at `0x005328b4`), as does the terrain initializer `FUN_005323f0` (`0x00532557`). 13,878 of Lost Kingdom's 16,384 cells carry it, all
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
(`0x7cdba8` is S, `0x7cdbc8` is E, `0x7cdbd8` is NE). **Cite the initialisers and `FUN_004d97e0`'s jump
table, never the address ordering.**

### Two things these dimensions disagree about, recorded rather than resolved

**Which array the build code mutates.** One decode read
it as the **0x44-byte** `CMapCell` at `DAT_007cf83c + 0x294`, the other as the **0x28-byte** record at
`DAT_007cf83c + 0x1102B0`, and their field offsets agree exactly (`+0x08` type, `+0x0c` mask, `+0x0d`
direction, `+0x0e` flags, `+0x10` owner, `+0x14/+0x18/+0x1c` tile) — which is what made it look like a
flat contradiction rather than two readings of two arrays.

Where it now rests: the adversarial pass over the placement decode calls the 0x28 reading **backwards
and refutes it four ways**, the clearest being the line walker `FUN_00536100`, which forms the cell
pointer as `SHL ECX,4; ADD ECX,EAX; LEA ECX,[EDX + ECX*0x4 + 0x294]` — id × 17 × 4 = id × `0x44`. So
**the build path writes the 0x44 record**, and the 0x28 array is the separate track layer. The
adversarial pass over the *tools* decode has `FUN_00522850` fetching the direction byte from
the **0x28** record, and that is right too: `FUN_00522850` is the bare `MOV AL,[ECX + 0xd]` and reads
whichever record it is handed. ExitRide (`0x0050158b`) and the demolisher (`0x0052826b`) hand it a 0x44
cell; `FUN_0053b280` hands it its own `this` (`0x0053b3a0`), a 0x28 record (`0x0053b2d5`..`0x0053b2e2`).

Nothing in OpenTPW depends on it — cells are modelled as records rather than as raw memory.

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
- mType 2 is the 240 river cells, every one a heightfield hole (`0x1`; fork watch 2026-10-25, `verify-forks.md` section 5); mType 5 is unidentified; the decode refused to guess water or rock.
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
