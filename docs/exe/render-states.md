# Render states

How Theme Park World sets Direct3D render states. It never sets them per material: the engine compiles
a single 32-bit **state word** per draw and applies it lazily against a cached copy, so every visual
difference between opaque, cut-out, graded and additive geometry is one of four constants. Three facts
matter more than the rest. **CULLMODE is set to `D3DCULL_NONE` once at startup and never rewritten** —
nothing in the game is one-sided. **See-through geometry still writes depth** — none of the four
material words disables depth writes. And the choice between a cut-out (hard-edged) and a graded
(soft) alpha reference normally follows a **pixel classifier**, subject to material-load options
and the material `0x800` opaque override described below.

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
| `0x1000` / `0x2000` | Not render states: together the `0x3000` "exempt from sorting" mask | `FUN_00565590` tests the mask |
| `0x80000` | `SPECULARENABLE` | `FUN_00567620` |
| `0x100000` | `FOGENABLE` | `FUN_00567620` |
| `0x800000` | Overrides the per-batch depth key with 0 | `FUN_00565590` |

## States set once for the whole run

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0056695c` | `CULLMODE = 1 = D3DCULL_NONE` | **Set once at startup and never rewritten anywhere in the program.** Nothing in the game is one-sided; foliage is authored expecting it. Render a park's foliage or fences with backface culling on and they come out half-missing. | All 88 `call [reg+0x58]` sites scanned; no other site writes CULLMODE |
| `0x005669b7` | `ALPHAFUNC = 7 = D3DCMP_GREATEREQUAL` | Alpha test comparison, never changed | Disassembly |
| `0x00573614` | `ZFUNC = 4 = D3DCMP_LESSEQUAL` | Depth comparison, never changed. OpenTPW uses `Less` — **a difference, not yet shown to matter.** | Disassembly |
| `DAT_007012d8` | ALPHAREF pair table | `{0x1000, 0xf000, 0x10, 0xf0}` | Data at that address |
| `FUN_00567270` | — | Probes the card and selects index 2 of that table, so the two references in use are **16 and 240 of 255** | Disassembly |

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
| `FUN_00575160` | — | Counts texels whose alpha is strictly between `0` and `0xf0`, and sets the "graded" bit when that count reaches `truncate(float(0.05) * width * height)`; `__ftol` truncates the threshold before the comparison at `0x005755c7` | Disassembly |
| `0x00701720` | — | The `0.05` multiplier used by that test | Data at that address |
| `0x00581820` | — | The only bit of the `.md2` **material** flag word the draw path tests: `0x800`, which forces the opaque word | Disassembly |

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
`LobbyModel.MaterialFlagsFor`. The current gate remains an empirical compatibility workaround.
Prior observations that removing it makes holes do not prove the original bit's meaning.

**Unknown:** why those holes occur. Texture decoding, another setup path, runtime mutation,
caching and the comparison scene remain possible explanations. This static trace did not
compare matching original/OpenTPW frames or live texture flags. It changes documentation and
comments only. Before altering rendering, independently capture the same disagreeing material's
source flags, runtime texture flags and decoded alpha pixels in both engines at the same draw.

## What actually disables depth writes

Bit `0x2` of a **mesh record's first dword**, not the material word of the same value. Conflating the
two sends you the wrong way, exactly like `Node.Flags` versus `Node.IdFlags`. `ModelFile.cs` skips
that dword as "initial mesh data".

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00581790` | — | Builds the state word for a mesh material | Disassembly |
| `0x005817ba` | — | `TEST AL,0x2` then `OR ESI,0x800` — the mesh-record bit that clears `ZWRITEENABLE` | Disassembly |

Swept across 839 static models / 4924 mesh records: only four distinct flag words exist —
`0x1` (x4585), `0x401` (x331), `0x2` (x5), `0x81` (x3). Bit `0x2` is set on exactly five records, every
one of them a mesh named `heightfield` in a `base.md2`: the four park terrains and the lobby. Every one of those five
records is empty (no vertices, no faces), so nothing that reaches the screen has depth writes off: `ParkTerrain` passes
the park `base.md2` through `LobbyModel`, and the ground itself is drawn by `ParkGround` from the file's heightfield block.

