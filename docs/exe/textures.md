# The `.wct` decode

What the original does with a `.wct` file's bytes (the bytes themselves: the FileFormats clone, `formats/texture.md`).
Read in Ghidra on `/testme.exe`, read only (Q354, Q355, Q357, Q358).

## The loader, `FUN_00564c00`, and the alpha chunk

It reads the 28-byte header, refuses a type other than `0x12` or `0x13`, reads the colour chunk, and reads the alpha
chunk **when the bit count (header byte 2) is 32** (`0x00564c72`) into `+0x20`; the byte at offset 1 is tested and
passed over there (a `JNZ` round a `NOP`, `0x00564cc0`), and the version is masked to its low two bits. The alpha chunk
is the codec's sixth argument (`0x00564eb3`): none, and the image is opaque.

**Byte 1 chooses the alpha reference.** The texture's caller `FUN_00574bd0`, on a `.wct` that loaded, sets the flags
`[0x0087a24c]` to `0x24`, to `0x25` when the alpha chunk was read (`+0x20` not nought), and to `0x27` when header byte
1 (`+5`) is set as well, then ORs in the masked version shifted 8; the pixel classifier `FUN_00575160` is not run for a
`.wct` (it is the other formats' path). `FUN_005755f0` turns bit `0x2` into the texture's `0x200` and, without it, bit
`0x1` into `0x100`, so a `.wct` with an alpha chunk draws **cut out** (state word `0x2756`, reference 240) under a byte 1
of nought and **graded** (`0x556`, reference 16) under one set, and one without a chunk opaque (`render-states.md`,
"The four state words"). Of the 236 files whose alpha chunk sits under a byte 1 of nought (FileFormats, `texture.md`),
every one draws cut out in the original.

**OpenTPW** (Q358) reads the alpha chunk by the bit count, so those 236 decode with their alpha (233 of type `0x13`)
and `stexture\b_NEWwin.wct` (byte 1 set, 24-bit, no chunk) decodes opaque where it threw; `Texture` logs each cut-out
texture as it loads (`Texture: '...' reads its alpha chunk by its bit count, header byte 1 nought: N of M texels clear,
K under 240`). Since Q359 the reference is the original's: `TextureFile.Alpha` is the three states above (opaque, cut
out, graded), `Texture.Alpha` takes it for a `.wct` and counts the pixels only for another format (`Texture.Classify`,
`FUN_00575160`'s rule), and a model's material is cut at 240 or graded at 16 by it; a material with `0x2` over a texture
with no chunk draws solid, as the original's state word gives it. Since Q362 **a material's alpha is drawn unless the material is `0x800`**, its file's or the loader's (below, "Which
materials the loader makes opaque"), and its `0x2` plays no part (`LobbyModel.SeeThrough`), as the original's state word
reads no other bit. Of the jungle's, the lobby's and
the global advisor's `0x2` uses, 99 of 555 change reference (`q359/flips.py`): in the stock park the Belly Bounce's sign
(`gk_sign1`, `gk_sign2`), the toilets' door (`j_wc2`), the Staff Room's roof (`strawroof`) and the advisor's four mouths
go from 240 to 16 (their byte 1 is set; their partly-clear texels are under a twentieth), and his Kiosk hat and the
lobby's `Sea` draw solid. Each model logs the materials whose texture's alpha and `0x2` disagree (`<model>: N
material(s) whose texture's alpha and 0x2 bit disagree - mesh:material Alpha, ...`), as `q359/predict.py` predicts
them from the files. The path tile `jpa_tju2` is drawn by `ParkPaths` with no flag, opaque. The interface keys a
texture's black out before any of this, so `ipan.wct` (the training screen's rows, the message box) draws the same
either way.

**The river's banks.** Each river mesh of `levels/jungle/terrain/base.MD2` draws `jri_bnk4` to `6` under `0x2`; their
river side is clear (`jri_bnk4`: 962 of 16,384 texels, its bottom row all clear, its top row opaque). Before Q358 the
banks were opaque and drew a black seam between the water and the near bank; the original under Proton, given the same
file, draws none, and neither does this game now (`q358/side-by-side.png`). Their frames carry no wrap bit (`0x3`), so
the original clamps them (`render-states.md`, "Texture addressing"), and so does this game since Q363: sampled with
Wrap, the bank's opaque top edge took in its clear bottom row and the cut-out test dropped it, a thin pale line along
each bank's outer edge (451 line pixels on main's frame, 38 since, none in the original's: `q363/side-by-side.png`).

## What a floor's clear border shows

Every ride's, shop's and sideshow's model opens with a floor plate as wide as its footprint (`park.md`), and most of
those plates' textures carry an alpha under a material without `0x2`: a run into the stock park loads 54 such uses,
six of them the lobby's Space island (`m_grass1` and `m_grass2` at the Belly Bounce, the Jungle Spray's `mj_grass*` and `ss_gplnk*`, the
toilets' `J_wc1`, the terrain's `dk_water3`...). Their border is painted grass at alpha nought (`m_grass1`: 3,328 of
16,384 texels clear, its top and left edges), so the original's state word drops it, and the original's frame of the
same file shows green there, the dirt meeting the park's ground (`q359/orig/a-2-full.png`, `orig-crops.png`). **Drawn
here by the texture alone, the same border shows the sky's clear colour** (`q359/cmp-bellyclose.png`): this game draws
no ground under a footprint (`ParkObjects.CoversGround`), so nothing lies under the clear texels. That is why the
`0x2` gate stays (Q359), and what the old note "removing the gate exposed holes" was.

