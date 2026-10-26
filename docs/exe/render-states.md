# Render states

How Theme Park World sets Direct3D render states. It never sets them per material: the engine compiles
a single 32-bit **state word** per draw and applies it lazily against a cached copy, so every visual
difference between opaque, cut-out, graded and additive geometry is one of four constants. Three facts
matter more than the rest. **CULLMODE is set to `D3DCULL_NONE` once at startup and never rewritten** —
because the engine culls every model face itself, by its screen winding ("The software back-face test"); a face
seen from both sides is authored twice. **See-through geometry still writes depth** — none of the four
material words disables depth writes. And the choice between a cut-out (hard-edged) and a graded
(soft) alpha reference follows, for a `.wct`, **its alpha chunk and its header byte 1**, and for any other
format a **pixel classifier**, subject to material-load options and the material `0x800` opaque override
described below.

## Device and how a state reaches it

DirectDraw + Direct3D 6 immediate mode. The binary imports `DDRAW.DLL` only; the device comes back
from `QueryInterface`, so there is no `d3dim` import to find. The device vtable is `IDirect3DDevice3`. The executable also carries a software
renderer ("MMX Software Renderer, by Martin Griffiths, 1998/9", `0x006ef770`) and two refusals of the hardware path,
" -- Detected Voodoo1, failing HAL init." (`0x007689d0`) and " -- Detected PowerVR1, failing HAL init." (`0x0076637c`);
which code reads them is not traced (Q194, from the bytes). DirectDraw's two callers are `boot.md`'s step 3.3.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `+0x58` | `SetRenderState` | Device vtable slot used for every state change | `IDirect3DDevice3` vtable, `call [reg+0x58]` sites |
| `+0x74` | `DrawIndexedPrimitive` | Device vtable slot | `IDirect3DDevice3` vtable |
| `+0x98` | `SetTexture` | Device vtable slot | `IDirect3DDevice3` vtable |
| `+0xa0` | `SetTextureStageState` | Device vtable slot | `IDirect3DDevice3` vtable |
| `FUN_00567620` | — | Applies a compiled state word, lazily, against a cached copy of the current states | Renderer vtable slot 2 |
| `0x00701330` | — | The renderer vtable holding `FUN_00567620` at slot 2 | Disassembly |

## The compiled 32-bit state word

| Bits | What it is | Evidence |
|---|---|---|
| `0x0f..0x00` | `DESTBLEND = word & 0xf`, `SRCBLEND = (word >> 4) & 0xf` | `FUN_00567620` |
| `0x100` | `ALPHABLENDENABLE` | `FUN_00567620` |
| `0x200` | Selects the **high** ALPHAREF of the pair | `FUN_00567620` |
| `0x400` | `ALPHATESTENABLE` | `FUN_00567620` |
| `0x800` | `ZWRITEENABLE = NOT this bit` — the only depth-write control in the engine | `FUN_00567620` |
| `0x8000` | `ZENABLE = NOT this bit` | `FUN_00567620` |
| `0x10000` | Stage 0's `COLOROP` `MODULATE2X` (5), `MODULATE` (4) without it (`FUN_00567a70` from `0x005678ba`). No model draw's word carries it: `FUN_00581790` and `Texture_BlendStateWord` never give it, and a breakpoint on its flip met none in 25 s of the advisor's line (Q657; `ui.md`, "How he is lit") | `FUN_00567620` |
| `0x1000` / `0x2000` | Not render states: together the `0x3000` "exempt from sorting" mask | `FUN_00565590` tests the mask |
| `0x80000` | `SPECULARENABLE` | `FUN_00567620` |
| `0x100000` | `FOGENABLE` | `FUN_00567620`. Cleared on a batch the game fogs itself; the park's own view asks for no fog at all (`park-engine.md`, "The park view's colours") |
| `0x800000` | Overrides the per-batch depth key `+0x30` after the depth sum is added: 0.0 for a record whose kind nibble (`[+0] & 0xf`) is 2, else the added vertex count x `[0x008bcc14]` + 0.01; set from the texture slot's flag `0x2` (`0x005817e4`) | `0x0058109e` (`DrawList_AddMaterialFaces`; fork watch 2026-10-26, `verify-all.md` M1) |