**Trap when sweeping `.md2`:** an animation file has `meshPtr` (`+0x70`) `== 0` and must be skipped, or
its matrices are read as flag words and come out as float bit patterns.

## Texture addressing is CLAMP, and nothing in the image ever wraps

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00566ce3` | `D3DTSS_ADDRESS = 3 = D3DTADDRESS_CLAMP` | Stage 0, in the renderer's own init block | `PUSH 0x3` / `PUSH 0xc` / `PUSH 0x0` before `CALL [ECX + 0xa0]` |
| `0x00566dae` | `D3DTSS_ADDRESS = 3` | The same write, on the stage that block's register holds | Disassembly |

Swept across **all 104 calls through the device vtable's `+0xa0`** (`SetTextureStageState`): 98 push a
literal state, and the only **two** that write `D3DTSS_ADDRESS` (`0xc`) both write **3,
`D3DTADDRESS_CLAMP`**. **No site writes `ADDRESSU` (`0xd`) or `ADDRESSV` (`0xe`) at all.**

The literal states that do appear are the positive control that the sweep can see a state when there
is one: `TEXCOORDINDEX` ×15, `COLORARG1` ×14, `COLOROP` ×13, `ALPHAOP` ×12, `ALPHAARG2` ×10,
`COLORARG2` ×8, `ALPHAARG1` ×7, `MINFILTER` ×4, `MAGFILTER` and `MIPFILTER` ×3 each, `ADDRESS` ×2,
and one each of the four `BUMPENVMAT` slots, `BUMPENVLSCALE`, `BUMPENVLOFFSET` and `MIPMAPLODBIAS` —
98 in total.

**Bounded:** 6 of the 104 push the state through a register (four of them inside that same init
block, where it is the block's own counter) and are not covered. So this is "no literal site ever
asks for wrapping", not "the engine cannot wrap".

**What it does not license.** OpenTPW's lobby sea is a 10,000-unit plane whose UVs the shader computes
as `position * 0.05`, so it runs hundreds of tiles wide and `CLAMP` would stretch a single texel over
the whole ocean. The original's sea is its own mesh with authored UVs, which is why clamping costs it
nothing there. Porting this constant onto that plane would be a faithful-looking change that wrecks
the picture — the sea's `TextureFlags.Wrap` stays, and re-sourcing the sea from the original's mesh is
where this becomes relevant.

## Three back-to-front sorts — which OpenTPW does not do

World geometry is FVF `0x1c4` (`XYZRHW`, stride `0x20`), so vertex `+8` is screen Z. The original sorts
at three levels, all back-to-front.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00565590` | — | 1. Per-batch depth key = mean vertex Z. State bit `0x800000` overrides it with 0. | Disassembly |
| `FUN_00565590` → `FUN_00565490` | — | 2. Per-triangle sort: `FUN_00565590` sums each triangle's `z0+z1+z2` and, **only when the state word misses the `0x3000` mask**, sorts them descending with the quicksort `FUN_00565490` — i.e. only for the graded (`0x556`) and additive (`0x200552`) words. The opaque and cut-out words carry `0x1000`/`0x2000` and skip it. | Disassembly |
| `FUN_00582ad0` → `FUN_005829e0` | — | 3. Per-batch sort, descending on the key at `+0x30` | Disassembly |

**OpenTPW does none of this sorting.** It does not need it for the lobby: the only graded surfaces
there are the shoreline ripples and the Space dish's cone, each a single layer that does not overlap
another. OpenTPW draws every see-through surface after every solid one (`Level`'s translucent pass), unsorted within
that pass. Parks load and have not been checked for it. **Whether the missing sorts show on screen, in either
scene, has never been measured.**
