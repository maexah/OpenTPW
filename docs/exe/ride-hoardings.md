# Ride hoardings

Q91, decoded 2026-10-03 against the 2.0 `testme.exe`. **Decoded, not implemented or verified on screen.**
The close/open calls raise and lower a separate, generated hoarding mesh around the ride. They change its
texture choice, vertices and UVs; they do not replace the ride model or fade its opacity. Q91b is the build.

## Construction and ownership

| Address / offset | Original name (if known) | What it is | Evidence |
|---|---|---|---|
| `0x00744be4`, `0x00744c20` | `Shape`, `Hoarding` | Item schema type 11 blocks with parser modes 1 and 2. The existing file syntax is documented in FileFormats `sam.md`, “Info.Shape is a block, not a value”. | Fresh schema bytes; schema/base accessors `00412c90`/`005b0d60` and `00401030` slot walk |
| `0x00414163`–`0x00414175` | — | The item loader passes descriptor `+0x18` and `+0x1c` as shape and hoarding to `FUN_004629d0`. Its other model-loading arm does the same at `0x004141c7`. | Raw call arguments; schema |
| `0x00462d88`–`0x00462dac` | — | Builds hoardings only when loader flag `0x8` is set and the hoarding argument is non-null; passes the current theme string, model definition and hoarding block to `FUN_00452e70`. | Raw instructions |
| `FUN_00452e70` | — | Requires the base model's node count at `[model+8]+0xc` to exceed one. Generates panels from the parsed block's edge bits `1/4/0x10/0x40`, sorts their angle keys through `FUN_00470630`, and creates a mesh named `Hoardings`. Each panel has four vertices and four triangles (opposite-facing pairs). | Raw generation and index writes, `0x00453283`–`0x0045403c`; decompiler stack recovery is unreliable here |
| Model `+0xb0` | — | Hoarding definition: panel count, two grid dimensions, endpoint-pair array; published at `0x00454012`–`0x00454039` (`+0xb0` at `0x00454028`, `+0xb4` at `0x00454033`). Null disables close/open effects. | Generator tail; consumers `00454190`, `004543c0` |
| Model `+0xb4` | — | Generated hoarding mesh, cloned per model instance by `FUN_004557c0` → `FUN_00455690`. It owns mutable vertex/UV/material-instance data. | Construction and clone calls |
| Model `+0xb8`, `+0xbc` | — | Float progress and signed progress rate. Close/open preserve current progress, allowing reversal. | `00454550`, `004547c0`, `004548f0` |
| `FUN_00454190` | — | Fits panel endpoints to terrain height, relative to the ride's model Y, with factor `0.5`; applies the object's quarter-turn rotation when locating endpoints. Called on placement when loader flag `4` is set. | `0x004635fc`; endpoint and height writes |
| `FUN_00454670` | — | With model flag `2`, activation copies position, rotation and bounds, then registers the hoarding in the spatial draw structure through `00425490` → `0045bcb0`. Deactivation resets the selected texture to closed and unregisters through `004254e0` → `0045be80`. | Helpers and raw instructions |
| `FUN_00455ad0` | — | Removes a registered child before releasing its generated allocations and clearing `+0xb4`. | Destructor |

The hoarding mesh is additional geometry; the ride's ordinary animation continues separately.
`Info.Hoarding` is the data key. OpenTPW's `ItemDescriptionFile` recognizes its block delimiters but
skips its contents; only `Info.Shape` is parsed into a grid. The old `RideInfo.Hoarding` property is not
this loader's implementation. Both `CLOSED_RIDE_MODEL_CHANGE` and `OPENED_RIDE_MODEL_CHANGE` remain counted.

## Four texture choices

`FUN_00454550(model, kind)` switches the first material's texture-frame pointer:

```text
child = read32(model + 0xb4)
mesh = read32(child + 8)
material = read32(mesh + 0x2c)
header = read32(read32(child + 0x24) + 4)
frames = read32(header + 0x50)
write32(material, frames + offset)
```

Frame entries are **eight bytes apart**, not the kind values apart.
`FUN_00424ef0` constructs that table from the four-entry template at `0x0074d008`, whose texture records
start at `0x0074cfc8`. The executable formats their folder as `data\levels\%s\MiscMesh\textures`.