## States set once for the whole run

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0056695c` | `CULLMODE = 1 = D3DCULL_NONE` | **Set once at startup and never rewritten anywhere in the program.** The card culls nothing because the engine has already: every model face passes `Face_KeptByWinding` first ("The software back-face test"). | All 88 `call [reg+0x58]` sites scanned; no other site writes CULLMODE |
| `0x005669b7` | `ALPHAFUNC = 7 = D3DCMP_GREATEREQUAL` | Alpha test comparison, never changed | Disassembly |
| `0x00573614` | `ZFUNC = 4 = D3DCMP_LESSEQUAL` | Depth comparison, never changed: of two faces at one depth the later drawn shows. OpenTPW's is the same (`Material.DepthComparisonFor`, Q586) | Disassembly (`SetRenderState( 0x17, 4 )`) |
| `0x00573660` | — | The `EnumZBufferFormats` callback (`FUN_00573080`, `0x005731b3`): keeps the format offered and stops at the first of 16 bits, so the z-buffer is 16-bit wherever the card offers one. At a park's depth 110 a step is about 0.43 units (`park-engine.md`, "What hides a person below their feet") | Disassembly |
| `DAT_007012d8` | ALPHAREF pair table | `{0x1000, 0xf000, 0x10, 0xf0}` | Data at that address |
| `FUN_00567270` | — | Probes the card and selects index 2 of that table, so the two references in use are **16 and 240 of 255** | Disassembly |

## The software back-face test

Q586. Every model face is culled by the engine before it reaches a batch. `DrawList_Flush` (`0x00581db0`) hands each
mesh entry's materials to `DrawList_AddMaterialFaces` (`0x00580530`), which copies a material's transformed vertices
and, on both of its paths (no clip flags, `0x00581000`; clipped, `0x00580eab`), keeps a face only when
`Face_KeptByWinding` (`0x00581320`) answers 1 for its corners in the file's own order (a, b, c: the face record's words at
`+2`, `+4`, `+6`). **The test reads clip space, before the divide.** It takes words `[0]`, `[4]` and `[0xc]` of each corner
(x, y, w; `0x00581324..0x00581392`): at the test they are the clip coordinates, `0x00561bc0` having compared `|x|`, `|y|`,
`|z|` against `|w|` for the outcodes, and the divide to screen (x, y in pixels, z, rhw at `[3]`, FVF `0x1c4`, the batch's
final format) comes after both loops, as the transformer's slot `+0xc` on `[0x0087b1d8]` (`0x00581092`). That is one of three
transformers chosen by CPU at `0x005781d0..0x00578379`: vtable `0x00701920` (SSE, CPUID bit 25) slot `+0xc` is `0x005f525f`
(`rcpps` and a Newton step), which runs on a present-day CPU; `0x007018f8` (`[0x008bd53c]`) is `0x005f4a46`; and `0x007018d0`
is `ClipToScreen_Fpu` (`0x00579690`, `fld1 / fdiv [+0xc]`). The test answers 1 when
`det[[x0, y0, w0], [x1, y1, w1], [x2, y2, w2]] > 0` of the clip coordinates, which is `w0 w1 w2` times the determinant of
the projected corners: **the projected winding for every face in front of the eye**, clockwise on a y-down screen, and the
true facing for one across the near plane, as the card's test of the clipped polygon also gives. Every model mesh goes through it
(or the inline copy below); which other draws do, "Which draws the test covers".

**So a face seen from both sides is authored twice**: the same three points, wound opposite ways, each with its own
vertex-order entries and normals, so each side is lit its own way. Of the game's `.md2` files under `data/` (2,123
read), 1,419 meshes carry 27,937 such groups, 27,930 of them turned opposite ways (`lib/twinfaces.py --sweep`). Drawn
both, the first of a pair at a depth wins under a less test and the later under less-or-equal, neither of which is the
original's: the Sun God's brazier plates (`incagod.MD2`, `Body`, `Inc_Saucer`, faces 124 to 139) put their undersides
first and drew dark under a less test; the gate pillars' eyes (`gates.MD2`, `gateway`, `md_pill01`, faces 0 to 15) put
their lit side first and drew dim under less-or-equal. Culled, both show their lit side, as the original does
(`q586/orig/G/o-t1008.png`, `q580fs/orig/S2/o-t764.png`).

**OpenTPW** culls a model's faces (`LobbyModel.DrawFlags`) on the card: back faces, clockwise kept
(`Material.CullModeFor`, `Material.KeptWinding`), which in this engine's axes keeps the original's side (Q586's rounds:
nothing drawn inside out, `q586/runA3`). The card tests the projected corners, which is the original's test (above): no
deviation. **Not every draw calls `Face_KeptByWinding`**: which do is the next section.

### Which draws the test covers

Q608. `DrawList_AddMesh` (`0x00581cd0`) has one caller, `ModelRenderable_DrawNodes` (`0x0057a6d6`), and
`DrawList_AddMaterialFaces` one, `DrawList_Flush`; every other draw writes its batch itself. Read for each draw
`Render_DrawView` (`0x00576a00`) makes:

| Draw | The original | OpenTPW |
|---|---|---|
| Every model mesh: the park's and the lobby's models, the advisor (`ui.md`), the object window's preview | Culled, `Face_KeptByWinding` | Culled (`LobbyModel.DrawFlags`) |
| The three dynamic-faces meshes: the build markers (`BlueprintMesh`), the sea (`WaterMesh`), the gates' flags (`FlagsMesh`), `park-engine.md` | Models: `FUN_0053d790` builds each and `0x00425440` shows it with `Model_ShowInRenderList`. Culled | Markers (`ParkBuildMarkers`) and sea (`ParkSea`) both-sided; flags (`ParkFlags`) culled |
| The ground, and the paths on it (a path is a ground cell's texture, `park-engine.md`, "A path laid over") | The terrain pass `FUN_0056f670` carries its own copy of the test over the same words `0x008c63b0..0x008c63d0`: a cell's two triangles at `0x00570084` and `0x0057020b` on the clipped path (each kept one goes to the clip, `0x00562a70`), and at `0x005708de` and `0x005709e4` on the path for a cell wholly inside the screen (the four corners' outcodes OR to nought, `0x0056faae`), which writes four corners, three or none itself. `FUN_00570d90`, the second layer (off in the park, `textures.md`), carries the same test (`0x0057178e`, `0x0057190b`, `0x00571d76`, `0x00571e72`). Culled | `ParkGround` and `ParkPaths` both-sided |
| The UI's meshes (below) | Culled | `UiMesh` both-sided |
| People, their shadows, particles, weather, everything through `SpriteBatch_AddQuad` (`0x00565f90`), `0x00566220` and `Batch_AddPolygon` (`0x00565bb0`) | No test: `Batch_AddPolygon` makes outcodes (`0x00561bc0`), clips (`0x00562a70`) and writes a fan; its flag `2` only reverses the index order. Two-sided | People (`ParkGuestSprites`) and particles (`WorldParticles`, `ScreenParticles`) both-sided. Weather (`WeatherSprites`) culled, its quads turned to the camera, so it drops none |
| The sky (`FUN_005863c0`, `0x00576c78`, through `0x00565960`: outcodes and `0x00562c90`'s clip, no test) | Two-sided | Two-sided (`Sky`) |

`0x00581a00` also carries an inline copy (`0x00581b5b..0x00581c3e`, a plain 4x4 transform with no divide), reached from the
node walk `0x0057b610` by `FUN_0057b8a0`, which `ModelRenderable_Draw` calls at `0x0057a8eb` (a model pass into the
renderable's `+0x13c` texture, most likely the model shadow; not named): it culls too.

**The ground, paths, sea and markers lose nothing seen from above.** Each is a sheet facing up, and a ray from an eye
above such a sheet meets its upper side first, so the test drops only faces a nearer one covers. Not photographed.

**The UI's meshes** are renderables: `UiControl_PlaceMesh` (`0x00476f10`) hands `Renderable_SetMatrix` (mode 1) the fit
`UiControl_FitMatrix` (`0x00476c50`) makes, an affine matrix whose x and y scales are both positive (the part's bounds
onto the control, then 2 / 2048 and 2 / 1536). `Renderable_SetMatrix` (`0x0057a260`) multiplies every matrix it is handed
by diag(1, -1, 1, 1), so at the test the file's y is turned over: **a UI face is kept when its corners turn clockwise in
the file's (x, y), y up**. `lib/uiwinding.py` counts them: of `ui.wad`'s 278 models, 1,185 meshes and 6,310 faces, 422 turn
the other way, in 13 meshes of three models: `s_X1.md2`'s ten (24 to 50 faces a mesh), `s_key.MD2`'s two (14 each) and
`warnlite.MD2`'s root mesh `warnlite` (both its faces; which control shows that part was not traced). `ui.wad`'s one twinned
mesh, `worldlob`, pairs faces turned the same way, so no pair is chosen between there. The key and the "x N" are closed
solids, so the kept faces cover the whole outline; ours draws every face in file order with no depth test, and a
turned-away face is painted last over 15.0% of the key and 11.5% of the "x 1" (`q608/coverage.py`). On Lost Kingdom's
island panel ours doubles the stem of the "1" with its back wall's edge, and the original does not (`q608/one-side-by-side.png`,
from `q608/run1/panel-1.png` and `q608/orig/run-a/a-08.png`).

## The four state words a `.md2` material can get

Built by `Texture_BlendStateWord` at `0x00590c40`, switching on the engine texture object's flags at
`+8`. `FUN_00581790` is what builds the word for a mesh material.

| `flags & 0x300` | State word | Alpha test | Blend | Depth write |
|---|---|---|---|---|
| none | `0x1256` | off | off | **ON** |
| `0x100` | `0x2756` | on, ref 240 | `SRCALPHA` / `INVSRCALPHA` | **ON** |
| `0x200` | `0x556` | on, ref 16 | `SRCALPHA` / `INVSRCALPHA` | **ON** |
| `0x300` | `0x200552` | on, ref 16 | `SRCALPHA` / `ONE` | **ON** |

**See-through geometry writes depth.** None of the four words sets bit `0x800`.

`0x100` on its own means a 32-bit source texture.

## Pixel classification and material-load overrides

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00574bd0` | — | On a `.wct` that loaded (`FUN_00564c00`), sets the flags `[0x0087a24c]` without the classifier: `0x24`, `0x25` with an alpha chunk read (cut out: `0x100`), `0x27` with header byte 1 set as well (graded: `0x200`); `textures.md`, "The loader" (Q358) | Decompile |
| `FUN_00575160` | — | The other formats': counts texels whose alpha is strictly between `0` and `0xf0`, and sets the "graded" bit when that count reaches `truncate(float(0.05) * width * height)`; `__ftol` truncates the threshold before the comparison at `0x005755c7`. A 4-byte source sets the cut-out bit whatever its alpha | Disassembly |
| `0x00701720` | — | The `0.05` multiplier used by that test | Data at that address |
| `0x00581820` | — | The only bit of the `.md2` **material** flag word the draw path tests: `0x800`, which forces the opaque word | Disassembly |
| `ProceduralBase_SkirtFlags` (`0x0046aa10`) | — | Sets material `0x800` on each triangle's material of a procedural base's floor, and clears it for material 0 on a footprint cell whose edge mask is nought; on every load that runs it, `Mesh_SetMaterialsOpaque` follows and sets it on them all (`textures.md`, "Which materials the loader makes opaque") | Decompilation (Q361, Q362) |
| `Mesh_SetMaterialsOpaque` (`0x0046a550`) | — | The writer that decides: loader flag `0x2000` (`0x00462c71`) sets `0x800` on every material of the floor mesh `FUN_00469a80` picked, or of the root mesh, and of an item's `P` model's root (`0x00462ca7`) | Decompilation; the original's memory (Q362) |