**The original draws no ground under a footprint either, and what shows at the floor's clear texels is the composited tile** (Q360, read again at Q617). `FUN_005365d0`
(and `FUN_005366e0`) ask `FUN_00535dd0` for a cell's ground tile, which answers 8 for cell types 2, 4, 7, 9, 10, 21 and
30 (a built thing's footprint is 4, 9 and 10); `FUN_00450470` hands that to `FUN_0046df50`, which ORs `0x101` into the
heightfield cell's flag word (`[[0x007a0854] + 0x6c] + 0x2c`, four bytes a cell, the flag word low and the ground's
texture index high) and clears `0x80`. The ground's drawer is the terrain pass `FUN_0056f670`, called by the scene's
draw `Render_DrawView` (`0x00576c8b`) every frame: it builds the ground's vertices afresh from `[[0x00879d60]] + 0x2c`
over each row's visible columns (`+0x128`..`+0x12c` the rows, `+0x130 + r`..`+0x1b0 + r` a row's cells) and skips a
cell whose flag word has `0x1` (`0x0056f9ce`); its texture fetch `FUN_0056f090` skips the same cells. The second pass
`FUN_00570d90` is a layer of its own heights and colour, off in the park (`[0x008bcbc8]` is `0x01444100`: no `0x10000`).