| Kind | Frame byte offset | Model flag | Texture name from executable | Caller meaning |
|---|---|---|---|---|
| `1` | `0` | `0x100` | `Closed.tga` | Ordinary close, including SetState's closing side effect |
| `2` | `8` | `0x200` | `Hoarding.tga` | Broken down: `0x004e1542` |
| `4` | `16` | `0x400` | `Condemn.tga` | Condemned: `0x004e1584` |
| `8` | `24` | `0x800` | `Upgrade.tga` | Upgrade requested: `0x004e0098` |

Kind 1 selects closed **only if opening bit `0x80` is set or no selection bit in `0xf00` is set**.
This preserves a broken/condemned/upgrade texture when an ordinary close follows. Kinds 2/4/8 change
selection only if their own bit is absent. A selection clears the other three selection bits.
Every path after the non-null `+0xb0` guard, even one making no texture change, sets rate `+0.2`,
activates if bit `0x20` was absent, then sets active/closing bits `0x20|0x40` and clears opening bit `0x80`.

`FUN_004547c0` also requires non-null `+0xb0`. It sets rate `−0.3`; only if active bit `0x20` is set
does it clear closing bit `0x40` and set opening bit `0x80`. It does not switch texture immediately.
All eleven close calls and seven open calls still agree with the caller list in `ride-operation.md`.

## What the animation draws

`FUN_004548f0` requires non-null `+0xb0` and `+0xb4`, and either movement bit in `0xc0`.
It updates `p = clamp(p + rate * dt, 0, 1)`. `dt` is `DAT_007b497c`, written by `FUN_00473440` as
an unsigned delta of the scaled, pausable engine clock multiplied by `0.001`. The chain reaches
`004031e0` → `00402f10` → `004030d0` → `004033a0` → `005f5fa0` → `005f5f10` (QPC or `timeGetTime`).
Nominal progress endpoints therefore take **5 engine-clock seconds** closing from zero, **10/3** opening
from one. These are decoded rates, not measured wall-clock timings; the clock's rate and pauses matter.

When progress changes, `FUN_004543c0` (guarded by model flag `2`) edits each panel `i`, out of `N`:

```text
f = clamp(4*p - 3.2*i/N, 0, 1)
a = 1.0 for even i, 0.8 for odd i
Y(first endpoint's upper vertex) = Y(first endpoint's lower vertex) + 10*f*a
Y(second endpoint's upper vertex) = Y(second endpoint's lower vertex) + 10*f*a
V(lower pair) = 1 - f*a; U(lower pair) = 0, 1
```

The upper pair keeps its initial V of 1. Thus height and sampled texture change together, in panel
order, with alternate heights. **Progress 1 is the stop condition, not a guarantee that every panel's
`f` equals 1**: for sufficiently large `N`, the last panels remain below that value under this formula.
Constants were read from `0x006fe440/468/470/480/484/488` and checked against the raw x87 operations.

The update returns immediately after changed progress. Cleanup happens on a subsequent update when
clamped progress is unchanged: at 1 it clears closing bit `0x40`; at 0 with opening bit `0x80` it clears
`0x20|0x80` and unregisters the hoarding. The helper resets its texture/selection to closed then.
`FUN_0044e410` supplies the frame delta for active hoardings; `FUN_0044e380` is another update caller.

The saved-model restore path (`FUN_004647a0` → `004547f0`) restores progress and selection/movement flags,
reactivates when marked active, reapplies the panel deformation and texture, and recovers the rate from
closing/opening bits. A build must preserve that behavior; the saved byte layout is outside this decode.

## Evidence and next session

Fresh Ghidra project copy: all 20 source-project files hash-matched before opening; original project and
locks preserved. Program identity: SHA256 `cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`,
image base `0x00400000`, `x86:LE:32:default`. Evidence is in scratch `q91/`: `initial-decode.txt`,
`hoarding-core.txt`, `raw-core-and-item-loader.txt`, `schema-and-callargs.txt`, `texture-template-and-clock.txt`,
`loader-and-tick.txt`, `block-builders.txt`, `timer-and-parser.txt`, `schema-accessors.txt` and
`instance-copy-and-schema-base.txt`. These are fresh executable reads;
no new shipped-file layout or corpus-count claim is made.

Q91b must implement the data-driven hoarding geometry/material/lifecycle and confirm Belly Bounce before,
after closing and after reopening with screenshots **and** predicted log/census values. Measure the real
panel count before choosing a numeric runtime prediction; do not replace the `Info.Hoarding` outline with a
bounding rectangle. The exact glyph-to-edge parser and complete corner-fitting algorithm need to be followed
when porting them. No regression test, mutation run, game launch, screenshot or runtime census is claimed
for this decode-only session. The visual bug remains unresolved.