The inspected draw routine `FUN_00581790` does not read source material `0x2` as an alpha gate.
This is a bounded observation, not proof that the bit is unused elsewhere. Texture flags are
also affected by material loading, so pixels alone do not determine every draw state.

### Independent load-to-draw trace, 2026-10-03

Observed with headless Ghidra on an isolated project copy, x86/Windows, image base `0x00400000`.
The loaded program and installed `testme.exe` report SHA256
`cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`.
The existing live Ghidra project was not opened or changed; the copy closed with `unsaved=False`.
The trace used decompilation plus instruction listings and data reads at the addresses below.

The model loader chain inspected was `0046dcf0 -> 0046dd50 -> 00579e00 -> 00575c60 ->
00575e10 -> 00575870 -> 005755f0 -> 00574bd0 -> 00575160`. `00579e00` supplies source
material records from model `+0x50` in eight-byte steps and frame records from `+0x54` in
sixteen-byte steps. `00575c60` retains those pointers and invokes `00575e10` immediately or deferred.

| Observed operation | Instruction evidence | Limit |
|---|---|---|
| Source flags `0x1000`, `0x2000`, `0x4000` select load options `2`, `4`, `8` respectively | `00575e32`, `00575e49`, `00575e60`, each calling `00575b80` | Which installed or generated materials reach these options was not established |
| Selecting an option assigns cache `+0x10008`; later matching tests overwrite earlier ones | `00575b80`; consumed and cleared in `005755f0` | These options are not ORed together |
| Option `4` bypasses the ordinary alpha-classifier mapping | `00575725..00575734` | This does not name all effects of the option |
| Option `8` ORs texture flags `0x300`, selecting the additive state | `00575754..00575760`, then `00590c40` | Source material flags and runtime texture flags are different words |
| Source low bits `0x10` and `0x20` propagate to texture flags `0x10000` and `0x20000` | `00575ea1`, `00575eb3`; reuse in `00575c60` ORs `0x30000` if either is present | The final high-level purpose of those bits remains unknown |
| Material `0x800` forces state `0x1256`; otherwise texture flags select the table above | `00581820`, `00581826`, `00581830`, `00590c40` | Mesh `0x2` is a separate depth-write control |
| Partial-alpha count is compared with an integer-truncated threshold | `0057557e`, `00575584`, `005755c2` (`__ftol`), `005755c7`, `005755cb`; multiplier bytes `cd cc 4c 3d` at `00701720` | OpenTPW currently compares `partial * 20 >= texels`, which can disagree at the boundary |