Read in the running original (`original/groundcell.py`, `q360/orig/`), on Q359's `run-fix3` file: the terrain pass's
heightfield is the base model's (`0x10db4e50` both); every one of the Belly Bounce's twelve cells has `0x1` (`0x0303`
to `0x0363`, its two middle cells `0x0103`); open grass holds `0x0002` or `0x0102`. **Bit `0x1` written on four grass
cells left of the Belly Bounce opened a black hole two cells square on the next frame** (`c-2.png` against `c-1.png`,
the box's mean (0, 0, 0) against (80.5, 121.9, 40.4), 15,496 of its 30,000 pixels under 8 against 6): behind the ground
is the frame's clear colour, black. **With `0x1` on all twelve of its cells, so no ground under it, the Belly Bounce's
floor border is green** (`a-2-loaded.png`), as it is with the bit cleared and the ground drawn under it
(`a-3-later.png`); the 37 pixels under 8 in its box in each frame are the trench's and the queue's, none on the
border. So the original shows green at the plate's clear border where this game's texture alone would cut it. Q360 read
that as the plate's own painted grass; Q617 found the floor's texture replaced by a composited tile (below), so what
shows at a clear texel is the ground tile blended in, and the plate's own texels are not shown to be the source.

**It sets the floor's materials `0x800` when the model loads** (Q361), the opaque word, which Q360 took for the reason the clear texels show; Q617's composited tile (below) is what is drawn there, and whether `0x800` still matters is not read: clearing it changed nothing on screen. The object model loader
`FUN_004629d0`, given flag `0x1` and a model exported "with 'has procedural base'" (its `+0x30` has `0x8`, else it logs
`0x0074d774`), calls `ProceduralBase_Build` (`0x0046a5a0`), which finds the floor mesh (`+0x2c`, marked `0x4000`) and
calls `ProceduralBase_SkirtFlags` (`0x0046aa10`, "Has %d flipped and %d non-flipped skirt textures"). That walks the
floor's triangles against `Footprint_EdgeMask` (`0x0052c420`: a 16 x 16 byte mask, 1 on a footprint cell of type 4, 9,
10 or 23 with a neighbour outside the footprint): a triangle of material 0 on an inner cell CLEARS the material's
`0x800` (`0x0046ac11`), every other triangle SETS it (`0x0046ac1e`), so a material's last triangle decides. The flag
word is the `.md2` material's own (the file holds `0x1`), and `0x800` is the bit `FUN_00581790` turns into the opaque
word `0x1256` (`0x00581820`; `render-states.md`). `Mesh_SetMaterialsOpaque` (`0x0046a550`) sets or clears it over a
whole mesh (the loader's flag `0x2000`, not looked into further). Nothing else in the code ORs `0x800` into a material.

Read in the running original on `q359/run-fix3/q359-fix.TPWS` (`q361/`): the Belly Bounce's floor mesh holds nine
material groups (`m_grass1`, `m_grass2` four times, `m_grass3` twice, `ss_PATH` twice), every one `0x0801` or
`0x0821`, and its body's (`Bd_42` to `Bd_muth2`) `0x0001` to `0x0031`, none with `0x800`; **of every loaded model's
materials over a floor texture (`m_grass*`, `mj_grass*`, `ss_gplnk*`, `J_wc1`, `cn_grass`, `sq_grass1`), 174 of 174
carry `0x800`** (`original/floormats.py`; per texture the cache's reference count but `m_grass3`, 23 against 19, not
looked into); no loaded floor's material 0 lies on inner cells alone. While the park draws, nothing reads those
materials, their texture's flags or the footprint cells' texture words (a read watchpoint on each, 15 s, against
`mGameTick` and the Belly Bounce's body texture, both read at once); `m_grass1`'s cached texture has no surface
(`+0x0c` -1, `+0x18` nought). Holding the textures' `0x300` additive, the footprint cells' texture words or the
floor's `0x800` cleared changed nothing on screen (`orig/belly-4.png`, `h-3.png`, `k-2.png`): the floor's draw state
is made at the load. **A procedural base's floor draws composited tiles, not its own texture** (Q617): as the model is
made, `FUN_0057ace0` blends each used footprint cell's ground tile with that cell's texture and puts the result in the
model's frame table in the texture's place (`park-engine.md`, "A built thing's floor is blended into the ground's
tiles"), which fits `m_grass1`'s cached texture having no surface. The texture cache
(`[0x0087a268]`, 4,096 entries of {folder, name, texture, refs}, the count at `+0x10000`) holds `m_grass1` once,
`data\levels\jungle\sharetex\m_grass1.TGA`, flags `0x11070281` (graded, version 2, `0x40000` from the loader's `0x20`,
`0x30000` from materials' `0x10`/`0x20`).

In the original's park view screen x runs against the cell's x: the path at x 56 lies left of the Belly Bounce at
51..53. Every cell Q359 poked lay outside the pass's visible range on that view (rows 13..32, row 23's cells 45..63),
so its pokes were not unread, only unseen.

## Which materials the loader makes opaque

**Loader flag `0x2000` decides, after the skirt pass** (Q362). Right after `ProceduralBase_Build` (`0x00462c69`), the
loader `FUN_004629d0`, asked with `0x2000` (`0x00462c71`), calls `Mesh_SetMaterialsOpaque( model, mesh, 1 )`
(`0x0046a550`): `0x800` on every material of the floor mesh `[model + 8] + 0x2c`, or of the root mesh (header `+0x78`)
where no floor was found, unless that node is flagged `0x200`; then the same on the root mesh of the item's `P` model,
if it loaded one (`[+0xd0]`, `0x00462ca7`), with no floor search. So `ProceduralBase_SkirtFlags`' clears never survive
on a load that runs it. A material's flag word is its frame-table entry's: the bit reaches every material of the model
naming the same texture (`m_grass1` on another mesh of the Belly Bounce would be opaque too).

| Who loads | Flags | `0x1` (base) | `0x2000` | Footprint |
|---|---|---|---|---|
| The catalogue, an ordinary item (`FUN_00413c10`, `0x00414175`) | `0xb24a9` (`0x4b24a9`) | yes | yes | the item's picture |
| The catalogue, a fixed item (`Info.DontApplyOffset`, `0x00414192`) | `0x50c00` (`0x450c00`) | no | no | — |
| The catalogue, an item's supplementary models (`0x0041437e`; the bumper boats' here) | `0xc0` | no | no | — |
| Queue pieces (`FUN_00522900`) | `0x12423` (`0x112423` when the row's byte `+5` is set: `0x0052295d`) | yes | yes | 1 x 1 at `0x00816d68`, kind nought |
| Type-3 items' pieces (`FUN_0042fad0`) | `0x1013405` | yes | yes | none |
| `FUN_00430130`, `FUN_00522aa0`/`c60`/`e40`, `FUN_005d7fd0` | `0xc0`, `0x10422`, `0x422`, `0xc0`/`0x40c0` | no | no | — |

**The floor is the mesh with most base vertices** (`FUN_00469a80`, its count mode). It walks the nodes from the root,
a node before its children, skips one flagged `0x200` (not its children), counts nothing on one flagged `0x20000`, and
takes each vertex through its node's matrix into model space (the file's axes, y up). A vertex counts when its height
is within 0.01 of nought and its x and z are each within 0.01 of a cell line of the park's grid (`[0x007a0854] + 0x6c`:
origin nought, cells of 10 in all four parks; a coordinate under the origin taken as nought), on a side of its cell
the footprint's outline runs along: `ProceduralBase_Build` gives each cell of the picture an edge bit per side where
the cell's kind (its low four bits) and that neighbour's differ in being nought (`1` low z, `2` low x, `4` high z, `8`
high x; past the picture is out), and a vertex on the cell's low x line asks `2`, high x `8`, low z `1`, high z `4`. A
cell past the picture's last is its last, asked from the far side. Header bit `0x100` would count every corner (no file
stores it). The first mesh to hold more than every one before it is the floor; `[model + 8] + 0xc` is the count over
all meshes. With no outline (a queue piece's kind-nought cell, no footprint) nothing counts and the root is taken.

**Measured in the original** (`original/floormesh.py` on `q359/run-fix3/q359-fix.TPWS`, `q362/floormesh-b.txt`): it loads
every catalogue item on offer, and **54 of 54 Lost Kingdom item models agree with OpenTPW's `ProceduralBase.FloorMesh` on
the count and the floor** (`q362/sweep.txt`): every floor is the item's mesh 0, its count the outline's corners (the Belly
Bounce 14, Sign 1 14, the 2 x 1 Log 6); the park gate, a fixed item, counts 0. Every floor's and root's material word
carries `0x800` (the Belly Bounce's `0x801`/`0x821`, Ptotem's `0x0803` with `0x2`), the seven queue pieces count 0 with
their root's `0x0831` (`jpa_que1`/`2`/`3`), and `jpa_que5`/`jpa_que6` on their second mesh have none (`0x0011` to `0x0033`).
The terrain (`base.MD2`, header `0x3`, not loaded by `FUN_004629d0`) keeps `dk_water3`'s word `0x8031`, no `0x800`.

**OpenTPW** (`ProceduralBase`) builds the same for a placed item (its `Info.Shape`), the object window's preview (the
item's own model the same way, a `P` model by its root) and a queue piece; a fixed item and the bumper boats' extra
models are given none, and no type-3 piece is loaded here. Each such load logs `<model>: N frame(s) set 0x800, K
material(s) whose texture's alpha is drawn opaque`. In the stock park K adds to 44 over 15 loads, the 54 old
alpha-without-`0x2` uses less the terrain's four `dk_water3` and the lobby Space island's six (`sfl_cnr3`,
`sho_top1`..`4`), which now draw their alpha. **On screen** (`q362/run-fix-4` the final build, `run-control-2` main's): the
waterfall sheet under the jungle's bridge (`Object12`, `dk_water3` alone) is see-through where main drew it solid dark
blue, 5,017 pixels at `camera 530 560 40 180`, blue minus red 92 to 38; the original's frame of the same saved park reads
18 there (`q362/side-by-side-fall.png`). The low pool's east end (`surface27`) changes 501 pixels the same way.

**Across Lost Kingdom's catalogue** (`q362/sweep.txt`): newly opaque are the floors whose materials carry `0x2` (`totem`,
`puzzle`, `minecart`, `mumbo`'s planks, `s_plant`'s base) and the `P` models' roots (`Pgokarts`'
flags and sign, `Pwateride`'s exit, `Pcoaster3`'s ground with its bamboo and palm leaves); newly see-through are
alphas on no floor without `0x2` (the rocks' `jfe_rck1`/`2`, `mumbo`'s tongue, `coaster1`'s `E_15_LEAF`/`E_16_LEAF`).
None of these stands in the stock park; none was photographed in either game.

## The header's scales, and the order they are handed on (`0x00564e20`)

The texture loader turns the header's four 16-bit scales into multipliers and hands them to the codec object at
`[0x00879a64]` (vtable `0x007026b8`, slot 2, `0x00592060`: type `0x12` to its primary vtable's slot 0, `FUN_00593ab0`,
the zlib codec; `0x13` to slot 2, `FUN_00595920`, LZSS). From the header (offset 0 the type byte):

| Offset | Multiplier | Handed on as |
|---|---|---|
| 8 | `1 + n x 0.5` (`[0x007011b8]` 1.0, `[0x007011c4]` -0.5) | the first channel's, Y |
| 12 | `1 + n x 0.25` (`[0x007011c0]` -0.25) | the second channel's, **Cb** |
| 10 | `1 + n x 0.25` | the third channel's, **Cr** |
| 14 | `n + 1` (`[0x007011bc]` -1.0) | the alpha's |

**The scale at offset 10 is Cr's and the one at 12 is Cb's.** Both codecs decode the second channel with the
second multiplier and convert it with 1.772 into blue (`FUN_00593ab0`: plane `0x00a667f8` times `[0x00702694]`;
`FUN_00595920`: `0x00f34a48`), the third with the third and 1.402 into red (`0x00caf228` times `[0x00702698]`;
`0x00cf0a28`). The same order is in the loader's second path (`0x00564ef0`, from `0x00564f21`).

**OpenTPW** (`TextureFile.ReadFromStream`) reads offset 10 into Cr's scale and 12 into Cb's. In `ui.wad` this changes
442 of the 448 textures whose two scales differ (the six left alike are flat or grey) and none of the 473 whose two
are equal (`q354/sweep.sh`). `ridestatbar.wct` (Y 6, offset 10 = 10, offset 12 = 2) decodes red to green, as the
original draws it (`hud.md`, "The stat bars' skin").

## The texture cache's surface format (fork watch 2026-10-18, `verify-other-b.md` L1, `verify-ui-hud.md` U1)

`[0x008bd554]` is the texture cache's surface format **index**, not the display mode. The window setup (`0x0044e1f4`,
`and ecx,0x400000; or ecx,0x800000; shr ecx,0x16`) makes it `2 + TEXTURE32`, where `TEXTURE32` is
`GraphicalOptions.TEXTURE32` (bit `0x400000` of the flag word, `boot.md`), and passes it as argument 4 of `0x0044df20`.
That reaches the engine init `0x00572780` (`0x00572966`), `FUN_0058cff0` (the "tcache ok" step) and `TexCache_Init` (`0x0058da40`), the
texture cache's init, which logs "Texture Cache Initialization" and "Surfaceformat chosen %d", stores the index
(`0x0058db5c`; forced 3 at `0x0058daca` when the renderer mode `[0x008bd524]` is 4) and calls
`TexCache_CreateStagingPool` (`0x0058b220`, below). `FUN_0058db90`'s one caller (`0x00577520`, in `FUN_005774c0`) passes -1: the index
stays, the cache is freed if `0x20000000` is set, and only the slots are remade (`FUN_0058d670`); the staging pool is not
remade on that path (with another index it would be, and only in renderer mode 1).
The table at `0x007015f8` holds four shorts a format, alpha, red, green, blue bits:

