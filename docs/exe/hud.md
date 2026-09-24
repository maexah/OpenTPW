# The park HUD, from the executable

Everything the park's interface is made of, read out of `testme.exe`: the 16-bit **layout stream** opcode
format that is compiled into the executable (there is no layout file), the mesh-name hash that binds a
control to a model, the assembly function that builds a park's interface, each park panel's stream with
its control ids, rects, artwork and help rows, the six management buttons' real dispatch, and the park map
screen. Read with the park-engine page, which holds the 17 shortcut actions, the 54-row screen map and the
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
    op 8     [rect] slider thumb, type 3, installs SliderThumb_Callback, RECURSE
    op 9,10  [rect] CONDITIONAL - read only if the branch is taken
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

**Ops 9 and 10 are runtime-conditional.** They read their rect *only* if a branch is taken, so a static
walk must assume one path. None of the park panels uses them; a screen that does cannot be dumped
statically without deciding the condition first.

### Which control an attribute binds to

`case 0` recurses with **ECX = the new child** (`0065fe4c: MOV ECX,[EBP-0x30]`), but `case 1`, `2`, `3`,
`0x11` and `0x12` all load **ECX = `[EBP+0xffffff60]`, the RUNNING invocation's own `this`**
(e.g. `0065fe75`). So a mesh, skin, text rect or help row binds to **whichever control's block encloses
it** — and after an `op 5` that is the **parent**, not the child that just closed.

**The trap:** a dumper that prints attributes without marking the closes makes a parent's mesh look like it
belongs to the child printed just above it. That is how the `guage` mesh was first recorded on the meter
`0x1f` when the bytes put it on its housing `0x1e` (`... 05 00 | 01 00 9f 57 d3 69 | 05 00` at
`0x007529a0` — close, THEN mesh, THEN close). A static walker **must** track op 5 to attribute
attributes, because `case 0` recurses and `case 5` returns: that is why `guage` lands on `0x1e` and `base`
on `0x1d` though each textually follows a later id. Always print CLOSE markers and bind by an explicit
stack; attributing by "the id that precedes it in the bytes" is wrong.

### The self-check that proves the decode

Every stream address from the 54-row screen table begins with **op 0** and the walk **terminates on a
balanced op 5**. Six streams walked, six clean. If a walk ends mid-record or hits an unknown opcode, the
decode is wrong — do not "fix" it by skipping bytes.

## The mesh-name hash — cracked, 39 of 39

    h = 0
    for each character c of the name:  h = ((c ^ h) * 47) & 0xFFFFFFFF

The name hashed is **the full, untruncated name of the model's FIRST mesh node** — not the member name,
not a clamped one. `w_dialog_wave.MD2`'s first node is `wdialog w` -> `wdialogw`; the truncation already
happened when the artist named the node, so **do not clamp it yourself**.