**Strong inference:** the inspected original path does not justify treating source material
`0x2` as the alpha-enable semantic used by `ModelFile.MaterialData.IsTranslucent` and
`LobbyModel.MaterialFlagsFor`. OpenTPW took the gate out in Q362 (below).
Prior observations that removing it makes holes do not prove the original bit's meaning.

**Settled since (Q362):** OpenTPW draws a material's alpha by its texture and `0x800` alone, `0x2` playing no part:
`0x800` is the file's or the loader's (`Mesh_SetMaterialsOpaque`, above; `textures.md`, "Which materials the loader makes
opaque"). The gate is gone; Q361's 174 of 174 floor materials are that loader's.

**Settled since (Q359):** the holes were this game's own: a floor plate's clear border shows what lies under the
footprint, and neither game's terrain pass draws ground there; the original's floor texture is a composited tile since Q617, so its border is the blended ground (`textures.md`, "What a floor's clear border shows"); the reference is the
texture's header since then.

**Unknown then:** why those holes occur. Texture decoding, another setup path, runtime mutation,
caching and the comparison scene remain possible explanations. This static trace did not
compare matching original/OpenTPW frames or live texture flags. It changes documentation and
comments only. Before altering rendering, independently capture the same disagreeing material's
source flags, runtime texture flags and decoded alpha pixels in both engines at the same draw.

## What actually disables depth writes

For a world sprite, its flag `0x80000`: `Sprite_BuildQuad` (`FUN_00589b50`) ORs `0x800` into the quad's word
(`0x00589e85`); `SpriteList_Draw` (`FUN_0058a770`) ORs one for `0x4000` too, but only into the key it groups by. For a
particle, its record's `0x4000`: `SpriteBatch_DrawParticles` ORs it (`0x0057c7fd`; `park.md`, "A park's particles, started
and drawn"). A person's flags carry neither (`park-engine.md`, "What hides a person below their feet"). For a mesh, bit `0x2` of a **mesh record's first dword**, not the material word of the same value. Conflating the
two sends you the wrong way, exactly like `Node.Flags` versus `Node.IdFlags`. `ModelFile.cs` skips
that dword as "initial mesh data".

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00581790` | — | Builds the state word for a mesh material | Disassembly |
| `0x005817ba` | — | `TEST AL,0x2` then `OR ESI,0x800` — the mesh-record bit that clears `ZWRITEENABLE` | Disassembly |

Swept across 839 static models / 4924 mesh records: only four distinct flag words exist —
`0x1` (x4585), `0x401` (x331), `0x2` (x5), `0x81` (x3). Bit `0x2` is set on exactly five records, every
one of them a mesh named `heightfield` in a `base.md2`: the four park terrains and the lobby. Every one of those five
records is empty (no vertices, no faces), so no mesh that reaches the screen has depth writes off (a world particle does): `ParkTerrain` passes
the park `base.md2` through `LobbyModel`, and the ground itself is drawn by `ParkGround` from the file's heightfield block.

**Trap when sweeping `.md2`:** an animation file has `meshPtr` (`+0x70`) `== 0` and must be skipped, or
its matrices are read as flag words and come out as float bit patterns.

## Texture addressing: CLAMP, and WRAP where a frame asks for it

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00566ce3`, `0x00566dae` | `D3DTSS_ADDRESS = 3 = D3DTADDRESS_CLAMP` | The device's init (`FUN_00566840`): stage 0 clamped | `PUSH 0x3` / `PUSH 0xc` / `PUSH 0x0` before `CALL [ECX + 0xa0]` (`IDirect3DDevice3::SetTextureStageState`) |
| `0x005678cf` → `FUN_00567a70` | — | The state word's applier (`FUN_00567620`): bit `0x400000` sets stage 0's `ADDRESS` to `1`, `D3DTADDRESS_WRAP`, and clear sets it back to `3`, CLAMP, whenever the bit changes | Disassembly: `FUN_00567a70( word, stage 0, mask 0x400000, state 0xc, set 1, clear 3 )` |
| `Texture_BlendStateWord` (`0x00590c40`), `FUN_00581790` | — | Put `0x400000` in a material's word when its texture's flags (`+8`) hold `0x10000` or `0x20000` (the first only where the renderer is `[0x008bd524]` 1) | Decompile |
| `FUN_00575e10` | — | Loads a frame's textures; after each, the frame's word `0x10` ORs `0x10000` and `0x20` ORs `0x20000` into the texture's flags; its byte 1's `0x20` asks the load for `0x8030000` | Decompile |
| `FUN_00575c60` | — | Called for a frame from a model's load (`FUN_00579e00`, `0x00579f7a`); a frame already loaded has `0x30000` ORed into all its textures where its word holds `0x30` | Decompile |
| `0x004626fb` | — | `FUN_00461f10` ORs `0x8030` into the frame word of every material of a mesh a UV track scrolls, before the same function loads the textures (`0x00462740`, `0x00462771`) | Disassembly |
| `FUN_00575870` | — | The texture cache: one texture per folder and name, counted by its users, so the bits above stay on it for every model using it | Decompile |
| `FUN_00590350` | — | A texture without `0x30000` is packed into a shared page with a half-texel inset (the reason it must clamp); one with it is not | Decompile |

So **a model's texture is clamped unless its frame carries `0x10` or `0x20`, or its mesh scrolls its UVs**, and then
it wraps in both directions, the stage having one address mode for both. The data agrees without exception: of the
12,951 material uses of the 838 static models, 2,573 reach a texture coordinate past 0 or 1 by more than 0.01, and
2,572 of those carry a wrap bit; the other names no texture (FileFormats `models.md`, "Bits 0x10 and 0x20"). The sweep
of every `SetTextureStageState` call: 98 push a literal state, two write `ADDRESS` (the init's CLAMP), none `ADDRESSU`
or `ADDRESSV`; the six through a register are the init block's counter (four) and `FUN_00567a70`; `SetRenderState`
(`+0x58`, 88 calls) is never handed the old address states 3, `0x2c` or `0x2d`.

**What this game does** (Q363): `LobbyModel.WrapFrames` and `Wraps` take the frames with a wrap bit and those of a
scrolled mesh; every other material's vertices carry `LobbyModel.ClampFlag`, and `test.shader` samples them with
`s_ColorClamp`, the clamped twin `Material.Set` binds beside `s_Color`. Lost Kingdom's outer river banks
(`jri_bnk4` to `6`, word `0x3`) now keep their top edge, where a wrapped sample took in their clear bottom row and the
cut-out dropped a thin pale line (`q363/side-by-side.png`: 451 line pixels on main, 38 here, none in the original).
**Deviation, counted:** each material here keeps its own way where the original's shared texture takes the bits any
of its users gave it; a texture path one model wraps and another clamps is counted `TEXTURE_WRAP_SHARED` (five in
the lobby and the stock park: the lobby's `hgt_rai1`, `sen_pnl1`, `sen_pnl2`, `Sen_pnl4` and the jungle's
`m_grass3`). The interface's meshes (`UiMesh`), the ground, the paths and the hoardings keep the sampler their
callers bind, which were not compared.

**The lobby sea.** OpenTPW's lobby sea is a 10,000-unit plane whose UVs the shader computes as `position * 0.05`, so
it runs hundreds of tiles wide; it keeps `TextureFlags.Wrap`. The original's sea is its own mesh with authored UVs.

## Texture filtering: chosen once at the start, then three bits of the word

Decoded for Q617 from the listing (`lib/testme.objdump`; the device's `SetTextureStageState` is `+0xa0`, its states
16, 17, 18 and 19 are `MAGFILTER`, `MINFILTER`, `MIPFILTER` and `MIPMAPLODBIAS`).

| Address / value | What it does | Evidence |
|---|---|---|
| `DetailFile_Apply`, `0x0044d8f0`..`0x0044d921` | `GraphicalOptions.TEXTUREFILTERING` (`[0x00785878]`, key 1 of the table at `0x00744300`; the files' comment says 0 point, 1 bilinear, 2 trilinear, 3 anisotropic): 0 and 1 clear `0x18` of `[0x008bd550]`, 2 and 3 set both bits, any other value leaves them. **So 0 and 1 draw alike, and 2 and 3 alike.** Shipped: `low.sam` 0, `med.sam` 1, `high.sam` 2 | Disassembly |
| `0x005669cc`..`0x00566a4a` (the device's init `FUN_00566840`) | With `[0x008bd550]` `0x2` (`MIPMAP` 1, `0x0044db3a`; 1 in all three files): `MIPFILTER` 3 (`D3DTFP_LINEAR`) where `0x8` is set and the triangle caps' filter word (`[0x00879da8] + 0xb0`) has `0x20` (`LINEARMIPLINEAR`), else 2 (`POINT`) where it has `0x8` (`MIPLINEAR`), else 1 (`NONE`), and 1 on the card `[ebp+0x408]` `0x12d2`, `[ebp+0x40c]` `0x18` (not identified); `MAGFILTER` 5 (`ANISOTROPIC`) where `0x10` is set and the caps have `0x4000000` (`MAGFANISOTROPIC`), else 2 (`LINEAR`); `MINFILTER` 1 (`D3DTFN_POINT`). Without `MIPMAP` `MIPFILTER` is still written 1 (`NONE`, `mov [esi-0x20],ebx` at `0x005669cc`, before the `MIPMAP` test); only the `MAGFILTER` and `MINFILTER` slots are skipped (jump `0x005669d6` to `0x00566a4d`). Handed to stage 0 at `0x00566ca9`, `0x00566cbd`, `0x00566cd1` | Disassembly |
| `0x00566f7b` | The init leaves the state cache's word `0xffffffff`, every bit "set" | Disassembly |
| `FUN_00567620`, `0x0056788e`..`0x0056792a` | Each word, through `FUN_00567a70` (which writes a state only where the bit differs from the cached word): `0x20000` set `MAGFILTER` 1 (`POINT`), clear 2 (`LINEAR`); `0x40000` set `MIPFILTER` 1 (`NONE`), clear the init's choice. `MINFILTER` is written on every word: 2 (`LINEAR`) where the word has `0x400000` (the wrap bit, "Texture addressing" above), 1 (`POINT`) where it has not. With the cache all ones, the init's `ANISOTROPIC` stands only until the first word without `0x20000` | Disassembly |
| `FUN_00566fa0` (from `0x00569d23`) | `MIPMAPLODBIAS` = `[0x008bd4a8]`, which no instruction writes: 0 (the zero-filled end of `.data`) | Disassembly |
| `0x0056f753` | The terrain pass `FUN_0056f670` asks its batch for word `0x1256` (`0x101256` where `[0x008bcbc8]` has `0x2000`): none of the three bits, so **a ground tile is magnified `LINEAR`, minified `POINT`, and its mip level is the init's choice** | Disassembly |

So a clamped texture, the ground's tiles and every texture packed in a page among them, is minified by its nearest
texel and magnified bilinear; at `med.sam` (`TEXTUREFILTERING` 1) the mip level is the nearest one where the caps
allow (`POINT`), at `high.sam` the two nearest blended where the caps have `0x20` (and the nearest or none where they do not). What the caps answer under Proton, and so which `MIPFILTER`
the reference install draws, is not read live. **OpenTPW** samples every material but the `PointFilter` ones (the loading screen's status box) 16x anisotropic over every mip level
(`Material.CreateSampler`, `Texture.SamplerFor`), which differs from the original only where a texture is minified (Q29). The ground's
tiles at the park's own cameras are magnified in both, and bilinear in both, at 16 pixels (Q617: the gate's path, `q617/path-zoom-ours-left-original-right.png`) and at 32 (Q618, `q618/side-by-side-gate.png`; `park-engine.md`, "The original at 32").

## Three back-to-front sorts — which OpenTPW does not do

World geometry is FVF `0x1c4` (`XYZRHW`, stride `0x20`), so vertex `+8` is screen Z. The original sorts
at three levels, all back-to-front.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00565590` | — | 1. Per-batch depth key `+0x30`: zeroed when the entry is allocated (`0x005826ae`); it gains the depth callback's sum only when the batch flags carry `0x20` (`0x0056560a`), then is divided by the vertex count (`0x005656f3`), so a batch without `0x20` keys 0. A caller may preset it with `FUN_005658c0( key )` (any value but -1.0). There is no `0x800000` test in `FUN_00565590` (fork watch 2026-10-25, `verify-all.md` near-conflict 1). | Disassembly |
| `FUN_00565590` → `FUN_00565490` | — | 2. Per-triangle sort: `FUN_00565590` sums each triangle's `z0+z1+z2` and, **only when the state word misses the `0x3000` mask**, sorts them descending with the quicksort `FUN_00565490` — i.e. for the graded (`0x556`) and additive (`0x200552`) words and the lobby's lightning words (`0x9a1`, `0x200d52`), which also miss `0x3000`. The opaque and cut-out words carry `0x1000`/`0x2000` and are drawn at submit (`0x0056570b`); a state of exactly `0x800` is queued unsorted (`0x0056571e`); in mode 4 a state with `0x1000` skips the triangle sort (`0x0056573d`). The renderer's constructor writes the `0x3000` mask (`0x0056682d`); the device probe `FUN_00567270` writes `0x1000` there instead (`0x0056728c`) when the caps word `[[0x00879da8]+0xa4]` lacks bit `0x40` (what that bit means is inferred, not read). Mode 4 sums the corners' depths as integers and sorts with the twin `FUN_005653d0` (`0x005657e7`); mode 4 is `[0x0079d86c]` bit `0x40` (`0x0044dde9`), which the live `0x82` lacks, so play runs mode 1 (fork watch 2026-10-26, L1). | Disassembly |
| `FUN_00582ad0` → `FUN_005829e0` | — | 3. Per-batch sort, descending on the key at `+0x30` | Disassembly |

**OpenTPW does none of this sorting.** It does not need it for the lobby: the only graded surfaces
there are the shoreline ripples and the Space dish's cone, each a single layer that does not overlap
another. OpenTPW draws every see-through surface after every solid one (`Level`'s translucent pass), unsorted within
that pass. Parks load and have not been checked for it. **Whether the missing sorts show on screen, in either
scene, has never been measured.**
