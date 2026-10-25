# The park HUD, from the executable

Everything the park's interface is made of, read out of `testme.exe`: the 16-bit **layout stream** opcode
format that is compiled into the executable (there is no layout file), the mesh-name hash that binds a
control to a model, the assembly function that builds a park's interface, each park panel's stream with
its control ids, rects, artwork and help rows, the six management buttons' real dispatch, and the park map
screen. Read with the park-engine page, which holds the 17 shortcut actions, the screen map (54 calls, 41 rows) and the
interaction modes.

## The layout stream opcode format

`UI_LoadTree( stream, handler )` is `UI_ParseTreeStream( stream )` followed by `FUN_0065e526( handler )`.
The stream argument is **16-bit opcodes compiled into the executable**, not a filename.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0065fd58` | `UI_ParseTreeStream` | Walks the opcode stream and builds the control tree | Disassembly; six streams walked clean |
| `FUN_0065e526` | — | Installs the handler after the tree is parsed | Second half of `UI_LoadTree` |
| `FUN_0065fc18` | — | Reader: one short | Operand reader |
| `FUN_0065fbf0` | — | Reader: one dword | Operand reader |
| `FUN_0065fc38` | — | Reader: four shorts (a rect) | Operand reader |
| `FUN_0065fcc5` | — | Reader: two shorts | Operand reader |
| `FUN_0065dd7b` | — | `( this, type, flags, id, x0, y0, x1, y1 )` constructs the control; **type** selects the class (1 = panel, 2 = button, 3 = slider, and ten more up to 0xd) | Disassembly |
| `FUN_00659c42` / `FUN_0065d070` | — | Look up a mesh by hash / set it on the control | op 1 |
| `FUN_0065d1c8` | — | Sets a skin | op 2 |
| `FUN_00661830` | — | Stores the UIHELPTEXT row at control `+0x90` | op 0x11 |
| `FUN_00661850` | — | Stores a second id at control `+0x94` | op 0x12 |

### The opcodes

    op 0     [short type][dword flags][dword id][4-short rect]  -> FUN_0065dd7b, then RECURSE
    op 1     [dword hash]   the control's mesh
    op 2     [dword hash]   a skin
    op 3     [4-short rect] the text rect
    op 4     [short sub]    1/3 = +rect, 2 = +6 bytes, 4 = [short n] then n*4 bytes
    op 5     END of the current level - pops one recursion
    op 6,7   [rect] implicit child, type 2 flags 0x21 id 1/2, RECURSE
    op 8     [rect] slider thumb, type 2 flags 1 id 3, installs SliderThumb_Callback, RECURSE
    op 9     [rect] CONDITIONAL - a type 4 or 7 parent with no child id 1: scrollbar, type 3 flags 0x10 id 1, RECURSE
    op 10    [rect] CONDITIONAL - read only under a type 4 or 7 parent
    op 0xb   [short n] then n pairs
    op 0xc   [short n][rect] type 2 flags 1 id n+0x10, RECURSE
    op 0xd   [short][short]
    op 0xe,f [rect] like 6/7
    op 0x10  [short n][rect] type 6, id n|0x11100000, RECURSE
    op 0x11  [dword] the UIHELPTEXT row
    op 0x12  [dword] a second id

### Operand sizes, read out of `UI_ParseTreeStream` rather than inferred

After the 2-byte opcode:

    0    18 then RECURSE     1  4     2  4     3  8     5  0 and RETURNS (this is the pop)
    6,7,8   8 then RECURSE   0xd  4   0x11  4  0x12  4
    0xc, 0x10   10 then RECURSE        0xe, 0xf   8 then RECURSE
    4    2 sub-op, then 8 / 6 / 8 / (2 + 4*n) for sub-ops 1 / 2 / 3 / 4
    0xb  2 count, then 4 * count
    9, 10   READ NOTHING WHEN THEIR CONDITION FAILS - the stream's length is state-dependent

**`0x10` is 10 bytes, not 4** — a walker that guesses 4 mis-walks the whole tail.

**Ops 9 and 10 are runtime-conditional.** They read their rect *only* under a type 4 or 7 parent (op 9 also
only while that parent has no child id 1, `0x0066043e`), so a static walk must track the enclosing control's
type. None of the park panels uses them; the buy and hire lists do ("Ops 9 and 10 are runtime-conditional" below).

### Which control an attribute binds to

`case 0` recurses with **ECX = the new child** (`0065fe4c: MOV ECX,[EBP-0x30]`), but `case 1`, `2`, `3`,
`0x11` and `0x12` all load **ECX = `[EBP+0xffffff60]`, the RUNNING invocation's own `this`**
(e.g. `0065fe75`). So a mesh, skin, text rect or help row binds to **whichever control's block encloses
it** — and after an `op 5` that is the **parent**, not the child that just closed.

**The trap:** a dumper that prints attributes without marking the closes makes a parent's mesh look like it
belongs to the child printed just above it. Such a dumper puts the `guage` mesh on the meter
`0x1f` when the bytes put it on its housing `0x1e` (`... 05 00 | 01 00 9f 57 d3 69 | 05 00` at
`0x007529a0` — close, THEN mesh, THEN close). A static walker **must** track op 5 to attribute
attributes, because `case 0` recurses and `case 5` returns: that is why `guage` lands on `0x1e` and `base`
on `0x1d` though each textually follows a later id. Always print CLOSE markers and bind by an explicit
stack; attributing by "the id that precedes it in the bytes" is wrong.

### The self-check that proves the decode

Every stream address from the screen table begins with **op 0** and the walk **terminates on a
balanced op 5**. Six streams walked, six clean. If a walk ends mid-record or hits an unknown opcode, the
decode is wrong — do not "fix" it by skipping bytes.

## The mesh-name hash — cracked, 39 of 39

    h = 0
    for each character c of the name:  h = ((c ^ h) * 47) & 0xFFFFFFFF

The name hashed is **the full, untruncated name of the model's FIRST mesh node** — not the member name,
not a clamped one. `w_dialog_wave.MD2`'s first node is `wdialog w` -> `wdialogw`; the truncation already
happened when the artist named the node, so **do not clamp it yourself**.

Hashing the 1,202 `ui.wad` **member names** instead gives **zero** hits across five inits and four
transforms — the wrong name source. Node names come from `wadcat --anim <wad>` (the C# tool in `CLAUDE.local.md`, not the `wadcat.py` harness below); its `--meshes` mode
lists each model's mesh node names too, first node first.

### Node name versus file name

The stream asks by the first node's name; the loader loads `ui/<FILE>.md2`. Mapped from
`wadcat --anim ui.wad`:

| Node name (hashed) | File | Note |
|---|---|---|
| `pan_money` | `panel.md2` | the arm |
| `pane_money` | `panelend.md2` | the arm's end |
| `messagel` / `messagem` / `messager` | `f_tagl.md2` / `f_tagm.md2` / `f_tagr.md2` | message bar slices |
| `base` | `mainpanel.md2` | the gadget body |
| `handle`, `b_retract`, `b_1person`, `b_postcard`, `mb_erase`, `aerial` | same as their node names | — |

**`pan_money` and `pane_money` differ by one letter and sit one control apart** — guess either and you
silently swap them. `panel.md2` and `panelend.md2` carry **four parts each**, and they are not animation
frames: `pan_money` / `pan_info` / `pan_buy` / `pan_staff` and the matching `pane_*`, so the arm wears the
colour of whichever category it carries.

## What builds a park's interface: `FUN_00489f50`

It is the assembly point. In order:

1. Installs the **interaction mode** if `DAT_007b05e8` is null — the default type-1 mode `FUN_0046c6a0`
   through the setter `FUN_0046c350`.
2. Creates **two full-screen layers**, `FUN_0065dd7b( 1, 0, id, 0, 0, 0x7ff, 0x5ff )` — **2047 x 1535, the
   same 2048x1536 virtual screen the lobby uses**. Layer id 0 gets handler `Park_MouseMessageProc` (`0x004881a0`) and is made
   visible; layer id 1 gets handler **`FUN_00488a00`**.
3. On layer 1: `FUN_0065f11d( 0x15 )` - the layer's **cursor**, `c_crosshair.ani` (`FUN_00489720` registers
   it as `0x15` at `0x004899a8`) - then `UI_LoadTree( 0x0074fa98, 0 )`, the camcorder viewfinder.
4. Then the six panels, each `UI_LoadTree` + `UI_SetVisible(0)` — **built hidden, except the gadget**:
   `FUN_004a1d70` loads its stream onto layer 0 (`0x004a1da4`) and hides only `0x30` (`0x004a22ba`), and `0x34` in
   Instant Action (`0x004a1fa8`). The other five load onto the UI root.

| Address | What it builds |
|---|---|
| `FUN_004a1d70` | management gadget |
| `FUN_00498b50` | camcorder / postcard |
| `FUN_004b3cf0` | locator |
| `FUN_00497b20` | finance |
| `FUN_0048d8b0` | message bar |
| `FUN_00498790` | coaster builder |

**`FUN_00488a00` is layer 1's handler**, and layer 1 has the focus only in first person (`FUN_004862a0`). Its key
messages run camera-table handlers only: `0x1000a` -> `FUN_0040c900( key, mods )` and `0x1000b` -> `FUN_0040c990`.
On a key-up it looks up two shortcuts actions without running them: 0x10 (camcorder) takes the same exit as key
0x1b, and **0xf = postcard** calls `FUN_004a9380`. The camera, game, shortcuts and cheat handler chain is layer 0's,
`Park_MouseMessageProc` (`0x004881a0`) - see `park-engine.md`, "How a key is matched" and "The hand's ways out", and
`scenes.md`, "The park Escape route". The coaster bar's `FUN_004982a0` is a third route, through the camera and
coaster tables.

## The management gadget — stream `0x00752940`

Built by `FUN_004a1d70` onto the park's layer 0, with no handler for the stream (`UI_LoadTree` is passed 0,
`0x004a1daa`); `FUN_004a0a20` is installed on the body `0x1d` alone (`0x004a231d`). Four root controls:

    0x1d  panel  ( 37, 984)-( 439,1507)   the bottom-left gadget body      mesh base
      0x1e  panel  help 477                                  mesh guage     (gauge housing)
        0x1f  t9   ( 85,1100)-( 144,1324) no mesh      + meter.wct skin
      0x20  panel  help 478  + text rect  mesh date    <- THE DATE READOUT
      0x21  panel  ( 331,1069)-(1006,1495)  THE ARM   mesh pan_money  = panel.md2
        0x22  panel (1006,1069)-(1071,1382)  its end   mesh pane_money = panelend.md2
          0x23  panel ( 976,1058)-(1129,1503) handle   mesh handle     = handle.md2, 16-point region
        0x24  button ( 349,1386)-( 429,1466)  help 481  mesh b_retract  (child of 0x21)
      0x25  panel  (170,1122)-( 392,1372)  THE SIX ROUND BUTTONS
        0x2c  panel  help 494  (child of 0x25)
    0x2d  panel ( 94, 918)-( 144, 984)  mesh aerial    0x2e  help 476, mesh aerialtop  (77,790)-(161,918)
    0x2f  panel (258,  60)-( 720, 260)    0x30 (458,273)-(720,363)   0x31 (728,109)-(831,211)
                                          0x32 ( 37, 58)-(242,262) mesh i_dollar
    0x33  panel (1688, 48)-(1963, 278)    0x34 gkey (1853,165)-(1955,268)  0x35 gtick (1853,54)-(1955,156)
                                          0x36 (1688,171)-(1829,261)  0x37 (1688,60)-(1829,150)

### How `FUN_004a1d70` populates the clusters

| Control | What the builder does | Evidence |
|---|---|---|
| `0x36`, `0x37` | **Text labels**, `LabelSkin_Construct`, colour **white** (0xff,0xff,0xff,0xff), font `FUN_00485a70(2)`, both seeded with the string at `0x00752f24`. They sit to the LEFT of the two icons, so the cluster is icon+count twice | Disassembly of `FUN_004a1d70` |
| `0x34` (`gkey`) | Takes **frame 3** (`FUN_0065d3a3(3)`) — what the four `gkey0-3.wct` textures are for — and **is hidden outright in Instant Action** (`DAT_00fb3b7c == 2` -> `UI_SetVisible(0)`) | Disassembly |
| `0x36`, `0x37`, `0x35` on the gadget's update (`0x004a1092`..`0x004a115b`, Q591) | `0x36` takes the keys held (`PlayerProgress_CountKeys` `0x005af680`), written only when that count changes (the last at `0x00752ec8`) and not at all in Instant Action (`0x004a1002`): at nought `0x36` and the key `0x34` are hidden (`0x004a1045`..`0x004a1067`), else both shown and `0x36` reads `"%d  x"` (`0x00752ef8`, two spaces between the number and the x). `0x37` takes the tickets **less those spent** (`FUN_005af5d0`: every ticket, the two secret ones too, less `+0x1c`), written only when that count differs from the last written (`0x00752ec4`, `0x004a10d2`), which is -1 in the image and is put back to -1 by the gadget's message 0x15 (`0x004a1160`, with `0x00752ec8`); so -1 is never the first count written (a later -1, or a -2, is written as any count) and a profile that spent one more than it won keeps `FUN_004a1d70`'s seed, `"0  x"` (`0x00752f24`), the ticket shown (Q603: `q707/orig/O2/g-05.png`; this game `q603/run1-F/F-corner.png`). Once written: at nought `0x37` and the ticket `0x35` are hidden, else both shown and `0x37` reads `"%d  x"` (the same string). In the original's Full Simulation jungle (Alexah's profile less two jungle tickets, one spent) the corner read "1 x" and "1 x" before the award of tick 19100 and "3 x" and "2 x" after (`q706/orig/O/g-05.png`, `g-40.png`) | Disassembly; the original's frames |
| `0x30` | **Yellow** (0xff,0xff,0,0xff), font 2, and **starts hidden** (`UI_SetVisible(0)`) — matches it being the price of the item in hand, absent when your hand is empty | Disassembly |
| `0x2f` | Its field is sized by **measuring the string `999999999`** (`UI_MeasureText` on the string at `0x00752f10`), white, font `FUN_00485a70(1)`, skin `FUN_0048fde0(0)`; `0x32`'s rect is then computed from that measurement rather than being fixed | Disassembly |
| `0x2d` / `0x2e` | Callbacks `LAB_004a11e0` and `FUN_004a1190`; the mast is shrunk to eight units and the top follows it down ("The arm and the aerial as built" below) | Disassembly |
| `0x1f` | Takes the `meter.wct` skin | Confirmed twice, independently |
| `0x20` | The date is **black** — `FUN_0065c5d5(0,0,0,0xff)` with font slot 3 (`_DAT_007cb2d4 = 3`) | Confirmed twice, independently |

### The happiness gauge's two quads (Q244; fork watch 2026-10-19, verify-advisor-ui.md L3, verify-other-b.md M3)

The `meter.wct` skin of control `0x1f` is drawn by a sub-object built by `Meter_Construct` `0x00477870` (vtable `0x006fee88`,
slots `+0xc` `Meter_OnMessage` `0x00477db0`, `+0x24` `Meter_SetQuadGeometry` `0x00477cc0`, `+0x28` `Meter_SetQuadUVs`
`0x00477d20`). Message 8 takes the value `x 1/1024 (0x006fee08) x 100 (0x006fee0c)`, held 0 to 100, as p, stored at `+0x120`
only when it changed, then rebuilds the geometry and the UVs. Eight vertices in two quads that share the boundary p: quad A
(0,100) (100,100) (100,p) (0,p), quad B (0,p) (100,p) (100,0) (0,0); indices (0,1,2) (2,3,0) (4,5,6) (6,7,4). UVs (t = p/100):
quad A **u 0.5 to 1.0** (the texture's right half), v t to 1.0; quad B **u 0 to 0.5** (the left half), v 0 to t. v runs the
same way as y on both, so each half is **cropped at the boundary, never scaled**. The constructor builds the geometry with
p = 0, so before any value and at nought quad A, the dark half, covers the whole box.

**The space's y of 0 is the control's bottom edge, and the bar fills upward** (settled 2026-10-19, Q244): the message's
call that places the space (`FUN_00476c50`) is handed the control's bottom as its origin, and the texture decides it
without that read. `meter.wct` (`ui.wad`, `textures\meter.wct`, type 0x13, 128 x 128) is two capsules side by side, each
with a yellow smiley at its top and a blue sad face at its bottom: the left half lit (blue, grey, orange, yellow from the
bottom up), the right half black with a white gloss. The original's frames show the lit half below the boundary and the
dark half above it, so quad B, y 0 to p, is the bottom. Measured in both games on three files whose every guest is at one
happiness (`q244/`, `lib/gaugesplit.py`): the original's box `(42,550)-(72,662)` of the 1024 x 768 frame (the stream's
`(85,1100)-(144,1324)` of 2048 x 1536) splits at 67.9%, 28.6% and 0% for 70, 30 and 0, its clock running on through the
load; OpenTPW's at 69.5%, 29.5% and 0% (paused on 766, the meter at 706, 296 and 0 of 1024; `q244/side-by-side.png`).

**What the gadget hands it** (`Gadget_GaugeOnMessage` `0x004a1cd0`, timer `0x80083` every 2000 ms): the mean of
`FUN_004c7bb0` times 1024.0f (`0x006ffa34`) times 0.01f (`0x006ffa38`), `__ftol`, so 50 is 511 and 100 is 1023 at 53-bit
precision, 0.01f being a shade under a hundredth; at 24 bits the products round up first and 50 is 512 and 100 is 1024
(70 is 716 at either). Which precision the timer message runs under is not read (`park-engine.md`, "Which rounding is
live"); ours is the 53-bit answer. **The mean is a float**: each guest's happiness `__ftol`ed and masked to a byte is summed as a
whole number, and the sum is divided by the count with `FILD`/`FIDIV` (`0x004c7c96`), so 657 over 13 is 50.54 (517 units),
not 50.

**OpenTPW** (`UiMeter.Quads`, `Percent`; `ParkGadget.GaugeUnits`): the two quads as above, the dark half drawn at nought,
`Max` 1024 and the units as the gadget's. `ParkPeople.AverageHappiness` still hands it a whole mean of every guest, where
`GuestsInsideHappiness` is the exact routine (Q459, Q148). The log line `UI: meter 0x1f at <units> of 1024,
the boundary <p>% up` on each change. **Not settled:** the faces show sharp in this game and blurred in the original's
frames (the eyes and mouths gone, the colours not darker, the gadget's other pictures crisp), the mark of how the texture
is sampled when minified, its filter and mip levels (Q29, Q519), not checked; the type-0x13 pixels of `meter.wct` are not
checked against the original's codec either (`wctorig.py` does not decode them).

### Depth

`FUN_0065f16b( this, n )` writes n to the control's `+0xe0`, clamps at `0x100`, and recurses into children
at n+2. The receivers are `__fastcall` and **Ghidra hides them** — read the disassembly:

    0x21 arm 4   0x22 end 4   0x24 b_retract 7   0x1d body 8   0x23 handle 9   0x25 six buttons 10

**Higher is nearer the front**, settled by screenshot: the six buttons sit at 10 inside a body at 8 and
plainly draw over it. So the arm draws **behind** the body, and the handle in front of it. Built the other way
the arm lays a hard seam across the panel. A control linked later takes its parent's depth and two when the caller asks for it (the second argument, `0x0065f4d6`)
(`FUN_0065f447`).

### The arm and the aerial as built

A control's flags `0x10000`, `0x20000`, `0x40000` and `0x80000` make it follow its parent's left, right, top and
bottom edge when the parent's rectangle is set to another size (`FUN_0065cc04` → `FUN_0065c9bd`, which moves the child
by that edge's change through `FUN_0065c7fe`; `0x30000`, `0xc0000` and `0xf0000` together stretch it instead).
`FUN_0065c7fe` moves a control's regions and then every child the same way. In the gadget's stream the arm's end
`0x22` and the handle `0x23` carry `0x20000`, and the aerial's top `0x2e` carries `0x40000`.

**The arm is built in.** `FUN_004a1d70` reads the arm's rectangle and the handle's, adds the arm's left less the
handle's (331 − 976 = −645) to the arm's right (`0x004a23da`) and sets it (`0x004a23e2`): the arm is
(331,1069)-(361,1495), 30 wide, and its end, following the right edge with the handle inside it, sits 645 left of the
stream: the end (361,1069)-(426,1382), the handle (331,1058)-(484,1503) with its 16 points moved as far. The handle is
at depth 9 over the body's 8, so with the arm in it is the blue grip that shows past the body's right edge (439);
every point of the narrowed arm and of its end is inside the body's outline or the handle's. The retract button `0x24`
(flags 1) stays at its stream rectangle, switched off.

**The aerial is built down.** The builder sets the mast `0x2d`'s top to its bottom less 8 (`0x004a2057`,
`0x004a205c`): (94,976)-(144,984). The top `0x2e` follows the mast's top edge, 58 down: (77,848)-(161,976), resting on
the body's neck. (The move at `0x004a2034` before it, the mast's top less the top's bottom, is nought in this stream.)
`LAB_004a11e0`, the mast's handler, answers `0x100001`, a message's icon handed to it: it links the icon as its child,
posts it message 6 with 1 to arrive 500 ms later (`FUN_00658df4`, `0x004a1292`), and grows the mast upward by the icon's height and 10 (`FUN_0065cd95( 0, -(h + 10), 0, 0 )`,
`0x004a12e3`), which lifts the top as far. `FUN_004a1190`, the top's handler, answers a right click (`0x10006`, button
1) by sending the mast `0x100003`, and the mast's `0x100003` arm (`0x004a13b7`) walks the icons from `[0x007cb2c8]`,
posting each message 5 (close) with a delay 100 ms longer than the last one's: "Right-click to delete ALL messages", help row 476. Seen in the original:
with no message the red top sits on the neck; with one, an icon block stands between them.

**The hit test's order.** `FUN_0065db25` skips a control flagged `0x2` or with `+4` set, and one with flag `0x1` clear and none of `0xf0000000` set (`0x0065db55`), tests every child in turn
whatever the parent's own region says, keeps the LAST child that answers, and answers itself only when no child did.
Children are linked at the tail (`FUN_0065f447`), so in stream order. Under the body: the gauge `0x1e`, the date
`0x20`, the arm `0x21`, the six buttons' panel `0x25`. So the panel answers before the arm where they meet, the arm
before the date (the date's x 331 to 361 with the arm in), and any child before the body's outline.

**Measured in the original** with the path tool armed and RMB cancel on (`[0x0078d911]` 1), reading the mode object
`[0x007b05d8]`, which a right click that reaches the park replaces: a quick right click on the body's bare metal, the
red top, the mast and the handle past the body's edge left it alone, each tried once or twice; one on the grass, and
one in the corner of the body's rectangle outside its outline, replaced it, four of four. Over the red top the help
bar read row 476. The arm out was not tried there.

**OpenTPW.** `ParkGadget` builds the body's children in that order under a body that draws by depth
(`UiControl.DrawsByDepth`, `Depth`), reads both outlines into `UiControl.Outline`, and builds the arm in
(`BuildIn`) and the aerial down. The arm, its end, the mast and the buttons' panel stop the pointer over their
rectangles (`UiControl.StopsPointer`). The arm slides ("The arm's handler", Q427). Not the original's: at rest in, the arm
and its end draw nothing, where the original draws them fitted to their narrowed rectangles behind the body and the
handle (the film of Q427 shows the arm's mesh squeezed to its rectangle through a slide), and the retract button is
hidden at rest in; through the slide in it is drawn switched off, as the original's, but stops the pointer, doing
nothing (and starts its glint and help row 481 for the 0.22 s, Q66), where the original's hit test passes over it. A carried panel is at depth 3, its parts at 5 and theirs at 7
(Q445, "The arm's handler"): behind the body, and its parts behind the retract button. A tag stands
on the mast and lifts the top (`ParkGadget.PutTags`), and the top's right click takes every tag off.

### Op 4 sub-op 4 is a hit-test region, not a motion path

All four sub-ops end in `FUN_0065cfff(obj)` after building a shape — sub-op 1 from a rect, sub-op 4 from
n points via `FUN_0066c4a1(n, pts)`. So `handle`'s 16 points and the gadget body's 23 are **click masks**.
Reading them as a path would have animated the arm along nothing.

### The six round buttons, ids 0x26-0x2b

Handler **`FUN_004a0840`**:

| Id | Mesh | Help row | Dispatch |
|---|---|---|---|
| `0x26` | `b_buy` | 469 | `FUN_004a0940(1)` |
| `0x27` | `b_camera` | 474 | press `FUN_00498bb0` / release `FUN_00498bd0` (shows the sub-panel) |
| `0x28` | `b_info` | 470 | `FUN_004a0940(2)` |
| `0x29` | `b_map` | 473 | `FUN_005f0b40` (the map screen) |
| `0x2a` | `b_money` | 471 | `FUN_004a0940(3)` |
| `0x2b` | `b_resrch` | 472 | `FUN_004aa480` (research) |

**The HUD is six buttons, not 17, and three of them are category pickers.** `FUN_004a0940( n )` opens
*the screen that category was last left on*, from three globals seeded 1 / 3 / 10 in `FUN_004a1d70`:

    n=1  DAT_007cb28c  1 -> buy FUN_004acc70      2 -> hire FUN_0049bdd0
    n=2  DAT_007cb2a0  3 -> parkstatus  4 -> allstaff  5 -> allitems  6 -> allpeeps
    n=3  DAT_007cb290  7 -> financeinfo  8 -> loans  9 -> staffcosts  10 -> entryprice

It does nothing at all when `FUN_0048c8d0()` returns 1, which needs **both** `DAT_007c2534` non-zero
**and** `FUN_006ad810()` non-zero - that is, while the game menu exists and is on screen (see "The gate that looks
like a bug, and is not"). This is why the 17
shortcut actions and the gadget's button count never reconciled — they are not meant to.

**Who writes the three globals (Q330).** Besides the seed (`0x004a1db4`) their one writer is
`FUN_004a0810( category, screen )`, and its ten callers are the ten screens' openers, each for its own screen. The
buy opener's is its first act, `(1, 1)` at `0x004acc8f`, before it looks whether its screen is up; the hire
opener's is `(1, 2)` at `0x0049bdf5`, after `FUN_00485b40` has destroyed the park screen that is open. So a
category is left on a screen whoever opened it: the sibling's button, the message bar's, the gadget's own. The
hire opener asked for -1 always builds, so the gadget's button with the hire screen up destroys it and builds it
again, which withdraws `0xbd` and posts it again (a cut while he says it, and the line said again); the buy
opener with its screen up picks its tab again and no more.

**The seed is the entry from the front end and nothing else.** `FUN_004a1d70` is called by `FUN_00489f50`, that
by `FUN_004813e0`, and that by `Game_StateMachine` at `0x0054ed25`, state 9, alone. A park loaded from the
park's own Load Park list keeps what the park before it left, and so does Restart Park: its confirm `FUN_0048b5b0`
asks `FUN_005508b0( 1 )`, served by states 0xd and 0xe, which load `restart.INTS` over the running park and go back
to state 10 without passing state 9 (`saves.md`, "Restart Park: restart.INTS").

Measured in the original, the stock file in four fresh programs, each predicted first (`q330/PREDICTION.txt`;
`q330/orig/run.sh <file> a|b|r|l`, `original/screens.py` beside `advisor.py`; nine of ten, the miss Restart
Park's, predicted again and run again):

| What was done | `DAT_007cb28c` | Screen up |
|---|---|---|
| the park entered | 1 | none |
| the gadget's button | 1 | buy |
| the buy screen's button to hire | 2 | hire; `0xbd` (55, 0, 0, 1) to (891, 0, 1, 1) |
| the gadget's button 4 s into his line | 2 | another hire screen (`0x46cee10` to `0x46cedf0`); `0xbd` (891, 0, 0, 2) on tick 919, (920, 0, 1, 2) on 920 |
| exit, the gadget's button | 2 | hire, nobody speaking (three cuts) |
| the hire screen's button to buy | 1 | buy |
| exit, the gadget's button | 1 | buy |
| hire, exit, the same file loaded from the list, the gadget's button | 2 | hire |
| hire, exit, Restart Park (the clock back from 916 to under 882), the gadget's button | 2 | hire |
| hire, exit, Exit To Lobby, the park entered again, the gadget's button | 1 (written as the clock read 0) | buy |

**OpenTPW** (`ParkCategoryScreens`): `ParkBuyScreen.Shown` and `ParkHireScreen.Shown` remember their screen,
the gadget's button opens the one remembered (`Open`), and `Game` seeds the three as a park is asked for from a
scene that is not a park (`ParkAsked`). Information is still seeded 4, the declared deviation. The stock park,
real clicks, predicted first (`q330/confirm.py fix|control|restart`): nine of nine and the control. `windows`
ends `; left on 1,4,10`; the button logs `Park gadget: category 1 is left on screen 2` after the hire screen
was up; clicked 3 s into his line with hire up, `withdraw message=189 cleared=0 cut=1 slaps=2`, a post on tick
815 and `attempt=3 message=189 ... tick=816`; left on hire it opens hire after `savepark` and a load from the
list, and after Restart Park; after `lobby` and the park again `left on 1,4,10` and it opens buy. Main's build
opened buy after hire and logged no such line. Sixteen bugs put back, sixteen caught
(`q330/mutations.py`); the one line no test reaches is `Game`'s own call, which the load, restart and lobby
runs hold. Not run: Full Simulation; the Information and Finance categories in either game (their four built
screens remember through `ParkCategoryScreens.Show` alone, so the message bar's entry-price button does, and
their seed is not put to the original); a key that opens a screen.

### The nine screens behind Info, Money and Research — censused 2026-09-21

Every one located, with its builder, its layout stream, its refresh callback, the size of the builder
and the control count parsed out of its stream. **The control counts are LOWER BOUNDS**: the stream
walk misses children of composite controls (a list's own column headers never appear).

| # | Screen | Builder | Stream | Callback | Builder bytes | Controls |
|---|---|---|---|---|---|---|
| 3 | parkstatus | `FUN_004a52a0` | `0x7537d0` | `0x4a4830` | 2926 | 28 |
| 4 | allstaff | `FUN_00496620` | `0x750e10` | `0x495da0` | 1438 | 37 |
| 5 | allitems | `FUN_00495aa0` | `0x7508e0` | `0x495290` | 342 + four sub-builders | 12 |
| 6 | allpeeps | `FUN_00493530` | `0x7506c8` | `0x493230` | 677 | 5 + 6 columns |
| 7 | financeinfo | `FUN_0049ac60` | `0x751ca8` | `0x49a0b0` | 2238 | 19 |
| 8 | loans | `FUN_0049fb30` | `0x7525a0` | `0x49f4e0` | 1905 | 18 |
| 9 | staffcosts | `FUN_004b2750` | `0x756400` | `0x4b24b0` | 2990 | 30 |
| 10 | entryprice | `FUN_00498d80` | `0x751798` | `0x498c60` | 721 | 9 |
| — | research | `FUN_004aa480` | `0x754490` | `0x4a96b0` | 2616 | 41, six sliders |

What each shows, from its own decoded labels:

- **parkstatus** — People in park / Arrival rate / Average happiness / Average time in park / Park
  rating, Top 3 Thoughts, Happiness Levels (`hap.wct` and `sad_undec.wct` gauges), Total park
  visitors, and a 1/3/12-year graph.
- **allstaff** — a five-column list: Name / Current Status / Monthly Wage / Skill / Happiness, plus
  average-happiness gauges.
- **allitems** — four tabs of placed objects, the same four categories the buy screen uses.
  **The four tabs do NOT share a list shape** — see below.
- **allpeeps** — a six-column guest list: Visitor Number / Cash Remaining / Time In Park / Rides
  Ridden / "?" / Happiness. Its row adder `FUN_00493800` fills them from the guest's `+0x1d8`, cash `+0x1a0`, a
  quarter of the park hours since the guest's arrival stamp `+0x1d4` (`mArrivalDate`, `mGameTick` at construction,
  `0x004fafdb`; `FUN_004fd950` shifts the ticks since right by two before `FUN_004f8730` turns them into park time,
  divided by 36,000,000,000 at `0x0049383e`),
  `mNumRides` `+0x1c4` (`0x00493850`), the last thought `+0x30` less one or 999 for none (`0x0049385a`), and happiness
  as a 0..1024 bar (`0x00493870`).
- **financeinfo** — Bank balance / Park value / Money in / Gate takings / Shop takings / Sideshow
  takings / Money out / Staff costs, over the same year graph.
- **loans** — "Available Loans": Lender Name / Amount / Interest Rate / Monthly Repayment.
- **staffcosts** — "Staff Training Budgets": Cash in / − Staff costs / − Other costs / − Loan
  payments / Balance.
- **entryprice** — one "Ticket Price" row. It writes the bank's `+0x118` (the thing `FUN_005195d0` answers, not the park object) through
  `FUN_004d05d0`, whose format string reads *"Admission fee set to %d"*. The setter does that and no more
  (`0x004d05e8`); the fee's two readers each take `+0x118` as they run, a guest's judgement (`FUN_004ff5b0`,
  `0x004ff602`, logged as *"GP: %d, AF: %d"*) and the charge (`FUN_004d0600`, `0x004d0609`), so a new price is the
  next judgement's, a sulking guest's second one included. OpenTPW's gate reads the running park's fee the same way
  (`ParkAdmission.Fee`, Q145).

### The four built screens, and what walking their streams alone could not give

**Column headings are in CODE, not in the layout stream.** Every `op 0xc` header child carries a rect
and nothing else; each builder then fetches it by id **`0x10 + index`** and hands it the text of a UITEXT row
(`FUN_00485b00( row )`) through `FUN_00486660` ("A list's order"). A stream walk therefore yields the right number of
unnamed boxes and no labels at all. The rows, read back from `UITEXT.str`:

| Screen | Header ids | UITEXT rows | Headings |
|---|---|---|---|
| allstaff | `0x10`–`0x14` | 101–105 | Name / Current Status / Monthly Wage / Skill / Happiness |
| allpeeps | `0x10`–`0x15` | 113–118 | Visitor Number / Cash Remaining / Time In Park / Rides Ridden / **`?`** / Happiness |
| allitems, rides | `0x10`–`0x14` | 82–86 | Name / Users Last Month / Excitement / State Of Repair / Remaining Life |
| allitems, shops | `0x10`–`0x14` | 87–91 | Name / Customers Last Month / Profit Last Month / Total Profit / Customer Satisfaction |
| allitems, sideshows | `0x10`–`0x15` | 92–97 | Name / Customers Last Month / Excitement / Profit Last Month / Total Profit / Customer Satisfaction |
| allitems, misc | `0x10`–`0x11` | 98–99 | Name / Number Owned |

**UITEXT row 117 is literally `"?"` in the shipped file.** The fifth visitor column has no heading in
the original either; it is not a decode failure.

**allitems loads THREE different list trees for its four tabs**, through four sub-builders that
`FUN_00495aa0` dispatches on the tab index:

    case 0 rides      FUN_00494ec0 -> tree 0x750ab8   5 columns
    case 1 shops      FUN_00494c70 -> tree 0x750ab8   5 columns (the same tree)
    case 2 sideshows  FUN_00494250 -> tree 0x750ba0   6 columns
    case 3 misc       FUN_00495110 -> tree 0x750ca0   2 columns

`FUN_006636b2( column, type )` sets each column's type, 0 text and 1 a number, which is what the list sorts it as
(`FUN_00663edc` reads it back through `FUN_006636f6`) and what the screens here take the alignment from: allstaff
passes 0,0,1,1,1 and allitems 0,1,1,1,1, but **allpeeps passes 1 for all six** — its name column is a visitor *number*.

**Tab ids are not in screen order.** allitems' switch takes case 1 to `0x12c4bc` (at x 1521–1623) and
case 2 to `0x12c4bb` (at 1402–1504), so laying the tabs out as a stride from the first one puts shops
and sideshows in each other's places while looking correct.

**`FUN_004a0810( category, screen )` is the seed setter**, called at the top of every builder —
`(2,4)` in allstaff, `(2,5)` allitems, `(2,6)` allpeeps, `(3,10)` entryprice. It confirms the
1 / 3 / 10 seeding above from the other direction.

**entryprice is not what its rects suggest.** Its three stacked right-hand buttons are **not** a
spinner: they resolve to `b_staffcost`, `b_loans` and `b_finance`, and `FUN_00498c60` sends them to
`FUN_004b2750`, `FUN_0049fb30` and `FUN_0049ac60` — this category's other three screens. The spinner
is `0x4f3ae`, control **type 12**, whose `op 0x0f` child is `b_minus` (left) and `op 0x0e` child is
`b_plus` (right); `FUN_0066a5c4(&0,&10000)` gives it a range of **0–10000** and `FUN_0066a68d(&1)` a
step of **1**. `0x4f3b0` is the black "Ticket Price" label (UITEXT 160), and `0x4f3b1` is `b_door`,
the park open/closed switch — `FUN_00519ef0( down != 1, 0 )` (`0x00498d33`), whose first argument is the OPEN
flag, so **down is closed**; the builder sets the switch down for a closed park (`Button_SetDown`, `0x00498fd9`),
once, when the screen is built. What the door does to the rides: `ride-operation.md`, "The closed ride". The fee is held in a global as the spinner moves (message `0x800`) and written on
dismissal (message `0x14`).

**allstaff's kind → happiness-label switch is deliberately out of order**: case 0→`0x6b`, 1→`0x6c`,
**2→`0x6e`**, **3→`0x6d`**, 4→`0x6f`. The string file lists guards (109) before entertainers (110)
while the kinds run cleaners, mechanics, entertainers, guards, scientists. Written as a plain 107–111
run, the guards tab draws a list of guards under the heading *"Entertainers' Happiness"*.

**Both list screens arm a 2000ms refresh timer** — `FUN_0065ef90( 0x80083, 2000 )`, the same timer id
the gadget's happiness gauge uses. Rebuilding such a list every frame instead is measurably wrong,
not merely untidy.

#### Resolving a mesh hash to a file name

The layout stream names a mesh by `h = (c ^ h) * 47` from zero over the model's **first node name**,
which is often not the file's name. Hashing every stem in `ui.wad` and matching resolves them — but
**only `.md2` entries are candidates**, because `UiMesh.Get` loads `ui/<name>.md2`. Matching against
all 1202 entries instead of the 278 models picks up textures and gives names that load nothing:
`b_pkinfo` and `b_sresrcher` are both `.wct` stems whose models ship as **`b_parkinfo.MD2`** and
**`b_sresrhcer.MD2`**. Validate the arithmetic against a known pair before trusting any match —
`b_buy` → `0x2135e13d` and `base` → `0x1beb0695`.

Resolved this way: `list_allstaff`, `list_kids` (allpeeps), `list_all` (the allitems tab group),
`b_finance`, `b_loans`, `b_staffcost`, `b_door`, `b_plus`, `b_minus`, `b_parkinfo`, `b_kids`,
`b_allstaff`, `b_allthings`, `b_sguard`, `b_sresrhcer`.

**The screen ROOT FRAMES resolve too.** Hashing file
stems leaves them unmatched, and scanning the models' raw bytes finds nothing either — every one of
ui.wad's 278 `.md2` members is **refpack-compressed**, so a byte scan reads compressed noise. Decompress
them (the tree's own `WadArchive` + `ModelFile` do it) and read the node names, and all three fall out:

| Hash | Node | File | Used by |
|---|---|---|---|
| `0xf76e4200` | `window4` | `w_big.MD2` | buy, hire, allstaff, allitems, allpeeps |
| `0xf76e42eb` | `window1` | `w_small.MD2` | entryprice |
| `0x257b71f9` | `varibox` | `f_varibox.MD2` | the entry-price spinner |

The two root hashes differ by `0xEB` = 235 = **47 × 5**, which is the signature of two names sharing
every character but the last — `window4` against `window1` — and that was visible before either was
identified. The ride window's `window2` in `w_med.MD2` is the third of the same family.

**A caution about every debug string quoted on this page and the next.** `FUN_005da3c0`, the logger
they are all handed to, is an **empty stub in the shipped build** — `void FUN_005da3c0(void) { return; }`.
So none of these lines is ever printed at runtime. They remain first-class evidence of **field names
and intent**, which is what this project uses them for and why they are quoted verbatim; they are
**not** evidence that the game emits anything, and "the original logs X" is wrong as a statement of
behaviour.
- **research** — six effort sliders. `FUN_004aa480` opens it only with the game menu down (`FUN_0048c8d0`) and the
  screen not already open (`DAT_007cc1d8`), and opens a message box instead in two cases: UITEXT `0x1d4` (*"Research is
  automatic in Instant Action mode."*) when the game type `DAT_00fb3b7c` is 2, and UITEXT `0x1d3` (*"You need to hire
  some scientists before you can carry out any research!"*) when `mFirstResearcher` (`+0x1da742`) is nought.

**Five are built and four are blocked on simulation this project does not have.** Built: **entryprice**,
**allpeeps**, **allstaff**, **allitems** and **staffcosts** ("The Staff Training Budgets screen", below). Blocked: **loans** (taking and repaying a loan: `ParkWorld.EconomyState.Loans` reads the save's `mLoans[]`
records, `ParkState` keeps them and the month's change pays the bought ones' instalments (`TurnTheMonth`); taking one is not built), **financeinfo** (the
money-in/out split and the graph history) and
**parkstatus** (Top 3 Thoughts, arrival rate, park rating, multi-year history), each counted when opened
(`LOANS_SCREEN`, `FINANCE_SCREEN`, `PARK_STATUS_SCREEN`), and **research** (`mResearchDone`,
`mResearchGroup`, `mFirstResearcher`, and per-group research points), whose screen is not built. The key reaches the
same function: the shortcuts' row 11 is `R` (`0x52`) with no modifier, handler `0x0040c5b0` (`MOV ECX, 0x007b51f0; CALL
0x004817e0`, a `JMP` to `FUN_004aa480`), heard where that table is run: over the park and under a park screen, not
under the game menu, a message box, the options, the map, the full-screen view or first person.

So the three buttons are not one job: **Info has 3 of 4 built, Money 2 of 4, and Research its two boxes and not its
screen.**

### The research opener and its two boxes (Q410)

`FUN_004aa480`, decompiled 2026-10-16, is what the gadget's Research button (`0x2b`), R and the message bar's button of
type `0x13` all run. In order: nothing with the game menu up (`FUN_0048c8d0`) or the screen open (`DAT_007cc1d8`); the
park screen that is open closed (`FUN_00485b40`); the mode object made once (`FUN_00550ca0`, behind `DAT_00fb34be` bit
1); then, when the game type `DAT_00fb3b7c` is 2 (Instant Action), `MessageBox_Open( &text, 2, 0, 0, 0, 0, 0, 0 )` with
UITEXT `0x1d4`, "Research is automatic in Instant Action mode.", white (`ff ff ff`) in font 4, and return; when
`mFirstResearcher` (`+0x1da742`) is nought, the same box with UITEXT `0x1d3`, "You need to hire some scientists before
you can carry out any research!"; else the screen's tree (`0x00754490`, `FUN_004a96b0`), its sounds `0x9a` and `0x9b`,
the effort gauge and the six sliders. `MessageBox_Open`'s second argument is the bottom place's button (`0x9873c8`; 1
`b_okay`, 2 `b_exit`, 0 hidden) and its third that button's act, and the same pairs follow for `0x9873c9` and
`0x9873ca`: here the cross alone, with no act, which the box's handler `FUN_0047eed0` answers by closing the box. The box
pauses the park (`g_ParkRunning`) and holds the key tables off (`FUN_00486b70`) until it closes.

**Built** (`ParkResearchOpener`, `MessageBox` with no tick): both boxes, at all three callers; the bar's type `0x13`
answers 2, the tag taken off. The screen behind them is built (Q590, "The research screen's panels"). In the running game (`q410/run-fix2`): R over the
stock park logged `Park: research opener: Instant Action, so the box of UITEXT 0x1d4`, the box up with its cross alone
(`1-r.png`) and the park's clock at tick 769 for 3 s under it; the gadget's button the same; B then R closed the buy
screen and opened the box. A fresh Full Simulation park (`run-fs1`) gave the `0x1d3` box, and with a scientist hired
`RESEARCH_SCREEN` counted and no box. The original given the same park (`q410/orig/a-sheet.png`) showed the same box by
R, by the button and over the buy screen, its cross at (714, 326) of 1024 x 768, the bottom place.
In no game run: the bar's type `0x13` (its rows 76 and 80 score only in Full Simulation; the test holds it), a carried
hire that is a researcher, the other three themes.

### The research screen's workload slider and its red text (Q245)

Fork watch 2026-10-18, `verify-ui-hud.md` U5 and U2, read in `FUN_004aa480`'s listing. With `c` = `FUN_004c9e60` (the sum
of `ResearchAbility` over the researchers, `0x0078545c + 12 x grade`) and `most` = `FUN_004c9ed0` (`[0x007852f8]` x
`[0x0078548c]`; `advisor-park.md` has both), the screen sets
`limit = ((((100 - c) / 3) + c) << 10) / most` (`0x004aacce`-`0x004aacf9`) on the child `0xd2d12`, and the slider
`0xd2d10`'s knob to `(workload - 70) x limit / 30` (`0x004aad01`-`0x004aad17`; `workload` is the lab's `+0x1460`; the
division is by 30 through the magic `0x88888889`). The limit is kept at `[0x007cc1c0]`. The inverse, for a drag
(`0x004a99ea`-`0x004a9a17`), clamps the slider's value to the limit and writes `workload = 30 x value / limit + 70` through
`FUN_00503900`. So the workload runs 70 to 100 along the slider's travel up to the limit. Five calls to the text-colour
setter `0x0065c5d5` in `FUN_004aa480` pass (224, 86, 86, 255): all five of the literal calls of that colour in the 134 the scan found; they colour the five panels' project labels (below). The ball's travel (`0x0066b088`, `0x0066b771`,
`0x0066bb9b`) is not read.

### The research screen's panels (Q704)

Read in the private Ghidra copy (lane B, 2026-10-18): the opener `ResearchScreen_Open` (`0x004aa480`), the handler
`ResearchScreen_OnMessage` (`0x004a96b0`), the five panels' procs and the lab's getters below; the stream `0x00754490`
walked (`lib/uistream.py`, `q704/stream.txt`), its sixteen meshes resolved (`meshhash.py`) and its help rows read
(`UIHELPTEXT.str` 238, 244 to 260). Nothing built and neither game run: the screen opens only in Full Simulation.
The advisor's own line for it (below) describes the same screen.

**The stream.** Rects on the 2048 x 1536 layout. The frame `0xd2cff` (186, 30)-(2018, 1032) is `w_res` (the node
`research_wind`); the title `0xd2d00` (866, 67)-(1338, 146) takes UITEXT 184, "Research Lab" (message `0x15`). Five
panels, one a kind, each (785, y)-(1830, y + 150 or 151, the stream's own), a `res_panel` (node `research`) skinned `res_pipe` (node `pipe`):

| Panel | y | Kind | Its slider | Slider help | Icon `0xd2d02` | Arrows' help | Proc |
|---|---|---|---|---|---|---|---|
| `0xd2d01` | 158 | 0 rides | `0xd2d0e` | 244 | `h_ride` | 249, 250 | `0x004aa0c0` |
| `0xd2d07` | 318 | 4 upgrades | `0xd2d0f` | 245 | `h_upg` (node `h_upgrade`) | 251, 252 | `0x004aa300` |
| `0xd2d08` | 479 | 1 shops | `0xd2d0d` | 246 | `h_shop` | 253, 254 | `0x004aa360` |
| `0xd2d09` | 640 | 2 sideshows | `0xd2d0c` | 247 | `h_show` | 255, 256 | `0x004aa3c0` |
| `0xd2d0a` | 801 | 3 miscellaneous | `0xd2d0b` | 248 | `h_feature` | 257, 258 | `0x004aa420` |

Inside each panel: the icon `0xd2d02` (type 6, x 816-919), the arrows `0xd2d03` `b_sleft` (x 924-1007) and `0xd2d04`
`b_sright` (x 1012-1096), the bar `0xd2d05` (type 9, x 1132-1497, help 259, "Name of item being researched / The coloured
bar indicates time remaining until complete") and the pump `0xd2d06` (type 6, `res_fluid`, node `fluid`, x 1555-1712,
help 260, "Research pump - its speed indicates how fast the research is progressing"). The five effort sliders sit
left of their panels at x 472-769 (type 3, flags `0x61`, a `b_scroller` ball on a `res_slider` track, node
`effortslidea`), range 0 to 100, each set at the opening to its kind's effort (`Lab_KindEffort`, `0x00503850`). The
workload slider `0xd2d10` (234, 148)-(464, 968) (flags `0x51`, `res_mainslider`, node `effort`, help 238, "Scientists'
work rate") holds the dial `0xd2d11` (`res_faces`, node `cameo`) and the gauge `0xd2d12` (type 9, `effortguage.wct`);
its arithmetic is the section above. The close button (1889, 844)-(1972, 927) is `b_okay` (id -1, help 1).

**The bar.** `ResearchScreen_SetFill( kind )` (`0x004a9bd0`) sets the panel's `0xd2d05` to `(100 - left) x 1024 / 100`
of 1024, as the skill bar's, where `left` is `Lab_PercentLeftOfDearest( kind )` (`0x00503ac0`): the points left in the
kind's current project **as a per cent of the dearest project's cost** (`mMostExpensiveProject`, `+0x1464`),
`(mCost - mResearchDone) x 100 / dearest mCost` in the FPU, cut to a whole number by `__ftol`; nought with no current
project, a cost of 0.1 or less (`[0x007007e8]`), or a dearest cost of 1.0 or less (`[0x007007f0]`). So the bar fills
as the project goes, a cheap project starts nearly full, and a kind with nothing left reads full. The original's own
bytes answer so under unicorn (`q704/fill.py`, `lib/exeleaf.py`): 300 of a dearest 1000 with nothing done is 30 left,
716 filled; 333 is 33; the dearest itself one point in is 99, 10 filled. On Alexah's fresh Full Simulation jungle
(`restart.INTS`, dearest project 3 at 1300; `q704/fslab.out`) the bars open at rides (Dino Karts) 552, shops (1212) 675,
sideshows (1304) 481, miscellaneous (Stone Head) 829 and upgrades (1100's tier 1) 829. The bars are set at the
opening, on each tick of the screen's timer (message `0x10`; it arms `0x80080` at 1000 ms on `0x15`, as the buy screen
does), after a workload drag, after an arrow and when a kind finishes (`Research_OnKindFinished`).

**The bar's text.** `ResearchScreen_SetProjectText( kind )` (`0x004aa120`) letters the bar's label with UITEXT 446,
"{P14}" over "Estimated price : $ {P13}": P14 the current project's item's name, P13 `Lab_KindEstimatedPrice( kind )`
(`0x00503a20`), for kinds 0 to 3 the item's control record's `+4` (`Upgrades[0].CostOfUpgrade`, copied at the record's
making, `FUN_004d3e00`) and for kind 4 `Upgrades[level].CostOfUpgrade` of the item upgraded (`+0x1b8 + 0x40 x level`).
With no current project the label is UITEXT 185, "All Items Researched", and that kind's pump is refreshed. Each label
is a `LabelSkin` aligned (1, 1) by `0x0065c428` (with `0x0065c4b9( 1, 1 )`), in font slot 6 (`FUN_00485a70`), coloured
(224, 86, 86, 255).

**The pump.** Each panel keeps four slots: 0 the position (16.16), 1 a clock stamp (`FUN_0065968e`) written and never
read, 2 the speed, 3 the state. The opening sets slot 0 to `0x30`, state 0 and the pump's frame `0x30`.
`ResearchPump_SetRate( panel, rate )` (`0x004a95b0`): a rate of nought turns a running pump (state 1 or 3) to state 2
at frame `0x20`; any other rate starts a stopped or stopping one (0 or 2) in state 3 at frame `0x30`, and sets the speed
to `(rate + 20) x 0x10000 / 100` (a rate of nought leaves the speed as it was). `ResearchPump_Tick` (`0x004a9fc0`, which
each panel's proc runs) acts on message `0x1e`, which `UI_TickTree` sends every control of the tree once a UI pass
(`FUN_0048a580`, run from six places in `Game_StateMachine`), so no clock paces it: state 1 runs frames `0x00` to `0x1f`
and starts again; state 3 runs `0x30` to `0x3f`, then state 1 from 0; state 2 runs `0x20` to `0x2f`, then stops in state 0
at `0x30`. Each pass adds twice the speed, so a pump moves `2 x (rate + 20) / 100` frames a pass. `res_fluid` holds 64
parts, the frames: `fluid` to `fluid32` (0 to `0x1f`, the run) and `empty01` to `empty32` (`0x20` to `0x3f`, the drain and
the fill; `0x30` is the rest). **Measured in the original (Q705, `q705b/orig/b`, the real screen, the 30 frames a second
cap, the clock TRUE; `original/respump.py`):** the rides' pump at rate 2 took 600 passes in 20.00 s of the wall clock,
30.00 a second, its stamps 33 ms apart (25 to 42), so a pass a frame, and ran 13.14 frames a second; after a drag, rides
and sideshows at rate 1 (speed 13762) 30.00 passes and 12.46 frames a second each. The rate is
`Lab_KindRate( kind )` (`0x00503980`; `advisor-park.md`, "The rate") where the kind has a current project
(`Lab_KindCurrentItem`, `0x005039f0`), and nought where not. `ResearchScreen_RefreshPumps( kind or -1 )` (`0x004a9c70`)
hands the pumps their rates, then sets the parameter `0x12` of the loop sound `0x9a` to the mean of the five rates (all
five, whatever the kind asked). It runs at the opening, after an effort or workload drag (not while the opening seeds
the sliders, `[0x007cc1bc]`), and for a kind left with no project. `Research_OnKindFinished` (`0x004aaec0`), the lab's
message `0x12` for a finished project, picks the kind's next where it has none open and, with the screen up, letters
and fills that kind's panel again (`ResearchScreen_SetProjectText`, `ResearchScreen_SetFill`), which for a kind left with
nothing stops its pump.

**The arrows.** A panel's proc on `0x100` (a click): `0xd2d03` runs `Lab_PickPreviousProject( kind )` (`0x005036a0`), the
other `Lab_PickNextProject`, then the text and the bar. Back takes the nearest open project of the kind at or under its
highest group before the current one, else the last such after it, else keeps the current, which is set to -1 only if
it is done; for kind 4 the same test of the upgraded item as forward's (`park-engine.md`, "The next project").

**The sliders.** Message `0x800` from a slider: the five efforts go to `Lab_SetEffort( kind, value )`, the value
unchanged; the workload slider's value is held to the limit and `Lab_SetWorkLoad( 30 x value / limit + 70 )`, then all
five bars are set, the dial `0xd2d11` to frame `2 x (30 x value / limit) / 7` (0 to 8), and the parameters `0xc` and `0xd`
of both loop sounds (`0x9a`, `0x9b`) to `value x 100 / 1024`, in the order (`0x9a`, `0xc`), (`0x9b`, `0xc`), (`0x9a`, `0xd`),
(`0x9b`, `0xd`). A press on an effort slider's thumb (`0x801`, `Slider_NotifyPressed` `0x0066b9bc` on the thumb's `0x10005`;
a click on the track pages and sends none) starts sound `0x9c` with its parameters `0xb` and `0xa` the value, each `0x800`
of an effort slider sets them again on the handle `[0x007cc1d4]` (nought with no thumb held, which finds no voice), and
the release (`0x802`, `Slider_NotifyReleased` `0x0066b9ef` on `0x10004`, after the thumb is put where its value belongs) fades it
(`Sound_StopFading`) and clears the handle. The limit and the knob
are set again on `0x1e` when the researchers' summed ability changes. **Measured in the original (Q590):** a slider's value setter
(vtable `+0x1c`, `Slider_SetValue` `0x0066b508`, then `Slider_SetPosition`) sends `0x800` to its parent whenever the
value changes, and the opening sets the knob after the limit, so the opening, and each `0x1e` that finds the summed ability changed, runs
`Lab_SetWorkLoad` on the knob's round trip, `30 x ((w - 70) x limit / 30) / limit + 70` in whole numbers, which is `w - 1` where it does not divide back. With
Full Simulation's `most` of 60, of the summed abilities 2 to 12 those of 3, 6, 9 and 12 do so at 85, and each opening would take the work load
down one (85, 84, 83...; `q704/knob-roundtrip.txt`): with one researcher of grade 1 the original's `mWorkLoad` read 85, 84, 83 and
82 across three openings (`q705/orig/d/drive.log`). The efforts' seeding writes each effort back unchanged. `Lab_SetEffort`
(`0x00503870`) and `Lab_SetWorkLoad` (`0x00503900`) each hold their value to 100 as an unsigned number (so one under
nought is 100 too) and work every kind's output out again, `effort x 100 / the efforts' sum x mWorkLoad / 100` in whole
unsigned numbers, nought where the sum is: after the first opening the original's outputs read 48, 6, 14, 6 and 4. The
screen does not pause the park: its tick ran on while it was open.

**The two sliders' directions** (`Slider_SetPosition` `0x0066b088`, `Slider_ReadThumb` `0x0066af19`, `Slider_PageTowards`
`0x0066b8aa`). With the slider's `+0x14c` bit 2 set the thumb runs across, the left the lowest value; with it clear, up
and down, **the top the highest**: its top at `(highest - value) x travel / range` below the track's, the travel the
track's height less the thumb's. A click off the thumb pages the value down where it is left of the thumb's right edge, or below
its bottom edge, and up otherwise (`+0x144` positive). The effort sliders (flags `0x61`) run across, the work-load slider
(`0x51`) up and down; each one's track is its stream text rect. The stream's op 2 on a panel is a skin looked up by
the same mesh hash as op 1's (`UI_ParseTreeStream` `0x0065fd58`): the panel's `res_pipe`, drawn in the panel's text
rect, as the frame shows it, between the bar and the pump.

**Opening and closing.** The opening loops sounds `0x9a` and `0x9b` and ends with `FUN_00486b00( 200 )`, the
advisor's line for the screen (row 200, posted-only, response 419, sample 391: "this is where you discover new stuff to
build, dragging the horizontal sliders sets how much research gets done in each field, you know, like rides or shops,
and those pumps on the right show how fast the work is going; if you want to change which item is being researched,
click the left and right arrow buttons; drag the vertical slider on the left to determine how hard your researchers
work", `global-speech-transcripts.tsv`). Message 5 and the close button close it; `0x14` (destroyed) stops the three
sounds and withdraws message 200. The knob's own proc (`ResearchScreen_KnobProc`, `0x004a9f90`) clears `[0x007cc1dc]`
when it goes. Not read: how the dial draws. The gauge is `Meter_Construct`'s (`0x00477870`), the happiness gauge's two-halves meter with
`effortguage.wct`, at 0 to 1024; the bars `FUN_00477ff0`'s, the stat bars' skin with `bluebar.wct`, whose filled part
is the dark half, from the left.

**The research screen's sounds** (`cat_ui`, `lib/sfxmap.py`; every effect of the category flagged `0x200`, so flat;
`audio.md`, "A voice's two controllers" for the rule). `0x9a`: flags `0x0606`, the music's class, its zone parameter key
18 (`0x12`); two variations of three samples each, every one volume 8 to 85 controlled by key 12 (`0xc`, mask 1), no
pitch, no wait; from either, a parameter of 1 to 100 goes to variation 1 and 0 to variation 2. `0x9b`: `0x0606`, key 13
(`0xd`) both the zone parameter and, in every variation, the controller of volume 2 to 20 and pitch -8 to 8 (mask 3);
variation 1 one sample, variation 2 ten; 0 to 80 goes to 1, 81 to 100 to 2. `0x9c`: `0x0206`, the plain class, no key;
four variations of one sample each, volume 2 to 15 by key 10 (`0xa`) and pitch -24 to 36 by key 11 (`0xb`), no wait. So
the work-load knob's `0xc` is `0x9a`'s volume and `0xd` `0x9b`'s zone, volume and pitch; the rates' mean picks `0x9a`'s
variation; the effort is `0x9c`'s volume and pitch, each set applied to the sounding sample at once. **Measured in the
original (Q705, `q705b/orig/b`; `musicvoices.py`, `mixfreq.py 2d`), predicted first:** with one researcher of grade 1
(rates 2, 0, 0, 0, 0, the mean 0) the opening left `0x9a` keys 18,12,0,0 values 0,29,0,0 on variation 1 and then 2
throughout, its channel 22 of 127 at its sample's own rate; `0x9b` keys 13,13,0,0 values 29,29,0,0 on variation 1, 5 of
127 at 21268 (22050 × 2^(-5/96), pitch -4); the rides' thumb pressed at 100, `0x9c` came with keys 0,10,11,0 values
0,100,100,0 at 11 of 127 and 28803 (pitch 36), each draw a variation other than the one before; dragged to 51, values
93, 84 ... 51 and 6 of 127 at 23193 (volume 8, pitch 6); let go, gone 337 ms later. While the advisor spoke row 200
every channel read lower (the music 22 of its 60, `0x9a` 8, `0x9b` 2): his duck.

**Built** (Q590, `ParkResearchScreen`, opened by `ParkResearchOpener`; the lab's `SetEffort`, `SetWorkLoad`,
`PercentLeftOfDearest`, `EstimatedPrice`, `PickNext`, `PickPrevious`, `MostExpensiveProject`; `UiSlider.Vertical`): the
stream's frame, title, five panels with icon, arrows, bar, label, pipe and pump, five effort sliders, the work-load
slider with its gauge and dial, the tick, the 1000 ms timer, the ability watched on every pass, the opening's round trip,
and advisor row 200 posted as it opens and withdrawn as it goes. A kind's price is the item's control record's `+0x04`,
read from the park file (a fresh park's, the item's own); a fresh world's dearest project is the fill's (`0x00503225`).
`mWorkLoad` and `mMostExpensiveProject` are written with the park. **The pumps and the sounds** (Q705; `ResearchPump`,
`ParkResearchScreen.RefreshPumps`, `RunPasses`; `ParkAudio.PlayInterfaceSound`, `SetScriptSoundParameter`,
`ApplyControllers`; `UiSlider.ThumbPressed`, `ThumbReleased`; `ParkResearch.KindFinished`): the pumps' states and rates
as above, moved on `Time.Delta` at 30 passes a second, the original's measured rate under its 30 frames a second cap,
where the original moves them once a frame at whatever rate it draws (**a deviation**, said at
`ParkResearchScreen.PassesPerSecond`); the three sounds as script voices of `cat_ui` with their controllers, each set
past slot 0 applying the volume and pitch to the sounding sample; a finished kind lettered again at once. Said: a held
voice's next sample is drawn as the last ends here, where the original's queues it soon after the last starts, so a
parameter changed in between picks here and not there. **Confirmed in both games** (Q705, `q705b/PREDICTION.txt`
rounds O and G): above for the original; here the same census (`researchscreen`: rides rate 2, starting at `0x30` at
14417; the loops' values and volumes 30/0 and 7/-4), 29.93 passes a second, the rides' pump green and the other four
dark in both frames (`q705b/run2/2-pumps.png`, `q705b/orig/b/o4-pump1.png`), the thumb's `0x9c` at 15/36 and, dragged to
49, 8/5, faded at the release, and the loops stopped at the close. **Q590's, confirmed in both games** on one scene (`q705/fs1.TPWS`, q327's `FSFRESH.TPWS` with its
researcher written to grade 1), predicted first (`q705/PREDICTION.txt`): the original's work load 85 to 82 over three
openings and its five bars within a frame pixel of the rule (round O, `q705/orig/d/t4-open1.png`); this game's census at
the opening `workload 84 limit 597 knob 298 dial 4 gauge 597 ability 3 most 60`, bars 624, 839, 686, 501 and 839, the
original's five labels, then 83 and 82, the rides' right arrow to "Sun God / Estimated price : $ 1500" at 593, the knob
dragged to the top held at 597 (work load 100, dial 8), the rides' effort dragged to nought (outputs 0, 14, 21, 42, 21),
and response 419, sample 391 said once the clock ran (round G, `q705/run3`). The knob stands at the same height in both
frames (layout y 534 there, 532 here).

### The Staff Training Budgets screen (Q352)

Screen 9 of the Money category, `staffcosts`. Read 2026-10-13: the builder `FUN_004b2750` (decompiled, its calls
read as disassembly), the handler at `0x004b24b0` (loose code, read by `disassemble`; nothing written or named),
the stream at `0x00756400` (walked to its balanced close; `lib/uistream.py`), its fourteen mesh hashes resolved
(`meshhash.py`), and `FUN_004c9c00`.

**The opener.** With its screen up (`DAT_007cc2ec`) it does nothing. Otherwise it destroys the park screen that is
open (`FUN_00485b40`), leaves the category on it (`FUN_004a0810( 3, 9 )`) and loads the tree. Its callers are the
category opener (`FUN_004a0940`), the entry-price, finance and loans screens' handlers (`0x00498d5f`, `0x0049a1a4`,
`0x0049f82e`), the message bar's button of type `0x15` (`FUN_0048e0f0`, `0x0048e206`) and the finance sub-panel's
`0x1b5b` (`FUN_00497a70`). No key was found for it.

**The stream.** Rects on the 2048 x 1536 layout.

| Id | Type | Rect | Mesh | Help | What it is |
|---|---|---|---|---|---|
| `0x2d9` | 1 | (208, 30)-(1840, 1007) | `w_park` (the node `window3`, `0xf76e4349`) | | the frame |
| `0x2da` | 1 | (805, 74)-(1277, 153) | | | the title, UITEXT 147 through `UI_SetTitle` (`0x004b3293`) |
| -1 | 2 | (1711, 820)-(1794, 903) | `b_okay` | 1 | close |
| `0x2db` | 2 | (1688, 575)-(1790, 677) | `b_entry` | 176 | to the entry price (`FUN_00498d80`) |
| `0x2dc` | 2 | (1688, 467)-(1790, 570) | `b_finance` | 175 | to the finances (`FUN_0049ac60`) |
| `0x2dd` | 2 | (1688, 360)-(1790, 462) | `b_loans` | 174 | to the loans (`FUN_0049fb30`); **disabled in game type 2**, Instant Action (vtable `+0x14` with 0, `0x004b32eb`) |
| `0x2de` | 1 | (267, 183)-(765, 411) | `!frame` | 177 | the month's account |
| `0x2e3`, `0x2e6`, `0x2e1`, `0x2e5` | 1, flags 3 | x 284-561; y 195, 246, 297, 349 | | | labels, UITEXT 357, 358, 359, 361: Cash in, - Staff costs, - Other costs, Balance |
| `0x2e2`, `0x2df`, `0x2e0`, `0x2e4` | 1, flags 3 | x 591-755; the same four rows | | | their figures, right-aligned (`FUN_0065c428( 2, 1 )`) |
| `0x2e7`, `0x2ec`, `0x2ea`, `0x2eb`, `0x2ed` | 12 | (770, y)-(1670, y + 149); y 183, 337, 491, 645, 799 | `f_train` | 164, 165, 166, 167, 168 | a spinner a kind: handymen, mechanics, entertainers, guards, researchers, top to bottom |

The stream lists the five spinners in the order `0x2e7`, `0x2ea`, `0x2eb`, `0x2ec`, `0x2ed`, not their order down
the screen. Each holds, 154 down a row (a unit more or less here and there): the text rect (996, 233)-(1273, 278);
op `0xf`, `b_minus`, (925, 224)-(985, 285); op `0xe`, `b_plus`, (1286, 224)-(1346, 285); `0x2e8`, type 9, the
skill bar, (1376, 235)-(1592, 274), help 169 to 173 down the screen; and `0x2e9`, the kind's picture, (800, 203)-(902, 306):
`h_handy`, `h_mech`, `h_enter`, `h_guard`, `h_resrcher`. The two cost rows are red (`FUN_0065c5d5( 0xff, 0, 0,
0xff )`), every figure font 6, a spinner's text black and centred.

**The builder.** Each spinner is given the range 0 to 10000 (`FUN_0066a5c4`), the step 25 (`FUN_0066a68d`), the
number skin that letters "$ 75" (`FUN_00490250`) and its kind's budget, thing 1's `mBudget` (`FUN_00519450`, `+0xc`
for `0x2e7`, `+0x10` `0x2ec`, `+0x14` `0x2ea`, `+0x18` `0x2eb`, `+0x1c` `0x2ed`). Each bar is given the skin
`ridestatbar.wct` and `( FUN_004c9c00( model ) << 10 ) / 100` of 1024, the models 5, 4, 6, 7, 8 in the rows' order:
`FUN_004c9c00` adds the grades (`+0x1e4`) of every thing of the model, times 100, over five a member, nought with
no member. The bars are set here and not again. The account's four figures are the hire screen's ("The mini-balance
is a MONTHLY ACCOUNT"): the analyser's newest month of cash in, of staff costs, of all costs less those, and cash in
less all costs, the three sums kept in `DAT_007cc2d4`, `DAT_007cc2e0` and `DAT_007cc2dc`, with `DAT_007cc2e4` the
other costs.

**The handler.**

- `0x800`, a spinner's value, ids `0x2e7` to `0x2ed`: the kind's budget is the value, **nought for a value below
  nought** (`SETL`/`DEC`/`AND`, `0x004b2544`), written at once; **a value over nought sets thing 1's `+0x20`,
  `mHaveEverTrained`** (`0x004b25d0`), which nothing here clears. Then, and for ids `0x2e8` and `0x2e9` too, the
  refresh: figure `0x2df` is **the five budgets added up** (`0x004b266a`), and `0x2e4` is the kept cash in less
  that sum less the kept other costs (`0x004b2695`).
- `0x100`, a click: -1 closes; `0x2db`, `0x2dc`, `0x2dd` open the entry price, the finances, the loans.
- `5` closes; `0x14` forgets the screen (`DAT_007cc2ec`).

**Opening it is a change of every budget (measured, not read).** A spinner given its first value tells the
handler as a press does. So the Staff costs row never shows the analyser's month: it reads the five budgets added
up from the opening, and a file whose budgets are over nought with `mHaveEverTrained` unset has it set by the
opening alone. The original, off-screen (`q352/orig/run2.sh`): given `q352/set75.TPWS` (the mechanics' 75) it read
"$ 75" and "-$ 11750" on 27 February, weeks before the month's change (run c); given `q351/bud2.TPWS` (25 and 50
written by hand, the flag nought) thing 1's `+0x20` went 0 to 1 on tick 1095, the tick the screen came up, the row
"$ 75" (run d). **Its reader is advisor row 172** (`0x0059fb10`: more than 15 staff and none ever trained; `advisor-park.md`, "Row 172 and row 72 read whole"; fork watch 2026-10-19, `verify-advisor-ui.md` L5), not built.

**OpenTPW** (`ParkStaffCostsScreen`; `ParkCategoryScreens.StaffCosts`; `ParkPeople.SetTrainingBudget`,
`HaveEverTrained`, `TrainingOf`; `ParkGadget`'s bar button of type `0x15`; the console's `budgets`). The stream's
frame, title (lettered as `UI_SetTitle` letters one), buttons, account panel and five rows; a press of a spinner's
button writes the budget as the handler does; the opening writes each back; the bars are `ParkObjectWindow.UiStatBar`.
The month's change spends what is set (`ride-operation.md`, "The month's change"), the advisor's rows 28 to 32 read
it (`advisor-park.md`, "Staff nobody has trained"), and a saved park holds it (`saves.md`, "OpenTPW's writer, the
training budgets").

**Deviations, each said at its site.** The account is built (Q385, "The mini-balance is a MONTHLY ACCOUNT"). The bars wear
`ridestatbar.wct` (Q353, "The stat bars' skin", below). The lettering in the panel and the spinners is smaller than the original's at this window
size (not looked into). The finances and the loans are not built, so those two buttons close the screen and count
(`FINANCE_SCREEN`; `LOANS_SCREEN` in Full Simulation).

**Predicted first, both games** (`q352/PREDICTION.txt`, three rounds; `q352/confirm.py <out> <bin>
fix|money|bud2|month|control`, `q352/orig/run.sh` and `run2.sh`). Two predictions missed, both mine: the month's
change at the original's tick 1383 in this game, whose calendar counts from the scene's start (Q149), and the
Staff costs row empty at the opening, which this game matched and the original did not: found by the original's
run, then built as above and run again.

| | The original | OpenTPW |
|---|---|---|
| The training tag's bar button, `q351/all0.TPWS` | the category left on 9, the screen up, five "$ 0", the tag off (Q351's a2, run a) | `button type 21 opens the Staff Training Budgets screen and takes tag 30 off`, `left on 1,4,9`, `opened, budgets $ 0,$ 0,$ 0,$ 0,$ 0 skill 0,0,0,0,0 staffcosts $ 0 loans off` (`run-fix2`) |
| The mechanics' plus, three times | thing 1's `+0x10` 25, 50, 75, `+0x20` 0 to 1 at the first; "$ 75", Staff costs "$ 75", Balance "-$ 11750" (`orig/a-c3-mech-plus3.png`) | `budgets 0,25,0,0,0 evertrained 1`, `0,50,0,0,0`, `0,75,0,0,0`; `staffcosts $ 75` (`run-fix2/3-budgets.png`) |
| The handymen's plus, then minus twice | `+0xc` 25, then 0; `+0x20` still 1 | 25, 0, 0; `evertrained 1` |
| The stock staff's bars | four at 60 per cent, the researchers' at 40 (Q198's `o12.png`) | `skill 60,60,60,60,40` (`run-money2/1-screen.png`) |
| The loans button in Instant Action | grey | disabled: the click does nothing |
| The gadget's Money button, then the entry price's staff-costs button | the screen (runs c, d) | the same; the Money button afterwards opens it (`category 3 is left on screen 9`) |
| Budgets 25, 0, 50 and the flag unset, the screen opened | `+0x20` 0 to 1 at the opening, Staff costs "$ 75" (run d) | `evertrained 0` to `1`, `staffcosts $ 75` (`run-bud2`) |
| The month's change with the mechanics' 75 | thing 26's `+0x1e8` 0 to 15 on tick 1383 (run b; 0 to 10 with 50 set, run a) | `through 0` to `through 15`, a withdrawal of 75 then the wages 27, 69, 36, 45, 75, on this game's own month, tick 1879 or so (`run-month2`, the final build; `run-month1` the build before the opening's write-back, the same); photographed either side in `run-month3/before-after.png`: 1/31/2000 at $86657, 2/1/2000 at $86360, each the `money` line's balance |
| Main's build, the same tag and button | | `button type 21 is not built`, `TAG_BAR_BUTTON` (`run-control`) |

`side-by-side.png` holds both screens with the mechanics at 75. Fifty bugs put back, fifty caught at the first round
(`q352/mutations.py`); the one line no test reaches is `Level`'s hand-over of the budgets to the writer, which
`run-fix2`'s file holds. Not run: Full Simulation (the loans button enabled there; the test holds only
Instant Action's), a budget at 10000 in either game (the test holds the end), the screen in the three other themes.

### The stat bars' skin, `ridestatbar.wct` (Q353)

Every type-9 bar of the park's screens is given the same skin, `FUN_00477ff0( texture )`: 28 of its 46 calls hand it
`"ridestatbar.wct"` (`0x00750e00`: the object window's four, the training screen's five, the hire screen's one and
the others' bars) and 8 `"happygrad.wct"` (`0x007508c0`: the all-staff screen's three, "The all-staff rows", below,
and one each in `FUN_004939d0`, `FUN_004afc10`, `FUN_004b1040`, `FUN_004b53c0` and `FUN_004b6c20`); the other ten
hand it `"bluebar.wct"` (five), `"smallguage.wct"` (two), `"hap.wct"`, `"sad_undec.wct"` and `"progress.wct"`. It is an
object of vtable `0x006feeb8` holding two quads in a 100 by 100 space, four vertices a block of `0x30` bytes (x, y
and z four apiece, `FUN_00477c00`). The control hands it its value through the skin's `+0xc` with flag 8
(`FUN_0066d885`, the value held to 0 to `0x400`); there (`0x00478593`) `v = value x 0.0009765625 x 100`, held to
0 to 100, and:

| Quad | x | u | v band |
|---|---|---|---|
| filled | 0 to v | 0 to v / 100 | 0.5 to 1 |
| the rest | v to 100 | v / 100 to 1 | 0 to 0.5 |

So each part is CROPPED, never squashed: a short bar shows only the ramp's left end, and only a full one its green
and its dark right edge; the empty part is drawn too, a bar at nought all track. The texture (`ui.wad`,
`textures\ridestatbar.wct`, 32 by 32; `stexture\` holds a copy) is a colour ramp in its first sixteen rows and a
grey track (56, `383838`) in the rest. The original's bar covers the
control's whole rectangle (rows 118 to 137 where the layout gives 117.5 to 137 at 1024 by 768), and the colour half
bleeds into the top rows of the track.

**OpenTPW** (`ParkObjectWindow.UiStatBar`, `Parts`; `Units` for the original's 0 to 1024; `Skin` the texture handed,
`ridestatbar.wct` unless the caller says otherwise; `ParkHireScreen.SkillUnits`).
The two parts over the whole rectangle as the table; the training screen hands its bars `(share << 10) / 100`, the
hire screen `((grade & 0xff) << 10) / 5` as the row under the pointer is picked (its list's flag `0x80`, stream
flags `0x291`, as the buy screen's); the object window's two built bars their percentage.

**Deviations, each said at its site.** The ramp runs red, orange, yellow, green as the original's (Q354, the header's scales; Q355, the original's zlib codec: `textures.md`), within 16 a channel along the 80% bar and red to its left end ((254, 4, 4), the original's (221, 42, 0); `q355/side-by-side.png`). The original's track is 49 grey where this game draws the texture's 56. The
hire list's first row is not picked as it fills (above), so the bar stands at nought until the pointer passes a row,
and keeps that pick through a change of tab where the original's is the new tab's first row.

**Predicted first, both games** (`q353/PREDICTION.txt`; `q353/confirm.py <out> <bin> fix|control`,
`q353/orig/run.sh`, `q353/measure.py <png> <rect>...` for a bar's coloured share). The file `q353/grades.TPWS` is
`q351/all0.TPWS` with the five grades 4, 1, 0, 3, 2 written (staff record `+398`). Round 1 missed three of four, all
mine: the halves swapped (this game's quad puts texture y 0 at the bottom of its rectangle) and a click on a hire row
that chose the candidate and closed the screen (the list did not pick under the pointer).

| | The original (`orig/a-*.png`) | OpenTPW (`run-fix4`, the final build; `run-fix2` and `run-fix3` the same) |
|---|---|---|
| The training screen's bars, grades 4, 1, 0, 3, 2 | coloured 0.811, 0.189, 0, 0.604, 0.406 of each bar, the rest grey | `skill 80,20,0,60,40`; 0.808, 0.202, 0, 0.606, 0.404 (`1-train.png`); `UI: the stat bars' skin ui/textures/ridestatbar.wct is 32x32` |
| The hire screen opened | the first row picked, the bar 0.617 | no row picked, the bar all grey (`2-hire.png`), `STAFF_SKILL_BAR` gone |
| The pointer over the first row | the bar unchanged | `Hire screen: candidate 11 grade 3, skill bar 614 of 1024`, 0.606 (`3-picked.png`) |
| The mechanics' tab | the bar 0.409, the first mechanic's | 0.606 kept (`4-tab.png`) |

The original's list held five handymen (Andy Buchanan, Chris Battson, Jan Svarovsky, Jon Taylor, Paul Carpenter)
where this game's, on the same file, held two (Chris Battson, Rajan Tande): not looked into. Not run: the object
window's bars in the original (no route there was driven), a full bar, the other three themes.

### How allpeeps keeps itself current: rows updated in place, never cleared (Q200)

Decoded 2026-09-30 (`wf_95649c5e-08a`: one Opus skeptic, three claims upheld and two amended, none refuted; the
amendments re-read first-hand). **The original never empties this list after it opens, so its scroll stays where the
player put it.** Three paths change it:

- **Opening.** `FUN_00493530` sets the sort from `DAT_007508bc` while the list is still empty (`FUN_006659af(v, 0)`),
  adds one row per guest (`FUN_00481bc0( 0x40, 0x004937e0 )`, thing type byte `+2` == 1, each through the row adder
  `FUN_00493800` → `FUN_0066403b` with no redraw), draws once (`FUN_00663324`, `0x00493632`), and, when handed a guest,
  selects that guest's row and scrolls to it (`FUN_00665739`, the one direct writer of the top row besides the
  constructor and the clear). Then it arms timer `0x80083` at 2000 ms (`FUN_0065ef90`, `FUN_0065f024`).
- **The timer, every two seconds** (message `0x10` with id `0x80083`, `0x00493270`). For each existing row, from the
  last to row 0, it fetches the row's guest (`FUN_00664c71`), works out the same six values the row adder does, but
  for one: **the time in park from the stay shifted back left by two** (`SHL EAX, 2` at `0x004932db`), four times the
  adder's hours ("The visitors rows", below), and writes them into that row (`FUN_006644ea( row, values, 0x3f, row == 0, -1 )`). Row 0's call redraws the visible
  rows from the top row `list+0x154`. **Nothing is added, removed, re-sorted or scrolled**: a row keeps its place from
  its last sort or insert even when the value it was sorted on has changed.
- **A guest arriving or going.** The guest constructor `FUN_004faec0` broadcasts message `0x1c` (`0x004fb2fd`) and the
  thing delete `FUN_0050b780` broadcasts `0x1b` for every thing (`0x0050b7f9`, before the free); `FUN_0050ca50` routes
  them. `0x1c` adds that guest's row (`FUN_00493c50`: the row adder, which inserts in sort order when the list's
  `+0x48 & 0x10` is set, then a redraw). `0x1b`, for type 1 only (`FUN_004818c0`), finds the row by its guest
  (`FUN_00664d2a`) and removes it (`FUN_00493c10` → `FUN_006647cb`). Neither writes the top row itself. Both finish in
  `FUN_00664495`, which sets the scrollbar's range to count − visible and puts its position back at the top row; the
  slider clamps the position, and if that moved it the list takes it back (`0x800` → `FUN_00664ea3`, `0x00664ee9`).
  So a removal near the bottom pulls the view up by the rows it lost, and nothing else moves it.

Three edges a copy must match, found by the skeptic:

- **With count ≤ visible rows the slider is disabled and never repositioned** (`0x006644d2`), so a view scrolled one
  row down that loses a guest down to thirteen stays one row down, row 0 unseen and the last slot blank, until the
  wheel clamps it (`0x1000d`).
- **Removing the selected row selects by visible slot, not by index**: `FUN_006647cb` passes the absolute index (the
  same, or one less if it was the last row) to `FUN_0066525c`, which adds the top row to it. At top 0 that is the
  next row; scrolled down, it lands further down the list.
- **The selection is not shifted** by an insert or removal above it, so the highlight moves onto a different guest.

The sort is remembered between openings: message `0x406` stores (column + 1), negated when descending, in
`DAT_007508bc` (`0x00493479`); its static default is 1, Visitor Number ascending.

Read first-hand for the build (Q200b, 2026-09-30):

- **The sorted insert goes after every row that is not greater.** `FUN_0066403b` walks from the head until
  `FUN_00663edc( new, row )` answers 1, which for a numeric ascending column is `new < row`, strictly. So equal
  values keep the order they came in, and an arrival not yet numbered (visitor 0) goes after the other noughts and
  before every numbered visitor.
- **An add that makes the count 1 selects the first row** (`FUN_0066525c( 0 )` in `FUN_0066403b`). The original's
  visitors list opens with a row highlighted, not always the first ("A right click on a list"). OpenTPW builds the
  pick for the hire, all-items and all-staff lists ("The first row is picked as a list fills") and counts it for the others
  (`LIST_FIRST_ROW_SELECTED`, `docs/QUEUE.md` Q232).
- **`FUN_0066525c( slot )`** selects `slot + top` when `slot < visible` and `slot < count`; otherwise, with
  `list+0x48 & 0x100` clear, it returns and the old index stands, and with it set it selects nothing. This list's
  flags are `0x291`, the bit clear ("A right click on a list"); OpenTPW still counts that branch
  (`LIST_RESELECT_PAST_THE_WINDOW`) and drops the selection (`docs/QUEUE.md` Q232).

**OpenTPW keeps it current as the original does** (Q200b): `ParkVisitorsScreen` fills the list once, rewrites each row
in place on the two-second timer, and adds and removes a row on `ParkPeople`'s `GuestArrived` and `GuestLeaving`;
`UiList` keeps its selection as an index and clamps the top row only as the slider does. The sort is always Visitor
Number ascending, the static default, because the headings do not sort yet. Confirmed in the game (`q200brun.py`,
`q200b-run1/`, silent, stock park, twenty admitted, 33 rows; `save/` unchanged), each predicted first: four rows down
the first row was cash 450 and still 450 three seconds on, with row values changed in place and not re-sorted; an
arrival's row went in at 1 (visitor 0, after the one nought already there) with the top row kept at 4, its going took
it out at 1; at the bottom (top row 20), the last row's going left 32 rows and the top row 19.

### A caution: `0x10`/`0x11`/`0x12` are column headers, not buttons

They are children of the buy list control `0x1f8`, numbered `0x10 + column`, and the UI library sorts
on them — labels UITEXT `0x7b` *Name*, `0x7c` *Price*, `0x8a` *"."*, help rows `0x90`/`0x91`/`0x92`
(*"sort the list by item name"* / *"by price"* / *"by items already owned or recently researched"*).
The buy stream itself contains ids `0x1e9`..`0x200` and **no** `0x10`/`0x11`/`0x12`, so they cannot be
top-level controls. The list widget sorts internally and the screen only remembers which column was
chosen. **Do not read them as gadget buttons** — the gadget's own six are `0x26`..`0x2b`.

### `FUN_00486b00` is a different id space from UIHELPTEXT

It goes to the advisor/message system (`FUN_0059b590` → `FUN_0059bf20`), not to the help table.
Reading its ids as UIHELPTEXT rows yields text that is plausible and wrong — the pylon button's
`0x125` decodes there as *"Left-click to view item's details"*. As an advisor message it
resolves through the metadata table at `0x0076e300`, filled by the static initializer at `0x005a0a50`: response 553,
sample 565, whose transcript is "looking this button let's you place normal pylons" (`advisor-park.md`, "A screen's line is a message id, resolved
through the metadata table").

### The arm is the gadget's panel carrier

`FUN_004a2590` puts a panel on the arm, `FUN_004a25f0` takes it off, and the message bar is only one of
the things it can carry. **Five callers** hand it a payload; the camcorder panel is `FUN_00498bb0`.
`FUN_004a25f0`'s argument lifts control **0x27** afterwards. Both are gated on `DAT_007cb2b8` (the gadget
is built, written by `FUN_004a1d70`) and `DAT_007cb2ac` (the interface is up, written by `FUN_00489f50`) —
**no simulation gate stands in front of the arm.**

## The camcorder viewfinder — stream `0x0074fa98`

Not a "park HUD root". Two top-level controls: the `viewfinder` panel (170,170)-(1878,1366) — which is
`f_viewfinder.MD2` — and a `b_eject` button (1916,1404)-(2018,1506), help 468 *"Left-click to exit
camcorder mode (C)"*.

**Confirmed against a screenshot of the original.** At 800x600 the virtual screen scales by 0.390625 with
no letterboxing, so `b_eject` lands at (748,548)-(788,588) — exactly the eject icon in the shot — and the
viewfinder's corner brackets sit on its rect. **The management gadget is NOT on screen in camcorder
mode**, which is why hiding it there is faithful and not a workaround.

### Anchors differ within one screen

Anchoring takes a rect's **centre** against the thirds: left third pins left, right third pins right,
middle centres — and the same down the screen. The gadget is bottom-**left**; the eject button's centre x
is 1967, past 2048*2/3, so it is bottom-**RIGHT**. Cropping it with the gadget's anchor missed it by the
whole 320px of horizontal slack at 1280x720 and returned a picture of grass. **Never assume one anchor for
a whole screen.**

### Four ways out of camcorder mode: Escape, the C key, the eject button, and a right click

`FUN_00488a00`'s key-up case treats `key == 0x1b` (VK_ESCAPE) and `action == 0x10` (camcorder, shortcut
16) the same way: it sets layer 1's cursor to 1 (`c_busy.ani`, `0x00489750`), leaves first person
(`FUN_0042ae70` or `FUN_0042a190`), and sets the cursor back to `0x15`. **`FUN_0065f11d` is a cursor setter, not a
page switch**: it stores the id at the control's `+0x50` and applies it at once if the pointer is over the
control (`FUN_006590f7`).

The same calls, from a copy at `0x00488ab7`..`0x00488adb` that joins Escape's code at `0x00488b5a` (at `0x00488b55` in a
ride view), are reached two more ways (decoded for `docs/QUEUE.md` Q59: three decoders, each put to a refuter, then a
critic; static only):

- **The eject button's left click**, message `0x100`, with no RMB cancel test and no button test (`0x00488a37`): the
  button class posts `0x100` with its id to its parent (`+0x60`) at the end of a left click (`0x006691d6`), and
  `b_eject`'s parent is layer 1.
- **A right click on the layer, with RMB cancel on**: the `0x10006` case (`0x00488aa1`..`0x00488adb`) reads
  `[0x0078d911]`, then the button (`1`: the user's secondary button, since the DirectInput library swaps the two when
  Windows has them swapped, `0x006342b9`), then `gui_CameraFlags & 0x16`. `0x10006` is the UI library's
  **single** click, not a double click - see "A click and a double click" below. A double click leaves on its first
  click, and its second press (`0x10007`) falls to the handler's tail.

**Where a right click leaves.** The viewfinder panel `0x89db52` is built with flags **3**, visible and disabled
(stream `0x0074fa98`: op 0, type 1, flags 3), and the hit test `FUN_0065db25` skips a control flagged `0x2` with all it
holds, so a press on the frame is layer 1's. `b_eject` (flags 1) takes a press on its own rect, and the button class
hands a right press or release (`param_3 != 0`) to the base proc on itself (`0x00669003`, `0x006690d6`), whose click
goes nowhere. So in 2048x1536 units a right click leaves anywhere on `0 <= x < 2047, 0 <= y < 1535` but
`1916 <= x < 2018, 1404 <= y < 1506`. With RMB cancel off, nowhere. Nothing passes a message to a parent: the queue
pump hands each entry to its own control's `+0x114` (`0x00669a84`, `FUN_0065f697`). The management screens are built
onto layer 0, which first person hides, and the help bar's label is built disabled (`0x004922a6`..`0x004922aa`, `+0x14` with 0; flag `0x2`, so the hit test skips it) and is
hidden in first person (below, "The help bar in first person"), so nothing else is under the pointer. The sink's own control
there is layer 1.

**What else a right press does in first person.** `FUN_00488a00` hands every message, before the base proc, to
`FUN_0042a760`, which sets `DAT_00790aac |= 4` on a right press and clears it on the release (`0x0042a8bd`,
`0x0042a8fb`). The park's own layer and the full-screen view's cover hand theirs to the same function (`0x00488290`,
`0x0048845c`, `0x004a287d`), so the bit is the right button held from a press on any of the three (with RMB cancel on, the park's own layer keeps the press back, `0x00488354`, and sends it once the pointer has moved more than 8 or 200 ms have passed), and a press the
eject button took never sets it. The camera update keeps bits 1, 2, 4 and `0x80` of the word each frame and rebuilds
the rest from the keys (`AND AL,0x87`, `0x0042b32b`). Walking (`gui_CameraFlags & 0x16` set, `& 0x3c` clear) it
builds the forward term from the keys, -0.1 for bit `0x40` then +0.1 for bit `0x20` (`0x0042b448`..`0x0042b468`), and
then, while bit 4 is set, adds another 0.1 (`EDI = word & 4` at `0x0042b716`; `0x0042b925`..`0x0042b93f`,
`[0x006fddac] = -0.1` subtracted), whatever RMB cancel is: **a held right button walks forward, as far as the Up arrow
does, and the two add**. The press and release also reach the camera table as key `0xfff1` (`button - 0x10`,
`0x00488a74`), which no row binds. The walk of a quick click is thrown away with the rest when leaving puts the saved
point back.

Measured in the original (Lost Kingdom, first person at (479.4,166.5), yaw 0, zoom word `0x007909ec` 110, RMB cancel
on, the camera read from memory): the right button held 1.84 s on the view, the word `0x84` while held and `0x80`
after, the position 40.4 along the facing, still in first person; the Up arrow held 1.87 s, the word `0xa0`, 40.4;
both for 0.81 s, `0xa4`, 34.1, twice the rate; Down with the button, `0xc4`, standing to a frame; held 2 s on the eject
button, the word `0x80` throughout and the position unmoved; a quick right click, out of first person. So one forward
term walks about 21.8 units a second there. The velocity that term feeds is not decoded to a speed (`0x0042bc2a` on:
the square root of the zoom word times `[0x006fddf8]` 0.02, halved with the last frame's).

**In a ride view** (`gui_CameraFlags` 4, "Ride it!", `FUN_004e15b0` → `FUN_0042a560`) the same exits run `FUN_0042a190`,
which goes back to walking first person when the ride was entered from it (`0x400`, `0x0042ac0b`) and to the orbit
otherwise. Flag `0x10` is tested and never set (dead by CODE). The two views of a park's end that `FUN_0048ac40` and `FUN_0048ae70` build
(world `+0x1da738 == 4`, where `FUN_004a2ac0` switches no layer) take the click in their own handlers, `0x0048a740` and
`0x0048a970`, and only 2000 ms after they open; the first then leaves the park in the full-screen view
(`park-engine.md`, "The full-screen view: F3").

**OpenTPW.** `WindowStack.RightClick` is the base proc's click for the right button; a press on the view asks
`ViewRightClick` what answers its click, and the park answers `ParkViewfinder.RightClicked` in first person, which leaves
it with RMB cancel on. The limit is timed on `Time.WallMilliseconds`, real time as the original's, and the way into first
person closes the open park screen, as the original's does (`FUN_00485b40` at `0x00481a2b`), so none is there to take the press. The eject button is `ParkViewfinder`'s, `Clicked = ParkCamcorderCameraMode.Leave`. A held right button walks:
`ParkCamcorderCameraMode.RightHeld` is the bit, read once a frame after the interface has had the press, and `Walk`
adds the forward key's amount for it. The walk's speed is a chosen 40 units a second for one forward term, where the
original's measures about 21.8, and its keys are W and S, where the original's are the zoom's, Up and Down
(`docs/QUEUE.md` Q25). There is no ride view: walking onto a ride's
entrance, which rides it in the original, is counted instead (`FIRST_PERSON_WALK_INTO_RIDE`, `park-engine.md`).

### A click and a double click: the UI library's `0x10006` and `0x10007`

The base control proc `FUN_0065f6d1` makes both, from the press (`0x10005`, case `0x0065f820`), the move (`0x10003`,
`0x0065fa33`) and the release (`0x10004`, `0x0065f913`). It keeps **one record per button, not per control**, at
`0x00faa598 + b * 0xc` (b = 0 left, 1 right, 2 middle): a state word (0 idle, 1 pressed and still a click, 2 spoiled),
the press point (`+2`, `+4`) and one time stamp (`+8`); and a mask at the control's `+0x11c` of the buttons pressed on
it. The clock is milliseconds of real time: `FUN_0065968e` calls slot 0 of the object at `[0x0077c490]`, which
`UI_Init` sets to the game's (`FUN_00659674` at `0x00489d9e`: `0x007b9864`, vtable `0x006ff178`, slot 0 `0x00489c80` →
`FUN_005f5fa0` → `FUN_005f5f10` on the timer at `0x00fa71e0`: QPC less its start, over its ticks a millisecond, plus
its start's milliseconds, with `timeGetTime` as the fallback). Nothing clamps or holds it. Measured in the original,
the game's process stopped 600 ms between a right press on the first-person view and its release, the press given 120
ms to be handled first: no click, twice, and a quick click after it left; the park's quick click the same way, stopped
400 ms, no click, three times. (A press sent 20 to 40 ms before the stop is handled after it, at 30 frames a second,
and then clicks: that measures the stop, not the clock.) OpenTPW's is `Time.WallMilliseconds`. A control's timer
runs on the same clock (`FUN_0065ef90` arms one, `FUN_00661fe5` fires it on `FUN_0065968e` differences): nothing
until a whole period has gone since its stamp, then message `0x10` once for each whole period gone, and the stamp
left the remainder behind now, so a late pass does not move its phase. `FUN_006622d2` runs every timer, and none
while `[0x00faa638]` is set: `FUN_00662411` sets it, called as a message box, the game menu and the options screen
open, and by three more (Q238): the park's map as it opens (`FUN_005f0b40`, `0x005f0b8a`, let go by its closer
`FUN_005f1130` at `0x005f121b`), and the pause overlay's two doors. `FUN_0048a6e0` (`0x0048a6e9`) holds and puts the
overlay up (`FUN_004a2790`, tree `0x00752e00`); its one caller is the pause `FUN_004092a0` when its first argument is
1, which only the window losing the focus passes (`WM_ACTIVATEAPP`, `0x0046b74c`); `FUN_0048a700` lets go and takes
it down on every resume (`FUN_00409300`, `0x00409315`). `FUN_0048a720` (`0x0048a72e`, `0x0048a733`) toggles the
overlay and holds or lets go with it, from `Game_TogglePause` (`0x0040935e`), which the overlay's own proc calls on a
key let go (`0x004a2706`). The overlay is `UI_LoadModalTree( 0x00752e00 )` (`0x0047ed80`): the LOLIGHT dimmer and one
control, 5094, at (174,500)-(1874,900), wearing `s_paused` (`0xa7a845bc`, the language folder's `paused.MD2`,
`park-engine.md`, the end of a park), which its proc's message `0x15` sets moving for ever by table `0x00752ed0`, a step
every 35 ms about (937,450) (`FUN_004a2680`, `0x004a2745`). The key is P: its row `0x00747f3c` calls `FUN_0040bf70`,
which calls `Game_TogglePause( 1 )` (`0x00409350`), and that acts only while `[0x00786b68 + 0x3c]` is 1. Not read: whether
the dimmer takes the pointer, and whether the banner moves while the timers are held. Fork watch 2026-10-24,
`verify-world-other.md` L2 and M2, `verify-audio-ui.md` L1 (Q739). `FUN_00662420` clears
the hold and stamps every timer afresh (`FUN_0066213e`). OpenTPW: `UiTimer`, held by `WindowStack.HoldsTimers` for the
message box, the game menu, the options screen and the map: the visitors list's 2000 ms, an object window's 4000 ms
and the buy screen's 1000 ms. The pause overlay is not built; the focus leaving is counted
(`FOCUS_LOSS_PAUSE_OVERLAY`).

**The buy screen's own timer** (Q238) is `0x80080` at 1000 ms, armed on the screen's message `0x15`
(`FUN_004ac270`, `0x004ac3ee`). Its tick, message `0x10` (`0x004ac2db`), draws the list's rows again
(`FUN_00663324` on control `0x1f8`) and letters the corner, control `0x200`, with the bank's balance: UITEXT `0x1ca`
("Cash  $ ") and the balance, or `0x1cb` ("Cash  -$ ") and the balance without its sign when it is under nought
(`0x004ac31e`). No other arm of the handler writes `0x200`, and the tab builder `FUN_004ab6b0` does not. The opener
`FUN_004acc70` letters it once itself, the same way (`0x004acdae`): measured in the original, the corner reads
"Cash  $ 88112" in the first frame its screen is drawn (`q238/orig-buy-sheet.png`). The timer is made stopped
(`FUN_00661ece`) and started and stamped by `FUN_0065f024` (`0x004ac403`), so its first tick is a second on. That
the number follows the string rests on the frame; the formatter `FUN_006acd60` is not decoded. OpenTPW:
`ParkBuyScreen.Tick` on a `UiTimer`, and the same lettering once as the screen is made. Not read: the two park's-end views' 2000 ms. The limit
`[0x0077c480] = 500`, and the points the interface's 2048x1536 units.

- **The press**, only from state 0: sets the mask bit, keeps the point, state 1. If the control's flags `+0x48` lack
  `0x8` and the press is less than 500 ms after the stamp, it posts **`0x10007`**, the double click, and sets state 2
  (`0x0065f8dd`). Then it stamps the press's time (`0x0065f908`).
- **A move** delivered to the control while its mask is set spoils (state 2) any button in state 1 whose press point is
  **more than 6** away across or down (`0x0065fab7`, `0x0065fadb`), for good. A move is posted to the capture if there is
  one, else to the control under the pointer, and a move onto another control is `0x10002`/`0x10001`, not a move: so
  without a capture only moves over the pressed control are judged.
- **The release**, delivered to the control that took the press wherever the pointer is: in state 1 with the mask bit, it
  posts **`0x10006`**, the click, with the press point, if less than 500 ms passed since the press (`0x0065f969`), and
  stamps the release's time either way (`0x0065f9af`); otherwise it clears the stamp (`0x0065f9bd`), to nought, which
  the next press compares signed: once the clock has passed `0x80000000`, about 25 days up, that press is a double
  click's second (read, not run; OpenTPW keeps no stamp instead). Mask bit and state
  are cleared.

So a click is one press and its release under 500 ms, never having strayed more than 6. A press within 500 ms of the
last clean release, **on any control**, since the stamp is the button's, is a double click's second, and its release
makes no click and clears the stamp, so a third press clicks again. Both messages are queued (`FUN_00658ccc` →
`FUN_006697e3`) to the control whose proc posted them and delivered in the same drain as the release. The window class
has no `CS_DBLCLKS` (style 0, `0x0044e0de`), and by default the buttons do not come from the window procedure at all:
the DirectInput cursor library is on (`FUN_0047e7d0` sets `DAT_007b4b88`, `0x0047e951`, unless `-bwcursor`), and its
callback `0x00486c70` posts the presses and releases in the same units. Its own double click bit (`0x0063446a`) never
reaches the UI, and `FUN_00658ab4`, which would post `0x10007` to the hover, has no caller (dead by CODE).

**A press let go at once is still a press and a release** (Q476). Measured in the original (`q476/orig/run.sh`, round
a): five presses on the gadget's buy button, each sent back to back with its release (one `xdotool mousedown 1 mouseup
1`, nothing between), each opened the buy screen, a new screen read 0.06 to 0.09 s on (`screens.py`'s poll).
How the cursor library's callback gets them (buffered or not) is not read; the measurement is what the build follows.
OpenTPW: `Input.MouseInfo.LeftPresses` and `LeftWentDown` come from the frame's events, not the button's state as the
frame ends; `WindowStack.LeftSteps` gives the stack a press and a release for each press let go inside a frame (and a
release before a press made while it was down), and `Level.WorldClick` acts on `LeftWentDown`. A frame's second press
reaches the interface and is counted at the park (`PARK_PRESSES_IN_ONE_FRAME`).

**The right button's are posted the same way** (Q499). The callback (`CursorLib_ButtonCallback`, `0x00486c70`, made a
function and named this session) takes one event a call: `0xf0` the left, `0xf1` the right, `0xf2` the middle, with
down or up. Each button keeps a latch (`0x007c24dc`, `0x007c24e0`, `0x007c24e4`): a down with the latch clear sets it and
posts the press (`UI_PostButtonPress`, `0x00658af1`, the button as its first argument, 1 the right), an up with it set
clears it and posts the release (`UI_PostButtonRelease`, `0x00658b5b`); a repeat of either is dropped. The point is
scaled to the interface's 2048 x 1536 by the screen's size (`0x00faa5c4`, `0x00faa5c0`). Measured in the original
(`q499/orig/run.sh`, round a, the stock park, RMB cancel `0x0078d911` = 1): one back-to-back right press (`xdotool
mousedown 3 mouseup 3`) left first person three times of three (`helpbar.py` flags `0x202` to `0x0`) and put the armed
patrol tool away (mode `0x0081ae2c` 54 to 0). OpenTPW: `Input.MouseInfo.RightPresses`, and `WindowStack.RightSteps`
gives the stack's right press and click, and `Level.WorldClick`'s quick right click, each step as `LeftSteps` does the
left's. A frame whose quick right click put the hand away hands the park none of that frame's left presses (ours:
which came first is not kept between the two buttons).

### The pointer is drawn last (Q634)

Decoded first-hand (Ghidra, names given this session) and seen in the original's frames. With the cursor library on
(`CursorLib_Start`, `0x0047e7d0`, the default unless `-bwcursor`), the start sets two hooks, `[0x008bd510]` =
`CursorLib_HookBeforeFlip` (`0x0047e9b0`) and `[0x008bd514]` = `CursorLib_HookAfterFlip` (`0x0047e9c0`).
`Display_Present` (`0x00564210`, called from `0x0051b010`, `0x00573cc0` and, through the vtable thunk `0x00573880` ->
`0x00573890` (`mov ecx,[0x00879a24]; jmp 0x00564210`), from a vtable slot) calls the first, then flips (the surface's
`+0x2c`) or blits (`+0x14`) the back buffer, then calls the second. The first is `CursorLib_DrawBeforeFlip`
(`0x005fbd40`): it takes the library's lock and draws the pointer onto the back buffer (through `0x005fb7f0`, which clears
`+0x20` and calls `CursorLib_DrawAt`, `0x005faef0`); the second (`0x005fbd90`) ends the draw (`0x005fb820`), sets the thread's next time and releases the
lock. Between frames the library's own thread (`0x005fba70`) draws it again at most every `0x42` ms, and only when it
has moved or its `+0x2c` is set (read, not run). `UI_RunPass`
(`0x0048a580`) only hands it the shape and the position. So **the pointer goes onto the finished frame, over everything
drawn in it**: the park, the interface, the object windows' model previews, the advisor. The frames agree
(`q634/orig/`): in the Drinks Shop's window and the Belly Bounce's the arrow is whole over the turning model.

OpenTPW: the pointer is not one of the interface's children but `RootPanel.Pointer`, drawn by `RootPanel.RenderPointer`
at the end of `Level.Render`, after the previews, the window's warning and the advisor (it was the interface's last
child, so the previews and the advisor, drawn after the interface, covered it). It hides with the interface under F2.

### Who acts on the click, and who on the release

Decoded for `docs/QUEUE.md` Q123b, first-hand, and run in the original.

**A button does not use the click.** The button class's proc `FUN_00668f9c` answers the left press and release
itself and never hands them to the base proc. The press, from state 0 only, stamps the button's record, sets state 1
and draws it down (`0x10005`). The pointer leaving the button sets state 2 and lifts it, coming back sets 1 and puts
it down again (`0x10002`, `0x10001`). The release posts **`0x100`** with the button's id to its parent when the state
is 1 and `FUN_00668eff` answers true, which is false only when a repeating button (flag `0x20`) has already repeated
(`+0x15a` 2; `FUN_00668dd9` on the tick `0x1e`, delays `+0x13c` then `+0x140`, posting `0x10009` then `0x10008`). No
clock is read at the release: **a button held any length of time is a click**, so long as the pointer is on it when
it comes up.

**Everything else gets the base proc's click**, `0x10006`: the release under 500 ms from the press, not strayed more
than 6. And for the release, the press, the click and the double click alike the base proc also calls
`FUN_0065dcaf`: if the message's own point is inside the control (slot 0 of the region object at `+0x2c`,
`0x0065dcc9`), it posts the same message with **`0x1000` set** to the control itself, carrying the record's press
point. For the click and the double click the two points are the same, so `0x11006` is "a click that began inside
me", and that is what turns `0x10006` into the message the list and the slider answer. (For a press the record still
holds the press before; nothing was found that reads `0x11004` or `0x11005`.)

**The stray is the base proc's, on the move message.** A class whose proc answers `0x10003` itself and returns
is never judged: the list's does (`0x00665d22`), so a press on a list is a click however far the pointer goes.

| Who | Message | What a click does |
|---|---|---|
| a list's rows, `FUN_00665c35` | `0x11006` | the left button: the row click `FUN_006655a2`; any other: `FUN_0066563d`, the `0x402` above; `0x11007`, the double click, has its own arm |
| the plain list class, `FUN_0065bb6b` (made by `FUN_0065ab64`) | `0x11006` | with flag `0x80`, posts `0x200` with the row; without, the row under the point (`FUN_0065b93f`, `0x201`) |
| a slider's track, `Slider_Callback` | `0x11006` | whatever the button: pages towards the point (`Slider_PageTowards`) |
| the lobby's player slots and Quit Game, `0x004a6000`, `0x004a61d0` (type 1 in the stream at `0x00753c68`, not buttons) | `0x10006` | whatever the button: the slot, or the quit question |
| an object window's title, `FUN_0048d2e0` (installed by `FUN_0048d290`, which eight windows call with their title's id; the ride window's is `0x3e28`) | `0x10006` | the left button: slot `+0x38` of the helper kept in the control's slot 0, the thing's rename box (`park-engine.md`, the name boxes). The move turns the title white (`FUN_0065c5d5`) and the leave puts it back (`FUN_00492d20`). OpenTPW counts the click, `RENAME_OBJECT` (Q28) |
| the entry price screen's heading `0x4f3af`, `FUN_00499060` (installed by `FUN_00498d80`) | `0x10006` | the left button: `FUN_004990f0`, the park's rename box; the same white on the move. OpenTPW counts the click, `RENAME_PARK` (Q28) |
| `FUN_004b3c10` (installed by `FUN_004b3cf0`, the staff and visitor locator) | `0x10006` | the left button: `FUN_004b79b0` or `FUN_004b6080` |
| the game menu's rows, `MenuList_ChoiceCallback` | `0x10006` | **whatever the button**: sound `0xc1`, then the choice (`0x100001`) |
| an object window's preview, `0x0048d1a0` | `0x10006` | whatever the button: the camera to the thing |
| `FUN_004b5c80` (installed by `FUN_004b6080`) | `0x10006` | whatever the button: `FUN_0048ce80`, the camera to a thing |
| the gadget's top, `FUN_004a1190` | `0x10006` | the right button only |
| the first-person layer, `FUN_00488a00` | `0x10006` | the right button, with RMB cancel |
| `FUN_004bed60` (two controls of `FUN_004bf460`) | `0x10006` | hides itself |
| `FUN_004c56e0` (five controls of `FUN_004c4fb0`) | `0x10006` | the online globe's chat satellite: `FUN_005b7910( index - 1 )` (`0x004c573f`..`0x004c5751`, once the online manager `0x0048b390`'s four lists are loaded) opens satellite `i`'s park details (`FUN_004c0ae0`, cancel `0x004c4fa0`, success `0x004c0830`); its message `0x1e` calls `FUN_004c4bb0`, which pushes "ONLINE CHATELLITES" (`0x00759924`). Online only (fork watch 2026-10-20, `verify-world.md`, side note) |
| `0x005d58e0` (a control of `FrontEnd_Init`) | `0x10006` | `FUN_004bc710`, or message box `0x1d9`; not identified |
| the two park's-end views, `0x0048a740` | `0x10006` | after their 2000 ms |
| two lobby objects, `FUN_005dd530`, `FUN_005dd840` | `0x10006` | the middle button toggles a flag |
| a third of that kind, the handler at `0x005dced0` (in no function; the slot at `0x00702bdc`) | `0x10006` | the middle button toggles its flag `+0x58` (`FUN_004c57e0`, `FUN_004c5800`) |
| the message bar's ten entries (records of `0x208` bytes at `0x007c26b0`), a handler in no function, the test at `0x0048de67` | `0x10006` | the left button: sound `0xc0`, then the message is shown (`FUN_0048d9c0`), or, if it is the one on show (`[0x007ca090]`), the arm is retracted (`FUN_004a25f0( 0 )`); the right button: an entry other than `[0x007ca094]` is hidden (vtable `+0x14`), and the arm retracted if it was the one on show |

The table is every `CMP` or `SUB` against `0x10006` or `0x11006` in the image: every place the four bytes
`06 00 01 00` or `06 10 01 00` stand in code (a byte search, 14 and 3, the base proc's own post at `0x0065f996`
among them; it reaches the two handlers that sit in no function, `0x0048de67` and `0x005dced0`, which a walk by
function does not), the handlers that reach the message by
a chain of `SUB`s (`SUB 4` after `SUB 0x10002`, or after `SUB 0x10001` and `DEC` as in `FUN_0048d2e0`, `FUN_00499060` and `0x004a61d0`: the lobby's two and the three after them, found by a second scan), and
`FUN_00488a00`, which reaches its case through a jump table. A handler that switches on the message that last way is
not found by either scan, so the table can be short.

**In the original**, predicted first, 6 of 6, read from memory: the left button held 1.0 s on the gadget's Buy, the
buy screen opened as it came up (`[0x007c24c8]` 0 to `0x46bca50`); held 600 ms on a visitors row, nothing
(`0x46cc670` stayed), and a 200 ms click on it opened the visitor's window (`0x46cf4a0`); held 600 ms on the game
menu's Resume Game, the menu stayed (`[0x007c2534]` `0x46c1c30`); a quick right click on it chose it (0), and a quick
left click the same. In the lobby (Q123c, photographed): Quit Game held 600 ms asked nothing, and a quick right click
put the quit question up.

A visitor's window's preview, the same way (Q123c): held 600 ms, the window stayed (`0x46d3b20`) and the camera where
it was; a 150 ms click, the window closed and the camera went to the visitor, (475,175) to (490,280).

**OpenTPW** (Q123c): `WindowStack.LeftClick` keeps the left button's press and stamp as `RightClick` keeps the
right's, on `Time.WallMilliseconds`, and hands a click to the control the press landed on:
`UiControl.LeftClicked`, then `LeftClickedAt` with the press's point (`0x11006`); a double click's second press is
`LeftDoubleClicked` (`0x11007`). A `UiButton` takes no part: `Release` is its proc, with no limit, and its release
clears the stamp; a slider's thumb keeps its press too and leaves the stamp alone (`0x0066bb9b`).
`UiList.LeftClickedAt` selects the row under the point and activates the selected row (`FUN_006655a2` under flag
`0x80`; all seven list records carry it: buy `0x91`, the rest `0x291`, none `0x8` or `0x100`), and
`LeftDoubleClicked` activates it again (`FUN_006656fa`); `UiList.StraySpoilsAClick` is false, for either button.
The original's selected row is the one last hovered, so there a click that misses every row activates that one, and so
it does here, as every list follows the pointer (Q376). A `UiList` made without the flag would count its miss,
`LIST_CLICK_MISS_SELECTION_NOT_UNDER_POINTER`; no screen's is. A screen that answers no activation is counted,
`LIST_ROW_ACTIVATED_UNANSWERED`: the visitors, all-staff and all-items lists, the first of which opens a visitor's
window in the original. The game menu's rows, the lobby's player slots and Quit Game answer `LeftClicked` and
`RightClicked`, the preview `LeftClicked`, and a slider's track pages towards a click of either button
(`UiSlider.LeftClickedAt`, `RightClickedAt`), where it paged on the press. The console's `click` makes the click a
short press would. Not built: a list's column message `0x404`, which no screen answers; the middle button; a
repeating button; a control's flag `0x8`. A button that the pointer leaves and comes back to keeps its press here as
there, but the two are not compared.

### Which window the click sound is for, the scroll bar's arrows and the rename boxes' keys (fork watch 2026-10-19, `verify-audio-fx.md` M1, M2)

The click hook (`0x00485780`, installed at `0x00489da6` through `0x006590ea`) plays on **every** message `0x100` delivered, by the
**receiver's** own style word: `0x0065d57c` reads `[ecx+0x48]` (`0x0065d586`), and bit `0x10` set (`0x004857cb`) gives `Select3`
(`0xbd`), else `BUTTON01` (`0x1f`), through `0x0051bfc0`, which gates only on the sound system being up, not on the pause. The base
control constructor stores the layout's flags at `+0x48` (`0x0065bd2c`) and the parent at `+0x60` (`0x0065bd52`).

- **A list's heading** is a button (op `0xc`, `( 2, 1, n+0x10 )`, `0x006606a8`..`0x006606b2`) whose parent is the list, and every sorted
  list record carries `0x10` (walked `0x00750ab8`, `0x00750ba0`, `0x00750ca0`: type 7, flags `0x291`; buy `0x91`, the rest `0x291`). A heading
  click plays **`0xbd`**. Ours plays `0x1f` (Q711).
- **The scroll bar's arrows** are buttons built under the type-3 bar (op 9 `0x0065dd7b( 3, 0x10, 1 )`, `0x00660495`..`0x006604a1`; ops 6 and 7,
  the arrows, `( 2, 0x21, 1|2 )`, `0x0066024b`..`0x00660257`; op 8, the thumb, `( 2, 1, 3 )`, `0x006602f5`). Every button constructor (`0x006688b3`)
  sets `+0x13c` 500 and `+0x140` 125 (`0x00668902`, `0x0066890f`); only a button with flag `0x20` (the arrows') repeats. The release posts `0x100`
  to `[button+0x60]` (`0x006691e5`..`0x006691f9`) only while `0x00668eff` answers 1, which it does not after a repeat (`+0x15a` 2, `0x00668f18`).
  So an arrow clicked without repeating plays **`0xbd`** (its receiver is the bar, flags `0x10`), and an arrow **held past 500 ms plays nothing**: the
  repeats `0x10009` / `0x10008` are not `0x100`. The thumb and the track post no `0x100`. Q247 builds the bar.
- **The rename boxes' keys.** The edit control sends `0x802` (Enter, only when its own `+0x48` lacks `0x80`), `0x803` (Tab) and `0x804` (Escape) to its
  parent at once (`0x00667fee`: `0x00668020`, `0x00668054`, `0x00668078`). In the two-line boxes (the object's, handler `0x0048d640`, layout `0x00750090`,
  opener `0x0048d370`; the park's, `0x004993e0`, layout `0x00751928`, opener `0x004990f0`), **Enter on the first line (control `0x3943c`) moves the focus
  to `0x3943d` with no click** (`0x00499707`..`0x00499711`), Tab swaps the lines, **Enter on the second line posts `0x100` with -1 and clicks**
  (`0x00499736`..`0x0049973e`), and **Escape closes by `0x00658d9f( window, 4 )`, silent** (`0x004996bf`..`0x004996ca`). Every root carries layout flags
  `0x1`, so the click is `0x1f`. Save Park (`0x0049ea30`) is the one box that clicks on Escape too (it posts `0x100` with -2).
- **The staff rename box.** `0x004b6110` is refused while a box is open (`[edi+0xc]`) or with no selected thing (`[0x007c2658]`), and the thing's class
  byte `[+2]` must be 4, 5, 6, 7 or 8. It opens table `0x007572e8` with callback `0x004b6320` (`0x0065fd0e`), properties 0 = the thing and 1 = the window;
  the title control `0x5daa` reads **UITEXT 195** (`0x004b6226`), the one edit `0x5dab` has buffer `0x007cc360` and a limit of **32** characters
  (`0x004b62c5`), and its first text is `0x00506860( thing )` (`0x004b62ec`). The callback, on message `0x100` with -1, writes through `0x00506830`
  (property 0, the buffer; no length or empty test) and closes; `0x00506830` copies at most 32 wide characters to the thing's `+0x1a0` and puts a
  terminator at `+0x1e0`. The same callback closes without writing on -2 and `0x804`; the verifier's read lists `0x802` among them too, which is not reconciled with
  the Enter site (`0x004b6400`..`0x004b640a`, which posts `0x100` with -1 and clicks). Not read: whether the edit refuses Enter on an empty field. Q28 builds the boxes.

## The other park streams

| Address | What it is | Contents |
|---|---|---|
| `0x00751530` | The **coaster builder** bar (859,916)-(1534,1190) | `cb_incline` 0x19, `cb_loft` 0x1a, `cb_move` 0x1b, `cb_rotate` 0x1c, `cb_swapdown` 0x1d, `cb_swapup` 0x1e, `cb_track` 0x1f, **0x20 with no mesh at all**, `cb_dellast` 0x21, framed by the nine-slice `!f_plain` |
| `0x007514c0` | Finance sub-panel (screen function `FUN_00497b20`) | `b_finance` 0x1b59, `b_loans` 0x1b5a, `b_staffcost` 0x1b5b |
| `0x00751720` | Camcorder / postcard | `b_1person` 0x63 help 479, `b_postcard` 0x64 help 480 |

`0x00751530` is the **coaster builder**, not a build toolbar: help rows 296-304 and 564-569 are *all*
pylons, track, loops and winches ("Left-click to move pylons", "...to build loop pylons", "Loop pylons
must always be placed in pairs").

## The message bar — stream `0x007501a0`

Built by `FUN_0048d8b0`. Walked in full: clean op 0, balanced op 5 at `0x007502ae`.

    0x10d2d  panel  (458,1101)-(1073,1442)   the bar itself
      0x10d2e  button (486,1325)-(589,1428)  mesh mb_erase
      0x10d2f  button (486,1218)-(589,1320)  NO mesh
      0x10d30  button (486,1110)-(589,1213)  NO mesh
      0x10d31  panel  (458,1101)-( 623,1442)  mesh messagel = f_tagl.md2
      0x10d32  panel  (623,1101)-( 835,1442)  mesh messagem = f_tagm.md2  + text rect (609,1118)-(1056,1422)
      0x10d33  panel  (835,1101)-(1073,1442)  mesh messager = f_tagr.md2

`FUN_0048d8b0` skins **0x10d32** only — label skin, **font 6** — so the middle slice carries the text. The
bar sits immediately right of the gadget, which ends at x 439.

### What feeds it

Every message arrives through `FUN_00481580`, whose **one** caller is `CTagSystem::ReceiveMessage`
(`FUN_00509bf0` - its own debug string names the class), and **every line the park advisor says feeds it**: a
said line sends an event of type `0x18` with its response's group, and the group's row in the tag table is a
tag on the gadget's aerial (`advisor-park.md`, "The tag a line leaves on the aerial", which has the table, the
list's rules and the mast's). The other two branches are an event of type `0x19` and the postcard's three ends
(ids `0x17b` / `0x17c` / `0x17d`, "Postcard sent", "Postcard to outbox", "Postcard failed"). `FUN_0048ef70` is
the **save-file loader**, not a live poster - it reads 0x21c-byte records through `FUN_005f5e20`. The cap is
**ten** (`DAT_00750610`, with the string `mb_erase` immediately after it at `0x00750614`). The tags are built
(`ParkTags`), and so is the bar a tag's left click opens (below).

**A screen's advisor line is not posted here.** `FUN_00486b00` runs into the advisor's own message system
(`FUN_0059b590`, `FUN_0059a940`); the bar keeps a separate list (`DAT_007cb2c8`, posted by
`FUN_0048eb20`). They talk in one direction only: showing a bar message (`FUN_0048d9c0`) posts advisor
message **0x11c** and calls `FUN_004a2590` to put the bar on the arm.

State globals: `DAT_007ca098` the bar's tree, `DAT_007ca090` the tag whose message is on show, `DAT_007ca094` the
tag being put on show, `DAT_007ca09c` how many tags are held.

### Showing a message, `FUN_0048d9c0`

A tag's left click (`FUN_0048dae0`, `0x10006` with button 0) plays sound `0xc0`, and then: the tag on show
(`[0x007ca090]`) folds the arm away (`FUN_004a25f0( 0 )`); any other is handed to `FUN_0048d9c0`:

1. Where the tag's byte `+0x134` is set (its timers run) and its second button is not `0x1b`, timers `0x80080`
   and `0x80081` are stopped and the byte cleared, and its particle killed. **A tag once opened does not time
   out.** A tag whose second button declines keeps its timers.
2. The bar's data words are written from the tag's: the text (the tag's word 2 to the bar's 1), the thing (3 to 2),
   the first button's type (5 to 3), the second's (6 to 4), the item (8 to 5).
3. `FUN_004a2590( bar, 1 )` asks the arm for the bar, `[0x007ca094]` is the tag, and advisor message `0x11c` is
   posted (`FUN_00486b00`).

**A tag's effects follow its visibility** (fork watch 2026-10-19, `verify-audio-fx.md` M3): message `0x11` is the visibility notice (the setter
`0x0065d90b` sends `0x11, shown` to the control's proc, `0x0065d998`). With shown set and property 0xa unset (`0x0048dfae`) `0x00486b90( 0x5a, tag )`
(`0x0048dfbe`) spawns the first-show effect at the tag's middle, only when the tag is visible (`0x0065d773`) and not paused (`[0x00786b84]`, `0x00486bce`),
the handle not kept; property 0xa becomes 1 (`0x0048dfcc`). A new tag resets property 9 to -1 and 0xa to 0 (`0x0048ed71`..`0x0048ed82`) before posting
`0x100001`. On every show and hide the live **0x5b** handle (property 9, `0x005222e0`) is shown or hidden (`0x0051ff10( h, 0 )` at `0x0048dff9`,
`0x0051ff10( h, 1 )` at `0x0048e02e`), and message `0x13` moves it onto the tag's middle (`0x0051fe30`, `0x0048df88`). Q463.

### The bar's handler, `FUN_0048e2f0`

| Message | What it does |
|---|---|
| `0x1001d9`, the arm starts out with it | the middle panel's text is the bar's word 1; `FUN_0048e610` dresses `0x10d30` by word 3 and `0x10d2f` by word 4; `0x10d2e` wears `mb_erase` and is shown, unless word 4 is `0x1b`, when it has no mesh and is hidden; each button's depth goes up one; `[0x007ca090]` takes `[0x007ca094]`; and the stream at `0x007502b8` is played on that tag at 100 (`FUN_0065e72d`) |
| `0x1d` with 1, the arm fully out | `[0x007ca094]` is cleared |
| `0x1001d8`, the arm starts in | the tag's stream is stopped (`FUN_0065e897`), advisor message `0x11c` is withdrawn (`FUN_00486b40`), `[0x007ca090]` is cleared |
| `0x100` from `0x10d2e` | the tag on show is hidden, the mast is posted `0x100002` with it and nought (off half a second on), and the arm folded away |
| `0x100` from `0x10d30` or `0x10d2f` | `FUN_0048e0f0( type, thing, item )`: on an answer of 1 the arm is folded away; on 2 the mast is sent `0x100002` for the tag and the arm folded away; on nought nothing more |

**A button's look, `FUN_0048e610`** (the type, the mesh's node, the help row put in the control's `+0x90`): 3
`mb_locate` `0x204`; 4 `mb_locate` `0x203`; 9 to `0x11` `mb_buy`, `0x200` and the type; `0x12` `b_loan` `0x212`;
`0x13` `mb_resrch` `0x213`; `0x14` `b_pkinfo` `0x214`; `0x15` `b_staffcost` `0x215`; `0x17` `mb_callmech`
`0x217`; `0x18` `b_entry` `0xec`; `0x1a` `mb_accept` and `0x1b` `mb_decline`, no row; `0x16` leaves the button as
it was; any other type takes the mesh off and hides it. Rows 515 and 516 both read "Left-click to go there".

**A button's act, `FUN_0048e0f0`** (its answer in brackets): 3 `FUN_004867b0( thing )`, the camera to the thing's
cell (0: the bar stays); 4 `FUN_004868d0( &item )` (0; not read); 9 to `0xc` `FUN_004acc70( 0..3, item )` (2);
`0xd` to `0x11` `FUN_0049bdd0( 0..4 )` (2); `0x12` `FUN_0049fb30` (2); `0x13` `FUN_004aa480`, research (2); `0x14`
`FUN_004a52a0` (2); `0x15` `FUN_004b2750` (2); `0x16` `FUN_004b7b00` (2); `0x17` `FUN_004dfe30( 1 )` on the
thing, the call for a mechanic (1); `0x18` `FUN_00498d80`, the entry price screen (2); `0x1a` and `0x1b`
`FUN_004d2df0` and `FUN_004d2ec0` on the thing `FUN_00519610` answers (2). None but 3, 9 to `0xc` and `0x18` is
read further; 9 to `0xc`, the buy screen on a tab, are in `advisor-park.md`, "A park with no ride in it".
`FUN_00498d80` is the entry-price screen's builder: nothing where the screen is open already (`[0x007ca210]`),
else the screen with the Finance category's last screen noted as 10 (`FUN_004a0810( 3, 10 )`) and its own advisor
message, `0x12f`, posted. On its answer of 2 the bar's handler posts the mast `0x100002` for the tag with nought
(hidden, and off half a second on) and folds the arm away. **Built** (Q319, `ParkGadget.BarButton`): the tag of
the advisor's row 1 carries it (`advisor-park.md`, "A shut park people wait outside"), and in the original its
click read as that: a slot took `0x12f`, the handle went back to 331 and no tag was held 0.53 s on.

**The stream at `0x007502b8`** is seven rows of four shorts, a change of the control's left, top, right and bottom:
three of (-2, -2, 2, 2), three of (2, 2, -2, -2) and a jump (`0xd8f3`, -9997) back to the row its second word names.
The tag on show swells two a side a step for three steps, shrinks the same, rests a step and goes round again.

**The window-animation engine** (`FUN_0065e72d` starts a stream on a control, `FUN_0065e9a4` steps it at its interval;
fork watch 2026-10-24, `verify-audio-ui.md` L2): a row of four shorts is a change of the control's left, top, right and
bottom, unless its first is -9999 (`0xd8f1`, the end), -9998 (an angle), -9997 (`0xd8f3`, a jump to the row its second
word names, which costs one interval) or -9996 (`FUN_0065d3a3`). The pivot is the rectangle's right and bottom halved
(`FUN_0065f345`). It moves this tag and the three language banners (`park-engine.md`, the end of a park; the pause
overlay above).

**The aerial's squash** (Q637). A row is applied on the first frame more than the stream's interval after the last
row (or the start: `+0xf4`, compared at `0x0065e9fc`), so never two in a frame; the offsets a stream has added are kept at
`+0xf8` (four shorts) and taken off at its end and when another is started over it (`FUN_0065e72d`); `+0x135` says one
plays, `+0xec` is its row. The streams at `0x007502f0` and `0x00752e48` hold the same fourteen rows: six of
(-2, 2, 2, 0), two of (4, -4, -4, 0), two of (-2, 2, 2, 0), four of (2, -2, -2, 0), and the end: the control's left
and right go out and its top down by 2, 4 ... 12, then 8, 4, 6, 8, 6, 4, 2, 0, its bottom still, a squash and a
spring back to where it stands. They are played as the aerial changes (`advisor-park.md`, "The mast"): a tag put on
plays `0x00752e48` on the red top at 50 ms unless one plays on it (`0x004a135b`); a tag taken off moves each later
tag down at once and plays `0x007502f0` on it at 20 ms unless one plays there, the swell of the tag on show among
them (`0x0048dcda`), and on the red top at 50 ms, started over (`0x0048dd06`). Measured (`q637/orig/`,
`original/aerialpoll.py` beside a `record.sh` film, Q309's broken Belly Bounce): the top read (77, 773, 161, 901) as
the tag was put on, (65, 785, 173, 901) at row 6 and back at rest with no stream 1.00 s on, a row each 66.7 ms (two
drawn frames under the 30-a-second cap); as it went, (77, 848, 161, 976), (65, 860, 173, 976) at row 6, at rest
1.00 s on. **OpenTPW** (`ParkGadget.Squash`) steps the same rows by the same rule on its own frames, so a row comes
each 55 ms or so at its usual frame rate, the whole in about 0.8 s (`q637/run3`).

### The arm's handler, `0x004a14f0`

In no function; a jump table over messages `0xa` to `0x100`. The arm's byte `+0x134` is its state: 0 at rest, 2
going out, 3 coming in, 4 coming in for a panel waiting.

- **`0xa`, out with a panel.** In any state but 0 the panel is kept in the arm's data word 2 and nothing more is
  done. At rest: timer `0x80080` is armed at 20 ms and the arm's `+0x130` set to ten; the panel is linked under
  the arm (`FUN_0065f447`, which gives it the arm's depth and two) and set three under that (`0x004a15bf` to
  `0x004a15cd`, `FUN_0065f16b`): the arm's 4 makes it 3, its parts 5 and theirs 7: the parts behind the retract button
  (7), and all of it behind the body (8) and the handle (9), so a part sliding out is cut off at the body's right edge; its rectangle is put at the arm's left less its own width
  and fifty, its middle on the arm's (`0x004a1613`), and each of its children is shown once its right edge is past
  the arm's left (`FUN_004a1440`); **the step is a tenth of the panel's width, the body's right edge less the
  arm's left, and a hundred** (`0x004a16d7`, a whole division); the retract button `0x24` is switched on; sound
  `0x1c` is played (`FUN_0051bfc0`); and the panel is sent `0x1001d9`.
- **`0xb`, in.** Nothing with no panel carried. With one: the panel is sent `0x1001d8`, the timer is armed again
  (`FUN_0065ef90` answers 1 and resets one already running), the step is a tenth of the arm's left less the handle's
  (`[0x007cb2d0]`), a whole division from wherever the handle stands, so a slide out turns back where it is and an
  `0xb` while coming in starts the ten steps again; the retract button is switched off and sound `0x1c` played; the
  state is 3, or 4 where the message's word is -1. **No sender passes -1** (`FUN_004a2590`, `FUN_004a25f0` and the
  retract button's `0x100` pass nought; nothing else sends the arm `0xb`): state 4 is dead by CODE.
- **Timer `0x80080`.** While `+0x130` is over nought it goes down one and the arm's right edge moves a step; the
  arm's resize (message `0x12`, `0x004a1a0e`) moves the panel as far (`FUN_0065c7fe`) and shows it and each of its
  children where its right edge is past the arm's parent's, the body's 439 (`FUN_004a1440`, which sets every one,
  so a part `0x1001d9` hid is shown again: the bar's button of type nought and a decline's erase button, each with
  no mesh, stand shown and take the pointer once the arm is out). At nought the timer stops: coming in (state 3),
  **the arm is put back at its rest rectangle**, the one the builder stored with the narrowed arm (`FUN_0065cf17` at
  `0x004a23ee` writes the control's `+0x14`; `FUN_0065d4cb` reads it at `0x004a18ef`, `FUN_0065cc04` sets it at
  `0x004a1933`), the panel is hidden and unlinked, sent `0x1d` with nought, and sound `0xc2` played
  (`FUN_00485aa0`, cat_ui flat); in state 4 the same but the arm's parent is posted `0xb` in place of the `0x1d` and
  the sound; going out, the panel is sent `0x1d` with 1. The state is 0, and a panel waiting in word 2 is posted
  `0xa`. The ten steps fire 20 ms apart and an eleventh ends the slide: 0.22 s.
- **`0x100` from `0x24`,** the retract button: `0xb`, or with the arm already coming in the waiting panel is
  forgotten; then control `0x27`, the camcorder button, is put up.

`FUN_004a2590( panel )`: nothing with a panel already waiting; else the arm is sent `0xb` and posted `0xa` with
the panel, so a panel replaces the one carried by the arm coming in and going out again. `FUN_004a25f0( lift )`:
`0xb`, or the waiting panel forgotten while the arm comes in; with `lift`, the camcorder button is put up. The
camcorder panel's own handler (`FUN_00498ad0`) has no arm for `0x1001d8`, so a tag that takes the arm from it
leaves the camcorder button down, and that button's next click, up, folds away whatever the arm carries
(`FUN_00498bd0`).

### The original (`q311/orig/run.sh`, `original/bar.py`; predicted first, seven of seven)

Q265's `bounce.TPWS`, its Belly Bounce broken through its script 20 s after the load; the mechanic's tag came at
24.1 s. Left clicked seven seconds on: `[0x007ca090]` and `[0x007ca094]` read the tag's control; the bar's data
read thing 13, buttons 3 and 0, item 1100 and "Your Belly Bounce ride has broken down, and a mechanic is on his
way to fix it."; its rectangle went from (-334, 1112, 281, 1453) to **(486, 1112, 1101, 1453)** and the handle's
left from 331 to **1151**, in steps of 82 inside 0.2 s; `[0x007ca094]` cleared 20 ms after the last. The tag's
`+0x134` read 0 from the click, and **the tag was still held 70 s after it came**, where an unopened one goes at
30.5 s. Its rectangle went (84, 909, 154, 974), then 2, 4, 6, 4 and 2 wider a side and back, a round every 0.9 s
(a step is 100 ms rounded up to the game's 31 ms frames). `mb_locate` clicked: the view moved onto the Bounce,
the bar and the tag stayed (the frame's help bar reads "Left-click to go there"). The tag clicked again: on show
read 0, the bar and the handle went back in six polls, the tag stayed and stopped swelling. Clicked a third time
and `mb_erase` clicked: the tag's shown bit read 0 at once, none was held 0.53 s on, the arm in. **The camcorder
button** after it: the handle stopped at 851, 520 out, a tenth of (319 + 108 + 100) ten times, where the
stream's rectangles and this game's arm have it 645 out.

### The slide, measured (Q427)

**The original** (`q427/orig/run.sh`, Q265's `bounce.TPWS` off-screen, `bar.py` polling, `record.sh` filming; predicted
first, `q427/PREDICTION.txt`): the camcorder button put the handle from 331 to 851 in steps of 52 in 0.22 s, the arm's
state 2 while it moved, its panel at (482,1206)-(801,1359), and back by -52 in state 3. The mechanic's tag: 331 to
1151 by 82 in 0.23 s, the tag on show and the one being put on show both set as it started and the second cleared at
its end, the bar starting at (-334,1112,281,1453); every one of the bar's six children shown once out, its button of
type nought too; clicked again, back by -82, the tag on show cleared as it started. The film: the arm's green body
squeezed to its rectangle through the slide, the retract cross from its first frame, the postcard button shown a frame
before the camera button. **A turn** (the camcorder button and Ctrl+S within the slide, twice): one step out to 383,
steps of -5 back to 333, 331 at the end, then out by 67 to 1001 with the staff panel at (484,1127): the rest
rectangle, not the tenths.

**OpenTPW** (`ParkGadget.Carry`, `Fold`, `Slide`; `ParkTags.PuttingOnShow`, `Shown`, `FullyOut`; the console's `tags`
reads the arm's move, what it carries and what waits). `q427/confirm.py` on the stock park, paused and stepped 16.7 ms a
frame (`run-fix4`, the final build, nine of nine): the tag's click `Park gadget: arm: Out with panel 10d2d, step 82,
handle 331, sound 0x1c a voice`, the handle 413, 577, 741, 987, 1151 after 2, 4, 7, 10 and 14 frames, the bar hidden
until its right edge passed 439; again, `step -82`, 331 and `sound 0xc2 a voice`; the camcorder button 383, 591, 851 and
its panel at (482,1206); the camcorder button and Ctrl+S three frames on, `arm: In with panel 62, step -10, handle 435`,
331, then 1001 with the staff panel at (484,1127). Side by side with the original's film (`q427/side-by-side.png`) the
bar came out at the same steps but drawn over the body and the retract cross, where the original's passes behind both;
built (Q445, below). Main's build had the handle at 1151 at the click and counted `GADGET_ARM_SLIDE`. The camcorder
button's advisor line `0x131` is built (Q428, below). Not heard in either game. In no game run: a panel refused while
another waits (tested only), Full Simulation (not asked: the arm is either mode's), the other three themes.

### The carried panel's depth (Q445)

**OpenTPW** (`ParkGadget.CarriedDepth`, on the camcorder panel, the bar and both locators' panels; the console's `tags`
reads `depth=` and `behind body N retract N of N` for the carried panel's shown parts, and `retract=on|off|hidden`).
`q445/confirm.py` on the stock park, paused and stepped (`run1`, the final build, five of five): the bar's click
`depth=3`, `retract=on`; behind body and retract 1 of 1, 2 of 2 and 6 of 6 at handles 577, 741 and 987; clicked again
`retract=off` through the slide and `hidden` at 331; the camcorder panel at 591 `1 retract 1 of 1`; the staff panel
out `behind body 6 retract 1 of 6`, its five buttons tied with the retract button at 7. Beside the original's film
(`q445/side-by-side.png`; the slide in: `q445/run1/3-in-1.png` beside `handoff/review2/q427/rev-orig-bar-in-f187-196.png`): the bar's text cut at the body's right edge and the cross over the bar (its f073 to f075),
the postcard button half behind the body (its f323), the cross drawn through the slide in (its f188 to f193). Main's
build drew the bar over the body and hid the cross at the click. In no game run: the original's locator panel
mid-slide, so the order at a tie of depth; Full Simulation (not asked: the arm is either mode's); the other three themes.

### OpenTPW (Q311: `ParkGadget.OpenTag`, `BarButton`, `EraseTag`, `Carry`, `Swell`; `ParkTags.Open`, `Shut`, `OnShow`)

`q311/confirm.py`, the stock park's Belly Bounce broken 25 s in and the mechanic's tag clicked three seconds
after his line, seven predictions of seven twice and the control's one: the log read `Park gadget: message bar:
tag 72 subject 13 item 1100 bar: out=1 rect=486,1112,1101,1453 arm=331,1069,1181,1495
handle=1151,1058,1304,1503 first=mb_locate/help=516 second=hidden erase=mb_erase/help=-1 camcorder=0` and the
text; `tags` read `timers=0/open=1`; the text's box in the frame went from 0.6% black to 94.8% with white
lettering in its top third; 40 s after the tag came it stood with the bar out; with the camera sent away,
`mb_locate` logged `Camera: to thing 13 on cell (51,23), point of interest (510,230)` and left the bar; the tag
clicked again read `bar: out=0` and `handle=331,1058,484,1503` with the tag standing; opened a third time and
erased, `tags` read `hidden=1` and `out=0` at once and `standing=0` 0.8 s on. `side-by-side.png` holds the
original's frame, this game's and main's. Main's build counted `TAG_MESSAGE_BAR` at the click, drew nothing and
its tag was gone 33 s after it came.

| | The original | OpenTPW |
|---|---|---|
| The arm's way out and in | ten steps 20 ms apart | the same on the frame clock (Q427); a tag's right click refused while it is the one being put on show; a panel asked for while the arm moves waits, and a third is refused |
| The arm's travel | a tenth of the panel's width and 208, ten times: 820 with the bar, 520 with the camcorder panel | the same (Q427) |
| The arm's sounds (`0x1c` as it starts, `0xc2` as it is in) | played | played, cat_ui at the effects' own volumes (Q427); not heard in either game |
| The panel's children as it comes out | each shown as its right edge passes the body's | the same (Q427) |
| A carried panel's depth | 3, its parts 5 and theirs 7: behind the body and the retract button | the same (Q445); a locator's buttons, at 7 under their group, are drawn after the retract button, the order at one depth not decoded |
| The retract button through the slide in | switched off and drawn | the same, but it stops the pointer (Q445) |
| The bar's text | the middle panel's, over the right slice | the same lettering; the right slice is put in the tree before the middle one so that it is drawn under it. At 1280 x 720 font 6 is a smaller face than the original's at 1024 x 768, so the line breaks fall later |
| The two stacked buttons | 21 types | dressed for every type whose mesh is a file of `ui.wad` (not `b_loan` and `b_pkinfo`, nodes of files not looked up); types 3, 9 to `0x11` and `0x18` act (`advisor-park.md`, "A park with no ride in it", "Worn rides and no mechanic"), the rest counted `TAG_BAR_BUTTON` |
| Advisor message `0x11c` | posted with the bar, withdrawn as it leaves | the same (`advisor-park.md`, "The line of an opened tag message") |
| The tag on show | swells by the stream, a step every 100 ms on the frame's clock | the same steps on real milliseconds, so a round is 0.7 s where the original's frames make it 0.9 |
| The buttons' depths | one more at every showing | not kept |
| A tag opened, in a park file | written with its whole life, and a load starts its timers again | the same (`ParkTags.Restore`) |

### The camcorder button's line (Q428)

**The executable.** Three places name message `0x131`, all through `FUN_00486b00` (post) and `FUN_00486b40` (withdraw).
The camcorder button's down, `FUN_00498bb0` (from `FUN_004a0840`'s case `0x27` with 1): `FUN_004a2590( panel, 0 )`,
then the post, whether or not the arm took the panel. Its up, `FUN_00498bd0`: `FUN_004a25f0( 0 )`, then the withdraw.
The panel's own handler, `FUN_00498ad0`, on any `0x100` (99 the camera, `FUN_00481a10`; 100 the postcard,
`FUN_004a9380`): the button's act, the withdraw, then `FUN_004a25f0( 1 )`, which lifts the camcorder button. Its
`0x14` withdraws nothing, so a tag taking the arm from the panel leaves the line be. The fourth hit of the bytes
`PUSH 0x131`, `0x004c0211` in `FUN_004bff70`, is a string id for `FUN_00485b00`, not a message. Row 305 of the metadata
table (`meta_full.json`): posted only, category 1 (the Tutorial's: said once, gone after three cuts), one line,
response 565, sample 583 (9.0 s, "the buttons on this panel let you use camcorder mode ... send email postcards"),
group `0x17f` (no tag).

**The original** (`q428/orig/run.sh`, `q325/stock.TPWS` off-screen, `advisor.py 0x131`; predicted first, five of five):
the button down put `0x131` in a slot on 872 and said it on 873, line 0, beside the panel (`a-1-line.png`); up 3 s in
cut it, slaps 1; down said it on 895 and the camera button cut it, slaps 2; said on 941 and cut by the up, slaps 3;
the fourth down took no slot. The postcard button was left out: it pauses the game and writes a picture.

**OpenTPW** (`ParkAdvisorMessages.CamcorderPanel`; `ParkGadget.ShowArm`, `EnterCamcorder`, `SendPostcard`).
`q428/confirm.py` on the stock park, real clicks (`run-fix2`, the final build, five of five): `post message=305
subject=0 score=1000 threshold=25 tick=790 result=queued`, said at once, response 565, sample 583, beside the panel
(`1-line.png`; side by side with the original's, `q428/side-by-side.png`); the up `withdraw message=305 cleared=0
cut=1 slaps=1`; the postcard button `... slaps=2` with `POSTCARD_BUTTON` counted; the camera button `... slaps=3`; the
fourth down `result=slapped`. Main's build counted `CAMCORDER_PANEL_LINE` and posted nothing. In no game run: a tag
taking the arm from the panel and a panel refused (tested only), the line let end and said once, Full Simulation (not
asked: the button is either mode's), the other three themes. Not heard in either game.

## The help rows, from the game's own UIHELPTEXT.str

Independent confirmation of the whole decode, from a different source than the disassembly — the wording
matches `FUN_004a0840`'s dispatch button for button, and names both readouts:

    469  Left-click to buy and build attractions or to hire staff   b_buy
    470  Left-click to view information screens                     b_info
    471  Left-click to view finance screens                         b_money
    472  Left-click for research (R)                                b_resrch
    473  Left-click to view the map (Space)                         b_map
    474  Left-click to use camcorder mode or send a postcard        b_camera
    477  Happiness of the park visitors                             the `guage`
    478  Current date                                               the `date` readout
    481  Left-click to close this arm                               b_retract
    494  Research is not available in Instant Action mode
    468  Left-click to exit camcorder mode (C)                      b_eject
    479/480  camcorder (C) / send a postcard (Ctrl-P)

`UIHELPTEXT.str` has **589 rows**. Dump them with the local `strdump` harness (`dotnet run --project <strdump> -- <data dir> <file.str> <lo-hi|n>...`;
its path is in `CLAUDE.local.md`), which mounts the file system first.

### Help ids are UIHELPTEXT.str, not UITEXT

Op 0x11's value is a row of **`Language/English/UIHELPTEXT.str`**, read through `Localization.Help( id )`.
**It is a different file from `UITEXT.str`**, which is what the `UIStrings` enum numbers. "help 469 on
`b_buy` vs `UIStrings.ParkClosed = 469`" is **not** a contradiction: the two
tables simply share index space.

**Reading a `.str` outside the game needs the file system mounted FIRST.** `BFSTReader` opens the character table
of the table's own folder (`Language/<folder>/MBToUni.dat`, English for a `StringFile( Stream )`), kept once per
folder and opened through `FileSystem.OpenRead` on the first character decoded. A `StringFile( Stream )` constructor does **not** avoid it, because it decodes as it constructs.
Assign `FileSystem = new BaseFileSystem( <data dir> )` before constructing anything. Otherwise the first read
throws, and with stderr filtered that looks like "the rows are empty".

### The help bar holds one posted row (Q473)

The bar is the UI library's help sink `[0x007ca0c0]` (vtable `0x006ff598`, built by `FUN_00492470`, which registers it
at `0x0077c48c` through `FUN_006595b3`), its label `[0x007ca0d8]` (shown by bit 0 of `+0x48`), its switches `+4` (set and
cleared by `FUN_004922e0` and `FUN_00492300`) and `+0xc` (built), its control `+0x14`. Whatever posts last stands; there is no second row underneath:

| Poster | What it does to the label |
|---|---|
| A control's `0x10001`, the pointer coming on (`FUN_0065f6d1` `0x0065fa15`: sink `+0x24`, `FUN_00492160`) | its row, asked by message `0xc` (`+0x90`, `FUN_0065bc70`; -1 is the empty string), through `FUN_00492070`; an empty row hides it |
| A control's `0x10002`, the pointer leaving (`0x0065fa2b`: sink `+0x28`, `FUN_0066a0bb`) | hidden (`FUN_00492130`) |
| `FUN_004924b0( text )`: the park's hover, the buy screen's `FUN_004abc80` (below, "The buy screen's help rows"), the online visit's poster (`0x004bd920` to `0x004bd9d4`, below, "The online visit's help rows") | the text, or hidden when it is empty |

The park's view is a control too (`[0x007cb2ac]`, `FUN_00489f50`; its row -1). On its `0x10001` `Park_MouseMessageProc`
runs the hover once, sets the category nought (`0x0048884e` to `0x00488870`), and passes the message to the library
(`0x004889b6`). What the pointer coming back from a control finds is in `park-engine.md`, "The hover after a return to
the view". Measured in the original (`q473/orig/`, `PREDICTION.txt`; the stock park, 1024 x 768, the Buy button at
(113, 590)): over ground "Left-click to build path"; on Buy its own row 469, the sink's `+6` 1 and its control the
button's; from Buy onto a hoarding with the ground left last, 441 again; with the hoarding left last, the label hidden
(shown 0) and still hidden 1.5 s on. With the pointer on ground below the entry-price screen, G opened it and the row
stood, and stood after Escape. Here
`WindowStack.HelpRow` is the one row: a change of what is under the pointer (`WindowStack._pointed`: a control, a modal
window's, a park screen's body or the cover, or the view) posts the control's row or -1, then the view's own post
(`WindowStack.WorldHelpPost`, the park's `Level.HoverPosted`) is taken while the pointer is on the view. Ours posts a
control's row again when it changes under a resting pointer. The original posts the hovered control's row again when a
button whose flags (`FUN_0065d57c`) hold `0x10` is let go: the button class's proc, message `0x10004` (jump table
`0x0066938c`, case `0x006690d6`), calls the sink's `+0x1c` at `0x00669172`, which is `0x00492320` and re-runs
`FUN_00492070` for the sink's control (`+0x14`) while `+4` is set. Whether it posts again at a change made without a
release is not decoded. Measured here
(`q473/run1`, 10 of 10, and main's build `q473/ctl1`, which kept 441 over the hoarding and emptied the bar under the
open screen).

### The help bar's lettering (Q484)

`HelpBar_Build` (`0x00492180`) builds the bar's label
(`FUN_0065dd7b( 1, 1, 0xfff355e4, 0x23e, 0x5f0 - h, 0x5c2, 0x5f0 )`: x 574 to 1474, its bottom at 1520, `h` the help font's line height times `0xc00` over the screen height, plus 10, so
two lines and ten units) with a `LabelSkin`, then `UI_SetTextAlign( 1, 1 )` (`0x0065c428`) and `UI_SetTextWrap( 1, 1 )`
(`0x0065c4b9`), font slot 7, colour 255, 255, 255, 255 and the mesh `f_helpbg`. `UI_SetTextWrap` posts `0x3000` with its
two bytes at the message's `+6` and `+7`; the skin's `FUN_0048fc40` stores them at `+0xda` (`0x1000`) and `+0xdb`
(`0x2000`). `LabelSkin_Draw` (`0x0048f830`) reads `+0xda`: nought, and the box it hands the renderer is `0x4000` pixels
wide (centred on the label for the centre code), so nothing wraps; one, and the box is the label's own width `+0x3c` in
the screen's pixels, less the left inset `+0xde` (nought here; the constructor writes it). `LabelSkin_Construct` writes
`+0xda` nought, so a label wraps only where its builder says: 26 calls of `UI_SetTextWrap` in 19 functions do (the message
box's `MessageBox_Open` and `NewPlayerDialog_Open` among them; which of them pass 1 is not read). So **the bar wraps at
its own width**.

Both posters set the down byte by the text: `HelpBar_PostText( text )` (`0x004924b0`) and `HelpBar_PostControlRow`
(`0x00492070`, the control's row) call `WideStrChr( text, 10 )` (`0x0067c2f0`), and `UI_SetTextAlign( 1, found == 0 )`:
**a text with a line break of its own stands at the top** ( 1, 0 ), any other in the middle ( 1, 1 ), centred across
either way. The block's height is the line count times the line height ("How a list's cells are aligned", below), so a
wrapped two-line row in the middle sits about five units under the strip's top, and a row with a break at the top of it.

**Where a line breaks** is the renderer's word-wrapper `TextLayout_WrapParagraph` (`0x006aec80`), over the glyph records
`TextLayout_LayGlyphs` (`0x006ae930`) lays out (16 bytes each: flags `+4`, the pen's advance `+8`).
`TextLayout_CharFlags` (`0x006aeab0`) gives the flags by the character: space and tab `0x12` (`0x10` a place to break
before, `2` blank), line feed `4`, carriage return `0xc`, anything else 1. `TextLayout_PlaceBlock` (`0x006aeb10`) ends a
paragraph after each glyph flagged 4 (with the next too where it is flagged 4 and 8) and wraps each one:

1. The advances are added from the line's start, the flag-4 glyphs counting nothing. The glyph that takes the sum past
   the box width `+0xa8` (greater than it, not equal) ends the line.
2. If it is the line's first glyph, the line is that glyph alone. Otherwise the wrapper steps back from it, to the
   line's second glyph at the furthest, for one flagged `0x10`: found, the line ends before it and the blank glyphs just
   before it are left off the line, though never its first (`0x006aecff`); none, the line ends before the overflowing
   glyph, cutting the word.
3. The next line starts at the break and skips blank glyphs (`0x006aed1a`); the line is added at `0x006aed2d`.

Each line is added by `FUN_006add90`, which keeps the widest in `+0x80`. Measured in the original at 800 x 600
(`q484/orig/`, `PREDICTION.txt`; the stock park off-screen): over the gate 459 on two lines, broken before "ticket"
(`a-1-gate.png`); over the Belly Bounce 550 broken before "CTRL" (`a-3-ride.png`); over the path one line in the middle
(`a-2-path.png`); the land rows' own two lines stand at the strip's top (`q495/orig/a-O10-buyland.png`).

**OpenTPW** (`HelpBar`: `TextWraps`, `TextDown` by a line feed; `UiText.Wrap`, the one wrapper every wrapping control
uses): the same breaks, at the strip's width in the font's pixels (`(int)` of the drawn width over the font scale, 351
at the 1280 x 720 window's set; the original's `+0x3c` is (x1 - x0) x the screen width / 2048, cut toward nought (`0x0048f47b`): 351 here too). The console's `helpbar` prints the
lines as last drawn. Measured (`q484/run2`, 4 of 4; `side-by-side.png`): 459 and 550 broken as the original's, every
letter inside the strip, 538 at its top; main's build (`q484/ctl1`) set both long rows on one line past both ends.

### The buy screen's help rows (Q495)

The buy screen's handler `FUN_004ac270` ends its frame message `0x1e` (`0x004ac465`) with `FUN_004abc80( &[0x007cc1e8] )`
whenever that word is not nought. `[0x007cc1e8]` is the item the list last selected: the `0x401` arm writes the row's id
there whatever else it does (`0x004aca3e`, after the footprint's wait; an index of -1 writes nothing), so the fill's
pick of the first row sets it too ("The first row is picked as a list fills"), and the opener noughts it with
`[0x007cc1e0]`, `[0x007cc1e4]` and `[0x007cc1f0]` before it builds the screen (`0x004acd72` to `0x004acd86`; an opener
that finds the screen up skips that). `FUN_004abc80`:

1. Copies the list's region (`0x1f8`'s `+0x2c`, `FUN_0065d4f6`) and tests the pointer `[0x00faa540]`, `[0x00faa544]`
   against it (`FUN_00659e83`: left and top in, right and bottom out). Off it, `[0x0075513c] = -1` and nothing is posted.
   The region is the **row area**, op `0xa`'s (1037,427)-(1727,871), not the list's whole rectangle: measured in the
   original (`q495/orig/`, round b), the state went to -1 at each of four points just off the row area and to the item's
   at four just on it, and to -1 over the tabs and the headings (round a), which stand inside the list's rectangle.
2. A land row (-1, -2): if `[0x007cc1e0]` already holds it, nothing (whatever the state); else state 4, the word
   set, and UIHELPTEXT `0x21a` (538, "Left-click to buy land / The cost shown is per tile of land") or `0x21b` (539,
   "Left-click to clear land / There is no cost for this"). So a land row is posted once for as long as it is the item.
3. An item: the state is, for a ride whose `UsageInfo.GoldenTicketCost` (`+0xc4`) is above nought and that the player
   has not unlocked (`FUN_004d4b70`: the mystery row's test), 0 where the cost is at most the tickets the player has left
   (`FUN_004d4b90`, `FUN_005af5d0`: the tickets earned, `0x005af530`'s count, less `+0x1c`, those spent) and 1 otherwise;
   for any other item 3 where the control record's price `+0x04` is at most the bank (`FUN_005195d0`'s thing `+0xc`,
   the balance the screen's corner letters) and 2 otherwise. The same item in the same state posts nothing; else the
   two words are set and the row posted:

| State | Row | Text |
|---|---|---|
| 0 | `0x97` (151) | "Left-click to buy this item" |
| 1 | `0x98` (152) | "You don't have enough golden tickets to buy this item!" |
| 2 | `0x96` (150) | "{P0}You can't afford to buy this item!" |
| 3 | `0x94` (148) | "{P0}Left-click to buy" |

**`{P0}` is the item's kind, not its name** (`0x004abe7f`): row `item+0x90` of the table `[0x0078bad0]`, which the boot
loads as `mObjectTypeNames` (table `0x3f9`, `FUN_00419710` at `0x00419db1`), `Language/English/ITEMTYPES.str`, 26
rows, row 0 empty. `item+0x90` is `Info.RideTypeStringIndex` (FileFormats, the item description files): the Belly
Bounce 7, "Bouncy Castle", the Crazy Ape 8, "Pirate Ship", the Jungle Spray 19, "Shooting Gallery"; no shop or feature
sets it, so theirs is row 0 and the bar reads "Left-click to buy" alone. The kind runs into the text with no space. All
measured in the original's frames (`q495/orig/a-bars.png`, `b-bars.png`): "Bouncy CastleLeft-click to buy", "Pirate
ShipYou can't afford to buy this item!", "Shooting GalleryYou can't afford to buy this item!", "Left-click to buy".
Row `0x95` (149, "{P0}Left-click to buy / This item was recently researched!") is not posted here.

Because a post is made only on a change and the bar holds the last one, what stands depends on the way the pointer
came. Measured in the original (round a, the stock park with the bank written to 1500, `q495/cash1500.TPWS`): over
the Name heading after the screen opened, the heading's own row (off the region; the first row, Aztec Mayhem, was
already the item); onto Belly Bounce's row 148, Crazy Ape's 150; a tab's own row over the tab, and the row again on the
way back; off the list nothing, and the row again on the way back; Buy Land's 538, then off the list and back onto
it the bar **empty** and the state -1; Clear Land's 539. After a click on the features tab the bar was hidden, the item
the fill's first row (Buy Land) and the pointer still on the tab; why the tab's own row was not shown after its click
is not looked into.

**OpenTPW** (`ParkBuyScreen.HelpPost`, asked by `WindowStack` after the control under the pointer has posted, the
`UiWindow.HelpPost` hook): the same states and rows, the kind by `ParkBuyScreen.TypeNameOf`, the price the item file's
as the list's (the record's `+0x04` agrees in all 50 shipped records), the tickets `Players.TicketsUnspent`. Its list
picks no first row as it fills (Q232), so nothing is posted before the pointer has selected a row. A row's own
two lines stand at the top of the strip, as the original's (Q484, above). Measured
(`q495/run1`, 15 of 15, `side-by-side.png`): every row's words as the original's frames.

### The online visit's help rows (Q496)

The poster at `0x004bd919` is not the camcorder's: it belongs to the **online visit**, game type 1, and no offline park
builds it. `Game_StateMachine`'s park load calls `FUN_0048a410` (through `0x00481420`, `0x0054ed1e`) when `[0x00fb3b7c]`
is 1 and `FUN_004813e0` (the park's interface, `FUN_00489f50`) otherwise; `FUN_0048a410` is the only caller of
`FUN_004bf460`, which loads stream `0x00758ae8` into `[0x007cc8bc]` and builds the full-screen control `[0x007cc8c0]`
(`FUN_0065dd7b( 1, 0, 0xfff04d2a, 0, 0, 0x7ff, 0x5ff )`), both with the handler `0x004bd4a0`, in no function. Those two
words and the state `[0x007cc8e4]` are written only there and in the handler (the two noughted on their `0x14`). That interface has no park view (`[0x007cb2ac]` stays
nought, so `FUN_004814a0` sets its cursors instead); `FUN_004bd090`, `FUN_004bd260`, `FUN_004bcf50`, `FUN_004bce80`, `FUN_004bcef0` (called at `0x004a7e51`; it picks the layer by
`[0x007cc698]`, `0x004bcf18`/`0x004bcf32`, then noughts that word) and the handler `0x004bd4a0` itself (`0x004bde21`..`0x004bde44`, by `[0x007cc8b4]`)
set property 6 of the two layers (`FUN_00658d9f`), and the frame below runs on the one `FUN_0065d57c` finds shown.

On a frame (`0x1e`) of whichever layer is shown, the handler works out a state (`0x004bd74f` to `0x004bd902`):

1. Nought unless the pointer is on one of the layers: `0x10001` sets `[0x007cc8dc]` (the tree) or `[0x007cc8e0]` (the
   control) and noughts the state, so the next frame posts again, and `0x10002` clears the flag (`0x004bda03`,
   `0x004bda40`).
2. 2, a ride the visitor can take, when the cell under the pointer `[0x007b05cc]` holds a thing (`FUN_00527e80`) of type
   3 whose `FUN_004e2710` word is nought and whose `FUN_004dd4e0` record has `+0x4ac` nought, or 3 with bit `0x10` of
   the thing's `+0x32`; the thing is kept in `[0x007cc8e8]`.
3. With the current mode's type 9 (the camcorder's pick), 3, or 4 where step 2 found a ride; otherwise 1 where it found
   none.

A change of state posts (`FUN_004924b0`, the text `FUN_006acd60` lays from table `[0x0078ba98]`, `UIHELPTEXT.str`) and
sets the layer's cursor (`FUN_0065f11d`):

| State | Row | Text | Cursor |
|---|---|---|---|
| 0 | none | the bar hidden | 0 |
| 1 | `0x191` (401) | "Left-click to walk to this point / Double-click to jump to this point" | 0 |
| 2, 4 | `0x244` (580) | "{P2}: Left-click to ride", `{P2}` the thing's name (`FUN_00486280`) | `0x13` |
| 3 | `0x243` (579) | "Left-click to use camcorder mode here" | `0x13` |

A left press (`0x10005`, button 0) with the state 2 and a mode other than 9 rides the thing (`FUN_004e15b0( 0 )`,
`0x004bdb34`).

**Measured in the original** (`q496/orig/`, `PREDICTION.txt`; the stock park off-screen, `helpbar.py`'s `cam` fields): in
orbit, in the camcorder's pick (mode vtable `0x6fead0`) and in first person (round b, the pick's click on the path at
(290, 395), `gui_CameraFlags` `0x202`, mode `0x6fea10`) the tree, the control, the state and the thing read nought
while the park's view stayed `0x4641ab0`, and 669,758 reads over a sweep of the pointer in first person found the state
nought throughout. The game type read 2, Instant Action: `loadfile.sh` loads the file into the reference install's
running game. **In the pick and in
first person the bar is hidden** (shown 0, `b-A5-first-person-after.png`); in the pick only until a ride has been under
the pointer (below, "The help bar in first person"). Here the online visit is not built (Go
Online is counted, Q242), so there is nothing of this to build in a Lost Kingdom park. In first person ours, as the
original, runs no park hover, the bar empty and the crosshair up (below, "The help bar in first person", Q498).

### The help bar in first person (Q497)

**The way in.** The pick's click reaches `FUN_0042ab20`, whose entry to first person calls `FUN_00481470( 1 )`, and
`FUN_00481470( x )` calls `FUN_004a2ac0( x == 0 )`: message 6 hides layer 0 (the park's view `[0x007cb2ac]`, the gadget
on it) and shows layer 1 (`[0x007cb2b0]`, `FUN_00488a00`, cursor `0x15`, `c_crosshair.ani`). The way out,
`FUN_0042ab20( 0x40 )`, calls `FUN_00481470( 0 )`, the reverse. `UI_SetVisible` then hit-tests the pointer where it
rests (`FUN_006589f9`): a new control under it is sent `0x10001` and the old one `0x10002`, both queued for the next pump
(`FUN_00658ccc` -> `FUN_006697e3`). A control hidden is also handed at once to the sink's `+0x34` (`FUN_0066a148`), which
noughts the sink's control if it was that one and hides the label. So, on the way in:

1. The bar is hidden the moment the view is, and the sink holds no control.
2. Until the queued `0x10002` reaches the view, its timer can run one more hover, and a row that hover posts stands
   until the pump (measured once: the path's row for 0.3 ms of reads, round b).
3. The view's `0x10002` clears `[0x007c24fc]`, so no hover runs in first person and the category stands where the
   last left it. Layer 1's `0x10001` makes it the sink's control and posts its row, which is none, so the bar stays
   hidden (the sink stays switched on, `+4` 1) and the cursor is layer 1's `0x15`.

**The way out.** Escape, C, the eject button and a right click with RMB cancel set layer 1's cursor to 1 (`c_busy.ani`)
and leave (above, "Four ways out of camcorder mode"). Layer 1 is dropped from the sink at once; on the next pump the
view's `0x10001` (`Park_MouseMessageProc`, `0x0048884e`) runs first: it noughts the thing, runs the hover (when the hover's
guard word `[0x007c24c8]` is nought; the hover puts up its own pointer), sets the flag to 1 and the category nought (`park-engine.md`,
"The hover runs only on the park's view"). Then the base proc (`0x004889b6`) applies the view's cursor, the one this hover
put up, and the library posts the view's own row, -1, which hides whatever that hover posted. The next timer hover posts its arm's row,
a change from nought. Its cell is the one the picker last held, the pick's click's, until the picker reads the pointer's
own place, 367 ms after the swap in both rounds (why it waits is not read); then that place's row.

**The pick posts rows of its own** (mode type 9, `0x00486e37` to `0x00486fcf`). It sets no category and puts up no
pointer (the mode wears `0x13`, `c_cam.ani`). No cell: nothing. A cell with **no thing**
(`0x00486e79`): UIHELPTEXT `0x243` (579, "Left-click to use camcorder mode here") when the last thing was set or the last
category was not nought, and the thing forgotten. A cell whose thing (`FUN_00527e80`) is of type 3, no add-on
(`FUN_004e2710`'s word nought) and whose item record (`FUN_004dd4e0`; its `+0x4ac` is the item's `Info.WhichUIType`, not the
thing's) has `+0x4ac` nought, a ride: UIHELPTEXT `0x244` (580, "{P2}: Left-click to ride", `{P2}` its name, `FUN_004e2780`)
when the thing changed **or** the last category was not nought (the hover noughts `[0x007c24f4]` at `0x00486de5`, and the
category saved at entry is compared, `0x00486eea`..`0x00486efe`). **Any other thing** (another type, an add-on, a shop,
sideshow or feature): nothing is posted and the thing is kept (`0x00487990`, a bare epilogue). Coming onto the view
in the pick, the `0x10001`'s hover posts 579 (the category was the orbit's) and the view's row hides it, so over ground
the bar stays hidden until a ride has been under the pointer.

**Measured in the original** (`q497/orig/`, `PREDICTION.txt`; Q265's `bounce.TPWS` off-screen, `helpbar.py`'s `fp`
fields; R the Belly Bounce, P path, G ground). In orbit at G: the view pointed, the flag 1, cursor 3, 441. The pick at
G: the bar hidden, cursor `0x13`; onto R "Belly Bounce: Left-click to ride" with the thing set; back to G "Left-click to
use camcorder mode here", the category 0 throughout. The click at P: flags `0x202`, layer 1 pointed and the sink's
control, the flag 0, the bar hidden, cursor `0x15`, and 338,155 reads over a sweep changed none of them. Escape (round
a) and C (round b) at G: cursor 1, the swap 29 ms after the key, the view pointed, the flag 1 and cursor 3 a frame
later, the bar shown with the click's cell's row 10 ms after the swap, and G's "Left-click to build path" 400 ms after
the key (`a-bars.png`).

**OpenTPW** has no pick (Q25). First person lays the viewfinder's layer over the view (`UiWindow.LayerCursor`, the
viewfinder's while the mode runs; `WindowStack` takes it for the control under the pointer wherever the eject button is
not): its row is none, so the bar empties and no hover runs, and the pointer is `c_crosshair` (`Level.WantedCursor`).
The eject button posts its own 468 and wears the arrow. Out by C or Escape the view comes back as from a control, so the
hover runs at the last place on the view and posts its row (Q473). Measured (`q498/`, `PREDICTION.txt`): ours 7 of 7 on
the final binary, the crosshair and `help row -1 ""` throughout first person, 468 over the eject button, the gate's 459
after C and after Escape; the original's round c the same layer, the bar hidden and `0x15` throughout
(`side-by-side.png`). Not built: the original's layer wears `c_busy` from the key to the swap (29 ms), and the hover a
timer can still run before the queued `0x10002` (under a frame); ours swaps in the frame of the key.

## The park map — `FUN_005f0b40`, stream `0x00774da0`

**It loads the shipped image *and* renders live over it** — both, not one or the other.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_005f0b40` | — | The map screen's own function, opened by `b_map` | Gadget dispatch |
| `DAT_00f86d20` | — | The static string `2dmap.tga`, constructed at `0x005f0ad0` with a destructor registered through atexit. **Not map logic** — reading it as part of the screen is an adjacency error (`FUN_005f09a0` ends at `0x005f0ac7`, then NOP padding) | Disassembly |
| `FUN_005f1ea0` | — | Builds a path (note the `0x5c` backslash), passes `&DAT_00f86d20` to a vtable method, stores the image's width and height into `+0x48` / `+0x4c` | Disassembly |
| `FUN_005777b0` | — | Rounds those dimensions up to 64 — `(w+0x3f)>>6` — and allocates `tilesX*tilesY` records of 0x60, each a 64x64 surface. For 512x512 that is **8x8 = 64 tiles** | Corroborates that the loaded image is the shipped TGA |
| `+0x60` | — | A **128x128 grid of classification codes**, 8 bytes a cell, in the screen's structure; `+0x60 + 0x20000 = +0x20060` is the selection field | Disassembly |
| `FUN_005f2050` | — | Fills the grid: **9** a cell of track type 25, **10/11/12** mType 1, mType 3 and `mFlags & 0x40` (below), **1-8** things by type (`+0x4ac`) and flags (`+0x32`) | Disassembly |
| `FUN_005f2380` | — | Paints it: a 9-dword (36-byte) blit descriptor per cell, then outlines footprints by comparing each cell with its neighbour above/below (vtable+8) and left/right (vtable+0xc), then draws a selection highlight for the item at `+0x20060` | Disassembly |

The base image is the park's own **`2dmap.tga`**, 512x512 24-bit, one per theme (fantasy, hallow, jungle,
space).

### It is a heat map, and that is the scope trap

`FUN_005f09a0` is **not** a sprite selector — it is a **three-stop colour ramp**: clamp a value to 0-100,
blend stop1->stop2 below 50 and stop2->stop3 above, patch R/G/B into the descriptor and force alpha 0xff.
The stops are `DAT_00f86bd8`, `DAT_00f86bfc`, `DAT_00f86c20` (beyond `.data`'s raw bytes, so a static read gives zeros;
they are filled by the initialiser `0x005f0700`, below). The value is a **per-thing metric**: for cell codes 1, 2, 3 and 8 with the radio (`[map+0x20]`) at 2 it is
`FUN_004e0860` (`0x005f24eb`), at 3 `FUN_004e1e30` (`0x005f2565`); **otherwise, in the radio's default setting, each thing
code 1 to 8 tests its own layer bit (`[map+0x18]`) and is coloured by `__ftol( [thing + 0x44] ) & 0xff`, its state of repair**
(`ride-operation.md`, `park-engine.md`; eight sites `0x005f2602`, `0x005f2681`, `0x005f2705`, `0x005f2784`, `0x005f2809`,
`0x005f288d`, `0x005f290b`, `0x005f298a`, the jump table `0x005f2f9c`; fork watch 2026-10-19, `verify-other-b.md` M2). The ramp
`0x005f09a0` clamps it to 0 to 100 and blends below 50 by `( v << 16 ) / 50`, from 50 by `( ( v - 50 ) << 16 ) / 50`. The first
radio metric is built (`ParkRideScore.ExcitementOf`); the second's days are kept
(`ParkObjectRings.Satisfaction`) and its average is not; the thing layers are unbuilt. The base image plus the cell codes 9-12 (below) is the honest first cut.

### The map's colours are immediates in `0x005f0700`

(Fork watch 2026-10-16, `verify-ui-hud.md` M4; read from the listing, not run.) `0x005f0700` is an initialiser of 36-byte
records (stride `0x24`) from `0x00f869e0`, reached from the data word at `0x0072df04`: eight written byte by byte, then
fifteen through `0x005f0970( this, a, b, c, d )`, which stores the bytes `[c, b, a, d]`, so memory is B, G, R, A for arguments
R, G, B, A. The object ramp `0x005f2380` takes `0x00f86bd8`, `0x00f86bfc`, `0x00f86c20` = (248, 0, 0), (248, 200, 0),
(48, 252, 0): red, orange, green, which reads as R, G, B. Read that way the eight byte-by-byte records are black, white,
black, green (0, 255, 0), cyan (0, 255, 255), orange (254, 78, 0), yellow (252, 255, 3) and brown (110, 66, 11). The fifteen
`0x005f0970` records from `0x00f86b00` (R, G, B, A): (31, 166, 31), (37, 198, 37), (0, 23, 0), (0, 23, 0, 128),
(25, 25, 150, 200), (25, 25, 250, 200), the ramp's three, then (32, 168, 248), (248, 132, 0), (248, 252, 0), (0, 0, 0),
(255, 0, 0), (200, 250, 200). The channel order is inferred, not proven: a photograph of the original's map would settle it.
The thing codes 1 to 8's colour is the state of repair through the ramp above (`0x005f09a0` over the stops (248, 0, 0), (248, 200, 0), (48, 252, 0), pushed at `0x005f08a5`..`0x005f08df`); which record colours which land code or other layer is not traced.

### Controls

Six toggles — **0x982, 0x987, 0x989, 0x993, 0x994, 0x983** — each restored from a bit in `+0x18`
(8, 1, 0x10, 2, 4, 0x20) and each gating one code in the painter: they are **layer filters**. Two radio
groups: **0x98b** with options 0x98e/0x98f/0x98c/0x98d from `+0x1c`, and **0x996** with 0x998/0x997/0x999
from `+0x20` — the **metric pickers**. **0x991** is a label, font 4, **yellow** (0xff,0xff,0,0xff).
**0x990** and **0x995** are display-only (vtable+0x14 with 0), and 0x990's depth+2 is handed to the six
toggles. **0x992** takes handler `LAB_005f0b20` (it calls the viewport's proc `0x005f18d0` on the static); 0x98a/0x984/0x981/0x988
get `FUN_00668f35( 0, 10 )` (`Button_SetRepeatDelays` in Ghidra), a repeating button's two delays (below, "The map's view").

### Sprite sheets

Six, in `data\2DMap.wad` — a whole archive listed beside `data\ui.wad` in the loader's table:
`.\data\2dmap\` + `vsprite/rsprite/ssprite/lsprite/esprite/gsprite.tga`. They are constructed as static
globals — `v` at `0xf86d40`, then `r` `0xf86d54`, `s` `0xf86d68`, `l` `0xf86d7c`, `e` `0xf86d90`,
`g` `0xf86da4` (stride 0x14). `FUN_005f3710` walks `&DAT_00f86d48` to `0xf86dc0` by 0x14 calling
`FUN_0056b380`, which allocates 0x18, constructs, loads by name and caches the handle.

**UNRESOLVED:** that walk starts at `d48`, so it skips `vsprite` at `d34`, and its last record `dac` would
put a string at `0xf86db8` — which the park-engine page already records as the map screen's own ECX. So
the "six records each holding a path at +0xc" reading is wrong somewhere. **Do not build on it — re-derive
the table layout first.**

### It pauses

`FUN_005f0b40` calls `FUN_004092a0(0,0)`, clears `g_ParkRunning`, and calls `Advisor_StopQuietly(1)` — the
*quiet* stop, not the crying one. So unlike the gadget this screen legitimately pauses. It posts advisor
message **0x130** as it opens, and refuses to open at all if the game menu is up (`FUN_0048c8d0`) or if it is
already open.

### The map's view (Q624)

Decoded from the disassembly (`testme.objdump`) and measured in the original's stock park (`q624/orig`, rounds a and b,
`mapview.py`). **Built** (`ParkMapView`, `ParkMapScreen`).

The screen is one static object at `0x00f86db8` (the `ECX` of every call); its constructor (`0x005f0690`) sets the zoom
`+0x5c` to 1.0, and nothing else does, so **the zoom outlives the screen** until the game is quit (the original reopened at
1.5 after a plus).

| Field | What | Written by |
|---|---|---|
| `+0x10`, `+0x14` | the pointer, layout units | the move message `0x10001`/`0x10003` |
| `+0x24`, `+0x28` | the heightfield's cells across and down, 96 and 85 (`FUN_00450b00`: `[[0x007a0854]+0x6c]+0x18`, `+0x1c`) | the opener |
| `+0x48`, `+0x4c` | the picture's size, 512 and 512 | `FUN_005f1ea0` |
| `+0x54`, `+0x58` | the centre, in cells, floats | below |
| `+0x5c` | the zoom, 1.0 to 3.0 | the constructor, the zoom cases |
| `+0x20084` | the right button held from a press on the map | the viewport's `0x10005`/`0x10004` with button 1 |

- **Opened on the camera's cell** (`0x005f0eba`..`0x005f0fa2`): `(int)( [0x00790ab8] * 0.1 )` and `(int)( [0x00790ac0] *
  0.1 )` (the camera's point; equal to its look-at `0x007908f0`/`f8` in both rounds), held to 0 and to `MapInfo`'s limits
  less one (`[0x007a0314] - 1` = 94, `[0x007a0374] - 1` = 83), then to the heightfield's cells: a camera on (950,260)
  opened on 94, not 95.
- **Moved** by `0x005f35b0( direction, cells )`: direction 0 takes from the centre across, 1 adds, **2 adds to the centre
  down, 3 takes** (its jump table `0x005f366c`), then the centre is held to 0 and the cells less one (95, 84) and the view
  redone (`0x005f3230`). Callers, each one cell: the arrows' repeats `0x10009`/`0x10008` (0x981 down 3, 0x984 left 0,
  0x988 right 1, 0x98a up 2; the table `0x005f18a4`), and the key cases `0x1000a` (`VK_LEFT` 0, `VK_UP` 2, `VK_RIGHT` 1,
  `VK_DOWN` 3, `0x005f16aa`). The arrows are repeating buttons (flag `0x20`, delays 0 and 10 ms by `FUN_00668f35`, `Button_SetRepeatDelays`): from
  the first interface pass after the press one repeat a pass while the pointer is on the arrow (`FUN_00668dd9`), and a
  button that has repeated posts no `0x100` as it is let go (`FUN_00668eff`), so a held arrow makes no click sound (`0x00485780`) and no
  click: the arrows' `0x100` cases do nothing. (One let go in state 1 before its first repeat, a press and release taken in the same pass, does post it and
  sounds; that is not built.) Measured: the right arrow held, 30 cells a second under the 30 fps cap; the
  Up key one cell up.
- **The edges and the right button** follow the Options' Scroll (`[0x0078d910]`, `GameOptions` `+0x38`). With Pushscroll
  (0) the root's frame message (`0x005f126b`) takes the pointer as fractions of 2048 and 1536 (`0x00703014`, `0x00703018`)
  held to 0..1, and inside 0.01 of an edge (`0x0070301c`, `0x00703024`) moves `(0.01 - f) * 250` (`0x00703020`) cells a
  pass: left 0, right 1, **top 2 (up)**, bottom 3; measured 2.5 a frame on the top edge. With Right button scroll (1) the
  viewport's frame message (`0x005f1913`), while `+0x20084` is set, moves right by `(x - 1024) / 409.6` and down (3) by
  `(y - 768) / 307.2` cells a pass (`0x00703034`, `0x00703038`).
- **Zoomed** by b_plus and the keypad's + (`0x6b00`) by +0.5, by b_minus and the keypad's - (`0x6d00`) by -0.5, held to 1
  and 3 (`0x00703028`, `0x00703030`, `0x00703004`, `0x0070302c`).
- **Drawn** by `0x005f3230`: the centre pixel is `( 15 + 480 / cells across x across, 42 + 425 / cells down x down )`
  (`0x00703044`, `0x00703048`, `0x00703054`, `0x00703058`); the source is the screen's own size in pixels (`[0x007a1968]`,
  `[0x007a1930]`, the resolution: `0x00449f48`..) over the zoom about it, held to 0 and the picture's size less one, and
  truncated (`__ftol`); the destination (`0x00577970`) is the whole screen in clip space, shrunk where the source was held,
  each held to -1..1. **One picture pixel is the zoom's screen pixels**, so the map is smaller the finer the resolution.
  The 2D blitter puts the source's first row at the bottom: **the picture is drawn upside down** (Lost Kingdom's gate,
  at the top of `2dmap.tga` in a viewer, is at the bottom of the map). Measured at 800 x 600, zoom 1 on (48,26): the
  field's left at 161 pixels, its bottom at 428 (`q624/orig/a-1-open.png`).
- **Closed** by b_okay (id -1) and by a left click on the map through `0x005f1050`: the centre held to the cells less one
  and the camera put on `( (int)across x 10, (int)down x 10 )` (`FUN_0042aab0`, which noughts the pan's momentum and
  leaves the zoom and turn). The click (`0x005f1d19`, the viewport's `0x10006` with button 0) first moves the centre by the
  pointer's distance from the SCREEN's centre over `480 / cells x zoom` screen pixels a cell (y negated), and, where a
  record was hovered (below), hands its `+0x18` to `FUN_00486920` after the close (not read). Measured: zoom 1.5, a click at layout (1218,765): 58.104,
  26.156, the camera on (580,260). **Escape** (the key-up case `0x005f17ef`) closes it without `0x005f1050`: the camera
  stays.
- **The help bar** stands at the top while the map is up: the opener's `FUN_00492530( -1, 10 )` (`HelpBar_MoveTo` in Ghidra; `0x005f1015`; it moves
  the bar's label to top 10, keeping its left), the close's `FUN_00492530( -1, -1 )` (`0x005f1256`) puts it back.
- **The camera's table is off**: the opener hands its root to `FUN_00485b70` (`0x005f0c01`), as a park screen's.
- **The advisor**: the opener posts line `0x130` (`FUN_00486b00`, `0x005f100d`), the close withdraws it
  (`FUN_00486b40`). Not built here: counted `MAP_ADVISOR_LINE` (Q666).
- **Hover**: on the viewport's `0x10001` the nearest record of the list at `+0x20080` within 5 (`0x00703040`) **screen
  pixels** of the pointer (taken in the game's pixels, x x W / 2048 and (`0x600` - y) x H / 1536, so y counts from the
  bottom; a record's `+0xc`/`+0x10` are written by `0x005f4180` as the cell centre x 480 / cells x zoom + W / 2, so the
  distance is zoom-dependent in picture terms) is kept at `+0x20060` (`0x005f198e`..`0x005f1a9a`). It also builds a string
  for the label `0x991` (`0x005f1bab`..`0x005f1cfc`; the control is found through `0x0065d55a`/`0x0065d7d0`, then its vtable
  `+0x24`): the thing's name (`0x00506860`) for kinds 4 to 8, the text of row `0x1c8` or `0x1c9` (`0x006acd60`) for kind 1,
  or otherwise the `0x007cfb90` table's entry (`0x004e2780`) for the code word `+0x20064` under the pointer. Not built: counted `MAP_HOVER` as the pointer comes onto the map, and `MAP_CLICK_HANDOFF` at a click (Q665).

**OpenTPW** draws the map at the scale the original has at 800 x 600, whatever the window (a deviation: the original's
shrinks on a finer screen), moves a per-pass amount at thirty passes a second on `Time.Delta`, and draws the source
rectangle exactly rather than truncated. The frame `map_win` is the root's mesh (`w_map.MD2`), drawn over the picture;
its interior's grid lies over the picture in both games.

### The map screen's stream, `0x00774da0` — walked in full

Clean op 0, balanced op 5, ending exactly where the `2dmap.tga` string starts at `0x007752e4`. Root
**0x980** is full screen (0,0)-(2047,1537). Every mesh named by its `ui.wad` **file** in brackets.

    0x992  THE MAP VIEWPORT  ( 49,  33)-(1877,1364)  28-point region, handler LAB_005f0b20
    0x98a  nuup   (b_mapup)   ( 80,1079)-( 237,1236) help 263 scroll up      7-pt region
    0x981  nudown (b_mapdn)   ( 80,1236)-( 237,1393) help 264 scroll down    7-pt region
    0x984  nuleft (b_mapleft) (  1,1157)-( 158,1315) help 265 scroll left    7-pt region
    0x988  nuright(b_mapright)( 158,1157)-( 315,1315) help 266 scroll right  7-pt region
    0x986  b_plus             ( 218,1327)-( 275,1384) help 261 zoom in   circle (246,1355) r28
    0x985  b_minus            (  32,1327)-(  90,1385) help 262 zoom out  circle ( 61,1356) r29
    0x990  buttpan1 (f_mapbut1) ( 35,1387)-( 681,1533)   left button panel backing
    0x995  buttpan2 (f_mapbut2) (1356,1387)-(1909,1533)  right button panel backing
    0x991  textpan  (f_maptext) ( 688,1395)-(1351,1525)  text rect (730,1424)-(1295,1503)
    (-1)   b_okay             (1941,1339)-(2024,1422) help 1 "close this screen"

**Six layer switches** (type 2, flags **0x11** = toggle), each with a show/hide help PAIR in op 0x11 and
op 0x12 — the captions for on and off:

    0x987 b_sride (b_srides) ( 64,1409)-( 167,1512)  267/268  rides
    0x994 b_sshow           ( 184,1409)-( 286,1512)  271/272  sideshows
    0x993 b_sshop           ( 303,1409)-( 406,1512)  269/270  shops
    0x982 b_sfeature        ( 423,1409)-( 525,1512)  273/274  miscellaneous items
    0x989 b_strack          ( 542,1409)-( 644,1512)  275/276  tracks
    0x983 b_svisit          (1390,1409)-(1493,1512)  277/278  visitors   <- apart from the cluster

**Two radio groups, both control type 8.** 0x98b is the STAFF overlay picker, a vertical stack on the
right edge (1883,443)-(1985,987); `FUN_005f0b40` restores it from `+0x1c` as 1/2/3/4:

    0x98e b_shandy (1883, 443)-(1985, 546)  279/280  cleaners and litter
    0x98f b_smech  (1883, 592)-(1985, 694)  281/282  mechanics and ride reliability
    0x98d b_senter (1883, 740)-(1985, 842)  285/286  entertainers and queue times
    0x98c b_sguard (1883, 885)-(1985, 987)  283/284  guards, camera coverage and pranks

0x996 is the METRIC picker, bottom right (1526,1409)-(1868,1512), restored from `+0x20` as 1/2/3. The mesh
**map_win (w_map.MD2)** is bound AFTER the group's own close (`0x007752da`), so by the op-5 rule it is the root
**0x980**'s, not the group's or 0x999's:

    0x999 b_ssatis (1526,1409)-(1629,1512)  291/292  customer satisfaction
    0x998 b_shap   (1646,1409)-(1748,1512)  287/288  happiness
    0x997 b_sexcit (1766,1409)-(1868,1512)  289/290  excitement ratings

The help rows confirm the heat map in the game's own words — "excitement ratings", "customer
satisfaction", "ride reliability", "queue times" are all simulation values.

### The cell codes, by exhaustion of the cell accessors

Each is a one-liner on a 0x44-byte runtime cell, except the first two, which read the 0x28-byte record the cell's first
word names (`FUN_004d0af0`), taking its parent's when it is type 12 or 17 (`FUN_0053ad90`):

| Address | What it tests |
|---|---|
| `FUN_00536450` | the record is type 25 (`FUN_0053ad60`) |
| `FUN_005363f0` | the record is type 11, 13, 16, 18 or 25 (`FUN_0053ad30`); false for a type 12 or 17 record with no parent |
| `FUN_00536310` | kind == 1 |
| `FUN_00536320` | kind == 3 or 9 |
| `FUN_00536340` | kind == 9 |
| `FUN_00536350` | kind == 10 |
| `FUN_00536380` | kind == 4 |
| `FUN_005363e0` | kind == 0x15 |
| `FUN_00535db0(cell,mask)` | `*(ushort*)(cell+0xe) & mask` |

Read against `FUN_005f2050`'s nesting, kind 9 is ruled out before code 11 is reached, so:

    code  9 = track type 25       code 10 = kind 1    code 11 = kind 3    code 12 = flags & 0x40

**The runtime kind at `+8` is the cell's `mType`, not the `.MAP` attribute byte.** The cell serialiser
`FUN_004d0b30` names `+0x08` `mType` (`0x004d0f19`; `park-engine.md`, "The runtime cell is not the save cell"), and
over the shipped park `mType` takes 7, 2 and 30, which jungle's attribute byte never does (below; `park.md`,
"`mType` over the shipped park"). So code 10 is a path (mType 1), code 11 a queue cell (mType 3) and code 12 land
outside the park (`mFlags & 0x40`; `park-engine.md`, "The path tool").

### The empirical check, and what it does and does not establish

Attribute values 1 and 3, drawn from `base.map`, reproduce the very features the shipped `2dmap.tga`
draws — the winding channel, its prongs, and the round blob bottom-right — with attribute **17** landing
on the entrance/bus structure at the top. **It is a falsifying test, not a flattering one:** four
orientations were rendered and three failed outright (transpose throws the bus slab to the left edge,
flipV to the bottom), and the full 128x128 extent matched *worse* than the playable crop, squashing every
feature up-left with dead space right and bottom.

    orientation  IDENTITY - x across, y down, indexed x*128 + y   (transpose/flipV rejected)
    extent       96 x 85 cells (x 0..95, y 0..84 INCLUSIVE), NOT the full 128x128

Counts (jungle `base.map`, 16384 cells): 0 x14889 (90.9%), **1 x689 (4.2%)**, 17 x490 (3.0%),
**3 x240 (1.5%)**, 144 x48, 148 x14, 8 x10, 128 x4. And a structural find: **`terrain.map` holds only TWO
values, 0 and 1** (805 cells) — a pure binary mask, not a second attribute grid.

What it proves is that the shipped `2dmap.tga` already draws the attribute map's value-1 and value-3
cells; the map's codes 10 and 11 are paths and queue cells (above), not these. **It does NOT license semantic
labels** — "3 is water, 1 is path" is a guess the pixels do not support, so name them by value, not by
meaning.

### The cell-to-pixel mapping is exact

The image carries a black frame, so the grid maps onto the INNER field, measured as **x 16..495 (480
wide), y 43..467 (425 tall)**:

    cell (x,y) -> pixel ( 16 + 5*x , 43 + 5*y )      jungle's 512x512 2dmap.tga, 96x85 cells
    480 = 96 * 5   and   425 = 85 * 5                EXACTLY, in both directions

The field yields **exactly 5.0000 px per cell both ways** and the aspect matches to four decimals
(480/425 = 1.1294 = 96/85), where the 95x84 extent would give 5.0526 / 5.0595 px and 1.1310 — a near-miss
that tidy is usually an off-by-one, not a measurement. Forcing the full 128x128 onto the same field would
demand **13% non-square** cells (3.750 vs 3.320), which no top-down map would have. Re-overlaid at the
exact mapping the extent border lands on the field edge on all four sides and the hatching covers the
channel, both prongs and the round blob, with attribute 17 over the entrance structure; the 128x128
version leaves the blob untouched.

So the extent is corroborated three independent ways — shape match, feature position, and whole-number
cell size. **Only the arithmetic one could not have been talked into.**

## Constraints on anything built against this

- **A HUD panel must leave `Pauses` false** or it stops `GameClock`, and with it the calendar, particles
  and every model animation. The map screen is the exception: the original genuinely pauses there.
- **The interface takes the mouse before the world does.** `WindowStack.WheelTaken` keeps a wheel a window used
  from also zooming the park (`ParkOrbitCameraMode`), and `WindowStack.PointerTaken` keeps a left press on a
  window, or any left press under a modal one, from reaching the world (`Level.WorldClick`).
  `WindowStack.RightPointerTaken` keeps a right press there from arming the quick click, except beside a park screen
  (`UiWindow.ParkScreen`), which the original builds onto the park's layer (`park-engine.md`, "Whose a right press
  is"). The camcorder key is guarded by `Level.Current?.PausedByWindow() != true`. A new HUD control that uses the
  wheel or a press must go through these, and a new park screen built onto layer 0 sets `ParkScreen`.
- **HUD work is verifiable by eye and capture, not by test** — it needs someone to look at the running
  game.

---

## The buy and hire screens, and the list control under them

Decoded 2026-09-20 by an eight-dimension pass, every claim then re-derived by a second agent that was
told to refute it.

### The scrolling list is UI control **type 7** - `UiList` here

Both screens are a single multi-column scrolling list class — ctor `FUN_00662562`, 0x178 bytes, vtable
`0x007059f0`, type getter returns **7**. OpenTPW's is `UiList` (`source/OpenTPW/UI/UiList.cs`); everything else on
these screens already had an analogue.

| Function | What it is |
|---|---|
| `FUN_006649d5` | Clear — frees each TEXT column's string, rebuilds the free-node chain, resets count/selection/scroll |
| `FUN_0066403b( &rec, id, insertAfter, commit )` | Add a row. **`rec` is `columnCount` dwords, not a fixed struct** |
| `FUN_00663324` | Commit/refresh — the only thing that moves data into widgets |
| `FUN_006639cb( factory, styler )` | Builds the reusable row widgets. **Neither argument is a draw or sort callback** |
| `FUN_006636b2( col, type )` | Column type: 0 = text (string, strcmp-sorted), 1 = numeric |
| `FUN_00664c71( rowIndex, &out )` | Row index → that row's stored id |
| `FUN_0066a295( memberId )` | Selects a member of a type-8 radio group (the tabs) |

**The record is sized by the column count.** Buy pushes **three** — `{char* name, int price, int state}`
— and hire pushes **two**, `{char* name, int wage}`. A fixed three-field struct is wrong for hire.

**The state column is a tick-box, not a number.** Buy's styler skins column 2 with the mesh
`i_boxtick` and calls SetValue, so the {0,1,2} picks one of three frames.

**`FUN_006636b2` must be called AFTER the tree is parsed**, because op `0xb` resets every column's type
to 1. A hard ordering constraint that fails silently.

**Row widgets are a fixed reused pool.** `FUN_006639cb` builds one chain per *visible slot*, and the
slot count is derived from the measured height of the factory's first widget — so the prototype row
must exist before the number of slots is knowable. Rows are never drawn; their values are pushed into
the pool on every refresh.

### The messages, and why one signature will not do

    0x400  row activated      (listId, selected ROW INDEX)
    0x401  selection changed  (listId, selected ROW INDEX)
    0x406  sort changed       (listId, signed column+1)
    0x100  button clicked     (CONTROL id, no row at all)

`param_3` means different things for `0x100` and for `0x400`/`0x401`. Index and id diverge as soon as
the list is sorted, which is what `FUN_00664c71` is for.

Three further messages are implemented by the class and handled by **neither** screen: `0x402`
right-click a row, `0x404` column hit, `0x405` visible range changed. By this project's own rule they
are dead by CONTENT, not by CODE. The all-staff, visitors and all-items screens do answer `0x402`: the camera goes to
the row's thing and the screen closes (`park-engine.md`, "The camera goes to a thing").

### A list's order: the sorted insert, and a heading's click (Q130d)

Decoded and measured 2026-10-06. **Every list whose flags `+0x48` hold `0x10` keeps itself in order**, and all seven
list records do (buy `0x91`, the rest `0x291`). The sort is two words on the list: the column `+0x168` and the
direction `+0x16a` (0 ascending, 1 descending).

- **The add is a sorted insert.** `FUN_0066403b` with an insert-after of -1 takes a free node, then walks from the
  head until `FUN_00663edc( new, row )` answers 1 and links the new row before that one, or after the last. For a
  numeric column (type 1) that is `new < row` ascending and `row < new` descending, both strict, so **equal rows stand
  in the order they were added**. For a text column (type 0) it is 1 when the row's text is null, else
  `FUN_0067c290( new, row ) < 0` ascending (the arguments swapped for descending): a compare of 16-bit characters by
  their plain values, with no case folding and no locale, so every capital stands before every small letter.
- **A heading's click re-sorts.** The headings are the list's children `0x10 + column`; a child's click reaches the
  list's proc `FUN_00665c35` as message `0x100` and goes to `FUN_00665a44( id )`. On the column already sorted on it
  flips the direction (`FUN_0066594d( -1 )`); on another it sets ascending, then the column (`FUN_0066590f`). Either
  way it sorts the whole list (`FUN_006628f4`), commits (`FUN_00663324`) and, with a row selected, posts `0x401` for
  the selected index, which now names a different row. Each setter that changed its word posts `0x406` with the column
  + 1, negated when descending (`FUN_006658b9`).
- **`FUN_006628f4` keeps equal rows as they stood.** It moves the first least row to the head, then takes each row
  from the third on and links it before the first row it is strictly less than. So a click on Monthly Wage from name
  order leaves equal wages in name order, where a fresh fill leaves them in the order added.
- **A screen remembers its sort in one word of its own**, for the session: its handler stores `0x406`'s value, and
  its opener hands the word to `FUN_006659af( word, 0 )` while the list is still empty, which sets both words and
  sorts nothing. Hire: `[0x007523e4]` (written `0x0049b85e`, read `0x0049c0f0`). Buy: `[0x00755140]` (`0x004ad057`).
  Visitors: `[0x007508bc]`. All three are 1 in the file: the first column, ascending.

**So the hire list stands in name order because its first column is the name.** `FUN_0049b5b0` clears the list and
adds the tab's candidates in slot order, 0 to 31, each through the sorted insert, the slot as the row's id; a
candidate who joins while the screen is open (`FUN_00481550`, `FUN_0049c970`) goes through the same insert. Nothing in
the hire screen's own code orders anything. The name handed over is `FUN_005074a0`'s text, held to 39 characters on a
fill and 40 on a join.

**In the original** (under Proton, the stock Lost Kingdom park, each predicted first; `q130d/orig`): the word read 1
with the screen open; a click on Candidate Name made it -1 and reversed the rows, on Monthly Wage 2 and by wage
rising, again -2 and falling; another tab, and the screen closed and opened again, kept -2 and the order. With the
word back at 1 all five tabs stood in name order, and four of them not in slot order, each row tied to its slot
through the pool's name index and the `american` tables (janitors: slots 19, 1, 17, 2, 14, 7). Ties, on six
entertainers: a click on Monthly Wage from name order gave Duncan Kershaw, Shintaro Kanaoya, Simon Carter at 48
(name order), and another tab and back gave Shintaro Kanaoya, Simon Carter, Duncan Kershaw (slots 0, 8, 13). The buy
list reversed on a click on Name and was still reversed when opened again.

**The hire and buy lists' headings** are the stream's `op 0xc` children, lettered and framed by `FUN_00486660(
control, text, font, r, g, b, help )`: a label skin, the nine-slice frame `!cbut`, the text centred both ways, the font
slot and colour handed over, and the help row at `+0x90`. Both openers hand font slot 8 (`[0x007523e0]`,
`[0x00755138]`) and the colour 255, 239, 0.

| List | Column | Rect | UITEXT | Help | Type |
|---|---|---|---|---|---|
| hire (`0x00751fa8`) | 0 | 1039, 331, 1460, 436 | 144 "Candidate Name" | 159 | text |
| | 1 | 1471, 331, 1731, 436 | 145 "Monthly Wage" | 160 | number |
| buy (`0x00754cf8`) | 0 | 1032, 317, 1454, 422 | 123 "Name" | 144 | text |
| | 1 | 1458, 317, 1664, 422 | 124 "Price" | 145 | number |
| | 2 | 1669, 317, 1729, 422 | 138 "." | 146 | number, the tick's state |

The types are `FUN_006636b2`'s (hire from `0x0049c0bf`, buy from `0x004ad027`); the visitors list leaves all six columns
numbers. The buy list sorts on the text its row shows, so a mystery ride's "??? Mystery Ride! ???" stands first, and
the two land rows are sorted in with the rest (`FUN_004aaf70` adds them through the same insert, Buy Land priced at
`[0x0078560c]`).

**OpenTPW** (Q130e): `UiList` keeps the column and direction (`SetSort`, `SortWord`), `Add` is the sorted insert
whenever a sort is set, `HeadingClicked` is `FUN_00665a44` and `Sort` `FUN_006628f4`; a heading is a button
(`AddHeading`). The hire, buy, visitors and all-staff screens each keep their word in a static for the session, the
all-items screen one a tab (Q374). A list built with headings and no sort, which only a test builds, counts a heading's
click (`LIST_HEADING_SORT_NOT_BUILT`).
Time In Park sorts on its hours (Q372), the "?" column on its thought's part (Q373). The buy list's rows are
handed over in the catalogue's order, as the original walks it (`FUN_00412e60`), so rows equal on the sorted column stand
as the original's: the two mystery rides, Jurassic Tours' (`tourride`) before Eruption's (`volcano`), Q709.

**Measured, both games side by side** (Q130e, 2026-10-07, each predicted first; `q130e/`). The original, on the save's
own sixteen candidates (every photograph before `mGameTick` 858, when the first times out), and OpenTPW with the park
paused, gave the same rows on all five tabs, apart from three names the `american` tables give differently, which
stand where their own letters put them. The entertainers (slots 5, 9, 13 at 60, slot 23 at 84): Monthly Wage, word 2,
the three at 60 in name order then 84; the mechanics' tab and back, word 2, the three in slot order; Monthly Wage
again, -2, the 84 first and the three as they stood; Candidate Name, 1; again, -1. The buy list's rides: Name -1,
Price 2, Price -2, the tick 3, the four rows the same in both at each step. The original's features tab at -1 reads
Small bathroom, Small Tree, Small Bush: a small letter after every capital.

### The pointer over a list

Under the list flag `0x80` the proc `FUN_00665c35` answers the pointer's move, `0x10001`, and `0x10003`, through
**`FUN_006656a0`**: when the list's `+0x11c` is nought and the point is inside the control's region (`+0x2c`), it
hands the point to `FUN_0066552e`, the same row test the clicks use. So the row under a moving pointer is selected
with no press, `FUN_0066525c` posts `0x401` when that changes the selected row, and a point over no row leaves the
selection where it was. The buy and hire lists carry the flag, as the five list trees below do. `+0x11c` is the
control's own record of the buttons pressed on it: the base proc sets a button's bit on its press and clears it on its
release (`FUN_0065f6d1`, `0x0065f871`, `0x0065f9e1`), and the button class and the slider's thumb keep theirs the
same way. So a list follows the pointer only while no button pressed on it is down. OpenTPW: `UiList.PointerMoved`,
told by the stack whether a press that landed on the list is still held; whether the original's bit outlives a
release let go off the control is not decoded. What sends `0x10003` is not traced.

**OpenTPW**: `UiList.SelectsUnderPointer` and `PointerMoved`; `WindowStack` sends the move to the control under the
pointer on a frame the pointer moved. Every list sets the flag: the buy list (Q233b), the park file screen's
(`ParkFileScreen`), the hire list (Q356), and the visitors, all-staff and all-items lists (Q376). A pick under the pointer
logs `List 0x<id>: row <id> at <index> picked under the pointer`.

**The visitors, all-staff and all-items lists follow the pointer** (Q376). Their tree records carry the flag
(`0x00750716`, `0x00750e5e`, `0x00750ab8`, `0x00750ba0`, `0x00750ca0`: type 7, flags `0x291`), and none of their
handlers has a `0x401` arm (the visitors' `0x00493230`: `0x400`, `0x402`, `0x406`; the all-staff's `FUN_00495da0`: the
same three; the all-items' `FUN_00495360`, "The miscellaneous tab", below), so the pick is the highlight alone, and a
right click on the strip under the rows names the row last hovered. **Measured in the original** (`q376/orig/a`,
`q373/thought.TPWS`; `rectcolour.py`'s teal share of each row's first cell): the visitors list opened with row 0
highlighted (0.934), and the pointer brought along row 2's band from the left of the list moved it there (0, 0, 0.917);
the misc tab, row 0 at its opening (0.949), row 2 under the pointer (0.811), still row 2 with the pointer brought round
outside the list to the strip under the rows (0.917), and a right click there shut the screen with the view on a
security camera. The all-staff list was not run in the original: every scene has one member a kind, which the add's
pick has highlighted already. **OpenTPW** (`q376/run-fix1`, the final game binary, the stock park paused, the pointer
put on a row by XTEST): `List 0x1e497: row 40 at 2`, `List 0x321: row 25 at 0`, `List 0x190: row 19 at 2`, each row's
teal 0.92 and every other row's nought, nothing picked on the strip, and the right click there `Camera: to thing` 40, 25
and 19, each screen shut. Main's build (`run-control1`): no highlight on the visitors or the staff, the misc tab's row 0
kept, and its right click to thing 17; the other two counted `LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED`.

### A frame worn at two sizes

A `!` mesh is a nine-slice frame grown to its control's size (`0x00477310`). `UiMesh` kept one set of vertices for
each frame part and re-wrote it for whichever control drew next, so two controls of different sizes wearing the same
frame on one screen were both drawn with the last size written: the buy screen's description panel, 414 by 414, was
drawn 712 by 281, the stats panel's shape, squeezed into its own rectangle's corner. Each size now has vertices of
its own (`UiMesh.Sized`). Photographed before and after (Q233b); no test, as a part needs a graphics device.

### A right click on a list

The list's proc `FUN_00665c35` answers the message `0x11006`, which carries a button and a point: the left button
(`param_3` nought) goes to `FUN_006655a2`, the row click above; any other goes to **`FUN_0066563d`** (`0x00665dbd`).
The base proc's click `0x10006` becomes `0x11006` in `FUN_0065dcaf` ("Who acts on the click, and who on the
release"). `FUN_0066563d` does nothing unless the list's
flags `+0x48` have `0x80`. Then `FUN_0066552e` hit-tests the point against the row region (`+0x138`), divides its
height above the rows' top (`+0x13e`) by the row height (`+0x170`), and hands the slot to `FUN_0066525c`, which
selects it only when it is under the visible count (`+0x172`) and the row count; and then, **if any row is selected**
(`+0x14c` not negative), it posts `0x402` to the parent with the list's id and the selected row.

So the right click names the selected row, having first selected the one under it: a click on the list that misses
every row still names the row selected before. All five trees of the three list screens carry flags `0x291`
(`0x00750716`, `0x00750e5e`, `0x00750ab8`, `0x00750ba0`, `0x00750ca0`): `0x80` set, and `0x100` clear, so a slot past
the rows leaves the selection where it was.

**In the original**: the visitors list opens with one row highlighted (the first on one opening, the second and the
tenth on two others; what chooses it is not decoded, `docs/QUEUE.md` Q232: the pointer resting over the list is one
candidate, as the list follows it, Q376), and a quick right click on the strip under
its tenth row went to that row's guest, not to an eleventh row's.

**OpenTPW**: `UiList.RightClickedAt`, called by `WindowStack.RightClick` with the press's point, and
`UiList.RowRightClicked`. The strip under the last whole row is no row's for either button. Every list follows the pointer ("The pointer
over a list"), so a miss names the row last hovered. The hire and all-items lists pick their first row as they fill
("The first row is picked as a list fills"), so there a miss before any hover names that row; the others open with no
row selected, and a miss before any row is hovered or chosen names nothing: counted,
`LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED`.

**With flag `0x80` set, `0x400` is posted once, on the click** (`FUN_006655a2`, from `0x11006`), and once more on a
double click's second press (`FUN_006656fa`); nothing posts it on a bare press ("Who acts on the click, and who on
the release", measured). `UiList.LeftClickedAt` and `LeftDoubleClicked`.

### The structural surprise: the tab group is a CHILD of the list

In both trees the tab button group, the column headers and the scrollbar are all children of the list
control. **Tab clicks reach the screen only by bubbling to the list's handler** — parenting the tabs to
the dialog instead would mean never seeing them.

### Ops 9 and 10 are runtime-conditional, and that is not optional detail

Op 9 creates the scrollbar (type 3 slider, flags 0x10, id 1) and op `0xa` writes the list's **row-area**
rect. **Both consume their operand only when the enclosing control's type is 4 or 7**; under any other
parent they read nothing. A walker that always eats 8 bytes for op `0xa` desynchronises the whole tail.
Op 3's text rect and op `0xa`'s row area happen to hold the same values on both these screens, so
conflating them looks correct here and breaks elsewhere.

### Both trees, walked to a balanced op 5

**BUY — `0x00754cf8`, 1080 bytes, 32 controls.** Handler `FUN_004ac270`.

    0x1e9  root (186,30)-(2018,1007)          mesh 0xf76e4200 = node "window4" in w_big.MD2
      0x1ea  panel  + text rect               !frame
        0x1eb, 0x1ec
      0x1ed  stats panel                      !frame
        0x1ee 0x1ef 0x1f0 (type 9 bars) ; 0x1f1 0x1f2 0x1f3 0x1f4 0x1f5 0x1f6 0x1f7 (labels)
      0x1f8  THE LIST (1009,179)-(1813,902)   mesh buyitem, 3 columns
        0x1f9  TAB GROUP
          0x1fa b_sshow help 142 | 0x1fb b_sride help 140
          0x1fc b_sfeature help 143 | 0x1fd b_sshop help 141
        0x10 0x11 0x12  column headers ; op 0xa row area (1037,427)-(1727,871)
        op 9 SCROLLBAR id 1  (!slider, b_up, b_down, b_scroller thumb)
      0x1fe  TITLE     0x200  MONEY
      id -2  EXIT b_exit help 2 ; 0x1ff -> HIRE  b_allstaff help 153

**HIRE — `0x00751fa8`, 1074 bytes, 31 controls.** Handler `FUN_0049b650`.

    0x247f root                                mesh 0xf76e4200 = node "window4" in w_big.MD2
      0x2480 TITLE ; id -2 EXIT
      0x2481 staffinfo   -> 0x2482 skill bar, 0x2483 label
      0x2484 PORTRAIT    staffpic
      0x2485 MINI-BALANCE help 163             mesh balance
        0x2486/0x2487  Balance      0x2488/0x2489  Cash in
        0x248a/0x248b  Other costs  0x248c/0x248d  Staff costs
      0x248e THE LIST                          mesh hirestaff, 2 columns
        0x248f TAB GROUP
          0x2490 b_shandy 154 | 0x2493 b_smech 155 | 0x2492 b_senter 156
          0x2494 b_sguard 157 | 0x2491 b_sresrcher 158
        0x11 then 0x10 column headers (reversed in the stream) ; op 9 SCROLLBAR
      0x2495 -> BUY  b_allthings help 162 ; 0x2496 MONEY

#### The mini-balance is a MONTHLY ACCOUNT, not the park's cash

From `FUN_0049bdd0`, corroborated by the `0x401` arm of `FUN_0049b650`. Three monthly ring buffers on
the park analyser (`mParkAnalyser`, `FUN_00519510`), each stored as samples / index / count / wrapped, each fed by the
month's running total 4 bytes before its samples (`+0x1fc90` cash in, `+0x1f7f0` staff, `+0x1f5a0` all costs, the cost
of goods among them), pushed on the month's change (`FUN_004c7720`; `ride-operation.md`, "The settle-up's bookkeeping"):

| Samples | Index | Count | Wrapped | Becomes |
|---|---|---|---|---|
| `+0x1fc94` | `+0x1fed4` | `+0x1fed8` | `+0x1fedc` | `DAT_007ca2f0`, cash in |
| `+0x1f7f4` | `+0x1fa34` | `+0x1fa38` | `+0x1fa3c` | `DAT_007ca2fc`, staff costs |
| `+0x1f5a4` | `+0x1f7e4` | `+0x1f7e8` | `+0x1f7ec` | `DAT_007ca2f8`, total costs |

    0x2489 Cash in     <- DAT_007ca2f0
    0x248c Staff costs <- DAT_007ca2fc              (on 0x401, plus the candidate's wage)
    0x248b Other costs <- DAT_007ca2f8 - DAT_007ca2fc
    0x2487 Balance     <- DAT_007ca2f0 - DAT_007ca2f8

A negative index reads `index + count`, and only when the wrapped byte is set - a ring that has not
filled once yet; the count is tested first, and a count of nought reads nought (`0x0049c54f`). The figures are read
once, as the screen opens; nothing rereads them while it stands. **A pick (`0x401`) moves two of them**: the staff costs
read `DAT_007ca2fc` plus the candidate's wage (`FUN_005077f0`) and the balance `DAT_007ca2f0` less the wage less
`DAT_007ca2fc` less `DAT_007ca300`, the kept other costs: cash in less all costs less the wage. Every figure is the
money skin's (`FUN_00490250`): "$ 1500", "-$ 11675". **The balance row is last month's net, so the park's cash does not belong in it**; a
build that puts it there shows the same figure twice, once correctly as `0x2496` and once mislabelled.

**The two cost rows are red.** `FUN_0065c5d5( 0xff, 0, 0, 0xff )` is called while `0x248d`/`0x248c` and
`0x248a`/`0x248b` are the current control, and never for `0x2488`/`0x2489` or `0x2486`/`0x2487`.

**What feeds the money months** (read 2026-10-15, Q385 and Q386; every writer of the offsets, a scan of the image's operands):

| Total | Added by |
|---|---|
| `+0x1fc90` cash in | every deposit (`FUN_004d0190`), the bank's month turn's batch (`FUN_004d0370`), the gate fee (`FUN_004d0600`, which adds to the gate's `+0x1fee0` too) and a loan taken (`FUN_004d0750`) |
| `+0x1f5a0` all costs | every withdrawal made (`FUN_004d01f0`, nothing while `+0x114` is nought), a loan's instalment (`FUN_004d0370`) and a loan paid off (`FUN_004d0850`) |
| `+0x1f7f0` staff costs | a member's monthly wage (`FUN_00504c70` on message `0xc`) and a dismissal's (`FUN_00505790`), each after its withdrawal and whether or not that was made |
| `+0x1fa40` training | a member's training share (`FUN_00505a10`, from thing 1's month turn), after its withdrawal and whether or not that was made; nothing at grade 4 |
| `+0x1fee0` gate takings | the gate fee alone (`FUN_004d0600`, which adds it to cash in first) |
| `+0x20130` shop takings | a shop's charge (`FUN_004e16b0` where the item's descriptor `+0x4ac` is 1, `0x004e1711`), after its deposit onto cash in |
| `+0x20380` sideshow takings | a sideshow's charge (the same, `+0x4ac` 2, `0x004e18a6`); a ride's (0) goes onto neither |

The close (`FUN_004c7720`, the analyser's turn of the month, after thing 1's training and before the bank's turn and
the wages) pushes each total into its ring (`FUN_004ce290` for all costs, staff costs, training and cash in; the same
steps written out in line for the gate, shop and sideshow takings: the slot one on, nought past the last with the
wrapped byte set, the total written there) and zeroes it; the constructor (`FUN_004c58c0`) leaves each slot -1, each
count 144. The same close samples the analyser's other nine rings, which no add feeds: **the bank's balance first**,
before any other ring (`FUN_006ad810` on the bank, `FUN_005195d0`'s thing `+0xc`, into `+0x1f100`'s next slot; its own
total `+0x1f100` is never written and reads 0 in every file), and after the seven money rings the other seven in this
order (read 2026-10-15, Q389, `0x004c788c`..`0x004c7a7a`; not ring order): the month total `+0x20cc0` closed as a money
ring is (`+0x20cc0`, added by `FUN_004cf3e0`; below, "The arrivals"), `FUN_004c7fa0` (`+0x205d0`; it and the 12th below, "The guests inside and the rides at a close"), the lifetime visitors `+0x21c08`
(`+0x20820`, below, "The lifetime visitors"), `FUN_004c8070` (`+0x20a70`), `FUN_004c7bb0`'s float (`+0x20f10`; below, "The guests' happiness at a close"),
`FUN_004c7db0`'s figure (`+0x1f350`; the graph's "Park value", below), a mean of `FUN_004cef30` (`+0x21160`; below,
"The leavers' stays") and `FUN_004cceb0` (`+0x213b0`; below, "The park score").

**The finances screen graphs eight of them** (`FUN_0049a2e0`, its range `DAT_007ca2ec` 12, 36 or 144 months; the rings
picked by the bits of `DAT_00751fa0`, 1 at the start: 1 the balance, 2 `+0x1f350`, 4 cash in, 8 gate takings, `0x10`
shop takings, `0x20` sideshow takings, `0x40` all costs, `0x80` staff costs; `FUN_0049a7b0` takes the largest month
of those picked for the scale through `FUN_004801d0`). Bits 1 and 2 and the other six do not stand together: a row of
one group ticked clears the other's. Training has no row.
The rings' place in a park file is `saves.md`, "The park analyser": in the shipped park one month is closed, cash in
1500, staff costs 0, all costs 13175, so the original's hire screen opens on "$ 1500", "$ 0", "$ 13175", "-$ 11675".

**OpenTPW** (Q385). `ParkMonthRing` is a ring; `ParkState.MonthCashIn`, `MonthStaffCosts` and `MonthCosts` are the
three, the file's at a load (`ParkWorld`) and empty in a park made with no file; `Deposit` and `Take` add to cash in,
`Spend` to all costs, `AddStaffCost` (from `PayTheWages` and `Fire`) to staff costs; `TurnTheMonth` closes them first;
the writer writes them back (`ParkWorld.PutMonthRings`). The hire screen (`ParkHireScreen.ShowAccount`) and the training
screen read the newest month as they open. **The other four money rings are kept the same way** (Q386):
`MonthTraining` (`AddTrainingCost`, from `ParkPeople.Train` after its `Spend`), `MonthGateTakings` (`Take`),
`MonthShopTakings` and `MonthSideshowTakings` (`AddShopTakings`, `AddSideshowTakings`, from the charge by the item's
`UiType`); `TurnTheMonth` closes the seven in the close's order and the writer writes them (`ParkWorld.MonthRingSet`).
Nothing here reads them: the finances screen is not built, and the original's reads them from a file this game wrote.
**The balance ring is kept too** (Q387): `MonthBalance`, the file's at a load, `ParkMonthRing.Push( Balance )` first in
`TurnTheMonth`, so after the training's withdrawal and before the bank's batch, the instalments and the wages, as
`Level`'s month turn calls them; written as the 1st ring. Measured in 579 park files: where its slot is the money
rings' (573; the six others are this game's own, written before it), each closed month is the last plus that month's
cash in less its all costs in 572; the one other (`q252/orig/ctl-written-by-the-original.TPWS`) holds as its second
month the shipped file's month so far unchanged where its balance fell 494 more, by every sign a month taken over from a
file this game wrote before Q385 kept the costs. **The park value ring is kept too** (Q388): `MonthParkValue`, the
file's at a load, `ParkMonthRing.Push( ParkValue.Of( ... ) )` after the seven money rings in `TurnTheMonth`, written as the
2nd ring (below, "The park value"). **And the lifetime visitors' ring** (Q389): `MonthVisitors`, the file's at a load,
`ParkMonthRing.Push( LifetimeVisitors )` after the money rings and before the park value, written as the 11th (below,
"The lifetime visitors"). **And the guests inside' and the rides' rings** (Q390): `MonthGuestsInside` and `MonthRides`,
written as the 10th and 12th (below, "The guests inside and the rides at a close"). **And the arrivals'** (Q391):
`MonthArrivals`, written as the 13th (below, "The arrivals"). **And the 14th and 15th** (Q392, Q393; below), **and the 16th** (Q394;
below, "The park score"): all sixteen are kept.

#### The park value

`FUN_004c7db0` (read 2026-10-15, Q388), a thiscall on the analyser with one argument, a number of quarter ticks. Its
answer is kept at `+0x1f0ec`, stamped at `+0x1f0e8` with `mGameTick` (`FUN_0041a960`) and flagged at `+0x1f0f0`: with
the flag set and the argument at or over the quarter ticks since the stamp (`FUN_0041a990`, `(tick >> 2) - (stamp >> 2)`,
unsigned) the kept answer is given again. Its four callers: advisor rows 156 and 157's score callbacks (`0x0059f873`, `0x0059f8e3`, argument `0xa`; each scores
when `FUN_004c7bb0`'s value is above its row's coefficient), the month's close (`0x004c79a7`, argument nought, so a fresh
sum unless one was taken in the same quarter tick), which pushes it into the 2nd int ring, `+0x1f350` (its slot
`+0x1f594`, its months `+0x1f354`), and the loans screen's test of a loan (`FUN_004d06b0`, argument `0x14`), which
weighs the value (times the float 1.0 at `0x00700370`) less what the bought loans still owe against the loan's record;
not read further. The analyser's serialiser
`FUN_004c5d70` writes each kept answer's count and flag, not its stamp (`0x004cd9c0`). The sum:

- **Every cell** of the 128 x 128 runtime array (world `+0x2d8`, `0x44` bytes, id `y*128+x+1`) whose `mFlags & 0x40`
  (outside the park) is clear adds by `mType`: 0 adds 2, 1 (path) 3, 2 adds 1, 3 (queue) 5, 9 adds 6 (both the `3 or 9`
  test and its own), 10 adds 1, every other nought (`0x004c7e24`..`0x004c7e7d`; `FUN_005d60c0`, which would add 10,
  answers nought). **And 50 where its track record counts** (`FUN_005363f0`): the record the cell's first word names,
  or for a type 12 or 17 its parent's (`+0x10`; none with no parent), of type 11, 13, 16, 18 or 25.
- **Every object on the chain** (`mFirstObject`, world `+0x1da746`, linked at `+0xc`) but a condemned one (`mState` 4,
  `FUN_004e0380`) adds the item's `CostOfUpgrade` (descriptor `+0x1b8 + 0x40 k`) for every tier `k` from nought to its
  `mUpgradeLevel` (`+0x50`), times `__ftol( State of repair, +0x44 ) & 0xff`, over 100, a signed whole division.

**Measured.** From the file alone (`lib/parkfile.park_value` with `lib/itemcosts`), the shipped `Easymode.TPWI` sums to
its own ring's one closed month, 23977: cells 19527 (2362 of type 0, 78 path, 4 queue, 8 of type 9, one of type 10, and
290 track cells, 216 of type 12 whose parent is a 25 and 74 of 25), objects 4450 at full repair (the fountain 150, three
toilets 100, the staff room 500, two cameras 250, a bin 100, the Drinks Shop 650, the Jungle Spray 1750, the Belly
Bounce 500; the gate and the two fixed items nought). Of 581 park files on hand 186 sum to their ring's newest month,
the shipped park and its untouched copies; the others were worn or built on after their last close.

**OpenTPW** (Q388). `ParkValue.Of` is the sum, from `ParkState.Record` and `ObjectsInChainOrder`, the tiers' costs
`ParkItemCatalogue.Item.UpgradeCostAt` (`ItemDescriptionFile.UpgradeCostAt` for tiers 1 and 2); it takes the sum afresh
every time where the original's close can reuse one of the same quarter tick (said at the site). Nothing here reads the
ring: the finances screen and the loans screen are not built.

#### The lifetime visitors

Read 2026-10-15 (Q389). **`mLifetimeVisitors` (`+0x21c08`) counts the admission fees taken**: `FUN_004c7520`, called by
the gate fee `FUN_004d0600` after its deposit and its gate takings, adds one to it whatever the fee, nought included,
and raises `mMostPaidForTicket` (`+0x21c40`) to the fee where the fee is above it (a signed compare, `0x004c754f`);
then it sends the challenge manager its message `0x1a` (`ride-operation.md`, the fee's row). Nothing else writes either
but the constructor `FUN_004c58c0` (nought, nought) and the serialiser `FUN_004c5d70`. It is not the header's
`mNumberOfVisitorsToDate`, which counts arrivals. **The month's close samples it into the 11th int ring** (`+0x20820`,
its slot `+0x20a64`, months `+0x20824`; `0x004c7904`..`0x004c792a`): the count as it stands, its month so far
`+0x20820` never written. **Its readers**: the park status screen (`FUN_004a52a0`, I) letters it as "Total park
visitors" (`0x4a68ca`, `FUN_004c94c0`), as it opens and on its one-second timer (`0x004a48b3`), beside "People in park"
(`FUN_004c9130`); the golden tickets' Local 0 and Local 5 (`FUN_004d4bc0`; Local 5 through `FUN_004c94d0`, the only
reader of the ring: the count less the month that many back); and advisor rows 170, 314 and 316's scores (`0x0059fa82`, `0x005a0557`, `0x005a05b2`; 314 and 316 score when the
count is above their product, each with preconditions of its own: `0x0051a280`, `0x004d4ac0`, `0x005af7f0`). **Nothing reads
`mMostPaidForTicket`**: the image's every operand `+0x21c40` is the constructor's, the serialiser's two and
`FUN_004c7520`'s.

**Measured** (`lib/ringsweep.py`, `q389/ratio.py`): the shipped `Easymode.TPWI` holds 8 and 25, the 11th ring one month
closed on 0, and its gate month so far is 200, eight fees of 25. Over all 584 park files on hand the 11th ring never
falls, its newest month is at most `mLifetimeVisitors` and its month so far is 0; in all 578 where it stands on the gate
ring's slot, the gate's month so far is `mLifetimeVisitors` less the ring's newest month, times one fee (25 in 561, 20
in 4, no admission since in 13), never above `mMostPaidForTicket`.

**OpenTPW** (Q389). `ParkState.LifetimeVisitors` and `MostPaidForTicket`, the file's at a load (`ParkWorld`) and nought
in a park made with no file; `Take`, the gate fee, raises them as `FUN_004c7520` does; `TurnTheMonth` pushes the count
into `MonthVisitors`; the writer writes all three (`ParkWorld.PutLifetimeVisitors`, `PutMonthRings`); `money` prints
them. The challenge message is counted (`GATE_FEE_CHALLENGE_POST`). Nothing here reads them: the park status screen is
not built (`PARK_STATUS_SCREEN`); the golden tickets' Local 0 reads the lifetime visitors and Local 5 the ring (Q591). **Confirmed** (`q389/`): the stock park with its fee
written to 26 took 18 fees before its close on tick 1474 (`Park: lifetime visitors 9` .. `26, most paid for a ticket
26`), closed `visitors 26` with the gate's 668 = 200 + 26 x 18, and wrote 26, 26 and the ring [0, 26]; main's build
wrote the file's 8, 25 and [0]. The original given this game's file read 26, 26, [0, 26], closed 26 into its third slot
at once, and its park status screen lettered "Total park visitors 26" (`q389/orig/a-2-status.png`); given main's file,
"8" (`c-2-status.png`).

#### The guests inside and the rides at a close

Read 2026-10-15 (Q390). **The month's close samples two counts into the 10th and 12th int rings**, each a thiscall on
the analyser with one argument, a number of quarter ticks, asked with nought (`PUSH EBP`, `0x004c78b1` and
`0x004c7929`), so counted afresh unless the same quarter tick counted already; each keeps its answer as the park value
does (`FUN_004c7fa0`: stamp `+0x1f0b8`, count `+0x1f0bc`, flag `+0x1f0c0`; `FUN_004c8070`: `+0x1f0c4`, `+0x1f0c8`,
`+0x1f0cc`), and the serialiser writes their counts and flags, not their stamps (`0x004cd9c0`).

- **`FUN_004c7fa0`, the guests inside** (`+0x205d0`, its slot `+0x20814`, months `+0x205d4`; `0x004c78c1`..`0x004c78f1`):
  the used thing list (`FUN_00516130`, `FUN_00516160`) walked for things of kind 1 (`+2`, guests) on whose cell
  (`+5` x, `+7` y) `FUN_004fa990` passes, a type 1, 3, 9, 10 or 0 (`FUN_00536310`, `20`, `40`, `50`, `90`): the count
  the gate's close takes (`FUN_004c9130`, `park-gate.md`), the music's crowd (`FUN_004c81e0`) and the strike's look
  (`FUN_00508e70`, asked with 5).
- **`FUN_004c8070`, the rides standing** (`+0x20a70`, slot `+0x20cb4`, months `+0x20a74`; `0x004c7933`..`0x004c7964`):
  the same list walked for things of kind 3 whose item (`FUN_00412e90` on `+0xe`) has `+0x4ac`, `Info.WhichUIType`,
  nought; nothing else of the object is asked, so a ride shut, broken or condemned counts, and so does a placed upgrade
  (the `upgrades` folder's category is 0).

Neither month so far is written. **Readers**: the park status screen's graph (`FUN_004a4b90` and its draw, bit `8` of
`DAT_00753c48` from its fourth series button, `0x4a68bf`) plots the 10th beside the 13th (`+0x20cc0`), the 14th's float
(`+0x20f10`), the 15th (`+0x21160`) and the 16th (`+0x213b0`); its labels are not read. **Nothing reads the 12th**: its
every operand is the constructor's, the close's, the serialiser's and the load factory's (`FUN_005179c0`, which empties
every ring before the read, `FUN_0051ae30`).

**The graph's scale, a ring's largest month (`0x004801d0` over integers, `0x004802a0` over floats; fork watch 2026-10-19,
`verify-world.md` L4).** The argument 0, 1 or 2 gives a window of 12, 36 or 144 months. The samples are `0` to the window
inclusive, and stop at the ring's count (`+0x248`): sample `i` is `data[ head - i ]` (`+4`; the head is `+0x244`), or
`data[ head - i + count ]` past the start only when the wrapped byte (`+0x24c`) is set, else the scan stops. A sample is kept
only when strictly greater than the best so far (`0x00480323`..`0x0048032c` for floats); the float is truncated
(`0x0067a830`) and raises `*out` if greater. Both keep the **index** of the best (start -1) and re-read the ring there after
the loop, so **with no sample above nought** the index is -1 and they read `data[ head + 1 ]`, the slot past the newest: the oldest month in a wrapped ring, an
emptied slot in an unwrapped one, and at head 143 the head word `+0x244` itself. Only an index past an unwrapped head takes the
limit fallback (`0x0048028d`, `0x00480365`). So an all-nought recent window takes its scale from that slot. (The fork's "0 when
none is above 0" is wrong.) In each of the status screen's seven folds (`FUN_004a4b90`, `0x004a51d0`, `0x004a5e10`; e.g.
`0x004a4c86`..`0x004a4d27`) bit 2 of `[0x00753c48]` takes the 14th ring (`+0x20f10`, the happiness float) through `0x004802a0`,
bits 1, 4, 8 and `0x10` take `+0x20cc0`, `+0x21160`, `+0x205d0` and `+0x213b0` through `0x004801d0`; then `0x0047fb30( 0, max, 10 )`.
The finances screen (`0x0049a7e2`..`0x0049a8d7`) uses `0x004801d0` only. Nothing here is built.

**The graph's backdrop (fork watch 2026-10-20, `verify-other-ui-audio.md` A-L1; read from the bytes, not run).** The span primitives
are `0x005f9860( x, y, len, colour )`, which clips (x, y)-(x+len, y+1), and `0x005f98d0`, which clips (x, y)-(x+1, y+len): the
third argument is a **length**. **Setup** `0x004806a0( graph, rect, ymin, ymax, ydivs, xmin, xmax, count )`: w = (rect.r - rect.l)
x `[0x00faa5c4]` / 2048, h = (rect.b - rect.t) x `[0x00faa5c0]` / 1536 (the control's size at the screen's resolution);
`0x0047fb50( 4, 4, w - 20, h - 20 )` stores left `+8`, top `+0xc`, right `+0x10`, bottom `+0x14`, width `+0` and height `+4`;
`0x0047fb30` stores `+0x1c` ymin, `+0x24` ymax, `+0x2c` the divisions, and `0x0047fb10` `+0x18`, `+0x20`, `+0x28` the count. The
colours (`0x005f73a0`): `+0x30` and `+0x35` (0, 200, 0) the axes, `+0x3f` (0, 0, 0) the fill, `+0x3a` (0, 100, 0) the grid. The
status screen's call (`0x004a5132`) passes 10 divisions and a count of 12 (`0x004a511f`..`0x004a5124`); the finance call
(`0x0049a734`) pushes 10 and 12 too (how its 36 and 144 reach `+0x28` was not read). **Draw** (`0x004808bd`..`0x004808cb`): the fill
`0x0047fc00`, the grid `0x0047fcd0`, the axes `0x0047fc50`.

- **Grid columns** step by the sample count: 12 draws every sample, 36 every 3rd, 144 every 12th (`0x0047fd06`..`0x0047fd5d`); **any
  other count leaves the step an unwritten stack word, an original defect**. x starts at left + width / count, a float advanced each
  sample; a column is `0x005f98d0( (int)x, top, bottom )` (bottom as the length), labelled `%d` (`0x0074f9d8`, wide) with sample + 1
  at (max( x, 2 ), max( bottom + 4, 2 )), through `0x00492750` (each coordinate also takes `0x7d00`, not read).
- **Grid rows**: dy = height / divisions, y0 = bottom - dy - (-1.0) (`0x006ff06c` is `0xbf800000`), each drawn
  `0x005f9860( left, (int)y, right )` (right as the length), y -= dy.
- **The zero axis** `0x0047fc50`: row = bottom + 1, or bottom + height x ymin / (ymax - ymin) when ymin < 0 and ymax - ymin > 0;
  `0x005f9860( left - 1, row, right + 1 )` and `0x005f98d0( left - 1, top - 1, bottom + 1 )`.

**Measured** (`q390/sweep.py`): the shipped `Easymode.TPWI` holds the 10th one month closed on 0 and the 12th on 1, the
Belly Bounce; over all 587 park files on hand both months so far are 0; in all 188 whose park value sums to their 2nd
ring's newest month (untouched since their close) the 12th's newest is the file's kind-3 things of `WhichUIType` 0; the
10th's newest is at most the file's guests in 586 (the exception, `q253/orig/sold-by-the-original.TPWS`, closed on 30
and holds 27 guests).

**OpenTPW** (Q390). `ParkState.MonthGuestsInside` and `MonthRides`, the file's at a load and empty in a park made with
no file; `TurnTheMonth` pushes `ParkPeople.GateGuestCensus` and `ParkState.RidesStanding` (the chain's objects whose
catalogue item is `UiType` 0) after the money rings, around the lifetime visitors, before the park value; the writer
writes both (`PutMonthRings`); `money` prints them. Each count is taken afresh where the original's can be one of the
same quarter tick (said at `RidesStanding`). Nothing here reads them: the park status screen is not built
(`PARK_STATUS_SCREEN`). An upgrade placed in an Instant Action park is not catalogued here (no `Easy_` file), so it is
not counted (none ships placed in Lost Kingdom's file). **Confirmed** (`q390/`): the stock park with a Crazy Ape bought
closed on tick 1470 with `guests inside 24` (the `guests` census 29 a moment on) and `rides 2`, and wrote [0, 24] and
[1, 2]; main's build left the file's [0] and [1] a slot behind the money rings. The original given this game's file read
[0, 24] and [1, 2] and its own close at once pushed 24 and 2, its own counts of the same park (`q390/orig/a-load.log`,
`side-by-side.png`).

#### The arrivals

Read 2026-10-15 (Q391). **The 13th int ring (`+0x20cc0`, its slot `+0x20f04`, months `+0x20cc4`) counts the guests the
arrival vehicles drop off**: the arrival manager `FUN_004cf3e0` (`park.md`, "Arrivals"), while the vehicle answers 2
and some of the load are still to drop, makes one guest (`FUN_004cf720`, its answer not looked at), lowers the count
still to drop and adds one to the month so far (`0x004cf56d`..`0x004cf592`); nothing else adds to it (the image's every
operand `+0x20cc0`: the constructor's nought, the serialiser's, the load factory's, the close's and the graph's). The
month's close pushes the month so far into the ring and zeroes it, first after the seven money rings
(`0x004c788c`..`0x004c78bb`). **The same manager pushes each load's size, as the load is called, into a ring of the
last five loads** (read 2026-10-16, Q396): the analyser's ring shape, `mTemp` `+0x216d8` (nought, never written but by
the constructor and the load factory), the five loads `+0x216dc`, slot `+0x216f0` (-1 before the first), count
`+0x216f4` 5, wrapped `+0x216f8` (`FUN_004c58c0` `0x004c5b20`..`0x004c5b36`; the load factory's `FUN_0051ae90` the same).
The push (`0x004cf41f`..`0x004cf451`) takes the headcount `FUN_004cf5b0` answered, after the offloading flag is set and
before any vehicle is asked: the slot one on, round to nought and wrapped past the fifth, then the size into it. The
constructor fills no load, so a slot no load has reached holds whatever memory held (`0xcdcdcdcd` in the shipped park,
nought in the original's own fresh parks). The serialiser reads and writes it whole (`FUN_004ce660`, the stays' shape,
from `0x004c69e8` and `0x004c70de`). It has three readers: advisor row 124's score (`FUN_0059f220`, at `0x005a368f`: the newest load against the one
before, only while the park is younger than `[+0x3c8]` 30-day months), row 140's (`FUN_0059f440`, at `0x005a3c50`: the
newest load), neither of them built, and `AdvisorScore_GoldTicketExplanation` (`0x005a0780`, at `0x00772088`, the
`+0x10` of advisor row `0x119` in the metadata table): how many loads have been called (five once wrapped, else the slot
plus one), and `GoldTicketExplanation.Score` (`+0x5f4`) when that is exactly one; built for Q708 (`ride-operation.md`,
"Golden tickets", "The hints"). **Readers**: the park status screen's graph plots the 13th as its first series (bit
`1` of `DAT_00753c48`; `FUN_004a51d0`, `FUN_004a5e10` and the draw at `0x004a4750` take its month so far's address,
the ring's head); its labels are not read.

**Measured** (`q391/sweep.py`, `law.py`): the shipped `Easymode.TPWI` holds one month closed on 13, its thirteen guests,
and nothing so far. Over all 589 park files on hand the month so far is never below nought, and the ring stands on the
cash-in ring's slot in 574; the 15 others are this game's own files written past a month before this was kept. The
header's `mNumberOfVisitorsToDate` is no partner of it: the months and the month so far sum to it in 24 of the 42 files
whose ring has moved.

**OpenTPW** (Q391). `ParkState.MonthArrivals`, the file's at a load and empty in a park made with no file;
`ParkState.AddArrival` adds one where `ParkPeople.StepArrivals` drops a guest (`Admit` made one); `TurnTheMonth` closes it
after the sideshow takings; the writer writes it (`PutMonthRings`); `money` prints it as month so far / newest / slot.
Nothing here reads it: the park status screen is not built (`PARK_STATUS_SCREEN`). The ring of five loads is kept (Q396).
**Confirmed** (`q391/`): the stock park's one load of the month, 13 arriving on tick 1264 and all off on 1313, read
`arrivals 13/13 slot 0`, and a file saved then held 13 so far; the close on tick 1470 named `arrivals 13`, and the file
held [13, 13] on the money rings' slot; main's build left [13] a slot behind. The original given this game's file read
[13, 13] and its close at once pushed 0; given the mid-month file, 13 so far, pushed whole by its own close on tick 1383
(`q391/orig/a-load.log`, `b-load.log`, `side-by-side.png`).

**The ring of five loads, measured** (`q396/sweep.py`): in all 608 park files on hand its count is 5 and its `mTemp`
nought. In 584 of the 599 whose ring has not gone round, the loads called sum exactly to the guests the 13th ring has
counted dropped (its months and its month so far); the 15 others are this game's own files, written before it kept the
ring, which held the file's one load while the guests dropped. The nine that have gone round (Alexah's Full Simulation
jungle park) hold 35, 35, 46, 46, 50, the newest the 13th's month so far of 50. **OpenTPW** (Q396):
`ParkState.ArrivalLoads`, the file's at a load (its unreached slots as the file holds them) and five noughts in a park
made with no file; `ParkState.AddArrivalLoad` pushes where `ParkPeople.StepArrivals` calls a load, and the console's
`load` (`ForceArrival`) pushes the same; the writer writes it (`PutMonthRings`); `money` prints it as slot / wrapped /
newest. A load the original calls of nobody (world state 4, or the park full) is not called here, so not pushed
(`ParkPeople.StepArrivals`, its two refusals). Nothing here reads it. **Confirmed** (`q396/`): the stock park read
`loads 0/0 newest 13`; its load called on tick 1264 logged `Park: a load of 13 into the arrival loads' slot 1, 2 called`
before any guest dropped, and files saved then and at the load's all-off on 1313 held [13, 13] and the file's
`0xcdcdcdcd` beyond; main's build left [13] while the guests dropped. The original given the all-off file read [13, 13],
pushed its own load of 12 into slot 2 on tick 1916 before its first drop, the 13th's month so far rising to 12, and then
12 and 11 into slots 3 and 4; given the call file, it dropped the load's 13 and called none (`q396/orig/a2-load.log`,
`b-load.log`).

#### The guests' happiness at a close

Read 2026-10-15 (Q392). **The 14th ring (`+0x20f10`, its slot `+0x21154`, count `+0x21158`, wrapped `+0x2115c`, months
`+0x20f14`) is the analyser's one ring of floats**: the close (`0x004c796d`, after the 12th's push) calls `FUN_004c7bb0`
(a thiscall with one stack argument it never reads, `RET 4`; it takes the thing manager `[0x0080239c]` for itself) and
`FSTP`s its answer into the next slot (`0x004c79a0`); nothing writes the month so far, nought in every file.
`FUN_004c7bb0` answers `0.0` (`[0x0070031c]`) while the park is shut (`FUN_0051a280`, the world's `mParkClosed`
`+0x1da710`); otherwise it walks the used thing list for things of kind 1 (`+2`, guests) on whose cell `FUN_004fa990`
passes, the 10th's walk and test (above), and for each one `__ftol`s the happiness float `+0x19c`, keeps its low byte,
and adds it to a whole-number sum (`FILD`, `FIADD`, `__ftol`, `0x004c7c46`..`0x004c7c67`); the answer is the sum over
the count (`FIDIV`), `0.0` with none. The 10th's `FUN_004c7fa0` can answer its kept count of the same quarter tick, this
one walks every time. **Readers**: the park status screen's graph (above); its labels are not read.

**Measured** (`q392/sweep.py`, `law.py`): the shipped `Easymode.TPWI` holds one month closed on `0.0`, nothing so far.
Over all 593 park files on hand every month is a float from 0 to 255 and the month so far is nought; the ring stands on
the cash-in ring's slot in 576. Of the 270 closed months with guests inside (the 10th above nought), the mean times the
10th's count is a whole number in 243; the 27 others are three months of one Full Simulation park in nine copies
(`1FullSimCheated`, jungle), where the first fits only a count of 52 for the 10th's 49 (the 10th's kept answer, by my
reading; not looked into). Every closed month with nobody inside reads `0.0`.

**OpenTPW** (Q392). `ParkState.MonthHappiness`, the file's at a load and empty in a park made with no file, its floats
kept as their bits (`ParkMonthRing.Push( float )`, `NewestSingle`); `ParkPeople.GuestsInsideHappiness` is the mean
(the guests `GateGuestCensus` counts, nought with the park shut); `TurnTheMonth` pushes it after the rides, before the
park value; the writer writes it (`PutMonthRings`); `money` prints it as newest, slot and now. Nothing here reads it:
the park status screen is not built (`PARK_STATUS_SCREEN`). **Confirmed** (`q392/`): the stock park's close on tick
1470 named `guests inside 16` and `happiness 45.4375` (727 over 16), `money` read the same at slot 1, and the file held
[0.0, 45.4375] on the money rings' slot, its own sixteen guests' mean the same; main's build left [0.0] a slot behind.
The original given this game's file read [0.0, 45.438] and its own close at once pushed 44.875 with 16 inside (718
over 16); given main's file, its close pushed 46.542, the mean of that file's 24 guests to the last place
(`q392/orig/a-load.log`, `b-load.log`, `side-by-side.png`).

#### The leavers' stays

Read 2026-10-15 (Q393). **The 15th int ring (`+0x21160`, its slot `+0x213a4`, count `+0x213a8`, wrapped `+0x213ac`,
months `+0x21164`) is the mean stay of the last fifty guests to go home, in whole park days.** A ring of the same shape
fifty long (`+0x21600`: `mTemp` `+0x21600`, never written, entries `+0x21604`, slot `+0x216cc`, count `+0x216d0` 50 from
the constructor, wrapped `+0x216d4`; serialised by `FUN_004ce2d0`, the int rings' `FUN_004cdf00` at another length)
holds the stays: a guest entering state `0x15`, at the stop to go home, whose `mPaidAdmission` (`+0x204`) is set hands
the analyser their stay (`FUN_00501db0`, `0x00502333`): `FUN_004fd950` (the ticks since `mArrivalDate`, unsigned,
shifted right by two) shifted back left by two (`SHL` at `0x0050234a`, a 32-bit register), and `FUN_004c7600` turns
that into park time with `FUN_004f8730` on the park calendar (times its rate `+0x1c`, over four, times 10,000,000 in 64
bits, which wraps) and `__alldiv`s it by 864,000,000,000, signed: whole days, the low half pushed into the next slot.
Nothing else writes it (the image's every operand `+0x21600` to `+0x216d4`: the constructor's, the serialiser's, the
load factory's empty and the close's read). **The month's close**, after the park value (`0x004c79d7`..`0x004c7a40`):
`FUN_004cef30` on the stays with fifty (`PUSH 0x32`) takes the newest min(50, filled) (filled the count once wrapped,
else slot + 1), newest first, sums them into a double and answers how many it took (`RET 8`, `EAX`); with any, the
close divides (`FDIVR`) and `__ftol`s (toward nought), else nought, and pushes that into the 15th. The stays are left as
they are. The same stretch zeroes `+0x21bfc` and `+0x21c00`, this month's misbehaving kids (below, "The misbehaving kids"). **Readers**: the
park status screen's graph (above, "The guests inside and the rides at a close"); that its series is the one lettered
"Average time in park" is not read from a label.

**Measured** (`q393/sweep.py`, `law.py`): the shipped `Easymode.TPWI` holds the stays empty (slot -1 of 50) and the 15th
one month closed on 0. Over all 596 park files on hand the stays' count is 50 and their `mTemp` 0, the 15th's month so far
0, on the cash-in ring's slot in 576. In all 34 files whose stays hold any and have not wrapped, every closed month of
the 15th is the truncated mean of the stays' oldest k for some k, the leavers before that close; the 553 with none read
0. The 9 wrapped (one Full Simulation jungle park's copies) cannot be checked, the window having moved. Stays seen run
from 18 to 587 days.

**OpenTPW** (Q393). `ParkMonthRing` takes a length (`Stays`, 50); `ParkState.LeaverStays` and `MonthMeanStay`, the file's
at a load and empty in a park made with no file; `PeepBehaviour`, where a guest walking outside reaches the stop, hands
a paying guest's `Peep.StayAt` to `ParkState.AddLeaverStay` (`StayDays`) after the state is set, where the original's
setter does it; `TurnTheMonth` pushes `MeanOfNewest( 50 )` after the park value; the writer writes both
(`PutMonthRings`); `money` prints them. Nothing here reads them: the park status screen is not built
(`PARK_STATUS_SCREEN`). **Confirmed** (`q393/`): in the stock park four guests reached the stop before the close, 125,
193, 194 and 199 fours of sweeps, logged 21, 33, 33 and 34 days; the close on tick 1470 named `mean stay 30` (121 over
4), and the file held the 15th [0, 30] on the money rings' slot and the stays [21, 33, 33, 34]; main's build left [0] a
slot behind and no stays. The original given this game's file read both and its own close at once pushed 30, then its
next leaver handed in 35 days; given main's file, its close pushed 0 (`q393/orig/a-load.log`, `b-load.log`,
`side-by-side.png`).

#### The park score

Read 2026-10-15 (Q394). **The 16th int ring (`+0x213b0`, its slot `+0x215f4`, count `+0x215f8`, wrapped `+0x215fc`,
months `+0x213b4`) is a score of the park out of a hundred**, the last thing the close pushes (`0x004c7a49`, a thiscall on
the analyser, then the ring's step written out to `0x004c7a7a`). `FUN_004cceb0` adds eleven counts, each capped, every
division whole:

| Count | Asked of | Points | Most |
|---|---|---|---|
| the guests inside | kind 1 (`+2`) on a cell `FUN_004fa990` passes, walked twice, 1,000 at most | n x 20 / 1000 | 20 |
| the rides | `FUN_004ca8e0( 0, 0 )`: type 3, item `+0x4ac` (`Info.WhichUIType`) 0 | n x 3 / 2 | 20 |
| the shops | `FUN_004ca8e0( 1, 0 )` | n x 2 | 10 |
| the sideshows | `FUN_004ca8e0( 2, 0 )` | n x 2 | 10 |
| the features | `FUN_004ca8e0( 3, 0 )` | n | 10 |
| the rides upgraded | `FUN_004ca8e0( 0, 2 )`: a ride whose `mUpgradeLevel` byte (`+0x50`) is 2 or over; the level is asked of rides alone | n | 10 |
| the handymen, the mechanics | the thing list walked inline (`FUN_004cd300`, `FUN_00481f80` a staff type), type 5, then 4, `+3` nought | n | 4 each |
| the entertainers, guards, researchers | `FUN_004c9960( 6 )`, `( 7 )`, `( 8 )`: type byte, `+3` nought | n | 4 each |

The fixed items (`WhichUIType` 4) count for nothing. **Readers**: the park status screen's graph (above, "The guests
inside and the rides at a close"); which of its labels names it is not read.

**Measured** (`q394/sweep.py`): the shipped `Easymode.TPWI` holds the 16th one month closed on 18: no guest inside, the
Belly Bounce (1), the Drinks Shop (2), the Jungle Spray (2), eight features (8) and one of each kind of staff (5). Over
all 598 park files on hand its month so far is 0, on the cash-in ring's slot in 576. In 180 of the 197 untouched files
(the park value its file's own, Q388) the newest month is the reckoning from the 10th ring's guests and the file's
things; each of the 17 others differs by the staff hired or gone since its close. Alexah's Full Simulation jungle park
reads [72, 72, 73, 74], its last month 74 where the file's twelve rides reckon 73: its 12th ring says thirteen stood at
that close.

**OpenTPW** (Q394). `ParkScore.Of` counts the objects by their item's `UiType` and level and takes the guests inside and
`ParkPeople.ScoreStaff` (a member in the hand counts; one fired here is gone at once); `ParkState.MonthParkScore`, the
file's at a load and empty in a park made with no file, is pushed last in `TurnTheMonth`; the writer writes it; `money`
prints it with the score now. Nothing here reads it: the park status screen is not built (`PARK_STATUS_SCREEN`).
**Confirmed** (`q394/`): the stock park with the Belly Bounce written to level 2 (`bounce2.TPWS`), loaded; `money` read
`park score 18 slot 0 now 19`; the close on tick 1470 (22 guests inside) named `park score 19`, and the file held the 16th
[18, 19] with every ring on slot 1; main's build left [18] a slot behind. The original given this game's file read
[18, 19] and its own close at once pushed 19; given main's, its close pushed 19 into slot 1 with its 15th on slot 2
(`q394/orig/c-load.log`, `b-load.log`, `side-by-side.png`).

#### The misbehaving kids

Read 2026-10-16 (Q395). **Three counts sit before `mLifetimeVisitors`**: `mMisbehavingKids` (`+0x21bfc`) and
`mMisbehavingKidsThatGotAwayWithIt` (`+0x21c00`), this month's, and `mMisbehavingKidsEver` (`+0x21c04`). Their one adder
is `FUN_004c74f0( caught )`: the first and the third one more, the second one more where `caught` is nought. Its one
caller is the guard's chase, `FUN_004d6790`: the catch ("Guard: Caught a kid", `0x004d691b`) hands 2, every other end
("Guard: abandoned chase", `0x004d6a1f`) hands nought. Nothing else writes them (the image's every operand `+0x21bfc` to
`+0x21c04`): the constructor `FUN_004c58c0` zeroes all three, the close zeroes the month's two (`0x004c79eb`,
`0x004c79f1`, `EBP` nought, after the park value and before the stays' mean), and the serialiser `FUN_004c5d70`.
**The serialiser's read never reads the third**: where it reads the first two with `FUN_004fae10` (the stream's read,
vtable `+8`, "File read failed"), it hands the third to `FUN_004ced70` (`0x004c719a`), the write (vtable `+0xc`, "File
write failed"), so a load writes the analyser's word over the buffer and the stream moves on four: the lifetime
visitors that follow read right. The load factory `FUN_005179c0` allocates the analyser (`0x21c50` bytes) without
zeroing these three, so after a load the third is whatever that memory held: 0 in two loads, -999 in a third. **The one
reader** is `FUN_004c94a0( got away )` (the second if non-nought, else the first), called from five places, all advisor
scores. With nought (this month's): `0x005a04f0`, the polled row 264's (message `0x108`, its first response 514):
`LotsOfPranks.Score` (100) when this month's count is over `LotsOfPranks.MorePrankstersThan` (2), else nought; and
`0x0059cd30`, row 12's, a park with no guard (`advisor-park.md`, "A park with no guard", Q413). With one (this month's
got away): `0x0059d3dd` in row 22's score `0x0059d3b0` (with a guard on the staff, a key's score past a key's count; its
lines 64 to 66 ask for security cameras; `advisor-park.md`, "Kids who got away from a guard", Q423), and `0x0059e853` and `0x0059e871` in row 98's `0x0059e840` (message `0x62`,
`advisor-park.md`, "Kids who got away", Q418). Nothing reads the third.

**Measured** (`q395/sweep.py`): of 608 park files on hand, 594 hold nought, nought and nought; nine hold 0, 0, 3 (Alexah's
Full Simulation jungle park and its copies); one the original wrote holds 1, 1, 1 (`q253/orig/sold-by-the-original.TPWS`,
a chase abandoned that month, counted since its load).

**OpenTPW** (Q395). `ParkWorld.MisbehavingKids` reads the three (record `+73468`), `PutMisbehavingKids` writes them;
`ParkState.MisbehavingKids` takes the file's month two at a load and nought for the third, where the original's is its
load's unzeroed memory (a deviation, said at the site); `TurnTheMonth` zeroes the month's two; `money` and the close's
line print them. Nothing counts into them: the guard's chase waits on the pranks (`DECIDE_PRANK_*`). Row 12 reads this
month's (Q413), row 264 too (Q414; `advisor-park.md`, "Lots of pranks"), and row 98 this month's got away (Q418; `advisor-park.md`, "Kids who got away"), and row 22 the same with a guard on the staff (Q423; `advisor-park.md`, "Kids who got away from a guard"). With them the close is built whole (`ANALYSER_MONTH_CLOSE` gone).
**Confirmed** (`q395/`): the stock park with the three written 5, 2 and 9 (`kids.TPWS`), loaded; `money` read `misbehaving
kids 5 got away 2 ever 0`; the close on tick 1470 named `misbehaving kids 0 got away 0 ever 0`, the file held 0, 0, 0 and
`unimplemented` no longer listed the close; main's build kept 5, 2, 9 in the file and listed it. The original given
main's file read 5, 2 and 0 (not 9) and its close on tick 1471 zeroed the two on the tick its ring 1 moved; given this
game's, 0, 0, 0 throughout (`q395/orig/c-load.log`, `d-load.log`, `side-by-side.png`).

#### The analyser's other stamps

Read 2026-10-16 (Q397). **Of the four stamps after `mLifetimeVisitors`, one moves.** Every operand `+0x21c10` to
`+0x21c2c` in the image: the constructor `FUN_004c58c0` (all four the calendar's now), the load factory `FUN_005179c0`
(zeroed, then the file's), the serialiser `FUN_004c5d70` (read `FUN_00510240`, write `FUN_004cede0`, eight bytes
each), and `FUN_004c7370`, the analyser's message handler (the dispatcher `FUN_0050b550`, model 13). Nothing reads
any of the four. In that handler:

- **Message `0x13` carrying 3 stamps `mParkLastOpened` (`+0x21c28`)** with the calendar's now (`FUN_004f8690` on world
  `+0x2a0`, `0x004c7444`..`0x004c747b`). The park door `FUN_00519ef0` posts it from its open arm (`0x0051a031`)
  **whether or not the park was shut**: an open request to an open park jumps straight to the post (`0x00519f7c`).
  The close arm posts 4 (`0x0051a1c1`), which the handler ignores; the end of the park (second argument non-nought)
  posts nothing. The analyser joins set `0x13` in its constructor (`FUN_004c7a90`, with `0xc` and `0x11`), and set
  `0x13` is things 3, 4, 5, 8 and 9 in the shipped park, the advisor first.
- **Message `0x17` carrying 2 would stamp `mLastStrike` (`+0x21c10`), and never does**: the strike's message
  (`FUN_00508f70`) goes to set `0x17`, which the advisor joins (`0x00599f08`) and the analyser does not. A load takes
  the sets from the file (`FUN_0040fd60`), and set `0x17` is the advisor alone in all 612 park files on hand
  (`q397/sweep.py`, `parkfile.sets`). Dead by CODE in the original.
- **`mLastPuke` (`+0x21c18`) and `mLastPrank` (`+0x21c20`) have no case**: only the constructor writes them.

**Measured** (`q397/sweep.py`): in all 612 park files the strike, puke and prank stamps are the calendar's start;
`mParkLastOpened` is later than it in 609 (`Easymode.TPWI`'s is tick 15).

**OpenTPW** (Q397). `ParkWorld.ParkLastOpened` reads it (record `+73508`), `PutParkLastOpened` writes it;
`ParkState.ParkLastOpened` takes the file's at a load and the calendar's start with no file; `SetParkClosed( false )`
stamps the calendar's now every time, the park open or not, and logs `Park: mParkLastOpened stamped <date> on mGameTick
N`. The other three are not kept: the file's go out as they came in, as the original's would.
**Confirmed** (`q397/`): the stock park paused on tick 763, the door clicked shut (no line) and open: `Park:
mParkLastOpened stamped 3/2/2000 02:47:30 on mGameTick 763` (`run-fix1/1-opened.png`); the file held tick 763's, the
three others the calendar's start, and held it again loaded from the list and saved; main's build left tick 15's. The
original given that file read tick 763's, kept it as its door shut on tick 896, and stamped tick 917's exactly as it
opened (`q397/orig/a-load.log`, `a-o-3-opened.png`).

#### The first row is picked as a list fills

Read 2026-10-14 (Q356). **The list's own add picks it.** `FUN_0066403b`, once its count reaches 1, calls
`FUN_0066525c( 0 )` (`0x006643d4`), which selects slot 0 plus the top row (`+0x14c`) and, where that moves the
selection, hands `0x401` with the list's id and the selected INDEX to `FUN_00658ccc`, which QUEUES it (`FUN_006697e3`, a
ring of four-word messages): posted, not sent. The clear `FUN_006649d5` sets `+0x14c` to -1 and the top row to 0 and
posts nothing, so every fill after a clear picks again. `FUN_0049b5b0` (called by the opener `FUN_0049bdd0` and by the
tab handler `FUN_0049bc80`, which does nothing else but set the title) clears the list and adds the tab's candidates in
slot order through the sorted insert, and the selection stays an index, so when the screen's handler `FUN_0049b650`
takes the message (`0x0049b869`) the row at index 0 is the one sorted FIRST, read back by the index (`FUN_00664c71`,
`0x0049b894`): the skill bar, Staff costs (`DAT_007ca2fc` plus that wage) and the balance are that row's, and the
highlight is on it. Where the index names no row `FUN_00664c71` answers 0 and writes nothing, and the handler goes on
with a stale word. The row id pushed is the candidate's SLOT INDEX (0 to 0x1f), which is what `FUN_00507bd0` takes
back: the pool `[0x007cf83c]`, 32 records of 0x14 bytes, `+0` the kind, `+4` the name's row (`FUN_005074a0`), `+8` the
grade, `+10` in use.

**The label `0x2483` reads UITEXT 146, "Skill"**, put on once by the builder (`FUN_00485b00( 0x92 )`, `0x0049c2c8`, the
only `PUSH 0x2483`); nothing letters a candidate there.

**Measured in the original** (Q356, `q356/orig/`, `q353/grades.TPWS`, the pool read by `staffpool.py`): opened on the
janitors on tick 1106 with slots 0 (grade 1, $ 36), 11, 18 and 19 (grade 3): Chris Battson (slot 11) stood first and
was highlighted, the bar at 0.617 and Staff costs $ 63, not those of slot 0, handed over first and standing second. On
the mechanics' tab Alex Cullum stood first and was highlighted, $ 92, the bar 0.209 (also the first handed over). Q353's
`a-tab.png` highlights its fifth row because `gmove.py` glides diagonally, and its way down from the tab crossed rows 0
to 4 ("The pointer over a list"). The original's pool at the load was this game's exactly; slot 7 timed out on tick 1066
and the top-up on tick 1083 added ten, which is why Q353's original, clicked after it, showed five janitors to this
game's two.

**OpenTPW** (Q356): `UiList.PicksFirstRow` builds the add's pick, the top row selected as the first row goes in and its
index posted; `TakePosted` hands the row standing at that index to `SelectionChanged` when the screen's `Update` calls
it. The hire, all-items and all-staff (Q382) lists set it; the other lists count the pick (`LIST_FIRST_ROW_SELECTED`, Q232). The picked wage stays in
Staff costs until the next pick. On the same file (`q356/run-fix2`) the opening logged `Hire list: added 7,11, row 11
picked` and `Hire screen: candidate 11 grade 3, skill bar 614 of 1024, staff costs 502`, the bar 0.606 with Chris
Battson's row highlighted, and the mechanics' tab `added 3,8,10, row 8 picked`, candidate 8 grade 2, 409, staff costs
554 (Karl Fitzhugh's $ 115 for Chris's $ 63).

#### A tab put down by the opener fills nothing

Read and measured 2026-10-14 (Q364). Each tabbed screen's opener fills its list itself and then puts its tab down with
`FUN_0066a295` (hire `FUN_0049bdd0`, buy `FUN_004acc70`, all items `FUN_00495aa0`, all staff `FUN_00496620`). That
call lifts the old button and puts the new one down through `Button_SetDown( down, 1 )`, which posts the button only
message 1 (`FUN_00658ccc`); the tab handlers fill on `0x101` with a fourth word of 1 (`FUN_0049bc80`, `FUN_004aca60`),
which a click sends. **Measured in the original** (`q364/orig/a`, `brk.sh` on the hire fill `0x0049b5b0` and the buy
fill `0x004aaf70`, `q353/grades.TPWS`): the gadget's Buy one buy fill (return `0x004ad15d`), b_allstaff one hire fill
(`0x0049c20d`), the mechanics' tab one (`0x0049bcd6`), b_allthings one buy fill: four hits for four steps. A click on the tab
already down fills nothing (`q364/orig/b`): the janitors' tab as the screen opened, then the mechanics' tab a second
time, no hit either time.

**OpenTPW** (Q364): `UiRadioGroup.Put` is `FUN_0066a295`, a button put down with no handler told; `Select` is a click,
`Put` and then the handler. The hire, buy, all-items and all-staff screens' fills call `Put`, so each fills once as it
opens and once a tab, where `Select` there had the handler fill again inside the fill: twice on every opening. On the
same file and route (`q364/run-fix1`) the log held one fill line a step, `Hire list: added 7,11, row 11 picked` once
where main's build logged it twice (`run-control1`), and the all-staff and all-items screens one `filling` line on
opening and one on a tab.

**The all-staff and all-items screens, read and measured 2026-10-14 (Q365).** Their tab handlers fill on `0x101` with a
fourth word of 1 alone, as the hire and buy ones: `FUN_004960a0` (each tab one `FUN_00481bc0` walk with the row adder
`0x00496320`, either inline or through `FUN_00496400`, then the label `FUN_00496570` and the kind's meter
`FUN_004964d0`) and `FUN_00495360` (each tab one sub-builder: rides `FUN_00494ec0`, shops `FUN_00494c70`, sideshows
`FUN_00494250`, misc `FUN_00495110`). The all-items opener puts its tab down before it even installs its handler; the
all-staff opener installs its handler first, and the put-down reaches it as message 1 all the same. In the original
(`q365/orig/a`, `q353/grades.TPWS`, `brk.sh` on the four `CALL`s `0x00496870`, `0x004961cf`, `0x00495b4d`,
`0x004953f0`): **S** opened all-staff with one hit at its opener's fill (through the `allstaff` stub `0x0040c52a`), the
mechanics' tab one, the same tab again none; **A** opened all-items with one hit at its rides fill (the `allitems` stub
`0x0040c51a`, from the screen's own handler running the shortcuts alone, `0x00488c1e`), the shops' tab one, the same
tab again none (`q365/orig/a-six.png`). This game's fills were already these.

**The shortcuts' screen rows** (Q366; their keys and what each opener does first are in `park-engine.md`, "The park
management gadget"). Measured in the original under Proton (`q366/orig/a`..`c`, `q353/grades.TPWS`, Instant Action), a
frame a key: A all items, and the Information button after it all items again; S, B, H, V, T and G all staff, buy, hire,
all visitors, Staff Training Budgets and the entry price, each in the last one's place; Space the map, and A over the map
nothing; I park status, F finances; L nothing, and the Money button after F and L finances; Ctrl+S and Ctrl+V a locator
panel on the gadget's arm. Ctrl+P was not pressed (the postcard writes a picture out).

**OpenTPW** (Q366): `ParkFrontEnd.ShortcutRow` is the table, a key let go matched on its key and its modifiers alike, and
`ShortcutKey` runs rows 1 to 15 where the original's table is heard: over the park, and under a park screen; not under
the game menu, a message box, the options screen, the map or the full-screen view; in first person the postcard's row
alone. A screen row calls `ParkCategoryScreens.Show`, the opener the category buttons reach, so the category is left on
it; Space opens the map as its button does; R runs the research opener ("The research opener and its two boxes").
Park status, finances and the postcard are counted (`PARK_STATUS_SCREEN`, `FINANCE_SCREEN`, `POSTCARD_SHORTCUT`), and
the two locators open ("The staff and visitor locators", Q424), loans as `LOANS_SCREEN` outside Instant Action and not at all inside it; G with the entry price up
leaves it as it is. **Rows 4 and 5 are O and E here, not the original's A and S**, which pan the camera with W and D
(Q631, `park-engine.md`, "The point of interest's edge"); the help bar's nine rows that name A and S name O and E
(`HelpBar.OurKeys`). On the stock park (`q366/run-fix2`, the final game binary): each
key's `Park keys: shortcut row N, name` line and the screen `windows` named, a frame a screen (`run-fix2/1-items-by-A.png`
to `9-map.png`); in that run, before Q631, A held a second left the camera's point where it was and W held a second moved it
200 units (now A pans and W moves about 105 a second at zoom 110: `q631/ours-run3`).

#### The staff and visitor locators

Read 2026-10-16 (Q424; the fork review 2's lead L1, `verify-ui-hud.md`, checked again in the private Ghidra). **Built
once, carried by the arm.** `FUN_004b3cf0` builds both at the interface's assembly, hidden: the stream `0x00756950`
holds two panels with the handler `FUN_004b3790`, `0x95c` (474,1159)-(941,1470) for the staff and `0x963`
(-37,1159)-(429,1470) for the visitors, each a type-8 group `0x95d` framed in `!f_plain` whose buttons (type 2, flags
`0x11`) are `0x95e` `b_shandy` (help 482), `0x95f` `b_smech` (483), `0x960` `b_sresrhcer` (486, the node `b_sresrcher`),
`0x961` `b_senter` (484), `0x962` `b_sguard` (485) and `0x964` `b_skids` (489); the group's handler `LAB_004b3b90`
puts a clicked button down whether the click put it up or down, and posts the panel `0x101` with it and 1. The stream
`0x00756ad8` is the strip `0x146` (448,964)-(1074,1066), `!frame`, help 552, its handler `FUN_004b3c10`: the name
`0x147` (font slot 11, white), the status `0x148` (`i_staffstat`), the hover `0x149` (`hilight`, shown on `0x10001`,
hidden on `0x10002`) and the thought `0x14a` (`thoughts`). The panels go on the arm through `FUN_004a2590`, so each is
put where the arm's handler puts any panel ("The arm's handler"): its left the arm's left less its width and fifty,
then ten steps of `(width + 208) / 10`, 67, so 670 out; its middle on the arm's. The staff's stands at (484,1127), the
visitors' at (485,1127).

**The keys.** Ctrl+S is `FUN_004b3f60( [0x007c2658] )` with a member's window up (`[0x007cc3c0]`), else
`FUN_004b3f60( 0 )`; Ctrl+V the same with a visitor's (`[0x007cc418]`), else `FUN_004b4280`. `FUN_004b3f60( 0 )` sets
the locator `[0x007cc2f0]` to 1 FIRST, then hands the arm the staff's panel, then noughts the person `[0x007cc308]`
and the role `[0x007cc30c]`, lifts every button (`FUN_0066a295( -1 )`), shows the status and hides the thought, posts
advisor message `0xc6`, closes the open park screen (`FUN_00485b40`) and puts the idle mode over the camcorder's pick
mode. `FUN_004b4280` hands the arm the visitors' panel FIRST, then sets role 6 and the locator 2, lifts the button,
noughts the person, hides the status and shows the thought, closes the park screen and posts `0x11f`. Given a person
(a window up), `FUN_004b3f60` picks the role and button by its type byte (5 handyman 1 `0x95e`, 4 mechanic 2 `0x95f`, 7
guard 3 `0x962`, 6 entertainer 4 `0x961`, 8 researcher 5 `0x960`, 1 visitor 6 `0x964`) and shows the strip on them.

**The arm folds a locator closed.** The arm takes a panel by folding what it carries, which sends that panel
`0x1001d8`, and the panel's answer is the locator closed: nobody, the strip hidden, `[0x007cc2f0]` nought,
`gui_CameraFlags` bit `0x1000` cleared, advisor messages `0x11f` and `0xc6` withdrawn. Because Ctrl+S sets its locator
before the fold, **Ctrl+S over a locator already out leaves the panel out and closed**: its buttons go down and find
nobody until Ctrl+S or Ctrl+V again. Ctrl+V sets its own after and is not caught. Escape asks `FUN_004816b0` first
(the game table's row 0, `0x0040c1a0`): with a locator open it folds the arm (`FUN_004a25f0( 0 )`, the camcorder button
left as it is) and is spent, so no menu opens.

**A button** (the panel's `0x101`, nothing with no locator open): from role 0 it takes up its role and the role's first
(`FUN_004b3340`); another role's drops the person and takes its first; the same role's steps to the next, round to
the first after the last (`FUN_004824d0`: the five staff lists from world `+0x1da73c` to `+0x1da744`, each member's next
at `+0x210`, newest first; the visitors the used-thing list from its head, newest first, type byte 1). Somebody found:
the strip shown if it was not, lettered (`FUN_004b3490`: a member's name `FUN_00506860`, a visitor UITEXT 456 "Visitor
#{P24}" with `+0x1d8`, or 457 "New Visitor" at nought), the camera put on them, bit `0x1000` set. Nobody: from role 0
nothing more; else the strip hidden and the bit cleared. **Each frame** (`0x1e`) with the bit set, `FUN_004b3640` puts the
camera's look-at where the person is drawn (`FUN_00486860`: `FUN_004f9f00` blends `+0x190`/`+0x194` towards the
navigator's place at the frame's fraction of the sweep, held to 0..1, truncates, and scales 10/65536; `FUN_0042aab0`
writes it, a snap) and draws a fenced member's patrol edges ("The patrol edges" below); then
`FUN_004b3580` sets the corner: a member's status `FUN_00485ec0( FUN_005064f0() )` (groups 0 and 1 picture 0, 2 1, 3 2,
4 3, picked up 9, no part), a visitor's last thought less one, `0x29a` with none. The camera's mode change clears the bit
(`0x0042ae52`), and so does any pan of the camera; opening a park screen folds the arm, so a person's window
and the follow never run together (`park-engine.md`, "The follow"). The strip's left click opens the person's window (`FUN_004b6080`, `FUN_004b79b0`).

**The original** (`q420/orig/run.sh`, `q325/stock.TPWS` off-screen; predicted first, six of six, `q420/PREDICTION.txt`):
Ctrl+S read locator 1, role 0, nobody, bit clear, the panel on the arm (`orig/a-1-ctrl-s.png`); the mechanics' button,
role 2, a person of type 4, the bit set, the look-at (532.19, 212.05), his `+0x190`/`+0x194` times 10/65536 to the
hundredth, the strip "Mike Cooper" with his status, him mid-frame (`orig/a-2-mechanic.png`); again the same person;
Ctrl+S again, the locator 0 with the panel out, the button then nobody; Ctrl+V locator 2, role 6, its button a person of
type 1 whose `+0x1d8` read 13, "Visitor #13" with no thought (`orig/a-6-visitor.png`); Escape the locator 0, the arm in,
no menu. The advisor came up as the staff's opened (`0xc6`).

**OpenTPW** (Q424): `ParkLocator`, carried by `ParkGadget`'s arm (`CarryLocatorPanel`, `TravelFor`, `PlaceFor`,
`FoldLocator`), opened by `ParkFrontEnd`'s rows 13 and 14 and folded by Escape; `Staff.StateGroup`; the console's
`locator`. On the stock park (`q420/run-fix3`, the final game binary; seven of seven): Ctrl+S `locator: open=Staff
role=0 actor=0 following=0 strip=0 ... staffPanel=1@484,1127 down=none` (`1-ctrl-s.png`); the mechanics' button `role=2
actor=26 following=1 strip=1 name="Mike Cooper" status=0`, the camera on him to a fraction of a unit, paused
(`2-mechanic.png`), and 3 s on; Ctrl+S again `open=None` with the panel out, the button then `actor=0`, "with no locator
open" (`4-closed.png`); Ctrl+V `visitorPanel=1@485,1127`, its button `actor=42 name="Visitor #13" thought=666`
(`5-visitor.png`); Escape "Escape: the locator folded away", no menu (`6-escape.png`). Side by side with the original's
(`q420/side-by-side.png`) the panel, the buttons and the strip stand alike. Main's build counted `STAFF_LOCATOR` and
`VISITOR_LOCATOR` once each and put nothing out. The strip's click opens the person's window, a visitor's (Q430)
or a member's (Q431), and Ctrl+S or Ctrl+V with one up opens on that person ("The person's windows"). **Deviations, said at the site:** the
arm slides as the original's (Q427); a person gone from the
park is let go of, where the original keeps the freed thing's address. Not looked into: the bit `0x1000`'s other
readers. In no game run: a kind with nobody (tested only), a fenced member, a person leaving the park while followed
(tested only), the strip's hover and click, first person over a follow, Full Simulation (not asked: the locators are
either mode's), the other three themes.

**The advisor's two lines** (Q425). `FUN_004b3f60` posts its message after `FUN_004a2590` has folded what the arm
carried and before it closes the park screen: `0x11f` where it was given a visitor (role 6), else `0xc6`.
`FUN_004b4280` posts `0x11f` last, after the park screen is closed. The fold's `0x1001d8` withdraws `0x11f` and then
`0xc6` (`FUN_00486b40`), whichever locator it was, and the destroy (`0x14`) withdraws nothing. So Ctrl+S over its own
locator cuts its line and posts it again. Both rows are posted-only, one line, the Tutorial category, and have no tag
(group `0x17f`): `0xc6` is response 417, sample 389 ("those buttons on the control panel arm let you locate your
employees..."), and `0x11f` is response 547, sample 564 ("you can locate visitors by clicking the button on the control
panel arm..."). The original (`q425/orig/run.sh`, `q325/stock.TPWS`, `advisor.py` on both; five of five predicted,
`q425/PREDICTION.txt`): after Ctrl+S, `0xc6` took a slot on 863 and was said on 864, its line 0, for 19998 ms. Ctrl+V
3 s in left `0xc6` at said 0, slaps 1, and `0x11f` was said on 879. Ctrl+V again after the line took no slot (said
once). Said again on 972, cut to slaps 2 by a second Ctrl+S and said on 986, then cut to slaps 3 by Ctrl+V: the next
Ctrl+S took no slot. **OpenTPW** (`ParkLocator.OpenStaff`, `OpenVisitors`, `Folded`; `ParkAdvisorMessages.StaffLocator`,
`VisitorLocator`): on the stock park (`q425/run-fix6`, the final game binary): Ctrl+S `post message=198 ... tick=782
result=queued`, `attempt=1 message=198 ... response=417 sample=389` (`1-staff-line.png`). Ctrl+V 2 s in gave `withdraw
message=287 cleared=0 cut=0`, then `withdraw message=198 cleared=0 cut=1 slaps=1`, then `post message=287`, and sample 564
at once (`2-visitor-line.png`). After the line, Escape and Ctrl+V gave `result=said-once` (`3-said-once.png`). The third
cut gave `slaps=3`, then `post message=198 ... result=slapped` (`5-slapped.png`). Main's build counted
`LOCATOR_ADVISOR_LINE` once. Side by side, `q425/side-by-side.png`. Ctrl+V over the visitor's window ran in both games (Q430); Ctrl+S over a staff member's is in no game run.

**The patrol edges** (Q426). `FUN_004b3640`, with the bit set and the person of type 4 to 8, asks `FUN_00506eb0` whether
the member's patrol area is the whole map (`+0x20a` 1 and `+0x20c` `0x4000`: none drawn); otherwise it reads the two
cells (`FUN_00506e70`) and walks four lines of `FUN_00536100( 0x136, ... )`: the top row from the left corner's right
neighbour to the right corner's left (both corners where the area is one cell wide), the right column from the top
down, the bottom row as the top, and the left column from the bottom up. `FUN_00536100` walks the longer way, a tie
along Y, holding the other coordinate at the start's, and passes over an off-map cell (`FUN_004d8300`). So a row two
cells wide has no middle: its line runs from the right corner back to the left, and both corners are laid twice. Op
`0x136` is `0x100` (lay a square) with mode `0x36`, the patrol tool's own (`FUN_005234d0` draws the tool's rectangle
with the same four lines and latches its refusal). `FUN_00532fc0`'s `0x100` arm asks `FUN_00535670( 0x36 )`, which
answers the refusal latch `DAT_00816d50` and nothing else (`0x005356df`), and reads the placer's failure `DAT_0081b604`
before it (`0x0053319a`); both are left by a tool's own pass, so after a park load, or a pass that accepted, the square is
texture 0, blue, lifted over a built cell and walled as any tool's ("Placement feedback"). Every frame, since the marker
draw empties its list. **The latch outlives the tool**
(read 2026-10-19, Q532): the pass `FUN_005234d0` clears `d48`, `d50` and `d58` at its start (`0x005234ec`..`0x005234f8`)
and its verdicts set them; the setter `FUN_0052f200( 0 )` clears `DAT_0081b604` (`0x0052f38d`) and `d48` (`FUN_00527940( 0 )`, `0x0052f477`), never `d50`; installing a tool
(`FUN_0046c350` -> the shell's `FUN_0046d5a0`) runs the pass first, under the old mode (`0x0046d5e8`, before the setter at `0x0046d5f6`), so
arming any tool, the path and patrol tools, the move tool and a carried item (mode 4) clears `d50` at once; the click
`FUN_00524960` and the press `FUN_00524790` clear `d50` only under the replay (`[0x00785a2c]`); a park's start clears all
(`FUN_0052f050`). So a tool put away, by its refused click, Escape or a commit, leaves its last pass's `d50`, and the edges
are red until the next tool is installed or passes. The path verdict (mode 1) sets `d50` itself on a thing's cell (types 4, 9, 10, unless a
lower overwrite priority lets it bulldoze), track type `0x19`, a run dearer than the balance and a queue that is not a loose
end; it sets only `DAT_00816d58` on a type `0x15` cell or a track corner (`0x00535b9f`, `0x00535d57`), and every red
square is judged a second time (`0x00533927`, the verdict's third argument 1), whose copy of `d58` into `d50`
(`0x00535885`) latches it on the same square (Q538); land outside the park (flag `0x40`) sets neither, and answers before
the copy. The patrol tool's anchored pass sets it by the rectangle's test (`0x00523dd3`). The queue
arm (mode 3; Q538, read 2026-10-19), in its order: land outside the park red with no latch, a thing's cell `d50`
(`0x00535d95`), NOMODIFY or not (the verdict's early NOMODIFY answer, `0x005356aa`..`0x005356c5`, is for modes 4, 9
and 10), the copy of `d58`, an earlier latch red, track `0x19` `d50` (`0x005358c0`), the money `d50` (only bare ground
charged, `0x00535931`; tested only once something is owed, `0x0053590a`, so in a park in the red the anchor and the
join still pass), its own queue's middle and another ride's queue `d50` (`0x00535aae`, `0x00535acc`), type `0x15` `d58` (`0x00535ba4`), a track corner `d58` (`0x00535d57`), bare
ground blue (NOMODIFY unread), the last cell a path `m_link` with `d58` and `DAT_00816d60` (`0x00535c78`), a path in the
run blue or, NOMODIFY, `d58` (`0x00535c8a`), any other type red with no latch. So a queue pass leaves `d50` after any
refusal but land outside the park and a type it may not be laid over (the stock park's type 7 land,
flags 0), and leaves `d58` alone only after the join. The second verdict of a red square, in either mode, skips only
the earlier latch's return (`0x00535898`), so past land outside the park, a thing's cell and track `0x19` it charges the
cell again into the run's total `DAT_0081af4c` (`0x005358f4`) and tests the money once more: a red square after a latch
is paid for there, one the first call charged is paid for twice, and a run that comes to more than the balance sets `d5c`
(`0x0053593f`) and so `c_cash` where the squares alone would show `c_noplace` (Q542; `park-engine.md`, "The path tool"). Named in Ghidra: `BuildTool_PreviewPass` (`FUN_005234d0`), `BuildTool_CellVerdict` (`FUN_00535670`),
`BuildTool_SetMode` (`FUN_0052f200`), `g_BuildRefusalLatch` (`d50`), `g_BuildRefusalPending` (`d58`), `g_PlacerFailed`
(`DAT_0081b604`). **The original** (`q426/orig/run.sh`, `q325/stock.TPWS`, `original/squares.py`; four of four
predicted, `q426/PREDICTION.txt`): the mechanics' button, texture 0, 48 squares on the 48 cells of (39,21)-(56,28),
none twice, at height 1.50 (no lift), in the order above (`a-1-mechanic.png`); the scientists' (the whole map) none;
the entertainers' (47,18)-(48,25) 20 squares on 16 cells, (47,18), (47,25), (48,18) and (48,25) twice, in the same
order; Escape none. **OpenTPW** (`ParkLocator.PatrolEdgeCells`, queued each frame to `ParkBuildMarkers.QueuePatrolEdges`,
which lays them as the tools' blue squares and keeps none past its frame): on the stock park (`q426/run-fix2`, the final
game binary), the mechanics' button logged `Park locator: thing 26 patrols (39, 21)-(56, 28), its edges 48 squares a
frame`, `locator` ended `patrol=48`, and `strip`'s drawn census held `| patrol 48` in that order, each marker 0, lift 0
(`1-mechanic.png`, beside the original's in `q426/side-by-side.png`); the scientists' `patrol=0`; the entertainers'
20 on 16 cells, (48,18), (47,18), (48,18) first (`3-entertainer.png`); Escape none. Main's build counted
`LOCATOR_PATROL_EDGES`. **Red after a refused pass** (Q532): in the original (`q532/orig/run.sh`, the stock park, three of
three predicted, `q532/PREDICTION.txt`) (43,16), outside the park, clicked with the hand empty refused the path tool with
`d50` 0, and the mechanics' button laid "texture 0: 48 squares on 48 cells"; (49,24) anchored and the click on (51,24),
the Belly Bounce's cell, refused with `d50` 1 (`d48` 1 too while the red edges were drawn), and the 48 were texture 1
(`a-2-edges.png`); (49,24) anchored again and Escape left `d50` 0 and the 48 texture 0. **OpenTPW**
(`ParkBuildMode.RefusalLatched`, set by each frame's pass in `ParkBuildMarkers.LatchAfterFrame`; the path verdict's two
latches, `ParkPathBuilding.Latches`; the edges by `ParkBuildMarkers.PatrolSquares`): the same clicks on the final build
(`q532/run5`) gave the same three, `tool`'s `latch` 0, 1, 0 and `strip`'s `| patrol 48` markers all 0, all 1, all 0
(`q532/side-by-side.png` holds the first two states; the third, Escape and blue, is `q532/run5/3-edges.png` and
`q532/orig/a-3-edges.png`). **After a queue tool's pass** (Q538): in the original (`q538/orig/run.sh`, `q325/stock.TPWS`,
three of three predicted, `q538/PREDICTION.txt`) a click on the Belly Bounce's queue cell (50,22) edits the queue, mode
3 anchored on (49,22); the pointer on (50,22), the queue's middle, `d50` 1 (`a-1-hover.png`), Escape kept it and the 48
edges were texture 1; on (48,22), the join, `d50` 0 with `d58` 1 and `d60` 1, the edges texture 0; on (49,17), across
type 7 land, `d50` 0. **OpenTPW** (`ParkPathBuilding.QueueStrip` and its `Verdict`, the second verdict
`ParkPathBuilding.JudgedAgain`): the same clicks on the final build (`q538/run4`) gave `tool` mode 3 anchored at (49,22),
the strips `49,22,0;50,22,1` latch 1 (`1b-hover.png`), Escape latch 1 and `| patrol 48` markers all 1 (`1-edges.png`;
`q538/side-by-side.png`), `49,22,0;48,22,8` latch 0 and markers 0, and `(49,18)`, `(49,17)` red with latch 0. The second
verdict, console-driven (no player route on the stock park, so in no run of the original): the queue tool anchored on
the NOMODIFY avenue cell (48,17) with the pointer held on (48,16), flags `0x04c0`, drew `(48,17):1[is marked NOMODIFY]
(48,16):1[is outside the park]`, latch 1, and the edges red after Escape (`5b-hover.png`, `5-edges.png`).
**Under a wall** (Q540): the edges are drawn after the tool's pass, so where a blue edge crosses a lifted cell its walls zero `d50` and `d58` (`park-engine.md`, "The walls"; a red
edge's, from the refused arm at `0x00533927`, zero neither). An edge is blue only with `d50` already nought (mode `0x36`
answers by `d50` alone, `0x005356e4`), and only a queue's join leaves `d58` set then. In the original (`q540/orig/`, `q325/stock.TPWS`, `q540/PREDICTION.txt`
round 1): Mike Cooper's fence drawn with the patrol tool, (52,26)-(54,27), and the locator following him laid 11 records,
6 squares and 5 walls under (52,26) and (53,26), texture 0; the Belly Bounce's queue edited and the pointer on the join
(49,28), `d60` 1 and `d58` 0 with the edges drawn (`a-16-join28-edges.png`), 1 with the follow off (`setcam.py off`, no
edges, `a-17-join28-noedges.png`), 0 again on (no frame kept). The latch values were printed to the terminal by
`q540/orig/lib.sh`'s `words` and are kept only as `q540/PREDICTION.txt`'s round-1 prose (eight reads of eight, four of
four), not in a log; the frames and `.squares` are stored. The click there laid the same five cells for $375 either way (`a-19-*`, `b-1-*`), so nothing
reads that `d58`. **OpenTPW** keeps no `d58` past a pass, so it zeroes nothing: the same clicks on the final build
(`q540/run2`) gave "thing 26 patrols (52, 26)-(54, 27), its edges 6 squares a frame", with the queue tool still armed
`49,22..49,27` marker 0 and `49,28` marker 8, `| patrol 6` and `| walls 5` all 0, latch 0 (`3-join-edges.png`, beside the
original's in `q540/side-by-side.png`), and the click "queue: laid 5 from (49,22) and joined the path at (49,28)", five
withdrawals of 75; main's build counted `MARKER_WALL_CLEARS_REFUSAL_LATCH` 3 times there. Two clears are built from the listing alone (in no game run, 2026-10-19 review):
`ParkBuildMode.Arm` of a tool and taking an item into the hand (`ParkBuilding.Hold`, which the move tool also reaches)
clear the latch. Not built: between the press and the release the original runs mode-1 hover passes over the clicked
cell, so an empty-hand click on one unaffordable cell leaves `d50` 1 (the money arm, `0x00535931`) where ours leaves it 0;
and mode 4's passes (`0x005235bb`..) set `d50` and `d5c` each tick while the item's price is above the bank's balance
(`0x00523658`; the golden-ticket cost `+0xc4` unpaid sets neither, `0x005235e6`..`0x0052360b`) and the placer sets it on an
entrance or exit facing off the map (`0x005295e8`, `0x00529730`), where ours leaves a carried item's latch clear
(`CARRY_PASS_REFUSAL_LATCH`; a carried thing's own squares are built, Q633). In no game run: a refused queue or patrol click before the edges (a queue pass then Escape was, Q538), a track's refusal (no shipped
track reaches the path tool here; tested), Full Simulation (not
asked: the locators are either mode's), the other three themes.

#### The person's windows: the visitor's and the staff member's (Q429, Q430, Q431)

Read 2026-10-16 (Q429, decode only; nothing is built). Two of the object windows' nine (`park-engine.md`, "The
per-object management screen is nine screens"), both on the shared base `FUN_0048cea0( stream, handler, preview,
stats, 0 )`, each a park screen (`FUN_00485b70`: the arm folds, so a locator out is put away; the camera and game
tables go off). **Who opens them**: the world click on a guest or a member (`FUN_004879d0`, categories 7 and 8 to `c`),
the locator strip's left click (`FUN_004b3c10`, `0x10006` button 0: the visitor's for any type but 4 to 8), the
object dispatcher `FUN_00486920`, and a row's `0x400` in the visitors list (`0x0049351e`), the all-staff list
(`0x00496088`) and the staff room's (`0x004b5267`). The windows are statics: the visitor's at `0x007cc408` (its tree
`[0x007cc418]`, which Ctrl+V tests), vtable `0x006fff40`; the staff member's at `0x007cc3b0` (tree `[0x007cc3c0]`), vtable
`0x006ffe50`. The person is `[0x007c2658]`, set by `FUN_0048ca00`, which also counts the things of the window's class
(`FUN_00481bc0`; the visitor's static holds class -2, which `0x0048ca00`'s table at `0x0048caac` maps to mask `0x40`, the type-1 things, `0x00481e0b`; the staff member's holds -1, mask 1, the types 4 to 8, `FUN_00481f80`).

**The visitor's** (`FUN_004b79b0`; stream `0x007575e0`, handler `FUN_004b7720`). The builder posts advisor message
`0x12e` and plays `cat_kids` effect `0x81`; the destroy (`0x14`) withdraws `0x12e`. The stream (`lib/uistream.py`;
meshes by `meshhash.py`, help rows UIHELPTEXT), 2048 x 1536:

| Id | Rect | What |
|---|---|---|
| `0x371f` | (328,130)-(1720,901) | the root; `window1` in `w_small` |
| `0x3736` | (749,174)-(1221,253) | the title, "Visitor #{P24}" (UITEXT 456, `+0x1d8`) or "New Visitor" (457) at nought; not editable |
| `0x3722` | (456,267)-(720,531) | the preview, `!frame` |
| `0x3723` | (745,265)-(1521,709) | the stats, `!frame`: two columns of eight rows, the labels at x 778 and the figures at x 1170 |
| `0x3734`, `0x3735` | (641,720)-(1352,856), (676,739)-(780,819) | the thought: `Ethoughts3` in `ethoughts` with its text rect (817,760)-(1314,805), help 95; inside it a type-6 icon wearing `thoughts.MD2` |
| `0x3720`, `0x3721` | (1591,234), (1580,322) | `b_arup` (help 98, the previous visitor), `b_ardown` (97, the next) |
| `0x3737`, `0x3738` | (1568,411), (1568,518) | `b_locate` (99: `FUN_004b3f60( person )`, the visitors' locator on them), `b_kids` (100: the visitors list, `FUN_00493530`) |
| `-2` | (1591,714) | `b_okay` (1): the close |

The stats fill (vtable `+0xc`, `FUN_004b6c20`) letters the labels in font 6: UITEXT 70 Cash remaining, 71 Time in park,
72 Rides ridden, 75 Purchases made, 73 Sideshows played, 74 Sideshows won, 76 Happiness, 77 Current status. The figures
(the refresh, vtable `+0x14`, `FUN_004b72a0`, at the show and on the timer `0x80080` every 4,000 ms) are the visitors
list's cells ("The visitors rows", below): a money cell of `+0x1a0`; the time cell (`FUN_00490e60`) of **four times the
stay** (`FUN_004fd950` shifted left by two before `FUN_004f8730`, as the list's rewrite); numbers of `+0x1c4`, `+0x1c8`,
`+0x1cc` and `+0x1d0`; a `happygrad.wct` bar inset 10 of `((__ftol( +0x19c ) & 0xff) << 10) / 100`; and the status, row
`+0x220` of `KIDSTATES.str` (`[0x0078baa0]`, table `0x3ec`, `mUIKidStates`), row 16 "Using: {P0}" with the name of the
thing whose id is `+0x1dc` (`FUN_004e2780`). The thought is part `+0x30` less one, or 999 at nought (none shown), and row
`+0x30` of `THOUGHTS.str` (`[0x0078ba9c]`). The title is set at the show (vtable `+4`, `0x004b7660`) and again by the
refresh while the window's kept number `this[9]` is nought, so a new visitor's title turns to their number once given. The visitor's open (vtable `+0`, `0x004b75a0`) calls `FUN_0048ca00` and sets the thought panel `0x3734` up (font 6, its colours, shown) but never hides the arrows: `b_arup` and `b_ardown` stay up whatever the count, where the staff member's open hides them below two staff.
The preview (vtable `+0x18`, `0x004b6b90`) is `FUN_00486530`: a copy of the person's model (`FUN_00475cf0` on `+0xc`) set
in the panel's rect, a fourth word 12.0 kept at the copy's `+0xa4` (which `FUN_00486530` also uses to place the copy: the word times -0.25, `0x006ff160`, truncated and subtracted from the vertical term at `0x0048659e`..`0x004865ad`, so a quarter of it, 3 for the visitor's 12.0 and 2 for the staff's 8.0, is added before the 1/768 scale; the store is `0x00475d6a`), copied from the live model again
on every `0x1e` (`FUN_00486610`). Its click goes to the person and closes the window (the base's `0x0048d1a0`). The base's handler `FUN_0048d200`, which ends every object window's handler, also turns the key-down message `0x1000a` into arrow moves: cursor Up (`0x2600`) calls `FUN_0048cbe0`, `b_arup`'s, and cursor Down (`0x2800`) `FUN_0048caf0`, `b_ardown`'s (`0x0048d220`..`0x0048d243`). Whether a key reaches the window depends on the focus, which is not read; this game has no key branch in either person window and counts none (Q31b).

**The staff member's** (`FUN_004b6080`; stream `0x00756f48`, handler `FUN_004b5cb0`). The builder posts `0xc0` and
gives the preview `FUN_004b5c80` (the camera to the member, the window kept); the destroy withdraws `0xc0` and `0xd4`.

| Id | Rect | What |
|---|---|---|
| `0x50a` | (328,130)-(1720,901) | the root, `window1` |
| `0x520` | (749,174)-(1221,253) | the title, the member's name (`FUN_00506860`); help 85, a click to edit it (`FUN_0048d290`, vtable `+0x30` sets the name, `+0x34` reads it) |
| `0x511` | (386,280)-(650,544) | the preview |
| `0x512` | (665,280)-(1538,623) | the stats: six rows, labels at x 688, figures at x 1193 |
| `0x50c`, `0x50e`, `0x510` | (380,694), (539,694), (1401,694) | `b_fire` (89), `b_pick` (88), `b_route` (86, a toggle, flags `0x11`, second help 87) |
| `0x50d`, `0x50f` | (1591,234), (1580,322) | `b_arup` (92), `b_ardown` (91) |
| `0x521`, `0x50b` | (1568,411), (1568,518) | `b_locate` (93), `b_allstaff` (94: the all-staff list on the member's kind, `FUN_00496620( FUN_00506490(), member )`) |
| `-1` | (1591,714) | `b_okay` |

The labels (vtable `+0xc`, `FUN_004b53c0`): UITEXT 64 Employed for, 65 Monthly wage, 67 Skill, 69 Happiness, 68 Energy,
66 Current status. The refresh (`FUN_004b58b0`): "{P8} days" (UITEXT 443, the plural at any count) of `FUN_00505b70`, the
days since `mTimeHired` (`saves.md`); a money cell of the wage `FUN_00506870`; a `ridestatbar.wct` bar of the skill
`FUN_00506550` (`<< 10) / 100`); a `happygrad.wct` bar of `+0x1f8`; a `ridestatbar.wct` bar of `+0x1fc`, the rest (500
until the first refresh); the status picture `0x51f` (`staffstat`, `FUN_00485ec0`) and the `STAFFSTATES` word
(`FUN_00485f00`, `[0x0078baa4]`) of `FUN_005064f0`, its 1 read as 0 where the member has no patrol area
(`FUN_00506eb0`). The show (vtable `+4`, `FUN_004b5ab0`): the title, the preview (scale word 8.0), the refresh, the patrol
toggle down with advisor message `0xd4` posted where an area is set, and PICK UP enabled by `FUN_00505bd0` (again every
`0x1e`). The open (vtable `+0`, `0x004b5a60`) hides both arrows (`UI_SetVisible( 0 )`) with fewer than two staff. **The verbs**: FIRE is the
dismissal `FUN_00505790`, behind a message box on UITEXT `0x18d` when `[0x0078d90f]` is set (the box's yes,
`FUN_004b6070`, dismisses), then the window closes; PICK UP keeps the member (`[0x007c2650]`), picks them up
(`FUN_00505c50`) and closes; the patrol toggle down (`FUN_004b5b50( 1 )`) keeps the member, puts map tool `0x36` in the
hand, closes the window and posts `0xc1`; up clears the area (`FUN_00506e90`).

**The advisor's four lines** (the metadata table, `q177`'s `meta_full.json`, rows of 56 bytes from `0x0076e300`): `0x12e`,
`0xc0`, `0xc1` and `0xd4` are each posted-only, one line, the Tutorial category, no tag, responses 562, 411, 412 and
431, samples 580 (the visitor's screen and the buttons on its right edge), 384 (the employee's screen, its three verbs,
the name's edit and the buttons on the right edge), 385 (how to draw a patrol area) and 457 (how to cancel one), by
their transcripts in `global-speech-transcripts.tsv`.

**The original** (`q429/orig/run.sh`, `q325/stock.TPWS` off-screen; predicted first, twelve of twelve,
`q429/PREDICTION.txt`): Ctrl+V, the visitors' button, the strip: `[0x007cc418]` set, the locator put away, "Visitor
#13" ($ 684, "11 days" at tick ~917 and "12 days" after a refresh, where the stay alone reads "2 day"; four noughts; the
bar 0.567 for 56.0; "Lining up for a ride", row 11 of the reference install's `american` table), no thought, the
visitor's model in the preview (`orig/a-1-visitor.png`, `a-1b-visitor-4s.png`); `0x12e` said on 918 and cut by OK on 962.
Ctrl+S, the mechanics' button, the strip: "Mike Cooper", "45 days", "$ 161", bars 0.61, 0.841 (`+0x1f8` 84.97) and 0.652
(`+0x1fc` 65.3), "Patrolling" (`a-3b-staff-4s.png`); `0xd4` said first, on 1041, `0xc0` waiting behind it, both gone at
OK. Not run: a thought shown, the verbs, the arrows, the name's edit, a visitor's or member's window from the world click
or a list.

**Read for the build** (Q430). The open (vtable `+0`, `FUN_004b75a0`) keeps the person (`FUN_0048ca00`) and letters the
thought panel `0x3734` in font 6, black, centred both ways; its words are `FUN_006ace60( +0x30 )` through
`FUN_00485ac0`, row 0 (none) an empty row. **The arrows** walk mask `0x40`, kind byte 1: `b_arup` is `FUN_00482da0`,
the used-thing list's previous link (`FUN_005161b0`, the entry's `+0xc`) from the person, so towards the newest, and
with none round from the list's tail (`FUN_004831f0`), the oldest; `b_ardown` is `FUN_004824d0`, the next link, towards
the oldest, round from the head. **`b_kids`** hands the visitors list the person: `FUN_00664d2a` finds the row,
`FUN_00665739` picks it and, where it is out of view, makes it the top, held so the view ends at the last row; the
list's opener also posts advisor message `0x11e`. **The status's row 16** names the thing as `FUN_004e2780` joins its
two name lines (`+0x3c8`, `+0x40a`): the second alone where the first is empty, the first less its last character and
the second straight after where the first ends in `-` (`"%s%s"`, `0x00749850`), the first alone where the second is
empty, else `"%s %s"` (`0x0075c0c0`). Nothing clears `[0x007c2658]` as a guest goes (only the delete and move verbs,
`FUN_0048cd10`, `FUN_0048cfa0`, write nought), so what the window does then is not decoded.

**The original** again (`q430/orig/run.sh`, the stock park; `q430/PREDICTION.txt` round 1): the window on $684 #13;
`b_ardown` $510 (stamp 659), `b_arup` $684, again $570 stamp 648, the oldest (no guest made since the load);
`b_locate` and Ctrl+V over the window: the window gone, `[0x007cc2f0]` 2 on the guest, role 6, `0x11f` posted; `b_kids`:
the list up with #12 (row 11 of 13) brought in as row 3's top, #4 (`a-7-kids.png`; the lit row the pointer's, flag
`0x80`), `0x11e` posted; its first row's click: the window on #4 ($570, stamp 655), the list gone; the preview's click:
the window gone. `0x12e` was posted and said once (916/917); the later three opens put none in a slot, and neither did this game's, by said-once (`q430/run-fix2/run.log`, `result=said-once`, the accept gate); the original's own cause is not read.

**OpenTPW** (Q430): `ParkVisitorWindow`, opened by the strip's click on a visitor and a visitors row's `0x400`; Ctrl+V
with it up and its `b_locate` are `ParkLocator.OpenOnVisitor`; `b_kids` is `UiList.Pick`. Counted: the preview's model
(`VISITOR_WINDOW_PREVIEW`), a guest gone while it is up (`VISITOR_WINDOW_GUEST_GONE`, the window closed), the list's
`0x11e` (`VISITORS_LIST_ADVISOR_LINE`). A guest's world click reaches nothing (the pick finds only a cell's
occupant). Its run (`q430/confirm.py`, round 2) walked
the same guests as the original's: 42, 41, 42, 29, the list's top row 3 and its first row Visitor #4 ($570).

**Read for the staff member's build** (Q431). The stats' rows, label then figure (`FUN_004b53c0`, the refresh
`FUN_004b58b0`): `0x51a` 64 then `0x51b` the days; `0x51d` 65 then `0x51e` a money cell; `0x519` 67 then bar `0x514`
(`ridestatbar.wct`); `0x518` 69 then bar `0x513` (`happygrad.wct`); `0x51c` 68 then bar `0x515` (`ridestatbar.wct`);
`0x516` 66 then the picture `0x51f` (1130,553), type 6 in `i_staffstat`, part `FUN_00485ec0( FUN_005064f0 )` (0 and 1
to 0, 2 to 1, 3 to 2, 4 to 3, else 9) and the word `0x517` (1193,553). The labels at x 688 to 1085, the figures at 1193
to 1520, each bar inset 10 (`FUN_00485e30`). `FUN_00505b70` is `FUN_004c90d0`'s divide on `+0x1f0`: whole days of the
park's calendar. **The arrows** walk mask 1 (`FUN_0048ca00`'s class -1): `FUN_00482da0`'s case 1 takes the
used-thing list's previous link from the member, or from the list's tail, and keeps the first whose kind byte is 4 to
8 (`FUN_00481f80`); `FUN_004824d0` the other way. The open counts the staff the same way. **PICK UP** is enabled by
`FUN_00505bd0`: never in states 4, 5, 7, `0xd` or `0x12`, else where the cell the member stands in (bytes `+5`, `+7`)
has `mType` 0, 1, 3, 9 or 10 (`FUN_004fa990`). **`b_allstaff`** is `FUN_00496620( kind, member )`, two arguments: the
list on that kind with the member's row picked and brought into view (`FUN_00664d2a`, `FUN_00665739`), as the
visitor's `b_kids`; a kind of -1 is the one last shown. That opener's last act posts advisor message `0x11d`
(`0x00496b9e`). The preview's handler (`FUN_004b5c80`) takes only `0x10006`: `FUN_0048ce80`, the camera to the
member, and the window stays. `FUN_004b3f60( member )` sets the locator 1 first, then the arm, then the role and
button by the type byte, shows the status, hides the thought and posts `0xc6`.

**The original** (`q431/orig/run.sh`, the stock park; `q431/PREDICTION.txt` round 1): the window on Mike Cooper, "39
days" on 2.10.2000, "$ 161", "Patrolling", both arrows, PICK UP lit (`a-1-window.png`); `b_ardown` the handyman (25),
`b_arup` back, again the entertainer (27); `b_locate` and Ctrl+S over the window: the window gone, `[0x007cc2f0]` 1,
role 4 on him; `b_allstaff`: the entertainers' tab, "Pierre Hintze" lit (`a-7-allstaff.png`), `0x11d` posted; its
row: the window on him; the preview: the window kept. **The advisor's gate decides what is said, not the window**:
`0xd4` went in once (916), was said to its end and was refused at every later show (the Tutorial group's
`SayOnlyOnce` 1); `0xc0` was said on 972, 1033 and 1080, each cut by the close, and refused at the fourth build
(`DiscardAfterSlaps` 3).

**OpenTPW** (Q431): `ParkStaffWindow`, opened by the strip's click on a member, a member's world click and an
all-staff row's `0x400`; Ctrl+S with it up and its `b_locate` are `ParkLocator.OpenOnMember`; `b_allstaff` opens
`ParkStaffScreen` on the kind with the row picked. Counted: the preview's model (`STAFF_WINDOW_PREVIEW`), the name's edit (`STAFF_WINDOW_NAME_EDIT`), a member gone
while it is up (`STAFF_WINDOW_MEMBER_GONE`, the window closed), the all-staff list's `0x11d` (`STAFF_LIST_ADVISOR_LINE`).
The switch's second help row, 87, is not shown. Its run (`q431/confirm.py`, run1) walked 26, 25, 26, 27 as the
original's, "47 days" on tick 1109.

**FIRE and PICK UP, read for the build** (Q432; `FUN_004b5cb0`'s `0x50c` and `0x50e`, read as disassembly). FIRE, with
the Confirmations option `[0x0078d90f]` set, opens `MessageBox_Open( &text, 2, 0, 1, FUN_004b6070, 0, 0, 0 )`: UITEXT
`0x18d` (397, "DISMISS EMPLOYEE" and "Are you sure you want to dismiss this employee ?") white in font 4, the cross in
the bottom place with no act and the tick in the middle place with `FUN_004b6070`. The text record's fourth word is
the member as the box opened (`[0x007c2658]` at `0x004b5e96`); the box's handler calls an act with its kept word 3
(`FUN_0047eed0`), which `FUN_004b6070` hands in ECX to the dismissal `FUN_00505790`. With the option
off, `FUN_00505790` at once. Either way the window's vtable `+0x2c` closes it next (`0x004b5ecc`), behind the box. The
dismissal first installs the idle mode where the current one is type 6 (`0x00505830`: a carried worker is put back,
"The worker (type 6)" in `park-engine.md`), takes one month's wage by kind and grade from the bank and adds it to the
analyser's staff costs (`+0x1f7f0`), puts particle `0x13` at the member (`FUN_004fa470`: the model's place plus the
map's offset, the ground's height there), and deletes the thing (`FUN_0050b780`). PICK UP keeps the member
(`FUN_0048c950`, `[0x007c2650]`), picks them up (`FUN_00505c50`, ECX the member) and closes the window.

**The original** (`q432/orig/run.sh`, the stock park; `q432/PREDICTION.txt` rounds 1 and 1b): the option read 1. PICK UP
on Mike Cooper: the window gone, `+0x19c` 7, the hand's word (`mStaffHQ`'s `+0x60`) 26, `[0x007c2650]` his address, a
new mode object, and a figure of him hanging at the pointer (`orig/a-2-pickup.png`); a click in the middle of the view
put him down at (47,24), state 1 (the mechanic's search), the hand's word 0, the bank unmoved. FIRE: the window gone and
the box up (`orig/b-5-fire-box.png`); the cross kept him; FIRE again and the tick: the bank 88122 to 87961 and the staff
costs 538 to 699 in the same 1.2 s, exactly 161 (his wage, 7 x 23), his `+3` 1; the mechanics' locator then named nobody.

**OpenTPW** (Q432): FIRE opens `MessageBox` on UITEXT 397 where `GameOptions.Confirmations` is on, its tick
`ParkPeople.Fire` on the member kept; PICK UP is `ParkPeople.PickUp`; each closes the window. `ParkPeople.Fire` puts a
carried worker back first. A member in the hand is not drawn in the park (`ParkGuestSprites.InTheHand`; the `guests`
census says "IN THE HAND, not drawn"; that the draw loop skips him is shown by the census predicate only, not on screen). Counted: the puff (`STAFF_DISMISSAL_PARTICLE`) and the figure at the pointer
(`STAFF_PICKED_UP_PREVIEW`), so our pointer stays the plain arrow while a worker is carried. Its run (`q432/confirm.py`)
logged one month's wage of 161 paid and put 161 on the staff costs across the tick (the balance moved -86, with income between the reads), the same box (`q432/side-by-side-box.png`).

**The patrol switch and the patrol tool, read** (Q433). The switch `0x510` calls `FUN_004b5b50` with its new state. Up
(0): `FUN_00506e90` on the member, the whole map (`+0x20a` 1, `+0x20c` `0x4000`), and the window stays. Down (1): the
member kept (`FUN_0048c950( [0x007c2658] )`, into `[0x007c2650]`), the build-tool shell `FUN_0046c580( 0x36 )` installed
(its install `FUN_0046d5a0` runs the setter `FUN_0052f200( 0x36, 1 )`), the window closed (vtable `+0x2c`), then advisor
message `0xc1` posted. **The setter withdraws ten messages on any change of mode** (`0x0052f245`..`0x0052f326`):
`0xb8`, `0xcb`, `0xd6`, `0xcd`, `0xc9`, `0xce`, `0xc1`, `0x159`, `0x15a` and `0x151`, so `0xc1` is cut as the tool is put
away, however it is. **The preview** (`FUN_005234d0`'s `0x36` arm) sets the run's price to -9999 (`FUN_0052fb90`), then
unanchored lays one square under the pointer and anchored the rectangle from the anchor to the pointer, its least and
greatest corners, in the four lines of "The patrol edges" above, after setting the refusal latch `DAT_00816d50` by
`FUN_00532a50`: **refused where every cell of the rectangle on the map is outside the park** (`mFlags & 0x40`) **or
answers track type `0x19`** (its own track record, `FUN_004d0af0`, or its parent's for types 12 and 17,
`FUN_0053ad90`), so a single cell of the park lets it through. The verdict `FUN_00535670( 0x36 )` is the latch alone
(`0x005356df`), so the outline is all blue or all red, and the anchored one-cell rectangle on land outside the park
is red already. **The patrol pointer** (read 2026-10-19, Q534): the cursor (`FUN_0052f950`) has no case for `0x36`; it
sets cursor 8, `c_noplace`, on a frame whose pass left `DAT_00816d48` (`FUN_00527950` answers it); `FUN_0052f950` and the
setter (`BuildTool_SetMode`) set no cursor for `0x36`, and `FUN_004981d0` writes the same control's cursor through
`FUN_004a2aa0` (not looked at with the tool armed), so once refused the pointer stays `c_noplace` over a blue outline
until the tool is put away. A re-arm through the staff window sets none (`FUN_004b5b50` -> `FUN_0046c350` ->
`FUN_0046d5a0`; the setter's cursor calls are for modes 3 and 1 only, and the cursor is the view control's own,
`FUN_0065f11d` +0x50); it has not been watched in the original's running game (Q537).
The original (`q438/orig/b-run.log`, `b-7-outline.png`): (54,17) anchored with `d48` 1, the pointer on (57,16), `d48`
0, the outline texture 0 and the no-entry pointer over it. **OpenTPW** (`ParkPatrolTool.PointerRefused`, set by
`Level.PatrolCursor`, cleared by `ParkBuildMode.Arm`): the final build (`q534/run4`, the stock park, five of five
predicted, `q534/PREDICTION.txt`) armed showed `pointer: cursor Normal`; (43,16), outside the park, anchored, `NoGo`
and the square red; the pointer on (43,21), the 14 squares blue, `latch 0` and still `NoGo`, 1.5 s on too
(`3-outline.png`, beside the original's in `q534/side-by-side.png`); Escape, `cursor Path`. **The click** (`FUN_00524960`, on the release; the press `FUN_00524790` only sets `DAT_008186d8`, which the refusal's sound reads):
refused (`DAT_00816d48`), sound `0xaf` and the tool put away, nothing written (the sound is the commit's own, ahead of its
mode switch, so every carry-shell mode's but `0x38`: `FUN_0051bfc0( 0, cat_ui, 0xaf, x * 10, 0, y * 10 )` at `0x00524aa7`, where
no layout replay runs, `[0x00785a2c]` nought, and the press reached the mode's down slot, `DAT_008186d8`, which
`FUN_00524790` sets on every press: a press beside an open park screen never calls it; effect 175 is one variation at
volume 35 of `blnl_error1`, `sfUiHD.sdt` entry 0, and every `cat_ui` effect is flagged `0x200`, so it sounds flat); unanchored, the anchor set and the kept
member read again (`thunk_FUN_0048c920` into `DAT_008186b0`); anchored, where a member is kept, `FUN_00506e50` writes
the least corner's cell id to `+0x20a` and the greatest's to `+0x20c`, then the anchor is cleared and the tool put
away (`FUN_0052f200( 0, 0 )`). Neither click plays a sound.

**The original** (`q433/orig/run.sh` and `run2.sh`, the stock park; `q433/PREDICTION.txt` rounds 1 and 2): Mike Cooper's
switch drawn down (footprints crossed in red) on (39,21)-(56,28), 2728 and 3641; up: 1 and 16384, the window up, the
footprints alone and "Idle" at the next refresh (`orig/a-2-switch-up.png`); down: the window gone, `[0x007c2650]` him,
mode `0x36`, `0xc1` posted and said, `0xd4` cut by the close, one blue square at (51,27) and the plain arrow; a click
there anchored (51,27); the pointer at (54,26): 8 cells in texture 0, (52,26), (53,26), (54,26), (54,27), (52,27),
(53,27), (51,27), (51,26), row 26 lifted over the built cells (`orig/a-5-outline.png`); the click: 3380 and 3511,
(51,26)-(54,27), mode 0, the anchor -1, `0xc1` cut (slaps 1); the window again down and "Patrolling". On the band
outside the park, (45,15) anchored with the latch already 1 and (53,15) gave 16 squares on 9 cells, every one
texture 1, and the no-entry pointer (`orig/b-4-outline.png`); the click put the tool away and wrote nothing.

**OpenTPW** (Q433): the switch is `ParkStaffWindow.Patrol`; the tool is build mode `0x36`, `ParkBuildMode.Patrol` with the
member as its `Serves`, and `ParkPatrolTool` (its `Strip`, `Refuses` and `Run`) lays the preview through
`ParkPathBuilding.Strip` and takes the world click; `Staff.SetPatrolArea` and `ClearPatrolArea` write the two corners.
`ParkBuildMode.Arm` withdraws `0xc1` on a change of mode, the only one of the setter's ten posted here; the cursor is the
plain pointer, `NoGo` while the outline is red. The refused click plays `0xaf` (`UiSounds.Refused`, Q439) unless a park
screen is open, where the press went to the screen; `ParkBuildMode.RefusedSound` is that one call, which a refused
placement, path run and queue run make too (Q442).
Its run (`q433/confirm.py`, run4, the final build; real clicks, each cell found by moving the pointer and reading `strip`'s drawn census, now `lib/cellprobe.py`): the same switch
words, the same 8 squares in the same order for (51,27) and (54,26), row 26 lifted 20 on (51,26) and (52,26) and 10 on
(53,26) as the original's (tops 21.5, 21.5 and 11.5 over 1.5), the plain pointer, "patrols (51, 26)-(54, 27), cells 3380 to 3511", the window again "Patrolling"; on the band
(45,15) four red squares on the anchored cell (each line lays it), 16 red at (53,15), `NoGo`, nothing written
(`q433/side-by-side.png`). **At one camera** (Q435) the lift is the original's: its camera after Mike's window
closed, read by `original/camera.py` (point (484.442, 259.319), zoom 110, rotation 0), given here as `camera 484.442
259.319 110 0`, puts the eye at the same (484.44, 93.62, 201.57) in both games, so the frames differed by the lens alone
(Q260; one lens since Q441); our frame's middle 480 x 360 scaled by 768/360 (the 90 degree lens of then; since Q441 the middle 960 x 720 by 768/720) lays our lifted tops of (51,26) and (52,26), y 284 to 314 here,
on the original's (`q435/side-by-side.png`), and the cells the original's camera put at two pixels were the cells under
the same pixels here at the first probe. Q433's "far higher" was its two cameras (ours zoom 60, pitch 45; the
original's 110, 58.3) and the lens. The walls a lifted square drops to a lower neighbour are drawn in both (Q436;
`park-engine.md`, "Placement feedback", "The walls"): the same nine records, face for face, under the outline's tops of
(51,26) to (53,26) (`q436/side-by-side.png`).
Escape with the tool armed and anchored, in the original, puts it away and writes nothing (mode 0, the anchor -1, the
area 1 and 16384; `q435/orig/a-run.log`), the outline and the advisor gone from the next frame and no menu up
(`q435/orig/a-6-escaped.png`): the game table's row 0 installs the idle mode (`0x0040c368`), whose setter cuts `0xc1`.
**Here the same** (Q437; `ParkFrontEnd.MenuKey` to `ParkHand.LetGo`): unanchored and anchored on (51,27), each Escape
logged `withdraw message=193 cleared=0 cut=1` then `Escape: the build tool is put away`, mode 0 not anchored, Mike's area
still the whole map, (0, 0)-(127, 127), no square or wall drawn and no menu; the next Escape opened the game menu (`q437/run2`,
`q437/side-by-side.png`). **A quick right click with the tool armed** (RMB cancel on, `[0x0078d911]` 1) does the same in
both games (Q438): the armed release installs the idle mode with no type test (`0x0048842b`; `park-engine.md`, "The hand's
ways out"). The original's (`q438/orig/run.sh`, the right button held 0.08 s): unanchored and anchored on (54,17), mode
0x36 to 0, the anchor -1, Mike's area 1 and 16384, `0xc1` cut (slaps 1, then 2), no marker square left and no menu; the
camera unmoved across the click; the pointer the idle one over the ground, with the help bar "Left-click to build path"
(`orig/a-4-rclick.png`, `orig/b-8-rclick.png`). Ours (`Level.RightButton` to `Level.QuickRightClick` to `ParkHand.LetGo`;
`q438/run1`), unanchored and anchored on (51,27): `withdraw message=193 cleared=0 cut=1` then `world click: right click - the
build tool is put away`, mode 0 not anchored, Mike's area the whole map, `strip` none, the camera unmoved, `pointer`
"cursor Path, help row 441", no menu; a second quick click, over the idle mode, logs nothing (`q438/side-by-side.png`).
A member fenced where one path cell lies stays on it, rolling it again and standing
eleven sweeps with thought `0x16` on one roll in about 55, in both games (Q434; `ride-operation.md`, "A member fenced
where one path cell lies").
**The refused click's sound** (Q439; `q439/confirm.py`, `run6`, the final build; the game's own mix to a file, `bus music
0`): on the stock park, (45,15) anchored with nothing heard (`blnl_error1` matched at 0.000), the click on (53,15) logged
`... is put away - sound 0xaf, a voice` and the mix matched `blnl_error1` once, at 0.999 and a gain of 0.1755: its 0.35
times the master's 0.5 times the effects group's 1, the advisor quiet (`duck=1.00`); the red outline and the no-entry
pointer before it, neither after (`q439/side-by-side.png`). In the original (`q439/orig/run.sh`, round 1j, its music on)
(48,16) anchored with `DAT_00816d48` already 1, the outline red to (50,16), and the click put the tool away (mode 0, the
anchor -1, Mike's area 1 and 16384); `blnl_error1` matched its own mix at 0.318 there, the best of the capture's 111
windows (the next 0.281), so the sound is suggested and not shown: the music masks a short clip, and the run with it
switched off was not made.
**At the other refusals** (Q442; `q442/confirm.py`, `run3`, the final build, the same mix): with the hand empty a click on
(43,16), outside the park, armed the path tool and logged `path: nothing laid - (43,16) is outside the park, so the path
tool is put away - sound 0xaf, a voice`; the queue tool handed back on the Belly Bounce's queue and run to (55,22), red
from (50,22), `queue: nothing laid - (50,22) is the middle of this ride's queue ... - sound 0xaf, a voice`; the Belly
Bounce in the hand on (43,16), refused and still held, `... - sound 0xaf, a voice`: each matched `blnl_error1` once at
1.000 and a gain of 0.1750. The same placement beside the hire screen logged `- no sound, the press went to a park
screen`, and the mix held none (0.017). **In the original, its Music switch off** (`q442/orig/run.sh`, round 1f): the
press on (43,16) with the hand empty installed the path tool red (mode 1, `DAT_00816d48` 1, the preview's cell (43,16));
the release put it away, and `blnl_error1` matched its mix at 0.558 there, against 0.077 in the quiet seconds before and
0.278 at best anywhere else in the capture: shown (`q442/side-by-side.png`).

#### How allstaff and allitems keep their rows current

Read and measured 2026-10-14 (Q365). Both screens rewrite their rows in place every two seconds on timer `0x80083`,
as allpeeps does ("How allpeeps keeps itself current"), and nothing else between a fill and the next:

- **allstaff.** The opener arms the timer once (`FUN_0065ef90( 0x80083, 2000 )`, `0x00496b84`); a tab does not. On it
  the screen's callback (`FUN_00495da0`, message `0x10`) walks the rows from the last to the first and hands
  `FUN_006644ea` each member's status, wage, skill and happiness with the column mask **`0x1a`**: the status, the skill
  and the happiness are written, the name and the wage stay as the fill wrote them. `FUN_006644ea` writes the masked
  cells and nothing else, so a row's colour and place stand.
- **allitems.** Each tab loads its own list tree, whose handler `FUN_004955e0` arms the timer as the tree is shown
  (message `0x15`, `0x0049560c`): **a tab starts the timer again**. On it, by the remembered tab (`[0x007ca154]`, which each
  sub-builder writes), the rides' and shops' rows are rewritten under mask **`0x1e`** and the sideshows' under
  **`0x3e`**: every column but the name. The miscellaneous tab has no case and is never rewritten.
- **The meters** are set by the opener (both) and by a tab (the kind's, `FUN_004964d0`) and by nothing else.

Seen in the original: between `q365/orig/a-shops` and `a-shops2`, nine seconds with no fill, the Drinks Shop's row
went from 1 customer, $10 and $20 to 4, $40 and $50.

**OpenTPW** (Q365): `ParkStaffScreen.Update` and `ParkItemsScreen.Update` are the two callbacks, on a `UiTimer` of
2000 ms of real time: the staff one stamped at the opening, the items one again at each tab; each logs `Staff list:
kind K, N rows rewritten, meters K/A, row 0 ...` or `Items list: tab T, N rows rewritten, row 0 ...`. The staff
meters are set at the opening and a tab, where they were set every frame. On `q353/grades.TPWS` (`q365/run-fix2`, the
final game binary; frames `run-fix2/four.png`): ten rewrites of the one mechanic in the 21 s after the opening, his
happiness 85 then 84 on screen and in the line, his wage 92 and the meters 880/895 in all ten, his status and
happiness the `staff` census's; three rewrites of the Drinks Shop in the 7 s after the shops' tab, none on the
miscellaneous tab. Main's build (`run-control2`) wrote none. Since Q367 and Q368 the rewrite moves every column below.

#### The all-items rows: a number, then bars

Read 2026-10-14 (Q367). Each tab's sub-builder hands `FUN_006639cb` a row factory and a styler, which make every row's
cells by the remembered tab: rides (`FUN_004944d0` and `FUN_00494690`) a name label, a number painter
(`FUN_0048fde0`) and three type-9 bars; shops a number, two money cells (`FUN_00490250`) and a bar; sideshows a number,
a bar, two money cells and a bar; miscellaneous a number. Every bar wears `ridestatbar.wct` (`FUN_00477ff0`, "The stat
bars' skin") and is inset 10 units from its row's top and bottom (`FUN_00485e30( cell, 0, 10 )`). The fill's adder
(`FUN_00493cd0`, through `FUN_00481bc0( 0x80, ... )`) and the rewrite (`FUN_004955e0`) hand the same values:

| Column | Rides | Shops | Sideshows |
|---|---|---|---|
| 1 | customers | customers | customers |
| 2 | excitement bar | last month's profit `$` | excitement bar |
| 3 | State of repair bar | total profit `$` | last month's profit `$` |
| 4 | remaining life bar | satisfaction bar | total profit `$` |
| 5 | | | satisfaction bar |

The customers are the ring `+0x1a8`'s last min( filled, 30 ) finished days added (`FUN_00495c20`, `PUSH 0x1e` at
`0x004956ca`), `__ftol`'d, a number. The excitement is `FUN_004e0860( 0 )` (`ParkRideScore.ExcitementOf`), the State
of repair `+0x44` (`0x00495715`), the remaining life `+0x48` (`FUN_004dd6d0`), last month's profit `FUN_004e1c70`,
the total `FUN_004e1c60` and the satisfaction `FUN_004e1e30`, read 2026-10-14 (Q368) on the object's day rings
(`ride-operation.md`, "The object's six day rings": each `mTemp`, `mData[30]`, `mCurrentEntry`, `mNumEntries`,
`mWrappedAround`):

- **Last month's profit** (`FUN_004e1c70`): the takings ring's (`+0x70`, days at `+0x74`) last min( filled, 30 )
  finished days added, less the costs ring's (`+0xf8`, days at `+0xfc`), each day loaded as an unsigned 32-bit figure
  (`FILD qword` with a nought high half, `0x004e1d21`), the difference `__ftol`'d (`0x004e1de9`). Today's is not among them.
- **The total** (`FUN_004e1c60`): `mTotalTakings` `+0x180` less `mTotalCosts` `+0x184`, a 32-bit `SUB`.
- **The satisfaction** (`FUN_004e1e30`): 50 plus the satisfaction ring's (`+0x340`, days at `+0x344`) last min( filled,
  30 ) days added as signed figures (`FIADD`), over their count (`FDIVR`), `__ftol`'d toward nought (`0x004e1ef1`); 50 when
  the ring holds no finished day (`0x004e1f06`). Its bar is the bar rule below.
- **A money cell** (`FUN_00490250`, vtable `0x006ff3b0`, its setter slot 3 `0x004903e0`) letters UITEXT `0x1c1` ("$ ")
  and the figure, or under nought `0x1c2` ("-$ ") and the figure without its sign (`CDQ`, `XOR`, `SUB`), through
  `FUN_006acd60` on the table `[0x0078ba94]` with one `VARM` variable; the variable's type word (`0xd`, `0x13`) names the
  placeholder, and its writer (`0x006add40`) prints the value `"%d"` (`0x0074f9d8`, wide).

Measured in the original under Proton (`q368/orig/a`, `q368/money.TPWS`: `mkscene.py` wrote the Drinks Shop's and Jungle
Spray's days and totals into `q353/grades.TPWS`; `original/money.py` polling): at the load's tick 963 the Drinks Shop
165, 3766, 53; the Jungle Spray -120, -600, 48; the Strength Bird, its rings empty, 0, 0, 50. Its frames letter "$ 165",
"$ 3766", "-$ 100", "-$ 580", "$ 10" and "$ 10" (a sale each by then, as the poll read), right-aligned, and its
satisfaction bars measure 0.526, 0.491 and 0.509 of their width (542, 491 and 512 of 1024).

The excitement, repair and life bars are `((value & 0xff) << 10) / 100` of 1024 (the excitement an int, the others a
float truncated first, `__ftol`), so a value past 255 shows its low byte. **The satisfaction bar is not masked**
(`0x00494031`, `0x00494199`, `0x00495888`, `0x00495959`: `FUN_004e1e30`, then the shift, no `and 0xff`); the bar's
setter `FUN_0066d885` holds it to 0..0x400, so below nought it is empty and from 100 up full (ours masks it, Q457). Measured in the original on `q353/grades.TPWS` (`q367/orig/a-*.png`, `object.py` on thing 13): the Belly
Bounce's row "5" with bars of 0.403, 0.925 and 0.993 of their width, its repair 92.32 and life 99.85 at the frame
(942 and 1013 of 1024); the sideshows' excitement bars 0.308 (Jungle Spray, 30) and 0.28 (Strength Bird, 27).

**OpenTPW** (Q367): `ParkItemsScreen.NumbersOf` is the row by column, `UiList.BarColumns` the bar cells (a
`ParkObjectWindow.UiStatBar` inset `BarInset`), made again at each tab since rides and shops share a tree here; each
fill logs `Items list: tab T, row N <name>: <columns>`, `-` for a column not worked out. On the same file, paused at
the load (`q367/run-fix4`, the final game binary): `row 13 Belly Bounce: 3 409 962 1013` beside `rings 13` last30 3,
`spend` excitement 40 and the ride window's repair 94.375 and life 99.887505; the bars 0.40, 0.944 and 1.0 of their
width; the Drinks Shop `1 - - -` with its satisfaction cell black; the sideshows `0 276 - - -` and `0 307 - - -`
(bars 0.27, 0.30). Main's build (`run-control1`) left the customers and excitement blank and lettered 94 and 99.
**OpenTPW** (Q368): `ParkObjectRings.ProfitOfLastDays` and `SatisfactionOfLastDays`, `ParkItemsScreen.Money` the
cell's text; `rings` now prints the total takings beside the total costs. On `q368/money.TPWS`, paused at the load
(`q368/run-fix2`, the final game binary): `row 16 Drinks Shop: 1 165 3766 542`, `row 43 Strength Bird: 0 276 0 0 512`,
`row 14 Jungle Spray: 0 307 -120 -600 491`, the frames lettering "$ 165", "$ 3766", "$ 0", "-$ 120", "-$ 600" and the
bars 0.532, 0.505, 0.485 (`q368/side-by-side.png`); main's build (`run-control2`) lettered nothing and counted
`ALL_ITEMS_MONTHLY_HISTORY`, which is gone. The original's sideshow rows stand Jungle Spray then Strength Bird by its
sort, the name rising (Q374, below). The rows' font and height are the factory's (Q377, "The rows' font and
height"); the headings are framed (Q370, "The list headings' frames").

#### The miscellaneous tab: a row an object, and each tab's sort

Read 2026-10-15 (Q374). The miscellaneous sub-builder `FUN_00495110` fills through `FUN_00481bc0( 0x400, 0x004941e0 )`:
bit `0x400` is `FUN_00482040( 3, adder )`, which walks every thing of type 3 whose `FUN_004e2710` word is nought and whose
item's `Info.WhichUIType` (`+0x4ac`) is 3, the rides' walk (`0x80`) with the kind changed. **The adder `0x004941e0` makes
a row an object**: the cells are the object's name (`FUN_00486280`, the name the other tabs' adder `FUN_00493cd0`
letters) and its item's standing count, the control record's `+0x18` (`FUN_004ca8c0` over `FUN_004d3d80`, keyed by the
thing's item word `+0xe`); the row's data is the thing (`FUN_0066403b( cells, thing, -1, 0 )`). So two Small Toilets are
two rows, each "3" where three stand. The list's right click (`0x00495554`) has no case by tab: a miscellaneous row
goes to its object as a ride's does.

**Each tab keeps its own sort**, as the hire, buy and visitors screens keep theirs ("A list's order"): the sub-builders
hand `FUN_006659af( word, 0 )` the words `[0x00750d5c]` (rides), `[0x00750d60]` (shops), `[0x00750d64]` (sideshows) and
`[0x00750d68]` (miscellaneous), each 1 in the file, the name rising; the screen's handler `FUN_00495360` stores `0x406`'s
value in the word of the remembered tab (`[0x007ca154]`, the jump table at `0x004955c8` from `0x004954f9`). Every
sub-builder makes column 0 text (`FUN_006636b2( 0, 0 )`).

Measured in the original under Proton (`q374/orig/a`, `q353/grades.TPWS`): the miscellaneous tab lettered Litter Bin 1,
Round Fountain 1, Security Camera 2 twice, Small Toilet 3 three times and Staff Room 1, the first row picked; a click on
Name stood them the other way, Staff Room first; the rides' tab and back kept that; the sideshows' tab stood Jungle Spray
above Strength Bird; a right click on the third row shut the screen with the view on a security camera.

**OpenTPW** (Q374): `ParkItemsScreen.Show`'s miscellaneous arm adds a row a placed object, keyed by its thing, with
`ParkState.BuiltOf( item ).Standing`; `_sorts` holds the four words, handed over as each tab fills, and the lists' column
0 is text. Each fill and heading click logs `Items order: tab T, sort W, N rows: ...`. On the stock park with a Giant
Puzzle and a Balloon Shop bought under pause (`q374/run-fix3`, the final game binary): `tab 3, sort 1, 8 rows: 17 Litter
Bin 1, 24 Round Fountain 1, 19 Security Camera 2, 18 Security Camera 2, 23 Small Toilet 3, 22 Small Toilet 3, 21 Small
Toilet 3, 20 Staff Room 1`, eight rows lettered (`4-misc.png`); the Name heading's click `sort -1`, Staff Room first
(`5-misc-falling.png`), kept across the rides' tab; `tab 1, sort 1: 44 Balloon Shop 0, 16 Drinks Shop 0` and `tab 2, sort
1: 43 Giant Puzzle 0, 14 Jungle Spray 0`, each bought row added last and standing first; the right click on row 2
`Camera: to thing 19 on cell (40,29)`, the screen shut (`7-camera.png`). Main's build (`run-control2`) lettered five rows,
a row a kind, and counted the right click (`ALL_ITEMS_MISC_ROW_RIGHT_CLICK`, which is gone).

**Every tab picks its first row as it fills** (Q375). The add's pick ("The first row is picked as a list fills") posts
`0x401`, and the handler `FUN_00495360` has no arm for it (it goes to the default, `FUN_0065f66e`): the pick is the
highlight alone. It is an index, so the row highlighted is the one sorted first, it stays at the top over a heading's
re-sort, and the right click (`0x402`, the selected index) on a point below every row goes to that row's thing.
Measured in the original (`q375/orig/a`, `q353/grades.TPWS`; `rectcolour.py`'s teal share of row 0's name cell, row 1's
nought each time): rides 0.935, shops 0.931, sideshows 0.93, misc 0.949, and 0.939 after the Name heading with the Staff
Room on top; the pointer brought up from under the list to below the rows and a right click shut the screen with the view
on the Staff Room. **OpenTPW**: `ParkItemsScreen.BuildList` sets `UiList.PicksFirstRow`; nothing takes the posted pick,
and the `Items order` line ends `; picked N`. On the stock park with the Giant Puzzle and the Balloon Shop bought
(`q375/run-fix1`, the final game binary): picked 13, 44 (the Balloon Shop, added after the Drinks Shop), 43, 17, and 20
after the heading; row 0's teal 0.959, 0.971, 0.965, 0.968, 0.965, row 1's nought; the right click below the rows
`Camera: to thing 20 on cell (58,16)`, the screen shut, neither `LIST_FIRST_ROW_SELECTED` nor
`LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED` counted. Main's build (`run-control1`): no highlight, the screen left open, the
miss counted once and the pick four times.

#### The all-staff rows: a money cell, then bars

Read 2026-10-14 (Q369). The opener `FUN_00496620` hands `FUN_006639cb` the row factory `0x00496c10` and the styler
`FUN_00496d20`, which make each row's five cells: the name and the status as labels, the wage a money cell
(`FUN_00490250`, "The all-items rows", above: "$ 81"), the skill a bar in `ridestatbar.wct` and the happiness a bar in
`happygrad.wct`, both inset 8 from the row's top and bottom (`FUN_00485e30( cell, 0, 8 )`). The row adder
`0x00496320` hands them:

- **the wage** `FUN_00506870`: a table by kind (`[0x00785380]`, types 5, 4, 6, 7, 8 as 0 to 4) times one by grade
  (`[0x0078532c + grade x 16]`), which this game reads as `PerTypeStaffConsts[kind].PayMultiplier` times
  `PerGradeStaffConsts[grade].BaseWage` (`ParkStaffPool.WageFor`): 81 for the grade-4 handyman in both games;
- **the skill's bar** `(skill << 10) / 100`, the skill `FUN_00506550`: `__ftol( (grade - pct x -0.01) x 20 )`, the
  grade `+0x1e4` and `mPercentageThroughGrade` the byte `+0x1e8` (the double `-0.01` at `0x00700838`, the float 20 at
  `0x00700840`). That is `(grade x 100 + pct) / 5` exactly, for every grade 0 to 4 and every byte, with the FPU at 24,
  53 or 64 bits (`q369/skillfp.py`): 0 to 100 through the grades, to 131 past it, which the control holds at 1024;
- **the happiness's bar** `((__ftol( +0x1f8 ) & 0xff) << 10) / 100`, as the all-items bars.

The screen's two meters (`0x32c`, `0x32d`) are the same skin in `happygrad.wct`, each inset 8 (`0x0049698d`,
`0x00496a49`), not the gauge's `meter.wct`: horizontal, the ramp to the value and the grey track after it. The
texture (`ui.wad`, `textures\happygrad.wct`, 32 by 32) is laid out as `ridestatbar.wct`, a ramp from cyan through
orange to yellow in one half and the grey track in the other. The factory measures the row's height from its font
(`[0x007ca170]`, as the items rows' `0x007ca158`), and so does this game (Q377, "The rows' font and height").

Measured in the original under Proton on `q369/skill.TPWS` (`q353/grades.TPWS` with the percentages 60, 45, 99, 0, 13
written at the staff record's `+480`; `q369/orig/b`, `thingwords.py` on `+0x1e4`, `+0x1e8` and `+0x1f8`): its rows
letter "$ 81" and draw the skill bars 0.925, 0.283, 0.192, 0.608 and 0.417 of their width (942, 296, 194, 614 and 430 of
1024), and the happiness bars 0.85, 0.817, 0.817, 0.858 and 0.817 against its polled happiness at the frames (0.850,
0.819, 0.829, 0.850, 0.819); its meters 0.828 with a grey rest. Measured by where the grey track starts
(`barsplit.py`): `barshare.py`'s colour count drops `happygrad.wct`'s greyish-teal texels and reads them 0.02 short.

**OpenTPW** (Q369): `ParkStaffScreen.NumbersOf` and `SkillOf`; `UiList.BarSkins` and `BarInset` the styler's skin and
inset; `ParkObjectWindow.UiStatBar.Skin` the texture handed; the meters are `UiStatBar`s. Each fill logs `Staff list:
kind K, row <thing> <name>: <status> <wage> <skill bar> <happiness bar>`, and the rewrite its row 0 the same way. On
`q369/skill.TPWS`, paused at the load (`q369/run-fix2`, the final game binary): `row 25 Duke Mighten: Walking 81 942
880`, `26 ... 92 296 880`, `27 ... 36 194 880`, `28 ... 105 614 901`, `30 ... 125 430 921`; the frames' skill bars 0.92,
0.292, 0.195, 0.602, 0.425 and happiness bars 0.866, 0.866, 0.866, 0.884, 0.902 (`barsplit.py`); the meters 0.846 and
0.863 with a grey rest; "$ 81" (`1-handymen.png`). Main's build (`run-control1`) lettered the grade and the happiness as
numbers and filled its meters from the bottom. The headings are framed (Q370, "The list headings' frames"); the
status's words are Q379's (below); the list's sort is Q381's ("The all-staff list's sort", below).

#### The all-staff list's sort

Read 2026-10-15 (Q381). The opener `FUN_00496620` sets the five columns' types (`FUN_006636b2`, `0x004966a9` to
`0x004966d5`): the name and the status 0, text; the wage, the skill and the happiness 1, numbers. After the handler
(`0x004966f6`) it hands the screen's own word `[0x0075118c]` to `FUN_006659af( word, 0 )` while the list is still
empty (`0x004966fb`), and the handler `FUN_00495da0` stores message `0x406`'s value there (`0x00495fe0`). The word is 1
in the file, the name rising, and is one word for all five tabs: each tab's fill goes through the sorted insert under it
("A list's order", above). The status sorts on the word the cell letters, the skill and the happiness on their bars'
units, the wage on the wage.

Measured in the original under Proton (`q381/orig/a`, `orig/a-rows.png`), given this game's file of the stock park with
the three cleaners of the pool hired in falling name order (`q381/run-fix2/q381-fix.TPWS`; the four walking, `mState`
1): the cleaners' tab opened Chris Battson, Duke Mighten, Paul Brooke, Rajan Tande; Monthly Wage, Rajan at $ 45 and the
three at $ 63 in name order; again, those three as they stood and Rajan last; Current Status, the three Idle as they
stood and Duke, Patrolling, last; Escape and S again, the same. Its list highlights row 0 throughout (the add's pick: "The all-staff list's first row", below).

**OpenTPW** (Q381): `ParkStaffScreen._sort`, handed to `UiList.SetSort` before the fill and kept by `SortChanged`;
`TextColumns` the name and the status. Each fill and heading click logs `Staff list: kind K, sort W, N rows: ...`. On
the same scene, paused (`q381/run-fix2`, the final game binary): `sort 1`: 45 Chris Battson, 25 Duke Mighten, 44 Paul
Brooke, 43 Rajan Tande; `sort 3`: 43 Rajan 45, 45 63, 25 63, 44 63; `sort -3`: 45, 25, 44, 43; `sort 2`: 45, 44, 43,
25, and `sort 2` again opened anew; no `LIST_HEADING_SORT_NOT_BUILT` (`q381/fix2-rows.png`). Main's build
(`run-control2`): 45, 44, 43, 25 as hired (a hire goes in at the staff list's head) in all five frames, and
`3x LIST_HEADING_SORT_NOT_BUILT`.

#### The all-staff list's first row

Read 2026-10-15 (Q382). The list's own add picks the top row as the first goes in ("The first row is picked as a list
fills"), and the screen's handler `FUN_00495da0` has no `0x401` arm: its arms are `0x10` (the rewrite), 5, `0x100`,
`0x14`, `0x15`, `0x400`, `0x402` and `0x406`, and its default `FUN_00488ba0` hands `0x401` on to `FUN_0065f66e`. So the
pick is the highlight alone, as the all-items screen's is: an index, the row sorted first, kept at the top over a
heading's re-sort, and the right click (`0x402`, the selected index, `0x00495feb`) on a point below every row goes to
that row's member. Measured in the original (`q382/orig/a`, given `q381/run-fix2/q381-fix.TPWS`; `rectcolour.py`'s teal
share of row k's name cell, virtual 300, 420 + 39k, 700, 452 + 39k): row 0 0.953 on the cleaners' tab, 0.956 on the
mechanics' (one row), 0.953 back on the cleaners', 0.953 after Current Status and 0.947 after it again, Duke Mighten then
first; rows 1 and 2 nought each time. A right click below the four rows shut the screen and moved the view.

**OpenTPW** (Q382): `ParkStaffScreen` sets `UiList.PicksFirstRow`; nothing takes the posted pick, and the `Staff list:
kind K, sort W, N rows: ...` line ends `; picked N`. On Q381's scene, paused (`q382/run-fix1`, the final game binary):
picked 45 (sort 1), 43 (sort 3), 45 (sort -3), 45 (sort 2), 26 (the mechanics' tab), 45 (the cleaners' again); row 0's
teal 0.941 to 0.956, rows 1 and 2 nought; the right click below the rows `Camera: to thing 45 on cell (47,21)`, the
screen shut, neither `LIST_FIRST_ROW_SELECTED` nor `LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED` counted. Main's build
(`run-control1`): no highlight, the screen left open, the miss counted once and the pick three times.

#### The all-staff status words

Read 2026-10-15 (Q379). The row adder `0x00496320` letters the status cell with a row of the table `[0x0078baa4]`
through `FUN_006ace60` (nothing for a row past the table's end or not a string) and `FUN_00485ac0`. That word is
`0x0078b530 + 0x574`: the boot's `FUN_00419710` (called from `WinMain_Main` `0x0045ab6a` with `ECX` `0x0078b530`)
loads table `0x3ed` into `+0x574` and names it `mUIStaffStates` / `STAFFSTATES::TableID` in its failure message
(`0x00419a57`; UITEXT, table `0x3ee`, at `+0x564` is `[0x0078ba94]`, the same `0x10` apart). The table is
`Language/English/STAFFSTATES.str`: 0 Idle, 1 Patrolling, 2 Working, 3 Resting, 4 On strike, 5 Picked up.

The row is `FUN_005064f0` on `mState` (`+0x19c`): 0 to 0, 1 to 1, 2 and 3 to 3, 4 and 5 to 4, 7 to 5, and every
other state (6, and each kind's own jobs from 8 up) to 2. A 1 is then read as 0 where `FUN_00506eb0` answers nought
(`0x0049636b`): the patrol area's two words `+0x20a` and `+0x20c` are 1 and `0x4000`, cells 1 to `0x4000`, the whole
map, which the constructor `FUN_00504b90` writes and the patrol button's clear (`FUN_004b5b50` with nought,
`FUN_00506e90`) writes back. So a member walking with no patrol area of their own reads Idle. The two-second rewrite
(`FUN_00495da0`, `0x00495e41`) reads the same table.

Measured in the original under Proton (`q379/orig/bA`, `cB`): `q369/skill.TPWS` with the five members' `mState`
written (`q379/mkscene.py`) and each held by `thingpoke.py` on `+0x19c`, the staff HQ's strike flag set for the kind on
strike and a resting member's rest held at 10. Scene A (3, 7, 5, 1, 1; the researcher's area 1 to 16384) letters
Resting, Picked up, On strike, Patrolling, Idle; scene B (4, 0, `0xe`, 3, `0xf`) On strike, Idle, Working, Resting,
Working. A member of state 2 or 3 with no rest area faults the original (`0x00505fe0`'s and `FUN_00506d10`'s read of
thing `[+0x208]`), so state 2 was not photographed there.

**OpenTPW** (Q379): `Staff.StatusRow` and `PatrolsWholeMap`, `ParkStaffScreen.StatusOf`; a hire carries the whole map
as the constructor writes it (Q380: `ParkPeople.Hire`, `Staff.WholeMapBottomLeft` and `WholeMapTopRight`), and a member
with no area at all, a record built with none, is taken as the whole map too: a deviation. The fill's
log line quotes the word: `Staff list: kind K, row <thing> <name>: "<status>" <wage> <skill bar> <happiness bar>`. On
the same scenes, paused at the load (`q379/run-fixA4`, `run-fixB4`, the final game binary): A `"Resting"`, `"Picked
up"`, `"On strike"`, `"Patrolling"`, `"Idle"` with the census Resting, Held, OnStrike, Walking, Walking; B `"On
strike"`, `"Patrolling"`, `"Idle"`, `"Resting"`, `"Working"` with GoingOnStrike, Walking, Idle, Resting, Researching (B's
mechanic and entertainer change state in the sweeps before the pause). Main's build (`run-controlA2`) lettered the
activities' names and counted `STAFF_STATUS_TEXT` five times.

**The skill bar `0x2482`** ((722, 536)-(957, 575), type 9) is built at nought (`0x0049c335`) and written in one place,
message `0x401` with a row picked (`0x0049b8c6`; the only other `PUSH 0x2482` is the builder's): `((grade & 0xff) <<
10) / 5` of 1024 (`0x0049b8dc`), the grade the candidate record's byte `+8`. A grade 4 fills four fifths.

**It is drawn 10 in from its rectangle's top and bottom** (Q636): the builder calls `FUN_00485e30( bar, 0, 10 )` at
`0x0049c2ef`, before it hands the bar its skin. That helper writes the control's second rectangle, `+0x30` (four
shorts), the one the generic layout hands a control's skin with flag `0x10` (`0x0065cd55`; the control's text is
handed `+0xc`); the skin's fit (`UiControl_FitMatrix`, `FUN_00476c50`) maps its 0 to 100 box onto that rectangle
whole. So the bar stands (722, 546)-(957, 565), 19 tall, centred in the 39 of the stream and of the "Skill" label
beside it. **Measured** (`q636/orig/read.py` on the running original, the stock park's hire screen): the control's
`+0xc` (722, 536, 957, 575), its `+0x30` and its skin's kept rectangle (`+0x18`) (722, 546, 957, 565), the label's
`+0x30` its own (283, 536, 679, 575); the frame lights rows 273 to 282 at 1024 by 768. The training screen's bars
(`0x2e8`, the same 39 and the same skin) are given no inset and fill theirs.

**The walk is corroborated three ways**: the tab help rows, the resolved mesh names, and each screen's
own tab-index switch all give the same ordering, and it matches UITEXT 119–122 and 139–143.

**The root frame mesh `0xf76e4200` is `w_big.MD2`, node `window4`.** The hash is over a node name, which is why
searching file stems never finds it; see the resolution table above for the method.

#### How a list's cells are aligned

Read 2026-10-15 (Q383). `FUN_0065c428( across, down )` posts `0xc00` to the control's text (`+0x98`) and skin (`+0x9c`)
with the two bytes at the message's `+4` and `+5`. A label's skin (vtable `0x006ff320`; the label `FUN_0048f6a0`, the
number painter `FUN_0048fde0` and the money cell `FUN_00490250` alike) takes it in its slot `+0xc`, `FUN_0048fc40`:
flag `0x400` stores the across byte at `+0xd8`, `0x800` the down byte at `+0xd9`. Each constructor writes 0 and 1 there,
so **a cell no styler aligns stands at ( 0, 1 ): from the left, in the middle**. `LabelSkin_Draw` (`0x0048f830`) **translates**
the two bytes into the renderer's own codes, after `FUN_004925a0` swaps 0 and 2 where the language is `0x40d` (Hebrew;
English never is). The renderer's across code `+0xa4` is 0 left, 1 right, 2 centre (its setter `0x006adf60` accepts 0 to 2),
so the label's byte 2 (right) becomes 1 (`0x0048f888`) and its byte 1 (centre) becomes 2. The renderer's down code `+0xa0`
is 2 middle, 5 bottom and anything else top (its setter `0x006adf20` accepts 2, 4 and 5); the label's down bytes 0, 1, 2
(top, middle, bottom) become 4, 2, 5 (`0x0048fa1c`; fork watch 2026-10-18, `verify-other-a.md` M2 read the mapping,
`verify-other-b.md` L4 did not).

**The renderer places the block, `FUN_006aeb10`, and each line, `FUN_006ae550`, with truncating integer arithmetic**
(fork watch 2026-10-18, `verify-other-b.md` L4, `verify-other-a.md` M2). `FUN_006aec80` is the word-wrapper, not a width:
it breaks at the box width `+0xa8`, adds each line through `0x006add90` and returns the count; a line's width is its
record's third word. The block's height is the line count times the line height `+0xbc`. Down: code 2 puts the top at
`(bottom - top - h) / 2 + top` (`cdq; sub; sar`, rounding toward nought, no clamp, so a block taller than the box rises
above its top), code 5 at `bottom - h`, otherwise `top` (results `+0xb0`, `+0xb8`). Across, with the block width `w`
(`+0x80`): code 1 puts the left at `right - w`, code 2 at `(right - left - w) / 2 + left` truncated, otherwise `left`
(results `+0xac`, `+0xb4`). Each line then moves by `right - lineWidth - left` for code 1, and for code 2 by that
shifted right once, unsigned (`0x006ae550`). `UiText.Align` centres in floats and rounds the drawn position
(`MathF.Round`), where the original truncates: a one-pixel difference is possible and unmeasured; inside the texture
its `(int)` cast truncates as the original does.

The stylers, each call on the cell just made (`ESI`, the listing; the decompiler drops it):

| Styler | Name | Then |
|---|---|---|
| all items `FUN_00494690`, every tab | ( 0, 1 ) | **the count `( 1, 1 )`, centred** (`0x00494780`, `0x00494822`...); each money cell `( 2, 1 )`; the bars none |
| all staff `FUN_00496d20` | ( 0, 1 ) | the status ( 0, 1 ); the wage `( 2, 1 )` (`0x00496e39`); the bars none |
| visitors `FUN_004939d0` | | above: the number and the time `( 1, 1 )`, the cash `( 2, 1 )`, the rides ridden ( 0, 1 ) |
| hire `FUN_0049c880` | ( 0, 1 ) | the wage (the money cell `FUN_00490250`) `( 2, 1 )` (`0x0049c94d`) |
| buy `FUN_004ad340` | ( 0, 1 ), colour 255, 255, 255, 255 | the price (`FUN_004904c0`, vtable `0x006ff3e0`, built at ( 0, 1 )) `( 2, 1 )` (`0x004ad428`); the third cell the `i_boxtick` mesh alone, no skin and no font, so nothing lettered |

Measured in the original under Proton at 800 x 600 (`q383/orig/a`, `q353/grades.TPWS`; `listcells.aligns`, white 110):
the count's middle 0.477 of its column on the rides tab, 0.472 shops, 0.483 and 0.478 sideshows, 0.494 to 0.495 on all
eight misc rows; the money cells' middles 0.83 to 0.88; the names from 0.000 to 0.012.

**OpenTPW** (Q383): `ParkItemsScreen.BuildList` sets `ColumnAligns` Start, Centre, then End, and its shape line ends
`aligned Start Centre End End End` (5), `... End` (6) and `aligned Start Centre` (2). On the stock park with a shop and a
sideshow bought, paused (`q383/run-fix3`, the final game binary): every count centred, its middle 0.488 to 0.518 (rides,
shops, sideshows) and 0.495 to 0.498 (misc, eight rows); the money cells' middles 0.873 to 0.906 (`q383/side-by-side.png`:
the original, this, main). Main's build (`run-control1`) lettered every count from the right. `ParkStaffScreen` already
letters the name and status from the left and the wage from the right; this game's `TextDown` is the middle by default.
The Hebrew swap is not kept (no Hebrew data ships).

**The hire and buy lists** (Q384): `ParkHireScreen` and `ParkBuyScreen` set `ColumnAligns` Start, End, which
`UiList`'s default already gave, and their shape lines end `aligned Start End`. The stock park, paused, the buy screen
by the gadget's button and the hire screen by its `b_allstaff` (`q384/run-fix1`, the final game binary): four buy rows,
the names from 0.000 to 0.016 of their column, the prices ending 0.969 to 0.979; three hire rows, the names from 0.000
to 0.005, the wages ending 0.966 to 0.974. Main's build (`run-control1`) measured the same to the third place. The
original at 800 x 600 (`q384/orig/b`, `q353/grades.TPWS`): the names from 0.000 to 0.010, every price ending 0.951 and
every wage 0.968, a "$ " before each (Q158's "From Q238"; `q384/fix1-lists.png`, `orig/a-lists.png`). In no game run:
Full Simulation (not asked: the screens are either mode's), the other three themes.

#### The visitors rows: a number, a money cell, a bar

Read 2026-10-14 (Q371). The visitors opener `FUN_00493530` hands `FUN_006639cb` the factory `FUN_004938c0` and the
styler `FUN_004939d0`, which makes the six cells, each in the font `[0x007508b4]`:

| Column | Cell | Alignment | The adder `FUN_00493800` hands |
|---|---|---|---|
| Visitor Number | `FUN_00490c30` (vtable `0x006ff470`, setter `0x00490dc0`): UITEXT `0x1c9` (457, "New Visitor") at nought, else `"%d"` | `FUN_0065c428( 1, 1 )` | `+0x1d8` |
| Cash Remaining | a money cell `FUN_00490250` ("The all-items rows", above) | `( 2, 1 )` | `+0x1a0` |
| Time In Park | `FUN_00490e60` (vtable `0x006ff4a0`, setter `0x00490ff0`): by hours, below | `( 1, 1 )` | `FUN_004fd950`'s stay through `FUN_004f8730`, `__alldiv` 36,000,000,000: hours |
| Rides Ridden | the number painter `FUN_0048fde0` | none set | `+0x1c4` |
| ? | a type-6 cell wearing the mesh filed as `ti_hungry`, `thoughts.MD2`, inset `( 4, 0 )`: below | | `+0x30` less 1, or 999 at nought |
| Happiness | a bar in `happygrad.wct` (`FUN_00477ff0`), inset `( 0, 8 )` | | `((__ftol( +0x19c ) & 0xff) << 10) / 100` (`0x00493870`) |

`FUN_0065c428( across, down )` posts `0xc00` with the two bytes to the cell's text and skin ("How a list's cells are
aligned", above: 0 left, 1 centre, 2 right). **Measured in the original's frame as well** (`q371/measure.py`'s `aligns`, the lettering's extent in
its column): the number centred (its middle 0.495 of the column), the cash to the right (its end 0.976), the rides from
the left (its start 0.032), as `( 2, 1 )` reads right on the park status screen above.

**The thought** (read 2026-10-15, Q373). The factory `FUN_004938c0` makes the row's fifth cell a **type 6** control
(`FUN_0065dd7b( 6, 1, 4, ... )` at `0x0049399b`: `FUN_0066d9e0`, vtable `0x00705c58`), where the happiness cell is type 9 (`FUN_0065dd7b( 9, 0x11, 5 )` at `0x004939b0`) and the number cell is the row
control itself (`FUN_0065bcad` at `0x00493932`). Its
value setter (vtable `+0x1c`, `0x0066da4d`) keeps the value at `+0x13c`, hands it to the text and the skin as the **state
word** (flag 1, data `+4`), then calls the base's (`0x00661525`, flag `0x80` alone, which a mesh skin passes over). The
styler's `FUN_00659bc9( "ti_hungry" )` clones a template from the list `[0x00faa610]`: `FUN_00489060` files every
`ui.wad` model whose name has no `!` under its first node's name as a mesh skin (`FUN_00476a90`, vtable `0x006fee10`),
so `ti_hungry` is **`thoughts.MD2`**, seventeen nodes, each wearing its own `ti_*.wct`:

| Part | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Node | hungry | thirsty | hungthir | toilet | good | costhigh | litter | superhappy | happy | normal | sad | bored | angry | sick | scared | queuetoolong | confused |

(each `ti_` and the node's name; `ti_costhigh` wears `ti_bad`, `ti_superhappy` `ti_suhappy`, `ti_queuetoolong`
`ti_queuebad`). The skin's `+0xc` (`0x00476f10`, `0x00476e80`) shows the node its state and frame (`+0xa`, nought here)
count to down the model's node list and hides the one before (`|= 0x8010`); a count past the last node shows nothing.
So thoughts 1 to 17 show parts 0 to 16, and 18 to 22 and none (999) show nothing. `FUN_00485e30( cell, 4, 0 )` is
`FUN_0065a43e( 4, 0, -4, 0 )`: four units in from the left and right, none from the top and bottom. The two-second
rewrite hands the same value (`0x0049330c`) and, as for every column, moves no row.

Measured in the original under Proton on `q373/thought.TPWS` (`q371/visitors.TPWS` with ten guests' `mLastThought`
written at the record's `+386`; `q373/orig/a` and `b`, `thingwords.py` on `+0x30`): 0.58 s after V the ten cells were
confused, bored, hungry, bad, thirsty, none, bored, toilet, hungthir and bad, each its guest's polled thought less one
(18 none), four of them already moved off the file's by its running park (`lib/texmatch.py`, the icon's pixels against
the 17 textures). The "?" heading's click sorted on the thought as it stood then: a guest whose thought went from 3 to 6
after the click stood between the thirsty and the toilet rows, drawn bad from the next rewrite on (`q373/orig/b`).

**The time in park** (read 2026-10-14, Q372). The stay is `FUN_004fd950`: `mGameTick` less the guest's `+0x1d4`, taken
unsigned and shifted right by two (`SHR` at `0x004fd963`). `FUN_004f8730` on the park calendar (world `+0x2a0`, its
`+0x1c` `mFunnySecsPerRealSec`, 15000) makes it 100 ns units: `__allmul( rate, stay )`, over four, `__allmul` by
10,000,000, a 64-bit product that wraps; the adder `__alldiv`s that by 36,000,000,000, signed (`0x0049383e`), and keeps the
low half: whole hours. **The two-second rewrite (`0x00493270`) shifts the stay back left by two first** (`SHL EAX, 2` at
`0x004932db`), so from the first rewrite on a row letters four times the hours its fill did; an arrival's row (`0x1c`,
`FUN_00493c50`) goes through the adder, on the tick the guest is stamped. The setter `0x00490ff0` (on a message with
`0x80`) letters through `FUN_006acd60` on `[0x0078ba94]` with one `VARM` variable, whose type word is the string's
parameter number:

| Hours | UITEXT | Parameter |
|---|---|---|
| past 48 | `0x1bb` (443, "{8} days") | 8: the hours / 24 (`MUL 0xaaaaaaab`, `SHR 4`) |
| past 24 | `0x1ba` (442, "{8} day") | 8: the hours / 24, so 48 hours is "2 day" |
| else | `0x1b9` (441, "{11} hours") | 11: the hours, a negative one `NEG`'d with the variable's sign word set |

The variable's writer (`0x006add40`, its vtable `0x006ff170` slot 0) prints the value word `"%d"` and never reads the
sign word, so a negative count letters its size. **A guest stamped after the clock** has a stay near 2^30: the adder
letters 93,661,950 hours four sweeps ahead ("3902581 days"), and the rewrite's four times that stay wraps past the sign,
-137,761,755, lettered "137761755 hours". The list sorts the column on the signed hours.

Measured in the original under Proton on `q372/time.TPWS` (`q371/visitors.TPWS`, tick 963, with eight guests' `+398`
written; `q372/orig/b`, `thingwords.py` polling its clock): 0.58 s after V, at its tick 1056, the ten rows read the
adder's words exactly ("4 days", "1 day", "1 day", "2 day", "3 days", "3 days", "9 days", "3902538 days", "23 hours",
"4 days"); 7 s on, the rewrite's at 1080 ("18 days", "5 days", "9 days", "9 days", "13 days", "13 days", "39 days",
".37765797 hours", "5 days", "18 days": a nine-digit count clipped at both ends of its cell); the Time In Park heading's
click sorted the rewrite's numbers, the wrapped guest first. The time is centred (0.317 to 0.65 of its column).

Measured in the original under Proton on `q371/visitors.TPWS` (`q353/grades.TPWS` with five guests' happiness written
at the guest record's `+422` and thing 42's `mArrivalIndex` at `+402` written 0; `q371/orig/b`, `thingwords.py` on
`+0x19c`): thing 42 letters "New Visitor" and sorts first, visitor 1 "$ 306"; its ten bars measure 0.5, 0.125, 0.3,
0.758, 1.0, 0.792, 0.525, 0.558, 0.583 and 0.5 of their width against its polled happiness's bars 0.5, 0.119, 0.289 to
0.31, 0.75, 1.0, 0.789, 0.52, 0.55, 0.569 to 0.59 and 0.5 (`barsplit.py`). A guest's happiness moves as its park runs:
run a, unpolled, read four bars off the file's by 0.06 to 0.14.

**OpenTPW** (Q371): `ParkVisitorsScreen.RowOf` and `NumberText`, the list's `ColumnAligns`, `BarColumns`, `BarSkins`
and `BarInset` 8. Each fill logs `Visitors list: row <thing> <number>: <cash> <rides> <happiness bar>`, and each
rewrite `N rows rewritten, row 0` the same way (the time added since, below). On the same file, paused at the load (`q371/run-fix1`, the final game
binary): `row 42 New Visitor: $ 684 0 512`, `row 38 1: $ 306 1 122`, ... `row 37 4: $ 570 0 1024`, sorted 42 first;
its bars 0.5, 0.125, 0.33, 0.902, 1.0, 0.714, 0.527 and 0.5 (the file's happiness); the number centred (0.497), the cash
right (0.974), the rides left (0.034) (`q371/side-by-side.png`: the original, this, main). Main's build (`run-control2`)
lettered 0 and the cash and happiness as bare numbers, every one from the right, and drew no bar. The rows are 56 units
apart, as the original's 55.8 (Q377, "The rows' font and height"). **Not built**: the first row picked as the list fills
(Q232); the scrollbar.

**OpenTPW** (Q372): `Peep.StayAt`, `ParkVisitorsScreen.HoursIn` and `TimeText`; `RowOf`'s `rewrite` shifts the stay.
The log's row reads `<cash> <time> (<hours> h) <rides> <bar>`. On `q372/time.TPWS`, paused at the load's tick 964
(`q372/run-fix3`, the final game binary): the fills `row 42 New Visitor: $ 684 3 days (79 h) 0 512`, `38 ... 5 hours
(5 h)`, `40 23 hours`, `34 1 day (25 h)`, `37 2 day (48 h)`, `36 2 days (50 h)`, `41 8 days`, `33 3902537 days
(93660909 h)`, `32 0 hours`; the rewrite's row 0 `13 days (316 h)`; the heading's click `sort 3`, `33 7 -137765918`
first, then 0, 20, 95, 100, 195, 200, 316, 316, 320, 325, 329, 833 hours (`2-by-time.png`). Main's build lettered no
time and counted `VISITOR_TIME_IN_PARK` and the sort. This game's smaller lettering fits the nine digits in the cell.

**OpenTPW** (Q373): `UiList.MeshColumns` and `MeshInset` (a mesh cell framed by the row's number for its column, nothing
past the mesh's last part), `ParkVisitorsScreen.ThoughtsMesh` and `ThoughtPart`; the log's row reads `... <rides> part
<part> <bar>`. On `q373/thought.TPWS`, paused at the load's tick 964 (`q373/run-fix1`, the final game binary): the fills
`row 42 New Visitor: ... 0 part 16 512`, `38 ... part 999`, `40 part 0`, `34 part 13`, `37 part 1`, `36 part 17`, `41 part
8`, `33 part 3`, `32 part 10`, `35 part 5`; the frame's cells confused, none, hungry, sick, thirsty, none, happy, toilet,
sad, bad and none on the last three; the "?" heading's click `sort 5`, `40 37 33 35 41 32 34 42 36 38 39 29 31`, drawn
hungry to confused, then five without (`q373/side-by-side.png`: the original's sorted column, then this game's). Main's
build (`run-control1`) drew no picture and counted `VISITOR_COLUMN_117_UNNAMED` and `VISITOR_SORT_ON_UNBUILT_COLUMN`.

#### The rows' font and height

Read 2026-10-15 (Q377; the hire and buy lists Q378). **The five lists' rows are lettered in font slot 7**, and their height is
the row factory's:

| List | Factory | Height | Kept at | Styler, every cell in |
|---|---|---|---|---|
| visitors | `FUN_004938c0` | `0x38`, 56: font `[0x007508b4]` is measured and its answer thrown away (`0x004938fa` writes 56 whatever it was) | `[0x007ca140]` | `FUN_004939d0`, font `[0x007508b4]` |
| all staff | `0x00496c10` (no function) | font `[0x00751184]`'s height x `0x600` / `[0x00faa5c0]` + 6 | `[0x007ca170]` | `FUN_00496d20`, font `[0x00751184]` |
| all items | `FUN_004944d0` | font `[0x00750d54]`'s height x `0x600` / `[0x00faa5c0]` + 6 | `[0x007ca158]` | `FUN_00494690`, font `[0x00750d54]` |
| hire | `FUN_0049bbb0` (the opener hands `0x0049c870`, a jump to it) | font `[0x007523dc]`'s height x `0x600` / `[0x00faa5c0]` + 6 | `[0x007ca314]` | `FUN_0049c880`, font `[0x007523dc]` |
| buy | `FUN_004ad240` | font `[0x00755134]`'s height x `0x600` / `[0x00faa5c0]` + 6 | `[0x007cc200]` | `FUN_004ad340`, font `[0x00755134]` (the third cell a tick-box mesh) |

The five font words are 7 and only read (the heading's word beside each is 8, Q370), and the rule is the park-file screens' own
(`FUN_0049f0b0`, `saves.md`). The list takes the height as handed (`FUN_00663718`: `+0x170`) and fits
`(bottom - top) / height` rows (`+0x172`, again in `FUN_00664ba8`), as `UiList.VisibleRows`. `[0x00faa5c0]` is the
screen's height and `[0x0078b534]` the font set (`FUN_00485a70`: `0x0078b54c + (slot + set x 13) x 0x18`).

**Measured in the original under Proton** (`q377/orig/a`, `q373/thought.TPWS` loaded, V, S, A, read with `tpwmem.py`):
`[0x00faa5c0]` 480 and `[0x00faa5c4]` 640, set 1, so **the reference original draws at 640 x 480**, its frames scaled to
1024 x 768; `[0x007ca140]` 56 once V is up, `[0x007ca170]` 41 once S is, `[0x007ca158]` 41 once A is (GAME8.bf4, 11 tall:
11 x 1536 / 480 + 6), `[0x007cb24c]` 41. Its frames: the visitors rows 27.9 pixels apart (55.8 units), ten shown; the
items rows 20.4 to 20.6 (41 units). At its own 800 x 600 and 1024 x 768 (`Data/Resolution.sam`, `q377/orig/c` and `b`): `[0x00faa5c0]` 600 and
768, sets 2 and 3, and the staff, items and Load Park heights 39 and 34 (GAME9.bf4, 13; GAME10.bf4, 14), the visitors'
56 in both; the rows 56 units apart in both frames.
At 800 x 600 with `q353/grades.TPWS` (Q378, `q378/orig/a`): `[0x007cc200]` and `[0x007ca314]` 0 after the load, 39 once
the gadget's Buy and then `b_allstaff` built each screen; the frames' wages and prices 38 and 39 units apart
(`listcells.ink` on the number column: a name's descender moves a band's middle).

**OpenTPW** (Q377): `UiList.RowHeightFor` and `RowHeightOf( font )`, the font of the current set; the staff and items
lists `RowFont` 7 at `RowHeightOf( 7 )`, the visitors `RowFont` 7 at `0x38`. At this game's 1280 x 720 window (set 2,
font 7 GAME9.bf4, 13 tall) that is 39 units, where the original at 640 x 480 has 41: the same rule at another size.
Each screen logs `<list>: rows of H units in font F, N visible` as it is built. In the stock park with every pool
candidate hired, paused (`q377/run-fix1`, the final game binary): `Visitors list: rows of 56 units in font 7, 10 visible`,
`Staff list: rows of 39 units in font 7, 9 visible`, `Items list: the 5-column shape, rows of 39 units in font 7, 11
visible` and the 6- and 2-column shapes the same; the frames' lettering 55.45 units apart in ten visitors rows and 38.4
in the four handymen's and the eight misc rows (`listcells.ink`), 1.17 to 1.25 times main's lettering in height; the
pointer at a slot's middle by the new height picked slot 2 in all three. Main's build (`run-control1`) lettered thirteen
visitors rows 44.8 apart, all in font 6.
The hire and buy lists the same (Q378): `RowFont` 7 at `RowHeightOf( 7 )`, logged `Hire list: rows of 39 units in font 7,
12 visible` and `Buy list: rows of 39 units in font 7, 11 visible` on the stock park (`q378/run-fix1`, the final game
binary); the three candidates' wages and the four rides' prices 38.4 units apart, 1.11 times main's lettering in height,
and the pointer at slot 2's middle by 39 picked candidate 8 and ride 1101. Main's build (`run-control2`): 44.8 apart.
No list takes `UiList`'s default of 44 now.

#### The list headings' frames

Read 2026-10-14 (Q370). **Every list's opener letters and frames its headings through `FUN_00486660`**, as the hire and
buy lists' ("A list's order", above): the label skin, the nine-slice frame `!cbut`, the text centred both ways, the font
slot and colour handed over and the help row at `+0x90`. The all-staff opener `FUN_00496620`, the four all-items tab
builders (`FUN_00494ec0` rides, `FUN_00494c70` shops, `FUN_00494250` sideshows, `FUN_00495110` misc) and the visitors'
opener `FUN_00493530` each hand font slot 8 from a word of their own that nothing writes (`[0x00751188]`, `[0x00750d58]`,
`[0x007508b8]`) and the colour 255, 239, 0. Only the help rows differ (`UIHELPTEXT.str`, each "Left-click to sort the
list by ..." its column):

| List | Help rows |
|---|---|
| all staff | `0x77` to `0x7b` (119 to 123) |
| visitors | `0x81` to `0x86` (129 to 134) |
| items, rides | `0x69` to `0x6d` (105 to 109) |
| items, shops | `0x69`, `0x248` to `0x24b` (105, 584 to 587) |
| items, sideshows | `0x69`, `0x248`, `0x6b`, `0x249` to `0x24b` (105, 584, 107, 585 to 587) |
| items, misc | `0x69`, `0x24c` (105, 588) |

Measured in the original under Proton (`q369/orig/b-1-handymen.png`, `q367/orig/a-*.png`, `q370/orig/a-visitors.png`
and `a-misc.png`, `rectcolour.py` on each heading's rectangle): teal 0.80 to 0.985 of each, yellow lettering in each.

**OpenTPW** (Q370): `UiList.AddFramedHeading`, `HeadingFont`, `HeadingColour`; the items screen sets each tab's help
rows with its headings' text. Each opening logs `Staff list: headings` or `Visitors list: headings`, and each items tab
`Items list: headings of tab N:`, every heading as `mesh font help "text"`. On the stock park (`q370/run-fix2`, the final
game binary): `Staff list: headings !cbut 8 119 "Name", ... !cbut 8 123 "Happiness"`, the visitors' `129` to `134`, and
the four tabs' rows as the table; all 29 headings teal 0.854 to 0.99 with yellow lettering and no white
(`q370/side-by-side.png`). Main's build (`run-control1`) lettered them white on black. **Not built**: the original's
lettering is larger and wraps "Monthly Wage" where this game's fits one line of its smaller font (the park screens'
lettering, a deviation Q352 filed); the all-items headings sort (Q374), and the all-staff ones (Q381).

### The node name is not the file name — measured

The stream hashes the model's first **node** name,
while the loader opens `ui/<file>.md2`. Where an artist named the two differently they diverge, and a
name taken from the hash table simply does not load.

Listed out of `ui.wad` itself rather than inferred:

    buyitem   -> f_buyitem        hirestaff -> list_hirestaff     balance  -> f_balance
    staffinfo -> f_staffinfo      staffpic  -> f_staffpic
    b_sride   -> b_srides         b_sresrcher -> b_sresrhcer

**`b_sresrhcer.MD2` is misspelled in the archive** — the h and c transposed — while its own textures are
spelled `b_sresrcher.wct`. That one cannot be guessed at from the hash or from the texture beside it.

The rest of the set (`b_sshop`, `b_sshow`, `b_sfeature`, `b_shandy`, `b_smech`, `b_senter`, `b_sguard`,
`b_scroller`, `b_allstaff`, `b_allthings`, `i_boxtick`, `!frame`, `!slider`) load as-is, because there
the node and the file happen to agree. **`b_up` and `b_down` agree too**: each is the first node of the `.MD2` of its own name, no other
mesh node in `ui.wad` hashes the same, and the scrollbar's two buttons wear them (buy, `0x0075507a` and `0x00755056`).

Read the archive with the `wadcat.py` harness (`<wad> list`; the harness folder is in `CLAUDE.local.md`) — a port of this tree's own
`WadArchive` and `Refpack`, validated against two values the codebase documents independently.

### The gate that looks like a bug, and is not

`FUN_004a0940` does nothing when `FUN_0048c8d0()` returns 1, which needs both `DAT_007c2534` and
`FUN_006ad810()` non-zero. Both halves are named:

- `DAT_007c2534` is the in-game **Escape menu object** — a 0x14-byte MenuList built by
  `GameMenu_BuildPark` / `GameMenu_BuildLobby`. Non-zero means the menu exists.
- **`FUN_006ad810` is not a money function at all.** It is a two-byte COMDAT-folded accessor,
  `MOV EAX,[ECX+0xc]; RET`, shared by unrelated classes across 29 references — its meaning is entirely
  the receiver's. Here the receiver is the MenuList, so it is `MenuList::IsShown()`.

So **`FUN_0048c8d0()` means "the game menu exists AND is on screen"**, and the buy button refusing
while the pause menu is up is correct behaviour rather than an unbuilt path. The same function on the
bank Thing is what reads the balance.

**A caveat about the teardown**: several menu arms do NOT clear
`DAT_007c2534` — those that open a confirm box leave it set until that box's own callback runs, so the
menu object outlives the menu on screen.

### What the buy list actually filters on

The list is built from a per-item descriptor array at `world+0x1d943c`, stride 0x20, 150 slots. The
four filter fields, as **byte** offsets:

    +0x508  the item's model failed to load
    +0x4AC  Info.WhichUIType      (4 = "not shown in the UI", the files' own words)
    +0x284  AddOn.UpgradesId      (must be 0 - a standalone item, not a ride's upgrade)
    desc+0x10  researched/available

`desc+0x10` is not a static "is this listed" flag - it is the **researched** flag, and at a park load it is the
**save's**, not the item file's (Q201). The record array is `CControlManager`'s, 150 records of 0x20 bytes looked up by
`FUN_004d3d10`, and it is saved and loaded raw as `mObjectControls[150]` (`FUN_004d3aa0`). Its writers, in the order a
park load meets them:

1. **World setup** (`FUN_005156a0`): the array is zeroed (`FUN_004d3de0`), then `FUN_004d3e00` appends one record per
   catalogued item with `+0x10` = `Upgrades[0].CostOfResearch` (`item+0x180`) is nought (`0x004d3e92`). The research
   manager's setup (`FUN_00502d60` -> `FUN_005031a0`, from `0x00515d2f`) sets `+0x10` to 1 again for every standalone
   item with `Info.WhichUIType` below 4 whose cost is nought (`0x0050320a`) and enrols the rest as research candidates.
2. **The park's save** (mode 2, `FUN_00414d40` -> `FUN_00415270` -> `FUN_005179c0`, the read at `0x005181e7`) lays the
   saved array over all of it. `FUN_00415140` then runs `FUN_004d3e00` again, which appends a record only for an item
   the save has none for; an item the save holds keeps the save's byte.
3. **Research completing** (`FUN_00504630`) sets `+0x10` to 1 (`0x005046a1`) and `+0x14` to the upgrade tier reached;
   `FUN_00504580` steps `+0x14` past a tier whose research costs nothing, so `+0x14` is the tier researched, 0 to 2.
4. **The Alt+R cheat** (`0x00503ff0`, from `0x0040c430`; `park-engine.md`, "The cheat table") sets `+0x10` to 1 for the item of
   every open lab project and writes nothing else: no project removed, no `+0x14` tier, no message (fork watch 2026-10-20,
   `verify-other-ui-audio.md` O-L1).

**Measured in `Easymode.TPWI`** (`q201/ctrldump`, all 50 records): `+0x10` is 1 on 26, exactly the 26 whose item's own
file, with its `Easy_` layer, sets `Upgrades[0].CostOfResearch` 0 - the two rules agree in the shipped park, and differ
only in a played save, where research has set more. Among them rides Belly Bounce, Crazy Ape, Rocky Racers, Aztec
Mayhem; shops Balloon, Burger, Drinks; sideshows Jungle Spray, Strength Bird. **Photographed in the original's Lost
Kingdom** (Instant Action, under Proton, 2026-09-30, Q178b): exactly those, and features Buy Land, Clear Land and the
eight whose cost is nought, and no mystery row. **OpenTPW lists the same** (Q201b, `ParkResearch`: the save's
records, an item it lacks seeded from its cost; `q201b/run2/`: rides 4, sideshows 2, shops 3). **A project finished
marks its item, and the list lists it from its next filling** (Q701; `park-engine.md`, "The lab at work"): Dino Karts
on the rides tab after its 95th hand-out in both games (Q701). It bears the recently researched star, row state 2, in
both (Q702, from `q702/dino576.TPWS`, its 4th hand-out after the load, below).

**The row state is {0,1,2} and both non-zero values are pinned**: 1 = you already own at least one
(`desc+0x18`, incremented on placement and decremented on demolition), 2 = one of the three most
recently **RESEARCHED** items in that tab — a 3-entry ring per tab fed only by the research-complete
message, **not** by building. The game's own help row 146 says the same thing: "sort the list by items
already owned or recently researched". **Read whole** (Q702; `BuyScreen_FillList` `0x004aaf70`, `0x004ab097`..`0x004ab0f7`):
owned (`FUN_004ca8c0` at or over 1) is 1 before anything else; else the item against the tab's ring 0, 1 and 2 back from
its newest (`Analyser_RecentlyResearchedBack` `0x004caa70`: at or before the newest slot, that slot; further back, only
once the ring has wrapped, else nought), 2 on a match; else 0. The ring is the analyser's (`park-engine.md`, "The lab at
work", message `0x11`). Built (`ParkBuyScreen.StateOf`, `ParkResearchedRings`): Dino Karts starred and the Belly Bounce
ticked in both games, and the state heading's sort the original's (`q702/side-by-side.png`). The original draws an empty
box on a row of state 0, this list nothing (Q158).

**The mystery row** is inside the researched test, never an unresearched item's (`0x004ab023`): a researched item
whose `UsageInfo.GoldenTicketCost` (`item+0xC4`, a dword; `Research.Group` is `+0x178`) is above nought and whose id
the player has not unlocked (`FUN_004d4b70`, `0x004ab047`) is named UITEXT 137 "??? Mystery Ride! ???" and priced at
**the negated ticket cost** (`0x004ab050`..`0x004ab063`), which its price cell letters "1 gt" ("Spending golden tickets", below). The unlocks are `PlayerProgress`'s set of item ids
(`DAT_00f7c58c`, built lazily by `FUN_00409410`; `FUN_005af780` finds an id), inserted only by the purchase
(`FUN_004db090` -> `FUN_004d4ad0`) when the tickets suffice, the cost added to the tickets spent; in game type 1 it
inserts nothing. The set is the player's, saved in `gms.dat` (`FUN_005aff50`; `saves.md`, the "ride ids" rows),
across every park and theme. No researched item in Instant Action's Lost Kingdom has a ticket cost, which is why the
original showed no mystery row. OpenTPW draws the row from the current player's `RideIds` (none with nobody playing) and buys it
with tickets ("Spending golden tickets", below).

**Spending golden tickets** (decoded for Q604, Q709's first piece; confirmed in the original on a fresh Full
Simulation park whose player had 4 tickets earned, 2 spent and no ride bought, `q709/orig/a`). Two tests on the
player's progress (`[0x00f7c58c]`) serve these readers: `GoldenTicket_RideBought` (`0x004d4b70`) asks the ride-id set
(`PlayerProgress_RideBought` `0x005af780`: an MSVC set of words, head `+0x2c`, nil `[0x00f7c970]`, a node's id at its
`+0xc`), and `GoldenTicket_CanBuyRide` (`0x004d4b90`) whether the item's `GoldenTicketCost` (`+0xc4`) is at most
`PlayerProgress_TicketsLeft` (`0x005af5d0`): `PlayerProgress_CountTickets` less `+0x1c`, the spent count, with no floor.
The listing's callers of `0x004d4b70` are 0x4ab047, 0x4ab290, 0x4abdc3, 0x4ac590, 0x4acbfa, 0x4db4c3, 0x5035df, 0x503792
and 0x523601; of `0x004d4b90` 0x4abddc, 0x4ac5a9 and 0x4acc10; of `0x005af5d0` 0x4a10cd (the gadget's corner), 0x4d4b3d
and 0x4d4bad. The bullets name the list's rows (0x4ab047 is the list fill's mystery naming, `BuyScreen_FillList`, above),
the cell colour, the click, the carry, the purchase and the lab; the help state (0x4abdc3, 0x4abddc) and the panel's fill
(0x4ab290) ask the same two tests for the panel's text and are not read further. Their readers:

- **The row's lettering.** The list's row is a name label, a price cell on its own skin (`PriceSkin_Construct`
  `0x004904c0`, by `BuyList_MakeRowCells` `0x004ad340`) and the tick box. The price cell's setter (`PriceSkin_SetValue`
  `0x00490650`) letters a number at or above nought by UITEXT 449 "$ {P13}" and one below nought by **455 "{P23} gt"**
  with its negation, so the mystery row reads "1 gt" and "3 gt" (a number past ±`0xeeeeeee` takes 449 or 450 less that
  bias). Both cells are coloured by `BuyList_CellColourCallback` (`0x004acb70`, on message `0x403`): a land row
  (231, 239, 75); a mystery row **gold (255, 224, 0)** where `GoldenTicket_CanBuyRide` passes and **red (255, 0, 0)**
  where it fails; any other row white, or red where its price (`FUN_004d3d10`'s `+4`) is above the bank. Measured: the
  -1 row's ink (255, 227, 0) at its purest, the -3 row's (243, 0, 0), the cash rows white (`q709/orig/a-lists.png`).
- **The click** (`BuyScreen_OnMessage` `0x004ac270`, message `0x400`): an item whose cost is nought, or already bought,
  goes through the bank test; a mystery item through `GoldenTicket_CanBuyRide` alone (`0x004ac5a9`), **the bank not
  asked**. Passing, both arms are the same: the carry tool (`FUN_0046c5a0( 4, item )`), the screen closed, advisor
  message `0xce` (`0xc9` where the item's `+0x4c` is nought and `FUN_004824d0( 0x80, 0 )` answers nought). Failing, `0x00486ae0` (the refusal sound, `cat_ui` `0x1D`) and the
  screen stays. Measured: the -3 row's click left the screen up and the pick's mode; the -1 row's closed it and put
  the ride in the hand.
- **The carry** (`BuildTool_PreviewPass`, `0x005235e6`..`0x00523613`): an item with a cost not yet bought sets the run's
  price to nought (`FUN_0052fb90( 0 )`) and skips the price-against-bank test below it, so it is never refused for
  money; the original shows "$ 0" under the balance while carrying it (`q709/orig/a-place.png`). The move tool's put-down (`0x00523940`..`0x005239fc`) has no such exemption: it sets the run's price to the item's full `+0x1b8` (`0x0052398b`) and meets the cell test `0x00535905`..`0x0053592b`, so a moved ticket ride is refused where its price is above the bank (read from the listing, not observed; OpenTPW's `ParkBuilding.Build` skips the test for a move too).
- **The purchase** (`Object_Construct` `0x004db090`, `0x004db4a6`..`0x004db4f6`): such an item calls
  `GoldenTicket_SpendForRide` (`0x004d4ad0`) **in place of** the withdrawal of its price, and ignores its answer.
  In game type 1 (online) that does nothing; in any other, where the tickets suffice, `PlayerProgress_BuyRide`
  (`0x005af740`) inserts the id and, only when the insert is new, adds the cost to the spent count. No cash is taken
  either way, so an item placed with too few tickets would be built free; the click's test is what stops it. Every
  later purchase of the item finds it bought and pays the price in cash. Measured: the bank 50000 either side of the
  put-down; `progress.py` "spent 2 ... rides []" to "spent 3 ... rides [1170]" on tick 291; the gadget's ticket count
  (`0x37`) "2 x" to "1 x", the keys' `0x36` "2 x" unchanged.
- **The save.** Nothing here writes `gms.dat`; the progress object is written at its next write ("Load Game and Save
  Game", `saves.md`): after a park save the file read spent 3 and ride ids [1170] (`q709/orig/a/gms-after.dat`);
  one byte more changed, file offset `0x13` (the swear filter, `+0x25`) from 1 to 0, which none of the functions above
  writes: the profile loader `FUN_005afd70` sets `+0x25` to 1, then clears it when `swears.txt` under
  `Data\Language\<language>\` does not open (`0x005afeba`..`0x005aff37`; `saves.md`), and a disc copy (the reference
  install) has no `Language\American\swears.txt` where an installed game has it, so an installed game keeps 1, so no test is built on "nothing else
  changes". Afterwards the row is the ride's own, "Jurassic Tours" at "$ 3000", with the owned tick.
- **The lab** asks the same set for a kind-4 project (`park-engine.md`, "The next project").

**The tie between the two mystery rows** is the list's sorted insert (after every equal row) fed in the catalogue's
order (`FUN_00412e60`; `park-engine.md`, "The catalogue's order"): `tourride` (Jurassic Tours) is walked before `volcano`
(Eruption), so it stands first.

**OpenTPW** (Q709): `GoldenTickets` holds the two tests, the mystery test and the spend; `ParkBuyScreen.PriceText` is
the price skin's setter, `ColourOf` the colour callback (asked through `UiList.RowColour` each time the rows are drawn
again, as `0x403` is sent from `FUN_00663324` for each cell, `0x00663499`), the click refuses with `cat_ui` `0x1D` on
either arm; `ParkBuilding.Carry` asks the tickets of a mystery ride, `Build` skips the money test for it and `PayFor`
spends in place of the price. Confirmed on `q709/gms-two.dat` (round B, `q709/run2-B`, 9 of 9 predicted): the rides tab
"1 gt" (255, 224, 0) and "3 gt" (255, 0, 0) first in the original's order, "$ 2500" white; the 3 gt click refused with
the screen up; the 1 gt click carried; put down, the bank 50000 either side, spent 2 to 3, ride ids [1170], the corner
"1 x"; `gms.dat` spent 3 [1170] after a save; then "Jurassic Tours" "$ 3000" with its tick (`side-by-side-lists.png`,
`side-by-side-place.png`). Not built: the price in the hand, "$ 0" there (Q541); the click's advisor message `0xce` or
`0xc9`, for either arm (Q465); the online arm (no game type 1 here).

**A row's name** is `0x00412fb0`'s (called at `0x004ab079`, 0x42 characters). It takes the item's `+0x4f4` when that
is set and is not "MISSING STRING" (`0x006fd880`): the first of the strings `NAME`, `SIGNA` and `SIGNB` (the table at
`0x007487f8`) that `0x00413600` parses from an item's "object text file", each starting as "MISSING STRING". No shipped
file holds those fields (none of the 274 item wads' `.txt` entries, all `qickload.txt`, nor any loose file under
`data/`), so the name is `0x004146b0`'s: the item's two `OBJECT_NAMES` rows by the table at `0x007488d8` (both empty
for an item it lacks), a first row ending in '-' cut and the second joined straight on (`%s%s`, `0x00749850`), else
`%s%s%s` with a space (`0x0074986c`) between where the second is not empty and nothing where it is. Dino Karts is rows 2
"DinoKarts" and 3 "", so its row reads "DinoKarts" where its `.sam` `Info.Name` says "Dino Karts" (Q701's frame).
OpenTPW letters that name (`ParkObjectNames.NameOf`) in the buy list, which sorts by it, the items list, the object
window's title and, through `FUN_004e2780`'s join, the visitor window's thought; an item the table lacks keeps its
`.sam` name there, where the original letters nothing.

**Buy Land is item 101 and Clear Land is 102**, both `WhichUIType` 4 — which is exactly *why*
`FUN_004aaf70` appends them as synthetic rows **-1** and **-2** on tab 3 rather than finding them in the
walk. The Buy Land price is `Costs.MapCell`; the executable ships no default for it and the four
sibling cell prices, so they are all zero in the image and filled from the balance file.

## The shop, sideshow and ride windows: load, apply, ranges and the buy screen's gauges (fork watch 2026-10-16)

Read from the listing (`verify-ui-hud.md` H1 and M3); no game run. The three windows are objects with vtables ride `0x006ffbe8`,
shop `0x006ffc68`, sideshow `0x006ffce8`: slot `+4` load (ride `0x004ae940` over `0x004af030`, shop `0x004b0950`, sideshow
`0x004b1ee0`), `+0x14` refresh (shop `0x004b01d0`, sideshow `0x004b16c0`), `+0x3c` apply (ride `0x004af440`, shop `0x004b0aa0`,
sideshow `0x004b20b0`). Window objects are globals: shop `0x007cc258`, sideshow `0x007cc298` (pending values at `+0x24`, `+0x28`,
`+0x2c`), ride `0x007cc218` (`+0x2c`, `+0x30`, `+0x34`). The selected object is `[0x007c2658]`.

- **Base proc `0x0048d200`**: message `0x14` (close) calls the apply slot only when an object is selected; key down `0x2600` and
  `0x2800` (the cursor Up and Down) call `0x0048cbe0` and `0x0048caf0`, which apply first, then step by the class mask and, on a
  different object, load it.
- **Shop** apply: `+0x28` to `0x004e21e0` (the amount of special ingredient, `+0x198`); the byte `+0x24` x 50 to `0x004e2190` (quality,
  `& 0xff` into `+0x18c`); `+0x2c` to `0x004e2770` (price, `+0x194`). Load: `0xc082` the door; `0xc06b` <- byte `+0x18c` / 50;
  `0xc086` <- byte `+0x198`, shown with label `0xc084` only when `0x004e2200` names an ingredient (item `+0x158` 1 to 4 picks string 0 to 3
  of the table `[0x0078ba8c]`); `0xc085` <- `+0x194`. Ranges: `0xc06b` 0 to 2, `0xc086` 0 to 100, `0xc085` 0 to 500, step 1; previous and
  next `0xc069`, `0xc06a` hidden under two shops. Buttons: `0xc068` delete, `0xc080` move, `0xc081` apply to all, `0xc082` door.
- **Sideshow** apply: the price (`+0x2c` to `0x004e2770`) always; then only when the item's `+0x10c` is 0, the byte `+0x24` to
  `0x004e21c0` (chance of winning, `& 0xff` into `+0x190`) and `+0x28` to `0x004e1a20` (cost of goods, the prize, `+0x188`).
  `+0x10c` is `UsageInfo.NonWinnableSideshow`; no shipped `.sam` sets it (56 loose files and all 312 wads), so the hidden branch (controls
  `0xa090`, `0xa08e`, `0xa0a6`, `0xa0a7` hidden, label UITEXT `0x34`) is dead by content: every sideshow window shows the chance
  slider, the prize box and UITEXT `0x33`. Load `0x004b1ee0`: `0xa08a` the door, `0xa08e` <- `+0x190`, `0xa08f` <- `+0x194`
  (price), `0xa090` <- `+0x188` (prize). Ranges: `0xa08e` 0 to 100, `0xa090` 0 to 10000, `0xa08f` 0 to 500; `0xa08c`, `0xa08d`
  hidden under two. Buttons: `0xa08b` delete, `0xa0a8` move, `0xa0a9` apply to all, `0xa08a` door.
- **Apply to all**: shop `0xc081` (`0x004b0dba`, `0x00481bc0( 0x200, 0x004b0ae0 )`), sideshow `0xa0a9` (`0x004b23b3`, **mask `0x100`**,
  `0x004b20f0`); the mask runs `0x00482040` over every thing of kind 3 whose item's `AddOn.UpgradesId` (`+0x284`) is 0 and whose
  `Info.WhichUIType` (`+0x4ac`) is 1 (shop) or 2 (sideshow); the callback acts only when the thing's item id (`+0xe`) equals the
  selected object's, so it reaches every object of **the same item**, not every shop.
- **Ride** load `0x004af030`: the speed control `0x3e30` ranges over the item's `+0x134` to `+0x138` (value thing `+0x58`); the gauge
  `0x3e2e` is `Upgrades[level].RedLineSpeed` (`+0x1ac` + `0x40` x the thing's byte `+0x50`) << 10 / (max - min); the duration
  `0x3e2f` shows only when item `+0x70` is non-zero (range `+0x12c` to `+0x130`, value byte `+0x5c`); the capacity `0x3e2d` (and
  panel `0x3e31`) is hidden when `+0x124` equals `+0x128`. Apply: `+0x2c` to `0x004dd6e0`, byte `+0x30` to `0x004dd7f0`, byte `+0x34`
  to `0x004dd720` only when item `+0x70` is set.
- **The buy screen's stats** `0x004abb00` (not a Ghidra function): the item by `0x00412e90( 0x007890a0, id )`. On tab `[0x007cc1fc]` 0, gauge `0x1ee`
  <- item `+0x13c` x 1024 / 100 (`UsageInfo.ExcitementLevel`), text `0x1f2` <- `+0x19c` (`Upgrades[0].RedLineCapacity`, `%d`), gauge
  `0x1ef` <- `+0x1b0` x 1024 / 100 (`Upgrades[0].WearRate`); tab 2 only gauge `0x1ee`; other tabs none; text `0x1f5` <- `0x004ca8c0`'s
  count, or UITEXT `0x88` when 0. Which tab value is rides is inferred. The hire gauge `0x2482` is the candidate's byte `+8` x 1024 / 5
  (`0x0049b8c3` to `0x0049b8f8`).

### The shop's window (Q31)

Built (`ParkShopWindow`), read in Ghidra and compared with the original over the stock Drinks Shop (`q31/orig`, rounds
b and c; `q31/side-by-side.png`). Named in Ghidra: `ShopWindow_Build` `0x004b0e30`, `ShopWindow_Open` `0x004b0640`,
`ShopWindow_Load` `0x004b0950`, `ShopWindow_Labels` `0x004afc10`, `ShopWindow_Refresh` `0x004b01d0`, `ShopWindow_Handler`
`0x004b0b30`, `Cell_LocalHappiness` `0x004cd880`, `Object_IngredientName` `0x004e2200`. The stream `0x00755908` walks (`lib/uistream.py`) to a balanced close at `0x00755e7e`.
Builder `FUN_004b0e30( shop )` keeps the shop in `[0x007cc288]`, calls the base `FUN_0048cea0( stream, FUN_004b0b30, 0xc06c,
0xc06f, 0xc06d )`, then posts the advisor message `0xd1` (209: response 428, sample 397, a Tutorial line; the destroy, message
`0x14`, withdraws it with `FUN_00486b40( 0xd1 )`). The base builds nothing while a window of the class is up
(`param_1[4]`), so a second shop's click leaves the window where it is; who writes `[0x007c2658]` for that click is not traced.

- **Open, vtable `+0` (`0x004b0640`).** Both arrows hidden when `FUN_0048ca00( shop )` counts fewer than two; quality's slider
  `0xc06b` 0 to 2, the amount's `0xc086` 0 to 100, the price spinner `0xc085` a money painter (`FUN_00490250`) 0 to 500 step 1,
  font 6, black, centred; `0xc07e` UITEXT 39 "Quality of goods", `0xc084` the ingredient's name, `0xc083` UITEXT 40 "Sale
  price", each black and centred; the title `0xc06e` through `FUN_0048d290` (`UI_SetTitle`: font 5, (234, 239, 102), shadow).
  Neither slider has an op 3, so each track is its own rect: the original's thumbs reach both ends of the control (b-2, b-3).
- **Labels, vtable `+0xc` (`0x004afc10`).** The left cells take UITEXT 32 to 38 in the rows' order (`0xc075` Customers last
  month, `0xc071` Cost of goods, `0xc072` Takings last month, `0xc073` Profit last month, `0xc074` Customer satisfaction,
  `0xc07a` Scrap value (row 37, the ride's is 23), `0xc07b` Local happiness); `0xc070` is a label, `0xc076`, `0xc077`, `0xc078`
  and `0xc07d` money painters; `0xc079` a bar in `ridestatbar.wct` starting at 50, `0xc07c` one in `happygrad.wct` starting at
  100, both inset 10.
- **Load, vtable `+4` (`0x004b0950`).** The name (`+0x34`), the preview (`+0x18`, `0x004afb70`: the item's model `+0x490`), the
  refresh, the door `0xc082` down where `0x004e0440` (`+0x68` nought) says shut, the quality slider at `+0x18c`'s low byte / 50,
  the amount at `+0x198`'s, and the price at `0x004e1a00`; each value setter posts `0x800`, so the buffered values follow.
  The amount's slider and its label show only where `FUN_004e2200` names an ingredient: the item's `+0x158` 1 to 4 as row
  0 to 3 of `INGREDIENT.str` (the boot's table at `[0x0078ba8c]`, `+0x55c` of `0x0078b530`): "Amount of fat", "Amount of
  salt", "Amount of ice", "Amount of sugar".
- **Refresh, vtable `+0x14` (`FUN_004b01d0`).** `0xc070` UITEXT 460 "{P25} of {P26}": P25 the customers ring's last thirty
  finished days, P26 those plus the walk-aways ring's (the rings are told apart by their asserts on `+0x228` and `+0x2b0`);
  `0xc076` `FUN_004e1a30( pending quality x 50, pending amount )`; `0xc077` the takings ring's thirty days (`+0xf0`); `0xc079`
  `(FUN_004e1e30 << 10) / 100` (no low byte); `0xc078` `FUN_004e1c70`; `0xc07d` `FUN_004e2400`; `0xc07c` `(FUN_004cd880( cell, 1 )
  << 10) / 100`; then `FUN_00485f60( shop, 2 )`, and the status box (`+0x24`) only when it changed. The handler reruns it on
  each slider's `0x800`, after apply to all, on the door and on timer `0x80080` (4000 ms, armed on `0x15`).
- **Local happiness, `FUN_004cd880( cell, r )`.** The things on every cell from x - r to x + r and y - r to y + r, the far ends
  held to 127 and the near ones not (the packed id is cut to a word, so x - 1 at x 0 is the row above's last cell); each thing
  of kind 1 adds its `+0x19c` happiness, `__ftol`'d, low byte; the sum over their count, nought with none.
- **Handler `FUN_004b0b30`.** `0xc067` (`b_allthings`, help 34) calls `FUN_00495aa0( 1, shop )`: the open park screen closed,
  the all-items screen on its shops tab, the shop's row picked (`FUN_00664d2a`, `FUN_00665739`). `0xc069` (`b_ardown`) is
  `FUN_0048cbe0` and `0xc06a` (`b_arup`) `FUN_0048caf0`, the other way round from the ride's meshes: each applies first
  (`+0x3c`), then takes the next shop towards the newest (`0x00482da0`) or the oldest (`0x004824d0`), round from the far end,
  through `FUN_004828f0( 1, current )` over the used-thing list (`0x00516130`, `0x00516160`): kind 3, the item's `+0x284`
  nought and `+0x4ac` 1. `0xc068` is delete, `FUN_0048cd10` (`park-engine.md`, the confirmation box). `0xc06b`'s `0x800` keeps
  the index and sets the type-13 control `0xc07f` to `1 << index`: its setter (`FUN_0066c85a`) passes each op-0x10 child its bit
  (`FUN_0065d3a3`), and the child's mesh (`l_low`, `l_med`, `l_high`) shows its `on` texture for a set bit, `off` otherwise.
  The rest as listed above (Q177's note: `0xc081`, `0xc082`, the setters).

**Measured in the original** (Proton, `q31/orig/run.sh` round b, `run2.sh` round c, the stock park): the window over the Drinks
Shop (thing at `[0x007c2658]`, item 1203) reads "0 of 0", "$ 20", "$ 0", "$ 0", the satisfaction bar half, scrap "$ 325", price
"$ 30", buffered 1, 50, 30; the amount's thumb dragged to the left end and the quality's to the right give 0 and 2, "$ 30" and
the high icon lit, and OK writes `+0x18c` 100, `+0x198` 0; five clicks on plus make 35 and "$ 35" (the cost unchanged); the
door makes `+0x68` 0 and a grey "CLOSED" box over the preview, and again 1 with no box; OK writes `+0x194` 35. The two
readouts under the amount and the price are lettered over their panels, though the stream makes them first.

## The Park Status screen's Top 3 Thoughts (fork watch 2026-10-19)

Read from the listing of `testme.exe`; no game run (fork watch 2026-10-19, `verify-other-b.md` H2). The screen is `PARK_STATUS_SCREEN`,
not built (Q714).

- **The ranking, `0x004c8430`.** It zeroes 23 counters and walks the thing list `[0x0080239c]` (`0x00516130`, `0x00516160`):
  a thing whose type byte (`+2`) is 1 and for which `0x004fa990` answers true adds one to `counts[ thing + 0x30 ]` (its
  thought). `counts[ 0 ]` is then set to nought, and three slots are filled in turn: for each id 3 to `0x16` the slot takes the
  id whose count most exceeds the slot's own, strictly above nought and the first of equals winning (`JLE`); a slot left with
  a count of nought is written `0x17`. Slot 0 goes to the first output argument, 1 to the second, 2 to the third.
- **Whose thought counts, `0x004fa990`.** The thing's cell (`[+7] x 128 + [+5] + 1`, record `[0x008023a0] + cell x 0x44 - 0x44`)
  must have a type byte (`+8`) of 0, 1, 3, 9 or 10 (tests `0x00536310`, `0x00536320`, `0x00536340`, `0x00536350`, `0x00536390`).
- **The icons, `0x004a4ab0`.** Slot 0 sets the control `0x4a68d4`, slot 1 `0x4a68d3`, slot 2 `0x4a68d2`. An id 1 to `0x16` sets
  the icon's frame to id - 1 (vtable `+0x1c`); 0 or `0x17` and over calls `0x0065d9dd( 0 )`. The table `0x007537d0` puts them
  (type 6; records `0x00753bd2`, `0x00753bfa`, `0x00753c22`): `0x4a68d2` at x 305 to 409, `0x4a68d3` 432 to 536, `0x4a68d4`
  562 to 666, all y 646 to 726. So **slot 0, the commonest thought, is the rightmost icon**, slot 2 the leftmost.
- **Hover, `0x004a4700`.** Message `0xc` sets the help to row (the icon's stored frame + 1) of `THOUGHTS.str` (`[0x0078ba9c]`,
  `0x006ace60`): each icon names its own thought (the type-6 setter `0x0066da4d` stores `+0x13c`, the getter `0x0066dad2` reads it).
- **Refresh:** the builder arms a timer of `0x3e8` (1000 ms) with id `0x80083` (`0x004a5309`).
- **Not proven:** whether an icon hidden by `0x0065d9dd( 0 )` is ever shown again (neither setter calls a show; the window proc
  `0x004a4830` was not read).