Hashing the 1,202 `ui.wad` **member names** instead gives **zero** hits across five inits and four
transforms — the wrong name source. Node names come from `wadcat --anim <wad>` (the C# tool in `CLAUDE.local.md`, not the `wadcat.py` harness below); its `--meshes` mode is
**still broken**.

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
4. Then the six panels, each `UI_LoadTree` + `UI_SetVisible(0)` — **all built hidden**.

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

Built by `FUN_004a1d70`, handler `FUN_004a0a20`. Four root controls:

    0x1d  panel  ( 37, 984)-( 439,1507)   the bottom-left gadget body      mesh base
      0x1e  panel  help 477                                                 (gauge housing)
        0x1f  t9   ( 85,1100)-( 144,1324) mesh guage   + meter.wct skin
      0x20  panel  help 478  + text rect  mesh date    <- THE DATE READOUT
      0x21  panel  ( 331,1069)-(1006,1495)  THE ARM   mesh pan_money  = panel.md2
        0x22  panel (1006,1069)-(1071,1382)  its end   mesh pane_money = panelend.md2
          0x23  panel ( 976,1058)-(1129,1503) handle   mesh handle     = handle.md2, 16-point region
        0x24  button ( 349,1386)-( 429,1466)  help 481  mesh b_retract  (child of 0x21)
      0x25  panel  (170,1122)-( 392,1372)  THE SIX ROUND BUTTONS
      0x2c  panel  help 494
    0x2d  panel ( 94, 918)-( 144, 984)    0x2e  help 476, meshes aerial + aerialtop  (77,790)-(161,918)
    0x2f  panel (258,  60)-( 720, 260)    0x30 (458,273)-(720,363)   0x31 (728,109)-(831,211)
                                          0x32 ( 37, 58)-(242,262) mesh i_dollar
    0x33  panel (1688, 48)-(1963, 278)    0x34 gkey (1853,165)-(1955,268)  0x35 gtick (1853,54)-(1955,156)
                                          0x36 (1688,171)-(1829,261)  0x37 (1688,60)-(1829,150)

### How `FUN_004a1d70` populates the clusters

| Control | What the builder does | Evidence |
|---|---|---|
| `0x36`, `0x37` | **Text labels**, `LabelSkin_Construct`, colour **white** (0xff,0xff,0xff,0xff), font `FUN_00485a70(2)`, both seeded with the string at `0x00752f24`. They sit to the LEFT of the two icons, so the cluster is icon+count twice | Disassembly of `FUN_004a1d70` |
| `0x34` (`gkey`) | Takes **frame 3** (`FUN_0065d3a3(3)`) — what the four `gkey0-3.wct` textures are for — and **is hidden outright in Instant Action** (`DAT_00fb3b7c == 2` -> `UI_SetVisible(0)`) | Disassembly |
| `0x30` | **Yellow** (0xff,0xff,0,0xff), font 2, and **starts hidden** (`UI_SetVisible(0)`) — matches it being the price of the item in hand, absent when your hand is empty | Disassembly |
| `0x2f` | Its field is sized by **measuring the string `999999999`** (`UI_MeasureText` on the string at `0x00752f10`), white, font `FUN_00485a70(1)`, skin `FUN_0048fde0(0)`; `0x32`'s rect is then computed from that measurement rather than being fixed | Disassembly |
| `0x2d` / `0x2e` | Callbacks `LAB_004a11e0` and `FUN_004a1190`; the aerial is repositioned by `sStack_32 - iStack_3e` — it moves against its neighbour rather than sitting at its stream rect | Disassembly |
| `0x1f` | Takes the `meter.wct` skin | Confirmed twice, independently |
| `0x20` | The date is **black** — `FUN_0065c5d5(0,0,0,0xff)` with font slot 3 (`_DAT_007cb2d4 = 3`) | Confirmed twice, independently |

### Depth

`FUN_0065f16b( this, n )` writes n to the control's `+0xe0`, clamps at `0x100`, and recurses into children
at n+2. The receivers are `__fastcall` and **Ghidra hides them** — read the disassembly:

    0x21 arm 4   0x22 end 4   0x24 b_retract 7   0x1d body 8   0x23 handle 9   0x25 six buttons 10

**Higher is nearer the front**, settled by screenshot: the six buttons sit at 10 inside a body at 8 and
plainly draw over it. So the arm draws **behind** the body. Built the other way it lays a hard seam across
the panel.

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

**The HUD is six category pickers, not 17 buttons.** `FUN_004a0940( n )` opens *the screen that category
was last left on*, from three globals seeded 1 / 3 / 10 in `FUN_004a1d70`:

    n=1  DAT_007cb28c  1 -> buy FUN_004acc70      2 -> hire FUN_0049bdd0
    n=2  DAT_007cb2a0  3 -> parkstatus  4 -> allstaff  5 -> allitems  6 -> allpeeps
    n=3  DAT_007cb290  7 -> financeinfo  8 -> loans  9 -> staffcosts  10 -> entryprice

It does nothing at all when `FUN_0048c8d0()` returns 1, which needs **both** `DAT_007c2534` non-zero
**and** `FUN_006ad810()` non-zero - that is, while the game menu exists and is on screen (see "The gate that looks
like a bug, and is not"). This is why the 17
shortcut actions and the gadget's button count never reconciled — they are not meant to.

### The nine screens behind Info, Money and Research — censused 2026-09-21

Every one located, with its builder, its layout stream, its refresh callback, the size of the builder
and the control count parsed out of its stream. **The control counts are LOWER BOUNDS**: the stream
walk misses children of composite controls (a list's own column headers never appear).

| # | Screen | Builder | Stream | Callback | Builder bytes | Controls |
|---|---|---|---|---|---|---|
| 3 | parkstatus | `FUN_004a52a0` | `0x7537d0` | `0x4a4830` | 2926 | 38 |
| 4 | allstaff | `FUN_00496620` | `0x750e10` | `0x495da0` | 1438 | 37 |
| 5 | allitems | `FUN_00495aa0` | `0x7508e0` | `0x495290` | 342 + four sub-builders | 11 |
| 6 | allpeeps | `FUN_00493530` | `0x7506c8` | `0x493230` | 677 | 5 + 6 columns |
| 7 | financeinfo | `FUN_0049ac60` | `0x751ca8` | `0x49a0b0` | 2238 | 18 |
| 8 | loans | `FUN_0049fb30` | `0x7525a0` | `0x49f4e0` | 1905 | 18 |
| 9 | staffcosts | `FUN_004b2750` | `0x756400` | `0x4b24b0` | 2990 | 16 |
| 10 | entryprice | `FUN_00498d80` | `0x751798` | `0x498c60` | 721 | 7 |
| — | research | `FUN_004aa480` | `0x754490` | `0x4a96b0` | 2616 | 34, six sliders |

What each shows, from its own decoded labels:

- **parkstatus** — People in park / Arrival rate / Average happiness / Average time in park / Park
  rating, Top 3 Thoughts, Happiness Levels (`hap.wct` and `sad_undec.wct` gauges), Total park
  visitors, and a 1/3/12-year graph.
- **allstaff** — a five-column list: Name / Current Status / Monthly Wage / Skill / Happiness, plus
  average-happiness gauges.
- **allitems** — four tabs of placed objects, the same four categories the buy screen uses.
  **The four tabs do NOT share a list shape** — see below.
- **allpeeps** — a six-column guest list: Visitor Number / Cash Remaining / Time In Park / Rides
  Ridden / "?" / Happiness.
- **financeinfo** — Bank balance / Park value / Money in / Gate takings / Shop takings / Sideshow
  takings / Money out / Staff costs, over the same year graph.
- **loans** — "Available Loans": Lender Name / Amount / Interest Rate / Monthly Repayment.
- **staffcosts** — "Staff Training Budgets": Cash in / − Staff costs / − Other costs / − Loan
  payments / Balance.
- **entryprice** — one "Ticket Price" row. It writes the park object's `+0x118` through
  `FUN_004d05d0`, whose format string reads *"Admission fee set to %d"*.

### The four screens built 2026-09-21, and what walking their streams alone could not give

**Column headings are in CODE, not in the layout stream.** Every `op 0xc` header child carries a rect
and nothing else; each builder then fetches it by id **`0x10 + index`** and hands it a UITEXT row
through `FUN_00485b00( row, …, sortMessage )`. A stream walk therefore yields the right number of
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

`FUN_006636b2( column, rightAligned )` sets alignment per column: allstaff passes 0,0,1,1,1 and
allitems 0,1,1,1,1, but **allpeeps passes 1 for all six** — its name column is a visitor *number*.

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
- **research** — six effort sliders, plus *"You need to hire some scientists before you can carry out
  any research!"* and *"Research is automatic in Instant Action mode."*

**Four are built and five are blocked on simulation this project does not have.** Built: **entryprice**,
**allpeeps**, **allstaff** and **allitems**. Blocked: **loans** (the `mLoans[]` records), **financeinfo** (the
money-in/out split and the graph history), **staffcosts** (training budgets, other costs, loan payments) and
**parkstatus** (Top 3 Thoughts, arrival rate, park rating, multi-year history), each counted when opened
(`LOANS_SCREEN`, `FINANCE_SCREEN`, `STAFF_COSTS_SCREEN`, `PARK_STATUS_SCREEN`), and **research** (`mResearchDone`,
`mResearchGroup`, `mFirstResearcher`, and per-group research points), whose gadget button only logs
(`ParkGadget.NotYet`, no counter yet - `docs/QUEUE.md` Q69).

So the three buttons are not one job: **Info has 3 of 4 built, Money 1 of 4, and Research is
none.**

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
`0x125` decodes there as *"Left-click to view item's details"*. **Unresolved**, and the likely table
is `TAG_SYSTEM.str`.

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

### Three ways out of camcorder mode: Escape, the C key, and the eject button

`FUN_00488a00`'s key-up case treats `key == 0x1b` (VK_ESCAPE) and `action == 0x10` (camcorder, shortcut
16) the same way: it sets layer 1's cursor to 1 (`c_busy.ani`, `0x00489750`), leaves first person
(`FUN_0042ae70` or `FUN_0042a190`), and sets the cursor back to `0x15`. **`FUN_0065f11d` is a cursor setter, not a
page switch**: it stores the id at the control's `+0x50` and applies it at once if the pointer is over the
control (`FUN_006590f7`).

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

### It can never be fed, so do not build it

Every message arrives through `FUN_00481580`, whose **one** caller is `CTagSystem::ReceiveMessage`
(`FUN_00509bf0` — its own debug string names the class). The only branch whose contents are readable is
**postcard status**: ids `0x17b` / `0x17c` / `0x17d` for "Postcard sent", "Postcard to outbox", "Postcard
failed". The other two branches (kinds `0x18`, `0x19`) take an event id from the simulation and look it up
through `FUN_0050af40`. `FUN_0048ef70` is the **save-file loader**, not a live poster — it reads
0x21c-byte records through `FUN_005f5e20`. The cap is **ten** (`DAT_00750610`, with the string `mb_erase`
immediately after it at `0x00750614`).

**Advisor lines do NOT land here.** `FUN_00486b00` runs into the advisor's own message system
(`FUN_0059b590`, `FUN_0059a940`); the bar keeps a separate list (`DAT_007cb2c8`, posted by
`FUN_0048eb20`). They talk in one direction only: showing a bar message (`FUN_0048d9c0`) posts advisor
line **0x11c** and calls `FUN_004a2590` to put the bar on the arm.

State globals: `DAT_007ca098` the bar's tree, `DAT_007ca090` the message on show, `DAT_007ca09c` how many
are held. Handler `LAB_0048e2f0` (not a Ghidra function): 0x10d30 and 0x10d2f go to `FUN_0048e0f0` — a
jump table that opens whichever screen the message is about — and 0x10d2e erases the current one and
retracts the arm via `FUN_004a25f0(0)`.

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
`b_buy` vs `UIStrings.ParkClosed = 469`" is **not** a contradiction — that flag is **withdrawn**; the two
tables simply share index space.

**Reading a `.str` outside the game needs the file system mounted FIRST.** `BFSTReader`'s lookup table is a
static `Lazy<BFMUReader>` over `Language/English/MBToUni.dat`, opened through `FileSystem.OpenRead` on the first
string decoded. A `StringFile( Stream )` constructor does **not** avoid it, because it decodes as it constructs.
Assign `FileSystem = new BaseFileSystem( <data dir> )` before constructing anything. Otherwise the first read
throws, and with stderr filtered that looks like "the rows are empty".

## The park map — `FUN_005f0b40`, stream `0x00774da0`

**It loads the shipped image *and* renders live over it** — both, not one or the other.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_005f0b40` | — | The map screen's own function, opened by `b_map` | Gadget dispatch |
| `DAT_00f86d20` | — | The static string `2dmap.tga`, constructed at `0x005f0ad0` with a destructor registered through atexit. **Not map logic** — reading it as part of the screen is an adjacency error (`FUN_005f09a0` ends at `0x005f0ac7`, then NOP padding) | Disassembly |
| `FUN_005f1ea0` | — | Builds a path (note the `0x5c` backslash), passes `&DAT_00f86d20` to a vtable method, stores the image's width and height into `+0x48` / `+0x4c` | Disassembly |
| `FUN_005777b0` | — | Rounds those dimensions up to 64 — `(w+0x3f)>>6` — and allocates `tilesX*tilesY` records of 0x60, each a 64x64 surface. For 512x512 that is **8x8 = 64 tiles** | Corroborates that the loaded image is the shipped TGA |
| `+0x60` | — | A **128x128 grid of classification codes**, 8 bytes a cell, in the screen's structure; `+0x60 + 0x20000 = +0x20060` is the selection field | Disassembly |
| `FUN_005f2050` | — | Fills the grid: **9** out of bounds, **10/11/12** terrain kinds, **1-8** things by type (`+0x4ac`) and flags (`+0x32`) | Disassembly |
| `FUN_005f2380` | — | Paints it: a 9-dword (36-byte) blit descriptor per cell, then outlines footprints by comparing each cell with its neighbour above/below (vtable+8) and left/right (vtable+0xc), then draws a selection highlight for the item at `+0x20060` | Disassembly |

The base image is the park's own **`2dmap.tga`**, 512x512 24-bit, one per theme (fantasy, hallow, jungle,
space).

### It is a heat map, and that is the scope trap

`FUN_005f09a0` is **not** a sprite selector — it is a **three-stop colour ramp**: clamp a value to 0-100,
blend stop1->stop2 below 50 and stop2->stop3 above, patch R/G/B into the descriptor and force alpha 0xff.
The stops are `DAT_00f86bd8`, `DAT_00f86bfc`, `DAT_00f86c20` (runtime-filled — beyond `.data`'s raw bytes,
so a static read gives zeros). The value is a **per-thing metric** fetched differently per radio setting
(`FUN_004e0860`, `FUN_004e1e30`). Those metrics are simulation values that do not exist yet, so the thing
layers are simulation work wearing a UI. The base image plus terrain codes 9-12 is the honest first cut.

### Controls

Six toggles — **0x982, 0x987, 0x989, 0x993, 0x994, 0x983** — each restored from a bit in `+0x18`
(8, 1, 0x10, 2, 4, 0x20) and each gating one code in the painter: they are **layer filters**. Two radio
groups: **0x98b** with options 0x98e/0x98f/0x98c/0x98d from `+0x1c`, and **0x996** with 0x998/0x997/0x999
from `+0x20` — the **metric pickers**. **0x991** is a label, font 4, **yellow** (0xff,0xff,0,0xff).
**0x990** and **0x995** are display-only (vtable+0x14 with 0), and 0x990's depth+2 is handed to the six
toggles. **0x992** takes handler `LAB_005f0b20`; 0x98a/0x984/0x981/0x988 get `MoveToXY(0,10)`.

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
line **0x130** as it opens, and refuses to open at all if the game menu is up (`FUN_0048c8d0`) or if it is
already open.

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

0x996 is the METRIC picker, bottom right (1526,1409)-(1868,1512), restored from `+0x20` as 1/2/3, and it
wears mesh **map_win (w_map.MD2)** — bound AFTER its children close, so by the op-5 rule it belongs to the
group and not to 0x999:

    0x999 b_ssatis (1526,1409)-(1629,1512)  291/292  customer satisfaction
    0x998 b_shap   (1646,1409)-(1748,1512)  287/288  happiness
    0x997 b_sexcit (1766,1409)-(1868,1512)  289/290  excitement ratings

The help rows confirm the heat map in the game's own words — "excitement ratings", "customer
satisfaction", "ride reliability", "queue times" are all simulation values.

### The terrain codes, by exhaustion of the cell accessors

Each is a one-liner on a 0x44-byte runtime cell:

| Address | What it tests |
|---|---|
| `FUN_00536310` | kind == 1 |
| `FUN_00536320` | kind == 3 or 9 |
| `FUN_00536340` | kind == 9 |
| `FUN_00536350` | kind == 10 |
| `FUN_00536380` | kind == 4 |
| `FUN_005363e0` | kind == 0x15 |
| `FUN_00535db0(cell,mask)` | `*(ushort*)(cell+0xe) & mask` |

Read against `FUN_005f2050`'s nesting, kind 9 is ruled out before code 11 is reached, so:

    code  9 = outside the grid    code 10 = kind 1    code 11 = kind 3    code 12 = flags & 0x40

**UNPROVEN AND MUST NOT BE ASSUMED: that the runtime kind at `+8` IS the `.MAP` attribute byte.**
The park data-layout page records the attribute values as 0, 1, 3, 8, 17, 128, 144, 148 and says **only
the 0/1/3 cells differ per park** — the same two numbers the map's two terrain codes use, which is
suggestive and no more. The runtime cell is 68 bytes and the attribute is one byte. Find where `+8` is
written before drawing terrain from a parsed `.MAP` — or verify empirically by drawing kind-1 and kind-3
cells and checking them against the shipped `2dmap.tga`.

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

This does **not** prove the runtime kind at `+8` is the attribute byte — that is still untraced. What it
proves is that the two values the map's terrain codes use are the two that draw the park's own features,
which makes drawing codes 10/11 from a parsed attribute map defensible. **It does NOT license semantic
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
  window, or any left press under a modal one, from reaching the world (`Level.WorldClick`). A right press is not
  guarded yet (`docs/QUEUE.md` Q56). The camcorder key is guarded by `Level.Current?.PausedByWindow() != true`. A
  new HUD control that uses the wheel or a press must go through these.
- **HUD work is verifiable by eye and capture, not by test** — it needs someone to look at the running
  game.

---

## The buy and hire screens, and the list control under them

Decoded 2026-09-20 by an eight-dimension pass, every claim then re-derived by a second agent that was
told to refute it. **The refutations are part of the record**: several load-bearing claims did not
survive, and where a correction is noted below it is the corrected reading that is written down.

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
are dead by CONTENT, not by CODE.

**With flag `0x80` set — buy is `0x91`, hire `0x291`, both have it — `0x400` fires twice per click**,
once on press and once on release. The buy handler's first one closes the screen, so the second finds
the tree gone and is a no-op. **A re-implementation that fires once, on the press, behaves as the original
does (`UiList.PointerPressed`); one that copies the press path without the close-then-guard sequence purchases twice.** This is INFERRED,
and is worth confirming in the running game by holding a click on a buy row and predicting one carried
item before looking.

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
the park object (`FUN_00519510`), each stored as samples / index / count / wrapped:

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
filled once yet. **The balance row is last month's net, so the park's cash does not belong in it**; a
build that puts it there shows the same figure twice, once correctly as `0x2496` and once mislabelled.

**The two cost rows are red.** `FUN_0065c5d5( 0xff, 0, 0, 0xff )` is called while `0x248d`/`0x248c` and
`0x248a`/`0x248b` are the current control, and never for `0x2488`/`0x2489` or `0x2486`/`0x2487`.

**Nothing is selected when the screen opens.** `FUN_0049b5b0` fills the list and commits; it never
calls the select. The info panel, the skill bar and the portrait are blank until the player picks
somebody, so a build that opens with a filled panel is wrong. The row id pushed is the candidate's
SLOT INDEX (0 to 0x1f), which is what `FUN_00507bd0` takes back.

**The walk is corroborated three ways**: the tab help rows, the resolved mesh names, and each screen's
own tab-index switch all give the same ordering, and it matches UITEXT 119–122 and 139–143.

**The root frame mesh `0xf76e4200` is `w_big.MD2`, node `window4`.** The hash is over a node name, which is why
searching file stems never finds it; see the resolution table above for the method.

### The node name is not the file name — measured, after seven meshes failed to load

The decode above recovered these names by hashing and matching **substrings of file names**, and flagged
that as inferred. It is worth more than a footnote: the stream hashes the model's first **node** name,
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
the node and the file happen to agree — which is exactly what made the failures look arbitrary until
the archive was listed. **`b_up` and `b_down` remain unconfirmed**: both load, but `b_up` had three
preimages in the shipped data.

Read the archive with the `wadcat.py` harness (`<wad> list`; the harness folder is in `CLAUDE.local.md`) — a port of this tree's own
`WadArchive` and `Refpack`, validated against two values the codebase documents independently.

### The gate that looks like a bug, and is not — **now named**

`FUN_004a0940` does nothing when `FUN_0048c8d0()` returns 1, which needs both `DAT_007c2534` and
`FUN_006ad810()` non-zero. Both halves are named:

- `DAT_007c2534` is the in-game **Escape menu object** — a 0x14-byte MenuList built by
  `GameMenu_BuildPark` / `GameMenu_BuildLobby`. Non-zero means the menu exists.
- **`FUN_006ad810` is not a money function at all.** It is a two-byte COMDAT-folded accessor,
  `MOV EAX,[ECX+0xc]; RET`, shared by unrelated classes across 29 references — its meaning is entirely
  the receiver's. Here the receiver is the MenuList, so it is `MenuList::IsShown()`.

So **`FUN_0048c8d0()` means "the game menu exists AND is on screen"**, and the buy button refusing
while the pause menu is up is correct behaviour rather than an unbuilt path. The same function on the
bank Thing is what reads the balance, which is how it was mistaken for one.

**A caveat that bit the first reading of the teardown**: several menu arms do NOT clear
`DAT_007c2534` — those that open a confirm box leave it set until that box's own callback runs, so the
menu object outlives the menu on screen.

### What the buy list actually filters on

The list is built from a per-item descriptor array at `world+0x1d943c`, stride 0x20, 150 slots. The
four filter fields, as **byte** offsets:

    +0x508  the item's model failed to load
    +0x4AC  Info.WhichUIType      (4 = "not shown in the UI", the files' own words)
    +0x284  AddOn.UpgradesId      (must be 0 - a standalone item, not a ride's upgrade)
    desc+0x10  researched/available

**Two premises were wrong and are corrected here.** `desc+0x10` is not a static "is this listed" flag —
it is the **researched** flag, set at level start for items whose `Upgrades[0].CostOfResearch` is nought
and again the moment research completes. And `item+0xC4` is not a research countdown — it is
`Research.Group`, a **golden-ticket tier** that placing the item literally spends.

**The row state is {0,1,2} and both non-zero values are now pinned**: 1 = you already own at least one
(`desc+0x18`, incremented on placement and decremented on demolition), 2 = one of the three most
recently **RESEARCHED** items in that tab — a 3-entry ring per tab fed only by the research-complete
message, **not** by building. The game's own help row 146 says the same thing: "sort the list by items
already owned or recently researched".

**The mystery row**: when `Research.Group > 0` and the item is not yet unlocked, the name becomes
UITEXT 137 "??? Mystery Ride! ???" and **the price column becomes the NEGATED group value**. There is
also a second, unpriced acquisition gate on that path — such an item is bought against a golden-ticket
count rather than against cash, and that arm is NOT TRACED.

**Buy Land is item 101 and Clear Land is 102**, both `WhichUIType` 4 — which is exactly *why*
`FUN_004aaf70` appends them as synthetic rows **-1** and **-2** on tab 3 rather than finding them in the
walk. The Buy Land price is `Costs.MapCell`; the executable ships no default for it and the four
sibling cell prices, so they are all zero in the image and filled from the balance file.