| Index | Format |
|---|---|
| 0 | R5G6B5 (0, 5, 6, 5) |
| 1 | A1R5G5B5 (1, 5, 5, 5) |
| 2 | A4R4G4B4 (4, 4, 4, 4): `TEXTURE32` 0 |
| 3 | A8R8G8B8 (8, 8, 8, 8): `TEXTURE32` 1 |

The bytes a texel are `2, 2, 2, 4` at `0x007014c8` and again at `0x00701fe0`. `0x004925d0` copies the 32-byte record
`0x008bd558 + 32 x [0x008bd554]`, the chosen format's own. `data/low.sam`, `med.sam` and `high.sam` all hold
`GraphicalOptions.TEXTURE32 0` (line 50), so a shipped game runs format 2 and its textures keep four bits a channel
(Q492; the path tiles, `park-engine.md`, "A path laid over"). The display depth is separate: 16 or 32 by `RENDER32`
(`boot.md`, `0x0044e20b`), and the shipped presets hold `RENDER32 0` too.

**A record is the device's own pixel format** (Q492). The Direct3D init `FUN_00573080` asks the device for its texture
formats (`IDirect3DDevice3::EnumTextureFormats`, vtable `+0x20`, callback `0x00573060`). `TexFormat_EnumCallback`
(`0x00572f70`) counts the set bits of each offered format's alpha, red, green and blue masks (`Bits_Count`, `0x005911d0`)
and copies its 32-byte `DDPIXELFORMAT` into the record whose four counts the table above holds, logging "Surfaceformat
%d found (argb) %d%d%d%d" (`0x007688c4`) under the flag word's `0x1000000`. A record left with `dwSize` nought is a format the device lacks: `TexFormat_GetRecord`
(`0x00573820`) refuses it (error `0xb0754003`), and with record 2 empty the init releases what it made and fails
(`0x005733fe`; "No suitable alpha surfaces" is logged only under the flag word's `0x2000000`). So the 4-4-4-4 format is
required; the loaders follow its masks' order on the device, the sign board assumes A-R-G-B.

**One table a format turns 8 bits into the surface's.** `TexFormat_BuildChannelTables` (`0x005839f0`), from the engine
init (`0x005727b0`, after the device's formats are taken at `0x0044df6b`), fills for each record whose `dwSize` is 32 a
table at `0x008bd5d8 + 0x1000 n`: for each value `v` from 0 to 255 four dwords, alpha, red, green, blue, each
`(v >> (8 - bits)) << shift`, `bits` that mask's count and `shift` its lowest set bit (`Bits_LowestSet`, `0x005911f0`).
**A channel is cut to its top bits, never rounded**: at format 2 a channel keeps `v >> 4`, so 0 to 15 store nought and
240 to 255 store 15. (Format 3's table starts as the static constructor's, `0x00583900`: `v << 24`, `<< 16`, `<< 8`, `v`; it is
rebuilt from the device's masks when the device offers 8-8-8-8, and stands when it does not, or in renderer mode 4,
where `0x005839a0` fills record 3 by hand after the tables are built, `0x005727c3`.) The texture loaders' writers go
through it:

- The zlib codec `FUN_00593ab0`, the LZSS one `FUN_00595920` and `FUN_00597100` (not identified) use the 512-word
  tables `Codec_BuildChannelTables` (`0x005916c0`) copies from it, alpha `0x00cef228`, blue `0x00cef628`, green
  `0x00cefe28`, red `0x00cf0228`, indexed by the channel's rounded value plus 128 (held to 0..255 first). A texel is the
  OR of one word a channel; with an alpha chunk the alpha's word (`0x005954c8`), without one the format's whole alpha
  mask, the record's `+0x1c` (`0x0059568c`, `0x00596e31`, `0x00598332`).
- `FUN_00575160`, the other formats' converter (from `FUN_00574bd0`), the same: a 4-byte source's alpha through the
  alpha column, a 3-byte source's the alpha of 255 (`+0xff0`); its texel stride is `[0x007016f8 + 4 n]`, 2 at format 2.
- `SpritePack_Load` at format 2 (`0x00587f0e`) turns its 256-colour palette into 16-bit entries through the table.

The sign board's mode 2 (`FUN_005ecb40`, `0x005ed011`) packs its own nibbles, `(b0 & 0xf0) << 8 | (b1 & 0xf0) << 4 |
(b2 & 0xf0) | b3 >> 4`: also a cut, but in a fixed A4R4G4B4 order whatever record 2's masks; the terrain's last pass
blends in place on the locked surface (`park-engine.md`, "The procedural ground").

**No texture picks its own format.** The texture cache makes every slot through the device's factory (`[0x008bd52c]`
`+0x10`, then its slot 0 with the size, the index `[0x008bd554]` and the mip count: `TexCache_CreateSlots`,
`0x0058d1d0`), and `TexCache_CreateStagingPool` its staging surfaces, two a size from `[0x007022e8]` 256 down by halves,
system-memory textures (`DDSCAPS_TEXTURE | DDSCAPS_SYSTEMMEMORY`, `0x1800`) through `TexCache_CreateStagingPair`
(`0x0058b0b0`), in the same one format. A texture's alpha class (opaque, cut out, graded: "The loader" above,
`render-states.md`) chooses its render state, not its surface: in the shipped game all three are A4R4G4B4, a cut-out or
graded texture keeping its alpha's top four bits and an opaque one 15.

**Four levels a texture, every one in the format** (Q700). `TexCache_SetDefaults` (`0x0058d0f0`) starts with no mip
levels; `TexCache_Init` sets four (`[+0x18e8]`, `0x0058dab0`) and the cache's bit 2 when `[0x008bd550]` has bit 2, which
`DetailFile_Apply` copies from `GraphicalOptions.MIPMAP` (`[0x007858a4]`, key 12 of the table at `0x00744300`,
`0x0044db1e`; 1 in all three shipped files). `TexCache_MeasureSpace` (`0x0058d400`) keeps them only where the renderer
`[0x008bd524]` is 1 (live 1) and the space measured is over 64 of its units, else logs "NOTICE not enough space for
mipmaps, creating surfaces without" (when bit 2 was set and the flag word `[0x008bd508]` holds `0x1000000`, `0x0058d5be`..`0x0058d5cc`) and clears both (`0x0058d5ed`..`0x0058d603`, unconditional); `TexCache_CreateAllSlots` (`0x0058d670`) hands the count to every
slot (`TexCache_CreateSlots`'s last argument, a slot over one level sized four thirds). How a level below the first is
filled is not read.

The sea's three `gby_sur` (`q492/census-gby_sur.txt`, decoded by this game's reader, as `wctorig.py` does not read
LZSS) average red 13.5 to 15.2 with 60 to 63% of texels under 16; the cut leaves 7.4 to 9.8 (a nibble taken back as
`n x 17`). How the device widens a nibble is not read from Proton's code; the unsigned-normalised rule (`n / 15`, so
`n x 17` of 255) is measured to hold well enough: at camera B the original's sea period reads (1.3, 55.1, 69.4), ours
cut (3.1, 55.0, 69.9) and uncut (5.9, 57.9, 71.3), and the brick (116.1, 54.3, 53.4) against ours cut (115.5, 54.0, 53.8)
and uncut (111.9, 60.8, 60.9) (`q700/PREDICTION.txt`, P9-P11). The red left over the sea's is most likely the 16-bit
display (`RENDER32` 0), whose 5-6-5 OpenTPW does not draw; not read. Not read either: `FUN_0057b6e0`'s format-2
branch (`0x0057b716`).

**OpenTPW keeps eight bits a channel by default, a deviation from the original** (Q616, Alexah 2026-10-21: the four-bit
cut beside the crisp interface was unsettling; `Texture.KeepsEightBitsUnder`). At the original's depth, `textures
original` in `save/opentpw.cfg` (`GameOptions.OriginalTextureDepth`), it follows the original's rule: it cuts every
texture of the game's art the same (Q700, `Texture.CutChannel`, `v >> 4` widened to `n x 17`, alpha too), unless the
detail file the game starts with says `TEXTURE32 1` (`Texture.KeepsEightBits`, the original's format 3). The log says
which once at boot ("Textures: ..."). At camera B, ours at its default against the original: brick (111.9, 60.8, 60.9)
against (116.1, 54.2, 53.4), the sea's period mean (5.5, 57.7, 71.2) against (1.9, 54.4, 68.5); at `textures original`,
(115.5, 54.0, 53.8) and (3.0, 54.5, 69.2), the pixels main drew while the cut was its default (mad 0.00 on grass, brick and road; the sea's mean within 1.2 of main's,
its words drawn afresh each run); the path
below the park gate (147.9, 107.8, 60.6) by default, (137.4, 106.2, 46.2) cut, the original's (139.4, 107.2, 44.4), a
path's alpha cut too (`q616/PREDICTION.txt`). Where cut, each level is made on the processor, the rounded mean of the
eight-bit level above's two by two, cut, down to one texel (the original's four levels and its filler not followed). Kept at eight (`TextureFlags.OwnPixels`): the
loading bar's reds, the object window's green and red lines and the sky's tint, which are OpenTPW's own, and the loading
screen, the pointer and the letters, whose way to the screen in the original is not read (Q517). The footprint picture's
three colours are the original's (`FUN_004ab1b0`, park-engine.md) and, at the original's depth, are cut with the rest: measured (8,89,140),
(0,117,25), (115,53,0) over black on a 16-bit frame fit the cut alone (the blue's red 0x1e is 8, the green's red 0x0f 0,
the brown's blue 0x0f 0), not the eight-bit colour. The procedural ground's sheet goes through the same cut, so a path's
texels, which its compositor reads from the locked surface as `n << 4` (Q489), reach the screen as `n x 17`; at eight
bits the compositor reads a path's texels whole (`ProceduralGround.PathTexture`) and the sheet is not cut.

**The sky's colour ramp is the exception to `n x 17`** (Q700). `sky_rgb.tga` is loaded as the other art is, A4R4G4B4, and
the ramp is read back from that surface at format 2 (`0x00585d67`..`0x00585da2`) as each colour channel `n << 4 | 8`
and alpha 255; at format 3 the texel is copied (`0x00585dc5`). OpenTPW writes `(v & 0xf0) | 8` and 255
(`Sky.WidenRamp`) where the art is cut, and copies the texel at eight bits.

**What the cut costs** (Q700), now under `textures original` only (Q616). The park loads about 0.76 s slower than with eight bits kept (totals 3.4 s against 4.2 s over
four runs: guest sprites 273 -> 729 ms, ground 560 -> 691, objects 717 -> 785, fixed items 172 -> 206; `q700/*/run.log`);
the guest atlas is also packed and cut again on every switch into and out of first person (about 0.45 s by the load
figure, not measured).

## The zlib codec, `FUN_00593ab0`

Built (Q355: `TextureFile.DecodeZlibCodec`). The multiplier of each value is the table's; nothing else scales it.

- **Values.** The decompressed stream is read straight through, Y's `n x n` values, then Cb's and Cr's `n/2 x n/2`
  (`n` the larger side rounded up to a power of two, at least 8): a signed byte, or `0x80` and a little-endian
  int16 (`[0x00702688]` 256 times the high byte plus the low one). Each times the channel's multiplier.
- **The transform**, per row of `n` values and then per column, into a plane of its own: plain D4, the coefficients
  `(1 + sqrt 3) / (4 sqrt 2)`, `(3 + sqrt 3) / ...`, `(3 - sqrt 3) / ...`, `(1 - sqrt 3) / ...` built at run time
  (`0x00591600`..`0x00591690` into `0x00c27208`..`0x00c27214`; the doubles 3, -1, 0.25, 2, -3, 1 at `0x00702610`).
  With `s` the row's first half and `d` its second, `k` from 0 to `n/2 - 1` and `p = k - 1` wrapping to the last:
  `out[2k] = s[p] h2 + s[k] h0 + d[p] h1 + d[k] h3`, `out[2k+1] = s[p] h3 + s[k] h1 - d[p] h0 - d[k] h2`.
- **Colour.** BT.601 with each chroma value taken for a 2 x 2 block (no blend): B = Y + 1.772 Cb (`[0x00702694]`),
  G = Y - 0.344136 Cb - 0.714136 Cr (`[0x007026b4]`, `[0x007026a0]`), R = Y + 1.402 Cr (`[0x00702698]`), each sum
  rounded to nearest (`+ 12582912.0`, `[0x007026ac]`, and the float's low bits) and held to 0 to 255 (`packssdw`,
  `packsswb` round a bias of `0x80`); stored B, G, R, A. With no alpha chunk the alpha is `12583167.0`, 255.
- **The decompressor** (vtable `0x00703128` slot 2, `0x00619380`) refuses a chunk that does not start `BILZ`
  (`0x006193d0`), so the codec then decodes whatever its buffer held. Three type `0x12` files are LZSS (FileFormats,
  `texture.md`); OpenTPW sends them to its LZSS decompressor, which throws on all three, as on main.

**OpenTPW** matches it: of the 986 type `0x12` files with a zlib chunk, 983 decode within 1 a channel of `wctorig.py`
(`docs/TOOLING.md`, the codec in Python; 827 to the byte), and the other three differ in the alpha alone, the loader's
rule above (`q355/compare.py`). `ridestatbar.wct`'s texels 1, 4, 8 and 24 are (250, 0, 0), (242, 97, 0), (248, 208,
0) and (93, 241, 1), the original's screen at each ((255, 14, 0), (255, 103, 0), (255, 206, 0), (90, 251, 0)), and
its track 56 where the original's is 49 on screen (`hud.md`, "The stat bars' skin"). Its LZSS path (type `0x13`) is
unchanged: all 7,406 that decode are the same to the byte as before. Before Q355 type `0x12` went down a
"half-scale" path whose rows were read by another index map with the multiplier times pi/6: right in brightness,
wrong at a row's ends and blurred inside: every zlib texture changes (mean 11.7 a byte), and the gadget's buttons, smeared
and doubled before, are crisp as in the original's frame (`q355/gadget-orig-fix-main.png`).

## A sign's artwork (`FUN_005e7990`)

Type 4, the only one shipped, reads six dwords after width, height and type: four floats, then the two chunk sizes. The
colour chunk goes to the codec's slot 3 (`0x005987b0` -> `0x00592000`, its colour flag set -> `FUN_00592730( w, h,
data, out, v0, v1, v2 )`, `0x005e7cb9`) and the alpha chunk to the same slot with the flag clear and `v3`
(`0x005e7ced`). **The floats are the multipliers as they are**, no formula: `FUN_00592730` reads the Y plane times
`v0` into `+0x2c`, the second plane times `v1` into `+0x1c` and the third times `v2` into `+0xc`; `FUN_005935a0` takes
`+0x1c` times 1.772 into blue and `+0xc` times 1.402 into red. So Y times 6, Cb 10, Cr 2 and the alpha 6, in all 23
shipped boards. Its rows and columns go through `FUN_00592b80`, `FUN_00592c50` and `FUN_005933a0`, not compared with
`FUN_00593ab0`'s instruction by instruction; the decode below agrees with the screen.

**The board holds the artwork with red and blue exchanged.** `FUN_005935a0` writes each texel blue, green, red;
`FUN_005e7990` puts the alpha before them (`0x005e7d03`: A, B, G, R); the sign's board (`FUN_005ecb40`, its mode 2)
packs a texel as `byte0 << 12 | byte1 << 8 | byte2 << 4 | byte3`, A4R4G4B4, so the codec's blue is drawn as red and
its red as blue. The inks go through the same packer with their file byte 0 in byte 1, which is why a line's ink is
read red, green, blue, opacity. The board's mode 1 copies the bytes as they are; which mode a board takes is not read.
Witnesses: the Windows screenshot `content/ReferenceScreenshots/ParkPicker/ParkPickerReference.jpg` shows Wonder Land's
board pale cream, the exchanged decode (the codec's own order is pale cyan); the original under Proton draws Lost
Kingdom's park gate blue-grey (the codec's order is brown). Under Proton the original letters no name on that board
(the Windows screenshot's board has its name; Wine's GDI fonts are not looked into).

**OpenTPW** (Q357): `SignFile` hands the four floats on as the multipliers, and `SignTexture.CropBoard` exchanges red
and blue as it cuts the board. All 23 boards decode within 1 a channel of `wctorig.py` (`q357/allsgn/sweep.py`), and
all 8,392 `.wct` files that decode are the same to the byte as before. Each board's mean colour is logged as it is
painted: Lost Kingdom's gate and island (64.3, 67.5, 82.6), Halloween (41.7, 34.7, 38.8), Wonder Land (232.1, 240.1,
178.3), Space (51.1, 27.7, 98.0), where the header's formulas and the codec's order gave the gate (56.0, 44.1, 45.2).
Through the alpha's 6 where the formula gave 7, four boards (the three `sign1` boards and the Jelly's) lie a shade
under opaque throughout (232 to 254); the eleven other solid ones are 253 or 254 on 2.3% of texels where they read
down to 77. The gate seen from inside the park (`camera 480 140 80 180`) measures (24.4, 30.5, 42.4) on its board's
plain stone here, (22.3, 23.8, 29.2) on main, and (25.8, 27.1, 39.0) in the original (`q357/side-by-side.png`,
`board-close.png`). This game's slab top beside it is 1.27 to 1.34 times the original's (not looked into: the light or
the model's texture).

## Predicted first (`q354/PREDICTION.txt`)

| | Predicted | Measured |
|---|---|---|
| `ridestatbar.wct`, texels 4, 8, 24 of row 4 (`wctpng.py`) | red-orange to green | (248, 109, 0), (255, 217, 2), (91, 254, 0); were (184, 182, 0), (229, 255, 0), (140, 255, 0) |
| Texel 1 | red | **missed**: (125, 84, 51), the half-scale path (above) |
| The training screen's 80% bar at 0.1 .. 0.7 of its width against the original's frame (`q353/orig/a-train.png`) | within 40 a channel | within 18 at all seven (`run-fix1`, `run-fix2` the final build) |
| Its first 3% | this game's R under 200, the original's over | (82, 40, 8) and (221, 42, 0) |
| The log | `UI: the stat bars' skin ... its ramp (125,84,51) (248,109,0) (255,217,2) (91,254,0)` | as predicted |

## Predicted first (`q355/PREDICTION.txt`)

| | Predicted | Measured |
|---|---|---|
| The 986 type `0x12` zlib files against `wctorig.py` | within 1 a channel | 983; 3 off in the alpha (**missed**: the loader's rule) |
| The 7,407 type `0x13` against main | the same bytes | 7,406; `b_NEWwin.wct` throws in both |
| The 3 type `0x12` LZSS files | decode | **missed**: they throw in both builds |
| The lobby's signs | under 5% darker or brighter | -2.5 to -3.9% |
| The training screen's log line | `its ramp (250,0,0) (242,97,0) (248,208,0) (93,241,1)` | as predicted (`run-fix2`, the final build) |
| Its 80% bar's left end | R over 200, within 45 of the original's (221, 42, 0) | (254, 4, 4); main's (82, 40, 8) (`run-control`) |
| Each bar's mean r - g | nearer the original's than Q354's | 16.3, 183.2, 60.3, 110.2 (original 9.4, 179.4, 56.4, 104.9) |

## Predicted first (`q357/PREDICTION.txt`)

| | Predicted | Measured |
|---|---|---|
| The five boards' log lines (round 2) | `wctorig.py`'s means, red and blue exchanged | as predicted, each exact (`run-fix2`, the final build) |
| The gate's board, round 1 (the floats alone) | (32.9, 36.4, 41.5), the whole board's ratio | **missed**: (29.8, 30.5, 35.7): a patch moves by its own texels, and the screen adds a constant |
| Against the original, round 1 | R and G within 10%, B 10 to 25% under | **missed**: B 30% under: the board's exchange, not foreseen |
| The gate's board, round 2 | (24.7, 30.5, 41.8) within 8% | (24.4, 30.5, 42.4) |
| Against the original, round 2 | each channel within 15%, B / R over 1.4 | (0.946, 1.125, 1.087), B / R 1.74 (the original's 1.51) |
| Main's build | no board line, (22.3, 23.8, 29.2) within 5% | as predicted (`run-control`) |

## Predicted first (`q358/PREDICTION.txt`)

| | Predicted | Measured |
|---|---|---|
| `wct2raw` over the 8,396 shipped `.wct` | 236 change, in alpha alone; `b_NEWwin.wct` decodes | as predicted; 8,393 decode where main's 8,392 |
| The log's lines in the stock park | `jri_bnk4` 962 clear and 1,230 under 240, `jri_bnk5` 1,888 and 2,145, `jri_bnk6` 1,033 and 1,258, `ipan` 5,273 and 6,008, `Sen_ant1` 3,199 and 3,682; none from main | as predicted, and eight more the census names (`run-fix-7`, the final build) |
| The river's left bend, `camera 480 465 110 180`, share under 45 a channel | under 0.012; main's over 0.025 | 0.0058; main's 0.0363 |
| The Staff Training Budgets screen against main's | under 60 pixels differ | 28, at its bottom edge |
| The near bank's seam, the original's frame of the same file (`seam.py`) | round 1: the bend; rounds 2 to 5: a level, a share, a run under 15 | **missed four times**: its view frames the long reach; it is lit darker; its rocks' outlines touch the water; its run was 15 |
| The same, round 6: longest run of seam columns | the original under 25 (its four longest under 60); the fix under 10, main over 25 | 15 (6 + 8 + 9 + 15 = 38); 4; 51; on the final build (`run-fix-7`, `orig/g`) 15 (38) and 4 |

## Predicted first (`q359/PREDICTION.txt`)

| | Predicted | Measured |
|---|---|---|
| The log's disagreeing materials in the stock park, against `predict.py` | round 1: every line; round 2: 19 of 19 | missed 3 of 19 (the predictor's list lacked two terrain meshes and the global advisor); then 19 of 19, three times |
| The texture by itself, the floors (`cmp-bellyclose.png`) | the park's ground shows through the borders | **missed**: the sky shows; the original's frame shows green (P6, held) |
| Two pokes of the original's cell words (`orig/b`, `orig/c`, `orig/d`) | a hole or a path tile | void twice: the poked cells lay off the frame (Q360) |
| The narrowed rule: `MATERIAL_ALPHA_WITHOUT_0X2` | 54 | 54 (my check's regex missed it once) |
| Fix against main, two close views, guests boxed out | no floor border; under 1,500 pixels | 681 (the sign), 629 (the roof, the doors) |
| The Staff Room's eaves in the original (`cmp-roof.png`) | a smooth fringe, as the fix's, not main's sawtooth | as predicted, at a quarter of this game's resolution |
| The same on the final build (`run-fix11`, the original on its own file, `orig/g`) | 19 of 19, 54, the floor band under 100, the eaves over 200 and a fringe | 19, 54, 0, 217 on the eave line, a fringe |

## Predicted first (`q360/PREDICTION.txt`)

| | Predicted | Measured |
|---|---|---|
| The terrain pass's heightfield against the base model's; the render word | the same; `0x10000` clear | both `0x10db4e50`; `0x01444100` |
| `0x1` written on four grass cells, frame before and after (`orig/a`, then `orig/c`) | a hole, not green | **missed** in `a`: my aim, the cells off the frame's right edge; held in `c`: black, (0, 0, 0) |
| Why `a` showed nothing, guessed before reading | the pass not run; then no cell visited | **missed** twice: it runs each frame, rows 13..32 visited |
| A path cell's texture word copied onto grass | a path tile | **missed**: my control, that word is the ground under the path |
| The Belly Bounce's box with no ground under its floor (`a-2`) and with it (`a-3`) | no pixel under 8 | 37 in each, the trench's and the queue's, none on the border: the border green both times |

## Predicted first (`q361/PREDICTION.txt`)

| | Predicted | Measured |
|---|---|---|
| `m_grass1` in the original's texture cache | once, `sharetex`, `0x11000280` | once, `sharetex`, `0x11070281`: `0x40000`, `0x30000` and `0x1` not foreseen |
| The floor textures' `0x300` held opaque, cut, additive (`orig/belly-4.png`) | cut darkens the fringe; additive (the control) changes the plate | **missed**: nothing changed, the control too: the floor reads no texture flags while the park draws |
| Four page records outside the cache held additive (`g-4.png`) | the floors change | **missed**: they are not the floors |
| The Belly Bounce's cells given a grass cell's texture word (`h-3.png`) | the plate the same | as predicted |
| A read watch on the floor's first material object | no read in 15 s | none; nor of its material record, which holds `0x0801` |
| Every loaded material over a floor texture | over nine in ten with `0x800` | 174 of 174 |
| The floor's nine materials held without `0x800` (`k-2.png`) | the plate the same (built at the load) | as predicted |

## Predicted first (`q362/PREDICTION.txt`)

| Prediction | Predicted | Read |
|---|---|---|
| `dk_water3`'s material word in the original | no `0x800` | `0x8031` |
| The queue pieces' `jpa_que1`/`2`, `m_grass1` | `0x800` | all |
| The floor picks against the original's | the stock models' 8 | 54 of 54 agree |
| `jpa_que5`/`6` | no `0x800` | none |
| The stock park's opaque materials, `MATERIAL_ALPHA_WITHOUT_0X2` | 44 over 11 models; 0 (main 54) | as predicted |
| The raised pool seen, over a third of its box changing | changes | missed: it lies under the ground (89 pixels, the waterfall's) |
| The waterfall under the bridge | over 500 pixels, bluer in main | 5,017; 92 to 38 |
| The low pool | over 1,000 pixels | missed: 501, at its east end alone |
| The original under the bridge | nearer the fix | 18 against 31 (main 77) |
| The mutations | — | 20 put back, 17 fail a test; 3 cannot differ on Lost Kingdom's data |
